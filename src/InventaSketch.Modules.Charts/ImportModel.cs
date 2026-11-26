using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using InventaSketch.Core;

namespace InventaSketch.Modules.Charts
{
    // =============================================================================================
    // Code import (roadmap 4b), the neutral model: what a matplotlib script or a pgfplots axis says about
    // a figure (axes in a grid, curves, bars, value lines, filled areas, notes), and the conversion into
    // chart elements: a "chart.figure" (FigureSpec) for curves, stacked panels, value lines, regions and
    // notes; a "chart.bar" for bars. Both readers fill this model; nothing here reads code.
    // =============================================================================================

    /// <summary>A bar series of an axes: positions (numbers) or category names, values, base values, width, look.</summary>
    internal sealed class ImpBars
    {
        public double[] Pos = Array.Empty<double>();
        public string[]? Cats;
        public double[] Values = Array.Empty<double>();
        public double[]? Bottom;
        public bool Horizontal;
        public string? Label;
        public int Line;
    }

    /// <summary>A filled area between two curves (or a curve and a constant), resolved to series when the chart is built.</summary>
    internal sealed class ImpFill
    {
        public double[] X = Array.Empty<double>();
        public double[] Y1 = Array.Empty<double>();
        public double[]? Y2;
        public double Y2Const;
        public bool[]? Where;
        public FigRegion Region = new();
        public string? Label;
        public int Line;
        public string Code = "";
        /// <summary>pgfplots: the series indices of the named paths (already resolved).</summary>
        public int? SeriesA, SeriesB;
        /// <summary>fill_betweenx: <see cref="X"/> holds the y values, <see cref="Y1"/>/<see cref="Y2"/> the x values of the edges.</summary>
        public bool Horizontal;
    }

    /// <summary>A note whose points may be given as axes fractions (resolved with the final axis ranges).</summary>
    internal sealed class ImpNote
    {
        public FigNote Note = new();
        public bool XFrac, YFrac, X2Frac, Y2Frac;
        public int Line;
        public string Code = "";
    }

    /// <summary>A pie of an axes (matplotlib pie): values, labels, hole, start angle and direction, label mode.</summary>
    internal sealed class ImpPie
    {
        public double[] Values = Array.Empty<double>();
        public string[]? Labels;
        /// <summary>Inner radius / outer radius (wedgeprops width).</summary>
        public double Hole;
        /// <summary>matplotlib start angle: degrees counter-clockwise from the positive x axis.</summary>
        public double StartAngle;
        public bool CounterClock = true;
        /// <summary>Percent labels (autopct).</summary>
        public bool Percent;
        public int Line;
    }

    /// <summary>One axes (plot area) of a figure.</summary>
    internal sealed class ImpAxes
    {
        /// <summary>A pie (the axes shows nothing else).</summary>
        public ImpPie? Pie;
        /// <summary>Polar projection: the series are (angle in radians, radius).</summary>
        public bool Polar;
        /// <summary>Polar: 0° at the top (set_theta_zero_location('N')).</summary>
        public bool PolarTop;
        /// <summary>Polar: angles clockwise (set_theta_direction(-1)).</summary>
        public bool PolarClockwise;
        /// <summary>Polar: the curves are filled (fill).</summary>
        public bool PolarFill;
        public int Row, Col;
        public int Line;
        public string? Title, XLabel, YLabel;
        public string XScale = "linear", YScale = "linear";
        public double? XMin, XMax, YMin, YMax;
        public bool? XGrid, YGrid;
        public double Share = 1;
        public List<FigSeries> Series { get; } = new();
        /// <summary>Series whose line is hidden (markers only, or invisible edges of a filled area).</summary>
        public HashSet<int> HiddenLines { get; } = new();
        public List<FigRef> Refs { get; } = new();
        public List<FigRegion> Spans { get; } = new();
        public List<ImpFill> Fills { get; } = new();
        public List<ImpNote> Notes { get; } = new();
        public List<ImpBars> Bars { get; } = new();
        public bool BarValues;
        /// <summary>Arrowheads at the axis ends (principle axes without ticks).</summary>
        public bool XArrow, YArrow;
        /// <summary>Stacked bars (pgfplots ybar stacked); null = from the base values.</summary>
        public bool? BarStacked;
        public bool Legend;
        public string LegendWhere = "inside";
        public List<double>? XTicks, YTicks;
        public List<string>? XTickLabels, YTickLabels;

        public bool HasContent => Series.Count > 0 || Bars.Count > 0 || Refs.Count > 0 || Spans.Count > 0 || Fills.Count > 0 || Notes.Count > 0 || Pie is not null;

        /// <summary>True if an axis range was set (empty axes with ranges are imported as a coordinate system).</summary>
        public bool HasRange => XMin is not null || XMax is not null || YMin is not null || YMax is not null;
    }

    /// <summary>A figure: axes in a grid, title, caption, size.</summary>
    internal sealed class ImpFigure
    {
        public List<ImpAxes> Axes { get; } = new();
        public int Rows = 1, Cols = 1;
        public string? Title, Caption, SupXLabel;
        public double? WidthMm, HeightMm;
        public bool Legend;
        public string LegendWhere = "inside";
        public int Line;
        /// <summary>Vertical lines through several panels (ConnectionPatch, pgfplots coordinates): x, first and last panel.</summary>
        public List<(double X, int From, int To, FigRef Ref)> PanelLines { get; } = new();
        /// <summary>Arrows and lines between panels.</summary>
        public List<ImpNote> CrossNotes { get; } = new();
    }

