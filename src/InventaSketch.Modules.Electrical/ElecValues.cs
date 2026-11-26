using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using InventaSketch.Core;
using static InventaSketch.Modules.Electrical.Common;

namespace InventaSketch.Modules.Electrical
{
    /// <summary>A value read from a component's value text: variable key, SI value and the part of the text it came from.</summary>
    internal readonly record struct MarkingValue(string Key, double Value, string Text);

    /// <summary>
    /// Element values of electrical parts (engineering-calculations.md sections 2.1–2.3, 3.1 and 10.10). Values live in
    /// optional text parameters that keep what was typed ("230 V"); the passive parts read their value text as R, C or L
    /// ("4k7", "100n"); sources, lamps, LEDs and protective devices read their value text as a marking ("5 V", "12 V 5 W",
    /// "2.1 V 20 mA", "B16", "40 A 30 mA"). Derived values are never stored. Ratings used only by checks (P_rated, I_F,max,
    /// I_z) are plain optional parameters, so they never make an element look under-determined.
    /// </summary>
    internal static class ElecValues
    {
        // ------------------------------------------------------------------ parameters

        private static ParamDef Txt(string key, string en, string de, string unit) => new(key, T(en, de), ParamKind.Text) { Unit = unit };

        private static readonly ParamDef Voltage = Txt("voltage", "Voltage", "Spannung", "V");
        private static readonly ParamDef Current = Txt("current", "Current", "Strom", "A");
        private static readonly ParamDef Power = Txt("power", "Power", "Leistung", "W");

        /// <summary>Optional value parameters of the resistor (text with a unit hint, entered via the Values UI or the inspector).</summary>
        public static readonly IReadOnlyList<ParamDef> ResistorParams = new[]
        {
            Voltage, Current, Power,
            Txt("prated", "Rated power P_rated", "Nennleistung P_rated", "W"),
            // Calc 2: the values label below the symbol ("U = 230 V, I = 2 A, P = 460 W"), drawn by ValueLabels.
            ValueLabels.ShowValues,
        };

        public static readonly IReadOnlyList<ParamDef> LampParams = new[] { Voltage, Current, Power, Txt("resistance", "Resistance", "Widerstand", "Ω"), ValueLabels.ShowValues };

        // Time constant, cut-off frequency and reactance (RC/RL elements, section 2.1): the resistance in series is taken from
        // the resistors wired in series with the capacitor or inductor, or entered.
        private static readonly ParamDef RSeries = Txt("rseries", "Resistance in series R (time constant)", "Widerstand in Reihe R (Zeitkonstante)", "Ω");
        private static readonly ParamDef Tau = Txt("tau", "Time constant τ", "Zeitkonstante τ", "s");
        private static readonly ParamDef Cutoff = Txt("cutoff", "Cut-off frequency f_c", "Grenzfrequenz f_g", "Hz");
        private static readonly ParamDef Frequency = Txt("frequency", "Frequency f", "Frequenz f", "Hz");
        private static readonly ParamDef Reactance = Txt("reactance", "Reactance X at f", "Blindwiderstand X bei f", "Ω");

        public static readonly IReadOnlyList<ParamDef> CapacitorParams = new[]
        {
            Voltage, Txt("charge", "Charge", "Ladung", "C"), Txt("energy", "Energy", "Energie", "J"), RSeries, Tau, Cutoff, Frequency, Reactance, ValueLabels.ShowValues,
        };

        public static readonly IReadOnlyList<ParamDef> InductorParams = new[]
        {
            Current, Txt("energy", "Energy", "Energie", "J"), RSeries, Tau, Cutoff, Frequency, Reactance, ValueLabels.ShowValues,
        };

        /// <summary>Potentiometer: resistance (value text), voltage across the track, wiper position and the divider values.</summary>
        public static readonly IReadOnlyList<ParamDef> PotentiometerParams = new[]
        {
            Voltage, Current, Power,
            new ParamDef("wiper", T("Wiper position k (0 at pin 1, 100 % at pin 2)", "Schleiferstellung k (0 an Pin 1, 100 % an Pin 2)"), ParamKind.Text) { Unit = "%" },
            Txt("r1", "Resistance pin 1 – wiper R₁", "Widerstand Pin 1 – Schleifer R₁", "Ω"),
            Txt("r2", "Resistance wiper – pin 2 R₂", "Widerstand Schleifer – Pin 2 R₂", "Ω"),
            Txt("uwiper", "Wiper voltage U₂ (to pin 2, unloaded)", "Schleiferspannung U₂ (gegen Pin 2, unbelastet)", "V"),
            ValueLabels.ShowValues,
        };

        /// <summary>Bipolar transistor as a switch: base resistor from the input voltage, collector current and current gain.</summary>
        public static readonly IReadOnlyList<ParamDef> BjtParams = new[]
        {
            Txt("ic", "Collector current I_C", "Kollektorstrom I_C", "A"),
            new ParamDef("beta", T("Current gain B (h_FE, datasheet)", "Stromverstärkung B (h_FE, Datenblatt)"), ParamKind.Text),
            Txt("uin", "Input voltage U_in (before the base resistor)", "Eingangsspannung U_in (vor dem Basiswiderstand)", "V"),
            Txt("rb", "Base resistor R_B", "Basiswiderstand R_B", "Ω"),
            Txt("ib", "Base current I_B", "Basisstrom I_B", "A"),
            Txt("ube", "Base-emitter voltage U_BE", "Basis-Emitter-Spannung U_BE", "V"),
            new ParamDef("overdrive", T("Overdrive factor k", "Übersteuerungsfaktor k"), ParamKind.Text),
            ValueLabels.ShowValues,
        };

        public static readonly ParamDef OpAmpCircuit = Choice("circuit", T("Circuit (for the gain)", "Schaltung (für die Verstärkung)"), "auto",
            ("auto", "From the wiring", "Aus der Verdrahtung"), ("inverting", "Inverting amplifier", "Invertierender Verstärker"),
            ("noninverting", "Non-inverting amplifier", "Nichtinvertierender Verstärker"), ("follower", "Voltage follower", "Spannungsfolger (Impedanzwandler)"),
            ("open", "None (comparator, open loop)", "Keine (Komparator, offen)"));

