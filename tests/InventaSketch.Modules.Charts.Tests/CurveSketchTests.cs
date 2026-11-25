using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using InventaSketch.Core;
using InventaSketch.Modules.Charts;
using Xunit;
using Xunit.Abstractions;

namespace InventaSketch.Modules.Charts.Tests
{
    /// <summary>Simulates a hand-drawn pointer trail of an ideal curve (seeded, reproducible).</summary>
    internal static class HandDraw
    {
        /// <param name="noise">Gaussian tremor per point, relative to the frame.</param>
        /// <param name="wobble">Amplitude of a slow drift along the stroke, relative to the frame.</param>
        public static List<Vec2> Draw(IReadOnlyList<Vec2> ideal, Rect frame, int seed, double noise = 0.005, double wobble = 0.006, bool hook = true, bool reverse = false)
        {
            var rnd = new Random(seed);
            double Gauss() => Math.Sqrt(-2 * Math.Log(1 - rnd.NextDouble())) * Math.Cos(2 * Math.PI * rnd.NextDouble());
            var n = ideal.Select(p => new Vec2((p.X - frame.X) / frame.W, (p.Y - frame.Y) / frame.H)).ToList();
            if (reverse) n.Reverse();
            var len = SketchFit.PathLength(n);
            var dense = SketchFit.Resample(n, (int)(len / 0.002) + 2).ToList();
            // Rounded corners: moving average over ±0.01 of the stroke.
            var round = new List<Vec2>();
            for (var i = 0; i < dense.Count; i++)
            {
                var w = Math.Min(5, Math.Min(i, dense.Count - 1 - i));
                var s = Vec2.Zero;
                for (var k = -w; k <= w; k++) s += dense[i + k];
                round.Add(s / (2 * w + 1));
            }
            // Slow drift: three random low-frequency sines in x and y.
            var waves = Enumerable.Range(0, 6).Select(_ => (F: 0.5 + rnd.NextDouble() * 3, P: rnd.NextDouble() * 2 * Math.PI)).ToArray();
            Vec2 Drift(double t) => new(
                wobble / 3 * (Math.Sin(2 * Math.PI * waves[0].F * t + waves[0].P) + Math.Sin(2 * Math.PI * waves[1].F * t + waves[1].P) + Math.Sin(2 * Math.PI * waves[2].F * t + waves[2].P)),
                wobble / 3 * (Math.Sin(2 * Math.PI * waves[3].F * t + waves[3].P) + Math.Sin(2 * Math.PI * waves[4].F * t + waves[4].P) + Math.Sin(2 * Math.PI * waves[5].F * t + waves[5].P)));
            // Uneven sampling: 1 … 8 dense steps (0.002 … 0.016) between pointer events, plus tremor.
            var trail = new List<Vec2>();
            for (var i = 0; i < round.Count; i += 1 + rnd.Next(8))
                trail.Add(round[i] + Drift((double)i / round.Count) + new Vec2(Gauss(), Gauss()) * noise);
            trail.Add(round[round.Count - 1] + Drift(1) + new Vec2(Gauss(), Gauss()) * noise);
            if (hook)
            {
                // A small hook when lifting the pen.
                var a = rnd.NextDouble() * 2 * Math.PI;
                var last = trail[trail.Count - 1];
                for (var k = 1; k <= 3; k++) trail.Add(last + Vec2.Polar(0.005 * k, a));
            }
            return trail.Select(p => new Vec2(frame.X + frame.W * p.X, frame.Y + frame.H * p.Y)).ToList();
        }
    }

    public class CurveSketchTests
    {
        private readonly ITestOutputHelper _out;
        public CurveSketchTests(ITestOutputHelper output) { _out = output; }

