using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using InventaSketch.Core;

namespace InventaSketch.Modules.Electrical
{
    /// <summary>
    /// AC analysis of the series/parallel network (round 6, engineering-calculations.md section 2.1), with complex impedances
    /// at the source frequency: R, jωL and 1/(jωC) add in series and combine as 1/Σ(1/Zᵢ) in parallel.
    /// <list type="bullet">
    /// <item>A circuit fed by alternating current (an AC source, a generator on AC or a transformer) is in "AC mode": voltages
    /// in series and currents in parallel add as magnitudes only when the elements are in phase (all resistive, all
    /// inductive or all capacitive); mixed groups get no sum relation, the phasor relations below take their place.</item>
    /// <item>An AC voltage source across a load of resistors, inductors and capacitors (fuses count as zero impedance) gives the
    /// source the impedance |Z| and the phase angle φ = arg Z of the load, every element its current magnitude and the phases
    /// of its current and voltage against the source voltage; the reactive parts take over the source frequency.</item>
    /// <item>One inductor and one capacitor in a series or parallel group (with resistors or alone) form a resonant circuit:
    /// f₀ = 1/(2π√(LC)), quality factor Q = √(L/C)/R (series) or R·√(C/L) (parallel), bandwidth B = f₀/Q.</item>
    /// </list>
    /// Values are RMS magnitudes; the relations are explicit (solved for their result only), except f₀, which also gives L or C.
    /// </summary>
    internal sealed partial class ElecNetwork
    {
        private const double TwoPi = 2 * Math.PI;

        // ------------------------------------------------------------------ AC mode

        /// <summary>Elements that make their circuit an AC circuit.</summary>
        private static bool FeedsAc(SketchObject o) => o.Type switch
        {
            "elec.source" => o.Str("kind", "dc") == "ac",
            "elec.transformer" => true,
            "elec.generator" => o.Str("supply", "none") is "ac" or "ac3",
            _ => false,
        };

        /// <summary>Marks the trees of circuits that contain an element feeding alternating current.</summary>
        private void MarkAc(IEnumerable<string> elements, List<Tree> roots)
        {
            var ac = new HashSet<string>(StringComparer.Ordinal);
            foreach (var id in elements)
                if (_doc.Get(id) is { } o && FeedsAc(o) && _componentOf.TryGetValue(id, out var c)) ac.Add(c);
            if (ac.Count == 0) return;
            void Mark(Tree t, bool value)
            {
                t.Ac = value;
                foreach (var k in t.Kids) Mark(k, value);
            }
            foreach (var t in roots)
            {
                var leaf = Leaves(t).FirstOrDefault();
                if (leaf?.Obj is string obj && _componentOf.TryGetValue(obj, out var c) && ac.Contains(c)) Mark(t, true);
            }
        }

        /// <summary>Phase class of a leaf: resistive (also diodes, lamps, transformer primaries), inductive, capacitive, own (an AC load with its own power factor), none (ideal conductor).</summary>
        private static char PhaseClass(Tree leaf)
        {
            var d = leaf.Def!;
            if (d.AcLoad) return 'Z';
            if (d.L is not null) return 'L';
            if (d.C is not null) return 'C';
            if (d.ZeroU && d.U is null) return '0';
            return 'R';
        }

        /// <summary>The group contains elements whose voltages or currents are not in phase.</summary>
        private static bool MixedPhase(Tree t)
        {
            var classes = Leaves(t).Select(PhaseClass).Where(c => c != '0').ToList();
            return classes.Distinct().Count() > 1 || classes.Contains('Z') && classes.Count > 1;
        }

        private bool HasVar(string obj, string key)
            => _doc.Get(obj) is { } o && ElectricalModule.FindType(o.Type) is ElecType t && t.Variables.Any(v => v.Key == key);

        // ------------------------------------------------------------------ impedances

        /// <summary>A leaf the phasor analysis can describe: resistance, inductance, capacitance or an ideal conductor.</summary>
        private static bool Phasor(Tree leaf)
        {
            var d = leaf.Def!;
            return !d.AcLoad && !d.Source && (d.R is not null || d.L is not null || d.C is not null || d.ZeroU && d.U is null);
        }