    /// <summary>Import of plotting code as chart elements (matplotlib scripts, pgfplots axes; roadmap 4b).</summary>
    public static class ChartCodeImport
    {
        /// <summary>The chart elements (placed on the page) and the report.</summary>
        public sealed record Result(IReadOnlyList<SketchObject> Charts, CodeImportReport Report);

        /// <summary>A matplotlib script (pyplot or object style) as charts; the code is read, never executed.</summary>
        public static Result Matplotlib(Registry registry, string text, Size2 page)
        {
            var report = new CodeImportReport();
            var figures = MatplotlibReader.Read(text ?? "", report);
            return Build(registry, figures, page, report);
        }

        /// <summary>pgfplots axes (axis, semilogxaxis, semilogyaxis, loglogaxis, groupplot) of a LaTeX text as charts.</summary>
        public static Result Pgfplots(Registry registry, string text, Size2 page)
        {
            var report = new CodeImportReport();
            var figures = PgfplotsReader.Read(text ?? "", report);
            return Build(registry, figures, page, report);
        }

        internal static Result Build(Registry registry, List<ImpFigure> figures, Size2 page, CodeImportReport report)
        {
            var charts = new List<SketchObject>();
            var k = 0;
            foreach (var f in figures)
                foreach (var (o, summary) in ChartBuilder.Build(registry, f, report))
                {
                    k++;
                    charts.Add(o);
                    report.Imported.Add(new I18nText("Chart " + Fmt.I(k) + ": " + summary.En, "Diagramm " + Fmt.I(k) + ": " + summary.Get(Lang.De)));
                }
            return new Result(Arrange(charts, page), report);
        }

        /// <summary>Charts side by side (rows of up to three), centred on the page and scaled down together when they do not fit.</summary>
        internal static List<SketchObject> Arrange(List<SketchObject> charts, Size2 page)
        {
            if (charts.Count == 0) return charts;
            const double gap = 8;
            var cols = charts.Count <= 3 ? charts.Count : (int)Math.Ceiling(Math.Sqrt(charts.Count));
            var rows = (int)Math.Ceiling(charts.Count / (double)cols);
            var colW = new double[cols]; var rowH = new double[rows];
            for (var i = 0; i < charts.Count; i++)
            {
                colW[i % cols] = Math.Max(colW[i % cols], charts[i].Size.W);
                rowH[i / cols] = Math.Max(rowH[i / cols], charts[i].Size.H);
            }
            var totalW = colW.Sum() + gap * (cols - 1);
            var totalH = rowH.Sum() + gap * (rows - 1);
            var s = Math.Min(1, Math.Min(page.W * 0.9 / totalW, page.H * 0.9 / totalH));
            var x0 = page.W / 2 - totalW * s / 2;
            var y0 = page.H / 2 - totalH * s / 2;
            var list = new List<SketchObject>();
            for (var i = 0; i < charts.Count; i++)
            {
                int c = i % cols, r = i / cols;
                var cx = x0 + (colW.Take(c).Sum() + gap * c + colW[c] / 2) * s;
                var cy = y0 + (rowH.Take(r).Sum() + gap * r + rowH[r] / 2) * s;
                var o = charts[i];
                list.Add(o with
                {
                    Transform = o.Transform with { X = Math.Round(cx, 3), Y = Math.Round(cy, 3) },
                    Size = new Size2(Math.Round(Math.Max(20, o.Size.W * s), 2), Math.Round(Math.Max(15, o.Size.H * s), 2)),
                });
            }
            return list;
        }
    }

    /// <summary>Conversion of the neutral model into chart elements.</summary>
    internal static class ChartBuilder
    {
        public static IEnumerable<(SketchObject Chart, I18nText Summary)> Build(Registry registry, ImpFigure f, CodeImportReport report)
        {
            var axes = f.Axes.Where(a => a.HasContent || a.XLabel is not null || a.YLabel is not null || a.Title is not null || a.HasRange).ToList();
            if (axes.Count == 0) yield break;
            var bars = axes.Where(a => a.Bars.Count > 0 || a.Pie is not null || a.Polar).ToList();
            var stack = f.Cols == 1 && bars.Count == 0 && axes.Count <= FigureSpec.MaxPanels;
            if (stack)
            {
                var o = Figure(registry, f, f.Axes.Where(a => a.Col == 0).OrderBy(a => a.Row).ToList(), report, true, out var summary);
                if (o is not null) yield return (o, summary);
                yield break;
            }
            if (f.Cols == 1 && bars.Count == 0)
                report.Skip(f.Line, "", new I18nText("More than " + FigureSpec.MaxPanels + " panels: every axes becomes its own chart.", "Mehr als " + FigureSpec.MaxPanels + " Felder: jede Achse wird ein eigenes Diagramm."));
            foreach (var a in axes.OrderBy(a => a.Row).ThenBy(a => a.Col))
            {
                SketchObject? o;
                I18nText summary;
                if (a.Pie is not null) o = PieOf(registry, f, a, report, out summary);
                else if (a.Polar) o = PolarOf(registry, f, a, report, out summary);
                else if (a.Bars.Count > 0) o = BarChartOf(registry, f, a, report, out summary);
                else o = Figure(registry, f, new List<ImpAxes> { a }, report, false, out summary);
                if (o is not null) yield return (o, summary);
            }
        }

        // ------------------------------------------------------------------ figure charts

        private static double? Lim(double? v) => v is { } d && Fmt.Finite(d) ? d : null;

