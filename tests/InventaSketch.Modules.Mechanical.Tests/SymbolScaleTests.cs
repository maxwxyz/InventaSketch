using System;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Mechanical.Tests
{
    /// <summary>Scaling a stick symbol changes what carries meaning (width, diameter), not the hub symbol.</summary>
    public class SymbolScaleTests
    {
        private static double HubExtent(Registry r, SketchObject o)
        {
            var empty = SketchDocument.Create();
            var doc = Edits.Add(empty, empty.Pages[0].Id, o);
            var hub = Prim.Flatten(r.Render(o, new RenderContext(doc))).Where(p => p.Tag == "hub").ToList();
            Assert.NotEmpty(hub);
            var b = Prim.Bounds(hub)!.Value;
            return Math.Max(b.W, b.H);
        }

        [Fact]
        public void HubCrossDoesNotGrowWithTheGear()
        {
            var r = Fx.Reg();
            var gear = r.Create(r.Item("mech.spur.stick")!, new Vec2(50, 50));
            var big = gear with { Size = new Size2(gear.Size.W * 3, gear.Size.H * 3) };
            var small = gear with { Size = new Size2(gear.Size.W * 0.5, gear.Size.H * 0.5) };
            var normal = HubExtent(r, gear);
            Assert.Equal(normal, HubExtent(r, big), 6);
            Assert.Equal(normal * 0.5, HubExtent(r, small), 6);
        }
    }
}
