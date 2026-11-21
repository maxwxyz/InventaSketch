using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Engine.Tests
{
    /// <summary>One line language for wires, pipes, roads, walls and polylines: extend, split, join (<see cref="Polylines"/>).</summary>
    public class LineLanguageTests
    {
        private static readonly Registry Reg = ModuleCatalog.CreateDefault();

        private static SketchDocument Doc(params SketchObject[] objects)
        {
            var doc = SketchDocument.Create();
            return Edits.Add(doc, doc.Pages[0].Id, objects);
        }

        private static SketchObject Line(string typeId, params Vec2[] pts)
        {
            var o = Reg.Create(Reg.Library.First(i => i.TypeId == typeId), Vec2.Zero) with { Points = pts };
            return typeId == "elec.wire" ? o.WithParam("route", "free") : o;
        }

        [Theory]
        [InlineData("elec.wire")]
        [InlineData("fluid.line")]
        [InlineData("map.road")]
        [InlineData("arch.wall")]
        [InlineData("ann.polyline")]
        [InlineData("auto.trajectory")]
        public void OpenLinesCanBeExtendedAtBothEnds(string typeId)
        {
            var line = Line(typeId, new Vec2(20, 40), new Vec2(80, 40));
            var doc = Doc(line);
            Assert.True(Polylines.CanExtend(Reg, line), typeId);
            var (d1, i1) = Polylines.Extend(Reg, doc, line.Id, false, new Vec2(80, 90));
            Assert.Equal(2, i1);
            var (d2, i2) = Polylines.Extend(Reg, d1, line.Id, true, new Vec2(0, 40));
            Assert.Equal(0, i2);
            Assert.Equal(new[] { new Vec2(0, 40), new Vec2(20, 40), new Vec2(80, 40), new Vec2(80, 90) }, d2.ResolvePoints(d2.Get(line.Id)!));
        }

        [Theory]
        [InlineData("elec.bus")]
        [InlineData("elec.house.cable")]
        [InlineData("elec.pcb.track")]
        [InlineData("map.river")]
        public void CablesBusesAndTracksTakeCorners(string typeId)
        {
            var line = Line(typeId, new Vec2(20, 40), new Vec2(80, 40));
            Assert.True(Polylines.CanInsert(Reg, line), typeId);
            Assert.True(Polylines.CanExtend(Reg, line), typeId);
        }

        [Fact]
        public void AnArrowDrawnOnBecomesAPolylineWithItsHead()
        {
            var arrow = Line("ann.arrow", new Vec2(20, 40), new Vec2(80, 40));
            var doc = Doc(arrow);
            Assert.False(Polylines.CanInsert(Reg, arrow));
            Assert.True(Polylines.CanExtend(Reg, arrow));
            var (next, _) = Polylines.Extend(Reg, doc, arrow.Id, false, new Vec2(80, 90));
            var o = next.Get(arrow.Id)!;
            Assert.Equal("ann.polyline", o.Type);
            Assert.Equal(3, o.Points!.Count);
            Assert.Equal("triangle", o.Str("headEnd", "none"));
            Assert.NotNull(((PathPrim)Reg.Render(o, new RenderContext(next)).Single()).Style.ArrowEnd);
        }

        [Theory]
        [InlineData("arch.room")]
        [InlineData("map.region")]
        [InlineData("ann.hatch.polygon")]
        public void ClosedOutlinesHaveNoEndsButACornerOnTheClosingSegment(string typeId)
        {
            var o = Line(typeId, new Vec2(0, 0), new Vec2(40, 0), new Vec2(40, 30), new Vec2(0, 30));
            var doc = Doc(o);
            Assert.False(Polylines.CanExtend(Reg, o), typeId);
            Assert.True(Polylines.CanInsert(Reg, o), typeId);
            Assert.Equal(4, Polylines.SegmentCount(Reg, doc, o));
            var mid = Polylines.SegmentHandle(Reg, doc, o, 3);
            Assert.Equal(new Vec2(0, 15), mid);
            var next = Polylines.InsertCorner(Reg, doc, o.Id, 4, mid);
            Assert.Equal(5, next.Get(o.Id)!.Points!.Count);
        }

        [Fact]
        public void ExtendingADockedEndKeepsTheNewEndDockedAndTheCornerFree()
        {
            var r1 = Reg.Create(Reg.Library.First(i => i.TypeId == "elec.resistor"), new Vec2(100, 40));
            var probe = Doc(r1);
            var pin = Reg.PageAnchors(r1, new RenderContext(probe)).First(x => x.Kind == AnchorKind.Point && x.Role == AnchorRole.Port);
            var wire = Line("elec.wire", new Vec2(20, 40), new Vec2(60, 40));
            var doc = Edits.Add(probe, probe.Pages[0].Id, wire);
            var (next, index) = Polylines.Extend(Reg, doc, wire.Id, false, pin.P, Attachments.For(r1, pin.P, pin));
            var w = next.Get(wire.Id)!;
            Assert.Equal(2, index);
            Assert.Null(w.Attach![1]);
            Assert.Equal(r1.Id, w.Attach![2]!.ObjectId);
        }

        [Fact]
        public void SplittingAWireKeepsItsBranchOnTheRightPart()
        {
            var main = Line("elec.wire", new Vec2(0, 40), new Vec2(50, 40), new Vec2(100, 40));
            var (doc, branch) = Polylines.Branch(Reg, Doc(main), main.Id, new Vec2(80, 40), new Vec2(80, 80));
            var (split, newId) = Polylines.Split(Reg, doc, main.Id, 1);
            Assert.NotNull(newId);
            Assert.Equal(new[] { new Vec2(0, 40), new Vec2(50, 40) }, split.Get(main.Id)!.Points);
            Assert.Equal(new[] { new Vec2(50, 40), new Vec2(100, 40) }, split.Get(newId!)!.Points);
            Assert.Equal(newId, split.Get(branch!)!.Attach![0]!.ObjectId);
            Assert.Equal(new Vec2(80, 40), split.ResolvePoints(split.Get(branch!)!)[0]);
        }

        [Fact]
        public void SplittingAClosedWallOpensIt()
        {
            var wall = Line("arch.wall", new Vec2(0, 0), new Vec2(40, 0), new Vec2(40, 30), new Vec2(0, 30)).WithParam("closed", true);
            var (doc, newId) = Polylines.Split(Reg, Doc(wall), wall.Id, 2);
            Assert.Null(newId);
            var w = doc.Get(wall.Id)!;
            Assert.False(w.Flag("closed", true));
            Assert.Equal(new[] { new Vec2(40, 30), new Vec2(0, 30), new Vec2(0, 0), new Vec2(40, 0), new Vec2(40, 30) }, w.Points);
        }

        [Fact]
        public void JoiningTwoPipesMakesOneAndKeepsBranches()
        {
            var a = Line("fluid.line", new Vec2(0, 40), new Vec2(50, 40));
            var b = Line("fluid.line", new Vec2(100, 40), new Vec2(50, 40));
            var (doc, branch) = Polylines.Branch(Reg, Doc(a, b), b.Id, new Vec2(80, 40), new Vec2(80, 80));
            var joined = Polylines.Join(Reg, doc, a.Id, false, b.Id, false);
            Assert.Null(joined.Get(b.Id));
            Assert.Equal(new[] { new Vec2(0, 40), new Vec2(50, 40), new Vec2(100, 40) }, joined.Get(a.Id)!.Points);
            Assert.Equal(a.Id, joined.Get(branch!)!.Attach![0]!.ObjectId);
            Assert.Equal(new Vec2(80, 40), joined.ResolvePoints(joined.Get(branch!)!)[0]);
            // Different kinds do not join.
            var wire = Line("elec.wire", new Vec2(0, 0), new Vec2(10, 0));
            var mixed = Doc(a, wire);
            Assert.Same(mixed, Polylines.Join(Reg, mixed, a.Id, false, wire.Id, true));
        }

        [Fact]
        public void ABranchWallDocksOntoTheWallAndFollowsIt()
        {
            var wall = Line("arch.wall", new Vec2(0, 0), new Vec2(100, 0));
            var doc = Doc(wall);
            Assert.True(Polylines.CanBranch(Reg, doc, wall));
            var (withBranch, id) = Polylines.Branch(Reg, doc, wall.Id, new Vec2(40, 0), new Vec2(40, 50));
            Assert.Equal("arch.wall", withBranch.Get(id!)!.Type);
            var moved = Attachments.Refresh(Reg, Edits.Move(withBranch, new[] { wall.Id }, new Vec2(0, 10)));
            Assert.Equal(new Vec2(40, 10), moved.ResolvePoints(moved.Get(id!)!)[0]);
        }
    }
}