        /// <summary>Ideal test curves in a 0 … 100 × 0 … 100 frame, drawn from x = 5 to x = 95.</summary>
        public static readonly (CurveKind Kind, string Spec)[] Cases =
        {
            (CurveKind.Constant, "constant(y=40)"),
            (CurveKind.Ramp, "ramp(x0=0, y0=10, slope=0.7)"),
            (CurveKind.Step, "step(x0=40, y0=20, y1=70)"),
            (CurveKind.Pulse, "pulse(start=35, width=25, low=20, high=75)"),
            (CurveKind.Sawtooth, "sawtooth(period=30, low=15, high=80, phase=5)"),
            (CurveKind.Triangle, "triangle(period=40, low=15, high=80, phase=5)"),
            (CurveKind.Sine, "sine(amplitude=30, period=40, phase=3, offset=50)"),
            (CurveKind.Pt1, "pt1(y0=15, yinf=80, tau=18, t0=5)"),
            (CurveKind.Decay, "decay(y0=85, tau=15, t0=5)"),
            (CurveKind.Ellipse, "ellipse(cx=50, cy=50, rx=30, ry=20)"),
        };

        /// <summary>Axis scalings x' = ax + bx·x, y' = ay + by·y (decay keeps ay = 0 so its asymptote stays at zero).</summary>
        public static readonly (double Ax, double Bx, double Ay, double By)[] Scales =
        {
            (0, 1, 0, 1),
            (1000, 0.01, -250, 50),
            (-3, 1e-3, 0, 1e4),
        };

        public static IEnumerable<object[]> KindCases() => Cases.Select((c, i) => new object[] { c.Kind, i });

        internal static (Rect Frame, CurveSpec Spec, IReadOnlyList<Vec2> Ideal) Scaled(int caseIndex, int scaleIndex)
        {
            var (kind, text) = Cases[caseIndex];
            var (ax, bx, ay, by) = Scales[scaleIndex];
            if (kind == CurveKind.Decay) ay = 0;
            var spec = CurveFunctions.MapAffine(CurveFunctions.Parse(text), ax, bx, ay, by);
            var frame = new Rect(ax, ay, 100 * bx, 100 * by);
            return (frame, spec, spec.Sample(ax + 5 * bx, ax + 95 * bx));
        }

        /// <summary>Symmetric RMS distance of two curves relative to the frame (densified, each direction).</summary>
        private static double CurveDistance(IReadOnlyList<Vec2> a, IReadOnlyList<Vec2> b, Rect f)
        {
            List<Vec2> N(IReadOnlyList<Vec2> pts)
            {
                var n = pts.Select(p => new Vec2((p.X - f.X) / f.W, (p.Y - f.Y) / f.H)).ToList();
                return SketchFit.Resample(n, (int)(SketchFit.PathLength(n) / 0.004) + 2).ToList();
            }
            double OneWay(List<Vec2> from, List<Vec2> to) => Math.Sqrt(from.Average(p =>
            {
                var best = double.PositiveInfinity;
                for (var i = 0; i + 1 < to.Count; i++) best = Math.Min(best, p.DistanceTo(Geo.ClosestOnSegment(p, to[i], to[i + 1]).Point));
                return best * best;
            }));
            var na = N(a);
            var nb = N(b);
            return Math.Max(OneWay(na, nb), OneWay(nb, na));
        }

        /// <summary>
        /// Phase difference measured at the centre of the frame, as a share of the period: where the fitted and the ideal wave
        /// are within their cycle there (a phase compared at x = 0 would also scale the period error by the distance to x = 0).
        /// </summary>
        private static double PhaseError(CurveSpec fit, CurveSpec ideal, Rect f)
        {
            var centre = f.X + f.W / 2;
            double Cycle(CurveSpec s) { var t = (centre - s["phase"]) / s["period"]; return t - Math.Floor(t); }
            var d = Math.Abs(Cycle(fit) - Cycle(ideal));
            return Math.Min(d, 1 - d);
        }

