using System;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Mechanical.Tests
{
    /// <summary>
    /// Value relations of the flywheel, strain wave and cycloidal drives, differential, Ravigneaux set, CVT, rack and pinion
    /// and the slip clutch (Calc 3). Every expected value is a hand calculation with textbook formulas.
    /// </summary>
    public class TransmissionValueTests
    {
        private const double RpmToSi = 2 * Math.PI / 60;

        private static double Rpm(ValueTable t, string id, string key) => t.Get(id, key)!.Value!.Value / RpmToSi;
        private static double Val(ValueTable t, string id, string key) => t.Get(id, key)!.Value!.Value;

        private static ValueTable Solve(SketchDocument doc, Registry r) => ValueSolver.Solve(doc, r, null, new ValueSolveOptions { BudgetMs = 10000 });

        private static SketchObject Make(Registry r, string item, string id, double x, double y, double? w = null, double? h = null)
        {
            var o = r.Create(r.Item(item)!, new Vec2(x, y)) with { Id = id };
            return w is null ? o : o with { Size = new Size2(w.Value, h!.Value) };
        }

        private static SketchDocument Doc(params SketchObject[] objs)
        {
            var doc = SketchDocument.Create(PageSizes.Slide16x9, "Values");
            return Edits.Add(doc, doc.Pages[0].Id, objs);
        }

        private static string[] Checks(ValueTable t, Registry r, SketchDocument doc, string id)
            => ValueSolver.Checks(t, r, new RenderContext(doc)).Where(f => f.ObjectId == id).Select(f => f.CheckId).ToArray();

        // ------------------------------------------------------------------ declarations

        [Fact]
        public void NewValueTypesDeclareBilingualVariablesWithSourcesAndAtMostFourCardValues()
        {
            var r = Fx.Reg();
            foreach (var id in new[] { "mech.flywheel", "mech.gearset.harmonic", "mech.gearset.cycloidal", "mech.gearset.differential", "mech.gearset.ravigneaux", "mech.cvt", "mech.clutch.slip", "mech.gear.rack" })
            {
                var t = r.Type(id)!;
                Assert.True(ValueModel.HasValues(t), id);
                var vars = ValueModel.Variables(t);
                Assert.True(vars.Count(v => v.Relevance == VarRelevance.Card) <= 4, id);
                Assert.Equal(vars.Count, vars.Select(v => v.Key).Distinct().Count());
                Assert.Equal(t.Parameters.Count, t.Parameters.Select(p => p.Key).Distinct().Count());
                foreach (var v in vars)
                {
                    Assert.False(string.IsNullOrEmpty(v.Name.De), id + "." + v.Key);
                    Assert.Contains(t.Parameters, p => p.Key == v.ParamKey);
                }
                foreach (var rel in ValueModel.Relations(t))
                {
                    Assert.NotNull(ValueSources.Get(rel.Source));
                    Assert.StartsWith("mech.", rel.Id);
                    Assert.False(string.IsNullOrEmpty(rel.Name.De), rel.Id);
                }
                foreach (var c in ((IValueChecks)t).ValueChecks)
                {
                    Assert.StartsWith("mech.", c.Id);
                    Assert.NotNull(ValueSources.Get(c.Source));
                    Assert.False(string.IsNullOrEmpty(c.Message.De), c.Id);
                }
            }
        }

        // ------------------------------------------------------------------ flywheel

        /// <summary>
        /// Solid steel disc m = 50 kg, D = 400 mm: J = ½·m·r² = ½·50·0.2² = 1 kg·m². At n = 3000 min⁻¹ (ω = 100π s⁻¹):
        /// E = ½·J·ω² = 49 348 J. An energy fluctuation ΔE = 2 kJ gives Δω = ΔE/(J·ω) = 6.366 s⁻¹ → Δn = 60.79 min⁻¹, δ = 2.026 %.
        /// </summary>
        [Fact]
        public void FlywheelEnergyAndSpeedFluctuationOfASolidDisc()
        {
            var r = Fx.Reg();
            var f = Make(r, "mech.flywheel", "f", 0, 0).WithParam("mass", "50 kg").WithParam("diameter", "400 mm")
                .WithParam("speed", "3000 rpm").WithParam("energyFluctuation", "2 kJ");
            var doc = Doc(f);
            var t = Solve(doc, r);
            Assert.Empty(t.Conflicts);
            Assert.Equal(1.0, Val(t, "f", "J"), 9);
            var w = 3000 * RpmToSi;
            Assert.Equal(0.5 * w * w, Val(t, "f", "E"), 6);
            Assert.Equal(49348.0, Val(t, "f", "E"), 0);
            Assert.Equal(2000 / w / RpmToSi, Rpm(t, "f", "dn"), 6);
            Assert.Equal(60.79, Rpm(t, "f", "dn"), 2);
            Assert.Equal(0.02026, Val(t, "f", "delta"), 5);
            Assert.Contains(t.Explain("f", "E", Lang.En), s => s.RelationId == "mech.flywheel.energy");
        }

        /// <summary>A thin rim (d_i → D) has J = m·r²; a rim 400/360 mm: J = 50·(0.4² + 0.36²)/8 = 1.81 kg·m².</summary>
        [Fact]
        public void FlywheelRimTakesTheInnerDiameter()
        {
            var r = Fx.Reg();
            var f = Make(r, "mech.flywheel", "f", 0, 0).WithParam("mass", "50 kg").WithParam("diameter", "400 mm").WithParam("innerDiameter", "360 mm");
            var t = Solve(Doc(f), r);
            Assert.Equal(50 * (0.16 + 0.1296) / 8, Val(t, "f", "J"), 9);
            Assert.Equal(1.81, Val(t, "f", "J"), 9);
            // From the energy back to the speed: E = 10 kJ with J = 1 kg·m² gives ω = √(2E/J) = 141.42 s⁻¹ = 1350.5 min⁻¹.
            var g = Make(r, "mech.flywheel", "g", 0, 0).WithParam("inertia", "1 kg·m²").WithParam("energy", "10 kJ");
            var t2 = Solve(Doc(g), r);
            Assert.Equal(Math.Sqrt(20000) / RpmToSi, Rpm(t2, "g", "n"), 6);
        }

        /// <summary>The flywheel takes the speed of its shaft (no torque: it does not split the line's torque).</summary>
        [Fact]
        public void FlywheelOnAShaftTakesItsSpeed()
        {
            var r = Fx.Reg();
            var shaft = Make(r, "mech.shaft.line", "s", 20, 0, 80, 8).WithParam("speed", "3000 rpm").WithParam("torque", "20 N·m");
            var f = Make(r, "mech.flywheel", "f", 10, 0).WithParam("inertia", "1 kg·m²");
            var doc = Doc(shaft, f);
            var t = Solve(doc, r);
            Assert.Empty(t.Conflicts);
            Assert.Equal(3000, Rpm(t, "f", "n"), 9);
            Assert.Equal(Provenance.Linked, t.Get("f", "n")!.Provenance);
            var w = 3000 * RpmToSi;
            Assert.Equal(0.5 * w * w, Val(t, "f", "E"), 6);
            Assert.DoesNotContain(ValueModel.Variables(r.Type("mech.flywheel")), v => v.Key == "T");
        }

        // ------------------------------------------------------------------ strain wave and cycloidal drives

        /// <summary>
        /// Strain wave gear z_f = 200, z_c = 202 (circular spline fixed): i = −200/(202 − 200) = −100. n₁ = 3000 min⁻¹ →
        /// n₂ = −30 min⁻¹; T₁ = 2 N·m, η = 80 % → T₂ = 100·0.8·2 = 160 N·m.
        /// </summary>
        [Fact]
        public void StrainWaveGearRatioFromTheTeethDifference()
        {
            var r = Fx.Reg();
            var h = Make(r, "mech.harmonic.stick", "h", 0, 0).WithParam("speedIn", "3000 rpm").WithParam("torqueIn", "2 N·m").WithParam("efficiency", "80 %");
            var doc = Doc(h);
            var t = Solve(doc, r);
            Assert.Empty(t.Conflicts);
            Assert.Equal(-100, Val(t, "h", "i"), 9);
            Assert.Equal(-30, Rpm(t, "h", "n2"), 9);
            Assert.Equal(160, Val(t, "h", "T2"), 9);
            Assert.Empty(Checks(t, r, doc, "h"));

            // z_f = 160, z_c = 162: i = −80; the ratio entered gives z_c back.
            var h2 = Make(r, "mech.harmonic.stick", "h", 0, 0).WithParam("flexTeeth", "160").WithParam("circularTeeth", "162");
            Assert.Equal(-80, Val(Solve(Doc(h2), r), "h", "i"), 9);
            var h3 = Make(r, "mech.harmonic.stick", "h", 0, 0).WithParam("flexTeeth", "100").WithParam("ratio", "-50");
            Assert.Equal(102, Val(Solve(Doc(h3), r), "h", "zc"), 9);
        }

        [Fact]
        public void StrainWaveGearWarnsAboutAnOddOrMissingTeethDifference()
        {
            var r = Fx.Reg();
            foreach (var (zc, warn) in new[] { ("201", true), ("200", true), ("198", true), ("202", false), ("204", false) })
            {
                var h = Make(r, "mech.harmonic.stick", "h", 0, 0).WithParam("flexTeeth", "200").WithParam("circularTeeth", zc);
                var doc = Doc(h);
                var t = Solve(doc, r);
                Assert.Equal(warn, Checks(t, r, doc, "h").Contains("mech.harmonic.teeth"));
            }
        }

        /// <summary>
        /// Cycloidal drive with 11 lobes (default) and 12 pins: i = −11/(12 − 11) = −11; n₁ = 1100 min⁻¹ → n₂ = −100 min⁻¹.
        /// With 29 lobes: i = −29.
        /// </summary>
        [Fact]
        public void CycloidalDriveRatioFromLobesAndPins()
        {
            var r = Fx.Reg();
            var c = Make(r, "mech.cycloidal.stick", "c", 0, 0).WithParam("speedIn", "1100 rpm");
            var t = Solve(Doc(c), r);
            Assert.Empty(t.Conflicts);
            Assert.Equal(12, Val(t, "c", "zp"), 9);
            Assert.Equal(-11, Val(t, "c", "i"), 9);
            Assert.Equal(-100, Rpm(t, "c", "n2"), 9);
            var c29 = Make(r, "mech.cycloidal.stick", "c", 0, 0).WithParam("lobes", 29);
            Assert.Equal(-29, Val(Solve(Doc(c29), r), "c", "i"), 9);
        }

        // ------------------------------------------------------------------ differential and Ravigneaux

        /// <summary>Open differential in a curve: n₁ = 400, n₂ = 360 min⁻¹ → n_c = 380 min⁻¹; T_c = 500 N·m → T₁ = T₂ = 250 N·m.</summary>
        [Fact]
        public void DifferentialAveragesTheSpeedsAndSplitsTheTorqueEqually()
        {
            var r = Fx.Reg();
            var d = Make(r, "mech.differential.stick", "d", 0, 0).WithParam("speed1", "400 rpm").WithParam("speed2", "360 rpm").WithParam("torquec", "500 N·m");
            var t = Solve(Doc(d), r);
            Assert.Empty(t.Conflicts);
            Assert.Equal(380, Rpm(t, "d", "nc"), 9);
            Assert.Equal(250, Val(t, "d", "T1"), 9);
            Assert.Equal(250, Val(t, "d", "T2"), 9);
            // One wheel held (n₁ = 0): the other turns at twice the case speed.
            var d2 = Make(r, "mech.differential.stick", "d", 0, 0).WithParam("speed1", "0 rpm").WithParam("speedc", "300 rpm");
            Assert.Equal(600, Rpm(Solve(Doc(d2), r), "d", "n2"), 9);
        }

        /// <summary>
        /// Ravigneaux set z_s1 = 14, z_s2 = 20, z_r = 52. Carrier held, small sun driven at 1000 min⁻¹: the planet pairs turn
        /// the ring in the same sense, n_r = 1000·14/52 = 269.23 min⁻¹, and the large sun backwards, n_s2 = −(52/20)·n_r = −700.
        /// Ring held, large sun driven at 1000: n_c = 1000·20/72 = 277.78 (i = 3.6), small sun n_s1 = n_c·(1 − 52/14) = −753.97.
        /// </summary>
        [Fact]
        public void RavigneauxSetFollowsBothWillisEquations()
        {
            var r = Fx.Reg();
            var a = Make(r, "mech.ravigneaux.stick", "rv", 0, 0).WithParam("speedc", "0 rpm").WithParam("speeds1", "1000 rpm");
            var t = Solve(Doc(a), r);
            Assert.Empty(t.Conflicts);
            Assert.Equal(1000.0 * 14 / 52, Rpm(t, "rv", "nr"), 9);
            Assert.Equal(-700, Rpm(t, "rv", "ns2"), 9);

            var b = Make(r, "mech.ravigneaux.stick", "rv", 0, 0).WithParam("speedr", "0 rpm").WithParam("speeds2", "1000 rpm");
            var t2 = Solve(Doc(b), r);
            Assert.Equal(1000.0 * 20 / 72, Rpm(t2, "rv", "nc"), 9);
            Assert.Equal(1000.0 * 20 / 72 * (1 - 52.0 / 14), Rpm(t2, "rv", "ns1"), 9);
        }

        // ------------------------------------------------------------------ CVT

        /// <summary>CVT with ratio 2: n₁ = 3000 → n₂ = 1500 min⁻¹; T₁ = 100 N·m, η = 90 % → T₂ = 2·0.9·100 = 180 N·m.</summary>
        [Fact]
        public void CvtRatioComesFromItsParameter()
        {
            var r = Fx.Reg();
            var c = Make(r, "mech.cvt.stick", "v", 0, 0).WithParam("ratio", 2.0).WithParam("speedIn", "3000 rpm").WithParam("torqueIn", "100 N·m").WithParam("efficiency", "90 %");
            var doc = Doc(c);
            var t = Solve(doc, r);
            Assert.Empty(t.Conflicts);
            Assert.Equal(1500, Rpm(t, "v", "n2"), 9);
            Assert.Equal(180, Val(t, "v", "T2"), 9);
            Assert.Empty(Checks(t, r, doc, "v"));
            // Without an entered ratio the default 1 holds.
            var plain = Make(r, "mech.cvt.stick", "v", 0, 0).WithParam("speedIn", "3000 rpm");
            Assert.Equal(3000, Rpm(Solve(Doc(plain), r), "v", "n2"), 9);
        }

        [Fact]
        public void CvtWarnsWhenTheSpeedsNeedARatioOutsideItsSpread()
        {
            var r = Fx.Reg();
            var c = Make(r, "mech.cvt.stick", "v", 0, 0).WithParam("speedIn", "3000 rpm").WithParam("speedOut", "1000 rpm");
            var doc = Doc(c);
            var t = Solve(doc, r);
            Assert.Equal(3, Val(t, "v", "i"), 9);
            Assert.Contains("mech.cvt.range", Checks(t, r, doc, "v"));
            var ok = Make(r, "mech.cvt.stick", "v", 0, 0).WithParam("speedIn", "3000 rpm").WithParam("speedOut", "2000 rpm");
            var doc2 = Doc(ok);
            Assert.Empty(Checks(Solve(doc2, r), r, doc2, "v"));
        }

        // ------------------------------------------------------------------ rack and pinion

        /// <summary>
        /// Rack m = 2 mm with a pinion z = 20: d = 40 mm. n = 60 min⁻¹ (ω = 2π s⁻¹) → v = ω·r = 2π·0.02 = 0.1257 m/s;
        /// T = 10 N·m → F = T/r = 500 N; P = F·v = 62.83 W = 2π·n·T.
        /// </summary>
        [Fact]
        public void RackSpeedAndForceFromThePinion()
        {
            var r = Fx.Reg();
            var rack = Make(r, "mech.rack.teeth", "k", 0, 0).WithParam("module", 2.0).WithParam("pinionTeeth", "20")
                .WithParam("pinionSpeed", "60 rpm").WithParam("pinionTorque", "10 N·m");
            var t = Solve(Doc(rack), r);
            Assert.Empty(t.Conflicts);
            Assert.Equal(0.040, Val(t, "k", "dp"), 12);
            Assert.Equal(2 * Math.PI * 0.02, Val(t, "k", "v"), 12);
            Assert.Equal(500, Val(t, "k", "F"), 9);
            Assert.Equal(2 * Math.PI * 10, Val(t, "k", "P"), 9);
            // Backwards: the rack speed gives the pinion speed.
            var back = Make(r, "mech.rack.teeth", "k", 0, 0).WithParam("pinionDiameter", "40 mm").WithParam("linSpeed", "0.5 m/s");
            Assert.Equal(0.5 / 0.02 / RpmToSi, Rpm(Solve(Doc(back), r), "k", "np"), 9);
        }

        // ------------------------------------------------------------------ slip clutch

        [Fact]
        public void SlipClutchIsInsertableAndDrawsInBothViews()
        {
            var r = Fx.Reg();
            var t = r.Type("mech.clutch.slip")!;
            Assert.Equal("mech.couplings", t.Category);
            Assert.Equal("Rutschkupplung", t.Name.De);
            foreach (var item in new[] { "mech.clutch.slip", "mech.clutch.slip.side" })
            {
                var o = r.Create(r.Item(item)!, new Vec2(0, 0));
                Assert.NotEmpty(t.Render(o, Fx.Ctx()));
                Assert.Contains(t.Anchors(o, Fx.Ctx()), a => a.Role == AnchorRole.Hub);
                // Scaling changes the size only.
                var big = o with { Size = new Size2(o.Size.W * 2, o.Size.H * 3) };
                Assert.NotEmpty(t.Render(big, Fx.Ctx()));
                Assert.DoesNotContain(big.Params, p => p.Key == "slipTorque");
            }
            Assert.Contains(r.Library, i => i.TypeId == "mech.clutch.slip" && i.Keywords!.Contains("torque limiter"));
        }

        /// <summary>
        /// Slip clutch on a shaft: the torque of the shaft passes through the clutch; 60 N·m against a slip torque of 40 N·m
        /// is a warning, 30 N·m is not.
        /// </summary>
        [Fact]
        public void SlipClutchWarnsWhenTheShaftTorqueExceedsTheSlipTorque()
        {
            var r = Fx.Reg();
            foreach (var (torque, warn) in new[] { ("60 N·m", true), ("30 N·m", false) })
            {
                var shaft = Make(r, "mech.shaft.line", "s", 20, 0, 80, 8).WithParam("torque", torque).WithParam("speed", "1000 rpm");
                var clutch = Make(r, "mech.clutch.slip", "c", 10, 0).WithParam("slipTorque", "40 N·m");
                var doc = Doc(shaft, clutch);
                var t = Solve(doc, r);
                Assert.Empty(t.Conflicts);
                Assert.Equal(double.Parse(torque.Split(' ')[0]), Val(t, "c", "T"), 9);
                Assert.Equal(1000, Rpm(t, "c", "n"), 9);
                Assert.Equal(warn, Checks(t, r, doc, "c").Contains("mech.clutch.slip"));
            }
        }

        [Fact]
        public void SlipClutchPassesTheTorqueBetweenTwoParts()
        {
            // Gear z 20 on the shaft with 50 N·m and a slip clutch on the same shaft: one torque member, the clutch passes it.
            var r = Fx.Reg();
            var shaft = Make(r, "mech.shaft.line", "s", 20, 0, 80, 8);
            var gear = Make(r, "mech.spur.stick", "g", 40, 0, 8, 20).WithParam("teeth", 20).WithParam("torque", "50 N·m");
            var clutch = Make(r, "mech.clutch.slip", "c", 0, 0).WithParam("slipTorque", "45 N·m");
            var doc = Doc(shaft, gear, clutch);
            var t = Solve(doc, r);
            Assert.Equal(50, Val(t, "c", "T"), 9);
            Assert.Contains("mech.clutch.slip", Checks(t, r, doc, "c"));
        }
    }
}
