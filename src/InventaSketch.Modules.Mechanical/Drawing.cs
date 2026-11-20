using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace System.Runtime.CompilerServices
{
    /// <summary>Polyfill so records/init accessors compile for netstandard2.0.</summary>
    internal static class IsExternalInit { }
}

namespace InventaSketch.Modules.Mechanical
{
    /// <summary>Shared line styles of the mechanical module (ISO 128 line roles).</summary>
    internal static class S
    {
        /// <summary>Visible outlines (continuous wide line).</summary>
        public static readonly Style Vis = Style.Visible;
        /// <summary>Stick/kinematic schematic lines (slightly heavier than outlines).</summary>
        public static readonly Style Stick = Style.Visible with { StrokeWidth = 0.6 };
        /// <summary>Shaft lines in schematics.</summary>
        public static readonly Style Heavy = Style.Visible with { StrokeWidth = 0.7 };
        /// <summary>Thin continuous lines (thread roots, auxiliary lines).</summary>
        public static readonly Style Thin = Style.ThinLine;
        /// <summary>Centre lines, axes and pitch circles (thin dash-dot).</summary>
        public static readonly Style Ctr = Style.CenterLine;
        /// <summary>Hidden edges (dashed).</summary>
        public static readonly Style Hid = Style.Hidden;
        /// <summary>Blackened small sections (weld seams, spring wire sections, masses).</summary>
        public static readonly Style Black = Style.Visible with { Fill = Fill.Solid("#000000") };
        /// <summary>Dotted fill for elastic/adhesive layers.</summary>
        public static readonly Style Dotted = Style.ThinLine with { Fill = Fill.Dotted(0.9) };
    }

    /// <summary>
    /// Drawing helper. Symbols are authored in "design millimetres" for a reference box (the view's design size,
    /// centred at 0,0) and stretched to the actual object size, so free uniform and non-uniform scaling simply
    /// stretches the drawing. Small details (hub marks, rolling elements, pivots) use the uniform detail scale
    /// <see cref="K"/> so they stay crisp.
    /// </summary>
    internal sealed class Pen
    {
        private readonly List<Primitive> _items = new();

        public Pen(Size2 actual, Size2 design)
        {
            W = Math.Max(actual.W, 0.01);
            H = Math.Max(actual.H, 0.01);
            DW = design.W;
            DH = design.H;
            Sx = W / DW;
            Sy = H / DH;
            K = Math.Min(Sx, Sy);
        }

        /// <summary>Actual box size (mm).</summary>
        public double W { get; }
        public double H { get; }
        /// <summary>Design box size (mm).</summary>
        public double DW { get; }
        public double DH { get; }
        public double Sx { get; }
        public double Sy { get; }
        /// <summary>Uniform detail scale (min of both stretch factors).</summary>
        public double K { get; }

        /// <summary>
        /// Scale of schematic symbol details (hub cross, loose/sliding hub marks): they shrink with a part drawn smaller
        /// than its design size but do not grow with it, like line weights; scaling a stick symbol changes what carries
        /// meaning (gear width, pitch diameter), not the symbols.
        /// </summary>
        public double D => Math.Min(1, K);

        public IReadOnlyList<Primitive> Items => _items;

        /// <summary>Design point to local (actual) point.</summary>
        public Vec2 V(double x, double y) => new(x * Sx, y * Sy);
        public Vec2 V(Vec2 p) => new(p.X * Sx, p.Y * Sy);

        /// <summary>Hatch spacing scaled to the object (1.5 to 2.5 mm).</summary>
        public double HatchSpacing => Geo.Clamp(Math.Min(W, H) * 0.08, 1.5, 2.5);

        public Style Hatch(double angle = 45, Style? baseStyle = null)
            => (baseStyle ?? S.Vis) with { Fill = Fill.Hatched(new HatchPattern(angle, HatchSpacing)) };

        public void Add(Primitive p, string? tag = null) => _items.Add(tag is null ? p : p with { Tag = tag });

        public void Line(double x1, double y1, double x2, double y2, Style? s = null, string? tag = null)
            => Add(Prim.Line(V(x1, y1), V(x2, y2), s ?? S.Vis), tag);

        /// <summary>Line between actual points.</summary>
        public void LineP(Vec2 a, Vec2 b, Style? s = null, string? tag = null) => Add(Prim.Line(a, b, s ?? S.Vis), tag);

