using System;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Optics.Tests
{
    /// <summary>Fresnel equations, Brewster angle, total internal reflection, coatings, beam splitters and the ray readout.</summary>
    public class FresnelTests
    {
        private const double Deg = Math.PI / 180;

        // ------------------------------------------------------------------ equations (known values)

        [Fact]
        public void GlassReflectsFourPercentAtNormalIncidence()
        {
            var (rs, rp) = Fresnel.Reflectance(1, 1.5, 0);
            Assert.Equal(0.04, rs, 12);   // ((n − 1) / (n + 1))²
            Assert.Equal(0.04, rp, 12);
            var (ts, tp) = Fresnel.Transmittance(1.5, 1, 0);
            Assert.Equal(0.96, ts, 12);
            Assert.Equal(0.96, tp, 12);
        }

        [Theory]
        [InlineData(20)]
        [InlineData(45)]
        [InlineData(70)]
        public void ReflectancesFollowTheFresnelEquations(double i)
        {
            // Independent textbook form: R_s = sin²(i − t) / sin²(i + t), R_p = tan²(i − t) / tan²(i + t).
            var t = Math.Asin(Math.Sin(i * Deg) / 1.5);
            var a = i * Deg;
            var rs = Math.Pow(Math.Sin(a - t) / Math.Sin(a + t), 2);
            var rp = Math.Pow(Math.Tan(a - t) / Math.Tan(a + t), 2);
            var (Rs, Rp) = Fresnel.Reflectance(1, 1.5, i);
            Assert.Equal(rs, Rs, 12);
            Assert.Equal(rp, Rp, 12);
            Assert.True(Rs > Rp, "s is reflected more than p");
            if (i == 45)
            {
                Assert.Equal(0.0920, Rs, 4);
                Assert.Equal(0.0085, Rp, 4);
            }
        }

        [Fact]
        public void BrewsterAngleReflectsNoP()
        {
            var b = Fresnel.BrewsterAngle(1, 1.5);
            Assert.Equal(56.31, b, 2);
            var (rs, rp) = Fresnel.Reflectance(1, 1.5, b);
            Assert.Equal(0, rp, 12);
            Assert.Equal(0.1479, rs, 4);
            // Inside the glass the refracted ray meets the far face at the internal Brewster angle 90° − θB.
            Assert.Equal(90 - b, Fresnel.BrewsterAngle(1.5, 1), 9);
            Assert.Equal(0, Fresnel.Reflectance(1.5, 1, 90 - b).Rp, 12);
        }

        [Fact]
        public void TotalInternalReflectionAboveTheCriticalAngle()
        {
            Assert.Equal(41.81, Fresnel.CriticalAngle(1.5, 1), 2);
            Assert.True(double.IsNaN(Fresnel.CriticalAngle(1, 1.5)));
            var (rs, rp) = Fresnel.Reflectance(1.5, 1, 42);
            Assert.Equal(1, rs, 12);
            Assert.Equal(1, rp, 12);
            var below = Fresnel.Reflectance(1.5, 1, 41);
            Assert.True(below.Rs < 1 && below.Rp < 1);
            Assert.Equal(0, Fresnel.Transmittance(1.5, 1, 60).Ts, 12);
            // Phase of total reflection: tan(Δ/2) = cos i·√(sin² i − n²) / sin² i (n = 1/1.5), shifted by 180° in the
            // frame in which an ideal mirror changes nothing.
            foreach (var i in new[] { 45.0, 60.0 })
            {
                double s2 = Math.Pow(Math.Sin(i * Deg), 2), n2 = 1 / 2.25;
                var textbook = 2 * Math.Atan(Math.Cos(i * Deg) * Math.Sqrt(s2 - n2) / s2) / Deg;
                Assert.Equal(180 - textbook, Math.Abs(Fresnel.ReflectionPhase(1.5, 1, i)), 9);
            }
            // Fresnel rhomb (n = 1.51, 54.6°): two total reflections make a quarter wave.
            var two = 2 * Fresnel.ReflectionPhase(1.51, 1, 54.6);
            var quarter = Math.Abs(Math.IEEERemainder(two, 360));
            Assert.Equal(90, quarter, 0);
            // Below the critical angle the reflection phase is 0 or 180°.
            Assert.Equal(0, Fresnel.ReflectionPhase(1, 1.5, 30), 9);
            Assert.Equal(180, Math.Abs(Fresnel.ReflectionPhase(1, 1.5, 70)), 9);
        }

        [Fact]
        public void QuarterWaveCoatingCancelsTheReflectionAtItsDesignWavelength()
        {
            var nc = Math.Sqrt(1.5);
            var (ts, tp) = Fresnel.CoatedTransmittance(1, 1.5, 1, nc, 550);
            Assert.Equal(1, ts, 12);
            Assert.Equal(1, tp, 12);
            var red = Fresnel.CoatedTransmittance(1, 1.5, 1, nc, 656.3);
            Assert.True(red.Ts < 1 && red.Ts > 0.995, red.Ts.ToString());
            // Seen from inside the glass it works the same way; at 30° it still reflects less than bare glass.
            Assert.Equal(1, Fresnel.CoatedTransmittance(1.5, 1, 1, nc, 550).Ts, 12);
            var bare = Fresnel.Transmittance(1, 1.5, 30);
            var coated = Fresnel.CoatedTransmittance(1, 1.5, Math.Cos(30 * Deg), nc, 550);
            Assert.True(coated.Ts > bare.Ts && coated.Tp > bare.Tp);
        }

        [Fact]
        public void CoherencyMatrixAgreesWithJonesVectors()
        {
            var j = Jones.Linear(20).Retard(50, 1.1);
            var c = Coherency.From(j);
            Assert.Equal(1, c.Degree, 9);
            Assert.Equal(j.Circularity, c.Circularity, 9);
            foreach (var a in new[] { -60.0, 0, 33, 90 }) Assert.Equal(j.Transmission(a), c.Transmission(a), 9);
            var r = Coherency.From(Jones.Linear(20)).Retard(50, 1.1);
            foreach (var a in new[] { -60.0, 0, 33, 90 }) Assert.Equal(j.Transmission(a), r.Transmission(a), 9);
            Assert.Equal(0, Coherency.Unpolarized.Degree, 12);
            Assert.Equal(30, Coherency.Linear(30).Orientation, 9);
        }

        // ------------------------------------------------------------------ traced elements

        private static TraceResult Trace(params SketchObject[] elements)
        {
            var ray = Fx.Ray(new Vec2(100, 150), new Vec2(120, 150));
            var doc = Fx.Doc(elements.Concat(new[] { ray }).ToArray());
            return Fx.Trace(doc, ray.Id);
        }

        private static double End(TracedPath p) => p.Intensities[p.Intensities.Count - 1];

        private static SketchObject Pol(Registry r, double x, double angle) => Fx.Item(r, "optics.polarizer", new Vec2(x, 150)).WithParam("angle", angle);

        [Fact]
        public void ExistingElementsStayLosslessByDefault()
        {
            var r = Fx.Reg();
            var laser = Fx.Item(r, "optics.source.laser", new Vec2(40, 150)).WithParam("rays", 3).WithParam("beam", 4.0);
            var doc = Fx.Doc(laser,
                Fx.Item(r, "optics.lens.biconvex", new Vec2(70, 150)),
                Fx.Item(r, "optics.lens.thick", new Vec2(110, 150)),
                Fx.Item(r, "optics.prism", new Vec2(160, 150)),
                Fx.Item(r, "optics.splitter.cube", new Vec2(230, 150)),
                Fx.Item(r, "optics.lightguide", new Vec2(330, 150)));
            (doc, var rayId) = Fx.Bundle(doc, laser, "exit");
            var result = Fx.Trace(doc, rayId);
            Assert.True(result.Paths.Count >= 3);
            Assert.All(result.Paths, p => Assert.All(p.Intensities, i => Assert.Equal(1, i)));
            Assert.All(result.Paths, p => Assert.All(p.Polarizations, x => Assert.Null(x)));
            // So the rays are drawn as before: no transparency anywhere.
            var prims = Prim.Flatten(r.Render(doc.Get(rayId)!, new RenderContext(doc)));
            Assert.All(prims, x => Assert.Null(x switch { LinePrim l => l.Style.Opacity, PathPrim pp => pp.Style.Opacity, _ => null }));
        }

        [Fact]
        public void UncoatedGlassLosesFourPercentPerFace()
        {
            var r = Fx.Reg();
            foreach (var id in new[] { "optics.lens.thick", "optics.lens.biconvex" })
            {
                var lens = Fx.Item(r, id, new Vec2(200, 150));
                Assert.Equal(1, End(Trace(lens).Paths[0]), 12);
                Assert.Equal(0.9216, End(Trace(lens.WithParam("coating", "none")).Paths[0]), 9);
                Assert.Equal(1, End(Trace(lens.WithParam("coating", "ar")).Paths[0]), 9);
            }
            // A window across the axis: 0.96² as well; inside the glass the light has 0.96.
            var window = Fx.Item(r, "optics.window", new Vec2(200, 150));
            Assert.Equal("none", window.Str("coating", "none"));
            var path = Trace(window).Paths[0];
            Assert.Equal(new[] { 1, 0.96, 0.9216 }, path.Intensities.Select(x => Math.Round(x, 12)));
        }

        [Fact]
        public void BrewsterWindowPassesPAndPolarisesUnpolarisedLightPartly()
        {
            var r = Fx.Reg();
            var window = Fx.Item(r, "optics.window.brewster", new Vec2(200, 150));
            Assert.Equal(56.31, WindowType.Tilt(window), 2);
            var t = 1 - Fresnel.Reflectance(1, 1.5, Fresnel.BrewsterAngle(1, 1.5)).Rs;   // per face, s
            Assert.Equal(0.8521, t, 4);

            // p-polarised light passes without loss, s loses about 15 % at each face.
            Assert.Equal(0.5, End(Trace(Pol(r, 150, 0), window).Paths[0]), 9);
            Assert.Equal(0.5 * t * t, End(Trace(Pol(r, 150, 90), window).Paths[0]), 9);

            // Unpolarised light: (1 + T²)/2 behind the window, partly p-polarised (degree (1 − T²)/(1 + T²)).
            var behind = Trace(window).Paths[0];
            Assert.Equal((1 + t * t) / 2, End(behind), 9);
            var pol = behind.Polarizations.Last()!.Value;
            Assert.Equal((1 - t * t) / (1 + t * t), pol.Degree, 9);
            Assert.Equal(0, pol.Orientation, 9);

            // A polariser after the window: along p it passes half of the original light, across it (s) less.
            Assert.Equal(0.5, End(Trace(window, Pol(r, 260, 0)).Paths[0]), 9);
            Assert.Equal(0.5 * t * t, End(Trace(window, Pol(r, 260, 90)).Paths[0]), 9);
            Assert.Equal(0.363, 0.5 * t * t, 3);
            // The beam is displaced sideways but keeps its direction.
            Assert.True(Fx.Last(behind).D.DistanceTo(new Vec2(1, 0)) < 1e-9);
            Assert.True(Math.Abs(behind.Last.Y - 150) > 0.5, "displaced by the tilted plate");
        }

        [Fact]
        public void TotalReflectionInAPrismShiftsTheRelativePhase()
        {
            var r = Fx.Reg();
            var prism = Fx.Item(r, "optics.prism.rightangle", new Vec2(200, 150));
            SketchObject Analyser(double angle) => Fx.Item(r, "optics.polarizer", new Vec2(200, 200)).WithParam("angle", angle) with { Transform = new Transform(200, 200, 90) };
            double Through(SketchObject p, double angle)
            {
                var path = Trace(Pol(r, 150, 45), p, Analyser(angle)).Paths[0];
                Assert.Equal(200, path.Points[5].Y, 9);   // start, polariser, prism entry, hypotenuse, prism exit, analyser
                return path.Absorbed ? 0 : End(path);
            }
            // Ideal faces: the polarisation stays linear at 45°.
            Assert.Equal(0.5, Through(prism, 45), 9);
            Assert.Equal(0, Through(prism, -45), 9);
            // Uncoated: entry and exit at normal incidence lose 4 % each (equal for s and p); the total reflection at 45°
            // shifts p against s by Δ with |Δ| = 180° − 36.87°, so the analyser at 45° passes cos²(Δ/2) = 0.1.
            var bare = prism.WithParam("coating", "none");
            var delta = Fresnel.ReflectionPhase(1.5, 1, 45) * Deg;
            Assert.Equal(0.1, Math.Pow(Math.Cos(delta / 2), 2), 9);
            Assert.Equal(0.5 * 0.9216 * 0.1, Through(bare, 45), 9);
            Assert.Equal(0.5 * 0.9216 * 0.9, Through(bare, -45), 9);
        }

        [Fact]
        public void TotalReflectionKeepsTheIntensity()
        {
            var r = Fx.Reg();
            var guide = Fx.Item(r, "optics.lightguide", new Vec2(200, 150)).WithParam("coating", "none");
            // From inside the glass at 60° to the top face: totally reflected without loss.
            var a = 30 * Deg;
            var start = new Vec2(170, 150);
            var ray = Fx.Ray(start, start + new Vec2(Math.Cos(a), -Math.Sin(a)) * 3);
            var path = Fx.Trace(Fx.Doc(guide, ray), ray.Id).Paths[0];
            Assert.Equal(146, path.Points[1].Y, 9);
            Assert.Equal(1, path.Intensities[1], 12);
            // Leaving through the end face (at 30° there) it loses the Fresnel share.
            Assert.True(End(path) < 1);
        }

        [Fact]
        public void PolarisingBeamSplitterSeparatesSAndP()
        {
            var r = Fx.Reg();
            var pbs = Fx.Item(r, "optics.splitter.pbs", new Vec2(200, 150));
            Assert.Equal("polarizing", pbs.Str("split", ""));
            var result = Trace(pbs);
            Assert.Equal(2, result.Paths.Count);
            var (t, refl) = (result.Paths[0], result.Paths[1]);
            Assert.Equal(0.5, End(t), 9);
            Assert.Equal(0.5, End(refl), 9);
            Assert.Equal(0, t.Polarizations.Last()!.Value.Orientation, 9);
            Assert.Equal(90, Math.Abs(refl.Polarizations.Last()!.Value.Orientation), 9);
            Assert.True(Fx.Last(refl).D.DistanceTo(new Vec2(0, -1)) < 1e-9, "reflected upwards");
            // p-polarised light passes completely, nothing is reflected; s is reflected completely.
            var p = Trace(Pol(r, 150, 0), pbs);
            Assert.Single(p.Paths);
            Assert.Equal(0.5, End(p.Paths[0]), 9);
            var s = Trace(Pol(r, 150, 90), pbs);
            Assert.Equal(2, s.Paths.Count);
            Assert.True(s.Paths[0].Absorbed);
            Assert.Equal(0.5, End(s.Paths[1]), 9);
            // At 45° the splitter divides it in halves.
            var d = Trace(Pol(r, 150, 45), pbs);
            Assert.Equal(0.25, End(d.Paths[0]), 9);
            Assert.Equal(0.25, End(d.Paths[1]), 9);
            // The polarising cube draws its splitting surface double.
            var lines = Prim.Flatten(r.Render(pbs, Fx.Ctx())).OfType<LinePrim>().Count(l => l.Tag == OpticsTags.Detail);
            Assert.Equal(2, lines);
        }

        [Fact]
        public void NonPolarisingSplitterDividesTheIntensity()
        {
            var r = Fx.Reg();
            var cube = Fx.Item(r, "optics.splitter.cube.5050", new Vec2(200, 150));
            var result = Trace(Pol(r, 150, 30), cube);
            Assert.Equal(0.25, End(result.Paths[0]), 9);
            Assert.Equal(0.25, End(result.Paths[1]), 9);
            Assert.Equal(30, result.Paths[1].Polarizations.Last()!.Value.Orientation, 9);   // polarisation kept
            var plate = Fx.Item(r, "optics.splitter.plate", new Vec2(200, 150)).WithParam("split", "nonpolarizing").WithParam("reflectance", 0.3);
            var split = Trace(plate);
            Assert.Equal(0.7, End(split.Paths[0]), 9);
            Assert.Equal(0.3, End(split.Paths[1]), 9);
            // Schematic (the default): both at full intensity, as before.
            var schematic = Trace(Fx.Item(r, "optics.splitter.cube", new Vec2(200, 150)));
            Assert.All(schematic.Paths, x => Assert.Equal(1, End(x)));
            Assert.Contains(r.Actions(cube, new RenderContext(Fx.Doc(cube))), a => a.Id == "optics.splitter.reflectance");
        }

        // ------------------------------------------------------------------ readout, grips, persistence

        [Fact]
        public void RayShowsIntensityAndPolarisationAtItsEnds()
        {
            var r = Fx.Reg();
            var ray = Fx.Ray(new Vec2(100, 150), new Vec2(120, 150));
            var doc = Fx.Doc(Pol(r, 150, 0), Fx.Item(r, "optics.window.brewster", new Vec2(200, 150)), ray);
            string[] Labels(SketchDocument d, Lang lang) => Prim.Flatten(r.Render(d.Get(ray.Id)!, new RenderContext(d, lang)))
                .OfType<TextPrim>().Where(t => t.Tag == ValueLabels.Part).Select(t => t.Text).ToArray();
            Assert.Empty(Labels(doc, Lang.En));
            var shown = Edits.Replace(doc, doc.Get(ray.Id)!.WithParam(ValueLabels.ShowParam, true));
            Assert.Equal(new[] { "50 % (linear 0°)" }, Labels(shown, Lang.En));
            Assert.Contains(r.Parts(shown.Get(ray.Id)!, new RenderContext(shown)), p => p.Key == ValueLabels.Part);
            // Unpolarised light through the window: partly polarised.
            var t = 1 - Fresnel.Reflectance(1, 1.5, Fresnel.BrewsterAngle(1, 1.5)).Rs;
            Assert.Equal("86 % (16 % polarised)", Beams_Readout((1 + t * t) / 2, Coherency.From(Jones.Linear(0)), 0.16, Lang.En));
            Assert.Equal("4,2 % (zirkular)", RayType.Readout(0.042, Coherency.From(Jones.Linear(0).Retard(45, Math.PI / 2)), Lang.De));
            Assert.Equal("100 %", RayType.Readout(1, null, Lang.En));
            Assert.Equal("37 % (linear -45°)", RayType.Readout(0.37, Coherency.Linear(-45), Lang.En));
            Assert.Contains(r.Actions(ray, new RenderContext(doc)), a => a.Id == "optics.ray.values");
        }

        /// <summary>The readout of a partly polarised state with the given degree (mixing a pure state with unpolarised light).</summary>
        private static string Beams_Readout(double intensity, Coherency pure, double degree, Lang lang)
        {
            var mix = new Coherency(degree * pure.A + (1 - degree) * 0.5, degree * pure.B + (1 - degree) * 0.5, degree * pure.Cr, degree * pure.Ci);
            Assert.Equal(degree, mix.Degree, 9);
            return RayType.Readout(intensity, mix, lang);
        }

        [Fact]
        public void WindowTiltGripTurnsThePlate()
        {
            var r = Fx.Reg();
            var window = Fx.Item(r, "optics.window.brewster", new Vec2(200, 150));
            var doc = Fx.Doc(window);
            var g = Assert.Single(r.DragHandles(window, new RenderContext(doc)));
            Assert.Equal("tilt", g.Key);
            Assert.Equal("Tilt: 56.31°", g.Label.En);
            Assert.Equal("Neigung: 56,31°", g.Label.De);
            var moved = r.MoveHandle(window, "tilt", new Vec2(210, 140), new RenderContext(doc));
            Assert.Equal(45, moved.Num("tilt", 0), 9);
            Assert.False(moved.Flag("brewster", true));
            Assert.Equal(window.Size, moved.Size);
            Fx.AssertInsideBox(r.TypeOf(moved).Render(moved, Fx.Ctx()), moved.Size, "tilted window");
        }

        [Fact]
        public void FresnelSettingsSurviveAJsonRoundTrip()
        {
            var r = Fx.Reg();
            var laser = Fx.Item(r, "optics.source.laser", new Vec2(60, 150));
            var doc = Fx.Doc(laser,
                Fx.Item(r, "optics.window.brewster", new Vec2(100, 150)).WithParam("n", 1.6),
                Fx.Item(r, "optics.prism", new Vec2(150, 150)).WithParam("coating", "ar"),
                Fx.Item(r, "optics.splitter.pbs", new Vec2(220, 150)),
                Fx.Item(r, "optics.splitter.plate", new Vec2(260, 150)).WithParam("split", "nonpolarizing").WithParam("reflectance", 0.2));
            (doc, var rayId) = Fx.Bundle(doc, laser, "exit");
            doc = Edits.Replace(doc, doc.Get(rayId)!.WithParam(ValueLabels.ShowParam, true));
            var json = DocumentJson.Serialize(doc);
            var back = DocumentJson.Deserialize(json);
            Assert.Equal(json, DocumentJson.Serialize(back));
            Assert.Equal(SvgRenderer.RenderPage(r, doc, doc.Pages[0]), SvgRenderer.RenderPage(r, back, back.Pages[0]));
            Assert.Equal("ar", back.Objects.Values.Single(o => o.Type == PrismType.TypeId).Str("coating", ""));
        }
    }
}