        /// <summary>Op-amp basic circuits (ideal op-amp): gain from the feedback and input resistors, output from input voltage.</summary>
        public static readonly IReadOnlyList<ParamDef> OpAmpParams = new[]
        {
            OpAmpCircuit,
            new ParamDef("gain", T("Voltage gain A", "Spannungsverstärkung V"), ParamKind.Text),
            Txt("uin", "Input voltage U_in", "Eingangsspannung U_e", "V"),
            Txt("uout", "Output voltage U_out", "Ausgangsspannung U_a", "V"),
            Txt("rf", "Feedback resistor R_f", "Rückkopplungswiderstand R_f", "Ω"),
            Txt("rin", "Input resistor R_in (inverting) / R_g (non-inverting)", "Eingangswiderstand R_e (invertierend) / R_g (nichtinvertierend)", "Ω"),
            Txt("usupply", "Supply voltage ±U_S", "Versorgungsspannung ±U_B", "V"),
            ValueLabels.ShowValues,
        };

        public static readonly IReadOnlyList<ParamDef> DiodeParams = new[]
        {
            Txt("uf", "Forward voltage U_F", "Durchlassspannung U_F", "V"), Txt("if", "Forward current I_F", "Durchlassstrom I_F", "A"), Power, ValueLabels.ShowValues,
        };

        public static readonly IReadOnlyList<ParamDef> LedParams = DiodeParams.Take(3)
            .Append(Txt("ifmax", "Maximum forward current I_F,max (datasheet)", "Maximaler Durchlassstrom I_F,max (Datenblatt)", "A"))
            .Append(ValueLabels.ShowValues).ToList();

        public static readonly IReadOnlyList<ParamDef> ZenerParams = new[] { Txt("uz", "Zener voltage U_Z", "Z-Spannung U_Z", "V"), Current, Power, ValueLabels.ShowValues };

        public static readonly IReadOnlyList<ParamDef> SourceParams = new[] { Voltage, Current, Power, ValueLabels.ShowValues };

        public static readonly IReadOnlyList<ParamDef> RegulatorParams = new[]
        {
            Txt("uout", "Output voltage U_out", "Ausgangsspannung U_out", "V"), Txt("uin", "Input voltage U_in", "Eingangsspannung U_in", "V"),
            Txt("current", "Output current I_out", "Ausgangsstrom I_out", "A"), Txt("power", "Power loss P", "Verlustleistung P", "W"), ValueLabels.ShowValues,
        };

        public static readonly IReadOnlyList<ParamDef> DeviceParams = new[]
        {
            Txt("current", "Operating current I_B", "Betriebsstrom I_B", "A"),
            Txt("rated", "Rated current I_n", "Bemessungsstrom I_n", "A"),
            Txt("iz", "Current-carrying capacity of the cable I_z (user)", "Strombelastbarkeit der Leitung I_z (Benutzer)", "A"),
            ValueLabels.ShowValues,
        };

        public static readonly IReadOnlyList<ParamDef> RcdParams = new[]
        {
            Txt("rated", "Rated current I_n", "Bemessungsstrom I_n", "A"),
            Txt("idn", "Rated residual current I_Δn", "Bemessungsdifferenzstrom I_Δn", "A"),
            ValueLabels.ShowValues,
        };

        public static readonly IReadOnlyList<ParamDef> LoadParams = new[] { Voltage, Current, Power, ValueLabels.ShowValues };

        public static readonly IReadOnlyList<ParamDef> TransformerParams = new[]
        {
            Txt("u1", "Primary voltage U_1", "Primärspannung U_1", "V"), Txt("u2", "Secondary voltage U_2", "Sekundärspannung U_2", "V"),
            Txt("i1", "Primary current I_1", "Primärstrom I_1", "A"), Txt("i2", "Secondary current I_2", "Sekundärstrom I_2", "A"),
            new ParamDef("ratio", T("Voltage ratio n", "Übersetzungsverhältnis ü"), ParamKind.Text),
            ValueLabels.ShowValues,
        };

        // ------------------------------------------------------------------ variables

        private static VarDef V(string key, string en, string de, string symbol, QuantityKind kind, string param, string unit)
            => new(key, T(en, de), T(symbol, symbol), kind) { Param = param, Unit = unit };

        private static VarDef U(string param = "voltage") => V("U", "Voltage", "Spannung", "U", Quantities.Voltage, param, "V");
        private static VarDef I(string param = "current") => V("I", "Current", "Strom", "I", Quantities.Current, param, "A");
        private static VarDef P(string param = "power") => V("P", "Power", "Leistung", "P", Quantities.Power, param, "W");

        public static readonly IReadOnlyList<VarDef> ResistorVariables = new[]
        {
            new VarDef("R", T("Resistance", "Widerstand"), T("R", "R"), Quantities.Resistance) { Param = "value", OnTypeLabel = true, Unit = "Ω", Min = 1e-6, Max = 1e12 },
            U(), I(), P(),
        };

        public static readonly IReadOnlyList<VarDef> LampVariables = new[]
        {
            U(), I(), P(),
            V("R", "Resistance (hot)", "Widerstand (warm)", "R", Quantities.Resistance, "resistance", "Ω") with { Min = 1e-6, Max = 1e12 },
        };

        public static readonly IReadOnlyList<VarDef> CapacitorVariables = new[]
        {
            new VarDef("C", T("Capacitance", "Kapazität"), T("C", "C"), Quantities.Capacitance) { Param = "value", OnTypeLabel = true, Unit = "F", Min = 1e-15, Max = 1e4 },
            U(),
            V("Q", "Charge", "Ladung", "Q", Quantities.Charge, "charge", "C"),
            V("E", "Stored energy", "Gespeicherte Energie", "E", Quantities.Energy, "energy", "J"),
        }.Concat(Dynamics("X_C")).ToList();

        public static readonly IReadOnlyList<VarDef> InductorVariables = new[]
        {
            new VarDef("L", T("Inductance", "Induktivität"), T("L", "L"), Quantities.Inductance) { Param = "value", OnTypeLabel = true, Unit = "H", Min = 1e-12, Max = 1e4 },
            I(),
            V("E", "Stored energy", "Gespeicherte Energie", "E", Quantities.Energy, "energy", "J"),
        }.Concat(Dynamics("X_L")).ToList();

