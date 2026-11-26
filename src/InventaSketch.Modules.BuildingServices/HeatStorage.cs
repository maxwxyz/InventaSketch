using System;
using System.Collections.Generic;
using InventaSketch.Core;

namespace InventaSketch.Modules.BuildingServices
{
    /// <summary>
    /// Heat storage and generation additions: buffer tank (generator circuit f1/r1 on the left, consumer circuit f2/r2 on the
    /// right, ids like the manifold circuits) and solar thermal collector (flow, return). Drawn to scale at 1:100.
    /// </summary>
    internal static class HeatStorage
    {
        private const string Cat = BsCategories.Heating;

        private static readonly Style FineDash = St.Fine with { Dash = DashStyle.Dash };

        public static IEnumerable<ObjectType> Types()
        {
            yield return new BsBoxType("bs.heat.buffer", new I18nText("Buffer tank", "Pufferspeicher"), Cat,
                new Size2(8, 8), new Size2(8, 18), new Size2(8, 12), BufferPlan, BufferSection, BufferSchematic,
                "buffer tank", "buffer", "thermal store", "storage tank", "accumulator", "pufferspeicher", "puffer", "heizungspuffer",
                "speicher", "schichtenspeicher", "heizung", "tga")
            {
                Mount = Mounting.Floor, WallBacked = false, DefaultHeight = 0,
                Ports = BsBoxType.Uv(
                    new[] { (-0.5, -0.15, "f1"), (-0.5, 0.15, "r1"), (0.5, -0.15, "f2"), (0.5, 0.15, "r2") },
                    new[] { (-0.5, -0.3, "f1"), (-0.5, 0.35, "r1"), (0.5, -0.3, "f2"), (0.5, 0.35, "r2") },
                    new[] { (-0.5, -0.35, "f1"), (-0.5, 0.35, "r1"), (0.5, -0.35, "f2"), (0.5, 0.35, "r2") }),
                CompanionIds = new[] { PipeType.TypeId, "bs.heat.heatpump", "bs.heat.boiler", "bs.heat.solarthermal", "bs.heat.pump", "bs.heat.mixer" },
            };

            // Hydraulic separator (low-loss header): a vertical tube between the generator circuit (f1/r1, left) and the
            // consumer circuit (f2/r2, right); a symbol, not to scale.
            yield return new BsBoxType(BsValues.SeparatorId, new I18nText("Hydraulic separator (low-loss header)", "Hydraulische Weiche"), Cat,
                new Size2(6, 4), new Size2(6, 10), new Size2(6, 8), SeparatorPlan, SeparatorSection, SeparatorSchematic,
                "hydraulic separator", "low-loss header", "low loss header", "hydraulic decoupler", "decoupler", "hydraulische weiche",
                "weiche", "entkopplung", "hydraulische entkopplung", "heizung", "tga")
            {
                Mount = Mounting.Wall, ToScale = false, DefaultHeight = 1000,
                Ports = BsBoxType.Uv(
                    new[] { (-0.5, -0.2, "f1"), (-0.5, 0.2, "r1"), (0.5, -0.2, "f2"), (0.5, 0.2, "r2") },
                    new[] { (-0.5, -0.3, "f1"), (-0.5, 0.3, "r1"), (0.5, -0.3, "f2"), (0.5, 0.3, "r2") }),
                CompanionIds = new[] { PipeType.TypeId, "bs.heat.boiler", "bs.heat.heatpump", "bs.heat.pump", "bs.heat.mixer", "bs.meter.heat" },
            };

            yield return new BsBoxType("bs.heat.solarthermal", new I18nText("Solar thermal collector", "Solarkollektor (Solarthermie)"), Cat,
                new Size2(20, 8), new Size2(20, 9), new Size2(20, 7), SolarPlan, SolarSection, SolarSchematic,
                "solar collector", "solar thermal", "flat plate collector", "solar panel", "solarkollektor", "solarthermie",
                "flachkollektor", "kollektor", "solar", "heizung", "tga")
            {
                Mount = Mounting.Floor, WallBacked = false, DefaultHeight = 0,
                Ports = BsBoxType.Uv(
                    new[] { (-0.5, -0.25, "flow"), (-0.5, 0.25, "return") },
                    new[] { (-0.5, -0.2, "flow"), (-0.5, 0.1, "return") },
                    new[] { (-0.5, -0.25, "flow"), (-0.5, 0.25, "return") }),
                CompanionIds = new[] { PipeType.TypeId, "bs.heat.buffer", "bs.heat.pump", "bs.heat.expansion" },
            };
        }

