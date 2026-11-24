using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Engine.Tests
{
    /// <summary>Electrical house symbols switch plan ↔ section with the building services (one installation rule).</summary>
    public class HouseInstallationViewTests
    {
        private static readonly Registry Reg = ModuleCatalog.CreateDefault();

        [Fact]
        public void SocketsAndBasinsGoToSectionTogetherAndBack()
        {
            var socket = Reg.Create(Reg.Library.First(i => i.TypeId == "elec.house.socket"), new Vec2(60, 40));
            var basin = Reg.Create(Reg.Library.First(i => i.TypeId == "bs.san.basin" && (i.View ?? "plan") == "plan"), new Vec2(100, 40));
            var doc = SketchDocument.Create();
            doc = Edits.Add(doc, doc.Pages[0].Id, socket, basin);
            Assert.Contains(Arrange.CommonViews(Reg, new[] { socket, basin }), v => v.Id == "section");
            var ids = new[] { socket.Id, basin.Id };
            var section = AssemblyViews.Switch(Reg, doc, ids, "section");
            Assert.Equal("section", section.Get(socket.Id)!.View);
            Assert.Equal("section", section.Get(basin.Id)!.View);
            // One floor line for both: the socket (300 mm) sits lower than... its bottom is 300 mm / 100 = 3 mm above the floor.
            var floor = double.Parse(section.Get(socket.Id)!.Meta!["bs.floorY"], System.Globalization.CultureInfo.InvariantCulture);
            Assert.Equal(floor, double.Parse(section.Get(basin.Id)!.Meta!["bs.floorY"], System.Globalization.CultureInfo.InvariantCulture), 6);
            var s = section.Get(socket.Id)!;
            Assert.Equal(floor - 3, s.Transform.Y + s.Size.H / 2, 3);
            var plan = AssemblyViews.Switch(Reg, section, ids, "plan");
            Assert.Equal(socket.Transform.Y, plan.Get(socket.Id)!.Transform.Y, 6);
            Assert.Equal(300, plan.Get(socket.Id)!.Num("height", 0), 1);
        }

        [Fact]
        public void EveryHouseSymbolRendersInItsThreeViews()
        {
            foreach (var typeId in new[] { "elec.house.socket", "elec.house.switch", "elec.house.light", "elec.house.junction", "elec.house.board" })
            {
                var type = Reg.Type(typeId)!;
                Assert.Equal(new[] { "plan", "section", "schematic" }, type.Views.Select(v => v.Id).ToArray());
                foreach (var v in type.Views)
                {
                    var o = Reg.WithView(Reg.Create(Reg.Library.First(i => i.TypeId == typeId), new Vec2(50, 50)), v.Id, new RenderContext(SketchDocument.Create()));
                    var doc = SketchDocument.Create();
                    doc = Edits.Add(doc, doc.Pages[0].Id, o);
                    Assert.NotEmpty(Reg.Render(o, new RenderContext(doc)));
                }
            }
        }
    }
}
