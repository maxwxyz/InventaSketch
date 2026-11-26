using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.BuildingServices.Tests
{
    /// <summary>Calc 5: heating curve of the heat generators and pump curve of the circulator (hand calculations).</summary>
    public class CurveTests
    {
        private const double C0 = 273.15;

        private static SketchDocument Doc(params SketchObject[] objs)
        {
            var doc = SketchDocument.Create(PageSizes.Slide16x9, "Curves");
            return Edits.Add(doc, doc.Pages[0].Id, objs);
        }

        private static SketchObject Make(Registry r, string item, string id) => r.Create(r.Item(item)!, Vec2.Zero) with { Id = id };

        private static CharacteristicSet Set(Registry r, SketchDocument doc, string id, string key) => Characteristics.Resolve(r, doc, Characteristics.Link(id, key))!;

        /// <summary>Flow temperature of the heating curve by hand: q = (θ_i − θ_e)/(θ_i − θ_e,d), θ_F = θ_i + Δθ_m·q^(1/n) + ½(θ_F,d − θ_R,d)·q.</summary>
        private static double Flow(double te, double tf, double tr, double ti, double ted, double n)
        {
            var q = (ti - te) / (ti - ted);
            return ti + ((tf + tr) / 2 - ti) * Math.Pow(q, 1 / n) + (tf - tr) / 2 * q;
        }

        [Fact]
        public void HeatingCurvePassesTheDesignPointAndTheRoomTemperature()
        {
            var r = Fx.Reg();
            var boiler = Make(r, "bs.heat.boiler", "b").WithParam("flowTemp", "55 °C").WithParam("returnTemp", "45 °C")
                .WithParam("curveDesignOutdoor", "-12 °C").WithParam("curveRoomTemp", "20 °C").WithParam("curveExponent", "1,3").WithParam("curveOutdoor", "4 °C");
            var set = Set(r, Doc(boiler), "b", "heating");
            var flow = set.Curve("flow")!.Points;
            var design = set.Point("design")!;
            Assert.Equal(-12, design.X, 9);
            Assert.Equal(55, design.Y, 9);
            Assert.Equal(20, flow[^1].Y, 9); // at θ_e = θ_i the curve reaches the room temperature
            // At +4 °C: q = 0.5, θ_F = 20 + 30·0.5^(1/1.3) + 2.5 = 40.10 °C.
            var op = set.Point("op")!;
            Assert.Equal(4, op.X, 9);
            Assert.Equal(Flow(4, 55, 45, 20, -12, 1.3), op.Y, 9);
            Assert.Equal(40.10, op.Y, 2);
            var ret = set.Curve("return")!.Points;
            Assert.True(ret.Zip(flow, (a, b) => a.Y <= b.Y + 1e-9).All(x => x));
        }

        [Fact]
        public void DraggingTheDesignPointEntersTheFlowTemperature()
        {
            var r = Fx.Reg();
            var boiler = Make(r, "bs.heat.boiler", "b").WithParam("flowTemp", "55 °C");
            var doc = Doc(boiler);
            var design = Set(r, doc, "b", "heating").Point("design")!;
            Assert.Equal(PointMotion.AlongY, design.Motion);
            var after = Characteristics.Apply(r, doc, design.Write!(60), Lang.En);
            var t = ValueSolver.Solve(after, r, new[] { "b" });
            Assert.Equal(C0 + 60, t.Get("b", "thf")!.Value!.Value, 6);
            Assert.Equal(60, Set(r, after, "b", "heating").Point("design")!.Y, 6);
        }

        [Fact]
        public void CurrentOutdoorPointSlidesAlongTheCurveAndStoresTheTemperature()
        {
            var r = Fx.Reg();
            var boiler = Make(r, "bs.heat.boiler", "b").WithParam("flowTemp", "55 °C").WithParam("curveOutdoor", "0 °C");
            var doc = Doc(boiler);
            var op = Set(r, doc, "b", "heating").Point("op")!;
            var after = Characteristics.Apply(r, doc, op.Write!(-5), Lang.En);
            Assert.Equal(C0 - 5, Characteristics.ReadParam(after.Get("b")!, "curveOutdoor", Quantities.Temperature, "°C")!.Value, 6);
        }

        [Fact]
        public void CirculatorHasThePumpCurveInLitresPerHour()
        {
            var r = Fx.Reg();
            var pump = Make(r, "bs.heat.pump", "p").WithParam("curveH0", "6 m").WithParam("curveQmax", "3000 l/h").WithParam("flowRate", "1500 l/h");
            var set = Set(r, Doc(pump), "p", CharacteristicKit.PumpSet);
            Assert.Equal("l/h", set.X.Unit);
            // H = 6·(1 − 0.25) = 4.5 m at 1500 l/h.
            Assert.Equal(1500, set.Point("op")!.X, 6);
            Assert.Equal(4.5, set.Point("op")!.Y, 9);
            Assert.Empty(Characteristics.SetsOf(r, Make(r, "bs.heat.radiator", "x"), new RenderContext(Doc())));
        }
    }
}
