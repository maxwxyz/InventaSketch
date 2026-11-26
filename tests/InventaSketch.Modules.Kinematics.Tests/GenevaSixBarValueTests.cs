using System;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Kinematics.Tests
{
    /// <summary>
    /// Element values of the Geneva drive (indexing angle 360°/z, driver angles in motion and at rest, motion/dwell ratio,
    /// wheel speed at the current position) and of the six-bar linkage (output angle, transmission angles of both loops,
    /// velocity ratio and output speed). Expected values are hand calculations (textbook formulas, cosine rule), cross-checked
    /// against the position solution.
    /// </summary>
    public class GenevaSixBarValueTests
    {
        private const double Deg = Math.PI / 180;
        private const double RpmToSi = 2 * Math.PI / 60;

        private static (Registry R, SketchDocument Doc) One(string item, Func<SketchObject, SketchObject>? f = null)
        {
            var r = Fx.Reg();
            var o = r.Create(r.Item(item)!, new Vec2(100, 80)) with { Id = "m" };
            if (f is not null) o = f(o);
            var doc = SketchDocument.Create(PageSizes.Slide16x9, "Values");
            return (r, Edits.Add(doc, doc.Pages[0].Id, o));
        }

        private static ValueTable Solve(Registry r, SketchDocument doc) => ValueSolver.Solve(doc, r, null, new ValueSolveOptions { BudgetMs = 10000 });

        private static double Val(ValueTable t, string key) => t.Get("m", key)!.Value!.Value;

        [Fact]
        public void GenevaAndSixBarDeclareBilingualValuesWithSources()
        {
            var r = Fx.Reg();
            foreach (var id in new[] { "kin.mech.geneva", "kin.mech.sixbar" })
            {
                var t = r.Type(id)!;
                Assert.True(ValueModel.HasValues(t), id);
                var vars = ValueModel.Variables(t);
                Assert.True(vars.Count(v => v.Relevance == VarRelevance.Card) <= 4, id);
                Assert.All(vars, v => Assert.False(string.IsNullOrEmpty(v.Name.De), v.Key));
                Assert.All(ValueModel.Relations(t), rel => { Assert.StartsWith("kin.", rel.Id); Assert.NotNull(ValueSources.Get(rel.Source)); });
                Assert.Equal(t.Parameters.Count, t.Parameters.Select(p => p.Key).Distinct().Count());
            }
            var o = r.Create(r.Item("kin.sixbar.watt")!, new Vec2(0, 0));
            var links = ((IValueLinks)r.Type("kin.mech.sixbar")!).ValueLinks(o, Fx.Ctx()).ToList();
            Assert.Equal(new[] { "kin.sixbar.output", "kin.sixbar.mu2", "kin.sixbar.ratio" }, links.Select(l => l.Relation.Id).ToArray());
            Assert.All(links, l => { Assert.Equal("S24", l.Relation.Source); Assert.False(string.IsNullOrEmpty(l.Relation.Validity?.De)); });
        }

        // ------------------------------------------------------------------ Geneva drive

        /// <summary>
        /// z = 4: β = 360°/4 = 90°, driver angle in motion 180° − 90° = 90°, at rest 270°, ratio 90/270 = 1/3;
        /// z = 6: β = 60°, 120°, 240°, ratio 1/2. Crank radius a = C·sin(180°/z) = 30·sin 45° = 21.21 mm.
        /// </summary>
        [Theory]
        [InlineData("kin.geneva", 4, 90, 90, 270, 1.0 / 3)]
        [InlineData("kin.geneva.6", 6, 60, 120, 240, 0.5)]
        public void GenevaIndexingMotionAndDwellAngles(string item, int z, double beta, double am, double ad, double tau)
        {
            var (r, doc) = One(item);
            var t = Solve(r, doc);
            Assert.Empty(t.Conflicts);
            Assert.Equal(z, Val(t, "z"), 9);
            Assert.Equal(beta * Deg, Val(t, "beta"), 9);
            Assert.Equal(am * Deg, Val(t, "am"), 9);
            Assert.Equal(ad * Deg, Val(t, "ad"), 9);
            Assert.Equal(tau, Val(t, "tau"), 9);
            Assert.Equal(0.030 * Math.Sin(Math.PI / z), Val(t, "a"), 12);
            Assert.Contains(t.Explain("m", "beta", Lang.En), s => s.RelationId == "kin.geneva.index" && s.Source == "S24");
        }

        /// <summary>
        /// z = 4, pin on the line of centres (φ = 0): λ = sin 45° = 0.7071, ω₂/ω₁ = −λ/(1 − λ) = −2.414 (the known peak ratio
        /// of the four-slot drive); n = 60 min⁻¹ (2π s⁻¹) → ω₂ = −15.17 s⁻¹. Outside the slots (φ = 90°) the wheel is locked.
        /// </summary>
        [Fact]
        public void GenevaWheelSpeedAtThePosition()
        {
            var (r, doc) = One("kin.geneva", o => o.WithParam("angle", 0.0).WithParam("inputSpeed", "60 rpm"));
            var t = Solve(r, doc);
            var lambda = Math.Sqrt(0.5);
            Assert.Equal(-lambda / (1 - lambda), Val(t, "i"), 9);
            Assert.Equal(-2.4142, Val(t, "i"), 4);
            Assert.Equal(-lambda / (1 - lambda) * 2 * Math.PI, Val(t, "w"), 9);

            var (r2, doc2) = One("kin.geneva", o => o.WithParam("angle", 90.0).WithParam("inputSpeed", "60 rpm"));
            var t2 = Solve(r2, doc2);
            Assert.Equal(0, Val(t2, "i"), 12);
            Assert.Equal(0, Val(t2, "w"), 12);
        }

        /// <summary>The velocity ratio agrees with the slope of the drawn wheel angle (central difference) inside the motion.</summary>
        [Theory]
        [InlineData(4, 20)]
        [InlineData(4, -40)]
        [InlineData(6, 25)]
        [InlineData(6, -10)]
        public void GenevaVelocityRatioIsTheSlopeOfTheWheelAngle(int z, double phi)
        {
            const double h = 1e-4;
            var slope = (Linkages.GenevaWheelAngle(z, 30, phi + h) - Linkages.GenevaWheelAngle(z, 30, phi - h)) / (2 * h);
            Assert.Equal(slope, MoreKinValues.GenevaVelocityRatio(z, phi * Deg), 6);
        }

        [Fact]
        public void GenevaSlotsEnteredAsAValueAreDrawn()
        {
            var (r, doc) = One("kin.geneva", o => o.WithParam("slots", "5"));
            var t = Solve(r, doc);
            Assert.Equal(72 * Deg, Val(t, "beta"), 9);
            Assert.Equal(5, GenevaType.ShapeOf(doc.Get("m")!).Z);
            Assert.Equal(4, GenevaType.ShapeOf(doc.Get("m")!.WithParam("slots", "4")).Z);
            Assert.Equal(6, GenevaType.ShapeOf(doc.Get("m")!.WithParam("slots", "6")).Z);
        }

        // ------------------------------------------------------------------ six-bar

        /// <summary>
        /// First loop d = 40, a = 12, b = 36, c = 30: at φ = 0 |A O4| = 28 and cos μ₁ = (36² + 30² − 28²)/(2·36·30) → 49.18°;
        /// at φ = 180° |A O4| = 52 → 103.60° (the four-bar's extremes).
        /// </summary>
        [Theory]
        [InlineData(0.0, 1412.0 / 2160)]
        [InlineData(180.0, -508.0 / 2160)]
        public void SixBarFirstTransmissionAngleFromTheCosineRule(double phi, double cos)
        {
            var (r, doc) = One("kin.sixbar.watt", o => o.WithParam("angle", phi));
            var t = Solve(r, doc);
            Assert.Empty(t.Conflicts);
            Assert.Equal(Math.Acos(cos), Val(t, "mu1"), 9);
        }

        /// <summary>
        /// Watt chain at φ = 0 (defaults): the output angle closes the second loop, |C D| = 28 with D = O6 + 22·(cos θ₆, sin θ₆);
        /// μ₂ follows from the cosine rule in the triangle C, D, O6: cos μ₂ = (28² + 22² − |C O6|²)/(2·28·22).
        /// </summary>
        [Fact]
        public void SixBarWattOutputAngleClosesTheSecondLoop()
        {
            var (r, doc) = One("kin.sixbar.watt", o => o.WithParam("angle", 0.0));
            var t = Solve(r, doc);
            var fb = Linkages.FourBar(40, 12, 36, 30, 0, false)!.Value;
            var o4 = new Vec2(40, 0);
            var c = o4 + Vec2.Polar(24, Math.Atan2(fb.B.Y, fb.B.X - 40) + 60 * Deg);
            var o6 = new Vec2(76, 0);
            var th = Val(t, "th6");
            var d = o6 + Vec2.Polar(22, th);
            Assert.Equal(28, d.DistanceTo(c), 6);
            var co6 = c.DistanceTo(o6);
            Assert.Equal(Math.Acos((28 * 28 + 22 * 22 - co6 * co6) / (2 * 28 * 22)), Val(t, "mu2"), 6);
        }

        /// <summary>Stephenson chain at φ = −60°: |P C| = 40 with C = O6 + 22·(cos θ₆, sin θ₆); μ₂ from the triangle P, C, O6.</summary>
        [Fact]
        public void SixBarStephensonOutputAngleClosesTheDyad()
        {
            var (r, doc) = One("kin.sixbar.stephenson");
            var t = Solve(r, doc);
            var fb = Linkages.FourBar(40, 12, 36, 30, -60, false)!.Value;
            var ab = fb.B - fb.A;
            var p = fb.A + ab * 0.5 + ab.Perp * 0.3;
            var o6 = new Vec2(62, -6);
            var c = o6 + Vec2.Polar(22, Val(t, "th6"));
            Assert.Equal(40, c.DistanceTo(p), 6);
            var po6 = p.DistanceTo(o6);
            Assert.Equal(Math.Acos((40 * 40 + 22 * 22 - po6 * po6) / (2 * 40 * 22)), Val(t, "mu2"), 6);
        }

        /// <summary>The velocity ratio is the slope of the output angle; with n = 30 min⁻¹ the output speed is ω₆ = i·π s⁻¹.</summary>
        [Theory]
        [InlineData("kin.sixbar.watt")]
        [InlineData("kin.sixbar.stephenson")]
        public void SixBarOutputSpeedFollowsTheSlopeOfTheOutputAngle(string item)
        {
            var (r, doc) = One(item, o => o.WithParam("inputSpeed", "30 rpm"));
            var t = Solve(r, doc);
            var o = doc.Get("m")!;
            double Theta(double phi) => MoreKinValues.SixBarOutput(o, 40, 12, 36, 30, phi)!.Value.Theta;
            var slope = (Theta(-59.5) - Theta(-60.5)) / (1.0 * Deg);
            Assert.Equal(slope, Val(t, "i"), 3);
            Assert.Equal(Val(t, "i") * 30 * RpmToSi, Val(t, "w"), 9);
            Assert.Contains(t.Explain("m", "w", Lang.En), s => s.RelationId == "kin.sixbar.speed");
        }

        [Fact]
        public void SixBarValuesFollowEnteredLinkLengths()
        {
            // A longer coupler changes the first transmission angle: at φ = 0 with b = 40: cos μ₁ = (40² + 30² − 28²)/(2·40·30).
            var (r, doc) = One("kin.sixbar.watt", o => o.WithParam("angle", 0.0).WithParam("coupler", 40.0));
            var t = Solve(r, doc);
            Assert.Equal(Math.Acos((1600.0 + 900 - 784) / 2400), Val(t, "mu1"), 9);
            Assert.True(t.Get("m", "th6")!.Value is double);
        }
    }
}
