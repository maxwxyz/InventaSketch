using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Diagrams.Tests
{
    public class ControlLoopTests
    {
        private static Vec2 Port(Registry r, SketchDocument doc, string id, string port)
            => r.PageAnchors(doc.Get(id)!, new RenderContext(doc)).Single(a => a.Role == AnchorRole.Port && a.Id == port).P;

        [Fact]
        public void SummingJunctionHasACircleWithPortsAndSignsNextToTheInputs()
        {
            var r = T.Reg();
            var (doc, o) = T.Insert(r, "diag.ctrl.sum", new Vec2(100, 100));
            var prims = T.Local(r, doc, o.Id);
            var circle = Assert.Single(T.All<EllipsePrim>(prims), e => e.Tag == DiagTags.Outline);
            Assert.Equal(3, circle.Rx, 9); // Ø 6 mm at the default size
            Assert.Equal(new Vec2(97, 100), Port(r, doc, o.Id, "left"));
            Assert.Equal(new Vec2(100, 103), Port(r, doc, o.Id, "bottom"));
            Assert.Equal(new Vec2(103, 100), Port(r, doc, o.Id, "right"));
            Assert.Equal(new Vec2(100, 97), Port(r, doc, o.Id, "top"));
            var signs = T.All<TextPrim>(prims).Where(t => t.Tag == DiagTags.Sign).ToList();
            Assert.Equal(2, signs.Count);
            // "+" at the left input: above the incoming line, left of the circle.
            var plus = signs.Single(t => t.Text == "+");
            Assert.True(plus.Position.X < -3 && plus.Position.Y < 0);
            // "−" at the feedback input: left of the vertical line, below the circle.
            var minus = signs.Single(t => t.Text == "−");
            Assert.True(minus.Position.X < 0 && minus.Position.Y > 3);
            Assert.True(new Rect(-6, -6, 12, 12).Inflate(0.01).Contains(T.Bounds(prims)));

            // Cross style: the signs move into the sectors of their inputs.
            var cross = T.Local(r, Edits.Replace(doc, o.WithParam("style", "cross")), o.Id);
            Assert.Equal(2, T.All<LinePrim>(cross).Count(l => l.Tag == DiagTags.Marker));
            Assert.All(T.All<TextPrim>(cross), t => Assert.True(t.Position.Length < 3));

            // Actions: the feedback sign is a choice (+ / −, the current one marked), add an input from above.
            var type = r.TypeOf(o);
            var sign = type.Actions(o, Lang.En).Where(a => a.Choice?.Key == "diag.sum.feedback").ToList();
            Assert.Equal(new[] { "diag.sum.feedback=plus", "diag.sum.feedback=minus" }, sign.Select(a => a.Id));
            Assert.True(sign[1].IsActive);
            var flipped = sign[0].Apply(o);
            Assert.Equal("++", flipped.Str("signs", ""));
            var three = type.Actions(o, Lang.En).Single(a => a.Id == "diag.sum.topInput").Apply(o);
            Assert.Equal("+-+", three.Str("signs", ""));
            Assert.Equal(3, T.All<TextPrim>(T.Local(r, Edits.Replace(doc, three), o.Id)).Count());
        }

        [Fact]
        public void FractionHasNumeratorAboveTheBarAndDenominatorBelow()
        {
            var r = T.Reg();
            var (doc, o) = T.Insert(r, "diag.ctrl.tf.fraction", new Vec2(100, 100));
            doc = Edits.Replace(doc, o.WithParam("numerator", "K_P").WithParam("denominator", "1 + s T_1"));
            var prims = T.Local(r, doc, o.Id);
            var num = T.All<TextPrim>(prims).Single(t => t.Text == "K_P");
            var den = T.All<TextPrim>(prims).Single(t => t.Text == "1 + s T_1");
            var bar = Assert.Single(T.All<LinePrim>(prims), l => l.Tag == DiagTags.Marker);
            Assert.Equal(bar.A.Y, bar.B.Y, 9);
            Assert.True(num.Position.Y < bar.A.Y && den.Position.Y > bar.A.Y);
            Assert.True(num.Position.Y + num.TextStyle.Size / 2 <= bar.A.Y + 1e-9 && den.Position.Y - den.TextStyle.Size / 2 >= bar.A.Y - 1e-9, "text overlaps the bar");
            // The bar spans the longer of the two.
            Assert.True(bar.B.X - bar.A.X >= D.Width("1 + s T_1", den.TextStyle.Size) - 1e-9);
            Assert.True(new Rect(-o.Size.W / 2, -o.Size.H / 2, o.Size.W, o.Size.H).Inflate(0.01).Contains(T.Bounds(prims)));
        }

        [Fact]
        public void TransferBlockShowsTheRoleNameOrADistinctCharacteristic()
        {
            var r = T.Reg();
            var (doc, o) = T.Insert(r, "diag.ctrl.tf.controller", new Vec2(100, 100));
            Assert.Equal("Controller", T.All<TextPrim>(T.Local(r, doc, o.Id)).Single().Text);
            Assert.Equal("Regler", T.All<TextPrim>(r.TypeOf(o).Render(o, new RenderContext(doc, Lang.De))).Single().Text);
            var sigs = new HashSet<string>();
            foreach (var (icon, _, _) in TransferType.Icons)
            {
                var x = o.WithParam("mode", "icon").WithParam("icon", icon);
                var prims = T.Local(r, Edits.Replace(doc, x), o.Id);
                Assert.True(new Rect(-o.Size.W / 2, -o.Size.H / 2, o.Size.W, o.Size.H).Inflate(0.01).Contains(T.Bounds(prims)), icon);
                Assert.Empty(T.All<TextPrim>(prims)); // icons are drawn, no font glyphs
                sigs.Add(T.Sig(prims.Where(p => p.Tag == DiagTags.Marker)));
            }
            Assert.Equal(TransferType.Icons.Length, sigs.Count);
            // The label is written below the content.
            var labelled = T.Local(r, Edits.Replace(doc, o.WithParam("label", "K, T")), o.Id);
            Assert.True(T.All<TextPrim>(labelled).Single(t => t.Text == "K, T").Position.Y > T.All<TextPrim>(labelled).Single(t => t.Text == "Controller").Position.Y);
        }

        [Fact]
        public void FeedbackSignalBetweenTwoBottomPortsRunsAroundBelow()
        {
            var r = T.Reg();
            var (doc, branch) = T.Insert(r, "diag.ctrl.branch", new Vec2(160, 100));
            (doc, var sum) = T.Insert(r, "diag.ctrl.sum", new Vec2(60, 100), doc);
            (doc, var sig) = T.Insert(r, "diag.ctrl.signal", new Vec2(100, 120), doc);
            var a = Port(r, doc, branch.Id, "bottom");
            var b = Port(r, doc, sum.Id, "bottom");
            var anchors = r.PageAnchors(doc.Get(branch.Id)!, new RenderContext(doc)).ToList();
            doc = Edits.SetPoint(doc, sig.Id, 0, a, Attachments.For(doc.Get(branch.Id)!, a, anchors.Single(x => x.Id == "bottom" && x.Role == AnchorRole.Port)));
            var sumAnchor = r.PageAnchors(doc.Get(sum.Id)!, new RenderContext(doc)).Single(x => x.Id == "bottom" && x.Role == AnchorRole.Port);
            doc = Edits.SetPoint(doc, sig.Id, 1, b, Attachments.For(doc.Get(sum.Id)!, b, sumAnchor));
            var main = Assert.IsType<PathPrim>(T.Render(r, doc, sig.Id).Single(p => p.Tag == DiagTags.Main));
            var pts = main.Segments.Select(s => s.P).ToList();
            Assert.Equal(4, pts.Count);
            Assert.True(pts[1].Y >= Math.Max(a.Y, b.Y) + SignalType.Clearance - 1e-9);
            Assert.Equal(pts[1].Y, pts[2].Y, 9);
            Assert.Equal(ArrowHead.Triangle, main.Style.ArrowEnd);
            Assert.True(pts[3].DistanceTo(b) < 1e-9);
        }

        [Fact]
        public void QuickAddChainsControlElementsWithSignalArrows()
        {
            var r = T.Reg();
            var (doc, sum) = T.Insert(r, "diag.ctrl.sum", new Vec2(60, 100));
            var res = Flow.QuickAdd(r, doc, sum.Id, FlowSide.Right, itemId: "diag.ctrl.tf.controller");
            var line = res.Doc.Get(res.ConnectorId!)!;
            Assert.Equal("diag.ctrl.signal", line.Type);
            Assert.Equal("right", line.Attach![0]!.AnchorId);
            Assert.Equal("left", line.Attach[1]!.AnchorId);
        }

        [Fact]
        public void StandardControlLoopExplodesIntoDockedElements()
        {
            var r = T.Reg();
            foreach (var item in new[] { "diag.ctrl.loop", "diag.ctrl.loop.sensor" })
            {
                var (doc, loop) = T.Insert(r, item, new Vec2(150, 100));
                var prims = T.Local(r, doc, loop.Id);
                Assert.True(new Rect(-loop.Size.W / 2, -loop.Size.H / 2, loop.Size.W, loop.Size.H).Inflate(0.01).Contains(T.Bounds(prims)), item);
                Assert.True(Assemblies.CanExplode(r, loop));
                var parts = Assemblies.Explode(r, doc, loop.Id);
                var sensor = item.EndsWith("sensor", StringComparison.Ordinal);
                Assert.Equal(sensor ? 5 : 4, parts.Count(p => !p.IsPointGeometry));
                Assert.Equal(sensor ? 7 : 6, parts.Count(p => p.Type == "diag.ctrl.signal"));
                Assert.Equal(new[] { "controller", "plant" }.Concat(sensor ? new[] { "sensor" } : Array.Empty<string>()),
                    parts.Where(p => p.Type == "diag.ctrl.tf").Select(p => p.Str("role", "")));
                // Every arrow end except the loop input and output is docked on a port and sits on it.
                var exploded = SketchDocument.Create(PageSizes.A4Landscape);
                exploded = Edits.Add(exploded, exploded.Pages[0].Id, parts.ToArray());
                var docked = 0;
                foreach (var line in parts.Where(p => p.IsPointGeometry))
                    for (var i = 0; i < line.Points!.Count; i++)
                    {
                        if (line.Attach?[i] is not { } at) continue;
                        docked++;
                        var port = r.PageAnchors(exploded.Get(at.ObjectId)!, new RenderContext(exploded)).Single(a => a.Role == AnchorRole.Port && a.Id == at.AnchorId);
                        Assert.True(exploded.ResolvePoints(exploded.Get(line.Id)!)[i].DistanceTo(port.P) < 1e-6);
                    }
                Assert.Equal(sensor ? 12 : 10, docked);
                // The feedback reaches the summing junction from below.
                var sum = parts.Single(p => p.Type == "diag.ctrl.sum");
                Assert.Contains(parts, p => p.Attach is { } a && a.Last()?.ObjectId == sum.Id && a.Last()!.AnchorId == "bottom");
            }
        }
    }

    public class ComputerSystemTests
    {
        [Fact]
        public void ComputerBlocksAreNamedByTheirRoleInBothLanguages()
        {
            var r = T.Reg();
            var (doc, o) = T.Insert(r, "diag.sys.block.processor", new Vec2(100, 100));
            Assert.Equal("Processor", T.All<TextPrim>(T.Local(r, doc, o.Id)).Single().Text);
            Assert.Equal("Prozessor", T.All<TextPrim>(r.TypeOf(o).Render(o, new RenderContext(doc, Lang.De))).Single().Text);
            Assert.DoesNotContain(T.Local(r, doc, o.Id), p => p.Tag == DiagTags.Port); // FIG. 1 boxes without port squares
            Assert.Equal("CPU", T.All<TextPrim>(T.Local(r, Edits.Replace(doc, o with { Text = "CPU" }), o.Id)).Single().Text);
        }

        [Fact]
        public void SystemBusIsARailThatDockedLinesFollow()
        {
            var r = T.Reg();
            var (doc, bus) = T.Insert(r, "diag.sys.bus", new Vec2(100, 100));
            var anchors = r.PageAnchors(doc.Get(bus.Id)!, new RenderContext(doc)).ToList();
            var rail = Assert.Single(anchors, a => a.Role == AnchorRole.Rail);
            var prims = T.Render(r, doc, bus.Id);
            Assert.Equal(1.0, T.StyleOf(prims.Single(p => p.Tag == DiagTags.Main)).StrokeWidth);
            Assert.Equal("Bus", T.All<TextPrim>(prims).Single().Text);
            var hollow = T.Render(r, Edits.Replace(doc, bus.WithParam("style", "double")), bus.Id).Single(p => p.Tag == DiagTags.Main);
            Assert.True(((PathPrim)hollow).IsClosed);
            // A connection line docks anywhere on the bus (a tap) and follows it.
            (doc, var line) = T.Insert(r, "diag.uml.relation", new Vec2(100, 80), doc);
            var opts = new SnapOptions { Grid = null };
            var raw = new Vec2(87.3, 100.8);
            var snap = Snapper.SnapPoint(raw, Snapper.Targets(r, doc, doc.Pages[0], new[] { line.Id }, new RenderContext(doc), opts), opts);
            Assert.Equal("rail", snap.Kind);
            var tap = raw + snap.Delta;
            Assert.Equal(new Vec2(87.3, 100), tap);
            var att = Attachments.FromSnap(doc, tap, snap);
            Assert.NotNull(att);
            doc = Edits.SetPoint(doc, line.Id, 1, tap, att);
            doc = Edits.Move(doc, new[] { bus.Id }, new Vec2(0, 10));
            doc = Attachments.Refresh(r, doc); // rail attachments are re-resolved after edits (as the engine does)
            Assert.True(doc.ResolvePoints(doc.Get(line.Id)!)[1].DistanceTo(tap + new Vec2(0, 10)) < 1e-6);
        }

        [Fact]
        public void WirelessLinkDrawsAZigzagOrRadioArcs()
        {
            var r = T.Reg();
            var (doc, o) = T.Insert(r, "diag.sys.wireless", new Vec2(100, 100));
            var zig = Assert.IsType<PathPrim>(T.Render(r, doc, o.Id).Single(p => p.Tag == DiagTags.Main));
            Assert.Equal(6, zig.Segments.Count);
            var arcs = T.Render(r, Edits.Replace(doc, o.WithParam("style", "arcs").WithParam("arrows", "both")), o.Id);
            var main = arcs.Single(p => p.Tag == DiagTags.Main);
            Assert.Equal(DashStyle.Dash, T.StyleOf(main).Dash);
            Assert.Equal(ArrowHead.Open, T.StyleOf(main).ArrowStart);
            Assert.Equal(6, arcs.Count(p => p.Tag == DiagTags.Marker));
        }

        [Fact]
        public void CommunicationIconsAreOutlinesWithPortsOnAllSides()
        {
            var r = T.Reg();
            foreach (var id in new[] { "diag.sys.phone", "diag.sys.basestation", "diag.sys.router", "diag.sys.switch", "diag.sys.satellite", "diag.sys.iot" })
            {
                var (doc, o) = T.Insert(r, id, new Vec2(100, 100));
                var ports = r.PageAnchors(doc.Get(o.Id)!, new RenderContext(doc)).Where(a => a.Role == AnchorRole.Port).Select(a => a.Id).ToList();
                Assert.Contains("top", ports); Assert.Contains("right", ports); Assert.Contains("bottom", ports); Assert.Contains("left", ports);
                var prims = T.Local(r, doc, o.Id);
                Assert.True(prims.Count(p => p.Tag is DiagTags.Outline or DiagTags.Marker) >= 3, id);
                // Black and white only.
                Assert.All(Prim.Flatten(prims).Where(p => p is not TextPrim), p => Assert.True(T.FillOf(p) is null || T.FillOf(p)!.Kind != FillKind.Solid || T.FillOf(p)!.Color is D.Paper or D.Ink, id));
            }
        }
    }

    public class DataLayoutTests
    {
        private static SketchObject Fields(Registry r, string item, string fields, out SketchDocument doc, Func<SketchObject, SketchObject>? f = null)
        {
            var (d, o) = T.Insert(r, item, new Vec2(150, 100));
            o = o.WithParam("fields", fields);
            if (f is not null) o = f(o);
            doc = Edits.Replace(d, o);
            return o;
        }

        [Fact]
        public void RowWidthsAreProportionalOrEqual()
        {
            var r = T.Reg();
            var o = Fields(r, "diag.data.fields", "Preamble:8;Header:16;CRC:0x10", out var doc);
            var cells = DataFieldsType.Cells(o, Lang.En);
            Assert.Equal(3, cells.Count);
            Assert.Equal(2 * cells[0].Rect.W, cells[1].Rect.W, 6);
            Assert.Equal(cells[1].Rect.W, cells[2].Rect.W, 6); // 0x10 = 16
            Assert.Equal(o.Size.W, cells.Sum(c => c.Rect.W), 6);
            Assert.Equal(new[] { "Preamble", "Header", "CRC" }, T.All<TextPrim>(T.Local(r, doc, o.Id)).Where(t => t.Tag == DiagTags.Name).Select(t => t.Text));
            Assert.Contains(T.All<TextPrim>(T.Local(r, doc, o.Id)), t => t.Text == "16 bit");
            var equal = DataFieldsType.Cells(o.WithParam("widths", "equal"), Lang.En);
            Assert.All(equal, c => Assert.Equal(o.Size.W / 3, c.Rect.W, 6));
            // Ports above and below every field.
            var ports = r.PageAnchors(doc.Get(o.Id)!, new RenderContext(doc)).Where(a => a.Role == AnchorRole.Port).ToDictionary(a => a.Id!, a => a.P);
            Assert.Equal(o.BoxPoint(0, 0) + new Vec2(cells[1].Rect.X + cells[1].Rect.W / 2, cells[1].Rect.Y), ports["f2.top"]);
            Assert.Equal(o.BoxPoint(0, 0) + new Vec2(cells[2].Rect.X + cells[2].Rect.W / 2, cells[2].Rect.Y + cells[2].Rect.H), ports["f3.bottom"]);
        }

        [Fact]
        public void VariableFieldsGetABreakMark()
        {
            var r = T.Reg();
            var o = Fields(r, "diag.data.fields", "Header:16;Payload:n;CRC:16", out var doc);
            var cells = DataFieldsType.Cells(o, Lang.En);
            Assert.True(cells[1].Variable);
            Assert.Equal(cells[0].Rect.W, cells[2].Rect.W, 6);
            var prims = T.Local(r, doc, o.Id);
            Assert.Equal(4, T.All<LinePrim>(prims).Count(l => l.Tag == DiagTags.Marker)); // two slashes on each long edge
            Assert.True(new Rect(-o.Size.W / 2, -o.Size.H / 2, o.Size.W, o.Size.H).Inflate(0.01).Contains(T.Bounds(prims)));
        }

        [Fact]
        public void WrappedRowsSplitFieldsAtTheRowEnd()
        {
            var r = T.Reg();
            var o = Fields(r, "diag.data.fields", "A:16;B:32;C:16;D:n", out var doc, x => x.WithParam("layout", "wrapped").WithParam("rowBits", 32).WithParam("ruler", true) with { Size = new Size2(96, 30) });
            var cells = DataFieldsType.Cells(o, Lang.En);
            // B starts in the middle of row 1 and continues in row 2; D (variable) fills row 3.
            var b = cells.Where(c => c.Field == 1).OrderBy(c => c.Rect.Y).ToList();
            Assert.Equal(2, b.Count);
            Assert.Equal(48, b[0].Rect.W, 6);
            Assert.Equal(48, b[1].Rect.W, 6);
            Assert.Equal(-48, b[1].Rect.X, 6);
            Assert.True(b[1].Rect.Y > b[0].Rect.Y);
            var d = cells.Single(c => c.Field == 3);
            Assert.True(d.Variable);
            Assert.Equal(96, d.Rect.W, 6);
            Assert.Equal(3, cells.Select(c => Math.Round(c.Rect.Y, 6)).Distinct().Count());
            var prims = T.Local(r, doc, o.Id);
            Assert.Equal(new[] { "0", "8", "16", "24" }, T.All<TextPrim>(prims).Where(t => t.Tag == DiagTags.Ruler).Select(t => t.Text));
            Assert.Equal(4, T.All<TextPrim>(prims).Count(t => t.Tag == DiagTags.Name)); // each name once
        }

        [Fact]
        public void MemoryMapShowsStartAddresses()
        {
            var r = T.Reg();
            var o = Fields(r, "diag.data.fields.memory", "Boot:0x1000;Kernel:0x4000;Config:0x400", out var doc);
            var prims = T.Local(r, doc, o.Id);
            Assert.Equal(new[] { "0x0000", "0x1000", "0x5000", "0x5400" }, T.All<TextPrim>(prims).Where(t => t.Tag == DiagTags.Ruler).Select(t => t.Text));
            var cells = DataFieldsType.Cells(o, Lang.En);
            Assert.True(cells[1].Rect.Y > cells[0].Rect.Y); // stacked top-down
            Assert.True(new Rect(-o.Size.W / 2, -o.Size.H / 2, o.Size.W, o.Size.H).Inflate(0.01).Contains(T.Bounds(prims)));
            Assert.Contains(r.PageAnchors(doc.Get(o.Id)!, new RenderContext(doc)), a => a.Id == "f2.right");
            // Addresses stop after a variable field.
            Assert.Equal(new string?[] { "0x0000", null, null }, DataFieldsType.Addresses(o, DataFieldsType.Parse("A:n;B:4")));
        }

        [Fact]
        public void AddFieldAppendsToTheFieldList()
        {
            var r = T.Reg();
            var (_, o) = T.Insert(r, "diag.data.fields", new Vec2(100, 100));
            var add = r.TypeOf(o).Actions(o, Lang.En).Single(a => a.Id == "diag.data.addField");
            var x = add.Input!.Apply(o, "Flags:4");
            Assert.Equal("Preamble:8;Header:16;Payload:n;CRC:16;Flags:4", x.Str("fields", ""));
            Assert.Equal(5, DataFieldsType.FieldsOf(x, Lang.En).Count);
        }
    }
}
