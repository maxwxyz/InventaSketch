using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using InventaSketch.Core;
using InventaSketch.Modules.Charts.Scanning;
using Xunit;
using Xunit.Abstractions;

namespace InventaSketch.Modules.Charts.Tests
{
    /// <summary>
    /// Label reading: number styles, the built-in digit reader on stamped digits (14 px patterns of the first font in
    /// <see cref="DigitGlyphs"/>), axis calibration from labels for curves and bars, and the conversion to chart data text.
    /// </summary>
    public class ScanningLabelTests
    {
        private static readonly int Ink = PixelColor.Rgb(89, 89, 89);
        private static readonly int Blue = PixelColor.Rgb(31, 78, 121);

        private readonly ITestOutputHelper _out;

        public ScanningLabelTests(ITestOutputHelper output) => _out = output;

        private static readonly Lazy<Dictionary<char, (int W, int H, float[] Cov)>> Glyphs = new(() =>
        {
            var map = new Dictionary<char, (int, int, float[])>();
            foreach (var line in DigitGlyphs.Data.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var p = line.Split('|');
                int w = int.Parse(p[1], CultureInfo.InvariantCulture), h = int.Parse(p[2], CultureInfo.InvariantCulture);
                if (h != 14 || map.ContainsKey(p[0][0])) continue;
                map[p[0][0]] = (w, h, p[3].Select(c => int.Parse(c.ToString(), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 15f).ToArray());
            }
            return map;
        });

        /// <summary>Stamps a number; anchor: x is the centre (or the right edge), y the vertical centre.</summary>
        private static void Label(TestCanvas c, string text, double x, double y, bool right = false)
        {
            const int spacing = 2;
            int width = text.Sum(ch => Glyphs.Value[ch].W) + spacing * (text.Length - 1);
            int x0 = (int)Math.Round(right ? x - width : x - width / 2.0), y0 = (int)Math.Round(y - 7);
            foreach (var ch in text)
            {
                var (w, h, cov) = Glyphs.Value[ch];
                c.Stamp(x0, y0, w, h, cov, Ink);
                x0 += w + spacing;
            }
        }

        /// <summary>The XY test chart with value labels on both axes.</summary>
        private static TestCanvas LabelledChart()
        {
            var c = TestChart.Frame();
            for (int v = 0; v <= 100; v += 20) Label(c, v.ToString(CultureInfo.InvariantCulture), TestChart.Left - 8, TestChart.Py(v), right: true);
            for (int v = 0; v <= 10; v += 2) Label(c, v.ToString(CultureInfo.InvariantCulture), TestChart.Px(v), TestChart.Bottom + 17);
            return c;
        }

        [Fact]
        public void LabelNumberStyles()
        {
            Assert.Equal(1000, LabelNumber.Parse("1.000", german: true));
            Assert.Equal(1, LabelNumber.Parse("1.000", german: false));
            Assert.Equal(2.5, LabelNumber.Parse("2,5", german: true));
            Assert.Null(LabelNumber.Parse("2,5", german: false));
            Assert.Equal(2500, LabelNumber.Parse("2,500", german: false));
            Assert.Equal(-10, LabelNumber.Parse("−10", german: true));
            Assert.Equal(-10, LabelNumber.Parse("(10)", german: true));
            Assert.Equal(12, LabelNumber.Parse("12 %", german: true));
            Assert.Equal(1.5, LabelNumber.Parse("€ 1,5", german: true));
            Assert.Equal(128, LabelNumber.Parse("EUR 128m", german: false));
            Assert.Equal(1200000, LabelNumber.Parse("1 200 000", german: true));
            Assert.Equal(0, LabelNumber.Parse("O", german: true));
            Assert.Null(LabelNumber.Parse("1.00.0", german: true));
            Assert.Null(LabelNumber.Parse("Nord", german: true));
            Assert.Null(LabelNumber.Parse("Q1", german: true));
            Assert.Null(LabelNumber.Parse("1,", german: true));
            Assert.Null(LabelNumber.Parse(null, german: true));
        }

        [Fact]
        public void DigitReaderReadsStampedAxisLabels()
        {
            var image = LabelledChart().ToImage();
            var words = DigitTextReader.Instance.Read(image, null);
            var texts = words.Select(w => w.Text).ToList();
            _out.WriteLine("read: " + string.Join(" ", texts));
            foreach (var expected in new[] { "0", "20", "40", "60", "80", "100", "2", "4", "6", "8", "10" })
                Assert.Contains(expected, texts);
        }

        [Fact]
        public void CurvesAreCalibratedFromTheAxisLabels()
        {
            var c = LabelledChart();
            Func<double, double> wave = x => 50 + 35 * Math.Sin(0.8 * x);
            c.Polyline(TestChart.Curve(wave), 2.5, TestChart.Red);
            var result = CurveDigitizer.Digitize(c.ToImage(), new DigitizeOptions { TextReader = DigitTextReader.Instance });
            Assert.Equal(CalibrationSource.Labels, result.CalibrationSource);
            Assert.NotNull(result.Calibration);
            var cal = result.Calibration!;
            Assert.InRange(cal.X.ToValue(TestChart.Px(4)), 3.97, 4.03);
            Assert.InRange(cal.Y.ToValue(TestChart.Py(60)), 59.7, 60.3);
            Assert.Equal(12, result.AxisLabels.Count);
            var s = Assert.Single(result.Series);
            double max = TestChart.MaxError(s, wave);
            _out.WriteLine($"labels: max error {TestChart.Pct(max)}");
            Assert.True(max < 0.02, TestChart.Pct(max));
        }

        [Fact]
        public void UserCalibrationOverridesOneAxis()
        {
            var c = LabelledChart();
            c.Polyline(TestChart.Curve(x => 10 + 8 * x), 2, TestChart.Blue);
            // y from the user (in per cent instead of the labels), x from the labels.
            var y = new AxisCalibration(TestChart.Py(0), 0, TestChart.Py(100), 1);
            var result = CurveDigitizer.Digitize(c.ToImage(), new DigitizeOptions { TextReader = DigitTextReader.Instance, YAxis = y });
            Assert.Equal(CalibrationSource.Labels, result.CalibrationSource);
            var s = Assert.Single(result.Series);
            Assert.All(s.DataPoints!.Where(p => p.X > 0.5 && p.X < 9.5), p => Assert.InRange(p.Y, (10 + 8 * p.X) / 100 - 0.02, (10 + 8 * p.X) / 100 + 0.02));
        }

        [Fact]
        public void LogarithmicAxisFromLabels()
        {
            // Labels 1, 10, 100, 1000 evenly spaced on the y axis.
            var c = TestChart.Frame(grid: false);
            string[] labels = { "1", "10", "100", "1000" };
            for (int k = 0; k < labels.Length; k++) Label(c, labels[k], TestChart.Left - 8, TestChart.Py(k * 100 / 3.0), right: true);
            for (int v = 0; v <= 10; v += 2) Label(c, v.ToString(CultureInfo.InvariantCulture), TestChart.Px(v), TestChart.Bottom + 17);
            var words = DigitTextReader.Instance.Read(c.ToImage(), null);
            var (x, y, _) = CurveLabelCalibration.FromLabels(new PixelRect(TestChart.Left, TestChart.Top, TestChart.Right - TestChart.Left, TestChart.Bottom - TestChart.Top), words);
            Assert.NotNull(x);
            Assert.NotNull(y);
            Assert.True(y!.Logarithmic);
            Assert.InRange(y.ToValue(TestChart.Py(50)), 30, 33.5);
            Assert.False(x!.Logarithmic);
        }

        /// <summary>Columns on the baseline y = 200 (1 value per pixel), axis labels left, category numbers below.</summary>
        private static TestCanvas LabelledColumns(int[] heights)
        {
            var c = new TestCanvas(420, 260);
            for (int g = 50; g <= 150; g += 50) c.Fill(40, 200 - g, 370, 1, PixelColor.Rgb(217, 217, 217));
            for (int v = 0; v <= 150; v += 50) Label(c, v.ToString(CultureInfo.InvariantCulture), 34, 200 - v, right: true);
            for (int i = 0; i < heights.Length; i++)
            {
                int x = 50 + i * 70;
                c.Fill(x, 200 - heights[i], 40, heights[i], Blue);
                Label(c, (i + 1).ToString(CultureInfo.InvariantCulture), x + 20, 216);
            }
            c.Fill(40, 200, 370, 1, PixelColor.Rgb(0, 0, 0));
            return c;
        }

        [Fact]
        public void BarScaleAndCategoriesFromLabels()
        {
            int[] heights = { 35, 80, 55, 20, 95 };
            var image = LabelledColumns(heights).ToImage();
            var result = BarScanner.Scan(image);
            Assert.Equal(5, result.Categories);
            var labels = BarLabelReader.Read(result, DigitTextReader.Instance.Read(image, TextReadHints.From(result)));
            Assert.Equal(BarScaleSource.Axis, labels.Source);
            Assert.Equal(4, labels.ValueLabels.Count);
            Assert.Equal(0, labels.Scale!.BaselineValue);
            var values = BarScanner.Values(result, labels.Scale);
            for (int i = 0; i < heights.Length; i++) Assert.InRange(values[0, i]!.Value, heights[i] - 1, heights[i] + 1);
            Assert.Equal(new[] { "1", "2", "3", "4", "5" }, labels.Categories);

            // Data text for chart.bar: header row, quoted category names, values.
            var text = ScanDataText.FromBars(result, labels.Scale, labels.Categories);
            _out.WriteLine(text);
            var set = ChartData.Categories(ChartData.Parse(text));
            Assert.Equal(new[] { "1", "2", "3", "4", "5" }, set.Categories);
            var series = Assert.Single(set.Series);
            Assert.Equal("Series 1", series.Name);
            for (int i = 0; i < heights.Length; i++) Assert.InRange(series.Values[i], heights[i] - 1, heights[i] + 1);
            Assert.StartsWith("Kategorie;Serie 1", ScanDataText.FromBars(result, labels.Scale, null, null, Lang.De));
        }

        [Fact]
        public void CurvesBecomeXyDataText()
        {
            var c = TestChart.Frame();
            Func<double, double> wave = x => 50 + 35 * Math.Sin(0.8 * x), ramp = x => 10 + 8 * x;
            c.Polyline(TestChart.Curve(wave), 2.5, TestChart.Red);
            c.Polyline(TestChart.Curve(ramp), 2.5, TestChart.Blue);
            var result = CurveDigitizer.Digitize(c.ToImage(), new DigitizeOptions { XAxis = TestChart.XAxis, YAxis = TestChart.YAxis });
            var text = ScanDataText.FromCurves(result, maxPointsPerSeries: 50);
            var series = ChartData.Xy(ChartData.Parse(text));
            Assert.Equal(2, series.Count);
            Assert.All(series, s => Assert.InRange(s.Points.Count, 45, 50));
            // Each parsed series follows one of the functions (order is top to bottom in the picture).
            foreach (var s in series)
            {
                double ew = s.Points.Max(p => Math.Abs(p.Y - wave(p.X))), er = s.Points.Max(p => Math.Abs(p.Y - ramp(p.X)));
                Assert.True(Math.Min(ew, er) < 2, $"error {Math.Min(ew, er)}");
            }
            Assert.Contains("# series 1, solid, #", text);

            // Without calibration: pixels relative to the plot area, with a comment.
            var raw = ScanDataText.FromCurves(CurveDigitizer.Digitize(c.ToImage()));
            Assert.StartsWith("# pixels", raw);
            Assert.Equal(2, ChartData.Xy(ChartData.Parse(raw)).Count);
        }
    }
}
