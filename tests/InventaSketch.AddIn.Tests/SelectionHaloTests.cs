using System;
using System.Drawing;
using System.Linq;
using InventaSketch.AddIn.OnSlide;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.AddIn.Tests
{
    /// <summary>
    /// The selection look (maintainer 2026-11-25): a soft halo along the exact outline of the selected element, the element
    /// itself kept clear, nothing on an overlapping element that is not selected; and the "Select connected" pie entries.
    /// </summary>
    public class SelectionHaloTests
    {
        private const double Ppm = 4; // 4 px per mm
        private static readonly Rectangle Pane = new(0, 0, 800, 600);

        /// <summary>A box of 30 × 20 mm at (20, 20) mm, white fill, black outline.</summary>
        private static Primitive Box(double x, double y) => Prim.Rect(x, y, 30, 20, Core.Style.Visible with { Fill = Fill.Solid("#ffffff") });

        private static int Alpha(HaloImage img, double xMm, double yMm)
        {
            var x = (int)Math.Round(xMm * Ppm) - img.Origin.X;
            var y = (int)Math.Round(yMm * Ppm) - img.Origin.Y;
            if (x < 0 || y < 0 || x >= img.Bitmap.Width || y >= img.Bitmap.Height) return 0;
            return img.Bitmap.GetPixel(x, y).A;
        }

        [Fact]
        public void TheHaloGlowsJustOutsideTheOutlineAndLeavesTheElementClear()
        {
            using var img = SelectionHalo.Render(new[] { new HaloLayer(new[] { Box(20, 20) }, 1f) }, 0, 0, Ppm, 1f, false, Pane)!;
            Assert.NotNull(img);
            // Just outside the left edge (1 mm = 4 px): clearly tinted blue.
            Assert.True(Alpha(img, 19, 30) > 40, "glow beside the outline: " + Alpha(img, 19, 30));
            var c = img.Bitmap.GetPixel((int)(19 * Ppm) - img.Origin.X, (int)(30 * Ppm) - img.Origin.Y);
            Assert.True(c.B > c.R && c.B > c.G, "the glow is blue");
            // The filled body and the stroke itself stay clear (the glow lies "behind" the element).
            Assert.Equal(0, Alpha(img, 35, 30));
            Assert.Equal(0, Alpha(img, 20, 30));
            // It fades out: far away nothing.
            Assert.Equal(0, Alpha(img, 5, 30));
            Assert.True(Alpha(img, 19, 30) > Alpha(img, 16.5, 30));
        }

        [Fact]
        public void AnOverlappingElementThatIsNotSelectedGetsNoHaloOfItsOwn()
        {
            // A is selected, B overlaps it (B's right half lies beyond A).
            var a = Box(20, 20);
            var b = Prim.Rect(40, 25, 40, 10, Core.Style.Visible);
            using var img = SelectionHalo.Render(new[] { new HaloLayer(new[] { a }, 1f) }, 0, 0, Ppm, 1f, false, Pane)!;
            // Along B's far edge (x = 80 mm), well away from A: no glow.
            Assert.Equal(0, Alpha(img, 79, 30));
            Assert.Equal(0, Alpha(img, 70, 24));
            // Where A's outline runs under B the glow shows through (that is what tells A is the selected one).
            Assert.True(Alpha(img, 51, 30) > 40);
            Assert.NotNull(b);
        }

        [Fact]
        public void ConnectedButNotSelectedElementsGlowFainter()
        {
            var selected = Box(20, 20);
            var connected = Box(100, 20);
            using var img = SelectionHalo.Render(new[] { new HaloLayer(new[] { connected }, 0.45f), new HaloLayer(new[] { selected }, 1f) }, 0, 0, Ppm, 1f, false, Pane)!;
            var strong = Alpha(img, 19, 30);
            var faint = Alpha(img, 99, 30);
            Assert.True(faint > 0, "the connected element glows");
            Assert.True(faint < strong * 0.7, $"fainter ({faint} vs {strong})");
        }

        [Fact]
        public void AConnectedElementOnTopDoesNotHideTheSelectedOutlineBelowIt()
        {
            // B (connected, drawn on top) covers A's right edge: A's glow still shows there; inside A nothing.
            var a = Box(20, 20);
            var b = Prim.Rect(40, 22, 40, 16, Core.Style.Visible with { Fill = Fill.Solid("#ffffff") });
            using var img = SelectionHalo.Render(new[] { new HaloLayer(new[] { b }, 0.45f), new HaloLayer(new[] { a }, 1f) }, 0, 0, Ppm, 1f, false, Pane)!;
            Assert.True(Alpha(img, 51, 30) > 40, "A's outline under B: " + Alpha(img, 51, 30));
            Assert.Equal(0, Alpha(img, 30, 30));
            // An unfilled closed outline keeps its inside clear as well (a gear drawn as circles is not flooded).
            using var ring = SelectionHalo.Render(new[] { new HaloLayer(new[] { Prim.Circle(50, 50, 10, Core.Style.Visible) }, 1f) }, 0, 0, Ppm, 1f, false, Pane)!;
            Assert.Equal(0, Alpha(ring, 50, 50));
            Assert.True(Alpha(ring, 61, 50) > 40);
        }

        [Fact]
        public void DarkSlidesGetALighterTintAndNothingVisibleGivesNoImage()
        {
            Assert.True(SelectionHalo.Tint(true).R > SelectionHalo.Tint(false).R);
            Assert.True(SelectionHalo.Tint(true).G > SelectionHalo.Tint(false).G);
            Assert.Null(SelectionHalo.Render(new[] { new HaloLayer(new[] { Box(500, 500) }, 1f) }, 0, 0, Ppm, 1f, false, Pane));
            Assert.Null(SelectionHalo.Render(Array.Empty<HaloLayer>(), 0, 0, Ppm, 1f, false, Pane));
        }

        [Fact]
        public void TheBlurSpreadsSymmetricallyAndKeepsTheTotal()
        {
            const int w = 21, h = 21;
            var a = new byte[w * h];
            a[10 * w + 10] = 255;
            a[10 * w + 9] = 255;
            a[10 * w + 11] = 255;
            var before = a.Sum(x => (int)x);
            SelectionHalo.BlurAlpha(a, w, h, 2);
            Assert.Equal(a[10 * w + 6], a[10 * w + 14]);
            Assert.Equal(a[6 * w + 10], a[14 * w + 10]);
            Assert.True(a[10 * w + 10] < 255 && a[10 * w + 10] > 0);
            Assert.Equal(0, a[0]);
            Assert.InRange(a.Sum(x => (int)x), before * 0.8, before * 1.2);
        }

        [Fact]
        public void PathOfCoversEveryPrimitiveKind()
        {
            using (var p = SelectionHalo.PathOf(Prim.Line(0, 0, 10, 0), out var filled, out var stroked, out var w))
            {
                Assert.NotNull(p);
                Assert.False(filled);
                Assert.True(stroked);
                Assert.True(w > 0);
            }
            using (var p = SelectionHalo.PathOf(Prim.Circle(0, 0, 5, Core.Style.Visible with { Fill = Fill.Solid("#000000") }), out var filled, out _, out _)) { Assert.NotNull(p); Assert.True(filled); }
            using (var p = SelectionHalo.PathOf(Prim.Text(0, 0, "Pump"), out var filled, out var stroked, out _)) { Assert.NotNull(p); Assert.True(filled); Assert.False(stroked); }
            using (var p = SelectionHalo.PathOf(Prim.Polyline(new[] { new Vec2(0, 0), new Vec2(5, 5), new Vec2(10, 0) }), out _, out _, out _)) Assert.NotNull(p);
            Assert.Null(SelectionHalo.PathOf(Prim.Text(0, 0, " "), out _, out _, out _));
        }

        [Fact]
        public void SelectConnectedEntriesFollowTheOfferedOrderAndSaySoInBothLanguages()
        {
            var items = OnSlideController.ConnectItems(new[] { (ConnectKind.Loop, 4), (ConnectKind.Net, 3), (ConnectKind.Chain, 1) }, Lang.En);
            Assert.Equal(new[] { "connect:net", "connect:loop" }, items.Select(i => i.Id));
            Assert.Equal("Net", items[0].Label);
            Assert.Contains("3 elements", items[0].Hint);
            var de = OnSlideController.ConnectItems(new[] { (ConnectKind.Coupled, 2), (ConnectKind.Walls, 5) }, Lang.De);
            Assert.Equal(new[] { "Bewegt sich mit", "Raumwände" }, de.Select(i => i.Label));
            Assert.Equal(IconIds.SelectAssembly, de[0].IconId);
            foreach (var k in Connectivity.Offered) Assert.Equal(k, OnSlideController.KindOf(OnSlideController.KindId(k)));
            Assert.Null(OnSlideController.KindOf("nope"));
        }

        [Fact]
        public void SameNameReportsTheOtherSlides()
        {
            var also = Continuations.AlsoOn(new[] { "2", "4" }, Lang.En);
            Assert.Equal("also on slides 2, 4", also);
            Assert.Equal("auch auf Folie 3", Continuations.AlsoOn(new[] { "3" }, Lang.De));
            Assert.Equal("", Continuations.AlsoOn(null, Lang.En));
            var items = OnSlideController.ConnectItems(new[] { (ConnectKind.SameName, 2) }, Lang.En, also);
            Assert.Equal(new[] { "connect:samename", "info:samename" }, items.Select(i => i.Id));
            Assert.Equal("Same name", items[0].Label);
            Assert.Contains("also on slides 2, 4", items[0].Hint);
            Assert.False(items[1].Enabled);
            // Only on other slides: just the report.
            var only = OnSlideController.ConnectItems(new[] { (ConnectKind.SameName, 1) }, Lang.De, Continuations.AlsoOn(new[] { "5" }, Lang.De));
            Assert.Equal("Gleicher Name: auch auf Folie 5", only.Single().Label);
        }
    }
}
