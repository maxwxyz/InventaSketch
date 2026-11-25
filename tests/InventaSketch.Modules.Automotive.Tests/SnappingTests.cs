using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Automotive.Tests
{
    /// <summary>Mount-to-mount snapping of sensors and lane centres as rails.</summary>
    public class SnappingRuleTests
    {
        [Fact]
        public void SensorMountSnapsAsMountRule()
        {
            var r = Fx.Reg();
            var car = r.Type("auto.vehicle.car")!.Create(new Vec2(100, 100));
            var (doc0, _) = Fx.Doc(car);
            var front = r.PageAnchors(car, new RenderContext(doc0)).Single(a => a.Id == "mount.front");
            // Camera placed so that its mount is 1.5 mm off the vehicle's front mount.
            var cam = r.Type("auto.sensor.camera")!.Create(Vec2.Zero);
            var camMount0 = r.PageAnchors(cam, Fx.Ctx()).Single(a => a.Id == "mount").P;
            var at = front.P - camMount0 + new Vec2(1.2, -0.9);
            cam = cam with { Transform = cam.Transform with { X = at.X, Y = at.Y } };
            var (doc, page) = Fx.Doc(car, cam);
            var ctx = new RenderContext(doc);
            var options = new SnapOptions { Tolerance = 2, Grid = null, Alignment = false, Page = false };
            var res = Snapper.SnapMove(r.PageAnchors(cam, ctx).ToList(), Vec2.Zero, Snapper.Targets(r, doc, page, new[] { cam.Id }, ctx, options), options);
            Assert.Equal("mount", res.Kind);
            Assert.Equal("mount.front", res.Target!.Id);
            Assert.Equal(car.Id, res.Target.Owner);
            var camMount = r.PageAnchors(cam, ctx).Single(a => a.Id == "mount").P + res.Delta;
            Assert.True(camMount.DistanceTo(front.P) < 1e-9);
        }

        [Fact]
        public void LaneCentresAreRailsForPointsAndAxesForVehicles()
        {
            var r = Fx.Reg();
            var road = r.Create(r.Item("auto.road.2")!, new Vec2(170, 100));
            var anchors = r.PageAnchors(road, Fx.Ctx()).ToList();
            Assert.Equal(anchors.Count(a => a.Role == AnchorRole.Axis), anchors.Count(a => a.Role == AnchorRole.Rail));
            Assert.Contains(anchors, a => a.Role == AnchorRole.Rail && a.Id == "lane1.rail");

            // A trajectory point dragged near the lower lane centre docks onto it.
            var laneCentre = 100 + road.Size.H / 4;
            var (doc, page) = Fx.Doc(road);
            var options = new SnapOptions { Tolerance = 3, Grid = null, Alignment = false, Page = false };
            var targets = Snapper.Targets(r, doc, page, new string[0], new RenderContext(doc), options);
            var p = new Vec2(140.4, laneCentre + 1.6);
            var res = Snapper.SnapPoint(p, targets, options);
            Assert.Equal("rail", res.Kind);
            Assert.Equal(140.4, (p + res.Delta).X, 9);
            Assert.Equal(laneCentre, (p + res.Delta).Y, 9);

            foreach (var item in new[] { "auto.curve", "auto.intersection", "auto.roundabout" })
            {
                var o = r.Create(r.Item(item)!, new Vec2(100, 100));
                var list = r.PageAnchors(o, Fx.Ctx()).ToList();
                Assert.True(list.Count(a => a.Role == AnchorRole.Rail) > 0, item + " has no lane rails");
                Assert.Equal(list.Count(a => a.Role == AnchorRole.Axis), list.Count(a => a.Role == AnchorRole.Rail));
            }
        }
    }
}
