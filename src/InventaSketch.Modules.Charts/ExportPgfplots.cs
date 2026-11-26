using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using InventaSketch.Core;
using static InventaSketch.Modules.Charts.ExportText;

namespace InventaSketch.Modules.Charts
{
    /// <summary>
    /// Export of the selected charts as a standalone LaTeX document with pgfplots (charts v2 stage 4): one tikzpicture per
    /// chart (a page each) with an axis, or a groupplot for stacked panels; data inline as coordinates. Every text (axis
    /// titles, tick marks, legend, titles, notes, numerals) is LaTeX text set in the document font and stays editable.
    /// </summary>
    internal sealed class PgfplotsExporter : IExporter
    {
        public string Id => "chart.pgfplots";
        public I18nText Name => new("Charts as LaTeX (pgfplots)", "Diagramme als LaTeX (pgfplots)");
        public string Extension => "tex";
        public string MimeType => "application/x-tex";
        public I18nText Preserves => new(
            "Charts as pgfplots axes: data, axes, scales, line styles, colours, markers, hatched regions, value lines, notes and numerals; every text is LaTeX text in the document font",
            "Diagramme als pgfplots-Achsen: Daten, Achsen, Skalen, Linienarten, Farben, Marker, schraffierte Flächen, Wertlinien, Anmerkungen und Bezugszeichen; alle Texte sind LaTeX-Text in der Dokumentschrift");

        public bool Applies(Registry registry, SketchDocument doc, IReadOnlyList<SketchObject> objects) => ChartExport.Charts(registry, objects).Any();

        public string Export(Registry registry, SketchDocument doc, IReadOnlyList<SketchObject> objects, Lang lang)
            => PgfplotsWriter.Write(ChartExport.Figures(registry, doc, objects, lang), lang);
    }

    /// <summary>Writes export models as a standalone pgfplots document.</summary>
    internal static class PgfplotsWriter
    {
        public static string Write(IReadOnlyList<(SketchObject Object, XFigure? Figure, string? Error)> charts, Lang lang)
        {
            var colors = new SortedDictionary<string, string>(StringComparer.Ordinal);
            var body = new StringBuilder();
            var k = 0;
            foreach (var (o, fig, error) in charts)
            {
                k++;
                if (fig is null)
                {
                    body.Append("% Chart ").Append(Fmt.I(k)).Append(" [").Append(Comment(o.Id)).Append("] could not be exported: ").Append(Comment(error ?? "")).Append('\n');
                    continue;
                }
                new Pic(body, fig, colors).Write(k);
            }
            var sb = new StringBuilder();
            sb.Append(lang == Lang.De
                ? "% Diagramme aus InventaSketch als pgfplots (pdfLaTeX/LuaLaTeX). Jedes Diagramm ist ein tikzpicture (eine Seite);\n% alle Texte sind LaTeX-Text in der Dokumentschrift und können hier bearbeitet werden.\n"
                : "% Charts exported from InventaSketch as pgfplots (pdfLaTeX/LuaLaTeX). Every chart is one tikzpicture (one page);\n% every text is LaTeX text in the document font and can be edited here.\n");
            sb.Append("\\documentclass[tikz,border=2mm]{standalone}\n");
            sb.Append("\\usepackage[T1]{fontenc}\n\\usepackage[utf8]{inputenc}\n\\usepackage{textcomp}\n\\usepackage{pgfplots}\n");
            // Polar axes and box plots/histograms need their libraries (only then, so other documents stay as they were).
            var figures = charts.Select(c => c.Figure).Where(f => f is not null).ToList();
            var libs = "fillbetween,groupplots"
                + (figures.Any(f => f!.Polar is not null) ? ",polar" : "")
                + (figures.Any(f => f!.Boxes is not null || f.Hist is not null) ? ",statistics" : "");
            sb.Append("\\pgfplotsset{compat=1.18}\n\\usepgfplotslibrary{" + libs + "}\n\\usetikzlibrary{arrows.meta,patterns.meta}\n");
            sb.Append("\\pgfplotsset{every axis/.append style={font=\\footnotesize}, every axis legend/.append style={font=\\footnotesize}}\n");
            // Data keys: they draw nothing and keep the chart data that the drawing does not show exactly (values of pie
            // slices and Sankey flows, raw samples of box plots) readable for an import of this document.
            var keys = new List<string>();
            if (figures.Any(f => f!.Pie is not null)) { keys.Add("pie chart/.style={}"); keys.Add("pie slice/.style 2 args={}"); }
            if (figures.Any(f => f!.Sankey is not null)) { keys.Add("sankey chart/.style={}"); keys.Add("sankey flow/.style n args={3}{}"); }
            if (figures.Any(f => f!.Boxes is not null)) keys.Add("box samples/.style={}");
            if (keys.Count > 0)
                sb.Append("% data keys (they draw nothing; they keep the chart data for an import)\n\\tikzset{").Append(string.Join(", ", keys)).Append("}\n");
            foreach (var c in colors) sb.Append("\\definecolor{").Append(c.Value).Append("}{HTML}{").Append(c.Key).Append("}\n");
            sb.Append("\\begin{document}\n");
            sb.Append(body);
            sb.Append("\\end{document}\n");
            return sb.ToString();
        }

        private static string Comment(string s) => s.Replace("\r", " ").Replace("\n", " ").Replace("%", "\\%");

        /// <summary>Writer of one tikzpicture.</summary>
        private sealed class Pic
        {
            private readonly StringBuilder _sb;
            private readonly XFigure _f;
            private readonly SortedDictionary<string, string> _colors;
            private readonly List<string> _after = new();
            private int _names;

            public Pic(StringBuilder sb, XFigure f, SortedDictionary<string, string> colors) { _sb = sb; _f = f; _colors = colors; }

            private void L(string line) => _sb.Append(line).Append('\n');

            private string Name(string prefix) => prefix + Fmt.I(++_names);

            /// <summary>Colour name of a #rrggbb colour (defined in the preamble).</summary>
            private string C(string color)
            {
                var c = (color ?? "#000000").Trim().TrimStart('#');
                if (c.Length == 3) c = new string(new[] { c[0], c[0], c[1], c[1], c[2], c[2] });
                if (c.Length != 6 || !c.All(Uri.IsHexDigit)) c = "000000";
                c = c.ToUpperInvariant();
                if (!_colors.TryGetValue(c, out var name)) _colors[c] = name = "c" + c.ToLowerInvariant();
                return name;
            }

