using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using InventaSketch.Modules.Electrical;
using Xunit;

namespace InventaSketch.Modules.Switchgear.Tests
{
    /// <summary>
    /// One wire type (maintainer 2026-11-26): the switchgear devices connect with the electrical module's wires (core colour,
    /// cross-section, hops at crossings, junctions); the switchgear's own core items give way to them, the old core type keeps
    /// loading, rendering (with hops) and feeding the reports.
    /// </summary>
    public class ElectricalWireTests
    {
        private static readonly Registry Reg = new Registry().Register(new SwitchgearModule()).Register(new ElectricalModule());

        private static SketchObject Item(string id, double x = 100, double y = 100) => Reg.Create(Reg.Item(id) ?? throw new KeyNotFoundException(id), new Vec2(x, y));

        private static SketchObject Line(string itemId, SketchDocument doc, SketchObject a, string pa, SketchObject b, string pb)
        {
            var ctx = new RenderContext(doc);
            var pinA = Reg.PageAnchors(a, ctx).First(x => x.Role == AnchorRole.Port && x.Id == pa);
            var pinB = Reg.PageAnchors(b, ctx).First(x => x.Role == AnchorRole.Port && x.Id == pb);
            var line = Reg.Create(Reg.Item(itemId)!, pinA.P) with { Points = new[] { pinA.P, pinB.P } };
            return line with { Attach = new Attachment?[] { Attachments.For(a, pinA.P, pinA), Attachments.For(b, pinB.P, pinB) } };
        }

        private static SketchObject Free(string itemId, Vec2 a, Vec2 b) => Reg.Create(Reg.Item(itemId)!, a) with { Points = new[] { a, b } };

        [Fact]
        public void TheSwitchgearCoreItemsGiveWayToTheElectricalWires()
        {
            var offered = Reg.Library.Select(i => i.Id).ToList();
            foreach (var id in new[] { "sw.line.wire", "sw.line.wire.bn", "sw.line.wire.bu", "sw.line.wire.pe" })
            {
                Assert.DoesNotContain(id, offered);
                Assert.NotNull(Reg.Item(id));
            }
            foreach (var id in new[] { "elec.wire.bk", "elec.wire.bn", "elec.wire.bu", "elec.wire.pe" }) Assert.Contains(id, offered);
            Assert.Equal("GNYE", Reg.Item("elec.wire.pe")!.Params!["colour"].Text);
            // The cables are a different thing (sheathed multi-core cable for the cable list) and stay.
            Assert.Contains("sw.line.cable", offered);
            Assert.Contains("sw.line.cable.knx", offered);
            // Without the electrical module the switchgear's own cores are offered.
            var alone = new Registry().Register(new SwitchgearModule());
            Assert.Contains("sw.line.wire.bu", alone.Library.Select(i => i.Id));
        }

        [Fact]
        public void PortsOfferTheElectricalWireInThePinsColour()
        {
            var mcb = Item("sw.protection.mcb");
            var n = Item("sw.terminal.n", 150, 100);
            var pe = Item("sw.terminal.pe", 170, 100);
            var d = SketchDocument.Create(new Size2(600, 400));
            d = Edits.Add(d, d.Pages[0].Id, mcb, n, pe);
            string FirstConnector(SketchObject o)
            {
                var port = Building.Ports(Reg, d, o.Id).First(p => p.Kind == PortKind.Connect && p.Medium == PortMedia.Electric);
                return Building.Items(Reg, d, o.Id, port, 6)[0].Id;
            }
            Assert.Equal("elec.wire.bk", FirstConnector(mcb));
            Assert.Equal("elec.wire.bu", FirstConnector(n));
            Assert.Equal("elec.wire.pe", FirstConnector(pe));
            // The "+" offers no second, switchgear wire.
            var port = Building.Ports(Reg, d, mcb.Id).First(p => p.Kind == PortKind.Connect);
            Assert.DoesNotContain(Building.Items(Reg, d, mcb.Id, port, 8), i => i.TypeId == "sw.line.wire");
        }

