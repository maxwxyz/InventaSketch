using System;
using System.Linq;
using InventaSketch.Core;
using InventaSketch.Modules.Machines;
using InventaSketch.Modules.Mechanical;
using Xunit;

namespace InventaSketch.Modules.Fluid.Tests
{
    /// <summary>
    /// "A motor drives a pump": the hydraulic pump has a hub at its shaft end, the hydraulic motor also its output stub as a
    /// shaft axis, so an e-machine, a machines motor or a mechanical shaft docks on them through the core coupling and the
    /// shaft values n, T, P are shared. Expected values are hand calculations.
    /// </summary>
    public class ShaftTests
    {
        private const double RpmToSi = 2 * Math.PI / 60;

        private static Registry Reg() => new Registry().Register(new FluidModule()).Register(new MechanicalModule()).Register(new MachinesModule());

        private static readonly RenderContext Empty = new(SketchDocument.Create());

        private static SketchDocument Doc(params SketchObject[] objs)
        {
            var doc = SketchDocument.Create(PageSizes.Slide16x9, "Shaft");
            return Edits.Add(doc, doc.Pages[0].Id, objs);
        }

        private static SketchObject Make(Registry r, string item, string id, double x = 0, double y = 0) => r.Create(r.Item(item)!, new Vec2(x, y)) with { Id = id };

        private static Vec2 Pos(SketchDocument doc, string id) => new(doc.Get(id)!.Transform.X, doc.Get(id)!.Transform.Y);

        private static Vec2 HubOf(Registry r, SketchObject o) => r.PageAnchors(o, Empty).Single(a => a.Role == AnchorRole.Hub).P;

        private static SketchObject Dock(Registry r, SketchObject part, Vec2 at)
        {
            var d = at - HubOf(r, part);
            return part with { Transform = part.Transform with { X = part.Transform.X + d.X, Y = part.Transform.Y + d.Y } };
        }

        private static SketchObject Flipped(SketchObject o) => o with { Transform = o.Transform with { FlipX = true } };

        private static void Near(Vec2 expected, Vec2 actual, string what)
            => Assert.True(expected.DistanceTo(actual) < 1e-6, $"{what}: expected {expected}, got {actual}");

        private static double Val(ValueTable t, string id, string key) => t.Get(id, key)!.Value!.Value;

        [Fact]
        public void PumpHasAHubAtItsShaftEndAndTheMotorAlsoItsStubAsAxis()
        {
            var r = Reg();
            var pump = Make(r, "fluid.pump", "p");
            var pa = r.TypeOf(pump).Anchors(pump, Empty).ToList();
            var hub = Assert.Single(pa, a => a.Role == AnchorRole.Hub);
            Near(new Vec2(pump.Size.W / 2, 0), hub.P, "pump hub at the end of the drawn shaft");
            Assert.DoesNotContain(pa, a => a.Role == AnchorRole.Axis);
            Assert.False(r.TypeOf(pump).DrivesShaft);

            var motor = Make(r, "fluid.motor", "m");
            var ma = r.TypeOf(motor).Anchors(motor, Empty).ToList();
            var axis = Assert.Single(ma, a => a.Role == AnchorRole.Axis);
            Near(new Vec2(motor.Size.W / 2, 0), axis.Q, "motor axis to the shaft end");
            Near(axis.Q, Assert.Single(ma, a => a.Role == AnchorRole.Hub).P, "motor hub at the shaft end");
            Assert.True(r.TypeOf(motor).DrivesShaft);
            // The compressor (pneumatic pump) and the air motor keep the shaft.
            Assert.Single(r.TypeOf(pump).Anchors(Make(r, "fluid.compressor", "c"), Empty), a => a.Role == AnchorRole.Hub);
        }

