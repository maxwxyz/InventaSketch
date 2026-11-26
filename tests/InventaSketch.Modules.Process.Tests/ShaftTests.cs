using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using InventaSketch.Modules.Machines;
using InventaSketch.Modules.Mechanical;
using Xunit;

namespace InventaSketch.Modules.Process.Tests
{
    /// <summary>
    /// "A motor drives a pump": the rotating process machines expose the shaft contract of the mechanical and machines modules
    /// (a hub at the shaft end; drives also the shaft stub as an axis), so a mechanical shaft, a machines motor, an e-machine,
    /// the process motor or a turbine docks on them through the core coupling, and the shaft values n, T, P are shared.
    /// Expected values are hand calculations.
    /// </summary>
    public class ShaftTests
    {
        private const double RpmToSi = 2 * Math.PI / 60;

        private static Registry Reg() => new Registry().Register(new ProcessModule()).Register(new MechanicalModule()).Register(new MachinesModule());

        private static readonly RenderContext Empty = new(SketchDocument.Create());

        private static SketchDocument Doc(params SketchObject[] objs)
        {
            var doc = SketchDocument.Create(PageSizes.Slide16x9, "Shaft");
            return Edits.Add(doc, doc.Pages[0].Id, objs);
        }

        private static SketchObject Make(Registry r, string item, string id, double x = 0, double y = 0) => r.Create(r.Item(item)!, new Vec2(x, y)) with { Id = id };

        /// <summary>The machines e-motor in its circuit symbol view (shaft stub to the right).</summary>
        private static SketchObject MotorSymbol(Registry r, string id, double x, double y)
            => r.WithView(r.Type("machines.drive.motor")!.Create(new Vec2(x, y)) with { Id = id }, "symbol", Empty);

        private static Vec2 Pos(SketchDocument doc, string id) => new(doc.Get(id)!.Transform.X, doc.Get(id)!.Transform.Y);

        private static Anchor HubOf(Registry r, SketchObject o) => r.PageAnchors(o, Empty).Single(a => a.Role == AnchorRole.Hub);

        /// <summary>The outer end of a drive's shaft stub (the end of its axis segment).</summary>
        private static Vec2 ShaftEnd(Registry r, SketchObject drive) => r.PageAnchors(drive, Empty).Single(a => a.Role == AnchorRole.Axis).Q;

        /// <summary>The part moved so that its hub lies on <paramref name="at"/>.</summary>
        private static SketchObject Dock(Registry r, SketchObject part, Vec2 at)
        {
            var d = at - HubOf(r, part).P;
            return part with { Transform = part.Transform with { X = part.Transform.X + d.X, Y = part.Transform.Y + d.Y } };
        }

        private static SketchObject Flipped(SketchObject o) => o with { Transform = o.Transform with { FlipX = true } };

        private static void Near(Vec2 expected, Vec2 actual, string what)
            => Assert.True(expected.DistanceTo(actual) < 1e-6, $"{what}: expected {expected}, got {actual}");

        private static ValueTable Solve(SketchDocument doc, Registry r) => ValueSolver.Solve(doc, r, null, new ValueSolveOptions { BudgetMs = 10000 });

        private static double Val(ValueTable t, string id, string key) => t.Get(id, key)!.Value!.Value;

        private static double Rpm(ValueTable t, string id) => Val(t, id, "n") / RpmToSi;

        // ------------------------------------------------------------------ the contract

        public static IEnumerable<object[]> Driven() => new[]
        {
            new object[] { "process.pump.centrifugal" }, new object[] { "process.pump.displacement" }, new object[] { "process.compressor" },
            new object[] { "process.fan" }, new object[] { "process.fan.axial" },
        };