        /// <summary>Resistance in series, time constant, cut-off frequency, frequency and reactance (panel values).</summary>
        private static IEnumerable<VarDef> Dynamics(string reactance) => new[]
        {
            V("Rt", "Resistance in series (time constant)", "Widerstand in Reihe (Zeitkonstante)", "R", Quantities.Resistance, "rseries", "Ω") with { Relevance = VarRelevance.Panel, Min = 1e-6, Max = 1e12 },
            V("tau", "Time constant", "Zeitkonstante", "τ", Quantities.Time_, "tau", "s") with { Relevance = VarRelevance.Panel, Min = 1e-15, Max = 1e9 },
            new VarDef("fc", T("Cut-off frequency (−3 dB)", "Grenzfrequenz (−3 dB)"), T("f_c", "f_g"), Quantities.Frequency) { Param = "cutoff", Unit = "Hz", Relevance = VarRelevance.Panel, Min = 1e-9, Max = 1e15 },
            V("f", "Frequency", "Frequenz", "f", Quantities.Frequency, "frequency", "Hz") with { Relevance = VarRelevance.Panel, Min = 1e-9, Max = 1e15 },
            V("X", "Reactance at f", "Blindwiderstand bei f", reactance, Quantities.Resistance, "reactance", "Ω") with { Relevance = VarRelevance.Panel, Min = 1e-12, Max = 1e15 },
        };

        public static readonly IReadOnlyList<VarDef> PotentiometerVariables = new[]
        {
            new VarDef("R", T("Resistance (track)", "Widerstand (Bahn)"), T("R", "R"), Quantities.Resistance) { Param = "value", OnTypeLabel = true, Unit = "Ω", Min = 1e-6, Max = 1e12 },
            U(),
            new VarDef("k", T("Wiper position", "Schleiferstellung"), T("k", "k"), Quantities.Ratio) { Param = "wiper", Unit = "%", Min = 0, Max = 1 },
            V("Uw", "Wiper voltage (unloaded)", "Schleiferspannung (unbelastet)", "U₂", Quantities.Voltage, "uwiper", "V"),
            I() with { Relevance = VarRelevance.Panel }, P() with { Relevance = VarRelevance.Panel },
            V("R1", "Resistance pin 1 – wiper", "Widerstand Pin 1 – Schleifer", "R₁", Quantities.Resistance, "r1", "Ω") with { Relevance = VarRelevance.Panel, Min = 0, Max = 1e12 },
            V("R2", "Resistance wiper – pin 2", "Widerstand Schleifer – Pin 2", "R₂", Quantities.Resistance, "r2", "Ω") with { Relevance = VarRelevance.Panel, Min = 0, Max = 1e12 },
        };

        public static readonly IReadOnlyList<VarDef> BjtVariables = new[]
        {
            V("Rb", "Base resistor", "Basiswiderstand", "R_B", Quantities.Resistance, "rb", "Ω") with { Min = 1e-3, Max = 1e12 },
            V("Ic", "Collector current", "Kollektorstrom", "I_C", Quantities.Current, "ic", "A") with { Min = 0, Max = 1e6 },
            new VarDef("B", T("Current gain (h_FE)", "Stromverstärkung (h_FE)"), T("B", "B"), Quantities.Ratio) { Param = "beta", Min = 1, Max = 1e5 },
            V("Uin", "Input voltage", "Eingangsspannung", "U_in", Quantities.Voltage, "uin", "V"),
            V("Ib", "Base current", "Basisstrom", "I_B", Quantities.Current, "ib", "A") with { Relevance = VarRelevance.Panel, Min = 0, Max = 1e6 },
            V("Ube", "Base-emitter voltage", "Basis-Emitter-Spannung", "U_BE", Quantities.Voltage, "ube", "V") with { Relevance = VarRelevance.Panel, Default = 0.7, Min = 0, Max = 10 },
            new VarDef("k", T("Overdrive factor", "Übersteuerungsfaktor"), T("k", "k"), Quantities.Ratio) { Param = "overdrive", Relevance = VarRelevance.Panel, Default = 3, Min = 0.01, Max = 100 },
        };

        public static readonly IReadOnlyList<VarDef> OpAmpVariables = new[]
        {
            new VarDef("A", T("Voltage gain", "Spannungsverstärkung"), T("A", "V_u"), Quantities.Ratio) { Param = "gain", Min = -1e7, Max = 1e7 },
            V("Uin", "Input voltage", "Eingangsspannung", "U_in", Quantities.Voltage, "uin", "V") with { Symbol = T("U_in", "U_e"), Min = -1e6, Max = 1e6 },
            V("Uout", "Output voltage", "Ausgangsspannung", "U_out", Quantities.Voltage, "uout", "V") with { Symbol = T("U_out", "U_a"), Min = -1e6, Max = 1e6 },
            V("Rf", "Feedback resistor", "Rückkopplungswiderstand", "R_f", Quantities.Resistance, "rf", "Ω") with { Min = 1e-6, Max = 1e12 },
            V("Rin", "Input resistor (inverting) / R_g (non-inverting)", "Eingangswiderstand (invertierend) / R_g (nichtinvertierend)", "R_in", Quantities.Resistance, "rin", "Ω")
                with { Relevance = VarRelevance.Panel, Min = 1e-6, Max = 1e12 },
            V("Us", "Supply voltage (±)", "Versorgungsspannung (±)", "U_S", Quantities.Voltage, "usupply", "V") with { Symbol = T("U_S", "U_B"), Relevance = VarRelevance.Panel, Min = 0, Max = 1e6 },
        };

        public static readonly IReadOnlyList<VarDef> DiodeVariables = new[]
        {
            V("U", "Forward voltage", "Durchlassspannung", "U_F", Quantities.Voltage, "uf", "V"),
            V("I", "Forward current", "Durchlassstrom", "I_F", Quantities.Current, "if", "A"),
            V("P", "Power loss", "Verlustleistung", "P", Quantities.Power, "power", "W"),
        };

        public static readonly IReadOnlyList<VarDef> ZenerVariables = new[]
        {
            V("U", "Zener voltage", "Z-Spannung", "U_Z", Quantities.Voltage, "uz", "V"), I(), V("P", "Power loss", "Verlustleistung", "P", Quantities.Power, "power", "W"),
        };

