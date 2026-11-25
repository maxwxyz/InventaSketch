using System.Collections.Generic;
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
                    new BarButton("act:more", "", "Mehr Zähne") { IconId = IconIds.Increase },
                    new BarButton("act:helix", "Schrägverzahnung", "Schrägverzahnung an/aus", Active: true),
                    new BarButton("planscale", "1:50", "Planmaßstab der Folie …"),
                    new BarButton("insert", "", "Passendes Element einfügen") { IconId = IconIds.Insert },
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
        public void ChoiceDropdownShowsIconAndLabelPerOption(bool dark)
        {
            Directory.CreateDirectory(Out);
            var buttons = new List<BarButton>
            {
                new("choice:headStart", "", "Spitze Anfang") { IconId = IconIds.HeadNone, Menu = true, Mirror = true },
                new("choice:headEnd", "", "Spitze Ende", Active: true) { IconId = IconIds.HeadTriangle, Menu = true },
                new("choice:kind", "Permanentmagnet…", "Maschinenart") { Menu = true },
                new("", "", "", Separator: true),
                new("delete", "", "Löschen", Danger: true) { IconId = IconIds.Delete },
            };
            var bar = new FloatingBar(buttons, 2f);
            bar.Place(new RectangleF(400, 400, 200, 200), new Rectangle(0, 0, 1600, 1200));
            var items = new[] { ("none", "Keine", IconIds.HeadNone), ("triangle", "Dreieck", IconIds.HeadTriangle), ("open", "Offen", IconIds.HeadOpen), ("oval", "Punkt", IconIds.HeadDot), ("tick", "Schrägstrich", IconIds.HeadTick) }
                .Select(x => new DropItem("pick:headEnd:" + x.Item1, x.Item2, x.Item3, null, x.Item1 == "triangle", false)).ToList();
            bar.SetDropdown(1, items);
            bar.Hover = FloatingBar.DropBase + 2;
            using var bmp = bar.Render(dark);
            Assert.Equal(bar.WindowBounds.Size, bmp.Size);
            Assert.True(bmp.Height > bar.Size.Height + items.Count * 50, "the list hangs below the bar");
            // Rows hit-test as dropdown entries and map to their commands.
            var b = bar.Bounds;
            var row = new Point(b.X + 80 + 20, b.Y + bar.Size.Height + 8 + 64 * 3 + 32);   // rows of 32 logical px (UX review 2026-11-25)
            Assert.Equal(FloatingBar.DropBase + 3, bar.HitTest(row));
            Assert.Equal("pick:headEnd:oval", bar.IdAt(bar.HitTest(row)));
            bmp.Save(Path.Combine(Out, dark ? "dropdown-dark.png" : "dropdown-light.png"));
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
                new PieItem("view:plan", "Grundriss", Active: true) { IconId = IconIds.Plan },
                new PieItem("view:elevation", "Ansicht") { IconId = IconIds.Front },
                new PieItem("dup", "Duplizieren") { IconId = IconIds.Duplicate },
                new PieItem("rot90", "Drehen 90°") { IconId = IconIds.Rotate },
                new PieItem("act:thicker", "Dickere Wand (300 mm)") { IconId = IconIds.Increase },
                new PieItem("act:kind", "Wandart: Innenwand (tragend, verputzt)", Enabled: false) { IconId = IconIds.Cycle },
                new PieItem("menu:more", "Mehr ▸") { IconId = IconIds.Cycle },
            };
            var pie = new PieMenu(new PointF(300, 300), items, 2f) { Hover = 6 };
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

        /// <summary>
        /// UX review 2026-11-25 (maintainer: "the labels stick out of the pie"): with 6, 8, 10 and 12 places, long German
        /// labels with and without pictograms, no text pixel lies outside the ring.
        /// </summary>
        [Theory]
        [InlineData(6)]
        [InlineData(8)]
        [InlineData(10)]
        [InlineData(12)]
        public void PieLabelsStayInsideTheRing(int count)
        {
            Directory.CreateDirectory(Out);
            var labels = new[] { "Kurvenform: Ausgleichsfunktion", "Gelenkkreis anzeigen", "Verbundene auswählen ▸", "Leitungsführung: orthogonal",
                "Wandart: Innenwand (tragend)", "Zwischenlagen", "Text bearbeiten …", "Wellenanschluss: Passfeder", "Freiheitsgrad F = 1",
                "Achsenbeschriftung", "Strahlenbündel einfügen", "Diagrammwerte normieren" };
            var items = Enumerable.Range(0, count).Select(i => new PieItem("i" + i, labels[i % labels.Length]) { IconId = i % 2 == 0 ? IconIds.Text : null }).ToArray();
            var pie = new PieMenu(new PointF(520, 520), items, 2f) { Title = "Stirnrad" };
            using var bmp = new Bitmap(1040, 1040);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.White);
                pie.Draw(g, dark: false);
            }
            bmp.Save(Path.Combine(Out, "pie-fit-" + count + ".png"));
            var outside = 0;
            for (var y = 0; y < bmp.Height; y += 2)
                for (var x = 0; x < bmp.Width; x += 2)
                {
                    var d = System.Math.Sqrt((x - 520.0) * (x - 520.0) + (y - 520.0) * (y - 520.0));
                    if (d <= pie.Outer + 1) continue;
                    var c = bmp.GetPixel(x, y);
                    // Text is dark; the shadow outside the ring is a faint grey.
                    if (c.R < 150 && c.G < 150 && c.B < 150) outside++;
                }
            Assert.Equal(0, outside);
            Assert.True(pie.Outer <= 236 * 2f + 0.1f, "the ring stays within its cap");
        }

        [Fact]
        public void TextOnlySubmenuKeepsLongWordsReadable()
        {
            Directory.CreateDirectory(Out);
            var names = new[] { "← Zurück", "Linie", "Pfeil", "Doppelpfeil", "Bogenpfeil", "Winkelverbinder", "Bezugslinie", "Bezugszeichen", "Lineares Maß", "Paralleles Maß" };
            var pie = new PieMenu(new PointF(320, 320), names.Select((n, i) => new PieItem("i" + i, n, Active: i == 2)).ToArray(), 2f);
            using var bmp = new Bitmap(640, 640);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.White);
                pie.Draw(g, dark: false);
            }
            bmp.Save(Path.Combine(Out, "pie-submenu.png"));
            Assert.Equal(0, pie.HitTest(new PointF(320, 320 - 100)));
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
