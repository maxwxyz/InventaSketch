using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Charts
{
    /// <summary>Options of <see cref="SketchFit.Fit"/>.</summary>
    public sealed record SketchFitOptions
    {
        /// <summary>
        /// Data-space extent of the plot area (X/Y = smallest x/y value, W/H = ranges). All tolerances are relative to it, so they
        /// behave like screen distances. Without a frame the bounding box of the trail is used, which makes a flat trail look
        /// as tall as the plot – pass the frame whenever it is known.
        /// </summary>
        public Rect? Frame { get; init; }
        /// <summary>Largest residual (relative to the frame) of an accepted idealised curve.</summary>
        public double AcceptResidual { get; init; } = SketchFit.AcceptResidual;
        /// <summary>Kinds to consider (default <see cref="SketchFit.RecognisableKinds"/>).</summary>
        public IReadOnlyCollection<CurveKind>? Kinds { get; init; }
    }

    /// <summary>A fitted kind: spec in data coordinates, residual relative to the frame and number of free parameters.</summary>
    public sealed record SketchCandidate(CurveKind Kind, CurveSpec Spec, double Residual, int ParameterCount);

    /// <summary>Result of <see cref="SketchFit.Fit"/>.</summary>
    public sealed record SketchFitResult
    {
        /// <summary>The recognised kind; null if nothing fits well – then use <see cref="Smoothed"/>.</summary>
        public CurveKind? Kind { get; init; }
        /// <summary>The fitted function generator in data coordinates (null if not recognised).</summary>
        public CurveSpec? Spec { get; init; }
        /// <summary>Robust RMS distance between trail and curve relative to the frame (of the chosen kind, or of the best one).</summary>
        public double Residual { get; init; } = double.NaN;
        /// <summary>0 … 1: 1 − residual / accepted residual (0 if not recognised).</summary>
        public double Confidence { get; init; }
        /// <summary>All fitted kinds, the chosen one first, then by residual.</summary>
        public IReadOnlyList<SketchCandidate> Ranking { get; init; } = Array.Empty<SketchCandidate>();
        /// <summary>The trail simplified with Ramer–Douglas–Peucker (data coordinates).</summary>
        public IReadOnlyList<Vec2> Simplified { get; init; } = Array.Empty<Vec2>();
        /// <summary>The smoothed trail (centripetal Catmull–Rom through the simplified, denoised trail; data coordinates).</summary>
        public IReadOnlyList<Vec2> Smoothed { get; init; } = Array.Empty<Vec2>();
        /// <summary>True if the trail was recognised as a closed loop.</summary>
        public bool Closed { get; init; }
        /// <summary>x range of the trail (where <see cref="Spec"/> should be sampled).</summary>
        public double XMin { get; init; }
        public double XMax { get; init; }

        public bool Recognised => Kind is not null;

        /// <summary>The curve to draw: the sampled spec over the trail's x range, otherwise the smoothed trail.</summary>
        public IReadOnlyList<Vec2> Curve => Spec is null ? Smoothed : Spec.Sample(XMin, XMax);
    }

    /// <summary>
    /// Sketch recognition (charts v2, stage 3): turns a hand-drawn pointer trail into an idealised function generator or, if
    /// none fits, a smooth curve. Steps: normalise to the frame, resample by arc length, simplify (Ramer–Douglas–Peucker),
    /// detect a closed loop, fit every kind (grid search with linear least squares on vertical residuals for a start, then
    /// Nelder–Mead on a robust symmetric trail ↔ curve distance), pick the simplest kind among the nearly best and accept it if
    /// its residual is small enough. Pure and host-free.
    /// </summary>
    public static class SketchFit
    {
        // ---------------------------------------------------------------- thresholds (relative to the frame, 1 = plot size)

        /// <summary>Arc-length spacing of the resampled trail.</summary>
        public const double ResampleSpacing = 0.008;
        /// <summary>Bounds of the number of resampled points.</summary>
        public const int MinResample = 96, MaxResample = 384;
        /// <summary>Ramer–Douglas–Peucker tolerance of <see cref="SketchFitResult.Simplified"/>.</summary>
        public const double SimplifyEpsilon = 0.01;
        /// <summary>Ramer–Douglas–Peucker tolerance before the smoothing spline (keeps more detail).</summary>
        public const double SmoothSimplifyEpsilon = 0.004;
        /// <summary>Half-width (in resampled points) of the moving average that removes jitter before smoothing.</summary>
        public const int SmoothWindow = 3;
        /// <summary>Huber scale: distances above it count linearly, so a single overshoot or hook does not dominate.</summary>
        public const double HuberScale = 0.04;
        /// <summary>
        /// Largest residual of an accepted idealised curve: 2 % of the plot (hand tremor is about 0.5 … 1 %; steep scribbles reach
        /// about 3 % against a smooth wave because the distance is measured perpendicular to the curve).
        /// </summary>
        public const double AcceptResidual = 0.02;
        /// <summary>
        /// Simplicity rule: a kind with fewer parameters wins if its residual is at most this factor times the best residual plus
        /// <see cref="SimplicityMargin"/>.
        /// </summary>
        public const double SimplicityRatio = 1.25;
        public const double SimplicityMargin = 0.003;
        /// <summary>Closed loop: gap between start and end at most this share of the larger side of the trail's bounding box …</summary>
        public const double ClosedGapRatio = 0.2;
        /// <summary>… and at least this share of the horizontal travel runs backwards (right to left or vice versa).</summary>
        public const double ClosedBacktrackShare = 0.3;
        /// <summary>Shortest trail (relative path length) that is fitted at all.</summary>
        public const double MinTrailLength = 0.01;
        /// <summary>Spacing of the curve points checked against the trail (curve → trail direction of the residual).</summary>
        public const double DensifyStep = 0.01;
        /// <summary>Half-width (resampled points) of the chord that measures a point's horizontal travel.</summary>
        public const int WeightWindow = 3;
        /// <summary>Points of the trail subsample used while refining.</summary>
        public const int CoarsePoints = 128;
        /// <summary>A start whose residual exceeds this is not refined (it cannot become acceptable; it stays in the ranking).</summary>
        public const double SkipRefineResidual = 0.15;
        /// <summary>Function evaluations of one Nelder–Mead refinement.</summary>
        public const int RefineEvaluations = 240;

        /// <summary>Shortest period (share of the trail's x range) a periodic fit may take (24 periods across the trail).</summary>
        public const double MinPeriodShare = 1.0 / 24;
        /// <summary>Largest level or amplitude (in frame sizes) a fit may take.</summary>
        public const double MaxLevel = 5;
        /// <summary>Longest candidate curve (relative path length) that is evaluated; longer ones are implausible.</summary>
        public const double MaxCurveLength = 60;

        /// <summary>Kinds the recognition considers.</summary>
        public static IReadOnlyList<CurveKind> RecognisableKinds { get; } = new[]
        {
            CurveKind.Constant, CurveKind.Ramp, CurveKind.Step, CurveKind.Pulse, CurveKind.Sawtooth, CurveKind.Triangle,
            CurveKind.Sine, CurveKind.Pt1, CurveKind.Decay, CurveKind.Ellipse,
        };

        private static readonly Dictionary<CurveKind, int> ParameterCounts = new()
        {
            [CurveKind.Constant] = 1, [CurveKind.Ramp] = 2, [CurveKind.Step] = 3, [CurveKind.Decay] = 3, [CurveKind.Pulse] = 4,
            [CurveKind.Sawtooth] = 4, [CurveKind.Triangle] = 4, [CurveKind.Sine] = 4, [CurveKind.Pt1] = 4, [CurveKind.Ellipse] = 4,
        };

        // ---------------------------------------------------------------- public entry

        /// <summary>Recognises a hand-drawn trail (data coordinates, noisy, unevenly spaced, in any direction).</summary>
        public static SketchFitResult Fit(IReadOnlyList<Vec2> trail, SketchFitOptions? options = null)
        {
            options ??= new SketchFitOptions();
            var raw = trail.Where(p => Fmt.Finite(p.X) && Fmt.Finite(p.Y)).ToList();
            var frame = options.Frame ?? Rect.FromPoints(raw);
            double fx = frame.X, fy = frame.Y, fw = frame.W > 1e-300 ? frame.W : 1, fh = frame.H > 1e-300 ? frame.H : 1;
            if (raw.Count > 0 && options.Frame is null)
            {
                // A degenerate side of the bounding box (perfectly flat or vertical trail) gets size 1, centred on the trail.
                if (frame.W <= 1e-300) fx -= 0.5;
                if (frame.H <= 1e-300) fy -= 0.5;
            }
            Vec2 ToData(Vec2 n) => new(fx + fw * n.X, fy + fh * n.Y);
            var norm = new List<Vec2>();
            foreach (var p in raw)
            {
                var q = new Vec2((p.X - fx) / fw, (p.Y - fy) / fh);
                if (norm.Count == 0 || norm[norm.Count - 1].DistanceTo(q) > 1e-9) norm.Add(q);
            }
            var length = PathLength(norm);
            double xMin = raw.Count > 0 ? raw.Min(p => p.X) : 0, xMax = raw.Count > 0 ? raw.Max(p => p.X) : 0;
            if (norm.Count < 2 || length < MinTrailLength)
                return new SketchFitResult { Simplified = raw, Smoothed = raw, XMin = xMin, XMax = xMax };

            var count = (int)Geo.Clamp(Math.Ceiling(length / ResampleSpacing) + 1, MinResample, MaxResample);
            var r = Resample(norm, count);
            var closed = IsClosed(r);
            var simplified = Simplify(r, SimplifyEpsilon).Select(ToData).ToList();
            var smoothed = Smooth(r, closed).Select(ToData).ToList();
            var kinds = new HashSet<CurveKind>(options.Kinds ?? RecognisableKinds);

            var candidates = new List<(CurveKind Kind, CurveSpec Norm, double Residual)>();
            var ctx = new Context(r, fy, fh);
            if (closed)
            {
                if (kinds.Contains(CurveKind.Ellipse) && FitEllipse(ctx) is { } e) candidates.Add(e);
            }
            else if (ctx.URange > 1e-6)
            {
                void Try(CurveKind kind, Func<Context, (CurveKind, CurveSpec, double)?> fit)
                {
                    if (kinds.Contains(kind) && fit(ctx) is { } c) candidates.Add(c);
                }
                Try(CurveKind.Constant, FitConstant);
                Try(CurveKind.Ramp, FitRamp);
                Try(CurveKind.Step, FitStep);
                Try(CurveKind.Pulse, FitPulse);
                Try(CurveKind.Sine, c => FitPeriodic(c, CurveKind.Sine));
                Try(CurveKind.Sawtooth, c => FitPeriodic(c, CurveKind.Sawtooth));
                Try(CurveKind.Triangle, c => FitPeriodic(c, CurveKind.Triangle));
                Try(CurveKind.Pt1, c => FitExponential(c, false));
                Try(CurveKind.Decay, c => FitExponential(c, true));
            }

            var ranking = candidates
                .Select(c => new SketchCandidate(c.Kind, ToDataSpec(c.Kind, c.Norm, fx, fw, fy, fh), c.Residual, ParameterCounts[c.Kind]))
                .OrderBy(c => c.Residual).ToList();
            var result = new SketchFitResult
            {
                Simplified = simplified, Smoothed = smoothed, Closed = closed, XMin = xMin, XMax = xMax,
                Residual = ranking.Count > 0 ? ranking[0].Residual : double.NaN,
            };
            if (ranking.Count == 0) return result;
            var best = ranking[0].Residual;
            // The simplest kind among the nearly best ones that is also acceptable (the best one is, if any is).
            var chosen = ranking
                .Where(c => c.Residual <= best * SimplicityRatio + SimplicityMargin && c.Residual <= options.AcceptResidual)
                .OrderBy(c => c.ParameterCount).ThenBy(c => c.Residual).FirstOrDefault();
            if (chosen is null) return result with { Ranking = ranking };
            var ordered = new[] { chosen }.Concat(ranking.Where(c => !ReferenceEquals(c, chosen))).ToList();
            return result with
            {
                Kind = chosen.Kind,
                Spec = chosen.Spec,
                Residual = chosen.Residual,
                Confidence = Geo.Clamp(1 - chosen.Residual / options.AcceptResidual, 0, 1),
                Ranking = ordered,
            };
        }

        /// <summary>Converts a spec fitted in frame-normalised coordinates into data coordinates (decay is fitted as PT1 towards y = 0).</summary>
        private static CurveSpec ToDataSpec(CurveKind kind, CurveSpec norm, double fx, double fw, double fy, double fh)
        {
            if (kind == CurveKind.Decay)
                return new CurveSpec(CurveKind.Decay, new[] { fy + fh * norm.Values[0], fw * norm.Values[2], fx + fw * norm.Values[3] });
            var s = CurveFunctions.MapAffine(norm, fx, fw, fy, fh);
            var v = s.Values.ToArray();
            switch (kind)
            {
                case CurveKind.Sine:
                    v[2] = Mod(v[2], v[1]);
                    break;
                case CurveKind.Sawtooth:
                case CurveKind.Triangle:
                    v[3] = Mod(v[3], v[0]);
                    break;
            }
            return new CurveSpec(kind, v);
        }

        private static double Mod(double a, double m) => m > 0 ? a - Math.Floor(a / m) * m : a;

        // ---------------------------------------------------------------- geometry helpers (public: useful for hosts)

        /// <summary>Total length of a polyline.</summary>
        public static double PathLength(IReadOnlyList<Vec2> pts)
        {
            double s = 0;
            for (var i = 1; i < pts.Count; i++) s += pts[i].DistanceTo(pts[i - 1]);
            return s;
        }

        /// <summary>Resamples a polyline to <paramref name="count"/> points equally spaced by arc length (first and last kept).</summary>
        public static IReadOnlyList<Vec2> Resample(IReadOnlyList<Vec2> pts, int count)
        {
            if (pts.Count < 2 || count < 2) return pts.ToList();
            var total = PathLength(pts);
            if (total <= 0) return Enumerable.Repeat(pts[0], count).ToList();
            var result = new List<Vec2>(count) { pts[0] };
            var step = total / (count - 1);
            double travelled = 0, next = step;
            for (var i = 1; i < pts.Count && result.Count < count - 1; i++)
            {
                var seg = pts[i].DistanceTo(pts[i - 1]);
                while (seg > 0 && travelled + seg >= next && result.Count < count - 1)
                {
                    result.Add(Vec2.Lerp(pts[i - 1], pts[i], (next - travelled) / seg));
                    next += step;
                }
                travelled += seg;
            }
            result.Add(pts[pts.Count - 1]);
            return result;
        }

        /// <summary>Ramer–Douglas–Peucker simplification (iterative; first and last point kept).</summary>
        public static IReadOnlyList<Vec2> Simplify(IReadOnlyList<Vec2> pts, double epsilon)
        {
            if (pts.Count < 3) return pts.ToList();
            var keep = new bool[pts.Count];
            keep[0] = keep[pts.Count - 1] = true;
            var stack = new Stack<(int, int)>();
            stack.Push((0, pts.Count - 1));
            while (stack.Count > 0)
            {
                var (a, b) = stack.Pop();
                double max = -1;
                var index = -1;
                for (var i = a + 1; i < b; i++)
                {
                    var d = pts[i].DistanceTo(Geo.ClosestOnSegment(pts[i], pts[a], pts[b]).Point);
                    if (d > max) { max = d; index = i; }
                }
                if (index < 0 || max <= epsilon) continue;
                keep[index] = true;
                stack.Push((a, index));
                stack.Push((index, b));
            }
            var result = new List<Vec2>();
            for (var i = 0; i < pts.Count; i++)
                if (keep[i]) result.Add(pts[i]);
            return result;
        }

        /// <summary>
        /// Catmull–Rom spline through the points with <paramref name="stepsPerSegment"/> steps per segment (passes through every
        /// point). <paramref name="alpha"/> 0 = uniform (exactly Slidemax <c>ChartGeometry.Smoothed</c>, end points doubled),
        /// 0.5 = centripetal (no cusps or self-intersections on unevenly spaced points; end tangents from mirrored points).
        /// </summary>
        public static IReadOnlyList<Vec2> CatmullRom(IReadOnlyList<Vec2> points, int stepsPerSegment = 8, double alpha = 0.5, bool closed = false)
        {
            var pts = points.ToList();
            if (closed && pts.Count > 2 && pts[0].DistanceTo(pts[pts.Count - 1]) < 1e-12) pts.RemoveAt(pts.Count - 1);
            if (pts.Count < 3) return closed && pts.Count > 0 ? pts.Concat(new[] { pts[0] }).ToList() : pts;
            var n = pts.Count;
            var segments = closed ? n : n - 1;
            var result = new List<Vec2> { pts[0] };
            for (var i = 0; i < segments; i++)
            {
                Vec2 p1 = pts[i], p2 = pts[(i + 1) % n];
                Vec2 p0, p3;
                if (closed)
                {
                    p0 = pts[(i - 1 + n) % n];
                    p3 = pts[(i + 2) % n];
                }
                else if (alpha == 0)
                {
                    p0 = pts[Math.Max(0, i - 1)];
                    p3 = pts[Math.Min(n - 1, i + 2)];
                }
                else
                {
                    p0 = i > 0 ? pts[i - 1] : p1 * 2 - p2;
                    p3 = i + 2 < n ? pts[i + 2] : p2 * 2 - p1;
                }
                for (var k = 1; k <= stepsPerSegment; k++)
                    result.Add(CatmullRomAt(p0, p1, p2, p3, (double)k / stepsPerSegment, alpha));
            }
            // A closed curve ends exactly where it starts (the last spline step only reaches it up to rounding, which
            // differs between platforms).
            if (closed) result[result.Count - 1] = pts[0];
            return result;
        }

        private static Vec2 CatmullRomAt(Vec2 p0, Vec2 p1, Vec2 p2, Vec2 p3, double t, double alpha)
        {
            if (alpha == 0)
            {
                // Uniform Catmull–Rom, ported from Slidemax ChartGeometry.Smoothed.
                double t2 = t * t, t3 = t2 * t;
                double x = 0.5 * (2 * p1.X + (-p0.X + p2.X) * t + (2 * p0.X - 5 * p1.X + 4 * p2.X - p3.X) * t2 + (-p0.X + 3 * p1.X - 3 * p2.X + p3.X) * t3);
                double y = 0.5 * (2 * p1.Y + (-p0.Y + p2.Y) * t + (2 * p0.Y - 5 * p1.Y + 4 * p2.Y - p3.Y) * t2 + (-p0.Y + 3 * p1.Y - 3 * p2.Y + p3.Y) * t3);
                return new Vec2(x, y);
            }
            // Barry–Goldman pyramid with knot spacing |Δp|^alpha.
            double Knot(Vec2 a, Vec2 b) => Math.Max(1e-12, Math.Pow(a.DistanceTo(b), alpha));
            double t0 = 0, t1 = t0 + Knot(p0, p1), t2k = t1 + Knot(p1, p2), t3k = t2k + Knot(p2, p3);
            var tt = t1 + (t2k - t1) * t;
            Vec2 L(Vec2 a, Vec2 b, double ta, double tb) => a * ((tb - tt) / (tb - ta)) + b * ((tt - ta) / (tb - ta));
            Vec2 a1 = L(p0, p1, t0, t1), a2 = L(p1, p2, t1, t2k), a3 = L(p2, p3, t2k, t3k);
            Vec2 b1 = L(a1, a2, t0, t2k), b2 = L(a2, a3, t1, t3k);
            return L(b1, b2, t1, t2k);
        }

        // ---------------------------------------------------------------- pre-processing

        private static bool IsClosed(IReadOnlyList<Vec2> r)
        {
            var box = Rect.FromPoints(r);
            var side = Math.Max(box.W, box.H);
            if (side < 1e-9 || Math.Min(box.W, box.H) < 0.1 * side) return false;
            if (r[0].DistanceTo(r[r.Count - 1]) > ClosedGapRatio * side) return false;
            double forward = 0, backward = 0;
            for (var i = 1; i < r.Count; i++)
            {
                var du = r[i].X - r[i - 1].X;
                if (du > 0) forward += du; else backward -= du;
            }
            return Math.Min(forward, backward) >= ClosedBacktrackShare * (forward + backward);
        }

        private static IReadOnlyList<Vec2> Smooth(IReadOnlyList<Vec2> r, bool closed)
        {
            // Moving average against jitter (ends kept), simplify, then a centripetal Catmull–Rom spline.
            var n = r.Count;
            var avg = new List<Vec2>(n);
            for (var i = 0; i < n; i++)
            {
                if (!closed && (i == 0 || i == n - 1)) { avg.Add(r[i]); continue; }
                var w = closed ? SmoothWindow : Math.Min(SmoothWindow, Math.Min(i, n - 1 - i));
                var s = Vec2.Zero;
                for (var k = -w; k <= w; k++) s += r[((i + k) % n + n) % n];
                avg.Add(s / (2 * w + 1));
            }
            var simple = Simplify(avg, SmoothSimplifyEpsilon);
            return CatmullRom(simple, 8, 0.5, closed);
        }

        // ---------------------------------------------------------------- residual machinery

        /// <summary>Trail data shared by the fits (frame-normalised coordinates).</summary>
        private sealed class Context
        {
            public Context(IReadOnlyList<Vec2> r, double fy, double fh)
            {
                Trail = r;
                // Weight of a point = its share of the horizontal travel (chord over ±WeightWindow points, robust to
                // tremor), so least squares on v(u) samples evenly in u and vertical edges (many arc-length points at one u)
                // do not dominate.
                var n = r.Count;
                var w = new double[n];
                for (var i = 0; i < n; i++)
                {
                    int a = Math.Max(0, i - WeightWindow), b = Math.Min(n - 1, i + WeightWindow);
                    w[i] = b > a ? Math.Abs(r[b].X - r[a].X) / (b - a) : 1;
                }
                var total = w.Sum();
                for (var i = 0; i < n; i++) w[i] = total > 0 ? w[i] * n / total : 1;
                var order = Enumerable.Range(0, n).OrderBy(i => r[i].X).ToArray();
                Sorted = order.Select(i => r[i]).ToArray();
                Weight = order.Select(i => w[i]).ToArray();
                SortedU = Sorted.Select(p => p.X).ToArray();
                UMin = SortedU[0];
                UMax = SortedU[SortedU.Length - 1];
                VZero = -fy / fh;
                var step = Math.Max(1, (int)Math.Ceiling(n / (double)CoarsePoints));
                Coarse = r.Where((_, i) => i % step == 0 || i == n - 1).ToArray();
                CoarseSorted = Coarse.OrderBy(p => p.X).ToArray();
                CoarseSortedU = CoarseSorted.Select(p => p.X).ToArray();
            }

            public IReadOnlyList<Vec2> Trail { get; }
            /// <summary>Trail points sorted by u (for vertical least squares and nearest-point search).</summary>
            public Vec2[] Sorted { get; }
            /// <summary>Weights of <see cref="Sorted"/> (share of the horizontal travel, mean 1).</summary>
            public double[] Weight { get; }
            public double[] SortedU { get; }
            /// <summary>Subsample of the trail used while refining (final residuals use the full trail).</summary>
            public Vec2[] Coarse { get; }
            public Vec2[] CoarseSorted { get; }
            public double[] CoarseSortedU { get; }
            public double UMin { get; }
            public double UMax { get; }
            public double URange => UMax - UMin;
            /// <summary>Normalised v of the data value y = 0 (asymptote of a decay).</summary>
            public double VZero { get; }
        }

        private static double Huber(double d) => d <= HuberScale ? d * d : HuberScale * (2 * d - HuberScale);

        /// <summary>Symmetric robust residual of a function curve: sqrt(½ (mean ρ(trail → curve) + mean ρ(curve → trail))).</summary>
        private static double Residual(Context c, CurveSpec spec, bool coarse = false)
        {
            var pad = 0.05 * c.URange;
            if (!Plausible(c, spec)) return double.PositiveInfinity;
            var curve = spec.Sample(c.UMin - pad, c.UMax + pad, coarse ? 48 : 96);
            if (curve.Count < 2 || PathLength(curve) > MaxCurveLength) return double.PositiveInfinity;
            var trail = coarse ? (IReadOnlyList<Vec2>)c.Coarse : c.Trail;
            double fwd = 0;
            foreach (var p in trail) fwd += Huber(DistanceToMonotone(curve, p));
            fwd /= trail.Count;
            double rev = 0;
            var m = 0;
            var sorted = coarse ? c.CoarseSorted : c.Sorted;
            var sortedU = coarse ? c.CoarseSortedU : c.SortedU;
            foreach (var q in Densify(curve, c.UMin, c.UMax, coarse ? 2 * DensifyStep : DensifyStep))
            {
                rev += Huber(NearestTrail(sorted, sortedU, q));
                m++;
            }
            if (m == 0) return double.PositiveInfinity;
            return Math.Sqrt(0.5 * (fwd + rev / m));
        }

        /// <summary>
        /// Parameter sets a sketch can plausibly mean (also keeps Nelder–Mead away from huge sampled curves): periods between
        /// <see cref="MinPeriodShare"/> and 4 × the trail's x range, time constants above 1/500 of it, levels and amplitudes
        /// within ±<see cref="MaxLevel"/> frames.
        /// </summary>
        private static bool Plausible(Context c, CurveSpec spec)
        {
            var v = spec.Values;
            var r = c.URange;
            bool Level(double y) => Math.Abs(y) <= MaxLevel;
            bool Period(double p) => p >= MinPeriodShare * r && p <= 4 * r;
            switch (spec.Kind)
            {
                case CurveKind.Constant: return Level(v[0]);
                case CurveKind.Ramp: return Level(v[1]) && Math.Abs(v[2]) * r <= MaxLevel;
                case CurveKind.Step: return Level(v[1]) && Level(v[2]);
                case CurveKind.Pulse: return v[1] >= 0.005 * r && Level(v[2]) && Level(v[3]);
                case CurveKind.Sawtooth:
                case CurveKind.Triangle: return Period(v[0]) && Level(v[1]) && Level(v[2]);
                case CurveKind.Sine: return Period(v[1]) && Level(v[0]) && Level(v[3]);
                case CurveKind.Pt1: return v[2] >= 0.002 * r && v[2] <= 100 * r && Level(v[0]) && Level(v[1]);
                default: return true;
            }
        }

        /// <summary>Distance of <paramref name="p"/> to a polyline whose x never decreases (window search around p.X).</summary>
        private static double DistanceToMonotone(IReadOnlyList<Vec2> c, Vec2 p)
        {
            int lo = 0, hi = c.Count - 1;
            while (lo < hi)
            {
                var mid = (lo + hi) / 2;
                if (c[mid].X < p.X) lo = mid + 1; else hi = mid;
            }
            var j = Math.Max(1, Math.Min(c.Count - 1, lo));
            var best = SegDist(p, c[j - 1], c[j]);
            for (var s = j - 2; s >= 0 && c[s + 1].X >= p.X - best; s--) best = Math.Min(best, SegDist(p, c[s], c[s + 1]));
            for (var s = j; s + 1 < c.Count && c[s].X <= p.X + best; s++) best = Math.Min(best, SegDist(p, c[s], c[s + 1]));
            return best;
        }

        private static double SegDist(Vec2 p, Vec2 a, Vec2 b) => p.DistanceTo(Geo.ClosestOnSegment(p, a, b).Point);

        /// <summary>Points along the curve within [uMin, uMax], at most <see cref="DensifyStep"/> apart.</summary>
        private static IEnumerable<Vec2> Densify(IReadOnlyList<Vec2> c, double uMin, double uMax, double stepLength)
        {
            for (var i = 0; i + 1 < c.Count; i++)
            {
                Vec2 a = c[i], b = c[i + 1];
                if (Math.Max(a.X, b.X) < uMin || Math.Min(a.X, b.X) > uMax) continue;
                double t0 = 0, t1 = 1;
                if (b.X != a.X)
                {
                    double ta = (uMin - a.X) / (b.X - a.X), tb = (uMax - a.X) / (b.X - a.X);
                    t0 = Math.Max(0, Math.Min(ta, tb));
                    t1 = Math.Min(1, Math.Max(ta, tb));
                    if (t1 <= t0) continue;
                }
                Vec2 s = Vec2.Lerp(a, b, t0), e = Vec2.Lerp(a, b, t1);
                var k = Math.Max(1, (int)Math.Ceiling(s.DistanceTo(e) / stepLength));
                for (var j = 0; j < k; j++) yield return Vec2.Lerp(s, e, (double)j / k);
                if (i + 2 == c.Count || c[i + 2].X > uMax) yield return e;
            }
        }

        /// <summary>Distance from <paramref name="q"/> to the nearest resampled trail point.</summary>
        private static double NearestTrail(Vec2[] sorted, double[] u, Vec2 q)
        {
            var i = Array.BinarySearch(u, q.X);
            if (i < 0) i = ~i;
            var best = double.PositiveInfinity;
            for (var k = Math.Min(i, u.Length - 1); k >= 0 && q.X - u[k] < best; k--) best = Math.Min(best, q.DistanceTo(sorted[k]));
            for (var k = i; k < u.Length && u[k] - q.X < best; k++) best = Math.Min(best, q.DistanceTo(sorted[k]));
            return best;
        }

        /// <summary>Refines the parameters with Nelder–Mead on the symmetric residual; invalid parameter sets cost +∞.</summary>
        private static (CurveSpec Spec, double Residual) Refine(Context c, CurveKind kind, double[] start, double[] steps, Func<double[], double[]?> build)
        {
            double Cost(double[] p)
            {
                var v = build(p);
                if (v is null) return double.PositiveInfinity;
                var info = CurveFunctions.Info(kind);
                for (var i = 0; i < v.Length; i++)
                    if (!info.Params[i].IsValid(v[i])) return double.PositiveInfinity;
                return Residual(c, new CurveSpec(kind, v), coarse: true);
            }
            var best = Cost(start) > SkipRefineResidual ? start : NelderMead(Cost, start, steps, RefineEvaluations);
            var values = build(best) ?? build(start)!;
            var spec = new CurveSpec(kind, values);
            return (spec, Residual(c, spec));
        }

        private static double[] NelderMead(Func<double[], double> f, double[] x0, double[] steps, int maxEvals)
        {
            var n = x0.Length;
            var simplex = new double[n + 1][];
            var values = new double[n + 1];
            simplex[0] = (double[])x0.Clone();
            values[0] = f(simplex[0]);
            var evals = 1;
            for (var i = 0; i < n; i++)
            {
                var x = (double[])x0.Clone();
                x[i] += steps[i];
                simplex[i + 1] = x;
                values[i + 1] = f(x);
                evals++;
            }
            double[] Combine(double[] a, double[] b, double t)
            {
                var r = new double[n];
                for (var i = 0; i < n; i++) r[i] = a[i] + t * (b[i] - a[i]);
                return r;
            }
            while (evals < maxEvals)
            {
                var order = Enumerable.Range(0, n + 1).OrderBy(i => values[i]).ToArray();
                simplex = order.Select(i => simplex[i]).ToArray();
                values = order.Select(i => values[i]).ToArray();
                if (Math.Abs(values[n] - values[0]) <= 1e-7 && !double.IsInfinity(values[n])) break;
                var centroid = new double[n];
                for (var i = 0; i < n; i++)
                    for (var k = 0; k < n; k++) centroid[k] += simplex[i][k] / n;
                var reflected = Combine(centroid, simplex[n], -1);
                var fr = f(reflected); evals++;
                if (fr < values[0])
                {
                    var expanded = Combine(centroid, simplex[n], -2);
                    var fe = f(expanded); evals++;
                    if (fe < fr) { simplex[n] = expanded; values[n] = fe; }
                    else { simplex[n] = reflected; values[n] = fr; }
                }
                else if (fr < values[n - 1]) { simplex[n] = reflected; values[n] = fr; }
                else
                {
                    var outside = fr < values[n];
                    var contracted = Combine(centroid, outside ? reflected : simplex[n], 0.5);
                    var fc = f(contracted); evals++;
                    if (fc < (outside ? fr : values[n])) { simplex[n] = contracted; values[n] = fc; }
                    else
                    {
                        for (var i = 1; i <= n; i++)
                        {
                            simplex[i] = Combine(simplex[0], simplex[i], 0.5);
                            values[i] = f(simplex[i]); evals++;
                        }
                    }
                }
            }
            var bestIndex = 0;
            for (var i = 1; i <= n; i++) if (values[i] < values[bestIndex]) bestIndex = i;
            return simplex[bestIndex];
        }

        /// <summary>Weighted median of v over the sorted index ranges [from, to) and [from2, to2).</summary>
        private static double WeightedMedian(Context c, int from, int to, int from2 = 0, int to2 = 0)
        {
            var items = Enumerable.Range(from, Math.Max(0, to - from)).Concat(Enumerable.Range(from2, Math.Max(0, to2 - from2)))
                .Select(i => (V: c.Sorted[i].Y, W: c.Weight[i])).OrderBy(x => x.V).ToList();
            if (items.Count == 0) return 0;
            var total = items.Sum(x => x.W);
            if (total <= 1e-12) return items[items.Count / 2].V;
            double acc = 0;
            foreach (var x in items)
            {
                acc += x.W;
                if (acc >= total / 2) return x.V;
            }
            return items[items.Count - 1].V;
        }

        /// <summary>Least squares v ≈ a + b·w; returns (a, b, sum of squared residuals).</summary>
        private static (double A, double B, double Sse) Linear(Vec2[] s, double[] weight, Func<double, double> w)
        {
            double n = 0, sw = 0, sww = 0, sv = 0, swv = 0, svv = 0;
            for (var i = 0; i < s.Length; i++)
            {
                var p = s[i];
                var g = weight[i];
                var x = w(p.X);
                n += g; sw += g * x; sww += g * x * x; sv += g * p.Y; swv += g * x * p.Y; svv += g * p.Y * p.Y;
            }
            var det = n * sww - sw * sw;
            if (Math.Abs(det) < 1e-12 * n * n) return (sv / n, 0, Math.Max(0, svv - sv * sv / n));
            var b = (n * swv - sw * sv) / det;
            var a = (sv - b * sw) / n;
            // SSE = Σ(v − a − b w)² expanded.
            var sse = svv + n * a * a + b * b * sww - 2 * a * sv - 2 * b * swv + 2 * a * b * sw;
            return (a, b, Math.Max(0, sse));
        }

        // ---------------------------------------------------------------- fits (frame-normalised coordinates)

        private static (CurveKind, CurveSpec, double)? FitConstant(Context c)
        {
            var v0 = WeightedMedian(c, 0, c.Sorted.Length);
            var (spec, res) = Refine(c, CurveKind.Constant, new[] { v0 }, new[] { 0.02 }, p => new[] { p[0] });
            return (CurveKind.Constant, spec, res);
        }

        private static (CurveKind, CurveSpec, double)? FitRamp(Context c)
        {
            // Total least squares line (principal axis of the arc-length resampled points).
            var pts = c.Trail;
            double cu = pts.Average(p => p.X), cv = pts.Average(p => p.Y), suu = 0, svv = 0, suv = 0;
            foreach (var p in pts)
            {
                double du = p.X - cu, dv = p.Y - cv;
                suu += du * du; svv += dv * dv; suv += du * dv;
            }
            var angle = 0.5 * Math.Atan2(2 * suv, suu - svv);
            double dx = Math.Cos(angle), dy = Math.Sin(angle);
            if (Math.Abs(dx) < 1e-3) return null;
            var slope = dy / dx;
            var u0 = c.UMin;
            var y0 = cv + slope * (u0 - cu);
            var (spec, res) = Refine(c, CurveKind.Ramp, new[] { y0, slope }, new[] { 0.02, 0.05 * Math.Max(0.2, Math.Abs(slope)) }, p => new[] { u0, p[0], p[1] });
            return (CurveKind.Ramp, spec, res);
        }

        private static (CurveKind, CurveSpec, double)? FitStep(Context c)
        {
            var s = c.Sorted;
            var n = s.Length;
            var prefix = Prefix(c);
            var side = Math.Max(2, n / 20);
            double bestCost = double.PositiveInfinity;
            var k = -1;
            for (var i = side; i <= n - side; i++)
            {
                var cost = Sse(prefix, 0, i) + Sse(prefix, i, n);
                if (cost < bestCost) { bestCost = cost; k = i; }
            }
            if (k < 0) return null;
            var u0 = (s[k - 1].X + s[k].X) / 2;
            var v0 = WeightedMedian(c, 0, k);
            var v1 = WeightedMedian(c, k, n);
            var (spec, res) = Refine(c, CurveKind.Step, new[] { u0, v0, v1 }, new[] { 0.02 * c.URange, 0.02, 0.02 }, p => p);
            return (CurveKind.Step, spec, res);
        }

        private static (CurveKind, CurveSpec, double)? FitPulse(Context c)
        {
            var s = c.Sorted;
            var n = s.Length;
            var prefix = Prefix(c);
            var side = Math.Max(2, n / 20);
            var inner = Math.Max(2, n / 40);
            double bestCost = double.PositiveInfinity;
            int bi = -1, bj = -1;
            for (var i = side; i <= n - side - inner; i++)
                for (var j = i + inner; j <= n - side; j++)
                {
                    // Outside = [0, i) ∪ [j, n) about one mean, inside = [i, j) about another.
                    var cnt = prefix.W[i] + (prefix.W[n] - prefix.W[j]);
                    if (cnt <= 1e-12) continue;
                    double sum = prefix.S[i] + (prefix.S[n] - prefix.S[j]);
                    double sq = prefix.Q[i] + (prefix.Q[n] - prefix.Q[j]);
                    var cost = sq - sum * sum / cnt + Sse(prefix, i, j);
                    if (cost < bestCost) { bestCost = cost; bi = i; bj = j; }
                }
            if (bi < 0) return null;
            double start = (s[bi - 1].X + s[bi].X) / 2, end = (s[bj - 1].X + s[bj].X) / 2;
            var low = WeightedMedian(c, 0, bi, bj, n);
            var high = WeightedMedian(c, bi, bj);
            var (spec, res) = Refine(c, CurveKind.Pulse, new[] { start, end - start, low, high }, new[] { 0.02 * c.URange, 0.02 * c.URange, 0.02, 0.02 }, p => p);
            return (CurveKind.Pulse, spec, res);
        }

        /// <summary>Weighted prefix sums of weight (W), w·v (S) and w·v² (Q) over the sorted points.</summary>
        private sealed class Sums
        {
            public double[] W = Array.Empty<double>();
            public double[] S = Array.Empty<double>();
            public double[] Q = Array.Empty<double>();
        }

        private static Sums Prefix(Context c)
        {
            var s = c.Sorted;
            var r = new Sums { W = new double[s.Length + 1], S = new double[s.Length + 1], Q = new double[s.Length + 1] };
            for (var i = 0; i < s.Length; i++)
            {
                var g = c.Weight[i];
                r.W[i + 1] = r.W[i] + g;
                r.S[i + 1] = r.S[i] + g * s[i].Y;
                r.Q[i + 1] = r.Q[i] + g * s[i].Y * s[i].Y;
            }
            return r;
        }

        private static double Sse(Sums p, int from, int to)
        {
            var n = p.W[to] - p.W[from];
            if (n <= 1e-12) return 0;
            var sum = p.S[to] - p.S[from];
            return p.Q[to] - p.Q[from] - sum * sum / n;
        }

        private static double Frac(double t) => t - Math.Floor(t);

        private static (CurveKind, CurveSpec, double)? FitPeriodic(Context c, CurveKind kind)
        {
            var s = c.Sorted;
            var range = c.URange;
            // Grid: number of periods across the trail 0.6 … 12, phases in 1/32 period (sine: phase by linear least squares).
            var grid = new List<(double Sse, double[] Start)>();
            for (var m = 0.6; m <= 12.0001; m += 0.1)
            {
                var period = range / m;
                if (kind == CurveKind.Sine)
                {
                    var g = SineLsq(s, c.Weight, period);
                    if (g is { } x) grid.Add((x.Sse, new[] { x.Amplitude, period, x.Phase, x.Offset }));
                    continue;
                }
                for (var k = 0; k < 32; k++)
                {
                    var phase = c.UMin + period * k / 32;
                    Func<double, double> w = kind == CurveKind.Sawtooth
                        ? u => Frac((u - phase) / period)
                        : u => { var f = Frac((u - phase) / period); return f < 0.5 ? 2 * f : 2 - 2 * f; };
                    var (a, b, sse) = Linear(s, c.Weight, w);
                    grid.Add((sse, new[] { period, a, a + b, phase }));
                }
            }
            if (grid.Count == 0) return null;
            // Refine the two best grid starts with clearly different periods.
            var periodIndex = kind == CurveKind.Sine ? 1 : 0;
            var starts = new List<double[]>();
            var bestSse = grid.Min(g => g.Sse);
            foreach (var g in grid.OrderBy(g => g.Sse))
            {
                if (starts.Count > 0 && g.Sse > 1.5 * bestSse + 1e-9) break;
                if (starts.Any(x => Math.Abs(x[periodIndex] - g.Start[periodIndex]) < 0.08 * g.Start[periodIndex])) continue;
                starts.Add(g.Start);
                if (starts.Count == 2) break;
            }
            (CurveSpec Spec, double Residual)? best = null;
            foreach (var st in starts)
            {
                var period = st[periodIndex];
                var steps = kind == CurveKind.Sine
                    ? new[] { 0.02, 0.03 * period, 0.05 * period, 0.02 }
                    : new[] { 0.03 * period, 0.02, 0.02, 0.05 * period };
                var r = Refine(c, kind, st, steps, p => p);
                if (best is null || r.Residual < best.Value.Residual) best = r;
            }
            var spec = best!.Value.Spec;
            var v = spec.Values.ToArray();
            if (kind == CurveKind.Sine && v[0] < 0) { v[0] = -v[0]; v[2] += v[1] / 2; }
            if (kind == CurveKind.Triangle && v[1] > v[2]) { (v[1], v[2]) = (v[2], v[1]); v[3] += v[0] / 2; }
            return (kind, new CurveSpec(kind, v), best.Value.Residual);
        }

        private static (double Amplitude, double Phase, double Offset, double Sse)? SineLsq(Vec2[] s, double[] weight, double period)
        {
            // v ≈ c + a sin(ωu) + b cos(ωu): 3 × 3 normal equations.
            var w = 2 * Math.PI / period;
            var m = new double[3, 3];
            var rhs = new double[3];
            double svv = 0;
            for (var k = 0; k < s.Length; k++)
            {
                var p = s[k];
                var g = weight[k];
                double[] f = { 1, Math.Sin(w * p.X), Math.Cos(w * p.X) };
                for (var i = 0; i < 3; i++)
                {
                    rhs[i] += g * f[i] * p.Y;
                    for (var j = 0; j < 3; j++) m[i, j] += g * f[i] * f[j];
                }
                svv += g * p.Y * p.Y;
            }
            var x = Solve(m, rhs, 3);
            if (x is null) return null;
            double c0 = x[0], a = x[1], b = x[2];
            // SSE = vᵀv − 2 xᵀ rhs + xᵀ M x = vᵀv − xᵀ rhs (normal equations).
            var sse = svv - (c0 * rhs[0] + a * rhs[1] + b * rhs[2]);
            var amplitude = Math.Sqrt(a * a + b * b);
            var phase = period * Math.Atan2(-b, a) / (2 * Math.PI);
            return (amplitude, phase, c0, Math.Max(0, sse));
        }

        /// <summary>PT1 (free asymptote) or decay (asymptote fixed at y = 0, fitted as PT1 with that asymptote).</summary>
        private static (CurveKind, CurveSpec, double)? FitExponential(Context c, bool decay)
        {
            var s = c.Sorted;
            var range = c.URange;
            var vz = c.VZero;
            double bestSse = double.PositiveInfinity;
            double[]? bestStart = null;
            for (var ti = 0; ti < 24; ti++)
            {
                var tau = range * 0.02 * Math.Pow(150, ti / 23.0);
                for (var k = 0; k <= 10; k++)
                {
                    var t0 = c.UMin + range * 0.05 * k;
                    double sse;
                    double[] start;
                    if (decay)
                    {
                        // v − vz = a·g, g = 1 before t0, e^(−(u − t0)/τ) after.
                        double sgg = 0, sgv = 0, svv = 0;
                        for (var i = 0; i < s.Length; i++)
                        {
                            var p = s[i];
                            var wt = c.Weight[i];
                            var g = p.X < t0 ? 1 : Math.Exp(-(p.X - t0) / tau);
                            var d = p.Y - vz;
                            sgg += wt * g * g; sgv += wt * g * d; svv += wt * d * d;
                        }
                        var a = sgv / sgg;
                        sse = svv - a * sgv;
                        start = new[] { vz + a, tau, t0 };
                    }
                    else
                    {
                        var (a, b, e) = Linear(s, c.Weight, u => u < t0 ? 0 : 1 - Math.Exp(-(u - t0) / tau));
                        sse = e;
                        start = new[] { a, a + b, tau, t0 };
                    }
                    if (sse < bestSse) { bestSse = sse; bestStart = start; }
                }
            }
            if (bestStart is null) return null;
            var uMin = c.UMin;
            if (decay)
            {
                var (spec, res) = Refine(c, CurveKind.Pt1, bestStart, new[] { 0.02, 0.1 * bestStart[1], 0.02 * range },
                    p => new[] { p[0], vz, p[1], Math.Max(uMin, p[2]) });
                return (CurveKind.Decay, spec, res);
            }
            var (pt1, r1) = Refine(c, CurveKind.Pt1, bestStart, new[] { 0.02, 0.02, 0.1 * bestStart[2], 0.02 * range },
                p => new[] { p[0], p[1], p[2], Math.Max(uMin, p[3]) });
            return (CurveKind.Pt1, pt1, r1);
        }

        // ---------------------------------------------------------------- closed loops

        private static (CurveKind, CurveSpec, double)? FitEllipse(Context c)
        {
            var pts = c.Trail;
            double mu = pts.Average(p => p.X), mv = pts.Average(p => p.Y);
            // Axis-parallel conic a·u² + b·v² + d·u + e·v = 1 in centred coordinates (the centre is inside, so F ≠ 0).
            var m = new double[4, 4];
            var rhs = new double[4];
            foreach (var p in pts)
            {
                double u = p.X - mu, v = p.Y - mv;
                double[] f = { u * u, v * v, u, v };
                for (var i = 0; i < 4; i++)
                {
                    rhs[i] += f[i];
                    for (var j = 0; j < 4; j++) m[i, j] += f[i] * f[j];
                }
            }
            var x = Solve(m, rhs, 4);
            double cx, cy, rx, ry;
            if (x is not null && x[0] > 0 && x[1] > 0)
            {
                var ou = -x[2] / (2 * x[0]);
                var ov = -x[3] / (2 * x[1]);
                var g = 1 + x[0] * ou * ou + x[1] * ov * ov;
                cx = mu + ou; cy = mv + ov;
                rx = Math.Sqrt(Math.Max(1e-9, g / x[0]));
                ry = Math.Sqrt(Math.Max(1e-9, g / x[1]));
            }
            else
            {
                var box = Rect.FromPoints(pts);
                cx = box.Center.X; cy = box.Center.Y; rx = Math.Max(1e-6, box.W / 2); ry = Math.Max(1e-6, box.H / 2);
            }
            double Cost(double[] p)
            {
                if (!(p[2] > 1e-6) || !(p[3] > 1e-6)) return double.PositiveInfinity;
                return ClosedResidual(c, new CurveSpec(CurveKind.Ellipse, p), coarse: true);
            }
            var best = NelderMead(Cost, new[] { cx, cy, rx, ry }, new[] { 0.02 * rx, 0.02 * ry, 0.03 * rx, 0.03 * ry }, RefineEvaluations);
            var spec = new CurveSpec(CurveKind.Ellipse, best);
            return (CurveKind.Ellipse, spec, ClosedResidual(c, spec));
        }

        private static double ClosedResidual(Context c, CurveSpec spec, bool coarse = false)
        {
            var poly = spec.Sample(0, 0, coarse ? 48 : 96);
            var trail = coarse ? (IReadOnlyList<Vec2>)c.Coarse : c.Trail;
            double fwd = 0;
            foreach (var p in trail)
            {
                var best = double.PositiveInfinity;
                for (var i = 0; i + 1 < poly.Count; i++) best = Math.Min(best, SegDist(p, poly[i], poly[i + 1]));
                fwd += Huber(best);
            }
            double rev = 0;
            var dense = Densify(poly, coarse ? 2 * DensifyStep : DensifyStep);
            foreach (var q in dense)
            {
                var best = double.PositiveInfinity;
                foreach (var p in trail) best = Math.Min(best, q.DistanceTo(p));
                rev += Huber(best);
            }
            return Math.Sqrt(0.5 * (fwd / trail.Count + rev / Math.Max(1, dense.Count)));
        }

        private static List<Vec2> Densify(IReadOnlyList<Vec2> poly, double stepLength)
        {
            var list = new List<Vec2>();
            for (var i = 0; i + 1 < poly.Count; i++)
            {
                var k = Math.Max(1, (int)Math.Ceiling(poly[i].DistanceTo(poly[i + 1]) / stepLength));
                for (var j = 0; j < k; j++) list.Add(Vec2.Lerp(poly[i], poly[i + 1], (double)j / k));
            }
            return list;
        }

        private static double[]? Solve(double[,] m, double[] r, int n)
        {
            var a = new double[n, n + 1];
            for (var i = 0; i < n; i++)
            {
                for (var j = 0; j < n; j++) a[i, j] = m[i, j];
                a[i, n] = r[i];
            }
            for (var col = 0; col < n; col++)
            {
                var pivot = col;
                for (var i = col + 1; i < n; i++) if (Math.Abs(a[i, col]) > Math.Abs(a[pivot, col])) pivot = i;
                if (Math.Abs(a[pivot, col]) < 1e-14) return null;
                for (var j = 0; j <= n; j++) (a[col, j], a[pivot, j]) = (a[pivot, j], a[col, j]);
                for (var i = 0; i < n; i++)
                {
                    if (i == col) continue;
                    var f = a[i, col] / a[col, col];
                    for (var j = col; j <= n; j++) a[i, j] -= f * a[col, j];
                }
            }
            var x = new double[n];
            for (var i = 0; i < n; i++) x[i] = a[i, n] / a[i, i];
            return x;
        }
    }
}
