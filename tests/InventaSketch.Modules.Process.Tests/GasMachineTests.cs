using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using InventaSketch.Modules.Energy;
using InventaSketch.Modules.Machines;
using InventaSketch.Modules.Mechanical;
using Xunit;

namespace InventaSketch.Modules.Process.Tests
{
    /// <summary>
    /// Gas values of compressors and turbines (ideal gas, constant κ): pressure ratio, isentropic and actual outlet temperature,
    /// power P = ṁ·c_p·ΔT, volume flows, the gas choice, the checks, the values passed along process lines, and the shaft
    /// power that reaches a driving motor or a driven generator (and from there an energy cable). Every expected value is a
    /// hand calculation from the textbook formulas.
    /// </summary>
    public class GasMachineTests
    {
        private const double C0 = 273.15;
        private const double RAir = 287.0;
        private const double CpAir = 1.4 * RAir / 0.4; // 1004.5 J/(kg·K)

        private static Registry Reg() => new Registry().Register(new ProcessModule()).Register(new MechanicalModule()).Register(new MachinesModule()).Register(new EnergyModule());

        private static readonly RenderContext Empty = new(SketchDocument.Create());

        private static SketchDocument Doc(params SketchObject[] objs)
        {
            var doc = SketchDocument.Create(PageSizes.Slide16x9, "Gas");
            return Edits.Add(doc, doc.Pages[0].Id, objs);
        }

        private static SketchObject Make(Registry r, string item, string id, double x = 0, double y = 0) => r.Create(r.Item(item)!, new Vec2(x, y)) with { Id = id };

        private static SketchObject Line(Registry r, string type, string id, (string Obj, string Port) a, (string Obj, string Port) b)
            => r.Create(r.Item(type)!, Vec2.Zero) with
            {
                Id = id,
                Points = new[] { new Vec2(0, 0), new Vec2(10, 0) },
                Attach = new Attachment?[] { new Attachment(a.Obj, 0, 0) { AnchorId = a.Port }, new Attachment(b.Obj, 0, 0) { AnchorId = b.Port } },
            };

        private static ValueTable Solve(SketchDocument doc, Registry r, IEnumerable<string>? ids = null) => ValueSolver.Solve(doc, r, ids, new ValueSolveOptions { BudgetMs = 10000 });

        private static double Val(ValueTable t, string id, string key) => t.Get(id, key)!.Value!.Value;

        private static IReadOnlyList<CheckFinding> Checks(Registry r, SketchDocument doc) => ValueSolver.Checks(Solve(doc, r), r, new RenderContext(doc));

        private static Anchor HubOf(Registry r, SketchObject o) => r.PageAnchors(o, Empty).Single(a => a.Role == AnchorRole.Hub);

        private static Vec2 ShaftEnd(Registry r, SketchObject drive) => r.PageAnchors(drive, Empty).Single(a => a.Role == AnchorRole.Axis).Q;

        private static SketchObject Dock(Registry r, SketchObject part, Vec2 at)
        {
            var d = at - HubOf(r, part).P;
            return part with { Transform = part.Transform with { X = part.Transform.X + d.X, Y = part.Transform.Y + d.Y } };
        }

        // ------------------------------------------------------------------ formulas

