using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Robotics
{
    /// <summary>
    /// Further end effectors (welding gun, adhesive dispenser), the parts between flange and tool (tool changer,
    /// force-torque sensor) and the vision sensor. Same conventions as the grippers: side view with the flange on top,
    /// axial view along the tool axis; flange plates span design x −5 … 5, so a part docked with "+" is scaled to the face
    /// it docks on and a tool on a tool changer has the same size as on the bare flange.
    /// </summary>
    internal static partial class Tools
    {
        private static readonly string[] PassCompanions = { "robot.gripper.parallel", "robot.gripper.vacuum", "robot.sensor.ft", "robot.tool.changer", "robot.arm.articulated" };

        /// <summary>
        /// Anchors of a part between flange and tool (side view): the mount "flange" on top facing the robot, the tool-side
        /// mount "tool" at the bottom facing the tool; both with the flange plate's face radius.
        /// </summary>
        private static IEnumerable<Anchor> PassAnchors(Pad p, double top, double bottom)
        {
            yield return Anchor.Directed(AnchorRole.Mount, p.V(0, top), new Vec2(0, -1), "flange", p.V(5, top).DistanceTo(p.V(0, top)));
            yield return Anchor.Directed(AnchorRole.Mount, p.V(0, bottom), new Vec2(0, 1), RoboValues.ToolMount, p.V(5, bottom).DistanceTo(p.V(0, bottom)));
        }

        private static IEnumerable<Anchor> PassAxialAnchors(Pad p)
        {
            yield return RoboBoxType.Mount(p, 0, 0, "flange");
            yield return RoboBoxType.Mount(p, 0, 0, RoboValues.ToolMount);
        }

        // ------------------------------------------------------------------ tool changer

        private static ObjectType Changer() => new RoboBoxType("robot.tool.changer", new I18nText("Tool changer", "Werkzeugwechsler"), RoboCategories.Tools,
            new[] { "tool changer", "automatic tool changer", "quick changer", "atc", "werkzeugwechsler", "werkzeugwechselsystem", "schnellwechsler", "wechselsystem", "wechselflansch" },
            new SymView(RoboViews.SideView, new Size2(20, 10), (p, o) =>
            {
                p.Box(-5, -5, 5, -4, RS.White, RoboTags.Flange);
                p.RoundBox(-7, -4, 7, -0.3, 0.6, RS.Body, RoboTags.Base);
                p.Line(-5.5, -2.2, 5.5, -2.2, RS.Fine);
                p.RoundBox(-7, 0.3, 7, 4, 0.6, RS.Dark, RoboTags.Link);
                // Locking balls in the coupling plane.
                p.Round(-3.5, 0, 0.5, RS.White, RoboTags.Joint);
                p.Round(3.5, 0, 0.5, RS.White, RoboTags.Joint);
                p.Box(-5, 4, 5, 5, RS.White, RoboTags.Flange);
                if (o.Flag("utility", true))
                {
                    // Media module (air, signals), split in the coupling plane like the changer.
                    p.Box(7, -3.4, 9.6, -0.3, RS.Dark, RoboTags.Base);
                    p.Box(7, 0.3, 9.6, 3.4, RS.Body, RoboTags.Link);
                    p.Line(7.6, -1.8, 9, -1.8, RS.Fine);
                    p.Line(7.6, 1.8, 9, 1.8, RS.Fine);
                }
            }, (p, o) => PassAnchors(p, -5, 5)),
            new SymView(RoboViews.AxialView, new Size2(20, 20), (p, o) =>
            {
                p.Ellipse(0, 0, 7, 7, RS.Body, RoboTags.Base);
                p.Ellipse(0, 0, 5, 5, RS.Hidden, RoboTags.Flange);
                foreach (var a in new[] { 45.0, 165, 285 })
                {
                    var r = Geo.DegToRad(a);
                    p.Round(3.5 * Math.Cos(r), 3.5 * Math.Sin(r), 0.6, RS.White, RoboTags.Joint);
                }
                if (o.Flag("utility", true)) p.Box(7, -2.6, 9.6, 2.6, RS.Dark, RoboTags.Base);
                p.Line(-1, 0, 1, 0, RS.Center); p.Line(0, -1, 0, 1, RS.Center);
            }, (p, o) => PassAxialAnchors(p)))
        {
            Params = new[] { Pm.Bool("utility", "Media module", "Medienmodul", true) },
            CompanionIds = PassCompanions,
            HasMass = true,
            ToolSide = true,
            Extra = o => new[] { Rb.Toggle(o, "robot.changer.utility", new I18nText("Media module", "Medienmodul"), IconIds.Toggle, "utility", true) },
        };

        // ------------------------------------------------------------------ force-torque sensor

        private static ObjectType FtSensor() => new RoboBoxType("robot.sensor.ft", new I18nText("Force-torque sensor", "Kraft-Momenten-Sensor"), RoboCategories.Tools,
            new[] { "force torque sensor", "force-torque sensor", "f/t sensor", "ft sensor", "force sensor", "kraft-momenten-sensor", "kraftmomentensensor", "kraftsensor", "momentensensor", "kms" },
            new SymView(RoboViews.SideView, new Size2(20, 8), (p, o) =>
            {
                p.Box(-5, -4, 5, -3, RS.White, RoboTags.Flange);
                p.RoundBox(-7, -3, 7, 3, 1, RS.Body, RoboTags.Base);
                // Measuring body between the two plates.
                p.Line(-7, -1.4, 7, -1.4, RS.Fine);
                p.Line(-7, 1.4, 7, 1.4, RS.Fine);
                p.Box(7, -1, 8.4, 1, RS.Dark, RoboTags.Base);
                p.PolyD(new[] { new Vec2(8.4, 0), new Vec2(9.2, 0.8), new Vec2(9.7, 2.8) }, RS.Medium, false, RoboTags.Link);
                if (o.Flag("label", true)) p.Text(0, 0, "F/T", 1.8, RoboTags.Label, bold: true);
                p.Box(-5, 3, 5, 4, RS.White, RoboTags.Flange);
            }, (p, o) => PassAnchors(p, -4, 4)),
            new SymView(RoboViews.AxialView, new Size2(20, 20), (p, o) =>
            {
                p.Ellipse(0, 0, 7, 7, RS.Body, RoboTags.Base);
                p.Ellipse(0, 0, 5, 5, RS.Hidden, RoboTags.Flange);
                p.Box(7, -1, 8.6, 1, RS.Dark, RoboTags.Base);
                p.Arrow(0, 0, 4.6, 0, 1.2, RS.Thin);
                p.Arrow(0, 0, 0, -4.6, 1.2, RS.Thin);
                if (o.Flag("label", true)) p.Text(-3, 3.4, "F/T", 1.8, RoboTags.Label, bold: true);
            }, (p, o) => PassAxialAnchors(p)))
        {
            Params = new[] { Pm.Bool("label", "Label F/T", "Beschriftung F/T", true) },
            CompanionIds = PassCompanions,
            HasMass = true,
            ToolSide = true,
            Extra = o => new[] { Rb.Toggle(o, "robot.ft.label", new I18nText("Label F/T", "Beschriftung F/T"), IconIds.LabelShow, "label", true, "annotation") },
        };

        // ------------------------------------------------------------------ vision sensor

        /// <summary>Half opening of the field of view (degrees, 5 … 60).</summary>
        private static double HalfFov(SketchObject o) => Geo.Clamp(o.Num("angle", 50), 10, 120) / 2;

        /// <summary>Side view: apex of the field of view (design) and its length, kept inside the box.</summary>
        private const double LensY = -2.2;

        private static double FovLength(SketchObject o) => Math.Min(13 - LensY, 9.8 / Math.Tan(Geo.DegToRad(HalfFov(o))));

        private static ObjectType Vision() => new RoboBoxType("robot.sensor.vision", new I18nText("Vision sensor (camera)", "Bildverarbeitungssensor (Kamera)"), RoboCategories.Tools,
            new[] { "camera", "vision", "vision sensor", "machine vision", "smart camera", "field of view", "kamera", "bildverarbeitung", "vision-sensor", "industriekamera", "sichtfeld", "sichtkegel" },
            new SymView(RoboViews.SideView, new Size2(20, 26), (p, o) =>
            {
                if (o.Flag("cone", true))
                {
                    var len = FovLength(o);
                    var half = len * Math.Tan(Geo.DegToRad(HalfFov(o)));
                    var cone = new[] { new Vec2(0, LensY), new Vec2(half, LensY + len), new Vec2(-half, LensY + len) };
                    p.PolyD(cone, RS.Area(Fill.Solid(RoboColors.EnvelopeTint)), true, RoboTags.Fov);
                    var st = Style.ThinLine with { Stroke = RoboColors.Envelope, StrokeWidth = 0.25, Dash = DashStyle.Dash };
                    p.PolyD(new[] { cone[1], cone[0], cone[2] }, st, false, RoboTags.Fov);
                    p.Line(-half, LensY + len, half, LensY + len, st, RoboTags.Fov);
                    p.Line(0, LensY, 0, LensY + len, RS.Center, RoboTags.Axis);
                }
                p.Box(-5, -13, 5, -12, RS.White, RoboTags.Flange);
                p.RoundBox(-6, -12, 6, -5, 0.8, RS.Dark, RoboTags.Base);
                p.Round(3.6, -9.8, 0.5, RS.White, RoboTags.Base);
                p.Box(-3, -5, 3, -3, RS.Body, RoboTags.Link);
                p.Box(-2.2, -3, 2.2, LensY, RS.White, RoboTags.Link);
            }, (p, o) => new[]
            {
                Anchor.Directed(AnchorRole.Mount, p.V(0, -13), new Vec2(0, -1), "flange", p.V(5, -13).DistanceTo(p.V(0, -13))),
                RoboBoxType.Port(p, 0, o.Flag("cone", true) ? LensY + FovLength(o) : LensY, "view"),
            }),
            new SymView(RoboViews.AxialView, new Size2(20, 20), (p, o) =>
            {
                if (o.Flag("cone", true))
                {
                    // Footprint of the field of view (4:3), seen along the optical axis behind the camera.
                    // Wider for a wider opening (full box from 50°), at least around the camera body.
                    var hw = Math.Min(9.6, 4.6 + 5 * Math.Min(1, Math.Tan(Geo.DegToRad(HalfFov(o))) / Math.Tan(Geo.DegToRad(25))));
                    var hh = hw * 0.75;
                    p.Box(-hw, -hh, hw, hh, RS.Area(Fill.Solid(RoboColors.EnvelopeTint)), RoboTags.Fov);
                    p.Box(-hw, -hh, hw, hh, Style.ThinLine with { Stroke = RoboColors.Envelope, StrokeWidth = 0.25, Dash = DashStyle.Dash }, RoboTags.Fov);
                }
                p.RoundBox(-4, -4, 4, 4, 0.8, RS.Dark, RoboTags.Base);
                p.Round(0, 0, 2.6, RS.Body, RoboTags.Link);
                p.Round(0, 0, 1.4, RS.White, RoboTags.Link);
                p.Round(0, 0, 0.4, RS.Black, RoboTags.Link);
            }, (p, o) => new[] { RoboBoxType.Mount(p, 0, 0, "flange"), RoboBoxType.Port(p, 0, 0, "view") }))
        {
            Params = new[]
            {
                Pm.Num("angle", "Field of view (opening angle)", "Sichtfeld (Öffnungswinkel)", 50, 10, 120, "°", 5),
                Pm.Bool("cone", "Field of view", "Sichtfeld zeigen", true),
            },
            CompanionIds = new[] { "robot.cell.workpiece", "robot.cell.conveyor", "robot.arm.articulated" },
            HasMass = true,
            ToolSide = true,
            Extra = o => new[] { Rb.Toggle(o, "robot.vision.cone", new I18nText("Field of view", "Sichtfeld zeigen"), IconIds.FillTint, "cone", true) },
        };

        // ------------------------------------------------------------------ welding gun / torch

        private static readonly ParamDef WeldKind = Pm.Choice("kind", "Welding process", "Schweißverfahren", "spot",
            ("spot", "Spot welding gun", "Punktschweißzange"), ("arc", "Arc welding torch", "Lichtbogen-Schweißbrenner"));

        private static readonly Vec2 SpotTcp = new(5, 5.8);

        private static ObjectType WeldGun() => new RoboBoxType("robot.tool.weldgun", new I18nText("Welding gun / torch", "Schweißzange / Schweißbrenner"), RoboCategories.Tools,
            new[] { "welding gun", "weld gun", "spot welding", "resistance welding", "c-gun", "welding torch", "schweißzange", "punktschweißzange", "punktschweißen", "widerstandsschweißen", "schweißbrenner", "schweißen", "endeffektor" },
            new SymView(RoboViews.SideView, new Size2(20, 26), (p, o) =>
            {
                if (o.Str("kind", "spot") == "arc") { TorchSide(p); return; }
                p.Box(-5, -13, 5, -11.6, RS.White, RoboTags.Flange);
                p.Box(-4, -11.6, 4, -9.6, RS.Dark, RoboTags.Base);
                // Lower C-arm from the transformer round to the fixed electrode.
                p.PolyD(Rb.Capsule(new Vec2(-6, -4), new Vec2(-6, 10.6), 1.3, 6), RS.Body, true, RoboTags.Link);
                p.PolyD(Rb.Capsule(new Vec2(-6, 10.6), new Vec2(5, 10.6), 1.3, 6), RS.Body, true, RoboTags.Link);
                p.Box(4.4, 6.8, 5.6, 9.4, RS.White, RoboTags.Jaw);
                p.Poly(RS.White, true, RoboTags.Jaw, 4.4, 6.8, 4.7, 6.0, 5.3, 6.0, 5.6, 6.8);
                // Transformer with cooling fins, servo cylinder and the moving electrode.
                p.RoundBox(-9, -9.6, 3, -3, 0.8, RS.Body, RoboTags.Base);
                for (var y = -8.4; y < -3.5; y += 1.4) p.Line(-8, y, -1, y, RS.Fine);
                p.Box(3, -9, 7.4, -3.6, RS.Dark, RoboTags.Base);
                p.Box(4.2, -3.6, 5.8, -2.6, RS.Body, RoboTags.Link);
                p.Box(4.4, -2.6, 5.6, 4.6, RS.White, RoboTags.Jaw);
                p.Poly(RS.White, true, RoboTags.Jaw, 4.4, 4.6, 5.6, 4.6, 5.3, 5.6, 4.7, 5.6);
            }, (p, o) => o.Str("kind", "spot") == "arc" ? SideAnchors(p, -13, TorchTip.Y, TorchTip.X) : SideAnchors(p, -13, SpotTcp.Y, SpotTcp.X)),
            new SymView(RoboViews.AxialView, new Size2(20, 20), (p, o) =>
            {
                if (o.Str("kind", "spot") == "arc") { TorchAxial(p); return; }
                p.RoundBox(-9, -4, 3, 4, 0.8, RS.Body, RoboTags.Base);
                p.Box(3, -2.4, 7.4, 2.4, RS.Dark, RoboTags.Base);
                p.Round(SpotTcp.X, 0, 1.2, RS.White, RoboTags.Jaw);
                p.Round(SpotTcp.X, 0, 0.4, RS.Black, RoboTags.Jaw);
                p.Line(-1, 0, 1, 0, RS.Center); p.Line(0, -1, 0, 1, RS.Center);
            }, (p, o) => AxialAnchors(p, o.Str("kind", "spot") == "arc" ? TorchTip.X : SpotTcp.X)))
        {
            Params = new[] { WeldKind },
            CompanionIds = new[] { "robot.frame.tcp", "robot.tool.changer", "robot.arm.articulated" },
            HasMass = true,
            ToolSide = true,
            Extra = o => Rb.Choice(o, "robot.weldgun.kind", WeldKind),
        };

        // ------------------------------------------------------------------ adhesive dispenser

        private const string Bead = "#c98a12";

        private static ObjectType Dispenser() => new RoboBoxType("robot.tool.dispenser", new I18nText("Adhesive dispenser", "Klebedüse (Dosierventil)"), RoboCategories.Tools,
            new[] { "dispenser", "adhesive", "glue", "sealing", "sealant", "bead", "dosing valve", "nozzle", "klebedüse", "kleben", "dosierventil", "dosieren", "kleberaupe", "abdichten", "düse", "endeffektor" },
            new SymView(RoboViews.SideView, new Size2(20, 26), (p, o) =>
            {
                p.Box(-5, -13, 5, -11.6, RS.White, RoboTags.Flange);
                p.Box(-4, -11.6, 4, -10, RS.Dark, RoboTags.Base);
                p.RoundBox(-3.5, -10, 3.5, 0.6, 0.8, RS.Body, RoboTags.Base);
                p.Line(-3.5, -4.6, 3.5, -4.6, RS.Fine);
                // Feed hose to the valve.
                p.Box(3.5, -8, 5.5, -6.4, RS.Dark, RoboTags.Base);
                p.PolyD(new[] { new Vec2(5.5, -7.2), new Vec2(7.6, -8.2), new Vec2(8.8, -10.8) }, RS.Medium, false, RoboTags.Link);
                p.Box(-2.4, 0.6, 2.4, 2.6, RS.Dark, RoboTags.Link);
                p.Poly(RS.White, true, RoboTags.Jaw, -1.4, 2.6, 1.4, 2.6, 0.35, 10.6, -0.35, 10.6);
                if (o.Flag("bead", false))
                {
                    p.Round(0, 11.8, 0.7, RS.Medium with { Fill = Fill.Solid(Bead), Stroke = Bead }, RoboTags.Jaw);
                    p.Line(-6, 12.6, 6, 12.6, Style.Visible with { StrokeWidth = 0.6, Stroke = Bead }, RoboTags.Jaw);
                }
            }, (p, o) => SideAnchors(p, -13, 10.6)),
            new SymView(RoboViews.AxialView, new Size2(20, 20), (p, o) =>
            {
                p.Box(3.5, -0.8, 5.5, 0.8, RS.Dark, RoboTags.Base);
                p.Ellipse(0, 0, 3.5, 3.5, RS.Body, RoboTags.Base);
                p.Round(0, 0, 1.4, RS.White, RoboTags.Jaw);
                p.Round(0, 0, 0.35, RS.Black, RoboTags.Jaw);
            }, (p, o) => AxialAnchors(p)))
        {
            Params = new[] { Pm.Bool("bead", "Adhesive bead", "Kleberaupe", false) },
            CompanionIds = new[] { "robot.frame.tcp", "robot.cell.workpiece", "robot.arm.articulated" },
            HasMass = true,
            ToolSide = true,
            Extra = o => new[] { Rb.Toggle(o, "robot.dispenser.bead", new I18nText("Adhesive bead", "Kleberaupe"), IconIds.Toggle, "bead", false) },
        };
    }
}
