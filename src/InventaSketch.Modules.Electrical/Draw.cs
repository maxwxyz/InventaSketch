using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace System.Runtime.CompilerServices
{
    /// <summary>Polyfill so records/init accessors compile for netstandard2.0.</summary>
    internal static class IsExternalInit { }
}

namespace InventaSketch.Modules.Electrical
{
    /// <summary>Shared line styles of the electrical module.</summary>
    internal static class S
    {
        /// <summary>Symbol outlines and leads (one uniform weight, as usual in circuit diagrams).</summary>
        public static readonly Style Sym = Style.Visible with { StrokeWidth = 0.35 };
        /// <summary>Emphasised strokes (gate/base bars, battery minus plate).</summary>
        public static readonly Style Bold = Style.Visible with { StrokeWidth = 0.7 };
        /// <summary>Busbars and bus lines.</summary>
        public static readonly Style Bus = Style.Visible with { StrokeWidth = 1.0 };
        public static readonly Style Thin = Style.ThinLine;
        /// <summary>Mechanical linkage (dashed thin line), module housings.</summary>
        public static readonly Style Link = Style.ThinLine with { Dash = DashStyle.Dash };
        public static readonly Style Black = Sym with { Fill = Fill.Solid("#000000") };
        public static readonly Style Wire = Style.Visible with { StrokeWidth = 0.35 };
        public static Style Hatch(double spacing, bool cross = false) => Style.ThinLine with { Fill = Fill.Hatched(new HatchPattern(45, spacing, cross)) };
    }

    /// <summary>
    /// Drawing helper. Symbols are authored in "design millimetres" for a reference box (the view's design size,
    /// centred at 0,0) and stretched to the actual object size, so free scaling simply stretches the drawing.
    /// Small details (dots, arrow heads, text) use the uniform detail scale <see cref="K"/>. <see cref="Mirror"/>
    /// flips the drawing in x (polarity reversal); text is kept upright on the page via <see cref="TextRotation"/>.
    /// </summary>
    internal sealed class Pen
    {
        private readonly List<Primitive> _items = new();

        public Pen(Size2 actual, Size2 design, bool mirror = false, double textRotation = 0)
        {
            W = Math.Max(actual.W, 0.01);
            H = Math.Max(actual.H, 0.01);
            DW = design.W;
            DH = design.H;
            Sx = W / DW;
            Sy = H / DH;
            K = Math.Min(Sx, Sy);
            Mirror = mirror;
            TextRotation = textRotation;
        }

        public double W { get; }
        public double H { get; }
        public double DW { get; }
        public double DH { get; }
        public double Sx { get; }
        public double Sy { get; }
        public double K { get; }
        public bool Mirror { get; }
        public double TextRotation { get; }

        public IReadOnlyList<Primitive> Items => _items;

        /// <summary>Design point to local (actual) point.</summary>
        public Vec2 V(double x, double y) => new(x * Sx * (Mirror ? -1 : 1), y * Sy);
        public Vec2 V(Vec2 p) => V(p.X, p.Y);

        public void Add(Primitive p, string? tag = null) => _items.Add(tag is null ? p : p with { Tag = tag });

        public void Line(double x1, double y1, double x2, double y2, Style? s = null, string? tag = null)
            => Add(Prim.Line(V(x1, y1), V(x2, y2), s ?? S.Sym), tag);

        /// <summary>Open or closed polyline from design coordinate pairs.</summary>
        public void Poly(Style s, bool closed, params double[] xy) => Poly(s, closed, null, xy);

        public void Poly(Style s, bool closed, string? tag, params double[] xy)
        {
            var pts = new List<Vec2>(xy.Length / 2);
            for (var i = 0; i + 1 < xy.Length; i += 2) pts.Add(V(xy[i], xy[i + 1]));
            Add(Prim.Polyline(pts, s, closed), tag);
        }

        public void PolyD(IEnumerable<Vec2> design, Style? s = null, bool closed = false, string? tag = null)
            => Add(Prim.Polyline(design.Select(V).ToList(), s ?? S.Sym, closed), tag);

        /// <summary>Path from design segments.</summary>
        public void Path(IEnumerable<PathSeg> design, Style? s = null, string? tag = null)
            => Add(Prim.Path(design.Select(seg => seg.Op switch
            {
                SegOp.Z => seg,
                SegOp.C => new PathSeg(SegOp.C, V(seg.P), V(seg.C1), V(seg.C2)),
                SegOp.Q => new PathSeg(SegOp.Q, V(seg.P), V(seg.C1)),
                _ => new PathSeg(seg.Op, V(seg.P)),
            }), s ?? S.Sym), tag);

        /// <summary>Axis-aligned rectangle given by two design corners (optional corner radius in design mm).</summary>
        public void Rect(double x0, double y0, double x1, double y1, Style? s = null, string? tag = null, double radius = 0)
        {
            var a = V(x0, y0);
            var b = V(x1, y1);
            Add(Prim.Rect(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Abs(b.X - a.X), Math.Abs(b.Y - a.Y), s ?? S.Sym, radius * K), tag);
        }

