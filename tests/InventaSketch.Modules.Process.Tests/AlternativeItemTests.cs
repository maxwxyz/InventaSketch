using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Process.Tests
{
    /// <summary>One element, one item: the duplicate AHU fan steps back; AHU sections and the P&amp;ID motor are named apart.</summary>
    public class AlternativeItemTests
    {
        [Fact]
        public void TheSupplyExtractAirFanGivesWayToTheFanAndStillRenders()
        {
            var r = Fx.Reg();
            Assert.DoesNotContain(r.Library, i => i.Id == "process.hvac.fan");
            var hidden = r.Item("process.hvac.fan");
            Assert.NotNull(hidden);
            Assert.Equal("process.fan", hidden!.Alternative);
            var fan = r.Library.Single(i => i.Id == "process.fan");
            Assert.Equal(hidden.TypeId, fan.TypeId);
            // The fan answers to the names of the item that gave way.
            Assert.Contains(LibrarySearch.Find(r, "zuluft", Lang.De, max: 50), i => i.Id == "process.fan");
            Assert.Contains(LibrarySearch.Find(r, "extract air", Lang.En, max: 50), i => i.Id == "process.fan");
            var o = r.Create(hidden, new Vec2(50, 50));
            var doc = SketchDocument.Create();
            doc = Edits.Add(doc, doc.Pages[0].Id, o);
            Assert.NotEmpty(r.Render(o, new RenderContext(doc)));
        }

        [Fact]
        public void AirHandlingSectionsAndTheMotorSayWhatTheyAreFor()
        {
            var r = Fx.Reg();
            foreach (var i in r.Library.Where(i => i.TypeId.StartsWith("process.hvac.", System.StringComparison.Ordinal)))
            {
                Assert.EndsWith("(AHU)", i.Name.En);
                Assert.EndsWith("RLT)", i.Name.De);
                Assert.Contains("ahu", i.Keywords);
            }
            Assert.Equal("Electric motor (P&ID drive)", r.Item("process.motor")!.Name.En);
            Assert.Equal("Elektromotor (R&I-Antrieb)", r.Item("process.motor")!.Name.De);
            Assert.Equal(r.Library.Count, r.Library.Select(i => i.Name.En).Distinct().Count());
        }
    }
}
