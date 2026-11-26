using System.Linq;
using InventaSketch.AddIn.OnSlide;
using InventaSketch.Core;
using InventaSketch.Engine;
using Xunit;

namespace InventaSketch.AddIn.Tests
{
    /// <summary>Calc 2 in the COM add-in: the value card's content, the level switch and the drag step (no PowerPoint needed).</summary>
    public class ValueCardTests
    {
        private static readonly Registry Registry = ModuleCatalog.CreateDefault();

        private static (SketchDocument Doc, SketchObject O) Resistor(params (string Key, string Text)[] values)
        {
            var o = Registry.Create(Registry.Library.First(i => i.TypeId == "elec.resistor"), new Vec2(50, 50));
            foreach (var (k, t) in values) o = o.WithParam(k, t);
            var doc = SketchDocument.Create();
            doc = Edits.Add(doc, doc.Pages[0].Id, o);
            return (doc, o);
        }

        private static ValueCardModel Card(ValueLevel level, Lang lang, params (string Key, string Text)[] values)
        {
            var (doc, o) = Resistor(values);
            var table = ValueSolver.Solve(doc, Registry, new[] { o.Id }, null, lang);
            return ValueCardModel.Build(table, o.Id, "Resistor · Values", level, lang, ValueLabels.Shows(o));
        }

        [Fact]
        public void TextValuedVariablesAreShownButNeitherTypedNorDragged()
        {
            // The four-bar's Grashof class is a text-valued variable ("Crank-rocker"): the card shows it read-only.
            var o = Registry.Create(Registry.Library.First(i => i.TypeId == "kin.mech.fourbar"), new Vec2(50, 50));
            var doc = SketchDocument.Create();
            doc = Edits.Add(doc, doc.Pages[0].Id, o);
            var table = ValueSolver.Solve(doc, Registry, new[] { o.Id }, null, Lang.En);
            var card = ValueCardModel.Build(table, o.Id, "Four-bar · Values", ValueLevel.Explore, Lang.En, false);
            var cls = card.Rows.Single(r => r.Key == "cls");
            Assert.True(cls.ReadOnly);
            Assert.False(cls.CanDrag);
            Assert.False(string.IsNullOrEmpty(cls.Field));
            Assert.All(card.Rows.Where(r => r.Key != "cls"), r => Assert.False(r.ReadOnly));
        }

        [Fact]
        public void OhmCardShowsDerivedValuesWithFormulas()
        {
            var card = Card(ValueLevel.Explore, Lang.En, ("voltage", "230 V"), ("current", "2 A"));
            Assert.Equal(new[] { "R", "U", "I", "P" }, card.Rows.Select(r => r.Key).ToArray());
            var r = card.Rows.Single(x => x.Key == "R");
            var p = card.Rows.Single(x => x.Key == "P");
            var u = card.Rows.Single(x => x.Key == "U");
            Assert.Equal("115 Ω", r.Field);
            Assert.Equal("460 W", p.Field);
            Assert.Equal("=", r.Marker);
            Assert.Equal("✎", u.Marker);
            Assert.Equal("230 V", u.Field);
            Assert.Equal("R = U/I = 230 V/2.00 A = 115 Ω", r.Formula);
            Assert.Equal("P = U·I = 230 V·2.00 A = 460 W", p.Formula);
            Assert.Null(u.Formula);
            Assert.True(u.CanClear);
            Assert.False(r.CanClear);
            Assert.True(r.CanDrag);
            Assert.Equal("Ω", r.Unit);
            Assert.Equal(ValueStatus.Determined, card.State);
            Assert.Empty(card.Conflicts);
        }

        [Fact]
        public void QuickSketchCardStaysPlainAndEngineerAddsSourceAndDigits()
        {
            var quick = Card(ValueLevel.QuickSketch, Lang.En, ("voltage", "230 V"), ("current", "3 A"));
            Assert.All(quick.Rows, r => Assert.Null(r.Formula));
            Assert.All(quick.Rows, r => Assert.False(r.CanDrag));
            Assert.Equal("76.7 Ω", quick.Rows[0].Field);
            var engineer = Card(ValueLevel.Engineer, Lang.En, ("voltage", "230 V"), ("current", "3 A"));
            Assert.Equal("76.67 Ω", engineer.Rows[0].Field);
            Assert.EndsWith("[S7]", engineer.Rows[0].Formula);
            Assert.All(engineer.Rows, r => Assert.False(r.CanDrag));
        }

