using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Charts.Tests
{
    /// <summary>
    /// The presentation charts as code (2026-11-26, round 2): matplotlib and pgfplots/TikZ exports of pie, polar, box plot,
    /// histogram and Gantt charts, the matplotlib import of hist and boxplot, export → import round trips, calendar dates in
    /// Gantt charts and the milestone at the end of the time axis.
    /// </summary>
    public class PresentationChartExportTests
    {
        private static readonly Size2 Page = new(280, 157.5);

        private static string Skipped(CodeImportReport r) => string.Join("\n", r.Skipped.Select(s => s.Line + ": " + s.Code + " – " + s.Reason.En));

        private static IReadOnlyList<Primitive> Drawn(Registry r, SketchObject o, Lang lang = Lang.En)
            => Prim.Flatten(r.RenderLocal(o, new RenderContext(SketchDocument.Create(), lang))).ToList();

        private static double Day(int y, int m, int d) => (new DateTime(y, m, d) - new DateTime(1970, 1, 1)).TotalDays;

        // ------------------------------------------------------------------ exports

        [Fact]
        public void PieAndDonutExportAsMatplotlibPieAndTikzArcs()
        {
            var r = Fx.Reg();
            var donut = Fx.Item(r, "chart.donut").WithParam("start", 30.0).WithParam("direction", "ccw").WithParam("center", "Total");
            var py = ExportTests.Py(r, Lang.En, donut);
            Assert.Contains("pie = ax.pie(slice_values", py);
            Assert.Contains("startangle=60", py);
            Assert.Contains("counterclock=True", py);
            Assert.Contains("wedgeprops=dict(width=0.45", py);
            Assert.Contains("autopct='%1.0f%%'", py);
            Assert.Contains("pctdistance=0.775", py);
            Assert.Contains("ax.text(0, 0, 'Total'", py);
            Assert.Contains("ax.legend(pie[0], slice_names", py);
            for (var k = 0; k < 5; k++) Assert.Contains("# slice." + k, py);
            Assert.Contains("autopct='%1.0f %%'", ExportTests.Py(r, Lang.De, donut));
            var tex = ExportTests.Tex(r, Lang.De, donut);
            Assert.Contains("\\begin{tikzpicture}[x=1mm, y=1mm", tex);
            Assert.Contains("arc[start angle=60, end angle=", tex);
            Assert.Contains("% slice.4", tex);
            Assert.Contains("{Total}", tex);
            Assert.Contains("\\usepgfplotslibrary{fillbetween,groupplots}\n", tex);
            ExportTests.AssertLatexBalanced(tex, "donut");
            // Monochrome: white wedges with hatches, labels on a white backing.
            var mono = ExportTests.Py(r, Lang.En, Fx.Item(r, "chart.pie").WithParam("patterns", true));
            Assert.Contains("wedge.set_hatch(hatch)", mono);
            Assert.Contains("text.set_bbox(BOX)", mono);
        }

        [Fact]
        public void PolarChartsExportAsPolarAxes()
        {
            var r = Fx.Reg();
            var radar = Fx.Item(r, "chart.polar.radar");
            Assert.Equal("top", radar.Str("zero", ""));
            var py = ExportTests.Py(r, Lang.En, radar);
            Assert.Contains("subplot_kw={'projection': 'polar'}", py);
            Assert.Contains("ax.set_theta_zero_location('N')", py);
            Assert.Contains("ax.set_theta_direction(-1)", py);
            Assert.Contains("ax.fill([a * rad for a in series_0_deg], series_0_r", py);
            Assert.Contains("# series.1", py);
            var tex = ExportTests.Tex(r, Lang.En, radar);
            Assert.Contains("\\usepgfplotslibrary{fillbetween,groupplots,polar}", tex);
            Assert.Contains("\\begin{polaraxis}[", tex);
            Assert.Contains("rotate=90", tex);
            Assert.Contains("x dir=reverse", tex);
            Assert.Contains("xtick={0,", tex);
            var plain = Fx.Item(r, "chart.polar");
            Assert.DoesNotContain("set_theta_zero_location", ExportTests.Py(r, Lang.En, plain));
            Assert.DoesNotContain("rotate=90", ExportTests.Tex(r, Lang.En, plain));
            // Closed curves repeat their first point: 8 directions, 9 points.
            Assert.Contains("series_0_deg = [0, 45, 90, 135, 180, 225, 270, 315, 0]", ExportTests.Py(r, Lang.En, plain));
        }

        [Fact]
        public void BoxPlotsAndHistogramsExportFromTheRawSamples()
        {
            var r = Fx.Reg();
            var box = Fx.Item(r, "chart.box");
            var py = ExportTests.Py(r, Lang.En, box);
            Assert.Contains(".boxplot(box_0_data, positions=[0, 1, 2], widths=0.462, whis=1.5, showfliers=True, patch_artist=True, manage_ticks=False", py);
            Assert.Contains("# outlier.0", py);
            var tex = ExportTests.Tex(r, Lang.En, box);
            Assert.Contains("\\usepgfplotslibrary{fillbetween,groupplots,statistics}", tex);
            var a = BoxChart.Groups(BoxChart.DefaultData).Series[0].Samples[0];
            var st = Stats.Box(a)!;
            Assert.Contains("boxplot prepared={draw position=0, box extend=0.462, lower whisker=" + ExportText.N(st.Low) + ", lower quartile=" + ExportText.N(st.Q1)
                + ", median=" + ExportText.N(st.Median), tex);
            Assert.Contains("{data\\\\ 9.6\\\\}", tex);
            // Whiskers to minimum and maximum.
            Assert.Contains("whis=(0, 100), showfliers=False", ExportTests.Py(r, Lang.En, box.WithParam("outliers", false)));

            var hist = Fx.Item(r, "chart.histogram");
            var hp = ExportTests.Py(r, Lang.En, hist);
            Assert.Contains(".hist(bar_0_samples, bins=6, range=(4.2, 12.3)", hp);
            var ht = ExportTests.Tex(r, Lang.De, hist);
            Assert.Contains("hist={bins=6, data min=4.2, data max=12.3}", ht);
            Assert.Contains("table[row sep=\\\\, y index=0, header=false]", ht);
            var two = hist.WithParam("data", "1;2;3;4\n\n2;3;3;5");
            var tp = ExportTests.Py(r, Lang.En, two);
            Assert.Contains(".hist([bar_0_samples, bar_1_samples]", tp);
            Assert.Contains("rwidth=1", tp);
            Assert.Contains("label=['Series 1', 'Series 2']", tp);
            // Other charts keep the document exactly as before (no extra libraries).
            Assert.Contains("\\usepgfplotslibrary{fillbetween,groupplots}\n", ExportTests.Tex(r, Lang.En, Fx.Item(r, "chart.line")));
        }

        [Fact]
        public void GanttChartsExportAsBarsFromTheStart()
        {
            var r = Fx.Reg();
            var g = Fx.Item(r, "chart.gantt");
            var py = ExportTests.Py(r, Lang.En, g);
            Assert.Contains(".barh([0, 1, 2, 3], [3, 4, 3, 3], 0.6, left=[0, 2, 5, 7]", py);
            Assert.Contains("# milestone.0", py);
            Assert.Contains("marker='D'", py);
            Assert.Contains(".axvline(6, ", py);
            Assert.Contains("'Today'", py);
            var tex = ExportTests.Tex(r, Lang.En, g);
            Assert.Contains("\\fill[fill=", tex);
            Assert.Contains("(axis cs:0,-0.3) rectangle (axis cs:3,0.3); % A", tex);
            Assert.Contains("mark=diamond*", tex);
            Assert.Contains("y dir=reverse", tex);
            Assert.Contains("{Today}", tex);
        }

        // ------------------------------------------------------------------ round trips (matplotlib)

        [Theory]
        [InlineData("chart.pie")]
        [InlineData("chart.pie.names")]
        [InlineData("chart.donut")]
        public void PieExportRoundTrips(string id)
        {
            var r = Fx.Reg();
            foreach (var o in new[] { Fx.Item(r, id), Fx.Item(r, id).WithParam("start", -45.0).WithParam("direction", "ccw") })
                foreach (var lang in new[] { Lang.En, Lang.De })
                {
                    var res = ChartCodeImport.Matplotlib(r, ExportTests.Py(r, lang, o), Page);
                    Assert.True(res.Report.Skipped.Count == 0, Skipped(res.Report));
                    var c = Assert.Single(res.Charts);
                    Assert.Equal(PieChart.TypeId, c.Type);
                    var before = PieChart.Slices(o.Str("data", PieChart.DefaultData), o.Num("start", 0), o.Str("direction", "cw") != "ccw");
                    var after = PieChart.Slices(c.Str("data", ""), c.Num("start", 0), c.Str("direction", "cw") != "ccw");
                    Assert.Equal(before.Select(s => s.Name), after.Select(s => s.Name));
                    Assert.Equal(before.Select(s => s.Value), after.Select(s => s.Value));
                    for (var i = 0; i < before.Count; i++)
                    {
                        Assert.Equal(before[i].Start, after[i].Start, 6);
                        Assert.Equal(before[i].End, after[i].End, 6);
                    }
                    Assert.Equal(o.Num("hole", 0), c.Num("hole", 0), 6);
                    Assert.Equal(o.Str("labels", "percent"), c.Str("labels", "percent"));
                }
        }

        [Theory]
        [InlineData("chart.polar")]
        [InlineData("chart.polar.radar")]
        public void PolarExportRoundTrips(string id)
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, id);
            var res = ChartCodeImport.Matplotlib(r, ExportTests.Py(r, Lang.En, o), Page);
            Assert.True(res.Report.Skipped.Count == 0, Skipped(res.Report));
            var c = Assert.Single(res.Charts);
            Assert.Equal(PolarChart.TypeId, c.Type);
            var before = ChartData.Xy(ChartData.Parse(o.Str("data", PolarChart.DefaultData)));
            var after = ChartData.Xy(ChartData.Parse(c.Str("data", "")));
            Assert.Equal(before.Count, after.Count);
            for (var k = 0; k < before.Count; k++)
            {
                Assert.Equal(before[k].Name, after[k].Name);
                Assert.Equal(before[k].Points.Select(p => p.X), after[k].Points.Select(p => Math.Round(p.X, 6)));
                Assert.Equal(before[k].Points.Select(p => p.Y), after[k].Points.Select(p => p.Y));
            }
            Assert.Equal(o.Str("style", "line"), c.Str("style", "line"));
            Assert.Equal(o.Str("zero", "right"), c.Str("zero", "right"));
            Assert.Equal(o.Flag("closed", true), c.Flag("closed", true));
            // The radial range of the drawing is kept.
            Assert.Equal(PolarChart.RadialAxis(o).Max, PolarChart.RadialAxis(c).Max, 9);
        }

        [Theory]
        [InlineData(Lang.En)]
        [InlineData(Lang.De)]
        public void HistogramAndBoxPlotExportsRoundTrip(Lang lang)
        {
            var r = Fx.Reg();
            foreach (var o in new[] { Fx.Item(r, "chart.histogram"), Fx.Item(r, "chart.histogram").WithParam("data", "1;2;3;4;4;5\n\n2;3;3;5;6").WithParam("bins", 3.0) })
            {
                var res = ChartCodeImport.Matplotlib(r, ExportTests.Py(r, lang, o), Page);
                Assert.True(res.Report.Skipped.Count == 0, Skipped(res.Report));
                var c = Assert.Single(res.Charts);
                Assert.Equal(HistogramChart.TypeId, c.Type);
                var a = HistogramChart.Samples(o.Str("data", HistogramChart.DefaultData));
                var b = HistogramChart.Samples(c.Str("data", ""));
                Assert.Equal(a.Select(s => s.Values), b.Select(s => s.Values));
                var ha = Stats.Histogram(a.Select(s => (IReadOnlyList<double>)s.Values).ToList(), HistogramChart.BinCount(o, a), ChartData.Range(o.Str("xrange", "")));
                var hb = Stats.Histogram(b.Select(s => (IReadOnlyList<double>)s.Values).ToList(), HistogramChart.BinCount(c, b), ChartData.Range(c.Str("xrange", "")));
                Assert.Equal(ha.Edges.Select(e => Math.Round(e, 9)), hb.Edges.Select(e => Math.Round(e, 9)));
                Assert.Equal(ha.Counts, hb.Counts);
            }
            foreach (var o in new[] { Fx.Item(r, "chart.box"), Fx.Item(r, "chart.box").WithParam("whis", 2.5), Fx.Item(r, "chart.box").WithParam("outliers", false),
                         Fx.Item(r, "chart.box").WithParam("data", "Before\nA;1;2;3;4;9\nB;2;3;4;5\n\nAfter\nA;2;3;3;5\nB;1;1;2;8") })
            {
                var res = ChartCodeImport.Matplotlib(r, ExportTests.Py(r, lang, o), Page);
                Assert.True(res.Report.Skipped.Count == 0, Skipped(res.Report));
                var c = Assert.Single(res.Charts);
                Assert.Equal(BoxChart.TypeId, c.Type);
                var a = BoxChart.Groups(o.Str("data", BoxChart.DefaultData));
                var b = BoxChart.Groups(c.Str("data", ""));
                Assert.Equal(a.Categories, b.Categories);
                Assert.Equal(a.Series.Count, b.Series.Count);
                for (var k = 0; k < a.Series.Count; k++)
                    foreach (var kv in a.Series[k].Samples) Assert.Equal(kv.Value, b.Series[k].Samples[kv.Key]);
                if (a.Series.Count > 1) Assert.Equal(a.Series.Select(s => s.Name), b.Series.Select(s => s.Name));
                Assert.Equal(o.Num("whis", 1.5), c.Num("whis", 1.5), 9);
                Assert.Equal(o.Flag("outliers", true), c.Flag("outliers", true));
            }
        }

        // ------------------------------------------------------------------ matplotlib import

        [Fact]
        public void MatplotlibHistBecomesAHistogram()
        {
            const string code = @"
import matplotlib.pyplot as plt
data = [1.2, 2.3, 2.9, 3.1, 3.3, 4.0, 4.4, 5.2, 6.8]
fig, ax = plt.subplots()
n, bins, patches = ax.hist(data, bins=4, range=(1, 7), color='C0', edgecolor='white', label='run 1')
ax.set_xlabel('Value')
ax.set_ylabel('Count')
ax.set_title('Spread')
plt.show()
";
            var res = ChartCodeImport.Matplotlib(Fx.Reg(), code, Page);
            Assert.True(res.Report.Skipped.Count == 0, Skipped(res.Report));
            var o = Assert.Single(res.Charts);
            Assert.Equal(HistogramChart.TypeId, o.Type);
            Assert.Equal(new[] { 1.2, 2.3, 2.9, 3.1, 3.3, 4.0, 4.4, 5.2, 6.8 }, HistogramChart.Samples(o.Str("data", ""))[0].Values);
            Assert.Equal("run 1", HistogramChart.Samples(o.Str("data", ""))[0].Name);
            Assert.Equal(4, o.Num("bins", 0));
            Assert.Equal("1;7", o.Str("xrange", ""));
            Assert.Equal("Value", o.Str("xlabel", ""));
            Assert.Equal("Count", o.Str("ylabel", ""));
            Assert.Equal("Spread", o.Str("title", ""));
            Assert.Contains("histogram with 9 samples", res.Report.Imported[0].En);
            Fx.AssertInsideBox(Fx.Render(Fx.Reg(), o), o.Size, "imported histogram");

            // Two data sets with edges and a density: two series, the edges as bins and range, the density reported.
            var two = ChartCodeImport.Matplotlib(Fx.Reg(), "import matplotlib.pyplot as plt\nplt.hist([[1, 2, 3], [2, 4, 7]], bins=[0, 2, 4, 6, 8], label=['a', 'b'], density=True)\nplt.legend()\n", Page);
            var t = Assert.Single(two.Charts);
            Assert.Equal(new[] { "a", "b" }, HistogramChart.Samples(t.Str("data", "")).Select(s => s.Name));
            Assert.Equal(4, t.Num("bins", 0));
            Assert.Equal("0;8", t.Str("xrange", ""));
            Assert.Single(two.Report.Skipped);
            // Unequal bins and a histogram next to a curve are reported.
            var uneven = ChartCodeImport.Matplotlib(Fx.Reg(), "import matplotlib.pyplot as plt\nplt.hist([1, 2, 3, 4], bins=[0, 1, 5])\n", Page);
            Assert.Contains(uneven.Report.Skipped, s => s.Reason.En.Contains("different widths"));
            var mixed = ChartCodeImport.Matplotlib(Fx.Reg(), "import matplotlib.pyplot as plt\nplt.plot([1, 2], [3, 4])\nplt.hist([1, 2, 3])\n", Page);
            Assert.Contains(mixed.Report.Skipped, s => s.Line == 3);
            Assert.Equal(FigureChart.TypeId, Assert.Single(mixed.Charts).Type);
        }

        [Fact]
        public void MatplotlibBoxplotBecomesABoxPlot()
        {
            const string code = @"
import matplotlib.pyplot as plt
fig, ax = plt.subplots()
ax.boxplot([[1, 2, 3, 4, 100], [2, 3, 4]], tick_labels=['A', 'B'], whis=2.0)
ax.set_ylabel('Time')
";
            var res = ChartCodeImport.Matplotlib(Fx.Reg(), code, Page);
            Assert.True(res.Report.Skipped.Count == 0, Skipped(res.Report));
            var o = Assert.Single(res.Charts);
            Assert.Equal(BoxChart.TypeId, o.Type);
            var g = BoxChart.Groups(o.Str("data", ""));
            Assert.Equal(new[] { "A", "B" }, g.Categories);
            Assert.Equal(new double[] { 1, 2, 3, 4, 100 }, g.Series[0].Samples[0]);
            Assert.Equal(2, o.Num("whis", 0));
            Assert.True(o.Flag("outliers", false));
            Assert.Equal("Time", o.Str("ylabel", ""));
            Assert.Contains("box plot with 2 boxes", res.Report.Imported[0].En);
            Assert.Single(Drawn(Fx.Reg(), o), p => p.Tag == "outlier.0");

            // Default positions name the categories 1, 2 …; hidden outliers and horizontal boxes are reported.
            var plain = ChartCodeImport.Matplotlib(Fx.Reg(), "import matplotlib.pyplot as plt\nplt.boxplot([[1, 2, 3], [4, 5, 6]], showfliers=False, vert=False)\n", Page);
            var p = Assert.Single(plain.Charts);
            Assert.Equal(new[] { "1", "2" }, BoxChart.Groups(p.Str("data", "")).Categories);
            Assert.False(p.Flag("outliers", true));
            Assert.Equal(2, plain.Report.Skipped.Count);
        }

        // ------------------------------------------------------------------ Gantt: dates and milestones

        [Fact]
        public void DatesAreReadAsDayNumbers()
        {
            Assert.Equal("A;" + Day(2026, 11, 2) + ";3", ChartDates.ToDays("A;2026-11-02;3", out var any));
            Assert.True(any);
            Assert.Equal("B;" + Day(2026, 12, 31) + ";1,5", ChartDates.ToDays("B;31.12.2026;1,5", out _));
            Assert.Equal("C;2026-13-01;1.234,5;12.5", ChartDates.ToDays("C;2026-13-01;1.234,5;12.5", out any));
            Assert.False(any);
            Assert.True(ChartDates.TryDay(" 01.02.2027 ", out var d));
            Assert.Equal(Day(2027, 2, 1), d);
            Assert.False(ChartDates.TryDay("12.5", out _));
            Assert.Equal("2026-11-26", ChartDates.Iso(Day(2026, 11, 26)));
            // Weekly ticks on Mondays with the year at the first one.
            var ticks = ChartDates.Ticks(Day(2026, 11, 2), Day(2026, 11, 24), Lang.En, out var lo, out var hi);
            Assert.Equal(Day(2026, 11, 2), lo);
            Assert.Equal(Day(2026, 11, 30), hi);
            Assert.Equal(new[] { "Nov 2, 2026", "Nov 9", "Nov 16", "Nov 23", "Nov 30" }, ticks.Select(t => t.Label));
            Assert.Equal(new[] { "2.11.2026", "9.11.", "16.11.", "23.11.", "30.11." }, ChartDates.Ticks(Day(2026, 11, 2), Day(2026, 11, 24), Lang.De, out _, out _).Select(t => t.Label));
            // Months and years.
            Assert.Equal(new[] { "Jan 2026", "Mar", "May", "Jul", "Sep", "Nov", "Jan 2027" }, ChartDates.Ticks(Day(2026, 1, 10), Day(2026, 12, 20), Lang.En, out _, out _).Select(t => t.Label));
            Assert.Equal(new[] { "2020", "2022", "2024", "2026", "2028", "2030" }, ChartDates.Ticks(Day(2020, 3, 1), Day(2029, 6, 1), Lang.En, out _, out _).Select(t => t.Label));
        }

        [Fact]
        public void GanttReadsCalendarDatesWithDateTicks()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "chart.gantt").WithParam("data", "Design;2026-11-02;5\nBuild;09.11.2026;10\nLaunch;2026-11-23").WithParam("today", "2026-11-12");
            var tasks = GanttChart.Tasks(o.Str("data", ""), Lang.En).Tasks;
            Assert.Equal(new[] { Day(2026, 11, 2), Day(2026, 11, 9), Day(2026, 11, 23) }, tasks.Select(t => t.Start));
            Assert.Equal(new double[] { 5, 10, 0 }, tasks.Select(t => t.Duration));
            Assert.Equal(Day(2026, 11, 12), GanttChart.Today(o));
            var prims = Drawn(r, o);
            var labels = prims.OfType<TextPrim>().Where(t => ChartTags.Is(t.Tag, ChartTags.TickLabel)).Select(t => t.Text).ToList();
            Assert.Contains("Nov 9", labels);
            Assert.Contains("Nov 2, 2026", labels);
            Assert.Contains(prims.OfType<TextPrim>(), t => t.Text == "Date");
            Assert.Contains(Drawn(r, o, Lang.De).OfType<TextPrim>(), t => t.Text == "9.11.");
            // The today line sits at its date.
            var anchors = Fx.Type(r, o).Anchors(o, Fx.Ctx()).Where(a => a.Id is not null).ToDictionary(a => a.Id!);
            double X(double day) => anchors["plot.bl"].P.X + (anchors["plot.br"].P.X - anchors["plot.bl"].P.X) * (day - Day(2026, 11, 2)) / 28;
            Assert.Equal(X(Day(2026, 11, 12)), prims.OfType<LinePrim>().Single(p => p.Tag == ChartTags.Today).A.X, 6);
            // Actions keep dates: a new task starts on the last end as an ISO date; the today line takes a date.
            var added = r.TypeOf(o).Actions(o, new RenderContext(SketchDocument.Create(), Lang.En)).Single(a => a.Id == "chart.gantt.add").Apply(o);
            Assert.EndsWith("Task 4;2026-11-23;2", added.Str("data", ""));
            var removed = r.TypeOf(o).Actions(o, new RenderContext(SketchDocument.Create(), Lang.En)).Single(a => a.Id == "chart.gantt.remove").Apply(o);
            Assert.Equal(2, GanttChart.Tasks(removed.Str("data", ""), Lang.En).Tasks.Count);
            var today = r.TypeOf(o).Actions(o, new RenderContext(SketchDocument.Create(), Lang.De)).Single(a => a.Id == "chart.today");
            Assert.Equal("2026-11-15", today.Input!.Apply(o, "15.11.2026").Str("today", ""));
            // The export writes the date ticks as named ticks.
            Assert.Contains("'Nov 9'", ExportTests.Py(r, Lang.En, o));
            // Cleaning: normalised times without the dates; only renaming keeps the dates.
            var norm = r.TypeOf(o).Sanitize(o, new SanitizeOptions { Mode = SanitizeMode.Normalised, ReplaceTexts = true }, new Random(2));
            var nt = GanttChart.Tasks(norm.Str("data", ""), Lang.En).Tasks;
            Assert.Equal(0, nt.Min(t => t.Start), 9);
            Assert.Equal(1, nt.Max(t => t.End), 9);
            Assert.DoesNotContain("2026", norm.Str("data", ""));
            Assert.InRange(GanttChart.Today(norm)!.Value, 0, 1);
            var renamed = r.TypeOf(o).Sanitize(o, new SanitizeOptions { Mode = SanitizeMode.None, ReplaceTexts = true, RenameCategories = true }, new Random(2));
            Assert.Contains("2026-11-02", renamed.Str("data", ""));
            Assert.DoesNotContain("Design", renamed.Str("data", ""));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("0;10")]
        public void AMilestoneAtTheEndOfTheAxisStaysInsideThePlot(string? range)
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "chart.gantt");
            if (range is not null) o = o.WithParam("xrange", range);
            var anchors = Fx.Type(r, o).Anchors(o, Fx.Ctx()).Where(a => a.Id is not null).ToDictionary(a => a.Id!);
            double left = anchors["plot.bl"].P.X, right = anchors["plot.br"].P.X;
            var diamond = Drawn(r, o).OfType<PathPrim>().Single(p => p.Tag == "milestone.0");
            Assert.All(diamond.Segments.Where(s => s.Op != SegOp.Z), s => Assert.InRange(s.P.X, left - 1e-6, right + 1e-6));
            if (range is null)
            {
                // The automatic axis grows by a step (0 … 12 in steps of 2): the milestone at 10 keeps its exact place.
                var centre = diamond.Segments.Where(s => s.Op != SegOp.Z).Average(s => s.P.X);
                Assert.Equal(left + (right - left) * 10 / 12, centre, 6);
            }
        }
    }
}
