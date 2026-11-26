using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.BuildingServices.Tests
{
    /// <summary>
    /// Round 5 (2026-11-26): pressure drop of pipes and ducts, expansion vessel sizing, domestic hot water sizing and the
    /// hydraulic separator. Every expected value is a hand calculation.
    /// </summary>
    public class SizingTests
    {
        private const double C0 = 273.15;
        private const double RhoC = 1000 * 4190.0;

        private static ValueTable Solve(SketchDocument doc, Registry r) => ValueSolver.Solve(doc, r, null, new ValueSolveOptions { BudgetMs = 10000 });

        private static double Val(ValueTable t, string id, string key) => t.Get(id, key)!.Value!.Value;

        private static SketchObject Make(Registry r, string item, string id) => r.Create(r.Item(item)!, Vec2.Zero) with { Id = id };

        private static SketchDocument Doc(params SketchObject[] objs)
        {
            var doc = SketchDocument.Create(PageSizes.Slide16x9, "Values");
            return Edits.Add(doc, doc.Pages[0].Id, objs);
        }

        private static SketchObject Pipe(Registry r, string medium, string id, (string Obj, string Port) a, (string Obj, string Port) b)
            => (r.Create(r.Item("bs.pipe")!, Vec2.Zero) with
            {
                Id = id,
                Points = new[] { new Vec2(0, 0), new Vec2(10, 0) },
                Attach = new Attachment?[] { new Attachment(a.Obj, 0, 0) { AnchorId = a.Port }, new Attachment(b.Obj, 0, 0) { AnchorId = b.Port } },
            }).WithParam("medium", medium);

        private static IReadOnlyList<CheckFinding> Checks(Registry r, SketchDocument doc) => ValueSolver.Checks(Solve(doc, r), r, new RenderContext(doc));

        private static double Haaland(double re, double k, double d)
        {
            var x = -1.8 * Math.Log10(Math.Pow(k / d / 3.7, 1.11) + 6.9 / re);
            return 1 / (x * x);
        }

        [Fact]
        public void PipePressureDropFromFlowDiameterAndLength()
        {
            // 1 m³/h in a 26 mm pipe over 20 m: v = 0.523 m/s, Re ≈ 13 600 (turbulent), Δp = λ·L/d·ρ·v²/2.
            var r = Fx.Reg();
            var pipe = Make(r, "bs.pipe", "p").WithParam("flowRate", "1 m³/h").WithParam("innerDiameter", "26 mm").WithParam("length", "20 m");
            var t = Solve(Doc(pipe), r);
            var v = 4 * (1.0 / 3600) / (Math.PI * 0.026 * 0.026);
            var re = v * 0.026 / 1e-6;
            var lambda = Haaland(re, 0.0015e-3, 0.026);
            Assert.Equal(v, Val(t, "p", "v"), 9);
            Assert.Equal(re, Val(t, "p", "Re"), 3);
            Assert.Equal(lambda, Val(t, "p", "lambda"), 9);
            Assert.Equal(lambda * 20 / 0.026 * 1000 * v * v / 2, Val(t, "p", "dp"), 3);
            Assert.Empty(t.Conflicts);
        }

        [Fact]
        public void DuctPressureDropUsesTheHydraulicDiameter()
        {
            // Rectangular 300 × 200 mm, 1000 m³/h over 10 m: d_h = 240 mm, v = 4.63 m/s.
            var r = Fx.Reg();
            var duct = Make(r, "bs.duct", "d").WithParam("flowRate", "1000 m³/h").WithParam("width", 300.0).WithParam("ductHeight", "200 mm").WithParam("length", "10 m");
            var t = Solve(Doc(duct), r);
            var v = 1000.0 / 3600 / (0.3 * 0.2);
            var dh = 2 * 0.3 * 0.2 / 0.5;
            var lambda = Haaland(v * dh / 15e-6, 0.15e-3, dh);
            Assert.Equal(dh, Val(t, "d", "dh"), 9);
            Assert.Equal(lambda * 10 / dh * 1.2 * v * v / 2, Val(t, "d", "dp"), 6);
        }

        [Fact]
        public void ExpansionVesselNominalVolumeAndCheck()
        {
            // V_A = 200 l, e = 2.9 % → V_e = 5.8 l; V_V = 3 l, p₀ = 1.5 bar, p_e = 2.5 bar:
            // V_n = 8.8 l · (2.5 + 1.013)/(2.5 − 1.5) = 30.9 l.
            var r = Fx.Reg();
            SketchObject Vessel(string chosen) => Make(r, "bs.heat.expansion", "mag").WithParam("systemVolume", "200 l").WithParam("expansion", "2,9 %")
                .WithParam("waterSeal", "3 l").WithParam("prePressure", "1,5 bar").WithParam("finalPressure", "2,5 bar").WithParam("chosenVolume", chosen);
            var t = Solve(Doc(Vessel("35 l")), r);
            Assert.Equal(5.8e-3, Val(t, "mag", "Ve"), 12);
            var vn = 8.8e-3 * (2.5e5 + 101325) / 1e5;
            Assert.Equal(vn, Val(t, "mag", "Vn"), 12);
            Assert.DoesNotContain(Checks(r, Doc(Vessel("35 l"))), f => f.CheckId == "heat.expansion.size");
            Assert.Contains(Checks(r, Doc(Vessel("25 l"))), f => f.CheckId == "heat.expansion.size" && f.ObjectId == "mag");
        }

        [Fact]
        public void HotWaterCylinderGivesMixedWaterAndContinuousDraw()
        {
            // 300 l at 60 °C, cold 10 °C, tap 40 °C (defaults): V_mix = 300 · 50/30 = 500 l; Q = ρ·c·V·50 K = 17.46 kWh;
            // 24 kW heating gives 24 000/(ρ·c·30 K) = 11.5 l/min at 40 °C.
            var r = Fx.Reg();
            var heater = Make(r, "bs.san.heater", "wh").WithParam("volume", "300 l").WithParam("storeTemp", "60 °C").WithParam("power", "24 kW");
            var t = Solve(Doc(heater), r);
            Assert.Equal(50, Val(t, "wh", "dT"), 9);
            Assert.Equal(0.5, Val(t, "wh", "Vmix"), 9);
            Assert.Equal(1000 * 0.3 * 4190 * 50, Val(t, "wh", "Q"), 3);
            Assert.Equal(24000 / (RhoC * 30), Val(t, "wh", "Vtap"), 12);
            Assert.Equal(Provenance.Default, t.Get("wh", "thC")!.Provenance);
            Assert.Empty(t.Conflicts);
        }

        [Fact]
        public void SeparatorMixesWhenTheConsumerFlowIsLarger()
        {
            var r = Fx.Reg();
            SketchObject Sep(string vp, string vs) => Make(r, "bs.heat.separator", "hw").WithParam("flowPrimary", vp).WithParam("flowSecondary", vs)
                .WithParam("flowTemp1", "70 °C").WithParam("returnTemp2", "50 °C");
            // Generator flow larger: the consumer gets 70 °C, the surplus raises the primary return.
            var t = Solve(Doc(Sep("1200 l/h", "1000 l/h")), r);
            Assert.Equal(C0 + 70, Val(t, "hw", "thf2"), 9);
            Assert.Equal(C0 + (1000 * 50 + 200 * 70) / 1200.0, Val(t, "hw", "thr1"), 9);
            Assert.Equal(RhoC * 1000e-3 / 3600 * 20, Val(t, "hw", "Phi"), 6);
            // Consumer flow larger: return water mixes into the consumer flow, and the hint shows.
            var u = Solve(Doc(Sep("800 l/h", "1000 l/h")), r);
            Assert.Equal(C0 + (800 * 70 + 200 * 50) / 1000.0, Val(u, "hw", "thf2"), 9);
            Assert.Equal(C0 + 50, Val(u, "hw", "thr1"), 9);
            Assert.Contains(Checks(r, Doc(Sep("800 l/h", "1000 l/h"))), f => f.CheckId == "heat.separator.flow");
            Assert.DoesNotContain(Checks(r, Doc(Sep("1200 l/h", "1000 l/h"))), f => f.CheckId == "heat.separator.flow");
        }

        [Fact]
        public void SeparatorPassesTemperaturesAlongThePipes()
        {
            // Boiler (70 °C) → separator → radiator side: the radiator's flow temperature is the separator's secondary flow.
            var r = Fx.Reg();
            var doc = Doc(
                Make(r, "bs.heat.boiler", "boiler").WithParam("flowTemp", "70 °C"),
                Make(r, "bs.heat.separator", "hw").WithParam("flowPrimary", "1200 l/h").WithParam("flowSecondary", "1000 l/h").WithParam("returnTemp2", "50 °C"),
                Make(r, "bs.heat.radiator", "rad"),
                Pipe(r, "flow", "p1", ("boiler", "flow"), ("hw", "f1")),
                Pipe(r, "return", "p2", ("hw", "r1"), ("boiler", "return")),
                Pipe(r, "flow", "p3", ("hw", "f2"), ("rad", "flow")),
                Pipe(r, "return", "p4", ("rad", "return"), ("hw", "r2")));
            var t = Solve(doc, r);
            Assert.Equal(C0 + 70, Val(t, "hw", "thf1"), 9);
            Assert.Equal(C0 + 70, Val(t, "rad", "thf"), 9);
            Assert.Equal(1000e-3 / 3600, Val(t, "rad", "Vdot"), 12);
            Assert.Equal(1200e-3 / 3600, Val(t, "boiler", "Vdot"), 12);
        }

        [Fact]
        public void SeparatorInsertsInEveryViewWithItsPorts()
        {
            var r = Fx.Reg();
            var t = r.Type("bs.heat.separator")!;
            foreach (var view in new[] { "plan", "section", "schematic" })
            {
                var o = r.WithView(t.Create(Vec2.Zero), view, Fx.Ctx());
                var ports = t.Anchors(o, Fx.Ctx()).Where(a => a.Role == AnchorRole.Port).Select(a => a.Id).ToList();
                Assert.Equal(new[] { "f1", "r1", "f2", "r2" }, ports);
                Assert.NotEmpty(t.Render(o, Fx.Ctx()));
            }
            Assert.Equal("Hydraulische Weiche", r.Item("bs.heat.separator")!.Name.De);
        }
    }
}
