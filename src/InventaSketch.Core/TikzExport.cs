using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace InventaSketch.Core
{
    /// <summary>
    /// LaTeX/TikZ export of any drawing (flowcharts, block diagrams, sketches, charts): one <c>tikzpicture</c> in mm with
    /// y down (<c>x=1mm, y=-1mm</c>), one commented <c>scope</c> per element. Texts become TikZ nodes, so they are set in
    /// the document's font and stay editable and formattable in LaTeX; hatches become <c>patterns.meta</c> patterns,
    /// colours named <c>\definecolor</c>s. Arrowheads are drawn as explicit shapes, exactly as in the SVG export.
    /// The output is a <c>standalone</c> document; the <c>tikzpicture</c> can be copied into any document.
    /// </summary>
    public sealed class TikzExporter : IExporter
    {
        public string Id => "latex.tikz";
        public I18nText Name => new("LaTeX (TikZ)", "LaTeX (TikZ)");
        public string Extension => "tex";
        public string MimeType => "application/x-tex";
        public I18nText Preserves => new("Drawing as TikZ: texts editable in LaTeX (document font), lines, fills, hatches and colours",
            "Zeichnung als TikZ: Texte in LaTeX bearbeitbar (Dokumentschrift), Linien, Füllungen, Schraffuren und Farben");

        public bool Applies(Registry registry, SketchDocument doc, IReadOnlyList<SketchObject> objects) => objects.Count > 0;

        public string Export(Registry registry, SketchDocument doc, IReadOnlyList<SketchObject> objects, Lang lang)
        {
            var ctx = new RenderContext(doc, lang).For(registry);
            var w = new TikzWriter();
            foreach (var o in objects)
            {
                if (registry.Type(o.Type) is not { } type) continue;
                IReadOnlyList<Primitive> prims;
                try { prims = registry.Render(o, ctx); }
                catch (Exception) { continue; }
                w.Object(type.Name.Get(lang) + (string.IsNullOrWhiteSpace(o.Text) ? "" : ": " + o.Text!.Replace("\n", " ")), o.Id, prims);
            }
            return w.Document();
        }
    }

    /// <summary>Writes primitives (page mm, y down) as TikZ.</summary>
    public sealed class TikzWriter
    {
        private readonly StringBuilder _body = new();
        private readonly Dictionary<string, string> _colors = new(StringComparer.OrdinalIgnoreCase);

        private static string F(double v) => Math.Round(v, 3).ToString("0.###", CultureInfo.InvariantCulture);
        private static string P(Vec2 p) => "(" + F(p.X) + "," + F(p.Y) + ")";

        /// <summary>Adds one element as a commented scope.</summary>
        public void Object(string label, string id, IEnumerable<Primitive> prims)
        {
            _body.Append("  % ").Append(Comment(label)).Append(" [").Append(Comment(id)).Append("]\n");
            _body.Append("  \\begin{scope}\n");
            foreach (var p in prims) Write(p, "    ");
            _body.Append("  \\end{scope}\n");
        }

        /// <summary>The standalone document with colour definitions and the picture.</summary>
        public string Document()
        {
            var sb = new StringBuilder();
            sb.Append("% Exported by InventaSketch. Texts are TikZ nodes: edit and format them here; they use the document font.\n");
            sb.Append("% Copy the tikzpicture into your document (needs \\usepackage{tikz} and the libraries below).\n");
            sb.Append("\\documentclass[tikz,border=2mm]{standalone}\n\\usepackage[T1]{fontenc}\n\\usepackage{tikz}\n");
            sb.Append("\\usetikzlibrary{patterns,patterns.meta}\n");
            foreach (var kv in _colors) sb.Append("\\definecolor{").Append(kv.Value).Append("}{HTML}{").Append(kv.Key).Append("}\n");
            sb.Append("\\begin{document}\n\\begin{tikzpicture}[x=1mm, y=-1mm, line cap=round, line join=round]\n");
            sb.Append(_body);
            sb.Append("\\end{tikzpicture}\n\\end{document}\n");
            return sb.ToString();
        }

        private static string Comment(string s) => s.Replace("\r", " ").Replace("\n", " ");

        /// <summary>A named colour for a hex value ("#1f4e9a" → isk1F4E9A); null for none/unknown.</summary>
        private string? Color(string? hex)
        {
            if (string.IsNullOrEmpty(hex) || hex!.Equals("none", StringComparison.OrdinalIgnoreCase)) return null;
            var h = hex.TrimStart('#');
            if (h.Length == 3) h = string.Concat(h.Select(c => new string(c, 2)));
            if (h.Length != 6 || !h.All(Uri.IsHexDigit)) return "black";
            h = h.ToUpperInvariant();
            if (h == "000000") return "black";
            if (h == "FFFFFF") return "white";
            if (!_colors.TryGetValue(h, out var name)) _colors[h] = name = "isk" + h;
            return name;
        }

        private static string Dash(DashStyle d, double width)
        {
            var u = Math.Max(0.35, width) ;
            string On(double k) => F(k * u) + "mm";
            return d switch
            {
                DashStyle.Dash => "dash pattern=on " + On(8) + " off " + On(4),
                DashStyle.LongDash => "dash pattern=on " + On(16) + " off " + On(5),
                DashStyle.Dot => "dash pattern=on " + On(0.1) + " off " + On(3),
                DashStyle.DashDot => "dash pattern=on " + On(12) + " off " + On(3) + " on " + On(0.1) + " off " + On(3),
                DashStyle.DashDotDot => "dash pattern=on " + On(12) + " off " + On(3) + " on " + On(0.1) + " off " + On(3) + " on " + On(0.1) + " off " + On(3),
                _ => "",
            };
        }

        /// <summary>Draw options of a style: stroke colour, width, dash, fill (solid or pattern) and opacity.</summary>
        private string Options(Style s, bool closed)
        {
            var opts = new List<string>();
            if (s.HasStroke)
            {
                opts.Add("draw=" + (Color(s.EffectiveStroke) ?? "black"));
                opts.Add("line width=" + F(s.EffectiveWidth) + "mm");
                var dash = Dash(s.EffectiveDash, s.EffectiveWidth);
                if (dash.Length > 0) opts.Add(dash);
            }
            var fill = s.EffectiveFill;
            if (closed)
                switch (fill.Kind)
                {
                    case FillKind.Solid when Color(fill.Color) is { } fc:
                        opts.Add("fill=" + fc);
                        break;
                    case FillKind.Hatch when fill.Hatch is { } h:
                        {
                            if (Color(fill.Color) is { } bg) opts.Add("fill=" + bg);
                            var hc = Color(h.Color ?? s.EffectiveStroke) ?? "black";
                            var lw = F(h.Width ?? Math.Max(0.18, s.EffectiveWidth * 0.5)) + "mm";
                            var name = h.Cross ? "Hatch" : "Lines";
                            opts.Add("pattern={" + name + "[angle=" + F(h.Angle) + ", distance=" + F(h.Spacing) + "mm, line width=" + lw + "]}, pattern color=" + hc);
                            break;
                        }
                    case FillKind.Dots:
                        opts.Add("pattern={Dots[distance=" + F(fill.DotSpacing ?? 1.5) + "mm]}, pattern color=" + (Color(s.EffectiveStroke) ?? "black"));
                        break;
                }
            if (s.Opacity is double op && op < 1) opts.Add("opacity=" + F(op));
            return string.Join(", ", opts);
        }

        private void Write(Primitive p, string indent)
        {
            switch (p)
            {
                case GroupPrim g:
                    _body.Append(indent).Append("\\begin{scope}").Append(p.Tag is { } gt ? " % " + Comment(gt) : "").Append('\n');
                    foreach (var c in g.Children) Write(c, indent + "  ");
                    _body.Append(indent).Append("\\end{scope}\n");
                    return;
                case TextPrim t:
                    Text(t, indent);
                    return;
            }
            // Arrowheads as explicit shapes (crow's feet, open heads … look exactly as in the SVG export).
            var (shaft, heads) = ArrowHeads.Explode(p);
            switch (shaft)
            {
                case LinePrim l:
                    Path(indent, l.Style, false, P(l.A) + " -- " + P(l.B), p.Tag);
                    break;
                case RectPrim r:
                    {
                        var path = r.Radius > 0
                            ? "[rounded corners=" + F(r.Radius) + "mm] " + P(new Vec2(r.X, r.Y)) + " rectangle " + P(new Vec2(r.X + r.W, r.Y + r.H))
                            : P(new Vec2(r.X, r.Y)) + " rectangle " + P(new Vec2(r.X + r.W, r.Y + r.H));
                        Path(indent, r.Style, true, path, p.Tag);
                        break;
                    }
                case EllipsePrim e:
                    Path(indent, e.Style, true, P(e.Center) + " ellipse [x radius=" + F(e.Rx) + ", y radius=" + F(e.Ry) + "]", p.Tag);
                    break;
                case PathPrim path:
                    Path(indent, path.Style, path.IsClosed, Segments(path.Segments), p.Tag);
                    break;
            }
            foreach (var h in heads) Write(h, indent);
        }

        private void Path(string indent, Style s, bool closed, string path, string? tag)
        {
            var opts = Options(s, closed);
            if (opts.Length == 0) return;
            _body.Append(indent).Append("\\path[").Append(opts).Append("] ").Append(path).Append(';');
            if (tag is not null) _body.Append(" % ").Append(Comment(tag));
            _body.Append('\n');
        }

        private static string Segments(IReadOnlyList<PathSeg> segs)
        {
            var sb = new StringBuilder();
            var cur = default(Vec2);
            foreach (var s in segs)
            {
                switch (s.Op)
                {
                    case SegOp.M: if (sb.Length > 0) sb.Append(' '); sb.Append(P(s.P)); cur = s.P; break;
                    case SegOp.L: sb.Append(" -- ").Append(P(s.P)); cur = s.P; break;
                    case SegOp.C: sb.Append(" .. controls ").Append(P(s.C1)).Append(" and ").Append(P(s.C2)).Append(" .. ").Append(P(s.P)); cur = s.P; break;
                    case SegOp.Q:
                        {
                            var c1 = cur + (s.C1 - cur) * (2.0 / 3); var c2 = s.P + (s.C1 - s.P) * (2.0 / 3);
                            sb.Append(" .. controls ").Append(P(c1)).Append(" and ").Append(P(c2)).Append(" .. ").Append(P(s.P)); cur = s.P; break;
                        }
                    case SegOp.Z: sb.Append(" -- cycle"); break;
                }
            }
            return sb.ToString();
        }

        /// <summary>LaTeX-escaped text (special characters; umlauts stay UTF-8).</summary>
        public static string Escape(string text)
        {
            var sb = new StringBuilder();
            foreach (var c in text)
                sb.Append(c switch
                {
                    '\\' => "\\textbackslash{}",
                    '{' => "\\{",
                    '}' => "\\}",
                    '$' => "\\$",
                    '&' => "\\&",
                    '#' => "\\#",
                    '%' => "\\%",
                    '_' => "\\_",
                    '^' => "\\textasciicircum{}",
                    '~' => "\\textasciitilde{}",
                    '\u0394' => "$\\Delta$",
                    '\u00b0' => "\\textdegree{}",
                    _ => c.ToString(),
                });
            return sb.ToString();
        }

        private void Text(TextPrim t, string indent)
        {
            var ts = t.TextStyle;
            var lines = t.Text.Replace("\r", "").Split('\n');
            // Anchor like the SVG export: text-anchor start/middle/end, baseline middle/top/bottom.
            var h = ts.Anchor switch { TextAnchor.Start => "west", TextAnchor.End => "east", _ => "" };
            var v = ts.Baseline switch { TextBaseline.Top => "north", TextBaseline.Bottom or TextBaseline.Alphabetic => "south", _ => "" };
            var anchor = (v + " " + h).Trim();
            if (anchor.Length == 0) anchor = "center";
            var pt = ts.Size * 72 / 25.4;
            var font = "\\fontsize{" + F(pt) + "}{" + F(pt * 1.2) + "}\\selectfont";
            if (ts.Bold) font += "\\bfseries";
            if (ts.Italic) font += "\\itshape";
            var opts = new List<string> { "anchor=" + anchor, "inner sep=0pt", "font=" + font };
            if (lines.Length > 1) opts.Add("align=" + (ts.Anchor switch { TextAnchor.Start => "left", TextAnchor.End => "right", _ => "center" }));
            if (Color(ts.Color) is { } c && c != "black") opts.Add("text=" + c);
            // Screen rotation is clockwise; TikZ rotates counter-clockwise on paper.
            if (Math.Abs(t.Rotation) > 1e-9) opts.Add("rotate=" + F(-t.Rotation));
            var body = string.Join("\\\\ ", lines.Select(l => ts.Underline ? "\\underline{" + Escape(l) + "}" : Escape(l)));
            _body.Append(indent).Append("\\node[").Append(string.Join(", ", opts)).Append("] at ").Append(P(t.Position)).Append(" {").Append(body).Append("};");
            if (t.Tag is not null) _body.Append(" % ").Append(Comment(t.Tag));
            _body.Append('\n');
        }
    }
}
