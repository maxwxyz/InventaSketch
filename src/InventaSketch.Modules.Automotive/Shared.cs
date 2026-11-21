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

namespace InventaSketch.Modules.Automotive
{
    /// <summary>Library category ids of the automotive module.</summary>
    public static class AutoCategories
    {
        public const string Vehicles = "auto.vehicles";
        public const string Subsystems = "auto.subsystems";
        public const string Humans = "auto.humans";
        public const string Road = "auto.road";
        public const string Traffic = "auto.traffic";
        public const string Sensors = "auto.sensors";
        public const string Perception = "auto.perception";
        public const string Motion = "auto.motion";
        public const string Hmi = "auto.hmi";
    }

    /// <summary>Semantic primitive tags used by the automotive types (tests and host naming).</summary>
    public static class AutoTags
    {
        public const string Body = "body";
        public const string Wheel = "wheel";
        public const string Glass = "glass";
        public const string Lane = "lane";
        public const string Marking = "marking";
        public const string Edge = "edge";
        public const string Sector = "sector";
        public const string Path = "path";
        public const string Corridor = "corridor";
        public const string Tick = "tick";
        public const string Label = "label";
    }

    /// <summary>Pictogram ids used by the module's context actions in addition to <see cref="IconIds"/>.</summary>
    public static class AutoIcons
    {
        /// <summary>Increase a value (not yet part of the shared <see cref="IconIds"/> vocabulary; hosts fall back to a neutral icon).</summary>
        public const string Increase = IconIds.Increase;
        /// <summary>Decrease a value (see <see cref="Increase"/>).</summary>
        public const string Decrease = IconIds.Decrease;
    }

    /// <summary>Shared line styles (ISO 128 line roles; patent-style monochrome drawings).</summary>
    internal static class S
    {
        public static readonly Style Vis = Style.Visible;
        /// <summary>Outlines of small symbols (sensors, HMI details, humans).</summary>
        public static readonly Style Fine = Style.Visible with { StrokeWidth = 0.35 };
        public static readonly Style Thin = Style.ThinLine;
        public static readonly Style Ctr = Style.CenterLine;
        public static readonly Style Hid = Style.Hidden;
        public static readonly Style Phantom = Style.Of(LineRole.Phantom);
        /// <summary>White-filled outline that hides what was drawn before it (occlusion).</summary>
        public static readonly Style Opaque = Style.Visible with { Fill = Fill.Solid("#ffffff") };
        public static readonly Style OpaqueFine = Fine with { Fill = Fill.Solid("#ffffff") };
        public static readonly Style Black = Fine with { Fill = Fill.Solid("#000000") };
        public static readonly Style Dots = Style.ThinLine with { Fill = Fill.Dotted(2.0) };

        public static Style Hatch(double spacing, double angle = 45, Style? baseStyle = null)
            => (baseStyle ?? Thin) with { Fill = Fill.Hatched(new HatchPattern(angle, spacing)) };
    }

    /// <summary>
    /// Drawing helper. Symbols are authored in "design millimetres" for a design rectangle and stretched to the actual
    /// object box (centred at 0,0), so free uniform and non-uniform scaling stretches the drawing. Small details use the
    /// uniform detail scale <see cref="K"/>.
    /// </summary>
    internal sealed class Pen
    {
        private readonly List<Primitive> _items = new();
        private readonly double _cx, _cy;

        public Pen(Size2 actual, Size2 design) : this(actual, new Rect(-design.W / 2, -design.H / 2, design.W, design.H)) { }

        public Pen(Size2 actual, Rect design)
        {
            W = Math.Max(actual.W, 0.01);
            H = Math.Max(actual.H, 0.01);
            DW = Math.Max(design.W, 0.01);
            DH = Math.Max(design.H, 0.01);
            _cx = design.X + design.W / 2;
            _cy = design.Y + design.H / 2;
            Sx = W / DW;
            Sy = H / DH;
            K = Math.Min(Sx, Sy);
        }

        public double W { get; }
        public double H { get; }
        public double DW { get; }
        public double DH { get; }
        public double Sx { get; }
        public double Sy { get; }
        public double K { get; }

        public IReadOnlyList<Primitive> Items => _items;

        public Vec2 V(double x, double y) => new((x - _cx) * Sx, (y - _cy) * Sy);
        public Vec2 V(Vec2 p) => V(p.X, p.Y);

        public void Add(Primitive p, string? tag = null) => _items.Add(tag is null ? p : p with { Tag = tag });

