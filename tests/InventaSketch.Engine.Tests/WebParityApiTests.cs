using System.Linq;
using System.Text.Json;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Engine.Tests
{
    /// <summary>
    /// Engine methods behind the web host's UX parity with the COM add-in: "Insert ▸" suggestions, the black-and-white
    /// document switch, main-text editing and the centred-part flag for resizing.
    /// </summary>
    public class WebParityApiTests
    {
        private static (EngineApi Api, string Session) DefaultSession()
        {
            var api = new EngineApi(ModuleCatalog.CreateDefault());
            var st = EngineApiTests.Call(api, "createSession", new { lang = "en" });
            return (api, st.GetProperty("session").GetString()!);
        }

        private static string ItemOf(EngineApi api, string typeId, string? view = null)
            => api.Registry.Library.First(i => i.TypeId == typeId && (view is null || i.View == view)).Id;

        private static JsonElement Obj(JsonElement state, string id)
            => state.GetProperty("objects").EnumerateArray().First(o => o.GetProperty("id").GetString() == id);

        [Fact]
        public void SuggestionsFollowTheSelectedObject()
        {
            var (api, session) = DefaultSession();
            var shaft = EngineApiTests.Insert(api, session, ItemOf(api, "mech.shaft", "line"), 100, 100);
            var list = EngineApiTests.Call(api, "suggestions", new { session, ids = new[] { shaft } }).EnumerateArray().ToList();
            Assert.Equal("mech.gear.spur", list[0].GetProperty("typeId").GetString());
            Assert.Equal("stick", list[0].GetProperty("view").GetString());
            Assert.Contains(list, i => i.GetProperty("typeId").GetString() == "patent.numeral");
            Assert.All(list, i => Assert.False(string.IsNullOrEmpty(i.GetProperty("name").GetString())));
            Assert.True(list.Count <= 9);

            var limited = EngineApiTests.Call(api, "suggestions", new { session, ids = new[] { shaft }, max = 4 }).EnumerateArray().ToList();
            Assert.Equal(4, limited.Count);
            // Without ids the current selection (the inserted shaft) is used.
            Assert.Equal(list.Count, EngineApiTests.Call(api, "suggestions", new { session }).GetArrayLength());
        }

        [Fact]
        public void SuggestionsAreEmptyWithoutSelection()
        {
            var (api, session) = DefaultSession();
            Assert.Equal(0, EngineApiTests.Call(api, "suggestions", new { session }).GetArrayLength());
        }

        [Fact]
        public void MonochromeIsAnUndoableDocumentSettingThatTouchesEveryObject()
        {
            var (api, session) = EngineApiTests.NewSession();
            var a = EngineApiTests.Insert(api, session, "etest.block", 50, 50);
            var b = EngineApiTests.Insert(api, session, "etest.block", 90, 50);
            EngineApiTests.Call(api, "setStyle", new { session, ids = new[] { a }, style = new { stroke = "#ff0000" } });

            var st = EngineApiTests.Call(api, "setMonochrome", new { session, value = true });
            Assert.True(st.GetProperty("monochrome").GetBoolean());
            Assert.Equal("Black and white", st.GetProperty("undoLabel").GetString());
            var changed = st.GetProperty("changed").EnumerateArray().Select(x => x.GetString()).ToList();
            Assert.Contains(a, changed);
            Assert.Contains(b, changed);
            Assert.DoesNotContain("#ff0000", st.GetProperty("svg").GetString()!);
            Assert.DoesNotContain("#ff0000", EngineApiTests.Call(api, "renderPageSvg", new { session }).GetProperty("svg").GetString()!);
            var plan = EngineApiTests.Call(api, "drawPlan", new { session, ids = new[] { a } });
            Assert.DoesNotContain("#ff0000", plan.GetRawText(), System.StringComparison.OrdinalIgnoreCase);

            // Same value again: no new undo step.
            var again = EngineApiTests.Call(api, "setMonochrome", new { session, value = true });
            Assert.Equal(st.GetProperty("revision").GetInt32(), again.GetProperty("revision").GetInt32());

            var undone = EngineApiTests.Call(api, "undo", new { session });
            Assert.False(undone.GetProperty("monochrome").GetBoolean());
            Assert.Contains("#ff0000", undone.GetProperty("svg").GetString()!);
        }

        [Fact]
        public void ImportFromTheSlideAdoptsItsBlackAndWhiteSetting()
        {
            var (api, session) = EngineApiTests.NewSession();
            var a = EngineApiTests.Insert(api, session, "etest.block", 50, 50);
            EngineApiTests.Call(api, "setMonochrome", new { session, value = true });
            var payload = EngineApiTests.Call(api, "exportCompact", new { session, ids = new[] { a } }).GetProperty("payload").GetString();

            var (api2, session2) = EngineApiTests.NewSession();
            var st = EngineApiTests.Call(api2, "importCompact", new { session = session2, payload });
            Assert.True(st.GetProperty("monochrome").GetBoolean());
        }

        [Fact]
        public void CentredFlagMarksSelectedPartsWithPitchCircleOrHub()
        {
            var (api, session) = EngineApiTests.NewSession();
            var block = EngineApiTests.Insert(api, session, "etest.block", 50, 50);
            var wheel = EngineApiTests.Insert(api, session, "etest.wheel", 100, 50);
            var st = EngineApiTests.Call(api, "select", new { session, ids = new[] { block, wheel } });
            Assert.True(Obj(st, wheel).GetProperty("centred").GetBoolean());
            Assert.False(Obj(st, block).GetProperty("centred").GetBoolean());
            st = EngineApiTests.Call(api, "select", new { session, ids = new[] { block } });
            Assert.False(Obj(st, wheel).GetProperty("centred").GetBoolean());
        }

        [Fact]
        public void MainTextOfTextTypesAndNumerals()
        {
            var (api, session) = DefaultSession();
            var text = EngineApiTests.Insert(api, session, ItemOf(api, "ann.text"), 50, 50);
            var st = EngineApiTests.Call(api, "state", new { session, includeSvg = false });
            var main = Obj(st, text).GetProperty("mainText");
            Assert.False(main.TryGetProperty("key", out var k) && k.ValueKind == JsonValueKind.String);
            st = EngineApiTests.Call(api, "setMainText", new { session, id = text, text = "Hello" });
            Assert.Equal("Hello", Obj(st, text).GetProperty("text").GetString());

            var numeral = EngineApiTests.Insert(api, session, ItemOf(api, "patent.numeral"), 80, 50);
            st = EngineApiTests.Call(api, "state", new { session, includeSvg = false });
            Assert.Equal("numeral", Obj(st, numeral).GetProperty("mainText").GetProperty("key").GetString());
            st = EngineApiTests.Call(api, "setMainText", new { session, text = "42" });
            Assert.Equal("42", Obj(st, numeral).GetProperty("ref").GetString());
            Assert.Equal("42", Obj(st, numeral).GetProperty("mainText").GetProperty("value").GetString());
            Assert.Equal("Text", st.GetProperty("undoLabel").GetString());
        }

        [Fact]
        public void ObjectsWithoutMainTextAreLeftAlone()
        {
            var (api, session) = EngineApiTests.NewSession();
            var block = EngineApiTests.Insert(api, session, "etest.block", 50, 50);
            var st = EngineApiTests.Call(api, "state", new { session, includeSvg = false });
            Assert.False(Obj(st, block).TryGetProperty("mainText", out var m) && m.ValueKind == JsonValueKind.Object);
            var rev = st.GetProperty("revision").GetInt32();
            st = EngineApiTests.Call(api, "setMainText", new { session, id = block, text = "x" });
            Assert.Equal(rev, st.GetProperty("revision").GetInt32());
        }

        [Fact]
        public void SetViewUsesTheDistortionFreeRegistrySwitch()
        {
            var (api, session) = DefaultSession();
            var gear = EngineApiTests.Insert(api, session, "mech.spur.axial", 100, 100);
            var s = api.Sessions.Get(session);
            var expected = api.Registry.WithView(s.Doc.Get(gear)!, "side", s.Context());
            var st = EngineApiTests.Call(api, "setView", new { session, ids = new[] { gear }, view = "side" });
            var after = Obj(st, gear);
            Assert.Equal("side", after.GetProperty("view").GetString());
            Assert.Equal(expected.Size.W, after.GetProperty("w").GetDouble(), 6);
            Assert.Equal(expected.Size.H, after.GetProperty("h").GetDouble(), 6);
        }
    }
}