        /// <summary>Data range of the axes (for axes fractions): explicit limits or null.</summary>
        private static (double Min, double Max)? Range(double? min, double? max) => min is { } a && max is { } b && a != b ? (a, b) : null;

        private static double FromFrac(double frac, (double Min, double Max) r, bool log)
            => log && r.Min > 0 && r.Max > 0 ? Math.Pow(10, Math.Log10(r.Min) + frac * (Math.Log10(r.Max) - Math.Log10(r.Min))) : r.Min + frac * (r.Max - r.Min);

        private static SketchObject? Figure(Registry registry, ImpFigure f, List<ImpAxes> panels, CodeImportReport report, bool whole, out I18nText summary)
        {
            summary = new I18nText("");
            var type = registry.Type(FigureChart.TypeId);
            if (type is null || panels.Count == 0) return null;
            var spec = new FigureSpec();
            var n = panels.Count;
            var bottom = panels[n - 1];
            spec.Title = Text(f.Title ?? (n == 1 ? panels[0].Title : null));
            spec.Caption = Text(f.Caption ?? (f.SupXLabel is not null && panels.Any(p => p.XLabel is not null) ? f.SupXLabel : null));
            var xLabel = bottom.XLabel ?? panels.Select(p => p.XLabel).LastOrDefault(l => l is not null) ?? (panels.All(p => p.XLabel is null) ? f.SupXLabel : null);
            spec.X.Label = Text(xLabel);
            spec.X.Scale = panels.Any(p => p.XScale == "log") ? "log" : "linear";
            var xmin = panels.Select(p => Lim(p.XMin)).LastOrDefault(v => v is not null);
            var xmax = panels.Select(p => Lim(p.XMax)).LastOrDefault(v => v is not null);
            if (xmin is { } x0 && xmax is { } x1 && x0 > x1)
            {
                report.Skip(bottom.Line, "", new I18nText("A reversed x axis is not imported; the axis runs left to right.", "Eine umgekehrte x-Achse wird nicht importiert; die Achse läuft von links nach rechts."));
                (xmin, xmax) = (x1, x0);
            }
            spec.X.Min = xmin; spec.X.Max = xmax;
            spec.X.Grid = panels.Any(p => p.XGrid == true);
            if (panels.Any(p => p.XArrow)) spec.X.Mode = "principle";
            var xTicks = panels.LastOrDefault(p => p.XTicks is not null);
            if (xTicks is not null) Marks(spec.X, xTicks.XTicks!, xTicks.XTickLabels, xTicks.Line, report);
            var distinctX = panels.Select(p => (Lim(p.XMin), Lim(p.XMax))).Where(r => r.Item1 is not null || r.Item2 is not null).Distinct().Count();
            if (distinctX > 1) report.Skip(f.Line, "", new I18nText("The panels have different x ranges; they share one x axis now.", "Die Felder haben verschiedene x-Bereiche; sie teilen jetzt eine x-Achse."));
            var legend = f.Legend || panels.Any(p => p.Legend);
            spec.Legend = legend ? (panels.Any(p => p.Legend && p.LegendWhere == "right") || f.LegendWhere == "right" ? "right" : "inside") : "none";
            var hidden = new List<int>();
            var edges = new List<int>();
            var fills = 0; var hlines = 0; var vlines = 0; var notes = 0; var spans = 0;
            for (var i = 0; i < n; i++)
            {
                var a = panels[i];
                var p = new FigPanel { Scale = a.YScale, Label = Text(a.YLabel), Grid = a.YGrid == true, Share = a.Share > 0 ? a.Share : 1 };
                var ymin = Lim(a.YMin); var ymax = Lim(a.YMax);
                if (ymin is { } y0 && ymax is { } y1 && y0 > y1)
                {
                    report.Skip(a.Line, "", new I18nText("A reversed y axis is not imported; the axis runs upwards.", "Eine umgekehrte y-Achse wird nicht importiert; die Achse läuft nach oben."));
                    (ymin, ymax) = (y1, y0);
                }
                p.Min = ymin; p.Max = ymax;
                if (a.YArrow) p.Mode = "principle";
                if (a.YTicks is not null) Marks(p, a.YTicks, a.YTickLabels, a.Line, report);
                if (n > 1) p.Title = Text(a.Title);
                spec.Panels.Add(p);
                var offset = spec.Series.Count;
                foreach (var se in a.Series)
                {
                    if (spec.Series.Count >= FigureSpec.MaxSeries)
                    {
                        report.Skip(a.Line, "", new I18nText("More than " + FigureSpec.MaxSeries + " curves: the rest is not imported.", "Mehr als " + FigureSpec.MaxSeries + " Kurven: der Rest wird nicht importiert."));
                        break;
                    }
                    se.Panel = i;
                    Thin(se, report, a.Line);
                    spec.Series.Add(se);
                }
                foreach (var h in a.HiddenLines) if (offset + h < spec.Series.Count) hidden.Add(offset + h);
                foreach (var r in a.Refs)
                {
                    r.Panel = r.Kind == "v" && n == 1 ? null : i;
                    spec.Refs.Add(r);
                    if (r.Kind == "h") hlines++; else vlines++;
                }
                foreach (var r in a.Spans) { r.Panel = n == 1 ? null : i; spec.Regions.Add(r); spans++; }
                // Filled areas: the bounding curves are the plotted series where they match, else invisible edge curves.
                foreach (var fill in a.Fills)
                {
                    var region = Resolve(spec, a, i, offset, fill, hidden, edges, report);
                    if (region is not null) { spec.Regions.Add(region); fills++; }
                }
                foreach (var note in a.Notes)
                {
                    if (Resolve(note, a, spec, i, i, report) is { } fn) { spec.Notes.Add(fn); notes++; }
                }
            }
            if (!whole && f.PanelLines.Count + f.CrossNotes.Count > 0)
                report.Skip(f.Line, "", new I18nText("Lines and arrows between subplots side by side are not imported.", "Linien und Pfeile zwischen nebeneinanderliegenden Teildiagrammen werden nicht importiert."));
            foreach (var (x, from, to, r) in whole ? f.PanelLines : new List<(double, int, int, FigRef)>())
            {
                r.Kind = "v"; r.X = x;
                r.Panel = from <= 0 && to >= n - 1 ? null : Math.Max(0, Math.Min(n - 1, from));
                if (r.Panel is not null && to != from)
                    report.Skip(f.Line, "", new I18nText("A vertical line through some of the panels is drawn in its first panel only.", "Eine senkrechte Linie durch einige Felder wird nur im ersten Feld gezeichnet."));
                spec.Refs.Add(r);
                vlines++;
            }
            foreach (var note in whole ? f.CrossNotes : new List<ImpNote>())
            {
                var p1 = Math.Max(0, Math.Min(n - 1, note.Note.P1.Panel));
                var p2 = Math.Max(0, Math.Min(n - 1, note.Note.P2?.Panel ?? p1));
                if (Resolve(note, panels[p1], spec, p1, p2, report, panels[p2]) is { } fn) { spec.Notes.Add(fn); notes++; }
            }
            if (spec.Series.Count == 0 && spec.Refs.Count == 0 && spec.Regions.Count == 0 && spec.Notes.Count == 0 && spec.X.Label is null && spec.Panels.All(p => p.Label is null) && !panels.Any(p => p.HasRange))
                return null;
            if (legend && edges.Count > 0)
                report.Skip(f.Line, "", new I18nText("The legend also lists the invisible edge curves of a filled area.", "Die Legende führt auch die unsichtbaren Randkurven einer Fläche auf."));

            var size = SizeOf(f, n);
            var o = type.Create(new Vec2(0, 0)) with { Size = size };
            o = o.WithParam("spec", spec.ToJson());
            if (hidden.Count > 0)
            {
                var parts = new Dictionary<string, PartLook>(StringComparer.Ordinal);
                foreach (var h in hidden.Distinct()) parts[ChartTags.Nth(ChartTags.Series, h)] = new PartLook { Hidden = true };
                o = o with { Parts = parts };
            }
            var counts = new List<(int N, string En, string De)>
            {
                (spec.Series.Count - hidden.Distinct().Count(h => h < spec.Series.Count && spec.Series[h].Marker is null), "curve", "Kurve"),
                (hlines, "value line", "Wertlinie"), (vlines, "vertical line", "senkrechte Linie"), (fills + spans, "filled area", "Fläche"), (notes, "note", "Anmerkung"),
            };
            summary = Summary(n > 1 ? new I18nText(n + " stacked panels", n + " gestapelte Felder") : new I18nText("figure", "Abbildung"), counts);
            return o;
        }

