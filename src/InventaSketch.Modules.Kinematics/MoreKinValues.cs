using System;
using System.Collections.Generic;
using InventaSketch.Core;

namespace InventaSketch.Modules.Kinematics
{
    /// <summary>
    /// Element values of the Geneva drive and the six-bar linkage (engineering-calculations.md sections 2.6 and 3.4), in
    /// the conventions of <see cref="KinValues"/>: lengths in metres, angles in radians, speeds of rotation in rad/s
    /// internally; a positive input speed turns the input angle clockwise on screen, output speeds are signed (same sense as
    /// the input = positive). The geometry is the mechanism's own parameters; everything else is derived [S24].
    /// </summary>
    internal static class MoreKinValues
    {
        private static I18nText T(string en, string de) => new(en, de);

        private const double Deg = Math.PI / 180;

        // ------------------------------------------------------------------ shared variables and parameters

        private static ParamDef Text(string key, I18nText label, string unit) => new(key, label, ParamKind.Text) { Unit = unit, Advanced = true };

        private static readonly ParamDef PInputSpeed = Text("inputSpeed", T("Input speed", "Antriebsdrehzahl"), "min⁻¹");
        private static readonly ParamDef POutputSpeed = Text("outputSpeed", T("Output angular velocity", "Abtriebswinkelgeschwindigkeit"), "rad/s");

        private static ParamDef[] WithShow(params ParamDef[] ps) => new List<ParamDef>(ps) { ValueLabels.ShowValues }.ToArray();

        private static VarDef Length(string key, string param, I18nText name, string symbol, double defMm, VarRelevance rel = VarRelevance.Panel) =>
            new(key, name, T(symbol, symbol), Quantities.Length)
            {
                Unit = "mm", StoredUnit = "mm", Param = param, Relevance = rel, Default = defMm / 1000, Min = 1e-7, Max = 100,
            };

        private static VarDef Phi(double defDeg, I18nText? name = null) =>
            new("phi", name ?? T("Input angle", "Antriebswinkel"), T("φ", "φ"), Quantities.Angle)
            {
                Unit = "°", StoredUnit = "°", Param = "angle", Relevance = VarRelevance.Panel, Default = defDeg * Deg, Min = -2 * Math.PI, Max = 2 * Math.PI,
            };

        private static VarDef InputSpeed() =>
            new("n", T("Input speed", "Antriebsdrehzahl"), T("n", "n"), Quantities.RotationalSpeed)
            {
                Unit = "min⁻¹", Param = "inputSpeed", Relevance = VarRelevance.Panel, Min = -1e5, Max = 1e5,
            };

        private static VarDef Angle(string key, I18nText name, I18nText symbol, VarRelevance rel, double min = 0, double max = Math.PI) =>
            new(key, name, symbol, Quantities.Angle) { Unit = "°", Relevance = rel, Min = min, Max = max };

        private static VarDef Ratio(I18nText name) =>
            new("i", name, T("i", "i"), Quantities.Ratio) { Relevance = VarRelevance.Panel, Min = -1e6, Max = 1e6 };

        private static VarDef OutputSpeed(I18nText name, string symbol) =>
            new("w", name, T(symbol, symbol), Quantities.RotationalSpeed) { Unit = "rad/s", Param = "outputSpeed", Relevance = VarRelevance.Card, Min = -1e5, Max = 1e5 };

        /// <summary>Angle in (−π, π].</summary>
        private static double Wrap(double a)
        {
            a %= 2 * Math.PI;
            if (a > Math.PI) a -= 2 * Math.PI;
            if (a <= -Math.PI) a += 2 * Math.PI;
            return a;
        }

        /// <summary>ω_out = i·n at the current position [S24].</summary>
        private static Relation Speed(string id) =>
            new Relation(id, T("Output angular velocity", "Abtriebswinkelgeschwindigkeit"), "{w} = {i}·{n}", "w", "i", "n") { Source = "S24" }
                .Solve("w", "{i}·{n}", v => v("i") * v("n"))
                .Solve("n", "{w}/{i}", v => Math.Abs(v("i")) < 1e-12 ? double.NaN : v("w") / v("i"));

        // ------------------------------------------------------------------ Geneva drive

        /// <summary>Indexing angle of the wheel per turn of the driver β = 360°/z [S24].</summary>
        private static readonly Relation GenevaIndex =
            new Relation("kin.geneva.index", T("Indexing angle of the wheel", "Schaltwinkel des Malteserkreuzes"), "{beta} = 360°/{z}", "beta", "z") { Source = "S24" }
                .Solve("beta", "360°/{z}", v => 2 * Math.PI / v("z"))
                .Solve("z", "360°/{beta}", v => 2 * Math.PI / v("beta"));

