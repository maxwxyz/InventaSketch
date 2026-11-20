using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Maps.Tests
{
    public class ExportTests
    {
        private static IExporter Exporter(Registry r) => Exports.All(r).Single(e => e.Id == "map.route.stops");

        [Fact]
        public void RouteStopsListStartStopsAndDestinationWithScaledDistances()
        {
            var r = Fx.Reg();
            var (doc, page) = SampleMap.Build(r);
            var all = doc.ObjectsOn(page).ToList();
            var route = all.Single(o => o.Type == "map.route");
            Assert.Contains(Exports.For(r, doc, new[] { route }), e => e.Id == "map.route.stops");
            Assert.DoesNotContain(Exports.For(r, doc, all.Where(o => o.Type != "map.route").ToList()), e => e.Id == "map.route.stops");

            var csv = Exporter(r).Export(r, doc, new[] { route }, Lang.En).Split('\n').Select(l => l.TrimEnd('\r')).Where(l => l.Length > 0).ToList();
            Assert.Equal("Route,No,Name,Kind,Leg (km),Total (km),X (mm),Y (mm)", csv[0]);
            // start marker, stop 1, stop 2, finish marker (docked) — shape points in between are folded into the legs
            Assert.Equal(5, csv.Count);
            Assert.StartsWith("Route 1,1,Start,Route marker: Start,0,0,", csv[1]);
            Assert.StartsWith("Route 1,2,Stop 1,Numbered stop,", csv[2]);
            Assert.StartsWith("Route 1,3,Stop 2,Numbered stop,", csv[3]);
            Assert.StartsWith("Route 1,4,Finish,Route marker: Finish,", csv[4]);

            // Distances: page mm along the route divided by the scale bar's mm per km (40 mm = 1 km).
            var pts = doc.ResolvePoints(route);
            double len = 0;
            for (var i = 0; i + 1 < pts.Count; i++) len += pts[i].DistanceTo(pts[i + 1]);
            var total = double.Parse(csv[4].Split(',')[5], System.Globalization.CultureInfo.InvariantCulture);
            Assert.Equal(len / 40, total, 2);
            var legs = csv.Skip(1).Select(l => double.Parse(l.Split(',')[4], System.Globalization.CultureInfo.InvariantCulture)).Sum();
            Assert.Equal(total, legs, 2);
        }

        [Fact]
        public void WithoutScaleBarDistancesAreMillimetresAndHeadersAreGerman()
        {
            var r = Fx.Reg();
            var a = Fx.Item(r, "map.place.city", new Vec2(0, 0)) with { Text = "Altstadt" };
            var route = Fx.Line(r, "map.route", a.BoxPoint(-14.8 / 34, 0), new Vec2(50, 0), new Vec2(50, 40)) with { Text = "Tour" };
            var (doc, _) = Fx.Doc(a, route);
            var csv = Exporter(r).Export(r, doc, new[] { route }, Lang.De).Split('\n').Select(l => l.TrimEnd('\r')).Where(l => l.Length > 0).ToList();
            Assert.Equal("Route,Nr,Name,Art,Abschnitt (mm),Gesamt (mm),X (mm),Y (mm)", csv[0]);
            Assert.StartsWith("Tour,1,Altstadt,Ortsbeschriftung,0,0,", csv[1]);
            Assert.StartsWith("Tour,2,Ziel,,", csv[2]);
            Assert.Contains(",104.8,104.8,", csv[2]);
        }
    }

    public class LegendTests
    {
        [Fact]
        public void AutomaticLegendListsThePageContentInBothLanguages()
        {
            var r = Fx.Reg();
            var (doc, page) = SampleMap.Build(r);
            var legend = doc.ObjectsOn(page).Single(o => o.Type == "map.legend");
            var en = Legend.Entries(legend, new RenderContext(doc, Lang.En)).Select(e => e.Label).ToList();
            Assert.Equal("Route", en[0]);
            Assert.Contains("Motorway", en);
            Assert.Contains("Railway", en);
            Assert.Contains("River", en);
            Assert.Contains("Lake / water", en);
            Assert.Contains("Fuel station", en);
            Assert.Contains("Stop", en);
            Assert.True(en.Count <= 16);
            Assert.Equal("Start", en[1]);
            var de = Legend.Entries(legend, new RenderContext(doc, Lang.De)).Select(e => e.Label).ToList();
            Assert.Contains("Autobahn", de);
            Assert.Contains("Tankstelle", de);
            Assert.Contains("Halt", de);
            // Samples are drawn by the real types and stay inside the legend box.
            var prims = r.TypeOf(legend).Render(legend, new RenderContext(doc));
            Fx.AssertInsideBox(prims, legend.Size, "legend");
            Assert.True(prims.Count(p => p.Tag == MapTags.Sample) >= 8);
        }

        [Fact]
        public void MapObjectsReportTheLegendAsDependent()
        {
            var r = Fx.Reg();
            var (doc, page) = SampleMap.Build(r);
            var legend = doc.ObjectsOn(page).Single(o => o.Type == "map.legend");
            var ctx = new RenderContext(doc);
            Assert.True(r.TypeOf(legend).DependsOnNeighbours);
            foreach (var o in doc.ObjectsOn(page).Where(o => o.Id != legend.Id))
                Assert.Contains(legend.Id, r.TypeOf(o).Dependents(o, ctx));
            // A manual legend (text lines) does not follow the page.
            var manual = legend with { Text = "[road.main] B 17\n[poi.parking] P+R\nFree text" };
            var doc2 = Edits.Replace(doc, manual);
            var road = doc2.ObjectsOn(page).First(o => o.Type == "map.road");
            Assert.DoesNotContain(legend.Id, r.TypeOf(road).Dependents(road, new RenderContext(doc2)));
            var entries = Legend.Entries(manual, new RenderContext(doc2));
            Assert.Equal(new[] { "road.main", "poi.parking", "bullet" }, entries.Select(e => e.Key));
            Assert.Equal("P+R", entries[1].Label);
        }

        [Fact]
        public void LegendWithoutPageShowsDefaultEntries()
        {
            var r = Fx.Reg();
            var legend = Fx.Item(r, "map.legend", Vec2.Zero);
            var entries = Legend.Entries(legend, Fx.Ctx(Lang.De));
            Assert.Equal(6, entries.Count);
            Assert.Equal("Autobahn", entries[1].Label);
        }

        [Fact]
        public void LegendTitleInputAndOverrideKeepSamplesColoured()
        {
            var r = Fx.Reg();
            var legend = Fx.Item(r, "map.legend", Vec2.Zero);
            var (doc, _) = Fx.Doc(legend);
            doc = ContextActions.Run(r, doc, new[] { legend.Id }, "map.legend.title", "Zeichenerklärung", Lang.De);
            var o = doc.Get(legend.Id)! with { Style = new Style { Stroke = "#ff0000" } };
            var prims = r.Render(o, new RenderContext(doc));
            Assert.Contains(prims.OfType<TextPrim>(), t => t.Text == "Zeichenerklärung");
            var sample = (GroupPrim)prims.First(p => p.Tag == MapTags.Sample);
            Assert.DoesNotContain(Prim.Flatten(sample.Children).OfType<PathPrim>(), p => p.Style.Stroke == "#ff0000");
        }
    }
}
