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
    /// Export of the selected charts as a self-contained Python script for matplotlib (charts v2 stage 4): one function per
    /// chart that recreates it with its data, scales, ranges, line styles, colours, markers, regions, value lines, notes and
    /// reference numerals. Every text is an ordinary matplotlib text (mathtext-safe), so it stays editable in the script.
    /// </summary>
    internal sealed class MatplotlibExporter : IExporter
    {
        public string Id => "chart.matplotlib";
        public I18nText Name => new("Charts as Python (matplotlib)", "Diagramme als Python (matplotlib)");
        public string Extension => "py";
        public string MimeType => "text/x-python";
        public I18nText Preserves => new(
            "Charts as a matplotlib script: data, axes, scales, line styles, colours, markers, regions, value lines, notes and numerals; every text stays editable",
            "Diagramme als matplotlib-Skript: Daten, Achsen, Skalen, Linienarten, Farben, Marker, Flächen, Wertlinien, Anmerkungen und Bezugszeichen; alle Texte bleiben bearbeitbar");

        public bool Applies(Registry registry, SketchDocument doc, IReadOnlyList<SketchObject> objects) => ChartExport.Charts(registry, objects).Any();

        public string Export(Registry registry, SketchDocument doc, IReadOnlyList<SketchObject> objects, Lang lang)
            => MatplotlibWriter.Write(ChartExport.Figures(registry, doc, objects, lang), lang);
    }

    /// <summary>Charts of a selection and their export models.</summary>
    internal static class ChartExport
    {
        public static IEnumerable<SketchObject> Charts(Registry registry, IEnumerable<SketchObject> objects)
            => objects.Where(o => o.Type.StartsWith("chart.", StringComparison.Ordinal) && registry.Type(o.Type) is ChartType);

        /// <summary>The export model of every chart (null with a message when a chart cannot be exported).</summary>
        public static List<(SketchObject Object, XFigure? Figure, string? Error)> Figures(Registry registry, SketchDocument doc, IReadOnlyList<SketchObject> objects, Lang lang)
        {
            var list = new List<(SketchObject, XFigure?, string?)>();
            foreach (var o in Charts(registry, objects))
            {
                try { list.Add((o, ExportBuilder.Build(registry, doc, o, lang), null)); }
                catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IndexOutOfRangeException or NullReferenceException or FormatException or OverflowException)
                {
                    list.Add((o, null, ex.Message));
                }
            }
            return list;
        }
    }

    /// <summary>Writes export models as a matplotlib script.</summary>
    internal static class MatplotlibWriter
    {
        private const string I = "    ";

        public static string Write(IReadOnlyList<(SketchObject Object, XFigure? Figure, string? Error)> charts, Lang lang)
        {
            var sb = new StringBuilder();
            sb.Append("# -*- coding: utf-8 -*-\n");
            sb.Append("\"\"\"\n");
            sb.Append(lang == Lang.De
                ? "Diagramme aus InventaSketch als matplotlib-Skript (matplotlib 3.5 oder neuer).\nAlle Texte sind normale matplotlib-Texte und können hier bearbeitet werden.\n\n  python skript.py               zeigt die Diagramme\n  python skript.py out/bild.pdf  speichert out/bild-1.pdf, out/bild-2.pdf ...\n"
                : "Charts exported from InventaSketch as a matplotlib script (matplotlib 3.5 or later).\nEvery text is an ordinary matplotlib text and can be edited here.\n\n  python script.py                shows the charts\n  python script.py out/chart.pdf  saves out/chart-1.pdf, out/chart-2.pdf ...\n");
            sb.Append("\"\"\"\n");
            sb.Append("import os\nimport sys\n\nimport matplotlib\nimport matplotlib.pyplot as plt\nfrom matplotlib.patches import ConnectionPatch\nfrom matplotlib.ticker import FuncFormatter, MultipleLocator\n\n");
            sb.Append("nan = float('nan')  # a gap in a curve\n");
            sb.Append("MM = 1 / 25.4  # inch per millimetre\n");
            sb.Append("BOX = dict(boxstyle='square,pad=0.15', facecolor='white', edgecolor='none')  # white backing behind a text\n\n\n");
            sb.Append("def plain(v, pos=None):\n    \"\"\"Tick label as a plain number.\"\"\"\n    return '%g' % v\n\n\n");
            sb.Append("def comma(v, pos=None):\n    \"\"\"Tick label with a decimal comma.\"\"\"\n    return ('%g' % v).replace('.', ',')\n\n\n");
            sb.Append("def arrow_x(ax):\n    \"\"\"Arrowhead at the end of the x axis (principle axes).\"\"\"\n    ax.plot(1, 0, '>', color='#222222', ms=4, transform=ax.transAxes, clip_on=False)\n\n\n");
            sb.Append("def arrow_y(ax):\n    \"\"\"Arrowhead at the end of the y axis (principle axes).\"\"\"\n    ax.plot(0, 1, '^', color='#222222', ms=4, transform=ax.transAxes, clip_on=False)\n\n\n");
            var names = new List<string>();
            var k = 0;
            foreach (var (o, fig, error) in charts)
            {
                k++;
                if (fig is null)
                {
                    sb.Append("# Chart ").Append(Fmt.I(k)).Append(" (").Append(Comment(o.Id)).Append(") could not be exported: ").Append(Comment(error ?? "")).Append("\n\n\n");
                    continue;
                }
                var name = "figure_" + Fmt.I(k);
                names.Add(name);
                new Fig(sb, fig, name).Write();
                sb.Append("\n\n");
            }
            sb.Append("FIGURES = [").Append(string.Join(", ", names)).Append("]\n\n");
            sb.Append("if __name__ == '__main__':\n");
            sb.Append(I).Append("figures = [make() for make in FIGURES]\n");
            sb.Append(I).Append("if len(sys.argv) > 1:\n");
            sb.Append(I).Append(I).Append("base, ext = os.path.splitext(sys.argv[1])\n");
            sb.Append(I).Append(I).Append("for i, fig in enumerate(figures, 1):\n");
            sb.Append(I).Append(I).Append(I).Append("fig.savefig('%s-%d%s' % (base, i, ext or '.pdf'))\n");
            sb.Append(I).Append("else:\n");
            sb.Append(I).Append(I).Append("plt.show()\n");
            return sb.ToString();
        }

        private static string Comment(string s) => s.Replace("\r", " ").Replace("\n", " ");

        /// <summary>Writer of one figure function.</summary>
        private sealed class Fig
        {
            private readonly StringBuilder _sb;
            private readonly XFigure _f;
            private readonly string _name;
            private readonly HashSet<string> _vars = new(StringComparer.Ordinal);
            private readonly bool _de;

            public Fig(StringBuilder sb, XFigure f, string name) { _sb = sb; _f = f; _name = name; _de = f.Lang == Lang.De; }

            private void L(string line) => (line.Length == 0 ? _sb : _sb.Append(I).Append(line)).Append('\n');

            private string Var(string key, string suffix)
            {
                var b = new string(key.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray()).Trim('_');
                if (b.Length == 0 || char.IsDigit(b[0])) b = "d_" + b;
                var name = b + "_" + suffix;
                for (var i = 2; !_vars.Add(name); i++) name = b + Fmt.I(i) + "_" + suffix;
                return name;
            }

            private static string Ls(DashStyle d) => d switch
            {
                DashStyle.Dash => "'--'",
                DashStyle.Dot => "':'",
                DashStyle.DashDot => "'-.'",
                DashStyle.LongDash => "(0, (8, 3))",
                DashStyle.DashDotDot => "(0, (4, 2, 1, 2, 1, 2))",
                _ => "'-'",
            };

            private static string Mk(string kind) => kind switch
            {
                "square" => "'s'",
                "triangle" => "'^'",
                "diamond" => "'D'",
                "cross" => "'x'",
                "plus" => "'+'",
                _ => "'o'",
            };

            private static string StrokeArgs(XStroke s) => "color=" + PyLit(s.Color) + ", lw=" + Pt(s.Width) + ", ls=" + Ls(s.Dash) + (s.Opacity is { } o ? ", alpha=" + N(o) : "");

            private string Ax(int panel) => "axs[" + Fmt.I(Math.Max(0, Math.Min(_f.Panels.Count - 1, panel))) + "]";

            private string Size(double factor) => Math.Abs(factor - 1) < 1e-9 ? "fs" : "fs * " + N(Math.Round(factor, 3));

            public void Write()
            {
                var f = _f;
                var n = f.Panels.Count;
                _sb.Append("def ").Append(_name).Append("():\n");
                L("\"\"\"" + Comment(f.Name).Replace("\"", "'") + " [" + Comment(f.Id).Replace("\"", "'") + "]\"\"\"");
                L("fs = " + N(Math.Round(f.FontMm * ExportBuilder.PT, 2)) + "  # chart font size (pt)");
                L("plt.rcParams.update({'font.size': fs, 'hatch.linewidth': 0.5})");
                var grid = n > 1 ? ", gridspec_kw={'height_ratios': " + PyList(f.Panels.Select(p => p.Share), I) + "}" : "";
                L("fig, axs = plt.subplots(" + Fmt.I(n) + ", 1, sharex=True, squeeze=False, figsize=(" + N(Math.Round(f.WidthMm, 2)) + " * MM, " + N(Math.Round(f.HeightMm, 2)) + " * MM), layout='constrained'" + grid + ")");
                L("axs = [row[0] for row in axs]");
                for (var i = 0; i < n; i++)
                    if (f.Panels[i].Y2 is not null) L("tw" + Fmt.I(i) + " = axs[" + Fmt.I(i) + "].twinx()  # secondary y axis");

                // Axes.
                L("");
                L("# x axis (shared by all panels)");
                AxisSetup(Ax(n - 1), f.X, 'x', false);
                L("for ax in axs:");
                L(I + "ax.set_axisbelow(True)");
                if (!f.Box) L(I + "ax.spines['top'].set_visible(False)");
                if (f.X.LineHidden) L(I + "ax.spines['bottom'].set_visible(False)");
                if (f.X.Arrow && !f.Box) L(I + "arrow_x(ax)");
                if (f.X.GridMajor) L(I + "ax.xaxis.grid(True, which='major', " + StrokeArgs(f.GridStroke) + ")");
                if (f.X.GridMinor) L(I + "ax.xaxis.grid(True, which='minor', " + StrokeArgs(f.MinorGridStroke) + ")");
                for (var i = 0; i < n; i++)
                {
                    var p = f.Panels[i];
                    var ax = Ax(i);
                    L("");
                    L("# panel " + Fmt.I(i + 1) + ": y axis");
                    AxisSetup(ax, p.Y, 'y', false);
                    if (!f.Box && p.Y2 is null) L(ax + ".spines['right'].set_visible(False)");
                    if (p.Y.LineHidden) L(ax + ".spines['left'].set_visible(False)");
                    if (p.Y.Arrow && !f.Box) L("arrow_y(" + ax + ")");
                    if (p.Y.GridMajor) L(ax + ".yaxis.grid(True, which='major', " + StrokeArgs(f.GridStroke) + ")");
                    if (p.Y.GridMinor) L(ax + ".yaxis.grid(True, which='minor', " + StrokeArgs(f.MinorGridStroke) + ")");
                    if (p.Y2 is { } y2)
                    {
                        var tw = "tw" + Fmt.I(i);
                        AxisSetup(tw, y2, 'y', true);
                        if (!f.Box) L(tw + ".spines['top'].set_visible(False)");
                    }
                }

                // Items.
                if (f.Bars is { } bars) Bars(bars);
                foreach (var item in f.Items)
                {
                    L("");
                    switch (item)
                    {
                        case XSeries s: Series(s); break;
                        case XHLine h:
                            L("# " + h.Key);
                            L(Ax(h.Panel) + ".axhline(" + N(h.Y) + ", " + StrokeArgs(h.Stroke) + ")");
                            break;
                        case XVLine v: VLine(v); break;
                        case XFill r: Fill(r); break;
                        case XLabel l: L("# " + l.Key); Label(l); break;
                        case XLeader d: Leader(d); break;
                        case XSegment g: Segment(g); break;
                        case XContours c: Contours(c); break;
                    }
                }
                foreach (var t in f.PanelTitles) { L(""); L("# " + t.Key); Label(t); }

                // Legend, title, caption.
                if (f.Legend != "none")
                    for (var i = 0; i < n; i++)
                    {
                        var labeled = f.Items.Any(x => x.Panel == i && (x is XSeries { Legend: not null } || x is XFill { Legend: not null }))
                            || i == 0 && f.Bars is { } b && b.Series.Any(s => s.InLegend);
                        if (!labeled) continue;
                        var where = f.Legend == "right" ? "loc='upper left', bbox_to_anchor=(1.02, 1), borderaxespad=0" : "loc=" + PyLit(f.LegendCorner);
                        L("");
                        L("# legend");
                        if (f.Panels[i].Y2 is not null)
                        {
                            L("h1, l1 = " + Ax(i) + ".get_legend_handles_labels()");
                            L("h2, l2 = tw" + Fmt.I(i) + ".get_legend_handles_labels()");
                            L(Ax(i) + ".legend(h1 + h2, l1 + l2, " + where + ", fontsize=fs * 0.9)");
                        }
                        else L(Ax(i) + ".legend(" + where + ", fontsize=fs * 0.9)");
                    }
                if (f.Title is { } title) { L(""); L("fig.suptitle(" + Py(title) + ", fontsize=fs, fontweight='bold')"); }
                if (f.Caption is { } caption) { L(""); L("fig.supxlabel(" + Py(caption) + ", fontsize=fs)  # caption below the chart"); }
                L("return fig");
            }

            /// <summary>Scale, range, ticks, tick labels and title of one axis.</summary>
            private void AxisSetup(string ax, XAxis a, char d, bool twin)
            {
                var D = d.ToString();
                if (a.IsLog) L(ax + ".set_" + D + "scale('log')");
                if (a.IsCategory)
                {
                    var n = a.Categories.Count;
                    L(ax + ".set_" + D + "ticks(" + PyList(Enumerable.Range(0, n).Select(i => (double)i), I) + ")");
                    L(ax + ".set_" + D + "ticklabels([" + string.Join(", ", a.Categories.Select(Py)) + "])");
                    L(ax + ".set_" + D + "lim(" + (a.Reversed ? N(a.Max) + ", " + N(a.Min) : N(a.Min) + ", " + N(a.Max)) + ")");
                    L(ax + ".tick_params(axis='" + D + "', length=0)");
                }
                else
                {
                    L(ax + ".set_" + D + "lim(" + N(a.Min) + ", " + N(a.Max) + ")");
                    if (a.Marks is { } marks)
                    {
                        L(ax + ".set_" + D + "ticks(" + PyList(marks.Select(m => m.At), I) + ")");
                        if (marks.Count > 0) L(ax + ".set_" + D + "ticklabels([" + string.Join(", ", marks.Select(m => Py(m.Text))) + "])");
                        L(ax + ".set_" + D + "ticks([], minor=True)");
                    }
                    else
                    {
                        if (!a.IsLog && a.Step > 0) L(ax + "." + D + "axis.set_major_locator(MultipleLocator(" + N(a.Step) + "))");
                        if (_de) L(ax + "." + D + "axis.set_major_formatter(FuncFormatter(comma))");
                        else if (a.IsLog) L(ax + "." + D + "axis.set_major_formatter(FuncFormatter(plain))");
                    }
                }
                if (a.Title is { } t) L(ax + ".set_" + D + "label(" + Py(t) + ")");
                if (a.TicksHidden) L(ax + ".tick_params(axis='" + D + "', which='both', length=0)");
                if (a.TickLabelsHidden) L(ax + ".tick_params(axis='" + D + "', " + (d == 'x' ? "labelbottom" : twin ? "labelright" : "labelleft") + "=False)");
                if (twin && a.LineHidden) L(ax + ".spines['right'].set_visible(False)");
            }

            private void Series(XSeries s)
            {
                L("# " + s.Key + (s.Legend is { } lg ? " – " + Comment(lg) : ""));
                var vx = Var(s.Key, "x"); var vy = Var(s.Key, "y");
                L(vx + " = " + PyList(s.X, I));
                L(vy + " = " + PyList(s.Y, I));
                var args = new List<string> { vx, vy };
                if (s.Line is { } line) args.Add(StrokeArgs(line));
                else args.Add("ls='none'");
                if (s.Marker is { } m)
                {
                    args.Add("marker=" + Mk(m.Kind) + ", ms=" + Pt(2 * m.Size) + ", mec=" + PyLit(m.Color) + ", mfc=" + (m.Filled ? PyLit(m.Color) : "'none'") + ", mew=0.8");
                    if (s.Line is null) args.Add("color=" + PyLit(m.Color));
                }
                if (s.Step) args.Add("drawstyle='steps-post'");
                if (s.Legend is { } legend) args.Add("label=" + Py(legend));
                var ax = s.Y2 ? "tw" + Fmt.I(s.Panel) : Ax(s.Panel);
                if (s.Y2 && _f.Panels[Math.Max(0, Math.Min(_f.Panels.Count - 1, s.Panel))].Y2 is null) ax = Ax(s.Panel);
                L(ax + ".plot(" + string.Join(", ", args) + ")");
            }

            private void VLine(XVLine v)
            {
                L("# " + v.Key);
                if (v.LastPanel <= v.Panel) { L(Ax(v.Panel) + ".axvline(" + N(v.X) + ", " + StrokeArgs(v.Stroke) + ")"); return; }
                L("# a vertical marker through panels " + Fmt.I(v.Panel + 1) + " to " + Fmt.I(v.LastPanel + 1));
                L("fig.add_artist(ConnectionPatch((" + N(v.X) + ", 1), (" + N(v.X) + ", 0), coordsA=" + Ax(v.Panel) + ".get_xaxis_transform(), coordsB=" + Ax(v.LastPanel)
                    + ".get_xaxis_transform(), " + StrokeArgs(v.Stroke) + "))");
            }

            private static string Hatch(XFillStyle s)
            {
                var a = ((s.Angle % 180) + 180) % 180;
                string c;
                if (s.Kind == "dots") c = ".";
                else if (s.Kind == "cross") c = Math.Abs(a - 45) < 22.5 || Math.Abs(a - 135) < 22.5 ? "x" : "+";
                else c = Math.Abs(a - 45) < 22.5 ? "/" : Math.Abs(a - 135) < 22.5 ? "\\" : Math.Abs(a - 90) < 22.5 ? "|" : "-";
                var times = s.Spacing <= 0.9 ? 3 : s.Spacing <= 1.8 ? 2 : 1;
                return string.Concat(Enumerable.Repeat(c, times));
            }

            private static string FillArgs(XFillStyle s)
            {
                if (s.Kind == "solid") return "facecolor=" + PyLit(s.Color) + ", linewidth=0" + (s.Opacity is { } o ? ", alpha=" + N(o) : "");
                if (s.Kind == "none") return "facecolor='none', linewidth=0";
                return "facecolor='none', edgecolor=" + PyLit(s.Color) + ", hatch=" + PyLit(Hatch(s)) + ", linewidth=0" + (s.Opacity is { } op ? ", alpha=" + N(op) : "");
            }

            private void Fill(XFill r)
            {
                L("# " + r.Key + (r.Legend is { } lg ? " – " + Comment(lg) : ""));
                var label = r.Legend is { } legend ? ", label=" + Py(legend) : "";
                if (r.Kind == "span")
                {
                    foreach (var p in r.Panels.DefaultIfEmpty(r.Panel))
                        L(Ax(p) + ".axvspan(" + N(r.X0) + ", " + N(r.X1) + ", " + FillArgs(r.Fill) + label + ")");
                    return;
                }
                var vx = Var(r.Key, "x"); var va = Var(r.Key, "a"); var vb = Var(r.Key, "b");
                L(vx + " = " + PyList(r.X, I));
                L(va + " = " + PyList(r.Lo, I));
                L(vb + " = " + PyList(r.Hi, I));
                var where = "";
                if (r.Where is not null)
                {
                    if (r.WhereText is { } wt) L("# only where " + wt);
                    where = ", where=[a >= b for a, b in zip(" + va + ", " + vb + ")], interpolate=True";
                }
                L(Ax(r.Panel) + ".fill_between(" + vx + ", " + va + ", " + vb + where + ", " + FillArgs(r.Fill) + label + ")");
            }

            private static string Coords(bool xf, bool yf) => xf && yf ? "'axes fraction'" : xf ? "('axes fraction', 'data')" : yf ? "('data', 'axes fraction')" : "'data'";

            private void Label(XLabel l)
            {
                var args = new List<string>
                {
                    Py(l.Text), "xy=(" + N(l.X) + ", " + N(l.Y) + ")", "xycoords=" + Coords(l.XFrac, l.YFrac),
                    "xytext=(" + N(Math.Round(l.Dx, 2)) + ", " + N(Math.Round(l.Dy, 2)) + ")", "textcoords='offset points'",
                    "ha='" + l.HAlign + "'", "va='" + l.VAlign + "'", "fontsize=" + Size(l.Size), "color=" + PyLit(l.Color),
                };
                if (l.Italic) args.Add("fontstyle='italic'");
                if (l.Bold) args.Add("fontweight='bold'");
                if (l.Box) args.Add("bbox=BOX");
                args.Add("annotation_clip=False");
                L(Ax(l.Panel) + ".annotate(" + string.Join(", ", args) + ")");
            }

            private void Leader(XLeader d)
            {
                L("# " + d.Key + ": reference numeral with leader line");
                L(Ax(d.Panel) + ".annotate(" + Py(d.Text) + ", xy=(" + N(d.X) + ", " + N(d.Y) + "), xycoords=" + Coords(d.XFrac, d.YFrac)
                    + ", xytext=(" + N(Math.Round(d.Dx, 3)) + " * fs, " + N(Math.Round(d.Dy, 3)) + " * fs), textcoords='offset points', ha='center', va='center', fontsize=fs * 0.95, color="
                    + PyLit(d.Color) + ", arrowprops=dict(arrowstyle='-', color=" + PyLit(d.Color) + ", lw=0.57, shrinkA=1.5, shrinkB=0), annotation_clip=False)");
            }

            private void Segment(XSegment g)
            {
                L("# " + g.Key + (g.Kind == "dim" ? ": dimension arrow" : g.Kind == "arrow" ? ": arrow" : ": line"));
                var style = g.Kind switch { "arrow" => "'-|>'", "dim" => "'<|-|>'", _ => "'-'" };
                var props = "arrowstyle=" + style + ", mutation_scale=7, shrinkA=0, shrinkB=0, " + StrokeArgs(g.Stroke).Replace("ls=", "linestyle=");
                if (g.Panel2 == g.Panel)
                    L(Ax(g.Panel) + ".annotate('', xy=(" + N(g.B.X) + ", " + N(g.B.Y) + "), xytext=(" + N(g.A.X) + ", " + N(g.A.Y) + "), arrowprops=dict(" + props + "), annotation_clip=False)");
                else
                    L("fig.add_artist(ConnectionPatch((" + N(g.A.X) + ", " + N(g.A.Y) + "), (" + N(g.B.X) + ", " + N(g.B.Y) + "), coordsA='data', coordsB='data', axesA=" + Ax(g.Panel)
                        + ", axesB=" + Ax(g.Panel2) + ", " + props + "))");
            }

            private void Contours(XContours c)
            {
                L("# " + c.Key + ": contour lines of the field (nan outside the valid range)");
                var vx = Var(c.Key, "x"); var vy = Var(c.Key, "y"); var vz = Var(c.Key, "z");
                L(vx + " = " + PyList(c.X, I));
                L(vy + " = " + PyList(c.Y, I));
                L(vz + " = [");
                for (var j = 0; j < c.Y.Length; j++)
                    L(I + PyList(Enumerable.Range(0, c.X.Length).Select(i => c.Z[i, j]), I + I) + ",");
                L("]");
                var cs = Var(c.Key, "set");
                L(cs + " = " + Ax(c.Panel) + ".contour(" + vx + ", " + vy + ", " + vz + ", levels=" + PyList(c.Levels, I) + ", colors=" + PyLit(c.Stroke.Color)
                    + ", linewidths=" + Pt(c.Stroke.Width) + ", linestyles=" + Ls(c.Stroke.Dash) + ")");
                if (c.Labels)
                {
                    var fmt = "%g" + c.LabelSuffix.Replace("%", "%%");
                    L(Ax(c.Panel) + ".clabel(" + cs + ", fmt=" + (_de ? "lambda v: (" + PyLit(fmt) + " % v).replace('.', ',')" : PyLit(fmt)) + ", fontsize=fs * 0.72)");
                }
            }

            private void Bars(XBars bars)
            {
                var ax = Ax(0);
                L("");
                L("# bars: category i is centred at i");
                foreach (var s in bars.Series)
                {
                    L("# " + s.Key + " – " + Comment(s.Name));
                    var pos = Enumerable.Range(0, s.Values.Count).Select(i => Math.Round(i + s.Offset, 6));
                    var vb = Var(s.Key, "bars");
                    var args = new List<string> { PyList(pos, I), PyList(s.Values, I), N(Math.Round(bars.Width, 6)) };
                    if (bars.Stacked) args.Add((bars.Horizontal ? "left=" : "bottom=") + PyList(s.Bottoms, I));
                    args.Add(s.Fill.Kind == "solid" ? "color=" + PyLit(s.Fill.Color) : "color='white', hatch=" + PyLit(Hatch(s.Fill)));
                    args.Add("edgecolor=" + PyLit(s.Fill.Kind == "solid" || s.Fill.Kind == "none" ? s.Edge : s.Fill.Color));
                    args.Add("linewidth=" + Pt(s.EdgeWidth));
                    if (s.InLegend) args.Add("label=" + Py(s.Name));
                    L(vb + " = " + ax + (bars.Horizontal ? ".barh(" : ".bar(") + string.Join(", ", args) + ")");
                    if (s.Labels is { } labels)
                        L(ax + ".bar_label(" + vb + ", labels=[" + string.Join(", ", labels.Select(Py)) + "], label_type=" + (bars.Stacked ? "'center'" : "'edge'") + ", padding=2, fontsize=fs * 0.8)");
                }
            }
        }
    }
}