        /// <summary>Driver angle while the wheel moves α_m = 180° − 360°/z (external Geneva drive, radial entry) [S24].</summary>
        private static readonly Relation GenevaMotion =
            new Relation("kin.geneva.motion", T("Driver angle while the wheel moves", "Antriebswinkel während der Schaltung"), "{am} = 180° − 360°/{z}", "am", "z")
            {
                Source = "S24",
                Validity = T("external Geneva drive, the pin enters and leaves the slots radially", "Außenmalteserkreuz, der Bolzen tritt radial in die Schlitze ein und aus"),
            }
                .Solve("am", "180° − 360°/{z}", v => Math.PI - 2 * Math.PI / v("z"))
                .Solve("z", "360°/(180° − {am})", v => 2 * Math.PI / (Math.PI - v("am")));

        /// <summary>Driver angle while the wheel rests (dwell) α_d = 180° + 360°/z [S24].</summary>
        private static readonly Relation GenevaDwell =
            new Relation("kin.geneva.dwell", T("Driver angle while the wheel rests", "Antriebswinkel während der Rast"), "{ad} = 180° + 360°/{z}", "ad", "z") { Source = "S24" }
                .Solve("ad", "180° + 360°/{z}", v => Math.PI + 2 * Math.PI / v("z"))
                .Solve("z", "360°/({ad} − 180°)", v => 2 * Math.PI / (v("ad") - Math.PI));

        /// <summary>Motion/dwell ratio τ = α_m/α_d = (z − 2)/(z + 2) [S24].</summary>
        private static readonly Relation GenevaRatio =
            new Relation("kin.geneva.ratio", T("Motion/dwell ratio", "Schalt-/Rastverhältnis"), "{tau} = (180° − 360°/{z})/(180° + 360°/{z}) = ({z} − 2)/({z} + 2)", "tau", "z") { Source = "S24" }
                .Solve("tau", "({z} − 2)/({z} + 2)", v => (v("z") - 2) / (v("z") + 2))
                .Solve("z", "2·(1 + {tau})/(1 − {tau})", v => 2 * (1 + v("tau")) / (1 - v("tau")));

        /// <summary>Crank radius for radial entry a = C·sin(180°/z) [S24].</summary>
        private static readonly Relation GenevaCrank =
            new Relation("kin.geneva.crank", T("Crank radius of the driver", "Kurbelradius des Antriebs"), "{a} = {C}·sin(180°/{z})", "a", "C", "z") { Source = "S24" }
                .Solve("a", "{C}·sin(180°/{z})", v => v("C") * Math.Sin(Math.PI / v("z")))
                .Solve("C", "{a}/sin(180°/{z})", v => v("a") / Math.Sin(Math.PI / v("z")));

        /// <summary>
        /// Velocity ratio of the Geneva drive at the driver angle φ (φ = 0: pin on the line of centres, deepest in the slot):
        /// ω_w/ω_d = −λ·(cos φ − λ)/(1 − 2λ·cos φ + λ²) with λ = sin(180°/z) while |φ| ≤ 90° − 180°/z, else 0 (locked).
        /// Negative: the wheel turns against the driver.
        /// </summary>
        public static double GenevaVelocityRatio(double z, double phi)
        {
            if (!(z >= 3)) return double.NaN;
            var lambda = Math.Sin(Math.PI / z);
            var p = Wrap(phi);
            if (Math.Abs(p) > Math.PI / 2 - Math.PI / z) return 0;
            var c = Math.Cos(p);
            return -lambda * (c - lambda) / (1 - 2 * lambda * c + lambda * lambda);
        }

        private static readonly Relation GenevaVelocity =
            new Relation("kin.geneva.velocity", T("Velocity ratio at this position", "Übersetzung in dieser Stellung"),
                "{i} = −λ·(cos {phi} − λ)/(1 − 2λ·cos {phi} + λ²), λ = sin(180°/{z})", "i", "z", "phi")
            {
                Source = "S24",
                Validity = T("while the pin is in a slot (|φ| ≤ 90° − 180°/z), else 0 (wheel locked); φ = 0 on the line of centres",
                             "solange der Bolzen im Schlitz ist (|φ| ≤ 90° − 180°/z), sonst 0 (Rad gesperrt); φ = 0 auf der Mittellinie"),
            }
                .Solve("i", "−λ·(cos {phi} − λ)/(1 − 2λ·cos {phi} + λ²)", v => GenevaVelocityRatio(v("z"), v("phi")));