        /// <summary>Polyline/polygon from design coordinate pairs.</summary>
        public void Poly(Style s, bool closed, params double[] xy)
        {
            var pts = new List<Vec2>(xy.Length / 2);
            for (var i = 0; i + 1 < xy.Length; i += 2) pts.Add(V(xy[i], xy[i + 1]));
            Add(Prim.Polyline(pts, s, closed));
        }

        /// <summary>Polyline/polygon from design points.</summary>
        public void PolyD(IEnumerable<Vec2> design, Style s, bool closed, string? tag = null)
            => Add(Prim.Polyline(design.Select(V).ToList(), s, closed), tag);

        /// <summary>Polyline/polygon from actual points.</summary>
        public void PolyP(IEnumerable<Vec2> actual, Style s, bool closed, string? tag = null)
            => Add(Prim.Polyline(actual.ToList(), s, closed), tag);

        /// <summary>Axis-aligned rectangle given by two design corners.</summary>
        public void Box(double x0, double y0, double x1, double y1, Style? s = null, string? tag = null)
        {
            var a = V(Math.Min(x0, x1), Math.Min(y0, y1));
            var b = V(Math.Max(x0, x1), Math.Max(y0, y1));
            Add(Prim.Rect(a.X, a.Y, b.X - a.X, b.Y - a.Y, s ?? S.Vis), tag);
        }

        /// <summary>Stretched ellipse/circle in design coordinates.</summary>
        public void Ellipse(double cx, double cy, double rx, double ry, Style? s = null, string? tag = null)
            => Add(Prim.Ellipse(cx * Sx, cy * Sy, rx * Sx, ry * Sy, s ?? S.Vis), tag);

        public void Circle(double cx, double cy, double r, Style? s = null, string? tag = null) => Ellipse(cx, cy, r, r, s, tag);

        /// <summary>True circle (detail scale) around an actual centre.</summary>
        public void Round(Vec2 c, double r, Style? s = null, string? tag = null)
            => Add(Prim.Circle(c.X, c.Y, r * K, s ?? S.Vis), tag);

        /// <summary>Detail line relative to an actual anchor point, offsets in design mm (uniform scale).</summary>
        public void Seg(Vec2 c, double dx1, double dy1, double dx2, double dy2, Style? s = null)
            => LineP(c + new Vec2(dx1 * K, dy1 * K), c + new Vec2(dx2 * K, dy2 * K), s);

        /// <summary>Elliptical arc points in design coordinates (degrees, positive = clockwise on screen).</summary>
        public static List<Vec2> ArcD(double cx, double cy, double rx, double ry, double startDeg, double sweepDeg, int n = 0)
            => Geo.ArcPoints(new Vec2(cx, cy), rx, ry, Geo.DegToRad(startDeg), Geo.DegToRad(sweepDeg), n);

        public void Arc(double cx, double cy, double rx, double ry, double startDeg, double sweepDeg, Style? s = null, string? tag = null)
            => PolyD(ArcD(cx, cy, rx, ry, startDeg, sweepDeg), s ?? S.Vis, false, tag);

        /// <summary>Centre cross (two centre lines) with the given design half extents.</summary>
        public void CenterCross(double cx, double cy, double ex, double ey)
        {
            Line(cx - ex, cy, cx + ex, cy, S.Ctr, "center");
            Line(cx, cy - ey, cx, cy + ey, S.Ctr, "center");
        }

        /// <summary>Horizontal centre line (axis) across the design width.</summary>
        public void Axis(double ex) => Line(-ex, 0, ex, 0, S.Ctr, "axis");
    }

    /// <summary>Reusable symbol fragments.</summary>
    internal static class Frag
    {
        public static Vec2 Polar(double r, double a) => new(r * Math.Cos(a), r * Math.Sin(a));

