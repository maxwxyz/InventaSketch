using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Maps.Tests
{
    public class SnappingTests
    {
        private static readonly SnapOptions Options = new() { Tolerance = 3, Grid = null, Alignment = false, Page = false };

        [Fact]
        public void RoadCentreLineIsARail()
        {
            var r = Fx.Reg();
            var road = Fx.Line(r, "map.road.main", new Vec2(0, 50), new Vec2(100, 50), new Vec2(100, 120));
            var (doc, _) = Fx.Doc(road);
            var rails = r.PageAnchors(road, new RenderContext(doc)).Where(a => a.Role == AnchorRole.Rail).ToList();
            Assert.Equal(2, rails.Count);
            Assert.Equal(new Vec2(0, 50), rails[0].P);
            Assert.Equal(new Vec2(100, 120), rails[1].Q);
        }

        [Fact]
        public void MarkerTipDocksOntoARoadRail()
        {
            var r = Fx.Reg();
            var road = Fx.Line(r, "map.road.main", new Vec2(0, 50), new Vec2(200, 50));
            var marker = Fx.Item(r, "map.marker.start", new Vec2(80, 40));
            var (doc, page) = Fx.Doc(road, marker);
            var ctx = new RenderContext(doc);
            var moving = r.PageAnchors(marker, ctx).ToList();
            var tip = moving.Single(a => a.Id == "loc");
            Assert.Equal(AnchorRole.Port, tip.Role);
            Assert.Equal(45.5, tip.P.Y, 6); // pin tip at the bottom centre of the 8 x 11 box
            var targets = Snapper.Targets(r, doc, page, new[] { marker.Id }, ctx, Options);
            var res = Snapper.SnapMove(moving, new Vec2(3, 3.5), targets, Options);
            Assert.Equal("rail", res.Kind);
            Assert.Equal(50, tip.P.Y + res.Delta.Y, 6);
            Assert.Equal(road.Id, res.Target!.Owner);
        }

        [Fact]
        public void PoiBadgeCentreDocksOntoARoute()
        {
            var r = Fx.Reg();
            var route = Fx.Line(r, "map.route", new Vec2(0, 0), new Vec2(100, 0), new Vec2(100, 100));
            var poi = Fx.Item(r, "map.poi.fuel", new Vec2(98, 40));
            var (doc, page) = Fx.Doc(route, poi);
            var ctx = new RenderContext(doc);
            var moving = r.PageAnchors(poi, ctx).ToList();
            var res = Snapper.SnapMove(moving, new Vec2(1, 0), Snapper.Targets(r, doc, page, new[] { poi.Id }, ctx, Options), Options);
            Assert.Equal("rail", res.Kind);
            Assert.Equal(2, res.Delta.X, 6);
        }

        [Fact]
        public void RoutePointDocksOntoARoadAndOntoAStopAndFollowsTheStop()
        {
            var r = Fx.Reg();
            var road = Fx.Line(r, "map.road.side", new Vec2(0, 100), new Vec2(200, 100));
            var stop = Fx.Item(r, "map.stop", new Vec2(150, 40));
            var route = Fx.Line(r, "map.route", new Vec2(20, 20), new Vec2(60, 98), new Vec2(148, 41));
            var (doc, page) = Fx.Doc(road, stop, route);
            var ctx = new RenderContext(doc);
            var targets = Snapper.Targets(r, doc, page, new[] { route.Id }, ctx, Options);

            // Waypoint 1 onto the road centre line (rail, anywhere along it).
            var onRoad = Snapper.SnapPoint(new Vec2(60, 98), targets, Options);
            Assert.Equal("rail", onRoad.Kind);
            Assert.Equal(new Vec2(60, 100), new Vec2(60, 98) + onRoad.Delta);

            // Last point onto the stop's centre port: docked and attached.
            var p = new Vec2(148, 41);
            var res = Snapper.SnapPoint(p, targets, Options);
            Assert.Equal("port", res.Kind);
            var snapped = p + res.Delta;
            Assert.Equal(new Vec2(150, 40), snapped);
            var att = Attachments.FromSnap(doc, snapped, res);
            Assert.NotNull(att);
            Assert.Equal(stop.Id, att!.ObjectId);
            Assert.Equal("loc", att.AnchorId);
            doc = Edits.SetPoint(doc, route.Id, 2, snapped, att);

            // Moving the stop drags the route end along.
            doc = Edits.Move(doc, new[] { stop.Id }, new Vec2(10, 5));
            var pts = doc.ResolvePoints(doc.Get(route.Id)!);
            Assert.Equal(new Vec2(160, 45), pts[2]);
        }

        [Fact]
        public void RoutePointDocksOntoAMarkerTip()
        {
            var r = Fx.Reg();
            var marker = Fx.Item(r, "map.marker.finish", new Vec2(50, 50));
            var route = Fx.Line(r, "map.route", new Vec2(0, 0), new Vec2(49, 54));
            var (doc, page) = Fx.Doc(marker, route);
            var ctx = new RenderContext(doc);
            var res = Snapper.SnapPoint(new Vec2(49, 54), Snapper.Targets(r, doc, page, new[] { route.Id }, ctx, Options), Options);
            Assert.Equal("port", res.Kind);
            Assert.Equal(new Vec2(50, 55.5), new Vec2(49, 54) + res.Delta);
        }

        [Fact]
        public void CalloutTailAndPlaceDotArePorts()
        {
            var r = Fx.Reg();
            var callout = Fx.Item(r, "map.callout", new Vec2(0, 0));
            var place = Fx.Item(r, "map.place.city", new Vec2(0, 0));
            var ctx = Fx.Ctx();
            var tip = r.PageAnchors(callout, ctx).Single(a => a.Id == "tip");
            Assert.Equal(new Vec2(-14.5, 7), tip.P);
            var dot = r.PageAnchors(place, ctx).Single(a => a.Id == "loc");
            Assert.Equal(AnchorRole.Port, dot.Role);
            Assert.Equal(-14.8, dot.P.X, 6);
            var right = callout.WithParam("tail", "right");
            Assert.Equal(new Vec2(14.5, 7), r.PageAnchors(right, ctx).Single(a => a.Id == "tip").P);
        }

        [Fact]
        public void RegionOffersItsCentroid()
        {
            var r = Fx.Reg();
            var region = Fx.Line(r, "map.region", new Vec2(0, 0), new Vec2(40, 0), new Vec2(40, 20), new Vec2(0, 20));
            var (doc, _) = Fx.Doc(region);
            var c = r.PageAnchors(region, new RenderContext(doc)).Single(a => a.Id == "centroid");
            Assert.Equal(new Vec2(20, 10), c.P);
        }
    }
}