        [Fact]
        public void TerminalPlanAndFeedTreeReadTheElectricalWire()
        {
            var f1 = Item("sw.protection.rcd", 100, 100).WithParam("ref", "-F1");
            var f2 = Item("sw.protection.mcb", 160, 100).WithParam("ref", "-F2");
            var x1 = Item("sw.terminal.feedthrough", 150, 220).WithParam("ref", "-X1").WithParam("number", "1");
            var doc = SketchDocument.Create(new Size2(600, 400));
            doc = Edits.Add(doc, doc.Pages[0].Id, f1, f2, x1);
            var w1 = Line("elec.wire.bu", doc, f2, "2", x1, "1").WithParam("ref", "W12").WithParam("section", "2.5");
            var w2 = Line("elec.wire.bk", doc, f1, "2", f2, "1");
            doc = Edits.Add(doc, doc.Pages[0].Id, w1, w2);
            var e = Exports.All(Reg).Single(x => x.Id == "sw.terminals.csv");
            var csv = e.Export(Reg, doc, doc.ObjectsOn(doc.Pages[0]).ToList(), Lang.En).Split('\n').Select(l => l.TrimEnd('\r')).ToList();
            Assert.Contains("-X1,1,,top,W12,-F2:2,2.5,BU,", csv);
            // A wire without colour or cross-section claims none.
            var plain = Line("elec.wire", doc, f2, "2", x1, "1");
            var doc2 = Edits.Add(Edits.Remove(doc, new[] { w1.Id }), doc.Pages[0].Id, plain);
            var csv2 = e.Export(Reg, doc2, doc2.ObjectsOn(doc2.Pages[0]).ToList(), Lang.En).Split('\n').Select(l => l.TrimEnd('\r')).ToList();
            Assert.Contains("-X1,1,,top,,-F2:2,,,", csv2);
            // The feed tree follows the electrical wire from the RCD's bottom pin to the breaker's top pin.
            var tree = new Board(doc, doc.ObjectsOn(doc.Pages[0]).ToList(), Lang.En).FeedTree();
            Assert.Contains(tree.SelectMany(Flatten), node => node.Designation == "-F2" && node.Upstream == "-F1");
        }

        private static IEnumerable<Board.Node> Flatten(Board.Node n) => new[] { n }.Concat(n.Children.SelectMany(Flatten));

        [Fact]
        public void OldCoresAndCablesHopAtCrossingsWithElectricalWires()
        {
            var doc = SketchDocument.Create(new Size2(600, 400));
            var wire = Free("elec.wire.free", new Vec2(50, 100), new Vec2(150, 100));
            var core = Free("sw.line.wire.bn", new Vec2(100, 50), new Vec2(100, 150));
            var cable = Free("sw.line.cable", new Vec2(60, 50), new Vec2(60, 150));
            doc = Edits.Add(doc, doc.Pages[0].Id, wire, core, cable);
            var ctx = new RenderContext(doc);
            // Drawn later, the old core and the cable hop over the electrical wire (a path with a curve), the wire does not.
            Assert.Contains(Prim.Flatten(Reg.Render(core, ctx)), p => p is PathPrim pa && pa.Segments.Any(s => s.Op == SegOp.C));
            Assert.Contains(Prim.Flatten(Reg.Render(cable, ctx)), p => p is PathPrim pa && pa.Segments.Any(s => s.Op == SegOp.C));
            Assert.DoesNotContain(Prim.Flatten(Reg.Render(wire, ctx)), p => p is PathPrim pa && pa.Segments.Any(s => s.Op == SegOp.C));
            Assert.True(Reg.Type("sw.line.wire")!.HopsAtCrossings);
            Assert.Contains(wire.Id, Reg.Type("sw.line.wire")!.Dependents(core, ctx));
            // Connecting at the crossing docks the old core onto the wire with a junction dot.
            var crossing = Crossings.Find(Reg, doc, core).Single(c => c.Other == wire.Id);
            var connected = Crossings.Connect(Reg, doc, core.Id, crossing);
            Assert.Contains(Prim.Flatten(Reg.Render(connected.Get(core.Id)!, new RenderContext(connected))), p => p.Tag == "junction");
        }

        [Fact]
        public void TheElectricalWireDrawsItsCoreColour()
        {
            var doc = SketchDocument.Create(new Size2(600, 400));
            var pe = Free("elec.wire.pe", new Vec2(50, 100), new Vec2(150, 100));
            var bu = Free("elec.wire.bu", new Vec2(50, 120), new Vec2(150, 120));
            var plain = Free("elec.wire", new Vec2(50, 140), new Vec2(150, 140));
            doc = Edits.Add(doc, doc.Pages[0].Id, pe, bu, plain);
            var ctx = new RenderContext(doc);
            string?[] Strokes(SketchObject o) => Prim.Flatten(Reg.Render(o, ctx)).Where(p => p.Tag == "wire").Select(p => p is PathPrim pa ? pa.Style.Stroke : null).ToArray();
            Assert.Equal(new[] { "#43a047", "#fdd835" }, Strokes(pe));
            Assert.Equal(new[] { "#1e88e5" }, Strokes(bu));
            Assert.Single(Strokes(plain));
            Assert.Equal(LineMedia.OwnColour, Reg.Type("elec.wire")!.MediumRole(bu));
            Assert.Null(Reg.Type("elec.wire")!.MediumRole(plain));
        }
    }
}