            private static string Mm(double v) => N(Math.Round(v, 3)) + "mm";

            private static string Dash(DashStyle d) => d switch
            {
                DashStyle.Dash => "dashed",
                DashStyle.Dot => "dotted",
                DashStyle.DashDot => "dash dot",
                DashStyle.LongDash => "dash pattern=on 6pt off 2pt",
                DashStyle.DashDotDot => "dash dot dot",
                _ => "solid",
            };

            private string StrokeOpts(XStroke s) => C(s.Color) + ", line width=" + Mm(s.Width) + ", " + Dash(s.Dash) + (s.Opacity is { } o ? ", opacity=" + N(o) : "");

            private static string Mark(XMarker m) => m.Kind switch
            {
                "square" => m.Filled ? "square*" : "square",
                "triangle" => m.Filled ? "triangle*" : "triangle",
                "diamond" => m.Filled ? "diamond*" : "diamond",
                "cross" => "x",
                "plus" => "+",
                _ => m.Filled ? "*" : "o",
            };

            private string FillOpts(XFillStyle s)
            {
                var d = Mm(Math.Max(0.3, s.Spacing));
                var op = s.Opacity is { } o ? ", fill opacity=" + N(o) : "";
                if (s.Edge is { } edge)
                {
                    // The same fill with its edge line instead of "draw=none".
                    var plain = FillOpts(s with { Edge = null });
                    return plain.Replace(", draw=none", "") + ", draw=" + C(edge.Color) + ", line width=" + Mm(edge.Width) + ", " + Dash(edge.Dash);
                }
                if (s.Kind == "pattern") return PatternOpts(s, op);
                return s.Kind switch
                {
                    "solid" => "fill=" + C(s.Color) + ", draw=none" + op,
                    "none" => "fill=none, draw=none",
                    "dots" => "pattern={Dots[distance=" + d + ", radius=0.15mm]}, pattern color=" + C(s.Color) + ", draw=none" + op,
                    "cross" => "pattern={Hatch[angle=" + N(s.Angle) + ", distance=" + d + ", line width=0.15mm]}, pattern color=" + C(s.Color) + ", draw=none" + op,
                    _ => "pattern={Lines[angle=" + N(s.Angle) + ", distance=" + d + ", line width=0.15mm]}, pattern color=" + C(s.Color) + ", draw=none" + op,
                };
            }

            /// <summary>
            /// A matplotlib hatch pattern as a TikZ pattern (patterns.meta): one line family "Lines", two crossing families
            /// "Hatch", dots and circles "Dots", stars "Stars"; the densest part of the pattern is taken (TikZ fills one pattern
            /// per path). A solid face becomes the fill colour under the pattern ("postaction").
            /// </summary>
            private string PatternOpts(XFillStyle s, string op)
            {
                var p = AreaPattern.Parse(s.HatchCode);
                var unit = s.Spacing * Math.Max(1, p.Lines.Count > 0 ? p.Lines[0].Count : 1);
                string pattern;
                if (p.Lines.Count >= 2)
                {
                    var (a, c) = p.Lines.OrderByDescending(l => l.Count).First();
                    pattern = "Hatch[angle=" + N(a) + ", distance=" + Mm(Math.Max(0.3, AreaPattern.LineSpacing(unit, c))) + ", line width=0.15mm]";
                }
                else if (p.Lines.Count == 1)
                    pattern = "Lines[angle=" + N(p.Lines[0].Angle) + ", distance=" + Mm(Math.Max(0.3, AreaPattern.LineSpacing(unit, p.Lines[0].Count))) + ", line width=0.15mm]";
                else if (p.Shapes.Count > 0)
                {
                    var (shape, c) = p.Shapes[0];
                    var dist = Math.Max(0.3, AreaPattern.ShapeSpacing(unit, c));
                    pattern = shape == '*'
                        ? "Stars[distance=" + Mm(dist) + ", radius=" + Mm(Math.Max(0.1, AreaPattern.ShapeSize(shape) * dist * 0.7)) + "]"
                        : "Dots[distance=" + Mm(dist) + ", radius=" + Mm(Math.Max(0.08, AreaPattern.ShapeSize(shape) * dist / 2)) + "]";
                }
                else pattern = "Lines[angle=45, distance=" + Mm(Math.Max(0.3, unit)) + ", line width=0.15mm]";
                var fill = s.Face is { } face
                    ? "fill=" + C(face) + ", postaction={pattern={" + pattern + "}, pattern color=" + C(s.Color) + "}"
                    : "pattern={" + pattern + "}, pattern color=" + C(s.Color);
                return fill + ", draw=none" + op;
            }

            private static string Coords(IReadOnlyList<double> xs, IReadOnlyList<double> ys)
            {
                var sb = new StringBuilder();
                var line = 0;
                for (var i = 0; i < Math.Min(xs.Count, ys.Count); i++)
                {
                    var c = "(" + N(xs[i]) + "," + N(ys[i]) + ")";
                    if (line + c.Length > 100) { sb.Append("\n  "); line = 2; }
                    else if (i > 0) { sb.Append(' '); line++; }
                    sb.Append(c); line += c.Length;
                }
                return sb.ToString();
            }

            private double XAt(double v, bool frac) => frac ? _f.X.At(v) : v;

            private double YAt(int panel, double v, bool frac) => frac ? _f.Panels[Clamp(panel)].Y.At(v) : v;

            private int Clamp(int p) => Math.Max(0, Math.Min(_f.Panels.Count - 1, p));

            private static string P(double x, double y) => "(axis cs:" + N(x) + "," + N(y) + ")";

            private static string Font(double size, bool bold, bool italic)
            {
                var f = size >= 1.15 ? "\\small" : size >= 0.92 ? "" : size >= 0.68 ? "\\scriptsize" : "\\tiny";
                if (bold) f += "\\bfseries";
                if (italic) f += "\\itshape";
                return f;
            }

            private static string Anchor(string ha, string va)
            {
                var v = va switch { "top" => "north", "bottom" => "south", _ => "" };
                var h = ha switch { "left" => "west", "right" => "east", _ => "" };
                return v.Length == 0 && h.Length == 0 ? "center" : (v + " " + h).Trim();
            }

