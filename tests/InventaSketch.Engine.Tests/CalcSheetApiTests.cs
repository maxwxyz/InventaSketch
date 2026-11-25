using System;
using System.Linq;
using System.Text.Json;
using InventaSketch.Core;
using InventaSketch.Engine;
using Xunit;

namespace InventaSketch.Engine.Tests
{
    /// <summary>Engine method <c>calcSheet</c> (Calc 6) and the tolerance range in <c>values</c>.</summary>
    public class CalcSheetApiTests
    {
        private static JsonElement Call(EngineApi api, string method, object args) => EngineApiTests.Call(api, method, args);

        private static readonly Lazy<EngineApi> Api = new(() => new EngineApi(ModuleCatalog.CreateDefault()));

        [Fact]
        public void CalcSheetInEveryFormatAndRangesInValues()
        {
            var api = Api.Value;
            var session = Call(api, "createSession", new { lang = "de" }).GetProperty("session").GetString()!;
            var id = EngineApiTests.Insert(api, session, "elec.resistor", 50, 50);
            Call(api, "setValue", new { session, id, key = "R", text = "100 Ω ±10 %" });
            Call(api, "setValue", new { session, id, key = "I", text = "2 A" });

            var values = Call(api, "values", new { session, id }).GetProperty("objects").EnumerateArray().Single();
            var u = values.GetProperty("vars").EnumerateArray().Single(v => v.GetProperty("key").GetString() == "U");
            Assert.Equal(180, u.GetProperty("lo").GetDouble(), 6);
            Assert.Equal(220, u.GetProperty("hi").GetDouble(), 6);
            Assert.Equal("180 V … 220 V", u.GetProperty("range").GetString());

            var csv = Call(api, "calcSheet", new { session, ids = new[] { id } });
            Assert.StartsWith("Element;Größe;Zeichen;Wert;Einheit;Bereich;Formel;Herkunft;Quelle", csv.GetProperty("text").GetString());
            Assert.Equal(4, csv.GetProperty("rows").GetInt32());
            Assert.Contains("S7", csv.GetProperty("sources").EnumerateArray().Select(s => s.GetProperty("id").GetString()));
            var md = Call(api, "calcSheet", new { session, ids = new[] { id }, mode = "markdown" }).GetProperty("text").GetString()!;
            Assert.Contains("| U |", md.Replace("|  U |", "| U |"));
            Assert.Contains("Quellen:", md);
            Assert.Contains("\\begin{tabular}", Call(api, "calcSheet", new { session, ids = new[] { id }, mode = "latex" }).GetProperty("text").GetString());
            var cells = Call(api, "calcSheet", new { session, ids = new[] { id }, mode = "table" }).GetProperty("text").GetString()!;
            Assert.Equal(5, cells.Split('\n').Length);
            Assert.Contains("200 V (180 … 220)", cells);
            Assert.Throws<ArgumentException>(() => api.CalcSheetOrThrow(session, "pdf"));
        }
    }

    internal static class CalcSheetApiExtensions
    {
        /// <summary>Runs calcSheet with an unknown mode and rethrows the engine's error as ArgumentException.</summary>
        public static void CalcSheetOrThrow(this EngineApi api, string session, string mode)
        {
            var json = api.CalcSheet(JsonSerializer.Serialize(new { session, mode }));
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("error", out var e)) throw new ArgumentException(e.ToString());
        }
    }
}