        /// <summary>Checks the fitted parameters against the ideal ones; returns null if all are within tolerance, else a reason.</summary>
        internal static string? CheckParameters(CurveSpec fit, CurveSpec ideal, Rect f)
        {
            var errors = new List<string>();
            void X(string name, double tol) { var d = Math.Abs(fit[name] - ideal[name]) / f.W; if (d > tol) errors.Add($"{name} off by {d:P1} of W"); }
            void Y(string name, double tol) { var d = Math.Abs(fit[name] - ideal[name]) / f.H; if (d > tol) errors.Add($"{name} off by {d:P1} of H"); }
            void Rel(string name, double tol) { var d = Math.Abs(fit[name] / ideal[name] - 1); if (d > tol) errors.Add($"{name} off by {d:P1}"); }
            switch (ideal.Kind)
            {
                case CurveKind.Constant: Y("y", 0.02); break;
                case CurveKind.Ramp:
                {
                    var mid = f.X + f.W / 2;
                    if (Math.Abs(fit.Evaluate(mid) - ideal.Evaluate(mid)) / f.H > 0.02) errors.Add("ramp value at the centre");
                    if (Math.Abs((fit["slope"] - ideal["slope"]) * f.W / f.H) > 0.05) errors.Add("ramp slope");
                    break;
                }
                case CurveKind.Step: X("x0", 0.02); Y("y0", 0.03); Y("y1", 0.03); break;
                case CurveKind.Pulse: X("start", 0.02); X("width", 0.03); Y("low", 0.03); Y("high", 0.03); break;
                case CurveKind.Sawtooth:
                case CurveKind.Triangle:
                    Rel("period", 0.04); Y("low", 0.04); Y("high", 0.04);
                    if (PhaseError(fit, ideal, f) > 0.04) errors.Add("phase");
                    break;
                case CurveKind.Sine:
                    Rel("period", 0.04); Y("amplitude", 0.03); Y("offset", 0.03);
                    if (PhaseError(fit, ideal, f) > 0.03) errors.Add("phase");
                    break;
                case CurveKind.Pt1: Y("y0", 0.04); Y("yinf", 0.05); Rel("tau", 0.25); X("t0", 0.05); break;
                case CurveKind.Decay: Y("y0", 0.05); Rel("tau", 0.2); break;
                case CurveKind.Ellipse: X("cx", 0.02); Y("cy", 0.02); X("rx", 0.03); Y("ry", 0.03); break;
            }
            return errors.Count == 0 ? null : string.Join("; ", errors);
        }

        private static string Describe(SketchFitResult r) =>
            $"got {(r.Kind?.ToString() ?? "smoothed")} ({r.Spec?.Format()}, residual {r.Residual:0.0000}); ranking: " +
            string.Join(", ", r.Ranking.Select(c => $"{c.Kind} {c.Residual:0.0000}"));

        [Theory]
        [MemberData(nameof(KindCases))]
        public void EachKindIsRecognisedAtSeveralScales(CurveKind kind, int caseIndex)
        {
            for (var scale = 0; scale < Scales.Length; scale++)
                for (var seed = 1; seed <= 2; seed++)
                {
                    var (frame, ideal, idealPts) = Scaled(caseIndex, scale);
                    var trail = HandDraw.Draw(idealPts, frame, seed * 101 + caseIndex, reverse: seed == 2 && kind != CurveKind.Ellipse);
                    var result = SketchFit.Fit(trail, new SketchFitOptions { Frame = frame });
                    var where = $"{kind}, scale {scale}, seed {seed}: ";
                    Assert.True(result.Kind == kind, where + Describe(result));
                    Assert.True(result.Confidence > 0, where + "confidence");
                    var problem = CheckParameters(result.Spec!, ideal, frame);
                    Assert.True(problem is null, where + problem + " – " + Describe(result));
                    var distance = CurveDistance(kind == CurveKind.Ellipse ? result.Spec!.Sample(0, 0) : result.Spec!.Sample(frame.X + 5 * frame.W / 100, frame.X + 95 * frame.W / 100), idealPts, frame);
                    Assert.True(distance < 0.02, where + $"curve distance {distance:0.0000}");
                    Assert.Equal(kind == CurveKind.Ellipse, result.Closed);
                    Assert.Equal(kind, result.Ranking[0].Kind);
                }
        }

