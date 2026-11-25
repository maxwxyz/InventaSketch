using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Charts.Tests
{
    /// <summary>Roadmap 4b: matplotlib scripts and pgfplots axes read (never executed) into charts and figures.</summary>
    public class CodeImportTests
    {
        private static readonly Size2 Page = new(280, 157.5);

        private static ChartCodeImport.Result Py(string code) => ChartCodeImport.Matplotlib(Fx.Reg(), code, Page);

        private static ChartCodeImport.Result Tex(string code) => ChartCodeImport.Pgfplots(Fx.Reg(), code, Page);

        private static FigureSpec Spec(SketchObject o)
        {
            Assert.Equal(FigureChart.TypeId, o.Type);
            Assert.True(FigureSpec.TryParse(o.Str("spec", ""), out var spec, out var error), error);
            return spec!;
        }

        private static void Near(double expected, double actual, double tol = 1e-9) => Assert.InRange(actual, expected - tol, expected + tol);

        private static string Skipped(CodeImportReport r) => string.Join("\n", r.Skipped.Select(s => s.Line + ": " + s.Code + " – " + s.Reason.En));

        // ------------------------------------------------------------------ the literal subset

        [Fact]
        public void NumpyArraysAndArithmeticAreEvaluatedWithoutRunningCode()
        {
            const string code = @"
import numpy as np
import matplotlib.pyplot as plt
x = np.linspace(0, 2 * np.pi, 5)
y = 2 * np.sin(x) + 1
z = np.arange(0, 1, 0.25) ** 2
w = [v * 10 for v in [1, 2, 3, 4, 5]]
plt.plot(x, y, 'k--', label='sine')
plt.plot(x, w)
plt.plot(z)
";
            var res = Py(code);
            Assert.Empty(res.Report.Skipped);
            var spec = Spec(Assert.Single(res.Charts));
            Assert.Equal(3, spec.Series.Count);
            var s0 = spec.Series[0];
            Near(Math.PI / 2, s0.X[1]);
            Near(3, s0.Y[1]);
            Assert.Equal("dash", s0.Line);
            Assert.Equal("#000000", s0.Color);
            Assert.Equal("sine", s0.Label!.En);
            Assert.Equal(new double[] { 10, 20, 30, 40, 50 }, spec.Series[1].Y);
            Assert.Equal(new double[] { 0, 1, 2, 3 }, spec.Series[2].X);
            Near(0.5625, spec.Series[2].Y[3]);
        }

        [Fact]
        public void AnythingBeyondTheSubsetIsReportedNotGuessed()
        {
            const string code = @"
import matplotlib.pyplot as plt
import numpy as np
import pandas as pd
import os
os.system('rm -rf /')
x = np.random.rand(10)
y = [1, 2, 3]
if len(y) > 2:
    plt.plot(y)
plt.plot([1, 2, 3], [4, 5, 6])
plt.hist(y)
z = [v if v > 1 else 0 for v in y]
plt.plot(y, label=f'{len(y)} values')
";
            var res = Py(code);
            var lines = res.Report.Skipped.Select(s => s.Line).ToList();
            Assert.Contains(4, lines);   // pandas
            Assert.Contains(6, lines);   // os.system: never run
            Assert.Contains(7, lines);   // random numbers
            Assert.Contains(9, lines);   // if block with plotting code
            Assert.Contains(12, lines);  // hist
            Assert.Contains(13, lines);  // conditional expression
            Assert.Contains(14, lines);  // f-string
            Assert.All(res.Report.Skipped, s => Assert.False(string.IsNullOrEmpty(s.Reason.Get(Lang.De))));
            var spec = Spec(Assert.Single(res.Charts));
            Assert.Single(spec.Series);
            Assert.Equal(new double[] { 4, 5, 6 }, spec.Series[0].Y);
        }

        [Fact]
        public void LoopsAndSafetyLimitsAreBounded()
        {
            var res = Py("import numpy as np\nx = np.linspace(0, 1, 10000000)\nimport matplotlib.pyplot as plt\nfor k in range(3):\n    plt.plot([0, 1], [k, k + 1])\n");
            Assert.Contains(res.Report.Skipped, s => s.Line == 2);
            Assert.Equal(3, Spec(Assert.Single(res.Charts)).Series.Count);
        }

        // ------------------------------------------------------------------ matplotlib

        [Fact]
        public void PyplotStyleGivesAFigureWithLabelsRangesScalesAndLegend()
        {
            const string code = @"
import matplotlib.pyplot as plt
plt.figure(figsize=(6, 4))
plt.semilogy([1, 2, 3], [10, 100, 1000], 'o-', color='tab:red', lw=2, label='Loss')
plt.axhline(50, color='gray', ls=':')
plt.axvline(2.5)
plt.xlabel('Epoch')
plt.ylabel('Value $\eta_{max}$')
plt.title('Training')
plt.xlim(0, 4)
plt.ylim(1, 10000)
plt.grid(True)
plt.legend(loc='upper right')
plt.show()
";
            var res = Py(code);
            Assert.True(res.Report.Skipped.Count == 0, Skipped(res.Report));
            var o = Assert.Single(res.Charts);
            var spec = Spec(o);
            Assert.Equal("Training", spec.Title!.En);
            Assert.Equal("Epoch", spec.X.Label!.En);
            Assert.Equal("Value η_max", spec.Panels[0].Label!.En);
            Assert.Equal("log", spec.Panels[0].Scale);
            Assert.Equal(0, spec.X.Min); Assert.Equal(4, spec.X.Max);
            Assert.Equal(1, spec.Panels[0].Min); Assert.Equal(10000, spec.Panels[0].Max);
            Assert.True(spec.Panels[0].Grid);
            Assert.Equal("inside", spec.Legend);
            var s = Assert.Single(spec.Series);
            Assert.Equal("#d62728", s.Color);
            Assert.Equal("circle", s.Marker);
            Assert.Equal("solid", s.Line);
            Near(2 * 25.4 / 72, s.Width!.Value, 1e-3);
            Assert.Equal(2, spec.Refs.Count);
            Assert.Equal("h", spec.Refs[0].Kind); Assert.Equal(50, spec.Refs[0].Y); Assert.Equal("dot", spec.Refs[0].Line);
            Assert.Equal("v", spec.Refs[1].Kind); Assert.Equal(2.5, spec.Refs[1].X);
            // figsize 6 x 4 inch, inside the page.
            Assert.Equal(152.4, o.Size.W, 1);
            Assert.InRange(o.Transform.X, 0, Page.W);
        }

        [Fact]
        public void StackedSubplotsBecomePanelsOfOneFigure()
        {
            const string code = @"
import matplotlib.pyplot as plt
import numpy as np
t = np.linspace(0, 10, 11)
fig, (ax1, ax2) = plt.subplots(2, 1, sharex=True, gridspec_kw={'height_ratios': [2, 1]})
ax1.plot(t, t ** 2, label='position')
ax1.set_ylabel('s in m')
ax2.step(t, np.ones(11), where='post')
ax2.set_ylabel('on')
ax2.set_xlabel('t in s')
ax1.fill_between(t, t ** 2, 0, alpha=0.3)
ax2.annotate('start', xy=(1, 1), xytext=(3, 0.5), arrowprops=dict(arrowstyle='->'))
fig.suptitle('Motion')
";
            var res = Py(code);
            Assert.True(res.Report.Skipped.Count == 0, Skipped(res.Report));
            var spec = Spec(Assert.Single(res.Charts));
            Assert.Equal(2, spec.Panels.Count);
            Assert.Equal(2, spec.Panels[0].Share);
            Assert.Equal("t in s", spec.X.Label!.En);
            Assert.Equal("Motion", spec.Title!.En);
            Assert.Equal(1, spec.Series[1].Panel);
            Assert.True(spec.Series[1].Step);
            var region = Assert.Single(spec.Regions);
            Assert.Equal("below", region.Kind);
            Assert.Equal(0, region.A);          // the plotted curve is the edge
            Assert.Equal(0, region.Base);
            Assert.Equal(0.3, region.Opacity);
            var note = Assert.Single(spec.Notes);
            Assert.Equal("arrow", note.Kind);
            Assert.Equal(3, note.P1.X); Assert.Equal(0.5, note.P1.Y); Assert.Equal(1, note.P1.Panel);
            Assert.Equal(1, note.P2!.X);
            Assert.Equal("start", note.Text!.En);
        }

        [Fact]
        public void StepsBeforeAndAtTheMiddleAreConvertedExactly()
        {
            var res = Py("import matplotlib.pyplot as plt\nplt.step([0, 1, 2], [5, 6, 7])\nplt.step([0, 2, 4], [1, 2, 3], where='mid')\n");
            var spec = Spec(Assert.Single(res.Charts));
            // pre: (0,5) then post steps holding 6 on (0,1] and 7 on (1,2].
            Assert.Equal(new double[] { 0, 0, 1, 2 }, spec.Series[0].X);
            Assert.Equal(new double[] { 5, 6, 7, 7 }, spec.Series[0].Y);
            Assert.Equal(new double[] { 0, 1, 3, 4 }, spec.Series[1].X);
            Assert.Equal(new double[] { 1, 2, 3, 3 }, spec.Series[1].Y);
        }

        [Fact]
        public void FillBetweenTwoPlottedCurvesUsesThemAndKeepsTheWhereCondition()
        {
            const string code = @"
import numpy as np
import matplotlib.pyplot as plt
x = np.array([0, 1, 2, 3])
a = np.array([1, 3, 2, 4])
b = np.array([2, 2, 2, 2])
fig, ax = plt.subplots()
ax.plot(x, a)
ax.plot(x, b)
ax.fill_between(x, a, b, where=a >= b, interpolate=True, color='none', edgecolor='#000000', hatch='//')
ax.fill_between(x, [0, 0, 0, 0], [5, 5, 5, 5], color='gold', alpha=0.2)
";
            var res = Py(code);
            Assert.True(res.Report.Skipped.Count == 0, Skipped(res.Report));
            var o = Assert.Single(res.Charts);
            var spec = Spec(o);
            Assert.Equal(4, spec.Series.Count);   // two curves and the invisible edges of the second area
            var r0 = spec.Regions[0];
            Assert.Equal("between", r0.Kind);
            Assert.Equal(0, r0.A); Assert.Equal(1, r0.B); Assert.True(r0.Above);
            Assert.Equal("hatch", r0.Fill); Assert.Equal(45, r0.Angle);
            var r1 = spec.Regions[1];
            Assert.Equal("solid", r1.Fill); Assert.Equal("#ffd700", r1.Color);
            Assert.Equal(new[] { 2, 3 }, new[] { r1.A, r1.B });
            Assert.True(PartLooks.LookOf(o.Parts, "series.2")!.Hidden);
            Assert.True(PartLooks.LookOf(o.Parts, "series.3")!.Hidden);
        }

        [Fact]
        public void ScatterKeepsMarkersAndHidesTheLine()
        {
            var res = Py("import matplotlib.pyplot as plt\nplt.scatter([1, 2, 3], [3, 1, 2], marker='s', facecolors='none', edgecolors='b', label='pts')\nplt.legend()\n");
            var o = Assert.Single(res.Charts);
            var s = Assert.Single(Spec(o).Series);
            Assert.Equal("square", s.Marker);
            Assert.True(s.MarkerOpen);
            Assert.Equal("#0000ff", s.Color);
            Assert.True(PartLooks.LookOf(o.Parts, "series.0")!.Hidden);
            // The export writes a marker-only curve again.
            var x = ExportBuilder.Build(Fx.Reg(), SketchDocument.Create(), o, Lang.En)!;
            var xs = Assert.Single(x.Items.OfType<XSeries>());
            Assert.Null(xs.Line);
            Assert.NotNull(xs.Marker);
        }

        [Fact]
        public void BarsBecomeABarChartWithCategories()
        {
            const string code = @"
import matplotlib.pyplot as plt
fig, ax = plt.subplots()
ax.bar(['Q1', 'Q2', 'Q3'], [4, 5.5, 3], label='2024')
ax.set_ylabel('Revenue')
ax.set_title('Sales')
";
            var res = Py(code);
            var o = Assert.Single(res.Charts);
            Assert.Equal(BarChart.TypeId, o.Type);
            var set = ChartData.Categories(ChartData.Parse(o.Str("data", "")));
            Assert.Equal(new[] { "Q1", "Q2", "Q3" }, set.Categories);
            Assert.Equal(new double[] { 4, 5.5, 3 }, set.Series[0].Values);
            Assert.Equal("2024", set.Series[0].Name);
            Assert.Equal("Sales", o.Str("title", ""));
            Assert.Equal("Revenue", o.Str("ylabel", ""));
            Assert.Equal("column", o.Str("orient", ""));
        }

        // ------------------------------------------------------------------ pgfplots

        [Fact]
        public void PgfplotsAxesWithCoordinatesTablesAndExpressionsAreRead()
        {
            const string tex = @"
\documentclass{standalone}
\usepackage{pgfplots}
\definecolor{myblue}{HTML}{1F77B4}
\begin{document}
\begin{tikzpicture}
\begin{semilogyaxis}[xlabel={Time $t$ in s}, ylabel=Signal, xmin=0, xmax=6.3, ymin=0.01, ymax=100, ymajorgrids, legend pos=north west, title={\bfseries Test}]
\addplot[myblue, thick, dashed, mark=square*] coordinates {(0,1) (1,10) (2,nan) (3,100)};
\addlegendentry{measured}
\addplot table [x=t, y=v] {
t v
0 0.5
1 0.6
2 0.7
};
\addlegendentry{table}
\addplot[domain=0:6.283185307, samples=5, red] {2+sin(deg(x))};
\addplot table {data.dat};
\draw[gray, dashed] (axis cs:0,5) -- (axis cs:6.3,5);
\node[anchor=west] at (axis cs:1,20) {Peak};
\end{semilogyaxis}
\end{tikzpicture}
\end{document}";
            var res = Tex(tex);
            var spec = Spec(Assert.Single(res.Charts));
            Assert.Equal("Test", spec.Title!.En);
            Assert.Equal("Time t in s", spec.X.Label!.En);
            Assert.Equal("log", spec.Panels[0].Scale);
            Assert.Equal(0.01, spec.Panels[0].Min);
            Assert.True(spec.Panels[0].Grid);
            Assert.Equal("inside", spec.Legend);
            Assert.Equal(3, spec.Series.Count);
            var a = spec.Series[0];
            Assert.Equal("#1f77b4", a.Color); Assert.Equal("dash", a.Line); Assert.Equal("square", a.Marker); Assert.False(a.MarkerOpen);
            Assert.True(double.IsNaN(a.Y[2]));
            Assert.Equal("measured", a.Label!.En);
            Assert.Equal(new double[] { 0.5, 0.6, 0.7 }, spec.Series[1].Y);
            Assert.Equal("table", spec.Series[1].Label!.En);
            var f = spec.Series[2];
            Assert.Equal(5, f.X.Count);
            Near(2 + Math.Sin(Math.PI / 2), f.Y[1], 1e-6);   // trigonometry in degrees: sin(deg(x)) is sin x
            Assert.Equal("#ff0000", f.Color);
            var h = Assert.Single(spec.Refs);
            Assert.Equal("h", h.Kind); Assert.Equal(5, h.Y); Assert.Equal("dash", h.Line);
            var n = Assert.Single(spec.Notes);
            Assert.Equal("Peak", n.Text!.En); Assert.Equal("start", n.Anchor);
            Assert.Contains(res.Report.Skipped, s => s.Reason.En.Contains("data.dat"));
        }

        [Fact]
        public void PgfplotsGroupplotGivesStackedPanelsAndFillBetweenUsesNamedPaths()
        {
            const string tex = @"
\begin{tikzpicture}
\begin{groupplot}[group style={group size=1 by 2}, xmin=0, xmax=3, xlabel=x]
\nextgroupplot[ylabel=A, height=4cm]
\addplot[name path=upper] coordinates {(0,2) (1,3) (2,2) (3,3)};
\addplot[name path=lower] coordinates {(0,0) (1,1) (2,0) (3,1)};
\addplot[fill=blue, fill opacity=0.2] fill between[of=upper and lower];
\nextgroupplot[ylabel=B, height=2cm]
\addplot[ybar] coordinates {(1,1)};
\addplot coordinates {(0,1) (3,2)};
\end{groupplot}
\end{tikzpicture}";
            var res = Tex(tex);
            var charts = res.Charts.ToList();
            // A panel with bars cannot be stacked into a figure: every axes becomes its own chart.
            Assert.Equal(2, charts.Count);
            var spec = Spec(charts[0]);
            Assert.Equal(2, spec.Series.Count);
            var r = Assert.Single(spec.Regions);
            Assert.Equal(0, r.A); Assert.Equal(1, r.B);
            Assert.Equal(0.2, r.Opacity);
            Assert.Equal(BarChart.TypeId, charts[1].Type);

            var stacked = Tex(tex.Replace(@"\addplot[ybar] coordinates {(1,1)};", ""));
            var s2 = Spec(Assert.Single(stacked.Charts));
            Assert.Equal(2, s2.Panels.Count);
            Assert.Equal(2, s2.Panels[0].Share);
            Assert.Equal("x", s2.X.Label!.En);
            Assert.Equal("B", s2.Panels[1].Label!.En);
        }

        [Fact]
        public void PgfplotsExpressionsConvertDegreesAndRejectUnknownNames()
        {
            Assert.Equal("sin((((x)*57.29577951308232))*0.017453292519943295)", PgfplotsReader.Convert("sin(deg(x))", false));
            Assert.Equal("x^2+lg(x)", PgfplotsReader.Convert("\\x^2+log10(x)", false));
            Assert.Equal("cos(x)", PgfplotsReader.Convert("cos(x)", true));
            var res = Tex("\\begin{axis}\\addplot {max(x,1)};\\addplot {y+1};\\addplot {\\pgfmathresult};\\end{axis}");
            Assert.Empty(res.Charts);
            Assert.Equal(3, res.Report.Skipped.Count);
        }

        // ------------------------------------------------------------------ round trips of our own exports

        private static Registry Reg => Fx.Reg();

        /// <summary>The export model of a chart (what the exporters write).</summary>
        private static XFigure Model(Registry r, SketchObject o) => ExportBuilder.Build(r, SketchDocument.Create(), o, Lang.En)!;

        private static List<XSeries> Curves(XFigure f) => f.Items.OfType<XSeries>().OrderBy(s => s.Panel).ThenBy(s => s.Key).ToList();

        private static void SameNumbers(IReadOnlyList<double> a, IReadOnlyList<double> b, string what)
        {
            Assert.True(a.Count == b.Count, what + ": " + a.Count + " vs " + b.Count + " values");
            for (var i = 0; i < a.Count; i++)
            {
                if (double.IsNaN(a[i])) { Assert.True(double.IsNaN(b[i]), what + " [" + i + "]"); continue; }
                var tol = 1e-6 * Math.Max(1, Math.Abs(a[i]));
                Assert.True(Math.Abs(a[i] - b[i]) <= tol, what + " [" + i + "]: " + a[i] + " vs " + b[i]);
            }
        }

        private static void SameCurves(XFigure before, XFigure after, string what)
        {
            var a = Curves(before); var b = Curves(after);
            Assert.True(a.Count == b.Count, what + ": " + a.Count + " vs " + b.Count + " curves");
            for (var k = 0; k < a.Count; k++)
            {
                Assert.Equal(a[k].Panel, b[k].Panel);
                SameNumbers(a[k].X, b[k].X, what + " curve " + k + " x");
                SameNumbers(a[k].Y, b[k].Y, what + " curve " + k + " y");
                if (before.Legend != "none" && a[k].Legend is not null) Assert.Equal(a[k].Legend, b[k].Legend);
                Assert.Equal(a[k].Step, b[k].Step);
                Assert.Equal(a[k].Line is null, b[k].Line is null);
                Assert.Equal(a[k].Marker is null, b[k].Marker is null);
            }
        }

        private static void SameAxes(XFigure before, XFigure after, string what)
        {
            Assert.Equal(before.X.Kind, after.X.Kind);
            Near(before.X.Min, after.X.Min, 1e-6 * Math.Max(1, Math.Abs(before.X.Min)));
            Near(before.X.Max, after.X.Max, 1e-6 * Math.Max(1, Math.Abs(before.X.Max)));
            Assert.Equal(before.X.Title, after.X.Title);
            Assert.Equal(before.Title, after.Title);
            Assert.Equal(before.Panels.Count, after.Panels.Count);
            for (var i = 0; i < before.Panels.Count; i++)
            {
                var p = before.Panels[i].Y; var q = after.Panels[i].Y;
                Assert.True(p.Kind == q.Kind, what + " panel " + i + " scale");
                Near(p.Min, q.Min, 1e-6 * Math.Max(1, Math.Abs(p.Min)));
                Near(p.Max, q.Max, 1e-6 * Math.Max(1, Math.Abs(p.Max)));
                Assert.Equal(p.Title, q.Title);
            }
        }

        public static IEnumerable<object[]> CurveCharts()
        {
            foreach (var id in new[] { "chart.line", "chart.line.markers", "chart.line.multi", "chart.line.logx", "chart.scatter", "chart.step", "chart.limits",
                         "chart.axes.loglog", "chart.figure.timing", "chart.figure.temperature", "chart.function.sine", "chart.pump", "chart.bode", "chart.band.measured" })
                foreach (var format in new[] { "py", "tex" })
                    yield return new object[] { id, format };
        }

        [Theory]
        [MemberData(nameof(CurveCharts))]
        public void ExportedChartsRoundTrip(string id, string format)
        {
            var r = Reg;
            var o = Fx.Item(r, id);
            var code = format == "py" ? ExportTests.Py(r, Lang.En, o) : ExportTests.Tex(r, Lang.En, o);
            var res = format == "py" ? ChartCodeImport.Matplotlib(r, code, Page) : ChartCodeImport.Pgfplots(r, code, Page);
            var chart = Assert.Single(res.Charts);
            var before = Model(r, o);
            var after = Model(r, chart);
            SameCurves(before, after, id + " (" + format + ")");
            SameAxes(before, after, id + " (" + format + ")");
            Assert.Equal(before.Items.OfType<XHLine>().Select(h => h.Y).OrderBy(v => v), after.Items.OfType<XHLine>().Select(h => h.Y).OrderBy(v => v));
        }

        [Theory]
        [InlineData("chart.column", "py")]
        [InlineData("chart.column", "tex")]
        [InlineData("chart.column.grouped", "py")]
        [InlineData("chart.column.grouped", "tex")]
        [InlineData("chart.column.stacked", "py")]
        [InlineData("chart.column.stacked", "tex")]
        [InlineData("chart.bar.horizontal", "py")]
        [InlineData("chart.bar.horizontal", "tex")]
        public void ExportedBarChartsRoundTrip(string id, string format)
        {
            var r = Reg;
            var o = Fx.Item(r, id);
            var code = format == "py" ? ExportTests.Py(r, Lang.De, o) : ExportTests.Tex(r, Lang.De, o);
            var res = format == "py" ? ChartCodeImport.Matplotlib(r, code, Page) : ChartCodeImport.Pgfplots(r, code, Page);
            var chart = Assert.Single(res.Charts);
            Assert.Equal(BarChart.TypeId, chart.Type);
            var a = ChartData.Categories(ChartData.Parse(o.Str("data", BarChart.DefaultData)), Lang.De);
            var b = ChartData.Categories(ChartData.Parse(chart.Str("data", "")), Lang.De);
            Assert.Equal(a.Categories, b.Categories);
            Assert.Equal(a.Series.Count, b.Series.Count);
            for (var k = 0; k < a.Series.Count; k++) SameNumbers(a.Series[k].Values, b.Series[k].Values, id + " series " + k);
            Assert.Equal(o.Str("orient", "column"), chart.Str("orient", "column"));
            Assert.Equal(a.Series.Count > 1 && o.Str("mode", "grouped") == "stacked", chart.Str("mode", "grouped") == "stacked");
            Assert.Equal(o.Flag("values", false), chart.Flag("values", false));
            var before = Model(r, o); var after = Model(r, chart);
            Assert.Equal(before.Bars!.Series.Select(s => s.Name), after.Bars!.Series.Select(s => s.Name));
        }

        [Theory]
        [InlineData("py")]
        [InlineData("tex")]
        public void EveryExportedChartItemImportsWithoutErrors(string format)
        {
            var r = Reg;
            // Timing diagrams are not exported to matplotlib/pgfplots (the exporters say so), so there is nothing to import.
            foreach (var item in r.Library.Where(i => i.TypeId != TimingChart.TypeId))
            {
                var o = Fx.Item(r, item.Id);
                foreach (var lang in new[] { Lang.En, Lang.De })
                {
                    var code = format == "py" ? ExportTests.Py(r, lang, o) : ExportTests.Tex(r, lang, o);
                    var res = format == "py" ? ChartCodeImport.Matplotlib(r, code, Page) : ChartCodeImport.Pgfplots(r, code, Page);
                    Assert.True(res.Charts.Count >= 1, item.Id + " " + lang + ": nothing imported\n" + Skipped(res.Report));
                    foreach (var c in res.Charts) Fx.AssertFinite(Fx.Render(r, c), item.Id);
                }
            }
        }

        [Fact]
        public void TheOwnExportOfALineChartImportsWithoutReportLines()
        {
            var r = Reg;
            var o = Fx.Item(r, "chart.line.multi");
            foreach (var code in new[] { ExportTests.Py(r, Lang.De, o), ExportTests.Tex(r, Lang.De, o) })
            {
                var res = code.Contains("matplotlib") ? ChartCodeImport.Matplotlib(r, code, Page) : ChartCodeImport.Pgfplots(r, code, Page);
                Assert.True(res.Report.Skipped.Count == 0, Skipped(res.Report));
                Assert.Single(res.Report.Imported);
                Assert.Contains("3", res.Report.Imported[0].Get(Lang.De));
            }
        }

        [Fact]
        public void PrincipleAxesWithNamedMarksRoundTrip()
        {
            var r = Reg;
            var o = Fx.Item(r, "chart.figure.phases");
            Assert.True(FigureSpec.TryParse(o.Str("spec", ""), out var before, out _));
            foreach (var code in new[] { ExportTests.Py(r, Lang.En, o), ExportTests.Tex(r, Lang.En, o) })
            {
                var res = code.Contains("matplotlib") ? ChartCodeImport.Matplotlib(r, code, Page) : ChartCodeImport.Pgfplots(r, code, Page);
                var spec = Spec(Assert.Single(res.Charts));
                Assert.Equal(before!.X.Principle, spec.X.Principle);
                Assert.Equal(before.X.Marks.Select(m => m.Text.En), spec.X.Marks.Select(m => m.Text.En));
                Assert.Equal(before.Panels.Select(p => p.Principle), spec.Panels.Select(p => p.Principle));
            }
        }

        [Fact]
        public void BrokenOrTruncatedCodeNeverThrows()
        {
            var r = Reg;
            foreach (var id in new[] { "chart.figure.timing", "chart.column.grouped", "chart.band.measured" })
            {
                var o = Fx.Item(r, id);
                foreach (var code in new[] { ExportTests.Py(r, Lang.En, o), ExportTests.Tex(r, Lang.En, o) })
                    for (var k = 1; k <= 24; k++)
                    {
                        var cut = code.Substring(0, code.Length * k / 25);
                        ChartCodeImport.Matplotlib(r, cut, Page);
                        ChartCodeImport.Pgfplots(r, cut, Page);
                        TikzImport.Parse(cut, new CodeImportReport());
                    }
            }
            foreach (var junk in new[] { "((((", "[1, 2", "'''", "\\begin{axis}[", "\\addplot coordinates {(1,", "def f(:\n  x", "for x in :", "\\draw (0,0) -- ", "x = [" + new string('[', 500) })
            {
                ChartCodeImport.Matplotlib(r, junk, Page);
                ChartCodeImport.Pgfplots(r, junk, Page);
                TikzImport.Parse(junk, new CodeImportReport());
            }
        }
    }
}
