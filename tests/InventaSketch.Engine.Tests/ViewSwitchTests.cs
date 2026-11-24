using System;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Engine.Tests
{
    /// <summary>
    /// Switching views takes the target view's proportions from the library presets instead of stretching the box; parts
    /// with a pitch circle (gears, pulleys) keep their pitch radius, so the same part is equally big in every view.
    /// </summary>
    public class ViewSwitchTests
    {
        private static readonly Registry Reg = ModuleCatalog.CreateDefault();

        private static (SketchObject Before, SketchObject After, RenderContext Ctx) Switch(string itemId, string view, double scale = 1)
        {
            var item = Reg.Item(itemId)!;
            var o = Reg.Create(item, new Vec2(100, 100));
            o = o with { Size = new Size2(o.Size.W * scale, o.Size.H * scale) };
            var empty = SketchDocument.Create();
            var doc = Edits.Add(empty, empty.Pages[0].Id, o);
            var ctx = new RenderContext(doc);
            return (o, Reg.WithView(o, view, ctx), ctx);
        }

        private static double? Pitch(SketchObject o, RenderContext ctx)
            => Reg.PageAnchors(o, ctx).FirstOrDefault(a => a.Kind == AnchorKind.Circle && a.Role is AnchorRole.Pitch or AnchorRole.PitchInner)?.Radius;

        [Fact]
        public void GearKeepsItsPitchCircleFromFrontToSideView()
        {
            var (before, after, ctx) = Switch("mech.spur.axial", "side", scale: 1.5);
            Assert.Equal("side", after.View);
            Assert.Equal(Pitch(before, ctx)!.Value, Pitch(after, ctx)!.Value, 6);
            var back = Reg.WithView(after, "axial", ctx);
            Assert.Equal(before.Size.W, back.Size.W, 6);
            Assert.Equal(before.Size.H, back.Size.H, 6);
        }

        [Fact]
        public void EveryMechanicalViewSwitchKeepsPresetProportionsOrThePitchCircle()
        {
            foreach (var type in Reg.Types.Where(t => t.Id.StartsWith("mech.", StringComparison.Ordinal) && t.Views.Count > 1))
            {
                var first = Reg.Library.FirstOrDefault(i => i.TypeId == type.Id && i.Size is not null);
                if (first is null) continue;
                foreach (var v in type.Views)
                {
                    var preset = Reg.Library.FirstOrDefault(i => i.TypeId == type.Id && i.View == v.Id && i.Size is not null);
                    if (preset is null || v.Id == first.View) continue;
                    var (before, after, ctx) = Switch(first.Id, v.Id);
                    if (Pitch(before, ctx) is double r0 && Pitch(after, ctx) is double r1)
                        Assert.True(Math.Abs(r0 - r1) < 1e-6, $"{first.Id} → {v.Id}: pitch {r0} → {r1}");
                    else
                        Assert.Equal(preset.Size!.W / preset.Size.H, after.Size.W / after.Size.H, 6);
                }
            }
        }
    }
}