        // ------------------------------------------------------------------ buffer tank

        private static string BufferLabel(Lang lang) => lang == Lang.De ? "PS" : "BUF";

        private static void BufferPlan(Canvas c, SketchObject o, RenderContext ctx)
        {
            var r = 0.42 * c.K;
            c.Circle(Vec2.Zero, r, St.Thin, BsTags.Outline);
            c.Circle(Vec2.Zero, r * 0.82, St.Fine, "insulation");
            c.Text(Vec2.Zero, BufferLabel(ctx.Lang), 1.8, r * 1.1, r * 0.45);
            foreach (var v in new[] { -0.15, 0.15 })
            {
                var x = Math.Sqrt(Math.Max(0, r * r - v * v * c.H * c.H)) / c.W;
                c.LineN(-0.5, v, -x, v, St.Thin, BsTags.Stub);
                c.LineN(x, v, 0.5, v, St.Thin, BsTags.Stub);
            }
        }

        private static void BufferSection(Canvas c, SketchObject o, RenderContext ctx)
        {
            double top = -c.Hh, bot = 0.44 * c.H, w = 0.4 * c.W;
            c.RoundRect(-w, top, w, bot, Math.Min(w, (bot - top) / 2) * 0.6, St.Thin, BsTags.Outline);
            c.RectN(-0.3, 0.44, -0.22, 0.5, St.Thin, "foot");
            c.RectN(0.22, 0.44, 0.3, 0.5, St.Thin, "foot");
            // Temperature layers (stratification).
            foreach (var v in new[] { 0.0, 0.15, 0.3 }) c.Line(new Vec2(-0.75 * w, v * c.H), new Vec2(0.75 * w, v * c.H), FineDash, "layer");
            c.Text(c.N(0, -0.2), BufferLabel(ctx.Lang), 2.5, 1.2 * w, 0.1 * c.H);
            foreach (var v in new[] { -0.3, 0.35 })
            {
                c.LineN(-0.5, v, -0.4, v, St.Thin, BsTags.Stub);
                c.LineN(0.4, v, 0.5, v, St.Thin, BsTags.Stub);
            }
        }

        private static void BufferSchematic(Canvas c, SketchObject o, RenderContext ctx)
        {
            c.RectN(-0.38, -0.48, 0.38, 0.48, St.Medium, BsTags.Outline);
            foreach (var v in new[] { -0.35, 0.35 })
            {
                c.LineN(-0.5, v, -0.38, v, St.Medium, BsTags.Stub);
                c.LineN(0.38, v, 0.5, v, St.Medium, BsTags.Stub);
            }
            foreach (var v in new[] { 0.05, 0.2 }) c.LineN(-0.3, v, 0.3, v, FineDash, "layer");
            c.Text(c.N(0, -0.15), BufferLabel(ctx.Lang), 2.5, 0.6 * c.W, 0.14 * c.H);
        }

        // ------------------------------------------------------------------ hydraulic separator

        /// <summary>Plan: the tube seen from above with the four stubs.</summary>
        private static void SeparatorPlan(Canvas c, SketchObject o, RenderContext ctx)
        {
            var r = 0.3 * c.K;
            c.Circle(Vec2.Zero, r, St.Thin, BsTags.Outline);
            foreach (var v in new[] { -0.2, 0.2 })
            {
                var x = Math.Sqrt(Math.Max(0, r * r - v * v * c.H * c.H)) / c.W;
                c.LineN(-0.5, v, -x, v, St.Thin, BsTags.Stub);
                c.LineN(x, v, 0.5, v, St.Thin, BsTags.Stub);
            }
        }

        /// <summary>Section: a vertical tube with closed ends, flow stubs at the top and return stubs at the bottom.</summary>
        private static void SeparatorSection(Canvas c, SketchObject o, RenderContext ctx) => SeparatorTube(c, ctx, St.Thin, -0.3, 0.3);

