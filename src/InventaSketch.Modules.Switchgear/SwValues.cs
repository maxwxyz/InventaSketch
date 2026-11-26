using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using InventaSketch.Core;

namespace InventaSketch.Modules.Switchgear
{
    /// <summary>What kind of values a switchgear type carries.</summary>
    internal enum SwValueKind
    {
        None,
        /// <summary>Overcurrent protective device: circuit breaker, RCBO, motor protection, overload relay, fuse.</summary>
        Protection,
        /// <summary>Residual current device (no overload protection: no cable check).</summary>
        Rcd,
        /// <summary>Main switch / switch-disconnector.</summary>
        Switch,
        /// <summary>A load of its circuit: room devices, frequency inverter.</summary>
        Load,
        /// <summary>A DIN rail: the totals per phase of the loads fed from its devices.</summary>
        Board,
    }

    /// <summary>Variables, relations, value parameters and checks of one type.</summary>
    internal sealed class SwValueSpec
    {
        public static readonly SwValueSpec Empty = new(SwValueKind.None, Array.Empty<VarDef>(), Array.Empty<Relation>(), Array.Empty<ParamDef>(), Array.Empty<ValueCheck>());

        public SwValueSpec(SwValueKind kind, IReadOnlyList<VarDef> variables, IReadOnlyList<Relation> relations, IReadOnlyList<ParamDef> parameters, IReadOnlyList<ValueCheck> checks)
        {
            Kind = kind; Variables = variables; Relations = relations; Params = parameters; Checks = checks;
        }

        public SwValueKind Kind { get; }
        public IReadOnlyList<VarDef> Variables { get; }
        public IReadOnlyList<Relation> Relations { get; }
        /// <summary>Text parameters of the variables without a parameter of their own, and the values label switch.</summary>
        public IReadOnlyList<ParamDef> Params { get; }
        public IReadOnlyList<ValueCheck> Checks { get; }
        public bool Has(string key) => Variables.Any(v => v.Key == key);
    }

    /// <summary>
    /// Element values of the switchgear module (engineering calculations, section 1): protective devices (rated current
    /// I_n read from the rating choice, operating current I_B and power P of the circuit, current-carrying capacity I_z,
    /// instantaneous tripping current I_a from the characteristic), RCDs and main switches (I_n, I_B, P), loads (room
    /// devices and frequency inverters: P, I, U, cos φ, phase L1/L2/L3 or three-phase) and rails (current per phase, power
    /// and imbalance of the loads fed from the rail's devices). The circuit of a load is the feed tree of the reports
    /// (<see cref="Board.FeedTree"/>): its "fed from" designation, the wiring, or the rail order. Only loads with a power or
    /// current entered count in the totals. Every value is optional; derived values are never stored.
    /// </summary>
    internal static class SwValues
    {
        public const string PhaseKey = "phase";
        public const string ThreePhase = "L123";
        public const string FromCircuit = "circuit";
        public static readonly string[] Phases = { "L1", "L2", "L3" };

        private static I18nText T(string en, string de) => new(en, de);

        // ------------------------------------------------------------------ phase parameters

        /// <summary>Phase of a single-pole circuit (circuit breaker, RCBO 1+N): the loads of the circuit are on it.</summary>
        public static readonly ParamDef DevicePhase = Pm.Choice(PhaseKey, "Phase", "Phase", "L1", ("L1", "L1", "L1"), ("L2", "L2", "L2"), ("L3", "L3", "L3"))
            .WithOptionIcons(IconIds.ForPhase);

        /// <summary>Phase of a load: from its circuit (default), L1, L2, L3 or three-phase.</summary>
        public static readonly ParamDef LoadPhase = Pm.Choice(PhaseKey, "Phase", "Phase", FromCircuit,
            (FromCircuit, "From the circuit", "Vom Stromkreis"), ("L1", "L1", "L1"), ("L2", "L2", "L2"), ("L3", "L3", "L3"), (ThreePhase, "Three-phase", "Dreiphasig"))
            .WithOptionIcons(IconIds.ForPhase);

        private static bool IsSingle(string phase) => phase is "L1" or "L2" or "L3";

