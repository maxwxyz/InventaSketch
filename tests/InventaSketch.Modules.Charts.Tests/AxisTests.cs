using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Charts.Tests
{
    public class AxisTests
    {
        [Theory]
        [InlineData(0.7, 1)]
        [InlineData(1.0, 1)]
        [InlineData(1.3, 2)]
        [InlineData(2.2, 2.5)]
        [InlineData(3, 5)]
        [InlineData(7, 10)]
        [InlineData(0.023, 0.025)]
        [InlineData(1800, 2000)]
        public void NiceSteps(double raw, double expected) => Assert.Equal(expected, Axis.NiceStep(raw), 9);

        [Fact]
        public void LinearAutoRangeExtendsToNiceTicks()
        {
            var a = Axis.Linear(new[] { 0.3, 9.2 });
            Assert.Equal(0, a.Min);
            Assert.Equal(10, a.Max);
            Assert.Equal(new[] { 0.0, 2, 4, 6, 8, 10 }, a.Ticks(Lang.En).Select(t => t.Value));
            Assert.All(a.Ticks(Lang.En), t => Assert.Equal(TickMark.Major, t.Mark));
            Assert.Equal(new[] { "0", "2", "4", "6", "8", "10" }, a.Ticks(Lang.En).Select(t => t.Label));
        }

        [Fact]
        public void LinearIncludesZeroAndHandlesNegatives()
        {
            var a = Axis.Linear(new[] { 3.0, 7.0 }, includeZero: true);
            Assert.Equal(0, a.Min);
            var b = Axis.Linear(new[] { -35.0, 12.0 });
            Assert.True(b.Min <= -35 && b.Max >= 12);
            Assert.Contains(b.Ticks(Lang.En), t => t.Value == 0 && t.Label == "0");
        }

        [Fact]
        public void LinearDecimalLabelsUseGermanComma()
        {
            var a = Axis.Linear(new[] { 0.0, 1.0 }, target: 4);
            Assert.Equal(0.25, a.Step, 9);
            Assert.Equal(2, a.Decimals);
            Assert.Contains("0.25", a.Ticks(Lang.En).Select(t => t.Label));
            Assert.Contains("0,25", a.Ticks(Lang.De).Select(t => t.Label));
        }

        [Fact]
        public void LinearFixedRangeKeepsBounds()
        {
            var a = Axis.Linear(new[] { 0.0, 1000 }, (1.5, 8.5));
            Assert.Equal(1.5, a.Min);
            Assert.Equal(8.5, a.Max);
            Assert.All(a.Ticks(Lang.En), t => Assert.InRange(t.Value, 1.5, 8.5));
            Assert.Equal(0, a.T(1.5), 9);
            Assert.Equal(1, a.T(8.5), 9);
        }

        [Fact]
        public void ConstantDataGetsARange()
        {
            var a = Axis.Linear(new[] { 5.0, 5.0 });
            Assert.True(a.Max > a.Min);
            Assert.True(a.Contains(5));
            var e = Axis.Linear(System.Array.Empty<double>());
            Assert.Equal(0, e.Min);
            Assert.Equal(1, e.Max);
        }

        [Fact]
        public void PhaseAxisUsesDegreeSteps()
        {
            var a = Axis.Linear(new[] { -180.0, 0 }, target: 4, steps: new double[] { 15, 30, 45, 90, 180 });
            Assert.Equal(45, a.Step);
            Assert.Equal(-180, a.Min);
            Assert.Equal(0, a.Max);
        }

        [Fact]
        public void LogAxisDecadesAndMinorTicks()
        {
            var a = Axis.Logarithmic(new[] { 3.0, 4500 });
            Assert.Equal(1, a.Min, 9);
            Assert.Equal(10000, a.Max, 9);
            var major = a.Ticks(Lang.En).Where(t => t.IsMajor).Select(t => t.Value).ToList();
            Assert.Equal(new[] { 1.0, 10, 100, 1000, 10000 }, major);
            var minor = a.Ticks(Lang.En).Where(t => t.Mark == TickMark.Minor).Select(t => t.Value).ToList();
            Assert.Equal(4 * 8, minor.Count);
            Assert.Contains(200.0, minor);
            Assert.Equal(new[] { "1", "10", "100", "1000", "10k" }, a.Ticks(Lang.En).Where(t => t.Label is not null).Select(t => t.Label));
            Assert.Equal(0.5, a.T(100), 9);
            Assert.True(double.IsNaN(a.T(-1)));
        }

        [Fact]
        public void ShortLogAxisLabelsTwoAndFive()
        {
            var a = Axis.Logarithmic(new[] { 0.5, 8.0 });
            var labels = a.Ticks(Lang.De).Where(t => t.Label is not null).Select(t => t.Label).ToList();
            Assert.Equal(new[] { "0,1", "0,2", "0,5", "1", "2", "5", "10" }, labels);
        }

        [Fact]
        public void LogAxisFixedRangeAndFallbacks()
        {
            var a = Axis.Logarithmic(new[] { 1.0 }, (20, 20000));
            Assert.Equal(20, a.Min);
            Assert.Equal(20000, a.Max);
            Assert.Equal(new[] { 100.0, 1000, 10000 }, a.Ticks(Lang.En).Where(t => t.IsMajor).Select(t => t.Value));
            var none = Axis.Logarithmic(new[] { -1.0, 0 });
            Assert.True(none.Min > 0 && none.Max > none.Min);
            // Ranges with non-positive bounds fall back to the data.
            var b = Axis.Logarithmic(new[] { 5.0, 50 }, (-1, 10));
            Assert.Equal(1, b.Min, 9);
        }

        [Theory]
        [InlineData(1e6, "1M")]
        [InlineData(2.5e4, "25k")]
        [InlineData(1e-4, "1e-4")]
        [InlineData(0.01, "0.01")]
        [InlineData(1000, "1000")]
        public void LogLabels(double v, string text) => Assert.Equal(text, Axis.LogLabel(v, Lang.En));

        [Fact]
        public void CategoryAxisBands()
        {
            var a = Axis.Category(new[] { "A", "B", "C", "D" });
            Assert.Equal(0.125, a.T(0.5), 9);
            Assert.Equal(new[] { "A", "B", "C", "D" }, a.Ticks(Lang.En).Where(t => t.Label is not null).Select(t => t.Label));
            Assert.Equal(5, a.Ticks(Lang.En).Count(t => t.IsMajor));
            var r = Axis.Category(new[] { "A", "B" }, reversed: true);
            Assert.Equal(0.75, r.T(0.5), 9);
        }
    }
}
