using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Engine.Tests
{
    /// <summary>Corners and branches in wires, pipes and roads (<see cref="Polylines"/>).</summary>
    public class PolylineTests
    {
        private static readonly Registry Reg = ModuleCatalog.CreateDefault();

        private static SketchDocument Doc(params SketchObject[] objects)
        {
            var doc = SketchDocument.Create();
            return Edits.Add(doc, doc.Pages[0].Id, objects);
        }

        private static SketchObject Wire(Vec2 a, Vec2 b)
            => Reg.Create(Reg.Library.First(i => i.TypeId == "elec.wire"), Vec2.Zero).WithParam("route", "free") with { Points = new[] { a, b } };

        [Fact]
        public void WiresPipesAndRoadsTakeCornersDimensionsDoNot()
        {
            Assert.True(Polylines.CanInsert(Reg, Wire(new Vec2(0, 0), new Vec2(10, 0))));
            foreach (var t in new[] { "fluid.line", "map.road", "ann.polyline" })
                Assert.True(Reg.Type(t)!.InsertablePoints, t);
            Assert.False(Reg.Type("ann.dimension.linear")!.InsertablePoints);
        }

        [Fact]
        public void ACornerKeepsBranchesWhereTheyAre()
        {
            var main = Wire(new Vec2(20, 40), new Vec2(120, 40));
            var doc = Doc(main);
            var (withBranch, branchId) = Polylines.Branch(Reg, doc, main.Id, new Vec2(90, 40), new Vec2(90, 80));
            doc = withBranch;
            Assert.NotNull(branchId);
            Assert.Equal(new Vec2(90, 40), doc.ResolvePoints(doc.Get(branchId!)!)[0]);

            // A corner at x = 50 splits the rail; the branch stays at x = 90, now on the second segment.
            doc = Polylines.InsertCorner(Reg, doc, main.Id, 1, new Vec2(50, 40));
            Assert.Equal(3, doc.Get(main.Id)!.Points!.Count);
            Assert.Equal("rail1", doc.Get(branchId!)!.Attach![0]!.AnchorId);
            Assert.Equal(new Vec2(90, 40), doc.ResolvePoints(doc.Get(branchId!)!)[0]);

            // Pulling the corner down: the branch follows its segment.
            doc = Edits.SetPoint(doc, main.Id, 1, new Vec2(50, 20));
            doc = Attachments.Refresh(Reg, doc);
            var end = doc.ResolvePoints(doc.Get(branchId!)!)[0];
            Assert.True(end.Y < 40 && end.Y > 20, "branch end moved with the slanted segment");

            // Removing the corner again keeps it docked on the straight wire.
            doc = Polylines.RemoveCorner(Reg, doc, main.Id, 1);
            Assert.Equal(2, doc.Get(main.Id)!.Points!.Count);
            Assert.Equal("rail0", doc.Get(branchId!)!.Attach![0]!.AnchorId);
        }

        [Fact]
        public void ABranchWireDrawsAJunctionDot()
        {
            var main = Wire(new Vec2(20, 40), new Vec2(120, 40));
            var (doc, id) = Polylines.Branch(Reg, Doc(main), main.Id, new Vec2(70, 41), new Vec2(70, 80));
            var branch = doc.Get(id!)!;
            Assert.Equal("elec.wire", branch.Type);
            Assert.Equal(new Vec2(70, 40), doc.ResolvePoints(branch)[0]);
            Assert.Contains(Reg.Render(branch, new RenderContext(doc)), p => p.Tag == "junction");
            Assert.DoesNotContain(Reg.Render(doc.Get(main.Id)!, new RenderContext(doc)), p => p.Tag == "junction");
        }

        [Fact]
        public void TheSegmentHandleSitsOnTheRoutedPath()
        {
            var wire = Reg.Create(Reg.Library.First(i => i.TypeId == "elec.wire"), Vec2.Zero).WithParam("route", "orthogonal") with
            {
                Points = new[] { new Vec2(0, 0), new Vec2(40, 20) },
            };
            var doc = Doc(wire);
            var h = Polylines.SegmentHandle(Reg, doc, doc.Get(wire.Id)!, 0);
            var rails = Reg.PageAnchors(doc.Get(wire.Id)!, new RenderContext(doc)).Where(a => a.Role == AnchorRole.Rail).ToList();
            Assert.Contains(rails, r => Geo.ClosestOnSegment(h, r.P, r.Q).Point.DistanceTo(h) < 1e-6);
        }
    }
}