        /// <summary>
        /// Gear-to-shaft connection symbol on a vertical gear line (stick schematic). <paramref name="hub"/> is the
        /// actual point on the shaft axis; <paramref name="top"/>/<paramref name="bottom"/> are the gear line ends.
        /// </summary>
        public static void Hub(Pen p, Vec2 top, Vec2 hub, Vec2 bottom, string kind, Style st)
        {
            var k = p.D;
            if (top.Y > bottom.Y) (top, bottom) = (bottom, top);
            switch (kind)
            {
                case "fixed":
                    {
                        p.LineP(top, bottom, st);
                        var a = 1.5 * k;
                        p.LineP(hub + new Vec2(-a, -a), hub + new Vec2(a, a), st, "hub");
                        p.LineP(hub + new Vec2(-a, a), hub + new Vec2(a, -a), st, "hub");
                        break;
                    }
                case "sliding":
                    {
                        double gx = 2.4 * k, gy = 1.5 * k;
                        p.LineP(top, new Vec2(top.X, hub.Y - gy), st);
                        p.LineP(new Vec2(bottom.X, hub.Y + gy), bottom, st);
                        p.Add(Prim.Rect(hub.X - gx, hub.Y - gy, 2 * gx, 2 * gy, st), "hub");
                        break;
                    }
                case "loose":
                    {
                        double gx = 2.4 * k, gy = 1.5 * k;
                        p.LineP(top, new Vec2(top.X, hub.Y - gy), st);
                        p.LineP(new Vec2(bottom.X, hub.Y + gy), bottom, st);
                        p.LineP(new Vec2(hub.X - gx, hub.Y - gy), new Vec2(hub.X + gx, hub.Y - gy), st, "hub");
                        p.LineP(new Vec2(hub.X - gx, hub.Y + gy), new Vec2(hub.X + gx, hub.Y + gy), st, "hub");
                        break;
                    }
                default:
                    p.LineP(top, bottom, st);
                    break;
            }
        }

        /// <summary>Bore circle with optional keyway notch at the top (design coordinates).</summary>
        public static void Bore(Pen p, double r, bool keyway)
        {
            if (!keyway) { p.Circle(0, 0, r, S.Vis, "bore"); return; }
            var kw = r * 0.3;
            var kd = r * 0.35;
            var ar = -Math.Acos(kw / r);
            var al = -Math.PI + Math.Acos(kw / r);
            var pts = Geo.ArcPoints(Vec2.Zero, r, r, ar, al + 2 * Math.PI - ar, 48);
            pts.Add(new Vec2(-kw, -(r + kd)));
            pts.Add(new Vec2(kw, -(r + kd)));
            p.PolyD(pts, S.Vis, true, "bore");
        }

        /// <summary>
        /// Rectangle x0..x1 between yFar (away from the bite) and yEdge, with a circular bite of the circle (c, r)
        /// cut into the edge at yEdge. Used for bearing rings around rolling elements. Design coordinates.
        /// </summary>
        public static List<Vec2> NotchedRect(double x0, double x1, double yFar, double yEdge, Vec2 c, double r)
        {
            var dy = yEdge - c.Y;
            var pts = new List<Vec2> { new(x0, yFar), new(x1, yFar), new(x1, yEdge) };
            if (Math.Abs(dy) < r)
            {
                var dx = Math.Sqrt(r * r - dy * dy);
                var a0 = Math.Atan2(dy, dx);
                var a1 = Math.Atan2(dy, -dx);
                // Choose the sweep that passes through the far side (into the rectangle).
                var sweep = a1 - a0;
                var towardsFar = Math.Sign(yFar - yEdge);
                var midAngle = a0 + sweep / 2;
                if (Math.Sign(Math.Sin(midAngle)) != towardsFar) sweep = sweep > 0 ? sweep - 2 * Math.PI : sweep + 2 * Math.PI;
                pts.AddRange(Geo.ArcPoints(c, r, r, a0, sweep, 24));
            }
            pts.Add(new Vec2(x0, yEdge));
            return pts;
        }

        /// <summary>Ground/housing hatch strokes (45 degrees) along a line from a to b on the side given by the normal n (actual mm).</summary>
        public static void GroundStrokes(Pen p, Vec2 a, Vec2 b, Vec2 n, double depth, double spacing)
        {
            var d = b - a;
            var len = d.Length;
            if (len < Geo.Eps) return;
            var u = d / len;
            n = n.Normalized();
            var count = Math.Max(2, (int)Math.Floor(len / spacing));
            var step = len / count;
            // Strokes lean "backwards" along u so all of them stay between a and b.
            for (var i = 1; i <= count; i++)
            {
                var s = a + u * (i * step);
                var e = s + n * depth - u * Math.Min(depth, i * step);
                p.LineP(s, e, S.Thin, "ground");
            }
        }