        public void Line(double x1, double y1, double x2, double y2, Style? s = null, string? tag = null)
            => Add(Prim.Line(V(x1, y1), V(x2, y2), s ?? S.Vis), tag);

        public void Line(Vec2 a, Vec2 b, Style? s = null, string? tag = null) => Add(Prim.Line(V(a), V(b), s ?? S.Vis), tag);

        /// <summary>Polyline/polygon from design coordinate pairs.</summary>
        public void Poly(Style s, bool closed, params double[] xy)
        {
            var pts = new List<Vec2>(xy.Length / 2);
            for (var i = 0; i + 1 < xy.Length; i += 2) pts.Add(V(xy[i], xy[i + 1]));
            Add(Prim.Polyline(pts, s, closed));
        }

        public void PolyD(IEnumerable<Vec2> design, Style s, bool closed, string? tag = null)
            => Add(Prim.Polyline(design.Select(V).ToList(), s, closed), tag);

        /// <summary>Closed outline with rounded corners (radius in design mm, per corner clamped).</summary>
        public void Rounded(IReadOnlyList<Vec2> design, double r, Style s, string? tag = null)
            => Add(Shapes.RoundedPath(design.Select(V).ToList(), r * K, true, s), tag);

        public void RoundedOpen(IReadOnlyList<Vec2> design, double r, Style s, string? tag = null)
            => Add(Shapes.RoundedPath(design.Select(V).ToList(), r * K, false, s), tag);

        public void Box(double x0, double y0, double x1, double y1, Style? s = null, string? tag = null)
        {
            var a = V(Math.Min(x0, x1), Math.Min(y0, y1));
            var b = V(Math.Max(x0, x1), Math.Max(y0, y1));
            Add(Prim.Rect(a.X, a.Y, b.X - a.X, b.Y - a.Y, s ?? S.Vis), tag);
        }

        /// <summary>Rounded rectangle (corner radius in design mm, uniform detail scale).</summary>
        public void RBox(double x0, double y0, double x1, double y1, double r, Style? s = null, string? tag = null)
        {
            var a = V(Math.Min(x0, x1), Math.Min(y0, y1));
            var b = V(Math.Max(x0, x1), Math.Max(y0, y1));
            Add(Shapes.RoundedRect(a.X, a.Y, b.X - a.X, b.Y - a.Y, r * K, s ?? S.Vis), tag);
        }

        public void Ellipse(double cx, double cy, double rx, double ry, Style? s = null, string? tag = null)
            => Add(Prim.Ellipse((cx - _cx) * Sx, (cy - _cy) * Sy, rx * Sx, ry * Sy, s ?? S.Vis), tag);

        public void Circle(double cx, double cy, double r, Style? s = null, string? tag = null) => Ellipse(cx, cy, r, r, s, tag);

        /// <summary>True circle (detail scale) around a design centre.</summary>
        public void Round(double cx, double cy, double r, Style? s = null, string? tag = null)
        {
            var c = V(cx, cy);
            Add(Prim.Circle(c.X, c.Y, r * K, s ?? S.Vis), tag);
        }

        public static List<Vec2> ArcD(double cx, double cy, double rx, double ry, double startDeg, double sweepDeg, int n = 0)
            => Geo.ArcPoints(new Vec2(cx, cy), rx, ry, Geo.DegToRad(startDeg), Geo.DegToRad(sweepDeg), n);

        public void Arc(double cx, double cy, double rx, double ry, double startDeg, double sweepDeg, Style? s = null, string? tag = null)
            => PolyD(ArcD(cx, cy, rx, ry, startDeg, sweepDeg), s ?? S.Vis, false, tag);

        /// <summary>Tapered capsule (limb) between two design points; drawn opaque so later shapes hide earlier ones.</summary>
        public void Capsule(Vec2 a, Vec2 b, double ra, double rb, Style? s = null, string? tag = null)
            => PolyD(Shapes.Capsule(a, b, ra, rb), s ?? S.OpaqueFine, true, tag);

        /// <summary>
        /// Text centred (or anchored) at a design point. The font size (design mm) is scaled with <see cref="K"/> and
        /// reduced so the text fits <paramref name="maxWidth"/> (design mm) and <paramref name="maxHeight"/>.
        /// </summary>
        public void Text(double x, double y, string text, double size, double maxWidth, double maxHeight = double.PositiveInfinity,
            TextAnchor anchor = TextAnchor.Middle, bool bold = false, string? tag = null)
        {
            if (string.IsNullOrEmpty(text)) return;
            var fs = Shapes.FitText(text, size * K, maxWidth * Sx, maxHeight * Sy);
            if (fs <= 0.05) return;
            Add(new TextPrim(V(x, y), text, new TextStyle { Size = fs, Anchor = anchor, Bold = bold }) { Tag = tag ?? AutoTags.Label });
        }
    }

