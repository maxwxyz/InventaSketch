using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Process.Tests
{
    /// <summary>
    /// Calc 4 process and thermal: pump power, heat exchanger balance and LMTD, vessel heat, flows and temperatures passed
    /// along process lines, and the temperature-cross check. Every expected value is a hand calculation.
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

        private static SketchObject Line(Registry r, string id, (string Obj, string Port) a, (string Obj, string Port) b)
            => r.Create(r.Item("process.line")!, Vec2.Zero) with
            {
                Id = id,
                Points = new[] { new Vec2(0, 0), new Vec2(10, 0) },
                Attach = new Attachment?[] { new Attachment(a.Obj, 0, 0) { AnchorId = a.Port }, new Attachment(b.Obj, 0, 0) { AnchorId = b.Port } },
            };

        private static IReadOnlyList<CheckFinding> Checks(Registry r, SketchDocument doc) => ValueSolver.Checks(Solve(doc, r), r, new RenderContext(doc));

        private const double C0 = 273.15;

        [Fact]
        public void ValueTypesDeclareBilingualVariablesWithParametersAndSources()
        {
            var r = Fx.Reg();
            foreach (var id in new[] { "process.pump.centrifugal", "process.pump.displacement", "process.fan", "process.compressor", "process.turbine", "process.motor", "process.hx.shelltube", "process.hx.plate", "process.hx.general", "process.hx.aircooler", "process.hx.coolingtower", "process.vessel.vertical", "process.vessel.tank", "process.line" })
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
            Assert.False(ValueModel.HasValues(r.Type("process.valve.manual")));
        }

        [Fact]
        public void EveryItemStillInsertsWithoutValuesAndShowsNoCheck()
        {
            var r = Fx.Reg();
            var objs = r.Library.Select((item, i) => r.Create(item, new Vec2(i % 12 * 60, i / 12 * 60)) with { Id = "o" + i }).ToArray();
            var doc = Doc(objs);
            var table = Solve(doc, r);
            Assert.Empty(table.Conflicts);
            Assert.Empty(ValueSolver.Checks(table, r, new RenderContext(doc)));
        }

        [Fact]
        public void CentrifugalPumpPowerFromFlowHeadAndEfficiency()
        {
            var r = Fx.Reg();
            var p = Make(r, "process.pump.centrifugal", "p").WithParam("flowRate", "10 m³/h").WithParam("head", "30 m").WithParam("efficiency", "70 %");
            var t = Solve(Doc(p), r);
            // P_hyd = ρ·g·H·V̇ = 1000 · 9.80665 · 30 · 10/3600 = 817.2 W; P = P_hyd/η = 1167.5 W.
            var phyd = 1000 * 9.80665 * 30 * 10 / 3600.0;
            Assert.Equal(phyd, Val(t, "p", "Phyd"), 6);
            Assert.Equal(phyd / 0.7, Val(t, "p", "P"), 6);
            Assert.Equal(Provenance.Default, t.Get("p", "rho")!.Provenance);
        }

        [Fact]
        public void HeatExchangerBalanceLmtdAndArea()
        {
            var r = Fx.Reg();
            var hx = Make(r, "process.hx.shelltube", "hx").WithParam("massFlow1", "2000 kg/h").WithParam("temp1In", "90 °C").WithParam("temp1Out", "70 °C")
                .WithParam("temp2In", "20 °C").WithParam("temp2Out", "50 °C").WithParam("u", "1000 W/(m²·K)");
            var t = Solve(Doc(hx), r);
            Assert.Empty(t.Conflicts);
            // Φ = ṁ₁·c·Δθ₁ = 2000/3600 · 4190 · 20 = 46.56 kW; Δθ_lm = (40 − 50)/ln(40/50) = 44.81 K; A = Φ/(U·Δθ_lm) = 1.039 m².
            var phi = 2000 / 3600.0 * 4190 * 20;
            var lmtd = (40 - 50) / Math.Log(40 / 50.0);
            Assert.Equal(phi, Val(t, "hx", "Phi"), 6);
            Assert.Equal(lmtd, Val(t, "hx", "dTlm"), 9);
            Assert.Equal(phi / (1000 * lmtd), Val(t, "hx", "A"), 9);
            // ṁ₂ = Φ/(c·Δθ₂) = 46556/(4190·30) = 0.3704 kg/s.
            Assert.Equal(phi / (4190 * 30), Val(t, "hx", "m2"), 9);
        }

        [Fact]
        public void FlowAndTemperatureRunAlongProcessLines()
        {
            var r = Fx.Reg();
            var doc = Doc(
                Make(r, "process.pump.centrifugal", "pump").WithParam("flowRate", "2 m³/h"),
                Make(r, "process.valve.manual", "valve"),
                Make(r, "process.hx.shelltube", "hx").WithParam("temp1In", "90 °C").WithParam("temp1Out", "70 °C"),
                Make(r, "process.hx.aircooler", "cooler").WithParam("tempOut", "40 °C"),
                Line(r, "l1", ("pump", "D"), ("valve", "1")), Line(r, "l2", ("valve", "2"), ("hx", "T1")), Line(r, "l3", ("hx", "T2"), ("cooler", "P1")));
            var t = Solve(doc, r);
            Assert.Empty(t.Conflicts);
            // ṁ = ρ·V̇ = 1000 · 2/3600 = 0.5556 kg/s reaches side 1 through the valve; Φ = 0.5556 · 4190 · 20 = 46.56 kW.
            Assert.Equal(2000 / 3600.0, Val(t, "hx", "m1"), 9);
            Assert.Equal(Provenance.Linked, t.Get("hx", "m1")!.Provenance);
            Assert.Equal(2000 / 3600.0 * 4190 * 20, Val(t, "hx", "Phi"), 6);
            // The cooler takes the outlet temperature and the mass flow: Φ = 0.5556 · 4190 · (70 − 40) = 69.83 kW.
            Assert.Equal(70 + C0, Val(t, "cooler", "thin"), 9);
            Assert.Equal(2000 / 3600.0 * 4190 * 30, Val(t, "cooler", "Phi"), 6);
            Assert.Equal(70 + C0, Val(t, "l3", "theta"), 9);
        }

        [Fact]
        public void VesselVolumeAndStoredHeat()
        {
            var r = Fx.Reg();
            var v = Make(r, "process.vessel.vertical", "v").WithParam("diameter", "2 m").WithParam("length", "3 m").WithParam("deltaT", "40 K");
            var t = Solve(Doc(v), r);
            // V = π·2²·3/4 = 9.425 m³; Q = ρ·V·c·Δθ = 1000 · 9.425 · 4190 · 40 = 1.580 GJ = 438.8 kWh.
            var vol = Math.PI * 4 * 3 / 4;
            Assert.Equal(vol, Val(t, "v", "V"), 9);
            Assert.Equal(1000 * vol * 4190 * 40, Val(t, "v", "Q"), 0);
        }

        [Fact]
        public void TemperatureCrossGivesTheWarning()
        {
            var r = Fx.Reg();
            var hx = Make(r, "process.hx.plate", "hx").WithParam("temp1In", "90 °C").WithParam("temp1Out", "60 °C")
                .WithParam("temp2In", "20 °C").WithParam("temp2Out", "95 °C");
            var f = Checks(r, Doc(hx)).Single();
            Assert.Equal("heat.cross", f.CheckId);
            Assert.Equal(CheckSeverity.Warning, f.Severity);
            Assert.Equal("S29", f.Source);
            var ok = hx.WithParam("temp2Out", "50 °C");
            Assert.Empty(Checks(r, Doc(ok)));
        }
    }
}