        /// <summary>Stretched ellipse in design coordinates.</summary>
        public void Ellipse(double cx, double cy, double rx, double ry, Style? s = null, string? tag = null)
        {
            var c = V(cx, cy);
            Add(Prim.Ellipse(c.X, c.Y, rx * Sx, ry * Sy, s ?? S.Sym), tag);
        }

        /// <summary>Circle that follows the stretch (becomes an ellipse when the box is scaled non-uniformly).</summary>
        public void Circle(double cx, double cy, double r, Style? s = null, string? tag = null) => Ellipse(cx, cy, r, r, s, tag);

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
        public void Text(double x, double y, string text, double size, TextAnchor anchor = TextAnchor.Middle, bool bold = false, string? tag = "text")
        {
            if (Mirror && anchor != TextAnchor.Middle) anchor = anchor == TextAnchor.Start ? TextAnchor.End : TextAnchor.Start;
            var ts = new TextStyle { Size = size * K, Anchor = anchor, Baseline = TextBaseline.Middle, Bold = bold };
            Add(Prim.Text(V(x, y).X, V(x, y).Y, text, ts, TextRotation), tag);
        }

        /// <summary>Filled arrow head with its tip at design (tx, ty), pointing away from design (fx, fy). Sizes in design mm (detail scale).</summary>
        public void Head(double tx, double ty, double fx, double fy, double len = 1.6, double half = 0.65, string? tag = "head")
        {
            var tip = V(tx, ty);
            var dir = (tip - V(fx, fy)).Normalized();
            if (dir.Length < Geo.Eps) return;
            var n = dir.Perp;
            var b = tip - dir * (len * K);
            Add(Prim.Polygon(new[] { tip, b + n * (half * K), b - n * (half * K) }, S.Black), tag);
        }

        /// <summary>Line from (fx, fy) to (tx, ty) with a filled arrow head at the end.</summary>
        public void Arrow(double fx, double fy, double tx, double ty, double len = 1.6, double half = 0.65, Style? s = null, string? tag = null)
        {
            var a = V(fx, fy);
            var tip = V(tx, ty);
            var dir = (tip - a).Normalized();
            Add(Prim.Line(a, tip - dir * (len * K * 0.8), s ?? S.Sym), tag);
            Head(tx, ty, fx, fy, len, half);
        }

        /// <summary>Small cross (x) centred at a design point, half size in design mm (detail scale).</summary>
        public void Cross(double cx, double cy, double a, Style? s = null, string? tag = null)
        {
            var c = V(cx, cy);
            var d = a * K;
            Add(Prim.Line(c + new Vec2(-d, -d), c + new Vec2(d, d), s ?? S.Sym), tag);
            Add(Prim.Line(c + new Vec2(-d, d), c + new Vec2(d, -d), s ?? S.Sym), tag);
        }
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

        /// <summary>Coil humps (semicircles) from x0 to x1 on the line y, bulging to negative y (upwards).</summary>
        public static List<Vec2> HumpsH(double x0, double x1, double y, int n)
        {
            var pts = new List<Vec2>();
            var r = (x1 - x0) / (2 * n);
            for (var i = 0; i < n; i++)
            {
                var arc = Geo.ArcPoints(new Vec2(x0 + r * (2 * i + 1), y), r, r, Math.PI, Math.PI, 12);
                pts.AddRange(i == 0 ? arc : arc.Skip(1));
            }
            return pts;
        }

        /// <summary>Coil humps from y0 to y1 on the line x, bulging to +x (side = 1) or -x (side = -1).</summary>
        public static List<Vec2> HumpsV(double x, double y0, double y1, int n, int side)
        {
            var pts = new List<Vec2>();
            var r = (y1 - y0) / (2 * n);
            for (var i = 0; i < n; i++)
            {
                var c = new Vec2(x, y0 + r * (2 * i + 1));
                // From the top point of each hump to its bottom point via the side.
                var arc = Geo.ArcPoints(c, r, r, -Math.PI / 2, side > 0 ? Math.PI : -Math.PI, 12);
                pts.AddRange(i == 0 ? arc : arc.Skip(1));
            }
            return pts;
        }

        /// <summary>One period of a sine ("~") centred at (cx, cy) with half width w and amplitude a.</summary>
        public static List<Vec2> Sine(double cx, double cy, double w, double a)
        {
            var pts = new List<Vec2>();
            for (var i = 0; i <= 16; i++)
            {
                var t = i / 16.0;
                pts.Add(new Vec2(cx - w + 2 * w * t, cy - a * Math.Sin(2 * Math.PI * t)));
            }
            return pts;
        }

        /// <summary>Point at distance len from p in direction angleDeg (degrees, clockwise on screen).</summary>
        public static Vec2 Polar(Vec2 p, double len, double angleDeg) => p + Vec2.Polar(len, Geo.DegToRad(angleDeg));
    }
}