        [Theory]
        [MemberData(nameof(Driven))]
        public void DrivenMachinesHaveAShaftHubOnTheirBody(string item)
        {
            var r = Reg();
            var o = r.Create(r.Item(item)!, Vec2.Zero);
            var t = r.TypeOf(o);
            var anchors = t.Anchors(o, Empty).ToList();
            var hub = Assert.Single(anchors, a => a.Role == AnchorRole.Hub);
            Assert.Equal("hub", hub.Id);
            Assert.Equal(0, hub.Radius); // no bore: keeps its size on a shaft
            Assert.True(Math.Abs(hub.P.X) <= o.Size.W / 2 + 1e-9 && Math.Abs(hub.P.Y) <= o.Size.H / 2 + 1e-9, item + " hub inside the box");
            Assert.DoesNotContain(anchors, a => a.Role == AnchorRole.Axis);
            Assert.False(t.DrivesShaft);
            Assert.True(ValueModel.Variable(t, "n") is not null && ValueModel.Variable(t, "T") is not null && ValueModel.Variable(t, "P") is not null, item);
            // Shaft anchors scale with the box like the nozzles.
            var big = o with { Size = new Size2(o.Size.W * 2, o.Size.H * 3) };
            var hub2 = t.Anchors(big, Empty).Single(a => a.Role == AnchorRole.Hub);
            Near(new Vec2(hub.P.X * 2, hub.P.Y * 3), hub2.P, item + " scaled hub");
        }

        [Theory]
        [InlineData("process.motor")]
        [InlineData("process.turbine")]
        public void DrivesExposeTheShaftStubAsAxisWithTheHubAtItsEnd(string item)
        {
            var r = Reg();
            var o = r.Create(r.Item(item)!, Vec2.Zero);
            var t = r.TypeOf(o);
            var anchors = t.Anchors(o, Empty).ToList();
            var axis = Assert.Single(anchors, a => a.Role == AnchorRole.Axis);
            var hub = Assert.Single(anchors, a => a.Role == AnchorRole.Hub);
            Near(axis.Q, hub.P, item + " hub at the shaft end");
            Assert.Contains(anchors, a => a.Role == AnchorRole.End && a.P.DistanceTo(axis.Q) < 1e-9);
            Assert.True(t.DrivesShaft);
            Assert.True(ValueModel.Variable(t, "n") is not null && ValueModel.Variable(t, "T") is not null, item);
            // The motor's shaft port stays (old attachments survive).
            if (item == "process.motor") Assert.Contains(anchors, a => a.Role == AnchorRole.Port && a.Id == "shaft");
        }

        // ------------------------------------------------------------------ docking (coupling)

        [Theory]
        [MemberData(nameof(Driven))]
        public void AMechanicalShaftThroughTheHubCarriesTheMachine(string item)
        {
            var r = Reg();
            var pump = Make(r, item, "pump", 80, 60);
            var hub = HubOf(r, pump).P;
            // Shaft (120 mm) along x with the hub 30 mm from its left end.
            var shaft = Make(r, "mech.shaft.line", "shaft", hub.X - 30 + 60, hub.Y);
            var before = Doc(pump, shaft);
            Assert.Contains(Coupling.Graph(r, before), l => l.Kind == LinkKind.Carried && l.A == "shaft" && l.B == "pump");
            var delta = new Vec2(17, -9);
            var after = Coupling.Propagate(r, before, Edits.Move(before, new[] { "shaft" }, delta));
            Near(Pos(before, "pump") + delta, Pos(after, "pump"), item + " follows the shaft");
        }

        [Fact]
        public void AMachinesMotorDocksWithItsShaftStubOnThePump()
        {
            var r = Reg();
            var motor = MotorSymbol(r, "motor", 40, 60);
            var pump = Dock(r, Flipped(Make(r, "process.pump.centrifugal", "pump")), ShaftEnd(r, motor));
            var before = Doc(motor, pump);
            Assert.Contains(Coupling.Graph(r, before), l => l.Kind == LinkKind.Carried && l.A == "motor" && l.B == "pump");
            var delta = new Vec2(-12, 25);
            var after = Coupling.Propagate(r, before, Edits.Move(before, new[] { "motor" }, delta));
            Near(Pos(before, "pump") + delta, Pos(after, "pump"), "pump follows its motor");
            // Moving the pump alone leaves the motor where it is.
            var slid = Coupling.Propagate(r, before, Edits.Move(before, new[] { "pump" }, new Vec2(0, 30)));
            Near(Pos(before, "motor"), Pos(slid, "motor"), "motor stays");
        }

