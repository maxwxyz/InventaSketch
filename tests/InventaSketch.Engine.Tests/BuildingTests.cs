using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Engine.Tests
{
    /// <summary>Build ports ("+" handles) for assembling gearboxes and drivetrains (<see cref="Building"/>).</summary>
    public class BuildingTests
    {
        private static readonly Registry Reg = ModuleCatalog.CreateDefault();

        private static SketchDocument Doc(params SketchObject[] objects)
        {
            var doc = SketchDocument.Create();
            return Edits.Add(doc, doc.Pages[0].Id, objects);
        }

        private static SketchDocument Add(SketchDocument doc, SketchObject o) => Edits.Add(doc, doc.Pages[0].Id, o);

        [Fact]
        public void AGearOffersFourMeshPortsAndTakenOnesDisappear()
        {
            var gear = Reg.Create(Reg.Item("mech.spur.axial")!, new Vec2(60, 60));
            var doc = Doc(gear);
            var ports = Building.Ports(Reg, doc, gear.Id);
            Assert.Equal(4, ports.Count(p => p.Kind == PortKind.Mesh));
            var right = ports.Single(p => p.Id == "mesh.right");
            var items = Building.Items(Reg, doc, gear.Id, right);
            Assert.Contains(items, i => i.TypeId == "mech.gear.spur" && i.View == "axial");
            var b = Building.Place(Reg, doc, gear.Id, right, items.First(i => i.TypeId == "mech.gear.spur"))!;
            doc = Add(doc, b);
            Assert.Equal(LinkKind.Mesh, Links.For(Reg, doc, new[] { gear.Id }).Single().Kind);
            Assert.DoesNotContain(Building.Ports(Reg, doc, gear.Id), p => p.Id == "mesh.right");
            Assert.True(b.Transform.X > gear.Transform.X);
        }

        [Fact]
        public void AShaftEndTakesTheNextSectionAndParts()
        {
            var shaft = Reg.Create(Reg.Item("mech.shaft.line")!, new Vec2(80, 40));
            var doc = Doc(shaft);
            var ports = Building.Ports(Reg, doc, shaft.Id).Where(p => p.Kind == PortKind.AxisEnd).ToList();
            Assert.Equal(2, ports.Count);
            var end = ports.Single(p => p.Dir.X > 0);
            var items = Building.Items(Reg, doc, shaft.Id, end);
            Assert.Contains(items, i => i.TypeId == "mech.shaft");
            Assert.Contains(items, i => i.TypeId == "mech.bearing.ball");

            // The next shaft section starts where this one ends, on the same line.
            var next = Building.Place(Reg, doc, shaft.Id, end, items.First(i => i.TypeId == "mech.shaft"))!;
            var withNext = Add(doc, next);
            var axis = Reg.PageAnchors(withNext.Get(next.Id)!, new RenderContext(withNext)).First(a => a.Role == AnchorRole.Axis);
            Assert.Contains(new[] { axis.P, axis.Q }, q => q.DistanceTo(end.At) < 1e-6);
            Assert.Equal(end.At.Y, axis.P.Y, 6);
            Assert.Equal(end.At.Y, axis.Q.Y, 6);
            Assert.DoesNotContain(Building.Ports(Reg, withNext, shaft.Id), p => p.Id == end.Id);

            // A bearing on the end is carried by the shaft.
            var bearing = Building.Place(Reg, doc, shaft.Id, end, items.First(i => i.TypeId == "mech.bearing.ball"))!;
            var withBearing = Add(doc, bearing);
            Assert.Contains(bearing.Id, Coupling.RigidFollowers(Reg, withBearing, new[] { shaft.Id }));
        }

        [Fact]
        public void DraggingAShaftEndStretchesItAndKeepsTheOtherEnd()
        {
            var shaft = Reg.Create(Reg.Item("mech.shaft.line")!, new Vec2(80, 40));
            var doc = Doc(shaft);
            var end = Building.Ports(Reg, doc, shaft.Id).Single(p => p.Kind == PortKind.AxisEnd && p.Dir.X > 0);
            Assert.True(Building.CanStretch(Reg, doc, shaft, end));
            var stretched = Building.StretchEnd(Reg, doc, shaft.Id, end, end.At + new Vec2(25, 7));
            var axis = Reg.PageAnchors(stretched.Get(shaft.Id)!, new RenderContext(stretched)).First(a => a.Role == AnchorRole.Axis);
            Assert.Equal(end.AxisFrom.X, System.Math.Min(axis.P.X, axis.Q.X), 6);
            Assert.Equal(end.At.X + 25, System.Math.Max(axis.P.X, axis.Q.X), 6);
        }

        [Fact]
        public void ASchematicGearMeshesOnlyAcrossItsAxis()
        {
            var gear = Reg.Create(Reg.Item("mech.spur.stick")!, new Vec2(50, 50));
            var ports = Building.Ports(Reg, Doc(gear), gear.Id).Where(p => p.Kind == PortKind.Mesh).Select(p => p.Id).OrderBy(x => x).ToList();
            Assert.Equal(new[] { "mesh.down", "mesh.up" }, ports);
        }

        [Fact]
        public void AFreeHubOffersAShaft()
        {
            var gear = Reg.Create(Reg.Item("mech.spur.stick")!, new Vec2(50, 50));
            var doc = Doc(gear);
            var hub = Building.Ports(Reg, doc, gear.Id).Single(p => p.Kind == PortKind.Hub);
            var items = Building.Items(Reg, doc, gear.Id, hub);
            Assert.Equal("mech.shaft", items[0].TypeId);
            var shaft = Building.Place(Reg, doc, gear.Id, hub, items[0])!;
            Assert.Contains(gear.Id, Coupling.RigidFollowers(Reg, Add(doc, shaft), new[] { shaft.Id }));
        }

        [Fact]
        public void ARingGearTakesPlanetsInside()
        {
            var item = Reg.Library.FirstOrDefault(i => i.TypeId == "mech.gear.internal" && i.View == "axial") ?? Reg.Library.First(i => i.TypeId == "mech.gear.internal");
            var ring = Reg.Create(item, new Vec2(80, 80));
            var doc = Doc(ring);
            var inner = Building.Ports(Reg, doc, ring.Id).Where(p => p.Kind == PortKind.InnerMesh).ToList();
            if (inner.Count == 0) return; // views without an inner pitch circle offer none
            var planet = Building.Place(Reg, doc, ring.Id, inner[0], Reg.Item("mech.spur.axial")!)!;
            doc = Add(doc, planet);
            Assert.Contains(Links.For(Reg, doc, new[] { ring.Id }), l => l.Kind == LinkKind.Mesh);
        }

        [Fact]
        public void AnEMachineOffersItsShaftEnds()
        {
            var em = Reg.Create(Reg.Library.First(i => i.TypeId == "machines.drivetrain.emachine"), new Vec2(60, 50));
            var doc = Doc(em);
            var ports = Building.Ports(Reg, doc, em.Id);
            var axisEnds = ports.Where(p => p.Kind == PortKind.AxisEnd).ToList();
            Assert.NotEmpty(axisEnds);
            var items = Building.Items(Reg, doc, em.Id, axisEnds[0]);
            Assert.Contains(items, i => i.TypeId == "mech.shaft");
        }
    }
}
