using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace InventaSketch.Core
{
    /// <summary>Arrowhead geometry (explicit, so output is identical in every SVG consumer and host fallback).</summary>
    public static class ArrowHeads
    {
        /// <summary>Head length in mm for a given size and stroke width.</summary>
        public static double Length(ArrowSize size, double strokeWidth) =>
            Math.Max(1.6, strokeWidth * 5) * size switch { ArrowSize.Small => 0.7, ArrowSize.Large => 1.5, _ => 1.0 };

        /// <summary>
        /// Builds the head at <paramref name="tip"/> pointing along <paramref name="dir"/> (unit vector from the line towards the tip).
        /// Returns the primitives and how far the line must be shortened so it does not poke through the head.
        /// </summary>
        public static (IReadOnlyList<Primitive> Prims, double Inset) Build(ArrowHead head, Vec2 tip, Vec2 dir, ArrowSize size, string color, double strokeWidth)
        {
            if (head == ArrowHead.None) return (Array.Empty<Primitive>(), 0);
            var l = Length(size, strokeWidth);
            var w = l * 0.38;
            var d = dir.Normalized();
            var n = d.Perp;
            var back = tip - d * l;
            var solid = new Style { Stroke = color, StrokeWidth = strokeWidth, Fill = Fill.Solid(color), Dash = DashStyle.Solid };
            var open = new Style { Stroke = color, StrokeWidth = strokeWidth, Fill = Fill.None, Dash = DashStyle.Solid };
            switch (head)
            {
                case ArrowHead.Triangle:
                    return (new Primitive[] { Prim.Polygon(new[] { tip, back + n * w, back - n * w }, solid) }, l * 0.9);
                case ArrowHead.Stealth:
                    return (new Primitive[] { Prim.Polygon(new[] { tip, back + n * w, tip - d * (l * 0.65), back - n * w }, solid) }, l * 0.6);
                case ArrowHead.Open:
                    return (new Primitive[] { Prim.Polyline(new[] { back + n * w, tip, back - n * w }, open) }, 0);
                case ArrowHead.Half:
                    return (new Primitive[] { Prim.Polygon(new[] { tip, back + n * w, back }, solid) }, l * 0.9);
                case ArrowHead.Diamond:
                    {
                        var m = tip - d * (l / 2);
                        return (new Primitive[] { Prim.Polygon(new[] { tip, m + n * w, tip - d * l, m - n * w }, solid) }, l);
                    }
                case ArrowHead.Oval:
                    {
                        var r = l * 0.3;
                        var c = tip - d * r;
                        return (new Primitive[] { Prim.Circle(c.X, c.Y, r, solid) }, r);
                    }
                case ArrowHead.Tick:
                    {
                        // Architectural/dimension tick: short oblique stroke through the tip.
                        var t = (d + n).Normalized() * (l * 0.45);
                        return (new Primitive[] { Prim.Line(tip - t, tip + t, open with { StrokeWidth = strokeWidth * 1.6 }) }, 0);
                    }
                default:
                    return (Array.Empty<Primitive>(), 0);
            }
        }
    }

    public sealed record SvgOptions
    {
        /// <summary>Converts all colours to black/white (patent and monochrome export).</summary>
        public bool Monochrome { get; init; }
        /// <summary>Writes explicit hatch lines instead of SVG patterns (best compatibility, required for PowerPoint SVG import).</summary>
        public bool ExplicitHatch { get; init; } = true;
        public string? Background { get; init; } = "#ffffff";
        /// <summary>Viewport; defaults to the page.</summary>
        public Rect? ViewBox { get; init; }
        /// <summary>Adds data-isk-id attributes for round-trip/hit-testing.</summary>
        public bool ObjectIds { get; init; } = true;
        public string FontFamily { get; init; } = "Arial, Helvetica, sans-serif";
    }

    /// <summary>Renders primitives/pages to standalone SVG (mm user units).</summary>
    public static class SvgRenderer
    {
        private static string F(double v) => Math.Round(v, 3).ToString("0.###", CultureInfo.InvariantCulture);

        public static string RenderPage(Registry registry, SketchDocument doc, Page page, SvgOptions? options = null, Lang lang = Lang.En)
        {
            options ??= new SvgOptions();
            var ctx = new RenderContext(doc, lang, options.Monochrome);
            var vb = options.ViewBox ?? new Rect(0, 0, page.Size.W, page.Size.H);
            var sb = new StringBuilder();
            sb.Append("<svg xmlns=\"http://www.w3.org/2000/svg\" version=\"1.1\" ")
              .Append("width=\"").Append(F(vb.W)).Append("mm\" height=\"").Append(F(vb.H)).Append("mm\" ")
              .Append("viewBox=\"").Append(F(vb.X)).Append(' ').Append(F(vb.Y)).Append(' ').Append(F(vb.W)).Append(' ').Append(F(vb.H)).Append("\">\n");
            if (options.Background is not null)
                sb.Append("<rect x=\"").Append(F(vb.X)).Append("\" y=\"").Append(F(vb.Y)).Append("\" width=\"").Append(F(vb.W)).Append("\" height=\"").Append(F(vb.H)).Append("\" fill=\"").Append(options.Background).Append("\"/>\n");
            foreach (var r in registry.RenderPage(doc, page, ctx))
            {
                sb.Append("<g");
                if (options.ObjectIds) sb.Append(" data-isk-id=\"").Append(Esc(r.Object.Id)).Append("\" data-isk-type=\"").Append(Esc(r.Object.Type)).Append('"');
                sb.Append(">\n");
                foreach (var p in r.Primitives) Write(sb, p, options, "  ");
                sb.Append("</g>\n");
            }
            sb.Append("</svg>\n");
            return sb.ToString();
        }

        /// <summary>Renders loose primitives (e.g. a library thumbnail) fitted into a viewBox.</summary>
        public static string RenderPrimitives(IReadOnlyList<Primitive> prims, SvgOptions? options = null, double padding = 2)
        {
            options ??= new SvgOptions { Background = null };
            var b = options.ViewBox ?? (Prim.Bounds(prims) ?? new Rect(0, 0, 10, 10)).Inflate(padding);
            var sb = new StringBuilder();
            sb.Append("<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"").Append(F(b.X)).Append(' ').Append(F(b.Y)).Append(' ').Append(F(b.W)).Append(' ').Append(F(b.H)).Append("\">\n");
            foreach (var p in prims) Write(sb, p, options, "");
            sb.Append("</svg>\n");
            return sb.ToString();
        }

        public static void Write(StringBuilder sb, Primitive p, SvgOptions o, string indent)
        {
            switch (p)
            {
                case GroupPrim g:
                    sb.Append(indent).Append("<g>\n");
                    foreach (var c in g.Children) Write(sb, c, o, indent + "  ");
                    sb.Append(indent).Append("</g>\n");
                    return;
                case TextPrim t:
                    WriteText(sb, t, o, indent);
                    return;
            }
            var style = p switch { LinePrim l => l.Style, PathPrim pa => pa.Style, EllipsePrim e => e.Style, RectPrim r => r.Style, _ => Style.Default };
            var fill = style.EffectiveFill;
            // 1) Hatch / dots below the outline.
            if (fill.Kind is FillKind.Hatch or FillKind.Dots && p is not LinePrim)
            {
                var contours = Hatching.Contours(p);
                if (fill.Kind == FillKind.Hatch && fill.Hatch is not null)
                {
                    if (fill.Color is not null && !o.Monochrome)
                        WriteShape(sb, p, style with { Stroke = "none", Fill = Fill.Solid(fill.Color) }, o, indent, includeArrows: false);
                    var color = Col(fill.Hatch.Color ?? style.EffectiveStroke, o);
                    var w = fill.Hatch.Width ?? Math.Max(0.18, style.EffectiveWidth * 0.5);
                    var segs = Hatching.Segments(contours, fill.Hatch);
                    if (segs.Count > 0)
                    {
                        sb.Append(indent).Append("<path fill=\"none\" stroke=\"").Append(color).Append("\" stroke-width=\"").Append(F(w)).Append("\" d=\"");
                        foreach (var (a, b) in segs) sb.Append('M').Append(F(a.X)).Append(' ').Append(F(a.Y)).Append('L').Append(F(b.X)).Append(' ').Append(F(b.Y));
                        sb.Append("\"/>\n");
                    }
                }
                else if (fill.Kind == FillKind.Dots)
                {
                    var spacing = fill.DotSpacing ?? 1.5;
                    var bounds = Rect.FromPoints(contours.SelectMany(c => c));
                    var color = Col(style.EffectiveStroke, o);
                    var row = 0;
                    for (var y = bounds.Y + spacing / 2; y < bounds.Bottom; y += spacing, row++)
                        for (var x = bounds.X + spacing / 2 + (row % 2) * spacing / 2; x < bounds.Right; x += spacing)
                        {
                            var pt = new Vec2(x, y);
                            var inside = contours.Count(c => Geo.PointInPolygon(pt, c)) % 2 == 1;
                            if (inside) sb.Append(indent).Append("<circle cx=\"").Append(F(x)).Append("\" cy=\"").Append(F(y)).Append("\" r=\"").Append(F(Math.Max(0.12, spacing * 0.12))).Append("\" fill=\"").Append(color).Append("\"/>\n");
                        }
                }
                WriteShape(sb, p, style with { Fill = Fill.None }, o, indent, includeArrows: true);
                return;
            }
            WriteShape(sb, p, style, o, indent, includeArrows: true);
        }

        private static void WriteShape(StringBuilder sb, Primitive p, Style style, SvgOptions o, string indent, bool includeArrows)
        {
            var stroke = style.HasStroke ? Col(style.EffectiveStroke, o) : "none";
            var width = style.EffectiveWidth;
            var fill = style.EffectiveFill;
            var fillAttr = fill.Kind == FillKind.Solid && fill.Color is not null ? MonoFill(fill.Color, o) : "none";
            var attrs = new StringBuilder();
            attrs.Append(" fill=\"").Append(fillAttr).Append("\" stroke=\"").Append(stroke).Append('"');
            if (stroke != "none")
            {
                attrs.Append(" stroke-width=\"").Append(F(width)).Append('"');
                var dash = Style.DashArray(style.EffectiveDash, width);
                if (dash is not null) attrs.Append(" stroke-dasharray=\"").Append(string.Join(" ", dash.Select(F))).Append('"');
                attrs.Append(" stroke-linecap=\"").Append(style.EffectiveDash == DashStyle.Solid ? "round" : "butt").Append("\" stroke-linejoin=\"round\"");
            }
            if (style.Opacity is double op && op < 1) attrs.Append(" opacity=\"").Append(F(op)).Append('"');

            switch (p)
            {
                case LinePrim l:
                    {
                        var (a, b, heads) = includeArrows ? WithArrows(l.A, l.B, l.A, l.B, style, o) : (l.A, l.B, new List<Primitive>());
                        sb.Append(indent).Append("<line x1=\"").Append(F(a.X)).Append("\" y1=\"").Append(F(a.Y)).Append("\" x2=\"").Append(F(b.X)).Append("\" y2=\"").Append(F(b.Y)).Append('"').Append(attrs).Append("/>\n");
                        foreach (var h in heads) Write(sb, h, o, indent);
                        break;
                    }
                case RectPrim r:
                    sb.Append(indent).Append("<rect x=\"").Append(F(r.X)).Append("\" y=\"").Append(F(r.Y)).Append("\" width=\"").Append(F(r.W)).Append("\" height=\"").Append(F(r.H)).Append('"');
                    if (r.Radius > 0) sb.Append(" rx=\"").Append(F(r.Radius)).Append('"');
                    sb.Append(attrs).Append("/>\n");
                    break;
                case EllipsePrim e:
                    sb.Append(indent).Append("<ellipse cx=\"").Append(F(e.Center.X)).Append("\" cy=\"").Append(F(e.Center.Y)).Append("\" rx=\"").Append(F(e.Rx)).Append("\" ry=\"").Append(F(e.Ry)).Append('"').Append(attrs).Append("/>\n");
                    break;
                case PathPrim path:
                    {
                        var segs = path.Segments.ToList();
                        var heads = new List<Primitive>();
                        if (includeArrows && !path.IsClosed && segs.Count >= 2 && (style.ArrowStart ?? ArrowHead.None) != ArrowHead.None | (style.ArrowEnd ?? ArrowHead.None) != ArrowHead.None)
                            heads = ApplyPathArrows(segs, style, o);
                        sb.Append(indent).Append("<path d=\"").Append(PathData(segs)).Append('"').Append(attrs).Append("/>\n");
                        foreach (var h in heads) Write(sb, h, o, indent);
                        break;
                    }
            }
        }

        private static (Vec2 A, Vec2 B, List<Primitive> Heads) WithArrows(Vec2 a, Vec2 b, Vec2 dirFromA, Vec2 dirFromB, Style style, SvgOptions o)
        {
            var heads = new List<Primitive>();
            var color = Col(style.EffectiveStroke, o);
            var size = style.ArrowSize ?? ArrowSize.Medium;
            var w = style.EffectiveWidth;
            if ((style.ArrowEnd ?? ArrowHead.None) != ArrowHead.None)
            {
                var dir = (b - dirFromA).Normalized();
                var (prims, inset) = ArrowHeads.Build(style.ArrowEnd!.Value, b, dir, size, color, w);
                heads.AddRange(prims);
                b -= dir * inset;
            }
            if ((style.ArrowStart ?? ArrowHead.None) != ArrowHead.None)
            {
                var dir = (a - dirFromB).Normalized();
                var (prims, inset) = ArrowHeads.Build(style.ArrowStart!.Value, a, dir, size, color, w);
                heads.AddRange(prims);
                a -= dir * inset;
            }
            return (a, b, heads);
        }

        private static List<Primitive> ApplyPathArrows(List<PathSeg> segs, Style style, SvgOptions o)
        {
            var heads = new List<Primitive>();
            var color = Col(style.EffectiveStroke, o);
            var size = style.ArrowSize ?? ArrowSize.Medium;
            var w = style.EffectiveWidth;
            if ((style.ArrowEnd ?? ArrowHead.None) != ArrowHead.None)
            {
                var last = segs[segs.Count - 1];
                var prev = segs[segs.Count - 2].P;
                var from = last.Op switch { SegOp.C => last.C2, SegOp.Q => last.C1, _ => prev };
                if ((last.P - from).Length < Geo.Eps) from = prev;
                var dir = (last.P - from).Normalized();
                var (prims, inset) = ArrowHeads.Build(style.ArrowEnd!.Value, last.P, dir, size, color, w);
                heads.AddRange(prims);
                segs[segs.Count - 1] = last with { P = last.P - dir * inset };
            }
            if ((style.ArrowStart ?? ArrowHead.None) != ArrowHead.None)
            {
                var first = segs[0];
                var next = segs[1];
                var to = next.Op is SegOp.C or SegOp.Q ? next.C1 : next.P;
                if ((to - first.P).Length < Geo.Eps) to = next.P;
                var dir = (first.P - to).Normalized();
                var (prims, inset) = ArrowHeads.Build(style.ArrowStart!.Value, first.P, dir, size, color, w);
                heads.AddRange(prims);
                segs[0] = first with { P = first.P - dir * inset };
            }
            return heads;
        }

        private static void WriteText(StringBuilder sb, TextPrim t, SvgOptions o, string indent)
        {
            var ts = t.TextStyle;
            var anchor = ts.Anchor switch { TextAnchor.Start => "start", TextAnchor.End => "end", _ => "middle" };
            var baseline = ts.Baseline switch { TextBaseline.Top => "hanging", TextBaseline.Bottom => "text-after-edge", TextBaseline.Alphabetic => "alphabetic", _ => "central" };
            var lines = t.Text.Replace("\r", "").Split('\n');
            var lineH = ts.Size * 1.2;
            var y0 = t.Position.Y - (lines.Length - 1) * lineH / 2;
            sb.Append(indent).Append("<text x=\"").Append(F(t.Position.X)).Append("\" y=\"").Append(F(y0)).Append('"')
              .Append(" font-family=\"").Append(Esc(ts.Font ?? o.FontFamily)).Append("\" font-size=\"").Append(F(ts.Size)).Append('"')
              .Append(" text-anchor=\"").Append(anchor).Append("\" dominant-baseline=\"").Append(baseline).Append('"')
              .Append(" fill=\"").Append(Col(ts.Color ?? "#000000", o)).Append('"');
            if (ts.Bold) sb.Append(" font-weight=\"bold\"");
            if (ts.Italic) sb.Append(" font-style=\"italic\"");
            if (Math.Abs(t.Rotation) > 1e-9) sb.Append(" transform=\"rotate(").Append(F(t.Rotation)).Append(' ').Append(F(t.Position.X)).Append(' ').Append(F(t.Position.Y)).Append(")\"");
            sb.Append('>');
            for (var i = 0; i < lines.Length; i++)
            {
                if (lines.Length == 1) { sb.Append(Esc(lines[i])); break; }
                sb.Append("<tspan x=\"").Append(F(t.Position.X)).Append("\" dy=\"").Append(i == 0 ? "0" : F(lineH)).Append("\">").Append(Esc(lines[i])).Append("</tspan>");
            }
            sb.Append("</text>\n");
        }

        public static string PathData(IEnumerable<PathSeg> segs)
        {
            var sb = new StringBuilder();
            foreach (var s in segs)
            {
                switch (s.Op)
                {
                    case SegOp.M: sb.Append('M').Append(F(s.P.X)).Append(' ').Append(F(s.P.Y)); break;
                    case SegOp.L: sb.Append('L').Append(F(s.P.X)).Append(' ').Append(F(s.P.Y)); break;
                    case SegOp.C: sb.Append('C').Append(F(s.C1.X)).Append(' ').Append(F(s.C1.Y)).Append(' ').Append(F(s.C2.X)).Append(' ').Append(F(s.C2.Y)).Append(' ').Append(F(s.P.X)).Append(' ').Append(F(s.P.Y)); break;
                    case SegOp.Q: sb.Append('Q').Append(F(s.C1.X)).Append(' ').Append(F(s.C1.Y)).Append(' ').Append(F(s.P.X)).Append(' ').Append(F(s.P.Y)); break;
                    case SegOp.Z: sb.Append('Z'); break;
                }
            }
            return sb.ToString();
        }

        private static string Col(string color, SvgOptions o) => o.Monochrome ? (IsWhite(color) ? "#ffffff" : "#000000") : color;

        private static string MonoFill(string color, SvgOptions o)
        {
            if (!o.Monochrome) return color;
            // Monochrome: dark fills stay black (e.g. arrowheads, filled dots), everything else becomes white.
            return Luminance(color) < 0.35 ? "#000000" : "#ffffff";
        }

        private static bool IsWhite(string c) => Luminance(c) > 0.97;

        public static double Luminance(string color)
        {
            var c = color.TrimStart('#');
            if (c.Length == 3) c = string.Concat(c.Select(ch => new string(ch, 2)));
            if (c.Length < 6 || !int.TryParse(c.Substring(0, 6), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v)) return 0;
            double r = (v >> 16) & 255, g = (v >> 8) & 255, b = v & 255;
            return (0.2126 * r + 0.7152 * g + 0.0722 * b) / 255;
        }

        public static string Esc(string s) => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
    }
}