        [Fact]
        public void TheTaskExamplePt1IsRecognised()
        {
            var frame = new Rect(0, 0, 60, 200);
            var ideal = CurveFunctions.Parse("pt1(y0=20, yinf=160, tau=12)");
            var trail = HandDraw.Draw(ideal.Sample(0, 60), frame, 42);
            var r = SketchFit.Fit(trail, new SketchFitOptions { Frame = frame });
            Assert.Equal(CurveKind.Pt1, r.Kind);
            Assert.InRange(r.Spec!["yinf"], 150, 170);
            Assert.InRange(r.Spec["y0"], 12, 28);
            Assert.InRange(r.Spec["tau"], 9, 15);
            Assert.Equal(0, r.XMin, 0);
            Assert.Equal(60, r.XMax, 0);
            Assert.Equal(r.Spec.Sample(r.XMin, r.XMax), r.Curve);
        }

        [Fact]
        public void RecognitionAccuracyTable()
        {
            const int seeds = 4;
            var table = new StringBuilder();
            table.AppendLine("| Kind | Trials | Recognised | Parameters within tolerance | Mean residual | Max residual |");
            table.AppendLine("|---|---|---|---|---|---|");
            var total = 0;
            var good = 0;
            for (var c = 0; c < Cases.Length; c++)
            {
                int n = 0, kindOk = 0, paramOk = 0;
                double residual = 0, maxResidual = 0;
                for (var scale = 0; scale < Scales.Length; scale++)
                    for (var seed = 0; seed < seeds; seed++)
                    {
                        var (frame, ideal, idealPts) = Scaled(c, scale);
                        // Stronger distortion than in the per-kind test: more tremor and drift.
                        var trail = HandDraw.Draw(idealPts, frame, 7919 * seed + 31 * c + scale, noise: 0.008, wobble: 0.01, reverse: seed % 2 == 1 && Cases[c].Kind != CurveKind.Ellipse);
                        var r = SketchFit.Fit(trail, new SketchFitOptions { Frame = frame });
                        n++;
                        residual += r.Residual;
                        maxResidual = Math.Max(maxResidual, r.Residual);
                        if (r.Kind != Cases[c].Kind) continue;
                        kindOk++;
                        if (CheckParameters(r.Spec!, ideal, frame) is null) paramOk++;
                    }
                total += n;
                good += kindOk;
                table.AppendLine($"| {Cases[c].Kind} | {n} | {kindOk} ({100.0 * kindOk / n:0}%) | {paramOk} ({100.0 * paramOk / n:0}%) | {residual / n:0.0000} | {maxResidual:0.0000} |");
                Assert.True(kindOk >= 0.9 * n, $"{Cases[c].Kind}: only {kindOk} of {n} recognised\n{table}");
            }
            _out.WriteLine(table.ToString());
            Assert.True(good >= 0.95 * total, table.ToString());
        }