    /// <summary>Geometry helpers shared by the automotive types.</summary>
    internal static class Shapes
    {
        public const double CharWidth = 0.55;
        private const double Kappa = 0.5522847498307936;

        /// <summary>Largest font size not above <paramref name="size"/> whose estimated extent fits the given width/height.</summary>
        public static double FitText(string text, double size, double maxWidth, double maxHeight)
        {
            var lines = text.Replace("\r", "").Split('\n');
            var chars = Math.Max(1, lines.Max(l => l.Length));
            var fs = Math.Min(size, maxWidth / (chars * CharWidth));
            fs = Math.Min(fs, maxHeight / (1 + 1.2 * (lines.Length - 1)));
            return Math.Max(0, fs);
        }

        public static PathPrim RoundedRect(double x, double y, double w, double h, double r, Style style)
        {
            r = Math.Max(0, Math.Min(r, Math.Min(w, h) / 2));
            if (r < 1e-6) return Prim.Polygon(new[] { new Vec2(x, y), new Vec2(x + w, y), new Vec2(x + w, y + h), new Vec2(x, y + h) }, style);
            double x1 = x + w, y1 = y + h, c = r * (1 - Kappa);
            return new PathPrim(new[]
            {
                PathSeg.Move(new Vec2(x + r, y)), PathSeg.Line(new Vec2(x1 - r, y)),
                PathSeg.Cubic(new Vec2(x1 - c, y), new Vec2(x1, y + c), new Vec2(x1, y + r)), PathSeg.Line(new Vec2(x1, y1 - r)),
                PathSeg.Cubic(new Vec2(x1, y1 - c), new Vec2(x1 - c, y1), new Vec2(x1 - r, y1)), PathSeg.Line(new Vec2(x + r, y1)),
                PathSeg.Cubic(new Vec2(x + c, y1), new Vec2(x, y1 - c), new Vec2(x, y1 - r)), PathSeg.Line(new Vec2(x, y + r)),
                PathSeg.Cubic(new Vec2(x, y + c), new Vec2(x + c, y), new Vec2(x + r, y)), PathSeg.Close,
            }, style);
        }

        /// <summary>
        /// Polyline/polygon whose corners are rounded with cubic fillets (the curve stays inside the corner triangle,
        /// so the outline never leaves the convex hull of the points).
        /// </summary>
        public static PathPrim RoundedPath(IReadOnlyList<Vec2> pts, double r, bool closed, Style style)
        {
            var n = pts.Count;
            if (n < 3 || r <= 1e-6) return Prim.Polyline(pts, style, closed);
            var segs = new List<PathSeg>();
            (Vec2 In, Vec2 Out) Corner(int i)
            {
                var p = pts[i];
                var a = pts[(i - 1 + n) % n];
                var b = pts[(i + 1) % n];
                var la = (a - p).Length; var lb = (b - p).Length;
                var rr = Math.Min(r, Math.Min(la, lb) * 0.45);
                return (p + (a - p).Normalized() * rr, p + (b - p).Normalized() * rr);
            }
            void Fillet(int i, (Vec2 In, Vec2 Out) c)
            {
                var p = pts[i];
                segs.Add(PathSeg.Line(c.In));
                segs.Add(PathSeg.Cubic(c.In + (p - c.In) * (2.0 / 3), c.Out + (p - c.Out) * (2.0 / 3), c.Out));
            }
            if (closed)
            {
                var c0 = Corner(0);
                segs.Add(PathSeg.Move(c0.Out));
                for (var i = 1; i < n; i++) Fillet(i, Corner(i));
                Fillet(0, c0);
                segs.Add(PathSeg.Close);
            }
            else
            {
                segs.Add(PathSeg.Move(pts[0]));
                for (var i = 1; i < n - 1; i++) Fillet(i, Corner(i));
                segs.Add(PathSeg.Line(pts[n - 1]));
            }
            return new PathPrim(segs, style);
        }

