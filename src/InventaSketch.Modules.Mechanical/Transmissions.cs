using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Mechanical
{
    /// <summary>
    /// Transmission elements beyond plain gears and clutches: freewheel, hydrodynamic torque converter, park lock, CVT
    /// variator, strain wave gear and cycloidal drive. Schematic views follow the style of the gear-train schematics of
    /// this module (shaft horizontal, hub on the box centre so the element sits on a shaft axis); all drawings are
    /// illustrative and drawn independently, not taken from a standard.
    /// </summary>
    internal static class Transmissions
    {
        private static I18nText T(string en, string de) => new(en, de);

        private static readonly I18nText VStick = T("Schematic", "Schema");
        private static readonly I18nText VSection = T("Section", "Schnitt");
        private static readonly I18nText VAxial = T("Axial view", "Stirnansicht");

        public static IEnumerable<MechType> Types()
        {
            yield return Freewheel();
            yield return TorqueConverter();
            yield return ParkLock();
            yield return Cvt();
            yield return StrainWave();
            yield return Cycloidal();
        }

        private static Anchor Mount(Pen p, double x, double y, string id) => Anchor.Point(AnchorRole.Mount, p.V(x, y), id);

        /// <summary>Ground symbol: line from a to b (design) with hatch strokes on the side of the normal n.</summary>
        private static void GroundAt(Pen p, double x1, double y1, double x2, double y2, Vec2 n, double depth, double spacing = 1.6)
        {
            var a = p.V(x1, y1);
            var b = p.V(x2, y2);
            p.LineP(a, b, S.Vis, "ground");
            var horizontal = Math.Abs(y2 - y1) < Math.Abs(x2 - x1);
            Frag.GroundStrokes(p, a, b, n, depth * (horizontal ? p.Sy : p.Sx), spacing * (horizontal ? p.Sx : p.Sy));
        }

        // ------------------------------------------------------------------ freewheel

        private static MechType Freewheel() => new(
            "mech.clutch.freewheel", T("Freewheel (one-way clutch)", "Freilauf (Freilaufkupplung)"), Couplings.Cat,
            new[]
            {
                "freewheel", "one-way clutch", "overrunning clutch", "roller clutch", "sprag clutch", "backstop",
                "freilauf", "freilaufkupplung", "überholkupplung", "klemmrollenfreilauf", "klemmkörperfreilauf", "rücklaufsperre",
                Drivetrain.Kw1, Drivetrain.Kw2, Drivetrain.Kw3,
            },
            // Bore: inside the inner ring (section 3, the schematic the same design value so the fit carries over a view
            // switch, axial 3.2 round as drawn).
            new ViewSpec("stick", VStick, 12, 20, FreewheelStick) { Bore = (p, o) => 3 * p.Sy },
            new ViewSpec("section", VSection, 14, 24, FreewheelSection) { Bore = (p, o) => 3 * p.Sy },
            new ViewSpec("axial", VAxial, 30, 30, FreewheelAxial) { Bore = (p, o) => 3.2 * p.K })
        {
            Params = new[] { Common.Flag("reverse", T("Reversed locking direction", "Sperrrichtung umgekehrt"), false) },
            Extra = o => new[] { Common.Toggle(o, "reverse", false, T("Reverse locking direction", "Sperrrichtung umkehren"), IconIds.Reverse) },
            CompanionTypes = new[] { "mech.shaft", "mech.gearset.planetary", "mech.converter.torque", "mech.clutch.multidisc" },
        };

        private static double Dir(SketchObject o) => o.Flag("reverse", false) ? -1 : 1;

        /// <summary>Outer member as a bracket, inner member with clamping ramps, a roller in each wedge gap.</summary>
        private static void FreewheelStick(Pen p, SketchObject o)
        {
            var d = Dir(o);
            p.Line(d * -6, 0, d * -4, 0, S.Heavy, "shaft");
            p.Poly(S.Stick, false, d * 3, -8, d * -4, -8, d * -4, 8, d * 3, 8);
            p.Line(d * 4, 0, d * 6, 0, S.Heavy, "shaft");
            p.Line(d * 4, -3.2, d * 4, 3.2, S.Stick, "inner");
            foreach (var s in new[] { -1.0, 1.0 })
            {
                p.Line(d * 4, s * 3.2, d * -2.6, s * 4.8, S.Stick, "ramp");
                p.Round(p.V(d * -0.6, s * 6.2), 1.5, S.Vis, "roller");
            }
        }

        private static void FreewheelSection(Pen p, SketchObject o)
        {
            var hOut = p.Hatch(45);
            var hIn = p.Hatch(135);
            foreach (var s in new[] { -1.0, 1.0 })
            {
                p.Box(-5, s * 12, 5, s * 8.6, hOut, "outer-ring");
                p.Box(-7, s * 3, 7, s * 5.4, hIn, "inner-ring");
                p.Box(-3.4, s * 5.4, 3.4, s * 8.6, S.Vis, "roller");
                p.Line(-4.2, s * 5.4, -4.2, s * 8.6, S.Thin, "cage");
                p.Line(4.2, s * 5.4, 4.2, s * 8.6, S.Thin, "cage");
            }
            p.Axis(7);
        }

        /// <summary>Axial view: outer race, inner star with ramps, clamping rollers in the wedge gaps.</summary>
        private static void FreewheelAxial(Pen p, SketchObject o)
        {
            var d = Dir(o);
            const int n = 6;
            const double rIn = 7, rOut = 9.6, race = 11.2, ramp = 0.82, at = 0.45;
            var pitch = 2 * Math.PI / n;
            var star = new List<Vec2>();
            for (var i = 0; i < n; i++)
            {
                var a0 = -Math.PI / 2 + i * pitch;
                for (var k = 0; k <= 8; k++)
                {
                    var f = k / 8.0;
                    star.Add(Frag.Polar(rIn + (rOut - rIn) * f, a0 + d * f * ramp * pitch));
                }
                star.Add(Frag.Polar(rIn, a0 + d * (ramp + 0.04) * pitch));
            }
            p.Circle(0, 0, 14, S.Vis, "outer-ring");
            p.Circle(0, 0, race, S.Vis, "race");
            p.PolyD(star, S.Vis, true, "inner");
            for (var i = 0; i < n; i++)
            {
                var a = -Math.PI / 2 + i * pitch + d * at * pitch;
                var rRamp = rIn + (rOut - rIn) * at / ramp;
                var c = Frag.Polar((rRamp + race) / 2, a);
                p.Hole(c.X, c.Y, (race - rRamp) / 2 * 0.95, S.Vis, "roller");
            }
            p.Hole(0, 0, 3.2, S.Vis, "bore");
            p.CenterCross(0, 0, 15, 15);
        }

        // ------------------------------------------------------------------ torque converter

        private static MechType TorqueConverter() => new(
            "mech.converter.torque", T("Torque converter (hydrodynamic)", "Drehmomentwandler (hydrodynamisch)"), Couplings.Cat,
            new[]
            {
                "torque converter", "hydrodynamic", "fluid coupling", "impeller", "pump", "turbine", "stator", "reactor", "lock-up clutch",
                "drehmomentwandler", "wandler", "föttinger", "pumpenrad", "turbinenrad", "leitrad", "überbrückungskupplung", "wandlerüberbrückung",
                Drivetrain.Kw1, Drivetrain.Kw2, Drivetrain.Kw3,
            },
            new ViewSpec("stick", VStick, 30, 40, ConverterStick) { Anchors = (p, o) => new[] { Mount(p, 11, -6, "ground") } },
            new ViewSpec("section", VSection, 30, 40, ConverterSection) { Anchors = (p, o) => new[] { Mount(p, 15, -3, "ground") } })
        {
            Params = new[] { Common.Flag("lockup", T("Lock-up clutch", "Überbrückungskupplung"), false) },
            Extra = o => new[] { Common.Toggle(o, "lockup", false, T("Lock-up clutch on/off", "Überbrückungskupplung an/aus"), IconIds.Toggle) },
            CompanionTypes = new[] { "mech.shaft", Drivetrain.Engine, "mech.gearset.planetary", "mech.clutch.freewheel", "mech.clutch.multidisc" },
        };

        /// <summary>
        /// Schematic: input cover around the turbine carrying the pump (right shell), turbine (left shell) fixed on the output
        /// shaft, stator blade between them on a freewheel (circle) and a grounded hollow stator shaft; optional lock-up clutch
        /// between cover and turbine.
        /// </summary>
        private static void ConverterStick(Pen p, SketchObject o)
        {
            p.Line(-15, 0, -12, 0, S.Heavy, "shaft");
            p.Line(-12, -18, -12, 18, S.Stick, "cover");
            foreach (var s in new[] { -1.0, 1.0 })
            {
                p.Line(-12, s * 18, 3, s * 18, S.Stick, "cover");
                var pump = new List<Vec2>();
                var turbine = new List<Vec2>();
                for (var k = 0; k <= 16; k++)
                {
                    var u = Math.PI * k / 16;
                    pump.Add(new Vec2(3 + 4 * Math.Sin(u), s * (12.5 + 5.5 * Math.Cos(u))));
                    turbine.Add(new Vec2(-3 - 4 * Math.Sin(u), s * (12.5 + 4.5 * Math.Cos(u))));
                }
                p.PolyD(pump, S.Stick, false, "pump");
                p.PolyD(turbine, S.Stick, false, "turbine");
                p.Poly(S.Stick, true, -1.2, s * 7.2, 0.4, s * 7.2, 1.2, s * 9.8, -0.4, s * 9.8);
                p.Line(0, s * 7.2, 0, s * 2.6, S.Stick, "stator");
                p.Line(0, s * 2.6, 11, s * 2.6, S.Stick, "stator");
                p.Round(p.V(5.5, s * 2.6), 0.8, S.Vis, "freewheel");
                GroundAt(p, 11, s * 1.3, 11, s * 6, new Vec2(1, 0), 1.6, 1.0);
                if (o.Flag("lockup", false))
                {
                    p.Line(-10.4, s * 10.5, -10.4, s * 16, S.Stick, "lockup");
                    p.Line(-9.2, s * 10.5, -9.2, s * 16, S.Stick, "lockup");
                    p.Line(-12, s * 13.2, -10.4, s * 13.2, S.Stick, "lockup");
                    p.Line(-9.2, s * 12.5, -7, s * 12.5, S.Stick, "lockup");
                }
            }
            Frag.Hub(p, p.V(-3, -8), p.V(-3, 0), p.V(-3, 8), "fixed", S.Stick);
            p.Line(-3, 0, 15, 0, S.Heavy, "shaft");
        }

        /// <summary>Half-shell outline of a torus quarter: outer and inner curve around (cx, 12.5), side sx (+1 right, -1 left).</summary>
        private static List<Vec2> Shell(double cx, double sx, double s)
        {
            var pts = new List<Vec2>();
            for (var k = 0; k <= 16; k++)
            {
                var u = Math.PI * k / 16;
                pts.Add(new Vec2(cx + sx * 6.5 * Math.Sin(u), s * (12.5 + 6 * Math.Cos(u))));
            }
            for (var k = 16; k >= 0; k--)
            {
                var u = Math.PI * k / 16;
                pts.Add(new Vec2(cx + sx * 5.5 * Math.Sin(u), s * (12.5 + 5 * Math.Cos(u))));
            }
            return pts;
        }

        /// <summary>Section: hatched pump and turbine shells, unhatched blades and core rings, stator on its hub, cover, shafts in view.</summary>
        private static void ConverterSection(Pen p, SketchObject o)
        {
            var h45 = p.Hatch(45);
            var h135 = p.Hatch(135);
            // input boss and output shaft (shafts in view, not sectioned)
            p.Box(-15, -2.5, -13, 2.5, h45, "cover");
            p.Box(-4.5, -1.5, 15, 1.5, S.Vis, "shaft");
            foreach (var s in new[] { -1.0, 1.0 })
            {
                // cover from the input boss around the turbine to the pump
                p.Poly(h45, true, -14, s * 2.5, -14, s * 19.5, 0.4, s * 19.5, 0.4, s * 18.5, -13, s * 18.5, -13, s * 2.5);
                p.PolyD(Shell(0.4, 1, s), h45, true, "pump");
                p.PolyD(Shell(-0.4, -1, s), h135, true, "turbine");
                p.Ellipse(3, s * 12.5, 1.4, 2, S.Vis, "core");
                p.Ellipse(-3, s * 12.5, 1.4, 2, S.Vis, "core");
                // stator blade on its hub, hub on the grounded stator shaft
                p.Box(-1.6, s * 6.4, 1.6, s * 9, S.Vis, "stator");
                p.Box(-2, s * 3, 2, s * 6.4, h45, "stator-hub");
                p.Box(-2, s * 1.5, 15, s * 3, h135, "stator-shaft");
                // pump hub
                p.Poly(h45, true, 3, s * 3, 15, s * 3, 15, s * 4.4, 4.2, s * 4.4, 4.2, s * 8.3, 3, s * 8.3);
                // turbine hub
                p.Box(-4.5, s * 1.5, -2.5, s * 7, h135, "turbine-hub");
                if (o.Flag("lockup", false))
                {
                    p.Poly(h135, true, -12, s * 17, -11, s * 17, -11, s * 6.6, -4.5, s * 6.6, -4.5, s * 5.6, -12, s * 5.6);
                    p.Box(-13, s * 13, -12, s * 17, S.Dotted, "lining");
                }
            }
            p.Axis(15);
        }

        // ------------------------------------------------------------------ park lock

        private static MechType ParkLock() => new(
            "mech.parklock", T("Park lock (parking pawl)", "Parksperre (Sperrklinke)"), Couplings.Cat,
            new[]
            {
                "park lock", "parking lock", "parking pawl", "pawl", "park gear", "ratchet",
                "parksperre", "parksperrenrad", "sperrklinke", "sperrrad", "klinke", "parkstellung",
                Drivetrain.Kw1, Drivetrain.Kw2, Drivetrain.Kw3,
            },
            new ViewSpec("stick", VStick, 16, 30, ParkStick) { Anchors = (p, o) => new[] { Mount(p, -7, -13.2, "pivot") } },
            new ViewSpec("axial", VAxial, 40, 40, ParkAxial) { Anchors = (p, o) => new[] { Mount(p, -17.5, -14, "pivot") }, Bore = (p, o) => 3 * p.K })
        {
            Params = new[]
            {
                Common.Count("teeth", T("Teeth of the park gear", "Zähne des Parksperrenrads"), 12, 6, 30),
                Common.Flag("engaged", T("Engaged (P)", "Eingelegt (P)"), true),
            },
            Extra = o => Common.Stepper(o, "teeth", T("Teeth", "Zähne"), 12, 1, 6, 30)
                .Append(Common.Toggle(o, "engaged", true, T("Engaged / released", "Eingelegt / gelöst"), IconIds.Toggle)),
            CompanionTypes = new[] { "mech.shaft", "mech.gear.spur", "mech.housing.ground" },
        };

        private static bool Engaged(SketchObject o) => o.Flag("engaged", true);

        /// <summary>Schematic: park gear on the shaft (fixed hub), pawl pivoted on the housing, its tooth in the gear when engaged.</summary>
        private static void ParkStick(Pen p, SketchObject o)
        {
            p.Line(-3, -11, 3, -11, S.Stick, "tip");
            p.Line(-3, 11, 3, 11, S.Stick, "tip");
            Frag.Hub(p, p.V(0, -11), p.V(0, 0), p.V(0, 11), "fixed", S.Stick);
            var lift = Engaged(o) ? 0.0 : 1.3;
            p.Line(-5.5, -13.2, 6, -13.2 - lift, S.Stick, "pawl");
            var ty = -13.2 - lift * 5.5 / 11.5;
            p.Line(1.8, ty - lift * 1.8 / 11.5, 1.8, Engaged(o) ? -10 : ty + 1.0, S.Stick, "pawl");
            p.Round(p.V(-5.5, -13.2), 0.9, S.Vis, "pivot");
            p.Line(-5.5, -13.2, -7, -13.2, S.Stick, "pivot");
            GroundAt(p, -7, -14.8, -7, -11.6, new Vec2(-1, 0), 1);
        }

        /// <summary>Axial view: park gear with a tooth gap at the top, pawl on its pivot, actuator cone pressing it in.</summary>
        private static void ParkAxial(Pen p, SketchObject o)
        {
            var z = GearMath.Teeth(o, 12);
            const double tip = 12, root = 9.3;
            p.PolyD(GearMath.Outline(z, root, tip, (tip + root) / 2, -Math.PI / 2 + Math.PI / z), S.Vis, true, "teeth");
            p.Hole(0, 0, 3, S.Vis, "bore");
            p.CenterCross(0, 0, 13.5, 13.5);
            var pivot = new Vec2(-14, -14);
            var turn = Engaged(o) ? 0 : Geo.DegToRad(-10);
            Vec2 R(double x, double y)
            {
                var d = new Vec2(x, y) - pivot;
                return pivot + new Vec2(d.X * Math.Cos(turn) - d.Y * Math.Sin(turn), d.X * Math.Sin(turn) + d.Y * Math.Cos(turn));
            }
            var pawl = new[] { R(-15.5, -16), R(6, -16), R(6, -12.6), R(1.6, -12.6), R(0.9, -9.9), R(-0.9, -9.9), R(-1.6, -12.6), R(-15.5, -12.6) };
            p.PolyD(pawl, S.Vis, true, "pawl");
            p.Hole(pivot.X, pivot.Y, 1.1, S.Vis, "pivot");
            p.Line(-17.5, -14, pivot.X - 1.1, -14, S.Vis, "pivot");
            GroundAt(p, -17.5, -17, -17.5, -11, new Vec2(-1, 0), 2.3);
            p.Hole(4.5, -17.7, 1.6, Engaged(o) ? S.Vis : S.Hid, "actuator");
        }

        // ------------------------------------------------------------------ CVT variator

        private static MechType Cvt() => new(
            "mech.cvt", T("CVT variator (cone pulleys)", "CVT-Variator (Kegelscheiben)"), Drives.Cat,
            new[]
            {
                "cvt", "variator", "continuously variable", "cone pulley", "push belt", "chain variator", "belt drive",
                "stufenlos", "stufenlosgetriebe", "kegelscheibe", "kegelscheibenumschlingungsgetriebe", "umschlingungsgetriebe", "schubgliederband",
                Drivetrain.Kw1, Drivetrain.Kw2, Drivetrain.Kw3,
            },
            new ViewSpec("stick", VStick, 24, 60, CvtStick)
            {
                Anchors = (p, o) => new[] { Anchor.Point(AnchorRole.Hub, p.V(0, -15), "hub"), Anchor.Point(AnchorRole.Hub, p.V(0, 15), "hub2") },
            },
            new ViewSpec("axial", VAxial, 60, 30, CvtAxial)
            {
                Anchors = (p, o) => new[] { Anchor.Point(AnchorRole.Hub, p.V(-15, 0), "hub"), Anchor.Point(AnchorRole.Hub, p.V(15, 0), "hub2") },
            })
        {
            Params = new[]
            {
                new ParamDef("ratio", T("Ratio (input / output speed)", "Übersetzung (Antriebs- / Abtriebsdrehzahl)"), ParamKind.Number) { Min = 0.4, Max = 2.5, Step = 0.1 },
                Common.Flag("chain", T("Chain instead of push belt", "Kette statt Schubgliederband"), false),
            },
            // Two pulleys on two shafts: no hub at the box centre, one on each shaft.
            Rotating = false,
            Extra = CvtActions,
            CompanionTypes = new[] { "mech.shaft", "mech.bearing.ball", "mech.clutch.multidisc", "mech.gearset.planetary" },
        };

        private const double CvtMinRatio = 0.4, CvtMaxRatio = 2.5, CvtMeanRadius = 8;

        private static double Ratio(SketchObject o)
        {
            var i = o.Num("ratio", 1);
            return i <= 0 ? 1 : Geo.Clamp(i, CvtMinRatio, CvtMaxRatio);
        }

        /// <summary>Running radii of the belt on the primary and secondary pulley (design mm); their sum stays constant.</summary>
        internal static (double Primary, double Secondary) CvtRadii(SketchObject o)
        {
            var i = Ratio(o);
            return (2 * CvtMeanRadius / (1 + i), 2 * CvtMeanRadius * i / (1 + i));
        }

        private static IEnumerable<ContextAction> CvtActions(SketchObject o)
        {
            var i = Ratio(o);
            if (i + 0.2 <= CvtMaxRatio + 1e-9)
                yield return new ContextAction("param.ratio.inc", T("Ratio +0.2 (slower output)", "Übersetzung +0,2 (langsamer)"), IconIds.Increase,
                    x => x.WithParam("ratio", Math.Round(Math.Min(CvtMaxRatio, Ratio(x) + 0.2), 6))) { Group = "param", ParamKeys = new[] { "ratio" } };
            if (i - 0.2 >= CvtMinRatio - 1e-9)
                yield return new ContextAction("param.ratio.dec", T("Ratio −0.2 (faster output)", "Übersetzung −0,2 (schneller)"), IconIds.Decrease,
                    x => x.WithParam("ratio", Math.Round(Math.Max(CvtMinRatio, Ratio(x) - 0.2), 6))) { Group = "param", ParamKeys = new[] { "ratio" } };
            yield return Common.Toggle(o, "chain", false, T("Chain / push belt", "Kette / Schubgliederband"), IconIds.Toggle);
        }

        /// <summary>
        /// Schematic: primary pulley on the upper shaft, secondary on the lower one, each a pair of cone sheaves (V) on both
        /// sides of its shaft; the belt sits in the V at the running radius given by the ratio.
        /// </summary>
        private static void CvtStick(Pen p, SketchObject o)
        {
            var (r1, r2) = CvtRadii(o);
            var chain = o.Flag("chain", false);
            const double sheave = 13, g0 = 1.2, g1 = 5;
            double Gap(double r) => g0 + (g1 - g0) * r / sheave;
            foreach (var (yc, r) in new[] { (-15.0, r1), (15.0, r2) })
            {
                p.Line(-12, yc, 12, yc, S.Heavy, "shaft");
                foreach (var s in new[] { -1.0, 1.0 })
                {
                    p.Line(-g0, yc, -g1, yc + s * sheave, S.Stick, "sheave");
                    p.Line(g0, yc, g1, yc + s * sheave, S.Stick, "sheave");
                    double ro = r + 0.8, ri = r - 0.8;
                    var belt = new[] { new Vec2(-Gap(ro), yc + s * ro), new Vec2(Gap(ro), yc + s * ro), new Vec2(Gap(ri), yc + s * ri), new Vec2(-Gap(ri), yc + s * ri) };
                    p.PolyD(belt, chain ? S.Vis : S.Black, true, "belt");
                    if (chain) p.Round(p.V(0, yc + s * r), 0.4, S.Vis, "pin");
                }
            }
            p.Line(0, -15 + r1 + 0.8, 0, 15 - r2 - 0.8, S.Vis, "belt");
        }

        /// <summary>Axial view: both pulleys seen along their shafts with the belt (or chain) loop at the running radii.</summary>
        private static void CvtAxial(Pen p, SketchObject o)
        {
            var (r1, r2) = CvtRadii(o);
            var chain = o.Flag("chain", false);
            foreach (var cx in new[] { -15.0, 15.0 })
            {
                p.Circle(cx, 0, 13, S.Vis, "sheave");
                p.Hole(cx, 0, 3, S.Vis, "hub");
                p.Line(cx - 14, 0, cx + 14, 0, S.Ctr, "center");
                p.Line(cx, -14, cx, 14, S.Ctr, "center");
            }
            foreach (var dr in new[] { 0.6, -0.6 })
                p.PolyD(Loop(-15, r1 + dr, 15, r2 + dr), S.Vis, true, "belt");
            if (chain)
            {
                // Link pins along the straight strands.
                var (t1, t2, b1, b2) = Tangents(-15, r1, 15, r2);
                foreach (var (a, b) in new[] { (t1, t2), (b1, b2) })
                {
                    var n = Math.Max(2, (int)(a.DistanceTo(b) / 2.4));
                    for (var k = 1; k < n; k++)
                    {
                        var q = Vec2.Lerp(a, b, k / (double)n);
                        p.Hole(q.X, q.Y, 0.35, S.Vis, "pin");
                    }
                }
            }
        }

        /// <summary>Upper and lower tangent points of the open belt between circles (c1, r1) and (c2, r2) on the x axis.</summary>
        private static (Vec2 T1, Vec2 T2, Vec2 B1, Vec2 B2) Tangents(double c1, double r1, double c2, double r2)
        {
            var phi = Math.Asin(Geo.Clamp((r1 - r2) / (c2 - c1), -0.99, 0.99));
            var nu = new Vec2(Math.Sin(phi), -Math.Cos(phi));
            var nl = new Vec2(Math.Sin(phi), Math.Cos(phi));
            return (new Vec2(c1, 0) + nu * r1, new Vec2(c2, 0) + nu * r2, new Vec2(c1, 0) + nl * r1, new Vec2(c2, 0) + nl * r2);
        }

        /// <summary>Closed open-belt loop (design points): arcs on both circles joined by the two outer tangents.</summary>
        private static List<Vec2> Loop(double c1, double r1, double c2, double r2)
        {
            var phi = Math.Asin(Geo.Clamp((r1 - r2) / (c2 - c1), -0.99, 0.99));
            var pts = new List<Vec2>();
            pts.AddRange(Geo.ArcPoints(new Vec2(c2, 0), r2, r2, phi - Math.PI / 2, Math.PI - 2 * phi, 32));
            pts.AddRange(Geo.ArcPoints(new Vec2(c1, 0), r1, r1, Math.PI / 2 - phi, Math.PI + 2 * phi, 32));
            return pts;
        }

        // ------------------------------------------------------------------ strain wave gear

        private static MechType StrainWave() => new(
            "mech.gearset.harmonic", T("Strain wave gear", "Wellgetriebe (Spannungswellengetriebe)"), Gears.Cat,
            new[]
            {
                "strain wave gear", "harmonic drive", "harmonic gear", "wave generator", "flexspline", "circular spline", "robot joint",
                "wellgetriebe", "spannungswellengetriebe", "wellgenerator", "flexrad", "hohlrad", "robotergelenk", "getriebe",
            },
            // Member ports at the ends of the input (wave generator) and output (flexspline) shafts.
            new ViewSpec("stick", VStick, 24, 40, StrainWaveStick) { Anchors = (p, o) => new[] { Mount(p, -5, -17, "ground"), Gears.MemberPort(p, -12, "in"), Gears.MemberPort(p, 12, "out") } },
            new ViewSpec("axial", VAxial, 40, 40, StrainWaveAxial) { Anchors = (p, o) => new[] { Mount(p, 0, -19, "ground") } })
        {
            CompanionTypes = new[] { "mech.shaft", Drivetrain.EMachine, "mech.bearing.angular", "mech.housing.ground" },
        };

        /// <summary>
        /// Schematic: wave generator on the input shaft, thin-walled flexspline cup with its teeth around the generator and
        /// its bottom on the output shaft, rigid circular spline (internal teeth) fixed to the housing.
        /// </summary>
        private static void StrainWaveStick(Pen p, SketchObject o)
        {
            p.Line(-12, 0, -5, 0, S.Heavy, "shaft");
            Frag.Hub(p, p.V(-5, -9.5), p.V(-5, 0), p.V(-5, 9.5), "fixed", S.Stick);
            foreach (var s in new[] { -1.0, 1.0 })
            {
                p.Round(p.V(-5, s * 10.3), 0.7, S.Vis, "generator");
                p.Line(-7, s * 11.2, 7, s * 11.2, S.Stick, "flexspline");
                p.Box(-7, s * 11.2, -3, s * 12.4, S.Stick, "teeth");
                p.Poly(S.Stick, false, -8, s * 17, -8, s * 13.2, -2, s * 13.2, -2, s * 17);
                GroundAt(p, -9.5, s * 17, -0.5, s * 17, new Vec2(0, s), 2.6);
            }
            p.Line(7, -11.2, 7, 11.2, S.Stick, "flexspline");
            p.Line(7, 0, 12, 0, S.Heavy, "shaft");
            p.Axis(12);
        }

        /// <summary>Axial view: circular spline (rim, internal teeth), elliptical flexspline meshing at the major axis, wave generator with balls.</summary>
        private static void StrainWaveAxial(Pen p, SketchObject o)
        {
            p.Circle(0, 0, 19, S.Vis, "rim");
            p.Circle(0, 0, 16.4, S.Thin, "root");
            p.Circle(0, 0, 15.6, S.Vis, "circular-spline");
            p.Circle(0, 0, 16, S.Ctr, "pitch");
            p.Ellipse(0, 0, 16.3, 13.6, S.Vis, "flexspline");
            p.Ellipse(0, 0, 15.9, 13.2, S.Ctr, "pitch");
            p.Ellipse(0, 0, 14.6, 11.9, S.Vis, "flexspline");
            p.Ellipse(0, 0, 12.8, 10.1, S.Vis, "generator");
            for (var k = 0; k < 20; k++)
            {
                var a = 2 * Math.PI * k / 20;
                p.Hole(13.7 * Math.Cos(a), 11.0 * Math.Sin(a), 0.6, S.Vis, "ball");
            }
            p.Hole(0, 0, 4, S.Vis, "hub");
            p.Hole(0, 0, 2.2, S.Vis, "bore");
            p.CenterCross(0, 0, 20, 20);
        }

        // ------------------------------------------------------------------ cycloidal drive

        private static MechType Cycloidal() => new(
            "mech.gearset.cycloidal", T("Cycloidal drive", "Zykloidgetriebe"), Gears.Cat,
            new[]
            {
                "cycloidal drive", "cycloid", "cyclo", "cycloidal disc", "eccentric", "pin wheel", "pin ring", "robot joint",
                "zykloidgetriebe", "zykloidscheibe", "exzenter", "bolzenring", "triebstockgetriebe", "robotergelenk", "getriebe",
            },
            // Member ports at the ends of the input (eccentric) and output shafts.
            new ViewSpec("stick", VStick, 24, 40, CycloidalStick) { Anchors = (p, o) => new[] { Mount(p, -2, -18.5, "ground"), Gears.MemberPort(p, -12, "in"), Gears.MemberPort(p, 12, "out") } },
            new ViewSpec("axial", VAxial, 40, 40, CycloidalAxial) { Anchors = (p, o) => new[] { Mount(p, 0, -19, "ground") } })
        {
            Params = new[] { Common.Count("lobes", T("Lobes of the cycloid disc", "Höcker der Zykloidscheibe"), 11, 5, 29) },
            Extra = o => Common.Stepper(o, "lobes", T("Lobes", "Höcker"), 11, 1, 5, 29),
            CompanionTypes = new[] { "mech.shaft", Drivetrain.EMachine, "mech.bearing.cylroller", "mech.housing.ground" },
        };

        private static int Lobes(SketchObject o) => Math.Max(5, Math.Min(29, o.Int("lobes", 11)));

        /// <summary>
        /// Schematic: input shaft with eccentric, cycloid disc running loose on it (offset from the axis), grounded pin ring,
        /// output pins through the disc to the output shaft.
        /// </summary>
        private static void CycloidalStick(Pen p, SketchObject o)
        {
            const double e = 2.2;
            p.Line(-12, 0, -6, 0, S.Heavy, "shaft");
            p.Line(-6, 0, -6, -e, S.Stick, "eccentric");
            p.Line(-6, -e, -2, -e, S.Stick, "eccentric");
            p.Line(-4, -e - 12, 0, -e - 12, S.Stick, "disc");
            p.Line(-4, -e + 12, 0, -e + 12, S.Stick, "disc");
            Frag.Hub(p, p.V(-2, -e - 12), p.V(-2, -e), p.V(-2, -e + 12), "loose", S.Stick);
            foreach (var s in new[] { -1.0, 1.0 })
            {
                p.Poly(S.Stick, false, -5, s * 18.5, -5, s * 15.2, 1, s * 15.2, 1, s * 18.5);
                GroundAt(p, -6.5, s * 18.5, 2.5, s * 18.5, new Vec2(0, s), 1.3);
                p.Line(-1, s * 6, 6, s * 6, S.Stick, "output-pin");
            }
            p.Line(6, -6, 6, 6, S.Stick, "output");
            p.Line(6, 0, 12, 0, S.Heavy, "shaft");
            p.Axis(12);
        }

        /// <summary>
        /// Axial view: pin ring (lobes + 1 pins) in the housing, cycloid disc offset by the eccentricity (epitrochoid curve
        /// offset by the pin radius), holes for the output pins and the eccentric bearing.
        /// </summary>
        private static void CycloidalAxial(Pen p, SketchObject o)
        {
            var lobes = Lobes(o);
            var n = lobes + 1;
            const double R = 15.5;
            var rr = Geo.Clamp(R * Math.Sin(Math.PI / n) * 0.5, 0.5, 1.6);
            var e = Math.Min(0.9 * R / n, 0.6 * rr);
            p.Circle(0, 0, 19, S.Vis, "housing");
            for (var k = 0; k < n; k++)
            {
                var c = Frag.Polar(R, 2 * Math.PI * k / n);
                p.Hole(c.X, c.Y, rr, S.Vis, "pin");
            }
            var disc = new List<Vec2>();
            var samples = Math.Min(720, n * 24);
            for (var k = 0; k < samples; k++)
            {
                var t = 2 * Math.PI * k / samples;
                var psi = Math.Atan2(Math.Sin((1 - n) * t), R / (e * n) - Math.Cos((1 - n) * t));
                disc.Add(new Vec2(
                    e + R * Math.Cos(t) - rr * Math.Cos(t + psi) - e * Math.Cos(n * t),
                    -R * Math.Sin(t) + rr * Math.Sin(t + psi) + e * Math.Sin(n * t)));
            }
            p.PolyD(disc, S.Vis, true, "disc");
            const double ro = 7.5, rp = 1.6;
            for (var j = 0; j < 6; j++)
            {
                var a = -Math.PI / 2 + j * Math.PI / 3;
                var q = Frag.Polar(ro, a);
                p.Hole(e + q.X, q.Y, rp + e, S.Vis, "hole");
                p.Hole(q.X, q.Y, rp, S.Vis, "output-pin");
            }
            p.Hole(e, 0, 4, S.Vis, "eccentric");
            p.Hole(0, 0, 1.8, S.Vis, "bore");
            p.CenterCross(0, 0, 20, 20);
        }
    }
}