        [Fact]
        public void TheProcessMotorDrivesThePumpAndLeadsItsShaft()
        {
            var r = Reg();
            var motor = Flipped(Make(r, "process.motor", "motor", 100, 60));
            var pump = Dock(r, Make(r, "process.pump.centrifugal", "pump"), ShaftEnd(r, motor));
            Assert.True(Pos(Doc(pump), "pump").X < 100, "pump left of the flipped motor");
            var before = Doc(motor, pump);
            Assert.Contains(Coupling.Graph(r, before), l => l.Kind == LinkKind.Carried && l.A == "motor" && l.B == "pump");
            var after = Coupling.Propagate(r, before, Edits.Move(before, new[] { "motor" }, new Vec2(5, 5)));
            Near(Pos(before, "pump") + new Vec2(5, 5), Pos(after, "pump"), "pump follows the process motor");

            // A drive on a shaft takes the shaft along (ObjectType.DrivesShaft): the shaft continues from the motor's shaft end.
            var lone = Flipped(Make(r, "process.motor", "m2", 100, 120));
            var end = ShaftEnd(r, lone);
            var shaft = Make(r, "mech.shaft.line", "s2", end.X - 60, end.Y);
            var doc = Doc(lone, shaft);
            Assert.Contains(Coupling.Graph(r, doc), l => l.Kind == LinkKind.Carried && l.A == "s2" && l.B == "m2");
            var moved = Coupling.Propagate(r, doc, Edits.Move(doc, new[] { "m2" }, new Vec2(0, 7)));
            Near(Pos(doc, "s2") + new Vec2(0, 7), Pos(moved, "s2"), "the shaft follows its drive");
        }

        // ------------------------------------------------------------------ shaft values

        [Fact]
        public void MotorSpeedAndTorqueBecomeThePumpShaftPower()
        {
            var r = Reg();
            var motor = MotorSymbol(r, "motor", 40, 60)
                .WithParam("speed", "1450 rpm").WithParam("torque", "50 N·m");
            var pump = Dock(r, Flipped(Make(r, "process.pump.centrifugal", "pump")), ShaftEnd(r, motor))
                .WithParam("flowRate", "100 m³/h").WithParam("head", "20 m");
            var t = Solve(Doc(motor, pump), r);
            Assert.Empty(t.Conflicts);
            // n = 1450 min⁻¹, T = 50 N·m: P = 2π·(1450/60) s⁻¹·50 N·m = 7592.0 W.
            var p = 2 * Math.PI * 1450 / 60 * 50;
            Assert.Equal(1450, Rpm(t, "pump"), 9);
            Assert.Equal(50, Val(t, "pump", "T"), 9);
            Assert.Equal(p, Val(t, "pump", "P"), 6);
            // P_hyd = ρ·g·H·V̇ = 1000·9.80665·20·100/3600 = 5448.1 W; η = P_hyd/P = 71.8 %.
            var phyd = 1000 * 9.80665 * 20 * 100 / 3600.0;
            Assert.Equal(phyd / p, Val(t, "pump", "eta"), 6);
        }

        [Fact]
        public void PumpOnAShaftDrivenByAnEMachineGetsSpeedTorqueAndPower()
        {
            var r = Reg();
            var shaft = Make(r, "mech.shaft.line", "s1", 20, 0) with { Size = new Size2(80, 8) };
            var em = Make(r, "machines.emachine.stick", "em", -10, 0).WithParam("speed", "3000 rpm").WithParam("torque", "20 N·m");
            var pump = Dock(r, Make(r, "process.pump.displacement", "pump"), new Vec2(40, 0));
            var doc = Doc(shaft, em, pump);
            var graph = Coupling.Graph(r, doc);
            Assert.Contains(graph, l => l.Kind == LinkKind.Carried && l.A == "s1" && l.B == "pump");
            Assert.Contains(graph, l => l.Kind == LinkKind.Carried && l.A == "s1" && l.B == "em");
            // Solving only the pump pulls the shaft line into the scope through the shaft links.
            var t = ValueSolver.Solve(doc, r, new[] { "pump" }, new ValueSolveOptions { BudgetMs = 10000 });
            Assert.Empty(t.Conflicts);
            Assert.Equal(3000, Rpm(t, "pump"), 9);
            // Two torque members on the shaft line (e-machine and pump): the torque passes; P = 2π·50 s⁻¹·20 N·m = 6283.2 W.
            Assert.Equal(20, Val(t, "pump", "T"), 9);
            Assert.Equal(2 * Math.PI * 50 * 20, Val(t, "pump", "P"), 6);
        }

