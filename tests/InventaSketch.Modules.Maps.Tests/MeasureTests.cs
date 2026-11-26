using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Maps.Tests
{
    /// <summary>
    /// Round 7 measurements: region area and perimeter, the distance measure with bearing, ascent and descent of a route
    /// from the contour lines it crosses, the elevation profile and the scale bar against the document plan scale. A scale
    /// bar 40 mm wide for 1 km gives 25 m per page millimetre (map scale 1:25 000).
    /// </summary>
    public class MeasureTests
    {
        private static ValueTable Solve(Registry r, SketchDocument doc) => ValueSolver.Solve(doc, r, null, new ValueSolveOptions { BudgetMs = 10000 });

        private static IReadOnlyList<CheckFinding> Checks(Registry r, SketchDocument doc) => ValueSolver.Checks(Solve(r, doc), r, new RenderContext(doc).For(r));

        private static double? Val(ValueTable t, SketchObject o, string key) => t.Get(o.Id, key)?.Value;

        private static SketchObject Bar(Registry r) => Fx.Item(r, "map.scalebar", new Vec2(150, 150)) with { Size = new Size2(40, 8) };

        private static SketchObject Contour(Registry r, double x, double elevation)
            => Fx.Line(r, "map.contour", new Vec2(x, -30), new Vec2(x, 30)).WithParam("elevation", elevation).WithParam("smooth", false);

        private static string Texts(Registry r, SketchObject o, SketchDocument doc, Lang lang = Lang.En)
            => string.Join(" | ", r.Render(o, new RenderContext(doc, lang)).OfType<TextPrim>().Select(t => t.Text));

        // ------------------------------------------------------------------ region

        [Fact]
        public void RegionAreaAndPerimeterAtTheMapScale()
        {
            var r = Fx.Reg();
            var region = Fx.Line(r, "map.region", new Vec2(0, 0), new Vec2(40, 0), new Vec2(40, 40), new Vec2(0, 40));
            var t = Solve(r, Fx.Doc(region, Bar(r)).Doc);
            Assert.Equal(1e6, Val(t, region, "A")!.Value, 3);      // 1600 mm² · 625 m²/mm² = 1 km²
            Assert.Equal(4000, Val(t, region, "U")!.Value, 6);     // 160 mm · 25 m/mm
            // Without a scale nothing is known; an entered area wins over the drawing.
            Assert.Null(Val(Solve(r, Fx.Doc(region).Doc), region, "A"));
            var entered = region.WithParam("area", "2 km²");
            var te = Solve(r, Fx.Doc(entered, Bar(r)).Doc);
            Assert.Equal(2e6, Val(te, entered, "A")!.Value, 3);
            Assert.Equal(Provenance.Entered, te.Get(entered.Id, "A")!.Provenance);
            Assert.Empty(te.Conflicts);
            // The library item shows its values.
            Assert.True(ValueLabels.Shows(Fx.Item(r, "map.region.values", Vec2.Zero)));
        }

        // ------------------------------------------------------------------ distance measure

        [Fact]
        public void MeasureShowsTheDistanceAndBearing()
        {
            var r = Fx.Reg();
            var m = Fx.Line(r, "map.measure", new Vec2(0, 0), new Vec2(30, 40));
            var (doc, _) = Fx.Doc(m, Bar(r));
            var t = Solve(r, doc);
            Assert.Equal(1250, Val(t, m, "d")!.Value, 6);
            Assert.Equal(Math.Atan2(30, -40), Val(t, m, "theta")!.Value, 9); // 143.13° from north
            Assert.Contains("1.25 km", Texts(r, m, doc));
            Assert.Contains("1,25 km", Texts(r, m, doc, Lang.De));
            var withBearing = m.WithParam("showBearing", true);
            var (doc2, _) = Fx.Doc(withBearing, Bar(r));
            Assert.Contains("1.25 km · 143°", Texts(r, withBearing, doc2));
            // A north arrow turned by 90° (north to the right) turns the bearing.
            var north = Fx.Item(r, "map.north", new Vec2(160, 120));
            north = north with { Transform = north.Transform with { Rotation = 90 } };
            var t3 = Solve(r, Fx.Doc(m, Bar(r), north).Doc);
            Assert.Equal(Math.Atan2(40, 30), Val(t3, m, "theta")!.Value, 9);
            // Without a scale the label says so and the distance is unknown.
            var (alone, _) = Fx.Doc(m);
            Assert.Contains("no scale", Texts(r, m, alone));
            Assert.Null(Val(Solve(r, alone), m, "d"));
        }

        [Fact]
        public void MeasureReadsAnEnteredDistance()
        {
            var r = Fx.Reg();
            var m = Fx.Line(r, "map.measure", new Vec2(0, 0), new Vec2(30, 40)).WithParam("distance", "800 m");
            var (doc, _) = Fx.Doc(m, Bar(r));
            Assert.Equal(800, Val(Solve(r, doc), m, "d")!.Value, 6);
            Assert.Contains("800 m", Texts(r, m, doc));
        }

        // ------------------------------------------------------------------ elevation from contours

        private static (SketchDocument Doc, SketchObject Route) Hill(Registry r, bool withBar = true, bool showValues = false)
        {
            var route = Fx.Line(r, "map.route", new Vec2(0, 0), new Vec2(100, 0));
            if (showValues) route = route.WithParam(ValueLabels.ShowParam, true);
            var list = new List<SketchObject> { route, Contour(r, 20, 100), Contour(r, 40, 150), Contour(r, 60, 200), Contour(r, 80, 150) };
            if (withBar) list.Add(Bar(r));
            return (Fx.Doc(list.ToArray()).Doc, route);
        }

        [Fact]
        public void AscentAndDescentFromTheContoursCrossed()
        {
            var r = Fx.Reg();
            var (doc, route) = Hill(r);
            var t = Solve(r, doc);
            Assert.Equal(100, Val(t, route, "up")!.Value, 9);
            Assert.Equal(50, Val(t, route, "down")!.Value, 9);
            Assert.Equal(0.1, Val(t, route, "gmax")!.Value, 9);    // 50 m over 20 mm · 25 m/mm = 500 m
            Assert.Empty(t.Conflicts);
            // Without a scale the steepest gradient stays unknown, ascent and descent do not need one.
            var (noBar, route2) = Hill(r, withBar: false);
            var t2 = Solve(r, noBar);
            Assert.Equal(100, Val(t2, route2, "up")!.Value, 9);
            Assert.Null(Val(t2, route2, "gmax"));
            // A route that crosses no contour line has no elevation values.
            var flat = Fx.Line(r, "map.route", new Vec2(0, 50), new Vec2(100, 50));
            Assert.Null(Val(Solve(r, Fx.Doc(flat, Bar(r)).Doc), flat, "up"));
        }

        [Fact]
        public void ContoursRedrawTheRoutesShowingValuesAndTheProfile()
        {
            var r = Fx.Reg();
            var (doc, route) = Hill(r, showValues: true);
            var profile = Fx.Item(r, "map.profile", new Vec2(50, 80));
            doc = Edits.Add(doc, doc.Pages[0].Id, new[] { profile });
            var contour = doc.Objects.Values.First(o => o.Type == "map.contour");
            var ctx = new RenderContext(doc).For(r);
            var deps = r.TypeOf(contour).Dependents(contour, ctx).ToList();
            Assert.Contains(route.Id, deps);
            Assert.Contains(profile.Id, deps);
            Assert.Contains(profile.Id, r.TypeOf(route).Dependents(route, ctx));
            var bar = doc.Objects.Values.First(o => o.Type == "map.scalebar");
            var barDeps = r.TypeOf(bar).Dependents(bar, ctx).ToList();
            Assert.Contains(profile.Id, barDeps);
            Assert.Contains(route.Id, barDeps);
            Assert.True(r.TypeOf(profile).DependsOnNeighbours);
        }

        // ------------------------------------------------------------------ elevation profile

        [Fact]
        public void ProfileDrawsTheCrossingsOfTheRoute()
        {
            var r = Fx.Reg();
            var (doc, _) = Hill(r);
            var profile = Fx.Item(r, "map.profile", new Vec2(50, 80));
            doc = Edits.Add(doc, doc.Pages[0].Id, new[] { profile });
            var prims = r.TypeOf(profile).Render(profile, new RenderContext(doc)).ToList();
            var line = Assert.IsType<PathPrim>(prims.Single(p => p.Tag == "profile"));
            var pts = line.Segments.Where(g => g.Op != SegOp.Z).Select(g => g.P).ToList();
            Assert.Equal(4, pts.Count);
            Assert.Equal(4, prims.Count(p => p.Tag == "point"));
            // Highest point at the third crossing (smallest y), x increasing along the route.
            Assert.True(pts[2].Y < pts[0].Y && pts[2].Y < pts[3].Y);
            Assert.True(pts.Zip(pts.Skip(1), (a, b) => b.X > a.X).All(x => x));
            var texts = string.Join(" | ", prims.OfType<TextPrim>().Select(t => t.Text));
            Assert.Contains("2.5 km", texts);   // 100 mm · 25 m/mm
            Assert.Contains("200", texts);
            Fx.AssertInsideBox(prims, profile.Size, "profile");
            // Without a route it draws a hint, in the host language.
            var empty = Fx.Item(r, "map.profile", Vec2.Zero);
            var (alone, _) = Fx.Doc(empty);
            Assert.Contains("Höhenlinien", Texts(r, empty, alone, Lang.De));
            Assert.DoesNotContain(r.TypeOf(empty).Render(empty, new RenderContext(alone)), p => p.Tag == "profile");
        }

        // ------------------------------------------------------------------ scale bar and plan scale

        [Fact]
        public void ScaleBarAgainstThePlanScale()
        {
            var r = Fx.Reg();
            var bar = Bar(r);
            var (doc, _) = Fx.Doc(bar);
            var t = Solve(r, doc);
            Assert.Equal(25000, Val(t, bar, "N")!.Value, 6);
            Assert.Null(Val(t, bar, "Np"));
            Assert.Empty(Checks(r, doc));
            var same = doc with { Settings = doc.Settings with { PlanScale = 25000 } };
            Assert.Empty(Checks(r, same));
            var other = doc with { Settings = doc.Settings with { PlanScale = 50000 } };
            var f = Assert.Single(Checks(r, other));
            Assert.Equal("map.scale.plan", f.CheckId);
            Assert.Equal(CheckSeverity.Hint, f.Severity);
        }
    }
}
