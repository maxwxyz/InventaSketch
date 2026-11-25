using System;
using System.Linq;
using InventaSketch.Modules.Charts.Scanning;
using Xunit;
using Xunit.Abstractions;

namespace InventaSketch.Modules.Charts.Tests
{
    /// <summary>Bar and column scanner (ported from Slidemax) on synthetic pictures.</summary>
    public class ScanningBarTests
    {
        private static readonly int Black = PixelColor.Rgb(0, 0, 0);
        private static readonly int Grid = PixelColor.Rgb(200, 200, 200);
        private static readonly int Blue = PixelColor.Rgb(31, 78, 121);
        private static readonly int Orange = PixelColor.Rgb(237, 125, 49);
        private static readonly int Green = PixelColor.Rgb(112, 173, 71);

        private readonly ITestOutputHelper _out;

        public ScanningBarTests(ITestOutputHelper output) => _out = output;

        /// <summary>Text-like specks (strokes), as labels in a picture.</summary>
        private static void Text(TestCanvas c, int x, int y)
        {
            for (int k = 0; k < 3; k++)
            {
                c.Fill(x + k * 5, y, 1, 8, Black);
                c.Fill(x + k * 5 + 1, y + 3, 2, 1, Black);
            }
        }

        /// <summary>Columns from the baseline y = 200 with axis, gridlines every 50 px and label specks.</summary>
        private static TestCanvas Columns(params int[] heights)
        {
            var c = new TestCanvas(400, 260);
            for (int g = 50; g <= 150; g += 50) c.Fill(20, 200 - g, 360, 1, Grid);
            for (int i = 0; i < heights.Length; i++)
            {
                int x = 30 + i * 60;
                c.Fill(x, 200 - heights[i], 36, heights[i], Blue);
                Text(c, x + 10, 200 - heights[i] - 14);
                Text(c, x + 8, 214);
            }
            c.Fill(20, 200, 360, 1, Black);
            return c;
        }

        private static double?[] Row(double?[,] values, int series = 0) => Enumerable.Range(0, values.GetLength(1)).Select(c => values[series, c]).ToArray();

        [Fact]
        public void SimpleColumns()
        {
            var result = BarScanner.Scan(Columns(50, 100, 75, 25, 150).ToImage());
            Assert.True(result.Found);
            Assert.Empty(result.Warnings);
            Assert.Equal(BarOrientation.Columns, result.Orientation);
            Assert.Equal(5, result.Categories);
            Assert.Equal(1, result.Series);
            Assert.Equal(200, result.Baseline);
            Assert.Equal(50, result.GridStep);
            var values = BarScanner.Values(result, new BarScale(0, BarScaleReference.LargestValue, 150));
            Assert.Equal(new double?[] { 50, 100, 75, 25, 150 }, Row(values));
            // The gridline step as reference gives the same numbers.
            Assert.Equal(new double?[] { 50, 100, 75, 25, 150 }, Row(BarScanner.Values(result, new BarScale(0, BarScaleReference.GridStep, 50))));
            Assert.Equal("5 categories, 1 series, columns, gridlines found", BarScanner.Describe(result).En);
            Assert.Equal("5 Kategorien, 1 Serien, Säulen, Gitternetz erkannt", BarScanner.Describe(result).De);
        }

        [Theory]
        [InlineData(false, 0)]
        [InlineData(true, 0)]
        [InlineData(true, 6)]
        public void ColumnsSurviveBlurAndNoise(bool blur, double noise)
        {
            double[] expected = { 50, 100, 75, 25, 150 };
            var c = Columns(50, 100, 75, 25, 150);
            if (blur) c.BoxBlur();
            if (noise > 0) c.Noise(noise, 5);
            var result = BarScanner.Scan(c.ToImage());
            Assert.Equal(5, result.Categories);
            var values = Row(BarScanner.Values(result, new BarScale(0, BarScaleReference.LargestValue, 150)));
            double worst = expected.Select((e, i) => Math.Abs(values[i]!.Value - e)).Max() / 150;
            _out.WriteLine($"bars (blur {blur}, noise {noise}): worst error {TestChart.Pct(worst)} of the largest value");
            Assert.True(worst < 0.02, TestChart.Pct(worst));
        }

