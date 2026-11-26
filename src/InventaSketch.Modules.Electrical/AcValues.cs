using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using static InventaSketch.Modules.Electrical.Common;

namespace InventaSketch.Modules.Electrical
{
    /// <summary>
    /// Alternating current values (round 6, engineering-calculations.md sections 2.1 and 2.2): AC power of a source and of a
    /// motor or generator (S = U·I, P = S·cos φ, Q = S·sin φ; three-phase S = √3·U·I), star-delta starting, the reactive
    /// parts' Ohm's law at a frequency, resonance bandwidth, and cable values (conductor resistance, voltage drop with cos φ,
    /// losses). The network relations (impedance, phase, per-element currents, resonance) are in <see cref="ElecNetwork"/>.
    /// All values are optional; nothing is needed to insert an element. Values are RMS.
    /// </summary>
    internal static class AcValues
    {
        private const double TwoPi = 2 * Math.PI;
        private static readonly double Sqrt3 = Math.Sqrt(3);

        private static ParamDef Txt(string key, string en, string de, string unit) => new(key, T(en, de), ParamKind.Text) { Unit = unit };
        private static ParamDef Num(string key, string en, string de) => new(key, T(en, de), ParamKind.Text);

        private static VarDef V(string key, string en, string de, string symbol, QuantityKind kind, string param, string unit, VarRelevance rel = VarRelevance.Panel)
            => new(key, T(en, de), T(symbol, symbol), kind) { Param = param, Unit = unit, Relevance = rel };

        private static readonly I18nText AcValidity = T("sinusoidal AC, RMS values", "sinusförmiger Wechselstrom, Effektivwerte");

        // ------------------------------------------------------------------ parameters

        /// <summary>
        /// The frequency of an AC source: a plain parameter (like a rating), read by the circuit analysis together with a
        /// frequency in the value text ("230 V 50 Hz"); the reactive parts of the circuit take it over as their f.
        /// </summary>
        public static readonly ParamDef Frequency = Txt("frequency", "Frequency f (AC)", "Frequenz f (Wechselstrom)", "Hz");
        public static readonly ParamDef Impedance = Txt("impedance", "Impedance of the load |Z|", "Impedanz der Last |Z|", "Ω");
        public static readonly ParamDef Phase = Txt("phase", "Phase angle φ (load)", "Phasenwinkel φ (Last)", "°");
        public static readonly ParamDef PowerFactor = Num("pf", "Power factor cos φ", "Leistungsfaktor cos φ");
        public static readonly ParamDef Apparent = Txt("apparent", "Apparent power S", "Scheinleistung S", "VA");
        public static readonly ParamDef Reactive = Txt("reactive", "Reactive power Q", "Blindleistung Q", "var");

        /// <summary>Phases of an element's voltage and current against the source voltage (results of the circuit).</summary>
        public static readonly ParamDef PhaseU = Txt("phaseu", "Phase of the voltage (to the source)", "Phase der Spannung (zur Quelle)", "°");
        public static readonly ParamDef PhaseI = Txt("phasei", "Phase of the current (to the source)", "Phase des Stroms (zur Quelle)", "°");
        /// <summary>Resonance with the inductor or capacitor wired with it.</summary>
        public static readonly ParamDef Resonance = Txt("resonance", "Resonance frequency f₀ (with the wired L/C)", "Resonanzfrequenz f₀ (mit verdrahtetem L/C)", "Hz");
        public static readonly ParamDef Quality = Num("quality", "Quality factor Q (resonant circuit)", "Güte Q (Schwingkreis)");
        public static readonly ParamDef Bandwidth = Txt("bandwidth", "Bandwidth B (resonant circuit)", "Bandbreite B (Schwingkreis)", "Hz");

        /// <summary>The AC values of a resistor, capacitor or inductor in an AC circuit (parameters).</summary>
        public static readonly IReadOnlyList<ParamDef> PhaseParams = new[] { PhaseU, PhaseI };
        public static readonly IReadOnlyList<ParamDef> ResonanceParams = new[] { Resonance, Quality, Bandwidth };

        /// <summary>AC source: frequency, the load seen by the source and its power.</summary>
        public static readonly IReadOnlyList<ParamDef> SourceParams = new[] { Frequency, Impedance, Phase, PowerFactor, Apparent, Reactive };

        // ------------------------------------------------------------------ variables

