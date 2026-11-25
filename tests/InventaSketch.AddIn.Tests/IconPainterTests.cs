using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using InventaSketch.AddIn.OnSlide;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.AddIn.Tests
{
    /// <summary>Every abstract icon id has its own pictogram; a sheet of all icons is written for visual review.</summary>
    public class IconPainterTests
    {
        private static byte[] Render(string id)
        {
            using var bmp = new Bitmap(24, 24);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.White);
                IconPainter.Draw(g, id, new RectangleF(0, 0, 24, 24), Color.Black);
            }
            using var ms = new MemoryStream();
            bmp.Save(ms, ImageFormat.Png);
            return ms.ToArray();
        }

        public static IEnumerable<object[]> Ids()
        {
            foreach (var id in IconIds.All) yield return new object[] { id };
        }

        [Theory]
        [MemberData(nameof(Ids))]
        public void EveryIconIdHasItsOwnPictogram(string id)
        {
            Assert.NotEqual(Render("__unknown__"), Render(id));
        }

        [Fact]
        public void AllIconIdsAreDistinct()
        {
            Assert.Equal(IconIds.All.Count, new HashSet<string>(IconIds.All).Count);
        }

        [Fact]
        public void SharedPathsParse()
        {
            foreach (var kv in IconPainter.SharedPaths)
            {
                var figures = IconPainter.ParseSvgPath(kv.Value);
                Assert.True(figures.Count > 0, kv.Key);
                foreach (var f in figures)
                {
                    foreach (var p in f.Figure.PathPoints)
                        Assert.True(p.X >= 0 && p.X <= 24 && p.Y >= 0 && p.Y <= 24, $"{kv.Key}: point {p} outside the 24-unit grid");
                    f.Figure.Dispose();
                }
            }
        }

        /// <summary>Contact sheets for review: every pictogram at 32 and 16 px, light and dark (ui-renders/icons-*.png).</summary>
        [Fact]
        public void WritesIconSheet()
        {
            const int cell = 64, cols = 10;
            var rows = (IconIds.All.Count + cols - 1) / cols;
            var dir = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "ui-renders");
            Directory.CreateDirectory(dir);
            foreach (var (name, bg, ink) in new[] { ("light", Color.White, Color.FromArgb(40, 40, 40)), ("dark", Color.FromArgb(31, 31, 31), Color.FromArgb(232, 232, 232)) })
            {
                using var sheet = new Bitmap(cell * cols, cell * rows);
                using (var g = Graphics.FromImage(sheet))
                using (var font = new Font("Segoe UI", 6.5f, GraphicsUnit.Pixel))
                using (var text = new SolidBrush(ink))
                {
                    g.Clear(bg);
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                    for (var i = 0; i < IconIds.All.Count; i++)
                    {
                        float x = i % cols * cell, y = i / cols * cell;
                        IconPainter.Draw(g, IconIds.All[i], new RectangleF(x + 16, y + 3, 32, 32), ink);
                        IconPainter.Draw(g, IconIds.All[i], new RectangleF(x + 24, y + 46, 16, 16), ink);
                        var label = IconIds.All[i];
                        var w = g.MeasureString(label, font).Width;
                        g.DrawString(label, font, text, x + (cell - w) / 2, y + 35);
                    }
                }
                sheet.Save(Path.Combine(dir, "icons-" + name + ".png"), ImageFormat.Png);
            }
        }
    }
}
