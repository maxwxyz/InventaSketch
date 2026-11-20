using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Architecture
{
    /// <summary>
    /// Doors and windows. All are box objects whose local x axis runs along the wall and whose centre (Hub anchor)
    /// sits on the wall axis. The wall band inside the opening is hidden by an opaque white band (so the opening
    /// must lie above the wall in z-order); the band thickness is taken from the wall under the centre, or from the
    /// optional wall thickness parameter when the opening is not placed on a wall.
    /// </summary>
    internal static class Openings
    {
        /// <summary>Glass tint used in elevations (becomes white in monochrome output).</summary>
        private const string GlassTint = "#e3eef7";

        private static readonly string[] DoorKw = { "door", "tür", "tuer", "opening", "öffnung" };
        private static readonly string[] WindowKw = { "window", "fenster", "glazing", "verglasung" };

        public static IEnumerable<ObjectType> Types()
        {
            yield return Door("arch.door.single", "Door (single swing)", "Tür (einflügelig)", new Size2(9, 20.4), DoorSingle, ElevDoorSingle, 2, "single", "einflügelig", "drehflügeltür", "swing");
            yield return Door("arch.door.double", "Double door", "Tür (zweiflügelig)", new Size2(16, 18.4), DoorDouble, ElevDoorDouble, 1, "double", "zweiflügelig", "doppeltür", "swing");
            yield return Door("arch.door.sliding", "Sliding door", "Schiebetür", new Size2(10, 6), DoorSliding, ElevDoorSliding, 0, "sliding", "schiebetür", "slide");
            yield return Door("arch.door.opening", "Opening (no door)", "Öffnung / Durchgang", new Size2(10, 4), DoorOpening, ElevOpening, 0, "opening", "durchgang", "passage", "doorway", "wandöffnung");
            yield return Window("arch.window.single", "Window (single casement)", "Fenster (einflügelig)", new Size2(10, 5), WindowSingle, ElevWindowSingle, 0, "single", "einflügelig", "casement", "flügel");
            yield return Window("arch.window.double", "Window (double casement)", "Fenster (zweiflügelig)", new Size2(16, 5), WindowDouble, ElevWindowDouble, 0, "double", "zweiflügelig", "casement", "flügel");
            yield return Window("arch.window.french", "French window", "Fenstertür (zweiflügelig)", new Size2(16, 18.4), WindowFrench, ElevWindowFrench, 1, "french", "fenstertür", "balcony door", "balkontür", "terrace", "terrasse");
        }

        private static IReadOnlyList<ParamDef> CommonParams(bool window) => new[]
        {
            Pm.Bool("hingeRight", window ? "Hinges on the right" : "Hinge on the right", window ? "Anschlag rechts" : "Anschlag rechts", false),
            window ? Pm.Bool("flip", "Swap inside/outside", "Innen/außen tauschen", false) : Pm.Bool("swingOut", "Swing to the other side", "Aufschlag zur anderen Seite", false),
            Pm.Num("wallThickness", "Wall thickness (if not on a wall)", "Wanddicke (ohne Wand)", 240, 10, 2000, "mm", 5, advanced: true),
            Pm.Scale,
        };

        private static BoxType Door(string id, string en, string de, Size2 size, DrawFn plan, DrawFn elev, int leafFactor, params string[] kw)
            => new(id, new I18nText(en, de), ArchCategories.Openings, size, plan, DoorKw.Concat(kw).ToArray())
            {
                Elevation = elev,
                Hub = true,
                Params = CommonParams(false),
                Extra = o => DoorActions(o, id, leafFactor, false),
            };

        private static BoxType Window(string id, string en, string de, Size2 size, DrawFn plan, DrawFn elev, int leafFactor, params string[] kw)
            => new(id, new I18nText(en, de), ArchCategories.Openings, size, plan, WindowKw.Concat(kw).ToArray())
            {
                Elevation = elev,
                Hub = true,
                Params = CommonParams(true),
                Extra = o => DoorActions(o, id, leafFactor, true),
            };

        /// <summary>Real width in mm of an opening (box width times plan scale).</summary>
        public static double RealWidth(SketchObject o) => o.Size.W * Arch.ScaleOf(o);

        /// <summary>Hinge/swing flips and width steps of 125 mm (leaf depth grows with hinged leaves).</summary>
        private static IEnumerable<ContextAction> DoorActions(SketchObject o, string id, int leafFactor, bool window)
        {
            var sliding = id == "arch.door.sliding";
            if (id != "arch.door.opening" && id != "arch.door.double")
                yield return Arch.Toggle(o, "arch.hinge", sliding ? new I18nText("Flip sliding direction", "Schieberichtung wechseln") : new I18nText("Flip hinge side", "Anschlagseite wechseln"),
                    IconIds.MirrorH, "hingeRight", false, "flip");
            if (window)
                yield return Arch.Toggle(o, "arch.side", new I18nText("Swap inside/outside", "Innen/außen tauschen"), IconIds.MirrorV, "flip", false, "flip");
            else if (id != "arch.door.opening")
                yield return Arch.Toggle(o, "arch.swing", new I18nText("Flip swing direction", "Aufschlagrichtung wechseln"), IconIds.MirrorV, "swingOut", false, "flip");
            var scale = Arch.ScaleOf(o);
            var w = RealWidth(o);
            foreach (var dir in new[] { 1, -1 })
            {
                var next = Math.Max(250, Math.Round((w + dir * 125) / 5) * 5);
                var label = next.ToString("0", CultureInfo.InvariantCulture) + " mm";
                yield return new ContextAction(dir > 0 ? "arch.width.inc" : "arch.width.dec",
                    dir > 0 ? new I18nText("Wider (" + label + ")", "Breiter (" + label + ")") : new I18nText("Narrower (" + label + ")", "Schmaler (" + label + ")"),
                    dir > 0 ? IconIds.Increase : IconIds.Decrease, x =>
                    {
                        var dw = next / Arch.ScaleOf(x) - x.Size.W;
                        return x with { Size = new Size2(x.Size.W + dw, Math.Max(0.5, x.Size.H + leafFactor * dw)) };
                    }) { Group = "width" };
            }
        }

        /// <summary>Thickness (page mm) of the wall under the opening centre, else the fallback parameter.</summary>
        public static double BandThickness(SketchObject o, RenderContext ctx, double maxH)
        {
            var c = new Vec2(o.Transform.X, o.Transform.Y);
            double best = double.MaxValue, t = Math.Max(0.05, o.Num("wallThickness", 240)) / Arch.ScaleOf(o);
            if (!o.IsPointGeometry)
                foreach (var w in WallLine.OnPageOf(o, ctx.Doc))
                    for (var k = 0; k < w.SegCount; k++)
                    {
                        var d = Arch.DistanceToSegment(c, w.A(k), w.B(k));
                        if (d <= Math.Max(w.Half, WallBuilder.JoinTolerance) && d < best) { best = d; t = 2 * w.Half; }
                    }
            return Math.Min(t, maxH);
        }

        // ------------------------------------------------------------------ plan views

        /// <summary>White band over the wall plus the two jamb lines; returns the half band thickness.</summary>
        private static double Band(Canvas c, SketchObject o, RenderContext ctx, bool jambs = true)
        {
            var tb = BandThickness(o, ctx, c.H);
            var h = tb / 2;
            c.Rect(-c.Hw, -h, c.Hw, h, St.Mask, ArchTags.Mask);
            if (jambs)
            {
                c.Line(new Vec2(-c.Hw, -h), new Vec2(-c.Hw, h), St.Wall, ArchTags.Jamb);
                c.Line(new Vec2(c.Hw, -h), new Vec2(c.Hw, h), St.Wall, ArchTags.Jamb);
            }
            return h;
        }

        /// <summary>Swing side: +1 = +y (default), -1 = -y.</summary>
        private static int Side(SketchObject o, string key) => o.Flag(key, false) ? -1 : 1;

        /// <summary>Hinged leaf with its quarter swing arc (elliptical when the box is stretched).</summary>
        private static void Leaf(Canvas c, double hingeX, double freeX, double faceY, int s, double len, Style leaf)
        {
            if (len <= 1e-6) return;
            var hinge = new Vec2(hingeX, faceY);
            c.Line(hinge, new Vec2(hingeX, faceY + s * len), leaf, ArchTags.Leaf);
            var closed = freeX > hingeX ? 0.0 : 180.0;
            var start = s * 90.0;
            var sweep = closed - start;
            if (sweep > 180) sweep -= 360;
            if (sweep <= -180) sweep += 360;
            c.Arc(hinge, Math.Abs(freeX - hingeX), len, start, sweep, St.Fine, ArchTags.Swing);
        }

        private static void DoorSingle(Canvas c, SketchObject o, RenderContext ctx)
        {
            var h = Band(c, o, ctx);
            var s = Side(o, "swingOut");
            var right = o.Flag("hingeRight", false);
            Leaf(c, right ? c.Hw : -c.Hw, right ? -c.Hw : c.Hw, s * h, s, c.Hh - h, St.Medium);
        }

        private static void DoorDouble(Canvas c, SketchObject o, RenderContext ctx)
        {
            var h = Band(c, o, ctx);
            var s = Side(o, "swingOut");
            Leaf(c, -c.Hw, 0, s * h, s, c.Hh - h, St.Medium);
            Leaf(c, c.Hw, 0, s * h, s, c.Hh - h, St.Medium);
        }

        private static void DoorSliding(Canvas c, SketchObject o, RenderContext ctx)
        {
            var h = Band(c, o, ctx);
            var s = Side(o, "swingOut");
            var dir = o.Flag("hingeRight", false) ? -1 : 1;
            var lt = Math.Min(h * 0.6, 0.6);
            // Fixed panel and sliding panel on two tracks inside the band.
            c.Rect(-c.Hw, -s * lt * 0.1, 0.1 * c.W, -s * lt * 1.1, St.Medium, ArchTags.Leaf);
            c.Rect(-0.1 * c.W, s * lt * 0.1, c.Hw, s * lt * 1.1, St.Medium, ArchTags.Leaf);
            var room = c.Hh - h;
            if (room > 0.2)
            {
                var y = s * (h + room * 0.55);
                c.Add(Prim.Line(new Vec2(-dir * 0.3 * c.W, y), new Vec2(dir * 0.3 * c.W, y), St.Fine with { ArrowEnd = ArrowHead.Open, ArrowSize = ArrowSize.Small }), ArchTags.Swing);
            }
        }

        private static void DoorOpening(Canvas c, SketchObject o, RenderContext ctx)
        {
            var h = Band(c, o, ctx);
            // Lintel above the opening (hidden edges).
            c.Line(new Vec2(-c.Hw, -h), new Vec2(c.Hw, -h), St.Hidden, "lintel");
            c.Line(new Vec2(-c.Hw, h), new Vec2(c.Hw, h), St.Hidden, "lintel");
        }

        /// <summary>Window in the wall band: reveal lines, glass and outside sill. Returns the half band thickness.</summary>
        private static double WindowBand(Canvas c, SketchObject o, RenderContext ctx, bool sill)
        {
            var h = Band(c, o, ctx);
            var outside = -Side(o, "flip");
            c.Line(new Vec2(-c.Hw, -h), new Vec2(c.Hw, -h), St.Thin, "reveal");
            c.Line(new Vec2(-c.Hw, h), new Vec2(c.Hw, h), St.Thin, "reveal");
            var g = Math.Min(h * 0.25, 0.35);
            c.Line(new Vec2(-c.Hw, -g), new Vec2(c.Hw, -g), St.Fine, ArchTags.Glass);
            c.Line(new Vec2(-c.Hw, g), new Vec2(c.Hw, g), St.Fine, ArchTags.Glass);
            var depth = Math.Min(Math.Max(0.4, h * 0.5), c.Hh - h);
            if (sill && depth > 0.05)
                c.Rect(-c.Hw, outside * h, c.Hw, outside * (h + depth), St.Thin, ArchTags.Sill);
            return h;
        }

        private static void WindowSingle(Canvas c, SketchObject o, RenderContext ctx) => WindowBand(c, o, ctx, true);

        private static void WindowDouble(Canvas c, SketchObject o, RenderContext ctx)
        {
            var h = WindowBand(c, o, ctx, true);
            c.Line(new Vec2(0, -h), new Vec2(0, h), St.Medium, "mullion");
        }

        private static void WindowFrench(Canvas c, SketchObject o, RenderContext ctx)
        {
            var h = WindowBand(c, o, ctx, false);
            c.Line(new Vec2(0, -h), new Vec2(0, h), St.Medium, "mullion");
            var s = Side(o, "flip");
            Leaf(c, -c.Hw, 0, s * h, s, c.Hh - h, St.Thin);
            Leaf(c, c.Hw, 0, s * h, s, c.Hh - h, St.Thin);
        }

        // ------------------------------------------------------------------ elevations (box = opening in the facade)

        /// <summary>Opening triangle (dashed) of a hinged leaf: the apex points to the hinge side.</summary>
        private static void Triangle(Canvas c, double x0, double y0, double x1, double y1, bool hingeRight)
        {
            double hx = hingeRight ? x1 : x0, fx = hingeRight ? x0 : x1;
            c.Poly(new[] { new Vec2(fx, y0), new Vec2(hx, (y0 + y1) / 2), new Vec2(fx, y1) }, St.Hidden, false, "opening-triangle");
        }

        private static double Frame(Canvas c) => Math.Min(0.06 * c.K, 0.8);

        private static void ElevDoorSingle(Canvas c, SketchObject o, RenderContext ctx)
        {
            var f = Frame(c);
            c.Rect(-c.Hw, -c.Hh, c.Hw, c.Hh, St.Medium, ArchTags.Outline);
            c.Rect(-c.Hw + f, -c.Hh + f, c.Hw - f, c.Hh, St.Thin, ArchTags.Leaf);
            var right = o.Flag("hingeRight", false);
            Triangle(c, -c.Hw + f, -c.Hh + f, c.Hw - f, c.Hh, right);
            var hx = right ? -c.Hw + f * 2.5 : c.Hw - f * 2.5;
            c.Line(new Vec2(hx, 0.05 * c.H), new Vec2(hx + (right ? 1 : -1) * Math.Min(0.15 * c.W, 1.2), 0.05 * c.H), St.Medium, "handle");
        }

        private static void ElevDoorDouble(Canvas c, SketchObject o, RenderContext ctx)
        {
            var f = Frame(c);
            c.Rect(-c.Hw, -c.Hh, c.Hw, c.Hh, St.Medium, ArchTags.Outline);
            c.Rect(-c.Hw + f, -c.Hh + f, 0, c.Hh, St.Thin, ArchTags.Leaf);
            c.Rect(0, -c.Hh + f, c.Hw - f, c.Hh, St.Thin, ArchTags.Leaf);
            Triangle(c, -c.Hw + f, -c.Hh + f, 0, c.Hh, false);
            Triangle(c, 0, -c.Hh + f, c.Hw - f, c.Hh, true);
        }

        private static void ElevDoorSliding(Canvas c, SketchObject o, RenderContext ctx)
        {
            var f = Frame(c);
            c.Rect(-c.Hw, -c.Hh, c.Hw, c.Hh, St.Medium, ArchTags.Outline);
            var glass = St.Thin with { Fill = Fill.Solid(GlassTint) };
            c.Rect(-c.Hw + f, -c.Hh + f, 0.05 * c.W, c.Hh, glass, ArchTags.Leaf);
            c.Rect(-0.05 * c.W, -c.Hh + f, c.Hw - f, c.Hh, glass, ArchTags.Leaf);
            var dir = o.Flag("hingeRight", false) ? -1 : 1;
            c.Add(Prim.Line(new Vec2(-dir * 0.25 * c.W, 0), new Vec2(dir * 0.25 * c.W, 0), St.Thin with { ArrowEnd = ArrowHead.Open, ArrowSize = ArrowSize.Small }), ArchTags.Swing);
        }

        private static void ElevOpening(Canvas c, SketchObject o, RenderContext ctx)
        {
            c.Poly(new[] { new Vec2(-c.Hw, c.Hh), new Vec2(-c.Hw, -c.Hh), new Vec2(c.Hw, -c.Hh), new Vec2(c.Hw, c.Hh) }, St.Medium, false, ArchTags.Outline);
        }

        /// <summary>Window elevation with <paramref name="leaves"/> casements; sill line at the bottom unless it reaches the floor.</summary>
        private static void ElevWindow(Canvas c, SketchObject o, int leaves, bool toFloor)
        {
            var f = Frame(c);
            c.Rect(-c.Hw, -c.Hh, c.Hw, c.Hh, St.Medium, ArchTags.Outline);
            var bottom = toFloor ? c.Hh : c.Hh - f;
            var glass = St.Thin with { Fill = Fill.Solid(GlassTint) };
            var right = o.Flag("hingeRight", false);
            var x0 = -c.Hw + f; var x1 = c.Hw - f;
            for (var i = 0; i < leaves; i++)
            {
                var a = x0 + (x1 - x0) * i / leaves; var b = x0 + (x1 - x0) * (i + 1) / leaves;
                c.Rect(a, -c.Hh + f, b, bottom, glass, ArchTags.Leaf);
                // Single casement: hinge side from the parameter; pairs open to the outer sides.
                var hingeRight = leaves == 1 ? right : i == leaves - 1;
                Triangle(c, a, -c.Hh + f, b, bottom, hingeRight);
            }
            if (!toFloor) c.Line(new Vec2(-c.Hw, c.Hh), new Vec2(c.Hw, c.Hh), St.Wall, ArchTags.Sill);
        }

        private static void ElevWindowSingle(Canvas c, SketchObject o, RenderContext ctx) => ElevWindow(c, o, 1, false);
        private static void ElevWindowDouble(Canvas c, SketchObject o, RenderContext ctx) => ElevWindow(c, o, 2, false);
        private static void ElevWindowFrench(Canvas c, SketchObject o, RenderContext ctx) => ElevWindow(c, o, 2, true);
    }
}