        /// <summary>The value key of a leaf (R, L or C), or null for an ideal conductor.</summary>
        private static string? ValueKey(Tree leaf) => leaf.Def!.R ?? leaf.Def.L ?? leaf.Def.C;

        /// <summary>Complex impedance of a tree at angular frequency <paramref name="w"/>; leaf values from <paramref name="value"/>.</summary>
        private static Complex Z(Tree n, double w, Func<Tree, double> value)
        {
            switch (n.Kind)
            {
                case 'L':
                    var d = n.Def!;
                    if (d.R is not null) return new Complex(value(n), 0);
                    if (d.L is not null) return new Complex(0, w * value(n));
                    if (d.C is not null) return new Complex(0, -1 / (w * value(n)));
                    return Complex.Zero;
                case 'S':
                    var s = Complex.Zero;
                    foreach (var k in n.Kids) s += Z(k, w, value);
                    return s;
                default:
                    var y = Complex.Zero;
                    foreach (var k in n.Kids) y += 1 / Z(k, w, value);
                    return 1 / y;
            }
        }

        /// <summary>Impedance of a tree as text: R as {z}, L as j·2π·f·{z}, C as 1/(j·2π·f·{z}); series +, parallel ∥.</summary>
        private static string ZText(Tree n, IReadOnlyDictionary<Tree, string> index, string f)
        {
            if (n.Kind == 'L')
            {
                var d = n.Def!;
                if (d.R is not null) return "{" + index[n] + "}";
                if (d.L is not null) return "j·2π·" + f + "·{" + index[n] + "}";
                if (d.C is not null) return "1/(j·2π·" + f + "·{" + index[n] + "})";
                return "0";
            }
            var s = string.Join(n.Kind == 'S' ? " + " : " ∥ ", n.Kids.Select(k => ZText(k, index, f)));
            return n.Kids.Count == 1 ? s : "(" + s + ")";
        }

        /// <summary>Voltage and current phasors of every leaf for a voltage <paramref name="v"/> across <paramref name="n"/>.</summary>
        private static void Phasors(Tree n, Complex v, Complex i, double w, Func<Tree, double> value, Dictionary<Tree, (Complex U, Complex I)> into)
        {
            switch (n.Kind)
            {
                case 'L':
                    into[n] = (v, i);
                    break;
                case 'S':
                    foreach (var k in n.Kids) Phasors(k, i * Z(k, w, value), i, w, value, into);
                    break;
                default:
                    foreach (var k in n.Kids) Phasors(k, v, v / Z(k, w, value), w, value, into);
                    break;
            }
        }

        private static double Finite(double x) => double.IsNaN(x) || double.IsInfinity(x) ? double.NaN : x;

        // ------------------------------------------------------------------ AC source across a load

        private static readonly I18nText ZName = new("Impedance of the load (complex, series/parallel)", "Impedanz der Last (komplex, Reihe/parallel)");
        private static readonly I18nText PhiName = new("Phase angle of the load φ = arg Z", "Phasenwinkel der Last φ = arg Z");
        private static readonly I18nText BranchName = new("Current of the element (AC, complex current divider)", "Strom des Bauteils (Wechselstrom, komplexe Stromteilung)");
        private static readonly I18nText PhaseIName = new("Phase of the element's current to the source voltage", "Phase des Bauteilstroms zur Quellenspannung");
        private static readonly I18nText PhaseUName = new("Phase of the element's voltage to the source voltage", "Phase der Bauteilspannung zur Quellenspannung");
        private static readonly I18nText FrequencyName = new("Source frequency", "Frequenz der Quelle");
        private static readonly I18nText AcValidity = new("sinusoidal steady state, ideal R, L and C, RMS values", "eingeschwungener sinusförmiger Zustand, ideale R, L und C, Effektivwerte");

