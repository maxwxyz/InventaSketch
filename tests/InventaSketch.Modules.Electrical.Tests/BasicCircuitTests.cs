using System;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Electrical.Tests
{
    /// <summary>
    /// Round 5 (2026-11-26): RC/RL time constants and reactance, the potentiometer as a voltage divider, op-amp basic
    /// circuits read from the wiring and the transistor's base resistor.
    /// </summary>
    public class BasicCircuitTests
    {
        private static double V(ValueTable t, string id, string key) => t.Get(id, key)?.Value ?? double.NaN;

        [Fact]
        public void ResistorInSeriesWithACapacitorGivesTheTimeConstant()
        {
            var t = new Circuit()
                .Part("G1", "elec.source", ("value", "5 V"))
                .Part("R1", "elec.resistor", ("value", "10k"))
                .Part("C1", "elec.capacitor", ("value", "100n"))
                .Wire("G1", "+", "R1", "1").Wire("R1", "2", "C1", "1").Wire("C1", "2", "G1", "-")
                .Solve();
            Assert.Equal(10000, V(t, "C1", "Rt"), 6);
            Assert.Equal(1e-3, V(t, "C1", "tau"), 12);
            Assert.Equal(1 / (2 * Math.PI * 1e-3), V(t, "C1", "fc"), 6);
            Assert.Equal("elec.net.rtau", t.Get("C1", "Rt")!.Via!.Relation.Id);
            Assert.Empty(t.Conflicts);
        }

        [Fact]
        public void TwoResistorsInSeriesWithAnInductorAddUp()
        {
            var t = new Circuit()
                .Part("G1", "elec.source", ("value", "12 V"))
                .Part("R1", "elec.resistor", ("value", "60"))
                .Part("R2", "elec.resistor", ("value", "40"))
                .Part("L1", "elec.inductor", ("value", "10 mH"))
                .Wire("G1", "+", "R1", "1").Wire("R1", "2", "R2", "1").Wire("R2", "2", "L1", "1").Wire("L1", "2", "G1", "-")
                .Solve();
            Assert.Equal(100, V(t, "L1", "Rt"), 9);
            Assert.Equal(1e-4, V(t, "L1", "tau"), 12);
        }

        [Fact]
        public void ReactanceFollowsFromFrequencyAlone()
        {
            var t = new Circuit()
                .Part("C1", "elec.capacitor", ("value", "1µ"), ("frequency", "1 kHz"))
                .Part("L1", "elec.inductor", ("value", "10 mH"), ("frequency", "50 Hz"))
                .Solve();
            Assert.Equal(1 / (2 * Math.PI * 1000 * 1e-6), V(t, "C1", "X"), 6);
            Assert.Equal(2 * Math.PI * 50 * 0.01, V(t, "L1", "X"), 9);
        }

        [Fact]
        public void EnteredTimeConstantGivesTheResistance()
        {
            var t = new Circuit().Part("C1", "elec.capacitor", ("value", "100n"), ("cutoff", "1 kHz")).Solve();
            Assert.Equal(1 / (2 * Math.PI * 1000) / 100e-9, V(t, "C1", "Rt"), 3);
        }

        [Fact]
        public void PotentiometerIsAnUnloadedVoltageDivider()
        {
            var t = new Circuit()
                .Part("G1", "elec.source", ("value", "10 V"))
                .Part("P1", "elec.potentiometer", ("value", "10k"), ("wiper", "30 %"))
                .Wire("G1", "+", "P1", "1").Wire("P1", "2", "G1", "-")
                .Solve();
            Assert.Equal(10, V(t, "P1", "U"), 9);
            Assert.Equal(1e-3, V(t, "P1", "I"), 12);
            Assert.Equal(3000, V(t, "P1", "R1"), 6);
            Assert.Equal(7000, V(t, "P1", "R2"), 6);
            Assert.Equal(7, V(t, "P1", "Uw"), 9);
        }

        [Fact]
        public void APotentiometerWithALoadOnTheWiperIsNotReduced()
        {
            var t = new Circuit()
                .Part("G1", "elec.source", ("value", "10 V"))
                .Part("P1", "elec.potentiometer", ("value", "10k"), ("wiper", "30 %"))
                .Part("R1", "elec.resistor", ("value", "1k"))
                .Wire("G1", "+", "P1", "1").Wire("P1", "2", "G1", "-")
                .Wire("P1", "wiper", "R1", "1").Wire("R1", "2", "G1", "-")
                .Solve();
            // The loaded divider is no series/parallel network: the current through the track stays unknown.
            Assert.True(double.IsNaN(V(t, "P1", "I")));
        }

        /// <summary>Inverting amplifier: R1 from the input terminal to −, R2 from − to the output, + on earth.</summary>
        private static Circuit Inverting(string uin, string? supply = null)
        {
            var c = new Circuit()
                .Part("U1", "elec.opamp", ("uin", uin))
                .Part("R1", "elec.resistor", ("value", "10k"))
                .Part("R2", "elec.resistor", ("value", "100k"))
                .Part("X1", "elec.terminal").Part("E1", "elec.ground")
                .Wire("X1", "1", "R1", "1").Wire("R1", "2", "U1", "-").Wire("U1", "-", "R2", "1").Wire("R2", "2", "U1", "out")
                .Wire("U1", "+", "E1", "1");
            return supply is null ? c : c.With("U1", "usupply", supply);
        }

        [Fact]
        public void InvertingAmplifierIsRecognisedFromTheWiring()
        {
            var t = Inverting("0.5 V").Solve("U1");
            Assert.Equal(100000, V(t, "U1", "Rf"), 6);
            Assert.Equal(10000, V(t, "U1", "Rin"), 6);
            Assert.Equal(-10, V(t, "U1", "A"), 9);
            Assert.Equal(-5, V(t, "U1", "Uout"), 9);
            Assert.Equal("elec.opamp.inverting", t.Get("U1", "A")!.Via!.Relation.Id);
        }

        [Fact]
        public void OutputBeyondTheSupplyGivesTheSaturationWarning()
        {
            Assert.DoesNotContain(Inverting("0.5 V", "12 V").Checks(), f => f.CheckId == "elec.opamp.saturation");
            Assert.Contains(Inverting("2 V", "12 V").Checks(), f => f.CheckId == "elec.opamp.saturation" && f.ObjectId == "U1");
        }

        [Fact]
        public void NonInvertingAmplifierAndFollowerAreRecognised()
        {
            var t = new Circuit()
                .Part("U1", "elec.opamp", ("uin", "1 V"))
                .Part("R1", "elec.resistor", ("value", "90k"))
                .Part("R2", "elec.resistor", ("value", "10k"))
                .Part("X1", "elec.terminal").Part("E1", "elec.ground")
                .Wire("X1", "1", "U1", "+").Wire("U1", "-", "R1", "1").Wire("R1", "2", "U1", "out")
                .Wire("U1", "-", "R2", "1").Wire("R2", "2", "E1", "1")
                .Solve("U1");
            Assert.Equal(10, V(t, "U1", "A"), 9);
            Assert.Equal(10, V(t, "U1", "Uout"), 9);

            var f = new Circuit()
                .Part("U1", "elec.opamp", ("uin", "3.3 V")).Part("X1", "elec.terminal")
                .Wire("X1", "1", "U1", "+").Wire("U1", "out", "U1", "-")
                .Solve("U1");
            Assert.Equal(1, V(f, "U1", "A"), 12);
            Assert.Equal(3.3, V(f, "U1", "Uout"), 12);
        }

        [Fact]
        public void ChosenCircuitWorksWithoutWiring()
        {
            var t = new Circuit()
                .Part("U1", "elec.opamp", ("circuit", "noninverting"), ("rf", "47k"), ("rin", "4k7"), ("uin", "0.1 V"))
                .Solve();
            Assert.Equal(11, V(t, "U1", "A"), 9);
            Assert.Equal(1.1, V(t, "U1", "Uout"), 9);
            // A bare op-amp has no gain relation.
            Assert.True(double.IsNaN(V(new Circuit().Part("U2", "elec.opamp", ("uin", "1 V")).Solve(), "U2", "A")));
        }

        [Fact]
        public void TransistorSwitchGivesTheBaseResistor()
        {
            var t = new Circuit().Part("Q1", "elec.transistor.bjt", ("ic", "100 mA"), ("beta", "100"), ("uin", "5 V")).Solve();
            Assert.Equal(3e-3, V(t, "Q1", "Ib"), 12);
            Assert.Equal((5 - 0.7) / 3e-3, V(t, "Q1", "Rb"), 6);
        }

        [Fact]
        public void WiredBaseResistorDecidesTheBaseCurrentAndTheOverdriveCheck()
        {
            Circuit Wired(string rb) => new Circuit()
                .Part("Q1", "elec.transistor.bjt", ("ic", "100 mA"), ("beta", "100"), ("uin", "5 V"))
                .Part("R1", "elec.resistor", ("value", rb)).Part("X1", "elec.terminal")
                .Wire("X1", "1", "R1", "1").Wire("R1", "2", "Q1", "B");
            var ok = Wired("1k");
            var t = ok.Solve("Q1");
            Assert.Equal(1000, V(t, "Q1", "Rb"), 9);
            Assert.Equal(4.3e-3, V(t, "Q1", "Ib"), 12);
            Assert.Equal(4.3e-3, V(t, "R1", "I"), 12);
            Assert.Equal(4.3, V(t, "Q1", "k"), 9);
            Assert.DoesNotContain(ok.Checks(), f => f.CheckId == "elec.bjt.overdrive");
            Assert.Contains(Wired("10k").Checks(), f => f.CheckId == "elec.bjt.overdrive" && f.ObjectId == "Q1");
        }

        [Fact]
        public void NewValuesKeepEveryElementInsertableWithoutParameters()
        {
            var reg = Fx.Reg();
            foreach (var id in new[] { "elec.opamp", "elec.transistor.bjt", "elec.potentiometer", "elec.capacitor", "elec.inductor" })
            {
                var o = new SketchObject { Id = "x", Type = id, Size = reg.Type(id)!.DefaultSize };
                var doc = SketchDocument.Create();
                doc = doc with { Objects = new System.Collections.Generic.Dictionary<string, SketchObject> { ["x"] = o }, Pages = new[] { doc.Pages[0] with { Order = new[] { "x" } } } };
                var table = ValueSolver.Solve(doc, reg, null);
                Assert.Empty(table.Conflicts);
                Assert.NotEmpty(reg.Type(id)!.Render(o, new RenderContext(doc)));
            }
        }
    }
}