        private static VarDef PhiU() => V("phiU", "Phase of the voltage to the source voltage", "Phase der Spannung zur Quellenspannung", "φ_U", Quantities.Angle, "phaseu", "°")
            with { ReadOnly = true, Min = -Math.PI, Max = Math.PI };
        private static VarDef PhiI() => V("phiI", "Phase of the current to the source voltage", "Phase des Stroms zur Quellenspannung", "φ_I", Quantities.Angle, "phasei", "°")
            with { ReadOnly = true, Min = -Math.PI, Max = Math.PI };

        public static readonly IReadOnlyList<VarDef> PhaseVariables = new[] { PhiU(), PhiI() };

        public static readonly IReadOnlyList<VarDef> ResonanceVariables = new[]
        {
            V("f0", "Resonance frequency (with the wired L/C)", "Resonanzfrequenz (mit verdrahtetem L/C)", "f₀", Quantities.Frequency, "resonance", "Hz") with { Min = 1e-6, Max = 1e15 },
            new VarDef("Qf", T("Quality factor (resonant circuit)", "Güte (Schwingkreis)"), T("Q", "Q"), Quantities.Ratio) { Param = "quality", Relevance = VarRelevance.Panel, Min = 1e-3, Max = 1e7 },
            V("B", "Bandwidth (resonant circuit)", "Bandbreite (Schwingkreis)", "B", Quantities.Frequency, "bandwidth", "Hz") with { Min = 1e-9, Max = 1e15 },
        };

        private static VarDef Lambda(VarRelevance rel = VarRelevance.Panel)
            => new VarDef("lambda", T("Power factor cos φ", "Leistungsfaktor cos φ"), T("cos φ", "cos φ"), Quantities.Ratio) { Param = "pf", Relevance = rel, Min = 0, Max = 1 };
        private static VarDef S() => V("S", "Apparent power", "Scheinleistung", "S", Quantities.ApparentPower, "apparent", "VA") with { Min = 0, Max = 1e12 };
        private static VarDef Q() => V("Q", "Reactive power", "Blindleistung", "Q", Quantities.ReactivePower, "reactive", "var") with { Min = -1e12, Max = 1e12 };

        /// <summary>The AC variables of a voltage source (panel): |Z|, φ, cos φ, S, Q of the load it feeds.</summary>
        public static readonly IReadOnlyList<VarDef> SourceVariables = new[]
        {
            V("Z", "Impedance of the load", "Impedanz der Last", "|Z|", Quantities.Resistance, "impedance", "Ω") with { Min = 1e-9, Max = 1e15 },
            V("phi", "Phase angle of the load", "Phasenwinkel der Last", "φ", Quantities.Angle, "phase", "°") with { Min = -Math.PI / 2, Max = Math.PI / 2 },
            Lambda(), S(), Q(),
        };

        // ------------------------------------------------------------------ relations

        /// <summary>S = k·U·I (k = 1 single-phase, √3 three-phase with line quantities) [S7].</summary>
        private static Relation ApparentPower(bool threePhase)
        {
            var k = threePhase ? Sqrt3 : 1;
            var kt = threePhase ? "√3·" : "";
            return new Relation(threePhase ? "elec.ac3.s" : "elec.ac.s",
                    threePhase ? T("Apparent power (three-phase, balanced)", "Scheinleistung (Drehstrom, symmetrisch)") : T("Apparent power", "Scheinleistung"),
                    "{S} = " + kt + "{U}·{I}", "S", "U", "I")
                { Source = "S7", Validity = threePhase ? T("balanced load, line voltage and line current", "symmetrische Last, Leiterspannung und Leiterstrom") : AcValidity }
                .Solve("S", kt + "{U}·{I}", v => k * v("U") * v("I"))
                .Solve("U", "{S}/(" + kt + "{I})", v => v("S") / (k * v("I")))
                .Solve("I", "{S}/(" + kt + "{U})", v => v("S") / (k * v("U")));
        }

        private static readonly Relation ActivePower =
            new Relation("elec.ac.p", T("Active power", "Wirkleistung"), "{P} = {S}·{lambda}", "P", "S", "lambda") { Source = "S7", Validity = AcValidity }
                .Solve("P", "{S}·{lambda}", v => v("S") * v("lambda"))
                .Solve("S", "{P}/{lambda}", v => v("P") / v("lambda"))
                .Solve("lambda", "{P}/{S}", v => v("P") / v("S"));

