using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Electrical.Tests
{
    /// <summary>Basic electronics parts added in round 1 (2026-11-26): resistor kinds, diodes, crystal, optocoupler, triac/diac, JFET, regulator, meter, solenoid.</summary>
    public class ElectronicsPartsTests
    {
        /// <summary>Library item id → type id and the expected pin ids.</summary>
        public static IEnumerable<object[]> Items() => new[]
        {
            new object[] { "elec.resistor.ntc", "elec.resistor.thermistor", new[] { "1", "2" } },
            new object[] { "elec.resistor.ptc", "elec.resistor.thermistor", new[] { "1", "2" } },
            new object[] { "elec.resistor.ldr", "elec.resistor.ldr", new[] { "1", "2" } },
            new object[] { "elec.resistor.varistor", "elec.resistor.varistor", new[] { "1", "2" } },
            new object[] { "elec.crystal", "elec.crystal", new[] { "1", "2" } },
            new object[] { "elec.schottky", "elec.diode.schottky", new[] { "A", "K" } },
            new object[] { "elec.tvs", "elec.diode.tvs", new[] { "A", "K" } },
            new object[] { "elec.tvs.bi", "elec.diode.tvs", new[] { "A", "K" } },
            new object[] { "elec.bridge", "elec.diode.bridge", new[] { "AC1", "AC2", "+", "-" } },
            new object[] { "elec.njfet", "elec.transistor.jfet", new[] { "G", "D", "S" } },
            new object[] { "elec.pjfet", "elec.transistor.jfet", new[] { "G", "D", "S" } },
            new object[] { "elec.triac", "elec.triac", new[] { "A1", "A2", "G" } },
            new object[] { "elec.diac", "elec.diac", new[] { "1", "2" } },
            new object[] { "elec.optocoupler", "elec.optocoupler", new[] { "A", "K", "C", "E" } },
            new object[] { "elec.regulator", "elec.regulator", new[] { "IN", "OUT", "GND" } },
            new object[] { "elec.meter.v", "elec.meter", new[] { "1", "2" } },
            new object[] { "elec.meter.a", "elec.meter", new[] { "1", "2" } },
            new object[] { "elec.meter.w", "elec.meter", new[] { "1", "2" } },
            new object[] { "elec.meter.ohm", "elec.meter", new[] { "1", "2" } },
            new object[] { "elec.solenoid", "elec.solenoid", new[] { "1", "2" } },
        };

        [Theory]
        [MemberData(nameof(Items))]
        public void ItemInsertsWithoutParametersRendersAndHasItsPorts(string itemId, string typeId, string[] pins)
        {
            var r = Fx.Reg();
            var item = r.Item(itemId);
            Assert.NotNull(item);
            Assert.Equal(typeId, item!.TypeId);
            var t = r.Type(typeId)!;
            Assert.False(string.IsNullOrEmpty(t.Name.De));
            Assert.False(string.IsNullOrEmpty(item.Name.De));

            // The bare type (no parameters at all) and the library item both render inside the box.
            var bare = new SketchObject { Id = "x", Type = typeId, Size = t.DefaultSize };
            Fx.AssertInsideBox(t.Render(bare, Fx.Ctx()), bare.Size, typeId);
            var o = r.Create(item, Vec2.Zero);
            Fx.AssertInsideBox(t.Render(o, Fx.Ctx()), o.Size, itemId);

            var ports = t.Anchors(o, Fx.Ctx()).Where(a => a.Role == AnchorRole.Port).Select(a => a.Id).ToArray();
            Assert.Equal(pins, ports);
            Assert.All(t.Anchors(o, Fx.Ctx()).Where(a => a.Role == AnchorRole.Port), a => Assert.NotNull(t.Connection(o, a)));
        }

        [Theory]
        [InlineData("thermistor")]
        [InlineData("heißleiter")]
        [InlineData("kaltleiter")]
        [InlineData("fotowiderstand")]
        [InlineData("varistor")]
        [InlineData("quarz")]
        [InlineData("schottky")]
        [InlineData("suppressordiode")]
        [InlineData("brückengleichrichter")]
        [InlineData("optokoppler")]
        [InlineData("triac")]
        [InlineData("diac")]
        [InlineData("jfet")]
        [InlineData("spannungsregler")]
        [InlineData("strommesser")]
        [InlineData("hubmagnet")]
        public void SearchFindsTheNewParts(string q) => Assert.NotEmpty(Fx.Reg().Search(q));

        [Fact]
        public void SolenoidIsNoLongerAnAliasOfTheActuatorBlock()
        {
            var r = Fx.Reg();
            Assert.DoesNotContain("hubmagnet", r.Type("elec.actuator")!.Keywords);
            Assert.Contains("hubmagnet", r.Type("elec.solenoid")!.Keywords);
            Assert.Equal("Aktor", r.Item("elec.actuator")!.Name.De);
        }

        [Fact]
        public void VariantsDrawDifferently()
        {
            var r = Fx.Reg();
            string Svg(string item)
            {
                var prims = Fx.Render(r, r.Create(r.Item(item)!, Vec2.Zero));
                return string.Join("|", Prim.Flatten(prims).SelectMany(Prim.Points).Select(pt => pt.ToString()))
                    + string.Join("|", prims.OfType<TextPrim>().Select(x => x.Text));
            }
            Assert.NotEqual(Svg("elec.resistor.ntc"), Svg("elec.resistor.ptc"));
            Assert.NotEqual(Svg("elec.tvs"), Svg("elec.tvs.bi"));
            Assert.NotEqual(Svg("elec.njfet"), Svg("elec.pjfet"));
            Assert.NotEqual(Svg("elec.schottky"), Svg("elec.diode"));
            var letters = new[] { "elec.meter.v", "elec.meter.a", "elec.meter.w", "elec.meter.ohm" }
                .Select(i => Fx.Render(r, r.Create(r.Item(i)!, Vec2.Zero)).OfType<TextPrim>().Single(p => p.Tag == "letter").Text).ToArray();
            Assert.Equal(new[] { "V", "A", "W", "Ω" }, letters);
            Assert.Contains(Fx.Render(r, r.Create(r.Item("elec.resistor.ntc")!, Vec2.Zero)), p => p is TextPrim { Text: "−ϑ" });
            Assert.Contains(Fx.Render(r, r.Create(r.Item("elec.resistor.ptc")!, Vec2.Zero)), p => p is TextPrim { Text: "+ϑ" });
        }

        [Fact]
        public void MeterQuantityIsAChoiceWithPictograms()
        {
            var r = Fx.Reg();
            var t = r.Type("elec.meter")!;
            var o = r.Create(r.Item("elec.meter.a")!, Vec2.Zero);
            var options = t.Actions(o, Lang.De).Where(a => a.Choice?.Key == "elec.meter.quantity").ToList();
            Assert.Equal(new[] { "elec.meter.quantity=v", "elec.meter.quantity=a", "elec.meter.quantity=w", "elec.meter.quantity=ohm" }, options.Select(a => a.Id));
            Assert.Equal("elec.meter.quantity=a", Assert.Single(options, a => a.IsActive).Id);
            Assert.All(options, a => Assert.NotNull(a.Preview));
            Assert.Contains(options[3].Preview!, p => p is TextPrim { Text: "Ω" });
            Assert.Equal("ohm", options[3].Apply(o).Str("quantity", ""));
            Assert.DoesNotContain(t.Actions(o, Lang.De), a => a.Icon == IconIds.Cycle);
        }

        [Theory]
        [InlineData("elec.resistor.thermistor", "elec.thermistor.kind", "coefficient", "ptc")]
        [InlineData("elec.diode.tvs", "elec.tvs.direction", "direction", "bi")]
        [InlineData("elec.transistor.jfet", "elec.jfet.channel", "channel", "p")]
        public void VariantChoicesAreDropdowns(string typeId, string choice, string key, string value)
        {
            var r = Fx.Reg();
            var t = r.Type(typeId)!;
            var o = t.Create(Vec2.Zero);
            var options = t.Actions(o, Lang.En).Where(a => a.Choice?.Key == choice).ToList();
            Assert.Equal(2, options.Count);
            Assert.Single(options, a => a.IsActive);
            Assert.Equal(value, options.Single(a => a.Id == choice + "=" + value).Apply(o).Str(key, ""));
        }

        [Fact]
        public void PinsFollowMirrorAndScaling()
        {
            // The optocoupler's four pins stay on the box edges for free scaling.
            var r = Fx.Reg();
            var t = r.Type("elec.optocoupler")!;
            var o = r.Create(r.Item("elec.optocoupler")!, Vec2.Zero) with { Size = new Size2(60, 10) };
            var pins = t.Anchors(o, Fx.Ctx()).Where(a => a.Role == AnchorRole.Port).ToDictionary(a => a.Id!, a => a.P);
            Assert.Equal(new Vec2(-30, -2.5), pins["A"]);
            Assert.Equal(new Vec2(30, 2.5), pins["E"]);
        }

        // ------------------------------------------------------------------ values

        private static double V(ValueTable t, string id, string key) => t.Get(id, key)!.Value ?? double.NaN;

        [Theory]
        [InlineData("elec.resistor.thermistor")]
        [InlineData("elec.resistor.ldr")]
        public void ResistorKindsCarryTheResistanceVariable(string type)
        {
            var t = new Circuit().Part("R1", type, ("value", "10k"), ("voltage", "5 V")).Solve();
            Assert.Equal(10000, V(t, "R1", "R"), 6);
            Assert.Equal(0.5e-3, V(t, "R1", "I"), 12);
        }

        [Fact]
        public void ThermistorInASeriesCircuitGetsTheCurrent()
        {
            var t = new Circuit()
                .Part("G1", "elec.source", ("value", "10 V"))
                .Part("R1", "elec.resistor", ("value", "1k"))
                .Part("R2", "elec.resistor.thermistor", ("value", "4k"))
                .Wire("G1", "+", "R1", "1").Wire("R1", "2", "R2", "1").Wire("R2", "2", "G1", "-")
                .Solve();
            Assert.Equal(8, V(t, "R2", "U"), 9);
            Assert.Equal(2e-3, V(t, "R1", "I"), 12);
        }

        [Fact]
        public void VaristorAndTvsInParallelCarryNoCurrent()
        {
            // Protective parts across the load do not conduct in normal operation: the load circuit stays reducible.
            var t = new Circuit()
                .Part("G1", "elec.source", ("value", "10 V"))
                .Part("R1", "elec.resistor", ("value", "1k"))
                .Part("RV1", "elec.resistor.varistor", ("value", "275 V"))
                .Part("D1", "elec.diode.tvs")
                .Wire("G1", "+", "R1", "1").Wire("R1", "2", "G1", "-")
                .Wire("G1", "+", "RV1", "1").Wire("RV1", "2", "G1", "-")
                .Wire("G1", "+", "D1", "K").Wire("D1", "A", "G1", "-")
                .Solve();
            Assert.Equal(10e-3, V(t, "R1", "I"), 12);
            Assert.Equal(10e-3, V(t, "G1", "I"), 12);
            Assert.False(ValueModel.HasValues(Fx.Reg().Type("elec.resistor.varistor")));
        }

        [Fact]
        public void IdealAmmeterInSeriesAndVoltmeterInParallelDoNotDisturbTheCircuit()
        {
            var t = new Circuit()
                .Part("G1", "elec.source", ("value", "10 V"))
                .Part("P1", "elec.meter", ("quantity", "a"))
                .Part("R1", "elec.resistor", ("value", "1k"))
                .Part("P2", "elec.meter", ("quantity", "v"))
                .Wire("G1", "+", "P1", "1").Wire("P1", "2", "R1", "1").Wire("R1", "2", "G1", "-")
                .Wire("R1", "1", "P2", "1").Wire("P2", "2", "R1", "2")
                .Solve();
            Assert.Equal(10e-3, V(t, "R1", "I"), 12);
            Assert.Equal(10, V(t, "R1", "U"), 9);
        }

        [Fact]
        public void RegulatorPowerLossFollowsFromItsVoltagesAndCurrent()
        {
            // The voltage in the value text is the output voltage (marking); U_in and I_out entered give the power loss.
            var t = new Circuit().Part("U1", "elec.regulator", ("value", "LM7805 5 V"), ("uin", "12 V"), ("current", "1 A")).Solve();
            Assert.Equal(5, V(t, "U1", "Uout"), 9);
            Assert.Equal(7, V(t, "U1", "P"), 9);

            var back = new Circuit().Part("U1", "elec.regulator", ("uout", "3.3 V"), ("uin", "5 V"), ("power", "0.85 W")).Solve();
            Assert.Equal(0.5, V(back, "U1", "I"), 9);
            // A part number alone gives nothing and no error.
            var none = new Circuit().Part("U1", "elec.regulator", ("value", "LM317")).Solve();
            Assert.Null(none.Get("U1", "Uout")?.Value);
        }

        // ------------------------------------------------------------------ exports

        private static string Export(string id, SketchDocument doc, Registry r)
        {
            var exporter = new ElectricalModule().Exporters.Single(e => e.Id == id);
            var objects = doc.ObjectsOn(doc.Pages[0]).ToList();
            Assert.True(exporter.Applies(r, doc, objects));
            return exporter.Export(r, doc, objects, Lang.En);
        }

        [Fact]
        public void NetlistListsThePinsOfTheNewMultiPinParts()
        {
            var c = new Circuit()
                .Part("BR", "elec.diode.bridge", ("ref", "D1"))
                .Part("REG", "elec.regulator", ("ref", "U1"))
                .Part("OK", "elec.optocoupler", ("ref", "U2"))
                .Part("Y", "elec.crystal", ("ref", "Y1"))
                .Part("M", "elec.meter", ("ref", "P1"), ("quantity", "a"))
                .Wire("BR", "+", "REG", "IN")
                .Wire("BR", "-", "REG", "GND")
                .Wire("REG", "OUT", "OK", "A")
                .Wire("OK", "K", "Y", "1")
                .Wire("Y", "2", "M", "1");
            var csv = Export("elec.netlist", c.Doc(), c.Reg);
            Assert.Contains("D1.+ U1.IN", csv);
            Assert.Contains("D1.- U1.GND", csv);
            Assert.Contains("U1.OUT U2.A", csv);
            Assert.Contains("U2.K Y1.1", csv);
            Assert.Contains("P1.1 Y1.2", csv);
        }

        [Fact]
        public void RuleCheckReportsTheFreePinsOfTheNewParts()
        {
            var r = Fx.Reg();
            var ok = r.Create(r.Item("elec.optocoupler")!, new Vec2(50, 50)).WithParam("ref", "U2");
            var triac = r.Create(r.Item("elec.triac")!, new Vec2(100, 50)).WithParam("ref", "Q1");
            var (doc, pageId) = Fx.Doc(ok, triac);
            var from = Fx.Port(r, doc, ok, "C").P;
            var to = Fx.Port(r, doc, triac, "A1").P;
            var wire = r.Create(r.Item("elec.wire")!, Vec2.Zero) with { Points = new[] { from, to } };
            doc = Edits.Add(doc, pageId, wire);
            doc = ElectricalModule.ConnectToPin(r, doc, wire.Id, 0);
            doc = ElectricalModule.ConnectToPin(r, doc, wire.Id, 1);
            var csv = Export("elec.erc", doc, r);
            foreach (var pin in new[] { "U2,A", "U2,K", "U2,E", "Q1,A2", "Q1,G" }) Assert.Contains("pin.unconnected," + pin + ",", csv);
            Assert.DoesNotContain("U2,C,", csv);
            Assert.DoesNotContain("Q1,A1,", csv);
        }

        [Fact]
        public void NewPartsRoundTripThroughJson()
        {
            var r = Fx.Reg();
            var items = Items().Select(x => (string)x[0]).ToList();
            var objects = items.Select((id, i) => r.Create(r.Item(id)!, new Vec2(20 + 30 * (i % 6), 20 + 25 * (i / 6))).WithParam("ref", "X" + i)).ToArray();
            var (doc, _) = Fx.Doc(objects);
            var back = DocumentJson.Deserialize(DocumentJson.Serialize(doc));
            Assert.Equal(DocumentJson.Serialize(doc), DocumentJson.Serialize(back));
            Assert.Equal(SvgRenderer.RenderPage(r, doc, doc.Pages[0]), SvgRenderer.RenderPage(r, back, back.Pages[0]));
        }
    }
}
