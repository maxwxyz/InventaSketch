using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Geometry.Tests
{
    /// <summary>Solids in oblique view, circle sector/segment, Pythagoras, Venn diagram and probability tree.</summary>
    public class SolidTests
    {
        private static List<LinePrim> Lines(Registry r, SketchObject o, string tag) => Fx.Tagged(r, o, tag).OfType<LinePrim>().ToList();

        [Theory]
        [InlineData("math.solid.cube")]
        [InlineData("math.solid.cylinder")]
        [InlineData("math.solid.cone")]
        [InlineData("math.solid.sphere")]
        [InlineData("math.solid.pyramid")]
        public void HiddenEdgesAreDashedAndCanBeSwitchedOff(string id)
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, id);
            var hidden = Fx.Tagged(r, o, "hidden").ToList();
            Assert.NotEmpty(hidden);
            Assert.All(hidden, p => Assert.Equal(LineRole.Hidden, Style(p).Role));
            Assert.Empty(Fx.Tagged(r, o.WithParam("hidden", false), "hidden"));
            Assert.NotEmpty(Fx.Tagged(r, o, id == "math.solid.sphere" ? MathTags.Outline : "edge"));
            foreach (var proj in new[] { "cabinet", "cavalier" })
                foreach (var size in new[] { o.Size, new Size2(o.Size.W * 1.7, o.Size.H * 0.6), new Size2(o.Size.W * 0.5, o.Size.H * 1.5) })
                {
                    var s = o.WithParam("projection", proj) with { Size = size };
                    Fx.AssertInsideBox(Fx.Render(r, s), size, id + " " + proj);
                }
        }

        private static Style Style(Primitive p) => p switch { LinePrim l => l.Style, PathPrim pa => pa.Style, _ => throw new InvalidOperationException() };

        [Fact]
        public void CubeHasNineVisibleAndThreeHiddenEdges()
        {
            var r = Fx.Reg();
            var cube = Fx.Item(r, "math.solid.cube");
            Assert.Equal(9, Lines(r, cube, "edge").Count);
            Assert.Equal(3, Lines(r, cube, "hidden").Count);
            // The hidden edges meet in the hidden back-bottom-left corner.
            var hidden = Lines(r, cube, "hidden");
            var corner = hidden.SelectMany(l => new[] { l.A, l.B }).GroupBy(p => (Math.Round(p.X, 6), Math.Round(p.Y, 6))).Single(g => g.Count() == 3).First();
            Assert.Equal(8, r.TypeOf(cube).Anchors(cube, Fx.Ctx()).Count(a => a.Role == AnchorRole.Port));
            Assert.Contains(r.TypeOf(cube).Anchors(cube, Fx.Ctx()), a => a.Role == AnchorRole.Port && a.P.DistanceTo(corner) < 1e-6);
        }

        [Theory]
        [InlineData("cabinet", 0.5)]
        [InlineData("cavalier", 1.0)]
        public void DepthIsShortenedByTheProjectionFactorAt45Degrees(string projection, double q)
        {
            var r = Fx.Reg();
            var cube = Fx.Item(r, "math.solid.cube").WithParam("projection", projection);
            var all = Lines(r, cube, "edge").Concat(Lines(r, cube, "hidden")).ToList();
            var width = all.Where(l => Math.Abs(l.A.Y - l.B.Y) < 1e-9).Max(l => l.A.DistanceTo(l.B));
            var receding = all.Where(l => Math.Abs(l.A.Y - l.B.Y) > 1e-9 && Math.Abs(l.A.X - l.B.X) > 1e-9).ToList();
            Assert.Equal(4, receding.Count);
            Assert.All(receding, l =>
            {
                Assert.Equal(q * width, l.A.DistanceTo(l.B), 6);
                Assert.Equal(Math.Abs(l.B.X - l.A.X), Math.Abs(l.B.Y - l.A.Y), 6);
            });
            // A square box gives a cube: front face height equals its width.
            var height = all.Where(l => Math.Abs(l.A.X - l.B.X) < 1e-9).Max(l => l.A.DistanceTo(l.B));
            Assert.Equal(width, height, 6);
            Assert.DoesNotContain(Fx.Texts(r, cube), t => t == "h");
            Assert.Contains("h", Fx.Texts(r, cube with { Size = new Size2(60, 30) }));
        }

        [Fact]
        public void PyramidShowsFiveVisibleAndThreeHiddenEdgesAndItsApex()
        {
            var r = Fx.Reg();
            var p = Fx.Item(r, "math.solid.pyramid");
            Assert.Equal(5, Lines(r, p, "edge").Count);
            Assert.Equal(3, Lines(r, p, "hidden").Count);
            var apex = r.TypeOf(p).Anchors(p, Fx.Ctx()).Single(a => a.Role == AnchorRole.Apex).P;
            var top = Prim.Bounds(Fx.Tagged(r, p, "edge").ToList())!.Value.Y;
            Assert.Equal(top, apex.Y, 6);
            Assert.Contains("a", Fx.Texts(r, p));
            Assert.Contains("h", Fx.Texts(r, p));
        }

        [Fact]
        public void CylinderHasAHiddenBackArcAndVerticalSilhouettes()
        {
            var r = Fx.Reg();
            var c = Fx.Item(r, "math.solid.cylinder");
            var silhouettes = Lines(r, c, "edge");
            Assert.Equal(2, silhouettes.Count);
            Assert.All(silhouettes, l => Assert.Equal(l.A.X, l.B.X, 6));
            Assert.Single(Fx.Tagged(r, c, "hidden").OfType<PathPrim>());
            // Silhouettes touch the widest points of the base: the drawing is exactly as wide as they are apart (plus nothing).
            var edges = Fx.Tagged(r, c, "edge").ToList();
            var b = Prim.Bounds(edges)!.Value;
            Assert.Equal(b.W, Math.Abs(silhouettes[0].A.X - silhouettes[1].A.X), 3);
            Assert.Contains("r", Fx.Texts(r, c));
        }

        [Fact]
        public void ConeSidesAreTangentFromTheApex()
        {
            var r = Fx.Reg();
            var c = Fx.Item(r, "math.solid.cone");
            var sides = Lines(r, c, "edge");
            Assert.Equal(2, sides.Count);
            var apex = r.TypeOf(c).Anchors(c, Fx.Ctx()).Single(a => a.Role == AnchorRole.Apex).P;
            Assert.All(sides, l => Assert.True(l.B.DistanceTo(apex) < 1e-6));
            // Tangency: every point of the base lies on one side of each side line.
            var basePts = Fx.Tagged(r, c, "edge").OfType<PathPrim>().Concat(Fx.Tagged(r, c, "hidden").OfType<PathPrim>()).SelectMany(Prim.Points).ToList();
            foreach (var l in sides)
            {
                var d = l.B - l.A;
                var signs = basePts.Select(p => d.Cross(p - l.A)).Where(v => Math.Abs(v) > 1e-3).Select(Math.Sign).Distinct().ToList();
                Assert.Single(signs);
            }
        }

        [Fact]
        public void SphereStaysRoundWithAHiddenBackEquator()
        {
            var r = Fx.Reg();
            var s = Fx.Item(r, "math.solid.sphere") with { Size = new Size2(60, 40) };
            var outline = Prim.Bounds(Fx.Tagged(r, s, MathTags.Outline).ToList())!.Value;
            Assert.Equal(outline.W, outline.H, 3);
            Assert.Single(Fx.Tagged(r, s, "equator"));
            var cav = Prim.Bounds(Fx.Tagged(r, s.WithParam("projection", "cavalier"), "equator").ToList())!.Value;
            var cab = Prim.Bounds(Fx.Tagged(r, s, "equator").ToList())!.Value;
            Assert.True(cav.H > cab.H * 1.9, "cavalier equator is twice as deep");
        }
    }

    public class SectorTests
    {
        [Fact]
        public void SectorShowsTheAngleValueOrAlpha()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "math.arc.sector");
            Assert.Contains("70°", Fx.Texts(r, o));
            Assert.Contains("70°", Fx.Texts(r, o, Lang.De));
            Assert.Contains("α", Fx.Texts(r, o.WithParam("angleLabel", "alpha")));
            Assert.DoesNotContain(Fx.Texts(r, o.WithParam("angleLabel", "none")), t => t.EndsWith("°", StringComparison.Ordinal));
            Assert.Contains("62,5°", Fx.Texts(r, o.WithParam("angle", 62.5), Lang.De));
        }

        [Fact]
        public void SectorAreaStartsAtTheCentreAndTheSegmentDoesNot()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "math.arc.sector");
            var sector = Fx.Tagged(r, o, MathTags.Outline).OfType<PathPrim>().Single();
            Assert.Contains(Prim.Points(sector), p => p.Length < 1e-9);
            var seg = Fx.Item(r, "math.arc.sector.segment");
            var segment = Fx.Tagged(r, seg, MathTags.Outline).OfType<PathPrim>().Single();
            Assert.DoesNotContain(Prim.Points(segment), p => p.Length < 1e-6);
            Assert.Equal(2, Fx.Tagged(r, seg, MathTags.Construction).Count());
            // The end points of the arc lie on the circle at 20° and 20° + 70° (counter-clockwise).
            var p2 = Fx.Anchor(r, o, "ray2").P;
            Assert.Equal(90, Geo.RadToDeg(Math.Atan2(-p2.Y, p2.X)), 6);
        }

        [Fact]
        public void DraggingTheSecondRaySetsTheCentralAngle()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "math.arc.sector", new Vec2(100, 100));
            Assert.Contains(r.DragHandles(o, Fx.Ctx()), h => h.Key == "start");
            var target = new Vec2(100, 100) + new Vec2(Math.Cos(Geo.DegToRad(140)), -Math.Sin(Geo.DegToRad(140))) * 10;
            var moved = r.MoveHandle(o, "angle", target, Fx.Ctx());
            Assert.Equal(120, moved.Num("angle", 0), 6);
        }
    }

    public class PythagorasTests
    {
        private static double Area(IReadOnlyList<Vec2> p) => Math.Abs(Geo.PolygonArea(p));

        [Fact]
        public void SquaresOnTheLegsAddUpToTheSquareOnTheHypotenuse()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "math.shape.pythagoras");
            var squares = Fx.Tagged(r, o, "square").OfType<PathPrim>().Select(p => Prim.Points(p).Distinct().ToList()).ToList();
            Assert.Equal(3, squares.Count);
            var areas = squares.Select(Area).OrderBy(a => a).ToList();
            Assert.Equal(areas[2], areas[0] + areas[1], 6);
            Assert.Equal(9.0 / 16, areas[0] / areas[1], 6);
            var a = Fx.Anchor(r, o, "A").P; var b = Fx.Anchor(r, o, "B").P; var c = Fx.Anchor(r, o, "C").P;
            Assert.Equal(0, (a - c).Dot(b - c), 6);
            Assert.Contains("c²", Fx.Texts(r, o));
            Assert.Contains("25", Fx.Texts(r, o.WithParam("areas", "values")));
        }

        [Fact]
        public void UnitSquaresOnlyForWholeNumberSides()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "math.shape.pythagoras").WithParam("grid", true);
            Assert.Equal(2 * 2 + 3 * 2 + 4 * 2, Fx.Tagged(r, o, MathTags.Grid).Count());
            // 1-1-√2: only the legs are whole numbers, but a side of 1 has no inner lines.
            Assert.Empty(Fx.Tagged(r, o.WithParam("a", 1.0).WithParam("b", 1.0), MathTags.Grid));
            Assert.Equal(2 * 2 + 2 * 2, Fx.Tagged(r, o.WithParam("a", 3.0).WithParam("b", 3.0), MathTags.Grid).Count());
        }
    }

    public class VennTests
    {
        [Fact]
        public void TwoAndThreeOverlappingSetsWithNames()
        {
            var r = Fx.Reg();
            var two = Fx.Item(r, "math.venn.diagram");
            var circles = Fx.Tagged(r, two, "set").OfType<EllipsePrim>().ToList();
            Assert.Equal(2, circles.Count);
            Assert.Equal(circles[0].Rx, circles[0].Center.DistanceTo(circles[1].Center), 6);
            Assert.Contains("A", Fx.Texts(r, two));
            Assert.Contains("U", Fx.Texts(r, two));
            Assert.Contains("G", Fx.Texts(r, two, Lang.De));
            var three = Fx.Item(r, "math.venn.diagram.three");
            Assert.Equal(3, Fx.Tagged(r, three, "set").Count());
            Assert.Contains("C", Fx.Texts(r, three));
            Assert.Equal(3, Fx.Tagged(r, three, "fill").Count());
            Assert.Empty(Fx.Tagged(r, three.WithParam("fill", false), "fill"));
            Assert.Empty(Fx.Tagged(r, three.WithParam("universe", false), "universe"));
            Assert.Contains("Ω", Fx.Texts(r, two.WithParam("universeLabel", "Ω")));
        }

        [Fact]
        public void SetNamesInputRenamesTheSets()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "math.venn.diagram");
            var a = r.TypeOf(o).Actions(o, Lang.En).Single(x => x.Id == "math.venn.names");
            Assert.Equal("A; B", a.Input!.Value);
            var doc = SketchDocument.Create();
            doc = Edits.Add(doc, doc.Pages[0].Id, o);
            var after = a.Run(doc, o, new RenderContext(doc), "Even; Prime").Get(o.Id)!;
            Assert.Equal("Even", after.Str("labelA", ""));
            Assert.Contains("Prime", Fx.Texts(r, after));
        }
    }

    public class ProbabilityTreeTests
    {
        [Fact]
        public void DefaultTreeShowsComplementsAndPathProducts()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "math.tree.probability");
            Assert.Equal(6, Fx.Tagged(r, o, "branch").Count());
            var en = Fx.Texts(r, o).ToList();
            foreach (var t in new[] { "0.3", "0.7", "0.6", "0.4", "0.2", "0.8", "= 0.18", "= 0.12", "= 0.14", "= 0.56", "A", "Ā", "B" }) Assert.Contains(t, en);
            var de = Fx.Texts(r, o, Lang.De).ToList();
            Assert.Contains("0,7", de);
            Assert.Contains("= 0,56", de);
            Assert.DoesNotContain(Fx.Texts(r, o.WithParam("products", false)), t => t.StartsWith("=", StringComparison.Ordinal));
        }

        [Fact]
        public void FractionsStayExactAndPercentagesArePercentages()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "math.tree.probability").WithParam("p1", "1/3").WithParam("p2", "1/4");
            var texts = Fx.Texts(r, o).ToList();
            Assert.Contains("2/3", texts);
            Assert.Contains("3/4", texts);
            Assert.Contains("= 1/12", texts);
            Assert.Contains("= 1/2", texts); // 2/3 · 3/4
            var dice = Fx.Item(r, "math.tree.probability.dice");
            Assert.Equal(3, Fx.Tagged(r, dice, "branch").Count());
            Assert.Contains("= 1/6", Fx.Texts(r, dice));
            var pct = Fx.Item(r, "math.tree.probability").WithParam("p1", "30 %").WithParam("levels", 1);
            Assert.Contains("70 %", Fx.Texts(r, pct));
        }

        [Fact]
        public void ThreeStagesAndThreeBranchesLayOutEveryNodeOverItsChildren()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "math.tree.probability").WithParam("levels", 3).WithParam("branches", 3).WithParam("p1", "").WithParam("p2", "");
            Assert.Equal(3 + 9 + 27, Fx.Tagged(r, o, "branch").Count());
            Assert.Contains("1/3", Fx.Texts(r, o));
            Assert.Contains("= 1/27", Fx.Texts(r, o));
            var anchors = r.TypeOf(o).Anchors(o, Fx.Ctx()).Where(a => a.Role == AnchorRole.Mid).ToDictionary(a => a.Id!, a => a.P);
            for (var i = 0; i < 3; i++)
            {
                var parent = anchors["n1." + i];
                var kids = Enumerable.Range(0, 3).Select(j => anchors["n2." + (i * 3 + j)]).ToList();
                Assert.Equal(kids.Average(k => k.Y), parent.Y, 6);
                Assert.All(kids, k => Assert.True(k.X > parent.X));
            }
            Fx.AssertInsideBox(Fx.Render(r, o), o.Size, "3x3 tree");
        }

        [Theory]
        [InlineData("0.3", 0.3)]
        [InlineData("0,3", 0.3)]
        [InlineData("30 %", 0.3)]
        [InlineData("1/6", 1.0 / 6)]
        public void ProbabilitiesParse(string text, double value) => Assert.Equal(value, Prob.Parse(text)!.Value.Value, 9);

        [Theory]
        [InlineData("")]
        [InlineData("Q")]
        [InlineData("1.5")]
        [InlineData("3/2")]
        public void InvalidProbabilitiesAreIgnored(string text) => Assert.Null(Prob.Parse(text));

        [Fact]
        public void ProbabilitiesThatExceedOneShowAQuestionMark()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "math.tree.probability").WithParam("levels", 1).WithParam("branches", 3).WithParam("p1", "0.6; 0.6");
            Assert.Contains("?", Fx.Texts(r, o));
        }
    }

    public class NewFigurePersistenceTests
    {
        [Fact]
        public void NewFiguresSurviveTheJsonRoundTrip()
        {
            var r = Fx.Reg();
            var doc = SketchDocument.Create();
            var objs = new[] { "math.solid.cube", "math.arc.sector.segment", "math.shape.pythagoras", "math.venn.diagram.three", "math.tree.probability.dice" }
                .Select(id => Fx.Item(r, id)).ToArray();
            doc = Edits.Add(doc, doc.Pages[0].Id, objs);
            var back = DocumentJson.Deserialize(DocumentJson.Serialize(doc));
            Assert.Equal(SvgRenderer.RenderPage(r, doc, doc.Pages[0]), SvgRenderer.RenderPage(r, back, back.Pages[0]));
        }
    }
}
