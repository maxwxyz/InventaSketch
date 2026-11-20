using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

#if NETSTANDARD2_0
namespace System.Runtime.CompilerServices
{
    /// <summary>Polyfill so records/init accessors compile for netstandard2.0.</summary>
    internal static class IsExternalInit { }
}
#endif

namespace InventaSketch.Modules.Kinematics
{
    /// <summary>Semantic primitive tags of the kinematics module (tests, host naming).</summary>
    public static class KinTags
    {
        /// <summary>A rigid link (bar) of the current position.</summary>
        public const string Link = "link";
        /// <summary>A joint circle (revolute pair).</summary>
        public const string Joint = "joint";
        /// <summary>Ground (frame) symbol strokes and lines.</summary>
        public const string Ground = "ground";
        /// <summary>Slider block or guide of a prismatic pair.</summary>
        public const string Slider = "slider";
        /// <summary>Guide line of a prismatic pair.</summary>
        public const string Guide = "guide";
        /// <summary>One ghost (alternative) position, a group drawn with phantom lines.</summary>
        public const string Ghost = "ghost";
        /// <summary>Motion path / trace of a point.</summary>
        public const string Path = "path";
        /// <summary>Degree-of-freedom badge.</summary>
        public const string Dof = "dof";
        /// <summary>"No assembly possible" note of a mechanism in a position that cannot be assembled.</summary>
        public const string NoAssembly = "noassembly";
        /// <summary>Angle-limit arc.</summary>
        public const string Arc = "arc";
        /// <summary>Labels (angle values).</summary>
        public const string Label = "label";
        /// <summary>Spring / damper body.</summary>
        public const string Element = "element";
    }

    /// <summary>Shared line styles (ISO 128 line roles).</summary>
    internal static class KS
    {
        /// <summary>Links and bodies: continuous wide line.</summary>
        public static readonly Style Link = Style.Visible;
        /// <summary>Joint circles: white filled so link ends disappear underneath.</summary>
        public static readonly Style Joint = Style.Visible with { StrokeWidth = 0.35, Fill = Fill.Solid("#ffffff") };
        /// <summary>Bodies drawn as white-filled outlines (blocks, plates, detailed bars).</summary>
        public static readonly Style Body = Style.Visible with { Fill = Fill.Solid("#ffffff") };
        public static readonly Style Thin = Style.ThinLine;
        public static readonly Style Ctr = Style.CenterLine;
        /// <summary>Alternative positions of movable parts (ISO 128 phantom line).</summary>
        public static readonly Style Ghost = Style.Of(LineRole.Phantom);
        /// <summary>Motion traces (thin dashed).</summary>
        public static readonly Style Trace = Style.ThinLine with { Dash = DashStyle.Dash };
        public static readonly Style Black = Style.Visible with { StrokeWidth = 0.25, Fill = Fill.Solid("#000000") };
        public static readonly Style Arrow = Style.ThinLine with { StrokeWidth = 0.35, ArrowEnd = ArrowHead.Triangle, ArrowSize = ArrowSize.Small };
        public static readonly Style DoubleArrow = Arrow with { ArrowStart = ArrowHead.Triangle };
        public static Style Hatch(double spacing, double angle = 45) => Style.Visible with { Fill = Fill.Hatched(new HatchPattern(angle, spacing)) };
        /// <summary>Hatched body on a white background (covers lines underneath).</summary>
        public static Style HatchWhite(double spacing, double angle = 45) => Style.Visible with { Fill = Fill.Hatched(new HatchPattern(angle, spacing), "#ffffff") };
    }

    /// <summary>
    /// Drawing helper for symbols authored in "design millimetres" for a reference box centred at (0,0) and stretched
    /// to the actual object size (free non-uniform scaling stretches the symbol). Joint circles use the uniform detail
    /// scale <see cref="K"/> so they stay round.
    /// </summary>
    internal sealed class Pen
    {
        public Pen(Size2 actual, Size2 design)
        {
            W = Math.Max(actual.W, 0.01);
            H = Math.Max(actual.H, 0.01);
            Sx = W / design.W;
            Sy = H / design.H;
            K = Math.Min(Sx, Sy);
        }

