using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using InventaSketch.Core;

namespace InventaSketch.Modules.Machines
{
    /// <summary>
    /// Element values of electric machines and drive blocks (Calc 3, engineering-calculations.md sections 2.5 and 3.3):
    /// synchronous speed and slip of the machine cross-sections, P = 2π·n·T and the efficiency of motors, ratio and
    /// efficiency of the gearbox and differential blocks, wheel speed and force. Speeds, torques and powers are magnitudes
    /// (n in rad/s internally, so P = T·n holds in SI). Every value is optional.
    /// <para>
    /// Links (<see cref="MachLinks"/>): blocks joined by a connector (first docked end → last docked end) pass n and T from
    /// the output of one to the input of the next (motor → gearbox → differential → wheel); drivetrain parts on a mechanical
    /// shaft share its speed (the mechanical module passes the torque along the shaft line). Rotating elements of both modules
    /// use the variable keys n, T and P, so the links meet without a code dependency between the modules.
    /// </para>
    /// </summary>
    internal static class MachValues
    {
        private static I18nText T(string en, string de) => new(en, de);

        internal sealed class Spec
        {
            public static readonly Spec Empty = new(Array.Empty<VarDef>(), Array.Empty<Relation>(), Array.Empty<ParamDef>(), Array.Empty<ValueCheck>(), null, null);

            public Spec(IReadOnlyList<VarDef> variables, IReadOnlyList<Relation> relations, IReadOnlyList<ParamDef> parameters, IReadOnlyList<ValueCheck> checks,
                (string N, string T)? input, (string N, string T)? output)
            {
                Variables = variables; Relations = relations; Params = parameters; Checks = checks; Input = input; Output = output;
            }

            public IReadOnlyList<VarDef> Variables { get; }
            public IReadOnlyList<Relation> Relations { get; }
            public IReadOnlyList<ParamDef> Params { get; }
            public IReadOnlyList<ValueCheck> Checks { get; }
            /// <summary>Variable keys of the mechanical input of a drive block (speed, torque), null if none.</summary>
            public (string N, string T)? Input { get; }
            /// <summary>Variable keys of the mechanical output of a drive block, null if none.</summary>
            public (string N, string T)? Output { get; }
        }

        // ------------------------------------------------------------------ parameters and variables

        private static ParamDef Text(string key, I18nText label, string? unit) => new(key, label, ParamKind.Text) { Unit = unit, Advanced = true };

        private static VarDef V(string key, I18nText name, I18nText symbol, QuantityKind kind, string? unit, string param, VarRelevance rel = VarRelevance.Card)
            => new(key, name, symbol, kind) { Unit = unit, Param = param, Relevance = rel };

        private static readonly I18nText SymT = T("T", "M");

        private static VarDef Speed(string key = "n", string param = "speed", I18nText? name = null, string sym = "n", VarRelevance rel = VarRelevance.Card)
            => V(key, name ?? T("Speed of rotation", "Drehzahl"), T(sym, sym), Quantities.RotationalSpeed, "min⁻¹", param, rel) with { Min = 0, Max = 1e5 };
        private static VarDef Torque(string key = "T", string param = "torque", I18nText? name = null, string en = "T", string de = "M", VarRelevance rel = VarRelevance.Card)
            => V(key, name ?? T("Torque", "Drehmoment"), T(en, de), Quantities.Torque, "N·m", param, rel) with { Min = 0, Max = 1e7 };
        private static VarDef Power(string key = "P", string param = "power", I18nText? name = null, string sym = "P", VarRelevance rel = VarRelevance.Card)
            => V(key, name ?? T("Mechanical power", "Mechanische Leistung"), T(sym, sym), Quantities.Power, "W", param, rel) with { Min = 0, Max = 1e9 };
        private static VarDef Eta(VarRelevance rel = VarRelevance.Engineer)
            => V("eta", T("Efficiency", "Wirkungsgrad"), T("η", "η"), Quantities.Efficiency, "%", "efficiency", rel) with { Min = 0, Max = 1 };
        private static VarDef ElPower() => Power("Pel", "powerEl", T("Electrical power", "Elektrische Leistung"), "P_el", VarRelevance.Engineer);
        private static VarDef RatedPower() => Power("PN", "ratedPower", T("Rated power", "Bemessungsleistung"), "P_N", VarRelevance.Engineer);
        private static VarDef RatedTorque() => Torque("TN", "ratedTorque", T("Rated torque", "Bemessungsmoment"), "T_N", "M_N", VarRelevance.Engineer);

