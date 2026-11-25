using System.Linq;
using System.Text.Json;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Engine.Tests
{
    /// <summary>Engine API for parts of an element (charts v2): parts, partAt, partActions, applyPartAction, setPartLook.</summary>
    public class PartApiTests
    {
        [Fact]
        public void HostsListHitStyleAndUndoTheParts()
        {
            var api = new EngineApi(ModuleCatalog.CreateDefault());
            var session = EngineApiTests.Call(api, "createSession", new { lang = "de" }).GetProperty("session").GetString()!;
            var id = EngineApiTests.Insert(api, session, "chart.line", 100, 80);

            var parts = EngineApiTests.Call(api, "parts", new { session, id }).EnumerateArray().ToList();
            Assert.Contains(parts, p => p.GetProperty("key").GetString() == "series.0" && p.GetProperty("kind").GetString() == "series");
            Assert.Contains(parts, p => p.GetProperty("key").GetString() == "axis.x" && p.GetProperty("label").GetString() == "Achse x");

            // The curve's own actions come first, the generic ones after.
            var actions = EngineApiTests.Call(api, "partActions", new { session, id, part = "series.0" }).EnumerateArray().Select(a => a.GetProperty("id").GetString()).ToList();
            Assert.Contains("chart.smooth", actions);
            Assert.Contains("part.hide", actions);

            EngineApiTests.Call(api, "applyPartAction", new { session, id, part = "grid", action = "part.hide" });
            var o = api.Sessions.Get(session).Doc.Get(id)!;
            Assert.True(o.Parts!["grid"].Hidden);

            // Native formatting folded back: one curve red and thicker, merged into its look.
            EngineApiTests.Call(api, "setPartLook", new { session, ids = new[] { id }, part = "series.0", style = new { stroke = "#c00000" } });
            EngineApiTests.Call(api, "setPartLook", new { session, ids = new[] { id }, part = "series.0", style = new { strokeWidth = 0.9 } });
            o = api.Sessions.Get(session).Doc.Get(id)!;
            Assert.Equal("#c00000", o.Parts!["series.0"].Style!.Stroke);
            Assert.Equal(0.9, o.Parts["series.0"].Style!.StrokeWidth);
            var listed = EngineApiTests.Call(api, "parts", new { session, id }).EnumerateArray().Single(p => p.GetProperty("key").GetString() == "series.0");
            Assert.Equal("#c00000", listed.GetProperty("style").GetProperty("stroke").GetString());
            Assert.True(EngineApiTests.Call(api, "parts", new { session, id }).EnumerateArray().Single(p => p.GetProperty("key").GetString() == "grid.major").GetProperty("hidden").GetBoolean());

            // Hit test at the curve's middle point (page coordinates).
            var line = api.Registry.Render(o, new RenderContext(api.Sessions.Get(session).Doc)).OfType<PathPrim>().Single(p => p.Tag == "series.0");
            var pts = PartLooks.Sample(line.Segments);
            var at = pts[pts.Count / 2];
            var hit = EngineApiTests.Call(api, "partAt", new { session, id, x = at.X, y = at.Y, tolerance = 0.3 });
            Assert.Equal("series.0", hit.GetProperty("part").GetString());

            // Outline of the selected part for host highlighting (page mm polylines).
            var outline = EngineApiTests.Call(api, "partOutline", new { session, id, part = "series.0" }).EnumerateArray().ToList();
            Assert.NotEmpty(outline);
            Assert.True(outline[0].GetArrayLength() >= 2);

            // Each change is one undo step.
            EngineApiTests.Call(api, "undo", new { session });
            Assert.Null(api.Sessions.Get(session).Doc.Get(id)!.Parts!["series.0"].Style!.StrokeWidth);
            EngineApiTests.Call(api, "undo", new { session });
            EngineApiTests.Call(api, "undo", new { session });
            Assert.Null(api.Sessions.Get(session).Doc.Get(id)!.Parts);
        }
    }
}