        /// <summary>Outline of the convex hull of two circles (a tapered capsule) as a polygon.</summary>
        public static List<Vec2> Capsule(Vec2 a, Vec2 b, double ra, double rb, int arcSteps = 10)
        {
            var d = b - a;
            var len = d.Length;
            if (len < Math.Abs(ra - rb) + 1e-6) return Geo.ArcPoints(ra >= rb ? a : b, Math.Max(ra, rb), Math.Max(ra, rb), 0, 2 * Math.PI, 2 * arcSteps).Take(2 * arcSteps).ToList();
            var th = Math.Atan2(d.Y, d.X);
            var al = Math.Acos(Geo.Clamp((ra - rb) / len, -1, 1));
            var pts = new List<Vec2>();
            pts.AddRange(Geo.ArcPoints(b, rb, rb, th - al, 2 * al, arcSteps));
            pts.AddRange(Geo.ArcPoints(a, ra, ra, th + al, 2 * Math.PI - 2 * al, arcSteps));
            return pts;
        }

        /// <summary>Catmull-Rom spline through the points as cubic Bézier segments (open curve).</summary>
        public static List<PathSeg> Spline(IReadOnlyList<Vec2> p)
        {
            var segs = new List<PathSeg> { PathSeg.Move(p[0]) };
            if (p.Count == 2) { segs.Add(PathSeg.Line(p[1])); return segs; }
            for (var i = 0; i + 1 < p.Count; i++)
            {
                var p0 = p[Math.Max(0, i - 1)]; var p1 = p[i]; var p2 = p[i + 1]; var p3 = p[Math.Min(p.Count - 1, i + 2)];
                segs.Add(PathSeg.Cubic(p1 + (p2 - p0) / 6, p2 - (p3 - p1) / 6, p2));
            }
            return segs;
        }

        /// <summary>Samples a path (lines and cubic segments) into a polyline.</summary>
        public static List<Vec2> Sample(IReadOnlyList<PathSeg> segs, int steps = 16)
        {
            var pts = new List<Vec2>();
            var last = Vec2.Zero;
            foreach (var s in segs)
            {
                switch (s.Op)
                {
                    case SegOp.M: pts.Add(s.P); last = s.P; break;
                    case SegOp.L: pts.Add(s.P); last = s.P; break;
                    case SegOp.C:
                        for (var i = 1; i <= steps; i++) pts.Add(Geo.CubicAt(last, s.C1, s.C2, s.P, (double)i / steps));
                        last = s.P; break;
                    case SegOp.Q:
                        for (var i = 1; i <= steps; i++) pts.Add(Geo.QuadAt(last, s.C1, s.P, (double)i / steps));
                        last = s.P; break;
                }
            }
            return pts;
        }

        /// <summary>Unit tangent at every vertex of a polyline.</summary>
        public static List<Vec2> Tangents(IReadOnlyList<Vec2> pts)
        {
            var t = new List<Vec2>(pts.Count);
            for (var i = 0; i < pts.Count; i++)
            {
                var a = pts[Math.Max(0, i - 1)]; var b = pts[Math.Min(pts.Count - 1, i + 1)];
                var d = (b - a).Normalized();
                t.Add(d.Length < 0.5 ? new Vec2(1, 0) : d);
            }
            return t;
        }

        /// <summary>Polyline offset by <paramref name="dist"/> along the left normal (screen coordinates, y down).</summary>
        public static List<Vec2> Offset(IReadOnlyList<Vec2> pts, double dist)
        {
            var t = Tangents(pts);
            return pts.Select((p, i) => p + t[i].Perp * dist).ToList();
        }

        /// <summary>Splits a polyline into dashes of length <paramref name="dash"/> separated by <paramref name="gap"/>.</summary>
        public static List<List<Vec2>> Dashes(IReadOnlyList<Vec2> pts, double dash, double gap, double phase = 0)
        {
            var result = new List<List<Vec2>>();
            if (pts.Count < 2 || dash <= 0) return result;
            if (gap <= 0) { result.Add(pts.ToList()); return result; }
            var period = dash + gap;
            var pos = -phase;
            List<Vec2>? cur = null;
            for (var i = 0; i + 1 < pts.Count; i++)
            {
                var a = pts[i]; var b = pts[i + 1];
                var len = (b - a).Length;
                if (len < 1e-9) continue;
                double s = 0;
                while (s < len - 1e-9)
                {
                    var m = ((pos % period) + period) % period;
                    var on = m < dash;
                    var step = Math.Min(len - s, (on ? dash : period) - m);
                    if (step < 1e-9) step = Math.Min(len - s, 1e-6);
                    var p0 = Vec2.Lerp(a, b, s / len);
                    var p1 = Vec2.Lerp(a, b, (s + step) / len);
                    if (on)
                    {
                        if (cur is null) { cur = new List<Vec2> { p0 }; result.Add(cur); }
                        cur.Add(p1);
                    }
                    else cur = null;
                    s += step; pos += step;
                }
            }
            return result.Where(d => d.Count >= 2).ToList();
        }

