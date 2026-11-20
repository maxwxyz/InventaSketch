using System.Drawing;
using System.IO;
using System.Linq;
using Xunit;

namespace InventaSketch.AddIn.Tests
{
    /// <summary>The embedded logo loads under .NET Framework 4.8 as a window icon and as bitmaps of any size.</summary>
    public class BrandingTests
    {
        [Fact]
        public void IcoHasAllTitleBarAndExplorerSizes()
        {
            var ico = File.ReadAllBytes(Path.Combine(Root(), "assets", "branding", "inventasketch.ico"));
            var sizes = Branding.ReadFrames(ico).Select(f => f.Size).ToArray();
            Assert.Equal(new[] { 16, 20, 24, 32, 40, 48, 64, 128, 256 }, sizes);
        }

        [Theory]
        [InlineData(16)]
        [InlineData(32)]
        [InlineData(50)]
        public void ImagesShowTheAccentTile(int size)
        {
            using var bmp = Branding.Image(size);
            Assert.Equal(size, bmp.Width);
            var c = bmp.GetPixel((int)(size * 0.2), (int)(size * 0.8)); // free part of the tile, lower left
            Assert.True(c.B > 150 && c.R < 80 && c.A > 200, c.ToString());
            var dir = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "ui-renders");
            Directory.CreateDirectory(dir);
            bmp.Save(Path.Combine(dir, $"logo-{size}.png"));
        }

        [Fact]
        public void WindowIconLoads()
        {
            var icon = Branding.WindowIcon;
            Assert.True(icon.Width > 0);
            using var bmp = icon.ToBitmap();
            Assert.True(bmp.Width >= 16);
        }

        private static string Root()
        {
            var dir = new DirectoryInfo(System.AppDomain.CurrentDomain.BaseDirectory);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "InventaSketch.sln"))) dir = dir.Parent;
            return dir!.FullName;
        }
    }
}
