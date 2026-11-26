using System;
using System.Collections.Generic;
using InventaSketch.Core;

namespace InventaSketch.Modules.BuildingServices
{
    /// <summary>
    /// Inline pipe fittings and meters of heating and drinking water installations: ball valve, check valve, safety valve,
    /// strainer, heat meter and water meter. They sit in a pipe run like the circulator pump and the thermostatic valve
    /// (ports <c>in</c>/<c>out</c>, the safety valve blows off into a <c>waste</c> port); valves and the strainer take a
    /// heating or a drinking water pipe by their optional <c>service</c>. Symbols are drawn independently following common
    /// drawing practice.
    /// </summary>
    internal static class Fittings
    {
        /// <summary>Pipe system of valves and strainers (<see cref="BsBoxType.Service"/>).</summary>
        internal static readonly ParamDef ServiceParam = Pm.Choice("service", "Pipe system", "Leitungssystem", "heating",
            ("heating", "Heating", "Heizung"), ("water", "Drinking water", "Trinkwasser"));

        private static readonly Style ThinBlack = St.Thin with { Fill = Fill.Solid("#000000") };
        private static readonly Style MediumBlack = St.Medium with { Fill = Fill.Solid("#000000") };
        private static readonly Style FineDash = St.Fine with { Dash = DashStyle.Dash };

        private static readonly string[] Pipe = { PipeType.TypeId };

        private static IEnumerable<ContextAction> ServiceChoice(SketchObject o)
            => Bs.Choice(o, "bs.fitting.service", ServiceParam, new I18nText("Pipe system", "Leitungssystem"));

