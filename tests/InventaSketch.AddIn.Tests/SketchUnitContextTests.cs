using System.Linq;
using InventaSketch.AddIn.OnSlide;
using InventaSketch.Core;
using InventaSketch.Engine;
using Xunit;

namespace InventaSketch.AddIn.Tests
{
    /// <summary>
    /// Units on a slide each hold their own model; neighbour-dependent objects (walls) are rendered with the other units
    /// as context. These checks need no PowerPoint (the shape reference is not touched).
    /// </summary>
    public class SketchUnitContextTests
    {
        private static SketchDocument Unit(SketchObject o)
        {
            var doc = SketchDocument.Create();
            return Edits.Add(doc, doc.Pages[0].Id, o);
        }

        [Fact]
        public void WallsInSeparateUnitsJoinWhenRenderedWithContext()
        {
            var registry = ModuleCatalog.CreateDefault();
            var item = registry.Item("arch.wall")!;
            var a = registry.Create(item, Vec2.Zero) with { Points = new[] { new Vec2(0, 0), new Vec2(60, 0) } };
            var b = registry.Create(item, Vec2.Zero) with { Points = new[] { new Vec2(60, 0), new Vec2(60, 40) } };
            var unitA = Unit(a);
            var unitB = new SketchUnit(null!, b.Id, Unit(b), default);

            Assert.True(SketchUnits.NeedsContext(registry, unitA));
            Assert.False(SketchUnits.NeedsContext(registry, Unit(registry.Create(registry.Item("arch.furniture.bed")!, Vec2.Zero))));

            int Caps(SketchDocument doc) => registry.Render(doc.Get(a.Id)!, new RenderContext(doc)).Count(p => p.Tag == "wall-cap");
            Assert.Equal(2, Caps(unitA));                                     // alone: both ends capped
            var context = SketchUnits.WithContext(unitA, new[] { unitB });
            Assert.Equal(1, Caps(context));                                   // L joint with the other unit's wall
            Assert.Equal(2, context.ObjectsOn(context.Pages[0]).Count());
            Assert.Same(unitA, SketchUnits.WithContext(unitA, new SketchUnit[0]));

            // The joined wall depends on the edited one, so the host redraws its unit as well.
            Assert.Contains(b.Id, ChangeSet.DependentsOf(registry, context, context, new[] { a.Id }, Lang.En));
        }
    }
}
