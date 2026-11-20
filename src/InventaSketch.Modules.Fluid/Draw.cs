using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace System.Runtime.CompilerServices
{
    /// <summary>Polyfill so records/init accessors compile for netstandard2.0.</summary>
    internal static class IsExternalInit { }
}

namespace InventaSketch.Modules.Fluid
{
    /// <summary>Shared line styles of the fluid power module (black only, so monochrome output is unchanged).</summary>
    internal static class S
    {
        /// <summary>Symbol outlines and working lines (one uniform weight, as usual in circuit diagrams).</summary>
        public static readonly Style Sym = Style.Visible with { StrokeWidth = 0.35 };
        /// <summary>Emphasised strokes (piston, seat bars).</summary>
        public static readonly Style Bold = Style.Visible with { StrokeWidth = 0.7 };
        /// <summary>Thin details (springs, hatching inside symbols, adjustment arrows).</summary>
        public static readonly Style Thin = Style.ThinLine with { StrokeWidth = 0.25 };
        /// <summary>Pilot (control) line: long dashes.</summary>
        public static readonly Style Pilot = Style.ThinLine with { StrokeWidth = 0.25, Dash = DashStyle.LongDash };
        /// <summary>Drain/leakage line: short dashes.</summary>
        public static readonly Style Drain = Style.ThinLine with { StrokeWidth = 0.25, Dash = DashStyle.Dash };
        /// <summary>Enclosure of an assembly (dash-dot boundary).</summary>
        public static readonly Style Enclosure = Style.ThinLine with { StrokeWidth = 0.25, Dash = DashStyle.DashDot };
        /// <summary>Filled black (hydraulic triangles, arrow heads, dots).</summary>
        public static readonly Style Black = Sym with { Fill = Fill.Solid("#000000") };
        /// <summary>Hollow outline (pneumatic triangles).</summary>
        public static readonly Style Hollow = Sym with { Fill = Fill.None };
        /// <summary>Working line between components.</summary>
        public static readonly Style Line = Style.Visible with { StrokeWidth = 0.35 };
    }

    /// <summary>
    /// Drawing helper. Symbols are authored in "design millimetres" for a reference box (the design size, centred at 0,0)
    /// and stretched to the actual object size, so free scaling simply stretches the drawing. Small details (arrow heads,
    /// triangles, dots, text) use the uniform detail scale <see cref="K"/>; text is kept upright on the page via
    /// <see cref="TextRotation"/>.
    /// </summary>
    internal sealed class Pen
    {
        private readonly List<Primitive> _items = new();

        public Pen(Size2 actual, Size2 design, double textRotation = 0)
        {
            W = Math.Max(actual.W, 0.01);
            H = Math.Max(actual.H, 0.01);
            DW = Math.Max(design.W, 0.01);
            DH = Math.Max(design.H, 0.01);
            Sx = W / DW;
            Sy = H / DH;
            K = Math.Min(Sx, Sy);
            TextRotation = textRotation;
        }

        public double W { get; }
        public double H { get; }
        public double DW { get; }
        public double DH { get; }
        public double Sx { get; }
        public double Sy { get; }
        public double K { get; }
        public double TextRotation { get; }

        public IReadOnlyList<Primitive> Items => _items;

        /// <summary>Design point to local (actual) point.</summary>
        public Vec2 V(double x, double y) => new(x * Sx, y * Sy);
        public Vec2 V(Vec2 p) => V(p.X, p.Y);

        public void Add(Primitive p, string? tag = null) => _items.Add(tag is null ? p : p with { Tag = tag });

        public void Line(double x1, double y1, double x2, double y2, Style? s = null, string? tag = null)
            => Add(Prim.Line(V(x1, y1), V(x2, y2), s ?? S.Sym), tag);

        /// <summary>Open or closed polyline from design coordinate pairs.</summary>
        public void Poly(Style s, bool closed, string? tag, params double[] xy)
        {
            var pts = new List<Vec2>(xy.Length / 2);
            for (var i = 0; i + 1 < xy.Length; i += 2) pts.Add(V(xy[i], xy[i + 1]));
            Add(Prim.Polyline(pts, s, closed), tag);
        }

        public void PolyD(IEnumerable<Vec2> design, Style? s = null, bool closed = false, string? tag = null)
            => Add(Prim.Polyline(design.Select(V).ToList(), s ?? S.Sym, closed), tag);

        /// <summary>Axis-aligned rectangle given by two design corners (optional corner radius in design mm).</summary>
        public void Rect(double x0, double y0, double x1, double y1, Style? s = null, string? tag = null, double radius = 0)
        {
            var a = V(x0, y0);
            var b = V(x1, y1);
            var w = Math.Abs(b.X - a.X);
            var h = Math.Abs(b.Y - a.Y);
            Add(Prim.Rect(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), w, h, s ?? S.Sym, Math.Min(radius * K, Math.Min(w, h) / 2)), tag);
        }

        /// <summary>Circle that follows the stretch (becomes an ellipse when the box is scaled non-uniformly).</summary>
        public void Circle(double cx, double cy, double r, Style? s = null, string? tag = null)
        {
            var c = V(cx, cy);
            Add(Prim.Ellipse(c.X, c.Y, r * Sx, r * Sy, s ?? S.Sym), tag);
        }