        public static IEnumerable<ObjectType> Types()
        {
            yield return new BsBoxType("bs.valve.ball", new I18nText("Ball valve (heating)", "Kugelhahn (Heizung)"), BsCategories.Heating,
                new Size2(3, 3), new Size2(3, 3), new Size2(3, 3), BallPlan, BallSection, BallSchematic,
                "ball valve", "shut-off valve", "isolating valve", "stop valve", "kugelhahn", "absperrhahn", "absperrventil",
                "absperrarmatur", "heizung", "trinkwasser", "tga")
            {
                Mount = Mounting.Inline, ToScale = false, WallBacked = false, Uniform = true, DefaultHeight = 1000,
                Service = "heating", Params = new[] { ServiceParam }, Extra = ServiceChoice,
                Ports = BsBoxType.Same((-0.5, 0, "in"), (0.5, 0, "out")),
                CompanionIds = new[] { PipeType.TypeId, "bs.heat.pump", "bs.strainer" },
            };

            yield return new BsBoxType("bs.valve.check", new I18nText("Check valve (heating)", "Rückschlagventil (Heizung)"), BsCategories.Heating,
                new Size2(3, 3), new Size2(3, 3), new Size2(3, 3), CheckPlan, CheckSection, CheckSchematic,
                "check valve", "non-return valve", "backflow preventer", "rückschlagventil", "rückflussverhinderer",
                "rückschlagklappe", "heizung", "trinkwasser", "tga")
            {
                Mount = Mounting.Inline, ToScale = false, WallBacked = false, Uniform = true, DefaultHeight = 1000,
                Service = "heating", Params = new[] { ServiceParam }, Extra = ServiceChoice,
                Ports = BsBoxType.Same((-0.5, 0, "in"), (0.5, 0, "out")),
                CompanionIds = new[] { PipeType.TypeId, "bs.heat.pump", "bs.valve.ball" },
            };

            yield return new BsBoxType("bs.valve.safety", new I18nText("Safety valve (heating)", "Sicherheitsventil (Heizung)"), BsCategories.Heating,
                new Size2(3, 3), new Size2(3, 3), new Size2(3, 3), SafetyPlan, SafetySection, SafetySchematic,
                "safety valve", "relief valve", "pressure relief valve", "sicherheitsventil", "überdruckventil", "sv",
                "heizung", "trinkwasser", "tga")
            {
                Mount = Mounting.Inline, ToScale = false, WallBacked = false, Uniform = true, DefaultHeight = 1500,
                Service = "heating", Params = new[] { ServiceParam }, Extra = ServiceChoice,
                // Inlet from below (the system), blow-off to the side into the waste pipe (tundish / drain).
                Ports = BsBoxType.Same((0, 0.5, "in"), (0.5, 0, "waste")),
                CompanionIds = new[] { PipeType.TypeId, "bs.heat.boiler", "bs.heat.expansion" },
            };

            yield return new BsBoxType("bs.strainer", new I18nText("Strainer (heating)", "Schmutzfänger (Heizung)"), BsCategories.Heating,
                new Size2(3, 3), new Size2(3, 3), new Size2(3, 3), StrainerPlan, StrainerSection, StrainerSchematic,
                "strainer", "y-strainer", "dirt trap", "filter", "schmutzfänger", "filter", "siebfilter", "heizung", "trinkwasser", "tga")
            {
                Mount = Mounting.Inline, ToScale = false, WallBacked = false, Uniform = true, DefaultHeight = 1000,
                Service = "heating", Params = new[] { ServiceParam }, Extra = ServiceChoice,
                Ports = BsBoxType.Same((-0.5, 0, "in"), (0.5, 0, "out")),
                CompanionIds = new[] { PipeType.TypeId, "bs.valve.ball", "bs.heat.pump" },
            };

            yield return new BsBoxType("bs.meter.heat", new I18nText("Heat meter", "Wärmemengenzähler"), BsCategories.Heating,
                new Size2(4, 3), new Size2(4, 3), new Size2(4, 3), HeatMeterPlan, HeatMeterSection, HeatMeterSchematic,
                "heat meter", "energy meter", "btu meter", "wärmemengenzähler", "wärmezähler", "wmz", "heizung", "tga")
            {
                Mount = Mounting.Inline, ToScale = false, WallBacked = false, Uniform = true, DefaultHeight = 1000,
                Ports = BsBoxType.Same((-0.5, 0, "in"), (0.5, 0, "out")),
                CompanionIds = new[] { PipeType.TypeId, "bs.heat.pump", "bs.valve.ball" },
            };

            yield return new BsBoxType("bs.meter.water", new I18nText("Water meter", "Wasserzähler"), BsCategories.Plumbing,
                new Size2(3, 3), new Size2(3, 3), new Size2(3, 3), WaterMeterPlan, WaterMeterSection, WaterMeterSchematic,
                "water meter", "flow meter", "wasserzähler", "wasseruhr", "hauswasserzähler", "trinkwasser", "sanitär", "tga")
            {
                Mount = Mounting.Inline, ToScale = false, WallBacked = false, Uniform = true, DefaultHeight = 1000,
                Service = "water",
                Ports = BsBoxType.Same((-0.5, 0, "in"), (0.5, 0, "out")),
                CompanionIds = new[] { PipeType.TypeId, "bs.valve.ball", "bs.valve.check" },
            };
        }

        /// <summary>Straight connection stubs from the box edges to the body (half length <paramref name="s"/>).</summary>
        private static void Stubs(Canvas c, double s, Style st)
        {
            c.LineN(-0.5, 0, -Math.Min(s, c.Hw) / c.W, 0, st, BsTags.Stub);
            c.LineN(Math.Min(s, c.Hw) / c.W, 0, 0.5, 0, st, BsTags.Stub);
        }

        // ------------------------------------------------------------------ ball valve

        private static void BallPlan(Canvas c, SketchObject o, RenderContext ctx)
        {
            var s = 0.3 * c.K;
            Stubs(c, s, St.Thin);
            c.BowTie(Vec2.Zero, s, 0.6 * s, St.Thin);
            c.Circle(Vec2.Zero, 0.11 * c.K, St.Black, "ball");
        }

