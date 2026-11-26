using System;
using System.IO;
using System.Linq;
using InventaSketch.AddIn.OnSlide;
using InventaSketch.Core;
using InventaSketch.Engine;
using InventaSketch.Modules.Patent;
using Xunit;

namespace InventaSketch.AddIn.Tests
{
    /// <summary>"Number all parts" of a slide without a selection ("More" menu): the slide model and how the result splits into units.</summary>
    public class SlideNumberingTests
    {
        private static readonly Registry Reg = ModuleCatalog.CreateDefault();

        private static SketchObject Part(double x, double y)
        {
            var item = Reg.Library.First(i => PartsList.IsPart(Reg, Reg.Create(i, new Vec2(0, 0))));
            return Reg.Create(item, new Vec2(x, y));
        }

        private static SketchUnit Unit(params SketchObject[] objects)
        {
            var doc = SketchDocument.Create(PageSizes.Slide16x9);
            doc = Edits.Add(doc, doc.Pages[0].Id, objects);
            // The shape is not needed for the model logic.
            return new SketchUnit(null!, Ids.New("u"), doc, default);
        }

        [Fact]
        public void NumeralsContinueAfterTheOtherSlidesAndBecomeUnitsOfTheirOwn()
        {
            var a = Unit(Part(60, 60));
            var b = Unit(Part(180, 60));
            var units = new[] { a, b };
            var slide = SketchUnits.Combined(units);
            // Slide 1 already uses numeral 10 (on a numeral object) and has 20 in its reference list.
            var other = SketchUnits.Combined(new[] { Unit(Part(60, 60)) });
            other = ReferenceNumerals.Insert(Reg, other, other.Pages[0].Id, new Vec2(60, 60), new Vec2(80, 40), "Gear", null, "10").Doc;
            other = Edits.SetReference(other, "20", "Shaft");
            var doc = SlideNumbering.WithUsedNumerals(slide, other);
            Assert.True(doc.References.ContainsKey("10") && doc.References.ContainsKey("20"));

            var commands = PageActions.For(Reg, doc, doc.Pages[0].Id);
            Assert.Equal(2, commands.Count);
            var ran = PageActions.Run(Reg, doc, doc.Pages[0].Id, PartNumbering.ActionKey + "=tens", Lang.En);
            var (changed, added) = SlideNumbering.Split(doc, ran, units);
            // Both parts got a numeral (their units are redrawn), each new numeral is its own unit, after 10 and 20.
            Assert.Equal(2, changed.Count);
            Assert.Equal(2, added.Count);
            Assert.All(added, n => Assert.Equal(PatentIds.Numeral, n.Type));
            // The two identical parts share one numeral (same feature, same sign), the first free one after 10 and 20.
            Assert.All(added, n => Assert.Equal("30", ReferenceNumerals.NumeralOf(n)));
        }

        [Fact]
        public void TheRibbonOffersNumberAllPartsAsADynamicMenu()
        {
            var old = RibbonCache.Folder;
            var dir = Path.Combine(Path.GetTempPath(), "isk-ribbon-" + Guid.NewGuid().ToString("N"));
            try
            {
                RibbonCache.Folder = dir;
                Connect.WarmRibbonCache();
                var xml = System.Xml.Linq.XDocument.Parse(RibbonCache.Load(Lang.De)!);
                var menu = xml.Descendants().Single(e => (string?)e.Attribute("id") == "IskMenuNumberParts");
                Assert.Equal("dynamicMenu", menu.Name.LocalName);
                Assert.Equal("Alle Teile nummerieren", (string?)menu.Attribute("label"));
                // The callbacks the menu names exist on the add-in.
                Assert.NotNull(typeof(Connect).GetMethod((string)menu.Attribute("getContent")!));
                Assert.NotNull(typeof(Connect).GetMethod("OnNumberParts"));
                Assert.NotNull(typeof(Connect).GetMethod((string)menu.Attribute("getImage")!));
            }
            finally
            {
                RibbonCache.Folder = old;
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
        }
    }
}