            // ------------------------------------------------------------------ picture

            public void Write(int number)
            {
                var f = _f;
                var n = f.Panels.Count;
                L("% --- Chart " + Fmt.I(number) + ": " + Comment(f.Name) + " [" + Comment(f.Id) + "]");
                foreach (var note in f.Notes) L("% Note: " + Comment(note));
                if (f.Pie is { } pie) { Pie(pie); return; }
                if (f.Polar is { } polar) { Polar(polar); return; }
                if (f.Sankey is { } sankey) { Sankey(sankey); return; }
                L("\\begin{tikzpicture}");
                var common = CommonOptions();
                if (n > 1)
                {
                    var gap = Math.Max(2, f.Panels.Take(n - 1).Average(p => p.GapMm));
                    L("\\begin{groupplot}[group style={group size=1 by " + Fmt.I(n) + ", vertical sep=" + Mm(Math.Round(gap, 1)) + ", xlabels at=edge bottom, xticklabels at=edge bottom},");
                    WriteOptions(common);
                    L("]");
                    for (var i = 0; i < n; i++)
                    {
                        L("% panel " + Fmt.I(i + 1));
                        L("\\nextgroupplot[");
                        WriteOptions(PanelOptions(i));
                        L("]");
                        Items(i);
                    }
                    L("\\end{groupplot}");
                }
                else
                {
                    L("\\begin{axis}[");
                    WriteOptions(common.Concat(PanelOptions(0)).ToList());
                    L("]");
                    Items(0);
                    if (f.Panels[0].Y2 is not null)
                        foreach (var s in f.Items.OfType<XSeries>().Where(s => s.Y2 && s.Legend is not null && f.Legend != "none"))
                        {
                            L("\\addlegendimage{" + SeriesOptions(s) + "}");
                            L("\\addlegendentry{" + Tex(s.Legend!) + "}");
                        }
                    L("\\end{axis}");
                    if (f.Panels[0].Y2 is { } y2) SecondaryAxis(y2);
                }
                foreach (var a in _after) L(a);
                if (f.Caption is { } caption) L("\\node[anchor=north, inner sep=2pt] at (current bounding box.south) {" + Tex(caption) + "}; % caption");
                L("\\end{tikzpicture}");
                L("");
            }

            private void WriteOptions(IReadOnlyList<string> opts)
            {
                for (var i = 0; i < opts.Count; i++) L("  " + opts[i] + (i + 1 < opts.Count ? "," : ""));
            }

            private List<string> CommonOptions()
            {
                var f = _f;
                var o = new List<string> { "scale only axis", "width=" + Mm(f.Panels[0].WidthMm > 1 ? f.Panels[0].WidthMm : f.WidthMm * 0.8) };
                o.AddRange(AxisOptions(f.X, "x"));
                o.Add("clip mode=individual");
                o.Add("unbounded coords=jump");
                o.Add("scaled ticks=false");
                o.Add("tick align=outside");
                if (f.Lang == Lang.De) o.Add("/pgf/number format/use comma");
                if (!f.Box) o.Add("axis x line=bottom");
                o.Add("x axis line style={" + (f.X.LineHidden ? "draw=none" : f.X.Arrow && !f.Box ? "-{Stealth[length=2mm]}" : "-") + "}");
                o.Add("major grid style={" + StrokeOpts(f.GridStroke) + "}");
                o.Add("minor grid style={" + StrokeOpts(f.MinorGridStroke) + "}");
                if (f.Bars is { } b)
                {
                    o.Add(b.Horizontal ? (b.Stacked ? "xbar stacked" : "xbar") : (b.Stacked ? "ybar stacked" : "ybar"));
                    o.Add("bar width=" + N(Math.Round(b.Width, 4)));
                }
                return o;
            }

            private List<string> AxisOptions(XAxis a, string d)
            {
                var o = new List<string>();
                if (a.IsLog) { o.Add(d + "mode=log"); o.Add("log ticks with fixed point"); }
                if (a.IsCategory)
                {
                    o.Add(d + "min=" + N(a.Min)); o.Add(d + "max=" + N(a.Max));
                    o.Add(d + "tick={" + string.Join(",", Enumerable.Range(0, a.Categories.Count).Select(i => Fmt.I(i))) + "}");
                    o.Add(d + "ticklabels={" + string.Join(",", a.Categories.Select(c => "{" + Tex(c) + "}")) + "}");
                    o.Add(d + "tick style={draw=none}");
                    if (a.Reversed) o.Add(d + " dir=reverse");
                }
                else
                {
                    o.Add(d + "min=" + N(a.Min)); o.Add(d + "max=" + N(a.Max));
                    if (a.Marks is { } marks)
                    {
                        if (marks.Count == 0) o.Add(d + "tick=\\empty");
                        else
                        {
                            o.Add(d + "tick={" + string.Join(",", marks.Select(m => N(m.At))) + "}");
                            o.Add(d + "ticklabels={" + string.Join(",", marks.Select(m => "{" + Tex(m.Text) + "}")) + "}");
                        }
                        o.Add("minor " + d + "tick={}");
                    }
                    else if (!a.IsLog && a.Step > 0) o.Add(d + "tick distance=" + N(a.Step));
                }
                if (a.Title is { } t) o.Add(d + "label={" + Tex(t) + "}");
                if (a.GridMajor) o.Add(d + "majorgrids");
                if (a.GridMinor) o.Add(d + "minorgrids");
                if (a.TicksHidden) o.Add(d + "tick style={draw=none}");
                if (a.TickLabelsHidden) o.Add(d + "ticklabel={}");
                return o;
            }