        [Fact]
        public void AirCompressorOutletTemperatureAndPowerFromRatioAndEfficiency()
        {
            var r = Reg();
            var k = Make(r, "process.compressor", "k").WithParam("pressureRatio", "4").WithParam("efficiency", "80 %").WithParam("massFlow", "1 kg/s");
            var t = Solve(Doc(k), r);
            Assert.Empty(t.Conflicts);
            // Ambient suction by default: T₁ = 293.15 K, p₁ = 1.01325 bar; air κ = 1.4, R = 287 J/(kg·K), c_p = 1004.5 J/(kg·K).
            Assert.Equal(Provenance.Default, t.Get("k", "th1")!.Provenance);
            Assert.Equal(CpAir, Val(t, "k", "cp"), 9);
            // T₂s = T₁·π^((κ−1)/κ) = 293.15 · 4^0.2857 = 435.62 K; T₂ = T₁ + (T₂s − T₁)/η_s = 471.24 K (198.1 °C).
            var t1 = 20 + C0;
            var t2s = t1 * Math.Pow(4, 0.4 / 1.4);
            var t2 = t1 + (t2s - t1) / 0.8;
            Assert.Equal(t2s, Val(t, "k", "th2s"), 6);
            Assert.Equal(t2, Val(t, "k", "th2"), 6);
            Assert.Equal(t2 - t1, Val(t, "k", "dT"), 6);
            // P = ṁ·c_p·ΔT = 1 · 1004.5 · 178.09 = 178.9 kW; the isentropic power P_s = ṁ·c_p·(T₂s − T₁) = η_s·P.
            Assert.Equal(CpAir * (t2 - t1), Val(t, "k", "P"), 4);
            Assert.Equal(0.8 * Val(t, "k", "P"), Val(t, "k", "Ps"), 4);
            // p₂ = π·p₁ = 4.053 bar; V̇₁ = ṁ·R·T₁/p₁ = 287 · 293.15/101325 = 0.8303 m³/s; V̇₂ = ṁ·R·T₂/p₂.
            Assert.Equal(4 * 101325, Val(t, "k", "p2"), 6);
            Assert.Equal(RAir * t1 / 101325, Val(t, "k", "V1"), 9);
            Assert.Equal(RAir * t2 / (4 * 101325), Val(t, "k", "V2"), 9);
        }

        [Fact]
        public void CompressorEfficiencyFollowsFromAMeasuredOutletTemperature()
        {
            var r = Reg();
            var k = Make(r, "process.compressor", "k").WithParam("pressureIn", "1 bar").WithParam("pressureOut", "3 bar").WithParam("tempIn", "15 °C").WithParam("tempOut", "160 °C");
            var t = Solve(Doc(k), r);
            Assert.Empty(t.Conflicts);
            // π = 3; T₂s = 288.15 · 3^0.2857 = 394.41 K; η_s = (T₂s − T₁)/(T₂ − T₁) = 106.26/145 = 73.3 %.
            var t1 = 15 + C0;
            var t2s = t1 * Math.Pow(3, 0.4 / 1.4);
            Assert.Equal(3, Val(t, "k", "pi"), 9);
            Assert.Equal((t2s - t1) / 145.0, Val(t, "k", "eta"), 9);
        }

        [Fact]
        public void GasTurbineExpansionWorkAndOutletTemperature()
        {
            var r = Reg();
            var tb = Make(r, "process.turbine", "tb").WithParam("tempIn", "900 °C").WithParam("pressureIn", "10 bar").WithParam("pressureOut", "1 bar")
                .WithParam("efficiency", "88 %").WithParam("massFlow", "5 kg/s");
            var t = Solve(Doc(tb), r);
            Assert.Empty(t.Conflicts);
            // π = p₁/p₂ = 10; T₂s = T₁·π^(−(κ−1)/κ) = 1173.15/10^0.2857 = 607.63 K; T₂ = T₁ − η_s·(T₁ − T₂s) = 675.49 K.
            var t1 = 900 + C0;
            var t2s = t1 * Math.Pow(10, -0.4 / 1.4);
            var t2 = t1 - 0.88 * (t1 - t2s);
            Assert.Equal(10, Val(t, "tb", "pi"), 9);
            Assert.Equal(t2s, Val(t, "tb", "th2s"), 6);
            Assert.Equal(t2, Val(t, "tb", "th2"), 6);
            // Expansion work per second P = ṁ·c_p·(T₁ − T₂) = 5 · 1004.5 · 497.66 = 2.50 MW; P_s = P/η_s.
            Assert.Equal(5 * CpAir * (t1 - t2), Val(t, "tb", "P"), 3);
            Assert.Equal(Val(t, "tb", "P") / 0.88, Val(t, "tb", "Ps"), 3);
            // No defaults on the turbine inlet: nothing is assumed for a turbine without values.
            var bare = Solve(Doc(Make(r, "process.turbine", "x")), r);
            Assert.Null(bare.Get("x", "th1")!.Value);
            Assert.Null(bare.Get("x", "p1")!.Value);
        }