        private static void BallSection(Canvas c, SketchObject o, RenderContext ctx)
        {
            double bw = 0.2 * c.K, bh = 0.13 * c.K;
            c.LineN(-0.5, 0, 0.5, 0, St.Thin, BsTags.Stub);
            c.Rect(-bw, -bh, bw, bh, St.ThinWhite, BsTags.Outline);
            c.Circle(Vec2.Zero, 0.08 * c.K, St.Fine, "ball");
            // Stem and lever handle along the pipe (open position).
            c.Line(new Vec2(0, -bh), new Vec2(0, -0.3 * c.K), St.Thin, "stem");
            c.RoundRect(-0.06 * c.K, -0.38 * c.K, 0.46 * c.K, -0.3 * c.K, 0.03 * c.K, St.ThinWhite, "lever");
        }

        private static void BallSchematic(Canvas c, SketchObject o, RenderContext ctx)
        {
            var s = 0.3 * c.K;
            Stubs(c, s, St.Medium);
            c.Line(Vec2.Zero, new Vec2(0, -0.34 * c.K), St.Thin, "stem");
            c.Line(new Vec2(-0.16 * c.K, -0.34 * c.K), new Vec2(0.16 * c.K, -0.34 * c.K), St.Medium, "lever");
            c.BowTie(Vec2.Zero, s, 0.6 * s, St.Medium);
            c.Circle(Vec2.Zero, 0.1 * c.K, St.Black, "ball");
        }

        // ------------------------------------------------------------------ check valve

        /// <summary>Check valve body: the inlet triangle open, the outlet triangle filled (flow from left to right only).</summary>
        private static void CheckBody(Canvas c, double s, Style open, Style filled)
        {
            var hs = 0.6 * s;
            c.Poly(new[] { Vec2.Zero, new Vec2(-s, -hs), new Vec2(-s, hs) }, open, true, "valve");
            c.Poly(new[] { Vec2.Zero, new Vec2(s, -hs), new Vec2(s, hs) }, filled, true, "valve");
        }

        private static void CheckPlan(Canvas c, SketchObject o, RenderContext ctx)
        {
            var s = 0.3 * c.K;
            Stubs(c, s, St.Thin);
            CheckBody(c, s, St.ThinWhite, ThinBlack);
        }

        private static void CheckSection(Canvas c, SketchObject o, RenderContext ctx)
        {
            double bw = 0.24 * c.K, bh = 0.14 * c.K;
            c.LineN(-0.5, 0, 0.5, 0, St.Thin, BsTags.Stub);
            c.Rect(-bw, -bh, bw, bh, St.ThinWhite, BsTags.Outline);
            c.Rect(-0.1 * c.K, -0.26 * c.K, 0.1 * c.K, -bh, St.ThinWhite, "bonnet");
            c.Arrow(new Vec2(-0.16 * c.K, 0), new Vec2(0.16 * c.K, 0), St.Fine, 0.1 * c.K, "flow");
        }

        private static void CheckSchematic(Canvas c, SketchObject o, RenderContext ctx)
        {
            var s = 0.3 * c.K;
            Stubs(c, s, St.Medium);
            CheckBody(c, s, St.MediumWhite, MediumBlack);
            c.Arrow(new Vec2(-0.25 * c.K, -0.36 * c.K), new Vec2(0.25 * c.K, -0.36 * c.K), St.Thin, 0.12 * c.K, "flow");
        }

        // ------------------------------------------------------------------ safety valve

        /// <summary>Angle body: inlet triangle from below, outlet triangle to the right, tips at the centre.</summary>
        private static void AngleBody(Canvas c, double s, Style st)
        {
            var hs = 0.6 * s;
            c.Poly(new[] { Vec2.Zero, new Vec2(-hs, s), new Vec2(hs, s) }, st, true, "valve");
            c.Poly(new[] { Vec2.Zero, new Vec2(s, -hs), new Vec2(s, hs) }, st, true, "valve");
        }

