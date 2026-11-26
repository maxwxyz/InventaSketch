using System;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Optics.Tests
{
    /// <summary>
    /// Values of curved mirrors (f = R/2, mirror equation, magnification), the prism (apex angle, minimum deviation,
    /// dispersion), the diffraction grating (line density, period, grating equation for the orders drawn), the irradiance
    /// estimate of detectors and the system focal length with a thick lens (principal planes). Expected values are textbook
    /// hand calculations, cross-checked with the tracer where it draws the same thing.
    /// </summary>
    public class ElementValueTests
    {
        private const double Deg = Math.PI / 180;

        private static ValueTable Solve(Registry r, SketchDocument doc) => ValueSolver.Solve(doc, r, null, new ValueSolveOptions { BudgetMs = 10000 });

        private static double Val(ValueTable t, string id, string key) => t.Get(id, key)?.Value ?? throw new Xunit.Sdk.XunitException(id + "." + key + " unknown");

        private static SketchObject At(Registry r, string type, double x, double y, string id, double rotation = 0)
            => r.Type(type)!.Create(new Vec2(x, y)) with { Id = id, Transform = new Transform(x, y, rotation) };

        private static CheckFinding[] Checks(Registry r, SketchDocument doc, ValueTable t) => ValueSolver.Checks(t, r, new RenderContext(doc).For(r)).ToArray();

        // ------------------------------------------------------------------ curved mirrors

        [Fact]
        public void ConcaveMirrorFollowsTheMirrorEquation()
        {
            var r = Fx.Reg();
            var doc = Fx.Doc(At(r, "optics.mirror.concave", 100, 100, "M").WithParam("g", "60 mm"));
            var t = Solve(r, doc);
            Assert.Empty(t.Conflicts);
            // f = 40 mm, R = 2f = 80 mm; 1/b = 1/40 − 1/60 → b = 120 mm (real, in front), β = −2.
            Assert.Equal(0.04, Val(t, "M", "f"), 12);
            Assert.Equal(0.08, Val(t, "M", "R"), 12);
            Assert.Equal(0.12, Val(t, "M", "b"), 12);
            Assert.Equal(-2, Val(t, "M", "beta"), 12);
            Assert.DoesNotContain(Checks(r, doc, t), c => c.CheckId == "optics.mirror.virtual");
            // g = f: the image lies at infinity.
            var focal = Fx.Doc(At(r, "optics.mirror.concave", 100, 100, "M").WithParam("g", "40 mm"));
            var tf = Solve(r, focal);
            Assert.Null(tf.Get("M", "b")!.Value);
            Assert.Contains(Checks(r, focal, tf), c => c.CheckId == "optics.mirror.focalplane");
            // The radius is what the drawing shows (C at 2f): read-only; the plane mirror has no values.
            Assert.True(OpticsValues.MirrorVariables(true).Single(v => v.Key == "R").IsReadOnly);
            Assert.All(OpticsValues.MirrorRelations(true), rel => Assert.Equal("S41", rel.Source));
            Assert.False(ValueModel.HasValues(r.Type("optics.mirror.plane")));
        }

        [Fact]
        public void ConvexMirrorGivesAVirtualUprightImage()
        {
            var r = Fx.Reg();
            var doc = Fx.Doc(At(r, "optics.mirror.convex", 100, 100, "M").WithParam("f", 30.0).WithParam("g", "60 mm"));
            var t = Solve(r, doc);
            // f = −30 mm: 1/b = −1/30 − 1/60 = −1/20 → b = −20 mm (behind the mirror), β = 1/3.
            Assert.Equal(0.03, Val(t, "M", "f"), 12);
            Assert.Equal(0.06, Val(t, "M", "R"), 12);
            Assert.Equal(-0.02, Val(t, "M", "b"), 12);
            Assert.Equal(1.0 / 3, Val(t, "M", "beta"), 12);
            Assert.Contains(Checks(r, doc, t), c => c.CheckId == "optics.mirror.virtual" && c.ObjectId == "M");
            // The other way round: g from b, f from g and b once f is cleared.
            var m = ValueEdits.Clear(r, At(r, "optics.mirror.concave", 100, 100, "M"), "f");
            m = ValueEdits.Set(r, m, "g", "30 mm", Lang.En);
            m = ValueEdits.Set(r, m, "b", "60 mm", Lang.En);
            var t2 = Solve(r, Fx.Doc(m));
            Assert.Empty(t2.Conflicts);
            Assert.Equal(0.02, Val(t2, "M", "f"), 12);
            Assert.Equal(0.04, Val(t2, "M", "R"), 12);
        }

        // ------------------------------------------------------------------ prism

        /// <summary>An equilateral prism (60° apex: width 30 mm, height 15·√3 mm) at (100, 100).</summary>
        private static SketchObject Equilateral(Registry r, string id = "P")
            => At(r, "optics.prism.triangle", 100, 100, id) with { Size = new Size2(30, 15 * Math.Sqrt(3)) };

        [Fact]
        public void EquilateralPrismHasTheTextbookMinimumDeviation()
        {
            var r = Fx.Reg();
            var prism = Equilateral(r);
            var doc = Fx.Doc(prism);
            var t = Solve(r, doc);
            Assert.Empty(t.Conflicts);
            Assert.Equal(60 * Deg, Val(t, "P", "A"), 12);
            Assert.Equal(1.5, Val(t, "P", "n"), 12);
            // n = 1.5, A = 60°: δ_min = 2·asin(1.5·sin 30°) − 60° = 2·48.5904° − 60° = 37.1808°.
            var dmin = Val(t, "P", "dmin");
            Assert.Equal(37.1808, dmin / Deg, 4);
            Assert.Equal(2 * Math.Asin(0.75) - Math.PI / 3, dmin, 12);
            // Without dispersion there are no F/C values.
            Assert.Null(t.Get("P", "nF")!.Value);

            // The tracer agrees: a ray entering at θ₁ = asin(n·sin(A/2)) runs parallel to the base and leaves deviated by δ_min.
            var theta1 = Math.Asin(0.75);
            // Inward normal of the left face points 30° below the x axis (y down); the ray comes in θ₁ above it.
            var dir = new Vec2(Math.Cos(Math.PI / 6 - theta1), Math.Sin(Math.PI / 6 - theta1));
            var mid = new Vec2(100 - 7.5, 100);
            var ray = Fx.Ray(mid - dir * 30, mid);
            var withRay = Fx.Add(doc, ray);
            var path = Fx.Trace(withRay, ray.Id).Paths[0];
            var inside = Fx.Segment(path, 1).D;
            Assert.Equal(0, inside.Y, 9);   // parallel to the base
            var (_, outDir) = Fx.Last(path);
            Assert.Equal(dmin, Math.Acos(Math.Max(-1, Math.Min(1, dir.Dot(outDir)))), 9);

            // A measured minimum deviation gives the index (clear n first): the spectrometer method.
            var measured = ValueEdits.Clear(r, prism, "n");
            measured = ValueEdits.Set(r, measured, "dmin", "37.1808°", Lang.En);
            Assert.Equal(1.5, Val(Solve(r, Fx.Doc(measured)), "P", "n"), 5);
        }

        [Fact]
        public void PrismDispersionGivesFAndCIndicesAbbeNumberAndSpread()
        {
            var r = Fx.Reg();
            var prism = Equilateral(r).WithParam("dispersion", true);
            var t = Solve(r, Fx.Doc(prism));
            double nF = Val(t, "P", "nF"), nC = Val(t, "P", "nC");
            // Cauchy fit through n_d = 1.5 and n_F − n_C = 0.03 (the default dn).
            Assert.Equal(0.03, nF - nC, 12);
            Assert.Equal(Spectrum.Index(1.5, 0.03, 486.1), nF, 12);
            Assert.InRange(nF, 1.520, 1.522);
            Assert.InRange(nC, 1.490, 1.492);
            // Abbe number ν_d = (n_d − 1)/(n_F − n_C) = 0.5/0.03.
            Assert.Equal(0.5 / 0.03, Val(t, "P", "vd"), 9);
            // Spread at the incidence of minimum deviation for the d line, by Snell's law at both faces.
            var a = Math.PI / 3;
            var th1 = Math.Asin(1.5 * Math.Sin(a / 2));
            double Dev(double n) => th1 + Math.Asin(n * Math.Sin(a - Math.Asin(Math.Sin(th1) / n))) - a;
            var spread = Val(t, "P", "dFC");
            Assert.Equal(Dev(nF) - Dev(nC), spread, 12);
            // Close to the first-order estimate Δn·2·sin(A/2)/cos θ₁ = 0.03/cos 48.59° ≈ 2.60° (exact: 2.62°).
            Assert.InRange(spread / (0.03 * 2 * Math.Sin(a / 2) / Math.Cos(th1)), 0.98, 1.02);
            Assert.All(OpticsValues.PrismVariables.Where(v => v.Key is "A" or "nF" or "nC" or "vd" or "dFC"), v => Assert.True(v.IsReadOnly, v.Key));
        }

        [Fact]
        public void PrismWithoutSymmetricPassageHasNoMinimumDeviation()
        {
            var r = Fx.Reg();
            // n = 2.1, A = 60°: n·sin 30° = 1.05 ≥ 1, total internal reflection at the second face.
            var doc = Fx.Doc(Equilateral(r).WithParam("n", 2.1));
            var t = Solve(r, doc);
            Assert.Null(t.Get("P", "dmin")!.Value);
            Assert.Contains(Checks(r, doc, t), c => c.CheckId == "optics.prism.nominimum" && c.ObjectId == "P");
            // A square right-angle prism has an apex angle of 45° at its top corner.
            var right = Fx.Doc(At(r, "optics.prism.triangle", 100, 100, "R").WithParam("shape", "rightangle") with { Size = new Size2(26, 26) });
            var tr = Solve(r, right);
            Assert.Equal(45 * Deg, Val(tr, "R", "A"), 12);
            Assert.Equal(2 * Math.Asin(1.5 * Math.Sin(22.5 * Deg)) - 45 * Deg, Val(tr, "R", "dmin"), 12);
            Assert.DoesNotContain(Checks(r, right, tr), c => c.CheckId == "optics.prism.nominimum");
        }

        // ------------------------------------------------------------------ grating

        [Fact]
        public void GratingPeriodFollowsTheDrawnAngle()
        {
            var r = Fx.Reg();
            var doc = Fx.Doc(At(r, "optics.grating.transmission", 100, 100, "G"));
            var t = Solve(r, doc);
            Assert.Empty(t.Conflicts);
            // 20° at 550 nm: d = 550 nm / sin 20° = 1.60809 µm, N = 621.8 lines/mm.
            var d = 550e-9 / Math.Sin(20 * Deg);
            Assert.Equal(d, Val(t, "G", "d"), 15);
            Assert.Equal(1e-3 / d, Val(t, "G", "N"), 9);
            // Without light the values use 550 nm: the first order lies at the drawn 20°.
            Assert.Equal(550e-9, Val(t, "G", "lambda"), 15);
            Assert.Equal(Provenance.Default, t.Get("G", "lambda")!.Provenance);
            Assert.Equal(20 * Deg, Val(t, "G", "theta1"), 12);
            Assert.Null(t.Get("G", "theta2")!.Value);   // only one order drawn
            Assert.Equal(2, Val(t, "G", "mmax"));     // d/λ = 2.92
        }

        [Fact]
        public void GratingEquationUsesTheLaserWavelengthAndMatchesTheDrawnOrders()
        {
            var r = Fx.Reg();
            var grating = At(r, "optics.grating.transmission", 100, 100, "G").WithParam("lines", "600").WithParam("orders", 3);
            var laser = At(r, "optics.source.laser", 50, 100, "L");   // red, 650 nm
            var doc = Fx.Doc(laser, grating, At(r, "optics.screen.plain", 200, 100, "S"));
            var (withRay, ray) = Fx.Bundle(doc, doc.Get("L")!, "exit");
            var t = Solve(r, withRay);
            Assert.Empty(t.Conflicts);
            // N = 600 lines/mm: d = 1.6667 µm; λ = 650 nm from the traced light.
            Assert.Equal(600, Val(t, "G", "N"), 9);
            Assert.Equal(Provenance.Entered, t.Get("G", "N")!.Provenance);
            Assert.Equal(1e-3 / 600, Val(t, "G", "d"), 15);
            Assert.Equal(650e-9, Val(t, "G", "lambda"), 15);
            // sin θ₁ = 0.39, sin θ₂ = 0.78; the third order (1.17) does not exist.
            Assert.Equal(Math.Asin(0.39), Val(t, "G", "theta1"), 12);
            Assert.Equal(Math.Asin(0.78), Val(t, "G", "theta2"), 12);
            Assert.Null(t.Get("G", "theta3")!.Value);
            Assert.Equal(2, Val(t, "G", "mmax"));
            Assert.Contains(Checks(r, withRay, t), c => c.CheckId == "optics.grating.beyond" && c.ObjectId == "G");
            // The line density sets the drawn orders: the first-order branches leave at sin θ = ±0.39.
            var branches = Fx.Trace(withRay, ray).Paths.Skip(1).Select(p => Fx.Segment(p, 0).D.Y).ToList();
            Assert.Contains(branches, s => Math.Abs(s - 0.39) < 1e-9);
            Assert.Contains(branches, s => Math.Abs(s + 0.78) < 1e-9);
            Assert.DoesNotContain(branches, s => Math.Abs(Math.Abs(s) - 1.17) < 1e-6);
            // The angle input is not offered while a line density is entered.
            Assert.DoesNotContain(r.Type("optics.grating.transmission")!.Actions(withRay.Get("G")!, new RenderContext(withRay)), a => a.Id == "optics.grating.angle");

            // A green laser: θ₁ = asin(600 · 532e-6) = 18.62°; two orders, no note.
            var green = Edits.Replace(Edits.Replace(withRay, withRay.Get("L")!.WithParam("color", "green")), withRay.Get("G")!.WithParam("orders", 2));
            var tg = Solve(r, green);
            Assert.Equal(532e-9, Val(tg, "G", "lambda"), 15);
            Assert.Equal(Math.Asin(600 * 532e-6), Val(tg, "G", "theta1"), 12);
            Assert.DoesNotContain(Checks(r, green, tg), c => c.CheckId == "optics.grating.beyond");
            // An entered wavelength wins over the traced one.
            var entered = Edits.Replace(green, green.Get("G")!.WithParam("lambda", "500 nm"));
            Assert.Equal(Math.Asin(0.3), Val(Solve(r, entered), "G", "theta1"), 12);
        }

        // ------------------------------------------------------------------ irradiance

        [Fact]
        public void DetectorIrradianceIsAnEstimateFromTheReceivingArea()
        {
            var r = Fx.Reg();
            var doc = Fx.Doc(At(r, "optics.source.laser", 50, 100, "L"), At(r, "optics.detector.photodiode", 150, 100, "D"));
            var (bench, _) = Fx.Bundle(doc, doc.Get("L")!, "exit");
            var t = Solve(r, bench);
            // Sample area: a square as high as the drawn photodiode (10 mm) = 100 mm²; 1 mW / 1e-4 m² = 10 W/m².
            Assert.Equal(1e-4, Val(t, "D", "Ad"), 15);
            Assert.Equal(10, Val(t, "D", "Ee"), 9);
            var ee = OpticsValues.DetectorVariables.Single(v => v.Key == "Ee");
            Assert.True(ee.IsReadOnly);
            Assert.Same(Quantities.Irradiance, ee.Kind);
            Assert.Contains("estimate", ee.Name.En);
            Assert.Contains("schätzung", ee.Name.De);
            // An entered area replaces the sample: 1 mW on 5 mm² = 200 W/m².
            var area = Edits.Replace(bench, ValueEdits.Set(r, bench.Get("D")!, "Ad", "5 mm²", Lang.En));
            var ta = Solve(r, area);
            Assert.Empty(ta.Conflicts);
            Assert.Equal(200, Val(ta, "D", "Ee"), 9);
            Assert.Equal("5 mm²", area.Get("D")!.Str("area", ""));
        }

        // ------------------------------------------------------------------ thick lens in a system

        [Fact]
        public void ThickLensPrincipalPlanesFollowFromItsFocalPoints()
        {
            var r = Fx.Reg();
            var lens = At(r, "optics.lens.thick", 100, 100, "T");
            var t = Solve(r, Fx.Doc(lens));
            var g = ThickLensType.Of(lens);
            var foci = g.Foci!.Value;
            // H = F + f from the front vertex, H′ = F′ − f from the rear vertex (mm → m).
            Assert.Equal((foci.Front + g.F - g.XL) / 1000, Val(t, "T", "sH"), 12);
            Assert.Equal((foci.Back - g.F - g.XR) / 1000, Val(t, "T", "sH2"), 12);
            // A symmetric biconvex lens: both planes lie inside, at the same depth.
            Assert.Equal(Val(t, "T", "sH"), -Val(t, "T", "sH2"), 12);
            Assert.True(Val(t, "T", "sH") > 0);
        }

        [Fact]
        public void ThinAndThickLensGiveASystemFocalLengthBetweenThePrincipalPlanes()
        {
            var r = Fx.Reg();
            var thin = At(r, "optics.lens.converging", 100, 100, "L1");
            var thick = At(r, "optics.lens.thick", 160, 100, "T");
            var doc = Fx.Doc(thin, thick);
            var t = Solve(r, doc);
            var g = ThickLensType.Of(thick);
            var foci = g.Foci!.Value;
            var h = 160 + foci.Front + g.F;           // page x of the thick lens's H
            var e = h - 100;                          // H′ of the thin lens is its centre
            var fs = 40 * g.F / (40 + g.F - e);
            Assert.Equal(e / 1000, Val(t, "L1", "d"), 12);
            Assert.Equal(fs / 1000, Val(t, "L1", "fs"), 12);
            var hb = 160 + foci.Back - g.F;           // H′ of the thick lens
            var sF = g.F * (40 - e) / (40 + g.F - e);
            Assert.Equal(sF / 1000, Val(t, "L1", "sF"), 12);
            // The traced rays agree (the thick lens is traced exactly, so paraxially within 0.2 %).
            const double y = 0.2;
            var ray = Fx.Ray(new Vec2(40, 100 - y), new Vec2(60, 100 - y));
            var path = Fx.Trace(Fx.Add(doc, ray), ray.Id).Paths[0];
            var (p, dir) = Fx.Last(path);
            Assert.InRange(y / (dir.Y / dir.X) / fs, 0.998, 1.002);
            var cross = Fx.Intersect(p, dir, new Vec2(0, 100), new Vec2(1, 0));
            Assert.InRange(cross.X - (hb + sF), -0.2, 0.2);

            // The thick lens first: its own values name the thin lens behind it (distance e from its H′).
            var first = Fx.Doc(At(r, "optics.lens.thick", 100, 100, "T"), At(r, "optics.lens.converging", 160, 100, "L2").WithParam("f", 20.0));
            var tf = Solve(r, first);
            var e2 = 160 - (100 + foci.Back - g.F);
            Assert.Equal(e2 / 1000, Val(tf, "T", "e"), 12);
            Assert.Equal(g.F * 20 / (g.F + 20 - e2) / 1000, Val(tf, "T", "fs"), 12);
        }

        // ------------------------------------------------------------------ persistence

        [Fact]
        public void NewEnteredValuesSurviveAJsonRoundTrip()
        {
            var r = Fx.Reg();
            var doc = Fx.Doc(At(r, "optics.grating.transmission", 100, 100, "G").WithParam("lines", "1200").WithParam("lambda", "405 nm"),
                At(r, "optics.detector.photodiode", 150, 300, "D").WithParam("area", "7 mm²"),
                At(r, "optics.mirror.concave", 100, 200, "M").WithParam("g", "100 mm"),
                Equilateral(r).WithParam("dispersion", true).WithParam("dn", 0.05));
            var back = DocumentJson.Deserialize(DocumentJson.Serialize(doc));
            var a = Solve(r, doc);
            var b = Solve(r, back);
            foreach (var (id, key) in new[] { ("G", "theta1"), ("G", "d"), ("D", "Ad"), ("M", "b"), ("P", "vd"), ("P", "dFC") })
                Assert.Equal(Val(a, id, key), Val(b, id, key), 15);
            Assert.Equal("1200", back.Get("G")!.Str("lines", ""));
            // 1200 lines/mm at 405 nm: sin θ₁ = 0.486.
            Assert.Equal(Math.Asin(1200 * 405e-6), Val(b, "G", "theta1"), 12);
        }
    }
}
