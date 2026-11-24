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
    }
}
