using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Mechanical.Tests
{
    /// <summary>Drivetrain ("Antriebsstrang") keywords and "Insert ▸" companions of shafts, differential and clutch.</summary>
    public class DrivetrainTests
    {
        [Theory]
        [InlineData("antriebsstrang")]
        [InlineData("drivetrain")]
        [InlineData("powertrain")]
        public void DrivetrainSearchFindsShaftsGearsDifferentialAndClutches(string query)
        {
            var found = Fx.Reg().Search(query).Select(i => i.TypeId).ToHashSet();
            foreach (var id in new[] { "mech.shaft", "mech.gear.spur", "mech.gearset.differential", "mech.clutch.disc", "mech.clutch.multidisc", "mech.joint.universal" })
                Assert.Contains(id, found);
        }

        [Fact]
        public void ShaftOffersGearBearingEMachineWheelClutchAndCouplingFirst()
        {
            var shaft = Fx.Reg().Type("mech.shaft")!;
            Assert.Equal(new[]
            {
                "mech.gear.spur", "mech.bearing.ball", "machines.drivetrain.emachine", "machines.drive.wheel", "mech.clutch.disc", "mech.coupling.flexible",
            }, shaft.Companions.Take(6));
        }

        [Fact]
        public void OwnCompanionsAreRegisteredAndForeignOnesAreMachinesTypes()
        {
            var r = Fx.Reg();
            foreach (var t in r.Types)
                foreach (var c in t.Companions)
                    Assert.True(r.Type(c) is not null || c.StartsWith("machines.", System.StringComparison.Ordinal), $"{t.Id}: {c}");
            Assert.Contains("machines.drive.wheel", r.Type("mech.gearset.differential")!.Companions);
            Assert.Contains("machines.drivetrain.engine", r.Type("mech.clutch.disc")!.Companions);
        }
    }
}
