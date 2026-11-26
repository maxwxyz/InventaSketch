using System;
using System.Linq;
using System.Text.Json;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Engine.Tests
{
    /// <summary>Commands of the whole page without a selection: <c>pageActions</c> and <c>applyPageAction</c> ("Number all parts").</summary>
    public class PageActionApiTests
    {
        private static JsonElement Call(EngineApi api, string method, object args) => EngineApiTests.Call(api, method, args);

        private static int Numerals(JsonElement state)
            => state.GetProperty("objects").EnumerateArray().Count(o => o.GetProperty("type").GetString() == "patent.numeral");

        [Fact]
        public void NumberAllPartsIsAPageCommandWithoutASelection()
        {
            var api = new EngineApi(ModuleCatalog.CreateDefault());
            var session = Call(api, "createSession", new { lang = "de" }).GetProperty("session").GetString()!;
            // An empty page offers nothing (no command that cannot do anything).
            Assert.Empty(Call(api, "pageActions", new { session }).EnumerateArray());

            var part = api.Registry.Library.First(i => PartsList.IsPart(api.Registry, api.Registry.Create(i, new Vec2(0, 0)))).Id;
            EngineApiTests.Insert(api, session, part, 60, 60);
            EngineApiTests.Insert(api, session, part, 160, 60);
            Call(api, "select", new { session, ids = Array.Empty<string>() });

            var actions = Call(api, "pageActions", new { session }).EnumerateArray().ToList();
            Assert.Equal(new[] { "patent.numerals.auto=tens", "patent.numerals.auto=ones" }, actions.Select(a => a.GetProperty("id").GetString()));
            Assert.All(actions, a =>
            {
                Assert.True(a.GetProperty("enabled").GetBoolean());
                Assert.Equal("page", a.GetProperty("kind").GetString());
                Assert.Equal("patent.numerals.auto", a.GetProperty("choice").GetString());
                Assert.Equal("Alle Teile nummerieren", a.GetProperty("choiceLabel").GetString());
                Assert.True(a.GetProperty("choiceMenu").GetBoolean());
                Assert.Equal(IconIds.Renumber, a.GetProperty("choiceBarIcon").GetString());
            });
            Assert.Equal("Number all parts", Call(api, "pageActions", new { session, lang = "en" })[0].GetProperty("choiceLabel").GetString());

            var state = Call(api, "applyPageAction", new { session, action = "patent.numerals.auto=tens" });
            Assert.Equal(2, Numerals(state));
            Assert.True(state.GetProperty("canUndo").GetBoolean());
            // Every part has a numeral now: the commands stay, greyed out, and running one changes nothing.
            Assert.All(Call(api, "pageActions", new { session }).EnumerateArray(), a => Assert.False(a.GetProperty("enabled").GetBoolean()));
            var revision = state.GetProperty("revision").GetInt64();
            Assert.Equal(revision, Call(api, "applyPageAction", new { session, action = "patent.numerals.auto=ones" }).GetProperty("revision").GetInt64());

            // One undo step removes every numeral.
            Assert.Equal(0, Numerals(Call(api, "undo", new { session })));
            Assert.ThrowsAny<Exception>(() => Call(api, "applyPageAction", new { session, action = "patent.numerals.auto=roman" }));
        }
    }
}
