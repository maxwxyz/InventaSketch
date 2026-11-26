using System;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Engine.Tests
{
    /// <summary>
    /// Shaft lines across modules (<see cref="ShaftLines"/>): an e-machine, a mechanical shaft and a process pump share speed and
    /// torque whatever their ids; a machines motor with a linked torque chart solves without recursing through the coupling
    /// graph; belt pulleys whose pitch circles touch do not mesh. Expected values are hand calculations.
    /// </summary>
    public class ShaftLineTests
    {
        private const double RpmToSi = 2 * Math.PI / 60;

        private static readonly Registry Reg = ModuleCatalog.CreateDefault();

        private static readonly RenderContext Empty = new(SketchDocument.Create());

        private static SketchObject Make(string item, string id, double x = 0, double y = 0) => Reg.Create(Reg.Item(item)!, new Vec2(x, y)) with { Id = id };

        private static SketchDocument Doc(params SketchObject[] objs)
        {
            var doc = SketchDocument.Create(PageSizes.Slide16x9, "Shaft lines");
            return Edits.Add(doc, doc.Pages[0].Id, objs);
        }

        private static Anchor HubOf(SketchObject o) => Reg.PageAnchors(o, Empty).Single(a => a.Role == AnchorRole.Hub);

        private static SketchObject Dock(SketchObject part, Vec2 at)
        {
            var d = at - HubOf(part).P;
            return part with { Transform = part.Transform with { X = part.Transform.X + d.X, Y = part.Transform.Y + d.Y } };
        }

        private static ValueTable Solve(SketchDocument doc, params string[] ids)
            => ValueSolver.Solve(doc, Reg, ids.Length == 0 ? null : ids, new ValueSolveOptions { BudgetMs = 10000 });

        private static double Val(ValueTable t, string id, string key) => t.Get(id, key)!.Value!.Value;

        /// <summary>
        /// The e-machine sorts after the pump: the shaft line's representative is the shaft (not the pump), so the shaft links
        /// the e-machine's torque, which its own module does not pass on.
        /// </summary>
        [Theory]
        [InlineData("em")]
        [InlineData("zm")]
        public void TorqueFromAnEMachineReachesThePumpThroughTheShaft(string motorId)
        {
            var shaft = Make("mech.shaft.line", "s1", 20, 0) with { Size = new Size2(80, 8) };
            var em = Make("machines.emachine.stick", motorId, -10, 0).WithParam("speed", "3000 rpm").WithParam("torque", "20 N·m");
            var pump = Dock(Make("process.pump.displacement", "pump"), new Vec2(40, 0));
            var doc = Doc(shaft, em, pump);
            var graph = Coupling.Graph(Reg, doc);
            Assert.Contains(graph, l => l.Kind == LinkKind.Carried && l.A == "s1" && l.B == "pump");
            Assert.Contains(graph, l => l.Kind == LinkKind.Carried && l.A == "s1" && l.B == motorId);
            var line = ShaftLines.Of(Reg, doc).LineOf("pump")!;
            Assert.Equal("s1", line.Rep);
            Assert.Equal("s1", line.TorqueRep);
            Assert.True(line.CarriesTorque);

            foreach (var scope in new[] { new[] { "pump" }, new[] { motorId }, Array.Empty<string>() })
            {
                var t = Solve(doc, scope);
                Assert.Empty(t.Conflicts);
                Assert.Equal(3000 * RpmToSi, Val(t, "pump", "n"), 9);
                Assert.Equal(20, Val(t, "pump", "T"), 9);
                Assert.Equal(20, Val(t, "s1", "T"), 9);
                // P = 2π·50 s⁻¹·20 N·m = 6283.2 W on the drive and the pump.
                Assert.Equal(2 * Math.PI * 50 * 20, Val(t, "pump", "P"), 6);
                Assert.Equal(Val(t, motorId, "P"), Val(t, "pump", "P"), 6);
            }
        }

        /// <summary>
        /// A mechanical gear and an e-machine on one shaft with the pump: three torque members, a branch: the speed is shared,
        /// the torque is not passed.
        /// </summary>
        [Fact]
        public void ABranchOnTheShaftSharesOnlyTheSpeed()
        {
            var shaft = Make("mech.shaft.line", "s1", 20, 0) with { Size = new Size2(80, 8) };
            var em = Make("machines.emachine.stick", "zm", -10, 0).WithParam("speed", "1500 rpm").WithParam("torque", "20 N·m");
            var pump = Dock(Make("process.pump.displacement", "pump"), new Vec2(40, 0));
            var gear = Dock(Reg.Create(Reg.Library.First(i => i.TypeId == "mech.gear.spur" && i.View == "side"), Vec2.Zero) with { Id = "gear" }, new Vec2(10, 0));
            var doc = Doc(shaft, em, pump, gear);
            var line = ShaftLines.Of(Reg, doc).LineOf("pump")!;
            Assert.Contains("gear", line.Members);
            Assert.False(line.CarriesTorque);
            var t = Solve(doc, "pump");
            Assert.Empty(t.Conflicts);
            Assert.Equal(1500 * RpmToSi, Val(t, "pump", "n"), 9);
            Assert.Equal(1500 * RpmToSi, Val(t, "gear", "n"), 9);
            Assert.Null(t.Get("pump", "T")!.Value);
        }

        /// <summary>
        /// The linked chart's anchors solve the motor, whose shaft links ask for the coupling graph that is being built: the
        /// nested request gets an empty graph instead of recursing (the per-module guard used to be missing in the machines
        /// module).
        /// </summary>
        [Fact]
        public void AnEMachineWithALinkedChartDoesNotRecurse()
        {
            var motor = Make("machines.emachine.stick", "motor", -10, 0).WithParam("speed", "1450 rpm").WithParam("torque", "50 N·m");
            Assert.NotEmpty(Characteristics.SetsOf(Reg, motor, Empty));
            var shaft = Make("mech.shaft.line", "s1", 20, 0) with { Size = new Size2(80, 8) };
            var pump = Dock(Make("process.pump.centrifugal", "pump"), new Vec2(40, 0));
            var chart = Reg.Type(Characteristics.ChartTypeId)!.Create(new Vec2(200, 60)).WithParam(Characteristics.LinkParam, Characteristics.Link("motor", "torque")) with { Id = "chart" };
            var doc = Doc(motor, shaft, pump, chart);

            var graph = Coupling.Graph(Reg, doc);
            Assert.Contains(graph, l => l.Kind == LinkKind.Carried && l.A == "s1" && l.B == "motor");
            Assert.Contains(graph, l => l.Kind == LinkKind.Carried && l.A == "s1" && l.B == "pump");
            Assert.False(Coupling.GraphPending(doc));

            var t = Solve(doc, "motor");
            Assert.Empty(t.Conflicts);
            Assert.Equal(1450 * RpmToSi, Val(t, "pump", "n"), 9);
            Assert.Equal(50, Val(t, "pump", "T"), 9);
            Assert.NotNull(Characteristics.Resolve(Reg, doc, Characteristics.Link("motor", "torque")));
            // The chart renders (its anchors resolved) in a fresh document instance as well.
            var copy = DocumentJson.Deserialize(DocumentJson.Serialize(doc));
            Assert.NotEmpty(Reg.PageAnchors(copy.Get("chart")!, new RenderContext(copy).For(Reg)));
        }

        [Fact]
        public void BeltPulleysWhosePitchCirclesTouchDoNotMesh()
        {
            var item = Reg.Library.First(i => i.TypeId == "mech.pulley" && i.View == "axial");
            var a = Reg.Create(item, new Vec2(100, 100)) with { Id = "a" };
            var r = Reg.PageAnchors(a, Empty).Single(x => x.Role == AnchorRole.Pitch).Radius;
            var b = Reg.Create(item, new Vec2(100 + 2 * r, 100)) with { Id = "b" };
            var doc = Doc(a, b);
            Assert.DoesNotContain(Coupling.Graph(Reg, doc), l => l.Kind == LinkKind.Mesh);
            // Moving one away keeps both sizes (no centre-distance scaling as for gears).
            var after = Coupling.Propagate(Reg, doc, Edits.Move(doc, new[] { "b" }, new Vec2(15, 0)));
            Assert.Equal(doc.Get("a")!.Size, after.Get("a")!.Size);
            Assert.Equal(doc.Get("b")!.Size, after.Get("b")!.Size);

            // Sprockets neither; spur gears in the same place still mesh.
            var sprocket = Reg.Library.First(i => i.TypeId == "mech.sprocket" && i.View == "teeth");
            var s1 = Reg.Create(sprocket, new Vec2(100, 100)) with { Id = "s1" };
            var rs = Reg.PageAnchors(s1, Empty).Single(x => x.Role == AnchorRole.Pitch).Radius;
            Assert.DoesNotContain(Coupling.Graph(Reg, Doc(s1, Reg.Create(sprocket, new Vec2(100 + 2 * rs, 100)) with { Id = "s2" })), l => l.Kind == LinkKind.Mesh);
            var spur = Reg.Library.First(i => i.TypeId == "mech.gear.spur" && i.View == "axial");
            var g1 = Reg.Create(spur, new Vec2(100, 100)) with { Id = "g1" };
            var rg = Reg.PageAnchors(g1, Empty).Single(x => x.Role == AnchorRole.Pitch).Radius;
            Assert.Contains(Coupling.Graph(Reg, Doc(g1, Reg.Create(spur, new Vec2(100 + 2 * rg, 100)) with { Id = "g2" })), l => l.Kind == LinkKind.Mesh);
        }
    }
}
