using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Annotation.Tests
{
    /// <summary>Round 7 (2026-11-26): automatic exit labels of decisions and step numbering along the flow (both off by default).</summary>
    public class FlowNumberingTests
    {
        /// <summary>The step number drawn at a flow box ("" if none).</summary>
        private static string Step(Registry r, SketchDocument doc, string id)
            => T.All<TextPrim>(T.Render(r, doc, id)).Where(t => t.Tag == AnnTags.Numeral).Select(t => t.Text).SingleOrDefault() ?? "";

        private static string Label(Registry r, SketchDocument doc, string id, Lang lang = Lang.En)
            => T.All<TextPrim>(r.Render(doc.Get(id)!, new RenderContext(doc, lang))).Select(t => t.Text).SingleOrDefault() ?? "";

        private static (SketchDocument Doc, string Id) Connect(Registry r, SketchDocument doc, string from, string to, string fromSide = "bottom", string toSide = "top")
        {
            var a = doc.Get(from)!; var b = doc.Get(to)!;
            var (d, c) = T.Insert(r, "ann.arrow.elbow", Vec2.Zero, doc);
            (double U, double V) At(string side) => side switch { "right" => (0.5, 0), "left" => (-0.5, 0), "top" => (0, -0.5), _ => (0, 0.5) };
            var (ua, va) = At(fromSide); var (ub, vb) = At(toSide);
            d = Edits.Update(d, new[] { c.Id }, x => x with
            {
                Points = new List<Vec2> { a.BoxPoint(ua, va), b.BoxPoint(ub, vb) },
                Attach = new[] { new Attachment(a.Id, ua, va) { AnchorId = fromSide }, new Attachment(b.Id, ub, vb) { AnchorId = toSide } },
            });
            return (d, c.Id);
        }

        /// <summary>Start → A → B → decision D; yes → C → end, no → back to A.</summary>
        private static (SketchDocument Doc, Dictionary<string, string> Ids) Method(Registry r)
        {
            var (doc, start) = T.Insert(r, "ann.flow.start", new Vec2(100, 20));
            var ids = new Dictionary<string, string> { ["start"] = start.Id };
            var a = Flow.QuickAdd(r, doc, start.Id, FlowSide.Bottom, itemId: "ann.flow.process");
            ids["A"] = a.ObjectId;
            var b = Flow.QuickAdd(r, a.Doc, a.ObjectId, FlowSide.Bottom, itemId: "ann.flow.process");
            ids["B"] = b.ObjectId;
            var d = Flow.QuickAdd(r, b.Doc, b.ObjectId, FlowSide.Bottom, itemId: "ann.flow.decision");
            ids["D"] = d.ObjectId;
            var c = Flow.QuickAdd(r, d.Doc, d.ObjectId, FlowSide.Bottom, itemId: "ann.flow.process");
            ids["C"] = c.ObjectId;
            var end = Flow.QuickAdd(r, c.Doc, c.ObjectId, FlowSide.Bottom, itemId: "ann.flow.end");
            ids["end"] = end.ObjectId;
            var (doc2, loop) = Connect(r, end.Doc, d.ObjectId, a.ObjectId, "left", "left");
            ids["loop"] = loop;
            return (doc2, ids);
        }

        [Fact]
        public void StepNumbersAreOffByDefaultAndFollowTheFlowWhenSwitchedOn()
        {
            var r = T.Reg();
            var (doc, ids) = Method(r);
            Assert.All(new[] { "start", "A", "B", "D", "C", "end" }, k => Assert.Equal("", Step(r, doc, ids[k])));

            doc = Edits.Update(doc, new[] { ids["start"] }, x => x.WithParam(FlowNumbering.NumberParam, true));
            // Start and end are passed through; the loop back from the decision does not renumber A.
            Assert.Equal("", Step(r, doc, ids["start"]));
            Assert.Equal("S100", Step(r, doc, ids["A"]));
            Assert.Equal("S110", Step(r, doc, ids["B"]));
            Assert.Equal("S120", Step(r, doc, ids["D"]));
            Assert.Equal("S130", Step(r, doc, ids["C"]));
            Assert.Equal("", Step(r, doc, ids["end"]));

            // Prefix, first number and increment; an entered step number wins.
            doc = Edits.Update(doc, new[] { ids["start"] }, x => x.WithParam(FlowNumbering.PrefixParam, "").WithParam(FlowNumbering.FirstParam, 1).WithParam(FlowNumbering.IncrementParam, 1));
            doc = Edits.Update(doc, new[] { ids["B"] }, x => x.WithParam("step", "S2a"));
            Assert.Equal("1", Step(r, doc, ids["A"]));
            Assert.Equal("S2a", Step(r, doc, ids["B"]));
            Assert.Equal("3", Step(r, doc, ids["D"]));
            Assert.Equal("4", Step(r, doc, ids["C"]));
        }

        [Fact]
        public void BranchesAreNumberedBeforeTheStepWhereTheyMeet()
        {
            // D: yes → X, no → Y; both → Z.
            var r = T.Reg();
            var (doc, d) = T.Insert(r, "ann.flow.decision", new Vec2(100, 30));
            doc = Edits.Update(doc, new[] { d.Id }, x => x.WithParam(FlowNumbering.NumberParam, true));
            var x1 = Flow.QuickAdd(r, doc, d.Id, FlowSide.Bottom, itemId: "ann.flow.process");
            var y1 = Flow.QuickAdd(r, x1.Doc, d.Id, FlowSide.Right, itemId: "ann.flow.process");
            var z1 = Flow.QuickAdd(r, y1.Doc, x1.ObjectId, FlowSide.Bottom, itemId: "ann.flow.process");
            var (doc2, _) = Connect(r, z1.Doc, y1.ObjectId, z1.ObjectId, "bottom", "right");
            Assert.Equal("S100", Step(r, doc2, d.Id));
            Assert.Equal("S110", Step(r, doc2, x1.ObjectId));
            Assert.Equal("S120", Step(r, doc2, y1.ObjectId));
            Assert.Equal("S130", Step(r, doc2, z1.ObjectId));
        }

        [Fact]
        public void NumberingIsOfferedOnTheFirstElementAndRedrawsTheFlow()
        {
            var r = T.Reg();
            var (doc, ids) = Method(r);
            var ctx = new RenderContext(doc);
            Assert.Contains(r.Actions(doc.Get(ids["start"])!, ctx), a => a.Id == "ann.flow.number");
            Assert.DoesNotContain(r.Actions(doc.Get(ids["B"])!, ctx), a => a.Id == "ann.flow.number");
            var on = ContextActions.Run(r, doc, new[] { ids["start"] }, "ann.flow.number", null, Lang.En);
            Assert.True(on.Get(ids["start"])!.Flag(FlowNumbering.NumberParam, false));
            // Switching it on, or changing a connector of the flow, redraws every numbered step.
            var type = r.Type("ann.flow.terminator")!;
            Assert.Equal(new[] { ids["A"], ids["B"], ids["C"], ids["D"] }.OrderBy(x => x), type.Dependents(on.Get(ids["start"])!, new RenderContext(on)).OrderBy(x => x));
            var loop = on.Get(ids["loop"])!;
            var deps = r.Type(loop.Type)!.Dependents(loop, new RenderContext(on)).ToList();
            Assert.Contains(ids["A"], deps);
            Assert.Contains(ids["C"], deps);
        }

        [Fact]
        public void DrawnExitsOfADecisionAreLabelledAutomaticallyWhenSwitchedOn()
        {
            var r = T.Reg();
            var (doc, d) = T.Insert(r, "ann.flow.decision", new Vec2(100, 40));
            var (doc1, p) = T.Insert(r, "ann.flow.process", new Vec2(100, 120), doc);
            var (doc2, q) = T.Insert(r, "ann.flow.process", new Vec2(200, 40), doc1);
            var (doc3, s) = T.Insert(r, "ann.flow.process", new Vec2(20, 40), doc2);
            var (doc4, c1) = Connect(r, doc3, d.Id, p.Id);
            var (doc5, c2) = Connect(r, doc4, d.Id, q.Id, "right", "left");
            var (doc6, c3) = Connect(r, doc5, d.Id, s.Id, "left", "right");
            // Off (default): drawn connectors stay unlabelled.
            Assert.Equal("", Label(r, doc6, c1));
            Assert.Equal("", Label(r, doc6, c2));

            var on = Edits.Update(doc6, new[] { d.Id }, x => x.WithParam(FlowNumbering.AutoExitsParam, true));
            Assert.Equal("yes", Label(r, on, c1));
            Assert.Equal("nein", Label(r, on, c2, Lang.De));
            Assert.Equal("", Label(r, on, c3));
            // Numbers for any count of exits; an own text wins.
            var numbers = Edits.Update(on, new[] { d.Id }, x => x.WithParam(FlowNumbering.ExitStyleParam, "numbers"));
            Assert.Equal(new[] { "1", "2", "3" }, new[] { c1, c2, c3 }.Select(c => Label(r, numbers, c)));
            var own = Edits.Update(numbers, new[] { c2 }, x => x with { Text = "else" });
            Assert.Equal("else", Label(r, own, c2));
            // The decision redraws its exits; an exit redraws its siblings.
            Assert.Contains(c3, r.Type("ann.flow.decision")!.Dependents(numbers.Get(d.Id)!, new RenderContext(numbers)));
            Assert.Contains(c1, r.Type("ann.arrow.elbow")!.Dependents(numbers.Get(c3)!, new RenderContext(numbers)));
            // Only on decisions: actions offered there.
            Assert.Contains(r.Actions(numbers.Get(d.Id)!, new RenderContext(numbers)), a => a.Id == "ann.flow.exits.auto");
            Assert.DoesNotContain(r.Actions(numbers.Get(p.Id)!, new RenderContext(numbers)), a => a.Id == "ann.flow.exits.auto");
        }

        [Fact]
        public void QuickAddNumbersTheExitsOfANumberedDecision()
        {
            var r = T.Reg();
            var (doc, d) = T.Insert(r, "ann.flow.decision", new Vec2(100, 60));
            doc = Edits.Update(doc, new[] { d.Id }, x => x.WithParam(FlowNumbering.ExitStyleParam, "numbers"));
            var first = Flow.QuickAdd(r, doc, d.Id, FlowSide.Bottom);
            var second = Flow.QuickAdd(r, first.Doc, d.Id, FlowSide.Right);
            var third = Flow.QuickAdd(r, second.Doc, d.Id, FlowSide.Left);
            Assert.Equal(new[] { "1", "2", "3" }, new[] { first, second, third }.Select(x => third.Doc.Get(x.ConnectorId!)!.Str("exit", "")));
            Assert.Equal("", FlowBoxType.ExitLabel(d, 2));
            Assert.Equal("no", FlowBoxType.ExitLabel(d, 1));
        }
    }
}
