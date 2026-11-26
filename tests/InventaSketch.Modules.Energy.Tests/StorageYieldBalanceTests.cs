using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Energy.Tests
{
    /// <summary>
    /// Round 6 (2026-11-26): battery state of charge, time left and round-trip efficiency; PV annual yield from the
    /// irradiation and the performance ratio; the grid connection point with the inverters' AC power, the feed-in limit and
    /// the annual energy balance (self-consumption share, self-sufficiency). Expected values are hand calculations.
    /// </summary>
    public class StorageYieldBalanceTests
    {
        private static ValueTable Solve(SketchDocument doc, Registry r) => ValueSolver.Solve(doc, r, null, new ValueSolveOptions { BudgetMs = 10000 });

        private static double Val(ValueTable t, string id, string key) => t.Get(id, key)!.Value!.Value;

        private static SketchObject Make(Registry r, string item, string id) => r.Create(r.Item(item)!, Vec2.Zero) with { Id = id };

        private static SketchDocument Doc(params SketchObject[] objs)
        {
            var doc = SketchDocument.Create(PageSizes.Slide16x9, "Values");
            return Edits.Add(doc, doc.Pages[0].Id, objs);
        }

        private static SketchObject Cable(Registry r, string id, (string Obj, string Port) a, (string Obj, string Port) b)
            => r.Create(r.Item("energy.line.cable")!, Vec2.Zero) with
            {
                Id = id,
                Points = new[] { new Vec2(0, 0), new Vec2(10, 0) },
                Attach = new Attachment?[] { new Attachment(a.Obj, 0, 0) { AnchorId = a.Port }, new Attachment(b.Obj, 0, 0) { AnchorId = b.Port } },
            };

        private static IReadOnlyList<CheckFinding> Checks(Registry r, SketchDocument doc) => ValueSolver.Checks(Solve(doc, r), r, new RenderContext(doc));

        private const double KWh = 3.6e6;

        [Fact]
        public void BatteryShowsStoredEnergyTimeLeftAndChargeEnergy()
        {
            var r = Fx.Reg();
            var bat = Make(r, "energy.storage.battery.home", "bat").WithParam("powerBat", "2,5 kW").WithParam("roundTrip", "90 %");
            var t = Solve(Doc(bat), r);
            Assert.Empty(t.Conflicts);
            // 50 % of 10 kWh = 5 kWh; 5 kWh / 2.5 kW = 2 h; 10 kWh / 0.9 = 11.11 kWh drawn to charge from empty.
            Assert.Equal(0.5, Val(t, "bat", "soc"), 9);
            Assert.Equal(5 * KWh, Val(t, "bat", "Est"), 3);
            Assert.Equal(2 * 3600, Val(t, "bat", "tl"), 6);
            Assert.Equal(10 / 0.9 * KWh, Val(t, "bat", "Ein"), 1);
            Assert.Equal(Provenance.Derived, t.Get("bat", "Est")!.Provenance);
        }

        [Fact]
        public void BatteryStateOfChargeIsTheDrawnLevelParameter()
        {
            var r = Fx.Reg();
            var type = r.Type("energy.storage.battery")!;
            Assert.Single(type.Parameters, p => p.Key == "soc");
            var bat = Make(r, "energy.storage.battery", "bat").WithParam("soc", 20.0);
            var t = Solve(Doc(bat), r);
            Assert.Equal(0.2, Val(t, "bat", "soc"), 9);
            Assert.Equal(2 * KWh, Val(t, "bat", "Est"), 3);
        }

        [Fact]
        public void PvArrayGivesTheAnnualYieldFromIrradiationAndPerformanceRatio()
        {
            var r = Fx.Reg();
            var pv = Make(r, "energy.pv.array.yield", "pv").WithParam("irradiation", "1100 kWh/m²");
            var t = Solve(Doc(pv), r);
            Assert.Empty(t.Conflicts);
            // Y_f = 1100 kWh/m² / 1 kW/m² · 0.8 (default PR) = 880 kWh/kWp; E_a = 10 kWp · 880 kWh/kWp = 8800 kWh.
            Assert.Equal(1100 * KWh, Val(t, "pv", "Hpoa"), 0);
            Assert.Equal(880 * 3600.0, Val(t, "pv", "Ysp"), 3);
            Assert.Equal(8800 * KWh, Val(t, "pv", "Ea"), 0);
            Assert.Equal(Provenance.Default, t.Get("pv", "PR")!.Provenance);
            // The fields carry the new kinds and show kWh/m² and kWh/kWp.
            var type = (IValueType)r.Type("energy.pv.array")!;
            Assert.Same(Quantities.Irradiation, type.Variables.Single(v => v.Key == "Hpoa").Kind);
            Assert.Same(Quantities.SpecificYield, type.Variables.Single(v => v.Key == "Ysp").Kind);
            Assert.Equal("880 kWh/kWp", ValueFormat.Format(Val(t, "pv", "Ysp"), type.Variables.Single(v => v.Key == "Ysp").DisplayUnit, Lang.En));
            Assert.Equal("1100 kWh/m²", ValueFormat.Format(Val(t, "pv", "Hpoa"), type.Variables.Single(v => v.Key == "Hpoa").DisplayUnit, Lang.En));
        }

        [Theory]
        [InlineData("1100 h")]
        [InlineData("1100")]
        [InlineData("3960 MJ/m²")]
        public void PvYieldReadsIrradiationStoredInHoursBareOrInOtherUnits(string text)
        {
            // Drawings of round 6 stored the irradiation as peak-sun hours ("1100 h") or as a bare number in h.
            var r = Fx.Reg();
            var pv = Make(r, "energy.pv.array.yield", "pv").WithParam("irradiation", text);
            var t = Solve(Doc(pv), r);
            Assert.Empty(t.Conflicts);
            Assert.Equal(1100 * KWh, Val(t, "pv", "Hpoa"), 0);
            Assert.Equal(8800 * KWh, Val(t, "pv", "Ea"), 0);
        }

        [Fact]
        public void PvSpecificYieldStoredInFullLoadHoursStillParses()
        {
            var r = Fx.Reg();
            foreach (var text in new[] { "950 h", "950 kWh/kWp", "950" })
            {
                var s = Make(r, "energy.pv.string", "s").WithParam("modules", 10).WithParam("power", 400.0).WithParam("specificYield", text);
                var t = Solve(Doc(s), r);
                Assert.Empty(t.Conflicts);
                // 4 kWp · 950 kWh/kWp = 3800 kWh; H_POA = 950 kWh/kWp · 1 kW/m² / 0.8 = 1187.5 kWh/m².
                Assert.Equal(3800 * KWh, Val(t, "s", "Ea"), 0);
                Assert.Equal(1187.5 * KWh, Val(t, "s", "Hpoa"), 0);
            }
        }

        [Fact]
        public void PvStringSolvesTheSpecificYieldBackFromAnEnteredAnnualYield()
        {
            var r = Fx.Reg();
            // 10 modules · 400 Wp = 4 kWp; 3800 kWh per year → 950 h specific yield.
            var s = Make(r, "energy.pv.string", "s").WithParam("modules", 10).WithParam("power", 400.0).WithParam("annualYield", "3800 kWh");
            var t = Solve(Doc(s), r);
            Assert.Empty(t.Conflicts);
            Assert.Equal(950 * 3600.0, Val(t, "s", "Ysp"), 3);
        }

        [Fact]
        public void GridConnectionPointAddsTheInvertersAndWarnsAboveTheFeedInLimit()
        {
            var r = Fx.Reg();
            var doc = Doc(
                Make(r, "energy.conv.inverter", "i1").WithParam("powerAc", "6 kW"),
                Make(r, "energy.conv.inverter", "i2").WithParam("powerAc", "4 kW"),
                Make(r, "energy.grid.busbar", "bus").WithParam("voltage", 0.4),
                Make(r, "energy.grid.connection.point", "poc").WithParam("feedLimit", "7 kW"),
                Cable(r, "c1", ("i1", "ac"), ("bus", "t1")), Cable(r, "c2", ("i2", "ac"), ("bus", "t2")), Cable(r, "c3", ("poc", "poc"), ("bus", "t3")));
            var t = Solve(doc, r);
            Assert.Empty(t.Conflicts);
            Assert.Equal(10000, Val(t, "poc", "Pgen"), 6);
            Assert.Equal(10000, Val(t, "bus", "P"), 6);
            var f = Checks(r, doc).Single(x => x.CheckId == "energy.feedin.limit");
            Assert.Equal("poc", f.ObjectId);
            Assert.Equal(CheckSeverity.Hint, f.Severity);

            var ok = Edits.Update(doc, new[] { "poc" }, x => x.WithParam("feedLimit", "12 kW"));
            Assert.DoesNotContain(Checks(r, ok), x => x.CheckId == "energy.feedin.limit");
        }

        [Fact]
        public void EnergyBalanceGivesSelfConsumptionAutarkyFeedInAndGridEnergy()
        {
            var r = Fx.Reg();
            var poc = Make(r, "energy.grid.connection.point", "poc")
                .WithParam("energyPv", "9000 kWh").WithParam("energyLoad", "5000 kWh").WithParam("energySelf", "3000 kWh");
            var t = Solve(Doc(poc), r);
            Assert.Empty(t.Conflicts);
            // 3000/9000 = 33.3 % self-consumption; 3000/5000 = 60 % autarky; 6000 kWh fed in; 2000 kWh drawn.
            Assert.Equal(1.0 / 3, Val(t, "poc", "rself"), 9);
            Assert.Equal(0.6, Val(t, "poc", "raut"), 9);
            Assert.Equal(6000 * KWh, Val(t, "poc", "Efeed"), 1);
            Assert.Equal(2000 * KWh, Val(t, "poc", "Egrid"), 1);
        }

        [Fact]
        public void EnergyBalanceSolvesTheSelfConsumedEnergyFromTheAutarkyAndWarnsWhenImpossible()
        {
            var r = Fx.Reg();
            var poc = Make(r, "energy.grid.connection.point", "poc").WithParam("energyLoad", "4000 kWh").WithParam("autarky", "70 %").WithParam("energyPv", "6000 kWh");
            var doc = Doc(poc);
            var t = Solve(doc, r);
            Assert.Equal(2800 * KWh, Val(t, "poc", "Eself"), 1);
            Assert.Equal(2800.0 / 6000, Val(t, "poc", "rself"), 9);
            Assert.DoesNotContain(Checks(r, doc), x => x.CheckId == "energy.balance");

            var bad = Doc(Make(r, "energy.grid.connection.point", "poc").WithParam("energyLoad", "4000 kWh").WithParam("energyPv", "2000 kWh").WithParam("energySelf", "3000 kWh"));
            var f = Checks(r, bad).Single(x => x.CheckId == "energy.balance");
            Assert.Equal(CheckSeverity.Warning, f.Severity);
        }

        [Fact]
        public void NewLibraryItemsInsertWithoutValuesAndAreFoundInBothLanguages()
        {
            var r = Fx.Reg();
            foreach (var (id, query) in new[]
            {
                ("energy.grid.connection.point", "einspeisebegrenzung"), ("energy.grid.connection.point", "autarky"),
                ("energy.storage.battery.home", "ladezustand"), ("energy.pv.array.yield", "jahresertrag"),
            })
            {
                var item = r.Item(id)!;
                Assert.False(string.IsNullOrEmpty(item.Name.De));
                Assert.Contains(query, item.Keywords!);
                var o = r.Create(item, Vec2.Zero);
                Assert.NotEmpty(r.TypeOf(o).Render(o, new RenderContext(Doc(o))));
            }
        }
    }
}
