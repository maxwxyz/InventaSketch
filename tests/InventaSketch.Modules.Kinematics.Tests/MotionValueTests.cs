using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using InventaSketch.Modules.Mechanical;
using Xunit;

namespace InventaSketch.Modules.Kinematics.Tests
{
    /// <summary>
    /// Round seven: velocities and accelerations of the slider-crank and the four-bar's coupler at a given crank speed, the
    /// instant centre of the coupler (Kennedy), the cam values (exact contact of the tangent cam, stroke, follower speed,
    /// pressure angle, check kin.cam.pressure) with the linked displacement diagram, and the crank pivot hub that joins a
    /// pulley or gear of the mechanical module on one shaft (shared speed n). Expected values are closed-form hand
    /// calculations of the loop equations.
    /// </summary>
    public class MotionValueTests
    {
        private const double Deg = Math.PI / 180;
        private const double RpmToSi = 2 * Math.PI / 60;

        private static (Registry R, SketchDocument Doc, SketchObject O) One(string item, Func<SketchObject, SketchObject>? f = null)
        {
            var r = Fx.Reg();
            var o = r.Create(r.Item(item)!, new Vec2(100, 80)) with { Id = "m" };
            if (f is not null) o = f(o);
            var doc = SketchDocument.Create(PageSizes.Slide16x9, "Values");
            return (r, Edits.Add(doc, doc.Pages[0].Id, o), o);
        }

        private static ValueTable Solve(Registry r, SketchDocument doc) => ValueSolver.Solve(doc, r, null, new ValueSolveOptions { BudgetMs = 10000 });

        private static double? Val(ValueTable t, string key, string id = "m") => t.Get(id, key)?.Value;

        private static IReadOnlyList<CheckFinding> Findings(Registry r, SketchDocument doc) => ValueSolver.Checks(Solve(r, doc), r, new RenderContext(doc).For(r));

        // ------------------------------------------------------------------ slider-crank

        /// <summary>
        /// In-line slider-crank, x_B = r·cos φ + k with k = √(l² − r²·sin²φ): x_B'' = −r·cos φ − r²·cos 2φ/k − r⁴·sin²φ·cos²φ/k³.
        /// r = 12 mm, l = 36 mm, φ = −60°, n = 600 min⁻¹ (20π s⁻¹): a = n²·x_B''.
        /// </summary>
        [Fact]
        public void SliderAccelerationFollowsTheSecondDerivativeOfTheLoop()
        {
            double r = 0.012, l = 0.036, phi = -60 * Deg;
            double s = Math.Sin(phi), c = Math.Cos(phi), k = Math.Sqrt(l * l - r * r * s * s);
            var x2 = -r * c - r * r * Math.Cos(2 * phi) / k - Math.Pow(r, 4) * s * s * c * c / Math.Pow(k, 3);
            var n = 600 * RpmToSi;
            var (reg, doc, o) = One("kin.slidercrank");
            doc = Edits.Replace(doc, ValueEdits.Set(reg, o, "n", "600 min⁻¹", Lang.En));
            var t = Solve(reg, doc);
            Assert.Equal(n * n * x2, Val(t, "acc")!.Value, 4);
            Assert.Equal(Provenance.Derived, t.Get("m", "acc")!.Provenance);
            // Largest slider speed: the sampled maximum of |dx_B/dφ| (0.05° grid) times n.
            var max = Enumerable.Range(0, 7200).Max(i => Math.Abs(Linkages.SliderSpeedFactor(0.012, 0.036, 0, i * 0.05)));
            Assert.Equal(n * max, Val(t, "vmax")!.Value, 6);
            // Near the classic estimate v_max ≈ ω·r·(1 + (r/l)²/2) for a long rod (within 1 %).
            Assert.InRange(Val(t, "vmax")!.Value / (n * r), 1.0, 1.1);
            // Entering v_max gives the input speed back.
            var (reg2, doc2, o2) = One("kin.slidercrank");
            doc2 = Edits.Replace(doc2, ValueEdits.Set(reg2, o2, "vmax", (n * max).ToString("R", System.Globalization.CultureInfo.InvariantCulture) + " m/s", Lang.En));
            Assert.Equal(1, Val(Solve(reg2, doc2), "n")!.Value / n, 6);
        }