        [Fact]
        public void TheGasChoiceSetsKappaAndRAndAnEnteredValueWins()
        {
            var r = Reg();
            var he = Make(r, "process.compressor", "he").WithParam(ProcessValues.GasKey, "helium");
            var t = Solve(Doc(he), r);
            Assert.Empty(t.Conflicts);
            Assert.Equal(1.667, Val(t, "he", "kappa"), 9);
            Assert.Equal(2076.9, Val(t, "he", "R"), 9);
            Assert.Equal(1.667 * 2076.9 / 0.667, Val(t, "he", "cp"), 6);
            // Every gas of the choice has its κ and R (textbook ideal-gas values at about 300 K).
            foreach (var gas in ProcessValues.Gases)
            {
                var o = Make(r, "process.turbine", "g").WithParam(ProcessValues.GasKey, gas.Id);
                var tg = Solve(Doc(o), r);
                Assert.Equal(gas.Kappa, Val(tg, "g", "kappa"), 9);
                Assert.Equal(gas.R, Val(tg, "g", "R"), 9);
                Assert.InRange(gas.Kappa, 1.28, 1.67);
            }
            // An entered κ takes precedence over the table without a conflict; R still comes from the gas.
            var own = Make(r, "process.compressor", "own").WithParam("kappa", "1.3");
            var to = Solve(Doc(own), r);
            Assert.Empty(to.Conflicts);
            Assert.Equal(1.3, Val(to, "own", "kappa"), 9);
            Assert.Equal(Provenance.Entered, to.Get("own", "kappa")!.Provenance);
            Assert.Equal(RAir, Val(to, "own", "R"), 9);
            // "Other gas": κ and R only as entered (c_p entered directly works too).
            var other = Make(r, "process.compressor", "o").WithParam(ProcessValues.GasKey, ProcessValues.CustomGas).WithParam("specificHeat", "2 kJ/(kg·K)");
            var tc = Solve(Doc(other), r);
            Assert.Null(tc.Get("o", "kappa")!.Value);
            Assert.Equal(2000, Val(tc, "o", "cp"), 9);
        }

        [Fact]
        public void GasDocumentSurvivesTheJsonRoundTrip()
        {
            var r = Reg();
            var k = Make(r, "process.compressor", "k").WithParam(ProcessValues.GasKey, "co2").WithParam("pressureRatio", "2.5").WithParam("tempMax", "180 °C");
            var doc = Doc(k);
            var back = DocumentJson.Deserialize(DocumentJson.Serialize(doc));
            Assert.Equal(DocumentJson.Serialize(doc), DocumentJson.Serialize(back));
            Assert.Equal("co2", back.Get("k")!.Str(ProcessValues.GasKey, "air"));
            Assert.Equal(1.289, Val(Solve(back, r), "k", "kappa"), 9);
        }

        // ------------------------------------------------------------------ checks

