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

        [Fact]
        public void BarShowsKeyActionsTellingIconsFirstThenTextButtons()
        {
            static InventaSketch.Core.ContextAction A(string id, string icon, bool enabled = true)
                => new(id, new InventaSketch.Core.I18nText(id, id), icon, o => o) { IsEnabled = enabled };
            var actions = new[]
            {
                A("custom", "true-size"), A("off", InventaSketch.Core.IconIds.Increase, enabled: false),
                A("ref", InventaSketch.Core.IconIds.Text), A("more", InventaSketch.Core.IconIds.Increase),
                A("toggle", InventaSketch.Core.IconIds.Toggle), A("more2", InventaSketch.Core.IconIds.Increase),
                A("less", InventaSketch.Core.IconIds.Decrease), A("flip", InventaSketch.Core.IconIds.Swap), A("fourth", InventaSketch.Core.IconIds.ArrowEnd),
            };
            Assert.Equal(new[] { "more", "less", "flip", "fourth", "custom" }, OnSlideController.BarActions(actions, hasTextButton: true).Select(a => a.Id));
            Assert.Equal(new[] { "ref", "more", "less", "flip", "fourth" }, OnSlideController.BarActions(actions, hasTextButton: false).Select(a => a.Id));
        }

        [Fact]
        public void ShortLabelsFitTheBar()
        {
            Assert.Equal("Helical teeth", OnSlideController.ShortLabel("Helical teeth on/off"));
            Assert.Equal("Schrägverzahnung", OnSlideController.ShortLabel("Schrägverzahnung an/aus"));
            Assert.Equal("Next centre", OnSlideController.ShortLabel("Next centre (closed, tandem)"));
            Assert.Equal(18, OnSlideController.ShortLabel("A very long action label indeed").Length);
        }
    }
}
