using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Modules.Charts.Scanning;
using Xunit;

namespace InventaSketch.Modules.Charts.Tests
{
    /// <summary>Empty, tiny, blank and inconsistent pictures give warnings, never exceptions.</summary>
    public class ScanningDegenerateTests
    {
        public static IEnumerable<object[]> Pictures()
        {
            yield return new object[] { "empty", new PixelImage(0, 0, Array.Empty<byte>()) };
            yield return new object[] { "null buffer", new PixelImage(10, 10, null!) };
            yield return new object[] { "short buffer", new PixelImage(10, 10, new byte[17]) };
            yield return new object[] { "negative size", new PixelImage(-3, 5, Array.Empty<byte>()) };
            yield return new object[] { "tiny", PixelImage.Blank(3, 3, PixelColor.Black) };
            yield return new object[] { "all white", PixelImage.Blank(200, 120, PixelColor.White) };
            yield return new object[] { "all black", PixelImage.Blank(200, 120, PixelColor.Black) };
            yield return new object[] { "transparent", new PixelImage(50, 40, new byte[50 * 40 * 4]) };
            var dot = new TestCanvas(80, 60);
            dot.Fill(40, 30, 2, 2, PixelColor.Black);
            yield return new object[] { "one dot", dot.ToImage() };
            var noise = new TestCanvas(120, 90);
            noise.Noise(120, 1);
            yield return new object[] { "pure noise", noise.ToImage() };
        }

        [Theory]
        [MemberData(nameof(Pictures))]
        public void ScannersReturnWarningsInsteadOfThrowing(string name, PixelImage image)
        {
            var curves = CurveDigitizer.Digitize(image, new DigitizeOptions { TextReader = DigitTextReader.Instance });
            Assert.NotNull(curves);
            if (name != "pure noise") Assert.False(curves.Found, name);
            Assert.NotEmpty(curves.Warnings);
            Assert.All(curves.Warnings, w => Assert.False(string.IsNullOrEmpty(w.De), name));

            var bars = BarScanner.Scan(image);
            Assert.NotNull(bars);
            if (name != "pure noise" && name != "all black")
            {
                Assert.False(bars.Found, name);
                Assert.NotEmpty(bars.Warnings);
            }

            Assert.NotNull(DigitTextReader.Instance.Read(image, null));
            Assert.Equal(string.Empty, ScanDataText.FromCurves(curves.Found ? new ScanResult() : curves));
            Assert.Equal(string.Empty, ScanDataText.FromBars(bars.Found ? BarScanResult.Empty : bars, new BarScale()));
            Assert.Same(BarLabels.Empty, BarLabelReader.Read(BarScanResult.Empty, Array.Empty<RecognizedText>()));
        }

        [Fact]
        public void AxesWithoutCurvesGiveAPlotAreaAndAWarning()
        {
            var result = CurveDigitizer.Digitize(TestChart.Frame().ToImage(), new DigitizeOptions { XAxis = TestChart.XAxis, YAxis = TestChart.YAxis });
            Assert.False(result.Found);
            Assert.NotNull(result.PlotArea);
            Assert.NotNull(result.XAxis);
            Assert.NotNull(result.YAxis);
            Assert.Contains(result.Warnings, w => w.En.StartsWith("No curves", StringComparison.Ordinal));
        }

        [Fact]
        public void PictureWithoutAxesIsStillTraced()
        {
            var c = new TestCanvas(300, 200);
            c.Polyline(Enumerable.Range(0, 260).Select(i => (20.0 + i, 100 + 60 * Math.Sin(i / 40.0))).ToList(), 2.5, TestChart.Red);
            var result = CurveDigitizer.Digitize(c.ToImage());
            var s = Assert.Single(result.Series);
            Assert.Null(result.XAxis);
            Assert.Contains(result.Warnings, w => w.En.StartsWith("Axes not found", StringComparison.Ordinal));
            Assert.All(s.PixelPoints.Where(p => p.X >= 21 && p.X <= 278), p => Assert.InRange(p.Y, 100 + 60 * Math.Sin((p.X - 20) / 40.0) - 1.5, 100 + 60 * Math.Sin((p.X - 20) / 40.0) + 1.5));
        }

        [Fact]
        public void BarChartPictureWarnsInTheDigitizer()
        {
            var c = new TestCanvas(300, 220);
            for (int i = 0; i < 4; i++) c.Fill(40 + i * 60, 200 - 40 * (i + 1), 40, 40 * (i + 1), PixelColor.Rgb(31, 78, 121));
            c.Fill(30, 200, 260, 2, PixelColor.Black);
            c.Fill(30, 20, 2, 182, PixelColor.Black);
            var result = CurveDigitizer.Digitize(c.ToImage());
            Assert.False(result.Found);
            Assert.Contains(result.Warnings, w => w.En.StartsWith("Filled areas", StringComparison.Ordinal));
        }

        [Fact]
        public void ThrowingTextReaderIsContained()
        {
            var c = TestChart.Frame();
            c.Polyline(TestChart.Curve(x => 10 + 8 * x), 2, TestChart.Blue);
            var result = CurveDigitizer.Digitize(c.ToImage(), new DigitizeOptions { TextReader = new ThrowingReader() });
            Assert.Single(result.Series);
            Assert.Contains(result.Warnings, w => w.En.StartsWith("The text recognition failed", StringComparison.Ordinal));
        }

        private sealed class ThrowingReader : ITextReader
        {
            public IReadOnlyList<RecognizedText> Read(PixelImage image, TextReadHints? hints) => throw new InvalidOperationException("host OCR unavailable");
        }

        [Fact]
        public void PixelImageHelpers()
        {
            var image = PixelImage.FromArgb(2, 1, new[] { PixelColor.Rgb(10, 20, 30), unchecked((int)0x00000000) });
            Assert.True(image.IsValid);
            Assert.Equal(PixelColor.Rgb(10, 20, 30), image[0, 0]);
            // Transparent is composed onto white; outside the picture is white.
            Assert.Equal(PixelColor.White, image[1, 0]);
            Assert.Equal(PixelColor.White, image[5, 5]);
            Assert.False(PixelImage.FromArgb(3, 3, new int[2]).IsValid);
            Assert.Equal("#0A141E", PixelColor.Hex(PixelColor.Rgb(10, 20, 30)));
        }

        [Fact]
        public void TextReaderGateSwitchesOffOnce()
        {
            var gate = new TextReaderGate();
            Assert.True(gate.Usable);
            Assert.NotNull(gate.TimedOut(500));
            Assert.Null(gate.TimedOut(500));
            Assert.True(gate.Usable);
            Assert.NotNull(gate.Failed("missing types"));
            Assert.False(gate.Usable);
            Assert.Null(gate.Failed("again"));
        }
    }
}
