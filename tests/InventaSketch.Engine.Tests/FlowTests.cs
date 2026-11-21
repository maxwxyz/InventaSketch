using System.Linq;
using System.Text.Json;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Engine.Tests
{
    /// <summary>Quick add ("+" arrows) and "next element": flowcharts, ER and UML grow with connected elements.</summary>
    public class FlowTests
    {
        private static readonly Registry Reg = ModuleCatalog.CreateDefault();

        private static SketchDocument Doc(params SketchObject[] objects)
        {
            var doc = SketchDocument.Create();
            return Edits.Add(doc, doc.Pages[0].Id, objects);
        }

        private static SketchObject Item(string typeId, Vec2 at) => Reg.Create(Reg.Library.First(i => i.TypeId == typeId), at);

        [Fact]
        public void ChainableTypes()
        {
            Assert.True(Flow.CanChain(Reg, Item("ann.flow.process", Vec2.Zero)));
            Assert.True(Flow.CanChain(Reg, Item("diag.er.entity", Vec2.Zero)));
            Assert.True(Flow.CanChain(Reg, Item("basic.rect", Vec2.Zero)));
            Assert.False(Flow.CanChain(Reg, Item("mech.gear.spur", Vec2.Zero)));
            Assert.False(Flow.CanChain(Reg, Item("diag.activity.swimlane", Vec2.Zero)));
            Assert.False(Flow.CanChain(Reg, Item("ann.arrow", Vec2.Zero)));
        }

        [Fact]
        public void QuickAddPlacesAConnectedCopyAndStepsPastExistingOnes()
        {
            var step = Item("ann.flow.process", new Vec2(60, 40)) with { Text = "Start" };
            var doc = Doc(step);
            var first = Flow.QuickAdd(Reg, doc, step.Id, FlowSide.Bottom);
            var copy = first.Doc.Get(first.ObjectId)!;
            Assert.Equal(step.Transform.X, copy.Transform.X, 6);
            Assert.True(copy.Bounds().Y > step.Bounds().Bottom, "below the source");
            Assert.Equal("Start", copy.Text);
            var line = first.Doc.Get(first.ConnectorId!)!;
            Assert.Equal("ann.arrow.elbow", line.Type);
            Assert.Equal(step.Id, line.Attach![0]!.ObjectId);
            Assert.Equal(copy.Id, line.Attach![1]!.ObjectId);
            var pts = first.Doc.ResolvePoints(line);
            Assert.Equal(step.Bounds().Bottom, pts[0].Y, 6);
            Assert.Equal(copy.Bounds().Y, pts[1].Y, 6);

            // A second element on the same side goes past the first one instead of on top of it.
            var second = Flow.QuickAdd(Reg, first.Doc, step.Id, FlowSide.Bottom);
            Assert.True(second.Doc.Get(second.ObjectId)!.Bounds().Y > copy.Bounds().Bottom);

            // Moving the source drags the connector end along (attached).
            var moved = Attachments.Refresh(Reg, Edits.Move(first.Doc, new[] { step.Id }, new Vec2(10, 0)));
            Assert.Equal(step.Transform.X + 10, moved.ResolvePoints(moved.Get(first.ConnectorId!)!)[0].X, 6);
        }

        [Fact]
        public void QuickAddUsesFacingPortsAndTheKindsConnector()
        {
            var entity = Item("diag.er.entity", new Vec2(50, 50));
            var res = Flow.QuickAdd(Reg, Doc(entity), entity.Id, FlowSide.Right);
            var line = res.Doc.Get(res.ConnectorId!)!;
            Assert.Equal("diag.er.connector", line.Type);
            Assert.NotNull(line.Attach![0]!.AnchorId);
            Assert.NotNull(line.Attach![1]!.AnchorId);
            var pts = res.Doc.ResolvePoints(line);
            Assert.Equal(entity.Bounds().Right, pts[0].X, 6);
            Assert.True(pts[1].X > pts[0].X);
        }

        [Fact]
        public void ReplaceKeepsTheConnection()
        {
            var step = Item("ann.flow.process", new Vec2(60, 40));
            var add = Flow.QuickAdd(Reg, Doc(step), step.Id, FlowSide.Bottom);
            var decision = Reg.Library.First(i => i.TypeId == "ann.flow.decision");
            Assert.Contains(Flow.NextItems(Reg, add.Doc.Get(add.ObjectId)!), i => i.TypeId == "ann.flow.decision");
            var doc = Flow.Replace(Reg, add.Doc, add.ObjectId, decision.Id);
            var replaced = doc.Get(add.ObjectId)!;
            Assert.Equal("ann.flow.decision", replaced.Type);
            var line = doc.Get(add.ConnectorId!)!;
            Assert.Equal(add.ObjectId, line.Attach![1]!.ObjectId);
            var end = doc.ResolvePoints(line)[1];
            Assert.True(end.DistanceTo(new Vec2(replaced.Transform.X, replaced.Bounds().Y)) < 0.5, "end on the top of the decision: " + end);
            // The top edge (facing the step above) stays; the taller decision grows downwards.
            Assert.Equal(add.Doc.Get(add.ObjectId)!.Bounds().Y, replaced.Bounds().Y, 6);
        }

        [Fact]
        public void EngineQuickAddSelectsTheNewElementInOneUndoStep()
        {
            var api = new EngineApi(Reg);
            var s = EngineApiTests.Call(api, "createSession", new { lang = "de" }).GetProperty("session").GetString()!;
            var id = EngineApiTests.Insert(api, s, Reg.Library.First(i => i.TypeId == "ann.flow.process").Id, 60, 40);
            var st = EngineApiTests.Call(api, "state", new { session = s });
            Assert.True(EngineApiTests.Obj(st, id).GetProperty("chain").GetBoolean());
            st = EngineApiTests.Call(api, "quickAdd", new { session = s, id, side = "right" });
            Assert.Equal(3, st.GetProperty("objects").GetArrayLength());
            var added = st.GetProperty("selection")[0].GetString()!;
            Assert.NotEqual(id, added);
            var next = EngineApiTests.Call(api, "nextItems", new { session = s, id = added });
            Assert.True(next.GetArrayLength() > 2);
            var item = next.EnumerateArray().First(e => e.GetProperty("typeId").GetString() == "ann.flow.decision").GetProperty("id").GetString();
            st = EngineApiTests.Call(api, "replaceItem", new { session = s, id = added, itemId = item });
            Assert.Equal("ann.flow.decision", EngineApiTests.Obj(st, added).GetProperty("type").GetString());
            st = EngineApiTests.Call(api, "undo", new { session = s });
            st = EngineApiTests.Call(api, "undo", new { session = s });
            Assert.Equal(1, st.GetProperty("objects").GetArrayLength());
        }
    }
}
