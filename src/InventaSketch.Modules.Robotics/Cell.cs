using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Robotics
{
    /// <summary>
    /// Work cell periphery and safety devices drawn as box symbols in top and side view. Default sizes suit the default
    /// robot boxes (roughly 1:50); every symbol scales freely.
    /// </summary>
    internal static class Cell
    {
        public static IEnumerable<ObjectType> Types()
        {
            yield return Workpiece();
            yield return Conveyor();
            yield return Pallet();
            yield return Pedestal();
            yield return Door();
            yield return LightCurtain();
            yield return Scanner();
            yield return EStop();
            yield return Agv();
        }

        private const string Wood = "#ead9b8";
        private const string Part = "#efe3cf";

        // ------------------------------------------------------------------ workpiece

        private static readonly ParamDef ShapeParam = Pm.Choice("shape", "Shape", "Form", "box", ("box", "Block", "Quader"), ("cylinder", "Cylinder", "Zylinder"))
            .WithOptionIcons(v => v == "cylinder" ? IconIds.ShapeCircle : IconIds.ShapeSquare);

        private static ObjectType Workpiece() => new RoboBoxType("robot.cell.workpiece", new I18nText("Workpiece", "Werkstück"), RoboCategories.Cell,
            new[] { "workpiece", "part", "object", "werkstück", "bauteil", "teil" },
            new SymView(RoboViews.TopView, new Size2(10, 8), (p, o) =>
            {
                var fill = RS.Medium with { Fill = Fill.Solid(Part) };
                if (o.Str("shape", "box") == "cylinder")
                {
                    p.Ellipse(0, 0, 4, 4, fill, RoboTags.Base);
                    p.Line(-4.8, 0, 4.8, 0, RS.Center); p.Line(0, -3.4, 0, 3.4, RS.Center);
                }
                else
                {
                    p.Box(-5, -4, 5, 4, fill, RoboTags.Base);
                    p.Line(-5, -2.6, -3.6, -4, RS.Fine);
                }
            }, (p, o) => new[] { RoboBoxType.Port(p, 0, 0, "grip") }),
            new SymView(RoboViews.SideView, new Size2(10, 5), (p, o) =>
            {
                var fill = RS.Medium with { Fill = Fill.Solid(Part) };
                p.Box(o.Str("shape", "box") == "cylinder" ? -4 : -5, -2.5, o.Str("shape", "box") == "cylinder" ? 4 : 5, 2.5, fill, RoboTags.Base);
                if (o.Str("shape", "box") == "cylinder") p.Line(0, -2.5, 0, 2.5, RS.Center);
            }, (p, o) => new[] { RoboBoxType.Port(p, 0, -2.5, "grip"), RoboBoxType.Port(p, 0, 2.5, "bottom") }))
        {
            Params = new[] { ShapeParam },
            Extra = o => new[] { Rb.Cycle(o, "robot.workpiece.shape", ShapeParam) },
            CompanionIds = new[] { "robot.cell.conveyor", "robot.cell.pallet", "robot.gripper.parallel" },
        };

        // ------------------------------------------------------------------ belt conveyor

        private static ObjectType Conveyor() => new RoboBoxType("robot.cell.conveyor", new I18nText("Belt conveyor", "Gurtförderer (Förderband)"), RoboCategories.Cell,
            new[] { "conveyor", "belt", "conveyor belt", "transport", "förderband", "gurtförderer", "bandförderer", "transportband", "förderer" },
            new SymView(RoboViews.TopView, new Size2(40, 10), (p, o) =>
            {
                p.Box(-20, -4.8, 20, -4, RS.Dark, RoboTags.Base);
                p.Box(-20, 4, 20, 4.8, RS.Dark, RoboTags.Base);
                p.Box(-18, -4, 18, 4, RS.Body, RoboTags.Link);
                foreach (var x in new[] { -19, 19 }) p.Box(x - 1, -4, x + 1, 4, RS.White, RoboTags.Joint);
                for (var x = -15.0; x <= 15.01; x += 3) p.Line(x, -4, x, 4, RS.Fine);
                if (o.Flag("arrow", true)) DirArrow(p, o, 0, 6, 1.8);
            }, (p, o) => new[] { RoboBoxType.Port(p, -18, 0, "in"), RoboBoxType.Port(p, 18, 0, "out"), RoboBoxType.Rail(p, -18, 0, 18, 0, "belt") }),
            new SymView(RoboViews.SideView, new Size2(40, 12), (p, o) =>
            {
                const double y0 = -2.2, r = 2.0;
                if (o.Flag("legs", true))
                {
                    foreach (var x in new[] { -14.0, 14.0 })
                    {
                        p.Box(x - 0.6, y0 + r, x + 0.6, 5.6, RS.Dark, RoboTags.Base);
                        p.Box(x - 1.6, 5.4, x + 1.6, 6, RS.Dark, RoboTags.Base);
                    }
                    p.Line(-13.4, 3.6, 13.4, 3.6, RS.Thin);
                }
                // Belt loop around the end rollers.
                var pts = new List<Vec2> { new(-18, y0 - r) , new(18, y0 - r) };
                pts.AddRange(Geo.ArcPoints(new Vec2(18, y0), r, r, -Math.PI / 2, Math.PI, 12).Skip(1));
                pts.Add(new Vec2(-18, y0 + r));
                pts.AddRange(Geo.ArcPoints(new Vec2(-18, y0), r, r, Math.PI / 2, Math.PI, 12).Skip(1));
                p.PolyD(pts, RS.Medium with { Fill = Fill.Solid(RoboColors.Dark) }, true, RoboTags.Link);
                foreach (var x in new[] { -18.0, 18.0 }) { p.Round(x, y0, 1.5, RS.White, RoboTags.Joint); p.Round(x, y0, 0.3, RS.Black, RoboTags.Joint); }
                for (var x = -12.0; x <= 12.01; x += 6) p.Round(x, y0, 0.9, RS.ThinWhite, RoboTags.Joint);
                if (o.Flag("arrow", true)) DirArrow(p, o, -5.3, 6, 1.4);
            }, (p, o) => new[]
            {
                RoboBoxType.Port(p, -18, -4.2, "in"), RoboBoxType.Port(p, 18, -4.2, "out"), RoboBoxType.Rail(p, -18, -4.2, 18, -4.2, "belt"),
                RoboBoxType.Mount(p, 0, 6, "floor"),
            }))
        {
            Params = new[]
            {
                Pm.Choice("dir", "Direction", "Förderrichtung", "forward", ("forward", "Left to right", "Von links nach rechts"), ("backward", "Right to left", "Von rechts nach links")),
                Pm.Bool("arrow", "Direction arrow", "Richtungspfeil", true),
                Pm.Bool("legs", "Legs (side view)", "Stützen (Seitenansicht)", true),
            },
            Extra = o => new[]
            {
                new ContextAction("robot.conveyor.reverse", new I18nText("Reverse direction", "Richtung umkehren"), IconIds.Swap,
                    x => x.WithParam("dir", x.Str("dir", "forward") == "forward" ? "backward" : "forward")) { Group = "param", ParamKeys = new[] { "dir" } },
                Rb.Toggle(o, "robot.conveyor.arrow", new I18nText("Direction arrow", "Richtungspfeil"), IconIds.ArrowEnd, "arrow", true),
            },
            CompanionIds = new[] { "robot.cell.workpiece", "robot.arm.articulated", "robot.safety.curtain" },
        };

        private static void DirArrow(Pad p, SketchObject o, double y, double half, double head)
        {
            var fwd = o.Str("dir", "forward") != "backward";
            var s = RS.Medium with { Stroke = RoboColors.Blue };
            if (fwd) p.Arrow(-half, y, half, y, head, s, RoboTags.Axis);
            else p.Arrow(half, y, -half, y, head, s, RoboTags.Axis);
        }

        // ------------------------------------------------------------------ pallet

        private static ObjectType Pallet() => new RoboBoxType("robot.cell.pallet", new I18nText("Pallet", "Palette"), RoboCategories.Cell,
            new[] { "pallet", "palletizing", "euro pallet", "palette", "europalette", "palettieren", "ladungsträger" },
            new SymView(RoboViews.TopView, new Size2(24, 16), (p, o) =>
            {
                var wood = RS.Thin with { Fill = Fill.Solid(Wood) };
                foreach (var y in new[] { -6.6, 0.0, 6.6 })
                    foreach (var x in new[] { -10.8, 0.0, 10.8 })
                        p.Box(x - 1.2, y - 1.2, x + 1.2, y + 1.2, RS.Hidden);
                var widths = new[] { 2.9, 2.0, 2.9, 2.0, 2.9 };
                var gap = (16 - widths.Sum()) / 4;
                var y0 = -8.0;
                foreach (var w in widths) { p.Box(-12, y0, 12, y0 + w, wood, RoboTags.Base); y0 += w + gap; }
            }, (p, o) => new[] { RoboBoxType.Port(p, 0, 0, "load") }),
            new SymView(RoboViews.SideView, new Size2(24, 3), (p, o) =>
            {
                var wood = RS.Thin with { Fill = Fill.Solid(Wood) };
                p.Box(-12, -1.5, 12, -1.05, wood, RoboTags.Base);
                foreach (var x in new[] { -10.8, 0.0, 10.8 }) p.Box(x - 1.2, -1.05, x + 1.2, 1.05, wood, RoboTags.Base);
                p.Box(-12, 1.05, 12, 1.5, wood, RoboTags.Base);
            }, (p, o) => new[] { RoboBoxType.Port(p, 0, -1.5, "load"), RoboBoxType.Rail(p, -12, -1.5, 12, -1.5, "deck"), RoboBoxType.Mount(p, 0, 1.5, "floor") }))
        {
            CompanionIds = new[] { "robot.cell.workpiece", "robot.arm.articulated" },
        };

        // ------------------------------------------------------------------ pedestal

        private static ObjectType Pedestal() => new RoboBoxType("robot.cell.pedestal", new I18nText("Robot pedestal", "Robotersockel"), RoboCategories.Cell,
            new[] { "pedestal", "riser", "base", "stand", "sockel", "robotersockel", "unterbau", "podest" },
            new SymView(RoboViews.SideView, new Size2(14, 12), (p, o) =>
            {
                p.Box(-7, 5, 7, 6, RS.Dark, RoboTags.Base);
                p.Box(-4.5, -4.8, 4.5, 5, RS.Body, RoboTags.Base);
                p.Poly(RS.Body, true, RoboTags.Base, -4.5, 5, -4.5, 2.6, -6.6, 5);
                p.Poly(RS.Body, true, RoboTags.Base, 4.5, 5, 4.5, 2.6, 6.6, 5);
                p.Box(-6, -6, 6, -4.8, RS.Dark, RoboTags.Base);
            }, (p, o) => new[] { RoboBoxType.Mount(p, 0, -6, "top"), RoboBoxType.Mount(p, 0, 6, "floor") }),
            new SymView(RoboViews.TopView, new Size2(14, 14), (p, o) =>
            {
                p.Box(-7, -7, 7, 7, RS.Dark, RoboTags.Base);
                p.Box(-6, -6, 6, 6, RS.Body, RoboTags.Base);
                foreach (var x in new[] { -4.8, 4.8 }) foreach (var y in new[] { -4.8, 4.8 }) p.Round(x, y, 0.55, RS.White, RoboTags.Base);
                p.Line(-1.2, 0, 1.2, 0, RS.Center); p.Line(0, -1.2, 0, 1.2, RS.Center);
            }, (p, o) => new[] { RoboBoxType.Mount(p, 0, 0, "top") }))
        {
            CompanionIds = new[] { "robot.arm.articulated", "robot.arm.cobot", "robot.arm.scara" },
        };

        // ------------------------------------------------------------------ fence door

        private static readonly ParamDef DoorKind = Pm.Choice("kind", "Door", "Tür", "hinged", ("hinged", "Hinged door", "Drehtür"), ("sliding", "Sliding door", "Schiebetür"));

        private static ObjectType Door() => new RoboBoxType("robot.safety.door", new I18nText("Safety fence door", "Schutztür (Schutzzaun)"), RoboCategories.Safety,
            new[] { "door", "fence door", "access door", "interlock", "safety door", "schutztür", "zugangstür", "türzuhaltung", "sicherheitsschalter", "zaun" },
            new SymView(RoboViews.TopView, new Size2(20, 20), (p, o) =>
            {
                const double y = -9.4;
                foreach (var x in new[] { -9.4, 9.4 }) p.Box(x - 0.6, y - 0.6, x + 0.6, y + 0.6, RS.Black, RoboTags.Post);
                if (o.Str("kind", "hinged") == "sliding")
                {
                    p.Box(-17.6 + 8.8, y + 1.2, 8.8 - 7.0, y + 1.9, RS.White, RoboTags.Link);
                    p.Line(-8.8, y + 2.6, 8.8, y + 2.6, RS.Hidden);
                    p.Arrow(1.6, y + 4.4, -4.6, y + 4.4, 1.2, RS.Thin);
                }
                else
                {
                    p.Box(-9.1, y, -8.4, y + 17.4, RS.White, RoboTags.Link);
                    p.Arc(-8.8, y, 17.6, 17.6, 0, 90, RS.Fine);
                }
                // Interlock (guard locking) switch at the latch post.
                p.Box(7.0, y + 0.8, 9.6, y + 2.8, RS.White with { Stroke = RoboColors.Red }, RoboTags.Protective);
                p.Round(8.3, y + 1.8, 0.45, RS.Black with { Fill = Fill.Solid(RoboColors.Red), Stroke = RoboColors.Red }, RoboTags.Protective);
            }, (p, o) => new[] { RoboBoxType.Port(p, -9.4, -9.4, "hinge"), RoboBoxType.Port(p, 9.4, -9.4, "latch") }),
            new SymView(RoboViews.SideView, new Size2(20, 28), (p, o) =>
            {
                foreach (var x in new[] { -9.4, 9.4 }) p.Box(x - 0.6, -14, x + 0.6, 14, RS.Dark, RoboTags.Post);
                p.Box(-8.4, -12.6, 8.4, 13, RS.Medium with { Fill = Fill.Solid("#ffffff") }, RoboTags.Link);
                p.Box(-7.6, -11.8, 7.6, 12.2, RS.Fine with { Fill = Fill.Hatched(new HatchPattern(45, 1.2 * p.K, Cross: true, Width: 0.12)) }, RoboTags.Link);
                p.Box(6.0, -0.8, 7.2, 1.6, RS.Dark, RoboTags.Link);
                p.Box(7.4, -4.4, 9.0, -1.2, RS.White with { Stroke = RoboColors.Red }, RoboTags.Protective);
            }, (p, o) => new[] { RoboBoxType.Port(p, -9.4, 14, "hinge"), RoboBoxType.Port(p, 9.4, 14, "latch") }))
        {
            Params = new[] { DoorKind },
            Extra = o => new[] { Rb.Cycle(o, "robot.door.kind", DoorKind) },
            CompanionIds = new[] { "robot.safety.fence", "robot.safety.curtain", "robot.safety.estop" },
        };

        // ------------------------------------------------------------------ light curtain

        private static readonly Style BeamStyle = Style.ThinLine with { Stroke = RoboColors.Red, StrokeWidth = 0.18 };

        private static ObjectType LightCurtain() => new RoboBoxType("robot.safety.curtain", new I18nText("Safety light curtain", "Sicherheits-Lichtvorhang"), RoboCategories.Safety,
            new[] { "light curtain", "light grid", "aopd", "safety light curtain", "lichtvorhang", "lichtgitter", "sicherheitslichtvorhang", "schutzeinrichtung" },
            new SymView(RoboViews.TopView, new Size2(30, 4), (p, o) =>
            {
                if (o.Flag("field", true)) p.Box(-13, -0.8, 13, 0.8, RS.Area(Fill.Solid(RoboColors.ProtectiveTint)), RoboTags.Protective);
                p.Line(-13, 0, 13, 0, BeamStyle with { Dash = DashStyle.Dash }, RoboTags.Beam);
                p.Box(-15, -1.4, -13, 1.4, RS.Dark, RoboTags.Base);
                p.Box(13, -1.4, 15, 1.4, RS.Dark, RoboTags.Base);
                p.Line(-13, -1.0, -13, 1.0, RS.Medium with { Stroke = RoboColors.Red });
                p.Line(13, -1.0, 13, 1.0, RS.Medium with { Stroke = RoboColors.Red });
            }, (p, o) => new[] { RoboBoxType.Port(p, -14, 0, "emitter"), RoboBoxType.Port(p, 14, 0, "receiver") }),
            new SymView(RoboViews.SideView, new Size2(30, 36), (p, o) =>
            {
                var n = Math.Max(2, Math.Min(32, o.Int("beams", 10)));
                if (o.Flag("field", true)) p.Box(-12.6, -16, 12.6, 15, RS.Area(Fill.Solid(RoboColors.ProtectiveTint)), RoboTags.Protective);
                for (var i = 0; i < n; i++)
                {
                    var y = -16 + 31.0 * i / (n - 1);
                    p.Line(-12.6, y, 12.6, y, BeamStyle, RoboTags.Beam);
                }
                foreach (var x in new[] { -13.6, 13.6 })
                {
                    p.Box(x - 1, -17.4, x + 1, 16.4, RS.Dark, RoboTags.Base);
                    p.Box(x - 1.4, 16.4, x + 1.4, 18, RS.Dark, RoboTags.Base);
                }
            }, (p, o) => new[] { RoboBoxType.Port(p, -13.6, 18, "emitter"), RoboBoxType.Port(p, 13.6, 18, "receiver") }))
        {
            Params = new[] { Pm.Int("beams", "Beams (side view)", "Strahlen (Seitenansicht)", 10, 2, 32), Pm.Bool("field", "Tint protective field", "Schutzfeld tönen", true) },
            Extra = o => new[] { Rb.Toggle(o, "robot.curtain.field", new I18nText("Tint protective field", "Schutzfeld tönen"), IconIds.FillTint, "field", true) },
            CompanionIds = new[] { "robot.safety.fence", "robot.cell.conveyor", "robot.safety.zone" },
        };

        // ------------------------------------------------------------------ safety laser scanner

        private static readonly ParamDef FieldShape = Pm.Choice("shape", "Field shape", "Feldform", "sector", ("sector", "Sector (circular)", "Sektor (kreisförmig)"), ("rect", "Rectangular (in front)", "Rechteckig (vorne)"))
            .WithOptionIcons(v => v == "rect" ? IconIds.ShapeSquare : IconIds.ShapeCircle);

        private static readonly Style WarnLine = Style.ThinLine with { Stroke = "#b88600", StrokeWidth = 0.25, Dash = DashStyle.Dash };
        private static readonly Style ProtLine = Style.ThinLine with { Stroke = RoboColors.Red, StrokeWidth = 0.3 };

        private static ObjectType Scanner() => new RoboBoxType("robot.safety.scanner", new I18nText("Safety laser scanner", "Sicherheits-Laserscanner"), RoboCategories.Safety,
            new[] { "laser scanner", "safety scanner", "protective field", "warning field", "laserscanner", "sicherheitslaserscanner", "schutzfeld", "warnfeld", "scanner" },
            new SymView(RoboViews.TopView, new Size2(40, 40), (p, o) =>
            {
                var ratio = Geo.Clamp(o.Num("ratio", 0.55), 0.05, 1);
                var labels = o.Flag("labels", false);
                if (o.Str("shape", "sector") == "rect")
                {
                    p.Box(-20, -20, 20, 17.8, RS.Area(Fill.Solid(RoboColors.WarningTint)), RoboTags.Warning);
                    p.Box(-20, -20, 20, 17.8, WarnLine, RoboTags.Warning);
                    var top = 17.8 - 37.8 * ratio;
                    p.Box(-14, top, 14, 17.8, RS.Area(Fill.Solid(RoboColors.ProtectiveTint)), RoboTags.Protective);
                    p.Box(-14, top, 14, 17.8, ProtLine, RoboTags.Protective);
                    p.RoundBox(-2.2, 15.6, 2.2, 20, 0.6, RS.Dark, RoboTags.Base);
                    p.Round(0, 17.0, 1.1, RS.White, RoboTags.Base);
                    if (labels)
                    {
                        p.Text(0, -17.6, p.De ? "Warnfeld" : "Warning field", 2.2, RoboTags.Label, color: "#8a6500");
                        p.Text(0, top + 2.6, p.De ? "Schutzfeld" : "Protective field", 2.2, RoboTags.Label, color: RoboColors.Red);
                    }
                    return;
                }
                var angle = Geo.Clamp(o.Num("angle", 270), 10, 360);
                Sector(p, 20, angle, RS.Area(Fill.Solid(RoboColors.WarningTint)), WarnLine, RoboTags.Warning);
                Sector(p, 20 * ratio, angle, RS.Area(Fill.Solid(RoboColors.ProtectiveTint)), ProtLine, RoboTags.Protective);
                p.RoundBox(-2.2, -2.2, 2.2, 2.2, 0.6, RS.Dark, RoboTags.Base);
                p.Round(0, 0, 1.2, RS.White, RoboTags.Base);
                if (labels)
                {
                    p.Text(0, -20 + (20 - 20 * ratio) / 2, p.De ? "Warnfeld" : "Warning field", 2.2, RoboTags.Label, color: "#8a6500");
                    p.Text(0, -20 * ratio * 0.55, p.De ? "Schutzfeld" : "Protective field", 2.2, RoboTags.Label, color: RoboColors.Red);
                }
            }, (p, o) => new[] { RoboBoxType.Mount(p, 0, o.Str("shape", "sector") == "rect" ? 17.8 : 0, "body") }),
            new SymView(RoboViews.SideView, new Size2(40, 6), (p, o) =>
            {
                var ratio = Geo.Clamp(o.Num("ratio", 0.55), 0.05, 1);
                var rect = o.Str("shape", "sector") == "rect";
                double x0 = rect ? 0 : -20, cx = rect ? -17.8 : 0;
                // Scan plane: warning field dashed, protective field solid.
                if (rect)
                {
                    p.Line(cx, 0.4, 20, 0.4, WarnLine, RoboTags.Warning);
                    p.Line(cx, 0.4, cx + 37.8 * ratio, 0.4, ProtLine with { StrokeWidth = 0.5 }, RoboTags.Protective);
                }
                else
                {
                    p.Line(x0, 0.4, 20, 0.4, WarnLine, RoboTags.Warning);
                    p.Line(-20 * ratio, 0.4, 20 * ratio, 0.4, ProtLine with { StrokeWidth = 0.5 }, RoboTags.Protective);
                }
                p.RoundBox(cx - 2, -2.6, cx + 2, 3, 0.5, RS.Dark, RoboTags.Base);
                p.Box(cx - 2, -0.3, cx + 2, 1.1, RS.White, RoboTags.Base);
            }, (p, o) => new[] { RoboBoxType.Mount(p, o.Str("shape", "sector") == "rect" ? -17.8 : 0, 3, "body") }))
        {
            Params = new[]
            {
                FieldShape,
                Pm.Num("angle", "Scanning angle", "Scanwinkel", 270, 10, 360, "°", 15),
                Pm.Num("ratio", "Protective / warning field", "Schutzfeld / Warnfeld", 0.55, 0.05, 1, null, 0.05),
                Pm.Bool("labels", "Field labels", "Feldbeschriftung", false),
            },
            Extra = o => new[]
            {
                Rb.Cycle(o, "robot.scanner.shape", FieldShape),
                Rb.Toggle(o, "robot.scanner.labels", new I18nText("Field labels", "Feldbeschriftung"), IconIds.Text, "labels", false, "annotation"),
            },
            CompanionIds = new[] { "robot.mobile.agv", "robot.safety.zone", "robot.safety.fence" },
        };

        /// <summary>Field sector of radius r (design) opening <paramref name="angle"/> towards −y (forward), centred at the origin.</summary>
        private static void Sector(Pad p, double r, double angle, Style fill, Style line, string tag)
        {
            var pts = new List<Vec2>();
            var full = angle >= 359.999;
            if (!full) pts.Add(Vec2.Zero);
            pts.AddRange(Geo.ArcPoints(Vec2.Zero, r, r, Geo.DegToRad(-90 - angle / 2), Geo.DegToRad(full ? 360 : angle), Math.Max(12, (int)(angle / 4))));
            if (full) pts.RemoveAt(pts.Count - 1);
            p.PolyD(pts, fill, true, tag);
            p.PolyD(pts, line, true, tag);
        }

        // ------------------------------------------------------------------ emergency stop

        private static ObjectType EStop() => new RoboBoxType("robot.safety.estop", new I18nText("Emergency stop button", "Not-Halt-Taster"), RoboCategories.Safety,
            new[] { "emergency stop", "e-stop", "estop", "stop button", "not-halt", "nothalt", "not-aus", "notaus", "pilztaster" },
            new SymView(RoboViews.TopView, new Size2(8, 8), (p, o) =>
            {
                p.RoundBox(-4, -4, 4, 4, 0.8, RS.Thin with { Fill = Fill.Solid(RoboColors.Yellow) }, RoboTags.Base);
                p.Round(0, 0, 2.7, RS.Medium with { Fill = Fill.Solid(RoboColors.Red) }, RoboTags.Protective);
                p.Round(0, 0, 1.7, RS.Fine with { Stroke = "#8e1b1b" }, RoboTags.Protective);
            }, (p, o) => new[] { RoboBoxType.Mount(p, 0, 0, "mount") }),
            new SymView(RoboViews.SideView, new Size2(8, 7), (p, o) =>
            {
                p.Box(-3.2, 0.2, 3.2, 3.5, RS.Thin with { Fill = Fill.Solid(RoboColors.Yellow) }, RoboTags.Base);
                p.Box(-1, -0.6, 1, 0.2, RS.Dark, RoboTags.Base);
                var dome = new List<Vec2> { new(-3, -0.6) };
                dome.AddRange(Geo.ArcPoints(new Vec2(0, -1.4), 3, 2.0, Math.PI, Math.PI, 14));
                dome.Add(new Vec2(3, -0.6));
                p.PolyD(dome, RS.Medium with { Fill = Fill.Solid(RoboColors.Red) }, true, RoboTags.Protective);
            }, (p, o) => new[] { RoboBoxType.Mount(p, 0, 3.5, "mount") }))
        {
            Uniform = true,
            CompanionIds = new[] { "robot.safety.door", "robot.safety.fence" },
        };

        // ------------------------------------------------------------------ mobile robot (AGV / AMR)

        private static ObjectType Agv() => new RoboBoxType("robot.mobile.agv", new I18nText("Mobile robot (AGV / AMR)", "Mobiler Roboter (FTF / AMR)"), RoboCategories.Robots,
            new[] { "agv", "amr", "mobile robot", "automated guided vehicle", "autonomous mobile robot", "ftf", "fts", "fahrerloses transportfahrzeug", "mobiler roboter", "roboter" },
            new SymView(RoboViews.TopView, new Size2(30, 20), (p, o) =>
            {
                p.RoundBox(-15, -10, 15, 10, 2, RS.Body, RoboTags.Base);
                foreach (var y in new[] { -1, 1 }) p.Box(-2.6, y * 9.6, 2.6, y * 7.6, RS.Dark, RoboTags.Joint);
                foreach (var x in new[] { -11.5, 11.5 }) foreach (var y in new[] { -7.0, 7.0 }) p.Round(x, y, 1, RS.Hidden, RoboTags.Joint);
                p.RoundBox(-8, -6.4, 8, 6.4, 1, RS.White, RoboTags.Link);
                if (o.Flag("arrow", true)) p.Arrow(-4, 0, 5, 0, 2.0, RS.Medium with { Stroke = RoboColors.Blue }, RoboTags.Axis);
                foreach (var (x, y) in new[] { (13.0, -8.0), (-13.0, 8.0) })
                {
                    p.Round(x, y, 1.4, RS.Dark, RoboTags.Base);
                    p.Round(x, y, 0.7, RS.White, RoboTags.Base);
                }
            }, (p, o) => new[] { RoboBoxType.Mount(p, 0, 0, "deck"), RoboBoxType.Mount(p, 13, -8, "scanner1"), RoboBoxType.Mount(p, -13, 8, "scanner2") }),
            new SymView(RoboViews.SideView, new Size2(30, 10), (p, o) =>
            {
                p.RoundBox(-15, -4, 15, 3, 1.2, RS.Body, RoboTags.Base);
                p.Box(-8, -5, 8, -4, RS.Dark, RoboTags.Link);
                p.Box(13.4, -1.6, 15, 0.6, RS.Dark, RoboTags.Base);
                p.Round(0, 3, 2, RS.Dark, RoboTags.Joint);
                p.Round(0, 3, 0.5, RS.White, RoboTags.Joint);
                foreach (var x in new[] { -11.5, 11.5 }) p.Round(x, 4, 1, RS.White, RoboTags.Joint);
                if (o.Flag("arrow", true)) p.Arrow(-4, -0.6, 5, -0.6, 1.6, RS.Medium with { Stroke = RoboColors.Blue }, RoboTags.Axis);
            }, (p, o) => new[] { RoboBoxType.Mount(p, 0, -5, "deck"), RoboBoxType.Mount(p, 14.2, -0.5, "scanner1") }))
        {
            Params = new[] { Pm.Bool("arrow", "Direction arrow", "Fahrtrichtungspfeil", true) },
            Extra = o => new[] { Rb.Toggle(o, "robot.agv.arrow", new I18nText("Direction arrow", "Fahrtrichtungspfeil"), IconIds.ArrowEnd, "arrow", true) },
            CompanionIds = new[] { "robot.safety.scanner", "robot.arm.cobot", "robot.cell.pallet" },
        };
    }
}
