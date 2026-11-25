using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Automotive.Tests
{
    internal static class Fx
    {
        public static Registry Reg() => new Registry().Register(new AutomotiveModule());

        public static RenderContext Ctx() => new(SketchDocument.Create());

        /// <summary>All (library item, view) combinations of box types.</summary>
        public static IEnumerable<(LibraryItem Item, ObjectType Type, string View)> BoxItemViews(Registry r)
        {
            foreach (var item in r.Library)
            {
                var t = r.Type(item.TypeId)!;
                if (t.Geometry != GeometryKind.Box) continue;
                foreach (var v in t.Views) yield return (item, t, v.Id);
            }
        }

        public static void AssertFinite(IEnumerable<Primitive> prims, string what)
        {
            foreach (var pt in Prim.Flatten(prims).SelectMany(Prim.Points))
                Assert.False(double.IsNaN(pt.X) || double.IsNaN(pt.Y) || double.IsInfinity(pt.X) || double.IsInfinity(pt.Y), what + ": NaN/Infinity");
        }

        public static void AssertInsideBox(IReadOnlyList<Primitive> prims, Size2 size, string what)
        {
            Assert.True(prims.Count > 0, what + ": no primitives");
            var b = Prim.Bounds(prims);
            Assert.True(b.HasValue, what + ": no bounds");
            var r = b!.Value;
            const double tol = 0.02;
            Assert.True(r.X >= -size.W / 2 - tol && r.Right <= size.W / 2 + tol && r.Y >= -size.H / 2 - tol && r.Bottom <= size.H / 2 + tol,
                $"{what}: bounds {r} exceed box {size.W}x{size.H}");
            AssertFinite(prims, what);
        }

        public static (SketchDocument Doc, Page Page) Doc(params SketchObject[] objects)
        {
            var doc = SketchDocument.Create();
            var page = doc.Pages[0];
            doc = Edits.Add(doc, page.Id, objects);
            return (doc, doc.Pages[0]);
        }
    }

    public class ModuleRegistrationTests
    {
        public static readonly string[] ExpectedTypes =
        {
            "auto.vehicle.car", "auto.vehicle.suv", "auto.vehicle.van", "auto.vehicle.truck", "auto.vehicle.semi", "auto.vehicle.bus",
            "auto.vehicle.motorcycle", "auto.vehicle.bicycle", "auto.vehicle.scooter", "auto.vehicle.trailer", "auto.vehicle.box",
            "auto.sub.axles", "auto.sub.steering", "auto.sub.battery", "auto.sub.edrive", "auto.sub.seat", "auto.sub.door",
            "auto.human.standing", "auto.human.walking", "auto.human.seated", "auto.human.child", "auto.human.hand", "auto.human.head",
            "auto.road.straight", "auto.road.curve", "auto.road.intersection", "auto.road.roundabout", "auto.road.crosswalk", "auto.marking",
            "auto.traffic.light", "auto.traffic.sign", "auto.traffic.barrier", "auto.traffic.parking", "auto.traffic.building", "auto.traffic.obstacle",
            "auto.sensor.camera", "auto.sensor.radar", "auto.sensor.lidar", "auto.sensor.ultrasonic", "auto.fov",
            "auto.det.bbox2d", "auto.det.bbox3d", "auto.det.grid", "auto.det.lanepoly",
            "auto.trajectory", "auto.motion.conflict", "auto.motion.safetydistance", "auto.motion.ttc",
            "auto.hmi.cluster", "auto.hmi.display", "auto.hmi.hud", "auto.hmi.button", "auto.hmi.knob", "auto.hmi.slider", "auto.hmi.warning", "auto.hmi.wireframe",
        };

        [Fact]
        public void ModuleRegistersWithAllTypes()
        {
            var r = Fx.Reg();
            Assert.Equal("automotive", r.Modules.Single().Id);
            var ids = r.Types.Select(t => t.Id).ToList();
            foreach (var id in ExpectedTypes) Assert.Contains(id, ids);
            Assert.All(ids, id => Assert.StartsWith("auto.", id));
        }

        [Fact]
        public void EverythingIsBilingualCategorisedAndHasViewIcons()
        {
            var r = Fx.Reg();
            var cats = new HashSet<string>(r.Categories.Select(c => c.Id));
            var icons = typeof(IconIds).GetFields().Select(f => (string)f.GetValue(null)!).ToList();
            foreach (var t in r.Types)
            {
                Assert.False(string.IsNullOrEmpty(t.Name.De), t.Id + " has no German name");
                Assert.Contains(t.Category, cats);
                Assert.NotEmpty(t.Keywords);
                foreach (var v in t.Views)
                {
                    Assert.False(string.IsNullOrEmpty(v.Label.De), t.Id + "/" + v.Id);
                    Assert.Contains(v.Icon, icons);
                }
                foreach (var p in t.Parameters)
                {
                    Assert.False(string.IsNullOrEmpty(p.Label.De), t.Id + "/" + p.Key);
                    foreach (var opt in p.Options ?? Array.Empty<ParamOption>()) Assert.False(string.IsNullOrEmpty(opt.Label.De), t.Id + "/" + p.Key + "/" + opt.Value);
                }
            }
            foreach (var item in r.Library)
            {
                Assert.False(string.IsNullOrEmpty(item.Name.De), item.Id);
                Assert.Contains(item.Category, cats);
            }
            foreach (var c in r.Categories) Assert.Contains(r.Library, i => i.Category == c.Id);
            foreach (var t in r.Types) Assert.Contains(r.Library, i => i.TypeId == t.Id);
            Assert.Equal(r.Library.Count, r.Library.Select(i => i.Id).Distinct().Count());
        }

        [Theory]
        [InlineData("car")]
        [InlineData("pkw")]
        [InlineData("lkw")]
        [InlineData("fußgänger")]
        [InlineData("pedestrian")]
        [InlineData("kreuzung")]
        [InlineData("radar")]
        [InlineData("sichtbereich")]
        [InlineData("trajektorie")]
        [InlineData("head-up")]
        [InlineData("ampel")]
        public void SearchFindsEnglishAndGerman(string q) => Assert.NotEmpty(Fx.Reg().Search(q));

        [Fact]
        public void ParametersAreOptional()
        {
            var r = Fx.Reg();
            foreach (var t in r.Types)
            {
                // A bare object without any parameters must render (quick sketch).
                var o = t.Geometry == GeometryKind.Points
                    ? new SketchObject { Id = "x", Type = t.Id, Points = t.DefaultPoints.ToList() }
                    : new SketchObject { Id = "x", Type = t.Id, Size = t.DefaultSize };
                Assert.NotEmpty(t.Render(o, Fx.Ctx()));
            }
        }
    }

    public class RenderTests
    {
        [Fact]
        public void EveryLibraryItemRendersInEveryViewInsideTheBox()
        {
            var r = Fx.Reg();
            var count = 0;
            foreach (var (item, type, view) in Fx.BoxItemViews(r))
            {
                var o = r.Create(item, Vec2.Zero);
                if (o.View != view)
                {
                    var act = type.Actions(o, Lang.En).FirstOrDefault(a => a.Id == "view." + view);
                    o = act is null ? o with { View = view } : act.Apply(o);
                }
                Fx.AssertInsideBox(type.Render(o, Fx.Ctx()), o.Size, $"{item.Id}/{view}");
                count++;
            }
            Assert.True(count > 250, "expected many item/view combinations, got " + count);
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
                var o = r.Create(item, Vec2.Zero) with { View = view };
                o = o with { Size = new Size2(o.Size.W * sx, o.Size.H * sy) };
                Fx.AssertInsideBox(type.Render(o, Fx.Ctx()), o.Size, $"{item.Id}/{view} scaled {sx}x{sy}");
            }
        }

        [Fact]
        public void PointItemsRenderFiniteGeometry()
        {
            var r = Fx.Reg();
            foreach (var item in r.Library.Where(i => r.Type(i.TypeId)!.Geometry == GeometryKind.Points))
            {
                var o = r.Create(item, new Vec2(100, 80));
                var (doc, _) = Fx.Doc(o);
                var prims = r.Render(o, new RenderContext(doc));
                Assert.NotEmpty(prims);
                Fx.AssertFinite(prims, item.Id);
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
                var o = r.Create(item, Vec2.Zero);
                foreach (var a in type.Actions(o, Lang.En))
                {
                    var x = a.Apply(o);
                    Assert.Equal(o.Id, x.Id);
                    var (doc, _) = Fx.Doc(x);
                    var prims = type.Render(x, new RenderContext(doc));
                    if (type.Geometry == GeometryKind.Box) Fx.AssertInsideBox(prims, x.Size, $"{item.Id}/{a.Id}");
                    else Fx.AssertFinite(prims, $"{item.Id}/{a.Id}");
                }
            }
        }

        [Fact]
        public void ScalingChangesSizeNotParameters()
        {
            var r = Fx.Reg();
            var road = r.Create(r.Item("auto.road.3")!, new Vec2(100, 100));
            var (doc, _) = Fx.Doc(road);
            doc = Edits.Scale(doc, new[] { road.Id }, new Vec2(100, 100), 2, 0.5);
            var scaled = doc.Get(road.Id)!;
            Assert.Equal(3, scaled.Int("lanes", 0));
            Assert.Equal(road.Size.W * 2, scaled.Size.W, 6);
            // lane centrelines follow the stretched box
            var axes = r.PageAnchors(scaled, new RenderContext(doc)).Where(a => a.Role == AnchorRole.Axis).ToList();
            Assert.Equal(3, axes.Count);
            Assert.Equal(100 - scaled.Size.H / 3, axes[0].P.Y, 6);
        }
    }

    public class VehicleTests
    {
        [Fact]
        public void NextViewCyclesAllViewsAndKeepsScale()
        {
            var r = Fx.Reg();
            var t = r.Type("auto.vehicle.car")!;
            var o = t.Create(new Vec2(50, 50));
            Assert.Equal("top", o.View);
            o = o with { Size = new Size2(o.Size.W * 2, o.Size.H * 2) };
            var seen = new List<string>();
            for (var i = 0; i < 4; i++)
            {
                o = t.Actions(o, Lang.En).Single(a => a.Id == "view.next").Apply(o);
                seen.Add(o.View!);
            }
            Assert.Equal(new[] { "side", "front", "rear", "top" }, seen);
            Assert.Equal(90, o.Size.W, 6);
            var side = t.Actions(o, Lang.En).Single(a => a.Id == "view.side").Apply(o);
            Assert.Equal(90, side.Size.W, 6);
            Assert.Equal(29, side.Size.H, 6);
        }

        [Fact]
        public void ViewsUseTheSharedIconVocabulary()
        {
            var t = Fx.Reg().Type("auto.vehicle.truck")!;
            Assert.Equal(new[] { IconIds.Top, IconIds.Side, IconIds.Front, IconIds.Rear }, t.Views.Select(v => v.Icon));
        }

        [Theory]
        [InlineData("auto.vehicle.car")]
        [InlineData("auto.vehicle.semi")]
        [InlineData("auto.vehicle.bicycle")]
        public void VehiclesExposeCentreAxleAndMountAnchors(string typeId)
        {
            var r = Fx.Reg();
            var o = r.Type(typeId)!.Create(new Vec2(100, 100));
            var anchors = r.PageAnchors(o, Fx.Ctx()).ToList();
            Assert.Contains(anchors, a => a.Role == AnchorRole.Hub && a.Id == "center" && a.P.DistanceTo(new Vec2(100, 100)) < 1e-9);
            var front = anchors.Single(a => a.Id == "axle.front");
            var rear = anchors.Single(a => a.Id == "axle.rear");
            Assert.True(front.P.X > rear.P.X, "front axle ahead of the rear axle (driving direction +x)");
            if (typeId != "auto.vehicle.bicycle") Assert.Contains(anchors, a => a.Role == AnchorRole.Mount && a.Id == "mount.front");
        }

        [Fact]
        public void VehicleSnapsOntoLaneCentreline()
        {
            var r = Fx.Reg();
            var road = r.Create(r.Item("auto.road.2")!, new Vec2(170, 100));
            var laneCentre = 100 + road.Size.H / 4;          // lower lane centre (2 lanes)
            var car = r.Create(r.Item("auto.car.top")!, new Vec2(150, laneCentre + 1.5));
            var (doc, page) = Fx.Doc(road, car);
            var ctx = new RenderContext(doc);
            var options = new SnapOptions { Tolerance = 3 };
            var targets = Snapper.Targets(r, doc, page, new[] { car.Id }, ctx, options);
            var moving = r.PageAnchors(car, ctx).ToList();
            var res = Snapper.SnapMove(moving, new Vec2(4, 0), targets, options);
            Assert.Equal("axis", res.Kind);
            Assert.Equal(laneCentre, car.Transform.Y + res.Delta.Y, 6);
            Assert.Equal(4, res.Delta.X, 6);
        }

        [Fact]
        public void VehicleSnapsOntoRotatedLaneAndCurve()
        {
            var r = Fx.Reg();
            var road = r.Create(r.Item("auto.road.2")!, new Vec2(170, 100)) with { Transform = new Transform(170, 100, 30) };
            var lane = r.PageAnchors(road, Fx.Ctx()).First(a => a.Role == AnchorRole.Axis);
            var onLane = Vec2.Lerp(lane.P, lane.Q, 0.4);
            var car = r.Type("auto.vehicle.car")!.Create(onLane + new Vec2(1.0, -1.2));
            var (doc, page) = Fx.Doc(road, car);
            var ctx = new RenderContext(doc);
            var options = new SnapOptions { Tolerance = 3 };
            var res = Snapper.SnapMove(r.PageAnchors(car, ctx).ToList(), Vec2.Zero, Snapper.Targets(r, doc, page, new[] { car.Id }, ctx, options), options);
            Assert.Equal("axis", res.Kind);
            var c = new Vec2(car.Transform.X, car.Transform.Y) + res.Delta;
            Assert.True((Geo.ProjectOnLine(c, lane.P, lane.Q) - c).Length < 1e-6, "vehicle centre on the rotated lane axis");

            var curve = r.Create(r.Item("auto.curve")!, new Vec2(100, 100));
            var chord = r.PageAnchors(curve, Fx.Ctx()).Where(a => a.Role == AnchorRole.Axis).Skip(3).First();
            var bike = r.Type("auto.vehicle.bicycle")!.Create(Vec2.Lerp(chord.P, chord.Q, 0.5) + (chord.Q - chord.P).Normalized().Perp * 1.5);
            (doc, page) = Fx.Doc(curve, bike);
            ctx = new RenderContext(doc);
            res = Snapper.SnapMove(r.PageAnchors(bike, ctx).ToList(), Vec2.Zero, Snapper.Targets(r, doc, page, new[] { bike.Id }, ctx, options), options);
            Assert.Equal("axis", res.Kind);
        }

        [Fact]
        public void SensorMountSnapsToVehicleMountPoint()
        {
            var r = Fx.Reg();
            var car = r.Type("auto.vehicle.car")!.Create(new Vec2(100, 100));
            var cam = r.Type("auto.sensor.camera")!.Create(new Vec2(130, 101));
            var (doc, page) = Fx.Doc(car, cam);
            var ctx = new RenderContext(doc);
            var options = new SnapOptions { Tolerance = 4 };
            var res = Snapper.SnapMove(r.PageAnchors(cam, ctx).ToList(), Vec2.Zero, Snapper.Targets(r, doc, page, new[] { cam.Id }, ctx, options), options);
            var mount = r.PageAnchors(car, ctx).Single(a => a.Id == "mount.front");
            var camMount = r.PageAnchors(cam, ctx).Single(a => a.Id == "mount");
            Assert.True((camMount.P + res.Delta - mount.P).Length < 1e-6, $"camera mount snapped to the front mount ({res.Kind})");
        }
    }

    public class RoadTests
    {
        [Fact]
        public void LanesActionsChangeLaneCountAndKeepLaneWidth()
        {
            var r = Fx.Reg();
            var t = r.Type("auto.road.straight")!;
            var road = r.Create(r.Item("auto.road.2")!, Vec2.Zero);
            var lane = road.Size.H / 2;
            var more = t.Actions(road, Lang.En).Single(a => a.Id == "param.lanes.inc").Apply(road);
            Assert.Equal(3, more.Int("lanes", 0));
            Assert.Equal(3 * lane, more.Size.H, 6);
            var axes = t.Anchors(more, Fx.Ctx()).Where(a => a.Role == AnchorRole.Axis).ToList();
            Assert.Equal(3, axes.Count);
            var less = t.Actions(road, Lang.En).Single(a => a.Id == "param.lanes.dec").Apply(road);
            Assert.Equal(1, less.Int("lanes", 0));
            Assert.Equal(lane, less.Size.H, 6);
            var none = t.Actions(less, Lang.En).Single(a => a.Id == "param.lanes.dec").Apply(less);
            Assert.Equal(1, none.Int("lanes", 0));
        }

        [Fact]
        public void MarkingsFollowThePattern()
        {
            var r = Fx.Reg();
            var t = r.Type("auto.road.straight")!;
            var road = r.Create(r.Item("auto.road.4")!, Vec2.Zero);
            var prims = t.Render(road, Fx.Ctx());
            var markings = prims.Where(p => p.Tag == AutoTags.Marking).ToList();
            // centre separator is one solid line, the two others are dashed (several pieces)
            Assert.Single(markings, p => Prim.Bounds(new[] { p })!.Value.W > road.Size.W - 0.01);
            Assert.True(markings.Count > 10);
            var none = t.Render(road.WithParam("markings", "none"), Fx.Ctx());
            Assert.DoesNotContain(none, p => p.Tag == AutoTags.Marking);
        }

        [Fact]
        public void IntersectionAndRoundaboutExposeLaneAxes()
        {
            var r = Fx.Reg();
            var x = r.Create(r.Item("auto.intersection")!, Vec2.Zero);
            Assert.Equal(8, r.TypeOf(x).Anchors(x, Fx.Ctx()).Count(a => a.Role == AnchorRole.Axis));
            var tee = r.Create(r.Item("auto.tjunction")!, Vec2.Zero);
            Assert.Equal(6, r.TypeOf(tee).Anchors(tee, Fx.Ctx()).Count(a => a.Role == AnchorRole.Axis));
            var ra = r.Create(r.Item("auto.roundabout")!, Vec2.Zero);
            Assert.Equal(16 + 8, r.TypeOf(ra).Anchors(ra, Fx.Ctx()).Count(a => a.Role == AnchorRole.Axis));
        }
    }

    public class AdasTests
    {
        private static (Registry R, SketchDocument Doc, SketchObject Sensor, SketchObject Fov) Attached()
        {
            var r = Fx.Reg();
            var sensor = r.Type("auto.sensor.camera")!.Create(new Vec2(100, 100));
            var fov = r.Create(r.Item("auto.fov.camera")!, new Vec2(0, 0));
            var (doc, _) = Fx.Doc(sensor, fov);
            doc = Fov.AttachTo(doc, fov.Id, sensor.Id);
            return (r, doc, sensor, doc.Get(fov.Id)!);
        }

        [Fact]
        public void FovAttachesToTheSensorAperture()
        {
            var (r, doc, sensor, fov) = Attached();
            var pts = doc.ResolvePoints(fov);
            Assert.Equal(new Vec2(104, 100), pts[0]);
            var tip = r.PageAnchors(fov, new RenderContext(doc)).Single(a => a.Id == "tip");
            Assert.Equal(104 + 80, tip.P.X, 6);
            Assert.Equal(100, tip.P.Y, 6);
        }

        [Fact]
        public void FovFollowsTheSensorWhenMoved()
        {
            var (r, doc, sensor, fov) = Attached();
            var before = Prim.Bounds(r.Render(fov, new RenderContext(doc)))!.Value;
            doc = Edits.Move(doc, new[] { sensor.Id }, new Vec2(25, -10));
            var after = Prim.Bounds(r.Render(doc.Get(fov.Id)!, new RenderContext(doc)))!.Value;
            Assert.Equal(before.X + 25, after.X, 6);
            Assert.Equal(before.Y - 10, after.Y, 6);
            Assert.NotNull(doc.Get(fov.Id)!.Attach);
            Assert.Equal(new Vec2(129, 90), doc.ResolvePoints(doc.Get(fov.Id)!)[0]);
        }

        [Fact]
        public void FovFollowsTheSensorRotationAndMovesWithItInASelection()
        {
            var (r, doc, sensor, fov) = Attached();
            doc = Edits.Rotate(doc, new[] { sensor.Id }, new Vec2(100, 100), 90);
            var tip = r.PageAnchors(doc.Get(fov.Id)!, new RenderContext(doc)).Single(a => a.Id == "tip");
            Assert.Equal(100, tip.P.X, 6);
            Assert.Equal(100 + 4 + 80, tip.P.Y, 6);
            // moving sensor and FoV together keeps the attachment
            doc = Edits.Move(doc, new[] { sensor.Id, fov.Id }, new Vec2(10, 0));
            Assert.NotNull(doc.Get(fov.Id)!.Attach);
            Assert.Equal(new Vec2(110, 104), doc.ResolvePoints(doc.Get(fov.Id)!)[0]);
        }

        [Fact]
        public void FovActionsChangeRangeAndAngle()
        {
            var (r, doc, sensor, fov) = Attached();
            var t = r.TypeOf(fov);
            var acts = t.Actions(fov, Lang.En).ToList();
            Assert.Equal(100, acts.Single(a => a.Id == "param.range.inc").Apply(fov).Num("range", 0), 6);
            Assert.Equal(64, acts.Single(a => a.Id == "param.range.dec").Apply(fov).Num("range", 0), 6);
            Assert.Equal(60, acts.Single(a => a.Id == "param.angle.inc").Apply(fov).Num("angle", 0), 6);
            Assert.Equal(40, acts.Single(a => a.Id == "param.angle.dec").Apply(fov).Num("angle", 0), 6);
            var wider = acts.Single(a => a.Id == "param.angle.inc").Apply(fov);
            var b0 = Prim.Bounds(t.Render(fov, new RenderContext(doc)))!.Value;
            var b1 = Prim.Bounds(t.Render(wider, new RenderContext(doc)))!.Value;
            Assert.True(b1.H > b0.H, "wider sector is taller");
        }

        [Fact]
        public void LidarFovCanBeAFullCircle()
        {
            var r = Fx.Reg();
            var o = r.Create(r.Item("auto.fov.lidar")!, new Vec2(50, 50));
            var (doc, _) = Fx.Doc(o);
            var sector = r.Render(o, new RenderContext(doc)).First(p => p.Tag == AutoTags.Sector);
            Assert.IsType<EllipsePrim>(sector);
        }

        [Fact]
        public void TrajectoryCorridorToggle()
        {
            var r = Fx.Reg();
            var t = r.Type("auto.trajectory")!;
            var o = t.Create(new Vec2(100, 100));
            var (doc, _) = Fx.Doc(o);
            var ctx = new RenderContext(doc);
            Assert.DoesNotContain(t.Render(o, ctx), p => p.Tag == AutoTags.Corridor);
            var toggle = t.Actions(o, Lang.En).Single(a => a.Id == "param.corridor");
            Assert.False(toggle.IsActive);
            var on = toggle.Apply(o);
            var prims = t.Render(on, ctx);
            Assert.Equal(2, prims.Count(p => p.Tag == AutoTags.Corridor));
            Assert.Equal(4, prims.Count(p => p.Tag == AutoTags.Tick));
            Assert.Equal(new[] { "t0", "t1", "t2", "t3" }, prims.OfType<TextPrim>().Select(x => x.Text));
            // corridor boundaries are offset by half the width
            var path = (PathPrim)prims.Single(p => p.Tag == AutoTags.Path);
            var start = path.Segments[0].P;
            var corridor = (PathPrim)prims.First(p => p.Tag == AutoTags.Corridor);
            Assert.Equal(9, corridor.Segments[0].P.DistanceTo(start), 6);
            Assert.True(t.Actions(on, Lang.En).Single(a => a.Id == "param.corridor").IsActive);
        }
    }

    public class PersistenceTests
    {
        [Fact]
        public void JsonRoundTripPreservesSceneAndRendering()
        {
            var r = Fx.Reg();
            var (doc, page) = Scene.Build(r);
            var json = DocumentJson.Serialize(doc);
            var back = DocumentJson.Deserialize(json);
            Assert.Equal(doc.Objects.Count, back.Objects.Count);
            foreach (var o in doc.Objects.Values)
            {
                var b = back.Get(o.Id)!;
                Assert.Equal(o.Type, b.Type);
                Assert.Equal(o.View, b.View);
                Assert.Equal(o.Params.Count, b.Params.Count);
                Assert.Equal(o.Attach?.Count ?? 0, b.Attach?.Count ?? 0);
            }
            Assert.Equal(SvgRenderer.RenderPage(r, doc, page), SvgRenderer.RenderPage(r, back, back.Pages[0]));
            Assert.Equal(json, DocumentJson.Serialize(back));
            var compact = DocumentJson.FromCompact(DocumentJson.ToCompact(doc));
            Assert.Equal(SvgRenderer.RenderPage(r, doc, page), SvgRenderer.RenderPage(r, compact, compact.Pages[0]));
        }

        [Fact]
        public void EveryLibraryItemSurvivesJson()
        {
            var r = Fx.Reg();
            var objects = r.Library.Select((item, i) => r.Create(item, new Vec2(20 + (i % 10) * 30, 20 + (i / 10) * 20))).ToArray();
            var (doc, page) = Fx.Doc(objects);
            var back = DocumentJson.Deserialize(DocumentJson.Serialize(doc));
            Assert.Equal(SvgRenderer.RenderPage(r, doc, page), SvgRenderer.RenderPage(r, back, back.Pages[0]));
        }
    }
}