            private List<string> PanelOptions(int i)
            {
                var f = _f;
                var p = f.Panels[i];
                var o = new List<string>();
                if (f.Panels.Count > 1) o.Add("height=" + Mm(p.HeightMm));
                else o.Add("height=" + Mm(p.HeightMm > 1 ? p.HeightMm : f.HeightMm * 0.7));
                o.AddRange(AxisOptions(p.Y, "y"));
                if (!f.Box) o.Add("axis y line=left");
                o.Add("y axis line style={" + (p.Y.LineHidden ? "draw=none" : p.Y.Arrow && !f.Box ? "-{Stealth[length=2mm]}" : "-") + "}");
                if (i == 0 && f.Title is { } title) o.Add("title={\\bfseries " + Tex(title) + "}");
                if (f.Legend != "none" && HasLegend(i))
                {
                    o.Add(f.Legend == "right" ? "legend pos=outer north east" : "legend pos=" + (f.LegendCorner.StartsWith("lower", StringComparison.Ordinal) ? "south" : "north")
                        + " " + (f.LegendCorner.EndsWith("left", StringComparison.Ordinal) ? "west" : "east"));
                    o.Add("legend cell align=left");
                    o.Add("legend style={draw=" + C(Palette.LegendBorder) + ", fill=white}");
                }
                return o;
            }

            private bool HasLegend(int i)
                => _f.Items.Any(x => x.Panel == i && (x is XSeries { Legend: not null } || x is XFill { Legend: not null }))
                   || i == 0 && _f.Bars is { } b && b.Series.Any(s => s.InLegend)
                   || i == 0 && (_f.Hist?.Series.Any(s => s.InLegend) == true || _f.Boxes?.Series.Any(s => s.InLegend) == true || _f.Gantt?.Groups.Any(g => g.InLegend) == true);

            private void SecondaryAxis(XAxis y2)
            {
                var f = _f;
                L("% secondary y axis (right)");
                L("\\begin{axis}[");
                var o = new List<string> { "scale only axis", "width=" + Mm(f.Panels[0].WidthMm), "height=" + Mm(f.Panels[0].HeightMm), "xmin=" + N(f.X.Min), "xmax=" + N(f.X.Max) };
                if (f.X.IsLog) o.Add("xmode=log");
                o.Add("axis x line=none");
                o.Add("axis y line=right");
                o.Add("y axis line style={-}");
                o.Add("clip mode=individual");
                o.Add("unbounded coords=jump");
                o.Add("scaled ticks=false");
                o.Add("tick align=outside");
                if (f.Lang == Lang.De) o.Add("/pgf/number format/use comma");
                o.AddRange(AxisOptions(new XAxis
                {
                    Kind = y2.Kind, Min = y2.Min, Max = y2.Max, Step = y2.Step, Title = y2.Title, Marks = y2.Marks, TicksHidden = y2.TicksHidden, TickLabelsHidden = y2.TickLabelsHidden,
                }, "y"));
                WriteOptions(o);
                L("]");
                foreach (var s in f.Items.OfType<XSeries>().Where(s => s.Y2)) Series(s, forceForget: true);
                L("\\end{axis}");
            }

            // ------------------------------------------------------------------ items

            private void Items(int panel)
            {
                var f = _f;
                var single = f.Panels.Count == 1;
                if (panel == 0 && f.Bars is { } bars) Bars(bars);
                if (panel == 0 && f.Hist is { } hist) Hist(hist);
                if (panel == 0 && f.Boxes is { } boxes) Boxes(boxes);
                if (panel == 0 && f.Gantt is { } gantt) Gantt(gantt);
                foreach (var item in f.Items)
                {
                    switch (item)
                    {
                        case XVLine v when v.LastPanel > v.Panel:
                            if (v.Panel == panel || v.LastPanel == panel) VLine(v, panel);
                            continue;
                        case XSegment g when g.Panel2 != g.Panel:
                            if (g.Panel == panel || g.Panel2 == panel) Segment(g, panel);
                            continue;
                        case XFill { Kind: "span" } s:
                            if (s.Panels.DefaultIfEmpty(s.Panel).Contains(panel)) Span(s, panel);
                            continue;
                        case XFill { Kind: "hspan" } h:
                            if (h.Panels.DefaultIfEmpty(h.Panel).Contains(panel))
                            {
                                L("% " + h.Key + ": a band between two y values");
                                L("\\fill[" + FillOpts(h.Fill) + "] " + P(h.X0, h.Y0) + " rectangle " + P(h.X1, h.Y1) + ";");
                            }
                            continue;
                        case XSeries { Y2: true } when single && f.Panels[0].Y2 is not null:
                            continue;
                    }
                    if (item.Panel != panel) continue;
                    switch (item)
                    {
                        case XSeries s: Series(s); break;
                        case XHLine h:
                            L("% " + h.Key);
                            L("\\draw[" + StrokeOpts(h.Stroke) + "] " + P(f.X.Min, h.Y) + " -- " + P(f.X.Max, h.Y) + ";");
                            break;
                        case XVLine v: VLine(v, panel); break;
                        case XFill r: Between(r); break;
                        case XLabel l: L("% " + l.Key); Label(l); break;
                        case XLeader d: Leader(d); break;
                        case XSegment g: Segment(g, panel); break;
                        case XContours c: Contours(c); break;
                    }
                }
                foreach (var t in f.PanelTitles.Where(t => t.Panel == panel)) { L("% " + t.Key); Label(t); }
            }

            private string SeriesOptions(XSeries s)
            {
                var o = new List<string>();
                if (s.Line is { } line) o.Add(StrokeOpts(line));
                else { o.Add("only marks"); if (s.Marker is { } mm) o.Add(C(mm.Color)); }
                if (s.Marker is { } m)
                    o.Add("mark=" + Mark(m) + ", mark size=" + Mm(m.Size) + ", mark options={solid, draw=" + C(m.Color) + ", fill=" + (m.Filled ? C(m.Color) : "white") + "}");
                else o.Add("no marks");
                if (s.Step) o.Add("const plot");
                return string.Join(", ", o);
            }

            private void Series(XSeries s, bool forceForget = false)
            {
                L("% " + s.Key + (s.Legend is { } lg ? ": " + Comment(lg) : ""));
                var legend = s.Legend is not null && _f.Legend != "none" && !forceForget;
                L("\\addplot[" + SeriesOptions(s) + (legend ? "" : ", forget plot") + "] coordinates {");
                L("  " + Coords(s.X, s.Y));
                L("};");
                if (legend) L("\\addlegendentry{" + Tex(s.Legend!) + "}");
            }