        [Fact]
        public void AnEMachineDrivesTheHydraulicPumpAndTakesItsTorque()
        {
            var r = Reg();
            var em = Make(r, "machines.emachine.stick", "em", 40, 60).WithParam("speed", "1450 rpm");
            var end = r.PageAnchors(em, Empty).Single(a => a.Role == AnchorRole.Port && a.Id == "e").P;
            var pump = Dock(r, Flipped(Make(r, "fluid.pump", "pump")), end).WithParam("displacement", "10 cm³").WithParam("pressure", "100 bar");
            var doc = Doc(em, pump);
            Assert.Contains(Coupling.Graph(r, doc), l => l.Kind == LinkKind.Carried && l.A == "em" && l.B == "pump");

            var t = ValueSolver.Solve(doc, r, new[] { "pump" }, new ValueSolveOptions { BudgetMs = 10000 });
            Assert.Empty(t.Conflicts);
            // Q = V_g·n = 10 cm³ · 1450 min⁻¹ = 14.5 l/min.
            Assert.Equal(1450, Val(t, "pump", "n") / RpmToSi, 9);
            Assert.Equal(14.5e-3 / 60, Val(t, "pump", "Q"), 9);
            // T = V_g·p/(2π) = 10e-6 m³ · 1e7 Pa / 2π = 15.92 N·m is the e-machine's load; P = 2π·n·T = V_g·p·n = 2416.7 W.
            var torque = 10e-6 * 1e7 / (2 * Math.PI);
            Assert.Equal(torque, Val(t, "em", "T"), 6);
            Assert.Equal(10e-6 * 1e7 * 1450 / 60, Val(t, "em", "P"), 6);
        }

        [Fact]
        public void AMechanicalShaftCarriesThePumpAndTheHydraulicMotorLeadsItsShaft()
        {
            var r = Reg();
            var pump = Make(r, "fluid.pump", "pump", 80, 60);
            var hub = HubOf(r, pump);
            var shaft = Make(r, "mech.shaft.line", "s", hub.X + 60, hub.Y);
            var before = Doc(pump, shaft);
            Assert.Contains(Coupling.Graph(r, before), l => l.Kind == LinkKind.Carried && l.A == "s" && l.B == "pump");
            var after = Coupling.Propagate(r, before, Edits.Move(before, new[] { "s" }, new Vec2(10, -4)));
            Near(Pos(before, "pump") + new Vec2(10, -4), Pos(after, "pump"), "pump follows the shaft");

            var motor = Make(r, "fluid.motor", "m", 80, 120);
            var end = HubOf(r, motor);
            var s2 = Make(r, "mech.shaft.line", "s2", end.X + 60, end.Y);
            var doc = Doc(motor, s2);
            Assert.Contains(Coupling.Graph(r, doc), l => l.Kind == LinkKind.Carried && l.A == "s2" && l.B == "m");
            var moved = Coupling.Propagate(r, doc, Edits.Move(doc, new[] { "m" }, new Vec2(0, 6)));
            Near(Pos(doc, "s2") + new Vec2(0, 6), Pos(moved, "s2"), "the shaft follows the hydraulic motor (a drive)");
        }

        [Fact]
        public void AHydraulicMotorDrivesAShaftLineWithItsSpeedAndTorque()
        {
            var r = Reg();
            var motor = Make(r, "fluid.motor", "m", 0, 0)
                .WithParam("displacement", "10 cm³").WithParam("flowRate", "20 l/min").WithParam("pressure", "100 bar");
            var end = HubOf(r, motor);
            var shaft = Make(r, "mech.shaft.line", "s", end.X + 40, end.Y) with { Size = new Size2(80, 8) };
            var gear = Make(r, "mech.spur.stick", "g", end.X + 50, end.Y) with { Size = new Size2(8, 20) };
            var t = ValueSolver.Solve(Doc(motor, shaft, gear), r, new[] { "m" }, new ValueSolveOptions { BudgetMs = 10000 });
            Assert.Empty(t.Conflicts);
            // n = Q/V_g = 2000 min⁻¹; T = V_g·p/(2π) = 15.92 N·m on the shaft and the gear (two torque members).
            Assert.Equal(2000, Val(t, "s", "n") / RpmToSi, 9);
            Assert.Equal(2000, Val(t, "g", "n") / RpmToSi, 9);
            Assert.Equal(10e-6 * 1e7 / (2 * Math.PI), Val(t, "g", "T"), 6);
        }
    }
}