        /// <summary>Ticks of a curve axis: named marks (ticks with labels), none (principle axes); other tick settings are reported.</summary>
        private static void Marks(FigAxis axis, List<double> ticks, List<string>? labels, int line, CodeImportReport report)
        {
            if (ticks.Count == 0)
            {
                if (!axis.Principle) report.Skip(line, "", new I18nText("Hidden ticks are not imported.", "Ausgeblendete Teilstriche werden nicht importiert."));
                return;
            }
            if (labels is null || labels.Count != ticks.Count)
            {
                report.Skip(line, "", new I18nText("Tick positions without labels are not imported (the ticks are automatic).", "Teilstrichpositionen ohne Beschriftung werden nicht importiert (die Teilstriche sind automatisch)."));
                return;
            }
            for (var i = 0; i < ticks.Count; i++)
                if (Fmt.Finite(ticks[i])) axis.Marks.Add(new FigMark(ticks[i], new I18nText(labels[i])));
        }

        /// <summary>Curves longer than the figure limit are thinned (every k-th point, the last one kept).</summary>
        private static void Thin(FigSeries se, CodeImportReport report, int line)
        {
            var count = Math.Min(se.X.Count, se.Y.Count);
            if (count <= FigureSpec.MaxPoints) return;
            var k = (int)Math.Ceiling(count / (double)(FigureSpec.MaxPoints - 1));
            var xs = new List<double>(); var ys = new List<double>();
            for (var i = 0; i < count; i += k) { xs.Add(se.X[i]); ys.Add(se.Y[i]); }
            if ((count - 1) % k != 0) { xs.Add(se.X[count - 1]); ys.Add(se.Y[count - 1]); }
            se.X = xs; se.Y = ys;
            report.Skip(line, "", new I18nText("A curve with " + count + " points was thinned to " + xs.Count + " points.", "Eine Kurve mit " + count + " Punkten wurde auf " + xs.Count + " Punkte ausgedünnt."));
        }

        private static I18nText? Text(string? s) => string.IsNullOrWhiteSpace(s) ? null : new I18nText(s!.Trim());

