using System.Linq;
using System.Text.Json;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Engine.Tests
{
    /// <summary>Motion mode through the engine API (web and LibreOffice hosts).</summary>
    public class MotionApiTests
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
        public void TurningAGearTurnsItsMate()
        {
            var (api, session) = Session();
            var gear = EngineApiTests.Insert(api, session, ItemOf(api, "mech.gear.spur", "axial"), 100, 100);
            var ports = EngineApiTests.Call(api, "buildPorts", new { session, id = gear }).EnumerateArray().ToList();
            var mesh = ports.First(p => p.GetProperty("kind").GetString() == "Mesh").GetProperty("id").GetString();
            var item = EngineApiTests.Call(api, "buildItems", new { session, id = gear, key = mesh }).EnumerateArray().First().GetProperty("id").GetString();
            var st = EngineApiTests.Call(api, "buildPlace", new { session, id = gear, key = mesh, itemId = item });
            var mate = st.GetProperty("objects").EnumerateArray().Select(o => o.GetProperty("id").GetString()).First(x => x != gear);
            var info = EngineApiTests.Call(api, "motionInfo", new { session, id = gear });
            Assert.Equal("gear", info.GetProperty("kind").GetString());
            Assert.True(info.GetProperty("coupled").GetBoolean());
            var prev = EngineApiTests.Call(api, "motionPreview", new { session, id = gear, degrees = 30 });
            Assert.True(prev.GetProperty("ids").GetArrayLength() >= 2);
            Assert.StartsWith("<svg", prev.GetProperty("svg").GetString());
            var before = api.Sessions.Get(session).Doc.Get(mate)!.Transform.Rotation;
            EngineApiTests.Call(api, "motionTurn", new { session, id = gear, degrees = 30 });
            Assert.NotEqual(before, api.Sessions.Get(session).Doc.Get(mate)!.Transform.Rotation);
        }

        [Fact]
        public void APredefinedMechanismTurnsItsCrankAndThePreviewShowsFollowingLinks()
        {
            var (api, session) = Session();
            var mech = EngineApiTests.Insert(api, session, ItemOf(api, "kin.mech.fourbar"), 100, 100);
            Assert.Equal("mechanism", EngineApiTests.Call(api, "motionInfo", new { session, id = mech }).GetProperty("kind").GetString());
            var a0 = api.Sessions.Get(session).Doc.Get(mech)!.Num("angle", 0);
            EngineApiTests.Call(api, "motionTurn", new { session, id = mech, degrees = 20 });
            Assert.Equal(a0 + 20, api.Sessions.Get(session).Doc.Get(mech)!.Num("angle", 0), 3);

            // Exploded: dragging the crank moves the rocker too (it follows only through docked joints).
            var st = EngineApiTests.Call(api, "explode", new { session, id = mech });
            var crank = st.GetProperty("objects").EnumerateArray().First(o => o.GetProperty("type").GetString() == "kin.link.binary").GetProperty("id").GetString()!;
            var doc = api.Sessions.Get(session).Doc;
            var p = doc.ResolvePoints(doc.Get(crank)!);
            var prev = EngineApiTests.Call(api, "motionPreview", new { session, id = crank, index = 1, x = p[1].X + 2, y = p[1].Y - 2 });
            Assert.True(prev.GetProperty("ids").GetArrayLength() >= 3, "crank, coupler and rocker move");
        }

        [Fact]
        public void DraggingALinkKeepsLengths()
        {
            var (api, session) = Session();
            var mech = EngineApiTests.Insert(api, session, ItemOf(api, "kin.mech.fourbar"), 100, 100);
            var st = EngineApiTests.Call(api, "explode", new { session, id = mech });
            var crank = st.GetProperty("objects").EnumerateArray().First(o => o.GetProperty("type").GetString() == "kin.link.binary").GetProperty("id").GetString()!;
            Assert.Equal("linkage", EngineApiTests.Call(api, "motionInfo", new { session, id = crank }).GetProperty("kind").GetString());
            var doc = api.Sessions.Get(session).Doc;
            var p = doc.ResolvePoints(doc.Get(crank)!);
            var len = p[0].DistanceTo(p[1]);
            EngineApiTests.Call(api, "motionDrag", new { session, id = crank, index = 1, x = p[1].X + 3, y = p[1].Y - 3 });
            var doc2 = api.Sessions.Get(session).Doc;
            var q = doc2.ResolvePoints(doc2.Get(crank)!);
            Assert.Equal(len, q[0].DistanceTo(q[1]), 3);
            Assert.True(q[1].DistanceTo(p[1]) > 0.5);
        }
    }
}
