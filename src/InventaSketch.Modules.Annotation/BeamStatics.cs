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

    /// <summary>A uniform line load from <paramref name="X0"/> to <paramref name="X1"/>; <paramref name="Q"/> (force per length) is positive downwards.</summary>
    public sealed record BeamLineLoad(double X0, double X1, double Q);

    /// <summary>A straight beam of <paramref name="Length"/> with supports, point loads and uniform line loads (transverse loads only).</summary>
    public sealed record BeamModel(double Length, IReadOnlyList<BeamSupport> Supports, IReadOnlyList<BeamPointLoad> PointLoads, IReadOnlyList<BeamLineLoad> LineLoads)
    {
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

    /// <summary>
    /// Shear force and bending moment of a solved statically determinate beam. Sign convention (common textbook use): the
    /// shear force is the sum of the upward forces left of the section; the bending moment is positive when it sags the beam
    /// (tension at the lower side).
    /// </summary>
    public sealed class BeamSolution
    {
        private readonly List<(double S, double F)> _forces;   // concentrated forces, upward positive (reactions and loads)
        private readonly List<(double S, double C)> _couples;  // concentrated couples (jump of the bending moment)
        private readonly List<BeamLineLoad> _lines;

        internal BeamSolution(BeamModel model, IReadOnlyList<BeamReaction> reactions, List<(double, double)> forces, List<(double, double)> couples)
        {
            Model = model;
            Reactions = reactions;
            _forces = forces;
            _couples = couples;
            _lines = model.LineLoads.Where(l => Math.Abs(l.X1 - l.X0) > 1e-12 && Math.Abs(l.Q) > 0).ToList();
            Breaks = BuildBreaks();
            (MaxShear, MaxMoment) = Extremes();
        }

        public BeamModel Model { get; }
        public IReadOnlyList<BeamReaction> Reactions { get; }

        /// <summary>Positions where the diagrams change their law (ends, supports, point loads, ends of line loads), sorted.</summary>
        public IReadOnlyList<double> Breaks { get; }

        /// <summary>Largest shear force by magnitude (signed), the first one if several are equal.</summary>
        public BeamExtreme MaxShear { get; }

        /// <summary>Largest bending moment by magnitude (signed), the first one if several are equal.</summary>
        public BeamExtreme MaxMoment { get; }

        private const double Tol = 1e-9;

        /// <summary>Shear force just right of <paramref name="x"/> (or just left with <paramref name="left"/>).</summary>
        public double Shear(double x, bool left = false)
        {
            var v = 0.0;
            foreach (var (s, f) in _forces) if (left ? s < x - Tol : s <= x + Tol) v += f;
            foreach (var l in _lines) v -= l.Q * Covered(l, x);
            return v;
        }

        /// <summary>Bending moment just right of <paramref name="x"/> (or just left with <paramref name="left"/>).</summary>
        public double Moment(double x, bool left = false)
        {
            var m = 0.0;
            foreach (var (s, f) in _forces) if (s < x - Tol) m += f * (x - s);
            foreach (var (s, c) in _couples) if (left ? s < x - Tol : s <= x + Tol) m += c;
            foreach (var l in _lines)
            {
                var len = Covered(l, x);
                if (len <= 0) continue;
                var a = Math.Min(l.X0, l.X1);
                m -= l.Q * len * (x - (a + len / 2));
            }
            return m;
        }

        private static double Covered(BeamLineLoad l, double x)
        {
            double a = Math.Min(l.X0, l.X1), b = Math.Max(l.X0, l.X1);
            return Math.Max(0, Math.Min(x, b) - a);
        }

        private List<double> BuildBreaks()
        {
            var L = Model.Length;
            var xs = new List<double> { 0, L };
            xs.AddRange(_forces.Select(f => f.S));
            xs.AddRange(_couples.Select(c => c.S));
            foreach (var l in _lines) { xs.Add(l.X0); xs.Add(l.X1); }
            var sorted = xs.Select(x => Geo.Clamp(x, 0, L)).OrderBy(x => x).ToList();
            var result = new List<double>();
            foreach (var x in sorted) if (result.Count == 0 || x - result[result.Count - 1] > 1e-9 * Math.Max(1, L)) result.Add(x);
            return result;
        }

        private bool Loaded(double a, double b) => _lines.Any(l => Math.Min(l.X0, l.X1) < b - Tol && Math.Max(l.X0, l.X1) > a + Tol);

        /// <summary>The shear force diagram as (x, V) points from left to right; jumps appear as two points at the same x.</summary>
        public IReadOnlyList<Vec2> ShearCurve()
        {
            var pts = new List<Vec2>();
            for (var i = 0; i + 1 < Breaks.Count; i++)
            {
                double a = Breaks[i], b = Breaks[i + 1];
                pts.Add(new Vec2(a, Shear(a)));
                pts.Add(new Vec2(b, Shear(b, left: true)));
            }
            return pts;
        }

        /// <summary>The bending moment diagram as (x, M) points (curved stretches under line loads sampled with <paramref name="steps"/> steps).</summary>
        public IReadOnlyList<Vec2> MomentCurve(int steps = 24)
        {
            var pts = new List<Vec2>();
            for (var i = 0; i + 1 < Breaks.Count; i++)
            {
                double a = Breaks[i], b = Breaks[i + 1];
                pts.Add(new Vec2(a, Moment(a)));
                if (Loaded(a, b))
                    for (var k = 1; k < steps; k++) { var x = a + (b - a) * k / steps; pts.Add(new Vec2(x, Moment(x))); }
                pts.Add(new Vec2(b, Moment(b, left: true)));
            }
            return pts;
        }

        private (BeamExtreme, BeamExtreme) Extremes()
        {
            var scale = 1e-9 * Math.Max(1, _forces.Sum(f => Math.Abs(f.F)) + _lines.Sum(l => Math.Abs(l.Q * (l.X1 - l.X0))));
            BeamExtreme v = new(0, 0), m = new(0, 0);
            void Take(ref BeamExtreme best, double x, double value, double eps)
            {
                if (Math.Abs(value) > Math.Abs(best.Value) + eps) best = new BeamExtreme(x, value);
            }
            var mScale = scale * Math.Max(1, Model.Length);
            for (var i = 0; i + 1 < Breaks.Count; i++)
            {
                double a = Breaks[i], b = Breaks[i + 1];
                double va = Shear(a), vb = Shear(b, left: true);
                Take(ref v, a, va, scale);
                Take(ref v, b, vb, scale);
                Take(ref m, a, Moment(a), mScale);
                // Inside a stretch the shear force is linear; where it passes zero the moment has its vertex.
                if (va * vb < 0)
                {
                    var x0 = a + va / (va - vb) * (b - a);
                    Take(ref m, x0, Moment(x0), mScale);
                }
                Take(ref m, b, Moment(b, left: true), mScale);
            }
            return (v, m);
        }
    }

    /// <summary>
    /// Support reactions, shear force and bending moment of statically determinate straight beams under transverse loads:
    /// a beam on two hinged supports (pin and roller; overhangs allowed) or a cantilever with one fixed support. Pure
    /// computation from the equilibrium conditions, no host involved.
    /// </summary>
    public static class BeamStatics
    {
        /// <summary>
        /// True if the beam is statically determinate for transverse loads: exactly one fixed support, or exactly two hinged
        /// (pin/roller) supports at different positions. Two pins are accepted (only transverse loads are considered, so the
        /// horizontal reactions are zero). <paramref name="reason"/> names the problem otherwise.
        /// </summary>
        public static bool IsDeterminate(BeamModel model, out string? reason)
        {
            reason = null;
            if (!(model.Length > 0)) { reason = "length"; return false; }
            var s = model.Supports;
            if (s.Count == 1 && s[0].Kind == BeamSupportKind.Fixed) return true;
            if (s.Count == 2 && s.All(x => x.Kind != BeamSupportKind.Fixed) && Math.Abs(s[0].X - s[1].X) > 1e-9 * model.Length) return true;
            reason = s.Count == 0 ? "no support" : s.Count == 1 ? "unstable" : "indeterminate";
            return false;
        }

        /// <summary>Solves the beam, or returns null if it is not statically determinate (<see cref="IsDeterminate"/>).</summary>
        public static BeamSolution? Solve(BeamModel model)
        {
            if (!IsDeterminate(model, out _)) return null;
            var lines = model.LineLoads.Where(l => Math.Abs(l.X1 - l.X0) > 1e-12).ToList();
            var forces = new List<(double S, double F)>();
            foreach (var p in model.PointLoads) forces.Add((p.X, -p.F));
            var totalPoint = model.PointLoads.Sum(p => p.F);
            var totalLine = lines.Sum(l => l.Q * Math.Abs(l.X1 - l.X0));
            var total = totalPoint + totalLine;
            // Moment of the loads about a point x0 (downward loads right of x0 positive).
            double LoadMoment(double x0) => model.PointLoads.Sum(p => p.F * (p.X - x0)) + lines.Sum(l => l.Q * Math.Abs(l.X1 - l.X0) * ((l.X0 + l.X1) / 2 - x0));

            var reactions = new List<BeamReaction>();
            var couples = new List<(double S, double C)>();
            if (model.Supports.Count == 1)
            {
                var sup = model.Supports[0];
                forces.Add((sup.X, total));
                // The clamping couple makes the moment vanish beyond the beam: C = Σ f·s − Σ Q·c (forces upward positive).
                var c = forces.Sum(f => f.F * f.S) - lines.Sum(l => l.Q * Math.Abs(l.X1 - l.X0) * (l.X0 + l.X1) / 2);
                couples.Add((sup.X, c));
                // Bending moment in the beam at the wall: on the side of the beam that carries the loads.
                var wallMoment = sup.X <= model.Length / 2 ? c : -c;
                reactions.Add(new BeamReaction(sup, total, wallMoment));
            }
            else
            {
                var ordered = model.Supports.OrderBy(s => s.X).ToList();
                BeamSupport a = ordered[0], b = ordered[1];
                var rb = LoadMoment(a.X) / (b.X - a.X);
                var ra = total - rb;
                forces.Add((a.X, ra));
                forces.Add((b.X, rb));
                // Reactions in the order of the model's supports.
                foreach (var s in model.Supports) reactions.Add(new BeamReaction(s, ReferenceEquals(s, a) ? ra : rb, 0));
            }
            return new BeamSolution(model, reactions, forces, couples);
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

        /// <summary>
        /// Symbolic value "c·factor" written compactly: "F/2", "F·L/4", "3·q·L²/8", "−F·L", "0.123·F·L" (decimal when no short
        /// fraction fits).
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
            return sign + Ann.Fmt(a, 3) + "·" + factor;
        }
    }
}
