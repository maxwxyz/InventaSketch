using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Electrical.Tests
{
    /// <summary>
    /// Core colours of the wire (cabinet wiring, round of 2026-11-26) and "Make bridge (not connected)" on a wire at a
    /// junction and on a junction dot.
    /// </summary>
    public class WireColourBridgeTests
    {
        private static readonly Registry R = new Registry().Register(new ElectricalModule());

        private static SketchObject Wire(Vec2 a, Vec2 b, string item = "elec.wire.free")
            => R.Create(R.Item(item)!, a).WithParam("route", "free") with { Points = new[] { a, b } };

        private static SketchDocument Doc(params SketchObject[] objects)
        {
            var doc = SketchDocument.Create();
            return Edits.Add(doc, doc.Pages[0].Id, objects);
        }

        [Fact]
        public void CoreColourItemsAreBilingualAndSetTheColour()
        {
            foreach (var (id, code) in new[] { ("elec.wire.bk", "BK"), ("elec.wire.bn", "BN"), ("elec.wire.bu", "BU"), ("elec.wire.pe", "GNYE") })
            {
                var item = R.Item(id)!;
                Assert.Equal("elec.wire", item.TypeId);
                Assert.False(string.IsNullOrEmpty(item.Name.En) || string.IsNullOrEmpty(item.Name.De) || item.Name.En == item.Name.De);
                Assert.Equal(code, R.Create(item, Vec2.Zero).Str("colour", "auto"));
            }
            var wire = Wire(new Vec2(0, 0), new Vec2(50, 0), "elec.wire");
            var colours = R.Actions(wire, new RenderContext(Doc(wire))).Where(a => a.Choice?.Key == "elec.wire.colour").ToList();
            Assert.Equal(11, colours.Count);
            Assert.True(colours.Single(a => a.IsActive).Id.EndsWith("=auto", System.StringComparison.Ordinal));
            Assert.All(colours, a => Assert.NotNull(a.Preview));
            // The colour round-trips through the document JSON.
            var doc = Doc(wire.WithParam("colour", "BN").WithParam("section", "2.5"));
            var back = DocumentJson.Deserialize(DocumentJson.Serialize(doc));
            Assert.Equal("BN", back.Get(wire.Id)!.Str("colour", ""));
            Assert.Equal("2.5", back.Get(wire.Id)!.Str("section", ""));
        }

        [Fact]
        public void AWireAtAJunctionOffersToMakeItABridge()
        {
            var under = Wire(new Vec2(50, 0), new Vec2(50, 100));
            var over = Wire(new Vec2(0, 50), new Vec2(100, 50));
            var doc = Doc(under, over);
            Assert.DoesNotContain(R.Actions(over, new RenderContext(doc)), a => a.Id == "elec.bridge");
            var joined = Crossings.ConnectBetween(R, doc, over.Id, under.Id);
            var half = joined.Get(over.Id)!;
            var bridge = R.Actions(half, new RenderContext(joined)).Single(a => a.Id == "elec.bridge");
            Assert.Equal(IconIds.Bridge, bridge.Icon);
            Assert.Equal("Als Brücke (nicht verbunden)", bridge.Label.De);
            var back = ContextActions.Run(R, joined, new[] { over.Id }, "elec.bridge", null, Lang.En);
            Assert.Equal(2, back.Objects.Values.Count(o => o.Type == "elec.wire"));
            var line = back.Get(over.Id)!;
            Assert.Equal(2, line.Points!.Count);
            Assert.Single(Crossings.Find(R, back, line));
            Assert.DoesNotContain(R.Render(line, new RenderContext(back)), p => p.Tag == "junction");
        }

        [Fact]
        public void AJunctionDotWithWiresPassingItBecomesABridge()
        {
            var dot = R.Create(R.Item("elec.junction")!, new Vec2(50, 50));
            var doc = Doc(dot);
            var pin = R.PageAnchors(dot, new RenderContext(doc)).Single(a => a.Role == AnchorRole.Port);
            SketchObject To(Vec2 from, bool start) => Wire(start ? pin.P : from, start ? from : pin.P) with
            {
                Attach = start ? new[] { Attachments.For(dot, pin.P, pin), null } : new[] { null, Attachments.For(dot, pin.P, pin) },
            };
            var wires = new[] { To(new Vec2(0, 50), false), To(new Vec2(100, 50), true), To(new Vec2(50, 0), false) };
            doc = Edits.Add(doc, doc.Pages[0].Id, wires);
            var type = R.Type("elec.junction")!;
            Assert.True(type.HasDependents);
            Assert.Equal(3, type.Dependents(dot, new RenderContext(doc)).Count());
            Assert.Contains(R.Actions(dot, new RenderContext(doc)), a => a.Id == "elec.bridge");
            var bridged = ContextActions.Run(R, doc, new[] { dot.Id }, "elec.bridge", null, Lang.En);
            Assert.Null(bridged.Get(dot.Id));
            var lines = bridged.Objects.Values.Where(o => o.Type == "elec.wire").ToList();
            Assert.Equal(2, lines.Count);
            // The branch from above now runs a little beyond the through line and crosses it.
            Assert.Contains(lines, l => Crossings.Find(R, bridged, l).Count == 1);
            // Without wires the dot offers nothing.
            Assert.DoesNotContain(R.Actions(dot, new RenderContext(Doc(dot))), a => a.Id == "elec.bridge");
        }
    }
}
