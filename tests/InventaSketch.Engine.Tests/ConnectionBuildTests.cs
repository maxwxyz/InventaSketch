using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Engine.Tests
{
    /// <summary>Build "+" for connections (maintainer report 2026-11-25): PV, building services, electrical, cross-module media.</summary>
    public class ConnectionBuildTests
    {
        private static readonly Registry Reg = ModuleCatalog.CreateDefault();

        private static (SketchDocument Doc, SketchObject Obj) Alone(string itemId)
        {
            var o = Reg.Create(Reg.Item(itemId)!, new Vec2(200, 150));
            var doc = SketchDocument.Create(new Size2(400, 300));
            return (Edits.Add(doc, doc.Pages[0].Id, o), o);
        }

        [Fact]
        public void PvModuleToInverterToGridThroughTheEngine()
        {
            var api = new EngineApi(ModuleCatalog.CreateDefault());
            var session = EngineApiTests.Call(api, "createSession", new { lang = "en", pageWidth = 400.0, pageHeight = 300.0 }).GetProperty("session").GetString()!;
            var pv = EngineApiTests.Call(api, "insert", new { session, itemId = "energy.pv.module", x = 100.0, y = 100.0 }).GetProperty("selection")[0].GetString()!;

            // The DC port of the PV module: "+" with the DC cable first.
            var dc = EngineApiTests.Call(api, "buildPorts", new { session, id = pv }).EnumerateArray().Single(p => p.GetProperty("kind").GetString() == "Connect");
            Assert.Equal(PortMedia.ElectricDc, dc.GetProperty("medium").GetString());
            var items = EngineApiTests.Call(api, "buildItems", new { session, id = pv, key = dc.GetProperty("id").GetString() }).EnumerateArray().ToList();
            Assert.Equal("energy.line.cable.dc", items[0].GetProperty("id").GetString());
            Assert.Contains(items, i => i.GetProperty("typeId").GetString() == "energy.conv.inverter");

            // Placing the cable selects it; its free end offers "+" with the inverter.
            var st = EngineApiTests.Call(api, "buildPlace", new { session, id = pv, key = dc.GetProperty("id").GetString(), itemId = "energy.line.cable.dc" });
            var cable = st.GetProperty("selection")[0].GetString()!;
            var doc = api.Sessions.Get(session).Doc;
            Assert.Equal(pv, doc.Get(cable)!.Attach![0]!.ObjectId);
            Assert.True(doc.Get(cable)!.Flag("dc", false));
            var end = EngineApiTests.Call(api, "buildPorts", new { session, id = cable }).EnumerateArray().Single();
            Assert.Equal("LineEnd", end.GetProperty("kind").GetString());
            var endItems = EngineApiTests.Call(api, "buildItems", new { session, id = cable, key = end.GetProperty("id").GetString() }).EnumerateArray().ToList();
            Assert.Equal("energy.line.cable", endItems[0].GetProperty("typeId").GetString());                      // continue the cable
            var inverter = endItems.First(i => i.GetProperty("typeId").GetString() == "energy.conv.inverter").GetProperty("id").GetString();
            Assert.DoesNotContain(endItems, i => i.GetProperty("typeId").GetString()!.StartsWith("elec."));      // no resistor after a PV cable

            // The inverter docks with its DC port on the cable end; its AC port offers an AC cable.
            st = EngineApiTests.Call(api, "buildPlace", new { session, id = cable, key = end.GetProperty("id").GetString(), itemId = inverter });
            var inv = st.GetProperty("selection")[0].GetString()!;
            doc = api.Sessions.Get(session).Doc;
            var att = doc.Get(cable)!.Attach!.Last()!;
            Assert.Equal(inv, att.ObjectId);
            Assert.Equal("dc", att.AnchorId);
            var ac = EngineApiTests.Call(api, "buildPorts", new { session, id = inv }).EnumerateArray().Single(p => p.GetProperty("kind").GetString() == "Connect");
            Assert.Equal(PortMedia.ElectricAc, ac.GetProperty("medium").GetString());

            // One undo step per build.
            EngineApiTests.Call(api, "undo", new { session });
            Assert.Null(api.Sessions.Get(session).Doc.Get(inv));
        }

        [Fact]
        public void RadiatorFlowAndReturnOfferTheirOwnPipes()
        {
            var (doc, rad) = Alone("bs.heat.radiator");
            var ports = Building.Ports(Reg, doc, rad.Id).Where(p => p.Kind == PortKind.Connect).ToList();
            var flow = ports.Single(p => p.AnchorId == "flow");
            var ret = ports.Single(p => p.AnchorId == "return");
            Assert.Equal("bs.pipe.flow", Building.Items(Reg, doc, rad.Id, flow)[0].Id);
            Assert.Equal("bs.pipe.return", Building.Items(Reg, doc, rad.Id, ret)[0].Id);
            // A boiler chosen at the flow port docks with its own flow connection.
            var built = Building.Apply(Reg, doc, rad.Id, flow, Reg.Item("bs.heat.boiler")!)!;
            var pipe = built.Doc.Objects.Values.Single(o => o.IsPointGeometry);
            Assert.Equal("flow", pipe.Attach![0]!.AnchorId);
            Assert.Equal("flow", pipe.Attach[pipe.Attach.Count - 1]!.AnchorId);
            Assert.Equal(built.SelectId, pipe.Attach[pipe.Attach.Count - 1]!.ObjectId);
        }

        [Fact]
        public void ConnectedPortsOfferNoPlus()
        {
            var (doc, r) = Alone("elec.resistor");
            var port = Building.Ports(Reg, doc, r.Id).First(p => p.Kind == PortKind.Connect);
            var built = Building.Apply(Reg, doc, r.Id, port, Reg.Item("elec.wire")!)!;
            Assert.DoesNotContain(Building.Ports(Reg, built.Doc, r.Id), p => p.Id == port.Id);
            Assert.Single(Building.Ports(Reg, built.Doc, r.Id), p => p.Kind == PortKind.Connect);
        }

        [Fact]
        public void AHandDrawnLineFromAPortCarriesItsMedium()
        {
            // An arrow (no medium of its own) drawn from a drive block's side continues like a connector.
            var (doc, block) = Alone("machines.block.battery");
            var port = Reg.PageAnchors(block, new RenderContext(doc)).First(a => a.Role == AnchorRole.Port && a.Id == "e");
            var arrow = Reg.CreateBetween(Reg.Item("ann.arrow")!, port.P, port.P + new Vec2(20, 0));
            arrow = arrow with { Attach = new[] { Attachments.For(block, port.P, port), null } };
            doc = Edits.Add(doc, doc.Pages[0].Id, arrow);
            Assert.Equal(PortMedia.Block, Building.LineMedium(Reg, doc, arrow));
            var end = Assert.Single(Building.Ports(Reg, doc, arrow.Id));
            Assert.Equal(PortKind.LineEnd, end.Kind);
            Assert.Contains(Building.Items(Reg, doc, arrow.Id, end), i => i.TypeId.StartsWith("machines.drive."));
        }

        [Fact]
        public void MediaFitByPrefix()
        {
            Assert.True(PortMedia.Fits(PortMedia.Electric, PortMedia.ElectricDc));
            Assert.True(PortMedia.Fits(PortMedia.HeatingFlow, PortMedia.Heating));
            Assert.False(PortMedia.Fits(PortMedia.HeatingFlow, PortMedia.HeatingReturn));
            Assert.False(PortMedia.Fits("electric", "electricity"));
            Assert.True(PortMedia.Covers(PortMedia.Electric, PortMedia.ElectricAc));
            Assert.False(PortMedia.Covers(PortMedia.ElectricAc, PortMedia.Electric));
        }
    }
}
