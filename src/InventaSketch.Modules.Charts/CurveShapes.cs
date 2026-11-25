using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Charts
{
    /// <summary>How a hand-sketched curve is drawn. The trail is kept, so the shape can be switched at any time without loss.</summary>
    public enum CurveShape
    {
        /// <summary>Jitter removed (moving average of the chosen strength), then a smooth spline.</summary>
        Smoothed,
        /// <summary>A spline exactly through the key points of the trail (corners and turning points).</summary>
        Interpolated,
        /// <summary>The fitted function (step, pulse, ramp, sine, PT1, exponential …; one of <see cref="CurveSketchMemo.Fits"/>).</summary>
        Fitted,
        /// <summary>Straight segments between the key points.</summary>
        Segments,
        /// <summary>The trail as drawn (evenly spaced, only the finest tremor removed).</summary>
        Raw,
    }

    /// <summary>
    /// How many points a sketched curve keeps (part choice "Detail"): the tolerances of the key point selection
    /// (<see cref="CurveKeyPoints"/>) are doubled for <see cref="Low"/> and halved for <see cref="High"/>.
    /// </summary>
    public enum CurveDetail
    {
        Low,
        /// <summary>The default: adaptive key points with the base tolerances.</summary>
        Medium,
        High,
    }

    /// <summary>
    /// What a sketched curve remembers: the chosen shape, the smoothing strength, the detail, the fitted functions (best
    /// first), the lightly cleaned trail and the drawing pace along it. Stored with the series (XY chart: a comment line <c># sketch …</c> before the data block; figure:
    /// the member <c>sketch</c> of the series), so <see cref="CurveShapes.Points"/> can recompute the curve for every shape.
    /// </summary>
    public sealed record CurveSketchMemo
    {
        public CurveShape Shape { get; init; } = CurveShape.Smoothed;
        /// <summary>Smoothing strength 1 (light), 2 (medium) or 3 (strong).</summary>
        public int Strength { get; init; } = CurveShapes.DefaultStrength;
        /// <summary>How many key points the curve keeps (low, medium = adaptive default, high).</summary>
        public CurveDetail Detail { get; init; } = CurveDetail.Medium;
        /// <summary>The chosen fitted kind (one of <see cref="Fits"/>); null when nothing could be fitted.</summary>
        public CurveKind? Fit { get; init; }
        /// <summary>Fitted functions, best first, in fit space (the axes' own scale: log10 of the values on log axes).</summary>
        public IReadOnlyList<CurveSpec> Fits { get; init; } = Array.Empty<CurveSpec>();
        /// <summary>Axis scales the curve was sketched in (fit space = log10 on these axes).</summary>
        public bool XLog { get; init; }
        public bool YLog { get; init; }
        /// <summary>Plot extent in fit space when the curve was sketched; all tolerances are relative to it.</summary>
        public Rect Frame { get; init; }
        /// <summary>The cleaned trail in data coordinates (at most <see cref="CurveShapes.MaxRaw"/> points).</summary>
        public IReadOnlyList<Vec2> Raw { get; init; } = Array.Empty<Vec2>();
        /// <summary>
        /// Drawing pace per point of <see cref="Raw"/>: the tolerance factor from the drawing speed (<see cref="CurveKeyPoints.Pace"/>,
        /// 0.6 slow … 1.5 fast, in steps of 0.1); empty when the host gave no time stamps (factor 1 everywhere).
        /// </summary>
        public IReadOnlyList<double> Pace { get; init; } = Array.Empty<double>();

        /// <summary>The spec of <see cref="Fit"/> (else the best fit), or null.</summary>
        public CurveSpec? FitSpec => Fits.FirstOrDefault(s => s.Kind == Fit) ?? Fits.FirstOrDefault();

        /// <summary>True if the memo can produce a curve (trail of two or more points and a frame).</summary>
        public bool IsValid => Raw.Count >= 2 && Frame.W > 0 && Frame.H > 0 && Fmt.Finite(Frame.W) && Fmt.Finite(Frame.H);
    }

    /// <summary>
    /// Curve shapes of sketched curves (charts v2): the trail is cleaned once when it is sketched (<see cref="Create"/>), every
    /// shape is computed from it (<see cref="Points"/>), and the part actions "Curve shape", "Fitted function", "Smoothing" and
    /// "Detail" switch between them. All tolerances are relative to the plot frame (1 = plot size), so they act like screen
    /// distances.
    /// <para>
    /// Stored points (2026-11-25): a curve keeps only its key points (<see cref="CurveKeyPoints"/>), not dense samples.
    /// Smoothed and interpolated shapes and smooth fitted functions are drawn through them with the chart's spline
    /// (<see cref="IsSpline"/>: XY chart block marked by its memo or <c># spline</c>, figure series <c>smooth</c>); straight
    /// segments, "as drawn" and piecewise-linear functions (step, pulse, ramp, sawtooth, triangle) are drawn straight.
    /// </para>
    /// </summary>
    public static class CurveShapes
    {
        /// <summary>Most points of the kept trail.</summary>
        public const int MaxRaw = 96;
        /// <summary>Most stored points of a sketched curve (detail high; medium keeps at most 40, low 24).</summary>
        public const int MaxPoints = 64;
        /// <summary>Most fitted functions kept (best first).</summary>
        public const int MaxFits = 6;
        public const int DefaultStrength = 2;
        /// <summary>Arc-length spacing of the kept trail.</summary>
        public const double RawSpacing = 0.01;
        /// <summary>Arc-length spacing the shapes work on.</summary>
        public const double WorkSpacing = 0.005;
        /// <summary>Moving average half-widths (in <see cref="WorkSpacing"/> steps) of smoothing strengths 1, 2 and 3: ±1 %, ±2.5 %, ±5 % of the plot.</summary>
        private static readonly int[] StrengthWindow = { 0, 2, 5, 10 };
        /// <summary>Ramer–Douglas–Peucker tolerances before the spline of smoothing strengths 1, 2 and 3.</summary>
        private static readonly double[] StrengthEpsilon = { 0, 0.002, 0.004, 0.008 };
        /// <summary>Moving average half-width before the key points are picked.</summary>
        public const int KeyWindow = 3;
        /// <summary>Ramer–Douglas–Peucker tolerance of the key points of straight segments and the interpolating spline (1.5 % of the plot).</summary>
        public const double KeyEpsilon = 0.015;
        /// <summary>Largest distance between the smoothed curve and the spline through its stored points (0.6 % of the plot).</summary>
        public const double SmoothTolerance = 0.006;
        /// <summary>Largest distance between a smooth fitted function and the spline through its stored points (0.5 % of the plot).</summary>
        public const double FitTolerance = 0.005;
        /// <summary>Ramer–Douglas–Peucker tolerance of the thinned trail "as drawn" (0.2 % of the plot).</summary>
        public const double RawTolerance = 0.002;
        /// <summary>Tolerance scale of the details low, medium and high.</summary>
        private static readonly double[] DetailScale = { 2, 1, 0.5 };
        /// <summary>Most stored points of the details low, medium and high.</summary>
        private static readonly int[] DetailMax = { 24, 40, MaxPoints };

        /// <summary>Order of the "Curve shape" choice.</summary>
        public static IReadOnlyList<CurveShape> Order { get; } = new[] { CurveShape.Smoothed, CurveShape.Interpolated, CurveShape.Fitted, CurveShape.Segments, CurveShape.Raw };

        /// <summary>Order of the "Detail" choice.</summary>
        public static IReadOnlyList<CurveDetail> Details { get; } = new[] { CurveDetail.Low, CurveDetail.Medium, CurveDetail.High };

        /// <summary>Tolerance scale of a detail (low 2, medium 1, high 0.5).</summary>
        public static double Scale(CurveDetail d) => DetailScale[(int)Clamp(d)];

        /// <summary>Most stored points of a detail (low 24, medium 40, high 64).</summary>
        public static int MaxOf(CurveDetail d) => DetailMax[(int)Clamp(d)];

        private static CurveDetail Clamp(CurveDetail d) => d < CurveDetail.Low ? CurveDetail.Low : d > CurveDetail.High ? CurveDetail.High : d;

        /// <summary>True for the function kinds whose sample is exact with straight lines (constant, ramp, step, pulses, sawtooth, triangle).</summary>
        public static bool IsPiecewiseLinear(CurveKind k)
            => k is CurveKind.Constant or CurveKind.Ramp or CurveKind.Step or CurveKind.Pulse or CurveKind.PulseTrain or CurveKind.Sawtooth or CurveKind.Triangle;

        /// <summary>
        /// True if the memo's points are drawn with the chart spline: smoothed, interpolated, and fitted functions that are
        /// smooth over the drawn range (sine, ellipse; PT1, PT2 and decay only when their start corner lies before it).
        /// </summary>
        public static bool IsSpline(CurveSketchMemo m) => m.Shape switch
        {
            CurveShape.Segments or CurveShape.Raw => false,
            CurveShape.Fitted => m.FitSpec is not { } s || !StraightFit(m, s),
            _ => true,
        };

        /// <summary>
        /// True if a fitted function is stored as straight segments: piecewise-linear kinds (their exact corners) and kinds whose
        /// start corner t0 (PT1, PT2, decay) lies inside the drawn x range (a spline would round it off).
        /// </summary>
        private static bool StraightFit(CurveSketchMemo m, CurveSpec s)
        {
            if (IsPiecewiseLinear(s.Kind)) return true;
            double? corner = s.Kind switch
            {
                CurveKind.Pt1 => s.Values[3],
                CurveKind.Decay => s.Values[2],
                CurveKind.Pt2 => s.Values[4],
                _ => null,
            };
            if (corner is not double c) return false;
            var xs = m.Raw.Select(p => To(p.X, m.XLog)).Where(Fmt.Finite).ToList();
            return xs.Count > 0 && c > xs.Min() + 1e-9 * Math.Max(1, m.Frame.W);
        }

        /// <summary>True if the detail changes the memo's points (all shapes but piecewise-linear fitted functions, which are exact).</summary>
        public static bool HasDetail(CurveSketchMemo m) => !(m.Shape == CurveShape.Fitted && m.FitSpec is { } s && IsPiecewiseLinear(s.Kind));

        // ------------------------------------------------------------------ creating and computing

        /// <summary>
        /// The memo of a newly sketched curve: <paramref name="trail"/> in fit space (log10 on log axes), the plot
        /// <paramref name="frame"/> in fit space and the recognition result of the trail. A recognised kind starts as
        /// <see cref="CurveShape.Fitted"/>, anything else as <see cref="CurveShape.Smoothed"/>. <paramref name="times"/> (ms, one
        /// per trail point, optional) gives the drawing pace (<see cref="CurveKeyPoints.Pace"/>).
        /// </summary>
        public static CurveSketchMemo Create(IReadOnlyList<Vec2> trail, Rect frame, bool xlog, bool ylog, SketchFitResult fit, IReadOnlyList<double>? times = null)
        {
            frame = SafeFrame(frame, trail);
            var keep = Enumerable.Range(0, trail.Count).Where(i => Finite(trail[i])).ToList();
            var norm = keep.Select(i => Norm(trail[i], frame)).ToList();
            var raw = Clean(norm);
            var pace = times is not null && times.Count == trail.Count
                ? RawPace(norm, CurveKeyPoints.Pace(norm, keep.Select(i => times[i]).ToList()), raw.Count)
                : Array.Empty<double>();
            var fits = fit.Ranking.Take(MaxFits).Select(c => RoundSpec(c.Spec)).ToList();
            var rawData = raw.Select(p => Denorm(p, frame)).Select(p => new Vec2(Round(Back(p.X, xlog), xlog, frame.W), Round(Back(p.Y, ylog), ylog, frame.H))).ToList();
            var finite = Enumerable.Range(0, rawData.Count).Where(i => Finite(rawData[i])).ToList();
            return new CurveSketchMemo
            {
                Shape = fit.Kind is null ? CurveShape.Smoothed : CurveShape.Fitted,
                Fit = fit.Kind ?? (fits.Count > 0 ? fits[0].Kind : null),
                Fits = fits,
                XLog = xlog,
                YLog = ylog,
                Frame = frame,
                Raw = finite.Select(i => rawData[i]).ToList(),
                Pace = pace.Count == rawData.Count ? finite.Select(i => pace[i]).ToList() : Array.Empty<double>(),
            };
        }

        /// <summary>
        /// The pace at the <paramref name="count"/> evenly spaced points of the cleaned trail (by share of the arc length of
        /// <paramref name="norm"/>), lightly averaged (±2 points) and rounded to the stored steps; empty when it is 1 everywhere
        /// or unknown.
        /// </summary>
        private static IReadOnlyList<double> RawPace(IReadOnlyList<Vec2> norm, IReadOnlyList<double>? pace, int count)
        {
            if (pace is null || count < 2 || norm.Count < 2) return Array.Empty<double>();
            var n = norm.Count;
            var s = new double[n];
            for (var i = 1; i < n; i++) s[i] = s[i - 1] + norm[i].DistanceTo(norm[i - 1]);
            var total = s[n - 1];
            if (!(total > 0)) return Array.Empty<double>();
            var at = new double[count];
            var k = 0;
            for (var j = 0; j < count; j++)
            {
                var u = total * j / (count - 1);
                while (k < n - 2 && s[k + 1] < u) k++;
                var seg = s[k + 1] - s[k];
                var t = seg > 0 ? Geo.Clamp((u - s[k]) / seg, 0, 1) : 0;
                at[j] = pace[k] + (pace[k + 1] - pace[k]) * t;
            }
            var result = new List<double>(count);
            for (var j = 0; j < count; j++)
            {
                int a = Math.Max(0, j - 2), b = Math.Min(count - 1, j + 2);
                double sum = 0;
                for (var i = a; i <= b; i++) sum += at[i];
                result.Add(CurveKeyPoints.Quantise(sum / (b - a + 1)));
            }
            return result.All(f => Math.Abs(f - 1) < 1e-9) ? Array.Empty<double>() : result;
        }

        /// <summary>
        /// The stored points of the memo's shape in data coordinates – its key points (<see cref="CurveKeyPoints"/>; at most
        /// <see cref="MaxOf"/> of its detail), drawn as a spline when <see cref="IsSpline"/>; empty for an invalid memo.
        /// </summary>
        public static IReadOnlyList<Vec2> Points(CurveSketchMemo m)
        {
            if (!m.IsValid) return Array.Empty<Vec2>();
            // The choices' previews ask for the same memos again and again: keep the last results (keyed by the memo's text).
            var key = ToComment(m) + " strength=" + Fmt.I(m.Strength);
            lock (Cache)
                if (Cache.TryGetValue(key, out var hit)) return hit;
            var result = Compute(m);
            lock (Cache)
            {
                if (Cache.Count >= CacheSize) Cache.Clear();
                Cache[key] = result;
            }
            return result;
        }

        private const int CacheSize = 256;
        private static readonly Dictionary<string, IReadOnlyList<Vec2>> Cache = new(StringComparer.Ordinal);

        private static IReadOnlyList<Vec2> Compute(CurveSketchMemo m)
        {
            var frame = m.Frame;
            var fitRaw = m.Raw.Select(p => new Vec2(To(p.X, m.XLog), To(p.Y, m.YLog))).ToList();
            var ok = Enumerable.Range(0, fitRaw.Count).Where(i => Finite(fitRaw[i])).ToList();
            if (ok.Count < 2) return Array.Empty<Vec2>();
            var raw = ok.Select(i => Norm(fitRaw[i], frame)).ToList();
            var pace = m.Pace.Count == m.Raw.Count ? ok.Select(i => m.Pace[i]).ToList() : null;
            var d = Clamp(m.Detail);
            var adaptive = true;
            IReadOnlyList<Vec2> norm;
            if (m.Shape == CurveShape.Fitted && m.FitSpec is { } spec)
            {
                var sample = spec.Sample(ok.Min(i => fitRaw[i].X), ok.Max(i => fitRaw[i].X)).Where(Finite).Select(p => Norm(p, frame)).ToList();
                if (IsPiecewiseLinear(spec.Kind)) { norm = Thin(sample, MaxPoints); adaptive = false; }
                else
                {
                    var straight = StraightFit(m, spec);
                    var target = straight ? sample : Uniform(sample);
                    norm = straight
                        ? CurveKeyPoints.Polyline(target, FitTolerance * Scale(d), null, MaxOf(d))
                        : CurveKeyPoints.Spline(target, FitTolerance * Scale(d), null, MaxOf(d));
                }
            }
            else
            {
                switch (m.Shape)
                {
                    case CurveShape.Raw:
                        {
                            var along = CurveKeyPoints.Along(raw, pace);
                            norm = CurveKeyPoints.Polyline(raw, RawTolerance * Scale(d), along, MaxOf(d));
                            break;
                        }
                    case CurveShape.Segments:
                        {
                            var target = KeyTarget(raw);
                            var along = CurveKeyPoints.Along(target, pace);
                            norm = CurveKeyPoints.Polyline(target, KeyEpsilon * Scale(d), along, MaxOf(d));
                            break;
                        }
                    case CurveShape.Interpolated:
                        {
                            var target = KeyTarget(raw);
                            var along = CurveKeyPoints.Along(target, pace);
                            norm = CurveKeyPoints.Spline(target, KeyEpsilon * Scale(d), along, MaxOf(d));
                            break;
                        }
                    default:
                        {
                            var target = Uniform(Smooth(raw, m.Strength));
                            var along = CurveKeyPoints.Along(target, pace);
                            norm = CurveKeyPoints.Spline(target, SmoothTolerance * Scale(d), along, MaxOf(d));
                            break;
                        }
                }
            }
            var data = norm.Select(p => Denorm(p, frame)).Select(p => new Vec2(Back(p.X, m.XLog), Back(p.Y, m.YLog))).Where(Finite).ToList();
            var own = Thin(data, MaxPoints);
            // A lower detail never keeps more points than a higher one: the finer key points keep a tighter tolerance, so
            // when they are fewer they are taken.
            if (adaptive && d < CurveDetail.High && Points(m with { Detail = d + 1 }) is { } finer && finer.Count <= own.Count) return finer;
            return own;
        }

        /// <summary>The curve as drawn (data coordinates): the stored points, through the chart spline when <see cref="IsSpline"/>.</summary>
        public static IReadOnlyList<Vec2> Drawn(CurveSketchMemo m) => Drawn(m, Points(m));

        private static IReadOnlyList<Vec2> Drawn(CurveSketchMemo m, IReadOnlyList<Vec2> pts)
        {
            if (!IsSpline(m) || pts.Count < 3) return pts;
            var fit = pts.Select(p => new Vec2(To(p.X, m.XLog), To(p.Y, m.YLog))).ToList();
            return CurveKeyPoints.SplineCurve(fit).Select(p => new Vec2(Back(p.X, m.XLog), Back(p.Y, m.YLog))).ToList();
        }

        /// <summary>
        /// Cleans a trail (frame-normalised): duplicates dropped, resampled by arc length every <see cref="RawSpacing"/> (at most
        /// <see cref="MaxRaw"/> points, first and last kept) and the finest tremor removed with a 1-2-1 average.
        /// </summary>
        public static IReadOnlyList<Vec2> Clean(IReadOnlyList<Vec2> norm)
        {
            var pts = Dedupe(norm);
            if (pts.Count < 3) return pts;
            var length = SketchFit.PathLength(pts);
            var n = (int)Geo.Clamp(Math.Ceiling(length / RawSpacing) + 1, 8, MaxRaw);
            var r = SketchFit.Resample(pts, n);
            var list = new List<Vec2>(r.Count) { r[0] };
            for (var i = 1; i < r.Count - 1; i++) list.Add((r[i - 1] + r[i] * 2 + r[i + 1]) / 4);
            list.Add(r[r.Count - 1]);
            return list;
        }

        /// <summary>Smoothed curve (frame-normalised, dense): moving average of the strength's width, simplified, centripetal Catmull–Rom spline.</summary>
        public static IReadOnlyList<Vec2> Smooth(IReadOnlyList<Vec2> norm, int strength)
        {
            var s = Math.Max(1, Math.Min(3, strength));
            var r = Uniform(norm);
            if (r.Count < 3) return r;
            var avg = Average(r, StrengthWindow[s]);
            return SketchFit.CatmullRom(SketchFit.Simplify(avg, StrengthEpsilon[s]), 8, 0.5);
        }

        /// <summary>Key points (frame-normalised, medium detail, no pace): corners and turning points of the lightly averaged trail.</summary>
        public static IReadOnlyList<Vec2> KeyPoints(IReadOnlyList<Vec2> norm)
            => CurveKeyPoints.Polyline(KeyTarget(norm), KeyEpsilon, null, MaxOf(CurveDetail.Medium));

        /// <summary>The lightly averaged, evenly spaced trail the key points are picked from.</summary>
        private static IReadOnlyList<Vec2> KeyTarget(IReadOnlyList<Vec2> norm)
        {
            var r = Uniform(norm);
            return r.Count < 3 ? r : Average(r, KeyWindow);
        }

        private static IReadOnlyList<Vec2> Uniform(IReadOnlyList<Vec2> norm)
        {
            var pts = Dedupe(norm);
            if (pts.Count < 3) return pts;
            var n = (int)Geo.Clamp(Math.Ceiling(SketchFit.PathLength(pts) / WorkSpacing) + 1, 16, 1200);
            return SketchFit.Resample(pts, n);
        }

        /// <summary>Moving average with half-width <paramref name="w"/>; the window shrinks towards the ends, which stay in place.</summary>
        private static List<Vec2> Average(IReadOnlyList<Vec2> r, int w)
        {
            var n = r.Count;
            var avg = new List<Vec2>(n);
            for (var i = 0; i < n; i++)
            {
                var k = Math.Min(w, Math.Min(i, n - 1 - i));
                var s = Vec2.Zero;
                for (var j = -k; j <= k; j++) s += r[i + j];
                avg.Add(s / (2 * k + 1));
            }
            return avg;
        }

        private static List<Vec2> Dedupe(IReadOnlyList<Vec2> pts)
        {
            var list = new List<Vec2>(pts.Count);
            foreach (var p in pts)
                if (Finite(p) && (list.Count == 0 || list[list.Count - 1].DistanceTo(p) > 1e-9)) list.Add(p);
            return list;
        }

        /// <summary>At most <paramref name="max"/> points, evenly picked, first and last kept.</summary>
        public static IReadOnlyList<Vec2> Thin(IReadOnlyList<Vec2> pts, int max)
        {
            if (pts.Count <= max) return pts;
            var list = new List<Vec2>(max);
            for (var i = 0; i < max; i++) list.Add(pts[(int)Math.Round(i * (pts.Count - 1) / (double)(max - 1))]);
            return list;
        }

        private static bool Finite(Vec2 p) => Fmt.Finite(p.X) && Fmt.Finite(p.Y);

        private static Rect SafeFrame(Rect frame, IReadOnlyList<Vec2> trail)
        {
            if (frame.W > 0 && frame.H > 0 && Fmt.Finite(frame.W) && Fmt.Finite(frame.H) && Fmt.Finite(frame.X) && Fmt.Finite(frame.Y)) return frame;
            var pts = trail.Where(Finite).ToList();
            var b = pts.Count > 0 ? Rect.FromPoints(pts) : new Rect(0, 0, 1, 1);
            return new Rect(b.X, b.Y, b.W > 1e-12 ? b.W : 1, b.H > 1e-12 ? b.H : 1);
        }

        private static Vec2 Norm(Vec2 p, Rect f) => new((p.X - f.X) / f.W, (p.Y - f.Y) / f.H);
        private static Vec2 Denorm(Vec2 p, Rect f) => new(f.X + p.X * f.W, f.Y + p.Y * f.H);
        private static double To(double v, bool log) => log ? (v > 0 ? Math.Log10(v) : double.NaN) : v;
        private static double Back(double v, bool log) => log ? Math.Pow(10, v) : v;

        /// <summary>A stored number: linear values to 1/10 000 of the frame size, log values to six significant digits.</summary>
        private static double Round(double v, bool log, double span)
        {
            if (!Fmt.Finite(v)) return v;
            if (log) return Dragging.SnapSignificant(v, 6);
            var d = (int)Geo.Clamp(4 - Math.Floor(Math.Log10(Math.Max(span, 1e-300))), 0, 12);
            return Math.Round(v, d);
        }

        private static CurveSpec RoundSpec(CurveSpec s)
            => new(s.Kind, s.Values.Select(v => Fmt.Finite(v) && v != 0 ? Dragging.SnapSignificant(v, 6) : v).ToArray());

        /// <summary>A data value as text for the XY chart data (same precision as the stored trail).</summary>
        internal static string Text(double v, bool log, double span) => Round(v, log, span).ToString("R", CultureInfo.InvariantCulture);

        // ------------------------------------------------------------------ names and actions

        private static readonly (CurveShape Shape, string Id, string En, string De)[] Names =
        {
            (CurveShape.Smoothed, "smoothed", "Smoothed", "Geglättet"),
            (CurveShape.Interpolated, "interpolated", "Interpolated (spline)", "Interpoliert (Spline)"),
            (CurveShape.Fitted, "fitted", "Fitted function", "Ausgleichsfunktion"),
            (CurveShape.Segments, "segments", "Straight segments", "Gerade Abschnitte"),
            (CurveShape.Raw, "raw", "As drawn", "Wie gezeichnet"),
        };

        private static readonly I18nText[] StrengthNames = { new("Light", "Leicht"), new("Medium", "Mittel"), new("Strong", "Stark") };

        private static readonly (CurveDetail Detail, string Id, string En, string De)[] DetailNames =
        {
            (CurveDetail.Low, "low", "Low", "Gering"),
            (CurveDetail.Medium, "medium", "Medium (adaptive)", "Mittel (adaptiv)"),
            (CurveDetail.High, "high", "High", "Hoch"),
        };

        /// <summary>Text id of a detail ("low", "medium", "high").</summary>
        public static string Id(CurveDetail d) => DetailNames.First(n => n.Detail == Clamp(d)).Id;

        /// <summary>The detail with this text id (case-insensitive), or null.</summary>
        public static CurveDetail? DetailOf(string? id)
            => DetailNames.Where(n => string.Equals(n.Id, id?.Trim(), StringComparison.OrdinalIgnoreCase)).Select(n => (CurveDetail?)n.Detail).FirstOrDefault();

        /// <summary>Display name of a detail.</summary>
        public static I18nText Name(CurveDetail d)
        {
            var n = DetailNames.First(x => x.Detail == Clamp(d));
            return new I18nText(n.En, n.De);
        }

        /// <summary>Text id of a shape ("smoothed", "interpolated", "fitted", "segments", "raw").</summary>
        public static string Id(CurveShape s) => Names.First(n => n.Shape == s).Id;

        /// <summary>The shape with this text id (case-insensitive), or null.</summary>
        public static CurveShape? ShapeOf(string? id)
            => Names.Where(n => string.Equals(n.Id, id?.Trim(), StringComparison.OrdinalIgnoreCase)).Select(n => (CurveShape?)n.Shape).FirstOrDefault();

        /// <summary>The kind with this text id ("sine", "pt1" …; case-insensitive), or null.</summary>
        public static CurveKind? KindOf(string? id)
        {
            foreach (CurveKind k in Enum.GetValues(typeof(CurveKind)))
                if (string.Equals(CurveFunctions.Info(k).Id, id?.Trim(), StringComparison.OrdinalIgnoreCase)) return k;
            return null;
        }

        /// <summary>Display name of a shape; the fitted shape names its function ("Fitted function (Sine)").</summary>
        public static I18nText Name(CurveShape s, CurveSketchMemo? m = null)
        {
            var n = Names.First(x => x.Shape == s);
            if (s == CurveShape.Fitted && m?.FitSpec is { } spec)
            {
                var k = CurveFunctions.Info(spec.Kind).Name;
                return new I18nText(n.En + " (" + k.En + ")", n.De + " (" + k.Get(Lang.De) + ")");
            }
            return new I18nText(n.En, n.De);
        }

        /// <summary>The shapes offered for the memo (the fitted shape only with a fitted function).</summary>
        public static IReadOnlyList<CurveShape> Offered(CurveSketchMemo m) => Order.Where(s => s != CurveShape.Fitted || m.Fits.Count > 0).ToList();

        /// <summary>The shape after the current one in the "Curve shape" cycle.</summary>
        public static CurveShape NextShape(CurveSketchMemo m)
        {
            var list = Offered(m);
            var i = list.ToList().IndexOf(m.Shape);
            return list[(i + 1) % list.Count];
        }

        /// <summary>The fitted kind after the current one (ranked by fit error).</summary>
        public static CurveKind? NextFit(CurveSketchMemo m)
        {
            if (m.Fits.Count == 0) return null;
            var i = m.Fits.ToList().FindIndex(s => s.Kind == m.Fit);
            return m.Fits[(i + 1) % m.Fits.Count].Kind;
        }

        public static int NextStrength(CurveSketchMemo m) => Math.Max(1, Math.Min(3, m.Strength)) % 3 + 1;

        /// <summary>Choice keys of the part actions (option ids "key=value", e.g. "chart.curve.shape=fitted").</summary>
        public const string ShapeAction = "chart.curve.shape", FitAction = "chart.curve.fit", SmoothingAction = "chart.curve.smoothing",
            DetailAction = "chart.curve.detail";

        /// <summary>The option id of <paramref name="shape"/> in the "Curve shape" choice.</summary>
        public static string ShapeOption(CurveShape shape) => ShapeAction + ActionChoices.Separator + Id(shape);

        /// <summary>
        /// The part actions of a sketched curve, each a choice (maintainer 2026-11-25: choices are dropdowns, never cycles):
        /// "Curve shape" (smoothed, interpolated, fitted function, straight segments, as drawn), "Fitted function" (the ranked
        /// fits; fitted shape with two or more fits), "Smoothing" (light, medium, strong; smoothed shape) and "Detail" (low,
        /// medium = adaptive default, high: how many key points are kept; not for exact piecewise-linear functions). Every
        /// option has a small preview of the curve in that shape (the detail options with the kept points as dots). <paramref name="edit"/> turns a memo change into the object edit
        /// (recompute the points, store memo and points).
        /// </summary>
        internal static IEnumerable<ContextAction> Actions(CurveSketchMemo m, Func<Func<CurveSketchMemo, CurveSketchMemo>, Func<SketchObject, SketchObject>> edit)
        {
            // TODO(icons): pictograms for the curve shapes (smoothed, interpolated, fitted, segments, as drawn) would replace the previews.
            var shape = new ActionChoice(ShapeAction, new I18nText("Curve shape", "Kurvenform"));
            foreach (var s in Offered(m))
            {
                var to = s;
                yield return ActionChoices.Option(shape, Id(s), Name(s, m), "", s == m.Shape, edit(x => x with { Shape = to })) with
                {
                    Group = "curve", Preview = Preview(m with { Shape = to }),
                };
            }
            if (m.Shape == CurveShape.Fitted && m.Fits.Count > 1)
            {
                var fit = new ActionChoice(FitAction, new I18nText("Fitted function", "Ausgleichsfunktion"));
                foreach (var spec in m.Fits)
                {
                    var kind = spec.Kind;
                    yield return ActionChoices.Option(fit, CurveFunctions.Info(kind).Id, CurveFunctions.Info(kind).Name, "", kind == m.Fit, edit(x => x with { Fit = kind })) with
                    {
                        Group = "curve", Preview = Preview(m with { Fit = kind }),
                    };
                }
            }
            if (m.Shape == CurveShape.Smoothed)
            {
                var smoothing = new ActionChoice(SmoothingAction, new I18nText("Smoothing", "Glättung"));
                var current = Math.Max(1, Math.Min(3, m.Strength));
                for (var k = 1; k <= 3; k++)
                {
                    var to = k;
                    yield return ActionChoices.Option(smoothing, Fmt.I(k), StrengthNames[k - 1], "", k == current, edit(x => x with { Strength = to })) with
                    {
                        Group = "curve", Preview = Preview(m with { Strength = to }),
                    };
                }
            }
            if (HasDetail(m))
            {
                var detail = new ActionChoice(DetailAction, new I18nText("Detail", "Detailgrad"));
                foreach (var d in Details)
                {
                    var to = d;
                    yield return ActionChoices.Option(detail, Id(d), Name(d), "", d == Clamp(m.Detail), edit(x => x with { Detail = to })) with
                    {
                        Group = "curve", Preview = Preview(m with { Detail = to }, dots: true),
                    };
                }
            }
        }

        /// <summary>
        /// The curve of the memo as a small drawing for a dropdown option: the trail's frame scaled into 7 × 4.5 mm (centred at
        /// 0,0, y down), one thin path as drawn (spline or straight); with <paramref name="dots"/> the stored points as small
        /// dots; null when the memo gives no curve.
        /// </summary>
        public static IReadOnlyList<Primitive>? Preview(CurveSketchMemo m, bool dots = false)
        {
            var keys = Points(m);
            var pts = Drawn(m, keys);
            if (pts.Count < 2) return null;
            var f = m.Frame;
            double Fx(double v) => m.XLog ? Math.Log10(v) : v;
            double Fy(double v) => m.YLog ? Math.Log10(v) : v;
            const double W = 7, H = 4.5;
            var page = pts.Select(p => new Vec2((Fx(p.X) - f.X) / f.W * W - W / 2, H / 2 - (Fy(p.Y) - f.Y) / f.H * H)).Where(Finite).ToList();
            if (page.Count < 2) return null;
            var segs = new List<PathSeg> { PathSeg.Move(page[0]) };
            segs.AddRange(page.Skip(1).Select(PathSeg.Line));
            var list = new List<Primitive> { new PathPrim(segs, new Style { Stroke = Palette.Ink, StrokeWidth = 0.3, Role = LineRole.Visible }) };
            if (dots)
            {
                var dot = new Style { Stroke = "none", Fill = Fill.Solid("#1f5fbf"), Role = LineRole.Thin };
                foreach (var k in keys)
                {
                    var p = new Vec2((Fx(k.X) - f.X) / f.W * W - W / 2, H / 2 - (Fy(k.Y) - f.Y) / f.H * H);
                    if (Finite(p)) list.Add(Prim.Circle(p.X, p.Y, 0.22, dot));
                }
            }
            return list;
        }

        // ------------------------------------------------------------------ text form (XY chart data comment)

        /// <summary>Start of the comment line that carries a memo in XY chart data.</summary>
        public const string CommentTag = "# sketch";

        /// <summary>True if the (trimmed) line is a memo comment.</summary>
        public static bool IsComment(string line)
        {
            var t = line.Trim();
            return t.StartsWith(CommentTag, StringComparison.OrdinalIgnoreCase) && (t.Length == CommentTag.Length || char.IsWhiteSpace(t[CommentTag.Length]));
        }

        private static string N(double v) => v.ToString("R", CultureInfo.InvariantCulture);

        /// <summary>
        /// The memo as one comment line: <c># sketch shape=fitted detail=high fit=sine log=x frame=x,y,w,h fits=sine(…);pt1(…)
        /// pace=4456… raw=x,y x,y …</c> (invariant numbers; detail only when not medium; pace one digit per raw point, 0 = 0.6 …
        /// 9 = 1.5, only with time stamps; the data parser skips it).
        /// </summary>
        public static string ToComment(CurveSketchMemo m)
        {
            var parts = new List<string> { CommentTag, "shape=" + Id(m.Shape) };
            if (m.Strength != DefaultStrength) parts.Add("strength=" + Fmt.I(m.Strength));
            if (Clamp(m.Detail) != CurveDetail.Medium) parts.Add("detail=" + Id(m.Detail));
            if (m.Fit is CurveKind k) parts.Add("fit=" + CurveFunctions.Info(k).Id);
            if (m.XLog || m.YLog) parts.Add("log=" + (m.XLog ? "x" : "") + (m.YLog ? "y" : ""));
            parts.Add("frame=" + string.Join(",", new[] { m.Frame.X, m.Frame.Y, m.Frame.W, m.Frame.H }.Select(N)));
            if (m.Fits.Count > 0) parts.Add("fits=" + string.Join(";", m.Fits.Select(s => s.Format().Replace(" ", ""))));
            if (m.Pace.Count == m.Raw.Count && m.Pace.Count > 0) parts.Add("pace=" + PaceText(m.Pace));
            parts.Add("raw=" + string.Join(" ", m.Raw.Select(p => N(p.X) + "," + N(p.Y))));
            return string.Join(" ", parts);
        }

        /// <summary>Reads a memo comment line; false (null) for anything else or a memo without a usable trail.</summary>
        public static bool TryParseComment(string? line, out CurveSketchMemo? memo)
        {
            memo = null;
            if (line is null || !IsComment(line)) return false;
            var t = line.Trim().Substring(CommentTag.Length).Trim();
            var rawAt = t.IndexOf("raw=", StringComparison.OrdinalIgnoreCase);
            var head = rawAt >= 0 ? t.Substring(0, rawAt) : t;
            var rawText = rawAt >= 0 ? t.Substring(rawAt + 4) : "";
            var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var tok in head.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var eq = tok.IndexOf('=');
                if (eq > 0) fields[tok.Substring(0, eq)] = tok.Substring(eq + 1);
            }
            var raw = new List<Vec2>();
            foreach (var tok in rawText.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var xy = tok.Split(',');
                if (xy.Length == 2 && TryNum(xy[0], out var x) && TryNum(xy[1], out var y)) raw.Add(new Vec2(x, y));
            }
            var frame = default(Rect);
            if (fields.TryGetValue("frame", out var ft) && ft.Split(',') is { Length: 4 } fp && fp.All(f => TryNum(f, out _)))
            {
                var v = fp.Select(f => { TryNum(f, out var d); return d; }).ToArray();
                frame = new Rect(v[0], v[1], v[2], v[3]);
            }
            var log = fields.TryGetValue("log", out var lg) ? lg.ToLowerInvariant() : "";
            var m = new CurveSketchMemo
            {
                Shape = fields.TryGetValue("shape", out var sh) && ShapeOf(sh) is CurveShape s ? s : CurveShape.Smoothed,
                Strength = fields.TryGetValue("strength", out var st) && int.TryParse(st, NumberStyles.Integer, CultureInfo.InvariantCulture, out var si) ? Math.Max(1, Math.Min(3, si)) : DefaultStrength,
                Fit = fields.TryGetValue("fit", out var fk) ? KindOf(fk) : null,
                Fits = fields.TryGetValue("fits", out var fs) ? ParseSpecs(fs.Split(';')) : Array.Empty<CurveSpec>(),
                XLog = log.Contains("x"),
                YLog = log.Contains("y"),
                Frame = frame,
                Raw = raw,
                Detail = fields.TryGetValue("detail", out var dt) && DetailOf(dt) is CurveDetail d ? d : CurveDetail.Medium,
                Pace = fields.TryGetValue("pace", out var pc) ? ParsePace(pc, raw.Count) : Array.Empty<double>(),
            };
            if (!m.IsValid) return false;
            memo = m;
            return true;
        }

        /// <summary>The pace as digits, one per point (0 = 0.6 … 4 = 1 … 9 = 1.5).</summary>
        internal static string PaceText(IReadOnlyList<double> pace)
            => new(pace.Select(f => (char)('0' + CurveKeyPoints.Digit(f))).ToArray());

        /// <summary>The pace from its digits; empty unless there is exactly one digit per point of the trail.</summary>
        internal static IReadOnlyList<double> ParsePace(string? text, int count)
        {
            var t = (text ?? "").Trim();
            if (t.Length != count || count == 0 || !t.All(c => c >= '0' && c <= '9')) return Array.Empty<double>();
            return t.Select(c => CurveKeyPoints.Quantise(CurveKeyPoints.PaceMin + (c - '0') * CurveKeyPoints.PaceStep)).ToList();
        }

        /// <summary>Fitted functions from their text forms (unreadable ones are skipped, at most <see cref="MaxFits"/>).</summary>
        internal static IReadOnlyList<CurveSpec> ParseSpecs(IEnumerable<string?> texts)
        {
            var list = new List<CurveSpec>();
            foreach (var t in texts)
                if (list.Count < MaxFits && CurveFunctions.TryParse(t, out var spec, out _) && spec is not null) list.Add(spec);
            return list;
        }

        private static bool TryNum(string s, out double v)
            => double.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out v) && Fmt.Finite(v);
    }
}
