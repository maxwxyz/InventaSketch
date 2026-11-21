using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using InventaSketch.Core;

#if NETSTANDARD2_0
namespace System.Runtime.CompilerServices
{
    /// <summary>Polyfill so records/init accessors compile for netstandard2.0.</summary>
    internal static class IsExternalInit { }
}
#endif

namespace InventaSketch.Modules.Maps
{
    /// <summary>Library category ids of the maps module.</summary>
    public static class MapCategories
    {
        public const string Base = "map.base";
        public const string Lines = "map.lines";
        public const string Routes = "map.routes";
        public const string Poi = "map.poi";
        public const string Marks = "map.marks";
    }

    /// <summary>Semantic primitive tags used by the map types (tests and host naming).</summary>
    public static class MapTags
    {
        public const string Frame = "frame";
        public const string Background = "background";
        public const string Grid = "grid";
        public const string Terrain = "terrain";
        public const string Band = "band";
        public const string Edge = "edge";
        public const string Cap = "cap";
        public const string Divider = "divider";
        public const string Track = "track";
        public const string Water = "water";
        public const string Route = "route";
        public const string Halo = "halo";
        /// <summary>White pattern inside a line (railway bands, direction chevrons); keeps its colour under style overrides.</summary>
        public const string Inlay = "inlay";
        public const string Arrow = "arrow";
        public const string Waypoint = "waypoint";
        public const string Marker = "marker";
        public const string Glyph = "glyph";
        public const string Label = "label";
        public const string Shield = "shield";
        public const string Region = "region";
        public const string Bar = "bar";
        public const string Tick = "tick";
        public const string Needle = "needle";
        public const string Sample = "sample";
        public const string Tail = "tail";
        public const string Dot = "dot";
    }

    /// <summary>
    /// Colours. Every solid fill is either clearly dark (luminance below 0.3, becomes black in monochrome output, glyphs
    /// on it are white) or clearly light (luminance above 0.45, becomes white), so monochrome output keeps the contrast.
    /// </summary>
    internal static class Palette
    {
        public const string White = "#ffffff";
        public const string Black = "#000000";
        public const string Land = "#f4f1e8";
        public const string GridLine = "#9e9e9e";
        public const string Contour = "#a1887f";
        public const string Ink = "#333333";
        public const string Water = "#a9d3f2";
        public const string WaterLine = "#2f7bbf";
        public const string Route = "#1565c0";
        public const string RouteDark = "#0d47a1";
        public const string Start = "#1b5e20";
        public const string Finish = "#b71c1c";
        public const string Stop = "#0d47a1";
    }

    /// <summary>Shared line styles (ISO 128 line roles).</summary>
    internal static class S
    {
        public static readonly Style Vis = Style.Visible;
        public static readonly Style Fine = Style.Visible with { StrokeWidth = 0.35 };
        public static readonly Style Hair = Style.Visible with { StrokeWidth = 0.18 };
        public static readonly Style Thin = Style.ThinLine;
        public static readonly Style NoStroke = new() { Stroke = "none", Role = LineRole.Visible };

        public static Style Filled(string fill, Style? baseStyle = null) => (baseStyle ?? Fine) with { Fill = Fill.Solid(fill) };
        public static Style Stroke(string color, double width, DashStyle dash = DashStyle.Solid) => new() { Stroke = color, StrokeWidth = width, Dash = dash, Role = LineRole.Visible };
        public static Style FillOnly(string fill) => NoStroke with { Fill = Fill.Solid(fill) };
        public static readonly Style White = Filled(Palette.White);
        public static readonly Style Black = Filled(Palette.Black);
    }

    /// <summary>
    /// Drawing helper for box types. Symbols are authored in "design millimetres" for a design rectangle and stretched to
    /// the actual object box (centred at 0,0), so free uniform and non-uniform scaling stretches the drawing. Round details
    /// and text use the uniform detail scale <see cref="K"/>.
    /// </summary>
    internal sealed class Pen
    {
        private readonly List<Primitive> _items = new();