        private static void AngleStubs(Canvas c, double s, Style st)
        {
            c.Line(new Vec2(0, Math.Min(s, c.Hh)), new Vec2(0, c.Hh), st, BsTags.Stub);
            c.Line(new Vec2(Math.Min(s, c.Hw), 0), new Vec2(c.Hw, 0), st, BsTags.Stub);
        }

        private static void SafetyPlan(Canvas c, SketchObject o, RenderContext ctx)
        {
            var s = 0.3 * c.K;
            AngleStubs(c, s, St.Thin);
            AngleBody(c, s, St.ThinWhite);
            // Spring bonnet seen from above.
            c.Circle(Vec2.Zero, 0.13 * c.K, St.ThinWhite, "bonnet");
        }

        private static void SafetySection(Canvas c, SketchObject o, RenderContext ctx)
        {
            var b = 0.16 * c.K;
            AngleStubs(c, b, St.Thin);
            c.RoundRect(-b, -b, b, b, 0.04 * c.K, St.ThinWhite, BsTags.Outline);
            // Spring bonnet with the lifting lever on top.
            c.Rect(-0.09 * c.K, -0.42 * c.K, 0.09 * c.K, -b, St.ThinWhite, "bonnet");
            c.Line(new Vec2(-0.09 * c.K, -0.42 * c.K), new Vec2(-0.3 * c.K, -0.48 * c.K), St.Thin, "lever");
        }

        private static void SafetySchematic(Canvas c, SketchObject o, RenderContext ctx)
        {
            var s = 0.3 * c.K;
            AngleStubs(c, s, St.Medium);
            // Spring loading: a zigzag on the stem.
            c.Line(Vec2.Zero, new Vec2(0, -0.14 * c.K), St.Thin, "stem");
            c.Zigzag(new Vec2(0, -0.14 * c.K), new Vec2(0, -0.48 * c.K), 3, 0.1 * c.K, St.Thin, "spring");
            AngleBody(c, s, St.MediumWhite);
        }

        // ------------------------------------------------------------------ strainer

        private static void StrainerPlan(Canvas c, SketchObject o, RenderContext ctx)
        {
            double s = 0.3 * c.K, h = 0.2 * c.K;
            Stubs(c, s, St.Thin);
            c.Rect(-s, -h, s, h, St.ThinWhite, BsTags.Outline);
            c.Line(new Vec2(-s, -h), new Vec2(s, h), FineDash, "screen");
            c.Circle(new Vec2(0.5 * s, -0.5 * h), 0.05 * c.K, St.Fine, "plug");
        }

        private static void StrainerSection(Canvas c, SketchObject o, RenderContext ctx)
        {
            double bw = 0.25 * c.K, bh = 0.15 * c.K;
            c.LineN(-0.5, 0, 0.5, 0, St.Thin, BsTags.Stub);
            // Y body: the straight part and the screen leg slanting down with the plug at its end.
            c.Poly(new[] { new Vec2(-0.05 * c.K, bh), new Vec2(0.15 * c.K, bh), new Vec2(0.38 * c.K, 0.42 * c.K), new Vec2(0.18 * c.K, 0.42 * c.K) }, St.ThinWhite, true, "leg");
            c.Line(new Vec2(0.05 * c.K, bh), new Vec2(0.28 * c.K, 0.42 * c.K), FineDash, "screen");
            c.Rect(-bw, -bh, bw, bh, St.ThinWhite, BsTags.Outline);
        }

        private static void StrainerSchematic(Canvas c, SketchObject o, RenderContext ctx)
        {
            double s = 0.3 * c.K, h = 0.2 * c.K;
            Stubs(c, s, St.Medium);
            c.Rect(-s, -h, s, h, St.MediumWhite, BsTags.Outline);
            c.Line(new Vec2(-s, -h), new Vec2(s, h), FineDash with { StrokeWidth = 0.25 }, "screen");
        }

        // ------------------------------------------------------------------ heat meter