        [Fact]
        public void TheProcessMotorPassesSpeedAndTorqueToThePump()
        {
            var r = Reg();
            var motor = Flipped(Make(r, "process.motor", "motor", 100, 60)).WithParam("speed", "2900 rpm").WithParam("power", "11 kW");
            var pump = Dock(r, Make(r, "process.pump.centrifugal", "pump"), ShaftEnd(r, motor));
            var t = Solve(Doc(motor, pump), r);
            Assert.Empty(t.Conflicts);
            // T = P/(2π·n) = 11000/(2π·2900/60) = 36.22 N·m on both; the pump's shaft power is the motor's 11 kW.
            Assert.Equal(2900, Rpm(t, "pump"), 9);
            Assert.Equal(11000 / (2 * Math.PI * 2900 / 60), Val(t, "pump", "T"), 6);
            Assert.Equal(11000, Val(t, "pump", "P"), 6);
        }

        [Fact]
        public void ATurbineDrivesACompressorBelowIt()
        {
            var r = Reg();
            var turbine = Make(r, "process.turbine", "tb", 100, 40).WithParam("power", "100 kW").WithParam("speed", "6000 rpm");
            var comp = Dock(r, Make(r, "process.compressor", "k"), ShaftEnd(r, turbine));
            Assert.True(Pos(Doc(comp), "k").Y > 40, "compressor below the turbine (its hub is on top)");
            var doc = Doc(turbine, comp);
            Assert.Contains(Coupling.Graph(r, doc), l => l.Kind == LinkKind.Carried && l.A == "tb" && l.B == "k");
            var t = Solve(doc, r);
            Assert.Empty(t.Conflicts);
            Assert.Equal(6000, Rpm(t, "k"), 9);
            Assert.Equal(100000, Val(t, "k", "P"), 6);
            Assert.Equal(100000 / (2 * Math.PI * 100), Val(t, "k", "T"), 6);
        }

        [Fact]
        public void AMachineBesideTheShaftIsNotCoupled()
        {
            var r = Reg();
            var motor = MotorSymbol(r, "motor", 40, 60).WithParam("speed", "1450 rpm");
            var pump = Dock(r, Flipped(Make(r, "process.pump.centrifugal", "pump")), ShaftEnd(r, motor) + new Vec2(0, 3));
            var doc = Doc(motor, pump);
            Assert.DoesNotContain(Coupling.Graph(r, doc), l => l.A == "motor" && l.B == "pump" || l.A == "pump" && l.B == "motor");
            var t = Solve(doc, r);
            Assert.Null(t.Get("pump", "n")!.Value);
        }

        [Fact]
        public void ShaftDocumentSurvivesTheJsonRoundTripWithItsCoupling()
        {
            var r = Reg();
            var motor = Flipped(Make(r, "process.motor", "motor", 100, 60));
            var pump = Dock(r, Make(r, "process.pump.centrifugal", "pump"), ShaftEnd(r, motor)).WithParam("speed", "1450 rpm");
            var doc = Doc(motor, pump);
            var back = DocumentJson.Deserialize(DocumentJson.Serialize(doc));
            Assert.Equal(DocumentJson.Serialize(doc), DocumentJson.Serialize(back));
            Assert.Contains(Coupling.Graph(r, back), l => l.Kind == LinkKind.Carried && l.A == "motor" && l.B == "pump");
        }
    }
}