        [Fact]
        public void ChecksFlagEfficiencyRatioTemperatureLimitAndKappa()
        {
            var r = Reg();
            string[] Ids(SketchObject o) => Checks(r, Doc(o)).Select(f => f.CheckId).OrderBy(x => x).ToArray();

            Assert.Empty(Ids(Make(r, "process.compressor", "k").WithParam("pressureRatio", "4").WithParam("efficiency", "80 %")));
            Assert.Contains("process.efficiency", Ids(Make(r, "process.compressor", "k").WithParam("efficiency", "120 %")));
            // An outlet colder than the isentropic one means η_s > 1.
            Assert.Contains("process.efficiency", Ids(Make(r, "process.compressor", "k").WithParam("pressureRatio", "4").WithParam("tempOut", "100 °C")));
            Assert.Contains("gas.ratio", Ids(Make(r, "process.compressor", "k").WithParam("pressureIn", "5 bar").WithParam("pressureOut", "2 bar")));
            Assert.Contains("gas.ratio", Ids(Make(r, "process.turbine", "t").WithParam("pressureIn", "1 bar").WithParam("pressureOut", "2 bar")));
            // π = 8, η_s = 80 %: T₂ = 293.15 + (293.15·8^0.2857 − 293.15)/0.8 = 589.8 K (316.7 °C) > 250 °C.
            var hot = Make(r, "process.compressor", "k").WithParam("pressureRatio", "8").WithParam("efficiency", "80 %").WithParam("tempMax", "250 °C");
            var f = Checks(r, Doc(hot)).Single();
            Assert.Equal("gas.temperature", f.CheckId);
            Assert.Equal(CheckSeverity.Warning, f.Severity);
            Assert.Empty(Ids(hot.WithParam("tempMax", "350 °C")));
            Assert.Contains("gas.temperature", Ids(Make(r, "process.turbine", "t").WithParam("tempIn", "1400 °C").WithParam("tempMax", "1250 °C")));
            Assert.Contains("gas.kappa", Ids(Make(r, "process.compressor", "k").WithParam(ProcessValues.GasKey, ProcessValues.CustomGas).WithParam("kappa", "0.9")));
            Assert.Contains("process.efficiency", Ids(Make(r, "process.generator", "g").WithParam("efficiency", "105 %")));
        }

        // ------------------------------------------------------------------ nets and shafts

        [Fact]
        public void CompressorOutletFeedsTheTurbineInletAlongAProcessLine()
        {
            var r = Reg();
            var k = Make(r, "process.compressor", "k").WithParam("pressureRatio", "10").WithParam("efficiency", "85 %").WithParam("massFlow", "2 kg/s");
            var tb = Make(r, "process.turbine", "tb").WithParam("pressureOut", "1.01325 bar").WithParam("efficiency", "90 %");
            var doc = Doc(k, tb, Line(r, "process.line", "l1", ("k", "D"), ("tb", "I")));
            var t = Solve(doc, r);
            Assert.Empty(t.Conflicts);
            // The turbine takes ṁ, θ and p from the compressor outlet; the line shares p only between the two nozzles.
            Assert.Equal(2, Val(t, "tb", "m"), 9);
            Assert.Equal(Val(t, "k", "th2"), Val(t, "tb", "th1"), 9);
            Assert.Equal(Provenance.Linked, t.Get("tb", "p1")!.Provenance);
            Assert.Equal(10 * 101325, Val(t, "tb", "p1"), 6);
            Assert.Equal(10, Val(t, "tb", "pi"), 9);
            // The volume flow is not passed (it changes with p and T): each machine has its own from ṁ·R·T/p.
            Assert.Equal(Val(t, "k", "V2"), Val(t, "tb", "V1"), 9);
            Assert.Equal(Provenance.Derived, t.Get("tb", "V1")!.Provenance);
            // Without heat input between them the turbine gives back less than the compressor takes.
            Assert.True(Val(t, "tb", "P") < Val(t, "k", "P"));
        }

        [Fact]
        public void APressureIsNotPassedThroughAValve()
        {
            var r = Reg();
            var k = Make(r, "process.compressor", "k").WithParam("pressureOut", "6 bar").WithParam("massFlow", "1 kg/s");
            var v = Make(r, "process.valve.control", "v");
            var tb = Make(r, "process.turbine", "tb");
            var doc = Doc(k, v, tb, Line(r, "process.line", "l1", ("k", "D"), ("v", "1")), Line(r, "process.line", "l2", ("v", "2"), ("tb", "I")));
            var t = Solve(doc, r);
            Assert.Empty(t.Conflicts);
            Assert.Equal(1, Val(t, "tb", "m"), 9);
            Assert.Null(t.Get("tb", "p1")!.Value);
        }

