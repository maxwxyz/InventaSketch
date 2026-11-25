using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Core.Tests
{
    /// <summary>All capitals as a text override (ribbon toggle "All caps"): shown in capitals, the model text keeps its case.</summary>
    public class CapsTests
    {
        private static readonly TextPrim Text = new(new Vec2(0, 0), "Gear z1", new TextStyle());

        [Fact]
        public void CapsShowsTheTextInCapitals()
        {
            var shown = (TextPrim)Prim.TextOverride(Text, new Style { Caps = true });
            Assert.Equal("GEAR Z1", shown.Text);
        }

        [Fact]
        public void WithoutCapsTheTextKeepsItsCase()
        {
            Assert.Equal("Gear z1", ((TextPrim)Prim.TextOverride(Text, new Style { Caps = false })).Text);
            Assert.Equal("Gear z1", ((TextPrim)Prim.TextOverride(Text, new Style { Bold = true })).Text);
        }

        [Fact]
        public void CapsCountsAsATextOverrideAndMerges()
        {
            Assert.True(new Style { Caps = true }.HasTextOverride);
            Assert.True(new Style { Bold = true }.Merge(new Style { Caps = true }).Caps);
            Assert.Null(new Style().Merge(new Style { Bold = true }).Caps);
        }
    }
}
