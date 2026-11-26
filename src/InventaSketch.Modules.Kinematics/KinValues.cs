using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Kinematics
{
    /// <summary>
    /// Element values of the predefined mechanisms (engineering-calculations.md sections 2.6 and 3.4): variables, relations,
    /// the optional value parameters and the plausibility checks of the four-bar, slider-crank, quick-return mechanism and
    /// scotch yoke. Link lengths and the input angle are the mechanism's own parameters (entering one in the values panel
    /// changes the drawing); everything else is derived from them with the textbook relations of [S24]. The input speed
    /// n is optional; with it the output speed at the current position follows.
    /// <para>
    /// Conventions: lengths in metres, angles in radians, speeds of rotation in rad/s internally (SI); a positive input
    /// speed turns the input angle clockwise on screen, as the angle grip does. Output speeds are signed (same sense as the
    /// input = positive). Angles shown are in degrees.
    /// </para>
    /// </summary>
    internal static class KinValues
    {
        private static I18nText T(string en, string de) => new(en, de);

        /// <summary>The values of one mechanism type: variables, relations, the parameters they add and the checks.</summary>
        internal sealed class Spec
        {
            public Spec(IReadOnlyList<VarDef> variables, IReadOnlyList<Relation> relations, IReadOnlyList<ParamDef> parameters, IReadOnlyList<ValueCheck> checks)
            {
                Variables = variables; Relations = relations; Params = parameters; Checks = checks;
            }

            public IReadOnlyList<VarDef> Variables { get; }
            public IReadOnlyList<Relation> Relations { get; }
            /// <summary>Optional value parameters (text with a unit hint) and the values-label switch, added to the type's parameters.</summary>
            public IReadOnlyList<ParamDef> Params { get; }
            public IReadOnlyList<ValueCheck> Checks { get; }
        }

        private const double Deg = Math.PI / 180;

        // ------------------------------------------------------------------ parameters

        internal static ParamDef Text(string key, I18nText label, string unit) => new(key, label, ParamKind.Text) { Unit = unit, Advanced = true };

        internal static readonly ParamDef PInputSpeed = Text("inputSpeed", T("Input speed", "Antriebsdrehzahl"), "min⁻¹");
        private static readonly ParamDef POutputSpeed = Text("outputSpeed", T("Output angular velocity", "Abtriebswinkelgeschwindigkeit"), "rad/s");
        private static readonly ParamDef PSliderSpeed = Text("sliderSpeed", T("Slider speed", "Schiebergeschwindigkeit"), "m/s");
        private static readonly ParamDef PMaxSliderSpeed = Text("maxSliderSpeed", T("Largest slider speed", "Größte Schiebergeschwindigkeit"), "m/s");

        // ------------------------------------------------------------------ variables

        /// <summary>A link length bound to the mechanism's own number parameter (mm).</summary>
        internal static VarDef Length(string key, string param, I18nText name, string symbol, double defMm) =>
            new(key, name, T(symbol, symbol), Quantities.Length)
            {
                Unit = "mm", StoredUnit = "mm", Param = param, Relevance = VarRelevance.Panel, Default = defMm / 1000, Min = 1e-7, Max = 100,
            };

        /// <summary>The input angle, bound to the parameter "angle" (degrees).</summary>
        internal static VarDef Phi(double defDeg) =>
            new("phi", T("Input angle", "Antriebswinkel"), T("φ", "φ"), Quantities.Angle)
            {
                Unit = "°", StoredUnit = "°", Param = "angle", Relevance = VarRelevance.Panel, Default = defDeg * Deg, Min = -2 * Math.PI, Max = 2 * Math.PI,
            };

        internal static VarDef InputSpeed() =>
            new("n", T("Input speed", "Antriebsdrehzahl"), T("n", "n"), Quantities.RotationalSpeed)
            {
                Unit = "min⁻¹", Param = "inputSpeed", Relevance = VarRelevance.Panel, Min = -1e5, Max = 1e5,
            };

        internal static VarDef Angle(string key, I18nText name, I18nText symbol, VarRelevance rel, double max = Math.PI) =>
            new(key, name, symbol, Quantities.Angle) { Unit = "°", Relevance = rel, Min = 0, Max = max };

        private static VarDef TimeRatioVar() =>
            new("Q", T("Time ratio (slow stroke : fast stroke)", "Zeitverhältnis (langsamer : schneller Hub)"), T("Q", "Q"), Quantities.Ratio) { Relevance = VarRelevance.Card, Min = 1, Max = 1e6 };

        private static VarDef AlphaVar() =>
            Angle("alpha", T("Crank angle between the dead positions", "Kurbelwinkel zwischen den Totlagen"), T("α", "α"), VarRelevance.Engineer);

        private static VarDef StrokeVar() =>
            new("H", T("Stroke", "Hub"), T("H", "H"), Quantities.Length) { Unit = "mm", Relevance = VarRelevance.Card, Min = 0, Max = 100 };

        private static VarDef SliderSpeedVar() =>
            new("v", T("Slider speed at this position", "Schiebergeschwindigkeit in dieser Stellung"), T("v", "v"), Quantities.Speed)
            {
                Unit = "m/s", Param = "sliderSpeed", Relevance = VarRelevance.Card, Min = -1e4, Max = 1e4,
            };

        // ------------------------------------------------------------------ relations

        private static double Rad(double? deg) => deg is double d ? d * Deg : double.NaN;

        /// <summary>Q = (180° + α)/(180° − α) [S24], shared by every mechanism with a time ratio.</summary>
        private static readonly Relation TimeRatio =
            new Relation("kin.timeratio", T("Time ratio", "Zeitverhältnis"), "{Q} = (180° + {alpha})/(180° − {alpha})", "Q", "alpha") { Source = "S24" }
                .Solve("Q", "(180° + {alpha})/(180° − {alpha})", v => (Math.PI + v("alpha")) / (Math.PI - v("alpha")))
                .Solve("alpha", "180°·({Q} − 1)/({Q} + 1)", v => Math.PI * (v("Q") - 1) / (v("Q") + 1));

        // four-bar ------------------------------------------------------------------

        private static readonly Relation GrashofMargin =
            new Relation("kin.fourbar.grashof", T("Grashof condition", "Grashof-Bedingung"), "{dG} = (p + q) − (s + l)", "dG", "a", "b", "c", "d")
            {
                Source = "S24",
                Validity = T("s shortest, l longest, p and q the other two links; ≥ 0: one link turns fully", "s kürzestes, l längstes, p und q die anderen Glieder; ≥ 0: ein Glied läuft um"),
            }
                .Solve("dG", "(p + q) − (s + l)", v => Linkages.GrashofMargin(v("d"), v("a"), v("b"), v("c")));

        /// <summary>
        /// The Grashof class as a text-valued quantity (<see cref="Quantities.Text"/>): one label per <see cref="GrashofClass"/>,
        /// in enum order (the value is the enum's index).
        /// </summary>
        internal static readonly QuantityKind GrashofClassKind = Quantities.Text("kin.grashofClass", T("Grashof class", "Grashof-Klasse"),
            ((GrashofClass[])Enum.GetValues(typeof(GrashofClass))).Select(Linkages.ClassName).ToArray());

        /// <summary>Grashof class from the four link lengths (shortest link and s + l against p + q) [S24].</summary>
        private static readonly Relation GrashofClassRelation =
            new Relation("kin.fourbar.class", T("Grashof class", "Grashof-Klasse"), "{cls} = class(s + l ≶ p + q, shortest link)", "cls", "a", "b", "c", "d")
            {
                Source = "S24",
                Validity = T("crank-driven four-bar: frame d, crank a, coupler b, rocker c", "kurbelgetriebenes Viergelenk: Gestell d, Kurbel a, Koppel b, Schwinge c"),
            }
                .Solve("cls", "class(s + l ≶ p + q, shortest link)", v => (int)Linkages.Classify(v("d"), v("a"), v("b"), v("c")));

        private static readonly Relation MuMin =
            new Relation("kin.fourbar.mumin", T("Smallest transmission angle", "Kleinster Übertragungswinkel"), "{mumin} = arccos(({b}² + {c}² − max(|{d} − {a}|, |{b} − {c}|)²)/(2·{b}·{c}))", "mumin", "a", "b", "c", "d") { Source = "S24" }
                .Solve("mumin", "arccos(({b}² + {c}² − max(|{d} − {a}|, |{b} − {c}|)²)/(2·{b}·{c}))",
                    v => Rad(Linkages.TransmissionAngles(v("d"), v("a"), v("b"), v("c"))?.Min));

        private static readonly Relation MuMax =
            new Relation("kin.fourbar.mumax", T("Largest transmission angle", "Größter Übertragungswinkel"), "{mumax} = arccos(({b}² + {c}² − min({d} + {a}, {b} + {c})²)/(2·{b}·{c}))", "mumax", "a", "b", "c", "d") { Source = "S24" }
                .Solve("mumax", "arccos(({b}² + {c}² − min({d} + {a}, {b} + {c})²)/(2·{b}·{c}))",
                    v => Rad(Linkages.TransmissionAngles(v("d"), v("a"), v("b"), v("c"))?.Max));

        private static readonly Relation Swing =
            new Relation("kin.fourbar.swing", T("Swing angle of the rocker", "Schwingwinkel der Schwinge"),
                "{psi} = arccos(({d}² + {c}² − ({b} + {a})²)/(2·{d}·{c})) − arccos(({d}² + {c}² − ({b} − {a})²)/(2·{d}·{c}))", "psi", "a", "b", "c", "d")
            {
                Source = "S24",
                Validity = T("crank-rocker (the crank turns, the rocker swings)", "Kurbelschwinge (die Kurbel läuft um, die Schwinge schwingt)"),
            }
                .Solve("psi", "arccos(({d}² + {c}² − ({b} + {a})²)/(2·{d}·{c})) − arccos(({d}² + {c}² − ({b} − {a})²)/(2·{d}·{c}))",
                    v => Rad(Linkages.RockerSwing(v("d"), v("a"), v("b"), v("c"))));

        private static readonly Relation FourBarAlpha =
            new Relation("kin.fourbar.alpha", T("Crank angle between the dead positions", "Kurbelwinkel zwischen den Totlagen"),
                "{alpha} = arccos(({d}² + ({b} − {a})² − {c}²)/(2·{d}·({b} − {a}))) − arccos(({d}² + ({b} + {a})² − {c}²)/(2·{d}·({b} + {a})))", "alpha", "a", "b", "c", "d")
            {
                Source = "S24",
                Validity = T("crank-rocker", "Kurbelschwinge"),
            }
                .Solve("alpha", "arccos(({d}² + ({b} − {a})² − {c}²)/(2·{d}·({b} − {a}))) − arccos(({d}² + ({b} + {a})² − {c}²)/(2·{d}·({b} + {a})))",
                    v => Rad(Linkages.CrankAngleDifference(v("d"), v("a"), v("b"), v("c"))));

        private static Relation Ratio(bool crossed) =>
            new Relation(crossed ? "kin.fourbar.ratio.crossed" : "kin.fourbar.ratio.open",
                crossed ? T("Velocity ratio at this position (crossed)", "Übersetzung in dieser Stellung (gekreuzt)") : T("Velocity ratio at this position", "Übersetzung in dieser Stellung"),
                "{i} = {a}·sin({phi} − θ₃)/({c}·sin(θ₄ − θ₃))", "i", "a", "b", "c", "d", "phi")
            {
                Source = "S24",
                Validity = T("θ₃, θ₄: coupler and rocker angles from the position analysis", "θ₃, θ₄: Koppel- und Schwingenwinkel aus der Lageanalyse"),
            }
                .Solve("i", "{a}·sin({phi} − θ₃)/({c}·sin(θ₄ − θ₃))", v =>
                {
                    var p = Linkages.FourBar(v("d"), v("a"), v("b"), v("c"), v("phi") / Deg, crossed);
                    return p is null ? double.NaN : Linkages.VelocityRatio(p.Value);
                });

        public static readonly Relation RatioOpen = Ratio(false);
        public static readonly Relation RatioCrossed = Ratio(true);

        private static readonly Relation OutputSpeed =
            new Relation("kin.fourbar.speed", T("Output angular velocity", "Abtriebswinkelgeschwindigkeit"), "{w} = {i}·{n}", "w", "i", "n") { Source = "S24" }
                .Solve("w", "{i}·{n}", v => v("i") * v("n"))
                .Solve("n", "{w}/{i}", v => v("w") / v("i"))
                .Solve("i", "{w}/{n}", v => v("w") / v("n"));

        // slider-crank -------------------------------------------------------------

        private static readonly Relation SliderStroke =
            new Relation("kin.slidercrank.stroke", T("Stroke of the slider", "Hub des Schiebers"), "{H} = √(({l} + {r})² − {e}²) − √(({l} − {r})² − {e}²)", "H", "r", "l", "e")
            {
                Source = "S24",
                Validity = T("crank turns fully (l ≥ r + |e|); without offset H = 2r", "Kurbel läuft um (l ≥ r + |e|); ohne Versatz H = 2r"),
            }
                .Solve("H", "√(({l} + {r})² − {e}²) − √(({l} − {r})² − {e}²)", v => Linkages.SliderStroke(v("r"), v("l"), v("e")) ?? double.NaN);

        private static readonly Relation SliderAlpha =
            new Relation("kin.slidercrank.alpha", T("Crank angle between the dead positions", "Kurbelwinkel zwischen den Totlagen"), "{alpha} = arcsin(|{e}|/({l} − {r})) − arcsin(|{e}|/({l} + {r}))", "alpha", "r", "l", "e") { Source = "S24" }
                .Solve("alpha", "arcsin(|{e}|/({l} − {r})) − arcsin(|{e}|/({l} + {r}))", v => Rad(Linkages.SliderCrankAngleDifference(v("r"), v("l"), v("e"))));

        private static readonly Relation SliderMu =
            new Relation("kin.slidercrank.mumin", T("Smallest transmission angle", "Kleinster Übertragungswinkel"), "{mumin} = 90° − arcsin(({r} + |{e}|)/{l})", "mumin", "r", "l", "e") { Source = "S24" }
                .Solve("mumin", "90° − arcsin(({r} + |{e}|)/{l})", v => Rad(Linkages.SliderMinTransmission(v("r"), v("l"), v("e"))));

        private static readonly Relation SliderSpeed =
            new Relation("kin.slidercrank.speed", T("Slider speed at this position", "Schiebergeschwindigkeit in dieser Stellung"),
                "{v} = {n}·(−{r}·sin {phi} + {r}·cos {phi}·({e} − {r}·sin {phi})/√({l}² − ({e} − {r}·sin {phi})²))", "v", "n", "r", "l", "e", "phi") { Source = "S24" }
                .Solve("v", "{n}·(−{r}·sin {phi} + {r}·cos {phi}·({e} − {r}·sin {phi})/√({l}² − ({e} − {r}·sin {phi})²))",
                    v => v("n") * Linkages.SliderSpeedFactor(v("r"), v("l"), v("e"), v("phi") / Deg))
                .Solve("n", "{v}/(−{r}·sin {phi} + {r}·cos {phi}·({e} − {r}·sin {phi})/√({l}² − ({e} − {r}·sin {phi})²))",
                    v => v("v") / Linkages.SliderSpeedFactor(v("r"), v("l"), v("e"), v("phi") / Deg));

        private static readonly I18nText ConstantSpeed = T("constant input speed n; derivatives of the exact position analysis (central differences)",
            "konstante Antriebsdrehzahl n; Ableitungen der exakten Lageanalyse (zentrale Differenzen)");

        private static readonly Relation SliderAcc =
            new Relation("kin.slidercrank.acc", T("Slider acceleration at this position", "Schieberbeschleunigung in dieser Stellung"),
                "{acc} = {n}²·d²x_B/dφ²", "acc", "n", "r", "l", "e", "phi") { Source = "S24", Validity = ConstantSpeed }
                .Solve("acc", "{n}²·d²x_B/dφ²", v => v("n") * v("n") * Linkages.SliderAccelerationFactor(v("r"), v("l"), v("e"), v("phi") / Deg));

        private static readonly Relation SliderVmax =
            new Relation("kin.slidercrank.vmax", T("Largest slider speed over a turn", "Größte Schiebergeschwindigkeit über eine Umdrehung"),
                "{vmax} = |{n}|·max|dx_B/dφ|", "vmax", "n", "r", "l", "e") { Source = "S24", Validity = ConstantSpeed }
                .Solve("vmax", "|{n}|·max|dx_B/dφ|", v => Math.Abs(v("n")) * (Linkages.SliderMaxSpeedFactor(v("r"), v("l"), v("e")) ?? double.NaN))
                .Solve("n", "{vmax}/max|dx_B/dφ|", v => v("vmax") / (Linkages.SliderMaxSpeedFactor(v("r"), v("l"), v("e")) ?? double.NaN));

        // four-bar: coupler speed, coupler point speed and acceleration (bound per element in FourBarType.ValueLinks) -------

        private static Relation CouplerSpeed(bool crossed) =>
            new Relation(crossed ? "kin.fourbar.coupler.crossed" : "kin.fourbar.coupler.open",
                crossed ? T("Angular velocity of the coupler (crossed)", "Winkelgeschwindigkeit der Koppel (gekreuzt)") : T("Angular velocity of the coupler", "Winkelgeschwindigkeit der Koppel"),
                "{w3} = {n}·dθ₃/dφ", "w3", "n", "a", "b", "c", "d", "phi") { Source = "S24", Validity = ConstantSpeed }
                .Solve("w3", "{n}·dθ₃/dφ", v => v("n") * Linkages.CouplerRate(v("d"), v("a"), v("b"), v("c"), v("phi") / Deg, crossed));

        public static readonly Relation CouplerSpeedOpen = CouplerSpeed(false);
        public static readonly Relation CouplerSpeedCrossed = CouplerSpeed(true);

        /// <summary>Speed of the coupler point (u along, v across the coupler) at the current position: |n|·|dP/dφ|.</summary>
        public static Relation CouplerPointSpeed(bool crossed, double u, double cv) =>
            new Relation("kin.fourbar.point.speed", T("Speed of the coupler point", "Geschwindigkeit des Koppelpunkts"),
                "{vP} = |{n}|·|dP/dφ|", "vP", "n", "a", "b", "c", "d", "phi") { Source = "S24", Validity = ConstantSpeed }
                .Solve("vP", "|{n}|·|dP/dφ|", v =>
                {
                    var d = Linkages.CouplerPointDerivatives(v("d"), v("a"), v("b"), v("c"), v("phi") / Deg, crossed, u, cv);
                    return d is null ? double.NaN : Math.Abs(v("n")) * d.Value.D1.Length;
                });

        /// <summary>Acceleration of the coupler point at constant input speed: n²·|d²P/dφ²|.</summary>
        public static Relation CouplerPointAcc(bool crossed, double u, double cv) =>
            new Relation("kin.fourbar.point.acc", T("Acceleration of the coupler point", "Beschleunigung des Koppelpunkts"),
                "{aP} = {n}²·|d²P/dφ²|", "aP", "n", "a", "b", "c", "d", "phi") { Source = "S24", Validity = ConstantSpeed }
                .Solve("aP", "{n}²·|d²P/dφ²|", v =>
                {
                    var d = Linkages.CouplerPointDerivatives(v("d"), v("a"), v("b"), v("c"), v("phi") / Deg, crossed, u, cv);
                    return d is null ? double.NaN : v("n") * v("n") * d.Value.D2.Length;
                });

        // quick return -------------------------------------------------------------

        private static readonly Relation LeverSwing =
            new Relation("kin.quickreturn.swing", T("Swing angle of the slotted lever", "Schwingwinkel der Kurbelschwinge"), "{psi} = 2·arcsin({r}/{d})", "psi", "r", "d")
            {
                Source = "S24",
                Validity = T("r < d (the lever swings)", "r < d (die Schwinge schwingt)"),
            }
                .Solve("psi", "2·arcsin({r}/{d})", v => Rad(Linkages.LeverSwing(v("d"), v("r"))));

        private static readonly Relation LeverAlpha =
            new Relation("kin.quickreturn.alpha", T("Crank angle between the dead positions", "Kurbelwinkel zwischen den Totlagen"), "{alpha} = 180° − 2·arccos({r}/{d})", "alpha", "r", "d")
            {
                Source = "S24",
                Validity = T("the ram follows the lever", "der Stößel folgt der Schwinge"),
            }
                .Solve("alpha", "180° − 2·arccos({r}/{d})", v => Rad(Linkages.LeverCrankAngleDifference(v("d"), v("r"))));

        private static readonly Relation RamStroke =
            new Relation("kin.quickreturn.stroke", T("Stroke of the ram", "Hub des Stößels"), "{H} = x_B,max − x_B,min", "H", "d", "r", "L", "l", "h")
            {
                Source = "S24",
                Validity = T("from the position analysis over one turn of the crank", "aus der Lageanalyse über eine Kurbelumdrehung"),
            }
                .Solve("H", "x_B,max − x_B,min", v =>
                {
                    double d = v("d"), r = v("r"), lv = v("L"), l = v("l"), h = v("h");
                    var ext = Linkages.Extremes(a => Linkages.QuickReturnRam(d, r, lv, l, h, a));
                    return ext is { } e ? e.Max - e.Min : double.NaN;
                });

        // scotch yoke ---------------------------------------------------------------

        private static readonly Relation YokeStroke =
            new Relation("kin.scotchyoke.stroke", T("Stroke of the yoke", "Hub der Schleife"), "{H} = 2·{r}", "H", "r") { Source = "S24" }
                .Solve("H", "2·{r}", v => 2 * v("r"))
                .Solve("r", "{H}/2", v => v("H") / 2);

        private static readonly Relation YokeSpeed =
            new Relation("kin.scotchyoke.speed", T("Yoke speed at this position", "Geschwindigkeit der Schleife in dieser Stellung"), "{v} = −{n}·{r}·sin {phi}", "v", "n", "r", "phi") { Source = "S24" }
                .Solve("v", "−{n}·{r}·sin {phi}", v => -v("n") * v("r") * Math.Sin(v("phi")))
                .Solve("n", "−{v}/({r}·sin {phi})", v => -v("v") / (v("r") * Math.Sin(v("phi"))));

        // ------------------------------------------------------------------ checks (section 4)

        private const double Tol = 1e-9;

        /// <summary>Usual design limit of the transmission angle (degrees): μ between 40° and 140° [S24].</summary>
        public const double TransmissionLimit = 40;

        /// <summary>kin.transmission: the transmission angle leaves 40° … 140° somewhere in the motion [S24].</summary>
        public static readonly ValueCheck Transmission = new(
            "kin.transmission", CheckSeverity.Warning,
            T("The transmission angle falls below 40° (or exceeds 140°) during the motion: near these positions the mechanism runs stiffly or jams. Longer coupler or rocker, or a shorter crank, helps.",
              "Der Übertragungswinkel unterschreitet während der Bewegung 40° (oder überschreitet 140°): in diesen Stellungen läuft das Getriebe schwer oder klemmt. Längere Koppel oder Schwinge oder eine kürzere Kurbel hilft."),
            "40° ≤ μ ≤ 140°",
            c => c["mumin"] is double lo && lo < TransmissionLimit * Deg - Tol
                 || c["mumax"] is double hi && hi > (180 - TransmissionLimit) * Deg + Tol)
        { Source = "S24" };

        /// <summary>kin.grashof: the crank of the four-bar cannot turn fully, although it is driven as a crank [S24].</summary>
        public static readonly ValueCheck Grashof = new(
            "kin.grashof", CheckSeverity.Hint,
            T("The crank cannot turn fully with these link lengths: the input link only swings (Grashof: s + l ≤ p + q with the crank or the frame as the shortest link). Positions beyond the limits cannot be assembled.",
              "Die Kurbel kann mit diesen Gliedlängen nicht umlaufen: das Antriebsglied schwingt nur (Grashof: s + l ≤ p + q mit Kurbel oder Gestell als kürzestem Glied). Stellungen außerhalb der Grenzen lassen sich nicht montieren."),
            "s + l ≤ p + q, crank or frame shortest",
            c => c["a"] is double a && c["b"] is double b && c["c"] is double cc && c["d"] is double d && !Linkages.CrankTurnsFully(d, a, b, cc))
        { Source = "S24" };

        // ------------------------------------------------------------------ specs per type

        private static ParamDef[] WithShow(params ParamDef[] ps)
        {
            var list = new List<ParamDef>(ps) { ValueLabels.ShowValues };
            return list.ToArray();
        }

        /// <summary>Four-bar: lengths, input angle and speed; Grashof margin, transmission angles, swing, time ratio, velocity ratio and output speed.</summary>
        public static readonly Spec FourBar = new(
            new[]
            {
                Length("a", "crank", T("Crank length", "Kurbellänge"), "a", 12),
                Length("b", "coupler", T("Coupler length", "Koppellänge"), "b", 36),
                Length("c", "rocker", T("Rocker length", "Schwingenlänge"), "c", 30),
                Length("d", "ground", T("Frame length O2-O4", "Gestelllänge O2-O4"), "d", 40),
                Phi(-60),
                InputSpeed(),
                new VarDef("dG", T("Grashof margin (p + q) − (s + l)", "Grashof-Reserve (p + q) − (s + l)"), T("Δ_G", "Δ_G"), Quantities.Length)
                    { Unit = "mm", Relevance = VarRelevance.Panel, Min = -100, Max = 100 },
                // Text-valued: shown as "Crank-rocker", never entered (the parameter key is not a parameter of the type).
                new VarDef("cls", T("Grashof class", "Grashof-Klasse"), T("Class", "Klasse"), GrashofClassKind)
                    { Param = "grashofClass", Relevance = VarRelevance.Panel },
                Angle("mumin", T("Smallest transmission angle", "Kleinster Übertragungswinkel"), T("μ_min", "μ_min"), VarRelevance.Card),
                Angle("mumax", T("Largest transmission angle", "Größter Übertragungswinkel"), T("μ_max", "μ_max"), VarRelevance.Card),
                Angle("psi", T("Swing angle of the rocker", "Schwingwinkel der Schwinge"), T("ψ", "ψ"), VarRelevance.Card),
                AlphaVar(),
                TimeRatioVar(),
                new VarDef("i", T("Velocity ratio ω₄/ω₂ at this position", "Übersetzung ω₄/ω₂ in dieser Stellung"), T("i", "i"), Quantities.Ratio)
                    { Relevance = VarRelevance.Panel, Min = -1e6, Max = 1e6 },
                new VarDef("w", T("Output angular velocity at this position", "Abtriebswinkelgeschwindigkeit in dieser Stellung"), T("ω₄", "ω₄"), Quantities.RotationalSpeed)
                    { Unit = "rad/s", Param = "outputSpeed", Relevance = VarRelevance.Panel, Min = -1e5, Max = 1e5 },
                new VarDef("w3", T("Angular velocity of the coupler at this position", "Winkelgeschwindigkeit der Koppel in dieser Stellung"), T("ω₃", "ω₃"), Quantities.RotationalSpeed)
                    { Unit = "rad/s", Relevance = VarRelevance.Panel, Min = -1e5, Max = 1e5 },
                new VarDef("vP", T("Speed of the coupler point at this position", "Geschwindigkeit des Koppelpunkts in dieser Stellung"), T("v_P", "v_P"), Quantities.Speed)
                    { Unit = "m/s", Relevance = VarRelevance.Panel, Min = 0, Max = 1e4 },
                new VarDef("aP", T("Acceleration of the coupler point at this position (constant n)", "Beschleunigung des Koppelpunkts in dieser Stellung (n konstant)"), T("a_P", "a_P"), Quantities.Acceleration)
                    { Unit = "m/s²", Relevance = VarRelevance.Panel, Min = 0, Max = 1e7 },
            },
            new[] { GrashofMargin, GrashofClassRelation, MuMin, MuMax, Swing, FourBarAlpha, TimeRatio, OutputSpeed },
            WithShow(PInputSpeed, POutputSpeed),
            new[] { Transmission, Grashof });

        /// <summary>Slider-crank: lengths, input angle and speed; stroke, time ratio, smallest transmission angle and slider speed.</summary>
        public static readonly Spec SliderCrank = new(
            new[]
            {
                Length("r", "crank", T("Crank length", "Kurbellänge"), "r", 12),
                Length("l", "rod", T("Connecting rod length", "Pleuellänge"), "l", 36),
                Length("e", "offset", T("Offset (eccentricity)", "Versatz (Exzentrizität)"), "e", 0) with { Min = -100 },
                Phi(-60),
                InputSpeed(),
                StrokeVar(),
                AlphaVar(),
                TimeRatioVar(),
                Angle("mumin", T("Smallest transmission angle", "Kleinster Übertragungswinkel"), T("μ_min", "μ_min"), VarRelevance.Card),
                SliderSpeedVar(),
                new VarDef("vmax", T("Largest slider speed over a turn", "Größte Schiebergeschwindigkeit über eine Umdrehung"), T("v_max", "v_max"), Quantities.Speed)
                    { Unit = "m/s", Param = "maxSliderSpeed", Relevance = VarRelevance.Panel, Min = 0, Max = 1e4 },
                new VarDef("acc", T("Slider acceleration at this position (constant n)", "Schieberbeschleunigung in dieser Stellung (n konstant)"), T("a", "a"), Quantities.Acceleration)
                    { Unit = "m/s²", Relevance = VarRelevance.Panel, ReadOnly = true, Min = -1e7, Max = 1e7 },
            },
            new[] { SliderStroke, SliderAlpha, TimeRatio, SliderMu, SliderSpeed, SliderVmax, SliderAcc },
            WithShow(PInputSpeed, PSliderSpeed, PMaxSliderSpeed),
            new[] { Transmission });

        /// <summary>Crank and slotted lever: lengths and input angle; lever swing, time ratio and ram stroke.</summary>
        public static readonly Spec QuickReturn = new(
            new[]
            {
                Length("d", "ground", T("Pivot distance O2-O4", "Gestellabstand O2-O4"), "d", 20),
                Length("r", "crank", T("Crank length", "Kurbellänge"), "r", 10),
                Length("L", "lever", T("Lever length", "Schwingenlänge"), "L", 40),
                Length("l", "link", T("Link length", "Koppellänge"), "l", 16),
                Length("h", "ram", T("Ram guide height above O4", "Höhe Stößelführung über O4"), "h", 44),
                Phi(-30),
                Angle("psi", T("Swing angle of the lever", "Schwingwinkel der Schwinge"), T("ψ", "ψ"), VarRelevance.Card),
                AlphaVar(),
                TimeRatioVar(),
                StrokeVar(),
            },
            new[] { LeverSwing, LeverAlpha, TimeRatio, RamStroke },
            WithShow(),
            Array.Empty<ValueCheck>());

        /// <summary>Scotch yoke: crank and input angle; stroke H = 2r and the yoke speed at the current position.</summary>
        public static readonly Spec ScotchYoke = new(
            new[]
            {
                Length("r", "crank", T("Crank length", "Kurbellänge"), "r", 12),
                Phi(-60),
                InputSpeed(),
                StrokeVar(),
                SliderSpeedVar() with { Name = T("Yoke speed at this position", "Geschwindigkeit der Schleife in dieser Stellung") },
            },
            new[] { YokeStroke, YokeSpeed },
            WithShow(PInputSpeed, PSliderSpeed),
            Array.Empty<ValueCheck>());
    }
}
