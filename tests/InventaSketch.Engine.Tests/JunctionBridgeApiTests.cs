using System;
using System.Linq;
using System.Text.Json;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Engine.Tests
{
    /// <summary>
    /// Junction ⇄ bridge through the engine (web and LibreOffice multi-selection bar, maintainer 2026-11-26): two crossing
    /// wires get "Connect (junction)", wires meeting at a junction "Make bridge (not connected)"; a single wire at a junction
    /// and a junction dot offer the bridge as a type action.
    /// </summary>
    public class JunctionBridgeApiTests
    {
        private static readonly Lazy<EngineApi> Api = new(() => new EngineApi(ModuleCatalog.CreateDefault()));
        private static Registry Reg => Api.Value.Registry;

        private static JsonElement Call(string method, object args) => EngineApiTests.Call(Api.Value, method, args);

        private static string Open(SketchDocument doc)
            => Call("createSession", new { lang = "en", json = DocumentJson.Serialize(doc) }).GetProperty("session").GetString()!;

        private static SketchDocument Doc(params SketchObject[] objects)
        {
            var doc = SketchDocument.Create();
            return Edits.Add(doc, doc.Pages[0].Id, objects);
        }

        private static SketchObject Wire(string item, Vec2 a, Vec2 b)
            => Reg.Create(Reg.Item(item)!, Vec2.Zero).WithParam("route", "free") with { Points = new[] { a, b } };

        private static string[] Options(string session, params string[] ids)
            => Call("arrangeOptions", new { session, ids }).EnumerateArray().Select(o => o.GetProperty("id").GetString()!).ToArray();

        [Fact]
        public void TwoCrossingWiresConnectAndBecomeABridgeAgain()
        {
            var under = Wire("elec.wire", new Vec2(70, 10), new Vec2(70, 90));
            // A cabinet core of another colour: the same wire type, hops over the first and connects to it.
            var over = Wire("elec.wire.bu", new Vec2(20, 40), new Vec2(120, 40));
            var session = Open(Doc(under, over));
            var opts = Options(session, under.Id, over.Id);
            Assert.Contains("junction", opts);
            Assert.DoesNotContain("bridge", opts);
            var junction = Call("arrangeOptions", new { session, ids = new[] { under.Id, over.Id }, lang = "de" }).EnumerateArray()
                .Single(o => o.GetProperty("id").GetString() == "junction");
            Assert.Equal("Verbinden (Abzweig)", junction.GetProperty("label").GetString());
            Assert.Equal(IconIds.Junction, junction.GetProperty("icon").GetString());

            var st = Call("arrange", new { session, ids = new[] { under.Id, over.Id }, action = "junction" });
            var objects = st.GetProperty("objects").EnumerateArray().Select(o => o.GetProperty("id").GetString()!).ToList();
            Assert.Equal(3, objects.Count);
            var part = objects.Single(id => id != under.Id && id != over.Id);
            var after = Options(session, under.Id, over.Id, part);
            Assert.Contains("bridge", after);
            Assert.DoesNotContain("junction", after);
            // The first selected wire was split and docked onto the other; a half alone offers the bridge as its own action,
            // the wire passing through (no end at the junction) does not.
            string[] Actions(string id) => Call("contextActions", new { session, ids = new[] { id } }).EnumerateArray().Select(a => a.GetProperty("id").GetString()!).ToArray();
            Assert.Contains("t:elec.bridge", Actions(under.Id));
            Assert.Contains("t:elec.bridge", Actions(part));
            Assert.DoesNotContain("t:elec.bridge", Actions(over.Id));

            var back = Call("arrange", new { session, ids = new[] { over.Id, under.Id, part }, action = "bridge" });
            Assert.Equal(2, back.GetProperty("objects").GetArrayLength());
            Assert.Single(Call("crossings", new { session, id = over.Id }).EnumerateArray());
            Assert.Contains("junction", Options(session, under.Id, over.Id));
        }

        [Fact]
        public void APipeAndAWireThatDoNotCrossOfferNeither()
        {
            var a = Wire("elec.wire", new Vec2(20, 40), new Vec2(120, 40));
            var b = Wire("elec.wire", new Vec2(20, 60), new Vec2(120, 60));
            var session = Open(Doc(a, b));
            var opts = Options(session, a.Id, b.Id);
            Assert.DoesNotContain("junction", opts);
            Assert.DoesNotContain("bridge", opts);
        }
    }
}