        [Fact]
        public void RandomTrailsFallBackToSmoothing()
        {
            var frame = new Rect(0, 0, 100, 100);
            for (var seed = 0; seed < 8; seed++)
            {
                var rnd = new Random(seed);
                // Jagged random walk from left to right (a gentle walk can honestly look like a wave or a ramp).
                var walk = new List<Vec2>();
                double y = 50;
                for (var x = 5.0; x <= 95; x += 1)
                {
                    y = Geo.Clamp(y + (rnd.NextDouble() - 0.5) * 20, 5, 95);
                    walk.Add(new Vec2(x, y));
                }
                var r = SketchFit.Fit(walk, new SketchFitOptions { Frame = frame });
                Assert.True(r.Kind is null, "random walk " + seed + ": " + Describe(r));
                Assert.Null(r.Spec);
                Assert.Equal(0, r.Confidence);
                Assert.NotEmpty(r.Ranking);
                Assert.True(r.Residual > SketchFit.AcceptResidual, Describe(r));
                Assert.Same(r.Smoothed, r.Curve);
                Assert.True(r.Smoothed.Count >= 2);

                // Scribble between random points.
                var scribble = Enumerable.Range(0, 12).Select(_ => new Vec2(5 + rnd.NextDouble() * 90, 5 + rnd.NextDouble() * 90)).ToList();
                var s = SketchFit.Fit(SketchFit.Resample(scribble, 300), new SketchFitOptions { Frame = frame });
                Assert.True(s.Kind is null, "scribble " + seed + ": " + Describe(s));
            }
        }

        [Fact]
        public void AmbiguousCompositeFallsBackToSmoothing()
        {
            // Flat, then a step up, then a fast wiggle, then a ramp down: no single kind explains it.
            var frame = new Rect(0, 0, 100, 100);
            var pts = new List<Vec2> { new(5, 20), new(30, 20), new(30, 70) };
            for (var x = 30.0; x <= 60; x += 0.5) pts.Add(new Vec2(x, 70 + 8 * Math.Sin((x - 30) * 0.8)));
            pts.Add(new Vec2(95, 10));
            for (var seed = 0; seed < 4; seed++)
            {
                var trail = HandDraw.Draw(pts, frame, seed);
                var r = SketchFit.Fit(trail, new SketchFitOptions { Frame = frame });
                Assert.True(r.Kind is null, Describe(r));
                // The smoothed curve follows the trail closely.
                Assert.True(CurveDistance(r.Smoothed, trail, frame) < 0.02);
            }
        }

        [Fact]
        public void OptionsRestrictTheKindsAndTheAcceptance()
        {
            var (frame, _, idealPts) = Scaled(6, 0); // sine
            var trail = HandDraw.Draw(idealPts, frame, 5);
            var onlyLines = SketchFit.Fit(trail, new SketchFitOptions { Frame = frame, Kinds = new[] { CurveKind.Constant, CurveKind.Ramp } });
            Assert.Null(onlyLines.Kind);
            Assert.All(onlyLines.Ranking, c => Assert.Contains(c.Kind, new[] { CurveKind.Constant, CurveKind.Ramp }));
            var strict = SketchFit.Fit(trail, new SketchFitOptions { Frame = frame, AcceptResidual = 1e-4 });
            Assert.Null(strict.Kind);
            Assert.Equal(CurveKind.Sine, strict.Ranking[0].Kind);
        }

        [Fact]
        public void WithoutAFrameTheTrailBoxIsUsed()
        {
            var spec = CurveFunctions.Parse("step(x0=4, y0=1, y1=3)");
            var trail = HandDraw.Draw(spec.Sample(0, 10), new Rect(0, 0, 10, 4), 9);
            var r = SketchFit.Fit(trail);
            Assert.Equal(CurveKind.Step, r.Kind);
            Assert.InRange(r.Spec!["x0"], 3.8, 4.2);
        }

        [Fact]
        public void DegenerateTrailsDoNotThrow()
        {
            Assert.Null(SketchFit.Fit(Array.Empty<Vec2>()).Kind);
            Assert.Null(SketchFit.Fit(new[] { new Vec2(1, 1) }).Kind);
            Assert.Null(SketchFit.Fit(new[] { new Vec2(1, 1), new Vec2(1, 1), new Vec2(double.NaN, 2) }).Kind);
            // A perfectly flat line without a frame is a constant.
            var flat = SketchFit.Fit(new[] { new Vec2(0, 5), new Vec2(3, 5), new Vec2(10, 5) });
            Assert.Equal(CurveKind.Constant, flat.Kind);
            Assert.Equal(5, flat.Spec!["y"], 6);
            // A vertical stroke has no y(x): smoothing only.
            var vertical = SketchFit.Fit(new[] { new Vec2(2, 0), new Vec2(2, 5), new Vec2(2, 10) }, new SketchFitOptions { Frame = new Rect(0, 0, 10, 10) });
            Assert.Null(vertical.Kind);
            Assert.NotEmpty(vertical.Smoothed);
        }