        private static readonly ParamDef PSpeed = Text("speed", T("Speed of rotation", "Drehzahl"), "min⁻¹");
        private static readonly ParamDef PTorque = Text("torque", T("Torque", "Drehmoment"), "N·m");
        private static readonly ParamDef PPower = Text("power", T("Mechanical power", "Mechanische Leistung"), "W");
        private static readonly ParamDef PEta = Text("efficiency", T("Efficiency", "Wirkungsgrad"), "%");
        private static readonly ParamDef PElPower = Text("powerEl", T("Electrical power", "Elektrische Leistung"), "W");
        private static readonly ParamDef PRatedPower = Text("ratedPower", T("Rated power", "Bemessungsleistung"), "W");
        private static readonly ParamDef PRatedTorque = Text("ratedTorque", T("Rated torque", "Bemessungsmoment"), "N·m");

        private static ParamDef[] WithShow(params ParamDef[] ps) => ps.Append(ValueLabels.ShowValues).ToArray();

        // ------------------------------------------------------------------ relations

        /// <summary>P = 2π·n·T [S14, S15].</summary>
        private static Relation RotPower(string p, string n, string t, string id = "mach.power.rot") =>
            new Relation(id, T("Mechanical power", "Mechanische Leistung"), "{" + p + "} = 2π·{" + n + "}·{" + t + "}", p, n, t) { Source = "S15" }
                .Solve(p, "2π·{" + n + "}·{" + t + "}", v => v(n) * v(t))
                .Solve(t, "{" + p + "}/(2π·{" + n + "})", v => v(p) / v(n))
                .Solve(n, "{" + p + "}/(2π·{" + t + "})", v => v(p) / v(t));

        private static readonly Relation PowerRel = RotPower("P", "n", "T");

        /// <summary>Motor operation: P_mech = η·P_el [S14].</summary>
        private static readonly Relation MotorEfficiency =
            new Relation("mach.efficiency.motor", T("Motor efficiency", "Motorwirkungsgrad"), "{P} = {eta}·{Pel}", "P", "eta", "Pel")
            {
                Source = "S14",
                Validity = T("motor operation (electrical power in, mechanical power out)", "Motorbetrieb (elektrische Leistung hinein, mechanische heraus)"),
            }
                .Solve("P", "{eta}·{Pel}", v => v("eta") * v("Pel"))
                .Solve("Pel", "{P}/{eta}", v => v("P") / v("eta"))
                .Solve("eta", "{P}/{Pel}", v => v("P") / v("Pel"));

        /// <summary>Synchronous speed n_s = 60·f/p with p = pole count / 2 [S14, S15].</summary>
        private static readonly Relation SyncSpeed =
            new Relation("mach.speed.sync", T("Synchronous speed", "Synchrondrehzahl"), "{ns} = 60·{f}/({poles}/2)", "ns", "f", "poles") { Source = "S15" }
                .Solve("ns", "60·{f}/({poles}/2)", v => 4 * Math.PI * v("f") / v("poles"))
                .Solve("f", "{ns}·({poles}/2)/60", v => v("ns") * v("poles") / (4 * Math.PI))
                .Solve("poles", "120·{f}/{ns}", v => 4 * Math.PI * v("f") / v("ns"));

        /// <summary>Slip s = (n_s − n)/n_s [S15].</summary>
        private static readonly Relation Slip =
            new Relation("mach.slip", T("Slip", "Schlupf"), "{s} = ({ns} − {n})/{ns}", "s", "ns", "n") { Source = "S15" }
                .Solve("s", "({ns} − {n})/{ns}", v => (v("ns") - v("n")) / v("ns"))
                .Solve("n", "{ns}·(1 − {s})", v => v("ns") * (1 - v("s")))
                .Solve("ns", "{n}/(1 − {s})", v => v("n") / (1 - v("s")));

        /// <summary>Gearbox speed n₁ = i·n₂ [S18].</summary>
        private static readonly Relation BoxSpeed =
            new Relation("mach.gearbox.speed", T("Gear ratio", "Übersetzung"), "{n1} = {i}·{n2}", "n1", "i", "n2") { Source = "S18" }
                .Solve("n1", "{i}·{n2}", v => v("i") * v("n2"))
                .Solve("n2", "{n1}/{i}", v => v("n1") / v("i"))
                .Solve("i", "{n1}/{n2}", v => v("n1") / v("n2"));

