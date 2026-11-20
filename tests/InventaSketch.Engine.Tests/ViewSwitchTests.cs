using System;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Engine.Tests
{
    /// <summary>Switching views takes the target view's proportions from the library presets instead of stretching the box.</summary>
    public class ViewSwitchTests
    {
        private static readonly Registry Reg = ModuleCatalog.CreateDefault();

        private static (Size2 Size, string View) Switch(string itemId, string view, double scale = 1)
        {
            var item = Reg.Item(itemId)!;
            var o = Reg.Create(item, new Vec2(100, 100));
            o = o with { Size = new Size2(o.Size.W * scale, o.Size.H * scale) };
            var empty = SketchDocument.Create();
            var doc = Edits.Add(empty, empty.Pages[0].Id, o);
            var switched = Reg.WithView(o, view, new RenderContext(doc));
            return (switched.Size, switched.View!);
        }

        [Fact]
        public void GearKeepsItsDiameterFromFrontToSideView()
        {
            var axial = Reg.Item("mech.spur.axial")!.Size!;
            var side = Reg.Item("mech.spur.side")!.Size!;
            var (size, view) = Switch("mech.spur.axial", "side", scale: 1.5);
            Assert.Equal("side", view);
            // Side preset proportions, scaled like the object was (no distortion).
            Assert.Equal(side.W / side.H, size.W / size.H, 6);
            Assert.True(Math.Abs(size.H - axial.H * 1.5) < 1e-6 || Math.Abs(size.W * size.H - side.W * side.H * 2.25) < 1e-6);
        }

        [Fact]
        public void EveryMechanicalViewSwitchKeepsPresetProportions()
        {
            foreach (var type in Reg.Types.Where(t => t.Id.StartsWith("mech.", StringComparison.Ordinal) && t.Views.Count > 1))
            {
                var first = Reg.Library.FirstOrDefault(i => i.TypeId == type.Id && i.Size is not null);
                if (first is null) continue;
                foreach (var v in type.Views)
                {
                    var preset = Reg.Library.FirstOrDefault(i => i.TypeId == type.Id && i.View == v.Id && i.Size is not null);
                    if (preset is null || v.Id == first.View) continue;
                    var (size, _) = Switch(first.Id, v.Id);
                    Assert.Equal(preset.Size!.W / preset.Size.H, size.W / size.H, 6);
                }
            }
        }
    }
}
