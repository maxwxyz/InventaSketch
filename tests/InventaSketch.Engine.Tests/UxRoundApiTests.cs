using System.Linq;
using System.Text.Json;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Engine.Tests
{
    /// <summary>
    /// Engine methods behind the UI/UX round (2026-11-24) for the web and LibreOffice hosts: insert search, transform
    /// effect, selection-wide operations, assembly and coupler curves.
    /// </summary>
    public class UxRoundApiTests
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
        public void SearchRanksFittingPartsFirst()
        {
            var (api, session) = Session();
            var shaft = EngineApiTests.Insert(api, session, ItemOf(api, "mech.shaft", "line"), 100, 100);
            var hits = EngineApiTests.Call(api, "search", new { session, ids = new[] { shaft }, text = "lager" }).EnumerateArray().ToList();
            Assert.NotEmpty(hits);
            Assert.True(hits[0].GetProperty("fits").GetBoolean());
            var empty = EngineApiTests.Call(api, "search", new { session, ids = new[] { shaft }, text = "" }).EnumerateArray().ToList();
            Assert.All(empty, h => Assert.True(h.GetProperty("fits").GetBoolean()));
        }

        [Fact]
        public void TransformEffectAndArrangeOptions()
        {
            var (api, session) = Session();
            var a = EngineApiTests.Insert(api, session, ItemOf(api, "mech.gear.spur", "axial"), 60, 60);
            var fx = EngineApiTests.Call(api, "transformEffect", new { session, ids = new[] { a } });
            Assert.False(fx.GetProperty("mirrorH").GetBoolean());
            var b = EngineApiTests.Insert(api, session, ItemOf(api, "mech.gear.spur", "axial"), 160, 90);
            var opts = EngineApiTests.Call(api, "arrangeOptions", new { session, ids = new[] { a, b } }).EnumerateArray().Select(o => o.GetProperty("id").GetString()).ToList();
            Assert.Contains("align:row", opts);
            Assert.Contains("mview:stick", opts);
            var st = EngineApiTests.Call(api, "arrange", new { session, ids = new[] { a, b }, action = "align:row" });
            var ys = st.GetProperty("objects").EnumerateArray().Where(o => o.GetProperty("id").GetString() is var id && (id == a || id == b))
                .Select(o => o.GetProperty("bounds").GetProperty("y").GetDouble() + o.GetProperty("bounds").GetProperty("h").GetDouble() / 2).ToList();
            Assert.Equal(ys[0], ys[1], 3);
            var after = EngineApiTests.Call(api, "arrangeOptions", new { session, ids = new[] { a, b } }).EnumerateArray().Select(o => o.GetProperty("id").GetString()).ToList();
            Assert.DoesNotContain("align:row", after);
        }

        [Fact]
        public void AssemblyOfALoneGearIsTheGear()
        {
            var (api, session) = Session();
            var g = EngineApiTests.Insert(api, session, ItemOf(api, "mech.gear.spur", "axial"), 60, 160);
            var asm = EngineApiTests.Call(api, "assembly", new { session, ids = new[] { g } }).EnumerateArray().Select(x => x.GetString()).ToList();
            Assert.Equal(new[] { g }, asm);
        }
    }
}