        public Pen(Size2 actual, Size2 design)
        {
            W = Math.Max(actual.W, 0.01);
            H = Math.Max(actual.H, 0.01);
            DW = Math.Max(design.W, 0.01);
            DH = Math.Max(design.H, 0.01);
            Sx = W / DW;
            Sy = H / DH;
            K = Math.Min(Sx, Sy);
        }

        /// <summary>Actual box size (mm).</summary>
        public double W { get; }
        public double H { get; }
        /// <summary>Design box size (design mm).</summary>
        public double DW { get; }
        public double DH { get; }
        public double Sx { get; }
        public double Sy { get; }
        public double K { get; }

        public IReadOnlyList<Primitive> Items => _items;

        public Vec2 V(double x, double y) => new(x * Sx, y * Sy);
        public Vec2 V(Vec2 p) => V(p.X, p.Y);

        public void Add(Primitive p, string? tag = null) => _items.Add(tag is null ? p : p with { Tag = tag });

        public void Line(double x1, double y1, double x2, double y2, Style s, string? tag = null) => Add(Prim.Line(V(x1, y1), V(x2, y2), s), tag);
        public void Line(Vec2 a, Vec2 b, Style s, string? tag = null) => Add(Prim.Line(V(a), V(b), s), tag);

        public void Poly(IEnumerable<Vec2> design, Style s, bool closed, string? tag = null) => Add(Prim.Polyline(design.Select(V).ToList(), s, closed), tag);

        public void Box(double x0, double y0, double x1, double y1, Style s, string? tag = null)
        {
            var a = V(Math.Min(x0, x1), Math.Min(y0, y1));
            var b = V(Math.Max(x0, x1), Math.Max(y0, y1));
            Add(Prim.Rect(a.X, a.Y, b.X - a.X, b.Y - a.Y, s), tag);
        }

        /// <summary>Rounded rectangle (corner radius in design mm at the detail scale).</summary>
        public void RBox(double x0, double y0, double x1, double y1, double r, Style s, string? tag = null)
        {
            var a = V(Math.Min(x0, x1), Math.Min(y0, y1));
            var b = V(Math.Max(x0, x1), Math.Max(y0, y1));
            Add(Geom.RoundedRect(a.X, a.Y, b.X - a.X, b.Y - a.Y, r * K, s), tag);
        }

        /// <summary>Ellipse stretched with the box.</summary>
        public void Ellipse(double cx, double cy, double rx, double ry, Style s, string? tag = null)
            => Add(Prim.Ellipse(cx * Sx, cy * Sy, rx * Sx, ry * Sy, s), tag);

        /// <summary>True circle (detail scale) around a design centre.</summary>
        public void Round(double cx, double cy, double r, Style s, string? tag = null)
        {
            var c = V(cx, cy);
            Add(Prim.Circle(c.X, c.Y, r * K, s), tag);
        }

        /// <summary>Path from design-coordinate segments.</summary>
        public void Path(IEnumerable<PathSeg> design, Style s, string? tag = null)
            => Add(new PathPrim(design.Select(g => g.Op == SegOp.Z ? g : new PathSeg(g.Op, V(g.P), V(g.C1), V(g.C2))).ToList(), s), tag);

        /// <summary>
        /// Text anchored at a design point. The font size (design mm) is scaled with <see cref="K"/> and reduced so the text
        /// fits <paramref name="maxWidth"/> and <paramref name="maxHeight"/> (design mm).
        /// </summary>
        public void Text(double x, double y, string text, double size, double maxWidth, double maxHeight, TextAnchor anchor = TextAnchor.Middle,
            bool bold = false, string? color = null, bool italic = false, string tag = MapTags.Label)
        {
            TextAt(V(x, y), text, size * K, maxWidth * Sx, maxHeight * Sy, anchor, bold, color, italic, tag);
        }

