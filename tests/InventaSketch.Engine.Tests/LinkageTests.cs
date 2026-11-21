using System;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Engine.Tests
{
    /// <summary>Self-built linkages: links docked on joints and pivots, degree of freedom, dragging (<see cref="Linkage"/>).</summary>
    public class LinkageTests
    {
        private static readonly Registry Reg = ModuleCatalog.CreateDefault();

        private static SketchDocument Doc(params SketchObject[] objects)
        {
            var doc = SketchDocument.Create();
            return Edits.Add(doc, doc.Pages[0].Id, objects);
        }

        private static SketchObject Pivot(Vec2 at)
        {
            var g = Reg.Create(Reg.Library.First(i => i.TypeId == "kin.joint.ground"), at);
            var doc = Doc(g);
            var c = Reg.PageAnchors(g, new RenderContext(doc)).First(a => a.Role == AnchorRole.Port);
            return g with { Transform = g.Transform with { X = g.Transform.X + at.X - c.P.X, Y = g.Transform.Y + at.Y - c.P.Y } };
        }

        private static SketchObject Link(Vec2 a, Vec2 b, Attachment? atA = null, Attachment? atB = null)
            => Reg.Create(Reg.Library.First(i => i.TypeId == "kin.link.binary"), Vec2.Zero) with { Points = new[] { a, b }, Attach = new[] { atA, atB } };

        private static Attachment OnPivot(SketchObject pivot, Vec2 at)
        {
            var c = Reg.PageAnchors(pivot, new RenderContext(Doc(pivot))).First(a => a.Role == AnchorRole.Port);
            return Attachments.For(pivot, at, c)!;
        }

        private static Attachment OnJoint(SketchObject link, int i) => new(link.Id, 0, 0) { AnchorId = "j" + i };

        /// <summary>A four-bar built from parts: crank on pivot 1, coupler, rocker on pivot 2.</summary>
        private static (SketchDocument Doc, string Crank, string Coupler, string Rocker) FourBar()
        {
            var g1 = Pivot(new Vec2(50, 80));
            var g2 = Pivot(new Vec2(90, 80));
            var crank = Link(new Vec2(50, 80), new Vec2(50, 68), OnPivot(g1, new Vec2(50, 80)));
            var coupler = Link(new Vec2(50, 68), new Vec2(80, 55), OnJoint(crank, 1));
            var rocker = Link(new Vec2(90, 80), new Vec2(80, 55), OnPivot(g2, new Vec2(90, 80)), OnJoint(coupler, 1));
            return (Doc(g1, g2, crank, coupler, rocker), crank.Id, coupler.Id, rocker.Id);
        }

        private static double Len(SketchDocument doc, string id)
        {
            var p = doc.ResolvePoints(doc.Get(id)!);
            return p[0].DistanceTo(p[1]);
        }

        [Fact]
        public void ALinkEndDockedOnAJointFollowsIt()
        {
            var a = Link(new Vec2(0, 0), new Vec2(20, 0));
            var b = Link(new Vec2(20, 0), new Vec2(20, 20), OnJoint(a, 1));
            var doc = Doc(a, b);
            doc = Edits.SetPoint(doc, a.Id, 1, new Vec2(25, 5));
            Assert.Equal(new Vec2(25, 5), doc.ResolvePoints(doc.Get(b.Id)!)[0]);
            // Only the docked end shows a (closed) lock; the joint it sits on shows no extra open one.
            Assert.DoesNotContain(Links.For(Reg, doc, new[] { a.Id }), l => l.Released);
            Assert.Contains(Links.For(Reg, doc, new[] { a.Id }), l => l.Kind == LinkKind.Attached && !l.Released && l.A == b.Id);
            // Snapping a link end onto another link's joint port gives that attachment.
            var port = Reg.PageAnchors(doc.Get(a.Id)!, new RenderContext(doc)).First(x => x.Role == AnchorRole.Port && x.Id == "j1");
            Assert.Equal("j1", Attachments.For(doc.Get(a.Id)!, port.P, port)!.AnchorId);
        }

        [Fact]
        public void AFourBarFromPartsHasOneDegreeOfFreedomAndKeepsItsLengths()
        {
            var (doc, crank, coupler, rocker) = FourBar();
            var dof = Linkage.Dof(Reg, doc, coupler)!.Value;
            Assert.Equal(4, dof.Links);
            Assert.Equal(1, dof.Dof);
            double lc = Len(doc, crank), lk = Len(doc, coupler), lr = Len(doc, rocker);
            // Turn the crank by dragging its free joint round the pivot in small steps.
            for (var deg = -90; deg <= 0; deg += 10)
            {
                var a = deg * Math.PI / 180;
                doc = Linkage.Drag(Reg, doc, crank, 1, new Vec2(50 + 12 * Math.Cos(a), 80 + 12 * Math.Sin(a)));
            }
            var tip = doc.ResolvePoints(doc.Get(crank)!)[1];
            Assert.Equal(62, tip.X, 2);
            Assert.Equal(80, tip.Y, 2);
            Assert.Equal(lc, Len(doc, crank), 3);
            Assert.Equal(lk, Len(doc, coupler), 3);
            Assert.Equal(lr, Len(doc, rocker), 3);
            Assert.Equal(new Vec2(50, 80), doc.ResolvePoints(doc.Get(crank)!)[0]);
            Assert.Equal(new Vec2(90, 80), doc.ResolvePoints(doc.Get(rocker)!)[0]);
        }

        [Fact]
        public void ASliderCrankKeepsTheSliderOnItsGuide()
        {
            var g1 = Pivot(new Vec2(40, 80));
            var slider = Reg.Create(Reg.Library.First(i => i.TypeId == "kin.joint.prismatic"), new Vec2(90, 80));
            var probe = Doc(slider);
            var sc = Reg.PageAnchors(slider, new RenderContext(probe)).First(a => a.Role == AnchorRole.Port);
            var crank = Link(new Vec2(40, 80), new Vec2(40, 70), OnPivot(g1, new Vec2(40, 80)));
            var rod = Link(new Vec2(40, 70), sc.P, OnJoint(crank, 1), Attachments.For(slider, sc.P, sc));
            var doc = Doc(g1, slider, crank, rod);
            Assert.Equal(1, Linkage.Dof(Reg, doc, crank.Id)!.Value.Dof);
            var lr = Len(doc, rod.Id);
            doc = Linkage.Drag(Reg, doc, crank.Id, 1, new Vec2(50, 80));
            var end = doc.ResolvePoints(doc.Get(rod.Id)!)[1];
            Assert.Equal(80, end.Y, 3); // still on the guide
            Assert.Equal(lr, Len(doc, rod.Id), 3);
            Assert.True(end.X > sc.P.X, "the slider moved out");
        }

        [Fact]
        public void APivotPlacedAtAJointHasItsPivotPointThere()
        {
            var link = Link(new Vec2(50, 50), new Vec2(70, 50));
            var doc = Doc(link);
            var port = Building.Ports(Reg, doc, link.Id).Single(p => p.Id == "joint0");
            var pivot = Building.Place(Reg, doc, link.Id, port, Reg.Item("kin.ground")!)!;
            var withPivot = Edits.Add(doc, doc.Pages[0].Id, pivot);
            var c = Reg.PageAnchors(pivot, new RenderContext(withPivot)).First(a => a.Role == AnchorRole.Port);
            Assert.Equal(port.At.X, c.P.X, 6);
            Assert.Equal(port.At.Y, c.P.Y, 6);
            Assert.NotEqual(port.At.Y, pivot.Transform.Y, 3); // the pivot point is not the box centre

            // Docked on the pivot, the link end stays on the pivot point after a refresh (not on the box centre, also "c").
            var docked = Building.DockBack(Reg, withPivot, link.Id, port, pivot.Id)!;
            docked = Attachments.Refresh(Reg, docked);
            Assert.Equal(c.P.Y, docked.ResolvePoints(docked.Get(link.Id)!)[0].Y, 6);
        }

        [Fact]
        public void BuildHandlesAddTheNextLinkAndAFixedPivot()
        {
            var link = Link(new Vec2(50, 50), new Vec2(70, 50));
            var doc = Doc(link);
            var ports = Building.Ports(Reg, doc, link.Id);
            Assert.Equal(2, ports.Count(p => p.Kind == PortKind.Joint));
            var end = ports.Single(p => p.Id == "joint1");
            var items = Building.Items(Reg, doc, link.Id, end);
            Assert.Contains(items, i => i.TypeId == "kin.link.binary");
            Assert.Contains(items, i => i.TypeId == "kin.joint.ground");
            var next = Building.Place(Reg, doc, link.Id, end, items.First(i => i.TypeId == "kin.link.binary"))!;
            doc = Edits.Add(doc, doc.Pages[0].Id, next);
            Assert.Equal(new Vec2(70, 50), doc.ResolvePoints(doc.Get(next.Id)!)[0]);
            Assert.Equal(20, Len(doc, next.Id), 6);
            Assert.DoesNotContain(Building.Ports(Reg, doc, link.Id), p => p.Id == "joint1");

            // A fixed pivot at the other end: the link is docked on it afterwards.
            var start = Building.Ports(Reg, doc, link.Id).Single(p => p.Id == "joint0");
            var pivot = Building.Place(Reg, doc, link.Id, start, items.First(i => i.TypeId == "kin.joint.ground"))!;
            doc = Edits.Add(doc, doc.Pages[0].Id, pivot);
            doc = Building.DockBack(Reg, doc, link.Id, start, pivot.Id)!;
            Assert.Equal(pivot.Id, doc.Get(link.Id)!.Attach![0]!.ObjectId);
            Assert.Equal(new Vec2(50, 50), doc.ResolvePoints(doc.Get(link.Id)!)[0]);
        }
    }
}