            private void VLine(XVLine v, int panel)
            {
                var f = _f;
                L("% " + v.Key);
                if (v.LastPanel <= v.Panel)
                {
                    var y = f.Panels[Clamp(v.Panel)].Y;
                    L("\\draw[" + StrokeOpts(v.Stroke) + "] " + P(v.X, y.Min) + " -- " + P(v.X, y.Max) + ";");
                    return;
                }
                // Through several panels: one coordinate at the top of the first and one at the bottom of the last panel,
                // the line drawn after the group.
                var id = "vline" + Fmt.I(f.Items.IndexOf(v));
                if (panel == v.Panel) L("\\coordinate (" + id + "a) at " + P(v.X, f.Panels[Clamp(v.Panel)].Y.Max) + "; % top of a vertical marker through the panels");
                if (panel == v.LastPanel)
                {
                    L("\\coordinate (" + id + "b) at " + P(v.X, f.Panels[Clamp(v.LastPanel)].Y.Min) + ";");
                    _after.Add("\\draw[" + StrokeOpts(v.Stroke) + "] (" + id + "a) -- (" + id + "b); % " + v.Key + ": vertical marker through the panels");
                }
            }

            private void Span(XFill s, int panel)
            {
                var y = _f.Panels[Clamp(panel)].Y;
                L("% " + s.Key);
                L("\\fill[" + FillOpts(s.Fill) + "] " + P(s.X0, y.Min) + " rectangle " + P(s.X1, y.Max) + ";");
            }

            private void Between(XFill r)
            {
                L("% " + r.Key + (r.Legend is { } lg ? ": " + Comment(lg) : ""));
                // "fill between" for plain areas between two edges over x; steps, fill_betweenx and where-conditions as the
                // filled polygons (computed exactly like the drawing).
                var plain = r.Kind == "between" && r.Step is null && r.Where is null && r.X.Count >= 2 && r.X.Concat(r.Lo).Concat(r.Hi).All(Fmt.Finite);
                if (plain)
                {
                    var a = Name("path"); var b = Name("path");
                    L("\\addplot[draw=none, forget plot, name path=" + a + "] coordinates {");
                    L("  " + Coords(r.X, r.Lo));
                    L("};");
                    L("\\addplot[draw=none, forget plot, name path=" + b + "] coordinates {");
                    L("  " + Coords(r.X, r.Hi));
                    L("};");
                    var legend = r.Legend is not null && _f.Legend != "none";
                    L("\\addplot[" + FillOpts(r.Fill) + (legend ? ", area legend" : ", forget plot") + "] fill between[of=" + a + " and " + b + "];");
                    if (legend) L("\\addlegendentry{" + Tex(r.Legend!) + "}");
                    return;
                }
                if (r.WhereText is { } wt) L("% only where " + wt + ": the filled areas, computed from both curves");
                foreach (var poly in r.Polygons.Where(p => p.Count >= 3))
                {
                    var sb = new StringBuilder("\\fill[" + FillOpts(r.Fill) + "] ");
                    var line = sb.Length;
                    for (var i = 0; i < poly.Count; i++)
                    {
                        var c = P(poly[i].X, poly[i].Y) + " -- ";
                        if (line + c.Length > 110) { sb.Append("\n  "); line = 2; }
                        sb.Append(c); line += c.Length;
                    }
                    L(sb.Append("cycle;").ToString());
                }
            }

            private void Label(XLabel l)
            {
                var o = new List<string> { "anchor=" + Anchor(l.HAlign, l.VAlign) };
                if (Math.Abs(l.Dx) > 1e-9) o.Add("xshift=" + N(Math.Round(l.Dx, 2)) + "pt");
                if (Math.Abs(l.Dy) > 1e-9) o.Add("yshift=" + N(Math.Round(l.Dy, 2)) + "pt");
                var font = Font(l.Size, l.Bold, l.Italic);
                if (font.Length > 0) o.Add("font=" + font);
                o.Add("text=" + C(l.Color));
                if (l.Box) o.Add("fill=white");
                if (l.Text.IndexOf('\n') >= 0) o.Add("align=" + l.HAlign);
                o.Add("inner sep=1pt");
                L("\\node[" + string.Join(", ", o) + "] at " + P(XAt(l.X, l.XFrac), YAt(l.Panel, l.Y, l.YFrac)) + " {" + Tex(l.Text) + "};");
            }

            private void Leader(XLeader d)
            {
                var id = Name("numeral");
                var target = P(XAt(d.X, d.XFrac), YAt(d.Panel, d.Y, d.YFrac));
                L("% " + d.Key + ": reference numeral with leader line");
                L("\\node[inner sep=1pt, text=" + C(d.Color) + ", xshift=" + N(Math.Round(d.Dx, 3)) + "em, yshift=" + N(Math.Round(d.Dy, 3)) + "em] (" + id + ") at " + target + " {" + Tex(d.Text) + "};");
                L("\\draw[" + C(d.Color) + ", line width=0.2mm] (" + id + ") -- " + target + ";");
            }

            private string Arrows(string kind) => kind switch
            {
                "arrow" => ", -{Stealth[length=1.6mm]}",
                "dim" => ", {Stealth[length=1.6mm]}-{Stealth[length=1.6mm]}",
                _ => "",
            };

            private void Segment(XSegment g, int panel)
            {
                var what = g.Kind == "dim" ? ": dimension arrow" : g.Kind == "arrow" ? ": arrow" : ": line";
                if (g.Panel2 == g.Panel)
                {
                    L("% " + g.Key + what);
                    L("\\draw[" + StrokeOpts(g.Stroke) + Arrows(g.Kind) + "] " + P(g.A.X, g.A.Y) + " -- " + P(g.B.X, g.B.Y) + ";");
                    return;
                }
                var id = "seg" + Fmt.I(_f.Items.IndexOf(g));
                if (panel == g.Panel) L("\\coordinate (" + id + "a) at " + P(g.A.X, g.A.Y) + "; % " + g.Key);
                if (panel == g.Panel2) L("\\coordinate (" + id + "b) at " + P(g.B.X, g.B.Y) + "; % " + g.Key);
                if (panel == Math.Max(g.Panel, g.Panel2)) _after.Add("\\draw[" + StrokeOpts(g.Stroke) + Arrows(g.Kind) + "] (" + id + "a) -- (" + id + "b); % " + g.Key + what);
            }

