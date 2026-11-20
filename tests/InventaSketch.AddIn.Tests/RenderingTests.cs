using System.Drawing;
using System.IO;
using System.Linq;
using InventaSketch.AddIn;
using InventaSketch.AddIn.OnSlide;
using InventaSketch.Core;
using InventaSketch.Engine;
using Xunit;

namespace InventaSketch.AddIn.Tests
{
    /// <summary>
    /// Renders the on-slide UI (floating bar, pie menu) in light and dark theme to PNG files in the test output folder
    /// ("ui-renders") for visual review, and checks basic properties.
    /// </summary>
    public class RenderingTests
    {
        private static readonly string Out = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "ui-renders");

        private static Bitmap GearIcon(string view, bool dark)
        {
            var registry = ModuleCatalog.CreateDefault();
            var item = registry.Library.First(i => i.TypeId == "mech.gear.spur");
            var doc = SketchDocument.Create();
            var preset = registry.Library.FirstOrDefault(i => i.TypeId == "mech.gear.spur" && i.View == view && i.Size is not null)?.Size;
            var o = registry.Create(item, new Vec2(50, 50)) with { View = view, Size = preset ?? new Size2(40, 40) };
            doc = Edits.Add(doc, doc.Pages[0].Id, o);
            var prims = registry.Render(o, new RenderContext(doc, Lang.De));
            return ThumbnailRenderer.Render(prims, 44, dark ? Color.FromArgb(230, 230, 230) : Color.FromArgb(40, 40, 40), Color.Transparent);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void FloatingBarRendersWithIconsAndTooltip(bool dark)
        {
            Directory.CreateDirectory(Out);
            var buttons = new[] { "stick", "side", "section", "axial", "teeth" }
                .Select(v => new BarButton("view:" + v, v, "Ansicht " + v, Active: v == "axial") { IconId = IconPainter.ForView(new ViewDef(v, v)) })
                .Concat(new[]
                {
                    new BarButton("", "", "", Separator: true),
                    new BarButton("flipH", "", "Horizontal spiegeln") { IconId = IconIds.MirrorH },
                    new BarButton("flipV", "", "Vertikal spiegeln") { IconId = IconIds.MirrorV },
                    new BarButton("rot90", "", "Um 90 Grad drehen") { IconId = IconIds.Rotate },
                    new BarButton("dup", "", "Duplizieren") { IconId = IconIds.Duplicate },
                    new BarButton("", "", "", Separator: true),
                    new BarButton("delete", "", "Löschen", Danger: true) { IconId = IconIds.Delete },
                }).ToList();
            var bar = new FloatingBar(buttons, 2f);
            bar.Place(new RectangleF(400, 400, 200, 200), new Rectangle(0, 0, 1600, 1200));
            bar.Hover = 2;
            using var bmp = bar.Render(dark);
            Assert.Equal(bar.WindowBounds.Size, bmp.Size);
            Assert.True(bar.Bounds.Bottom <= 400 - 2 * 46 + 2, "bar sits above the selection, clear of the rotation handle");
            bmp.Save(Path.Combine(Out, dark ? "bar-dark.png" : "bar-light.png"));
            // Opaque pixels exist both in the bar and in the tooltip strip (hover tooltip).
            Assert.True(bmp.GetPixel(bmp.Width / 4, bar.Size.Height / 2).A > 200);
            Assert.Contains(Enumerable.Range(0, bmp.Width), x => bmp.GetPixel(x, bar.Size.Height + bar.TipHeight / 2).A > 200);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void PieMenuRendersWithIcons(bool dark)
        {
            Directory.CreateDirectory(Out);
            var items = new[]
            {
                new PieItem("delete", "Löschen", Danger: true) { IconId = IconIds.Delete },
                new PieItem("view:stick", "Schema") { IconId = IconIds.Schematic },
                new PieItem("view:axial", "Stirnansicht", Active: true) { IconId = IconIds.Axial },
                new PieItem("view:teeth", "Mit Zähnen") { IconId = IconIds.Teeth },
                new PieItem("dup", "Duplizieren") { IconId = IconIds.Duplicate },
                new PieItem("flipH", "Spiegeln") { IconId = IconIds.MirrorH },
                new PieItem("rot90", "Drehen 90°") { IconId = IconIds.Rotate },
                new PieItem("menu:type", "Typ ▸", Enabled: false) { IconId = IconIds.Cycle },
            };
            var pie = new PieMenu(new PointF(300, 300), items, 2f) { Hover = 1 };
            using var bmp = new Bitmap(600, 600);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(dark ? Color.FromArgb(40, 40, 40) : Color.White);
                pie.Draw(g, dark);
            }
            bmp.Save(Path.Combine(Out, dark ? "pie-dark.png" : "pie-light.png"));
            Assert.Equal(0, pie.HitTest(new PointF(300, 300 - 60)));
            Assert.Equal(-1, pie.HitTest(new PointF(300, 300)));
        }

        private static int Ink(Bitmap bmp) => Enumerable.Range(0, bmp.Width).Sum(x => Enumerable.Range(0, bmp.Height).Count(y => bmp.GetPixel(x, y).R < 128));

        private static Bitmap Thumb(params Primitive[] prims) => ThumbnailRenderer.Render(prims, 200, Color.Black, Color.White);

        private static Primitive Arrow(ArrowHead end, double y = 0)
            => Prim.Line(0, y, 20, y, Core.Style.Visible with { StrokeWidth = 0.3, ArrowEnd = end, ArrowSize = ArrowSize.Large });

        [Fact]
        public void ThumbnailsDrawEveryArrowheadIncludingHollowAndCrowsFootEnds()
        {
            Directory.CreateDirectory(Out);
            var heads = System.Enum.GetValues(typeof(ArrowHead)).Cast<ArrowHead>().ToList();
            using (var gallery = ThumbnailRenderer.Render(heads.Select((h, i) => Arrow(h, i * 6)).ToArray(), 600, Color.Black, Color.White))
                gallery.Save(Path.Combine(Out, "arrowheads.png"));

            // Each head adds ink to the bare line; a fixed reference square keeps the scale identical for every image.
            var frame = Prim.Rect(-2, -8, 26, 16, Core.Style.Visible with { StrokeWidth = 0.1 });
            using var bare = Thumb(frame, Arrow(ArrowHead.None));
            var baseInk = Ink(bare);
            foreach (var h in heads.Where(h => h != ArrowHead.None))
            {
                using var bmp = Thumb(frame, Arrow(h));
                Assert.True(Ink(bmp) > baseInk + 3, h + " draws nothing");
            }
            // Hollow heads leave their interior blank.
            using var filled = Thumb(frame, Arrow(ArrowHead.Diamond));
            using var hollow = Thumb(frame, Arrow(ArrowHead.DiamondOpen));
            Assert.True(Ink(hollow) < Ink(filled));
        }

        [Fact]
        public void ThumbnailsUnderlineText()
        {
            var frame = Prim.Rect(-12, -6, 24, 12, Core.Style.Visible with { StrokeWidth = 0.1 });
            using var plain = Thumb(frame, Prim.Text(0, 0, "Key"));
            using var under = Thumb(frame, Prim.Text(0, 0, "Key", new TextStyle { Underline = true }));
            Assert.True(Ink(under) > Ink(plain) + 10);
        }
    }
}
