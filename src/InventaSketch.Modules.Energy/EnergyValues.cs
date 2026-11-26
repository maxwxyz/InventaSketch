using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Energy
{
    /// <summary>
    /// Element values of the energy elements (Calc 4, engineering-calculations.md sections 2.10 and 3.8): PV module, string and
    /// array (STC data, the open-circuit voltage at the lowest site temperature, the MPP voltage at the highest), inverter (DC
    /// power, efficiency, DC/AC ratio, AC current per phase, input limits), boards (grid busbar and meter: the sum of the
    /// feeders and the current per phase), battery (energy, C-rate, run time), EV charger, heat pump (COP), wind turbine
    /// (P = ½·ρ·A·v³·c_p) and transformer (current per side). Every value is optional; existing rating parameters (Wp, kW,
    /// kWh, kV, kVA) are read in their stored units.
    /// <para>
    /// Port flows (<see cref="ValueNets"/>): cables join ports into nets. DC side: the string voltages (<c>UocMax</c>,
    /// <c>UmppMin</c>, <c>Ustr</c>) are shared by the whole net (parallel strings must agree), the short-circuit current
    /// <c>Isc</c> and the STC power <c>Pdc</c> of parallel strings add up at the inverter. AC side: the system voltage
    /// <c>Uac</c> is shared, the AC power <c>Pac</c> of the inverters adds up at a busbar (every tap) or at the device side
    /// (port b) of a meter; the meter passes it on at its grid side (port a). Heat side: heat pipes join heat ports (V̇ and the
    /// flow temperature θ, named as in building services), hydrogen pipes join hydrogen ports (P_H2, ṁ). Other modules meet
    /// these elements only through the physical keys (<see cref="NetKeys"/>, stated in <see cref="NetSpec.Keys"/>; no code
    /// dependency), so an energy heat pump on a building heating pipe passes V̇ and θ to a radiator.
    /// </para>
    /// <para>
    /// Also: CHP (η_el, η_th), thermal storage (Q = ρ·V·c·Δθ), electrolyser and fuel cell (η, P_el, P_H2, ṁ), hydro power plant
    /// (P = ρ·g·Q·H·η), pumped storage (E = ρ·g·V·H) and the EV charging time t = E/P.
    /// </para>
    /// </summary>
    internal static class EnergyValues
    {
        private static I18nText T(string en, string de) => new(en, de);

        internal sealed class Spec
        {
            public static readonly Spec Empty = new(Array.Empty<VarDef>(), Array.Empty<Relation>(), Array.Empty<ParamDef>(), Array.Empty<ValueCheck>());

            public Spec(IReadOnlyList<VarDef> variables, IReadOnlyList<Relation> relations, IReadOnlyList<ParamDef> parameters, IReadOnlyList<ValueCheck> checks)
            {
                Variables = variables; Relations = relations; Params = parameters; Checks = checks;
            }

            public IReadOnlyList<VarDef> Variables { get; }
            public IReadOnlyList<Relation> Relations { get; }
            public IReadOnlyList<ParamDef> Params { get; }
            public IReadOnlyList<ValueCheck> Checks { get; }
            public Func<SketchObject, string, IEnumerable<PortVar>> Ports { get; init; } = (_, _) => Array.Empty<PortVar>();
            /// <summary>Relations that depend on the element's choices (phases, DC).</summary>
            public Func<SketchObject, IEnumerable<Relation>>? Local { get; init; }
        }

        // ------------------------------------------------------------------ parameters and variables

        private static ParamDef Text(string key, I18nText label, string? unit) => new(key, label, ParamKind.Text) { Unit = unit, Advanced = true };

        private static VarDef V(string key, I18nText name, I18nText symbol, QuantityKind kind, string? unit, string param, VarRelevance rel = VarRelevance.Card)
            => new(key, name, symbol, kind) { Unit = unit, Param = param, Relevance = rel };

        /// <summary>Text parameters for the variables without a parameter of their own (existing rating parameters are reused).</summary>
        private static ParamDef[] Params(IEnumerable<VarDef> vars, params string[] existing)
            => vars.Where(v => !existing.Contains(v.ParamKey)).Select(v => Text(v.ParamKey, v.Name, v.Unit)).Append(ValueLabels.ShowValues).ToArray();

        private const double Stc = 298.15;
        private static readonly double Sqrt3 = Math.Sqrt(3);

        private static VarDef Volt(string key, I18nText name, I18nText sym, string param, VarRelevance rel) => V(key, name, sym, Quantities.Voltage, "V", param, rel) with { Min = 0, Max = 1e7 };
        private static VarDef Amp(string key, I18nText name, I18nText sym, string param, VarRelevance rel) => V(key, name, sym, Quantities.Current, "A", param, rel) with { Min = 0, Max = 1e6 };
        private static VarDef Watt(string key, I18nText name, I18nText sym, string param, VarRelevance rel, string unit = "W") => V(key, name, sym, Quantities.Power, unit, param, rel) with { Min = 0, Max = 1e10 };
        private static VarDef Temp(string key, I18nText name, I18nText sym, string param, VarRelevance rel) => V(key, name, sym, Quantities.Temperature, "°C", param, rel) with { Min = 173.15, Max = 473.15 };

        private static VarDef Uoc(VarRelevance rel = VarRelevance.Panel) => Volt("Uoc", T("Open-circuit voltage (STC, module)", "Leerlaufspannung (STC, Modul)"), T("U_oc", "U_oc"), "voc", rel);
        private static VarDef Umpp(VarRelevance rel = VarRelevance.Panel) => Volt("Umpp", T("MPP voltage (STC, module)", "MPP-Spannung (STC, Modul)"), T("U_mpp", "U_mpp"), "vmp", rel);
        private static VarDef Isc(VarRelevance rel = VarRelevance.Panel) => Amp("Isc", T("Short-circuit current (STC)", "Kurzschlussstrom (STC)"), T("I_sc", "I_sc"), "isc", rel);
        private static VarDef Impp(VarRelevance rel = VarRelevance.Panel) => Amp("Impp", T("MPP current (STC)", "MPP-Strom (STC)"), T("I_mpp", "I_mpp"), "imp", rel);
        private static VarDef Beta() => V("beta", T("Temperature coefficient of U_oc (per K)", "Temperaturkoeffizient von U_oc (je K)"), T("β", "β"), Quantities.Ratio, "%", "betaVoc", VarRelevance.Panel) with { Min = -0.05, Max = 0.05 };
        private static VarDef ThMin() => Temp("thmin", T("Lowest module temperature (site)", "Tiefste Modultemperatur (Standort)"), T("θ_min", "θ_min"), "tmin", VarRelevance.Panel);
        private static VarDef ThMax() => Temp("thmax", T("Highest module temperature (site)", "Höchste Modultemperatur (Standort)"), T("θ_max", "θ_max"), "tmax", VarRelevance.Panel);
        private static VarDef UocMax(I18nText name, VarRelevance rel = VarRelevance.Card) => Volt("UocMax", name, T("U_oc,max", "U_oc,max"), "vocMax", rel);
        private static VarDef UmppMin(I18nText name, VarRelevance rel = VarRelevance.Panel) => Volt("UmppMin", name, T("U_mpp,min", "U_mpp,min"), "vmpMin", rel);

        // ------------------------------------------------------------------ relations

        /// <summary>U(θ) = U_STC·(1 + β·(θ − 25 °C)) for n modules in series [S33, S34].</summary>
        private static Relation Corrected(string id, I18nText name, string result, string u, string th, bool series, I18nText? validity = null) =>
            series
                ? new Relation(id, name, "{" + result + "} = {Ns}·{" + u + "}·(1 + {beta}·({" + th + "} − 25 °C))", result, "Ns", u, "beta", th) { Source = "S33", Validity = validity }
                    .Solve(result, "{Ns}·{" + u + "}·(1 + {beta}·({" + th + "} − 25 °C))", v => v("Ns") * v(u) * (1 + v("beta") * (v(th) - Stc)))
                    .Solve(u, "{" + result + "}/({Ns}·(1 + {beta}·({" + th + "} − 25 °C)))", v => v(result) / (v("Ns") * (1 + v("beta") * (v(th) - Stc))))
                    .Solve("Ns", "{" + result + "}/({" + u + "}·(1 + {beta}·({" + th + "} − 25 °C)))", v => v(result) / (v(u) * (1 + v("beta") * (v(th) - Stc))))
                    .Solve("beta", "({" + result + "}/({Ns}·{" + u + "}) − 1)/({" + th + "} − 25 °C)", v => (v(result) / (v("Ns") * v(u)) - 1) / (v(th) - Stc))
                    .Solve(th, "25 °C + ({" + result + "}/({Ns}·{" + u + "}) − 1)/{beta}", v => Stc + (v(result) / (v("Ns") * v(u)) - 1) / v("beta"))
                : new Relation(id, name, "{" + result + "} = {" + u + "}·(1 + {beta}·({" + th + "} − 25 °C))", result, u, "beta", th) { Source = "S33", Validity = validity }
                    .Solve(result, "{" + u + "}·(1 + {beta}·({" + th + "} − 25 °C))", v => v(u) * (1 + v("beta") * (v(th) - Stc)))
                    .Solve(u, "{" + result + "}/(1 + {beta}·({" + th + "} − 25 °C))", v => v(result) / (1 + v("beta") * (v(th) - Stc)))
                    .Solve("beta", "({" + result + "}/{" + u + "} − 1)/({" + th + "} − 25 °C)", v => (v(result) / v(u) - 1) / (v(th) - Stc))
                    .Solve(th, "25 °C + ({" + result + "}/{" + u + "} − 1)/{beta}", v => Stc + (v(result) / v(u) - 1) / v("beta"));

        private static readonly I18nText MaxName = T("Open-circuit voltage at the lowest temperature", "Leerlaufspannung bei tiefster Temperatur");
        private static readonly I18nText MinName = T("MPP voltage at the highest temperature (β of U_oc)", "MPP-Spannung bei höchster Temperatur (β von U_oc)");
        private static readonly I18nText MinValidity = T("uses β of U_oc for the MPP voltage (approximation)", "verwendet β von U_oc für die MPP-Spannung (Näherung)");

        /// <summary>P_mpp = U_mpp·I_mpp, 2 % tolerance for rounded datasheet values [S33].</summary>
        private static readonly Relation Mpp =
            new Relation("pv.mpp", T("MPP power", "MPP-Leistung"), "{P} = {Umpp}·{Impp}", "P", "Umpp", "Impp")
            {
                Source = "S33", Tolerance = 0.02, Validity = T("STC; datasheet values are rounded (2 %)", "STC; Datenblattwerte sind gerundet (2 %)"),
            }
                .Solve("P", "{Umpp}·{Impp}", v => v("Umpp") * v("Impp"))
                .Solve("Umpp", "{P}/{Impp}", v => v("P") / v("Impp"))
                .Solve("Impp", "{P}/{Umpp}", v => v("P") / v("Umpp"));

        private static Relation Product(string id, I18nText name, string result, string a, string b, string source = "S34") =>
            new Relation(id, name, "{" + result + "} = {" + a + "}·{" + b + "}", result, a, b) { Source = source }
                .Solve(result, "{" + a + "}·{" + b + "}", v => v(a) * v(b))
                .Solve(a, "{" + result + "}/{" + b + "}", v => v(result) / v(b))
                .Solve(b, "{" + result + "}/{" + a + "}", v => v(result) / v(a));

        private static readonly Relation StringPower = Product("pv.string.power", T("String power", "Stringleistung"), "P", "Ns", "Pm");
        private static readonly Relation StringVoltage = Product("pv.string.voltage", T("String MPP voltage", "MPP-Spannung des Strings"), "Ustr", "Ns", "Umpp");
        private static readonly Relation ModuleMpp =
            new Relation("pv.mpp", T("MPP power", "MPP-Leistung"), "{Pm} = {Umpp}·{Impp}", "Pm", "Umpp", "Impp")
            {
                Source = "S33", Tolerance = 0.02, Validity = T("STC; datasheet values are rounded (2 %)", "STC; Datenblattwerte sind gerundet (2 %)"),
            }
                .Solve("Pm", "{Umpp}·{Impp}", v => v("Umpp") * v("Impp"))
                .Solve("Umpp", "{Pm}/{Impp}", v => v("Pm") / v("Impp"))
                .Solve("Impp", "{Pm}/{Umpp}", v => v("Pm") / v("Umpp"));

        private static readonly Relation ArrayPower = Product("pv.array.power", T("Array power", "Generatorleistung"), "P", "Np", "Pstr");
        private static readonly Relation ArrayCurrent = Product("pv.array.current", T("Array short-circuit current", "Kurzschlussstrom des Generators"), "Isc", "Np", "Iscs");

        /// <summary>Inverter P_AC = η·P_DC [S33].</summary>
        private static readonly Relation InverterEff = Product("pv.inverter.efficiency", T("Inverter efficiency", "Wechselrichter-Wirkungsgrad"), "Pac", "eta", "Pdc", "S33");

        /// <summary>DC/AC ratio R = P_DC,STC/P_AC,rated [S34].</summary>
        private static readonly Relation DcAc =
            new Relation("pv.dcac", T("DC/AC ratio", "DC/AC-Verhältnis"), "{R} = {Pdc}/{PacN}", "R", "Pdc", "PacN") { Source = "S34" }
                .Solve("R", "{Pdc}/{PacN}", v => v("Pdc") / v("PacN"))
                .Solve("Pdc", "{R}·{PacN}", v => v("R") * v("PacN"))
                .Solve("PacN", "{Pdc}/{R}", v => v("Pdc") / v("R"));

        /// <summary>Three-phase current per phase I = P/(√3·U·cos φ), U line-to-line [S7].</summary>
        private static Relation ThreePhase(string id, string i, string p, string u, string pf) =>
            new Relation(id, T("Current per phase (three-phase)", "Strom je Außenleiter (dreiphasig)"), "{" + i + "} = {" + p + "}/(√3·{" + u + "}·{" + pf + "})", i, p, u, pf)
            {
                Source = "S7", Validity = T("symmetrical three-phase load, U line-to-line", "symmetrische Drehstromlast, U Außenleiterspannung"),
            }
                .Solve(i, "{" + p + "}/(√3·{" + u + "}·{" + pf + "})", v => v(p) / (Sqrt3 * v(u) * v(pf)))
                .Solve(p, "√3·{" + u + "}·{" + i + "}·{" + pf + "}", v => Sqrt3 * v(u) * v(i) * v(pf))
                .Solve(u, "{" + p + "}/(√3·{" + i + "}·{" + pf + "})", v => v(p) / (Sqrt3 * v(i) * v(pf)))
                .Solve(pf, "{" + p + "}/(√3·{" + u + "}·{" + i + "})", v => v(p) / (Sqrt3 * v(u) * v(i)));

        /// <summary>Single-phase current I = P/((U/√3)·cos φ) on a three-phase system with line-to-line voltage U [S7].</summary>
        private static Relation SinglePhase(string id, string i, string p, string u, string pf) =>
            new Relation(id, T("Current (single-phase)", "Strom (einphasig)"), "{" + i + "} = {" + p + "}/(({" + u + "}/√3)·{" + pf + "})", i, p, u, pf)
            {
                Source = "S7", Validity = T("phase to neutral, U line-to-line of the system", "Außenleiter gegen Neutralleiter, U Außenleiterspannung des Netzes"),
            }
                .Solve(i, "{" + p + "}/(({" + u + "}/√3)·{" + pf + "})", v => v(p) * Sqrt3 / (v(u) * v(pf)))
                .Solve(p, "({" + u + "}/√3)·{" + i + "}·{" + pf + "}", v => v(u) / Sqrt3 * v(i) * v(pf))
                .Solve(u, "√3·{" + p + "}/({" + i + "}·{" + pf + "})", v => Sqrt3 * v(p) / (v(i) * v(pf)));

        /// <summary>DC current I = P/U [S7].</summary>
        private static Relation Dc(string id, string i, string p, string u) =>
            new Relation(id, T("Current (DC)", "Strom (Gleichstrom)"), "{" + i + "} = {" + p + "}/{" + u + "}", i, p, u) { Source = "S7" }
                .Solve(i, "{" + p + "}/{" + u + "}", v => v(p) / v(u))
                .Solve(p, "{" + u + "}·{" + i + "}", v => v(u) * v(i))
                .Solve(u, "{" + p + "}/{" + i + "}", v => v(p) / v(i));

        private static readonly Relation InverterThree = ThreePhase("pv.inverter.current3", "Iac", "Pac", "Uac", "pf");
        private static readonly Relation InverterSingle = SinglePhase("pv.inverter.current1", "Iac", "Pac", "Uac", "pf");
        private static readonly Relation BoardThree = ThreePhase("energy.board.current", "I", "P", "U", "pf");
        private static readonly Relation BoardDc = Dc("energy.board.current.dc", "I", "P", "U");
        private static readonly Relation ChargerThree = ThreePhase("energy.charger.current", "I", "P", "U", "pf");
        private static readonly Relation ChargerDc = Dc("energy.charger.current.dc", "I", "P", "U");

        /// <summary>Battery: E = U·Q [S35].</summary>
        private static readonly Relation BatEnergy = Product("bat.energy", T("Nominal energy", "Nennenergie"), "E", "U", "Q", "S35");
        private static readonly Relation BatUsable = Product("bat.usable", T("Usable energy", "Nutzbare Energie"), "Eus", "E", "dod", "S35");
        private static readonly Relation BatPower = Product("bat.power", T("Power", "Leistung"), "P", "U", "I", "S35");

        /// <summary>C-rate: I = C·Q/1 h [S35].</summary>
        private static readonly Relation CRate =
            new Relation("bat.crate", T("C-rate", "C-Rate"), "{I} = {C}·{Q}/1 h", "I", "C", "Q") { Source = "S35" }
                .Solve("I", "{C}·{Q}/1 h", v => v("C") * v("Q") / 3600)
                .Solve("C", "{I}·1 h/{Q}", v => v("I") * 3600 / v("Q"))
                .Solve("Q", "{I}·1 h/{C}", v => v("I") * 3600 / v("C"));

        private static readonly Relation RunTime =
            new Relation("bat.time", T("Run time", "Laufzeit"), "{t} = {Eus}/{P}", "t", "Eus", "P") { Source = "S35", Validity = T("constant power, no losses", "konstante Leistung, ohne Verluste") }
                .Solve("t", "{Eus}/{P}", v => v("Eus") / v("P"))
                .Solve("Eus", "{t}·{P}", v => v("t") * v("P"))
                .Solve("P", "{Eus}/{t}", v => v("Eus") / v("t"));

        /// <summary>Heat pump Φ_H = COP·P_el [S32].</summary>
        private static readonly Relation Cop = Product("heat.cop", T("Coefficient of performance", "Leistungszahl"), "Phi", "cop", "Pel", "S32");

        /// <summary>Wind turbine P = ½·ρ·(π·D²/4)·v³·c_p [S36].</summary>
        private static readonly Relation WindPower =
            new Relation("wind.power", T("Power from the wind", "Leistung aus dem Wind"), "{P} = ½·{rho}·π·{D}²/4·{v}³·{cp}", "P", "rho", "D", "v", "cp") { Source = "S36" }
                .Solve("P", "½·{rho}·π·{D}²/4·{v}³·{cp}", v => 0.5 * v("rho") * Math.PI * v("D") * v("D") / 4 * Math.Pow(v("v"), 3) * v("cp"))
                .Solve("cp", "{P}/(½·{rho}·π·{D}²/4·{v}³)", v => v("P") / (0.5 * v("rho") * Math.PI * v("D") * v("D") / 4 * Math.Pow(v("v"), 3)))
                .Solve("v", "∛({P}/(½·{rho}·π·{D}²/4·{cp}))", v => Math.Pow(v("P") / (0.5 * v("rho") * Math.PI * v("D") * v("D") / 4 * v("cp")), 1.0 / 3))
                .Solve("D", "√({P}/(½·{rho}·π/4·{v}³·{cp}))", v => Math.Sqrt(v("P") / (0.5 * v("rho") * Math.PI / 4 * Math.Pow(v("v"), 3) * v("cp"))));

        /// <summary>EV charger: charging time t = E/P [S35].</summary>
        private static readonly Relation ChargeTime =
            new Relation("energy.charger.time", T("Charging time", "Ladedauer"), "{t} = {E}/{P}", "t", "E", "P") { Source = "S35", Validity = T("constant power, no losses", "konstante Leistung, ohne Verluste") }
                .Solve("t", "{E}/{P}", v => v("E") / v("P"))
                .Solve("E", "{t}·{P}", v => v("t") * v("P"))
                .Solve("P", "{E}/{t}", v => v("E") / v("t"));

        // Water as heat carrier (the same defaults as building services, so the two meet without a conflict).
        private const double WaterRho = 1000;
        private const double WaterC = 4190;
        /// <summary>Standard acceleration of free fall [S1].</summary>
        private const double G0 = 9.80665;
        /// <summary>Lower heating value of hydrogen, 119.96 MJ/kg ≈ 33.3 kWh/kg [S29].</summary>
        private const double H2Lhv = 119.96e6;

        /// <summary>Heat carried by water Φ = ρ·c·V̇·(θ_F − θ_R) [S29], the same relation as building services' heat generators.</summary>
        private static readonly Relation Carrier =
            new Relation("energy.heat.carrier", T("Heat carried by water", "Vom Wasser transportierte Wärme"), "{Phi} = {rho}·{c}·{Vdot}·({thf} − {thr})", "Phi", "rho", "c", "Vdot", "thf", "thr") { Source = "S29" }
                .Solve("Phi", "{rho}·{c}·{Vdot}·({thf} − {thr})", v => v("rho") * v("c") * v("Vdot") * (v("thf") - v("thr")))
                .Solve("Vdot", "{Phi}/({rho}·{c}·({thf} − {thr}))", v => v("Phi") / (v("rho") * v("c") * (v("thf") - v("thr"))))
                .Solve("thf", "{thr} + {Phi}/({rho}·{c}·{Vdot})", v => v("thr") + v("Phi") / (v("rho") * v("c") * v("Vdot")))
                .Solve("thr", "{thf} − {Phi}/({rho}·{c}·{Vdot})", v => v("thf") - v("Phi") / (v("rho") * v("c") * v("Vdot")))
                .Solve("rho", "{Phi}/({c}·{Vdot}·({thf} − {thr}))", v => v("Phi") / (v("c") * v("Vdot") * (v("thf") - v("thr"))))
                .Solve("c", "{Phi}/({rho}·{Vdot}·({thf} − {thr}))", v => v("Phi") / (v("rho") * v("Vdot") * (v("thf") - v("thr"))));

        /// <summary>Heat stored in a water tank Q = ρ·V·c·Δθ [S29].</summary>
        private static readonly Relation Stored =
            new Relation("energy.storage.heat", T("Stored heat", "Gespeicherte Wärme"), "{Q} = {rho}·{V}·{c}·{dT}", "Q", "rho", "V", "c", "dT") { Source = "S29" }
                .Solve("Q", "{rho}·{V}·{c}·{dT}", v => v("rho") * v("V") * v("c") * v("dT"))
                .Solve("V", "{Q}/({rho}·{c}·{dT})", v => v("Q") / (v("rho") * v("c") * v("dT")))
                .Solve("dT", "{Q}/({rho}·{V}·{c})", v => v("Q") / (v("rho") * v("V") * v("c")))
                .Solve("rho", "{Q}/({V}·{c}·{dT})", v => v("Q") / (v("V") * v("c") * v("dT")))
                .Solve("c", "{Q}/({rho}·{V}·{dT})", v => v("Q") / (v("rho") * v("V") * v("dT")));

        /// <summary>Hydropower P = ρ·g·Q·H·η (η overall: turbine, generator) [S27].</summary>
        private static readonly Relation HydroPower =
            new Relation("energy.hydro.power", T("Power from the water", "Leistung aus dem Wasser"), "{P} = {rho}·g·{Q}·{H}·{eta}", "P", "rho", "Q", "H", "eta")
            {
                Source = "S27", Validity = T("η overall (turbine and generator), H net head", "η gesamt (Turbine und Generator), H Nettofallhöhe"),
            }
                .Solve("P", "{rho}·g·{Q}·{H}·{eta}", v => v("rho") * G0 * v("Q") * v("H") * v("eta"))
                .Solve("Q", "{P}/({rho}·g·{H}·{eta})", v => v("P") / (v("rho") * G0 * v("H") * v("eta")))
                .Solve("H", "{P}/({rho}·g·{Q}·{eta})", v => v("P") / (v("rho") * G0 * v("Q") * v("eta")))
                .Solve("eta", "{P}/({rho}·g·{Q}·{H})", v => v("P") / (v("rho") * G0 * v("Q") * v("H")))
                .Solve("rho", "{P}/(g·{Q}·{H}·{eta})", v => v("P") / (G0 * v("Q") * v("H") * v("eta")));

        /// <summary>Pumped storage: potential energy of the upper reservoir E = ρ·g·V·H [S27].</summary>
        private static readonly Relation Potential =
            new Relation("energy.pumped.potential", T("Stored potential energy", "Gespeicherte potenzielle Energie"), "{E} = {rho}·g·{V}·{H}", "E", "rho", "V", "H")
            {
                Source = "S27", Validity = T("usable volume V, mean head H", "nutzbares Volumen V, mittlere Fallhöhe H"),
            }
                .Solve("E", "{rho}·g·{V}·{H}", v => v("rho") * G0 * v("V") * v("H"))
                .Solve("V", "{E}/({rho}·g·{H})", v => v("E") / (v("rho") * G0 * v("H")))
                .Solve("H", "{E}/({rho}·g·{V})", v => v("E") / (v("rho") * G0 * v("V")))
                .Solve("rho", "{E}/(g·{V}·{H})", v => v("E") / (G0 * v("V") * v("H")));

        private static readonly Relation PumpedElectric = Product("energy.pumped.electric", T("Electrical energy (generating)", "Elektrische Energie (Turbinenbetrieb)"), "Eel", "eta", "E", "S38");

        private static readonly Relation GeneratingTime =
            new Relation("energy.pumped.time", T("Generating time at rated power", "Turbinendauer bei Nennleistung"), "{t} = {Eel}/{P}", "t", "Eel", "P") { Source = "S1", Validity = T("constant power", "konstante Leistung") }
                .Solve("t", "{Eel}/{P}", v => v("Eel") / v("P"))
                .Solve("Eel", "{t}·{P}", v => v("t") * v("P"))
                .Solve("P", "{Eel}/{t}", v => v("Eel") / v("t"));

        /// <summary>Hydrogen power from the mass flow P_H2 = ṁ·H_i [S29].</summary>
        private static readonly Relation H2Flow =
            new Relation("energy.h2.flow", T("Hydrogen power from the mass flow", "Wasserstoffleistung aus dem Massenstrom"), "{PH2} = {mH2}·H_i", "PH2", "mH2")
            {
                Source = "S29", Validity = T("lower heating value H_i = 33.3 kWh/kg (120 MJ/kg)", "Heizwert H_i = 33,3 kWh/kg (120 MJ/kg)"),
            }
                .Solve("PH2", "{mH2}·H_i", v => v("mH2") * H2Lhv)
                .Solve("mH2", "{PH2}/H_i", v => v("PH2") / H2Lhv);

        private static readonly Relation ElyEff = Product("energy.electrolyser.efficiency", T("Electrolyser efficiency", "Wirkungsgrad des Elektrolyseurs"), "PH2", "eta", "Pel", "S38");
        private static readonly Relation FcEff = Product("energy.fuelcell.efficiency", T("Electrical efficiency", "Elektrischer Wirkungsgrad"), "Pel", "eta", "PH2", "S38");
        private static readonly Relation FcHeat = Product("energy.fuelcell.heat", T("Thermal efficiency", "Thermischer Wirkungsgrad"), "Phi", "etath", "PH2", "S38");
        private static readonly Relation ChpEl = Product("energy.chp.electric", T("Electrical efficiency", "Elektrischer Wirkungsgrad"), "Pel", "etael", "Pf", "S38");
        private static readonly Relation ChpTh = Product("energy.chp.thermal", T("Thermal efficiency", "Thermischer Wirkungsgrad"), "Phi", "etath", "Pf", "S38");

        /// <summary>Transformer side current I = S/(√3·U) [S7].</summary>
        private static Relation SideCurrent(string id, I18nText name, string i, string u) =>
            new Relation(id, name, "{" + i + "} = {S}/(√3·{" + u + "})", i, "S", u) { Source = "S7" }
                .Solve(i, "{S}/(√3·{" + u + "})", v => v("S") / (Sqrt3 * v(u)))
                .Solve("S", "√3·{" + u + "}·{" + i + "}", v => Sqrt3 * v(u) * v(i))
                .Solve(u, "{S}/(√3·{" + i + "})", v => v("S") / (Sqrt3 * v(i)));

        // ------------------------------------------------------------------ specs per type

        private static readonly Dictionary<string, Spec> Specs = new(StringComparer.Ordinal);

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
            "energy.pv.module" => Module(),
            "energy.pv.string" => PvString(),
            "energy.pv.array" => PvArray(),
            "energy.conv.inverter" => Inverter(),
            En.BusbarTypeId => Board(true),
            "energy.grid.meter" => Board(false),
            "energy.storage.battery" => Battery(),
            "energy.mobility.charger" => Charger(),
            "energy.heat.heatpump" => HeatPump(),
            "energy.heat.chp" => Chp(),
            "energy.heat.storage" => HeatStorage(),
            "energy.h2.electrolyser" => Electrolyser(),
            "energy.h2.fuelcell" => FuelCell(),
            "energy.wind.turbine" => Wind(),
            "energy.hydro.turbine" => Hydro(),
            "energy.storage.pumped" => Pumped(),
            "energy.grid.transformer" => Transformer(),
            _ => Spec.Empty,
        };

        /// <summary>DC port of a PV source: voltages shared, current and power parts of the inverter's sum.</summary>
        private static IEnumerable<PortVar> PvPort(string port, string power, bool withUstr = true)
            => port == "dc"
                ? new[]
                {
                    new PortVar("UocMax", "UocMax"), new PortVar("UmppMin", "UmppMin"), new PortVar("Isc", "Isc", PortRole.Part), new PortVar("Pdc", power, PortRole.Part),
                }.Concat(withUstr ? new[] { new PortVar("Ustr", "Ustr") } : Array.Empty<PortVar>())
                : Array.Empty<PortVar>();

        private static Spec Module()
        {
            var vars = new[]
            {
                UocMax(MaxName), Watt("Pm", T("Module power (STC)", "Modulleistung (STC)"), T("P_mpp", "P_mpp"), "power", VarRelevance.Card, "Wp") with { StoredUnit = "Wp" },
                Uoc(VarRelevance.Card), Umpp(VarRelevance.Card), Isc(), Impp(), Beta(), ThMin(), ThMax(), UmppMin(MinName),
            };
            var rels = new[]
            {
                ModuleMpp,
                Corrected("pv.voc.temperature", MaxName, "UocMax", "Uoc", "thmin", false),
                Corrected("pv.vmp.temperature", MinName, "UmppMin", "Umpp", "thmax", false, MinValidity),
            };
            return new Spec(vars, rels, Params(vars, "power"), Array.Empty<ValueCheck>())
            {
                Ports = (_, p) => p == "dc"
                    ? new[] { new PortVar("UocMax", "UocMax"), new PortVar("UmppMin", "UmppMin"), new PortVar("Isc", "Isc", PortRole.Part), new PortVar("Pdc", "Pm", PortRole.Part), new PortVar("Ustr", "Umpp") }
                    : Array.Empty<PortVar>(),
            };
        }

        private static Spec PvString()
        {
            var vars = new[]
            {
                UocMax(T("String open-circuit voltage at the lowest temperature", "Leerlaufspannung des Strings bei tiefster Temperatur")),
                Volt("Ustr", T("String MPP voltage (STC)", "MPP-Spannung des Strings (STC)"), T("U_mpp,S", "U_mpp,S"), "vmpString", VarRelevance.Card),
                Isc(VarRelevance.Card),
                Watt("P", T("String power (STC)", "Stringleistung (STC)"), T("P", "P"), "powerString", VarRelevance.Card, "Wp"),
                V("Ns", T("Modules in the string", "Module im String"), T("N_s", "N_s"), Quantities.Count, null, "modules", VarRelevance.Panel) with { Min = 1, Max = 1000 },
                Watt("Pm", T("Module power (STC)", "Modulleistung (STC)"), T("P_mpp", "P_mpp"), "power", VarRelevance.Panel, "Wp") with { StoredUnit = "Wp" },
                Uoc(), Umpp(), Impp(), Beta(), ThMin(), ThMax(),
                UmppMin(T("String MPP voltage at the highest temperature (β of U_oc)", "MPP-Spannung des Strings bei höchster Temperatur (β von U_oc)")),
            };
            var rels = new[]
            {
                StringPower, StringVoltage, ModuleMpp,
                Corrected("pv.string.voc", MaxName, "UocMax", "Uoc", "thmin", true),
                Corrected("pv.string.vmp", MinName, "UmppMin", "Umpp", "thmax", true, MinValidity),
            };
            return new Spec(vars, rels, Params(vars, "modules", "power"), Array.Empty<ValueCheck>()) { Ports = (_, p) => PvPort(p, "P") };
        }

        private static Spec PvArray()
        {
            var vars = new[]
            {
                Watt("P", T("Peak power (STC)", "Spitzenleistung (STC)"), T("P", "P"), "power", VarRelevance.Card, "kWp") with { StoredUnit = "kWp" },
                V("Np", T("Strings in parallel", "Parallele Strings"), T("N_p", "N_p"), Quantities.Count, null, "strings") with { Min = 1, Max = 1e5 },
                Isc(VarRelevance.Card) with { Name = T("Array short-circuit current (STC)", "Kurzschlussstrom des Generators (STC)"), Param = "iscArray" },
                UocMax(MaxName),
                Watt("Pstr", T("String power (STC)", "Stringleistung (STC)"), T("P_S", "P_S"), "powerString", VarRelevance.Panel, "Wp"),
                Amp("Iscs", T("String short-circuit current (STC)", "Kurzschlussstrom je String (STC)"), T("I_sc,S", "I_sc,S"), "isc", VarRelevance.Panel),
                UmppMin(MinName),
            };
            return new Spec(vars, new[] { ArrayPower, ArrayCurrent }, Params(vars, "power"), Array.Empty<ValueCheck>()) { Ports = (_, p) => PvPort(p, "P", false) };
        }

        private static Spec Inverter()
        {
            var vars = new[]
            {
                Watt("Pdc", T("DC power (STC, connected)", "DC-Leistung (STC, angeschlossen)"), T("P_DC", "P_DC"), "powerDc", VarRelevance.Card),
                Watt("Pac", T("AC power", "AC-Leistung"), T("P_AC", "P_AC"), "powerAc", VarRelevance.Card),
                Amp("Iac", T("AC current per phase", "AC-Strom je Außenleiter"), T("I_AC", "I_AC"), "currentAc", VarRelevance.Card),
                UocMax(T("Highest DC input voltage (strings at θ_min)", "Höchste DC-Eingangsspannung (Strings bei θ_min)")),
                V("eta", T("Efficiency", "Wirkungsgrad"), T("η", "η"), Quantities.Efficiency, "%", "efficiency", VarRelevance.Panel) with { Default = 0.97, Min = 0, Max = 1 },
                V("R", T("DC/AC ratio", "DC/AC-Verhältnis"), T("R", "R"), Quantities.Ratio, null, "dcAcRatio", VarRelevance.Panel) with { Min = 0, Max = 100 },
                Watt("PacN", T("Rated AC power", "AC-Nennleistung"), T("P_AC,N", "P_AC,N"), "power", VarRelevance.Panel, "kW") with { StoredUnit = "kW" },
                Volt("Uac", T("AC system voltage (line-to-line)", "AC-Netzspannung (Außenleiter)"), T("U_AC", "U_AC"), "voltageAc", VarRelevance.Panel) with { Default = 400 },
                V("pf", T("Power factor", "Leistungsfaktor"), T("cos φ", "cos φ"), Quantities.Ratio, null, "powerFactor", VarRelevance.Panel) with { Default = 1, Min = 0, Max = 1 },
                UmppMin(T("Lowest MPP voltage (strings at θ_max)", "Niedrigste MPP-Spannung (Strings bei θ_max)")),
                Amp("Isc", T("DC short-circuit current (connected)", "DC-Kurzschlussstrom (angeschlossen)"), T("I_sc", "I_sc"), "iscDc", VarRelevance.Panel),
                Volt("Ustr", T("DC MPP voltage (STC)", "DC-MPP-Spannung (STC)"), T("U_mpp", "U_mpp"), "vmpDc", VarRelevance.Panel),
                Volt("UdcMax", T("Maximum DC input voltage (datasheet)", "Maximale DC-Eingangsspannung (Datenblatt)"), T("U_DC,max", "U_DC,max"), "udcMax", VarRelevance.Panel),
                Volt("UmpptMin", T("MPP range, minimum (datasheet)", "MPP-Bereich, Minimum (Datenblatt)"), T("U_MPPT,min", "U_MPPT,min"), "mpptMin", VarRelevance.Panel),
                Volt("UmpptMax", T("MPP range, maximum (datasheet)", "MPP-Bereich, Maximum (Datenblatt)"), T("U_MPPT,max", "U_MPPT,max"), "mpptMax", VarRelevance.Panel),
                Amp("IdcMax", T("Maximum DC short-circuit current (datasheet)", "Maximaler DC-Kurzschlussstrom (Datenblatt)"), T("I_DC,max", "I_DC,max"), "idcMax", VarRelevance.Panel),
            };
            return new Spec(vars, new[] { InverterEff, DcAc }, Params(vars, "power"), new[] { VocMax(), MppMin(), IscMax(), DcAcRange() })
            {
                Ports = (_, p) => p switch
                {
                    "dc" => new[]
                    {
                        new PortVar("UocMax", "UocMax"), new PortVar("UmppMin", "UmppMin"), new PortVar("Isc", "Isc", PortRole.Total), new PortVar("Pdc", "Pdc", PortRole.Total),
                        new PortVar("Ustr", "Ustr"),
                    },
                    "ac" => new[] { new PortVar("Uac", "Uac"), new PortVar("Pac", "Pac", PortRole.Part) },
                    _ => Array.Empty<PortVar>(),
                },
                Local = o => new[] { o.Str("phases", "3") == "1" ? InverterSingle : InverterThree },
            };
        }

        private static Spec Board(bool busbar)
        {
            var u = busbar
                ? Volt("U", T("Nominal voltage", "Nennspannung"), T("U", "U"), "voltage", VarRelevance.Card) with { Unit = "kV", StoredUnit = "kV" }
                : Volt("U", T("System voltage (line-to-line)", "Netzspannung (Außenleiter)"), T("U", "U"), "systemVoltage", VarRelevance.Card);
            var vars = new[]
            {
                Watt("P", T("Power of the connected feeders", "Leistung der angeschlossenen Einspeisungen"), T("P", "P"), "powerIn", VarRelevance.Card),
                Amp("I", T("Current per phase", "Strom je Außenleiter"), T("I", "I"), "current", VarRelevance.Card),
                u,
                V("pf", T("Power factor", "Leistungsfaktor"), T("cos φ", "cos φ"), Quantities.Ratio, null, "powerFactor", VarRelevance.Panel) with { Default = 1, Min = 0, Max = 1 },
            };
            return new Spec(vars, Array.Empty<Relation>(), Params(vars, busbar ? new[] { "voltage" } : Array.Empty<string>()), Array.Empty<ValueCheck>())
            {
                Ports = busbar
                    ? (_, _) => new[] { new PortVar("Uac", "U"), new PortVar("Pac", "P", PortRole.Total) }
                    : (_, p) => p switch
                    {
                        "b" => new[] { new PortVar("Uac", "U"), new PortVar("Pac", "P", PortRole.Total) },
                        "a" => new[] { new PortVar("Uac", "U"), new PortVar("Pac", "P", PortRole.Part) },
                        _ => Array.Empty<PortVar>(),
                    },
                Local = o => new[] { busbar && o.Flag("dc", false) ? BoardDc : BoardThree },
            };
        }

        internal static IEnumerable<string[]> BusbarTaps()
        {
            yield return Enumerable.Range(1, 12).Select(i => "t" + i.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append("*").ToArray();
        }

        private static Spec Battery()
        {
            var vars = new[]
            {
                V("Eus", T("Usable capacity", "Nutzbare Kapazität"), T("E_nutz", "E_nutz"), Quantities.Energy, "kWh", "capacity") with { StoredUnit = "kWh", Min = 0, Max = 1e15 },
                Watt("P", T("Charge / discharge power", "Lade- / Entladeleistung"), T("P", "P"), "powerBat", VarRelevance.Card),
                V("C", T("C-rate", "C-Rate"), T("C", "C"), Quantities.Ratio, null, "crate") with { Min = 0, Max = 100 },
                V("t", T("Run time", "Laufzeit"), T("t", "t"), Quantities.Time_, "h", "runtime") with { Min = 0, Max = 1e9 },
                Volt("U", T("Nominal voltage", "Nennspannung"), T("U", "U"), "voltage", VarRelevance.Panel),
                V("Q", T("Capacity (charge)", "Kapazität (Ladung)"), T("Q", "Q"), Quantities.Charge, "Ah", "ah", VarRelevance.Panel) with { Min = 0, Max = 1e9 },
                V("E", T("Nominal energy", "Nennenergie"), T("E", "E"), Quantities.Energy, "kWh", "energyNominal", VarRelevance.Panel) with { Min = 0, Max = 1e15 },
                V("dod", T("Depth of discharge", "Entladetiefe"), T("DoD", "DoD"), Quantities.Efficiency, "%", "dod", VarRelevance.Panel) with { Min = 0, Max = 1 },
                Amp("I", T("Current", "Strom"), T("I", "I"), "current", VarRelevance.Panel),
                V("Cmax", T("Maximum C-rate (datasheet)", "Maximale C-Rate (Datenblatt)"), T("C_max", "C_max"), Quantities.Ratio, null, "cmax", VarRelevance.Panel) with { Min = 0, Max = 100 },
            };
            return new Spec(vars, new[] { BatEnergy, BatUsable, BatPower, CRate, RunTime }, Params(vars, "capacity"), new[] { CRateCheck() });
        }

        private static Spec Charger()
        {
            var vars = new[]
            {
                Watt("P", T("Charging power", "Ladeleistung"), T("P", "P"), "power", VarRelevance.Card, "kW") with { StoredUnit = "kW" },
                Volt("U", T("Voltage (AC line-to-line or DC)", "Spannung (AC Außenleiter oder DC)"), T("U", "U"), "voltage", VarRelevance.Card) with { Default = 400 },
                Amp("I", T("Current (per phase)", "Strom (je Außenleiter)"), T("I", "I"), "current", VarRelevance.Card),
                V("pf", T("Power factor", "Leistungsfaktor"), T("cos φ", "cos φ"), Quantities.Ratio, null, "powerFactor", VarRelevance.Panel) with { Default = 1, Min = 0, Max = 1 },
                V("t", T("Charging time", "Ladedauer"), T("t", "t"), Quantities.Time_, "h", "chargeTime") with { Min = 0, Max = 1e7 },
                V("E", T("Energy to charge", "Zu ladende Energie"), T("E", "E"), Quantities.Energy, "kWh", "chargeEnergy", VarRelevance.Panel) with { Min = 0, Max = 1e12 },
            };
            return new Spec(vars, new[] { ChargeTime }, Params(vars, "power"), Array.Empty<ValueCheck>())
            {
                Local = o => new[] { o.Flag("dc", false) ? ChargerDc : ChargerThree },
            };
        }

        // ------------------------------------------------------------------ heat side (joins building heating)

        private static VarDef HeatFlowVar(string key, I18nText name, I18nText sym, string param, VarRelevance rel = VarRelevance.Card, string? stored = null)
            => V(key, name, sym, Quantities.HeatFlow, "kW", param, rel) with { StoredUnit = stored, Min = 0, Max = 1e10 };

        /// <summary>
        /// The water side of a heat port: flow and return temperature, volume flow and the water properties of the carrier
        /// relation (named as in building services: <c>thf</c>, <c>thr</c>, <c>Vdot</c>, <c>rho</c>, <c>c</c>).
        /// </summary>
        private static VarDef[] WaterSide() => new[]
        {
            Temp("thf", T("Flow temperature", "Vorlauftemperatur"), T("θ_F", "θ_VL"), "flowTemp", VarRelevance.Panel),
            Temp("thr", T("Return temperature", "Rücklauftemperatur"), T("θ_R", "θ_RL"), "returnTemp", VarRelevance.Panel),
            V("Vdot", T("Volume flow (heating water)", "Volumenstrom (Heizwasser)"), T("V̇", "V̇"), Quantities.VolumeFlow, "m³/h", "flowRate", VarRelevance.Panel) with { Min = 0, Max = 1e3 },
            V("rho", T("Density of water", "Dichte des Wassers"), T("ρ", "ρ"), Quantities.Density, "kg/m³", "density", VarRelevance.Engineer) with { Default = WaterRho, Min = 1, Max = 3e4 },
            V("c", T("Specific heat capacity of water", "Spezifische Wärmekapazität des Wassers"), T("c", "c"), Quantities.SpecificHeat, "kJ/(kg·K)", "specificHeat", VarRelevance.Engineer) with { Default = WaterC, Min = 1, Max = 1e5 },
        };

        /// <summary>A heat port gives the heating water's volume flow and its flow temperature (building services' generator ports name the same).</summary>
        private static IEnumerable<PortVar> HeatPort(string port)
            => port == "heat" ? new[] { new PortVar("Vdot", "Vdot"), new PortVar("theta", "thf") } : Array.Empty<PortVar>();

        private static IEnumerable<PortVar> H2Port(string port)
            => port == "h2" ? new[] { new PortVar("PH2", "PH2"), new PortVar("m", "mH2") } : Array.Empty<PortVar>();

        private static VarDef H2Power(VarRelevance rel = VarRelevance.Card)
            => Watt("PH2", T("Hydrogen power (lower heating value)", "Wasserstoffleistung (Heizwert)"), T("P_H2", "P_H2"), "powerH2", rel, "kW");

        private static VarDef H2MassFlow(VarRelevance rel)
            => V("mH2", T("Hydrogen mass flow", "Wasserstoff-Massenstrom"), T("ṁ_H2", "ṁ_H2"), Quantities.MassFlow, "kg/h", "hydrogenFlow", rel) with { Min = 0, Max = 1e6 };

        private static VarDef Eff(string key, I18nText name, I18nText sym, string param, VarRelevance rel = VarRelevance.Panel)
            => V(key, name, sym, Quantities.Efficiency, "%", param, rel) with { Min = 0, Max = 1 };

        private static Spec HeatPump()
        {
            // Variable keys as on building services' heat pump (Phi, Pel, cop, thf, thr, ths, Vdot, rho, c), so the two meet.
            var vars = new[]
            {
                HeatFlowVar("Phi", T("Heating power", "Heizleistung"), T("Φ_H", "Φ_H"), "power", stored: "kW"),
                Watt("Pel", T("Electrical power", "Elektrische Leistung"), T("P_el", "P_el"), "powerEl", VarRelevance.Card),
                V("cop", T("Coefficient of performance", "Leistungszahl"), T("COP", "COP"), Quantities.Ratio, null, "cop") with { Min = 0.1, Max = 100 },
                Temp("ths", T("Source temperature", "Quellentemperatur"), T("θ_Q", "θ_Q"), "sourceTemp", VarRelevance.Panel),
            }.Concat(WaterSide()).ToArray();
            return new Spec(vars, new[] { Cop, Carrier }, Params(vars, "power"), new[] { Carnot() }) { Ports = (_, p) => HeatPort(p) };
        }

        private static Spec Chp()
        {
            var vars = new[]
            {
                Watt("Pel", T("Electrical power", "Elektrische Leistung"), T("P_el", "P_el"), "power", VarRelevance.Card, "kW") with { StoredUnit = "kW" },
                HeatFlowVar("Phi", T("Thermal power", "Thermische Leistung"), T("Φ_th", "Φ_th"), "thermal", stored: "kW"),
                Watt("Pf", T("Fuel power (lower heating value)", "Brennstoffleistung (Heizwert)"), T("P_B", "P_B"), "powerFuel", VarRelevance.Card, "kW") with { Symbol = T("P_F", "P_B") },
                Eff("etael", T("Electrical efficiency", "Elektrischer Wirkungsgrad"), T("η_el", "η_el"), "efficiencyEl", VarRelevance.Card),
                Eff("etath", T("Thermal efficiency", "Thermischer Wirkungsgrad"), T("η_th", "η_th"), "efficiencyTh"),
            }.Concat(WaterSide()).ToArray();
            return new Spec(vars, new[] { ChpEl, ChpTh, Carrier }, Params(vars, "power", "thermal"), Array.Empty<ValueCheck>()) { Ports = (_, p) => HeatPort(p) };
        }

        private static Spec HeatStorage()
        {
            var vars = new[]
            {
                V("Q", T("Stored heat", "Gespeicherte Wärme"), T("Q", "Q"), Quantities.Energy, "kWh", "heat") with { Min = 0, Max = 1e15 },
                V("V", T("Storage volume", "Speichervolumen"), T("V", "V"), Quantities.Volume, "l", "volume") with { StoredUnit = "l", Min = 1e-4, Max = 1e6 },
                V("dT", T("Temperature spread", "Temperaturspreizung"), T("Δθ", "Δθ"), Quantities.TemperatureDifference, "K", "deltaT") with { Min = 0, Max = 200 },
                Temp("th", T("Storage temperature (top)", "Speichertemperatur (oben)"), T("θ_S", "θ_Sp"), "temperature", VarRelevance.Card),
                V("rho", T("Density of water", "Dichte des Wassers"), T("ρ", "ρ"), Quantities.Density, "kg/m³", "density", VarRelevance.Engineer) with { Default = WaterRho, Min = 1, Max = 3e4 },
                V("c", T("Specific heat capacity of water", "Spezifische Wärmekapazität des Wassers"), T("c", "c"), Quantities.SpecificHeat, "kJ/(kg·K)", "specificHeat", VarRelevance.Engineer) with { Default = WaterC, Min = 1, Max = 1e5 },
            };
            // Charged at the top with the generator's flow temperature and discharged at the top again (stratified tank).
            return new Spec(vars, new[] { Stored }, Params(vars, "volume"), Array.Empty<ValueCheck>())
            {
                Ports = (_, p) => p is "charge" or "discharge" ? new[] { new PortVar("theta", "th") } : Array.Empty<PortVar>(),
            };
        }

        private static Spec Electrolyser()
        {
            var vars = new[]
            {
                Watt("Pel", T("Electrical power", "Elektrische Leistung"), T("P_el", "P_el"), "power", VarRelevance.Card, "kW") with { StoredUnit = "kW" },
                H2Power(),
                Eff("eta", T("Efficiency (lower heating value)", "Wirkungsgrad (Heizwert)"), T("η", "η"), "efficiency", VarRelevance.Card),
                H2MassFlow(VarRelevance.Card),
            };
            return new Spec(vars, new[] { ElyEff, H2Flow }, Params(vars, "power"), Array.Empty<ValueCheck>()) { Ports = (_, p) => H2Port(p) };
        }

        private static Spec FuelCell()
        {
            var vars = new[]
            {
                Watt("Pel", T("Electrical power", "Elektrische Leistung"), T("P_el", "P_el"), "power", VarRelevance.Card, "kW") with { StoredUnit = "kW" },
                H2Power(),
                Eff("eta", T("Electrical efficiency (lower heating value)", "Elektrischer Wirkungsgrad (Heizwert)"), T("η_el", "η_el"), "efficiency", VarRelevance.Card),
                HeatFlowVar("Phi", T("Heat output", "Wärmeleistung"), T("Φ_th", "Φ_th"), "heatOutput"),
                Eff("etath", T("Thermal efficiency", "Thermischer Wirkungsgrad"), T("η_th", "η_th"), "efficiencyTh"),
                H2MassFlow(VarRelevance.Panel),
            }.Concat(WaterSide()).ToArray();
            return new Spec(vars, new[] { FcEff, FcHeat, H2Flow, Carrier }, Params(vars, "power"), Array.Empty<ValueCheck>())
            {
                Ports = (_, p) => p == "h2" ? H2Port(p) : HeatPort(p),
            };
        }

        // ------------------------------------------------------------------ hydropower

        private static VarDef HeadVar() => V("H", T("Head", "Fallhöhe"), T("H", "H"), Quantities.Length, "m", "head") with { Min = 0.1, Max = 3000 };
        private static VarDef WaterFlow(VarRelevance rel) => V("Q", T("Water flow", "Durchfluss"), T("Q", "Q"), Quantities.VolumeFlow, "m³/s", "flow", rel) with { Min = 0, Max = 1e5 };
        private static VarDef WaterDensity() => V("rho", T("Density of water", "Dichte des Wassers"), T("ρ", "ρ"), Quantities.Density, "kg/m³", "waterDensity", VarRelevance.Engineer) with { Default = WaterRho, Min = 1, Max = 3e4 };

        private static Spec Hydro()
        {
            var vars = new[]
            {
                Watt("P", T("Power from the water", "Leistung aus dem Wasser"), T("P", "P"), "powerHydro", VarRelevance.Card, "MW"),
                WaterFlow(VarRelevance.Card), HeadVar(),
                Eff("eta", T("Overall efficiency", "Gesamtwirkungsgrad"), T("η", "η"), "efficiency", VarRelevance.Card),
                WaterDensity(),
            };
            // The water ports give the flow to lines of other modules docked on them (V̇ joins by physical key).
            return new Spec(vars, new[] { HydroPower }, Params(vars), Array.Empty<ValueCheck>())
            {
                Ports = (_, p) => p is "water" or "tail" ? new[] { new PortVar("Vdot", "Q") } : Array.Empty<PortVar>(),
            };
        }

        private static Spec Pumped()
        {
            var vars = new[]
            {
                V("Eel", T("Storage capacity (electrical)", "Speicherkapazität (elektrisch)"), T("E_el", "E_el"), Quantities.Energy, "MWh", "capacity") with { StoredUnit = "MWh", Min = 0, Max = 1e18 },
                Watt("P", T("Turbine power", "Turbinenleistung"), T("P", "P"), "power", VarRelevance.Card, "MW") with { StoredUnit = "MW" },
                V("V", T("Usable volume of the upper reservoir", "Nutzinhalt des Oberbeckens"), T("V", "V"), Quantities.Volume, "m³", "reservoirVolume") with { Min = 1, Max = 1e11 },
                HeadVar(),
                V("E", T("Stored potential energy", "Gespeicherte potenzielle Energie"), T("E_pot", "E_pot"), Quantities.Energy, "MWh", "energyPotential", VarRelevance.Panel) with { Min = 0, Max = 1e18 },
                Eff("eta", T("Efficiency, generating (overall)", "Wirkungsgrad, Turbinenbetrieb (gesamt)"), T("η_T", "η_T"), "efficiency"),
                WaterFlow(VarRelevance.Panel),
                V("t", T("Generating time at rated power", "Turbinendauer bei Nennleistung"), T("t", "t"), Quantities.Time_, "h", "generatingTime", VarRelevance.Panel) with { Min = 0, Max = 1e7 },
                WaterDensity(),
            };
            return new Spec(vars, new[] { Potential, PumpedElectric, HydroPower, GeneratingTime }, Params(vars, "power", "capacity"), Array.Empty<ValueCheck>());
        }

        private static Spec Wind()
        {
            var vars = new[]
            {
                Watt("P", T("Power from the wind", "Leistung aus dem Wind"), T("P", "P"), "powerWind", VarRelevance.Card),
                V("D", T("Rotor diameter", "Rotordurchmesser"), T("D", "D"), Quantities.Length, "m", "diameter") with { Min = 0.1, Max = 1000 },
                V("v", T("Wind speed", "Windgeschwindigkeit"), T("v", "v"), Quantities.Speed, "m/s", "windSpeed") with { Min = 0, Max = 100 },
                V("cp", T("Power coefficient", "Leistungsbeiwert"), T("c_p", "c_p"), Quantities.Ratio, null, "cp") with { Min = 0, Max = 1 },
                V("rho", T("Air density", "Luftdichte"), T("ρ", "ρ"), Quantities.Density, "kg/m³", "airDensity", VarRelevance.Engineer) with { Default = 1.225, Min = 0.1, Max = 10 },
            };
            return new Spec(vars, new[] { WindPower }, Params(vars), new[] { Betz() });
        }

        private static Spec Transformer()
        {
            var vars = new[]
            {
                V("S", T("Rated power", "Bemessungsleistung"), T("S_r", "S_r"), Quantities.ApparentPower, "kVA", "power") with { StoredUnit = "kVA", Min = 0, Max = 1e10 },
                Amp("Ihv", T("Rated current, high-voltage side", "Bemessungsstrom, Oberspannungsseite"), T("I_OS", "I_OS"), "currentHv", VarRelevance.Card) with { Symbol = T("I_HV", "I_OS") },
                Amp("Ilv", T("Rated current, low-voltage side", "Bemessungsstrom, Unterspannungsseite"), T("I_US", "I_US"), "currentLv", VarRelevance.Card) with { Symbol = T("I_LV", "I_US") },
                Volt("Uhv", T("High-voltage side", "Oberspannung"), T("U_OS", "U_OS"), "hv", VarRelevance.Panel) with { Unit = "kV", StoredUnit = "kV", Symbol = T("U_HV", "U_OS") },
                Volt("Ulv", T("Low-voltage side", "Unterspannung"), T("U_US", "U_US"), "lv", VarRelevance.Panel) with { Unit = "kV", StoredUnit = "kV", Symbol = T("U_LV", "U_US") },
            };
            var rels = new[]
            {
                SideCurrent("energy.transformer.ihv", T("Current, high-voltage side", "Strom, Oberspannungsseite"), "Ihv", "Uhv"),
                SideCurrent("energy.transformer.ilv", T("Current, low-voltage side", "Strom, Unterspannungsseite"), "Ilv", "Ulv"),
            };
            return new Spec(vars, rels, Params(vars, "power", "hv", "lv"), Array.Empty<ValueCheck>());
        }

        // ------------------------------------------------------------------ net description

        private static IEnumerable<string[]> Through(SketchObject o, string cls) => o.Type == En.BusbarTypeId ? BusbarTaps() : Array.Empty<string[]>();

        /// <summary>
        /// Lines of the energy nets: cables, and the heat and hydrogen pipes (heat ports pass V̇ and θ, hydrogen ports P_H2
        /// and ṁ). Gas, cooling and water pipes carry no values.
        /// </summary>
        internal static bool IsNetLine(SketchObject o)
            => o.Type == CableType.TypeId || (o.Type == PipeType.TypeId && o.S(PipeType.CarrierParam) is "heat" or "hydrogen");

        internal static readonly NetSpec Nets = new()
        {
            Prefix = "energy",
            IsLine = IsNetLine,
            PortVars = (o, port) => For(o.Type).Ports(o, port),
            Through = Through,
            Classes = new[] { "UocMax", "UmppMin", "Ustr", "Isc", "Pdc", "Uac", "Pac", "Vdot", "theta", "PH2", "m" },
            Rule = c => c switch
            {
                "Isc" or "Pdc" or "Pac" => NetRule.Sum,
                "Vdot" or "theta" or "PH2" or "m" => NetRule.Flow,
                _ => NetRule.Equal,
            },
            // The physical keys for joins with other modules, stated explicitly (limits and the hydrogen power stay inside).
            Keys = c => c switch
            {
                "Uac" or "Ustr" => NetKeys.Voltage,
                "Pac" or "Pdc" => NetKeys.Power,
                "Vdot" => NetKeys.VolumeFlow,
                "theta" => NetKeys.Temperature,
                "m" => NetKeys.MassFlow,
                _ => null,
            },
            Name = c => c switch
            {
                "Isc" => T("Short-circuit currents of parallel strings", "Kurzschlussströme paralleler Strings"),
                "Pdc" => T("DC power of the connected strings", "DC-Leistung der angeschlossenen Strings"),
                "Pac" => T("AC power of the feeders", "AC-Leistung der Einspeisungen"),
                "Uac" => T("AC voltage passed on", "AC-Spannung weitergegeben"),
                "Vdot" => T("Volume flow of the heating water passed on", "Volumenstrom des Heizwassers weitergegeben"),
                "theta" => T("Flow temperature passed on", "Vorlauftemperatur weitergegeben"),
                "PH2" => T("Hydrogen power passed on", "Wasserstoffleistung weitergegeben"),
                "m" => T("Hydrogen mass flow passed on", "Wasserstoff-Massenstrom weitergegeben"),
                _ => T("String voltage passed on", "Stringspannung weitergegeben"),
            },
        };

        public static IEnumerable<BoundRelation> Links(SketchObject o, RenderContext ctx)
        {
            var spec = For(o.Type);
            if (spec.Local is not null)
                foreach (var r in spec.Local(o)) yield return BoundRelation.Local(r, o.Id);
            foreach (var b in ValueNets.Of(ctx.Doc, Nets).For(o.Id)) yield return b;
        }

        // ------------------------------------------------------------------ checks (section 4)

        private const double Rel = 1e-6;

        /// <summary>pv.voc.max: N_s·U_oc(θ_min) above the inverter's maximum DC voltage [S33] (IEC 62548-1 names the rule).</summary>
        private static ValueCheck VocMax() => new(
            "pv.voc.max", CheckSeverity.Warning,
            T("At the lowest temperature the string's open-circuit voltage exceeds the inverter's maximum DC input voltage. Use fewer modules per string.",
              "Bei tiefster Temperatur übersteigt die Leerlaufspannung des Strings die maximale DC-Eingangsspannung des Wechselrichters. Weniger Module je String vorsehen."),
            "N_s·U_oc(θ_min) > U_DC,max",
            c => c["UocMax"] is double u && c["UdcMax"] is double max && u > max * (1 + Rel))
        { Source = "S33" };

        /// <summary>pv.mpp.min: N_s·U_mpp(θ_max) below the MPPT minimum [S33, S34].</summary>
        private static ValueCheck MppMin() => new(
            "pv.mpp.min", CheckSeverity.Hint,
            T("At the highest temperature the string's MPP voltage falls below the inverter's MPP range. Use more modules per string.",
              "Bei höchster Temperatur fällt die MPP-Spannung des Strings unter den MPP-Bereich des Wechselrichters. Mehr Module je String vorsehen."),
            "N_s·U_mpp(θ_max) < U_MPPT,min",
            c => c["UmppMin"] is double u && c["UmpptMin"] is double min && u < min * (1 - Rel))
        { Source = "S33" };

        /// <summary>pv.isc.max: N_p·I_sc above the inverter's maximum DC current [S33].</summary>
        private static ValueCheck IscMax() => new(
            "pv.isc.max", CheckSeverity.Warning,
            T("The short-circuit current of the connected strings exceeds the inverter's maximum DC current. Use fewer strings in parallel.",
              "Der Kurzschlussstrom der angeschlossenen Strings übersteigt den maximalen DC-Strom des Wechselrichters. Weniger Strings parallel schalten."),
            "N_p·I_sc > I_DC,max",
            c => c["Isc"] is double i && c["IdcMax"] is double max && i > max * (1 + Rel))
        { Source = "S33" };

        /// <summary>pv.dcac: DC/AC ratio outside 0.8 … 1.5 (default user range) [S34].</summary>
        private static ValueCheck DcAcRange() => new(
            "pv.dcac", CheckSeverity.Info,
            T("The DC/AC ratio is outside the usual range of 0.8–1.5. Check the inverter size against the PV power.",
              "Das DC/AC-Verhältnis liegt außerhalb des üblichen Bereichs von 0,8–1,5. Wechselrichtergröße gegen die PV-Leistung prüfen."),
            "R ∉ [0.8, 1.5]",
            c => c["R"] is double r && (r < 0.8 * (1 - Rel) || r > 1.5 * (1 + Rel)))
        { Source = "S34" };

        /// <summary>bat.crate: C-rate above the datasheet maximum [S35].</summary>
        private static ValueCheck CRateCheck() => new(
            "bat.crate", CheckSeverity.Hint,
            T("The C-rate is above the battery's maximum. Reduce the power or choose a larger battery.",
              "Die C-Rate liegt über dem Maximum der Batterie. Leistung verringern oder eine größere Batterie wählen."),
            "C > C_max",
            c => c["C"] is double cr && c["Cmax"] is double max && cr > max * (1 + Rel))
        { Source = "S35" };

        /// <summary>heat.cop.carnot: COP above T_flow/(T_flow − T_source) (impossible) [S29].</summary>
        private static ValueCheck Carnot() => new(
            "heat.cop.carnot", CheckSeverity.Warning,
            T("The COP is above the Carnot limit T_flow/(T_flow − T_source): impossible. Check the COP and the temperatures.",
              "Die Leistungszahl liegt über der Carnot-Grenze T_VL/(T_VL − T_Q): unmöglich. Leistungszahl und Temperaturen prüfen."),
            "COP > T_F/(T_F − T_Q)",
            c => c["cop"] is double cop && c["thf"] is double tf && c["ths"] is double ts && tf > ts && cop > tf / (tf - ts) * (1 + Rel))
        { Source = "S29" };

        /// <summary>wind.betz: power coefficient above the Betz limit 16/27 (impossible) [S36].</summary>
        private static ValueCheck Betz() => new(
            "wind.betz", CheckSeverity.Warning,
            T("The power coefficient is above the Betz limit of 16/27 ≈ 0.593: impossible. Check c_p, the wind speed and the power.",
              "Der Leistungsbeiwert liegt über der Betz-Grenze von 16/27 ≈ 0,593: unmöglich. c_p, Windgeschwindigkeit und Leistung prüfen."),
            "c_p > 16/27",
            c => c["cp"] is double cp && cp > 16.0 / 27 * (1 + Rel))
        { Source = "S36" };
    }
}