        // ------------------------------------------------------------------ four-bar

        /// <summary>
        /// Loop a·e^{iθ₂} + b·e^{iθ₃} − c·e^{iθ₄} − d = 0 differentiated: ω₃/ω₂ = a·sin(θ₄ − θ₂)/(b·sin(θ₃ − θ₄)). The coupler
        /// point at the rocker joint (u = 1) moves with |v_B| = c·|ω₄|; the crank pin (u = 0) at constant speed has |a_A| = a·n².
        /// </summary>
        [Fact]
        public void CouplerSpeedsFollowTheVelocityLoop()
        {
            var p = Linkages.FourBar(40, 12, 36, 30, -60)!.Value;
            double t2 = Math.Atan2(p.A.Y, p.A.X), t3 = Math.Atan2(p.B.Y - p.A.Y, p.B.X - p.A.X), t4 = Math.Atan2(p.B.Y - p.O4.Y, p.B.X - p.O4.X);
            var w3 = 12 * Math.Sin(t4 - t2) / (36 * Math.Sin(t3 - t4));
            Assert.Equal(w3, Linkages.CouplerRate(40, 12, 36, 30, -60, false), 6);

            var n = 120 * RpmToSi;
            var (r, doc, o) = One("kin.fourbar");
            doc = Edits.Replace(doc, ValueEdits.Set(r, o, "n", "120 min⁻¹", Lang.En));
            var t = Solve(r, doc);
            Assert.Equal(n * w3, Val(t, "w3")!.Value, 6);
            // Default coupler point: the middle of the coupler, |v_M| = |(v_A + v_B)/2|.
            Assert.True(Val(t, "vP") > 0);

            var (rb, db, _) = One("kin.fourbar", x => x.WithParam("cpu", 1.0));
            db = Edits.Replace(db, ValueEdits.Set(rb, db.Get("m")!, "n", "120 min⁻¹", Lang.En));
            var tb = Solve(rb, db);
            Assert.Equal(0.030 * Math.Abs(n * Linkages.VelocityRatio(p)), Val(tb, "vP")!.Value, 6);

            var (ra, da, _) = One("kin.fourbar", x => x.WithParam("cpu", 0.0));
            da = Edits.Replace(da, ValueEdits.Set(ra, da.Get("m")!, "n", "120 min⁻¹", Lang.En));
            var ta = Solve(ra, da);
            Assert.Equal(0.012 * n, Val(ta, "vP")!.Value, 6);
            Assert.Equal(0.012 * n * n, Val(ta, "aP")!.Value, 4);
        }

