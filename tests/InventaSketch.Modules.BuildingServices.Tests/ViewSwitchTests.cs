using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.BuildingServices.Tests
{
    /// <summary>The installation stays the same when it switches between plan, section and schematic (<see cref="ServiceViews"/>).</summary>
    public class ViewSwitchTests
    {
        /// <summary>A small bathroom: radiator and washbasin on the wall, a WC, a cold pipe from the basin to a riser.</summary>
        internal static (Registry R, SketchDocument Doc, string Radiator, string Basin, string Wc, string Riser, string Pipe) Bathroom()
        {
            var r = Fx.Reg();
            var radiator = Fx.Item(r, "bs.heat.radiator", new Vec2(40, 30.6));
            var basin = Fx.Item(r, "bs.san.basin", new Vec2(60, 32.25));
            var wc = Fx.Item(r, "bs.san.wc", new Vec2(75, 32.7));
            var riser = Fx.Item(r, "bs.san.riser", new Vec2(90, 31));
            var (doc, page) = Fx.Doc(radiator, basin, wc, riser);
            var ctx = new RenderContext(doc);
            var cold = r.PageAnchors(basin, ctx).Single(a => a.Id == "cold").P;
            var top = r.PageAnchors(riser, ctx).Single(a => a.Id == "riser").P;
            var pipe = Fx.Item(r, "bs.pipe.cold").WithParam("height", 300.0) with { Points = new[] { cold, new Vec2(cold.X, 31), top } };
            doc = Edits.Add(doc, page, pipe);
            doc = Edits.AttachPoint(doc, pipe.Id, 0, basin.Id, cold, "cold");
            doc = Edits.AttachPoint(doc, pipe.Id, 2, riser.Id, top, "riser");
            return (r, doc, radiator.Id, basin.Id, wc.Id, riser.Id, pipe.Id);
        }

        private static string[] All((Registry R, SketchDocument Doc, string Radiator, string Basin, string Wc, string Riser, string Pipe) b)
            => new[] { b.Radiator, b.Basin, b.Wc, b.Riser, b.Pipe };

        [Fact]
        public void PlanToSectionKeepsXAndPlacesElementsAtTheirMountingHeight()
        {
            var b = Bathroom();
            var floor = 40.0;
            var doc = ServiceViews.Switch(b.R, b.Doc, All(b), "section", floor);
            var radiator = doc.Get(b.Radiator)!;
            Assert.Equal("section", radiator.View);
            Assert.Equal(40, radiator.Transform.X, 6);
            Assert.Equal(floor - 1.5, radiator.Transform.Y + radiator.Size.H / 2, 6); // bottom edge 150 mm above the floor at 1:100
            Assert.Equal(10, radiator.Size.W, 6);
            Assert.Equal(6, radiator.Size.H, 6);
            var wc = doc.Get(b.Wc)!;
            Assert.Equal(floor, wc.Transform.Y + wc.Size.H / 2, 6); // floor-standing
            var basin = doc.Get(b.Basin)!;
            Assert.Equal(floor - 5, basin.Transform.Y + basin.Size.H / 2, 6);
            var riser = doc.Get(b.Riser)!;
            Assert.Equal(floor - 28, riser.Transform.Y - riser.Size.H / 2, 6); // a full storey high
        }

        [Fact]
        public void TheFloorLineDefaultsToTheLowestPlanEdge()
        {
            var b = Bathroom();
            var bottom = All(b).Select(b.Doc.Get).Where(o => !o!.IsPointGeometry).Max(o => o!.Bounds().Bottom);
            var doc = ServiceViews.Switch(b.R, b.Doc, All(b), "section");
            var wc = doc.Get(b.Wc)!;
            Assert.Equal(bottom, wc.Transform.Y + wc.Size.H / 2, 6);
            Assert.Equal(bottom.ToString("R", System.Globalization.CultureInfo.InvariantCulture), wc.Meta![ServiceViews.MetaFloorY]);
        }

        [Fact]
        public void DockedPipesFollowThePortsIntoTheSection()
        {
            var b = Bathroom();
            var doc = ServiceViews.Switch(b.R, b.Doc, All(b), "section", 40);
            var ctx = new RenderContext(doc);
            var pipe = doc.Get(b.Pipe)!;
            Assert.Equal("section", pipe.View);
            var pts = doc.ResolvePoints(pipe);
            var cold = b.R.PageAnchors(doc.Get(b.Basin)!, ctx).Single(a => a.Id == "cold").P;
            Assert.Equal(cold.X, pts[0].X, 6);
            Assert.Equal(cold.Y, pts[0].Y, 6);
            // The free corner keeps its x and goes to the pipe's height (300 mm above the floor line).
            Assert.Equal(37, pts[1].Y, 6);
            // The end on the riser (a port only in plan) slides onto the riser's rail at the pipe's height.
            var riser = doc.Get(b.Riser)!;
            Assert.Equal(riser.Transform.X, pts[2].X, 6);
            Assert.Equal(37, pts[2].Y, 6);
            Assert.Equal("riser", pipe.Attach![2]!.AnchorId);
        }

        [Fact]
        public void SectionToSchematicKeepsXAndTheBottomEdge()
        {
            var b = Bathroom();
            var section = ServiceViews.Switch(b.R, b.Doc, All(b), "section", 40);
            var schematic = ServiceViews.Switch(b.R, section, All(b), "schematic");
            foreach (var id in new[] { b.Radiator, b.Basin, b.Wc })
            {
                SketchObject s = section.Get(id)!, m = schematic.Get(id)!;
                Assert.Equal("schematic", m.View);
                Assert.Equal(s.Transform.X, m.Transform.X, 6);
                Assert.Equal(s.Transform.Y + s.Size.H / 2, m.Transform.Y + m.Size.H / 2, 6);
            }
            Assert.Equal("schematic", schematic.Get(b.Pipe)!.View);
        }

        [Fact]
        public void BackToPlanRestoresTheLayout()
        {
            var b = Bathroom();
            var section = ServiceViews.Switch(b.R, b.Doc, All(b), "section", 40);
            var plan = ServiceViews.Switch(b.R, section, All(b), "plan");
            foreach (var id in All(b))
            {
                SketchObject before = b.Doc.Get(id)!, after = plan.Get(id)!;
                Assert.Equal("plan", after.View);
                if (before.IsPointGeometry)
                {
                    var p0 = b.Doc.ResolvePoints(before);
                    var p1 = plan.ResolvePoints(after);
                    for (var i = 0; i < p0.Count; i++) Assert.True(p0[i].DistanceTo(p1[i]) < 1e-6, id + " point " + i);
                    continue;
                }
                Assert.Equal(before.Transform.X, after.Transform.X, 6);
                Assert.Equal(before.Transform.Y, after.Transform.Y, 6);
                Assert.Equal(before.Size.W, after.Size.W, 6);
                Assert.Equal(before.Size.H, after.Size.H, 6);
                Assert.Equal(before.Num("height", -1), after.Num("height", -2), 6);
            }
        }

        [Fact]
        public void MovingAnElementUpInTheSectionChangesItsMountingHeight()
        {
            var b = Bathroom();
            var section = ServiceViews.Switch(b.R, b.Doc, All(b), "section", 40);
            section = Edits.Move(section, new[] { b.Radiator }, new Vec2(3, -2)); // 200 mm higher, 300 mm to the right
            var plan = ServiceViews.Switch(b.R, section, All(b), "plan");
            var radiator = plan.Get(b.Radiator)!;
            Assert.Equal(350, radiator.Num("height", 0), 6);
            Assert.Equal(43, radiator.Transform.X, 6);
            Assert.Equal(30.6, radiator.Transform.Y, 6);
        }

        [Fact]
        public void RotatedPlanElementsStandUprightInTheSectionAndTurnBack()
        {
            var b = Bathroom();
            var doc = Edits.Update(b.Doc, new[] { b.Radiator }, o => o with { Transform = o.Transform with { Rotation = 90 } });
            var section = ServiceViews.Switch(b.R, doc, new[] { b.Radiator }, "section", 40);
            Assert.Equal(0, section.Get(b.Radiator)!.Transform.Rotation);
            var plan = ServiceViews.Switch(b.R, section, new[] { b.Radiator }, "plan");
            Assert.Equal(90, plan.Get(b.Radiator)!.Transform.Rotation);
        }

        [Fact]
        public void UnknownViewsAndForeignObjectsAreIgnored()
        {
            var b = Bathroom();
            Assert.Same(b.Doc, ServiceViews.Switch(b.R, b.Doc, All(b), "elevation"));
            Assert.Same(b.Doc, ServiceViews.Switch(b.R, b.Doc, All(b), "plan"));
        }

        [Fact]
        public void HeightMappingIsInvertible()
        {
            var y = ServiceViews.BottomY(100, 850, 100);
            Assert.Equal(91.5, y, 9);
            Assert.Equal(850, ServiceViews.HeightAt(100, y, 100), 9);
            Assert.Equal(83, ServiceViews.BottomY(100, 850, 50), 9);
        }
    }

    public class DocumentTests
    {
        [Fact]
        public void JsonRoundTripKeepsEveryElementInEveryView()
        {
            var r = Fx.Reg();
            var doc = SketchDocument.Create();
            var x = 0.0;
            foreach (var item in r.Library)
            {
                var o = r.Create(item, new Vec2(x += 5, 50));
                doc = Edits.Add(doc, doc.Pages[0].Id, o);
            }
            var b = ViewSwitchTests.Bathroom();
            var switched = ServiceViews.Switch(b.R, b.Doc, new[] { b.Radiator, b.Basin, b.Wc, b.Riser, b.Pipe }, "schematic", 40);
            foreach (var d in new[] { doc, switched })
            {
                var json = DocumentJson.Serialize(d);
                var back = DocumentJson.Deserialize(json);
                Assert.Equal(json, DocumentJson.Serialize(back));
                foreach (var o in d.Objects.Values)
                {
                    var o2 = back.Get(o.Id)!;
                    Assert.Equal(o.View, o2.View);
                    Assert.Equal(o.Params.Count, o2.Params.Count);
                    var ctx1 = new RenderContext(d);
                    var ctx2 = new RenderContext(back);
                    Assert.Equal(r.Render(o, ctx1).Count, r.Render(o2, ctx2).Count);
                }
            }
        }
    }
}
