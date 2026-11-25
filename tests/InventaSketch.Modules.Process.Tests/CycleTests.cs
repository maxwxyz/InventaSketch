using System;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Process.Tests
{
    public class CycleTests
    {
        [Fact]
        public void OttoStatesFollowTheIsentropesAndTheTextbookEfficiency()
        {
            var d = Cycles.Otto(8, 2.5, 1.4);
            var (p1, p2, p3, p4) = (d.States[0], d.States[1], d.States[2], d.States[3]);
            Assert.Equal(p1.Y * Math.Pow(p1.X, 1.4), p2.Y * Math.Pow(p2.X, 1.4), 9);
            Assert.Equal(p3.Y * Math.Pow(p3.X, 1.4), p4.Y * Math.Pow(p4.X, 1.4), 9);
            Assert.Equal(p2.X, p3.X, 9); // heat addition at constant volume
            Assert.Equal(p4.X, p1.X, 9); // heat rejection at constant volume
            Assert.Equal(8, p1.X / p2.X, 9);
            Assert.Equal(1 - Math.Pow(8, -0.4), d.Efficiency!.Value, 9);
            Assert.Equal(0.5647, d.Efficiency.Value, 3);
        }

        [Fact]
        public void DieselHeatAdditionIsIsobaricAndItsEfficiencyIsBelowOtto()
        {
            var d = Cycles.Diesel(16, 2, 1.4);
            Assert.Equal(d.States[1].Y, d.States[2].Y, 9);
            Assert.Equal(2, d.States[2].X / d.States[1].X, 9);
            Assert.Equal(d.States[3].X, d.States[0].X, 9);
            var expected = 1 - Math.Pow(16, -0.4) * (Math.Pow(2, 1.4) - 1) / (1.4 * (2 - 1));
            Assert.Equal(expected, d.Efficiency!.Value, 9);
            Assert.True(d.Efficiency.Value < Cycles.OttoEfficiency(16));
        }

        [Fact]
        public void IdealGasCarnotOnPvUsesIsothermsAndAdiabats()
        {
            var d = Cycles.CarnotPv(8, 1.4);
            var s = d.States;
            Assert.Equal(s[1].X * s[1].Y, s[2].X * s[2].Y, 9); // T_high isotherm
            Assert.Equal(s[3].X * s[3].Y, s[0].X * s[0].Y, 9); // T_low isotherm
            Assert.Equal(s[0].Y * Math.Pow(s[0].X, 1.4), s[1].Y * Math.Pow(s[1].X, 1.4), 9);
            Assert.Equal(s[2].Y * Math.Pow(s[2].X, 1.4), s[3].Y * Math.Pow(s[3].X, 1.4), 9);
            var tRatio = s[1].X * s[1].Y / (s[0].X * s[0].Y);
            Assert.Equal(1 - 1 / tRatio, d.Efficiency!.Value, 9);
            Assert.Equal(8, s[3].X / s[1].X, 9);
        }

        [Fact]
        public void RankineExpandsIntoTheWetRegionAndCarnotIsARectangle()
        {
            var d = Cycles.Rankine();
            var (p1, p3, p4) = (d.States[0], d.States[2], d.States[3]);
            Assert.Equal(Cycles.SatLiquidS(p1.Y), p1.X, 9);   // 1: saturated liquid
            Assert.True(p3.X > Cycles.SatVapourS(0.62));          // 3: superheated
            Assert.Equal(p3.X, p4.X, 9);                         // 3→4 isentropic
            Assert.True(p4.X < Cycles.SatVapourS(p4.Y) && p4.X > Cycles.SatLiquidS(p4.Y)); // wet steam at the turbine exit
            var saturated = Cycles.Rankine(superheat: false);
            Assert.Equal(Cycles.SatVapourS(0.62), saturated.States[2].X, 9);

            var c = Cycles.CarnotTs();
            Assert.Equal(c.States[0].X, c.States[1].X, 9);
            Assert.Equal(c.States[1].Y, c.States[2].Y, 9);
            Assert.Equal(c.States[2].X, c.States[3].X, 9);
            Assert.Equal(c.States[3].Y, c.States[0].Y, 9);
            Assert.Null(c.Efficiency); // normalised axes: no number is claimed
        }

        [Fact]
        public void DomesRiseToTheCriticalPointAndAreClosedOverIt()
        {
            var ts = Cycles.TsDome();
            Assert.Equal(Cycles.TsCriticalT, ts.Max(p => p.Y), 9);
            Assert.True(ts.First().X < Cycles.TsCriticalS && ts.Last().X > Cycles.TsCriticalS);
            var ph = Cycles.PhDome();
            Assert.Equal(Cycles.PhCriticalY, ph.Max(p => p.Y), 9);
            Assert.All(ts.Concat(ph), p => Assert.InRange(p.X, 0, 1));
        }

        [Fact]
        public void RefrigerationCycleThrottlesIntoTheDomeAndCompressesAlongLnP()
        {
            var d = Cycles.Refrigeration(4, superheat: true, subcool: false);
            var (p1, p2, p3, p4) = (d.States[0], d.States[1], d.States[2], d.States[3]);
            Assert.Equal(p3.X, p4.X, 9);                       // isenthalpic throttling
            Assert.Equal(p2.Y, p3.Y, 9);                       // condensation at the upper pressure
            Assert.Equal(p4.Y, p1.Y, 9);                       // evaporation at the lower pressure
            Assert.Equal(Cycles.PhLogScale * Math.Log(4), p2.Y - p1.Y, 9);
            Assert.Equal(0.09 * Math.Log(4), p2.X - p1.X, 9); // h2 − h1 = c·ln(p2/p1)
            Assert.True(p4.X > Cycles.SatLiquidH(p4.Y) && p4.X < Cycles.SatVapourH(p4.Y)); // wet vapour after the valve
            Assert.True(p1.X > Cycles.SatVapourH(p1.Y));      // superheated suction gas
            var high = Cycles.Refrigeration(100);
            Assert.True(high.States[1].Y < Cycles.PhCriticalY, "the condensing pressure stays below the critical point");
        }

        [Theory]
        [InlineData("process.chart.ts")]
        [InlineData("process.chart.pv")]
        [InlineData("process.chart.ph")]
        [InlineData("process.chart.ts.carnot")]
        [InlineData("process.chart.pv.diesel")]
        [InlineData("process.chart.pv.carnot")]
        public void ChartsDrawAxesTheCycleAndFourNumberedStates(string itemId)
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, itemId);
            var prims = r.TypeOf(o).Render(o, Fx.Ctx());
            Assert.Equal(new[] { "1", "2", "3", "4" }, prims.OfType<TextPrim>().Where(p => p.Tag == ProcessTags.State).Select(p => p.Text));
            Assert.Equal(4, prims.Count(p => p is EllipsePrim && p.Tag == ProcessTags.State));
            Assert.Single(prims, p => p is PathPrim && p.Tag == ProcessTags.Cycle && ((PathPrim)p).Segments.Last().Op == SegOp.Z);
            Assert.Equal(2, prims.Count(p => p is LinePrim && p.Tag == ProcessTags.Axis));
            Assert.Equal(2, prims.Count(p => p is TextPrim && p.Tag == ProcessTags.Axis));
            Fx.AssertInsideBox(prims, o.Size, itemId);
        }

        [Fact]
        public void ChartAxisLabelsAndCaptionFollowTheLanguage()
        {
            var r = Fx.Reg();
            string Texts(SketchObject o, Lang lang) => string.Join("|", r.TypeOf(o).Render(o, Fx.Ctx(lang)).OfType<TextPrim>().Select(p => p.Text));
            var ph = Fx.Item(r, "process.chart.ph");
            Assert.Contains("log p", Texts(ph, Lang.En));
            Assert.Contains("lg p", Texts(ph, Lang.De));
            Assert.Contains("Kaltdampf", Texts(ph, Lang.De));
            var ts = Fx.Item(r, "process.chart.ts");
            Assert.Contains("Rankine cycle", Texts(ts, Lang.En));
            Assert.Contains("Clausius-Rankine-Prozess", Texts(ts, Lang.De));
        }

        [Fact]
        public void PvChartShowsTheComputedEfficiencyOnRequest()
        {
            var r = Fx.Reg();
            var pv = Fx.Item(r, "process.chart.pv");
            string Caption(SketchObject o, Lang lang) => r.TypeOf(o).Render(o, Fx.Ctx(lang)).OfType<TextPrim>().Single(p => p.Tag == ProcessTags.Label).Text;
            Assert.DoesNotContain("η", Caption(pv, Lang.En));
            var on = Fx.Action(r, pv, "process.chart.efficiency").Apply(pv);
            Assert.Equal("Otto cycle, η = 56.5 %", Caption(on, Lang.En));
            Assert.Equal("Otto-Prozess, η = 56,5 %", Caption(on, Lang.De));
        }

        [Fact]
        public void ChartDomeAndLabelsCanBeSwitchedOff()
        {
            var r = Fx.Reg();
            var ts = Fx.Item(r, "process.chart.ts");
            var t = r.TypeOf(ts);
            Assert.Contains(t.Render(ts, Fx.Ctx()), p => p.Tag == ProcessTags.Dome);
            var noDome = Fx.Action(r, ts, "process.chart.dome").Apply(ts);
            Assert.DoesNotContain(t.Render(noDome, Fx.Ctx()), p => p.Tag == ProcessTags.Dome);
            var noLabels = Fx.Action(r, ts, "process.chart.labels").Apply(ts);
            Assert.DoesNotContain(t.Render(noLabels, Fx.Ctx()), p => p is TextPrim && p.Tag == ProcessTags.State);
            var pv = Fx.Item(r, "process.chart.pv");
            Assert.DoesNotContain(r.TypeOf(pv).Render(pv, Fx.Ctx()), p => p.Tag == ProcessTags.Dome);
        }

        [Fact]
        public void CompressionRatioActionsStayWithinTheirLimits()
        {
            var r = Fx.Reg();
            var pv = Fx.Item(r, "process.chart.pv");
            var up = Fx.Action(r, pv, "process.chart.ratio.up").Apply(pv);
            Assert.Equal(9, up.Num("ratio", 0), 9);
            var low = pv.WithParam("ratio", 2.0);
            Assert.Equal(2, Fx.Action(r, low, "process.chart.ratio.down").Apply(low).Num("ratio", 0), 9);
        }
    }
}