        /// <summary>cos φ from φ; φ is not taken from cos φ (its sign, inductive or capacitive, would be lost).</summary>
        private static readonly Relation PowerFactorOfPhase =
            new Relation("elec.ac.pf", T("Power factor", "Leistungsfaktor"), "{lambda} = cos {phi}", "lambda", "phi") { Source = "S6" }
                .Solve("lambda", "cos {phi}", v => Math.Cos(v("phi")));

        private static readonly Relation ReactivePower =
            new Relation("elec.ac.q", T("Reactive power", "Blindleistung"), "{Q} = {S}·sin {phi}", "Q", "S", "phi") { Source = "S7", Validity = AcValidity }
                .Solve("Q", "{S}·sin {phi}", v => v("S") * Math.Sin(v("phi")))
                .Solve("S", "{Q}/sin {phi}", v => v("Q") / Math.Sin(v("phi")))
                .Solve("phi", "arcsin({Q}/{S})", v => Math.Asin(v("Q") / v("S")));

        /// <summary>S² = P² + Q²; Q from S and P only for an inductive load (a motor), where Q ≥ 0.</summary>
        private static Relation PowerTriangle(bool inductive)
        {
            var r = new Relation(inductive ? "elec.ac.spq.ind" : "elec.ac.spq", T("Power triangle", "Leistungsdreieck"), "{S}² = {P}² + {Q}²", "S", "P", "Q")
                { Source = "S7", Validity = inductive ? T("inductive load (Q ≥ 0)", "induktive Last (Q ≥ 0)") : AcValidity }
                .Solve("S", "√({P}² + {Q}²)", v => Math.Sqrt(v("P") * v("P") + v("Q") * v("Q")))
                .Solve("P", "√({S}² − {Q}²)", v => Math.Sqrt(v("S") * v("S") - v("Q") * v("Q")));
            return inductive ? r.Solve("Q", "√({S}² − {P}²)", v => Math.Sqrt(v("S") * v("S") - v("P") * v("P"))) : r;
        }

        private static readonly Relation LoadImpedance =
            new Relation("elec.ac.z", T("Impedance (Ohm's law for AC)", "Scheinwiderstand (Ohmsches Gesetz bei Wechselstrom)"), "{U} = {Z}·{I}", "U", "Z", "I") { Source = "S7", Validity = AcValidity }
                .Solve("U", "{Z}·{I}", v => v("Z") * v("I"))
                .Solve("Z", "{U}/{I}", v => v("U") / v("I"))
                .Solve("I", "{U}/{Z}", v => v("U") / v("Z"));

        private static readonly Relation DcPower =
            new Relation("elec.power.ui", T("Electric power", "Elektrische Leistung"), "{P} = {U}·{I}", "P", "U", "I") { Source = "S7" }
                .Solve("P", "{U}·{I}", v => v("U") * v("I"))
                .Solve("U", "{P}/{I}", v => v("P") / v("I"))
                .Solve("I", "{P}/{U}", v => v("P") / v("U"));

        private static readonly IReadOnlyList<Relation> AcSourceRelations = new[]
        {
            ApparentPower(false), ActivePower, PowerFactorOfPhase, ReactivePower, PowerTriangle(false), LoadImpedance,
        };

        /// <summary>Direct current: no phase shift, so φ = 0 follows from cos φ = P/S = 1 (used only for DC).</summary>
        private static readonly Relation DcPhase =
            new Relation("elec.dc.phi", T("Direct current: no phase shift", "Gleichstrom: keine Phasenverschiebung"), "{phi} = arccos {lambda}", "phi", "lambda") { Source = "S7" }
                .Solve("phi", "arccos {lambda}", v => Math.Acos(Math.Min(1, v("lambda"))));

        /// <summary>
        /// DC: P = U·I, and the AC quantities are trivial (S = U·I = P, cos φ = 1, φ = 0, Q = 0, |Z| = U/I is the load
        /// resistance), so a DC source with U and I is fully determined.
        /// </summary>
        private static readonly IReadOnlyList<Relation> DcSourceRelations = new[]
        {
            DcPower, ApparentPower(false), ActivePower, DcPhase, ReactivePower, LoadImpedance,
        };

