using System.Linq;
using System.Text.Json;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Engine.Tests
{
    /// <summary>Engine methods requested by the web host: insertFitting, drawTrace, explode, dof, snap labels.</summary>
    public class WebWishApiTests
    {
        private static (EngineApi Api, string Session) Session()
        {
            var api = new EngineApi(ModuleCatalog.CreateDefault());
            var st = EngineApiTests.Call(api, "createSession", new { lang = "de" });
            return (api, st.GetProperty("session").GetString()!);
        }

        private static string ItemOf(EngineApi api, string typeId, string? view = null)
            => api.Registry.Library.First(i => i.TypeId == typeId && (view is null || i.View == view)).Id;

        [Fact]
        public void InsertFittingPutsAGearOnTheShaft()
        {
            var (api, session) = Session();
            var shaft = EngineApiTests.Insert(api, session, ItemOf(api, "mech.shaft", "line"), 100, 100);
            var res = EngineApiTests.Call(api, "insertFitting", new { session, id = shaft, itemId = ItemOf(api, "mech.gear.spur", "stick") });
            Assert.True(res.GetProperty("placed").GetBoolean());
            Assert.Equal(2, res.GetProperty("state").GetProperty("objects").GetArrayLength());
        }

        [Fact]
        public void ExplodeDofAndDrawTraceAreSingleUndoSteps()
        {
            var (api, session) = Session();
            var mech = EngineApiTests.Insert(api, session, ItemOf(api, "kin.mech.fourbar"), 100, 100);
            Assert.True(EngineApiTests.Call(api, "dof", new { session, id = mech }).GetProperty("explodable").GetBoolean());
            var st = EngineApiTests.Call(api, "explode", new { session, id = mech });
            var links = st.GetProperty("objects").EnumerateArray().Where(o => o.GetProperty("type").GetString() == "kin.link.binary").Select(o => o.GetProperty("id").GetString()!).ToList();
            Assert.Equal(3, links.Count);
            Assert.Equal(1, EngineApiTests.Call(api, "dof", new { session, id = links[1] }).GetProperty("dof").GetInt32());
            var before = st.GetProperty("objects").GetArrayLength();
            var traced = EngineApiTests.Call(api, "drawTrace", new { session, id = links[1] });
            Assert.Equal(before + 1, traced.GetProperty("objects").GetArrayLength());
            var undone = EngineApiTests.Call(api, "undo", new { session });
            Assert.Equal(before, undone.GetProperty("objects").GetArrayLength());
        }
    }
}
