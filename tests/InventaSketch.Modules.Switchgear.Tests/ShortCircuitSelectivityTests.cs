using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Switchgear.Tests
{
    /// <summary>
    /// Round 6 (2026-11-26): the short-circuit estimate at the board (main switch: transformer and supply cable), the
    /// currents passed to the protective devices below (disconnection and breaking capacity), selectivity hints against
    /// the device upstream and the RCD type a frequency inverter below an RCD needs. Expected values are hand calculations.
    /// </summary>
    public class ShortCircuitSelectivityTests
    {
        private static readonly Registry Reg = Fx.Reg;
        private static readonly double Sqrt3 = Math.Sqrt(3);

        private static ValueTable Solve(SketchDocument doc) => ValueSolver.Solve(doc, Reg, null, new ValueSolveOptions { BudgetMs = 10000 });

        private static double? Val(ValueTable t, SketchObject o, string key) => t.Get(o.Id, key)?.Value;

        private static IReadOnlyList<CheckFinding> Checks(SketchDocument doc) => ValueSolver.Checks(Solve(doc), Reg, new RenderContext(doc));

        /// <summary>630 kVA, u_k = 4 %, 50 m of 95 mm² copper.</summary>
        private static SketchObject Main()
            => Fx.Item("sw.switch.main.supply").WithParam("ref", "-Q1").WithParam("trafoPower", "630 kVA").WithParam("uk", "4 %")
                .WithParam("supplyLength", "50 m").WithParam("supplySection", "95 mm²");

        private const double Zt = 0.04 * 400 * 400 / 630000.0;
        private const double Zl = 0.017241 * 50 / 95;

        [Fact]
        public void MainSwitchEstimatesTheShortCircuitCurrentsAtTheBoard()
        {
            var q = Main();
            var t = Solve(Fx.Doc(q));
            Assert.Empty(t.Conflicts);
            Assert.Equal(Zt, Val(t, q, "ZT")!.Value, 9);
            Assert.Equal(Zl, Val(t, q, "ZL")!.Value, 9);
            Assert.Equal(Zt + 2 * Zl, Val(t, q, "Zs")!.Value, 9);
            // I_k1 = 400 V/(√3 · 28.3 mΩ) ≈ 8.16 kA; I_k3 = 400 V/(√3 · 19.2 mΩ) ≈ 12.0 kA.
            Assert.Equal(400 / (Sqrt3 * (Zt + 2 * Zl)), Val(t, q, "Ik1")!.Value, 6);
            Assert.Equal(8158, Val(t, q, "Ik1")!.Value, 0);
            Assert.Equal(400 / (Sqrt3 * (Zt + Zl)), Val(t, q, "Ik3")!.Value, 6);
            Assert.Equal(Provenance.Derived, t.Get(q.Id, "Ik1")!.Provenance);
        }

        [Fact]
        public void TheBoardCurrentsReachTheBreakerAndItsBreakingCapacityIsChecked()
        {
            var q = Main();
            var f1 = Fx.Item("sw.protection.mcb", 300, 100).WithParam("ref", "-F1").WithParam("feed", "-Q1").WithParam("icn", "6 kA");
            var doc = Fx.Doc(q, f1);
            var t = Solve(doc);
            Assert.Empty(t.Conflicts);
            Assert.Equal(Val(t, q, "Ik3")!.Value, Val(t, f1, "Ik3")!.Value, 6);
            Assert.Equal(Provenance.Linked, t.Get(f1.Id, "Ik1")!.Provenance);
            var checks = Checks(doc);
            var f = checks.Single(x => x.CheckId == "sw.breaking");
            Assert.Equal(f1.Id, f.ObjectId);
            Assert.Equal("S11", f.Source);
            // 8.2 kA reaches 5 · 16 A = 80 A: the breaker trips instantaneously.
            Assert.DoesNotContain(checks, x => x.CheckId == "sw.disconnect");

            var ok = Fx.Doc(q, f1.WithParam("icn", "15 kA"));
            Assert.DoesNotContain(Checks(ok), x => x.CheckId == "sw.breaking");
        }

        [Fact]
        public void AHighLoopImpedanceMissesTheInstantaneousTrip()
        {
            // Z_s = 3 Ω entered: I_k1 = 400 V/(√3 · 3 Ω) = 77 A < I_a = 80 A (B16).
            var q = Fx.Item("sw.switch.main").WithParam("ref", "-Q1").WithParam("zs", "3 Ω");
            var f1 = Fx.Item("sw.protection.mcb", 300, 100).WithParam("ref", "-F1").WithParam("feed", "-Q1");
            var doc = Fx.Doc(q, f1);
            var t = Solve(doc);
            Assert.Equal(400 / (Sqrt3 * 3), Val(t, f1, "Ik1")!.Value, 6);
            var f = Checks(doc).Single(x => x.CheckId == "sw.disconnect");
            Assert.Equal(f1.Id, f.ObjectId);
            Assert.Equal(CheckSeverity.Warning, f.Severity);
            Assert.Equal("S10", f.Source);

            // Characteristic B10 (I_a = 50 A) is reached.
            var b10 = Fx.Doc(q, f1.WithParam("rating", "10"));
            Assert.DoesNotContain(Checks(b10), x => x.CheckId == "sw.disconnect");
        }

        [Fact]
        public void SelectivityHintsCompareWithTheBreakerUpstream()
        {
            var q = Main();
            var f1 = Fx.Item("sw.protection.mcb", 300, 100).WithParam("ref", "-F1").WithParam("feed", "-Q1").WithParam("rating", "20");
            var f2 = Fx.Item("sw.protection.mcb", 340, 100).WithParam("ref", "-F2").WithParam("feed", "-F1");
            var doc = Fx.Doc(q, f1, f2);
            var t = Solve(doc);
            Assert.Empty(t.Conflicts);
            Assert.Equal(20, Val(t, f2, "Inup")!.Value, 9);
            Assert.Equal(100, Val(t, f2, "Iaup")!.Value, 9);
            Assert.Equal(Provenance.Linked, t.Get(f2.Id, "Inup")!.Provenance);
            // The board's I_k3 reaches F2 through F1 from the main switch.
            Assert.Equal(Val(t, q, "Ik3")!.Value, Val(t, f2, "Ik3")!.Value, 6);
            var checks = Checks(doc).Where(x => x.ObjectId == f2.Id).Select(x => x.CheckId).ToList();
            Assert.Contains("sw.selectivity", checks);          // 20 A < 1.6 · 16 A
            Assert.Contains("sw.selectivity.instant", checks);  // 12 kA ≥ 5 · 20 A
            // F1 has no breaker upstream (the main switch is no overcurrent device).
            Assert.Null(Val(t, f1, "Inup"));

            // An upstream NH fuse of 63 A: ratio 3.9, no instantaneous trip value.
            var fuse = Fx.Item("sw.fuse.nh", 300, 100).WithParam("ref", "-F1").WithParam("feed", "-Q1");
            var withFuse = Fx.Doc(q, fuse, f2);
            var t2 = Solve(withFuse);
            Assert.Equal(63, Val(t2, f2, "Inup")!.Value, 9);
            Assert.Null(Val(t2, f2, "Iaup"));
            Assert.DoesNotContain(Checks(withFuse), x => x.ObjectId == f2.Id && x.CheckId.StartsWith("sw.selectivity", StringComparison.Ordinal));
        }

        [Fact]
        public void AFrequencyInverterBelowARcdAsksForTheRightType()
        {
            var rcd = Fx.Item("sw.protection.rcd", 300, 100).WithParam("ref", "-F1");
            var mcb = Fx.Item("sw.protection.mcb.c16.3p", 340, 100).WithParam("ref", "-F2").WithParam("feed", "-F1");
            var vfd = Fx.Item("sw.drive.vfd", 340, 200).WithParam("ref", "-T1").WithParam("feed", "-F2");
            var doc = Fx.Doc(rcd, mcb, vfd);
            var f = Checks(doc).Single(x => x.CheckId == "sw.rcd.type");
            Assert.Equal(rcd.Id, f.ObjectId);
            Assert.Equal(CheckSeverity.Hint, f.Severity);
            Assert.False(string.IsNullOrEmpty(f.Message.Get(Lang.De)));
            Assert.DoesNotContain(Checks(Fx.Doc(rcd.WithParam("rcdType", "B"), mcb, vfd)), x => x.CheckId == "sw.rcd.type");

            // Single-phase inverter: type F is enough, type A is not; an RCBO is checked as well.
            var rcbo = Fx.Item("sw.protection.rcbo", 300, 100).WithParam("ref", "-F3");
            var one = Fx.Item("sw.drive.vfd.1ph", 340, 200).WithParam("ref", "-T2").WithParam("feed", "-F3");
            Assert.Contains(Checks(Fx.Doc(rcbo, one)), x => x.CheckId == "sw.rcd.type" && x.ObjectId == rcbo.Id);
            Assert.DoesNotContain(Checks(Fx.Doc(rcbo.WithParam("rcdType", "F"), one)), x => x.CheckId == "sw.rcd.type");
        }

        [Fact]
        public void OverloadRelaysCarryNoShortCircuitValuesAndNewItemsAreFound()
        {
            Assert.Null(ValueModel.Variable(Reg.Type("sw.protection.overload"), "Ik1"));
            Assert.NotNull(ValueModel.Variable(Reg.Type("sw.protection.mpcb"), "Ik3"));
            Assert.True(ValueModel.Variable(Reg.Type("sw.protection.mcb"), "Inup")!.IsReadOnly);
            foreach (var (id, kw) in new[] { ("sw.switch.main.supply", "kurzschlussstrom"), ("sw.switch.main.supply", "loop impedance"), ("sw.protection.rcd.f", "typ f") })
            {
                var item = Reg.Item(id)!;
                Assert.Contains(kw, item.Keywords!);
                Assert.False(string.IsNullOrEmpty(item.Name.De));
            }
            Assert.Equal("F", Reg.Item("sw.protection.rcd.f")!.Params!["rcdType"].Text);
        }
    }
}
