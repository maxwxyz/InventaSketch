using System.Drawing;
using InventaSketch.AddIn.OnSlide;
using Xunit;

namespace InventaSketch.AddIn.Tests
{
    /// <summary>The floating bar sits above the selection unless that hides other elements (a coupled gear, a shaft).</summary>
    public class BarPlacementTests
    {
        private static readonly Size Bar = new(400, 40);
        private static readonly Rectangle Pane = new(0, 0, 1600, 1200);
        private static readonly RectangleF Selection = new(700, 500, 60, 200);

        [Fact]
        public void AboveWhenNothingIsInTheWay()
        {
            var r = FloatingBar.Placement(Bar, 1, Selection, Pane);
            Assert.True(r.Bottom <= Selection.Top, "above");
        }

        [Fact]
        public void BelowWhenAnotherElementIsAbove()
        {
            var gearAbove = new RectangleF(650, 380, 160, 110);
            var r = FloatingBar.Placement(Bar, 1, Selection, Pane, new[] { gearAbove });
            Assert.True(r.Top >= Selection.Bottom, "below");
            Assert.False(r.IntersectsWith(Rectangle.Round(gearAbove)));
        }

        [Fact]
        public void BesideWhenAboveAndBelowAreTaken()
        {
            var above = new RectangleF(400, 380, 700, 110);
            var below = new RectangleF(400, 710, 700, 110);
            var r = FloatingBar.Placement(Bar, 1, Selection, Pane, new[] { above, below });
            Assert.True(r.Left >= Selection.Right || r.Right <= Selection.Left, "beside");
            Assert.False(r.IntersectsWith(Rectangle.Round(Selection)));
        }

        [Fact]
        public void BelowAtTheTopOfThePane()
        {
            var r = FloatingBar.Placement(Bar, 1, new RectangleF(700, 20, 60, 100), Pane);
            Assert.True(r.Top >= 120);
        }
    }
}