        /// <summary>True for a device that carries all three phases (3, 3+N or 4 poles; motor protection, overload relay, NH fuse switch; three-phase inverter).</summary>
        public static bool IsThreePhase(SketchObject o)
        {
            switch (o.Type)
            {
                case Industrial.Mpcb:
                case Industrial.Overload:
                case Industrial.NhFuse:
                    return true;
                case Industrial.Vfd:
                    return o.Str("supply", "3") == "3";
            }
            var def = SwitchgearModule.TypeOf(o.Type)?.Parameters.FirstOrDefault(p => p.Key == "poles");
            return def is not null && o.Str("poles", def.Default?.Text ?? "1") is "3" or "3N" or "4";
        }

        private static bool HasPhaseParam(SketchObject o) => SwitchgearModule.TypeOf(o.Type)?.Parameters.Any(p => p.Key == PhaseKey) == true;

        /// <summary>The phase a load asks for: its parameter (an inverter: three-phase or from the circuit).</summary>
        public static string OwnPhase(SketchObject load)
            => load.Type == Industrial.Vfd ? (IsThreePhase(load) ? ThreePhase : FromCircuit) : load.Str(PhaseKey, FromCircuit);

        /// <summary>
        /// The phase a load is on ("L1" … "L3" or "L123"): in a single-phase circuit the phase of its device (a single-pole
        /// device decides), in a three-phase circuit the load's own choice ("from the circuit" = three-phase); without a
        /// feeding device its own choice ("from the circuit" = L1).
        /// </summary>
        public static string EffectivePhase(SketchObject load, SketchObject? feeder)
        {
            var own = OwnPhase(load);
            if (feeder is not null && !IsThreePhase(feeder))
            {
                if (HasPhaseParam(feeder)) { var fp = feeder.Str(PhaseKey, "L1"); return IsSingle(fp) ? fp : "L1"; }
                return IsSingle(own) ? own : "L1";
            }
            // A symbol of another module computes its current single-phase unless it says three-phase itself.
            if (feeder is not null) return own == FromCircuit ? (Board.House(load) is { IsForeign: true } ? "L1" : ThreePhase) : own;
            return own == FromCircuit ? "L1" : own;
        }

        // ------------------------------------------------------------------ variables

        private static ParamDef Text(string key, I18nText label, string? unit) => new(key, label, ParamKind.Text) { Unit = unit, Advanced = true };

        private static VarDef V(string key, I18nText name, string symbol, QuantityKind kind, string? unit, string param, VarRelevance rel, double max = 1e6)
            => new(key, name, T(symbol, symbol), kind) { Unit = unit, Param = param, Relevance = rel, Min = 0, Max = max };

        private static VarDef RatedCurrent(string typeId) => typeId is Industrial.Mpcb or Industrial.Overload
            ? V("In", T("Set current", "Einstellstrom"), "I_r", Quantities.Current, "A", "rating", VarRelevance.Card)
            : V("In", T("Rated current", "Bemessungsstrom"), "I_n", Quantities.Current, "A", "rating", VarRelevance.Card);

        private static VarDef OperatingCurrent() => V("IB", T("Operating current of the circuit", "Betriebsstrom des Stromkreises"), "I_B", Quantities.Current, "A", "ib", VarRelevance.Card);
        private static VarDef CircuitPower() => V("P", T("Power of the circuit (sum of its loads)", "Leistung des Stromkreises (Summe der Verbraucher)"), "P", Quantities.Power, "W", "power", VarRelevance.Card, 1e10);
        private static VarDef CableCapacity() => V("Iz", T("Current-carrying capacity of the cable", "Strombelastbarkeit der Leitung"), "I_z", Quantities.Current, "A", "iz", VarRelevance.Panel);
        private static VarDef InstantCurrent() => V("Ia", T("Instantaneous tripping current (upper limit)", "Sofortauslösestrom (obere Grenze)"), "I_a", Quantities.Current, "A", "ia", VarRelevance.Panel);
        private static VarDef ResidualCurrent() => V("IDn", T("Rated residual current", "Bemessungsdifferenzstrom"), "I_Δn", Quantities.Current, "mA", "idn", VarRelevance.Panel) with { StoredUnit = "mA" };