        /// <summary>True circle at a design centre with a radius in design mm scaled by the detail scale.</summary>
        public void Round(double cx, double cy, double r, Style? s = null, string? tag = null)
        {
            var c = V(cx, cy);
            Add(Prim.Circle(c.X, c.Y, r * K, s ?? S.Sym), tag);
        }

        /// <summary>Filled connection dot.</summary>
        public void Dot(double cx, double cy, double r = 0.6, string? tag = "dot") => Round(cx, cy, r, S.Black, tag);

        /// <summary>Elliptical arc in design coordinates (degrees, positive sweep = clockwise on screen).</summary>
        public void Arc(double cx, double cy, double rx, double ry, double startDeg, double sweepDeg, Style? s = null, string? tag = null)
            => PolyD(Geo.ArcPoints(new Vec2(cx, cy), rx, ry, Geo.DegToRad(startDeg), Geo.DegToRad(sweepDeg)), s, false, tag);

        /// <summary>Text at a design point; size in design mm (detail scale), kept upright on the page.</summary>
        public void Text(double x, double y, string text, double size, TextAnchor anchor = TextAnchor.Middle, string? tag = "text")
        {
            var ts = new TextStyle { Size = size * K, Anchor = anchor, Baseline = TextBaseline.Middle };
            var p = V(x, y);
            Add(Prim.Text(p.X, p.Y, text, ts, TextRotation), tag);
        }

        /// <summary>Filled arrow head with its tip at design (tx, ty), pointing away from design (fx, fy). Sizes in design mm (detail scale).</summary>
        public void Head(double tx, double ty, double fx, double fy, double len = 1.6, double half = 0.6, string? tag = "head")
        {
            var tip = V(tx, ty);
            var dir = (tip - V(fx, fy)).Normalized();
            if (dir.Length < Geo.Eps) return;
            var n = dir.Perp;
            var b = tip - dir * (len * K);
            Add(Prim.Polygon(new[] { tip, b + n * (half * K), b - n * (half * K) }, S.Black), tag);
        }

        /// <summary>Line from (fx, fy) to (tx, ty) with a filled arrow head at the end (flow path inside a valve box).</summary>
        public void Arrow(double fx, double fy, double tx, double ty, Style? s = null, string? tag = "flow", double len = 1.6, double half = 0.6)
        {
            var a = V(fx, fy);
            var tip = V(tx, ty);
            var dir = (tip - a).Normalized();
            Add(Prim.Line(a, tip - dir * (len * K * 0.8), s ?? S.Sym), tag);
            Head(tx, ty, fx, fy, len, half);
        }

        /// <summary>
        /// Triangle with its tip at design (tx, ty), pointing away from (fx, fy): filled for hydraulic, hollow for pneumatic
        /// energy (pumps, motors, sources, pilot actuation). Length and half width in design mm (detail scale).
        /// </summary>
        public void Triangle(double tx, double ty, double fx, double fy, bool hollow, double len = 2.2, double half = 1.3, string? tag = "triangle")
        {
            var tip = V(tx, ty);
            var dir = (tip - V(fx, fy)).Normalized();
            if (dir.Length < Geo.Eps) return;
            var n = dir.Perp;
            var b = tip - dir * (len * K);
            Add(Prim.Polygon(new[] { tip, b + n * (half * K), b - n * (half * K) }, hollow ? S.Hollow : S.Black), tag);
        }

        /// <summary>Adjustability arrow: a thin diagonal line through a symbol with a filled head at the upper right.</summary>
        public void Adjust(double x0, double y0, double x1, double y1, string? tag = "adjust")
            => Arrow(x0, y0, x1, y1, S.Thin, tag, 1.4, 0.55);

        /// <summary>Spring: zigzag from (x0, y) to (x1, y) with n peaks of amplitude a (design mm).</summary>
        public void SpringH(double x0, double x1, double y, double a, int n = 3, string? tag = "spring")
            => PolyD(Frag.Zigzag(x0, x1, y, a, n), S.Thin, false, tag);

        /// <summary>Vertical spring: zigzag from (x, y0) to (x, y1).</summary>
        public void SpringV(double x, double y0, double y1, double a, int n = 3, string? tag = "spring")
            => PolyD(Frag.Zigzag(y0, y1, x, a, n).Select(p => new Vec2(p.Y, p.X)), S.Thin, false, tag);
    }

    /// <summary>Reusable symbol fragments (design coordinates).</summary>
    internal static class Frag
    {
        /// <summary>Zigzag between x0 and x1 with n peaks of amplitude a around y.</summary>
        public static List<Vec2> Zigzag(double x0, double x1, double y, double a, int n)
        {
            var pts = new List<Vec2> { new(x0, y) };
            var step = (x1 - x0) / (2 * n);
            for (var i = 0; i < 2 * n; i++) pts.Add(new Vec2(x0 + step * (i + 0.5), y + (i % 2 == 0 ? -a : a)));
            pts.Add(new Vec2(x1, y));
            return pts;
        }

        /// <summary>Diamond (square standing on a corner) centred at (cx, cy) with half diagonals rx, ry.</summary>
        public static Vec2[] Diamond(double cx, double cy, double rx, double ry)
            => new[] { new Vec2(cx - rx, cy), new Vec2(cx, cy - ry), new Vec2(cx + rx, cy), new Vec2(cx, cy + ry) };
    }
}
