using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Charts.Tests
{
    /// <summary>
    /// The presentation charts as code (2026-11-26, round 4): the Sankey export (matplotlib patches, TikZ paths with data
    /// keys), export → import round trips of all six forms in matplotlib and pgfplots/TikZ in both languages, and the import
    /// of hand-written variants (matplotlib Sankey and barh Gantt charts, pgf-pie, polaraxis, hist and boxplot in pgfplots).
    /// </summary>
    public class PresentationChartImportTests
    {
        private static readonly Size2 Page = new(280, 157.5);

        private static string Skipped(CodeImportReport r) => string.Join("\n", r.Skipped.Select(s => s.Line + ": " + s.Code + " – " + s.Reason.En));

        private static ChartCodeImport.Result Import(Registry r, string format, string code)
            => format == "py" ? ChartCodeImport.Matplotlib(r, code, Page) : ChartCodeImport.Pgfplots(r, code, Page);

        private static string Export(Registry r, string format, Lang lang, SketchObject o) => format == "py" ? ExportTests.Py(r, lang, o) : ExportTests.Tex(r, lang, o);

        /// <summary>The export of <paramref name="o"/> imported again: one chart of the same type, without report lines.</summary>
        private static SketchObject RoundTrip(Registry r, SketchObject o, string format, Lang lang)
        {
            var res = Import(r, format, Export(r, format, lang, o));
            Assert.True(res.Report.Skipped.Count == 0, o.Type + " " + format + " " + lang + ":\n" + Skipped(res.Report));
            var c = Assert.Single(res.Charts);
            Assert.Equal(o.Type, c.Type);
            Fx.AssertFinite(Fx.Render(r, c), o.Type);
            return c;
        }

        public static IEnumerable<object[]> Formats()
        {
            foreach (var f in new[] { "py", "tex" })
                foreach (var l in new[] { Lang.En, Lang.De })
                    yield return new object[] { f, l };
        }

        // ------------------------------------------------------------------ Sankey export

        [Fact]
        public void SankeyDiagramsExportAsDrawnWithTheirFlows()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "chart.sankey").WithParam("title", "Energy");
            var py = ExportTests.Py(r, Lang.En, o);
            Assert.Contains("def sankey_flow(ax, source, target, value, ribbon=None, **style):", py);
            Assert.Contains("from matplotlib.path import Path", py);
            Assert.Contains("sankey_flow(ax, 'A', 'M', 30, (", py);
            Assert.Contains("sankey_node(ax, 'M', ", py);
            Assert.Contains("ax.text(", py);
            Assert.Contains("'M 100'", py);
            Assert.Contains("fig.suptitle('Energy'", py);
            for (var k = 0; k < 6; k++) Assert.Contains("# flow." + k, py);
            ExportTests.AssertPythonBalanced(py, "sankey");
            // Other scripts keep their header.
            Assert.DoesNotContain("sankey_flow", ExportTests.Py(r, Lang.En, Fx.Item(r, "chart.line")));
            var tex = ExportTests.Tex(r, Lang.De, o);
            Assert.Contains("\\tikzset{sankey chart/.style={}, sankey flow/.style n args={3}{}}", tex);
            Assert.Contains("sankey chart={values=true, title={Energy}}", tex);
            Assert.Contains("\\fill[sankey flow={A}{M}{30}, fill=", tex);
            Assert.Contains(".. controls (", tex);
            Assert.Contains("{M 100}", tex);
            ExportTests.AssertLatexBalanced(tex, "sankey");
            Assert.DoesNotContain("\\tikzset", ExportTests.Tex(r, Lang.De, Fx.Item(r, "chart.line")));

            // A flow against the direction is listed but not drawn; monochrome ribbons are outlined white areas.
            var cycle = Fx.Item(r, "chart.sankey").WithParam("data", "A;B;10\nB;C;10\nC;A;4").WithParam("patterns", true);
            var cpy = ExportTests.Py(r, Lang.En, cycle);
            Assert.Contains("sankey_flow(ax, 'C', 'A', 4)\n", cpy);
            Assert.Contains("facecolor='#ffffff', edgecolor='#000000'", cpy);
            Assert.Contains("\\path[sankey flow={C}{A}{4}] (0,0);", ExportTests.Tex(r, Lang.En, cycle));

            // A hidden flow stays in the data.
            var hidden = o with { Parts = new Dictionary<string, PartLook> { ["flow.1"] = new PartLook { Hidden = true } } };
            Assert.Contains("sankey_flow(ax, 'B', 'M', 45)\n", ExportTests.Py(r, Lang.En, hidden));
        }

        // ------------------------------------------------------------------ round trips

        [Theory]
        [MemberData(nameof(Formats))]
        public void SankeyRoundTrips(string format, Lang lang)
        {
            var r = Fx.Reg();
            foreach (var o in new[]
                     {
                         Fx.Item(r, "chart.sankey"),
                         Fx.Item(r, "chart.sankey").WithParam("data", "Coal, lignite;Power;30\nGas;Power;20,5\nPower;Homes, flats;35\nPower;Losses;15,5").WithParam("values", false).WithParam("title", "Flows 2026"),
                         Fx.Item(r, "chart.sankey").WithParam("data", "A;B;10\nB;C;10\nC;A;4").WithParam("patterns", true),
                     })
            {
                var c = RoundTrip(r, o, format, lang);
                var a = SankeyModel.Parse(o.Str("data", SankeyChart.DefaultData));
                var b = SankeyModel.Parse(c.Str("data", ""));
                Assert.Equal(a.Nodes, b.Nodes);
                Assert.Equal(a.Flows.Select(f => (f.Source, f.Target, f.Value)), b.Flows.Select(f => (f.Source, f.Target, f.Value)));
                Assert.Equal(o.Flag("values", true), c.Flag("values", true));
                Assert.Equal(o.Str("title", ""), c.Str("title", ""));
            }
        }

        [Theory]
        [MemberData(nameof(Formats))]
        public void PieRoundTrips(string format, Lang lang)
        {
            var r = Fx.Reg();
            foreach (var o in new[]
                     {
                         Fx.Item(r, "chart.pie"), Fx.Item(r, "chart.pie.names"),
                         Fx.Item(r, "chart.donut").WithParam("start", 30.0).WithParam("direction", "ccw").WithParam("center", "Total").WithParam("title", "Shares"),
                         Fx.Item(r, "chart.pie").WithParam("labels", "name").WithParam("start", -45.0).WithParam("data", "Q1;10\nQ2;20,5\nQ3;30"),
                     })
            {
                var c = RoundTrip(r, o, format, lang);
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
                Assert.Equal(o.Str("center", ""), c.Str("center", ""));
                Assert.Equal(o.Str("title", ""), c.Str("title", ""));
            }
        }

        [Theory]
        [MemberData(nameof(Formats))]
        public void PolarRoundTrips(string format, Lang lang)
        {
            var r = Fx.Reg();
            foreach (var o in new[]
                     {
                         Fx.Item(r, "chart.polar"), Fx.Item(r, "chart.polar.radar"), Fx.Item(r, "chart.polar").WithParam("style", "points").WithParam("closed", false),
                         Fx.Item(r, "chart.polar.radar").WithParam("patterns", true),
                     })
            {
                var c = RoundTrip(r, o, format, lang);
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
                Assert.Equal(PolarChart.RadialAxis(o).Max, PolarChart.RadialAxis(c).Max, 9);
            }
        }

        [Theory]
        [MemberData(nameof(Formats))]
        public void HistogramRoundTrips(string format, Lang lang)
        {
            var r = Fx.Reg();
            foreach (var o in new[]
                     {
                         Fx.Item(r, "chart.histogram"),
                         Fx.Item(r, "chart.histogram").WithParam("data", "Before\n1;2;3;4;4;5\n\nAfter\n2;3;3;5;6").WithParam("bins", 3.0).WithParam("xrange", "0;8"),
                     })
            {
                var c = RoundTrip(r, o, format, lang);
                var a = HistogramChart.Samples(o.Str("data", HistogramChart.DefaultData));
                var b = HistogramChart.Samples(c.Str("data", ""));
                Assert.Equal(a.Select(s => s.Values), b.Select(s => s.Values));
                if (a.Count > 1) Assert.Equal(a.Select(s => s.Name), b.Select(s => s.Name));
                var ha = Stats.Histogram(a.Select(s => (IReadOnlyList<double>)s.Values).ToList(), HistogramChart.BinCount(o, a), ChartData.Range(o.Str("xrange", "")));
                var hb = Stats.Histogram(b.Select(s => (IReadOnlyList<double>)s.Values).ToList(), HistogramChart.BinCount(c, b), ChartData.Range(c.Str("xrange", "")));
                Assert.Equal(ha.Edges.Select(e => Math.Round(e, 9)), hb.Edges.Select(e => Math.Round(e, 9)));
                Assert.Equal(ha.Counts, hb.Counts);
            }
        }

        [Theory]
        [MemberData(nameof(Formats))]
        public void BoxPlotRoundTrips(string format, Lang lang)
        {
            var r = Fx.Reg();
            foreach (var o in new[]
                     {
                         Fx.Item(r, "chart.box"), Fx.Item(r, "chart.box").WithParam("whis", 2.5), Fx.Item(r, "chart.box").WithParam("outliers", false),
                         Fx.Item(r, "chart.box").WithParam("data", "Before\nA;1;2;3;4;9\nB;2;3;4;5\n\nAfter\nA;2;3;3;5\nB;1;1;2;8").WithParam("patterns", true),
                     })
            {
                var c = RoundTrip(r, o, format, lang);
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

        /// <summary>Two groups; milestones at the end of the first and the last group and at the start of the second.</summary>
        private const string Groups = "Planning\nA;0;3\nB;2;4\nM1;6\n\nExecution\nM2;6\nC;5;3\nD;7;2,5\nEnd;10";

        [Theory]
        [MemberData(nameof(Formats))]
        public void GanttRoundTrips(string format, Lang lang)
        {
            var r = Fx.Reg();
            foreach (var o in new[]
                     {
                         Fx.Item(r, "chart.gantt"),
                         Fx.Item(r, "chart.gantt").WithParam("data", "Design;2026-11-02;5\nBuild;09.11.2026;10\nLaunch;2026-11-23").WithParam("today", "2026-11-12").WithParam("title", "Plan"),
                         Fx.Item(r, "chart.gantt").WithParam("data", Groups).WithParam("legend", "inside").WithParam("xrange", "0;14").WithParam("xlabel", "Weeks"),
                         // Monochrome: black diamonds next to black and white bars; the milestones keep their groups.
                         Fx.Item(r, "chart.gantt").WithParam("data", Groups).WithParam("patterns", true),
                     })
            {
                var c = RoundTrip(r, o, format, lang);
                var (ta, ga) = GanttChart.Tasks(o.Str("data", GanttChart.DefaultData), lang);
                var (tb, gb) = GanttChart.Tasks(c.Str("data", ""), lang);
                Assert.Equal(ta.Select(t => (t.Group, t.Name, t.Start, t.Duration)), tb.Select(t => (t.Group, t.Name, t.Start, t.Duration)));
                Assert.Equal(GanttChart.Today(o), GanttChart.Today(c));
                Assert.Equal(GanttChart.UsesDates(o), GanttChart.UsesDates(c));
                Assert.Equal(o.Str("title", ""), c.Str("title", ""));
                Assert.Equal(o.Str("xlabel", ""), c.Str("xlabel", ""));
                if (ga.Count > 1) Assert.Equal(ga, gb);
                // The same time axis.
                var ctx = new RenderContext(SketchDocument.Create(), lang).For(r);
                var fa = Fx.Type(r, o).Compose(o, ctx).F.X; var fb = Fx.Type(r, c).Compose(c, ctx).F.X;
                Assert.Equal(fa.Min, fb.Min, 9);
                Assert.Equal(fa.Max, fb.Max, 9);
            }
        }

        [Theory]
        [InlineData("py")]
        [InlineData("tex")]
        public void EveryPresentationChartImportsWithoutReportLines(string format)
        {
            var r = Fx.Reg();
            foreach (var id in new[] { "chart.pie", "chart.pie.names", "chart.donut", "chart.polar", "chart.polar.radar", "chart.sankey", "chart.gantt", "chart.box", "chart.histogram" })
                foreach (var lang in new[] { Lang.En, Lang.De })
                    foreach (var mono in new[] { false, true })
                        RoundTrip(r, Fx.Item(r, id).WithParam("patterns", mono), format, lang);
        }

        // ------------------------------------------------------------------ hand-written matplotlib

        [Fact]
        public void MatplotlibSankeyClassBecomesASankeyDiagram()
        {
            const string code = @"
import matplotlib.pyplot as plt
from matplotlib.sankey import Sankey

fig = plt.figure()
ax = fig.add_subplot(1, 1, 1, xticks=[], yticks=[])
sankey = Sankey(ax=ax, unit=None)
sankey.add(flows=[1.0, -0.6, -0.4], labels=['Input', '', 'Waste'], label='Boiler')
sankey.add(flows=[0.6, -0.5, -0.1], labels=['', 'Work', 'Heat'], label='Turbine', prior=0, connect=(1, 0))
diagrams = sankey.finish()
plt.title('Plant')
";
            var res = ChartCodeImport.Matplotlib(Fx.Reg(), code, Page);
            var o = Assert.Single(res.Charts);
            Assert.Equal(SankeyChart.TypeId, o.Type);
            var m = SankeyModel.Parse(o.Str("data", ""));
            Assert.Equal(new[] { ("Input", "Boiler", 1.0), ("Boiler", "Turbine", 0.6), ("Boiler", "Waste", 0.4), ("Turbine", "Work", 0.5), ("Turbine", "Heat", 0.1) },
                m.Flows.Select(f => (m.Nodes[f.Source], m.Nodes[f.Target], f.Value)));
            Assert.Equal("Plant", o.Str("title", ""));
            Assert.Contains("Sankey diagram with 5 flows, 6 nodes", res.Report.Imported[0].En);
            Assert.True(res.Report.Skipped.Count == 0, Skipped(res.Report));
            Fx.AssertInsideBox(Fx.Render(Fx.Reg(), o), o.Size, "imported sankey");

            // Constructor arguments make the first diagram; orientations are reported once; unlabelled flows get names.
            var one = ChartCodeImport.Matplotlib(Fx.Reg(), "from matplotlib.sankey import Sankey\nSankey(flows=[0.25, 0.15, -0.3, -0.1], orientations=[0, 1, 0, -1]).finish()\n", Page);
            var p = Assert.Single(one.Charts);
            var pm = SankeyModel.Parse(p.Str("data", ""));
            Assert.Equal(new[] { "P1 in 1", "P1", "P1 in 2", "P1 out 3", "P1 out 4" }, pm.Nodes);
            Assert.Single(one.Report.Skipped, s => s.Reason.En.Contains("Orientations"));
        }

        [Fact]
        public void MatplotlibBarhWithStartsBecomesAGanttChart()
        {
            const string code = @"
import matplotlib.pyplot as plt
tasks = ['Design', 'Build', 'Test']
fig, ax = plt.subplots()
ax.barh(tasks, [3, 5, 2], left=[0, 2, 7], color='tab:blue')
ax.plot([9], [2], 'D', color='tab:blue')
ax.axvline(4, color='red', ls='--')
ax.invert_yaxis()
ax.set_xlabel('Days')
ax.set_title('Project')
ax.grid(axis='x')
";
            var res = ChartCodeImport.Matplotlib(Fx.Reg(), code, Page);
            Assert.True(res.Report.Skipped.Count == 0, Skipped(res.Report));
            var o = Assert.Single(res.Charts);
            Assert.Equal(GanttChart.TypeId, o.Type);
            var (tasks, _) = GanttChart.Tasks(o.Str("data", ""), Lang.En);
            Assert.Equal(new[] { ("Design", 0.0, 3.0), ("Build", 2.0, 5.0), ("Test", 7.0, 2.0), ("Test", 9.0, 0.0) }, tasks.Select(t => (t.Name, t.Start, t.Duration)));
            Assert.Equal(4, GanttChart.Today(o));
            Assert.Equal("Days", o.Str("xlabel", ""));
            Assert.Equal("Project", o.Str("title", ""));
            Assert.Contains("Gantt chart with 3 tasks, 1 milestone", res.Report.Imported[0].En);

            // Without invert_yaxis the first task is at the bottom: the import lists the tasks top-down as drawn.
            var up = ChartCodeImport.Matplotlib(Fx.Reg(), "import matplotlib.pyplot as plt\nplt.barh([0, 1], [2, 3], left=[1, 0])\nplt.yticks([0, 1], ['first', 'second'])\n", Page);
            Assert.Equal(new[] { "second", "first" }, GanttChart.Tasks(Assert.Single(up.Charts).Str("data", ""), Lang.En).Tasks.Select(t => t.Name));
            // A stacked bar chart (the same rows in every series) stays a bar chart; a reversed y axis there is reported.
            var stacked = ChartCodeImport.Matplotlib(Fx.Reg(), "import matplotlib.pyplot as plt\nplt.barh([0, 1], [2, 3])\nplt.barh([0, 1], [1, 1], left=[2, 3])\nplt.gca().invert_yaxis()\n", Page);
            Assert.Equal(BarChart.TypeId, Assert.Single(stacked.Charts).Type);
            Assert.Contains(stacked.Report.Skipped, s => s.Reason.En.Contains("Reversed"));
        }

        // ------------------------------------------------------------------ hand-written pgfplots and TikZ

        [Fact]
        public void PgfPieBecomesAPieChart()
        {
            const string tex = @"\documentclass{article}
\usepackage{pgf-pie}
\begin{document}
\begin{tikzpicture}
\pie[rotate=90, text=legend]{30/Rent, 45/Food, 25/{Travel, misc.}}
\end{tikzpicture}
\pie[sum=auto, hide number]{2/A, 3/B}
\end{document}";
            var res = ChartCodeImport.Pgfplots(Fx.Reg(), tex, Page);
            Assert.True(res.Report.Skipped.Count == 0, Skipped(res.Report));
            Assert.Equal(2, res.Charts.Count);
            var a = res.Charts[0];
            Assert.Equal(PieChart.TypeId, a.Type);
            var slices = PieChart.Slices(a.Str("data", ""), a.Num("start", 0), a.Str("direction", "cw") != "ccw");
            Assert.Equal(new[] { "Rent", "Food", "Travel, misc." }, slices.Select(s => s.Name));
            Assert.Equal(new double[] { 30, 45, 25 }, slices.Select(s => s.Value));
            // pgf-pie: counter-clockwise from rotate = 90 (12 o'clock).
            Assert.Equal("ccw", a.Str("direction", ""));
            Assert.Equal(0, a.Num("start", 99), 9);
            Assert.Equal("percent", a.Str("labels", ""));
            Assert.Equal("right", a.Str("legend", ""));
            var b = res.Charts[1];
            Assert.Equal("name", b.Str("labels", ""));
            Assert.Equal(90, b.Num("start", 0), 9);
            // The default sum of 100 with smaller values is a partial pie: reported.
            var partial = ChartCodeImport.Pgfplots(Fx.Reg(), "\\pie{10/A, 20/B}", Page);
            Assert.Single(partial.Charts);
            Assert.Contains(partial.Report.Skipped, s => s.Reason.En.Contains("partial pie"));
            // Diagrams of the sankey package are reported.
            Assert.Contains(ChartCodeImport.Pgfplots(Fx.Reg(), "\\begin{sankeydiagram}\\end{sankeydiagram}", Page).Report.Skipped, s => s.Reason.En.Contains("sankey package"));
        }

        [Fact]
        public void PgfplotsPolarAxisBecomesAPolarChart()
        {
            const string tex = @"\begin{tikzpicture}
\begin{polaraxis}[ymax=2, title={Pattern}]
\addplot[blue, domain=0:360, samples=73] {1+cos(x)};
\addlegendentry{cardioid}
\addplot[red, mark=*, only marks] coordinates {(0,1) (90,1.5) (180,0.5)};
\end{polaraxis}
\end{tikzpicture}";
            var res = ChartCodeImport.Pgfplots(Fx.Reg(), tex, Page);
            Assert.True(res.Report.Skipped.Count == 0, Skipped(res.Report));
            var o = Assert.Single(res.Charts);
            Assert.Equal(PolarChart.TypeId, o.Type);
            var series = ChartData.Xy(ChartData.Parse(o.Str("data", "")));
            Assert.Equal(2, series.Count);
            Assert.Equal("cardioid", series[0].Name);
            Assert.Equal(90, series[0].Points[18].X, 6);
            Assert.Equal(1, series[0].Points[18].Y, 6);
            Assert.Equal(new[] { 0.0, 90, 180 }, series[1].Points.Select(p => Math.Round(p.X, 6)));
            Assert.Equal("right", o.Str("zero", ""));
            Assert.Equal("Pattern", o.Str("title", ""));
            Assert.Equal(2, PolarChart.RadialAxis(o).Max, 9);
            // rotate=90 alone (0° up, counter-clockwise) is reported.
            var turned = ChartCodeImport.Pgfplots(Fx.Reg(), "\\begin{polaraxis}[rotate=90]\\addplot coordinates {(0,1) (90,2)};\\end{polaraxis}", Page);
            Assert.Equal("top", Assert.Single(turned.Charts).Str("zero", ""));
            Assert.Single(turned.Report.Skipped);
        }

        [Fact]
        public void PgfplotsHistAndBoxplotsBecomeStatisticsCharts()
        {
            const string hist = @"\begin{axis}[xlabel={Value}]
\addplot[hist={bins=4, data min=0, data max=8}] table[row sep=\\, y index=0] {data\\ 1\\ 2.5\\ 3\\ 7\\ 4\\};
\end{axis}";
            var h = Assert.Single(ChartCodeImport.Pgfplots(Fx.Reg(), hist, Page).Charts);
            Assert.Equal(HistogramChart.TypeId, h.Type);
            Assert.Equal(new[] { 1, 2.5, 3, 7, 4 }, HistogramChart.Samples(h.Str("data", ""))[0].Values);
            Assert.Equal(4, h.Num("bins", 0));
            Assert.Equal("0;8", h.Str("xrange", ""));
            Assert.Equal("Value", h.Str("xlabel", ""));

            // Raw samples (pgfplots computes the box), and prepared values with outliers (representative samples, reported).
            const string box = @"\begin{axis}[xtick={1,2}, xticklabels={Old, New}]
\addplot+[boxplot={whisker range=2}] table[row sep=\\, y index=0] {data\\ 1\\ 2\\ 3\\ 4\\ 10\\};
\addplot+[boxplot prepared={lower whisker=1, lower quartile=2, median=3, upper quartile=4, upper whisker=5}] table[row sep=\\, y index=0] {data\\ 9\\ -5\\};
\end{axis}";
            var res = ChartCodeImport.Pgfplots(Fx.Reg(), box, Page);
            var o = Assert.Single(res.Charts);
            Assert.Equal(BoxChart.TypeId, o.Type);
            var g = BoxChart.Groups(o.Str("data", ""));
            Assert.Equal(new[] { "Old", "New" }, g.Categories);
            Assert.Equal(new double[] { 1, 2, 3, 4, 10 }, g.Series[0].Samples[0]);
            Assert.Equal(2, o.Num("whis", 0), 9);
            var st = Stats.Box(g.Series[0].Samples[1], 2)!;
            Assert.Equal((1.0, 2.0, 3.0, 4.0, 5.0), (st.Low, st.Q1, st.Median, st.Q3, st.High));
            Assert.Equal(new[] { -5.0, 9 }, st.Outliers);
            Assert.Single(res.Report.Skipped, s => s.Reason.En.Contains("representative samples"));
        }

        [Fact]
        public void RepresentativeSamplesKeepQuartilesWhiskersAndOutliers()
        {
            foreach (var outliers in new[] { new double[0], new[] { -5.0 }, new[] { -9.0, -8, 20, 21, 22 } })
            {
                var s = PgfplotsReader.Representative(1, 2, 2.5, 4, 6, outliers);
                var st = Stats.Box(s)!;
                Assert.Equal((1.0, 2.0, 2.5, 4.0, 6.0), (st.Low, st.Q1, st.Median, st.Q3, st.High));
                Assert.Equal(outliers.OrderBy(v => v), st.Outliers);
            }
        }

        [Fact]
        public void TruncatedPresentationChartCodeNeverThrows()
        {
            var r = Fx.Reg();
            foreach (var id in new[] { "chart.donut", "chart.polar.radar", "chart.sankey", "chart.gantt", "chart.box", "chart.histogram" })
            {
                var o = Fx.Item(r, id);
                foreach (var code in new[] { ExportTests.Py(r, Lang.En, o), ExportTests.Tex(r, Lang.En, o) })
                    for (var k = 1; k <= 24; k++)
                    {
                        var cut = code.Substring(0, code.Length * k / 25);
                        ChartCodeImport.Matplotlib(r, cut, Page);
                        ChartCodeImport.Pgfplots(r, cut, Page);
                    }
            }
            foreach (var junk in new[] { "\\pie", "\\pie[", "\\pie{", "\\pie{/}", "\\pie{a/b}", "\\begin{tikzpicture}[pie chart={", "\\fill[pie slice={A}] (0,0);",
                         "\\fill[sankey flow={A}{B}] (0,0);", "\\begin{polaraxis}\\addplot[fill] coordinates {(0,1)};", "\\begin{axis}[ymin=0,ymax=1]\\fill (axis cs:0,0.2) rectangle (axis cs:1,0.4);",
                         "\\begin{axis}\\addplot[boxplot prepared={}] coordinates {};", "\\begin{axis}\\addplot[hist] coordinates {};",
                         "from matplotlib.sankey import Sankey\nSankey().add(prior=3).finish()", "sankey_flow(1, 2)", "import matplotlib.pyplot as plt\nplt.barh([0], [1], left=[float('nan')])" })
            {
                ChartCodeImport.Matplotlib(r, junk, Page);
                ChartCodeImport.Pgfplots(r, junk, Page);
            }
        }

        [Fact]
        public void TheOwnTikzFormsNeedNoComments()
        {
            // The data keys carry the data: an export without its comments imports the same.
            var r = Fx.Reg();
            foreach (var id in new[] { "chart.donut", "chart.sankey", "chart.box", "chart.gantt" })
            {
                var o = Fx.Item(r, id);
                var tex = string.Join("\n", ExportTests.Tex(r, Lang.En, o).Split('\n').Where(l => !l.TrimStart().StartsWith("%", StringComparison.Ordinal)));
                var res = ChartCodeImport.Pgfplots(r, tex, Page);
                Assert.True(res.Report.Skipped.Count == 0, id + "\n" + Skipped(res.Report));
                Assert.Equal(o.Type, Assert.Single(res.Charts).Type);
            }
        }
    }
}
