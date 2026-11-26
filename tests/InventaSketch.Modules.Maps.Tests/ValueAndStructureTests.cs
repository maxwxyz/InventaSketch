using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Maps.Tests
{
    /// <summary>Route values (length at the map scale, speed preset, travel time) and the scale they use.</summary>
    public class RouteValueTests
    {
        private static ValueTable Solve(Registry r, SketchDocument doc) => ValueSolver.Solve(doc, r, null, new ValueSolveOptions { BudgetMs = 10000 });

        private static double? Val(ValueTable t, SketchObject o, string key) => t.Get(o.Id, key)?.Value;

        /// <summary>A route of 70 mm drawn length: 40 mm right, 30 mm down.</summary>
        private static SketchObject Route(Registry r) => Fx.Line(r, "map.route", new Vec2(0, 0), new Vec2(40, 0), new Vec2(40, 30));

        /// <summary>A scale bar 40 mm wide representing 1 km: 25 m per page millimetre.</summary>
        private static SketchObject Bar(Registry r) => Fx.Item(r, "map.scalebar", new Vec2(100, 100)) with { Size = new Size2(40, 8) };

        [Fact]
        public void RouteDeclaresItsValues()
        {
            var r = Fx.Reg();
            var t = r.Type("map.route")!;
            Assert.Equal(new[] { "s", "v", "t", "up", "down", "gmax" }, ValueModel.Variables(t).Select(v => v.Key));
            Assert.Equal("map.route.time", Assert.Single(ValueModel.Relations(t)).Id);
            Assert.Equal("S2", ValueModel.Relations(t)[0].Source);
            Assert.Contains(t.Parameters, p => p.Key == ValueLabels.ShowParam);
            Assert.True(t.DependsOnNeighbours);
            var mode = t.Parameters.Single(p => p.Key == "mode");
            Assert.Equal(new[] { "walk", "bike", "car" }, mode.Options!.Select(o => o.Value));
            Assert.All(mode.Options!, o => Assert.False(string.IsNullOrEmpty(o.Label.De)));
        }

        [Fact]
        public void LengthComesFromTheScaleBarAndTimeFromTheCarPreset()
        {
            var r = Fx.Reg();
            var route = Route(r);
            var (doc, _) = Fx.Doc(route, Bar(r));
            var t = Solve(r, doc);
            Assert.Equal(1750, Val(t, route, "s")!.Value, 6);
            Assert.Equal(50 / 3.6, Val(t, route, "v")!.Value, 9);
            Assert.Equal(1750 / (50 / 3.6), Val(t, route, "t")!.Value, 6);
            Assert.Equal(Provenance.Derived, t.Get(route.Id, "s")!.Provenance);
            Assert.Empty(t.Conflicts);
        }

        [Fact]
        public void WithoutAScaleTheLengthIsUnknown()
        {
            var r = Fx.Reg();
            var route = Route(r);
            var (doc, _) = Fx.Doc(route);
            var t = Solve(r, doc);
            Assert.Null(Val(t, route, "s"));
            Assert.Null(Val(t, route, "t"));
            Assert.Equal(50 / 3.6, Val(t, route, "v")!.Value, 9);
        }

        [Fact]
        public void OwnScaleAndDocumentPlanScaleAlsoWork()
        {
            var r = Fx.Reg();
            var own = Route(r).WithParam("scale", 25000.0);
            var (doc, _) = Fx.Doc(own, Bar(r) with { Size = new Size2(10, 8) });
            Assert.Equal(1750, Val(Solve(r, doc), own, "s")!.Value, 6);
            Assert.Equal("own", MapScale.Of(own, doc)!.Origin);

            var route = Route(r);
            var (plain, _) = Fx.Doc(route);
            var planned = plain with { Settings = plain.Settings with { PlanScale = 50000 } };
            Assert.Equal(3500, Val(Solve(r, planned), route, "s")!.Value, 6);
            Assert.Equal("plan", MapScale.Of(route, planned)!.Origin);
        }

        [Theory]
        [InlineData("km", 1.0, 25.0)]
        [InlineData("mi", 1.0, 1609.344 / 40)]
        [InlineData("m", 500.0, 12.5)]
        [InlineData("ft", 1000.0, 304.8 / 40)]
        public void ScaleBarUnitsAreConverted(string unit, double length, double metresPerMm)
        {
            var r = Fx.Reg();
            var route = Route(r);
            var bar = Bar(r).WithParam("unit", unit).WithParam("length", length);
            var (doc, _) = Fx.Doc(route, bar);
            Assert.Equal(metresPerMm, MapScale.Of(route, doc)!.MetresPerMm, 9);
        }

        [Fact]
        public void SmoothRoutesAreMeasuredAlongTheCurve()
        {
            var r = Fx.Reg();
            var straight = Fx.Line(r, "map.route", new Vec2(0, 0), new Vec2(20, 20), new Vec2(40, 0));
            var smooth = straight.WithParam("smooth", true);
            var chord = Math.Sqrt(800) * 2;
            Assert.Equal(chord, MapScale.DrawnLength(straight, straight.Points!), 9);
            Assert.NotEqual(chord, MapScale.DrawnLength(smooth, smooth.Points!), 3);
        }

        [Fact]
        public void EnteredValuesWinOverDrawingAndPreset()
        {
            var r = Fx.Reg();
            var route = Route(r).WithParam("length", "12 km").WithParam("speed", "100 km/h");
            var (doc, _) = Fx.Doc(route, Bar(r));
            var t = Solve(r, doc);
            Assert.Equal(12000, Val(t, route, "s")!.Value, 6);
            Assert.Equal(12000 / (100 / 3.6), Val(t, route, "t")!.Value, 6);
            Assert.Empty(t.Conflicts);

            // Entered length and time give the speed; the mode preset steps back.
            var timed = Route(r).WithParam("length", "10 km").WithParam("time", "30 min");
            var t2 = Solve(r, Fx.Doc(timed).Doc);
            Assert.Equal(20 / 3.6, Val(t2, timed, "v")!.Value, 9);
            Assert.Empty(t2.Conflicts);

            // An entered time with the drawn length also gives the speed.
            var drawnTimed = Route(r).WithParam("time", "7 min");
            var t3 = Solve(r, Fx.Doc(drawnTimed, Bar(r)).Doc);
            Assert.Equal(1750 / 420.0, Val(t3, drawnTimed, "v")!.Value, 9);
            Assert.Empty(t3.Conflicts);
        }

        [Theory]
        [InlineData("walk", 5.0)]
        [InlineData("bike", 15.0)]
        [InlineData("car", 50.0)]
        public void TravelModesPresetTheSpeed(string mode, double kmh)
        {
            var r = Fx.Reg();
            var route = Route(r).WithParam("mode", mode);
            var (doc, _) = Fx.Doc(route, Bar(r));
            var t = Solve(r, doc);
            Assert.Equal(kmh / 3.6, Val(t, route, "v")!.Value, 9);
            Assert.Equal(1750 / (kmh / 3.6), Val(t, route, "t")!.Value, 6);
        }

        [Fact]
        public void ModeIsAChoiceActionAndTheWalkingPresetWalks()
        {
            var r = Fx.Reg();
            var route = Route(r);
            var actions = r.Type("map.route")!.Actions(route, Lang.En).Where(a => a.Id.StartsWith("map.route.mode", StringComparison.Ordinal)).ToList();
            Assert.Equal(3, actions.Count);
            // Every travel mode has its own pictogram (dropdown with option pictograms).
            Assert.Equal(new[] { IconIds.TravelWalk, IconIds.TravelBike, IconIds.TravelCar }, actions.Select(a => a.Icon));
            Assert.True(RouteValues.ModeDef.HasOptionIcons);
            var callout = r.Type("map.callout")!.Actions(Fx.Item(r, "map.callout.walk", Vec2.Zero), Lang.En).Where(a => a.Id.StartsWith("map.callout.mode=", StringComparison.Ordinal)).ToList();
            Assert.Equal(new[] { IconIds.TravelCar, IconIds.TravelWalk, IconIds.TravelBike, IconIds.TravelTransit, IconIds.TravelNone }, callout.Select(a => a.Icon));
            var walk = actions.Single(a => a.Id.EndsWith("walk", StringComparison.Ordinal)).Apply(route);
            Assert.Equal("walk", walk.Str("mode", ""));
            Assert.Equal("walk", Fx.Item(r, "map.route.dotted", Vec2.Zero).Str("mode", ""));
        }

        [Fact]
        public void ValuesLabelShowsLengthSpeedAndTime()
        {
            var r = Fx.Reg();
            var route = Route(r).WithParam(ValueLabels.ShowParam, true);
            var (doc, _) = Fx.Doc(route, Bar(r));
            var label = r.Render(route, new RenderContext(doc)).OfType<TextPrim>().Single(p => p.Tag == ValueLabels.Part);
            Assert.Contains("s = 1.75 km", label.Text);
            Assert.Contains("v = 50 km/h", label.Text);
            Assert.Contains("t = ", label.Text);
            var de = r.Render(route, new RenderContext(doc, Lang.De)).OfType<TextPrim>().Single(p => p.Tag == ValueLabels.Part);
            Assert.Contains("1,75 km", de.Text);
        }

        [Fact]
        public void TheScaleBarRedrawsRoutesThatShowTheirValues()
        {
            var r = Fx.Reg();
            var shows = Route(r).WithParam(ValueLabels.ShowParam, true);
            var plain = Route(r);
            var bar = Bar(r);
            var (doc, _) = Fx.Doc(shows, plain, bar);
            var t = r.Type("map.scalebar")!;
            Assert.True(t.HasDependents);
            Assert.False(t.DependsOnNeighbours);
            var deps = t.Dependents(bar, new RenderContext(doc)).ToList();
            Assert.Contains(shows.Id, deps);
            Assert.DoesNotContain(plain.Id, deps);
        }

        [Fact]
        public void ValuesSurviveTheJsonRoundTrip()
        {
            var r = Fx.Reg();
            var route = Route(r).WithParam("length", "12,5 km").WithParam("mode", "bike").WithParam("scale", 50000.0).WithParam(ValueLabels.ShowParam, true);
            var (doc, _) = Fx.Doc(route);
            var back = DocumentJson.Deserialize(DocumentJson.Serialize(doc));
            Assert.Equal(DocumentJson.Serialize(doc), DocumentJson.Serialize(back));
            var t = Solve(r, back);
            Assert.Equal(12500, Val(t, route, "s")!.Value, 6);
            Assert.Equal(15 / 3.6, Val(t, route, "v")!.Value, 9);
        }
    }

    /// <summary>Stations, bridges, tunnels and contour lines.</summary>
    public class StructureTests
    {
        private static readonly SnapOptions Options = new() { Tolerance = 3, Grid = null, Alignment = false, Page = false };

        [Fact]
        public void StationDocksOntoARailLineWithItsSymbol()
        {
            var r = Fx.Reg();
            var rail = Fx.Line(r, "map.rail", new Vec2(0, 50), new Vec2(200, 50));
            var station = Fx.Item(r, "map.station", new Vec2(80, 48));
            var (doc, page) = Fx.Doc(rail, station);
            var ctx = new RenderContext(doc);
            var moving = r.PageAnchors(station, ctx).ToList();
            var loc = moving.Single(a => a.Id == "loc");
            Assert.Equal(AnchorRole.Port, loc.Role);
            var res = Snapper.SnapMove(moving, new Vec2(1, 1), Snapper.Targets(r, doc, page, new[] { station.Id }, ctx, Options), Options);
            Assert.Equal("rail", res.Kind);
            Assert.Equal(50, loc.P.Y + res.Delta.Y, 6);
        }

        [Theory]
        [InlineData("map.station", "Railway station", "Bahnhof")]
        [InlineData("map.station.interchange", "Interchange", "Umsteigebahnhof")]
        [InlineData("map.station.tram", "Tram stop", "Straßenbahnhaltestelle")]
        [InlineData("map.station.bus", "Bus stop", "Bushaltestelle")]
        public void StationKindsDrawTheirSymbolAndDefaultName(string item, string en, string de)
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, item, Vec2.Zero);
            var prims = r.TypeOf(o).Render(o, Fx.Ctx());
            Fx.AssertInsideBox(prims, o.Size, item);
            Assert.Contains(prims, p => p.Tag == MapTags.Station);
            Assert.Contains(prims, p => p is TextPrim tp && tp.Text == en);
            Assert.Contains(r.TypeOf(o).Render(o, Fx.Ctx(Lang.De)), p => p is TextPrim tp && tp.Text == de);
            var named = o with { Text = "Hbf" };
            Assert.Contains(r.TypeOf(named).Render(named, Fx.Ctx()), p => p is TextPrim tp && tp.Text == "Hbf");
            var nameless = o.WithParam("name", false);
            Assert.DoesNotContain(r.TypeOf(nameless).Render(nameless, Fx.Ctx()), p => p is TextPrim tp && tp.Tag == MapTags.Label);
        }

        [Fact]
        public void BridgeFollowsTheRoadBeneathAndTakesItsWidth()
        {
            var r = Fx.Reg();
            var road = Fx.Line(r, "map.road.motorway", new Vec2(0, 50), new Vec2(100, 50), new Vec2(100, 150));
            var bridge = Fx.Line(r, "map.bridge", new Vec2(80, 50.5), new Vec2(100.4, 70));
            var (doc, _) = Fx.Doc(road, bridge);
            var s = RoadSectionType.Resolve(doc.Get(bridge.Id)!, doc, doc.ResolvePoints(bridge));
            Assert.Equal(road.Id, s.Road!.Id);
            Assert.Equal(4.2, s.Width, 6);
            // Around the corner along the centre line: (80,50) → (100,50) → (100,70).
            Assert.Equal(new Vec2(80, 50), s.Line[0]);
            Assert.Equal(new Vec2(100, 50), s.Line[1]);
            Assert.Equal(new Vec2(100, 70), s.Line[2]);
            var prims = r.Render(bridge, new RenderContext(doc));
            var deck = prims.Single(p => p.Tag == MapTags.Band);
            Assert.Equal("#f7b267", ((PathPrim)deck).Style.Fill!.Color);
            var parapets = Prim.Bounds(new[] { prims.Single(p => p.Tag == MapTags.Parapet) })!.Value;
            Assert.True(parapets.Y < 50 - 2.1 - 0.4, "parapet outside the road edge");
            // The road reports the bridge as a dependent; the bridge reads the road.
            Assert.Contains(bridge.Id, r.TypeOf(road).Dependents(road, new RenderContext(doc)));
            Assert.True(r.TypeOf(bridge).DependsOnNeighbours);
        }

        [Fact]
        public void BridgeAndTunnelWithoutARoadUseTheirOwnPoints()
        {
            var r = Fx.Reg();
            foreach (var id in new[] { "map.bridge", "map.tunnel" })
            {
                var o = Fx.Line(r, id, new Vec2(0, 0), new Vec2(20, 0));
                var (doc, _) = Fx.Doc(o);
                var s = RoadSectionType.Resolve(o, doc, o.Points!);
                Assert.Null(s.Road);
                Assert.Equal(RoadSectionType.DefaultWidth, s.Width, 9);
                Assert.Equal(2, s.Line.Count);
                var wide = o.WithParam("width", 6.0);
                Assert.Equal(6, RoadSectionType.Resolve(wide, doc, o.Points!).Width, 9);
                Fx.AssertFinite(r.Render(o, new RenderContext(doc)), id);
            }
        }

        [Fact]
        public void TunnelCoversTheRoadAndHasTwoPortals()
        {
            var r = Fx.Reg();
            var road = Fx.Line(r, "map.road.main", new Vec2(0, 50), new Vec2(200, 50));
            var tunnel = Fx.Line(r, "map.tunnel", new Vec2(60, 50), new Vec2(120, 50)) with { Text = "Kehrtunnel" };
            var (doc, _) = Fx.Doc(road, tunnel);
            var prims = r.Render(tunnel, new RenderContext(doc));
            var mask = prims.Single(p => p.Tag == MapTags.Mask);
            var mb = Prim.Bounds(new[] { mask })!.Value;
            Assert.True(mb.H >= 3.0 && mb.X <= 60 + 1e-6 && mb.Right >= 120 - 1e-6, "mask covers the road section");
            var portals = (PathPrim)prims.Single(p => p.Tag == MapTags.Portal);
            Assert.Equal(2, portals.Segments.Count(s => s.Op == SegOp.M));
            var edges = (PathPrim)prims.Single(p => p.Tag == MapTags.Edge);
            Assert.True(edges.Segments.Count(s => s.Op == SegOp.M) > 10, "dashed edges");
            Assert.Contains(prims, p => p is TextPrim tp && tp.Text == "Kehrtunnel");
            // Monochrome and style overrides keep the land-coloured cover.
            Assert.Same(mask, r.TypeOf(tunnel).ApplyOverride(mask, new Style { Stroke = "#ff0000", Fill = Fill.Solid("#ff0000") }));
        }

        [Fact]
        public void ContourHasAGapWithItsElevation()
        {
            var r = Fx.Reg();
            var c = Fx.Item(r, "map.contour", new Vec2(100, 100)).WithParam("elevation", 250.0);
            var (doc, _) = Fx.Doc(c);
            var prims = r.Render(c, new RenderContext(doc));
            var label = prims.OfType<TextPrim>().Single();
            Assert.Equal("250", label.Text);
            var line = (PathPrim)prims.Single(p => p.Tag == MapTags.Contour);
            Assert.Equal(2, line.Segments.Count(s => s.Op == SegOp.M));
            foreach (var p in line.Segments.Where(s => s.Op != SegOp.Z)) Assert.True(p.P.DistanceTo(label.Position) >= 2.0 - 1e-6, "line keeps clear of the label");
            Assert.Equal(0.25, line.Style.StrokeWidth!.Value, 9);
            var index = c.WithParam("index", true);
            Assert.Equal(0.5, ((PathPrim)r.Render(index, new RenderContext(Fx.Doc(index).Doc)).Single(p => p.Tag == MapTags.Contour)).Style.StrokeWidth!.Value, 9);
            var unlabelled = c.WithParam("label", false);
            Assert.DoesNotContain(r.Render(unlabelled, new RenderContext(Fx.Doc(unlabelled).Doc)), p => p is TextPrim);
        }

        [Fact]
        public void ClosedContourIsAClosedOutline()
        {
            var r = Fx.Reg();
            var hill = Fx.Item(r, "map.contour.hill", new Vec2(100, 100));
            Assert.True(r.TypeOf(hill).IsClosed(hill));
            var line = ContourType.Line(hill, hill.Points!);
            Assert.Equal(line[0], line[line.Count - 1]);
            Assert.False(r.TypeOf(hill).IsClosed(Fx.Item(r, "map.contour", Vec2.Zero)));
        }

        [Fact]
        public void ContourElevationInputAcceptsMetres()
        {
            var r = Fx.Reg();
            var c = Fx.Item(r, "map.contour", Vec2.Zero);
            var input = r.TypeOf(c).Actions(c, Lang.De).Single(a => a.Id == "map.contour.elevation").Input!;
            Assert.Equal(1250.5, input.Apply(c, "1250,5 m").Num("elevation", 0), 9);
            Assert.Throws<FormatException>(() => input.Apply(c, "hoch"));
        }

        [Fact]
        public void LegendListsTheNewElements()
        {
            var r = Fx.Reg();
            var road = Fx.Line(r, "map.road.main", new Vec2(0, 50), new Vec2(200, 50));
            var legend = Fx.Item(r, "map.legend", new Vec2(150, 150));
            var (doc, _) = Fx.Doc(road, Fx.Line(r, "map.bridge", new Vec2(20, 50), new Vec2(40, 50)), Fx.Line(r, "map.tunnel", new Vec2(60, 50), new Vec2(90, 50)),
                Fx.Item(r, "map.station.bus", new Vec2(120, 50)), Fx.Item(r, "map.contour", new Vec2(100, 120)), legend);
            var keys = Legend.Entries(legend, new RenderContext(doc)).Select(e => e.Key).ToList();
            Assert.Contains("bridge", keys);
            Assert.Contains("tunnel", keys);
            Assert.Contains("station.bus", keys);
            Assert.Contains("contour", keys);
            foreach (var k in new[] { "bridge", "tunnel", "station.bus", "contour" })
                Assert.NotEmpty(Legend.Sample(k, new Rect(0, 0, 11, 5), new RenderContext(doc)));
            Assert.Equal("Bushaltestelle", Legend.KeyLabel("station.bus", Lang.De));
        }

        [Fact]
        public void StationsOnARouteAppearInTheStopExport()
        {
            var r = Fx.Reg();
            var route = Fx.Line(r, "map.route", new Vec2(0, 0), new Vec2(50, 0), new Vec2(100, 0));
            var station = Fx.Item(r, "map.station", new Vec2(50, 0)) with { Text = "Hauptbahnhof" };
            // Move the station so its symbol (port "loc") sits on the middle route point.
            var loc = r.PageAnchors(station, Fx.Ctx()).Single(a => a.Id == "loc").P;
            station = station with { Transform = station.Transform with { X = station.Transform.X + 50 - loc.X, Y = station.Transform.Y - loc.Y } };
            var (doc, _) = Fx.Doc(route, station);
            var csv = new MapsModule().Exporters.Single(e => e.Id == "map.route.stops").Export(r, doc, new[] { route }, Lang.En);
            Assert.Contains("Hauptbahnhof", csv);
            Assert.Contains("Station: Railway station", csv);
        }

        [Fact]
        public void WriteStructuresGallery()
        {
            var r = Fx.Reg();
            SketchObject L(string id, params Vec2[] pts) => Fx.Line(r, id, pts);
            var frame = Fx.Item(r, "map.frame", new Vec2(110, 70)) with { Size = new Size2(210, 130) };
            var river = L("map.river", new Vec2(10, 20), new Vec2(70, 60), new Vec2(120, 70), new Vec2(210, 60)) with { Text = "River" };
            var road = L("map.road.main", new Vec2(10, 100), new Vec2(80, 95), new Vec2(140, 40), new Vec2(210, 30)) with { Text = "B17" };
            var rail = L("map.rail", new Vec2(10, 125), new Vec2(210, 120));
            var hill1 = Fx.Item(r, "map.contour.hill", new Vec2(170, 95)).WithParam("elevation", 300.0);
            var hill2 = L("map.contour", new Vec2(130, 110), new Vec2(150, 80), new Vec2(185, 72), new Vec2(212, 90)).WithParam("elevation", 250.0).WithParam("index", true);
            // The river is drawn over the road; the bridge puts the road back on top where it crosses.
            var (doc0, _) = Fx.Doc(frame, road, river, rail, hill1, hill2);
            var bridge = L("map.bridge", new Vec2(103, 73.92), new Vec2(116, 62));
            var tunnel = L("map.tunnel", new Vec2(150, 36), new Vec2(190, 31)) with { Text = "Tunnel" };
            var s1 = Fx.Item(r, "map.station", new Vec2(70, 123)) with { Text = "Neustadt" };
            var s2 = Fx.Item(r, "map.station.interchange", new Vec2(150, 121)) with { Text = "Hbf" };
            var bus = Fx.Item(r, "map.station.bus", new Vec2(60, 104));
            var route = L("map.route", new Vec2(20, 99.4), new Vec2(80, 95), new Vec2(140, 40)).WithParam(ValueLabels.ShowParam, true);
            var bar = Fx.Item(r, "map.scalebar", new Vec2(190, 128)) with { Size = new Size2(30, 7) };
            var legend = Fx.Item(r, "map.legend", new Vec2(35, 40)) with { Size = new Size2(46, 52) };
            var doc = Edits.Add(doc0, doc0.Pages[0].Id, bridge, tunnel, s1, s2, bus, route, bar, legend);
            var dir = Path.Combine(AppContext.BaseDirectory, "gallery-maps");
            Directory.CreateDirectory(dir);
            var html = new StringBuilder("<!DOCTYPE html><html><head><meta charset=\"utf-8\"><title>Structures</title></head><body>");
            foreach (var (lang, mono) in new[] { (Lang.En, false), (Lang.De, false), (Lang.En, true) })
                html.Append(SvgRenderer.RenderPage(r, doc, doc.Pages[0], new SvgOptions { Monochrome = mono }, lang)).Append("<br/>");
            html.Append("</body></html>");
            File.WriteAllText(Path.Combine(dir, "structures.html"), html.ToString(), Encoding.UTF8);
            File.WriteAllText(Path.Combine(dir, "structures.svg"), SvgRenderer.RenderPage(r, doc, doc.Pages[0]), Encoding.UTF8);
            Assert.True(File.Exists(Path.Combine(dir, "structures.svg")));
        }
    }
}