        /// <summary>Schematic: the tube with flow directions on the stubs (into f1 and r2, out of f2 and r1).</summary>
        private static void SeparatorSchematic(Canvas c, SketchObject o, RenderContext ctx)
        {
            SeparatorTube(c, ctx, St.Medium, -0.3, 0.3);
            var head = 0.12 * c.W;
            c.Head(c.N(-0.25, -0.3), new Vec2(1, 0), head, "direction");
            c.Head(c.N(0.45, -0.3), new Vec2(1, 0), head, "direction");
            c.Head(c.N(0.25, 0.3), new Vec2(-1, 0), head, "direction");
            c.Head(c.N(-0.45, 0.3), new Vec2(-1, 0), head, "direction");
        }

        private static void SeparatorTube(Canvas c, RenderContext ctx, Style s, double vFlow, double vReturn)
        {
            const double u = 0.17;
            c.RectN(-u, -0.5, u, 0.5, s, BsTags.Outline);
            foreach (var v in new[] { vFlow, vReturn })
            {
                c.LineN(-0.5, v, -u, v, s, BsTags.Stub);
                c.LineN(u, v, 0.5, v, s, BsTags.Stub);
            }
            // The short circuit between the circuits: a vertical arrow inside the tube.
            c.Arrow(c.N(0, vFlow + 0.08), c.N(0, vReturn - 0.08), St.Fine, 0.18 * c.W, "bypass");
        }

        // ------------------------------------------------------------------ solar thermal collector

        private static void SolarStubs(Canvas c, Style s, params double[] vs)
        {
            foreach (var v in vs) c.LineN(-0.5, v, -0.45, v, s, BsTags.Stub);
        }

        private static void SolarPlan(Canvas c, SketchObject o, RenderContext ctx)
        {
            var x0 = -0.45 * c.W;
            c.Rect(x0, -c.Hh, c.Hw, c.Hh, St.Thin, BsTags.Outline);
            // Absorber tubes between the two headers.
            double y0 = -0.38 * c.H, y1 = 0.38 * c.H;
            c.Line(new Vec2(x0, y0), new Vec2(c.Hw, y0), St.Fine, "header");
            c.Line(new Vec2(x0, y1), new Vec2(c.Hw, y1), St.Fine, "header");
            var n = Math.Max(4, (int)Math.Round((c.Hw - x0) / 1.2));
            for (var i = 1; i < n; i++)
            {
                var x = x0 + i * (c.Hw - x0) / n;
                c.Line(new Vec2(x, y0), new Vec2(x, y1), St.Fine, "absorber");
            }
            SolarStubs(c, St.Thin, -0.25, 0.25);
        }

        private static void SolarSection(Canvas c, SketchObject o, RenderContext ctx)
        {
            var x0 = -0.45 * c.W;
            var bot = 0.25 * c.H;
            // Collector face (frame, glazing) on its mounting frame.
            c.Rect(x0, -c.Hh, c.Hw, bot, St.Thin, BsTags.Outline);
            c.Rect(x0 + 0.02 * c.W, -c.Hh + 0.08 * c.H, c.Hw - 0.02 * c.W, bot - 0.08 * c.H, St.Fine, "glazing");
            foreach (var u in new[] { -0.35, 0.4 }) c.LineN(u, bot / c.H, u, 0.5, St.Thin, "stand");
            c.LineN(-0.35, 0.5, 0.4, 0.5, St.Fine, "stand");
            SolarStubs(c, St.Thin, -0.2, 0.1);
        }

        private static void SolarSchematic(Canvas c, SketchObject o, RenderContext ctx)
        {
            var x0 = -0.45 * c.W;
            c.Rect(x0, -c.Hh, c.Hw, c.Hh, St.Medium, BsTags.Outline);
            var teeth = Math.Max(3, (int)Math.Round((c.Hw - x0) / 2.0));
            c.Zigzag(new Vec2(x0 + 0.04 * c.W, 0), new Vec2(c.Hw - 0.04 * c.W, 0), teeth, 0.3 * c.H, St.Thin, "absorber");
            SolarStubs(c, St.Medium, -0.25, 0.25);
        }
    }
}
