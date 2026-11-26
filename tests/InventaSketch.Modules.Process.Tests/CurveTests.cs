using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Process.Tests
{
    /// <summary>Calc 5: the centrifugal pump's pump and system curve with the operating point (hand calculations).</summary>
    public class CurveTests
    {
        private static SketchDocument Doc(params SketchObject[] objs)
        {
            var doc = SketchDocument.Create(PageSizes.Slide16x9, "Curves");
            return Edits.Add(doc, doc.Pages[0].Id, objs);
        }

        private static SketchObject Pump(Registry r) => r.Create(r.Item("process.pump.centrifugal")!, Vec2.Zero) with { Id = "p" };

        private static CharacteristicSet Set(Registry r, SketchDocument doc) => Characteristics.Resolve(r, doc, Characteristics.Link("p", CharacteristicKit.PumpSet))!;

        [Fact]
        public void OperatingPointLiesOnThePumpCurveAndTheSystemCurveRunsThroughIt()
        {
            var r = Fx.Reg();
            var p = Pump(r).WithParam("curveH0", "40 m").WithParam("curveQmax", "100 m³/h").WithParam("curveHstat", "10 m").WithParam("flowRate", "70 m³/h");
            var set = Set(r, Doc(p));
            var op = set.Point("op")!;
            // H = H₀·(1 − (V̇/V̇_max)²) = 40·(1 − 0.49) = 20.4 m at 70 m³/h.
            Assert.Equal(70, op.X, 9);
            Assert.Equal(20.4, op.Y, 9);
            var pump = set.Curve("pump")!.Points;
            Assert.Equal(new Vec2(0, 40), pump[0]);
            Assert.Equal(0, pump[^1].Y, 9);
            Assert.Equal(100, pump[^1].X, 9);
            // System curve H = H_stat + k·V̇² through (0, 10 m) and the operating point: k = 10.4 m / (70 m³/h)².
            var sys = set.Curve("system")!.Points;
            Assert.Equal(10, sys[0].Y, 9);
            var k = 10.4 / (70 * 70);
            Assert.All(sys, s => Assert.Equal(10 + k * s.X * s.X, s.Y, 6));
            Assert.Equal(CurveLook.Load, set.Curve("system")!.Look);
            Assert.Empty(set.Notes);
        }

        [Fact]
        public void ChangingTheShutOffHeadMovesTheOperatingPoint()
        {
            var r = Fx.Reg();
            var p = Pump(r).WithParam("curveH0", "40 m").WithParam("curveQmax", "100 m³/h").WithParam("flowRate", "70 m³/h");
            var before = Set(r, Doc(p)).Point("op")!;
            var after = Set(r, Doc(p.WithParam("curveH0", "50 m"))).Point("op")!;
            Assert.Equal(20.4, before.Y, 9);
            Assert.Equal(25.5, after.Y, 9);
        }

        [Fact]
        public void DraggingThePointEntersFlowAndHeadAndPinsTheAssumedCurve()
        {
            var r = Fx.Reg();
            var p = Pump(r).WithParam("curveH0", "40 m").WithParam("flowRate", "70 m³/h");
            var doc = Doc(p);
            var op = Set(r, doc).Point("op")!;
            Assert.True(op.Draggable);
            var after = Characteristics.Apply(r, doc, op.Write!(50), Lang.En);
            var t = ValueSolver.Solve(after, r, new[] { "p" });
            Assert.Equal(50 / 3600.0, t.Get("p", "Vdot")!.Value!.Value, 9);
            // V̇_max was assumed V̇/0.7 = 100 m³/h and is stored now: H = 40·(1 − 0.25) = 30 m.
            Assert.Equal(30, t.Get("p", "H")!.Value!.Value, 6);
            Assert.Equal(Provenance.Entered, t.Get("p", "H")!.Provenance);
            Assert.Equal(100 / 3600.0, Characteristics.ReadParam(after.Get("p")!, "curveQmax", Quantities.VolumeFlow, "m³/h")!.Value, 9);
            Assert.Equal(50, Set(r, after).Point("op")!.X, 9);
        }

        [Fact]
        public void QuickSketchPumpWithoutValuesStillHasACurve()
        {
            var r = Fx.Reg();
            var set = Set(r, Doc(Pump(r)));
            Assert.Equal(70, set.Point("op")!.X, 9);
            Assert.Equal(40 * 0.51, set.Point("op")!.Y, 9);
            Assert.Equal(3, r.Type("process.pump.centrifugal")!.Parameters.Count(x => x.Key.StartsWith("curve", StringComparison.Ordinal)));
            Assert.Empty(Characteristics.SetsOf(r, r.Create(r.Item("process.valve.manual")!, Vec2.Zero), new RenderContext(Doc())));
        }

        [Fact]
        public void StaticHeadAbovePumpHeadGivesNoOperatingPoint()
        {
            var r = Fx.Reg();
            var p = Pump(r).WithParam("curveH0", "40 m").WithParam("curveHstat", "35 m").WithParam("flowRate", "70 m³/h");
            var set = Set(r, Doc(p));
            Assert.Null(set.Point("op"));
            Assert.Null(set.Curve("system"));
            Assert.NotEmpty(set.Notes);
        }
    }
}
