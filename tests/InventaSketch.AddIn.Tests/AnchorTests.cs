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

        [Fact]
        public void BranchWireInAnotherUnitFollowsTheMainWire()
        {
            var registry = ModuleCatalog.CreateDefault();
            var wireItem = System.Linq.Enumerable.First(registry.Library, i => i.TypeId == "elec.wire");
            var main = registry.Create(wireItem, Vec2.Zero) with { Points = new[] { new Vec2(20, 40), new Vec2(120, 40) } };
            var branch = registry.Create(wireItem, Vec2.Zero) with
            {
                Points = new[] { new Vec2(70, 90), new Vec2(70, 40) },
                Attach = new Attachment?[] { null, new Attachment(main.Id, 0.5, 0) { AnchorId = "rail0" } },
            };
            var branchUnit = Unit(branch);
            Assert.True(SketchUnits.HasExternalAttachments(branchUnit));
            // The main wire (another unit) is moved 10 mm down: the branch end follows along its rail.
            var moved = main with { Points = new[] { new Vec2(20, 50), new Vec2(120, 50) } };
            var resolved = SketchUnits.ResolveAnchors(registry, branchUnit, new[] { new SketchUnit(null!, moved.Id, Unit(moved), default) });
            var pts = resolved.Get(branch.Id)!.Points!;
            Assert.Equal(new Vec2(70, 50), pts[1]);
            Assert.Equal(new Vec2(70, 90), pts[0]);
        }
    }
}
