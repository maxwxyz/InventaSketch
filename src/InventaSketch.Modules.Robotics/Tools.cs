using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Robotics
{
    /// <summary>
    /// Grippers and end effectors. Side view: the flange is at the top edge (mount anchor "flange"), the tool points down;
    /// the port "tcp" marks the tool centre point. View "axial": seen along the tool axis from the flange.
    /// </summary>
    internal static class Tools
    {
        private static readonly string[] Companions = { "robot.frame.tcp", "robot.cell.workpiece", "robot.arm.articulated" };

        public static IEnumerable<ObjectType> Types()
        {
            yield return Parallel();
            yield return Vacuum();
            yield return Magnetic();
            yield return ThreeFinger();
            yield return Torch();
            yield return Spindle();
            yield return CoordinateFrame();
            yield return TcpMarker();
        }

        private static readonly ParamDef OpenParam = Pm.Num("open", "Opening (0 = closed, 1 = open)", "Öffnung (0 = zu, 1 = offen)", 0.6, 0, 1, null, 0.1);

        /// <summary>Flange plate and adapter at the top of a side view (design y −11 … −8).</summary>
        private static void FlangeTop(Pad p, double top)
        {
            p.Box(-5, top, 5, top + 1.4, RS.White, RoboTags.Flange);
            p.Box(-3.5, top + 1.4, 3.5, top + 3, RS.Body, RoboTags.Flange);
        }

        private static IEnumerable<ContextAction> OpenActions(SketchObject o)
        {
            var v = Geo.Clamp(o.Num("open", 0.6), 0, 1);
            yield return new ContextAction("robot.gripper.toggle", v > 0.3 ? new I18nText("Close gripper", "Greifer schließen") : new I18nText("Open gripper", "Greifer öffnen"),
                IconIds.Toggle, x => x.WithParam("open", Geo.Clamp(x.Num("open", 0.6), 0, 1) > 0.3 ? 0.0 : 0.8)) { IsActive = v > 0.3, Group = "param", ParamKeys = new[] { "open" } };
        }

        private static IEnumerable<Anchor> SideAnchors(Pad p, double top, double tcpY, double tcpX = 0)
        {
            // The mount faces up, back to the robot flange (a "+" on the flange turns the tool to match).
            // Face radius: half the flange plate (design x −5 … 5), so a "+" on a robot flange scales the tool to match.
            yield return Anchor.Directed(AnchorRole.Mount, p.V(0, top), new Vec2(0, -1), "flange", p.V(5, top).DistanceTo(p.V(0, top)));
            yield return RoboBoxType.Port(p, tcpX, tcpY, "tcp");
        }

        private static IEnumerable<Anchor> AxialAnchors(Pad p, double tcpX = 0)
        {
            yield return RoboBoxType.Mount(p, 0, 0, "flange");
            yield return RoboBoxType.Port(p, tcpX, 0, "tcp");
        }

        // ------------------------------------------------------------------ parallel gripper

        private static ObjectType Parallel() => new RoboBoxType("robot.gripper.parallel", new I18nText("Parallel gripper", "Parallelgreifer"), RoboCategories.Tools,
            new[] { "gripper", "parallel gripper", "two-jaw", "jaws", "greifer", "parallelgreifer", "zweibackengreifer", "backengreifer", "endeffektor", "end effector" },
            new SymView(RoboViews.SideView, new Size2(20, 22), (p, o) =>
            {
                var open = Geo.Clamp(o.Num("open", 0.6), 0, 1);
                FlangeTop(p, -11);
                p.RoundBox(-9, -8, 9, 0, 1, RS.Body, RoboTags.Base);
                p.Line(-6, -4, 6, -4, RS.Fine);
                p.Box(-9.5, 0, 9.5, 1.2, RS.Dark, RoboTags.Base);
                var xi = 1.0 + open * 5.4;
                foreach (var sgn in new[] { -1, 1 })
                {
                    double X(double x) => sgn * x;
                    p.Poly(RS.White, true, RoboTags.Jaw, X(xi), 1.2, X(xi + 3.2), 1.2, X(xi + 3.2), 3.6, X(xi + 1.8), 4.8, X(xi + 1.8), 10.8, X(xi), 10.8);
                    p.Box(X(xi), 7.4, X(xi + 0.5), 10.4, RS.Dark, RoboTags.Jaw);
                }
                if (o.Flag("arrows", false))
                {
                    p.Arrow(-xi - 1.6, -2.2, -xi - 4.6, -2.2, 1.0, RS.Thin);
                    p.Arrow(xi + 1.6, -2.2, xi + 4.6, -2.2, 1.0, RS.Thin);
                }
            }, (p, o) => SideAnchors(p, -11, 9.2)),
            new SymView(RoboViews.AxialView, new Size2(20, 20), (p, o) =>
            {
                var open = Geo.Clamp(o.Num("open", 0.6), 0, 1);
                p.RoundBox(-9, -5, 9, 5, 1, RS.Body, RoboTags.Base);
                p.Ellipse(0, 0, 5, 5, RS.Hidden, RoboTags.Flange);
                var xi = 1.0 + open * 5.4;
                foreach (var sgn in new[] { -1, 1 }) p.Box(sgn * xi, -3.6, sgn * (xi + 1.8), 3.6, RS.White, RoboTags.Jaw);
                p.Line(-1, 0, 1, 0, RS.Center); p.Line(0, -1, 0, 1, RS.Center);
            }, (p, o) => AxialAnchors(p)))
        {
            Params = new[] { OpenParam, Pm.Bool("arrows", "Stroke arrows", "Hubpfeile", false) },
            CompanionIds = Companions,
            Extra = o => OpenActions(o).Concat(new[] { Rb.Toggle(o, "robot.gripper.arrows", new I18nText("Stroke arrows", "Hubpfeile"), IconIds.ArrowEnd, "arrows", false) }),
        };

        // ------------------------------------------------------------------ vacuum gripper

        private static ObjectType Vacuum() => new RoboBoxType("robot.gripper.vacuum", new I18nText("Vacuum gripper", "Vakuumgreifer (Sauggreifer)"), RoboCategories.Tools,
            new[] { "vacuum", "suction", "suction cup", "vacuum gripper", "sauggreifer", "saugnapf", "vakuum", "vakuumgreifer", "greifer" },
            new SymView(RoboViews.SideView, new Size2(20, 22), (p, o) =>
            {
                var n = Math.Max(1, Math.Min(4, o.Int("cups", 1)));
                FlangeTop(p, -11);
                p.Box(-4, -8, 4, -4, RS.Body, RoboTags.Base);
                p.Box(4, -7, 6.5, -5.4, RS.Body, RoboTags.Base);
                p.Line(6.5, -6.2, 8.8, -6.2, RS.Medium);
                if (n == 1) Cup(p, 0, 1.0);
                else
                {
                    p.Box(-9, -4, 9, -2.6, RS.Dark, RoboTags.Base);
                    var span = 14.0;
                    for (var i = 0; i < n; i++) Cup(p, -span / 2 + span * i / (n - 1), 0.62);
                }
            }, (p, o) => SideAnchors(p, -11, 10.6)),
            new SymView(RoboViews.AxialView, new Size2(20, 20), (p, o) =>
            {
                var n = Math.Max(1, Math.Min(4, o.Int("cups", 1)));
                if (n == 1) { p.Round(0, 0, 5.5, RS.Body, RoboTags.Jaw); p.Round(0, 0, 3, RS.White, RoboTags.Jaw); p.Round(0, 0, 0.7, RS.Black, RoboTags.Jaw); return; }
                p.Box(-9, -1.2, 9, 1.2, RS.Dark, RoboTags.Base);
                var span = 12.4;
                for (var i = 0; i < n; i++)
                {
                    var x = -span / 2 + span * i / (n - 1);
                    p.Round(x, 0, 3.4, RS.Body, RoboTags.Jaw); p.Round(x, 0, 1.9, RS.White, RoboTags.Jaw);
                }
            }, (p, o) => AxialAnchors(p)))
        {
            Params = new[] { Pm.Int("cups", "Suction cups", "Saugnäpfe", 1, 1, 4) },
            CompanionIds = Companions,
            Extra = o => new[]
            {
                new ContextAction("robot.cups.more", new I18nText("More cups", "Mehr Saugnäpfe"), IconIds.Increase, x => x.WithParam("cups", Math.Min(4, x.Int("cups", 1) + 1))) { Group = "param", ParamKeys = new[] { "cups" }, IsEnabled = o.Int("cups", 1) < 4 },
                new ContextAction("robot.cups.fewer", new I18nText("Fewer cups", "Weniger Saugnäpfe"), IconIds.Decrease, x => x.WithParam("cups", Math.Max(1, x.Int("cups", 1) - 1))) { Group = "param", ParamKeys = new[] { "cups" }, IsEnabled = o.Int("cups", 1) > 1 },
            },
        };

        /// <summary>Suction cup with bellows at design x (scale k), from y −4 (stem top) to 10.6 (lip).</summary>
        private static void Cup(Pad p, double x, double k)
        {
            p.Box(x - 0.9 * k, -4, x + 0.9 * k, 3, RS.Body, RoboTags.Jaw);
            var pts = new List<Vec2>();
            for (var i = 0; i <= 6; i++) pts.Add(new Vec2(x + (i % 2 == 0 ? -2.0 : -3.0) * k, 3 + 4.0 * i / 6));
            var right = pts.Select(q => new Vec2(2 * x - q.X, q.Y)).Reverse().ToList();
            p.PolyD(pts.Concat(right), RS.Body, true, RoboTags.Jaw);
            p.Poly(RS.Dark, true, RoboTags.Jaw, x - 2.2 * k, 7, x - 4.8 * k, 10.6, x + 4.8 * k, 10.6, x + 2.2 * k, 7);
        }

        // ------------------------------------------------------------------ magnetic gripper

        private static ObjectType Magnetic() => new RoboBoxType("robot.gripper.magnetic", new I18nText("Magnetic gripper", "Magnetgreifer"), RoboCategories.Tools,
            new[] { "magnet", "magnetic gripper", "electromagnet", "magnetgreifer", "elektromagnet", "haftmagnet", "greifer" },
            new SymView(RoboViews.SideView, new Size2(20, 22), (p, o) =>
            {
                FlangeTop(p, -11);
                p.RoundBox(-8, -8, 8, 3.6, 1, RS.Body, RoboTags.Base);
                // Coil indicated by a winding zigzag.
                var z = new List<Vec2>();
                for (var i = 0; i <= 8; i++) z.Add(new Vec2(-4.5 + 9.0 * i / 8, i % 2 == 0 ? -4.4 : -1.2));
                p.PolyD(z, RS.Fine, false);
                p.Box(-8, 3.6, 8, 7.2, RS.White, RoboTags.Jaw);
                for (var i = 0; i < 4; i++)
                {
                    var x0 = -8 + 4 * i;
                    if (i % 2 == 0) p.Box(x0, 3.6, x0 + 4, 7.2, RS.HatchWhite(0.9 * p.K), RoboTags.Jaw);
                    p.Text(x0 + 2, 5.4, i % 2 == 0 ? "N" : "S", 1.8);
                }
                for (var i = 0; i < 3; i++) p.Arc(-4 + 4 * i, 7.2, 2, 2.4, 0, 180, RS.Fine);
            }, (p, o) => SideAnchors(p, -11, 7.2)),
            new SymView(RoboViews.AxialView, new Size2(20, 20), (p, o) =>
            {
                p.RoundBox(-8, -8, 8, 8, 1, RS.White, RoboTags.Jaw);
                for (var i = 0; i < 4; i += 2) p.Box(-8 + 4 * i, -8, -4 + 4 * i, 8, RS.HatchWhite(0.9 * p.K), RoboTags.Jaw);
                p.Line(-1, 0, 1, 0, RS.Center); p.Line(0, -1, 0, 1, RS.Center);
            }, (p, o) => AxialAnchors(p)))
        {
            CompanionIds = Companions,
        };

        // ------------------------------------------------------------------ three-finger gripper

        private static ObjectType ThreeFinger() => new RoboBoxType("robot.gripper.threefinger", new I18nText("Three-finger gripper", "Dreifingergreifer"), RoboCategories.Tools,
            new[] { "three-finger", "three jaw", "centric gripper", "adaptive gripper", "dreifinger", "dreibackengreifer", "zentrischgreifer", "greifer" },
            new SymView(RoboViews.SideView, new Size2(20, 22), (p, o) =>
            {
                var open = Geo.Clamp(o.Num("open", 0.6), 0, 1);
                FlangeTop(p, -11);
                p.RoundBox(-8, -8, 8, 0.4, 2, RS.Body, RoboTags.Base);
                // The finger at 0° (behind) as hidden outline, the two at ±120° in view.
                Finger(p, 0, open, RS.Hidden);
                Finger(p, -1, open, RS.White);
                Finger(p, 1, open, RS.White);
            }, (p, o) => SideAnchors(p, -11, 10.0)),
            new SymView(RoboViews.AxialView, new Size2(20, 20), (p, o) =>
            {
                var open = Geo.Clamp(o.Num("open", 0.6), 0, 1);
                p.Ellipse(0, 0, 8.5, 8.5, RS.Body, RoboTags.Base);
                p.Ellipse(0, 0, 2.2 + open * 3.4, 2.2 + open * 3.4, RS.Hidden);
                for (var i = 0; i < 3; i++)
                {
                    var a = Geo.DegToRad(-90 + 120 * i);
                    var u = new Vec2(Math.Cos(a), Math.Sin(a));
                    var v = u.Perp;
                    var r0 = 1.4 + open * 3.4;
                    var c0 = u * r0; var c1 = u * (r0 + 4.2);
                    p.PolyD(new[] { c0 - v * 1.1, c1 - v * 1.1, c1 + v * 1.1, c0 + v * 1.1 }, RS.White, true, RoboTags.Jaw);
                }
                p.Line(-1, 0, 1, 0, RS.Center); p.Line(0, -1, 0, 1, RS.Center);
            }, (p, o) => AxialAnchors(p)))
        {
            Params = new[] { OpenParam },
            CompanionIds = Companions,
            Extra = OpenActions,
        };

        /// <summary>Two-segment finger (side view); side −1/1 left/right, 0 = centre finger behind.</summary>
        private static void Finger(Pad p, int side, double open, Style s)
        {
            var sx = side == 0 ? 0 : side;
            Vec2 b = new(sx * (4.2 + open * 1.6), 0.4), k = new(sx * (3.6 + open * 3.0), 5.6), t = new(sx * (1.3 + open * 4.4), 10.0);
            if (side == 0) { b = new Vec2(0, 0.4); k = new Vec2(0, 5.6); t = new Vec2(0, 10.0); }
            var w = 0.95;
            p.PolyD(Rb.Capsule(b, k, w, 6), s, true, RoboTags.Jaw);
            p.PolyD(Rb.Capsule(k, t, w * 0.85, 6), s, true, RoboTags.Jaw);
            if (side != 0) p.Round(k.X, k.Y, 0.55, RS.White, RoboTags.Joint);
        }

        // ------------------------------------------------------------------ welding torch

        private static readonly Vec2 NeckDir = new(Math.Sin(Geo.DegToRad(35)), Math.Cos(Geo.DegToRad(35)));
        private static Vec2 TorchTip => NeckDir * (6 + 4.2 + 1.6);

        private static ObjectType Torch() => new RoboBoxType("robot.tool.torch", new I18nText("Welding torch", "Schweißbrenner"), RoboCategories.Tools,
            new[] { "welding", "torch", "mig", "mag", "arc welding", "schweißen", "schweißbrenner", "brenner", "schutzgas", "endeffektor" },
            new SymView(RoboViews.SideView, new Size2(20, 26), (p, o) =>
            {
                p.Box(-5, -13, 5, -11.6, RS.White, RoboTags.Flange);
                p.Box(-4, -11.6, 4, -7, RS.Dark, RoboTags.Base);
                // Hose package leaving the clamp to the side.
                p.PolyD(new[] { new Vec2(-4, -9.4), new Vec2(-6.5, -10.2), new Vec2(-8.6, -12.4) }, RS.Medium, false, RoboTags.Link);
                p.PolyD(new[] { new Vec2(-4, -8.2), new Vec2(-7.2, -9.2), new Vec2(-9.6, -11.6) }, RS.Medium, false, RoboTags.Link);
                p.Box(-2.2, -7, 2.2, 0.4, RS.Body, RoboTags.Link);
                var n0 = new Vec2(0, 0); var n1 = NeckDir * 6; var n2 = n1 + NeckDir * 4.2;
                p.PolyD(Rb.Capsule(n0, n1, 1.7, 6), RS.Body, true, RoboTags.Link);
                var v = NeckDir.Perp;
                p.PolyD(new[] { n1 - v * 2.1, n2 - v * 1.4, n2 + v * 1.4, n1 + v * 2.1 }, RS.Dark, true, RoboTags.Jaw);
                p.Add(Prim.Line(p.V(n2), p.V(TorchTip), RS.Medium), RoboTags.Jaw);
            }, (p, o) => SideAnchors(p, -13, TorchTip.Y, TorchTip.X)),
            new SymView(RoboViews.AxialView, new Size2(20, 20), (p, o) =>
            {
                p.Round(0, 0, 4, RS.Dark, RoboTags.Base);
                p.Round(0, 0, 2.2, RS.Body, RoboTags.Link);
                var x = TorchTip.X;
                p.Round(x, 0, 2.1, RS.Dark, RoboTags.Jaw);
                p.Round(x, 0, 1.1, RS.White, RoboTags.Jaw);
                p.Round(x, 0, 0.3, RS.Black, RoboTags.Jaw);
            }, (p, o) => AxialAnchors(p, TorchTip.X)))
        {
            CompanionIds = Companions,
        };

        // ------------------------------------------------------------------ spindle

        private static ObjectType Spindle() => new RoboBoxType("robot.tool.spindle", new I18nText("Spindle (milling / drilling)", "Spindel (Fräsen / Bohren)"), RoboCategories.Tools,
            new[] { "spindle", "milling", "drilling", "deburring", "spindel", "frässpindel", "fräsen", "bohren", "entgraten", "endeffektor" },
            new SymView(RoboViews.SideView, new Size2(20, 26), (p, o) =>
            {
                p.Box(-5, -13, 5, -11.6, RS.White, RoboTags.Flange);
                p.Box(-6, -11.6, 6, 2, RS.Body, RoboTags.Base);
                for (var y = -9.6; y < 0.5; y += 2) p.Line(-6, y, 6, y, RS.Fine);
                p.Box(6, -10, 8, -6.6, RS.Dark, RoboTags.Base);
                p.Poly(RS.Body, true, RoboTags.Link, -4, 2, 4, 2, 2.6, 5, -2.6, 5);
                p.Box(-2.4, 5, 2.4, 6.6, RS.Dark, RoboTags.Link);
                p.Box(-1, 6.6, 1, 8.6, RS.White, RoboTags.Jaw);
                p.Poly(RS.White, true, RoboTags.Jaw, -1, 8.6, 1, 8.6, 1, 12.2, 0, 12.8, -1, 12.2);
                for (var y = 9.2; y < 12; y += 1.1) p.Line(-1, y + 0.6, 1, y, RS.Fine);
                if (o.Flag("arrow", false)) p.Arc(0, 7.6, 4.2, 1.6, 200, 140, RS.Arrow);
            }, (p, o) => SideAnchors(p, -13, 12.8)),
            new SymView(RoboViews.AxialView, new Size2(20, 20), (p, o) =>
            {
                p.Box(-6, -6, 6, 6, RS.Body, RoboTags.Base);
                p.Round(0, 0, 4, RS.Body, RoboTags.Link);
                p.Round(0, 0, 2.4, RS.Dark, RoboTags.Link);
                p.Round(0, 0, 1, RS.White, RoboTags.Jaw);
                for (var i = 0; i < 4; i++)
                {
                    var a = Geo.DegToRad(45 + 90 * i);
                    p.Line(0, 0, Math.Cos(a), Math.Sin(a), RS.Fine);
                }
            }, (p, o) => AxialAnchors(p)))
        {
            Params = new[] { Pm.Bool("arrow", "Rotation arrow", "Drehrichtungspfeil", false) },
            CompanionIds = Companions,
            Extra = o => new[] { Rb.Toggle(o, "robot.spindle.arrow", new I18nText("Rotation arrow", "Drehrichtungspfeil"), IconIds.Rotate, "arrow", false) },
        };

        // ------------------------------------------------------------------ coordinate frame

        internal static readonly ParamDef FrameKind = Pm.Choice("kind", "Frame", "Koordinatensystem", "base",
            ("world", "World", "Welt"), ("base", "Base", "Basis"), ("tool", "Tool", "Werkzeug"), ("user", "User", "Benutzer"), ("object", "Workpiece", "Werkstück"));

        internal static readonly ParamDef FrameAxes = Pm.Choice("axes", "Axes in the drawing", "Achsen in der Zeichnung", "xz",
            ("xz", "x right, z up (side view)", "x rechts, z oben (Seitenansicht)"),
            ("xy", "x right, y up (top view)", "x rechts, y oben (Draufsicht)"),
            ("yz", "y right, z up (front view)", "y rechts, z oben (Vorderansicht)"));

        /// <summary>
        /// Coordinate frame symbol (right-handed): two axis arrows in the drawing plane and the third axis at the origin,
        /// a dot (towards the observer) or a cross (away from the observer).
        /// </summary>
        private static ObjectType CoordinateFrame() => new RoboBoxType("robot.frame.coordinate", new I18nText("Coordinate frame", "Koordinatensystem"), RoboCategories.Frames,
            new[] { "frame", "coordinate system", "axes", "base frame", "tool frame", "world frame", "koordinatensystem", "basiskoordinaten", "werkzeugkoordinaten", "achsenkreuz" },
            new SymView(RoboViews.SideView, new Size2(16, 16), (p, o) =>
            {
                var axes = o.Str("axes", "xz");
                var (h, v, t, towards) = axes switch
                {
                    "xy" => ("x", "y", "z", true),
                    "yz" => ("y", "z", "x", true),
                    _ => ("x", "z", "y", false),
                };
                var colH = RoboColors.Red; var colV = axes == "xy" ? RoboColors.Green : RoboColors.Blue;
                var colT = axes == "xz" ? RoboColors.Green : axes == "xy" ? RoboColors.Blue : RoboColors.Red;
                var sh = RS.Medium with { Stroke = colH };
                var sv = RS.Medium with { Stroke = colV };
                p.Arrow(-5.5, 5.5, 6.6, 5.5, 1.6, sh);
                p.Arrow(-5.5, 5.5, -5.5, -6.6, 1.6, sv);
                p.Text(6.6, 3.6, h, 2.2, RoboTags.Axis, italic: true, color: colH);
                p.Text(-3.6, -6.4, v, 2.2, RoboTags.Axis, italic: true, color: colV);
                var st = RS.White with { Stroke = colT, StrokeWidth = 0.3 };
                p.Round(-5.5, 5.5, 1.1, st, RoboTags.Axis);
                if (towards) p.Round(-5.5, 5.5, 0.35, RS.Black with { Fill = Fill.Solid(colT), Stroke = colT }, RoboTags.Axis);
                else
                {
                    var c = p.V(-5.5, 5.5); var d = 0.75 * p.K;
                    p.Add(Prim.Line(c + new Vec2(-d, -d), c + new Vec2(d, d), RS.Thin with { Stroke = colT }), RoboTags.Axis);
                    p.Add(Prim.Line(c + new Vec2(-d, d), c + new Vec2(d, -d), RS.Thin with { Stroke = colT }), RoboTags.Axis);
                }
                p.Text(-7.0, 2.9, t, 2.0, RoboTags.Axis, italic: true, color: colT);
                if (o.Flag("name", true))
                {
                    var kind = FrameKind.Options!.FirstOrDefault(x => x.Value == o.Str("kind", "base")) ?? FrameKind.Options![1];
                    p.Text(2.6, -2.4, kind.Label.Get(p.Lang), 2.2, RoboTags.Label, bold: true);
                }
            }, (p, o) => new[] { RoboBoxType.Port(p, -5.5, 5.5, "origin") }))
        {
            NoViews = true,
            Uniform = true,
            Part = false,
            Params = new[] { FrameKind, FrameAxes, Pm.Bool("name", "Show name", "Name zeigen", true) },
            Extra = o => new[]
            {
                Rb.Cycle(o, "robot.frame.kind", FrameKind),
                Rb.Cycle(o, "robot.frame.axes", FrameAxes, "axes"),
                Rb.Toggle(o, "robot.frame.name", new I18nText("Show name", "Name zeigen"), IconIds.Text, "name", true, "annotation"),
            },
        };

        // ------------------------------------------------------------------ TCP marker

        private static ObjectType TcpMarker() => new RoboBoxType("robot.frame.tcp", new I18nText("Tool centre point (TCP)", "Werkzeugarbeitspunkt (TCP)"), RoboCategories.Frames,
            new[] { "tcp", "tool center point", "tool centre point", "werkzeugarbeitspunkt", "werkzeugspitze", "arbeitspunkt" },
            new SymView(RoboViews.SideView, new Size2(10, 8), (p, o) =>
            {
                double cx = -2, cy = 1;
                p.Round(cx, cy, 2.3, RS.White with { Stroke = RoboColors.Red, StrokeWidth = 0.3 }, RoboTags.Axis);
                var c = p.V(cx, cy); var r = 3.0 * p.K; var g = 0.9 * p.K;
                var st = RS.Thin with { Stroke = RoboColors.Red };
                p.Add(Prim.Line(c + new Vec2(-r, 0), c + new Vec2(-g, 0), st), RoboTags.Axis);
                p.Add(Prim.Line(c + new Vec2(g, 0), c + new Vec2(r, 0), st), RoboTags.Axis);
                p.Add(Prim.Line(c + new Vec2(0, -r), c + new Vec2(0, -g), st), RoboTags.Axis);
                p.Add(Prim.Line(c + new Vec2(0, g), c + new Vec2(0, r), st), RoboTags.Axis);
                p.Round(cx, cy, 0.35, RS.Black with { Fill = Fill.Solid(RoboColors.Red), Stroke = RoboColors.Red }, RoboTags.Axis);
                if (o.Flag("name", true)) p.Text(2.6, -2.4, "TCP", 2.2, RoboTags.Label, bold: true, color: RoboColors.Red);
            }, (p, o) => new[] { RoboBoxType.Port(p, -2, 1, "tcp") }))
        {
            NoViews = true,
            Uniform = true,
            Part = false,
            Params = new[] { Pm.Bool("name", "Show label", "Beschriftung zeigen", true) },
            Extra = o => new[] { Rb.Toggle(o, "robot.tcp.name", new I18nText("Show label", "Beschriftung zeigen"), IconIds.Text, "name", true, "annotation") },
        };
    }
}
