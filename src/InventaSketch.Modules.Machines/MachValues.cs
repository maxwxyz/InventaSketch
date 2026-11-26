using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using InventaSketch.Core;

namespace InventaSketch.Modules.Machines
{
    /// <summary>
    /// Element values of electric machines and drive blocks (Calc 3, engineering-calculations.md sections 2.5 and 3.3):
    /// synchronous speed and slip of the machine cross-sections, P = 2π·n·T and the efficiency of motors (P = η·P_el) or, in
    /// generator mode (parameter <c>mode</c>), of generators (P_el = η·P), ratio and
    /// efficiency of the gearbox and differential blocks, wheel speed and force. Speeds, torques and powers are magnitudes
    /// (n in rad/s internally, so P = T·n holds in SI). Every value is optional.
    /// <para>
    /// Links (<see cref="MachLinks"/>): blocks joined by a connector (first docked end → last docked end) pass n and T from
    /// the output of one to the input of the next (motor → gearbox → differential → wheel); drivetrain parts on a mechanical
    /// shaft share its speed (the mechanical module passes the torque along the shaft line). Rotating elements of both modules
    /// use the variable keys n, T and P, so the links meet without a code dependency between the modules. A machine wired to
    /// a switchgear frequency inverter (known by its type id) takes the inverter's output frequency; the inverter's own value
    /// port passes voltage and power as well. The maximum permissible speed n_max is checked against n and n_s.
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
            /// <summary>Relations that depend on the object (the efficiency rule of its operating mode); null = none.</summary>
            public Func<SketchObject, IEnumerable<Relation>>? Local { get; init; }
            /// <summary>
            /// Variable key of the moment of inertia this element presents at its mechanical input (a load's own inertia, the
            /// inertia reflected through a gearbox, a vehicle mass at the wheel); null if none.
            /// </summary>
            public string? InertiaIn { get; init; }
            /// <summary>Variable key of the inertia of everything downstream of the output (a motor's load inertia); null if none.</summary>
            public string? InertiaOut { get; init; }
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

        /// <summary>Generator operation: P_el = η·P_mech (driven, e.g. by a turbine or an engine) [S14].</summary>
        private static readonly Relation GeneratorEfficiency =
            new Relation("mach.efficiency.generator", T("Generator efficiency", "Generatorwirkungsgrad"), "{Pel} = {eta}·{P}", "Pel", "eta", "P")
            {
                Source = "S14",
                Validity = T("generator operation (mechanical power in, electrical power out)", "Generatorbetrieb (mechanische Leistung hinein, elektrische heraus)"),
            }
                .Solve("Pel", "{eta}·{P}", v => v("eta") * v("P"))
                .Solve("P", "{Pel}/{eta}", v => v("Pel") / v("eta"))
                .Solve("eta", "{Pel}/{P}", v => v("Pel") / v("P"));

        /// <summary>True if the machine runs as a generator (parameter <c>mode</c>; motor by default).</summary>
        internal static bool IsGenerator(SketchObject o) => o.Str("mode", "motor") == "generator";

        /// <summary>The efficiency rule of the operating mode: motor P = η·P_el (default), generator P_el = η·P.</summary>
        private static IEnumerable<Relation> ModeEfficiency(SketchObject o) => new[] { IsGenerator(o) ? GeneratorEfficiency : MotorEfficiency };

        /// <summary>The operating mode as a value parameter of machines that do not draw it (the drivetrain e-machine).</summary>
        private static readonly ParamDef PMode = Common.Mode with { Advanced = true };

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

        /// <summary>Synchronous machines run at the synchronous speed: n = n_s [S15].</summary>
        private static readonly Relation SyncLock =
            new Relation("mach.speed.synchronous", T("Synchronous machine: speed equals the synchronous speed", "Synchronmaschine: Drehzahl gleich Synchrondrehzahl"), "{n} = {ns}", "n", "ns")
            {
                Source = "S15",
                Validity = T("steady state, synchronous machine (magnets or wound field)", "stationärer Betrieb, Synchronmaschine (Magnete oder Erregerwicklung)"),
            }
                .Solve("n", "{ns}", v => v("ns"))
                .Solve("ns", "{n}", v => v("n"));

