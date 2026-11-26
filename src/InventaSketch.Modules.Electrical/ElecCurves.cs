using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Electrical
{
    /// <summary>
    /// Calc 5 (linked charts): Bode plot and step response of the RC, RL or RLC network a resistor, capacitor or inductor is
    /// wired into [S7]. The circuit is the element's circuit (<see cref="ElecNetwork.CircuitOf"/>); its first resistor,
    /// capacitor and inductor (in drawing order) form the network, taken as a series circuit driven by the source:
    /// <list type="bullet">
    /// <item>RC, output across C: first-order low pass, f_c = 1/(2π·R·C), τ = R·C;</item>
    /// <item>RL, output across R: first-order low pass, f_c = R/(2π·L), τ = L/R;</item>
    /// <item>RLC, output across C: second-order low pass, f₀ = 1/(2π·√(L·C)), ζ = (R/2)·√(C/L).</item>
    /// </list>
    /// |H| = 1/√(1 + (f/f_c)²) (−3.01 dB at f_c), φ = −arctan(f/f_c); second order |H| = 1/√((1 − r²)² + (2ζr)²),
    /// φ = −arctan2(2ζr, 1 − r²) with r = f/f₀. Step response (output over its final value): 1 − e^(−t/τ) (63.2 % at τ), and
    /// the under-, critically and over-damped forms of the second-order system. Values that are not known are assumed
    /// (R = 1 kΩ, C = 100 nF, L = 10 mH) and named in a note. Dragging the corner-frequency point enters C (RC, RLC) or L
    /// (RL) for the new frequency; R stays.
    /// </summary>
    internal sealed partial class ElecType : ICharacteristics
    {
        private static I18nText Tx(string en, string de) => new(en, de);

        public const string BodeSet = "bode", StepSet = "step";

        private static readonly CharacteristicInfo BodeInfo = new(BodeSet, Tx("Bode plot", "Bode-Diagramm"));
        private static readonly CharacteristicInfo StepInfo = new(StepSet, Tx("Step response", "Sprungantwort"));

        private bool IsPassive => _id is "elec.resistor" or "elec.capacitor" or "elec.capacitor.polarized" or "elec.inductor";

        private static bool IsR(string? type) => type == "elec.resistor";
        private static bool IsC(string? type) => type is "elec.capacitor" or "elec.capacitor.polarized";
        private static bool IsL(string? type) => type == "elec.inductor";

        /// <summary>The first resistor, capacitor and inductor of the circuit of <paramref name="id"/> (drawing order) and whether there are more.</summary>
        internal static (string? R, string? C, string? L, bool More) Network(SketchDocument doc, string id)
        {
            var members = new HashSet<string>(ElecNetwork.Of(doc).CircuitOf(id), StringComparer.Ordinal);
            var order = doc.Pages.SelectMany(p => p.Order).Where(members.Contains).ToList();
            string? First(Func<string?, bool> kind) => order.FirstOrDefault(m => kind(doc.Get(m)?.Type));
            int Count(Func<string?, bool> kind) => order.Count(m => kind(doc.Get(m)?.Type));
            return (First(IsR), First(IsC), First(IsL), Count(IsR) > 1 || Count(IsC) > 1 || Count(IsL) > 1);
        }

        public IReadOnlyList<CharacteristicInfo> CharacteristicSets(SketchObject o, RenderContext ctx)
        {
            if (!IsPassive) return Array.Empty<CharacteristicInfo>();
            var (r, c, l, _) = Network(ctx.Doc, o.Id);
            return r is not null && (c is not null || l is not null) ? new[] { BodeInfo, StepInfo } : Array.Empty<CharacteristicInfo>();
        }

        public CharacteristicSet? Characteristic(SketchObject o, string key, CharacteristicContext c)
        {
            if (!IsPassive || key is not (BodeSet or StepSet)) return null;
            var net = Filter.Of(c);
            if (net is null) return null;
            return key == BodeSet ? Bode(net) : Step(net);
        }

        /// <summary>Passive parts name their linked charts; a junction dot names the wires docked on it (<see cref="ElecType.JunctionNode"/>).</summary>
        public override bool HasDependents => IsPassive || JunctionNode;

        public override IEnumerable<string> Dependents(SketchObject o, RenderContext ctx)
            => IsPassive ? Characteristics.LinkedCharts(ctx.Doc, o.Id)
                : JunctionNode ? ctx.Doc.Objects.Values.Where(x => x.Id != o.Id && x.Attach is not null && x.Attach.Any(a => a?.ObjectId == o.Id)).Select(x => x.Id).ToList()
                : Array.Empty<string>();

        /// <summary>Passive parts add their characteristic charts; a junction dot with wires passing it adds "Make bridge (not connected)".</summary>
        public override IEnumerable<ContextAction> Actions(SketchObject o, RenderContext ctx)
        {
            if (IsPassive) return base.Actions(o, ctx).Concat(Characteristics.InsertActions(this, o, ctx));
            if (JunctionNode && Bridges.ForElement(o, ctx, Owner) is { } bridge) return base.Actions(o, ctx).Append(bridge);
            return base.Actions(o, ctx);
        }

        // ------------------------------------------------------------------ the network

        /// <summary>The RC, RL or RLC network with its values (SI), the elements holding them and the notes on assumptions.</summary>
        internal sealed record Filter(char Kind, double R, double C, double L, string? CId, string? LId, List<I18nText> Notes)
        {
            public bool SecondOrder => Kind == 'Z';
            /// <summary>Corner (first order) or natural (second order) frequency in Hz.</summary>
            public double F0 => Kind switch { 'C' => 1 / (2 * Math.PI * R * C), 'L' => R / (2 * Math.PI * L), _ => 1 / (2 * Math.PI * Math.Sqrt(L * C)) };
            public double Zeta => (R / 2) * Math.Sqrt(C / L);
            public double Tau => Kind == 'L' ? L / R : R * C;

            public static Filter? Of(CharacteristicContext c)
            {
                var (r, cap, ind, more) = Network(c.Doc, c.Object.Id);
                if (r is null || cap is null && ind is null) return null;
                var notes = new List<I18nText>();
                double Val(string id, string key, double fallback, string text)
                {
                    // The values of the circuit's elements (in the solver scope through the circuit relations; an element
                    // outside it is solved on its own).
                    if (c.Value(id, key) is double v && v > 0) return v;
                    if (c.Table.Get(id, key) is null && SolveOne(c, id, key) is double s) return s;
                    notes.Add(Tx(text + " assumed", text + " angenommen"));
                    return fallback;
                }
                var rv = Val(r, "R", 1e3, "R = 1 kΩ");
                var cv = cap is null ? 0 : Val(cap, "C", 100e-9, "C = 100 nF");
                var lv = ind is null ? 0 : Val(ind, "L", 10e-3, "L = 10 mH");
                if (more) notes.Add(Tx("First R, C and L of the circuit used (series)", "Erstes R, C und L des Stromkreises verwendet (Reihe)"));
                var kind = cap is not null && ind is not null ? 'Z' : cap is not null ? 'C' : 'L';
                return new Filter(kind, rv, cv, lv, cap, ind, notes);
            }

            /// <summary>A value of an element outside the solved scope (solved on its own), or null.</summary>
            private static double? SolveOne(CharacteristicContext c, string id, string key)
            {
                var t = ValueSolver.Solve(c.Doc, c.Registry, new[] { id }, new ValueSolveOptions { BudgetMs = 50 });
                return t.Get(id, key)?.Value is double v && v > 0 ? v : null;
            }
        }

        /// <summary>Magnitude (dB) and phase (°) of the network at <paramref name="f"/>.</summary>
        internal static (double Db, double Deg) Response(Filter n, double f)
        {
            var r = f / n.F0;
            if (!n.SecondOrder) return (-10 * Math.Log10(1 + r * r), -Math.Atan(r) * 180 / Math.PI);
            var z = n.Zeta;
            return (-10 * Math.Log10((1 - r * r) * (1 - r * r) + 4 * z * z * r * r), -Math.Atan2(2 * z * r, 1 - r * r) * 180 / Math.PI);
        }

        /// <summary>Step response (output over its final value, 0 … 1 and above for overshoot) at time <paramref name="t"/> (s).</summary>
        internal static double StepResponse(Filter n, double t)
        {
            if (t <= 0) return 0;
            if (!n.SecondOrder) return 1 - Math.Exp(-t / n.Tau);
            var w0 = 2 * Math.PI * n.F0;
            var z = n.Zeta;
            if (z < 1 - 1e-9)
            {
                var wd = w0 * Math.Sqrt(1 - z * z);
                return 1 - Math.Exp(-z * w0 * t) * (Math.Cos(wd * t) + z / Math.Sqrt(1 - z * z) * Math.Sin(wd * t));
            }
            if (z <= 1 + 1e-9) return 1 - (1 + w0 * t) * Math.Exp(-w0 * t);
            var root = Math.Sqrt(z * z - 1);
            double s1 = -w0 * (z - root), s2 = -w0 * (z + root);
            return 1 - (s2 * Math.Exp(s1 * t) - s1 * Math.Exp(s2 * t)) / (s2 - s1);
        }

        private static string Freq(double f, Lang lang) => ValueFormat.Format(f, Characteristics.UnitOf(Quantities.Frequency, "Hz"), lang);

        private static CharacteristicSet Bode(Filter n)
        {
            var set = new CharacteristicSet(BodeSet, BodeInfo.Name,
                new CharacteristicAxis(Tx("Frequency f", "Frequenz f"), "Hz") { Log = true, Min = n.F0 / 100, Max = n.F0 * 100 },
                new CharacteristicAxis(Tx("Magnitude", "Betrag"), "dB"),
                new CharacteristicAxis(Tx("Phase φ", "Phase φ"), "°"));
            const int steps = 240;
            var mag = new List<Vec2>();
            var ph = new List<Vec2>();
            for (var i = 0; i <= steps; i++)
            {
                var f = n.F0 / 100 * Math.Pow(1e4, (double)i / steps);
                var (db, deg) = Response(n, f);
                mag.Add(new Vec2(f, db));
                ph.Add(new Vec2(f, deg));
            }
            set.Curves.Add(new CharacteristicCurve("magnitude", Tx("Magnitude |H|", "Betrag |H|"), mag));
            set.Curves.Add(new CharacteristicCurve("phase", Tx("Phase φ", "Phase φ"), ph) { Panel = 1 });
            var (db0, _) = Response(n, n.F0);
            var sym = n.SecondOrder ? "f₀" : "f_c";
            var label = new I18nText(sym + " = " + Freq(n.F0, Lang.En), sym + " = " + Freq(n.F0, Lang.De));
            var target = n.Kind == 'L' ? n.LId : n.CId;
            var fixedR = n.R; var fixedL = n.L;
            var kind = n.Kind;
            set.Points.Add(new CharacteristicPoint("fc", n.SecondOrder ? Tx("Natural frequency", "Kennfrequenz") : Tx("Corner frequency", "Eckfrequenz"), n.F0, db0)
            {
                Label = label,
                Motion = target is null ? PointMotion.None : PointMotion.AlongX,
                Write = target is null ? null : f =>
                {
                    if (!(f > 0)) return Array.Empty<ValueWrite>();
                    var w = 2 * Math.PI * f;
                    return kind switch
                    {
                        'C' => new[] { new ValueWrite(target, "C", 1 / (w * fixedR)) },
                        'L' => new[] { new ValueWrite(target, "L", fixedR / w) },
                        _ => new[] { new ValueWrite(target, "C", 1 / (w * w * fixedL)) },
                    };
                },
            });
            set.Lines.Add(new CharacteristicLine("f0", label, n.F0));
            if (n.SecondOrder)
                set.Notes.Add(new I18nText("ζ = " + ValueFormat.Format(n.Zeta, Quantities.Ratio, Lang.En), "ζ = " + ValueFormat.Format(n.Zeta, Quantities.Ratio, Lang.De)));
            set.Notes.AddRange(n.Notes);
            return set;
        }

        private static CharacteristicSet Step(Filter n)
        {
            double tEnd;
            if (!n.SecondOrder) tEnd = 5 * n.Tau;
            else
            {
                var w0 = 2 * Math.PI * n.F0;
                tEnd = n.Zeta < 1 ? Math.Min(60, 5 / Math.Max(n.Zeta, 0.08)) / w0 : (6 * (n.Zeta + Math.Sqrt(n.Zeta * n.Zeta - 1))) / w0;
            }
            var unit = Characteristics.UnitOf(Quantities.Time_, tEnd < 1e-3 ? "µs" : tEnd < 1 ? "ms" : "s");
            var set = new CharacteristicSet(StepSet, StepInfo.Name,
                new CharacteristicAxis(Tx("Time t", "Zeit t"), unit.Symbol) { Min = 0, Max = unit.FromSi(tEnd) },
                new CharacteristicAxis(Tx("Output / final value", "Ausgang / Endwert"), "%") { Min = 0 });
            const int steps = 200;
            var pts = Enumerable.Range(0, steps + 1).Select(i => tEnd * i / steps).Select(t => new Vec2(unit.FromSi(t), 100 * StepResponse(n, t))).ToList();
            set.Curves.Add(new CharacteristicCurve("step", n.SecondOrder ? Tx("Output u_C(t)", "Ausgang u_C(t)") : n.Kind == 'L' ? Tx("Current i(t)", "Strom i(t)") : Tx("Output u_C(t)", "Ausgang u_C(t)"), pts));
            if (!n.SecondOrder)
            {
                var tau = Tx("τ = " + ValueFormat.Format(n.Tau, unit, Lang.En), "τ = " + ValueFormat.Format(n.Tau, unit, Lang.De));
                set.Points.Add(new CharacteristicPoint("tau", Tx("Time constant", "Zeitkonstante"), unit.FromSi(n.Tau), 100 * (1 - Math.Exp(-1))) { Label = tau });
            }
            else if (n.Zeta < 1)
            {
                var wd = 2 * Math.PI * n.F0 * Math.Sqrt(1 - n.Zeta * n.Zeta);
                var tp = Math.PI / wd;
                set.Points.Add(new CharacteristicPoint("peak", Tx("Overshoot", "Überschwingen"), unit.FromSi(tp), 100 * StepResponse(n, tp)));
            }
            set.Lines.Add(new CharacteristicLine("final", Tx("Final value", "Endwert"), 100) { Vertical = false, Panel = 0 });
            set.Notes.AddRange(n.Notes);
            return set;
        }
    }
}
