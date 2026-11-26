using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Switchgear.Tests
{
    internal static class Fx
    {
        public static readonly Registry Reg = new Registry().Register(new SwitchgearModule());

        public static SketchObject Item(string id, double x = 100, double y = 100)
            => Reg.Create(Reg.Item(id) ?? throw new KeyNotFoundException(id), new Vec2(x, y));

        public static SketchDocument Doc(params SketchObject[] objects)
        {
            var doc = SketchDocument.Create(new Size2(600, 400));
            return Edits.Add(doc, doc.Pages[0].Id, objects);
        }

        public static RenderContext Ctx(SketchDocument doc, Lang lang = Lang.En) => new(doc, lang);

        public static IReadOnlyList<Primitive> Local(SketchObject o) => Reg.RenderLocal(o, Ctx(Doc(o)));

        /// <summary>A line from pin <paramref name="pa"/> of <paramref name="a"/> to pin <paramref name="pb"/> of <paramref name="b"/>, both ends docked.</summary>
        public static SketchObject Line(string itemId, SketchDocument doc, SketchObject a, string pa, SketchObject b, string pb)
        {
            var ctx = Ctx(doc);
            var pinA = Reg.PageAnchors(a, ctx).First(x => x.Role == AnchorRole.Port && x.Id == pa);
            var pinB = Reg.PageAnchors(b, ctx).First(x => x.Role == AnchorRole.Port && x.Id == pb);
            var line = Reg.Create(Reg.Item(itemId)!, pinA.P) with { Points = new[] { pinA.P, pinB.P } };
            return line with { Attach = new Attachment?[] { Attachments.For(a, pinA.P, pinA), Attachments.For(b, pinB.P, pinB) } };
        }
    }

    public class ModuleTests
    {
        private static readonly Registry Reg = Fx.Reg;

        [Fact]
        public void TypesAndItemsUseTheModulePrefixAndBilingualNames()
        {
            Assert.All(Reg.Types, t => Assert.StartsWith("sw.", t.Id));
            Assert.All(Reg.Library, i => Assert.StartsWith("sw.", i.Id));
            Assert.All(Reg.Library, i => Assert.False(string.IsNullOrWhiteSpace(i.Name.En) || string.IsNullOrWhiteSpace(i.Name.Get(Lang.De)), i.Id));
            Assert.All(Reg.Library, i => Assert.DoesNotContain("patent", (i.Name.En + i.Name.Get(Lang.De)).ToLowerInvariant()));
            Assert.Equal(Reg.Library.Count(), Reg.Library.Select(i => i.Id).Distinct().Count());
            Assert.True(Reg.Library.Count() >= 60, "items: " + Reg.Library.Count());
            Assert.Contains(Reg.Library, i => i.Name.Get(Lang.De).StartsWith("Leitungsschutzschalter", StringComparison.Ordinal));
            Assert.Contains(Reg.Library, i => i.Name.Get(Lang.De).StartsWith("FI-Schutzschalter", StringComparison.Ordinal));
            Assert.Contains(Reg.Types, t => t.Name.Get(Lang.De) == "Fehlerstromschutzschalter (Hutschiene)");
            Assert.Contains(Reg.Types, t => t.Name.Get(Lang.De) == "Durchgangsreihenklemme");
            Assert.Contains(Reg.Types, t => t.Name.Get(Lang.De) == "Hutschiene TS35");
        }

        [Fact]
        public void EveryItemRendersInsideItsBoxInEveryView()
        {
            foreach (var item in Reg.Library)
            {
                var o = Reg.Create(item, new Vec2(100, 100));
                var type = Reg.Type(o.Type)!;
                var views = type.Views.Count == 0 ? new string?[] { null } : type.Views.Select(v => (string?)v.Id).ToArray();
                foreach (var view in views)
                {
                    var x = view is null || o.IsPointGeometry ? o : Reg.WithView(o, view, Fx.Ctx(Fx.Doc(o)));
                    var prims = Fx.Local(x);
                    Assert.True(prims.Count > 0, item.Id + " " + view + " draws nothing");
                    if (x.IsPointGeometry) continue;
                    var b = Prim.Bounds(prims)!.Value;
                    const double tol = 1.0;
                    Assert.True(b.X >= -x.Size.W / 2 - tol && b.Right <= x.Size.W / 2 + tol && b.Y >= -x.Size.H / 2 - tol && b.Bottom <= x.Size.H / 2 + tol,
                        $"{item.Id} ({view}): drawing {b} outside box {x.Size.W}x{x.Size.H}");
                }
            }
        }

        [Fact]
        public void FreeScalingChangesSizeNotParameters()
        {
            foreach (var item in Reg.Library)
            {
                var o = Reg.Create(item, new Vec2(100, 100));
                if (o.IsPointGeometry) continue;
                var s = o with { Size = new Size2(o.Size.W * 1.7, o.Size.H * 0.6) };
                Assert.NotEmpty(Fx.Local(s));
                Assert.Equal(o.Params.OrderBy(kv => kv.Key), s.Params.OrderBy(kv => kv.Key));
            }
        }

        [Theory]
        [InlineData("sw.protection.mcb", 1)]
        [InlineData("sw.protection.mcb.c16.3p", 3)]
        [InlineData("sw.protection.mcb.b16.1n", 2)]
        [InlineData("sw.protection.rcd", 2)]
        [InlineData("sw.protection.rcd.4p", 4)]
        [InlineData("sw.protection.rcbo", 2)]
        [InlineData("sw.protection.spd", 4)]
        [InlineData("sw.switch.main", 3)]
        [InlineData("sw.meter.energy", 2)]
        [InlineData("sw.meter.energy.3", 4)]
        [InlineData("sw.switching.contactor", 1)]
        [InlineData("sw.switching.contactor.4", 2)]
        [InlineData("sw.bus.actuator", 4)]
        [InlineData("sw.bus.actuator.8", 8)]
        [InlineData("sw.bus.power", 4)]
        [InlineData("sw.socket.dinrail", 3)]
        public void DevicesAreWholeModulesWide(string itemId, int modules)
        {
            var o = Fx.Item(itemId);
            Assert.Equal(modules * Modular.Unit, o.Size.W, 6);
            Assert.Equal(Modular.DeviceHeight, o.Size.H, 6);
        }

        [Fact]
        public void MorePolesWidenTheDeviceAndKeepItsLeftEdge()
        {
            var mcb = Fx.Item("sw.protection.mcb", 100, 100);
            var left = mcb.Transform.X - mcb.Size.W / 2;
            var three = Reg.SetParam(mcb, "poles", "3");
            Assert.Equal(3 * Modular.Unit, three.Size.W, 6);
            Assert.Equal(left, three.Transform.X - three.Size.W / 2, 6);
            // The choice action does the same (and keeps a scaled device scaled).
            var scaled = mcb with { Size = new Size2(mcb.Size.W * 2, mcb.Size.H * 2) };
            var action = Reg.Type(mcb.Type)!.Actions(scaled, Fx.Ctx(Fx.Doc(scaled))).First(a => a.Id == "sw.poles=3");
            Assert.NotNull(action.Choice);
            var after = action.Apply(scaled);
            Assert.Equal(6 * Modular.Unit, after.Size.W, 6);
            Assert.Equal(scaled.Transform.X - scaled.Size.W / 2, after.Transform.X - after.Size.W / 2, 6);
        }

        [Fact]
        public void TerminalWidthFollowsTheCrossSection()
        {
            var t = Fx.Item("sw.terminal.feedthrough");
            Assert.Equal(5.2, t.Size.W, 6);
            Assert.Equal(8.2, Reg.SetParam(t, "section", "6").Size.W, 6);
        }

        [Fact]
        public void PinsArePortsWithAMediumAndTheirConnector()
        {
            var mcb = Fx.Item("sw.protection.mcb");
            var doc = Fx.Doc(mcb);
            var type = Reg.Type(mcb.Type)!;
            var ports = Reg.PageAnchors(mcb, Fx.Ctx(doc)).Where(a => a.Role == AnchorRole.Port).ToList();
            Assert.Equal(new[] { "1", "2" }, ports.Select(p => p.Id).OrderBy(x => x));
            Assert.True(ports.First(p => p.Id == "1").P.Y < ports.First(p => p.Id == "2").P.Y);
            Assert.All(ports, p => Assert.Equal(PortMedia.Electric, type.Connection(mcb, p)!.Medium));
            // One wire type: the screw terminals connect with the electrical module's core items (black by default).
            Assert.Equal("elec.wire.bk", type.Connection(mcb, ports[0])!.Connector);

            var knx = Fx.Item("sw.bus.actuator");
            var bus = Reg.Type(knx.Type)!.Anchors(knx, Fx.Ctx(Fx.Doc(knx))).First(a => a.Id == "bus");
            Assert.Equal(PortMedia.Data, Reg.Type(knx.Type)!.Connection(knx, bus)!.Medium);

            var socket = Fx.Item("sw.house.socket");
            var c = Reg.Type(socket.Type)!.Anchors(socket, Fx.Ctx(Fx.Doc(socket))).Single();
            Assert.Equal(PortMedia.ElectricInstallation, Reg.Type(socket.Type)!.Connection(socket, c)!.Medium);
        }

        [Fact]
        public void SymbolViewKeepsThePinIds()
        {
            foreach (var id in new[] { "sw.protection.rcd.4p", "sw.switching.relay", "sw.switching.contactor", "sw.protection.spd", "sw.terminal.multilevel" })
            {
                var o = Fx.Item(id);
                var front = Reg.Type(o.Type)!.Anchors(o, Fx.Ctx(Fx.Doc(o))).Where(a => a.Role == AnchorRole.Port).Select(a => a.Id).OrderBy(x => x).ToList();
                var sym = Reg.WithView(o, DeviceType.SymbolView, Fx.Ctx(Fx.Doc(o)));
                var symbol = Reg.Type(o.Type)!.Anchors(sym, Fx.Ctx(Fx.Doc(sym))).Where(a => a.Role == AnchorRole.Port).Select(a => a.Id).OrderBy(x => x).ToList();
                Assert.Equal(front, symbol);
                Assert.Equal(front.Count, front.Distinct().Count());
            }
        }

        [Fact]
        public void DevicesSnapOntoTheRailGridAndSideBySide()
        {
            var rail = Fx.Item("sw.rail.ts35", 200, 100);
            var grid = RailType.Grid(rail);
            Assert.Equal(13, grid.Count);
            // First device: its left edge snaps onto a grid point of the rail.
            var a = Fx.Item("sw.protection.rcd", 200 + grid[2] + 17.5 + 1.2, 101.5);
            var doc = Fx.Doc(rail, a);
            var page = doc.Pages[0];
            var opts = new SnapOptions { Tolerance = 4 };
            var ctx = Fx.Ctx(doc);
            var res = Snapper.SnapMove(Reg.PageAnchors(a, ctx).ToList(), Vec2.Zero, Snapper.Targets(Reg, doc, page, new[] { a.Id }, ctx, opts), opts);
            Assert.Equal("mount", res.Kind);
            a = a with { Transform = a.Transform with { X = a.Transform.X + res.Delta.X, Y = a.Transform.Y + res.Delta.Y } };
            Assert.Equal(100, a.Transform.Y, 6);
            Assert.Equal(200 + grid[2], a.Transform.X - a.Size.W / 2, 6);
            // Second device right of it: its left edge snaps onto the first one's right edge.
            var b = Fx.Item("sw.protection.mcb", a.Transform.X + a.Size.W / 2 + 8.75 + 1.4, 98.8);
            doc = Fx.Doc(rail, a, b);
            ctx = Fx.Ctx(doc);
            res = Snapper.SnapMove(Reg.PageAnchors(b, ctx).ToList(), Vec2.Zero, Snapper.Targets(Reg, doc, doc.Pages[0], new[] { b.Id }, ctx, opts), opts);
            Assert.Equal("mount", res.Kind);
            var bx = b.Transform.X + res.Delta.X;
            Assert.Equal(a.Transform.X + a.Size.W / 2, bx - b.Size.W / 2, 6);
            Assert.Equal(100, b.Transform.Y + res.Delta.Y, 6);
        }

        [Fact]
        public void RenumberNumbersTheStripFromLeftToRight()
        {
            var t1 = Fx.Item("sw.terminal.feedthrough", 120, 100).WithParam("ref", "-X1");
            var t2 = Fx.Item("sw.terminal.feedthrough", 109.6, 100).WithParam("ref", "-X1");
            var t3 = Fx.Item("sw.terminal.pe", 114.8, 100).WithParam("ref", "-X1");
            var other = Fx.Item("sw.terminal.feedthrough", 140, 100).WithParam("ref", "-X2");
            var doc = Fx.Doc(t1, t2, t3, other);
            var action = Reg.Type(t1.Type)!.Actions(t1, Fx.Ctx(doc)).Single(a => a.Id == "sw.terminal.renumber");
            doc = action.Run(doc, t1, Fx.Ctx(doc));
            Assert.Equal("1", doc.Get(t2.Id)!.Str("number", ""));
            Assert.Equal("2", doc.Get(t3.Id)!.Str("number", ""));
            Assert.Equal("3", doc.Get(t1.Id)!.Str("number", ""));
            Assert.Equal("", doc.Get(other.Id)!.Str("number", ""));
        }

        [Fact]
        public void LabellingStripListsTheDevicesBelowIt()
        {
            var rail = Fx.Item("sw.rail.ts35", 200, 100);
            var rcd = Fx.Item("sw.protection.rcd", 200 - 70, 100).WithParam("ref", "-F1");
            var mcb = Fx.Item("sw.protection.mcb", 200 - 70 + 17.5 + 8.75, 100).WithParam("ref", "-F2").WithParam("circuit", "Kitchen");
            var strip = Fx.Item("sw.label.strip", 200, 100 - 45 - 8);
            var doc = Fx.Doc(rail, rcd, mcb, strip);
            var texts = Reg.Render(doc.Get(strip.Id)!, Fx.Ctx(doc)).OfType<TextPrim>().Select(t => t.Text).ToList();
            Assert.Contains("-F1", texts);
            Assert.Contains("-F2", texts);
            Assert.Contains("Kitchen", texts);
            Assert.Contains(texts, t => t.StartsWith("40 A", StringComparison.Ordinal));
            // A device names the strip as dependent, so the strip is redrawn when the device changes.
            Assert.Contains(strip.Id, Reg.Type(mcb.Type)!.Dependents(mcb, Fx.Ctx(doc)));
        }

        [Fact]
        public void FrontViewsShowRatingsAndDesignations()
        {
            string Texts(SketchObject o) => string.Join("|", Fx.Local(o).OfType<TextPrim>().Select(t => t.Text));
            Assert.Contains("B16", Texts(Fx.Item("sw.protection.mcb")));
            Assert.Contains("C16", Texts(Fx.Item("sw.protection.mcb.c16.3p")));
            var rcd = Texts(Fx.Item("sw.protection.rcd").WithParam("ref", "-F1"));
            Assert.Contains("30 mA", rcd);
            Assert.Contains("T", rcd);
            Assert.Contains("-F1", rcd);
            Assert.Contains("63 A", Texts(Fx.Item("sw.switch.main")));
            Assert.Contains("KNX", Texts(Fx.Item("sw.bus.actuator")));
        }

        [Fact]
        public void LeverFollowsTheSwitchState()
        {
            var mcb = Fx.Item("sw.protection.mcb");
            var on = Fx.Local(mcb).OfType<RectPrim>().Where(r => r.Tag == SwTags.Lever).Min(r => r.Y);
            var toggle = Reg.Type(mcb.Type)!.Actions(mcb, Lang.En).Single(a => a.Id == "sw.on");
            var off = toggle.Apply(mcb);
            Assert.False(off.Flag("on", true));
            var handleOn = Fx.Local(mcb).OfType<RectPrim>().Where(r => r.Tag == SwTags.Lever && r.Style.Fill?.Color == Sx.Lever).Select(r => r.Y).Min();
            var handleOff = Fx.Local(off).OfType<RectPrim>().Where(r => r.Tag == SwTags.Lever && r.Style.Fill?.Color == Sx.Lever).Select(r => r.Y).Min();
            Assert.True(handleOff > handleOn);
            Assert.True(on <= handleOn);
        }

        [Fact]
        public void ChoicesAreDropdownsWithOneCurrentOption()
        {
            foreach (var item in Reg.Library)
            {
                var o = Reg.Create(item, new Vec2(100, 100));
                var actions = Reg.Type(o.Type)!.Actions(o, Fx.Ctx(Fx.Doc(o))).ToList();
                Assert.DoesNotContain(actions, a => a.Icon == IconIds.Cycle);
                Assert.All(actions, a => Assert.Contains(a.Icon, IconIds.All));
                foreach (var (choice, options) in ActionChoices.Group(actions))
                {
                    Assert.True(options.Count >= 2, item.Id + " " + choice.Key);
                    Assert.Equal(1, options.Count(a => a.IsActive));
                    Assert.All(options, a => Assert.NotNull(a.ParamKeys));
                }
            }
        }

        [Fact]
        public void DocumentsRoundTripThroughJson()
        {
            var objects = Reg.Library.Select((item, i) => Reg.Create(item, new Vec2(20 + i % 10 * 40, 20 + i / 10 * 40))).ToArray();
            objects[0] = objects[0].WithParam("ref", "-F1").WithParam("circuit", "Küche").WithParam("on", false);
            var doc = Fx.Doc(objects);
            var json = DocumentJson.Serialize(doc);
            var back = DocumentJson.Deserialize(json);
            Assert.Equal(json, DocumentJson.Serialize(back));
            Assert.Equal("Küche", back.Get(objects[0].Id)!.Str("circuit", ""));
            var compact = DocumentJson.FromCompact(DocumentJson.ToCompact(doc));
            Assert.Equal(json, DocumentJson.Serialize(compact));
        }

        [Fact]
        public void WireAndCableEndsDockOnPins()
        {
            var mcb = Fx.Item("sw.protection.mcb", 100, 100);
            var term = Fx.Item("sw.terminal.feedthrough", 100, 200);
            var doc = Fx.Doc(mcb, term);
            var wire = Fx.Line("sw.line.wire.bu", doc, mcb, "2", term, "1");
            doc = Fx.Doc(mcb, term, wire);
            var pts = doc.ResolvePoints(doc.Get(wire.Id)!);
            Assert.Equal(new Vec2(100, 145), pts[0]);
            Assert.Equal(new Vec2(100, 172), pts[1]);
            // Moving the terminal: the wire end follows.
            var moved = doc.Get(term.Id)! with { Transform = term.Transform with { X = 130 } };
            doc = Edits.Replace(doc, moved);
            Assert.Equal(130, doc.ResolvePoints(doc.Get(wire.Id)!)[1].X, 6);
        }
    }
}
