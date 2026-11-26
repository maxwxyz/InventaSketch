using System;
using System.Linq;
using System.Text.Json;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Engine.Tests
{
    /// <summary>
    /// Engine methods the web add-in needs for the last COM-only line and medium features (EngineWeb.cs): drawing a line on
    /// from a free end (<c>lineEnds</c>, <c>extendLine</c>), junctions at crossings (<c>crossings</c>,
    /// <c>connectCrossing</c>), the medium colour legend (<c>insertMediumLegend</c>) and the Calc 6 settings
    /// (<c>valueSettings</c>).
    /// </summary>
    public class WebDrawOnApiTests
    {
        private static readonly Lazy<EngineApi> Api = new(() => new EngineApi(ModuleCatalog.CreateDefault()));
        private static Registry Reg => Api.Value.Registry;

        private static JsonElement Call(string method, object args) => EngineApiTests.Call(Api.Value, method, args);

        private static string Open(SketchDocument doc, string lang = "en")
            => Call("createSession", new { lang, json = DocumentJson.Serialize(doc) }).GetProperty("session").GetString()!;

        private static SketchDocument Doc(params SketchObject[] objects)
        {
            var doc = SketchDocument.Create();
            return Edits.Add(doc, doc.Pages[0].Id, objects);
        }

        private static SketchObject Wire(Vec2 a, Vec2 b)
            => Reg.Create(Reg.Library.First(i => i.TypeId == "elec.wire"), Vec2.Zero).WithParam("route", "free") with { Points = new[] { a, b } };

        private static JsonElement Points(JsonElement state, string id) => EngineApiTests.Obj(state, id).GetProperty("points");

        [Fact]
        public void FreeEndsAreListedAndDrawingOnAddsThePointsInOneStep()
        {
            var w = Wire(new Vec2(20, 40), new Vec2(80, 40));
            var session = Open(Doc(w));
            var ends = Call("lineEnds", new { session, id = w.Id }).EnumerateArray().ToList();
            Assert.Equal(2, ends.Count);
            var end = ends.Single(e => !e.GetProperty("atStart").GetBoolean());
            Assert.Equal(1, end.GetProperty("index").GetInt32());
            Assert.Equal(80, end.GetProperty("x").GetDouble(), 6);
            Assert.Equal(1, end.GetProperty("dx").GetDouble(), 6);

            // The preview changes nothing.
            var preview = Call("extendLine", new { session, id = w.Id, index = 1, points = new[] { new[] { 100.0, 40.0 } }, preview = true });
            Assert.Contains("<svg", preview.GetProperty("svg").GetString());
            Assert.False(Call("state", new { session, includeSvg = false }).GetProperty("canUndo").GetBoolean());

            // Two corners from the end: one undo step, the line stays selected.
            var st = Call("extendLine", new { session, id = w.Id, index = 1, points = new[] { new[] { 100.0, 40.0 }, new[] { 100.0, 70.0 } } });
            var pts = Points(st, w.Id);
            Assert.Equal(4, pts.GetArrayLength());
            Assert.Equal(70, pts[3].GetProperty("y").GetDouble(), 6);
            Assert.Equal(new[] { w.Id }, st.GetProperty("selection").EnumerateArray().Select(x => x.GetString()));
            st = Call("undo", new { session });
            Assert.Equal(2, Points(st, w.Id).GetArrayLength());

            // From the start: the new point goes in front.
            st = Call("extendLine", new { session, id = w.Id, index = 0, points = new[] { new[] { 10.0, 30.0 } } });
            Assert.Equal(10, Points(st, w.Id)[0].GetProperty("x").GetDouble(), 6);
            Assert.Contains("must be an end", Api.Value.Call("extendLine", JsonSerializer.Serialize(new { session, id = w.Id, index = 1, points = new[] { new[] { 0.0, 0.0 } } })));
        }

        [Fact]
        public void AStraightLineDrawnOnBecomesAPolyline()
        {
            var item = Reg.AllItems.First(i => i.TypeId == "ann.line");
            var line = Reg.CreateBetween(item, new Vec2(10, 10), new Vec2(50, 10));
            var session = Open(Doc(line));
            Assert.Equal(2, Call("lineEnds", new { session, id = line.Id }).GetArrayLength());
            var st = Call("extendLine", new { session, id = line.Id, index = 1, points = new[] { new[] { 50.0, 40.0 } } });
            Assert.Equal(3, Points(st, line.Id).GetArrayLength());
            Assert.NotEqual("ann.line", EngineApiTests.Obj(st, line.Id).GetProperty("type").GetString());
        }

        [Fact]
        public void ACrossingIsListedAndConnectingSplitsTheLineThere()
        {
            var a = Wire(new Vec2(20, 40), new Vec2(120, 40));
            var b = Wire(new Vec2(70, 10), new Vec2(70, 90));
            var session = Open(Doc(a, b));
            var list = Call("crossings", new { session, id = b.Id }).EnumerateArray().ToList();
            var c = Assert.Single(list);
            Assert.Equal(a.Id, c.GetProperty("other").GetString());
            Assert.Equal(70, c.GetProperty("x").GetDouble(), 6);
            Assert.Equal(40, c.GetProperty("y").GetDouble(), 6);
            Assert.True(c.GetProperty("above").GetBoolean());

            var st = Call("connectCrossing", new { session, id = b.Id, index = 0 });
            Assert.Equal(3, st.GetProperty("objects").GetArrayLength());
            Assert.Equal(new[] { b.Id }, st.GetProperty("selection").EnumerateArray().Select(x => x.GetString()));
            Assert.Equal("Connect", st.GetProperty("undoLabel").GetString());
            Assert.Empty(Call("crossings", new { session, id = b.Id }).EnumerateArray());
            Assert.Contains("Unknown crossing", Api.Value.Call("connectCrossing", JsonSerializer.Serialize(new { session, id = b.Id, index = 3 })));
        }

        [Fact]
        public void TheLegendListsTheColoursUsedAsOneGroup()
        {
            var item = Reg.AllItems.First(i => i.TypeId == "elec.wire");
            var pe = Reg.CreateBetween(item, new Vec2(10, 50), new Vec2(80, 50)).WithParam("lineRole", "PE");
            var l1 = Reg.CreateBetween(item, new Vec2(10, 60), new Vec2(80, 60)).WithParam("lineRole", "L1");
            var session = Open(Doc(pe, l1));
            var used = Call("mediumColours", new { session, mode = "used" }).EnumerateArray().Select(c => c.GetProperty("id").GetString()).ToList();
            Assert.Equal(new[] { "L1", "PE" }, used);

            var r = Call("insertMediumLegend", new { session });
            Assert.True(r.GetProperty("inserted").GetBoolean());
            var st = r.GetProperty("state");
            // Title, two lines (green-yellow has a dashed copy, so three) and two names, grouped and selected.
            var added = st.GetProperty("objects").EnumerateArray().Where(o => o.GetProperty("id").GetString() != pe.Id && o.GetProperty("id").GetString() != l1.Id).ToList();
            var grouped = added.Where(o => o.TryGetProperty("groupId", out var g) && g.ValueKind == JsonValueKind.String).ToList();
            Assert.True(grouped.Count >= 6, "legend objects: " + grouped.Count);
            Assert.Single(grouped.Select(o => o.GetProperty("groupId").GetString()).Distinct());
            Assert.Equal("Legend", st.GetProperty("undoLabel").GetString());
            Call("undo", new { session });

            // Only the given colours; nothing used: nothing inserted.
            r = Call("insertMediumLegend", new { session, json = "[\"PE\"]" });
            Assert.True(r.GetProperty("inserted").GetBoolean());
            var empty = Open(Doc());
            r = Call("insertMediumLegend", new { session = empty });
            Assert.False(r.GetProperty("inserted").GetBoolean());
            Assert.False(r.GetProperty("state").GetProperty("canUndo").GetBoolean());
        }

        [Fact]
        public void ValueSettingsReportMutedChecksAndPreferredUnits()
        {
            var session = Open(Doc());
            var s0 = Call("valueSettings", new { session });
            Assert.Empty(s0.GetProperty("mutedChecks").EnumerateArray());
            Call("muteCheck", new { session, key = "elec.r.power" });
            Call("setUnitPreference", new { session, quantityKind = "pressure", unit = "MPa" });
            var s1 = Call("valueSettings", new { session });
            Assert.Equal(new[] { "elec.r.power" }, s1.GetProperty("mutedChecks").EnumerateArray().Select(x => x.GetString()));
            Assert.Equal("MPa", s1.GetProperty("unitPreferences").GetProperty("pressure").GetString());
        }

        [Fact]
        public void ShowValuesInOffersKnownUnitsOfEachKind()
        {
            // The web add-in's "Show values in" choice (as the COM add-in's ribbon): every offered unit is accepted.
            var session = Open(Doc());
            var offered = new (string Kind, string[] Units)[]
            {
                ("pressure", new[] { "bar", "MPa", "kPa" }),
                ("rotationalSpeed", new[] { "min⁻¹", "rev/s", "rad/s" }),
                ("energy", new[] { "kWh", "MJ", "J" }),
                ("power", new[] { "W", "kW" }),
            };
            foreach (var (kind, units) in offered)
                foreach (var unit in units)
                {
                    var r = Call("setUnitPreference", new { session, quantityKind = kind, unit });
                    Assert.Equal(unit, r.GetProperty("unitPreferences").GetProperty(kind).GetString());
                }
            var cleared = Call("setUnitPreference", new { session, quantityKind = "power", unit = "" });
            Assert.False(cleared.GetProperty("unitPreferences").TryGetProperty("power", out _));
        }
    }
}
