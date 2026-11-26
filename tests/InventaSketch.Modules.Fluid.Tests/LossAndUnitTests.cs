using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Fluid.Tests
{
    /// <summary>
    /// Round 5 (2026-11-26): pressure drop in lines (Darcy–Weisbach, laminar/turbulent), throttle orifice flow with the
    /// pressures of its docked lines, the hydraulic power unit and the free air consumption of pneumatic cylinders. Every
    /// expected value is a hand calculation.
    /// </summary>
    public class LossAndUnitTests
    {
        private const double Lpm = 1e-3 / 60;
        private const double Bar = 1e5;

        private static ValueTable Solve(SketchDocument doc, Registry r) => ValueSolver.Solve(doc, r, null, new ValueSolveOptions { BudgetMs = 10000 });

        private static double Val(ValueTable t, string id, string key) => t.Get(id, key)!.Value!.Value;

        private static SketchObject Make(Registry r, string item, string id) => r.Create(r.Item(item)!, Vec2.Zero) with { Id = id };

        private static SketchDocument Doc(params SketchObject[] objs)
        {
            var doc = SketchDocument.Create(PageSizes.Slide16x9, "Values");
            return Edits.Add(doc, doc.Pages[0].Id, objs);
        }

        private static SketchObject Line(Registry r, string id, (string Obj, string Port) a, (string Obj, string Port) b)
            => r.Create(r.Item("fluid.line")!, Vec2.Zero) with
            {
                Id = id,
                Points = new[] { new Vec2(0, 0), new Vec2(10, 0) },
                Attach = new Attachment?[] { new Attachment(a.Obj, 0, 0) { AnchorId = a.Port }, new Attachment(b.Obj, 0, 0) { AnchorId = b.Port } },
            };

        private static IReadOnlyList<CheckFinding> Checks(Registry r, SketchDocument doc) => ValueSolver.Checks(Solve(doc, r), r, new RenderContext(doc));

        [Fact]
        public void LaminarPressureDropInAnOilLine()
        {
            var r = Fx.Reg();
            var line = Make(r, "fluid.line", "l1").WithParam("flowRate", "20 l/min").WithParam("innerDiameter", "12 mm").WithParam("length", "5 m");
            var t = Solve(Doc(line), r);
            var v = 4 * 20 * Lpm / (Math.PI * 0.012 * 0.012);
            var re = v * 0.012 / 46e-6;
            Assert.True(re < 2320);
            var lambda = 64 / re;
            Assert.Equal(re, Val(t, "l1", "Re"), 6);
            Assert.Equal(lambda, Val(t, "l1", "lambda"), 9);
            Assert.Equal(lambda * 5 / 0.012 * 870 * v * v / 2, Val(t, "l1", "dp"), 3);
            Assert.Equal(Provenance.Default, t.Get("l1", "nu")!.Provenance);
        }

        [Fact]
        public void TurbulentFlowUsesTheHaalandFrictionFactor()
        {
            var r = Fx.Reg();
            var line = Make(r, "fluid.line", "l1").WithParam("flowRate", "20 l/min").WithParam("innerDiameter", "12 mm").WithParam("length", "10 m")
                .WithParam("viscosity", "1 mm²/s").WithParam("density", "1000 kg/m³");
            var t = Solve(Doc(line), r);
            var v = 4 * 20 * Lpm / (Math.PI * 0.012 * 0.012);
            var re = v * 0.012 / 1e-6;
            var x = -1.8 * Math.Log10(Math.Pow(0.0015e-3 / 0.012 / 3.7, 1.11) + 6.9 / re);
            var lambda = 1 / (x * x);
            Assert.True(re > 2320);
            Assert.InRange(lambda, 0.02, 0.03);
            Assert.Equal(lambda, Val(t, "l1", "lambda"), 9);
            Assert.Equal(lambda * 10 / 0.012 * 1000 * v * v / 2, Val(t, "l1", "dp"), 3);
            // The length follows from an allowed pressure drop.
            var back = Make(r, "fluid.line", "l2").WithParam("flowRate", "20 l/min").WithParam("innerDiameter", "12 mm")
                .WithParam("viscosity", "1 mm²/s").WithParam("density", "1000 kg/m³").WithParam("pressureDrop", "0,5 bar");
            Assert.Equal(0.5 * Bar / (lambda / 0.012 * 1000 * v * v / 2), Val(Solve(Doc(back), r), "l2", "L"), 6);
        }

        [Fact]
        public void OrificeFlowFromDiameterAndPressureDrop()
        {
            var r = Fx.Reg();
            var th = Make(r, "fluid.throttle", "th").WithParam("orificeDiameter", "2 mm").WithParam("pressureDrop", "50 bar");
            var t = Solve(Doc(th), r);
            var a0 = Math.PI * 0.002 * 0.002 / 4;
            Assert.Equal(0.6 * a0 * Math.Sqrt(2 * 50 * Bar / 870), Val(t, "th", "Q"), 12);
            // Required orifice for a flow.
            var back = Make(r, "fluid.throttle", "th2").WithParam("flowRate", "10 l/min").WithParam("pressureDrop", "20 bar");
            var a = 10 * Lpm / (0.6 * Math.Sqrt(2 * 20 * Bar / 870));
            Assert.Equal(Math.Sqrt(4 * a / Math.PI), Val(Solve(Doc(back), r), "th2", "d0"), 9);
        }

        [Fact]
        public void ThrottleReadsThePressuresOfItsDockedLines()
        {
            // Pressure source 100 bar → throttle (d₀ = 2 mm) → single-acting cylinder at 60 bar: Δp = 40 bar gives the flow and the
            // cylinder's speed.
            var r = Fx.Reg();
            var src = Make(r, "fluid.source", "src").WithParam("pressure", "100 bar");
            var th = Make(r, "fluid.throttle", "th").WithParam("orificeDiameter", "2 mm");
            var cyl = Make(r, "fluid.cylinder.single", "cyl").WithParam("pressure", "60 bar").WithParam("bore", "40 mm");
            var doc = Doc(src, th, cyl, Line(r, "l1", ("src", "P"), ("th", "P")), Line(r, "l2", ("th", "A"), ("cyl", "A")));
            var t = Solve(doc, r);
            Assert.Equal(100 * Bar, Val(t, "th", "p1"), 6);
            Assert.Equal(60 * Bar, Val(t, "th", "p2"), 6);
            Assert.Equal(40 * Bar, Val(t, "th", "dp"), 6);
            var q = 0.6 * Math.PI * 0.002 * 0.002 / 4 * Math.Sqrt(2 * 40 * Bar / 870);
            Assert.Equal(q, Val(t, "th", "Q"), 12);
            Assert.Equal(q, Val(t, "cyl", "Q"), 12);
            Assert.Equal(q / (Math.PI * 0.04 * 0.04 / 4), Val(t, "cyl", "v"), 9);
            Assert.Empty(t.Conflicts);
        }

        [Fact]
        public void PowerUnitGivesDrivePowerAndRelievesTheCylinder()
        {
            var r = Fx.Reg();
            var unit = Make(r, "fluid.powerunit", "hpu").WithParam("flowRate", "30 l/min").WithParam("pressure", "100 bar")
                .WithParam("efficiency", "85 %").WithParam("setPressure", "120 bar");
            var cyl = Make(r, "fluid.cylinder.double", "cyl").WithParam("bore", "50 mm");
            var doc = Doc(unit, cyl, Line(r, "l1", ("hpu", "P"), ("cyl", "A")));
            var t = Solve(doc, r);
            Assert.Equal(5000, Val(t, "hpu", "Phyd"), 6);
            Assert.Equal(5000 / 0.85, Val(t, "hpu", "P"), 6);
            Assert.Equal(120 * Bar, Val(t, "cyl", "pmax"), 6);
            Assert.Equal(30 * Lpm / (Math.PI * 0.05 * 0.05 / 4), Val(t, "cyl", "vext"), 9);
        }

        [Fact]
        public void SmallTankGivesTheTankHint()
        {
            var r = Fx.Reg();
            SketchDocument Unit(string vt) => Doc(Make(r, "fluid.powerunit", "hpu").WithParam("flowRate", "30 l/min").WithParam("tankVolume", vt));
            Assert.Contains(Checks(r, Unit("50 l")), f => f.CheckId == "fluid.tank.volume");
            Assert.DoesNotContain(Checks(r, Unit("120 l")), f => f.CheckId == "fluid.tank.volume");
        }

        [Fact]
        public void PneumaticCylinderAirConsumption()
        {
            var r = Fx.Reg();
            var cyl = Make(r, "fluid.cylinder.double", "cyl").WithParam("bore", "50 mm").WithParam("rodDiameter", "20 mm")
                .WithParam("stroke", "100 mm").WithParam("pressure", "6 bar").WithParam("speedExt", "0,2 m/s");
            var t = Solve(Doc(cyl), r);
            double a1 = Math.PI * 0.05 * 0.05 / 4, a2 = Math.PI * (0.05 * 0.05 - 0.02 * 0.02) / 4;
            var k = (6 * Bar + 101325) / 101325;
            Assert.Equal((a1 + a2) * 0.1, Val(t, "cyl", "Vs"), 12);
            Assert.Equal((a1 + a2) * 0.1 * k, Val(t, "cyl", "Vair"), 12);
            Assert.Equal(0.2 * a1 * k, Val(t, "cyl", "Qn"), 12);
        }

        [Fact]
        public void PowerUnitInsertsWithPortsOnTheGridAndRenders()
        {
            var r = Fx.Reg();
            var o = Make(r, "fluid.powerunit", "hpu");
            Assert.Equal(new Size2(30, 25), o.Size);
            var doc = Doc(o);
            var ports = r.Type("fluid.powerunit")!.Anchors(o, new RenderContext(doc)).Where(a => a.Role == AnchorRole.Port).ToList();
            Assert.Equal(new[] { "P", "T" }, ports.Select(p => p.Id).ToArray());
            foreach (var p in ports)
            {
                Assert.Equal(0, Math.IEEERemainder(p.P.X, 2.5), 9);
                Assert.Equal(0, Math.IEEERemainder(p.P.Y, 2.5), 9);
            }
            var prims = r.Type("fluid.powerunit")!.Render(o, new RenderContext(doc));
            Assert.Contains(prims, p => p.Tag == "enclosure");
            Assert.Contains(prims, p => p.Tag == "motor");
            Assert.Equal("Hydraulikaggregat", r.Item("fluid.powerunit")!.Name.De);
        }
    }
}