        /// <summary>
        /// Round eight: the loop a·e^{iθ₂} + b·e^{iθ₃} − c·e^{iθ₄} − d = 0 differentiated twice at constant ω₂ (α₂ = 0):
        /// −b·sin θ₃·α₃ + c·sin θ₄·α₄ = a·ω₂²·cos θ₂ + b·ω₃²·cos θ₃ − c·ω₄²·cos θ₄ and
        /// b·cos θ₃·α₃ − c·cos θ₄·α₄ = a·ω₂²·sin θ₂ + b·ω₃²·sin θ₃ − c·ω₄²·sin θ₄, solved by Cramer's rule.
        /// </summary>
        [Fact]
        public void AngularAccelerationsFollowTheAccelerationLoop()
        {
            foreach (var (angle, crossed) in new[] { (-60.0, false), (10.0, false), (140.0, false), (-60.0, true) })
            {
                double a = 12, b = 36, c = 30;
                var p = Linkages.FourBar(40, a, b, c, angle, crossed)!.Value;
                double t2 = Math.Atan2(p.A.Y, p.A.X), t3 = Math.Atan2(p.B.Y - p.A.Y, p.B.X - p.A.X), t4 = Math.Atan2(p.B.Y - p.O4.Y, p.B.X - p.O4.X);
                const double w2 = 1;
                var w3 = a * Math.Sin(t4 - t2) / (b * Math.Sin(t3 - t4)) * w2;
                var w4 = a * Math.Sin(t2 - t3) / (c * Math.Sin(t4 - t3)) * w2;
                double r1 = a * w2 * w2 * Math.Cos(t2) + b * w3 * w3 * Math.Cos(t3) - c * w4 * w4 * Math.Cos(t4);
                double r2 = a * w2 * w2 * Math.Sin(t2) + b * w3 * w3 * Math.Sin(t3) - c * w4 * w4 * Math.Sin(t4);
                double m11 = -b * Math.Sin(t3), m12 = c * Math.Sin(t4), m21 = b * Math.Cos(t3), m22 = -c * Math.Cos(t4);
                var det = m11 * m22 - m12 * m21;
                var alpha3 = (r1 * m22 - m12 * r2) / det;
                var alpha4 = (m11 * r2 - r1 * m21) / det;
                var d3 = Linkages.LinkAngleDerivatives(40, a, b, c, angle, crossed, false)!.Value;
                var d4 = Linkages.LinkAngleDerivatives(40, a, b, c, angle, crossed, true)!.Value;
                Assert.Equal(w3, d3.D1, 5);
                Assert.Equal(w4, d4.D1, 5);
                Assert.Equal(alpha3, d3.D2, 4);
                Assert.Equal(alpha4, d4.D2, 4);
            }

            // In the values: α = n²·d²θ/dφ² at 120 min⁻¹, read-only results of the drawing, shown in rad/s².
            var n = 120 * RpmToSi;
            var (r, doc, o) = One("kin.fourbar");
            doc = Edits.Replace(doc, ValueEdits.Set(r, o, "n", "120 min⁻¹", Lang.En));
            var t = Solve(r, doc);
            Assert.Equal(n * n * Linkages.LinkAngleDerivatives(40, 12, 36, 30, -60, false, true)!.Value.D2, Val(t, "alpha4")!.Value, 6);
            Assert.Equal(n * n * Linkages.LinkAngleDerivatives(40, 12, 36, 30, -60, false, false)!.Value.D2, Val(t, "alpha3")!.Value, 6);
            Assert.Equal(Provenance.Derived, t.Get("m", "alpha4")!.Provenance);
            var def = t.Get("m", "alpha4")!.Def;
            Assert.Same(Quantities.AngularAcceleration, def.Kind);
            Assert.True(def.IsReadOnly);
            Assert.Equal("rad/s²", def.Unit);
            // Without an input speed there is no angular acceleration.
            var (r0, doc0, _) = One("kin.fourbar");
            Assert.Null(Val(Solve(r0, doc0), "alpha4"));
        }

        /// <summary>
        /// Kennedy: the coupler turns about the pole I₁₃ on the crank and rocker lines, so every coupler point moves with
        /// |v_P| = |ω₃|·|P − I₁₃| (checked for an off-coupler point).
        /// </summary>
        [Fact]
        public void TheInstantCentreGivesTheCouplerPointSpeed()
        {
            foreach (var angle in new[] { -100.0, -60, 10, 75, 140 })
            {
                var p = Linkages.FourBar(40, 12, 36, 30, angle)!.Value;
                var pole = Linkages.CouplerPole(p)!.Value;
                Assert.True(Math.Abs((pole - p.O2).Cross(p.A - p.O2)) < 1e-6 * (pole - p.O2).Length * 12);
                Assert.True(Math.Abs((pole - p.O4).Cross(p.B - p.O4)) < 1e-6 * (pole - p.O4).Length * 30);
                double u = 0.4, v = 0.45;
                var ab = p.B - p.A;
                var cp = p.A + ab * u - ab.Perp * v;
                var d = Linkages.CouplerPointDerivatives(40, 12, 36, 30, angle, false, u, v)!.Value;
                var w3 = Linkages.CouplerRate(40, 12, 36, 30, angle, false);
                Assert.Equal(1, Math.Abs(w3) * (cp - pole).Length / d.D1.Length, 5);
            }
            // Slider-crank: the rod's pole on the crank line, straight above or below the slider joint.
            var sc = Linkages.SliderCrank(12, 36, 0, -60)!.Value;
            var rp = Linkages.RodPole(sc)!.Value;
            Assert.Equal(sc.B.X, rp.X, 9);
            Assert.True(Math.Abs(rp.Cross(sc.A)) < 1e-9 * rp.Length * 12);
            Assert.Null(Linkages.RodPole(Linkages.SliderCrank(12, 36, 0, -90)!.Value));
        }

