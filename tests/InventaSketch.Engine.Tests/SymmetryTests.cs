using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Engine.Tests
{
    /// <summary>Only transforms that change a drawing are offered (<see cref="Symmetry"/>).</summary>
    public class SymmetryTests
    {
        private static readonly Registry Reg = ModuleCatalog.CreateDefault();

        private static TransformEffect Of(string typeId, string? view = null)
        {
            var item = Reg.Library.First(i => i.TypeId == typeId && (view is null || i.View == view));
            var o = Reg.Create(item, new Vec2(100, 100));
            var doc = SketchDocument.Create();
            doc = Edits.Add(doc, doc.Pages[0].Id, o);
            return Symmetry.Of(Reg.Render(o, new RenderContext(doc)));
        }

        [Fact]
        public void PlainShapes()
        {
            var circle = Symmetry.Of(new Primitive[] { Prim.Circle(0, 0, 10) });
            Assert.Equal(new TransformEffect(false, false, false), circle);
            var wide = Symmetry.Of(new Primitive[] { Prim.CRect(0, 0, 20, 10) });
            Assert.Equal(new TransformEffect(false, false, true), wide);
            var arrow = Symmetry.Of(new Primitive[] { Prim.Line(0, 0, 20, 0), Prim.Line(20, 0, 16, -3), Prim.Line(20, 0, 16, 3) });
            Assert.True(arrow.MirrorH);
            Assert.False(arrow.MirrorV);
            var label = Symmetry.Of(new Primitive[] { Prim.Circle(0, 0, 10), Prim.Text(0, 0, "A") });
            Assert.True(label.MirrorH);
        }

        [Fact]
        public void LibraryElements()
        {
            // A ball bearing in axial view looks the same mirrored; an arrow does not.
            var bearing = Of("mech.bearing.ball");
            Assert.False(bearing.MirrorH && bearing.MirrorV && bearing.Rotate90, "a bearing is symmetric in some way");
            var arrow = Of("ann.arrow");
            Assert.True(arrow.MirrorH || arrow.MirrorV);
        }
    }
}
