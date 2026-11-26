using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Automotive
{
    /// <summary>
    /// Vehicle dynamics values (engineering calculations, section 1; 2026-11-26, round 7): driving resistance and power demand
    /// (<c>auto.motion.resistance</c>), turning circle with lateral acceleration (<c>auto.motion.turning</c>), the range of a
    /// battery pack (<c>auto.sub.battery</c>), the sensor range of a field of view against the stopping distance
    /// (<c>auto.fov</c>) and the friction limit of the braking deceleration. Textbook vehicle mechanics for sketches (steady
    /// state, single-track model at low speed, constant deceleration), not a validated driving function; no catalogued source
    /// is cited (section 9 has no vehicle dynamics textbook yet).
    /// </summary>
    internal static class DynValues
    {
        private static I18nText T(string en, string de) => new(en, de);

        /// <summary>Standard acceleration of gravity (m/s², [S1] conventional value).</summary>
        public const double G = 9.80665;

        private static ParamDef Text(string key, I18nText label, string unit, bool advanced = true) => new(key, label, ParamKind.Text) { Unit = unit, Advanced = advanced };

        private static VarDef V(string key, I18nText name, I18nText sym, QuantityKind kind, string? unit, string param, VarRelevance rel = VarRelevance.Card)
            => new(key, name, sym, kind) { Unit = unit, Param = param, Relevance = rel };

        // ------------------------------------------------------------------ driving resistance

        private static readonly I18nText RSpeed = T("Vehicle speed", "Fahrgeschwindigkeit");
        private static readonly I18nText RMass = T("Vehicle mass", "Fahrzeugmasse");
        private static readonly I18nText RTotal = T("Driving resistance (total)", "Fahrwiderstand (gesamt)");
        private static readonly I18nText RPower = T("Power demand at the wheels", "Leistungsbedarf an den Rädern");
        private static readonly I18nText RCw = T("Drag coefficient c_w (default 0.30)", "Luftwiderstandsbeiwert c_w (Vorgabe 0,30)");
        private static readonly I18nText RArea = T("Frontal area (default 2.2 m²)", "Stirnfläche (Vorgabe 2,2 m²)");
        private static readonly I18nText RRoll = T("Rolling resistance coefficient (default 0.012, car tyre on asphalt)", "Rollwiderstandsbeiwert (Vorgabe 0,012, Pkw-Reifen auf Asphalt)");
        private static readonly I18nText RGrade = T("Gradient (default 0 %; uphill positive)", "Steigung (Vorgabe 0 %; bergauf positiv)");
        private static readonly I18nText RRho = T("Air density (default 1.2 kg/m³)", "Luftdichte (Vorgabe 1,2 kg/m³)");
        private static readonly I18nText RAcc = T("Acceleration (default 0, steady speed)", "Beschleunigung (Vorgabe 0, konstante Geschwindigkeit)");
        private static readonly I18nText RRollF = T("Rolling resistance", "Rollwiderstand");
        private static readonly I18nText RAirF = T("Air drag", "Luftwiderstand");
        private static readonly I18nText RGradeF = T("Gradient resistance", "Steigungswiderstand");
        private static readonly I18nText RAccF = T("Acceleration resistance (translational mass only)", "Beschleunigungswiderstand (nur translatorische Masse)");
        private static readonly I18nText RPmax = T("Available drive power (at the wheels)", "Verfügbare Antriebsleistung (an den Rädern)");

        public static readonly IReadOnlyList<VarDef> ResistanceVars = new[]
        {
            V("v", RSpeed, T("v", "v"), Quantities.Speed, "km/h", "speed") with { Min = 0, Max = 150 },
            V("m", RMass, T("m", "m"), Quantities.Mass_, "kg", "mass") with { Min = 1, Max = 1e6 },
            V("F", RTotal, T("F", "F_W"), Quantities.Force, "N", "force") with { Min = -1e7, Max = 1e7 },
            V("P", RPower, T("P", "P"), Quantities.Power, "kW", "power") with { Min = -1e9, Max = 1e9 },
            V("cw", RCw, T("c_w", "c_w"), Quantities.Ratio, null, "cw", VarRelevance.Panel) with { Default = 0.30, Min = 0.05, Max = 2 },
            V("A", RArea, T("A", "A"), Quantities.Area, "m²", "frontalArea", VarRelevance.Panel) with { Default = 2.2, Min = 0.1, Max = 50 },
            V("fr", RRoll, T("f_R", "f_R"), Quantities.Ratio, null, "rolling", VarRelevance.Panel) with { Default = 0.012, Min = 0.001, Max = 0.5 },
            V("q", RGrade, T("q", "q"), Quantities.Ratio, "%", "gradient", VarRelevance.Panel) with { Default = 0, Min = -1, Max = 1 },
            V("rho", RRho, T("ρ", "ρ"), Quantities.Density, "kg/m³", "airDensity", VarRelevance.Engineer) with { Default = 1.2, Min = 0.5, Max = 2 },
            V("a", RAcc, T("a", "a"), Quantities.Acceleration, "m/s²", "accel", VarRelevance.Panel) with { Default = 0, Min = -15, Max = 15 },
            V("FR", RRollF, T("F_roll", "F_R"), Quantities.Force, "N", "forceRoll", VarRelevance.Panel) with { Min = 0, Max = 1e7 },
            V("FL", RAirF, T("F_air", "F_L"), Quantities.Force, "N", "forceAir", VarRelevance.Panel) with { Min = 0, Max = 1e7 },
            V("FSt", RGradeF, T("F_grade", "F_St"), Quantities.Force, "N", "forceGrade", VarRelevance.Panel) with { Min = -1e7, Max = 1e7 },
            V("FA", RAccF, T("F_acc", "F_B"), Quantities.Force, "N", "forceAccel", VarRelevance.Engineer) with { Min = -1e7, Max = 1e7 },
            V("Pmax", RPmax, T("P_max", "P_max"), Quantities.Power, "kW", "powerMax", VarRelevance.Panel) with { Min = 0, Max = 1e9 },
        };

        public static readonly IReadOnlyList<ParamDef> ResistanceParams = new[]
        {
            Text("speed", RSpeed, "km/h", false), Text("mass", RMass, "kg", false), Text("gradient", RGrade, "%", false),
            Text("cw", RCw, ""), Text("frontalArea", RArea, "m²"), Text("rolling", RRoll, ""), Text("airDensity", RRho, "kg/m³"),
            Text("accel", RAcc, "m/s²"), Text("powerMax", RPmax, "kW"),
            Text("force", RTotal, "N"), Text("power", RPower, "kW"), Text("forceRoll", RRollF, "N"), Text("forceAir", RAirF, "N"),
            Text("forceGrade", RGradeF, "N"), Text("forceAccel", RAccF, "N"),
            ValueLabels.ShowValues,
        };

        private static double Cos(double q) => 1 / Math.Sqrt(1 + q * q);
        private static double Sin(double q) => q / Math.Sqrt(1 + q * q);

        /// <summary>auto.resistance.roll: F_roll = m·g·f_R·cos α with tan α = q.</summary>
        public static readonly Relation RollRelation =
            new Relation("auto.resistance.roll", RRollF, "{FR} = {m}·g·{fr}·cos(arctan {q})", "FR", "m", "fr", "q")
            {
                Source = "S45", Validity = T("Free rolling, rolling resistance coefficient independent of speed", "Freies Rollen, Rollwiderstandsbeiwert unabhängig von der Geschwindigkeit"),
            }
            .Solve("FR", "{m}·g·{fr}·cos(arctan {q})", v => v("m") * G * v("fr") * Cos(v("q")))
            .Solve("m", "{FR}/(g·{fr}·cos(arctan {q}))", v => v("FR") / (G * v("fr") * Cos(v("q"))))
            .Solve("fr", "{FR}/({m}·g·cos(arctan {q}))", v => v("FR") / (v("m") * G * Cos(v("q"))));

        /// <summary>auto.resistance.air: F_air = ½·ρ·c_w·A·v² (still air).</summary>
        public static readonly Relation AirRelation =
            new Relation("auto.resistance.air", RAirF, "{FL} = ½·{rho}·{cw}·{A}·{v}²", "FL", "rho", "cw", "A", "v")
            {
                Source = "S45", Validity = T("Still air (no wind), speed relative to the air", "Ruhende Luft (kein Wind), Geschwindigkeit gegenüber der Luft"),
            }
            .Solve("FL", "½·{rho}·{cw}·{A}·{v}²", v => 0.5 * v("rho") * v("cw") * v("A") * v("v") * v("v"))
            .Solve("v", "√(2·{FL}/({rho}·{cw}·{A}))", v => Math.Sqrt(2 * v("FL") / (v("rho") * v("cw") * v("A"))))
            .Solve("cw", "2·{FL}/({rho}·{A}·{v}²)", v => 2 * v("FL") / (v("rho") * v("A") * v("v") * v("v")))
            .Solve("A", "2·{FL}/({rho}·{cw}·{v}²)", v => 2 * v("FL") / (v("rho") * v("cw") * v("v") * v("v")))
            .Solve("rho", "2·{FL}/({cw}·{A}·{v}²)", v => 2 * v("FL") / (v("cw") * v("A") * v("v") * v("v")));

        /// <summary>auto.resistance.grade: F_grade = m·g·sin α with tan α = q (negative downhill).</summary>
        public static readonly Relation GradeRelation =
            new Relation("auto.resistance.grade", RGradeF, "{FSt} = {m}·g·sin(arctan {q})", "FSt", "m", "q") { Source = "S45" }
            .Solve("FSt", "{m}·g·sin(arctan {q})", v => v("m") * G * Sin(v("q")))
            .Solve("q", "tan(arcsin({FSt}/({m}·g)))", v => Math.Tan(Math.Asin(Geo.Clamp(v("FSt") / (v("m") * G), -0.999, 0.999))))
            .Solve("m", "{FSt}/(g·sin(arctan {q}))", v => v("FSt") / (G * Sin(v("q"))));

        /// <summary>auto.resistance.accel: F_acc = m·a (rotating masses neglected).</summary>
        public static readonly Relation AccelRelation =
            new Relation("auto.resistance.accel", RAccF, "{FA} = {m}·{a}", "FA", "m", "a")
            {
                Source = "S45", Validity = T("Translational mass only; the rotating masses (wheels, drivetrain) add a few per cent", "Nur translatorische Masse; die rotierenden Massen (Räder, Antriebsstrang) kommen mit einigen Prozent hinzu"),
            }
            .Solve("FA", "{m}·{a}", v => v("m") * v("a"))
            .Solve("a", "{FA}/{m}", v => v("FA") / v("m"))
            .Solve("m", "{FA}/{a}", v => v("FA") / v("a"));

        /// <summary>auto.resistance.total: F = F_roll + F_air + F_grade + F_acc.</summary>
        public static readonly Relation TotalRelation =
            new Relation("auto.resistance.total", RTotal, "{F} = {FR} + {FL} + {FSt} + {FA}", "F", "FR", "FL", "FSt", "FA") { Source = "S45" }
            .Solve("F", "{FR} + {FL} + {FSt} + {FA}", v => v("FR") + v("FL") + v("FSt") + v("FA"))
            .Solve("FR", "{F} − {FL} − {FSt} − {FA}", v => v("F") - v("FL") - v("FSt") - v("FA"))
            .Solve("FL", "{F} − {FR} − {FSt} − {FA}", v => v("F") - v("FR") - v("FSt") - v("FA"))
            .Solve("FSt", "{F} − {FR} − {FL} − {FA}", v => v("F") - v("FR") - v("FL") - v("FA"))
            .Solve("FA", "{F} − {FR} − {FL} − {FSt}", v => v("F") - v("FR") - v("FL") - v("FSt"));

        /// <summary>auto.resistance.power: P = F·v (power at the wheels).</summary>
        public static readonly Relation PowerRelation =
            new Relation("auto.resistance.power", RPower, "{P} = {F}·{v}", "P", "F", "v") { Source = "S45" }
            .Solve("P", "{F}·{v}", v => v("F") * v("v"))
            .Solve("F", "{P}/{v}", v => v("P") / v("v"))
            .Solve("v", "{P}/{F}", v => v("P") / v("F"));

        public static readonly IReadOnlyList<Relation> ResistanceRelations = new[] { RollRelation, AirRelation, GradeRelation, AccelRelation, TotalRelation, PowerRelation };

        /// <summary>auto.drive.power: the power demand at the wheels exceeds the available drive power.</summary>
        public static readonly ValueCheck DrivePower = new("auto.drive.power", CheckSeverity.Warning,
            T("The power demand P at the wheels is above the available drive power P_max: the vehicle cannot hold this speed on this gradient (or accelerate as entered). Reduce the speed or the gradient, or enter a stronger drive.",
              "Der Leistungsbedarf P an den Rädern liegt über der verfügbaren Antriebsleistung P_max: Das Fahrzeug kann diese Geschwindigkeit an dieser Steigung nicht halten (oder nicht wie eingetragen beschleunigen). Geschwindigkeit oder Steigung verringern oder einen stärkeren Antrieb eintragen."),
            "P ≤ P_max", c => c["P"] is double p && c["Pmax"] is double pm && p > pm * (1 + 1e-9));

        public static readonly IReadOnlyList<ValueCheck> ResistanceChecks = new[] { DrivePower };

        // ------------------------------------------------------------------ turning circle and lateral acceleration

        private static readonly I18nText TWheelbase = T("Wheelbase", "Radstand");
        private static readonly I18nText TSteer = T("Steering angle (mean of the front wheels)", "Lenkwinkel (Mittel der Vorderräder)");
        private static readonly I18nText TRadius = T("Turning radius (path of the front axle centre)", "Wendekreisradius (Bahn der Vorderachsmitte)");
        private static readonly I18nText TRearRadius = T("Radius of the rear axle centre", "Radius der Hinterachsmitte");
        private static readonly I18nText TTrack = T("Track width (default 1.6 m)", "Spurweite (Vorgabe 1,6 m)");
        private static readonly I18nText TCircle = T("Turning circle (track of the outer front wheel)", "Spurkreis (Spur des kurvenäußeren Vorderrads)");
        private static readonly I18nText TSpeed = T("Speed in the curve", "Geschwindigkeit in der Kurve");
        private static readonly I18nText TLateral = T("Lateral acceleration", "Querbeschleunigung");
        private static readonly I18nText TMu = T("Tyre–road friction coefficient μ (dry asphalt about 0.8 … 1.0, wet 0.5 … 0.7, snow 0.2)",
            "Haftreibungsbeiwert μ Reifen–Fahrbahn (trockener Asphalt etwa 0,8 … 1,0, nass 0,5 … 0,7, Schnee 0,2)");

        public static readonly IReadOnlyList<VarDef> TurningVars = new[]
        {
            V("L", TWheelbase, T("L", "L"), Quantities.Length, "m", "wheelbase") with { Min = 0.3, Max = 30 },
            V("delta", TSteer, T("δ", "δ"), Quantities.Angle, "°", "steer") with { Min = Geo.DegToRad(0.5), Max = Geo.DegToRad(80) },
            V("R", TRadius, T("R", "R"), Quantities.Length, "m", "radius") with { Min = 0.3, Max = 1e5 },
            V("D", TCircle, T("D", "D"), Quantities.Length, "m", "circle") with { Min = 0.3, Max = 2e5 },
            V("Rh", TRearRadius, T("R_r", "R_h"), Quantities.Length, "m", "radiusRear", VarRelevance.Panel) with { Min = 0, Max = 1e5 },
            V("b", TTrack, T("b", "b"), Quantities.Length, "m", "track", VarRelevance.Panel) with { Default = 1.6, Min = 0.1, Max = 5 },
            V("v", TSpeed, T("v", "v"), Quantities.Speed, "km/h", "speed", VarRelevance.Panel) with { Min = 0, Max = 150 },
            V("ay", TLateral, T("a_y", "a_y"), Quantities.Acceleration, "m/s²", "lateral", VarRelevance.Panel) with { Min = 0, Max = 100 },
            V("mu", TMu, T("μ", "μ"), Quantities.Ratio, null, "friction", VarRelevance.Panel) with { Min = 0.05, Max = 2 },
        };

        public static readonly IReadOnlyList<ParamDef> TurningParams = new[]
        {
            Text("wheelbase", TWheelbase, "m", false), Text("steer", TSteer, "°", false), Text("radius", TRadius, "m", false),
            Text("circle", TCircle, "m"), Text("radiusRear", TRearRadius, "m"), Text("track", TTrack, "m"), Text("speed", TSpeed, "km/h"),
            Text("lateral", TLateral, "m/s²"), Text("friction", TMu, ""),
            ValueLabels.ShowValues,
        };

        private static readonly I18nText SingleTrack = T("Single-track model at low speed (kinematic steering, no tyre slip angles)",
            "Einspurmodell bei kleiner Geschwindigkeit (kinematische Lenkung, ohne Schräglaufwinkel)");

        /// <summary>auto.turn.radius: R = L / sin δ (front axle centre).</summary>
        public static readonly Relation RadiusRelation =
            new Relation("auto.turn.radius", TRadius, "{R} = {L}/sin {delta}", "R", "L", "delta") { Source = "S45", Validity = SingleTrack }
            .Solve("R", "{L}/sin {delta}", v => v("L") / Math.Sin(v("delta")))
            .Solve("L", "{R}·sin {delta}", v => v("R") * Math.Sin(v("delta")))
            .Solve("delta", "arcsin({L}/{R})", v => Math.Asin(Geo.Clamp(v("L") / v("R"), -1, 1)));

        /// <summary>auto.turn.rear: R_r = L / tan δ (rear axle centre, on the line of the rear axle).</summary>
        public static readonly Relation RearRadiusRelation =
            new Relation("auto.turn.rear", TRearRadius, "{Rh} = {L}/tan {delta}", "Rh", "L", "delta") { Source = "S45", Validity = SingleTrack }
            .Solve("Rh", "{L}/tan {delta}", v => v("L") / Math.Tan(v("delta")))
            .Solve("L", "{Rh}·tan {delta}", v => v("Rh") * Math.Tan(v("delta")))
            .Solve("delta", "arctan({L}/{Rh})", v => Math.Atan(v("L") / v("Rh")));

        /// <summary>auto.turn.circle: D = 2·√((R_r + b/2)² + L²) (outer front wheel).</summary>
        public static readonly Relation CircleRelation =
            new Relation("auto.turn.circle", TCircle, "{D} = 2·√(({Rh} + {b}/2)² + {L}²)", "D", "Rh", "b", "L")
            {
                Source = "S45", Validity = T("Track circle of the outer front wheel; the wall-to-wall circle adds the front overhang", "Spurkreis des kurvenäußeren Vorderrads; der Wendekreis von Wand zu Wand kommt um den vorderen Überhang hinzu"),
            }
            .Solve("D", "2·√(({Rh} + {b}/2)² + {L}²)", v => 2 * Math.Sqrt(Math.Pow(v("Rh") + v("b") / 2, 2) + v("L") * v("L")))
            .Solve("Rh", "√(({D}/2)² − {L}²) − {b}/2", v => Math.Sqrt(Math.Max(0, Math.Pow(v("D") / 2, 2) - v("L") * v("L"))) - v("b") / 2);

        /// <summary>auto.turn.lateral: a_y = v²/R.</summary>
        public static readonly Relation LateralRelation =
            new Relation("auto.turn.lateral", TLateral, "{ay} = {v}²/{R}", "ay", "v", "R") { Source = "S45" }
            .Solve("ay", "{v}²/{R}", v => v("v") * v("v") / v("R"))
            .Solve("v", "√({ay}·{R})", v => Math.Sqrt(v("ay") * v("R")))
            .Solve("R", "{v}²/{ay}", v => v("v") * v("v") / v("ay"));

        public static readonly IReadOnlyList<Relation> TurningRelations = new[] { RadiusRelation, RearRadiusRelation, CircleRelation, LateralRelation };

        /// <summary>auto.turn.friction: the lateral acceleration exceeds the friction limit μ·g.</summary>
        public static readonly ValueCheck TurnFriction = new("auto.turn.friction", CheckSeverity.Warning,
            T("The lateral acceleration a_y = v²/R is above μ·g: the tyres cannot hold the curve and the vehicle slides. Reduce the speed or drive a larger radius.",
              "Die Querbeschleunigung a_y = v²/R liegt über μ·g: Die Reifen können die Kurve nicht halten, das Fahrzeug rutscht. Geschwindigkeit verringern oder einen größeren Radius fahren."),
            "a_y ≤ μ·g", c => c["ay"] is double ay && c["mu"] is double mu && ay > mu * G * (1 + 1e-9));

        public static readonly IReadOnlyList<ValueCheck> TurningChecks = new[] { TurnFriction };

        // ------------------------------------------------------------------ battery range

        private static readonly I18nText EEnergy = T("Usable battery energy", "Nutzbare Batterieenergie");
        private static readonly I18nText EConsumption = T("Energy consumption per 100 km", "Energieverbrauch je 100 km");
        private static readonly I18nText ERange = T("Range", "Reichweite");
        private static readonly I18nText ETrip = T("Planned trip distance", "Geplante Fahrstrecke");

        public static readonly IReadOnlyList<VarDef> RangeVars = new[]
        {
            V("E", EEnergy, T("E", "E"), Quantities.Energy, "kWh", "energy") with { Min = 0, Max = 1e10 },
            V("c", EConsumption, T("c", "c"), Quantities.Energy, "kWh", "consumption") with { Min = 1e5, Max = 1e9 },
            V("R", ERange, T("R", "R"), Quantities.Length, "km", "range") with { Min = 0, Max = 1e8 },
            V("d", ETrip, T("d", "d"), Quantities.Length, "km", "trip", VarRelevance.Panel) with { Min = 0, Max = 1e8 },
        };

        public static readonly IReadOnlyList<ParamDef> RangeParams = new[]
        {
            Text("energy", EEnergy, "kWh"), Text("consumption", EConsumption, "kWh"), Text("range", ERange, "km"), Text("trip", ETrip, "km"),
            ValueLabels.ShowValues,
        };

        /// <summary>auto.ev.range: R = E / c · 100 km.</summary>
        public static readonly Relation RangeRelation =
            new Relation("auto.ev.range", T("Range from battery energy and consumption", "Reichweite aus Batterieenergie und Verbrauch"), "{R} = {E}/{c}·100 km", "R", "E", "c")
            {
                Source = "S45", Validity = T("Mean consumption over the trip (speed profile, temperature and auxiliary loads included in c)", "Mittlerer Verbrauch über die Fahrt (Geschwindigkeitsprofil, Temperatur und Nebenverbraucher in c enthalten)"),
            }
            .Solve("R", "{E}/{c}·100 km", v => v("E") / v("c") * 1e5)
            .Solve("E", "{R}·{c}/100 km", v => v("R") * v("c") / 1e5)
            .Solve("c", "{E}·100 km/{R}", v => v("E") * 1e5 / v("R"));

        public static readonly IReadOnlyList<Relation> RangeRelations = new[] { RangeRelation };

        /// <summary>auto.ev.range: the range is shorter than the planned trip.</summary>
        public static readonly ValueCheck RangeShort = new("auto.ev.range", CheckSeverity.Warning,
            T("The range R is shorter than the planned trip d: a charging stop is needed (or more usable energy, or less consumption).",
              "Die Reichweite R ist kürzer als die geplante Fahrstrecke d: Ein Ladehalt ist nötig (oder mehr nutzbare Energie oder weniger Verbrauch)."),
            "R ≥ d", c => c["R"] is double r && c["d"] is double d && r < d * (1 - 1e-9));

        public static readonly IReadOnlyList<ValueCheck> RangeChecks = new[] { RangeShort };

        // ------------------------------------------------------------------ sensor range against the stopping distance

        private static readonly I18nText FRange = T("Sensor range (drawn range at the scale, or entered)", "Sensorreichweite (gezeichnete Reichweite im Maßstab oder eingegeben)");
        private static readonly I18nText FPreview = T("Preview time (sensor range / speed)", "Vorausschauzeit (Sensorreichweite / Geschwindigkeit)");

        /// <summary>Field-of-view values: the braking variables with the sensor range in place of the gap, and the preview time.</summary>
        public static readonly IReadOnlyList<VarDef> FovVars = new[]
        {
            AutoValues.BrakingVars[0],
            V("rS", FRange, T("r_S", "r_S"), Quantities.Length, "m", "sensorRange") with { Min = 0, Max = 1e4 },
            AutoValues.BrakingVars[1],
            V("tP", FPreview, T("t_P", "t_V"), Quantities.Time_, "s", "preview") with { Min = 0, Max = 1e5 },
        }.Concat(AutoValues.BrakingVars.Skip(3)).ToList();

        public static readonly IReadOnlyList<ParamDef> FovParams = new[]
        {
            Text("speed", T("Speed of the ego vehicle", "Geschwindigkeit des Egofahrzeugs"), "km/h"), Text("sensorRange", FRange, "m"),
            Text("reaction", T("Reaction time (default 1 s)", "Reaktionszeit (Vorgabe 1 s)"), "s"),
            Text("decel", T("Braking deceleration (default 8 m/s²)", "Bremsverzögerung (Vorgabe 8 m/s²)"), "m/s²"),
            Text("stopping", T("Stopping distance", "Anhalteweg"), "m"), Text("preview", FPreview, "s"),
            Text("reactionDistance", T("Reaction distance", "Reaktionsweg"), "m"), Text("brakingDistance", T("Braking distance", "Bremsweg"), "m"),
            Text("friction", TMu, ""),
            new ParamDef("scale", T("Drawing scale (mm per m)", "Zeichnungsmaßstab (mm je m)"), ParamKind.Number) { Default = 10.0, Min = 0.01, Max = 1000, Unit = "mm/m", Step = 1, Advanced = true },
            ValueLabels.ShowValues,
        };

        /// <summary>auto.fov.preview: t_P = r_S / v.</summary>
        public static readonly Relation PreviewRelation =
            new Relation("auto.fov.preview", FPreview, "{tP} = {rS}/{v}", "tP", "rS", "v") { Source = "S45" }
            .Solve("tP", "{rS}/{v}", v => v("rS") / v("v"))
            .Solve("rS", "{tP}·{v}", v => v("tP") * v("v"))
            .Solve("v", "{rS}/{tP}", v => v("rS") / v("tP"));

        public static readonly IReadOnlyList<Relation> FovRelations = AutoValues.BrakingRelations.Concat(new[] { PreviewRelation }).ToList();

        /// <summary>auto.fov.range: the sensor sees less far than the vehicle needs to stop.</summary>
        public static readonly ValueCheck FovRange = new("auto.fov.range", CheckSeverity.Warning,
            T("The sensor range r_S is shorter than the stopping distance s at this speed: an object appearing at the edge of the field of view cannot be avoided by braking. Reduce the speed or use a sensor with a longer range.",
              "Die Sensorreichweite r_S ist kürzer als der Anhalteweg s bei dieser Geschwindigkeit: Vor einem am Rand des Sichtbereichs auftauchenden Objekt kann nicht mehr angehalten werden. Geschwindigkeit verringern oder einen Sensor mit größerer Reichweite verwenden."),
            "s ≤ r_S", c => c["s"] is double s && c["rS"] is double r && s > r * (1 + 1e-9));

        public static readonly IReadOnlyList<ValueCheck> FovChecks = new[] { FovRange, AutoValues.BrakingFriction };

        /// <summary>The drawn range of a field of view in metres at its scale (mm per m).</summary>
        public static double DrawnRange(SketchObject o) => Geo.Clamp(o.Num("range", 60), 0.5, 5000) / Math.Max(0.01, o.Num("scale", 10));

        /// <summary>The field of view's sensor range from the drawing, unless it is entered.</summary>
        public static IEnumerable<BoundRelation> FovLinks(SketchObject o)
        {
            if (ValueModel.ReadEntered(o, FovVars[1]).Value is not null) yield break;
            var m = DrawnRange(o);
            if (!(m > 0) || double.IsInfinity(m)) yield break;
            var expr = Geo.Clamp(o.Num("range", 60), 0.5, 5000).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + " mm / "
                       + Math.Max(0.01, o.Num("scale", 10)).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + " mm/m";
            var rel = new Relation("auto.fov.drawn", T("Sensor range from the drawing at the scale", "Sensorreichweite aus der Zeichnung im Maßstab"), "{rS} = " + expr, "rS")
                .Solve("rS", expr, _ => m);
            yield return BoundRelation.Local(rel, o.Id);
        }
    }
}