        [Fact]
        public void LegendAndTextDoNotCount()
        {
            var c = Columns(40, 80);
            c.Fill(300, 20, 10, 10, Blue);
            Text(c, 315, 21);
            var result = BarScanner.Scan(c.ToImage());
            Assert.Equal(2, result.Categories);
            Assert.Equal(2, result.Segments.Count);
        }

        [Fact]
        public void StackedColumnsByColour()
        {
            var c = new TestCanvas(300, 240);
            int[][] parts = { new[] { 40, 30 }, new[] { 60, 20 }, new[] { 20, 50 } };
            for (int k = 0; k < parts.Length; k++)
            {
                int x = 30 + k * 80, y = 200;
                c.Fill(x, y - parts[k][0], 40, parts[k][0], Blue);
                y -= parts[k][0];
                // White separator between the parts (1 pixel).
                c.Fill(x, y - 1, 40, 1, PixelColor.White);
                c.Fill(x, y - parts[k][1], 40, parts[k][1] - 1, Orange);
            }
            c.Fill(10, 200, 280, 1, Black);
            var result = BarScanner.Scan(c.ToImage());
            Assert.False(result.Clustered);
            Assert.Equal(3, result.Categories);
            Assert.Equal(2, result.Series);
            var values = BarScanner.Values(result, new BarScale(0, BarScaleReference.LargestValue, 80));
            // First series on top of the stack (orange), second at the bottom (blue); the separator is shared (at most 1 pixel).
            Near(30, values[0, 0]);
            Near(40, values[1, 0]);
            Near(20, values[0, 1]);
            Near(60, values[1, 1]);
            Near(50, values[0, 2]);
            Near(20, values[1, 2]);
            Assert.Equal("3 categories, 2 series, columns, stacked", BarScanner.Describe(result).En);
        }

        private static void Near(double expected, double? actual) => Assert.InRange(actual!.Value, expected - 1, expected + 1);

        [Fact]
        public void ClusteredColumns()
        {
            var c = new TestCanvas(360, 240);
            int[,] heights = { { 50, 70 }, { 90, 30 }, { 60, 60 } };
            for (int k = 0; k < 3; k++)
            {
                int x = 30 + k * 100;
                c.Fill(x, 200 - heights[k, 0], 30, heights[k, 0], Blue);
                c.Fill(x + 30, 200 - heights[k, 1], 30, heights[k, 1], Green);
            }
            c.Fill(10, 200, 340, 1, Black);
            var result = BarScanner.Scan(c.ToImage());
            Assert.True(result.Clustered);
            Assert.Equal(3, result.Categories);
            Assert.Equal(2, result.Series);
            var values = BarScanner.Values(result, new BarScale(0, BarScaleReference.LargestValue, 90));
            Assert.Equal(new double?[] { 50, 90, 60 }, Row(values, 0));
            Assert.Equal(new double?[] { 70, 30, 60 }, Row(values, 1));
        }

        [Fact]
        public void HorizontalBarsWithNegativeValues()
        {
            // Bars to the right and to the left of the baseline x = 150.
            var c = new TestCanvas(320, 260);
            int[] lengths = { 80, -40, 120, -60 };
            for (int k = 0; k < lengths.Length; k++)
            {
                int y = 30 + k * 50, len = lengths[k];
                if (len > 0) c.Fill(150, y, len, 30, Orange);
                else c.Fill(150 + len, y, -len, 30, Orange);
            }
            c.Fill(150, 10, 1, 240, Black);
            var result = BarScanner.Scan(c.ToImage());
            Assert.Equal(BarOrientation.Bars, result.Orientation);
            Assert.Equal(4, result.Categories);
            var values = BarScanner.Values(result, new BarScale(0, BarScaleReference.LargestValue, 120));
            Assert.Equal(new double?[] { 80, -40, 120, -60 }, Row(values));
        }

        [Fact]
        public void TruncatedAxisUsesBaselineValue()
        {
            var result = BarScanner.Scan(Columns(50, 100).ToImage());
            var values = BarScanner.Values(result, new BarScale(1000, BarScaleReference.LargestValue, 1100));
            Assert.Equal(new double?[] { 1050, 1100 }, Row(values));
        }

        [Fact]
        public void DecimalsFollowThePixelResolution()
        {
            Assert.Equal(0, BarScanner.Decimals(1));
            Assert.Equal(1, BarScanner.Decimals(0.5));
            Assert.Equal(2, BarScanner.Decimals(0.02));
            Assert.Equal(0, BarScanner.Decimals(double.NaN));
        }
    }
}