        private static Size2 SizeOf(ImpFigure f, int panels)
        {
            var w = f.WidthMm is { } fw && fw > 10 ? fw : 110;
            var h = f.HeightMm is { } fh && fh > 10 ? fh : panels > 1 ? Math.Min(160, 30 + 38 * panels) : 75;
            if (f.Cols > 1 && f.WidthMm is not null) w /= f.Cols;
            if (f.Rows > 1 && f.HeightMm is not null && (f.Cols > 1 || f.Axes.Any(a => a.Bars.Count > 0))) h /= f.Rows;
            return new Size2(Math.Round(Math.Max(40, Math.Min(400, w)), 1), Math.Round(Math.Max(30, Math.Min(300, h)), 1));
        }

        internal static I18nText Summary(I18nText kind, IEnumerable<(int N, string En, string De)> counts)
        {
            var en = new List<string>(); var de = new List<string>();
            foreach (var (c, e, d) in counts.Where(x => x.N > 0))
            {
                en.Add(c + " " + (c == 1 || e.EndsWith("series", StringComparison.Ordinal) ? e : e.EndsWith("y", StringComparison.Ordinal) ? e.Substring(0, e.Length - 1) + "ies" : e + "s"));
                de.Add(c + " " + (c == 1 ? d : Plural(d)));
            }
            return new I18nText(kind.En + (en.Count > 0 ? " with " + string.Join(", ", en) : ""), kind.Get(Lang.De) + (de.Count > 0 ? " mit " + string.Join(", ", de) : ""));
        }

        private static string Plural(string de) => de switch
        {
            "Kurve" => "Kurven", "Wertlinie" => "Wertlinien", "senkrechte Linie" => "senkrechten Linien", "Fläche" => "Flächen", "Anmerkung" => "Anmerkungen",
            "Balkenreihe" => "Balkenreihen", "Kategorie" => "Kategorien", "Segment" => "Segmente", _ => de,
        };

        /// <summary>Index of a series of the panel whose curve passes through every (x, y) of the data (within a small tolerance).</summary>
        private static int Match(FigureSpec spec, int panel, int offset, double[] xs, double[] ys)
        {
            for (var k = offset; k < spec.Series.Count; k++)
            {
                var se = spec.Series[k];
                if (se.Panel != panel) continue;
                var pts = Enumerable.Range(0, Math.Min(se.X.Count, se.Y.Count)).Where(i => Fmt.Finite(se.X[i]) && Fmt.Finite(se.Y[i])).ToList();
                if (pts.Count < 2) continue;
                var sx = pts.Select(i => se.X[i]).ToList(); var sy = pts.Select(i => se.Y[i]).ToList();
                var span = Math.Max(1e-12, sy.Max() - sy.Min());
                var lo = sx.Min(); var hi = sx.Max();
                var ok = true; var any = false;
                for (var i = 0; i < Math.Min(xs.Length, ys.Length) && ok; i++)
                {
                    if (!Fmt.Finite(xs[i]) || !Fmt.Finite(ys[i])) continue;
                    if (xs[i] < lo - 1e-9 * Math.Max(1, Math.Abs(lo)) || xs[i] > hi + 1e-9 * Math.Max(1, Math.Abs(hi))) { ok = false; break; }
                    var y = Interp(sx, sy, xs[i]);
                    if (!Fmt.Finite(y) || Math.Abs(y - ys[i]) > 1e-6 * span + 1e-9) ok = false;
                    any = true;
                }
                if (ok && any) return k;
            }
            return -1;
        }

        private static double Interp(List<double> xs, List<double> ys, double x)
        {
            for (var i = 0; i < xs.Count; i++)
            {
                if (xs[i] == x) return ys[i];
                if (i > 0 && (xs[i - 1] - x) * (xs[i] - x) < 0)
                    return ys[i - 1] + (ys[i] - ys[i - 1]) * (x - xs[i - 1]) / (xs[i] - xs[i - 1]);
            }
            return double.NaN;
        }

