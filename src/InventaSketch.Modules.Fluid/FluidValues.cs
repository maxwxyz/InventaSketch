using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Fluid
{
    /// <summary>
    /// Element values of the fluid power symbols (Calc 4, engineering-calculations.md sections 2.7 and 3.5): pumps and
    /// motors (displacement, speed, flow, pressure, torque, power), cylinders (areas, force and speed both ways, stroke time),
    /// rotary actuator, relief and reducing valves (set pressure), accumulator (Boyle), gauges and flow meters, and the line
    /// (velocity from flow and inner diameter). Every value is optional; speeds are in rad/s internally, so Q = V_g·n/(2π).
    /// <para>
    /// Port flows (<see cref="ValueNets"/>): lines join ports into nets. Class <c>Q</c> (volume flow) is shared when at most two
    /// elements carry it in a net; class <c>p</c> (pressure) and <c>pmax</c> (the relief setting) are shared by the whole net.
    /// Directional, non-return and flow valves, couplings, inline conditioning parts and flow meters pass the classes from
    /// port to port (a throttle passes the flow, not the pressure; a reducing valve passes the flow and sets the outlet
    /// pressure). Losses in lines and valves are not modelled.
    /// </para>
    /// </summary>
    internal static class FluidValues
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
            /// <summary>Variables at a port (net classes Q, p, pmax).</summary>
            public Func<SketchObject, string, IEnumerable<PortVar>> Ports { get; init; } = (_, _) => Array.Empty<PortVar>();
            /// <summary>Relations that depend on the element's choices (through rod, pull type).</summary>
            public Func<SketchObject, IEnumerable<Relation>>? Local { get; init; }
            /// <summary>Links to the lines docked on the element (the throttle reads flow and pressures of its inlet and outlet lines).</summary>
            public Func<SketchObject, RenderContext, IEnumerable<BoundRelation>>? Docked { get; init; }
        }

        // ------------------------------------------------------------------ parameters and variables

        private static ParamDef Text(string key, I18nText label, string? unit) => new(key, label, ParamKind.Text) { Unit = unit, Advanced = true };

        private static VarDef V(string key, I18nText name, I18nText symbol, QuantityKind kind, string? unit, string param, VarRelevance rel = VarRelevance.Card)
            => new(key, name, symbol, kind) { Unit = unit, Param = param, Relevance = rel };

        private static readonly I18nText NDisp = T("Displacement", "Verdrängungsvolumen");
        private static readonly I18nText NSpeed = T("Speed of rotation", "Drehzahl");
        private static readonly I18nText NFlow = T("Volume flow", "Volumenstrom");
        private static readonly I18nText NPressure = T("Pressure", "Druck");
        private static readonly I18nText NEtaV = T("Volumetric efficiency", "Volumetrischer Wirkungsgrad");
        private static readonly I18nText NEtaHm = T("Hydraulic-mechanical efficiency", "Hydraulisch-mechanischer Wirkungsgrad");
        private static readonly I18nText NTorque = T("Torque", "Drehmoment");
        private static readonly I18nText NHydPower = T("Hydraulic power", "Hydraulische Leistung");
        private static readonly I18nText NSetPressure = T("Set pressure", "Einstelldruck");
        private static readonly I18nText NPmax = T("Maximum pressure (relief setting)", "Maximaler Druck (Einstellung Druckbegrenzung)");

        private static VarDef Disp(VarRelevance rel = VarRelevance.Card) => V("Vg", NDisp, T("V_g", "V_g"), Quantities.Volume, "cm³", "displacement", rel) with { Min = 1e-9, Max = 1e-1 };
        private static VarDef Speed(string param = "speed", VarRelevance rel = VarRelevance.Card) => V("n", NSpeed, T("n", "n"), Quantities.RotationalSpeed, "min⁻¹", param, rel) with { Min = 0, Max = 1e4 };
        private static VarDef Flow(VarRelevance rel = VarRelevance.Card) => V("Q", NFlow, T("Q", "Q"), Quantities.VolumeFlow, "l/min", "flowRate", rel) with { Min = 0, Max = 1 };
        private static VarDef Pressure(VarRelevance rel = VarRelevance.Card) => V("p", NPressure, T("p", "p"), Quantities.Pressure, "bar", "pressure", rel) with { Min = 0, Max = 1e9 };
        private static VarDef EtaV() => V("etaV", NEtaV, T("η_v", "η_v"), Quantities.Efficiency, "%", "efficiencyV", VarRelevance.Engineer) with { Default = 1, Min = 0, Max = 1 };
        private static VarDef EtaHm() => V("etaHm", NEtaHm, T("η_hm", "η_hm"), Quantities.Efficiency, "%", "efficiencyHm", VarRelevance.Engineer) with { Default = 1, Min = 0, Max = 1 };
        private static VarDef Torque(I18nText? name = null) => V("T", name ?? NTorque, T("T", "M"), Quantities.Torque, "N·m", "torque", VarRelevance.Panel) with { Min = 0, Max = 1e7 };
        private static VarDef ShaftPower(I18nText name) => V("P", name, T("P", "P"), Quantities.Power, "W", "power", VarRelevance.Panel) with { Min = 0, Max = 1e9 };
        private static VarDef HydPower() => V("Phyd", NHydPower, T("P_hyd", "P_hyd"), Quantities.Power, "W", "powerHyd", VarRelevance.Panel) with { Min = 0, Max = 1e9 };
        private static VarDef Pmax() => V("pmax", NPmax, T("p_max", "p_max"), Quantities.Pressure, "bar", "pmax", VarRelevance.Engineer) with { Min = 0, Max = 1e9 };
        private static VarDef Length(string key, I18nText name, string sym, string param, VarRelevance rel) => V(key, name, T(sym, sym), Quantities.Length, "mm", param, rel) with { Min = 1e-4, Max = 10 };
        private static VarDef Area(string key, I18nText name, string sym, string param) => V(key, name, T(sym, sym), Quantities.Area, "cm²", param, VarRelevance.Panel) with { Min = 1e-8, Max = 10 };
        private static VarDef Force(string key, I18nText name, string sym, string param, VarRelevance rel) => V(key, name, T(sym, sym), Quantities.Force, "N", param, rel) with { Min = 0, Max = 1e9 };
        private static VarDef Velocity(string key, I18nText name, string sym, string param, VarRelevance rel) => V(key, name, T(sym, sym), Quantities.Speed, "m/s", param, rel) with { Min = 0, Max = 100 };
        private static VarDef Time(string key, I18nText name, string sym, string param) => V(key, name, T(sym, sym), Quantities.Time_, "s", param, VarRelevance.Panel) with { Min = 0, Max = 1e6 };

        private static ParamDef[] Params(IEnumerable<VarDef> vars)
            => vars.Select(v => Text(v.ParamKey, v.Name, v.Unit)).Append(ValueLabels.ShowValues).ToArray();

        // ------------------------------------------------------------------ relations

        private const double TwoPi = 2 * Math.PI;

        /// <summary>Pump: Q = V_g·n·η_v [S26].</summary>
        private static readonly Relation PumpFlow =
            new Relation("fluid.pump.flow", T("Pump flow", "Förderstrom"), "{Q} = {Vg}·{n}·{etaV}", "Q", "Vg", "n", "etaV") { Source = "S26" }
                .Solve("Q", "{Vg}·{n}·{etaV}", v => v("Vg") * v("n") / TwoPi * v("etaV"))
                .Solve("Vg", "{Q}/({n}·{etaV})", v => v("Q") * TwoPi / (v("n") * v("etaV")))
                .Solve("n", "{Q}/({Vg}·{etaV})", v => v("Q") * TwoPi / (v("Vg") * v("etaV")))
                .Solve("etaV", "{Q}/({Vg}·{n})", v => v("Q") * TwoPi / (v("Vg") * v("n")));

        /// <summary>Pump: T = V_g·p/(2π·η_hm) [S26].</summary>
        private static readonly Relation PumpTorque =
            new Relation("fluid.pump.torque", T("Drive torque", "Antriebsmoment"), "{T} = {Vg}·{p}/(2π·{etaHm})", "T", "Vg", "p", "etaHm") { Source = "S26" }
                .Solve("T", "{Vg}·{p}/(2π·{etaHm})", v => v("Vg") * v("p") / (TwoPi * v("etaHm")))
                .Solve("p", "2π·{T}·{etaHm}/{Vg}", v => TwoPi * v("T") * v("etaHm") / v("Vg"))
                .Solve("Vg", "2π·{T}·{etaHm}/{p}", v => TwoPi * v("T") * v("etaHm") / v("p"))
                .Solve("etaHm", "{Vg}·{p}/(2π·{T})", v => v("Vg") * v("p") / (TwoPi * v("T")));

        /// <summary>Motor: Q = V_g·n/η_v, i.e. n = Q·η_v/V_g [S26].</summary>
        private static readonly Relation MotorFlow =
            new Relation("fluid.motor.speed", T("Motor speed", "Motordrehzahl"), "{n} = {Q}·{etaV}/{Vg}", "n", "Q", "etaV", "Vg") { Source = "S26" }
                .Solve("n", "{Q}·{etaV}/{Vg}", v => v("Q") * v("etaV") * TwoPi / v("Vg"))
                .Solve("Q", "{n}·{Vg}/{etaV}", v => v("n") * v("Vg") / (TwoPi * v("etaV")))
                .Solve("Vg", "{Q}·{etaV}/{n}", v => v("Q") * v("etaV") * TwoPi / v("n"))
                .Solve("etaV", "{n}·{Vg}/{Q}", v => v("n") * v("Vg") / (TwoPi * v("Q")));

        /// <summary>Motor and rotary actuator: T = V_g·p·η_hm/(2π) [S26].</summary>
        private static readonly Relation MotorTorque =
            new Relation("fluid.motor.torque", T("Output torque", "Abtriebsmoment"), "{T} = {Vg}·{p}·{etaHm}/(2π)", "T", "Vg", "p", "etaHm") { Source = "S26" }
                .Solve("T", "{Vg}·{p}·{etaHm}/(2π)", v => v("Vg") * v("p") * v("etaHm") / TwoPi)
                .Solve("p", "2π·{T}/({Vg}·{etaHm})", v => TwoPi * v("T") / (v("Vg") * v("etaHm")))
                .Solve("Vg", "2π·{T}/({p}·{etaHm})", v => TwoPi * v("T") / (v("p") * v("etaHm")))
                .Solve("etaHm", "2π·{T}/({Vg}·{p})", v => TwoPi * v("T") / (v("Vg") * v("p")));

        /// <summary>P = 2π·n·T (n in rad/s internally) [S26].</summary>
        private static readonly Relation ShaftPowerRel =
            new Relation("fluid.power.shaft", T("Shaft power", "Wellenleistung"), "{P} = 2π·{n}·{T}", "P", "n", "T") { Source = "S26" }
                .Solve("P", "2π·{n}·{T}", v => v("n") * v("T"))
                .Solve("T", "{P}/(2π·{n})", v => v("P") / v("n"))
                .Solve("n", "{P}/(2π·{T})", v => v("P") / v("T"));

        /// <summary>P_hyd = p·Q [S26].</summary>
        private static readonly Relation HydPowerRel =
            new Relation("fluid.power.hyd", NHydPower, "{Phyd} = {p}·{Q}", "Phyd", "p", "Q") { Source = "S26" }
                .Solve("Phyd", "{p}·{Q}", v => v("p") * v("Q"))
                .Solve("p", "{Phyd}/{Q}", v => v("Phyd") / v("Q"))
                .Solve("Q", "{Phyd}/{p}", v => v("Phyd") / v("p"));

        /// <summary>Pump: P = P_hyd/(η_v·η_hm) [S26].</summary>
        private static readonly Relation PumpTotal =
            new Relation("fluid.pump.efficiency", T("Pump efficiency", "Pumpenwirkungsgrad"), "{P} = {Phyd}/({etaV}·{etaHm})", "P", "Phyd", "etaV", "etaHm") { Source = "S26" }
                .Solve("P", "{Phyd}/({etaV}·{etaHm})", v => v("Phyd") / (v("etaV") * v("etaHm")))
                .Solve("Phyd", "{P}·{etaV}·{etaHm}", v => v("P") * v("etaV") * v("etaHm"));

        /// <summary>Motor: P = P_hyd·η_v·η_hm [S26].</summary>
        private static readonly Relation MotorTotal =
            new Relation("fluid.motor.efficiency", T("Motor efficiency", "Motorwirkungsgrad"), "{P} = {Phyd}·{etaV}·{etaHm}", "P", "Phyd", "etaV", "etaHm") { Source = "S26" }
                .Solve("P", "{Phyd}·{etaV}·{etaHm}", v => v("Phyd") * v("etaV") * v("etaHm"))
                .Solve("Phyd", "{P}/({etaV}·{etaHm})", v => v("P") / (v("etaV") * v("etaHm")));

        /// <summary>Piston area A = π·D²/4 [S26].</summary>
        private static Relation CircleArea(string id, string a, string d, I18nText name) =>
            new Relation(id, name, "{" + a + "} = π·{" + d + "}²/4", a, d) { Source = "S26" }
                .Solve(a, "π·{" + d + "}²/4", v => Math.PI * v(d) * v(d) / 4)
                .Solve(d, "√(4·{" + a + "}/π)", v => Math.Sqrt(4 * v(a) / Math.PI));

        /// <summary>Annulus area A = π·(D² − d²)/4 [S26].</summary>
        private static Relation RingArea(string id, string a, I18nText name) =>
            new Relation(id, name, "{" + a + "} = π·({D}² − {d}²)/4", a, "D", "d") { Source = "S26" }
                .Solve(a, "π·({D}² − {d}²)/4", v => Math.PI * (v("D") * v("D") - v("d") * v("d")) / 4)
                .Solve("d", "√({D}² − 4·{" + a + "}/π)", v => Math.Sqrt(v("D") * v("D") - 4 * v(a) / Math.PI))
                .Solve("D", "√({d}² + 4·{" + a + "}/π)", v => Math.Sqrt(v("d") * v("d") + 4 * v(a) / Math.PI));

        private static readonly I18nText NAreaExt = T("Effective area, extending", "Wirksame Fläche, Ausfahren");
        private static readonly I18nText NAreaRet = T("Annulus area, retracting", "Ringfläche, Einfahren");

        private static readonly Relation PistonArea = CircleArea("fluid.cyl.area", "A1", "D", NAreaExt);
        private static readonly Relation ThroughArea = RingArea("fluid.cyl.area.through", "A1", NAreaExt);
        private static readonly Relation Annulus = RingArea("fluid.cyl.annulus", "A2", NAreaRet);

        /// <summary>Area ratio φ = A₁/A₂ [S26].</summary>
        private static readonly Relation AreaRatio =
            new Relation("fluid.cyl.ratio", T("Area ratio", "Flächenverhältnis"), "{phi} = {A1}/{A2}", "phi", "A1", "A2") { Source = "S26" }
                .Solve("phi", "{A1}/{A2}", v => v("A1") / v("A2"))
                .Solve("A1", "{phi}·{A2}", v => v("phi") * v("A2"))
                .Solve("A2", "{A1}/{phi}", v => v("A1") / v("phi"));

        /// <summary>F = p·A·η_hm [S26].</summary>
        private static Relation ForceRel(string id, string f, string a, I18nText name) =>
            new Relation(id, name, "{" + f + "} = {p}·{" + a + "}·{etaHm}", f, "p", a, "etaHm") { Source = "S26" }
                .Solve(f, "{p}·{" + a + "}·{etaHm}", v => v("p") * v(a) * v("etaHm"))
                .Solve("p", "{" + f + "}/({" + a + "}·{etaHm})", v => v(f) / (v(a) * v("etaHm")))
                .Solve(a, "{" + f + "}/({p}·{etaHm})", v => v(f) / (v("p") * v("etaHm")))
                .Solve("etaHm", "{" + f + "}/({p}·{" + a + "})", v => v(f) / (v("p") * v(a)));

        /// <summary>v = Q/A [S26, S27].</summary>
        private static Relation SpeedRel(string id, string s, string a, I18nText name) =>
            new Relation(id, name, "{" + s + "} = {Q}/{" + a + "}", s, "Q", a) { Source = "S26" }
                .Solve(s, "{Q}/{" + a + "}", v => v("Q") / v(a))
                .Solve("Q", "{" + s + "}·{" + a + "}", v => v(s) * v(a))
                .Solve(a, "{Q}/{" + s + "}", v => v("Q") / v(s));

        /// <summary>Stroke time t = s/v [S1].</summary>
        private static Relation TimeRel(string id, string t, string sp, I18nText name) =>
            new Relation(id, name, "{" + t + "} = {s}/{" + sp + "}", t, "s", sp) { Source = "S1" }
                .Solve(t, "{s}/{" + sp + "}", v => v("s") / v(sp))
                .Solve("s", "{" + t + "}·{" + sp + "}", v => v(t) * v(sp))
                .Solve(sp, "{s}/{" + t + "}", v => v("s") / v(t));

        private static readonly Relation ForceExt = ForceRel("fluid.cyl.force.ext", "Fext", "A1", T("Force, extending", "Kraft, Ausfahren"));
        private static readonly Relation ForceRet = ForceRel("fluid.cyl.force.ret", "Fret", "A2", T("Force, retracting", "Kraft, Einfahren"));
        private static readonly Relation SpeedExt = SpeedRel("fluid.cyl.speed.ext", "vext", "A1", T("Speed, extending", "Geschwindigkeit, Ausfahren"));
        private static readonly Relation SpeedRet = SpeedRel("fluid.cyl.speed.ret", "vret", "A2", T("Speed, retracting", "Geschwindigkeit, Einfahren"));
        private static readonly Relation TimeExt = TimeRel("fluid.cyl.time.ext", "text", "vext", T("Stroke time, extending", "Hubzeit, Ausfahren"));
        private static readonly Relation TimeRet = TimeRel("fluid.cyl.time.ret", "tret", "vret", T("Stroke time, retracting", "Hubzeit, Einfahren"));

        private static readonly Relation SingleArea = CircleArea("fluid.cyl.area", "A", "D", T("Effective area", "Wirksame Fläche"));
        private static readonly Relation SingleRing = RingArea("fluid.cyl.area.pull", "A", T("Effective area", "Wirksame Fläche"));
        private static readonly Relation SingleForce = ForceRel("fluid.cyl.force", "F", "A", T("Piston force", "Kolbenkraft"));
        private static readonly Relation SingleSpeed = SpeedRel("fluid.cyl.speed", "v", "A", T("Piston speed", "Kolbengeschwindigkeit"));
        private static readonly Relation SingleTime = TimeRel("fluid.cyl.time", "t", "v", T("Stroke time", "Hubzeit"));

        /// <summary>Line: v = Q/(π·d_i²/4) [S27].</summary>
        private static readonly Relation LineVelocity =
            new Relation("fluid.line.velocity", T("Flow velocity", "Strömungsgeschwindigkeit"), "{v} = 4·{Q}/(π·{di}²)", "v", "Q", "di") { Source = "S27" }
                .Solve("v", "4·{Q}/(π·{di}²)", v => 4 * v("Q") / (Math.PI * v("di") * v("di")))
                .Solve("Q", "π·{di}²·{v}/4", v => Math.PI * v("di") * v("di") * v("v") / 4)
                .Solve("di", "√(4·{Q}/(π·{v}))", v => Math.Sqrt(4 * v("Q") / (Math.PI * v("v"))));

        /// <summary>Accumulator, isothermal (Boyle–Mariotte): p₀·V₀ = p₁·V₁ = p₂·V₂ with absolute pressures [S26].</summary>
        private static Relation Boyle(string id, string vx, string px, I18nText name) =>
            new Relation(id, name, "{" + vx + "} = {p0}·{V0}/{" + px + "}", vx, "p0", "V0", px) { Source = "S26", Validity = T("isothermal, absolute pressures", "isotherm, absolute Drücke") }
                .Solve(vx, "{p0}·{V0}/{" + px + "}", v => v("p0") * v("V0") / v(px))
                .Solve(px, "{p0}·{V0}/{" + vx + "}", v => v("p0") * v("V0") / v(vx))
                .Solve("V0", "{" + vx + "}·{" + px + "}/{p0}", v => v(vx) * v(px) / v("p0"))
                .Solve("p0", "{" + vx + "}·{" + px + "}/{V0}", v => v(vx) * v(px) / v("V0"));

        private static readonly Relation AccuDelta =
            new Relation("fluid.accu.delta", T("Usable volume", "Nutzvolumen"), "{dV} = {V1} − {V2}", "dV", "V1", "V2") { Source = "S26" }
                .Solve("dV", "{V1} − {V2}", v => v("V1") - v("V2"))
                .Solve("V1", "{dV} + {V2}", v => v("dV") + v("V2"))
                .Solve("V2", "{V1} − {dV}", v => v("V1") - v("dV"));

        // ------------------------------------------------------------------ losses, orifices, power unit, air consumption (round 5)

        /// <summary>Hydraulic oil defaults: ρ ≈ 870 kg/m³, ν = 46 mm²/s (viscosity grade VG 46 at 40 °C), drawn tube k = 0.0015 mm [S26, S27].</summary>
        private const double OilRho = 870, OilNu = 46e-6, TubeK = 0.0015e-3;

        /// <summary>Ambient (atmospheric) pressure for free air volumes, 1.013 bar [S26].</summary>
        internal const double Ambient = 101325;

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

        private static readonly Relation Reynolds =
            new Relation("fluid.line.reynolds", T("Reynolds number", "Reynolds-Zahl"), "{Re} = {v}·{di}/{nu}", "Re", "v", "di", "nu") { Source = "S27" }
                .Solve("Re", "{v}·{di}/{nu}", v => v("v") * v("di") / v("nu"))
                .Solve("v", "{Re}·{nu}/{di}", v => v("Re") * v("nu") / v("di"))
                .Solve("nu", "{v}·{di}/{Re}", v => v("v") * v("di") / v("Re"));

        private static readonly Relation FrictionRel =
            new Relation("fluid.line.friction", T("Friction factor (laminar 64/Re, turbulent Haaland)", "Rohrreibungszahl (laminar 64/Re, turbulent Haaland)"),
                "{lambda} = 64/{Re} (Re < 2320) or 1/(−1.8·lg(({k}/{di}/3.7)^1.11 + 6.9/{Re}))²", "lambda", "Re", "k", "di") { Source = "S27" }
                .Solve("lambda", "64/{Re} or Haaland({Re}, {k}/{di})", v => Friction(v("Re"), v("k"), v("di")));

        /// <summary>Darcy–Weisbach Δp = λ·(L/d_i)·ρ·v²/2 [S27].</summary>
        private static readonly Relation DarcyRel =
            new Relation("fluid.line.loss", T("Pressure drop in the line (Darcy–Weisbach)", "Druckverlust in der Leitung (Darcy–Weisbach)"),
                "{dp} = {lambda}·{L}/{di}·{rho}·{v}²/2", "dp", "lambda", "L", "di", "rho", "v") { Source = "S27", Validity = T("straight line, fittings not counted", "gerade Leitung, Formteile nicht gerechnet") }
                .Solve("dp", "{lambda}·{L}/{di}·{rho}·{v}²/2", v => v("lambda") * v("L") / v("di") * v("rho") * v("v") * v("v") / 2)
                .Solve("L", "2·{dp}·{di}/({lambda}·{rho}·{v}²)", v => 2 * v("dp") * v("di") / (v("lambda") * v("rho") * v("v") * v("v")));

        /// <summary>Orifice A₀ = π·d₀²/4 and Q = α·A₀·√(2Δp/ρ) [S26].</summary>
        private static readonly Relation OrificeArea = CircleArea("fluid.orifice.area", "A0", "d0", T("Orifice cross-section", "Drosselquerschnitt"));

        private static readonly Relation Orifice =
            new Relation("fluid.orifice.flow", T("Flow through an orifice", "Durchfluss durch eine Blende"), "{Q} = {alpha}·{A0}·√(2·{dp}/{rho})", "Q", "alpha", "A0", "dp", "rho")
            { Source = "S26", Validity = T("sharp-edged orifice, incompressible fluid (hydraulics)", "scharfkantige Blende, inkompressibles Fluid (Hydraulik)") }
                .Solve("Q", "{alpha}·{A0}·√(2·{dp}/{rho})", v => v("alpha") * v("A0") * Math.Sqrt(2 * v("dp") / v("rho")))
                .Solve("dp", "{rho}/2·({Q}/({alpha}·{A0}))²", v => v("rho") / 2 * Math.Pow(v("Q") / (v("alpha") * v("A0")), 2))
                .Solve("A0", "{Q}/({alpha}·√(2·{dp}/{rho}))", v => v("Q") / (v("alpha") * Math.Sqrt(2 * v("dp") / v("rho"))))
                .Solve("alpha", "{Q}/({A0}·√(2·{dp}/{rho}))", v => v("Q") / (v("A0") * Math.Sqrt(2 * v("dp") / v("rho"))));

        private static readonly Relation PressureDifference =
            new Relation("fluid.dp", T("Pressure difference", "Druckdifferenz"), "{dp} = {p1} − {p2}", "dp", "p1", "p2") { Source = "S1" }
                .Solve("dp", "{p1} − {p2}", v => v("p1") - v("p2"))
                .Solve("p1", "{p2} + {dp}", v => v("p2") + v("dp"))
                .Solve("p2", "{p1} − {dp}", v => v("p1") - v("dp"));

        /// <summary>Power unit: P = P_hyd/η_t [S26].</summary>
        private static readonly Relation UnitPower =
            new Relation("fluid.unit.power", T("Drive power of the power unit", "Antriebsleistung des Aggregats"), "{P} = {Phyd}/{eta}", "P", "Phyd", "eta") { Source = "S26" }
                .Solve("P", "{Phyd}/{eta}", v => v("Phyd") / v("eta"))
                .Solve("Phyd", "{P}·{eta}", v => v("P") * v("eta"))
                .Solve("eta", "{Phyd}/{P}", v => v("Phyd") / v("P"));

        /// <summary>Free air: compressed volume or flow × (p_e + p_amb)/p_amb (isothermal) [S26].</summary>
        private static Relation FreeAir(string id, I18nText name, string free, string compressed, string expr) =>
            new Relation(id, name, "{" + free + "} = " + expr + "·({p} + p_amb)/p_amb", free, compressed, "p")
            { Source = "S26", Validity = T("pneumatics, p as gauge pressure, p_amb = 1.013 bar", "Pneumatik, p als Überdruck, p_amb = 1,013 bar") }
                .Solve(free, expr + "·({p} + p_amb)/p_amb", v => v(compressed) * (v("p") + Ambient) / Ambient)
                .Solve(compressed, "{" + free + "}·p_amb/({p} + p_amb)", v => v(free) * Ambient / (v("p") + Ambient))
                .Solve("p", "p_amb·({" + free + "}/{" + compressed + "} − 1)", v => Ambient * (v(free) / v(compressed) - 1));

        /// <summary>Swept volume of one double stroke V_s = (A₁ + A₂)·s, or A·s for a single-acting cylinder [S26].</summary>
        private static Relation Swept(string id, string area, string? area2) =>
            new Relation(id, T("Swept volume per cycle", "Hubvolumen je Arbeitsspiel"), area2 is null ? "{Vs} = {" + area + "}·{s}" : "{Vs} = ({" + area + "} + {" + area2 + "})·{s}",
                area2 is null ? new[] { "Vs", area, "s" } : new[] { "Vs", area, area2, "s" }) { Source = "S26" }
                .Solve("Vs", area2 is null ? "{" + area + "}·{s}" : "({" + area + "} + {" + area2 + "})·{s}",
                    v => (v(area) + (area2 is null ? 0 : v(area2))) * v("s"))
                .Solve("s", area2 is null ? "{Vs}/{" + area + "}" : "{Vs}/({" + area + "} + {" + area2 + "})",
                    v => v("Vs") / (v(area) + (area2 is null ? 0 : v(area2))));

        private static readonly Relation AirPerCycle = FreeAir("fluid.air.cycle", T("Free air per cycle", "Freie Luft je Arbeitsspiel"), "Vair", "Vs", "{Vs}");
        private static readonly Relation AirFlow = FreeAir("fluid.air.flow", T("Free air flow during a stroke", "Freier Luftverbrauch während eines Hubs"), "Qn", "Q", "{Q}");

        private static IEnumerable<VarDef> AirVars() => new[]
        {
            V("Vs", T("Swept volume per cycle", "Hubvolumen je Arbeitsspiel"), T("V_s", "V_H"), Quantities.Volume, "l", "sweptVolume", VarRelevance.Engineer) with { Min = 0, Max = 100 },
            V("Vair", T("Free air per cycle (pneumatic)", "Freie Luft je Arbeitsspiel (Pneumatik)"), T("V_air", "V_L"), Quantities.Volume, "l", "airPerCycle", VarRelevance.Engineer) with { Min = 0, Max = 1e4 },
            V("Qn", T("Free air flow during a stroke (pneumatic)", "Luftverbrauch während eines Hubs (Pneumatik)"), T("Q_n", "Q_n"), Quantities.VolumeFlow, "l/min", "airFlow", VarRelevance.Engineer) with { Min = 0, Max = 100 },
        };

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
            "fluid.pump" => Pump(),
            "fluid.motor" => Motor(),
            "fluid.cylinder.double" => DoubleCylinder(),
            "fluid.cylinder.single" => SingleCylinder(),
            "fluid.rotary" => Rotary(),
            "fluid.valve.relief" => SetPressure(PortsOf(("P", "pmax", "pset"))),
            "fluid.valve.reducing" => SetPressure(PortsOf(("A", "p", "pset"))),
            "fluid.valve.sequence" => SetPressure((_, _) => Array.Empty<PortVar>()),
            "fluid.gauge" or "fluid.pressureswitch" => Gauge(),
            "fluid.source" => Source(),
            "fluid.flowmeter" => FlowMeter(),
            "fluid.accumulator" => Accumulator(),
            "fluid.valve.throttle" => Throttle(),
            PowerUnitId => PowerUnit(),
            FluidLineType.TypeId => Line(),
            _ => Spec.Empty,
        };

        private static Func<SketchObject, string, IEnumerable<PortVar>> PortsOf(params (string Port, string Cls, string Key)[] map)
            => (_, port) => map.Where(m => m.Port == port).Select(m => new PortVar(m.Cls, m.Key)).ToArray();

        private static Func<SketchObject, string, IEnumerable<PortVar>> Working(params string[] ports)
            => (_, port) => ports.Contains(port) ? new[] { new PortVar("Q", "Q"), new PortVar("p", "p") } : Array.Empty<PortVar>();

        private static Spec Pump()
        {
            var vars = new[]
            {
                Disp(), Speed(), Flow(), Pressure(), EtaV(), EtaHm(),
                Torque(T("Drive torque", "Antriebsmoment")), ShaftPower(T("Drive power", "Antriebsleistung")), HydPower(),
            };
            return new Spec(vars, new[] { PumpFlow, PumpTorque, ShaftPowerRel, HydPowerRel, PumpTotal }, Params(vars), new[] { ReliefMissing() })
            {
                Ports = Working("P"),
            };
        }

        private static Spec Motor()
        {
            var vars = new[]
            {
                Disp(), Speed(), Flow(), Pressure(), EtaV(), EtaHm(),
                Torque(T("Output torque", "Abtriebsmoment")), ShaftPower(T("Output power", "Abtriebsleistung")), HydPower(),
            };
            return new Spec(vars, new[] { MotorFlow, MotorTorque, ShaftPowerRel, HydPowerRel, MotorTotal }, Params(vars), Array.Empty<ValueCheck>())
            {
                Ports = Working("A", "B"),
            };
        }

        private static IEnumerable<PortVar> CylinderPort(string port)
            => port is "A" or "B" ? new[] { new PortVar("Q", "Q"), new PortVar("p", "p"), new PortVar("pmax", "pmax") } : Array.Empty<PortVar>();

        private static Spec DoubleCylinder()
        {
            var vars = new[]
            {
                Length("D", T("Piston diameter (bore)", "Kolbendurchmesser"), "D", "bore", VarRelevance.Card),
                Length("d", T("Rod diameter", "Kolbenstangendurchmesser"), "d", "rodDiameter", VarRelevance.Card),
                Force("Fext", T("Force, extending", "Kraft, Ausfahren"), "F_aus", "forceExt", VarRelevance.Card) with { Symbol = T("F_ext", "F_aus") },
                Velocity("vext", T("Speed, extending", "Geschwindigkeit, Ausfahren"), "v_aus", "speedExt", VarRelevance.Card) with { Symbol = T("v_ext", "v_aus") },
                Pressure(VarRelevance.Panel), Flow(VarRelevance.Panel),
                Force("Fret", T("Force, retracting", "Kraft, Einfahren"), "F_ein", "forceRet", VarRelevance.Panel) with { Symbol = T("F_ret", "F_ein") },
                Velocity("vret", T("Speed, retracting", "Geschwindigkeit, Einfahren"), "v_ein", "speedRet", VarRelevance.Panel) with { Symbol = T("v_ret", "v_ein") },
                Area("A1", NAreaExt, "A₁", "areaExt"), Area("A2", NAreaRet, "A₂", "areaRet"),
                V("phi", T("Area ratio", "Flächenverhältnis"), T("φ", "φ"), Quantities.Ratio, null, "areaRatio", VarRelevance.Panel) with { Min = 0.5, Max = 100 },
                Length("s", T("Stroke", "Hub"), "s", "stroke", VarRelevance.Panel),
                Time("text", T("Stroke time, extending", "Hubzeit, Ausfahren"), "t_aus", "timeExt") with { Symbol = T("t_ext", "t_aus") },
                Time("tret", T("Stroke time, retracting", "Hubzeit, Einfahren"), "t_ein", "timeRet") with { Symbol = T("t_ret", "t_ein") },
                EtaHm(),
                Force("Freq", T("Required force", "Erforderliche Kraft"), "F_req", "forceReq", VarRelevance.Engineer) with { Symbol = T("F_req", "F_erf") },
                Pmax(),
            }.Concat(AirVars()).ToArray();
            var rels = new[] { Annulus, AreaRatio, ForceExt, ForceRet, SpeedExt, SpeedRet, TimeExt, TimeRet, Swept("fluid.cyl.swept", "A1", "A2"), AirPerCycle, AirFlow };
            return new Spec(vars, rels, Params(vars), new[] { CylinderForce("A1") })
            {
                Ports = (_, port) => CylinderPort(port),
                Local = o => new[] { o.Str("rod", "single") == "double" ? ThroughArea : PistonArea },
            };
        }

        private static Spec SingleCylinder()
        {
            var vars = new[]
            {
                Length("D", T("Piston diameter (bore)", "Kolbendurchmesser"), "D", "bore", VarRelevance.Card),
                Force("F", T("Piston force (without spring)", "Kolbenkraft (ohne Feder)"), "F", "force", VarRelevance.Card),
                Velocity("v", T("Piston speed", "Kolbengeschwindigkeit"), "v", "speed", VarRelevance.Card),
                Pressure(VarRelevance.Card), Flow(VarRelevance.Panel),
                Length("d", T("Rod diameter", "Kolbenstangendurchmesser"), "d", "rodDiameter", VarRelevance.Panel),
                Area("A", T("Effective area", "Wirksame Fläche"), "A", "area"),
                Length("s", T("Stroke", "Hub"), "s", "stroke", VarRelevance.Panel),
                Time("t", T("Stroke time", "Hubzeit"), "t", "time"),
                EtaHm(),
                Force("Freq", T("Required force", "Erforderliche Kraft"), "F_req", "forceReq", VarRelevance.Engineer) with { Symbol = T("F_req", "F_erf") },
                Pmax(),
            }.Concat(AirVars()).ToArray();
            return new Spec(vars, new[] { SingleForce, SingleSpeed, SingleTime, Swept("fluid.cyl.swept.single", "A", null), AirPerCycle, AirFlow }, Params(vars), new[] { CylinderForce("A") })
            {
                Ports = (_, port) => CylinderPort(port),
                // Pull type (spring extends): the pressure acts on the annulus.
                Local = o => new[] { o.Str("spring", "return") == "extend" ? SingleRing : SingleArea },
            };
        }

        private static Spec Rotary()
        {
            var vars = new[] { Disp(), Pressure(), Torque(T("Output torque", "Abtriebsmoment")) with { Relevance = VarRelevance.Card }, EtaHm() };
            return new Spec(vars, new[] { MotorTorque }, Params(vars), Array.Empty<ValueCheck>())
            {
                Ports = (_, port) => port is "A" or "B" ? new[] { new PortVar("p", "p") } : Array.Empty<PortVar>(),
            };
        }

        private static Spec SetPressure(Func<SketchObject, string, IEnumerable<PortVar>> ports)
        {
            var vars = new[] { V("pset", NSetPressure, T("p_set", "p_E"), Quantities.Pressure, "bar", "setPressure") with { Min = 0, Max = 1e9 } };
            return new Spec(vars, Array.Empty<Relation>(), Params(vars), Array.Empty<ValueCheck>()) { Ports = ports };
        }

        private static Spec Gauge()
        {
            var vars = new[] { Pressure() };
            return new Spec(vars, Array.Empty<Relation>(), Params(vars), Array.Empty<ValueCheck>()) { Ports = PortsOf(("P", "p", "p")) };
        }

        private static Spec Source()
        {
            var vars = new[] { Pressure() };
            return new Spec(vars, Array.Empty<Relation>(), Params(vars), Array.Empty<ValueCheck>()) { Ports = PortsOf(("P", "p", "p")) };
        }

        private static Spec FlowMeter()
        {
            var vars = new[] { Flow() };
            return new Spec(vars, Array.Empty<Relation>(), Params(vars), Array.Empty<ValueCheck>()) { Ports = PortsOf(("P", "Q", "Q"), ("A", "Q", "Q")) };
        }

        private static Spec Accumulator()
        {
            VarDef Vol(string key, I18nText name, string sym, string param, VarRelevance rel)
                => V(key, name, T(sym, sym), Quantities.Volume, "l", param, rel) with { Min = 1e-7, Max = 10 };
            VarDef Abs(string key, I18nText name, string sym, string param, VarRelevance rel)
                => V(key, name, T(sym, sym), Quantities.Pressure, "bar", param, rel) with { Min = 1, Max = 1e9 };
            var vars = new[]
            {
                Vol("V0", T("Nominal gas volume", "Nenn-Gasvolumen"), "V₀", "volume", VarRelevance.Card),
                Abs("p0", T("Precharge pressure (absolute)", "Vorfülldruck (absolut)"), "p₀", "p0", VarRelevance.Card),
                Abs("p1", T("Minimum working pressure (absolute)", "Minimaler Arbeitsdruck (absolut)"), "p₁", "p1", VarRelevance.Panel),
                Abs("p2", T("Maximum working pressure (absolute)", "Maximaler Arbeitsdruck (absolut)"), "p₂", "p2", VarRelevance.Panel),
                Vol("V1", T("Gas volume at p₁", "Gasvolumen bei p₁"), "V₁", "volume1", VarRelevance.Panel),
                Vol("V2", T("Gas volume at p₂", "Gasvolumen bei p₂"), "V₂", "volume2", VarRelevance.Panel),
                Vol("dV", T("Usable volume", "Nutzvolumen"), "ΔV", "volumeUsable", VarRelevance.Card),
            };
            var rels = new[]
            {
                Boyle("fluid.accu.v1", "V1", "p1", T("Gas volume at p₁", "Gasvolumen bei p₁")),
                Boyle("fluid.accu.v2", "V2", "p2", T("Gas volume at p₂", "Gasvolumen bei p₂")),
                AccuDelta,
            };
            return new Spec(vars, rels, Params(vars), Array.Empty<ValueCheck>());
        }

        private static Spec Line()
        {
            var vars = new[]
            {
                Flow(), Length("di", T("Inner diameter", "Innendurchmesser"), "d_i", "innerDiameter", VarRelevance.Card),
                Velocity("v", T("Flow velocity", "Strömungsgeschwindigkeit"), "v", "velocity", VarRelevance.Card),
                Pressure(VarRelevance.Panel),
                // Pressure drop (round 5): length and Δp in the panel; fluid and wall data for engineers (oil defaults).
                V("L", T("Line length", "Leitungslänge"), T("L", "L"), Quantities.Length, "m", "length", VarRelevance.Panel) with { Min = 0, Max = 1e4 },
                V("dp", T("Pressure drop", "Druckverlust"), T("Δp", "Δp"), Quantities.Pressure, "bar", "pressureDrop", VarRelevance.Panel) with { Min = 0, Max = 1e9 },
                V("Re", T("Reynolds number", "Reynolds-Zahl"), T("Re", "Re"), Quantities.Ratio, null, "reynolds", VarRelevance.Engineer) with { Min = 0, Max = 1e9 },
                V("lambda", T("Friction factor", "Rohrreibungszahl"), T("λ", "λ"), Quantities.Ratio, null, "friction", VarRelevance.Engineer) with { Min = 1e-4, Max = 100 },
                V("nu", T("Kinematic viscosity", "Kinematische Viskosität"), T("ν", "ν"), Quantities.Viscosity, "mm²/s", "viscosity", VarRelevance.Engineer) with { Default = OilNu, Min = 1e-8, Max = 1 },
                V("rho", T("Density", "Dichte"), T("ρ", "ρ"), Quantities.Density, "kg/m³", "density", VarRelevance.Engineer) with { Default = OilRho, Min = 0.01, Max = 3e4 },
                V("k", T("Wall roughness", "Wandrauigkeit"), T("k", "k"), Quantities.Length, "mm", "roughness", VarRelevance.Engineer) with { Default = TubeK, Min = 0, Max = 0.1 },
            };
            return new Spec(vars, new[] { LineVelocity, Reynolds, FrictionRel, DarcyRel }, Params(vars), new[] { LineSpeed() });
        }

        /// <summary>
        /// Throttle: orifice flow Q = α·A₀·√(2Δp/ρ) with Δp = p₁ − p₂; the flow is that of the line docked at P (or A), p₁ and
        /// p₂ the pressures of the lines docked at P and A (the throttle passes the flow, not the pressure) [S26].
        /// </summary>
        private static Spec Throttle()
        {
            var vars = new[]
            {
                Flow(), V("dp", T("Pressure drop", "Druckabfall"), T("Δp", "Δp"), Quantities.Pressure, "bar", "pressureDrop") with { Min = 0, Max = 1e9 },
                Length("d0", T("Orifice diameter", "Drosseldurchmesser"), "d₀", "orificeDiameter", VarRelevance.Card),
                V("p1", T("Inlet pressure (P)", "Eingangsdruck (P)"), T("p₁", "p₁"), Quantities.Pressure, "bar", "pressureIn", VarRelevance.Panel) with { Min = 0, Max = 1e9 },
                V("p2", T("Outlet pressure (A)", "Ausgangsdruck (A)"), T("p₂", "p₂"), Quantities.Pressure, "bar", "pressureOut", VarRelevance.Panel) with { Min = 0, Max = 1e9 },
                Area("A0", T("Orifice cross-section", "Drosselquerschnitt"), "A₀", "orificeArea") with { Unit = "mm²" },
                V("alpha", T("Flow coefficient", "Durchflusszahl"), T("α", "α"), Quantities.Ratio, null, "flowCoefficient", VarRelevance.Engineer) with { Default = 0.6, Min = 0.01, Max = 1 },
                V("rho", T("Density", "Dichte"), T("ρ", "ρ"), Quantities.Density, "kg/m³", "density", VarRelevance.Engineer) with { Default = OilRho, Min = 0.01, Max = 3e4 },
            };
            return new Spec(vars, new[] { OrificeArea, Orifice, PressureDifference }, Params(vars), Array.Empty<ValueCheck>())
            {
                Docked = (o, ctx) => DockedLines(o, ctx, ("P", "Q", "Q"), ("A", "Q", "Q"), ("P", "p", "p1"), ("A", "p", "p2")),
            };
        }

        private static readonly Relation FromLine = Relation.Equality("fluid.line.docked", T("Taken from the docked line", "Von der angeschlossenen Leitung übernommen"));

        /// <summary>
        /// Links from an element's variables to the lines docked on its ports: (port, line variable, element variable); a
        /// variable is linked once, to the first docked line (by id) of its first port.
        /// </summary>
        private static IEnumerable<BoundRelation> DockedLines(SketchObject o, RenderContext ctx, params (string Port, string LineKey, string Key)[] map)
        {
            var lines = ctx.Doc.Objects.Values.Where(x => x.Type == FluidLineType.TypeId && x.Attach is { } a && a.Any(t => t?.ObjectId == o.Id))
                .OrderBy(x => x.Id, StringComparer.Ordinal).ToList();
            var done = new HashSet<string>(StringComparer.Ordinal);
            foreach (var (port, lineKey, key) in map)
            {
                if (done.Contains(key)) continue;
                var line = lines.FirstOrDefault(l => l.Attach!.Any(t => t?.ObjectId == o.Id && t.AnchorId == port));
                if (line is null) continue;
                done.Add(key);
                yield return new BoundRelation(FromLine, new Dictionary<string, VarRef>(StringComparer.Ordinal) { ["a"] = new VarRef(o.Id, key), ["b"] = new VarRef(line.Id, lineKey) });
            }
        }

        /// <summary>Type id of the hydraulic power unit (tank, motor-pump group, relief valve, return filter).</summary>
        public const string PowerUnitId = "fluid.powerunit";

        /// <summary>
        /// Hydraulic power unit: flow and pressure at P, hydraulic and drive power, the integrated relief valve (p_max at P) and
        /// the tank volume with the textbook rule V_T ≈ 3 … 5 × Q per minute [S26].
        /// </summary>
        private static Spec PowerUnit()
        {
            var vars = new[]
            {
                Flow(), Pressure(),
                ShaftPower(T("Drive power", "Antriebsleistung")) with { Relevance = VarRelevance.Card },
                V("VT", T("Tank volume", "Tankvolumen"), T("V_T", "V_T"), Quantities.Volume, "l", "tankVolume") with { Min = 1e-4, Max = 1e3 },
                HydPower(),
                V("eta", T("Total efficiency", "Gesamtwirkungsgrad"), T("η_t", "η_t"), Quantities.Efficiency, "%", "efficiency", VarRelevance.Panel) with { Default = 1, Min = 0.01, Max = 1 },
                V("pset", NSetPressure, T("p_set", "p_E"), Quantities.Pressure, "bar", "setPressure", VarRelevance.Panel) with { Min = 0, Max = 1e9 },
            };
            return new Spec(vars, new[] { HydPowerRel, UnitPower }, Params(vars), new[] { TankSize() })
            {
                Ports = (_, port) => port == "P" ? new[] { new PortVar("Q", "Q"), new PortVar("p", "p"), new PortVar("pmax", "pset") } : Array.Empty<PortVar>(),
            };
        }

        /// <summary>fluid.tank.volume: the tank holds less than three minutes of the pump flow (textbook rule 3 … 5 × Q per minute) [S26].</summary>
        private static ValueCheck TankSize() => new(
            "fluid.tank.volume", CheckSeverity.Hint,
            T("The tank holds less than about three minutes of the pump flow (rule of thumb for stationary units: 3–5 × Q per minute). Check the tank size.",
              "Der Tank fasst weniger als etwa drei Minuten Förderstrom (Faustregel für stationäre Aggregate: 3–5 × Q je Minute). Tankgröße prüfen."),
            "V_T < 3·Q·1 min",
            c => c["VT"] is double vt && c["Q"] is double q && q > 0 && vt < 3 * q * 60 * (1 - Rel))
        { Source = "S26" };

        // ------------------------------------------------------------------ net description

        private static readonly IReadOnlyList<string> DirectionalReturn = new[] { "T", "R", "S", "X", "Y", "L" };

        /// <summary>Port groups that pass a class through unchanged (valves, inline parts, couplings).</summary>
        private static IEnumerable<string[]> Through(SketchObject o, string cls)
        {
            switch (o.Type)
            {
                case DirectionalValves.TypeId:
                    return new[] { new[] { "P", "A", "B" } };
                case "fluid.valve.check":
                case "fluid.flowmeter":
                case "fluid.filter":
                case "fluid.separator":
                case "fluid.lubricator":
                case "fluid.cooler":
                case "fluid.heater":
                case "fluid.frl":
                    return new[] { new[] { "P", "A" } };
                case "fluid.valve.throttle":
                case "fluid.valve.flow":
                    return cls == "p" ? Array.Empty<string[]>() : new[] { new[] { "P", "A" } };
                case "fluid.valve.pilotcheck":
                    return o.Flag("twin", false) ? new[] { new[] { "A1", "A2" }, new[] { "B1", "B2" } } : new[] { new[] { "P", "A" } };
                case "fluid.valve.counterbalance":
                    // The load pressure is held back: only the flow passes.
                    return cls == "Q" ? new[] { new[] { "C", "V" } } : Array.Empty<string[]>();
                case "fluid.valve.reducing":
                    return cls == "Q" ? new[] { new[] { "P", "A" } } : Array.Empty<string[]>();
            }
            // Permanent passages of the symbol (crossing W–E and N–S, coupled quick coupling).
            return FluidModule.TypeById(o.Type) is FluidType { Passages: { } passages } ? passages(o) : Array.Empty<string[]>();
        }

        internal static readonly NetSpec Nets = new()
        {
            Prefix = "fluid",
            IsLine = o => o.Type == FluidLineType.TypeId,
            PortVars = (o, port) => For(o.Type).Ports(o, port),
            LineVars = _ => new[] { new PortVar("Q", "Q"), new PortVar("p", "p") },
            Through = Through,
            Classes = new[] { "Q", "p", "pmax" },
            Rule = c => c == "Q" ? NetRule.Flow : NetRule.Equal,
            Name = c => c switch
            {
                "Q" => T("Flow passed on", "Volumenstrom weitergegeben"),
                "p" => T("Pressure passed on", "Druck weitergegeben"),
                _ => T("Maximum pressure passed on", "Maximaldruck weitergegeben"),
            },
        };

        /// <summary>Port relations of <paramref name="o"/>: net links and its choice-dependent relations.</summary>
        public static IEnumerable<BoundRelation> Links(SketchObject o, RenderContext ctx)
        {
            var spec = For(o.Type);
            if (spec.Local is not null)
                foreach (var r in spec.Local(o)) yield return BoundRelation.Local(r, o.Id);
            if (spec.Docked is not null)
                foreach (var b in spec.Docked(o, ctx)) yield return b;
            foreach (var b in ValueNets.Of(ctx.Doc, Nets).For(o.Id)) yield return b;
        }

        // ------------------------------------------------------------------ checks (section 4)

        private const double Rel = 1e-6;

        /// <summary>fluid.cyl.force: the required force exceeds p_max·A·η_hm (p_max from the relief valve, else the supply pressure) [S26].</summary>
        private static ValueCheck CylinderForce(string area) => new(
            "fluid.cyl.force", CheckSeverity.Warning,
            T("The required force is above what the cylinder can give at the available pressure (p_max·A·η_hm). Choose a larger bore or a higher pressure.",
              "Die erforderliche Kraft liegt über dem, was der Zylinder beim verfügbaren Druck aufbringt (p_max·A·η_hm). Größeren Kolben oder höheren Druck wählen."),
            "F_req > p_max·A·η_hm",
            c => c["Freq"] is double f && (c["pmax"] ?? c["p"]) is double p && c[area] is double a
                 && f > p * a * (c["etaHm"] ?? 1) * (1 + Rel))
        { Source = "S26" };

        /// <summary>Velocity ranges per line function (textbook values [S26]): suction, return, pressure line.</summary>
        internal static (double Min, double Max, I18nText Kind) VelocityRange(SketchObject line, SketchDocument doc)
        {
            var ends = (line.Attach ?? Array.Empty<Attachment?>()).Where(a => a is not null).Select(a => (Obj: doc.Get(a!.ObjectId), Port: a.AnchorId ?? "")).ToList();
            if (ends.Any(e => e.Obj?.Type == "fluid.pump" && e.Port == "S"))
                return (0.5, 1.5, T("suction line", "Saugleitung"));
            if (line.Str("kind", "working") == "drain" || ends.Any(e => e.Obj?.Type == "fluid.tank" || e.Port is "T" or "R"))
                return (2, 4, T("return line", "Rücklaufleitung"));
            return (3, 6, T("pressure line", "Druckleitung"));
        }

        /// <summary>fluid.velocity: the flow velocity is outside the textbook range of the line's function [S26].</summary>
        private static ValueCheck LineSpeed() => new(
            "fluid.velocity", CheckSeverity.Hint,
            T("The flow velocity is outside the usual range for this line (suction 0.5–1.5 m/s, pressure 3–6 m/s, return 2–4 m/s). Check the inner diameter.",
              "Die Strömungsgeschwindigkeit liegt außerhalb des üblichen Bereichs dieser Leitung (Saugleitung 0,5–1,5 m/s, Druckleitung 3–6 m/s, Rücklauf 2–4 m/s). Innendurchmesser prüfen."),
            "v outside [v_min, v_max] of the line function",
            c =>
            {
                if (c["v"] is not double v || c.Table.Get(c.Object.Id, "v") is not { Provenance: not Provenance.Default }) return false;
                var (min, max, _) = VelocityRange(c.Object, c.Context.Doc);
                return v < min * (1 - Rel) || v > max * (1 + Rel);
            })
        { Source = "S26" };

        /// <summary>fluid.pressure.relief: a pump with values but no relief valve in its pressure net [S25].</summary>
        private static ValueCheck ReliefMissing() => new(
            "fluid.pressure.relief", CheckSeverity.Hint,
            T("No pressure relief valve found at this pump's pressure port: nothing limits the pressure. Add a relief valve.",
              "Am Druckanschluss dieser Pumpe ist kein Druckbegrenzungsventil zu finden: nichts begrenzt den Druck. Druckbegrenzungsventil vorsehen."),
            "pump with values, no relief valve in its pressure net",
            c =>
            {
                if (c.Table.Get(c.Object.Id, "Q") is not { Provenance: Provenance.Entered or Provenance.Derived or Provenance.Linked }
                    && c.Table.Get(c.Object.Id, "p") is not { Provenance: Provenance.Entered or Provenance.Derived or Provenance.Linked }) return false;
                var doc = c.Context.Doc;
                var ports = ValueNets.Of(doc, Nets).PortsWith(c.Object.Id, "P", "pmax");
                if (ports.Count == 0) return false;
                return !ports.Any(x => doc.Get(x.Obj)?.Type == "fluid.valve.relief" && x.Port == "P");
            })
        { Source = "S25" };
    }
}
