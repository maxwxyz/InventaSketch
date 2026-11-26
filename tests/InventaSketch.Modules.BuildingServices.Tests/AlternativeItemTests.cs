using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.BuildingServices.Tests
{
    /// <summary>
    /// The appliance outlet is the canonical one (the switchgear module's appliance connections give way to it), so it also
    /// answers to their names.
    /// </summary>
    public class AlternativeItemTests
    {
        [Theory]
        [InlineData("cooker connection", Lang.En, "bs.elec.appliance")]
        [InlineData("Herdanschluss", Lang.De, "bs.elec.appliance")]
        [InlineData("washing machine connection", Lang.En, "bs.elec.appliance.washer")]
        [InlineData("Waschmaschinenanschluss", Lang.De, "bs.elec.appliance.washer")]
        public void TheApplianceOutletAnswersToTheSwitchgearNames(string query, Lang lang, string id)
        {
            var r = Fx.Reg();
            Assert.Contains(LibrarySearch.Find(r, query, lang, max: 50), i => i.Id == id);
            Assert.Null(r.Item(id)!.Alternative);
        }
    }
}
