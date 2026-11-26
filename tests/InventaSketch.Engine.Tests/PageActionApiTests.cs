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

        private static string[] NumeralTexts(JsonElement state)
            => state.GetProperty("objects").EnumerateArray().Where(o => o.GetProperty("type").GetString() == "patent.numeral")
                .Select(o => o.GetProperty("ref").GetString()!).OrderBy(n => n, StringComparer.Ordinal).ToArray();

        /// <summary>
        /// Slides kept in sessions of their own (the web add-in, LibreOffice): <c>numeralsOf</c> summarises the other slides'
        /// payloads, <c>applyPageAction</c>, the numeral's <c>applyAction</c> and <c>preflight</c> take them as <c>elsewhere</c>.
        /// </summary>
        [Fact]
        public void NumberingAndPreflightSeeTheOtherSlides()
        {
            var api = new EngineApi(ModuleCatalog.CreateDefault());
            // Two parts of different kinds (type and parameters).
            var parts = api.Registry.Library.Select(i => (i.Id, Obj: api.Registry.Create(i, new Vec2(0, 0)))).Where(x => PartsList.IsPart(api.Registry, x.Obj))
                .GroupBy(x => InventaSketch.Modules.Patent.ReferenceNumerals.PartKind(x.Obj)).Select(g => g.First().Id).Take(2).ToList();
            // Slide 1: part X numbered 10.
            var one = Call(api, "createSession", new { lang = "en" }).GetProperty("session").GetString()!;
            EngineApiTests.Insert(api, one, parts[0], 60, 60);
            var state1 = Call(api, "applyPageAction", new { session = one, action = "patent.numerals.auto=tens" });
            Assert.Equal(new[] { "10" }, NumeralTexts(state1));
            var ids = state1.GetProperty("objects").EnumerateArray().Select(o => o.GetProperty("id").GetString()).ToList();
            var payload = Call(api, "exportCompact", new { session = one, ids }).GetProperty("payload").GetString()!;
            var summary = Call(api, "numeralsOf", new { json = JsonSerializer.Serialize(new[] { payload, "not a payload" }) });
            Assert.Equal(2, summary.GetArrayLength());
            var numerals = summary[0].EnumerateArray().ToList();
            var ten = Assert.Single(numerals);
            Assert.Equal("10", ten.GetProperty("numeral").GetString());
            Assert.False(string.IsNullOrEmpty(ten.GetProperty("kind").GetString()));
            Assert.Empty(summary[1].EnumerateArray());
            var elsewhere = new[] { new { page = "1", numerals = JsonSerializer.Deserialize<object[]>(summary[0].GetRawText()) } };

            // Slide 2: the same part X and another part Y. X keeps 10, Y continues after it.
            var two = Call(api, "createSession", new { lang = "en" }).GetProperty("session").GetString()!;
            EngineApiTests.Insert(api, two, parts[0], 60, 60);
            EngineApiTests.Insert(api, two, parts[1], 160, 60);
            Call(api, "select", new { session = two, ids = Array.Empty<string>() });
            var state2 = Call(api, "applyPageAction", new { session = two, action = "patent.numerals.auto=tens", elsewhere });
            Assert.Equal(new[] { "10", "20" }, NumeralTexts(state2));
            Assert.DoesNotContain(Call(api, "preflight", new { session = two, elsewhere, pageName = "slide" }).GetProperty("findings").EnumerateArray(),
                f => f.GetProperty("rule").GetString()!.StartsWith("PF-REF", StringComparison.Ordinal));

            // Slide 3 with part Y alone would start again at 10; the preflight with slide 1 then says that 10 names a different
            // part there.
            var three = Call(api, "createSession", new { lang = "en" }).GetProperty("session").GetString()!;
            EngineApiTests.Insert(api, three, parts[1], 60, 60);
            Assert.Equal(new[] { "10" }, NumeralTexts(Call(api, "applyPageAction", new { session = three, action = "patent.numerals.auto=tens" })));
            var findings = Call(api, "preflight", new { session = three, elsewhere, pageName = "slide" }).GetProperty("findings").EnumerateArray().ToList();
            Assert.Contains(findings, f => f.GetProperty("rule").GetString() == "PF-REF-DUPLICATE" && f.GetProperty("message").GetString()!.Contains("slide 1"));
            Assert.DoesNotContain(Call(api, "preflight", new { session = three }).GetProperty("findings").EnumerateArray(), f => f.GetProperty("rule").GetString() == "PF-REF-DUPLICATE");

            // The numeral's own menu ("Number all parts ▸ 1, 2, 3 …") with the other slide runs the page command: part X
            // takes over 10 from slide 1 (on its own the page would give it 1).
            Call(api, "undo", new { session = three });
            Assert.Equal(new[] { "20" }, NumeralTexts(Call(api, "applyPageAction", new { session = three, action = "patent.numerals.auto=tens", elsewhere })));
            EngineApiTests.Insert(api, three, parts[0], 160, 60);
            var numeral = Call(api, "select", new { session = three, ids = Array.Empty<string>() }).GetProperty("objects").EnumerateArray()
                .First(o => o.GetProperty("type").GetString() == "patent.numeral").GetProperty("id").GetString()!;
            var viaMenu = Call(api, "applyAction", new { session = three, ids = new[] { numeral }, action = "t:patent.numerals.auto=ones", elsewhere });
            Assert.Equal(new[] { "10", "20" }, NumeralTexts(viaMenu));
            Assert.Equal(new[] { "20" }, NumeralTexts(Call(api, "undo", new { session = three })));
        }
    }
}
