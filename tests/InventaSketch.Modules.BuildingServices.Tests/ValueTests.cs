using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.BuildingServices.Tests
{
    /// <summary>
    /// Calc 4 building services: radiator characteristic and operating point, heating circuit flows and temperatures along
    /// pipes, room air change to duct velocity, mixing, heat pump and the checks. Every expected value is a hand calculation.
    /// </summary>
    public class ValueTests
    {
        private static ValueTable Solve(SketchDocument doc, Registry r) => ValueSolver.Solve(doc, r, null, new ValueSolveOptions { BudgetMs = 10000 });

        private static double Val(ValueTable t, string id, string key) => t.Get(id, key)!.Value!.Value;

        private static SketchObject Make(Registry r, string item, string id) => r.Create(r.Item(item)!, Vec2.Zero) with { Id = id };

        private static SketchDocument Doc(params SketchObject[] objs)
        {
            var doc = SketchDocument.Create(PageSizes.Slide16x9, "Values");
            return Edits.Add(doc, doc.Pages[0].Id, objs);
        }

        private static SketchObject Line(Registry r, string item, string id, (string Obj, string Port) a, (string Obj, string Port) b)
            => r.Create(r.Item(item)!, Vec2.Zero) with
            {
                Id = id,
                Points = new[] { new Vec2(0, 0), new Vec2(10, 0) },
                Attach = new Attachment?[] { new Attachment(a.Obj, 0, 0) { AnchorId = a.Port }, new Attachment(b.Obj, 0, 0) { AnchorId = b.Port } },
            };

        private static IReadOnlyList<CheckFinding> Checks(Registry r, SketchDocument doc) => ValueSolver.Checks(Solve(doc, r), r, new RenderContext(doc));

        private const double C0 = 273.15;
        private const double RhoC = 1000 * 4190.0;

        private static SketchObject Pipe(Registry r, string medium, string id, (string, string) a, (string, string) b)
            => Line(r, "bs.pipe", id, a, b).WithParam("medium", medium);

        [Fact]
        public void ValueTypesDeclareBilingualVariablesWithParametersAndSources()
        {
            var r = Fx.Reg();
            foreach (var id in new[] { "bs.heat.boiler", "bs.heat.heatpump", "bs.heat.radiator", "bs.heat.floorloop", "bs.heat.pump", "bs.heat.mixer", "bs.san.heater", "bs.vent.terminal", "bs.vent.unit", "bs.pipe", "bs.duct", "bs.heat.buffer", "bs.meter.heat", "bs.meter.water" })
            {
                var t = r.Type(id)!;
                Assert.True(ValueModel.HasValues(t), id);
                Assert.True(ValueModel.Variables(t).Count(v => v.Relevance == VarRelevance.Card) <= 4, id);
                foreach (var v in ValueModel.Variables(t))
                {
                    Assert.False(string.IsNullOrEmpty(v.Name.De), id + "." + v.Key);
                    Assert.Single(t.Parameters, p => p.Key == v.ParamKey);
                }
                Assert.All(ValueModel.Relations(t), rel => Assert.NotNull(rel.Source));
            }
            Assert.False(ValueModel.HasValues(r.Type("bs.san.wc")));
        }

        [Fact]
        public void EveryItemStillInsertsWithoutValuesAndShowsNoCheck()
        {
            var r = Fx.Reg();
            var objs = r.Library.Select((item, i) => r.Create(item, new Vec2(i % 12 * 60, i / 12 * 60)) with { Id = "o" + i }).ToArray();
            var doc = Doc(objs);
            var table = Solve(doc, r);
            Assert.Empty(table.Conflicts);
            Assert.Empty(ValueSolver.Checks(table, r, new RenderContext(doc)));
        }

        // ------------------------------------------------------------------ radiator

        [Fact]
        public void RadiatorOutputFromTemperaturesAndCharacteristic()
        {
            var r = Fx.Reg();
            var rad = Make(r, "bs.heat.radiator", "rad").WithParam("phi50", "1000 W").WithParam("flowTemp", "55 °C").WithParam("returnTemp", "45 °C");
            var t = Solve(Doc(rad), r);
            Assert.Empty(t.Conflicts);
            // Δθ_ln = (55 − 45)/ln(35/25) = 29.72 K; Φ = 1000 W · (29.72/50)^1.3 = 508.5 W (n and θ_i = 20 °C defaults).
            var dT = 10 / Math.Log(35 / 25.0);
            Assert.Equal(dT, Val(t, "rad", "dT"), 9);
            Assert.Equal(1000 * Math.Pow(dT / 50, 1.3), Val(t, "rad", "Phi"), 6);
            Assert.Equal(Provenance.Default, t.Get("rad", "n")!.Provenance);
            // V̇ = Φ/(ρ·c·Δθ) = 508.5/(4.19e6 · 10) = 12.1e-6 m³/s = 43.7 l/h.
            Assert.Equal(1000 * Math.Pow(dT / 50, 1.3) / (RhoC * 10), Val(t, "rad", "Vdot"), 12);
        }

        /// <summary>Return temperature of a radiator from flow, flow temperature, room temperature, Φ₅₀ and n (bisection).</summary>
        private static double ReturnTemp(double vdot, double thf, double thi, double phi50, double n)
        {
            double F(double thr) => RhoC * vdot * (thf - thr) - phi50 * Math.Pow((thf - thr) / Math.Log((thf - thi) / (thr - thi)) / 50, n);
            double lo = thi + 1e-6, hi = thf - 1e-9;
            for (var i = 0; i < 200; i++)
            {
                var mid = (lo + hi) / 2;
                if (F(mid) > 0) lo = mid; else hi = mid;
            }
            return (lo + hi) / 2;
        }

        [Fact]
        public void RadiatorChainShowsTheOutputFromFlowTemperaturesAndCharacteristic()
        {
            var r = Fx.Reg();
            var doc = Doc(
                Make(r, "bs.heat.boiler", "boiler").WithParam("flowTemp", "55 °C"),
                Make(r, "bs.heat.radiator", "rad").WithParam("phi50", "1000 W").WithParam("exponent", "1,3"),
                Pipe(r, "flow", "pf", ("boiler", "flow"), ("rad", "flow")).WithParam("flowRate", "50 l/h"),
                Pipe(r, "return", "pr", ("rad", "return"), ("boiler", "return")));
            var t = Solve(doc, r);
            Assert.Empty(t.Conflicts);
            // The radiator takes V̇ = 50 l/h and θ_F = 55 °C over the pipe; its return temperature solves
            // ρ·c·V̇·(θ_F − θ_R) = Φ₅₀·(Δθ_ln/50 K)^n: θ_R ≈ 46.0 °C, Φ ≈ 521 W.
            var vdot = 50e-3 / 3600;
            var thr = ReturnTemp(vdot, C0 + 55, C0 + 20, 1000, 1.3);
            Assert.Equal(vdot, Val(t, "rad", "Vdot"), 12);
            Assert.Equal(Provenance.Linked, t.Get("rad", "thf")!.Provenance);
            Assert.Equal(thr, Val(t, "rad", "thr"), 6);
            Assert.InRange(thr - C0, 46.0, 46.1);
            var phi = RhoC * vdot * (C0 + 55 - thr);
            Assert.Equal(phi, Val(t, "rad", "Phi"), 3);
            Assert.InRange(phi, 520, 523);
            // The return pipe and the boiler take the return temperature; the boiler gives what the radiator emits.
            Assert.Equal(thr, Val(t, "pr", "theta"), 6);
            Assert.Equal(phi, Val(t, "boiler", "Phi"), 3);
            Assert.Empty(Checks(r, doc));
        }

        [Fact]
        public void TwoRadiatorsOnABranchAddUpAtTheBoiler()
        {
            var r = Fx.Reg();
            var doc = Doc(
                Make(r, "bs.heat.boiler", "boiler").WithParam("flowTemp", "70 °C"),
                Make(r, "bs.heat.radiator", "r1").WithParam("flowRate", "40 l/h"),
                Make(r, "bs.heat.radiator", "r2").WithParam("flowRate", "60 l/h"),
                Pipe(r, "flow", "trunk", ("boiler", "flow"), ("x", "*")),
                Pipe(r, "flow", "b1", ("r1", "flow"), ("trunk", "rail0")),
                Pipe(r, "flow", "b2", ("r2", "flow"), ("trunk", "rail0")));
            var t = Solve(doc, r);
            // V̇ at the boiler = 40 + 60 l/h; the flow temperature reaches both radiators.
            Assert.Equal(100e-3 / 3600, Val(t, "boiler", "Vdot"), 12);
            Assert.Equal(C0 + 70, Val(t, "r1", "thf"), 9);
            Assert.Equal(C0 + 70, Val(t, "r2", "thf"), 9);
        }

        // ------------------------------------------------------------------ air

        [Fact]
        public void RoomAreaAndAirChangeGiveTheAirFlowAndDuctVelocity()
        {
            var r = Fx.Reg();
            var doc = Doc(
                Make(r, "bs.vent.terminal", "term").WithParam("roomArea", "20 m²").WithParam("airChange", "1"),
                Make(r, "bs.vent.unit", "unit"),
                Line(r, "bs.duct", "duct", ("term", "duct"), ("unit", "supply")).WithParam("width", 80.0));
            var t = Solve(doc, r);
            Assert.Empty(t.Conflicts);
            // V = 20 m² · 2.5 m (default height) = 50 m³; V̇ = n·V = 1 h⁻¹ · 50 m³ = 50 m³/h.
            Assert.Equal(50, Val(t, "term", "V"), 9);
            Assert.Equal(50 / 3600.0, Val(t, "term", "Vdot"), 12);
            Assert.Equal(50 / 3600.0, Val(t, "duct", "Vdot"), 12);
            Assert.Equal(50 / 3600.0, Val(t, "unit", "Vsup"), 12);
            // v = V̇/A = (50/3600 m³/s)/(π · 0.08²/4 m²) = 2.763 m/s.
            Assert.Equal(50 / 3600.0 / (Math.PI * 0.08 * 0.08 / 4), Val(t, "duct", "v"), 9);
            Assert.DoesNotContain(Checks(r, doc), f => f.CheckId == "air.velocity");
            // The default 160 mm duct runs at 0.69 m/s: below 2 m/s, a hint.
            var wide = Doc(doc.Objects.Values.Select(o => o.Id == "duct" ? o.WithParam("width", 160.0) : o).ToArray());
            Assert.Contains(Checks(r, wide), f => f.CheckId == "air.velocity" && f.ObjectId == "duct");
        }

        [Fact]
        public void RectangularDuctUsesWidthAndHeight()
        {
            var r = Fx.Reg();
            var d = Line(r, "bs.duct", "d", ("x", "a"), ("y", "b")).WithParam("width", 200.0).WithParam("ductHeight", "100 mm").WithParam("flowRate", "216 m³/h");
            var t = Solve(Doc(d), r);
            // A = 0.2 · 0.1 = 0.02 m²; v = 0.06 m³/s / 0.02 m² = 3 m/s; d_h = 2·0.2·0.1/0.3 = 0.1333 m.
            Assert.Equal(0.02, Val(t, "d", "A"), 12);
            Assert.Equal(3, Val(t, "d", "v"), 9);
            Assert.Equal(2 * 0.2 * 0.1 / 0.3, Val(t, "d", "dh"), 12);
        }

        [Fact]
        public void VentilationUnitHeatRecovery()
        {
            var r = Fx.Reg();
            var u = Make(r, "bs.vent.unit", "u").WithParam("flowSupply", "150 m³/h").WithParam("recovery", "80 %")
                .WithParam("tempOutdoor", "-5 °C").WithParam("tempExtract", "21 °C");
            var t = Solve(Doc(u), r);
            // θ_SUP = −5 + 0.8 · 26 = 15.8 °C; Φ = 0.34 · 150 · 20.8 = 1060.8 W.
            Assert.Equal(C0 + 15.8, Val(t, "u", "thSup"), 9);
            Assert.Equal(0.34 * 150 * 20.8, Val(t, "u", "Phi"), 6);
        }

        // ------------------------------------------------------------------ other elements

        [Fact]
        public void MixingValveTemperature()
        {
            var r = Fx.Reg();
            var m = Make(r, "bs.heat.mixer", "m").WithParam("flowA", "300 l/h").WithParam("tempA", "70 °C").WithParam("flowB", "700 l/h").WithParam("tempB", "30 °C");
            var t = Solve(Doc(m), r);
            // θ_AB = (300·70 + 700·30)/1000 = 42 °C.
            Assert.Equal(C0 + 42, Val(t, "m", "thab"), 9);
            Assert.Equal(1e-3 / 3.6, Val(t, "m", "Vab"), 12);
        }

        [Fact]
        public void FloorLoopFlowAndLength()
        {
            var r = Fx.Reg();
            var f = Make(r, "bs.heat.floorloop", "f").WithParam("area", "12 m²").WithParam("heatFlux", "60 W/m²").WithParam("flowTemp", "35 °C").WithParam("returnTemp", "30 °C");
            var t = Solve(Doc(f), r);
            // Φ = 60 · 12 = 720 W; V̇ = 720/(4.19e6 · 5) = 34.4e-6 m³/s; L = 12 m²/0.15 m = 80 m (spacing 150 mm stored at insert).
            Assert.Equal(720, Val(t, "f", "Phi"), 9);
            Assert.Equal(720 / (RhoC * 5), Val(t, "f", "Vdot"), 12);
            Assert.Equal(80, Val(t, "f", "L"), 9);
        }

        [Fact]
        public void WaterHeaterStoredHeatAndHeatUpTime()
        {
            var r = Fx.Reg();
            var h = Make(r, "bs.san.heater", "h").WithParam("volume", "1000 l").WithParam("deltaT", "40 K").WithParam("power", "10 kW");
            var t = Solve(Doc(h), r);
            // Q = 1000 kg · 4.19 kJ/(kg·K) · 40 K = 167.6 MJ = 46.6 kWh; t = Q/P = 4.66 h.
            Assert.Equal(1000 * 4190.0 * 40, Val(t, "h", "Q"), 3);
            Assert.Equal(1000 * 4190.0 * 40 / 1e4, Val(t, "h", "t"), 6);
        }

        [Fact]
        public void BufferTankStoredHeat()
        {
            var r = Fx.Reg();
            var b = Make(r, "bs.heat.buffer", "b").WithParam("volume", "800 l").WithParam("deltaT", "30 K");
            var t = Solve(Doc(b), r);
            // Q = 800 kg · 4.19 kJ/(kg·K) · 30 K = 100.56 MJ = 27.9 kWh.
            Assert.Equal(800 * 4190.0 * 30, Val(t, "b", "Q"), 3);
        }

        [Fact]
        public void HeatMeterShowsThePowerFromFlowAndTemperatures()
        {
            var r = Fx.Reg();
            var m = Make(r, "bs.meter.heat", "m").WithParam("flowRate", "500 l/h").WithParam("flowTemp", "55 °C").WithParam("returnTemp", "45 °C");
            var t = Solve(Doc(m), r);
            // Φ = ρ·c·V̇·Δθ = 4.19e6 J/(m³·K) · (0.5/3600 m³/s) · 10 K = 5819 W.
            Assert.Equal(RhoC * 0.5 / 3600 * 10, Val(t, "m", "Phi"), 6);
        }

        [Fact]
        public void MetersAndValvesInARunPassTheFlowOn()
        {
            var r = Fx.Reg();
            // Pump → ball valve → heat meter → radiator: the radiator's flow reaches the meter and the pump through the valve.
            var doc = Doc(
                Make(r, "bs.heat.pump", "pump"),
                Make(r, "bs.valve.ball", "valve"),
                Make(r, "bs.meter.heat", "meter"),
                Make(r, "bs.heat.radiator", "rad").WithParam("flowRate", "120 l/h"),
                Pipe(r, "flow", "p1", ("pump", "out"), ("valve", "in")),
                Pipe(r, "flow", "p2", ("valve", "out"), ("meter", "in")),
                Pipe(r, "flow", "p3", ("meter", "out"), ("rad", "flow")));
            var t = Solve(doc, r);
            Assert.Empty(t.Conflicts);
            Assert.Equal(120e-3 / 3600, Val(t, "meter", "Vdot"), 12);
            Assert.Equal(120e-3 / 3600, Val(t, "p1", "Vdot"), 12);
            Assert.Equal(120e-3 / 3600, Val(t, "pump", "Vdot"), 12);

            // Water meter in a cold water line.
            var w = Doc(
                Make(r, "bs.meter.water", "wm"),
                Make(r, "bs.valve.ball.water", "v"),
                Pipe(r, "cold", "c1", ("v", "out"), ("wm", "in")).WithParam("flowRate", "1500 l/h"));
            Assert.Equal(1.5 / 3600, Val(Solve(w, r), "wm", "Vdot"), 12);
        }

        [Fact]
        public void PipeVelocityFromDnApproximationAndTheHint()
        {
            var r = Fx.Reg();
            var p = Pipe(r, "flow", "p", ("x", "a"), ("y", "b")).WithParam("dn", 20.0).WithParam("flowRate", "500 l/h");
            var t = Solve(Doc(p), r);
            // d_i ≈ DN 20: v = 4 · (0.5/3600 m³/s)/(π · 0.02²) = 0.442 m/s.
            Assert.Equal(4 * 0.5 / 3600 / (Math.PI * 0.02 * 0.02), Val(t, "p", "v"), 9);
            Assert.Empty(Checks(r, Doc(p)));
            var fast = p.WithParam("flowRate", "2000 l/h");
            Assert.Contains(Checks(r, Doc(fast)), f => f.CheckId == "heat.velocity");
            // Entered d_i 16 mm next to DN 20 is within the approximation, no conflict.
            Assert.Empty(Solve(Doc(p.WithParam("innerDiameter", "16 mm")), r).Conflicts);
        }

        [Fact]
        public void HeatPumpAboveCarnotGivesTheWarning()
        {
            var r = Fx.Reg();
            var hp = Make(r, "bs.heat.heatpump", "hp").WithParam("cop", "9").WithParam("flowTemp", "35 °C").WithParam("sourceTemp", "2 °C");
            // COP_Carnot = 308.15/33 = 9.34: 9 is still below.
            Assert.Empty(Checks(r, Doc(hp)));
            var f = Checks(r, Doc(hp.WithParam("flowTemp", "55 °C"))).Single();
            // COP_Carnot = 328.15/53 = 6.19 < 9.
            Assert.Equal("heat.cop.carnot", f.CheckId);
            Assert.Equal(CheckSeverity.Warning, f.Severity);
        }

        [Fact]
        public void ConsumersAboveTheGeneratorGiveTheBalanceHint()
        {
            var r = Fx.Reg();
            var doc = Doc(
                Make(r, "bs.heat.boiler", "boiler").WithParam("heatOutput", "1 kW"),
                Make(r, "bs.heat.radiator", "r1").WithParam("heatOutput", "800 W"),
                Make(r, "bs.heat.radiator", "r2").WithParam("heatOutput", "600 W"),
                Pipe(r, "flow", "p1", ("boiler", "flow"), ("r1", "flow")),
                Pipe(r, "return", "p2", ("r1", "return"), ("r2", "flow")),
                Pipe(r, "return", "p3", ("r2", "return"), ("boiler", "return")));
            Assert.Contains(Checks(r, doc), f => f.CheckId == "heat.balance" && f.ObjectId == "boiler");
        }
    }
}