        /// <summary>Voltage source: DC (and current source) P = U·I with trivial AC values; AC the AC power relations and |Z| = U/I.</summary>
        public static IEnumerable<BoundRelation> SourceLinks(SketchObject o, RenderContext ctx)
            => (o.Str("kind", "dc") == "ac" ? AcSourceRelations : DcSourceRelations).Select(r => BoundRelation.Local(r, o.Id));

        /// <summary>The frequency of an AC source: the parameter, else a frequency in the value text ("230 V 50 Hz"); null if none.</summary>
        public static double? SourceFrequency(SketchObject o)
        {
            if (ElecValues.Rating(o, "frequency", Quantities.Frequency, "Hz") is double f) return f > 0 ? f : null;
            foreach (var t in ElecValues.Tokens(o.Str("value", ""), Quantities.Frequency)) return t.Value > 0 ? t.Value : null;
            return null;
        }

        /// <summary>X·I = U for a capacitor or inductor at the frequency of its reactance [S7].</summary>
        public static readonly Relation ReactiveOhm =
            new Relation("elec.ac.ohm.x", T("Ohm's law for a reactance", "Ohmsches Gesetz für einen Blindwiderstand"), "{U} = {X}·{I}", "U", "X", "I") { Source = "S7", Validity = AcValidity }
                .Solve("U", "{X}·{I}", v => v("X") * v("I"))
                .Solve("X", "{U}/{I}", v => v("U") / v("I"))
                .Solve("I", "{U}/{X}", v => v("U") / v("X"));

        /// <summary>Bandwidth of a resonant circuit B = f₀/Q [S7].</summary>
        public static readonly Relation BandwidthRelation =
            new Relation("elec.ac.bandwidth", T("Bandwidth of a resonant circuit", "Bandbreite eines Schwingkreises"), "{B} = {f0}/{Qf}", "B", "f0", "Qf") { Source = "S7" }
                .Solve("B", "{f0}/{Qf}", v => v("f0") / v("Qf"))
                .Solve("f0", "{B}·{Qf}", v => v("B") * v("Qf"))
                .Solve("Qf", "{f0}/{B}", v => v("f0") / v("B"));

        /// <summary>The AC relations of a capacitor or inductor (in addition to its reactance).</summary>
        public static readonly IReadOnlyList<Relation> ReactiveRelations = new[] { ReactiveOhm, BandwidthRelation };

        // ------------------------------------------------------------------ motor and generator

        public static readonly ParamDef Starting = Choice("starting", T("Starting (three-phase motor)", "Anlauf (Drehstrommotor)"), "dol",
            ("dol", "Direct on line", "Direktanlauf"), ("stardelta", "Star-delta (Y–Δ)", "Stern-Dreieck (Y–Δ)"));

        public static readonly IReadOnlyList<ParamDef> MachineParams = new[]
        {
            Txt("voltage", "Voltage U (line voltage for 3~)", "Spannung U (Leiterspannung bei 3~)", "V"),
            Txt("current", "Current I (line current for 3~)", "Strom I (Leiterstrom bei 3~)", "A"),
            Txt("power", "Electrical (active) power P", "Elektrische (Wirk-)Leistung P", "W"),
            PowerFactor, Apparent, Reactive,
            new ParamDef("efficiency", T("Efficiency η", "Wirkungsgrad η"), ParamKind.Text) { Unit = "%" },
            Txt("pmech", "Mechanical power P_mech (motor: output, generator: input)", "Mechanische Leistung P_mech (Motor: abgegeben, Generator: aufgenommen)", "W"),
            ValueLabels.ShowValues,
        };

        public static readonly IReadOnlyList<ParamDef> MotorParams = MachineParams.Take(MachineParams.Count - 1).Concat(new[]
        {
            Starting,
            Num("kstart", "Starting current ratio I_A/I_N (datasheet)", "Anlaufstromverhältnis I_A/I_N (Datenblatt)"),
            Txt("istart", "Starting current direct on line I_A", "Anlaufstrom direkt I_A", "A"),
            Txt("istartyd", "Starting current in star-delta I_A,YΔ", "Anlaufstrom Stern-Dreieck I_A,YΔ", "A"),
            ValueLabels.ShowValues,
        }).ToList();

        private static VarDef Pm(string en, string de) => V("Pm", en, de, "P_mech", Quantities.Power, "pmech", "W") with { Min = 0, Max = 1e10 };