        private static IReadOnlyList<VarDef> LoadVariables(bool drive) => new[]
        {
            drive
                ? V("P", T("Rated power", "Bemessungsleistung"), "P", Quantities.Power, "kW", "power", VarRelevance.Card, 1e10) with { StoredUnit = "kW" }
                : V("P", T("Power", "Leistung"), "P", Quantities.Power, "W", "power", VarRelevance.Card, 1e10),
            V("I", T("Current (per phase)", "Strom (je Außenleiter)"), "I", Quantities.Current, "A", "current", VarRelevance.Card),
            V("U", T("Voltage phase to neutral", "Spannung Außenleiter–Neutralleiter"), "U", Quantities.Voltage, "V", "voltage", VarRelevance.Panel, 1e5) with { Default = 230 },
            V("pf", T("Power factor", "Leistungsfaktor"), "cos φ", Quantities.Ratio, null, "powerFactor", VarRelevance.Panel, 1) with { Default = 1 },
        };

        private static IReadOnlyList<VarDef> BoardVariables() => new[]
        {
            V("IL1", T("Current in L1", "Strom in L1"), "I_L1", Quantities.Current, "A", "currentL1", VarRelevance.Card),
            V("IL2", T("Current in L2", "Strom in L2"), "I_L2", Quantities.Current, "A", "currentL2", VarRelevance.Card),
            V("IL3", T("Current in L3", "Strom in L3"), "I_L3", Quantities.Current, "A", "currentL3", VarRelevance.Card),
            V("P", T("Power of the board (sum of its loads)", "Leistung der Verteilung (Summe der Verbraucher)"), "P", Quantities.Power, "W", "power", VarRelevance.Card, 1e10),
            V("dI", T("Phase imbalance (largest minus smallest phase current)", "Schieflast (größter minus kleinster Außenleiterstrom)"), "ΔI", Quantities.Current, "A", "imbalance", VarRelevance.Panel),
            V("dImax", T("Permitted phase imbalance (default 20 A; your grid operator's limit)", "Zulässige Schieflast (Vorgabe 20 A; Grenze des Netzbetreibers)"), "ΔI_max", Quantities.Current, "A", "imbalanceMax", VarRelevance.Panel) with { Default = 20 },
        };

        // ------------------------------------------------------------------ relations

        private static Relation LoadCurrent(bool three) => three
            ? new Relation("sw.load.current3", T("Current per phase (three-phase)", "Strom je Außenleiter (dreiphasig)"), "{I} = {P}/(3·{U}·{pf})", "I", "P", "U", "pf")
            {
                Source = "S7", Validity = T("symmetrical three-phase load, U phase to neutral", "symmetrische Drehstromlast, U Außenleiter–Neutralleiter"),
            }
                .Solve("I", "{P}/(3·{U}·{pf})", v => v("P") / (3 * v("U") * v("pf")))
                .Solve("P", "3·{U}·{I}·{pf}", v => 3 * v("U") * v("I") * v("pf"))
                .Solve("U", "{P}/(3·{I}·{pf})", v => v("P") / (3 * v("I") * v("pf")))
                .Solve("pf", "{P}/(3·{U}·{I})", v => v("P") / (3 * v("U") * v("I")))
            : new Relation("sw.load.current1", T("Current (single-phase)", "Strom (einphasig)"), "{I} = {P}/({U}·{pf})", "I", "P", "U", "pf")
            {
                Source = "S7", Validity = T("U phase to neutral", "U Außenleiter–Neutralleiter"),
            }
                .Solve("I", "{P}/({U}·{pf})", v => v("P") / (v("U") * v("pf")))
                .Solve("P", "{U}·{I}·{pf}", v => v("U") * v("I") * v("pf"))
                .Solve("U", "{P}/({I}·{pf})", v => v("P") / (v("I") * v("pf")))
                .Solve("pf", "{P}/({U}·{I})", v => v("P") / (v("U") * v("I")));

        private static readonly Relation SingleCurrent = LoadCurrent(false);
        private static readonly Relation ThreeCurrent = LoadCurrent(true);

        /// <summary>Upper limit of the instantaneous tripping range: B 5·I_n, C 10·I_n, D 20·I_n [S11].</summary>
        public static double TripFactor(string characteristic) => characteristic switch { "C" => 10, "D" => 20, _ => 5 };

        private static readonly Dictionary<string, Relation> Instant = new(StringComparer.Ordinal);

