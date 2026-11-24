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
        public void WallsGoToElevationWhenTheInstallationGoesToSection()
        {
            var wall = Reg.Create(Reg.Library.First(i => i.TypeId == "arch.wall" && (i.View ?? "plan") == "plan"), new Vec2(90, 80))
                with { Points = new System.Collections.Generic.List<Vec2> { new(20, 80), new(160, 80) } };
            var socket = Reg.Create(Reg.Library.First(i => i.TypeId == "elec.house.socket"), new Vec2(60, 40));
            var basin = Reg.Create(Reg.Library.First(i => i.TypeId == "bs.san.basin" && (i.View ?? "plan") == "plan"), new Vec2(100, 40));
            var door = Reg.Create(Reg.Library.First(i => i.TypeId == "arch.door.single" && (i.View ?? "plan") == "plan"), new Vec2(130, 80));
            var doc = SketchDocument.Create();
            doc = Edits.Add(doc, doc.Pages[0].Id, wall, socket, basin, door);
            var views = Arrange.CommonViews(Reg, new[] { wall, socket, basin, door }).Select(v => v.Id).ToList();
            Assert.Contains("section", views);
            Assert.DoesNotContain("elevation", views);
            Assert.DoesNotContain("schematic", views);

            var ids = new[] { wall.Id, socket.Id, basin.Id, door.Id };
            var section = AssemblyViews.Switch(Reg, doc, ids, "section");
            Assert.Equal("elevation", section.Get(wall.Id)!.View);
            Assert.Equal("elevation", section.Get(door.Id)!.View);
            Assert.Equal(wall.Points, section.Get(wall.Id)!.Points);
            Assert.Equal("section", section.Get(socket.Id)!.View);
            // The installation stands on the wall's ground line (its centre line), not on its own lowest plan edge.
            var s = section.Get(socket.Id)!;
            Assert.Equal(80 - 3, s.Transform.Y + s.Size.H / 2, 3);
            Assert.DoesNotContain(Arrange.CommonViews(Reg, ids.Select(id => section.Get(id)!).ToList()), v => v.Id == "section");

            var plan = AssemblyViews.Switch(Reg, section, ids, "plan");
            Assert.Equal("plan", plan.Get(wall.Id)!.View);
            Assert.Equal(socket.Transform.Y, plan.Get(socket.Id)!.Transform.Y, 6);
            Assert.Equal(300, plan.Get(socket.Id)!.Num("height", 0), 1);
            Assert.Equal(2750, plan.Get(wall.Id)!.Num("height", 2750), 1);
        }

        [Fact]
        public void AliasesResolvePerType()
        {
            Assert.True(ViewAliases.Same("section", "elevation"));
            Assert.False(ViewAliases.Same("section", "schematic"));
            Assert.Equal("elevation", ViewAliases.Resolve(Reg.Type("arch.wall"), "section"));
            Assert.Equal("section", ViewAliases.Resolve(Reg.Type("elec.house.socket"), "elevation"));
            Assert.Null(ViewAliases.Resolve(Reg.Type("arch.wall"), "schematic"));
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
