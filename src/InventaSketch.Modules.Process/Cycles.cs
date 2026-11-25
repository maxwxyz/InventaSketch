using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Process
{
    /// <summary>
    /// An idealised cycle in data coordinates (x to the right, y up): one path per change of state (path i runs from state
    /// i + 1 to state i + 2, the last one back to state 1), the state points, an optional saturation dome and the data range
    /// the diagram shows.
    /// </summary>
    public sealed record CycleDiagram(
        IReadOnlyList<IReadOnlyList<Vec2>> Paths,
        IReadOnlyList<Vec2> States,
        IReadOnlyList<Vec2>? Dome,
        Rect Range)
    {
        /// <summary>Thermal efficiency of the ideal cycle when it follows from the model (air-standard and Carnot cycles).</summary>
        public double? Efficiency { get; init; }
    }

    /// <summary>
    /// Idealised thermodynamic cycles for teaching diagrams. The air-standard cycles on the p-v diagram are computed for an
    /// ideal gas with constant heat capacities (isentropes p·v^γ = const, isotherms p·v = const). The saturation domes of the
    /// T-s and log p-h diagrams are qualitative shapes (normalised axes without scale values), not property data of a real
    /// substance; the vapour-region curves use the ideal-gas relations named at each method. Nothing here is fitted to
    /// measured data.
    /// </summary>
    public static class Cycles
    {
        // ------------------------------------------------------------------ saturation dome (T-s)

        /// <summary>Critical point and dome foot of the qualitative T-s dome (normalised axes 0…1).</summary>
        public const double TsCriticalS = 0.48, TsCriticalT = 0.88, TsFootT = 0.08;

        private static double Tt(double t) => Geo.Clamp((TsCriticalT - t) / (TsCriticalT - TsFootT), 0, 1);

        /// <summary>Entropy of the saturated liquid at temperature <paramref name="t"/>: s_c − 0.30·√τ − 0.05·τ, τ = (T_c − T)/(T_c − T_0).</summary>
        public static double SatLiquidS(double t) { var x = Tt(t); return TsCriticalS - 0.30 * Math.Sqrt(x) - 0.05 * x; }

        /// <summary>Entropy of the saturated vapour at temperature <paramref name="t"/>: s_c + 0.28·√τ + 0.12·τ.</summary>
        public static double SatVapourS(double t) { var x = Tt(t); return TsCriticalS + 0.28 * Math.Sqrt(x) + 0.12 * x; }

        /// <summary>The T-s dome from the foot of the liquid line over the critical point to the foot of the vapour line.</summary>
        public static List<Vec2> TsDome(int n = 32)
        {
            var left = new List<Vec2>();
            var right = new List<Vec2>();
            for (var i = 0; i <= n; i++)
            {
                // Denser near the critical point, where the dome bends most.
                var f = 1 - Math.Pow(1 - (double)i / n, 2);
                var t = TsFootT + (TsCriticalT - TsFootT) * f;
                left.Add(new Vec2(SatLiquidS(t), t));
                right.Add(new Vec2(SatVapourS(t), t));
            }
            right.Reverse();
            return left.Concat(right.Skip(1)).ToList();
        }

        private static readonly Rect UnitRange = new(0, 0, 1, 1);

        private static List<Vec2> Segment(Vec2 a, Vec2 b) => new() { a, b };

        /// <summary>
        /// Clausius-Rankine cycle on the T-s diagram: 1→2 pump (isentropic, drawn slightly exaggerated), 2→3 heating,
        /// evaporation and optional superheating at the upper pressure, 3→4 isentropic expansion, 4→1 condensation.
        /// The superheated isobar uses the ideal-gas relation T = T_g·exp((s − s_g)/c_p).
        /// </summary>
        public static CycleDiagram Rankine(bool superheat = true, double tLow = 0.22, double tHigh = 0.62, double tMax = 0.86)
        {
            const double cp = 0.6, t0 = 0.6; // c_p and the offset of the absolute temperature on the normalised axis
            var s1 = SatLiquidS(tLow);
            var p1 = new Vec2(s1, tLow);
            var p2 = new Vec2(s1, tLow + 0.035);
            var heat = new List<Vec2> { p2 };
            var sOff = SatLiquidS(p2.Y) - s1;
            for (var i = 1; i <= 16; i++)
            {
                var t = p2.Y + (tHigh - p2.Y) * i / 16.0;
                var blend = (double)i / 16;
                heat.Add(new Vec2(SatLiquidS(t) - sOff * (1 - blend), t));
            }
            var sg = SatVapourS(tHigh);
            heat.Add(new Vec2(sg, tHigh));
            var p3 = new Vec2(sg, tHigh);
            if (superheat)
            {
                var s3 = sg + cp * Math.Log((tMax + t0) / (tHigh + t0));
                for (var i = 1; i <= 12; i++)
                {
                    var s = sg + (s3 - sg) * i / 12.0;
                    heat.Add(new Vec2(s, (tHigh + t0) * Math.Exp((s - sg) / cp) - t0));
                }
                p3 = heat[heat.Count - 1];
            }
            var p4 = new Vec2(p3.X, tLow);
            return new CycleDiagram(
                new IReadOnlyList<Vec2>[] { Segment(p1, p2), heat, Segment(p3, p4), Segment(p4, p1) },
                new[] { p1, p2, p3, p4 }, TsDome(), UnitRange);
        }

        /// <summary>
        /// Carnot cycle on the T-s diagram (a rectangle): 1→2 isentropic compression, 2→3 isothermal heat addition,
        /// 3→4 isentropic expansion, 4→1 isothermal heat rejection, placed inside the dome (wet-vapour Carnot cycle). The
        /// axes carry no scale, so no efficiency is stated.
        /// </summary>
        public static CycleDiagram CarnotTs(double tLow = 0.25, double tHigh = 0.62)
        {
            double sa = SatLiquidS(tHigh), sb = SatVapourS(tHigh);
            var p1 = new Vec2(sa, tLow);
            var p2 = new Vec2(sa, tHigh);
            var p3 = new Vec2(sb, tHigh);
            var p4 = new Vec2(sb, tLow);
            return new CycleDiagram(
                new IReadOnlyList<Vec2>[] { Segment(p1, p2), Segment(p2, p3), Segment(p3, p4), Segment(p4, p1) },
                new[] { p1, p2, p3, p4 }, TsDome(), UnitRange);
        }

        // ------------------------------------------------------------------ air-standard cycles (p-v)

        /// <summary>Isentrope p·v^γ = const through (v0, p0), sampled from v0 to v1.</summary>
        public static List<Vec2> Isentrope(double v0, double p0, double v1, double gamma, int n = 24)
        {
            var list = new List<Vec2>(n + 1);
            for (var i = 0; i <= n; i++)
            {
                var v = v0 + (v1 - v0) * i / n;
                list.Add(new Vec2(v, p0 * Math.Pow(v0 / v, gamma)));
            }
            return list;
        }

        /// <summary>Isotherm p·v = const through (v0, p0), sampled from v0 to v1.</summary>
        public static List<Vec2> Isotherm(double v0, double p0, double v1, int n = 24)
        {
            var list = new List<Vec2>(n + 1);
            for (var i = 0; i <= n; i++)
            {
                var v = v0 + (v1 - v0) * i / n;
                list.Add(new Vec2(v, p0 * v0 / v));
            }
            return list;
        }

        private static Rect PvRange(IEnumerable<Vec2> states)
        {
            var list = states.ToList();
            return new Rect(0, 0, list.Max(s => s.X) * 1.08, list.Max(s => s.Y) * 1.08);
        }

        /// <summary>Otto efficiency η = 1 − r^(1−γ).</summary>
        public static double OttoEfficiency(double r, double gamma = 1.4) => 1 - Math.Pow(r, 1 - gamma);

        /// <summary>Diesel efficiency η = 1 − r^(1−γ)·(r_c^γ − 1)/(γ·(r_c − 1)).</summary>
        public static double DieselEfficiency(double r, double rc, double gamma = 1.4)
            => 1 - Math.Pow(r, 1 - gamma) * (Math.Pow(rc, gamma) - 1) / (gamma * (rc - 1));

        /// <summary>
        /// Otto (constant-volume) cycle, v2 = 1, p1 = 1: 1→2 isentropic compression by the compression ratio r, 2→3 heat
        /// addition at constant volume (p3 = α·p2), 3→4 isentropic expansion, 4→1 heat rejection at constant volume.
        /// </summary>
        public static CycleDiagram Otto(double r = 8, double alpha = 2.5, double gamma = 1.4)
        {
            r = Geo.Clamp(r, 1.5, 30);
            alpha = Geo.Clamp(alpha, 1.1, 10);
            var p1 = new Vec2(r, 1);
            var p2 = new Vec2(1, Math.Pow(r, gamma));
            var p3 = new Vec2(1, alpha * p2.Y);
            var p4 = new Vec2(r, p3.Y * Math.Pow(1 / r, gamma));
            return new CycleDiagram(
                new IReadOnlyList<Vec2>[] { Isentrope(r, 1, 1, gamma), Segment(p2, p3), Isentrope(1, p3.Y, r, gamma), Segment(p4, p1) },
                new[] { p1, p2, p3, p4 }, null, PvRange(new[] { p1, p3 })) { Efficiency = OttoEfficiency(r, gamma) };
        }

        /// <summary>
        /// Diesel (constant-pressure) cycle, v2 = 1, p1 = 1: 1→2 isentropic compression by r, 2→3 heat addition at constant
        /// pressure up to the cut-off ratio r_c = v3/v2, 3→4 isentropic expansion, 4→1 heat rejection at constant volume.
        /// </summary>
        public static CycleDiagram Diesel(double r = 18, double rc = 2, double gamma = 1.4)
        {
            r = Geo.Clamp(r, 1.5, 30);
            rc = Geo.Clamp(rc, 1.05, Math.Max(1.06, r * 0.8));
            var p1 = new Vec2(r, 1);
            var p2 = new Vec2(1, Math.Pow(r, gamma));
            var p3 = new Vec2(rc, p2.Y);
            var p4 = new Vec2(r, p3.Y * Math.Pow(rc / r, gamma));
            return new CycleDiagram(
                new IReadOnlyList<Vec2>[] { Isentrope(r, 1, 1, gamma), Segment(p2, p3), Isentrope(rc, p3.Y, r, gamma), Segment(p4, p1) },
                new[] { p1, p2, p3, p4 }, null, PvRange(new[] { p1, p2 })) { Efficiency = DieselEfficiency(r, rc, gamma) };
        }

        /// <summary>
        /// Carnot cycle of an ideal gas on the p-v diagram with the overall volume ratio r = v4/v2 split evenly into the
        /// adiabatic and the isothermal volume ratio (each √r), R = 1, T_low = 1: 1→2 isentropic compression to
        /// T_high = (√r)^(γ−1), 2→3 isothermal expansion, 3→4 isentropic expansion, 4→1 isothermal compression.
        /// η = 1 − T_low/T_high.
        /// </summary>
        public static CycleDiagram CarnotPv(double r = 8, double gamma = 1.4)
        {
            r = Geo.Clamp(r, 1.5, 30);
            var a = Math.Sqrt(r);
            var th = Math.Pow(a, gamma - 1);
            var p1 = new Vec2(a, 1 / a);
            var p2 = new Vec2(1, th);
            var p3 = new Vec2(a, th / a);
            var p4 = new Vec2(r, 1 / r);
            return new CycleDiagram(
                new IReadOnlyList<Vec2>[] { Isentrope(a, 1 / a, 1, gamma), Isotherm(1, th, a), Isentrope(a, th / a, r, gamma), Isotherm(r, 1 / r, a) },
                new[] { p1, p2, p3, p4 }, null, PvRange(new[] { p4, p2 })) { Efficiency = 1 - 1 / th };
        }

        // ------------------------------------------------------------------ vapour-compression refrigeration (log p-h)

        /// <summary>Critical point and dome foot of the qualitative log p-h dome (normalised axes 0…1, y = log p).</summary>
        public const double PhCriticalH = 0.5, PhCriticalY = 0.86, PhFootY = 0.05;

        /// <summary>Axis units per unit of ln p (pressure ratio 4 spans 0.37 of the axis).</summary>
        public const double PhLogScale = 0.267;

        private static double Ty(double y) => Geo.Clamp((PhCriticalY - y) / (PhCriticalY - PhFootY), 0, 1);

        /// <summary>Enthalpy of the saturated liquid at y = log p: h_c − 0.30·√τ − 0.06·τ.</summary>
        public static double SatLiquidH(double y) { var x = Ty(y); return PhCriticalH - 0.30 * Math.Sqrt(x) - 0.06 * x; }

        /// <summary>Enthalpy of the saturated vapour at y = log p: h_c + 0.36·√τ − 0.16·τ.</summary>
        public static double SatVapourH(double y) { var x = Ty(y); return PhCriticalH + 0.36 * Math.Sqrt(x) - 0.16 * x; }

        public static List<Vec2> PhDome(int n = 32)
        {
            var left = new List<Vec2>();
            var right = new List<Vec2>();
            for (var i = 0; i <= n; i++)
            {
                var f = 1 - Math.Pow(1 - (double)i / n, 2);
                var y = PhFootY + (PhCriticalY - PhFootY) * f;
                left.Add(new Vec2(SatLiquidH(y), y));
                right.Add(new Vec2(SatVapourH(y), y));
            }
            right.Reverse();
            return left.Concat(right.Skip(1)).ToList();
        }

        /// <summary>Largest pressure ratio that keeps the condensing pressure below the critical point in the diagram.</summary>
        public const double PhMaxRatio = 7.5;

        /// <summary>
        /// Vapour-compression refrigeration cycle on the log p-h diagram: 1→2 isentropic compression, 2→3 desuperheating and
        /// condensation at the upper pressure (optionally subcooled), 3→4 isenthalpic throttling, 4→1 evaporation at the
        /// lower pressure (optionally superheated). The compression follows dh = v·dp with p·v ≈ const (ideal gas), so
        /// h2 − h1 = c·ln(p2/p1).
        /// </summary>
        public static CycleDiagram Refrigeration(double ratio = 4, bool superheat = true, bool subcool = false)
        {
            const double yLow = 0.25, c = 0.09;
            ratio = Geo.Clamp(ratio, 1.5, PhMaxRatio);
            var yHigh = yLow + PhLogScale * Math.Log(ratio);
            var p1 = new Vec2(SatVapourH(yLow) + (superheat ? 0.04 : 0), yLow);
            var p2 = new Vec2(p1.X + c * Math.Log(ratio), yHigh);
            var p3 = new Vec2(SatLiquidH(yHigh) - (subcool ? 0.035 : 0), yHigh);
            var p4 = new Vec2(p3.X, yLow);
            return new CycleDiagram(
                new IReadOnlyList<Vec2>[] { Segment(p1, p2), Segment(p2, p3), Segment(p3, p4), Segment(p4, p1) },
                new[] { p1, p2, p3, p4 }, PhDome(), UnitRange);
        }
    }
}
