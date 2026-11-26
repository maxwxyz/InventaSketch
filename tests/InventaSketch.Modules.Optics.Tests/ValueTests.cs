using System;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Optics.Tests
{
    /// <summary>
    /// Element values of the optics module: light sources (P, λ, photon energy), detectors and screens (received power,
    /// share, degree and angle of polarisation from the trace), thin lenses (D = 1/f, thin lens equation, magnification,
    /// system focal length of two lenses), the thick lens, the object arrow, checks, ghost reflections. Expected values are
    /// hand calculations (Malus's law, Fresnel 4 % per face, 1/f = 1/g + 1/b, Gullstrand) and cross-checked with the tracer.
    /// </summary>
    public class ValueTests
    {
        private const double Deg = Math.PI / 180;

        private static ValueTable Solve(Registry r, SketchDocument doc) => ValueSolver.Solve(doc, r, null, new ValueSolveOptions { BudgetMs = 10000 });

        private static double Val(ValueTable t, string id, string key) => t.Get(id, key)?.Value ?? throw new Xunit.Sdk.XunitException(id + "." + key + " unknown");

        /// <summary>A new element of type <paramref name="type"/> (its defaults) at (x, y).</summary>
        private static SketchObject At(Registry r, string type, double x, double y, string id, double rotation = 0)
            => r.Type(type)!.Create(new Vec2(x, y)) with { Id = id, Transform = new Transform(x, y, rotation) };

        /// <summary>A laser at the left (exit at x = 64) with a docked bundle, and the given elements; the bundle's id.</summary>
        private static (SketchDocument Doc, string Ray) Bench(Registry r, SketchObject laser, params SketchObject[] elements)
        {
            var doc = Fx.Doc(new[] { laser }.Concat(elements).ToArray());
            return Fx.Bundle(doc, doc.Get(laser.Id)!, "exit");
        }

        // ------------------------------------------------------------------ declarations

        [Fact]
        public void ValueTypesDeclareBilingualCardValuesBoundToTheirParameters()
        {
            var r = Fx.Reg();
            var ids = new[]
            {
                "optics.source.lamp", "optics.source.led", "optics.source.laser", "optics.detector.photodiode", "optics.detector.camera",
                "optics.detector.eye", "optics.screen.plain", "optics.detector.spectrometer", "optics.lens.converging", "optics.lens.diverging",
                "optics.lens.thick", "optics.object.arrow",
            };
            foreach (var id in ids)
            {
                var t = r.Type(id)!;
                Assert.True(ValueModel.HasValues(t), id);
                var vars = ValueModel.Variables(t);
                Assert.InRange(vars.Count(v => v.Relevance == VarRelevance.Card), 1, 4);
                Assert.All(vars, v => Assert.False(string.IsNullOrEmpty(v.Name.De) || string.IsNullOrEmpty(v.Name.En), id + "." + v.Key));
                Assert.Equal(vars.Count, vars.Select(v => v.Key).Distinct().Count());
                Assert.Equal(t.Parameters.Count, t.Parameters.Select(p => p.Key).Distinct().Count());
                Assert.Contains(t.Parameters, p => p.Key == ValueLabels.ShowParam);
                Assert.All(ValueModel.Relations(t), rel => Assert.StartsWith("optics.", rel.Id));
                // Entered values live in parameters of the type (f, n, power, lambda, g, b).
                foreach (var v in vars.Where(v => v.Param is not null)) Assert.Contains(t.Parameters, p => p.Key == v.ParamKey);
            }
            // Elements without values stay without.
            Assert.False(ValueModel.HasValues(r.Type("optics.mirror.plane")));
            Assert.False(ValueModel.HasValues(r.Type("optics.ray")));
        }

        // ------------------------------------------------------------------ sources and detectors

        [Fact]
        public void LaserOnAPhotodiodeDeliversItsPowerUnpolarised()
        {
            var r = Fx.Reg();
            var (doc, _) = Bench(r, At(r, "optics.source.laser", 50, 100, "L"), At(r, "optics.detector.photodiode", 150, 100, "D"));
            var t = Solve(r, doc);
            Assert.Empty(t.Conflicts);
            // Quick sketch: 1 mW by default, 650 nm from the colour.
            Assert.Equal(1e-3, Val(t, "L", "P"), 12);
            Assert.Equal(Provenance.Default, t.Get("L", "P")!.Provenance);
            Assert.Equal(650e-9, Val(t, "L", "lambda"), 15);
            // E = h·c/λ = 6.62607015e-34 · 299792458 / 650e-9 J = 1.9074 eV.
            Assert.Equal(1.9074, Val(t, "L", "Eph") / 1.602176634e-19, 4);
            Assert.Equal(1e-3, Val(t, "D", "Prx"), 12);
            Assert.Equal(1, Val(t, "D", "eta"), 12);
            Assert.Equal(0, Val(t, "D", "V"), 12);
            Assert.Null(t.Get("D", "theta")!.Value);
            Assert.Equal(0, Val(t, "D", "pol"));   // unpolarised
        }

        [Theory]
        [InlineData(30)]
        [InlineData(-60)]
        public void PolariserHalvesThePowerAndSetsTheAngle(double axis)
        {
            var r = Fx.Reg();
            var laser = At(r, "optics.source.laser", 50, 100, "L").WithParam("power", "5 mW");
            var (doc, _) = Bench(r, laser, At(r, "optics.polarizer", 100, 100, "Pol").WithParam("angle", axis),
                At(r, "optics.detector.photodiode", 150, 100, "D"));
            var t = Solve(r, doc);
            Assert.Equal(5e-3, Val(t, "L", "P"), 12);
            Assert.Equal(Provenance.Entered, t.Get("L", "P")!.Provenance);
            Assert.Equal(2.5e-3, Val(t, "D", "Prx"), 12);
            Assert.Equal(0.5, Val(t, "D", "eta"), 12);
            Assert.Equal(1, Val(t, "D", "V"), 9);
            Assert.Equal(axis * Deg, Val(t, "D", "theta"), 9);
            Assert.Equal(1, Val(t, "D", "pol"));   // linear
        }

        [Fact]
        public void TwoPolarisersFollowMalusAndAQuarterWavePlateMakesCircularLight()
        {
            var r = Fx.Reg();
            // Polariser 0°, analyser 60°: ½·cos² 60° = 12.5 %.
            var (doc, _) = Bench(r, At(r, "optics.source.laser", 50, 100, "L"), At(r, "optics.polarizer", 90, 100, "P1"),
                At(r, "optics.polarizer", 120, 100, "P2").WithParam("angle", 60), At(r, "optics.detector.photodiode", 160, 100, "D"));
            var t = Solve(r, doc);
            Assert.Equal(0.125, Val(t, "D", "eta"), 9);
            Assert.Equal(0.125e-3, Val(t, "D", "Prx"), 12);
            // Polariser 0° and a quarter-wave plate at 45°: circular, no angle.
            var (doc2, _) = Bench(r, At(r, "optics.source.laser", 50, 100, "L"), At(r, "optics.polarizer", 90, 100, "P1"),
                At(r, "optics.waveplate", 120, 100, "Q"), At(r, "optics.detector.photodiode", 160, 100, "D"));
            var t2 = Solve(r, doc2);
            Assert.Equal(0.5, Val(t2, "D", "eta"), 9);
            Assert.Equal(1, Val(t2, "D", "V"), 9);
            Assert.Null(t2.Get("D", "theta")!.Value);
            Assert.Equal(2, Val(t2, "D", "pol"));   // circular
        }

        [Fact]
        public void EnteredReceivedPowerGivesTheSourcePower()
        {
            var r = Fx.Reg();
            var (doc, _) = Bench(r, At(r, "optics.source.laser", 50, 100, "L"), At(r, "optics.polarizer", 100, 100, "Pol"),
                At(r, "optics.detector.photodiode", 150, 100, "D"));
            var det = ValueEdits.Set(r, doc.Get("D")!, "Prx", "2 mW", Lang.En);
            doc = Edits.Replace(doc, det);
            var t = Solve(r, doc);
            Assert.Empty(t.Conflicts);
            // k = 0.5 (polariser): P = 2 mW / 0.5 = 4 mW, the default 1 mW gives way.
            Assert.Equal(4e-3, Val(t, "L", "P"), 12);
            Assert.Equal(Provenance.Derived, t.Get("L", "P")!.Provenance);
            // Solving from the source alone reaches the detector too (both report the link).
            var t2 = ValueSolver.Solve(doc, r, new[] { "L" }, new ValueSolveOptions { BudgetMs = 10000 });
            Assert.Equal(4e-3, Val(t2, "L", "P"), 12);
        }

        [Fact]
        public void NonPolarisingSplitterDividesThePowerBetweenTwoDetectors()
        {
            var r = Fx.Reg();
            var splitter = At(r, "optics.splitter.cube", 110, 100, "S").WithParam("split", "nonpolarizing").WithParam("reflectance", 0.3);
            // The diagonal reflects light from the left upwards (−y).
            var (doc, _) = Bench(r, At(r, "optics.source.laser", 50, 100, "L").WithParam("power", "10 mW"), splitter,
                At(r, "optics.detector.photodiode", 170, 100, "T"), At(r, "optics.detector.photodiode", 110, 40, "R", 90));
            var t = Solve(r, doc);
            Assert.Equal(7e-3, Val(t, "T", "Prx"), 12);
            Assert.Equal(3e-3, Val(t, "R", "Prx"), 12);
            Assert.Equal(0.7, Val(t, "T", "eta"), 12);
            var checks = ValueSolver.Checks(t, r, new RenderContext(doc).For(r));
            Assert.DoesNotContain(checks, c => c.CheckId == "optics.light.estimate");
            // A schematic splitter draws both rays at full intensity; the power is assumed to divide in halves (a hint says so).
            var schematic = Edits.Replace(doc, doc.Get("S")!.WithParam("split", "schematic"));
            var ts = Solve(r, schematic);
            Assert.Equal(5e-3, Val(ts, "T", "Prx"), 12);
            Assert.Equal(5e-3, Val(ts, "R", "Prx"), 12);
            var hints = ValueSolver.Checks(ts, r, new RenderContext(schematic).For(r));
            Assert.Contains(hints, c => c.CheckId == "optics.light.estimate" && c.ObjectId == "T" && c.Severity == CheckSeverity.Hint);
        }

        [Fact]
        public void TwoSourcesOnOneScreenAddTheirPowers()
        {
            var r = Fx.Reg();
            var a = At(r, "optics.source.laser", 50, 90, "A").WithParam("power", "2 mW");
            var b = At(r, "optics.source.laser", 50, 110, "B").WithParam("power", "6 mW");
            var pol = At(r, "optics.polarizer", 100, 90, "Pol") with { Size = new Size2(8, 10) };
            var doc = Fx.Doc(a, b, pol, At(r, "optics.screen.plain", 160, 100, "S"));
            doc = Fx.Bundle(doc, doc.Get("A")!, "exit").Doc;
            doc = Fx.Bundle(doc, doc.Get("B")!, "exit").Doc;
            var t = Solve(r, doc);
            // A passes a polariser (½), B arrives fully: 1 mW + 6 mW of 8 mW.
            Assert.Equal(7e-3, Val(t, "S", "Prx"), 12);
            Assert.Equal(7.0 / 8, Val(t, "S", "eta"), 12);
            // Coherency sum: 1 mW linear 0° plus 6 mW unpolarised → V = 1/7.
            Assert.Equal(1.0 / 7, Val(t, "S", "V"), 9);
            Assert.Equal(4, Val(t, "S", "pol"));   // partially polarised
        }

        [Fact]
        public void LampBundleSharesItsPowerAmongItsRays()
        {
            var r = Fx.Reg();
            // A parallel lamp bundle of 5 rays, a narrow stop lets the middle one through.
            var lamp = At(r, "optics.source.lamp", 50, 100, "Lamp").WithParam("emission", "parallel").WithParam("beam", 20).WithParam("power", "100 mW");
            var stop = At(r, "optics.stop.aperture", 100, 100, "Stop").WithParam("opening", 0.1);
            var doc = Fx.Doc(lamp, stop, At(r, "optics.screen.plain", 150, 100, "S"));
            doc = Fx.Bundle(doc, doc.Get("Lamp")!, "exit").Doc;
            var t = Solve(r, doc);
            Assert.Equal(0.2, Val(t, "S", "eta"), 12);
            Assert.Equal(20e-3, Val(t, "S", "Prx"), 12);
        }

        [Fact]
        public void ElementsWithoutLightReportZeroAndANoteWhenThePageHasRays()
        {
            var r = Fx.Reg();
            var (doc, _) = Bench(r, At(r, "optics.source.laser", 50, 100, "L"), At(r, "optics.detector.photodiode", 150, 100, "D"),
                At(r, "optics.screen.plain", 150, 200, "Off"));
            var t = Solve(r, doc);
            Assert.Equal(0, Val(t, "Off", "Prx"));
            Assert.Equal(0, Val(t, "Off", "eta"));
            var checks = ValueSolver.Checks(t, r, new RenderContext(doc).For(r));
            Assert.Contains(checks, c => c.CheckId == "optics.light.none" && c.ObjectId == "Off");
            Assert.DoesNotContain(checks, c => c.CheckId == "optics.light.none" && c.ObjectId == "D");
            // A screen on a page without rays has no note (image construction figures).
            var bare = Fx.Doc(At(r, "optics.screen.plain", 150, 200, "Off"));
            Assert.DoesNotContain(ValueSolver.Checks(Solve(r, bare), r, new RenderContext(bare).For(r)), c => c.CheckId == "optics.light.none");
            // A free ray reaching a detector gives the share, not a power.
            var free = Fx.Doc(At(r, "optics.detector.photodiode", 150, 100, "D"), Fx.Ray(new Vec2(80, 100), new Vec2(100, 100)) with { Id = "ray" });
            var tf = Solve(r, free);
            Assert.Equal(1, Val(tf, "D", "eta"), 12);
            Assert.Null(tf.Get("D", "Prx")!.Value);
            Assert.Contains(ValueSolver.Checks(tf, r, new RenderContext(free).For(r)), c => c.CheckId == "optics.light.nosource");
        }

        [Fact]
        public void EnteredWavelengthIsTracedAndTheColourChoiceClearsIt()
        {
            var r = Fx.Reg();
            var laser = At(r, "optics.source.laser", 50, 100, "L").WithParam("lambda", "405 nm");
            var (doc, ray) = Bench(r, laser, At(r, "optics.detector.photodiode", 150, 100, "D"));
            Assert.Equal(405, Fx.Trace(doc, ray).Paths[0].Lambdas[0], 9);
            var t = Solve(r, doc);
            Assert.Equal(405e-9, Val(t, "L", "lambda"), 15);
            Assert.Equal(Provenance.Entered, t.Get("L", "lambda")!.Provenance);
            Assert.DoesNotContain(ValueSolver.Checks(t, r, new RenderContext(doc).For(r)), c => c.CheckId == "optics.source.invisible");
            // Choosing a colour again drops the entered wavelength.
            var green = r.Type("optics.source.laser")!.Actions(doc.Get("L")!, new RenderContext(doc)).Single(a => a.Id == "optics.color=green").Apply(doc.Get("L")!);
            Assert.False(green.Params.ContainsKey("lambda"));
            Assert.Equal("green", green.Str("color", ""));
            // Infrared is outside the visible range: a hint.
            var ir = Edits.Replace(doc, doc.Get("L")!.WithParam("lambda", "1064 nm"));
            Assert.Contains(ValueSolver.Checks(Solve(r, ir), r, new RenderContext(ir).For(r)), c => c.CheckId == "optics.source.invisible");
            // A white LED has no wavelength until one is entered.
            var led = Fx.Doc(At(r, "optics.source.led", 50, 100, "LED"));
            Assert.Null(Solve(r, led).Get("LED", "lambda")!.Value);
        }

        // ------------------------------------------------------------------ thin lenses

        [Fact]
        public void ConvergingLensPowerAndThinLensEquation()
        {
            var r = Fx.Reg();
            var lens = At(r, "optics.lens.converging", 100, 100, "F").WithParam("g", "60 mm");
            var t = Solve(r, Fx.Doc(lens));
            Assert.Equal(0.04, Val(t, "F", "f"), 12);
            Assert.Equal(25, Val(t, "F", "D"), 9);
            // 1/b = 1/40 − 1/60 → b = 120 mm, β = −2.
            Assert.Equal(0.12, Val(t, "F", "b"), 12);
            Assert.Equal(-2, Val(t, "F", "beta"), 12);
            Assert.Equal(0.12, ThinLens.ImageDistance(0.04, 0.06), 12);
            // The other way round: g from b.
            var back = Solve(r, Fx.Doc(At(r, "optics.lens.converging", 100, 100, "F").WithParam("b", "120 mm")));
            Assert.Equal(0.06, Val(back, "F", "g"), 12);
        }

        [Fact]
        public void DivergingLensGivesAVirtualImage()
        {
            var r = Fx.Reg();
            var lens = At(r, "optics.lens.diverging", 100, 100, "Z").WithParam("g", "60 mm");
            var doc = Fx.Doc(lens);
            var t = Solve(r, doc);
            Assert.Equal(0.04, Val(t, "Z", "f"), 12);
            Assert.Equal(-25, Val(t, "Z", "D"), 9);
            // 1/b = −1/40 − 1/60 = −1/24 → b = −24 mm, β = 0.4 (upright, reduced).
            Assert.Equal(-0.024, Val(t, "Z", "b"), 12);
            Assert.Equal(0.4, Val(t, "Z", "beta"), 12);
            var checks = ValueSolver.Checks(t, r, new RenderContext(doc).For(r));
            Assert.Contains(checks, c => c.CheckId == "optics.lens.virtual" && c.ObjectId == "Z");
        }

        [Fact]
        public void FocalLengthFollowsFromObjectAndImageDistanceWhenCleared()
        {
            var r = Fx.Reg();
            var lens = At(r, "optics.lens.converging", 100, 100, "F");
            lens = ValueEdits.Clear(r, lens, "f");
            lens = ValueEdits.Set(r, lens, "g", "60 mm", Lang.En);
            lens = ValueEdits.Set(r, lens, "b", "120 mm", Lang.En);
            var t = Solve(r, Fx.Doc(lens));
            Assert.Empty(t.Conflicts);
            Assert.Equal(0.04, Val(t, "F", "f"), 12);
            Assert.Equal(Provenance.Derived, t.Get("F", "f")!.Provenance);
            // With f entered as well the three disagree: a conflict, nothing changed.
            var all = ValueEdits.Set(r, lens, "f", "50 mm", Lang.En);
            Assert.NotEmpty(Solve(r, Fx.Doc(all)).Conflicts);
        }

        [Fact]
        public void ObjectInTheFocalPlaneHasNoImage()
        {
            var r = Fx.Reg();
            var doc = Fx.Doc(At(r, "optics.lens.converging", 100, 100, "F").WithParam("g", "40 mm"));
            var t = Solve(r, doc);
            Assert.Null(t.Get("F", "b")!.Value);
            Assert.Contains(ValueSolver.Checks(t, r, new RenderContext(doc).For(r)), c => c.CheckId == "optics.lens.focalplane");
        }

        [Fact]
        public void TwoLensesHaveASystemFocalLengthThatTheTracerConfirms()
        {
            var r = Fx.Reg();
            var l1 = At(r, "optics.lens.converging", 100, 100, "L1");
            var l2 = At(r, "optics.lens.converging", 150, 100, "L2").WithParam("f", 20.0);
            var doc = Fx.Doc(l1, l2);
            var t = Solve(r, doc);
            // d = 50 mm: f_s = 40·20/(40 + 20 − 50) = 80 mm; s′_F = 20·(40 − 50)/10 = −20 mm (in front of the second lens).
            Assert.Equal(0.05, Val(t, "L1", "d"), 12);
            Assert.Equal(0.08, Val(t, "L1", "fs"), 12);
            Assert.Equal(-0.02, Val(t, "L1", "sF"), 12);
            Assert.Null(t.Get("L2", "fs")!.Value);   // no lens behind the second one
            // Trace: a ray 1 mm above the axis leaves the second lens with slope −h/f_s.
            const double h = 1;
            var ray = Fx.Ray(new Vec2(40, 100 - h), new Vec2(60, 100 - h));
            var withRay = Fx.Add(doc, ray);
            var path = Fx.Trace(withRay, ray.Id).Paths[0];
            var (p, dir) = Fx.Last(path);
            Assert.Equal(80, h / (dir.Y / dir.X), 6);
            var cross = Fx.Intersect(p, dir, new Vec2(0, 100), new Vec2(1, 0));
            Assert.Equal(150 - 20, cross.X, 6);

            // A diverging second lens: f₂ = −20, d = 50: f_s = 40·(−20)/(40 − 20 − 50) = 26.67 mm.
            var div = Fx.Doc(l1, At(r, "optics.lens.diverging", 150, 100, "L2").WithParam("f", 20.0));
            Assert.Equal(0.04 * -0.02 / (0.04 - 0.02 - 0.05), Val(Solve(r, div), "L1", "fs"), 12);
        }

        [Fact]
        public void KeplerTelescopeIsAfocal()
        {
            var r = Fx.Reg();
            var doc = Fx.Doc(At(r, "optics.lens.converging", 100, 100, "L1"), At(r, "optics.lens.converging", 160, 100, "L2").WithParam("f", 20.0));
            var t = Solve(r, doc);
            Assert.Equal(0.06, Val(t, "L1", "d"), 12);
            Assert.Null(t.Get("L1", "fs")!.Value);
            var checks = ValueSolver.Checks(t, r, new RenderContext(doc).For(r));
            Assert.Contains(checks, c => c.CheckId == "optics.lens.afocal" && c.ObjectId == "L1");
            // A lens off the axis or turned across it is no partner.
            var off = Fx.Doc(At(r, "optics.lens.converging", 100, 100, "L1"), At(r, "optics.lens.converging", 160, 160, "L2"));
            Assert.Null(Solve(r, off).Get("L1", "d")!.Value);
        }

        // ------------------------------------------------------------------ thick lens, object arrow

        [Fact]
        public void ThickLensFocalLengthComesFromTheLensmakerEquation()
        {
            var r = Fx.Reg();
            var lens = At(r, "optics.lens.thick", 100, 100, "T").WithParam("n", 1.6);
            var t = Solve(r, Fx.Doc(lens));
            var g = ThickLensType.Of(lens);
            Assert.Equal(g.F / 1000, Val(t, "T", "f"), 12);
            Assert.Equal(1000 / g.F, Val(t, "T", "D"), 9);
            Assert.Equal(g.Bfd / 1000, Val(t, "T", "bfd"), 12);
            Assert.Equal(1.6, Val(t, "T", "n"), 12);
        }

        [Theory]
        [InlineData(40, 60, 120, -2, 0)]      // g = 1.5 f: real, inverted, magnified twice
        [InlineData(80, 20, -40, 2, 1)]       // g = f/2: virtual, upright, magnified twice
        public void ObjectArrowShowsTheDrawnImage(double x, double g, double b, double beta, int kind)
        {
            var r = Fx.Reg();
            // The arrow stands on the axis (foot at y = 100), the lens (f = 40 mm) at x = 100.
            var arrow = Fx.Item(r, "optics.object.arrow", new Vec2(x, 92)) with { Id = "A" };
            var doc = Fx.Doc(arrow, At(r, "optics.lens.converging", 100, 100, "F"));
            var t = Solve(r, doc);
            Assert.Equal(g / 1000, Val(t, "A", "g"), 12);
            Assert.Equal(b / 1000, Val(t, "A", "b"), 12);
            Assert.Equal(beta, Val(t, "A", "beta"), 12);
            Assert.Equal(kind, Val(t, "A", "image"));
            Assert.Equal(beta * 0.016, Val(t, "A", "yi"), 12);
            var checks = ValueSolver.Checks(t, r, new RenderContext(doc).For(r));
            Assert.Equal(kind == 1, checks.Any(c => c.CheckId == "optics.image.virtual" && c.ObjectId == "A"));
            // Without a lens in front there are no values.
            Assert.Null(Solve(r, Fx.Doc(arrow)).Get("A", "g")!.Value);
        }

        // ------------------------------------------------------------------ ghosts, labels, dependents, persistence

        [Fact]
        public void GhostReflectionsOfAnUncoatedPlate()
        {
            var r = Fx.Reg();
            var plate = At(r, "optics.window.plate", 100, 100, "W");   // uncoated, normal incidence
            var (doc, ray) = Bench(r, At(r, "optics.source.laser", 50, 100, "L"), plate, At(r, "optics.detector.photodiode", 150, 100, "D"));
            var plain = Fx.Trace(doc, ray);
            Assert.Single(plain.Paths);
            Assert.DoesNotContain(plain.Paths, p => p.Ghost);
            var withGhosts = Edits.Replace(doc, doc.Get(ray)!.WithParam(RayType.GhostsKey, true));
            var traced = Fx.Trace(withGhosts, ray);
            var ghosts = traced.Paths.Where(p => p.Ghost).ToList();
            // Front face: 4 % straight back; back face: 0.96 · 4 % back inside, 0.96 of it leaves the front face.
            Assert.Equal(2, ghosts.Count);
            Assert.Contains(ghosts, p => Math.Abs(p.Intensities[0] - 0.04) < 1e-9 && Fx.Segment(p, 0).D.X < -0.999);
            Assert.Contains(ghosts, p => Math.Abs(p.Intensities[p.Intensities.Count - 1] - 0.96 * 0.04 * 0.96) < 1e-9);
            // The transmitted light is the same with or without ghosts: 0.96² = 92.16 %.
            Assert.Equal(0.9216, Val(Solve(r, doc), "D", "eta"), 9);
            Assert.Equal(0.9216, Val(Solve(r, withGhosts), "D", "eta"), 9);
            // Ideal glass reflects nothing.
            var ideal = Edits.Replace(withGhosts, withGhosts.Get("W")!.WithParam("coating", "ideal"));
            Assert.DoesNotContain(Fx.Trace(ideal, ray).Paths, p => p.Ghost);
            // The toggle is offered on traced rays.
            Assert.Contains(r.Type("optics.ray")!.Actions(doc.Get(ray)!, new RenderContext(doc)), a => a.Id == "optics.ray.ghosts");
        }

        [Fact]
        public void DetectorShowingValuesIsADependentAndDrawsItsLabel()
        {
            var r = Fx.Reg();
            var det = At(r, "optics.detector.photodiode", 150, 100, "D").WithParam(ValueLabels.ShowParam, true);
            var (doc, _) = Bench(r, At(r, "optics.source.laser", 50, 100, "L").WithParam("power", "3 mW"), At(r, "optics.polarizer", 100, 100, "Pol"), det);
            var ctx = new RenderContext(doc).For(r);
            Assert.Contains("D", r.Type("optics.polarizer")!.Dependents(doc.Get("Pol")!, ctx));
            // Without the label it is not.
            var hidden = Edits.Replace(doc, doc.Get("D")!.WithParam(ValueLabels.ShowParam, false));
            Assert.DoesNotContain("D", r.Type("optics.polarizer")!.Dependents(hidden.Get("Pol")!, new RenderContext(hidden).For(r)));
            var label = r.RenderLocal(doc.Get("D")!, ctx).OfType<TextPrim>().Single(p => p.Tag == ValueLabels.Part);
            Assert.Contains("1.5 mW", label.Text);
        }

        [Fact]
        public void EnteredValuesSurviveAJsonRoundTrip()
        {
            var r = Fx.Reg();
            var (doc, _) = Bench(r, At(r, "optics.source.laser", 50, 100, "L").WithParam("power", "5 mW").WithParam("lambda", "532 nm"),
                At(r, "optics.detector.photodiode", 150, 100, "D"), At(r, "optics.lens.converging", 100, 300, "F").WithParam("g", "60 mm"));
            var back = DocumentJson.Deserialize(DocumentJson.Serialize(doc));
            var a = Solve(r, doc);
            var b = Solve(r, back);
            foreach (var (id, key) in new[] { ("L", "P"), ("L", "lambda"), ("D", "Prx"), ("F", "b") })
                Assert.Equal(Val(a, id, key), Val(b, id, key), 15);
            Assert.Equal("5 mW", back.Get("L")!.Str("power", ""));
        }
    }
}
