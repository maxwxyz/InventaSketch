using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Architecture
{
    /// <summary>
    /// Stairs in plan: tread lines and the walking line (start circle at the bottom step, arrow pointing up).
    /// Straight, quarter-turn (L) with landing, half-turn (U) with landing and spiral.
    /// </summary>
    internal static class Stairs
    {
        private static readonly string[] Kw = { "stair", "stairs", "treppe", "steps", "stufen", "walking line", "lauflinie" };

        public static IEnumerable<ObjectType> Types()
        {
            yield return Stair("arch.stair.straight", "Straight stair", "Treppe (gerade)", new Size2(36, 10), 14, Straight, "straight", "gerade", "einläufig");
            yield return Stair("arch.stair.l", "Quarter-turn stair (L)", "Podesttreppe (L-Form)", new Size2(30, 30), 16, LShape, "l-shaped", "quarter", "viertelgewendelt", "podest", "landing");
            yield return Stair("arch.stair.u", "Half-turn stair (U)", "Podesttreppe (U-Form)", new Size2(34, 22), 18, UShape, "u-shaped", "half-turn", "zweiläufig", "gegenläufig", "podest", "landing");
            yield return Stair("arch.stair.spiral", "Spiral stair", "Wendeltreppe", new Size2(20, 20), 12, Spiral, "spiral", "wendeltreppe", "helical", "spindel");
        }

        private static StairType Stair(string id, string en, string de, Size2 size, int steps, DrawFn draw, params string[] kw)
            => new(id, new I18nText(en, de), size, steps, draw, Kw.Concat(kw).Concat(StairType.ValueKeywords).ToArray())
            {
                Params = new[]
                {
                    Pm.Int("steps", "Number of steps (risers)", "Anzahl Stufen (Steigungen)", steps, 2, 60),
                    Pm.Bool("down", "Walking line reversed", "Lauflinie umgekehrt", false),
                    new ParamDef(StairType.StoreyParam, new I18nText("Storey height (default 2.75 m)", "Geschosshöhe (Vorgabe 2,75 m)"), ParamKind.Number)
                        { Min = 0.2, Max = 20, Unit = "m", Step = 0.05, Advanced = true },
                    new ParamDef(StairType.RiserParam, new I18nText("Riser height", "Steigungshöhe"), ParamKind.Text) { Unit = "cm", Advanced = true },
                    Pm.Scale,
                    ValueLabels.ShowValues,
                },
                Extra = o => Actions(o, steps),
                ExtraInDocument = (o, ctx) => new[] { StairType.FitAction(o, ctx, id, steps) },
            };

        private static IEnumerable<ContextAction> Actions(SketchObject o, int def)
        {
            yield return Arch.Toggle(o, "arch.stair.reverse", new I18nText("Reverse walking direction", "Laufrichtung umkehren"), IconIds.Reverse, "down", false, "direction");
            var n = o.Int("steps", def);
            yield return new ContextAction("arch.steps.inc", new I18nText("More steps (" + (n + 1) + ")", "Mehr Stufen (" + (n + 1) + ")"), IconIds.CountMore, x => x.WithParam("steps", Math.Min(60, x.Int("steps", def) + 1))) { Group = "steps" };
            yield return new ContextAction("arch.steps.dec", new I18nText("Fewer steps (" + Math.Max(2, n - 1) + ")", "Weniger Stufen (" + Math.Max(2, n - 1) + ")"), IconIds.CountFewer, x => x.WithParam("steps", Math.Max(2, x.Int("steps", def) - 1))) { Group = "steps" };
        }

        internal static int Steps(SketchObject o, int def) => Math.Max(2, Math.Min(60, o.Int("steps", def)));

        /// <summary>
        /// Depth (page mm) of one drawn tread along the walking line for <paramref name="n"/> steps in a box of
        /// <paramref name="size"/>, exactly as the plan draws it: straight W/n; L and U the tread of the first flight; spiral the
        /// arc per step on the walking line (60 % of the radius).
        /// </summary>
        internal static double TreadOnPage(string id, Size2 size, int n)
        {
            var c = new Canvas(size);
            n = Math.Max(2, Math.Min(60, n));
            switch (id)
            {
                case "arch.stair.l":
                    {
                        var b = c.K / 3;
                        double l1 = c.W - b, l2 = c.H - b;
                        var n1 = Math.Max(1, Math.Min(n - 1, (int)Math.Round(n * l1 / Math.Max(1e-9, l1 + l2))));
                        return Math.Max(0, l1) / n1;
                    }
                case "arch.stair.u":
                    {
                        var d = Math.Min(c.Hh, c.W * 0.35);
                        return (c.W - d) / Math.Max(1, (n + 1) / 2);
                    }
                case "arch.stair.spiral":
                    return 2 * Math.PI * 0.6 * (c.Hw + c.Hh) / 2 / n;
                default:
                    return c.W / n;
            }
        }

        /// <summary>Walking line with a start circle and an arrow at the end (reversed for "down").</summary>
        private static void WalkLine(Canvas c, IList<Vec2> pts, bool down)
        {
            var list = down ? pts.Reverse().ToList() : pts.ToList();
            if (list.Count < 2) return;
            var start = list[0];
            // The start circle must stay inside the box even for very short or narrow stairs.
            var r = Math.Max(0.01, Math.Min(Math.Min(0.4, c.K * 0.06), Math.Min(c.Hw - Math.Abs(start.X), c.Hh - Math.Abs(start.Y)) * 0.95));
            var d = (list[1] - list[0]).Normalized();
            c.Ellipse(start, r, r, St.Thin, "walk-start");
            list[0] = start + d * r;
            c.Add(Prim.Polyline(list, St.Thin with { ArrowEnd = ArrowHead.Triangle, ArrowSize = ArrowSize.Small }), ArchTags.WalkLine);
        }

        private static void Straight(Canvas c, SketchObject o, RenderContext ctx)
        {
            var n = Steps(o, 14);
            c.Rect(-c.Hw, -c.Hh, c.Hw, c.Hh, St.Thin, ArchTags.Outline);
            var step = c.W / n;
            for (var i = 1; i < n; i++) c.Line(new Vec2(-c.Hw + i * step, -c.Hh), new Vec2(-c.Hw + i * step, c.Hh), St.Thin, ArchTags.Tread);
            WalkLine(c, new[] { new Vec2(-c.Hw + step / 2, 0), new Vec2(c.Hw - step * 0.2, 0) }, o.Flag("down", false));
        }

        private static void LShape(Canvas c, SketchObject o, RenderContext ctx)
        {
            var n = Steps(o, 16);
            var b = c.K / 3;
            double l1 = c.W - b, l2 = c.H - b;
            var n1 = Math.Max(1, Math.Min(n - 1, (int)Math.Round(n * l1 / (l1 + l2))));
            var n2 = Math.Max(1, n - n1);
            c.Poly(new[]
            {
                new Vec2(-c.Hw, c.Hh - b), new Vec2(c.Hw - b, c.Hh - b), new Vec2(c.Hw - b, -c.Hh), new Vec2(c.Hw, -c.Hh),
                new Vec2(c.Hw, c.Hh), new Vec2(-c.Hw, c.Hh),
            }, St.Thin, true, ArchTags.Outline);
            for (var i = 1; i <= n1; i++) { var x = -c.Hw + i * l1 / n1; c.Line(new Vec2(x, c.Hh - b), new Vec2(x, c.Hh), St.Thin, ArchTags.Tread); }
            for (var i = 0; i < n2; i++) { var y = c.Hh - b - i * l2 / n2; c.Line(new Vec2(c.Hw - b, y), new Vec2(c.Hw, y), St.Thin, ArchTags.Tread); }
            WalkLine(c, new[]
            {
                new Vec2(-c.Hw + l1 / n1 / 2, c.Hh - b / 2), new Vec2(c.Hw - b / 2, c.Hh - b / 2), new Vec2(c.Hw - b / 2, -c.Hh + l2 / n2 * 0.2),
            }, o.Flag("down", false));
        }

        private static void UShape(Canvas c, SketchObject o, RenderContext ctx)
        {
            var n = Steps(o, 18);
            var d = Math.Min(c.Hh, c.W * 0.35);
            var g = Math.Min(c.H * 0.06, 1.0);
            var l = c.W - d;
            var n1 = Math.Max(1, (n + 1) / 2);
            var n2 = Math.Max(1, n - n1);
            c.Rect(-c.Hw, -c.Hh, c.Hw, c.Hh, St.Thin, ArchTags.Outline);
            c.Rect(-c.Hw, -g / 2, c.Hw - d, g / 2, St.Thin, "eye");
            for (var i = 1; i <= n1; i++) { var x = -c.Hw + i * l / n1; c.Line(new Vec2(x, g / 2), new Vec2(x, c.Hh), St.Thin, ArchTags.Tread); }
            for (var i = 0; i < n2; i++) { var x = c.Hw - d - i * l / n2; c.Line(new Vec2(x, -c.Hh), new Vec2(x, -g / 2), St.Thin, ArchTags.Tread); }
            var yl = (g / 2 + c.Hh) / 2;
            WalkLine(c, new[]
            {
                new Vec2(-c.Hw + l / n1 / 2, yl), new Vec2(c.Hw - d / 2, yl), new Vec2(c.Hw - d / 2, -yl), new Vec2(-c.Hw + l / n2 * 0.2, -yl),
            }, o.Flag("down", false));
        }

        private static void Spiral(Canvas c, SketchObject o, RenderContext ctx)
        {
            var n = Steps(o, 12);
            const double inner = 0.16;
            c.Ellipse(Vec2.Zero, c.Hw, c.Hh, St.Thin, ArchTags.Outline);
            c.Ellipse(Vec2.Zero, c.Hw * inner, c.Hh * inner, St.Thin, "newel");
            for (var i = 0; i < n; i++)
            {
                var a = Geo.DegToRad(-90 + i * 360.0 / n);
                double cs = Math.Cos(a), sn = Math.Sin(a);
                c.Line(new Vec2(c.Hw * inner * cs, c.Hh * inner * sn), new Vec2(c.Hw * cs, c.Hh * sn), St.Thin, ArchTags.Tread);
            }
            // Walking line on 60 % of the radius over 300 degrees, clockwise (up) or anticlockwise (down).
            var step = 360.0 / n;
            var pts = Geo.ArcPoints(Vec2.Zero, c.Hw * 0.6, c.Hh * 0.6, Geo.DegToRad(-90 + step / 2), Geo.DegToRad(300), 48);
            WalkLine(c, pts, o.Flag("down", false));
        }
    }
}
