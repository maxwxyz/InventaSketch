using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.BuildingServices
{
    /// <summary>
    /// Element values of the building services elements (Calc 4, engineering-calculations.md sections 2.9 and 3.7): heat
    /// generators (boiler, heat pump with COP), radiators (EN 442 characteristic Φ = Φ₅₀·(Δθ/50 K)^n with the logarithmic excess
    /// temperature, and the operating return temperature from flow and characteristic), underfloor loop, circulator pump,
    /// mixing valve, water heater, buffer tank (stored heat), heat meter (power from flow and temperatures), water meter, air terminal (room area, height and air change give the air flow), ventilation unit (heat
    /// recovery), pipes (velocity, DN as approximation of the inner diameter) and ducts (round or rectangular). Every value is
    /// optional. Water: ρ = 1000 kg/m³ and c = 4.19 kJ/(kg·K) as defaults; air: ρ·c_p = 0.34 W·h/(m³·K).
    /// <para>
    /// Port flows (<see cref="ValueNets"/>): pipes and ducts join ports into nets. The volume flow (<c>Vdot</c>) is passed on an
    /// unbranched run and summed at a branch (the generator's port, the pump's outlet, the ventilation unit's spigot and the
    /// mixer outlet are the totals of the radiators, loops and terminals on the branches). The stream temperature (<c>theta</c>)
    /// is passed on an unbranched run and from the single supplying outlet to every inlet of a split. Thermostatic valves pass
    /// both; pumps pass the temperature. Heat losses of pipes are not modelled. Pipes of different media (flow, return,
    /// water kinds) and ducts of different air kinds stay separate nets where they meet only at a port of another module's
    /// element; the heating flow takes that port's values (an energy heat port).
    /// </para>
    /// <para>
    /// Room values by containment: an air terminal (A, h, V) or radiator (Φ_HL) standing inside an architecture room
    /// (<see cref="RoomTypeId"/>, same page) takes the room's values (<see cref="RoomLinks"/>).
    /// </para>
    /// </summary>
    internal static class BsValues
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
            /// <summary>Relations that depend on what is entered (round or rectangular duct).</summary>
            public Func<SketchObject, IEnumerable<Relation>>? Local { get; init; }
            /// <summary>
            /// Variables taken from the room the element stands in (<see cref="RoomLinks"/>): linked to the room's variable of
            /// the same key and quantity kind.
            /// </summary>
            public IReadOnlyList<string> RoomKeys { get; init; } = Array.Empty<string>();
        }

        // ------------------------------------------------------------------ parameters and variables

        private static ParamDef Text(string key, I18nText label, string? unit) => new(key, label, ParamKind.Text) { Unit = unit, Advanced = true };

        private static VarDef V(string key, I18nText name, I18nText symbol, QuantityKind kind, string? unit, string param, VarRelevance rel = VarRelevance.Card)
            => new(key, name, symbol, kind) { Unit = unit, Param = param, Relevance = rel };

        /// <summary>Text parameters for the variables without a parameter of their own (existing number parameters are reused).</summary>
        private static ParamDef[] Params(IEnumerable<VarDef> vars, params string[] existing)
            => vars.Where(v => !existing.Contains(v.ParamKey)).Select(v => Text(v.ParamKey, v.Name, v.Unit)).Append(ValueLabels.ShowValues).ToArray();

        private const double G = 9.80665;
        private const double WaterC = 4190;
        private const double WaterRho = 1000;
        /// <summary>Volumetric heat capacity of air ρ·c_p ≈ 0.34 W·h/(m³·K) [S30].</summary>
        private const double AirRhoC = 0.34 * 3600;
        private const double C0 = 273.15;

        private static VarDef Flow(string unit = "l/h", string key = "Vdot", string param = "flowRate", I18nText? name = null, VarRelevance rel = VarRelevance.Panel)
            => V(key, name ?? T("Volume flow", "Volumenstrom"), T("V̇", "V̇"), Quantities.VolumeFlow, unit, param, rel) with { Min = 0, Max = 100 };
        private static VarDef Temp(string key, I18nText name, I18nText sym, string param, VarRelevance rel, double? def = null)
            => V(key, name, sym, Quantities.Temperature, "°C", param, rel) with { Min = C0 - 60, Max = C0 + 200, Default = def };
        private static VarDef FlowTemp(VarRelevance rel = VarRelevance.Card) => Temp("thf", T("Flow temperature", "Vorlauftemperatur"), T("θ_F", "θ_VL"), "flowTemp", rel);
        private static VarDef ReturnTemp(VarRelevance rel = VarRelevance.Card) => Temp("thr", T("Return temperature", "Rücklauftemperatur"), T("θ_R", "θ_RL"), "returnTemp", rel);
        private static VarDef HeatOut(I18nText? name = null, VarRelevance rel = VarRelevance.Card)
            => V("Phi", name ?? T("Heat output", "Wärmeleistung"), T("Φ", "Φ"), Quantities.HeatFlow, "W", "heatOutput", rel) with { Min = 0, Max = 1e9 };
        private static VarDef Rho() => V("rho", T("Density of water", "Dichte des Wassers"), T("ρ", "ρ"), Quantities.Density, "kg/m³", "density", VarRelevance.Engineer) with { Default = WaterRho, Min = 1, Max = 3e4 };
        private static VarDef Cp() => V("c", T("Specific heat capacity of water", "Spezifische Wärmekapazität des Wassers"), T("c", "c"), Quantities.SpecificHeat, "kJ/(kg·K)", "specificHeat", VarRelevance.Engineer) with { Default = WaterC, Min = 1, Max = 1e5 };
        private static VarDef Velocity(VarRelevance rel = VarRelevance.Card) => V("v", T("Flow velocity", "Strömungsgeschwindigkeit"), T("v", "v"), Quantities.Speed, "m/s", "velocity", rel) with { Min = 0, Max = 100 };

        // ------------------------------------------------------------------ relations

        /// <summary>Φ = ρ·c·V̇·(θ_F − θ_R) [S29, S30].</summary>
        private static readonly Relation Carrier =
            new Relation("bs.heat.carrier", T("Heat carried by water", "Vom Wasser transportierte Wärme"), "{Phi} = {rho}·{c}·{Vdot}·({thf} − {thr})", "Phi", "rho", "c", "Vdot", "thf", "thr") { Source = "S29" }
                .Solve("Phi", "{rho}·{c}·{Vdot}·({thf} − {thr})", v => v("rho") * v("c") * v("Vdot") * (v("thf") - v("thr")))
                .Solve("Vdot", "{Phi}/({rho}·{c}·({thf} − {thr}))", v => v("Phi") / (v("rho") * v("c") * (v("thf") - v("thr"))))
                .Solve("thf", "{thr} + {Phi}/({rho}·{c}·{Vdot})", v => v("thr") + v("Phi") / (v("rho") * v("c") * v("Vdot")))
                .Solve("thr", "{thf} − {Phi}/({rho}·{c}·{Vdot})", v => v("thf") - v("Phi") / (v("rho") * v("c") * v("Vdot")))
                .Solve("rho", "{Phi}/({c}·{Vdot}·({thf} − {thr}))", v => v("Phi") / (v("c") * v("Vdot") * (v("thf") - v("thr"))))
                .Solve("c", "{Phi}/({rho}·{Vdot}·({thf} − {thr}))", v => v("Phi") / (v("rho") * v("Vdot") * (v("thf") - v("thr"))));

        /// <summary>Logarithmic excess temperature (θ_F − θ_R)/ln((θ_F − θ_i)/(θ_R − θ_i)); continuous at θ_F = θ_R, 0 for θ_R ≤ θ_i (EN 442-2 [S31]).</summary>
        internal static double ExcessLn(double thf, double thr, double thi)
        {
            double a = thf - thi, b = thr - thi;
            if (a <= 0 || b <= 0) return 0;
            if (Math.Abs(a - b) <= 1e-12 * a) return a;
            return (a - b) / Math.Log(a / b);
        }

        /// <summary>Δθ = logarithmic excess temperature over the room (EN 442-2 [S31]).</summary>
        private static readonly Relation Excess =
            new Relation("bs.radiator.excess", T("Logarithmic excess temperature", "Logarithmische Übertemperatur"), "{dT} = ({thf} − {thr})/ln(({thf} − {thi})/({thr} − {thi}))", "dT", "thf", "thr", "thi") { Source = "S31" }
                .Solve("dT", "({thf} − {thr})/ln(({thf} − {thi})/({thr} − {thi}))", v =>
                {
                    var d = ExcessLn(v("thf"), v("thr"), v("thi"));
                    return d > 0 ? d : double.NaN;
                });

        /// <summary>Radiator characteristic Φ = Φ₅₀·(Δθ/50 K)^n (EN 442-2 [S31]).</summary>
        private static readonly Relation Characteristic =
            new Relation("bs.radiator.characteristic", T("Radiator characteristic", "Heizkörperkennlinie"), "{Phi} = {phi50}·({dT}/50 K)^{n}", "Phi", "phi50", "dT", "n") { Source = "S31" }
                .Solve("Phi", "{phi50}·({dT}/50 K)^{n}", v => v("phi50") * Math.Pow(v("dT") / 50, v("n")))
                .Solve("phi50", "{Phi}/({dT}/50 K)^{n}", v => v("Phi") / Math.Pow(v("dT") / 50, v("n")))
                .Solve("dT", "50 K·({Phi}/{phi50})^(1/{n})", v => 50 * Math.Pow(v("Phi") / v("phi50"), 1 / v("n")))
                .Solve("n", "ln({Phi}/{phi50})/ln({dT}/50 K)", v => Math.Log(v("Phi") / v("phi50")) / Math.Log(v("dT") / 50));

        /// <summary>
        /// Operating point: the water gives off what the radiator emits, ρ·c·V̇·(θ_F − θ_R) = Φ₅₀·(Δθ_ln/50 K)^n, solved numerically
        /// (this gives the return temperature and the output from flow, flow temperature, room temperature and Φ₅₀/n) [S31].
        /// </summary>
        private static readonly Relation Operating =
            new Relation("bs.radiator.operating", T("Radiator operating point", "Betriebspunkt des Heizkörpers"),
                "{rho}·{c}·{Vdot}·({thf} − {thr}) = {phi50}·(Δθ_ln({thf}, {thr}, {thi})/50 K)^{n}", "thr", "Vdot", "thf", "thi", "phi50", "n", "rho", "c") { Source = "S31" }
                .WithResidual(v => v("rho") * v("c") * v("Vdot") * (v("thf") - v("thr")) - v("phi50") * Math.Pow(ExcessLn(v("thf"), v("thr"), v("thi")) / 50, v("n")));

        /// <summary>Δp = ρ·g·H [S28].</summary>
        private static readonly Relation Head =
            new Relation("bs.pump.head", T("Head", "Förderhöhe"), "{Phyd} = {rho}·g·{H}·{Vdot}", "Phyd", "rho", "H", "Vdot") { Source = "S28" }
                .Solve("Phyd", "{rho}·g·{H}·{Vdot}", v => v("rho") * G * v("H") * v("Vdot"))
                .Solve("H", "{Phyd}/({rho}·g·{Vdot})", v => v("Phyd") / (v("rho") * G * v("Vdot")))
                .Solve("Vdot", "{Phyd}/({rho}·g·{H})", v => v("Phyd") / (v("rho") * G * v("H")));

        private static readonly Relation PumpPower =
            new Relation("bs.pump.power", T("Electrical power", "Elektrische Leistung"), "{Pel} = {Phyd}/{eta}", "Pel", "Phyd", "eta") { Source = "S28" }
                .Solve("Pel", "{Phyd}/{eta}", v => v("Phyd") / v("eta"))
                .Solve("Phyd", "{Pel}·{eta}", v => v("Pel") * v("eta"))
                .Solve("eta", "{Phyd}/{Pel}", v => v("Phyd") / v("Pel"));

        /// <summary>Mixing: V̇_AB = V̇_A + V̇_B [S1].</summary>
        private static readonly Relation MixFlow =
            new Relation("bs.mixer.flow", T("Flow balance", "Volumenstrombilanz"), "{Vab} = {Va} + {Vb}", "Vab", "Va", "Vb") { Source = "S1" }
                .Solve("Vab", "{Va} + {Vb}", v => v("Va") + v("Vb"))
                .Solve("Va", "{Vab} − {Vb}", v => v("Vab") - v("Vb"))
                .Solve("Vb", "{Vab} − {Va}", v => v("Vab") - v("Va"));

        /// <summary>Mixing temperature θ_AB = (V̇_A·θ_A + V̇_B·θ_B)/V̇_AB (equal ρ·c) [S29].</summary>
        private static readonly Relation MixTemp =
            new Relation("bs.mixer.temperature", T("Mixing temperature", "Mischtemperatur"), "{thab} = ({Va}·{tha} + {Vb}·{thb})/{Vab}", "thab", "Va", "tha", "Vb", "thb", "Vab") { Source = "S29" }
                .Solve("thab", "({Va}·{tha} + {Vb}·{thb})/{Vab}", v => (v("Va") * v("tha") + v("Vb") * v("thb")) / v("Vab"))
                .Solve("tha", "({Vab}·{thab} − {Vb}·{thb})/{Va}", v => (v("Vab") * v("thab") - v("Vb") * v("thb")) / v("Va"))
                .Solve("thb", "({Vab}·{thab} − {Va}·{tha})/{Vb}", v => (v("Vab") * v("thab") - v("Va") * v("tha")) / v("Vb"));

        /// <summary>Heat pump Φ_H = COP·P_el [S29, S32].</summary>
        private static readonly Relation Cop =
            new Relation("bs.heatpump.cop", T("Coefficient of performance", "Leistungszahl"), "{Phi} = {cop}·{Pel}", "Phi", "cop", "Pel") { Source = "S32" }
                .Solve("Phi", "{cop}·{Pel}", v => v("cop") * v("Pel"))
                .Solve("cop", "{Phi}/{Pel}", v => v("Phi") / v("Pel"))
                .Solve("Pel", "{Phi}/{cop}", v => v("Phi") / v("cop"));

        /// <summary>Underfloor loop Φ = q·A [S30].</summary>
        private static readonly Relation Flux =
            new Relation("bs.floor.flux", T("Heat flux", "Wärmestromdichte"), "{Phi} = {q}·{A}", "Phi", "q", "A") { Source = "S30" }
                .Solve("Phi", "{q}·{A}", v => v("q") * v("A"))
                .Solve("q", "{Phi}/{A}", v => v("Phi") / v("A"))
                .Solve("A", "{Phi}/{q}", v => v("Phi") / v("q"));

        /// <summary>Loop length L ≈ A/s (pipe spacing s) [S30].</summary>
        private static readonly Relation LoopLength =
            new Relation("bs.floor.length", T("Loop length", "Rohrlänge des Heizkreises"), "{L} = {A}/{s}", "L", "A", "s") { Source = "S30", Validity = T("approximation, connection lines not counted", "Näherung, Anbindeleitungen nicht mitgerechnet") }
                .Solve("L", "{A}/{s}", v => v("A") / v("s"))
                .Solve("A", "{L}·{s}", v => v("L") * v("s"))
                .Solve("s", "{A}/{L}", v => v("A") / v("L"));

        /// <summary>Stored heat Q = ρ·V·c·Δθ [S29].</summary>
        private static readonly Relation Stored =
            new Relation("bs.storage.heat", T("Stored heat", "Gespeicherte Wärme"), "{Q} = {rho}·{V}·{c}·{dT}", "Q", "rho", "V", "c", "dT") { Source = "S29" }
                .Solve("Q", "{rho}·{V}·{c}·{dT}", v => v("rho") * v("V") * v("c") * v("dT"))
                .Solve("V", "{Q}/({rho}·{c}·{dT})", v => v("Q") / (v("rho") * v("c") * v("dT")))
                .Solve("dT", "{Q}/({rho}·{V}·{c})", v => v("Q") / (v("rho") * v("V") * v("c")));

        private static readonly Relation HeatUp =
            new Relation("bs.storage.time", T("Heat-up time", "Aufheizzeit"), "{t} = {Q}/{P}", "t", "Q", "P") { Source = "S29", Validity = T("no losses", "ohne Verluste") }
                .Solve("t", "{Q}/{P}", v => v("Q") / v("P"))
                .Solve("Q", "{t}·{P}", v => v("t") * v("P"))
                .Solve("P", "{Q}/{t}", v => v("Q") / v("t"));

        /// <summary>Room volume V = A·h [S1].</summary>
        private static readonly Relation RoomVolume =
            new Relation("bs.room.volume", T("Room volume", "Raumvolumen"), "{V} = {A}·{h}", "V", "A", "h") { Source = "S1" }
                .Solve("V", "{A}·{h}", v => v("A") * v("h"))
                .Solve("A", "{V}/{h}", v => v("V") / v("h"))
                .Solve("h", "{V}/{A}", v => v("V") / v("A"));

        /// <summary>Air flow V̇ = n·V with the air change rate n in h⁻¹ [S30].</summary>
        private static readonly Relation AirChange =
            new Relation("bs.room.airchange", T("Air change", "Luftwechsel"), "{Vdot} = {n}·{V}/1 h", "Vdot", "n", "V") { Source = "S30" }
                .Solve("Vdot", "{n}·{V}/1 h", v => v("n") * v("V") / 3600)
                .Solve("n", "{Vdot}·1 h/{V}", v => v("Vdot") * 3600 / v("V"))
                .Solve("V", "{Vdot}·1 h/{n}", v => v("Vdot") * 3600 / v("n"));

        /// <summary>Heat recovery θ_SUP = θ_ODA + η_t·(θ_ETA − θ_ODA) [S30].</summary>
        private static readonly Relation Recovery =
            new Relation("bs.vent.recovery", T("Heat recovery", "Wärmerückgewinnung"), "{thSup} = {thOut} + {eta}·({thExt} − {thOut})", "thSup", "thOut", "eta", "thExt") { Source = "S30" }
                .Solve("thSup", "{thOut} + {eta}·({thExt} − {thOut})", v => v("thOut") + v("eta") * (v("thExt") - v("thOut")))
                .Solve("eta", "({thSup} − {thOut})/({thExt} − {thOut})", v => (v("thSup") - v("thOut")) / (v("thExt") - v("thOut")))
                .Solve("thExt", "{thOut} + ({thSup} − {thOut})/{eta}", v => v("thOut") + (v("thSup") - v("thOut")) / v("eta"))
                .Solve("thOut", "({thSup} − {eta}·{thExt})/(1 − {eta})", v => (v("thSup") - v("eta") * v("thExt")) / (1 - v("eta")));

        /// <summary>Recovered heat Φ = 0.34 W·h/(m³·K)·V̇·(θ_SUP − θ_ODA) [S30].</summary>
        private static readonly Relation Recovered =
            new Relation("bs.vent.heat", T("Recovered heat flow", "Zurückgewonnener Wärmestrom"), "{Phi} = 0.34 W·h/(m³·K)·{Vsup}·({thSup} − {thOut})", "Phi", "Vsup", "thSup", "thOut") { Source = "S30" }
                .Solve("Phi", "0.34 W·h/(m³·K)·{Vsup}·({thSup} − {thOut})", v => AirRhoC * v("Vsup") * (v("thSup") - v("thOut")))
                .Solve("Vsup", "{Phi}/(0.34 W·h/(m³·K)·({thSup} − {thOut}))", v => v("Phi") / (AirRhoC * (v("thSup") - v("thOut"))));

        /// <summary>v = V̇/A [S27].</summary>
        private static readonly Relation VelocityArea =
            new Relation("bs.flow.velocity", T("Flow velocity", "Strömungsgeschwindigkeit"), "{v} = {Vdot}/{A}", "v", "Vdot", "A") { Source = "S27" }
                .Solve("v", "{Vdot}/{A}", v => v("Vdot") / v("A"))
                .Solve("Vdot", "{v}·{A}", v => v("v") * v("A"))
                .Solve("A", "{Vdot}/{v}", v => v("Vdot") / v("v"));

        /// <summary>Round cross-section A = π·d²/4 [S27].</summary>
        private static readonly Relation RoundArea =
            new Relation("bs.duct.round", T("Cross-section (round)", "Querschnitt (rund)"), "{A} = π·{d}²/4", "A", "d") { Source = "S27" }
                .Solve("A", "π·{d}²/4", v => Math.PI * v("d") * v("d") / 4)
                .Solve("d", "√(4·{A}/π)", v => Math.Sqrt(4 * v("A") / Math.PI));

        /// <summary>Rectangular cross-section A = a·b [S30].</summary>
        private static readonly Relation RectArea =
            new Relation("bs.duct.rect", T("Cross-section (rectangular)", "Querschnitt (rechteckig)"), "{A} = {d}·{b}", "A", "d", "b") { Source = "S30" }
                .Solve("A", "{d}·{b}", v => v("d") * v("b"))
                .Solve("b", "{A}/{d}", v => v("A") / v("d"))
                .Solve("d", "{A}/{b}", v => v("A") / v("b"));

        private static readonly Relation RoundHydraulic =
            new Relation("bs.duct.dh.round", T("Hydraulic diameter (round)", "Hydraulischer Durchmesser (rund)"), "{dh} = {d}", "dh", "d") { Source = "S30" }
                .Solve("dh", "{d}", v => v("d"))
                .Solve("d", "{dh}", v => v("dh"));

        /// <summary>Hydraulic diameter of a rectangle d_h = 2·a·b/(a + b) [S30].</summary>
        private static readonly Relation RectHydraulic =
            new Relation("bs.duct.dh.rect", T("Hydraulic diameter (rectangular)", "Hydraulischer Durchmesser (rechteckig)"), "{dh} = 2·{d}·{b}/({d} + {b})", "dh", "d", "b") { Source = "S30" }
                .Solve("dh", "2·{d}·{b}/({d} + {b})", v => 2 * v("d") * v("b") / (v("d") + v("b")));

        /// <summary>Pipe: d_i ≈ DN (nominal size; the inner diameter depends on the pipe series) [S30].</summary>
        private static readonly Relation Nominal =
            new Relation("bs.pipe.dn", T("Inner diameter from DN (approximation)", "Innendurchmesser aus DN (Näherung)"), "{di} ≈ {DN}", "di", "DN")
            {
                Source = "S30", Tolerance = 0.5,
                Validity = T("DN is a nominal size; enter the inner diameter of the pipe series for exact values",
                             "DN ist eine Nennweite; für genaue Werte den Innendurchmesser der Rohrreihe eintragen"),
            }
                .Solve("di", "{DN}", v => v("DN"));

        /// <summary>Pipe: v = 4·V̇/(π·d_i²) [S27].</summary>
        private static readonly Relation PipeVelocity =
            new Relation("bs.pipe.velocity", T("Flow velocity", "Strömungsgeschwindigkeit"), "{v} = 4·{Vdot}/(π·{di}²)", "v", "Vdot", "di") { Source = "S27" }
                .Solve("v", "4·{Vdot}/(π·{di}²)", v => 4 * v("Vdot") / (Math.PI * v("di") * v("di")))
                .Solve("Vdot", "π·{di}²·{v}/4", v => Math.PI * v("di") * v("di") * v("v") / 4)
                .Solve("di", "√(4·{Vdot}/(π·{v}))", v => Math.Sqrt(4 * v("Vdot") / (Math.PI * v("v"))));

        // ------------------------------------------------------------------ pressure drop, expansion vessel, hot water, separator (round 5)

        /// <summary>Ambient pressure for absolute pressures (1.013 bar) [S30].</summary>
        private const double Ambient = 101325;

        /// <summary>
        /// Darcy friction factor: laminar λ = 64/Re below Re = 2320, turbulent the explicit Haaland approximation of the
        /// Colebrook–White equation 1/√λ = −1.8·lg((k/d/3.7)^1.11 + 6.9/Re) [S27].
        /// </summary>
        internal static double Friction(double re, double k, double d)
        {
            if (!(re > 0) || !(d > 0)) return double.NaN;
            if (re < 2320) return 64 / re;
            var x = -1.8 * Math.Log10(Math.Pow(Math.Max(k, 0) / d / 3.7, 1.11) + 6.9 / re);
            return 1 / (x * x);
        }

        private static Relation Reynolds(string d) =>
            new Relation("bs.flow.reynolds", T("Reynolds number", "Reynolds-Zahl"), "{Re} = {v}·{" + d + "}/{nu}", "Re", "v", d, "nu") { Source = "S27" }
                .Solve("Re", "{v}·{" + d + "}/{nu}", v => v("v") * v(d) / v("nu"))
                .Solve("v", "{Re}·{nu}/{" + d + "}", v => v("Re") * v("nu") / v(d))
                .Solve("nu", "{v}·{" + d + "}/{Re}", v => v("v") * v(d) / v("Re"));

        private static Relation FrictionRel(string d) =>
            new Relation("bs.flow.friction", T("Friction factor (laminar 64/Re, turbulent Haaland)", "Rohrreibungszahl (laminar 64/Re, turbulent Haaland)"),
                "{lambda} = 64/{Re} (Re < 2320) or 1/(−1.8·lg(({k}/{" + d + "}/3.7)^1.11 + 6.9/{Re}))²", "lambda", "Re", "k", d) { Source = "S27" }
                .Solve("lambda", "64/{Re} or Haaland({Re}, {k}/{" + d + "})", v => Friction(v("Re"), v("k"), v(d)));

        /// <summary>Darcy–Weisbach Δp = λ·(L/d)·ρ·v²/2 for a straight run (fittings not counted) [S27].</summary>
        private static Relation Darcy(string d) =>
            new Relation("bs.flow.loss", T("Pressure drop (Darcy–Weisbach)", "Druckverlust (Darcy–Weisbach)"),
                "{dp} = {lambda}·{L}/{" + d + "}·{rho}·{v}²/2", "dp", "lambda", "L", d, "rho", "v") { Source = "S27", Validity = T("straight run, fittings not counted", "gerade Strecke, Formteile nicht gerechnet") }
                .Solve("dp", "{lambda}·{L}/{" + d + "}·{rho}·{v}²/2", v => v("lambda") * v("L") / v(d) * v("rho") * v("v") * v("v") / 2)
                .Solve("L", "2·{dp}·{" + d + "}/({lambda}·{rho}·{v}²)", v => 2 * v("dp") * v(d) / (v("lambda") * v("rho") * v("v") * v("v")));

        /// <summary>Variables of the pressure drop: length and Δp (panel), Re, λ, ν, roughness and density (engineer).</summary>
        private static IEnumerable<VarDef> LossVars(double nu, double k, VarDef rho) => new[]
        {
            V("L", T("Length", "Länge"), T("L", "L"), Quantities.Length, "m", "length", VarRelevance.Panel) with { Min = 0, Max = 1e5 },
            V("dp", T("Pressure drop", "Druckverlust"), T("Δp", "Δp"), Quantities.Pressure, "Pa", "pressureDrop", VarRelevance.Panel) with { Min = 0, Max = 1e9 },
            V("Re", T("Reynolds number", "Reynolds-Zahl"), T("Re", "Re"), Quantities.Ratio, null, "reynolds", VarRelevance.Engineer) with { Min = 0, Max = 1e9 },
            V("lambda", T("Friction factor", "Rohrreibungszahl"), T("λ", "λ"), Quantities.Ratio, null, "friction", VarRelevance.Engineer) with { Min = 1e-4, Max = 100 },
            V("nu", T("Kinematic viscosity", "Kinematische Viskosität"), T("ν", "ν"), Quantities.Viscosity, "mm²/s", "viscosity", VarRelevance.Engineer) with { Default = nu, Min = 1e-8, Max = 1 },
            V("k", T("Wall roughness", "Wandrauigkeit"), T("k", "k"), Quantities.Length, "mm", "roughness", VarRelevance.Engineer) with { Default = k, Min = 0, Max = 0.1 },
            rho,
        };

        /// <summary>Membrane expansion vessel: V_e = e·V_A; V_n = (V_e + V_V)·(p_e + p_amb)/(p_e − p₀) (gauge pressures) [S30].</summary>
        private static readonly Relation Expansion =
            new Relation("bs.expansion.volume", T("Expansion volume", "Ausdehnungsvolumen"), "{Ve} = {e}·{VA}", "Ve", "e", "VA") { Source = "S30" }
                .Solve("Ve", "{e}·{VA}", v => v("e") * v("VA"))
                .Solve("VA", "{Ve}/{e}", v => v("Ve") / v("e"))
                .Solve("e", "{Ve}/{VA}", v => v("Ve") / v("VA"));

        private static readonly Relation VesselNominal =
            new Relation("bs.expansion.nominal", T("Nominal volume of the vessel", "Nennvolumen des Gefäßes"), "{Vn} = ({Ve} + {VV})·({pe} + p_amb)/({pe} − {p0})",
                "Vn", "Ve", "VV", "pe", "p0") { Source = "S30", Validity = T("membrane vessel, gauge pressures, p_amb = 1.013 bar", "Membrangefäß, Überdrücke, p_amb = 1,013 bar") }
                .Solve("Vn", "({Ve} + {VV})·({pe} + p_amb)/({pe} − {p0})", v => (v("Ve") + v("VV")) * (v("pe") + Ambient) / (v("pe") - v("p0")))
                .Solve("Ve", "{Vn}·({pe} − {p0})/({pe} + p_amb) − {VV}", v => v("Vn") * (v("pe") - v("p0")) / (v("pe") + Ambient) - v("VV"))
                .Solve("VV", "{Vn}·({pe} − {p0})/({pe} + p_amb) − {Ve}", v => v("Vn") * (v("pe") - v("p0")) / (v("pe") + Ambient) - v("Ve"))
                .Solve("p0", "{pe} − ({Ve} + {VV})·({pe} + p_amb)/{Vn}", v => v("pe") - (v("Ve") + v("VV")) * (v("pe") + Ambient) / v("Vn"));

        /// <summary>Storage temperature over cold water: Δθ = θ_S − θ_C [S29].</summary>
        private static readonly Relation StoreSpread =
            new Relation("bs.dhw.spread", T("Heating of the stored water", "Erwärmung des Speicherwassers"), "{dT} = {thS} − {thC}", "dT", "thS", "thC") { Source = "S29" }
                .Solve("dT", "{thS} − {thC}", v => v("thS") - v("thC"))
                .Solve("thS", "{thC} + {dT}", v => v("thC") + v("dT"))
                .Solve("thC", "{thS} − {dT}", v => v("thS") - v("dT"));

        /// <summary>Mixed water at the tap: V_mix = V·(θ_S − θ_C)/(θ_T − θ_C) (mixing with cold water, no losses) [S29].</summary>
        private static readonly Relation MixedWater =
            new Relation("bs.dhw.mixed", T("Mixed water at tap temperature", "Mischwasser bei Zapftemperatur"), "{Vmix} = {V}·({thS} − {thC})/({thT} − {thC})", "Vmix", "V", "thS", "thC", "thT")
            { Source = "S29", Validity = T("mixed with cold water, no losses", "mit Kaltwasser gemischt, ohne Verluste") }
                .Solve("Vmix", "{V}·({thS} − {thC})/({thT} − {thC})", v => v("V") * (v("thS") - v("thC")) / (v("thT") - v("thC")))
                .Solve("V", "{Vmix}·({thT} − {thC})/({thS} − {thC})", v => v("Vmix") * (v("thT") - v("thC")) / (v("thS") - v("thC")))
                .Solve("thS", "{thC} + {Vmix}·({thT} − {thC})/{V}", v => v("thC") + v("Vmix") * (v("thT") - v("thC")) / v("V"));

        /// <summary>Continuous draw at tap temperature from the heating power: V̇ = P/(ρ·c·(θ_T − θ_C)) [S29].</summary>
        private static readonly Relation ContinuousDraw =
            new Relation("bs.dhw.continuous", T("Continuous draw at tap temperature", "Dauerleistung bei Zapftemperatur"), "{Vtap} = {P}/({rho}·{c}·({thT} − {thC}))", "Vtap", "P", "rho", "c", "thT", "thC")
            { Source = "S29", Validity = T("heating power fully used for the draw, no losses", "Heizleistung ganz für das Zapfen genutzt, ohne Verluste") }
                .Solve("Vtap", "{P}/({rho}·{c}·({thT} − {thC}))", v => v("P") / (v("rho") * v("c") * (v("thT") - v("thC"))))
                .Solve("P", "{rho}·{c}·{Vtap}·({thT} − {thC})", v => v("rho") * v("c") * v("Vtap") * (v("thT") - v("thC")));

        /// <summary>
        /// Hydraulic separator (low-loss header), equal ρ·c: with V̇_P ≥ V̇_S the secondary flow takes the primary flow
        /// temperature and the surplus mixes into the primary return; with V̇_P &lt; V̇_S secondary return water mixes into the
        /// secondary flow [S30].
        /// </summary>
        private static readonly Relation SeparatorFlow =
            new Relation("bs.separator.flow", T("Secondary flow temperature", "Vorlauftemperatur sekundär"),
                "{thf2} = {thf1} (V̇_P ≥ V̇_S) or ({Vp}·{thf1} + ({Vs} − {Vp})·{thr2})/{Vs}", "thf2", "thf1", "Vp", "Vs", "thr2") { Source = "S30" }
                .Solve("thf2", "{thf1} or ({Vp}·{thf1} + ({Vs} − {Vp})·{thr2})/{Vs}",
                    v => v("Vp") >= v("Vs") ? v("thf1") : (v("Vp") * v("thf1") + (v("Vs") - v("Vp")) * v("thr2")) / v("Vs"));

        private static readonly Relation SeparatorReturn =
            new Relation("bs.separator.return", T("Primary return temperature", "Rücklauftemperatur primär"),
                "{thr1} = ({Vs}·{thr2} + ({Vp} − {Vs})·{thf1})/{Vp} (V̇_P ≥ V̇_S) or {thr2}", "thr1", "thf1", "Vp", "Vs", "thr2") { Source = "S30" }
                .Solve("thr1", "({Vs}·{thr2} + ({Vp} − {Vs})·{thf1})/{Vp} or {thr2}",
                    v => v("Vp") >= v("Vs") ? (v("Vs") * v("thr2") + (v("Vp") - v("Vs")) * v("thf1")) / v("Vp") : v("thr2"));

        /// <summary>Heat passed to the secondary circuit Φ = ρ·c·V̇_S·(θ_F2 − θ_R2) [S29].</summary>
        private static readonly Relation SeparatorHeat =
            new Relation("bs.separator.heat", T("Heat to the consumer circuit", "Wärme an den Verbraucherkreis"), "{Phi} = {rho}·{c}·{Vs}·({thf2} − {thr2})", "Phi", "rho", "c", "Vs", "thf2", "thr2") { Source = "S29" }
                .Solve("Phi", "{rho}·{c}·{Vs}·({thf2} − {thr2})", v => v("rho") * v("c") * v("Vs") * (v("thf2") - v("thr2")))
                .Solve("Vs", "{Phi}/({rho}·{c}·({thf2} − {thr2}))", v => v("Phi") / (v("rho") * v("c") * (v("thf2") - v("thr2"))));

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
            "bs.heat.boiler" => Boiler(),
            "bs.heat.heatpump" => HeatPump(),
            "bs.heat.radiator" => Radiator(),
            "bs.heat.floorloop" => FloorLoop(),
            "bs.heat.pump" => Pump(),
            "bs.heat.mixer" => Mixer(),
            "bs.san.heater" => WaterHeater(),
            "bs.heat.buffer" => Buffer(),
            "bs.meter.heat" => HeatMeter(),
            "bs.meter.water" => WaterMeter(),
            "bs.vent.terminal" => Terminal(),
            "bs.vent.unit" => VentUnit(),
            "bs.elec.appliance" => Appliance(),
            "bs.heat.expansion" => ExpansionVessel(),
            SeparatorId => Separator(),
            PipeType.TypeId => Pipe(),
            DuctType.TypeId => Duct(),
            _ => Spec.Empty,
        };

        // ------------------------------------------------------------------ electrical load (appliance outlet)

        public const string PhaseKey = "phase";
        public const string ThreePhase = "L123";

        private static Relation LoadCurrent(bool three) => three
            ? new Relation("bs.load.current3", T("Current per phase (three-phase)", "Strom je Außenleiter (dreiphasig)"), "{I} = {P}/(3·{U}·{pf})", "I", "P", "U", "pf")
            {
                Source = "S7", Validity = T("symmetrical three-phase load, U phase to neutral", "symmetrische Drehstromlast, U Außenleiter–Neutralleiter"),
            }
                .Solve("I", "{P}/(3·{U}·{pf})", v => v("P") / (3 * v("U") * v("pf")))
                .Solve("P", "3·{U}·{I}·{pf}", v => 3 * v("U") * v("I") * v("pf"))
                .Solve("U", "{P}/(3·{I}·{pf})", v => v("P") / (3 * v("I") * v("pf")))
                .Solve("pf", "{P}/(3·{U}·{I})", v => v("P") / (3 * v("U") * v("I")))
            : new Relation("bs.load.current1", T("Current (single-phase)", "Strom (einphasig)"), "{I} = {P}/({U}·{pf})", "I", "P", "U", "pf")
            {
                Source = "S7", Validity = T("U phase to neutral", "U Außenleiter–Neutralleiter"),
            }
                .Solve("I", "{P}/({U}·{pf})", v => v("P") / (v("U") * v("pf")))
                .Solve("P", "{U}·{I}·{pf}", v => v("U") * v("I") * v("pf"))
                .Solve("U", "{P}/({I}·{pf})", v => v("P") / (v("I") * v("pf")))
                .Solve("pf", "{P}/({U}·{I})", v => v("P") / (v("U") * v("I")));

        private static readonly Relation SingleCurrent = LoadCurrent(false);
        private static readonly Relation ThreeCurrent = LoadCurrent(true);

        /// <summary>
        /// Appliance outlet as an electrical load: power, current per phase, voltage phase to neutral (default 230 V) and power
        /// factor (default 1); single-phase unless its phase is three-phase [S7]. The parameters are those of the switchgear
        /// room devices, so the feeding protective device sums this load into its operating current I_B.
        /// </summary>
        private static Spec Appliance()
        {
            var vars = new[]
            {
                V("P", T("Power", "Leistung"), T("P", "P"), Quantities.Power, "W", "power") with { Min = 0, Max = 1e10 },
                V("I", T("Current (per phase)", "Strom (je Außenleiter)"), T("I", "I"), Quantities.Current, "A", "current") with { Min = 0, Max = 1e6 },
                V("U", T("Voltage phase to neutral", "Spannung Außenleiter–Neutralleiter"), T("U", "U"), Quantities.Voltage, "V", "voltage", VarRelevance.Panel) with { Default = 230, Min = 0, Max = 1e5 },
                V("pf", T("Power factor", "Leistungsfaktor"), T("cos φ", "cos φ"), Quantities.Ratio, null, "powerFactor", VarRelevance.Panel) with { Default = 1, Min = 0, Max = 1 },
            };
            return new Spec(vars, Array.Empty<Relation>(), Params(vars), Array.Empty<ValueCheck>())
            {
                Local = o => new[] { o.Str(PhaseKey, "circuit") == ThreePhase ? ThreeCurrent : SingleCurrent },
            };
        }

        /// <summary>A heat generator: the flow port supplies (total of the branches), the return port collects.</summary>
        private static IEnumerable<PortVar> GeneratorPort(string port) => port switch
        {
            "flow" => new[] { new PortVar("Vdot", "Vdot", PortRole.Total), new PortVar("theta", "thf", PortRole.Total) },
            "return" => new[] { new PortVar("Vdot", "Vdot", PortRole.Total), new PortVar("theta", "thr", PortRole.Part) },
            _ => Array.Empty<PortVar>(),
        };

        /// <summary>A consumer: the flow port takes the supply temperature, the return port gives its own.</summary>
        private static IEnumerable<PortVar> ConsumerPort(string port) => port switch
        {
            "flow" => new[] { new PortVar("Vdot", "Vdot", PortRole.Part), new PortVar("theta", "thf", PortRole.Part) },
            "return" => new[] { new PortVar("Vdot", "Vdot", PortRole.Part), new PortVar("theta", "thr", PortRole.Total) },
            _ => Array.Empty<PortVar>(),
        };

        private static Spec Boiler()
        {
            var vars = new[] { HeatOut(), FlowTemp(), ReturnTemp(), Flow(rel: VarRelevance.Card), Rho(), Cp() };
            return new Spec(vars, new[] { Carrier }, Params(vars), new[] { Balance() }) { Ports = (_, p) => GeneratorPort(p) };
        }

        private static Spec HeatPump()
        {
            var vars = new[]
            {
                HeatOut(T("Heat output", "Heizleistung")),
                V("Pel", T("Electrical power", "Elektrische Leistung"), T("P_el", "P_el"), Quantities.Power, "W", "powerEl") with { Min = 0, Max = 1e9 },
                V("cop", T("Coefficient of performance", "Leistungszahl"), T("COP", "COP"), Quantities.Ratio, null, "cop") with { Min = 0.1, Max = 100 },
                FlowTemp(),
                Temp("ths", T("Source temperature (outdoor air)", "Quellentemperatur (Außenluft)"), T("θ_Q", "θ_Q"), "sourceTemp", VarRelevance.Panel),
                ReturnTemp(VarRelevance.Panel), Flow(), Rho(), Cp(),
            };
            return new Spec(vars, new[] { Cop, Carrier }, Params(vars), new[] { Carnot(), Balance() }) { Ports = (_, p) => GeneratorPort(p) };
        }

        private static Spec Radiator()
        {
            var vars = new[]
            {
                HeatOut(),
                V("phi50", T("Standard output Φ₅₀ (Δθ = 50 K)", "Normwärmeleistung Φ₅₀ (Δθ = 50 K)"), T("Φ₅₀", "Φ₅₀"), Quantities.HeatFlow, "W", "phi50") with { Min = 1, Max = 1e6 },
                FlowTemp(), ReturnTemp(), Flow(),
                Temp("thi", T("Room temperature", "Raumtemperatur"), T("θ_i", "θ_i"), "roomTemp", VarRelevance.Panel, C0 + 20),
                V("n", T("Radiator exponent", "Heizkörperexponent"), T("n", "n"), Quantities.Ratio, null, "exponent", VarRelevance.Panel) with { Default = 1.3, Min = 0.5, Max = 3 },
                V("dT", T("Logarithmic excess temperature", "Logarithmische Übertemperatur"), T("Δθ_ln", "Δθ_ln"), Quantities.TemperatureDifference, "K", "excessTemp", VarRelevance.Panel) with { Min = 0.01, Max = 200 },
                Rho(), Cp(),
                V("PhiHL", T("Heating load of the room (estimate)", "Heizlast des Raums (Schätzwert)"), T("Φ_HL", "Φ_HL"), Quantities.HeatFlow, "W", "roomHeatLoad", VarRelevance.Panel) with { Min = 0, Max = 1e9 },
            };
            return new Spec(vars, new[] { Excess, Characteristic, Carrier, Operating }, Params(vars), new[] { RoomLoad() })
            {
                Ports = (_, p) => ConsumerPort(p),
                RoomKeys = new[] { "PhiHL" },
            };
        }

        private static Spec FloorLoop()
        {
            var vars = new[]
            {
                HeatOut(),
                V("A", T("Heated area", "Beheizte Fläche"), T("A", "A"), Quantities.Area, "m²", "area") with { Min = 1e-3, Max = 1e5 },
                V("q", T("Heat flux density", "Wärmestromdichte"), T("q", "q"), Quantities.Irradiance, "W/m²", "heatFlux") with { Min = 0, Max = 1e4 },
                Flow(rel: VarRelevance.Card), FlowTemp(VarRelevance.Panel), ReturnTemp(VarRelevance.Panel),
                V("s", T("Pipe spacing", "Verlegeabstand"), T("s", "s"), Quantities.Length, "mm", "spacing", VarRelevance.Panel) with { Min = 1e-3, Max = 2 },
                V("L", T("Loop length", "Rohrlänge des Heizkreises"), T("L", "L"), Quantities.Length, "m", "loopLength", VarRelevance.Panel) with { Min = 0, Max = 1e4 },
                Rho(), Cp(),
            };
            return new Spec(vars, new[] { Flux, Carrier, LoopLength }, Params(vars, "spacing"), Array.Empty<ValueCheck>()) { Ports = (_, p) => ConsumerPort(p) };
        }

        private static Spec Pump()
        {
            var vars = new[]
            {
                Flow(rel: VarRelevance.Card),
                V("H", T("Head", "Förderhöhe"), T("H", "H"), Quantities.Length, "m", "head") with { Min = 0, Max = 1e4 },
                V("Pel", T("Electrical power", "Elektrische Leistung"), T("P_el", "P_el"), Quantities.Power, "W", "powerEl") with { Min = 0, Max = 1e9 },
                V("eta", T("Efficiency", "Wirkungsgrad"), T("η", "η"), Quantities.Efficiency, "%", "efficiency") with { Min = 0, Max = 1 },
                V("Phyd", T("Hydraulic power", "Hydraulische Leistung"), T("P_hyd", "P_hyd"), Quantities.Power, "W", "powerHyd", VarRelevance.Panel) with { Min = 0, Max = 1e9 },
                Rho(),
            };
            return new Spec(vars, new[] { Head, PumpPower }, Params(vars), Array.Empty<ValueCheck>())
            {
                Ports = (_, p) => p switch
                {
                    "in" => new[] { new PortVar("Vdot", "Vdot", PortRole.Part) },
                    "out" => new[] { new PortVar("Vdot", "Vdot", PortRole.Total) },
                    _ => Array.Empty<PortVar>(),
                },
            };
        }

        private static Spec Mixer()
        {
            VarDef F(string key, I18nText name, string param, VarRelevance rel) => Flow(key: key, param: param, name: name, rel: rel) with { Symbol = T("V̇_" + key.Substring(1).ToUpperInvariant(), "V̇_" + key.Substring(1).ToUpperInvariant()) };
            var vars = new[]
            {
                Temp("thab", T("Mixed temperature (AB)", "Mischtemperatur (AB)"), T("θ_AB", "θ_AB"), "tempAB", VarRelevance.Card),
                Temp("tha", T("Temperature at A (hot)", "Temperatur an A (warm)"), T("θ_A", "θ_A"), "tempA", VarRelevance.Card),
                Temp("thb", T("Temperature at B (bypass)", "Temperatur an B (Beimischung)"), T("θ_B", "θ_B"), "tempB", VarRelevance.Card),
                F("Vab", T("Volume flow AB", "Volumenstrom AB"), "flowAB", VarRelevance.Card),
                F("Va", T("Volume flow A", "Volumenstrom A"), "flowA", VarRelevance.Panel),
                F("Vb", T("Volume flow B", "Volumenstrom B"), "flowB", VarRelevance.Panel),
            };
            return new Spec(vars, new[] { MixFlow, MixTemp }, Params(vars), Array.Empty<ValueCheck>())
            {
                Ports = (_, p) => p switch
                {
                    "a" => new[] { new PortVar("Vdot", "Va", PortRole.Part), new PortVar("theta", "tha", PortRole.Part) },
                    "b" => new[] { new PortVar("Vdot", "Vb", PortRole.Part), new PortVar("theta", "thb", PortRole.Part) },
                    "ab" => new[] { new PortVar("Vdot", "Vab", PortRole.Total), new PortVar("theta", "thab", PortRole.Total) },
                    _ => Array.Empty<PortVar>(),
                },
            };
        }

        private static Spec WaterHeater()
        {
            var vars = new[]
            {
                V("V", T("Storage volume", "Speichervolumen"), T("V", "V"), Quantities.Volume, "l", "volume") with { Min = 1e-4, Max = 1e3 },
                V("dT", T("Heating by", "Erwärmung um"), T("Δθ", "Δθ"), Quantities.TemperatureDifference, "K", "deltaT") with { Min = 0, Max = 200 },
                V("Vmix", T("Mixed water at tap temperature", "Mischwasser bei Zapftemperatur"), T("V_mix", "V_M"), Quantities.Volume, "l", "mixedVolume") with { Min = 0, Max = 1e4 },
                V("P", T("Heating power", "Heizleistung"), T("P", "P"), Quantities.Power, "W", "power") with { Min = 0, Max = 1e9 },
                V("Q", T("Stored heat", "Gespeicherte Wärme"), T("Q", "Q"), Quantities.Energy, "kWh", "heat", VarRelevance.Panel) with { Min = 0, Max = 1e15 },
                V("t", T("Heat-up time", "Aufheizzeit"), T("t", "t"), Quantities.Time_, "h", "heatUpTime", VarRelevance.Panel) with { Min = 0, Max = 1e8 },
                // Domestic hot water sizing (round 5): storage, cold water and tap temperatures, continuous draw.
                Temp("thS", T("Storage temperature", "Speichertemperatur"), T("θ_S", "θ_Sp"), "storeTemp", VarRelevance.Panel),
                Temp("thC", T("Cold water temperature", "Kaltwassertemperatur"), T("θ_C", "θ_KW"), "coldTemp", VarRelevance.Panel, C0 + 10),
                Temp("thT", T("Tap (mixed) temperature", "Zapftemperatur (gemischt)"), T("θ_T", "θ_Z"), "tapTemp", VarRelevance.Panel, C0 + 40),
                Flow("l/min", "Vtap", "continuousFlow", T("Continuous draw at tap temperature", "Dauerzapfmenge bei Zapftemperatur")),
                Rho(), Cp(),
            };
            return new Spec(vars, new[] { Stored, HeatUp, StoreSpread, MixedWater, ContinuousDraw }, Params(vars), Array.Empty<ValueCheck>());
        }

        /// <summary>
        /// Membrane expansion vessel: system volume and expansion coefficient give the expansion volume, with the water seal
        /// and the pre-charge and final pressures the required nominal volume; a chosen vessel is checked against it [S30].
        /// </summary>
        private static Spec ExpansionVessel()
        {
            VarDef Vol(string key, I18nText name, string sym, string param, VarRelevance rel) => V(key, name, T(sym, sym), Quantities.Volume, "l", param, rel) with { Min = 0, Max = 1e5 };
            VarDef Gauge(string key, I18nText name, I18nText sym, string param) => V(key, name, sym, Quantities.Pressure, "bar", param, VarRelevance.Panel) with { Min = 0, Max = 1e8 };
            var vars = new[]
            {
                Vol("VA", T("Water volume of the system", "Wasserinhalt der Anlage"), "V_A", "systemVolume", VarRelevance.Card),
                V("e", T("Expansion coefficient (from cold to the highest temperature)", "Ausdehnungskoeffizient (von kalt bis zur höchsten Temperatur)"), T("e", "e"),
                    Quantities.Ratio, "%", "expansion") with { Min = 0, Max = 0.2 },
                Vol("Vn", T("Required nominal volume", "Erforderliches Nennvolumen"), "V_n", "nominalVolume", VarRelevance.Card),
                Vol("Vsel", T("Chosen vessel (nominal volume)", "Gewähltes Gefäß (Nennvolumen)"), "V_n,sel", "chosenVolume", VarRelevance.Card) with { Symbol = T("V_n,sel", "V_n,gew") },
                Vol("Ve", T("Expansion volume", "Ausdehnungsvolumen"), "V_e", "expansionVolume", VarRelevance.Panel),
                Vol("VV", T("Water seal", "Wasservorlage"), "V_V", "waterSeal", VarRelevance.Panel),
                Gauge("p0", T("Pre-charge pressure (gauge)", "Vordruck (Überdruck)"), T("p₀", "p₀"), "prePressure"),
                Gauge("pe", T("Final pressure (gauge)", "Enddruck (Überdruck)"), T("p_e", "p_e"), "finalPressure"),
            };
            return new Spec(vars, new[] { Expansion, VesselNominal }, Params(vars), new[] { VesselSize() });
        }

        /// <summary>Type id of the hydraulic separator (low-loss header).</summary>
        public const string SeparatorId = "bs.heat.separator";

        /// <summary>
        /// Hydraulic separator: the generator circuit (f1/r1, a consumer of the generator) and the consumer circuit (f2/r2, a
        /// generator for the consumers) meet in the separator; flows and temperatures pass along the pipes [S30].
        /// </summary>
        private static Spec Separator()
        {
            VarDef F(string key, I18nText name, I18nText sym, string param) => Flow(key: key, param: param, name: name, rel: VarRelevance.Card) with { Symbol = sym };
            var vars = new[]
            {
                F("Vp", T("Primary (generator) flow", "Volumenstrom primär (Erzeuger)"), T("V̇_P", "V̇_P"), "flowPrimary"),
                F("Vs", T("Secondary (consumer) flow", "Volumenstrom sekundär (Verbraucher)"), T("V̇_S", "V̇_S"), "flowSecondary"),
                Temp("thf2", T("Secondary flow temperature", "Vorlauftemperatur sekundär"), T("θ_F2", "θ_VL2"), "flowTemp2", VarRelevance.Card),
                Temp("thr1", T("Primary return temperature", "Rücklauftemperatur primär"), T("θ_R1", "θ_RL1"), "returnTemp1", VarRelevance.Card),
                Temp("thf1", T("Primary flow temperature", "Vorlauftemperatur primär"), T("θ_F1", "θ_VL1"), "flowTemp1", VarRelevance.Panel),
                Temp("thr2", T("Secondary return temperature", "Rücklauftemperatur sekundär"), T("θ_R2", "θ_RL2"), "returnTemp2", VarRelevance.Panel),
                HeatOut(T("Heat to the consumer circuit", "Wärme an den Verbraucherkreis"), VarRelevance.Panel),
                Rho(), Cp(),
            };
            return new Spec(vars, new[] { SeparatorFlow, SeparatorReturn, SeparatorHeat }, Params(vars), new[] { SeparatorFlowCheck() })
            {
                Ports = (_, p) => p switch
                {
                    "f1" => new[] { new PortVar("Vdot", "Vp", PortRole.Part), new PortVar("theta", "thf1", PortRole.Part) },
                    "r1" => new[] { new PortVar("Vdot", "Vp", PortRole.Part), new PortVar("theta", "thr1", PortRole.Total) },
                    "f2" => new[] { new PortVar("Vdot", "Vs", PortRole.Total), new PortVar("theta", "thf2", PortRole.Total) },
                    "r2" => new[] { new PortVar("Vdot", "Vs", PortRole.Total), new PortVar("theta", "thr2", PortRole.Part) },
                    _ => Array.Empty<PortVar>(),
                },
            };
        }

        /// <summary>Buffer tank: stored heat of the volume over a temperature spread (no losses, no heat-up time).</summary>
        private static Spec Buffer()
        {
            var vars = new[]
            {
                V("V", T("Storage volume", "Speichervolumen"), T("V", "V"), Quantities.Volume, "l", "volume") with { Min = 1e-4, Max = 1e3 },
                V("dT", T("Temperature spread", "Temperaturspreizung"), T("Δθ", "Δθ"), Quantities.TemperatureDifference, "K", "deltaT") with { Min = 0, Max = 200 },
                V("Q", T("Stored heat", "Gespeicherte Wärme"), T("Q", "Q"), Quantities.Energy, "kWh", "heat") with { Min = 0, Max = 1e15 },
                Rho(), Cp(),
            };
            return new Spec(vars, new[] { Stored }, Params(vars), Array.Empty<ValueCheck>());
        }

        /// <summary>Inline meter ports like the circulator: the inlet is a part, the outlet the total of what flows on.</summary>
        private static IEnumerable<PortVar> InlinePort(string port) => port switch
        {
            "in" => new[] { new PortVar("Vdot", "Vdot", PortRole.Part) },
            "out" => new[] { new PortVar("Vdot", "Vdot", PortRole.Total) },
            _ => Array.Empty<PortVar>(),
        };

        /// <summary>Heat meter: shows the thermal power from volume flow and the flow/return temperatures it measures.</summary>
        private static Spec HeatMeter()
        {
            var vars = new[]
            {
                HeatOut(T("Thermal power", "Wärmeleistung")), Flow(rel: VarRelevance.Card), FlowTemp(), ReturnTemp(), Rho(), Cp(),
            };
            return new Spec(vars, new[] { Carrier }, Params(vars), Array.Empty<ValueCheck>()) { Ports = (_, p) => InlinePort(p) };
        }

        /// <summary>Water meter: shows the volume flow passing through it.</summary>
        private static Spec WaterMeter()
        {
            var vars = new[] { Flow("m³/h", rel: VarRelevance.Card) };
            return new Spec(vars, Array.Empty<Relation>(), Params(vars), Array.Empty<ValueCheck>()) { Ports = (_, p) => InlinePort(p) };
        }

        private static Spec Terminal()
        {
            var vars = new[]
            {
                Flow("m³/h", rel: VarRelevance.Card) with { Name = T("Air flow", "Luftvolumenstrom") },
                V("A", T("Room area", "Raumfläche"), T("A", "A"), Quantities.Area, "m²", "roomArea") with { Min = 1e-2, Max = 1e5 },
                V("n", T("Air change rate (per hour)", "Luftwechselzahl (je Stunde)"), T("n", "n"), Quantities.Ratio, null, "airChange") with { Min = 0, Max = 100 },
                V("V", T("Room volume", "Raumvolumen"), T("V", "V"), Quantities.Volume, "m³", "roomVolume") with { Min = 1e-2, Max = 1e6 },
                V("h", T("Room height", "Raumhöhe"), T("h", "h"), Quantities.Length, "m", "roomHeight", VarRelevance.Panel) with { Default = 2.5, Min = 0.5, Max = 100 },
            };
            return new Spec(vars, new[] { RoomVolume, AirChange }, Params(vars), Array.Empty<ValueCheck>())
            {
                Ports = (_, p) => p == "duct" ? new[] { new PortVar("Vdot", "Vdot", PortRole.Part) } : Array.Empty<PortVar>(),
                // Placed inside a room (arch.room), the terminal takes its floor area, height and volume.
                RoomKeys = new[] { "A", "h", "V" },
            };
        }

        private static Spec VentUnit()
        {
            var vars = new[]
            {
                Flow("m³/h", "Vsup", "flowSupply", T("Supply air flow", "Zuluftvolumenstrom"), VarRelevance.Card) with { Symbol = T("V̇_SUP", "V̇_ZUL") },
                V("eta", T("Heat recovery efficiency", "Wärmebereitstellungsgrad"), T("η_t", "η_t"), Quantities.Efficiency, "%", "recovery") with { Min = 0, Max = 1 },
                Temp("thSup", T("Supply air temperature", "Zulufttemperatur"), T("θ_SUP", "θ_ZUL"), "tempSupply", VarRelevance.Card),
                V("Phi", T("Recovered heat flow", "Zurückgewonnener Wärmestrom"), T("Φ_WRG", "Φ_WRG"), Quantities.HeatFlow, "W", "heatRecovered") with { Min = -1e9, Max = 1e9 },
                Flow("m³/h", "Vext", "flowExtract", T("Extract air flow", "Abluftvolumenstrom"), VarRelevance.Panel) with { Symbol = T("V̇_ETA", "V̇_ABL") },
                Temp("thOut", T("Outdoor air temperature", "Außenlufttemperatur"), T("θ_ODA", "θ_AUL"), "tempOutdoor", VarRelevance.Panel),
                Temp("thExt", T("Extract air temperature", "Ablufttemperatur"), T("θ_ETA", "θ_ABL"), "tempExtract", VarRelevance.Panel),
            };
            return new Spec(vars, new[] { Recovery, Recovered }, Params(vars), Array.Empty<ValueCheck>())
            {
                Ports = (_, p) => p switch
                {
                    "supply" or "outdoor" => new[] { new PortVar("Vdot", "Vsup", PortRole.Total) },
                    "extract" or "exhaust" => new[] { new PortVar("Vdot", "Vext", PortRole.Total) },
                    _ => Array.Empty<PortVar>(),
                },
            };
        }

        private static Spec Pipe()
        {
            var vars = new[]
            {
                Flow(rel: VarRelevance.Card),
                V("di", T("Inner diameter", "Innendurchmesser"), T("d_i", "d_i"), Quantities.Length, "mm", "innerDiameter") with { Min = 1e-4, Max = 5 },
                Velocity(),
                Temp("theta", T("Water temperature", "Wassertemperatur"), T("θ", "θ"), "temperature", VarRelevance.Card),
                V("DN", T("Nominal size DN", "Nennweite DN"), T("DN", "DN"), Quantities.Length, "mm", "dn", VarRelevance.Panel) with { StoredUnit = "mm", Min = 1e-3, Max = 5 },
            }.Concat(LossVars(WaterNu, PipeK, Rho())).ToArray();
            return new Spec(vars, new[] { PipeVelocity, Nominal, Reynolds("di"), FrictionRel("di"), Darcy("di") }, Params(vars, "dn"), new[] { WaterVelocity() });
        }

        /// <summary>Water at 20 °C ν ≈ 1.0 mm²/s; smooth pipe (copper, plastic) k = 0.0015 mm [S27, S30].</summary>
        private const double WaterNu = 1.0e-6, PipeK = 0.0015e-3;
        /// <summary>Air at 20 °C ρ = 1.2 kg/m³, ν ≈ 15 mm²/s; galvanised sheet duct k = 0.15 mm [S27, S30].</summary>
        private const double AirRho = 1.2, AirNu = 15e-6, DuctK = 0.15e-3;

        private static Spec Duct()
        {
            var vars = new[]
            {
                Flow("m³/h", rel: VarRelevance.Card) with { Name = T("Air flow", "Luftvolumenstrom") },
                V("d", T("Duct size (diameter / width)", "Kanalgröße (Durchmesser / Breite)"), T("d", "d"), Quantities.Length, "mm", "width") with { StoredUnit = "mm", Min = 1e-3, Max = 10 },
                Velocity(),
                V("A", T("Cross-section", "Querschnitt"), T("A", "A"), Quantities.Area, "cm²", "crossSection") with { Min = 1e-6, Max = 100 },
                V("b", T("Duct height (rectangular)", "Kanalhöhe (rechteckig)"), T("b", "b"), Quantities.Length, "mm", "ductHeight", VarRelevance.Panel) with { Min = 1e-3, Max = 10 },
                V("dh", T("Hydraulic diameter", "Hydraulischer Durchmesser"), T("d_h", "d_h"), Quantities.Length, "mm", "hydraulicDiameter", VarRelevance.Panel) with { Min = 1e-3, Max = 10 },
            }.Concat(LossVars(AirNu, DuctK,
                V("rho", T("Density of air", "Dichte der Luft"), T("ρ", "ρ"), Quantities.Density, "kg/m³", "density", VarRelevance.Engineer) with { Default = AirRho, Min = 0.01, Max = 100 })).ToArray();
            return new Spec(vars, new[] { VelocityArea, Reynolds("dh"), FrictionRel("dh"), Darcy("dh") }, Params(vars, "width"), new[] { AirVelocity() })
            {
                // A height b makes the duct rectangular (a = width); otherwise it is round.
                Local = o => o.Params.TryGetValue("ductHeight", out var b) && !string.IsNullOrWhiteSpace(b.Text)
                    ? new[] { RectArea, RectHydraulic }
                    : new[] { RoundArea, RoundHydraulic },
            };
        }

        // ------------------------------------------------------------------ net description

        private static IEnumerable<string[]> Through(SketchObject o, string cls) => o.Type switch
        {
            "bs.heat.trv" or "bs.valve.ball" or "bs.valve.check" or "bs.strainer" => new[] { new[] { "in", "out" } },
            "bs.heat.pump" or "bs.meter.heat" or "bs.meter.water" when cls == "theta" => new[] { new[] { "in", "out" } },
            "bs.vent.damper" or "bs.vent.firedamper" or "bs.vent.silencer" when cls == "Vdot" => new[] { new[] { "inlet", "outlet" } },
            _ => Array.Empty<string[]>(),
        };

        internal static readonly NetSpec Nets = new()
        {
            Prefix = "bs",
            IsLine = o => o.Type is PipeType.TypeId or DuctType.TypeId,
            PortVars = (o, port) => For(o.Type).Ports(o, port),
            LineVars = o => o.Type == PipeType.TypeId
                ? new[] { new PortVar("Vdot", "Vdot"), new PortVar("theta", "theta") }
                : new[] { new PortVar("Vdot", "Vdot") },
            Through = Through,
            // Flow and return (and the air kinds) stay apart where they meet only at a port of another module's element;
            // there the heating flow takes the port's values (an energy heat port gives V̇ and the flow temperature).
            Medium = o => o.Type == PipeType.TypeId ? PipeType.MediumOf(o.Str("medium", "cold")) : PortMedia.AirOf(o.Str("kind", "supply")),
            Leads = m => m == PortMedia.HeatingFlow,
            Classes = new[] { "Vdot", "theta" },
            Rule = c => c == "Vdot" ? NetRule.Sum : NetRule.Spread,
            Name = c => c == "Vdot" ? T("Volume flow passed on", "Volumenstrom weitergegeben") : T("Temperature passed on", "Temperatur weitergegeben"),
        };

        public static IEnumerable<BoundRelation> Links(SketchObject o, RenderContext ctx)
        {
            var spec = For(o.Type);
            if (spec.Local is not null)
                foreach (var r in spec.Local(o)) yield return BoundRelation.Local(r, o.Id);
            foreach (var b in ValueNets.Of(ctx.Doc, Nets).For(o.Id)) yield return b;
            foreach (var b in RoomLinks(o, spec, ctx)) yield return b;
        }

        // ------------------------------------------------------------------ room values by containment

        /// <summary>Type id of the architecture room (A, h, V, Φ_HL); referenced by id only, modules do not depend on each other.</summary>
        public const string RoomTypeId = "arch.room";

        private static readonly Relation RoomLink = Relation.Equality("bs.room.link", T("Taken from the room", "Aus dem Raum übernommen"));

        private sealed record Room(string Id, string? Page, IReadOnlyList<Vec2> Outline, double Area);

        private sealed class RoomIndex
        {
            public readonly List<Room> Rooms = new();
            public readonly Dictionary<string, string> PageOf = new(StringComparer.Ordinal);
        }

        /// <summary>Rooms and the page of every object, per document (documents are immutable).</summary>
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<SketchDocument, RoomIndex> RoomsOf = new();

        private static RoomIndex Index(SketchDocument doc) => RoomsOf.GetValue(doc, d =>
        {
            var index = new RoomIndex();
            foreach (var page in d.Pages)
                foreach (var id in page.Order)
                    if (!index.PageOf.ContainsKey(id)) index.PageOf[id] = page.Id;
            foreach (var o in d.Objects.Values.OrderBy(x => x.Id, StringComparer.Ordinal))
            {
                if (o.Type != RoomTypeId || !o.IsPointGeometry) continue;
                var p = d.ResolvePoints(o);
                if (p.Count < 3) continue;
                index.Rooms.Add(new Room(o.Id, index.PageOf.TryGetValue(o.Id, out var pg) ? pg : null, p, Math.Abs(Geo.PolygonArea(p))));
            }
            return index;
        });

        /// <summary>
        /// The room (<see cref="RoomTypeId"/>) an element stands in: its centre inside the room's outline on the same page;
        /// the smallest such room when rooms overlap (ties by id). Null for lines and elements outside every room.
        /// </summary>
        internal static string? ContainingRoom(SketchDocument doc, SketchObject o)
        {
            if (o.IsPointGeometry) return null;
            var index = Index(doc);
            if (index.Rooms.Count == 0) return null;
            var page = index.PageOf.TryGetValue(o.Id, out var pg) ? pg : null;
            var c = new Vec2(o.Transform.X, o.Transform.Y);
            Room? best = null;
            foreach (var r in index.Rooms)
                if (r.Page == page && r.Id != o.Id && Geo.PointInPolygon(c, r.Outline) && (best is null || r.Area < best.Area)) best = r;
            return best?.Id;
        }

        /// <summary>
        /// Room values by containment (engineering-calculations.md, section 6.1): an element placed inside a room takes the
        /// room's variables named in <see cref="Spec.RoomKeys"/> through an equality, when the room's type has a variable of
        /// the same key and quantity kind. The room is not joined by a line; it joins the solver scope through the link.
        /// </summary>
        private static IEnumerable<BoundRelation> RoomLinks(SketchObject o, Spec spec, RenderContext ctx)
        {
            if (spec.RoomKeys.Count == 0 || ctx.Registry is not { } registry) yield break;
            if (ContainingRoom(ctx.Doc, o) is not { } roomId || ctx.Doc.Get(roomId) is not { } room) yield break;
            var roomVars = ValueModel.Variables(registry.Type(room.Type));
            foreach (var key in spec.RoomKeys)
            {
                var mine = spec.Variables.FirstOrDefault(v => v.Key == key);
                var theirs = roomVars.FirstOrDefault(v => v.Key == key);
                if (mine is null || theirs is null || mine.Kind.Id != theirs.Kind.Id) continue;
                yield return new BoundRelation(RoomLink, new Dictionary<string, VarRef>(StringComparer.Ordinal)
                {
                    ["a"] = new VarRef(o.Id, key), ["b"] = new VarRef(roomId, key),
                });
            }
        }

        // ------------------------------------------------------------------ checks (section 4)

        private const double Rel = 1e-6;

        private static bool Heating(SketchObject pipe) => pipe.Str("medium", "cold") is "flow" or "return";

        /// <summary>heat.velocity: heating pipes outside 0.3–1.0 m/s, water pipes above 2 m/s (textbook values [S30]).</summary>
        private static ValueCheck WaterVelocity() => new(
            "heat.velocity", CheckSeverity.Hint,
            T("The flow velocity is outside the usual range (heating distribution 0.3–1.0 m/s, water pipes up to 2 m/s). Check the inner diameter.",
              "Die Strömungsgeschwindigkeit liegt außerhalb des üblichen Bereichs (Heizungsverteilung 0,3–1,0 m/s, Wasserleitungen bis 2 m/s). Innendurchmesser prüfen."),
            "heating: v ∉ [0.3, 1.0] m/s; water: v > 2 m/s",
            c => c["v"] is double v && (Heating(c.Object) ? v < 0.3 * (1 - Rel) || v > 1.0 * (1 + Rel) : v > 2.0 * (1 + Rel)))
        { Source = "S30" };

        /// <summary>air.velocity: duct velocity outside 2–4 m/s (residential, textbook [S30]).</summary>
        private static ValueCheck AirVelocity() => new(
            "air.velocity", CheckSeverity.Hint,
            T("The air velocity is outside the usual residential range of 2–4 m/s. Check the duct size.",
              "Die Luftgeschwindigkeit liegt außerhalb des im Wohnbau üblichen Bereichs von 2–4 m/s. Kanalgröße prüfen."),
            "v ∉ [2, 4] m/s",
            c => c["v"] is double v && (v < 2 * (1 - Rel) || v > 4 * (1 + Rel)))
        { Source = "S30" };

        /// <summary>heat.cop.carnot: COP above the Carnot limit T_F/(T_F − T_Q) in kelvin (impossible) [S29].</summary>
        private static ValueCheck Carnot() => new(
            "heat.cop.carnot", CheckSeverity.Warning,
            T("The COP is above the Carnot limit T_flow/(T_flow − T_source): impossible. Check the COP and the temperatures.",
              "Die Leistungszahl liegt über der Carnot-Grenze T_VL/(T_VL − T_Q): unmöglich. Leistungszahl und Temperaturen prüfen."),
            "COP > T_F/(T_F − T_Q)",
            c => c["cop"] is double cop && c["thf"] is double tf && c["ths"] is double ts && tf > ts && cop > tf / (tf - ts) * (1 + Rel))
        { Source = "S29" };

        /// <summary>heat.expansion.size: the chosen vessel is smaller than the required nominal volume [S30].</summary>
        private static ValueCheck VesselSize() => new(
            "heat.expansion.size", CheckSeverity.Warning,
            T("The chosen expansion vessel is smaller than the required nominal volume. Choose the next larger vessel.",
              "Das gewählte Ausdehnungsgefäß ist kleiner als das erforderliche Nennvolumen. Das nächstgrößere Gefäß wählen."),
            "V_n,sel ≥ V_n",
            c => c["Vsel"] is double sel && c["Vn"] is double need && need > 0 && sel < need * (1 - Rel))
        { Source = "S30" };

        /// <summary>
        /// heat.separator.flow: the primary flow is smaller than the secondary flow, so return water mixes into the consumer
        /// flow and its temperature drops (the generator side is usually laid out for a slightly larger flow) [S30].
        /// </summary>
        private static ValueCheck SeparatorFlowCheck() => new(
            "heat.separator.flow", CheckSeverity.Hint,
            T("The generator flow is smaller than the consumer flow: return water mixes into the consumer flow and lowers its temperature. Check the pump flows.",
              "Der Volumenstrom des Erzeugers ist kleiner als der der Verbraucher: Rücklaufwasser mischt sich in den Verbrauchervorlauf und senkt seine Temperatur. Pumpenförderströme prüfen."),
            "V̇_P ≥ V̇_S",
            c => c["Vp"] is double vp && c["Vs"] is double vs && vp < vs * (1 - Rel))
        { Source = "S30" };

        private static readonly HashSet<string> Consumers =new(StringComparer.Ordinal) { "bs.heat.radiator", "bs.heat.floorloop" };

        /// <summary>heat.balance: the consumers on the generator's heating circuit need more than the generator gives [S29].</summary>
        private static ValueCheck Balance() => new(
            "heat.balance", CheckSeverity.Hint,
            T("The consumers on this heating circuit need more heat than the generator gives. Check the outputs.",
              "Die Verbraucher an diesem Heizkreis brauchen mehr Wärme, als der Wärmeerzeuger abgibt. Leistungen prüfen."),
            "ΣΦ_consumers > Φ_source",
            c =>
            {
                if (c.Table.Get(c.Object.Id, "Phi") is not { Value: double source, Provenance: Provenance.Entered }) return false;
                var sum = 0.0;
                foreach (var id in HeatingCircuit(c.Context.Doc, c.Object.Id))
                    if (c.Context.Doc.Get(id) is { } x && Consumers.Contains(x.Type) && c.Table.Get(id, "Phi")?.Value is double phi) sum += phi;
                return sum > source * (1 + Rel);
            })
        { Source = "S29" };

        /// <summary>
        /// heat.roomload: the heating surfaces (radiators, underfloor loops) standing in the room give less than the room's
        /// heating load estimate Φ_HL. Judged only when the output of every heating surface in the room is known [S30].
        /// </summary>
        private static ValueCheck RoomLoad() => new(
            "heat.roomload", CheckSeverity.Hint,
            T("The heating surfaces in this room give less heat than the room's heating load estimate. Check the outputs.",
              "Die Heizflächen in diesem Raum geben weniger Wärme ab als der Schätzwert der Heizlast des Raums. Leistungen prüfen."),
            "ΣΦ_room < Φ_HL",
            c =>
            {
                if (c["PhiHL"] is not double load || !(load > 0) || ContainingRoom(c.Context.Doc, c.Object) is not { } room) return false;
                var sum = 0.0;
                foreach (var x in c.Context.Doc.Objects.Values)
                {
                    if (!Consumers.Contains(x.Type) || ContainingRoom(c.Context.Doc, x) != room) continue;
                    if (c.Table.Get(x.Id, "Phi")?.Value is not double phi) return false;
                    sum += phi;
                }
                return sum < load * (1 - Rel);
            })
        { Source = "S30" };

        /// <summary>Elements joined to <paramref name="id"/> by heating pipes (through every element on the way).</summary>
        internal static IReadOnlyCollection<string> HeatingCircuit(SketchDocument doc, string id)
        {
            var adj = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            void Edge(string a, string b)
            {
                if (!adj.TryGetValue(a, out var l)) adj[a] = l = new List<string>();
                l.Add(b);
                if (!adj.TryGetValue(b, out l)) adj[b] = l = new List<string>();
                l.Add(a);
            }
            foreach (var line in doc.Objects.Values)
                if (line.Type == PipeType.TypeId && Heating(line))
                    foreach (var a in line.Attach ?? Array.Empty<Attachment?>())
                        if (a is not null && doc.Get(a.ObjectId) is not null) Edge(line.Id, a.ObjectId);
            var seen = new HashSet<string>(StringComparer.Ordinal) { id };
            var queue = new Queue<string>(new[] { id });
            while (queue.Count > 0)
                foreach (var n in adj.TryGetValue(queue.Dequeue(), out var l) ? l : new List<string>())
                    if (seen.Add(n)) queue.Enqueue(n);
            return seen;
        }
    }
}
