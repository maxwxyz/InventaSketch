using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Geometry.Tests
{
    public class NumberAndFormulaTests
    {
        [Fact]
        public void NumbersUseTheDecimalCommaInGermanAndTheMinusSign()
        {
            Assert.Equal("−2.5", Mx.Num(-2.5, Lang.En));
            Assert.Equal("−2,5", Mx.Num(-2.5, Lang.De));
            Assert.Equal("0", Mx.Num(-0.0000001, Lang.En));
            Assert.Equal("0.3", Mx.Num(0.1 + 0.2, Lang.En));
        }

        [Theory]
        [InlineData("-5;5", -5, 5)]
        [InlineData("−2,5..2,5", -2.5, 2.5)]
        [InlineData("0 10", 0, 10)]
        [InlineData("10 bis 0", 0, 10)]
        [InlineData("1 to 3", 1, 3)]
        [InlineData("-1…4", -1, 4)]
        public void RangesAreParsed(string text, double min, double max)
        {
            var (a, b) = Mx.ParseRange(text);
            Assert.Equal(min, a, 9);
            Assert.Equal(max, b, 9);
        }

        [Theory]
        [InlineData("abc")]
        [InlineData("5")]
        [InlineData("3;3")]
        public void InvalidRangesAreRejected(string text) => Assert.Throws<FormatException>(() => Mx.ParseRange(text));

        [Theory]
        [InlineData(0, "0")]
        [InlineData(1, "π/2")]
        [InlineData(2, "π")]
        [InlineData(3, "3π/2")]
        [InlineData(4, "2π")]
        [InlineData(-1, "−π/2")]
        [InlineData(-2, "−π")]
        public void PiTicksAreLabelledInMultiplesOfHalfPi(long halves, string label) => Assert.Equal(label, Ticks.PiLabel(halves));

        [Theory]
        [InlineData("quadratic", 1, 1, 0, 0, "f(x) = x²")]
        [InlineData("quadratic", 2, 1, 1, -3, "f(x) = 2(x − 1)² − 3")]
        [InlineData("quadratic", 1, 2, 0, 0, "f(x) = 4x²")]
        [InlineData("linear", 0.5, 1, 0, 1, "f(x) = 0.5x + 1")]
        [InlineData("linear", 2, 1, 1, 0, "f(x) = 2x − 2")]
        [InlineData("cubic", -1, 1, -2, 0, "f(x) = −(x + 2)³")]
        [InlineData("sine", 1, 1, 0, 0, "f(x) = sin(x)")]
        [InlineData("sine", 2, 3, 0, 1, "f(x) = 2·sin(3x) + 1")]
        [InlineData("cosine", 1, 1, 1, 0, "f(x) = cos(x − 1)")]
        [InlineData("exponential", 1, 1, 0, 0, "f(x) = eˣ")]
        [InlineData("exponential", 1, -1, 0, 0, "f(x) = e⁻ˣ")]
        [InlineData("exponential", 3, 2, 1, 0, "f(x) = 3·e²⁽ˣ⁻¹⁾")]
        [InlineData("exponential", 1, 0.5, 0, 0, "f(x) = exp(0.5x)")]
        [InlineData("logarithm", 1, 1, 0, 0, "f(x) = ln(x)")]
        [InlineData("sqrt", 1, 1, 0, 0, "f(x) = √x")]
        [InlineData("abs", 1, 1, 2, 0, "f(x) = |x − 2|")]
        [InlineData("reciprocal", 1, 1, 0, 0, "f(x) = 1/x")]
        [InlineData("reciprocal", -2, 1, 1, 3, "f(x) = −2/(x − 1) + 3")]
        [InlineData("sine", 0, 1, 0, 2, "f(x) = 2")]
        public void FormulasUseSchoolNotation(string kind, double a, double b, double c, double d, string expected)
            => Assert.Equal(expected, Fn.Formula(new FnSpec(kind, a, b, c, d), Lang.En));

        [Fact]
        public void GermanFormulasUseTheDecimalComma()
            => Assert.Equal("f(x) = 0,5x + 1", Fn.Formula(new FnSpec("linear", 0.5, 1, 0, 1), Lang.De));

        [Fact]
        public void EveryFunctionEvaluatesTheTransformedFamily()
        {
            Assert.Equal(2 * Math.Sin(3 * (1.2 - 0.5)) + 1, new FnSpec("sine", 2, 3, 0.5, 1).At(1.2), 12);
            Assert.Equal(-4, new FnSpec("quadratic", 1, 1, 0, -4).At(0), 12);
            Assert.True(double.IsNaN(new FnSpec("logarithm", 1, 1, 0, 0).At(-1)));
            Assert.True(double.IsNaN(new FnSpec("sqrt", 1, 1, 2, 0).At(1)));
        }
    }

    public class AxesAndGraphTests
    {
        private static Plot2D PlotOf(Registry r, SketchObject o)
        {
            var t = r.TypeOf(o);
            return Plot2D.Create(AxesSpec.From(o, t), o.Size, Mx.Scale(o.Size, t.DefaultSize), Lang.En);
        }

        [Fact]
        public void TheOriginAndTheGridPointsAreAnchors()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "math.axes.cartesian");
            var p = PlotOf(r, o);
            Assert.True(Fx.Anchor(r, o, "origin").P.DistanceTo(p.Map(0, 0)) < 1e-9);
            Assert.True(Fx.Anchor(r, o, "g3_-2").P.DistanceTo(p.Map(3, -2)) < 1e-9);
            // The origin lies in the middle of the symmetric ranges.
            Assert.True(Math.Abs(p.MapX(0) - p.Area.Center.X) < 1e-9);
        }

        [Fact]
        public void TheFirstQuadrantHasItsOriginBottomLeft()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "math.axes.cartesian.quadrant");
            var p = PlotOf(r, o);
            var origin = Fx.Anchor(r, o, "origin").P;
            Assert.True(origin.DistanceTo(new Vec2(p.Area.X, p.Area.Bottom)) < 1e-9);
            // A single "0" at the origin, numbers 1 … 6 on the x axis.
            var texts = Fx.Texts(r, o).ToList();
            Assert.Single(texts, "0");
            foreach (var n in new[] { "1", "2", "3", "4", "5", "6" }) Assert.Contains(n, texts);
        }

        [Fact]
        public void FreeScalingStretchesTheUnitsAndKeepsTheRanges()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "math.axes.cartesian");
            var wide = o with { Size = new Size2(o.Size.W * 2, o.Size.H) };
            double Unit(SketchObject x) => Fx.Anchor(r, x, "g1_0").P.X - Fx.Anchor(r, x, "origin").P.X;
            Assert.True(Unit(wide) > Unit(o) * 1.8);
            Assert.Equal(o.Params.Count, wide.Params.Count);
        }

        [Fact]
        public void DenseTicksAreLabelledSparselySoNumbersNeverOverlap()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "math.axes.cartesian").WithParam("xMin", -50.0).WithParam("xMax", 50.0);
            var numbers = Prim.Flatten(Fx.Render(r, o)).OfType<TextPrim>().Where(t => t.Tag == MathTags.Number && t.TextStyle.Anchor == TextAnchor.Middle)
                .OrderBy(t => t.Position.X).ToList();
            Assert.True(numbers.Count > 2);
            for (var i = 1; i < numbers.Count; i++)
            {
                var gap = numbers[i].Position.X - numbers[i - 1].Position.X;
                var need = (Mx.TextW(numbers[i].Text, numbers[i].TextStyle.Size) + Mx.TextW(numbers[i - 1].Text, numbers[i - 1].TextStyle.Size)) / 2;
                Assert.True(gap >= need, $"numbers {numbers[i - 1].Text} and {numbers[i].Text} overlap");
            }
        }

        [Fact]
        public void TheParabolaIsClippedToThePlotAreaAndHasItsKeyPoints()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "math.graph.function").WithParam("d", -4.0).WithParam("yMin", -5.0);
            var p = PlotOf(r, o);
            var curve = Fx.Tagged(r, o, MathTags.Curve).Cast<PathPrim>().ToList();
            Assert.Single(curve);
            var pts = curve[0].Segments.Select(s => s.P).ToList();
            Assert.All(pts, q => Assert.True(q.Y >= p.Area.Y - 1e-6 && q.Y <= p.Area.Bottom + 1e-6 && q.X >= p.Area.X - 1e-6 && q.X <= p.Area.Right + 1e-6));
            // y = x² − 4 leaves the range y ≤ 8 at x = ±√12.
            Assert.True(pts[0].DistanceTo(p.Map(-Math.Sqrt(12), 8)) < 1e-4);
            Assert.True(pts[pts.Count - 1].DistanceTo(p.Map(Math.Sqrt(12), 8)) < 1e-4);
            Assert.True(Fx.Anchor(r, o, "zero0").P.DistanceTo(p.Map(-2, 0)) < 1e-6);
            Assert.True(Fx.Anchor(r, o, "zero1").P.DistanceTo(p.Map(2, 0)) < 1e-6);
            Assert.True(Fx.Anchor(r, o, "ext0").P.DistanceTo(p.Map(0, -4)) < 1e-4);
            Assert.True(Fx.Anchor(r, o, "yint").P.DistanceTo(p.Map(0, -4)) < 1e-9);
            Assert.Contains("f(x) = x² − 4", Fx.Texts(r, o));
        }

        [Fact]
        public void TheHyperbolaIsDrawnInTwoBranchesWithoutAJumpAcrossThePole()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "math.graph.function").WithParam("function", "reciprocal").WithParam("yMin", -5.0).WithParam("yMax", 5.0);
            var p = PlotOf(r, o);
            var branches = Fx.Tagged(r, o, MathTags.Curve).Cast<PathPrim>().ToList();
            Assert.Equal(2, branches.Count);
            var x0 = p.MapX(0);
            Assert.All(branches[0].Segments, s => Assert.True(s.P.X < x0));
            Assert.All(branches[1].Segments, s => Assert.True(s.P.X > x0));
        }

        [Fact]
        public void TheSineCurveHasPiTicksAndItsZerosAndExtrema()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "math.graph.function.sine");
            var texts = Fx.Texts(r, o).ToList();
            Assert.Contains("π", texts);
            Assert.Contains("2π", texts);
            var p = PlotOf(r, o);
            var anchors = r.TypeOf(o).Anchors(o, Fx.Ctx()).ToList();
            var zeros = anchors.Where(a => a.Id!.StartsWith("zero", StringComparison.Ordinal)).ToList();
            Assert.Equal(5, zeros.Count); // −2π, −π, 0, π, 2π
            Assert.Contains(zeros, z => z.P.DistanceTo(p.Map(Math.PI, 0)) < 1e-6);
            Assert.Contains(anchors, a => a.Id!.StartsWith("ext", StringComparison.Ordinal) && a.P.DistanceTo(p.Map(Math.PI / 2, 1)) < 1e-3);
        }

        [Fact]
        public void TheLogarithmStartsAtTheBottomOfThePlot()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "math.graph.function").WithParam("function", "logarithm").WithParam("xMin", -1.0).WithParam("yMin", -3.0).WithParam("yMax", 3.0);
            var p = PlotOf(r, o);
            var curve = Fx.Tagged(r, o, MathTags.Curve).Cast<PathPrim>().Single();
            var first = curve.Segments[0].P;
            Assert.True(first.DistanceTo(p.Map(Math.Exp(-3), -3)) < 1e-3);
        }

        [Fact]
        public void TheCoefficientsInputSetsTheFunction()
        {
            var r = Fx.Reg();
            var doc = SketchDocument.Create();
            var o = Fx.Item(r, "math.graph.function");
            doc = Edits.Add(doc, doc.Pages[0].Id, o);
            doc = ContextActions.Run(r, doc, new[] { o.Id }, "math.graph.coefficients", "2; 1; 1; -3", Lang.De);
            var x = doc.Get(o.Id)!;
            Assert.Equal(2, x.Num("a", 0));
            Assert.Equal(-3, x.Num("d", 0));
            Assert.Contains("f(x) = 2(x − 1)² − 3", Fx.Texts(r, x, Lang.De));
        }

        [Fact]
        public void TheRangeInputAcceptsGermanNotation()
        {
            var r = Fx.Reg();
            var doc = SketchDocument.Create();
            var o = Fx.Item(r, "math.axes.cartesian");
            doc = Edits.Add(doc, doc.Pages[0].Id, o);
            doc = ContextActions.Run(r, doc, new[] { o.Id }, "math.axes.xrange", "−2,5;7,5", Lang.De);
            Assert.Equal(-2.5, doc.Get(o.Id)!.Num("xMin", 0));
            Assert.Equal(7.5, doc.Get(o.Id)!.Num("xMax", 0));
        }

        [Fact]
        public void ObliqueAxesKeepTheirAnglesUnderFreeScaling()
        {
            var r = Fx.Reg();
            foreach (var (item, deg) in new[] { ("math.axes.spatial", 135.0), ("math.axes.spatial.isometric", 150.0) })
            {
                var o = Fx.Item(r, item);
                foreach (var size in new[] { o.Size, new Size2(o.Size.W * 2, o.Size.H * 0.7) })
                {
                    var x = o with { Size = size };
                    var origin = Fx.Anchor(r, x, "origin").P;
                    var tick = Fx.Anchor(r, x, "x_1").P;
                    // The x axis points down-left: −135° (oblique) or −150° (isometric) in the mathematical sense.
                    var angle = Geo.RadToDeg(Math.Atan2(-(tick.Y - origin.Y), tick.X - origin.X));
                    Assert.Equal(-deg, angle, 6);
                }
            }
        }

        [Fact]
        public void PolarGridLabelsAnglesInDegreesOrRadians()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "math.axes.polar");
            Assert.Contains("30°", Fx.Texts(r, o));
            var rad = Fx.Texts(r, o.WithParam("unit", "rad")).ToList();
            foreach (var s in new[] { "π/6", "π/3", "π/2", "2π/3", "5π/6", "π", "7π/6", "11π/6" }) Assert.Contains(s, rad);
            Assert.Contains(r.TypeOf(o).Anchors(o, Fx.Ctx()), a => a.Id == "r4a90");
        }

        [Fact]
        public void TheNumberLineMarksZeroAndEveryTick()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "math.axes.number");
            var anchors = r.TypeOf(o).Anchors(o, Fx.Ctx()).ToList();
            Assert.Contains(anchors, a => a.Id == "zero");
            Assert.Equal(11, anchors.Count(a => a.Id!.StartsWith("t", StringComparison.Ordinal)));
            var minor = o.WithParam("minor", 4);
            Assert.True(Fx.Tagged(r, minor, MathTags.Tick).Count() > Fx.Tagged(r, o, MathTags.Tick).Count() + 30);
        }
    }

    public class VectorAndAngleTests
    {
        [Fact]
        public void TheVectorHasItsNameWithAnArrowAndOptionalComponents()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "math.vector.arrow");
            Assert.Contains("v", Fx.Texts(r, o));
            // The arrow notation is a small arrow line above the letter.
            Assert.Contains(Fx.Tagged(r, o, MathTags.Label), p => p is LinePrim l && l.Style.ArrowEnd == ArrowHead.Triangle);
            Assert.Empty(Fx.Tagged(r, o, MathTags.Helper));
            var c = o.WithParam("components", true);
            var helpers = Fx.Tagged(r, c, MathTags.Helper).Cast<LinePrim>().ToList();
            Assert.Equal(2, helpers.Count);
            Assert.True(Math.Abs(helpers[0].A.Y - helpers[0].B.Y) < 1e-9); // horizontal component first
            Assert.True(Math.Abs(helpers[1].A.X - helpers[1].B.X) < 1e-9);
            Assert.Contains("x", Fx.Texts(r, c));
            Assert.Contains("y", Fx.Texts(r, c));
            var bold = Prim.Flatten(Fx.Render(r, o.WithParam("notation", "bold"))).OfType<TextPrim>().Single(t => t.Text == "v");
            Assert.True(bold.TextStyle.Bold);
        }

        [Fact]
        public void ThePositionVectorStartsAtOAndEndsAtP()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "math.vector.arrow.position");
            var texts = Fx.Texts(r, o).ToList();
            Assert.Contains("O", texts);
            Assert.Contains("P", texts);
            Assert.Equal(2, Fx.Tagged(r, o, MathTags.Dot).Count());
        }

        [Fact]
        public void TheVectorSumDrawsTheResultantAndKeepsItWhenTheConstructionChanges()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "math.vector.sum");
            var pts = o.Points!;
            var expected = pts[1] + (pts[2] - pts[0]);
            var res = Fx.Tagged(r, o, MathTags.Resultant).Cast<LinePrim>().Single();
            Assert.True(res.A.DistanceTo(pts[0]) < 1e-9 && res.B.DistanceTo(expected) < 1e-9);
            Assert.Equal(2, Fx.Tagged(r, o, MathTags.Helper).Count()); // dashed parallelogram sides
            var tail = r.SetParam(o, "construction", "tail");
            Assert.True(tail.Points![2].DistanceTo(expected) < 1e-9);
            var res2 = Fx.Tagged(r, tail, MathTags.Resultant).Cast<LinePrim>().Single();
            Assert.True(res2.B.DistanceTo(expected) < 1e-9);
            Assert.Empty(Fx.Tagged(r, tail, MathTags.Helper));
            var back = r.SetParam(tail, "construction", "parallelogram");
            Assert.True(back.Points![2].DistanceTo(pts[2]) < 1e-9);
            // The resultant label "a + b" is three tokens with arrows over a and b.
            var texts = Fx.Texts(r, o).ToList();
            Assert.Equal(2, texts.Count(t => t == "a"));
            Assert.Contains("+", texts);
        }

        [Fact]
        public void TheAngleIsMeasuredFromThePoints()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "math.angle.mark.value");
            Assert.Contains("α = 45°", Fx.Texts(r, o));
            Assert.Contains("α = 45°", Fx.Texts(r, o, Lang.De));
            var reflex = o.WithParam("reflex", true);
            Assert.Contains("α = 315°", Fx.Texts(r, reflex));
        }

        [Fact]
        public void ARightAngleGetsTheSquareAutomatically()
        {
            var r = Fx.Reg();
            var right = Fx.Item(r, "math.angle.mark.right");
            var marks = Fx.Tagged(r, right, MathTags.Mark).Cast<PathPrim>().ToList();
            Assert.Single(marks);
            Assert.Equal(3, marks[0].Segments.Count); // the square's two outer sides
            Assert.Single(Fx.Tagged(r, right, MathTags.Dot));
            var arc = Fx.Tagged(r, Fx.Item(r, "math.angle.mark"), MathTags.Mark).Cast<PathPrim>().Single();
            Assert.True(arc.Segments.Count > 4);
        }

        [Fact]
        public void SetAngleTurnsTheSecondRay()
        {
            var r = Fx.Reg();
            var doc = SketchDocument.Create();
            var o = Fx.Item(r, "math.angle.mark", new Vec2(50, 50));
            doc = Edits.Add(doc, doc.Pages[0].Id, o);
            foreach (var (input, expected, reflex) in new[] { ("30", 30.0, false), ("120,5", 120.5, false), ("270°", 270.0, true), ("90", 90.0, false) })
            {
                var after = ContextActions.Run(r, doc, new[] { o.Id }, "math.angle.set", input, Lang.De).Get(o.Id)!;
                Assert.Equal(reflex, after.Flag("reflex", false));
                Assert.Equal(expected, AngleMarkType.Measure(after, after.Points!).Degrees, 6);
                Assert.Equal(o.Points![2].DistanceTo(o.Points[1]), after.Points![2].DistanceTo(after.Points[1]), 9);
            }
        }
    }

    public class FigureTests
    {
        private static Vec2 P(Registry r, SketchObject o, string id) => Fx.Anchor(r, o, id).P;

        [Fact]
        public void TheRightTriangleStaysRightAngledUnderFreeScaling()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "math.shape.triangle.right");
            foreach (var size in new[] { o.Size, new Size2(90, 30), new Size2(20, 60) })
            {
                var x = o with { Size = size };
                Vec2 A = P(r, x, "A"), B = P(r, x, "B"), C = P(r, x, "C");
                Assert.Equal(0, (A - C).Dot(B - C), 6);
            }
        }

        [Fact]
        public void TheIsoscelesTriangleKeepsEqualLegs()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "math.shape.triangle").WithParam("kind", "isosceles") with { Size = new Size2(80, 30) };
            Assert.Equal(P(r, o, "C").DistanceTo(P(r, o, "A")), P(r, o, "C").DistanceTo(P(r, o, "B")), 6);
        }

        [Fact]
        public void TheEquilateralTriangleStaysEquilateral()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "math.shape.triangle").WithParam("kind", "equilateral") with { Size = new Size2(90, 30) };
            Vec2 A = P(r, o, "A"), B = P(r, o, "B"), C = P(r, o, "C");
            Assert.Equal(A.DistanceTo(B), B.DistanceTo(C), 6);
            Assert.Equal(A.DistanceTo(B), C.DistanceTo(A), 6);
        }

        private static double DistToLine(Vec2 p, Vec2 a, Vec2 b) => Geo.ProjectOnLine(p, a, b).DistanceTo(p);

        [Fact]
        public void TheIncircleTouchesAllThreeSides()
        {
            var r = Fx.Reg();
            foreach (var size in new[] { new Size2(50, 42), new Size2(100, 30) })
            {
                var o = Fx.Item(r, "math.shape.triangle.incircle") with { Size = size };
                Vec2 A = P(r, o, "A"), B = P(r, o, "B"), C = P(r, o, "C");
                var circle = Fx.Tagged(r, o, MathTags.Incircle).Cast<EllipsePrim>().Single();
                Assert.True(circle.Center.DistanceTo(P(r, o, "centre")) < 1e-9);
                foreach (var (p, q) in new[] { (A, B), (B, C), (C, A) }) Assert.Equal(circle.Rx, DistToLine(circle.Center, p, q), 6);
                Assert.Equal(3, Fx.Tagged(r, o, MathTags.Construction).Count());
            }
        }

        [Fact]
        public void TheCircumcirclePassesThroughTheVerticesAndFitsTheBox()
        {
            var r = Fx.Reg();
            foreach (var kind in new[] { "general", "right", "isosceles", "equilateral" })
            {
                var o = Fx.Item(r, "math.shape.triangle.circumcircle").WithParam("kind", kind);
                var circle = Fx.Tagged(r, o, MathTags.Circumcircle).Cast<EllipsePrim>().Single();
                foreach (var v in new[] { "A", "B", "C" }) Assert.Equal(circle.Rx, circle.Center.DistanceTo(P(r, o, v)), 6);
                Fx.AssertInsideBox(Fx.Render(r, o), o.Size, kind);
            }
        }

        [Fact]
        public void CentresAreNamedPerLanguage()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "math.shape.triangle");
            Assert.Contains("U", Fx.Texts(r, o.WithParam("construction", "circumcircle"), Lang.De));
            Assert.Contains("O", Fx.Texts(r, o.WithParam("construction", "circumcircle"), Lang.En));
            Assert.Contains("S", Fx.Texts(r, o.WithParam("construction", "medians"), Lang.De));
            Assert.Contains("G", Fx.Texts(r, o.WithParam("construction", "medians"), Lang.En));
            var med = o.WithParam("construction", "medians");
            Vec2 A = P(r, med, "A"), B = P(r, med, "B"), C = P(r, med, "C");
            Assert.True(P(r, med, "centre").DistanceTo((A + B + C) / 3) < 1e-9);
        }

        [Fact]
        public void TheAltitudesMeetInTheOrthocentre()
        {
            var r = Fx.Reg();
            foreach (var size in new[] { new Size2(50, 40), new Size2(100, 20) }) // the flat one is obtuse
            {
                var o = Fx.Item(r, "math.shape.triangle").WithParam("construction", "altitudes") with { Size = size };
                Vec2 A = P(r, o, "A"), B = P(r, o, "B"), C = P(r, o, "C"), H = P(r, o, "centre");
                Assert.Equal(0, (H - A).Dot(C - B), 6);
                Assert.Equal(0, (H - B).Dot(A - C), 6);
                Fx.AssertInsideBox(Fx.Render(r, o), o.Size, "altitudes " + size);
            }
        }

        [Fact]
        public void TheRegularPolygonStaysRegularUnderFreeScaling()
        {
            var r = Fx.Reg();
            foreach (var n in new[] { 3, 5, 6, 12 })
            {
                var o = Fx.Item(r, "math.shape.polygon").WithParam("sides", n) with { Size = new Size2(80, 30) };
                var v = Enumerable.Range(0, n).Select(i => P(r, o, "v" + i)).ToList();
                var c = P(r, o, "centre");
                for (var i = 0; i < n; i++)
                {
                    Assert.Equal(v[0].DistanceTo(c), v[i].DistanceTo(c), 6);
                    Assert.Equal(v[0].DistanceTo(v[1]), v[i].DistanceTo(v[(i + 1) % n]), 6);
                }
                Assert.Equal(v[0].Y, v[1].Y, 9); // flat bottom edge
            }
        }

        [Fact]
        public void TheUnitCircleShowsSineAndCosineOfTheAngle()
        {
            var r = Fx.Reg();
            foreach (var deg in new[] { 40.0, 130.0, 225.0, -60.0 })
            {
                var o = Fx.Item(r, "math.circle.unit").WithParam("angle", deg).WithParam("tan", true).WithParam("values", true);
                var circle = Fx.Tagged(r, o, MathTags.Outline).Cast<EllipsePrim>().Single();
                var R = circle.Rx;
                var cos = Fx.Tagged(r, o, MathTags.Cosine).Cast<LinePrim>().Single();
                var sin = Fx.Tagged(r, o, MathTags.Sine).Cast<LinePrim>().Single();
                var rad = Geo.DegToRad(deg);
                Assert.Equal(Math.Cos(rad) * R, cos.B.X - cos.A.X, 6);
                Assert.Equal(-Math.Sin(rad) * R, sin.B.Y - sin.A.Y, 6); // y down on the page
                Assert.True(P(r, o, "P").DistanceTo(sin.B) < 1e-9);
                Fx.AssertInsideBox(Fx.Render(r, o), o.Size, "unit circle " + deg);
            }
            Assert.Contains("cos α = 0,77", Fx.Texts(r, Fx.Item(r, "math.circle.unit").WithParam("values", true), Lang.De));
        }

        [Fact]
        public void ThalesGivesARightAngleAtCWhereverCIs()
        {
            var r = Fx.Reg();
            foreach (var deg in new[] { 20.0, 90.0, 115.0, 170.0 })
            {
                var o = Fx.Item(r, "math.circle.thales").WithParam("angle", deg) with { Size = new Size2(90, 30) };
                Vec2 A = P(r, o, "A"), B = P(r, o, "B"), C = P(r, o, "C");
                Assert.Equal(0, (A - C).Dot(B - C), 6);
            }
        }

        [Fact]
        public void TheTangentIsPerpendicularToTheRadius()
        {
            var r = Fx.Reg();
            foreach (var deg in new[] { 0.0, 60.0, 200.0 })
            {
                var o = Fx.Item(r, "math.circle.tangent").WithParam("angle", deg);
                var t = Fx.Tagged(r, o, MathTags.Main).Cast<LinePrim>().Single();
                Vec2 M = P(r, o, "centre"), T = P(r, o, "contact");
                Assert.Equal(0, (t.B - t.A).Dot(T - M), 6);
                Assert.Equal(0, DistToLine(T, t.A, t.B), 6);
            }
        }

        [Fact]
        public void TheCircleStaysRoundUnderFreeScaling()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "math.circle.basic") with { Size = new Size2(80, 30) };
            var c = Fx.Tagged(r, o, MathTags.Outline).Cast<EllipsePrim>().Single();
            Assert.Equal(c.Rx, c.Ry, 9);
            Assert.Contains("M", Fx.Texts(r, o, Lang.De));
            Assert.Contains("r", Fx.Texts(r, o));
            Assert.Contains("d", Fx.Texts(r, o.WithParam("show", "both")));
        }
    }

    public class ConstructionTests
    {
        private static Vec2 P(Registry r, SketchObject o, string id) => Fx.Anchor(r, o, id).P;

        [Fact]
        public void ThePerpendicularBisectorRunsThroughTheMidpointAndTheArcIntersections()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "math.construct.perpendicular") with { Size = new Size2(90, 40) };
            Vec2 A = P(r, o, "A"), B = P(r, o, "B"), M = P(r, o, "M"), S1 = P(r, o, "S1"), S2 = P(r, o, "S2");
            Assert.True(M.DistanceTo(Vec2.Lerp(A, B, 0.5)) < 1e-9);
            Assert.Equal(S1.DistanceTo(A), S1.DistanceTo(B), 9);
            Assert.Equal(S2.DistanceTo(A), S2.DistanceTo(B), 9);
            Assert.Equal(0, (S1 - S2).Dot(B - A), 9);
            Assert.Equal(4, Fx.Tagged(r, o, MathTags.Construction).Count(p => p is PathPrim));
        }

        [Fact]
        public void TheAngleBisectorHalvesTheAngle()
        {
            var r = Fx.Reg();
            foreach (var deg in new[] { 10.0, 70.0, 150.0 })
            {
                var o = Fx.Item(r, "math.construct.anglebisector").WithParam("angle", deg);
                Vec2 S = P(r, o, "vertex"), P1 = P(r, o, "P1"), P2 = P(r, o, "P2"), Q = P(r, o, "Q");
                Assert.Equal(S.DistanceTo(P1), S.DistanceTo(P2), 9);
                Assert.Equal(Q.DistanceTo(P1), Q.DistanceTo(P2), 9);
                var a1 = Math.Atan2(-(P1.Y - S.Y), P1.X - S.X);
                var a2 = Math.Atan2(-(P2.Y - S.Y), P2.X - S.X);
                var aq = Math.Atan2(-(Q.Y - S.Y), Q.X - S.X);
                Assert.Equal(Geo.RadToDeg(a2 - a1), deg, 6);
                Assert.Equal(Geo.RadToDeg(aq - a1), deg / 2, 6);
            }
        }

        [Fact]
        public void ParallelLinesMarkTheChosenAnglePair()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "math.lines.parallel");
            int Arcs(string pair) => Fx.Tagged(r, o.WithParam("pair", pair), MathTags.Mark).Count(p => p is PathPrim pa && pa.Segments.Count > 4);
            Assert.Equal(2, Arcs("corresponding"));
            Assert.Equal(2, Arcs("alternate"));
            Assert.Equal(3, Arcs("cointerior")); // β with a double arc
            Assert.Equal(12, Arcs("all"));
            Assert.Equal(0, Arcs("none"));
            var values = Fx.Texts(r, o.WithParam("pair", "cointerior").WithParam("values", true)).ToList();
            Assert.Contains("60°", values);
            Assert.Contains("120°", values);
            // The transversal keeps its angle under free scaling.
            var wide = o with { Size = new Size2(150, 40) };
            Vec2 s1 = P(r, wide, "s1"), s2 = P(r, wide, "s2");
            Assert.Equal(60, Geo.RadToDeg(Math.Atan2(-(s1.Y - s2.Y), s1.X - s2.X)), 6);
        }
    }
}
