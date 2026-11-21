using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Engine.Tests
{
    /// <summary>
    /// Line branches: a line end docked onto the rail of another line (wire, pipe, road) is attached to that rail, follows
    /// the line when it moves or changes, and connection exports count the branch as connected.
    /// </summary>
    public class BranchTests
    {
        private static readonly Registry Reg = ModuleCatalog.CreateDefault();

        private static JsonElement Call(EngineApi api, string method, object args) => EngineApiTests.Call(api, method, args);

        private static Vec2 Point(JsonElement state, string id, int index)
        {
            var p = EngineApiTests.Obj(state, id).GetProperty("points")[index];
            return new Vec2(p.GetProperty("x").GetDouble(), p.GetProperty("y").GetDouble());
        }

        [Fact]
        public void BranchWireFollowsTheWireItJoins()
        {
            var api = new EngineApi(Reg);
            var s = Call(api, "createSession", new { lang = "en" }).GetProperty("session").GetString()!;
            var wireItem = Reg.Library.First(i => i.TypeId == "elec.wire").Id;
            var main = EngineApiTests.Insert(api, s, wireItem, 70, 40);
            var branch = EngineApiTests.Insert(api, s, wireItem, 70, 80);
            Call(api, "setPoint", new { session = s, id = main, index = 0, x = 20.0, y = 40.0, snap = false });
            Call(api, "setPoint", new { session = s, id = main, index = 1, x = 120.0, y = 40.0, snap = false });
            Call(api, "setPoint", new { session = s, id = branch, index = 0, x = 70.0, y = 90.0, snap = false });
            // Dragged near the middle of the main wire: snaps onto its rail and attaches.
            var st = Call(api, "setPoint", new { session = s, id = branch, index = 1, x = 70.4, y = 40.6, snap = true, tolerance = 3.0 });
            var end = Point(st, branch, 1);
            Assert.Equal(40, end.Y, 6);
            Assert.Equal(70.4, end.X, 6);

            st = Call(api, "move", new { session = s, ids = new[] { main }, dx = 0.0, dy = 10.0 });
            Assert.Equal(new Vec2(70.4, 50), Point(st, branch, 1));
            Assert.Equal(new Vec2(70, 90), Point(st, branch, 0));

            // Lengthening the main wire keeps the branch at its fraction along the rail (0.504).
            st = Call(api, "setPoint", new { session = s, id = main, index = 0, x = 0.0, y = 50.0, snap = false });
            var moved = Point(st, branch, 1);
            Assert.Equal(0.504 * 120, moved.X, 6);
            Assert.Equal(50, moved.Y, 6);

            // Undo brings the branch back with the main wire.
            st = Call(api, "undo", new { session = s });
            Assert.Equal(new Vec2(70.4, 50), Point(st, branch, 1));
        }

        [Fact]
        public void AttachingToALineNeedsARail()
        {
            var arrow = Reg.Create(Reg.Item("ann.arrow")!, new Vec2(50, 50));
            var end = Anchor.Point(AnchorRole.End, new Vec2(40, 50), "p0");
            Assert.Null(Attachments.For(arrow, new Vec2(40, 50), end));
            var rail = Anchor.Segment(AnchorRole.Rail, new Vec2(0, 0), new Vec2(10, 0)) with { Id = "rail3" };
            var a = Attachments.For(arrow, new Vec2(2.5, 1), rail)!;
            Assert.Equal("rail3", a.AnchorId);
            Assert.Equal(0.25, a.U, 9);
        }

        private static SketchDocument Doc(params SketchObject[] objects)
        {
            var doc = SketchDocument.Create();
            return Edits.Add(doc, doc.Pages[0].Id, objects);
        }

        private static List<Anchor> Ports(SketchDocument doc, SketchObject o)
            => Reg.PageAnchors(o, new RenderContext(doc)).Where(x => x.Kind == AnchorKind.Point && x.Role == AnchorRole.Port).ToList();

        private static Anchor FirstRail(SketchDocument doc, SketchObject line)
            => Reg.PageAnchors(line, new RenderContext(doc)).First(x => x.Kind == AnchorKind.Segment && x.Role == AnchorRole.Rail);

        private static string Run(string exporter, SketchDocument doc)
            => Exports.All(Reg).Single(e => e.Id == exporter).Export(Reg, doc, doc.ObjectsOn(doc.Pages[0]).ToList(), Lang.En);

        private static SketchObject Line(string typeId, Vec2 a, Vec2 b, Attachment? atA, Attachment? atB)
            => Reg.Create(Reg.Library.First(i => i.TypeId == typeId), Vec2.Zero) with { Points = new[] { a, b }, Attach = new[] { atA, atB } };

        [Fact]
        public void NetlistJoinsABranchWireIntoTheNet()
        {
            var r1 = Reg.Create(Reg.Library.First(i => i.TypeId == "elec.resistor"), new Vec2(30, 40)).WithParam("ref", "R1");
            var c1 = Reg.Create(Reg.Library.First(i => i.TypeId == "elec.capacitor"), new Vec2(130, 40)).WithParam("ref", "C1");
            var r2 = Reg.Create(Reg.Library.First(i => i.TypeId == "elec.resistor"), new Vec2(80, 100)).WithParam("ref", "R2");
            var doc0 = Doc(r1, c1, r2);
            var p1 = Ports(doc0, r1)[1]; var pc = Ports(doc0, c1)[0]; var p2 = Ports(doc0, r2)[0];
            var main = Line("elec.wire", p1.P, pc.P, new Attachment(r1.Id, 0, 0) { AnchorId = p1.Id }, new Attachment(c1.Id, 0, 0) { AnchorId = pc.Id });
            var doc1 = Edits.Add(doc0, doc0.Pages[0].Id, main);
            var rail = FirstRail(doc1, main);
            var on = Vec2.Lerp(rail.P, rail.Q, 0.5);
            var branch = Line("elec.wire", p2.P, on, new Attachment(r2.Id, 0, 0) { AnchorId = p2.Id }, new Attachment(main.Id, 0.5, 0) { AnchorId = rail.Id });
            var doc = Edits.Add(doc1, doc1.Pages[0].Id, branch);
            var rows = Run("elec.netlist", doc).Split('\n').Select(l => l.TrimEnd('\r')).Where(l => l.Length > 0).Skip(1).ToList();
            var net = Assert.Single(rows);
            Assert.Contains("R1." + p1.Id, net);
            Assert.Contains("C1." + pc.Id, net);
            Assert.Contains("R2." + p2.Id, net);
            // The rule check sees the branch end as connected.
            Assert.DoesNotContain("wire.open", Run("elec.erc", doc));
        }

        [Fact]
        public void FluidConnectionListJoinsABranchLine()
        {
            var pump = Reg.Create(Reg.Library.First(i => i.TypeId == "fluid.pump"), new Vec2(30, 80)).WithParam("ref", "0P1");
            var valve = Reg.Create(Reg.Library.First(i => i.TypeId == "fluid.valve.directional"), new Vec2(120, 30)).WithParam("ref", "1V1");
            var gauge = Reg.Create(Reg.Library.First(i => i.TypeId == "fluid.gauge"), new Vec2(80, 120)).WithParam("ref", "0Z1");
            var doc0 = Doc(pump, valve, gauge);
            var pp = Ports(doc0, pump)[0]; var pv = Ports(doc0, valve).First(x => x.Id == "P"); var pg = Ports(doc0, gauge)[0];
            var main = Line("fluid.line", pp.P, pv.P, new Attachment(pump.Id, 0, 0) { AnchorId = pp.Id }, new Attachment(valve.Id, 0, 0) { AnchorId = pv.Id });
            var doc1 = Edits.Add(doc0, doc0.Pages[0].Id, main);
            var rail = FirstRail(doc1, main);
            var branch = Line("fluid.line", pg.P, Vec2.Lerp(rail.P, rail.Q, 0.5), new Attachment(gauge.Id, 0, 0) { AnchorId = pg.Id }, new Attachment(main.Id, 0.5, 0) { AnchorId = rail.Id });
            var doc = Edits.Add(doc1, doc1.Pages[0].Id, branch);
            var rows = Run("fluid.connections", doc).Split('\n').Select(l => l.TrimEnd('\r')).Where(l => l.Length > 0).Skip(1).ToList();
            var row = Assert.Single(rows);
            Assert.Contains("0P1.", row);
            Assert.Contains("1V1.P", row);
            Assert.Contains("0Z1.", row);
        }
    }
}
