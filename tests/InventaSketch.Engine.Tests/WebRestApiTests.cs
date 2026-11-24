using System.Linq;
using System.Text.Json;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Engine.Tests
{
    /// <summary>Engine methods for the remaining web features: ghost positions, dimension between parts, coupling preview.</summary>
    public class WebRestApiTests
    {
        private static (EngineApi Api, string Session) Session()
        {
            var api = new EngineApi(ModuleCatalog.CreateDefault());
            var st = EngineApiTests.Call(api, "createSession", new { lang = "en" });
            return (api, st.GetProperty("session").GetString()!);
        }

        private static string ItemOf(EngineApi api, string typeId, string? view = null)
            => api.Registry.Library.First(i => i.TypeId == typeId && (view is null || i.View == view)).Id;

        [Fact]
        public void GhostsAndDimension()
        {
            var (api, session) = Session();
            var mech = EngineApiTests.Insert(api, session, ItemOf(api, "kin.mech.fourbar"), 100, 100);
            var st = EngineApiTests.Call(api, "explode", new { session, id = mech });
            var link = st.GetProperty("objects").EnumerateArray().First(o => o.GetProperty("type").GetString() == "kin.link.binary").GetProperty("id").GetString();
            var before = st.GetProperty("objects").GetArrayLength();
            var ghosts = EngineApiTests.Call(api, "drawGhosts", new { session, id = link });
            Assert.Equal(before + 12, ghosts.GetProperty("objects").GetArrayLength());

            var a = EngineApiTests.Insert(api, session, ItemOf(api, "mech.gear.spur", "axial"), 30, 200);
            var b = EngineApiTests.Insert(api, session, ItemOf(api, "mech.gear.spur", "axial"), 90, 200);
            var dim = EngineApiTests.Call(api, "dimensionBetween", new { session, ids = new[] { a, b } });
            Assert.Contains(dim.GetProperty("objects").EnumerateArray(), o => o.GetProperty("type").GetString() == "ann.dimension.aligned");
        }

        [Fact]
        public void CouplingPreviewShowsTheMeshingPartner()
        {
            var (api, session) = Session();
            var shaft = EngineApiTests.Insert(api, session, ItemOf(api, "mech.shaft", "line"), 100, 100);
            var res = EngineApiTests.Call(api, "insertFitting", new { session, id = shaft, itemId = ItemOf(api, "mech.gear.spur", "stick") });
            Assert.True(res.GetProperty("placed").GetBoolean());
            var prev = EngineApiTests.Call(api, "couplingPreview", new { session, ids = new[] { shaft }, dx = 10, dy = 0 });
            Assert.Equal(1, prev.GetProperty("ids").GetArrayLength());
            Assert.StartsWith("<svg", prev.GetProperty("svg").GetString());
            var none = EngineApiTests.Call(api, "couplingPreview", new { session, ids = new[] { shaft }, dx = 0, dy = 0 });
            Assert.Equal(0, none.GetProperty("ids").GetArrayLength());
        }
    }
}
