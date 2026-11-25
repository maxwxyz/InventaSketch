using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using InventaSketch.Modules.Charts;
using Xunit;

namespace InventaSketch.Modules.Geometry.Tests
{
    /// <summary>
    /// Charts v2 stage 4: the 2D coordinate system and the function graphs moved into the charts module. The math items step
    /// back while the charts module is registered, existing objects keep rendering, and points and vectors dock on the
    /// chart's axes.
    /// </summary>
    public class ChartsMergeTests
    {
        private static readonly Dictionary<string, string> Moved = new()
        {
            ["math.axes.cartesian"] = "chart.axes.math",
            ["math.axes.cartesian.quadrant"] = "chart.axes.math.quadrant",
            ["math.graph.function"] = "chart.function",
            ["math.graph.function.linear"] = "chart.function.linear",
            ["math.graph.function.sine"] = "chart.function.sine",
            ["math.graph.function.exponential"] = "chart.function.exponential",
        };

        private static Registry Both() => new Registry().Register(new GeometryModule()).Register(new ChartsModule());

        [Fact]
        public void MovedItemsNameTheirChartAlternative()
        {
            var math = Fx.Reg();
            foreach (var item in math.AllItems)
            {
                if (Moved.TryGetValue(item.Id, out var alt)) Assert.Equal(alt, item.Alternative);
                else Assert.Null(item.Alternative);
            }
            // Alone, the math module still offers everything.
            Assert.Equal(math.AllItems.Count, math.Library.Count);
            var charts = new Registry().Register(new ChartsModule());
            foreach (var alt in Moved.Values) Assert.NotNull(charts.Item(alt));
        }

        [Fact]
        public void WithTheChartsModuleTheLibraryShowsOnlyTheChartVersion()
        {
            var r = Both();
            foreach (var (id, alt) in Moved)
            {
                Assert.DoesNotContain(r.Library, i => i.Id == id);
                Assert.NotNull(r.Item(id));                       // existing slides: still found by id
                Assert.Contains(r.Library, i => i.Id == alt);
            }
            // The function graph category has no math items left; the coordinate systems keep number line, 3D and polar.
            Assert.DoesNotContain(r.Categories, c => c.Id == MathCategories.Graphs);
            Assert.Contains(r.Library, i => i.Id == "math.axes.number");
            Assert.Contains(r.Library, i => i.Id == "math.axes.polar");
            Assert.Contains(r.Search("koordinatensystem"), i => i.Id == "chart.axes.math");
            Assert.Contains(r.Search("parabel"), i => i.Id == "chart.function");
            Assert.DoesNotContain(r.Search("parabel"), i => i.Id.StartsWith("math.graph", StringComparison.Ordinal));
        }

        [Fact]
        public void ExistingMathObjectsKeepRendering()
        {
            var r = Both();
            foreach (var id in Moved.Keys)
            {
                var o = Fx.Item(r, id);
                Fx.AssertInsideBox(r.TypeOf(o).Render(o, Fx.Ctx()), o.Size, id);
            }
        }

        [Fact]
        public void VectorsSuggestTheChartCoordinateSystem()
        {
            var r = Both();
            var v = Fx.Item(r, "math.vector.arrow");
            Assert.Contains(r.Suggestions(v), i => i.Id == "chart.axes.math");
            Assert.DoesNotContain(r.Suggestions(v), i => i.Id == "math.axes.cartesian");
            // Without the charts module the math coordinate system is offered.
            Assert.Contains(Fx.Reg().Suggestions(Fx.Item(Fx.Reg(), "math.vector.arrow")), i => i.Id == "math.axes.cartesian");
            var axes = Fx.Item(r, "chart.axes.math");
            Assert.Contains(r.Suggestions(axes), i => i.Id == "math.vector.arrow");
        }

        [Theory]
        [InlineData("chart.axes.math", "origin")]
        [InlineData("chart.axes.math", "g2_3")]
        [InlineData("chart.function", "ext0")]
        public void VectorEndsDockOnTheChartAndFollowIt(string chartItem, string anchorId)
        {
            var r = Both();
            var chart = Fx.Item(r, chartItem, new Vec2(120, 90));
            var doc = SketchDocument.Create(new Size2(300, 200));
            doc = Edits.Add(doc, doc.Pages[0].Id, chart);
            var target = r.PageAnchors(chart, new RenderContext(doc)).Single(a => a.Id == anchorId);
            var tail = target.P + new Vec2(-25, 15);
            var vector = Fx.Item(r, "math.vector.arrow", tail) with { Points = new[] { tail, target.P } };
            vector = vector with { Attach = new[] { null, Attachments.For(chart, target.P, target) } };
            Assert.Equal(anchorId, vector.Attach![1]!.AnchorId);
            doc = Edits.Add(doc, doc.Pages[0].Id, vector);

            foreach (var change in new Func<SketchObject, SketchObject>[]
            {
                x => x with { Transform = x.Transform with { X = x.Transform.X + 17, Y = x.Transform.Y - 9 } },
                x => x with { Size = new Size2(x.Size.W * 1.5, x.Size.H * 0.8) },
                x => x with { Transform = x.Transform with { Rotation = 90 } },
            })
            {
                var moved = Edits.Update(doc, new[] { chart.Id }, change);
                moved = Attachments.Refresh(r, moved, new RenderContext(moved));
                var now = r.PageAnchors(moved.Get(chart.Id)!, new RenderContext(moved)).Single(a => a.Id == anchorId);
                var end = moved.ResolvePoints(moved.Get(vector.Id)!)[1];
                Assert.True(end.DistanceTo(now.P) < 0.05, $"{anchorId}: end {end} off {now.P}");
                Assert.NotEmpty(r.TypeOf(vector).Render(moved.Get(vector.Id)!, new RenderContext(moved)));
            }
        }

        [Fact]
        public void VectorsDockAnywhereOnTheChartSurface()
        {
            var r = Both();
            var chart = Fx.Item(r, "chart.axes.math", new Vec2(50, 50));
            Assert.True(r.TypeOf(Fx.Item(r, "math.vector.arrow")).DocksOnSurface);
            var a = Attachments.OnSurface(new Vec2(55, 48), new[] { chart });
            Assert.NotNull(a);
            Assert.Equal(chart.Id, a!.ObjectId);
            // The axes are rails to snap to.
            var rails = r.PageAnchors(chart, Fx.Ctx()).Where(x => x.Role == AnchorRole.Rail).Select(x => x.Id).ToList();
            Assert.Contains("axis.x", rails);
            Assert.Contains("axis.y", rails);
        }
    }
}