        public static readonly IReadOnlyList<VarDef> GeneratorVariables = new[]
        {
            V("U", "Voltage (line voltage for 3~)", "Spannung (Leiterspannung bei 3~)", "U", Quantities.Voltage, "voltage", "V", VarRelevance.Card),
            V("I", "Current (line current for 3~)", "Strom (Leiterstrom bei 3~)", "I", Quantities.Current, "current", "A", VarRelevance.Card),
            V("P", "Electrical power", "Elektrische Leistung", "P", Quantities.Power, "power", "W", VarRelevance.Card),
            Lambda(VarRelevance.Card), S(), Q(),
            new VarDef("eta", T("Efficiency", "Wirkungsgrad"), T("η", "η"), Quantities.Efficiency) { Param = "efficiency", Unit = "%", Relevance = VarRelevance.Panel, Min = 0.01, Max = 1 },
            Pm("Mechanical input power", "Mechanische Antriebsleistung"),
        };

        public static readonly IReadOnlyList<VarDef> MotorVariables = GeneratorVariables.Take(GeneratorVariables.Count - 1).Concat(new[]
        {
            Pm("Mechanical output power", "Mechanische Abgabeleistung"),
            new VarDef("kst", T("Starting current ratio I_A/I_N", "Anlaufstromverhältnis I_A/I_N"), T("I_A/I_N", "I_A/I_N"), Quantities.Ratio) { Param = "kstart", Relevance = VarRelevance.Panel, Min = 1, Max = 20 },
            V("Ist", "Starting current direct on line", "Anlaufstrom direkt", "I_A", Quantities.Current, "istart", "A") with { Min = 0, Max = 1e6 },
            V("IstYD", "Starting current in star-delta", "Anlaufstrom Stern-Dreieck", "I_A,YΔ", Quantities.Current, "istartyd", "A") with { Min = 0, Max = 1e6 },
        }).ToList();

        private static readonly Relation MotorEfficiency =
            new Relation("elec.motor.eta", T("Motor efficiency", "Wirkungsgrad des Motors"), "{Pm} = {eta}·{P}", "Pm", "eta", "P") { Source = "S15" }
                .Solve("Pm", "{eta}·{P}", v => v("eta") * v("P"))
                .Solve("P", "{Pm}/{eta}", v => v("Pm") / v("eta"))
                .Solve("eta", "{Pm}/{P}", v => v("Pm") / v("P"));

        private static readonly Relation GeneratorEfficiency =
            new Relation("elec.generator.eta", T("Generator efficiency", "Wirkungsgrad des Generators"), "{P} = {eta}·{Pm}", "P", "eta", "Pm") { Source = "S15" }
                .Solve("P", "{eta}·{Pm}", v => v("eta") * v("Pm"))
                .Solve("Pm", "{P}/{eta}", v => v("P") / v("eta"))
                .Solve("eta", "{P}/{Pm}", v => v("P") / v("Pm"));

        private static readonly Relation StartingCurrent =
            new Relation("elec.motor.start", T("Starting current", "Anlaufstrom"), "{Ist} = {kst}·{I}", "Ist", "kst", "I")
            { Source = "S15", Validity = T("I = rated current, I_A/I_N from the datasheet", "I = Bemessungsstrom, I_A/I_N aus dem Datenblatt") }
                .Solve("Ist", "{kst}·{I}", v => v("kst") * v("I"))
                .Solve("kst", "{Ist}/{I}", v => v("Ist") / v("I"))
                .Solve("I", "{Ist}/{kst}", v => v("Ist") / v("kst"));

        /// <summary>
        /// Star-delta starting: in star each winding gets U/√3 instead of U, so the line current (and the torque) drop to one
        /// third of direct-on-line starting in delta [S15].
        /// </summary>
        private static readonly Relation StarDelta =
            new Relation("elec.motor.stardelta", T("Star-delta starting current", "Anlaufstrom bei Stern-Dreieck-Anlauf"), "{IstYD} = {Ist}/3", "IstYD", "Ist")
            { Source = "S15", Validity = T("motor rated for delta at the line voltage", "Motor im Dreieck für die Leiterspannung bemessen") }
                .Solve("IstYD", "{Ist}/3", v => v("Ist") / 3)
                .Solve("Ist", "3·{IstYD}", v => 3 * v("IstYD"));