        private static Relation InstantTrip(string characteristic)
        {
            var ch = characteristic is "C" or "D" ? characteristic : "B";
            lock (Instant)
            {
                if (Instant.TryGetValue(ch, out var r)) return r;
                var k = TripFactor(ch);
                var ks = k.ToString(CultureInfo.InvariantCulture);
                r = new Relation("sw.trip.instant", T("Instantaneous tripping current", "Sofortauslösestrom"), "{Ia} = " + ks + "·{In}", "Ia", "In")
                {
                    Source = "S11",
                    Validity = T("characteristic " + ch + ": upper limit of the instantaneous tripping range", "Charakteristik " + ch + ": obere Grenze des Sofortauslösebereichs"),
                }
                    .Solve("Ia", ks + "·{In}", v => k * v("In"))
                    .Solve("In", "{Ia}/" + ks, v => v("Ia") / k);
                Instant[ch] = r;
                return r;
            }
        }

        private static readonly Relation Imbalance =
            new Relation("sw.board.imbalance", T("Phase imbalance", "Schieflast"), "{dI} = max({IL1}, {IL2}, {IL3}) − min({IL1}, {IL2}, {IL3})", "dI", "IL1", "IL2", "IL3")
                .Solve("dI", "max({IL1}, {IL2}, {IL3}) − min({IL1}, {IL2}, {IL3})",
                    v => Math.Max(v("IL1"), Math.Max(v("IL2"), v("IL3"))) - Math.Min(v("IL1"), Math.Min(v("IL2"), v("IL3"))));

        /// <summary>
        /// s = the largest of the group sums (one group: the plain sum), a1 … an the parts; solved for s only (Kirchhoff's
        /// current law per phase, [S7]).
        /// </summary>
        private static Relation Total(string id, I18nText name, int n, IReadOnlyList<int[]> groups)
        {
            var keys = Enumerable.Range(1, n).Select(i => "a" + i.ToString(CultureInfo.InvariantCulture)).ToArray();
            string GroupText(int[] g) => string.Join(" + ", g.Select(i => "{" + keys[i] + "}"));
            var expr = groups.Count == 1 ? GroupText(groups[0]) : "max(" + string.Join(", ", groups.Select(GroupText)) + ")";
            var gs = groups.Select(g => g.Select(i => keys[i]).ToArray()).ToList();
            return new Relation(id, name, "{s} = " + expr, new[] { "s" }.Concat(keys).ToArray()) { Source = "S7" }
                .Solve("s", expr, v => gs.Max(g => g.Sum(k => v(k))));
        }

        /// <summary>A value without inputs (a phase without loads, the I_z entered on a cable): x = <paramref name="text"/>.</summary>
        private static Relation Constant(string id, I18nText name, double value, string text)
        {
            var t = text.Replace("{", "").Replace("}", "");
            return new Relation(id, name, "{x} = " + t, "x").Solve("x", t, _ => value);
        }

        private static BoundRelation Bind(Relation r, string objectId, string key, IReadOnlyList<CircuitMap.Load> parts, string partKey)
        {
            var map = new Dictionary<string, VarRef>(StringComparer.Ordinal) { ["s"] = new VarRef(objectId, key) };
            for (var i = 0; i < parts.Count; i++) map["a" + (i + 1).ToString(CultureInfo.InvariantCulture)] = new VarRef(parts[i].Id, partKey);
            return new BoundRelation(r, map);
        }

        private static BoundRelation BindOne(Relation r, string objectId, string key)
            => new(r, new Dictionary<string, VarRef>(StringComparer.Ordinal) { ["x"] = new VarRef(objectId, key) });

        // ------------------------------------------------------------------ checks

        private static bool Above(double? value, double? limit) => value is double v && limit is double l && v > l * (1 + 1e-9);

        /// <summary>sw.load.rating: the loads of the circuit draw more than the device's rated (or set) current [S9].</summary>
        public static readonly ValueCheck LoadAboveRating = new("sw.load.rating", CheckSeverity.Warning,
            T("The operating current I_B of the circuit (sum of its loads) is above the rated current I_n of the device. Move loads to another circuit or check the rating.",
              "Der Betriebsstrom I_B des Stromkreises (Summe der Verbraucher) liegt über dem Bemessungsstrom I_n des Geräts. Verbraucher auf einen anderen Stromkreis legen oder die Bemessung prüfen."),
            "I_B ≤ I_n", c => Above(c["IB"], c["In"])) { Source = "S9" };