        /// <summary>Geneva drive: slots, centre distance, input angle and speed; indexing, motion and dwell angles, their ratio and the wheel speed.</summary>
        public static readonly KinValues.Spec Geneva = new(
            new[]
            {
                new VarDef("z", T("Number of slots", "Schlitzanzahl"), T("z", "z"), Quantities.Count) { Param = "slots", Relevance = VarRelevance.Panel, Default = 4, Min = 3, Max = 24 },
                Length("C", "centre", T("Centre distance O1-O2", "Achsabstand O1-O2"), "C", 30),
                Phi(-20, T("Driver angle", "Antriebswinkel")),
                InputSpeed(),
                Angle("beta", T("Indexing angle of the wheel", "Schaltwinkel des Malteserkreuzes"), T("β", "β"), VarRelevance.Card),
                Angle("am", T("Driver angle while the wheel moves", "Antriebswinkel während der Schaltung"), T("α_m", "α_s"), VarRelevance.Card),
                Angle("ad", T("Driver angle while the wheel rests", "Antriebswinkel während der Rast"), T("α_d", "α_r"), VarRelevance.Panel, Math.PI, 2 * Math.PI),
                new VarDef("tau", T("Motion/dwell ratio", "Schalt-/Rastverhältnis"), T("τ", "τ"), Quantities.Ratio) { Relevance = VarRelevance.Card, Min = 0, Max = 1 },
                new VarDef("a", T("Crank radius of the driver", "Kurbelradius des Antriebs"), T("a", "a"), Quantities.Length) { Unit = "mm", Relevance = VarRelevance.Panel, Min = 0, Max = 100 },
                Ratio(T("Velocity ratio ω_wheel/ω_driver at this position", "Übersetzung ω_Rad/ω_Antrieb in dieser Stellung")),
                OutputSpeed(T("Angular velocity of the wheel at this position", "Winkelgeschwindigkeit des Rads in dieser Stellung"), "ω₂"),
            },
            new[] { GenevaIndex, GenevaMotion, GenevaDwell, GenevaRatio, GenevaCrank, GenevaVelocity, Speed("kin.geneva.speed") },
            WithShow(PInputSpeed, POutputSpeed),
            Array.Empty<ValueCheck>());

        // ------------------------------------------------------------------ six-bar

        /// <summary>
        /// Transmission angle of the first loop at the current position, at B between coupler A-B and rocker B-O4 (cosine
        /// rule in the triangle A, B, O4 with |A O4|² = a² + d² − 2·a·d·cos φ) [S24].
        /// </summary>
        private static readonly Relation SixBarMu1 =
            new Relation("kin.sixbar.mu1", T("Transmission angle of the first loop at this position", "Übertragungswinkel der ersten Masche in dieser Stellung"),
                "{mu1} = arccos(({b}² + {c}² − {a}² − {d}² + 2·{a}·{d}·cos {phi})/(2·{b}·{c}))", "mu1", "a", "b", "c", "d", "phi")
            {
                Source = "S24",
                Validity = T("angle at B between coupler A-B and rocker B-O4", "Winkel bei B zwischen Koppel A-B und Schwinge B-O4"),
            }
                .Solve("mu1", "arccos(({b}² + {c}² − {a}² − {d}² + 2·{a}·{d}·cos {phi})/(2·{b}·{c}))", v =>
                {
                    double a = v("a"), b = v("b"), c = v("c"), d = v("d");
                    var x2 = a * a + d * d - 2 * a * d * Math.Cos(v("phi"));
                    var cos = (b * b + c * c - x2) / (2 * b * c);
                    return cos < -1 - 1e-12 || cos > 1 + 1e-12 ? double.NaN : Math.Acos(Math.Max(-1, Math.Min(1, cos)));
                });

        private static readonly Relation SixBarSpeed = Speed("kin.sixbar.speed");

        /// <summary>Six-bar: the four-bar lengths, input angle and speed; output angle, both transmission angles, velocity ratio and output speed at this position.</summary>
        public static readonly KinValues.Spec SixBar = new(
            new[]
            {
                Length("a", "crank", T("Crank length", "Kurbellänge"), "a", 12),
                Length("b", "coupler", T("Coupler length", "Koppellänge"), "b", 36),
                Length("c", "rocker", T("Rocker length", "Schwingenlänge"), "c", 30),
                Length("d", "ground", T("Frame length O2-O4", "Gestelllänge O2-O4"), "d", 40),
                Phi(-60),
                InputSpeed(),
                Angle("th6", T("Output angle at this position (rocker O6)", "Abtriebswinkel in dieser Stellung (Schwinge O6)"), T("θ₆", "θ₆"), VarRelevance.Card, -2 * Math.PI, 2 * Math.PI),
                Angle("mu1", T("Transmission angle of the first loop at this position", "Übertragungswinkel der ersten Masche in dieser Stellung"), T("μ₁", "μ₁"), VarRelevance.Card),
                Angle("mu2", T("Transmission angle of the second loop at this position", "Übertragungswinkel der zweiten Masche in dieser Stellung"), T("μ₂", "μ₂"), VarRelevance.Card),
                Ratio(T("Velocity ratio ω₆/ω₂ at this position", "Übersetzung ω₆/ω₂ in dieser Stellung")),
                OutputSpeed(T("Output angular velocity at this position", "Abtriebswinkelgeschwindigkeit in dieser Stellung"), "ω₆"),
            },
            new[] { SixBarMu1, SixBarSpeed },
            WithShow(PInputSpeed, POutputSpeed),
            Array.Empty<ValueCheck>());