            private void Contours(XContours c)
            {
                L("% " + c.Key + ": contour lines (traced from the field)");
                foreach (var g in c.Pieces.GroupBy(p => p.Level))
                {
                    L("% level " + N(g.Key));
                    foreach (var (_, pts) in g)
                    {
                        L("\\addplot[" + StrokeOpts(c.Stroke) + ", no marks, forget plot] coordinates {");
                        L("  " + Coords(pts.Select(p => p.X).ToList(), pts.Select(p => p.Y).ToList()));
                        L("};");
                    }
                }
                if (c.Labels)
                    foreach (var t in c.LabelTexts) { L("% " + t.Key); Label(t); }
            }

            private void Bars(XBars bars)
            {
                L("% bars: category i is centred at i");
                foreach (var s in bars.Series)
                {
                    L("% " + s.Key + ": " + Comment(s.Name));
                    var o = new List<string>();
                    o.Add(s.Fill.Kind == "solid" ? "fill=" + C(s.Fill.Color) : "fill=white, " + FillOpts(s.Fill).Replace(", draw=none", ""));
                    o.Add("draw=" + C(s.Fill.Kind == "solid" || s.Fill.Kind == "none" ? s.Edge : s.Fill.Color));
                    o.Add("line width=" + Mm(s.EdgeWidth));
                    if (!bars.Stacked) o.Add("bar shift=0pt");
                    if (s.Labels is not null) o.Add("nodes near coords, point meta=explicit symbolic, every node near coord/.append style={font=\\scriptsize}");
                    if (!s.InLegend || _f.Legend == "none") o.Add("forget plot");
                    else o.Add("area legend");
                    var pts = new StringBuilder();
                    var line = 0;
                    for (var i = 0; i < s.Values.Count; i++)
                    {
                        var v = s.Values[i];
                        if (!Fmt.Finite(v)) continue;
                        var pos = Math.Round(i + (bars.Stacked ? 0 : s.Offset), 6);
                        var c = bars.Horizontal ? "(" + N(v) + "," + N(pos) + ")" : "(" + N(pos) + "," + N(v) + ")";
                        if (s.Labels is { } labels) c += " [" + Tex(labels[i]) + "]";
                        if (line + c.Length > 100) { pts.Append("\n  "); line = 2; }
                        else if (pts.Length > 0) { pts.Append(' '); line++; }
                        pts.Append(c); line += c.Length;
                    }
                    L("\\addplot[" + string.Join(", ", o) + "] coordinates {");
                    L("  " + pts);
                    L("};");
                    if (s.InLegend && _f.Legend != "none") L("\\addlegendentry{" + Tex(s.Name) + "}");
                }
            }

            // ------------------------------------------------------------------ presentation charts

            /// <summary>Fill and edge of an area (white under a pattern); no edge for "none".</summary>
            private string AreaOpts(XFillStyle fill, string edge, double width, DashStyle dash = DashStyle.Solid)
            {
                var white = fill.Kind is "solid" or "none" ? "" : "fill=white, ";
                return white + FillOpts(fill with { Edge = edge == "none" ? null : new XStroke(edge, width, dash) });
            }

            /// <summary>Numbers as an inline table column ("4.2\\ 5.1\\ …"), wrapped.</summary>
            private static string Column(IEnumerable<double> values)
            {
                var sb = new StringBuilder();
                var line = 0;
                foreach (var v in values.Where(Fmt.Finite))
                {
                    var c = N(v) + "\\\\";
                    if (line + c.Length > 100) { sb.Append("\n  "); line = 2; }
                    else if (sb.Length > 0) { sb.Append(' '); line++; }
                    sb.Append(c); line += c.Length;
                }
                return sb.ToString();
            }

            private void Hist(XHist h)
            {
                if (h.Series.Count == 0 || h.Edges.Count < 2) return;
                var bins = h.Edges.Count - 1;
                double lo = h.Edges[0], hi = h.Edges[bins];
                var many = h.Series.Count > 1;
                L("% histogram (statistics library): " + Fmt.I(bins) + " bins of equal width from " + N(lo) + " to " + N(hi));
                if (many) L("% several series: pgfplots draws each over the whole bin (translucent); the chart puts them side by side");
                foreach (var s in h.Series)
                {
                    L("% " + s.Key + ": " + Comment(s.Name));
                    var legend = s.InLegend && _f.Legend != "none";
                    var o = new List<string> { "hist={bins=" + Fmt.I(bins) + ", data min=" + N(lo) + ", data max=" + N(hi) + "}", AreaOpts(s.Fill, s.Fill.Kind is "solid" or "none" ? s.Edge : s.Fill.Color, s.EdgeWidth) };
                    if (many) o.Add("fill opacity=0.6");
                    o.Add(legend ? "area legend" : "forget plot");
                    L("\\addplot[" + string.Join(", ", o) + "] table[row sep=\\\\, y index=0, header=false] {");
                    L("  " + Column(s.Samples));
                    L("};");
                    if (legend) L("\\addlegendentry{" + Tex(s.Name) + "}");
                }
            }

            private void Boxes(XBoxes b)
            {
                L("% box plots (statistics library, boxplot prepared): category i is centred at i; quartiles with linear interpolation, whiskers "
                    + (b.Outliers ? "at the furthest samples within " + N(b.Whis) + " IQR beyond the box, outliers beyond" : "at the minimum and maximum"));
                foreach (var s in b.Series)
                {
                    L("% " + s.Key + ": " + Comment(s.Name));
                    var opts = AreaOpts(s.Fill, s.Line.Color, s.Line.Width, s.Line.Dash);
                    var mark = "mark=o, mark size=" + Mm(0.22 * _f.FontMm) + ", mark options={solid, draw=" + C(s.OutlierColor) + ", fill=white}";
                    if (b.Outliers && s.OutlierKey is { } ok && s.Stats.Any(st => st.Outliers.Count > 0)) L("% " + ok + ": the samples beyond the whiskers");
                    for (var j = 0; j < s.Positions.Count; j++)
                    {
                        var st = s.Stats[j];
                        // whisker range: the rule of the whiskers (pgfplots only uses it for raw data; here it documents the rule).
                        var prepared = "boxplot prepared={draw position=" + N(s.Positions[j]) + ", box extend=" + N(s.Width) + ", lower whisker=" + N(st.Low) + ", lower quartile=" + N(st.Q1)
                            + ", median=" + N(st.Median) + ", upper quartile=" + N(st.Q3) + ", upper whisker=" + N(st.High) + (b.Outliers ? ", whisker range=" + N(b.Whis) : "") + "}";
                        var outliers = b.Outliers && s.OutlierKey is not null ? st.Outliers : Array.Empty<double>();
                        var samples = "box samples={" + string.Join(",", s.Samples[j].Select(N)) + "}";
                        var head = "\\addplot[" + prepared + ", " + samples + ", " + opts + ", boxplot/every median/.style={line width=" + Mm(2 * s.Line.Width) + "}, " + mark + ", forget plot]";
                        if (outliers.Count == 0) L(head + " coordinates {};");
                        else
                        {
                            L(head + " table[row sep=\\\\, y index=0] {data\\\\ " + Column(outliers) + "};");
                        }
                    }
                    if (s.InLegend && _f.Legend != "none")
                    {
                        L("\\addlegendimage{area legend, " + opts + "}");
                        L("\\addlegendentry{" + Tex(s.Name) + "}");
                    }
                }
            }