        private static readonly IReadOnlyList<Relation> DcMachine = new[] { DcPower, ApparentPower(false), ActivePower, PowerTriangle(true) };
        private static readonly IReadOnlyList<Relation> SinglePhaseMachine = new[] { ApparentPower(false), ActivePower, PowerTriangle(true) };
        private static readonly IReadOnlyList<Relation> ThreePhaseMachine = new[] { ApparentPower(true), ActivePower, PowerTriangle(true) };

        /// <summary>Motor or generator by its supply: DC P = U·I (cos φ = 1); AC or unspecified S = U·I; three-phase S = √3·U·I; then P = S·cos φ.</summary>
        public static Func<SketchObject, RenderContext, IEnumerable<BoundRelation>> MachineLinks(bool motor) => (o, ctx) =>
        {
            var supply = o.Str("supply", "none");
            // An unspecified supply is taken as single-phase AC (the general case: with cos φ = 1 it is DC).
            var list = new List<Relation>(supply switch { "dc" => DcMachine, "ac3" => ThreePhaseMachine, _ => SinglePhaseMachine });
            list.Add(motor ? MotorEfficiency : GeneratorEfficiency);
            if (motor)
            {
                list.Add(StartingCurrent);
                if (supply == "ac3" && o.Str("starting", "dol") == "stardelta") list.Add(StarDelta);
            }
            return list.Select(r => BoundRelation.Local(r, o.Id));
        };

        // ------------------------------------------------------------------ cable (installation cable route)

        /// <summary>Resistivity of copper at 20 °C, 1/58 Ω·mm²/m (IEC 60028, [S12]).</summary>
        public const double RhoCopper = 0.017241e-6;
        /// <summary>Resistivity of aluminium at 20 °C, 0.028264 Ω·mm²/m (IEC 60889, [S12]).</summary>
        public const double RhoAluminium = 0.028264e-6;

        public static readonly ParamDef CableSystem = Choice("system", T("System", "Netzart"), "auto",
            ("auto", "From the conductors (4–5: three-phase)", "Aus den Adern (4–5: Drehstrom)"), ("ac1", "Single-phase AC", "Wechselstrom (einphasig)"),
            ("ac3", "Three-phase AC", "Drehstrom"), ("dc", "DC", "Gleichstrom"));
        public static readonly ParamDef CableMaterial = Choice("material", T("Conductor material", "Leiterwerkstoff"), "cu",
            ("cu", "Copper", "Kupfer"), ("al", "Aluminium", "Aluminium"));

        public static readonly IReadOnlyList<ParamDef> CableParams = new[]
        {
            CableSystem, CableMaterial,
            Txt("length", "Length l (one way)", "Länge l (einfach)", "m"),
            Txt("section", "Cross-section A", "Querschnitt A", "mm²"),
            Txt("current", "Current I (operating current)", "Strom I (Betriebsstrom)", "A"),
            Txt("voltage", "Nominal voltage U_n (line voltage for 3~)", "Nennspannung U_n (Leiterspannung bei 3~)", "V"),
            PowerFactor,
            Txt("rho", "Resistivity ρ (default: copper at 20 °C)", "Spezifischer Widerstand ρ (Vorgabe: Kupfer bei 20 °C)", "Ω·mm²/m"),
            Txt("resistance", "Resistance of one conductor R", "Widerstand eines Leiters R", "Ω"),
            Txt("drop", "Voltage drop ΔU", "Spannungsfall ΔU", "V"),
            new ParamDef("droprel", T("Relative voltage drop Δu", "Relativer Spannungsfall Δu"), ParamKind.Text) { Unit = "%" },
            Txt("loss", "Power loss P_loss", "Verlustleistung P_V", "W"),
            new ParamDef("dumax", T("Permitted voltage drop Δu_max (user, for the check)", "Zulässiger Spannungsfall Δu_max (Benutzer, für die Prüfung)"), ParamKind.Text) { Unit = "%" },
        };

