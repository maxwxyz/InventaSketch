using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Engine.Tests
{
    /// <summary>Domain exports: bill of materials, connection graph (Mermaid/GraphML), netlist, reference sign list.</summary>
    public class ExportTests
    {
        private static readonly Registry Reg = ModuleCatalog.CreateDefault();

        private static SketchDocument Doc(params SketchObject[] objects)
        {
            var doc = SketchDocument.Create();
            return Edits.Add(doc, doc.Pages[0].Id, objects);
        }

        private static string Run(string exporter, SketchDocument doc, Lang lang = Lang.En)
            => Exports.All(Reg).Single(e => e.Id == exporter).Export(Reg, doc, doc.ObjectsOn(doc.Pages[0]).ToList(), lang);

        [Fact]
        public void BillOfMaterialsCountsEqualParts()
        {
            var g1 = Reg.Create(Reg.Item("mech.spur.axial")!, new Vec2(50, 50));
            var g2 = Reg.Create(Reg.Item("mech.spur.axial")!, new Vec2(90, 50));
            var shaft = Reg.Create(Reg.Library.First(i => i.TypeId == "mech.shaft"), new Vec2(70, 80));
            var arrow = Reg.Create(Reg.Item("ann.arrow")!, new Vec2(10, 10));
            var doc = Doc(g1, g2, shaft, arrow);
            Assert.Contains(Exports.For(Reg, doc, doc.ObjectsOn(doc.Pages[0]).ToList()), e => e.Id == "core.bom");
            var csv = Run("core.bom", doc, Lang.De).Split('\n').Select(l => l.TrimEnd('\r')).Where(l => l.Length > 0).ToList();
            Assert.StartsWith("Pos,Bezugszeichen,Bezeichnung", csv[0]);
            Assert.Equal(3, csv.Count); // header, gears (2), shaft; the arrow is annotation
            Assert.Contains(csv, l => l.Contains(",Stirnrad,2,") || l.Contains("Stirnrad") && l.Contains(",2,"));
        }

        [Fact]
        public void ConnectionGraphFollowsAttachedConnectors()
        {
            var a = Reg.Create(Reg.Item("diag.uml.class")!, new Vec2(40, 40)) with { Text = "Order" };
            var b = Reg.Create(Reg.Item("diag.uml.class")!, new Vec2(120, 40)) with { Text = "Customer" };
            var rel = Reg.Create(Reg.Library.First(i => i.TypeId == "diag.uml.relation"), new Vec2(80, 40)) with
            {
                Points = new[] { new Vec2(60, 40), new Vec2(100, 40) },
                Attach = new Attachment?[] { new Attachment(a.Id, 0.5, 0), new Attachment(b.Id, -0.5, 0) },
            };
            var doc = Doc(a, b, rel);
            var mermaid = Run("core.graph.mermaid", doc);
            Assert.StartsWith("flowchart LR", mermaid);
            Assert.Contains("n1[\"Order\"]", mermaid);
            Assert.Contains("n1 --", mermaid);
            Assert.Contains("n2", mermaid);
            var graphml = Run("core.graph.graphml", doc);
            Assert.Contains("<edge", graphml);
            Assert.Contains("Customer", graphml);
            System.Xml.Linq.XDocument.Parse(graphml);
        }

        [Fact]
        public void NetlistJoinsPinsThroughWires()
        {
            var r = Reg.Create(Reg.Library.First(i => i.TypeId == "elec.resistor"), new Vec2(40, 40)).WithParam("ref", "R1");
            var c = Reg.Create(Reg.Library.First(i => i.TypeId == "elec.capacitor"), new Vec2(100, 40)).WithParam("ref", "C1");
            var doc0 = Doc(r, c);
            var ctx = new RenderContext(doc0);
            var rPins = Reg.PageAnchors(r, ctx).Where(x => x.Role == AnchorRole.Port).ToList();
            var cPins = Reg.PageAnchors(c, ctx).Where(x => x.Role == AnchorRole.Port).ToList();
            var wire = Reg.Create(Reg.Library.First(i => i.TypeId == "elec.wire"), Vec2.Zero) with
            {
                Points = new[] { rPins[1].P, cPins[0].P },
                Attach = new Attachment?[] { new Attachment(r.Id, 0.5, 0) { AnchorId = rPins[1].Id }, new Attachment(c.Id, -0.5, 0) { AnchorId = cPins[0].Id } },
            };
            var doc = Edits.Add(doc0, doc0.Pages[0].Id, wire);
            var csv = Run("elec.netlist", doc);
            Assert.Contains("N1,", csv);
            Assert.Contains("R1." + rPins[1].Id, csv);
            Assert.Contains("C1." + cPins[0].Id, csv);
        }

        [Fact]
        public void RuleCheckReportsOpenPinsOpenEndsAndShorts()
        {
            var r = Reg.Create(Reg.Library.First(i => i.TypeId == "elec.resistor"), new Vec2(40, 40)).WithParam("ref", "R1");
            var c = Reg.Create(Reg.Library.First(i => i.TypeId == "elec.capacitor"), new Vec2(100, 40)).WithParam("ref", "C1");
            var r2 = Reg.Create(Reg.Library.First(i => i.TypeId == "elec.resistor"), new Vec2(40, 100)).WithParam("ref", "R2");
            var doc0 = Doc(r, c, r2);
            var ctx = new RenderContext(doc0);
            List<Anchor> Pins(SketchObject o) => Reg.PageAnchors(o, ctx).Where(x => x.Role == AnchorRole.Port).ToList();
            var rPins = Pins(r); var cPins = Pins(c); var r2Pins = Pins(r2);
            SketchObject Wire(Vec2 a, Vec2 b, Attachment? atA, Attachment? atB)
                => Reg.Create(Reg.Library.First(i => i.TypeId == "elec.wire"), Vec2.Zero) with { Points = new[] { a, b }, Attach = new[] { atA, atB } };
            var joined = Wire(rPins[1].P, cPins[0].P, new Attachment(r.Id, 0.5, 0) { AnchorId = rPins[1].Id }, new Attachment(c.Id, -0.5, 0) { AnchorId = cPins[0].Id });
            // Lies on C1's second pin without docking (counts as connected), other end in the open.
            var loose = Wire(cPins[1].P, cPins[1].P + new Vec2(20, 0), null, null);
            var shorting = Wire(r2Pins[0].P, r2Pins[1].P, new Attachment(r2.Id, -0.5, 0) { AnchorId = r2Pins[0].Id }, new Attachment(r2.Id, 0.5, 0) { AnchorId = r2Pins[1].Id });
            var doc = Edits.Add(doc0, doc0.Pages[0].Id, joined, loose, shorting);
            Assert.Contains(Exports.For(Reg, doc, doc.ObjectsOn(doc.Pages[0]).ToList()), e => e.Id == "elec.erc");
            var rows = Run("elec.erc", doc).Split('\n').Select(l => l.TrimEnd('\r')).Where(l => l.Length > 0).Skip(1).ToList();
            Assert.Contains(rows, l => l.StartsWith("pin.unconnected,R1," + rPins[0].Id + ",", System.StringComparison.Ordinal));
            Assert.DoesNotContain(rows, l => l.StartsWith("pin.unconnected,C1,", System.StringComparison.Ordinal));
            Assert.DoesNotContain(rows, l => l.StartsWith("pin.unconnected,R2,", System.StringComparison.Ordinal));
            Assert.Single(rows, l => l.StartsWith("wire.open,", System.StringComparison.Ordinal));
            Assert.Contains(rows, l => l.StartsWith("part.shorted,R2,", System.StringComparison.Ordinal));
            Assert.Equal(3, rows.Count);
            Assert.StartsWith("Regel,Objekt,Pin,Befund", Run("elec.erc", doc, Lang.De));
        }

        [Fact]
        public void ReferenceListListsDrawnNumerals()
        {
            var n = Reg.Create(Reg.Library.First(i => i.TypeId == "patent.numeral"), new Vec2(40, 40)) with { Ref = "10" };
            var doc = Doc(n);
            Assert.Contains(Exports.For(Reg, doc, doc.ObjectsOn(doc.Pages[0]).ToList()), e => e.Id == "patent.reference-list");
            Assert.Contains("10", Run("patent.reference-list", doc));
        }
    }
}