            private void Gantt(XGantt g)
            {
                L("% Gantt chart: task i is centred at i (the first task on top); a bar runs from the start over the duration");
                var half = g.Height / 2;
                foreach (var grp in g.Groups)
                {
                    var opts = AreaOpts(grp.Fill, grp.Fill.Kind is "solid" or "none" ? grp.Edge : grp.Fill.Color, grp.EdgeWidth);
                    var mark = "mark=diamond*, mark size=" + Mm(grp.MilestoneSize) + ", mark options={solid, draw=" + C(grp.MilestoneColor) + ", fill=" + C(grp.MilestoneColor) + "}";
                    if (grp.Bars.Count > 0)
                    {
                        L("% " + grp.Key + ": " + Comment(grp.Name));
                        foreach (var b in grp.Bars) L("\\fill[" + opts + "] " + P(b.Start, b.Row - half) + " rectangle " + P(b.Start + b.Duration, b.Row + half) + "; % " + Comment(b.Task));
                    }
                    if (grp.Milestones.Count > 0)
                    {
                        L("% " + grp.MilestoneKey + ": " + Comment(string.Join(", ", grp.Milestones.Select(m => m.Task))));
                        L("\\addplot[only marks, " + mark + ", forget plot, clip=false] coordinates {" + Coords(grp.Milestones.Select(m => m.At).ToList(), grp.Milestones.Select(m => m.Row).ToList()) + "};");
                    }
                    if (grp.InLegend && _f.Legend != "none")
                    {
                        L(grp.Bars.Count > 0 ? "\\addlegendimage{area legend, " + opts + "}" : "\\addlegendimage{only marks, " + mark + "}");
                        L("\\addlegendentry{" + Tex(grp.Name) + "}");
                    }
                }
            }

            /// <summary>A TikZ angle (counter-clockwise from 3 o'clock) of a clock angle (clockwise from 12 o'clock).</summary>
            private static string Angle(double clock) => N(Math.Round(90 - clock, 4));

            /// <summary>Pie or donut as TikZ arcs (pgfplots has no pie); texts and the legend where the chart draws them (mm, y up).</summary>
            private void Pie(XPie p)
            {
                // The chart settings and the slice values as data keys (the arcs show the shares only).
                var settings = new List<string>
                {
                    "hole=" + N(Math.Round(p.Hole, 6)), "start=" + N(Math.Round(p.Start, 6)), "direction=" + (p.Clockwise ? "cw" : "ccw"), "labels=" + p.Labels,
                    "legend=" + _f.Legend,
                };
                if (p.Center is { } centre) settings.Add("center={" + Tex(centre) + "}");
                if (_f.Title is { } title) settings.Add("title={" + Tex(title) + "}");
                L("\\begin{tikzpicture}[x=1mm, y=1mm, font=\\footnotesize, pie chart={" + string.Join(", ", settings) + "}]");
                L("% slices " + (p.Clockwise ? "clockwise" : "counter-clockwise") + " from " + N(Math.Round(p.Start, 3)) + " degrees clockwise from 12 o'clock (TikZ angles run counter-clockwise from 3 o'clock); radius "
                    + N(p.RadiusMm) + " mm" + (p.Hole > 0 ? ", hole " + N(Math.Round(p.Hole * p.RadiusMm, 3)) + " mm" : ""));
                var r = N(p.RadiusMm);
                var ri = N(Math.Round(p.Hole * p.RadiusMm, 3));
                foreach (var s in p.Slices)
                {
                    string a0 = Angle(s.Start), a1 = Angle(s.End);
                    L("% " + s.Key + ": " + Comment(s.Name));
                    var path = p.Hole > 0
                        ? "(" + a0 + ":" + r + ") arc[start angle=" + a0 + ", end angle=" + a1 + ", radius=" + r + "] -- (" + a1 + ":" + ri + ") arc[start angle=" + a1 + ", end angle=" + a0 + ", radius=" + ri + "] -- cycle"
                        : "(0,0) -- (" + a0 + ":" + r + ") arc[start angle=" + a0 + ", end angle=" + a1 + ", radius=" + r + "] -- cycle";
                    L("\\fill[pie slice={" + Tex(s.Name) + "}{" + N(s.Value) + "}, " + AreaOpts(s.Fill, s.Edge, s.EdgeWidth) + "] " + path + ";");
                }
                foreach (var (key, pts) in p.Leaders)
                    L("\\draw[" + C(Palette.Ink) + ", line width=0.15mm] " + string.Join(" -- ", pts.Select(v => "(" + N(v.X) + "," + N(v.Y) + ")")) + "; % " + key);
                foreach (var (key, x, y, w, h, fill) in p.Rects)
                    L("\\fill[" + FillOpts(fill) + "] (" + N(x) + "," + N(y) + ") rectangle ++(" + N(w) + "," + N(h) + "); % " + key);
                foreach (var t in p.Texts)
                {
                    var o = new List<string> { "anchor=" + Anchor(t.HAlign, "center") };
                    var font = Font(t.Size, t.Bold, false);
                    if (font.Length > 0) o.Add("font=" + font);
                    o.Add("text=" + C(t.Color));
                    if (t.Box) o.Add("fill=white");
                    o.Add("inner sep=1pt");
                    L("\\node[" + string.Join(", ", o) + "] at (" + N(t.X) + "," + N(t.Y) + ") {" + Tex(t.Text) + "}; % " + t.Key);
                }
                L("\\end{tikzpicture}");
                L("");
            }

