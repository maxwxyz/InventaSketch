using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Optics.Tests
{
    /// <summary>Thick lens, scanner mirror, light guide, polariser, wave plate and spectrometer.</summary>
    public class NewElementTests
    {
        private const double Tol = 1e-6;

        private static TracedPath FreeRay(SketchDocument doc, Vec2 a, Vec2 b, out SketchDocument withRay)
        {
            var ray = Fx.Ray(a, b);
            withRay = Fx.Add(doc, ray);
            return Fx.Trace(withRay, ray.Id).Paths.First();
        }

        // ------------------------------------------------------------------ thick lens

        [Theory]
        [InlineData("biconvex", 1.5)]
        [InlineData("biconvex", 1.7)]
        [InlineData("planoconvex", 1.5)]
        public void ThickLensFocusesWhereTheLensmakerEquationSays(string shape, double n)
        {
            var r = Fx.Reg();
            var lens = Fx.Item(r, "optics.lens.thick", new Vec2(200, 150)).WithParam("shape", shape).WithParam("n", n);
            Assert.Equal(new Size2(10, 34), lens.Size);
            // Independent geometry: a face of sag s over the half aperture h has R = (s² + h²) / (2 s).
            const double w = 10, h = 17, share = 0.35;
            double r1, r2;
            if (shape == "biconvex")
            {
                var sag = share * w;
                r1 = (sag * sag + h * h) / (2 * sag);
                r2 = -r1;
            }
            else
            {
                var sag = 2 * share * w;
                r1 = double.PositiveInfinity;
                r2 = -(sag * sag + h * h) / (2 * sag);
            }
            // Lensmaker's equation for a thick lens (d = centre thickness = box width).
            var power = (n - 1) * (1 / r1 - 1 / r2 + (n - 1) * w / (n * r1 * r2));
            var f = 1 / power;
            var bfd = f * (1 - (n - 1) * w / (n * r1));
            Assert.Equal(f, ThickLens.FocalLength(n, r1, r2, w), 9);
            Assert.Equal(bfd, ThickLens.BackFocalDistance(n, r1, r2, w), 9);

            // A paraxial ray 0.3 mm above the axis, refracted at both spherical faces.
            const double y = 0.3;
            var path = FreeRay(Fx.Doc(lens), new Vec2(100, 150 - y), new Vec2(120, 150 - y), out _);
            Assert.Equal(4, path.Points.Count);   // start, front face, back face, end
            Assert.Equal(195, path.Points[1].X, 1);
            var (p, d) = Fx.Last(path);
            var cross = Fx.Intersect(p, d, new Vec2(0, 150), new Vec2(1, 0));
            var tracedBfd = cross.X - 205;
            var tracedF = y / (d.Y / d.X);
            Assert.True(Math.Abs(tracedBfd - bfd) / bfd < 0.002, $"back focal distance {tracedBfd} vs lensmaker {bfd}");
            Assert.True(Math.Abs(tracedF - f) / f < 0.002, $"focal length {tracedF} vs lensmaker {f}");

            // The focal point anchor sits there too.
            var f2 = r.PageAnchors(lens, new RenderContext(Fx.Doc(lens))).Single(a => a.Id == "f2").P;
            Assert.Equal(205 + bfd, f2.X, 6);
        }

        [Fact]
        public void ThickConcaveLensDivergesAndScalingChangesItsFocalLength()
        {
            var r = Fx.Reg();
            var lens = Fx.Item(r, "optics.lens.thick", new Vec2(200, 150)).WithParam("shape", "biconcave");
            var g = ThickLensType.Of(lens);
            Assert.True(g.F < 0, "diverging: " + g.F);
            var path = FreeRay(Fx.Doc(lens), new Vec2(100, 145), new Vec2(120, 145), out _);
            Assert.True(Fx.Last(path).D.Y < -1e-6, "leaves away from the axis");
            // The radii follow the box: a taller lens of the same width has flatter faces and a longer focal length.
            var tall = lens with { Size = new Size2(10, 60) };
            Assert.True(Math.Abs(ThickLensType.Of(tall).F) > Math.Abs(g.F));
        }

        [Fact]
        public void ThickLensBundleAndFocalMarks()
        {
            var r = Fx.Reg();
            var lens = Fx.Item(r, "optics.lens.thick", new Vec2(200, 150)).WithParam("focal", true);
            var (doc, rayId) = Fx.Bundle(Fx.Doc(lens), lens, "o");
            var result = Fx.Trace(doc, rayId);
            Assert.Equal(5, result.Paths.Count);
            Assert.All(result.Paths, p => Assert.False(p.Absorbed));
            var prims = r.Render(lens, new RenderContext(doc));
            var names = Prim.Flatten(prims).OfType<TextPrim>().Select(t => t.Text).ToList();
            Assert.Contains("F", names);
            Assert.Contains("F′", names);
            Assert.Contains("H′", names);
            var beam = r.DragHandles(lens, new RenderContext(doc)).Single(h => h.Key == "beam");
            Assert.Equal(30, r.MoveHandle(lens, "beam", beam.Position + new Vec2(0, -(15 - 0.7 * 17)), new RenderContext(doc)).Num("beam", 0), 9);
        }

        // ------------------------------------------------------------------ scanner mirror

        private static Vec2 ScannerOut(Registry r, double angle, bool fan = false)
        {
            var scanner = Fx.Item(r, "optics.mirror.scanner", new Vec2(200, 150)).WithParam("angle", angle).WithParam("fan", fan);
            var path = FreeRay(Fx.Doc(scanner), new Vec2(100, 150), new Vec2(150, 150), out _);
            Assert.Equal(3, path.Points.Count);
            Assert.True(path.Points[1].DistanceTo(new Vec2(200, 150)) < Tol, "hits the pivot: " + path.Points[1]);
            return Fx.Last(path).D;
        }

        [Theory]
        [InlineData(30)]
        [InlineData(40)]
        [InlineData(52)]
        [InlineData(60)]
        public void ScannerMirrorDeflectsByTwiceTheMirrorAngle(double angle)
        {
            var r = Fx.Reg();
            var at45 = ScannerOut(r, 45);
            Assert.True(at45.DistanceTo(new Vec2(0, -1)) < Tol, "45° sends the beam up: " + at45);
            var turned = ScannerOut(r, angle);
            var delta = Fx.Deg(turned) - Fx.Deg(at45);
            Assert.Equal(2 * (angle - 45), delta, 9);
        }

        [Fact]
        public void ScannerFanShowsTheRaysAtBothEndsOfTheScanRange()
        {
            var r = Fx.Reg();
            var scanner = Fx.Item(r, "optics.mirror.scanner.fan", new Vec2(200, 150));
            Assert.True(scanner.Flag("fan", false));
            var ray = Fx.Ray(new Vec2(100, 150), new Vec2(150, 150));
            var doc = Fx.Doc(scanner, ray);
            var result = Fx.Trace(doc, ray.Id);
            Assert.Equal(3, result.Paths.Count);
            Assert.False(result.Paths[0].Fan);
            var main = Fx.Deg(Fx.Last(result.Paths[0]).D);
            var fan = result.Paths.Skip(1).Select(p => { Assert.True(p.Fan); return Fx.Deg(Fx.Last(p).D) - main; }).OrderBy(x => x).ToList();
            Assert.Equal(-20, fan[0], 9);   // optical scan range ±2 × 10° mechanical
            Assert.Equal(20, fan[1], 9);
            var prims = Prim.Flatten(r.Render(doc.Get(ray.Id)!, new RenderContext(doc))).ToList();
            Assert.Contains(prims, p => p is LinePrim l && l.Style.Role == LineRole.Hidden);
            // Without the option there is no fan.
            Assert.Single(Fx.Trace(Fx.Doc(scanner.WithParam("fan", false), ray), ray.Id).Paths);
        }

        [Fact]
        public void ScannerAngleGripTurnsTheMirror()
        {
            var r = Fx.Reg();
            var scanner = Fx.Item(r, "optics.mirror.scanner", new Vec2(200, 150));
            var doc = Fx.Doc(scanner);
            var g = Assert.Single(r.DragHandles(scanner, new RenderContext(doc)));
            Assert.Equal("angle", g.Key);
            Assert.Equal("Mirror angle: 45°", g.Label.En);
            Assert.Equal("Spiegelwinkel: 45°", g.Label.De);
            // The grip sits on the upper right end of the mirror, on the 45° line through the pivot.
            Assert.True(g.Position.X > 200 && g.Position.Y < 150 && Math.Abs((g.Position.X - 200) + (g.Position.Y - 150)) < 1e-9, g.Position.ToString());
            var moved = r.MoveHandle(scanner, "angle", new Vec2(205, 140), new RenderContext(doc));
            Assert.Equal(27, moved.Num("angle", 0), 9);   // atan(5/10) = 26.6° → 27°
            Assert.Equal(scanner.Size, moved.Size);
            Assert.Equal(scanner.Transform, moved.Transform);
            // Below the pivot the line is the same: the angle folds into −90 … 90°.
            Assert.Equal(27, r.MoveHandle(scanner, "angle", new Vec2(195, 160), new RenderContext(doc)).Num("angle", 0), 9);
        }

        // ------------------------------------------------------------------ light guide

        [Theory]
        [InlineData(60)]
        [InlineData(50)]
        [InlineData(42.5)]
        public void LightGuideReflectsTotallyAboveTheCriticalAngle(double incidence)
        {
            var r = Fx.Reg();
            var guide = Fx.Item(r, "optics.lightguide", new Vec2(200, 150));
            Assert.Equal(41.81, LightGuideType.CriticalAngle(1.5), 2);
            Assert.True(incidence > LightGuideType.CriticalAngle(1.5));
            // From inside the glass towards the top face, at the given angle from its normal.
            var a = (90 - incidence) * Math.PI / 180;
            var start = new Vec2(170, 150);
            var path = FreeRay(Fx.Doc(guide), start, start + new Vec2(Math.Cos(a), -Math.Sin(a)) * 3, out _);
            var top = Fx.Segment(path, 1);
            Assert.Equal(146, path.Points[1].Y, 9);   // reflected on the top face …
            Assert.True(top.D.Y > 0, "… back into the glass: " + top.D);
            Assert.Equal(Math.Cos(a), top.D.X, 9);
            // Guided to the far end: every reflection stays on the faces, the ray leaves through the end face.
            var inside = path.Points.Skip(1).TakeWhile(p => p.X < 240 - 1e-9).ToList();
            Assert.All(inside, p => Assert.True(Math.Abs(p.Y - 150) < 4 + 1e-9, p.ToString()));
            Assert.Contains(path.Points, p => Math.Abs(p.X - 240) < 1e-9);
            // It leaves through the end face unless it meets that face beyond the critical angle too (then it stays trapped).
            if (1.5 * Math.Sin(a) < 1) Assert.True(Fx.Last(path).P.X >= 240 - 1e-9, "leaves at the far end");
        }

        [Fact]
        public void LightGuideLetsLightOutBelowTheCriticalAngle()
        {
            var r = Fx.Reg();
            var guide = Fx.Item(r, "optics.lightguide", new Vec2(200, 150));
            const double incidence = 30;
            var a = (90 - incidence) * Math.PI / 180;
            var start = new Vec2(170, 150);
            var path = FreeRay(Fx.Doc(guide), start, start + new Vec2(Math.Cos(a), -Math.Sin(a)) * 3, out _);
            var (p, d) = Fx.Last(path);
            Assert.Equal(146, p.Y, 9);
            Assert.True(d.Y < 0, "refracted out through the top face");
            // Snell: sin θt = n · sin θi = 1.5 · 0.5.
            Assert.Equal(0.75, d.X, 9);
        }

        [Fact]
        public void LightEnteringTheEndFaceIsGuided()
        {
            var r = Fx.Reg();
            var guide = Fx.Item(r, "optics.lightguide", new Vec2(200, 150));
            var dir = new Vec2(Math.Cos(0.6), Math.Sin(0.6));
            var entry = new Vec2(160, 151);
            var path = FreeRay(Fx.Doc(guide), entry - dir * 30, entry - dir * 10, out _);
            Assert.True(path.Points[1].DistanceTo(entry) < 1e-9);
            Assert.True(path.Points.Count >= 5, "several total reflections: " + path.Points.Count);
            Assert.Contains(path.Points, p => Math.Abs(p.X - 240) < 1e-9);
        }

        // ------------------------------------------------------------------ polarisation

        private static TracedPath PolarizedPath(Registry r, params SketchObject[] elements)
        {
            var ray = Fx.Ray(new Vec2(100, 150), new Vec2(120, 150));
            var doc = Fx.Doc(elements.Concat(new[] { ray }).ToArray());
            return Fx.Trace(doc, ray.Id).Paths.Single();
        }

        private static SketchObject Pol(Registry r, double x, double angle) => Fx.Item(r, "optics.polarizer", new Vec2(x, 150)).WithParam("angle", angle);

        [Fact]
        public void CrossedPolarizersBlockTheLight()
        {
            var r = Fx.Reg();
            var path = PolarizedPath(r, Pol(r, 150, 0), Pol(r, 200, 90));
            Assert.True(path.Absorbed);
            Assert.Equal(200, path.Last.X, 9);
            Assert.Equal(new[] { 1.0, 0.5 }, path.Intensities);
        }

        [Theory]
        [InlineData(0, 0.5)]
        [InlineData(30, 0.375)]
        [InlineData(60, 0.125)]
        public void SecondPolarizerFollowsMalussLaw(double angle, double expected)
        {
            var r = Fx.Reg();
            var path = PolarizedPath(r, Pol(r, 150, 0), Pol(r, 200, angle));
            Assert.False(path.Absorbed);
            Assert.Equal(expected, path.Intensities[path.Intensities.Count - 1], 9);
        }

        [Fact]
        public void WavePlatesTurnAndCirculariseThePolarisation()
        {
            var r = Fx.Reg();
            // A half-wave plate with its fast axis at 22.5° turns 0° into 45°: a polariser at 45° passes everything.
            var half = Fx.Item(r, "optics.waveplate.half", new Vec2(175, 150)).WithParam("angle", 22.5);
            Assert.Equal(0.5, PolarizedPath(r, Pol(r, 150, 0), half, Pol(r, 200, 45)).Intensities.Last(), 9);
            Assert.True(PolarizedPath(r, Pol(r, 150, 0), half, Pol(r, 200, -45)).Absorbed);
            // A quarter-wave plate at 45° makes circular light: any analyser passes half of it.
            var quarter = Fx.Item(r, "optics.waveplate", new Vec2(175, 150));   // fast axis 45° by default
            foreach (var a in new[] { 90.0, 0, 30 })
                Assert.Equal(0.25, PolarizedPath(r, Pol(r, 150, 0), quarter, Pol(r, 200, a)).Intensities.Last(), 9);
            Assert.Equal(1, Math.Abs(Jones.Linear(0).Retard(45, Math.PI / 2).Circularity), 9);
            Assert.Equal(0, Jones.Linear(0).Retard(0, Math.PI / 2).Circularity, 9);
            // Unpolarised light passes a wave plate unchanged.
            Assert.Equal(new[] { 1.0, 1.0 }, PolarizedPath(r, quarter).Intensities);
        }

        [Fact]
        public void WeakerLightIsDrawnTransparent()
        {
            var r = Fx.Reg();
            var ray = Fx.Ray(new Vec2(100, 150), new Vec2(120, 150));
            var doc = Fx.Doc(Pol(r, 150, 0), Pol(r, 200, 30), ray);
            var opacities = Prim.Flatten(r.Render(doc.Get(ray.Id)!, new RenderContext(doc)))
                .Select(p => p switch { LinePrim l => l.Style.Opacity, PathPrim pp => pp.Style.Opacity, _ => null })
                .ToList();
            Assert.Contains(null, opacities);    // full intensity before the first polariser
            Assert.Contains(0.5, opacities);
            Assert.Contains(0.38, opacities);    // 0.375 drawn at the level 0.38
        }

        [Fact]
        public void PolarizerAndWavePlateAxisGrips()
        {
            var r = Fx.Reg();
            foreach (var id in new[] { "optics.polarizer", "optics.waveplate" })
            {
                var o = Fx.Item(r, id, new Vec2(200, 150));
                var doc = Fx.Doc(o);
                var g = Assert.Single(r.DragHandles(o, new RenderContext(doc)));
                Assert.Equal("axis", g.Key);
                Assert.False(string.IsNullOrEmpty(g.Label.De));
                // The grip lies on the drawn axis line.
                var v = g.Position - new Vec2(200, 150);
                Assert.Equal(id == "optics.polarizer" ? 0 : 45, Grips.LineDegrees(v)!.Value, 9);
                var moved = r.MoveHandle(o, "axis", new Vec2(203, 147), new RenderContext(doc));
                Assert.Equal(45, moved.Num("angle", 0), 9);
                Assert.Equal(-45, r.MoveHandle(o, "axis", new Vec2(197, 147), new RenderContext(doc)).Num("angle", 0), 9);
                Assert.Equal(o.Size, moved.Size);
            }
            var half = Fx.Item(r, "optics.waveplate.half");
            Assert.Contains(Prim.Flatten(r.Render(half, Fx.Ctx())).OfType<TextPrim>(), t => t.Text == "λ/2");
        }

        // ------------------------------------------------------------------ spectrometer, persistence

        [Fact]
        public void SpectrometerEndsTheRaysAtItsSlitWall()
        {
            var r = Fx.Reg();
            var spec = Fx.Item(r, "optics.detector.spectrometer", new Vec2(200, 150));
            var path = FreeRay(Fx.Doc(spec), new Vec2(100, 150), new Vec2(150, 150), out var doc);
            Assert.True(path.Absorbed);
            Assert.Equal(200 - spec.Size.W / 2, path.Last.X, 9);
            var o = r.PageAnchors(spec, new RenderContext(doc)).Single(a => a.Id == "o").P;
            Assert.True(o.DistanceTo(new Vec2(180, 150)) < Tol);
            var colours = Prim.Flatten(r.Render(spec, Fx.Ctx())).OfType<LinePrim>().Select(l => l.Style.Stroke).Distinct().Count();
            Assert.True(colours >= 6, "light inside in spectral colours");
        }

        [Fact]
        public void NewElementsSurviveAJsonRoundTrip()
        {
            var r = Fx.Reg();
            var laser = Fx.Item(r, "optics.source.laser", new Vec2(60, 150));
            var doc = Fx.Doc(laser,
                Fx.Item(r, "optics.polarizer", new Vec2(90, 150)).WithParam("angle", 20.0),
                Fx.Item(r, "optics.waveplate.half", new Vec2(110, 150)),
                Fx.Item(r, "optics.lens.thick", new Vec2(140, 150)).WithParam("n", 1.6).WithParam("focal", true),
                Fx.Item(r, "optics.mirror.scanner.fan", new Vec2(220, 150)).WithParam("angle", 40.0),
                Fx.Item(r, "optics.lightguide", new Vec2(220, 60)) with { Transform = new Transform(220, 60, 90) },
                Fx.Item(r, "optics.detector.spectrometer", new Vec2(300, 150)));
            (doc, _) = Fx.Bundle(doc, laser, "exit");
            var json = DocumentJson.Serialize(doc);
            var back = DocumentJson.Deserialize(json);
            Assert.Equal(json, DocumentJson.Serialize(back));
            Assert.Equal(SvgRenderer.RenderPage(r, doc, doc.Pages[0]), SvgRenderer.RenderPage(r, back, back.Pages[0]));
        }

        [Fact]
        public void NewGripsAreBilingualAndKeepTheBox()
        {
            var r = Fx.Reg();
            var lens = Fx.Item(r, "optics.lens.thick", new Vec2(100, 150));
            var doc = Fx.Doc(lens,
                Fx.Item(r, "optics.polarizer", new Vec2(150, 150)),
                Fx.Item(r, "optics.waveplate", new Vec2(170, 150)),
                Fx.Item(r, "optics.mirror.scanner.fan", new Vec2(220, 150)));
            (doc, _) = Fx.Bundle(doc, lens, "o");
            var ctx = new RenderContext(doc);
            var count = 0;
            foreach (var o in doc.Objects.Values)
                foreach (var h in r.DragHandles(o, ctx))
                {
                    count++;
                    Assert.False(string.IsNullOrEmpty(h.Label.De), o.Type + "/" + h.Key);
                    var moved = r.MoveHandle(o, h.Key, h.Position + new Vec2(3, -2), ctx);
                    Assert.Equal(o.Size, moved.Size);
                    Assert.Equal(o.Transform, moved.Transform);
                }
            Assert.Equal(4, count);
        }
    }
}
