using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Charts
{
    /// <summary>Five-number summary of a sample with whiskers and outliers (box plot).</summary>
    internal sealed record BoxStats(double Min, double Q1, double Median, double Q3, double Max, double Low, double High, IReadOnlyList<double> Outliers, int N)
    {
        public double Iqr => Q3 - Q1;
    }

    /// <summary>Descriptive statistics of raw samples: quantiles, box plot summary, histogram bins.</summary>
    internal static class Stats
    {
        /// <summary>
        /// Quantile <paramref name="p"/> (0 … 1) of a sorted sample with linear interpolation between the order statistics
        /// (Hyndman and Fan type 7, the default of numpy, matplotlib and spreadsheet QUARTILE.INC).
        /// </summary>
        public static double Quantile(IReadOnlyList<double> sorted, double p)
        {
            if (sorted.Count == 0) return double.NaN;
            if (sorted.Count == 1) return sorted[0];
            var h = (sorted.Count - 1) * Geo.Clamp(p, 0, 1);
            var lo = (int)Math.Floor(h);
            var hi = Math.Min(lo + 1, sorted.Count - 1);
            return sorted[lo] + (h - lo) * (sorted[hi] - sorted[lo]);
        }

        /// <summary>
        /// Box plot summary: quartiles, whiskers at the furthest samples within <paramref name="whis"/> × IQR beyond the box
        /// (Tukey; matplotlib's default 1.5) and the samples beyond them as outliers; without <paramref name="outliers"/> the
        /// whiskers reach the minimum and maximum. Null for an empty sample.
        /// </summary>
        public static BoxStats? Box(IEnumerable<double> samples, double whis = 1.5, bool outliers = true)
        {
            var s = samples.Where(Fmt.Finite).OrderBy(v => v).ToList();
            if (s.Count == 0) return null;
            double q1 = Quantile(s, 0.25), med = Quantile(s, 0.5), q3 = Quantile(s, 0.75);
            if (!outliers || !(whis >= 0) || !Fmt.Finite(whis)) return new BoxStats(s[0], q1, med, q3, s[s.Count - 1], s[0], s[s.Count - 1], Array.Empty<double>(), s.Count);
            var iqr = q3 - q1;
            double lowFence = q1 - whis * iqr, highFence = q3 + whis * iqr;
            var inside = s.Where(v => v >= lowFence - 1e-12 && v <= highFence + 1e-12).ToList();
            var low = inside.Count > 0 ? Math.Min(inside[0], q1) : q1;
            var high = inside.Count > 0 ? Math.Max(inside[inside.Count - 1], q3) : q3;
            var outs = s.Where(v => v < low || v > high).ToList();
            return new BoxStats(s[0], q1, med, q3, s[s.Count - 1], low, high, outs, s.Count);
        }

        /// <summary>Sturges' rule: ⌈log₂ n⌉ + 1 bins (at least 1).</summary>
        public static int SturgesBins(int n) => n <= 1 ? 1 : (int)Math.Ceiling(Math.Log(n, 2) + 1 - 1e-12);

        /// <summary>
        /// Equal-width bins over the range (the samples' minimum to maximum unless given) and the count of every series per
        /// bin; a bin holds [left, right), the last one includes its right edge (like numpy.histogram). Samples outside the
        /// range are not counted.
        /// </summary>
        public static (double[] Edges, int[][] Counts) Histogram(IReadOnlyList<IReadOnlyList<double>> series, int bins, (double Min, double Max)? range = null)
        {
            bins = Math.Max(1, Math.Min(200, bins));
            var all = series.SelectMany(s => s).Where(Fmt.Finite).ToList();
            double lo, hi;
            if (range is { } r && r.Max > r.Min) { lo = r.Min; hi = r.Max; }
            else if (all.Count == 0) { lo = 0; hi = 1; }
            else { lo = all.Min(); hi = all.Max(); }
            if (!(hi > lo)) { lo -= 0.5; hi += 0.5; }
            var w = (hi - lo) / bins;
            var edges = Enumerable.Range(0, bins + 1).Select(i => i == bins ? hi : lo + i * w).ToArray();
            var counts = series.Select(s =>
            {
                var c = new int[bins];
                foreach (var v in s)
                {
                    if (!Fmt.Finite(v) || v < lo || v > hi) continue;
                    var i = v >= hi ? bins - 1 : (int)Math.Floor((v - lo) / w);
                    c[Math.Max(0, Math.Min(bins - 1, i))]++;
                }
                return c;
            }).ToArray();
            return (edges, counts);
        }

        /// <summary>The colour mixed with white (<paramref name="t"/> = 1: white).</summary>
        public static string Tint(string hex, double t)
        {
            var h = hex.TrimStart('#');
            if (h.Length != 6 || !int.TryParse(h, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v)) return hex;
            int Mix(int c) => (int)Math.Round(c + (255 - c) * Geo.Clamp(t, 0, 1));
            return "#" + Mix((v >> 16) & 255).ToString("x2", CultureInfo.InvariantCulture) + Mix((v >> 8) & 255).ToString("x2", CultureInfo.InvariantCulture) + Mix(v & 255).ToString("x2", CultureInfo.InvariantCulture);
        }
    }

    /// <summary>
    /// Box plot: per category the box from the lower to the upper quartile with the median, whiskers to the furthest samples
    /// within 1.5 × IQR (<c>whis</c>) and the samples beyond as outliers – computed from the raw samples in the data. Rows
    /// "category;sample;sample…" (the bar chart's layout), or one column per category under a header row, or one block per
    /// category; several blocks with labelled rows are several series side by side.
    /// </summary>
    internal sealed class BoxChart : ChartType
    {
        public const string TypeId = "chart.box";

        public const string DefaultData = "A;4.1;4.8;5.0;5.3;5.5;5.9;6.2;6.4;7.0;9.6\nB;3.0;3.6;4.2;4.4;4.9;5.1;5.6;6.0;6.3\nC;5.2;5.9;6.4;6.6;7.0;7.3;7.5;8.1;8.8;2.4";

        public static readonly ParamDef DataDef = Prm.Text("data", "Data (rows category;sample;sample…)", "Daten (Zeilen Kategorie;Wert;Wert…)", DefaultData);
        public static readonly ParamDef WhisDef = Prm.Num("whis", "Whiskers (× IQR)", "Whisker (× IQR)", 1.5, 0, 10, step: 0.5);
        public static readonly ParamDef OutliersDef = Prm.Bool("outliers", "Outliers", "Ausreißer", true);

        public BoxChart() : base(TypeId, new I18nText("Box plot", "Boxplot"), ChartCategories.Data,
            "box plot", "boxplot", "box", "whisker", "quartile", "median", "outlier", "distribution", "statistics", "spread",
            "boxplot", "kastengrafik", "box-whisker", "quartil", "median", "ausreißer", "verteilung", "statistik", "streuung")
        { }

        protected override IReadOnlyList<ParamDef> BuildParams() => new[]
        {
            DataDef, WhisDef, OutliersDef, Common.Title, Common.XLabel, Common.YLabel, Common.YUnit, Common.Grid, Common.Legend, Common.YRange, Common.Patterns, Common.Box,
        };

        /// <summary>Categories and per series (name, samples per category) of the data.</summary>
        internal static (List<string> Categories, List<(string? Name, Dictionary<int, List<double>> Samples)> Series) Groups(string? data)
        {
            var blocks = ChartData.Parse(data);
            var cats = new List<string>();
            var index = new Dictionary<string, int>(StringComparer.Ordinal);
            int Cat(string label) { if (!index.TryGetValue(label, out var i)) { i = cats.Count; cats.Add(label); index[label] = i; } return i; }
            var series = new List<(string? Name, Dictionary<int, List<double>> Samples)>();
            void Add(Dictionary<int, List<double>> d, int c, IEnumerable<double> vs)
            {
                if (!d.TryGetValue(c, out var l)) d[c] = l = new List<double>();
                l.AddRange(vs.Where(Fmt.Finite));
            }
            // Blocks of a single column without labels: one category per block (named by its header), one series.
            if (blocks.Count > 0 && blocks.All(b => !b.HasLabels && b.ValueColumns == 1))
            {
                var d = new Dictionary<int, List<double>>();
                for (var k = 0; k < blocks.Count; k++) Add(d, Cat(blocks[k].Name ?? blocks[k].ColumnName(0) ?? Fmt.I(k + 1)), blocks[k].Rows.SelectMany(r => r.Values));
                series.Add((null, d));
                return (cats, series);
            }
            foreach (var b in blocks)
            {
                var d = new Dictionary<int, List<double>>();
                if (b.HasLabels)
                    for (var i = 0; i < b.Rows.Count; i++) Add(d, Cat(b.Rows[i].Label ?? Fmt.I(i + 1)), b.Rows[i].Values);
                else
                    for (var j = 0; j < b.ValueColumns; j++) Add(d, Cat(b.ColumnName(j) ?? Fmt.I(j + 1)), b.Rows.Select(r => j < r.Values.Count ? r.Values[j] : double.NaN));
                series.Add((b.Name, d));
            }
            return (cats, series);
        }

        internal override Chart Compose(SketchObject o, RenderContext ctx)
        {
            var lang = ctx.Lang;
            var (cats, series) = Groups(o.Str("data", DefaultData));
            var whis = o.Num("whis", 1.5);
            var showOut = o.Flag("outliers", true);
            var stats = series.Select(s => cats.Select((_, i) => s.Samples.TryGetValue(i, out var v) ? Stats.Box(v, whis, showOut) : null).ToList()).ToList();
            var values = stats.SelectMany(l => l).Where(b => b is not null).SelectMany(b => new[] { b!.Low, b.High }.Concat(b.Outliers));
            var y = Axis.Linear(values, ChartData.Range(o.Str("yrange", "")));
            var x = Axis.Category(cats);
            var panel = new Panel(y, Common.AxisTitle(o, "ylabel", "yunit", "", null, lang));
            var f = new Frame(o.Size, Design, x, Common.AxisTitle(o, "xlabel", "xunit-none", "", null, lang), panel);
            Common.Apply(f, o, ctx);
            var mono = f.Mono || o.Flag("patterns", false);
            var m = series.Count;
            for (var k = 0; k < m; k++) f.Legend.Add(new LegendEntry(Common.SeriesName(series[k].Name, k, lang), BoxStyle(k, mono), Area: true));
            if (m < 2 && f.LegendMode == "auto") f.LegendMode = "none";
            f.Layout(lang);
            var c = new Chart(f, lang);
            var group = m == 1 ? 0.55 : Math.Min(0.84, 0.5 + 0.1 * m);
            double Y(double v) => Geo.Clamp(v, y.Min, y.Max);
            for (var k = 0; k < m; k++)
            {
                var look = SeriesLook.Of(k);
                var line = new Style { Stroke = mono ? Palette.Black : look.Color, StrokeWidth = 0.3, Role = LineRole.Visible };
                var tag = ChartTags.Nth(ChartTags.Box, k);
                var outliers = new List<Vec2>();
                var lines = new List<IReadOnlyList<Vec2>>();
                var medians = new List<IReadOnlyList<Vec2>>();
                for (var i = 0; i < cats.Count; i++)
                {
                    if (stats[k][i] is not { } b) continue;
                    var w = group / m;
                    var c0 = i + (1 - group) / 2 + k * w + w * 0.08;
                    var c1 = c0 + w * 0.84;
                    var mid = (c0 + c1) / 2;
                    var p0 = f.P(panel, c0, Y(b.Q3)); var p1 = f.P(panel, c1, Y(b.Q1));
                    c.Areas.Add(Prim.Rect(Math.Min(p0.X, p1.X), Math.Min(p0.Y, p1.Y), Math.Max(Math.Abs(p1.X - p0.X), 0.05), Math.Max(Math.Abs(p1.Y - p0.Y), 0.05), BoxStyle(k, mono)) with { Tag = tag });
                    var cap = (c1 - c0) * 0.25;
                    lines.Add(new[] { f.P(panel, mid, Y(b.Q3)), f.P(panel, mid, Y(b.High)) });
                    lines.Add(new[] { f.P(panel, mid - cap, Y(b.High)), f.P(panel, mid + cap, Y(b.High)) });
                    lines.Add(new[] { f.P(panel, mid, Y(b.Q1)), f.P(panel, mid, Y(b.Low)) });
                    lines.Add(new[] { f.P(panel, mid - cap, Y(b.Low)), f.P(panel, mid + cap, Y(b.Low)) });
                    medians.Add(new[] { f.P(panel, c0, Y(b.Median)), f.P(panel, c1, Y(b.Median)) });
                    outliers.AddRange(b.Outliers.Where(y.Contains).Select(v => f.P(panel, mid, v)));
                    c.Avoid.Add(new[] { f.P(panel, mid, Y(b.Low)), f.P(panel, mid, Y(b.High)) });
                    c.Port(f.P(panel, mid, Y(b.Median)), "s" + Fmt.I(k + 1) + "." + Fmt.I(i + 1));
                }
                if (Draw.MultiPath(lines, line, tag) is { } wl) c.Lines.Add(wl);
                if (Draw.MultiPath(medians, line with { StrokeWidth = 0.6 }, tag) is { } ml) c.Lines.Add(ml);
                if (outliers.Count > 0) c.Lines.Add(Draw.Markers(outliers, MarkerKind.Circle, false, mono ? Palette.Black : look.Color, f.Fs * 0.22) with { Tag = ChartTags.Nth(ChartTags.Outlier, k) });
            }
            return c;
        }

        /// <summary>Box fill: a light tint of the series colour with its colour as edge; in monochrome white, then patterns.</summary>
        internal static Style BoxStyle(int k, bool mono)
        {
            var look = SeriesLook.Of(k);
            if (mono) return new Style { Stroke = Palette.Black, StrokeWidth = 0.3, Role = LineRole.Visible, Fill = k == 0 ? Fill.Solid(Palette.White) : SeriesLook.Of(k + 1).MonoFill };
            return new Style { Stroke = look.Color, StrokeWidth = 0.3, Role = LineRole.Visible, Fill = Fill.Solid(Stats.Tint(look.Color, 0.78)) };
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang) => new[]
        {
            Common.DataInput(o, DefaultData) with { Input = Common.DataInput(o, DefaultData).Input! with
            {
                Label = new I18nText("Rows “category;sample;sample…” separated by |", "Zeilen „Kategorie;Wert;Wert…“ getrennt durch |"),
            } },
            ContextAction.WithInput("chart.whis", new I18nText("Whisker length…", "Whiskerlänge…"), IconIds.RangeY,
                new I18nText("Whiskers reach samples within this multiple of the box height (IQR)", "Whisker reichen bis zu Werten innerhalb dieses Vielfachen der Boxhöhe (IQR)"),
                Fmt.Num(o.Num("whis", 1.5), lang, 2), (x, text) => x.WithParam("whis", Fmt.ParseInput(text ?? "", lang, 0, 10))) with { Group = "param", ParamKeys = new[] { "whis" } },
            Prm.Toggle(o, "chart.outliers", "outliers", true, new I18nText("Outliers", "Ausreißer")),
            Common.GridToggle(o),
        }.Concat(Common.LegendChoice(o));
    }

    /// <summary>
    /// Histogram: the raw samples of the data (every number of a block; several blocks are several series side by side)
    /// counted in equal-width bins (<see cref="Stats.Histogram"/>); the bin count is automatic (Sturges' rule) or chosen.
    /// </summary>
    internal sealed class HistogramChart : ChartType
    {
        public const string TypeId = "chart.histogram";

        public const string DefaultData = "4.2;5.1;5.6;6.0;6.3;6.4;6.8;7.0;7.1;7.3\n7.4;7.6;7.8;7.9;8.1;8.2;8.4;8.6;8.9;9.1\n9.3;9.6;9.9;10.4;10.8;11.5;12.3;6.6;7.7;8.0";

        public static readonly ParamDef DataDef = Prm.Text("data", "Samples (any numbers, blank line = new series)", "Messwerte (beliebige Zahlen, Leerzeile = neue Reihe)", DefaultData);
        public static readonly ParamDef BinsDef = Prm.Num("bins", "Bins (0 = automatic)", "Klassen (0 = automatisch)", 0, 0, 200, step: 1);

        public HistogramChart() : base(TypeId, new I18nText("Histogram", "Histogramm"), ChartCategories.Data,
            "histogram", "frequency", "distribution", "bins", "classes", "samples", "statistics", "count",
            "histogramm", "häufigkeit", "häufigkeitsverteilung", "verteilung", "klassen", "messwerte", "statistik", "anzahl")
        { }

        protected override IReadOnlyList<ParamDef> BuildParams() => new[]
        {
            DataDef, BinsDef, Common.Title, Common.XLabel, Common.XUnit, Common.YLabel, Common.Grid, Common.Legend, Common.XRange, Common.YRange, Common.Patterns, Common.Box,
        };

        private static readonly double[] CountSteps = { 1, 2, 5, 10, 20, 25, 50, 100, 200, 250, 500, 1000, 2000, 2500, 5000, 10000, 20000, 50000, 100000 };

        /// <summary>The samples of every block (series name, values).</summary>
        internal static List<(string? Name, List<double> Values)> Samples(string? data)
            => ChartData.Parse(data).Select(b => (b.Name ?? b.ColumnName(0), b.Rows.SelectMany(r => r.Values).Where(Fmt.Finite).ToList())).Where(s => s.Item2.Count > 0).ToList();

        /// <summary>The bin count: the parameter, or Sturges' rule for the largest series.</summary>
        internal static int BinCount(SketchObject o, IReadOnlyList<(string? Name, List<double> Values)> samples)
        {
            var b = o.Num("bins", 0);
            if (b >= 1 && Fmt.Finite(b)) return (int)Math.Min(200, Math.Round(b));
            return Stats.SturgesBins(samples.Select(s => s.Values.Count).DefaultIfEmpty(0).Max());
        }

        internal override Chart Compose(SketchObject o, RenderContext ctx)
        {
            var lang = ctx.Lang;
            var samples = Samples(o.Str("data", DefaultData));
            var bins = BinCount(o, samples);
            var (edges, counts) = Stats.Histogram(samples.Select(s => (IReadOnlyList<double>)s.Values).ToList(), bins, ChartData.Range(o.Str("xrange", "")));
            var x = Axis.Linear(edges, (edges[0], edges[edges.Length - 1]));
            // Counts are whole numbers: whole-number tick steps.
            var y = Axis.Linear(counts.SelectMany(c => c).Select(v => (double)v), ChartData.Range(o.Str("yrange", "")), includeZero: true, steps: CountSteps);
            var panel = new Panel(y, Common.AxisTitle(o, "ylabel", "yunit-none", new I18nText("Count", "Anzahl"), null, lang));
            var f = new Frame(o.Size, Design, x, Common.AxisTitle(o, "xlabel", "xunit", new I18nText("Value", "Wert"), null, lang), panel);
            Common.Apply(f, o, ctx);
            f.XGrid = false;
            var mono = f.Mono || o.Flag("patterns", false);
            var m = samples.Count;
            for (var k = 0; k < m; k++) f.Legend.Add(new LegendEntry(Common.SeriesName(samples[k].Name, k, lang), BarStyle(k, mono), Area: true));
            if (m < 2 && f.LegendMode == "auto") f.LegendMode = "none";
            f.Layout(lang);
            var c = new Chart(f, lang);
            for (var k = 0; k < m; k++)
                for (var j = 0; j < edges.Length - 1; j++)
                {
                    var n = counts[k][j];
                    var w = (edges[j + 1] - edges[j]) / m;
                    var x0 = edges[j] + k * w; var x1 = x0 + w;
                    var top = Geo.Clamp(n, y.Min, y.Max);
                    var p0 = f.P(panel, x0, top); var p1 = f.P(panel, x1, Math.Max(0, y.Min));
                    var rh = Math.Abs(p1.Y - p0.Y);
                    if (n > 0 && rh > 1e-6) c.Areas.Add(Prim.Rect(Math.Min(p0.X, p1.X), Math.Min(p0.Y, p1.Y), Math.Abs(p1.X - p0.X), rh, BarStyle(k, mono)) with { Tag = ChartTags.Nth(ChartTags.Bar, k) });
                    var tip = f.P(panel, (x0 + x1) / 2, top);
                    c.Avoid.Add(new[] { tip, f.P(panel, (x0 + x1) / 2, Math.Max(0, y.Min)) });
                    c.Port(tip, "s" + Fmt.I(k + 1) + "." + Fmt.I(j + 1));
                }
            return c;
        }

        /// <summary>Bars of series <paramref name="k"/>: solid colour with white seams; in monochrome the series pattern.</summary>
        private static Style BarStyle(int k, bool mono)
        {
            var look = SeriesLook.Of(k);
            return mono
                ? new Style { Stroke = Palette.Black, StrokeWidth = 0.2, Role = LineRole.Visible, Fill = k == 0 ? Fill.Hatched(new HatchPattern(45, 1.2)) : SeriesLook.Of(k + 2).MonoFill }
                : new Style { Stroke = Palette.White, StrokeWidth = 0.2, Role = LineRole.Visible, Fill = Fill.Solid(look.Color) };
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            var samples = Samples(o.Str("data", DefaultData));
            var n = BinCount(o, samples);
            var auto = !(o.Num("bins", 0) >= 1);
            return new[]
            {
                Common.DataInput(o, DefaultData) with { Input = Common.DataInput(o, DefaultData).Input! with
                {
                    Label = new I18nText("Samples (numbers) separated by ; or | (new series: ||)", "Messwerte (Zahlen) getrennt durch ; oder | (neue Reihe: ||)"),
                } },
                ContextAction.WithInput("chart.bins", new I18nText("Bins…", "Klassen…"), IconIds.Value,
                    new I18nText("Number of bins (“auto” = Sturges' rule)", "Anzahl der Klassen („auto“ = Sturges-Regel)"),
                    auto ? "auto" : Fmt.I(n),
                    (x, text) =>
                    {
                        var t = (text ?? "").Trim();
                        if (t.Length == 0 || t.Equals("auto", StringComparison.OrdinalIgnoreCase) || t.Equals("automatisch", StringComparison.OrdinalIgnoreCase)) return x.WithoutParam("bins");
                        return x.WithParam("bins", Math.Round(Fmt.ParseInput(t, lang, 1, 200)));
                    }) with { Group = "param", ParamKeys = new[] { "bins" } },
                new ContextAction("chart.bins.more", new I18nText("More bins", "Mehr Klassen"), IconIds.CountMore, x => x.WithParam("bins", (double)Math.Min(200, n + Math.Max(1, n / 5))))
                    { Group = "param", ParamKeys = new[] { "bins" }, IsEnabled = n < 200 },
                new ContextAction("chart.bins.fewer", new I18nText("Fewer bins", "Weniger Klassen"), IconIds.CountFewer, x => x.WithParam("bins", (double)Math.Max(1, n - Math.Max(1, n / 5))))
                    { Group = "param", ParamKeys = new[] { "bins" }, IsEnabled = n > 1 },
                Common.GridToggle(o),
            }.Concat(Common.LegendChoice(o));
        }
    }
}