        /// <summary>
        /// Text at an actual (not design) position, fitted to an actual width/height. Multi-line text becomes one text
        /// primitive per line (line height 1.2 em, block centred on the position), so every host lays it out the same way.
        /// </summary>
        public void TextAt(Vec2 pos, string text, double size, double maxWidth, double maxHeight, TextAnchor anchor = TextAnchor.Middle,
            bool bold = false, string? color = null, bool italic = false, string tag = MapTags.Label)
        {
            if (string.IsNullOrEmpty(text)) return;
            var fs = Geom.FitText(text, size, maxWidth, maxHeight);
            if (fs <= 0.05) return;
            var lines = text.Replace("\r", "").Split('\n');
            var lineH = fs * 1.2;
            for (var i = 0; i < lines.Length; i++)
            {
                if (lines[i].Length == 0) continue;
                var p = pos + new Vec2(0, (i - (lines.Length - 1) / 2.0) * lineH);
                Add(new TextPrim(p, lines[i], new TextStyle { Size = fs, Anchor = anchor, Bold = bold, Italic = italic, Color = color }) { Tag = tag });
            }
        }
    }

    /// <summary>Geometry helpers shared by the map types.</summary>
    internal static class Geom
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

        /// <summary>Estimated width of a single-line text (mm).</summary>
        public static double TextWidth(string text, double size) => Math.Max(1, text.Length) * size * CharWidth;

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

        /// <summary>Catmull-Rom spline through the points as cubic Bézier segments (open or closed curve).</summary>
        public static List<PathSeg> Spline(IReadOnlyList<Vec2> p, bool closed = false)
        {
            var n = p.Count;
            var segs = new List<PathSeg> { PathSeg.Move(p[0]) };
            if (n == 2 && !closed) { segs.Add(PathSeg.Line(p[1])); return segs; }
            Vec2 P(int i) => closed ? p[((i % n) + n) % n] : p[Math.Max(0, Math.Min(n - 1, i))];
            var count = closed ? n : n - 1;
            for (var i = 0; i < count; i++)
            {
                Vec2 p0 = P(i - 1), p1 = P(i), p2 = P(i + 1), p3 = P(i + 2);
                segs.Add(PathSeg.Cubic(p1 + (p2 - p0) / 6, p2 - (p3 - p1) / 6, p2));
            }
            if (closed) segs.Add(PathSeg.Close);
            return segs;
        }

        /// <summary>Samples a path (lines and cubic/quadratic segments) into a polyline.</summary>
        public static List<Vec2> Sample(IReadOnlyList<PathSeg> segs, int steps = 12)
        {
            var pts = new List<Vec2>();
            var last = Vec2.Zero;
            var start = Vec2.Zero;
            foreach (var s in segs)
            {
                switch (s.Op)
                {
                    case SegOp.M: pts.Add(s.P); last = start = s.P; break;
                    case SegOp.L: pts.Add(s.P); last = s.P; break;
                    case SegOp.C:
                        for (var i = 1; i <= steps; i++) pts.Add(Geo.CubicAt(last, s.C1, s.C2, s.P, (double)i / steps));
                        last = s.P; break;
                    case SegOp.Q:
                        for (var i = 1; i <= steps; i++) pts.Add(Geo.QuadAt(last, s.C1, s.P, (double)i / steps));
                        last = s.P; break;
                    case SegOp.Z:
                        if (pts.Count > 0 && pts[pts.Count - 1].DistanceTo(start) > 1e-9) pts.Add(start);
                        break;
                }
            }
            return pts;
        }

        /// <summary>The centre line of a point object: the points, or a sampled smooth curve through them.</summary>
        public static List<Vec2> Centerline(IReadOnlyList<Vec2> pts, bool smooth, bool closed = false)
        {
            var clean = Dedupe(pts);
            if (!smooth || clean.Count < 3) return clean;
            return Sample(Spline(clean, closed), 12);
        }

