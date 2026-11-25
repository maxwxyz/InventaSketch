using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Core.Tests
{
    /// <summary>Choices among actions (rule: a choice among several options is a dropdown, never a cycle).</summary>
    public class ActionChoiceTests
    {
        private static readonly ParamDef Mode = new("mode", new I18nText("Mode", "Modus"), ParamKind.Choice)
        {
            Default = "a",
            Options = new[] { new ParamOption("a", new I18nText("A")), new ParamOption("b", new I18nText("B")) { Icon = IconIds.LineDash } },
        };

        [Fact]
        public void OptionsCarryTheirChoiceIdsAndTheCurrentOne()
        {
            var c = new ActionChoice("x.shape", new I18nText("Shape", "Form"));
            var a = ActionChoices.Option(c, "round", new I18nText("Round", "Rund"), IconIds.ShapeCircle, true, o => o);
            var b = ActionChoices.Option(c, "square", new I18nText("Square", "Eckig"), "", false, o => o);
            Assert.Equal("x.shape=round", a.Id);
            Assert.Equal("round", ActionChoices.ValueOf(a.Id));
            Assert.Null(ActionChoices.ValueOf("plain"));
            Assert.True(a.IsActive);
            Assert.Equal("choice:x.shape", a.Group);
            Assert.Equal("Shape: Round", ActionChoices.Caption(c, new[] { a, b }).En);
            Assert.Equal("Form: Rund", ActionChoices.Caption(c, new[] { a, b }).De);
            Assert.Same(a, ActionChoices.Current(new[] { a, b }));
        }

        [Fact]
        public void MenusHaveNoCurrentOptionAndGroupingKeepsTheOrder()
        {
            var menu = new ActionChoice("x.add", new I18nText("Add", "Hinzufügen")) { IsMenu = true };
            var shape = new ActionChoice("x.shape", new I18nText("Shape"));
            var plain = new ContextAction("x.plain", new I18nText("Plain"), IconIds.Insert, o => o);
            var actions = new[]
            {
                ActionChoices.Option(shape, "1", new I18nText("One"), "", false, o => o),
                plain,
                ActionChoices.Option(menu, "a", new I18nText("A"), "", true, o => o),
                ActionChoices.Option(shape, "2", new I18nText("Two"), "", true, o => o),
            };
            Assert.False(actions[2].IsActive); // a menu marks nothing
            Assert.Equal("Add", ActionChoices.Caption(menu, new[] { actions[2] }).En);
            var groups = ActionChoices.Group(actions);
            Assert.Equal(new[] { "x.shape", "x.add" }, groups.Select(g => g.Choice.Key));
            Assert.Equal(new[] { "x.shape=1", "x.shape=2" }, groups[0].Options.Select(o => o.Id));
            Assert.Equal(new[] { plain }, ActionChoices.Plain(actions));
        }

        [Fact]
        public void AChoiceParameterBecomesOneOptionPerValue()
        {
            var o = new SketchObject { Id = "o", Type = "t" };
            var options = ActionChoices.FromParam(o, "t.mode", Mode, new I18nText("Mode", "Modus")).ToList();
            Assert.Equal(new[] { "t.mode=a", "t.mode=b" }, options.Select(x => x.Id));
            Assert.True(options[0].IsActive);
            Assert.Equal(IconIds.LineDash, options[1].Icon);
            Assert.Equal("", options[0].Icon);
            Assert.All(options, x => Assert.Equal(new[] { "mode" }, x.ParamKeys));
            Assert.Equal("b", options[1].Apply(o).Str("mode", ""));
            var withB = o.WithParam("mode", "b");
            Assert.True(ActionChoices.FromParam(withB, "t.mode", Mode, new I18nText("Mode")).Last().IsActive);
        }

        [Fact]
        public void TheGenericLineStyleOfAPartIsAChoice()
        {
            var o = new SketchObject { Id = "o", Type = "t" };
            var actions = PartLooks.Generic(o, "series.0", 0.5).ToList();
            var dash = actions.Where(a => a.Choice?.Key == PartLooks.DashChoice).ToList();
            Assert.Equal(new[] { "part.dash=solid", "part.dash=dash", "part.dash=dot", "part.dash=dashdot" }, dash.Select(a => a.Id));
            Assert.DoesNotContain(dash, a => a.IsActive);
            Assert.All(dash, a => Assert.Contains(a.Icon, IconIds.All));
            var dotted = dash[2].Apply(o);
            Assert.Equal(DashStyle.Dot, dotted.Parts!["series.0"].Style!.Dash);
            Assert.True(PartLooks.Generic(dotted, "series.0", 0.5).Single(a => a.Id == "part.dash=dot").IsActive);
            Assert.DoesNotContain(actions, a => a.Icon == IconIds.Cycle && a.Choice is not null);
        }
    }
}
