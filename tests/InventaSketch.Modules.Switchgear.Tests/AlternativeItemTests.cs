using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Switchgear.Tests
{
    /// <summary>
    /// The room devices give way to the canonical installation symbols (electrical) and appliance outlet (building services)
    /// while those are registered; on its own the module keeps them, and existing documents keep rendering.
    /// </summary>
    public class AlternativeItemTests
    {
        private static readonly Dictionary<string, string> Expected = new(StringComparer.Ordinal)
        {
            ["sw.house.socket"] = "elec.house.socket",
            ["sw.house.socket.double"] = "elec.house.socket.double",
            ["sw.house.switch"] = "elec.house.switch",
            ["sw.house.switch.twoway"] = "elec.house.switch.twoway",
            ["sw.house.switch.push"] = "elec.house.switch.push",
            ["sw.house.light"] = "elec.house.light",
            ["sw.house.appliance"] = "bs.elec.appliance",
            ["sw.house.appliance.washer"] = "bs.elec.appliance.washer",
        };

        /// <summary>Stands in for the modules that offer the canonical items.</summary>
        private sealed class CanonicalStub : IModule
        {
            private sealed class StubType : ObjectType
            {
                public override string Id => "stub.canonical";
                public override I18nText Name => new("Stub", "Stub");
                public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx) => Array.Empty<Primitive>();
            }

            public string Id => "stub";
            public I18nText Name => new("Stub", "Stub");
            public string Version => "1";
            public IEnumerable<ObjectType> Types => new ObjectType[] { new StubType() };
            public IEnumerable<LibraryCategory> Categories => new[] { new LibraryCategory("stub", new I18nText("Stub", "Stub"), Id) };
            public IEnumerable<LibraryItem> Library => Expected.Values.Select(id => new LibraryItem(id, "stub.canonical", new I18nText(id, id), "stub"));
        }

        [Fact]
        public void OnItsOwnTheModuleOffersEveryRoomDeviceAndNamesTheAlternative()
        {
            foreach (var (id, alt) in Expected)
            {
                var item = Fx.Reg.Library.Single(i => i.Id == id);
                Assert.Equal(alt, item.Alternative);
            }
            Assert.Null(Fx.Reg.Item("sw.house.light.wall")!.Alternative);
        }

        [Fact]
        public void WithTheCanonicalModulesTheRoomDevicesStepBackButStillLoadAndRender()
        {
            var reg = new Registry().Register(new CanonicalStub()).Register(new SwitchgearModule());
            foreach (var id in Expected.Keys)
            {
                Assert.DoesNotContain(reg.Library, i => i.Id == id);
                var item = reg.Item(id);
                Assert.NotNull(item);
                var o = reg.Create(item!, new Vec2(100, 100)).WithParam("room", "Kitchen").WithParam("feed", "-F2");
                Assert.NotEmpty(reg.Render(o, new RenderContext(Fx.Doc(o))));
            }
            // The wall light outlet has no counterpart: it and its category stay.
            Assert.Contains(reg.Library, i => i.Id == "sw.house.light.wall");
            Assert.Contains(reg.Categories, c => c.Id == SwCategories.House);
        }
    }
}
