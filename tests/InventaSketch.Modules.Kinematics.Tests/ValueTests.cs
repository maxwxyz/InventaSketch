using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Kinematics.Tests
{
    /// <summary>
    /// Element values of the mechanisms (engineering-calculations.md sections 2.6 and 3.4): Grashof class, transmission
    /// angles, rocker swing, time ratio and speeds of the four-bar; stroke, time ratio and slider speed of the slider-crank;
    /// lever swing, time ratio and ram stroke of the quick-return mechanism; the checks kin.transmission and kin.grashof;
    /// the linked characteristic "output angle over input angle". Expected values are hand calculations with the cosine
    /// rule, cross-checked against a sweep of the position solution.
    /// </summary>
    public class ValueTests
    {
        private const double Deg = Math.PI / 180;
        private const double RpmToSi = 2 * Math.PI / 60;

        private static double Acosd(double x) => Math.Acos(x) / Deg;

        private static (Registry R, SketchDocument Doc, SketchObject O) One(string item, Func<SketchObject, SketchObject>? f = null)
        {
            var r = Fx.Reg();
            var o = r.Create(r.Item(item)!, new Vec2(100, 80)) with { Id = "m" };
            if (f is not null) o = f(o);
            var doc = SketchDocument.Create(PageSizes.Slide16x9, "Values");
            return (r, Edits.Add(doc, doc.Pages[0].Id, o), o);
        }

        private static ValueTable Solve(Registry r, SketchDocument doc) => ValueSolver.Solve(doc, r, null, new ValueSolveOptions { BudgetMs = 10000 });

        private static double? Val(ValueTable t, string key) => t.Get("m", key)?.Value;

        // ------------------------------------------------------------------ Grashof

        [Theory]
        [InlineData(40, 12, 36, 30, GrashofClass.CrankRocker)]      // 12 + 40 < 36 + 30, crank shortest
        [InlineData(10, 30, 40, 35, GrashofClass.DoubleCrank)]      // 10 + 40 < 30 + 35, frame shortest (drag link)
        [InlineData(40, 30, 10, 35, GrashofClass.GrashofDoubleRocker)] // coupler shortest
        [InlineData(40, 30, 35, 10, GrashofClass.RockerCrank)]      // rocker shortest
        [InlineData(40, 20, 40, 20, GrashofClass.ChangePoint)]      // parallelogram: 20 + 40 = 20 + 40
        [InlineData(40, 30, 12, 20, GrashofClass.TripleRocker)]     // 12 + 40 > 30 + 20
        public void GrashofClassFollowsTheShortestLink(double d, double a, double b, double c, GrashofClass expected)
        {
            Assert.Equal(expected, Linkages.Classify(d, a, b, c));
            Assert.Equal(expected != GrashofClass.TripleRocker, Linkages.IsGrashof(d, a, b, c));
            var l = new[] { d, a, b, c }.OrderBy(x => x).ToArray();
            Assert.Equal(l[1] + l[2] - l[0] - l[3], Linkages.GrashofMargin(d, a, b, c), 9);
            Assert.False(string.IsNullOrEmpty(Linkages.ClassName(expected).De));
        }

        [Fact]
        public void TheCrankTurnsOnlyForCrankRockerAndDoubleCrank()
        {
            Assert.True(Linkages.CrankTurnsFully(40, 12, 36, 30));
            Assert.True(Linkages.CrankTurnsFully(10, 30, 40, 35));
            Assert.False(Linkages.CrankTurnsFully(40, 30, 10, 35));
            Assert.False(Linkages.CrankTurnsFully(40, 30, 35, 10));
            Assert.False(Linkages.CrankTurnsFully(40, 30, 12, 20));
            // The position solution agrees: a crank-rocker assembles at every angle, a triple rocker does not.
            Assert.All(Enumerable.Range(0, 72), k => Assert.NotNull(Linkages.FourBar(40, 12, 36, 30, k * 5)));
            Assert.Contains(Enumerable.Range(0, 72), k => Linkages.FourBar(40, 30, 12, 20, k * 5) is null);
        }

        // ------------------------------------------------------------------ transmission angle, swing, time ratio

        /// <summary>
        /// Crank-rocker d = 40, a = 12, b = 36, c = 30: the extreme transmission angles are at the crank's positions on the
        /// frame line, |A O4| = d ∓ a: cos μ_min = (36² + 30² − 28²)/(2·36·30) = 0.6537 → 49.18°,
        /// cos μ_max = (36² + 30² − 52²)/(2·36·30) = −0.2352 → 103.60°.
        /// </summary>
        [Fact]
        public void CrankRockerTransmissionAnglesFromTheCosineRule()
        {
            var mu = Linkages.TransmissionAngles(40, 12, 36, 30)!.Value;
            Assert.Equal(Acosd(1412.0 / 2160), mu.Min, 9);
            Assert.Equal(Acosd(-508.0 / 2160), mu.Max, 9);
            Assert.Equal(49.18, mu.Min, 2);
            Assert.Equal(103.60, mu.Max, 2);
            // Sweep of the position solution, both assembly modes.
            foreach (var crossed in new[] { false, true })
            {
                double lo = 180, hi = 0;
                for (var t = 0.0; t < 360; t += 0.05)
                {
                    var m = Linkages.TransmissionAngle(Linkages.FourBar(40, 12, 36, 30, t, crossed)!.Value);
                    lo = Math.Min(lo, m); hi = Math.Max(hi, m);
                }
                Assert.Equal(mu.Min, lo, 3);
                Assert.Equal(mu.Max, hi, 3);
            }
        }

        [Fact]
        public void NonGrashofTransmissionReachesTheAlignedLimits()
        {
            // Triple rocker: at its limits coupler and rocker line up (μ = 0° or 180°).
            var mu = Linkages.TransmissionAngles(40, 30, 12, 20)!.Value;
            Assert.Equal(180, mu.Max, 6);
            Assert.Null(Linkages.TransmissionAngles(100, 10, 10, 10));
        }

        /// <summary>
        /// Rocker swing and time ratio of the same crank-rocker: dead positions with |O2 B| = b ± a = 48 and 24.
        /// ψ = arccos((40² + 30² − 48²)/(2·40·30)) − arccos((40² + 30² − 24²)/(2·40·30)) = 85.316° − 36.711° = 48.61°;
        /// α = arccos((40² + 24² − 30²)/(2·40·24)) − arccos((40² + 48² − 30²)/(2·40·48)) = 48.351° − 38.529° = 9.82°,
        /// Q = (180° + α)/(180° − α) = 1.1154. Cross-checked with a sweep of the rocker angle.
        /// </summary>
        [Fact]
        public void CrankRockerSwingAndTimeRatio()
        {
            var psi = Linkages.RockerSwing(40, 12, 36, 30)!.Value;
            var alpha = Linkages.CrankAngleDifference(40, 12, 36, 30)!.Value;
            Assert.Equal(Acosd(196.0 / 2400) - Acosd(1924.0 / 2400), psi, 9);
            Assert.Equal(Acosd(1276.0 / 1920) - Acosd(3004.0 / 3840), alpha, 9);
            Assert.Equal(48.61, psi, 2);
            Assert.Equal(9.82, alpha, 2);
            Assert.Equal((180 + alpha) / (180 - alpha), Linkages.TimeRatio(alpha), 12);
            Assert.Equal(1.1154, Linkages.TimeRatio(alpha), 4);

            // Sweep: extremes of the rocker angle and the crank angles where they occur.
            double min = double.MaxValue, max = double.MinValue, atMin = 0, atMax = 0;
            for (var t = 0.0; t < 360; t += 0.01)
            {
                var o = Linkages.OutputAngle(Linkages.FourBar(40, 12, 36, 30, t)!.Value);
                if (o < min) { min = o; atMin = t; }
                if (o > max) { max = o; atMax = t; }
            }
            Assert.Equal(psi, max - min, 3);
            var one = Geo.NormDeg(atMax - atMin);
            var slow = Math.Max(one, 360 - one);
            Assert.Equal(Linkages.TimeRatio(alpha), slow / (360 - slow), 3);
            // Only a crank-rocker has a swing and a time ratio.
            Assert.Null(Linkages.RockerSwing(10, 30, 40, 35));
            Assert.Null(Linkages.CrankAngleDifference(40, 30, 12, 20));
        }

        [Fact]
        public void VelocityRatioMatchesTheDerivativeOfThePosition()
        {
            foreach (var crossed in new[] { false, true })
                for (var t = -170.0; t < 180; t += 20)
                {
                    var p = Linkages.FourBar(40, 12, 36, 30, t, crossed)!.Value;
                    const double h = 1e-4;
                    var o1 = Linkages.OutputAngle(Linkages.FourBar(40, 12, 36, 30, t + h, crossed)!.Value);
                    var o0 = Linkages.OutputAngle(Linkages.FourBar(40, 12, 36, 30, t - h, crossed)!.Value);
                    Assert.Equal((o1 - o0) / (2 * h), Linkages.VelocityRatio(p), 5);
                }
        }

        // ------------------------------------------------------------------ slider-crank and quick return

        [Theory]
        [InlineData(12, 36, 0)]
        [InlineData(12, 36, 6)]
        [InlineData(20, 50, -8)]
        public void SliderStrokeAndTimeRatioMatchTheSweep(double r, double l, double e)
        {
            var stroke = Linkages.SliderStroke(r, l, e)!.Value;
            Assert.Equal(Math.Sqrt((l + r) * (l + r) - e * e) - Math.Sqrt((l - r) * (l - r) - e * e), stroke, 12);
            if (e == 0) Assert.Equal(2 * r, stroke, 12);
            double min = double.MaxValue, max = double.MinValue, atMin = 0, atMax = 0;
            for (var t = 0.0; t < 360; t += 0.01)
            {
                var x = Linkages.SliderCrank(r, l, e, t)!.Value.B.X;
                if (x < min) { min = x; atMin = t; }
                if (x > max) { max = x; atMax = t; }
            }
            Assert.Equal(stroke, max - min, 6);
            var one = Geo.NormDeg(atMax - atMin);
            var slow = Math.Max(one, 360 - one);
            Assert.Equal(Linkages.TimeRatio(Linkages.SliderCrankAngleDifference(r, l, e)!.Value), slow / (360 - slow), 3);
            // Slider speed factor = dx/dφ.
            const double h = 1e-5;
            for (var t = -150.0; t < 180; t += 30)
            {
                var d = (Linkages.SliderCrank(r, l, e, t + h)!.Value.B.X - Linkages.SliderCrank(r, l, e, t - h)!.Value.B.X) / (2 * h * Deg);
                Assert.Equal(d, Linkages.SliderSpeedFactor(r, l, e, t), 4);
            }
            Assert.Equal(90 - Math.Asin((r + Math.Abs(e)) / l) / Deg, Linkages.SliderMinTransmission(r, l, e)!.Value, 9);
        }

        [Fact]
        public void QuickReturnLeverSwingAndTimeRatio()
        {
            // r = 10, d = 20: the crank is at right angles to the lever in the dead positions, cos γ = r/d → γ = 60°,
            // forward 240°, return 120°: Q = 2, lever swing 2·arcsin(1/2) = 60°.
            Assert.Equal(60, Linkages.LeverSwing(20, 10)!.Value, 9);
            Assert.Equal(60, Linkages.LeverCrankAngleDifference(20, 10)!.Value, 9);
            Assert.Equal(2, Linkages.TimeRatio(60), 12);
            Assert.Null(Linkages.LeverSwing(20, 25));
            var ext = Linkages.Extremes(a => Linkages.QuickReturnRam(20, 10, 40, 16, 44, a))!.Value;
            double min = double.MaxValue, max = double.MinValue;
            for (var t = 0.0; t < 360; t += 0.01)
            {
                var x = Linkages.QuickReturnRam(20, 10, 40, 16, 44, t)!.Value;
                min = Math.Min(min, x); max = Math.Max(max, x);
            }
            Assert.Equal(min, ext.Min, 6);
            Assert.Equal(max, ext.Max, 6);
        }

        // ------------------------------------------------------------------ the solver

        [Fact]
        public void FourBarValuesSolveFromTheParameters()
        {
            var (r, doc, _) = One("kin.fourbar");
            var t = Solve(r, doc);
            Assert.Equal(Provenance.Entered, t.Get("m", "a")!.Provenance);
            Assert.Equal(0.012, Val(t, "a")!.Value, 12);
            Assert.Equal(Acosd(1412.0 / 2160), Val(t, "mumin")!.Value / Deg, 9);
            Assert.Equal(Acosd(-508.0 / 2160), Val(t, "mumax")!.Value / Deg, 9);
            Assert.Equal(48.61, Val(t, "psi")!.Value / Deg, 2);
            Assert.Equal(1.1154, Val(t, "Q")!.Value, 4);
            Assert.Equal(Provenance.Derived, t.Get("m", "Q")!.Provenance);
            Assert.Equal(0.014, Val(t, "dG")!.Value, 12); // (36 + 30) − (12 + 40) = 14 mm
            Assert.Equal(-60, Val(t, "phi")!.Value / Deg, 9);
            var pose = Linkages.FourBar(40, 12, 36, 30, -60)!.Value;
            Assert.Equal(Linkages.VelocityRatio(pose), Val(t, "i")!.Value, 9);
            // Without an input speed the output speed is unknown.
            Assert.Null(Val(t, "w"));
            // Card values on the label: transmission angles, swing, time ratio.
            var label = ValueLabels.Text(doc.Get("m")!, t, Lang.En);
            Assert.Contains("μ_min = 49.2 °", label);
            Assert.Contains("Q = 1.12", label);
            // Explanation names the cosine rule.
            Assert.Contains(t.Explain("m", "mumin", Lang.En), s => s.RelationId == "kin.fourbar.mumin" && s.Source == "S24");
        }

        [Fact]
        public void InputSpeedGivesTheOutputSpeedAtThisPosition()
        {
            var (r, doc, o) = One("kin.fourbar");
            doc = Edits.Replace(doc, ValueEdits.Set(r, o, "n", "60 min⁻¹", Lang.En));
            var t = Solve(r, doc);
            Assert.Equal(2 * Math.PI, Val(t, "n")!.Value, 9);
            var i = Linkages.VelocityRatio(Linkages.FourBar(40, 12, 36, 30, -60)!.Value);
            Assert.Equal(i * 2 * Math.PI, Val(t, "w")!.Value, 9);
            // The crossed assembly mode has its own ratio.
            var crossed = Edits.Replace(doc, doc.Get("m")!.WithParam("branch", "crossed"));
            var tc = Solve(r, crossed);
            Assert.Equal(Linkages.VelocityRatio(Linkages.FourBar(40, 12, 36, 30, -60, true)!.Value) * 2 * Math.PI, Val(tc, "w")!.Value, 9);
        }

        [Fact]
        public void EnteringALinkLengthChangesTheDrawing()
        {
            var (r, doc, o) = One("kin.fourbar");
            var next = ValueEdits.Set(r, o, "a", "10 mm", Lang.En);
            Assert.Equal(10, next.Num("crank", 0), 9);
            var t = Solve(r, Edits.Replace(doc, next));
            Assert.Equal(Acosd((36.0 * 36 + 30 * 30 - 30 * 30) / (2 * 36.0 * 30)), Val(t, "mumin")!.Value / Deg, 9);
        }

        [Fact]
        public void SliderCrankValues()
        {
            var (r, doc, o) = One("kin.slidercrank.offset"); // r 12, l 36, e 6
            doc = Edits.Replace(doc, ValueEdits.Set(r, o, "n", "120 min⁻¹", Lang.En));
            var t = Solve(r, doc);
            Assert.Equal(Linkages.SliderStroke(12, 36, 6)!.Value / 1000, Val(t, "H")!.Value, 12);
            Assert.Equal(Linkages.TimeRatio(Linkages.SliderCrankAngleDifference(12, 36, 6)!.Value), Val(t, "Q")!.Value, 9);
            Assert.Equal(90 - Math.Asin(18.0 / 36) / Deg, Val(t, "mumin")!.Value / Deg, 9); // 60°
            Assert.Equal(4 * Math.PI * Linkages.SliderSpeedFactor(12, 36, 6, -60) / 1000, Val(t, "v")!.Value, 9);
            var (r2, doc2, _) = One("kin.slidercrank");
            var t2 = Solve(r2, doc2);
            Assert.Equal(0.024, Val(t2, "H")!.Value, 12); // 2r
            Assert.Equal(1, Val(t2, "Q")!.Value, 12);
        }

        [Fact]
        public void QuickReturnAndScotchYokeValues()
        {
            var (r, doc, _) = One("kin.quickreturn");
            var t = Solve(r, doc);
            Assert.Equal(2, Val(t, "Q")!.Value, 9);
            Assert.Equal(60, Val(t, "psi")!.Value / Deg, 9);
            var ext = Linkages.Extremes(a => Linkages.QuickReturnRam(20, 10, 40, 16, 44, a))!.Value;
            Assert.Equal((ext.Max - ext.Min) / 1000, Val(t, "H")!.Value, 9);
            var (r2, doc2, o2) = One("kin.scotchyoke");
            doc2 = Edits.Replace(doc2, ValueEdits.Set(r2, o2, "n", "60 min⁻¹", Lang.En));
            var t2 = Solve(r2, doc2);
            Assert.Equal(0.024, Val(t2, "H")!.Value, 12);
            Assert.Equal(-2 * Math.PI * 0.012 * Math.Sin(-60 * Deg), Val(t2, "v")!.Value, 9);
        }

        // ------------------------------------------------------------------ checks

        private static IReadOnlyList<CheckFinding> Findings(Registry r, SketchDocument doc)
            => ValueSolver.Checks(Solve(r, doc), r, new RenderContext(doc).For(r));

        [Fact]
        public void TransmissionCheckWarnsBelowFortyDegrees()
        {
            var (r, doc, _) = One("kin.fourbar");
            Assert.DoesNotContain(Findings(r, doc), f => f.CheckId == "kin.transmission");
            // Crank 20: μ_min = arccos((36² + 30² − 20²)/(2·36·30)) = 33.7° < 40°.
            var (r2, doc2, _) = One("kin.fourbar", o => o.WithParam("crank", 20));
            var f2 = Assert.Single(Findings(r2, doc2), f => f.CheckId == "kin.transmission");
            Assert.Equal(CheckSeverity.Warning, f2.Severity);
            Assert.Equal("S24", f2.Source);
            Assert.False(string.IsNullOrEmpty(f2.Message.De));
            Assert.DoesNotContain(Findings(r2, doc2), f => f.CheckId == "kin.grashof");
            // Slider-crank: rod obliquity arcsin((12 + 15)/30) = 64.2° → μ_min = 25.8°.
            var (r3, doc3, _) = One("kin.slidercrank", o => o.WithParam("rod", 30).WithParam("offset", 15));
            Assert.Contains(Findings(r3, doc3), f => f.CheckId == "kin.transmission");
        }

        [Fact]
        public void GrashofCheckHintsWhenTheCrankCannotTurn()
        {
            var (r, doc, _) = One("kin.fourbar", o => o.WithParam("ground", 40).WithParam("crank", 30).WithParam("coupler", 12).WithParam("rocker", 20).WithParam("angle", 0));
            var f = Assert.Single(Findings(r, doc), x => x.CheckId == "kin.grashof");
            Assert.Equal(CheckSeverity.Hint, f.Severity);
        }

        // ------------------------------------------------------------------ Grashof badge

        [Fact]
        public void GrashofBadgeNamesTheClass()
        {
            var r = Fx.Reg();
            var t = r.Type("kin.mech.fourbar")!;
            var o = r.Create(r.Item("kin.fourbar.grashof")!, Vec2.Zero);
            Assert.Contains(t.Render(o, Fx.Ctx()), p => p is TextPrim tp && tp.Tag == KinTags.Grashof && tp.Text == "Crank-rocker");
            Assert.Contains(t.Render(o, Fx.Ctx(lang: Lang.De)), p => p is TextPrim tp && tp.Tag == KinTags.Grashof && tp.Text == "Kurbelschwinge");
            var drag = o.WithParam("ground", 10).WithParam("crank", 30).WithParam("coupler", 40).WithParam("rocker", 35).WithParam("dof", true);
            var prims = t.Render(drag, Fx.Ctx());
            Assert.Contains(prims, p => p is TextPrim tp && tp.Text == "Double-crank");
            Assert.Contains(prims, p => p is TextPrim tp && tp.Text == "DOF = 1");
            Fx.AssertInsideBox(prims, drag.Size, "badges");
            var plain = r.Create(r.Item("kin.fourbar")!, Vec2.Zero);
            Assert.DoesNotContain(t.Render(plain, Fx.Ctx()), p => p.Tag == KinTags.Grashof);
            var toggle = t.Actions(plain, Lang.En).Single(a => a.Id == "kin.grashof.badge");
            Assert.True(toggle.Apply(plain).Flag("grashof", false));
        }

        // ------------------------------------------------------------------ linked characteristic

        [Fact]
        public void FourBarOffersTheTransferCharacteristic()
        {
            var (r, doc, o) = One("kin.fourbar");
            var type = (ICharacteristics)r.TypeOf(o);
            var info = Assert.Single(type.CharacteristicSets(o, new RenderContext(doc)));
            Assert.Equal("transfer", info.Key);
            var set = Characteristics.Resolve(r, doc, Characteristics.Link("m", "transfer"))!;
            Assert.Equal(2, set.Panels.Count);
            var output = set.Curve("output")!;
            Assert.Equal(361, output.Points.Count);
            var swing = output.Points.Max(p => p.Y) - output.Points.Min(p => p.Y);
            Assert.Equal(Linkages.RockerSwing(40, 12, 36, 30)!.Value, swing, 1);
            var mu = set.Curve("mu")!;
            Assert.Equal(1, mu.Panel);
            Assert.Equal(49.18, mu.Points.Min(p => p.Y), 1);
            Assert.Contains(set.Lines, l => !l.Vertical && l.Value == 40 && l.Panel == 1);
            var point = set.Point("position")!;
            Assert.Equal(-60, point.X, 9);
            Assert.Equal(Linkages.OutputAngle(Linkages.FourBar(40, 12, 36, 30, -60)!.Value), point.Y, 6);
            Assert.True(point.Draggable);
            // Dragging the point enters a new input angle.
            var moved = Characteristics.Apply(r, doc, point.Write!(-30), Lang.En);
            Assert.Equal(-30, moved.Get("m")!.Num("angle", 0), 6);
            // Non-assemblable positions are gaps.
            var (r2, doc2, _) = One("kin.fourbar", x => x.WithParam("ground", 40).WithParam("crank", 30).WithParam("coupler", 12).WithParam("rocker", 20).WithParam("angle", 0));
            var gaps = Characteristics.Resolve(r2, doc2, Characteristics.Link("m", "transfer"))!;
            Assert.Contains(gaps.Curve("output")!.Points, p => double.IsNaN(p.Y));
            Assert.NotEmpty(gaps.Notes);
            // Dependents: linked charts are redrawn with the element.
            Assert.True(r.TypeOf(o).HasDependents);
        }
    }
}