        private static FigRegion? Resolve(FigureSpec spec, ImpAxes a, int panel, int offset, ImpFill fill, List<int> hidden, List<int> edges, CodeImportReport report)
        {
            var r = fill.Region;
            r.Panel = null;
            if (fill.Label is { Length: > 0 } label && !label.StartsWith("_", StringComparison.Ordinal)) r.Label ??= new I18nText(label);
            // fill_betweenx: the edges are curves x(y) – their points are (edge value, y).
            int Edge(double[] values)
            {
                double[] xs = fill.Horizontal ? values : fill.X, ys = fill.Horizontal ? fill.X : values;
                var k = Match(spec, panel, offset, xs, ys);
                if (k >= 0) return k;
                if (spec.Series.Count >= FigureSpec.MaxSeries) return -1;
                spec.Series.Add(new FigSeries { X = xs.ToList(), Y = ys.ToList(), Panel = panel, Color = r.Color, Width = 0.1 });
                hidden.Add(spec.Series.Count - 1);
                edges.Add(spec.Series.Count - 1);
                return spec.Series.Count - 1;
            }
            if (fill.SeriesA is { } sa)
            {
                r.Kind = "between"; r.A = offset + sa; r.B = offset + (fill.SeriesB ?? sa);
                return r;
            }
            var a1 = Edge(fill.Y1);
            if (a1 < 0) return null;
            // The where-mask as "curve A above (right of) the other edge", "below (left of)" or everywhere; null if it is neither.
            string? WhereOf(Func<int, double> other)
            {
                if (fill.Where is not { } w || w.All(x => x)) return "";
                var n = Math.Min(w.Length, fill.Y1.Length);
                bool Same(Func<int, bool> f) => Enumerable.Range(0, n).All(i => !Fmt.Finite(fill.Y1[i]) || !Fmt.Finite(other(i)) || w[i] == f(i));
                if (Same(i => fill.Y1[i] >= other(i)) || Same(i => fill.Y1[i] > other(i))) return "above";
                if (Same(i => fill.Y1[i] <= other(i)) || Same(i => fill.Y1[i] < other(i))) return "below";
                return null;
            }
            I18nText NotRead() => new("This where-condition of a filled area is not imported (only “curve above/below the other edge” is read).",
                "Diese where-Bedingung einer Fläche wird nicht importiert (nur „Kurve über/unter dem anderen Rand“ wird gelesen).");
            if (fill.Y2 is null)
            {
                var where = WhereOf(_ => fill.Y2Const);
                if (where is null) { report.Skip(fill.Line, fill.Code, NotRead()); return null; }
                r.A = a1;
                if (fill.Horizontal) { r.Kind = "betweenx"; r.X2 = fill.Y2Const; r.Where = where.Length == 0 ? null : where; }
                else if (where.Length == 0) { r.Kind = "below"; r.Base = fill.Y2Const; }
                else { r.Kind = "between"; r.Y2 = fill.Y2Const; r.Where = where; }
                return r;
            }
            var y2 = fill.Y2;
            var b1 = Edge(y2);
            if (b1 < 0) return null;
            r.Kind = fill.Horizontal ? "betweenx" : "between"; r.A = a1; r.B = b1;
            var w2 = WhereOf(i => i < y2.Length ? y2[i] : double.NaN);
            if (w2 is null) { report.Skip(fill.Line, fill.Code, NotRead()); return null; }
            if (w2 == "below" && !fill.Horizontal) { r.A = b1; r.B = a1; r.Where = "above"; }
            else r.Where = w2.Length == 0 ? null : w2;
            return r;
        }

        private static FigNote? Resolve(ImpNote n, ImpAxes a, FigureSpec spec, int panel1, int panel2, CodeImportReport report, ImpAxes? a2 = null)
        {
            a2 ??= a;
            var note = n.Note;
            var xr = Range(spec.X.Min, spec.X.Max);
            var xlog = spec.X.Scale == "log";
            (double, double)? Yr(int p) => p < spec.Panels.Count ? Range(spec.Panels[p].Min, spec.Panels[p].Max) : null;
            bool Ylog(int p) => p < spec.Panels.Count && spec.Panels[p].Scale == "log";
            bool Fix(FigPoint pt, bool xf, bool yf, int p)
            {
                pt.Panel = p;
                if (xf)
                {
                    if (xr is not { } r) return false;
                    pt.X = FromFrac(pt.X, r, xlog);
                }
                if (yf)
                {
                    if (Yr(p) is not { } r || pt.Y is not { } y) return false;
                    pt.Y = FromFrac(y, r, Ylog(p));
                }
                return true;
            }
            if (!Fix(note.P1, n.XFrac, n.YFrac, panel1) || note.P2 is { } p2 && !Fix(p2, n.X2Frac, n.Y2Frac, panel2))
            {
                report.Skip(n.Line, n.Code, new I18nText("A note in axes coordinates needs fixed axis ranges (set_xlim/set_ylim); it is not imported.",
                    "Eine Anmerkung in Achskoordinaten braucht feste Achsbereiche (set_xlim/set_ylim); sie wird nicht importiert."));
                return null;
            }
            return note;
        }

        // ------------------------------------------------------------------ bar charts

