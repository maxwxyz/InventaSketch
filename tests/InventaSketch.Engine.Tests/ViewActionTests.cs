using System.Linq;
using InventaSketch.Core;
using InventaSketch.Engine;
using Xunit;

namespace InventaSketch.Engine.Tests
{
    /// <summary>
    /// The hosts' action lists (<see cref="Registry.Actions"/>) never offer a view a second time next to the view control
    /// (interaction concept section 9 item 4; found in LibreOffice: the resistor listed "IEC symbol/ANSI style" twice).
    /// </summary>
    public class ViewActionTests
    {
        private static readonly Registry Reg = ModuleCatalog.CreateDefault();

        [Fact]
        public void NoLibraryElementListsItsViewsAsActions()
        {
            var offenders = Reg.Library
                .Select(item => Reg.Create(item, new Vec2(100, 100)))
                .Where(o => Reg.Type(o.Type) is { } t && t.Views.Count > 1)
                .Select(o =>
                {
                    var views = Reg.Type(o.Type)!.Views.Select(v => "view." + v.Id).ToHashSet();
                    var empty = SketchDocument.Create();
                    var doc = Edits.Add(empty, empty.Pages[0].Id, o);
                    var ctx = new RenderContext(doc, Lang.En);
                    return (o.Type, Ids: Reg.Actions(doc.Get(o.Id) ?? o, ctx).Where(a => views.Contains(a.Id)).Select(a => a.Id).ToList());
                })
                .Where(x => x.Ids.Count > 0)
                .Select(x => x.Type + ": " + string.Join(", ", x.Ids))
                .Distinct()
                .ToList();
            Assert.True(offenders.Count == 0, string.Join("\n", offenders.Take(20)));
        }

        [Fact]
        public void TheResistorSwitchesItsViewThroughTheViewControlOnly()
        {
            var o = Reg.Create(Reg.Item("elec.resistor")!, new Vec2(50, 50));
            var type = Reg.Type(o.Type)!;
            // The type still has its own view actions (size rules, module tests) …
            Assert.Contains(type.Actions(o, Lang.En), a => a.Id == "view.ansi");
            // … but the hosts' list leaves them to the view control, which switches the view.
            Assert.DoesNotContain(Reg.Actions(o, new RenderContext(SketchDocument.Create(), Lang.En)), a => a.Id == "view.ansi");
            Assert.Equal("ansi", Reg.WithView(o, "ansi", new RenderContext(SketchDocument.Create(), Lang.En)).View);
        }
    }
}
