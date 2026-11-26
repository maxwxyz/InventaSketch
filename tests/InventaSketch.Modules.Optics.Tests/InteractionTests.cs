using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Optics.Tests
{
    /// <summary>Grips (drag handles) of the optics types: positions, constraints, labels and the parameters a drag writes.</summary>
    public class GripTests
    {
        private const double Tol = 1e-6;

        private static List<DragHandle> Grips(Registry r, SketchDocument doc, string id) => r.DragHandles(doc.Get(id)!, new RenderContext(doc)).ToList();

        private static SketchObject Move(Registry r, SketchDocument doc, string id, string key, Vec2 at)
            => r.MoveHandle(doc.Get(id)!, key, at, new RenderContext(doc));

        private static void Near(Vec2 expected, Vec2 actual, string what)
            => Assert.True(expected.DistanceTo(actual) < Tol, $"{what}: expected {expected}, got {actual}");

        [Fact]
        public void LensFocalLengthGripSitsAtTheFocalPointAndSetsF()
        {
            var r = Fx.Reg();
            var lens = Fx.Item(r, "optics.lens.biconvex", new Vec2(200, 150)).WithParam("f", 30.0);
            var doc = Fx.Doc(lens);
            // Without the focal point marks: one grip, at F′ behind the lens, moving along the axis.
            var g = Assert.Single(Grips(r, doc, lens.Id));
            Assert.Equal("f.back", g.Key);
            Near(new Vec2(230, 150), g.Position, "F′");
            Assert.Equal(DragConstraint.Horizontal, g.Constraint);
            Near(new Vec2(1, 0), g.Direction, "direction");
            Assert.Equal("Focal length f: 30 mm", g.Label.En);
            Assert.Equal("Brennweite f: 30 mm", g.Label.De);
            Assert.Null(g.Part);
            // Dragged to 47.4 mm behind the lens: f = 47 mm (whole millimetres); only f changes, the box stays.
            var moved = Move(r, doc, lens.Id, "f.back", new Vec2(247.4, 153));
            Assert.Equal(47, moved.Num("f", 0), 9);
            Assert.Equal(lens.Size, moved.Size);
            Assert.Equal(lens.Transform, moved.Transform);

            // With the marks: both focal points, part "focal".
            var marked = lens.WithParam("focal", true);
            var d2 = Fx.Doc(marked);
            var both = Grips(r, d2, marked.Id);
            Assert.Equal(new[] { "f.front", "f.back" }, both.Select(h => h.Key));
            Near(new Vec2(170, 150), both[0].Position, "F");
            Assert.All(both, h => Assert.Equal(OpticsTags.Focal, h.Part));
            Assert.Equal(44, Move(r, d2, marked.Id, "f.front", new Vec2(155.6, 140)).Num("f", 0), 9);
            // Never below 1 mm, and the grip may not cross to the other side to change the sign.
            Assert.Equal(1, Move(r, d2, marked.Id, "f.front", new Vec2(200.2, 150)).Num("f", 0), 9);
            Assert.Equal(15, Move(r, d2, marked.Id, "f.front", new Vec2(215, 150)).Num("f", 0), 9);
            Assert.Same(marked, Move(r, d2, marked.Id, "nothing", new Vec2(0, 0)));
        }

        [Fact]
        public void TurnedAndDivergingLensesKeepTheGripOnTheirAxis()
        {
            var r = Fx.Reg();
            var lens = Fx.Item(r, "optics.lens.biconvex", new Vec2(200, 150)).WithParam("f", 30.0);
            var turned = lens with { Transform = lens.Transform with { Rotation = 90 } };
            var doc = Fx.Doc(turned);
            var g = Assert.Single(Grips(r, doc, turned.Id));
            Near(new Vec2(200, 180), g.Position, "turned F′");
            Near(new Vec2(0, 1), g.Direction, "turned direction");
            Assert.Equal(72, Move(r, doc, turned.Id, "f.back", new Vec2(201, 222.3)).Num("f", 0), 9);

            var div = Fx.Item(r, "optics.lens.biconcave", new Vec2(200, 150)).WithParam("f", 25.0);
            var d2 = Fx.Doc(div);
            Near(new Vec2(225, 150), Assert.Single(Grips(r, d2, div.Id)).Position, "diverging");
            var moved = Move(r, d2, div.Id, "f.back", new Vec2(260, 150));
            Assert.Equal(60, moved.Num("f", 0), 9);
            Assert.Equal(-60, ((LensType)r.TypeOf(moved)).F(moved), 9);   // still diverging
        }

        [Fact]
        public void LensBundleHeightGripAppearsWithItsBundle()
        {
            var r = Fx.Reg();
            var lens = Fx.Item(r, "optics.lens.biconvex", new Vec2(200, 150)).WithParam("f", 30.0);
            var (doc, _) = Fx.Bundle(Fx.Doc(lens), lens, "o");
            var beam = Grips(r, doc, lens.Id).Single(h => h.Key == "beam");
            Near(new Vec2(170, 150 - 0.7 * 17), beam.Position, "bundle edge");
            Assert.Equal(DragConstraint.Vertical, beam.Constraint);
            Assert.Equal(40, Move(r, doc, lens.Id, "beam", new Vec2(170, 130.2)).Num("beam", 0), 9);
            // Rays from a point: the edge sits on the lens plane.
            var point = doc.Get(lens.Id)!.WithParam("bundle", "point");
            var d2 = Edits.Replace(doc, point);
            Near(new Vec2(200, 150 - 0.7 * 17), Grips(r, d2, lens.Id).Single(h => h.Key == "beam").Position, "point bundle edge");
        }

        [Fact]
        public void CurvedMirrorGripsSitAtFocusAndCentreOfCurvature()
        {
            var r = Fx.Reg();
            var mirror = Fx.Item(r, "optics.mirror.concave", new Vec2(200, 150)).WithParam("f", 40.0);
            var doc = Fx.Doc(mirror);
            var anchors = r.PageAnchors(mirror, new RenderContext(doc)).Where(a => a.Id is not null).ToDictionary(a => a.Id!, a => a.P);
            var g = Assert.Single(Grips(r, doc, mirror.Id));
            Assert.Equal("f", g.Key);
            Near(anchors["f"], g.Position, "F of the concave mirror");
            var vertex = anchors["o"];
            Assert.Equal(55, Move(r, doc, mirror.Id, "f", new Vec2(vertex.X - 55.4, 150)).Num("f", 0), 9);

            var marked = mirror.WithParam("focal", true);
            var d2 = Fx.Doc(marked);
            var cc = Grips(r, d2, marked.Id).Single(h => h.Key == "cc");
            Near(anchors["cc"], cc.Position, "centre of curvature");
            Assert.Equal(50, Move(r, d2, marked.Id, "cc", new Vec2(vertex.X - 100.6, 150)).Num("f", 0), 9);

            // A convex mirror's focal point lies behind it.
            var convex = Fx.Item(r, "optics.mirror.convex", new Vec2(200, 150)).WithParam("f", 30.0);
            var d3 = Fx.Doc(convex);
            var cv = r.PageAnchors(convex, new RenderContext(d3)).Single(a => a.Id == "o").P;
            Near(new Vec2(cv.X + 30, 150), Assert.Single(Grips(r, d3, convex.Id)).Position, "convex F");
            Assert.Equal(20, Move(r, d3, convex.Id, "f", new Vec2(cv.X + 20, 150)).Num("f", 0), 9);
            var plane = Fx.Item(r, "optics.mirror.plane", new Vec2(200, 150));
            Assert.Empty(Grips(r, Fx.Doc(plane), plane.Id));
        }

        [Fact]
        public void StopOpeningGripSitsOnTheBladeEdge()
        {
            var r = Fx.Reg();
            var stop = Fx.Item(r, "optics.stop.aperture", new Vec2(200, 150));
            var doc = Fx.Doc(stop);
            var g = Assert.Single(Grips(r, doc, stop.Id));
            Near(new Vec2(200, 150 - 0.35 * 17), g.Position, "blade edge");
            Assert.Equal("Opening: 35 %", g.Label.En);
            Assert.Equal(0.5, Move(r, doc, stop.Id, "opening", new Vec2(200, 141.5)).Num("opening", 0), 9);
            Assert.Equal(0.95, Move(r, doc, stop.Id, "opening", new Vec2(200, 100)).Num("opening", 0), 9);
        }

        [Fact]
        public void LampConeGripFollowsTheEdgeRayAndSetsTheHalfConeAngle()
        {
            var r = Fx.Reg();
            var lamp = Fx.Item(r, "optics.source.lamp", new Vec2(100, 150));
            var lens = Fx.Item(r, "optics.lens.biconvex", new Vec2(180, 150));
            Assert.Empty(Grips(r, Fx.Doc(lamp, lens), lamp.Id));   // no rays, no grip
            var (doc, rayId) = Fx.Bundle(Fx.Doc(lamp, lens), lamp, "exit");
            var g = Assert.Single(Grips(r, doc, lamp.Id));
            Assert.Equal("spread", g.Key);
            var origin = r.PageAnchors(lamp, new RenderContext(doc)).Single(a => a.Id == "o").P;
            // The grip lies on the upper edge ray of the automatic cone (the first traced ray).
            var first = Fx.Trace(doc, rayId).Paths[0];
            var (_, d) = Fx.Segment(first, 0);
            Assert.True(Math.Abs((g.Position - origin).Normalized().Cross(d)) < 1e-9, "grip off the edge ray");
            // Dragged to 20° from the axis: spread = 20°, and the traced cone opens to it.
            var at = origin + new Vec2(Math.Cos(Geo.DegToRad(20)), -Math.Sin(Geo.DegToRad(20))) * 30;
            var moved = Move(r, doc, lamp.Id, "spread", at + new Vec2(0, 0.05));
            Assert.Equal(20, moved.Num("spread", 0), 9);
            var after = Edits.Replace(doc, moved);
            Assert.Equal(-20, Fx.Deg(Fx.Segment(Fx.Trace(after, rayId).Paths[0], 0).D), 6);
            Assert.Equal("Half cone angle: 20°", Grips(r, after, lamp.Id).Single().Label.En);

            // Parallel light: the edge of the bundle sets its height.
            var parallel = Edits.Replace(doc, doc.Get(lamp.Id)!.WithParam("emission", "parallel"));
            var beam = Assert.Single(Grips(r, parallel, lamp.Id));
            Assert.Equal("beam", beam.Key);
            Assert.Equal(DragConstraint.Vertical, beam.Constraint);
            Assert.Equal(24, Move(r, parallel, lamp.Id, "beam", new Vec2(beam.Position.X, 150 - 12)).Num("beam", 0), 9);
            // All directions: nothing to drag.
            Assert.Empty(Grips(r, Edits.Replace(doc, doc.Get(lamp.Id)!.WithParam("emission", "radial")), lamp.Id));
        }

        [Fact]
        public void LedLaserAndFibreGrips()
        {
            var r = Fx.Reg();
            var led = Fx.Item(r, "optics.source.led", new Vec2(100, 150)).WithParam("spread", 10.0);
            var (d1, _) = Fx.Bundle(Fx.Doc(led), led, "exit");
            var cone = Assert.Single(Grips(r, d1, led.Id));
            var origin = r.PageAnchors(led, new RenderContext(d1)).Single(a => a.Id == "o").P;
            Assert.Equal(-10, Fx.Deg(cone.Position - origin), 9);
            Assert.Equal(35, Move(r, d1, led.Id, "spread", origin + new Vec2(Math.Cos(Geo.DegToRad(35)), Math.Sin(Geo.DegToRad(35))) * 20).Num("spread", 0), 9);

            var laser = Fx.Item(r, "optics.source.laser", new Vec2(100, 150));
            var (d2, _) = Fx.Bundle(Fx.Doc(laser), laser, "exit");
            Assert.Empty(Grips(r, d2, laser.Id));   // a single ray has no height
            var three = Edits.Replace(d2, d2.Get(laser.Id)!.WithParam("rays", 3));
            var beam = Assert.Single(Grips(r, three, laser.Id));
            Near(new Vec2(100 + laser.Size.W / 2, 150 - 0.3 * laser.Size.H / 2), beam.Position, "laser beam edge");
            Assert.Equal(8, Move(r, three, laser.Id, "beam", new Vec2(114, 154)).Num("beam", 0), 9);

            var fibre = Fx.Item(r, "optics.fibre", new Vec2(100, 150));
            var (d3, _) = Fx.Bundle(Fx.Doc(fibre), fibre, "exit");
            var spread = Assert.Single(Grips(r, d3, fibre.Id));
            var exit = new Vec2(100 + fibre.Size.W / 2, 150);
            Assert.Equal(-12, Fx.Deg(spread.Position - exit), 9);
            Assert.Equal(30, Move(r, d3, fibre.Id, "spread", exit + new Vec2(Math.Cos(Geo.DegToRad(30)), -Math.Sin(Geo.DegToRad(30))) * 15).Num("spread", 0), 9);
        }

        [Fact]
        public void FreeRayDirectionGripTurnsTheRayAroundItsStart()
        {
            var r = Fx.Reg();
            var ray = Fx.Ray(new Vec2(100, 150), new Vec2(140, 150));
            var doc = Fx.Doc(ray);
            var g = Assert.Single(Grips(r, doc, ray.Id));
            Assert.Equal("dir", g.Key);
            Near(new Vec2(120, 150), g.Position, "halfway along the ray");
            Assert.Equal("Start angle: 0°", g.Label.En);
            Assert.Equal(DragConstraint.Free, g.Constraint);
            // Dragged anywhere: the direction snaps to whole degrees, the length (40 mm) stays.
            var moved = Move(r, doc, ray.Id, "dir", new Vec2(110.2, 160));
            var p = moved.Points!;
            Near(new Vec2(100, 150), p[0], "start");
            Assert.Equal(40, p[1].DistanceTo(p[0]), 9);
            Assert.Equal(44, Fx.Deg(p[1] - p[0]), 9);   // atan2(10, 10.2) = 44.4° → 44°
            // The ray follows: traced in its new direction.
            var after = Edits.Replace(doc, moved);
            Assert.Equal(44, Fx.Deg(Fx.Segment(Fx.Trace(after, ray.Id).Paths.Single(), 0).D), 9);

            // With an element in the way, the grip stays on the drawn part before it.
            var lens = Fx.Item(r, "optics.lens.biconvex", new Vec2(120, 150));
            Near(new Vec2(110, 150), Assert.Single(Grips(r, Fx.Doc(lens, ray), ray.Id)).Position, "before the lens");

            // A docked bundle takes its direction from the emitter: no grip on the ray.
            var laser = Fx.Item(r, "optics.source.laser", new Vec2(100, 150));
            var (d2, rayId) = Fx.Bundle(Fx.Doc(laser), laser, "exit");
            Assert.Empty(Grips(r, d2, rayId));
            // A freely drawn (untraced) ray is edited by its points.
            Assert.Empty(Grips(r, Fx.Doc(ray.WithParam("trace", false)), ray.Id));
        }

        [Fact]
        public void EveryGripIsBilingualAndEveryMoveKeepsTheBox()
        {
            var r = Fx.Reg();
            var lamp = Fx.Item(r, "optics.source.lamp", new Vec2(60, 150));
            var lens = Fx.Item(r, "optics.lens.biconvex", new Vec2(160, 150)).WithParam("focal", true);
            var mirror = Fx.Item(r, "optics.mirror.concave", new Vec2(300, 150)).WithParam("focal", true);
            var stop = Fx.Item(r, "optics.stop.aperture", new Vec2(220, 150));
            var ray = Fx.Ray(new Vec2(100, 250), new Vec2(140, 240));
            var doc = Fx.Doc(lamp, lens, mirror, stop, ray);
            (doc, _) = Fx.Bundle(doc, lamp, "exit");
            (doc, _) = Fx.Bundle(doc, lens, "o");
            var ctx = new RenderContext(doc);
            var count = 0;
            foreach (var o in doc.Objects.Values)
                foreach (var h in r.DragHandles(o, ctx))
                {
                    count++;
                    Assert.False(string.IsNullOrEmpty(h.Label.De), o.Type + "/" + h.Key);
                    Assert.False(double.IsNaN(h.Position.X) || double.IsNaN(h.Position.Y), o.Type + "/" + h.Key);
                    // Dropping a focal or opening grip where it is changes nothing (the values are already whole).
                    var same = r.MoveHandle(o, h.Key, h.Position, ctx);
                    Assert.Equal(o.Size, same.Size);
                    if (h.Key is "f.front" or "f.back" or "f" or "cc")
                        Assert.Equal(o.Num("f", 0), same.Num("f", 0), 9);
                    if (h.Key == "opening") Assert.Equal(o.Num("opening", 0.35), same.Num("opening", 0), 9);
                    var moved = r.MoveHandle(o, h.Key, h.Position + new Vec2(7, -5), ctx);
                    Assert.Equal(o.Size, moved.Size);
                    Assert.Equal(o.Transform, moved.Transform);
                }
            Assert.True(count >= 8, "grips: " + count);
        }
    }

    /// <summary>Image construction of the object arrow: thin lens equation, magnification, real and virtual images.</summary>
    public class ImagingTests
    {
        private const double Tol = 1e-6;
        private const double F = 40;

        /// <summary>A lens of focal length f at (300, 150) and an object arrow 16 mm high standing on the axis g in front of it.</summary>
        private static (Registry R, SketchDocument Doc, SketchObject Arrow) LensScene(double g, string lensId = "optics.lens.biconvex", double f = F, string mode = "image")
        {
            var r = Fx.Reg();
            var lens = Fx.Item(r, lensId, new Vec2(300, 150)).WithParam("f", f);
            var arrow = Fx.Item(r, "optics.object.arrow", new Vec2(300 - g, 150 - 8)).WithParam("image", mode);
            return (r, Fx.Doc(lens, arrow), arrow);
        }

        private static LinePrim ImageArrow(Registry r, SketchDocument doc, SketchObject arrow)
            => Prim.Flatten(r.Render(arrow, new RenderContext(doc))).OfType<LinePrim>().Single(l => l.Tag == OpticsTags.Image);

        private static void Near(Vec2 expected, Vec2 actual, string what)
            => Assert.True(expected.DistanceTo(actual) < Tol, $"{what}: expected {expected}, got {actual}");

        [Theory]
        // g = 2f: real, inverted, same size at b = 2f.
        [InlineData(80, 80, -1)]
        // g = 1.5f: real, inverted, twice the size at b = 3f.
        [InlineData(60, 120, -2)]
        // g < f: virtual, upright, magnified, on the object side (b = −40, β = 2).
        [InlineData(20, -40, 2)]
        public void ConvergingLensImagesByTheThinLensEquation(double g, double b, double beta)
        {
            var (r, doc, arrow) = LensScene(g);
            var frame = Imaging.Find(arrow, new RenderContext(doc))!;
            Assert.NotNull(frame);
            var tip = Imaging.Image(frame, new Vec2(300 - g, 134))!.Value;
            Assert.Equal(g, tip.G, 9);
            Assert.Equal(b, tip.B, 9);
            Assert.Equal(beta, tip.Magnification, 9);
            Assert.Equal(1 / F, 1 / tip.G + 1 / tip.B, 12);   // 1/f = 1/g + 1/b
            Assert.Equal(b > 0, tip.Real);
            var line = ImageArrow(r, doc, arrow);
            Near(new Vec2(300 + b, 150), line.A, "image foot on the axis");
            Near(new Vec2(300 + b, 150 - 16 * beta), line.B, "image tip");
            Assert.Equal(b > 0 ? LineRole.Visible : LineRole.Hidden, line.Style.Role ?? LineRole.Visible);
            Assert.Equal(ArrowHead.Triangle, line.Style.ArrowEnd);
        }

        [Fact]
        public void DivergingLensGivesAVirtualReducedImage()
        {
            var (r, doc, arrow) = LensScene(60, "optics.lens.biconcave", 30);
            var line = ImageArrow(r, doc, arrow);
            // 1/b = −1/30 − 1/60 → b = −20, β = 1/3.
            Near(new Vec2(280, 150), line.A, "foot");
            Near(new Vec2(280, 150 - 16.0 / 3), line.B, "tip");
            Assert.Equal(LineRole.Hidden, line.Style.Role);
        }

        [Fact]
        public void MirrorsImageOnTheirFrontAndBehindThem()
        {
            var r = Fx.Reg();
            var concave = Fx.Item(r, "optics.mirror.concave", new Vec2(300, 150)).WithParam("f", F);
            var vertex = r.PageAnchors(concave, new RenderContext(Fx.Doc(concave))).Single(a => a.Id == "o").P;
            // Object at 2f in front of a concave mirror: real, inverted image at 2f in front of it.
            var arrow = Fx.Item(r, "optics.object.arrow", new Vec2(vertex.X - 80, 142));
            var line = ImageArrow(r, Fx.Doc(concave, arrow), arrow);
            Near(new Vec2(vertex.X - 80, 150), line.A, "concave foot");
            Near(new Vec2(vertex.X - 80, 166), line.B, "concave tip");
            Assert.NotEqual(LineRole.Hidden, line.Style.Role ?? LineRole.Visible);

            // Convex mirror, g = f: virtual, upright, half size, f/2 behind the mirror.
            var convex = Fx.Item(r, "optics.mirror.convex", new Vec2(300, 150)).WithParam("f", F);
            var cv = r.PageAnchors(convex, new RenderContext(Fx.Doc(convex))).Single(a => a.Id == "o").P;
            var a2 = Fx.Item(r, "optics.object.arrow", new Vec2(cv.X - 40, 142));
            var l2 = ImageArrow(r, Fx.Doc(convex, a2), a2);
            Near(new Vec2(cv.X + 20, 150), l2.A, "convex foot");
            Near(new Vec2(cv.X + 20, 142), l2.B, "convex tip");
            Assert.Equal(LineRole.Hidden, l2.Style.Role);

            // A mirror turned with its back to the object images nothing.
            var back = concave with { Transform = concave.Transform with { FlipX = true } };
            Assert.DoesNotContain(r.Render(arrow, new RenderContext(Fx.Doc(back, arrow))), p => p.Tag == OpticsTags.Image);
        }

        [Fact]
        public void NoImageAtTheFocalPointWithoutALensOrWhenSwitchedOff()
        {
            var (r, doc, arrow) = LensScene(F);
            Assert.DoesNotContain(r.Render(arrow, new RenderContext(doc)), p => p.Tag == OpticsTags.Image);   // image at infinity
            var alone = Fx.Item(r, "optics.object.arrow", new Vec2(100, 142));
            Assert.Single(r.Render(alone, new RenderContext(Fx.Doc(alone))));   // just the arrow
            var (r2, d2, off) = LensScene(80, mode: "off");
            Assert.DoesNotContain(r2.Render(off, new RenderContext(d2)), p => p.Tag == OpticsTags.Image);
            Assert.DoesNotContain(r2.Render(off, new RenderContext(d2)), p => p.Tag == OpticsTags.Construction);
        }

        [Theory]
        [InlineData(80)]
        [InlineData(60)]
        public void ConstructionRaysMeetInTheRealImageTip(double g)
        {
            var (r, doc, arrow) = LensScene(g, mode: "construction");
            var frame = Imaging.Find(arrow, new RenderContext(doc))!;
            var tip = new Vec2(300 - g, 134);
            var image = Imaging.Image(frame, tip)!.Value;
            var imageTip = frame.After(image.Point);
            var rays = Imaging.ConstructionRays(frame, frame.Frame(tip), image);
            Assert.Equal(new[] { 0, 1, 2 }, rays.Select(x => x.Ray).Distinct().OrderBy(x => x));
            Assert.All(rays, x => Assert.NotEqual(LineRole.Hidden, ((LinePrim)x.Prim).Style.Role));   // real image: no virtual parts
            for (var k = 0; k < 3; k++)
            {
                var segs = rays.Where(x => x.Ray == k).Select(x => (LinePrim)x.Prim).ToList();
                Assert.Equal(2, segs.Count);
                Near(tip, segs[0].A, $"ray {k} starts at the object tip");
                Assert.Equal(300, segs[0].B.X, 9);                       // up to the lens plane
                var off = Math.Abs((imageTip - segs[1].A).Cross((segs[1].B - segs[1].A).Normalized()));
                Assert.True(off < Tol, $"ray {k} misses the image tip by {off}");
                Assert.True(segs[1].B.X > imageTip.X, $"ray {k} ends before the image");
            }
            // Parallel ray parallel before the lens, focal ray parallel after it, central ray straight through.
            var parallel = rays.Where(x => x.Ray == 0).Select(x => (LinePrim)x.Prim).ToList();
            Assert.Equal(134, parallel[0].B.Y, 9);
            var focal = rays.Where(x => x.Ray == 2).Select(x => (LinePrim)x.Prim).ToList();
            Assert.Equal(focal[1].A.Y, focal[1].B.Y, 9);
            Assert.True(Math.Abs((focal[0].B - focal[0].A).Normalized().Cross((new Vec2(300 - F, 150) - tip).Normalized())) < 1e-9, "focal ray misses F");
            // Rendered with the arrow, tagged as their own part.
            Assert.Equal(6, r.Render(arrow, new RenderContext(doc)).Count(p => p.Tag == OpticsTags.Construction));
            Assert.Contains(r.Parts(arrow, new RenderContext(doc)), p => p.Key == OpticsTags.Construction);
        }

        [Fact]
        public void ConstructionRaysOfAVirtualImageAreExtendedBackDashed()
        {
            var (_, doc, arrow) = LensScene(20, mode: "construction");
            var frame = Imaging.Find(arrow, new RenderContext(doc))!;
            var tip = new Vec2(280, 134);
            var image = Imaging.Image(frame, tip)!.Value;
            var imageTip = frame.After(image.Point);
            Near(new Vec2(260, 118), imageTip, "virtual image tip");
            var rays = Imaging.ConstructionRays(frame, frame.Frame(tip), image);
            var dashed = rays.Where(x => ((LinePrim)x.Prim).Style.Role == LineRole.Hidden).Select(x => (LinePrim)x.Prim).ToList();
            // Three backward extensions end in the virtual image; the focal ray comes from F in front of the object.
            Assert.Equal(3, dashed.Count(l => l.B.DistanceTo(imageTip) < Tol));
            Assert.Contains(dashed, l => l.A.DistanceTo(new Vec2(260, 150)) < Tol && l.B.DistanceTo(tip) < Tol);
        }

        [Fact]
        public void NamesAreOptionalAndBilingual()
        {
            var (r, doc, arrow) = LensScene(80);
            Assert.DoesNotContain(r.Render(arrow, new RenderContext(doc)), p => p.Tag == OpticsTags.Names);
            var named = arrow.WithParam("names", true);
            var d2 = Edits.Replace(doc, named);
            string Texts(Lang lang) => string.Join(",", r.Render(named, new RenderContext(d2, lang)).OfType<TextPrim>().Where(t => t.Tag == OpticsTags.Names).Select(t => t.Text));
            Assert.Equal("G,B", string.Join(",", Texts(Lang.De).Split(',').OrderBy(x => x == "B")));
            Assert.Equal("O,I", string.Join(",", Texts(Lang.En).Split(',').OrderBy(x => x == "I")));
            var actions = r.TypeOf(named).Actions(named, new RenderContext(d2)).ToList();
            Assert.Contains(actions, a => a.Id == "optics.names" && a.IsActive);
            Assert.Equal(3, actions.Count(a => a.Choice?.Key == "optics.image"));
        }
    }

    /// <summary>Live follow: rays and images are recomputed from the document whenever an element moves.</summary>
    public class LiveFollowTests
    {
        [Fact]
        public void MovingALensRetracesTheDockedBundleAndMovesTheImage()
        {
            var r = Fx.Reg();
            var lens = Fx.Item(r, "optics.lens.biconvex", new Vec2(300, 150)).WithParam("f", 40.0);
            var arrow = Fx.Item(r, "optics.object.arrow", new Vec2(220, 142));
            var (doc, rayId) = Fx.Bundle(Fx.Doc(lens, arrow), arrow, "tip");
            var ctx = new RenderContext(doc);
            // The lens names the ray and the arrow (it draws the image) as dependents, so hosts redraw both.
            var deps = r.TypeOf(lens).Dependents(lens, ctx).ToList();
            Assert.Contains(rayId, deps);
            Assert.Contains(arrow.Id, deps);
            Assert.True(r.TypeOf(doc.Get(rayId)!).DependsOnNeighbours);
            Assert.True(r.TypeOf(arrow).DependsOnNeighbours);
            // The lens renders on its own: it names dependents but does not read its neighbours, so a host keeps its shape
            // when it is moved (PowerPoint redraws only the rays and images).
            Assert.True(r.TypeOf(lens).HasDependents);
            Assert.False(r.TypeOf(lens).DependsOnNeighbours);
            // The arrow itself (no optical surface) and an arrow without image are no dependents of each other.
            Assert.DoesNotContain(arrow.Id, r.TypeOf(arrow).Dependents(arrow, ctx));
            var off = Edits.Replace(doc, arrow.WithParam("image", "off"));
            Assert.DoesNotContain(arrow.Id, r.TypeOf(lens).Dependents(lens, new RenderContext(off)));

            var before = Fx.Trace(doc, rayId);
            var imageBefore = Prim.Flatten(r.Render(arrow, ctx)).OfType<LinePrim>().Single(l => l.Tag == OpticsTags.Image);
            Assert.Equal(380, imageBefore.A.X, 6);

            // Move the lens 20 mm to the right (g = 100): the rays are re-traced through it, the image moves to b = 66.67.
            var moved = Edits.Move(doc, new[] { lens.Id }, new Vec2(20, 0));
            var after = Fx.Trace(moved, rayId);
            Assert.Equal(before.Paths.Count, after.Paths.Count);
            Assert.All(after.Paths, p => Assert.Equal(320, p.Points[1].X, 9));   // the rays now meet the lens at x = 320
            Assert.NotEqual(SvgRenderer.RenderPrimitives(r.Render(moved.Get(rayId)!, new RenderContext(moved))),
                SvgRenderer.RenderPrimitives(r.Render(doc.Get(rayId)!, ctx)));
            var imageAfter = Prim.Flatten(r.Render(moved.Get(arrow.Id)!, new RenderContext(moved))).OfType<LinePrim>().Single(l => l.Tag == OpticsTags.Image);
            var b = ThinLens.ImageDistance(40, 100);
            Assert.Equal(320 + b, imageAfter.A.X, 6);
            // The traced rays still meet in the image tip.
            var tip = imageAfter.B;
            foreach (var p in after.Paths)
            {
                var (s, d) = Fx.Last(p);
                Assert.True(Math.Abs((tip - s).Cross(d)) < 1e-6, "ray misses the moved image");
            }
        }

        [Fact]
        public void ChangingTheFocalLengthByItsGripRetracesTheRays()
        {
            var r = Fx.Reg();
            var lens = Fx.Item(r, "optics.lens.biconvex", new Vec2(200, 150)).WithParam("f", 30.0);
            var (doc, rayId) = Fx.Bundle(Fx.Doc(lens), lens, "o");
            var moved = r.MoveHandle(lens, "f.back", new Vec2(250, 150), new RenderContext(doc));
            var after = Edits.Replace(doc, moved);
            foreach (var path in Fx.Trace(after, rayId).Paths.Where(p => Math.Abs(p.Points[0].Y - 150) > 1e-9))
            {
                var (p, d) = Fx.Last(path);
                Assert.True(Fx.Intersect(p, d, new Vec2(0, 150), new Vec2(1, 0)).DistanceTo(new Vec2(250, 150)) < 1e-6);
            }
        }
    }

    public class ImagePersistenceTests
    {
        [Fact]
        public void ImageSettingsSurviveAJsonRoundTrip()
        {
            var r = Fx.Reg();
            var lens = Fx.Item(r, "optics.lens.biconvex", new Vec2(300, 150)).WithParam("f", 40.0).WithParam("focal", true);
            var arrow = Fx.Item(r, "optics.object.arrow", new Vec2(240, 142)).WithParam("image", "construction").WithParam("names", true);
            var ray = Fx.Ray(new Vec2(100, 260), new Vec2(140, 250));
            var (doc, _) = Fx.Bundle(Fx.Doc(lens, arrow, ray), arrow, "tip");
            var json = DocumentJson.Serialize(doc);
            var back = DocumentJson.Deserialize(json);
            Assert.Equal(json, DocumentJson.Serialize(back));
            Assert.Equal(SvgRenderer.RenderPage(r, doc, doc.Pages[0]), SvgRenderer.RenderPage(r, back, back.Pages[0]));
            var a = back.Get(arrow.Id)!;
            Assert.Equal("construction", a.Str("image", ""));
            Assert.True(a.Flag("names", false));
            Assert.Contains(r.Render(a, new RenderContext(back)), p => p.Tag == OpticsTags.Image);
            // Grips are recomputed from the stored state alone.
            var ctx = new RenderContext(back);
            Assert.Equal(r.DragHandles(lens, new RenderContext(doc)).Select(h => h.Key + h.Position), r.DragHandles(back.Get(lens.Id)!, ctx).Select(h => h.Key + h.Position));
        }
    }
}
