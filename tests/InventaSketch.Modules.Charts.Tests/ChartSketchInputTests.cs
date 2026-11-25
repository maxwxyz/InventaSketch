using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Charts.Tests
{
    /// <summary>Sketching a curve into a chart (charts v2 stage 3, host entry point).</summary>
    public class ChartSketchInputTests
    {
        /// <summary>A hand trail in page mm along <paramref name="f"/> (data coordinates) with a little jitter.</summary>
        private static List<Vec2> Trail(Registry r, SketchObject chart, Func<double, double> f, double x0, double x1, int panel = 0)
        {
            var c = ((ChartType)r.TypeOf(chart)).Compose(chart, Fx.Ctx());
            var m = chart.Matrix();
            var rnd = new Random(7);
            var list = new List<Vec2>();
            for (var i = 0; i <= 80; i++)
            {
                var x = x0 + (x1 - x0) * i / 80.0;
                var local = c.F.P(c.F.Panels[panel], x, f(x));
                list.Add(m.Apply(local + new Vec2((rnd.NextDouble() - 0.5) * 0.3, (rnd.NextDouble() - 0.5) * 0.3)));
            }
            return list;
        }

        [Fact]
        public void AStepSketchedIntoAnXyChartIsRecognisedAndAdded()
        {
            var r = Fx.Reg();
            var chart = Fx.Item(r, "chart.line", new Vec2(120, 90));
            var before = ChartData.Xy(ChartData.Parse(chart.Str("data", ""))).Count;
            var result = ChartSketchInput.Add(r, chart, Trail(r, chart, x => x < 3 ? 1.5 : 4.5, 0.2, 5.8), Fx.Ctx(Lang.De))!;
            Assert.NotNull(result);
            Assert.Equal(CurveKind.Step, result.Kind);
            Assert.StartsWith("Erkannt", result.Message.De);
            var series = ChartData.Xy(ChartData.Parse(result.Chart.Str("data", "")));
            Assert.Equal(before + 1, series.Count);
            var added = series[series.Count - 1].Points;
            Assert.InRange(added.First().Y, 1.0, 2.0);
            Assert.InRange(added.Last().Y, 4.0, 5.0);
            Fx.AssertInsideBox(Fx.Render(r, result.Chart), result.Chart.Size, "sketched");
        }

        [Fact]
        public void AScribbleIsSmoothedAndAFigureGetsASeries()
        {
            var r = Fx.Reg();
            var figure = Fx.Item(r, "chart.figure.timing", new Vec2(120, 90));
            var spec = FigureSpec.Parse(figure.Str("spec", ""), Lang.En);
            var c = ((ChartType)r.TypeOf(figure)).Compose(figure, Fx.Ctx());
            double x0 = c.F.X.Min, x1 = c.F.X.Max;
            var y = c.F.Panels[1].Y;
            double Mid(double x) => y.Min + (y.Max - y.Min) * (0.5 + 0.3 * Math.Sin(7 * (x - x0) / (x1 - x0)) * Math.Cos(13 * (x - x0) / (x1 - x0)));
            var result = ChartSketchInput.Add(r, figure, Trail(r, figure, Mid, x0 + (x1 - x0) * 0.1, x0 + (x1 - x0) * 0.9, panel: 1), Fx.Ctx())!;
            Assert.NotNull(result);
            var after = FigureSpec.Parse(result.Chart.Str("spec", ""), Lang.En);
            Assert.Equal(spec.Series.Count + 1, after.Series.Count);
            Assert.Equal(1, after.Series.Last().Panel);
            Assert.True(after.Series.Last().X.Count is >= 2 and <= ChartSketchInput.MaxPoints);
            Fx.AssertInsideBox(Fx.Render(r, result.Chart), result.Chart.Size, "sketched figure");
        }

        [Fact]
        public void OtherChartsAndShortTrailsAreLeftAlone()
        {
            var r = Fx.Reg();
            var bars = Fx.Item(r, "chart.column");
            Assert.False(ChartSketchInput.Supports(bars));
            Assert.Null(ChartSketchInput.Add(r, bars, new[] { new Vec2(0, 0), new Vec2(1, 1), new Vec2(2, 2) }, Fx.Ctx()));
            Assert.Null(ChartSketchInput.Add(r, Fx.Item(r, "chart.line"), new[] { new Vec2(0, 0) }, Fx.Ctx()));
        }
    }
}