        /// <summary>
        /// Output angle θ₆ of the six-bar <paramref name="o"/> (direction O6 → D for Watt, O6 → C for Stephenson, measured like
        /// the input angle) and the transmission angle μ₂ of the second loop (Watt: at D between C-D and D-O6; Stephenson: at
        /// C between P-C and C-O6), for the four-bar lengths in mm and the input angle in degrees; null if it cannot be
        /// assembled.
        /// </summary>
        internal static (double Theta, double Mu2)? SixBarOutput(SketchObject o, double d, double a, double b, double c, double phiDeg)
        {
            var p = SixBarType.SolveWith(o, d, a, b, c, phiDeg);
            var watt = SixBarType.IsWatt(o);
            var (end, prev) = watt ? ("D", "C") : ("C", "P");
            if (!p.Ok || !p.Has(end) || !p.Has(prev)) return null;
            Vec2 o6 = p["O6"], e = p[end], q = p[prev];
            var theta = Math.Atan2(e.Y - o6.Y, e.X - o6.X);
            Vec2 u = q - e, w = o6 - e;
            var cos = u.Dot(w) / (u.Length * w.Length);
            return (theta, Math.Acos(Math.Max(-1, Math.Min(1, cos))));
        }

        /// <summary>
        /// The relations of the six-bar <paramref name="o"/> that need the further links of its chain (Watt: arm, arm angle,
        /// O4-O6, C-D, O6-D; Stephenson: coupler point, P-C, O6-C, pivot O6): output angle, second transmission angle and the
        /// velocity ratio (central difference of the output angle over ±0.01° of input).
        /// </summary>
        internal static IEnumerable<BoundRelation> SixBarLinks(SketchObject o)
        {
            var validity = SixBarType.IsWatt(o)
                ? T("from the loop closure; further links (arm O4-C, arm angle, O4-O6, C-D, O6-D) from the element's parameters",
                    "aus dem Maschenschluss; weitere Glieder (Arm O4-C, Armwinkel, O4-O6, C-D, O6-D) aus den Parametern des Elements")
                : T("from the loop closure; further links (coupler point P, P-C, O6-C, pivot O6) from the element's parameters",
                    "aus dem Maschenschluss; weitere Glieder (Koppelpunkt P, P-C, O6-C, Drehpunkt O6) aus den Parametern des Elements");
            (double Theta, double Mu2)? At(Func<string, double> v, double phiDeg)
                => SixBarOutput(o, v("d") * 1000, v("a") * 1000, v("b") * 1000, v("c") * 1000, phiDeg);

            var output = new Relation("kin.sixbar.output", T("Output angle at this position", "Abtriebswinkel in dieser Stellung"),
                    "{th6} = ∠(O6 → output joint)", "th6", "a", "b", "c", "d", "phi") { Source = "S24", Validity = validity }
                .Solve("th6", "∠(O6 → output joint)", v => At(v, v("phi") / Deg)?.Theta ?? double.NaN);
            var mu2 = new Relation("kin.sixbar.mu2", T("Transmission angle of the second loop at this position", "Übertragungswinkel der zweiten Masche in dieser Stellung"),
                    "{mu2} = ∠(output coupler, output rocker)", "mu2", "a", "b", "c", "d", "phi") { Source = "S24", Validity = validity }
                .Solve("mu2", "∠(output coupler, output rocker)", v => At(v, v("phi") / Deg)?.Mu2 ?? double.NaN);
            var ratio = new Relation("kin.sixbar.ratio", T("Velocity ratio at this position", "Übersetzung in dieser Stellung"),
                    "{i} = dθ₆/d{phi}", "i", "a", "b", "c", "d", "phi") { Source = "S24", Validity = validity }
                .Solve("i", "dθ₆/d{phi}", v =>
                {
                    const double h = 0.01;
                    var phi = v("phi") / Deg;
                    if (At(v, phi + h) is not { } p1 || At(v, phi - h) is not { } p0) return double.NaN;
                    return Wrap(p1.Theta - p0.Theta) / (2 * h * Deg);
                });
            yield return BoundRelation.Local(output, o.Id);
            yield return BoundRelation.Local(mu2, o.Id);
            yield return BoundRelation.Local(ratio, o.Id);
        }
    }
}