        public double W { get; }
        public double H { get; }
        public double Sx { get; }
        public double Sy { get; }
        public double K { get; }
        public List<Primitive> Items { get; } = new();

        public Vec2 V(double x, double y) => new(x * Sx, y * Sy);
        public Vec2 V(Vec2 p) => new(p.X * Sx, p.Y * Sy);

        public void Add(Primitive p, string? tag = null) => Items.Add(tag is null ? p : p with { Tag = tag });
        public void Line(double x1, double y1, double x2, double y2, Style? s = null, string? tag = null) => Add(Prim.Line(V(x1, y1), V(x2, y2), s ?? KS.Link), tag);
        public void LineP(Vec2 a, Vec2 b, Style? s = null, string? tag = null) => Add(Prim.Line(a, b, s ?? KS.Link), tag);
        public void PolyD(IEnumerable<Vec2> design, Style s, bool closed, string? tag = null) => Add(Prim.Polyline(design.Select(V).ToList(), s, closed), tag);
        public void PolyP(IEnumerable<Vec2> actual, Style s, bool closed, string? tag = null) => Add(Prim.Polyline(actual.ToList(), s, closed), tag);

        public void Poly(Style s, bool closed, string? tag, params double[] xy)
        {
            var pts = new List<Vec2>(xy.Length / 2);
            for (var i = 0; i + 1 < xy.Length; i += 2) pts.Add(V(xy[i], xy[i + 1]));
            Add(Prim.Polyline(pts, s, closed), tag);
        }

        public void Box(double x0, double y0, double x1, double y1, Style? s = null, string? tag = null)
        {
            var a = V(Math.Min(x0, x1), Math.Min(y0, y1));
            var b = V(Math.Max(x0, x1), Math.Max(y0, y1));
            Add(Prim.Rect(a.X, a.Y, b.X - a.X, b.Y - a.Y, s ?? KS.Link), tag);
        }

        /// <summary>Stretched ellipse in design coordinates.</summary>
        public void Ellipse(double cx, double cy, double rx, double ry, Style? s = null, string? tag = null)
            => Add(Prim.Ellipse(cx * Sx, cy * Sy, rx * Sx, ry * Sy, s ?? KS.Link), tag);

        /// <summary>True circle (detail scale) around a design centre.</summary>
        public void Round(double cx, double cy, double r, Style? s = null, string? tag = null)
        {
            var c = V(cx, cy);
            Add(Prim.Circle(c.X, c.Y, r * K, s ?? KS.Link), tag);
        }

        /// <summary>Joint circle (white filled) at a design centre with design radius r (detail scale).</summary>
        public void Joint(double cx, double cy, double r) => Round(cx, cy, r, KS.Joint, KinTags.Joint);

        public void Arc(double cx, double cy, double rx, double ry, double startDeg, double sweepDeg, Style? s = null, string? tag = null)
            => PolyD(Geo.ArcPoints(new Vec2(cx, cy), rx, ry, Geo.DegToRad(startDeg), Geo.DegToRad(sweepDeg)), s ?? KS.Link, false, tag);

        /// <summary>Ground hatch strokes below the design line (x0..x1 at y) with design depth.</summary>
        public void GroundLine(double x0, double x1, double y, double depth, bool below = true)
        {
            var a = V(x0, y); var b = V(x1, y);
            LineP(a, b, KS.Link, KinTags.Ground);
            Frag.GroundStrokes(Items, a, b, new Vec2(0, below ? 1 : -1), depth * Sy, Frag.HatchSpacing(depth * Sy));
        }
    }

    /// <summary>Reusable symbol fragments in actual (local or page) millimetres.</summary>
    internal static class Frag
    {
        public static double HatchSpacing(double depth) => Geo.Clamp(depth * 1.1, 0.9, 2.2);

