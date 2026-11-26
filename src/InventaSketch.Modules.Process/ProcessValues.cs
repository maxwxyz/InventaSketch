using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Process
{
    /// <summary>
    /// Element values of the process and thermal symbols (Calc 4, engineering-calculations.md sections 2.8 and 3.6): pumps and
    /// fans (head, pressure rise, hydraulic and shaft power), heat exchangers (energy balance on both sides, counter-flow LMTD,
    /// Φ = U·A·Δθ_lm), single-stream coolers, vessels (volume, stored heat) and the process line (mass flow, velocity,
    /// temperature). Every value is optional.
    /// <para>
    /// Port flows (<see cref="ValueNets"/>): lines join nozzles into nets; the classes volume flow <c>Vdot</c>, mass flow
    /// <c>m</c> and stream temperature <c>theta</c> are shared when at most two elements carry them in a net (a branch stops
    /// them). The line converts volume and mass flow (ṁ = ρ·V̇). Valves, strainers, reducers and flow elements pass all
    /// three; pumps and fans pass the temperature. Heat losses of lines and the temperature rise in pumps are not modelled.
    /// </para>
    /// <para>
    /// Shaft (<see cref="ShaftLinks"/>): pumps, fans, compressors, turbines, the drive motor and the generator carry speed n,
    /// torque T and shaft power P = 2π·n·T; a machine docked on a drive or a mechanical shaft shares n (and T and P over a
    /// direct coupling).
    /// </para>
    /// <para>
    /// Gas machines (compressor, turbine): an ideal gas with constant properties (κ, R from the selected gas, c_p = κ·R/(κ − 1)),
    /// pressure ratio, isentropic outlet temperature T₂s = T₁·π^(±(κ−1)/κ), isentropic efficiency, actual outlet temperature,
    /// power P = ṁ·c_p·ΔT, specific work w = c_p·ΔT (kJ/kg) and the volume flows ṁ·R·T/p (thermodynamics textbooks, S40). The nozzles pass ṁ, θ and p; not V̇, which changes with p and T along a gas line (a valve passes V̇ unchanged, right for liquids only).
    /// </para>
    /// </summary>
    internal static class ProcessValues
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
            /// <summary>Variables at a nozzle (net classes Vdot, m, theta, p; the generator terminal: P for the joins across modules).</summary>
            public Func<SketchObject, string, IEnumerable<PortVar>> Ports { get; init; } = (_, _) => Array.Empty<PortVar>();
            /// <summary>Relations that depend on the object (the property values of the selected gas); null = none.</summary>
            public Func<SketchObject, IEnumerable<Relation>>? Local { get; init; }
        }

        // ------------------------------------------------------------------ parameters and variables

        private static ParamDef Text(string key, I18nText label, string? unit) => new(key, label, ParamKind.Text) { Unit = unit, Advanced = true };

        private static VarDef V(string key, I18nText name, I18nText symbol, QuantityKind kind, string? unit, string param, VarRelevance rel = VarRelevance.Card)
            => new(key, name, symbol, kind) { Unit = unit, Param = param, Relevance = rel };

        private static ParamDef[] Params(IEnumerable<VarDef> vars)
            => vars.Select(v => Text(v.ParamKey, v.Name, v.Unit)).Append(ValueLabels.ShowValues).ToArray();

        private const double G = 9.80665;
        private const double WaterC = 4190;
        private const double WaterRho = 1000;

        private static VarDef VolFlow(VarRelevance rel = VarRelevance.Card) => V("Vdot", T("Volume flow", "Volumenstrom"), T("V̇", "V̇"), Quantities.VolumeFlow, "m³/h", "flowRate", rel) with { Min = 0, Max = 1e3 };
        private static VarDef MassFlow(string key, I18nText name, string sym, string param, VarRelevance rel) => V(key, name, T(sym, sym), Quantities.MassFlow, "kg/h", param, rel) with { Min = 0, Max = 1e5 };
        private static VarDef Temp(string key, I18nText name, string sym, string param, VarRelevance rel) => V(key, name, T(sym, sym), Quantities.Temperature, "°C", param, rel) with { Min = 1, Max = 2000 };
        private static VarDef Cp(string key, string sym, string param) => V(key, T("Specific heat capacity", "Spezifische Wärmekapazität") , T(sym, sym), Quantities.SpecificHeat, "kJ/(kg·K)", param, VarRelevance.Engineer) with { Default = WaterC, Min = 1, Max = 1e5 };
        private static VarDef Rho(VarRelevance rel = VarRelevance.Engineer) => V("rho", T("Density", "Dichte"), T("ρ", "ρ"), Quantities.Density, "kg/m³", "density", rel) with { Default = WaterRho, Min = 1e-3, Max = 3e4 };
        private static VarDef Duty(VarRelevance rel = VarRelevance.Card) => V("Phi", T("Heat flow (duty)", "Wärmestrom (Leistung)"), T("Φ", "Φ"), Quantities.HeatFlow, "W", "duty", rel) with { Min = -1e10, Max = 1e10 };
        private static VarDef PowerVar(string key, I18nText name, string sym, string param, VarRelevance rel) => V(key, name, T(sym, sym), Quantities.Power, "W", param, rel) with { Min = 0, Max = 1e10 };
        private static VarDef Dp(VarRelevance rel) => V("dp", T("Pressure rise", "Druckerhöhung"), T("Δp", "Δp"), Quantities.Pressure, "bar", "pressureRise", rel) with { Min = 0, Max = 1e9 };
        private static VarDef Eta() => V("eta", T("Efficiency", "Wirkungsgrad"), T("η", "η"), Quantities.Efficiency, "%", "efficiency", VarRelevance.Panel) with { Min = 0, Max = 1 };
        private static VarDef Len(string key, I18nText name, string sym, string param, VarRelevance rel) => V(key, name, T(sym, sym), Quantities.Length, "m", param, rel) with { Min = 1e-4, Max = 1e3 };
        /// <summary>Shaft speed n (same key and quantity as the shafts and drives of the mechanical and machines modules).</summary>
        private static VarDef Speed(VarRelevance rel) => V("n", T("Speed of rotation", "Drehzahl"), T("n", "n"), Quantities.RotationalSpeed, "min⁻¹", "speed", rel) with { Min = 0, Max = 1e5 };
        /// <summary>Shaft torque T (German symbol M).</summary>
        private static VarDef Torque(VarRelevance rel) => V("T", T("Torque", "Drehmoment"), T("T", "M"), Quantities.Torque, "N·m", "torque", rel) with { Min = 0, Max = 1e7 };

        // ------------------------------------------------------------------ relations

        /// <summary>Δp = ρ·g·H [S28].</summary>
        private static readonly Relation HeadRel =
            new Relation("process.pump.head", T("Head", "Förderhöhe"), "{dp} = {rho}·g·{H}", "dp", "rho", "H") { Source = "S28" }
                .Solve("dp", "{rho}·g·{H}", v => v("rho") * G * v("H"))
                .Solve("H", "{dp}/({rho}·g)", v => v("dp") / (v("rho") * G))
                .Solve("rho", "{dp}/(g·{H})", v => v("dp") / (G * v("H")));

        /// <summary>P_hyd = Δp·V̇ [S28].</summary>
        private static readonly Relation HydPower =
            new Relation("process.power.hyd", T("Hydraulic power", "Hydraulische Leistung"), "{Phyd} = {dp}·{Vdot}", "Phyd", "dp", "Vdot") { Source = "S28" }
                .Solve("Phyd", "{dp}·{Vdot}", v => v("dp") * v("Vdot"))
                .Solve("dp", "{Phyd}/{Vdot}", v => v("Phyd") / v("Vdot"))
                .Solve("Vdot", "{Phyd}/{dp}", v => v("Phyd") / v("dp"));

        /// <summary>P = P_hyd/η [S28].</summary>
        private static readonly Relation ShaftPower =
            new Relation("process.power.shaft", T("Shaft power", "Wellenleistung"), "{P} = {Phyd}/{eta}", "P", "Phyd", "eta") { Source = "S28" }
                .Solve("P", "{Phyd}/{eta}", v => v("Phyd") / v("eta"))
                .Solve("Phyd", "{P}·{eta}", v => v("P") * v("eta"))
                .Solve("eta", "{Phyd}/{P}", v => v("Phyd") / v("P"));

        /// <summary>P = 2π·n·T (n in rad/s internally) [S18]: the shaft power from speed and torque.</summary>
        private static readonly Relation RotPower =
            new Relation("process.power.rot", T("Shaft power from speed and torque", "Wellenleistung aus Drehzahl und Drehmoment"), "{P} = 2π·{n}·{T}", "P", "n", "T") { Source = "S18" }
                .Solve("P", "2π·{n}·{T}", v => v("n") * v("T"))
                .Solve("T", "{P}/(2π·{n})", v => v("P") / v("n"))
                .Solve("n", "{P}/(2π·{T})", v => v("P") / v("T"));

        /// <summary>Φ = ṁ·c·(θ_in − θ_out) for the stream that gives off heat (sign: positive = cooled) [S29].</summary>
        private static Relation Stream(string id, I18nText name, string m, string c, string hot, string cold) =>
            new Relation(id, name, "{Phi} = {" + m + "}·{" + c + "}·({" + hot + "} − {" + cold + "})", "Phi", m, c, hot, cold) { Source = "S29" }
                .Solve("Phi", "{" + m + "}·{" + c + "}·({" + hot + "} − {" + cold + "})", v => v(m) * v(c) * (v(hot) - v(cold)))
                .Solve(m, "{Phi}/({" + c + "}·({" + hot + "} − {" + cold + "}))", v => v("Phi") / (v(c) * (v(hot) - v(cold))))
                .Solve(c, "{Phi}/({" + m + "}·({" + hot + "} − {" + cold + "}))", v => v("Phi") / (v(m) * (v(hot) - v(cold))))
                .Solve(hot, "{" + cold + "} + {Phi}/({" + m + "}·{" + c + "})", v => v(cold) + v("Phi") / (v(m) * v(c)))
                .Solve(cold, "{" + hot + "} − {Phi}/({" + m + "}·{" + c + "})", v => v(hot) - v("Phi") / (v(m) * v(c)));

        /// <summary>Logarithmic mean of two temperature differences of the same sign (arithmetic when they are equal); NaN otherwise.</summary>
        internal static double Lmtd(double a, double b)
        {
            if (a * b <= 0) return double.NaN;
            if (Math.Abs(a - b) <= 1e-9 * Math.Max(Math.Abs(a), Math.Abs(b))) return (a + b) / 2;
            return (a - b) / Math.Log(a / b);
        }

        /// <summary>Counter-flow LMTD: Δθ_a = θ₁,in − θ₂,out, Δθ_b = θ₁,out − θ₂,in [S29].</summary>
        private static readonly Relation LmtdRel =
            new Relation("process.hx.lmtd", T("Log mean temperature difference (counter-flow)", "Mittlere logarithmische Temperaturdifferenz (Gegenstrom)"),
                "{dTlm} = (({th1in} − {th2out}) − ({th1out} − {th2in}))/ln(({th1in} − {th2out})/({th1out} − {th2in}))", "dTlm", "th1in", "th1out", "th2in", "th2out")
            {
                Source = "S29",
                Validity = T("counter-flow; other layouts need a correction factor", "Gegenstrom; andere Bauarten brauchen einen Korrekturfaktor"),
            }
                .Solve("dTlm", "(({th1in} − {th2out}) − ({th1out} − {th2in}))/ln(({th1in} − {th2out})/({th1out} − {th2in}))",
                    v => Lmtd(v("th1in") - v("th2out"), v("th1out") - v("th2in")));

        /// <summary>Φ = U·A·Δθ_lm [S29].</summary>
        private static readonly Relation Transfer =
            new Relation("process.hx.transfer", T("Heat transfer", "Wärmedurchgang"), "{Phi} = {U}·{A}·{dTlm}", "Phi", "U", "A", "dTlm") { Source = "S29" }
                .Solve("Phi", "{U}·{A}·{dTlm}", v => v("U") * v("A") * v("dTlm"))
                .Solve("U", "{Phi}/({A}·{dTlm})", v => v("Phi") / (v("A") * v("dTlm")))
                .Solve("A", "{Phi}/({U}·{dTlm})", v => v("Phi") / (v("U") * v("dTlm")))
                .Solve("dTlm", "{Phi}/({U}·{A})", v => v("Phi") / (v("U") * v("A")));

        /// <summary>Cylinder volume V = π·D²·h/4 (heads not counted) [S29].</summary>
        private static readonly Relation VesselVolume =
            new Relation("process.vessel.volume", T("Volume (cylindrical shell)", "Volumen (zylindrischer Mantel)"), "{V} = π·{D}²·{h}/4", "V", "D", "h")
            {
                Source = "S29",
                Validity = T("cylindrical part only, heads not counted", "nur zylindrischer Teil, Böden nicht mitgerechnet"),
            }
                .Solve("V", "π·{D}²·{h}/4", v => Math.PI * v("D") * v("D") * v("h") / 4)
                .Solve("h", "4·{V}/(π·{D}²)", v => 4 * v("V") / (Math.PI * v("D") * v("D")))
                .Solve("D", "√(4·{V}/(π·{h}))", v => Math.Sqrt(4 * v("V") / (Math.PI * v("h"))));

        /// <summary>Stored heat Q = ρ·V·c·Δθ [S29].</summary>
        private static readonly Relation StoredHeat =
            new Relation("process.vessel.heat", T("Stored heat", "Gespeicherte Wärme"), "{Q} = {rho}·{V}·{c}·{dT}", "Q", "rho", "V", "c", "dT") { Source = "S29" }
                .Solve("Q", "{rho}·{V}·{c}·{dT}", v => v("rho") * v("V") * v("c") * v("dT"))
                .Solve("V", "{Q}/({rho}·{c}·{dT})", v => v("Q") / (v("rho") * v("c") * v("dT")))
                .Solve("dT", "{Q}/({rho}·{V}·{c})", v => v("Q") / (v("rho") * v("V") * v("c")))
                .Solve("rho", "{Q}/({V}·{c}·{dT})", v => v("Q") / (v("V") * v("c") * v("dT")))
                .Solve("c", "{Q}/({rho}·{V}·{dT})", v => v("Q") / (v("rho") * v("V") * v("dT")));

        /// <summary>ṁ = ρ·V̇ [S29].</summary>
        private static readonly Relation MassRel =
            new Relation("process.line.mass", T("Mass flow", "Massenstrom"), "{m} = {rho}·{Vdot}", "m", "rho", "Vdot") { Source = "S29" }
                .Solve("m", "{rho}·{Vdot}", v => v("rho") * v("Vdot"))
                .Solve("Vdot", "{m}/{rho}", v => v("m") / v("rho"))
                .Solve("rho", "{m}/{Vdot}", v => v("m") / v("Vdot"));

        /// <summary>v = 4·V̇/(π·d_i²) [S27].</summary>
        private static readonly Relation VelocityRel =
            new Relation("process.line.velocity", T("Flow velocity", "Strömungsgeschwindigkeit"), "{v} = 4·{Vdot}/(π·{di}²)", "v", "Vdot", "di") { Source = "S27" }
                .Solve("v", "4·{Vdot}/(π·{di}²)", v => 4 * v("Vdot") / (Math.PI * v("di") * v("di")))
                .Solve("Vdot", "π·{di}²·{v}/4", v => Math.PI * v("di") * v("di") * v("v") / 4)
                .Solve("di", "√(4·{Vdot}/(π·{v}))", v => Math.Sqrt(4 * v("Vdot") / (Math.PI * v("v"))));

        /// <summary>P_el = η·P: a generator turns shaft power into electrical power [S15].</summary>
        private static readonly Relation GeneratorEfficiency =
            new Relation("process.generator.efficiency", T("Generator efficiency", "Generatorwirkungsgrad"), "{Pel} = {eta}·{P}", "Pel", "eta", "P")
            {
                Source = "S15",
                Validity = T("generator operation (mechanical power in, electrical power out)", "Generatorbetrieb (mechanische Leistung hinein, elektrische heraus)"),
            }
                .Solve("Pel", "{eta}·{P}", v => v("eta") * v("P"))
                .Solve("P", "{Pel}/{eta}", v => v("Pel") / v("eta"))
                .Solve("eta", "{Pel}/{P}", v => v("Pel") / v("P"));

        /// <summary>P = η·P_el: a motor turns electrical power into shaft power [S15].</summary>
        private static readonly Relation MotorEfficiency =
            new Relation("process.motor.efficiency", T("Motor efficiency", "Motorwirkungsgrad"), "{P} = {eta}·{Pel}", "P", "eta", "Pel")
            {
                Source = "S15",
                Validity = T("motor operation (electrical power in, mechanical power out)", "Motorbetrieb (elektrische Leistung hinein, mechanische heraus)"),
            }
                .Solve("P", "{eta}·{Pel}", v => v("eta") * v("Pel"))
                .Solve("Pel", "{P}/{eta}", v => v("P") / v("eta"))
                .Solve("eta", "{P}/{Pel}", v => v("P") / v("Pel"));

        // ------------------------------------------------------------------ gas machines (compressor, turbine)

        /// <summary>
        /// An ideal gas with constant properties: isentropic exponent κ = c_p/c_v and specific gas constant R = R_m/M, values at
        /// about 300 K and low pressure from the ideal-gas property tables of the thermodynamics textbooks (module docs).
        /// </summary>
        internal sealed record Gas(string Id, I18nText Name, double Kappa, double R);

        /// <summary>The gases of the choice (R in J/(kg·K)); "custom" = no gas: κ and R are entered.</summary>
        internal static readonly IReadOnlyList<Gas> Gases = new[]
        {
            new Gas("air", T("Air", "Luft"), 1.400, 287.0),
            new Gas("nitrogen", T("Nitrogen (N₂)", "Stickstoff (N₂)"), 1.400, 296.8),
            new Gas("oxygen", T("Oxygen (O₂)", "Sauerstoff (O₂)"), 1.395, 259.8),
            new Gas("co2", T("Carbon dioxide (CO₂)", "Kohlenstoffdioxid (CO₂)"), 1.289, 188.9),
            new Gas("methane", T("Methane (CH₄, ≈ natural gas)", "Methan (CH₄, ≈ Erdgas)"), 1.299, 518.2),
            new Gas("hydrogen", T("Hydrogen (H₂)", "Wasserstoff (H₂)"), 1.405, 4124.0),
            new Gas("helium", T("Helium (He)", "Helium (He)"), 1.667, 2076.9),
            new Gas("argon", T("Argon (Ar)", "Argon (Ar)"), 1.667, 208.1),
            new Gas("steam", T("Steam (superheated, as ideal gas)", "Wasserdampf (überhitzt, als ideales Gas)"), 1.327, 461.5),
        };

        public const string GasKey = "gas";
        public const string CustomGas = "custom";

        /// <summary>The gas choice of compressors and turbines (air by default; "other" leaves κ and R to the user).</summary>
        internal static readonly ParamDef GasParam = new ParamDef(GasKey, T("Gas", "Gas"), ParamKind.Choice)
        {
            Default = "air",
            Options = Gases.Select(g => new ParamOption(g.Id, g.Name))
                .Append(new ParamOption(CustomGas, T("Other gas (enter κ and R)", "Anderes Gas (κ und R eingeben)"))).ToList(),
            Advanced = true,
        };

        /// <summary>The selected gas, or null for "other".</summary>
        internal static Gas? GasOf(SketchObject o)
        {
            var id = o.Str(GasKey, "air");
            return Gases.FirstOrDefault(g => g.Id == id) ?? (id == CustomGas ? null : Gases[0]);
        }

        private static readonly Dictionary<string, Relation> GasRelations = new(StringComparer.Ordinal);

        /// <summary>
        /// The property values of the selected gas as markings (relations without inputs): κ and R, unless the user entered
        /// them (an entered value wins over the table, no conflict) or chose "other".
        /// </summary>
        private static IEnumerable<Relation> GasProperties(SketchObject o)
        {
            if (GasOf(o) is not { } gas) yield break;
            if (!Entered(o, "kappa")) yield return GasRelation(gas, "kappa", gas.Kappa, gas.Kappa.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture));
            if (!Entered(o, "gasConstant")) yield return GasRelation(gas, "R", gas.R, gas.R.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " J/(kg·K)");
        }

        private static bool Entered(SketchObject o, string param)
            => o.Params.TryGetValue(param, out var p) && (p.Number.HasValue || !string.IsNullOrWhiteSpace(p.Text));

        private static Relation GasRelation(Gas gas, string key, double value, string text)
        {
            lock (GasRelations)
            {
                var id = gas.Id + "." + key;
                if (!GasRelations.TryGetValue(id, out var rel))
                    GasRelations[id] = rel = new Relation("process.gas." + key, T("Gas property: " + gas.Name.En, "Stoffwert: " + gas.Name.De), "{" + key + "} = " + text, key)
                    {
                        Source = "S40",
                        Validity = T("ideal gas, about 300 K, low pressure", "ideales Gas, etwa 300 K, niedriger Druck"),
                    }.Solve(key, text, _ => value);
                return rel;
            }
        }

        /// <summary>NaN for an infinite or undefined result (a pressure ratio of 1 in a logarithm, an efficiency of 1 in a divisor).</summary>
        private static double Fin(double x) => double.IsNaN(x) || double.IsInfinity(x) ? double.NaN : x;

        /// <summary>c_p = κ·R/(κ − 1) [S40].</summary>
        private static readonly Relation GasCp =
            new Relation("process.gas.cp", T("Specific heat capacity of an ideal gas", "Spezifische Wärmekapazität eines idealen Gases"), "{cp} = {kappa}·{R}/({kappa} − 1)", "cp", "kappa", "R") { Source = "S40" }
                .Solve("cp", "{kappa}·{R}/({kappa} − 1)", v => Fin(v("kappa") * v("R") / (v("kappa") - 1)))
                .Solve("R", "{cp}·({kappa} − 1)/{kappa}", v => v("cp") * (v("kappa") - 1) / v("kappa"))
                .Solve("kappa", "{cp}/({cp} − {R})", v => Fin(v("cp") / (v("cp") - v("R"))));

        /// <summary>Compressor p₂ = π·p₁; turbine p₁ = π·p₂ (π ≥ 1 for both) [S40].</summary>
        private static Relation PressureRatio(bool turbine)
        {
            var (hi, lo) = turbine ? ("p1", "p2") : ("p2", "p1");
            return new Relation(turbine ? "process.gas.ratio.expansion" : "process.gas.ratio",
                    turbine ? T("Pressure ratio (expansion)", "Druckverhältnis (Entspannung)") : T("Pressure ratio (compression)", "Druckverhältnis (Verdichtung)"),
                    "{" + hi + "} = {pi}·{" + lo + "}", hi, "pi", lo) { Source = "S40" }
                .Solve(hi, "{pi}·{" + lo + "}", v => v("pi") * v(lo))
                .Solve(lo, "{" + hi + "}/{pi}", v => v(hi) / v("pi"))
                .Solve("pi", "{" + hi + "}/{" + lo + "}", v => v(hi) / v(lo));
        }

        /// <summary>
        /// Isentropic outlet temperature: compression T₂s = T₁·π^((κ−1)/κ), expansion T₂s = T₁·π^(−(κ−1)/κ) (absolute
        /// temperatures) [S40].
        /// </summary>
        private static Relation Isentropic(bool turbine)
        {
            var sign = turbine ? -1.0 : 1.0;
            var exp = turbine ? "^(−({kappa} − 1)/{kappa})" : "^(({kappa} − 1)/{kappa})";
            double X(Func<string, double> v) => (v("kappa") - 1) / v("kappa");
            return new Relation(turbine ? "process.gas.isentropic.expansion" : "process.gas.isentropic",
                    turbine ? T("Isentropic expansion", "Isentrope Entspannung") : T("Isentropic compression", "Isentrope Verdichtung"),
                    "{th2s} = {th1}·{pi}" + exp, "th2s", "th1", "pi", "kappa")
                {
                    Source = "S40",
                    Validity = T("ideal gas, constant κ; absolute temperatures", "ideales Gas, konstantes κ; absolute Temperaturen"),
                }
                .Solve("th2s", "{th1}·{pi}" + exp, v => v("th1") * Math.Pow(v("pi"), sign * X(v)))
                .Solve("th1", "{th2s}·{pi}" + (turbine ? "^(({kappa} − 1)/{kappa})" : "^(−({kappa} − 1)/{kappa})"), v => v("th2s") * Math.Pow(v("pi"), -sign * X(v)))
                .Solve("pi", turbine ? "({th1}/{th2s})^({kappa}/({kappa} − 1))" : "({th2s}/{th1})^({kappa}/({kappa} − 1))",
                    v => Fin(Math.Pow(Math.Pow(v("th2s") / v("th1"), sign), 1 / X(v))))
                .Solve("kappa", turbine ? "1/(1 − ln({th1}/{th2s})/ln {pi})" : "1/(1 − ln({th2s}/{th1})/ln {pi})",
                    v => Fin(1 / (1 - sign * Math.Log(v("th2s") / v("th1")) / Math.Log(v("pi")))));
        }

        /// <summary>
        /// Isentropic efficiency: compression η_s = (T₂s − T₁)/(T₂ − T₁), expansion η_s = (T₁ − T₂)/(T₁ − T₂s) [S40].
        /// </summary>
        private static Relation IsentropicEfficiency(bool turbine) => turbine
            ? new Relation("process.gas.efficiency.expansion", T("Isentropic efficiency (expansion)", "Isentroper Wirkungsgrad (Entspannung)"),
                    "{eta} = ({th1} − {th2})/({th1} − {th2s})", "eta", "th1", "th2", "th2s") { Source = "S40" }
                .Solve("eta", "({th1} − {th2})/({th1} − {th2s})", v => Fin((v("th1") - v("th2")) / (v("th1") - v("th2s"))))
                .Solve("th2", "{th1} − {eta}·({th1} − {th2s})", v => v("th1") - v("eta") * (v("th1") - v("th2s")))
                .Solve("th2s", "{th1} − ({th1} − {th2})/{eta}", v => Fin(v("th1") - (v("th1") - v("th2")) / v("eta")))
                .Solve("th1", "({th2} − {eta}·{th2s})/(1 − {eta})", v => Fin((v("th2") - v("eta") * v("th2s")) / (1 - v("eta"))))
            : new Relation("process.gas.efficiency", T("Isentropic efficiency (compression)", "Isentroper Wirkungsgrad (Verdichtung)"),
                    "{eta} = ({th2s} − {th1})/({th2} − {th1})", "eta", "th1", "th2", "th2s") { Source = "S40" }
                .Solve("eta", "({th2s} − {th1})/({th2} − {th1})", v => Fin((v("th2s") - v("th1")) / (v("th2") - v("th1"))))
                .Solve("th2", "{th1} + ({th2s} − {th1})/{eta}", v => Fin(v("th1") + (v("th2s") - v("th1")) / v("eta")))
                .Solve("th2s", "{th1} + {eta}·({th2} − {th1})", v => v("th1") + v("eta") * (v("th2") - v("th1")))
                .Solve("th1", "({th2s} − {eta}·{th2})/(1 − {eta})", v => Fin((v("th2s") - v("eta") * v("th2")) / (1 - v("eta"))));

        /// <summary>Temperature rise of a compressor ΔT = T₂ − T₁, drop of a turbine ΔT = T₁ − T₂ [S40].</summary>
        private static Relation GasDeltaT(bool turbine)
        {
            var (a, b) = turbine ? ("th1", "th2") : ("th2", "th1");
            return new Relation(turbine ? "process.gas.drop" : "process.gas.rise",
                    turbine ? T("Temperature drop", "Temperaturabsenkung") : T("Temperature rise", "Temperaturerhöhung"),
                    "{dT} = {" + a + "} − {" + b + "}", "dT", a, b) { Source = "S40" }
                .Solve("dT", "{" + a + "} − {" + b + "}", v => v(a) - v(b))
                .Solve(a, "{" + b + "} + {dT}", v => v(b) + v("dT"))
                .Solve(b, "{" + a + "} − {dT}", v => v(a) - v("dT"));
        }

        /// <summary>
        /// Power of the gas stream P = ṁ·c_p·ΔT: the compressor's drive power, the turbine's output (its expansion work per
        /// second); mechanical losses not counted [S40].
        /// </summary>
        private static Relation GasPower(bool turbine) =>
            new Relation(turbine ? "process.gas.power.expansion" : "process.gas.power",
                    turbine ? T("Expansion work (power)", "Expansionsarbeit (Leistung)") : T("Compression power", "Verdichtungsleistung"),
                    "{P} = {m}·{cp}·{dT}", "P", "m", "cp", "dT")
                {
                    Source = "S40",
                    Validity = T("ideal gas, constant c_p, adiabatic machine, mechanical losses not counted", "ideales Gas, konstantes c_p, adiabate Maschine, mechanische Verluste nicht gerechnet"),
                }
                .Solve("P", "{m}·{cp}·{dT}", v => v("m") * v("cp") * v("dT"))
                .Solve("m", "{P}/({cp}·{dT})", v => Fin(v("P") / (v("cp") * v("dT"))))
                .Solve("dT", "{P}/({m}·{cp})", v => Fin(v("P") / (v("m") * v("cp"))))
                .Solve("cp", "{P}/({m}·{dT})", v => Fin(v("P") / (v("m") * v("dT"))));

        /// <summary>
        /// Specific work w = c_p·ΔT (J/kg, Core <see cref="Quantities.SpecificEnergy"/>): the work per kilogram of gas the
        /// compressor takes in, the turbine gives out (ΔT is the rise, respectively the drop); P = ṁ·w follows through ΔT [S40].
        /// </summary>
        private static Relation GasWork(bool turbine) =>
            new Relation(turbine ? "process.gas.work.expansion" : "process.gas.work",
                    turbine ? T("Specific expansion work", "Spezifische Expansionsarbeit") : T("Specific compression work", "Spezifische Verdichtungsarbeit"),
                    "{w} = {cp}·{dT}", "w", "cp", "dT")
                {
                    Source = "S40",
                    Validity = T("ideal gas, constant c_p, adiabatic machine", "ideales Gas, konstantes c_p, adiabate Maschine"),
                }
                .Solve("w", "{cp}·{dT}", v => v("cp") * v("dT"))
                .Solve("dT", "{w}/{cp}", v => Fin(v("w") / v("cp")))
                .Solve("cp", "{w}/{dT}", v => Fin(v("w") / v("dT")));

        /// <summary>Isentropic (ideal) power: compression P_s = ṁ·c_p·(T₂s − T₁), expansion P_s = ṁ·c_p·(T₁ − T₂s) [S40].</summary>
        private static Relation GasIsentropicPower(bool turbine)
        {
            var (a, b) = turbine ? ("th1", "th2s") : ("th2s", "th1");
            var d = "({" + a + "} − {" + b + "})";
            return new Relation(turbine ? "process.gas.power.isentropic.expansion" : "process.gas.power.isentropic",
                    T("Isentropic power", "Isentrope Leistung"), "{Ps} = {m}·{cp}·" + d, "Ps", "m", "cp", a, b) { Source = "S40" }
                .Solve("Ps", "{m}·{cp}·" + d, v => v("m") * v("cp") * (v(a) - v(b)))
                .Solve("m", "{Ps}/({cp}·" + d + ")", v => Fin(v("Ps") / (v("cp") * (v(a) - v(b)))))
                .Solve(a, "{" + b + "} + {Ps}/({m}·{cp})", v => Fin(v(b) + v("Ps") / (v("m") * v("cp"))))
                .Solve(b, "{" + a + "} − {Ps}/({m}·{cp})", v => Fin(v(a) - v("Ps") / (v("m") * v("cp"))));
        }

        /// <summary>Volume flow of an ideal gas V̇ = ṁ·R·T/p (inlet: V1, T1, p1; outlet: V2, T2, p2) [S40].</summary>
        private static Relation GasVolumeFlow(string vk, string tk, string pk, I18nText name) =>
            new Relation("process.gas.flow." + vk, name, "{" + vk + "} = {m}·{R}·{" + tk + "}/{" + pk + "}", vk, "m", "R", tk, pk)
                {
                    Source = "S40",
                    Validity = T("ideal gas, absolute pressure and temperature", "ideales Gas, absoluter Druck und absolute Temperatur"),
                }
                .Solve(vk, "{m}·{R}·{" + tk + "}/{" + pk + "}", v => Fin(v("m") * v("R") * v(tk) / v(pk)))
                .Solve("m", "{" + vk + "}·{" + pk + "}/({R}·{" + tk + "})", v => Fin(v(vk) * v(pk) / (v("R") * v(tk))))
                .Solve(pk, "{m}·{R}·{" + tk + "}/{" + vk + "}", v => Fin(v("m") * v("R") * v(tk) / v(vk)))
                .Solve(tk, "{" + vk + "}·{" + pk + "}/({m}·{R})", v => Fin(v(vk) * v(pk) / (v("m") * v("R"))));

        // ------------------------------------------------------------------ specs per type

        private static readonly Dictionary<string, Spec> Specs = new(StringComparer.Ordinal);

        /// <summary>The values of the type <paramref name="typeId"/> (empty for symbols without relations; cached).</summary>
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
            "process.pump.centrifugal" => CentrifugalPump(),
            "process.pump.displacement" => Machine(("S", "D")),
            "process.fan" => Machine(("I", "O")),
            "process.compressor" => GasMachine(false),
            "process.turbine" => GasMachine(true),
            "process.motor" => Motor(),
            "process.generator" => Generator(),
            "process.hx.shelltube" => Exchanger(("T1", "T2"), ("S1", "S2")),
            "process.hx.plate" => Exchanger(("A1", "A2"), ("B1", "B2")),
            "process.hx.general" => Exchanger(("P1", "P2"), ("S1", "S2")),
            "process.hx.aircooler" => Cooler(("P1", "P2")),
            "process.hx.coolingtower" => Cooler(("W1", "W2")),
            "process.vessel.vertical" or "process.vessel.horizontal" or "process.vessel.tank" or "process.reactor" or "process.column" => Vessel(),
            ProcessLineType.TypeId => Line(),
            _ => Spec.Empty,
        };

        private static Func<SketchObject, string, IEnumerable<PortVar>> FlowPorts(params string[] ports)
            => (_, port) => ports.Contains(port) ? new[] { new PortVar("Vdot", "Vdot") } : Array.Empty<PortVar>();

        private static Spec CentrifugalPump()
        {
            var vars = new[]
            {
                VolFlow(), Len("H", T("Head", "Förderhöhe"), "H", "head", VarRelevance.Card), PowerVar("P", T("Shaft power", "Wellenleistung"), "P", "power", VarRelevance.Card),
                Eta() with { Relevance = VarRelevance.Card }, Dp(VarRelevance.Panel),
                PowerVar("Phyd", T("Hydraulic power", "Hydraulische Leistung"), "P_hyd", "powerHyd", VarRelevance.Panel),
                Speed(VarRelevance.Panel), Torque(VarRelevance.Panel), Rho(),
            };
            return new Spec(vars, new[] { HeadRel, HydPower, ShaftPower, RotPower }, Params(vars), new[] { EfficiencyRange() }) { Ports = FlowPorts("S", "D") };
        }

        /// <summary>The drive motor: shaft power P = 2π·n·T and, optionally, its electrical input P = η·P_el.</summary>
        private static Spec Motor()
        {
            var vars = new[]
            {
                PowerVar("P", T("Shaft power (output)", "Wellenleistung (Abgabe)"), "P", "power", VarRelevance.Card), Speed(VarRelevance.Card), Torque(VarRelevance.Card),
                Eta() with { Name = T("Motor efficiency", "Motorwirkungsgrad") },
                PowerVar("Pel", T("Electrical power (input)", "Elektrische Leistung (Aufnahme)"), "P_el", "powerEl", VarRelevance.Panel),
            };
            return new Spec(vars, new[] { RotPower, MotorEfficiency }, Params(vars), new[] { EfficiencyRange() });
        }

        /// <summary>
        /// The generator driven by a turbine (or any drive): shaft power P = 2π·n·T and the electrical output P_el = η·P. Its
        /// terminal <c>el</c> gives P_el to an energy cable (join across modules, key P).
        /// </summary>
        private static Spec Generator()
        {
            var vars = new[]
            {
                PowerVar("Pel", T("Electrical power (output)", "Elektrische Leistung (Abgabe)"), "P_el", "powerEl", VarRelevance.Card),
                PowerVar("P", T("Shaft power (input)", "Wellenleistung (Aufnahme)"), "P", "power", VarRelevance.Card),
                Eta() with { Name = T("Generator efficiency", "Generatorwirkungsgrad"), Relevance = VarRelevance.Card },
                Speed(VarRelevance.Card), Torque(VarRelevance.Panel),
            };
            return new Spec(vars, new[] { RotPower, GeneratorEfficiency }, Params(vars), new[] { EfficiencyRange() })
            {
                Ports = (_, port) => port == "el" ? new[] { new PortVar("P", "Pel") } : Array.Empty<PortVar>(),
            };
        }

        /// <summary>
        /// Compressor (<paramref name="turbine"/> false) or turbine / expander: gas values on top of the shaft values. Nothing
        /// is required; air and, for the compressor, ambient suction (20 °C, 1.01325 bar) are defaults that give way.
        /// </summary>
        private static Spec GasMachine(bool turbine)
        {
            var inlet = turbine ? "I" : "S";
            var outlet = turbine ? "O" : "D";
            var th1 = Temp("th1", T("Inlet temperature", "Eintrittstemperatur"), "θ₁", "tempIn", turbine ? VarRelevance.Card : VarRelevance.Panel) with
            {
                Symbol = T("θ₁", "θ₁"), Default = turbine ? (double?)null : 293.15,
            };
            var th2 = Temp("th2", T("Outlet temperature", "Austrittstemperatur"), "θ₂", "tempOut", turbine ? VarRelevance.Panel : VarRelevance.Card);
            var vars = new[]
            {
                PowerVar("P", turbine ? T("Shaft power (output)", "Wellenleistung (Abgabe)") : T("Shaft power (drive)", "Wellenleistung (Antrieb)"), "P", "power", VarRelevance.Card),
                V("m", T("Mass flow", "Massenstrom"), T("ṁ", "ṁ"), Quantities.MassFlow, "kg/s", "massFlow", VarRelevance.Card) with { Min = 0, Max = 1e5 },
                V("pi", turbine ? T("Pressure ratio p₁/p₂", "Druckverhältnis p₁/p₂") : T("Pressure ratio p₂/p₁", "Druckverhältnis p₂/p₁"), T("π", "π"),
                    Quantities.Ratio, null, "pressureRatio", VarRelevance.Card) with { Min = 1, Max = 1e3 },
                turbine ? th1 : th2,
                turbine ? th2 : th1,
                V("p1", T("Inlet pressure (absolute)", "Eintrittsdruck (absolut)"), T("p₁", "p₁"), Quantities.Pressure, "bar", "pressureIn", VarRelevance.Panel)
                    with { Min = 1, Max = 1e9, Default = turbine ? (double?)null : 101325 },
                V("p2", T("Outlet pressure (absolute)", "Austrittsdruck (absolut)"), T("p₂", "p₂"), Quantities.Pressure, "bar", "pressureOut", VarRelevance.Panel) with { Min = 1, Max = 1e9 },
                V("eta", T("Isentropic efficiency", "Isentroper Wirkungsgrad"), T("η_s", "η_s"), Quantities.Efficiency, "%", "efficiency", VarRelevance.Panel) with { Min = 0, Max = 1 },
                V("dT", turbine ? T("Temperature drop", "Temperaturabsenkung") : T("Temperature rise", "Temperaturerhöhung"), T("ΔT", "ΔT"),
                    Quantities.TemperatureDifference, "K", "deltaT", VarRelevance.Panel) with { Min = -2000, Max = 2000 },
                V("w", turbine ? T("Specific work (expansion)", "Spezifische Arbeit (Entspannung)") : T("Specific work (compression)", "Spezifische Arbeit (Verdichtung)"), T("w", "w"),
                    Quantities.SpecificEnergy, "kJ/kg", "specificWork", VarRelevance.Panel) with { Min = -1e8, Max = 1e8 },
                V("V1", T("Inlet volume flow", "Ansaug-/Eintrittsvolumenstrom"), T("V̇₁", "V̇₁"), Quantities.VolumeFlow, "m³/h", "flowIn", VarRelevance.Panel) with { Min = 0, Max = 1e5 },
                Temp("thmax", turbine ? T("Permissible inlet temperature", "Zulässige Eintrittstemperatur") : T("Permissible outlet temperature", "Zulässige Austrittstemperatur"),
                    "θ_max", "tempMax", VarRelevance.Panel),
                Speed(VarRelevance.Panel), Torque(VarRelevance.Panel),
                Temp("th2s", T("Isentropic outlet temperature", "Isentrope Austrittstemperatur"), "θ₂s", "tempOutIs", VarRelevance.Engineer),
                PowerVar("Ps", T("Isentropic power", "Isentrope Leistung"), "P_s", "powerIs", VarRelevance.Engineer),
                V("V2", T("Outlet volume flow", "Austrittsvolumenstrom"), T("V̇₂", "V̇₂"), Quantities.VolumeFlow, "m³/h", "flowOut", VarRelevance.Engineer) with { Min = 0, Max = 1e5 },
                V("kappa", T("Isentropic exponent", "Isentropenexponent"), T("κ", "κ"), Quantities.Ratio, null, "kappa", VarRelevance.Engineer) with { Min = 1, Max = 2 },
                V("R", T("Specific gas constant", "Spezifische Gaskonstante"), T("R", "R_s"), Quantities.SpecificHeat, "J/(kg·K)", "gasConstant", VarRelevance.Engineer) with { Min = 1, Max = 1e5 },
                V("cp", T("Specific heat capacity (constant pressure)", "Spezifische Wärmekapazität (isobar)"), T("c_p", "c_p"), Quantities.SpecificHeat, "kJ/(kg·K)", "specificHeat", VarRelevance.Engineer) with { Min = 1, Max = 1e5 },
            };
            var rels = new[]
            {
                PressureRatio(turbine), GasCp, Isentropic(turbine), IsentropicEfficiency(turbine), GasDeltaT(turbine), GasPower(turbine),
                GasWork(turbine), GasIsentropicPower(turbine),
                GasVolumeFlow("V1", "th1", "p1", T("Inlet volume flow", "Ansaug-/Eintrittsvolumenstrom")),
                GasVolumeFlow("V2", "th2", "p2", T("Outlet volume flow", "Austrittsvolumenstrom")),
                RotPower,
            };
            var ps = vars.Select(v => Text(v.ParamKey, v.Name, v.Unit)).Append(GasParam).Append(ValueLabels.ShowValues).ToArray();
            return new Spec(vars, rels, ps, new[] { EfficiencyRange(), RatioBelowOne(turbine), TemperatureLimit(turbine), KappaRange() })
            {
                Ports = (_, port) =>
                    port == inlet ? new[] { new PortVar("m", "m"), new PortVar("theta", "th1"), new PortVar("p", "p1") }
                    : port == outlet ? new[] { new PortVar("m", "m"), new PortVar("theta", "th2"), new PortVar("p", "p2") }
                    : Array.Empty<PortVar>(),
                Local = GasProperties,
            };
        }

        private static Spec Machine((string In, string Out) ports)
        {
            var vars = new[]
            {
                VolFlow(), Dp(VarRelevance.Card), PowerVar("P", T("Shaft power", "Wellenleistung"), "P", "power", VarRelevance.Card),
                Eta() with { Relevance = VarRelevance.Card },
                PowerVar("Phyd", T("Hydraulic power", "Hydraulische Leistung"), "P_hyd", "powerHyd", VarRelevance.Panel),
                Speed(VarRelevance.Panel), Torque(VarRelevance.Panel),
            };
            return new Spec(vars, new[] { HydPower, ShaftPower, RotPower }, Params(vars), new[] { EfficiencyRange() }) { Ports = FlowPorts(ports.In, ports.Out) };
        }

        private static Spec Exchanger((string In, string Out) side1, (string In, string Out) side2)
        {
            var vars = new[]
            {
                Duty(), V("U", T("Heat transfer coefficient", "Wärmedurchgangskoeffizient"), T("U", "k"), Quantities.HeatTransfer, "W/(m²·K)", "u") with { Min = 1e-3, Max = 1e5 },
                V("A", T("Heat transfer area", "Wärmeübertragungsfläche"), T("A", "A"), Quantities.Area, "m²", "area") with { Min = 1e-6, Max = 1e6 },
                V("dTlm", T("Log mean temperature difference", "Mittlere logarithmische Temperaturdifferenz"), T("Δθ_lm", "Δθ_m"), Quantities.TemperatureDifference, "K", "lmtd") with { Min = -2000, Max = 2000 },
                MassFlow("m1", T("Mass flow, side 1", "Massenstrom, Seite 1"), "ṁ₁", "massFlow1", VarRelevance.Panel),
                Temp("th1in", T("Inlet temperature, side 1", "Eintrittstemperatur, Seite 1"), "θ₁,ein", "temp1In", VarRelevance.Panel) with { Symbol = T("θ₁,in", "θ₁,ein") },
                Temp("th1out", T("Outlet temperature, side 1", "Austrittstemperatur, Seite 1"), "θ₁,aus", "temp1Out", VarRelevance.Panel) with { Symbol = T("θ₁,out", "θ₁,aus") },
                MassFlow("m2", T("Mass flow, side 2", "Massenstrom, Seite 2"), "ṁ₂", "massFlow2", VarRelevance.Panel),
                Temp("th2in", T("Inlet temperature, side 2", "Eintrittstemperatur, Seite 2"), "θ₂,ein", "temp2In", VarRelevance.Panel) with { Symbol = T("θ₂,in", "θ₂,ein") },
                Temp("th2out", T("Outlet temperature, side 2", "Austrittstemperatur, Seite 2"), "θ₂,aus", "temp2Out", VarRelevance.Panel) with { Symbol = T("θ₂,out", "θ₂,aus") },
                Cp("c1", "c₁", "specificHeat1"), Cp("c2", "c₂", "specificHeat2"),
            };
            var rels = new[]
            {
                Stream("process.hx.side1", T("Energy balance, side 1", "Energiebilanz, Seite 1"), "m1", "c1", "th1in", "th1out"),
                Stream("process.hx.side2", T("Energy balance, side 2", "Energiebilanz, Seite 2"), "m2", "c2", "th2out", "th2in"),
                LmtdRel, Transfer,
            };
            return new Spec(vars, rels, Params(vars), new[] { Cross() })
            {
                Ports = (_, port) =>
                    port == side1.In ? new[] { new PortVar("m", "m1"), new PortVar("theta", "th1in") }
                    : port == side1.Out ? new[] { new PortVar("m", "m1"), new PortVar("theta", "th1out") }
                    : port == side2.In ? new[] { new PortVar("m", "m2"), new PortVar("theta", "th2in") }
                    : port == side2.Out ? new[] { new PortVar("m", "m2"), new PortVar("theta", "th2out") }
                    : Array.Empty<PortVar>(),
            };
        }

        private static Spec Cooler((string In, string Out) ports)
        {
            var vars = new[]
            {
                Duty(), MassFlow("m", T("Mass flow", "Massenstrom"), "ṁ", "massFlow", VarRelevance.Card),
                Temp("thin", T("Inlet temperature", "Eintrittstemperatur"), "θ_ein", "tempIn", VarRelevance.Card) with { Symbol = T("θ_in", "θ_ein") },
                Temp("thout", T("Outlet temperature", "Austrittstemperatur"), "θ_aus", "tempOut", VarRelevance.Card) with { Symbol = T("θ_out", "θ_aus") },
                Cp("c", "c", "specificHeat"),
            };
            var rels = new[] { Stream("process.cooler.balance", T("Energy balance", "Energiebilanz"), "m", "c", "thin", "thout") };
            return new Spec(vars, rels, Params(vars), Array.Empty<ValueCheck>())
            {
                Ports = (_, port) => port == ports.In ? new[] { new PortVar("m", "m"), new PortVar("theta", "thin") }
                    : port == ports.Out ? new[] { new PortVar("m", "m"), new PortVar("theta", "thout") }
                    : Array.Empty<PortVar>(),
            };
        }

        private static Spec Vessel()
        {
            var vars = new[]
            {
                V("V", T("Volume", "Volumen"), T("V", "V"), Quantities.Volume, "m³", "volume") with { Min = 1e-6, Max = 1e6 },
                Len("D", T("Diameter", "Durchmesser"), "D", "diameter", VarRelevance.Card),
                Len("h", T("Cylindrical length", "Zylindrische Länge"), "h", "length", VarRelevance.Card),
                V("Q", T("Stored heat", "Gespeicherte Wärme"), T("Q", "Q"), Quantities.Energy, "kWh", "heat") with { Min = 0, Max = 1e15 },
                V("dT", T("Temperature difference", "Temperaturdifferenz"), T("Δθ", "Δθ"), Quantities.TemperatureDifference, "K", "deltaT", VarRelevance.Panel) with { Min = -2000, Max = 2000 },
                Rho(), Cp("c", "c", "specificHeat"),
            };
            return new Spec(vars, new[] { VesselVolume, StoredHeat }, Params(vars), Array.Empty<ValueCheck>());
        }

        private static Spec Line()
        {
            var vars = new[]
            {
                VolFlow(), MassFlow("m", T("Mass flow", "Massenstrom"), "ṁ", "massFlow", VarRelevance.Card),
                V("di", T("Inner diameter", "Innendurchmesser"), T("d_i", "d_i"), Quantities.Length, "mm", "innerDiameter", VarRelevance.Panel) with { Min = 1e-4, Max = 10 },
                V("v", T("Flow velocity", "Strömungsgeschwindigkeit"), T("v", "v"), Quantities.Speed, "m/s", "velocity") with { Min = 0, Max = 100 },
                Temp("theta", T("Temperature", "Temperatur"), "θ", "temperature", VarRelevance.Card),
                Rho(),
            };
            return new Spec(vars, new[] { MassRel, VelocityRel }, Params(vars), Array.Empty<ValueCheck>());
        }

        // ------------------------------------------------------------------ net description

        /// <summary>
        /// Passages per class. The pressure class <c>p</c> passes nothing: valves, strainers and flow elements drop the pressure
        /// (not modelled), so a pressure is shared only along lines between two nozzles.
        /// </summary>
        private static IEnumerable<string[]> Through(SketchObject o, string cls) => cls == "p" ? Array.Empty<string[]>() : o.Type switch
        {
            "process.valve.manual" or "process.valve.control" or "process.valve.check" or "process.fitting.reducer" or "process.fitting.strainer"
                or "process.instrument.orifice" or "process.sightglass" => new[] { new[] { "1", "2" } },
            "process.hvac.filter" or "process.hvac.damper" => new[] { new[] { "A1", "A2" } },
            "process.pump.centrifugal" or "process.pump.displacement" when cls == "theta" => new[] { new[] { "S", "D" } },
            "process.fan" when cls == "theta" => new[] { new[] { "I", "O" } },
            _ => Array.Empty<string[]>(),
        };

        internal static readonly NetSpec Nets = new()
        {
            Prefix = "process",
            IsLine = o => o.Type == ProcessLineType.TypeId,
            PortVars = (o, port) => For(o.Type).Ports(o, port),
            LineVars = _ => new[] { new PortVar("Vdot", "Vdot"), new PortVar("m", "m"), new PortVar("theta", "theta") },
            Through = Through,
            Classes = new[] { "Vdot", "m", "theta", "p" },
            // A pressure is the same everywhere along connected lines (no pressure drop modelled); flows and temperatures are
            // through quantities.
            Rule = c => c == "p" ? NetRule.Equal : NetRule.Flow,
            Name = c => c switch
            {
                "Vdot" => T("Volume flow passed on", "Volumenstrom weitergegeben"),
                "m" => T("Mass flow passed on", "Massenstrom weitergegeben"),
                "p" => T("Pressure passed on", "Druck weitergegeben"),
                _ => T("Temperature passed on", "Temperatur weitergegeben"),
            },
        };

        public static IEnumerable<BoundRelation> Links(SketchObject o, RenderContext ctx)
        {
            if (For(o.Type).Local is { } local)
                foreach (var r in local(o)) yield return BoundRelation.Local(r, o.Id);
            foreach (var b in ValueNets.Of(ctx.Doc, Nets).For(o.Id)) yield return b;
        }

        // ------------------------------------------------------------------ checks (section 4)

        /// <summary>
        /// heat.cross: in counter-flow both end differences Δθ_a = θ₁,in − θ₂,out and Δθ_b = θ₁,out − θ₂,in must have the sign of
        /// the heat flow; otherwise the temperatures cross, which is impossible [S29].
        /// </summary>
        private static ValueCheck Cross() => new(
            "heat.cross", CheckSeverity.Warning,
            T("The temperatures cross: in counter-flow the cold outlet cannot be warmer than the hot inlet (nor the hot outlet colder than the cold inlet). Check the four temperatures.",
              "Die Temperaturen kreuzen sich: im Gegenstrom kann der kalte Austritt nicht wärmer als der warme Eintritt sein (und der warme Austritt nicht kälter als der kalte Eintritt). Die vier Temperaturen prüfen."),
            "(θ₁,in − θ₂,out)·(θ₁,out − θ₂,in) ≤ 0",
            c => c["th1in"] is double a && c["th1out"] is double b && c["th2in"] is double x && c["th2out"] is double y
                 && (a - y) * (b - x) <= 0 && !(Math.Abs(a - y) < 1e-9 && Math.Abs(b - x) < 1e-9))
        { Source = "S29" };

        /// <summary>process.efficiency: an efficiency (pumps, fans, motor, generator; isentropic of compressors and turbines) outside 0 … 1.</summary>
        private static ValueCheck EfficiencyRange() => new(
            "process.efficiency", CheckSeverity.Warning,
            T("The efficiency lies outside 0 … 100 %: no machine gives out more power than it takes in. Check the powers, temperatures or the efficiency entered.",
              "Der Wirkungsgrad liegt außerhalb von 0 … 100 %: keine Maschine gibt mehr Leistung ab, als sie aufnimmt. Leistungen, Temperaturen oder den eingegebenen Wirkungsgrad prüfen."),
            "0 < η ≤ 1",
            c => c["eta"] is double e && (e <= 0 || e > 1 + 1e-9))
        { Source = "S1" };

        /// <summary>gas.ratio: a compressor must raise the pressure, a turbine lower it (π ≥ 1 for both) [S40].</summary>
        private static ValueCheck RatioBelowOne(bool turbine) => new(
            "gas.ratio", CheckSeverity.Warning,
            turbine
                ? T("The outlet pressure is above the inlet pressure: a turbine expands the gas (pressure ratio p₁/p₂ at least 1). Check the pressures.",
                    "Der Austrittsdruck liegt über dem Eintrittsdruck: eine Turbine entspannt das Gas (Druckverhältnis p₁/p₂ mindestens 1). Die Drücke prüfen.")
                : T("The outlet pressure is below the inlet pressure: a compressor raises the pressure (pressure ratio p₂/p₁ at least 1). Check the pressures.",
                    "Der Austrittsdruck liegt unter dem Eintrittsdruck: ein Verdichter erhöht den Druck (Druckverhältnis p₂/p₁ mindestens 1). Die Drücke prüfen."),
            "π ≥ 1",
            c => c["pi"] is double x && x < 1 - 1e-9)
        { Source = "S40" };

        /// <summary>
        /// gas.temperature: the compressor's outlet temperature (the turbine's inlet temperature) above the permissible
        /// temperature θ_max the user entered (oil, seals, blades; the limit comes from the datasheet) [S40].
        /// </summary>
        private static ValueCheck TemperatureLimit(bool turbine) => new(
            "gas.temperature", CheckSeverity.Warning,
            turbine
                ? T("The inlet temperature is above the permissible temperature entered (θ_max). Lower the inlet temperature or check the limit.",
                    "Die Eintrittstemperatur liegt über der eingegebenen zulässigen Temperatur (θ_max). Eintrittstemperatur senken oder Grenzwert prüfen.")
                : T("The outlet temperature is above the permissible temperature entered (θ_max). Use a lower pressure ratio per stage (intercooling) or check the limit.",
                    "Die Austrittstemperatur liegt über der eingegebenen zulässigen Temperatur (θ_max). Kleineres Druckverhältnis je Stufe (Zwischenkühlung) oder Grenzwert prüfen."),
            turbine ? "θ₁ ≤ θ_max" : "θ₂ ≤ θ_max",
            c => c[turbine ? "th1" : "th2"] is double t && c["thmax"] is double max && t > max + 1e-9)
        { Source = "S40" };

        /// <summary>gas.kappa: the isentropic exponent of a gas is above 1 (5/3 for monatomic ideal gases at most) [S40].</summary>
        private static ValueCheck KappaRange() => new(
            "gas.kappa", CheckSeverity.Warning,
            T("The isentropic exponent κ of a gas lies between 1 and 5/3 (monatomic gases). Check κ, R or c_p.",
              "Der Isentropenexponent κ eines Gases liegt zwischen 1 und 5/3 (einatomige Gase). κ, R oder c_p prüfen."),
            "1 < κ ≤ 5/3",
            c => c["kappa"] is double k && (k <= 1 || k > 5.0 / 3 + 1e-3))
        { Source = "S40" };
    }
}