        /// <summary>Gearbox torque T₂ = T₁·i·η [S18].</summary>
        private static readonly Relation BoxTorque =
            new Relation("mach.gearbox.torque", T("Output torque", "Abtriebsmoment"), "{T2} = {T1}·{i}·{eta}", "T2", "T1", "i", "eta") { Source = "S18" }
                .Solve("T2", "{T1}·{i}·{eta}", v => v("T1") * v("i") * v("eta"))
                .Solve("T1", "{T2}/({i}·{eta})", v => v("T2") / (v("i") * v("eta")))
                .Solve("eta", "{T2}/({T1}·{i})", v => v("T2") / (v("T1") * v("i")))
                .Solve("i", "{T2}/({T1}·{eta})", v => v("T2") / (v("T1") * v("eta")));

        private static readonly Relation BoxPowerIn = RotPower("P1", "n1", "T1", "mach.power.in");
        private static readonly Relation BoxPowerOut = RotPower("P2", "n2", "T2", "mach.power.out");

        /// <summary>Wheel: v = ω·r, F = T/r [S18].</summary>
        private static readonly Relation WheelSpeed =
            new Relation("mach.wheel.speed", T("Vehicle speed", "Fahrgeschwindigkeit"), "{v} = 2π·{n}·{r}", "v", "n", "r") { Source = "S18" }
                .Solve("v", "2π·{n}·{r}", v => v("n") * v("r"))
                .Solve("n", "{v}/(2π·{r})", v => v("v") / v("r"))
                .Solve("r", "{v}/(2π·{n})", v => v("v") / v("n"));

        private static readonly Relation WheelForce =
            new Relation("mach.wheel.force", T("Tractive force", "Zugkraft"), "{F} = {T}/{r}", "F", "T", "r") { Source = "S18" }
                .Solve("F", "{T}/{r}", v => v("T") / v("r"))
                .Solve("T", "{F}·{r}", v => v("F") * v("r"))
                .Solve("r", "{T}/{F}", v => v("T") / v("F"));

        // ------------------------------------------------------------------ specs per type

        private static readonly (string, string) Shaft = ("n", "T");

        private static readonly Dictionary<string, Spec> Specs = new(StringComparer.Ordinal);

        /// <summary>The values of the type <paramref name="typeId"/> (empty for parts without relations; cached).</summary>
        public static Spec For(string typeId)
        {
            lock (Specs)
            {
                if (!Specs.TryGetValue(typeId, out var s)) Specs[typeId] = s = Build(typeId);
                return s;
            }
        }

        private static Spec Build(string typeId) => typeId switch
        {
            "machines.radial" or "machines.axial" => Machine(),
            "machines.drive.motor" or "machines.drivetrain.emachine" => Motor(),
            "machines.drivetrain.engine" => Engine(),
            "machines.drive.gearbox" => Box(T("Gear ratio", "Übersetzung")),
            "machines.drive.differential" => Box(T("Axle ratio", "Achsübersetzung")),
            "machines.drive.wheel" => Wheel(),
            _ => Spec.Empty,
        };

        private static Spec Machine()
        {
            var vars = new[]
            {
                Speed(), Torque(), Power(),
                V("f", T("Supply frequency", "Speisefrequenz"), T("f", "f"), Quantities.Frequency, "Hz", "frequency") with { Min = 0, Max = 1e5 },
                Speed("ns", "syncSpeed", T("Synchronous speed", "Synchrondrehzahl"), "n_s", VarRelevance.Panel),
                V("s", T("Slip", "Schlupf"), T("s", "s"), Quantities.Ratio, "%", "slip", VarRelevance.Panel) with { Min = -1, Max = 1 },
                V("poles", T("Pole count", "Polzahl"), T("2p", "2p"), Quantities.Count, null, "poles", VarRelevance.Panel) with { Default = 8, Min = 2, Max = 200 },
                Eta(), ElPower(), RatedPower(), RatedTorque(),
            };
            var ps = WithShow(PSpeed, PTorque, PPower, Text("frequency", T("Supply frequency", "Speisefrequenz"), "Hz"),
                Text("syncSpeed", T("Synchronous speed", "Synchrondrehzahl"), "min⁻¹"), Text("slip", T("Slip", "Schlupf"), "%"),
                PEta, PElPower, PRatedPower, PRatedTorque);
            return new Spec(vars, new[] { PowerRel, SyncSpeed, Slip, MotorEfficiency }, ps, new[] { SlipSign(), Efficiency(), Overload() }, null, Shaft);
        }

        private static Spec Motor()
        {
            var vars = new[] { Speed(), Torque(), Power(), Eta(VarRelevance.Card), ElPower(), RatedPower(), RatedTorque() };
            return new Spec(vars, new[] { PowerRel, MotorEfficiency }, WithShow(PSpeed, PTorque, PPower, PEta, PElPower, PRatedPower, PRatedTorque),
                new[] { Efficiency(), Overload() }, null, Shaft);
        }

