using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Engine.Tests
{
    /// <summary>"Insert ▸" offers elements that fit the selection, in its kind of view, plus annotation items.</summary>
    public class SuggestionTests
    {
        private static readonly Registry Reg = ModuleCatalog.CreateDefault();

        private static SketchObject Create(string itemId) => Reg.Create(Reg.Item(itemId)!, new Vec2(100, 100));

        [Fact]
        public void SchematicShaftSuggestsSchematicGearsAndBearings()
        {
            var shaft = Create(Reg.Library.First(i => i.TypeId == "mech.shaft" && i.View == "line").Id);
            var s = Reg.Suggestions(shaft);
            Assert.Equal("mech.gear.spur", s[0].TypeId);
            Assert.Equal("stick", s[0].View);
            Assert.Contains(s, i => i.TypeId == "mech.bearing.ball");
            Assert.Contains(s, i => i.TypeId == "patent.numeral");
            Assert.Contains(s, i => i.TypeId == "ann.leader");
            Assert.DoesNotContain(s, i => i.TypeId == "mech.shaft");
            Assert.True(s.Count <= 9);
            Assert.Equal(s.Count, s.Select(i => i.TypeId).Distinct().Count());
        }

        [Fact]
        public void WireSuggestsElectricalSymbolsAndKeepsAnnotations()
        {
            var wire = Create(Reg.Library.First(i => i.TypeId == "elec.wire").Id);
            var s = Reg.Suggestions(wire);
            Assert.Equal("elec.resistor", s[0].TypeId);
            Assert.Contains(s, i => i.TypeId == "elec.switch");
            Assert.Equal(3, s.Count(i => i.TypeId is "patent.numeral" or "ann.leader" or "ann.arrow"));
        }

        [Fact]
        public void TypesWithoutCompanionsSuggestTheirCategory()
        {
            var gear = Create("mech.spur.stick");
            var s = Reg.Suggestions(gear);
            var category = Reg.Type("mech.gear.spur")!.Category;
            Assert.Contains(s, i => i.Category == category && i.TypeId != "mech.gear.spur");
            Assert.Contains(s, i => i.TypeId == "patent.numeral");
        }
    }
}
