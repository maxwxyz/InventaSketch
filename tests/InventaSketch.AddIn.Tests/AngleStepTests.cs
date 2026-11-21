using InventaSketch.AddIn.OnSlide;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.AddIn.Tests
{
    /// <summary>Shift while drawing a line: the direction snaps to 15 degree steps, the length follows the pointer.</summary>
    public class AngleStepTests
    {
        [Fact]
        public void SnapsTo15DegreeSteps()
        {
            var p = OnSlideController.AngleStep(new Vec2(0, 0), new Vec2(10, 1));
            Assert.Equal(0, p.Y, 9);
            Assert.Equal(System.Math.Sqrt(101), p.X, 9);
            var q = OnSlideController.AngleStep(new Vec2(10, 10), new Vec2(20, 20.8));
            Assert.Equal(q.X - 10, q.Y - 10, 9); // 45 degrees
            var r = OnSlideController.AngleStep(new Vec2(0, 0), new Vec2(10, 2.9));
            Assert.Equal(System.Math.Tan(15 * System.Math.PI / 180), r.Y / r.X, 9);
        }
    }
}
