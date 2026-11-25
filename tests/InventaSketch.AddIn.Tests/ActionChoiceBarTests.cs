using System.Linq;
using InventaSketch.AddIn.OnSlide;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.AddIn.Tests
{
    /// <summary>Choices among part actions in the bar and pie (dropdowns, never cycles), grips of a selected curve, Del on a part.</summary>
    public class ActionChoiceBarTests
    {
        private static ContextAction Plain(string id) => new(id, new I18nText(id), IconIds.Insert, o => o);

        [Fact]
        public void AChoiceIsOneEntryAtThePlaceOfItsFirstOption()
        {
            var shape = new ActionChoice("chart.curve.shape", new I18nText("Curve shape", "Kurvenform"));
            var actions = new[]
            {
                ActionChoices.Option(shape, "smoothed", new I18nText("Smoothed"), "", true, o => o),
                Plain("chart.data"),
                ActionChoices.Option(shape, "raw", new I18nText("As drawn"), "", false, o => o),
                Plain("part.hide"),
            };
            var entries = ActionChoiceBar.Entries(actions);
            Assert.Equal(3, entries.Count);
            Assert.True(entries[0].IsChoice);
            Assert.Equal(2, entries[0].Options.Count);
            Assert.Equal("chart.data", entries[1].Action!.Id);
            Assert.Equal("@p.chart.curve.shape", ActionChoiceBar.Key(ActionChoiceBar.PartPrefix, shape));
            Assert.Equal("Curve shape: Smoothed ▸", ActionChoiceBar.PieLabel(shape, entries[0].Options, Lang.En));
            Assert.Equal("Kurvenform: Smoothed ▸", ActionChoiceBar.PieLabel(shape, entries[0].Options, Lang.De));
        }

        [Fact]
        public void PickedOptionsRunAsPartOrElementActions()
        {
            Assert.Equal("part:chart.curve.shape=raw", ActionChoiceBar.Command("@p.chart.curve.shape", "chart.curve.shape=raw"));
            Assert.Equal("act:chart.area.add=span", ActionChoiceBar.Command("@a.chart.area.add", "chart.area.add=span"));
            Assert.Null(ActionChoiceBar.Command("hub", "fixed"));
        }

        [Fact]
        public void TheGripsOfASelectedCurveAndOfItsPointsAreShown()
        {
            var grips = new[]
            {
                new DragHandle("point.0.0", default, DragConstraint.Free, new I18nText("p")) { Part = "point.0.0", ShowWith = "series.0" },
                new DragHandle("point.0.1", default, DragConstraint.Free, new I18nText("p")) { Part = "point.0.1", ShowWith = "series.0" },
                new DragHandle("point.1.0", default, DragConstraint.Free, new I18nText("p")) { Part = "point.1.0", ShowWith = "series.1" },
                new DragHandle("series.0", default, DragConstraint.Free, new I18nText("c")) { Part = "series.0", ByPart = true },
                new DragHandle("limit.0", default, DragConstraint.Vertical, new I18nText("l")) { Part = "limit" },
            };
            Assert.True(ActionChoiceBar.ShownWith(grips[0], "series.0", grips));
            Assert.False(ActionChoiceBar.ShownWith(grips[2], "series.0", grips));
            // A selected point shows its siblings, not the other curve's points.
            Assert.True(ActionChoiceBar.ShownWith(grips[1], "point.0.0", grips));
            Assert.False(ActionChoiceBar.ShownWith(grips[2], "point.0.0", grips));
            Assert.True(ActionChoiceBar.ShownWith(grips[4], "limit", grips));
            Assert.False(ActionChoiceBar.ShownWith(grips[0], null, grips));
            Assert.Same(grips[3], ActionChoiceBar.BodyGrip(grips, "series.0"));
            Assert.Null(ActionChoiceBar.BodyGrip(grips, "series.1"));
        }

        [Fact]
        public void DelDeletesThePartOrHidesItNeverTheElement()
        {
            var del = new ContextAction(PartLooks.DeleteAction, new I18nText("Delete point"), IconIds.Delete, o => o);
            var hide = new ContextAction("part.hide", new I18nText("Hide"), IconIds.Toggle, o => o);
            Assert.Equal(PartLooks.DeleteAction, ActionChoiceBar.DeleteActionId(new[] { hide, del }));
            Assert.Equal("part.hide", ActionChoiceBar.DeleteActionId(new[] { hide }));
            Assert.Null(ActionChoiceBar.DeleteActionId(new[] { hide with { IsActive = true } }));
            Assert.Equal("series.2", ActionChoiceBar.AfterDelete("point.2.7"));
            Assert.Null(ActionChoiceBar.AfterDelete("series.2"));
            Assert.Null(ActionChoiceBar.AfterDelete("area.0"));
            // An added area is selected: the one new part key.
            Assert.Equal("region.1", ActionChoiceBar.Added(new[] { "series.0", "region.0" }, new[] { "series.0", "region.0", "region.1" }));
            Assert.Null(ActionChoiceBar.Added(new[] { "series.0" }, new[] { "series.0" }));
        }
    }
}
