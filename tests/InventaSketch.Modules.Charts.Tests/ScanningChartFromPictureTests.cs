using System;
using System.Linq;
using InventaSketch.Core;
using InventaSketch.Modules.Charts.Scanning;
using Xunit;

namespace InventaSketch.Modules.Charts.Tests
{
    /// <summary>Host entry point of charts from pictures: bars become a column chart, curves an XY chart, blanks a message.</summary>
    public class ScanningChartFromPictureTests
    {
        private static readonly int Blue = PixelColor.Rgb(30, 90, 200);
        private static readonly int Black = PixelColor.Rgb(0, 0, 0);

        [Fact]
        public void ColumnsBecomeAColumnChartWithTheirData()
        {
            var c = new TestCanvas(400, 260);
            int[] heights = { 50, 100, 75, 25, 150 };
            for (int i = 0; i < heights.Length; i++) c.Fill(30 + i * 60, 200 - heights[i], 36, heights[i], Blue);
            c.Fill(20, 200, 360, 1, Black);
            var r = Fx.Reg();
            var result = ChartFromPicture.Build(r, c.ToImage(), new Vec2(100, 80), Lang.De);
            Assert.NotNull(result.Chart);
            Assert.True(result.Bars);
            Assert.Equal("chart.bar", result.Chart!.Type);
            var data = ChartData.Categories(ChartData.Parse(result.Chart.Str("data", "")), Lang.De);
            Assert.Equal(5, data.Categories.Count);
            // Without readable axis numbers the largest bar is 100.
            Assert.Equal(100, data.Series[0].Values.Max(), 0);
            Assert.Equal(new[] { 33.3, 66.7, 50, 16.7, 100 }, data.Series[0].Values.Select(v => Math.Round(v, 1)));
            Assert.Contains("100", result.Message.De);
            Fx.AssertInsideBox(Fx.Render(r, result.Chart), result.Chart.Size, "picture chart");
        }

        [Fact]
        public void CurvesBecomeAnXyChart()
        {
            var c = TestChart.Frame();
            c.Polyline(TestChart.Curve(x => 50 + 40 * Math.Sin(x)), 2.5, TestChart.Red);
            var r = Fx.Reg();
            var result = ChartFromPicture.Build(r, c.ToImage(), new Vec2(100, 80), Lang.En);
            Assert.NotNull(result.Chart);
            Assert.False(result.Bars);
            Assert.Equal("chart.xy", result.Chart!.Type);
            Assert.Equal(1, result.Series);
            Assert.NotEmpty(ChartData.Xy(ChartData.Parse(result.Chart.Str("data", "")))[0].Points);
            Assert.Equal(110, Math.Max(result.Chart.Size.W, result.Chart.Size.H), 0);
        }

        [Fact]
        public void ABlankPictureGivesAMessageInsteadOfAChart()
        {
            var c = new TestCanvas(200, 150);
            var result = ChartFromPicture.Build(Fx.Reg(), c.ToImage(), Vec2.Zero, Lang.En);
            Assert.Null(result.Chart);
            Assert.False(string.IsNullOrEmpty(result.Message.En));
            Assert.Null(ChartFromPicture.Build(Fx.Reg(), new PixelImage(0, 0, Array.Empty<byte>()), Vec2.Zero, Lang.De).Chart);
        }
    }
}
