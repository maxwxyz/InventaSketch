using System;
using System.Globalization;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Electrical.Tests
{
    /// <summary>
    /// Round 6 (2026-11-26): AC analysis – impedance and phase of series/parallel RLC loads, per-element currents and phases,
    /// resonance (f₀, Q, B), AC power of a source, motor and generator values (three-phase, star-delta starting) and cable
    /// voltage drop with cos φ. Textbook cases [S7].
    /// </summary>
    public class AcCircuitTests
    {
        private static double V(ValueTable t, string id, string key) => t.Get(id, key)?.Value ?? double.NaN;
        private static double Deg(double rad) => rad * 180 / Math.PI;
        /// <summary>A number as typed with a decimal comma (German reading rules).</summary>
        private static string Num(double x) => x.ToString("R", CultureInfo.InvariantCulture).Replace('.', ',');

        /// <summary>AC source G1 with R1, L1 (and C1) in series.</summary>
        private static Circuit SeriesRlc(string source, string r, string l, string? c = null)
        {
            var k = new Circuit()
                .Part("G1", "elec.source", ("kind", "ac"), ("value", source))
                .Part("R1", "elec.resistor", ("value", r))
                .Part("L1", "elec.inductor", ("value", l))
                .Wire("G1", "+", "R1", "1").Wire("R1", "2", "L1", "1");
            return c is null
                ? k.Wire("L1", "2", "G1", "-")
                : k.Part("C1", "elec.capacitor", ("value", c)).Wire("L1", "2", "C1", "1").Wire("C1", "2", "G1", "-");
        }

        [Fact]
        public void SeriesRlOn230V50HzGivesTextbookImpedanceAndPhase()
        {
            // R = 100 Ω, L = 0.318 H at 50 Hz: X_L = 99.9 Ω, |Z| ≈ 141 Ω, φ ≈ 45°.
            var t = SeriesRlc("230 V 50 Hz", "100", "0,318 H").Solve();
            var xl = 2 * Math.PI * 50 * 0.318;
            var z = Math.Sqrt(100 * 100 + xl * xl);
            Assert.Equal(z, V(t, "G1", "Z"), 6);
            Assert.InRange(V(t, "G1", "Z"), 141, 142);
            Assert.Equal(Math.Atan2(xl, 100), V(t, "G1", "phi"), 9);
            Assert.InRange(Deg(V(t, "G1", "phi")), 44.9, 45.1);
            // The current, the voltages per element and the power.
            var i = 230 / z;
            Assert.Equal(i, V(t, "G1", "I"), 9);
            Assert.Equal(i, V(t, "R1", "I"), 9);
            Assert.Equal(i, V(t, "L1", "I"), 9);
            Assert.Equal(100 * i, V(t, "R1", "U"), 6);
            Assert.Equal(xl * i, V(t, "L1", "U"), 6);
            Assert.Equal(50, V(t, "L1", "f"), 12);
            Assert.Equal(100 / z, V(t, "G1", "lambda"), 9);
            Assert.Equal(i * i * 100, V(t, "G1", "P"), 6);
            Assert.Equal(i * i * 100, V(t, "R1", "P"), 6);
            Assert.Equal(230 * i, V(t, "G1", "S"), 6);
            Assert.Equal(i * i * xl, V(t, "G1", "Q"), 6);
            // Phases against the source voltage: the current lags by φ, the inductor's voltage leads its current by 90°.
            Assert.Equal(-Atan(xl / 100), V(t, "L1", "phiI"), 9);
            Assert.Equal(Math.PI / 2 - Atan(xl / 100), V(t, "L1", "phiU"), 9);
            Assert.Equal("elec.ac.z.load", t.Get("G1", "Z")!.Via!.Relation.Id);
            Assert.Empty(t.Conflicts);
        }

        private static double Atan(double x) => Math.Atan(x);

        [Fact]
        public void SeriesRlcAtResonanceIsPurelyResistive()
        {
            // L = 10 mH, C = 1 µF: f₀ = 1591.5 Hz; with R = 10 Ω, Q = √(L/C)/R = 10.
            const double l = 0.01, c = 1e-6, r = 10;
            var f0 = 1 / (2 * Math.PI * Math.Sqrt(l * c));
            var k = SeriesRlc("10 V", "10", "10 mH", "1µ").With("G1", "frequency", Num(f0) + " Hz");
            var t = k.Solve();
            Assert.Equal(f0, V(t, "C1", "f0"), 6);
            Assert.Equal(f0, V(t, "L1", "f0"), 6);
            Assert.Equal(r, V(t, "G1", "Z"), 6);
            Assert.Equal(0, V(t, "G1", "phi"), 6);
            Assert.Equal(1, V(t, "G1", "lambda"), 9);
            Assert.Equal(1, V(t, "R1", "I"), 6);
            Assert.Equal(10, V(t, "C1", "Qf"), 9);
            Assert.Equal(10, V(t, "L1", "Qf"), 9);
            Assert.Equal(f0 / 10, V(t, "C1", "B"), 6);
            // At resonance the voltages across L and C are Q times the source voltage, in opposite phase.
            Assert.Equal(100, V(t, "L1", "U"), 4);
            Assert.Equal(100, V(t, "C1", "U"), 4);
            Assert.Equal(Math.PI / 2, V(t, "L1", "phiU"), 6);
            Assert.Equal(-Math.PI / 2, V(t, "C1", "phiU"), 6);
            Assert.Empty(t.Conflicts);
            Assert.Contains(k.Checks(), f => f.CheckId == "elec.ac.resonance" && f.ObjectId == "C1");
            // Far away from f₀ (50 Hz) there is no hint.
            Assert.DoesNotContain(SeriesRlc("10 V 50 Hz", "10", "10 mH", "1µ").Checks(), f => f.CheckId == "elec.ac.resonance");
        }

        [Fact]
        public void ParallelRcCurrentsAddAsPhasorsNotAsNumbers()
        {
            // 230 V, 50 Hz across 230 Ω and a capacitor with X_C = 230 Ω: 1 A each, the source carries √2 A, φ = −45°.
            var c = 1 / (2 * Math.PI * 50 * 230);
            var t = new Circuit()
                .Part("G1", "elec.source", ("kind", "ac"), ("value", "230 V 50 Hz"))
                .Part("R1", "elec.resistor", ("value", "230"))
                .Part("C1", "elec.capacitor", ("value", Num(c) + " F"))
                .Wire("G1", "+", "R1", "1").Wire("R1", "2", "G1", "-")
                .Wire("G1", "+", "C1", "1").Wire("C1", "2", "G1", "-")
                .Solve();
            Assert.Equal(1, V(t, "R1", "I"), 9);
            Assert.Equal(1, V(t, "C1", "I"), 9);
            Assert.Equal(230, V(t, "C1", "U"), 9);
            Assert.Equal(Math.Sqrt(2), V(t, "G1", "I"), 9);
            Assert.Equal(-Math.PI / 4, V(t, "G1", "phi"), 9);
            Assert.Equal(Math.Cos(Math.PI / 4), V(t, "G1", "lambda"), 9);
            Assert.Equal(230, V(t, "G1", "P"), 6);
            Assert.Equal(-230, V(t, "G1", "Q"), 6);
            Assert.Equal(Math.PI / 2, V(t, "C1", "phiI"), 9);
            Assert.Equal(0, V(t, "C1", "phiU"), 9);
            Assert.Empty(t.Conflicts);
        }

        [Fact]
        public void TheSameRcOnDcKeepsTheDcSums()
        {
            // On a DC source the capacitor carries no steady current: the source current is that of the resistor (unchanged).
            var t = new Circuit()
                .Part("G1", "elec.source", ("value", "230 V"))
                .Part("R1", "elec.resistor", ("value", "230"))
                .Part("C1", "elec.capacitor", ("value", "10µ"))
                .Wire("G1", "+", "R1", "1").Wire("R1", "2", "G1", "-")
                .Wire("G1", "+", "C1", "1").Wire("C1", "2", "G1", "-")
                .Solve();
            Assert.Equal(1, V(t, "R1", "I"), 9);
            Assert.True(double.IsNaN(V(t, "C1", "I")));
            Assert.True(double.IsNaN(V(t, "C1", "f")));
        }

        [Fact]
        public void PurelyResistiveAcLoadNeedsNoFrequency()
        {
            var t = new Circuit()
                .Part("G1", "elec.source", ("kind", "ac"), ("value", "230 V"))
                .Part("R1", "elec.resistor", ("value", "100"))
                .Part("R2", "elec.resistor", ("value", "130"))
                .Wire("G1", "+", "R1", "1").Wire("R1", "2", "R2", "1").Wire("R2", "2", "G1", "-")
                .Solve();
            Assert.Equal(230, V(t, "G1", "Z"), 9);
            Assert.Equal(0, V(t, "G1", "phi"), 12);
            Assert.Equal(1, V(t, "G1", "lambda"), 12);
            Assert.Equal(1, V(t, "R1", "I"), 9);
            Assert.Equal(130, V(t, "R2", "U"), 9);
            Assert.Equal(230, V(t, "G1", "S"), 9);
            Assert.Empty(t.Conflicts);
        }

        [Fact]
        public void ParallelResonantCircuitGivesQualityAndBandwidth()
        {
            // L ∥ C ∥ R as a tank without a source: f₀ = 1/(2π√(LC)), Q = R·√(C/L).
            var t = new Circuit()
                .Part("L1", "elec.inductor", ("value", "1 mH"))
                .Part("C1", "elec.capacitor", ("value", "10n"))
                .Part("R1", "elec.resistor", ("value", "10k"))
                .Wire("L1", "1", "C1", "1").Wire("C1", "1", "R1", "1")
                .Wire("L1", "2", "C1", "2").Wire("C1", "2", "R1", "2")
                .Solve();
            var f0 = 1 / (2 * Math.PI * Math.Sqrt(1e-3 * 10e-9));
            var q = 10000 * Math.Sqrt(10e-9 / 1e-3);
            Assert.Equal(f0, V(t, "L1", "f0"), 6);
            Assert.Equal(q, V(t, "C1", "Qf"), 9);
            Assert.Equal(f0 / q, V(t, "L1", "B"), 6);
            Assert.Equal("elec.ac.quality.parallel", t.Get("C1", "Qf")!.Via!.Relation.Id);
        }

        [Fact]
        public void EnteredResonanceFrequencyGivesTheInductance()
        {
            var t = new Circuit()
                .Part("L1", "elec.inductor")
                .Part("C1", "elec.capacitor", ("value", "100n"), ("resonance", "10 kHz"))
                .Wire("L1", "1", "C1", "1").Wire("L1", "2", "C1", "2")
                .Solve();
            Assert.Equal(1 / (Math.Pow(2 * Math.PI * 1e4, 2) * 100e-9), V(t, "L1", "L"), 9);
            Assert.Equal(1e4, V(t, "L1", "f0"), 6);
        }

        [Fact]
        public void AcSourcePowerFromCosPhi()
        {
            var t = new Circuit().Part("G1", "elec.source", ("kind", "ac"), ("value", "230 V"), ("power", "1,84 kW"), ("pf", "0,8")).Solve();
            Assert.Equal(2300, V(t, "G1", "S"), 6);
            Assert.Equal(10, V(t, "G1", "I"), 9);
            Assert.Equal(23, V(t, "G1", "Z"), 9);
            // A DC source keeps P = U·I; its AC values are trivial, so it is fully determined.
            var dc = new Circuit().Part("G1", "elec.source", ("value", "12 V"), ("current", "2 A")).Solve();
            Assert.Equal(24, V(dc, "G1", "P"), 12);
            Assert.Equal(24, V(dc, "G1", "S"), 12);
            Assert.Equal(1, V(dc, "G1", "lambda"), 12);
            Assert.Equal(0, V(dc, "G1", "phi"), 12);
            Assert.Equal(0, V(dc, "G1", "Q"), 12);
            Assert.Equal(6, V(dc, "G1", "Z"), 12);
            Assert.Contains(dc.Status("G1"), new[] { ValueStatus.Determined, ValueStatus.Checked });
        }

        [Fact]
        public void ThreePhaseMotorPowerAndStarDeltaStartingCurrent()
        {
            // 400 V, 10 A, cos φ = 0.85: P = √3·U·I·cos φ = 5.89 kW.
            var c = new Circuit().Part("M1", "elec.motor", ("supply", "ac3"), ("voltage", "400 V"), ("current", "10 A"), ("pf", "0,85"),
                ("efficiency", "90 %"), ("kstart", "6"), ("starting", "stardelta"));
            var t = c.Solve();
            var s = Math.Sqrt(3) * 400 * 10;
            Assert.Equal(s, V(t, "M1", "S"), 6);
            Assert.Equal(s * 0.85, V(t, "M1", "P"), 6);
            Assert.Equal(s * Math.Sqrt(1 - 0.85 * 0.85), V(t, "M1", "Q"), 6);
            Assert.Equal(0.9 * s * 0.85, V(t, "M1", "Pm"), 6);
            Assert.Equal(60, V(t, "M1", "Ist"), 9);
            Assert.Equal(20, V(t, "M1", "IstYD"), 9);
            Assert.Empty(t.Conflicts);
            // Direct on line: no star-delta value.
            Assert.True(double.IsNaN(V(c.With("M1", "starting", "dol").Solve(), "M1", "IstYD")));
            // From the nameplate backwards: P_mech and η give P, with cos φ the line current.
            var back = new Circuit().Part("M1", "elec.motor", ("supply", "ac3"), ("voltage", "400 V"), ("pmech", "7,5 kW"), ("efficiency", "88 %"), ("pf", "0,82")).Solve();
            Assert.Equal(7500 / 0.88 / (Math.Sqrt(3) * 400 * 0.82), V(back, "M1", "I"), 9);
        }

        [Fact]
        public void SingleMotorOnAnAcSourceTakesItsVoltage()
        {
            var t = new Circuit()
                .Part("G1", "elec.source", ("kind", "ac"), ("value", "230 V"))
                .Part("M1", "elec.motor", ("supply", "ac"), ("power", "1 kW"), ("pf", "0,8"))
                .Wire("G1", "+", "M1", "1").Wire("M1", "2", "G1", "-")
                .Solve();
            Assert.Equal(230, V(t, "M1", "U"), 9);
            Assert.Equal(1000 / (230 * 0.8), V(t, "M1", "I"), 9);
            Assert.Equal(1000 / (230 * 0.8), V(t, "G1", "I"), 9);
            Assert.Empty(t.Conflicts);
        }

        [Fact]
        public void MotorInSeriesWithAResistorGetsNoVoltageSumOnAc()
        {
            // The motor has a power factor of its own: its voltage and the resistor's do not add as numbers.
            var t = new Circuit()
                .Part("G1", "elec.source", ("kind", "ac"), ("value", "230 V"))
                .Part("R1", "elec.resistor", ("voltage", "30 V"))
                .Part("M1", "elec.motor", ("supply", "ac"))
                .Wire("G1", "+", "R1", "1").Wire("R1", "2", "M1", "1").Wire("M1", "2", "G1", "-")
                .Solve();
            Assert.True(double.IsNaN(V(t, "M1", "U")));
        }

        [Fact]
        public void GeneratorEfficiency()
        {
            var t = new Circuit().Part("G1", "elec.generator", ("supply", "ac"), ("pmech", "10 kW"), ("efficiency", "95 %")).Solve();
            Assert.Equal(9500, V(t, "G1", "P"), 6);
        }

        private static Circuit Cable(params (string Key, string Value)[] ps) => new Circuit().Part("K1", "elec.house.cable", ps);

        [Fact]
        public void SinglePhaseCableVoltageDrop()
        {
            // 20 m NYM 3×1.5 mm² copper, 16 A, 230 V: R = 0.017241·20/1.5 Ω, ΔU = 2·R·I = 7.36 V, Δu = 3.2 %.
            var c = Cable(("length", "20 m"), ("section", "1,5 mm²"), ("current", "16 A"), ("voltage", "230 V"));
            var t = c.Solve();
            var r = 0.017241 * 20 / 1.5;
            Assert.Equal(r, V(t, "K1", "R"), 9);
            Assert.Equal(2 * r * 16, V(t, "K1", "dU"), 9);
            Assert.Equal(2 * r * 16 / 230, V(t, "K1", "du"), 9);
            Assert.Equal(2 * 16 * 16 * r, V(t, "K1", "Pl"), 9);
            Assert.Equal(Provenance.Default, t.Get("K1", "rho")!.Provenance);
            Assert.Contains(c.With("K1", "dumax", "3 %").Checks(), f => f.CheckId == "elec.cable.drop" && f.ObjectId == "K1");
            Assert.DoesNotContain(Cable(("length", "20 m"), ("section", "1,5 mm²"), ("current", "16 A"), ("voltage", "230 V"), ("dumax", "4 %")).Checks(),
                f => f.CheckId == "elec.cable.drop");
        }

        [Fact]
        public void ThreePhaseCableDropWithCosPhiAndAluminium()
        {
            // Five conductors: three-phase. ΔU = √3·R·I·cos φ.
            var t = Cable(("material", "al"), ("length", "50 m"), ("section", "16 mm²"), ("current", "40 A"),
                ("voltage", "400 V"), ("pf", "0,85")).With("K1", "conductors", 5).Solve();
            var r = 0.028264 * 50 / 16;
            Assert.Equal(r, V(t, "K1", "R"), 9);
            Assert.Equal(Math.Sqrt(3) * r * 40 * 0.85, V(t, "K1", "dU"), 9);
            Assert.Equal(3 * 40 * 40 * r, V(t, "K1", "Pl"), 9);
            // The permitted drop gives the required cross-section the other way round.
            var back = Cable(("length", "20 m"), ("current", "16 A"), ("voltage", "230 V"), ("droprel", "3 %")).Solve();
            Assert.Equal(2 * 0.017241e-6 * 20 * 16 / (0.03 * 230), V(back, "K1", "A"), 12);
        }

        [Fact]
        public void DcCableIgnoresThePowerFactor()
        {
            var t = Cable(("system", "dc"), ("length", "10 m"), ("section", "4 mm²"), ("current", "20 A"), ("pf", "0,5")).Solve();
            Assert.Equal(2 * 0.017241 * 10 / 4 * 20, V(t, "K1", "dU"), 9);
        }
    }
}