        /// <summary>Removes consecutive duplicate points.</summary>
        public static List<Vec2> Dedupe(IReadOnlyList<Vec2> pts)
        {
            var list = new List<Vec2>(pts.Count);
            foreach (var p in pts) if (list.Count == 0 || list[list.Count - 1].DistanceTo(p) > 1e-6) list.Add(p);
            return list;
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

        /// <summary>
        /// Polyline offset by <paramref name="dist"/> along the left normal (screen coordinates) with mitred joints; very sharp
        /// joints are limited to <paramref name="miterLimit"/> times the distance.
        /// </summary>
        public static List<Vec2> Offset(IReadOnlyList<Vec2> pts, double dist, double miterLimit = 3)
        {
            var n = pts.Count;
            var result = new List<Vec2>(n);
            if (n < 2) { result.AddRange(pts); return result; }
            for (var i = 0; i < n; i++)
            {
                Vec2 nrm;
                double len = dist;
                if (i == 0) nrm = (pts[1] - pts[0]).Normalized().Perp;
                else if (i == n - 1) nrm = (pts[n - 1] - pts[n - 2]).Normalized().Perp;
                else
                {
                    var n0 = (pts[i] - pts[i - 1]).Normalized().Perp;
                    var n1 = (pts[i + 1] - pts[i]).Normalized().Perp;
                    var m = (n0 + n1).Normalized();
                    if (m.Length < 0.5) m = n0;
                    var cos = m.Dot(n0);
                    len = dist / Math.Max(cos, 1.0 / miterLimit);
                    nrm = m;
                }
                result.Add(pts[i] + nrm * len);
            }
            return result;
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

        /// <summary>Point and unit tangent at arc length <paramref name="s"/> along a polyline.</summary>
        public static (Vec2 P, Vec2 T) At(IReadOnlyList<Vec2> pts, double s)
        {
            for (var i = 0; i + 1 < pts.Count; i++)
            {
                var d = pts[i + 1] - pts[i];
                var l = d.Length;
                if (l < 1e-9 && i + 2 < pts.Count) continue;
                if (s <= l || i + 2 == pts.Count)
                    return (Vec2.Lerp(pts[i], pts[i + 1], l < 1e-9 ? 0 : Geo.Clamp(s / l, 0, 1)), l < 1e-9 ? new Vec2(1, 0) : d / l);
                s -= l;
            }
            return (pts.Count > 0 ? pts[0] : Vec2.Zero, new Vec2(1, 0));
        }

        /// <summary>Rail anchors along a centre line: every segment, or at most <paramref name="maxChords"/> chords of a dense curve.</summary>
        public static IEnumerable<Anchor> Rails(IReadOnlyList<Vec2> line, int maxChords = 24, string idPrefix = "rail")
        {
            if (line.Count < 2) yield break;
            if (line.Count - 1 <= maxChords)
            {
                for (var i = 0; i + 1 < line.Count; i++)
                    if (line[i].DistanceTo(line[i + 1]) > 1e-6) yield return Anchor.Rail(line[i], line[i + 1], idPrefix + I(i));
                yield break;
            }
            var total = Length(line);
            for (var i = 0; i < maxChords; i++)
                yield return Anchor.Rail(At(line, total * i / maxChords).P, At(line, total * (i + 1) / maxChords).P, idPrefix + I(i));
        }

        /// <summary>Distance from a point to a polyline.</summary>
        public static double DistanceToPolyline(Vec2 p, IReadOnlyList<Vec2> line)
        {
            var best = double.MaxValue;
            if (line.Count == 1) return p.DistanceTo(line[0]);
            for (var i = 0; i + 1 < line.Count; i++)
            {
                var (q, _) = Geo.ClosestOnSegment(p, line[i], line[i + 1]);
                best = Math.Min(best, p.DistanceTo(q));
            }
            return best;
        }

        /// <summary>Text rotation (degrees, clockwise) along a direction, kept upright (between -90 and 90).</summary>
        public static double Upright(Vec2 dir)
        {
            var a = Geo.RadToDeg(Math.Atan2(dir.Y, dir.X));
            if (a > 90) a -= 180;
            if (a < -90) a += 180;
            return a;
        }

        /// <summary>
        /// Cuts <paramref name="line"/> into the pieces that lie outside every "hole" (point predicate). Boundaries are
        /// refined by bisection, so the pieces end on the hole boundary within a few hundredths of a millimetre.
        /// </summary>
        public static List<List<Vec2>> Outside(IReadOnlyList<Vec2> line, Func<Vec2, bool> inHole, double step = 0.4)
        {
            var pieces = new List<List<Vec2>>();
            if (line.Count < 2) return pieces;
            List<Vec2>? cur = null;
            var prev = line[0];
            var prevIn = inHole(prev);
            if (!prevIn) { cur = new List<Vec2> { prev }; pieces.Add(cur); }
            Vec2 Boundary(Vec2 a, Vec2 b, bool aIn)
            {
                for (var k = 0; k < 12; k++)
                {
                    var m = Vec2.Lerp(a, b, 0.5);
                    if (inHole(m) == aIn) a = m; else b = m;
                }
                return Vec2.Lerp(a, b, 0.5);
            }
            for (var i = 0; i + 1 < line.Count; i++)
            {
                var a = line[i]; var b = line[i + 1];
                var len = a.DistanceTo(b);
                var n = Math.Max(1, (int)Math.Ceiling(len / step));
                for (var k = 1; k <= n; k++)
                {
                    var q = Vec2.Lerp(a, b, (double)k / n);
                    var qIn = inHole(q);
                    if (qIn != prevIn)
                    {
                        var x = Boundary(prev, q, prevIn);
                        if (prevIn) { cur = new List<Vec2> { x }; pieces.Add(cur); }
                        else { cur!.Add(x); cur = null; }
                    }
                    // Intermediate samples are collinear; only the original vertices shape the piece.
                    if (!qIn && k == n) cur!.Add(q);
                    prev = q; prevIn = qIn;
                }
            }
            return pieces.Where(p => p.Count >= 2 && Length(p) > 0.05).ToList();
        }

        /// <summary>
        /// One path with an open sub-path per piece (dashes, ticks, interrupted edges), so a host draws a single native
        /// freeform instead of many line shapes. Null if there is nothing to draw.
        /// </summary>
        public static PathPrim? MultiPath(IEnumerable<IReadOnlyList<Vec2>> pieces, Style style, string tag)
        {
            var segs = new List<PathSeg>();
            foreach (var piece in pieces)
            {
                if (piece.Count < 2) continue;
                segs.Add(PathSeg.Move(piece[0]));
                for (var i = 1; i < piece.Count; i++) segs.Add(PathSeg.Line(piece[i]));
            }
            return segs.Count == 0 ? null : new PathPrim(segs, style) { Tag = tag };
        }

        /// <summary>
        /// Text along the middle of a line, kept upright and placed <paramref name="offset"/> mm above it (or centred on it
        /// for offset 0).
        /// </summary>
        public static TextPrim LineLabel(IReadOnlyList<Vec2> line, string text, double offset, double size, string color, bool italic = false, bool bold = false)
        {
            var (mid, t) = At(line, Length(line) / 2);
            var n = t.Perp;
            if (n.Y > 0 || (Math.Abs(n.Y) < 1e-9 && n.X > 0)) n = -n;
            var pos = mid + n * (offset > 0 ? offset + size / 2 : 0);
            return new TextPrim(pos, text, new TextStyle { Size = size, Color = color, Italic = italic, Bold = bold }, Upright(t)) { Tag = MapTags.Label };
        }

        public static string I(int i) => i.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>Parameter definition and action helpers (all parameters are optional).</summary>
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

        public static ContextAction Toggle(SketchObject o, string id, string key, bool def, I18nText label, string icon = IconIds.Toggle)
            => new(id, label, icon, x => x.WithParam(key, !x.Flag(key, def))) { IsActive = o.Flag(key, def), Group = "param" };

        /// <summary>
        /// Cycles a choice parameter through its options; the label names the next option ("Road type: main road ▸").
        /// </summary>
        public static ContextAction Cycle(SketchObject o, string id, ParamDef def, I18nText prefix)
        {
            var options = def.Options ?? Array.Empty<ParamOption>();
            var fallback = def.Default?.Text ?? (options.Count > 0 ? options[0].Value : "");
            var cur = o.Str(def.Key, fallback);
            var idx = options.ToList().FindIndex(p => p.Value == cur);
            var next = options.Count == 0 ? null : options[(idx + 1) % options.Count];
            var label = next is null ? prefix : new I18nText(prefix.En + ": " + next.Label.En, (prefix.De ?? prefix.En) + ": " + (next.Label.De ?? next.Label.En));
            return new ContextAction(id, label, IconIds.Cycle, x =>
            {
                if (options.Count == 0) return x;
                var c = x.Str(def.Key, fallback);
                var i = options.ToList().FindIndex(p => p.Value == c);
                return x.WithParam(def.Key, options[(i + 1) % options.Count].Value);
            }) { Group = "param", ParamKeys = new[] { def.Key } };
        }

        /// <summary>Label of the current option of a choice parameter.</summary>
        public static I18nText OptionLabel(ParamDef def, string value)
            => def.Options?.FirstOrDefault(p => p.Value == value)?.Label ?? def.Options?.FirstOrDefault()?.Label ?? new I18nText(value);
    }

    /// <summary>Number formatting and parsing (decimal comma in German).</summary>
    internal static class Fmt
    {
        /// <summary>Shortest representation with at most <paramref name="decimals"/> decimals ("0.25", "1", "1,5").</summary>
        public static string Num(double v, Lang lang, int decimals = 3)
        {
            var s = Math.Round(v, decimals).ToString("0." + new string('#', Math.Max(1, decimals)), CultureInfo.InvariantCulture);
            if (s == "-0") s = "0";
            return lang == Lang.De ? s.Replace('.', ',') : s;
        }

        /// <summary>Parses "2", "2.5", "2,5" (and optional unit suffix handled by the caller); <see cref="FormatException"/> otherwise.</summary>
        public static double Parse(string text, Lang lang, double min, double max)
        {
            var t = (text ?? "").Trim().Replace(" ", "").Replace(',', '.');
            if (!double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) || double.IsNaN(v) || double.IsInfinity(v) || v < min || v > max)
                throw new FormatException(lang == Lang.De ? "Keine gültige Zahl: „" + text + "“" : "Not a valid number: “" + text + "”");
            return v;
        }

        /// <summary>
        /// Splits "2.5 km", "3mi", "500 m" into number and unit (one of <paramref name="units"/>, case-insensitive; null if
        /// none was given). Throws <see cref="FormatException"/> for unknown units or invalid numbers.
        /// </summary>
        public static (double Value, string? Unit) ParseWithUnit(string text, Lang lang, double min, double max, IReadOnlyList<string> units)
        {
            var t = (text ?? "").Trim().ToLowerInvariant();
            var i = t.Length;
            while (i > 0 && char.IsLetter(t[i - 1])) i--;
            var unit = t.Substring(i).Trim();
            var number = t.Substring(0, i).Trim();
            string? found = null;
            if (unit.Length > 0)
            {
                found = units.FirstOrDefault(u => string.Equals(u, unit, StringComparison.OrdinalIgnoreCase));
                if (found is null)
                    throw new FormatException(lang == Lang.De ? "Unbekannte Einheit: „" + unit + "“" : "Unknown unit: “" + unit + "”");
            }
            return (Parse(number, lang, min, max), found);
        }
    }
}