        /// <summary>
        /// An AC voltage source (a single leaf) across a load of R, L, C and ideal conductors: impedance and phase angle of the
        /// load on the source, current and phases per element, the source frequency on the reactive parts.
        /// </summary>
        private void AcCircuit(Tree c, List<BoundRelation> into)
        {
            var src = c.Kids.FirstOrDefault(k => k.Source);
            var load = c.Kids.FirstOrDefault(k => !k.Source);
            if (src is not { Kind: 'L', Def.U: string srcU } || load is null) return;
            if (_doc.Get(src.Obj!) is not { Type: "elec.source" } so || so.Str("kind", "dc") != "ac") return;
            var leaves = Leaves(load).ToList();
            if (!leaves.All(Phasor)) return;
            var reactive = leaves.Any(l => l.Def!.L is not null || l.Def.C is not null);
            // The frequency is a parameter of the source (or in its value text): without it only a resistive load is analysed.
            var freq = AcValues.SourceFrequency(so);
            if (reactive && freq is null) return;
            var f = freq ?? 0;
            var fText = Hz(f);
            var w = TwoPi * f;
            var valued = leaves.Where(l => ValueKey(l) is not null).ToList();
            var index = new Dictionary<Tree, string>();
            for (var k = 0; k < valued.Count; k++) index[valued[k]] = "z" + k;
            var zText = ZText(load, index, fText);
            var srcObj = src.Obj!;

            // Variables of every relation: the leaf values; the per-element currents also the source voltage.
            Dictionary<string, VarRef> Map(params (string Key, VarRef Ref)[] extra)
            {
                var map = new Dictionary<string, VarRef>(StringComparer.Ordinal);
                foreach (var (k, r) in extra) map[k] = r;
                foreach (var kv in index) map[kv.Value] = new VarRef(kv.Key.Obj!, ValueKey(kv.Key)!);
                return map;
            }
            string[] Vars(params string[] head) => head.Concat(index.Values).ToArray();
            Func<Tree, double> Values(Func<string, double> v) => l => v(index[l]);
            Complex Total(Func<string, double> v) => Z(load, w, Values(v));
            Dictionary<Tree, (Complex U, Complex I)> Solve(Func<string, double> v)
            {
                // Unit source voltage (phase 0): the phasors of every element relative to the source.
                var values = Values(v);
                var into = new Dictionary<Tree, (Complex, Complex)>();
                Phasors(load, Complex.One, 1 / Z(load, w, values), w, values, into);
                return into;
            }

            if (HasVar(srcObj, "Z"))
            {
                var rel = new Relation("elec.ac.z.load", ZName, "{Z} = |" + zText + "|", Vars("Z")) { Source = "S7", Validity = AcValidity }
                    .Solve("Z", "|" + zText + "|", v => Finite(Total(v).Magnitude));
                Add(new BoundRelation(rel, Map(("Z", new VarRef(srcObj, "Z")))), into);
            }
            if (HasVar(srcObj, "phi"))
            {
                var rel = new Relation("elec.ac.phi.load", PhiName, "{phi} = arg(" + zText + ")", Vars("phi")) { Source = "S7", Validity = AcValidity }
                    .Solve("phi", "arg(" + zText + ")", v => Finite(Total(v).Phase));
                Add(new BoundRelation(rel, Map(("phi", new VarRef(srcObj, "phi")))), into);
            }

            foreach (var leaf in leaves)
            {
                var obj = leaf.Obj!;
                var leafRef = index.TryGetValue(leaf, out var lk) ? "{" + lk + "}" : "0";
                // Current magnitude: only where reactive parts make it differ from the DC sums (resistive circuits keep those).
                var iKey = leaf.Def!.I ?? (HasVar(obj, "I") ? "I" : null);
                if (reactive && iKey is not null)
                {
                    var rel = new Relation("elec.ac.branch", BranchName, "{y} = |{u}·Î(" + leafRef + ")|, Z = " + zText, Vars("y", "u")) { Source = "S7", Validity = AcValidity }
                        .Solve("y", "|{u}·Î(" + leafRef + ")|", v => Finite(v("u") * Solve(v)[leaf].I.Magnitude));
                    Add(new BoundRelation(rel, Map(("y", new VarRef(obj, iKey)), ("u", new VarRef(srcObj, srcU)))), into);
                }
                if (HasVar(obj, "phiI"))
                {
                    var rel = new Relation("elec.ac.phase.i", PhaseIName, "{y} = ∠Î(" + leafRef + "), Z = " + zText, Vars("y")) { Source = "S7", Validity = AcValidity }
                        .Solve("y", "∠Î(" + leafRef + ")", v => Finite(Solve(v)[leaf].I.Phase));
                    Add(new BoundRelation(rel, Map(("y", new VarRef(obj, "phiI")))), into);
                }
                if (HasVar(obj, "phiU") && ValueKey(leaf) is not null)
                {
                    var rel = new Relation("elec.ac.phase.u", PhaseUName, "{y} = ∠Û(" + leafRef + "), Z = " + zText, Vars("y")) { Source = "S7", Validity = AcValidity }
                        .Solve("y", "∠Û(" + leafRef + ")", v => Finite(Solve(v)[leaf].U.Phase));
                    Add(new BoundRelation(rel, Map(("y", new VarRef(obj, "phiU")))), into);
                }
                // The reactive parts work at the source frequency (a value from the source, like a marking).
                if ((leaf.Def.L is not null || leaf.Def.C is not null) && HasVar(obj, "f"))
                {
                    var fr = new Relation("elec.ac.f", FrequencyName, "{f} = " + fText, "f") { Source = "S7" }.Solve("f", fText, _ => f);
                    Add(new BoundRelation(fr, new Dictionary<string, VarRef>(StringComparer.Ordinal) { ["f"] = new VarRef(obj, "f") }), into);
                }
            }
        }