        /// <summary>45° ground strokes along a..b towards the normal n; all strokes stay between a and b.</summary>
        public static void GroundStrokes(List<Primitive> list, Vec2 a, Vec2 b, Vec2 n, double depth, double spacing, Style? style = null)
        {
            var d = b - a;
            var len = d.Length;
            if (len < Geo.Eps || depth <= Geo.Eps) return;
            var u = d / len;
            n = n.Normalized();
            var count = Math.Max(2, (int)Math.Floor(len / spacing));
            var step = len / count;
            for (var i = 1; i <= count; i++)
            {
                var s = a + u * (i * step);
                var lean = Math.Min(depth, i * step);
                list.Add(new LinePrim(s, s + n * depth - u * lean, style ?? KS.Thin) { Tag = KinTags.Ground });
            }
        }

        /// <summary>
        /// Ground (fixed) pivot: triangle from the pivot to a hatched base, pivot circle on top. <paramref name="down"/>
        /// points from the pivot to the base. Extents: ±3r across, from -r to 4.4r along <paramref name="down"/>.
        /// </summary>
        public static void GroundPivot(List<Primitive> list, Vec2 c, double r, Vec2 down, bool detailed, Style? style = null)
        {
            var dn = down.Normalized();
            var t = dn.Perp;
            Vec2 P(double across, double along) => c + t * across + dn * along;
            var st = style ?? KS.Link;
            if (detailed)
            {
                // Bearing block: semicircular head around the pivot, straight flanks, foot plate.
                // Head arc from +t over the side away from the base (-dn) to -t, then down to the foot.
                var pts = new List<Vec2>();
                for (var i = 0; i <= 16; i++)
                {
                    var a = Math.PI * i / 16;
                    pts.Add(c + (t * Math.Cos(a) - dn * Math.Sin(a)) * (1.9 * r));
                }
                pts.Add(P(-1.9 * r, 2.2 * r));
                pts.Add(P(-2.9 * r, 2.2 * r));
                pts.Add(P(-2.9 * r, 3.0 * r));
                pts.Add(P(2.9 * r, 3.0 * r));
                pts.Add(P(2.9 * r, 2.2 * r));
                pts.Add(P(1.9 * r, 2.2 * r));
                list.Add(Prim.Polygon(pts, style is null ? KS.Body : st with { Fill = Fill.Solid("#ffffff") }) with { Tag = KinTags.Ground });
                GroundStrokes(list, P(-3.2 * r, 3.0 * r), P(3.2 * r, 3.0 * r), dn, 1.4 * r, HatchSpacing(1.4 * r));
                list.Add(new LinePrim(P(-3.2 * r, 3.0 * r), P(3.2 * r, 3.0 * r), st) { Tag = KinTags.Ground });
                list.Add(Prim.Circle(c.X, c.Y, r, KS.Joint) with { Tag = KinTags.Joint });
                list.Add(Prim.Circle(c.X, c.Y, 0.35 * r, KS.Black) with { Tag = KinTags.Joint });
                return;
            }
            list.Add(Prim.Polyline(new[] { P(-2.0 * r, 3.0 * r), c, P(2.0 * r, 3.0 * r) }, st) with { Tag = KinTags.Ground });
            list.Add(new LinePrim(P(-3.0 * r, 3.0 * r), P(3.0 * r, 3.0 * r), st) { Tag = KinTags.Ground });
            GroundStrokes(list, P(-3.0 * r, 3.0 * r), P(3.0 * r, 3.0 * r), dn, 1.4 * r, HatchSpacing(1.4 * r));
            list.Add(Prim.Circle(c.X, c.Y, r, KS.Joint) with { Tag = KinTags.Joint });
        }

