using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Machines.Tests
{
    /// <summary>
    /// Generator mode of the electric machines (parameter <c>mode</c>): a driven machine gives P_el = η·P; the motor rule
    /// P = η·P_el stays the default. Every expected value is a hand calculation.
    /// </summary>
    public class GeneratorModeTests
    {
        private const double RpmToSi = 2 * Math.PI / 60;

        private static double Val(ValueTable t, string id, string key) => t.Get(id, key)!.Value!.Value;

        private static ValueTable Solve(SketchDocument doc, Registry r) => ValueSolver.Solve(doc, r, null, new ValueSolveOptions { BudgetMs = 10000 });

        private static SketchObject Make(Registry r, string item, string id, double x, double y, double? w = null, double? h = null)
        {
            var o = r.Create(r.Item(item)!, new Vec2(x, y)) with { Id = id };
            return w is null ? o : o with { Size = new Size2(w.Value, h!.Value) };
        }

        private static SketchDocument Doc(params SketchObject[] objs)
        {
            var doc = SketchDocument.Create(PageSizes.Slide16x9, "Generator");
            return Edits.Add(doc, doc.Pages[0].Id, objs);
        }

        private static IReadOnlyList<CheckFinding> Checks(Registry r, SketchDocument doc) => ValueSolver.Checks(Solve(doc, r), r, new RenderContext(doc));

        /// <summary>3000 min⁻¹ and 100 N·m: P = 2π·50 s⁻¹·100 N·m = 31.416 kW; η = 95 %.</summary>
        private const double P = 2 * Math.PI * 50 * 100;

        [Fact]
        public void TheDrivetrainEMachineRunsAsMotorByDefaultAndAsGeneratorWhenChosen()
        {
            var r = Fx.Reg();
            var motor = Make(r, "machines.emachine.stick", "m", 0, 0).WithParam("speed", "3000 rpm").WithParam("torque", "100 N·m").WithParam("efficiency", "95 %");
            var tm = Solve(Doc(motor), r);
            Assert.Empty(tm.Conflicts);
            Assert.Equal(P / 0.95, Val(tm, "m", "Pel"), 6);
            Assert.Contains(tm.Explain("m", "Pel", Lang.En), s => s.RelationId == "mach.efficiency.motor");

            var gen = motor.WithParam("mode", "generator");
            var tg = Solve(Doc(gen), r);
            Assert.Empty(tg.Conflicts);
            Assert.Equal(0.95 * P, Val(tg, "m", "Pel"), 6);
            Assert.Contains(tg.Explain("m", "Pel", Lang.En), s => s.RelationId == "mach.efficiency.generator");
            // The mode is an engineering parameter of the e-machine with both names.
            var mode = Assert.Single(r.Type("machines.drivetrain.emachine")!.Parameters, p => p.Key == "mode");
            Assert.True(mode.Advanced);
            Assert.Equal("motor", mode.Default!.Value.Text);
            Assert.All(mode.Options!, o => Assert.False(string.IsNullOrEmpty(o.Label.De)));
        }

        [Fact]
        public void TheGeneratorSymbolAndTheMotorBlockFollowTheirMode()
        {
            var r = Fx.Reg();
            // The library's generator symbol already has mode = generator: the efficiency goes the other way.
            var g = Make(r, "machines.generator.symbol", "g", 0, 0).WithParam("power", "10 kW").WithParam("efficiency", "90 %");
            Assert.Equal(9000, Val(Solve(Doc(g), r), "g", "Pel"), 6);
            var b = Make(r, "machines.block.motor", "b", 0, 0).WithParam("mode", "generator").WithParam("powerEl", "9 kW").WithParam("efficiency", "90 %");
            Assert.Equal(10000, Val(Solve(Doc(b), r), "b", "P"), 6);
        }

        [Fact]
        public void TheEfficiencyCheckKnowsWhichSideIsTheOutput()
        {
            var r = Fx.Reg();
            string[] Ids(SketchObject o) => Checks(r, Doc(o)).Select(f => f.CheckId).ToArray();
            var gen = Make(r, "machines.emachine.stick", "m", 0, 0).WithParam("mode", "generator");
            // A generator gives out less electrical power than it takes in mechanically.
            Assert.DoesNotContain("mach.efficiency", Ids(gen.WithParam("power", "10 kW").WithParam("powerEl", "9 kW")));
            Assert.Contains("mach.efficiency", Ids(gen.WithParam("power", "9 kW").WithParam("powerEl", "10 kW")));
            // A motor the other way round.
            var motor = Make(r, "machines.emachine.stick", "m", 0, 0);
            Assert.DoesNotContain("mach.efficiency", Ids(motor.WithParam("power", "9 kW").WithParam("powerEl", "10 kW")));
            Assert.Contains("mach.efficiency", Ids(motor.WithParam("power", "10 kW").WithParam("powerEl", "9 kW")));
        }

        /// <summary>
        /// An e-machine motor drives a gear stage (i = 2) whose output shaft carries an e-machine in generator mode: the
        /// generator shaft runs at 1500 min⁻¹ with 40 N·m (P = 6283.2 W) and gives P_el = 0.95·P = 5969.0 W.
        /// </summary>
        [Fact]
        public void AGeneratorDrivenThroughAGearStageGivesItsElectricalOutput()
        {
            var r = DrivetrainTests.Both();
            var doc = Doc(
                Make(r, "mech.shaft.line", "s1", 20, 0, 80, 8),
                Make(r, "mech.shaft.line", "s2", 20, 30, 80, 8),
                Make(r, "machines.emachine.stick", "em", -10, 0).WithParam("speed", "3000 rpm").WithParam("torque", "20 N·m"),
                Make(r, "mech.spur.stick", "g1", 40, 0, 8, 20).WithParam("teeth", 20),
                Make(r, "mech.spur.stick", "g2", 40, 30, 8, 40).WithParam("teeth", 40),
                Make(r, "machines.emachine.stick", "gen", -10, 30).WithParam("mode", "generator").WithParam("efficiency", "95 %"));
            var t = Solve(doc, r);
            Assert.Empty(t.Conflicts);
            Assert.Equal(1500 * RpmToSi, Val(t, "gen", "n"), 9);
            Assert.Equal(40, Val(t, "gen", "T"), 9);
            Assert.Equal(2 * Math.PI * 25 * 40, Val(t, "gen", "P"), 6);
            Assert.Equal(0.95 * 2 * Math.PI * 25 * 40, Val(t, "gen", "Pel"), 6);
        }
    }
}