        private static Spec Engine()
        {
            var vars = new[] { Speed(), Torque(), Power(), RatedPower(), RatedTorque() };
            return new Spec(vars, new[] { PowerRel }, WithShow(PSpeed, PTorque, PPower, PRatedPower, PRatedTorque), new[] { Overload() }, null, Shaft);
        }

        private static Spec Box(I18nText ratioName)
        {
            var vars = new[]
            {
                V("i", ratioName, T("i", "i"), Quantities.Ratio, null, "ratio") with { Min = 1e-3, Max = 1e4 },
                Eta(VarRelevance.Card) with { Default = 1 },
                Speed("n2", "speedOut", T("Output speed", "Abtriebsdrehzahl"), "n₂"),
                Torque("T2", "torqueOut", T("Output torque", "Abtriebsmoment"), "T₂", "M₂"),
                Speed("n1", "speedIn", T("Input speed", "Antriebsdrehzahl"), "n₁", VarRelevance.Panel),
                Torque("T1", "torqueIn", T("Input torque", "Antriebsmoment"), "T₁", "M₁", VarRelevance.Panel),
                Power("P1", "powerIn", T("Input power", "Antriebsleistung"), "P₁", VarRelevance.Panel),
                Power("P2", "powerOut", T("Output power", "Abtriebsleistung"), "P₂", VarRelevance.Panel),
            };
            var ps = WithShow(Text("ratio", ratioName, null), Text("efficiency", T("Efficiency", "Wirkungsgrad"), "%"),
                Text("speedOut", T("Output speed", "Abtriebsdrehzahl"), "min⁻¹"), Text("torqueOut", T("Output torque", "Abtriebsmoment"), "N·m"),
                Text("speedIn", T("Input speed", "Antriebsdrehzahl"), "min⁻¹"), Text("torqueIn", T("Input torque", "Antriebsmoment"), "N·m"),
                Text("powerIn", T("Input power", "Antriebsleistung"), "W"), Text("powerOut", T("Output power", "Abtriebsleistung"), "W"));
            return new Spec(vars, new[] { BoxSpeed, BoxTorque, BoxPowerIn, BoxPowerOut }, ps, new[] { Efficiency() }, ("n1", "T1"), ("n2", "T2"));
        }

        private static Spec Wheel()
        {
            var vars = new[]
            {
                V("r", T("Dynamic rolling radius", "Dynamischer Rollradius"), T("r", "r"), Quantities.Length, "mm", "radius") with { Min = 1e-3, Max = 10 },
                Speed(), Torque(),
                V("v", T("Vehicle speed", "Fahrgeschwindigkeit"), T("v", "v"), Quantities.Speed, "km/h", "linSpeed") with { Min = 0, Max = 1e3 },
                V("F", T("Tractive force", "Zugkraft"), T("F", "F"), Quantities.Force, "N", "force", VarRelevance.Panel) with { Min = 0, Max = 1e8 },
                Power(rel: VarRelevance.Panel),
            };
            var ps = WithShow(Text("radius", T("Dynamic rolling radius", "Dynamischer Rollradius"), "mm"), PSpeed, PTorque,
                Text("linSpeed", T("Vehicle speed", "Fahrgeschwindigkeit"), "km/h"), Text("force", T("Tractive force", "Zugkraft"), "N"), PPower);
            return new Spec(vars, new[] { WheelSpeed, WheelForce, PowerRel }, ps, Array.Empty<ValueCheck>(), Shaft, null);
        }

        // ------------------------------------------------------------------ checks (section 4)

        private const double Rel = 1e-6;

        /// <summary>mach.slip.sign: an induction machine in motor mode above synchronous speed runs as a generator [S15].</summary>
        private static ValueCheck SlipSign() => new(
            "mach.slip.sign", CheckSeverity.Info,
            T("The speed is above the synchronous speed: an induction machine runs as a generator there (negative slip).",
              "Die Drehzahl liegt über der Synchrondrehzahl: eine Asynchronmaschine arbeitet dort als Generator (negativer Schlupf)."),
            "n > n_s in motor mode",
            c => c.Object.Str("rotor", "spm") == "asm" && c.Object.Str("mode", "motor") == "motor"
                 && c["n"] is double n && c["ns"] is double ns && n > ns * (1 + Rel))
        { Source = "S15" };

