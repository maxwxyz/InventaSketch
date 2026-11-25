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
    /// What a sketched curve remembers: the chosen shape, the smoothing strength, the fitted functions (best first) and the
    /// lightly cleaned trail. Stored with the series (XY chart: a comment line <c># sketch …</c> before the data block; figure:
    /// the member <c>sketch</c> of the series), so <see cref="CurveShapes.Points"/> can recompute the curve for every shape.
    /// </summary>
    public sealed record CurveSketchMemo
    {
        public CurveShape Shape { get; init; } = CurveShape.Smoothed;
        /// <summary>Smoothing strength 1 (light), 2 (medium) or 3 (strong).</summary>
        public int Strength { get; init; } = CurveShapes.DefaultStrength;
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

        /// <summary>The spec of <see cref="Fit"/> (else the best fit), or null.</summary>
        public CurveSpec? FitSpec => Fits.FirstOrDefault(s => s.Kind == Fit) ?? Fits.FirstOrDefault();

        /// <summary>True if the memo can produce a curve (trail of two or more points and a frame).</summary>
        public bool IsValid => Raw.Count >= 2 && Frame.W > 0 && Frame.H > 0 && Fmt.Finite(Frame.W) && Fmt.Finite(Frame.H);
    }

    /// <summary>
    /// Curve shapes of sketched curves (charts v2): the trail is cleaned once when it is sketched (<see cref="Create"/>), every
    /// shape is computed from it (<see cref="Points"/>), and the part actions "Curve shape", "Fitted function" and "Smoothing"
    /// switch between them. All tolerances are relative to the plot frame (1 = plot size), so they act like screen distances.
    /// </summary>
    public static class CurveShapes
    {
        /// <summary>Most points of the kept trail.</summary>
        public const int MaxRaw = 96;
        /// <summary>Most points of a drawn curve.</summary>
        public const int MaxPoints = 120;
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
        /// <summary>Ramer–Douglas–Peucker tolerance of the key points (1.5 % of the plot).</summary>
        public const double KeyEpsilon = 0.015;

        /// <summary>Order of the "Curve shape" cycle.</summary>
        public static IReadOnlyList<CurveShape> Order { get; } = new[] { CurveShape.Smoothed, CurveShape.Interpolated, CurveShape.Fitted, CurveShape.Segments, CurveShape.Raw };

        // ------------------------------------------------------------------ creating and computing

        /// <summary>
        /// The memo of a newly sketched curve: <paramref name="trail"/> in fit space (log10 on log axes), the plot
        /// <paramref name="frame"/> in fit space and the recognition result of the trail. A recognised kind starts as
        /// <see cref="CurveShape.Fitted"/>, anything else as <see cref="CurveShape.Smoothed"/>.
        /// </summary>
        public static CurveSketchMemo Create(IReadOnlyList<Vec2> trail, Rect frame, bool xlog, bool ylog, SketchFitResult fit)
        {
            frame = SafeFrame(frame, trail);
            var raw = Clean(trail.Where(Finite).Select(p => Norm(p, frame)).ToList());
            var fits = fit.Ranking.Take(MaxFits).Select(c => RoundSpec(c.Spec)).ToList();
            return new CurveSketchMemo
            {
                Shape = fit.Kind is null ? CurveShape.Smoothed : CurveShape.Fitted,
                Fit = fit.Kind ?? (fits.Count > 0 ? fits[0].Kind : null),
                Fits = fits,
                XLog = xlog,
                YLog = ylog,
                Frame = frame,
                Raw = raw.Select(p => Denorm(p, frame)).Select(p => new Vec2(Round(Back(p.X, xlog), xlog, frame.W), Round(Back(p.Y, ylog), ylog, frame.H)))
                    .Where(Finite).ToList(),
            };
        }

        /// <summary>The curve of the memo's shape in data coordinates (at most <see cref="MaxPoints"/> points; empty for an invalid memo).</summary>
        public static IReadOnlyList<Vec2> Points(CurveSketchMemo m)
        {
            if (!m.IsValid) return Array.Empty<Vec2>();
            var frame = m.Frame;
            var fitRaw = m.Raw.Select(p => new Vec2(To(p.X, m.XLog), To(p.Y, m.YLog))).Where(Finite).ToList();
            if (fitRaw.Count < 2) return Array.Empty<Vec2>();
            var raw = fitRaw.Select(p => Norm(p, frame)).ToList();
            IReadOnlyList<Vec2> fitSpace;
            if (m.Shape == CurveShape.Fitted && m.FitSpec is { } spec)
                fitSpace = spec.Sample(fitRaw.Min(p => p.X), fitRaw.Max(p => p.X));
            else
            {
                var norm = m.Shape switch
                {
                    CurveShape.Raw => raw,
                    CurveShape.Segments => KeyPoints(raw),
                    CurveShape.Interpolated => Interpolate(KeyPoints(raw)),
                    _ => Smooth(raw, m.Strength),
                };
                fitSpace = norm.Select(p => Denorm(p, frame)).ToList();
            }
            var data = fitSpace.Select(p => new Vec2(Back(p.X, m.XLog), Back(p.Y, m.YLog))).Where(Finite).ToList();
            return Thin(data, MaxPoints);
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

        /// <summary>Smoothed shape (frame-normalised): moving average of the strength's width, simplified, centripetal Catmull–Rom spline.</summary>
        public static IReadOnlyList<Vec2> Smooth(IReadOnlyList<Vec2> norm, int strength)
        {
            var s = Math.Max(1, Math.Min(3, strength));
            var r = Uniform(norm);
            if (r.Count < 3) return r;
            var avg = Average(r, StrengthWindow[s]);
            return SketchFit.CatmullRom(SketchFit.Simplify(avg, StrengthEpsilon[s]), 8, 0.5);
        }

        /// <summary>Key points (frame-normalised): corners and turning points of the lightly averaged trail (Ramer–Douglas–Peucker).</summary>
        public static IReadOnlyList<Vec2> KeyPoints(IReadOnlyList<Vec2> norm)
        {
            var r = Uniform(norm);
            if (r.Count < 3) return r;
            return SketchFit.Simplify(Average(r, KeyWindow), KeyEpsilon);
        }

        /// <summary>Centripetal Catmull–Rom spline through the key points, as many steps per segment as fit into <see cref="MaxPoints"/> (2 … 10).</summary>
        public static IReadOnlyList<Vec2> Interpolate(IReadOnlyList<Vec2> keys)
        {
            if (keys.Count < 3) return keys;
            var steps = Math.Max(2, Math.Min(10, (MaxPoints - 1) / (keys.Count - 1)));
            return SketchFit.CatmullRom(keys, steps, 0.5);
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

        /// <summary>Part action ids.</summary>
        public const string ShapeAction = "chart.curve.shape", FitAction = "chart.curve.fit", SmoothingAction = "chart.curve.smoothing";

        /// <summary>
        /// The part actions of a sketched curve: "Curve shape: next" (always), "Fitted function: next kind" (fitted shape with
        /// two or more fits) and "Smoothing: next strength" (smoothed shape). <paramref name="edit"/> turns a memo change into
        /// the object edit (recompute the points, store memo and points).
        /// </summary>
        internal static IEnumerable<ContextAction> Actions(CurveSketchMemo m, Func<Func<CurveSketchMemo, CurveSketchMemo>, Func<SketchObject, SketchObject>> edit)
        {
            var next = NextShape(m);
            var shapeName = Name(next, m);
            yield return new ContextAction(ShapeAction, new I18nText("Curve shape: " + shapeName.En, "Kurvenform: " + shapeName.Get(Lang.De)), IconIds.Cycle,
                edit(x => x with { Shape = NextShape(x) })) { Group = "curve" };
            if (m.Shape == CurveShape.Fitted && m.Fits.Count > 1 && NextFit(m) is CurveKind nk)
            {
                var kn = CurveFunctions.Info(nk).Name;
                yield return new ContextAction(FitAction, new I18nText("Fitted function: " + kn.En, "Ausgleichsfunktion: " + kn.Get(Lang.De)), IconIds.Cycle,
                    edit(x => x with { Fit = NextFit(x) })) { Group = "curve" };
            }
            if (m.Shape == CurveShape.Smoothed)
            {
                var sn = StrengthNames[NextStrength(m) - 1];
                yield return new ContextAction(SmoothingAction, new I18nText("Smoothing: " + sn.En, "Glättung: " + sn.Get(Lang.De)), IconIds.Cycle,
                    edit(x => x with { Strength = NextStrength(x) })) { Group = "curve" };
            }
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
        /// The memo as one comment line: <c># sketch shape=fitted fit=sine log=x frame=x,y,w,h fits=sine(…);pt1(…) raw=x,y x,y …</c>
        /// (invariant numbers; the data parser skips it).
        /// </summary>
        public static string ToComment(CurveSketchMemo m)
        {
            var parts = new List<string> { CommentTag, "shape=" + Id(m.Shape) };
            if (m.Strength != DefaultStrength) parts.Add("strength=" + Fmt.I(m.Strength));
            if (m.Fit is CurveKind k) parts.Add("fit=" + CurveFunctions.Info(k).Id);
            if (m.XLog || m.YLog) parts.Add("log=" + (m.XLog ? "x" : "") + (m.YLog ? "y" : ""));
            parts.Add("frame=" + string.Join(",", new[] { m.Frame.X, m.Frame.Y, m.Frame.W, m.Frame.H }.Select(N)));
            if (m.Fits.Count > 0) parts.Add("fits=" + string.Join(";", m.Fits.Select(s => s.Format().Replace(" ", ""))));
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
            };
            if (!m.IsValid) return false;
            memo = m;
            return true;
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