        /// <summary>Bar with semicircular ends (detailed link), closed outline.</summary>
        public static PathPrim Capsule(Vec2 a, Vec2 b, double hw, Style style)
        {
            var d = b - a;
            var ang = d.Length < Geo.Eps ? 0 : Math.Atan2(d.Y, d.X);
            var pts = new List<Vec2>();
            pts.AddRange(Geo.ArcPoints(b, hw, hw, ang - Math.PI / 2, Math.PI, 12));
            pts.AddRange(Geo.ArcPoints(a, hw, hw, ang + Math.PI / 2, Math.PI, 12));
            return Prim.Polygon(pts, style);
        }

        /// <summary>Rectangle centred at c with half length along <paramref name="axis"/> and half height across it.</summary>
        public static PathPrim Block(Vec2 c, Vec2 axis, double halfLen, double halfH, Style style)
        {
            var u = axis.Normalized();
            if (u.Length < Geo.Eps) u = new Vec2(1, 0);
            var v = u.Perp;
            return Prim.Polygon(new[] { c - u * halfLen - v * halfH, c + u * halfLen - v * halfH, c + u * halfLen + v * halfH, c - u * halfLen + v * halfH }, style);
        }

        /// <summary>Zigzag between a and b: straight lead-ins (fraction <paramref name="lead"/>), n peaks, amplitude amp.</summary>
        public static List<Vec2> Zigzag(Vec2 a, Vec2 b, int n, double amp, double lead)
        {
            var d = b - a;
            var len = d.Length;
            var u = len < Geo.Eps ? new Vec2(1, 0) : d / len;
            var v = u.Perp;
            var l0 = len * lead;
            var body = len - 2 * l0;
            var pts = new List<Vec2> { a, a + u * l0 };
            for (var i = 0; i < 2 * n; i++)
            {
                var t = l0 + body * (i + 0.5) / (2 * n);
                pts.Add(a + u * t + v * (i % 2 == 0 ? -amp : amp));
            }
            pts.Add(a + u * (len - l0));
            pts.Add(b);
            return pts;
        }

        /// <summary>Text that fits into the box (±hw, ±hh): the size shrinks and the position is clamped.</summary>
        public static TextPrim FitText(Vec2 at, string text, double size, double hw, double hh, string? tag = null, bool italic = false)
        {
            var n = Math.Max(1, text.Length);
            size = Math.Max(0.5, Math.Min(size, Math.Min(2 * hw / (n * 0.55 + 0.01), 2 * hh)));
            var w = n * size * 0.55;
            var x = Geo.Clamp(at.X, -hw + w / 2, hw - w / 2);
            var y = Geo.Clamp(at.Y, -hh + size / 2, hh - size / 2);
            if (w > 2 * hw) x = 0;
            return new TextPrim(new Vec2(x, y), text, new TextStyle { Size = size, Italic = italic }) { Tag = tag };
        }

        /// <summary>Formats an angle in degrees ("-30°").</summary>
        public static string Deg(double v)
        {
            var r = Math.Round(v, 1);
            var s = r.ToString(Math.Abs(r - Math.Round(r)) < 1e-9 ? "0" : "0.#", System.Globalization.CultureInfo.InvariantCulture);
            return (s == "-0" ? "0" : s).Replace("-", "−") + "°";
        }

        /// <summary>Catmull-Rom spline through the points as cubic segments.</summary>
        public static List<PathSeg> Smooth(IReadOnlyList<Vec2> p, bool closed)
        {
            var segs = new List<PathSeg> { PathSeg.Move(p[0]) };
            var n = p.Count;
            var count = closed ? n : n - 1;
            for (var i = 0; i < count; i++)
            {
                Vec2 P(int k) => closed ? p[((k % n) + n) % n] : p[Math.Max(0, Math.Min(n - 1, k))];
                var p0 = P(i - 1); var p1 = P(i); var p2 = P(i + 1); var p3 = P(i + 2);
                segs.Add(PathSeg.Cubic(p1 + (p2 - p0) / 6, p2 - (p3 - p1) / 6, p2));
            }
            if (closed) segs.Add(PathSeg.Close);
            return segs;
        }
    }
}
