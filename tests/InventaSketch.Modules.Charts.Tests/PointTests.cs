using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Charts.Tests
{
    /// <summary>Data points and whole curves on the drawing: point parts, drag handles, curve drag, delete, add, value.</summary>
    public class PointTests
    {
        private static SketchObject Xy(Registry r) => AreaTests.Xy(r);

        private static IReadOnlyList<XySeries> Series(SketchObject o) => ChartData.Xy(ChartData.Parse(o.Str("data", "")));

        [Fact]
        public void EveryDataPointIsAFinePartWithAShapeAndALabel()
        {
            var r = Fx.Reg();
            var o = Xy(r);
            var parts = r.Parts(o, Fx.Ctx());
            var points = parts.Where(p => p.Kind == ChartPoints.Kind).ToList();
            Assert.Equal(15, points.Count);
            Assert.All(points, p => Assert.True(p.Detail));
            var p3 = points.Single(p => p.Key == "point.0.2");
            Assert.Equal("Point 3 of Rising (2; 3)", p3.Label.En);
            Assert.Equal("Punkt 3 von Rising (2; 3)", p3.Label.De);
            // The shape is never drawn, but it is there for hit test and highlight ((1; 2) lies on the rising curve only).
            Assert.DoesNotContain(Prim.Flatten(Fx.Render(r, o)), p => p.Tag == "point.0.1");
            var shape = Assert.Single(r.PartShapes(o, Fx.Ctx()), p => p.Tag == "point.0.1");
            var at = ((EllipsePrim)shape).Center;
            Assert.Equal("point.0.1", r.PartAt(o, at, Fx.Ctx(), 1.0));
            // Between two points the curve is hit.
            var next = ((EllipsePrim)r.PartShapes(o, Fx.Ctx()).Single(p => p.Tag == "point.0.2")).Center;
            Assert.Equal("series.0", r.PartAt(o, (at + next) / 2, Fx.Ctx(), 1.0));
            Assert.NotEmpty(r.PartPrimitives(o, "point.0.2", Fx.Ctx()));
        }

        [Fact]
        public void PointHandlesAreFreeSnappedAndKeepTheOrderOfX()
        {
            var r = Fx.Reg();
            var o = Xy(r);
            var handles = r.DragHandles(o, Fx.Ctx());
            var h = handles.Single(x => x.Key == "point.0.2");
            Assert.Equal(DragConstraint.Free, h.Constraint);
            Assert.Equal("point.0.2", h.Part);
            Assert.Equal("series.0", h.ShowWith);
            var c = Fx.Type(r, o).Compose(o, Fx.Ctx());
            var panel = c.F.Panels[0];
            var m = o.Matrix();
            // Dragged to (2.03, 3.47): snapped to a tenth of the axis step.
            var moved = r.MoveHandle(o, h.Key, m.Apply(c.F.P(panel, 2.03, 3.47)), Fx.Ctx());
            var p = Series(moved)[0].Points[2];
            Assert.Equal(2.0, p.X, 9);
            Assert.Equal(3.5, p.Y, 9);
            // Far to the right: x stops at the next point (the curve stays a curve over x).
            var far = r.MoveHandle(o, h.Key, m.Apply(c.F.P(panel, 3.9, 3)), Fx.Ctx());
            Assert.Equal(3.0, Series(far)[0].Points[2].X, 9);
            // The other curves are unchanged, names stay.
            Assert.Equal(Series(o)[1].Points, Series(moved)[1].Points);
            Assert.Equal(new[] { "Rising", "Flat", "Falling" }, Series(moved).Select(s => s.Name));
        }

        [Fact]
        public void ACurveIsDraggedAsAWholeBySnappedOffsets()
        {
            var r = Fx.Reg();
            var o = Xy(r);
            var body = r.DragHandles(o, Fx.Ctx()).Single(x => x.Key == "series.1");
            Assert.True(body.ByPart);
            Assert.Equal("series.1", body.Part);
            var c = Fx.Type(r, o).Compose(o, Fx.Ctx());
            var panel = c.F.Panels[0];
            var m = o.Matrix();
            double mmPerY = panel.Area.H / (panel.Y.Max - panel.Y.Min);
            double mmPerX = c.F.Panels[0].Area.W / (c.F.X.Max - c.F.X.Min);
            // Mostly up: only y changes (by 0.8, snapped).
            var up = r.MoveHandle(o, body.Key, body.Position + new Vec2(0.1 * mmPerX, -0.81 * mmPerY), Fx.Ctx());
            var s = Series(up)[1].Points;
            Assert.All(s, q => Assert.Equal(3.8, q.Y, 9));
            Assert.Equal(Series(o)[1].Points.Select(q => q.X), s.Select(q => q.X));
            // Diagonal: both.
            var diag = r.MoveHandle(o, body.Key, body.Position + new Vec2(1 * mmPerX, -1 * mmPerY), Fx.Ctx());
            Assert.Equal(new Vec2(1, 4), Series(diag)[1].Points[0]);
        }

        [Fact]
        public void PointsAndCurvesAreDeletedAddedAndTyped()
        {
            var r = Fx.Reg();
            var o = Xy(r).WithParam("areas", FigureSpec.AreasJson(new[] { new FigRegion { Kind = "between", A = 0, B = 2 }, new FigRegion { Kind = "below", A = 1 } }));
            o = PartLooks.WithLook(o, "series.2", l => l with { Style = new Style { Stroke = "#ff0000" } });
            var pa = r.PartActions(o, "point.0.2", Fx.Ctx());
            var deleted = pa.Single(a => a.Id == PartLooks.DeleteAction).Apply(o);
            Assert.Equal(new[] { 0.0, 1, 3, 4 }, Series(deleted)[0].Points.Select(p => p.X));
            var added = pa.Single(a => a.Id == "chart.point.add").Apply(o);
            Assert.Equal(new Vec2(2.5, 3.5), Series(added)[0].Points[3]);
            var typed = pa.Single(a => a.Id == "chart.point.value").Input!.Apply(o, "2,5; 7");
            Assert.Equal(new Vec2(2.5, 7), Series(typed)[0].Points[2]);
            Assert.Throws<FormatException>(() => pa.Single(a => a.Id == "chart.point.value").Input!.Apply(o, "soon"));
            // Deleting curve 2 ("Flat"): its area goes, the area on curves 1 and 3 now refers to curves 1 and 2, and the
            // look of the old curve 3 moves with it.
            var sa = r.PartActions(o, "series.1", Fx.Ctx());
            Assert.Equal("Delete curve", sa.Single(a => a.Id == PartLooks.DeleteAction).Label.En);
            var noFlat = sa.Single(a => a.Id == PartLooks.DeleteAction).Apply(o);
            Assert.Equal(new[] { "Rising", "Falling" }, Series(noFlat).Select(s => s.Name));
            var areas = XyEditor.Areas(noFlat);
            var area = Assert.Single(areas);
            Assert.Equal((0, 1), (area.A, area.B));
            Assert.Equal("#ff0000", PartLooks.LookOf(noFlat.Parts, "series.1")!.Style!.Stroke);
            Assert.Null(PartLooks.LookOf(noFlat.Parts, "series.2"));
        }

        [Fact]
        public void EditingASketchedCurveMakesItPlainDataAndKeepsTheOtherMemos()
        {
            var r = Fx.Reg();
            var chart = Fx.Item(r, "chart.line", new Vec2(120, 90));
            var c = Fx.Type(r, chart).Compose(chart, Fx.Ctx());
            var m = chart.Matrix();
            var panel = c.F.Panels[0];
            var trail = Enumerable.Range(0, 40).Select(i => 0.2 + 5.6 * i / 39.0).Select(x => m.Apply(c.F.P(panel, x, 3 + Math.Sin(x)))).ToList();
            var once = ChartSketchInput.Add(r, chart, trail, Fx.Ctx())!.Chart;
            var twice = ChartSketchInput.Add(r, once, trail.Select(p => p + new Vec2(0, 3)).ToList(), Fx.Ctx())!.Chart;
            Assert.NotNull(ChartSketchInput.Memo(twice, 1));
            Assert.NotNull(ChartSketchInput.Memo(twice, 2));
            var edited = r.PartActions(twice, "point.1.0", Fx.Ctx()).Single(a => a.Id == PartLooks.DeleteAction).Apply(twice);
            Assert.Null(ChartSketchInput.Memo(edited, 1));
            Assert.NotNull(ChartSketchInput.Memo(edited, 2));
            Assert.Equal(Series(twice)[2].Points, Series(edited)[2].Points);
        }

        [Fact]
        public void FiguresOfferTheSamePointsCurvesAndHandles()
        {
            var r = Fx.Reg();
            var spec = new FigureSpec();
            spec.Series.Add(new FigSeries { X = new List<double> { 0, 1, double.NaN, 3, 4 }, Y = new List<double> { 1, 2, double.NaN, 2, 1 }, Label = new I18nText("Power", "Leistung") });
            spec.Series.Add(new FigSeries { X = new List<double> { 0, 4 }, Y = new List<double> { 0.5, 0.5 } });
            spec.Refs.Add(new FigRef { Kind = "point", Series = 1, X = 2 });
            spec.Regions.Add(new FigRegion { Kind = "below", A = 1 });
            var o = r.Type(FigureChart.TypeId)!.Create(new Vec2(60, 40)).WithParam("spec", spec.ToJson());
            var parts = r.Parts(o, Fx.Ctx()).Where(p => p.Kind == ChartPoints.Kind).Select(p => p.Key).ToList();
            Assert.Equal(new[] { "point.0.0", "point.0.1", "point.0.3", "point.0.4", "point.1.0", "point.1.1" }, parts);
            Assert.Equal("Point 4 of Power (3; 2)", r.Parts(o, Fx.Ctx()).Single(p => p.Key == "point.0.3").Label.En);
            var handles = r.DragHandles(o, Fx.Ctx());
            Assert.Contains(handles, h => h.Key == "point.0.3" && h.ShowWith == "series.0");
            Assert.Contains(handles, h => h.Key == "series.0" && h.ByPart);
            var deleted = r.PartActions(o, "point.0.3", Fx.Ctx()).Single(a => a.Id == PartLooks.DeleteAction).Apply(o);
            var ds = FigureSpec.Parse(deleted.Str("spec", ""), Lang.En);
            Assert.Equal(4, ds.Series[0].X.Count);
            Assert.True(double.IsNaN(ds.Series[0].X[2])); // the gap stays
            var noSecond = r.PartActions(o, "series.1", Fx.Ctx()).Single(a => a.Id == PartLooks.DeleteAction).Apply(o);
            var ns = FigureSpec.Parse(noSecond.Str("spec", ""), Lang.En);
            Assert.Single(ns.Series);
            Assert.Empty(ns.Refs);     // the point marker on the deleted curve
            Assert.Empty(ns.Regions);  // the area below it
        }
    }
}
