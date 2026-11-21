using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Engine.Tests
{
    /// <summary>
    /// Link markers: where objects touch (meshing gears, parts on a shaft, docked line ends), with release and re-coupling by
    /// one click (<see cref="Links"/>).
    /// </summary>
    public class LinkTests
    {
        private static readonly Registry Reg = ModuleCatalog.CreateDefault();

        private static SketchDocument Doc(params SketchObject[] objects)
        {
            var doc = SketchDocument.Create();
            return Edits.Add(doc, doc.Pages[0].Id, objects);
        }

        private static (Vec2 C, double R) Pitch(SketchDocument doc, string id)
        {
            var a = Reg.PageAnchors(doc.Get(id)!, new RenderContext(doc)).First(x => x.Kind == AnchorKind.Circle && x.Role == AnchorRole.Pitch);
            return (a.P, a.Radius);
        }

        private static SketchObject Gear(Vec2 at, double size) => Reg.Create(Reg.Item("mech.spur.axial")!, at) with { Size = new Size2(size, size) };

        private static SketchDocument Pair(out string a, out string b)
        {
            var ga = Gear(new Vec2(60, 60), 30);
            var gb = Gear(new Vec2(0, 0), 20);
            var doc = Doc(ga, gb);
            var d = Pitch(doc, ga.Id).R + Pitch(doc, gb.Id).R;
            doc = Edits.Update(doc, new[] { gb.Id }, o => o with { Transform = o.Transform with { X = 60 + d, Y = 60 } });
            a = ga.Id;
            b = gb.Id;
            return doc;
        }

        [Fact]
        public void MeshingGearsShowACoupledLinkAtTheContactPoint()
        {
            var doc = Pair(out var a, out var b);
            var link = Assert.Single(Links.For(Reg, doc, new[] { a }));
            Assert.Equal(LinkKind.Mesh, link.Kind);
            Assert.False(link.Released);
            Assert.Equal(b, link.Other(a));
            Assert.Equal(60 + Pitch(doc, a).R, link.At.X, 6);
            Assert.Equal(60, link.At.Y, 6);
            Assert.Contains("click releases", Links.Describe(Reg, doc, link, a));
        }

        [Fact]
        public void AReleasedPairNoLongerScalesAndCouplesAgainOnClick()
        {
            var doc = Pair(out var a, out var b);
            doc = Links.Toggle(doc, Links.For(Reg, doc, new[] { a }).Single());
            Assert.Equal(new[] { b }, doc.Get(a)!.Released);
            Assert.Equal(new[] { a }, doc.Get(b)!.Released);
            var link = Links.For(Reg, doc, new[] { b }).Single();
            Assert.True(link.Released);

            // Released and still touching: a small push does not scale the partner, and the release is kept.
            var after = Coupling.Propagate(Reg, doc, Edits.Move(doc, new[] { a }, new Vec2(0, 0.1)));
            Assert.Equal(doc.Get(b)!.Size, after.Get(b)!.Size);
            Assert.NotNull(after.Get(a)!.Released);

            // A click couples again: pulling away scales both.
            doc = Links.Toggle(doc, link);
            Assert.Null(doc.Get(a)!.Released);
            Assert.Null(doc.Get(b)!.Released);
            after = Coupling.Propagate(Reg, doc, Edits.Move(doc, new[] { a }, new Vec2(-10, 0)));
            Assert.True(after.Get(b)!.Size.W > doc.Get(b)!.Size.W);
        }

        [Fact]
        public void AReleaseIsForgottenOnceThePartsNoLongerTouch()
        {
            var doc = Pair(out var a, out var b);
            doc = Links.Toggle(doc, Links.For(Reg, doc, new[] { a }).Single());
            var after = Coupling.Propagate(Reg, doc, Edits.Move(doc, new[] { a }, new Vec2(-40, 0)));
            Assert.Equal(doc.Get(b)!.Size, after.Get(b)!.Size);
            Assert.Null(after.Get(a)!.Released);
            Assert.Null(after.Get(b)!.Released);
            Assert.Empty(Links.For(Reg, after, new[] { a }));
        }

        [Fact]
        public void AReleasedShaftNoLongerCarriesItsGear()
        {
            var shaft = Reg.Create(Reg.Item("mech.shaft.line")!, new Vec2(80, 40));
            var gear = Reg.Create(Reg.Item("mech.spur.stick")!, new Vec2(70, 40));
            var doc = Doc(shaft, gear);
            var link = Links.For(Reg, doc, new[] { shaft.Id }).Single(l => l.Kind == LinkKind.Carried);
            Assert.Equal(shaft.Id, link.A);
            Assert.Contains(gear.Id, Coupling.RigidFollowers(Reg, doc, new[] { shaft.Id }));
            doc = Links.Toggle(doc, link);
            Assert.DoesNotContain(gear.Id, Coupling.RigidFollowers(Reg, doc, new[] { shaft.Id }));
            var after = Coupling.Propagate(Reg, doc, Edits.Move(doc, new[] { shaft.Id }, new Vec2(0, 10)));
            Assert.Equal(doc.Get(gear.Id)!.Transform, after.Get(gear.Id)!.Transform);
        }

        [Fact]
        public void ADockedLineEndIsReleasedAndDockedAgain()
        {
            var r1 = Reg.Create(Reg.Library.First(i => i.TypeId == "elec.resistor"), new Vec2(30, 40));
            var doc = Doc(r1);
            var pin = Reg.PageAnchors(r1, new RenderContext(doc)).First(x => x.Kind == AnchorKind.Point && x.Role == AnchorRole.Port);
            var wire = Reg.Create(Reg.Library.First(i => i.TypeId == "elec.wire"), Vec2.Zero) with
            {
                Points = new[] { pin.P, pin.P + new Vec2(0, 30) },
                Attach = new[] { Attachments.For(r1, pin.P, pin), null },
            };
            doc = Edits.Add(doc, doc.Pages[0].Id, wire);

            var link = Links.For(Reg, doc, new[] { r1.Id }).Single(l => l.Kind == LinkKind.Attached);
            Assert.False(link.Released);
            Assert.Equal(wire.Id, link.A);
            Assert.Equal(0, link.Index);

            doc = Links.Toggle(doc, link);
            Assert.Null(doc.Get(wire.Id)!.Attach);
            Assert.Equal(pin.P, doc.Get(wire.Id)!.Points![0]);
            // The free end still lies on the pin: an open marker that docks it again.
            var open = Links.For(Reg, doc, new[] { wire.Id }).Single(l => l.Kind == LinkKind.Attached);
            Assert.True(open.Released);
            Assert.Equal(r1.Id, open.B);
            doc = Links.Toggle(doc, open);
            Assert.Equal(r1.Id, doc.Get(wire.Id)!.Attach![0]!.ObjectId);
            Assert.Equal(pin.Id, doc.Get(wire.Id)!.Attach![0]!.AnchorId);
        }

        [Fact]
        public void DuplicatesKeepReleasesOnlyAmongTheCopies()
        {
            var doc = Pair(out var a, out var b);
            doc = Links.Toggle(doc, Links.For(Reg, doc, new[] { a }).Single());
            var (dup, ids) = Edits.Duplicate(doc, doc.Pages[0].Id, new[] { a }, new Vec2(0, 100));
            Assert.Null(dup.Get(ids[0])!.Released);
            (dup, ids) = Edits.Duplicate(doc, doc.Pages[0].Id, new[] { a, b }, new Vec2(0, 100));
            Assert.Equal(new[] { ids[1] }, dup.Get(ids[0])!.Released);
        }
    }
}
