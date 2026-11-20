using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Maps.Tests
{
    public class RenderTests
    {
        [Theory]
        [InlineData(Lang.En)]
        [InlineData(Lang.De)]
        public void EveryLibraryItemRendersInEveryViewInsideTheBox(Lang lang)
        {
            var r = Fx.Reg();
            var count = 0;
            foreach (var (item, type, view) in Fx.BoxItemViews(r))
            {
                var o = Fx.InView(type, r.Create(item, Vec2.Zero), view);
                Fx.AssertInsideBox(type.Render(o, Fx.Ctx(lang)), o.Size, $"{item.Id}/{view}");
                count++;
            }
            Assert.True(count > 60, "expected many item/view combinations, got " + count);
        }

        [Theory]
        [InlineData(2.5, 0.6)]
        [InlineData(0.4, 1.8)]
        [InlineData(3, 3)]
        [InlineData(0.3, 0.3)]
        public void FreeNonUniformScalingKeepsPrimitivesInsideTheBox(double sx, double sy)
        {
            var r = Fx.Reg();
            foreach (var (item, type, view) in Fx.BoxItemViews(r))
            {
                var o = Fx.InView(type, r.Create(item, Vec2.Zero), view);
                o = o with { Size = new Size2(o.Size.W * sx, o.Size.H * sy) };
                Fx.AssertInsideBox(type.Render(o, Fx.Ctx()), o.Size, $"{item.Id}/{view} scaled {sx}x{sy}");
            }
        }

        [Fact]
        public void LongTextsStayInsideTheBox()
        {
            var r = Fx.Reg();
            foreach (var id in new[] { "map.place.city", "map.marker.a", "map.stop", "map.callout", "map.legend" })
            {
                var o = Fx.Item(r, id, Vec2.Zero) with { Text = "Llanfairpwllgwyngyll-gogerychwyrndrobwll\nsecond line" };
                Fx.AssertInsideBox(r.TypeOf(o).Render(o, Fx.Ctx()), o.Size, id);
            }
        }

        [Fact]
        public void PointItemsRenderNearTheirPoints()
        {
            var r = Fx.Reg();
            foreach (var item in r.Library.Where(i => r.Type(i.TypeId)!.Geometry == GeometryKind.Points))
            {
                var o = r.Create(item, new Vec2(120, 80)) with { Text = "Name" };
                var (doc, _) = Fx.Doc(o);
                var prims = r.Render(o, new RenderContext(doc));
                Assert.NotEmpty(prims);
                Fx.AssertFinite(prims, item.Id);
                var b = Prim.Bounds(prims)!.Value;
                var pb = Rect.FromPoints(o.Points!).Inflate(8);
                Assert.True(pb.Contains(b), $"{item.Id}: bounds {b} far from points {pb}");
                foreach (var v in r.TypeOf(o).Views) Assert.NotEmpty(r.Render(o with { View = v.Id }, new RenderContext(doc)));
            }
        }

        [Fact]
        public void AllContextActionsApplyAndKeepRenderingInsideTheBox()
        {
            var r = Fx.Reg();
            foreach (var item in r.Library)
            {
                var type = r.Type(item.TypeId)!;
                var o = r.Create(item, new Vec2(100, 100));
                foreach (var a in type.Actions(o, Fx.Ctx()))
                {
                    if (a.Input is not null) continue;
                    var x = a.Apply(o);
                    Assert.Equal(o.Id, x.Id);
                    var (doc, _) = Fx.Doc(x);
                    var prims = type.Render(x, new RenderContext(doc));
                    if (type.Geometry == GeometryKind.Box) Fx.AssertInsideBox(prims, x.Size, $"{item.Id}/{a.Id}");
                    else { Assert.NotEmpty(prims); Fx.AssertFinite(prims, $"{item.Id}/{a.Id}"); }
                }
            }
        }

        [Fact]
        public void ScalingChangesSizeNotParameters()
        {
            var r = Fx.Reg();
            var bar = Fx.Item(r, "map.scalebar", new Vec2(100, 100));
            var (doc, _) = Fx.Doc(bar);
            doc = Edits.Scale(doc, new[] { bar.Id }, new Vec2(100, 100), 2, 0.5);
            var scaled = doc.Get(bar.Id)!;
            Assert.Equal(1, scaled.Num("length", 0));
            Assert.Equal("km", scaled.Str("unit", ""));
            Assert.Equal(bar.Size.W * 2, scaled.Size.W, 6);
        }

        [Fact]
        public void RoadKindsDifferInWidthAndMotorwayHasADivider()
        {
            var r = Fx.Reg();
            double BandHeight(string id)
            {
                var o = Fx.Line(r, id, new Vec2(0, 50), new Vec2(100, 50));
                var band = r.Render(o, Fx.Ctx()).Single(p => p.Tag == MapTags.Band);
                return Prim.Bounds(new[] { band })!.Value.H;
            }
            Assert.True(BandHeight("map.road.motorway") > BandHeight("map.road.main"));
            Assert.True(BandHeight("map.road.main") > BandHeight("map.road.side"));
            var motorway = r.Render(Fx.Line(r, "map.road.motorway", new Vec2(0, 50), new Vec2(100, 50)), Fx.Ctx());
            Assert.Contains(motorway, p => p.Tag == MapTags.Divider);
            var path = r.Render(Fx.Line(r, "map.road.path", new Vec2(0, 50), new Vec2(100, 50)), Fx.Ctx());
            Assert.DoesNotContain(path, p => p.Tag == MapTags.Band);
            Assert.Equal(DashStyle.Dash, ((PathPrim)path.Single(p => p.Tag == MapTags.Track)).Style.Dash);
        }

        [Fact]
        public void CrossingRoadsInterruptEachOthersEdges()
        {
            var r = Fx.Reg();
            var a = Fx.Line(r, "map.road.main", new Vec2(0, 50), new Vec2(100, 50));
            var b = Fx.Line(r, "map.road.side", new Vec2(50, 0), new Vec2(50, 100));
            var (doc, _) = Fx.Doc(a, b);
            var ctx = new RenderContext(doc);
            var edgesA = (PathPrim)r.Render(a, ctx).Single(p => p.Tag == MapTags.Edge);
            // Two sides, each cut once by the side road: four pieces plus the two end lines.
            Assert.Equal(6, edgesA.Segments.Count(s => s.Op == SegOp.M));
            // No edge point of the main road lies inside the side road's band (half width 1 mm).
            foreach (var s in edgesA.Segments.Where(s => s.Op != SegOp.Z))
                Assert.False(Math.Abs(s.P.X - 50) < 1 - 0.02 && s.P.Y > 48 && s.P.Y < 52, "edge inside the crossing: " + s.P);
            // The pieces end on the side road's edges.
            Assert.Contains(edgesA.Segments, s => Math.Abs(s.P.X - 49) < 0.05);
            Assert.Contains(edgesA.Segments, s => Math.Abs(s.P.X - 51) < 0.05);
            // Each road reports the other as dependent (redrawn together).
            Assert.Contains(b.Id, r.TypeOf(a).Dependents(a, ctx));
            Assert.True(r.TypeOf(a).DependsOnNeighbours);
        }

        [Fact]
        public void RoadEndingOnAnotherRoadHasNoEndLineThere()
        {
            var r = Fx.Reg();
            var main = Fx.Line(r, "map.road.main", new Vec2(0, 50), new Vec2(100, 50));
            var side = Fx.Line(r, "map.road.side", new Vec2(50, 50), new Vec2(50, 100));
            var (doc, _) = Fx.Doc(main, side);
            var edges = (PathPrim)r.Render(side, new RenderContext(doc)).Single(p => p.Tag == MapTags.Edge);
            // Two edge pieces (from the main road's edge downwards) and one end line at the free end.
            Assert.Equal(3, edges.Segments.Count(s => s.Op == SegOp.M));
            Assert.All(edges.Segments.Where(s => s.Op != SegOp.Z), s => Assert.True(s.P.Y >= 51.5 - 0.05, "edge reaches into the main road: " + s.P));
        }

        [Fact]
        public void BridgeKeepsItsEdges()
        {
            var r = Fx.Reg();
            var a = Fx.Line(r, "map.road.main", new Vec2(0, 50), new Vec2(100, 50)).WithParam("bridge", true);
            var b = Fx.Line(r, "map.road.side", new Vec2(50, 0), new Vec2(50, 100));
            var (doc, _) = Fx.Doc(a, b);
            var ctx = new RenderContext(doc);
            Assert.Equal(4, ((PathPrim)r.Render(a, ctx).Single(p => p.Tag == MapTags.Edge)).Segments.Count(s => s.Op == SegOp.M));
            Assert.Equal(4, ((PathPrim)r.Render(b, ctx).Single(p => p.Tag == MapTags.Edge)).Segments.Count(s => s.Op == SegOp.M));
        }

        [Fact]
        public void RouteHasHaloArrowsAndWaypoints()
        {
            var r = Fx.Reg();
            var route = Fx.Line(r, "map.route", new Vec2(0, 0), new Vec2(60, 0), new Vec2(60, 60), new Vec2(120, 60));
            var prims = r.Render(route, Fx.Ctx());
            var halo = prims.First();
            Assert.Equal(MapTags.Halo, halo.Tag);
            var main = (PathPrim)prims.Single(p => p.Tag == MapTags.Route);
            Assert.Equal(ArrowHead.Triangle, main.Style.ArrowEnd);
            Assert.Equal(2, prims.Count(p => p.Tag == MapTags.Waypoint));
            Assert.Contains(prims, p => p.Tag == MapTags.Inlay);
            var dashed = r.Render(route.WithParam("style", "dashed"), Fx.Ctx());
            Assert.Equal(DashStyle.Dash, ((PathPrim)dashed.Single(p => p.Tag == MapTags.Route)).Style.Dash);
            Assert.Contains(dashed, p => p.Tag == MapTags.Arrow);
            var none = r.Render(route.WithParam("arrows", "none").WithParam("waypoints", false), Fx.Ctx());
            Assert.Null(((PathPrim)none.Single(p => p.Tag == MapTags.Route)).Style.ArrowEnd);
            Assert.DoesNotContain(none, p => p.Tag is MapTags.Waypoint or MapTags.Inlay or MapTags.Arrow);
        }

        [Fact]
        public void StyleOverrideRecoloursTheRouteButKeepsHaloAndChevronsWhite()
        {
            var r = Fx.Reg();
            var route = Fx.Line(r, "map.route", new Vec2(0, 0), new Vec2(100, 0)) with { Style = new Style { Stroke = "#e53935" } };
            var prims = r.Render(route, Fx.Ctx());
            Assert.Equal("#e53935", ((PathPrim)prims.Single(p => p.Tag == MapTags.Route)).Style.Stroke);
            Assert.Equal("#ffffff", ((PathPrim)prims.Single(p => p.Tag == MapTags.Halo)).Style.Stroke);
            Assert.Equal("#ffffff", ((PathPrim)prims.Single(p => p.Tag == MapTags.Inlay)).Style.Fill!.Color);
        }

        [Fact]
        public void TextOverridesApplyToLabels()
        {
            var r = Fx.Reg();
            var place = Fx.Item(r, "map.place.city", new Vec2(50, 50)) with { Text = "Berlin", Style = new Style { TextColor = "#c62828", Bold = false, FontScale = 0.8 } };
            var text = r.Render(place, Fx.Ctx()).OfType<TextPrim>().Single();
            Assert.Equal("#c62828", text.TextStyle.Color);
            Assert.False(text.TextStyle.Bold);
        }

        [Fact]
        public void CompassUsesGermanEastLetter()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "map.compass", Vec2.Zero);
            Assert.Contains(r.TypeOf(o).Render(o, Fx.Ctx(Lang.De)).OfType<TextPrim>(), t => t.Text == "O");
            Assert.Contains(r.TypeOf(o).Render(o, Fx.Ctx(Lang.En)).OfType<TextPrim>(), t => t.Text == "E");
        }

        [Fact]
        public void PlaceLabelDefaultsToTheBilingualRankName()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "map.place.village", Vec2.Zero);
            Assert.Contains(r.TypeOf(o).Render(o, Fx.Ctx(Lang.De)).OfType<TextPrim>(), t => t.Text == "Dorf");
            Assert.Contains(r.TypeOf(o).Render(o, Fx.Ctx(Lang.En)).OfType<TextPrim>(), t => t.Text == "Village");
        }

        [Theory]
        [InlineData(1, 4, "km", new[] { "0", "0.25", "0.5", "0.75", "1 km" })]
        [InlineData(10, 5, "mi", new[] { "0", "2", "4", "6", "8", "10 mi" })]
        public void ScaleBarLabelsShowValuesAndUnit(double length, int segments, string unit, string[] expected)
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "map.scalebar", Vec2.Zero).WithParam("length", length).WithParam("segments", segments).WithParam("unit", unit) with { Size = new Size2(80, 8) };
            var texts = r.TypeOf(o).Render(o, Fx.Ctx()).OfType<TextPrim>().Select(t => t.Text).ToList();
            Assert.Equal(expected, texts);
            var de = r.TypeOf(o).Render(o, Fx.Ctx(Lang.De)).OfType<TextPrim>().Select(t => t.Text).ToList();
            Assert.Equal(expected.Select(e => e.Replace('.', ',')), de);
        }

        [Fact]
        public void NarrowScaleBarThinsOutItsLabels()
        {
            var labels = ScaleBarLabels(n: 10, width: 25);
            Assert.Equal("0", labels.First());
            Assert.EndsWith("km", labels.Last());
            Assert.True(labels.Count < 11);
        }

        private static List<string> ScaleBarLabels(int n, double width)
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "map.scalebar", Vec2.Zero).WithParam("segments", n).WithParam("length", 100) with { Size = new Size2(width, 8) };
            return r.TypeOf(o).Render(o, Fx.Ctx()).OfType<TextPrim>().Select(t => t.Text).ToList();
        }
    }
}
