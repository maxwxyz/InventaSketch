using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Electrical.Tests
{
    internal static class Fx
    {
        public static Registry Reg() => new Registry().Register(new ElectricalModule());

        public static RenderContext Ctx() => new(SketchDocument.Create());

        /// <summary>Box types without electrical connection points (outlines, areas, text).</summary>
        public static readonly string[] Unconnected = { "elec.pcb.board", "elec.pcb.copper", "elec.pcb.hole", "elec.pcb.silk" };

        public static IEnumerable<(LibraryItem Item, ObjectType Type, string View)> BoxItemViews(Registry r)
        {
            foreach (var item in r.Library)
            {
                var t = r.Type(item.TypeId)!;
                if (t.Geometry != GeometryKind.Box) continue;
                foreach (var v in t.Views) yield return (item, t, v.Id);
            }
        }

        public static void AssertInsideBox(IReadOnlyList<Primitive> prims, Size2 size, string what)
        {
            Assert.True(prims.Count > 0, what + ": no primitives");
            var r = Prim.Bounds(prims)!.Value;
            const double tol = 0.02;
            Assert.True(r.X >= -size.W / 2 - tol && r.Right <= size.W / 2 + tol && r.Y >= -size.H / 2 - tol && r.Bottom <= size.H / 2 + tol,
                $"{what}: bounds {r} exceed box {size.W}x{size.H}");
            AssertFinite(prims, what);
        }

        public static void AssertFinite(IReadOnlyList<Primitive> prims, string what)
        {
            foreach (var pt in Prim.Flatten(prims).SelectMany(Prim.Points))
                Assert.False(double.IsNaN(pt.X) || double.IsNaN(pt.Y) || double.IsInfinity(pt.X) || double.IsInfinity(pt.Y), what + ": NaN");
        }

        public static (SketchDocument Doc, string PageId) Doc(params SketchObject[] objects)
        {
            var doc = SketchDocument.Create();
            return (Edits.Add(doc, doc.Pages[0].Id, objects), doc.Pages[0].Id);
        }

        public static Anchor Port(Registry r, SketchDocument doc, SketchObject o, string id)
            => r.PageAnchors(o, new RenderContext(doc)).Single(a => a.Role == AnchorRole.Port && a.Id == id);

        public static IReadOnlyList<Primitive> Render(Registry r, SketchObject o) => r.TypeOf(o).Render(o, Ctx());
    }

    public class ModuleRegistrationTests
    {
        [Fact]
        public void ModuleRegistersWithAllTypes()
        {
            var ids = Fx.Reg().Types.Select(t => t.Id).ToList();
            foreach (var id in new[]
            {
                "elec.resistor", "elec.potentiometer", "elec.capacitor", "elec.capacitor.polarized", "elec.inductor", "elec.transformer",
                "elec.diode", "elec.diode.led", "elec.diode.zener", "elec.diode.photo", "elec.transistor.bjt", "elec.transistor.mosfet",
                "elec.transistor.igbt", "elec.thyristor", "elec.opamp", "elec.gate", "elec.flipflop", "elec.block.ecu",
                "elec.switch", "elec.pushbutton", "elec.relay.coil", "elec.contactor", "elec.fuse", "elec.breaker", "elec.rcd",
                "elec.ground", "elec.source", "elec.battery", "elec.lamp", "elec.motor", "elec.generator", "elec.sensor", "elec.actuator",
                "elec.speaker", "elec.microphone", "elec.antenna", "elec.connector", "elec.terminal", "elec.testpoint", "elec.bus",
                "elec.wire", "elec.junction", "elec.netlabel",
                "elec.house.socket", "elec.house.switch", "elec.house.light", "elec.house.junction", "elec.house.board", "elec.house.cable",
                "elec.sld.busbar", "elec.sld.feeder", "elec.sld.meter",
                "elec.pe.inverter", "elec.pe.rectifier", "elec.pe.dcdc", "elec.pe.dclink", "elec.pe.gatedriver", "elec.pe.module",
                "elec.pcb.board", "elec.pcb.pad", "elec.pcb.via", "elec.pcb.track", "elec.pcb.copper", "elec.pcb.footprint", "elec.pcb.hole", "elec.pcb.silk",
            })
                Assert.Contains(id, ids);
            Assert.All(ids, id => Assert.StartsWith("elec.", id));
        }

        [Fact]
        public void EverythingIsBilingualCategorisedAndHasViewIcons()
        {
            var r = Fx.Reg();
            var cats = new HashSet<string>(r.Categories.Select(c => c.Id));
            Assert.Equal(9, cats.Count);
            foreach (var t in r.Types)
            {
                Assert.False(string.IsNullOrEmpty(t.Name.De), t.Id + " has no German name");
                Assert.Contains(t.Category, cats);
                Assert.NotEmpty(t.Keywords);
                Assert.NotEmpty(t.Views);
                foreach (var v in t.Views)
                {
                    Assert.False(string.IsNullOrEmpty(v.Label.De), t.Id + "/" + v.Id);
                    Assert.StartsWith("view-", v.Icon);
                }
                foreach (var p in t.Parameters) Assert.False(string.IsNullOrEmpty(p.Label.De), t.Id + "/" + p.Key);
            }
            foreach (var item in r.Library)
            {
                Assert.False(string.IsNullOrEmpty(item.Name.De), item.Id);
                Assert.Contains(item.Category, cats);
            }
            foreach (var c in r.Categories) Assert.Contains(r.Library, i => i.Category == c.Id);
            foreach (var t in r.Types) Assert.Contains(r.Library, i => i.TypeId == t.Id);
            Assert.Equal(r.Library.Count, r.Library.Select(i => i.Id).Distinct().Count());
        }

        [Fact]
        public void HouseItemsUsePlanViewAndSymbolsUseIecSymbolView()
        {
            var r = Fx.Reg();
            foreach (var id in new[] { "elec.house.socket", "elec.house.switch", "elec.house.light", "elec.house.junction", "elec.house.board", "elec.house.cable" })
                Assert.Equal(IconIds.Plan, r.Type(id)!.Views[0].Icon);
            Assert.Equal(new[] { IconIds.Symbol, IconIds.Simplified }, r.Type("elec.gate")!.Views.Select(v => v.Icon));
            Assert.Equal(new[] { IconIds.Symbol, IconIds.Simplified }, r.Type("elec.resistor")!.Views.Select(v => v.Icon));
        }

        [Theory]
        [InlineData("widerstand")]
        [InlineData("resistor")]
        [InlineData("schütz")]
        [InlineData("steckdose")]
        [InlineData("wechselrichter")]
        [InlineData("leiterbahn")]
        [InlineData("can")]
        [InlineData("nand ansi")]
        [InlineData("masse")]
        public void SearchFindsEnglishAndGerman(string q) => Assert.NotEmpty(Fx.Reg().Search(q));

        [Fact]
        public void ParametersAreOptional()
        {
            var r = Fx.Reg();
            foreach (var t in r.Types)
            {
                var o = t.Geometry == GeometryKind.Points
                    ? new SketchObject { Id = "x", Type = t.Id, Points = t.DefaultPoints }
                    : new SketchObject { Id = "x", Type = t.Id, Size = t.DefaultSize };
                Assert.NotEmpty(t.Render(o, Fx.Ctx()));
            }
        }
    }

    public class RenderTests
    {
        [Fact]
        public void EveryLibraryItemRendersInEveryViewInsideTheBox()
        {
            var r = Fx.Reg();
            var count = 0;
            foreach (var (item, type, view) in Fx.BoxItemViews(r))
            {
                var o = r.Create(item, Vec2.Zero) with { View = view };
                Fx.AssertInsideBox(type.Render(o, Fx.Ctx()), o.Size, $"{item.Id}/{view}");
                count++;
            }
            Assert.True(count > 100, "expected many item/view combinations, got " + count);
        }

        [Theory]
        [InlineData(2.5, 0.6)]
        [InlineData(0.4, 1.8)]
        [InlineData(3, 3)]
        [InlineData(0.3, 0.3)]
        public void FreeScalingKeepsPrimitivesInsideTheBox(double sx, double sy)
        {
            var r = Fx.Reg();
            foreach (var (item, type, view) in Fx.BoxItemViews(r))
            {
                var o = r.Create(item, Vec2.Zero) with { View = view };
                o = o with { Size = new Size2(o.Size.W * sx, o.Size.H * sy) };
                Fx.AssertInsideBox(type.Render(o, Fx.Ctx()), o.Size, $"{item.Id}/{view} scaled {sx}x{sy}");
            }
        }

        [Fact]
        public void AllParameterChoicesRenderInsideTheBox()
        {
            var r = Fx.Reg();
            foreach (var t in r.Types.Where(t => t.Geometry == GeometryKind.Box))
            {
                var baseObj = t.Create(Vec2.Zero);
                foreach (var p in t.Parameters)
                {
                    IEnumerable<ParamValue> values = p.Kind switch
                    {
                        ParamKind.Choice => p.Options!.Select(o => (ParamValue)o.Value),
                        ParamKind.Boolean => new ParamValue[] { true, false },
                        ParamKind.Integer => new ParamValue[] { p.Min ?? 0, p.Max ?? 1 },
                        _ => Array.Empty<ParamValue>(),
                    };
                    foreach (var v in values)
                        foreach (var view in t.Views)
                        {
                            var o = baseObj.WithParam(p.Key, v) with { View = view.Id };
                            Fx.AssertInsideBox(t.Render(o, Fx.Ctx()), o.Size, $"{t.Id}/{view.Id} {p.Key}={v}");
                        }
                }
            }
        }

        [Fact]
        public void PointTypesRenderAlongTheirPoints()
        {
            var r = Fx.Reg();
            foreach (var item in r.Library.Where(i => r.Type(i.TypeId)!.Geometry == GeometryKind.Points))
            {
                var o = r.Create(item, new Vec2(50, 50));
                var prims = Fx.Render(r, o);
                Assert.NotEmpty(prims);
                Fx.AssertFinite(prims, item.Id);
                var b = Prim.Bounds(prims)!.Value;
                Assert.True(b.Inflate(0.01).Contains(o.Points![0]) && b.Inflate(0.01).Contains(o.Points[o.Points.Count - 1]), item.Id);
            }
        }

        [Fact]
        public void IecAndAnsiGateViewsDiffer()
        {
            var r = Fx.Reg();
            var o = r.Create(r.Item("elec.gate.nand")!, Vec2.Zero);
            var iec = Fx.Render(r, o);
            var ansi = Fx.Render(r, o with { View = "ansi" });
            Assert.Contains(iec, p => p is TextPrim t && t.Text == "&");
            Assert.Contains(iec, p => p.Tag == "negation");
            Assert.DoesNotContain(ansi, p => p is TextPrim);
            Assert.Contains(ansi, p => p.Tag == "negation");
            Assert.Contains(Fx.Render(r, o.WithParam("gate", "or")), p => p is TextPrim t && t.Text == "≥1");
        }

        [Fact]
        public void GateCycleActionChangesFunction()
        {
            var r = Fx.Reg();
            var t = r.Type("elec.gate")!;
            var o = r.Create(r.Item("elec.gate.and")!, Vec2.Zero);
            var next = t.Actions(o, Lang.En).Single(a => a.Id == "elec.gate.next");
            Assert.Equal(IconIds.Cycle, next.Icon);
            Assert.Equal("or", next.Apply(o).Str("gate", ""));
        }

        [Fact]
        public void LabelsShowDesignatorAndValueUprightOnThePage()
        {
            var r = Fx.Reg();
            var o = r.Create(r.Item("elec.resistor")!, new Vec2(50, 50)).WithParam("ref", "R1").WithParam("value", "10k");
            var local = Fx.Render(r, o);
            var labels = local.OfType<TextPrim>().Where(p => p.Tag == "label").ToList();
            Assert.Equal(new[] { "R1", "10k" }, labels.Select(l => l.Text));
            Assert.True(labels[0].Position.Y < -o.Size.H / 2 && labels[1].Position.Y > o.Size.H / 2);

            // Rotated by 90 degrees: labels stay upright (rotation 0 on the page) and sit beside the vertical symbol.
            var rotated = o with { Transform = o.Transform with { Rotation = 90 } };
            var page = r.Render(rotated, Fx.Ctx()).OfType<TextPrim>().Where(p => p.Tag == "label").ToList();
            Assert.All(page, l => Assert.Equal(0, l.Rotation, 6));
            Assert.Contains(page, l => l.TextStyle.Anchor != TextAnchor.Middle);

            Assert.DoesNotContain(Fx.Render(r, o.WithParam("showLabel", false)), p => p.Tag == "label");
        }

        [Fact]
        public void LabelActionAddsDesignator()
        {
            var r = Fx.Reg();
            var t = r.Type("elec.capacitor")!;
            var o = r.Create(r.Item("elec.capacitor")!, Vec2.Zero);
            var action = t.Actions(o, Lang.De).Single(a => a.Id == "elec.label");
            Assert.Equal(IconIds.Text, action.Icon);
            Assert.False(action.IsActive);
            var shown = action.Apply(o);
            Assert.Equal("C1", shown.Str("ref", ""));
            Assert.Contains(Fx.Render(r, shown), p => p is TextPrim { Text: "C1" });
            var hidden = t.Actions(shown, Lang.De).Single(a => a.Id == "elec.label").Apply(shown);
            Assert.DoesNotContain(Fx.Render(r, hidden), p => p.Tag == "label");
        }

        [Fact]
        public void ViewActionsCarryViewIcons()
        {
            var r = Fx.Reg();
            var t = r.Type("elec.resistor")!;
            var o = r.Create(r.Item("elec.resistor")!, Vec2.Zero);
            var views = t.Actions(o, Lang.En).Where(a => a.Group == "view").ToList();
            Assert.Equal(new[] { IconIds.Symbol, IconIds.Simplified }, views.Select(a => a.Icon));
            Assert.True(views[0].IsActive);
            Assert.Equal("ansi", views[1].Apply(o).View);
        }
    }

    public class PinTests
    {
        [Fact]
        public void EverySymbolHasPortsOnOrInsideItsBox()
        {
            var r = Fx.Reg();
            foreach (var item in r.Library)
            {
                var t = r.Type(item.TypeId)!;
                if (t.Geometry != GeometryKind.Box || Fx.Unconnected.Contains(t.Id)) continue;
                var o = r.Create(item, Vec2.Zero);
                var ports = t.Anchors(o, Fx.Ctx()).Where(a => a.Role == AnchorRole.Port).ToList();
                Assert.True(ports.Count >= 1, item.Id + " has no port");
                Assert.Equal(ports.Count, ports.Select(p => p.Id).Distinct().Count());
                foreach (var p in ports)
                    Assert.True(Math.Abs(p.P.X) <= o.Size.W / 2 + 1e-9 && Math.Abs(p.P.Y) <= o.Size.H / 2 + 1e-9, $"{item.Id}: port {p.Id} outside the box");
            }
            foreach (var id in Fx.Unconnected)
                Assert.Empty(r.Type(id)!.Anchors(r.Type(id)!.Create(Vec2.Zero), Fx.Ctx()));
        }

        [Fact]
        public void SchematicPinsAreGridFriendly()
        {
            // At the default size and a grid-aligned insertion point, schematic pins land on a 2.5 mm grid.
            var r = Fx.Reg();
            foreach (var item in r.Library.Where(i => i.Category != "elec.pcb"))
            {
                var t = r.Type(item.TypeId)!;
                if (t.Geometry != GeometryKind.Box) continue;
                var o = r.Create(item, Vec2.Zero);
                foreach (var p in t.Anchors(o, Fx.Ctx()).Where(a => a.Role == AnchorRole.Port))
                {
                    Assert.True(Math.Abs(p.P.X / 2.5 - Math.Round(p.P.X / 2.5)) < 1e-6, $"{item.Id}: {p.Id} x={p.P.X}");
                    Assert.True(Math.Abs(p.P.Y / 2.5 - Math.Round(p.P.Y / 2.5)) < 1e-6, $"{item.Id}: {p.Id} y={p.P.Y}");
                }
            }
        }

        [Fact]
        public void TwoTerminalPinsSitOnTheEdgeMidpoints()
        {
            var r = Fx.Reg();
            var o = r.Create(r.Item("elec.resistor")!, new Vec2(50, 50));
            var (doc, _) = Fx.Doc(o);
            Assert.Equal(new Vec2(40, 50), Fx.Port(r, doc, o, "1").P);
            Assert.Equal(new Vec2(60, 50), Fx.Port(r, doc, o, "2").P);
        }

        [Fact]
        public void PolarityFlipMirrorsPinsAndDrawing()
        {
            var r = Fx.Reg();
            var t = r.Type("elec.diode")!;
            var o = r.Create(r.Item("elec.diode")!, Vec2.Zero);
            var anode = t.Anchors(o, Fx.Ctx()).Single(a => a.Id == "A").P;
            var flip = t.Actions(o, Lang.En).Single(a => a.Id == "elec.polarity");
            Assert.Equal(IconIds.Swap, flip.Icon);
            var reversed = flip.Apply(o);
            Assert.True(reversed.Flag("reverse", false));
            Assert.Equal(-anode.X, t.Anchors(reversed, Fx.Ctx()).Single(a => a.Id == "A").P.X, 9);
            var bar = (LinePrim)Fx.Render(r, o).First(p => p.Tag == "bar");
            var bar2 = (LinePrim)Fx.Render(r, reversed).First(p => p.Tag == "bar");
            Assert.Equal(-bar.A.X, bar2.A.X, 9);
        }

        [Fact]
        public void GateInputCountChangesPins()
        {
            var r = Fx.Reg();
            var t = r.Type("elec.gate")!;
            var o = r.Create(r.Item("elec.gate.and")!, Vec2.Zero);
            Assert.Equal(3, t.Anchors(o, Fx.Ctx()).Count());
            Assert.Equal(4, t.Anchors(o.WithParam("inputs", 3), Fx.Ctx()).Count());
            Assert.Equal(2, t.Anchors(o.WithParam("gate", "not"), Fx.Ctx()).Count());
        }

        [Fact]
        public void ThreePhaseMotorExposesUvwPins()
        {
            var r = Fx.Reg();
            var o = r.Create(r.Item("elec.motor.3ph")!, Vec2.Zero);
            var ids = r.TypeOf(o).Anchors(o, Fx.Ctx()).Select(a => a.Id).ToList();
            Assert.Equal(new[] { "U", "V", "W" }, ids);
        }
    }

    public class SwitchTests
    {
        [Fact]
        public void ToggleChangesRendering()
        {
            var r = Fx.Reg();
            var t = r.Type("elec.switch")!;
            var o = r.Create(r.Item("elec.switch.no")!, Vec2.Zero);
            var toggle = t.Actions(o, Lang.En).Single(a => a.Id == "elec.toggle");
            Assert.Equal(IconIds.Toggle, toggle.Icon);
            Assert.False(toggle.IsActive);
            var closed = toggle.Apply(o);
            Assert.True(closed.Flag("closed", false));
            var openBlade = (LinePrim)Fx.Render(r, o).Single(p => p.Tag == "blade");
            var closedBlade = (LinePrim)Fx.Render(r, closed).Single(p => p.Tag == "blade");
            Assert.NotEqual(openBlade.B, closedBlade.B);
            // The closed blade ends at the fixed contact (lower, nearly horizontal).
            Assert.True(Math.Abs(closedBlade.B.Y) < Math.Abs(openBlade.B.Y));
            Assert.True(t.Actions(closed, Lang.En).Single(a => a.Id == "elec.toggle").IsActive);
        }

        [Fact]
        public void NormallyClosedContactIsDrawnClosedByDefault()
        {
            var r = Fx.Reg();
            var t = r.Type("elec.switch")!;
            var nc = r.Create(r.Item("elec.switch.nc")!, Vec2.Zero);
            Assert.True(t.Actions(nc, Lang.En).Single(a => a.Id == "elec.toggle").IsActive);
            var opened = t.Actions(nc, Lang.En).Single(a => a.Id == "elec.toggle").Apply(nc);
            Assert.False(opened.Flag("closed", true));
            Assert.NotEqual(((LinePrim)Fx.Render(r, nc).Single(p => p.Tag == "blade")).B, ((LinePrim)Fx.Render(r, opened).Single(p => p.Tag == "blade")).B);
        }

        [Theory]
        [InlineData("elec.contactor")]
        [InlineData("elec.breaker")]
        [InlineData("elec.rcd")]
        [InlineData("elec.pushbutton")]
        public void SwitchingDevicesToggle(string itemId)
        {
            var r = Fx.Reg();
            var o = r.Create(r.Item(itemId)!, Vec2.Zero);
            var t = r.TypeOf(o);
            var closed = t.Actions(o, Lang.En).Single(a => a.Id == "elec.toggle").Apply(o);
            var a = Fx.Render(r, o).OfType<LinePrim>().Where(p => p.Tag == "blade").Select(p => p.B).ToList();
            var b = Fx.Render(r, closed).OfType<LinePrim>().Where(p => p.Tag == "blade").Select(p => p.B).ToList();
            Assert.NotEmpty(a);
            Assert.NotEqual(a, b);
        }

        [Fact]
        public void ChangeoverHasThreePins()
        {
            var r = Fx.Reg();
            var o = r.Create(r.Item("elec.switch.changeover")!, Vec2.Zero);
            Assert.Equal(new[] { "com", "no", "nc" }, r.TypeOf(o).Anchors(o, Fx.Ctx()).Select(a => a.Id));
        }
    }

    public class WireTests
    {
        private static readonly SnapOptions Opt = new() { Tolerance = 3, Grid = null, Alignment = false, Page = false };

        [Fact]
        public void WireEndpointSnapsToResistorPin()
        {
            var r = Fx.Reg();
            var res = r.Create(r.Item("elec.resistor")!, new Vec2(50, 50)) with { Transform = new Transform(50, 50, 30) };
            var wire = r.Create(r.Item("elec.wire")!, new Vec2(100, 100));
            var (doc, pageId) = Fx.Doc(res, wire);
            var ctx = new RenderContext(doc);
            var pin = Fx.Port(r, doc, res, "2").P;
            var targets = Snapper.Targets(r, doc, doc.Pages[0], new[] { wire.Id }, ctx, Opt);
            var drag = pin + new Vec2(1.2, -0.9);
            var res1 = Snapper.SnapPoint(drag, targets, Opt);
            Assert.Equal("port", res1.Kind); // a free end docks on pins with port priority (was a generic "point" snap)
            Assert.Equal(pin.X, (drag + res1.Delta).X, 6);
            Assert.Equal(pin.Y, (drag + res1.Delta).Y, 6);
        }

        [Fact]
        public void JunctionDotSnapsAsPortOntoWireEnd()
        {
            var r = Fx.Reg();
            var wire = r.Create(r.Item("elec.wire")!, new Vec2(100, 100));
            var dot = r.Create(r.Item("elec.junction")!, new Vec2(85.3, 100.2));
            var (doc, _) = Fx.Doc(wire, dot);
            var ctx = new RenderContext(doc);
            var moving = r.PageAnchors(dot, ctx).ToList();
            var targets = Snapper.Targets(r, doc, doc.Pages[0], new[] { dot.Id }, ctx, Opt);
            var res = Snapper.SnapMove(moving, Vec2.Zero, targets, Opt);
            Assert.Equal("port", res.Kind);
            var at = new Vec2(85.3, 100.2) + res.Delta;
            Assert.Equal(85, at.X, 6);
            Assert.Equal(100, at.Y, 6);
        }

        [Fact]
        public void ConnectedWireFollowsTheSymbol()
        {
            var r = Fx.Reg();
            var res = r.Create(r.Item("elec.resistor")!, new Vec2(50, 50));
            var wire = r.Create(r.Item("elec.wire")!, new Vec2(85, 70));
            var (doc, _) = Fx.Doc(res, wire);
            // Wire start (70, 70) is too far from any pin; move it next to pin 2 at (60, 50) and connect.
            doc = Edits.SetPoint(doc, wire.Id, 0, new Vec2(61, 51));
            Assert.Same(doc, ElectricalModule.ConnectToPin(r, doc, wire.Id, 1));
            doc = ElectricalModule.ConnectToPin(r, doc, wire.Id, 0);
            var w = doc.Get(wire.Id)!;
            Assert.Equal(res.Id, w.Attach![0]!.ObjectId);
            Assert.Equal(new Vec2(60, 50), doc.ResolvePoints(w)[0]);

            doc = Edits.Move(doc, new[] { res.Id }, new Vec2(10, -5));
            Assert.Equal(new Vec2(70, 45), doc.ResolvePoints(doc.Get(wire.Id)!)[0]);
            doc = Edits.Rotate(doc, new[] { res.Id }, new Vec2(60, 45), 90);
            Assert.Equal(60, doc.ResolvePoints(doc.Get(wire.Id)!)[0].X, 6);
            Assert.Equal(55, doc.ResolvePoints(doc.Get(wire.Id)!)[0].Y, 6);
        }

        [Fact]
        public void OrthogonalRoutingIsAxisAligned()
        {
            var r = Fx.Reg();
            var wire = r.Create(r.Item("elec.wire")!, Vec2.Zero) with { Points = new[] { new Vec2(0, 0), new Vec2(20, 15), new Vec2(35, -5) } };
            var path = (PathPrim)Fx.Render(r, wire).Single();
            var pts = path.Segments.Select(s => s.P).ToList();
            Assert.True(pts.Count > 3);
            for (var i = 0; i + 1 < pts.Count; i++)
                Assert.True(Math.Abs(pts[i].X - pts[i + 1].X) < 1e-9 || Math.Abs(pts[i].Y - pts[i + 1].Y) < 1e-9);
            Assert.Equal(new Vec2(35, -5), pts[pts.Count - 1]);
        }

        [Fact]
        public void OrthogonalWireLeavesPinsPerpendicularToRotatedSymbols()
        {
            var r = Fx.Reg();
            var res = r.Create(r.Item("elec.resistor")!, new Vec2(110, 40));
            var led = r.Create(r.Item("elec.led")!, new Vec2(140, 60)) with { Transform = new Transform(140, 60, 90) };
            var wire = r.Create(r.Item("elec.wire")!, Vec2.Zero) with { Points = new[] { new Vec2(120, 40), new Vec2(140, 50) } };
            var (doc, _) = Fx.Doc(res, led, wire);
            doc = ElectricalModule.ConnectToPin(r, doc, wire.Id, 0);
            doc = ElectricalModule.ConnectToPin(r, doc, wire.Id, 1);
            var w = doc.Get(wire.Id)!;
            var pts = ((PathPrim)r.TypeOf(w).Render(w, new RenderContext(doc)).Single()).Segments.Select(s => s.P).ToList();
            // Horizontal out of the resistor, vertical into the anode of the LED rotated by 90 degrees.
            Assert.Equal(new[] { new Vec2(120, 40), new Vec2(140, 40), new Vec2(140, 50) }, pts);
        }

        [Fact]
        public void FortyFiveDegreeRoutingUsesDiagonals()
        {
            var r = Fx.Reg();
            var wire = r.Create(r.Item("elec.wire")!, Vec2.Zero).WithParam("route", "45") with { Points = new[] { new Vec2(0, 0), new Vec2(30, 10) } };
            var pts = ((PathPrim)Fx.Render(r, wire).Single()).Segments.Select(s => s.P).ToList();
            Assert.Equal(new[] { new Vec2(0, 0), new Vec2(20, 0), new Vec2(30, 10) }, pts);
            var free = wire.WithParam("route", "free");
            Assert.Equal(2, ((PathPrim)Fx.Render(r, free).Single()).Segments.Count);
            var cycle = r.TypeOf(wire).Actions(wire, Lang.En).Single(a => a.Id == "elec.route.next");
            Assert.Equal("orthogonal", cycle.Apply(free).Str("route", ""));
        }

        [Fact]
        public void BusHasLabelAndTapPorts()
        {
            var r = Fx.Reg();
            var bus = r.Create(r.Item("elec.bus.lin")!, new Vec2(100, 50));
            var prims = Fx.Render(r, bus);
            Assert.Contains(prims, p => p is TextPrim { Text: "LIN" });
            var (doc, _) = Fx.Doc(bus);
            var taps = r.TypeOf(bus).Anchors(bus, new RenderContext(doc)).Where(a => a.Role == AnchorRole.Port).ToList();
            Assert.Equal(7, taps.Count);
        }
    }

    public class PersistenceTests
    {
        public static (Registry R, SketchDocument Doc) Circuit()
        {
            var r = Fx.Reg();
            var bat = r.Create(r.Item("elec.battery.cell")!, new Vec2(40, 60)).WithParam("ref", "G1").WithParam("value", "12 V") with { Transform = new Transform(40, 60, 90) };
            var sw = r.Create(r.Item("elec.switch.no")!, new Vec2(70, 40)).WithParam("ref", "S1");
            var res = r.Create(r.Item("elec.resistor")!, new Vec2(110, 40)).WithParam("ref", "R1").WithParam("value", "470");
            var led = r.Create(r.Item("elec.led")!, new Vec2(140, 60)).WithParam("ref", "D1") with { Transform = new Transform(140, 60, 90) };
            var gnd = r.Create(r.Item("elec.ground")!, new Vec2(90, 95));
            var (doc, pageId) = Fx.Doc(bat, sw, res, led, gnd);
            SketchObject Wire(Vec2 a, Vec2 b) => r.Create(r.Item("elec.wire")!, Vec2.Zero) with { Points = new[] { a, b } };
            var wires = new[]
            {
                Wire(new Vec2(40, 50), new Vec2(60, 40)),
                Wire(new Vec2(80, 40), new Vec2(100, 40)),
                Wire(new Vec2(120, 40), new Vec2(140, 50)),
                Wire(new Vec2(140, 70), new Vec2(90, 90)),
                Wire(new Vec2(40, 70), new Vec2(90, 90)),
            };
            doc = Edits.Add(doc, pageId, wires);
            foreach (var w in wires)
            {
                doc = ElectricalModule.ConnectToPin(r, doc, w.Id, 0);
                doc = ElectricalModule.ConnectToPin(r, doc, w.Id, 1);
            }
            var dot = r.Create(r.Item("elec.junction")!, new Vec2(90, 80));
            doc = Edits.Add(doc, pageId, dot);
            return (r, doc);
        }

        [Fact]
        public void CircuitWiresAreAttachedToPins()
        {
            var (_, doc) = Circuit();
            var wires = doc.Objects.Values.Where(o => o.Type == "elec.wire").ToList();
            Assert.Equal(5, wires.Count);
            Assert.Equal(10, wires.Sum(w => w.Attach?.Count(a => a is not null) ?? 0));
        }

        [Fact]
        public void JsonRoundTrip()
        {
            var (r, doc) = Circuit();
            var json = DocumentJson.Serialize(doc, indented: true);
            var back = DocumentJson.Deserialize(json);
            Assert.Equal(DocumentJson.Serialize(doc), DocumentJson.Serialize(back));
            Assert.Equal(DocumentJson.Serialize(doc), DocumentJson.Serialize(DocumentJson.FromCompact(DocumentJson.ToCompact(doc))));
            Assert.Equal(SvgRenderer.RenderPage(r, doc, doc.Pages[0]), SvgRenderer.RenderPage(r, back, back.Pages[0]));
            var sw = back.Objects.Values.Single(o => o.Type == "elec.switch");
            Assert.Equal("no", sw.Str("contact", ""));
            Assert.Equal("S1", sw.Str("ref", ""));
        }
    }
}
