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
        public void WritesIconSheet()
        {
            const int cell = 48;
            using var sheet = new Bitmap(cell * 8, cell * ((IconIds.All.Count + 7) / 8));
            using (var g = Graphics.FromImage(sheet))
            {
                g.Clear(Color.White);
                for (var i = 0; i < IconIds.All.Count; i++)
                    IconPainter.Draw(g, IconIds.All[i], new RectangleF(i % 8 * cell + 8, i / 8 * cell + 8, 32, 32), Color.FromArgb(40, 40, 40));
            }
            var dir = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "ui-renders");
            Directory.CreateDirectory(dir);
            sheet.Save(Path.Combine(dir, "icons.png"), ImageFormat.Png);
        }
    }
}