        public static readonly IReadOnlyList<VarDef> SourceVariables = new[] { U(), I(), P() };

        public static readonly IReadOnlyList<VarDef> RegulatorVariables = new[]
        {
            V("Uout", "Output voltage", "Ausgangsspannung", "U_out", Quantities.Voltage, "uout", "V"),
            V("Uin", "Input voltage", "Eingangsspannung", "U_in", Quantities.Voltage, "uin", "V"),
            V("I", "Output current", "Ausgangsstrom", "I_out", Quantities.Current, "current", "A"),
            V("P", "Power loss", "Verlustleistung", "P", Quantities.Power, "power", "W"),
        };

        public static readonly IReadOnlyList<VarDef> LoadVariables = new[] { U(), I(), P() };

        public static readonly IReadOnlyList<VarDef> DeviceVariables = new[]
        {
            V("I", "Operating current", "Betriebsstrom", "I_B", Quantities.Current, "current", "A"),
            V("In", "Rated current", "Bemessungsstrom", "I_n", Quantities.Current, "rated", "A"),
        };

        public static readonly IReadOnlyList<VarDef> RcdVariables = new[]
        {
            V("In", "Rated current", "Bemessungsstrom", "I_n", Quantities.Current, "rated", "A"),
            V("IDn", "Rated residual current", "Bemessungsdifferenzstrom", "I_Δn", Quantities.Current, "idn", "A"),
        };

        public static readonly IReadOnlyList<VarDef> TransformerVariables = new[]
        {
            V("U1", "Primary voltage", "Primärspannung", "U_1", Quantities.Voltage, "u1", "V"),
            V("U2", "Secondary voltage", "Sekundärspannung", "U_2", Quantities.Voltage, "u2", "V"),
            V("I1", "Primary current", "Primärstrom", "I_1", Quantities.Current, "i1", "A"),
            V("I2", "Secondary current", "Sekundärstrom", "I_2", Quantities.Current, "i2", "A"),
            // The voltage ratio links the two sides (usually derived).
            new VarDef("n", T("Voltage ratio", "Übersetzungsverhältnis"), T("n", "ü"), Quantities.Ratio) { Param = "ratio", Relevance = VarRelevance.Panel, Min = 1e-6, Max = 1e6 },
        };

        // ------------------------------------------------------------------ relations

        /// <summary>U = R·I and P = U·I = I²·R = U²/R [S7]; the square-root inverses take the positive branch.</summary>
        public static readonly IReadOnlyList<Relation> Ohm = new[]
        {
            new Relation("elec.ohm", T("Ohm's law", "Ohmsches Gesetz"), "{U} = {R}·{I}", "U", "R", "I") { Source = "S7" }
                .Solve("U", "{R}·{I}", v => v("R") * v("I"))
                .Solve("R", "{U}/{I}", v => v("U") / v("I"))
                .Solve("I", "{U}/{R}", v => v("U") / v("R")),
            PowerUi(),
            new Relation("elec.power.ir", T("Electric power", "Elektrische Leistung"), "{P} = {I}²·{R}", "P", "I", "R") { Source = "S7" }
                .Solve("P", "{I}²·{R}", v => v("I") * v("I") * v("R"))
                .Solve("I", "√({P}/{R})", v => Math.Sqrt(v("P") / v("R")))
                .Solve("R", "{P}/{I}²", v => v("P") / (v("I") * v("I"))),
            new Relation("elec.power.ur", T("Electric power", "Elektrische Leistung"), "{P} = {U}²/{R}", "P", "U", "R") { Source = "S7" }
                .Solve("P", "{U}²/{R}", v => v("U") * v("U") / v("R"))
                .Solve("U", "√({P}·{R})", v => Math.Sqrt(v("P") * v("R")))
                .Solve("R", "{U}²/{P}", v => v("U") * v("U") / v("P")),
        };

        private static Relation PowerUi() =>
            new Relation("elec.power.ui", T("Electric power", "Elektrische Leistung"), "{P} = {U}·{I}", "P", "U", "I") { Source = "S7" }
                .Solve("P", "{U}·{I}", v => v("U") * v("I"))
                .Solve("U", "{P}/{I}", v => v("P") / v("I"))
                .Solve("I", "{P}/{U}", v => v("P") / v("U"));

        /// <summary>P = U·I (DC power of a source, a diode or a load).</summary>
        public static readonly IReadOnlyList<Relation> PowerRelations = new[] { PowerUi() };

        /// <summary>
        /// Linear voltage regulator: the voltage difference across it times the output current is its power loss,
        /// P = (U_in − U_out)·I_out (P = U·I applied to the series element; quiescent current neglected) [S7].
        /// </summary>
        public static readonly IReadOnlyList<Relation> Regulator = new[]
        {
            new Relation("elec.regulator.loss", T("Power loss of a linear regulator", "Verlustleistung eines Linearreglers"), "{P} = ({Uin} − {Uout})·{I}", "P", "Uin", "Uout", "I")
            { Source = "S7", Validity = T("linear regulator, quiescent current neglected", "Linearregler, Eigenstromverbrauch vernachlässigt") }
                .Solve("P", "({Uin} − {Uout})·{I}", v => (v("Uin") - v("Uout")) * v("I"))
                .Solve("I", "{P}/({Uin} − {Uout})", v => v("P") / (v("Uin") - v("Uout")))
                .Solve("Uin", "{Uout} + {P}/{I}", v => v("Uout") + v("P") / v("I"))
                .Solve("Uout", "{Uin} − {P}/{I}", v => v("Uin") - v("P") / v("I")),
        };

