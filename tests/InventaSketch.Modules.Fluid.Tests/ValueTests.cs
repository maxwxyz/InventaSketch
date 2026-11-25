using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Fluid.Tests
{
    /// <summary>
    /// Calc 4 fluid power: pump, motor and cylinder relations, flow and pressure passed along lines and through valves, and the
    /// checks. Every expected value is a hand calculation.
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

        /// <summary>A fluid line docked on two ports.</summary>
        private static SketchObject Line(Registry r, string id, (string Obj, string Port) a, (string Obj, string Port) b)
            => r.Create(r.Item("fluid.line")!, Vec2.Zero) with
            {
                Id = id,
                Points = new[] { new Vec2(0, 0), new Vec2(10, 0) },
                Attach = new Attachment?[] { new Attachment(a.Obj, 0, 0) { AnchorId = a.Port }, new Attachment(b.Obj, 0, 0) { AnchorId = b.Port } },
            };

        private static IReadOnlyList<CheckFinding> Checks(Registry r, SketchDocument doc) => ValueSolver.Checks(Solve(doc, r), r, new RenderContext(doc));

        private const double Lpm = 1e-3 / 60;
        private const double Bar = 1e5;

        // ------------------------------------------------------------------ declarations

        [Fact]
        public void ValueTypesDeclareBilingualVariablesWithParametersAndSources()
        {
            var r = Fx.Reg();
            foreach (var id in new[] { "fluid.pump", "fluid.motor", "fluid.cylinder.double", "fluid.cylinder.single", "fluid.rotary", "fluid.valve.relief", "fluid.valve.reducing", "fluid.gauge", "fluid.flowmeter", "fluid.accumulator", "fluid.line" })
            {
                var t = r.Type(id)!;
                Assert.True(ValueModel.HasValues(t), id);
                Assert.True(ValueModel.Variables(t).Count(v => v.Relevance == VarRelevance.Card) <= 4, id);
                foreach (var v in ValueModel.Variables(t))
                {
                    Assert.False(string.IsNullOrEmpty(v.Name.De), id + "." + v.Key);
                    Assert.Contains(t.Parameters, p => p.Key == v.ParamKey);
                    Assert.Single(t.Parameters, p => p.Key == v.ParamKey);
                }
                Assert.All(ValueModel.Relations(t), rel => Assert.NotNull(rel.Source));
            }
            Assert.False(ValueModel.HasValues(r.Type("fluid.tank")));
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

        // ------------------------------------------------------------------ accept: pump → valve → 50/28 cylinder

        /// <summary>Pump P → junction (relief valve on the tee) → 4/3 valve P; valve A/B → cylinder A/B.</summary>
        private static (Registry R, SketchDocument Doc) Circuit(Func<SketchObject, SketchObject>? pump = null, Func<SketchObject, SketchObject>? cyl = null, bool relief = true)
        {
            var r = Fx.Reg();
            var p = Make(r, "fluid.pump", "pump").WithParam("displacement", "10 cm³").WithParam("speed", "1450 min⁻¹");
            var c = Make(r, "fluid.cylinder.double", "cyl").WithParam("bore", "50 mm").WithParam("rodDiameter", "28");
            var objs = new List<SketchObject>
            {
                pump?.Invoke(p) ?? p, cyl?.Invoke(c) ?? c, Make(r, "fluid.valve.43", "valve"), Make(r, "fluid.junction", "dot"), Make(r, "fluid.tank", "tank"),
                Line(r, "l1", ("pump", "P"), ("dot", "1")), Line(r, "l2", ("dot", "1"), ("valve", "P")),
                Line(r, "l3", ("valve", "A"), ("cyl", "A")), Line(r, "l4", ("valve", "B"), ("cyl", "B")),
                Line(r, "l5", ("valve", "T"), ("tank", "T")),
            };
            if (relief)
            {
                objs.Add(Make(r, "fluid.relief", "relief").WithParam("setPressure", "160 bar"));
                objs.Add(Line(r, "l6", ("dot", "1"), ("relief", "P")));
            }
            return (r, Doc(objs.ToArray()));
        }

        [Fact]
        public void PumpFeedingA50By28CylinderAt100BarShowsForceAndSpeedBothWays()
        {
            var (r, doc) = Circuit(pump: p => p.WithParam("pressure", "100 bar"));
            var t = Solve(doc, r);
            Assert.Empty(t.Conflicts);
            // Q = V_g·n = 10 cm³ · 1450 min⁻¹ = 14.5 l/min.
            Assert.Equal(14.5 * Lpm, Val(t, "pump", "Q"), 9);
            Assert.Equal(14.5 * Lpm, Val(t, "cyl", "Q"), 9);
            Assert.Equal(Provenance.Linked, t.Get("cyl", "Q")!.Provenance);
            Assert.Equal(100 * Bar, Val(t, "cyl", "p"), 3);
            // A₁ = π·50²/4 = 1963.50 mm², A₂ = π·(50² − 28²)/4 = 1347.74 mm².
            var a1 = Math.PI * 0.05 * 0.05 / 4;
            var a2 = Math.PI * (0.05 * 0.05 - 0.028 * 0.028) / 4;
            Assert.Equal(a1, Val(t, "cyl", "A1"), 12);
            Assert.Equal(a2, Val(t, "cyl", "A2"), 12);
            // F = p·A: 19.63 kN out, 13.48 kN in; v = Q/A: 0.123 m/s out, 0.179 m/s in.
            Assert.Equal(19634.95, Val(t, "cyl", "Fext"), 1);
            Assert.Equal(13477.39, Val(t, "cyl", "Fret"), 1);
            Assert.Equal(0.12308, Val(t, "cyl", "vext"), 5);
            Assert.Equal(0.17931, Val(t, "cyl", "vret"), 5);
            Assert.Equal(a1 / a2, Val(t, "cyl", "phi"), 9);
            // Hydraulic power p·Q = 100 bar · 14.5 l/min = 2.417 kW (= p[bar]·Q[l/min]/600 kW).
            Assert.Equal(100 * 14.5 / 600 * 1000, Val(t, "pump", "Phyd"), 6);
            Assert.Empty(Checks(r, doc));
        }

        [Fact]
        public void PressureEnteredAtTheCylinderReachesThePump()
        {
            var (r, doc) = Circuit(cyl: c => c.WithParam("pressure", "100 bar"));
            var t = Solve(doc, r);
            Assert.Equal(100 * Bar, Val(t, "pump", "p"), 3);
            Assert.Equal(Provenance.Linked, t.Get("pump", "p")!.Provenance);
            // Drive torque T = V_g·p/(2π) = 10e-6 m³ · 1e7 Pa / 2π = 15.92 N·m.
            Assert.Equal(10e-6 * 1e7 / (2 * Math.PI), Val(t, "pump", "T"), 6);
            Assert.Equal(19634.95, Val(t, "cyl", "Fext"), 1);
        }

        [Fact]
        public void ThroughRodCylinderHasEqualAreas()
        {
            var r = Fx.Reg();
            var c = Make(r, "fluid.cylinder.double.through", "c").WithParam("bore", "50").WithParam("rodDiameter", "28").WithParam("pressure", "100 bar");
            var t = Solve(Doc(c), r);
            Assert.Equal(Val(t, "c", "A2"), Val(t, "c", "A1"), 12);
            Assert.Equal(1, Val(t, "c", "phi"), 9);
            Assert.Equal(13477.39, Val(t, "c", "Fext"), 1);
        }

        [Fact]
        public void StrokeTimeFromStrokeAndSpeed()
        {
            var r = Fx.Reg();
            var c = Make(r, "fluid.cylinder.double", "c").WithParam("bore", "50").WithParam("rodDiameter", "28")
                .WithParam("flowRate", "14,5 l/min").WithParam("stroke", "300 mm");
            var t = Solve(Doc(c), r);
            // t = s/v = 0.3 m / 0.12308 m/s = 2.437 s.
            Assert.Equal(0.3 / (14.5 * Lpm / (Math.PI * 0.05 * 0.05 / 4)), Val(t, "c", "text"), 6);
        }

        // ------------------------------------------------------------------ motor, line, accumulator

        [Fact]
        public void MotorSpeedAndTorqueFromFlowAndPressure()
        {
            var r = Fx.Reg();
            var m = Make(r, "fluid.motor", "m").WithParam("displacement", "10 cm³").WithParam("flowRate", "20 l/min").WithParam("pressure", "100 bar")
                .WithParam("efficiencyV", "95 %");
            var t = Solve(Doc(m), r);
            // n = Q·η_v/V_g = 20 l/min · 0.95 / 10 cm³ = 1900 min⁻¹; T = V_g·p/(2π) = 15.92 N·m.
            Assert.Equal(1900 * 2 * Math.PI / 60, Val(t, "m", "n"), 6);
            Assert.Equal(10e-6 * 1e7 / (2 * Math.PI), Val(t, "m", "T"), 6);
            // P = P_hyd·η_v·η_hm = 1e7 Pa · 20 l/min · 0.95 = 3.167 kW = 2π·n·T.
            Assert.Equal(1e7 * 20 * Lpm * 0.95, Val(t, "m", "P"), 6);
        }

        [Fact]
        public void LineVelocityFromFlowAndInnerDiameterAndTheVelocityHint()
        {
            var r = Fx.Reg();
            var pump = Make(r, "fluid.pump", "pump").WithParam("flowRate", "14,5 l/min");
            var gauge = Make(r, "fluid.gauge", "g");
            var relief = Make(r, "fluid.relief", "relief");
            var dot = Make(r, "fluid.junction", "dot");
            SketchDocument Build(string di) => Doc(pump, gauge, relief, dot,
                Line(r, "l1", ("pump", "P"), ("g", "P")) .WithParam("innerDiameter", di));
            var t = Solve(Build("10 mm"), r);
            // v = 4·Q/(π·d²) = 4 · 2.4167e-4 / (π · 0.01²) = 3.077 m/s: inside 3–6 m/s for a pressure line.
            Assert.Equal(4 * 14.5 * Lpm / (Math.PI * 0.01 * 0.01), Val(t, "l1", "v"), 6);
            Assert.DoesNotContain(Checks(r, Build("10 mm")), f => f.CheckId == "fluid.velocity");
            // d = 6 mm: 8.55 m/s.
            Assert.Contains(Checks(r, Build("6 mm")), f => f.CheckId == "fluid.velocity" && f.ObjectId == "l1");
        }

        [Fact]
        public void AccumulatorUsableVolumeIsothermal()
        {
            var r = Fx.Reg();
            var a = Make(r, "fluid.accumulator", "a").WithParam("volume", "4 l").WithParam("p0", "40 bar").WithParam("p1", "60 bar").WithParam("p2", "150 bar");
            var t = Solve(Doc(a), r);
            // V₁ = 4·40/60 = 2.667 l, V₂ = 4·40/150 = 1.067 l, ΔV = 1.6 l.
            Assert.Equal(1.6e-3, Val(t, "a", "dV"), 9);
        }

        // ------------------------------------------------------------------ pass-through

        [Fact]
        public void ThrottlePassesTheFlowButNotThePressure()
        {
            var r = Fx.Reg();
            var doc = Doc(
                Make(r, "fluid.pump", "pump").WithParam("flowRate", "10 l/min").WithParam("pressure", "50 bar"),
                Make(r, "fluid.throttle", "thr"),
                Make(r, "fluid.motor", "m"),
                Line(r, "l1", ("pump", "P"), ("thr", "P")), Line(r, "l2", ("thr", "A"), ("m", "A")));
            var t = Solve(doc, r);
            Assert.Equal(10 * Lpm, Val(t, "m", "Q"), 9);
            Assert.Null(t.Get("m", "p")!.Value);
            Assert.Equal(10 * Lpm, Val(t, "l2", "Q"), 9);
            Assert.Equal(50 * Bar, Val(t, "l1", "p"), 3);
        }

        [Fact]
        public void TwoCylindersOnOneValveDoNotShareTheFlow()
        {
            var r = Fx.Reg();
            var doc = Doc(
                Make(r, "fluid.pump", "pump").WithParam("flowRate", "10 l/min"),
                Make(r, "fluid.cylinder.double", "c1"), Make(r, "fluid.cylinder.double", "c2"), Make(r, "fluid.junction", "dot"),
                Line(r, "l1", ("pump", "P"), ("dot", "1")), Line(r, "l2", ("dot", "1"), ("c1", "A")), Line(r, "l3", ("dot", "1"), ("c2", "A")));
            var t = Solve(doc, r);
            Assert.Null(t.Get("c1", "Q")!.Value);
            Assert.Empty(t.Conflicts);
        }

        // ------------------------------------------------------------------ checks

        [Fact]
        public void CylinderForceCheckUsesTheReliefSetting()
        {
            // Required 25 kN: 100 bar · 1963.5 mm² = 19.6 kN is too little.
            var (r, doc) = Circuit(pump: p => p.WithParam("pressure", "100 bar"), cyl: c => c.WithParam("forceReq", "25 kN"), relief: false);
            Assert.Contains(Checks(r, doc), f => f.CheckId == "fluid.cyl.force" && f.ObjectId == "cyl" && f.Source == "S26");
            // With the relief valve at 160 bar: p_max·A = 31.4 kN covers it.
            var (r2, doc2) = Circuit(pump: p => p.WithParam("pressure", "100 bar"), cyl: c => c.WithParam("forceReq", "25 kN"));
            var t = Solve(doc2, r2);
            Assert.Equal(160 * Bar, Val(t, "cyl", "pmax"), 3);
            Assert.DoesNotContain(Checks(r2, doc2), f => f.CheckId == "fluid.cyl.force");
        }

        [Fact]
        public void PumpWithoutReliefValveGivesTheHint()
        {
            var (r, doc) = Circuit(pump: p => p.WithParam("pressure", "100 bar"), relief: false);
            var found = Checks(r, doc).Single(f => f.CheckId == "fluid.pressure.relief");
            Assert.Equal("pump", found.ObjectId);
            Assert.Equal(CheckSeverity.Hint, found.Severity);
            Assert.False(string.IsNullOrEmpty(found.Message.De));
            var (r2, doc2) = Circuit(pump: p => p.WithParam("pressure", "100 bar"));
            Assert.DoesNotContain(Checks(r2, doc2), f => f.CheckId == "fluid.pressure.relief");
        }
    }
}