        public static readonly IReadOnlyList<VarDef> CableVariables = new[]
        {
            V("l", "Length (one way)", "Länge (einfach)", "l", Quantities.Length, "length", "m", VarRelevance.Card) with { Min = 1e-3, Max = 1e5 },
            V("A", "Cross-section", "Querschnitt", "A", Quantities.Area, "section", "mm²", VarRelevance.Card) with { Min = 1e-8, Max = 1e-3 },
            V("I", "Current", "Strom", "I", Quantities.Current, "current", "A", VarRelevance.Card) with { Min = 0, Max = 1e5 },
            new VarDef("du", T("Relative voltage drop", "Relativer Spannungsfall"), T("Δu", "Δu"), Quantities.Ratio) { Param = "droprel", Unit = "%", Min = 0, Max = 1 },
            V("U", "Nominal voltage", "Nennspannung", "U_n", Quantities.Voltage, "voltage", "V") with { Min = 1, Max = 1e6 },
            Lambda() with { Default = 1 },
            new VarDef("rho", T("Resistivity", "Spezifischer Widerstand"), T("ρ", "ρ"), Quantities.Resistivity)
                { Param = "rho", Unit = "Ω·mm²/m", Relevance = VarRelevance.Panel, Default = RhoCopper, Min = 1e-9, Max = 1e-6 },
            V("R", "Resistance of one conductor", "Widerstand eines Leiters", "R", Quantities.Resistance, "resistance", "Ω") with { Min = 1e-9, Max = 1e6 },
            V("dU", "Voltage drop", "Spannungsfall", "ΔU", Quantities.Voltage, "drop", "V") with { Min = 0, Max = 1e5 },
            V("Pl", "Power loss", "Verlustleistung", "P_loss", Quantities.Power, "loss", "W") with { Symbol = T("P_loss", "P_V"), Min = 0, Max = 1e9 },
        };

        private static readonly Relation ConductorResistance =
            new Relation("elec.cable.r", T("Conductor resistance", "Leiterwiderstand"), "{R} = {rho}·{l}/{A}", "R", "rho", "l", "A")
            { Source = "S12", Validity = T("at the temperature of ρ (default 20 °C)", "bei der Temperatur von ρ (Vorgabe 20 °C)") }
                .Solve("R", "{rho}·{l}/{A}", v => v("rho") * v("l") / v("A"))
                .Solve("l", "{R}·{A}/{rho}", v => v("R") * v("A") / v("rho"))
                .Solve("A", "{rho}·{l}/{R}", v => v("rho") * v("l") / v("R"))
                .Solve("rho", "{R}·{A}/{l}", v => v("R") * v("A") / v("l"));

        private static readonly Relation RelativeDrop =
            new Relation("elec.cable.du", T("Relative voltage drop", "Relativer Spannungsfall"), "{du} = {dU}/{U}", "du", "dU", "U") { Source = "S8" }
                .Solve("du", "{dU}/{U}", v => v("dU") / v("U"))
                .Solve("dU", "{du}·{U}", v => v("du") * v("U"))
                .Solve("U", "{dU}/{du}", v => v("dU") / v("du"));

        public static readonly IReadOnlyList<Relation> CableRelations = new[] { ConductorResistance, RelativeDrop };

        private static readonly I18nText DropValidity = T("line reactance X′ neglected (small cross-sections)", "Leitungsreaktanz X′ vernachlässigt (kleine Querschnitte)");

        /// <summary>ΔU = k·R·I·cos φ with k = 2 (single-phase, outgoing and return conductor) or √3 (three-phase, line voltage) [S7, S8].</summary>
        private static Relation Drop(string id, I18nText name, double k, string kt, bool withPf)
        {
            if (!withPf)
                return new Relation(id, name, "{dU} = " + kt + "·{R}·{I}", "dU", "R", "I") { Source = "S8" }
                    .Solve("dU", kt + "·{R}·{I}", v => k * v("R") * v("I"))
                    .Solve("R", "{dU}/(" + kt + "·{I})", v => v("dU") / (k * v("I")))
                    .Solve("I", "{dU}/(" + kt + "·{R})", v => v("dU") / (k * v("R")));
            return new Relation(id, name, "{dU} = " + kt + "·{R}·{I}·{lambda}", "dU", "R", "I", "lambda") { Source = "S8", Validity = DropValidity }
                .Solve("dU", kt + "·{R}·{I}·{lambda}", v => k * v("R") * v("I") * v("lambda"))
                .Solve("R", "{dU}/(" + kt + "·{I}·{lambda})", v => v("dU") / (k * v("I") * v("lambda")))
                .Solve("I", "{dU}/(" + kt + "·{R}·{lambda})", v => v("dU") / (k * v("R") * v("lambda")))
                .Solve("lambda", "{dU}/(" + kt + "·{R}·{I})", v => v("dU") / (k * v("R") * v("I")));
        }

