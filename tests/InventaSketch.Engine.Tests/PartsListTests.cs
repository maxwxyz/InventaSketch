using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Engine.Tests
{
    /// <summary>The parts list table lists the parts of the document and follows every part edit.</summary>
    public class PartsListTests
    {
        private static readonly Registry Reg = ModuleCatalog.CreateDefault();

        [Fact]
        public void RowsCountKindsOfPartsAndTheTableRendersThem()
        {
            var gear = Reg.Library.First(i => i.TypeId == "mech.gear.spur" && i.View == "axial");
            var doc = SketchDocument.Create();
            var a = Reg.Create(gear, new Vec2(20, 20));
            var b = Reg.Create(gear, new Vec2(60, 20));
            var arrow = Reg.Create(Reg.Library.First(i => i.TypeId == "ann.arrow"), new Vec2(0, 0));
            var table = Reg.Create(Reg.Library.First(i => i.TypeId == "ann.partslist"), new Vec2(100, 100));
            doc = Edits.Add(doc, doc.Pages[0].Id, a, b, arrow, table);
            var rows = PartsList.Rows(Reg, doc.Objects.Values, Lang.De);
            var row = Assert.Single(rows);
            Assert.Equal(2, row.Quantity);
            var prims = Reg.Render(doc.Get(table.Id)!, new RenderContext(doc, Lang.De));
            var texts = Prim.Flatten(prims).OfType<TextPrim>().Select(t => t.Text).ToList();
            Assert.Contains("Menge", texts);
            Assert.Contains("2", texts);
            Assert.All(Prim.Flatten(prims).SelectMany(Prim.Points), p => Assert.True(double.IsFinite(p.X) && double.IsFinite(p.Y)));
        }

        [Fact]
        public void APartEditMakesTheTableADependent()
        {
            var api = new EngineApi(ModuleCatalog.CreateDefault());
            var gear = api.Registry.Library.First(i => i.TypeId == "mech.gear.spur" && i.View == "axial");
            var doc = SketchDocument.Create();
            var a = api.Registry.Create(gear, new Vec2(20, 20));
            var table = api.Registry.Create(api.Registry.Library.First(i => i.TypeId == "ann.partslist"), new Vec2(100, 100));
            doc = Edits.Add(doc, doc.Pages[0].Id, a, table);
            var after = Edits.Update(doc, new[] { a.Id }, o => o.WithParam("teeth", 30));
            Assert.Contains(table.Id, ChangeSet.DependentsOf(api.Registry, doc, after, new[] { a.Id }, Lang.En));
            var moveArrow = ChangeSet.DependentsOf(api.Registry, doc, doc, new[] { table.Id }, Lang.En);
            Assert.DoesNotContain(table.Id, moveArrow);
        }
    }
}