        [Fact]
        public void ThirdDisagreeingEntryIsAConflictWithBothValues()
        {
            var card = Card(ValueLevel.Explore, Lang.En, ("voltage", "230 V"), ("current", "2 A"), ("value", "100 Ω"));
            Assert.Equal(ValueStatus.Conflict, card.State);
            Assert.Contains(card.Rows, r => r.Conflict && r.Marker == "⚠");
            Assert.NotEmpty(card.Conflicts);
            Assert.Contains("≠", card.Conflicts[0]);
            Assert.StartsWith("⚠", card.Status);
            // Every entered value stays as typed.
            Assert.Equal("100 Ω", card.Rows.Single(r => r.Key == "R").Field);
            Assert.Equal("230 V", card.Rows.Single(r => r.Key == "U").Field);
        }

        [Fact]
        public void StatusSaysWhatIsMissingAndHowToStart()
        {
            Assert.Equal("Enter what you know – the rest follows", Card(ValueLevel.Explore, Lang.En).Status);
            Assert.Equal("1 more value: R, I or P", Card(ValueLevel.Explore, Lang.En, ("voltage", "230 V")).Status);
            Assert.Equal("Noch 1 Wert: R, I oder P", Card(ValueLevel.Explore, Lang.De, ("voltage", "230 V")).Status);
            var de = Card(ValueLevel.Explore, Lang.De, ("voltage", "230 V"), ("current", "3 A"));
            Assert.Equal("76,7 Ω", de.Rows[0].Field);
            Assert.Equal("Widerstand", de.Rows[0].Name);
        }

        [Fact]
        public void UnreadableStoredTextStaysVisibleWithItsProblem()
        {
            var card = Card(ValueLevel.Explore, Lang.En, ("voltage", "16 A"));
            var u = card.Rows.Single(r => r.Key == "U");
            Assert.Equal("16 A", u.Field);
            Assert.NotNull(u.Problem);
            Assert.True(u.CanClear);
        }

        [Fact]
        public void PieLabelsShowTypedAndDerivedValues()
        {
            var card = Card(ValueLevel.Explore, Lang.En, ("voltage", "230 V"), ("current", "2 A"));
            Assert.Equal("Voltage U … 230 V", ValueCardModel.PieLabel(card.Rows.Single(r => r.Key == "U")));
            Assert.Equal("Power P = 460 W", ValueCardModel.PieLabel(card.Rows.Single(r => r.Key == "P")));
            Assert.Equal("Resistance R …", ValueCardModel.PieLabel(Card(ValueLevel.Explore, Lang.En).Rows[0]));
        }

        [Fact]
        public void DescribeListsRowsStatusAndLabel()
        {
            var text = Card(ValueLevel.Explore, Lang.En, ("voltage", "230 V"), ("current", "2 A")).Describe();
            Assert.Contains("R|derived|115 Ω|R = U/I = 230 V/2.00 A = 115 Ω", text);
            Assert.Contains("U|entered|230 V", text);
            Assert.Contains("label|off", text);
        }

        [Theory]
        [InlineData("explore", ValueLevel.Explore)]
        [InlineData("ENGINEER", ValueLevel.Engineer)]
        [InlineData("quick", ValueLevel.QuickSketch)]
        [InlineData("off", ValueLevel.QuickSketch)]
        public void LevelTagsParse(string tag, ValueLevel level)
        {
            Assert.Equal(level, ValueLevelSetting.Parse(tag));
            Assert.Equal(level, ValueLevelSetting.Parse(ValueLevelSetting.TagOf(level)));
        }

        [Fact]
        public void UnknownOrMissingTagIsNoLevel()
        {
            Assert.Null(ValueLevelSetting.Parse(null));
            Assert.Null(ValueLevelSetting.Parse(""));
            Assert.Null(ValueLevelSetting.Parse("loud"));
            Assert.Equal(3, ValueLevelSetting.Digits(ValueLevel.Explore));
            Assert.Equal(4, ValueLevelSetting.Digits(ValueLevel.Engineer));
            Assert.All(ValueLevelSetting.Levels, l => Assert.NotEqual(ValueLevelSetting.Label(l, Lang.En), ValueLevelSetting.Label(l, Lang.De)));
        }

        [Fact]
        public void DragStepIsLogarithmicAndRounded()
        {
            Assert.Equal(115, ValueCard.DragValue(115, 0));
            var up = ValueCard.DragValue(115, (int)ValueCard.PixelsPerE);
            Assert.Equal(313, up);          // 115·e ≈ 312.6, three significant digits
            Assert.True(ValueCard.DragValue(115, -40) < 115);
            Assert.True(ValueCard.DragValue(115, -2000) > 0);
        }
    }
}
