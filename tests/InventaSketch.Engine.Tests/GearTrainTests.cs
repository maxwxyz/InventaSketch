using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Engine.Tests
{
    /// <summary>Building gear trains yourself: carrier with pins, planets, sun and ring sized to fit, exploding a planetary set.</summary>
    public class GearTrainTests
    {
        private static readonly Registry Reg = ModuleCatalog.CreateDefault();

        private static SketchDocument Doc(params SketchObject[] objects)
        {
            var doc = SketchDocument.Create();
            return Edits.Add(doc, doc.Pages[0].Id, objects);
        }

        private static SketchDocument Add(SketchDocument doc, SketchObject o) => Edits.Add(doc, doc.Pages[0].Id, o);

        private static LibraryItem Item(string typeId, string view) => Reg.Library.First(i => i.TypeId == typeId && i.View == view);

        private static bool Meshes(SketchDocument doc, string a, string b)
            => Links.For(Reg, doc, new[] { a }).Any(l => l.Kind == LinkKind.Mesh && l.Other(a) == b);

        [Fact]
        public void APlanetaryStageIsBuiltFromACarrier()
        {
            var carrier = Reg.Create(Item("mech.carrier", "axial"), new Vec2(100, 100)).WithParam("arms", 4);
            var doc = Doc(carrier);
            var ports = Building.Ports(Reg, doc, carrier.Id);
            Assert.Equal(4, ports.Count(p => p.Kind == PortKind.Pin));
            var hubPort = ports.Single(p => p.Kind == PortKind.Hub);

            // Sun in the centre.
            var hubItems = Building.Items(Reg, doc, carrier.Id, hubPort);
            Assert.Contains(hubItems, i => i.TypeId == "mech.gear.spur");
            Assert.Contains(hubItems, i => i.TypeId == "mech.gear.internal");
            Assert.DoesNotContain(hubItems, i => i.TypeId == "mech.shaft"); // a side-view shaft does not fit an axial carrier
            var sun = Building.Place(Reg, doc, carrier.Id, hubPort, Item("mech.gear.spur", "axial"))!;
            doc = Add(doc, sun);

            // Planets on the pins mesh with the sun and ride on the carrier.
            foreach (var pin in Building.Ports(Reg, doc, carrier.Id).Where(p => p.Kind == PortKind.Pin).ToList())
            {
                var planet = Building.Place(Reg, doc, carrier.Id, pin, Item("mech.gear.spur", "axial"))!;
                doc = Add(doc, planet);
                Assert.True(Meshes(doc, planet.Id, sun.Id), "planet meshes with the sun");
                Assert.Contains(planet.Id, Coupling.RigidFollowers(Reg, doc, new[] { carrier.Id }));
            }
            Assert.DoesNotContain(Building.Ports(Reg, doc, carrier.Id), p => p.Kind == PortKind.Pin);

            // A ring gear around the planets.
            var ringPort = Building.Ports(Reg, doc, carrier.Id).Single(p => p.Kind == PortKind.Hub);
            var ring = Building.Place(Reg, doc, carrier.Id, ringPort, Item("mech.gear.internal", "axial"))!;
            doc = Add(doc, ring);
            var planets = doc.Objects.Values.Where(x => x.Type == "mech.gear.spur" && x.Id != sun.Id).ToList();
            Assert.Equal(4, planets.Count);
            Assert.All(planets, p => Assert.True(Meshes(doc, p.Id, ring.Id), "planet meshes with the ring"));
        }

        [Fact]
        public void ACarrierOnASunGearHasRoomForPlanets()
        {
            var sun = Reg.Create(Item("mech.gear.spur", "axial"), new Vec2(60, 60));
            var doc = Doc(sun);
            var hub = Building.Ports(Reg, doc, sun.Id).Single(p => p.Kind == PortKind.Hub);
            var items = Building.Items(Reg, doc, sun.Id, hub);
            Assert.Contains(items, i => i.TypeId == "mech.carrier");
            var carrier = Building.Place(Reg, doc, sun.Id, hub, items.First(i => i.TypeId == "mech.carrier"))!;
            doc = Add(doc, carrier);
            var rs = Reg.PageAnchors(sun, new RenderContext(doc)).First(a => a.Role == AnchorRole.Pitch).Radius;
            var pin = Building.Ports(Reg, doc, carrier.Id).First(p => p.Kind == PortKind.Pin);
            Assert.Equal(rs * 1.8, pin.At.DistanceTo(new Vec2(60, 60)), 6);
            var planet = Building.Place(Reg, doc, carrier.Id, pin, Item("mech.gear.spur", "axial"))!;
            Assert.True(Meshes(Add(doc, planet), planet.Id, sun.Id));
        }

        [Theory]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(8)]
        public void APlanetarySetExplodesIntoCoupledParts(int planets)
        {
            var set = Reg.Create(Item("mech.gearset.planetary", "axial"), new Vec2(100, 100)).WithParam("planets", planets);
            var doc = Doc(set);
            Assert.True(Assemblies.CanExplode(Reg, set));
            var parts = Assemblies.Explode(Reg, doc, set.Id);
            Assert.Equal(3 + planets, parts.Count);
            var exploded = Doc(parts.ToArray());
            var carrier = parts.Single(p => p.Type == "mech.carrier");
            var sun = parts.First(p => p.Type == "mech.gear.spur");
            var ring = parts.Single(p => p.Type == "mech.gear.internal");
            var planetParts = parts.Where(p => p.Type == "mech.gear.spur" && p.Id != sun.Id).ToList();
            Assert.Equal(planets, planetParts.Count);
            Assert.All(planetParts, p =>
            {
                Assert.True(Meshes(exploded, p.Id, sun.Id), "planet meshes with the sun");
                Assert.True(Meshes(exploded, p.Id, ring.Id), "planet meshes with the ring");
            });
            var followers = Coupling.RigidFollowers(Reg, exploded, new[] { carrier.Id });
            Assert.All(planetParts, p => Assert.Contains(p.Id, followers));
        }

        [Fact]
        public void ADifferentialIsBuiltFromBevelGears()
        {
            var side = Reg.Create(Item("mech.gear.bevel", "stick"), new Vec2(80, 80));
            var doc = Doc(side);
            var apex = Reg.PageAnchors(side, new RenderContext(doc)).Single(a => a.Role == AnchorRole.Apex).P;
            var ports = Building.Ports(Reg, doc, side.Id).Where(p => p.Kind == PortKind.Bevel).ToList();
            Assert.Equal(new[] { "bevel.across", "bevel.down", "bevel.up" }, ports.Select(p => p.Id).OrderBy(x => x));
            foreach (var port in ports)
            {
                var items = Building.Items(Reg, doc, side.Id, port);
                Assert.Contains(items, i => i.TypeId == "mech.gear.bevel");
                var mate = Building.Place(Reg, doc, side.Id, port, Item("mech.gear.bevel", "stick"))!;
                doc = Add(doc, mate);
                // Every mate shares the cone apex and sits on the port's side.
                var mateApex = Reg.PageAnchors(mate, new RenderContext(doc)).Single(a => a.Role == AnchorRole.Apex).P;
                Assert.True(mateApex.DistanceTo(apex) < 1e-6, port.Id);
                Assert.True((new Vec2(mate.Transform.X, mate.Transform.Y) - apex).Dot(port.Dir) > 0, port.Id);
            }
            // Side gear, opposite side gear and two spider gears: all ports taken.
            Assert.DoesNotContain(Building.Ports(Reg, doc, side.Id), p => p.Kind == PortKind.Bevel);
            Assert.Equal(4, doc.Objects.Count);
        }

        [Fact]
        public void StepsAndLimitsOfThePlanetarySet()
        {
            var set = Reg.Create(Item("mech.gearset.planetary", "stick"), new Vec2(100, 100));
            var plain = Reg.Render(set, new RenderContext(Doc(set))).Count(p => p.Tag == "planet");
            var stepped = set.WithParam("stepped", true);
            Assert.True(Reg.Render(stepped, new RenderContext(Doc(stepped))).Count(p => p.Tag == "planet") > plain);
            Assert.True(Assemblies.CanExplode(Reg, set)); // the schematic explodes through the axial view
            Assert.False(Assemblies.CanExplode(Reg, stepped)); // stepped planets have no recipe
            var p8 = Reg.Type("mech.gearset.planetary")!.Parameters.Single(x => x.Key == "planets");
            Assert.Equal(2, p8.Min);
            Assert.Equal(8, p8.Max);
        }
    }
}
