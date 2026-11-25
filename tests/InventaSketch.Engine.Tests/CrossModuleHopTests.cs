using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Engine.Tests
{
    /// <summary>One crossing rule for every line family (<see cref="Crossings"/>): lines of different modules hop over each other.</summary>
    public class CrossModuleHopTests
    {
        private static readonly Registry Reg = ModuleCatalog.CreateDefault();

        private static SketchObject Line(string typeId, Vec2 a, Vec2 b)
        {
            var item = Reg.Library.First(i => i.TypeId == typeId);
            return Reg.Create(item, (a + b) * 0.5) with { Points = new List<Vec2> { a, b }, Attach = null };
        }

        [Theory]
        [InlineData("fluid.line", "elec.wire")]
        [InlineData("process.line", "energy.line.cable")]
        [InlineData("energy.line.pipe", "bs.pipe")]
        [InlineData("bs.pipe", "fluid.line")]
        public void TheLaterLineHopsOverALineOfAnotherModule(string first, string second)
        {
            var under = Line(first, new Vec2(20, 50), new Vec2(120, 50));
            var over = Line(second, new Vec2(70, 10), new Vec2(70, 90));
            var doc = SketchDocument.Create();
            doc = Edits.Add(doc, doc.Pages[0].Id, under, over);
            var crossings = Crossings.Find(Reg, doc, over);
            var c = Assert.Single(crossings);
            Assert.Equal(under.Id, c.Other);
            Assert.True(c.Above, "the later line is drawn over the earlier one");
            Assert.InRange(c.At.X, 69.9, 70.1);
            Assert.False(Assert.Single(Crossings.Find(Reg, doc, under)).Above, "the earlier line does not hop");
            // The drawing of the later line carries the hop (a path with arcs instead of a straight polyline).
            var drawn = Reg.Render(doc.Get(over.Id)!, new RenderContext(doc));
            Assert.Contains(drawn, p => p is PathPrim path && path.Segments.Count > 2);
            // Moving the earlier line redraws the later one (its hop depends on it).
            Assert.Contains(over.Id, Reg.Type(under.Type)!.Dependents(doc.Get(under.Id)!, new RenderContext(doc).For(Reg)));
        }

        [Fact]
        public void EnergyFlowArrowsDoNotHop()
        {
            var under = Line("energy.line.cable", new Vec2(20, 50), new Vec2(120, 50));
            var arrow = Line("energy.line.flow", new Vec2(70, 10), new Vec2(70, 90));
            var doc = SketchDocument.Create();
            doc = Edits.Add(doc, doc.Pages[0].Id, under, arrow);
            Assert.Empty(Crossings.Find(Reg, doc, arrow));
            Assert.Empty(Crossings.Find(Reg, doc, under));
        }
    }
}
