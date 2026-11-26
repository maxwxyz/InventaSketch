using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Electrical.Tests
{
    /// <summary>A small circuit builder: parts with parameters, wires docked to pins (as the hosts dock them).</summary>
    internal sealed class Circuit
    {
        private readonly Dictionary<string, SketchObject> _objects = new(StringComparer.Ordinal);
        private readonly List<string> _order = new();
        private int _wires;

        public Registry Reg { get; } = Fx.Reg();

        public Circuit Part(string id, string type, params (string Key, string Value)[] ps)
        {
            var o = new SketchObject { Id = id, Type = type };
            foreach (var (k, v) in ps) o = o.WithParam(k, v);
            _objects[id] = o;
            _order.Add(id);
            return this;
        }

        public Circuit With(string id, string key, ParamValue value)
        {
            _objects[id] = _objects[id].WithParam(key, value);
            return this;
        }

        public Circuit Text(string id, string text)
        {
            _objects[id] = _objects[id] with { Text = text };
            return this;
        }

        /// <summary>A wire from pin <paramref name="pa"/> of <paramref name="a"/> to pin <paramref name="pb"/> of <paramref name="b"/>.</summary>
        public Circuit Wire(string a, string pa, string b, string pb, string type = "elec.wire")
        {
            var id = "w" + (++_wires);
            _objects[id] = new SketchObject
            {
                Id = id, Type = type, Points = new[] { new Vec2(0, 0), new Vec2(10, 0) },
                Attach = new Attachment?[] { new Attachment(a, -0.5, 0) { AnchorId = pa }, new Attachment(b, 0.5, 0) { AnchorId = pb } },
            };
            _order.Add(id);
            return this;
        }

        public SketchDocument Doc()
        {
            var doc = SketchDocument.Create();
            return doc with { Objects = new Dictionary<string, SketchObject>(_objects), Pages = new[] { doc.Pages[0] with { Order = _order.ToList() } } };
        }

        // A generous budget: the default 50 ms truncated the solve while other test assemblies kept every core busy.
        public ValueTable Solve(params string[] ids) => ValueSolver.Solve(Doc(), Reg, ids.Length == 0 ? null : ids, new ValueSolveOptions { BudgetMs = 10000 });

        public IReadOnlyList<CheckFinding> Checks()
        {
            var doc = Doc();
            return ValueSolver.Checks(ValueSolver.Solve(doc, Reg, null), Reg, new RenderContext(doc));
        }
    }

    /// <summary>Calc 3 (electrical): element relations, propagation through wires, network reduction, markings and checks.</summary>
    public class CircuitValueTests
    {
        private static double V(ValueTable t, string id, string key) => t.Get(id, key)!.Value ?? double.NaN;

        /// <summary>5 V source, R1 and an LED in one loop.</summary>
        private static Circuit LedCircuit(string resistor = "4k7", string led = "2.1 V")
            => new Circuit()
                .Part("G1", "elec.source", ("value", "5 V"))
                .Part("R1", "elec.resistor", ("value", resistor))
                .Part("D1", "elec.diode.led", ("uf", led))
                .Wire("G1", "+", "R1", "1")
                .Wire("R1", "2", "D1", "A")
                .Wire("D1", "K", "G1", "-");

        [Fact]
        public void LedSeriesResistorGivesTheCurrent()
        {
            // Accept (Calc 3): "4k7" on a resistor in a 5 V series circuit with an LED (2.1 V) gives I ≈ 0.62 mA.
            var t = LedCircuit().Solve();
            var i = (5 - 2.1) / 4700;
            Assert.Equal(i, V(t, "R1", "I"), 12);
            Assert.Equal(0.62e-3, V(t, "R1", "I"), 5);
            Assert.Equal(2.9, V(t, "R1", "U"), 9);
            Assert.Equal(i, V(t, "D1", "I"), 12);
            Assert.Equal(i, V(t, "G1", "I"), 12);
            Assert.Equal(2.1 * i, V(t, "D1", "P"), 12);
            Assert.Equal(5 * i, V(t, "G1", "P"), 12);
            Assert.Empty(t.Conflicts);
            Assert.Equal(Provenance.Linked, t.Get("D1", "I")!.Provenance);
            Assert.Equal("elec.marking", t.Get("G1", "U")!.Via!.Relation.Id);
        }

        [Fact]
        public void AToleranceOnTheResistorGivesTheCurrentAsARange()
        {
            // Accept (Calc 6): "4k7 ±5 %" in the LED circuit shows I as an interval: 2.9 V / (4.7 kΩ ± 5 %).
            var t = LedCircuit("4k7 ±5 %").Solve();
            var i = t.Get("R1", "I")!;
            Assert.Equal(2.9 / 4700, i.Value!.Value, 12);
            var range = i.Interval!.Value;
            Assert.Equal(2.9 / (4700 * 1.05), range.Lo, 12);
            Assert.Equal(2.9 / (4700 * 0.95), range.Hi, 12);
            // The LED's current is the same range (series circuit).
            var led = t.Get("D1", "I")!.Interval!.Value;
            Assert.Equal(range.Lo, led.Lo, 12);
            Assert.Equal(range.Hi, led.Hi, 12);
        }

        [Fact]
        public void ASingleElementSeesTheWholeCircuit()
        {
            // The card and the values label solve one element: the circuit joins the scope through its owner.
            var c = LedCircuit();
            foreach (var id in new[] { "R1", "D1", "G1" })
            {
                var t = c.Solve(id);
                Assert.Equal((5 - 2.1) / 4700, V(t, id, "I"), 12);
            }
            var explain = c.Solve("D1").Explain("D1", "I", Lang.En);
            Assert.Contains(explain, s => s.RelationId == "elec.ohm");
            Assert.Contains(explain, s => s.RelationId == "elec.net.parallel" || s.RelationId == "elec.net.closed");
        }

        private static Circuit GroundedLed(string led) => new Circuit()
            .Part("G1", "elec.battery", ("value", "5 V"))
            .Part("R1", "elec.resistor", ("value", "4k7"))
            .Part("D1", "elec.diode.led", ("value", led))
            .Part("E1", "elec.ground").Part("E2", "elec.ground")
            .Part("L1", "elec.netlabel").Text("L1", "VCC").Part("L2", "elec.netlabel").Text("L2", "VCC")
            .Wire("G1", "+", "L1", "1")
            .Wire("L2", "1", "R1", "1")
            .Wire("R1", "2", "D1", "A")
            .Wire("D1", "K", "E1", "1")
            .Wire("G1", "-", "E2", "1");

        [Fact]
        public void EarthSymbolsAndNetLabelsJoinNets()
        {
            var t = GroundedLed("2.1 V").Solve();
            Assert.Equal(2.1, V(t, "D1", "U"), 9);
            Assert.Equal(2.9, V(t, "R1", "U"), 9);
            Assert.Equal((5 - 2.1) / 4700, V(t, "G1", "I"), 12);
            Assert.Empty(t.Conflicts);
            // "2.1 V 20 mA" also gives a current: R = 4k7 with 2.9 V implies 0.617 mA, so the circuit reports a conflict.
            Assert.NotEmpty(GroundedLed("2.1 V 20 mA").Solve().Conflicts);
            // A lone net label leads elsewhere: the loop is open there and no current is derived.
            var open = GroundedLed("2.1 V").Text("L2", "VDD").Solve();
            Assert.Null(open.Get("R1", "I")!.Value);
        }

        [Fact]
        public void OpenSwitchBreaksTheCircuitClosedSwitchConducts()
        {
            Circuit WithSwitch(bool closed)
            {
                var c = new Circuit()
                    .Part("G1", "elec.source", ("value", "5 V"))
                    .Part("S1", "elec.switch")
                    .Part("R1", "elec.resistor", ("value", "4k7"))
                    .Part("D1", "elec.diode.led", ("uf", "2.1 V"))
                    .Wire("G1", "+", "S1", "1").Wire("S1", "2", "R1", "1").Wire("R1", "2", "D1", "A").Wire("D1", "K", "G1", "-");
                return closed ? c.With("S1", "closed", true) : c;
            }
            Assert.Null(WithSwitch(false).Solve().Get("R1", "I")!.Value);
            Assert.Equal((5 - 2.1) / 4700, V(WithSwitch(true).Solve(), "R1", "I"), 12);
        }

        [Fact]
        public void VoltageDividerFromSourceAndResistances()
        {
            var t = new Circuit()
                .Part("G1", "elec.source", ("voltage", "10 V"))
                .Part("R1", "elec.resistor", ("value", "1k"))
                .Part("R2", "elec.resistor", ("value", "4k"))
                .Wire("G1", "+", "R1", "1").Wire("R1", "2", "R2", "1").Wire("R2", "2", "G1", "-")
                .Solve();
            // U₂ = U·R₂/(R₁+R₂) = 8 V; I = U/(R₁+R₂) = 2 mA.
            Assert.Equal(0.002, V(t, "R1", "I"), 12);
            Assert.Equal(8, V(t, "R2", "U"), 9);
            Assert.Equal(2, V(t, "R1", "U"), 9);
            Assert.Equal(0.002, V(t, "G1", "I"), 12);
            Assert.Equal(ValueStatus.Determined, t.Status("R2"));
            var steps = t.Explain("R2", "U", Lang.En);
            Assert.Contains(steps, s => s.RelationId == "elec.net.req");
        }

        [Fact]
        public void LoadedDividerReducesTheParallelPart()
        {
            var t = new Circuit()
                .Part("G1", "elec.source", ("voltage", "10 V"))
                .Part("R1", "elec.resistor", ("value", "1k"))
                .Part("R2", "elec.resistor", ("value", "2k"))
                .Part("R3", "elec.resistor", ("value", "2k"))
                .Wire("G1", "+", "R1", "1").Wire("R1", "2", "R2", "1").Wire("R1", "2", "R3", "1")
                .Wire("R2", "2", "G1", "-").Wire("R3", "2", "R2", "2")
                .Solve();
            // R₂ ∥ R₃ = 1 kΩ: 5 V across each, 5 mA through R1, 2.5 mA through R2 and R3.
            Assert.Equal(5, V(t, "R2", "U"), 9);
            Assert.Equal(5, V(t, "R3", "U"), 9);
            Assert.Equal(0.005, V(t, "R1", "I"), 12);
            Assert.Equal(0.0025, V(t, "R3", "I"), 12);
            Assert.Equal(0.005, V(t, "G1", "I"), 12);
        }

        [Fact]
        public void ParallelCurrentsAddAndTheMissingResistanceFollows()
        {
            var c = new Circuit()
                .Part("G1", "elec.source", ("voltage", "12 V"), ("current", "3 A"))
                .Part("R1", "elec.resistor", ("value", "12"))
                .Part("R2", "elec.resistor")
                .Wire("G1", "+", "R1", "1").Wire("G1", "+", "R2", "1").Wire("R1", "2", "G1", "-").Wire("R2", "2", "G1", "-");
            var t = c.Solve();
            Assert.Equal(1, V(t, "R1", "I"), 12);
            // 3 A total with 1 A through R1 leaves 2 A through R2 at 12 V: R2 = 6 Ω.
            Assert.Equal(2, V(t, "R2", "I"), 9);
            Assert.Equal(6, V(t, "R2", "R"), 6);
        }

        [Fact]
        public void BridgeIsNotReducibleAndSaysSo()
        {
            // Accept (Calc 3): a bridge network reports "not reducible" (a hint, no exception).
            var c = new Circuit()
                .Part("G1", "elec.source", ("voltage", "10 V"))
                .Part("R1", "elec.resistor", ("value", "1k")).Part("R2", "elec.resistor", ("value", "2k"))
                .Part("R3", "elec.resistor", ("value", "2k")).Part("R4", "elec.resistor", ("value", "1k"))
                .Part("R5", "elec.resistor", ("value", "5k"))
                .Wire("G1", "+", "R1", "1").Wire("G1", "+", "R3", "1")
                .Wire("R1", "2", "R2", "1").Wire("R3", "2", "R4", "1")
                .Wire("R1", "2", "R5", "1").Wire("R3", "2", "R5", "2")
                .Wire("R2", "2", "G1", "-").Wire("R4", "2", "G1", "-")
                .With("R1", "voltage", "4 V");
            var findings = c.Checks().Where(f => f.CheckId == "elec.net.reducible").Select(f => f.ObjectId).OrderBy(x => x).ToList();
            Assert.Equal(new[] { "G1", "R1", "R2", "R3", "R4", "R5" }, findings);
            var f0 = c.Checks().First(f => f.CheckId == "elec.net.reducible");
            Assert.Equal(CheckSeverity.Info, f0.Severity);
            Assert.Contains("not reducible", f0.Message.En, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("nicht reduzierbar", f0.Message.De, StringComparison.OrdinalIgnoreCase);
            // Local values still work.
            var t = c.Solve();
            Assert.Equal(0.004, V(t, "R1", "I"), 12);
            Assert.Null(t.Get("R2", "I")!.Value);
        }

        [Fact]
        public void SeriesParallelCircuitIsNoBridge()
        {
            var findings = new Circuit()
                .Part("G1", "elec.source", ("voltage", "10 V"))
                .Part("R1", "elec.resistor", ("value", "1k")).Part("R2", "elec.resistor", ("value", "2k"))
                .Wire("G1", "+", "R1", "1").Wire("R1", "2", "R2", "1").Wire("R2", "2", "G1", "-")
                .Checks();
            Assert.DoesNotContain(findings, f => f.CheckId == "elec.net.reducible");
        }

        [Fact]
        public void CapacitorsInSeriesShareTheCharge()
        {
            var t = new Circuit()
                .Part("G1", "elec.source", ("voltage", "12 V"))
                .Part("C1", "elec.capacitor", ("value", "10µ"))
                .Part("C2", "elec.capacitor.polarized", ("value", "10 µF"))
                .Wire("G1", "+", "C1", "1").Wire("C1", "2", "C2", "+").Wire("C2", "-", "G1", "-")
                .Solve();
            // C = 5 µF in series: Q = 60 µC on each, 6 V across each.
            Assert.Equal(60e-6, V(t, "C1", "Q"), 15);
            Assert.Equal(60e-6, V(t, "C2", "Q"), 15);
            Assert.Equal(6, V(t, "C1", "U"), 9);
            Assert.Equal(0.5 * 10e-6 * 36, V(t, "C2", "E"), 15);
        }

        [Fact]
        public void CapacitorAndInductorLocalRelations()
        {
            var c = new Circuit().Part("C1", "elec.capacitor", ("value", "100n"), ("voltage", "5 V"))
                .Part("L1", "elec.inductor", ("value", "10 mH"), ("current", "2 A"));
            var t = c.Solve();
            Assert.Equal(0.5e-6, V(t, "C1", "Q"), 15);
            Assert.Equal(1.25e-6, V(t, "C1", "E"), 15);
            Assert.Equal(0.02, V(t, "L1", "E"), 12);
            // The time constant, cut-off frequency and reactance stay open without a resistance or a frequency.
            Assert.Empty(t.Conflicts);
            // Any two give the rest: E and Q give C and U.
            var back = new Circuit().Part("C1", "elec.capacitor", ("energy", "1,25 mJ"), ("charge", "0,5 mC")).Solve();
            Assert.Equal(100e-6, V(back, "C1", "C"), 15);
            Assert.Equal(5, V(back, "C1", "U"), 9);
        }

        [Fact]
        public void TransformerPassesValuesFromSecondaryToPrimary()
        {
            var t = new Circuit()
                .Part("G1", "elec.source", ("kind", "ac"), ("value", "230 V"))
                .Part("T1", "elec.transformer", ("value", "230/12 V"))
                .Part("E1", "elec.lamp", ("value", "12 V 6 W"))
                .Wire("G1", "+", "T1", "p1").Wire("T1", "p2", "G1", "-")
                .Wire("T1", "s1", "E1", "1").Wire("T1", "s2", "E1", "2")
                .Solve();
            Assert.Equal(230.0 / 12, V(t, "T1", "n"), 9);
            Assert.Equal(0.5, V(t, "E1", "I"), 12);
            Assert.Equal(0.5, V(t, "T1", "I2"), 12);
            Assert.Equal(0.5 * 12 / 230, V(t, "T1", "I1"), 12);
            Assert.Equal(0.5 * 12 / 230, V(t, "G1", "I"), 12);
            Assert.Equal(24, V(t, "E1", "R"), 9);
            Assert.Empty(t.Conflicts);
        }

        [Theory]
        [InlineData("B16", 16)]
        [InlineData("C 10 A", 10)]
        [InlineData("gG 35 A", 35)]
        [InlineData("16 A", 16)]
        [InlineData("16", 16)]
        public void ProtectiveDeviceMarkingGivesTheRatedCurrent(string marking, double amps)
        {
            var t = new Circuit().Part("F1", "elec.breaker", ("value", marking)).Part("F2", "elec.fuse", ("value", marking)).Solve();
            Assert.Equal(amps, V(t, "F1", "In"), 12);
            Assert.Equal(amps, V(t, "F2", "In"), 12);
            Assert.Equal(Provenance.Derived, t.Get("F1", "In")!.Provenance);
            Assert.Equal("I_n = " + marking + " = " + amps + " A", t.Explain("F1", "In", Lang.En).Single().Text);
        }

        [Theory]
        [InlineData("40 A 30 mA", 40.0, 0.03)]
        [InlineData("40/0,03 A", 40.0, 0.03)]
        [InlineData("30 mA", null, 0.03)]
        [InlineData("63 A", 63.0, null)]
        public void RcdMarkingGivesRatedAndResidualCurrent(string marking, double? rated, double? residual)
        {
            var t = new Circuit().Part("F1", "elec.rcd", ("value", marking)).Solve();
            Assert.Equal(rated, t.Get("F1", "In")!.Value);
            if (residual is null) Assert.Null(t.Get("F1", "IDn")!.Value);
            else Assert.Equal(residual.Value, t.Get("F1", "IDn")!.Value!.Value, 12);
        }

        [Fact]
        public void LampMarkingAndAnEnteredValueThatDisagreesGiveAConflict()
        {
            var t = new Circuit().Part("E1", "elec.lamp", ("value", "12 V 5 W")).Solve();
            Assert.Equal(5 / 12.0, V(t, "E1", "I"), 12);
            Assert.Equal(28.8, V(t, "E1", "R"), 9);
            var c = new Circuit().Part("E1", "elec.lamp", ("value", "12 V"), ("voltage", "24 V")).Solve();
            Assert.Equal(ValueStatus.Conflict, c.Status("E1"));
            Assert.Equal(new VarRef("E1", "U"), c.Conflicts.Single().Involved.Single());
            // A value text that is no marking ("1N4148", "red") gives nothing and no error.
            var none = new Circuit().Part("D1", "elec.diode", ("value", "1N4148")).Part("E2", "elec.lamp", ("value", "red")).Solve();
            Assert.Equal(ValueStatus.Empty, none.Status("D1"));
            Assert.Equal(ValueStatus.Empty, none.Status("E2"));
            Assert.All(none.Of("D1").Concat(none.Of("E2")), v => Assert.Null(v.Error));
        }

        [Fact]
        public void CurrentSourceMarkingIsACurrent()
        {
            var t = new Circuit()
                .Part("G1", "elec.source", ("kind", "current"), ("value", "10 mA"))
                .Part("R1", "elec.resistor", ("value", "1k"))
                .Wire("G1", "+", "R1", "1").Wire("R1", "2", "G1", "-")
                .Solve();
            Assert.Equal(0.01, V(t, "G1", "I"), 12);
            Assert.Equal(10, V(t, "R1", "U"), 9);
            Assert.Equal(10, V(t, "G1", "U"), 9);
        }

        [Fact]
        public void B16OnACircuitWithIz13GivesTheCoordinationWarning()
        {
            // Accept (Calc 3): a B16 MCB on a circuit with I_z = 13 A (user) gives the coordination warning (I_n > I_z).
            var findings = new Circuit().Part("F1", "elec.breaker", ("value", "B16"), ("iz", "13 A")).Checks();
            var f = Assert.Single(findings);
            Assert.Equal("elec.coord.overload", f.CheckId);
            Assert.Equal(CheckSeverity.Warning, f.Severity);
            Assert.Equal("S9", f.Source);
            Assert.Equal("I_B ≤ I_n ≤ I_z", f.Rule);
            Assert.Contains("I_z", f.Message.De);
            Assert.Empty(new Circuit().Part("F1", "elec.breaker", ("value", "B16"), ("iz", "16 A")).Checks());
            Assert.Empty(new Circuit().Part("F1", "elec.breaker", ("value", "B16")).Checks());
        }

        [Fact]
        public void LoadCurrentAboveTheRatedCurrentGivesTheCoordinationWarning()
        {
            Circuit Mcb(string rating) => new Circuit()
                .Part("G1", "elec.source", ("kind", "ac"), ("value", "230 V"))
                .Part("F1", "elec.breaker", ("value", rating))
                .Part("R1", "elec.resistor", ("power", "3 kW"))
                .Wire("G1", "+", "F1", "1").Wire("F1", "2", "R1", "1").Wire("R1", "2", "G1", "-");
            var t = Mcb("B10").Solve();
            Assert.Equal(3000 / 230.0, V(t, "F1", "I"), 9);
            Assert.Equal(230, V(t, "R1", "U"), 9);
            Assert.Contains(Mcb("B10").Checks(), f => f.CheckId == "elec.coord.overload" && f.ObjectId == "F1");
            Assert.DoesNotContain(Mcb("B16").Checks(), f => f.CheckId == "elec.coord.overload");
        }

        [Fact]
        public void BreakerSwitchedOffExplicitlyBreaksTheCircuit()
        {
            var c = new Circuit()
                .Part("G1", "elec.source", ("value", "230 V"))
                .Part("F1", "elec.breaker", ("value", "B16"))
                .Part("R1", "elec.resistor", ("value", "100"))
                .Wire("G1", "+", "F1", "1").Wire("F1", "2", "R1", "1").Wire("R1", "2", "G1", "-");
            // Drawn in its rest position (no "closed" parameter): it counts as conducting.
            Assert.Equal(2.3, V(c.Solve(), "F1", "I"), 9);
            Assert.Null(c.With("F1", "closed", false).Solve().Get("F1", "I")!.Value);
        }

        [Fact]
        public void SingleLineSocketsAddUpInTheFeeder()
        {
            var c = new Circuit()
                .Part("W1", "elec.sld.busbar")
                .Part("Q1", "elec.sld.feeder", ("value", "B16"))
                .Part("J1", "elec.house.junction")
                .Part("S1", "elec.house.socket", ("voltage", "230 V"), ("power", "2300 W"))
                .Part("S2", "elec.house.socket", ("power", "1150 W"))
                .Wire("W1", "tap0", "Q1", "bus")
                .Wire("Q1", "load", "J1", "n", "elec.house.cable")
                .Wire("J1", "e", "S1", "feed", "elec.house.cable")
                .Wire("J1", "s", "S2", "feed", "elec.house.cable");
            var t = c.Solve();
            Assert.Equal(230, V(t, "S2", "U"), 9);
            Assert.Equal(5, V(t, "S2", "I"), 9);
            Assert.Equal(15, V(t, "Q1", "I"), 9);
            Assert.Equal(16, V(t, "Q1", "In"), 9);
            Assert.DoesNotContain(c.Checks(), f => f.CheckId == "elec.coord.overload");
            Assert.Contains(c.With("Q1", "value", "B13").Checks(), f => f.CheckId == "elec.coord.overload");
        }

        [Fact]
        public void SocketCircuitWithoutA30MilliampRcdGetsAHint()
        {
            Circuit Plan(string? rcd)
            {
                var c = new Circuit()
                    .Part("W1", "elec.sld.busbar")
                    .Part("Q1", "elec.sld.feeder", ("value", "B16"))
                    .Part("S1", "elec.house.socket")
                    .Wire("W1", "tap0", "Q1", "bus");
                if (rcd is null) return c.Wire("Q1", "load", "S1", "feed", "elec.house.cable");
                return c.Part("F1", "elec.rcd", ("value", rcd))
                    .Wire("Q1", "load", "F1", "Lin", "elec.house.cable")
                    .Wire("F1", "Lout", "S1", "feed", "elec.house.cable");
            }
            var hint = Assert.Single(Plan(null).Checks(), f => f.CheckId == "elec.rcd.additional");
            Assert.Equal("S1", hint.ObjectId);
            Assert.Equal(CheckSeverity.Hint, hint.Severity);
            Assert.Equal("S10", hint.Source);
            Assert.Contains("national", hint.Message.En);
            Assert.Contains("national", hint.Message.De);
            Assert.DoesNotContain(Plan("40 A 30 mA").Checks(), f => f.CheckId == "elec.rcd.additional");
            Assert.Contains(Plan("40 A 300 mA").Checks(), f => f.CheckId == "elec.rcd.additional");
            // An RCD without a rating counts as present; a socket without any protective device drawn gets no hint.
            Assert.DoesNotContain(Plan("").Checks(), f => f.CheckId == "elec.rcd.additional");
            Assert.Empty(new Circuit().Part("S1", "elec.house.socket").Checks());
        }

        [Fact]
        public void ResistorPowerAboveRatingGivesAWarning()
        {
            Circuit R(string rating) => new Circuit().Part("R1", "elec.resistor", ("value", "100"), ("voltage", "10 V"), ("prated", rating));
            var f = Assert.Single(R("0.25 W").Checks());
            Assert.Equal("elec.r.power", f.CheckId);
            Assert.Equal("S7", f.Source);
            Assert.Empty(R("2 W").Checks());
            Assert.Empty(R("").Checks());
            // The rating is a plain parameter: it never makes the resistor look under-determined.
            Assert.Equal(ValueStatus.Determined, R("0.25 W").Solve().Status("R1"));
        }

        [Fact]
        public void LedCurrentAboveItsMaximumGivesAWarning()
        {
            var c = LedCircuit("100").With("D1", "ifmax", "20 mA");
            Assert.Contains(c.Checks(), f => f.CheckId == "elec.led.current" && f.ObjectId == "D1");
            Assert.DoesNotContain(LedCircuit("4k7").With("D1", "ifmax", "20 mA").Checks(), f => f.CheckId == "elec.led.current");
        }

        [Fact]
        public void ValuesRoundTripThroughEditsAndJson()
        {
            var c = LedCircuit("");
            var doc = c.Doc();
            var reg = c.Reg;
            // setValue → values: R entered through the Values UI path lands in the value text and drives the circuit.
            doc = Edits.Update(doc, new[] { "R1" }, o => ValueEdits.Set(reg, o, "R", "4k7", Lang.De));
            doc = Edits.Update(doc, new[] { "D1" }, o => ValueEdits.Set(reg, o, "U", "2,1 V", Lang.De));
            Assert.Equal("4k7", doc.Get("R1")!.Str("value", ""));
            Assert.Equal("2,1 V", doc.Get("D1")!.Str("uf", ""));
            var t = ValueSolver.Solve(doc, reg, new[] { "D1" });
            Assert.Equal((5 - 2.1) / 4700, V(t, "D1", "I"), 12);
            var back = DocumentJson.Deserialize(DocumentJson.Serialize(doc));
            var t2 = ValueSolver.Solve(back, reg, new[] { "D1" });
            Assert.Equal(V(t, "D1", "I"), V(t2, "D1", "I"), 15);
            Assert.Equal("2,1 V", t2.Get("D1", "U")!.Text);
            // Derived values are never stored.
            Assert.False(back.Get("D1")!.Params.ContainsKey("if"));
            Assert.False(back.Get("R1")!.Params.ContainsKey("current"));
            // Clearing gives the value back to the circuit.
            doc = Edits.Update(doc, new[] { "D1" }, o => ValueEdits.Clear(reg, o, "U"));
            Assert.Null(ValueSolver.Solve(doc, reg, null).Get("R1", "I")!.Value);
        }

        [Fact]
        public void EveryValueTypeInsertsWithoutParametersAndStaysQuiet()
        {
            var reg = Fx.Reg();
            var withValues = reg.Types.Where(ValueModel.HasValues).Select(t => t.Id).OrderBy(x => x).ToList();
            Assert.Equal(new[]
            {
                "elec.battery", "elec.breaker", "elec.capacitor", "elec.capacitor.polarized", "elec.diode", "elec.diode.led",
                "elec.diode.schottky", "elec.diode.zener", "elec.fuse", "elec.generator", "elec.house.cable", "elec.house.light", "elec.house.socket",
                "elec.inductor", "elec.lamp", "elec.motor", "elec.opamp", "elec.potentiometer", "elec.rcd", "elec.regulator", "elec.resistor", "elec.resistor.ldr", "elec.resistor.thermistor",
                "elec.sld.feeder", "elec.source", "elec.transformer", "elec.transistor.bjt",
            }, withValues);
            var c = new Circuit();
            foreach (var id in withValues) c.Part(id, id);
            var t = c.Solve();
            // Nothing is entered or derived: at most the defaults of a type (the transistor's U_BE and overdrive factor) show.
            foreach (var id in withValues)
                Assert.All(reg.Type(id) is { } ty ? ValueModel.Variables(ty) : Array.Empty<VarDef>(),
                    v => Assert.True(t.Get(id, v.Key)?.Value is null || t.Get(id, v.Key)!.Provenance == Provenance.Default, id + "." + v.Key));
            Assert.Empty(c.Checks());
            foreach (var id in withValues)
            {
                var type = reg.Type(id)!;
                var vars = ValueModel.Variables(type);
                Assert.True(vars.Count(v => v.Relevance == VarRelevance.Card) <= 4, id);
                // Every variable is bound to a declared, optional parameter with bilingual names.
                foreach (var v in vars)
                {
                    var p = type.Parameters.FirstOrDefault(x => x.Key == v.ParamKey);
                    Assert.True(p is not null, id + "." + v.Key);
                    Assert.Equal(ParamKind.Text, p!.Kind);
                    Assert.False(string.IsNullOrEmpty(v.Name.De));
                    Assert.NotEqual("", v.Name.En);
                }
                Assert.All(ValueModel.Relations(type), r => Assert.False(string.IsNullOrEmpty(r.Source)));
            }
        }

        [Fact]
        public void ChecksHaveBilingualMessagesAndSources()
        {
            var reg = Fx.Reg();
            var checks = reg.Types.OfType<IValueChecks>().SelectMany(t => t.ValueChecks).Distinct().ToList();
            Assert.Equal(new[] { "elec.ac.resonance", "elec.bjt.overdrive", "elec.cable.drop", "elec.coord.overload", "elec.led.current", "elec.net.reducible", "elec.opamp.saturation", "elec.r.power", "elec.rcd.additional" },
                checks.Select(c => c.Id).Distinct().OrderBy(x => x).ToArray());
            foreach (var c in checks)
            {
                Assert.False(string.IsNullOrWhiteSpace(c.Message.En));
                Assert.False(string.IsNullOrWhiteSpace(c.Message.De));
                Assert.NotEqual(c.Message.En, c.Message.De);
                Assert.DoesNotContain("patent", c.Message.En, StringComparison.OrdinalIgnoreCase);
                if (c.Id != "elec.net.reducible") Assert.False(string.IsNullOrEmpty(c.Source));
            }
        }

        [Fact]
        public void AnalysisIsDeterministic()
        {
            var a = LedCircuit().Solve();
            var b = LedCircuit().Solve();
            Assert.Equal(a.Of("R1").Select(v => (v.Value, v.Provenance, v.Via?.Relation.Id)), b.Of("R1").Select(v => (v.Value, v.Provenance, v.Via?.Relation.Id)));
        }

        [Fact]
        public void PersistenceCircuitIsSolvedFromTheValueTexts()
        {
            // The documented example circuit (battery 12 V, switch open, R1 470 Ω, LED, earth): open switch, no current.
            var (r, doc) = PersistenceTests.Circuit();
            var t = ValueSolver.Solve(doc, r, null);
            var bat = doc.Objects.Values.Single(o => o.Type == "elec.battery").Id;
            Assert.Equal(12, V(t, bat, "U"), 9);
            var res = doc.Objects.Values.Single(o => o.Type == "elec.resistor").Id;
            Assert.Null(t.Get(res, "I")!.Value);
            // Closing the switch and giving the LED its forward voltage closes the loop through the earth symbol.
            var closed = ValuedCircuit();
            var t2 = ValueSolver.Solve(closed.Doc, closed.R, null);
            var res2 = closed.Doc.Objects.Values.Single(o => o.Type == "elec.resistor").Id;
            Assert.Equal((12 - 2.1) / 470, V(t2, res2, "I"), 12);
        }

        /// <summary>The documented example circuit with the switch closed, the LED's forward voltage and values on the drawing.</summary>
        internal static (Registry R, SketchDocument Doc) ValuedCircuit()
        {
            var (r, doc) = PersistenceTests.Circuit();
            string Id(string type) => doc.Objects.Values.Single(o => o.Type == type).Id;
            doc = Edits.Update(doc, new[] { Id("elec.switch") }, o => o.WithParam("closed", true));
            doc = Edits.Update(doc, new[] { Id("elec.diode.led") }, o => o.WithParam("uf", "2.1 V").WithParam(ValueLabels.ShowParam, true));
            doc = Edits.Update(doc, new[] { Id("elec.resistor") }, o => o.WithParam(ValueLabels.ShowParam, true));
            return (r, doc);
        }
    }
}