        private static SketchObject? BarChartOf(Registry registry, ImpFigure f, ImpAxes a, CodeImportReport report, out I18nText summary)
        {
            summary = new I18nText("");
            var type = registry.Type(BarChart.TypeId);
            if (type is null) return null;
            var horizontal = a.Bars.All(b => b.Horizontal);
            if (a.Bars.Any(b => b.Horizontal) && !horizontal)
                report.Skip(a.Line, "", new I18nText("Vertical and horizontal bars in one axes: shown as columns.", "Senkrechte und waagerechte Balken in einer Achse: als Säulen dargestellt."));
            if (a.Series.Count > 0 || a.Fills.Count > 0 || a.Refs.Count > 0 || a.Spans.Count > 0)
                report.Skip(a.Line, "", new I18nText("Curves, value lines and areas next to bars are not imported (a bar chart shows bars only).", "Kurven, Wertlinien und Flächen neben Balken werden nicht importiert (ein Balkendiagramm zeigt nur Balken)."));
            // Categories.
            var cats = new List<string>();
            var idx = new Dictionary<string, int>(StringComparer.Ordinal);
            int Cat(string s) { if (!idx.TryGetValue(s, out var i)) { i = cats.Count; cats.Add(s); idx[s] = i; } return i; }
            var ticks = horizontal ? a.YTicks : a.XTicks;
            var tickLabels = horizontal ? a.YTickLabels : a.XTickLabels;
            var series = new List<(string? Name, Dictionary<int, double> Values, bool Stacked)>();
            var numericPos = a.Bars.Where(b => b.Cats is null).SelectMany(b => b.Pos).Where(Fmt.Finite).Distinct().OrderBy(v => v).ToList();
            // Numeric positions: category centres at the ticks with labels, else at the nearest integers (grouped bars are offset).
            var centres = new List<double>();
            List<string>? centreNames = null;
            if (numericPos.Count > 0)
            {
                if (ticks is { Count: > 0 } && tickLabels is { } tl && tl.Count == ticks.Count) { centres = ticks.ToList(); centreNames = tl; }
                else if (numericPos.All(p => Math.Abs(p - Math.Round(p)) <= 0.45)) centres = numericPos.Select(p => Math.Round(p)).Distinct().OrderBy(v => v).ToList();
                else centres = a.Bars.First(b => b.Cats is null).Pos.Distinct().OrderBy(v => v).ToList();
                for (var i = 0; i < centres.Count; i++) Cat(centreNames is not null && i < centreNames.Count ? centreNames[i] : Num(centres[i]));
            }
            foreach (var b in a.Bars)
            {
                var vals = new Dictionary<int, double>();
                for (var i = 0; i < b.Values.Length; i++)
                {
                    int c;
                    if (b.Cats is not null) c = Cat(i < b.Cats.Length ? b.Cats[i] : Fmt.I(i + 1));
                    else
                    {
                        if (i >= b.Pos.Length || !Fmt.Finite(b.Pos[i])) continue;
                        var p = b.Pos[i];
                        c = Enumerable.Range(0, centres.Count).OrderBy(j => Math.Abs(centres[j] - p)).FirstOrDefault();
                    }
                    vals[c] = b.Values[i];
                }
                series.Add((b.Label, vals, b.Bottom is { } bt && bt.Any(v => Fmt.Finite(v) && v != 0)));
            }
            var stacked = a.BarStacked ?? series.Any(s => s.Stacked);
            var sb = new StringBuilder();
            string Field(string s) => "\"" + s.Replace("\"", "'").Replace(";", ",").Replace("|", "/").Replace("\n", " ").Trim() + "\"";
            var named = series.Any(s => !string.IsNullOrWhiteSpace(s.Name));
            if (named)
            {
                var header = new List<string> { Field((horizontal ? a.YLabel : a.XLabel) ?? "") };
                for (var k = 0; k < series.Count; k++) header.Add(Field(series[k].Name ?? new I18nText("Series", "Reihe").En + " " + Fmt.I(k + 1)));
                if (header.Count == 2 && header[0] == "\"\"") header[0] = "\"x\"";
                sb.Append(string.Join(";", header)).Append('\n');
            }
            for (var c = 0; c < cats.Count; c++)
            {
                var row = new List<string> { Field(cats[c]) };
                foreach (var s in series) row.Add(s.Values.TryGetValue(c, out var v) && Fmt.Finite(v) ? Num(v) : "");
                sb.Append(string.Join(";", row)).Append('\n');
            }
            var o = type.Create(new Vec2(0, 0));
            o = o with { Size = SizeOf(f, 1) };
            o = o.WithParam("data", sb.ToString().TrimEnd('\n'))
                .WithParam("orient", horizontal ? "bar" : "column")
                .WithParam("mode", stacked ? "stacked" : "grouped")
                .WithParam("values", a.BarValues);
            var title = f.Title ?? a.Title;
            if (!string.IsNullOrWhiteSpace(title)) o = o.WithParam("title", title!.Trim());
            var catLabel = horizontal ? a.YLabel : a.XLabel;
            var valLabel = horizontal ? a.XLabel : a.YLabel;
            if (catLabel is not null) o = o.WithParam("xlabel", catLabel);
            if (valLabel is not null) o = o.WithParam("ylabel", valLabel).WithParam("yunit", "");
            var vmin = horizontal ? a.XMin : a.YMin; var vmax = horizontal ? a.XMax : a.YMax;
            if (Lim(vmin) is { } lo && Lim(vmax) is { } hi && lo != hi) o = o.WithParam("yrange", Num(Math.Min(lo, hi)) + ";" + Num(Math.Max(lo, hi)));
            var legend = a.Legend || f.Legend;
            o = o.WithParam("legend", legend ? (a.LegendWhere == "right" || f.LegendWhere == "right" ? "right" : "inside") : series.Count > 1 && named ? "auto" : "none");
            o = o.WithParam("grid", (horizontal ? a.XGrid : a.YGrid) == true);
            summary = Summary(new I18nText(horizontal ? "bar chart" : "column chart", horizontal ? "Balkendiagramm" : "Säulendiagramm"),
                new[] { (series.Count, "bar series", "Balkenreihe"), (cats.Count, "category", "Kategorie") });
            return o;
        }

        internal static string Num(double v) => ExportText.N(v);

        private static string Field(string s) => "\"" + s.Replace("\"", "'").Replace(";", ",").Replace("|", "/").Replace("\n", " ").Trim() + "\"";

        // ------------------------------------------------------------------ pie and polar charts

