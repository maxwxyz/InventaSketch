using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Machines.Tests
{
    /// <summary>
    /// Calc 3 machines: synchronous speed, slip and power of the machine cross-sections, the drive-block chain motor →
    /// gearbox → differential → wheel, a drivetrain e-machine driving a mechanical gear stage, and the checks. Every expected
    /// value is a hand calculation.
    /// </summary>
    public class ValueTests
    {
        private const double RpmToSi = 2 * Math.PI / 60;

        private static double Rpm(ValueTable t, string id, string key = "n") => t.Get(id, key)!.Value!.Value / RpmToSi;
        private static double Val(ValueTable t, string id, string key) => t.Get(id, key)!.Value!.Value;

        /// <summary>Solves the first page with a generous time budget (the first solve also builds the topology and JITs).</summary>
        private static ValueTable Solve(SketchDocument doc, Registry r) => ValueSolver.Solve(doc, r, null, new ValueSolveOptions { BudgetMs = 10000 });

        private static SketchObject Make(Registry r, string item, string id, double x, double y)
            => r.Create(r.Item(item)!, new Vec2(x, y)) with { Id = id };

        private static SketchDocument Doc(params SketchObject[] objs)
        {
            var doc = SketchDocument.Create(PageSizes.Slide16x9, "Values");
            return Edits.Add(doc, doc.Pages[0].Id, objs);
        }

        /// <summary>A connector from the right edge of <paramref name="from"/> to the left edge of <paramref name="to"/> (the first docked end is upstream).</summary>
        private static SketchObject Arrow(string id, string from, string to) => new()
        {
            Id = id,
            Type = "ann.arrow",
            Points = new[] { new Vec2(0, 0), new Vec2(10, 0) },
            Attach = new Attachment?[] { new Attachment(from, 0.5, 0) { AnchorId = "e" }, new Attachment(to, -0.5, 0) { AnchorId = "w" } },
        };

        private static IReadOnlyList<CheckFinding> Checks(Registry r, SketchDocument doc)
            => ValueSolver.Checks(Solve(doc, r), r, new RenderContext(doc));

        // ------------------------------------------------------------------ declarations

        [Fact]
        public void MachinesAndDriveBlocksDeclareBilingualVariables()
        {
            var r = Fx.Reg();
            foreach (var id in new[] { "machines.radial", "machines.axial", "machines.drive.motor", "machines.drivetrain.emachine", "machines.drivetrain.engine", "machines.drive.gearbox", "machines.drive.differential", "machines.drive.wheel" })
            {
                var t = r.Type(id)!;
                Assert.True(ValueModel.HasValues(t), id);
                Assert.True(ValueModel.Variables(t).Count(v => v.Relevance == VarRelevance.Card) <= 4, id);
                foreach (var v in ValueModel.Variables(t))
                {
                    Assert.False(string.IsNullOrEmpty(v.Name.De), id + "." + v.Key);
                    Assert.Contains(t.Parameters, p => p.Key == v.ParamKey);
                }
                Assert.All(ValueModel.Relations(t), rel => Assert.NotNull(rel.Source));
            }
            Assert.Equal(new[] { "i", "eta", "n2", "T2" }, ValueModel.Variables(r.Type("machines.drive.gearbox")).Where(v => v.Relevance == VarRelevance.Card).Select(v => v.Key).ToArray());
            Assert.False(ValueModel.HasValues(r.Type("machines.part.stator")));
        }

        [Fact]
        public void EveryItemStillInsertsWithoutValuesAndShowsNoCheck()
        {
            var r = DrivetrainTests.Both();
            var objs = r.Library.Select((item, i) => r.Create(item, new Vec2(i % 12 * 300, i / 12 * 300)) with { Id = "o" + i }).ToArray();
            var doc = Doc(objs);
            var table = ValueSolver.Solve(doc, r, null, new ValueSolveOptions { BudgetMs = 5000 });
            Assert.Empty(table.Conflicts);
            Assert.Empty(ValueSolver.Checks(table, r, new RenderContext(doc)));
        }

        // ------------------------------------------------------------------ machines

        [Fact]
        public void InductionMachineSynchronousSpeedSlipAndPower()
        {
            var r = Fx.Reg();
            var doc = Doc(Make(r, "machines.radial.asm", "m", 0, 0)
                .WithParam("poles", 4).WithParam("frequency", "50 Hz").WithParam("speed", "1440 rpm").WithParam("torque", "50 N·m").WithParam("efficiency", "90 %"));
            var t = Solve(doc, r);
            Assert.Empty(t.Conflicts);
            // n_s = 60·f/p = 60·50/2 = 1500 min⁻¹; s = (1500 − 1440)/1500 = 4 %; P = 2π·24 s⁻¹·50 N·m = 7539.8 W; P_el = P/η.
            Assert.Equal(1500, Rpm(t, "m", "ns"), 9);
            Assert.Equal(0.04, Val(t, "m", "s"), 12);
            Assert.Equal(2 * Math.PI * 24 * 50, Val(t, "m", "P"), 6);
            Assert.Equal(2 * Math.PI * 24 * 50 / 0.9, Val(t, "m", "Pel"), 6);
            Assert.Empty(Checks(r, doc));
        }

        [Fact]
        public void FrequencyFromSpeedAndPoles()
        {
            var r = Fx.Reg();
            // PMSM with 8 poles at 3000 min⁻¹ synchronous: f = n_s·p/60 = 3000·4/60 = 200 Hz.
            var doc = Doc(Make(r, "machines.radial.spm", "m", 0, 0).WithParam("poles", 8).WithParam("syncSpeed", "3000 rpm"));
            Assert.Equal(200, Val(Solve(doc, r), "m", "f"), 9);
        }

        [Fact]
        public void InductionMotorAboveSynchronousSpeedGivesTheSlipInfo()
        {
            var r = Fx.Reg();
            var doc = Doc(Make(r, "machines.radial.asm", "m", 0, 0).WithParam("poles", 4).WithParam("frequency", "50 Hz").WithParam("speed", "1550 rpm"));
            Assert.Contains(Checks(r, doc), f => f.CheckId == "mach.slip.sign" && f.Severity == CheckSeverity.Info && f.Source == "S15");
        }

        [Fact]
        public void RatingAndEfficiencyChecks()
        {
            var r = Fx.Reg();
            var over = Doc(Make(r, "machines.block.motor", "m", 0, 0).WithParam("speed", "3000 rpm").WithParam("torque", "100 N·m").WithParam("ratedPower", "25 kW"));
            // P = 2π·50 s⁻¹·100 N·m = 31.4 kW > 25 kW.
            Assert.Contains(Checks(r, over), f => f.CheckId == "mach.overload" && f.Source == "S14");
            var ok = Doc(Make(r, "machines.block.motor", "m", 0, 0).WithParam("speed", "3000 rpm").WithParam("torque", "100 N·m").WithParam("ratedPower", "40 kW"));
            Assert.DoesNotContain(Checks(r, ok), f => f.CheckId == "mach.overload");
            var eta = Doc(Make(r, "machines.block.gearbox", "g", 0, 0).WithParam("efficiency", "105 %"));
            Assert.Contains(Checks(r, eta), f => f.CheckId == "mach.efficiency" && f.Severity == CheckSeverity.Warning);
            var power = Doc(Make(r, "machines.block.motor", "m", 0, 0).WithParam("power", "10 kW").WithParam("powerEl", "9 kW"));
            Assert.Contains(Checks(r, power), f => f.CheckId == "mach.efficiency");
        }

        // ------------------------------------------------------------------ drive-block chain

        [Fact]
        public void DriveBlockChainPassesSpeedAndTorqueFromMotorToWheel()
        {
            var r = Fx.Reg();
            var doc = Doc(
                Make(r, "machines.block.motor", "mot", 0, 0).WithParam("speed", "3000 rpm").WithParam("torque", "100 N·m"),
                Make(r, "machines.block.gearbox", "gb", 50, 0).WithParam("ratio", "8").WithParam("efficiency", "97 %"),
                Make(r, "machines.block.differential", "diff", 100, 0).WithParam("ratio", "3.5").WithParam("efficiency", "98 %"),
                Make(r, "machines.wheel", "wh", 150, 0).WithParam("radius", "300 mm"),
                Arrow("a1", "mot", "gb"), Arrow("a2", "gb", "diff"), Arrow("a3", "diff", "wh"));
            var t = Solve(doc, r);
            Assert.Empty(t.Conflicts);
            // Gearbox: n₂ = 3000/8 = 375 min⁻¹, T₂ = 100·8·0.97 = 776 N·m.
            Assert.Equal(3000, Rpm(t, "gb", "n1"), 9);
            Assert.Equal(375, Rpm(t, "gb", "n2"), 9);
            Assert.Equal(776, Val(t, "gb", "T2"), 9);
            // Differential: n₂ = 375/3.5 = 107.14 min⁻¹, T₂ = 776·3.5·0.98 = 2661.68 N·m.
            Assert.Equal(375 / 3.5, Rpm(t, "diff", "n2"), 9);
            Assert.Equal(2661.68, Val(t, "diff", "T2"), 9);
            // Wheel: v = 2π·n·r = 2π·(107.14/60) s⁻¹·0.3 m = 3.366 m/s; F = T/r = 8872.27 N.
            Assert.Equal(2 * Math.PI * (375 / 3.5 / 60) * 0.3, Val(t, "wh", "v"), 9);
            Assert.Equal(2661.68 / 0.3, Val(t, "wh", "F"), 6);
            // Power balance: P_wheel = P_motor·0.97·0.98.
            Assert.Equal(Val(t, "mot", "P") * 0.97 * 0.98, Val(t, "wh", "P"), 6);
            Assert.Equal(Provenance.Linked, t.Get("wh", "n")!.Provenance);
            Assert.Empty(ValueSolver.Checks(t, r, new RenderContext(doc)));
        }

        [Fact]
        public void ConnectorDirectionDecidesUpstream()
        {
            var r = Fx.Reg();
            // Reversed connector: the gearbox output would feed the motor, which has no mechanical input: no link.
            var doc = Doc(
                Make(r, "machines.block.motor", "mot", 0, 0).WithParam("speed", "3000 rpm"),
                Make(r, "machines.block.gearbox", "gb", 50, 0).WithParam("ratio", "8"),
                Arrow("a1", "gb", "mot"));
            var t = Solve(doc, r);
            Assert.Null(t.Get("gb", "n1")!.Value);
        }

        // ------------------------------------------------------------------ e-machine on a mechanical shaft

        [Fact]
        public void EMachineOnAShaftDrivesAGearStage()
        {
            var r = DrivetrainTests.Both();
            SketchObject M(string item, string id, double x, double y, double? w = null, double? h = null)
            {
                var o = Make(r, item, id, x, y);
                return w is null ? o : o with { Size = new Size2(w.Value, h!.Value) };
            }
            var doc = Doc(
                M("mech.shaft.line", "s1", 20, 0, 80, 8),
                M("mech.shaft.line", "s2", 20, 30, 80, 8),
                M("machines.emachine.stick", "em", -10, 0).WithParam("speed", "3000 rpm").WithParam("torque", "20 N·m"),
                M("mech.spur.stick", "g1", 40, 0, 8, 20).WithParam("teeth", 20),
                M("mech.spur.stick", "g2", 40, 30, 8, 40).WithParam("teeth", 40));
            var t = Solve(doc, r);
            Assert.Empty(t.Conflicts);
            // i = 40/20 = 2: n = 1500 min⁻¹, T = 40 N·m on the second shaft; P = 2π·50 s⁻¹·20 N·m = 6283.2 W.
            Assert.Equal(3000, Rpm(t, "s1"), 9);
            Assert.Equal(1500, Rpm(t, "s2"), 9);
            Assert.Equal(40, Val(t, "s2", "T"), 9);
            Assert.Equal(2 * Math.PI * 50 * 20, Val(t, "s2", "P"), 6);
        }
    }
}
