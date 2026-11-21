using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Engine.Tests
{
    /// <summary>"Insert a fitting element": placed where it belongs and coupled right away (<see cref="Fitting"/>).</summary>
    public class FittingTests
    {
        private static readonly Registry Reg = ModuleCatalog.CreateDefault();

        private static SketchDocument Doc(params SketchObject[] objects)
        {
            var doc = SketchDocument.Create();
            return Edits.Add(doc, doc.Pages[0].Id, objects);
        }

        private static SketchDocument Add(SketchDocument doc, SketchObject o) => Edits.Add(doc, doc.Pages[0].Id, o);

        private static Anchor First(SketchDocument doc, string id, AnchorKind kind, AnchorRole role)
            => Reg.PageAnchors(doc.Get(id)!, new RenderContext(doc)).First(a => a.Kind == kind && a.Role == role);

        [Fact]
        public void AGearGoesOntoTheSelectedShaftAndIsCarried()
        {
            var shaft = Reg.Create(Reg.Item("mech.shaft.line")!, new Vec2(80, 40));
            var doc = Doc(shaft);
            var gear = Fitting.Place(Reg, doc, shaft.Id, Reg.Item("mech.spur.stick")!)!;
            Assert.NotNull(gear);
            doc = Add(doc, gear);
            var axis = First(doc, shaft.Id, AnchorKind.Segment, AnchorRole.Axis);
            Assert.Equal(Vec2.Lerp(axis.P, axis.Q, 0.5).X, gear.Transform.X, 6);
            Assert.Equal(axis.P.Y, gear.Transform.Y, 6);
            Assert.Contains(gear.Id, Coupling.RigidFollowers(Reg, doc, new[] { shaft.Id }));

            // A second part goes to a free place on the same shaft.
            var bearing = Fitting.Place(Reg, doc, shaft.Id, Reg.Item("mech.bearing.ball.stick") ?? Reg.Library.First(i => i.TypeId == "mech.bearing.ball" && i.View == "stick"))!;
            Assert.Equal(axis.P.Y, bearing.Transform.Y, 6);
            Assert.True(System.Math.Abs(bearing.Transform.X - gear.Transform.X) > 10);
        }

        [Fact]
        public void AGearNextToAGearIsInMesh()
        {
            var a = Reg.Create(Reg.Item("mech.spur.axial")!, new Vec2(60, 60));
            var doc = Doc(a);
            var b = Fitting.Place(Reg, doc, a.Id, Reg.Item("mech.spur.axial")!)!;
            doc = Add(doc, b);
            var pa = First(doc, a.Id, AnchorKind.Circle, AnchorRole.Pitch);
            var pb = First(doc, b.Id, AnchorKind.Circle, AnchorRole.Pitch);
            Assert.Equal(pa.Radius + pb.Radius, pa.P.DistanceTo(pb.P), 6);
            Assert.True(pb.P.Y > pa.P.Y, "placed below");
            Assert.Equal(LinkKind.Mesh, Links.For(Reg, doc, new[] { a.Id }).Single().Kind);
        }

        [Fact]
        public void ABearingBesideAGearGoesOntoItsShaft()
        {
            var shaft = Reg.Create(Reg.Item("mech.shaft.line")!, new Vec2(80, 40));
            var doc = Doc(shaft);
            var gear = Fitting.Place(Reg, doc, shaft.Id, Reg.Item("mech.spur.stick")!)!;
            doc = Add(doc, gear);
            var item = Reg.Library.First(i => i.TypeId == "mech.bearing.ball" && i.View == "stick");
            var bearing = Fitting.Place(Reg, doc, gear.Id, item)!;
            doc = Add(doc, bearing);
            Assert.Contains(bearing.Id, Coupling.RigidFollowers(Reg, doc, new[] { shaft.Id }));
        }

        [Fact]
        public void AShaftForAGearRunsThroughItsHub()
        {
            var gear = Reg.Create(Reg.Item("mech.spur.stick")!, new Vec2(50, 50));
            var doc = Doc(gear);
            var shaft = Fitting.Place(Reg, doc, gear.Id, Reg.Item("mech.shaft.line")!)!;
            doc = Add(doc, shaft);
            Assert.Contains(gear.Id, Coupling.RigidFollowers(Reg, doc, new[] { shaft.Id }));
        }

        [Fact]
        public void ASchematicGearSuggestsAMeshingGearAndAShaftInItsView()
        {
            var gear = Reg.Create(Reg.Item("mech.spur.stick")!, new Vec2(50, 50));
            var items = Reg.Suggestions(gear, 8);
            Assert.Contains(items, i => i.TypeId == "mech.gear.spur" && i.View == "stick");
            Assert.Contains(items, i => i.TypeId == "mech.shaft" && i.View == "line");
        }

        [Fact]
        public void UnrelatedItemsHaveNoFittingPlace()
        {
            var shaft = Reg.Create(Reg.Item("mech.shaft.line")!, new Vec2(80, 40));
            Assert.Null(Fitting.Place(Reg, Doc(shaft), shaft.Id, Reg.Item("ann.text") ?? Reg.Library.First(i => i.TypeId == "ann.text")));
        }
    }
}