        /// <summary>Three-phase electrical power at the terminals P_el = √3·U·I·cos φ [S14, S15].</summary>
        private static readonly Relation ThreePhasePower =
            new Relation("mach.power.el3", T("Three-phase electrical power", "Elektrische Drehstromleistung"), "{Pel} = √3·{U}·{I}·{cosphi}", "Pel", "U", "I", "cosphi") { Source = "S15" }
                .Solve("Pel", "√3·{U}·{I}·{cosphi}", v => Math.Sqrt(3) * v("U") * v("I") * v("cosphi"))
                .Solve("I", "{Pel}/(√3·{U}·{cosphi})", v => v("Pel") / (Math.Sqrt(3) * v("U") * v("cosphi")))
                .Solve("U", "{Pel}/(√3·{I}·{cosphi})", v => v("Pel") / (Math.Sqrt(3) * v("I") * v("cosphi")))
                .Solve("cosphi", "{Pel}/(√3·{U}·{I})", v => v("Pel") / (Math.Sqrt(3) * v("U") * v("I")));

        /// <summary>Single-phase electrical power P_el = U·I·cos φ [S7].</summary>
        private static readonly Relation SinglePhasePower =
            new Relation("mach.power.el1", T("Single-phase electrical power", "Elektrische Wechselstromleistung"), "{Pel} = {U}·{I}·{cosphi}", "Pel", "U", "I", "cosphi") { Source = "S7" }
                .Solve("Pel", "{U}·{I}·{cosphi}", v => v("U") * v("I") * v("cosphi"))
                .Solve("I", "{Pel}/({U}·{cosphi})", v => v("Pel") / (v("U") * v("cosphi")))
                .Solve("U", "{Pel}/({I}·{cosphi})", v => v("Pel") / (v("I") * v("cosphi")))
                .Solve("cosphi", "{Pel}/({U}·{I})", v => v("Pel") / (v("U") * v("I")));

        /// <summary>The terminal power rule of the phase count (three-phase by default; none for 2, 4, 5 or 6 phases).</summary>
        private static IEnumerable<Relation> Terminal(SketchObject o) => o.Int("phases", 3) switch
        {
            1 => new[] { SinglePhasePower },
            3 => new[] { ThreePhasePower },
            _ => Array.Empty<Relation>(),
        };

        /// <summary>Total inertia at the motor shaft J = J_M + J_L [S15].</summary>
        private static readonly Relation InertiaTotal =
            new Relation("mach.inertia.total", T("Total inertia at the motor shaft", "Gesamtträgheitsmoment an der Motorwelle"), "{J} = {JM} + {JL}", "J", "JM", "JL") { Source = "S15" }
                .Solve("J", "{JM} + {JL}", v => v("JM") + v("JL"))
                .Solve("JL", "{J} − {JM}", v => v("J") - v("JM"))
                .Solve("JM", "{J} − {JL}", v => v("J") - v("JL"));

        /// <summary>Run-up time from standstill t_a = J·2π·n/(T_M − T_L) at a constant accelerating torque [S15].</summary>
        private static readonly Relation RunUp =
            new Relation("mach.runup", T("Run-up time", "Hochlaufzeit"), "{ta} = {J}·2π·{n}/({TM} − {T})", "ta", "J", "n", "TM", "T")
            {
                Source = "S15",
                Validity = T("constant mean motor torque and constant load torque during the run-up, from standstill to n",
                    "konstantes mittleres Motormoment und konstantes Lastmoment während des Hochlaufs, aus dem Stillstand bis n"),
            }
                .Solve("ta", "{J}·2π·{n}/({TM} − {T})", v => v("TM") > v("T") ? v("J") * v("n") / (v("TM") - v("T")) : double.NaN)
                .Solve("TM", "{T} + {J}·2π·{n}/{ta}", v => v("T") + v("J") * v("n") / v("ta"))
                .Solve("J", "{ta}·({TM} − {T})/(2π·{n})", v => v("ta") * (v("TM") - v("T")) / v("n"));

