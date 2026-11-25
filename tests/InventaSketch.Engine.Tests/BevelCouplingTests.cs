using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Engine.Tests
{
    /// <summary>Bevel gears with a common cone apex and crossing axes mesh and move together (<see cref="Coupling"/>).</summary>
    public class BevelCouplingTests
    {
        private static readonly Registry Reg = ModuleCatalog.CreateDefault();

        private static Vec2 Apex(SketchDocument doc, string id)
            => Reg.PageAnchors(doc.Get(id)!, new RenderContext(doc)).First(a => a.Kind == AnchorKind.Point && a.Role == AnchorRole.Apex).P;

        [Fact]
        public void AMitrePairMeshesAndTheMatingGearFollows()
        {
            var item = Reg.Library.First(i => i.TypeId == "mech.gear.bevel" && i.View == "stick");
            var a = Reg.Create(item, new Vec2(100, 100));
            var b = Reg.Create(item, new Vec2(140, 60)) with { Transform = new Transform(140, 60, 90) };
            var doc = SketchDocument.Create();
            doc = Edits.Add(doc, doc.Pages[0].Id, a, b);
            var shift = Apex(doc, a.Id) - Apex(doc, b.Id);
            doc = Edits.Move(doc, new[] { b.Id }, shift);
            Assert.Contains(Coupling.Graph(Reg, doc), g => g.Kind == LinkKind.Mesh && (g.A == a.Id || g.B == a.Id));

            var moved = Edits.Move(doc, new[] { a.Id }, new Vec2(7, -3));
            var result = Coupling.Propagate(Reg, doc, moved);
            Assert.True(Apex(result, a.Id).DistanceTo(Apex(result, b.Id)) < 1e-6, "the apexes stay together");

            // Two bevels on one axis (same orientation) do not mesh.
            var c = Reg.Create(item, new Vec2(100, 100));
            var twin = SketchDocument.Create();
            twin = Edits.Add(twin, twin.Pages[0].Id, a, c);
            Assert.DoesNotContain(Coupling.Graph(Reg, twin), g => g.Kind == LinkKind.Mesh);
        }

        /// <summary>An angle drive 12:36 passes the speed on with i = 3; a differential with the cage held reverses.</summary>
        [Fact]
        public void BevelPairsPassTheSpeedOnByTheirTeeth()
        {
            var item = Reg.Library.First(i => i.TypeId == "mech.gear.bevel" && i.View == "stick");
            var pinion = Reg.Create(item, new Vec2(100, 100)).WithParam("teeth", 12.0);
            var wheel = Reg.Create(item, new Vec2(140, 60)).WithParam("teeth", 36.0) with { Transform = new Transform(140, 60, 90) };
            var doc = SketchDocument.Create();
            doc = Edits.Add(doc, doc.Pages[0].Id, pinion, wheel);
            doc = Edits.Move(doc, new[] { wheel.Id }, Apex(doc, pinion.Id) - Apex(doc, wheel.Id));
            var speeds = GearTrain.Speeds(Reg, doc, pinion.Id);
            Assert.Equal(1, speeds[pinion.Id], 6);
            Assert.Equal(-1.0 / 3, speeds[wheel.Id], 6);
            var link = Links.For(Reg, doc, new[] { pinion.Id }).First(g => g.Kind == LinkKind.Mesh);
            Assert.Contains("i = 3", Links.Describe(Reg, doc, link, pinion.Id));

            // Side gears facing each other on one axis, one spider gear between them (cage held): opposite senses on the axis,
            // i.e. equal speeds about their own axes pointing to the apex.
            var left = Reg.Create(item, new Vec2(100, 100));
            var right = Reg.Create(item, new Vec2(100, 100)) with { Transform = new Transform(100, 100, 180) };
            var spider = Reg.Create(item, new Vec2(100, 100)) with { Transform = new Transform(100, 100, 90) };
            var diff = SketchDocument.Create();
            diff = Edits.Add(diff, diff.Pages[0].Id, left, right, spider);
            var apex = Apex(diff, left.Id);
            diff = Edits.Move(diff, new[] { right.Id }, apex - Apex(diff, right.Id));
            diff = Edits.Move(diff, new[] { spider.Id }, apex - Apex(diff, spider.Id));
            var w = GearTrain.Speeds(Reg, diff, left.Id);
            Assert.Equal(1, w[right.Id], 6);
            Assert.Equal(-1, w[spider.Id], 6);
        }
    }
}
