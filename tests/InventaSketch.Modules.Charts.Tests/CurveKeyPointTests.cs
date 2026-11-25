using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Charts.Tests
{
    /// <summary>
    /// Adaptive key points of sketched curves (maintainer 2026-11-25: "it sometimes samples too many points – maybe it can be
    /// set, or depend on how fast or which curve you draw?"): few stored points by shape, the "Detail" choice, the drawing
    /// speed as a hint, point grips and old data.
    /// </summary>
    public class CurveKeyPointTests
    {
        /// <summary>A hand trail in page mm along <paramref name="f"/> (data coordinates), 161 points with jitter.</summary>
        private static List<Vec2> Trail(Registry r, SketchObject chart, Func<double, double> f, double x0 = 0.2, double x1 = 5.8, double jitter = 0.3, int seed = 7)
        {
            var c = Fx.Type(r, chart).Compose(chart, Fx.Ctx());
            var m = chart.Matrix();
            var rnd = new Random(seed);
            var list = new List<Vec2>();
            for (var i = 0; i <= 160; i++)
            {
                var x = x0 + (x1 - x0) * i / 160.0;
                var local = c.F.P(c.F.Panels[0], x, f(x));
                list.Add(m.Apply(local + new Vec2((rnd.NextDouble() - 0.5) * jitter, (rnd.NextDouble() - 0.5) * jitter)));
            }
            return list;
        }

        /// <summary>Time stamps (ms) of a trail drawn at a steady pace in <paramref name="seconds"/>.</summary>
        private static List<double> Steady(int count, double seconds) => Enumerable.Range(0, count).Select(i => seconds * 1000.0 * i / (count - 1)).ToList();

        private static SketchObject Chart(Registry r) => Fx.Item(r, "chart.line", new Vec2(120, 90)).WithParam("data", "0;0\n6;6");

        private static CurveSketchMemo Sketch(Registry r, Func<double, double> f, double jitter = 0.3, IReadOnlyList<double>? times = null, int seed = 7)
        {
            var chart = Chart(r);
            var trail = Trail(r, chart, f, jitter: jitter, seed: seed);
            var o = ChartSketchInput.Add(r, chart, trail, Fx.Ctx(), times)!.Chart;
            return ChartSketchInput.Memo(o, 1)!;
        }

        private static double Line(double x) => 1 + 0.6 * x;
        private static double Arc(double x) => 1 + 3.5 * (1 - Math.Pow((x - 3) / 2.8, 2));
        private static double Sine(double x) => 3 + 1.2 * Math.Sin(2 * Math.PI * x / 3);
        private static double Wavy(double x) => 3 + 0.8 * Math.Sin(2.3 * x) * Math.Cos(0.9 * x) + 0.3 * x * Math.Sin(5.1 * x) / 6;

        private static int Count(CurveSketchMemo m, CurveShape shape, CurveDetail detail = CurveDetail.Medium)
            => CurveShapes.Points(m with { Shape = shape, Detail = detail }).Count;

        [Fact]
        public void AStraightLineKeepsTwoPoints()
        {
            var r = Fx.Reg();
            var m = Sketch(r, Line);
            Assert.Equal(CurveKind.Ramp, m.Fit);
            foreach (var shape in new[] { CurveShape.Smoothed, CurveShape.Interpolated, CurveShape.Fitted, CurveShape.Segments })
                Assert.Equal(2, Count(m, shape));
            // "As drawn" keeps the light tremor, thinned.
            Assert.InRange(Count(m, CurveShape.Raw), 2, m.Raw.Count / 3);
        }

        [Fact]
        public void ASingleArcKeepsFewPointsAndTheSplineFollowsIt()
        {
            var r = Fx.Reg();
            var m = Sketch(r, Arc) with { Shape = CurveShape.Smoothed };
            Assert.InRange(Count(m, CurveShape.Smoothed), 3, 8);
            Assert.InRange(Count(m, CurveShape.Interpolated), 3, 8);
            // The curve as drawn (spline through the key points) follows the arc.
            var drawn = CurveShapes.Drawn(m);
            Assert.True(drawn.Count > 3 * Count(m, CurveShape.Smoothed));
            foreach (var p in drawn) Assert.InRange(p.Y - Arc(p.X), -0.2, 0.2);
        }

        [Fact]
        public void ASineKeepsMorePointsThanAnArcAndTheirTurningPoints()
        {
            var r = Fx.Reg();
            var sine = Sketch(r, Sine);
            var arc = Sketch(r, Arc);
            Assert.Equal(CurveKind.Sine, sine.Fit);
            foreach (var shape in new[] { CurveShape.Smoothed, CurveShape.Interpolated, CurveShape.Fitted })
            {
                var n = Count(sine, shape);
                Assert.True(n > Count(arc, shape), shape + ": sine " + n + ", arc " + Count(arc, shape));
                Assert.InRange(n, 6, 30);
            }
            // The fitted sine keeps a point near each peak and valley (good grips) and the spline stays on the function.
            var fitted = CurveShapes.Points(sine);
            var spec = sine.FitSpec!;
            foreach (var peak in new[] { 0.75, 2.25, 3.75, 5.25 })
                Assert.True(fitted.Min(p => Math.Abs(p.X - peak)) < 0.2, "turning point at " + peak);
            foreach (var p in CurveShapes.Drawn(sine)) Assert.InRange(p.Y - CurveFunctions.Evaluate(spec, p.X), -0.06, 0.06);
        }

        [Fact]
        public void ANoisyScribbleStaysBoundedInEveryShapeAndDetail()
        {
            var r = Fx.Reg();
            var chart = Chart(r);
            // Fast zig-zag with strong tremor.
            var m = Sketch(r, x => 3 + 2.2 * Math.Sin(9 * x) * Math.Cos(2.1 * x), jitter: 2.5, seed: 11);
            foreach (var shape in CurveShapes.Offered(m))
                foreach (var detail in CurveShapes.Details)
                {
                    var n = Count(m, shape, detail);
                    Assert.InRange(n, 2, CurveShapes.MaxOf(detail));
                    Assert.True(n <= CurveShapes.MaxPoints);
                }
            // Every sketched curve keeps few enough points for its grips.
            Assert.True(CurveShapes.MaxPoints <= ChartPoints.MaxPointHandles);
        }

        [Fact]
        public void TheDetailOrdersThePointCounts()
        {
            var r = Fx.Reg();
            foreach (var f in new Func<double, double>[] { Arc, Sine, Wavy })
            {
                var m = Sketch(r, f, jitter: 0.8);
                foreach (var shape in CurveShapes.Offered(m).Where(s => CurveShapes.HasDetail(m with { Shape = s })))
                {
                    int lo = Count(m, shape, CurveDetail.Low), mid = Count(m, shape), hi = Count(m, shape, CurveDetail.High);
                    Assert.True(lo <= mid && mid <= hi, shape + ": " + lo + " / " + mid + " / " + hi);
                }
                Assert.True(Count(m, CurveShape.Smoothed, CurveDetail.Low) < Count(m, CurveShape.Smoothed, CurveDetail.High));
            }
        }

        [Fact]
        public void ThePaceRuleIsTheSquareRootOfTheSpeedClamped()
        {
            // 1 plot size per second → factor 1; 4 → √4 = 2, clamped to 1.5; 1/4 → 0.5, clamped to 0.6; 0.49 → 0.7.
            var line = Enumerable.Range(0, 101).Select(i => new Vec2(i / 100.0, 0.5)).ToList();
            double At(double seconds) => CurveKeyPoints.Pace(line, Steady(101, seconds))![50];
            Assert.Equal(1.0, At(1), 6);
            Assert.Equal(CurveKeyPoints.PaceMax, At(0.25), 6);
            Assert.Equal(CurveKeyPoints.PaceMin, At(4), 6);
            Assert.Equal(0.7, At(1 / 0.49), 6);
            // Unusable time stamps give no pace.
            Assert.Null(CurveKeyPoints.Pace(line, null));
            Assert.Null(CurveKeyPoints.Pace(line, Steady(100, 1)));
            Assert.Null(CurveKeyPoints.Pace(line, Enumerable.Repeat(5.0, 101).ToList()));
            Assert.Null(CurveKeyPoints.Pace(line, Steady(101, 1).Select((t, i) => i == 50 ? -1 : t).ToList()));
        }

        [Fact]
        public void SlowDrawingKeepsMoreDetailThanAFastStroke()
        {
            var r = Fx.Reg();
            var none = Sketch(r, Wavy, jitter: 0.8);
            var slow = Sketch(r, Wavy, jitter: 0.8, times: Steady(161, 8));
            var fast = Sketch(r, Wavy, jitter: 0.8, times: Steady(161, 0.3));
            Assert.Empty(none.Pace);
            Assert.Equal(slow.Raw.Count, slow.Pace.Count);
            Assert.All(slow.Pace, f => Assert.Equal(CurveKeyPoints.PaceMin, f, 6));
            Assert.All(fast.Pace, f => Assert.Equal(CurveKeyPoints.PaceMax, f, 6));
            // The trail is the same: only the tolerances differ.
            Assert.Equal(none.Raw, slow.Raw);
            foreach (var shape in new[] { CurveShape.Smoothed, CurveShape.Interpolated, CurveShape.Segments, CurveShape.Raw })
            {
                int s = Count(slow, shape), n = Count(none, shape), q = Count(fast, shape);
                Assert.True(s >= n && n >= q && s > q, shape + ": slow " + s + ", none " + n + ", fast " + q);
            }
            // Subtle: a slow stroke stays below high detail, a fast one above low detail (for the smoothed shape).
            Assert.True(Count(slow, CurveShape.Smoothed) <= Count(none, CurveShape.Smoothed, CurveDetail.High));
            Assert.True(Count(fast, CurveShape.Smoothed) >= Count(none, CurveShape.Smoothed, CurveDetail.Low));
            // A stroke drawn slowly on its first half and fast on its second keeps more points on the first, fewer on the second.
            var times = new List<double>();
            for (var i = 0; i <= 160; i++) times.Add(i <= 80 ? i * 50.0 : 80 * 50.0 + (i - 80) * 2.0);
            var mixed = Sketch(r, Wavy, jitter: 0.8, times: times) with { Shape = CurveShape.Smoothed };
            Assert.True(mixed.Pace.First() < 1 && mixed.Pace.Last() > 1);
            var pm = CurveShapes.Points(mixed);
            var pn = CurveShapes.Points(none with { Shape = CurveShape.Smoothed });
            var why = "paced " + string.Join(" ", pm.Select(p => p.X.ToString("F1"))) + " | none " + string.Join(" ", pn.Select(p => p.X.ToString("F1")));
            Assert.True(pm.Count(p => p.X < 2.6) >= pn.Count(p => p.X < 2.6), why);
            Assert.True(pm.Count(p => p.X > 3.4) <= pn.Count(p => p.X > 3.4), why);
            Assert.NotEqual(pn.Count(p => p.X < 2.6) - pn.Count(p => p.X > 3.4), pm.Count(p => p.X < 2.6) - pm.Count(p => p.X > 3.4));
        }

        [Fact]
        public void TheDetailChoiceRoundTripsAndKeepsTheMemo()
        {
            var r = Fx.Reg();
            var chart = Chart(r);
            var o = ChartSketchInput.Add(r, chart, Trail(r, chart, Wavy, jitter: 0.8), Fx.Ctx(), Steady(161, 1.5))!.Chart;
            o = ChartSketchInput.WithShape(o, 1, m => m with { Shape = CurveShape.Smoothed });
            var memo = ChartSketchInput.Memo(o, 1)!;
            Assert.Equal(CurveDetail.Medium, memo.Detail);
            var before = ChartData.Xy(ChartData.Parse(o.Str("data", ""))).Last().Points;
            var actions = r.PartActions(o, "series.1", Fx.Ctx());
            var detail = actions.Where(a => a.Choice?.Key == CurveShapes.DetailAction).ToList();
            Assert.Equal(new[] { "low", "medium", "high" }.Select(v => CurveShapes.DetailAction + "=" + v), detail.Select(a => a.Id));
            Assert.Equal(CurveShapes.DetailAction + "=medium", Assert.Single(detail, a => a.IsActive).Id);
            Assert.Equal("Detail", detail[0].Choice!.Label.En);
            Assert.Equal("Detailgrad", detail[0].Choice!.Label.De);
            Assert.Equal("Mittel (adaptiv)", detail[1].Label.De);
            Assert.All(detail, a => Assert.NotNull(a.Preview));
            Assert.DoesNotContain(actions, a => a.Icon == IconIds.Cycle);

            var high = detail.Single(a => a.Id.EndsWith("=high")).Apply(o);
            var hm = ChartSketchInput.Memo(high, 1)!;
            Assert.Equal(CurveDetail.High, hm.Detail);
            Assert.Equal(memo.Raw, hm.Raw); // the trail memo is kept …
            Assert.Equal(memo.Pace, hm.Pace); // … with its pace
            Assert.Contains("detail=high", high.Str("data", ""));
            Assert.Contains("pace=", high.Str("data", ""));
            var more = ChartData.Xy(ChartData.Parse(high.Str("data", ""))).Last().Points;
            Assert.True(more.Count > before.Count);
            // Back to medium: exactly the points of before (lossless).
            var back = r.PartActions(high, "series.1", Fx.Ctx()).Single(a => a.Id == CurveShapes.DetailAction + "=medium").Apply(high);
            Assert.Equal(before, ChartData.Xy(ChartData.Parse(back.Str("data", ""))).Last().Points);
            Assert.DoesNotContain("detail=", back.Str("data", ""));

            // Comment and figure JSON round trips.
            Assert.True(CurveShapes.TryParseComment(CurveShapes.ToComment(hm), out var parsed));
            Assert.Equal(CurveDetail.High, parsed!.Detail);
            Assert.Equal(hm.Pace, parsed.Pace);
            var spec = new FigureSpec();
            spec.Series.Add(new FigSeries { X = more.Select(p => p.X).ToList(), Y = more.Select(p => p.Y).ToList(), Sketch = hm, Smooth = true });
            var json = spec.ToJson();
            Assert.Contains("\"detail\":\"high\"", json);
            var again = FigureSpec.Parse(json, Lang.En).Series[0].Sketch!;
            Assert.Equal(CurveDetail.High, again.Detail);
            Assert.Equal(hm.Pace, again.Pace);
            Assert.Equal(json, FigureSpec.Parse(json, Lang.En).ToJson());

            // Exact piecewise-linear fits offer no detail.
            var step = ChartSketchInput.Add(r, chart, Trail(r, chart, x => x < 3 ? 1.5 : 4.5), Fx.Ctx())!.Chart;
            Assert.Equal(CurveKind.Step, ChartSketchInput.Memo(step, 1)!.Fit);
            Assert.DoesNotContain(r.PartActions(step, "series.1", Fx.Ctx()), a => a.Choice?.Key == CurveShapes.DetailAction);
        }

        [Fact]
        public void FiguresDrawSmoothShapesWithTheSpline()
        {
            var r = Fx.Reg();
            var figure = Fx.Item(r, "chart.figure.timing", new Vec2(120, 90));
            var c = Fx.Type(r, figure).Compose(figure, Fx.Ctx());
            double x0 = c.F.X.Min, x1 = c.F.X.Max;
            var y = c.F.Panels[0].Y;
            double Mid(double x) => y.Min + (y.Max - y.Min) * (0.5 + 0.3 * Math.Sin(7 * (x - x0) / (x1 - x0)) * Math.Cos(13 * (x - x0) / (x1 - x0)));
            var trail = Enumerable.Range(0, 121).Select(i => x0 + (x1 - x0) * (0.1 + 0.8 * i / 120.0)).Select(x => figure.Matrix().Apply(c.F.P(c.F.Panels[0], x, Mid(x)))).ToList();
            var o = ChartSketchInput.Add(r, figure, trail, Fx.Ctx())!.Chart;
            var spec = FigureSpec.Parse(o.Str("spec", ""), Lang.En);
            var k = spec.Series.Count - 1;
            Assert.Equal(CurveShapes.IsSpline(spec.Series[k].Sketch!), spec.Series[k].Smooth);
            var seg = FigureSpec.Parse(ChartSketchInput.WithShape(o, k, m => m with { Shape = CurveShape.Segments }).Str("spec", ""), Lang.En);
            Assert.False(seg.Series[k].Smooth);
            var sm = FigureSpec.Parse(ChartSketchInput.WithShape(o, k, m => m with { Shape = CurveShape.Smoothed }).Str("spec", ""), Lang.En);
            Assert.True(sm.Series[k].Smooth);
            Assert.InRange(sm.Series[k].X.Count, 3, CurveShapes.MaxOf(CurveDetail.Medium));
        }

        [Fact]
        public void SketchedCurvesGetPointGripsAndAnEditKeepsTheSplineLook()
        {
            var r = Fx.Reg();
            var chart = Chart(r);
            var o = ChartSketchInput.Add(r, chart, Trail(r, chart, Wavy, jitter: 2.0, seed: 3), Fx.Ctx())!.Chart;
            o = ChartSketchInput.WithShape(o, 1, m => m with { Shape = CurveShape.Smoothed });
            var n = ChartData.Xy(ChartData.Parse(o.Str("data", ""))).Last().Points.Count;
            var grips = r.DragHandles(o, Fx.Ctx()).Where(h => h.Key.StartsWith("point.1.", StringComparison.Ordinal)).ToList();
            Assert.Equal(n, grips.Count); // every stored point has its grip

            // Deleting a point makes the curve plain data, but it stays a spline ("# spline").
            var edited = r.PartActions(o, "point.1.1", Fx.Ctx()).Single(a => a.Id == PartLooks.DeleteAction).Apply(o);
            var data = edited.Str("data", "");
            Assert.Null(ChartSketchInput.Memo(edited, 1));
            Assert.Contains(ChartSketchInput.SplineComment, data);
            Assert.Equal(new[] { false, true }, ChartSketchInput.XySplines(data));
            Assert.Equal(2, ChartData.Xy(ChartData.Parse(data)).Count);
            // A second edit of the other curve keeps the mark.
            var twice = r.PartActions(edited, "point.0.0", Fx.Ctx()).Single(a => a.Id == PartLooks.DeleteAction).Apply(edited);
            Assert.Equal(new[] { false, true }, ChartSketchInput.XySplines(twice.Str("data", "")));
            // Clean slides keep the look too.
            var clean = r.Type(o.Type)!.Sanitize(edited, new SanitizeOptions { Mode = SanitizeMode.Randomised, Seed = 1 }, new Random(1));
            Assert.Equal(new[] { false, true }, ChartSketchInput.XySplines(clean.Str("data", "")));
            Fx.AssertInsideBox(Fx.Render(r, edited), edited.Size, "edited");
        }

        [Fact]
        public void OldSketchedDataStillLoads()
        {
            // Data of a curve sketched before key points: a memo without detail or pace and 96 dense rows.
            var r = Fx.Reg();
            var raw = Enumerable.Range(0, 96).Select(i => 0.2 + 5.6 * i / 95.0).Select(x => new Vec2(Math.Round(x, 4), Math.Round(Sine(x), 4))).ToList();
            var memoLine = "# sketch shape=smoothed frame=0,0,6,6 raw=" + string.Join(" ", raw.Select(p => p.X.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "," + p.Y.ToString("R", System.Globalization.CultureInfo.InvariantCulture)));
            var rows = string.Join("\n", raw.Select(p => p.X.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + ";" + p.Y.ToString("R", System.Globalization.CultureInfo.InvariantCulture)));
            var o = Chart(r).WithParam("data", "0;0\n6;6\n\n" + memoLine + "\n" + rows);
            var memo = ChartSketchInput.Memo(o, 1)!;
            Assert.NotNull(memo);
            Assert.Equal(CurveDetail.Medium, memo.Detail);
            Assert.Empty(memo.Pace);
            Assert.Equal(96, ChartData.Xy(ChartData.Parse(o.Str("data", ""))).Last().Points.Count);
            Assert.Equal(new[] { false, true }, ChartSketchInput.XySplines(o.Str("data", "")));
            Fx.AssertInsideBox(Fx.Render(r, o), o.Size, "old data");
            Assert.Contains(r.PartActions(o, "series.1", Fx.Ctx()), a => a.Id == CurveShapes.DetailAction + "=medium" && a.IsActive);
            // Any choice recomputes the curve from the kept trail: now only its key points.
            var redone = r.PartActions(o, "series.1", Fx.Ctx()).Single(a => a.Id == CurveShapes.DetailAction + "=low").Apply(o);
            Assert.InRange(ChartData.Xy(ChartData.Parse(redone.Str("data", ""))).Last().Points.Count, 4, CurveShapes.MaxOf(CurveDetail.Low));
            Assert.Equal(memo.Raw, ChartSketchInput.Memo(redone, 1)!.Raw);
        }
    }
}