        /// <summary>Inertia reflected through a gear stage J₁ = J₂/i² (losses neglected) [S18].</summary>
        private static readonly Relation Reflect =
            new Relation("mach.gearbox.inertia", T("Inertia reflected to the input", "Auf den Antrieb reduziertes Trägheitsmoment"), "{J1} = {J2}/{i}²", "J1", "J2", "i")
            {
                Source = "S18",
                Validity = T("gear losses and the gearbox's own inertia neglected", "Getriebeverluste und Eigenträgheit des Getriebes vernachlässigt"),
            }
                .Solve("J1", "{J2}/{i}²", v => v("J2") / (v("i") * v("i")))
                .Solve("J2", "{J1}·{i}²", v => v("J1") * v("i") * v("i"))
                .Solve("i", "√({J2}/{J1})", v => Math.Sqrt(v("J2") / v("J1")));

        /// <summary>Vehicle mass seen at the wheel J = m·r² (rolling without slip) [S18].</summary>
        private static readonly Relation WheelInertia =
            new Relation("mach.wheel.inertia", T("Vehicle mass as inertia at the wheel", "Fahrzeugmasse als Trägheitsmoment am Rad"), "{J} = {m}·{r}²", "J", "m", "r") { Source = "S18" }
                .Solve("J", "{m}·{r}²", v => v("m") * v("r") * v("r"))
                .Solve("m", "{J}/{r}²", v => v("J") / (v("r") * v("r")));

        private static readonly I18nText FrequencyName = T("Supply frequency", "Speisefrequenz");
        private static readonly I18nText PolesName = T("Pole count (default 4)", "Polzahl (Vorgabe 4)");

        /// <summary>Supply frequency, pole count, synchronous speed and slip of the motor block and the drivetrain e-machine.</summary>
        private static readonly VarDef[] FrequencyVars =
        {
            V("f", FrequencyName, T("f", "f"), Quantities.Frequency, "Hz", "frequency", VarRelevance.Panel) with { Min = 0, Max = 1e5 },
            V("poles", PolesName, T("2p", "2p"), Quantities.Count, null, "poles", VarRelevance.Panel) with { Default = 4, Min = 2, Max = 200 },
            Speed("ns", "syncSpeed", T("Synchronous speed", "Synchrondrehzahl"), "n_s", VarRelevance.Panel),
            V("s", T("Slip", "Schlupf"), T("s", "s"), Quantities.Ratio, "%", "slip", VarRelevance.Engineer) with { Min = -1, Max = 1 },
        };

        private static readonly ParamDef[] FrequencyParams =
        {
            Text("frequency", FrequencyName, "Hz"), Text("poles", PolesName, null),
            Text("syncSpeed", T("Synchronous speed", "Synchrondrehzahl"), "min⁻¹"), Text("slip", T("Slip", "Schlupf"), "%"),
        };

        private static readonly I18nText MaxSpeedName = T("Maximum permissible speed (datasheet)", "Höchstzulässige Drehzahl (Datenblatt)");

        /// <summary>The maximum permissible speed of a machine (check <c>mach.speed.max</c>; a converter's frequency can exceed it).</summary>
        private static VarDef MaxSpeedVar() => Speed("nmax", "maxSpeed", MaxSpeedName, "n_max", VarRelevance.Panel);

        private static readonly ParamDef PMaxSpeed = Text("maxSpeed", MaxSpeedName, "min⁻¹");

        private static readonly I18nText VoltageName = T("Terminal voltage (line-to-line for three phases)", "Klemmenspannung (verkettet bei Drehstrom)");
        private static readonly I18nText CurrentName = T("Line current", "Leiterstrom");
        private static readonly I18nText CosName = T("Power factor cos φ", "Leistungsfaktor cos φ");

        /// <summary>Electrical terminal values: they also meet the values of cables and feeders of other modules (keys U, I, f).</summary>
        private static readonly VarDef[] TerminalVars =
        {
            V("U", VoltageName, T("U", "U"), Quantities.Voltage, "V", "voltage", VarRelevance.Panel) with { Min = 0, Max = 1e5 },
            V("I", CurrentName, T("I", "I"), Quantities.Current, "A", "current", VarRelevance.Panel) with { Min = 0, Max = 1e5 },
            V("cosphi", CosName, T("cos φ", "cos φ"), Quantities.Ratio, null, "powerFactor", VarRelevance.Engineer) with { Min = 0.05, Max = 1 },
        };

        private static readonly ParamDef[] TerminalParams =
        {
            Text("voltage", VoltageName, "V"), Text("current", CurrentName, "A"), Text("powerFactor", CosName, null),
        };