        /// <summary>sw.rating.cable: the protective device is rated above the cable's current-carrying capacity I_z (as entered) [S9].</summary>
        public static readonly ValueCheck RatingAboveCable = new("sw.rating.cable", CheckSeverity.Warning,
            T("The rated current I_n of the protective device is above the current-carrying capacity I_z of the cable (as entered). Choose a lower rating or a larger cable.",
              "Der Bemessungsstrom I_n des Schutzorgans liegt über der Strombelastbarkeit I_z der Leitung (wie eingegeben). Eine kleinere Bemessung oder eine stärkere Leitung wählen."),
            "I_n ≤ I_z", c => Above(c["In"], c["Iz"])) { Source = "S9" };

        /// <summary>sw.phase.imbalance: the phases of a board are loaded unevenly (difference above ΔI_max, default 20 A).</summary>
        public static readonly ValueCheck PhaseImbalance = new("sw.phase.imbalance", CheckSeverity.Warning,
            T("The phases are loaded unevenly: the most and the least loaded phase differ by more than ΔI_max (default 20 A; enter your grid operator's limit). Move circuits to another phase.",
              "Die Außenleiter sind ungleich belastet: der am stärksten und der am schwächsten belastete Außenleiter unterscheiden sich um mehr als ΔI_max (Vorgabe 20 A; Grenze des Netzbetreibers eintragen). Stromkreise auf einen anderen Außenleiter legen."),
            "I_max − I_min ≤ ΔI_max", c => Above(c["dI"], c["dImax"]));

        // ------------------------------------------------------------------ specs per type

        private static readonly Dictionary<string, SwValueSpec> Specs = new(StringComparer.Ordinal);

        public static SwValueSpec For(ObjectType type)
        {
            lock (Specs)
            {
                if (!Specs.TryGetValue(type.Id, out var s)) Specs[type.Id] = s = Build(type);
                return s;
            }
        }

        /// <summary>The value spec of a module type by id (empty for unknown ids and types without values).</summary>
        public static SwValueSpec For(string typeId) => SwitchgearModule.TypeOf(typeId) is { } t ? For(t) : SwValueSpec.Empty;

        private static SwValueSpec Build(ObjectType type)
        {
            switch (type)
            {
                case DeviceType d:
                    var spec = d.Spec;
                    return spec.Role switch
                    {
                        DeviceRole.Protection or DeviceRole.Rcbo => Protection(spec),
                        DeviceRole.Rcd => Make(SwValueKind.Rcd, spec.Params, new[] { RatedCurrent(spec.Id), OperatingCurrent(), CircuitPower(), ResidualCurrent() }, Array.Empty<Relation>(), new[] { LoadAboveRating }),
                        DeviceRole.MainSwitch => Make(SwValueKind.Switch, spec.Params, new[] { RatedCurrent(spec.Id), OperatingCurrent(), CircuitPower() }, Array.Empty<Relation>(), new[] { LoadAboveRating }),
                        DeviceRole.Drive => Make(SwValueKind.Load, spec.Params, LoadVariables(true), Array.Empty<Relation>(), Array.Empty<ValueCheck>()),
                        _ => SwValueSpec.Empty,
                    };
                case HouseType:
                    return Make(SwValueKind.Load, Array.Empty<ParamDef>(), LoadVariables(false), Array.Empty<Relation>(), Array.Empty<ValueCheck>());
                case RailType:
                    return Make(SwValueKind.Board, Array.Empty<ParamDef>(), BoardVariables(), new[] { Imbalance }, new[] { PhaseImbalance });
                default:
                    return SwValueSpec.Empty;
            }
        }

        private static SwValueSpec Protection(DeviceSpec spec)
        {
            var vars = new List<VarDef> { RatedCurrent(spec.Id), OperatingCurrent(), CircuitPower(), CableCapacity() };
            if (spec.Params.Any(p => p.Key == "char")) vars.Add(InstantCurrent());
            if (spec.Params.Any(p => p.Key == "idn")) vars.Add(ResidualCurrent());
            return Make(SwValueKind.Protection, spec.Params, vars, Array.Empty<Relation>(), new[] { LoadAboveRating, RatingAboveCable });
        }

