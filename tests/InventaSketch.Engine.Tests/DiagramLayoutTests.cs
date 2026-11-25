using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Engine.Tests
{
    /// <summary>Layered layout of a connected diagram (<see cref="DiagramLayout"/>, arrange "layout:down"/"layout:right").</summary>
    public class DiagramLayoutTests
    {
        private static readonly Registry Reg = ModuleCatalog.CreateDefault();

        private static SketchObject Box(Vec2 at) => Reg.Create(Reg.Library.First(i => i.TypeId == "ann.flow.process"), at);

        private static SketchObject Arrow(SketchObject from, SketchObject to)
        {
            var a = Reg.Create(Reg.Library.First(i => i.TypeId == "ann.arrow.elbow"), Vec2.Zero);
            return a with
            {
                Points = new List<Vec2> { from.BoxPoint(0, 0.5), to.BoxPoint(0, -0.5) },
                // Docked on the steps' ports, as the "+" below a step does.
                Attach = new[] { new Attachment(from.Id, 0, 0.5) { AnchorId = "bottom" }, new Attachment(to.Id, 0, -0.5) { AnchorId = "top" } },
            };
        }

        /// <summary>A diamond A → B, A → C, B → D, C → D scattered over the page.</summary>
        private static (SketchDocument Doc, SketchObject A, SketchObject B, SketchObject C, SketchObject D) Diamond()
        {
            var a = Box(new Vec2(150, 120)); var b = Box(new Vec2(40, 30)); var c = Box(new Vec2(200, 40)); var d = Box(new Vec2(60, 140));
            var doc = SketchDocument.Create(PageSizes.A4Landscape);
            doc = Edits.Add(doc, doc.Pages[0].Id, a, b, c, d, Arrow(a, b), Arrow(a, c), Arrow(b, d), Arrow(c, d));
            return (doc, a, b, c, d);
        }

        [Fact]
        public void TheDiamondIsLaidOutInThreeLayers()
        {
            var (doc, a, b, c, d) = Diamond();
            var t = DiagramLayout.Layered(doc, new[] { a, b, c, d }, down: true)!;
            Assert.True(t[a.Id].Y < t[b.Id].Y && t[b.Id].Y < t[d.Id].Y, "A above B above D");
            Assert.Equal(t[b.Id].Y, t[c.Id].Y, 6);                     // B and C in one layer
            Assert.True(System.Math.Abs(t[b.Id].X - t[c.Id].X) > a.Size.W, "B and C side by side, not overlapping");
            Assert.Equal(t[a.Id].X, t[d.Id].X, 6);                     // A and D centred over the layer
            var right = DiagramLayout.Layered(doc, new[] { a, b, c, d }, down: false)!;
            Assert.True(right[a.Id].X < right[b.Id].X && right[b.Id].X < right[d.Id].X, "left to right");
            // Unconnected elements give no layout.
            Assert.Null(DiagramLayout.Layered(doc, new[] { b, c }, down: true));
        }

        [Fact]
        public void ALongChainStaysOnThePage()
        {
            // Five steps (54 mm each) in a chain, laid out left to right on a 16:9 slide (338.7 mm wide): the gaps shrink to fit.
            var steps = Enumerable.Range(0, 5).Select(i => Box(new Vec2(150 + 20 * i, 60 + 10 * (i % 3)))).ToList();
            var doc = SketchDocument.Create();
            doc = Edits.Add(doc, doc.Pages[0].Id, steps.ToArray());
            doc = Edits.Add(doc, doc.Pages[0].Id, steps.Zip(steps.Skip(1), (a, b) => Arrow(a, b)).ToArray());
            var t = DiagramLayout.Layered(doc, steps, down: false)!;
            var w = steps[0].Size.W;
            Assert.All(steps, s => Assert.InRange(t[s.Id].X, w / 2, doc.Pages[0].Size.W - w / 2));
            Assert.True(t[steps[4].Id].X > t[steps[0].Id].X, "still left to right");
        }

        [Fact]
        public void ACycleIsBrokenAndTheLayoutStillWorks()
        {
            var a = Box(new Vec2(40, 40)); var b = Box(new Vec2(120, 40)); var c = Box(new Vec2(80, 120));
            var doc = SketchDocument.Create(PageSizes.A4Landscape);
            doc = Edits.Add(doc, doc.Pages[0].Id, a, b, c, Arrow(a, b), Arrow(b, c), Arrow(c, a));
            var t = DiagramLayout.Layered(doc, new[] { a, b, c }, down: true)!;
            Assert.Equal(3, t.Select(kv => System.Math.Round(kv.Value.Y, 3)).Distinct().Count());
        }

        [Fact]
        public void TheMultiSelectionOffersTheLayoutAndConnectorsFollow()
        {
            var api = new EngineApi(ModuleCatalog.CreateDefault());
            var session = EngineApiTests.Call(api, "createSession", new { lang = "en" }).GetProperty("session").GetString()!;
            var (doc, a, b, c, d) = Diamond();
            var s = api.Sessions.Get(session);
            s.Apply("Diagram", x => Edits.Add(x, x.Pages[0].Id, doc.Objects.Values.ToArray()));
            var ids = new[] { a.Id, b.Id, c.Id, d.Id };
            var opts = EngineApiTests.Call(api, "arrangeOptions", new { session, ids }).EnumerateArray().Select(o => o.GetProperty("id").GetString()).ToList();
            Assert.Contains("layout:down", opts);
            Assert.Contains("layout:right", opts);
            EngineApiTests.Call(api, "arrange", new { session, ids, action = "layout:down" });
            var after = api.Sessions.Get(session).Doc;
            Assert.True(after.Get(a.Id)!.Transform.Y < after.Get(b.Id)!.Transform.Y);
            // The arrow A → B still runs from A to B.
            var arrow = after.Objects.Values.First(o => o.IsPointGeometry && o.Attach is { } at && at[0]?.ObjectId == a.Id && at[1]?.ObjectId == b.Id);
            var pts = after.ResolvePoints(arrow);
            Assert.True(pts[0].DistanceTo(after.Get(a.Id)!.BoxPoint(0, 0.5)) < 1e-3);
            Assert.True(pts[^1].DistanceTo(after.Get(b.Id)!.BoxPoint(0, -0.5)) < 1e-3);
            // Left to right: the connectors dock on the facing sides (right of the source, left of the target).
            EngineApiTests.Call(api, "arrange", new { session, ids, action = "layout:right" });
            var row = api.Sessions.Get(session).Doc;
            var ab = row.Objects.Values.First(o => o.IsPointGeometry && o.Attach is { } at2 && at2[0]?.ObjectId == a.Id && at2[1]?.ObjectId == b.Id);
            Assert.Equal(("right", 0.5, 0.0), (ab.Attach![0]!.AnchorId, ab.Attach[0]!.U, ab.Attach[0]!.V));
            Assert.Equal(("left", -0.5, 0.0), (ab.Attach![1]!.AnchorId, ab.Attach[1]!.U, ab.Attach[1]!.V));
            EngineApiTests.Call(api, "arrange", new { session, ids, action = "layout:down" });
            // Laid out once, it is not offered again.
            var again = EngineApiTests.Call(api, "arrangeOptions", new { session, ids }).EnumerateArray().Select(o => o.GetProperty("id").GetString()).ToList();
            Assert.DoesNotContain("layout:down", again);
        }
    }
}
