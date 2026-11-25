using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using InventaSketch.Core;

namespace InventaSketch.Modules.Charts
{
    /// <summary>Kinds of function generators (charts v2, stage 3).</summary>
    public enum CurveKind
    {
        /// <summary>y = const.</summary>
        Constant,
        /// <summary>Straight line through (x0, y0) with a slope.</summary>
        Ramp,
        /// <summary>Jump from y0 to y1 at x0 (right-continuous: the value at x0 is y1).</summary>
        Step,
        /// <summary>One rectangle pulse: high on [start, start + width), low elsewhere.</summary>
        Pulse,
        /// <summary>Periodic rectangle pulses: high for duty × period from start + k × period.</summary>
        PulseTrain,
        /// <summary>Linear rise from low to high over one period, then a vertical drop (low &gt; high gives a falling sawtooth).</summary>
        Sawtooth,
        /// <summary>Triangle wave: low at phase + k × period, high half a period later.</summary>
        Triangle,
        /// <summary>offset + amplitude × sin(2π (x − phase) / period).</summary>
        Sine,
        /// <summary>First-order lag (PT1): y0 before t0, then yinf + (y0 − yinf) e^(−(x − t0)/τ).</summary>
        Pt1,
        /// <summary>Exponential decay towards zero: y0 before t0, then y0 e^(−(x − t0)/τ).</summary>
        Decay,
        /// <summary>Step response of a second-order system (PT2) from y0 to yinf with damping ratio ζ and natural angular frequency ω.</summary>
        Pt2,
        /// <summary>Gaussian bump: offset + height e^(−(x − centre)² / (2 width²)).</summary>
        Gauss,
        /// <summary>Axis-parallel ellipse (closed, parametric); a circle when rx = ry.</summary>
        Ellipse,
    }

    /// <summary>How a parameter transforms when the axes are scaled/shifted (x' = ax + bx·x, y' = ay + by·y).</summary>
    internal enum CurveParamRole { XPosition, XLength, YPosition, YLength, Slope, InverseX, Ratio }

    /// <summary>
    /// A parameter of a function generator: name used in the text form, default, bilingual label and valid range. All parameters
    /// are unit-less (they take the units of the chart axes).
    /// </summary>
    public sealed record CurveParamDef(string Name, double Default, I18nText Label)
    {
        /// <summary>Smallest valid value (null = unbounded).</summary>
        public double? Min { get; init; }
        /// <summary>Largest valid value (null = unbounded).</summary>
        public double? Max { get; init; }
        /// <summary>True if <see cref="Min"/> itself is not valid (e.g. τ &gt; 0).</summary>
        public bool MinExclusive { get; init; }
        /// <summary>Omitted from the text form while it equals <see cref="Default"/> (neutral offsets and shifts).</summary>
        public bool Optional { get; init; }
        internal CurveParamRole Role { get; init; }

