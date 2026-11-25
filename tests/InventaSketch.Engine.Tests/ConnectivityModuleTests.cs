using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Engine.Tests
{
    /// <summary>"Select connected" (<see cref="Connectivity"/>) with the real modules: PV strings and cables, gears on shafts, flowcharts, walls.</summary>
    public class ConnectivityModuleTests
    {
        private static readonly Registry Reg = ModuleCatalog.CreateDefault();

        private static SketchDocument Doc(params SketchObject[] objects)
        {
            var doc = SketchDocument.Create(new Size2(400, 300));
            return Edits.Add(doc, doc.Pages[0].Id, objects);
        }

        private static (SketchDocument Doc, string Placed) Build(SketchDocument doc, string id, BuildPort port, string? typePrefix = null)
        {
            var items = Building.Items(Reg, doc, id, port);
            var item = typePrefix is null ? items[0] : items.First(i => i.TypeId.StartsWith(typePrefix, System.StringComparison.Ordinal));
            var r = Building.Apply(Reg, doc, id, port, item)!;
            return (r.Doc, r.SelectId);
        }

        [Fact]
        public void APvStringWithTwoCablesIsOneNet()
        {
            var pv = Reg.Create(Reg.Item("energy.pv.string.10")!, new Vec2(120, 120));
            var doc = Doc(pv);
            var connect = Building.Ports(Reg, doc, pv.Id).First(p => p.Kind == PortKind.Connect);
            (doc, var cable1) = Build(doc, pv.Id, connect);
            // A second cable from the same port (docked like the first, to another place).
            var first = doc.Get(cable1)!;
            var start = doc.ResolvePoints(first)[0];
            var second = Reg.CreateBetween(Reg.Library.First(i => i.TypeId == first.Type), start, start + new Vec2(0, 80))
                with { Attach = new Attachment?[] { first.Attach![0], null } };
            doc = Edits.Add(doc, doc.Pages[0].Id, second);
            var cable2 = second.Id;
            var lone = Reg.Create(Reg.Item("energy.pv.string.10")!, new Vec2(300, 250));
            doc = Edits.Add(doc, doc.Pages[0].Id, lone);

            Assert.Equal(ConnectKind.Net, Connectivity.AutoKind(Reg, doc, cable1));
            var net = Connectivity.Connected(Reg, doc, cable1, ConnectKind.Auto);
            Assert.Equal(cable1, net[0]);
            Assert.Equal(new[] { cable1, cable2, pv.Id }.OrderBy(x => x), net.OrderBy(x => x));
            Assert.Equal(3, Connectivity.Connected(Reg, doc, pv.Id, ConnectKind.Net).Count);
            Assert.Contains(Connectivity.Options(Reg, doc, cable2), o => o.Kind == ConnectKind.Net);
            Assert.Empty(Connectivity.Options(Reg, doc, lone.Id));
        }

        [Fact]
        public void TwoGearsOnAShaftAndTheirPartnerMoveAlong()
        {
            // A shaft through the hub of a schematic gear, a second gear on the same shaft, and a gear meshing with the first.
            var gear = Reg.Create(Reg.Item("mech.spur.stick")!, new Vec2(100, 100));
            var doc = Doc(gear);
            (doc, var shaft) = Build(doc, gear.Id, Building.Ports(Reg, doc, gear.Id).Single(p => p.Kind == PortKind.Hub), "mech.shaft");
            (doc, var partner) = Build(doc, gear.Id, Building.Ports(Reg, doc, gear.Id).First(p => p.Kind == PortKind.Mesh), "mech.gear");
            var end = Building.Ports(Reg, doc, shaft).FirstOrDefault(p => p.Kind == PortKind.AxisEnd);
            Assert.NotNull(end);
            (doc, var second) = Build(doc, shaft, end!, "mech.gear");
            var apart = Reg.Create(Reg.Item("mech.spur.axial")!, new Vec2(330, 250));
            doc = Edits.Add(doc, doc.Pages[0].Id, apart);

            Assert.Equal(ConnectKind.Coupled, Connectivity.AutoKind(Reg, doc, shaft));
            var moves = Connectivity.Connected(Reg, doc, partner, ConnectKind.Coupled);
            Assert.Equal(partner, moves[0]);
            Assert.Equal(new[] { gear.Id, shaft, partner, second }.OrderBy(x => x), moves.OrderBy(x => x));
            Assert.DoesNotContain(apart.Id, moves);
        }

        [Fact]
        public void AFlowchartOfThreeStepsFollowsDownstream()
        {
            var step = Reg.Create(Reg.Item("ann.flow.process")!, new Vec2(100, 40));
            var doc = Doc(step);
            var first = Flow.QuickAdd(Reg, doc, step.Id, FlowSide.Bottom);
            var second = Flow.QuickAdd(Reg, first.Doc, first.ObjectId, FlowSide.Bottom);
            doc = second.Doc;
            var boxes = doc.Objects.Values.Where(o => !o.IsPointGeometry).Select(o => o.Id).ToList();
            Assert.Equal(3, boxes.Count);

            Assert.Equal(ConnectKind.Chain, Connectivity.AutoKind(Reg, doc, step.Id));
            var all = Connectivity.Connected(Reg, doc, step.Id, ConnectKind.Chain);
            Assert.Equal(5, all.Count); // three steps and two connectors
            Assert.Equal(step.Id, all[0]);
            var fromMiddle = Connectivity.Connected(Reg, doc, first.ObjectId, ConnectKind.Chain);
            Assert.Equal(3, fromMiddle.Count);
            Assert.DoesNotContain(step.Id, fromMiddle);
            Assert.Equal(new[] { step.Id }, Connectivity.Connected(Reg, doc, step.Id, ConnectKind.Loop));
        }

        [Fact]
        public void FourWallsAroundARoomAreItsClosedLoop()
        {
            var item = Reg.Library.First(i => i.TypeId == "arch.wall" && (i.View ?? "plan") == "plan");
            SketchObject Wall(Vec2 a, Vec2 b) => Reg.Create(item, a) with { Points = new List<Vec2> { a, b } };
            Vec2 p0 = new(40, 40), p1 = new(200, 40), p2 = new(200, 140), p3 = new(40, 140);
            var walls = new[] { Wall(p0, p1), Wall(p1, p2), Wall(p2, p3), Wall(p3, p0) };
            var stub = Wall(p2, new Vec2(260, 140));
            var doc = Doc(walls.Append(stub).ToArray());

            Assert.Equal(ConnectKind.Walls, Connectivity.AutoKind(Reg, doc, walls[0].Id));
            var joined = Connectivity.Connected(Reg, doc, walls[0].Id, ConnectKind.Walls);
            Assert.Equal(5, joined.Count);
            var room = Connectivity.Connected(Reg, doc, walls[0].Id, ConnectKind.Loop);
            Assert.Equal(walls.Select(w => w.Id).OrderBy(x => x), room.OrderBy(x => x));
            var kinds = Connectivity.Options(Reg, doc, walls[2].Id).Select(o => o.Kind).ToList();
            Assert.Contains(ConnectKind.Walls, kinds);
            Assert.Contains(ConnectKind.Loop, kinds);
        }
    }
}
