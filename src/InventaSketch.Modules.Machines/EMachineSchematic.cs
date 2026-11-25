using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Machines
{
    /// <summary>
    /// Electric machine as a gearbox-schematic symbol: rotor on the shaft, stator around it (radial flux, inner or outer
    /// rotor) or beside it (axial flux: single stator and rotor, double rotor of the YASA type, double stator). The machine
    /// kind is marked on the rotor so the variants differ at a glance: magnets (PMSM, red/blue), field winding with slip
    /// rings and brushes (EESM), cage bars with short-circuit rings (ASM), salient teeth (SRM). Stick schematic, simplified
    /// longitudinal section and axial view; the Hub anchor at the centre lies on the shaft axis in every variant.
    /// </summary>
    internal static class EMachineSchematic
    {
        public const string Id = "machines.drivetrain.emachine";

        private static I18nText T(string en, string de) => new(en, de);

        public static readonly string[] Layouts = { "inner", "outer", "axial", "axial-2r", "axial-2s" };
        public static readonly string[] Kinds = { "pmsm", "eesm", "asm", "srm" };

        public static readonly ParamDef Layout = Common.Choice("layout", T("Arrangement", "Bauform"), "inner",
            new ParamOption("inner", T("Radial flux, inner rotor", "Radialfluss, Innenläufer")),
            new ParamOption("outer", T("Radial flux, outer rotor", "Radialfluss, Außenläufer")),
            new ParamOption("axial", T("Axial flux, one stator and one rotor", "Axialfluss, ein Ständer und ein Läufer")),
            new ParamOption("axial-2r", T("Axial flux, double rotor (YASA type)", "Axialfluss, Doppelläufer (YASA-Bauart)")),
            new ParamOption("axial-2s", T("Axial flux, double stator", "Axialfluss, Doppelständer")))
            .WithOptionIcons(v => v switch
            {
                "outer" => IconIds.MachOuter, "axial" => IconIds.MachAxial, "axial-2r" => IconIds.MachAxialDoubleRotor,
                "axial-2s" => IconIds.MachAxialDoubleStator, _ => IconIds.MachInner,
            });

        public static readonly ParamDef Kind = Common.Choice("kind", T("Machine type", "Maschinenart"), "pmsm",
            new ParamOption("pmsm", T("Permanent-magnet synchronous (PMSM)", "Permanentmagneterregte Synchronmaschine (PMSM/PSM)")),
            new ParamOption("eesm", T("Wound-field synchronous (EESM, slip rings)", "Fremderregte Synchronmaschine (FSM/EESM, Schleifringe)")),
            new ParamOption("asm", T("Induction, squirrel cage (ASM)", "Asynchronmaschine, Käfigläufer (ASM)")),
            new ParamOption("srm", T("Switched reluctance (SRM)", "Geschaltete Reluktanzmaschine (SRM)")))
            .WithOptionIcons(v => v switch { "eesm" => IconIds.MachWound, "asm" => IconIds.MachCage, "srm" => IconIds.MachReluctance, _ => IconIds.MachMagnet });

        private static readonly (string Value, I18nText Label)[] ShortLayouts =
        {
            ("inner", T("Radial, inner rotor", "Radial, Innenläufer")), ("outer", T("Radial, outer rotor", "Radial, Außenläufer")),
            ("axial", T("Axial flux", "Axialfluss")), ("axial-2r", T("Axial, double rotor", "Axial, Doppelläufer")),
            ("axial-2s", T("Axial, double stator", "Axial, Doppelständer")),
        };

        private static readonly (string Value, I18nText Label)[] ShortKinds =
        {
            ("pmsm", T("Permanent magnet (PMSM)", "Permanentmagnet (PMSM)")), ("eesm", T("Wound field (EESM)", "Fremderregt (FSM)")),
            ("asm", T("Induction (ASM)", "Asynchron (ASM)")), ("srm", T("Reluctance (SRM)", "Reluktanz (SRM)")),
        };

        private static readonly ParamDef ShaftStubs = Common.Flag("shaft", T("Shaft stubs", "Wellenstümpfe"), true);
        private static readonly ParamDef Ground = Common.Flag("ground", T("Stator fixed to housing", "Ständer am Gehäuse"), true);

        public static string LayoutOf(SketchObject o) => Array.IndexOf(Layouts, o.Str("layout", "inner")) >= 0 ? o.Str("layout", "inner") : "inner";
        public static string KindOf(SketchObject o) => Array.IndexOf(Kinds, o.Str("kind", "pmsm")) >= 0 ? o.Str("kind", "pmsm") : "pmsm";

        public static MachType Type() => new(
            Id, T("E-machine (gearbox schematic)", "E-Maschine (Getriebeschema)"), Drivetrain.Cat,
            new[]
            {
                "e-machine", "electric machine", "electric motor", "motor", "generator", "motor generator", "traction motor", "hybrid",
                "rotor", "stator", "gearbox schematic", "kinematic", "e-axle", "radial flux", "axial flux", "outer rotor", "inner rotor",
                "yasa", "double rotor", "double stator", "pmsm", "psm", "eesm", "induction", "asm", "squirrel cage", "srm", "reluctance", "slip ring",
                "e-maschine", "elektromaschine", "elektromotor", "fahrmotor", "läufer", "ständer", "getriebeschema", "hybridgetriebe", "e-achse",
                "radialfluss", "axialfluss", "außenläufer", "innenläufer", "doppelläufer", "doppelständer", "synchronmaschine", "fremderregt",
                "asynchronmaschine", "käfigläufer", "reluktanz", "schleifring",
                Drivetrain.Kw1, Drivetrain.Kw2, Drivetrain.Kw3,
            },
            new ViewSpec("stick", T("Schematic", "Schema"), IconIds.Schematic, 30, 40, DrawStick)
            {
                Hub = true,
                Anchors = (p, o) => ShaftPorts(p, 15, 20),
            },
            new ViewSpec("side", T("Side view (sectioned)", "Seitenansicht (geschnitten)"), IconIds.Side, 40, 40, DrawSide)
            {
                Hub = true,
                Anchors = (p, o) => ShaftPorts(p, 20, 20),
            },
            new ViewSpec("axial", T("Axial view", "Stirnansicht"), IconIds.Axial, 40, 40, DrawAxial)
            {
                Hub = true,
                Anchors = (p, o) => new[] { Anchor.Point(AnchorRole.Mount, p.V(0, 20), "mount") },
            })
        {
            Scale = 0.9,
            Params = new[] { Layout, Kind, ShaftStubs, Ground, Common.Hatch },
            CompanionTypes = new[] { "machines.drive.inverter", "machines.drive.battery", "mech.shaft", "mech.gear.spur", "mech.bearing.ball", "machines.drive.dclink" },
            Extra = Actions,
        };

        private static IEnumerable<ContextAction> Actions(SketchObject o)
        {
            // Arrangement and machine type are choices with the machine pictograms (short option names for bar and pie).
            foreach (var a in ActionChoices.FromValues("param.layout", T("Arrangement", "Bauform"), "layout", LayoutOf(o), ShortLayouts.Select(x => (x.Value, x.Label, Layout.Options!.First(p => p.Value == x.Value).Icon)), "param"))
                yield return a;
            foreach (var a in ActionChoices.FromValues("param.kind", T("Machine type", "Maschinenart"), "kind", KindOf(o), ShortKinds.Select(x => (x.Value, x.Label, Kind.Options!.First(p => p.Value == x.Value).Icon)), "param"))
                yield return a;
            yield return Common.Toggle(o, "shaft", true, T("Shaft stubs on/off", "Wellenstümpfe an/aus")) with { Icon = IconIds.ShaftEnds };
            yield return Common.Toggle(o, "ground", true, T("Housing connection on/off", "Gehäuseanbindung an/aus")) with { Icon = IconIds.Ground };
        }

        /// <summary>
        /// Ports at both shaft ends (left/right), the rotor axis between them (gears and couplings sit on it, the next shaft
        /// section docks at its ends) and the housing mount below the machine.
        /// </summary>
        private static IEnumerable<Anchor> ShaftPorts(Pen p, double hw, double hh) => new[]
        {
            Anchor.Point(AnchorRole.Port, p.V(-hw, 0), "w"), Anchor.Point(AnchorRole.Port, p.V(hw, 0), "e"),
            Anchor.Segment(AnchorRole.Axis, p.V(-hw, 0), p.V(hw, 0)),
            Anchor.Point(AnchorRole.Mount, p.V(0, hh), "mount"),
        };

        // ------------------------------------------------------------------ styles and elements

        private const string CageColor = "#A7B0BA";
        private static readonly Style Cage = S.Thin with { Fill = Fill.Solid(CageColor) };
        private static readonly Style White = Drivetrain.Stick with { StrokeWidth = 0.5, Fill = Fill.Solid("#ffffff") };

        private static bool Hatched(SketchObject o) => o.Flag("hatch", true);

        private static Style StatorStyle(Pen p, SketchObject o, bool section)
            => section ? (Hatched(o) ? p.Hatch(45) : S.Vis) : (Hatched(o) ? p.Hatch(45, 0.7) with { StrokeWidth = 0.5 } : S.Vis with { StrokeWidth = 0.5 });

        private static Style RotorBase(Pen p, SketchObject o, bool section)
            => section ? (Hatched(o) ? p.Hatch(135) : S.White) : White;

        /// <summary>Part of the rectangle adjacent to its gap side ('T', 'B', 'L', 'R') with relative depth f.</summary>
        private static (double X0, double Y0, double X1, double Y1) Strip(double x0, double y0, double x1, double y1, char gap, double f)
        {
            double w = x1 - x0, h = y1 - y0;
            return gap switch
            {
                'T' => (x0, y0, x1, y0 + h * f),
                'B' => (x0, y1 - h * f, x1, y1),
                'L' => (x0, y0, x0 + w * f, y1),
                _ => (x1 - w * f, y0, x1, y1),
            };
        }

        /// <summary>
        /// Rotor element (bar in the schematic, stack or disc in the section) with the mark of the machine kind on the side
        /// facing the air gap: <paramref name="gaps"/> holds one or two of 'T', 'B', 'L', 'R'.
        /// </summary>
        private static void RotorElement(Pen p, SketchObject o, double x0, double y0, double x1, double y1, string gaps, bool north, bool section)
        {
            var kind = KindOf(o);
            if (kind == "srm")
            {
                // Salient teeth: the body is set back from the gap, three teeth reach across.
                double bx0 = x0, by0 = y0, bx1 = x1, by1 = y1;
                foreach (var g in gaps)
                {
                    var (sx0, sy0, sx1, sy1) = Strip(x0, y0, x1, y1, g, 0.3);
                    if (g == 'T') by0 = sy1; else if (g == 'B') by1 = sy0; else if (g == 'L') bx0 = sx1; else bx1 = sx0;
                }
                p.Box(bx0, by0, bx1, by1, RotorBase(p, o, section), "rotor");
                foreach (var g in gaps)
                {
                    var (sx0, sy0, sx1, sy1) = Strip(x0, y0, x1, y1, g, 0.3);
                    var along = g is 'T' or 'B';
                    for (var i = 0; i < 3; i++)
                    {
                        if (along)
                        {
                            var d = (sx1 - sx0) / 5;
                            p.Box(sx0 + 2 * i * d, sy0, sx0 + (2 * i + 1) * d, sy1, RotorBase(p, o, section), "teeth");
                        }
                        else
                        {
                            var d = (sy1 - sy0) / 5;
                            p.Box(sx0, sy0 + 2 * i * d, sx1, sy0 + (2 * i + 1) * d, RotorBase(p, o, section), "teeth");
                        }
                    }
                }
                return;
            }
            p.Box(x0, y0, x1, y1, RotorBase(p, o, section), "rotor");
            foreach (var g in gaps)
            {
                var (sx0, sy0, sx1, sy1) = Strip(x0, y0, x1, y1, g, gaps.Length > 1 ? 0.3 : section ? 0.3 : 0.45);
                switch (kind)
                {
                    case "eesm":
                        p.Box(sx0, sy0, sx1, sy1, p.Copper(), "field");
                        break;
                    case "asm":
                        p.Box(sx0, sy0, sx1, sy1, Cage, "cage");
                        // bar ends: short-circuit rings at both ends of the strip
                        if (g is 'T' or 'B')
                        {
                            var e = Math.Min(1.2, (sx1 - sx0) * 0.12);
                            p.Box(sx0, sy0, sx0 + e, sy1, S.Thin with { Fill = Fill.Solid("#6E7783") }, "cage-ring");
                            p.Box(sx1 - e, sy0, sx1, sy1, S.Thin with { Fill = Fill.Solid("#6E7783") }, "cage-ring");
                        }
                        else
                        {
                            var e = Math.Min(1.2, (sy1 - sy0) * 0.12);
                            p.Box(sx0, sy0, sx1, sy0 + e, S.Thin with { Fill = Fill.Solid("#6E7783") }, "cage-ring");
                            p.Box(sx0, sy1 - e, sx1, sy1, S.Thin with { Fill = Fill.Solid("#6E7783") }, "cage-ring");
                        }
                        break;
                    default:
                        p.Box(sx0, sy0, sx1, sy1, S.Magnet(north), "magnet");
                        break;
                }
            }
        }

        /// <summary>Fixed hub mark (cross) on the shaft axis at the schematic detail scale (shrinks, never grows).</summary>
        private static void HubCross(Pen p, double x, Style st)
        {
            var c = p.V(x, 0);
            var a = 1.5 * Math.Min(1, p.K);
            p.LineP(c + new Vec2(-a, -a), c + new Vec2(a, a), st, "hub");
            p.LineP(c + new Vec2(-a, a), c + new Vec2(a, -a), st, "hub");
        }

        /// <summary>Horizontal housing line at y with connectors at the given x positions and strokes away from the machine.</summary>
        private static void GroundH(Pen p, double x0, double x1, double yFrom, double y, double[] connectors)
        {
            var s = Math.Sign(y);
            foreach (var cx in connectors) p.Line(cx, yFrom, cx, y, S.Vis, "ground");
            p.Line(x0, y, x1, y, S.Vis, "ground");
            var n = Math.Max(2, (int)Math.Round((x1 - x0) / 2.6));
            var step = (x1 - x0) / n;
            for (var i = 0; i < n; i++)
            {
                var x = x0 + step * (i + 0.6);
                p.Line(x, y, x - Math.Min(1.4, step * 0.6), y + s * 1.5, S.Thin, "ground");
            }
        }

        /// <summary>Two slip rings with brushes on the shaft (field supply of a wound rotor), centred at x.</summary>
        private static void SlipRings(Pen p, double x, double r, double brush)
        {
            foreach (var dx in new[] { -1.0, 1.0 })
            {
                var cx = x + dx;
                p.Box(cx - 0.6, -r, cx + 0.6, r, S.Thin with { Fill = Fill.Solid(S.CopperColor) }, "slipring");
                p.Box(cx - 0.45, -r - brush, cx + 0.45, -r - 0.2, S.Black, "brush");
            }
        }

        // ------------------------------------------------------------------ stick schematic (30 x 40)

        private static void DrawStick(Pen p, SketchObject o)
        {
            var stick = Drivetrain.Stick;
            var ground = o.Flag("ground", true);
            if (o.Flag("shaft", true)) p.Line(-15, 0, 15, 0, Drivetrain.Heavy, "shaft");
            var stator = StatorStyle(p, o, false);
            switch (LayoutOf(o))
            {
                case "outer":
                    // rotor bell from the shaft around the stator; stator on a fixed sleeve tied to the housing
                    p.Line(-9, -14, -9, 14, stick, "carrier");
                    HubCross(p, -9, stick);
                    foreach (var s in new[] { -1.0, 1.0 })
                    {
                        RotorElement(p, o, -9, s < 0 ? -17 : 14, 8, s < 0 ? -14 : 17, s < 0 ? "B" : "T", s < 0, false);
                        p.Box(-6, s < 0 ? -13 : 9, 7, s < 0 ? -9 : 13, stator, "stator");
                        if (!ground) continue;
                        p.Poly(S.Vis, false, 7, s * 11, 10.5, s * 11, 10.5, s * 3, 13, s * 3);
                        p.Line(13, s * 1.8, 13, s * 5, S.Vis, "ground");
                        foreach (var y in new[] { 2.4, 3.6, 4.8 }) p.Line(13, s * y, 14.5, s * (y - 1.0), S.Thin, "ground");
                    }
                    break;
                case "axial":
                    p.Line(-3.5, -3, -3.5, 3, stick, "carrier");
                    HubCross(p, -3.5, stick);
                    foreach (var s in new[] { -1.0, 1.0 })
                    {
                        RotorElement(p, o, -5, s < 0 ? -16 : 3, -2, s < 0 ? -3 : 16, "R", s < 0, false);
                        p.Box(0, s < 0 ? -16 : 3.6, 4, s < 0 ? -3.6 : 16, stator, "stator");
                        if (ground) GroundH(p, -1, 5, s * 16, s * 18.5, new[] { 2.0 });
                    }
                    break;
                case "axial-2r":
                    foreach (var x in new[] { -7.5, 7.5 }) { p.Line(x, -3, x, 3, stick, "carrier"); HubCross(p, x, stick); }
                    foreach (var s in new[] { -1.0, 1.0 })
                    {
                        RotorElement(p, o, -9, s < 0 ? -16 : 3, -6, s < 0 ? -3 : 16, "R", s < 0, false);
                        RotorElement(p, o, 6, s < 0 ? -16 : 3, 9, s < 0 ? -3 : 16, "L", s > 0, false);
                        p.Box(-3.5, s < 0 ? -16 : 3.6, 3.5, s < 0 ? -3.6 : 16, stator, "stator");
                        if (ground) GroundH(p, -4.5, 4.5, s * 16, s * 18.5, new[] { 0.0 });
                    }
                    break;
                case "axial-2s":
                    p.Line(0, -3, 0, 3, stick, "carrier");
                    HubCross(p, 0, stick);
                    foreach (var s in new[] { -1.0, 1.0 })
                    {
                        RotorElement(p, o, -2, s < 0 ? -16 : 3, 2, s < 0 ? -3 : 16, "LR", s < 0, false);
                        p.Box(-9, s < 0 ? -16 : 3.6, -5, s < 0 ? -3.6 : 16, stator, "stator");
                        p.Box(5, s < 0 ? -16 : 3.6, 9, s < 0 ? -3.6 : 16, stator, "stator");
                        if (ground) GroundH(p, -10, 10, s * 16, s * 18.5, new[] { -7.0, 7.0 });
                    }
                    break;
                default:
                    p.Line(0, -9, 0, 9, stick, "carrier");
                    HubCross(p, 0, stick);
                    foreach (var s in new[] { -1.0, 1.0 })
                    {
                        RotorElement(p, o, -6, s < 0 ? -12 : 9, 6, s < 0 ? -9 : 12, s < 0 ? "T" : "B", s < 0, false);
                        p.Box(-8, s < 0 ? -17 : 13, 8, s < 0 ? -13 : 17, stator, "stator");
                        if (ground) GroundH(p, -7, 7, s * 17, s * 18.5, new[] { -4.0, 4.0 });
                    }
                    break;
            }
            if (KindOf(o) == "eesm") SlipRings(p, -12.5, 2.4, 2.0);
        }

        // ------------------------------------------------------------------ simplified longitudinal section (40 x 40)

        private static void DrawSide(Pen p, SketchObject o)
        {
            var layout = LayoutOf(o);
            var ground = o.Flag("ground", true);
            var stator = StatorStyle(p, o, true);
            var copper = p.Copper(3);
            var rShaft = layout == "inner" ? 4.0 : 3.0;
            var stubs = o.Flag("shaft", true);
            switch (layout)
            {
                case "outer":
                    foreach (var s in new[] { -1.0, 1.0 })
                    {
                        RotorElement(p, o, -11, s < 0 ? -15 : 11.2, 11, s < 0 ? -11.2 : 15, s < 0 ? "B" : "T", s < 0, true);
                        p.Box(-13, s < 0 ? -15 : 3, -11, s < 0 ? -3 : 15, RotorBase(p, o, true), "rotor");
                        p.Box(-9, s < 0 ? -10.6 : 4.6, 9, s < 0 ? -4.6 : 10.6, stator, "stator");
                        p.Box(-10.8, s < 0 ? -9.6 : 5.6, -9, s < 0 ? -5.6 : 9.6, copper, "winding");
                        p.Box(9, s < 0 ? -9.6 : 5.6, 10.8, s < 0 ? -5.6 : 9.6, copper, "winding");
                        // fixed sleeve carrying the stator, tied to the housing on the right
                        p.Box(-9, s < 0 ? -4.6 : 3.2, 18.5, s < 0 ? -3.2 : 4.6, stator, "sleeve");
                        if (!ground) continue;
                        p.Line(18.5, s * 3.2, 18.5, s * 7, S.Vis, "ground");
                        foreach (var y in new[] { 4.0, 5.5, 7.0 }) p.Line(18.5, s * y, 20, s * (y - 1.2), S.Thin, "ground");
                    }
                    break;
                case "axial":
                    if (ground) p.RoundBox(-12, -19.5, 12, 19.5, 1.5, S.Thin, "housing");
                    foreach (var s in new[] { -1.0, 1.0 })
                    {
                        RotorElement(p, o, -7, s < 0 ? -16 : 3, -2.5, s < 0 ? -3 : 16, "R", s < 0, true);
                        p.Box(-1.5, s < 0 ? -16 : 4, 4.5, s < 0 ? -4 : 16, stator, "stator");
                        p.Box(-1.5, s < 0 ? -15 : 5, -0.5, s < 0 ? -5 : 15, copper, "winding");
                    }
                    break;
                case "axial-2r":
                    if (ground) p.RoundBox(-14, -19.5, 14, 19.5, 1.5, S.Thin, "housing");
                    foreach (var s in new[] { -1.0, 1.0 })
                    {
                        RotorElement(p, o, -11, s < 0 ? -16 : 3, -6.5, s < 0 ? -3 : 16, "R", s < 0, true);
                        RotorElement(p, o, 6.5, s < 0 ? -16 : 3, 11, s < 0 ? -3 : 16, "L", s > 0, true);
                        p.Box(-5, s < 0 ? -16 : 4, 5, s < 0 ? -4 : 16, stator, "stator");
                        p.Box(-5, s < 0 ? -15 : 5, -4, s < 0 ? -5 : 15, copper, "winding");
                        p.Box(4, s < 0 ? -15 : 5, 5, s < 0 ? -5 : 15, copper, "winding");
                    }
                    break;
                case "axial-2s":
                    if (ground) p.RoundBox(-14, -19.5, 14, 19.5, 1.5, S.Thin, "housing");
                    foreach (var s in new[] { -1.0, 1.0 })
                    {
                        RotorElement(p, o, -3, s < 0 ? -16 : 3, 3, s < 0 ? -3 : 16, "LR", s < 0, true);
                        p.Box(-11, s < 0 ? -16 : 4, -5, s < 0 ? -4 : 16, stator, "stator");
                        p.Box(5, s < 0 ? -16 : 4, 11, s < 0 ? -4 : 16, stator, "stator");
                        p.Box(-6, s < 0 ? -15 : 5, -5, s < 0 ? -5 : 15, copper, "winding");
                        p.Box(5, s < 0 ? -15 : 5, 6, s < 0 ? -5 : 15, copper, "winding");
                    }
                    break;
                default:
                    if (ground) p.RoundBox(-15, -19.5, 15, 19.5, 1.5, S.Thin, "housing");
                    foreach (var s in new[] { -1.0, 1.0 })
                    {
                        p.Box(-10, s < 0 ? -18 : 12, 10, s < 0 ? -12 : 18, stator, "stator");
                        p.Box(-13, s < 0 ? -16 : 12.2, -10, s < 0 ? -12.2 : 16, copper, "winding");
                        p.Box(10, s < 0 ? -16 : 12.2, 13, s < 0 ? -12.2 : 16, copper, "winding");
                        RotorElement(p, o, -10, s < 0 ? -11.4 : 4, 10, s < 0 ? -4 : 11.4, s < 0 ? "T" : "B", s < 0, true);
                    }
                    break;
            }
            // white fill: the shaft passes in front of housing walls and sleeves (not sectioned); off when the machine sits on a drawn shaft
            if (stubs) p.Box(-20, -rShaft, 20, rShaft, S.White, "shaft");
            if (KindOf(o) == "eesm") SlipRings(p, -17, rShaft + 1.2, 2.0);
            p.Axis(-20, 20);
        }

        // ------------------------------------------------------------------ axial view (40 x 40)

        private static void Ring(Pen p, double r0, double r1, Style st, string tag)
        {
            var segs = new List<PathSeg>(G.CircleSegs(r1));
            if (r0 > 0) segs.AddRange(G.CircleSegs(r0, reverse: true));
            p.PathD(segs, st, tag);
        }

        /// <summary>Kind marks in the band r0..r1 of a rotor seen axially.</summary>
        private static void AxialMarks(Pen p, SketchObject o, double r0, double r1)
        {
            switch (KindOf(o))
            {
                case "eesm":
                    for (var k = 0; k < 4; k++) p.PolyD(G.Sector(r0, r1, k * 90 - 25, k * 90 + 25), p.Copper(), true, "field");
                    break;
                case "asm":
                    for (var k = 0; k < 16; k++)
                    {
                        var c = G.P((r0 + r1) / 2, k * 22.5);
                        p.Round(c, Math.Min(0.9, (r1 - r0) * 0.35), Cage, "cage");
                    }
                    break;
                case "srm":
                    // slots between six salient rotor teeth
                    for (var k = 0; k < 6; k++) p.PolyD(G.Sector(r0, r1, k * 60 + 12, k * 60 + 48), S.White with { StrokeWidth = 0.25 }, true, "teeth");
                    break;
                default:
                    for (var k = 0; k < 8; k++) p.PolyD(G.Sector(r0, r1, k * 45 + 3, k * 45 + 42), S.Magnet(k % 2 == 0), true, "magnet");
                    break;
            }
        }

        private static void DrawAxial(Pen p, SketchObject o)
        {
            var hatch = Hatched(o);
            var iron = hatch ? p.Hatch(45) : S.Vis;
            var rotor = hatch ? p.Hatch(135) : S.White;
            switch (LayoutOf(o))
            {
                case "inner":
                    if (o.Flag("ground", true)) p.Circle(0, 0, 19.5, S.Thin, "housing");
                    Ring(p, 13, 18.5, iron, "stator");
                    Ring(p, 4, 12.2, rotor, "rotor");
                    AxialMarks(p, o, 10.2, 12.2);
                    break;
                case "outer":
                    Ring(p, 14, 18.5, rotor, "rotor");
                    AxialMarks(p, o, 14, 16);
                    Ring(p, 5, 13.2, iron, "stator");
                    if (o.Flag("ground", true)) p.Circle(0, 0, 5, S.Thin, "housing");
                    break;
                default:
                    // axial flux: face of the rotor disc in front of the stator
                    if (o.Flag("ground", true)) p.Circle(0, 0, 19.5, S.Thin, "housing");
                    p.Circle(0, 0, 18.5, S.Vis, "stator");
                    Ring(p, 4, 17, S.White, "rotor");
                    AxialMarks(p, o, 8.5, 16);
                    break;
            }
            p.Round(new Vec2(0, 0), 4, S.Vis, "shaft");
            p.CenterCross(0, 0, 20, 20);
        }
    }
}
