using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.BuildingServices.Tests
{
    /// <summary>
    /// Round 6 (2026-11-26): hydraulic circuits. Single losses with ζ and k_v, the pipe's Σζ, water density and viscosity from
    /// the temperature, the floor loop's pressure drop, and the pump's index circuit (largest pressure-drop sum from its
    /// outlet back to its inlet) with the head and balance checks. Every expected value is a hand calculation.
    /// </summary>
    public class CircuitTests
    {
        private const double G = 9.80665;

        private static ValueTable Solve(SketchDocument doc, Registry r) => ValueSolver.Solve(doc, r, null, new ValueSolveOptions { BudgetMs = 10000 });

        private static double Val(ValueTable t, string id, string key) => t.Get(id, key)!.Value!.Value;

        private static SketchObject Make(Registry r, string item, string id) => r.Create(r.Item(item)!, Vec2.Zero) with { Id = id };

        private static SketchDocument Doc(params SketchObject[] objs)
        {
            var doc = SketchDocument.Create(PageSizes.Slide16x9, "Values");
            return Edits.Add(doc, doc.Pages[0].Id, objs);
        }

        private static Attachment? At((string Obj, string? Port)? end)
            => end is { } e ? new Attachment(e.Obj, 0, 0) { AnchorId = e.Port } : null;

        /// <summary>A pipe between two ends; an end (pipe id, null) docks on another pipe (a tee), a null end is free.</summary>
        private static SketchObject Pipe(Registry r, string medium, string id, (string, string?)? a, (string, string?)? b)
            => (r.Create(r.Item("bs.pipe")!, Vec2.Zero) with
            {
                Id = id,
                Points = new[] { new Vec2(0, 0), new Vec2(10, 0) },
                Attach = new[] { At(a), At(b) },
            }).WithParam("medium", medium);

        private static IReadOnlyList<CheckFinding> Checks(Registry r, SketchDocument doc) => ValueSolver.Checks(Solve(doc, r), r, new RenderContext(doc));

        private static double Haaland(double re, double k, double d)
        {
            var x = -1.8 * Math.Log10(Math.Pow(k / d / 3.7, 1.11) + 6.9 / re);
            return 1 / (x * x);
        }

        // ------------------------------------------------------------------ water properties

        [Theory]
        [InlineData(20, 998.21, 1.0016e-3)]
        [InlineData(50, 988.03, 0.5465e-3)]
        [InlineData(70, 977.76, 0.4040e-3)]
        [InlineData(80, 971.79, 0.3544e-3)]
        public void WaterDensityAndViscosityFollowTheTables(double celsius, double rho, double mu)
        {
            // Table values of liquid water at 1 bar (IAPWS formulations, as in the property tables of [S29]).
            var t = 273.15 + celsius;
            Assert.InRange(BsValues.WaterDensity(t) / rho, 0.998, 1.002);
            Assert.InRange(BsValues.WaterViscosity(t) / (mu / rho), 0.99, 1.01);
        }

        [Fact]
        public void PipeTakesDensityAndViscosityFromItsTemperature()
        {
            var r = Fx.Reg();
            var pipe = Make(r, "bs.pipe", "p").WithParam("flowRate", "1 m³/h").WithParam("innerDiameter", "26 mm").WithParam("length", "20 m")
                .WithParam("temperature", "70 °C");
            var t = Solve(Doc(pipe), r);
            var rho = BsValues.WaterDensity(273.15 + 70);
            var nu = BsValues.WaterViscosity(273.15 + 70);
            Assert.Equal(rho, Val(t, "p", "rho"), 9);
            Assert.Equal(Provenance.Derived, t.Get("p", "rho")!.Provenance);
            Assert.Equal(nu, Val(t, "p", "nu"), 15);
            var v = 4 * (1.0 / 3600) / (Math.PI * 0.026 * 0.026);
            var lambda = Haaland(v * 0.026 / nu, 0.0015e-3, 0.026);
            Assert.Equal(lambda * 20 / 0.026 * rho * v * v / 2, Val(t, "p", "dp"), 3);
            Assert.Empty(t.Conflicts);
            // Without a temperature the 20 °C defaults stay.
            var cold = Solve(Doc(Make(r, "bs.pipe", "q").WithParam("flowRate", "1 m³/h").WithParam("innerDiameter", "26 mm")), r);
            Assert.Equal(Provenance.Default, cold.Get("q", "rho")!.Provenance);
            Assert.Equal(1000, Val(cold, "q", "rho"), 9);
        }

        [Fact]
        public void TemperaturePassedOnTheNetKeepsTheDefaults()
        {
            // The flow pipe takes 70 °C from the boiler, but only an entered temperature sets ρ and ν; the radiator's
            // operating point (which needs its default exponent and room temperature) still solves, whatever the order.
            var r = Fx.Reg();
            var doc = Doc(
                Pipe(r, "flow", "a1", ("boiler", "flow"), ("rad", "flow")).WithParam("flowRate", "50 l/h"),
                Pipe(r, "return", "a2", ("rad", "return"), ("boiler", "return")),
                Make(r, "bs.heat.boiler", "boiler").WithParam("flowTemp", "70 °C"),
                Make(r, "bs.heat.radiator", "rad").WithParam("phi50", "1000 W"));
            var t = Solve(doc, r);
            Assert.Equal(273.15 + 70, Val(t, "a1", "theta"), 9);
            Assert.Equal(Provenance.Default, t.Get("a1", "rho")!.Provenance);
            Assert.NotNull(t.Get("rad", "thr")!.Value);
            Assert.NotNull(t.Get("a2", "theta")!.Value);
            Assert.Empty(t.Conflicts);
        }

        // ------------------------------------------------------------------ single losses

        [Fact]
        public void PipeSingleLossesFromTheSumOfLossCoefficients()
        {
            // 1 m³/h in 26 mm: v = 0.523 m/s; Σζ = 3 (two bends and a tee) → Δp_ζ = 3·1000·v²/2 ≈ 411 Pa.
            var r = Fx.Reg();
            var pipe = Make(r, "bs.pipe", "p").WithParam("flowRate", "1 m³/h").WithParam("innerDiameter", "26 mm").WithParam("lossCoefficient", "3");
            var t = Solve(Doc(pipe), r);
            var v = 4 * (1.0 / 3600) / (Math.PI * 0.026 * 0.026);
            Assert.Equal(3 * 1000 * v * v / 2, Val(t, "p", "dpz"), 6);
        }

        [Fact]
        public void ValveLossFromKvAndFromZetaWithThePipeItSitsIn()
        {
            var r = Fx.Reg();
            // Thermostatic valve k_v = 0.5 m³/h with 200 l/h: Δp = 1 bar·(0.2/0.5)² = 16 kPa.
            var trv = Make(r, "bs.heat.trv", "trv").WithParam("kvValue", "0,5 m³/h");
            var t = Solve(Doc(trv, Pipe(r, "flow", "p", ("trv", "in"), null).WithParam("flowRate", "200 l/h")), r);
            Assert.Equal(0.2 / 3600, Val(t, "trv", "Vdot"), 12);
            Assert.Equal(16000, Val(t, "trv", "dp"), 6);
            // Ball valve ζ = 2 in a 26 mm pipe with 1 m³/h: Δp = 2·ρ·v²/2.
            var ball = Make(r, "bs.valve.ball", "kh").WithParam("lossCoefficient", "2");
            var u = Solve(Doc(ball, Pipe(r, "flow", "q", null, ("kh", "out")).WithParam("flowRate", "1 m³/h").WithParam("innerDiameter", "26 mm")), r);
            var v = 4 * (1.0 / 3600) / (Math.PI * 0.026 * 0.026);
            Assert.Equal(v, Val(u, "kh", "v"), 9);
            Assert.Equal(2 * 1000 * v * v / 2, Val(u, "kh", "dp"), 6);
            Assert.Empty(u.Conflicts);
            // The k_v value follows from an allowed pressure drop.
            var back = Make(r, "bs.meter.water", "wz").WithParam("flowRate", "2 m³/h").WithParam("pressureDrop", "25 kPa");
            Assert.Equal(2 / Math.Sqrt(0.25) / 3600, Val(Solve(Doc(back), r), "wz", "kv"), 12);
        }

        [Fact]
        public void FloorLoopPressureDropFromItsPipe()
        {
            // 100 l/h through 80 m of 12 mm pipe: v = 0.246 m/s, Re ≈ 2950 (turbulent), Δp = λ·L/d·ρ·v²/2 ≈ 2.4 kPa.
            var r = Fx.Reg();
            var loop = Make(r, "bs.heat.floorloop", "fbh").WithParam("flowRate", "100 l/h").WithParam("loopLength", "80 m").WithParam("innerDiameter", "12 mm");
            var t = Solve(Doc(loop), r);
            var v = 4 * (0.1 / 3600) / (Math.PI * 0.012 * 0.012);
            var lambda = Haaland(v * 0.012 / 1e-6, 0.0015e-3, 0.012);
            Assert.Equal(lambda * 80 / 0.012 * 1000 * v * v / 2, Val(t, "fbh", "dp"), 3);
        }

        // ------------------------------------------------------------------ circuits

        /// <summary>
        /// Boiler → pump → flow trunk f1 to TRV 1 → radiator 1 → return r1 → boiler; a tee f2 from f1 to TRV 2 → radiator 2 →
        /// r2 teed into r1. Entered drops: boiler 5 kPa, f0 1 kPa, f1 2 kPa, f2 3 kPa, TRV 1 <paramref name="trv1"/>, TRV 2
        /// 1 kPa, radiators 1 kPa each, r1 2 kPa, r2 3 kPa; the connections c1/c2 carry nothing.
        /// </summary>
        private static SketchDocument TwoRadiators(Registry r, string trv1, string head) => Doc(
            Make(r, "bs.heat.boiler", "b").WithParam("pressureDrop", "5 kPa"),
            Make(r, "bs.heat.pump", "pu").WithParam("head", head),
            Make(r, "bs.heat.trv", "t1").WithParam("pressureDrop", trv1),
            Make(r, "bs.heat.trv", "t2").WithParam("pressureDrop", "1 kPa"),
            Make(r, "bs.heat.radiator", "rad1").WithParam("pressureDrop", "1 kPa"),
            Make(r, "bs.heat.radiator", "rad2").WithParam("pressureDrop", "1 kPa"),
            Pipe(r, "flow", "f0", ("b", "flow"), ("pu", "in")).WithParam("pressureDrop", "1000"),
            Pipe(r, "flow", "f1", ("pu", "out"), ("t1", "in")).WithParam("pressureDrop", "2000"),
            Pipe(r, "flow", "f2", ("f1", null), ("t2", "in")).WithParam("pressureDrop", "3000"),
            Pipe(r, "flow", "c1", ("t1", "out"), ("rad1", "flow")),
            Pipe(r, "flow", "c2", ("t2", "out"), ("rad2", "flow")),
            Pipe(r, "return", "r1", ("rad1", "return"), ("b", "return")).WithParam("pressureDrop", "2000"),
            Pipe(r, "return", "r2", ("rad2", "return"), ("r1", null)).WithParam("pressureDrop", "3000"));

        [Fact]
        public void PumpSumsTheIndexCircuit()
        {
            // Circuit 1: 2 + 8 + 1 + 2 + 5 + 1 = 19 kPa; circuit 2: 2 + 3 + 1 + 1 + 3 + 2 + 5 + 1 = 18 kPa → ΣΔp = 19 kPa,
            // H_req = 19 kPa/(1000 kg/m³·g) = 1.94 m.
            var r = Fx.Reg();
            var doc = TwoRadiators(r, "8 kPa", "2 m");
            var c = BsCircuits.Of(doc, doc.Get("pu")!)!;
            Assert.Equal(2, c.Paths.Count);
            Assert.Equal(new[] { "rad1", "rad2" }, c.Consumers.OrderBy(x => x).ToArray());
            var t = Solve(doc, r);
            Assert.Equal(19000, Val(t, "pu", "dpC"), 6);
            Assert.Equal(19000 / (1000 * G), Val(t, "pu", "Hreq"), 9);
            Assert.Equal(Provenance.Derived, t.Get("pu", "dpC")!.Provenance);
            Assert.Empty(t.Conflicts);
            Assert.DoesNotContain(Checks(r, doc), f => f.CheckId is "heat.pump.head" or "heat.circuit.balance");
        }

        [Fact]
        public void TooWeakPumpGetsTheHeadWarning()
        {
            var r = Fx.Reg();
            var findings = Checks(r, TwoRadiators(r, "8 kPa", "1,5 m"));
            Assert.Contains(findings, f => f.CheckId == "heat.pump.head" && f.ObjectId == "pu" && f.Severity == CheckSeverity.Warning);
        }

        [Fact]
        public void UnbalancedCircuitsGetTheBalanceHint()
        {
            // TRV 1 at 30 kPa: circuit 1 = 41 kPa, circuit 2 = 18 kPa < 75 % of 41 kPa.
            var r = Fx.Reg();
            var doc = TwoRadiators(r, "30 kPa", "5 m");
            Assert.Equal(41000, Val(Solve(doc, r), "pu", "dpC"), 6);
            var findings = Checks(r, doc);
            Assert.Contains(findings, f => f.CheckId == "heat.circuit.balance" && f.ObjectId == "pu" && f.Severity == CheckSeverity.Hint);
            Assert.DoesNotContain(findings, f => f.CheckId == "heat.pump.head");
        }

        [Fact]
        public void UnknownCountedDropLeavesTheSumOpen()
        {
            // f1 gets a length but no flow (a branched trunk takes no flow from its net): its Δp, and so the sum, stays unknown.
            var r = Fx.Reg();
            var doc = TwoRadiators(r, "8 kPa", "2 m");
            doc = Edits.Update(doc, new[] { "f1" }, o => o.WithParam("pressureDrop", "").WithParam("length", "10 m"));
            var t = Solve(doc, r);
            Assert.Null(t.Get("pu", "dpC")!.Value);
            Assert.DoesNotContain(Checks(r, doc), f => f.CheckId is "heat.pump.head" or "heat.circuit.balance");
        }

        [Fact]
        public void PumpWithoutAClosedCircuitHasNoSum()
        {
            var r = Fx.Reg();
            var doc = Doc(Make(r, "bs.heat.pump", "pu").WithParam("head", "2 m"),
                Make(r, "bs.heat.radiator", "rad").WithParam("pressureDrop", "1 kPa"),
                Pipe(r, "flow", "f", ("pu", "out"), ("rad", "flow")).WithParam("pressureDrop", "500"));
            Assert.Null(BsCircuits.Of(doc, doc.Get("pu")!));
            Assert.Null(Solve(doc, r).Get("pu", "dpC")!.Value);
        }

        [Fact]
        public void ManifoldCircuitsRunThroughEachLoop()
        {
            // Boiler → pump → manifold → loop 1 (20 kPa) or loop 2 (10 kPa) → manifold → boiler: ΣΔp = 20 kPa, and 10 kPa is
            // below 75 % of it.
            var r = Fx.Reg();
            var doc = Doc(
                Make(r, "bs.heat.boiler", "b"),
                Make(r, "bs.heat.pump", "pu"),
                Make(r, "bs.heat.manifold", "hkv"),
                Make(r, "bs.heat.floorloop", "l1").WithParam("pressureDrop", "20 kPa"),
                Make(r, "bs.heat.floorloop", "l2").WithParam("pressureDrop", "10 kPa"),
                Pipe(r, "flow", "f0", ("b", "flow"), ("pu", "in")),
                Pipe(r, "flow", "f1", ("pu", "out"), ("hkv", "flow")),
                Pipe(r, "flow", "a1", ("hkv", "f1"), ("l1", "flow")),
                Pipe(r, "return", "b1", ("l1", "return"), ("hkv", "r1")),
                Pipe(r, "flow", "a2", ("hkv", "f2"), ("l2", "flow")),
                Pipe(r, "return", "b2", ("l2", "return"), ("hkv", "r2")),
                Pipe(r, "return", "r0", ("hkv", "return"), ("b", "return")));
            var t = Solve(doc, r);
            Assert.Equal(20000, Val(t, "pu", "dpC"), 6);
            Assert.Contains(Checks(r, doc), f => f.CheckId == "heat.circuit.balance");
        }

        [Fact]
        public void ReversedRadiatorIsNotACircuit()
        {
            // The water would have to flow from the radiator's return to its flow port: no circuit through it.
            var r = Fx.Reg();
            var doc = Doc(
                Make(r, "bs.heat.boiler", "b").WithParam("pressureDrop", "5 kPa"),
                Make(r, "bs.heat.pump", "pu"),
                Make(r, "bs.heat.radiator", "rad").WithParam("pressureDrop", "1 kPa"),
                Pipe(r, "flow", "f0", ("b", "flow"), ("pu", "in")),
                Pipe(r, "flow", "f1", ("pu", "out"), ("rad", "return")),
                Pipe(r, "return", "r1", ("rad", "flow"), ("b", "return")));
            Assert.Null(BsCircuits.Of(doc, doc.Get("pu")!));
        }

        [Fact]
        public void NewLossValuesRoundTripThroughJson()
        {
            var r = Fx.Reg();
            var doc = TwoRadiators(r, "8 kPa", "2 m");
            doc = Edits.Update(doc, new[] { "t2" }, o => o.WithParam("kvValue", "0,8 m³/h"));
            doc = Edits.Update(doc, new[] { "f2" }, o => o.WithParam("pressureDropSingle", "1500"));
            var back = DocumentJson.Deserialize(DocumentJson.Serialize(doc));
            Assert.Equal("0,8 m³/h", back.Get("t2")!.Params["kvValue"].Text);
            Assert.Equal("1500", back.Get("f2")!.Params["pressureDropSingle"].Text);
            // The entered single losses of f2 count: circuit 2 = 18 + 1.5 = 19.5 kPa is now the index circuit.
            Assert.Equal(19500, Val(Solve(back, r), "pu", "dpC"), 6);
            Assert.Equal(Val(Solve(doc, r), "pu", "Hreq"), Val(Solve(back, r), "pu", "Hreq"), 12);
        }
    }
}
