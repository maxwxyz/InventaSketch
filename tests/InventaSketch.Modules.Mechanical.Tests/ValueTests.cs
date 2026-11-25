using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Mechanical.Tests
{
    /// <summary>
    /// Calc 3 machine elements: gear trains with absolute n, T, P along shafts and meshes, belt, chain and screw drives,
    /// springs, bearing life, the planetary set (Willis) and the checks. Every expected value is a hand calculation.
    /// </summary>
    public class ValueTests
    {
        private const double RpmToSi = 2 * Math.PI / 60;

        private static double Rpm(ValueTable t, string id, string key = "n") => t.Get(id, key)!.Value!.Value / RpmToSi;
        private static double Val(ValueTable t, string id, string key) => t.Get(id, key)!.Value!.Value;

        /// <summary>Solves the first page with a generous time budget (the first solve also builds the topology and JITs).</summary>
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

        /// <summary>Spur gear in the schematic view with the pitch diameter z (mm) so neighbours mesh; z entered.</summary>
        private static SketchObject Gear(Registry r, string id, int z, double x, double y)
            => Make(r, "mech.spur.stick", id, x, y, 8, z).WithParam("teeth", z);

        private static SketchObject Shaft(Registry r, string id, double y) => Make(r, "mech.shaft.line", id, 20, y, 80, 8);

        /// <summary>
        /// Three-stage spur train: shaft s1 carries g1 (z 20), s2 carries g2 (40) and g3 (15), s3 carries g4 (45) and g5 (18),
        /// s4 carries g6 (54). g1–g2, g3–g4 and g5–g6 mesh (centre distance (z₁ + z₂)/2 at module 1 in the drawing).
        /// </summary>
        private static SketchDocument ThreeStage(Registry r, params (string Id, string Key, string Text)[] values)
        {
            var objs = new List<SketchObject>
            {
                Shaft(r, "s1", 0), Shaft(r, "s2", 30), Shaft(r, "s3", 60), Shaft(r, "s4", 96),
                Gear(r, "g1", 20, 0, 0), Gear(r, "g2", 40, 0, 30), Gear(r, "g3", 15, 20, 30),
                Gear(r, "g4", 45, 20, 60), Gear(r, "g5", 18, 40, 60), Gear(r, "g6", 54, 40, 96),
            };
            foreach (var (id, key, text) in values)
            {
                var i = objs.FindIndex(o => o.Id == id);
                objs[i] = objs[i].WithParam(key, text);
            }
            return Doc(objs.ToArray());
        }

        // ------------------------------------------------------------------ declarations

        [Fact]
        public void GearsShaftsAndDrivesDeclareBilingualVariablesBoundToOptionalParameters()
        {
            var r = Fx.Reg();
            var spur = r.Type("mech.gear.spur")!;
            var vars = ValueModel.Variables(spur);
            Assert.Equal(new[] { "z", "m", "n", "T", "P", "d", "eta" }, vars.Select(v => v.Key).ToArray());
            Assert.Equal("teeth", vars[0].ParamKey);
            Assert.Equal("module", vars[1].ParamKey);
            Assert.True(vars.Count(v => v.Relevance == VarRelevance.Card) <= 4);
            Assert.Equal("M", vars.Single(v => v.Key == "T").Symbol.De);
            Assert.Equal("T", vars.Single(v => v.Key == "T").Symbol.En);
            foreach (var t in r.Types.Where(ValueModel.HasValues))
            {
                Assert.True(ValueModel.Variables(t).Count(v => v.Relevance == VarRelevance.Card) <= 4, t.Id);
                foreach (var v in ValueModel.Variables(t))
                {
                    Assert.False(string.IsNullOrEmpty(v.Name.De), t.Id + "." + v.Key);
                    Assert.Contains(t.Parameters, p => p.Key == v.ParamKey);
                }
                Assert.All(ValueModel.Relations(t), rel => Assert.NotNull(rel.Source));
                Assert.All(((IValueChecks)t).ValueChecks, c => { Assert.NotNull(c.Source); Assert.False(string.IsNullOrEmpty(c.Message.De)); });
            }
            foreach (var id in new[] { "mech.shaft", "mech.pulley", "mech.sprocket", "mech.belt", "mech.chain", "mech.screwdrive.screw", "mech.spring.compression", "mech.bearing.ball", "mech.gearset.planetary" })
                Assert.True(ValueModel.HasValues(r.Type(id)), id);
            // Elements without relations yet stay value-free.
            Assert.False(ValueModel.HasValues(r.Type("mech.screw")));
        }

        [Fact]
        public void EveryItemStillInsertsWithoutValuesAndShowsNoCheck()
        {
            var r = Fx.Reg();
            var objs = r.Library.Select((item, i) => r.Create(item, new Vec2(i % 12 * 200, i / 12 * 200)) with { Id = "o" + i }).ToArray();
            var doc = Doc(objs);
            var table = ValueSolver.Solve(doc, r, null, new ValueSolveOptions { BudgetMs = 5000 });
            Assert.Empty(table.Conflicts);
            Assert.Empty(ValueSolver.Checks(table, r, new RenderContext(doc)));
            // Free scaling changes the size only: values do not depend on it.
            var g = r.Create(r.Item("mech.spur.teeth")!, new Vec2(0, 0));
            Assert.Equal(18, g.Num("teeth", 0));
        }

        // ------------------------------------------------------------------ Calc 3 accept: three-stage spur train

        [Fact]
        public void ThreeStageSpurTrainShowsSpeedTorqueAndPowerOnEveryShaftAsCalculatedByHand()
        {
            var r = Fx.Reg();
            var doc = ThreeStage(r, ("s1", "speed", "1500 rpm"), ("s1", "torque", "10 N·m"));
            var t = Solve(doc, r);
            Assert.Empty(t.Conflicts);

            // Hand calculation: i₁ = 40/20 = 2, i₂ = 45/15 = 3, i₃ = 54/18 = 3; η = 1 (default).
            // n: 1500 → 750 → 250 → 83.33 min⁻¹; T: 10 → 20 → 60 → 180 N·m; P = 2π·n·T = 2π·25 s⁻¹·10 N·m = 1570.8 W.
            var expected = new (string Shaft, double N, double T)[] { ("s1", 1500, 10), ("s2", 750, 20), ("s3", 250, 60), ("s4", 1500.0 / 18, 180) };
            var p = 2 * Math.PI * 1500 / 60 * 10;
            foreach (var (s, n, tq) in expected)
            {
                Assert.Equal(n, Rpm(t, s), 9);
                Assert.Equal(tq, Val(t, s, "T"), 9);
                Assert.Equal(p, Val(t, s, "P"), 6);
            }
            Assert.Equal(1570.796, p, 3);
            // The gears carry the speed and torque of their shafts.
            Assert.Equal(750, Rpm(t, "g2"), 9);
            Assert.Equal(750, Rpm(t, "g3"), 9);
            Assert.Equal(20, Val(t, "g3", "T"), 9);
            Assert.Equal(1500.0 / 18, Rpm(t, "g6"), 9);
            Assert.Equal(180, Val(t, "g6", "T"), 9);
            Assert.Equal(Provenance.Linked, t.Get("g1", "n")!.Provenance);
            Assert.Equal(Provenance.Derived, t.Get("g2", "n")!.Provenance);
            Assert.Empty(ValueSolver.Checks(t, r, new RenderContext(doc)));

            // The explanation names the mesh: n = n·z/z.
            var steps = t.Explain("g2", "n", Lang.En);
            Assert.Contains(steps, s => s.RelationId == "mech.mesh.speed");
        }

        [Fact]
        public void TheCalculationSheetOfTheGearExampleListsQuantitySymbolValueUnitFormulaAndSource()
        {
            // Accept (Calc 6): the calculation sheet of the three-stage train.
            var r = Fx.Reg();
            var doc = ThreeStage(r, ("s1", "speed", "1500 rpm"), ("s1", "torque", "10 N·m"));
            var t = Solve(doc, r);
            var ids = doc.Pages[0].Order;
            var rows = CalcSheet.Rows(t, r, ids, Lang.En);
            Assert.NotEmpty(rows);
            var g2n = rows.First(x => x.Symbol == "n" && x.Formula.Contains("z") && x.Origin == "derived");
            Assert.Equal("min⁻¹", g2n.Unit);
            Assert.Equal("750", g2n.Value);
            var cited = rows.First(x => x.Formula.Contains("z") && x.Source.Length > 0).Source;
            Assert.Matches(@"^\[S\d+\]$", cited);
            Assert.Contains(rows, x => x.Origin == "entered" && x.Symbol == "T" && x.Value == "10" && x.Unit == "N·m");
            var csv = CalcSheet.Csv(rows, Lang.En);
            Assert.StartsWith("Element,Quantity,Symbol,Value,Unit,Range,Formula,Origin,Source", csv);
            var md = CalcSheet.Markdown(rows, Lang.De);
            Assert.Contains("| Element | Größe | Zeichen | Wert | Einheit |", md);
            Assert.Contains("Quellen:", md);
            Assert.Contains(ValueSources.Get(cited)!.De, md);
            var tex = CalcSheet.Latex(rows, Lang.En);
            Assert.Contains("\\begin{tabular}", tex);
            var cells = CalcSheet.TableCells(rows, Lang.En);
            Assert.StartsWith("Element|Symbol|Value|Formula|Source\n", cells);
        }

        [Fact]
        public void StageEfficienciesOnTheDrivenGearsReduceTorqueAndPower()
        {
            var r = Fx.Reg();
            var doc = ThreeStage(r, ("s1", "speed", "1500 rpm"), ("s1", "torque", "10 N·m"),
                ("g2", "efficiency", "98 %"), ("g4", "efficiency", "98 %"), ("g6", "efficiency", "98 %"));
            var t = Solve(doc, r);
            Assert.Empty(t.Conflicts);
            // T₂ = 10·2·0.98 = 19.6; T₃ = 19.6·3·0.98 = 57.624; T₄ = 57.624·3·0.98 = 169.41456 N·m.
            Assert.Equal(19.6, Val(t, "s2", "T"), 9);
            Assert.Equal(57.624, Val(t, "s3", "T"), 9);
            Assert.Equal(169.41456, Val(t, "s4", "T"), 9);
            // P_out = P_in·Πη = 1570.8 W · 0.98³; speeds do not change.
            Assert.Equal(2 * Math.PI * 25 * 10 * Math.Pow(0.98, 3), Val(t, "s4", "P"), 6);
            Assert.Equal(1500.0 / 18, Rpm(t, "s4"), 9);
        }

        [Fact]
        public void OutputTorqueEnteredWithTheInputGivesTheEfficiency()
        {
            var r = Fx.Reg();
            // Only g1–g2 in mesh: T₂ = 18 N·m entered at 10 N·m in, i = 2 → η = 18/20 = 0.9 on the driven gear.
            var doc = Doc(Shaft(r, "s1", 0), Shaft(r, "s2", 30), Gear(r, "g1", 20, 0, 0), Gear(r, "g2", 40, 0, 30));
            doc = Edits.Update(doc, new[] { "s1" }, o => o.WithParam("torque", "10 N·m"));
            doc = Edits.Update(doc, new[] { "s2" }, o => o.WithParam("torque", "18 N·m"));
            var t = Solve(doc, r);
            Assert.Empty(t.Conflicts);
            Assert.Equal(0.9, Val(t, "g2", "eta"), 9);
        }

        // ------------------------------------------------------------------ Calc 3 accept: module warning

        [Fact]
        public void ModulesTwoAndTwoPointFiveInOneMeshGiveTheModuleWarning()
        {
            var r = Fx.Reg();
            SketchDocument Pair(double m1, double m2) => Doc(
                Gear(r, "a", 20, 0, 0).WithParam("module", m1),
                Gear(r, "b", 40, 0, 30).WithParam("module", m2));
            var bad = Pair(2, 2.5);
            var t = Solve(bad, r);
            var found = ValueSolver.Checks(t, r, new RenderContext(bad));
            Assert.Contains(found, f => f.CheckId == "mech.mesh.module" && f.ObjectId == "a" && f.Severity == CheckSeverity.Warning && f.Source == "S16");
            Assert.Contains(found, f => f.CheckId == "mech.mesh.module" && f.ObjectId == "b");
            Assert.Contains("Modul", found.First(f => f.CheckId == "mech.mesh.module").Message.De);
            // d = m·z: 2 mm · 20 = 40 mm.
            Assert.Equal(0.040, Val(t, "a", "d"), 12);

            var good = Pair(2, 2);
            Assert.DoesNotContain(ValueSolver.Checks(Solve(good, r), r, new RenderContext(good)), f => f.CheckId == "mech.mesh.module");
        }

        [Fact]
        public void EfficiencyAboveOneGivesThePowerBalanceWarning()
        {
            var r = Fx.Reg();
            var doc = Doc(Gear(r, "a", 20, 0, 0), Gear(r, "b", 40, 0, 30).WithParam("efficiency", "110 %"));
            var t = Solve(doc, r);
            var found = ValueSolver.Checks(t, r, new RenderContext(doc));
            Assert.Contains(found, f => f.CheckId == "mech.power.balance" && f.ObjectId == "b" && f.Source == "S1");
        }

        [Fact]
        public void LooseGearDoesNotTakeTheShaftSpeed()
        {
            var r = Fx.Reg();
            var doc = Doc(Shaft(r, "s1", 0).WithParam("speed", "1000 rpm"), Gear(r, "g1", 20, 0, 0).WithParam("hub", "loose"));
            var t = Solve(doc, r);
            Assert.Null(t.Get("g1", "n")!.Value);
        }

        // ------------------------------------------------------------------ belt, chain, screw

        [Fact]
        public void BeltDrivePassesSpeedAndPullBetweenPulleys()
        {
            var r = Fx.Reg();
            // Pulleys (axial view) at x = 0 and 100, the open belt spans their hubs.
            var doc = Doc(
                Make(r, "mech.pulley.axial", "p1", 0, 0).WithParam("diameter", "100 mm").WithParam("speed", "1500 rpm").WithParam("torque", "10 N·m"),
                Make(r, "mech.pulley.axial", "p2", 100, 0).WithParam("diameter", "250 mm"),
                Make(r, "mech.belt.open", "belt", 50, 0, 100, 24));
            var t = Solve(doc, r);
            Assert.Empty(t.Conflicts);
            // v = π·d₁·n₁ = π·0.1 m·25 s⁻¹ = 7.854 m/s; n₂ = v/(π·d₂) = 600 min⁻¹; F = 2T/d = 200 N; T₂ = F·d₂/2 = 25 N·m.
            Assert.Equal(Math.PI * 0.1 * 25, Val(t, "belt", "v"), 9);
            Assert.Equal(600, Rpm(t, "p2"), 9);
            Assert.Equal(200, Val(t, "belt", "F"), 9);
            Assert.Equal(25, Val(t, "p2", "T"), 9);
            Assert.Equal(2 * Math.PI * 25 * 10, Val(t, "belt", "P"), 6);
            Assert.Equal(Val(t, "p1", "P"), Val(t, "p2", "P"), 6);
        }

        [Fact]
        public void ChainDriveGivesSpeedTorqueAndChainSpeed()
        {
            var r = Fx.Reg();
            var doc = Doc(
                Make(r, "mech.sprocket.teeth", "k1", 0, 0).WithParam("teeth", 17).WithParam("speed", "900 rpm").WithParam("torque", "50 N·m"),
                Make(r, "mech.sprocket.teeth", "k2", 100, 0).WithParam("teeth", 51),
                Make(r, "mech.chain", "chain", 50, 0, 100, 24));
            var t = Solve(doc, r);
            // Without a pitch: n₂ = 900·17/51 = 300 min⁻¹, T₂ = 50·51/17 = 150 N·m.
            Assert.Equal(300, Rpm(t, "k2"), 9);
            Assert.Equal(150, Val(t, "k2", "T"), 9);
            Assert.Null(t.Get("chain", "v")!.Value);

            doc = Edits.Update(doc, new[] { "chain" }, o => o.WithParam("pitch", "12.7 mm"));
            t = Solve(doc, r);
            Assert.Empty(t.Conflicts);
            // v = z·p·n = 17·0.0127 m·15 s⁻¹ = 3.2385 m/s; F = 2π·T/(z·p) = 2π·50/(17·0.0127) = 1455.1 N.
            Assert.Equal(17 * 0.0127 * 15, Val(t, "chain", "v"), 9);
            Assert.Equal(2 * Math.PI * 50 / (17 * 0.0127), Val(t, "chain", "F"), 6);
            Assert.Equal(150, Val(t, "k2", "T"), 9);
        }

        [Fact]
        public void ScrewDriveTurnsSpeedAndTorqueIntoFeedAndForceAndTheNutFollows()
        {
            var r = Fx.Reg();
            var doc = Doc(
                Make(r, "mech.ballscrew.stick", "sc", 0, 0).WithParam("pitch", 5.0).WithParam("speed", "600 rpm").WithParam("torque", "2 N·m").WithParam("efficiency", "90 %"),
                Make(r, "mech.ballnut.stick", "nut", 10, 0));
            var t = Solve(doc, r);
            Assert.Empty(t.Conflicts);
            // v = P_h·n = 5 mm · 10 s⁻¹ = 0.05 m/s; F = 2π·T·η/P_h = 2π·2·0.9/0.005 = 2261.9 N.
            Assert.Equal(0.05, Val(t, "sc", "v"), 12);
            Assert.Equal(2 * Math.PI * 2 * 0.9 / 0.005, Val(t, "sc", "F"), 6);
            Assert.Equal(0.05, Val(t, "nut", "v"), 12);
            Assert.Equal(Provenance.Linked, t.Get("nut", "F")!.Provenance);
            Assert.Empty(ValueSolver.Checks(t, r, new RenderContext(doc)));

            // A lead screw above 50 % efficiency is not self-locking (info).
            var lead = Edits.Update(doc, new[] { "sc" }, o => o.WithParam("kind", "lead").WithParam("efficiency", "60 %"));
            var found = ValueSolver.Checks(Solve(lead, r), r, new RenderContext(lead));
            Assert.Contains(found, f => f.CheckId == "mech.screw.selflock" && f.Severity == CheckSeverity.Info);
        }

        // ------------------------------------------------------------------ springs, bearings

        [Fact]
        public void SpringLawAndRateFromGeometry()
        {
            var r = Fx.Reg();
            var a = Doc(Make(r, "mech.spring.compression", "f", 0, 0).WithParam("rate", "10 N/mm").WithParam("travel", "5 mm"));
            var t = Solve(a, r);
            Assert.Equal(50, Val(t, "f", "F"), 9); // F = c·s = 10 N/mm · 5 mm

            // c = G·d⁴/(8·D³·n_f) = 81 000 N/mm² · 16 mm⁴ / (8 · 4096 mm³ · 8) = 4.944 N/mm (G default: steel).
            var b = Doc(Make(r, "mech.spring.compression", "f", 0, 0).WithParam("wire", "2 mm").WithParam("meanDiameter", "16 mm").WithParam("activeCoils", "8"));
            t = Solve(b, r);
            Assert.Equal(81000.0 * 16 / (8 * 4096 * 8), Val(t, "f", "c") / 1000, 9);
            Assert.Equal(Provenance.Default, t.Get("f", "G")!.Provenance);
        }

        [Fact]
        public void BearingLifeFromLoadRatingLoadAndShaftSpeed()
        {
            var r = Fx.Reg();
            var doc = Doc(
                Shaft(r, "s", 0).WithParam("speed", "1500 rpm"),
                Make(r, "mech.bearing.ball.stick", "b", -10, 0).WithParam("loadRating", "30 kN").WithParam("load", "3 kN").WithParam("requiredLife", "20000 h"),
                Make(r, "mech.bearing.cylroller", "c", 50, 0).WithParam("loadRating", "30 kN").WithParam("load", "3 kN"));
            var t = Solve(doc, r);
            Assert.Empty(t.Conflicts);
            // Ball: L₁₀ = (30/3)³ = 1000 (10⁶ rev); L₁₀h = 10⁹/(60·1500) = 11 111 h. Roller: L₁₀ = 10^(10/3) = 2154.4.
            Assert.Equal(1500, Rpm(t, "b"), 9);
            Assert.Equal(1000, Val(t, "b", "L10"), 9);
            Assert.Equal(1e9 / (60 * 1500), Val(t, "b", "L10h") / 3600, 6);
            Assert.Equal(Math.Pow(10, 10.0 / 3), Val(t, "c", "L10"), 9);
            var found = ValueSolver.Checks(t, r, new RenderContext(doc));
            Assert.Contains(found, f => f.CheckId == "mech.bearing.life" && f.ObjectId == "b" && f.Severity == CheckSeverity.Hint && f.Source == "S19");
            Assert.DoesNotContain(found, f => f.ObjectId == "c");
        }

        // ------------------------------------------------------------------ planetary set (Willis)

        [Fact]
        public void PlanetarySetWithFixedRingFollowsWillis()
        {
            var r = Fx.Reg();
            var doc = Doc(Make(r, "mech.planetary.axial", "ps", 0, 0).WithParam("speeds", "1500 rpm").WithParam("speedr", "0").WithParam("torques", "10 N·m"));
            var t = Solve(doc, r);
            Assert.Empty(t.Conflicts);
            // z_s = 20, z_r = 52 (as drawn): n_c = z_s·n_s/(z_s + z_r) = 20·1500/72 = 416.67 min⁻¹.
            Assert.Equal(20.0 * 1500 / 72, Rpm(t, "ps", "nc"), 9);
            // T_r = T_s·z_r/z_s = 26 N·m; T_c = −(T_s + T_r) = −36 N·m (power balance: T_s·n_s + T_c·n_c = 0).
            Assert.Equal(26, Val(t, "ps", "Tr"), 9);
            Assert.Equal(-36, Val(t, "ps", "Tc"), 9);
            Assert.Equal(0, Val(t, "ps", "Ts") * Val(t, "ps", "ns") + Val(t, "ps", "Tc") * Val(t, "ps", "nc"), 6);
            Assert.Empty(ValueSolver.Checks(t, r, new RenderContext(doc)));
        }

        [Theory]
        [InlineData(20, 51, 3)]
        [InlineData(20, 52, 5)]
        public void PlanetaryAssemblyConditionIsChecked(int zs, int zr, int planets)
        {
            var r = Fx.Reg();
            var doc = Doc(Make(r, "mech.planetary.axial", "ps", 0, 0).WithParam("zs", zs.ToString()).WithParam("zr", zr.ToString()).WithParam("planets", planets));
            var found = ValueSolver.Checks(Solve(doc, r), r, new RenderContext(doc));
            Assert.Contains(found, f => f.CheckId == "mech.planetary.assembly" && f.Severity == CheckSeverity.Warning);
        }

        [Fact]
        public void PersistenceRoundTripKeepsEnteredValues()
        {
            var r = Fx.Reg();
            var doc = ThreeStage(r, ("s1", "speed", "1500 rpm"), ("s1", "torque", "10 N·m"));
            var back = DocumentJson.Deserialize(DocumentJson.Serialize(doc));
            Assert.Equal("1500 rpm", back.Get("s1")!.Str("speed", ""));
            Assert.Equal(180, Val(Solve(back, r), "s4", "T"), 9);
        }
    }
}