        /// <summary>P_loss = m·I²·R with m conductors carrying the current (2 single-phase/DC, 3 three-phase) [S7].</summary>
        private static Relation Loss(int m)
            => new Relation("elec.cable.loss" + m, T("Cable losses", "Leitungsverluste"), "{Pl} = " + m + "·{I}²·{R}", "Pl", "I", "R") { Source = "S7" }
                .Solve("Pl", m + "·{I}²·{R}", v => m * v("I") * v("I") * v("R"))
                .Solve("I", "√({Pl}/(" + m + "·{R}))", v => Math.Sqrt(v("Pl") / (m * v("R"))))
                .Solve("R", "{Pl}/(" + m + "·{I}²)", v => v("Pl") / (m * v("I") * v("I")));

        private static readonly IReadOnlyList<Relation> SinglePhaseCable = new[]
            { Drop("elec.cable.drop1", T("Voltage drop (single-phase)", "Spannungsfall (Wechselstrom)"), 2, "2", true), Loss(2) };
        private static readonly IReadOnlyList<Relation> ThreePhaseCable = new[]
            { Drop("elec.cable.drop3", T("Voltage drop (three-phase, balanced)", "Spannungsfall (Drehstrom, symmetrisch)"), Sqrt3, "√3", true), Loss(3) };
        private static readonly IReadOnlyList<Relation> DcCable = new[]
            { Drop("elec.cable.dropdc", T("Voltage drop (DC)", "Spannungsfall (Gleichstrom)"), 2, "2", false), Loss(2) };

        private static readonly Relation Aluminium =
            new Relation("elec.cable.rho.al", T("Resistivity of aluminium (20 °C)", "Spezifischer Widerstand von Aluminium (20 °C)"), "{rho} = 0.028264 Ω·mm²/m", "rho") { Source = "S12" }
                .Solve("rho", "0.028264 Ω·mm²/m", _ => RhoAluminium);

        /// <summary>The system of a cable: chosen, or from its conductor count (4 or 5 conductors: three-phase).</summary>
        public static string CableSystemOf(SketchObject o)
        {
            var s = o.Str("system", "auto");
            if (s != "auto") return s;
            return Count(o, "conductors", 3, 0, 12) >= 4 ? "ac3" : "ac1";
        }

        public static IEnumerable<BoundRelation> CableLinks(SketchObject o)
        {
            var rels = CableSystemOf(o) switch { "ac3" => ThreePhaseCable, "dc" => DcCable, _ => SinglePhaseCable };
            foreach (var r in rels) yield return BoundRelation.Local(r, o.Id);
            if (o.Str("material", "cu") == "al") yield return BoundRelation.Local(Aluminium, o.Id);
        }

        /// <summary>elec.cable.drop: the relative voltage drop is above the permitted value the user entered (IEC 60364-5-52 clause 525, [S8]).</summary>
        public static readonly ValueCheck CableDrop = new("elec.cable.drop", CheckSeverity.Hint,
            T("The voltage drop of this cable is above the permitted value Δu_max you entered. Choose a larger cross-section or a shorter route.",
              "Der Spannungsfall dieser Leitung liegt über dem eingegebenen zulässigen Wert Δu_max. Einen größeren Querschnitt oder einen kürzeren Weg wählen."),
            "Δu ≤ Δu_max", c => c["du"] is double du && ElecValues.Rating(c.Object, "dumax", Quantities.Ratio, "%") is double max && du > max * (1 + 1e-9)) { Source = "S8" };

        /// <summary>
        /// elec.ac.resonance: an AC source drives a resonant circuit within its bandwidth around f₀ (|f − f₀| ≤ B/2): in a series
        /// circuit the voltages across L and C can be far above the source voltage (Q times at resonance) [S7].
        /// </summary>
        public static readonly ValueCheck NearResonance = new("elec.ac.resonance", CheckSeverity.Info,
            T("The circuit works within the bandwidth of its resonance: voltages (series) or currents (parallel) at L and C can be far above those of the source.",
              "Der Kreis arbeitet innerhalb der Bandbreite seiner Resonanz: Spannungen (Reihe) oder Ströme (parallel) an L und C können weit über denen der Quelle liegen."),
            "|f − f₀| > B/2", c => c.Table.Get(c.Object.Id, "f") is { Value: double f } && c["f0"] is double f0 && c["B"] is double b
                && b > 0 && Math.Abs(f - f0) <= b / 2) { Source = "S7" };
    }
}