        private static readonly I18nText RotorInertiaName = T("Rotor inertia of the motor", "Läuferträgheitsmoment des Motors");
        private static readonly I18nText LoadInertiaName = T("Load inertia at the motor shaft (from the drive chain, or entered)", "Lastträgheitsmoment an der Motorwelle (aus dem Antriebsstrang oder eingegeben)");
        private static readonly I18nText TotalInertiaName = T("Total inertia at the motor shaft", "Gesamtträgheitsmoment an der Motorwelle");
        private static readonly I18nText RunUpTorqueName = T("Mean motor torque during the run-up", "Mittleres Motormoment während des Hochlaufs");
        private static readonly I18nText RunUpTimeName = T("Run-up time to the speed n", "Hochlaufzeit auf die Drehzahl n");

        /// <summary>Inertia and run-up variables of motors (the load torque during the run-up is the shaft torque T).</summary>
        private static readonly VarDef[] RunUpVars =
        {
            V("JM", RotorInertiaName, T("J_M", "J_M"), Quantities.Inertia, "kg·m²", "rotorInertia", VarRelevance.Panel) with { Min = 0, Max = 1e6 },
            V("JL", LoadInertiaName, T("J_L", "J_L"), Quantities.Inertia, "kg·m²", "loadInertia", VarRelevance.Panel) with { Min = 0, Max = 1e7 },
            V("J", TotalInertiaName, T("J", "J"), Quantities.Inertia, "kg·m²", "inertia", VarRelevance.Engineer) with { Min = 0, Max = 1e7 },
            Torque("TM", "runUpTorque", RunUpTorqueName, "T_M", "M_M", VarRelevance.Panel),
            V("ta", RunUpTimeName, T("t_a", "t_a"), Quantities.Time_, "s", "runUpTime", VarRelevance.Panel) with { Min = 0, Max = 1e6 },
        };

