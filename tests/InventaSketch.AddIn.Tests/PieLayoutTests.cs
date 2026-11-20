using System.Linq;
using InventaSketch.AddIn.OnSlide;
using Xunit;

namespace InventaSketch.AddIn.Tests
{
    /// <summary>The pie menu keeps at most eight entries in its ring; the rest goes into "More" without duplicates.</summary>
    public class PieLayoutTests
    {
        private static (PieItem, bool) E(string id, bool essential) => (new PieItem(id, id), essential);

        [Fact]
        public void SmallMenusStayInOneRing()
        {
            var (main, overflow) = OnSlideController.PieLayout(new[] { E("delete", true), E("dup", true), E("a1", false) }, "More");
            Assert.Equal(new[] { "delete", "dup", "a1" }, main.Select(i => i.Id));
            Assert.Empty(overflow);
        }

        [Fact]
        public void LargeMenusOverflowIntoMoreWithoutDuplicates()
        {
            var all = new[]
            {
                E("delete", true), E("view:plan", true), E("view:elevation", true), E("dup", true), E("rot90", true),
                E("act:thicker", false), E("act:thinner", false), E("act:kind", false), E("act:thickness", false),
                E("act:closed", false), E("flipH", false), E("flipV", false),
            };
            var (main, overflow) = OnSlideController.PieLayout(all, "More");
            Assert.Equal(OnSlideController.MaxPieItems, main.Count);
            Assert.Equal("menu:more", main.Last().Id);
            Assert.Equal(new[] { "delete", "view:plan", "view:elevation", "dup", "rot90", "act:thicker", "act:thinner" }, main.Take(7).Select(i => i.Id));
            Assert.Equal(new[] { "act:kind", "act:thickness", "act:closed", "flipH", "flipV" }, overflow.Select(i => i.Id));
            Assert.Empty(main.Select(i => i.Id).Intersect(overflow.Select(i => i.Id)));
        }

        [Fact]
        public void TooManyEssentialEntriesAlsoOverflow()
        {
            var all = Enumerable.Range(0, 10).Select(i => E("e" + i, true)).ToArray();
            var (main, overflow) = OnSlideController.PieLayout(all, "More");
            Assert.Equal(8, main.Count);
            Assert.Equal(new[] { "e7", "e8", "e9" }, overflow.Select(i => i.Id));
        }
    }
}
