using System.Linq;
using InventaSketch.AddIn.OnSlide;
using InventaSketch.Core;
using InventaSketch.Engine;
using Xunit;

namespace InventaSketch.AddIn.Tests
{
    /// <summary>"Animate as slides": one unit model per frame, only the moving objects change.</summary>
    public class AnimationTests
    {
        [Fact]
        public void MechanismUnitsYieldOneModelPerFrame()
        {
            var registry = ModuleCatalog.CreateDefault();
            var doc = SketchDocument.Create();
            var mech = registry.Create(registry.Library.First(i => i.TypeId == "kin.mech.fourbar"), new Vec2(80, 60));
            var label = registry.Create(registry.Item("ann.arrow")!, new Vec2(20, 20));
            doc = Edits.Add(doc, doc.Pages[0].Id, mech, label);
            var docs = OnSlideController.AnimationDocs(registry, doc, OnSlideController.AnimationFrameCount);
            Assert.Equal(OnSlideController.AnimationFrameCount, docs.Count);
            Assert.Same(doc, docs[0]);
            Assert.All(docs, d => Assert.Same(doc.Get(label.Id), d.Get(label.Id)));
            Assert.Equal(docs.Count, docs.Select(d => d.Get(mech.Id)!.Num("angle", double.NaN)).Distinct().Count());
        }

        [Fact]
        public void StillUnitsHaveNoAnimation()
        {
            var registry = ModuleCatalog.CreateDefault();
            var doc = SketchDocument.Create();
            doc = Edits.Add(doc, doc.Pages[0].Id, registry.Create(registry.Item("ann.arrow")!, new Vec2(20, 20)));
            Assert.Empty(OnSlideController.AnimationDocs(registry, doc, 12));
        }
    }
}