        private static string Hz(double f) => f.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture) + " Hz";


        // ------------------------------------------------------------------ resonant circuits

        private static readonly I18nText ResonanceName = new("Resonance frequency f₀ = 1/(2π√(LC))", "Resonanzfrequenz f₀ = 1/(2π√(LC))");
        private static readonly I18nText QSeriesName = new("Quality factor of a series resonant circuit", "Güte eines Reihenschwingkreises");
        private static readonly I18nText QParallelName = new("Quality factor of a parallel resonant circuit", "Güte eines Parallelschwingkreises");
        private static readonly Relation ResonanceLink = Relation.Equality("elec.ac.resonance.link", new I18nText("Same resonant circuit", "Gleicher Schwingkreis"));

        /// <summary>
        /// One inductor and one capacitor in a series or parallel group (with resistive parts or alone): the resonance frequency
        /// on both, and with resistors the quality factor (series: √(L/C)/R with R in series; parallel: R·√(C/L) with R in
        /// parallel).
        /// </summary>
        private void Resonance(Tree t, List<BoundRelation> into)
        {
            if (t.Absorbed) return;
            var ls = t.Kids.Where(k => k.Kind == 'L' && k.Def!.L is not null).ToList();
            var cs = t.Kids.Where(k => k.Kind == 'L' && k.Def!.C is not null).ToList();
            if (ls.Count != 1 || cs.Count != 1) return;
            var rest = t.Kids.Where(k => !ReferenceEquals(k, ls[0]) && !ReferenceEquals(k, cs[0])).ToList();
            if (!rest.All(k => Leaves(k).All(l => l.Def!.R is not null))) return;
            var l = ls[0];
            var c = cs[0];
            if (!HasVar(c.Obj!, "f0") || !HasVar(l.Obj!, "f0")) return;

            var f0 = new Relation("elec.ac.resonance", ResonanceName, "{f0} = 1/(2π·√({L}·{C}))", "f0", "L", "C") { Source = "S7", Validity = AcValidity }
                .Solve("f0", "1/(2π·√({L}·{C}))", v => 1 / (TwoPi * Math.Sqrt(v("L") * v("C"))))
                .Solve("L", "1/((2π·{f0})²·{C})", v => 1 / (Math.Pow(TwoPi * v("f0"), 2) * v("C")))
                .Solve("C", "1/((2π·{f0})²·{L})", v => 1 / (Math.Pow(TwoPi * v("f0"), 2) * v("L")));
            Add(new BoundRelation(f0, new Dictionary<string, VarRef>(StringComparer.Ordinal)
            {
                ["f0"] = new VarRef(c.Obj!, "f0"), ["L"] = new VarRef(l.Obj!, l.Def!.L!), ["C"] = new VarRef(c.Obj!, c.Def!.C!),
            }), into);
            Add(new BoundRelation(ResonanceLink, new Dictionary<string, VarRef>(StringComparer.Ordinal) { ["a"] = new VarRef(c.Obj!, "f0"), ["b"] = new VarRef(l.Obj!, "f0") }), into);

            if (rest.Count == 0 || !HasVar(c.Obj!, "Qf") || !HasVar(l.Obj!, "Qf")) return;
            var resistors = rest.SelectMany(Leaves).ToList();
            var index = new Dictionary<Tree, string>();
            for (var k = 0; k < resistors.Count; k++) index[resistors[k]] = "z" + k;
            double Eval(Tree n, Func<string, double> v)
            {
                if (n.Kind == 'L') return v(index[n]);
                return n.Kind == 'S' ? n.Kids.Sum(k => Eval(k, v)) : 1 / n.Kids.Sum(k => 1 / Eval(k, v));
            }
            string Text(Tree n)
            {
                if (n.Kind == 'L') return "{" + index[n] + "}";
                var s = string.Join(n.Kind == 'S' ? " + " : " ∥ ", n.Kids.Select(Text));
                return n.Kids.Count == 1 ? s : "(" + s + ")";
            }
            var series = t.Kind == 'S';
            // The resistive kids combine like the group: added in series, in parallel as 1/Σ(1/R).
            double R(Func<string, double> v) => series ? rest.Sum(k => Eval(k, v)) : 1 / rest.Sum(k => 1 / Eval(k, v));
            var rText = rest.Count == 1 ? Text(rest[0]) : "(" + string.Join(series ? " + " : " ∥ ", rest.Select(Text)) + ")";
            var vars = new[] { "Q", "L", "C" }.Concat(index.Values).ToArray();
            Relation q;
            if (series)
            {
                q = new Relation("elec.ac.quality.series", QSeriesName, "{Q} = √({L}/{C})/" + rText, vars) { Source = "S7", Validity = AcValidity }
                    .Solve("Q", "√({L}/{C})/" + rText, v => Math.Sqrt(v("L") / v("C")) / R(v));
                if (resistors.Count == 1) q.Solve("z0", "√({L}/{C})/{Q}", v => Math.Sqrt(v("L") / v("C")) / v("Q"));
            }
            else
            {
                q = new Relation("elec.ac.quality.parallel", QParallelName, "{Q} = " + rText + "·√({C}/{L})", vars) { Source = "S7", Validity = AcValidity }
                    .Solve("Q", rText + "·√({C}/{L})", v => R(v) * Math.Sqrt(v("C") / v("L")));
                if (resistors.Count == 1) q.Solve("z0", "{Q}·√({L}/{C})", v => v("Q") * Math.Sqrt(v("L") / v("C")));
            }
            var map = new Dictionary<string, VarRef>(StringComparer.Ordinal)
            {
                ["Q"] = new VarRef(c.Obj!, "Qf"), ["L"] = new VarRef(l.Obj!, l.Def.L!), ["C"] = new VarRef(c.Obj!, c.Def.C!),
            };
            foreach (var kv in index) map[kv.Value] = new VarRef(kv.Key.Obj!, kv.Key.Def!.R!);
            Add(new BoundRelation(q, map), into);
            Add(new BoundRelation(ResonanceLink, new Dictionary<string, VarRef>(StringComparer.Ordinal) { ["a"] = new VarRef(c.Obj!, "Qf"), ["b"] = new VarRef(l.Obj!, "Qf") }), into);
        }
    }
}