            /// <summary>
            /// Sankey diagram as TikZ paths (neither pgfplots nor TikZ has a standard Sankey form): node bars, ribbons with two
            /// cubic Bézier edges as the chart lays them out, labels where the chart draws them (mm from the centre, y up). Every
            /// flow carries its data (source, target, value) in the data key "sankey flow"; a flow that is not drawn (against the
            /// direction, or hidden) is an empty path with that key.
            /// </summary>
            private void Sankey(XSankey k)
            {
                var settings = "values=" + (k.Values ? "true" : "false") + (_f.Title is { } title ? ", title={" + Tex(title) + "}" : "");
                L("\\begin{tikzpicture}[x=1mm, y=1mm, font=\\footnotesize, sankey chart={" + settings + "}]");
                L("% Sankey diagram: every flow (source, target, value) as in the chart data with its ribbon as the chart lays it out in columns");
                foreach (var fl in k.Flows)
                {
                    var key = "sankey flow={" + Tex(fl.Source) + "}{" + Tex(fl.Target) + "}{" + N(fl.Value) + "}";
                    L("% " + fl.Key + ": " + Comment(fl.Source) + " -> " + Comment(fl.Target) + (fl.Drawn ? "" : " (not drawn)"));
                    if (!fl.Drawn) { L("\\path[" + key + "] (0,0);"); continue; }
                    double xm = Math.Round((fl.X0 + fl.X1) / 2, 3), b0 = Math.Round(fl.Y0 - fl.Width, 3), b1 = Math.Round(fl.Y1 - fl.Width, 3);
                    string Pt(double x, double y) => "(" + N(x) + "," + N(y) + ")";
                    L("\\fill[" + key + ", " + FillOpts(fl.Fill) + "] " + Pt(fl.X0, fl.Y0) + " .. controls " + Pt(xm, fl.Y0) + " and " + Pt(xm, fl.Y1) + " .. " + Pt(fl.X1, fl.Y1)
                        + " -- " + Pt(fl.X1, b1) + " .. controls " + Pt(xm, b1) + " and " + Pt(xm, b0) + " .. " + Pt(fl.X0, b0) + " -- cycle;");
                }
                foreach (var nd in k.Nodes)
                    if (nd.Fill is { } fill) L("\\fill[" + FillOpts(fill) + "] (" + N(nd.X) + "," + N(nd.Y) + ") rectangle ++(" + N(nd.W) + "," + N(nd.H) + "); % " + nd.Key + ": " + Comment(nd.Name));
                foreach (var t in k.Texts)
                {
                    var o = new List<string> { "anchor=" + Anchor(t.HAlign, "center") };
                    var font = Font(t.Size, t.Bold, false);
                    if (font.Length > 0) o.Add("font=" + font);
                    o.Add("text=" + C(t.Color));
                    if (t.Box) o.Add("fill=white");
                    o.Add("inner sep=1pt");
                    L("\\node[" + string.Join(", ", o) + "] at (" + N(t.X) + "," + N(t.Y) + ") {" + Tex(t.Text) + "}; % " + t.Key);
                }
                L("\\end{tikzpicture}");
                L("");
            }

            /// <summary>Polar chart as a pgfplots polaraxis (polar library), angles in degrees.</summary>
            private void Polar(XPolar p)
            {
                var f = _f;
                L("\\begin{tikzpicture}");
                L("\\begin{polaraxis}[");
                var o = new List<string> { "scale only axis", "width=" + Mm(2 * p.RadiusMm), "height=" + Mm(2 * p.RadiusMm) };
                var ticks = new List<string>();
                for (var a = 0.0; a < 360 - 1e-9; a += p.AngleStep) ticks.Add(N(a));
                o.Add("xtick={" + string.Join(",", ticks) + "}");
                o.Add("ymin=" + N(p.RMin));
                o.Add("ymax=" + N(p.RMax));
                if (p.RStep > 0) o.Add("ytick distance=" + N(p.RStep));
                if (p.Top)
                {
                    // 0 degrees up and angles clockwise (compass): reversed angles, the axis turned by 90 degrees.
                    o.Add("rotate=90");
                    o.Add("x dir=reverse");
                    o.Add("xticklabel style={anchor=-\\tick-90}");
                }
                if (p.Grid) { o.Add("xmajorgrids"); o.Add("ymajorgrids"); o.Add("major grid style={" + StrokeOpts(f.GridStroke) + "}"); }
                else o.Add("grid=none");
                if (!p.AngleLabels) o.Add("xticklabel={}");
                if (!p.RadiusLabels) o.Add("yticklabel={}");
                o.Add("yticklabel style={font=\\scriptsize}");
                if (f.Lang == Lang.De) o.Add("/pgf/number format/use comma");
                if (f.Title is { } title) o.Add("title={\\bfseries " + Tex(title) + "}");
                var legend = f.Legend != "none" && p.Series.Any(s => s.Legend is not null);
                if (legend)
                {
                    o.Add("legend pos=outer north east");
                    o.Add("legend cell align=left");
                    o.Add("legend style={draw=" + C(Palette.LegendBorder) + ", fill=white}");
                }
                WriteOptions(o);
                L("]");
                foreach (var s in p.Series)
                {
                    L("% " + s.Key + (s.Legend is { } lg ? ": " + Comment(lg) : ""));
                    if (s.Fill is { } fill)
                    {
                        L("\\addplot[" + FillOpts(fill) + ", forget plot] coordinates {");
                        L("  " + Coords(s.Angles, s.R));
                        L("};");
                    }
                    if (s.Line is null && s.Marker is null) continue;
                    if (s.Marker is not null) L("% " + s.MarkerKey);
                    var withLegend = legend && s.Legend is not null;
                    L("\\addplot[" + SeriesOptions(new XSeries { Line = s.Line, Marker = s.Marker }) + (withLegend ? "" : ", forget plot") + "] coordinates {");
                    L("  " + Coords(s.Angles, s.R));
                    L("};");
                    if (withLegend) L("\\addlegendentry{" + Tex(s.Legend!) + "}");
                }
                L("\\end{polaraxis}");
                L("\\end{tikzpicture}");
                L("");
            }
        }
    }
}