        private static string HeatMeterLabel(Lang lang) => lang == Lang.De ? "WMZ" : "HM";

        private static void HeatMeterPlan(Canvas c, SketchObject o, RenderContext ctx)
        {
            Stubs(c, 0.35 * c.W, St.Thin);
            c.RectN(-0.35, -0.3, 0.35, 0.3, St.ThinWhite, BsTags.Outline);
            c.Text(Vec2.Zero, HeatMeterLabel(ctx.Lang), 1.2, 0.6 * c.W, 0.4 * c.H);
        }

        private static void HeatMeterSection(Canvas c, SketchObject o, RenderContext ctx)
        {
            c.LineN(-0.5, 0, 0.5, 0, St.Thin, BsTags.Stub);
            c.RectN(-0.3, -0.12, 0.3, 0.2, St.ThinWhite, BsTags.Outline);
            // Calculator with its display on top of the flow sensor.
            c.LineN(0, -0.12, 0, -0.2, St.Thin, "stem");
            c.RectN(-0.28, -0.48, 0.28, -0.2, St.ThinWhite, "calculator");
            c.RectN(-0.2, -0.42, 0.2, -0.26, St.Fine, "display");
            c.Text(c.N(0, -0.34), HeatMeterLabel(ctx.Lang), 0.8, 0.36 * c.W, 0.13 * c.H);
        }

        private static void HeatMeterSchematic(Canvas c, SketchObject o, RenderContext ctx)
        {
            Stubs(c, 0.35 * c.W, St.Medium);
            c.RectN(-0.35, -0.3, 0.35, 0.3, St.MediumWhite, BsTags.Outline);
            c.Text(Vec2.Zero, HeatMeterLabel(ctx.Lang), 1.2, 0.6 * c.W, 0.4 * c.H);
            c.Arrow(c.N(-0.25, -0.42), c.N(0.25, -0.42), St.Thin, 0.1 * c.K, "flow");
        }

        // ------------------------------------------------------------------ water meter

        private static string WaterMeterLabel(Lang lang) => lang == Lang.De ? "WZ" : "WM";

        private static void WaterMeterPlan(Canvas c, SketchObject o, RenderContext ctx)
        {
            var r = 0.32 * c.K;
            Stubs(c, r, St.Thin);
            c.Circle(Vec2.Zero, r, St.ThinWhite, BsTags.Outline);
            c.Text(Vec2.Zero, WaterMeterLabel(ctx.Lang), 1.0, 1.3 * r, 0.8 * r);
        }

        private static void WaterMeterSection(Canvas c, SketchObject o, RenderContext ctx)
        {
            double bw = 0.26 * c.K, bh = 0.12 * c.K;
            c.LineN(-0.5, 0, 0.5, 0, St.Thin, BsTags.Stub);
            c.Rect(-bw, -bh, bw, bh, St.ThinWhite, BsTags.Outline);
            // Counter head with the dial seen from the front.
            c.Rect(-0.2 * c.K, -0.46 * c.K, 0.2 * c.K, -bh, St.ThinWhite, "counter");
            c.Line(new Vec2(-0.2 * c.K, -0.36 * c.K), new Vec2(0.2 * c.K, -0.36 * c.K), St.Fine, "dial");
        }

        private static void WaterMeterSchematic(Canvas c, SketchObject o, RenderContext ctx)
        {
            var r = 0.38 * c.K;
            Stubs(c, r, St.Medium);
            c.Circle(Vec2.Zero, r, St.MediumWhite, BsTags.Outline);
            c.Text(new Vec2(0, -0.1 * c.K), WaterMeterLabel(ctx.Lang), 1.0, 1.2 * r, 0.5 * r);
            c.Arrow(new Vec2(-0.2 * c.K, 0.18 * c.K), new Vec2(0.2 * c.K, 0.18 * c.K), St.Thin, 0.1 * c.K, "flow");
        }
    }
}
