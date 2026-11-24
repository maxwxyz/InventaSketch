using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Engine.Tests
{
    /// <summary>Predefined linkages explode into pivots, links and sliders that form a self-built linkage (<see cref="Assemblies"/>).</summary>
    public class ExplodeMechanismTests
    {
        private static readonly Registry Reg = ModuleCatalog.CreateDefault();

        private static SketchDocument Doc(params SketchObject[] objects)
        {
            var doc = SketchDocument.Create();
            return Edits.Add(doc, doc.Pages[0].Id, objects);
        }

        private static double Len(SketchDocument doc, SketchObject link)
        {
            var p = doc.ResolvePoints(doc.Get(link.Id)!);
            return p[0].DistanceTo(p[1]);
        }

        [Fact]
        public void AFourBarExplodesIntoALinkageThatMovesTheSameWay()
        {
            var mech = Reg.Create(Reg.Library.First(i => i.TypeId == "kin.mech.fourbar"), new Vec2(120, 90)) with { Transform = new Transform(120, 90, 15) };
            var doc = Doc(mech);
            Assert.True(Assemblies.CanExplode(Reg, mech));
            var parts = Assemblies.Explode(Reg, doc, mech.Id);
            Assert.Equal(2, parts.Count(p => p.Type == "kin.joint.ground"));
            var links = parts.Where(p => p.Type == "kin.link.binary").ToList();
            Assert.Equal(3, links.Count);
            var exploded = Doc(parts.ToArray());

            // The joints of the exploded linkage sit where the mechanism's joints were.
            var ctx = new RenderContext(doc);
            var joints = Reg.PageAnchors(mech, ctx).Where(a => a.Kind == AnchorKind.Point && a.Role is AnchorRole.Port or AnchorRole.Mount && a.Id is "O2" or "A" or "B" or "O4").GroupBy(a => a.Id!).ToDictionary(g => g.Key, g => g.First().P);
            var crank = exploded.ResolvePoints(exploded.Get(links[0].Id)!);
            Assert.True(crank[0].DistanceTo(joints["O2"]) < 1e-6, "crank on O2");
            Assert.True(crank[1].DistanceTo(joints["A"]) < 1e-6, "crank tip at A");
            var rocker = exploded.ResolvePoints(exploded.Get(links[2].Id)!);
            Assert.True(rocker[1].DistanceTo(joints["B"]) < 1e-6, "rocker at B");

            Assert.Equal(1, Linkage.Dof(Reg, exploded, links[1].Id)!.Value.Dof);
            var lengths = links.Select(l => Len(exploded, l)).ToList();
            var moved = Linkage.Drag(Reg, exploded, links[0].Id, 1, crank[1] + new Vec2(-2, 3));
            for (var i = 0; i < 3; i++) Assert.Equal(lengths[i], Len(moved, links[i]), 3);
        }

        [Fact]
        public void ACouplerCurveOfTheExplodedFourBarIsClosed()
        {
            var mech = Reg.Create(Reg.Library.First(i => i.TypeId == "kin.mech.fourbar"), new Vec2(120, 90));
            var parts = Assemblies.Explode(Reg, Doc(mech), mech.Id);
            var exploded = Doc(parts.ToArray());
            var links = parts.Where(p => p.Type == "kin.link.binary").ToList();
            var crankTip = Linkage.Trace(Reg, exploded, links[0].Id, 1, 36);
            Assert.NotNull(crankTip);
            Assert.True(crankTip!.Value.Closed, "the crank goes all the way round");
            var pivot = exploded.ResolvePoints(exploded.Get(links[0].Id)!)[0];
            var r = Len(exploded, links[0]);
            Assert.All(crankTip.Value.Points, p => Assert.Equal(r, p.DistanceTo(pivot), 2));
            // The coupler's far joint swings on the rocker's arc.
            var rocker = exploded.ResolvePoints(exploded.Get(links[2].Id)!);
            var coupler = Linkage.Trace(Reg, exploded, links[1].Id, 1, 36);
            Assert.NotNull(coupler);
            Assert.All(coupler!.Value.Points, p => Assert.Equal(Len(exploded, links[2]), p.DistanceTo(rocker[0]), 2));
        }

        [Fact]
        public void GhostPositionsKeepTheLinkLengths()
        {
            var mech = Reg.Create(Reg.Library.First(i => i.TypeId == "kin.mech.fourbar"), new Vec2(120, 90));
            var parts = Assemblies.Explode(Reg, Doc(mech), mech.Id);
            var exploded = Doc(parts.ToArray());
            var links = parts.Where(p => p.Type == "kin.link.binary").ToList();
            var poses = Linkage.Poses(Reg, exploded, links[0].Id, 4);
            Assert.Equal(4, poses.Count);
            foreach (var pose in poses)
            {
                Assert.Equal(3, pose.Count);
                foreach (var l in links) Assert.Equal(Len(exploded, l), pose[l.Id][0].DistanceTo(pose[l.Id][1]), 2);
            }
            Assert.True(poses[0][links[0].Id][1].DistanceTo(poses[1][links[0].Id][1]) > 1, "the poses differ");
        }

        [Fact]
        public void ASliderCrankExplodesWithItsSliderOnTheGuide()
        {
            var mech = Reg.Create(Reg.Library.First(i => i.TypeId == "kin.mech.slidercrank"), new Vec2(100, 100));
            var parts = Assemblies.Explode(Reg, Doc(mech), mech.Id);
            Assert.Single(parts, p => p.Type == "kin.joint.prismatic");
            var exploded = Doc(parts.ToArray());
            var crank = parts.First(p => p.Type == "kin.link.binary");
            Assert.Equal(1, Linkage.Dof(Reg, exploded, crank.Id)!.Value.Dof);
            var rod = parts.Last(p => p.Type == "kin.link.binary");
            var before = exploded.ResolvePoints(exploded.Get(rod.Id)!)[1];
            var tip = exploded.ResolvePoints(exploded.Get(crank.Id)!)[1];
            var moved = Linkage.Drag(Reg, exploded, crank.Id, 1, tip + new Vec2(0, 4));
            var after = moved.ResolvePoints(moved.Get(rod.Id)!)[1];
            Assert.Equal(before.Y, after.Y, 3); // the slider stays on its guide
            Assert.Equal(Len(exploded, rod), Len(moved, rod), 3);
        }

        private static (SketchDocument Doc, System.Collections.Generic.IReadOnlyList<SketchObject> Parts) Explode(string typeId, double rotation = 0)
        {
            var mech = Reg.Create(Reg.Library.First(i => i.TypeId == typeId), new Vec2(100, 100)) with { Transform = new Transform(100, 100, rotation) };
            Assert.True(Assemblies.CanExplode(Reg, mech), typeId);
            var parts = Assemblies.Explode(Reg, Doc(mech), mech.Id);
            return (Doc(parts.ToArray()), parts);
        }

        private static Vec2 Pt(SketchDocument doc, SketchObject o, int i) => doc.ResolvePoints(doc.Get(o.Id)!)[i];

        private static double OffLine(Vec2 p, Vec2 a, Vec2 b) => System.Math.Abs((p - a).Cross((b - a).Normalized()));

        [Fact]
        public void AToggleExplodesWithItsSliderOnTheVerticalGuide()
        {
            var (doc, parts) = Explode("kin.mech.toggle");
            var slider = parts.Single(p => p.Type == "kin.joint.prismatic");
            Assert.Equal(270, slider.Transform.Rotation, 6); // vertical guide, frame on the right as in the mechanism
            var links = parts.Where(p => p.Type == "kin.link.binary").ToList();
            Assert.Equal(1, Linkage.Dof(Reg, doc, links[0].Id)!.Value.Dof);
            var c = Pt(doc, links[1], 1);
            var knee = Pt(doc, links[0], 1);
            var moved = Linkage.Drag(Reg, doc, links[0].Id, 1, knee + new Vec2(3, 0));
            Assert.Equal(c.X, Pt(moved, links[1], 1).X, 3); // C stays on the vertical guide
            Assert.NotEqual(c.Y, Pt(moved, links[1], 1).Y, 3);
            for (var i = 0; i < 2; i++) Assert.Equal(Len(doc, links[i]), Len(moved, links[i]), 3);
        }

        [Fact]
        public void AQuickReturnExplodesWithTheCrankPinInTheSlottedLever()
        {
            var (doc, parts) = Explode("kin.mech.quickreturn", 20);
            var links = parts.Where(p => p.Type == "kin.link.binary").ToList();
            Assert.Equal(3, links.Count);
            var (lever, crank, link) = (links[0], links[1], links[2]);
            Assert.Equal(Linkage.SlotPrefix + "0", doc.Get(crank.Id)!.Attach![1]!.AnchorId);
            Assert.Equal(1, Linkage.Dof(Reg, doc, crank.Id)!.Value.Dof);
            var ramBefore = Pt(doc, link, 1);
            var tip = Pt(doc, crank, 1);
            var moved = Linkage.Drag(Reg, doc, crank.Id, 1, tip + (tip - Pt(doc, crank, 0)).Perp.Normalized() * 3);
            Assert.True(Pt(moved, crank, 1).DistanceTo(tip) > 0.5, "the crank turned");
            Assert.Equal(Len(doc, crank), Len(moved, crank), 3);
            Assert.Equal(Len(doc, lever), Len(moved, lever), 3);
            Assert.Equal(Len(doc, link), Len(moved, link), 3);
            Assert.True(OffLine(Pt(moved, crank, 1), Pt(moved, lever, 0), Pt(moved, lever, 1)) < 1e-3, "the pin stays in the slot");
            var ram = Pt(moved, link, 1);
            Assert.True(OffLine(ram, ramBefore, ramBefore + new Vec2(System.Math.Cos(20 * System.Math.PI / 180), System.Math.Sin(20 * System.Math.PI / 180))) < 1e-3, "the ram stays on its guide");
            Assert.True(ram.DistanceTo(ramBefore) > 0.1, "the ram moved");
            // Refreshing the attachments keeps the pin where the solver put it (its fraction along the slot was updated).
            var refreshed = Attachments.Refresh(Reg, moved);
            Assert.True(Pt(refreshed, crank, 1).DistanceTo(Pt(moved, crank, 1)) < 1e-4);
        }

        [Fact]
        public void AScotchYokeExplodesIntoAYokeThatOnlyTranslates()
        {
            var (doc, parts) = Explode("kin.mech.scotchyoke");
            var yoke = parts.Single(p => p.Type == "kin.link.ternary");
            var crank = parts.Single(p => p.Type == "kin.link.binary");
            Assert.Equal(2, parts.Count(p => p.Type == "kin.joint.prismatic"));
            Assert.Equal(1, Linkage.Dof(Reg, doc, crank.Id)!.Value.Dof);
            var slot = Pt(doc, yoke, 1) - Pt(doc, yoke, 0);
            var rod = Pt(doc, yoke, 2) - Pt(doc, yoke, 0);
            var tip = Pt(doc, crank, 1);
            var moved = Linkage.Drag(Reg, doc, crank.Id, 1, tip + new Vec2(4, 2));
            Assert.Equal(Len(doc, crank), Len(moved, crank), 3);
            Assert.True((Pt(moved, yoke, 1) - Pt(moved, yoke, 0)).DistanceTo(slot) < 1e-3, "the slot stays upright");
            Assert.True((Pt(moved, yoke, 2) - Pt(moved, yoke, 0)).DistanceTo(rod) < 1e-3, "the yoke does not turn");
            Assert.Equal(Pt(doc, yoke, 0).Y, Pt(moved, yoke, 0).Y, 3);
            Assert.True(System.Math.Abs(Pt(moved, yoke, 0).X - Pt(doc, yoke, 0).X) > 0.1, "the yoke moved");
            Assert.True(OffLine(Pt(moved, crank, 1), Pt(moved, yoke, 0), Pt(moved, yoke, 1)) < 1e-3, "the pin stays in the slot");
        }

        [Fact]
        public void ACamMechanismIsNotExplodedBecauseNoPartKeepsItsProfile()
        {
            var cam = Reg.Create(Reg.Library.First(i => i.TypeId == "kin.cam.follower"), new Vec2(50, 50));
            Assert.False(Assemblies.CanExplode(Reg, cam));
        }

        [Fact]
        public void AJointDockedOnALinksSlotSlidesAlongIt()
        {
            // Self-built: crank O2-A with its pin in the slot of lever O4-T (a hand-built crank and slotted lever).
            var pivots = Reg.Library.First(i => i.TypeId == "kin.joint.ground");
            var o4 = Reg.Create(pivots, new Vec2(0, 0));
            var o2 = Reg.Create(pivots, new Vec2(0, 0));
            var doc = Doc(o4, o2);
            var ctx = new RenderContext(doc);
            Vec2 PivotOf(SketchObject p) => Reg.PageAnchors(p, ctx).First(a => a.Kind == AnchorKind.Point && a.Id == "c" && a.Role != AnchorRole.Center).P;
            doc = Edits.Update(doc, new[] { o2.Id }, x => x with { Transform = x.Transform with { Y = x.Transform.Y + 20 } });
            ctx = new RenderContext(doc);
            var p4 = PivotOf(doc.Get(o4.Id)!); var p2 = PivotOf(doc.Get(o2.Id)!);
            var linkItem = Reg.Library.First(i => i.TypeId == "kin.link.binary");
            var lever = Reg.Create(linkItem, p4) with { Points = new[] { p4, p4 + new Vec2(10, 40) } };
            lever = lever with { Attach = new Attachment?[] { Attachments.For(doc.Get(o4.Id)!, p4, Reg.PageAnchors(doc.Get(o4.Id)!, ctx).First(a => a.Id == "c" && a.Role != AnchorRole.Center)), null } };
            doc = Edits.Add(doc, doc.Pages[0].Id, lever);
            ctx = new RenderContext(doc);
            var slot = Reg.PageAnchors(doc.Get(lever.Id)!, ctx).Single(a => a.Id == Linkage.SlotPrefix + "0");
            Assert.Equal(AnchorRole.Rail, slot.Role);
            var pin = Geo.ClosestOnSegment(p2 + new Vec2(8, 0), slot.P, slot.Q).Point;
            var crank = Reg.Create(linkItem, p2) with { Points = new[] { p2, pin } };
            crank = crank with { Attach = new Attachment?[] { Attachments.For(doc.Get(o2.Id)!, p2, Reg.PageAnchors(doc.Get(o2.Id)!, ctx).First(a => a.Id == "c" && a.Role != AnchorRole.Center)), Attachments.For(doc.Get(lever.Id)!, pin, slot) } };
            doc = Edits.Add(doc, doc.Pages[0].Id, crank);

            Assert.Equal(1, Linkage.Dof(Reg, doc, crank.Id)!.Value.Dof);
            var moved = Linkage.Drag(Reg, doc, crank.Id, 1, pin + new Vec2(0, 4));
            var tip = Pt(moved, crank, 1);
            Assert.Equal(Len(doc, crank), Len(moved, crank), 3);
            Assert.Equal(Len(doc, lever), Len(moved, lever), 3);
            Assert.True(OffLine(tip, Pt(moved, lever, 0), Pt(moved, lever, 1)) < 1e-3, "pin on the slot line");
            Assert.True(Pt(moved, lever, 1).DistanceTo(Pt(doc, lever, 1)) > 0.1, "the lever swung");
        }
    }
}
