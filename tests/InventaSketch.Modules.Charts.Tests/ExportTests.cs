using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Charts.Tests
{
    /// <summary>Charts v2 stage 4: export of charts as matplotlib scripts and pgfplots documents.</summary>
    public class ExportTests
    {
        internal static string Py(Registry r, Lang lang, params SketchObject[] objects) => new MatplotlibExporter().Export(r, SketchDocument.Create(), objects, lang);

        internal static string Tex(Registry r, Lang lang, params SketchObject[] objects) => new PgfplotsExporter().Export(r, SketchDocument.Create(), objects, lang);

        /// <summary>Part kinds that every export must contain (as a comment naming the part key).</summary>
        private static readonly string[] Kinds = { ChartTags.Series, ChartTags.Bar, ChartTags.Region, ChartTags.Ref, ChartTags.Numeral };

        private static IEnumerable<string> VisibleKeys(Registry r, SketchObject o)
            => r.Parts(o, Fx.Ctx()).Where(p => !p.Hidden && Kinds.Contains(p.Kind)).Select(p => p.Key);

        // ------------------------------------------------------------------ syntax checks

        /// <summary>Python brackets balanced and properly nested (string literals and comments skipped).</summary>
        internal static void AssertPythonBalanced(string code, string what)
        {
            var stack = new Stack<char>();
            for (var i = 0; i < code.Length; i++)
            {
                var c = code[i];
                if (c == '#') { while (i < code.Length && code[i] != '\n') i++; continue; }
                if (c is '\'' or '"')
                {
                    var triple = i + 2 < code.Length && code[i + 1] == c && code[i + 2] == c;
                    var end = triple ? new string(c, 3) : c.ToString();
                    i += end.Length;
                    while (i < code.Length && string.CompareOrdinal(code, i, end, 0, end.Length) != 0)
                    {
                        if (code[i] == '\\') i++;
                        else Assert.False(!triple && code[i] == '\n', what + ": unterminated string near " + i);
                        i++;
                    }
                    i += end.Length - 1;
                    continue;
                }
                if (c is '(' or '[' or '{') stack.Push(c);
                if (c is ')' or ']' or '}')
                {
                    Assert.True(stack.Count > 0, what + ": unbalanced '" + c + "' at " + i);
                    var open = stack.Pop();
                    Assert.True(open == (c == ')' ? '(' : c == ']' ? '[' : '{'), what + ": mismatched '" + c + "' at " + i);
                }
            }
            Assert.True(stack.Count == 0, what + ": unclosed brackets");
        }

        /// <summary>LaTeX braces balanced (escaped braces and comments skipped), environments paired.</summary>
        internal static void AssertLatexBalanced(string tex, string what)
        {
            var depth = 0;
            for (var i = 0; i < tex.Length; i++)
            {
                var c = tex[i];
                if (c == '\\') { i++; continue; }
                if (c == '%') { while (i < tex.Length && tex[i] != '\n') i++; continue; }
                if (c == '{') depth++;
                if (c == '}') { depth--; Assert.True(depth >= 0, what + ": unbalanced '}' at " + i); }
            }
            Assert.True(depth == 0, what + ": unclosed braces");
            foreach (var env in new[] { "document", "tikzpicture", "axis", "groupplot" })
                Assert.Equal(Count(tex, "\\begin{" + env + "}"), Count(tex, "\\end{" + env + "}"));
        }

        internal static int Count(string s, string what) => Regex.Matches(s, Regex.Escape(what)).Count;

        // ------------------------------------------------------------------ provider

        [Fact]
        public void TheModuleOffersBothExportersForChartsOnly()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "chart.line");
            var ids = Exports.For(r, SketchDocument.Create(), new[] { o }).Select(e => e.Id).ToList();
            Assert.Contains("chart.matplotlib", ids);
            Assert.Contains("chart.pgfplots", ids);
            Assert.DoesNotContain(Exports.For(r, SketchDocument.Create(), new SketchObject[0]), e => e.Id.StartsWith("chart."));
            var mpl = new MatplotlibExporter();
            Assert.Equal("py", mpl.Extension);
            Assert.Equal("tex", new PgfplotsExporter().Extension);
            Assert.False(string.IsNullOrEmpty(mpl.Name.De));
            Assert.False(mpl.Applies(r, SketchDocument.Create(), new[] { o with { Type = "other.box" } }));
        }

        // ------------------------------------------------------------------ every library item

        [Theory]
        [InlineData(Lang.En)]
        [InlineData(Lang.De)]
        public void EveryChartItemGivesAPlausiblePythonScript(Lang lang)
        {
            var r = Fx.Reg();
            // Timing diagrams have no data axes; they are reported as not exportable (TimingChartTests).
            foreach (var item in r.Library.Where(i => i.TypeId != TimingChart.TypeId))
            {
                var o = Fx.Item(r, item.Id);
                var py = Py(r, lang, o);
                Assert.False(string.IsNullOrWhiteSpace(py), item.Id);
                Assert.Contains("import matplotlib", py);
                Assert.Contains("def figure_1():", py);
                Assert.Contains("FIGURES = [figure_1]", py);
                Assert.DoesNotContain("could not be exported", py);
                AssertPythonBalanced(py, item.Id);
                foreach (var key in VisibleKeys(r, o)) Assert.True(py.Contains("# " + key), item.Id + ": " + key + " missing");
                Assert.True(py.Contains(".plot(") || py.Contains(".bar") || item.Id.StartsWith("chart.axes"), item.Id + ": nothing plotted");
            }
        }

        [Theory]
        [InlineData(Lang.En)]
        [InlineData(Lang.De)]
        public void EveryChartItemGivesAPlausibleLatexDocument(Lang lang)
        {
            var r = Fx.Reg();
            // Timing diagrams have no data axes; they are reported as not exportable (TimingChartTests).
            foreach (var item in r.Library.Where(i => i.TypeId != TimingChart.TypeId))
            {
                var o = Fx.Item(r, item.Id);
                var tex = Tex(r, lang, o);
                Assert.False(string.IsNullOrWhiteSpace(tex), item.Id);
                Assert.Contains("\\documentclass", tex);
                Assert.Contains("\\usepackage{pgfplots}", tex);
                Assert.True(Count(tex, "\\begin{axis}") + Count(tex, "\\begin{groupplot}") >= 1, item.Id);
                Assert.DoesNotContain("could not be exported", tex);
                AssertLatexBalanced(tex, item.Id);
                foreach (var key in VisibleKeys(r, o)) Assert.True(tex.Contains("% " + key), item.Id + ": " + key + " missing");
                // Every colour used is defined.
                foreach (Match m in Regex.Matches(tex, @"\bc([0-9a-f]{6})\b"))
                    Assert.Contains("\\definecolor{c" + m.Groups[1].Value + "}{HTML}{" + m.Groups[1].Value.ToUpperInvariant() + "}", tex);
            }
        }

        [Fact]
        public void SeveralChartsGoIntoOneFileInSequence()
        {
            var r = Fx.Reg();
            var a = Fx.Item(r, "chart.line");
            var b = Fx.Item(r, "chart.bode");
            var py = Py(r, Lang.En, a, b);
            Assert.Contains("def figure_1():", py);
            Assert.Contains("def figure_2():", py);
            Assert.Contains("FIGURES = [figure_1, figure_2]", py);
            var tex = Tex(r, Lang.En, a, b);
            Assert.Equal(2, Count(tex, "\\begin{tikzpicture}"));
            Assert.Contains("\\documentclass[tikz", tex);
        }

        // ------------------------------------------------------------------ chart types

        [Fact]
        public void XyChartKeepsDataStylesScalesAndLegend()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "chart.line.multi");
            var py = Py(r, Lang.En, o);
            Assert.Contains("label='A'", py);
            Assert.Contains("label='C'", py);
            Assert.Contains("ls='--'", py);
            Assert.Contains("marker='o'", py);
            Assert.Contains(".legend(loc=", py);
            Assert.Contains("[0, 1, 2, 3, 4, 5]", py);
            var tex = Tex(r, Lang.En, o);
            Assert.Contains("\\addlegendentry{A}", tex);
            Assert.Contains("dashed", tex);
            Assert.Contains("(5,4.3)", tex);

            var step = Fx.Item(r, "chart.step");
            Assert.Contains("drawstyle='steps-post'", Py(r, Lang.En, step));
            Assert.Contains("const plot", Tex(r, Lang.En, step));

            var log = Fx.Item(r, "chart.line.logx");
            Assert.Contains("set_xscale('log')", Py(r, Lang.En, log));
            Assert.Contains("xmode=log", Tex(r, Lang.En, log));

            var limits = Fx.Item(r, "chart.limits");
            Assert.Contains(".axhline(1.5", Py(r, Lang.En, limits));
            Assert.Contains("(axis cs:0,5.5) -- (axis cs:6,5.5)", Tex(r, Lang.En, limits));

            // Smooth curves are sampled (more points than the data).
            var smooth = Py(r, Lang.En, Fx.Item(r, "chart.line.smooth"));
            var ys = Regex.Match(smooth, @"series_0_y = \[([^\]]*)\]").Groups[1].Value;
            Assert.True(ys.Split(',').Length > 20, "smooth curve not sampled");
        }

        [Fact]
        public void BarChartsKeepGroupingStackingAndOrientation()
        {
            var r = Fx.Reg();
            var grouped = Py(r, Lang.En, Fx.Item(r, "chart.column.grouped"));
            Assert.Equal(2, Count(grouped, ".bar("));
            Assert.Contains("set_xticklabels(['Q1', 'Q2', 'Q3', 'Q4'])", grouped);
            var stacked = Fx.Item(r, "chart.column.stacked");
            Assert.Contains("bottom=[", Py(r, Lang.En, stacked));
            Assert.Contains("ybar stacked", Tex(r, Lang.En, stacked));
            var horizontal = Fx.Item(r, "chart.bar.horizontal");
            var hp = Py(r, Lang.En, horizontal);
            Assert.Contains(".barh(", hp);
            Assert.Contains(".bar_label(", hp);
            var ht = Tex(r, Lang.En, horizontal);
            Assert.Contains("xbar", ht);
            Assert.Contains("y dir=reverse", ht);
            Assert.Contains("nodes near coords", ht);
            // Monochrome patterns.
            var mono = Fx.Item(r, "chart.column.stacked").WithParam("patterns", true);
            Assert.Contains("hatch=", Py(r, Lang.En, mono));
            Assert.Contains("pattern={", Tex(r, Lang.En, mono));
        }

        [Fact]
        public void EngineeringChartsUseTheirSpecialForms()
        {
            var r = Fx.Reg();
            var torque = Fx.Item(r, "chart.torque");
            Assert.Contains(".twinx()", Py(r, Lang.En, torque));
            Assert.Contains("axis y line=right", Tex(r, Lang.En, torque));
            Assert.Contains("constant torque", Py(r, Lang.En, torque));

            var eff = Fx.Item(r, "chart.efficiency");
            var ep = Py(r, Lang.En, eff);
            Assert.Contains(".contour(", ep);
            Assert.Contains(".clabel(", ep);
            Assert.Contains("levels=[80, 85, 88, 90, 92, 94, 95]", ep);
            Assert.Contains("nan", ep);
            Assert.Contains("% level 90", Tex(r, Lang.En, eff));

            var bode = Fx.Item(r, "chart.bode");
            var bp = Py(r, Lang.En, bode);
            Assert.Contains("plt.subplots(2, 1, sharex=True", bp);
            Assert.Contains("$f_{\\\\mathrm{c}}$", bp);
            var bt = Tex(r, Lang.En, bode);
            Assert.Contains("group size=1 by 2", bt);
            Assert.Contains("$f_{\\mathrm{c}}$", bt);

            var band = Fx.Item(r, "chart.band.measured");
            Assert.Contains(".fill_between(", Py(r, Lang.En, band));
            Assert.Contains("fill between[of=", Tex(r, Lang.En, band));

            var pump = Fx.Item(r, "chart.pump");
            Assert.Contains("'OP'", Py(r, Lang.En, pump));
            Assert.Contains("{BP}", Tex(r, Lang.De, pump));
        }

        // ------------------------------------------------------------------ figures

        [Fact]
        public void PhasesFigureKeepsTheHatchedRegionOnlyWhereOneCurveIsAbove()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "chart.figure.phases");
            var py = Py(r, Lang.En, o);
            Assert.Contains("# only where curve 2 above curve 3", py);
            Assert.Contains("where=[a >= b for a, b in zip(region_0_a, region_0_b)], interpolate=True", py);
            Assert.Contains("hatch='//'", py);
            Assert.Contains("plt.subplots(2, 1, sharex=True", py);
            Assert.Contains("'height_ratios': [1, 1.5]", py);
            Assert.Contains("arrowstyle='<|-|>'", py);
            Assert.Contains("'time gained'", py);
            Assert.Contains("set_xticklabels(['t0', 't1'])", py);
            Assert.Contains("arrow_x(ax)", py);
            var tex = Tex(r, Lang.En, o);
            Assert.Contains("% only where curve 2 above curve 3", tex);
            Assert.Contains("pattern={Lines[angle=45", tex);
            Assert.Contains("\\begin{groupplot}", tex);
            Assert.Contains("{Stealth[length=1.6mm]}-{Stealth[length=1.6mm]}", tex);
            Assert.Contains("{Zeitgewinn}", Tex(r, Lang.De, o));
            // Reference numerals 40 … 46 with leader lines.
            foreach (var num in new[] { "40", "41", "42", "43", "44", "45", "46" })
            {
                Assert.Contains(".annotate('" + num + "'", py);
                Assert.Contains("{" + num + "};", tex);
            }
            Assert.Equal(7, Count(py, "arrowstyle='-', color="));
        }

        [Fact]
        public void TimingFigureDrawsTheVerticalMarkerAcrossAllPanels()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "chart.figure.timing");
            var py = Py(r, Lang.En, o);
            Assert.Contains("ConnectionPatch((6, 1), (6, 0), coordsA=axs[0].get_xaxis_transform(), coordsB=axs[2].get_xaxis_transform()", py);
            Assert.Contains("$t_{\\\\mathrm{krit}}$", py);
            Assert.Contains("'64'", py);
            var tex = Tex(r, Lang.En, o);
            Assert.Contains("group size=1 by 3", tex);
            Assert.Matches(@"\\coordinate \(vline\d+a\) at \(axis cs:6,1\.25\)", tex);
            Assert.Matches(@"\\coordinate \(vline\d+b\) at \(axis cs:6,0\)", tex);
            Assert.Matches(@"\\draw\[[^\]]*\] \(vline\d+a\) -- \(vline\d+b\);", tex);
            // The marker line is drawn after the group (it spans the gaps between the panels).
            Assert.True(tex.IndexOf("(vline", tex.IndexOf("\\end{groupplot}")) > 0);
            Assert.Contains("$t_{\\mathrm{krit}}$", tex);
        }

        [Fact]
        public void TemperatureFigureKeepsGapsPointMarkersAndCaption()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "chart.figure.temperature");
            var py = Py(r, Lang.En, o);
            Assert.Contains("nan", py);
            Assert.Contains("fig.supxlabel('Fig. 3 Temperature over time with activation thresholds'", py);
            Assert.Contains("mfc='none'", py);
            Assert.Contains("'heating ram (15)'", py);
            Assert.Contains("yaxis.grid(True, which='major', color='#bdbdbd', lw=0.37, ls=':')", py);
            var tex = Tex(r, Lang.En, o);
            Assert.Contains("unbounded coords=jump", tex);
            Assert.Contains(",nan)", tex);
            Assert.Contains("% caption", tex);
            Assert.Contains("60~\\textdegree{}C", tex.Replace("60 \\textdegree{}C", "60~\\textdegree{}C"));
        }

        // ------------------------------------------------------------------ part looks

        [Fact]
        public void HiddenPartsAreLeftOut()
        {
            var r = Fx.Reg();
            var o = PartLooks.WithLook(Fx.Item(r, "chart.line.multi"), "series.1", p => p with { Hidden = true });
            o = PartLooks.WithLook(o, "grid", p => p with { Hidden = true });
            o = PartLooks.WithLook(o, "legend", p => p with { Hidden = true });
            var py = Py(r, Lang.En, o);
            Assert.Contains("# series.0", py);
            Assert.DoesNotContain("# series.1", py);
            Assert.Contains("# series.2", py);
            Assert.DoesNotContain(".grid(True", py);
            Assert.DoesNotContain(".legend(", py);
            var tex = Tex(r, Lang.En, o);
            Assert.DoesNotContain("% series.1", tex);
            Assert.DoesNotContain("majorgrids", tex);
            Assert.DoesNotContain("\\addlegendentry", tex);

            var fig = PartLooks.WithLook(Fx.Item(r, "chart.figure.phases"), "region.0", p => p with { Hidden = true });
            fig = PartLooks.WithLook(fig, "numeral", p => p with { Hidden = true });
            fig = PartLooks.WithLook(fig, "title", p => p with { Hidden = true });
            var fpy = Py(r, Lang.En, fig);
            Assert.DoesNotContain("fill_between", fpy);
            Assert.DoesNotContain("reference numeral", fpy);
            var ftex = Tex(r, Lang.En, fig);
            Assert.DoesNotContain("pattern=", ftex);
            Assert.DoesNotContain("reference numeral", ftex);
        }

        [Fact]
        public void PartStylesOverrideColourWidthAndDash()
        {
            var r = Fx.Reg();
            var o = PartLooks.WithLook(Fx.Item(r, "chart.line"), "series.0", p => p with { Style = new Style { Stroke = "#ff0000", StrokeWidth = 1.0, Dash = DashStyle.Dot } });
            var py = Py(r, Lang.En, o);
            Assert.Contains("color='#ff0000', lw=2.83, ls=':'", py);
            var tex = Tex(r, Lang.En, o);
            Assert.Contains("\\definecolor{cff0000}{HTML}{FF0000}", tex);
            Assert.Contains("cff0000, line width=1mm, dotted", tex);

            var fig = PartLooks.WithLook(Fx.Item(r, "chart.figure.phases"), "ref", p => p with { Style = new Style { Stroke = "#00aa00" } });
            Assert.Contains("'#00aa00'", Py(r, Lang.En, fig));
            Assert.Contains("c00aa00", Tex(r, Lang.En, fig));
        }

        // ------------------------------------------------------------------ numbers and texts

        [Fact]
        public void GermanDecimalCommasNeverLeakIntoNumbers()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "chart.line").WithParam("data", "0;1,5\n1;2,25\n2;3,75").WithParam("limits", "2,5").WithParam("yrange", "0,5;4,5");
            var py = Py(r, Lang.De, o);
            var tex = Tex(r, Lang.De, o);
            foreach (var code in new[] { py, tex })
            {
                foreach (var bad in new[] { "1,5", "2,25", "3,75", "2,5", "0,5", "4,5" }) Assert.DoesNotContain(bad, code);
            }
            Assert.Contains("[1.5, 2.25, 3.75]", py);
            Assert.Contains(".axhline(2.5", py);
            Assert.Contains("set_ylim(0.5, 4.5)", py);
            Assert.Contains("FuncFormatter(comma)", py);
            Assert.Contains("(1,2.25)", tex);
            Assert.Contains("/pgf/number format/use comma", tex);
            // German value labels are text (with a decimal comma), the bar values numbers.
            var bars = Fx.Item(r, "chart.bar.horizontal").WithParam("data", "A;4,5\nB;6,5");
            var bpy = Py(r, Lang.De, bars);
            Assert.Contains("[4.5, 6.5]", bpy);
            Assert.Contains("labels=['4,5', '6,5']", bpy);
            Assert.Contains("(6.5,1) [6,5]", Tex(r, Lang.De, bars));
        }

        [Fact]
        public void SpecialCharactersAreEscaped()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "chart.line").WithParam("title", "50% & $5 #1 {a} it's ^~").WithParam("xlabel", "t_krit").WithParam("ylabel", "η in %");
            var py = Py(r, Lang.En, o);
            Assert.Contains("'50% & \\\\$5 #1 {a} it\\'s ^~'", py);
            Assert.Contains("'$t_{\\\\mathrm{krit}}$'", py);
            Assert.Contains("'η in %'", py);
            AssertPythonBalanced(py, "special");
            var tex = Tex(r, Lang.En, o);
            Assert.Contains("50\\% \\& \\$5 \\#1 \\{a\\} it's \\textasciicircum{}\\textasciitilde{}", tex);
            Assert.Contains("xlabel={$t_{\\mathrm{krit}}$}", tex);
            Assert.Contains("ylabel={$\\eta$ in \\%}", tex);
            AssertLatexBalanced(tex, "special");
            // Line breaks and quotes in a figure note.
            var fig = Fx.Item(r, "chart.figure.timing").WithParam("spec", "{\"notes\":[{\"kind\":\"text\",\"x\":1,\"y\":1,\"text\":\"a\\\"b\\nc\\\\d\"}],\"series\":[{\"y\":[1,2]}]}");
            var fpy = Py(r, Lang.En, fig);
            Assert.Contains("'a\"b\\nc\\\\d'", fpy);
            AssertPythonBalanced(fpy, "note");
            var ftex = Tex(r, Lang.En, fig);
            Assert.Contains("a\"b\\\\ c\\textbackslash{}d", ftex);
            AssertLatexBalanced(ftex, "note");
        }

        [Fact]
        public void UnreadableFigureSpecificationStillExports()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "chart.figure.phases").WithParam("spec", "{ not json");
            var py = Py(r, Lang.En, o);
            Assert.Contains("Figure specification cannot be read", py);
            AssertPythonBalanced(py, "error");
            AssertLatexBalanced(Tex(r, Lang.En, o), "error");
        }
    }
}