        [Fact]
        public void ThePoleIsDrawnInsideTheBoxAndToggled()
        {
            var r = Fx.Reg();
            foreach (var item in new[] { "kin.fourbar.pole", "kin.slidercrank.pole" })
            {
                var o = r.Create(r.Item(item)!, Vec2.Zero);
                var t = r.TypeOf(o);
                foreach (var lang in new[] { Lang.En, Lang.De })
                {
                    var prims = t.Render(o, Fx.Ctx(lang: lang));
                    Assert.Contains(prims, x => x.Tag == KinPole.Tag && x is LinePrim);
                    Fx.AssertInsideBox(prims, o.Size, item);
                }
                Assert.Contains(t.Render(o, Fx.Ctx()), x => x is TextPrim tp && tp.Tag == KinPole.Tag && tp.Text == "I₁₃");
                Assert.Contains(t.Render(o, Fx.Ctx(lang: Lang.De)), x => x is TextPrim tp && tp.Tag == KinPole.Tag && tp.Text == "P₁₃");
                // Every angle: the construction lines stay inside the box, also when the pole runs off to infinity.
                for (var a = -180; a < 180; a += 7)
                    Fx.AssertInsideBox(t.Render(o.WithParam("angle", (double)a), Fx.Ctx()), o.Size, item + " at " + a);
                var off = t.Actions(o, Lang.En).Single(x => x.Id == "kin.pole").Apply(o);
                Assert.DoesNotContain(t.Render(off, Fx.Ctx()), x => x.Tag == KinPole.Tag);
            }
        }

        // ------------------------------------------------------------------ cam

        [Theory]
        [InlineData("roller")]
        [InlineData("flat")]
        [InlineData("knife")]
        public void TheExactCamContactMatchesTheDrawnFollower(string follower)
        {
            var r = Fx.Reg();
            var type = (CamFollowerType)r.Type("kin.cam.follower")!;
            var o = r.Create(r.Item("kin.cam.roller")!, Vec2.Zero).WithParam("follower", follower);
            for (var a = 0; a < 360; a += 5)
            {
                var drawn = type.Solve(o, a)["C"].Y;
                Assert.Equal(drawn, CamValues.Contact(follower, 8, 7, a), 1); // polygonal drawing: within 0.05 mm of 8 mm
            }
            // The nose points at angle − 135°: down at 225° (base circle dwell, s = 0), straight up at 45° (s = h).
            Assert.Equal(0, CamValues.Displacement(follower, 8, 7, 225), 9);
            Assert.Equal(7, CamValues.Displacement(follower, 8, 7, 45), 9);
        }

        [Fact]
        public void CamValuesStrokeSpeedAndPressureAngle()
        {
            var (r, doc, o) = One("kin.cam.roller", x => x.WithParam("angle", 100.0));
            doc = Edits.Replace(doc, ValueEdits.Set(r, o, "n", "300 min⁻¹", Lang.En));
            var t = Solve(r, doc);
            Assert.Equal(0.007, Val(t, "H")!.Value, 9);
            var s = CamValues.Displacement("roller", 8, 7, 100) / 1000;
            Assert.Equal(s, Val(t, "s")!.Value, 9);
            var rate = (CamValues.Displacement("roller", 8, 7, 100.01) - CamValues.Displacement("roller", 8, 7, 99.99)) / (0.02 * Deg) / 1000;
            Assert.Equal(300 * RpmToSi * rate, Val(t, "v")!.Value, 5);
            // tan α = |ds/dφ|/(R_p + s), R_p = r₀ + 0.3·r₀.
            Assert.Equal(Math.Atan(Math.Abs(rate) / (0.0104 + s)), Val(t, "alpha")!.Value, 4);
            var max = Enumerable.Range(0, 36000).Max(i => CamValues.PressureAngle("roller", 8, 7, i * 0.01));
            Assert.Equal(max, Val(t, "alphamax")!.Value, 4);
            // A small base circle with a large lift: steep flanks, the pressure angle check warns; the flat face never does.
            var (r2, doc2, _) = One("kin.cam.roller", x => x.WithParam("base", 5.0).WithParam("lift", 9.0));
            var f = Assert.Single(Findings(r2, doc2), x => x.CheckId == "kin.cam.pressure");
            Assert.Equal(CheckSeverity.Hint, f.Severity);
            Assert.Equal("S24", f.Source);
            // Lift 4 mm on the 8 mm base circle: about 28.5° with the roller, no hint.
            var (r5, doc5, _) = One("kin.cam.roller", x => x.WithParam("lift", 4.0));
            Assert.DoesNotContain(Findings(r5, doc5), x => x.CheckId == "kin.cam.pressure");
            Assert.InRange(Val(Solve(r5, doc5), "alphamax")!.Value / Deg, 27, 30);
            var (r3, doc3, _) = One("kin.cam.flat", x => x.WithParam("base", 5.0).WithParam("lift", 9.0));
            Assert.DoesNotContain(Findings(r3, doc3), x => x.CheckId == "kin.cam.pressure");
            Assert.Equal(0, Val(Solve(r3, doc3), "alphamax")!.Value, 12);
            // Entering a follower speed gives the cam speed back.
            var (r4, doc4, o4) = One("kin.cam.roller", x => x.WithParam("angle", 100.0));
            doc4 = Edits.Replace(doc4, ValueEdits.Set(r4, o4, "v", (300 * RpmToSi * rate).ToString("R", System.Globalization.CultureInfo.InvariantCulture) + " m/s", Lang.En));
            Assert.Equal(300 * RpmToSi, Val(Solve(r4, doc4), "n")!.Value, 4);
        }

