using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Maps.Tests
{
    /// <summary>Sample route map used by the persistence, export, legend tests and the gallery.</summary>
    internal static class SampleMap
    {
        public static (SketchDocument Doc, Page Page) Build(Registry r, double scaleBarWidth = 40)
        {
            SketchObject I(string id, double x, double y) => Fx.Item(r, id, new Vec2(x, y));
            SketchObject L(string id, params Vec2[] pts) => Fx.Line(r, id, pts);
            var frame = I("map.frame.grid", 169, 95) with { Size = new Size2(320, 176) };
            var lake = L("map.region.water", new Vec2(185, 152), new Vec2(215, 147), new Vec2(240, 156), new Vec2(232, 175), new Vec2(200, 177), new Vec2(184, 168)) with { Text = "Lake" };
            var forest = L("map.region.forest", new Vec2(215, 20), new Vec2(265, 15), new Vec2(285, 40), new Vec2(250, 55), new Vec2(220, 45));
            var city = L("map.region.urban", new Vec2(120, 60), new Vec2(170, 58), new Vec2(175, 95), new Vec2(125, 98));
            var river = L("map.river", new Vec2(15, 60), new Vec2(60, 80), new Vec2(100, 105), new Vec2(160, 130), new Vec2(230, 140), new Vec2(325, 125)) with { Text = "River" };
            var motorway = L("map.road.motorway", new Vec2(15, 40), new Vec2(120, 45), new Vec2(240, 75), new Vec2(325, 80)) with { Text = "A8" };
            var main = L("map.road.main", new Vec2(150, 15), new Vec2(148, 80), new Vec2(140, 175)) with { Text = "B17" };
            var side = L("map.road.side", new Vec2(148, 110), new Vec2(260, 115), new Vec2(300, 160));
            var rail = L("map.rail", new Vec2(15, 95), new Vec2(120, 92), new Vec2(325, 100));
            var start = I("map.marker.start", 60, 37.0);
            var finish = I("map.marker.finish", 280, 130.5);
            var stop1 = I("map.stop", 148, 80);
            var stop2 = I("map.stop", 230, 112.8).WithParam("number", 2);
            var route = L("map.route", new Vec2(60, 42.5), new Vec2(120, 45), new Vec2(148, 52), new Vec2(148, 80), new Vec2(149, 110), new Vec2(230, 112.8), new Vec2(260, 115), new Vec2(280, 136));
            var fuel = I("map.poi.fuel", 200, 62);
            var hospital = I("map.poi.hospital", 160, 72);
            var station = I("map.poi.station.pin", 120, 86.5);
            var airport = I("map.poi.airport", 300, 30);
            var cityLabel = I("map.place.city", 120, 64) with { Text = "Neustadt", Size = new Size2(30, 7) };
            var village = I("map.place.village", 285, 150) with { Text = "Altdorf" };
            var callout = I("map.callout", 205, 98);
            var bar = I("map.scalebar", 290, 172) with { Size = new Size2(scaleBarWidth, 8) };
            var north = I("map.north", 314, 22);
            var legend = I("map.legend", 45, 145) with { Size = new Size2(58, 64) };
            var doc = SketchDocument.Create(title: "Route map");
            doc = Edits.Add(doc, doc.Pages[0].Id, frame, lake, forest, city, river, rail, motorway, main, side, route,
                start, finish, stop1, stop2, fuel, hospital, station, airport, cityLabel, village, callout, bar, north, legend);
            // Route end docked onto the finish marker tip (follows the marker).
            doc = Edits.AttachPoint(doc, route.Id, route.Points!.Count - 1, finish.Id, new Vec2(280, 136), "loc");
            return (doc, doc.Pages[0]);
        }
    }

    public class PersistenceTests
    {
        [Fact]
        public void JsonRoundTripKeepsEverything()
        {
            var r = Fx.Reg();
            var (doc, page) = SampleMap.Build(r);
            var json = DocumentJson.Serialize(doc, indented: true);
            var back = DocumentJson.Deserialize(json);
            Assert.Equal(DocumentJson.Serialize(doc), DocumentJson.Serialize(back));
            var route = back.Objects.Values.Single(o => o.Type == "map.route");
            Assert.Equal(8, route.Points!.Count);
            Assert.Equal("loc", route.Attach![7]!.AnchorId);
            Assert.Equal("A8", back.Objects.Values.Single(o => o.Type == "map.road" && o.Str("kind", "") == "motorway").Text);
            // Rendering after the round trip is identical.
            Assert.Equal(SvgRenderer.RenderPage(r, doc, page), SvgRenderer.RenderPage(r, back, back.Pages[0]));
            var compact = DocumentJson.FromCompact(DocumentJson.ToCompact(doc));
            Assert.Equal(DocumentJson.Serialize(doc), DocumentJson.Serialize(compact));
        }

        [Fact]
        public void UnknownParametersAndMissingParametersAreHarmless()
        {
            var r = Fx.Reg();
            foreach (var t in r.Types)
            {
                var o = t.Geometry == GeometryKind.Points
                    ? new SketchObject { Id = "x", Type = t.Id, Points = t.DefaultPoints.ToList(), Params = new Dictionary<string, ParamValue> { ["kind"] = "nonsense", ["style"] = 3, ["future"] = true } }
                    : new SketchObject { Id = "x", Type = t.Id, Size = t.DefaultSize, View = "nonsense", Params = new Dictionary<string, ParamValue> { ["kind"] = "nonsense", ["style"] = 3, ["future"] = true } };
                var prims = t.Render(o, Fx.Ctx());
                Assert.NotEmpty(prims);
                if (t.Geometry == GeometryKind.Box) Fx.AssertInsideBox(prims, o.Size, t.Id);
            }
        }
    }

    public class MonochromeTests
    {
        [Fact]
        public void SolidFillsAreClearlyDarkOrLight()
        {
            // Monochrome output maps fills below luminance 0.35 to black and the rest to white: no fill may sit near the
            // threshold, otherwise contrast with white glyphs or black lines would depend on rounding.
            var r = Fx.Reg();
            var (doc, page) = SampleMap.Build(r);
            var prims = new List<Primitive>();
            foreach (var ro in r.RenderPage(doc, page, new RenderContext(doc))) prims.AddRange(Prim.Flatten(ro.Primitives));
            foreach (var item in r.Library)
            {
                var o = r.Create(item, new Vec2(100, 100));
                foreach (var v in r.TypeOf(o).Views) prims.AddRange(Prim.Flatten(r.Render(Fx.InView(r.TypeOf(o), o, v.Id), Fx.Ctx())));
            }
            foreach (var p in prims)
            {
                var style = p switch { LinePrim l => l.Style, PathPrim pa => pa.Style, EllipsePrim e => e.Style, RectPrim re => re.Style, _ => null };
                if (style?.Fill is { Kind: FillKind.Solid, Color: { } c })
                {
                    var lum = SvgRenderer.Luminance(c);
                    Assert.True(lum < 0.3 || lum > 0.45, $"fill {c} (luminance {lum:0.00}) of '{p.Tag}' is ambiguous in monochrome");
                }
            }
        }

        [Fact]
        public void BadgeGlyphsStayVisibleInMonochrome()
        {
            var r = Fx.Reg();
            foreach (var item in r.Library.Where(i => i.TypeId is "map.poi" or "map.marker" or "map.stop"))
            {
                var o = r.Create(item, new Vec2(50, 50));
                var (doc, page) = Fx.Doc(o);
                var svg = SvgRenderer.RenderPage(r, doc, page, new SvgOptions { Monochrome = true, Background = null });
                var colours = Regex.Matches(svg, "(fill|stroke)=\"(#[0-9a-f]{6})\"").Cast<Match>().Select(m => m.Groups[2].Value).Distinct().ToList();
                Assert.All(colours, c => Assert.True(c is "#000000" or "#ffffff", item.Id + ": " + c));
                // A dark badge turns black with a white glyph; a light one (via marker) turns white with a black glyph.
                Assert.Contains("#000000", colours);
                Assert.Contains("#ffffff", colours);
            }
        }

        [Fact]
        public void RegionTintBecomesHatchInMonochromeDocuments()
        {
            var r = Fx.Reg();
            var region = Fx.Item(r, "map.region.park", new Vec2(50, 50));
            var (doc, _) = Fx.Doc(region);
            var colour = (PathPrim)r.Render(region, new RenderContext(doc)).Single(p => p.Tag == MapTags.Region);
            Assert.Equal(FillKind.Solid, colour.Style.Fill!.Kind);
            var mono = doc with { Settings = doc.Settings with { Monochrome = true } };
            var hatched = (PathPrim)r.Render(region, new RenderContext(mono)).Single(p => p.Tag == MapTags.Region);
            Assert.Equal(FillKind.Hatch, hatched.Style.Fill!.Kind);
            var forest = Fx.Item(r, "map.region.forest", new Vec2(50, 50));
            var dots = (PathPrim)r.Render(forest, new RenderContext(SketchDocument.Create(), Lang.En, monochrome: true)).Single(p => p.Tag == MapTags.Region);
            Assert.Equal(FillKind.Dots, dots.Style.Fill!.Kind);
        }

        [Fact]
        public void LineFeaturesStayDistinguishableWithoutColour()
        {
            // Signature of a line feature without colours: widths, dashes and the number of primitives.
            var r = Fx.Reg();
            string Signature(string id)
            {
                var o = Fx.Line(r, id, new Vec2(0, 50), new Vec2(100, 50));
                return string.Join("|", r.Render(o, Fx.Ctx()).Select(p => p switch
                {
                    PathPrim pa => $"{pa.Tag}:{pa.Style.EffectiveWidth:0.00}:{pa.Style.EffectiveDash}:{(pa.Style.Fill?.Kind ?? FillKind.None)}:{Math.Round(Prim.Bounds(new[] { p })!.Value.H, 1)}",
                    _ => p.GetType().Name,
                }));
            }
            var ids = new[] { "map.road.motorway", "map.road.main", "map.road.side", "map.road.path", "map.rail", "map.rail.tram", "map.river", "map.river.stream", "map.river.canal", "map.route", "map.route.dashed" };
            var signatures = ids.Select(Signature).ToList();
            Assert.Equal(ids.Length, signatures.Distinct().Count());
        }
    }
}
