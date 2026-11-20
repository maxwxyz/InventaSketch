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
                .Select(v => new BarButton("view:" + v, v, "Ansicht " + v, Active: v == "axial") { Icon = GearIcon(v, dark) })
                .Concat(new[]
                {
                    new BarButton("", "", "", Separator: true),
                    new BarButton("flipH", "⇆", "Horizontal spiegeln"),
                    new BarButton("flipV", "⇅", "Vertikal spiegeln"),
                    new BarButton("rot90", "↻", "Um 90 Grad drehen"),
                    new BarButton("dup", "⧉", "Duplizieren"),
                    new BarButton("", "", "", Separator: true),
                    new BarButton("delete", "✕", "Löschen", Danger: true),
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
                new PieItem("delete", "Löschen", Danger: true) { Glyph = "✕" },
                new PieItem("view:stick", "Schema") { Icon = GearIcon("stick", dark) },
                new PieItem("view:axial", "Stirnansicht", Active: true) { Icon = GearIcon("axial", dark) },
                new PieItem("view:teeth", "Mit Zähnen") { Icon = GearIcon("teeth", dark) },
                new PieItem("dup", "Duplizieren") { Glyph = "⧉" },
                new PieItem("flipH", "Spiegeln") { Glyph = "⇆" },
                new PieItem("rot90", "Drehen 90°") { Glyph = "↻" },
                new PieItem("menu:type", "Typ ▸", Enabled: false),
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
    }
}