        /// <summary>Q = C·U; E = ½·C·U² = ½·Q·U = Q²/(2C) [S7].</summary>
        public static readonly IReadOnlyList<Relation> Capacitor = new[]
        {
            new Relation("elec.cap.charge", T("Capacitor charge", "Ladung des Kondensators"), "{Q} = {C}·{U}", "Q", "C", "U") { Source = "S7" }
                .Solve("Q", "{C}·{U}", v => v("C") * v("U"))
                .Solve("C", "{Q}/{U}", v => v("Q") / v("U"))
                .Solve("U", "{Q}/{C}", v => v("Q") / v("C")),
            new Relation("elec.cap.energy", T("Energy in a capacitor", "Energie im Kondensator"), "{E} = ½·{C}·{U}²", "E", "C", "U") { Source = "S7" }
                .Solve("E", "½·{C}·{U}²", v => 0.5 * v("C") * v("U") * v("U"))
                .Solve("C", "2·{E}/{U}²", v => 2 * v("E") / (v("U") * v("U")))
                .Solve("U", "√(2·{E}/{C})", v => Math.Sqrt(2 * v("E") / v("C"))),
            new Relation("elec.cap.energy.qu", T("Energy in a capacitor", "Energie im Kondensator"), "{E} = ½·{Q}·{U}", "E", "Q", "U") { Source = "S7" }
                .Solve("E", "½·{Q}·{U}", v => 0.5 * v("Q") * v("U"))
                .Solve("Q", "2·{E}/{U}", v => 2 * v("E") / v("U"))
                .Solve("U", "2·{E}/{Q}", v => 2 * v("E") / v("Q")),
            new Relation("elec.cap.energy.qc", T("Energy in a capacitor", "Energie im Kondensator"), "{E} = {Q}²/(2·{C})", "E", "Q", "C") { Source = "S7" }
                .Solve("E", "{Q}²/(2·{C})", v => v("Q") * v("Q") / (2 * v("C")))
                .Solve("Q", "√(2·{E}·{C})", v => Math.Sqrt(2 * v("E") * v("C")))
                .Solve("C", "{Q}²/(2·{E})", v => v("Q") * v("Q") / (2 * v("E"))),
        };

        /// <summary>E = ½·L·I² [S7].</summary>
        public static readonly IReadOnlyList<Relation> Inductor = new[]
        {
            new Relation("elec.ind.energy", T("Energy in an inductor", "Energie in der Spule"), "{E} = ½·{L}·{I}²", "E", "L", "I") { Source = "S7" }
                .Solve("E", "½·{L}·{I}²", v => 0.5 * v("L") * v("I") * v("I"))
                .Solve("L", "2·{E}/{I}²", v => 2 * v("E") / (v("I") * v("I")))
                .Solve("I", "√(2·{E}/{L})", v => Math.Sqrt(2 * v("E") / v("L"))),
            new Relation("elec.rl.tau", T("Time constant of an RL element", "Zeitkonstante eines RL-Glieds"), "{tau} = {L}/{Rt}", "tau", "L", "Rt") { Source = "S7" }
                .Solve("tau", "{L}/{Rt}", v => v("L") / v("Rt"))
                .Solve("L", "{tau}·{Rt}", v => v("tau") * v("Rt"))
                .Solve("Rt", "{L}/{tau}", v => v("L") / v("tau")),
            CutOff(),
            new Relation("elec.reactance.l", T("Inductive reactance", "Induktiver Blindwiderstand"), "{X} = 2π·{f}·{L}", "X", "f", "L") { Source = "S7" }
                .Solve("X", "2π·{f}·{L}", v => TwoPi * v("f") * v("L"))
                .Solve("f", "{X}/(2π·{L})", v => v("X") / (TwoPi * v("L")))
                .Solve("L", "{X}/(2π·{f})", v => v("X") / (TwoPi * v("f"))),
        };

        private const double TwoPi = 2 * Math.PI;

        /// <summary>Cut-off (−3 dB) frequency of a first-order RC or RL element f_c = 1/(2π·τ) [S7].</summary>
        private static Relation CutOff() =>
            new Relation("elec.cutoff", T("Cut-off frequency (first order)", "Grenzfrequenz (1. Ordnung)"), "{fc} = 1/(2π·{tau})", "fc", "tau") { Source = "S7" }
                .Solve("fc", "1/(2π·{tau})", v => 1 / (TwoPi * v("tau")))
                .Solve("tau", "1/(2π·{fc})", v => 1 / (TwoPi * v("fc")));

        /// <summary>RC element: τ = R·C, f_c = 1/(2π·τ); capacitive reactance X_C = 1/(2π·f·C) [S7].</summary>
        public static readonly IReadOnlyList<Relation> CapacitorDynamics = new[]
        {
            new Relation("elec.rc.tau", T("Time constant of an RC element", "Zeitkonstante eines RC-Glieds"), "{tau} = {Rt}·{C}", "tau", "Rt", "C") { Source = "S7" }
                .Solve("tau", "{Rt}·{C}", v => v("Rt") * v("C"))
                .Solve("Rt", "{tau}/{C}", v => v("tau") / v("C"))
                .Solve("C", "{tau}/{Rt}", v => v("tau") / v("Rt")),
            CutOff(),
            new Relation("elec.reactance.c", T("Capacitive reactance", "Kapazitiver Blindwiderstand"), "{X} = 1/(2π·{f}·{C})", "X", "f", "C") { Source = "S7" }
                .Solve("X", "1/(2π·{f}·{C})", v => 1 / (TwoPi * v("f") * v("C")))
                .Solve("f", "1/(2π·{X}·{C})", v => 1 / (TwoPi * v("X") * v("C")))
                .Solve("C", "1/(2π·{f}·{X})", v => 1 / (TwoPi * v("f") * v("X"))),
        };

        /// <summary>
        /// Potentiometer: Ohm's law over the whole track, the wiper position splits it (R₁ = k·R, R₂ = R − R₁) and the
        /// unloaded divider gives the wiper voltage U₂ = U·R₂/R [S7].
        /// </summary>
        public static readonly IReadOnlyList<Relation> Potentiometer = Ohm.Concat(new[]
        {
            new Relation("elec.pot.r1", T("Wiper position", "Schleiferstellung"), "{R1} = {k}·{R}", "R1", "k", "R") { Source = "S7" }
                .Solve("R1", "{k}·{R}", v => v("k") * v("R"))
                .Solve("k", "{R1}/{R}", v => v("R1") / v("R"))
                .Solve("R", "{R1}/{k}", v => v("R1") / v("k")),
            new Relation("elec.pot.r2", T("Track split by the wiper", "Durch den Schleifer geteilte Bahn"), "{R2} = {R} − {R1}", "R2", "R", "R1") { Source = "S7" }
                .Solve("R2", "{R} − {R1}", v => v("R") - v("R1"))
                .Solve("R", "{R1} + {R2}", v => v("R1") + v("R2"))
                .Solve("R1", "{R} − {R2}", v => v("R") - v("R2")),
            new Relation("elec.divider", T("Voltage divider (unloaded)", "Spannungsteiler (unbelastet)"), "{Uw} = {U}·{R2}/{R}", "Uw", "U", "R2", "R")
            { Source = "S7", Validity = T("unloaded: no current drawn from the wiper", "unbelastet: dem Schleifer wird kein Strom entnommen") }
                .Solve("Uw", "{U}·{R2}/{R}", v => v("U") * v("R2") / v("R"))
                .Solve("U", "{Uw}·{R}/{R2}", v => v("Uw") * v("R") / v("R2"))
                .Solve("R2", "{Uw}·{R}/{U}", v => v("Uw") * v("R") / v("U")),
        }).ToList();

