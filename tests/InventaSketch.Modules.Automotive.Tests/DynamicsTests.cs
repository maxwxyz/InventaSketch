using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Automotive.Tests
{
    /// <summary>
    /// Vehicle dynamics values (round 7): driving resistance and power, turning circle and lateral acceleration, battery range,
    /// sensor range against the stopping distance and the friction limit of braking. Expected values are hand calculations.
    /// </summary>
    public class DynamicsTests
    {
        private const double G = 9.80665;

        private static ValueTable Solve(Registry r, SketchDocument doc) => ValueSolver.Solve(doc, r, null, new ValueSolveOptions { BudgetMs = 10000 });

        private static IReadOnlyList<CheckFinding> Checks(Registry r, SketchDocument doc) => ValueSolver.Checks(Solve(r, doc), r, new RenderContext(doc).For(r));

        private static double Val(ValueTable t, SketchObject o, string key) => t.Get(o.Id, key)?.Value ?? double.NaN;

        private static SketchObject Item(Registry r, string id) => r.Create(r.Item(id) ?? throw new KeyNotFoundException(id), new Vec2(100, 100));

        private static (ValueTable T, SketchDocument Doc) Solved(Registry r, SketchObject o)
        {
            var doc = Fx.Doc(o).Doc;
            return (Solve(r, doc), doc);
        }

        // ------------------------------------------------------------------ declarations

        [Fact]
        public void NewTypesDeclareBilingualValuesAndParameters()
        {
            var r = Fx.Reg();
            foreach (var id in new[] { "auto.motion.resistance", "auto.motion.turning", "auto.sub.battery", "auto.fov" })
            {
                var t = r.Type(id)!;
                Assert.True(ValueModel.HasValues(t), id);
                Assert.True(ValueModel.Variables(t).Count(v => v.Relevance == VarRelevance.Card) <= 4, id);
                Assert.Contains(t.Parameters, p => p.Key == ValueLabels.ShowParam);
                foreach (var v in ValueModel.Variables(t))
                {
                    Assert.False(string.IsNullOrEmpty(v.Name.De), id + "/" + v.Key);
                    Assert.Contains(t.Parameters, p => p.Key == v.ParamKey);
                }
                foreach (var c in ((IValueChecks)t).ValueChecks)
                {
                    Assert.StartsWith("auto.", c.Id);
                    Assert.False(string.IsNullOrEmpty(c.Message.De));
                }
            }
        }

        [Fact]
        public void NewItemsInsertWithoutParametersAndDrawInsideTheirBox()
        {
            var r = Fx.Reg();
            foreach (var id in new[] { "auto.resistance", "auto.resistance.values", "auto.resistance.uphill", "auto.turning", "auto.turning.car", "auto.battery.range" })
            {
                var o = Item(r, id);
                var prims = r.TypeOf(o).Render(o, new RenderContext(SketchDocument.Create()));
                Fx.AssertInsideBox(prims, o.Size, id);
            }
            // Without entered values nothing is computed, but the drawings are complete.
            var plain = Item(r, "auto.resistance");
            var t = Solved(r, plain).T;
            Assert.True(double.IsNaN(Val(t, plain, "F")));
        }

        // ------------------------------------------------------------------ driving resistance

        [Fact]
        public void DrivingResistanceOfACarAt100KmH()
        {
            var r = Fx.Reg();
            var o = Item(r, "auto.resistance.values"); // 1500 kg, 100 km/h, defaults c_w 0.30, A 2.2 m², f_R 0.012, q 0
            var (t, _) = Solved(r, o);
            var v = 100 / 3.6;
            var fr = 1500 * G * 0.012;
            var fl = 0.5 * 1.2 * 0.30 * 2.2 * v * v;
            Assert.Equal(fr, Val(t, o, "FR"), 6);
            Assert.Equal(fl, Val(t, o, "FL"), 6);
            Assert.Equal(0, Val(t, o, "FSt"), 9);
            Assert.Equal(fr + fl, Val(t, o, "F"), 6);
            Assert.Equal((fr + fl) * v, Val(t, o, "P"), 4);
            Assert.Equal(13.39, Val(t, o, "P") / 1000, 2);
            Assert.Equal(Provenance.Default, t.Get(o.Id, "cw")!.Provenance);
            Assert.Empty(t.Conflicts);
        }

        [Fact]
        public void GradientAddsTheGradientResistanceAndTiltsTheRoad()
        {
            var r = Fx.Reg();
            var o = Item(r, "auto.resistance.uphill"); // 1500 kg, 80 km/h, 8 %
            var (t, doc) = Solved(r, o);
            var alpha = Math.Atan(0.08);
            Assert.Equal(1500 * G * Math.Sin(alpha), Val(t, o, "FSt"), 6);
            Assert.Equal(1500 * G * 0.012 * Math.Cos(alpha), Val(t, o, "FR"), 6);
            // The drawing: the road falls to the left by arctan 8 %, the gradient force is drawn.
            var prims = r.Render(o, new RenderContext(doc)).ToList();
            var road = prims.OfType<LinePrim>().First(p => p.Tag == AutoTags.Edge);
            Assert.Equal(alpha, Math.Atan2(road.A.Y - road.B.Y, road.B.X - road.A.X), 6);
            Assert.Contains(prims, p => p.Tag == "force.grade");
            // A flat road has no gradient arrow; a downhill gradient is drawn falling to the right.
            var flat = Item(r, "auto.resistance");
            Assert.DoesNotContain(r.Render(flat, new RenderContext(Fx.Doc(flat).Doc)), p => p.Tag == "force.grade");
            var down = flat.WithParam("gradient", "-5 %");
            var downRoad = r.Render(down, new RenderContext(Fx.Doc(down).Doc)).OfType<LinePrim>().First(p => p.Tag == AutoTags.Edge);
            Assert.True(downRoad.B.Y > downRoad.A.Y);
            // A steep gradient is drawn at 12° at most and stays inside the box.
            var steep = flat.WithParam("gradient", "60 %");
            var steepPrims = r.TypeOf(steep).Render(steep, new RenderContext(SketchDocument.Create()));
            Fx.AssertInsideBox(steepPrims, steep.Size, "steep");
        }

        [Fact]
        public void GradientFollowsFromAnEnteredGradientForce()
        {
            var r = Fx.Reg();
            var o = Item(r, "auto.resistance").WithParam("mass", "1000 kg").WithParam("forceGrade", "980.665 N");
            var t = Solved(r, o).T;
            Assert.Equal(Math.Tan(Math.Asin(0.1)), Val(t, o, "q"), 6);
        }

        [Fact]
        public void PowerAboveTheAvailableDrivePowerWarns()
        {
            var r = Fx.Reg();
            var o = Item(r, "auto.resistance.values");
            Assert.DoesNotContain(Checks(r, Fx.Doc(o.WithParam("powerMax", "50 kW")).Doc), c => c.CheckId == "auto.drive.power");
            var f = Checks(r, Fx.Doc(o.WithParam("powerMax", "10 kW")).Doc).Single(c => c.CheckId == "auto.drive.power");
            Assert.Equal(CheckSeverity.Warning, f.Severity);
        }

        [Fact]
        public void ForceNamesFollowTheLanguage()
        {
            var r = Fx.Reg();
            var o = Item(r, "auto.resistance.uphill");
            var doc = Fx.Doc(o).Doc;
            string Texts(Lang lang) => string.Join(" ", r.Render(o, new RenderContext(doc, lang)).OfType<TextPrim>().Select(x => x.Text));
            Assert.Contains("F_air", Texts(Lang.En));
            Assert.Contains("F_L", Texts(Lang.De));
            Assert.Contains("F_St", Texts(Lang.De));
            Assert.Contains("8 %", Texts(Lang.En));
        }

        // ------------------------------------------------------------------ turning circle

        [Fact]
        public void TurningCircleFromWheelbaseAndSteeringAngle()
        {
            var r = Fx.Reg();
            var o = Item(r, "auto.turning.car"); // L 2.7 m, δ 30°, track 1.6 m (default)
            var t = Solved(r, o).T;
            var d = Math.PI / 6;
            Assert.Equal(2.7 / Math.Sin(d), Val(t, o, "R"), 6);
            Assert.Equal(2.7 / Math.Tan(d), Val(t, o, "Rh"), 6);
            Assert.Equal(2 * Math.Sqrt(Math.Pow(2.7 / Math.Tan(d) + 0.8, 2) + 2.7 * 2.7), Val(t, o, "D"), 6);
            Assert.Empty(t.Conflicts);
        }

        [Fact]
        public void SteeringAngleFollowsFromAnEnteredTurningCircle()
        {
            var r = Fx.Reg();
            var o = Item(r, "auto.turning").WithParam("wheelbase", "2.7 m").WithParam("circle", "11 m");
            var t = Solved(r, o).T;
            var rh = Math.Sqrt(5.5 * 5.5 - 2.7 * 2.7) - 0.8;
            Assert.Equal(rh, Val(t, o, "Rh"), 6);
            Assert.Equal(Math.Atan(2.7 / rh), Val(t, o, "delta"), 6);
        }

        [Fact]
        public void LateralAccelerationAboveTheFrictionLimitWarns()
        {
            var r = Fx.Reg();
            var o = Item(r, "auto.turning.car").WithParam("speed", "20 km/h");
            var t = Solved(r, o).T;
            var ay = Math.Pow(20 / 3.6, 2) / 5.4;
            Assert.Equal(ay, Val(t, o, "ay"), 6);
            Assert.Contains(Checks(r, Fx.Doc(o.WithParam("friction", "0.5")).Doc), c => c.CheckId == "auto.turn.friction");
            Assert.DoesNotContain(Checks(r, Fx.Doc(o.WithParam("friction", "0.8")).Doc), c => c.CheckId == "auto.turn.friction");
            // Without a friction coefficient nothing is checked.
            Assert.DoesNotContain(Checks(r, Fx.Doc(o).Doc), c => c.CheckId == "auto.turn.friction");
        }

        [Fact]
        public void TurningDrawingFollowsTheSteeringAngle()
        {
            var r = Fx.Reg();
            var a = Item(r, "auto.turning");
            var b = a.WithParam("steer", "40°");
            Point Centre(SketchObject o) => new(r.TypeOf(o).Anchors(o, new RenderContext(Fx.Doc(o).Doc)).Single(x => x.Id == "center").P);
            // A larger steering angle brings the turning centre closer to the vehicle.
            Point Vehicle(SketchObject o) => new(r.TypeOf(o).Anchors(o, new RenderContext(Fx.Doc(o).Doc)).Single(x => x.Id == "vehicle").P);
            Assert.True(Centre(b).P.DistanceTo(Vehicle(b).P) / b.Size.W < Centre(a).P.DistanceTo(Vehicle(a).P) / a.Size.W);
            Fx.AssertInsideBox(r.TypeOf(b).Render(b, new RenderContext(SketchDocument.Create())), b.Size, "steer 40°");
        }

        private readonly record struct Point(Vec2 P);

        // ------------------------------------------------------------------ battery range

        [Fact]
        public void BatteryRangeFromEnergyAndConsumption()
        {
            var r = Fx.Reg();
            var o = Item(r, "auto.battery.range"); // 60 kWh, 16 kWh per 100 km
            var t = Solved(r, o).T;
            Assert.Equal(375, Val(t, o, "R") / 1000, 6);
            Assert.DoesNotContain(Checks(r, Fx.Doc(o.WithParam("trip", "300 km")).Doc), c => c.CheckId == "auto.ev.range");
            Assert.Contains(Checks(r, Fx.Doc(o.WithParam("trip", "400 km")).Doc), c => c.CheckId == "auto.ev.range");
            // The consumption follows from an entered range.
            var back = Item(r, "auto.battery.top").WithParam("energy", "77 kWh").WithParam("range", "500 km");
            Assert.Equal(15.4, Val(Solved(r, back).T, back, "c") / 3.6e6, 6);
            // The plain battery pack has no values.
            var plain = Item(r, "auto.battery.top");
            Assert.True(double.IsNaN(Val(Solved(r, plain).T, plain, "R")));
        }

        // ------------------------------------------------------------------ sensor range and friction

        [Fact]
        public void SensorRangeAgainstTheStoppingDistance()
        {
            var r = Fx.Reg();
            var o = Item(r, "auto.fov.radar.check"); // 160 m, 130 km/h
            var t = Solved(r, o).T;
            var v = 130 / 3.6;
            var s = v * 1.0 + v * v / 16;
            Assert.Equal(s, Val(t, o, "s"), 6);
            Assert.Equal(160 / v, Val(t, o, "tP"), 6);
            Assert.DoesNotContain(Checks(r, Fx.Doc(o).Doc), c => c.CheckId == "auto.fov.range");
            var shortRange = o.WithParam("sensorRange", "100 m");
            Assert.Contains(Checks(r, Fx.Doc(shortRange).Doc), c => c.CheckId == "auto.fov.range");
        }

        [Fact]
        public void SensorRangeComesFromTheDrawingUnlessEntered()
        {
            var r = Fx.Reg();
            var o = Item(r, "auto.fov.camera"); // range 80 mm at 10 mm per m
            var t = Solved(r, o).T;
            Assert.Equal(8, Val(t, o, "rS"), 9);
            Assert.Equal(Provenance.Derived, t.Get(o.Id, "rS")!.Provenance);
            var scaled = o.WithParam("scale", 2.0);
            Assert.Equal(40, Val(Solved(r, scaled).T, scaled, "rS"), 9);
            // A field of view without a speed has no stopping distance and no finding.
            Assert.Empty(Checks(r, Fx.Doc(o).Doc));
        }

        [Fact]
        public void DecelerationAboveTheFrictionLimitWarns()
        {
            var r = Fx.Reg();
            var o = Item(r, "auto.safetydistance.braking");
            Assert.Contains(Checks(r, Fx.Doc(o.WithParam("friction", "0.5")).Doc), c => c.CheckId == "auto.braking.friction");
            Assert.DoesNotContain(Checks(r, Fx.Doc(o.WithParam("friction", "0.9")).Doc), c => c.CheckId == "auto.braking.friction");
            // The default deceleration (8 m/s²) without a friction coefficient is not checked.
            Assert.DoesNotContain(Checks(r, Fx.Doc(o).Doc), c => c.CheckId == "auto.braking.friction");
        }

        [Fact]
        public void ValuesSurviveTheDocumentRoundTrip()
        {
            var r = Fx.Reg();
            var o = Item(r, "auto.resistance.uphill").WithParam("powerMax", "40 kW");
            var doc = Fx.Doc(o).Doc;
            var back = DocumentJson.Deserialize(DocumentJson.Serialize(doc));
            var o2 = back.Get(o.Id)!;
            Assert.Equal(Val(Solve(r, doc), o, "P"), Val(Solve(r, back), o2, "P"), 9);
        }
    }
}
