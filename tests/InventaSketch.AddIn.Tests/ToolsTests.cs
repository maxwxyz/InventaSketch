using Xunit;

namespace InventaSketch.AddIn.Tests
{
    /// <summary>Extras menu helpers: decimal separator switch and default file names for sent/saved slides.</summary>
    public class ToolsTests
    {
        [Theory]
        [InlineData("1.5", ',', "1,5")]
        [InlineData("1,5", '.', "1.5")]
        [InlineData("1,5", ',', "1,5")]
        [InlineData("1.234,5", '.', "1,234.5")]
        [InlineData("1,234.5", '.', "1,234.5")]
        [InlineData("1.234.567", ',', "1.234.567")]
        public void SwitchesDecimalSeparators(string number, char separator, string expected)
            => Assert.Equal(expected, Tools.SwitchNumber(number, separator, separator == ',' ? '.' : ','));

        [Fact]
        public void DefaultNamesNameTheSlides()
        {
            Assert.Equal("Getriebe Folie 4.pptx", Tools.DefaultName("Getriebe.pptx", new[] { 4 }, german: true));
            Assert.Equal("Getriebe Folien 3-5.pptx", Tools.DefaultName("Getriebe.pptx", new[] { 5, 3, 4 }, german: true));
            Assert.Equal("Deck slides 1,4.pptx", Tools.DefaultName("Deck.pptx", new[] { 1, 4 }, german: false));
            Assert.Equal("Slides slide 2.pptx", Tools.DefaultName("", new[] { 2 }, german: false));
        }
    }
}
