using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Engine.Tests
{
    /// <summary>Importing data files (<see cref="Imports"/>, engine <c>importData</c>, <c>importGeo</c>).</summary>
    public class ImportTests
    {
        private static (EngineApi Api, string Session) Session()
        {
            var api = new EngineApi(ModuleCatalog.CreateDefault());
            var st = EngineApiTests.Call(api, "createSession", new { lang = "de" });
            return (api, st.GetProperty("session").GetString()!);
        }

        [Fact]
        public void CsvBecomesChartData()
        {
            // German Excel: semicolons and decimal commas.
            Assert.Equal("x;Druck\n0;1.5\n1;2.25", Imports.CsvToChartData("x;Druck\r\n0;1,5\r\n\r\n1;2,25\r\n"));
            // English CSV with a quoted field containing the separator.
            Assert.Equal("Month;Sales\nJan, early;3.5\nFeb;4", Imports.CsvToChartData("Month,Sales\n\"Jan, early\",3.5\nFeb,4"));
            // Tab separated.
            Assert.Equal("a;b\n1;2", Imports.CsvToChartData("a\tb\n1\t2"));
        }

        [Fact]
        public void ImportDataFillsTheSelectedChartOrInsertsOne()
        {
            var (api, session) = Session();
            var st = EngineApiTests.Call(api, "importData", new { session, text = "x;A\n0;1\n1;3\n2;2" });
            var doc = api.Sessions.Get(session).Doc;
            var chart = Assert.Single(doc.Objects.Values);
            Assert.StartsWith("chart.", chart.Type);
            Assert.Equal("x;A\n0;1\n1;3\n2;2", chart.Str("data", ""));
            // With the chart selected the next import replaces its data (no new chart).
            EngineApiTests.Call(api, "importData", new { session, text = "x;B\n0;5\n1;6" });
            doc = api.Sessions.Get(session).Doc;
            Assert.Single(doc.Objects.Values);
            Assert.Equal("x;B\n0;5\n1;6", doc.Objects.Values.Single().Str("data", ""));
        }

        private const string GeoJson = @"{ ""type"": ""FeatureCollection"", ""features"": [
            { ""type"": ""Feature"", ""properties"": { ""name"": ""Ring road"" }, ""geometry"": { ""type"": ""LineString"", ""coordinates"": [[11.55, 48.13], [11.58, 48.14], [11.60, 48.13]] } },
            { ""type"": ""Feature"", ""properties"": { ""name"": ""Office"" }, ""geometry"": { ""type"": ""Point"", ""coordinates"": [11.57, 48.135] } } ] }";

        private const string Gpx = @"<?xml version=""1.0""?><gpx version=""1.1"" xmlns=""http://www.topografix.com/GPX/1/1"">
            <trk><name>Run</name><trkseg><trkpt lat=""48.10"" lon=""11.50""/><trkpt lat=""48.11"" lon=""11.52""/><trkpt lat=""48.12"" lon=""11.51""/></trkseg></trk></gpx>";

        [Fact]
        public void GeoJsonAndGpxAreParsed()
        {
            var g = Imports.ParseGeo(GeoJson)!;
            Assert.Single(g.Lines);
            Assert.Equal("Ring road", g.Lines[0].Name);
            Assert.Equal(("Office", new Vec2(11.57, 48.135)), (g.Points[0].Name, g.Points[0].LonLat));
            var t = Imports.ParseGeo(Gpx)!;
            Assert.Equal(3, Assert.Single(t.Lines).LonLat.Count);
            Assert.Null(Imports.ParseGeo("not a file"));
            // North up: the northern point is drawn above the southern one, and everything fits the area.
            var area = new Rect(10, 10, 100, 60);
            var map = Imports.Fit(t, area);
            Assert.True(map(new Vec2(11.51, 48.12)).Y < map(new Vec2(11.50, 48.10)).Y);
            Assert.All(t.Lines[0].LonLat.Select(map), p => Assert.True(p.X >= 10 - 1e-6 && p.X <= 110 + 1e-6 && p.Y >= 10 - 1e-6 && p.Y <= 70 + 1e-6));
        }

        [Fact]
        public void ImportGeoCreatesRoutesAndMarkersOnThePage()
        {
            var (api, session) = Session();
            EngineApiTests.Call(api, "importGeo", new { session, text = GeoJson });
            var doc = api.Sessions.Get(session).Doc;
            Assert.Single(doc.Objects.Values, o => o.Type == "map.route");
            var office = Assert.Single(doc.Objects.Values, o => o.Type == "map.marker");
            Assert.Equal("Office", office.Text);

            var (api2, session2) = Session();
            EngineApiTests.Call(api2, "importGeo", new { session = session2, text = Gpx });
            var doc2 = api2.Sessions.Get(session2).Doc;
            var route = Assert.Single(doc2.Objects.Values, o => o.Type == "map.route");
            Assert.Equal(2, doc2.Objects.Values.Count(o => o.Type == "map.marker")); // start and finish for a bare track
            var page = doc2.Pages[0].Size;
            Assert.All(route.Points!, p => Assert.True(p.X > 0 && p.X < page.W && p.Y > 0 && p.Y < page.H));
            Assert.ThrowsAny<System.Exception>(() => EngineApiTests.Call(api2, "importGeo", new { session = session2, text = "{}" }));
        }
    }
}
