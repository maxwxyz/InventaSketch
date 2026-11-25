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
    /// Tiny rasteriser for synthetic chart pictures: filled rectangles, anti-aliased thick (optionally dashed) polylines,
    /// glyph stamps, a 3 × 3 box blur (JPEG-like softening) and noise. Pixel centres lie at integer coordinates.
    /// </summary>
    internal sealed class TestCanvas
    {
        public readonly int W, H;
        private readonly double[] _r, _g, _b;

        public TestCanvas(int w, int h, int background = unchecked((int)0xFFFFFFFF))
        {
            W = w;
            H = h;
            _r = new double[w * h];
            _g = new double[w * h];
            _b = new double[w * h];
            for (int i = 0; i < w * h; i++)
            {
                _r[i] = PixelColor.R(background);
                _g[i] = PixelColor.G(background);
                _b[i] = PixelColor.B(background);
            }
        }

        public void Blend(int x, int y, int argb, double a)
        {
            if (a <= 0 || x < 0 || y < 0 || x >= W || y >= H) return;
            a = Math.Min(1, a);
            int i = y * W + x;
            _r[i] = _r[i] * (1 - a) + PixelColor.R(argb) * a;
            _g[i] = _g[i] * (1 - a) + PixelColor.G(argb) * a;
            _b[i] = _b[i] * (1 - a) + PixelColor.B(argb) * a;
        }

        public void Fill(int x, int y, int w, int h, int argb)
        {
            for (int yy = y; yy < y + h; yy++)
                for (int xx = x; xx < x + w; xx++)
                    Blend(xx, yy, argb, 1);
        }

        /// <summary>Anti-aliased polyline of the given width; with <paramref name="dash"/> &gt; 0 dashed (dash, gap along the arc).</summary>
        public void Polyline(IReadOnlyList<(double X, double Y)> points, double width, int argb, double dash = 0, double gap = 0)
        {
            var cov = new double[W * H];
            double arc = 0;
            for (int k = 0; k + 1 < points.Count; k++)
            {
                var (ax, ay) = points[k];
                var (bx, by) = points[k + 1];
                double len = Math.Sqrt((bx - ax) * (bx - ax) + (by - ay) * (by - ay));
                if (len < 1e-9) continue;
                if (dash <= 0) Segment(cov, ax, ay, bx, by, width);
                else
                {
                    // Pieces of this segment that fall on a dash.
                    double period = dash + gap, s = 0;
                    while (s < len)
                    {
                        double phase = (arc + s) % period;
                        double step = phase < dash ? Math.Min(dash - phase, len - s) : Math.Min(period - phase, len - s);
                        if (phase < dash)
                        {
                            double t0 = s / len, t1 = (s + step) / len;
                            Segment(cov, ax + (bx - ax) * t0, ay + (by - ay) * t0, ax + (bx - ax) * t1, ay + (by - ay) * t1, width, round: false);
                        }
                        s += Math.Max(step, 1e-6);
                    }
                }
                arc += len;
            }
            for (int i = 0; i < cov.Length; i++)
                if (cov[i] > 0) Blend(i % W, i / W, argb, cov[i]);
        }

        public void Line(double x0, double y0, double x1, double y1, double width, int argb) => Polyline(new[] { (x0, y0), (x1, y1) }, width, argb);

        private void Segment(double[] cov, double ax, double ay, double bx, double by, double width, bool round = true)
        {
            double r = width / 2 + 1;
            int x0 = Math.Max(0, (int)Math.Floor(Math.Min(ax, bx) - r)), x1 = Math.Min(W - 1, (int)Math.Ceiling(Math.Max(ax, bx) + r));
            int y0 = Math.Max(0, (int)Math.Floor(Math.Min(ay, by) - r)), y1 = Math.Min(H - 1, (int)Math.Ceiling(Math.Max(ay, by) + r));
            double dx = bx - ax, dy = by - ay, ll = dx * dx + dy * dy;
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    double t = ll < 1e-12 ? 0 : ((x - ax) * dx + (y - ay) * dy) / ll;
                    if (!round && (t < 0 || t > 1)) continue;
                    t = Math.Max(0, Math.Min(1, t));
                    double px = ax + t * dx - x, py = ay + t * dy - y;
                    double d = Math.Sqrt(px * px + py * py);
                    double c = Math.Max(0, Math.Min(1, width / 2 + 0.5 - d));
                    int i = y * W + x;
                    if (c > cov[i]) cov[i] = c;
                }
        }

        /// <summary>Stamps a coverage bitmap (0..1) with its top left corner at (x, y).</summary>
        public void Stamp(int x, int y, int w, int h, float[] coverage, int argb)
        {
            for (int j = 0; j < h; j++)
                for (int i = 0; i < w; i++)
                    Blend(x + i, y + j, argb, coverage[j * w + i]);
        }

        /// <summary>3 × 3 box filter (JPEG-like softening of edges).</summary>
        public void BoxBlur()
        {
            foreach (var ch in new[] { _r, _g, _b })
            {
                var copy = (double[])ch.Clone();
                for (int y = 0; y < H; y++)
                    for (int x = 0; x < W; x++)
                    {
                        double sum = 0;
                        int n = 0;
                        for (int dy = -1; dy <= 1; dy++)
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                int xx = x + dx, yy = y + dy;
                                if (xx < 0 || yy < 0 || xx >= W || yy >= H) continue;
                                sum += copy[yy * W + xx];
                                n++;
                            }
                        ch[y * W + x] = sum / n;
                    }
            }
        }

        /// <summary>Uniform noise ±<paramref name="amplitude"/> per channel.</summary>
        public void Noise(double amplitude, int seed)
        {
            var rnd = new Random(seed);
            for (int i = 0; i < _r.Length; i++)
            {
                _r[i] += (rnd.NextDouble() * 2 - 1) * amplitude;
                _g[i] += (rnd.NextDouble() * 2 - 1) * amplitude;
                _b[i] += (rnd.NextDouble() * 2 - 1) * amplitude;
            }
        }

        /// <summary>Random dark specks of one pixel (dust, compression artefacts).</summary>
        public void Specks(int count, int seed)
        {
            var rnd = new Random(seed);
            for (int k = 0; k < count; k++) Blend(rnd.Next(W), rnd.Next(H), PixelColor.Rgb(90, 90, 90), 0.8);
        }

        public PixelImage ToImage()
        {
            var bytes = new byte[W * H * 4];
            for (int i = 0; i < W * H; i++)
            {
                bytes[4 * i] = Byte(_r[i]);
                bytes[4 * i + 1] = Byte(_g[i]);
                bytes[4 * i + 2] = Byte(_b[i]);
                bytes[4 * i + 3] = 255;
            }
            return new PixelImage(W, H, bytes);
        }

        private static byte Byte(double v) => (byte)Math.Max(0, Math.Min(255, Math.Round(v)));
    }

    /// <summary>A synthetic XY chart: plot area 60..460 × 20..280 pixels for x 0..10 and y 0..100.</summary>
    internal static class TestChart
    {
        public const int Left = 60, Right = 460, Top = 20, Bottom = 280;
        public static readonly int Red = PixelColor.Rgb(220, 40, 40);
        public static readonly int Blue = PixelColor.Rgb(30, 90, 200);
        public static readonly int Black = PixelColor.Rgb(0, 0, 0);
        public static readonly int Grid = PixelColor.Rgb(215, 215, 215);

        public static double Px(double x) => Left + (Right - Left) * x / 10.0;
        public static double Py(double y) => Bottom - (Bottom - Top) * y / 100.0;

        public static readonly AxisCalibration XAxis = new(Px(0), 0, Px(10), 10);
        public static readonly AxisCalibration YAxis = new(Py(0), 0, Py(100), 100);

        /// <summary>Canvas with axes (2 px, black), ticks and optional light gridlines.</summary>
        public static TestCanvas Frame(bool grid = true, int width = 500, int height = 320)
        {
            var c = new TestCanvas(width, height);
            if (grid)
            {
                for (int v = 20; v <= 100; v += 20) c.Fill(Left, (int)Py(v), Right - Left + 1, 1, Grid);
                for (int v = 2; v <= 10; v += 2) c.Fill((int)Px(v), Top, 1, Bottom - Top, Grid);
            }
            c.Fill(Left - 1, Bottom, Right - Left + 2, 2, Black);
            c.Fill(Left - 1, Top, 2, Bottom - Top + 2, Black);
            for (int v = 0; v <= 10; v += 2) c.Fill((int)Px(v), Bottom + 2, 1, 4, Black);
            for (int v = 0; v <= 100; v += 20) c.Fill(Left - 5, (int)Py(v), 4, 1, Black);
            return c;
        }

        public static List<(double X, double Y)> Curve(Func<double, double> f, double from = 0, double to = 10)
        {
            var list = new List<(double, double)>();
            for (double px = Px(from); px <= Px(to) + 1e-9; px += 0.5)
            {
                double x = (px - Left) * 10.0 / (Right - Left);
                list.Add((px, Py(f(x))));
            }
            return list;
        }

        /// <summary>Largest deviation of the traced data points from the function, as a fraction of the y axis range (100).</summary>
        public static double MaxError(ScanSeries s, Func<double, double> f) => s.DataPoints!.Max(p => Math.Abs(p.Y - f(p.X))) / 100.0;

        /// <summary>Deviation at the 98th percentile (ignores single pixels at crossings and line ends).</summary>
        public static double P98Error(ScanSeries s, Func<double, double> f)
        {
            var e = s.DataPoints!.Select(p => Math.Abs(p.Y - f(p.X)) / 100.0).OrderBy(v => v).ToList();
            return e[(int)(0.98 * (e.Count - 1))];
        }

        public static string Pct(double v) => (100 * v).ToString("0.00", CultureInfo.InvariantCulture) + " %";

        /// <summary>One line per series and hatch (for test output).</summary>
        public static string Describe(ScanResult r) => string.Join("\n", r.Series.Select(s =>
                $"series {PixelColor.Hex(s.Color)} {s.Style} x {s.PixelPoints.First().X}..{s.PixelPoints.Last().X} y {s.PixelPoints.First().Y:0.0}..{s.PixelPoints.Last().Y:0.0} n {s.PixelPoints.Count} w {s.LineWidth:0.0} cov {s.Coverage:0.00}")
            .Concat(r.Hatches.Select(h => $"hatch {h.Bounds} {h.AngleDegrees} {h.Spacing}"))
            .Concat(r.Warnings.Select(w => "warning " + w.En)));
    }

    /// <summary>Curve digitiser on synthetic XY chart pictures.</summary>
    public class ScanningCurveTests
    {
        private readonly ITestOutputHelper _out;

        public ScanningCurveTests(ITestOutputHelper output) => _out = output;

        private static readonly Func<double, double> Wave = x => 50 + 35 * Math.Sin(0.8 * x);
        private static readonly Func<double, double> Ramp = x => 10 + 8 * x;
        private static readonly Func<double, double> Rising = x => 20 + 6 * x;
        private static readonly Func<double, double> Falling = x => 85 - 6 * x;

        private static DigitizeOptions Calibrated => new() { XAxis = TestChart.XAxis, YAxis = TestChart.YAxis };

        private static PixelImage TwoColours(bool blur, double noise)
        {
            var c = TestChart.Frame();
            c.Polyline(TestChart.Curve(Wave), 2.5, TestChart.Red);
            c.Polyline(TestChart.Curve(Ramp), 2.5, TestChart.Blue);
            if (blur) c.BoxBlur();
            if (noise > 0) c.Noise(noise, 7);
            return c.ToImage();
        }

        [Fact]
        public void PlotAreaAndAxesAreFound()
        {
            var result = CurveDigitizer.Digitize(TwoColours(false, 0), Calibrated);
            Assert.NotNull(result.PlotArea);
            var plot = result.PlotArea!;
            Assert.InRange(plot.X, TestChart.Left - 3, TestChart.Left + 1);
            Assert.InRange(plot.Y, TestChart.Top - 3, TestChart.Top + 2);
            Assert.InRange(plot.Right, TestChart.Right - 2, TestChart.Right + 3);
            Assert.InRange(plot.Bottom, TestChart.Bottom - 1, TestChart.Bottom + 3);
            Assert.NotNull(result.XAxis);
            Assert.NotNull(result.YAxis);
            Assert.True(result.XAxis!.Horizontal);
            Assert.InRange(result.XAxis.Position, TestChart.Bottom - 0.5, TestChart.Bottom + 1.5);
            Assert.InRange(result.YAxis!.Position, TestChart.Left - 1.5, TestChart.Left + 0.5);
            // Five horizontal and five vertical gridlines are masked out.
            Assert.Equal(5, result.Gridlines.Count(l => l.Horizontal));
            Assert.Equal(5, result.Gridlines.Count(l => !l.Horizontal));
            Assert.Equal(CalibrationSource.User, result.CalibrationSource);
        }

        [Theory]
        [InlineData(false, 0)]
        [InlineData(true, 0)]
        [InlineData(true, 10)]
        public void CrossingCurvesInTwoColoursAreTraced(bool blur, double noise)
        {
            var result = CurveDigitizer.Digitize(TwoColours(blur, noise), Calibrated);
            _out.WriteLine(TestChart.Describe(result));
            Assert.Equal(2, result.Series.Count);
            var red = result.Series.Single(s => PixelColor.R(s.Color) > PixelColor.B(s.Color));
            var blue = result.Series.Single(s => PixelColor.B(s.Color) > PixelColor.R(s.Color));
            foreach (var (s, f, name) in new[] { (red, Wave, "red"), (blue, Ramp, "blue") })
            {
                double max = TestChart.MaxError(s, f);
                _out.WriteLine($"{name} (blur {blur}, noise {noise}): max error {TestChart.Pct(max)}, 98 % {TestChart.Pct(TestChart.P98Error(s, f))}, {s.PixelPoints.Count} points, {s.Style}");
                Assert.True(max < 0.02, name + " error " + TestChart.Pct(max));
                Assert.Equal(CurveStyle.Solid, s.Style);
                // The whole x range is covered.
                Assert.InRange(s.DataPoints!.First().X, -0.1, 0.3);
                Assert.InRange(s.DataPoints!.Last().X, 9.7, 10.1);
                Assert.True(s.PixelPoints.Count > 0.9 * (TestChart.Right - TestChart.Left));
            }
        }

        private static PixelImage BlackAndWhite(bool blur, double noise, bool grid = true)
        {
            var c = TestChart.Frame(grid);
            c.Polyline(TestChart.Curve(Rising), 2, TestChart.Black);
            c.Polyline(TestChart.Curve(Falling), 2, TestChart.Black, dash: 10, gap: 6);
            if (blur) c.BoxBlur();
            if (noise > 0) c.Noise(noise, 3);
            return c.ToImage();
        }

        [Theory]
        [InlineData(false, 0, true)]
        [InlineData(false, 0, false)]
        [InlineData(true, 8, true)]
        public void DashedAndSolidBlackCurvesAreSeparated(bool blur, double noise, bool grid)
        {
            var result = CurveDigitizer.Digitize(BlackAndWhite(blur, noise, grid), Calibrated);
            _out.WriteLine(TestChart.Describe(result));
            Assert.Equal(2, result.Series.Count);
            var solid = result.Series.Single(s => s.Style == CurveStyle.Solid);
            var dashed = result.Series.Single(s => s.Style == CurveStyle.Dashed);
            foreach (var (s, f, name) in new[] { (solid, Rising, "solid"), (dashed, Falling, "dashed") })
            {
                double max = TestChart.MaxError(s, f);
                _out.WriteLine($"{name} (blur {blur}, noise {noise}, grid {grid}): max error {TestChart.Pct(max)}, coverage {s.Coverage:0.00}, {s.PixelPoints.Count} points");
                Assert.True(max < 0.02, name + " error " + TestChart.Pct(max));
                Assert.InRange(s.DataPoints!.First().X, 0, 0.5);
                Assert.InRange(s.DataPoints!.Last().X, 9.5, 10);
            }
            Assert.True(PixelColor.R(solid.Color) < 128 && PixelColor.Chroma(solid.Color) < 30, PixelColor.Hex(solid.Color));
        }

        [Fact]
        public void SteepCurveIsTraced()
        {
            Func<double, double> steep = x => 100 / (1 + Math.Exp(-4 * (x - 5)));
            var c = TestChart.Frame();
            c.Polyline(TestChart.Curve(steep), 2, TestChart.Blue);
            var result = CurveDigitizer.Digitize(c.ToImage(), Calibrated);
            var s = Assert.Single(result.Series);
            double max = TestChart.MaxError(s, steep);
            _out.WriteLine($"steep: max error {TestChart.Pct(max)}");
            Assert.True(max < 0.02, TestChart.Pct(max));
        }

        [Fact]
        public void DottedCurveIsRecognised()
        {
            var c = TestChart.Frame(grid: false);
            c.Polyline(TestChart.Curve(Rising), 2, TestChart.Black, dash: 2, gap: 5);
            var result = CurveDigitizer.Digitize(c.ToImage(), Calibrated);
            var s = Assert.Single(result.Series);
            Assert.Equal(CurveStyle.Dotted, s.Style);
            Assert.True(TestChart.MaxError(s, Rising) < 0.02);
        }

        [Fact]
        public void LegendAndSpecksDoNotBecomeCurves()
        {
            var c = TestChart.Frame();
            c.Polyline(TestChart.Curve(Wave), 2.5, TestChart.Red);
            // Legend inside the plot: a short line sample and a text-like block.
            c.Line(380, 40, 400, 40, 2.5, TestChart.Red);
            c.Fill(405, 36, 2, 8, TestChart.Black);
            c.Fill(409, 36, 2, 8, TestChart.Black);
            c.Specks(150, 11);
            var result = CurveDigitizer.Digitize(c.ToImage(), Calibrated);
            var s = Assert.Single(result.Series);
            Assert.True(TestChart.MaxError(s, Wave) < 0.02);
        }

        [Fact]
        public void HatchedRegionIsReportedNotTraced()
        {
            var c = TestChart.Frame(grid: false);
            c.Polyline(TestChart.Curve(Ramp), 2, TestChart.Black);
            // Hatched rectangle bottom right (pixels 300..400 × 190..270), 45° lines spaced 10 px horizontally.
            for (int k = 300 + 190; k <= 400 + 270; k += 10)
            {
                double xa = Math.Max(300, k - 270), xb = Math.Min(400, k - 190);
                if (xb - xa < 4) continue;
                c.Line(xa, k - xa, xb, k - xb, 1.5, TestChart.Black);
            }
            var result = CurveDigitizer.Digitize(c.ToImage(), Calibrated);
            _out.WriteLine(TestChart.Describe(result));
            var s = Assert.Single(result.Series);
            Assert.True(TestChart.MaxError(s, Ramp) < 0.02);
            var hatch = Assert.Single(result.Hatches);
            Assert.InRange(hatch.AngleDegrees, 40, 50);
            Assert.InRange(hatch.Bounds.CenterX, 330, 370);
            Assert.InRange(hatch.Bounds.CenterY, 210, 250);
        }

        [Fact]
        public void WithoutCalibrationPointsStayInPixelsWithAWarning()
        {
            var result = CurveDigitizer.Digitize(TwoColours(false, 0));
            Assert.Equal(2, result.Series.Count);
            Assert.Null(result.Calibration);
            Assert.Equal(CalibrationSource.None, result.CalibrationSource);
            Assert.All(result.Series, s => Assert.Null(s.DataPoints));
            Assert.Contains(result.Warnings, w => w.En.Contains("calibration") && !string.IsNullOrEmpty(w.De));
        }

        [Fact]
        public void LogarithmicAxisCalibration()
        {
            var axis = new AxisCalibration(280, 1, 20, 1000, Logarithmic: true);
            Assert.True(axis.IsValid);
            Assert.Equal(10, axis.ToValue(280 - 260 / 3.0), 9);
            Assert.Equal(280 - 2 * 260 / 3.0, axis.ToPixel(100), 9);
            Assert.False(new AxisCalibration(1, 0, 2, 10, Logarithmic: true).IsValid);
            Assert.False(new AxisCalibration(5, 0, 5, 10).IsValid);
        }
    }
}
