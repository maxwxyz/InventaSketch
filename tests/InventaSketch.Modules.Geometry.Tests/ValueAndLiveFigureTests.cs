using System;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Geometry.Tests
{
    /// <summary>
    /// Round 7: triangle values (law of sines and cosines in both directions), the triangle true to its values, live
    /// constructions on points (triangle with circles, bisectors, tangents from a point, polygon area and centroid) and
    /// conic sections.
    /// </summary>
    public class ValueAndLiveFigureTests
    {
        private static double Rad(double deg) => deg * Math.PI / 180;

        private static ValueTable Solve(Registry r, SketchObject o)
        {
            var doc = SketchDocument.Create();
            doc = Edits.Add(doc, doc.Pages[0].Id, o);
            return ValueSolver.Solve(doc, r, null, new ValueSolveOptions { BudgetMs = 10000 });
        }

        private static SketchObject Triangle(Registry r, params (string Key, string Value)[] values)
        {
            var o = Fx.Item(r, "math.shape.triangle", new Vec2(50, 50));
            foreach (var (k, v) in values) o = o.WithParam(k, v);
            return o;
        }

        private static double V(ValueTable t, SketchObject o, string key) => t.Get(o.Id, key)!.Value!.Value;

        // ------------------------------------------------------------------ triangle values

        [Fact]
        public void ThreeSidesGiveTheAnglesAreaAndPerimeter()
        {
            var r = Fx.Reg();
            var o = Triangle(r, ("a", "3 cm"), ("b", "4 cm"), ("c", "5 cm"));
            var t = Solve(r, o);
            Assert.Equal(Rad(90), V(t, o, "gamma"), 9);
            Assert.Equal(Math.Asin(0.6), V(t, o, "alpha"), 9);
            Assert.Equal(6e-4, V(t, o, "A"), 12);  // 6 cm²
            Assert.Equal(0.12, V(t, o, "u"), 12);  // 12 cm
            Assert.Empty(t.Conflicts);
        }

        [Fact]
        public void TwoSidesAndTheIncludedAngleGiveTheThirdSide()
        {
            var r = Fx.Reg();
            var o = Triangle(r, ("a", "5"), ("b", "4"), ("gamma", "60°"));
            var t = Solve(r, o);
            var c = Math.Sqrt(25 + 16 - 2 * 5 * 4 * 0.5) / 100;
            Assert.Equal(c, V(t, o, "c"), 12);
            Assert.Equal("math.triangle.cosine.c", t.Get(o.Id, "c")!.Via!.Relation.Id);
            Assert.Equal(Math.PI, V(t, o, "alpha") + V(t, o, "beta") + V(t, o, "gamma"), 9);
            Assert.Equal(0.5 * 0.05 * 0.04 * Math.Sin(Rad(60)), V(t, o, "A"), 12);
            Assert.Empty(t.Conflicts);
        }

        [Fact]
        public void OneSideAndTwoAnglesGiveTheOtherSidesByTheLawOfSines()
        {
            var r = Fx.Reg();
            var o = Triangle(r, ("c", "7 cm"), ("alpha", "50°"), ("beta", "60°"));
            var t = Solve(r, o);
            Assert.Equal(Rad(70), V(t, o, "gamma"), 9);
            Assert.Equal(0.07 * Math.Sin(Rad(50)) / Math.Sin(Rad(70)), V(t, o, "a"), 12);
            Assert.Equal(0.07 * Math.Sin(Rad(60)) / Math.Sin(Rad(70)), V(t, o, "b"), 12);
            Assert.Empty(t.Conflicts);
        }

        [Fact]
        public void TwoSidesAndAnOppositeAngleTakeTheAcuteSolution()
        {
            var r = Fx.Reg();
            var o = Triangle(r, ("a", "7 cm"), ("b", "5 cm"), ("alpha", "80°"));
            var t = Solve(r, o);
            Assert.Equal(Math.Asin(5 * Math.Sin(Rad(80)) / 7), V(t, o, "beta"), 9);
            Assert.True(t.Get(o.Id, "c")!.Value > 0);
            Assert.Empty(t.Conflicts);
        }

        [Fact]
        public void ContradictingValuesAreReportedAsAConflict()
        {
            var r = Fx.Reg();
            var o = Triangle(r, ("a", "3 cm"), ("b", "4 cm"), ("c", "5 cm"), ("gamma", "80°"));
            Assert.NotEmpty(Solve(r, o).Conflicts);
        }

        [Fact]
        public void TheTriangleTrueToItsValuesHasTheirShapeAndShowsThem()
        {
            var r = Fx.Reg();
            var o = Triangle(r, ("a", "3 cm"), ("b", "4 cm"), ("c", "5 cm")).WithParam("kind", "values");
            Vec2 P(string id) => Fx.Anchor(r, o, id).P;
            double a = P("B").DistanceTo(P("C")), b = P("C").DistanceTo(P("A")), c = P("A").DistanceTo(P("B"));
            Assert.Equal(3.0 / 5, a / c, 6);
            Assert.Equal(4.0 / 5, b / c, 6);
            Assert.Equal(0, (P("A") - P("C")).Dot(P("B") - P("C")), 6); // γ = 90°
            var texts = Fx.Texts(r, o).ToList();
            Assert.Contains("a = 3 cm", texts);
            Assert.Contains("c = 5 cm", texts);
            // Angles alone give the shape (equilateral), the free scaling keeps it.
            var eq = Triangle(r, ("alpha", "60°"), ("beta", "60°")).WithParam("kind", "values") with { Size = new Size2(90, 30) };
            Vec2 Q(string id) => Fx.Anchor(r, eq, id).P;
            Assert.Equal(Q("A").DistanceTo(Q("B")), Q("B").DistanceTo(Q("C")), 6);
            Assert.Contains("α = 60°", Fx.Texts(r, eq));
            // Values that give no triangle fall back to the general shape.
            var bad = Triangle(r, ("a", "1"), ("b", "1"), ("c", "5")).WithParam("kind", "values");
            Assert.Null(TriValues.Shape(bad));
            Assert.NotEmpty(Fx.Render(r, bad));
        }

        [Theory]
        [InlineData("5", "4", "", "", "", "60", 21.0)]       // SAS → c² = 21
        [InlineData("", "", "7", "50", "60", "", -1)]        // ASA
        [InlineData("7", "5", "", "80", "", "", -1)]         // SSA
        public void TheShapeSolverCoversTheUsualCases(string a, string b, string c, string alpha, string beta, string gamma, double c2)
        {
            var r = Fx.Reg();
            var o = Triangle(r, ("a", a), ("b", b), ("c", c), ("alpha", alpha), ("beta", beta), ("gamma", gamma));
            var s = TriValues.Shape(o);
            Assert.NotNull(s);
            if (c2 > 0) Assert.Equal(c2, s!.Value.C * s.Value.C * 1e4, 6);
        }

        // ------------------------------------------------------------------ live figures on points

        [Fact]
        public void TheTriangleThroughPointsDrawsItsIncircleLiveAndMeasuresItsAngles()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "math.free.triangle.incircle") with { Points = new[] { new Vec2(0, 40), new Vec2(60, 40), new Vec2(10, 0) } };
            Vec2 A = new(0, 40), B = new(60, 40), C = new(10, 0);
            var centre = Fx.Anchor(r, o, "centre").P;
            var (ic, ir) = Tri.Incircle(A, B, C);
            Assert.True(centre.DistanceTo(ic) < 1e-9);
            foreach (var (p, q) in new[] { (A, B), (B, C), (C, A) })
                Assert.Equal(ir, Geo.ProjectOnLine(centre, p, q).DistanceTo(centre), 6);
            Assert.Equal(A, Fx.Anchor(r, o, "A").P);
            var deg = FreeTriangleType.Angles(A, B, C);
            Assert.Equal(180, deg.Sum(), 9);
            var withValues = o.WithParam("values", true);
            Assert.Contains("α = " + Mx.Num(deg[0], Lang.En, 1) + "°", Fx.Texts(r, withValues));
            Assert.Contains("α = " + Mx.Num(deg[0], Lang.De, 1) + "°", Fx.Texts(r, withValues, Lang.De));
        }

        [Fact]
        public void ABisectorDockedOnATriangleFollowsIt()
        {
            var r = Fx.Reg();
            var doc = SketchDocument.Create(new Size2(300, 200));
            var tri = Fx.Item(r, "math.shape.triangle", new Vec2(100, 100));
            doc = Edits.Add(doc, doc.Pages[0].Id, tri);
            Vec2 G(SketchDocument d, string id) => r.PageAnchors(d.Get(tri.Id)!, new RenderContext(d)).Single(a => a.Id == id).P;
            var bis = Fx.Item(r, "math.free.bisector") with { Points = new[] { G(doc, "A"), G(doc, "B") } };
            doc = Edits.Add(doc, doc.Pages[0].Id, bis);
            doc = Edits.AttachPoint(doc, bis.Id, 0, tri.Id, G(doc, "A"), "A");
            doc = Edits.AttachPoint(doc, bis.Id, 1, tri.Id, G(doc, "B"), "B");
            var moved = Edits.Update(doc, new[] { tri.Id }, x => x with { Transform = x.Transform with { X = x.Transform.X + 20, Rotation = 30 }, Size = new Size2(70, 50) });
            moved = Attachments.Refresh(r, moved, new RenderContext(moved));
            var m = r.PageAnchors(moved.Get(bis.Id)!, new RenderContext(moved)).Single(a => a.Id == "M").P;
            Assert.True(m.DistanceTo(Vec2.Lerp(G(moved, "A"), G(moved, "B"), 0.5)) < 0.05);
            // The bisector is perpendicular to AB and passes through M.
            var rail = r.PageAnchors(moved.Get(bis.Id)!, new RenderContext(moved)).Single(a => a.Id == "m");
            Assert.Equal(0, (rail.Q - rail.P).Normalized().Dot((G(moved, "B") - G(moved, "A")).Normalized()), 6);
        }

        [Fact]
        public void TheAngleBisectorHalvesTheAngle()
        {
            var r = Fx.Reg();
            Vec2 P1 = new(40, 0), S = new(0, 0), P2 = new(0, -30);
            var o = Fx.Item(r, "math.free.anglebisector") with { Points = new[] { P1, S, P2 } };
            var w = Fx.Anchor(r, o, "w");
            var d = (w.Q - w.P).Normalized();
            Assert.Equal(Math.Cos(Rad(45)), d.Dot(new Vec2(1, 0)), 9);
            Assert.Equal(Math.Cos(Rad(45)), d.Dot(new Vec2(0, -1)), 9);
            Assert.Contains("45°", Fx.Texts(r, o.WithParam("values", true)));
        }

        [Fact]
        public void TangentsFromAPointTouchTheCircleAtRightAngles()
        {
            var r = Fx.Reg();
            Vec2 M = new(0, 0), P = new(25, 0);
            var o = Fx.Item(r, "math.free.tangents") with { Points = new[] { M, new Vec2(0, -10), P } };
            foreach (var id in new[] { "T1", "T2" })
            {
                var T = Fx.Anchor(r, o, id).P;
                Assert.Equal(10, T.DistanceTo(M), 9);
                Assert.Equal(0, (T - M).Dot(P - T), 9);
            }
            Assert.Equal(2, Fx.Tagged(r, o, MathTags.Tangent).Count());
            Assert.Contains("B₁", Fx.Texts(r, o, Lang.De));
            Assert.Contains("T₁", Fx.Texts(r, o));
            // A point inside the circle has no tangents.
            var inside = o with { Points = new[] { M, new Vec2(0, -10), new Vec2(3, 2) } };
            Assert.Empty(Fx.Tagged(r, inside, MathTags.Tangent));
            Assert.Null(FreeTangentsType.Contacts(M, 10, new Vec2(3, 2)));
        }

        [Fact]
        public void ThePolygonMeasuresAreaPerimeterAndCentroid()
        {
            // A 40 x 30 mm rectangle: 12 cm², 14 cm, centroid in the middle.
            var r = Fx.Reg();
            var pts = new[] { new Vec2(0, 0), new Vec2(40, 0), new Vec2(40, 30), new Vec2(0, 30) };
            var o = Fx.Item(r, "math.free.polygon") with { Points = pts };
            Assert.Equal(new Vec2(20, 15), Fx.Anchor(r, o, "centre").P);
            Assert.Contains("A = 12 cm²", Fx.Texts(r, o));
            var t = Solve(r, o);
            Assert.Equal(12e-4, V(t, o, "A"), 12);
            Assert.Equal(0.14, V(t, o, "u"), 12);
            Assert.True(ValueModel.Variable(r.TypeOf(o), "A")!.IsReadOnly);
            // At 1:10 the area is a hundred times larger; an L-shape keeps its centroid inside the hull.
            Assert.Equal(12e-2, V(Solve(r, o.WithParam("scale", 10.0)), o, "A"), 12);
            var l = o with { Points = new[] { new Vec2(0, 0), new Vec2(40, 0), new Vec2(40, 10), new Vec2(10, 10), new Vec2(10, 40), new Vec2(0, 40) } };
            var c = FreePolygonType.Centroid(l.Points!);
            Assert.Equal(c.X, c.Y, 9); // symmetric about the diagonal
            Assert.Equal(9500.0 / 700, c.X, 9); // (400·20 + 300·5)/700
            Assert.True(r.TypeOf(o).InsertablePoints);
        }

        // ------------------------------------------------------------------ conic sections

        [Fact]
        public void TheEllipseHasItsFociAtCSquaredEqualsASquaredMinusBSquared()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "math.conic.section").WithParam("labels", false).WithParam("formula", false);
            var centre = Fx.Anchor(r, o, "centre").P;
            var f1 = Fx.Anchor(r, o, "F1").P; var f2 = Fx.Anchor(r, o, "F2").P;
            var v2 = Fx.Anchor(r, o, "V2").P;
            var e = Prim.Flatten(Fx.Render(r, o)).OfType<EllipsePrim>().Single(x => x.Tag == MathTags.Outline);
            Assert.Equal(Math.Sqrt(e.Rx * e.Rx - e.Ry * e.Ry), f2.DistanceTo(centre), 9);
            Assert.Equal(e.Rx, v2.DistanceTo(centre), 9);
            // |PF₁| + |PF₂| = 2a for the point P.
            var p = Fx.Anchor(r, o, "P").P;
            Assert.Equal(2 * e.Rx, p.DistanceTo(f1) + p.DistanceTo(f2), 6);
            Assert.Contains("|PF₁| + |PF₂| = 2a", Fx.Texts(r, Fx.Item(r, "math.conic.section")));
        }

        [Fact]
        public void TheHyperbolaAndTheParabolaKeepTheirDefiningProperty()
        {
            var r = Fx.Reg();
            var h = Fx.Item(r, "math.conic.section.hyperbola");
            var (a, b, c) = ConicType.Axes("hyperbola", 20, 12);
            Assert.Equal(a * a + b * b, c * c, 9);
            Vec2 P(SketchObject o, string id) => Fx.Anchor(r, o, id).P;
            var v2 = P(h, "V2"); var centre = P(h, "centre");
            var twoA = 2 * v2.DistanceTo(centre);
            Assert.Equal(twoA, Math.Abs(P(h, "P").DistanceTo(P(h, "F1")) - P(h, "P").DistanceTo(P(h, "F2"))), 4);
            var par = Fx.Item(r, "math.conic.section.parabola");
            var pp = P(par, "P"); var f = P(par, "F");
            var dir = Fx.Anchor(r, par, "directrix");
            Assert.Equal(pp.DistanceTo(f), Math.Abs(dir.P.Y - pp.Y), 6);
            Assert.Contains("|PF| = |PL|", Fx.Texts(r, par, Lang.De));
            Assert.Contains("S", Fx.Texts(r, par, Lang.De)); // Scheitelpunkt
        }
    }
}