        [Fact]
        public void ResampleSimplifyAndSplines()
        {
            var poly = new[] { new Vec2(0, 0), new Vec2(1, 0), new Vec2(1, 3) };
            var r = SketchFit.Resample(poly, 9);
            Assert.Equal(9, r.Count);
            Assert.Equal(new Vec2(0, 0), r[0]);
            Assert.Equal(new Vec2(1, 3), r[8]);
            for (var i = 1; i < r.Count; i++) Assert.Equal(0.5, SketchFit.PathLength(new[] { r[i - 1], r[i] }), 9);

            var noisy = Enumerable.Range(0, 101).Select(i => new Vec2(i, i % 2 == 0 ? 0.001 : -0.001)).ToList();
            Assert.Equal(2, SketchFit.Simplify(noisy, 0.01).Count);
            Assert.Equal(3, SketchFit.Simplify(new[] { new Vec2(0, 0), new Vec2(5, 5), new Vec2(10, 0) }, 0.1).Count);

            var pts = new[] { new Vec2(0, 0), new Vec2(1, 2), new Vec2(3, 2), new Vec2(10, 0) };
            foreach (var alpha in new[] { 0.0, 0.5 })
            {
                var s = SketchFit.CatmullRom(pts, 8, alpha);
                Assert.Equal(1 + 3 * 8, s.Count);
                for (var i = 0; i < pts.Length; i++)
                {
                    Assert.Equal(pts[i].X, s[i * 8].X, 9);
                    Assert.Equal(pts[i].Y, s[i * 8].Y, 9);
                }
            }
            // Uniform Catmull–Rom is the Slidemax formula: midpoint of the middle segment.
            var mid = SketchFit.CatmullRom(pts, 2, 0)[3];
            Assert.Equal(0.5 * (2 * 1 + (-0 + 3) * 0.5 + (2 * 0 - 5 * 1 + 4 * 3 - 10) * 0.25 + (-0 + 3 * 1 - 3 * 3 + 10) * 0.125), mid.X, 12);
            var closed = SketchFit.CatmullRom(new[] { new Vec2(0, 0), new Vec2(1, 0), new Vec2(1, 1), new Vec2(0, 1) }, 4, 0.5, closed: true);
            Assert.Equal(closed[0], closed[closed.Count - 1]);
            Assert.Equal(1 + 4 * 4, closed.Count);
        }

        [Fact]
        public void SmoothedTrailFollowsAClosedLoop()
        {
            var frame = new Rect(0, 0, 100, 100);
            // A closed blob that is not an ellipse (a rounded square with a dent) falls back to a closed smooth curve.
            var blob = new List<Vec2>();
            for (var i = 0; i <= 200; i++)
            {
                var a = 2 * Math.PI * i / 200;
                var rad = 30 + 10 * Math.Cos(3 * a);
                blob.Add(new Vec2(50 + rad * Math.Cos(a), 50 + rad * Math.Sin(a)));
            }
            var trail = HandDraw.Draw(blob, frame, 1, hook: false);
            var r = SketchFit.Fit(trail, new SketchFitOptions { Frame = frame });
            Assert.True(r.Closed);
            Assert.True(r.Kind is null, Describe(r));
            Assert.Equal(r.Smoothed[0], r.Smoothed[r.Smoothed.Count - 1]);
            Assert.True(CurveDistance(r.Smoothed, blob, frame) < 0.02);
        }
    }
}
