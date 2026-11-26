using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Machines.Tests
{
    /// <summary>
    /// Round 7 machine values: supply frequency, synchronous speed and slip on the motor block and the drivetrain e-machine
    /// (synchronous machines locked to n_s), terminal power P_el = √3·U·I·cos φ, the driven load block, inertia reflected
    /// through gearboxes (J/i²) and a vehicle mass at the wheel (m·r²), and the run-up time of the motor. Expected values are
    /// hand calculations.
    /// </summary>
    public class RunUpTests
    {
        private const double RpmToSi = 2 * Math.PI / 60;

        private static double Rpm(ValueTable t, string id, string key = "n") => t.Get(id, key)!.Value!.Value / RpmToSi;
        private static double Val(ValueTable t, string id, string key) => t.Get(id, key)?.Value ?? double.NaN;

        private static ValueTable Solve(SketchDocument doc, Registry r) => ValueSolver.Solve(doc, r, null, new ValueSolveOptions { BudgetMs = 10000 });

        private static SketchObject Make(Registry r, string item, string id, double x = 0, double y = 0) => r.Create(r.Item(item)!, new Vec2(x, y)) with { Id = id };

        private static SketchDocument Doc(params SketchObject[] objs)
        {
            var doc = SketchDocument.Create(PageSizes.Slide16x9, "Values");
            return Edits.Add(doc, doc.Pages[0].Id, objs);
        }

        private static SketchObject Arrow(string id, string from, string to) => new()
        {
            Id = id,
            Type = "ann.arrow",
            Points = new[] { new Vec2(0, 0), new Vec2(10, 0) },
            Attach = new Attachment?[] { new Attachment(from, 0.5, 0) { AnchorId = "e" }, new Attachment(to, -0.5, 0) { AnchorId = "w" } },
        };

        private static IReadOnlyList<CheckFinding> Checks(Registry r, SketchDocument doc) => ValueSolver.Checks(Solve(doc, r), r, new RenderContext(doc));

        // ------------------------------------------------------------------ frequency, synchronous speed and slip

        [Fact]
        public void MotorBlockSlipFromFrequencyAndSpeed()
        {
            var r = Fx.Reg();
            // 4 poles (default) at 50 Hz: n_s = 1500 min⁻¹; at 1440 min⁻¹ the slip is 4 %.
            var t = Solve(Doc(Make(r, "machines.block.motor", "m").WithParam("frequency", "50 Hz").WithParam("speed", "1440 rpm")), r);
            Assert.Equal(1500, Rpm(t, "m", "ns"), 9);
            Assert.Equal(0.04, Val(t, "m", "s"), 9);
            Assert.Equal(Provenance.Default, t.Get("m", "poles")!.Provenance);
        }

        [Fact]
        public void SynchronousEMachineRunsAtTheSynchronousSpeed()
        {
            var r = Fx.Reg();
            // PMSM, 8 poles at 200 Hz: n = n_s = 60·200/4 = 3000 min⁻¹.
            var pm = Make(r, "machines.emachine.stick", "pm").WithParam("frequency", "200 Hz").WithParam("poles", "8");
            var t = Solve(Doc(pm), r);
            Assert.Equal(3000, Rpm(t, "pm"), 9);
            Assert.Equal(Provenance.Derived, t.Get("pm", "n")!.Provenance);
            // Induction kind: slip between n and n_s.
            var asm = Make(r, "machines.emachine.asm.stick", "asm").WithParam("frequency", "50 Hz").WithParam("speed", "1440 rpm");
            var ta = Solve(Doc(asm), r);
            Assert.Equal(0.04, Val(ta, "asm", "s"), 9);
            // Reluctance kind: no mains-frequency relation.
            var srm = Make(r, "machines.emachine.srm.stick", "srm").WithParam("frequency", "200 Hz");
            Assert.True(double.IsNaN(Val(Solve(Doc(srm), r), "srm", "ns")));
            // The magnet cross-section is synchronous too; the cage rotor is not.
            var spm = Make(r, "machines.radial.spm", "spm").WithParam("frequency", "50 Hz").WithParam("poles", 8);
            Assert.Equal(750, Rpm(Solve(Doc(spm), r), "spm"), 9);
            var cage = Make(r, "machines.radial.asm", "cage").WithParam("frequency", "50 Hz").WithParam("poles", 4);
            Assert.True(double.IsNaN(Val(Solve(Doc(cage), r), "cage", "n")));
        }

        [Fact]
        public void TerminalPowerFromVoltageCurrentAndPowerFactor()
        {
            var r = Fx.Reg();
            // 400 V, 10 A, cos φ 0.85: P_el = √3·400·10·0.85 = 5889.0 W; η 90 %: P = 5300.1 W.
            var m = Make(r, "machines.block.motor", "m").WithParam("voltage", "400 V").WithParam("current", "10 A").WithParam("powerFactor", "0.85").WithParam("efficiency", "90 %");
            var t = Solve(Doc(m), r);
            Assert.Equal(Math.Sqrt(3) * 400 * 10 * 0.85, Val(t, "m", "Pel"), 6);
            Assert.Equal(0.9 * Math.Sqrt(3) * 400 * 10 * 0.85, Val(t, "m", "P"), 6);
            // Single phase: P_el = U·I·cos φ.
            var one = m.WithParam("phases", 1).WithParam("voltage", "230 V");
            Assert.Equal(230 * 10 * 0.85, Val(Solve(Doc(one), r), "m", "Pel"), 6);
            // The current follows from the power.
            var back = Make(r, "machines.block.motor", "b").WithParam("voltage", "400 V").WithParam("powerFactor", "0.85").WithParam("powerEl", "5889 W");
            Assert.Equal(5889 / (Math.Sqrt(3) * 400 * 0.85), Val(Solve(Doc(back), r), "b", "I"), 6);
        }

        // ------------------------------------------------------------------ load, inertia and run-up

        [Fact]
        public void LoadInertiaReflectedThroughTheGearboxGivesTheRunUpTime()
        {
            var r = Fx.Reg();
            var doc = Doc(
                Make(r, "machines.block.motor", "mot", 0, 0).WithParam("speed", "1500 rpm").WithParam("rotorInertia", "0.02 kg·m²").WithParam("runUpTorque", "50 N·m"),
                Make(r, "machines.block.gearbox", "gb", 50, 0).WithParam("ratio", "5"),
                Make(r, "machines.block.load", "load", 100, 0).WithParam("inertia", "2.5 kg·m²").WithParam("torque", "100 N·m"),
                Arrow("a1", "mot", "gb"), Arrow("a2", "gb", "load"));
            var t = Solve(doc, r);
            Assert.Empty(t.Conflicts);
            // J₁ = 2.5/5² = 0.1 kg·m² at the motor; T = 100/5 = 20 N·m; n_load = 300 min⁻¹.
            Assert.Equal(0.1, Val(t, "gb", "J1"), 9);
            Assert.Equal(0.1, Val(t, "mot", "JL"), 9);
            Assert.Equal(Provenance.Linked, t.Get("mot", "JL")!.Provenance);
            Assert.Equal(20, Val(t, "mot", "T"), 9);
            Assert.Equal(300, Rpm(t, "load"), 9);
            // t_a = J·ω/(T_M − T_L) = 0.12·157.08/(50 − 20) = 0.628 s.
            Assert.Equal(0.12 * 1500 * RpmToSi / 30, Val(t, "mot", "ta"), 9);
            Assert.Empty(ValueSolver.Checks(t, r, new RenderContext(doc)));
        }

        [Fact]
        public void VehicleMassAtTheWheelBecomesMotorInertia()
        {
            var r = Fx.Reg();
            var doc = Doc(
                Make(r, "machines.block.motor", "mot", 0, 0).WithParam("rotorInertia", "0.05 kg·m²"),
                Make(r, "machines.block.differential", "diff", 50, 0).WithParam("ratio", "10"),
                Make(r, "machines.wheel", "wh", 100, 0).WithParam("radius", "300 mm").WithParam("mass", "1500 kg"),
                Arrow("a1", "mot", "diff"), Arrow("a2", "diff", "wh"));
            var t = Solve(doc, r);
            // J = 1500·0.3² = 135 kg·m² at the wheel, 135/10² = 1.35 kg·m² at the motor.
            Assert.Equal(135, Val(t, "wh", "J"), 9);
            Assert.Equal(1.35, Val(t, "mot", "JL"), 9);
            Assert.Equal(1.4, Val(t, "mot", "J"), 9);
        }

        [Fact]
        public void RunUpPresetAndTheTorqueCheck()
        {
            var r = Fx.Reg();
            // Preset: 1450 min⁻¹, load torque 20 N·m, mean motor torque 60 N·m, J = 0.01 + 0.09 kg·m².
            var m = Make(r, "machines.block.motor.runup", "m");
            var t = Solve(Doc(m), r);
            Assert.Equal(0.1 * 1450 * RpmToSi / 40, Val(t, "m", "ta"), 9);
            // A mean motor torque at or below the load torque cannot accelerate: warning, and no run-up time.
            var weak = m.WithParam("runUpTorque", "15 N·m");
            var f = Assert.Single(Checks(r, Doc(weak)), x => x.CheckId == "mach.runup.torque");
            Assert.Equal(CheckSeverity.Warning, f.Severity);
            Assert.Equal("S15", f.Source);
            Assert.True(double.IsNaN(Val(Solve(Doc(weak), r), "m", "ta")));
        }

        [Fact]
        public void LoadBlockIsInsertableAndDrawsInsideItsBox()
        {
            var r = Fx.Reg();
            foreach (var id in new[] { "machines.block.load", "machines.block.load.symbol" })
            {
                var o = r.Create(r.Item(id)!, Vec2.Zero);
                Fx.AssertInsideBox(r.TypeOf(o).Render(o, Fx.Ctx()), o.Size, id);
                Assert.NotEmpty(r.TypeOf(o).Anchors(o, Fx.Ctx()));
            }
            var t = r.Type("machines.drive.load")!;
            Assert.Equal(new[] { "n", "T", "P", "J" }, ValueModel.Variables(t).Select(v => v.Key));
            Assert.Contains(t.Parameters, p => p.Key == ValueLabels.ShowParam);
        }

        // ------------------------------------------------------------------ maximum speed (round 8)

        [Fact]
        public void SupplyFrequencyAboveTheMaximumSpeedIsReported()
        {
            var r = Fx.Reg();
            // 4 poles at 70 Hz: n_s = 2100 min⁻¹ above n_max = 1800 min⁻¹ (an inverter's frequency); at 50 Hz 1500: fine.
            var fast = Make(r, "machines.block.motor", "m").WithParam("frequency", "70 Hz").WithParam("maxSpeed", "1800 rpm");
            Assert.Contains(Checks(r, Doc(fast)), c => c.CheckId == "mach.speed.max" && c.ObjectId == "m");
            Assert.DoesNotContain(Checks(r, Doc(fast.WithParam("frequency", "50 Hz"))), c => c.CheckId == "mach.speed.max");
            // The speed itself above n_max on a machine cross-section; no maximum entered: no check.
            var radial = Make(r, "machines.radial.asm", "a").WithParam("speed", "4000 rpm");
            Assert.DoesNotContain(Checks(r, Doc(radial)), c => c.CheckId == "mach.speed.max");
            Assert.Contains(Checks(r, Doc(radial.WithParam("maxSpeed", "3600 rpm"))), c => c.CheckId == "mach.speed.max");
            foreach (var id in new[] { "machines.drive.motor", "machines.drivetrain.emachine", "machines.radial", "machines.axial" })
                Assert.Equal("maxSpeed", ValueModel.Variable(r.Type(id), "nmax")!.ParamKey);
        }
    }
}
