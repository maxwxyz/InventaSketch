using System.Linq;
using InventaSketch.Engine;
using Xunit;

namespace InventaSketch.Engine.Tests
{
    /// <summary>Choices among actions and the point/curve handles through the engine API (web and LibreOffice hosts).</summary>
    public class ChoiceApiTests
    {
        [Fact]
        public void PartActionsReportTheirChoiceAndOptionPreviews()
        {
            var api = new EngineApi(ModuleCatalog.CreateDefault());
            var session = EngineApiTests.Call(api, "createSession", new { lang = "en" }).GetProperty("session").GetString()!;
            var id = EngineApiTests.Insert(api, session, "chart.line", 100, 80);
            var actions = EngineApiTests.Call(api, "partActions", new { session, id, part = "series.0" }).EnumerateArray().ToList();
            var below = actions.Where(a => a.TryGetProperty("choice", out var c) && c.GetString() == "chart.area.below").ToList();
            Assert.NotEmpty(below);
            Assert.All(below, a => Assert.True(a.GetProperty("choiceMenu").GetBoolean()));
            Assert.All(below, a => Assert.StartsWith("<svg", a.GetProperty("previewSvg").GetString()));
            Assert.Equal("Fill below", below[0].GetProperty("choiceLabel").GetString());
            var dash = actions.Where(a => a.TryGetProperty("choice", out var c) && c.GetString() == "part.dash").ToList();
            Assert.Equal(4, dash.Count);
            Assert.All(dash, a => Assert.False(a.GetProperty("choiceMenu").GetBoolean()));

            // Element actions: the style is a choice too (one option per value, the current one active).
            var element = EngineApiTests.Call(api, "contextActions", new { session, ids = new[] { id } }).EnumerateArray().ToList();
            var styles = element.Where(a => a.TryGetProperty("choice", out var c) && c.GetString() == "chart.style").ToList();
            Assert.Equal(4, styles.Count);
            Assert.Single(styles, a => a.GetProperty("active").GetBoolean());

            // Point and curve handles.
            var handles = EngineApiTests.Call(api, "dragHandles", new { session, ids = new[] { id } }).EnumerateArray().Single().GetProperty("handles").EnumerateArray().ToList();
            Assert.Contains(handles, h => h.GetProperty("key").GetString() == "point.0.2" && h.GetProperty("showWith").GetString() == "series.0");
            Assert.Contains(handles, h => h.GetProperty("key").GetString() == "series.0" && h.GetProperty("byPart").GetBoolean());
        }
    }
}