        private static SwValueSpec Make(SwValueKind kind, IReadOnlyList<ParamDef> existing, IReadOnlyList<VarDef> vars, IReadOnlyList<Relation> relations, IReadOnlyList<ValueCheck> checks)
        {
            var ps = vars.Where(v => existing.All(p => p.Key != v.ParamKey)).Select(v => Text(v.ParamKey, v.Name, v.Unit)).Append(ValueLabels.ShowValues).ToList();
            return new SwValueSpec(kind, vars, relations, ps, checks);
        }

        // ------------------------------------------------------------------ links

        /// <summary>Power and current of a load as entered: the same parameters on the room devices and on the installation symbols of other modules.</summary>
        private static readonly IReadOnlyList<VarDef> EnteredLoad = LoadVariables(false).Where(v => v.Key is "P" or "I").ToList();

        /// <summary>
        /// True if a load has a power or a current entered (only those count in the totals). Installation symbols of other
        /// modules that take power (<see cref="HouseDevice.IsLoad"/>) are read through the same parameters; their own module
        /// declares the variables P and I the circuit totals refer to.
        /// </summary>
        public static bool HasLoadValue(SketchObject load)
        {
            if (Board.House(load) is { IsForeign: true } foreign)
                return foreign.IsLoad && EnteredLoad.Any(v => ValueModel.ReadEntered(load, v).Value is not null);
            var spec = For(load.Type);
            if (spec.Kind != SwValueKind.Load) return false;
            return spec.Variables.Where(v => v.Key is "P" or "I").Any(v => ValueModel.ReadEntered(load, v).Value is not null);
        }

        /// <summary>Default voltage phase to neutral of the loads (the room devices' U default; symbols of other modules in a circuit).</summary>
        private const double NominalVoltage = 230;

        private static readonly I18nText NominalVoltageName = T("Nominal voltage phase to neutral of the circuit (no voltage entered on the load)",
            "Nennspannung Außenleiter–Neutralleiter des Stromkreises (am Verbraucher keine Spannung eingetragen)");

        private static readonly VarDef EnteredVoltage = LoadVariables(false).Single(v => v.Key == "U");

        /// <summary>
        /// True for an installation symbol of another module in a circuit whose voltage is open: none entered, and not both power
        /// and current (which would give the voltage themselves).
        /// </summary>
        private static bool NeedsNominalVoltage(SketchObject load)
            => Board.House(load) is { IsForeign: true }
               && ValueModel.ReadEntered(load, EnteredVoltage).Value is null
               && !EnteredLoad.All(v => ValueModel.ReadEntered(load, v).Value is not null);

        /// <summary>The current-carrying capacity typed on a cable ("20 A", "20"), or null.</summary>
        public static double? EnteredCurrent(SketchObject o, string key)
        {
            var text = o.Str(key, "");
            return text.Trim().Length > 0 && ValueParser.TryParse(text, Quantities.Current, out var pv, out _, Lang.De, "A") && pv!.Value > 0 ? pv.Value : null;
        }

        private static readonly I18nText CurrentSingleName = T("Operating current of the circuit (sum of the load currents)", "Betriebsstrom des Stromkreises (Summe der Verbraucherströme)");
        private static readonly I18nText CurrentThreeName = T("Operating current of the circuit (most loaded phase)", "Betriebsstrom des Stromkreises (am stärksten belasteter Außenleiter)");
        private static readonly I18nText PowerName = T("Power of the circuit (sum of the loads)", "Leistung des Stromkreises (Summe der Verbraucher)");
        private static readonly I18nText PhaseName = T("Current in the phase (sum of the loads on it)", "Strom im Außenleiter (Summe der Verbraucher darauf)");
        private static readonly I18nText EmptyPhaseName = T("No load with a value on this phase", "Kein Verbraucher mit Wert auf diesem Außenleiter");
        private static readonly I18nText BoardPowerName = T("Power of the board (sum of the loads)", "Leistung der Verteilung (Summe der Verbraucher)");
        private static readonly I18nText CableName = T("Current-carrying capacity entered on the circuit's cable", "Am Kabel des Stromkreises eingetragene Strombelastbarkeit");

        /// <summary>Indices of the parts on <paramref name="phase"/> (three-phase loads are on every phase).</summary>
        private static int[] OnPhase(IReadOnlyList<CircuitMap.Load> loads, string phase)
            => Enumerable.Range(0, loads.Count).Where(i => loads[i].Phase == phase || loads[i].Phase == ThreePhase).ToArray();

