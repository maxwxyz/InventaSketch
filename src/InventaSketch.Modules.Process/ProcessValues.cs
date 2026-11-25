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
            /// <summary>Variables at a nozzle (net classes Vdot, m, theta).</summary>
            public Func<SketchObject, string, IEnumerable<PortVar>> Ports { get; init; } = (_, _) => Array.Empty<PortVar>();
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
                PowerVar("Phyd", T("Hydraulic power", "Hydraulische Leistung"), "P_hyd", "powerHyd", VarRelevance.Panel), Rho(),
            };
            return new Spec(vars, new[] { HeadRel, HydPower, ShaftPower }, Params(vars), Array.Empty<ValueCheck>()) { Ports = FlowPorts("S", "D") };
        }

        private static Spec Machine((string In, string Out) ports)
        {
            var vars = new[]
            {
                VolFlow(), Dp(VarRelevance.Card), PowerVar("P", T("Shaft power", "Wellenleistung"), "P", "power", VarRelevance.Card),
                Eta() with { Relevance = VarRelevance.Card },
                PowerVar("Phyd", T("Hydraulic power", "Hydraulische Leistung"), "P_hyd", "powerHyd", VarRelevance.Panel),
            };
            return new Spec(vars, new[] { HydPower, ShaftPower }, Params(vars), Array.Empty<ValueCheck>()) { Ports = FlowPorts(ports.In, ports.Out) };
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

        private static IEnumerable<string[]> Through(SketchObject o, string cls) => o.Type switch
        {
            "process.valve.manual" or "process.valve.control" or "process.valve.check" or "process.fitting.reducer" or "process.fitting.strainer"
                or "process.instrument.orifice" => new[] { new[] { "1", "2" } },
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
            Classes = new[] { "Vdot", "m", "theta" },
            Rule = _ => NetRule.Flow,
            Name = c => c switch
            {
                "Vdot" => T("Volume flow passed on", "Volumenstrom weitergegeben"),
                "m" => T("Mass flow passed on", "Massenstrom weitergegeben"),
                _ => T("Temperature passed on", "Temperatur weitergegeben"),
            },
        };

        public static IEnumerable<BoundRelation> Links(SketchObject o, RenderContext ctx) => ValueNets.Of(ctx.Doc, Nets).For(o.Id);

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
    }
}
