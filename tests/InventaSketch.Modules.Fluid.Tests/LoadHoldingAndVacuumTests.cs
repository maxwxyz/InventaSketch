using System;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Fluid.Tests
{
    /// <summary>Counterbalance valve, pilot-operated check valve (single and double), vacuum generator and suction cup.</summary>
    public class LoadHoldingAndVacuumTests
    {
        [Theory]
        [InlineData("fluid.counterbalance")]
        [InlineData("fluid.pilotcheck")]
        public void PilotPortsTakeAPilotLineAndWorkingPortsAWorkingLine(string item)
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, item);
            var t = r.TypeOf(o);
            foreach (var p in Fx.Ports(r, o))
                Assert.Equal(p.Id == "X" ? "fluid.line.pilot" : "fluid.line", t.Connection(o, p)!.Connector);
        }

        [Fact]
        public void DoublePilotCheckGrowsTheBoxAndKeepsTheScale()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "fluid.pilotcheck");
            Assert.Equal(new Size2(15, 15), o.Size);
            var twin = Fx.Action(r, o, "fluid.pilotcheck.twin").Apply(o);
            Assert.Equal(new Size2(15, 20), twin.Size);
            Assert.Equal(new[] { "A1", "A2", "B1", "B2" }, Fx.Ports(r, twin).Select(p => p.Id));
            Fx.AssertInsideBox(Fx.Render(r, twin), twin.Size, "double pilot check");
        }

        [Fact]
        public void BellowsAndSpringChangeTheDrawing()
        {
            var r = Fx.Reg();
            string Sig(SketchObject o) => string.Join("|", Prim.Flatten(Fx.Render(r, o)).Select(p => p.Tag + string.Join(";", Prim.Points(p))));
            var cup = Fx.Item(r, "fluid.suctioncup");
            Assert.NotEqual(Sig(cup), Sig(cup.WithParam("bellows", true)));
            Assert.Contains(Prim.Flatten(Fx.Render(r, cup.WithParam("bellows", true))), p => p.Tag == "bellows");
            var check = Fx.Item(r, "fluid.pilotcheck");
            Assert.NotEqual(Sig(check), Sig(check.WithParam("spring", false)));
            var cb = Fx.Item(r, "fluid.counterbalance");
            Assert.NotEqual(Sig(cb), Sig(cb.WithParam("adjustable", false)));
        }

        [Fact]
        public void CounterbalanceValvePassesTheFlowButHoldsThePressure()
        {
            var r = Fx.Reg();
            var cyl = Fx.Item(r, "fluid.cylinder.double") with { Id = "cyl" };
            var cb = Fx.Item(r, "fluid.counterbalance") with { Id = "cb" };
            var pump = Fx.Item(r, "fluid.pump") with { Id = "pump" };
            SketchObject Line(string id, (string Obj, string Port) a, (string Obj, string Port) b) => r.Create(r.Item("fluid.line")!, Vec2.Zero) with
            {
                Id = id,
                Points = new[] { new Vec2(0, 0), new Vec2(10, 0) },
                Attach = new Attachment?[] { new Attachment(a.Obj, 0, 0) { AnchorId = a.Port }, new Attachment(b.Obj, 0, 0) { AnchorId = b.Port } },
            };
            var (doc, _) = Fx.Doc(pump.WithParam("flowRate", "12 l/min").WithParam("pressure", "80 bar"), cb, cyl,
                Line("l1", ("pump", "P"), ("cb", "V")), Line("l2", ("cb", "C"), ("cyl", "A")));
            var t = ValueSolver.Solve(doc, r, null, new ValueSolveOptions { BudgetMs = 10000 });
            Assert.Empty(t.Conflicts);
            Assert.Equal(12e-3 / 60, t.Get("cyl", "Q")?.Value ?? double.NaN, 9);
            // The pump pressure stops at the valve (the load side holds its own pressure).
            Assert.False(t.Get("cyl", "p")?.Value is double pc && Math.Abs(pc - 80e5) < 1, "pump pressure passed through the counterbalance valve");
        }

        [Fact]
        public void VacuumGeneratorAndSuctionCupConnectThroughTheVacuumPort()
        {
            var r = Fx.Reg();
            var gen = Fx.Item(r, "fluid.vacuum", new Vec2(50, 50));
            var cup = Fx.Item(r, "fluid.suctioncup", new Vec2(50, 70));
            var v = Fx.Ports(r, gen).Single(p => p.Id == "V");
            Assert.Equal(new Vec2(0, gen.Size.H / 2), v.P);
            Assert.Equal(new Vec2(0, -cup.Size.H / 2), Fx.Ports(r, cup).Single().P);
            Assert.Contains("fluid.suctioncup", r.TypeOf(gen).Companions);
            Assert.Contains("fluid.vacuum.generator", r.TypeOf(cup).Companions);
        }
    }
}
