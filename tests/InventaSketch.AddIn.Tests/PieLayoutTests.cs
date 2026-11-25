using System.Collections.Generic;
using System.Linq;
using InventaSketch.AddIn.OnSlide;
using Xunit;

namespace InventaSketch.AddIn.Tests
{
    /// <summary>
    /// The pie menu has eight fixed places (text, edit, insert, duplicate, style, delete, view, arrange) for every element;
    /// groups open submenus, empty places stay greyed, long submenus continue in "More"; the bar shows pictograms only when
    /// they are unambiguous.
    /// </summary>
    public class PieLayoutTests
    {
        private static PieItem I(string id) => new(id, id);

        private static OnSlideController.PieGroup G(string key, params PieItem[] items) => new(key, key + " >", null, items);

        private static List<OnSlideController.PieGroup> Groups(int editCount, int styleCount = 0, int viewCount = 0)
            => new()
            {
                G("text", I("text")),
                G("edit", Enumerable.Range(0, editCount).Select(i => I("act:" + i)).ToArray()) with { AlwaysMenu = true },
                G("insert", I("insert")),
                G("duplicate", I("dup")),
                G("style", Enumerable.Range(0, styleCount).Select(i => I("menu:c:" + i)).ToArray()) with { AlwaysMenu = true },
                G("delete", I("delete")),
                G("view", Enumerable.Range(0, viewCount).Select(i => I("view:" + i)).ToArray()),
                G("arrange"),
            };

        [Fact]
        public void EveryElementGetsTheSameEightPlaces()
        {
            var (ring, menus) = OnSlideController.PieRing(Groups(3, 2, 2), "More");
            Assert.Equal(new[] { "text", "menu:g:edit", "insert", "dup", "menu:g:style", "delete", "menu:g:view", "menu:g:arrange" }, ring.Select(i => i.Id));
            Assert.Equal(8, OnSlideController.PieSlots.Length);
            Assert.Equal(new[] { "act:0", "act:1", "act:2" }, menus["edit"].Select(i => i.Id));
            // Nothing to arrange: the place stays, greyed.
            Assert.False(ring.Single(i => i.Id == "menu:g:arrange").Enabled);
            // Every entry has a label; submenu entries explain what is inside.
            Assert.All(ring, i => Assert.False(string.IsNullOrWhiteSpace(i.Label)));
            Assert.Equal("act:0, act:1, act:2", ring.Single(i => i.Id == "menu:g:edit").Hint);
        }

        [Fact]
        public void ASingleEntryIsShownDirectlyUnlessThePlaceIsAMenu()
        {
            var (ring, menus) = OnSlideController.PieRing(Groups(1, 0, 1), "More");
            Assert.Equal("view:0", ring[6].Id);           // one view: directly
            Assert.Equal("menu:g:edit", ring[1].Id);      // edit keeps its name with one entry
            Assert.Single(menus["edit"]);
            Assert.False(ring[4].Enabled);                // no style: greyed place
        }

        [Fact]
        public void LongSubmenusContinueInMoreWithoutDuplicates()
        {
            var (_, menus) = OnSlideController.PieRing(Groups(25), "More");
            var first = menus["edit"];
            Assert.Equal(OnSlideController.MaxSubItems, first.Count);
            Assert.Equal("menu:g:edit+", first.Last().Id);
            var all = menus["edit"].Concat(menus["edit+"]).Concat(menus["edit++"]).Where(i => !i.Id.StartsWith("menu:")).Select(i => i.Id).ToList();
            Assert.Equal(25, all.Count);
            Assert.Equal(25, all.Distinct().Count());
        }

        [Fact]
        public void BarShowsPictogramsOnlyWhenTheyAreUnambiguous()
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
            // Telling pictograms first; "+" and "−" alone say nothing (UX review 2026-11-25), so they are text buttons.
            var withText = OnSlideController.BarActions(actions, hasTextButton: true);
            Assert.Equal(new[] { "flip", "fourth" }, withText.Where(x => x.Pictorial).Select(x => x.Action.Id));
            Assert.All(withText.Where(x => x.Action.Icon is InventaSketch.Core.IconIds.Increase or InventaSketch.Core.IconIds.Decrease), x => Assert.False(x.Pictorial));
            Assert.DoesNotContain(withText, x => x.Action.Id == "off");
            Assert.Contains(OnSlideController.BarActions(actions, hasTextButton: false), x => x.Action.Id == "ref");
        }

        [Fact]
        public void BarGroupsAreSeparated()
        {
            var a = new List<BarButton> { new("insert", "", "") };
            var b = new List<BarButton>();
            var c = new List<BarButton> { new("flipH", "", ""), new("rot90", "", "") };
            var d = new List<BarButton> { new("more", "", "") };
            var bar = OnSlideController.JoinGroups(a, b, c, d);
            Assert.Equal(new[] { "insert", "", "flipH", "rot90", "", "more" }, bar.Select(x => x.Id));
            Assert.Equal(2, bar.Count(x => x.Separator));
        }

        [Fact]
        public void ShortLabelsFitTheBar()
        {
            Assert.Equal("Helical teeth", OnSlideController.ShortLabel("Helical teeth on/off"));
            Assert.Equal("Centre", OnSlideController.ShortLabel("Next centre (closed, tandem)"));
            Assert.Equal(22, OnSlideController.ShortLabel("A very long action label indeed").Length);
        }
    }
}
