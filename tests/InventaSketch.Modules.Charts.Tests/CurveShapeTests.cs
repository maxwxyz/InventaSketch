using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Charts.Tests
{
    /// <summary>Switchable shapes of sketched curves: smoothed, interpolated, fitted, straight segments, as drawn.</summary>
    public class CurveShapeTests
    {
        /// <summary>A hand trail in page mm along <paramref name="f"/> (data coordinates) with jitter of <paramref name="jitter"/> mm.</summary>
        private static List<Vec2> Trail(Registry r, SketchObject chart, Func<double, double> f, double x0, double x1, double jitter = 0.3, int panel = 0, int seed = 7)
        {
            var c = Fx.Type(r, chart).Compose(chart, Fx.Ctx());
            var m = chart.Matrix();
            var rnd = new Random(seed);
            var list = new List<Vec2>();
            for (var i = 0; i <= 160; i++)
            {
                var x = x0 + (x1 - x0) * i / 160.0;
                var local = c.F.P(c.F.Panels[panel], x, f(x));
                list.Add(m.Apply(local + new Vec2((rnd.NextDouble() - 0.5) * jitter, (rnd.NextDouble() - 0.5) * jitter)));
            }
            return list;
        }

        private static double Sine(double x) => 3 + 1.2 * Math.Sin(2 * Math.PI * x / 3);

        /// <summary>Sum of the absolute turning angles of a polyline (radians): a scribble turns a lot, a clean curve little.</summary>
        private static double Turning(IReadOnlyList<Vec2> p)
        {
            double sum = 0;
            for (var i = 1; i < p.Count - 1; i++)
            {
                var a = p[i] - p[i - 1];
                var b = p[i + 1] - p[i];
                if (a.Length < 1e-12 || b.Length < 1e-12) continue;
                sum += Math.Abs(Math.Atan2(a.X * b.Y - a.Y * b.X, a.X * b.X + a.Y * b.Y));
            }
            return sum;
        }

        private static IReadOnlyList<Vec2> LastSeries(SketchObject chart) => ChartData.Xy(ChartData.Parse(chart.Str("data", ""))).Last().Points;

        private static SketchObject Apply(Registry r, SketchObject o, string part, string action)
        {
            var a = r.PartActions(o, part, Fx.Ctx()).FirstOrDefault(x => x.Id == action);
            Assert.True(a is not null, "missing action " + action + " on " + part);
            return a!.Apply(o);
        }

        [Fact]
        public void ANoisySineIsFittedAsASineAndOffersTheShapeActions()
        {
            var r = Fx.Reg();
            var chart = Fx.Item(r, "chart.line", new Vec2(120, 90));
            var result = ChartSketchInput.Add(r, chart, Trail(r, chart, Sine, 0.2, 5.8), Fx.Ctx())!;
            Assert.Equal(CurveKind.Sine, result.Kind);
            var memo = ChartSketchInput.Memo(result.Chart, 1)!;
            Assert.NotNull(memo);
            Assert.Equal(CurveShape.Fitted, memo.Shape);
            Assert.Equal(CurveKind.Sine, memo.Fit);
            Assert.True(memo.Raw.Count is >= 8 and <= CurveShapes.MaxRaw);
            // The default series of the chart was not sketched: no shape actions there.
            Assert.Null(ChartSketchInput.Memo(result.Chart, 0));
            Assert.DoesNotContain(r.PartActions(result.Chart, "series.0", Fx.Ctx()), a => a.Choice?.Key == CurveShapes.ShapeAction);
            var actions = r.PartActions(result.Chart, "series.1", Fx.Ctx());
            // "Curve shape" is a choice (a dropdown): one option per shape, the current one active, each with a preview.
            var shapes = actions.Where(a => a.Choice?.Key == CurveShapes.ShapeAction).ToList();
            Assert.Equal(CurveShapes.Offered(memo).Select(CurveShapes.ShapeOption), shapes.Select(a => a.Id));
            Assert.Equal(CurveShapes.ShapeOption(CurveShape.Fitted), Assert.Single(shapes, a => a.IsActive).Id);
            Assert.Equal("Curve shape", shapes[0].Choice!.Label.En);
            Assert.Equal("Kurvenform", shapes[0].Choice!.Label.De);
            Assert.All(shapes, a => Assert.NotNull(a.Preview));
            Assert.DoesNotContain(actions, a => a.Icon == IconIds.Cycle);
            if (memo.Fits.Count > 1) Assert.Equal(memo.Fits.Count, actions.Count(a => a.Choice?.Key == CurveShapes.FitAction));
            Assert.DoesNotContain(actions, a => a.Choice?.Key == CurveShapes.SmoothingAction);
            // The markers part of the curve offers the same switch.
            Assert.Contains(r.PartActions(result.Chart, "marker.1", Fx.Ctx()), a => a.Choice?.Key == CurveShapes.ShapeAction);
            // The fitted curve follows the sine.
            foreach (var p in LastSeries(result.Chart)) Assert.InRange(p.Y - Sine(p.X), -0.25, 0.25);
        }

        [Fact]
        public void EveryShapeKeepsTheTrailAndHasTheExpectedForm()
        {
            var r = Fx.Reg();
            var chart = Fx.Item(r, "chart.line", new Vec2(120, 90));
            var o = ChartSketchInput.Add(r, chart, Trail(r, chart, Sine, 0.2, 5.8), Fx.Ctx())!.Chart;
            var raw = ChartSketchInput.Memo(o, 1)!.Raw;
            var seen = new Dictionary<CurveShape, IReadOnlyList<Vec2>>();
            foreach (var shape in CurveShapes.Order.Skip(3).Concat(CurveShapes.Order.Take(3)))
            {
                o = Apply(r, o, "series.1", CurveShapes.ShapeOption(shape));
                var memo = ChartSketchInput.Memo(o, 1)!;
                Assert.Equal(raw, memo.Raw); // the trail survives every switch unchanged
                seen[memo.Shape] = LastSeries(o);
                Fx.AssertInsideBox(Fx.Render(r, o), o.Size, memo.Shape.ToString());
            }
            Assert.Equal(CurveShapes.Order.Count, seen.Count); // every option was picked, the last one the fitted shape
            Assert.Equal(CurveShape.Fitted, ChartSketchInput.Memo(o, 1)!.Shape);
            Assert.Equal(raw.Count, seen[CurveShape.Raw].Count);
            Assert.InRange(seen[CurveShape.Segments].Count, 3, 30);
            Assert.True(seen[CurveShape.Interpolated].Count > seen[CurveShape.Segments].Count);
            Assert.True(seen[CurveShape.Smoothed].Count > seen[CurveShape.Segments].Count);
            Assert.True(seen.Values.All(s => s.Count <= CurveShapes.MaxPoints));
            // Interpolation passes through the key points (= the segment corners).
            foreach (var k in seen[CurveShape.Segments])
                Assert.True(seen[CurveShape.Interpolated].Min(p => p.DistanceTo(k)) < 1e-3, "key point " + k);
            // Every shape stays close to the drawn sine.
            foreach (var kv in seen)
                foreach (var p in kv.Value) Assert.InRange(p.Y - Sine(p.X), -0.45, 0.45);
        }

        [Fact]
        public void AScribbleBecomesACleanSmoothCurveAndTheStrengthCanBeChanged()
        {
            var r = Fx.Reg();
            var chart = Fx.Item(r, "chart.line", new Vec2(120, 90));
            // A wavy line with strong tremor, no standard shape.
            double Wavy(double x) => 3 + 0.8 * Math.Sin(2.3 * x) * Math.Cos(0.9 * x) + 0.3 * x * Math.Sin(5.1 * x) / 6;
            var result = ChartSketchInput.Add(r, chart, Trail(r, chart, Wavy, 0.3, 5.7, jitter: 1.2, seed: 3), Fx.Ctx())!;
            var o = result.Chart;
            var memo = ChartSketchInput.Memo(o, 1)!;
            if (result.Kind is not null) o = ChartSketchInput.WithShape(o, 1, m => m with { Shape = CurveShape.Smoothed });
            memo = ChartSketchInput.Memo(o, 1)!;
            Assert.Equal(CurveShape.Smoothed, memo.Shape);
            Assert.Equal(CurveShapes.DefaultStrength, memo.Strength);
            var smooth = LastSeries(o);
            var asDrawn = LastSeries(ChartSketchInput.WithShape(o, 1, m => m with { Shape = CurveShape.Raw }));
            var ideal = Enumerable.Range(0, 120).Select(i => 0.3 + 5.4 * i / 119.0).Select(x => new Vec2(x, Wavy(x))).ToList();
            // The tremor is in the trail as drawn; the default (medium) smoothing is as calm as the intended curve.
            var why = $"ideal {Turning(ideal):F2}, smoothed {Turning(smooth):F2}, drawn {Turning(asDrawn):F2}";
            Assert.True(Turning(asDrawn) > Turning(ideal) * 1.3, why);
            Assert.True(Turning(smooth) < Turning(ideal) * 1.1, why);
            Assert.True(Turning(smooth) < Turning(asDrawn) * 0.7, why);
            foreach (var p in smooth) Assert.InRange(p.Y - Wavy(p.X), -0.35, 0.35);

            // Smoothing: medium → strong → light → medium.
            var strong = Apply(r, o, "series.1", CurveShapes.SmoothingAction + "=3");
            Assert.Equal(3, ChartSketchInput.Memo(strong, 1)!.Strength);
            Assert.True(Turning(LastSeries(strong)) <= Turning(smooth) + 1e-9);
            var light = Apply(r, strong, "series.1", CurveShapes.SmoothingAction + "=1");
            Assert.Equal(1, ChartSketchInput.Memo(light, 1)!.Strength);
            Assert.True(Turning(LastSeries(light)) >= Turning(LastSeries(strong)));
            Assert.Equal(2, ChartSketchInput.Memo(Apply(r, light, "series.1", CurveShapes.SmoothingAction + "=2"), 1)!.Strength);
        }

        [Fact]
        public void TheFittedFunctionCanBeSwitchedThroughTheRanking()
        {
            var r = Fx.Reg();
            var chart = Fx.Item(r, "chart.line", new Vec2(120, 90));
            var o = ChartSketchInput.Add(r, chart, Trail(r, chart, x => x < 3 ? 1.5 : 4.5, 0.2, 5.8), Fx.Ctx())!.Chart;
            var memo = ChartSketchInput.Memo(o, 1)!;
            Assert.Equal(CurveKind.Step, memo.Fit);
            Assert.True(memo.Fits.Count >= 2);
            var next = Apply(r, o, "series.1", CurveShapes.FitAction + "=" + CurveFunctions.Info(memo.Fits[1].Kind).Id);
            var nm = ChartSketchInput.Memo(next, 1)!;
            Assert.Equal(memo.Fits[1].Kind, nm.Fit);
            Assert.Equal(CurveShape.Fitted, nm.Shape);
            Assert.NotEqual(LastSeries(o), LastSeries(next));
        }

        [Fact]
        public void TheMemoCommentRoundTripsAndTheDataParserIgnoresIt()
        {
            var r = Fx.Reg();
            var chart = Fx.Item(r, "chart.line", new Vec2(120, 90));
            var o = ChartSketchInput.Add(r, chart, Trail(r, chart, Sine, 0.2, 5.8), Fx.Ctx())!.Chart;
            var memo = ChartSketchInput.Memo(o, 1)!;
            var line = CurveShapes.ToComment(memo with { Strength = 3, Shape = CurveShape.Segments });
            Assert.StartsWith(CurveShapes.CommentTag, line);
            Assert.True(CurveShapes.TryParseComment(line, out var back));
            Assert.Equal(CurveShape.Segments, back!.Shape);
            Assert.Equal(3, back.Strength);
            Assert.Equal(memo.Fit, back.Fit);
            Assert.Equal(memo.Raw, back.Raw);
            Assert.Equal(memo.Frame, back.Frame);
            Assert.Equal(memo.Fits.Select(s => s.Kind), back.Fits.Select(s => s.Kind));
            Assert.Equal(CurveShapes.ToComment(back), CurveShapes.ToComment(memo with { Strength = 3, Shape = CurveShape.Segments }));
            Assert.False(CurveShapes.TryParseComment("# a plain comment", out _));
            Assert.False(CurveShapes.TryParseComment("# sketch shape=raw", out _)); // no trail

            // Two series: the memo line is a comment for the data parser.
            Assert.Equal(2, ChartData.Xy(ChartData.Parse(o.Str("data", ""))).Count);
            // A memo between the rows of a block is stale and ignored.
            var stale = "0;1\n" + line + "\n1;2\n2;3";
            Assert.Null(ChartSketchInput.XyBlock(stale, 0));
        }

        [Fact]
        public void FiguresKeepTheMemoThroughJsonAndOldSpecsAreUnchanged()
        {
            var r = Fx.Reg();
            var figure = Fx.Item(r, "chart.figure.timing", new Vec2(120, 90));
            var before = figure.Str("spec", "");
            var spec = FigureSpec.Parse(before, Lang.En);
            // Old specifications (without "sketch") are written exactly as before.
            Assert.DoesNotContain("\"sketch\"", spec.ToJson());
            Assert.Equal(spec.ToJson(), FigureSpec.Parse(spec.ToJson(), Lang.En).ToJson());
            Assert.All(spec.Series, s => Assert.Null(s.Sketch));

            var c = Fx.Type(r, figure).Compose(figure, Fx.Ctx());
            double x0 = c.F.X.Min, x1 = c.F.X.Max;
            var y = c.F.Panels[1].Y;
            double Mid(double x) => y.Min + (y.Max - y.Min) * (0.5 + 0.3 * Math.Sin(7 * (x - x0) / (x1 - x0)) * Math.Cos(13 * (x - x0) / (x1 - x0)));
            var o = ChartSketchInput.Add(r, figure, Trail(r, figure, Mid, x0 + (x1 - x0) * 0.1, x0 + (x1 - x0) * 0.9, panel: 1), Fx.Ctx())!.Chart;
            var k = spec.Series.Count;
            var json = o.Str("spec", "");
            Assert.Contains("\"sketch\"", json);
            var parsed = FigureSpec.Parse(json, Lang.En);
            var memo = parsed.Series[k].Sketch!;
            Assert.NotNull(memo);
            Assert.Equal(parsed.ToJson(), json); // stable round trip
            Assert.Equal(memo.Raw, FigureSpec.Parse(parsed.ToJson(), Lang.En).Series[k].Sketch!.Raw);

            // Switch to straight segments: the points change, the trail stays, the JSON still round-trips.
            var seg = ChartSketchInput.WithShape(o, k, m => m with { Shape = CurveShape.Segments });
            var sp = FigureSpec.Parse(seg.Str("spec", ""), Lang.En);
            Assert.Equal(CurveShape.Segments, sp.Series[k].Sketch!.Shape);
            Assert.Equal(memo.Raw, sp.Series[k].Sketch!.Raw);
            Assert.True(sp.Series[k].X.Count < parsed.Series[k].X.Count);
            Assert.Contains(r.PartActions(seg, "series." + k, Fx.Ctx()), a => a.Choice?.Key == CurveShapes.ShapeAction);
            Assert.DoesNotContain(r.PartActions(seg, "series.0", Fx.Ctx()), a => a.Choice?.Key == CurveShapes.ShapeAction);
            Fx.AssertInsideBox(Fx.Render(r, seg), seg.Size, "segments figure");

            // Clean slides: randomised values drop the kept trail (it carries the original values).
            var clean = r.Type(o.Type)!.Sanitize(o, new SanitizeOptions { Mode = SanitizeMode.Randomised, Seed = 1 }, new Random(1));
            Assert.DoesNotContain("\"sketch\"", clean.Str("spec", ""));

            // A memo without a trail is dropped when reading.
            Assert.Null(FigureSpec.Parse("{\"series\":[{\"x\":[0,1],\"y\":[0,1],\"sketch\":{\"shape\":\"raw\"}}]}", Lang.En).Series[0].Sketch);
        }

        [Fact]
        public void LogAxesKeepTheTrailInDataValues()
        {
            var r = Fx.Reg();
            var chart = Fx.Item(r, "chart.line", new Vec2(120, 90)).WithParam("data", "1;10\n10;100\n100;1000\n1000;10000").WithParam("xscale", "log").WithParam("yscale", "log");
            var o = ChartSketchInput.Add(r, chart, Trail(r, chart, x => 30 * x, 2, 500, jitter: 0.2), Fx.Ctx())!.Chart;
            var memo = ChartSketchInput.Memo(o, 1)!;
            Assert.True(memo.XLog && memo.YLog);
            Assert.All(memo.Raw, p => Assert.True(p.X > 1 && p.X < 1000 && p.Y > 30 && p.Y < 30000, p.ToString()));
            foreach (var shape in CurveShapes.Order)
            {
                var pts = CurveShapes.Points(memo with { Shape = shape });
                Assert.True(pts.Count >= 2, shape.ToString());
                // y ≈ 30 x within a factor of 1.5 (log space).
                Assert.All(pts, p => Assert.InRange(Math.Log10(p.Y / (30 * p.X)), -0.18, 0.18));
            }
        }
    }
}