        /// <summary>True if <paramref name="v"/> is finite and within the valid range.</summary>
        public bool IsValid(double v)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) return false;
            if (Min is double min && (MinExclusive ? v <= min : v < min)) return false;
            return !(Max is double max && v > max);
        }

        /// <summary>Inspector parameter definition for hosts (key = <see cref="Name"/>).</summary>
        public ParamDef ToParamDef() => new(Name, Label, ParamKind.Number) { Default = Default, Min = Min, Max = Max };
    }

    /// <summary>Description of a function generator kind: id of the text form, bilingual name and parameters.</summary>
    public sealed record CurveKindInfo(CurveKind Kind, string Id, I18nText Name, IReadOnlyList<CurveParamDef> Params)
    {
        /// <summary>True for closed parametric curves (ellipse); they have no y(x).</summary>
        public bool Closed => Kind == CurveKind.Ellipse;

        /// <summary>Index of a parameter by name (case-insensitive, also aliases such as "center"); −1 if unknown.</summary>
        public int IndexOf(string name)
        {
            for (var i = 0; i < Params.Count; i++)
                if (string.Equals(Params[i].Name, name, StringComparison.OrdinalIgnoreCase)) return i;
            if (string.Equals(name, "center", StringComparison.OrdinalIgnoreCase)) return IndexOf("centre");
            return -1;
        }
    }

    /// <summary>
    /// Bilingual parse error of a curve specification. <see cref="Exception.Message"/> is the English text, <see cref="Text"/>
    /// carries both languages.
    /// </summary>
    public sealed class CurveSpecFormatException : FormatException
    {
        public CurveSpecFormatException(I18nText text) : base(text.En) { Text = text; }
        public I18nText Text { get; }
    }

    /// <summary>
    /// An immutable function generator with its parameter values, e.g. <c>pt1(y0=20, yinf=160, tau=12)</c>. Equality compares the
    /// kind and all values.
    /// </summary>
    public sealed class CurveSpec : IEquatable<CurveSpec>
    {
        private readonly double[] _values;

        internal CurveSpec(CurveKind kind, double[] values)
        {
            Kind = kind;
            _values = values;
        }

        public CurveKind Kind { get; }
        public CurveKindInfo Info => CurveFunctions.Info(Kind);
        /// <summary>Values in the order of <see cref="CurveKindInfo.Params"/>.</summary>
        public IReadOnlyList<double> Values => _values;

        /// <summary>Value of a parameter by name; throws <see cref="ArgumentException"/> for an unknown name.</summary>
        public double this[string name]
        {
            get
            {
                var i = Info.IndexOf(name);
                if (i < 0) throw new ArgumentException("Unknown parameter '" + name + "' of " + Info.Id + ".", nameof(name));
                return _values[i];
            }
        }

        /// <summary>Copy with one parameter changed (validated).</summary>
        public CurveSpec With(string name, double value)
        {
            var i = Info.IndexOf(name);
            if (i < 0) throw new ArgumentException("Unknown parameter '" + name + "' of " + Info.Id + ".", nameof(name));
            var v = (double[])_values.Clone();
            v[i] = value;
            return CurveFunctions.Create(Kind, v);
        }

        /// <summary>y at <paramref name="x"/>; NaN for closed curves.</summary>
        public double Evaluate(double x) => CurveFunctions.Evaluate(this, x);

        /// <summary>Polyline over [xMin, xMax] (closed curves ignore the range); see <see cref="CurveFunctions.Sample"/>.</summary>
        public IReadOnlyList<Vec2> Sample(double xMin, double xMax, int smoothSegments = 0) => CurveFunctions.Sample(this, xMin, xMax, smoothSegments);

        /// <summary>Compact text form, invariant culture: <c>kind(name=value, …)</c>.</summary>
        public string Format() => CurveFunctions.Format(this);

        public override string ToString() => Format();

        public bool Equals(CurveSpec? other) => other is not null && other.Kind == Kind && other._values.SequenceEqual(_values);
        public override bool Equals(object? obj) => obj is CurveSpec s && Equals(s);
        public override int GetHashCode()
        {
            var h = (int)Kind;
            foreach (var v in _values) h = h * 31 + v.GetHashCode();
            return h;
        }
    }

    /// <summary>
    /// Named function generators for charts (charts v2, stage 3): definitions, evaluation, sampling with exact corners and the
    /// compact text form with parse/format round trip. Pure and host-free.
    /// </summary>
    public static class CurveFunctions
    {
        /// <summary>Default number of segments of a smooth curve over the sampled range.</summary>
        public const int DefaultSmoothSegments = 160;
        /// <summary>Segments per period of a sine (and per oscillation of an underdamped PT2).</summary>
        public const int SegmentsPerPeriod = 64;
        /// <summary>Segments per standard deviation of a Gaussian bump (within ±5 σ of its centre).</summary>
        public const int SegmentsPerSigma = 12;
        /// <summary>Segments of a closed ellipse.</summary>
        public const int EllipseSegments = 96;
        /// <summary>Upper bound of the sampled points; above it a curve is sampled uniformly with this many points.</summary>
        public const int MaxPoints = 20000;

        private static CurveParamDef P(string name, double def, string en, string de, CurveParamRole role, double? min = null, bool exclusive = false, double? max = null, bool optional = false)
            => new(name, def, new I18nText(en, de)) { Min = min, MinExclusive = exclusive, Max = max, Optional = optional, Role = role };

        private static readonly CurveKindInfo[] _kinds =
        {
            new(CurveKind.Constant, "constant", new I18nText("Constant", "Konstante"), new[]
            {
                P("y", 1, "Value", "Wert", CurveParamRole.YPosition),
            }),
            new(CurveKind.Ramp, "ramp", new I18nText("Ramp (linear)", "Rampe (linear)"), new[]
            {
                P("x0", 0, "Reference x", "Bezugs-x", CurveParamRole.XPosition, optional: true),
                P("y0", 0, "Value at x0", "Wert bei x0", CurveParamRole.YPosition),
                P("slope", 1, "Slope", "Steigung", CurveParamRole.Slope),
            }),
            new(CurveKind.Step, "step", new I18nText("Step", "Sprung"), new[]
            {
                P("x0", 0, "Step position", "Sprungstelle", CurveParamRole.XPosition),
                P("y0", 0, "Value before", "Wert davor", CurveParamRole.YPosition),
                P("y1", 1, "Value after", "Wert danach", CurveParamRole.YPosition),
            }),
            new(CurveKind.Pulse, "pulse", new I18nText("Rectangle pulse", "Rechteckimpuls"), new[]
            {
                P("start", 0, "Start", "Beginn", CurveParamRole.XPosition),
                P("width", 1, "Width", "Breite", CurveParamRole.XLength, 0, true),
                P("low", 0, "Low level", "Grundwert", CurveParamRole.YPosition),
                P("high", 1, "High level", "Impulswert", CurveParamRole.YPosition),
            }),
            new(CurveKind.PulseTrain, "pulsetrain", new I18nText("Pulse train", "Pulsfolge"), new[]
            {
                P("start", 0, "First rising edge", "Erste steigende Flanke", CurveParamRole.XPosition, optional: true),
                P("period", 2, "Period", "Periode", CurveParamRole.XLength, 0, true),
                P("duty", 0.5, "Duty cycle (0 … 1)", "Tastgrad (0 … 1)", CurveParamRole.Ratio, 0, false, 1),
                P("low", 0, "Low level", "Grundwert", CurveParamRole.YPosition),
                P("high", 1, "High level", "Impulswert", CurveParamRole.YPosition),
            }),
            new(CurveKind.Sawtooth, "sawtooth", new I18nText("Sawtooth", "Sägezahn"), new[]
            {
                P("period", 1, "Period", "Periode", CurveParamRole.XLength, 0, true),
                P("low", 0, "Value at the start of a tooth", "Wert am Zahnanfang", CurveParamRole.YPosition),
                P("high", 1, "Value at the end of a tooth", "Wert am Zahnende", CurveParamRole.YPosition),
                P("phase", 0, "Start of a tooth (x)", "Zahnanfang (x)", CurveParamRole.XPosition, optional: true),
            }),
            new(CurveKind.Triangle, "triangle", new I18nText("Triangle wave", "Dreieckschwingung"), new[]
            {
                P("period", 1, "Period", "Periode", CurveParamRole.XLength, 0, true),
                P("low", 0, "Low value", "Tiefstwert", CurveParamRole.YPosition),
                P("high", 1, "High value", "Höchstwert", CurveParamRole.YPosition),
                P("phase", 0, "Position of a low point (x)", "Lage eines Tiefpunkts (x)", CurveParamRole.XPosition, optional: true),
            }),
            new(CurveKind.Sine, "sine", new I18nText("Sine", "Sinus"), new[]
            {
                P("amplitude", 1, "Amplitude", "Amplitude", CurveParamRole.YLength),
                P("period", 6.283185307179586, "Period", "Periode", CurveParamRole.XLength, 0, true),
                P("phase", 0, "Phase shift (x)", "Phasenverschiebung (x)", CurveParamRole.XPosition, optional: true),
                P("offset", 0, "Offset", "Gleichanteil", CurveParamRole.YPosition, optional: true),
            }),
            new(CurveKind.Pt1, "pt1", new I18nText("Exponential approach (PT1)", "Exponentielle Annäherung (PT1)"), new[]
            {
                P("y0", 0, "Initial value", "Anfangswert", CurveParamRole.YPosition),
                P("yinf", 1, "Final value (asymptote)", "Endwert (Asymptote)", CurveParamRole.YPosition),
                P("tau", 1, "Time constant τ", "Zeitkonstante τ", CurveParamRole.XLength, 0, true),
                P("t0", 0, "Start", "Beginn", CurveParamRole.XPosition, optional: true),
            }),
            new(CurveKind.Decay, "decay", new I18nText("Exponential decay", "Exponentieller Abfall"), new[]
            {
                P("y0", 1, "Initial value", "Anfangswert", CurveParamRole.YLength),
                P("tau", 1, "Time constant τ", "Zeitkonstante τ", CurveParamRole.XLength, 0, true),
                P("t0", 0, "Start", "Beginn", CurveParamRole.XPosition, optional: true),
            }),
            new(CurveKind.Pt2, "pt2", new I18nText("Second-order step response (PT2)", "Sprungantwort zweiter Ordnung (PT2)"), new[]
            {
                P("y0", 0, "Initial value", "Anfangswert", CurveParamRole.YPosition),
                P("yinf", 1, "Final value", "Endwert", CurveParamRole.YPosition),
                P("zeta", 0.3, "Damping ratio ζ", "Dämpfungsgrad ζ", CurveParamRole.Ratio, 0),
                P("omega", 1, "Natural angular frequency ω₀", "Kennkreisfrequenz ω₀", CurveParamRole.InverseX, 0, true),
                P("t0", 0, "Start", "Beginn", CurveParamRole.XPosition, optional: true),
            }),
            new(CurveKind.Gauss, "gauss", new I18nText("Gaussian bump", "Gauß-Glocke"), new[]
            {
                P("centre", 0, "Centre", "Mitte", CurveParamRole.XPosition),
                P("width", 1, "Width (standard deviation)", "Breite (Standardabweichung)", CurveParamRole.XLength, 0, true),
                P("height", 1, "Height", "Höhe", CurveParamRole.YLength),
                P("offset", 0, "Base line", "Grundlinie", CurveParamRole.YPosition, optional: true),
            }),
            new(CurveKind.Ellipse, "ellipse", new I18nText("Circle / ellipse", "Kreis / Ellipse"), new[]
            {
                P("cx", 0, "Centre x", "Mittelpunkt x", CurveParamRole.XPosition),
                P("cy", 0, "Centre y", "Mittelpunkt y", CurveParamRole.YPosition),
                P("rx", 1, "Radius in x", "Halbachse in x", CurveParamRole.XLength, 0, true),
                P("ry", 1, "Radius in y", "Halbachse in y", CurveParamRole.YLength, 0, true),
            }),
        };

        /// <summary>All kinds in menu order.</summary>
        public static IReadOnlyList<CurveKindInfo> Kinds => _kinds;

        public static CurveKindInfo Info(CurveKind kind) => _kinds[(int)kind];

        /// <summary>Kind by id of the text form (case-insensitive; aliases "line" = ramp, "circle" = ellipse); null if unknown.</summary>
        public static CurveKindInfo? Find(string id)
        {
            var t = (id ?? "").Trim();
            if (t.Equals("line", StringComparison.OrdinalIgnoreCase)) return Info(CurveKind.Ramp);
            if (t.Equals("circle", StringComparison.OrdinalIgnoreCase)) return Info(CurveKind.Ellipse);
            return _kinds.FirstOrDefault(k => k.Id.Equals(t, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>The kind with all parameters at their defaults.</summary>
        public static CurveSpec Default(CurveKind kind) => new(kind, Info(kind).Params.Select(p => p.Default).ToArray());

        /// <summary>
        /// Creates a validated spec from values in parameter order (missing trailing values take the defaults). Throws
        /// <see cref="ArgumentOutOfRangeException"/> for an invalid value and <see cref="ArgumentException"/> for too many values.
        /// </summary>
        public static CurveSpec Create(CurveKind kind, params double[] values)
        {
            var info = Info(kind);
            if (values.Length > info.Params.Count) throw new ArgumentException(info.Id + " takes at most " + info.Params.Count + " values.", nameof(values));
            var v = new double[info.Params.Count];
            for (var i = 0; i < v.Length; i++)
            {
                var p = info.Params[i];
                v[i] = i < values.Length ? values[i] : p.Default;
                if (!p.IsValid(v[i])) throw new ArgumentOutOfRangeException(nameof(values), v[i], info.Id + ": invalid value of " + p.Name + ".");
            }
            return new CurveSpec(kind, v);
        }

        /// <summary>Creates a validated spec from named values; unnamed parameters take their defaults.</summary>
        public static CurveSpec Create(CurveKind kind, IEnumerable<KeyValuePair<string, double>> named)
        {
            var info = Info(kind);
            var v = info.Params.Select(p => p.Default).ToArray();
            foreach (var kv in named)
            {
                var i = info.IndexOf(kv.Key);
                if (i < 0) throw new ArgumentException("Unknown parameter '" + kv.Key + "' of " + info.Id + ".", nameof(named));
                v[i] = kv.Value;
            }
            return Create(kind, v);
        }

        // ------------------------------------------------------------------ evaluation

        private static double Frac(double t) => t - Math.Floor(t);

        /// <summary>y of the spec at <paramref name="x"/>; NaN for closed curves (ellipse).</summary>
        public static double Evaluate(CurveSpec spec, double x)
        {
            var v = spec.Values;
            switch (spec.Kind)
            {
                case CurveKind.Constant: return v[0];
                case CurveKind.Ramp: return v[1] + v[2] * (x - v[0]);
                case CurveKind.Step: return x < v[0] ? v[1] : v[2];
                case CurveKind.Pulse: return x >= v[0] && x < v[0] + v[1] ? v[3] : v[2];
                case CurveKind.PulseTrain: return Frac((x - v[0]) / v[1]) < v[2] ? v[4] : v[3];
                case CurveKind.Sawtooth: return v[1] + (v[2] - v[1]) * Frac((x - v[3]) / v[0]);
                case CurveKind.Triangle:
                {
                    var f = Frac((x - v[3]) / v[0]);
                    return v[1] + (v[2] - v[1]) * (f < 0.5 ? 2 * f : 2 - 2 * f);
                }
                case CurveKind.Sine: return v[3] + v[0] * Math.Sin(2 * Math.PI * (x - v[2]) / v[1]);
                case CurveKind.Pt1: return x < v[3] ? v[0] : v[1] + (v[0] - v[1]) * Math.Exp(-(x - v[3]) / v[2]);
                case CurveKind.Decay: return x < v[2] ? v[0] : v[0] * Math.Exp(-(x - v[2]) / v[1]);
                case CurveKind.Pt2: return x < v[4] ? v[0] : v[0] + (v[1] - v[0]) * Pt2Unit(x - v[4], v[2], v[3]);
                case CurveKind.Gauss:
                {
                    var d = (x - v[0]) / v[1];
                    return v[3] + v[2] * Math.Exp(-0.5 * d * d);
                }
                default: return double.NaN;
            }
        }

        /// <summary>Unit step response of a second-order system (0 at t = 0, → 1).</summary>
        internal static double Pt2Unit(double t, double zeta, double omega)
        {
            if (t <= 0) return 0;
            if (Math.Abs(zeta - 1) < 1e-4) return 1 - (1 + omega * t) * Math.Exp(-omega * t);
            if (zeta < 1)
            {
                var root = Math.Sqrt(1 - zeta * zeta);
                var wd = omega * root;
                return 1 - Math.Exp(-zeta * omega * t) * (Math.Cos(wd * t) + zeta / root * Math.Sin(wd * t));
            }
            var r = Math.Sqrt(zeta * zeta - 1);
            double s1 = -omega * (zeta - r), s2 = -omega * (zeta + r);
            return 1 + (s2 * Math.Exp(s1 * t) - s1 * Math.Exp(s2 * t)) / (s1 - s2);
        }

        // ------------------------------------------------------------------ sampling

        /// <summary>
        /// Polyline of the spec over [xMin, xMax]. Piecewise-linear kinds (constant, ramp, step, pulses, sawtooth, triangle) give
        /// their exact vertices – a jump is two points with the same x. Smooth kinds are sampled uniformly with
        /// <paramref name="smoothSegments"/> segments (0 = <see cref="DefaultSmoothSegments"/>), refined for short periods
        /// (<see cref="SegmentsPerPeriod"/>) and narrow bumps (<see cref="SegmentsPerSigma"/>), with an exact corner at t0.
        /// A closed ellipse ignores the range and returns <see cref="EllipseSegments"/> + 1 points, the last equal to the first.
        /// </summary>
        public static IReadOnlyList<Vec2> Sample(CurveSpec spec, double xMin, double xMax, int smoothSegments = 0)
        {
            var v = spec.Values;
            if (spec.Kind == CurveKind.Ellipse)
            {
                var n = Math.Max(8, smoothSegments > 0 ? smoothSegments : EllipseSegments);
                var pts = new List<Vec2>(n + 1);
                for (var i = 0; i < n; i++)
                {
                    var a = 2 * Math.PI * i / n;
                    pts.Add(new Vec2(v[0] + v[2] * Math.Cos(a), v[1] + v[3] * Math.Sin(a)));
                }
                pts.Add(pts[0]);
                return pts;
            }
            if (!(xMax > xMin) || double.IsInfinity(xMax - xMin))
            {
                var x = double.IsNaN(xMin) ? 0 : xMin;
                return new[] { new Vec2(x, Evaluate(spec, x)) };
            }
            var segs = smoothSegments > 0 ? smoothSegments : DefaultSmoothSegments;
            var range = xMax - xMin;
            switch (spec.Kind)
            {
                case CurveKind.Constant:
                case CurveKind.Ramp:
                    return new[] { new Vec2(xMin, Evaluate(spec, xMin)), new Vec2(xMax, Evaluate(spec, xMax)) };
                case CurveKind.Step:
                    return Jumps(xMin, xMax, new[] { v[0] }, spec);
                case CurveKind.Pulse:
                    return Jumps(xMin, xMax, new[] { v[0], v[0] + v[1] }, spec);
                case CurveKind.PulseTrain:
                {
                    if (range / v[1] * 2 + 4 > MaxPoints / 2) return Uniform(spec, xMin, xMax, MaxPoints - 1);
                    var edges = new List<double>();
                    for (var k = Math.Floor((xMin - v[0]) / v[1]) - 1; v[0] + k * v[1] <= xMax; k++)
                    {
                        edges.Add(v[0] + k * v[1]);
                        if (v[2] > 0 && v[2] < 1) edges.Add(v[0] + (k + v[2]) * v[1]);
                    }
                    return Jumps(xMin, xMax, edges, spec);
                }
                case CurveKind.Sawtooth:
                {
                    if (range / v[0] * 2 + 4 > MaxPoints) return Uniform(spec, xMin, xMax, MaxPoints - 1);
                    // Positions in periods, snapped to whole periods within rounding, so a range that starts or ends on a tooth
                    // boundary starts at the bottom (right limit) and ends at the top (left limit) of a tooth.
                    var t0 = Snap((xMin - v[3]) / v[0]);
                    var t1 = Snap((xMax - v[3]) / v[0]);
                    var pts = new List<Vec2> { new(xMin, v[1] + (v[2] - v[1]) * (t0 - Math.Floor(t0))) };
                    for (var k = Math.Floor(t0) + 1; k < t1; k++)
                    {
                        var xb = v[3] + k * v[0];
                        pts.Add(new Vec2(xb, v[2]));
                        pts.Add(new Vec2(xb, v[1]));
                    }
                    pts.Add(new Vec2(xMax, v[1] + (v[2] - v[1]) * (t1 - Math.Ceiling(t1) + 1)));
                    return pts;
                }
                case CurveKind.Triangle:
                {
                    var half = v[0] / 2;
                    if (range / half + 4 > MaxPoints) return Uniform(spec, xMin, xMax, MaxPoints - 1);
                    var t0 = Snap((xMin - v[3]) / half);
                    var t1 = Snap((xMax - v[3]) / half);
                    var pts = new List<Vec2> { new(xMin, Evaluate(spec, xMin)) };
                    for (var k = Math.Floor(t0) + 1; k < t1; k++)
                        pts.Add(new Vec2(v[3] + k * half, ((long)k & 1) == 0 ? v[1] : v[2]));
                    pts.Add(new Vec2(xMax, Evaluate(spec, xMax)));
                    return pts;
                }
                case CurveKind.Sine:
                    return Uniform(spec, xMin, xMax, Math.Max(segs, (int)Math.Ceiling(range / v[1] * SegmentsPerPeriod)));
                case CurveKind.Pt1:
                    return WithCorner(spec, xMin, xMax, v[3], segs);
                case CurveKind.Decay:
                    return WithCorner(spec, xMin, xMax, v[2], segs);
                case CurveKind.Pt2:
                {
                    var n = segs;
                    if (v[2] < 1)
                    {
                        var wd = v[3] * Math.Sqrt(1 - v[2] * v[2]);
                        n = Math.Max(n, (int)Math.Ceiling(range * wd / (2 * Math.PI) * SegmentsPerPeriod));
                    }
                    else n = Math.Max(n, (int)Math.Ceiling(range * v[3] * 8));
                    return WithCorner(spec, xMin, xMax, v[4], n);
                }
                case CurveKind.Gauss:
                {
                    // Uniform background plus a dense band of ±5 σ around the centre.
                    var xs = new List<double>();
                    for (var i = 0; i <= segs; i++) xs.Add(xMin + range * i / segs);
                    double lo = Math.Max(xMin, v[0] - 5 * v[1]), hi = Math.Min(xMax, v[0] + 5 * v[1]);
                    if (hi > lo)
                    {
                        var n = (int)Math.Min(MaxPoints / 2, Math.Ceiling((hi - lo) / v[1] * SegmentsPerSigma));
                        for (var i = 0; i <= n; i++) xs.Add(lo + (hi - lo) * i / Math.Max(1, n));
                    }
                    return Distinct(xs).Select(x => new Vec2(x, Evaluate(spec, x))).ToList();
                }
                default:
                    return Uniform(spec, xMin, xMax, segs);
            }
        }

        /// <summary>Rounds a position in periods to the nearest whole number when it is within rounding error of it.</summary>
        private static double Snap(double t)
        {
            var r = Math.Round(t);
            return Math.Abs(t - r) <= 1e-9 * Math.Max(1, Math.Abs(t)) ? r : t;
        }

        private static IReadOnlyList<Vec2> Uniform(CurveSpec spec, double xMin, double xMax, int segments)
        {
            segments = Math.Max(1, Math.Min(MaxPoints - 1, segments));
            var pts = new List<Vec2>(segments + 1);
            for (var i = 0; i <= segments; i++)
            {
                var x = i == segments ? xMax : xMin + (xMax - xMin) * i / segments;
                pts.Add(new Vec2(x, Evaluate(spec, x)));
            }
            return pts;
        }

        /// <summary>Uniform sampling with an exact vertex at <paramref name="corner"/> (start of PT1/PT2/decay).</summary>
        private static IReadOnlyList<Vec2> WithCorner(CurveSpec spec, double xMin, double xMax, double corner, int segments)
        {
            segments = Math.Max(1, Math.Min(MaxPoints - 2, segments));
            var xs = new List<double>(segments + 2);
            if (corner > xMin) xs.Add(xMin);
            if (corner > xMin && corner < xMax) xs.Add(corner);
            double from = Math.Max(xMin, corner);
            if (from < xMax)
            {
                // The flat part before the corner needs no samples; the smooth part gets all of them.
                for (var i = 0; i <= segments; i++) xs.Add(i == segments ? xMax : from + (xMax - from) * i / segments);
            }
            else xs.Add(xMax);
            return Distinct(xs).Select(x => new Vec2(x, Evaluate(spec, x))).ToList();
        }

        private static List<double> Distinct(List<double> xs)
        {
            xs.Sort();
            var r = new List<double>(xs.Count);
            foreach (var x in xs)
                if (r.Count == 0 || x - r[r.Count - 1] > 1e-12 * Math.Max(1, Math.Abs(x))) r.Add(x);
            return r;
        }

        /// <summary>Piecewise-constant kinds: horizontal runs with vertical jumps at the given edges.</summary>
        private static IReadOnlyList<Vec2> Jumps(double xMin, double xMax, IEnumerable<double> edges, CurveSpec spec)
        {
            // Levels are evaluated between the edges, so rounding at an edge (frac ≈ 0.99999) cannot drop a jump.
            var inner = edges.Where(e => e > xMin && e < xMax).OrderBy(e => e).ToList();
            double Level(int i) => Evaluate(spec, ((i == 0 ? xMin : inner[i - 1]) + (i == inner.Count ? xMax : inner[i])) / 2);
            var level = Level(0);
            var pts = new List<Vec2> { new(xMin, level) };
            for (var i = 0; i < inner.Count; i++)
            {
                var after = Level(i + 1);
                if (after == level) continue;
                pts.Add(new Vec2(inner[i], level));
                pts.Add(new Vec2(inner[i], after));
                level = after;
            }
            pts.Add(new Vec2(xMax, level));
            return pts;
        }

        // ------------------------------------------------------------------ axis transform (used by the sketch fit)

        /// <summary>
        /// The same curve after mapping x' = ax + bx·x and y' = ay + by·y (bx, by &gt; 0). Decay is not affine in y (its asymptote
        /// is zero) and is mapped only when <paramref name="ay"/> is 0.
        /// </summary>
        internal static CurveSpec MapAffine(CurveSpec spec, double ax, double bx, double ay, double by)
        {
            var info = spec.Info;
            var v = new double[spec.Values.Count];
            for (var i = 0; i < v.Length; i++)
            {
                var x = spec.Values[i];
                v[i] = info.Params[i].Role switch
                {
                    CurveParamRole.XPosition => ax + bx * x,
                    CurveParamRole.XLength => bx * x,
                    CurveParamRole.YPosition => ay + by * x,
                    CurveParamRole.YLength => by * x,
                    CurveParamRole.Slope => by / bx * x,
                    CurveParamRole.InverseX => x / bx,
                    _ => x,
                };
            }
            return new CurveSpec(spec.Kind, v);
        }

        // ------------------------------------------------------------------ text form

        /// <summary>
        /// Compact text form, invariant culture, shortest round-trip numbers: <c>pt1(y0=20, yinf=160, tau=12)</c>. Optional
        /// parameters (neutral shifts and offsets) are left out while they equal their default.
        /// </summary>
        public static string Format(CurveSpec spec)
        {
            var info = spec.Info;
            var sb = new StringBuilder(info.Id).Append('(');
            var first = true;
            for (var i = 0; i < info.Params.Count; i++)
            {
                var p = info.Params[i];
                var val = spec.Values[i];
                if (p.Optional && val == p.Default) continue;
                if (!first) sb.Append(", ");
                first = false;
                sb.Append(p.Name).Append('=').Append(Num(val));
            }
            return sb.Append(')').ToString();
        }

        private static string Num(double v)
        {
            if (v == 0) return "0";
            // "R" is the shortest round-trip form on .NET Core; on .NET Framework it can miss by one bit, then use 17 digits.
            var s = v.ToString("R", CultureInfo.InvariantCulture);
            return double.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture) == v ? s : v.ToString("G17", CultureInfo.InvariantCulture);
        }

        /// <summary>Parses the text form; throws <see cref="CurveSpecFormatException"/> (a <see cref="FormatException"/>) with a bilingual message.</summary>
        public static CurveSpec Parse(string text)
        {
            if (TryParse(text, out var spec, out var error)) return spec!;
            throw new CurveSpecFormatException(error!);
        }

        /// <summary>
        /// Parses <c>kind(a=1, b=2.5)</c>: kind ids and names are case-insensitive, values may also be given positionally in
        /// parameter order before named ones, missing parameters take their defaults. Numbers use the invariant culture; a German
        /// decimal comma is accepted where it is unambiguous: when the arguments are separated by semicolons, or inside a named
        /// value when the comma stands directly between two digits (<c>tau=1,5</c>). "circle(cx, cy, r)" gives an ellipse with
        /// rx = ry = r.
        /// </summary>
        public static bool TryParse(string? text, out CurveSpec? spec, out I18nText? error)
        {
            spec = null;
            error = null;
            var t = (text ?? "").Trim();
            if (t.Length == 0) { error = new I18nText("Empty curve function.", "Leere Kurvenfunktion."); return false; }
            int open = t.IndexOf('('), close = t.LastIndexOf(')');
            var name = open < 0 ? t : t.Substring(0, open).Trim();
            if (open < 0 && close < 0)
            {
                // A bare id ("sine") is allowed: all defaults.
            }
            else if (open < 0 || close < open || close != t.Length - 1)
            {
                error = new I18nText("Expected kind(name=value, …): “" + t + "”.", "Erwartet: Art(Name=Wert, …): „" + t + "“.");
                return false;
            }
            var info = Find(name);
            if (info is null)
            {
                error = new I18nText("Unknown curve function “" + name + "”. Known: " + string.Join(", ", _kinds.Select(k => k.Id)) + ".",
                    "Unbekannte Kurvenfunktion „" + name + "“. Bekannt: " + string.Join(", ", _kinds.Select(k => k.Id)) + ".");
                return false;
            }
            var circle = name.Equals("circle", StringComparison.OrdinalIgnoreCase);
            var body = open < 0 ? "" : t.Substring(open + 1, close - open - 1);
            var args = SplitArgs(body);
            var values = info.Params.Select(p => p.Default).ToArray();
            var set = new bool[values.Length];
            var namedSeen = false;
            var position = 0;
            double? radius = null;
            foreach (var raw in args)
            {
                var a = raw.Trim();
                if (a.Length == 0)
                {
                    if (args.Count == 1) break;
                    error = new I18nText("Empty argument in “" + t + "”.", "Leeres Argument in „" + t + "“.");
                    return false;
                }
                var eq = a.IndexOf('=');
                string? key = eq < 0 ? null : a.Substring(0, eq).Trim();
                var valueText = eq < 0 ? a : a.Substring(eq + 1).Trim();
                if (!TryNumber(valueText, out var value))
                {
                    error = new I18nText("Not a number: “" + valueText + "” in “" + t + "”.", "Keine Zahl: „" + valueText + "“ in „" + t + "“.");
                    return false;
                }
                int index;
                if (key is null)
                {
                    if (namedSeen)
                    {
                        error = new I18nText("A value without a name follows a named value in “" + t + "”.",
                            "Ein Wert ohne Namen folgt auf einen benannten Wert in „" + t + "“.");
                        return false;
                    }
                    if (circle && position == 2) { radius = value; position++; continue; }
                    index = position++;
                    if (index >= values.Length || (circle && index > 2))
                    {
                        error = new I18nText("Too many values for " + info.Id + " (at most " + (circle ? 3 : values.Length) + ").",
                            "Zu viele Werte für " + info.Id + " (höchstens " + (circle ? 3 : values.Length) + ").");
                        return false;
                    }
                }
                else
                {
                    namedSeen = true;
                    if (circle && key.Equals("r", StringComparison.OrdinalIgnoreCase)) { radius = value; continue; }
                    index = info.IndexOf(key);
                    if (index < 0)
                    {
                        var names = string.Join(", ", info.Params.Select(p => p.Name));
                        error = new I18nText("Unknown parameter “" + key + "” of " + info.Id + " (known: " + names + ").",
                            "Unbekannter Parameter „" + key + "“ von " + info.Id + " (bekannt: " + names + ").");
                        return false;
                    }
                    if (set[index])
                    {
                        error = new I18nText("Parameter “" + key + "” is given twice.", "Parameter „" + key + "“ ist doppelt angegeben.");
                        return false;
                    }
                }
                values[index] = value;
                set[index] = true;
            }
            if (radius is double r)
            {
                if (set[2] || set[3])
                {
                    error = new I18nText("A circle takes r, not rx/ry.", "Ein Kreis hat r, nicht rx/ry.");
                    return false;
                }
                values[2] = values[3] = r;
            }
            for (var i = 0; i < values.Length; i++)
            {
                var p = info.Params[i];
                if (p.IsValid(values[i])) continue;
                var range = Range(p);
                error = new I18nText("Invalid value of " + p.Name + " in " + info.Id + ": " + Num(values[i]) + " (" + range.En + ").",
                    "Ungültiger Wert von " + p.Name + " in " + info.Id + ": " + Num(values[i]).Replace('.', ',') + " (" + range.De + ").");
                return false;
            }
            spec = new CurveSpec(info.Kind, values);
            return true;
        }

        private static I18nText Range(CurveParamDef p)
        {
            if (p.Min is double min && p.Max is double max) return new I18nText("allowed " + Num(min) + " … " + Num(max), "erlaubt " + Num(min).Replace('.', ',') + " … " + Num(max).Replace('.', ','));
            if (p.Min is double m) return p.MinExclusive
                ? new I18nText("must be greater than " + Num(m), "muss größer als " + Num(m).Replace('.', ',') + " sein")
                : new I18nText("must be at least " + Num(m), "muss mindestens " + Num(m).Replace('.', ',') + " sein");
            return new I18nText("must be a finite number", "muss eine endliche Zahl sein");
        }

        /// <summary>Splits the argument list; see <see cref="TryParse"/> for the decimal comma rules.</summary>
        private static List<string> SplitArgs(string body)
        {
            if (body.IndexOf(';') >= 0) return body.Split(';').Select(a => a.Replace(',', '.')).ToList();
            var parts = body.Split(',').ToList();
            var named = parts.Any(p => p.IndexOf('=') >= 0);
            if (!named) return parts;
            var merged = new List<string>();
            foreach (var p in parts)
            {
                // "tau=1,5": the piece "5" has no name, follows a named piece ending in a digit and starts with a digit.
                if (merged.Count > 0 && p.Length > 0 && char.IsDigit(p[0]) && p.IndexOf('=') < 0)
                {
                    var prev = merged[merged.Count - 1];
                    if (prev.Length > 0 && char.IsDigit(prev[prev.Length - 1]) && prev.IndexOf('=') >= 0 && prev.IndexOf('.') < 0 && p.All(char.IsDigit))
                    {
                        merged[merged.Count - 1] = prev + "." + p;
                        continue;
                    }
                }
                merged.Add(p);
            }
            return merged;
        }

        private static bool TryNumber(string s, out double v)
        {
            var t = s.Trim().Replace('−', '-');
            return double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out v) && !double.IsNaN(v) && !double.IsInfinity(v);
        }
    }
}
