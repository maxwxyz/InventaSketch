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

    /// <summary>A point load at <paramref name="X"/>; <paramref name="F"/> is positive downwards (towards the beam's lower side).</summary>
    public sealed record BeamPointLoad(double X, double F);

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
    public sealed record BeamReaction(BeamSupport Support, double Force, double Moment);

    /// <summary>An extreme value of a diagram (signed) and where it occurs.</summary>
    public sealed record BeamExtreme(double X, double Value);

    /// <summary>Static classification of a beam.</summary>
    public enum BeamStatus
    {
        /// <summary>Statically determinate: the equilibrium conditions (and the hinge conditions) give all reactions.</summary>
        Determinate,
        /// <summary>More reactions than conditions: the reactions depend on the stiffness, statics alone cannot give them.</summary>
        Indeterminate,
        /// <summary>Not enough (or ill-placed) supports: the beam or a part of it can move (a mechanism).</summary>
        Unstable,
        /// <summary>No positive length.</summary>
        Invalid,
    }

    /// <summary>
    /// Result of <see cref="BeamStatics.Check"/>: the status and its degree (degree of indeterminacy for
    /// <see cref="BeamStatus.Indeterminate"/>, number of free motions for <see cref="BeamStatus.Unstable"/>, else 0), the
    /// number of unknown reactions and of conditions (2 equilibrium conditions plus one per internal hinge).
    /// </summary>
    public sealed record BeamCheck(BeamStatus Status, int Degree, int Unknowns, int Conditions)
    {
        public bool IsDeterminate => Status == BeamStatus.Determinate;
    }

    /// <summary>
    /// Shear force and bending moment of a solved statically determinate beam. Sign convention (common textbook use): the
    /// shear force is the sum of the upward forces left of the section; the bending moment is positive when it sags the beam
    /// (tension at the lower side).
    /// </summary>
    public sealed class BeamSolution
    {
        private readonly BeamLoads _loads;
        private readonly List<double> _vertices = new(); // where the shear force passes zero inside a stretch

        internal BeamSolution(BeamModel model, IReadOnlyList<BeamReaction> reactions, BeamLoads loads)
        {
            Model = model;
            Reactions = reactions;
            _loads = loads;
            Hinges = BeamStatics.InnerHinges(model);
            Breaks = BuildBreaks();
            (MaxShear, MaxMoment) = Extremes();
        }

        public BeamModel Model { get; }
        public IReadOnlyList<BeamReaction> Reactions { get; internal set; }

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

        public static BeamLoads Of(BeamModel model)
        {
            var loads = new BeamLoads();
            foreach (var p in model.PointLoads) loads.Forces.Add((p.X, -p.F));
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

        private static double Covered(Line l, double x) => Math.Max(0, Math.Min(x, l.B) - l.A);
    }

    /// <summary>
    /// Support reactions, shear force and bending moment of statically determinate straight beams under transverse loads
    /// (point loads, uniform, triangular and trapezoidal line loads, applied couples): beams on hinged supports (pin and
    /// roller; overhangs allowed), cantilevers and Gerber beams with internal hinges. The reactions follow from the two
    /// equilibrium conditions plus one condition (zero bending moment) per internal hinge; statically indeterminate and
    /// unstable systems are recognised and not solved. Pure computation, no host involved.
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
        /// Classifies the beam for transverse loads: unknown reactions (one force per pin or roller, force and couple per fixed
        /// support) against the conditions (vertical and moment equilibrium, zero moment at each internal hinge). Unstable when
        /// the conditions cannot all be met for any load (too few or ill-placed supports, a hinge that makes a part movable);
        /// indeterminate when unknowns remain; determinate otherwise. Two pins count as determinate because only transverse
        /// loads are considered (horizontal reactions are zero).
        /// </summary>
        public static BeamCheck Check(BeamModel model)
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

        /// <summary>Solves the beam, or returns null if it is not statically determinate (<see cref="Check"/>).</summary>
        public static BeamSolution? Solve(BeamModel model)
        {
            if (!Check(model).IsDeterminate) return null;
            var L = model.Length;
            var loads = BeamLoads.Of(model);
            var unknowns = Unknowns(model);
            var hinges = InnerHinges(model);
            var a = Conditions(model, unknowns, hinges, unit: false);
            var n = unknowns.Count;
            // Right-hand side: what the loads alone contribute to each condition, with opposite sign.
            var rhs = new double[n];
            rhs[0] = -loads.Shear(L, left: false);
            rhs[1] = -loads.Moment(L, left: false);
            for (var h = 0; h < hinges.Count; h++) rhs[2 + h] = -loads.Moment(hinges[h], left: true);
            var u = SolveSquare(a, rhs);

            for (var j = 0; j < n; j++)
                if (unknowns[j].Couple) loads.Couples.Add((unknowns[j].S, u[j])); else loads.Forces.Add((unknowns[j].S, u[j]));
            var solution = new BeamSolution(model, Array.Empty<BeamReaction>(), loads);
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
                reactions.Add(new BeamReaction(sup, force, moment));
            }
            solution.Reactions = reactions;
            return solution;
        }

        /// <summary>Solves the square system a·u = b (Gaussian elimination with partial pivoting).</summary>
        private static double[] SolveSquare(double[,] m, double[] rhs)
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
        public static (long Num, long Den)? Fraction(double c)
        {
            for (long d = 1; d <= 64; d++)
            {
                var n = Math.Round(c * d);
                if (Math.Abs(c * d - n) < 1e-6 * d) return ((long)n, d);
            }
            return null;
        }

        private static readonly double Sqrt3 = Math.Sqrt(3);

        /// <summary>
        /// Symbolic value "c·factor" written compactly: "F/2", "F·L/4", "3·q·L²/8", "−F·L", "q·L²/(9√3)" and "L/√3" (short
        /// fractions over √3, as under triangular loads), "0.123·F·L" (decimal when no short fraction fits).
        /// </summary>
        public static string Symbolic(double c, string factor)
        {
            if (Math.Abs(c) < 1e-12) return "0";
            var sign = c < 0 ? "−" : "";
            var a = Math.Abs(c);
            if (Fraction(a) is { } f)
            {
                var num = f.Num == 1 ? factor : f.Num.ToString(CultureInfo.InvariantCulture) + "·" + factor;
                return sign + (f.Den == 1 ? num : num + "/" + f.Den.ToString(CultureInfo.InvariantCulture));
            }
            if (Fraction(a * Sqrt3) is { } r && r.Num > 0 && r.Num <= 64)
            {
                var num = r.Num == 1 ? factor : r.Num.ToString(CultureInfo.InvariantCulture) + "·" + factor;
                return sign + num + "/" + (r.Den == 1 ? "√3" : "(" + r.Den.ToString(CultureInfo.InvariantCulture) + "√3)");
            }
            return sign + Ann.Fmt(a, 3) + "·" + factor;
        }
    }
}
