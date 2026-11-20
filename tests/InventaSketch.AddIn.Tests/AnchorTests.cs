using InventaSketch.AddIn.OnSlide;
using InventaSketch.Core;
using InventaSketch.Engine;
using Xunit;

namespace InventaSketch.AddIn.Tests
{
    /// <summary>"Anchor": a line end attached to an element of another unit follows that element (no PowerPoint needed).</summary>
    public class AnchorTests
    {
        private static SketchDocument Unit(SketchObject o)
        {
            var doc = SketchDocument.Create();
            return Edits.Add(doc, doc.Pages[0].Id, o);
        }

        [Fact]
        public void AnchoredLineEndFollowsTheMovedGear()
        {
            var registry = ModuleCatalog.CreateDefault();
            var gear = registry.Create(registry.Item("mech.spur.axial")!, new Vec2(50, 50));
            var corner = gear.BoxPoint(0.5, 0);
            var line = registry.Create(registry.Item("ann.arrow")!, Vec2.Zero) with
            {
                Points = new[] { new Vec2(0, 0), corner },
                Attach = new Attachment?[] { null, new Attachment(gear.Id, 0.5, 0) },
            };
            var lineUnit = Unit(line);
            Assert.True(SketchUnits.HasExternalAttachments(lineUnit));
            Assert.True(SketchUnits.NeedsContext(registry, lineUnit));

            // The gear (another unit) moves by 20 mm: resolving the anchors moves the stored end along, start stays.
            var moved = gear with { Transform = gear.Transform with { X = gear.Transform.X + 20 } };
            var gearUnit = new SketchUnit(null!, moved.Id, Unit(moved), default);
            var resolved = SketchUnits.ResolveAnchors(registry, lineUnit, new[] { gearUnit });
            var pts = resolved.Get(line.Id)!.Points!;
            Assert.Equal(corner.X + 20, pts[1].X, 6);
            Assert.Equal(0, pts[0].X, 6);
            Assert.NotNull(resolved.Get(line.Id)!.Attach![1]);

            // Without anchors nothing changes.
            var free = Unit(line with { Attach = null });
            Assert.False(SketchUnits.HasExternalAttachments(free));
            Assert.Same(free, SketchUnits.ResolveAnchors(registry, free, new[] { gearUnit }));
        }
    }
}