        [Fact]
        public void AMotorDrivingACompressorSeesItsPowerDemandWithoutASpeed()
        {
            var r = Reg();
            var motor = r.WithView(r.Type("machines.drive.motor")!.Create(new Vec2(40, 60)) with { Id = "motor" }, "symbol", Empty).WithParam("efficiency", "95 %");
            var k = Dock(r, Make(r, "process.compressor", "k"), ShaftEnd(r, motor))
                .WithParam("pressureRatio", "4").WithParam("efficiency", "80 %").WithParam("massFlow", "1 kg/s");
            var doc = Doc(motor, k);
            Assert.Contains(Coupling.Graph(r, doc), l => l.Kind == LinkKind.Carried && l.A == "motor" && l.B == "k");
            var t = Solve(doc, r, new[] { "k" });
            Assert.Empty(t.Conflicts);
            var p = Val(t, "k", "P");
            Assert.Equal(p, Val(t, "motor", "P"), 6);
            Assert.Equal(Provenance.Linked, t.Get("motor", "P")!.Provenance);
            // The machines motor draws P_el = P/η = 178.9 kW/0.95 = 188.3 kW.
            Assert.Equal(p / 0.95, Val(t, "motor", "Pel"), 4);
            // With a speed the torque follows as well: T = P/(2π·n).
            var fast = Solve(Doc(motor.WithParam("speed", "3000 rpm"), k), r);
            Assert.Empty(fast.Conflicts);
            Assert.Equal(p / (2 * Math.PI * 50), Val(fast, "k", "T"), 6);
        }

        [Fact]
        public void ATurbineDrivesAGeneratorThatFeedsAnEnergyCable()
        {
            var r = Reg();
            var tb = Make(r, "process.turbine", "tb", 100, 40).WithParam("tempIn", "900 °C").WithParam("pressureIn", "10 bar").WithParam("pressureOut", "1 bar")
                .WithParam("efficiency", "88 %").WithParam("massFlow", "5 kg/s");
            var g = Dock(r, Make(r, "process.generator", "g"), ShaftEnd(r, tb)).WithParam("efficiency", "98 %");
            Assert.True(g.Transform.Y > 40, "generator below the turbine (its hub is on top)");
            var meter = Make(r, "energy.grid.meter", "meter", 200, 120);
            var doc = Doc(tb, g, meter, Line(r, "energy.line.cable", "c1", ("g", "el"), ("meter", "a")));
            Assert.Contains(Coupling.Graph(r, doc), l => l.Kind == LinkKind.Carried && l.A == "tb" && l.B == "g");
            var t = Solve(doc, r, new[] { "tb" });
            Assert.Empty(t.Conflicts);
            // Turbine P = 5 · 1004.5 · (T₁ − T₂) = 2.50 MW goes to the generator shaft; P_el = 0.98 · P = 2.45 MW at the meter.
            var p = Val(t, "tb", "P");
            Assert.Equal(p, Val(t, "g", "P"), 6);
            Assert.Equal(0.98 * p, Val(t, "g", "Pel"), 3);
            Assert.Equal(0.98 * p, Val(t, "meter", "P"), 3);
            Assert.Equal("net.cross.P", t.Get("meter", "P")!.Via!.Relation.Id);
        }

        [Fact]
        public void TheProcessMotorShowsItsElectricalInput()
        {
            var r = Reg();
            var m = Make(r, "process.motor", "m").WithParam("power", "11 kW").WithParam("efficiency", "92 %");
            var t = Solve(Doc(m), r);
            Assert.Equal(11000 / 0.92, Val(t, "m", "Pel"), 6);
        }

        [Fact]
        public void TheGeneratorHasAShaftHubAndAPowerTerminal()
        {
            var r = Reg();
            var g = Make(r, "process.generator", "g");
            var t = r.TypeOf(g);
            var anchors = t.Anchors(g, Empty).ToList();
            Assert.Single(anchors, a => a.Role == AnchorRole.Hub);
            Assert.DoesNotContain(anchors, a => a.Role == AnchorRole.Axis);
            var el = Assert.Single(anchors, a => a.Role == AnchorRole.Port);
            Assert.Equal("el", el.Id);
            var c = t.Connection(g, el)!;
            Assert.Equal(PortMedia.ElectricAc, c.Medium);
            Assert.Equal("energy.line.cable", c.Connector);
            Assert.False(t.DrivesShaft);
        }
    }
}
