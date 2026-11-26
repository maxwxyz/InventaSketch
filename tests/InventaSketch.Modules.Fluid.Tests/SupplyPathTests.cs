using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Fluid.Tests
{
    /// <summary>
    /// Round 6 (2026-11-26): single losses of valves and lines (ζ, datasheet point), and the supply path of a cylinder: the
    /// pressure drops from the pump's P port to the cylinder summed, the pressure available at the cylinder and its checks.
    /// Every expected value is a hand calculation.
    /// </summary>
    public class SupplyPathTests
    {
        private const double Lpm = 1e-3 / 60;
        private const double Bar = 1e5;

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

        /// <summary>A line between two ends; an end (line id, null) docks on another line, a null end is free.</summary>
        private static SketchObject Line(Registry r, string id, (string, string?)? a, (string, string?)? b)
            => r.Create(r.Item("fluid.line")!, Vec2.Zero) with
            {
                Id = id,
                Points = new[] { new Vec2(0, 0), new Vec2(10, 0) },
                Attach = new[] { At(a), At(b) },
            };

        private static IReadOnlyList<CheckFinding> Checks(Registry r, SketchDocument doc) => ValueSolver.Checks(Solve(doc, r), r, new RenderContext(doc));

        // ------------------------------------------------------------------ single losses

        [Fact]
        public void LossTypesDeclareBilingualVariablesWithParametersAndSources()
        {
            var r = Fx.Reg();
            foreach (var id in new[] { "fluid.valve.directional", "fluid.valve.check", "fluid.valve.pilotcheck", "fluid.filter", "fluid.cooler", "fluid.heater" })
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
        }

        [Fact]
        public void CheckValveLossFromZetaWithItsLine()
        {
            // 20 l/min in 12 mm: v = 2.947 m/s; ζ = 2 → Δp = 2·870·v²/2 = 7.56 kPa.
            var r = Fx.Reg();
            var doc = Doc(Make(r, "fluid.check", "rv").WithParam("lossCoefficient", "2"),
                Line(r, "l", ("rv", "P"), null).WithParam("flowRate", "20 l/min").WithParam("innerDiameter", "12 mm"));
            var t = Solve(doc, r);
            var v = 4 * 20 * Lpm / (Math.PI * 0.012 * 0.012);
            Assert.Equal(v, Val(t, "rv", "v"), 9);
            Assert.Equal(2 * 870 * v * v / 2, Val(t, "rv", "dp"), 6);
            Assert.Empty(t.Conflicts);
        }

        [Fact]
        public void ValveLossFromTheDatasheetPoint()
        {
            // Datasheet 6 bar at 30 l/min; with 15 l/min: Δp = 6 bar·(15/30)² = 1.5 bar; 3 bar gives back 21.2 l/min.
            var r = Fx.Reg();
            var valve = Make(r, "fluid.valve.43", "v").WithParam("pressureDropNominal", "6 bar").WithParam("flowNominal", "30 l/min");
            Assert.Equal(1.5 * Bar, Val(Solve(Doc(valve.WithParam("flowRate", "15 l/min")), r), "v", "dp"), 6);
            Assert.Equal(30 * Math.Sqrt(0.5) * Lpm, Val(Solve(Doc(valve.WithParam("pressureDrop", "3 bar")), r), "v", "Q"), 12);
        }

        [Fact]
        public void LineSingleLossesFromTheSumOfLossCoefficients()
        {
            var r = Fx.Reg();
            var line = Make(r, "fluid.line", "l").WithParam("flowRate", "20 l/min").WithParam("innerDiameter", "12 mm").WithParam("lossCoefficient", "4");
            var v = 4 * 20 * Lpm / (Math.PI * 0.012 * 0.012);
            Assert.Equal(4 * 870 * v * v / 2, Val(Solve(Doc(line), r), "l", "dpz"), 6);
        }

        // ------------------------------------------------------------------ supply path

        /// <summary>
        /// Pump (10 cm³ · 1450 min⁻¹ = 14.5 l/min) P → l1 (10 mm, 2 m) → 4/3 valve P (datasheet 6 bar at 30 l/min); A → l3
        /// (1.5 bar entered) → cylinder A; B → l4 → cylinder B; T → tank.
        /// </summary>
        private static SketchDocument Circuit(Registry r, string pressure, string? force = null)
        {
            var cyl = Make(r, "fluid.cylinder.double", "cyl").WithParam("bore", "50 mm").WithParam("rodDiameter", "28");
            return Doc(
                Make(r, "fluid.pump", "pump").WithParam("displacement", "10 cm³").WithParam("speed", "1450 min⁻¹").WithParam("pressure", pressure),
                force is null ? cyl : cyl.WithParam("forceReq", force),
                Make(r, "fluid.valve.43", "valve").WithParam("pressureDropNominal", "6 bar").WithParam("flowNominal", "30 l/min"),
                Make(r, "fluid.tank", "tank"),
                Line(r, "l1", ("pump", "P"), ("valve", "P")).WithParam("innerDiameter", "10 mm").WithParam("length", "2 m"),
                Line(r, "l3", ("valve", "A"), ("cyl", "A")).WithParam("pressureDrop", "1,5 bar"),
                Line(r, "l4", ("valve", "B"), ("cyl", "B")),
                Line(r, "l5", ("valve", "T"), ("tank", "T")));
        }

        /// <summary>Hand calculation of the supply path's drops: laminar l1 (Re ≈ 670), the valve's datasheet point and l3.</summary>
        private static double SupplyDrops()
        {
            var q = 14.5 * Lpm;
            var v = 4 * q / (Math.PI * 0.01 * 0.01);
            var lambda = 64 / (v * 0.01 / 46e-6);
            var line = lambda * 2 / 0.01 * 870 * v * v / 2;
            var valve = 6 * Bar * Math.Pow(14.5 / 30, 2);
            return line + valve + 1.5 * Bar;
        }

        [Fact]
        public void CylinderSeesTheSupplyPressureMinusThePathLosses()
        {
            var r = Fx.Reg();
            var doc = Circuit(r, "100 bar");
            var c = FluidCircuits.Of(doc, doc.Get("cyl")!)!;
            Assert.Equal(("pump", "A"), (c.Supply, c.Port));
            Assert.Single(c.Paths);
            var t = Solve(doc, r);
            Assert.Empty(t.Conflicts);
            Assert.Equal(SupplyDrops(), Val(t, "cyl", "dpS"), 3);
            Assert.Equal(100 * Bar - SupplyDrops(), Val(t, "cyl", "pav"), 3);
            Assert.DoesNotContain(Checks(r, doc), f => f.CheckId is "fluid.circuit.losses" or "fluid.cyl.force.losses");
        }

        [Fact]
        public void ForceAfterTheLossesGetsItsWarning()
        {
            // 19.3 kN: 100 bar·A₁ = 19.63 kN is enough, but p_av·A₁ ≈ 18.9 kN is not.
            var r = Fx.Reg();
            var findings = Checks(r, Circuit(r, "100 bar", "19,3 kN"));
            Assert.DoesNotContain(findings, f => f.CheckId == "fluid.cyl.force");
            Assert.Contains(findings, f => f.CheckId == "fluid.cyl.force.losses" && f.ObjectId == "cyl" && f.Severity == CheckSeverity.Warning);
            // 25 kN is beyond the supply pressure itself: only the setting check speaks.
            var both = Checks(r, Circuit(r, "100 bar", "25 kN"));
            Assert.Contains(both, f => f.CheckId == "fluid.cyl.force");
            Assert.DoesNotContain(both, f => f.CheckId == "fluid.cyl.force.losses");
        }

        [Fact]
        public void LossesAboveTheSupplyPressureGetTheWarning()
        {
            var r = Fx.Reg();
            var doc = Circuit(r, "3 bar");
            Assert.True(Val(Solve(doc, r), "cyl", "pav") < 0);
            Assert.Contains(Checks(r, doc), f => f.CheckId == "fluid.circuit.losses" && f.ObjectId == "cyl");
        }

        [Fact]
        public void ReliefTeeDoesNotEnterTheSupplyPath()
        {
            // A relief valve on a tee from l1 ends there (it passes nothing); the path stays pump → valve → cylinder.
            var r = Fx.Reg();
            var basis = Circuit(r, "100 bar");
            var doc = Edits.Add(basis, basis.Pages[0].Id, Make(r, "fluid.relief", "relief").WithParam("setPressure", "160 bar"),
                Line(r, "l6", ("l1", null), ("relief", "P")).WithParam("pressureDrop", "5 bar"));
            var c = FluidCircuits.Of(doc, doc.Get("cyl")!)!;
            Assert.Single(c.Paths);
            Assert.DoesNotContain(c.Members, m => m.ObjectId == "l6");
        }

        [Fact]
        public void CylinderWithoutCountedLossesHasNoSupplyValues()
        {
            var r = Fx.Reg();
            var doc = Doc(Make(r, "fluid.pump", "pump").WithParam("pressure", "100 bar"), Make(r, "fluid.cylinder.single", "cyl"),
                Line(r, "l1", ("pump", "P"), ("cyl", "A")));
            var t = Solve(doc, r);
            Assert.Null(t.Get("cyl", "dpS")!.Value);
            Assert.Null(t.Get("cyl", "pav")!.Value);
        }
    }
}
