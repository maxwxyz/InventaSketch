using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Switchgear.Tests
{
    /// <summary>
    /// Element values of the switchgear module: rated current from the rating choice, the loads of a circuit summed per
    /// phase, the totals and the imbalance of a rail, and the checks "load above rating", "rating above cable" and
    /// "phase imbalance". Every expected value is a hand calculation (U = 230 V phase to neutral, cos φ = 1 by default).
    /// </summary>
    public class ValueTests
    {
        private static readonly Registry Reg = Fx.Reg;

        private static ValueTable Solve(SketchDocument doc) => ValueSolver.Solve(doc, Reg, null, new ValueSolveOptions { BudgetMs = 10000 });

        private static double? Val(ValueTable t, SketchObject o, string key) => t.Get(o.Id, key)?.Value;

        private static IReadOnlyList<CheckFinding> Checks(SketchDocument doc) => ValueSolver.Checks(Solve(doc), Reg, new RenderContext(doc));

        private static SketchObject Load(string item, string feed, string power, double x = 300, double y = 300)
            => Fx.Item(item, x, y).WithParam("feed", feed).WithParam("power", power);

        /// <summary>A rail at (200, 100) and the x of a device of <paramref name="width"/> starting at <paramref name="module"/>.</summary>
        private static (SketchObject Rail, Func<int, double, double> X) Rail()
        {
            var rail = Fx.Item("sw.rail.ts35.24", 300, 100);
            var grid = RailType.Grid(rail);
            return (rail, (module, width) => 300 + grid[0] + module * Modular.Unit + width / 2);
        }

        [Fact]
        public void ValueTypesDeclareBilingualVariablesWithParametersAndSources()
        {
            var withValues = new[]
            {
                "sw.protection.mcb", "sw.protection.rcbo", "sw.protection.rcd", "sw.switch.main", "sw.protection.mpcb", "sw.protection.overload", "sw.fuse.nh",
                "sw.drive.vfd", "sw.house.socket", "sw.house.light", "sw.house.appliance", "sw.house.switch", "sw.rail.ts35",
            };
            foreach (var id in withValues)
            {
                var t = Reg.Type(id)!;
                Assert.True(ValueModel.HasValues(t), id);
                Assert.True(ValueModel.Variables(t).Count(v => v.Relevance == VarRelevance.Card) <= 4, id);
                foreach (var v in ValueModel.Variables(t))
                {
                    Assert.False(string.IsNullOrEmpty(v.Name.En) || string.IsNullOrEmpty(v.Name.Get(Lang.De)), id + "." + v.Key);
                    Assert.Single(t.Parameters, p => p.Key == v.ParamKey);
                }
                Assert.All(ValueModel.Relations(t), r => Assert.False(string.IsNullOrEmpty(r.Name.Get(Lang.De))));
                if (t is IValueChecks checks)
                    foreach (var c in checks.ValueChecks)
                    {
                        Assert.StartsWith("sw.", c.Id);
                        Assert.False(string.IsNullOrEmpty(c.Message.En) || string.IsNullOrEmpty(c.Message.Get(Lang.De)), c.Id);
                        if (c.Source is not null) Assert.NotNull(ValueSources.Get(c.Source));
                    }
            }
            foreach (var id in new[] { "sw.switching.contactor", "sw.plc.module", "sw.safety.relay", "sw.busbar.comb", "sw.blank", "sw.terminal.feedthrough", "sw.line.cable" })
                Assert.False(ValueModel.HasValues(Reg.Type(id)), id);
            // The rated current is the rating choice itself; the load values are optional text parameters.
            Assert.Equal("rating", ValueModel.Variable(Reg.Type("sw.protection.mcb"), "In")!.ParamKey);
            Assert.Equal("I_r", ValueModel.Variable(Reg.Type("sw.protection.mpcb"), "In")!.Symbol.En);
            Assert.Equal(ParamKind.Text, Reg.Type("sw.house.socket")!.Parameters.Single(p => p.Key == "power").Kind);
        }

        [Fact]
        public void EveryItemStillInsertsWithoutValuesAndShowsNoCheck()
        {
            var objs = Reg.Library.Select((item, i) => Reg.Create(item, new Vec2(40 + i % 12 * 120, 40 + i / 12 * 120))).ToArray();
            var doc = Fx.Doc(objs);
            var table = Solve(doc);
            Assert.Empty(table.Conflicts);
            Assert.Empty(ValueSolver.Checks(table, Reg, new RenderContext(doc)));
        }

        [Fact]
        public void RatedCurrentComesFromTheRatingAndTheCharacteristicGivesTheInstantaneousTrip()
        {
            var b16 = Fx.Item("sw.protection.mcb");
            var c10 = Fx.Item("sw.protection.mcb").WithParam("char", "C").WithParam("rating", "10");
            var rcd = Fx.Item("sw.protection.rcd");
            var t = Solve(Fx.Doc(b16, c10, rcd));
            Assert.Equal(16, Val(t, b16, "In")!.Value, 9);
            Assert.Equal(Provenance.Entered, t.Get(b16.Id, "In")!.Provenance);
            Assert.Equal(80, Val(t, b16, "Ia")!.Value, 9);
            Assert.Equal(100, Val(t, c10, "Ia")!.Value, 9);
            Assert.Equal(40, Val(t, rcd, "In")!.Value, 9);
            Assert.Equal(0.030, Val(t, rcd, "IDn")!.Value, 12);
            // No loads: no operating current, nothing to check.
            Assert.Null(Val(t, b16, "IB"));
        }

        [Fact]
        public void AValueEnteredForTheRatedCurrentIsStoredAsTheRatingText()
        {
            var mcb = Fx.Item("sw.protection.mcb");
            var set = Reg.SetParam(mcb, "rating", 20.0);
            Assert.Equal("20", set.Str("rating", ""));
            Assert.Contains("B20", string.Join("|", Fx.Local(set).OfType<TextPrim>().Select(x => x.Text)));
            Assert.Equal(20, Val(Solve(Fx.Doc(set)), set, "In")!.Value, 9);
        }

        [Fact]
        public void LoadsOfACircuitAddUpAndALoadAboveTheRatingWarns()
        {
            var mcb = Fx.Item("sw.protection.mcb").WithParam("ref", "-F2");
            var s1 = Load("sw.house.socket", "-F2", "2000 W");
            var s2 = Load("sw.house.socket.double", "-F2", "1500 W", 320);
            var l1 = Load("sw.house.light", "-F2", "500 W", 340);
            var unvalued = Fx.Item("sw.house.socket", 360, 300).WithParam("feed", "-F2");
            var doc = Fx.Doc(mcb, s1, s2, l1, unvalued);
            var t = Solve(doc);
            Assert.Empty(t.Conflicts);
            Assert.Equal(2000 / 230.0, Val(t, s1, "I")!.Value, 9);
            Assert.Equal(4000, Val(t, mcb, "P")!.Value, 9);
            // I_B = 4000 W / 230 V = 17.39 A > 16 A.
            Assert.Equal(4000 / 230.0, Val(t, mcb, "IB")!.Value, 9);
            Assert.Equal(Provenance.Derived, t.Get(mcb.Id, "IB")!.Provenance);
            var f = Checks(doc).Single(x => x.CheckId == "sw.load.rating");
            Assert.Equal(mcb.Id, f.ObjectId);
            Assert.Equal(CheckSeverity.Warning, f.Severity);
            Assert.Equal("S9", f.Source);
            Assert.False(string.IsNullOrEmpty(f.Message.Get(Lang.De)));
            // Without the lamp: 3500 W → 15.2 A, no warning.
            var ok = Fx.Doc(mcb, s1, s2, unvalued);
            Assert.Equal(3500 / 230.0, Val(Solve(ok), mcb, "IB")!.Value, 9);
            Assert.DoesNotContain(Checks(ok), x => x.CheckId == "sw.load.rating");
        }

        [Fact]
        public void ACurrentEnteredOnALoadCountsAndThePowerFollows()
        {
            var mcb = Fx.Item("sw.protection.mcb.b10").WithParam("ref", "-F3");
            var a = Fx.Item("sw.house.appliance.washer", 300, 300).WithParam("feed", "-F3").WithParam("current", "8 A").WithParam("powerFactor", "0.9");
            var t = Solve(Fx.Doc(mcb, a));
            Assert.Equal(8 * 230 * 0.9, Val(t, a, "P")!.Value, 9);
            Assert.Equal(8, Val(t, mcb, "IB")!.Value, 9);
            Assert.Equal(8 * 230 * 0.9, Val(t, mcb, "P")!.Value, 9);
        }

        [Fact]
        public void RatingAboveTheCableCapacityWarnsWhenTheCableHasAnIz()
        {
            var mcb = Fx.Item("sw.protection.mcb", 100, 100).WithParam("ref", "-F2");
            var socket = Load("sw.house.socket", "-F2", "1000 W");
            var doc = Fx.Doc(mcb, socket);
            var cable = Fx.Line("sw.line.cable", doc, mcb, "2", socket, "c").WithParam("ref", "-W2").WithParam("iz", "13 A");
            doc = Edits.Add(doc, doc.Pages[0].Id, cable);
            var t = Solve(doc);
            Assert.Equal(13, Val(t, mcb, "Iz")!.Value, 9);
            Assert.Contains("-W2", t.Get(mcb.Id, "Iz")!.Via!.Relation.Formula);
            var f = Checks(doc).Single(x => x.CheckId == "sw.rating.cable");
            Assert.Equal(mcb.Id, f.ObjectId);
            Assert.Equal("S9", f.Source);
            // A cable for 20 A: no warning; a device without any I_z: only the first check can apply.
            var ok = Edits.Replace(doc, cable.WithParam("iz", "20 A"));
            Assert.DoesNotContain(Checks(ok), x => x.CheckId == "sw.rating.cable");
            Assert.Null(Val(Solve(Fx.Doc(mcb, socket)), mcb, "Iz"));
            // I_z typed on the device itself.
            var own = Fx.Doc(mcb.WithParam("iz", "10 A"), socket);
            Assert.Contains(Checks(own), x => x.CheckId == "sw.rating.cable");
        }

        [Fact]
        public void AThreePhaseCircuitCarriesItsMostLoadedPhase()
        {
            var mcb = Fx.Item("sw.protection.mcb.c16.3p").WithParam("ref", "-F5");
            var cooker = Load("sw.house.appliance", "-F5", "10800 W");
            var lamp = Load("sw.house.light", "-F5", "2300 W", 320).WithParam("phase", "L1");
            var t = Solve(Fx.Doc(mcb, cooker, lamp));
            // Cooker three-phase (from the circuit): 10800 / (3 · 230) = 15.65 A per phase; the lamp adds 10 A on L1.
            Assert.Equal(10800 / 690.0, Val(t, cooker, "I")!.Value, 9);
            Assert.Equal(10800 / 690.0 + 10, Val(t, mcb, "IB")!.Value, 9);
            Assert.Equal(13100, Val(t, mcb, "P")!.Value, 9);
            Assert.Equal("L123", CircuitMap.Of(Fx.Doc(mcb, cooker, lamp)).PhaseOf(cooker.Id));
        }

        [Fact]
        public void ASinglePoleDeviceDecidesThePhaseOfItsLoads()
        {
            var mcb = Fx.Item("sw.protection.mcb").WithParam("ref", "-F2").WithParam("phase", "L2");
            var a = Load("sw.house.socket", "-F2", "100 W");
            var b = Load("sw.house.socket", "-F2", "100 W", 320).WithParam("phase", "L3");
            var free = Load("sw.house.socket", "-F9", "100 W", 340).WithParam("phase", "L3");
            var map = CircuitMap.Of(Fx.Doc(mcb, a, b, free));
            Assert.Equal("L2", map.PhaseOf(a.Id));
            Assert.Equal("L2", map.PhaseOf(b.Id));
            Assert.Equal("L3", map.PhaseOf(free.Id));
            // The phase is a choice of the single-pole device and of the load (not of a three-pole device).
            var actions = Reg.Type(mcb.Type)!.Actions(mcb, Lang.En).ToList();
            Assert.Contains(actions, x => x.Id == "sw.phase=L3" && x.Choice is not null);
            var three = Fx.Item("sw.protection.mcb.c16.3p");
            Assert.DoesNotContain(Reg.Type(three.Type)!.Actions(three, Lang.En), x => x.Id.StartsWith("sw.phase=", StringComparison.Ordinal));
            var loadActions = Reg.Type(a.Type)!.Actions(a, Lang.En).Where(x => x.Id.StartsWith("sw.phase=", StringComparison.Ordinal)).ToList();
            Assert.Equal(5, loadActions.Count);
            Assert.Single(loadActions, x => x.IsActive);
            // Every phase option has its own pictogram (dropdown with option pictograms).
            Assert.Equal(new[] { IconIds.PhaseCircuit, IconIds.PhaseL1, IconIds.PhaseL2, IconIds.PhaseL3, IconIds.PhaseThree }, loadActions.Select(x => x.Icon));
            Assert.Equal(new[] { IconIds.PhaseL1, IconIds.PhaseL2, IconIds.PhaseL3 }, actions.Where(x => x.Id.StartsWith("sw.phase=", StringComparison.Ordinal)).Select(x => x.Icon));
        }

        /// <summary>
        /// A board: rail with a 4-pole RCD -F1 and 1-pole MCBs -F2 (L1), -F3 (L2), -F4 (L1) right of it (fed through the
        /// RCD by rail order); loads 3680 W on -F2, 2300 W on -F3 and -F4.
        /// </summary>
        private static (SketchDocument Doc, SketchObject Rail, SketchObject Rcd, SketchObject F4) Board(string f4Phase = "L1", string rcdRating = "40")
        {
            var (rail, x) = Rail();
            var rcd = Fx.Item("sw.protection.rcd.4p", x(0, 70), 100).WithParam("ref", "-F1").WithParam("rating", rcdRating);
            var f2 = Fx.Item("sw.protection.mcb", x(4, 17.5), 100).WithParam("ref", "-F2").WithParam("phase", "L1");
            var f3 = Fx.Item("sw.protection.mcb", x(5, 17.5), 100).WithParam("ref", "-F3").WithParam("phase", "L2");
            var f4 = Fx.Item("sw.protection.mcb", x(6, 17.5), 100).WithParam("ref", "-F4").WithParam("phase", f4Phase);
            var doc = Fx.Doc(rail, rcd, f2, f3, f4,
                Load("sw.house.socket", "-F2", "3680 W"), Load("sw.house.socket", "-F3", "2300 W", 320), Load("sw.house.appliance.washer", "-F4", "2300 W", 340));
            return (doc, rail, rcd, f4);
        }

        [Fact]
        public void TheRailAddsUpThePhasesAndWarnsAboutImbalance()
        {
            var (doc, rail, rcd, _) = Board();
            var t = Solve(doc);
            Assert.Empty(t.Conflicts);
            Assert.Equal(26, Val(t, rail, "IL1")!.Value, 9);
            Assert.Equal(10, Val(t, rail, "IL2")!.Value, 9);
            Assert.Equal(0, Val(t, rail, "IL3")!.Value, 9);
            Assert.Equal(8280, Val(t, rail, "P")!.Value, 9);
            Assert.Equal(26, Val(t, rail, "dI")!.Value, 9);
            Assert.Equal(20, Val(t, rail, "dImax")!.Value, 9);
            Assert.Equal(Provenance.Default, t.Get(rail.Id, "dImax")!.Provenance);
            // The 4-pole RCD carries its most loaded phase: 26 A ≤ 40 A.
            Assert.Equal(26, Val(t, rcd, "IB")!.Value, 9);
            var f = Checks(doc).Single(x => x.CheckId == "sw.phase.imbalance");
            Assert.Equal(rail.Id, f.ObjectId);
            Assert.Equal(CheckSeverity.Warning, f.Severity);
            Assert.DoesNotContain(Checks(doc), x => x.CheckId == "sw.load.rating");

            // -F4 on L3: 16 / 10 / 10 A, imbalance 6 A.
            var (balanced, rail2, _, _) = Board("L3");
            var tb = Solve(balanced);
            Assert.Equal(6, Val(tb, rail2, "dI")!.Value, 9);
            Assert.DoesNotContain(Checks(balanced), x => x.CheckId == "sw.phase.imbalance");
            // A user limit of 30 A accepts the first board too.
            var limit = Edits.Replace(doc, rail.WithParam("imbalanceMax", "30 A"));
            Assert.DoesNotContain(Checks(limit), x => x.CheckId == "sw.phase.imbalance");
            // A 25 A RCD is below the 26 A of L1.
            var (small, _, rcd2, _) = Board(rcdRating: "25");
            Assert.Contains(Checks(small), x => x.CheckId == "sw.load.rating" && x.ObjectId == rcd2.Id);
        }

        [Fact]
        public void AFrequencyInverterIsALoadOfItsMotorProtection()
        {
            var mpcb = Fx.Item("sw.protection.mpcb", 100, 100).WithParam("ref", "-Q1");
            var vfd = Fx.Item("sw.drive.vfd.4kw", 100, 250);
            var doc = Fx.Doc(mpcb, vfd);
            // Wired: -Q1:2 → inverter L1 (no "fed from" needed).
            var wire = Fx.Line("sw.line.wire", doc, mpcb, "2", vfd, "L1");
            doc = Edits.Add(doc, doc.Pages[0].Id, wire);
            var t = Solve(doc);
            Assert.Equal(4000, Val(t, vfd, "P")!.Value, 9);
            Assert.Equal(4000 / 690.0, Val(t, vfd, "I")!.Value, 9);
            Assert.Equal(4000 / 690.0, Val(t, mpcb, "IB")!.Value, 9);
            Assert.Equal(4, Val(t, mpcb, "In")!.Value, 9);
            var f = Checks(doc).Single(x => x.CheckId == "sw.load.rating");
            Assert.Equal(mpcb.Id, f.ObjectId);
            // Set to 6.3 A: fine.
            Assert.DoesNotContain(Checks(Edits.Replace(doc, mpcb.WithParam("rating", "6.3"))), x => x.CheckId == "sw.load.rating");
        }

        [Fact]
        public void AConflictingOperatingCurrentIsReported()
        {
            var mcb = Fx.Item("sw.protection.mcb").WithParam("ref", "-F2").WithParam("ib", "5 A");
            var s = Fx.Item("sw.house.socket", 300, 300).WithParam("feed", "-F2").WithParam("current", "10 A");
            var t = Solve(Fx.Doc(mcb, s));
            var c = Assert.Single(t.Conflicts);
            Assert.Equal("sw.circuit.current", c.Relation.Relation.Id);
        }
    }
}
