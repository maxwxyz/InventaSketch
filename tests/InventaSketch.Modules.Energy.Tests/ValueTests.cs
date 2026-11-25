using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Energy.Tests
{
    /// <summary>
    /// Calc 4 energy and solar: PV string voltage at the lowest temperature, inverter limits, the PV → inverter → board chain
    /// with the AC current per phase, battery, charger, heat pump, wind turbine, transformer and the checks. Every expected
    /// value is a hand calculation.
    /// </summary>
    public class ValueTests
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

        private static readonly double Sqrt3 = Math.Sqrt(3);

        /// <summary>The accept string: 10 modules, U_oc = 49.5 V, β = −0.27 %/K, θ_min = −10 °C.</summary>
        private static SketchObject AcceptString(Registry r, string id = "str")
            => Make(r, "energy.pv.string", id).WithParam("modules", 10).WithParam("voc", "49,5 V").WithParam("betaVoc", "-0,27").WithParam("tmin", "-10 °C");

        [Fact]
        public void ValueTypesDeclareBilingualVariablesWithParametersAndSources()
        {
            var r = Fx.Reg();
            foreach (var id in new[] { "energy.pv.module", "energy.pv.string", "energy.pv.array", "energy.conv.inverter", "energy.grid.busbar", "energy.grid.meter", "energy.storage.battery", "energy.mobility.charger", "energy.heat.heatpump", "energy.wind.turbine", "energy.grid.transformer" })
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
            Assert.False(ValueModel.HasValues(r.Type("energy.grid.breaker")));
        }

        [Fact]
        public void EveryItemStillInsertsWithoutValuesAndShowsNoCheck()
        {
            var r = Fx.Reg();
            var objs = r.Library.Select((item, i) => r.Create(item, new Vec2(i % 12 * 80, i / 12 * 80)) with { Id = "o" + i }).ToArray();
            var doc = Doc(objs);
            var table = Solve(doc, r);
            Assert.Empty(table.Conflicts);
            Assert.Empty(ValueSolver.Checks(table, r, new RenderContext(doc)));
        }

        // ------------------------------------------------------------------ accept: string voltage and inverter limit

        [Fact]
        public void StringOfTenModulesShowsUocMaxAndWarnsAgainstA500VInverter()
        {
            var r = Fx.Reg();
            var doc = Doc(AcceptString(r), Make(r, "energy.conv.inverter", "inv").WithParam("udcMax", "500 V"), Cable(r, "c1", ("str", "dc"), ("inv", "dc")));
            var t = Solve(doc, r);
            Assert.Empty(t.Conflicts);
            // U_oc,max = 10 · 49.5 V · (1 + (−0.0027/K) · (−10 − 25) K) = 495 V · 1.0945 = 541.78 V.
            Assert.Equal(10 * 49.5 * (1 - 0.0027 * -35), Val(t, "str", "UocMax"), 9);
            Assert.Equal(541.8, Val(t, "str", "UocMax"), 1);
            Assert.Equal(541.7775, Val(t, "inv", "UocMax"), 6);
            Assert.Equal(Provenance.Linked, t.Get("inv", "UocMax")!.Provenance);
            var f = Checks(r, doc).Single(x => x.CheckId == "pv.voc.max");
            Assert.Equal("inv", f.ObjectId);
            Assert.Equal(CheckSeverity.Warning, f.Severity);
            Assert.Equal("S33", f.Source);
            Assert.False(string.IsNullOrEmpty(f.Message.De));
            // 9 modules: 487.6 V, no warning.
            var nine = Doc(AcceptString(r).WithParam("modules", 9), Make(r, "energy.conv.inverter", "inv").WithParam("udcMax", "500 V"), Cable(r, "c1", ("str", "dc"), ("inv", "dc")));
            Assert.DoesNotContain(Checks(r, nine), x => x.CheckId == "pv.voc.max");
        }

        [Fact]
        public void ModuleVoltageAtTheLowestTemperature()
        {
            var r = Fx.Reg();
            var m = Make(r, "energy.pv.module", "m").WithParam("voc", "49,5 V").WithParam("betaVoc", "-0,27 %").WithParam("tmin", "-10 °C")
                .WithParam("vmp", "41,5 V").WithParam("imp", "10,36 A");
            var t = Solve(Doc(m), r);
            Assert.Equal(49.5 * 1.0945, Val(t, "m", "UocMax"), 9);
            // 41.5 V · 10.36 A = 429.9 W agrees with the stored 430 Wp within the 2 % datasheet tolerance.
            Assert.Empty(t.Conflicts);
        }

        // ------------------------------------------------------------------ accept: PV → inverter → board

        private static SketchDocument Chain(Registry r, string phases = "3", bool meter = false)
        {
            var objs = new List<SketchObject>
            {
                Make(r, "energy.pv.string", "str").WithParam("modules", 10).WithParam("power", 400.0),
                Make(r, "energy.conv.inverter", "inv").WithParam("efficiency", "96 %").WithParam("phases", phases).WithParam("power", 4.0),
                Make(r, "energy.grid.busbar", "bus"),
                Cable(r, "c1", ("str", "dc"), ("inv", "dc")),
            };
            if (meter)
            {
                objs.Add(Make(r, "energy.grid.meter", "meter"));
                objs.Add(Cable(r, "c2", ("inv", "ac"), ("meter", "b")));
                objs.Add(Cable(r, "c3", ("meter", "a"), ("bus", "t2")));
            }
            else objs.Add(Cable(r, "c2", ("inv", "ac"), ("bus", "t1")));
            return Doc(objs.ToArray());
        }

        [Fact]
        public void PvInverterBoardChainShowsTheAcCurrentPerPhase()
        {
            var r = Fx.Reg();
            var t = Solve(Chain(r), r);
            Assert.Empty(t.Conflicts);
            // P_DC = 10 · 400 Wp = 4 kW; P_AC = 0.96 · 4 kW = 3.84 kW; at the 0.4 kV busbar I = 3840 W/(√3 · 400 V) = 5.54 A.
            Assert.Equal(4000, Val(t, "str", "P"), 9);
            Assert.Equal(4000, Val(t, "inv", "Pdc"), 9);
            Assert.Equal(3840, Val(t, "inv", "Pac"), 9);
            Assert.Equal(3840, Val(t, "bus", "P"), 9);
            Assert.Equal(400, Val(t, "inv", "Uac"), 9);
            Assert.Equal(Provenance.Linked, t.Get("inv", "Uac")!.Provenance);
            Assert.Equal(3840 / (Sqrt3 * 400), Val(t, "bus", "I"), 9);
            Assert.Equal(3840 / (Sqrt3 * 400), Val(t, "inv", "Iac"), 9);
            // DC/AC ratio = 4 kWp / 4 kW = 1.0: inside 0.8–1.5.
            Assert.Equal(1, Val(t, "inv", "R"), 9);
            Assert.Empty(Checks(r, Chain(r)));
        }

        [Fact]
        public void SinglePhaseInverterAndMeterBetweenInverterAndBusbar()
        {
            var r = Fx.Reg();
            var t = Solve(Chain(r, "1", meter: true), r);
            Assert.Empty(t.Conflicts);
            // Single-phase on a 400 V system: I = 3840 W/(400 V/√3) = 16.6 A; the meter and the busbar see 3.84 kW.
            Assert.Equal(3840 * Sqrt3 / 400, Val(t, "inv", "Iac"), 9);
            Assert.Equal(3840, Val(t, "meter", "P"), 9);
            Assert.Equal(3840, Val(t, "bus", "P"), 9);
            Assert.Equal(400, Val(t, "meter", "U"), 9);
        }

        [Fact]
        public void TwoInvertersAddUpOnTheBusbar()
        {
            var r = Fx.Reg();
            var doc = Doc(
                Make(r, "energy.conv.inverter", "i1").WithParam("powerAc", "5 kW"),
                Make(r, "energy.conv.inverter", "i2").WithParam("powerAc", "3 kW"),
                Make(r, "energy.grid.busbar", "bus"),
                Cable(r, "c1", ("i1", "ac"), ("bus", "t1")), Cable(r, "c2", ("i2", "ac"), ("bus", "t3")));
            var t = Solve(doc, r);
            Assert.Equal(8000, Val(t, "bus", "P"), 9);
            Assert.Equal(8000 / (Sqrt3 * 400), Val(t, "bus", "I"), 9);
        }

        [Fact]
        public void ParallelStringsAddCurrentAndPowerAndTheCurrentLimitWarns()
        {
            var r = Fx.Reg();
            SketchObject S(string id) => Make(r, "energy.pv.string", id).WithParam("modules", 10).WithParam("power", 400.0).WithParam("isc", "11 A");
            var doc = Doc(S("s1"), S("s2"), Make(r, "energy.conv.inverter", "inv").WithParam("idcMax", "20 A").WithParam("power", 8.0),
                Cable(r, "c1", ("s1", "dc"), ("inv", "dc")), Cable(r, "c2", ("s2", "dc"), ("inv", "dc")));
            var t = Solve(doc, r);
            // Two strings in parallel: I_sc = 2 · 11 A = 22 A > 20 A; P_DC = 2 · 4 kWp = 8 kWp.
            Assert.Equal(22, Val(t, "inv", "Isc"), 9);
            Assert.Equal(8000, Val(t, "inv", "Pdc"), 9);
            Assert.Contains(Checks(r, doc), f => f.CheckId == "pv.isc.max" && f.ObjectId == "inv");
        }

        [Fact]
        public void MppVoltageAtTheHighestTemperatureBelowTheMpptRangeAndTheDcAcInfo()
        {
            var r = Fx.Reg();
            var doc = Doc(
                Make(r, "energy.pv.string", "s").WithParam("modules", 6).WithParam("vmp", "41,5 V").WithParam("betaVoc", "-0,27").WithParam("tmax", "70 °C"),
                Make(r, "energy.conv.inverter", "inv").WithParam("mpptMin", "250 V"),
                Cable(r, "c1", ("s", "dc"), ("inv", "dc")));
            var t = Solve(doc, r);
            // U_mpp,min = 6 · 41.5 V · (1 − 0.0027 · 45) = 249 V · 0.8785 = 218.7 V < 250 V.
            Assert.Equal(6 * 41.5 * (1 - 0.0027 * 45), Val(t, "inv", "UmppMin"), 9);
            var checks = Checks(r, doc);
            Assert.Contains(checks, f => f.CheckId == "pv.mpp.min");
            // 6 · 430 Wp = 2.58 kWp on the default 10 kW inverter: DC/AC ratio 0.26.
            Assert.Contains(checks, f => f.CheckId == "pv.dcac" && f.Severity == CheckSeverity.Info);
        }

        // ------------------------------------------------------------------ other elements

        [Fact]
        public void BatteryEnergyCRateAndRunTime()
        {
            var r = Fx.Reg();
            var b = Make(r, "energy.storage.battery", "b").WithParam("voltage", "48 V").WithParam("ah", "200 Ah").WithParam("capacity", 8.64)
                .WithParam("powerBat", "4,8 kW").WithParam("cmax", "0,5");
            var t = Solve(Doc(b), r);
            Assert.Empty(t.Conflicts);
            // E = 48 V · 200 Ah = 9.6 kWh; DoD = 8.64/9.6 = 90 %; I = 4800/48 = 100 A; C = 100 A/200 Ah = 0.5; t = 8.64 kWh/4.8 kW = 1.8 h.
            Assert.Equal(9.6 * 3.6e6, Val(t, "b", "E"), 3);
            Assert.Equal(0.9, Val(t, "b", "dod"), 9);
            Assert.Equal(0.5, Val(t, "b", "C"), 9);
            Assert.Equal(1.8 * 3600, Val(t, "b", "t"), 6);
            Assert.Empty(Checks(r, Doc(b)));
            Assert.Contains(Checks(r, Doc(b.WithParam("powerBat", "9,6 kW"))), f => f.CheckId == "bat.crate");
        }

        [Fact]
        public void ChargerCurrentFromPowerAndVoltage()
        {
            var r = Fx.Reg();
            var t = Solve(Doc(Make(r, "energy.mobility.charger", "c")), r);
            // 11 kW at 400 V three-phase (defaults): I = 11000/(√3 · 400) = 15.9 A.
            Assert.Equal(11000 / (Sqrt3 * 400), Val(t, "c", "I"), 9);
            var dc = Solve(Doc(Make(r, "energy.mobility.charger", "c").WithParam("dc", true).WithParam("power", 50.0).WithParam("voltage", "500 V")), r);
            Assert.Equal(100, Val(dc, "c", "I"), 9);
        }

        [Fact]
        public void HeatPumpCopAndCarnotWarning()
        {
            var r = Fx.Reg();
            var hp = Make(r, "energy.heat.heatpump", "hp").WithParam("cop", "4").WithParam("flowTemp", "35 °C").WithParam("sourceTemp", "0 °C");
            var t = Solve(Doc(hp), r);
            // Φ_H = 8 kW (stored rating) = COP · P_el: P_el = 2 kW.
            Assert.Equal(2000, Val(t, "hp", "Pel"), 9);
            Assert.Empty(Checks(r, Doc(hp)));
            // COP_Carnot = 308.15/35 = 8.8 < 10.
            Assert.Contains(Checks(r, Doc(hp.WithParam("cop", "10"))), f => f.CheckId == "heat.cop.carnot");
        }

        [Fact]
        public void WindPowerAndBetzLimit()
        {
            var r = Fx.Reg();
            var w = Make(r, "energy.wind.turbine", "w").WithParam("diameter", "100 m").WithParam("windSpeed", "10 m/s").WithParam("cp", "0,45");
            var t = Solve(Doc(w), r);
            // P = ½ · 1.225 · π · 100²/4 · 10³ · 0.45 = 2.165 MW.
            Assert.Equal(0.5 * 1.225 * Math.PI * 1e4 / 4 * 1e3 * 0.45, Val(t, "w", "P"), 3);
            Assert.Contains(Checks(r, Doc(w.WithParam("cp", "0,6"))), f => f.CheckId == "wind.betz");
        }

        [Fact]
        public void TransformerCurrentsFromTheStoredRating()
        {
            var r = Fx.Reg();
            var t = Solve(Doc(Make(r, "energy.grid.transformer", "tr")), r);
            // 630 kVA, 20/0.4 kV: I_LV = 630000/(√3 · 400) = 909.3 A, I_HV = 18.2 A.
            Assert.Equal(630000 / (Sqrt3 * 400), Val(t, "tr", "Ilv"), 6);
            Assert.Equal(630000 / (Sqrt3 * 20000), Val(t, "tr", "Ihv"), 6);
        }
    }
}
