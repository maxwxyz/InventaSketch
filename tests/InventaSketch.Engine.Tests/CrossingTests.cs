using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Engine.Tests
{
    /// <summary>Crossing wires and pipes: the later one hops, a click connects them (<see cref="Crossings"/>).</summary>
    public class CrossingTests
    {
        private static readonly Registry Reg = ModuleCatalog.CreateDefault();

        private static SketchDocument Doc(params SketchObject[] objects)
        {
            var doc = SketchDocument.Create();
            return Edits.Add(doc, doc.Pages[0].Id, objects);
        }

        private static SketchObject Line(string typeId, Vec2 a, Vec2 b)
            => Reg.Create(Reg.Library.First(i => i.TypeId == typeId), Vec2.Zero).WithParam("route", "free") with { Points = new[] { a, b } };

        private static bool Hops(SketchDocument doc, string id)
            => Reg.Render(doc.Get(id)!, new RenderContext(doc)).OfType<PathPrim>().Any(p => p.Segments.Any(s => s.Op == SegOp.C));

        [Theory]
        [InlineData("elec.wire")]
        [InlineData("fluid.line")]
        public void TheLaterLineHopsOverTheEarlierOne(string typeId)
        {
            var under = Line(typeId, new Vec2(50, 0), new Vec2(50, 100));
            var over = Line(typeId, new Vec2(0, 50), new Vec2(100, 50));
            var doc = Doc(under, over);
            Assert.True(Hops(doc, over.Id));
            Assert.False(Hops(doc, under.Id));
            var c = Assert.Single(Crossings.Find(Reg, doc, over));
            Assert.Equal(new Vec2(50, 50), c.At);
            Assert.True(c.Above);
            Assert.Contains(over.Id, Reg.Type(typeId)!.Dependents(under, new RenderContext(doc)));
            // Switched off: no hop.
            var off = Edits.Update(doc, new[] { over.Id }, o => o.WithParam("hops", false));
            Assert.False(Hops(off, over.Id));
        }

        [Fact]
        public void BranchesAndEndsAreNoCrossings()
        {
            var main = Line("elec.wire", new Vec2(0, 50), new Vec2(100, 50));
            var (doc, branch) = Polylines.Branch(Reg, Doc(main), main.Id, new Vec2(50, 50), new Vec2(50, 90));
            Assert.Empty(Crossings.Find(Reg, doc, doc.Get(branch!)!));
        }

        [Fact]
        public void ConnectingAtACrossingMakesAJunction()
        {
            var under = Line("elec.wire", new Vec2(50, 0), new Vec2(50, 100));
            var over = Line("elec.wire", new Vec2(0, 50), new Vec2(100, 50));
            var doc = Doc(under, over);
            var c = Crossings.Find(Reg, doc, over).Single();
            var joined = Crossings.Connect(Reg, doc, over.Id, c);
            var wires = joined.Objects.Values.Where(o => o.Type == "elec.wire").ToList();
            Assert.Equal(3, wires.Count);
            var docked = wires.Where(w => w.Attach is not null && w.Attach.Any(a => a?.ObjectId == under.Id)).ToList();
            Assert.Equal(2, docked.Count);
            foreach (var w in docked)
                Assert.Contains(Reg.Render(w, new RenderContext(joined)), p => p.Tag == "junction");
            Assert.Empty(Crossings.Find(Reg, joined, joined.Get(over.Id)!));
        }
    }
}
