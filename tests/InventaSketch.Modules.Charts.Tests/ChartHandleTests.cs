using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Charts.Tests
{
    /// <summary>Dragging limit lines, value lines, markers and axis marks of charts (<see cref="ObjectType.DragHandles"/>).</summary>
    public class ChartHandleTests
    {
        /// <summary>Page y of the drawn limit line nearest to <paramref name="near"/>.</summary>
        private static double DrawnY(Registry r, SketchObject o, string tag, double near)
            => Prim.Flatten(r.Render(o, Fx.Ctx())).Where(p => p.Tag == tag).SelectMany(Prim.Points).Select(p => p.Y).OrderBy(y => Math.Abs(y - near)).First();

        [Fact]
        public void LimitLinesOfAnXyChartCanBeDragged()
        {
            var r = Fx.Reg();
            var chart = Fx.Item(r, "chart.limits", new Vec2(120, 90));
            var limits = ChartData.Numbers(chart.Str("limits", ""));
            var handles = r.DragHandles(chart, Fx.Ctx());
            Assert.Equal(limits.Count, handles.Count(x => x.Key.StartsWith("limit.", System.StringComparison.Ordinal)));
            var h = handles.Single(x => x.Key == "limit.1");
            Assert.Equal(DragConstraint.Vertical, h.Constraint);
            Assert.Equal(ChartTags.Limit, h.Part);
            Assert.True(h.Direction.DistanceTo(new Vec2(0, 1)) < 1e-9);
            Assert.StartsWith("Limit line: ", h.Label.En);
            Assert.StartsWith("Grenzlinie: ", h.Label.De);
            Assert.True(Math.Abs(DrawnY(r, chart, ChartTags.Limit, h.Position.Y) - h.Position.Y) < 1e-6);

            // The lower limit 8 mm up (and sideways, which is ignored): a larger, snapped value; the other limit stays.
            var h0 = handles.Single(x => x.Key == "limit.0");
            var moved = r.MoveHandle(chart, "limit.0", h0.Position + new Vec2(5, -8), Fx.Ctx());
            var after = ChartData.Numbers(moved.Str("limits", ""));
            Assert.Equal(limits[1], after[1]);
            Assert.True(after[0] > limits[0], $"{after[0]} > {limits[0]}");
            var c = Fx.Type(r, chart).Compose(chart, Fx.Ctx());
            var res = c.F.Panels[0].Y.Step / 10;
            Assert.True(Math.Abs(after[0] / res - Math.Round(after[0] / res)) < 1e-6, "snapped to " + res + ": " + after[0]);
            // The handle follows the line (within the snapping).
            var h2 = r.DragHandles(moved, Fx.Ctx()).Single(x => x.Key == "limit.0");
            var mmPerUnit = c.F.Panels[0].Area.H / (c.F.Panels[0].Y.Max - c.F.Panels[0].Y.Min);
            Assert.True(Math.Abs(h2.Position.Y - (h0.Position.Y - 8)) <= res * mmPerUnit, h2.Position + " vs " + (h0.Position.Y - 8));
            // Far above the plot: clamped to the top of the axis.
            var top = ChartData.Numbers(r.MoveHandle(chart, "limit.1", h.Position + new Vec2(0, -500), Fx.Ctx()).Str("limits", ""))[1];
            Assert.Equal(c.F.Panels[0].Y.Max, top, 9);
            // Unknown key: unchanged.
            Assert.Same(chart, r.MoveHandle(chart, "limit.9", h.Position, Fx.Ctx()));
        }

        [Fact]
        public void RotatedChartsDragAlongTheirOwnAxis()
        {
            var r = Fx.Reg();
            var chart = Fx.Item(r, "chart.limits", new Vec2(120, 90));
            chart = chart with { Transform = chart.Transform with { Rotation = 90 } };
            var h = r.DragHandles(chart, Fx.Ctx()).Single(x => x.Key == "limit.0");
            Assert.True(Math.Abs(Math.Abs(h.Direction.X) - 1) < 1e-9, h.Direction.ToString());
            var limits = ChartData.Numbers(chart.Str("limits", ""));
            // Against the local "down" direction: the value grows.
            var moved = ChartData.Numbers(r.MoveHandle(chart, "limit.0", h.Position - h.Direction * 6, Fx.Ctx()).Str("limits", ""));
            Assert.True(moved[0] > limits[0], $"{moved[0]} > {limits[0]}");
        }

        [Fact]
        public void ChartsWithoutLimitsHaveOnlyTheirPointAndCurveHandles()
        {
            var r = Fx.Reg();
            var line = r.DragHandles(Fx.Item(r, "chart.line"), Fx.Ctx());
            Assert.NotEmpty(line);
            Assert.All(line, h => Assert.True(h.Key.StartsWith("point.", System.StringComparison.Ordinal) || h.ByPart, h.Key));
            Assert.Empty(r.DragHandles(Fx.Item(r, "chart.column"), Fx.Ctx()));
        }

        private const string FigureJson = "{\"x\":{\"min\":0,\"max\":10,\"marks\":[{\"at\":4,\"text\":\"t1\"}]},"
            + "\"panels\":[{\"min\":0,\"max\":5,\"marks\":[{\"at\":2,\"text\":\"y1\"}]},{\"min\":0,\"max\":1}],"
            + "\"series\":[{\"x\":[0,10],\"y\":[1,4]},{\"panel\":1,\"x\":[0,10],\"y\":[0.2,0.8]}],"
            + "\"refs\":[{\"kind\":\"h\",\"y\":3,\"label\":\"limit\"},{\"kind\":\"v\",\"x\":6},{\"kind\":\"point\",\"series\":0,\"x\":5}],"
            + "\"regions\":[{\"kind\":\"span\",\"from\":2,\"to\":3}]}";

        [Fact]
        public void FigureValueLinesMarkersMarksAndSpansCanBeDragged()
        {
            var r = Fx.Reg();
            var fig = r.Type(FigureChart.TypeId)!.Create(new Vec2(120, 90)).WithParam("spec", FigureJson);
            var handles = r.DragHandles(fig, Fx.Ctx());
            var keys = handles.Where(h => h.Part is not { } p || !p.StartsWith("point.", System.StringComparison.Ordinal) && !h.ByPart).Select(h => h.Key).ToList();
            Assert.Equal(new[] { "ref.0", "ref.1", "ref.2", "xmark.0", "ymark.0.0", "region.0.from", "region.0.to" }, keys);
            DragHandle H(string key) => handles.Single(h => h.Key == key);
            Assert.Equal(DragConstraint.Vertical, H("ref.0").Constraint);
            Assert.Contains("limit", H("ref.0").Label.En);
            Assert.Equal(DragConstraint.Horizontal, H("ref.1").Constraint);
            Assert.Equal(DragConstraint.Horizontal, H("ref.2").Constraint);
            Assert.Equal(DragConstraint.Horizontal, H("xmark.0").Constraint);
            Assert.Equal(DragConstraint.Vertical, H("ymark.0.0").Constraint);

            var c = Fx.Type(r, fig).Compose(fig, Fx.Ctx());
            var mmPerX = c.F.Panels[0].Area.W / 10;
            var mmPerY = c.F.Panels[0].Area.H / 5;
            FigureSpec Spec(SketchObject o) => FigureSpec.Parse(o.Str("spec", ""), Lang.En);

            // Value line 3 → 1 (two units down).
            var hl = Spec(r.MoveHandle(fig, "ref.0", H("ref.0").Position + new Vec2(0, 2 * mmPerY), Fx.Ctx()));
            Assert.Equal(1, hl.Refs[0].Y, 6);
            // Vertical marker 6 → 8.
            Assert.Equal(8, Spec(r.MoveHandle(fig, "ref.1", H("ref.1").Position + new Vec2(2 * mmPerX, 3), Fx.Ctx())).Refs[1].X, 6);
            // Point marker slides along its curve: 5 → 3, the handle sits on the curve again.
            var pm = r.MoveHandle(fig, "ref.2", H("ref.2").Position + new Vec2(-2 * mmPerX, 0), Fx.Ctx());
            Assert.Equal(3, Spec(pm).Refs[2].X, 6);
            var onCurve = r.DragHandles(pm, Fx.Ctx()).Single(h => h.Key == "ref.2").Position;
            Assert.True(Math.Abs(onCurve.Y - H("ref.2").Position.Y) > 0.5, "the point moved along the rising curve");
            // Marks and span edges.
            Assert.Equal(5, Spec(r.MoveHandle(fig, "xmark.0", H("xmark.0").Position + new Vec2(mmPerX, 0), Fx.Ctx())).X.Marks[0].At, 6);
            Assert.Equal(3, Spec(r.MoveHandle(fig, "ymark.0.0", H("ymark.0.0").Position + new Vec2(0, -mmPerY), Fx.Ctx())).Panels[0].Marks[0].At, 6);
            var span = Spec(r.MoveHandle(fig, "region.0.to", H("region.0.to").Position + new Vec2(2 * mmPerX, 0), Fx.Ctx())).Regions[0];
            Assert.Equal(2, span.From!.Value, 6);
            Assert.Equal(5, span.To!.Value, 6);
            // The label is kept and the rest of the specification is untouched.
            Assert.Equal("limit", hl.Refs[0].Label!.En);
            Assert.Equal(2, hl.Series.Count);
        }

        [Fact]
        public void SnapFollowsTheAxisResolution()
        {
            var lin = Axis.Linear(new[] { 0.0, 10 });
            Assert.Equal(2, lin.Step);
            Assert.Equal(3.4, ChartHandles.Snap(lin, 3.43), 9);
            Assert.Equal(10, ChartHandles.Snap(lin, 12), 9);
            var log = Axis.Logarithmic(new[] { 1.0, 1000 });
            Assert.Equal(470, ChartHandles.Snap(log, 468.2), 9);
            Assert.Equal(1000, ChartHandles.Snap(log, 5000), 9);
        }
    }
}