        /// <summary>mach.efficiency: η &gt; 1 or more power out than in [S1].</summary>
        private static ValueCheck Efficiency() => new(
            "mach.efficiency", CheckSeverity.Warning,
            T("More power out than in: an efficiency above 100 % is impossible. Check the entered efficiency, torques or powers.",
              "Mehr Leistung heraus als hinein: ein Wirkungsgrad über 100 % ist unmöglich. Eingetragenen Wirkungsgrad, Momente oder Leistungen prüfen."),
            "η ≤ 1; P_out ≤ P_in",
            c => c["eta"] is double eta && eta > 1 + Rel
                 || c["P2"] is double p2 && c["P1"] is double p1 && p2 > p1 * (1 + Rel)
                 || c["P"] is double p && c["Pel"] is double pel && p > pel * (1 + Rel))
        { Source = "S1" };

        /// <summary>mach.overload: power or torque above the entered rating [S14].</summary>
        private static ValueCheck Overload() => new(
            "mach.overload", CheckSeverity.Warning,
            T("The machine runs above its entered rating (power or torque). Check the load or choose a larger machine.",
              "Die Maschine läuft über ihrer eingetragenen Bemessung (Leistung oder Moment). Last prüfen oder eine größere Maschine wählen."),
            "P ≤ P_N; T ≤ T_N",
            c => c["P"] is double p && c["PN"] is double pn && p > pn * (1 + Rel)
                 || c["T"] is double t && c["TN"] is double tn && t > tn * (1 + Rel))
        { Source = "S14" };
    }

    /// <summary>Port relations of machines and drive blocks (see <see cref="MachValues"/>).</summary>
    internal static class MachLinks
    {
        private static I18nText T(string en, string de) => new(en, de);

        private static readonly Relation SameSpeed = Relation.Equality("mach.link.speed", T("Speed passed on", "Drehzahl weitergegeben"));
        private static readonly Relation SameTorque = Relation.Equality("mach.link.torque", T("Torque passed on", "Drehmoment weitergegeben"));
        private static readonly Relation ShaftSpeed = Relation.Equality("mach.shaft.speed", T("Same speed on one shaft", "Gleiche Drehzahl auf einer Welle"));

        /// <summary>Connectors between drive blocks, per document: (upstream, downstream).</summary>
        private static readonly ConditionalWeakTable<SketchDocument, List<(string Up, string Down)>> Chains = new();

        private static BoundRelation Eq(Relation r, string a, string ka, string b, string kb)
            => new(r, new Dictionary<string, VarRef>(StringComparer.Ordinal) { ["a"] = new VarRef(a, ka), ["b"] = new VarRef(b, kb) });

        /// <summary>The drive chain: lines whose first and last docked ends sit on two blocks with a mechanical output and input.</summary>
        private static MachValues.Spec? SpecOf(SketchDocument doc, string id) => doc.Get(id) is { } x ? MachValues.For(x.Type) : null;

        private static List<(string Up, string Down)> ChainOf(SketchDocument doc)
            => Chains.GetValue(doc, d =>
            {
                var list = new List<(string, string)>();
                foreach (var line in d.Objects.Values)
                {
                    if (!line.IsPointGeometry || line.Attach is null) continue;
                    var ends = line.Attach.Where(a => a is not null && a.ObjectId != line.Id && d.Get(a.ObjectId) is { IsPointGeometry: false })
                        .Select(a => a!.ObjectId).ToList();
                    if (ends.Count < 2 || ends[0] == ends[ends.Count - 1]) continue;
                    var (up, down) = (ends[0], ends[ends.Count - 1]);
                    if (SpecOf(d, up)?.Output is not null && SpecOf(d, down)?.Input is not null) list.Add((up, down));
                }
                return list;
            });

        /// <summary>The port relations element <paramref name="o"/> takes part in.</summary>
        public static IEnumerable<BoundRelation> For(MachType type, SketchObject o, RenderContext ctx)
        {
            var doc = ctx.Doc;
            var registry = ctx.Registry ?? type.Owner;
            var list = new List<BoundRelation>();
            foreach (var (up, down) in ChainOf(doc))
            {
                if (up != o.Id && down != o.Id) continue;
                var (no, to) = SpecOf(doc, up)!.Output!.Value;
                var (ni, ti) = SpecOf(doc, down)!.Input!.Value;
                list.Add(Eq(SameSpeed, up, no, down, ni));
                list.Add(Eq(SameTorque, up, to, down, ti));
            }

            // Drivetrain parts on a mechanical shaft share the speed of its shaft line (Core ShaftLines; the torque along the
            // line comes from the shaft).
            if (registry is not null && (type.DrivesShaft || type.Id == "machines.drive.wheel"))
                list.AddRange(ShaftLines.Of(registry, doc).Links(o.Id, ShaftSpeed, null));
            return list;
        }
    }
}
