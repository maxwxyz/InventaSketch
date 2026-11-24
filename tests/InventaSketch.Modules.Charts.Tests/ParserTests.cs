using System;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Charts.Tests
{
    public class ParserTests
    {
        [Theory]
        [InlineData("2.5", 2.5)]
        [InlineData("2,5", 2.5)]
        [InlineData("-0,25", -0.25)]
        [InlineData("1.234,5", 1234.5)]
        [InlineData("1,234.5", 1234.5)]
        [InlineData("1.234.567", 1234567)]
        [InlineData("1e3", 1000)]
        [InlineData("2,5E-3", 0.0025)]
        [InlineData("−3", -3)]
        [InlineData("95 %", 95)]
        [InlineData("1 000", 1000)]
        public void NumbersAcceptInvariantAndGermanFormats(string text, double expected)
        {
            Assert.True(ChartData.TryNumber(text, out var v), text);
            Assert.Equal(expected, v, 9);
        }

        [Theory]
        [InlineData("")]
        [InlineData("abc")]
        [InlineData("\"2025\"")]
        [InlineData("NaN")]
        [InlineData("1/0")]
        public void NonNumbersAreRejected(string text) => Assert.False(ChartData.TryNumber(text, out _));

        [Fact]
        public void DotDecimalsWithSemicolonsGiveOneSeries()
        {
            var s = Assert.Single(ChartData.Xy(ChartData.Parse("0;1.5\n1;2.5\n2;3.25")));
            Assert.Equal(new[] { new Vec2(0, 1.5), new Vec2(1, 2.5), new Vec2(2, 3.25) }, s.Points);
            Assert.Null(s.Name);
        }

        [Fact]
        public void GermanDecimalCommasWithSemicolonsAndSpaces()
        {
            var a = Assert.Single(ChartData.Xy(ChartData.Parse("0;1,5\r\n1;2,5\r\n2;3,25")));
            Assert.Equal(new Vec2(2, 3.25), a.Points[2]);
            var b = Assert.Single(ChartData.Xy(ChartData.Parse("0 1,5\n1   2,5\n2 3,25")));
            Assert.Equal(new Vec2(1, 2.5), b.Points[1]);
        }

        [Fact]
        public void ExcelPasteWithTabsHeaderAndGermanCommas()
        {
            var text = "Drehzahl\tMessung\tModell\n1000\t12,5\t12\n2000\t18,25\t18\n3000\t\t21\n";
            var series = ChartData.Xy(ChartData.Parse(text));
            Assert.Equal(2, series.Count);
            Assert.Equal("Messung", series[0].Name);
            Assert.Equal("Modell", series[1].Name);
            // The empty cell is skipped.
            Assert.Equal(new[] { new Vec2(1000, 12.5), new Vec2(2000, 18.25) }, series[0].Points);
            Assert.Equal(3, series[1].Points.Count);
        }

        [Fact]
        public void BlankLinesAndHeadersSeparateSeries()
        {
            var text = "# comment\nFirst\n0;1\n1;2\n\n\n0;3\n1;4\nThird\n0;5\n1;6\n";
            var series = ChartData.Xy(ChartData.Parse(text));
            Assert.Equal(3, series.Count);
            Assert.Equal("First", series[0].Name);
            Assert.Null(series[1].Name);
            Assert.Equal("Third", series[2].Name);
            Assert.Equal(new Vec2(1, 4), series[1].Points[1]);
        }

        [Fact]
        public void PipeSeparatesRowsInSingleLineInput()
        {
            var series = ChartData.Xy(ChartData.Parse("0;1 | 1;2 || 0;2 | 1;3"));
            Assert.Equal(2, series.Count);
            Assert.Equal(new Vec2(1, 3), series[1].Points[1]);
        }

        [Fact]
        public void CommaSeparatedValuesAndSingleColumns()
        {
            var csv = Assert.Single(ChartData.Xy(ChartData.Parse("x,y\n1,5\n2,7")));
            Assert.Equal(new[] { new Vec2(1, 5), new Vec2(2, 7) }, csv.Points);
            var spaced = Assert.Single(ChartData.Xy(ChartData.Parse("1, 5\n2, 7")));
            Assert.Equal(new Vec2(2, 7), spaced.Points[1]);
            // One value per row: x = 1, 2, 3.
            var single = Assert.Single(ChartData.Xy(ChartData.Parse("3\n4.5\n6")));
            Assert.Equal(new[] { new Vec2(1, 3), new Vec2(2, 4.5), new Vec2(3, 6) }, single.Points);
        }

        [Fact]
        public void GarbageNeverThrows()
        {
            foreach (var text in new[] { null, "", "   ", "\n\n", ";;;", "a;b;c", "\"x\";\"y\"", "1;;;", "|||", "#only comment" })
            {
                var blocks = ChartData.Parse(text);
                _ = ChartData.Xy(blocks);
                _ = ChartData.Categories(blocks);
            }
            Assert.Empty(ChartData.Parse("a;b;c"));
        }

        [Fact]
        public void CategoriesFromLabelsColumnsAndBlocks()
        {
            var set = ChartData.Categories(ChartData.Parse("Quarter;A;B\nQ1;4;3\nQ2;5;4,5\nQ3;3,5;-"));
            Assert.Equal(new[] { "Q1", "Q2", "Q3" }, set.Categories);
            Assert.Equal(2, set.Series.Count);
            Assert.Equal("A", set.Series[0].Name);
            Assert.Equal(4.5, set.Series[1].Values[1]);
            Assert.True(double.IsNaN(set.Series[1].Values[2]));

            var years = ChartData.Categories(ChartData.Parse("\"Year\";\"2025\";\"2026\"\n2020;1;2\n2021;3;4"));
            Assert.Equal(new[] { "2020", "2021" }, years.Categories);
            Assert.Equal(new[] { "2025", "2026" }, years.Series.Select(s => s.Name));

            var blocks = ChartData.Categories(ChartData.Parse("Plan\nA;1\nB;2\n\nActual\nB;3\nC;4"));
            Assert.Equal(new[] { "A", "B", "C" }, blocks.Categories);
            Assert.Equal(new[] { "Plan", "Actual" }, blocks.Series.Select(s => s.Name));
            Assert.True(double.IsNaN(blocks.Series[1].Values[0]));
            Assert.Equal(3, blocks.Series[1].Values[1]);

            var plain = ChartData.Categories(ChartData.Parse("5\n7"));
            Assert.Equal(new[] { "1", "2" }, plain.Categories);
        }

        [Theory]
        [InlineData("0;100", 0, 100)]
        [InlineData("100;0", 0, 100)]
        [InlineData("0..100", 0, 100)]
        [InlineData("0,5;2,5", 0.5, 2.5)]
        [InlineData("-5 5", -5, 5)]
        [InlineData("1e-3\t1e3", 0.001, 1000)]
        public void RangesParse(string text, double min, double max)
        {
            var r = ChartData.Range(text);
            Assert.NotNull(r);
            Assert.Equal(min, r!.Value.Min, 9);
            Assert.Equal(max, r.Value.Max, 9);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("auto")]
        [InlineData("5")]
        [InlineData("3;3")]
        [InlineData("a;b")]
        public void InvalidRangesAreAuto(string? text) => Assert.Null(ChartData.Range(text));

        [Fact]
        public void NumberListsForLevelsAndLimits()
        {
            Assert.Equal(new[] { 80.0, 85, 90.5 }, ChartData.Numbers("80;85;90,5"));
            Assert.Equal(new[] { 80.0, 85 }, ChartData.Numbers("80 85"));
            Assert.Equal(new[] { 92.5 }, ChartData.Numbers("92,5"));
            Assert.Equal(new[] { 80.0, 85, 90 }, ChartData.Numbers("80,85,90"));
            Assert.Empty(ChartData.Numbers(""));
        }
    }
}
