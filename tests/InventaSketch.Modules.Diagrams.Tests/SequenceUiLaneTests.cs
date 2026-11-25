using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Diagrams.Tests
{
    public class SequenceFragmentTests
    {
        [Fact]
        public void FragmentHasATagDashedOperandSeparatorsAndGuards()
        {
            var r = T.Reg();
            var (doc, o) = T.Insert(r, "diag.seq.fragment", new Vec2(100, 100));
            var prims = T.Local(r, doc, o.Id);
            Assert.Equal("alt", T.All<TextPrim>(prims).Single(t => t.Tag == DiagTags.Name).Text);
            var dashed = T.All<LinePrim>(prims).Where(l => l.Tag == DiagTags.Divider && l.Style.Dash == DashStyle.Dash).ToList();
            Assert.Single(dashed);
            Assert.Equal(0, dashed[0].A.Y, 6); // two operands of equal height
            var guards = T.All<TextPrim>(prims).Where(t => t.Tag == DiagTags.Guard).ToList();
            Assert.Equal(new[] { "[condition]", "[else]" }, guards.Select(g => g.Text));
            Assert.True(guards[0].Position.Y < 0 && guards[1].Position.Y > 0); // one guard per operand
            // Pentagon tag: an open polyline with a cut corner.
            Assert.Single(T.All<PathPrim>(prims), p => p.Tag == DiagTags.Divider && p.Segments.Count == 4);
            Assert.Equal("[Bedingung]", T.All<TextPrim>(r.TypeOf(o).Render(o, new RenderContext(doc, Lang.De))).First(t => t.Tag == DiagTags.Guard).Text);

            // Add an operand: the frame grows by one operand, the top stays.
            var type = r.TypeOf(o);
            var top = o.BoxPoint(0, -0.5);
            var three = type.Actions(o, Lang.En).Single(a => a.Id == "diag.fragment.addOperand").Apply(o);
            Assert.Equal(3, three.Int("operands", 0));
            Assert.Equal(o.Size.H * 1.5, three.Size.H, 9);
            Assert.True(three.BoxPoint(0, -0.5).DistanceTo(top) < 1e-9);
            var p3 = T.Local(r, Edits.Replace(doc, three), o.Id);
            Assert.Equal(2, T.All<LinePrim>(p3).Count(l => l.Tag == DiagTags.Divider && l.Style.Dash == DashStyle.Dash));
            Assert.Equal(new[] { "[condition]", "[else]", "[else]" }, T.All<TextPrim>(p3).Where(t => t.Tag == DiagTags.Guard).Select(g => g.Text));

            var loop = T.Local(r, Edits.Replace(doc, o.WithParam("kind", "loop").WithParam("operands", 1) with { Text = "1..n" }), o.Id);
            Assert.Equal("loop 1..n", T.All<TextPrim>(loop).Single(t => t.Tag == DiagTags.Name).Text);
            Assert.DoesNotContain(T.All<LinePrim>(loop), l => l.Style.Dash == DashStyle.Dash);
        }

        [Fact]
        public void NumberMessagesNumbersTopDown()
        {
            var r = T.Reg();
            var doc = SketchDocument.Create(PageSizes.A4Landscape);
            var ids = new List<string>();
            foreach (var y in new[] { 140.0, 100.0, 120.0 })
            {
                (doc, var m) = T.Insert(r, "diag.seq.message", new Vec2(100, y), doc);
                ids.Add(m.Id);
            }
            var o = doc.Get(ids[0])!;
            var action = r.TypeOf(o).Actions(o, new RenderContext(doc)).Single(a => a.Id == "diag.msg.number");
            Assert.NotNull(action.ApplyInDocument);
            doc = action.Run(doc, o, new RenderContext(doc));
            Assert.Equal(new[] { "306", "302", "304" }, ids.Select(id => doc.Get(id)!.Str("step", "")));
            // Running it again (several messages selected) changes nothing; a new start on the topmost message counts on from there.
            var again = ContextActions.Run(r, doc, ids, "diag.msg.number", null, Lang.En);
            Assert.True(Variants.Diff(doc, again).None);
            doc = Edits.Replace(doc, doc.Get(ids[1])!.WithParam("step", "100"));
            doc = ContextActions.Run(r, doc, new[] { ids[2] }, "diag.msg.number", null, Lang.En);
            Assert.Equal(new[] { "104", "100", "102" }, ids.Select(id => doc.Get(id)!.Str("step", "")));

            // The numeral is drawn near the start of the arrow, above the line.
            var m0 = doc.Get(ids[1])!;
            var numeral = T.All<TextPrim>(T.Render(r, doc, m0.Id)).Single(t => t.Tag == DiagTags.Numeral);
            Assert.Equal("100", numeral.Text);
            Assert.True(numeral.Position.Y < m0.Points![0].Y);
            Assert.True(numeral.Position.DistanceTo(m0.Points[0]) < numeral.Position.DistanceTo(m0.Points[1]));
        }

        [Fact]
        public void LifelineTimeGapsInterruptTheTail()
        {
            var r = T.Reg();
            var (doc, o) = T.Insert(r, "diag.seq.lifeline.gap", new Vec2(100, 100));
            var prims = T.Local(r, doc, o.Id);
            var tail = T.All<LinePrim>(prims).Where(l => l.Tag == DiagTags.Main).ToList();
            Assert.Equal(2, tail.Count);
            Assert.True(tail[1].A.Y - tail[0].B.Y >= 2 * LifelineType.BreakGap - 1e-9);
            Assert.Equal(2, T.All<LinePrim>(prims).Count(l => l.Tag == DiagTags.Marker)); // "//"
            var two = T.Local(r, Edits.Replace(doc, o.WithParam("breaks", "30; 70")), o.Id);
            Assert.Equal(3, T.All<LinePrim>(two).Count(l => l.Tag == DiagTags.Main));
            var off = r.TypeOf(o).Actions(o, Lang.En).Single(a => a.Id == "diag.seq.timegap").Apply(o);
            Assert.Single(T.All<LinePrim>(T.Local(r, Edits.Replace(doc, off), o.Id)), l => l.Tag == DiagTags.Main);
            // The tail stays one rail for docking messages.
            Assert.Single(r.PageAnchors(doc.Get(o.Id)!, new RenderContext(doc)), a => a.Role == AnchorRole.Rail);
        }
    }

    public class StateCircleTests
    {
        [Fact]
        public void CircleViewDrawsTheStateAsACircleWithItsName()
        {
            var r = T.Reg();
            var (doc, o) = T.Insert(r, "diag.state.circle", new Vec2(100, 100));
            var prims = T.Local(r, doc, o.Id);
            var e = Assert.Single(T.All<EllipsePrim>(prims));
            Assert.Equal(10, e.Rx, 9);
            Assert.Equal("State", T.All<TextPrim>(prims).Single().Text);
            var ports = r.PageAnchors(doc.Get(o.Id)!, new RenderContext(doc)).Where(a => a.Role == AnchorRole.Port).ToList();
            Assert.Equal(8, ports.Count);
            Assert.All(ports, p => Assert.Equal(10, p.P.DistanceTo(new Vec2(100, 100)), 6));
            // Switching back to the rounded view adapts the size.
            var rounded = r.WithView(doc.Get(o.Id)!, "rounded", new RenderContext(doc));
            Assert.True(rounded.Size.W > rounded.Size.H);
            var (d2, t) = T.Insert(r, "diag.state.transition", new Vec2(100, 100));
            Assert.Equal(ArrowHead.Open, T.StyleOf(T.Render(r, d2, t.Id).Single(p => p.Tag == DiagTags.Main)).ArrowEnd);
            Assert.Contains(T.All<TextPrim>(T.Render(r, d2, t.Id)), x => x.Text == "event [guard] / action");
        }
    }

    public class WireframeTests
    {
        [Fact]
        public void WidgetsRenderInsideTheirBoxAndShowTheirState()
        {
            var r = T.Reg();
            foreach (var item in r.Library.Where(i => i.Category == DiagCategories.Ui))
            {
                var (doc, o) = T.Insert(r, item.Id, new Vec2(100, 100));
                var prims = T.Local(r, doc, o.Id);
                Assert.True(prims.Count > 0, item.Id);
                Assert.True(new Rect(-o.Size.W / 2, -o.Size.H / 2, o.Size.W, o.Size.H).Inflate(0.01).Contains(T.Bounds(prims)), item.Id + " " + T.Bounds(prims));
                if (item.TypeId != "diag.ui.screen") Assert.Null(r.TypeOf(o).ChainConnector(o));
            }

            // Toggle: knob right and track filled when on, left and outlined when off.
            var (d1, toggle) = T.Insert(r, "diag.ui.toggle", new Vec2(100, 100));
            EllipsePrim Knob(SketchObject x) => T.All<EllipsePrim>(T.Local(r, Edits.Replace(d1, x), toggle.Id)).Single();
            var on = Knob(toggle);
            var off = Knob(toggle.WithParam("on", false));
            Assert.True(on.Center.X > off.Center.X);
            Assert.Equal(D.Ink, T.FillOf(T.Local(r, d1, toggle.Id).First(p => p.Tag == DiagTags.Outline))!.Color);

            // Checkbox: check mark only when checked.
            var (d2, cb) = T.Insert(r, "diag.ui.checkbox", new Vec2(100, 100));
            Assert.Single(T.Local(r, d2, cb.Id), p => p.Tag == DiagTags.Marker);
            Assert.DoesNotContain(T.Local(r, Edits.Replace(d2, cb.WithParam("checked", false)), cb.Id), p => p.Tag == DiagTags.Marker);

            // List: one row per item, divider lines between.
            var (d3, list) = T.Insert(r, "diag.ui.list", new Vec2(100, 100));
            d3 = Edits.Replace(d3, list.WithParam("items", "Inbox\nSent\nDrafts\nTrash"));
            var lp = T.Local(r, d3, list.Id);
            Assert.Equal(new[] { "Inbox", "Sent", "Drafts", "Trash" }, T.All<TextPrim>(lp).Select(t => t.Text));
            Assert.Equal(3, T.All<LinePrim>(lp).Count(l => l.Tag == DiagTags.Divider));
            Assert.Equal(new[] { "Eintrag 1", "Eintrag 2", "Eintrag 3" }, T.All<TextPrim>(r.TypeOf(list).Render(list, new RenderContext(d3, Lang.De))).Select(t => t.Text));

            // Tabs: the active one is bold and underlined.
            var (d4, tabs) = T.Insert(r, "diag.ui.tabs", new Vec2(100, 100));
            d4 = Edits.Replace(d4, tabs.WithParam("active", 2));
            var tp = T.Local(r, d4, tabs.Id);
            Assert.Equal("Tab 2", T.All<TextPrim>(tp).Single(t => t.TextStyle.Bold).Text);
            var bar = (RectPrim)tp.Single(p => p.Tag == DiagTags.Marker);
            Assert.True(bar.X > -tabs.Size.W / 6 - 1 && bar.X < 0);

            // Image placeholder: frame with both diagonals.
            var (d5, img) = T.Insert(r, "diag.ui.image", new Vec2(100, 100));
            Assert.Equal(2, T.All<LinePrim>(T.Local(r, d5, img.Id)).Count());

            // Pointers: the tip is a port.
            var (d6, touch) = T.Insert(r, "diag.ui.touch", new Vec2(100, 100));
            var tip = r.PageAnchors(d6.Get(touch.Id)!, new RenderContext(d6)).Single(a => a.Id == "tip").P;
            Assert.True(tip.Y < 100 - touch.Size.H / 4);
            var (d7, cursor) = T.Insert(r, "diag.ui.cursor", new Vec2(100, 100));
            Assert.Equal(cursor.BoxPoint(-0.5, -0.5), r.PageAnchors(d7.Get(cursor.Id)!, new RenderContext(d7)).Single(a => a.Id == "tip").P);
        }

        [Fact]
        public void ScreensOfferDevicesAndChainIntoAScreenFlow()
        {
            var r = T.Reg();
            var (doc, o) = T.Insert(r, "diag.ui.screen", new Vec2(100, 100));
            var sigs = new HashSet<string>();
            foreach (var (device, _, _) in UiScreenType.Devices)
            {
                var x = o.WithParam("device", device) with { Text = "Login" };
                var prims = T.Local(r, Edits.Replace(doc, x), o.Id);
                Assert.True(new Rect(-o.Size.W / 2, -o.Size.H / 2, o.Size.W, o.Size.H).Inflate(0.01).Contains(T.Bounds(prims)), device);
                Assert.True(UiScreenType.Content(x).W > 0 && UiScreenType.Content(x).H > 0);
                sigs.Add(T.Sig(prims));
            }
            Assert.Equal(UiScreenType.Devices.Length, sigs.Count);
            var res = Flow.QuickAdd(r, doc, o.Id, FlowSide.Right);
            Assert.Equal("diag.uml.relation", res.Doc.Get(res.ConnectorId!)!.Type);
        }
    }

    public class LaneTests
    {
        [Fact]
        public void PoolLanesAreAddedRemovedAndRenamed()
        {
            var r = T.Reg();
            var (doc, pool) = T.Insert(r, "diag.bpmn.pool", new Vec2(150, 100));
            var type = (LaneContainerType)r.TypeOf(pool);
            Assert.Equal(new[] { "Lane 1", "Lane 2" }, type.Lanes(pool, Lang.En));
            Assert.Equal(new[] { "Bahn 1", "Bahn 2" }, type.Lanes(pool, Lang.De));
            var prims = T.Local(r, doc, pool.Id);
            Assert.Equal("Pool", T.All<TextPrim>(prims).Single(t => t.Tag == DiagTags.Name).Text);
            Assert.All(T.All<TextPrim>(prims), t => Assert.Equal(-90, t.Rotation));

            // A task in the second lane, centred on its middle line.
            var lane2 = LaneMiddle(r, doc, pool.Id, 1);
            (doc, var task) = T.Insert(r, "diag.bpmn.task", new Vec2(170, lane2.Y), doc);
            var top = pool.BoxPoint(0, -0.5).Y;
            var actions = type.Actions(doc.Get(pool.Id)!, new RenderContext(doc)).ToList();
            var add = actions.Single(a => a.Id == "diag.lanes.add");
            Assert.Equal("Add lane below", add.Label.En);
            doc = add.Run(doc, doc.Get(pool.Id)!, new RenderContext(doc));
            var p3 = doc.Get(pool.Id)!;
            Assert.Equal(new[] { "Lane 1", "Lane 2", "Lane 3" }, type.Lanes(p3, Lang.En));
            Assert.Equal(pool.Size.H * 1.5, p3.Size.H, 9);
            // The task moved with its lane and is still centred on it.
            Assert.Equal(LaneMiddle(r, doc, pool.Id, 1).Y, doc.Get(task.Id)!.Transform.Y, 6);
            Assert.True(p3.BoxPoint(0, -0.5).Y < top);

            // Add above, then remove the top and the bottom lane again.
            doc = ContextActions.Run(r, doc, new[] { pool.Id }, "diag.lanes.addStart", null, Lang.En);
            Assert.Equal(4, type.Lanes(doc.Get(pool.Id)!, Lang.En).Count);
            Assert.Equal(LaneMiddle(r, doc, pool.Id, 2).Y, doc.Get(task.Id)!.Transform.Y, 6);
            doc = ContextActions.Run(r, doc, new[] { pool.Id }, "diag.lanes.removeStart", null, Lang.En);
            doc = ContextActions.Run(r, doc, new[] { pool.Id }, "diag.lanes.remove", null, Lang.En);
            Assert.Equal(new[] { "Lane 1", "Lane 2" }, type.Lanes(doc.Get(pool.Id)!, Lang.En));
            Assert.Equal(pool.Size, doc.Get(pool.Id)!.Size);
            Assert.Equal(lane2.Y, doc.Get(task.Id)!.Transform.Y, 6);

            // Rename a lane by an input action.
            doc = ContextActions.Run(r, doc, new[] { pool.Id }, "diag.lanes.rename.2", "Sales", Lang.En);
            Assert.Equal(new[] { "Lane 1", "Sales" }, type.Lanes(doc.Get(pool.Id)!, Lang.En));
            // One lane left: remove is offered but disabled.
            var single = doc.Get(pool.Id)!.WithParam("lanes", "Only");
            Assert.False(type.Actions(single, Lang.En).Single(a => a.Id == "diag.lanes.remove").IsEnabled);
        }

        private static Vec2 LaneMiddle(Registry r, SketchDocument doc, string poolId, int lane)
        {
            var axis = r.PageAnchors(doc.Get(poolId)!, new RenderContext(doc)).Single(a => a.Role == AnchorRole.Axis && a.Id == LaneContainerType.LaneAxisId(lane));
            return Vec2.Lerp(axis.P, axis.Q, 0.5);
        }

        [Fact]
        public void ElementsSnapCentredIntoALaneAndFollowThePool()
        {
            var r = T.Reg();
            var (doc, pool) = T.Insert(r, "diag.bpmn.pool", new Vec2(150, 100));
            var middle = LaneMiddle(r, doc, pool.Id, 0);
            (doc, var task) = T.Insert(r, "diag.bpmn.task", new Vec2(160, middle.Y + 1.3), doc);
            var ctx = new RenderContext(doc);
            var opts = new SnapOptions { Grid = null, Alignment = false, Page = false };
            var moving = r.PageAnchors(doc.Get(task.Id)!, ctx).ToList();
            var res = Snapper.SnapMove(moving, Vec2.Zero, Snapper.Targets(r, doc, doc.Pages[0], new[] { task.Id }, ctx, opts), opts);
            Assert.Equal("axis", res.Kind);
            Assert.Equal(-1.3, res.Delta.Y, 6);
            Assert.Equal(0, res.Delta.X, 6);
            doc = Edits.Move(doc, new[] { task.Id }, res.Delta);

            // Moving the pool carries the centred task (Core coupling: hub on an axis).
            var before = doc;
            var after = Coupling.Propagate(r, before, Edits.Move(before, new[] { pool.Id }, new Vec2(20, -10)));
            Assert.Equal(new Vec2(180, middle.Y - 10), new Vec2(after.Get(task.Id)!.Transform.X, after.Get(task.Id)!.Transform.Y));
            // Stretching the pool keeps it in its lane.
            var stretched = Coupling.Propagate(r, after, Edits.Update(after, new[] { pool.Id }, p => p with { Size = new Size2(p.Size.W, p.Size.H * 1.5) }));
            Assert.Equal(LaneMiddle(r, stretched, pool.Id, 0).Y, stretched.Get(task.Id)!.Transform.Y, 6);

            // Lane ends offer no mechanical build ports.
            Assert.DoesNotContain(Building.Ports(r, doc, pool.Id), p => p.Kind == PortKind.AxisEnd);
        }

        [Fact]
        public void SwimlaneHasTheSameInteractiveLanes()
        {
            var r = T.Reg();
            var (doc, lanes) = T.Insert(r, "diag.activity.swimlane", new Vec2(150, 100));
            var axes = r.PageAnchors(doc.Get(lanes.Id)!, new RenderContext(doc)).Where(a => a.Role == AnchorRole.Axis).ToList();
            Assert.Equal(2, axes.Count);
            Assert.All(axes, a => Assert.Equal(a.P.X, a.Q.X, 9)); // vertical lanes: vertical middle lines
            var labels = r.TypeOf(lanes).Actions(lanes, Lang.De).Where(a => a.Group == "lanes").Select(a => a.Label.De).ToList();
            Assert.Contains("Bahn rechts hinzufügen", labels);
            Assert.Contains("Bahn 2 umbenennen", labels);
        }
    }

    public class NewItemPersistenceTests
    {
        [Fact]
        public void ParametersOfTheNewElementsSurviveTheRoundTrip()
        {
            var r = T.Reg();
            var doc = SketchDocument.Create(PageSizes.A4Landscape);
            var edits = new (string Item, Func<SketchObject, SketchObject> F)[]
            {
                ("diag.ctrl.sum", o => o.WithParam("signs", "+-+").WithParam("style", "cross")),
                ("diag.ctrl.tf", o => o.WithParam("mode", "fraction").WithParam("numerator", "K (1 + s T_D)").WithParam("label", "K, T")),
                ("diag.ctrl.loop", o => o.WithParam("sensor", true).WithParam("disturbance", true)),
                ("diag.data.fields", o => o.WithParam("fields", "Länge:0x10;Daten:n").WithParam("layout", "wrapped").WithParam("ruler", true)),
                ("diag.seq.fragment", o => o.WithParam("kind", "par").WithParam("guards", "[a]\n[b]") with { Text = "x" }),
                ("diag.seq.message", o => o.WithParam("step", "310")),
                ("diag.seq.lifeline", o => o.WithParam("breaks", "40; 75")),
                ("diag.bpmn.pool", o => o.WithParam("lanes", "Kunde\nVertrieb\nLager") with { Text = "Firma" }),
                ("diag.ui.screen", o => o.WithParam("device", "browser") with { Text = "example.org" }),
                ("diag.ui.slider", o => o.WithParam("value", 75.0)),
                ("diag.sys.bus", o => o.WithParam("style", "double")),
                ("diag.state.circle", o => o with { Text = "S1" }),
            };
            var x = 20.0;
            foreach (var (item, f) in edits)
            {
                var o = f(r.Create(r.Item(item)!, new Vec2(x, 80)));
                doc = Edits.Add(doc, doc.Pages[0].Id, o);
                x += 25;
            }
            var json = DocumentJson.Serialize(doc);
            var back = DocumentJson.Deserialize(json);
            Assert.Equal(json, DocumentJson.Serialize(back));
            Assert.Equal(SvgRenderer.RenderPage(r, doc, doc.Pages[0]), SvgRenderer.RenderPage(r, back, back.Pages[0]));
            Assert.Equal(json, DocumentJson.Serialize(DocumentJson.FromCompact(DocumentJson.ToCompact(doc))));
            Assert.Contains(back.Objects.Values, o => o.Type == "diag.ctrl.sum" && o.Str("signs", "") == "+-+");
            Assert.Contains(back.Objects.Values, o => o.Type == "diag.state.state" && o.View == "circle");
        }
    }
}