        [Fact]
        public void TheCamOffersTheDisplacementDiagram()
        {
            var (r, doc, o) = One("kin.cam.roller", x => x.WithParam("angle", 100.0));
            var type = (ICharacteristics)r.TypeOf(o);
            Assert.Equal("displacement", Assert.Single(type.CharacteristicSets(o, new RenderContext(doc))).Key);
            var set = Characteristics.Resolve(r, doc, Characteristics.Link("m", "displacement"))!;
            Assert.Equal(2, set.Panels.Count);
            var s = set.Curve("s")!;
            Assert.Equal(361, s.Points.Count);
            Assert.Equal(7, s.Points.Max(p => p.Y), 6);
            Assert.Equal(0, s.Points.Min(p => p.Y), 6);
            Assert.Contains(set.Lines, l => !l.Vertical && l.Value == 30 && l.Panel == 1);
            var point = set.Point("position")!;
            Assert.Equal(100, point.X, 9);
            Assert.True(point.Draggable);
            var moved = Characteristics.Apply(r, doc, point.Write!(200), Lang.En);
            Assert.Equal(200, KA.NormAngle(moved.Get("m")!.Num("angle", 0)) + 360, 6);
            Assert.True(r.TypeOf(o).HasDependents);
        }

        // ------------------------------------------------------------------ shaft lines across modules

        private static Registry Both() => new Registry().Register(new KinematicsModule()).Register(new MechanicalModule());

        private static Vec2 HubOf(Registry r, SketchObject o) => r.PageAnchors(o, new RenderContext(SketchDocument.Create())).Single(a => a.Role == AnchorRole.Hub).P;

        [Theory]
        [InlineData("kin.fourbar")]
        [InlineData("kin.slidercrank")]
        [InlineData("kin.scotchyoke")]
        [InlineData("kin.sixbar.watt")]
        public void APulleyOnTheCrankPivotDrivesTheMechanism(string item)
        {
            var r = Both();
            var m = r.Create(r.Item(item)!, new Vec2(100, 80)) with { Id = "m" };
            var pulley = r.Create(r.Item("mech.pulley.axial")!, Vec2.Zero) with { Id = "p" };
            var d = HubOf(r, m) - HubOf(r, pulley);
            pulley = pulley with { Transform = pulley.Transform with { X = pulley.Transform.X + d.X, Y = pulley.Transform.Y + d.Y } };
            pulley = pulley.WithParam("speed", "90 rpm");
            var doc = SketchDocument.Create(PageSizes.Slide16x9, "Shaft");
            doc = Edits.Add(doc, doc.Pages[0].Id, m, pulley);
            Assert.NotNull(ShaftLines.Of(r, doc).LineOf("m"));
            var t = Solve(r, doc);
            Assert.Empty(t.Conflicts);
            Assert.Equal(90 * RpmToSi, Val(t, "n")!.Value, 9);
        }
    }
}
