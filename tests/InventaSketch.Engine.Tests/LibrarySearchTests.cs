using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Engine.Tests
{
    /// <summary>Insert by typing: ranked library hits (<see cref="LibrarySearch"/>).</summary>
    public class LibrarySearchTests
    {
        private static readonly Registry Reg = ModuleCatalog.CreateDefault();

        [Fact]
        public void NamesStartingWithTheQueryComeFirst()
        {
            var hits = LibrarySearch.Find(Reg, "zahnrad", Lang.De);
            Assert.NotEmpty(hits);
            Assert.StartsWith("zahnrad", LibrarySearch.Fold(hits[0].Name.Get(Lang.De)));
            var en = LibrarySearch.Find(Reg, "spur", Lang.En);
            Assert.StartsWith("spur", en[0].Name.En.ToLowerInvariant());
        }

        [Fact]
        public void EveryTermMustMatchAndUmlautsMatchTheirSpelledOutForm()
        {
            var both = LibrarySearch.Find(Reg, "four bar", Lang.En, max: 50);
            Assert.NotEmpty(both);
            Assert.All(both, i => Assert.Equal("kin.mech.fourbar", i.TypeId));
            Assert.Equal(LibrarySearch.Find(Reg, "stößel", Lang.De).Select(i => i.Id), LibrarySearch.Find(Reg, "stoessel", Lang.De).Select(i => i.Id));
            Assert.NotEmpty(LibrarySearch.Find(Reg, "stoessel", Lang.De));
            Assert.Empty(LibrarySearch.Find(Reg, "zahnrad qqqxyz", Lang.De));
        }

        [Fact]
        public void RecentItemsRankHigherAndAnEmptyQueryListsThem()
        {
            var plain = LibrarySearch.Find(Reg, "gear", Lang.En, max: 40);
            var last = plain[plain.Count - 1];
            var recent = LibrarySearch.Remember(null, last.Id);
            var boosted = LibrarySearch.Find(Reg, "gear", Lang.En, recent, max: 40);
            Assert.True(boosted.ToList().IndexOf(last) < plain.Count - 1);
            Assert.Equal(new[] { last.Id }, LibrarySearch.Find(Reg, "", Lang.En, recent).Select(i => i.Id));
            var again = LibrarySearch.Remember(LibrarySearch.Remember(recent, "x"), last.Id);
            Assert.Equal(new[] { last.Id, "x" }, again);
        }

        [Fact]
        public void TheSelectionsViewWinsATie()
        {
            var stick = LibrarySearch.Find(Reg, "spur gear", Lang.En, view: "stick", max: 40);
            var axial = LibrarySearch.Find(Reg, "spur gear", Lang.En, view: "axial", max: 40);
            Assert.NotEqual(stick.Select(i => i.Id), axial.Select(i => i.Id));
        }
    }
}
