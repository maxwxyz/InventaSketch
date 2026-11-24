using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.BuildingServices
{
    /// <summary>Plumbing and sanitary: WC, washbasin, shower, bathtub, water heater, floor drain, riser.</summary>
    internal static class Plumbing
    {
        private const string Cat = BsCategories.Plumbing;

        private static readonly ParamDef Coil = Pm.Bool("coil", "Heating coil", "Heizwendel", true);
        private static readonly ParamDef RiserDir = Pm.Choice("dir", "Direction in plan", "Richtung im Grundriss", "up",
            ("up", "Rising (continues up)", "Steigend (nach oben)"), ("down", "Falling (continues down)", "Fallend (nach unten)"),
            ("through", "Passing through", "Durchgehend"));

        public static IEnumerable<ObjectType> Types()
        {
            yield return new BsBoxType("bs.san.wc", new I18nText("WC", "WC"), Cat,
                new Size2(3.6, 5.4), new Size2(3.6, 4.0), new Size2(3.6, 3.6), WcPlan, WcSection, WcSchematic,
                "wc", "toilet", "water closet", "toilette", "klosett", "bad", "bathroom", "sanitär", "tga")
            {
                Mount = Mounting.Floor, DefaultHeight = 0,
                Ports = BsBoxType.Uv(new[] { (0.0, -0.5, "waste"), (0.3, -0.5, "cold") }, new[] { (0.0, 0.5, "waste"), (0.3, -0.5, "cold") },
                    new[] { (0.0, 0.5, "waste"), (0.3, -0.5, "cold") }),
                CompanionIds = new[] { PipeType.TypeId, "bs.san.riser", "bs.san.basin" },
            };

            yield return new BsBoxType("bs.san.basin", new I18nText("Washbasin", "Waschbecken"), Cat,
                new Size2(6, 4.5), new Size2(6, 4.0), new Size2(6, 3.5), BasinPlan, BasinSection, BasinSchematic,
                "washbasin", "basin", "sink", "lavatory", "waschbecken", "waschtisch", "bad", "bathroom", "sanitär", "tga")
            {
                Mount = Mounting.Wall, DefaultHeight = 500,
                Ports = BsBoxType.Uv(new[] { (0.0, -0.5, "waste"), (-0.2, -0.5, "hot"), (0.2, -0.5, "cold") },
                    new[] { (0.0, 0.5, "waste"), (-0.2, 0.15, "hot"), (0.2, 0.15, "cold") },
                    new[] { (0.0, 0.5, "waste"), (-0.2, -0.5, "hot"), (0.2, -0.5, "cold") }),
                CompanionIds = new[] { PipeType.TypeId, "bs.san.riser", "bs.san.wc" },
            };

            yield return new BsBoxType("bs.san.shower", new I18nText("Shower", "Dusche"), Cat,
                new Size2(9, 9), new Size2(9, 22), new Size2(9, 8), ShowerPlan, ShowerSection, ShowerSchematic,
                "shower", "shower tray", "dusche", "duschwanne", "bad", "bathroom", "sanitär", "tga")
            {
                Mount = Mounting.Floor, DefaultHeight = 0, Uniform = false,
                Ports = BsBoxType.Uv(new[] { (0.0, 0.0, "waste"), (0.25, -0.5, "hot"), (0.35, -0.5, "cold") },
                    new[] { (0.0, 0.5, "waste"), (0.25, 0.0, "hot"), (0.35, 0.0, "cold") },
                    new[] { (0.0, 0.5, "waste"), (0.5, -0.05, "hot"), (0.5, 0.1, "cold") }),
                CompanionIds = new[] { PipeType.TypeId, "bs.san.drain", "bs.san.riser" },
            };

            yield return new BsBoxType("bs.san.bathtub", new I18nText("Bathtub", "Badewanne"), Cat,
                new Size2(17, 7.5), new Size2(17, 6), new Size2(17, 4), TubPlan, TubSection, TubSchematic,
                "bathtub", "bath", "tub", "badewanne", "wanne", "bad", "bathroom", "sanitär", "tga")
            {
                Mount = Mounting.Floor, DefaultHeight = 0,
                Ports = BsBoxType.Uv(new[] { (-0.4, 0.0, "waste"), (-0.45, -0.5, "hot"), (-0.35, -0.5, "cold") },
                    new[] { (-0.4, 0.5, "waste"), (-0.45, -0.5, "hot"), (-0.35, -0.5, "cold") }),
                CompanionIds = new[] { PipeType.TypeId, "bs.san.riser" },
            };

            yield return new BsBoxType("bs.san.heater", new I18nText("Water heater (storage cylinder)", "Warmwasserspeicher"), Cat,
                new Size2(6, 6), new Size2(6, 16), new Size2(6, 10), HeaterPlan, HeaterSection, HeaterSchematic,
                "water heater", "hot water cylinder", "storage tank", "dhw", "calorifier", "boiler", "warmwasserspeicher", "speicher",
                "trinkwasserspeicher", "warmwasserbereiter", "tga")
            {
                Mount = Mounting.Floor, DefaultHeight = 0,
                Params = new[] { Coil },
                Ports = HeaterPorts,
                Extra = o => new[] { Bs.Toggle(o, "bs.heater.coil", new I18nText("Heating coil", "Heizwendel"), IconIds.Toggle, "coil", true) },
                CompanionIds = new[] { PipeType.TypeId, "bs.heat.boiler", "bs.heat.pump" },
            };

            yield return new BsBoxType("bs.san.drain", new I18nText("Floor drain", "Bodenablauf"), Cat,
                new Size2(2, 2), new Size2(2, 1.5), new Size2(2, 2), DrainPlan, DrainSection, DrainSchematic,
                "floor drain", "gully", "drain", "bodenablauf", "bodeneinlauf", "gully", "abfluss", "entwässerung", "tga")
            {
                Mount = Mounting.InFloor, WallBacked = false, ToScale = false, Uniform = true, DefaultHeight = -150,
                Ports = BsBoxType.Uv(new[] { (0.0, 0.0, "waste") }, new[] { (0.0, 0.5, "waste") }),
                CompanionIds = new[] { PipeType.TypeId, "bs.san.shower" },
            };

            yield return new BsBoxType("bs.san.riser", new I18nText("Riser (vertical pipe)", "Steigleitung / Fallleitung"), Cat,
                new Size2(2, 2), new Size2(2, 28), new Size2(2, 28), RiserPlan, RiserSection, RiserSchematic,
                "riser", "stack", "soil stack", "vertical pipe", "steigleitung", "fallleitung", "strang", "steigstrang", "tga")
            {
                Mount = Mounting.Floor, WallBacked = false, DefaultHeight = 0,
                Params = new[] { Media.PipeParam("cold"), RiserDir },
                Ports = BsBoxType.Uv(new[] { (0.0, 0.0, "riser") }, new[] { (0.0, 0.5, "bottom"), (0.0, -0.5, "top") }),
                Rails = (c, _, view) => view == BsViews.Plan ? Array.Empty<(Vec2, Vec2)>() : new[] { (new Vec2(0, c.Hh), new Vec2(0, -c.Hh)) },
                Extra = o => new[]
                {
                    Bs.Cycle(o, "bs.riser.medium", Media.PipeParam("cold")) with { Icon = IconIds.Medium },
                    Bs.Cycle(o, "bs.riser.dir", RiserDir),
                },
                CompanionIds = new[] { PipeType.TypeId, "bs.san.wc", "bs.san.basin" },
            };
        }

        // ------------------------------------------------------------------ WC

        private static void WcPlan(Canvas c, SketchObject o, RenderContext ctx)
        {
            // Concealed cistern frame at the wall, bowl in front.
            c.RectN(-0.45, -0.5, 0.45, -0.32, St.Thin, "cistern");
            var centre = c.N(0, 0.1);
            c.Ellipse(centre, 0.44 * c.W, 0.38 * c.H, St.Thin, BsTags.Outline);
            c.Ellipse(c.N(0, 0.14), 0.28 * c.W, 0.26 * c.H, St.Fine, "opening");
            c.LineN(-0.3, -0.32, -0.3, -0.18, St.Fine, "neck");
            c.LineN(0.3, -0.32, 0.3, -0.18, St.Fine, "neck");
        }

        private static void WcSection(Canvas c, SketchObject o, RenderContext ctx)
        {
            Vec2 P(double u, double v) => c.N(u, v);
            // Seat and lid on the rim, bowl narrowing to the pedestal foot.
            c.RoundRect(-0.48 * c.W, -0.5 * c.H, 0.48 * c.W, -0.4 * c.H, 0.04 * c.K, St.Thin, "seat");
            c.Add(new PathPrim(new[]
            {
                PathSeg.Move(P(-0.46, -0.4)),
                PathSeg.Cubic(P(-0.46, -0.1), P(-0.24, -0.05), P(-0.22, 0.12)),
                PathSeg.Line(P(-0.22, 0.42)), PathSeg.Line(P(-0.3, 0.5)), PathSeg.Line(P(0.3, 0.5)), PathSeg.Line(P(0.22, 0.42)),
                PathSeg.Line(P(0.22, 0.12)),
                PathSeg.Cubic(P(0.24, -0.05), P(0.46, -0.1), P(0.46, -0.4)),
                PathSeg.Close,
            }, St.Thin), BsTags.Outline);
            c.LineN(-0.22, 0.42, 0.22, 0.42, St.Fine, "foot");
        }

        private static void WcSchematic(Canvas c, SketchObject o, RenderContext ctx)
        {
            c.RectN(-0.25, -0.5, 0.45, -0.3, St.Medium, "cistern");
            c.PolyN(St.Medium, true, BsTags.Outline, -0.45, -0.2, 0.45, -0.2, 0.18, 0.25, -0.18, 0.25);
            c.LineN(0, 0.25, 0, 0.5, St.Medium, BsTags.Stub);
        }

        // ------------------------------------------------------------------ washbasin

        private static void BasinPlan(Canvas c, SketchObject o, RenderContext ctx)
        {
            c.RoundRect(-c.Hw, -c.Hh, c.Hw, c.Hh, 0.12 * c.K, St.Thin, BsTags.Outline);
            c.Ellipse(c.N(0, 0.08), 0.36 * c.W, 0.3 * c.H, St.Fine, "bowl");
            c.Circle(c.N(0, 0.12), 0.04 * c.K, St.Fine, "outlet");
            // Tap at the back.
            c.Circle(c.N(0, -0.34), 0.06 * c.K, St.Thin, "tap");
            c.LineN(0, -0.34, 0, -0.18, St.Thin, "spout");
        }

        private static void BasinSection(Canvas c, SketchObject o, RenderContext ctx)
        {
            Vec2 P(double u, double v) => c.N(u, v);
            // Rim at 850 mm (box 500…900 mm), bowl, bottle trap and angle valves below.
            c.Rect(-c.Hw, -0.375 * c.H, c.Hw, -0.3 * c.H, St.Thin, "rim");
            c.Add(new PathPrim(new[]
            {
                PathSeg.Move(P(-0.46, -0.3)),
                PathSeg.Cubic(P(-0.44, -0.05), P(-0.25, 0.04), P(-0.08, 0.05)),
                PathSeg.Line(P(0.08, 0.05)),
                PathSeg.Cubic(P(0.25, 0.04), P(0.44, -0.05), P(0.46, -0.3)),
            }, St.Thin), BsTags.Outline);
            c.LineN(0, 0.05, 0, 0.22, St.Thin, "trap");
            c.RoundRect(-0.05 * c.W, 0.22 * c.H, 0.05 * c.W, 0.38 * c.H, 0.03 * c.K, St.Thin, "trap");
            c.LineN(0, 0.38, 0, 0.5, St.Thin, BsTags.Stub);
            foreach (var u in new[] { -0.2, 0.2 }) c.Circle(c.N(u, 0.15), 0.035 * c.K, St.Fine, "valve");
            // Tap on the rim.
            c.Rect(-0.03 * c.W, -0.5 * c.H, 0.03 * c.W, -0.375 * c.H, St.Thin, "tap");
            c.LineN(0, -0.47, 0.12, -0.47, St.Thin, "spout");
        }

        private static void BasinSchematic(Canvas c, SketchObject o, RenderContext ctx)
        {
            c.LineN(-0.45, -0.25, 0.45, -0.25, St.Medium, "rim");
            c.Arc(c.N(0, -0.25), 0.4 * c.W, 0.5 * c.H, 0, 180, St.Medium, BsTags.Outline);
            c.LineN(-0.2, -0.5, -0.2, -0.25, St.Thin, BsTags.Stub);
            c.LineN(0.2, -0.5, 0.2, -0.25, St.Thin, BsTags.Stub);
        }

        // ------------------------------------------------------------------ shower

        private static void ShowerPlan(Canvas c, SketchObject o, RenderContext ctx)
        {
            c.Rect(-c.Hw, -c.Hh, c.Hw, c.Hh, St.Thin, BsTags.Outline);
            c.Rect(-c.Hw + 0.05 * c.W, -c.Hh + 0.05 * c.H, c.Hw - 0.05 * c.W, c.Hh - 0.05 * c.H, St.Fine, "tray");
            c.LineN(-0.45, -0.45, 0.45, 0.45, St.Fine, "slope");
            c.LineN(-0.45, 0.45, 0.45, -0.45, St.Fine, "slope");
            c.Circle(Vec2.Zero, 0.06 * c.K, St.ThinWhite, "drain");
            // Mixer on the wall.
            c.Circle(c.N(0.3, -0.42), 0.04 * c.K, St.Thin, "mixer");
        }

        private static void ShowerSection(Canvas c, SketchObject o, RenderContext ctx)
        {
            // Tray on the floor, mixer at 1100 mm, rail and head at the top, spray (box 0…2200 mm).
            c.RectN(-0.5, 0.46, 0.5, 0.5, St.Thin, "tray");
            c.RoundRect(0.22 * c.W, -0.03 * c.H, 0.38 * c.W, 0.03 * c.H, 0.02 * c.K, St.ThinWhite, "mixer");
            c.LineN(0.3, -0.03, 0.3, -0.42, St.Thin, "rail");
            c.LineN(0.3, -0.42, 0.15, -0.42, St.Thin, "arm");
            c.PolyN(St.ThinWhite, true, "head", 0.06, -0.44, 0.2, -0.44, 0.18, -0.41, 0.08, -0.41);
            var spray = St.Fine with { Dash = DashStyle.Dash };
            foreach (var du in new[] { -0.1, 0.0, 0.1 }) c.LineN(0.13 + du * 0.3, -0.4, 0.13 + du * 1.6, -0.1, spray, "spray");
            c.LineN(0, 0.46, 0, 0.5, St.Thin, BsTags.Stub);
        }

        private static void ShowerSchematic(Canvas c, SketchObject o, RenderContext ctx)
        {
            c.LineN(-0.45, 0.35, 0.45, 0.35, St.Medium, "tray");
            c.LineN(0, 0.35, 0, 0.5, St.Medium, BsTags.Stub);
            c.LineN(0.3, 0.02, 0.3, -0.35, St.Medium, "rail");
            // Head (fan) at the top.
            c.PolyN(St.Medium, true, "head", 0.3, -0.35, 0.05, -0.5, 0.05, -0.2);
            c.Circle(c.N(0.3, 0.02), 0.06 * c.K, St.MediumWhite, "mixer");
            c.Poly(new[] { c.N(0.3, 0.02) + new Vec2(0.06 * c.K, 0), c.N(0.5, -0.05) }, St.Thin, false, BsTags.Stub);
            c.Poly(new[] { c.N(0.3, 0.02) + new Vec2(0.05 * c.K, 0.03 * c.K), c.N(0.5, 0.1) }, St.Thin, false, BsTags.Stub);
        }

        // ------------------------------------------------------------------ bathtub

        private static void TubPlan(Canvas c, SketchObject o, RenderContext ctx)
        {
            c.Rect(-c.Hw, -c.Hh, c.Hw, c.Hh, St.Thin, BsTags.Outline);
            c.RoundRect(-c.Hw + 0.04 * c.W, -c.Hh + 0.1 * c.H, c.Hw - 0.04 * c.W, c.Hh - 0.1 * c.H, 0.3 * c.H, St.Fine, "tub");
            c.Circle(c.N(-0.4, 0), 0.05 * c.K, St.Thin, "drain");
            c.Circle(c.N(-0.4, -0.4), 0.04 * c.K, St.Thin, "mixer");
        }

        private static void TubSection(Canvas c, SketchObject o, RenderContext ctx)
        {
            c.Rect(-c.Hw, -c.Hh, c.Hw, -0.4 * c.H, St.Thin, "rim");
            c.Rect(-c.Hw + 0.01 * c.W, -0.4 * c.H, c.Hw - 0.01 * c.W, 0.5 * c.H, St.Thin, BsTags.Outline);
            // Inner tub behind the apron (hidden) and the waste below the outlet.
            c.Add(new PathPrim(new[]
            {
                PathSeg.Move(c.N(-0.46, -0.4)),
                PathSeg.Line(c.N(-0.42, 0.3)),
                PathSeg.Cubic(c.N(-0.41, 0.36), c.N(-0.38, 0.38), c.N(-0.34, 0.38)),
                PathSeg.Line(c.N(0.34, 0.38)),
                PathSeg.Cubic(c.N(0.38, 0.38), c.N(0.41, 0.36), c.N(0.42, 0.3)),
                PathSeg.Line(c.N(0.46, -0.4)),
            }, St.Hidden), "tub");
            c.LineN(-0.4, 0.38, -0.4, 0.5, St.Hidden, BsTags.Stub);
        }

        private static void TubSchematic(Canvas c, SketchObject o, RenderContext ctx)
        {
            c.LineN(-0.5, -0.3, 0.5, -0.3, St.Medium, "rim");
            c.Add(new PathPrim(new[]
            {
                PathSeg.Move(c.N(-0.46, -0.3)),
                PathSeg.Line(c.N(-0.42, 0.1)),
                PathSeg.Cubic(c.N(-0.41, 0.25), c.N(-0.38, 0.3), c.N(-0.3, 0.3)),
                PathSeg.Line(c.N(0.3, 0.3)),
                PathSeg.Cubic(c.N(0.38, 0.3), c.N(0.41, 0.25), c.N(0.42, 0.1)),
                PathSeg.Line(c.N(0.46, -0.3)),
            }, St.Medium), BsTags.Outline);
            c.LineN(-0.4, 0.3, -0.4, 0.5, St.Medium, BsTags.Stub);
            c.LineN(-0.45, -0.5, -0.45, -0.3, St.Thin, BsTags.Stub);
            c.LineN(-0.35, -0.5, -0.35, -0.3, St.Thin, BsTags.Stub);
        }

        // ------------------------------------------------------------------ water heater

        private static string DhwLabel(Lang lang) => lang == Lang.De ? "WW" : "DHW";

        private static IEnumerable<PortAt> HeaterPorts(Canvas c, SketchObject o, string view)
        {
            var coil = o.Flag("coil", true);
            var list = view == BsViews.Plan
                ? new List<(double, double, string)> { (0.5, -0.25, "hot"), (0.5, 0.0, "circulation"), (0.5, 0.25, "cold") }
                : new List<(double, double, string)> { (0.5, -0.38, "hot"), (0.5, -0.1, "circulation"), (0.5, 0.42, "cold") };
            if (coil)
            {
                if (view == BsViews.Plan) { list.Add((-0.5, -0.15, "flow")); list.Add((-0.5, 0.15, "return")); }
                else { list.Add((-0.5, 0.08, "flow")); list.Add((-0.5, 0.36, "return")); }
            }
            return list.Select(p => new PortAt(c.N(p.Item1, p.Item2), p.Item3));
        }

        private static void HeaterPlan(Canvas c, SketchObject o, RenderContext ctx)
        {
            var r = 0.42 * c.K;
            c.Circle(Vec2.Zero, r, St.Thin, BsTags.Outline);
            c.Circle(Vec2.Zero, r * 0.82, St.Fine, "insulation");
            c.Text(Vec2.Zero, DhwLabel(ctx.Lang), 1.8, r * 1.1, r * 0.45);
            foreach (var v in new[] { -0.25, 0.0, 0.25 }) c.LineN(0.5, v, Math.Sqrt(Math.Max(0, r * r - v * v * c.H * c.H)) / c.W, v, St.Thin, BsTags.Stub);
            if (o.Flag("coil", true))
                foreach (var v in new[] { -0.15, 0.15 }) c.LineN(-0.5, v, -Math.Sqrt(Math.Max(0, r * r - v * v * c.H * c.H)) / c.W, v, St.Thin, BsTags.Stub);
        }

        private static void Vessel(Canvas c, SketchObject o, Style s, bool section)
        {
            double w = 0.38 * c.W;
            if (section)
            {
                // Domed cylinder with its insulation jacket.
                c.RoundRect(-w, -c.Hh, w, c.Hh, w * 0.8, s, BsTags.Outline);
                c.RoundRect(-w * 0.85, -c.Hh + 0.15 * w, w * 0.85, c.Hh - 0.15 * w, w * 0.7, St.Fine, "insulation");
            }
            else c.Rect(-w, -c.Hh, w, c.Hh, s, BsTags.Outline);
            var stubs = new[] { -0.38, -0.1, 0.42 };
            foreach (var v in stubs) c.LineN(0.5, v, w / c.W, v, s == St.Medium ? St.Medium : St.Thin, BsTags.Stub);
            if (!o.Flag("coil", true)) return;
            double y0 = 0.08 * c.H, y1 = 0.36 * c.H;
            c.Line(new Vec2(-c.Hw, y0), new Vec2(-0.25 * c.W, y0), St.Thin, BsTags.Stub);
            c.Line(new Vec2(-c.Hw, y1), new Vec2(-0.25 * c.W, y1), St.Thin, BsTags.Stub);
            // Coil as a zigzag from flow (top) to return (bottom).
            var pts = new List<Vec2> { new(-0.25 * c.W, y0) };
            const int n = 6;
            for (var i = 1; i < n; i++) pts.Add(new Vec2(i % 2 == 1 ? 0.22 * c.W : -0.22 * c.W, y0 + i * (y1 - y0) / n));
            pts.Add(new Vec2(-0.25 * c.W, y1));
            c.Poly(pts, Media.PipeStyle(Media.Pipe("flow"), 0.18) with { Dash = DashStyle.Solid }, false, "coil");
        }

        private static void HeaterSection(Canvas c, SketchObject o, RenderContext ctx)
        {
            Vessel(c, o, St.Thin, true);
            c.Text(c.N(0, -0.22), DhwLabel(ctx.Lang), 2.5, 0.6 * c.W, 0.08 * c.H);
        }

        private static void HeaterSchematic(Canvas c, SketchObject o, RenderContext ctx)
        {
            Vessel(c, o, St.Medium, false);
            c.Text(c.N(0, -0.25), DhwLabel(ctx.Lang), 2.5, 0.6 * c.W, 0.12 * c.H);
        }

        // ------------------------------------------------------------------ floor drain

        private static void DrainPlan(Canvas c, SketchObject o, RenderContext ctx)
        {
            c.Rect(-c.Hw, -c.Hh, c.Hw, c.Hh, St.Thin, BsTags.Outline);
            c.Circle(Vec2.Zero, 0.36 * c.K, St.Fine, "grate");
            foreach (var u in new[] { -0.2, 0.0, 0.2 }) c.LineN(u, -0.3, u, 0.3, St.Fine, "grate");
        }

        private static void DrainSection(Canvas c, SketchObject o, RenderContext ctx)
        {
            c.LineN(-0.5, -0.5, 0.5, -0.5, St.Medium, "grate");
            c.PolyN(St.Thin, false, BsTags.Outline, -0.45, -0.45, -0.15, 0.15, 0.15, 0.15, 0.45, -0.45);
            c.LineN(-0.08, 0.15, -0.08, 0.5, St.Thin, BsTags.Stub);
            c.LineN(0.08, 0.15, 0.08, 0.5, St.Thin, BsTags.Stub);
        }

        private static void DrainSchematic(Canvas c, SketchObject o, RenderContext ctx)
        {
            var r = 0.3 * c.K;
            var centre = new Vec2(0, -c.Hh + r + 0.05 * c.H);
            c.Circle(centre, r, St.Medium, BsTags.Outline);
            c.Line(centre - new Vec2(r, 0), centre + new Vec2(r, 0), St.Medium, "grate");
            c.Line(new Vec2(0, centre.Y + r), new Vec2(0, c.Hh), St.Medium, BsTags.Stub);
        }

        // ------------------------------------------------------------------ riser

        private static Medium RiserMedium(SketchObject o) => Media.Pipe(o.Str("medium", "cold"));

        private static void RiserPlan(Canvas c, SketchObject o, RenderContext ctx)
        {
            var m = RiserMedium(o);
            var r = 0.28 * c.K;
            var s = new Style { Stroke = m.Color, StrokeWidth = Math.Min(0.35, m.Width), Role = LineRole.Visible, Fill = Fill.Solid("#ffffff") };
            var dir = o.Str("dir", "up");
            var d = new Vec2(1, -1).Normalized();
            var outer = d * (0.48 * c.K * Math.Sqrt(2));
            var near = d * r;
            var line = new Style { Stroke = m.Color, StrokeWidth = 0.18, Role = LineRole.Visible };
            if (dir == "down") c.Arrow(outer, near, line, 0.22 * c.K, "direction");
            else if (dir == "up") c.Arrow(near, outer, line, 0.22 * c.K, "direction");
            else c.Line(-outer, outer, line, "direction");
            c.Circle(Vec2.Zero, r, s, BsTags.Outline);
            if (dir == "through") c.Line(-near, near, line, "direction");
        }

        private static void RiserSection(Canvas c, SketchObject o, RenderContext ctx)
        {
            var m = RiserMedium(o);
            c.Line(new Vec2(0, c.Hh), new Vec2(0, -c.Hh), Media.PipeStyle(m), BsTags.Pipe);
        }

        private static void RiserSchematic(Canvas c, SketchObject o, RenderContext ctx)
        {
            var m = RiserMedium(o);
            var code = m.Code.Get(ctx.Lang);
            var ts = Bs.FitText(code, 2.2, c.W * 0.95, Math.Min(2.2, 0.2 * c.H));
            c.Line(new Vec2(0, c.Hh), new Vec2(0, -c.Hh + ts * 1.3), Media.PipeStyle(m, Math.Max(0.5, m.Width)) with { Dash = DashStyle.Solid }, BsTags.Pipe);
            c.Add(new TextPrim(new Vec2(0, -c.Hh + ts * 0.6), code, new TextStyle { Size = ts, Color = m.Color }) { Tag = BsTags.Label });
        }
    }
}