        private static readonly ParamDef[] RunUpParams =
        {
            Text("rotorInertia", RotorInertiaName, "kg·m²"), Text("loadInertia", LoadInertiaName, "kg·m²"), Text("inertia", TotalInertiaName, "kg·m²"),
            Text("runUpTorque", RunUpTorqueName, "N·m"), Text("runUpTime", RunUpTimeName, "s"),
        };

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
            "machines.drive.motor" => Motor(false),
            "machines.drivetrain.emachine" => Motor(true),
            "machines.drivetrain.engine" => Engine(),
            "machines.drive.gearbox" => Box(T("Gear ratio", "Übersetzung")),
            "machines.drive.differential" => Box(T("Axle ratio", "Achsübersetzung")),
            "machines.drive.wheel" => Wheel(),
            "machines.drive.load" => Load(),
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
                Eta(), ElPower(), RatedPower(), RatedTorque(), MaxSpeedVar(),
            }.Concat(TerminalVars).Concat(RunUpVars).ToArray();
            var ps = WithShow(new[] { PSpeed, PTorque, PPower, Text("frequency", T("Supply frequency", "Speisefrequenz"), "Hz"),
                Text("syncSpeed", T("Synchronous speed", "Synchrondrehzahl"), "min⁻¹"), Text("slip", T("Slip", "Schlupf"), "%"),
                PEta, PElPower, PRatedPower, PRatedTorque, PMaxSpeed }.Concat(TerminalParams).Concat(RunUpParams).ToArray());
            return new Spec(vars, new[] { PowerRel, SyncSpeed, Slip, InertiaTotal, RunUp }, ps, new[] { SlipSign(), Efficiency(), Overload(), RunUpTorque(), MaxSpeed() }, null, Shaft)
            {
                Local = o => ModeEfficiency(o).Concat(Terminal(o)).Concat(IsSynchronousSection(o) ? new[] { SyncLock } : Array.Empty<Relation>()),
                InertiaOut = "JL",
            };
        }

        /// <summary>A cross-section with a synchronous rotor (magnets or a wound field; the axial-flux machine has magnets).</summary>
        private static bool IsSynchronousSection(SketchObject o)
            => o.Type == "machines.axial" || Common.RotorKind(o) is "spm" or "ipm" or "spoke" or "eesm";

        /// <summary>
        /// Drive motor block and drivetrain e-machine: P = 2π·n·T and the efficiency rule of the operating mode (motor by default,
        /// generator P_el = η·P when driven). The drivetrain e-machine does not draw its mode: it gets the choice as a value
        /// parameter (<paramref name="modeParam"/>).
        /// </summary>
        private static Spec Motor(bool modeParam)
        {
            var vars = new[] { Speed(), Torque(), Power(), Eta(VarRelevance.Card), ElPower(), RatedPower(), RatedTorque() }
                .Concat(FrequencyVars).Append(MaxSpeedVar()).Concat(TerminalVars).Concat(RunUpVars).ToArray();
            var ps = new List<ParamDef> { PSpeed, PTorque, PPower, PEta, PElPower, PRatedPower, PRatedTorque };
            ps.AddRange(FrequencyParams);
            ps.Add(PMaxSpeed);
            ps.AddRange(TerminalParams);
            ps.AddRange(RunUpParams);
            if (modeParam) ps.Add(PMode);
            // The drive motor block has no machine kind: synchronous speed and slip always apply. The drivetrain e-machine
            // follows its kind: synchronous machines run at n = n_s, induction machines with slip, reluctance machines without
            // a mains-frequency relation.
            var fixedRels = modeParam ? new[] { PowerRel, InertiaTotal, RunUp } : new[] { PowerRel, SyncSpeed, Slip, InertiaTotal, RunUp };
            return new Spec(vars, fixedRels, WithShow(ps.ToArray()), new[] { Efficiency(), Overload(), RunUpTorque(), MaxSpeed() }, null, Shaft)
            {
                Local = modeParam ? o => ModeEfficiency(o).Concat(Terminal(o)).Concat(KindSpeed(o)) : o => ModeEfficiency(o).Concat(Terminal(o)),
                InertiaOut = "JL",
            };
        }

        /// <summary>The speed rules of the drivetrain e-machine's kind (PMSM/EESM: n = n_s; ASM: slip; SRM: none).</summary>
        private static IEnumerable<Relation> KindSpeed(SketchObject o) => o.Str("kind", "pmsm") switch
        {
            "asm" => new[] { SyncSpeed, Slip },
            "srm" => Array.Empty<Relation>(),
            _ => new[] { SyncSpeed, SyncLock },
        };

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
            vars = vars.Concat(new[]
            {
                V("J2", T("Load inertia at the output", "Lastträgheitsmoment am Abtrieb"), T("J₂", "J₂"), Quantities.Inertia, "kg·m²", "inertiaOut", VarRelevance.Panel) with { Min = 0, Max = 1e7 },
                V("J1", T("Load inertia reflected to the input", "Auf den Antrieb reduziertes Lastträgheitsmoment"), T("J₁", "J₁"), Quantities.Inertia, "kg·m²", "inertiaIn", VarRelevance.Panel) with { Min = 0, Max = 1e7 },
            }).ToArray();
            ps = ps.Take(ps.Length - 1).Concat(new[]
            {
                Text("inertiaOut", T("Load inertia at the output", "Lastträgheitsmoment am Abtrieb"), "kg·m²"),
                Text("inertiaIn", T("Load inertia reflected to the input", "Auf den Antrieb reduziertes Lastträgheitsmoment"), "kg·m²"),
                ValueLabels.ShowValues,
            }).ToArray();
            return new Spec(vars, new[] { BoxSpeed, BoxTorque, BoxPowerIn, BoxPowerOut, Reflect }, ps, new[] { Efficiency() }, ("n1", "T1"), ("n2", "T2"))
            {
                InertiaIn = "J1",
                InertiaOut = "J2",
            };
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
            vars = vars.Concat(new[]
            {
                V("m", T("Vehicle mass driven through this wheel or axle", "Über dieses Rad oder diese Achse angetriebene Fahrzeugmasse"), T("m", "m"), Quantities.Mass_, "kg", "mass", VarRelevance.Panel) with { Min = 0, Max = 1e6 },
                V("J", T("Inertia of the vehicle mass at the wheel", "Trägheitsmoment der Fahrzeugmasse am Rad"), T("J", "J"), Quantities.Inertia, "kg·m²", "inertia", VarRelevance.Engineer) with { Min = 0, Max = 1e7 },
            }).ToArray();
            var ps = WithShow(Text("radius", T("Dynamic rolling radius", "Dynamischer Rollradius"), "mm"), PSpeed, PTorque,
                Text("linSpeed", T("Vehicle speed", "Fahrgeschwindigkeit"), "km/h"), Text("force", T("Tractive force", "Zugkraft"), "N"), PPower,
                Text("mass", T("Vehicle mass driven through this wheel or axle", "Über dieses Rad oder diese Achse angetriebene Fahrzeugmasse"), "kg"),
                Text("inertia", T("Inertia of the vehicle mass at the wheel", "Trägheitsmoment der Fahrzeugmasse am Rad"), "kg·m²"));
            return new Spec(vars, new[] { WheelSpeed, WheelForce, PowerRel, WheelInertia }, ps, Array.Empty<ValueCheck>(), Shaft, null) { InertiaIn = "J" };
        }

        /// <summary>
        /// Driven load (block): speed, load torque, power and moment of inertia at its input; the chain passes the inertia
        /// upstream through gearboxes (J/i²) to the motor, which computes its run-up time.
        /// </summary>
        private static Spec Load()
        {
            var vars = new[]
            {
                Speed(), Torque(name: T("Load torque", "Lastmoment")), Power(name: T("Load power", "Lastleistung")),
                V("J", T("Moment of inertia of the load", "Trägheitsmoment der Last"), T("J", "J"), Quantities.Inertia, "kg·m²", "inertia") with { Min = 0, Max = 1e7 },
            };
            var ps = WithShow(PSpeed, Text("torque", T("Load torque", "Lastmoment"), "N·m"), Text("power", T("Load power", "Lastleistung"), "W"),
                Text("inertia", T("Moment of inertia of the load", "Trägheitsmoment der Last"), "kg·m²"));
            return new Spec(vars, new[] { PowerRel }, ps, Array.Empty<ValueCheck>(), Shaft, null) { InertiaIn = "J" };
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

        /// <summary>
        /// mach.efficiency: η &gt; 1 or more power out than in (motor: P &gt; P_el; generator: P_el &gt; P; gearbox P₂ &gt; P₁) [S1].
        /// </summary>
        private static ValueCheck Efficiency() => new(
            "mach.efficiency", CheckSeverity.Warning,
            T("More power out than in: an efficiency above 100 % is impossible. Check the entered efficiency, torques or powers.",
              "Mehr Leistung heraus als hinein: ein Wirkungsgrad über 100 % ist unmöglich. Eingetragenen Wirkungsgrad, Momente oder Leistungen prüfen."),
            "η ≤ 1; P_out ≤ P_in",
            c => c["eta"] is double eta && eta > 1 + Rel
                 || c["P2"] is double p2 && c["P1"] is double p1 && p2 > p1 * (1 + Rel)
                 || c["P"] is double p && c["Pel"] is double pel && (IsGenerator(c.Object) ? pel > p * (1 + Rel) : p > pel * (1 + Rel)))
        { Source = "S1" };

        /// <summary>mach.runup.torque: the mean motor torque during the run-up does not exceed the load torque [S15].</summary>
        private static ValueCheck RunUpTorque() => new(
            "mach.runup.torque", CheckSeverity.Warning,
            T("The mean motor torque during the run-up is not above the load torque: the drive cannot accelerate the load. Choose a motor with more starting torque or a larger gear ratio.",
              "Das mittlere Motormoment während des Hochlaufs liegt nicht über dem Lastmoment: Der Antrieb kann die Last nicht beschleunigen. Einen Motor mit mehr Anlaufmoment oder eine größere Übersetzung wählen."),
            "T_M > T_L",
            c => c["TM"] is double tm && c["T"] is double t && tm <= t * (1 + Rel))
        { Source = "S15" };

        /// <summary>
        /// mach.speed.max: the speed, or the synchronous speed the supply frequency gives (a frequency inverter's output), is above
        /// the machine's maximum permissible speed [S14].
        /// </summary>
        private static ValueCheck MaxSpeed() => new(
            "mach.speed.max", CheckSeverity.Warning,
            T("The speed is above the maximum permissible speed of the machine (or the supply frequency drives its synchronous speed above it). Lower the frequency or choose a machine for higher speeds.",
              "Die Drehzahl liegt über der höchstzulässigen Drehzahl der Maschine (oder die Speisefrequenz treibt ihre Synchrondrehzahl darüber). Frequenz senken oder eine Maschine für höhere Drehzahlen wählen."),
            "n ≤ n_max; n_s ≤ n_max",
            c => c["nmax"] is double m && (c["n"] is double n && n > m * (1 + Rel) || c["ns"] is double ns && ns > m * (1 + Rel)))
        { Source = "S14" };

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
        private static readonly Relation SameInertia = Relation.Equality("mach.link.inertia", T("Load inertia passed upstream", "Lastträgheitsmoment nach vorn weitergegeben"));

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
            // The efficiency rule of the operating mode (motor or generator) belongs to the element itself.
            if (MachValues.For(type.Id).Local is { } local)
                foreach (var r in local(o)) list.Add(BoundRelation.Local(r, o.Id));
            foreach (var (up, down) in ChainOf(doc))
            {
                if (up != o.Id && down != o.Id) continue;
                var (no, to) = SpecOf(doc, up)!.Output!.Value;
                var (ni, ti) = SpecOf(doc, down)!.Input!.Value;
                list.Add(Eq(SameSpeed, up, no, down, ni));
                list.Add(Eq(SameTorque, up, to, down, ti));
                // The inertia downstream of an output is what the next element presents at its input (J/i² through gearboxes).
                if (SpecOf(doc, up)!.InertiaOut is { } jo && SpecOf(doc, down)!.InertiaIn is { } ji) list.Add(Eq(SameInertia, up, jo, down, ji));
            }

            // Drivetrain parts on a mechanical shaft share the speed of its shaft line (Core ShaftLines; the torque along the
            // line comes from the shaft).
            if (registry is not null && (type.DrivesShaft || type.Id == "machines.drive.wheel"))
                list.AddRange(ShaftLines.Of(registry, doc).Links(o.Id, ShaftSpeed, null));

            // A machine wired to a frequency inverter takes its output frequency. The inverter's own port passes voltage and
            // power as well; this link lets the machine alone (its value label) find the inverter.
            if (MachValues.For(type.Id).Variables.Any(v => v.Key == "f") && DriveSupplyOf(doc, o) is { } vfd)
                list.Add(Eq(SupplyFrequency, o.Id, "f", vfd, "f"));
            return list;
        }

        private static readonly Relation SupplyFrequency = Relation.Equality("mach.link.drive.f", T("Supply frequency from the frequency inverter", "Speisefrequenz vom Frequenzumrichter"));

        /// <summary>Type id of the switchgear frequency inverter (modules do not reference each other; known by id).</summary>
        internal const string InverterTypeId = "sw.drive.vfd";

        private static readonly string[] PhaseTerminals = { "u", "v", "w" };
        private static readonly string[] InverterOutputs = { "U", "V", "W" };

        /// <summary>
        /// The one frequency inverter whose output pins (U, V, W) are wired to the machine's phase terminals u, v, w (lines of any
        /// module, followed end to end and through lines docked on them), or null.
        /// </summary>
        internal static string? DriveSupplyOf(SketchDocument doc, SketchObject machine)
        {
            var lines = doc.Objects.Values.Where(x => x.IsPointGeometry && x.Attach is { Count: > 0 }).OrderBy(x => x.Id, StringComparer.Ordinal).ToList();
            var stack = new Stack<SketchObject>();
            foreach (var l in lines)
            {
                var n = l.Attach!.Count;
                foreach (var a in new[] { l.Attach[0], l.Attach[n - 1] })
                    if (a is not null && a.ObjectId == machine.Id && PhaseTerminals.Contains(a.AnchorId)) { stack.Push(l); break; }
            }
            if (stack.Count == 0) return null;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var found = new HashSet<string>(StringComparer.Ordinal);
            while (stack.Count > 0)
            {
                var line = stack.Pop();
                if (!seen.Add(line.Id)) continue;
                foreach (var other in lines)
                    if (other.Attach!.Any(a => a is not null && a.ObjectId == line.Id)) stack.Push(other);
                var n = line.Attach!.Count;
                foreach (var a in new[] { line.Attach[0], line.Attach[n - 1] })
                {
                    if (a is null || a.ObjectId == machine.Id || doc.Get(a.ObjectId) is not { } x) continue;
                    if (x.IsPointGeometry) stack.Push(x);
                    else if (x.Type == InverterTypeId && InverterOutputs.Contains(a.AnchorId)) found.Add(x.Id);
                }
            }
            return found.Count == 1 ? found.First() : null;
        }
    }
}