        /// <summary>Zigzag polyline between x0 and x1 with n peaks, amplitude a around y (design).</summary>
        public static List<Vec2> Zigzag(double x0, double x1, double y, double a, int n)
        {
            var pts = new List<Vec2> { new(x0, y) };
            var step = (x1 - x0) / (2 * n);
            for (var i = 0; i < 2 * n; i++)
            {
                var x = x0 + step * (i + 0.5);
                pts.Add(new Vec2(x, y + (i % 2 == 0 ? -a : a)));
            }
            pts.Add(new Vec2(x1, y));
            return pts;
        }

        /// <summary>Hexagon with corners on the left/right (design coordinates).</summary>
        public static List<Vec2> Hexagon(double cx, double cy, double r)
            => Enumerable.Range(0, 6).Select(i => new Vec2(cx + r * Math.Cos(i * Math.PI / 3), cy + r * Math.Sin(i * Math.PI / 3))).ToList();
    }

    /// <summary>Gear proportions and tooth outlines (all relative, no module required).</summary>
    internal static class GearMath
    {
        public static int Teeth(SketchObject o, int fallback) => Math.Max(6, Math.Min(400, o.Int("teeth", fallback)));

        /// <summary>Pitch and root radius relative to the tip radius of an external gear with z teeth.</summary>
        public static (double Pitch, double Root) External(int z) => (z / (z + 2.0), (z - 2.5) / (z + 2.0));

        /// <summary>Pitch and tip radius relative to the root radius of an internal gear with z teeth.</summary>
        public static (double Pitch, double Tip) Internal(int z)
        {
            var pitch = z / (z + 2.5);
            return (pitch, pitch * (z - 2) / z);
        }

        /// <summary>
        /// Closed tooth outline (design coordinates) with involute-looking convex flanks. Works for external
        /// (tip &gt; root) and internal (tip &lt; root) gears. A tooth is centred at <paramref name="phase"/> (radians).
        /// </summary>
        public static List<Vec2> Outline(int z, double rRoot, double rTip, double rPitch, double phase)
        {
            var pts = new List<Vec2>(z * 13);
            var p = 2 * Math.PI / z;
            var depth = rTip - rRoot;
            var tp = Math.Abs(depth) < Geo.Eps ? 0.5 : Geo.Clamp((rPitch - rRoot) / depth, 0.1, 0.9);
            var flank = new[] { (0.0, 0.30), (tp * 0.5, 0.282), (tp, 0.25), ((tp + 1) / 2, 0.205), (1.0, 0.15) };
            for (var i = 0; i < z; i++)
            {
                var c = phase + i * p;
                foreach (var (t, h) in flank) pts.Add(Frag.Polar(rRoot + t * depth, c - h * p));
                pts.Add(Frag.Polar(rTip, c));
                for (var k = flank.Length - 1; k >= 0; k--) pts.Add(Frag.Polar(rRoot + flank[k].Item1 * depth, c + flank[k].Item2 * p));
                pts.Add(Frag.Polar(rRoot, c + 0.4 * p));
                pts.Add(Frag.Polar(rRoot, c + 0.5 * p));
                pts.Add(Frag.Polar(rRoot, c + 0.6 * p));
            }
            return pts;
        }

        /// <summary>Roller-chain sprocket outline: semicircular roller seats on the pitch circle, pointed teeth.</summary>
        public static List<Vec2> Sprocket(int z, double rPitch, double rTip, double phase)
        {
            var pts = new List<Vec2>();
            var p = 2 * Math.PI / z;
            var chord = 2 * rPitch * Math.Sin(Math.PI / z);
            var rr = 0.31 * chord;
            for (var i = 0; i < z; i++)
            {
                var a = phase + i * p;
                var u = new Vec2(Math.Cos(a), Math.Sin(a));
                var v = u.Perp;
                var c = u * rPitch;
                for (var t = -100; t <= 100; t += 20)
                {
                    var tr = Geo.DegToRad(t);
                    pts.Add(c - u * (rr * Math.Cos(tr)) + v * (rr * Math.Sin(tr)));
                }
                pts.Add(Frag.Polar(rTip, a + p / 2));
            }
            return pts;
        }
    }
}