        /// <summary>
        /// Bipolar transistor as a switch: I_B = k·I_C/B with an overdrive factor k (≈ 2 … 5), R_B = (U_in − U_BE)/I_B [S7].
        /// </summary>
        public static readonly IReadOnlyList<Relation> Bjt = new[]
        {
            new Relation("elec.bjt.ib", T("Base current for switching", "Basisstrom zum Schalten"), "{Ib} = {k}·{Ic}/{B}", "Ib", "k", "Ic", "B")
            { Source = "S7", Validity = T("transistor as a saturated switch", "Transistor als übersteuerter Schalter") }
                .Solve("Ib", "{k}·{Ic}/{B}", v => v("k") * v("Ic") / v("B"))
                .Solve("Ic", "{Ib}·{B}/{k}", v => v("Ib") * v("B") / v("k"))
                .Solve("k", "{Ib}·{B}/{Ic}", v => v("Ib") * v("B") / v("Ic"))
                .Solve("B", "{k}·{Ic}/{Ib}", v => v("k") * v("Ic") / v("Ib")),
            new Relation("elec.bjt.rb", T("Base resistor", "Basiswiderstand"), "{Rb} = ({Uin} − {Ube})/{Ib}", "Rb", "Uin", "Ube", "Ib") { Source = "S7" }
                .Solve("Rb", "({Uin} − {Ube})/{Ib}", v => (v("Uin") - v("Ube")) / v("Ib"))
                .Solve("Ib", "({Uin} − {Ube})/{Rb}", v => (v("Uin") - v("Ube")) / v("Rb"))
                .Solve("Uin", "{Ube} + {Rb}·{Ib}", v => v("Ube") + v("Rb") * v("Ib")),
        };

        /// <summary>Ideal op-amp: U_out = A·U_in [S7].</summary>
        public static readonly IReadOnlyList<Relation> OpAmp = new[]
        {
            new Relation("elec.opamp.out", T("Amplifier output", "Verstärkerausgang"), "{Uout} = {A}·{Uin}", "Uout", "A", "Uin") { Source = "S7", Validity = T("ideal op-amp, output within the supply", "idealer Operationsverstärker, Ausgang innerhalb der Versorgung") }
                .Solve("Uout", "{A}·{Uin}", v => v("A") * v("Uin"))
                .Solve("A", "{Uout}/{Uin}", v => v("Uout") / v("Uin"))
                .Solve("Uin", "{Uout}/{A}", v => v("Uout") / v("A")),
        };

        /// <summary>Gain of the inverting amplifier A = −R_f/R_in [S7].</summary>
        public static readonly Relation InvertingGain =
            new Relation("elec.opamp.inverting", T("Inverting amplifier", "Invertierender Verstärker"), "{A} = −{Rf}/{Rin}", "A", "Rf", "Rin") { Source = "S7", Validity = T("ideal op-amp", "idealer Operationsverstärker") }
                .Solve("A", "−{Rf}/{Rin}", v => -v("Rf") / v("Rin"))
                .Solve("Rf", "−{A}·{Rin}", v => -v("A") * v("Rin"))
                .Solve("Rin", "−{Rf}/{A}", v => -v("Rf") / v("A"));

        /// <summary>Gain of the non-inverting amplifier A = 1 + R_f/R_g [S7].</summary>
        public static readonly Relation NonInvertingGain =
            new Relation("elec.opamp.noninverting", T("Non-inverting amplifier", "Nichtinvertierender Verstärker"), "{A} = 1 + {Rf}/{Rin}", "A", "Rf", "Rin") { Source = "S7", Validity = T("ideal op-amp", "idealer Operationsverstärker") }
                .Solve("A", "1 + {Rf}/{Rin}", v => 1 + v("Rf") / v("Rin"))
                .Solve("Rf", "({A} − 1)·{Rin}", v => (v("A") - 1) * v("Rin"))
                .Solve("Rin", "{Rf}/({A} − 1)", v => v("Rf") / (v("A") - 1));

        /// <summary>Voltage follower A = 1 [S7].</summary>
        public static readonly Relation FollowerGain =
            new Relation("elec.opamp.follower", T("Voltage follower", "Spannungsfolger"), "{A} = 1", "A") { Source = "S7" }
                .Solve("A", "1", _ => 1);

        /// <summary>Ideal transformer: n = U₁/U₂ = I₂/I₁ [S7, S15].</summary>
        public static readonly IReadOnlyList<Relation> Transformer = new[]
        {
            new Relation("elec.trafo.u", T("Ideal transformer: voltages", "Idealer Transformator: Spannungen"), "{n} = {U1}/{U2}", "n", "U1", "U2")
            { Source = "S7", Validity = T("ideal transformer", "idealer Transformator") }
                .Solve("n", "{U1}/{U2}", v => v("U1") / v("U2"))
                .Solve("U1", "{n}·{U2}", v => v("n") * v("U2"))
                .Solve("U2", "{U1}/{n}", v => v("U1") / v("n")),
            new Relation("elec.trafo.i", T("Ideal transformer: currents", "Idealer Transformator: Ströme"), "{n} = {I2}/{I1}", "n", "I2", "I1")
            { Source = "S7", Validity = T("ideal transformer", "idealer Transformator") }
                .Solve("n", "{I2}/{I1}", v => v("I2") / v("I1"))
                .Solve("I2", "{n}·{I1}", v => v("n") * v("I1"))
                .Solve("I1", "{I2}/{n}", v => v("I2") / v("n")),
        };

        // ------------------------------------------------------------------ markings (value text)

