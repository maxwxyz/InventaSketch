using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Electrical.Tests
{
    /// <summary>Calc 5: Bode plot and step response of RC, RL and RLC networks (hand calculations).</summary>
    public class CurveTests
    {
        private static CharacteristicSet Set(Circuit c, string id, string key) => Characteristics.Resolve(c.Reg, c.Doc(), Characteristics.Link(id, key))!;

        private static Circuit Rc(string r = "1k", string cap = "100n") => new Circuit()
            .Part("G1", "elec.source", ("value", "5 V"))
            .Part("R1", "elec.resistor", ("value", r))
            .Part("C1", "elec.capacitor", ("value", cap))
            .Wire("G1", "+", "R1", "1")
            .Wire("R1", "2", "C1", "1")
            .Wire("C1", "2", "G1", "-");

        private static double At(IReadOnlyList<Vec2> curve, double x)
        {
            var best = curve.OrderBy(p => Math.Abs(Math.Log(p.X / x))).First();
            return best.Y;
        }

        [Fact]
        public void RcCornerFrequencyIsAtMinusThreeDecibels()
        {
            var c = Rc();
            var set = Set(c, "C1", "bode");
            // f_c = 1/(2π·1 kΩ·100 nF) = 1591.55 Hz; |H(f_c)| = −10·lg 2 = −3.0103 dB, φ = −45°.
            var fc = 1 / (2 * Math.PI * 1e3 * 100e-9);
            var p = set.Point("fc")!;
            Assert.Equal(fc, p.X, 6);
            Assert.Equal(-10 * Math.Log10(2), p.Y, 9);
            Assert.Equal(-45, At(set.Curve("phase")!.Points, fc), 1);
            Assert.Equal(-20, At(set.Curve("magnitude")!.Points, 10 * fc), 0); // −20 dB per decade above f_c
            Assert.True(set.X.Log);
            Assert.Empty(set.Notes);
            // The resistor of the same circuit offers the same plot.
            Assert.Equal(fc, Set(c, "R1", "bode").Point("fc")!.X, 6);
        }

        [Fact]
        public void RcStepResponseReachesSixtyThreePercentAtTau()
        {
            var set = Set(Rc(), "R1", "step");
            // τ = R·C = 100 µs; 1 − e⁻¹ = 63.21 %.
            var tau = set.Point("tau")!;
            Assert.Equal("µs", set.X.Unit);
            Assert.Equal(100, tau.X, 9);
            Assert.Equal(100 * (1 - Math.Exp(-1)), tau.Y, 9);
            Assert.Equal(500, set.Curve("step")!.Points[^1].X, 9);
        }

        [Fact]
        public void DraggingTheCornerFrequencyEntersTheCapacitance()
        {
            var c = Rc();
            var doc = c.Doc();
            var point = Set(c, "C1", "bode").Point("fc")!;
            var after = Characteristics.Apply(c.Reg, doc, point.Write!(1000), Lang.En);
            var t = ValueSolver.Solve(after, c.Reg, new[] { "C1" });
            // C = 1/(2π·1000 Hz·1 kΩ) = 159 nF (3 significant digits as dragged values are stored); R stays.
            Assert.Equal(159e-9, t.Get("C1", "C")!.Value!.Value, 12);
            Assert.Equal("1k", after.Get("R1")!.Str("value", ""));
        }

        [Fact]
        public void SeriesRlcHasNaturalFrequencyAndDamping()
        {
            var c = new Circuit()
                .Part("G1", "elec.source", ("value", "5 V"))
                .Part("R1", "elec.resistor", ("value", "100"))
                .Part("L1", "elec.inductor", ("value", "10m"))
                .Part("C1", "elec.capacitor", ("value", "100n"))
                .Wire("G1", "+", "R1", "1").Wire("R1", "2", "L1", "1").Wire("L1", "2", "C1", "1").Wire("C1", "2", "G1", "-");
            var set = Set(c, "L1", "bode");
            // f₀ = 1/(2π·√(10 mH·100 nF)) = 5032.9 Hz; ζ = (R/2)·√(C/L) = 50·√(1e-5) = 0.158; |H(f₀)| = 1/(2ζ) → −20·lg(2ζ).
            var f0 = 1 / (2 * Math.PI * Math.Sqrt(10e-3 * 100e-9));
            var zeta = 50 * Math.Sqrt(1e-5);
            var p = set.Point("fc")!;
            Assert.Equal(f0, p.X, 6);
            Assert.Equal(-20 * Math.Log10(2 * zeta), p.Y, 9);
            var step = Set(c, "C1", "step");
            var peak = step.Point("peak")!;
            // Overshoot of a PT2: e^(−πζ/√(1 − ζ²)) = 60.1 %.
            Assert.Equal(100 * (1 + Math.Exp(-Math.PI * zeta / Math.Sqrt(1 - zeta * zeta))), peak.Y, 6);
        }

        [Fact]
        public void ResistorAloneOffersNoNetworkCurves()
        {
            var c = new Circuit().Part("G1", "elec.source", ("value", "5 V")).Part("R1", "elec.resistor", ("value", "1k"))
                .Wire("G1", "+", "R1", "1").Wire("R1", "2", "G1", "-");
            var doc = c.Doc();
            Assert.Empty(Characteristics.SetsOf(c.Reg, doc.Get("R1")!, new RenderContext(doc)));
            var rc = Rc().Doc();
            Assert.Equal(new[] { "bode", "step" }, Characteristics.SetsOf(c.Reg, rc.Get("R1")!, new RenderContext(rc)).Select(s => s.Key).ToArray());
        }
    }
}