        public static IEnumerable<BoundRelation> Links(SketchObject o, SwValueSpec spec, RenderContext ctx)
        {
            switch (spec.Kind)
            {
                case SwValueKind.None:
                    yield break;
                case SwValueKind.Load:
                    {
                        var phase = CircuitMap.Of(ctx.Doc).PhaseOf(o.Id) ?? EffectivePhase(o, null);
                        yield return BoundRelation.Local(phase == ThreePhase ? ThreeCurrent : SingleCurrent, o.Id);
                        yield break;
                    }
                case SwValueKind.Board:
                    {
                        var loads = CircuitMap.Of(ctx.Doc).RailLoads(o.Id);
                        if (loads.Count == 0) yield break;
                        for (var k = 0; k < Phases.Length; k++)
                        {
                            var key = "I" + Phases[k];
                            var on = OnPhase(loads, Phases[k]);
                            if (on.Length == 0) { yield return BindOne(Constant("sw.board.phase.empty", EmptyPhaseName, 0, "0 A"), o.Id, key); continue; }
                            var parts = on.Select(i => loads[i]).ToList();
                            yield return Bind(Total("sw.board.phase", PhaseName, parts.Count, new[] { Enumerable.Range(0, parts.Count).ToArray() }), o.Id, key, parts, "I");
                        }
                        yield return Bind(Total("sw.board.power", BoardPowerName, loads.Count, new[] { Enumerable.Range(0, loads.Count).ToArray() }), o.Id, "P", loads, "P");
                        yield break;
                    }
                default:
                    {
                        if (spec.Has("Ia")) yield return BoundRelation.Local(InstantTrip(o.Str("char", "B")), o.Id);
                        var map = CircuitMap.Of(ctx.Doc);
                        var loads = map.LoadsOf(o.Id);
                        if (loads.Count > 0)
                        {
                            var all = Enumerable.Range(0, loads.Count).ToArray();
                            var three = IsThreePhase(o);
                            var groups = three ? Phases.Select(ph => OnPhase(loads, ph)).Where(g => g.Length > 0).ToList() : new List<int[]> { all };
                            yield return Bind(Total("sw.circuit.current", three ? CurrentThreeName : CurrentSingleName, loads.Count, groups), o.Id, "IB", loads, "I");
                            yield return Bind(Total("sw.circuit.power", PowerName, loads.Count, new[] { all }), o.Id, "P", loads, "P");
                            // Installation symbols of other modules have no default voltage of their own: in a circuit they are
                            // on 230 V phase to neutral unless a voltage is entered on them (as the room devices here default to).
                            foreach (var l in loads)
                                if (ctx.Doc.Get(l.Id) is { } lo && NeedsNominalVoltage(lo))
                                    yield return BindOne(Constant("sw.load.voltage", NominalVoltageName, NominalVoltage, "230 V"), l.Id, "U");
                        }
                        if (spec.Has("Iz") && map.CableCapacity(o.Id) is { } iz)
                            yield return BindOne(Constant("sw.cable.iz", CableName, iz.Value, iz.Text), o.Id, "Iz");
                        yield break;
                    }
            }
        }
    }

    /// <summary>
    /// The circuits of a document for the values (built once per document, documents are immutable): per protective
    /// device the loads with a value in its part of the feed tree (with the phase each one is on), per rail the loads fed
    /// from its devices (each load once, through its nearest device on a rail), the effective phase of every load in the
    /// tree, and the smallest current-carrying capacity entered on a cable of a circuit (docked at a bottom pin of the
    /// device or at one of its loads).
    /// </summary>
    internal sealed class CircuitMap
    {
        internal readonly record struct Load(string Id, string Phase);

        private static readonly ConditionalWeakTable<SketchDocument, CircuitMap> Cache = new();

        public static CircuitMap Of(SketchDocument doc) => Cache.GetValue(doc, d => new CircuitMap(d));

        private readonly Dictionary<string, List<Load>> _device = new(StringComparer.Ordinal);
        private readonly Dictionary<string, List<Load>> _rail = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _phase = new(StringComparer.Ordinal);
        private readonly Dictionary<string, (double Value, string Text)> _iz = new(StringComparer.Ordinal);