        public static double Length(IReadOnlyList<Vec2> pts)
        {
            double l = 0;
            for (var i = 0; i + 1 < pts.Count; i++) l += (pts[i + 1] - pts[i]).Length;
            return l;
        }

        /// <summary>Point and tangent at arc length <paramref name="s"/> along a polyline.</summary>
        public static (Vec2 P, Vec2 T) At(IReadOnlyList<Vec2> pts, double s)
        {
            for (var i = 0; i + 1 < pts.Count; i++)
            {
                var d = pts[i + 1] - pts[i];
                var l = d.Length;
                if (s <= l || i + 2 == pts.Count)
                    return (Vec2.Lerp(pts[i], pts[i + 1], l < 1e-9 ? 0 : Geo.Clamp(s / l, 0, 1)), l < 1e-9 ? new Vec2(1, 0) : d / l);
                s -= l;
            }
            return (pts.Count > 0 ? pts[0] : Vec2.Zero, new Vec2(1, 0));
        }

        /// <summary>Approximates a polyline by axis segments (for lane centrelines on curves and rings).</summary>
        public static IEnumerable<Anchor> AxisChords(IReadOnlyList<Vec2> pts, int chords)
        {
            if (pts.Count < 2) yield break;
            var total = Length(pts);
            for (var i = 0; i < chords; i++)
                yield return Anchor.Segment(AnchorRole.Axis, At(pts, total * i / chords).P, At(pts, total * (i + 1) / chords).P);
        }
    }

    /// <summary>Parameter definition helpers (all parameters are optional).</summary>
    internal static class Prm
    {
        public static ParamDef Num(string key, string en, string de, double def, double min, double max, string? unit = "mm", double? step = null, bool advanced = false)
            => new(key, new I18nText(en, de), ParamKind.Number) { Default = def, Min = min, Max = max, Unit = unit, Step = step, Advanced = advanced };

        public static ParamDef Int(string key, string en, string de, int def, int min, int max)
            => new(key, new I18nText(en, de), ParamKind.Integer) { Default = def, Min = min, Max = max, Step = 1 };

        public static ParamDef Bool(string key, string en, string de, bool def)
            => new(key, new I18nText(en, de), ParamKind.Boolean) { Default = def };

        public static ParamDef Text(string key, string en, string de, string def)
            => new(key, new I18nText(en, de), ParamKind.Text) { Default = def };

        public static ParamDef Choice(string key, string en, string de, string def, params (string Value, string En, string De)[] options)
            => new(key, new I18nText(en, de), ParamKind.Choice)
            {
                Default = def,
                Options = options.Select(x => new ParamOption(x.Value, new I18nText(x.En, x.De))).ToList(),
            };

        public static ContextAction Toggle(SketchObject o, string key, bool def, I18nText label, string icon = IconIds.Toggle)
            => new("param." + key, label, icon, x => x.WithParam(key, !x.Flag(key, def))) { IsActive = o.Flag(key, def), Group = "param" };

        /// <summary>Cycles a choice parameter through its options.</summary>
        public static ContextAction Cycle(SketchObject o, ParamDef def, I18nText label)
        {
            var options = def.Options ?? Array.Empty<ParamOption>();
            var fallback = def.Default?.Text ?? (options.Count > 0 ? options[0].Value : "");
            return new ContextAction("param." + def.Key, label, IconIds.Cycle, x =>
            {
                if (options.Count == 0) return x;
                var cur = x.Str(def.Key, fallback);
                var idx = options.ToList().FindIndex(p => p.Value == cur);
                return x.WithParam(def.Key, options[(idx + 1) % options.Count].Value);
            }) { Group = "param", ParamKeys = new[] { def.Key } };
        }

        /// <summary>Increase/decrease actions for a numeric parameter (multiplicative or additive step, clamped).</summary>
        public static IEnumerable<ContextAction> PlusMinus(string key, double def, double min, double max, double step, bool multiply, I18nText more, I18nText less, string group)
        {
            double Next(double v, int dir) => Geo.Clamp(multiply ? (dir > 0 ? v * step : v / step) : v + dir * step, min, max);
            yield return new ContextAction("param." + key + ".inc", more, AutoIcons.Increase, x => x.WithParam(key, Math.Round(Next(x.Num(key, def), +1), 3))) { Group = group };
            yield return new ContextAction("param." + key + ".dec", less, AutoIcons.Decrease, x => x.WithParam(key, Math.Round(Next(x.Num(key, def), -1), 3))) { Group = group };
        }
    }
}
