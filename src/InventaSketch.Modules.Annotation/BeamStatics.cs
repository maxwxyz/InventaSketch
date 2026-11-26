using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Annotation
{
    /// <summary>Support of a straight beam: pin (hinge), roller or fixed (clamped).</summary>
    public enum BeamSupportKind { Pin, Roller, Fixed }

    /// <summary>A support at position <paramref name="X"/> (distance from the left end of the beam).</summary>
    public sealed record BeamSupport(BeamSupportKind Kind, double X);

    /// <summary>
    /// A point load at <paramref name="X"/>; <paramref name="F"/> is the transverse component, positive downwards (towards the
    /// beam's lower side); <see cref="Axial"/> the component along the beam, positive towards its right end.
    /// </summary>
    public sealed record BeamPointLoad(double X, double F)
    {
        /// <summary>Component along the beam axis (positive towards the right end); gives the normal force.</summary>
        public double Axial { get; init; }

        /// <summary>
        /// An inclined point load of magnitude <paramref name="force"/> whose direction makes the angle <paramref name="angle"/>
        /// (degrees) with the beam axis, measured clockwise from the direction to the right end: 90° points straight down,
        /// 45° down and to the right, 135° down and to the left, 0° along the axis to the right.
        /// </summary>
        public static BeamPointLoad Inclined(double x, double force, double angle)
        {
            var a = angle * Math.PI / 180;
            double Clean(double v) => Math.Abs(v) < 1e-12 * Math.Max(1, Math.Abs(force)) ? 0 : v;
            return new BeamPointLoad(x, Clean(force * Math.Sin(a))) { Axial = Clean(force * Math.Cos(a)) };
        }
    }

    /// <summary>
    /// A line load from <paramref name="X0"/> to <paramref name="X1"/> (force per length, positive downwards):
    /// <paramref name="Q"/> at <paramref name="X0"/> and <paramref name="Q1"/> at <paramref name="X1"/>, linear in between
    /// (trapezoidal; a triangle when one end is zero). Without <paramref name="Q1"/> the load is uniform.
    /// </summary>
    public sealed record BeamLineLoad(double X0, double X1, double Q, double? Q1 = null)
    {
        /// <summary>Intensity at <see cref="X1"/>.</summary>
        public double QEnd => Q1 ?? Q;

        /// <summary>Resultant (total force) of the load.</summary>
        public double Resultant => (Q + QEnd) / 2 * Math.Abs(X1 - X0);
    }

    /// <summary>
    /// An applied moment (couple) at <paramref name="X"/>; <paramref name="M"/> is positive clockwise as drawn (beam axis to
    /// the right, lower side down). A clockwise couple makes the bending moment jump up by <paramref name="M"/> from left to right.
    /// </summary>
    public sealed record BeamCouple(double X, double M);

    /// <summary>
    /// A straight beam of <paramref name="Length"/> with supports, point loads and line loads (transverse loads only), and
    /// optionally applied couples and internal hinges (Gerber beam; the bending moment is zero at a hinge).
    /// </summary>
    public sealed record BeamModel(double Length, IReadOnlyList<BeamSupport> Supports, IReadOnlyList<BeamPointLoad> PointLoads, IReadOnlyList<BeamLineLoad> LineLoads)
    {
        /// <summary>Applied couples.</summary>
        public IReadOnlyList<BeamCouple> Couples { get; init; } = Array.Empty<BeamCouple>();

        /// <summary>Positions of internal hinges (moment-free joints); hinges at the ends of the beam have no effect.</summary>
        public IReadOnlyList<double> Hinges { get; init; } = Array.Empty<double>();

        /// <summary>Simply supported beam (pin left, roller right) with the given loads.</summary>
        public static BeamModel Simple(double length, IEnumerable<BeamPointLoad>? points = null, IEnumerable<BeamLineLoad>? lines = null)
            => new(length, new[] { new BeamSupport(BeamSupportKind.Pin, 0), new BeamSupport(BeamSupportKind.Roller, length) },
                (points ?? Enumerable.Empty<BeamPointLoad>()).ToList(), (lines ?? Enumerable.Empty<BeamLineLoad>()).ToList());

        /// <summary>Cantilever clamped at the left end (or the right end) with the given loads.</summary>
        public static BeamModel Cantilever(double length, bool fixedRight = false, IEnumerable<BeamPointLoad>? points = null, IEnumerable<BeamLineLoad>? lines = null)
            => new(length, new[] { new BeamSupport(BeamSupportKind.Fixed, fixedRight ? length : 0) },
                (points ?? Enumerable.Empty<BeamPointLoad>()).ToList(), (lines ?? Enumerable.Empty<BeamLineLoad>()).ToList());
    }

    /// <summary>
    /// Support reaction: <paramref name="Force"/> positive upwards (against downward loads); <paramref name="Moment"/> is the
    /// clamping moment of a fixed support, given as the bending moment in the beam at the support (negative = hogging).
    /// </summary>
    public sealed record BeamReaction(BeamSupport Support, double Force, double Moment)
    {
        /// <summary>Horizontal reaction (along the beam, positive towards the right end); only pins and fixed supports take one.</summary>
        public double Horizontal { get; init; }
    }

    /// <summary>An extreme value of a diagram (signed) and where it occurs.</summary>
    public sealed record BeamExtreme(double X, double Value);

    /// <summary>Static classification of a beam.</summary>
    public enum BeamStatus
    {
        /// <summary>Statically determinate: the equilibrium conditions (and the hinge conditions) give all reactions.</summary>
        Determinate,
        /// <summary>
        /// More reactions than conditions: statics alone cannot give them. They are solved with the stiffness of a prismatic
        /// beam (constant EI, and constant EA for the normal force), on which the reactions do not depend in size.
        /// </summary>
        Indeterminate,
        /// <summary>Not enough (or ill-placed) supports: the beam or a part of it can move (a mechanism).</summary>
        Unstable,
        /// <summary>No positive length.</summary>
        Invalid,
    }

    /// <summary>
    /// Result of <see cref="BeamStatics.Check"/>: the status and its degree (degree of indeterminacy for
    /// <see cref="BeamStatus.Indeterminate"/>, number of free motions for <see cref="BeamStatus.Unstable"/>, else 0), the
    /// number of unknown reactions and of conditions (2 equilibrium conditions plus one per internal hinge, plus the
    /// horizontal reactions and the horizontal equilibrium when loads act along the beam).
    /// </summary>
    public sealed record BeamCheck(BeamStatus Status, int Degree, int Unknowns, int Conditions)
    {
        public bool IsDeterminate => Status == BeamStatus.Determinate;
    }

    /// <summary>
    /// Normal force, shear force, bending moment and deflection of a solved beam. Sign convention (common textbook use): the
    /// shear force is the sum of the upward forces left of the section; the bending moment is positive when it sags the beam
    /// (tension at the lower side); the normal force is positive in tension; the deflection is positive downwards.
    /// </summary>
    public sealed class BeamSolution
    {
        private readonly BeamLoads _loads;
        private readonly BeamFrame? _frame;
        private readonly List<double> _vertices = new(); // where the shear force passes zero inside a stretch
        private readonly List<double> _wVertices = new(); // where the slope of the deflection line passes zero
        private double[][]? _cubics; // bending moment per stretch between frame nodes as a cubic in t ∈ [0, 1]
        private BeamExtreme? _maxDeflection;

        internal BeamSolution(BeamModel model, IReadOnlyList<BeamReaction> reactions, BeamLoads loads, BeamFrame? frame = null)
        {
            Model = model;
            Reactions = reactions;
            _loads = loads;
            _frame = frame;
            Hinges = BeamStatics.InnerHinges(model);
            Breaks = BuildBreaks();
            (MaxShear, MaxMoment) = Extremes();
            HasNormalForce = loads.Axial.Any(a => Math.Abs(a.H) > 0);
            MaxNormal = NormalExtreme();
        }

        public BeamModel Model { get; }
        public IReadOnlyList<BeamReaction> Reactions { get; internal set; }

        /// <summary>The static classification the solution was made for (determinate or indeterminate with its degree).</summary>
        public BeamCheck Check { get; internal set; } = new(BeamStatus.Determinate, 0, 0, 0);

        /// <summary>True if loads act along the beam (the normal force is not zero everywhere).</summary>
        public bool HasNormalForce { get; }

        /// <summary>Largest normal force by magnitude (signed, tension positive), the first one if several are equal.</summary>
        public BeamExtreme MaxNormal { get; }

        /// <summary>Normal force (tension positive) just right of <paramref name="x"/> (or just left with <paramref name="left"/>).</summary>
        public double Normal(double x, bool left = false) => _loads.Normal(x, left);

        /// <summary>The normal force diagram as (x, N) points (constant between loads, jumps as two points at the same x).</summary>
        public IReadOnlyList<Vec2> NormalCurve()
        {
            var pts = new List<Vec2>();
            for (var i = 0; i + 1 < Breaks.Count; i++)
            {
                double a = Breaks[i], b = Breaks[i + 1];
                pts.Add(new Vec2(a, Normal(a)));
                pts.Add(new Vec2(b, Normal(b, left: true)));
            }
            return pts;
        }

        private BeamExtreme NormalExtreme()
        {
            var best = new BeamExtreme(0, 0);
            var eps = 1e-9 * Math.Max(1, _loads.Axial.Sum(a => Math.Abs(a.H)));
            for (var i = 0; i + 1 < Breaks.Count; i++)
            {
                var v = Normal(Breaks[i]);
                if (Math.Abs(v) > Math.Abs(best.Value) + eps) best = new BeamExtreme(Breaks[i], v);
            }
            return best;
        }

        /// <summary>
        /// Deflection (positive downwards) at <paramref name="x"/> times the bending stiffness EI, i.e. for EI = 1 in the units
        /// of the model (forces and lengths); divide by the real EI for the real deflection.
        /// </summary>
        public double Deflection(double x)
        {
            if (_frame is null) return 0;
            var (e, t) = Stretch(x);
            return DeflectionAt(e, t);
        }

        /// <summary>Slope dw/dx of the deflection line at <paramref name="x"/> times EI (positive: falling to the right).</summary>
        public double Slope(double x)
        {
            if (_frame is null) return 0;
            var (e, t) = Stretch(x);
            return SlopeAt(e, t);
        }

        /// <summary>Largest deflection by magnitude (times EI, signed, downwards positive) and its position.</summary>
        public BeamExtreme MaxDeflection => _maxDeflection ??= DeflectionExtreme();

        /// <summary>The deflection line (times EI) as (x, w) points, <paramref name="steps"/> per stretch, through its extremes.</summary>
        public IReadOnlyList<Vec2> DeflectionCurve(int steps = 24)
        {
            var pts = new List<Vec2>();
            if (_frame is null) return pts;
            _ = MaxDeflection; // finds the vertices
            var nodes = _frame.Nodes;
            for (var e = 0; e + 1 < nodes.Length; e++)
            {
                double a = nodes[e], b = nodes[e + 1];
                var xs = Enumerable.Range(e == 0 ? 0 : 1, e == 0 ? steps + 1 : steps).Select(k => a + (b - a) * k / steps)
                    .Concat(_wVertices.Where(v => v > a && v < b)).OrderBy(x => x);
                foreach (var x in xs) pts.Add(new Vec2(x, DeflectionAt(e, (x - a) / (b - a))));
            }
            return pts;
        }

        private (int Element, double T) Stretch(double x)
        {
            var nodes = _frame!.Nodes;
            x = Geo.Clamp(x, 0, Model.Length);
            var e = 0;
            while (e + 2 < nodes.Length && x > nodes[e + 1]) e++;
            var l = nodes[e + 1] - nodes[e];
            return (e, l > 0 ? Geo.Clamp((x - nodes[e]) / l, 0, 1) : 0);
        }

        /// <summary>
        /// The bending moment on stretch <paramref name="e"/> as c₀ + c₁·t + c₂·t² + c₃·t³ (exact: line loads are at most
        /// linear, so the moment is at most cubic between two nodes), fitted through four values.
        /// </summary>
        private double[] Cubic(int e)
        {
            _cubics ??= new double[_frame!.Nodes.Length - 1][];
            if (_cubics[e] is { } c) return c;
            double a = _frame!.Nodes[e], b = _frame.Nodes[e + 1], l = b - a;
            var ts = new[] { 0.0, 1.0 / 3, 2.0 / 3, 1.0 };
            var m = new double[4, 4];
            var v = new double[4];
            for (var i = 0; i < 4; i++)
            {
                for (var k = 0; k < 4; k++) m[i, k] = Math.Pow(ts[i], k);
                v[i] = i == 0 ? Moment(a) : i == 3 ? Moment(b, left: true) : Moment(a + ts[i] * l);
            }
            return _cubics[e] = BeamStatics.SolveSquare(m, v);
        }

        // w(t) = w_a + θ_a·l·t − l²·Σ c_k·t^(k+2)/((k+1)(k+2)) from EI·w'' = −M (deflection downwards, sagging moment positive).
        private double DeflectionAt(int e, double t)
        {
            var f = _frame!;
            var l = f.Nodes[e + 1] - f.Nodes[e];
            var c = Cubic(e);
            var w = f.W[e] + f.ThetaRight[e] * l * t;
            for (var k = 0; k < 4; k++) w -= l * l * c[k] * Math.Pow(t, k + 2) / ((k + 1) * (k + 2));
            return w;
        }

        private double SlopeAt(int e, double t)
        {
            var f = _frame!;
            var l = f.Nodes[e + 1] - f.Nodes[e];
            var c = Cubic(e);
            var s = f.ThetaRight[e];
            for (var k = 0; k < 4; k++) s -= l * c[k] * Math.Pow(t, k + 1) / (k + 1);
            return s;
        }

        private BeamExtreme DeflectionExtreme()
        {
            if (_frame is null) return new BeamExtreme(0, 0);
            var nodes = _frame.Nodes;
            var candidates = new List<BeamExtreme>();
            for (var e = 0; e + 1 < nodes.Length; e++)
            {
                double a = nodes[e], l = nodes[e + 1] - a;
                candidates.Add(new BeamExtreme(a, DeflectionAt(e, 0)));
                // Vertices where the slope (a quartic in t) passes zero: sign changes on a fine grid, then bisection.
                const int n = 48;
                var prev = SlopeAt(e, 0);
                for (var k = 1; k <= n; k++)
                {
                    double t1 = (double)k / n, cur = SlopeAt(e, t1);
                    if (prev != 0 && cur != 0 && Math.Sign(prev) != Math.Sign(cur))
                    {
                        double lo = (double)(k - 1) / n, hi = t1, slo = prev;
                        for (var it = 0; it < 80 && hi - lo > 1e-15; it++)
                        {
                            var mid = (lo + hi) / 2;
                            var sm = SlopeAt(e, mid);
                            if (Math.Sign(sm) == Math.Sign(slo)) { lo = mid; slo = sm; } else hi = mid;
                        }
                        var t = (lo + hi) / 2;
                        _wVertices.Add(a + t * l);
                        candidates.Add(new BeamExtreme(a + t * l, DeflectionAt(e, t)));
                    }
                    prev = cur;
                }
                candidates.Add(new BeamExtreme(nodes[e + 1], DeflectionAt(e, 1)));
            }
            var top = candidates.Max(c => Math.Abs(c.Value));
            if (top < 1e-300) return new BeamExtreme(0, 0);
            return candidates.First(c => Math.Abs(c.Value) >= top * (1 - 1e-9));
        }

        /// <summary>Internal hinges that take part (inside the beam, sorted); the bending moment is zero there.</summary>
        public IReadOnlyList<double> Hinges { get; }

        /// <summary>Positions where the diagrams change their law (ends, supports, loads, couples, ends of line loads, hinges), sorted.</summary>
        public IReadOnlyList<double> Breaks { get; }

        /// <summary>Largest shear force by magnitude (signed), the first one if several are equal.</summary>
        public BeamExtreme MaxShear { get; }

        /// <summary>Largest bending moment by magnitude (signed) and its position, the first one if several are equal.</summary>
        public BeamExtreme MaxMoment { get; }

        /// <summary>Shear force just right of <paramref name="x"/> (or just left with <paramref name="left"/>).</summary>
        public double Shear(double x, bool left = false) => _loads.Shear(x, left);

        /// <summary>Bending moment just right of <paramref name="x"/> (or just left with <paramref name="left"/>).</summary>
        public double Moment(double x, bool left = false) => _loads.Moment(x, left);

        private List<double> BuildBreaks()
        {
            var L = Model.Length;
            var xs = new List<double> { 0, L };
            xs.AddRange(_loads.Forces.Select(f => f.S));
            xs.AddRange(_loads.Couples.Select(c => c.S));
            foreach (var l in _loads.Lines) { xs.Add(l.A); xs.Add(l.B); }
            xs.AddRange(Hinges);
            var sorted = xs.Select(x => Geo.Clamp(x, 0, L)).OrderBy(x => x).ToList();
            var result = new List<double>();
            foreach (var x in sorted) if (result.Count == 0 || x - result[result.Count - 1] > 1e-9 * Math.Max(1, L)) result.Add(x);
            return result;
        }

        /// <summary>True if a line load acts on (a, b); <paramref name="varying"/>: only loads whose intensity changes.</summary>
        private bool Loaded(double a, double b, bool varying = false)
            => _loads.Lines.Any(l => l.A < b - BeamLoads.Tol && l.B > a + BeamLoads.Tol && (!varying || Math.Abs(l.K) > 0));

        /// <summary>
        /// The shear force diagram as (x, V) points from left to right; jumps appear as two points at the same x. Stretches
        /// under triangular or trapezoidal loads (quadratic shear force) are sampled with <paramref name="steps"/> steps.
        /// </summary>
        public IReadOnlyList<Vec2> ShearCurve(int steps = 24)
        {
            var pts = new List<Vec2>();
            for (var i = 0; i + 1 < Breaks.Count; i++)
            {
                double a = Breaks[i], b = Breaks[i + 1];
                pts.Add(new Vec2(a, Shear(a)));
                if (Loaded(a, b, varying: true))
                    for (var k = 1; k < steps; k++) { var x = a + (b - a) * k / steps; pts.Add(new Vec2(x, Shear(x))); }
                pts.Add(new Vec2(b, Shear(b, left: true)));
            }
            return pts;
        }

        /// <summary>
        /// The bending moment diagram as (x, M) points; jumps (applied couples) appear as two points at the same x. Curved
        /// stretches under line loads (parabolic, cubic under triangular and trapezoidal loads) are sampled with
        /// <paramref name="steps"/> steps.
        /// </summary>
        public IReadOnlyList<Vec2> MomentCurve(int steps = 24)
        {
            var pts = new List<Vec2>();
            for (var i = 0; i + 1 < Breaks.Count; i++)
            {
                double a = Breaks[i], b = Breaks[i + 1];
                pts.Add(new Vec2(a, Moment(a)));
                if (Loaded(a, b))
                {
                    // The samples plus the vertices (zero shear force), so the drawn curve reaches its peak.
                    var xs = Enumerable.Range(1, steps - 1).Select(k => a + (b - a) * k / steps)
                        .Concat(_vertices.Where(v => v > a && v < b)).OrderBy(x => x);
                    foreach (var x in xs) pts.Add(new Vec2(x, Moment(x)));
                }
                pts.Add(new Vec2(b, Moment(b, left: true)));
            }
            return pts;
        }

        private (BeamExtreme, BeamExtreme) Extremes()
        {
            var scale = 1e-9 * Math.Max(1, _loads.Forces.Sum(f => Math.Abs(f.F)) + _loads.Lines.Sum(l => (Math.Abs(l.Qa) + Math.Abs(l.Qb)) / 2 * (l.B - l.A))
                + _loads.Couples.Sum(c => Math.Abs(c.C)) / Math.Max(1e-12, Model.Length));
            BeamExtreme v = new(0, 0), m = new(0, 0);
            void Take(ref BeamExtreme best, double x, double value, double eps)
            {
                if (Math.Abs(value) > Math.Abs(best.Value) + eps) best = new BeamExtreme(x, value);
            }
            var mScale = scale * Math.Max(1, Model.Length);
            for (var i = 0; i + 1 < Breaks.Count; i++)
            {
                double a = Breaks[i], b = Breaks[i + 1], w = b - a;
                // Inside a stretch the shear force is a polynomial of degree ≤ 2 (linear line loads): fit it exactly
                // through three values, V(t) = α + β·t + γ·t² with t = (x − a)/(b − a).
                double v0 = Shear(a), v1 = Shear(b, left: true), vm = Shear(a + w / 2);
                var gamma = 2 * (v1 - 2 * vm + v0);
                var beta = v1 - v0 - gamma;
                Take(ref v, a, v0, scale);
                if (Math.Abs(gamma) > scale)
                {
                    var tv = -beta / (2 * gamma);
                    if (tv > 0 && tv < 1) Take(ref v, a + tv * w, Shear(a + tv * w), scale);
                }
                Take(ref v, b, v1, scale);
                Take(ref m, a, Moment(a), mScale);
                // Where the shear force passes zero the moment has its vertex.
                foreach (var t in Roots(gamma, beta, v0, scale))
                {
                    if (!(t > 1e-9 && t < 1 - 1e-9)) continue;
                    _vertices.Add(a + t * w);
                    Take(ref m, a + t * w, Moment(a + t * w), mScale);
                }
                Take(ref m, b, Moment(b, left: true), mScale);
            }
            return (v, m);
        }

        /// <summary>Real roots of γ·t² + β·t + α (linear when γ vanishes).</summary>
        private static IEnumerable<double> Roots(double gamma, double beta, double alpha, double eps)
        {
            if (Math.Abs(gamma) <= eps)
            {
                if (Math.Abs(beta) > eps) yield return -alpha / beta;
                yield break;
            }
            var disc = beta * beta - 4 * gamma * alpha;
            if (disc < 0) yield break;
            var r = Math.Sqrt(disc);
            yield return (-beta - r) / (2 * gamma);
            yield return (-beta + r) / (2 * gamma);
        }
    }

    /// <summary>Concentrated forces (upward positive), couples (clockwise positive) and linear line loads acting on a beam.</summary>
    internal sealed class BeamLoads
    {
        public const double Tol = 1e-9;

        /// <summary>A line load on [A, B] with intensity Qa at A and Qb at B (downward positive); K = slope of the intensity.</summary>
        public readonly record struct Line(double A, double B, double Qa, double Qb)
        {
            public double K => (Qb - Qa) / (B - A);
        }

        public List<(double S, double F)> Forces { get; } = new();
        public List<(double S, double C)> Couples { get; } = new();
        public List<Line> Lines { get; } = new();

        /// <summary>Forces along the beam (positive towards the right end), applied loads and horizontal reactions.</summary>
        public List<(double S, double H)> Axial { get; } = new();

        public static BeamLoads Of(BeamModel model)
        {
            var loads = new BeamLoads();
            foreach (var p in model.PointLoads) loads.Forces.Add((p.X, -p.F));
            foreach (var p in model.PointLoads) if (p.Axial != 0) loads.Axial.Add((p.X, p.Axial));
            foreach (var c in model.Couples) loads.Couples.Add((c.X, c.M));
            foreach (var l in model.LineLoads)
            {
                if (Math.Abs(l.X1 - l.X0) <= 1e-12 || (l.Q == 0 && l.QEnd == 0)) continue;
                loads.Lines.Add(l.X0 <= l.X1 ? new Line(l.X0, l.X1, l.Q, l.QEnd) : new Line(l.X1, l.X0, l.QEnd, l.Q));
            }
            return loads;
        }

        public double Shear(double x, bool left)
        {
            var v = 0.0;
            foreach (var (s, f) in Forces) if (left ? s < x - Tol : s <= x + Tol) v += f;
            foreach (var l in Lines)
            {
                var c = Covered(l, x);
                if (c > 0) v -= l.Qa * c + l.K * c * c / 2;
            }
            return v;
        }

        public double Moment(double x, bool left)
        {
            var m = 0.0;
            foreach (var (s, f) in Forces) if (s < x - Tol) m += f * (x - s);
            foreach (var (s, c) in Couples) if (left ? s < x - Tol : s <= x + Tol) m += c;
            foreach (var l in Lines)
            {
                var c = Covered(l, x);
                if (c <= 0) continue;
                // ∫₀ᶜ (Qa + K·u)·(d − u) du with d = x − A.
                var d = x - l.A;
                m -= l.Qa * (d * c - c * c / 2) + l.K * (d * c * c / 2 - c * c * c / 3);
            }
            return m;
        }

        /// <summary>Normal force (tension positive): the forces along the beam left of the section, with opposite sign.</summary>
        public double Normal(double x, bool left)
        {
            var n = 0.0;
            foreach (var (s, h) in Axial) if (left ? s < x - Tol : s <= x + Tol) n -= h;
            return n;
        }

        private static double Covered(Line l, double x) => Math.Max(0, Math.Min(x, l.B) - l.A);
    }

    /// <summary>
    /// Displacement method for a straight prismatic beam (EI = 1, EA = 1): nodes at the ends, supports, loads, ends of line
    /// loads and hinges; per node a deflection w (downwards) and a slope θ = dw/dx (clockwise), two slopes at an internal
    /// hinge. Line loads enter as their exact equivalent nodal loads (Hermite elements), so the nodal values are exact for
    /// the Euler–Bernoulli beam. Gives the reactions of statically indeterminate beams (their size does not depend on a
    /// constant EI) and the nodal deflections and slopes from which <see cref="BeamSolution"/> integrates the deflection line.
    /// The normal force is a separate bar problem along the axis (horizontal displacement held at pins and fixed supports).
    /// </summary>
    internal sealed class BeamFrame
    {
        public double[] Nodes { get; private set; } = Array.Empty<double>();
        public double[] W { get; private set; } = Array.Empty<double>();
        public double[] ThetaRight { get; private set; } = Array.Empty<double>();

        /// <summary>Reaction per node: force in the w direction (downwards) and couple (clockwise) at the slope of a fixed support.</summary>
        public double[] ForceReaction { get; private set; } = Array.Empty<double>();
        public double[] CoupleReaction { get; private set; } = Array.Empty<double>();

        /// <summary>Horizontal reaction per node (towards the right end), zero without forces along the beam.</summary>
        public double[] HorizontalReaction { get; private set; } = Array.Empty<double>();

        /// <summary>Index of the node at <paramref name="x"/> (the nearest one).</summary>
        public int NodeAt(double x)
        {
            var best = 0;
            for (var i = 1; i < Nodes.Length; i++) if (Math.Abs(Nodes[i] - x) < Math.Abs(Nodes[best] - x)) best = i;
            return best;
        }

        /// <summary>Solves the beam; null if the stiffness matrix is singular (a mechanism).</summary>
        public static BeamFrame? Solve(BeamModel model, BeamLoads loads, IReadOnlyList<double> hinges)
        {
            var L = model.Length;
            var tol = 1e-9 * Math.Max(1, L);
            var xs = new List<double> { 0, L };
            xs.AddRange(model.Supports.Select(s => s.X));
            xs.AddRange(loads.Forces.Select(f => f.S));
            xs.AddRange(loads.Couples.Select(c => c.S));
            xs.AddRange(loads.Axial.Select(a => a.S));
            foreach (var l in loads.Lines) { xs.Add(l.A); xs.Add(l.B); }
            xs.AddRange(hinges);
            var nodes = new List<double>();
            foreach (var x in xs.Select(x => Geo.Clamp(x, 0, L)).OrderBy(x => x))
                if (nodes.Count == 0 || x - nodes[nodes.Count - 1] > tol) nodes.Add(x);
            var f = new BeamFrame { Nodes = nodes.ToArray() };
            var n = nodes.Count;
            if (n < 2) return null;
            bool IsHinge(int i) => i > 0 && i < n - 1 && hinges.Any(h => Math.Abs(h - nodes[i]) <= tol);

            // Degrees of freedom: w, θ (left side) and, at a hinge, θ (right side).
            var iw = new int[n]; var itl = new int[n]; var itr = new int[n];
            var dof = 0;
            for (var i = 0; i < n; i++) { iw[i] = dof++; itl[i] = dof++; itr[i] = IsHinge(i) ? dof++ : itl[i]; }
            var k = new double[dof, dof];
            var p = new double[dof];
            for (var e = 0; e + 1 < n; e++)
            {
                double a = nodes[e], b = nodes[e + 1], l = b - a;
                var idx = new[] { iw[e], itr[e], iw[e + 1], itl[e + 1] };
                var ke = new double[,]
                {
                    { 12, 6 * l, -12, 6 * l },
                    { 6 * l, 4 * l * l, -6 * l, 2 * l * l },
                    { -12, -6 * l, 12, -6 * l },
                    { 6 * l, 2 * l * l, -6 * l, 4 * l * l },
                };
                var l3 = l * l * l;
                for (var ri = 0; ri < 4; ri++) for (var ci = 0; ci < 4; ci++) k[idx[ri], idx[ci]] += ke[ri, ci] / l3;
                // Line loads covering the element (their ends are nodes), linear from qa to qb: equivalent nodal loads.
                double qa = 0, qb = 0;
                foreach (var line in loads.Lines)
                {
                    if (line.A > a + tol || line.B < b - tol) continue;
                    qa += line.Qa + line.K * (a - line.A);
                    qb += line.Qa + line.K * (b - line.A);
                }
                if (qa != 0 || qb != 0)
                {
                    p[idx[0]] += l * (7 * qa + 3 * qb) / 20;
                    p[idx[1]] += l * l * (qa / 20 + qb / 30);
                    p[idx[2]] += l * (3 * qa + 7 * qb) / 20;
                    p[idx[3]] -= l * l * (qa / 30 + qb / 20);
                }
            }
            foreach (var (s, up) in loads.Forces) p[iw[f.NodeAt(s)]] -= up;
            foreach (var (s, c) in loads.Couples) p[itr[f.NodeAt(s)]] += c;

            var held = new bool[dof];
            foreach (var s in model.Supports)
            {
                var i = f.NodeAt(Geo.Clamp(s.X, 0, L));
                held[iw[i]] = true;
                if (s.Kind == BeamSupportKind.Fixed) { held[itl[i]] = true; held[itr[i]] = true; }
            }
            var d = SolveHeld(k, p, held);
            if (d is null) return null;
            var r = Residual(k, d, p);
            f.W = Enumerable.Range(0, n).Select(i => d[iw[i]]).ToArray();
            f.ThetaRight = Enumerable.Range(0, n).Select(i => d[itr[i]]).ToArray();
            f.ForceReaction = Enumerable.Range(0, n).Select(i => held[iw[i]] ? r[iw[i]] : 0).ToArray();
            f.CoupleReaction = Enumerable.Range(0, n).Select(i => held[itl[i]] ? r[itl[i]] : 0).ToArray();

            // Normal force: a bar along the axis held at pins and fixed supports (rollers slide).
            f.HorizontalReaction = new double[n];
            if (loads.Axial.Count > 0)
            {
                var ka = new double[n, n];
                var pa = new double[n];
                for (var e = 0; e + 1 < n; e++)
                {
                    var c = 1 / (nodes[e + 1] - nodes[e]);
                    ka[e, e] += c; ka[e + 1, e + 1] += c; ka[e, e + 1] -= c; ka[e + 1, e] -= c;
                }
                foreach (var (s, h) in loads.Axial) pa[f.NodeAt(s)] += h;
                var heldA = new bool[n];
                foreach (var s in model.Supports) if (s.Kind != BeamSupportKind.Roller) heldA[f.NodeAt(Geo.Clamp(s.X, 0, L))] = true;
                var u = SolveHeld(ka, pa, heldA);
                if (u is null) return null;
                var ra = Residual(ka, u, pa);
                f.HorizontalReaction = Enumerable.Range(0, n).Select(i => heldA[i] ? ra[i] : 0).ToArray();
            }
            return f;
        }

        /// <summary>k·d − p: the reactions at the held degrees of freedom (zero elsewhere up to rounding).</summary>
        private static double[] Residual(double[,] k, double[] d, double[] p)
        {
            var n = p.Length;
            var r = new double[n];
            for (var i = 0; i < n; i++)
            {
                var s = -p[i];
                for (var j = 0; j < n; j++) s += k[i, j] * d[j];
                r[i] = s;
            }
            return r;
        }

        /// <summary>
        /// Solves k·d = p for the free degrees of freedom (held ones are zero), scaled to a unit diagonal; null when the free
        /// part is singular (a mechanism).
        /// </summary>
        private static double[]? SolveHeld(double[,] k, double[] p, bool[] held)
        {
            var free = Enumerable.Range(0, p.Length).Where(i => !held[i]).ToList();
            var d = new double[p.Length];
            var m = free.Count;
            if (m == 0) return d;
            var a = new double[m, m];
            var b = new double[m];
            var scale = new double[m];
            for (var i = 0; i < m; i++)
            {
                var diag = k[free[i], free[i]];
                if (!(diag > 0)) return null;
                scale[i] = 1 / Math.Sqrt(diag);
            }
            for (var i = 0; i < m; i++)
            {
                b[i] = p[free[i]] * scale[i];
                for (var j = 0; j < m; j++) a[i, j] = k[free[i], free[j]] * scale[i] * scale[j];
            }
            for (var c = 0; c < m; c++)
            {
                var piv = c;
                for (var r = c + 1; r < m; r++) if (Math.Abs(a[r, c]) > Math.Abs(a[piv, c])) piv = r;
                if (Math.Abs(a[piv, c]) < 1e-10) return null;
                for (var x = 0; x < m; x++) (a[piv, x], a[c, x]) = (a[c, x], a[piv, x]);
                (b[piv], b[c]) = (b[c], b[piv]);
                for (var r = c + 1; r < m; r++)
                {
                    var fac = a[r, c] / a[c, c];
                    if (fac == 0) continue;
                    for (var x = c; x < m; x++) a[r, x] -= fac * a[c, x];
                    b[r] -= fac * b[c];
                }
            }
            var y = new double[m];
            for (var r = m - 1; r >= 0; r--)
            {
                var s = b[r];
                for (var x = r + 1; x < m; x++) s -= a[r, x] * y[x];
                y[r] = s / a[r, r];
            }
            for (var i = 0; i < m; i++) d[free[i]] = y[i] * scale[i];
            return d;
        }
    }

    /// <summary>
    /// Support reactions, normal force, shear force, bending moment and deflection of straight beams under point loads
    /// (transverse or inclined), uniform, triangular and trapezoidal line loads and applied couples: beams on hinged supports
    /// (pin and roller; overhangs allowed), cantilevers, Gerber beams with internal hinges and statically indeterminate beams
    /// (continuous beams, propped cantilevers, beams clamped at both ends). Determinate beams follow from the equilibrium
    /// conditions plus one condition (zero bending moment) per internal hinge, indeterminate ones from the displacement
    /// method of a prismatic beam; unstable systems are recognised and not solved. Pure computation, no host involved.
    /// </summary>
    public static class BeamStatics
    {
        private const double RankTol = 1e-9;

        /// <summary>The internal hinges that count: inside the beam (not at its ends), sorted, duplicates merged.</summary>
        public static IReadOnlyList<double> InnerHinges(BeamModel model)
        {
            var L = model.Length;
            var tol = 1e-9 * Math.Max(1, L);
            var list = new List<double>();
            foreach (var h in model.Hinges.OrderBy(x => x))
                if (h > tol && h < L - tol && (list.Count == 0 || h - list[list.Count - 1] > tol)) list.Add(h);
            return list;
        }

        /// <summary>One unknown reaction: a support force (upward) or the clamping couple (clockwise) of a fixed support.</summary>
        private readonly record struct Unknown(int Support, bool Couple, double S);

        private static List<Unknown> Unknowns(BeamModel model)
        {
            var list = new List<Unknown>();
            for (var i = 0; i < model.Supports.Count; i++)
            {
                var s = model.Supports[i];
                var x = Geo.Clamp(s.X, 0, model.Length);
                list.Add(new Unknown(i, false, x));
                if (s.Kind == BeamSupportKind.Fixed) list.Add(new Unknown(i, true, x));
            }
            return list;
        }

        /// <summary>
        /// The condition matrix: row 0 = sum of the vertical forces, row 1 = bending moment just beyond the right end, then
        /// one row per hinge (bending moment just left of it). Entries are the effect of a unit reaction; with
        /// <paramref name="unit"/> lengths are measured in beam lengths (for the rank test).
        /// </summary>
        private static double[,] Conditions(BeamModel model, IReadOnlyList<Unknown> unknowns, IReadOnlyList<double> hinges, bool unit)
        {
            var L = model.Length;
            var k = unit ? 1 / L : 1;
            var a = new double[2 + hinges.Count, unknowns.Count];
            for (var j = 0; j < unknowns.Count; j++)
            {
                var u = unknowns[j];
                a[0, j] = u.Couple ? 0 : 1;
                a[1, j] = u.Couple ? 1 : (L - u.S) * k;
                for (var h = 0; h < hinges.Count; h++)
                {
                    var x = hinges[h];
                    a[2 + h, j] = u.S < x - BeamLoads.Tol ? (u.Couple ? 1 : (x - u.S) * k) : 0;
                }
            }
            return a;
        }

        private static int Rank(double[,] m)
        {
            int rows = m.GetLength(0), cols = m.GetLength(1);
            var a = (double[,])m.Clone();
            var rank = 0;
            for (var c = 0; c < cols && rank < rows; c++)
            {
                var p = rank;
                for (var r = rank + 1; r < rows; r++) if (Math.Abs(a[r, c]) > Math.Abs(a[p, c])) p = r;
                if (Math.Abs(a[p, c]) <= RankTol) continue;
                for (var x = 0; x < cols; x++) (a[p, x], a[rank, x]) = (a[rank, x], a[p, x]);
                for (var r = rank + 1; r < rows; r++)
                {
                    var f = a[r, c] / a[rank, c];
                    for (var x = c; x < cols; x++) a[r, x] -= f * a[rank, x];
                }
                rank++;
            }
            return rank;
        }

        /// <summary>
        /// Classifies the beam: unknown reactions (one force per pin or roller, force and couple per fixed support) against the
        /// conditions (vertical and moment equilibrium, zero moment at each internal hinge). Unstable when the conditions cannot
        /// all be met for any load (too few or ill-placed supports, a hinge that makes a part movable); indeterminate when
        /// unknowns remain; determinate otherwise. Without loads along the beam two pins count as determinate (the horizontal
        /// reactions are zero); with such loads every pin and fixed support adds a horizontal reaction and the horizontal
        /// equilibrium adds a condition, so a beam on rollers only becomes unstable and two pins indeterminate of degree 1.
        /// </summary>
        public static BeamCheck Check(BeamModel model)
        {
            var t = TransverseCheck(model);
            if (t.Status is BeamStatus.Invalid or BeamStatus.Unstable || !model.PointLoads.Any(p => p.Axial != 0)) return t;
            var held = model.Supports.Count(s => s.Kind != BeamSupportKind.Roller);
            if (held == 0) return new BeamCheck(BeamStatus.Unstable, 1, t.Unknowns, t.Conditions + 1);
            var degree = t.Degree + held - 1;
            return new BeamCheck(degree > 0 ? BeamStatus.Indeterminate : BeamStatus.Determinate, degree, t.Unknowns + held, t.Conditions + 1);
        }

        /// <summary>The classification for the transverse loads alone (see <see cref="Check"/>).</summary>
        private static BeamCheck TransverseCheck(BeamModel model)
        {
            if (!(model.Length > 0)) return new BeamCheck(BeamStatus.Invalid, 0, 0, 0);
            var unknowns = Unknowns(model);
            var hinges = InnerHinges(model);
            var conditions = 2 + hinges.Count;
            var rank = unknowns.Count == 0 ? 0 : Rank(Conditions(model, unknowns, hinges, unit: true));
            if (rank < conditions) return new BeamCheck(BeamStatus.Unstable, conditions - rank, unknowns.Count, conditions);
            if (unknowns.Count > rank) return new BeamCheck(BeamStatus.Indeterminate, unknowns.Count - rank, unknowns.Count, conditions);
            return new BeamCheck(BeamStatus.Determinate, 0, unknowns.Count, conditions);
        }

        /// <summary>
        /// True if the beam is statically determinate for transverse loads (<see cref="Check"/>). <paramref name="reason"/>
        /// names the problem otherwise: "length", "no support", "unstable" or "indeterminate".
        /// </summary>
        public static bool IsDeterminate(BeamModel model, out string? reason)
        {
            var check = Check(model);
            reason = check.Status switch
            {
                BeamStatus.Determinate => null,
                BeamStatus.Invalid => "length",
                BeamStatus.Unstable => model.Supports.Count == 0 ? "no support" : "unstable",
                _ => "indeterminate",
            };
            return check.IsDeterminate;
        }

        /// <summary>
        /// Solves the beam, or returns null if it is unstable or has no length (<see cref="Check"/>). Statically determinate
        /// beams are solved from the equilibrium and hinge conditions; statically indeterminate ones (continuous beams,
        /// propped cantilevers, beams clamped at both ends, two pins with loads along the beam) by the displacement method
        /// for a prismatic beam (constant EI and EA), whose reactions do not depend on the size of EI and EA. The deflection
        /// line (<see cref="BeamSolution.Deflection"/>) comes from the same method in both cases.
        /// </summary>
        public static BeamSolution? Solve(BeamModel model)
        {
            var check = Check(model);
            if (check.Status is BeamStatus.Invalid or BeamStatus.Unstable) return null;
            var L = model.Length;
            var loads = BeamLoads.Of(model);
            var unknowns = Unknowns(model);
            var hinges = InnerHinges(model);
            var frame = BeamFrame.Solve(model, loads, hinges);
            if (frame is null) return null;
            var n = unknowns.Count;
            double[] u;
            if (TransverseCheck(model).IsDeterminate)
            {
                var a = Conditions(model, unknowns, hinges, unit: false);
                // Right-hand side: what the loads alone contribute to each condition, with opposite sign.
                var rhs = new double[n];
                rhs[0] = -loads.Shear(L, left: false);
                rhs[1] = -loads.Moment(L, left: false);
                for (var h = 0; h < hinges.Count; h++) rhs[2 + h] = -loads.Moment(hinges[h], left: true);
                u = SolveSquare(a, rhs);
            }
            else
            {
                // Reactions of the displacement method: upward force = −(reaction in w), clamping couple clockwise. Several
                // supports at one node share it: the first takes the reaction.
                u = new double[n];
                var taken = new HashSet<(int, bool)>();
                for (var j = 0; j < n; j++)
                {
                    var node = frame.NodeAt(unknowns[j].S);
                    if (!taken.Add((node, unknowns[j].Couple))) continue;
                    u[j] = unknowns[j].Couple ? frame.CoupleReaction[node] : -frame.ForceReaction[node];
                }
            }

            for (var j = 0; j < n; j++)
                if (unknowns[j].Couple) loads.Couples.Add((unknowns[j].S, u[j])); else loads.Forces.Add((unknowns[j].S, u[j]));
            var horizontal = new double[model.Supports.Count];
            var takenH = new HashSet<int>();
            for (var i = 0; i < model.Supports.Count; i++)
            {
                if (model.Supports[i].Kind == BeamSupportKind.Roller) continue;
                var x = Geo.Clamp(model.Supports[i].X, 0, L);
                var node = frame.NodeAt(x);
                if (!takenH.Add(node)) continue;
                horizontal[i] = frame.HorizontalReaction[node];
                if (horizontal[i] != 0) loads.Axial.Add((x, horizontal[i]));
            }
            var solution = new BeamSolution(model, Array.Empty<BeamReaction>(), loads, frame) { Check = check };
            var reactions = new List<BeamReaction>();
            for (var i = 0; i < model.Supports.Count; i++)
            {
                var sup = model.Supports[i];
                var force = 0.0;
                for (var j = 0; j < n; j++) if (unknowns[j].Support == i && !unknowns[j].Couple) force = u[j];
                var moment = 0.0;
                if (sup.Kind == BeamSupportKind.Fixed)
                {
                    // Bending moment in the beam at the wall, on the side of the beam that carries the loads.
                    var x = Geo.Clamp(sup.X, 0, L);
                    moment = x <= L / 2 ? solution.Moment(x) : solution.Moment(x, left: true);
                }
                reactions.Add(new BeamReaction(sup, force, moment) { Horizontal = horizontal[i] });
            }
            solution.Reactions = reactions;
            return solution;
        }

        /// <summary>Solves the square system a·u = b (Gaussian elimination with partial pivoting).</summary>
        internal static double[] SolveSquare(double[,] m, double[] rhs)
        {
            var n = rhs.Length;
            var a = (double[,])m.Clone();
            var b = (double[])rhs.Clone();
            for (var c = 0; c < n; c++)
            {
                var p = c;
                for (var r = c + 1; r < n; r++) if (Math.Abs(a[r, c]) > Math.Abs(a[p, c])) p = r;
                for (var x = 0; x < n; x++) (a[p, x], a[c, x]) = (a[c, x], a[p, x]);
                (b[p], b[c]) = (b[c], b[p]);
                for (var r = c + 1; r < n; r++)
                {
                    var f = a[r, c] / a[c, c];
                    for (var x = c; x < n; x++) a[r, x] -= f * a[c, x];
                    b[r] -= f * b[c];
                }
            }
            var u = new double[n];
            for (var r = n - 1; r >= 0; r--)
            {
                var s = b[r];
                for (var x = r + 1; x < n; x++) s -= a[r, x] * u[x];
                u[r] = s / a[r, r];
            }
            return u;
        }

        /// <summary>
        /// A coefficient as a short fraction ("1/4", "3/16", "2") when it is one with a denominator up to 64, otherwise as a
        /// decimal number with three decimals.
        /// </summary>
        public static (long Num, long Den)? Fraction(double c) => Fraction(c, 64, 1e-6);

        /// <summary>
        /// <paramref name="c"/> as a fraction with a denominator up to <paramref name="maxDen"/> (the smallest one) when it lies
        /// within <paramref name="tol"/> of one, else null.
        /// </summary>
        public static (long Num, long Den)? Fraction(double c, int maxDen, double tol)
        {
            for (long d = 1; d <= maxDen; d++)
            {
                var n = Math.Round(c * d);
                if (Math.Abs(c * d - n) < tol * d) return ((long)n, d);
            }
            return null;
        }

        /// <summary>
        /// A deflection "c·numerator/EI" written compactly: "F·L³/(3·EI)", "5·q·L⁴/(384·EI)", "−M·L²/(2·EI)", "F·L³/EI"; fractions
        /// with denominators up to 2000 (deflection coefficients such as 5/384 or 1/192), otherwise three significant digits
        /// ("0.00542·q·L⁴/EI").
        /// </summary>
        public static string OverStiffness(double c, string numerator)
        {
            if (Math.Abs(c) < 1e-12) return "0";
            var sign = c < 0 ? "−" : "";
            var a = Math.Abs(c);
            if (Fraction(a, 2000, 1e-9) is { } f && f.Num <= 999)
            {
                var num = f.Num == 1 ? numerator : f.Num.ToString(CultureInfo.InvariantCulture) + "·" + numerator;
                return sign + num + "/" + (f.Den == 1 ? "EI" : "(" + f.Den.ToString(CultureInfo.InvariantCulture) + "·EI)");
            }
            return sign + a.ToString("G3", CultureInfo.InvariantCulture) + "·" + numerator + "/EI";
        }

        private static readonly double Sqrt3 = Math.Sqrt(3);

        /// <summary>
        /// Symbolic value "c·factor" written compactly: "F/2", "F·L/4", "3·q·L²/8", "−F·L", "13·F·L/128" (denominators above 64
        /// only when exact to 10⁻¹⁰), "q·L²/(9√3)" and "L/√3" (short fractions over √3, as under triangular loads), "√2·F/2"
        /// and "√3·F/2" (components of inclined loads), "0.123·F·L" (decimal when no fraction fits).
        /// </summary>
        public static string Symbolic(double c, string factor)
        {
            if (Math.Abs(c) < 1e-12) return "0";
            var sign = c < 0 ? "−" : "";
            var a = Math.Abs(c);
            // Short fractions; longer denominators (up to 512, as 13/128 in continuous beams) only when (nearly) exact.
            if ((Fraction(a) ?? Fraction(a, 512, 1e-10)) is { } f)
            {
                var num = f.Num == 1 ? factor : f.Num.ToString(CultureInfo.InvariantCulture) + "·" + factor;
                return sign + (f.Den == 1 ? num : num + "/" + f.Den.ToString(CultureInfo.InvariantCulture));
            }
            if (Fraction(a * Sqrt3) is { } r && r.Num > 0 && r.Num <= 64 && r.Num % 3 != 0)
            {
                var num = r.Num == 1 ? factor : r.Num.ToString(CultureInfo.InvariantCulture) + "·" + factor;
                return sign + num + "/" + (r.Den == 1 ? "√3" : "(" + r.Den.ToString(CultureInfo.InvariantCulture) + "√3)");
            }
            // Multiples of √2 and √3 (components of loads inclined at 45°, 30° or 60°): "√2·F/2", "√3·F/2".
            foreach (var (root, name) in new[] { (Math.Sqrt(2), "√2"), (Sqrt3, "√3") })
                if (Fraction(a / root) is { } w && w.Num > 0 && w.Num <= 64)
                {
                    var num = (w.Num == 1 ? "" : w.Num.ToString(CultureInfo.InvariantCulture) + "·") + name + "·" + factor;
                    return sign + (w.Den == 1 ? num : num + "/" + w.Den.ToString(CultureInfo.InvariantCulture));
                }
            return sign + Ann.Fmt(a, 3) + "·" + factor;
        }
    }
}
