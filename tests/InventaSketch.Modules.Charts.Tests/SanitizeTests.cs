using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Charts.Tests
{
    /// <summary>Clean slides for sharing: every chart type normalised (same shape, neutral scale) and randomised (same look).</summary>
    public class SanitizeTests
    {
        private static readonly Registry Reg = new Registry().Register(new ChartsModule());

        private static SketchObject Item(string id) => Reg.Create(Reg.Item(id)!, new Vec2(100, 100));

        private static SketchObject Clean(SketchObject o, SanitizeMode mode, int seed = 5, bool rename = false, bool texts = false)
            => Reg.Type(o.Type)!.Sanitize(o, new SanitizeOptions { Mode = mode, Seed = seed, RenameCategories = rename, ReplaceTexts = texts }, new Random(seed));

        private static IReadOnlyList<XySeries> Xy(SketchObject o) => ChartData.Xy(ChartData.Parse(o.Str("data", XyChart.DefaultData)));

        private static double Correlation(IReadOnlyList<double> a, IReadOnlyList<double> b)
        {
            double ma = a.Average(), mb = b.Average();
            double sab = 0, saa = 0, sbb = 0;
            for (var i = 0; i < a.Count; i++) { sab += (a[i] - ma) * (b[i] - mb); saa += (a[i] - ma) * (a[i] - ma); sbb += (b[i] - mb) * (b[i] - mb); }
            return sab / Math.Sqrt(saa * sbb);
        }

        private static bool Increasing(IReadOnlyList<double> v) => v.Zip(v.Skip(1), (a, b) => b >= a).All(x => x);

        private static void RendersInsideItsBox(SketchObject o)
        {
            var prims = Reg.Render(o, new RenderContext(SketchDocument.Create()));
            Assert.NotEmpty(prims);
            var b = Prim.Bounds(prims);
            Assert.NotNull(b);
            var tol = Math.Max(o.Size.W, o.Size.H) * 0.1 + 2;
            var box = o.Bounds();
            Assert.InRange(b!.Value.X, box.X - tol, box.Right + tol);
            Assert.InRange(b.Value.Right, box.X - tol, box.Right + tol);
            Assert.InRange(b.Value.Y, box.Y - tol, box.Bottom + tol);
            Assert.InRange(b.Value.Bottom, box.Y - tol, box.Bottom + tol);
        }

        [Fact]
        public void NormalisedXyKeepsTheShapeOnZeroToOne()
        {
            var o = Item("chart.line.multi");
            var n = Clean(o, SanitizeMode.Normalised);
            var before = Xy(o); var after = Xy(n);
            Assert.Equal(before.Count, after.Count);
            for (var k = 0; k < before.Count; k++)
            {
                Assert.Equal(before[k].Points.Count, after[k].Points.Count);
                Assert.Equal(before[k].Name, after[k].Name);
                Assert.True(Correlation(before[k].Points.Select(p => p.Y).ToList(), after[k].Points.Select(p => p.Y).ToList()) > 0.9999);
                Assert.True(Correlation(before[k].Points.Select(p => p.X).ToList(), after[k].Points.Select(p => p.X).ToList()) > 0.9999);
            }
            var ys = after.SelectMany(s => s.Points.Select(p => p.Y)).ToList();
            var xs = after.SelectMany(s => s.Points.Select(p => p.X)).ToList();
            Assert.Equal(0, ys.Min(), 6); Assert.Equal(1, ys.Max(), 6);
            Assert.Equal(0, xs.Min(), 6); Assert.Equal(1, xs.Max(), 6);
            // Ratios between series are kept (one map for the whole chart).
            Assert.True(Correlation(before.SelectMany(s => s.Points.Select(p => p.Y)).ToList(), ys) > 0.9999);
            RendersInsideItsBox(n);
        }

        [Fact]
        public void NormalisedLimitsRangesAndUnitsFollow()
        {
            var o = Item("chart.limits").WithParam("yrange", "0;10").WithParam("yunit", "°C").WithParam("data", "0;2\n1;4\n2;6");
            var n = Clean(o, SanitizeMode.Normalised);
            // The y axis spans the data and the limit lines (1.5 … 6): limits 1.5 and 5.5, range 0 … 10.
            Assert.Equal(new[] { 0, 0.88889 }, ChartData.Numbers(n.Str("limits", "")).Select(v => Math.Round(v, 5)).ToArray());
            var yr = ChartData.Range(n.Str("yrange", ""))!.Value;
            Assert.Equal(-0.33333, yr.Min, 4);
            Assert.Equal(1.88889, yr.Max, 4);
            Assert.Equal("", n.Str("yunit", "x"));
        }

        [Fact]
        public void LogAxesAreNormalisedByTheirMaximum()
        {
            var o = Item("chart.line.logx");
            var n = Clean(o, SanitizeMode.Normalised);
            var xs = Xy(n)[0].Points.Select(p => p.X).ToList();
            Assert.Equal(1, xs.Max(), 6);
            Assert.True(xs.All(x => x > 0));
            Assert.Equal(1000, xs.Max() / xs.Min(), 3); // the decades stay
        }

        [Fact]
        public void RandomisedXyLooksAlikeButDiffers()
        {
            var o = Item("chart.line.multi");
            var r = Clean(o, SanitizeMode.Randomised, 9);
            var before = Xy(o); var after = Xy(r);
            Assert.Equal(before.Count, after.Count);
            for (var k = 0; k < before.Count; k++)
            {
                var a = before[k].Points.Select(p => p.Y).ToList();
                var b = after[k].Points.Select(p => p.Y).ToList();
                Assert.Equal(a.Count, b.Count);
                Assert.NotEqual(a, b);
                Assert.True(Increasing(b), "monotone series stays monotone");
                Assert.True(Correlation(a, b) > 0.8);
            }
            // Reproducible with the seed, different with another seed.
            Assert.Equal(r.Str("data", ""), Clean(o, SanitizeMode.Randomised, 9).Str("data", ""));
            Assert.NotEqual(r.Str("data", ""), Clean(o, SanitizeMode.Randomised, 10).Str("data", ""));
            RendersInsideItsBox(r);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        public void RandomisedPeaksStayPeaks(int seed)
        {
            var o = Item("chart.line").WithParam("data", "0;0\n1;1\n2;3\n3;7\n4;4\n5;2\n6;1.5\n7;3\n8;2");
            var r = Clean(o, SanitizeMode.Randomised, seed);
            var a = Xy(o)[0].Points.Select(p => p.Y).ToList();
            var b = Xy(r)[0].Points.Select(p => p.Y).ToList();
            for (var i = 1; i < a.Count; i++) Assert.Equal(Math.Sign(a[i] - a[i - 1]), Math.Sign(b[i] - b[i - 1]));
            Assert.Equal(a.IndexOf(a.Max()), b.IndexOf(b.Max()));
        }

        [Fact]
        public void BarsNormaliseToTheLargestBarAndRenameCategories()
        {
            var o = Item("chart.column.grouped");
            var n = Clean(o, SanitizeMode.Normalised, rename: true, texts: true);
            var before = ChartData.Categories(ChartData.Parse(o.Str("data", "")));
            var after = ChartData.Categories(ChartData.Parse(n.Str("data", "")));
            Assert.Equal(new[] { "A", "B", "C", "D" }, after.Categories);
            Assert.Equal(new[] { "Series 1", "Series 2" }, after.Series.Select(s => s.Name));
            var max = before.Series.SelectMany(s => s.Values).Max();
            for (var k = 0; k < before.Series.Count; k++)
                for (var i = 0; i < before.Categories.Count; i++)
                    Assert.Equal(before.Series[k].Values[i] / max, after.Series[k].Values[i], 4);
            RendersInsideItsBox(n);
        }

        [Fact]
        public void StackedBarsNormaliseToTheHighestStack()
        {
            var n = Clean(Item("chart.column.stacked"), SanitizeMode.Normalised);
            var set = ChartData.Categories(ChartData.Parse(n.Str("data", "")));
            var stacks = Enumerable.Range(0, set.Categories.Count).Select(i => set.Series.Sum(s => s.Values[i])).ToList();
            Assert.Equal(1, stacks.Max(), 4);
        }

        [Fact]
        public void RandomisedBarsKeepCountsAndSigns()
        {
            var o = Item("chart.column").WithParam("data", "A;4\nB;-2\nC;3\nD;5");
            var r = Clean(o, SanitizeMode.Randomised, 3);
            var a = ChartData.Categories(ChartData.Parse(o.Str("data", ""))).Series[0].Values;
            var b = ChartData.Categories(ChartData.Parse(r.Str("data", ""))).Series[0].Values;
            Assert.Equal(a.Count, b.Count);
            for (var i = 0; i < a.Count; i++) { Assert.Equal(Math.Sign(a[i]), Math.Sign(b[i])); Assert.NotEqual(a[i], b[i]); }
        }

        [Fact]
        public void YearColumnsBecomeLettersWhenCategoriesAreRenamed()
        {
            var o = Item("chart.column").WithParam("data", "Year;Sales;Cost\n2022;4;3\n2023;5;4\n2024;6;5");
            var n = Clean(o, SanitizeMode.Normalised, rename: true);
            var set = ChartData.Categories(ChartData.Parse(n.Str("data", "")));
            Assert.Equal(new[] { "A", "B", "C" }, set.Categories);
            Assert.Equal(new[] { "Sales", "Cost" }, set.Series.Select(s => s.Name));
            var named = ChartData.Categories(ChartData.Parse(Clean(o, SanitizeMode.None, texts: true).Str("data", "")));
            Assert.Equal(new[] { "2022", "2023", "2024" }, named.Categories);
            Assert.Equal(new[] { "Series 1", "Series 2" }, named.Series.Select(s => s.Name));
            Assert.Equal(2, set.Series.Count);
            Assert.Equal(1, set.Series[0].Values[2], 4);
        }

        [Fact]
        public void TorqueCurveIsNormalisedToRatedValues()
        {
            var o = Item("chart.torque");
            var n = Clean(o, SanitizeMode.Normalised);
            Assert.Equal(1, n.Num("tmax", 0));
            Assert.Equal(1, n.Num("nmax", 0));
            Assert.Equal(0.333, n.Num("nbase", 0), 3);
            Assert.True(n.Parts!["label.y2"].Hidden);
            Assert.Equal("", n.Str("xunit", "x"));
            RendersInsideItsBox(n);
            var r = Clean(o, SanitizeMode.Randomised, 4);
            Assert.NotEqual(300, r.Num("tmax", 300));
            Assert.NotEqual(12000, r.Num("nmax", 12000));
            Assert.InRange(r.Num("nbase", 0) / r.Num("nmax", 1), 0.05, 0.95);
        }

        [Fact]
        public void EfficiencyMapKeepsItsContours()
        {
            var o = Item("chart.efficiency");
            double Odds(double l) => l / (100 - l);
            foreach (var mode in new[] { SanitizeMode.Normalised, SanitizeMode.Randomised })
            {
                var c = Clean(o, mode, 8);
                var p0 = o.Num("etamax", 96); var p1 = c.Num("etamax", 0);
                Assert.NotEqual(p0, p1);
                var l0 = ChartData.Numbers(o.Str("levels", EfficiencyChart.DefaultLevels));
                var l1 = ChartData.Numbers(c.Str("levels", ""));
                Assert.Equal(l0.Count, l1.Count);
                for (var i = 0; i < l0.Count; i++) Assert.InRange(Odds(l1[i]) / Odds(p1) / (Odds(l0[i]) / Odds(p0)), 0.99, 1.01);
                RendersInsideItsBox(c);
            }
            Assert.False(Clean(o, SanitizeMode.Normalised).Flag("labels", true));
        }

        [Fact]
        public void PumpAndFanCurvesKeepTheirOperatingPointRatio()
        {
            foreach (var id in new[] { "chart.pump", "chart.fan" })
            {
                var o = Item(id);
                var n = Clean(o, SanitizeMode.Normalised);
                Assert.Equal(1, n.Num("h0", 0));
                Assert.Equal(1, n.Num("qmax", 0));
                Assert.Equal(o.Num("qop", 70) / o.Num("qmax", 100), n.Num("qop", 0), 3);
                var r = Clean(o, SanitizeMode.Randomised, 2);
                Assert.NotEqual(o.Num("h0", 40), r.Num("h0", 40));
                Assert.InRange(r.Num("qop", 0) / r.Num("qmax", 1), 0.05, 0.95);
                RendersInsideItsBox(n); RendersInsideItsBox(r);
            }
        }

        [Fact]
        public void BodePlotIsNormalisedToTheCornerFrequency()
        {
            var o = Item("chart.bode").WithParam("fc", 2500.0).WithParam("gain", 12.0);
            var n = Clean(o, SanitizeMode.Normalised);
            Assert.Equal(1, n.Num("fc", 0));
            Assert.Equal(0, n.Num("gain", 1));
            Assert.Equal(0.3, n.Num("zeta", 0));
            for (var seed = 1; seed < 6; seed++)
            {
                var r = Clean(o, SanitizeMode.Randomised, seed);
                Assert.True(r.Num("zeta", 1) < 0.7071, "a resonance peak stays a peak");
                Assert.NotEqual(2500, r.Num("fc", 2500));
            }
            var measured = o.WithParam("data", "100;0;-5\n1000;-1;-45\n10000;-20;-85\n100000;-40;-90");
            var m = Clean(measured, SanitizeMode.Normalised);
            var rows = ChartData.Parse(m.Str("data", "")).SelectMany(b => b.Rows).ToList();
            Assert.Equal(0.04, rows[0].Values[0], 6);
            Assert.Equal(0, rows.Max(r => r.Values[1]), 6);
        }

        [Fact]
        public void ToleranceBandKeepsItsShape()
        {
            var o = Item("chart.band.measured");
            var n = Clean(o, SanitizeMode.Normalised);
            var b0 = ChartData.Parse(o.Str("data", ""));
            var b1 = ChartData.Parse(n.Str("data", ""));
            Assert.Equal(b0.Count, b1.Count);
            var (nom0, lo0, hi0) = BandChart.Curves(b0[0], 10, true);
            var (nom1, lo1, hi1) = BandChart.Curves(b1[0], 10, true);
            Assert.Equal(nom0.Count, nom1.Count);
            Assert.True(Correlation(hi0.Zip(lo0, (h, l) => h.Y - l.Y).ToList(), hi1.Zip(lo1, (h, l) => h.Y - l.Y).ToList()) > 0.9999);
            Assert.InRange(lo1.Min(p => p.Y), -1e-6, 1);
            Assert.InRange(hi1.Max(p => p.Y), 0, 1 + 1e-6);
            var r = Clean(o, SanitizeMode.Randomised, 6);
            var (nr, lr, hr) = BandChart.Curves(ChartData.Parse(r.Str("data", ""))[0], 10, true);
            Assert.Equal(nom0.Count, nr.Count);
            for (var i = 0; i < nr.Count; i++) Assert.InRange(nr[i].Y, lr[i].Y - 1e-9, hr[i].Y + 1e-9);
            Assert.True(Increasing(nr.Select(p => p.Y).ToList()));
            RendersInsideItsBox(n); RendersInsideItsBox(r);
        }

        [Fact]
        public void AxesRangesBecomeNeutral()
        {
            var n = Clean(Item("chart.axes"), SanitizeMode.Normalised);
            Assert.Equal((0.0, 1.0), ChartData.Range(n.Str("xrange", "")));
            var log = Clean(Item("chart.axes.loglog"), SanitizeMode.Normalised);
            Assert.Equal((0.001, 1.0), ChartData.Range(log.Str("xrange", "")));
        }

        [Fact]
        public void FiguresMapEveryCoordinateAndReplaceTexts()
        {
            foreach (var item in new[] { "chart.figure.phases", "chart.figure.timing", "chart.figure.temperature" })
            {
                var o = Item(item);
                var n = Clean(o, SanitizeMode.Normalised, texts: true);
                Assert.True(FigureSpec.TryParse(o.Str("spec", ""), out var before, out _));
                Assert.True(FigureSpec.TryParse(n.Str("spec", ""), out var after, out _));
                Assert.Equal(before!.Series.Count, after!.Series.Count);
                for (var k = 0; k < before.Series.Count; k++)
                {
                    Assert.Equal(before.Series[k].Y.Count, after.Series[k].Y.Count);
                    var ys = after.Series[k].Y.Where(Fmt.Finite).ToList();
                    Assert.All(ys, y => Assert.InRange(y, -1e-6, 1 + 1e-6));
                    var a = before.Series[k].Y.Where(Fmt.Finite).ToList();
                    if (a.Distinct().Count() > 2) Assert.True(Correlation(a, ys) > 0.9999);
                    if (before.Series[k].Label is not null) Assert.NotEqual(before.Series[k].Label!.En, after.Series[k].Label!.En);
                }
                if (before.Title is not null) Assert.NotEqual(before.Title.En, after.Title!.En);
                RendersInsideItsBox(n);
                var r1 = Clean(o, SanitizeMode.Randomised, 3);
                Assert.Equal(r1.Str("spec", ""), Clean(o, SanitizeMode.Randomised, 3).Str("spec", ""));
                Assert.NotEqual(o.Str("spec", ""), r1.Str("spec", ""));
                RendersInsideItsBox(r1);
            }
        }

        [Fact]
        public void TextsOnlyModeChangesNoValues()
        {
            var o = Item("chart.line.multi").WithParam("title", "Secret programme").WithParam("ylabel", "Range of model X");
            var t = Clean(o, SanitizeMode.None, texts: true);
            Assert.DoesNotContain("Secret", t.Str("title", ""));
            Assert.Equal("y", t.Str("ylabel", ""));
            var a = Xy(o); var b = Xy(t);
            Assert.Equal(a.SelectMany(s => s.Points), b.SelectMany(s => s.Points));
            Assert.Equal(new[] { "Series 1", "Series 2", "Series 3" }, b.Select(s => s.Name));
            var tq = Item("chart.torque");
            Assert.Same(tq, Clean(tq, SanitizeMode.None));
        }

        [Fact]
        public void EveryChartItemCleansAndRendersInBothModes()
        {
            foreach (var item in Reg.AllItems)
                foreach (var mode in new[] { SanitizeMode.Normalised, SanitizeMode.Randomised })
                {
                    var o = Reg.Create(item, new Vec2(0, 0));
                    var c = Clean(o, mode, 13, rename: true, texts: true);
                    RendersInsideItsBox(c);
                }
        }
    }
}