        public IReadOnlyList<Load> LoadsOf(string deviceId) => _device.TryGetValue(deviceId, out var l) ? l : (IReadOnlyList<Load>)Array.Empty<Load>();
        public IReadOnlyList<Load> RailLoads(string railId) => _rail.TryGetValue(railId, out var l) ? l : (IReadOnlyList<Load>)Array.Empty<Load>();
        public string? PhaseOf(string loadId) => _phase.TryGetValue(loadId, out var p) ? p : null;
        public (double Value, string Text)? CableCapacity(string deviceId) => _iz.TryGetValue(deviceId, out var v) ? v : null;

        private CircuitMap(SketchDocument doc)
        {
            var all = doc.Pages.SelectMany(doc.ObjectsOn).ToList();
            var board = new Board(doc, all, Lang.En);
            var parent = new Dictionary<Board.Node, Board.Node?>();
            var nodes = new List<Board.Node>();
            void Walk(Board.Node n, Board.Node? up)
            {
                if (parent.ContainsKey(n)) return;
                parent[n] = up;
                nodes.Add(n);
                foreach (var c in n.Children) Walk(c, n);
            }
            foreach (var r in board.FeedTree()) Walk(r, null);

            // Loads with their phase at the device feeding them.
            var direct = new Dictionary<Board.Node, List<Load>>();
            foreach (var n in nodes)
            {
                var feeder = n.Objects.FirstOrDefault();
                var list = new List<Load>();
                foreach (var l in n.Loads)
                {
                    var phase = SwValues.EffectivePhase(l, feeder);
                    _phase[l.Id] = phase;
                    if (SwValues.HasLoadValue(l)) list.Add(new Load(l.Id, phase));
                }
                direct[n] = list;
            }

            // Every device: the loads of its part of the tree.
            var below = new Dictionary<Board.Node, List<Load>>();
            List<Load> Below(Board.Node n)
            {
                if (below.TryGetValue(n, out var l)) return l;
                below[n] = l = new List<Load>(direct[n]);
                foreach (var c in n.Children) l.AddRange(Below(c));
                return l;
            }
            foreach (var n in nodes)
            {
                var loads = Below(n);
                var iz = Capacity(board, n);
                foreach (var o in n.Objects)
                {
                    _device[o.Id] = loads;
                    if (iz is { } v) _iz[o.Id] = v;
                }
            }

            // Rails: the devices on each rail (per page, in page coordinates), then each load through its nearest device on a rail.
            var railOf = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var page in doc.Pages)
                foreach (var row in new Board(doc, doc.ObjectsOn(page).ToList(), Lang.En).Rows())
                    if (row.Rail is { } rail)
                        foreach (var p in row.Devices) railOf[p.Obj.Id] = rail.Id;
            foreach (var n in nodes)
            {
                if (direct[n].Count == 0) continue;
                for (Board.Node? cur = n; cur is not null; cur = parent[cur])
                {
                    var rail = cur.Objects.Select(o => railOf.TryGetValue(o.Id, out var r) ? r : null).FirstOrDefault(r => r is not null);
                    if (rail is null) continue;
                    if (!_rail.TryGetValue(rail, out var l)) _rail[rail] = l = new List<Load>();
                    l.AddRange(direct[n]);
                    break;
                }
            }
        }

        /// <summary>The smallest I_z entered on a line docked at a bottom pin of the node's devices or at one of its loads.</summary>
        private static (double Value, string Text)? Capacity(Board board, Board.Node n)
        {
            (double Value, string Text)? best = null;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            void Look(SketchObject o, IEnumerable<string> pins)
            {
                foreach (var pin in pins)
                    foreach (var (line, _) in board.LinesAt(o, pin))
                    {
                        if (!seen.Add(line.Id) || SwValues.EnteredCurrent(line, "iz") is not double iz) continue;
                        if (best is { } b && b.Value <= iz) continue;
                        var rf = line.Str("ref", "").Trim();
                        best = (iz, line.Str("iz", "").Trim() + (rf.Length > 0 ? " (" + rf + ")" : ""));
                    }
            }
            foreach (var o in n.Objects)
                if (Board.Device(o) is { } t) Look(o, t.Pins(o).Where(p => p.V > 0).Select(p => p.Id));
            foreach (var l in n.Loads)
            {
                if (Board.House(l) is { } h) Look(l, h.Pins);
                else if (Board.Device(l) is { } t) Look(l, t.Pins(l).Where(p => p.V < 0).Select(p => p.Id));
            }
            return best;
        }
    }
}
