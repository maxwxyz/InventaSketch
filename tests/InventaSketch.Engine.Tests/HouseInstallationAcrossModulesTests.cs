using System;
using System.Linq;
using InventaSketch.Core;
using InventaSketch.Engine;
using Xunit;

namespace InventaSketch.Engine.Tests
{
    /// <summary>
    /// The room devices of the switchgear give way to the electrical installation symbols and the building services appliance
    /// outlet (DuplicateItemsTests). Those canonical items, inserted from the gallery, feed the switchgear house installation
    /// list, the circuit plan and the operating current of the breaker that feeds them. Hand calculations at 230 V.
    /// </summary>
    public class HouseInstallationAcrossModulesTests
    {
        private static readonly Registry Reg = ModuleCatalog.CreateDefault();

        private static SketchObject Item(string id, double x, double y) => Reg.Create(Reg.Item(id) ?? throw new ArgumentException(id), new Vec2(x, y));

        private static string Export(string id, SketchDocument doc, Lang lang = Lang.En)
        {
            var e = Exports.All(Reg).Single(x => x.Id == id);
            var objects = doc.ObjectsOn(doc.Pages[0]).ToList();
            Assert.True(e.Applies(Reg, doc, objects), id);
            return e.Export(Reg, doc, objects, lang);
        }

        [Fact]
        public void GallerySocketsLightsAndAppliancesAppearInTheReportsAndTheBreakerCurrent()
        {
            var f2 = Item("sw.protection.mcb", 100, 60).WithParam("ref", "-F2").WithParam("circuit", "Kitchen");
            var f3 = Item("sw.protection.mcb.b10", 120, 60).WithParam("ref", "-F3").WithParam("phase", "L2");
            var socket = Item("elec.house.socket.double", 100, 150).WithParam("room", "Kitchen").WithParam("feed", "-F2").WithParam("power", "2300 W");
            var dimmer = Item("elec.house.switch", 120, 150).WithParam("room", "Kitchen").WithParam("feed", "-F3").WithParam("kind", "dimmer");
            var light = Item("elec.house.light", 140, 150).WithParam("room", "Kitchen").WithParam("feed", "-F3").WithParam("power", "115 W");
            var washer = Item("bs.elec.appliance.washer", 160, 150).WithParam("room", "Utility room").WithParam("feed", "-F2").WithParam("power", "1150 W");
            var doc = SketchDocument.Create();
            doc = Edits.Add(doc, doc.Pages[0].Id, f2, f3, socket, dimmer, light, washer);

            var list = Export("sw.installation.csv", doc);
            Assert.Contains("Kitchen,-F2,Kitchen,Miniature circuit breaker (DIN rail),B16,Double socket outlet,,1", list);
            Assert.Contains("Kitchen,-F3,,Miniature circuit breaker (DIN rail),B10,Dimmer,,1", list);
            Assert.Contains("Kitchen,-F3,,Miniature circuit breaker (DIN rail),B10,Luminaire,,1", list);
            Assert.Contains("Utility room,-F2,Kitchen,Miniature circuit breaker (DIN rail),B16,Washing machine,,1", list);
            Assert.Contains("1 × Zweifachsteckdose", Export("sw.installation.md", doc, Lang.De));
            Assert.Contains("1 × Double socket outlet; 1 × Washing machine", Export("sw.circuits.csv", doc));

            var t = ValueSolver.Solve(doc, Reg, null, new ValueSolveOptions { BudgetMs = 10000 });
            // -F2: socket 2300 W / 230 V = 10 A plus washer 1150 W / 230 V = 5 A.
            Assert.Equal(15, t.Get(f2.Id, "IB")!.Value!.Value, 9);
            Assert.Equal(3450, t.Get(f2.Id, "P")!.Value!.Value, 9);
            // -F3: the luminaire 115 W / 230 V = 0.5 A on L2 (the switch takes no power).
            Assert.Equal(0.5, t.Get(f3.Id, "IB")!.Value!.Value, 9);
            Assert.Empty(t.Conflicts);
            Assert.Empty(ValueSolver.Checks(t, Reg, new RenderContext(doc)));
        }
    }
}
