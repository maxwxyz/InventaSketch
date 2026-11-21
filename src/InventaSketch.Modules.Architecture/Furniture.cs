using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Architecture
{
    /// <summary>
    /// Furniture, kitchen and sanitary symbols in plan. Sizes are authored for 1:100 (1 mm on the page = 100 mm);
    /// everything is drawn relative to the box, so free scaling simply stretches the symbol.
    /// </summary>
    internal static class Furniture
    {
        public static IEnumerable<ObjectType> Types()
        {
            var f = ArchCategories.Furniture;
            yield return new BoxType("arch.furniture.bed", new I18nText("Bed", "Bett"), f, new Size2(18, 20), Bed, "bed", "bett", "doppelbett", "schlafzimmer", "bedroom")
            {
                Params = new[] { Pm.Int("pillows", "Pillows", "Kissen", 2, 1, 3) },
            };
            yield return new BoxType("arch.furniture.sofa", new I18nText("Sofa", "Sofa"), f, new Size2(22, 9), Sofa, "sofa", "couch", "settee", "wohnzimmer", "living")
            {
                Params = new[] { Pm.Int("seats", "Seats", "Sitzplätze", 3, 1, 6) },
            };
            yield return new BoxType("arch.furniture.table", new I18nText("Table with chairs", "Tisch mit Stühlen"), f, new Size2(16, 18), Table, "table", "tisch", "chairs", "stühle", "dining", "esstisch")
            {
                Params = new[] { Pm.Int("chairs", "Chairs", "Stühle", 4, 0, 12), Pm.Bool("round", "Round table", "Runder Tisch", false) },
                Extra = o => new[] { Arch.Toggle(o, "arch.table.round", new I18nText("Round table", "Runder Tisch"), IconIds.Toggle, "round", false) },
            };
            yield return new BoxType("arch.furniture.desk", new I18nText("Desk with chair", "Schreibtisch mit Stuhl"), f, new Size2(16, 13), Desk, "desk", "schreibtisch", "office", "büro", "workplace", "arbeitsplatz");
            yield return new BoxType("arch.furniture.wardrobe", new I18nText("Wardrobe", "Schrank"), f, new Size2(20, 6), Wardrobe, "wardrobe", "closet", "schrank", "kleiderschrank", "cupboard");
            yield return new BoxType("arch.furniture.counter", new I18nText("Kitchen counter", "Küchenzeile"), f, new Size2(24, 6), Counter, "kitchen", "küche", "counter", "arbeitsplatte", "küchenzeile", "cabinet", "unterschrank");
            yield return new BoxType("arch.furniture.sink", new I18nText("Kitchen sink", "Spüle"), f, new Size2(9, 6), Sink, "sink", "spüle", "spülbecken", "kitchen", "küche")
            {
                Params = new[] { Pm.Int("basins", "Basins", "Becken", 1, 1, 2) },
            };
            yield return new BoxType("arch.furniture.stove", new I18nText("Stove / hob", "Herd / Kochfeld"), f, new Size2(6, 6), Stove, "stove", "hob", "cooker", "herd", "kochfeld", "kitchen", "küche");
            yield return new BoxType("arch.furniture.fridge", new I18nText("Fridge", "Kühlschrank"), f, new Size2(6, 6), Fridge, "fridge", "refrigerator", "kühlschrank", "kitchen", "küche");

            var s = ArchCategories.Sanitary;
            yield return new BoxType("arch.sanitary.wc", new I18nText("WC", "WC"), s, new Size2(4, 7), Wc, "wc", "toilet", "toilette", "klosett", "bathroom", "bad")
            { Ports = new[] { (0.0, -0.5, "waste") } };
            yield return new BoxType("arch.sanitary.washbasin", new I18nText("Washbasin", "Waschbecken"), s, new Size2(6, 4.5), Washbasin, "washbasin", "basin", "lavatory", "waschbecken", "waschtisch", "bathroom", "bad")
            { Ports = new[] { (0.0, -0.5, "waste") } };
            yield return new BoxType("arch.sanitary.shower", new I18nText("Shower", "Dusche"), s, new Size2(9, 9), Shower, "shower", "dusche", "duschwanne", "bathroom", "bad")
            { Ports = new[] { (0.0, 0.0, "waste") } };
            yield return new BoxType("arch.sanitary.bathtub", new I18nText("Bathtub", "Badewanne"), s, new Size2(17, 7.5), Bathtub, "bathtub", "bath", "tub", "badewanne", "wanne", "bathroom", "bad")
            { Ports = new[] { (-0.4, 0.0, "waste") } };
        }

        private static void Bed(Canvas c, SketchObject o, RenderContext ctx)
        {
            c.RoundRect(-c.Hw, -c.Hh, c.Hw, c.Hh, c.K * 0.03, St.Thin, ArchTags.Outline);
            c.Rect(-c.Hw, -c.Hh, c.Hw, -c.Hh + 0.05 * c.H, St.Thin, "headboard");
            var n = Math.Max(1, Math.Min(3, o.Int("pillows", 2)));
            var m = 0.06 * c.W;
            var pw = (c.W - m * (n + 1)) / n;
            for (var i = 0; i < n; i++)
            {
                var x0 = -c.Hw + m + i * (pw + m);
                c.RoundRect(x0, -c.Hh + 0.08 * c.H, x0 + pw, -c.Hh + 0.2 * c.H, Math.Min(pw, 0.12 * c.H) * 0.3, St.Thin, "pillow");
            }
            var y = -c.Hh + 0.28 * c.H;
            c.Line(new Vec2(-c.Hw, y), new Vec2(c.Hw, y), St.Thin, "blanket");
            c.Poly(new[] { new Vec2(c.Hw - 0.3 * c.W, y), new Vec2(c.Hw - 0.12 * c.W, y + 0.14 * c.H), new Vec2(c.Hw, y + 0.1 * c.H) }, St.Fine, false, "blanket");
        }

        private static void Sofa(Canvas c, SketchObject o, RenderContext ctx)
        {
            var arm = 0.1 * c.W;
            var back = 0.28 * c.H;
            var r = c.K * 0.08;
            c.RoundRect(-c.Hw + arm, -c.Hh, c.Hw - arm, -c.Hh + back, r, St.Thin, "back");
            c.RoundRect(-c.Hw, -c.Hh, -c.Hw + arm, c.Hh, r, St.Thin, "arm");
            c.RoundRect(c.Hw - arm, -c.Hh, c.Hw, c.Hh, r, St.Thin, "arm");
            var n = Math.Max(1, Math.Min(6, o.Int("seats", 3)));
            var sw = (c.W - 2 * arm) / n;
            for (var i = 0; i < n; i++)
            {
                var x0 = -c.Hw + arm + i * sw;
                c.RoundRect(x0, -c.Hh + back, x0 + sw, c.Hh, r, St.Thin, "cushion");
            }
        }

        /// <summary>Chair seen from above: seat with a backrest band on the side facing away from <paramref name="facing"/>.</summary>
        private static void Chair(Canvas c, Vec2 centre, double size, Vec2 facing)
        {
            var f = facing.Normalized();
            if (f == Vec2.Zero) f = new Vec2(0, 1);
            var s = f.Perp;
            var h = size / 2;
            Vec2 P(double a, double b) => centre + s * a + f * b;
            c.Poly(new[] { P(-h, -h), P(h, -h), P(h, h), P(-h, h) }, St.ThinWhite, true, "chair");
            c.Poly(new[] { P(-h, -h), P(h, -h), P(h, -h + size * 0.22), P(-h, -h + size * 0.22) }, St.Thin, true, "backrest");
        }

        private static void Table(Canvas c, SketchObject o, RenderContext ctx)
        {
            var n = Math.Max(0, Math.Min(12, o.Int("chairs", 4)));
            var cd = 0.25 * c.K;
            if (o.Flag("round", false))
            {
                var size = cd * 0.85;
                var off = size * 0.72;
                for (var i = 0; i < n; i++)
                {
                    var a = Geo.DegToRad(-90 + i * 360.0 / n);
                    var p = new Vec2((c.Hw - off) * Math.Cos(a), (c.Hh - off) * Math.Sin(a));
                    Chair(c, p, size, -p);
                }
                c.Ellipse(Vec2.Zero, c.Hw - cd * 0.8, c.Hh - cd * 0.8, St.ThinWhite, "table");
                return;
            }
            var top = (n + 1) / 2; var bottom = n / 2;
            for (var i = 0; i < top; i++)
                Chair(c, new Vec2(-c.Hw + (i + 0.5) * c.W / top, -c.Hh + cd / 2), Math.Min(cd, c.W / top * 0.8), new Vec2(0, 1));
            for (var i = 0; i < bottom; i++)
                Chair(c, new Vec2(-c.Hw + (i + 0.5) * c.W / bottom, c.Hh - cd / 2), Math.Min(cd, c.W / bottom * 0.8), new Vec2(0, -1));
            var inset = n > 0 ? cd * 0.7 : 0;
            c.Rect(-c.Hw, -c.Hh + (top > 0 ? inset : 0), c.Hw, c.Hh - (bottom > 0 ? inset : 0), St.ThinWhite, "table");
        }

        private static void Desk(Canvas c, SketchObject o, RenderContext ctx)
        {
            var cw = Math.Min(0.36 * c.W, 0.45 * c.H);
            Chair(c, new Vec2(0, c.Hh - cw / 2), cw, new Vec2(0, -1));
            c.Rect(-c.Hw, -c.Hh, c.Hw, -c.Hh + 0.6 * c.H, St.ThinWhite, "desk");
            c.Rect(-0.2 * c.W, -c.Hh + 0.1 * c.H, 0.2 * c.W, -c.Hh + 0.16 * c.H, St.Fine, "monitor");
        }

        private static void Wardrobe(Canvas c, SketchObject o, RenderContext ctx)
        {
            c.Rect(-c.Hw, -c.Hh, c.Hw, c.Hh, St.Thin, ArchTags.Outline);
            c.Line(new Vec2(-c.Hw + 0.04 * c.W, 0), new Vec2(c.Hw - 0.04 * c.W, 0), St.Center, "rod");
            var n = Math.Max(3, (int)Math.Round(c.W / 1.6));
            for (var i = 1; i < n; i++)
            {
                var x = -c.Hw + i * c.W / n;
                c.Line(new Vec2(x - 0.15 * c.H * 0.3, -0.32 * c.H), new Vec2(x + 0.15 * c.H * 0.3, 0.32 * c.H), St.Fine, "hanger");
            }
        }

        private static void Counter(Canvas c, SketchObject o, RenderContext ctx)
        {
            c.Rect(-c.Hw, -c.Hh, c.Hw, c.Hh, St.Thin, ArchTags.Outline);
            var n = Math.Max(1, (int)Math.Round(c.W / 6));
            for (var i = 1; i < n; i++) { var x = -c.Hw + i * c.W / n; c.Line(new Vec2(x, -c.Hh), new Vec2(x, c.Hh), St.Hidden, "cabinet"); }
            c.Line(new Vec2(-c.Hw, c.Hh - 0.1 * c.H), new Vec2(c.Hw, c.Hh - 0.1 * c.H), St.Fine, "front");
        }

        private static void Sink(Canvas c, SketchObject o, RenderContext ctx)
        {
            c.Rect(-c.Hw, -c.Hh, c.Hw, c.Hh, St.Thin, ArchTags.Outline);
            var n = Math.Max(1, Math.Min(2, o.Int("basins", 1)));
            var m = 0.08 * c.W;
            var bw = (c.W - m * (n + 1)) / n;
            for (var i = 0; i < n; i++)
            {
                var x0 = -c.Hw + m + i * (bw + m);
                c.RoundRect(x0, -c.Hh + 0.25 * c.H, x0 + bw, c.Hh - 0.12 * c.H, Math.Min(bw, c.H) * 0.15, St.Thin, "basin");
                var r = 0.04 * c.K;
                c.Ellipse(new Vec2(x0 + bw / 2, 0.06 * c.H), r, r, St.Fine, "drain");
            }
            var tr = 0.05 * c.K;
            c.Ellipse(new Vec2(0, -c.Hh + 0.13 * c.H), tr, tr, St.Thin, "tap");
        }

        private static void Stove(Canvas c, SketchObject o, RenderContext ctx)
        {
            c.Rect(-c.Hw, -c.Hh, c.Hw, c.Hh, St.Thin, ArchTags.Outline);
            var big = new Vec2(0.17 * c.W, 0.17 * c.H); var small = new Vec2(0.13 * c.W, 0.13 * c.H);
            c.Ellipse(c.N(-0.24, -0.24), big.X, big.Y, St.Thin, "burner");
            c.Ellipse(c.N(0.24, -0.24), small.X, small.Y, St.Thin, "burner");
            c.Ellipse(c.N(-0.24, 0.24), small.X, small.Y, St.Thin, "burner");
            c.Ellipse(c.N(0.24, 0.24), big.X, big.Y, St.Thin, "burner");
        }

        private static void Fridge(Canvas c, SketchObject o, RenderContext ctx)
        {
            c.Rect(-c.Hw, -c.Hh, c.Hw, c.Hh, St.Thin, ArchTags.Outline);
            c.Line(new Vec2(-c.Hw, c.Hh - 0.12 * c.H), new Vec2(c.Hw, c.Hh - 0.12 * c.H), St.Fine, "door");
            c.Text(new Vec2(0, -0.06 * c.H), ctx.Lang == Lang.De ? "KS" : "REF", 2.5, c.W * 0.8, c.H * 0.45, ArchTags.Label);
        }

        private static void Wc(Canvas c, SketchObject o, RenderContext ctx)
        {
            c.RoundRect(-0.48 * c.W, -c.Hh, 0.48 * c.W, -c.Hh + 0.22 * c.H, c.K * 0.1, St.Thin, "cistern");
            var cy = c.Hh - 0.39 * c.H;
            c.Ellipse(new Vec2(0, cy), 0.42 * c.W, 0.39 * c.H, St.Thin, "bowl");
            c.Ellipse(new Vec2(0, cy + 0.03 * c.H), 0.28 * c.W, 0.27 * c.H, St.Fine, "seat");
        }

        private static void Washbasin(Canvas c, SketchObject o, RenderContext ctx)
        {
            c.RoundRect(-c.Hw, -c.Hh, c.Hw, c.Hh, c.K * 0.3, St.Thin, ArchTags.Outline);
            c.Ellipse(new Vec2(0, 0.08 * c.H), 0.36 * c.W, 0.3 * c.H, St.Thin, "basin");
            var r = 0.05 * c.K;
            c.Ellipse(new Vec2(0, -c.Hh + 0.13 * c.H), r, r, St.Thin, "tap");
            c.Ellipse(new Vec2(0, 0.08 * c.H), r * 0.6, r * 0.6, St.Fine, "drain");
        }

        private static void Shower(Canvas c, SketchObject o, RenderContext ctx)
        {
            c.Rect(-c.Hw, -c.Hh, c.Hw, c.Hh, St.Thin, ArchTags.Outline);
            c.Line(new Vec2(-c.Hw, -c.Hh), new Vec2(c.Hw, c.Hh), St.Fine, "diagonal");
            c.Line(new Vec2(c.Hw, -c.Hh), new Vec2(-c.Hw, c.Hh), St.Fine, "diagonal");
            var r = 0.05 * c.K;
            c.Ellipse(Vec2.Zero, r, r, St.ThinWhite, "drain");
        }

        private static void Bathtub(Canvas c, SketchObject o, RenderContext ctx)
        {
            c.Rect(-c.Hw, -c.Hh, c.Hw, c.Hh, St.Thin, ArchTags.Outline);
            var m = 0.1 * c.K;
            c.RoundRect(-c.Hw + m, -c.Hh + m, c.Hw - m, c.Hh - m, (c.K - 2 * m) * 0.45, St.Thin, "tub");
            var r = 0.05 * c.K;
            c.Ellipse(new Vec2(-c.Hw + m + (c.K - 2 * m) * 0.4, 0), r, r, St.Fine, "drain");
        }
    }
}
