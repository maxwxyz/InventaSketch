using System;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Optics.Tests
{
    public class TraceTests
    {
        private const double Tol = 1e-6;

        [Fact]
        public void ThinLensEquation()
        {
            Assert.Equal(80, ThinLens.ImageDistance(40, 80), 9);
            Assert.Equal(-1, ThinLens.Magnification(40, 80), 9);
            Assert.Equal(120, ThinLens.ImageDistance(40, 60), 9);
            Assert.True(double.IsInfinity(ThinLens.ImageDistance(40, 40)));
            Assert.Equal(-20, ThinLens.ImageDistance(-20, 1e12), 6);
        }

        [Fact]
        public void ObjectAtTwiceTheFocalLengthIsImagedAtTwiceTheFocalLength()
        {
            var r = Fx.Reg();
            const double f = 40;
            var lens = Fx.Item(r, "optics.lens.biconvex", new Vec2(300, 150)).WithParam("f", f);
            // Object arrow 16 mm high standing on the axis (foot at y = 150), 2f in front of the lens.
            var obj = Fx.Item(r, "optics.object.arrow", new Vec2(300 - 2 * f, 150 - 8));
            var (doc, rayId) = Fx.Bundle(Fx.Doc(lens, obj), obj, "tip");
            var result = Fx.Trace(doc, rayId);
            Assert.Equal(3, result.Paths.Count);   // parallel, central and focal ray
            var image = new Vec2(300 + 2 * f, 150 + 16);   // real, inverted, same size
            for (var i = 0; i < 3; i++)
            {
                var (p, d) = Fx.Last(result.Paths[i]);
                var off = Math.Abs((image - p).Cross(d));
                Assert.True(off < Tol, $"ray {i} misses the image point by {off}");
            }
            // The automatic length lets the rays reach the image.
            Assert.All(result.Paths, p => Assert.True(p.Last.X >= image.X - 1e-6, "ray ends before the image: " + p.Last));
        }

        [Fact]
        public void ParallelBundleMeetsInTheBackFocalPoint()
        {
            var r = Fx.Reg();
            var lens = Fx.Item(r, "optics.lens.planoconvex", new Vec2(200, 150)).WithParam("f", 50.0);
            var (doc, rayId) = Fx.Bundle(Fx.Doc(lens), lens, "o");
            var result = Fx.Trace(doc, rayId);
            Assert.Equal(5, result.Paths.Count);
            foreach (var path in result.Paths.Where(p => Math.Abs(p.Points[0].Y - 150) > 1e-9))
            {
                var (p, d) = Fx.Last(path);
                var x = Fx.Intersect(p, d, new Vec2(0, 150), new Vec2(1, 0));
                Assert.True(x.DistanceTo(new Vec2(250, 150)) < Tol, "crosses the axis at " + x);
            }
        }

        [Fact]
        public void DivergingLensDrawsVirtualRaysBackToItsFocalPoint()
        {
            var r = Fx.Reg();
            var lens = Fx.Item(r, "optics.lens.biconcave", new Vec2(200, 150)).WithParam("f", 30.0);
            var (doc, rayId) = Fx.Bundle(Fx.Doc(lens), lens, "o");
            var result = Fx.Trace(doc, rayId);
            var virtuals = result.Paths.SelectMany(p => p.Virtual).ToList();
            Assert.Equal(4, virtuals.Count);   // the axial ray is not deflected
            Assert.All(virtuals, v => Assert.True(v.B.DistanceTo(new Vec2(170, 150)) < Tol, "virtual ray ends at " + v.B));
            var prims = r.Render(doc.Get(rayId)!, new RenderContext(doc));
            Assert.Contains(prims, p => p.Tag?.StartsWith(OpticsTags.Virtual) == true && p is LinePrim l && l.Style.Role == LineRole.Hidden);
        }

        [Theory]
        [InlineData(45)]
        [InlineData(30)]
        [InlineData(-20)]
        public void PlaneMirrorReflectsWithEqualAngles(double rotation)
        {
            var r = Fx.Reg();
            var mirror = Fx.Item(r, "optics.mirror.plane", new Vec2(200, 150)) with { Transform = new Transform(200, 150, rotation) };
            var ray = Fx.Ray(new Vec2(100, 150), new Vec2(150, 150));
            var doc = Fx.Doc(mirror, ray);
            var path = Fx.Trace(doc, ray.Id).Paths.Single();
            Assert.Equal(3, path.Points.Count);
            var din = Fx.Segment(path, 0).D;
            var dout = Fx.Segment(path, 1).D;
            var n = mirror.Matrix().ApplyLinear(new Vec2(1, 0)).Normalized();   // mirror normal (local x)
            Assert.Equal(din.Dot(n), -dout.Dot(n), 9);                          // angle of incidence = angle of reflection
            Assert.Equal(din.Cross(n), dout.Cross(n), 9);                       // the component along the mirror stays
            if (rotation == 45) Assert.True(dout.DistanceTo(new Vec2(0, -1)) < Tol, "45° mirror sends the ray up: " + dout);
        }

        [Fact]
        public void PlaneMirrorBackStopsTheRay()
        {
            var r = Fx.Reg();
            var mirror = Fx.Item(r, "optics.mirror.plane", new Vec2(200, 150)) with { Transform = new Transform(200, 150, 0, FlipX: true) };
            var ray = Fx.Ray(new Vec2(100, 150), new Vec2(150, 150));
            var doc = Fx.Doc(mirror, ray);
            var path = Fx.Trace(doc, ray.Id).Paths.Single();
            // Flipped, the reflecting side faces away: light from the left meets the back. Unflipped it reflects.
            Assert.True(path.Absorbed);
            Assert.False(Fx.Trace(Fx.Doc(mirror with { Transform = new Transform(200, 150) }, ray), ray.Id).Paths.Single().Absorbed);
        }

        [Fact]
        public void ConcaveMirrorFocusesParallelRaysInItsFocalPoint()
        {
            var r = Fx.Reg();
            var mirror = Fx.Item(r, "optics.mirror.concave", new Vec2(200, 150)).WithParam("f", 40.0);
            var doc = Fx.Doc(mirror);
            var vertex = r.PageAnchors(mirror, new RenderContext(doc)).Single(a => a.Id == "o").P;
            var focus = r.PageAnchors(mirror, new RenderContext(doc)).Single(a => a.Id == "f").P;
            Assert.Equal(vertex.X - 40, focus.X, 9);
            foreach (var y in new[] { -12.0, -5, 4, 15 })
            {
                var ray = Fx.Ray(new Vec2(100, 150 + y), new Vec2(120, 150 + y));
                var d2 = Fx.Add(doc, ray);
                var path = Fx.Trace(d2, ray.Id).Paths.Single();
                var (p, d) = Fx.Last(path);
                Assert.True(d.X < 0, "reflected back");
                var x = Fx.Intersect(p, d, new Vec2(0, 150), new Vec2(1, 0));
                Assert.True(x.DistanceTo(focus) < Tol, $"y {y}: crosses the axis at {x}, focus {focus}");
            }
        }

        [Fact]
        public void KeplerTelescopeTurnsAParallelBundleIntoAParallelBundle()
        {
            var r = Fx.Reg();
            const double f1 = 60, f2 = 20;
            var objective = Fx.Item(r, "optics.lens.biconvex", new Vec2(150, 150)).WithParam("f", f1);
            var eyepiece = Fx.Item(r, "optics.lens.biconvex", new Vec2(150 + f1 + f2, 150)).WithParam("f", f2);
            var (doc, rayId) = Fx.Bundle(Fx.Doc(objective, eyepiece), objective, "o");
            var result = Fx.Trace(doc, rayId);
            Assert.Equal(5, result.Paths.Count);
            foreach (var path in result.Paths)
            {
                Assert.Equal(4, path.Points.Count);   // start, objective, eyepiece, end
                var hIn = path.Points[1].Y - 150;
                var (p, d) = Fx.Last(path);
                Assert.True(Math.Abs(d.Y) < Tol && d.X > 0, "leaves parallel to the axis: " + d);
                Assert.Equal(-hIn * f2 / f1, p.Y - 150, 6);   // angular magnification f1/f2, image inverted
            }
        }

        [Fact]
        public void PrismAtMinimumDeviationFollowsSnellsLaw()
        {
            var r = Fx.Reg();
            const double n = 1.5;
            // Equilateral prism (apex 60°) around (200, 150).
            var prism = Fx.Item(r, "optics.prism", new Vec2(200, 150)) with { Size = new Size2(30, 15 * Math.Sqrt(3)) };
            prism = prism.WithParam("n", n);
            var mid = new Vec2(200 - 7.5, 150);   // middle of the left face
            var incidence = Math.Asin(n * Math.Sin(Math.PI / 6));   // 48.59°: the ray passes parallel to the base
            var dir = new Vec2(Math.Cos(Math.PI / 6 - incidence), Math.Sin(Math.PI / 6 - incidence));
            var ray = Fx.Ray(mid - dir * 60, mid - dir * 30);
            var doc = Fx.Doc(prism, ray);
            var path = Fx.Trace(doc, ray.Id).Paths.Single();
            Assert.Equal(4, path.Points.Count);
            Assert.True(path.Points[1].DistanceTo(mid) < Tol);
            var inside = Fx.Segment(path, 1).D;
            Assert.True(Math.Abs(inside.Y) < 1e-9, "inside the prism the ray runs parallel to the base: " + inside);
            var dmin = 2 * Math.Asin(n * Math.Sin(Math.PI / 6)) - Math.PI / 3;   // 37.18°
            var deviation = Math.Acos(Fx.Segment(path, 0).D.Dot(Fx.Segment(path, 2).D));
            Assert.Equal(dmin, deviation, 9);
        }

        [Fact]
        public void RightAnglePrismTurnsTheBeamByTotalInternalReflection()
        {
            var r = Fx.Reg();
            var prism = Fx.Item(r, "optics.prism.rightangle", new Vec2(200, 150));
            var ray = Fx.Ray(new Vec2(150, 148), new Vec2(170, 148));
            var path = Fx.Trace(Fx.Doc(prism, ray), ray.Id).Paths.Single();
            var (_, d) = Fx.Last(path);
            Assert.True(d.DistanceTo(new Vec2(0, 1)) < Tol, "leaves downwards: " + d);
        }

        [Fact]
        public void DispersingPrismSplitsWhiteLightBlueBendingMost()
        {
            var r = Fx.Reg();
            var prism = Fx.Item(r, "optics.prism.dispersing", new Vec2(200, 150));
            var ray = Fx.Ray(new Vec2(120, 165), new Vec2(160, 152));
            var doc = Fx.Doc(prism, ray);
            var result = Fx.Trace(doc, ray.Id);
            Assert.True(result.Dispersed);
            var colours = result.Paths.Skip(1).ToList();
            Assert.Equal(Spectrum.Lines.Length, colours.Count);
            var angles = colours.Select(p => Fx.Deg(Fx.Last(p).D)).ToList();
            // Red (656 nm) first … violet (436 nm) last: each shorter wavelength is deviated more (turned further down).
            for (var i = 1; i < angles.Count; i++) Assert.True(angles[i] > angles[i - 1], string.Join(", ", angles));
            var prims = r.Render(doc.Get(ray.Id)!, new RenderContext(doc));
            var strokes = Prim.Flatten(prims).OfType<PathPrim>().Select(p => p.Style.Stroke).Concat(Prim.Flatten(prims).OfType<LinePrim>().Select(l => l.Style.Stroke)).Distinct().ToList();
            Assert.True(strokes.Count >= 6, "white plus five colours: " + string.Join(", ", strokes));
        }

        [Fact]
        public void BeamSplitterTransmitsAndReflects()
        {
            var r = Fx.Reg();
            var cube = Fx.Item(r, "optics.splitter.cube", new Vec2(200, 150));
            var ray = Fx.Ray(new Vec2(100, 150), new Vec2(150, 150));
            var result = Fx.Trace(Fx.Doc(cube, ray), ray.Id);
            Assert.Equal(2, result.Paths.Count);
            Assert.True(Fx.Last(result.Paths[0]).D.DistanceTo(new Vec2(1, 0)) < Tol);
            Assert.True(Fx.Last(result.Paths[1]).D.DistanceTo(new Vec2(0, -1)) < Tol);
            Assert.True(result.Paths[1].Points[0].DistanceTo(new Vec2(200, 150)) < Tol);
        }

        [Fact]
        public void StopPassesTheCentreAndBlocksTheEdge()
        {
            var r = Fx.Reg();
            var stop = Fx.Item(r, "optics.stop.aperture", new Vec2(200, 150));
            var near = Fx.Ray(new Vec2(100, 153), new Vec2(150, 153));
            var far = Fx.Ray(new Vec2(100, 160), new Vec2(150, 160));
            var doc = Fx.Doc(stop, near, far);
            Assert.False(Fx.Trace(doc, near.Id).Paths.Single().Absorbed);
            var blocked = Fx.Trace(doc, far.Id).Paths.Single();
            Assert.True(blocked.Absorbed);
            Assert.Equal(200, blocked.Last.X, 9);
        }

        [Fact]
        public void GratingSendsOrdersAtTheGratingEquationAngles()
        {
            var r = Fx.Reg();
            var laser = Fx.Item(r, "optics.source.laser.green", new Vec2(100, 150));
            var grating = Fx.Item(r, "optics.grating", new Vec2(200, 150));
            var (doc, rayId) = Fx.Bundle(Fx.Doc(laser, grating), laser, "exit");
            var result = Fx.Trace(doc, rayId);
            Assert.Equal(3, result.Paths.Count);
            var sines = result.Paths.Select(p => Fx.Last(p).D.Y).OrderBy(x => x).ToList();
            var s1 = Math.Sin(20 * Math.PI / 180) * 532 / 550;
            Assert.Equal(-s1, sines[0], 9);
            Assert.Equal(0, sines[1], 9);
            Assert.Equal(s1, sines[2], 9);
        }

        [Fact]
        public void ColourFilterColoursWhiteLightAndBlocksOtherColours()
        {
            var r = Fx.Reg();
            var filter = Fx.Item(r, "optics.filter.red", new Vec2(200, 150));
            var white = Fx.Ray(new Vec2(100, 150), new Vec2(150, 150));
            var doc = Fx.Doc(filter, white);
            var path = Fx.Trace(doc, white.Id).Paths.Single();
            Assert.Equal(0, path.Lambdas[0]);
            Assert.Equal(650, path.Lambdas[path.Lambdas.Count - 1]);
            var blue = Fx.Item(r, "optics.source.laser", new Vec2(100, 150)).WithParam("color", "blue");
            var (d2, rid) = Fx.Bundle(Fx.Doc(filter, blue), blue, "exit");
            Assert.True(Fx.Trace(d2, rid).Paths.Single().Absorbed);
        }

        [Fact]
        public void EyeFocusesParallelLightOnTheRetina()
        {
            var r = Fx.Reg();
            var eye = Fx.Item(r, "optics.detector.eye", new Vec2(200, 150));
            var doc = Fx.Doc(eye);
            var ends = new[] { -1.5, 0.0, 1.5 }.Select(y =>
            {
                var ray = Fx.Ray(new Vec2(120, 150 + y), new Vec2(140, 150 + y));
                var path = Fx.Trace(Fx.Add(doc, ray), ray.Id).Paths.Single();
                Assert.True(path.Absorbed);
                return path.Last;
            }).ToList();
            // All three end close together at the back of the eyeball.
            Assert.True(ends.Max(p => p.DistanceTo(ends[1])) < 0.3, string.Join(", ", ends));
            Assert.True(ends[1].X > 200, "on the retina at the back: " + ends[1]);
        }

        [Fact]
        public void ScreenAndCameraEndTheRays()
        {
            var r = Fx.Reg();
            foreach (var id in new[] { "optics.screen", "optics.detector.camera", "optics.detector.photodiode" })
            {
                var det = Fx.Item(r, id, new Vec2(200, 150));
                var ray = Fx.Ray(new Vec2(100, 150.5), new Vec2(150, 150.5));
                var path = Fx.Trace(Fx.Doc(det, ray), ray.Id).Paths.Single();
                Assert.True(path.Absorbed, id);
                Assert.True(path.Last.X < 200 + det.Size.W / 2 + 1e-9, id);
            }
        }

        [Fact]
        public void RaysFollowTheElementsLive()
        {
            var r = Fx.Reg();
            var laser = Fx.Item(r, "optics.source.laser", new Vec2(100, 150));
            var mirror = Fx.Item(r, "optics.mirror.plane", new Vec2(200, 150)) with { Transform = new Transform(200, 150, 45) };
            var (doc, rayId) = Fx.Bundle(Fx.Doc(laser, mirror), laser, "exit");
            var ctx = new RenderContext(doc);
            Assert.Contains(rayId, r.TypeOf(mirror).Dependents(mirror, ctx));
            Assert.Contains(rayId, r.TypeOf(laser).Dependents(laser, ctx));
            Assert.True(r.TypeOf(doc.Get(rayId)!).DependsOnNeighbours);
            var before = SvgRenderer.RenderPrimitives(r.Render(doc.Get(rayId)!, ctx));
            // Turning the mirror changes the reflected ray; moving the laser moves the docked start along.
            var turned = Edits.Rotate(doc, new[] { mirror.Id }, new Vec2(200, 150), 10);
            Assert.NotEqual(before, SvgRenderer.RenderPrimitives(r.Render(turned.Get(rayId)!, new RenderContext(turned))));
            var moved = Edits.Move(doc, new[] { laser.Id }, new Vec2(0, 5));
            var start = moved.ResolvePoints(moved.Get(rayId)!)[0];
            Assert.True(start.DistanceTo(new Vec2(100 + laser.Size.W / 2, 155)) < Tol, start.ToString());
            Assert.Equal(155, Fx.Trace(moved, rayId).Paths.Single().Points[1].Y, 9);
        }

        [Fact]
        public void BundleActionAddsAndRemovesTheDockedRays()
        {
            var r = Fx.Reg();
            var lamp = Fx.Item(r, "optics.source.lamp", new Vec2(100, 150));
            var lens = Fx.Item(r, "optics.lens.biconvex", new Vec2(180, 150));
            var doc = Fx.Doc(lamp, lens);
            var add = r.TypeOf(lamp).Actions(lamp, new RenderContext(doc)).Single(a => a.Id == "optics.trace");
            Assert.False(add.IsActive);
            var withRays = ContextActions.Run(r, doc, new[] { lamp.Id }, "optics.trace", null, Lang.En);
            var ray = withRays.Objects.Values.Single(o => o.Type == RayType.TypeId);
            Assert.Equal(lamp.Id, ray.Attach![0]!.ObjectId);
            Assert.Equal("exit", ray.Attach[0]!.AnchorId);
            Assert.Contains(ray.Id, withRays.Pages[0].Order);
            // The automatic cone fills the lens: every ray passes through it.
            var paths = Fx.Trace(withRays, ray.Id).Paths;
            Assert.Equal(5, paths.Count);
            Assert.All(paths, p => Assert.True(p.Points.Count >= 3 && Math.Abs(p.Points[1].X - 180) < 1e-9, "misses the lens"));
            var remove = r.TypeOf(lamp).Actions(lamp, new RenderContext(withRays)).Single(a => a.Id == "optics.trace");
            Assert.True(remove.IsActive);
            var without = ContextActions.Run(r, withRays, new[] { lamp.Id }, "optics.trace", null, Lang.En);
            Assert.DoesNotContain(without.Objects.Values, o => o.Type == RayType.TypeId);
        }

        [Fact]
        public void FreeRayWithoutElementsEndsAtItsSecondPoint()
        {
            var r = Fx.Reg();
            var ray = Fx.Ray(new Vec2(100, 150), new Vec2(117, 160));
            var path = Fx.Trace(Fx.Doc(ray), ray.Id).Paths.Single();
            Assert.Equal(2, path.Points.Count);
            Assert.True(path.Last.DistanceTo(new Vec2(117, 160)) < Tol);
            var free = Fx.Item(r, "optics.ray.free", new Vec2(100, 150));
            var prims = r.Render(free, new RenderContext(Fx.Doc(free)));
            Assert.Contains(prims, p => p is PathPrim pp && pp.Segments.Count == 3);
        }

        [Fact]
        public void HiddenLayersDoNotTakePart()
        {
            var r = Fx.Reg();
            var screen = Fx.Item(r, "optics.screen", new Vec2(200, 150)) with { Layer = "off" };
            var ray = Fx.Ray(new Vec2(100, 150), new Vec2(150, 150));
            var doc = Fx.Doc(screen, ray) with { Layers = new[] { new Layer("default", "Default"), new Layer("off", "Off", Visible: false) } };
            Assert.False(Fx.Trace(doc, ray.Id).Paths.Single().Absorbed);
        }
    }

    public class PersistenceTests
    {
        private static SketchDocument Scene(Registry r)
        {
            var lamp = Fx.Item(r, "optics.source.lamp", new Vec2(80, 150));
            var lens = Fx.Item(r, "optics.lens.biconvex", new Vec2(160, 150)).WithParam("focal", true).WithParam("label", "Objective lens");
            var prism = Fx.Item(r, "optics.prism.dispersing", new Vec2(260, 150));
            var axis = Fx.Item(r, "optics.axis", new Vec2(160, 150));
            var (doc, _) = Fx.Bundle(Fx.Doc(axis, lamp, lens, prism), lamp, "exit");
            return doc;
        }

        [Fact]
        public void JsonRoundTripKeepsEverything()
        {
            var r = Fx.Reg();
            var doc = Scene(r);
            var json = DocumentJson.Serialize(doc);
            var back = DocumentJson.Deserialize(json);
            Assert.Equal(json, DocumentJson.Serialize(back));
            Assert.Equal(SvgRenderer.RenderPage(r, doc, doc.Pages[0]), SvgRenderer.RenderPage(r, back, back.Pages[0]));
            var ray = back.Objects.Values.Single(o => o.Type == RayType.TypeId);
            Assert.Equal("exit", ray.Attach![0]!.AnchorId);
            Assert.True(Fx.Trace(back, ray.Id).Paths.Count >= 5);
        }

        [Fact]
        public void SanitizeReplacesOnlyLabels()
        {
            var r = Fx.Reg();
            var doc = Scene(r);
            var (clean, report) = Sanitizer.Apply(r, doc, doc.Objects.Keys, new SanitizeOptions { ReplaceTexts = true, Seed = 7 });
            var lens = clean.Objects.Values.Single(o => o.Type == LensType.ConvergingId);
            Assert.NotEqual("Objective lens", lens.Str("label", "Objective lens"));
            Assert.Equal(40, lens.Num("f", 0));
            var prism = clean.Objects.Values.Single(o => o.Type == PrismType.TypeId);
            Assert.Equal(1.62, prism.Num("n", 0), 9);
            Assert.Equal(1, report.Texts);
        }
    }
}
