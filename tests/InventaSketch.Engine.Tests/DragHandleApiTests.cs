using System;
using System.Linq;
using System.Text.Json;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Engine.Tests
{
    /// <summary>Engine methods dragHandles and moveHandle (limit lines, value lines and markers of charts).</summary>
    public class DragHandleApiTests
    {
        [Fact]
        public void LimitLinesAreListedPreviewedAndMovedInOneUndoStep()
        {
            var api = new EngineApi(ModuleCatalog.CreateDefault());
            var session = EngineApiTests.Call(api, "createSession", new { lang = "de" }).GetProperty("session").GetString()!;
            var id = EngineApiTests.Insert(api, session, "chart.limits", 100, 80);
            var before = api.Sessions.Get(session).Doc.Get(id)!.Str("limits", "");

            var list = EngineApiTests.Call(api, "dragHandles", new { session, ids = new[] { id } });
            var obj = list.EnumerateArray().Single();
            Assert.Equal(id, obj.GetProperty("id").GetString());
            var handles = obj.GetProperty("handles").EnumerateArray().ToList();
            Assert.Equal(2, handles.Count);
            var h = handles.Single(x => x.GetProperty("key").GetString() == "limit.1");
            Assert.Equal("vertical", h.GetProperty("constraint").GetString());
            Assert.Equal(1, h.GetProperty("dy").GetDouble(), 6);
            Assert.StartsWith("Grenzlinie", h.GetProperty("label").GetString());
            Assert.Equal("limit", h.GetProperty("part").GetString());
            double x = h.GetProperty("x").GetDouble(), y = h.GetProperty("y").GetDouble();

            // Preview: the snapped handle, the document unchanged.
            var revision = api.Sessions.Get(session).Revision;
            var preview = EngineApiTests.Call(api, "moveHandle", new { session, id, key = "limit.1", x, y = y - 5, preview = true }).GetProperty("handle");
            Assert.True(preview.GetProperty("y").GetDouble() < y - 3);
            Assert.NotEqual(h.GetProperty("label").GetString(), preview.GetProperty("label").GetString());
            Assert.Equal(revision, api.Sessions.Get(session).Revision);
            Assert.Equal(before, api.Sessions.Get(session).Doc.Get(id)!.Str("limits", ""));

            // Move: one undo step.
            EngineApiTests.Call(api, "moveHandle", new { session, id, key = "limit.1", x, y = y - 5 });
            Assert.NotEqual(before, api.Sessions.Get(session).Doc.Get(id)!.Str("limits", ""));
            Assert.Equal(revision + 1, api.Sessions.Get(session).Revision);
            // Dropping it where it already is: no further undo step.
            var moved = EngineApiTests.Call(api, "dragHandles", new { session, id }).EnumerateArray().Single().GetProperty("handles").EnumerateArray().Single(e => e.GetProperty("key").GetString() == "limit.1");
            EngineApiTests.Call(api, "moveHandle", new { session, id, key = "limit.1", x = moved.GetProperty("x").GetDouble(), y = moved.GetProperty("y").GetDouble() });
            Assert.Equal(revision + 1, api.Sessions.Get(session).Revision);
            EngineApiTests.Call(api, "undo", new { session });
            Assert.Equal(before, api.Sessions.Get(session).Doc.Get(id)!.Str("limits", ""));
        }

        [Fact]
        public void ElementsWithoutHandlesAreLeftOutAndUnknownKeysChangeNothing()
        {
            var api = new EngineApi(ModuleCatalog.CreateDefault());
            var session = EngineApiTests.Call(api, "createSession", new { lang = "en" }).GetProperty("session").GetString()!;
            var line = EngineApiTests.Insert(api, session, "chart.line", 60, 60);
            var limits = EngineApiTests.Insert(api, session, "chart.limits", 160, 60);
            var list = EngineApiTests.Call(api, "dragHandles", new { session, ids = new[] { line, limits } });
            Assert.Equal(new[] { limits }, list.EnumerateArray().Select(e => e.GetProperty("id").GetString()).ToArray());
            var revision = api.Sessions.Get(session).Revision;
            EngineApiTests.Call(api, "moveHandle", new { session, id = limits, key = "nothing", x = 0.0, y = 0.0 });
            Assert.Equal(revision, api.Sessions.Get(session).Revision);
            var none = EngineApiTests.Call(api, "moveHandle", new { session, id = line, key = "limit.0", x = 0.0, y = 0.0, preview = true });
            // No such handle: "handle" is null (left out of the JSON).
            Assert.False(none.TryGetProperty("handle", out var hv) && hv.ValueKind != JsonValueKind.Null);
            Assert.Throws<ArgumentException>(() => EngineApiTests.Call(api, "moveHandle", new { session, id = limits, x = 0.0, y = 0.0 }));
        }
    }
}
