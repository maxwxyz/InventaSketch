using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Core.Tests
{
    /// <summary>
    /// Media that meet across modules (<see cref="PortMedia.Equivalents"/>) and the physical key vocabulary of the
    /// cross-module value join (<see cref="NetKeys"/>).
    /// </summary>
    public class CrossModuleMediaTests
    {
        [Theory]
        // Energy heat port and building heating pipes.
        [InlineData(PortMedia.Heat, PortMedia.HeatingFlow)]
        [InlineData(PortMedia.Heat, PortMedia.HeatingReturn)]
        [InlineData(PortMedia.Heat, PortMedia.Heating)]
        [InlineData(PortMedia.Thermal, PortMedia.HeatingFlow)]
        // A process port and water, air, gas, refrigerant, vacuum of other modules.
        [InlineData(PortMedia.Process, PortMedia.ColdWater)]
        [InlineData(PortMedia.Process, PortMedia.Water)]
        [InlineData(PortMedia.ProcessWater, PortMedia.HotWater)]
        [InlineData(PortMedia.Process, "air.supply")]
        [InlineData(PortMedia.ProcessGas, PortMedia.Gas)]
        [InlineData(PortMedia.ProcessHydrogen, PortMedia.Hydrogen)]
        [InlineData(PortMedia.Process, PortMedia.Refrigerant)]
        [InlineData(PortMedia.Process, PortMedia.Vacuum)]
        // Compressed air and pneumatics.
        [InlineData(PortMedia.Fluid, PortMedia.AirCompressed)]
        [InlineData(PortMedia.Air, PortMedia.AirCompressed)]
        public void SameMediumNamedByTwoModulesFits(string a, string b)
        {
            Assert.True(PortMedia.Fits(a, b));
            Assert.True(PortMedia.Fits(b, a));
        }

        [Theory]
        [InlineData(PortMedia.HeatingFlow, PortMedia.HeatingReturn)]
        [InlineData(PortMedia.Cooling, PortMedia.HeatingFlow)]
        [InlineData(PortMedia.Cooling, PortMedia.Heating)]
        [InlineData(PortMedia.Gas, PortMedia.Hydrogen)]
        [InlineData(PortMedia.Water, PortMedia.Air)]
        [InlineData(PortMedia.Fluid, PortMedia.Water)]
        [InlineData(PortMedia.Fluid, PortMedia.Process)]
        [InlineData("air.supply", PortMedia.AirCompressed)]
        [InlineData(PortMedia.Data, PortMedia.Optical)]
        [InlineData(PortMedia.Electric, PortMedia.MechShaft)]
        [InlineData(PortMedia.Refrigerant, PortMedia.Water)]
        public void DistinctMediaStayApart(string a, string b)
        {
            Assert.False(PortMedia.Fits(a, b));
            Assert.False(PortMedia.Fits(b, a));
        }

        [Fact]
        public void CoversStaysAPurePrefixTest()
        {
            // Roles and colours ask "is this the general form": water is no process medium for them.
            Assert.False(PortMedia.Covers(PortMedia.Process, PortMedia.ColdWater));
            Assert.False(PortMedia.Covers(PortMedia.Heat, PortMedia.HeatingFlow));
            Assert.True(PortMedia.Covers(PortMedia.Heating, PortMedia.HeatingFlow));
            Assert.Empty(LineMedia.RolesFor(PortMedia.ColdWater));
        }

        [Fact]
        public void FormsReadAMediumThroughItsEquivalents()
        {
            Assert.Equal(new[] { "heating.flow", "thermal.heat.flow" }, PortMedia.Forms(PortMedia.HeatingFlow).ToArray());
            Assert.Equal(new[] { PortMedia.Electric }, PortMedia.Forms(PortMedia.Electric).ToArray());
        }

        [Fact]
        public void NewMediaHaveColoursByMedium()
        {
            foreach (var m in new[] { PortMedia.AirCompressed, PortMedia.Refrigerant, PortMedia.Vacuum, PortMedia.Optical })
            {
                var role = LineMedia.DerivedRole(m);
                Assert.NotNull(LineMedia.Entry(LineMedia.ColourId(m, role)));
            }
            Assert.Equal("DIN 2403", LineMedia.Entry("air.compressed")!.Source.En);
        }

        [Theory]
        [InlineData("U", "U")]
        [InlineData("Uac", "U")]
        [InlineData("Ustr", "U")]
        [InlineData("Pac", "P")]
        [InlineData("Pdc", "P")]
        [InlineData("Q", "Vdot")]
        [InlineData("Vdot", "Vdot")]
        [InlineData("theta", "theta")]
        [InlineData("p", "p")]
        [InlineData("n", "n")]
        public void ClassesMapToPhysicalKeys(string cls, string key) => Assert.Equal(key, NetKeys.ForClass(cls));

        [Theory]
        [InlineData("UocMax")]
        [InlineData("UmppMin")]
        [InlineData("Isc")]
        [InlineData("pmax")]
        public void LimitsAreNotJoined(string cls) => Assert.Null(NetKeys.ForClass(cls));

        [Fact]
        public void EveryKeyHasAQuantityAndVariableNames()
        {
            foreach (var key in NetKeys.All)
            {
                Assert.NotNull(NetKeys.KindOf(key));
                Assert.NotEmpty(NetKeys.VariableNames(key));
                Assert.Equal(key, NetKeys.ForClass(key));
            }
            Assert.Equal(Quantities.VolumeFlow.Id, NetKeys.KindOf(NetKeys.VolumeFlow)!.Id);
            // A motor joins with its electrical input power.
            Assert.Equal("Pel", NetKeys.VariableNames(NetKeys.Power)[0]);
        }

        [Fact]
        public void ASpecMayDeclareItsOwnKeys()
        {
            var spec = new NetSpec { Prefix = "x", Keys = c => c == "flow" ? NetKeys.VolumeFlow : c == "bogus" ? "nokey" : null };
            Assert.Equal("Vdot", NetKeys.Of(spec, "flow"));
            Assert.Null(NetKeys.Of(spec, "U"));
            Assert.Null(NetKeys.Of(spec, "bogus"));
            Assert.Equal("U", NetKeys.Of(new NetSpec { Prefix = "y" }, "Uac"));
        }
    }
}
