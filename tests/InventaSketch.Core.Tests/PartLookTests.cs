using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Core.Tests
{
    /// <summary>Part looks (charts v2): key resolution, hiding, styling and group tag inheritance.</summary>
    public class PartLookTests
    {
        private static readonly Style Thin = new() { Stroke = "#000000", StrokeWidth = 0.25 };

        [Fact]
        public void TheLongestMatchingKeyWins()
        {
            var parts = new Dictionary<string, PartLook>
            {
                ["series"] = new() { Style = new Style { StrokeWidth = 1 } },
                ["series.1"] = new() { Hidden = true },
            };
            Assert.Equal(1, PartLooks.LookOf(parts, "series.0")!.Style!.StrokeWidth);
            Assert.True(PartLooks.LookOf(parts, "series.1")!.Hidden);
            Assert.Null(PartLooks.LookOf(parts, "seriesx"));
            Assert.Null(PartLooks.LookOf(parts, "axis.x"));
        }

        [Fact]
        public void HiddenPartsAreDroppedAndStyledOnesRestyledAlsoInsideGroups()
        {
            var a = Prim.Line(new Vec2(0, 0), new Vec2(10, 0), Thin) with { Tag = "series.0" };
            var b = Prim.Line(new Vec2(0, 5), new Vec2(10, 5), Thin) with { Tag = "series.1" };
            // Children without a tag inherit the group's tag.
            var g = new GroupPrim(new Primitive[] { Prim.Line(new Vec2(0, 9), new Vec2(10, 9), Thin), Prim.Line(new Vec2(0, 8), new Vec2(10, 8), Thin) }) { Tag = "grid.major" };
            var parts = new Dictionary<string, PartLook>
            {
                ["series.1"] = new() { Hidden = true },
                ["grid"] = new() { Style = new Style { Stroke = "#ff0000", Dash = DashStyle.Dot } },
            };
            var result = PartLooks.Apply(new Primitive[] { a, b, g }, parts);
            Assert.Equal(2, result.Count);
            Assert.Same(a, result[0]);
            var grid = Assert.IsType<GroupPrim>(result[1]);
            Assert.All(grid.Children.Cast<LinePrim>(), l => { Assert.Equal("#ff0000", l.Style.Stroke); Assert.Equal(DashStyle.Dot, l.Style.Dash); Assert.Equal(0.25, l.Style.StrokeWidth); });
            Assert.Same(a, PartLooks.Apply(new Primitive[] { a }, null)[0]);
        }

        [Fact]
        public void HitTestPrefersTextsThenLinesThenAreas()
        {
            var area = Prim.Rect(0, 0, 20, 20, new Style { Fill = Fill.Solid("#eeeeee") }) with { Tag = "region.0" };
            var line = Prim.Line(new Vec2(0, 10), new Vec2(20, 10), Thin) with { Tag = "series.0" };
            var text = new TextPrim(new Vec2(10, 10.5), "A", new TextStyle { Size = 2 }) { Tag = "note.0" };
            Assert.Equal("note.0", PartLooks.At(new Primitive[] { area, line, text }, new Vec2(10, 10.4), 0.5));
            Assert.Equal("series.0", PartLooks.At(new Primitive[] { area, line }, new Vec2(10, 10.3), 0.5));
            Assert.Equal("region.0", PartLooks.At(new Primitive[] { area, line }, new Vec2(5, 4), 0.5));
            Assert.Null(PartLooks.At(new Primitive[] { area, line }, new Vec2(40, 40), 0.5));
            // Only listed keys count; a longer tag resolves to its listed prefix.
            Assert.Equal("series", PartLooks.At(new Primitive[] { line }, new Vec2(10, 10), 0.5, new[] { "series" }));
        }
    }
}