        private static readonly Regex Token = new(@"(?<![\p{L}\d.,])(?<n>\d+(?:[.,]\d+)?)\s*(?<u>[\p{L}]+)(?![\p{L}\d])", RegexOptions.CultureInvariant | RegexOptions.Compiled);
        private static readonly Regex DeviceCode = new(@"^\s*(?:[BCDKZbcdkz]|[gGaA][GLNMRglnmr])?\s*(?<n>\d+(?:[.,]\d+)?)\s*A?\s*$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

        /// <summary>Quantities with a unit in a value text ("12 V 5 W", "2.1 V 20 mA"): each kind once, the first occurrence wins.</summary>
        public static IEnumerable<(QuantityKind Kind, double Value, string Text)> Tokens(string? text, params QuantityKind[] kinds)
        {
            if (string.IsNullOrWhiteSpace(text)) yield break;
            var seen = new HashSet<QuantityKind>();
            foreach (Match m in Token.Matches(text))
            {
                var unit = m.Groups["u"].Value;
                foreach (var kind in kinds)
                {
                    if (seen.Contains(kind) || UnitCatalog.Find(unit, kind) is null) continue;
                    if (!ValueParser.TryParse(m.Groups["n"].Value + " " + unit, kind, out var pv, out _, Lang.De)) continue;
                    seen.Add(kind);
                    yield return (kind, pv!.Value, m.Value.Trim());
                    break;
                }
            }
        }

        /// <summary>Marking by quantity kind: "12 V 5 W" on a lamp gives U and P.</summary>
        public static Func<SketchObject, IEnumerable<MarkingValue>> ByKind(params (QuantityKind Kind, string Key)[] map)
            => o => Tokens(o.Str("value", ""), map.Select(m => m.Kind).ToArray())
                .Select(t => new MarkingValue(map.First(m => m.Kind == t.Kind).Key, t.Value, t.Text));

        /// <summary>Rated current of a fuse or circuit breaker from its marking: "B16", "C 10 A", "gG 35 A", "16 A", "16".</summary>
        public static IEnumerable<MarkingValue> DeviceMarking(SketchObject o)
        {
            var text = o.Str("value", "");
            if (DeviceRating(text) is { } r) yield return new MarkingValue("In", r.Value, r.Text);
        }

        public static (double Value, string Text)? DeviceRating(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            // The whole marking is shown as the source of the value ("I_n = C 10 A = 10 A").
            foreach (var t in Tokens(text, Quantities.Current)) return (t.Value, text!.Trim());
            var m = DeviceCode.Match(text);
            return m.Success && ValueParser.TryNumber(m.Groups["n"].Value, out var n) && n > 0 ? (n, text!.Trim()) : null;
        }

        /// <summary>RCD marking: "40 A 30 mA", "40/0,03 A", "30 mA" (one current up to 1 A is the residual current).</summary>
        public static IEnumerable<MarkingValue> RcdMarking(SketchObject o)
        {
            var text = o.Str("value", "");
            if (string.IsNullOrWhiteSpace(text)) yield break;
            var found = new List<(double Value, string Text)>();
            if (ValueParser.TryParse(text, Quantities.Current, out var pv, out _, Lang.De, "A") && pv!.Parts.Count == 2)
                found.AddRange(pv.Parts.Select(x => (x, text.Trim())));
            else
                foreach (Match m in Token.Matches(text))
                    if (UnitCatalog.Find(m.Groups["u"].Value, Quantities.Current) is not null
                        && ValueParser.TryParse(m.Groups["n"].Value + " " + m.Groups["u"].Value, Quantities.Current, out var t, out _, Lang.De))
                        found.Add((t!.Value, m.Value.Trim()));
            if (found.Count >= 2)
            {
                var sorted = found.Take(2).OrderByDescending(f => f.Value).ToList();
                yield return new MarkingValue("In", sorted[0].Value, sorted[0].Text);
                yield return new MarkingValue("IDn", sorted[1].Value, sorted[1].Text);
            }
            else if (found.Count == 1)
                yield return new MarkingValue(found[0].Value <= 1 ? "IDn" : "In", found[0].Value, found[0].Text);
        }

        /// <summary>Transformer marking "230/12 V": primary and secondary voltage.</summary>
        public static IEnumerable<MarkingValue> TransformerMarking(SketchObject o)
        {
            var text = o.Str("value", "");
            if (string.IsNullOrWhiteSpace(text)) yield break;
            if (ValueParser.TryParse(text, Quantities.Voltage, out var pv, out _, Lang.De, "V") && pv!.Parts.Count == 2)
            {
                yield return new MarkingValue("U1", pv.Parts[0], text.Trim());
                yield return new MarkingValue("U2", pv.Parts[1], text.Trim());
            }
        }

        private static readonly I18nText MarkingName = new("Marking (value text)", "Kennzeichnung (Werttext)");

        /// <summary>One relation per marking value: the variable equals the value read from the text ("I_n = B16 = 16 A").</summary>
        public static IEnumerable<BoundRelation> MarkingRelations(SketchObject o, IEnumerable<MarkingValue> values)
        {
            foreach (var m in values)
            {
                if (double.IsNaN(m.Value) || double.IsInfinity(m.Value)) continue;
                var text = m.Text.Replace("{", "").Replace("}", "");
                var value = m.Value;
                var rel = new Relation("elec.marking", MarkingName, "{x} = " + text, "x").Solve("x", text, _ => value);
                yield return new BoundRelation(rel, new Dictionary<string, VarRef> { ["x"] = new VarRef(o.Id, m.Key) });
            }
        }

        // ------------------------------------------------------------------ checks (section 4)

        /// <summary>A rating typed into an optional text parameter (SI), or null.</summary>
        public static double? Rating(SketchObject o, string key, QuantityKind kind, string unit)
        {
            var text = o.Str(key, "");
            return text.Trim().Length > 0 && ValueParser.TryParse(text, kind, out var pv, out _, Lang.De, unit) ? pv!.Value : null;
        }

        private static bool Above(double? value, double? limit) => value is double v && limit is double l && v > l * (1 + 1e-9);

        public static readonly ValueCheck ResistorPower = new("elec.r.power", CheckSeverity.Warning,
            T("The power in the resistor is above its rated power P_rated. Choose a resistor with a higher rating or reduce the current.",
              "Die Leistung im Widerstand liegt über seiner Nennleistung P_rated. Einen Widerstand mit höherer Belastbarkeit wählen oder den Strom verringern."),
            "P ≤ P_rated", c => Above(c["P"], Rating(c.Object, "prated", Quantities.Power, "W"))) { Source = "S7" };

        public static readonly ValueCheck LedCurrent = new("elec.led.current", CheckSeverity.Warning,
            T("The LED current is above the maximum forward current I_F,max you entered (datasheet). Increase the series resistor.",
              "Der LED-Strom liegt über dem eingegebenen maximalen Durchlassstrom I_F,max (Datenblatt). Den Vorwiderstand vergrößern."),
            "I_F ≤ I_F,max", c => Above(c["I"], Rating(c.Object, "ifmax", Quantities.Current, "A"))) { Source = "S7" };

        /// <summary>
        /// Overload coordination I_B ≤ I_n ≤ I_z (IEC 60364-4-43, [S9]): every known neighbouring pair is checked; I_z is the
        /// user's value (the ampacity tables are not copied).
        /// </summary>
        public static readonly ValueCheck Coordination = new("elec.coord.overload", CheckSeverity.Warning,
            T("Overload coordination I_B ≤ I_n ≤ I_z is not met (I_z as entered). Check the rated current of the protective device or the cable.",
              "Die Zuordnung zum Überlastschutz I_B ≤ I_n ≤ I_z ist nicht erfüllt (I_z wie eingegeben). Bemessungsstrom des Schutzorgans oder die Leitung prüfen."),
            "I_B ≤ I_n ≤ I_z", c =>
            {
                var ib = c["I"];
                var In = c["In"];
                var iz = Rating(c.Object, "iz", Quantities.Current, "A");
                return Above(ib, In) || Above(In, iz) || In is null && Above(ib, iz);
            }) { Source = "S9" };

        /// <summary>
        /// Additional protection of socket outlets by an RCD with I_Δn ≤ 30 mA (IEC 60364-4-41, [S10]; national rules differ):
        /// a hint on a socket outlet whose circuit has an overcurrent protective device drawn but no such RCD. An RCD without a
        /// rating counts as present (nothing is known against it).
        /// </summary>
        public static readonly ValueCheck RcdAdditional = new("elec.rcd.additional", CheckSeverity.Hint,
            T("No RCD with I_Δn ≤ 30 mA found in the circuit of this socket outlet (additional protection; national rules differ).",
              "Im Stromkreis dieser Steckdose wurde kein RCD mit I_Δn ≤ 30 mA gefunden (Zusatzschutz; nationale Regeln weichen ab)."),
            "RCD with I_Δn ≤ 30 mA in the socket circuit", c => MissingRcd(c.Context.Doc, c.Object.Id)) { Source = "S10" };

        /// <summary>Not reducible by series/parallel steps (section 1.6): a bridge or several sources in different places.</summary>
        public static readonly ValueCheck NotReducible = new("elec.net.reducible", CheckSeverity.Info,
            T("Not reducible here – this circuit is not a series/parallel network (e.g. a bridge). Use a circuit simulator for its currents.",
              "Hier nicht reduzierbar – diese Schaltung ist kein Reihen-/Parallelnetz (z. B. eine Brücke). Für ihre Ströme einen Schaltungssimulator verwenden."),
            "series/parallel reduction only", c => ElecNetwork.Of(c.Context.Doc).Irreducible.Contains(c.Object.Id));

        private static bool MissingRcd(SketchDocument doc, string socketId)
        {
            var protection = false;
            foreach (var id in ElecNetwork.Of(doc).CircuitOf(socketId))
            {
                if (doc.Get(id) is not { } o) continue;
                switch (o.Type)
                {
                    case "elec.fuse":
                    case "elec.breaker":
                        protection = true;
                        break;
                    case "elec.sld.feeder":
                        protection |= o.Str("device", "breaker") != "none";
                        break;
                    case "elec.rcd":
                        var idn = Rating(o, "idn", Quantities.Current, "A") ?? RcdMarking(o).Where(m => m.Key == "IDn").Select(m => (double?)m.Value).FirstOrDefault();
                        if (idn is null || idn <= 0.030 * (1 + 1e-9)) return false;
                        break;
                }
            }
            return protection;
        }

        public static readonly IReadOnlyList<ValueCheck> ReducibleOnly = new[] { NotReducible };

        // ------------------------------------------------------------------ circuit roles

        public static Func<SketchObject, NetSpec> Two(string a, string b, BranchDef def) => _ => NetSpec.Branch(def with { PinA = a, PinB = b });

        public static BranchDef Resistive => new("1", "2") { U = "U", I = "I", R = "R" };
        public static BranchDef Capacitive => new("1", "2") { U = "U", C = "C", Q = "Q", Rt = "Rt" };
        public static BranchDef Inductive => new("1", "2") { I = "I", L = "L", Rt = "Rt" };
        public static BranchDef Drop => new("1", "2") { U = "U", I = "I" };
        public static BranchDef SourceBranch => new("+", "-") { U = "U", I = "I", Source = true };
        /// <summary>A two-terminal element without values: it keeps the circuit reducible, but no value passes through it.</summary>
        public static BranchDef Opaque => new("1", "2");

        /// <summary>Fuse or breaker: an ideal conductor carrying the circuit current; open only when switched off explicitly.</summary>
        public static Func<SketchObject, NetSpec> Device(string a, string b)
            => o => ExplicitlyOpen(o) ? NetSpec.Open(a, b) : NetSpec.Branch(new BranchDef(a, b) { ZeroU = true, I = "I" });

        /// <summary>A switch conducts when closed (explicitly or as a normally closed contact); open contacts carry no current.</summary>
        public static NetSpec Contact(SketchObject o, string a, string b) => IsClosed(o) ? NetSpec.Short(a, b) : NetSpec.Open(a, b);

        /// <summary>
        /// Protective devices are drawn in their rest position by convention; they count as closed unless the user switched
        /// them off explicitly (parameter <c>closed</c> = false).
        /// </summary>
        public static bool ExplicitlyOpen(SketchObject o) => o.Params.TryGetValue("closed", out var v) && v.Bool == false;

    }
}
