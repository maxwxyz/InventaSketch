using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Energy.Tests
{
    /// <summary>
    /// Values of the heat side (heat pump, CHP, thermal storage, fuel cell: the heating water's V̇ and θ), hydrogen
    /// (electrolyser, fuel cell: η, P_el, P_H2, ṁ), hydropower (P = ρ·g·Q·H·η), pumped storage (E = ρ·g·V·H) and the EV
    /// charging time t = E/P, plus the heat and hydrogen pipes as net lines. Every expected value is a hand calculation.
    /// </summary>
    public class HeatHydroValueTests
    {
        private const double G0 = 9.80665;
        private const double C0 = 273.15;
        private const double RhoC = 1000 * 4190.0;
        private const double Lhv = 119.96e6;

        private static ValueTable Solve(SketchDocument doc) => ValueSolver.Solve(doc, Fx.Reg(), null, new ValueSolveOptions { BudgetMs = 10000 });

        private static double Val(ValueTable t, string id, string key) => t.Get(id, key)!.Value!.Value;

        private static SketchObject Make(string item, string id) => Fx.Reg().Create(Fx.Reg().Item(item)!, Vec2.Zero) with { Id = id };

        private static SketchObject Without(SketchObject o, string key)
            => o with { Params = o.Params.Where(kv => kv.Key != key).ToDictionary(kv => kv.Key, kv => kv.Value) };

        private static SketchDocument Doc(params SketchObject[] objs)
        {
            var doc = SketchDocument.Create(PageSizes.Slide16x9, "Values");
            return Edits.Add(doc, doc.Pages[0].Id, objs);
        }

        private static SketchObject Line(string item, string id, (string Obj, string Port) a, (string Obj, string Port) b)
            => Make(item, id) with
            {
                Points = new[] { new Vec2(0, 0), new Vec2(10, 0) },
                Attach = new Attachment?[] { new Attachment(a.Obj, 0, 0) { AnchorId = a.Port }, new Attachment(b.Obj, 0, 0) { AnchorId = b.Port } },
            };

        // ------------------------------------------------------------------ hydropower

        [Fact]
        public void HydroPowerFromFlowHeadAndEfficiency()
        {
            var o = Make("energy.hydro.turbine", "h").WithParam("flow", "20 m³/s").WithParam("head", "50 m").WithParam("efficiency", "88 %");
            var t = Solve(Doc(o));
            Assert.Empty(t.Conflicts);
            // P = 1000 kg/m³ · 9.80665 m/s² · 20 m³/s · 50 m · 0.88 = 8.630 MW.
            Assert.Equal(1000 * G0 * 20 * 50 * 0.88, Val(t, "h", "P"), 6);
            Assert.Equal(8.63, Val(t, "h", "P") / 1e6, 2);
            Assert.Equal("energy.hydro.power", t.Get("h", "P")!.Via!.Relation.Id);
            Assert.Equal("S27", t.Get("h", "P")!.Via!.Relation.Source);
        }

        [Fact]
        public void HydroEfficiencyFromPowerFlowAndHead()
        {
            var o = Make("energy.hydro.turbine", "h").WithParam("powerHydro", "1 MW").WithParam("flow", "2,5 m³/s").WithParam("head", "48 m");
            var t = Solve(Doc(o));
            // η = 1e6 W/(1000 · 9.80665 · 2.5 · 48) = 0.8497.
            Assert.Equal(1e6 / (1000 * G0 * 2.5 * 48), Val(t, "h", "eta"), 9);
            // The rated power is a label only: it never contradicts the hydraulics.
            Assert.Empty(t.Conflicts);
        }

        [Fact]
        public void PumpedStorageDefaultsGiveTheGeneratingTime()
        {
            // Inserted as is: 300 MW and 2400 MWh give t = 8 h.
            var t = Solve(Doc(Make("energy.storage.pumped", "ps")));
            Assert.Empty(t.Conflicts);
            Assert.Equal(8 * 3600, Val(t, "ps", "t"), 6);
        }

        [Fact]
        public void PumpedStoragePotentialAndElectricalEnergy()
        {
            var o = Without(Make("energy.storage.pumped", "ps"), "capacity")
                .WithParam("reservoirVolume", "1500000 m³").WithParam("head", "300 m").WithParam("efficiency", "85 %");
            var t = Solve(Doc(o));
            Assert.Empty(t.Conflicts);
            // E = 1000 · 9.80665 · 1.5e6 m³ · 300 m = 4.413e12 J = 1225.8 MWh; E_el = 0.85 · E = 1041.9 MWh; t = E_el/300 MW = 3.47 h.
            var e = 1000 * G0 * 1.5e6 * 300;
            Assert.Equal(e, Val(t, "ps", "E"), 0);
            Assert.Equal(1225.8, Val(t, "ps", "E") / 3.6e9, 1);
            Assert.Equal(0.85 * e, Val(t, "ps", "Eel"), 0);
            Assert.Equal(0.85 * e / 300e6, Val(t, "ps", "t"), 6);
            Assert.Equal("energy.pumped.potential", t.Get("ps", "E")!.Via!.Relation.Id);
        }

        [Fact]
        public void PumpedStorageEnteredCapacityGivesTheVolume()
        {
            // 2400 MWh (default) at H = 500 m and η = 0.8: V = E_el/(η·ρ·g·H) = 2.20 million m³.
            var o = Make("energy.storage.pumped", "ps").WithParam("head", "500 m").WithParam("efficiency", "80 %");
            var t = Solve(Doc(o));
            Assert.Empty(t.Conflicts);
            Assert.Equal(2400 * 3.6e9 / (0.8 * 1000 * G0 * 500), Val(t, "ps", "V"), 3);
        }

        // ------------------------------------------------------------------ hydrogen

        [Fact]
        public void ElectrolyserGivesHydrogenPowerAndMassFlow()
        {
            var o = Make("energy.h2.electrolyser", "ely").WithParam("efficiency", "70 %");
            var t = Solve(Doc(o));
            Assert.Empty(t.Conflicts);
            // P_H2 = 0.7 · 1000 kW = 700 kW; ṁ = 700 kW/(33.32 kWh/kg) = 21.0 kg/h.
            Assert.Equal(700e3, Val(t, "ely", "PH2"), 6);
            Assert.Equal(700e3 / Lhv, Val(t, "ely", "mH2"), 12);
            Assert.Equal(21.0, Val(t, "ely", "mH2") * 3600, 1);
        }

        [Fact]
        public void HydrogenPipeCarriesTheHydrogenPowerToAFuelCell()
        {
            var doc = Doc(
                Make("energy.h2.electrolyser", "ely").WithParam("efficiency", "70 %"),
                Without(Make("energy.h2.fuelcell", "fc"), "power").WithParam("efficiency", "50 %").WithParam("efficiencyTh", "40 %"),
                Line("energy.line.pipe.hydrogen", "p1", ("ely", "h2"), ("fc", "h2")));
            var t = Solve(doc);
            Assert.Empty(t.Conflicts);
            // P_H2 = 700 kW passed on; the fuel cell gives P_el = 0.5 · 700 kW = 350 kW and Φ = 0.4 · 700 kW = 280 kW.
            Assert.Equal(700e3, Val(t, "fc", "PH2"), 6);
            Assert.Equal(Provenance.Linked, t.Get("fc", "PH2")!.Provenance);
            Assert.Equal(350e3, Val(t, "fc", "Pel"), 6);
            Assert.Equal(280e3, Val(t, "fc", "Phi"), 6);
            Assert.Equal(700e3 / Lhv, Val(t, "fc", "mH2"), 12);
        }

        [Fact]
        public void FuelCellHeatGivesTheHeatingWaterFlow()
        {
            var o = Make("energy.h2.fuelcell", "fc").WithParam("heatOutput", "80 kW").WithParam("flowTemp", "70 °C").WithParam("returnTemp", "50 °C");
            var t = Solve(Doc(o));
            Assert.Empty(t.Conflicts);
            // V̇ = Φ/(ρ·c·Δθ) = 80 kW/(1000 · 4190 · 20 K) = 9.55e-4 m³/s = 3.44 m³/h.
            Assert.Equal(80e3 / (RhoC * 20), Val(t, "fc", "Vdot"), 12);
        }

        // ------------------------------------------------------------------ heat side

        [Fact]
        public void ChpEfficienciesFromTheFuelPower()
        {
            var t = Solve(Doc(Make("energy.heat.chp", "chp").WithParam("powerFuel", "150 kW")));
            Assert.Empty(t.Conflicts);
            // Defaults 50 kWel and 80 kWth: η_el = 50/150 = 0.333, η_th = 80/150 = 0.533.
            Assert.Equal(50.0 / 150, Val(t, "chp", "etael"), 9);
            Assert.Equal(80.0 / 150, Val(t, "chp", "etath"), 9);
        }

        [Fact]
        public void HeatPumpGivesTheHeatingWaterFlow()
        {
            var o = Make("energy.heat.heatpump", "hp").WithParam("flowTemp", "35 °C").WithParam("returnTemp", "30 °C");
            var t = Solve(Doc(o));
            Assert.Empty(t.Conflicts);
            // V̇ = 8 kW/(1000 · 4190 · 5 K) = 3.82e-4 m³/s = 1.37 m³/h.
            Assert.Equal(8000 / (RhoC * 5), Val(t, "hp", "Vdot"), 12);
            Assert.Equal("energy.heat.carrier", t.Get("hp", "Vdot")!.Via!.Relation.Id);
        }

        [Fact]
        public void ThermalStorageHoldsTheHeatOfItsVolume()
        {
            var t = Solve(Doc(Make("energy.heat.storage", "st").WithParam("deltaT", "30 K")));
            Assert.Empty(t.Conflicts);
            // 1000 l (default) over 30 K: Q = 1000 · 1 m³ · 4190 · 30 = 125.7 MJ = 34.9 kWh.
            Assert.Equal(RhoC * 1 * 30, Val(t, "st", "Q"), 3);
            Assert.Equal(34.9, Val(t, "st", "Q") / 3.6e6, 1);
        }

        [Fact]
        public void HeatPipeCarriesTheFlowTemperatureIntoTheStorage()
        {
            var doc = Doc(
                Make("energy.heat.heatpump", "hp").WithParam("flowTemp", "55 °C"),
                Make("energy.heat.storage", "st"),
                Line("energy.line.pipe.heat", "p1", ("hp", "heat"), ("st", "charge")));
            var t = Solve(doc);
            Assert.Empty(t.Conflicts);
            Assert.Equal(55 + C0, Val(t, "st", "th"), 9);
            Assert.Equal(Provenance.Linked, t.Get("st", "th")!.Provenance);
            Assert.Equal("energy.link.theta", t.Get("st", "th")!.Via!.Relation.Id);
        }

        [Theory]
        [InlineData("energy.line.pipe.cooling")]
        [InlineData("energy.line.pipe.gas")]
        public void PipesOfOtherCarriersCarryNoValues(string item)
        {
            var doc = Doc(
                Make("energy.heat.heatpump", "hp").WithParam("flowTemp", "55 °C"),
                Make("energy.heat.storage", "st"),
                Line(item, "p1", ("hp", "heat"), ("st", "charge")));
            Assert.Null(Solve(doc).Get("st", "th")!.Value);
        }

        [Fact]
        public void HeatAndWaterPortsAreDescribedForTheNets()
        {
            var r = Fx.Reg();
            var hp = r.Create(r.Item("energy.heat.heatpump")!, Vec2.Zero);
            Assert.Equal(new[] { ("Vdot", "Vdot"), ("theta", "thf") }, EnergyValues.Nets.PortVars(hp, "heat").Select(v => (v.Class, v.Key)));
            Assert.Empty(EnergyValues.Nets.PortVars(hp, "el"));
            var hydro = r.Create(r.Item("energy.hydro.turbine")!, Vec2.Zero);
            Assert.Equal(new[] { "Q" }, EnergyValues.Nets.PortVars(hydro, "water").Select(v => v.Key));
            Assert.Equal(new[] { "Q" }, EnergyValues.Nets.PortVars(hydro, "tail").Select(v => v.Key));
            // Physical keys for joins with other modules; limits and the hydrogen power stay inside the module.
            Assert.Equal(NetKeys.VolumeFlow, NetKeys.Of(EnergyValues.Nets, "Vdot"));
            Assert.Equal(NetKeys.Temperature, NetKeys.Of(EnergyValues.Nets, "theta"));
            Assert.Equal(NetKeys.Voltage, NetKeys.Of(EnergyValues.Nets, "Uac"));
            Assert.Equal(NetKeys.Power, NetKeys.Of(EnergyValues.Nets, "Pdc"));
            Assert.Null(NetKeys.Of(EnergyValues.Nets, "UocMax"));
            Assert.Null(NetKeys.Of(EnergyValues.Nets, "PH2"));
            // Only cables and heat/hydrogen pipes are net lines.
            Assert.True(EnergyValues.IsNetLine(r.Create(r.Item("energy.line.pipe.heat")!, Vec2.Zero)));
            Assert.True(EnergyValues.IsNetLine(r.Create(r.Item("energy.line.pipe.hydrogen")!, Vec2.Zero)));
            Assert.False(EnergyValues.IsNetLine(r.Create(r.Item("energy.line.pipe.water")!, Vec2.Zero)));
            Assert.False(EnergyValues.IsNetLine(r.Create(r.Item("energy.line.flow.heat")!, Vec2.Zero)));
        }

        // ------------------------------------------------------------------ e-mobility

        [Fact]
        public void ChargingTimeFromEnergyAndPower()
        {
            var t = Solve(Doc(Make("energy.mobility.charger", "wb").WithParam("chargeEnergy", "44 kWh")));
            Assert.Empty(t.Conflicts);
            // 44 kWh at 11 kW (default) = 4 h.
            Assert.Equal(4 * 3600, Val(t, "wb", "t"), 6);
            Assert.Equal("energy.charger.time", t.Get("wb", "t")!.Via!.Relation.Id);
            // A 150 kW fast charger: 60 kWh in 24 min.
            var hpc = Solve(Doc(Make("energy.mobility.charger.hpc", "hpc").WithParam("chargeEnergy", "60 kWh")));
            Assert.Equal(24 * 60, Val(hpc, "hpc", "t"), 6);
        }
    }
}
