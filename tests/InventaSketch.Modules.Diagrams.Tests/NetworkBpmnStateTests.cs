using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Diagrams.Tests
{
    /// <summary>
    /// Round 6 (2026-11-26): network diagrams with the critical path (forward and backward pass, floats, heavy critical
    /// activities and links, "Arrange network"), BPMN event triggers, data objects and flows, and UML pseudo states.
    /// </summary>
    public class NetworkBpmnStateTests
    {
        /// <summary>A dependency from the right side of <paramref name="from"/> to the left side of <paramref name="to"/>.</summary>
        private static SketchDocument Link(Registry r, SketchDocument doc, SketchObject from, SketchObject to)
        {
            var (d, l) = T.Insert(r, Cpm.LinkType, new Vec2(0, 0), doc);
            d = D.AttachPoint(d, l.Id, 0, from.Id, from.BoxPoint(0.5, 0));
            return D.AttachPoint(d, l.Id, 1, to.Id, to.BoxPoint(-0.5, 0));
        }

        /// <summary>A (3) → B (2) → D (4) and A → C (5) → D: critical path A–C–D, 12 units; B has 3 units of float.</summary>
        private static (SketchDocument Doc, SketchObject A, SketchObject B, SketchObject C, SketchObject D) Network(Registry r)
        {
            var (doc, a) = T.Insert(r, Cpm.NodeType, new Vec2(40, 60));
            (doc, var b) = T.Insert(r, Cpm.NodeType, new Vec2(100, 30), doc);
            (doc, var c) = T.Insert(r, Cpm.NodeType, new Vec2(100, 90), doc);
            (doc, var d) = T.Insert(r, Cpm.NodeType, new Vec2(160, 60), doc);
            doc = Edits.Replace(doc, a.WithParam("duration", "3"));
            doc = Edits.Replace(doc, b.WithParam("duration", "2"));
            doc = Edits.Replace(doc, c.WithParam("duration", "5 d"));
            doc = Edits.Replace(doc, d.WithParam("duration", "4"));
            doc = Link(r, doc, a, b);
            doc = Link(r, doc, a, c);
            doc = Link(r, doc, b, d);
            doc = Link(r, doc, c, d);
            return (doc, a, b, c, d);
        }

        [Fact]
        public void NetworkElementsInsertWithoutParametersAndChainWithDependencies()
        {
            var r = T.Reg();
            var (doc, n) = T.Insert(r, Cpm.NodeType, new Vec2(50, 50));
            Assert.NotEmpty(T.Render(r, doc, n.Id));
            Assert.Equal(Cpm.LinkType, r.TypeOf(n).ChainConnector(n));
            Assert.Contains(T.All<TextPrim>(T.Local(r, doc, n.Id)), t => t.Text == "Activity");
            var (d2, l) = T.Insert(r, Cpm.LinkType, new Vec2(50, 50));
            Assert.NotEmpty(T.Render(r, d2, l.Id));
            // Without durations nothing is computed.
            Assert.Empty(Cpm.Schedule(doc));
        }

        [Fact]
        public void ForwardAndBackwardPassGiveTheFloatsAndTheCriticalPath()
        {
            var r = T.Reg();
            var (doc, a, b, c, d) = Network(r);
            var s = Cpm.Schedule(doc);
            Assert.Equal(4, s.Count);
            Assert.Equal((0.0, 3.0), (s[a.Id].Es, s[a.Id].Ef));
            Assert.Equal((3.0, 5.0, 6.0, 8.0, 3.0), (s[b.Id].Es, s[b.Id].Ef, s[b.Id].Ls, s[b.Id].Lf, s[b.Id].Tf));
            Assert.Equal(3.0, s[b.Id].Ff);
            Assert.Equal((3.0, 8.0, 0.0), (s[c.Id].Es, s[c.Id].Ef, s[c.Id].Tf));
            Assert.Equal((8.0, 12.0, 12.0), (s[d.Id].Es, s[d.Id].Ef, s[d.Id].Lf));
            Assert.True(s[a.Id].Critical && s[c.Id].Critical && s[d.Id].Critical);
            Assert.False(s[b.Id].Critical);
            Assert.True(Cpm.IsCriticalLink(doc, (a.Id, c.Id)));
            Assert.False(Cpm.IsCriticalLink(doc, (a.Id, b.Id)));
        }

        [Fact]
        public void CriticalActivitiesAndLinksAreDrawnHeavyAndShowTheirTimes()
        {
            var r = T.Reg();
            var (doc, a, b, c, d) = Network(r);
            double OutlineWidth(SketchObject o) => T.StyleOf(T.Render(r, doc, o.Id).First(p => p.Tag == DiagTags.Outline)).StrokeWidth ?? 0;
            Assert.True(OutlineWidth(c) > OutlineWidth(b) + 0.2);
            var texts = T.All<TextPrim>(T.Local(r, doc, b.Id)).Where(t => t.Tag == DiagTags.Value).Select(t => t.Text).ToList();
            Assert.Equal(new[] { "3", "2", "5", "6", "3", "8" }, texts);
            var links = doc.Objects.Values.Where(o => o.Type == Cpm.LinkType).ToList();
            double LinkWidth(SketchObject l) => T.StyleOf(T.Render(r, doc, l.Id).First(p => p.Tag == DiagTags.Main)).StrokeWidth ?? 0;
            var critical = links.Where(l => l.Attach!.Any(x => x?.ObjectId == c.Id)).ToList();
            var normal = links.Where(l => l.Attach!.Any(x => x?.ObjectId == b.Id)).ToList();
            Assert.All(critical, l => Assert.True(LinkWidth(l) > 0.5));
            Assert.All(normal, l => Assert.True(LinkWidth(l) < 0.5));
            // German decimal comma.
            var half = Edits.Replace(doc, doc.Get(b.Id)!.WithParam("duration", "2,5"));
            var de = Prim.Flatten(r.Render(half.Get(b.Id)!, new RenderContext(half, Lang.De))).OfType<TextPrim>().Where(t => t.Tag == DiagTags.Value).Select(t => t.Text).ToList();
            Assert.Contains("2,5", de);
            // A changed duration redraws the whole network.
            Assert.Contains(d.Id, r.TypeOf(a).Dependents(doc.Get(a.Id)!, new RenderContext(doc)));
        }

        [Fact]
        public void AMissingDurationOrACycleLeavesTheNetworkWithoutTimes()
        {
            var r = T.Reg();
            var (doc, a, b, _, d) = Network(r);
            var open = Edits.Replace(doc, doc.Get(b.Id)! with { Params = doc.Get(b.Id)!.Params.Where(kv => kv.Key != "duration").ToDictionary(kv => kv.Key, kv => kv.Value) });
            Assert.Empty(Cpm.Schedule(open));
            var cycle = Link(r, doc, d, a);
            Assert.Empty(Cpm.Schedule(cycle));
            Assert.Equal(2.5, Cpm.ParseDuration("2,5 Wochen"));
            Assert.Null(Cpm.ParseDuration("abc"));
        }

        [Fact]
        public void ArrangeNetworkPlacesActivitiesInColumnsByTheirPredecessors()
        {
            var r = T.Reg();
            var (doc, a, b, c, d) = Network(r);
            var shuffled = Edits.Update(doc, new[] { d.Id }, o => o with { Transform = o.Transform with { X = 20, Y = 150 } });
            var action = r.TypeOf(a).Actions(shuffled.Get(a.Id)!, new RenderContext(shuffled)).Single(x => x.Id == "diag.net.arrange");
            Assert.True(action.IsEnabled);
            var arranged = ContextActions.Run(r, shuffled, new[] { a.Id }, "diag.net.arrange", null, Lang.En);
            double X(string id) => arranged.Get(id)!.Transform.X;
            Assert.True(X(a.Id) < X(b.Id));
            Assert.Equal(X(b.Id), X(c.Id), 6);
            Assert.True(X(c.Id) < X(d.Id));
        }

        [Fact]
        public void BpmnEventsShowTheirTriggerFilledWhenThrowing()
        {
            var r = T.Reg();
            var (d0, plain) = T.Insert(r, "diag.bpmn.start", new Vec2(50, 50));
            var (d1, msg) = T.Insert(r, "diag.bpmn.start.message", new Vec2(50, 50));
            Assert.DoesNotContain(T.Local(r, d0, plain.Id), p => p.Tag == DiagTags.Marker);
            Assert.Contains(T.Local(r, d1, msg.Id), p => p.Tag == DiagTags.Marker);
            Fill? MarkerFill(string item)
            {
                var (d, o) = T.Insert(r, item, new Vec2(50, 50));
                return T.FillOf(T.Local(r, d, o.Id).First(p => p.Tag == DiagTags.Marker));
            }
            Assert.Equal(D.Paper, MarkerFill("diag.bpmn.intermediate.message")!.Color);
            Assert.Equal(D.Ink, MarkerFill("diag.bpmn.intermediate.message.throw")!.Color);
            Assert.Equal(D.Ink, MarkerFill("diag.bpmn.end.message")!.Color);
            // Every trigger draws a different marker.
            var sigs = BpmnEventType.Triggers.Where(t => t.Value is not ("none" or "terminate"))
                .Select(t => { var (d, o) = T.Insert(r, "diag.bpmn.intermediate", new Vec2(50, 50)); d = Edits.Replace(d, o.WithParam("trigger", t.Value)); return T.Sig(T.Local(r, d, o.Id)); }).ToList();
            Assert.Equal(sigs.Count, sigs.Distinct().Count());
            // A non-interrupting boundary event is dashed; terminate exists only at the end.
            var (d2, boundary) = T.Insert(r, "diag.bpmn.intermediate.boundary.timer", new Vec2(50, 50));
            Assert.Equal(DashStyle.Dash, T.StyleOf(T.Local(r, d2, boundary.Id).First(p => p.Tag == DiagTags.Outline)).Dash);
            Assert.Contains(r.Type("diag.bpmn.end")!.Parameters.Single(p => p.Key == "trigger").Options!, o => o.Value == "terminate");
            Assert.DoesNotContain(r.Type("diag.bpmn.start")!.Parameters.Single(p => p.Key == "trigger").Options!, o => o.Value == "terminate");
            Assert.Contains(r.TypeOf(msg).Actions(d1.Get(msg.Id)!, Lang.En), a => a.Id.StartsWith("diag.bpmn.trigger", StringComparison.Ordinal));
        }

        [Fact]
        public void BpmnDataObjectsStoresAndFlowsInsertAndDiffer()
        {
            var r = T.Reg();
            foreach (var id in new[] { "diag.bpmn.dataobject", "diag.bpmn.datastore", "diag.bpmn.dataobject.input", "diag.bpmn.dataobject.output", "diag.bpmn.dataobject.collection" })
            {
                var (d, o) = T.Insert(r, id, new Vec2(50, 50));
                Assert.NotEmpty(T.Render(r, d, o.Id));
                Assert.Equal(DiagCategories.Bpmn, r.TypeOf(o).Category);
            }
            string FlowSig(string item)
            {
                var (d, o) = T.Insert(r, item, new Vec2(50, 50));
                return T.Sig(T.Local(r, d, o.Id)) + string.Join(",", T.Local(r, d, o.Id).Select(p => T.StyleOf(p).Dash + "/" + T.StyleOf(p).ArrowStart + "/" + T.StyleOf(p).ArrowEnd));
            }
            var sigs = new[] { "diag.bpmn.flow" }.Concat(BpmnFlowType.Kinds.Skip(1).Select(k => "diag.bpmn.flow." + k.Value)).Select(FlowSig).ToList();
            Assert.Equal(sigs.Count, sigs.Distinct().Count());
            var (dm, m) = T.Insert(r, "diag.bpmn.flow.message", new Vec2(50, 50));
            var main = T.Local(r, dm, m.Id).First(p => p.Tag == DiagTags.Main);
            Assert.Equal(DashStyle.Dash, T.StyleOf(main).Dash);
            Assert.Equal(ArrowHead.TriangleOpen, T.StyleOf(main).ArrowEnd);
            Assert.Single(T.Local(r, dm, m.Id).OfType<EllipsePrim>());
        }

        [Fact]
        public void PseudoStatesDrawEveryKindDifferently()
        {
            var r = T.Reg();
            var sigs = new List<string>();
            foreach (var (value, _, _) in PseudoStateType.Kinds)
            {
                var item = value == "history" ? PseudoStateType.TypeId : PseudoStateType.TypeId + "." + value;
                var (d, o) = T.Insert(r, item, new Vec2(50, 50));
                Assert.Equal(DiagCategories.State, r.TypeOf(o).Category);
                sigs.Add(T.Sig(T.Local(r, d, o.Id)));
            }
            Assert.Equal(sigs.Count, sigs.Distinct().Count());
            var (dh, h) = T.Insert(r, "diag.state.pseudo.deepHistory", new Vec2(50, 50));
            Assert.Contains(T.All<TextPrim>(T.Local(r, dh, h.Id)), t => t.Text == "H*");
            Assert.Equal("diag.uml.relation.directed", r.TypeOf(h).ChainConnector(h));
        }

        [Fact]
        public void NewItemsHaveGermanNamesAndKeywords()
        {
            var r = T.Reg();
            var ids = new[] { "diag.net.node", "diag.net.link", "diag.net.node.duration", "diag.bpmn.flow.message", "diag.bpmn.end.terminate", "diag.state.pseudo.choice", "diag.bpmn.datastore" };
            foreach (var id in ids)
            {
                var item = r.Item(id)!;
                Assert.False(string.IsNullOrEmpty(item.Name.De), id);
                Assert.NotEqual(item.Name.En, item.Name.De);
            }
            Assert.Contains(r.Categories, c => c.Id == DiagCategories.Network && c.Name.De == "Netzplan (kritischer Pfad)");
            Assert.Contains("netzplan", r.Type(Cpm.NodeType)!.Keywords);
        }

        [Fact]
        public void QuickAddOnBpmnShapesUsesTheSequenceFlowWhileUmlShapesKeepTheirRelation()
        {
            var r = T.Reg();
            // BPMN flow objects chain with a sequence flow (kind sequence, filled arrowhead).
            foreach (var item in new[] { "diag.bpmn.task", "diag.bpmn.start", "diag.bpmn.intermediate", "diag.bpmn.gateway", "diag.bpmn.gateway.parallel", "diag.bpmn.task.subprocess" })
            {
                var (doc, o) = T.Insert(r, item, new Vec2(60, 60));
                Assert.Equal("diag.bpmn.flow", r.TypeOf(o).ChainConnector(o));
                var res = Flow.QuickAdd(r, doc, o.Id, FlowSide.Right);
                var line = res.Doc.Get(res.ConnectorId!)!;
                Assert.Equal("diag.bpmn.flow", line.Type);
                Assert.Equal("sequence", line.Str("kind", "sequence"));
                Assert.Equal(o.Id, line.Attach![0]!.ObjectId);
                Assert.Equal(res.ObjectId, line.Attach![1]!.ObjectId);
            }
            // Data objects and stores: a data association (sequence flows never touch data).
            foreach (var item in new[] { "diag.bpmn.dataobject", "diag.bpmn.datastore" })
            {
                var (doc, o) = T.Insert(r, item, new Vec2(60, 60));
                var res = Flow.QuickAdd(r, doc, o.Id, FlowSide.Right, itemId: "diag.bpmn.task");
                var line = res.Doc.Get(res.ConnectorId!)!;
                Assert.Equal("diag.bpmn.flow", line.Type);
                Assert.Equal("data", line.Str("kind", "sequence"));
            }
            // The pool still has no quick add; UML shapes keep the UML relations.
            var (_, pool) =T.Insert(r, "diag.bpmn.pool", new Vec2(60, 60));
            Assert.Null(r.TypeOf(pool).ChainConnector(pool));
            foreach (var (item, connector) in new[]
            {
                ("diag.activity.action", "diag.uml.relation.directed"), ("diag.state.state", "diag.uml.relation.directed"),
                ("diag.uml.class", "diag.uml.relation"), ("diag.uml.note", "diag.uml.relation.link"),
            })
            {
                var (doc, o) = T.Insert(r, item, new Vec2(60, 60));
                Assert.Equal(connector, r.TypeOf(o).ChainConnector(o));
                var res = Flow.QuickAdd(r, doc, o.Id, FlowSide.Right);
                Assert.Equal("diag.uml.relation", res.Doc.Get(res.ConnectorId!)!.Type);
            }
        }
    }
}