        private static SketchObject? PieOf(Registry registry, ImpFigure f, ImpAxes a, CodeImportReport report, out I18nText summary)
        {
            summary = new I18nText("");
            var type = registry.Type(PieChart.TypeId);
            var pie = a.Pie!;
            if (type is null) return null;
            if (a.Series.Count > 0 || a.Bars.Count > 0 || a.Fills.Count > 0 || a.Refs.Count > 0 || a.Spans.Count > 0)
                report.Skip(a.Line, "", new I18nText("Curves, bars and lines next to a pie are not imported (a pie chart shows the pie only).", "Kurven, Balken und Linien neben einem Kreis werden nicht importiert (ein Kreisdiagramm zeigt nur den Kreis)."));
            var rows = new List<string>();
            for (var i = 0; i < pie.Values.Length; i++)
            {
                var label = pie.Labels is { } l && i < l.Length && !string.IsNullOrWhiteSpace(l[i]) ? l[i] : Fmt.I(i + 1);
                rows.Add(Field(label) + ";" + (Fmt.Finite(pie.Values[i]) ? Num(pie.Values[i]) : ""));
            }
            var o = type.Create(new Vec2(0, 0)) with { Size = SizeOf(f, 1) };
            // matplotlib: degrees counter-clockwise from 3 o'clock; here: degrees clockwise from 12 o'clock.
            var start = ((90 - pie.StartAngle) % 360 + 360) % 360;
            if (start > 180) start -= 360;
            var labels = pie.Labels is not null ? (pie.Percent ? "namepercent" : "name") : pie.Percent ? "percent" : "none";
            o = o.WithParam("data", string.Join("\n", rows))
                .WithParam("hole", Math.Round(Geo.Clamp(pie.Hole, 0, 0.9), 3))
                .WithParam("start", Math.Round(start, 3))
                .WithParam("direction", pie.CounterClock ? "ccw" : "cw")
                .WithParam("labels", labels);
            var legend = a.Legend || f.Legend;
            o = o.WithParam("legend", legend ? "right" : labels is "name" or "namepercent" ? "auto" : "none");
            var title = f.Title ?? a.Title;
            if (!string.IsNullOrWhiteSpace(title)) o = o.WithParam("title", title!.Trim());
            if (pie.Values.Any(v => !(v > 0))) report.Skip(pie.Line, "", new I18nText("Slices without a positive value are not drawn.", "Segmente ohne positiven Wert werden nicht gezeichnet."));
            summary = Summary(new I18nText(pie.Hole > 0 ? "donut chart" : "pie chart", pie.Hole > 0 ? "Ringdiagramm" : "Kreisdiagramm"), new[] { (pie.Values.Length, "slice", "Segment") });
            return o;
        }

        private static SketchObject? PolarOf(Registry registry, ImpFigure f, ImpAxes a, CodeImportReport report, out I18nText summary)
        {
            summary = new I18nText("");
            var type = registry.Type(PolarChart.TypeId);
            if (type is null) return null;
            if (a.Bars.Count > 0 || a.Fills.Count > 0 || a.Refs.Count > 0 || a.Spans.Count > 0 || a.Notes.Count > 0)
                report.Skip(a.Line, "", new I18nText("Bars, areas, lines and notes on polar axes are not imported (the curves are).", "Balken, Flächen, Linien und Anmerkungen auf Polarachsen werden nicht importiert (die Kurven schon)."));
            var blocks = new List<string>();
            for (var k = 0; k < a.Series.Count; k++)
            {
                var se = a.Series[k];
                var lines = new List<string>();
                if (se.Label is { } l && l.En.Length > 0) lines.Add(l.En.Replace(";", ",").Replace("\n", " ").Trim() + ";");
                var n = Math.Min(se.X.Count, se.Y.Count);
                for (var i = 0; i < n; i++)
                    if (Fmt.Finite(se.X[i]) && Fmt.Finite(se.Y[i])) lines.Add(Num(Math.Round(se.X[i] * 180 / Math.PI, 9)) + ";" + Num(se.Y[i]));
                if (lines.Count > 0) blocks.Add(string.Join("\n", lines));
            }
            var o = type.Create(new Vec2(0, 0)) with { Size = SizeOf(f, 1) };
            var markersOnly = a.Series.Count > 0 && a.HiddenLines.Count == a.Series.Count;
            var markers = a.Series.Any(s => s.Marker is not null);
            var style = a.PolarFill ? "area" : markersOnly ? "points" : markers ? "linepoints" : "line";
            // A curve whose last point repeats the first is closed; otherwise it stays open (as matplotlib draws it).
            var closed = a.Series.Count > 0 && a.Series.All(s => s.X.Count > 2 && Math.Abs(s.Y[0] - s.Y[s.Y.Count - 1]) < 1e-9
                && Math.Abs(Math.IEEERemainder(s.X[0] - s.X[s.X.Count - 1], 2 * Math.PI)) < 1e-6) || a.PolarFill;
            o = o.WithParam("data", string.Join("\n\n", blocks)).WithParam("style", style).WithParam("closed", closed);
            if (a.PolarTop != a.PolarClockwise)
                report.Skip(a.Line, "", new I18nText("This combination of the 0° direction and the angle direction is drawn with 0° right and counter-clockwise angles, or with 0° up and clockwise angles.",
                    "Diese Kombination aus 0°-Richtung und Winkelrichtung wird mit 0° rechts und Winkeln gegen den Uhrzeigersinn oder 0° oben und Winkeln im Uhrzeigersinn gezeichnet."));
            o = o.WithParam("zero", a.PolarTop ? "top" : "right");
            if (Lim(a.YMin) is { } lo && Lim(a.YMax) is { } hi && lo != hi) o = o.WithParam("rrange", Num(Math.Min(lo, hi)) + ";" + Num(Math.Max(lo, hi)));
            else if (Lim(a.YMax) is { } top && top > 0) o = o.WithParam("rrange", "0;" + Num(top));
            var title = f.Title ?? a.Title;
            if (!string.IsNullOrWhiteSpace(title)) o = o.WithParam("title", title!.Trim());
            var legend = a.Legend || f.Legend;
            o = o.WithParam("legend", legend ? "right" : "none");
            o = o.WithParam("grid", a.XGrid != false && a.YGrid != false);
            summary = Summary(new I18nText("polar chart", "Polardiagramm"), new[] { (a.Series.Count, "curve", "Kurve") });
            return o;
        }
    }
}
