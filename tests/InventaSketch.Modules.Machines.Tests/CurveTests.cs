using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Machines.Tests
{
    /// <summary>Calc 5: torque–speed curves (Kloss for the induction machine, envelope for the motor blocks), hand calculations.</summary>
    public class CurveTests
    {
        private static SketchDocument Doc(params SketchObject[] objs)
        {
            var doc = SketchDocument.Create(PageSizes.Slide16x9, "Curves");
            return Edits.Add(doc, doc.Pages[0].Id, objs);
        }

        private static CharacteristicSet Set(Registry r, SketchDocument doc) => Characteristics.Resolve(r, doc, Characteristics.Link("m", "torque"))!;

        private static double At(IReadOnlyList<Vec2> curve, double x)
        {
            for (var i = 1; i < curve.Count; i++)
                if (curve[i].X >= x)
                {
                    var t = (x - curve[i - 1].X) / (curve[i].X - curve[i - 1].X);
                    return curve[i - 1].Y + t * (curve[i].Y - curve[i - 1].Y);
                }
            return curve[^1].Y;
        }

        /// <summary>Kloss by hand: T = 2·T_k/(s/s_k + s_k/s).</summary>
        private static double Kloss(double s, double tk, double sk) => 2 * tk / (s / sk + sk / s);

        private static SketchObject Asm(Registry r) => r.Create(r.Item("machines.radial.asm")!, Vec2.Zero) with { Id = "m" };

        [Fact]
        public void KlossCurvePassesTheRatedPointAndPeaksAtTheBreakdownTorque()
        {
            var r = Fx.Reg();
            var m = Asm(r).WithParam("rotor", "asm").WithParam("poles", 4).WithParam("frequency", "50 Hz").WithParam("speed", "1440 rpm").WithParam("torque", "100 N·m");
            var set = Set(r, Doc(m));
            // n_s = 1500 min⁻¹, s_N = 4 %, λ = 2.5: s_k = 0.04·(2.5 + √5.25) = 0.191652, T_k = 250 N·m.
            var sk = 0.04 * (2.5 + Math.Sqrt(5.25));
            var op = set.Point("op")!;
            Assert.Equal(1440, op.X, 6);
            Assert.Equal(100, op.Y, 6);
            var bd = set.Point("breakdown")!;
            Assert.Equal(1500 * (1 - sk), bd.X, 6);
            Assert.Equal(250, bd.Y, 6);
            var motor = set.Curve("motor")!.Points;
            Assert.Equal(0, motor[^1].Y, 9); // no torque at synchronous speed
            Assert.Equal(1500, motor[^1].X, 9);
            // Starting torque T(s = 1) = 2·T_k/(1/s_k + s_k).
            Assert.Equal(2 * 250 / (1 / sk + sk), motor[0].Y, 6);
            Assert.True(motor.Max(p => p.Y) <= 250 + 1e-9);
            Assert.Equal(100, Kloss(0.04, 250, sk), 9);
        }

        [Fact]
        public void BreakdownRatioIsACurveParameter()
        {
            var r = Fx.Reg();
            var m = Asm(r).WithParam("rotor", "asm").WithParam("poles", 4).WithParam("frequency", "50 Hz").WithParam("speed", "1440 rpm").WithParam("torque", "100 N·m").WithParam("curveKloss", "3");
            Assert.Equal(300, Set(r, Doc(m)).Point("breakdown")!.Y, 6);
        }

        [Fact]
        public void DraggingTheOperatingPointEntersSpeedAndTorqueOnTheCurve()
        {
            var r = Fx.Reg();
            var m = Asm(r).WithParam("rotor", "asm").WithParam("poles", 4).WithParam("frequency", "50 Hz").WithParam("speed", "1440 rpm").WithParam("torque", "100 N·m");
            var doc = Doc(m);
            var set = Set(r, doc);
            var after = Characteristics.Apply(r, doc, set.Point("op")!.Write!(1470), Lang.En);
            var t = ValueSolver.Solve(after, r, new[] { "m" });
            const double rpm = 2 * Math.PI / 60;
            var sk = 0.04 * (2.5 + Math.Sqrt(5.25));
            Assert.Equal(1470 * rpm, t.Get("m", "n")!.Value!.Value, 6);
            Assert.Equal(Kloss(0.02, 250, sk), t.Get("m", "T")!.Value!.Value, 1); // stored with 3 significant digits like any dragged value
            // The rated point was taken from the operating point and is stored, so the curve stays.
            Assert.Equal(100, t.Get("m", "TN")!.Value!.Value, 6);
            var again = Set(r, after);
            Assert.Equal(250, again.Point("breakdown")!.Y, 6);
            Assert.Equal(1470, again.Point("op")!.X, 6);
            Assert.Empty(t.Conflicts);
        }

        [Fact]
        public void MotorBlockShowsTheTorqueEnvelope()
        {
            var r = Fx.Reg();
            const double rpm = 2 * Math.PI / 60;
            var pn = 200 * 3000 * rpm;
            var m = r.Type("machines.drive.motor")!.Create(Vec2.Zero) with { Id = "m" };
            m = m.WithParam("ratedTorque", "200 N·m").WithParam("ratedPower", (pn / 1000).ToString("R", System.Globalization.CultureInfo.InvariantCulture) + " kW");
            var set = Set(r, Doc(m));
            var env = set.Curve("envelope")!.Points;
            // Base speed P_N/T_N = 3000 min⁻¹, n_max = 3·n_b; constant power above: T(6000) = 100 N·m.
            Assert.Equal(200, At(env, 1500), 6);
            Assert.Equal(100, At(env, 6000), 3);
            Assert.Equal(9000, env[^1].X, 6);
            Assert.Equal(3000, set.Point("op")!.X, 6);
        }

        [Fact]
        public void PmMachineCrossSectionUsesTheEnvelope()
        {
            var r = Fx.Reg();
            var set = Set(r, Doc(Asm(r).WithParam("rotor", "spm")));
            Assert.NotNull(set.Curve("envelope"));
            Assert.Null(set.Curve("motor"));
        }
    }
}
