using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Charts.Tests
{
    /// <summary>Charts v2 stage 4: coordinate systems and function graphs in math style (moved from the math module).</summary>
    public class MathChartTests
    {
        private static List<Primitive> Drawn(Registry r, SketchObject o, Lang lang = Lang.En) => Prim.Flatten(Fx.Render(r, o, Fx.Ctx(lang))).ToList();

        private static Anchor A(Registry r, SketchObject o, string id) => Fx.Type(r, o).Anchors(o, Fx.Ctx()).Single(a => a.Id == id);

        private static ContextAction Act(Registry r, SketchObject o, string id) => r.TypeOf(o).Actions(o, Fx.Ctx()).Single(a => a.Id == id);

        // ------------------------------------------------------------------ expression evaluator

        [Theory]
        [InlineData("x^2 - 2", 3, 7)]
        [InlineData("f(x) = 2x + 1", 2, 5)]
        [InlineData("y = 3(x+1)", 1, 6)]
        [InlineData("-x^2", 2, -4)]
        [InlineData("2^-1", 0, 0.5)]
        [InlineData("2 sin(x)", 0, 0)]
        [InlineData("x sin(pi/2)", 3, 3)]
        [InlineData("|x - 1|", -2, 3)]
        [InlineData("2|x|", -1.5, 3)]
        [InlineData("e^(-x)", 0, 1)]
        [InlineData("exp(1) - e", 0, 0)]
        [InlineData("2,5x", 2, 5)]
        [InlineData("x²+x³", 2, 12)]
        [InlineData("(-8)^(1/3)", 0, -2)]
        [InlineData("sqrt(x) * √4", 9, 6)]
        [InlineData("ln(e^2) + log(100) + log2(8)", 0, 7)]
        [InlineData("1e-3 * 1000 x", 2, 2)]
        [InlineData("2 ** 3 / 4 : 2", 0, 1)]
        [InlineData("π x", 1, Math.PI)]
        [InlineData("sin^2(x) + cos^2(x)", 0.7, 1)]
        [InlineData("xsin(x)", 0, 0)]
        [InlineData("x^3/4 - x", 2, 0)]
        public void ExpressionsEvaluate(string text, double x, double expected)
        {
            var e = FunctionExpression.Parse(text);
            Assert.Equal(expected, e.Evaluate(x), 9);
        }

        [Fact]
        public void ExpressionsOutsideTheDomainAreGaps()
        {
            Assert.True(double.IsNaN(FunctionExpression.Parse("1/x").Evaluate(0)));
            Assert.True(double.IsNaN(FunctionExpression.Parse("ln(x)").Evaluate(-1)));
            Assert.True(double.IsNaN(FunctionExpression.Parse("sqrt(x)").Evaluate(-1)));
            Assert.True(double.IsNaN(FunctionExpression.Parse("exp(x)").Evaluate(1000)));
        }

        [Theory]
        [InlineData("")]
        [InlineData("x +")]
        [InlineData("foo(x)")]
        [InlineData("(x + 1")]
        [InlineData("x ) 2")]
        [InlineData("System.IO.File(x)")]
        [InlineData("2 # 3")]
        public void InvalidExpressionsGiveBilingualMessages(string text)
        {
            Assert.False(FunctionExpression.TryParse(text, out _, out var error));
            Assert.False(string.IsNullOrEmpty(error!.En));
            Assert.False(string.IsNullOrEmpty(error.De));
            Assert.ThrowsAny<FormatException>(() => FunctionExpression.Parse(text));
        }

        [Fact]
        public void ExpressionLengthAndDepthAreLimited()
        {
            Assert.False(FunctionExpression.TryParse(new string('(', 100) + "x" + new string(')', 100), out _, out _));
            Assert.False(FunctionExpression.TryParse(string.Join("+", Enumerable.Repeat("x", 300)), out _, out _));
            Assert.Equal("x³ − 2·x", FunctionExpression.Parse("x^3 - 2*x").Pretty());
        }

        [Fact]
        public void FamilyFormulasReadBackAsTheSameFunction()
        {
            foreach (var kind in MathFunctions.Kinds.Select(k => k.Value).Where(k => k is not ("expression" or "generator")))
                foreach (var (a, b, c, d) in new[] { (1.0, 1.0, 0.0, 0.0), (2.0, -0.5, 1.5, -3.0), (-1.0, 2.0, -1.0, 0.25) })
                {
                    var text = FunctionChart.ExpressionOf(kind, a, b, c, d);
                    var e = FunctionExpression.Parse(text);
                    foreach (var x in new[] { -2.3, -0.7, 0.4, 1.9, 3.3 })
                    {
                        var want = a * MathFunctions.Base(kind, b * (x - c)) + d;
                        var got = e.Evaluate(x);
                        if (double.IsNaN(want)) Assert.True(double.IsNaN(got), kind + " " + text + " at " + x);
                        else Assert.Equal(want, got, 9);
                    }
                }
        }

        // ------------------------------------------------------------------ coordinate system

        [Fact]
        public void CoordinateSystemCrossesAtTheOriginWithArrowsAndGridPoints()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "chart.axes.math");
            Assert.Equal("chart.math.axes", o.Type);
            var drawn = Drawn(r, o);
            var x = (LinePrim)drawn.Single(p => p.Tag == "axis.x");
            var y = (LinePrim)drawn.Single(p => p.Tag == "axis.y");
            Assert.Equal(ArrowHead.Triangle, x.Style.ArrowEnd);
            Assert.Equal(ArrowHead.Triangle, y.Style.ArrowEnd);
            var origin = A(r, o, "origin");
            Assert.Equal(AnchorRole.Port, origin.Role);
            Assert.Equal(x.A.Y, origin.P.Y, 9);
            Assert.Equal(y.A.X, origin.P.X, 9);
            // −5 … 5 and −4 … 4: the origin is in the middle of the plot.
            Assert.Equal((A(r, o, "plot.tl").P.X + A(r, o, "plot.tr").P.X) / 2, origin.P.X, 6);
            Assert.Contains(drawn.OfType<TextPrim>(), t => t.Text == "0");
            Assert.Contains(drawn.OfType<TextPrim>(), t => t.Text == "x" && t.TextStyle.Italic);
            Assert.Contains(drawn.OfType<TextPrim>(), t => t.Text == "y" && t.TextStyle.Italic);
            // Grid points to dock on, and the axes as rails.
            var g = A(r, o, "g2_3");
            Assert.Equal(AnchorRole.Mid, g.Role);
            Assert.Equal(AnchorKind.Segment, A(r, o, "axis.x").Kind);
            Assert.Equal(AnchorKind.Segment, A(r, o, "axis.y").Kind);
            Assert.True(g.P.X > origin.P.X && g.P.Y < origin.P.Y);
        }

        [Fact]
        public void FirstQuadrantKeepsTheAxesAtTheLowerLeftAndEverythingInside()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "chart.axes.math.quadrant");
            var origin = A(r, o, "origin");
            Assert.Equal(A(r, o, "plot.bl").P, origin.P);
            foreach (var size in new[] { o.Size, new Size2(30, 25), new Size2(150, 40), new Size2(25, 90) })
                Fx.AssertInsideBox(Fx.Render(r, o with { Size = size }), size, "quadrant " + size.W);
            // Ranges entirely below zero: the axes run along the upper/right edge.
            var neg = o.WithParam("xmin", -6.0).WithParam("xmax", -1.0).WithParam("ymin", -5.0).WithParam("ymax", -1.0);
            Assert.Equal(A(r, neg, "plot.tr").P, A(r, neg, "origin").P);
            Fx.AssertInsideBox(Fx.Render(r, neg), neg.Size, "negative");
        }

        [Fact]
        public void CoordinateSystemActionsWork()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "chart.axes.math");
            var noGrid = Act(r, o, "chart.grid").Apply(o);
            Assert.DoesNotContain(Drawn(r, noGrid), p => p.Tag == ChartTags.Grid);
            var noNumbers = Act(r, o, "chart.numbers").Apply(o);
            Assert.DoesNotContain(Drawn(r, noNumbers), p => p.Tag == "label.x" || p.Tag == "label.y");
            var range = Act(r, o, "chart.xrange");
            Assert.Equal("-5;5", range.Input!.Value);
            var wide = range.Input.Apply(o, "-10;20");
            Assert.Equal(-10, wide.Num("xmin", 0));
            Assert.Equal(20, wide.Num("xmax", 0));
            Assert.Throws<FormatException>(() => range.Input.Apply(o, "abc"));
            var broken = Act(r, o, "chart.ybreak").Input!.Apply(o, "1-3");
            Assert.Contains(r.Parts(broken, Fx.Ctx()), p => p.Key == "axisbreak.y");
            Fx.AssertInsideBox(Fx.Render(r, broken), broken.Size, "math break");
        }

        // ------------------------------------------------------------------ function graph

        [Fact]
        public void ParabolaHasItsFormulaAndKeyPointPorts()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "chart.function");
            var drawn = Drawn(r, o);
            Assert.Contains(drawn, p => p.Tag == "series.0");
            var formula = drawn.OfType<TextPrim>().Single(t => t.Tag == ChartTags.Formula);
            Assert.Equal("f(x) = x²", formula.Text);
            var origin = A(r, o, "origin");
            Assert.Equal(origin.P.X, A(r, o, "ext0").P.X, 6);
            Assert.Equal(origin.P.Y, A(r, o, "ext0").P.Y, 6);
            Assert.Equal(origin.P.X, A(r, o, "yint").P.X, 6);
            Assert.Equal(AnchorRole.Port, A(r, o, "zero0").Role);
            // The curve names its part.
            Assert.Equal("f(x) = x²", r.Parts(o, Fx.Ctx()).Single(p => p.Key == "series.0").Label.En);
            Assert.Contains(r.Parts(o, Fx.Ctx()), p => p.Key == ChartTags.Formula && p.Label.De == "Funktionsgleichung");
        }

        [Fact]
        public void LinearFunctionFormulaUsesTheDecimalCommaInGerman()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "chart.function.linear");
            Assert.Equal("f(x) = 0.5x + 1", Drawn(r, o).OfType<TextPrim>().Single(t => t.Tag == ChartTags.Formula).Text);
            Assert.Equal("f(x) = 0,5x + 1", Drawn(r, o, Lang.De).OfType<TextPrim>().Single(t => t.Tag == ChartTags.Formula).Text);
            // Zero at x = −2.
            var z = A(r, o, "zero0");
            var c = Fx.Type(r, o).Compose(o, Fx.Ctx());
            Assert.Equal(c.F.P(c.F.Panels[0], -2, 0).X, z.P.X, 4);
        }

        [Fact]
        public void SineUsesPiTicks()
        {
            var r = Fx.Reg();
            var texts = Drawn(r, Fx.Item(r, "chart.function.sine")).OfType<TextPrim>().Where(t => t.Tag == "label.x").Select(t => t.Text).ToList();
            Assert.Contains("π", texts);
            Assert.Contains("2π", texts);
            Assert.Contains("−π", texts);
            Assert.Equal("π/2", MathAxes.PiLabel(1));
            Assert.Equal("−3π/2", MathAxes.PiLabel(-3));
        }

        [Fact]
        public void ReciprocalDrawsNoLineThroughThePole()
        {
            var r = Fx.Reg();
            foreach (var o in new[]
            {
                Fx.Item(r, "chart.function").WithParam("function", "reciprocal").WithParam("ymin", -5.0).WithParam("ymax", 5.0),
                Fx.Item(r, "chart.function.formula").WithParam("expr", "1/(x - 0.5)").WithParam("ymin", -5.0).WithParam("ymax", 5.0),
            })
            {
                var runs = FunctionChart.DataRuns(o, r.TypeOf(o), Lang.En);
                Assert.True(runs.Count >= 2);
                var pole = o.Str("function", "") == "reciprocal" ? 0 : 0.5;
                Assert.All(runs, run => Assert.True(run.All(p => p.X < pole) || run.All(p => p.X > pole), "a run crosses the pole"));
                Assert.All(runs.SelectMany(x => x), p => Assert.InRange(p.Y, -5 - 1e-6, 5 + 1e-6));
            }
        }

        [Fact]
        public void FormulaAndGeneratorItemsDraw()
        {
            var r = Fx.Reg();
            var f = Fx.Item(r, "chart.function.formula");
            Assert.Equal("f(x) = x³/4 − x", Drawn(r, f).OfType<TextPrim>().Single(t => t.Tag == ChartTags.Formula).Text);
            Assert.Equal(3, Fx.Type(r, f).Anchors(f, Fx.Ctx()).Count(a => a.Id is { } id && id.StartsWith("zero", StringComparison.Ordinal)));
            var g = Fx.Item(r, "chart.function.generator");
            var gd = Drawn(r, g);
            Assert.Contains(gd, p => p.Tag == "series.0");
            Assert.StartsWith("f(x) = pt1(", gd.OfType<TextPrim>().Single(t => t.Tag == ChartTags.Formula).Text);
            Assert.Contains(gd.OfType<TextPrim>(), t => t.Text == "t" && t.Tag == "axistitle.x");
            // A step generator keeps its exact jump.
            var step = Act(r, g, "chart.function.generator").Input!.Apply(g, "step(x0=2, y0=0.5, y1=3)");
            var run = FunctionChart.DataRuns(step, r.TypeOf(step), Lang.En).Single();
            Assert.Contains(new Vec2(2, 0.5), run);
            Assert.Contains(new Vec2(2, 3), run);
            Assert.ThrowsAny<FormatException>(() => Act(r, g, "chart.function.generator").Input!.Apply(g, "wobble(1)"));
        }

        [Fact]
        public void UnreadableFormulaRendersAMessage()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "chart.function.formula").WithParam("expr", "x +* 2");
            var drawn = Drawn(r, o);
            Assert.DoesNotContain(drawn, p => p.Tag == "series.0");
            Assert.Contains(drawn.OfType<TextPrim>(), t => t.Tag == ChartTags.Error && t.Text == "Formula cannot be read");
            Fx.AssertInsideBox(Fx.Render(r, o), o.Size, "unreadable");
        }

        [Fact]
        public void FormulaInputSwitchesToATypedFormula()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "chart.function.linear");
            var a = Act(r, o, "chart.function.formula");
            Assert.Equal("0.5*x + 1", a.Input!.Value);
            var same = a.Input.Apply(o, a.Input.Value);
            Assert.Equal("expression", same.Str("function", ""));
            Assert.Equal(A(r, o, "zero0").P.X, A(r, same, "zero0").P.X, 3);
            var other = a.Input.Apply(o, "x^2 - 4");
            Assert.Equal(2, Fx.Type(r, other).Anchors(other, Fx.Ctx()).Count(x => x.Id is { } id && id.StartsWith("zero", StringComparison.Ordinal)));
            Assert.ThrowsAny<FormatException>(() => a.Input.Apply(o, "x +"));
            var coef = Act(r, o, "chart.function.coefficients").Input!.Apply(o, "2; 1; 1; -3");
            Assert.Equal(2, coef.Num("a", 0));
            Assert.Equal(-3, coef.Num("d", 0));
        }

        [Fact]
        public void FunctionGraphWithAxisBreakInterruptsTheCurve()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "chart.function.exponential").WithParam("ybreaks", "2-5");
            Fx.AssertInsideBox(Fx.Render(r, o), o.Size, "exp break");
            var c = Fx.Type(r, o).Compose(o, Fx.Ctx());
            var p = c.F.Panels[0];
            double top = c.F.P(p, 0, 5).Y, bottom = c.F.P(p, 0, 2).Y;
            var series = Drawn(r, o).Single(x => x.Tag == "series.0");
            Assert.DoesNotContain(Prim.Points(series), q => q.Y > top + 0.01 && q.Y < bottom - 0.01);
            Assert.Contains(r.Parts(o, Fx.Ctx()), x => x.Key == "axisbreak.y");
        }

        [Fact]
        public void FunctionGraphExportsTheCurve()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "chart.function.sine");
            var py = ExportTests.Py(r, Lang.En, o);
            Assert.Contains(".plot(", py);
            Assert.Contains("# Note: Math-style axes", py);
            ExportTests.AssertPythonBalanced(py, "sine");
            ExportTests.AssertLatexBalanced(ExportTests.Tex(r, Lang.De, o), "sine");
        }
    }
}
