using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Engine.Tests
{
    /// <summary>Engine methods for hosts without in-process charts code: sketchCurve and chartFromPicture (charts v2).</summary>
    public class ChartHostApiTests
    {
        [Fact]
        public void ASketchedRampIsAddedToTheChart()
        {
            var api = new EngineApi(ModuleCatalog.CreateDefault());
            var session = EngineApiTests.Call(api, "createSession", new { lang = "de" }).GetProperty("session").GetString()!;
            var id = EngineApiTests.Insert(api, session, "chart.line", 100, 80);
            var chart = api.Sessions.Get(session).Doc.Get(id)!;
            var b = chart.Bounds();
            // A diagonal through the middle of the plot area (the plot fills most of the box).
            var trail = Enumerable.Range(0, 40).Select(i => new[] { b.X + b.W * (0.3 + 0.5 * i / 39.0), b.Y + b.H * (0.75 - 0.5 * i / 39.0) }).ToArray();
            var result = EngineApiTests.Call(api, "sketchCurve", new { session, id, points = trail });
            Assert.False(string.IsNullOrEmpty(result.GetProperty("message").GetString()));
            var data = api.Sessions.Get(session).Doc.Get(id)!.Str("data", "");
            Assert.Contains("\n\n", data.Replace("\r", ""));
            EngineApiTests.Call(api, "undo", new { session });
            Assert.DoesNotContain("\n\n", api.Sessions.Get(session).Doc.Get(id)!.Str("data", "").Replace("\r", ""));
        }

        [Fact]
        public void APictureOfColumnsBecomesAChart()
        {
            var api = new EngineApi(ModuleCatalog.CreateDefault());
            var session = EngineApiTests.Call(api, "createSession", new { lang = "en" }).GetProperty("session").GetString()!;
            int w = 400, h = 260;
            var rgba = new byte[w * h * 4];
            void Fill(int x0, int y0, int fw, int fh, byte r, byte g, byte bl)
            {
                for (var y = y0; y < y0 + fh; y++)
                    for (var x = x0; x < x0 + fw; x++)
                    {
                        var k = (y * w + x) * 4;
                        rgba[k] = r; rgba[k + 1] = g; rgba[k + 2] = bl; rgba[k + 3] = 255;
                    }
            }
            Fill(0, 0, w, h, 255, 255, 255);
            int[] heights = { 50, 100, 75, 25, 150 };
            for (var i = 0; i < heights.Length; i++) Fill(30 + i * 60, 200 - heights[i], 36, heights[i], 30, 90, 200);
            Fill(20, 200, 360, 1, 0, 0, 0);
            var result = EngineApiTests.Call(api, "chartFromPicture", new { session, w, h, payload = Convert.ToBase64String(rgba) });
            var newId = result.GetProperty("id").GetString();
            Assert.False(string.IsNullOrEmpty(newId));
            Assert.Equal("chart.bar", api.Sessions.Get(session).Doc.Get(newId!)!.Type);
            Assert.Contains("100", result.GetProperty("message").GetString());
            // A blank picture: a message, no chart.
            var blank = EngineApiTests.Call(api, "chartFromPicture", new { session, w = 10, h = 10, payload = Convert.ToBase64String(new byte[400]) });
            Assert.False(string.IsNullOrEmpty(blank.GetProperty("message").GetString()));
        }
    }
}
