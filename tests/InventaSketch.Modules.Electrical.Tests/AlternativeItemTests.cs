using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Electrical.Tests
{
    /// <summary>
    /// The house installation symbols are the canonical room devices (the switchgear module's room devices give way to
    /// them), so they also answer to the switchgear names.
    /// </summary>
    public class AlternativeItemTests
    {
        [Theory]
        [InlineData("Zweifachsteckdose", Lang.De, "elec.house.socket.double")]
        [InlineData("light switch", Lang.En, "elec.house.switch")]
        [InlineData("Lichtschalter", Lang.De, "elec.house.switch")]
        [InlineData("Wechsel-Lichtschalter", Lang.De, "elec.house.switch.twoway")]
        [InlineData("ceiling light outlet", Lang.En, "elec.house.light")]
        [InlineData("Deckenauslass", Lang.De, "elec.house.light")]
        public void TheHouseSymbolsAnswerToTheSwitchgearNames(string query, Lang lang, string id)
        {
            var r = Fx.Reg();
            Assert.Contains(LibrarySearch.Find(r, query, lang, max: 50), i => i.Id == id);
            Assert.Null(r.Item(id)!.Alternative);
        }
    }
}
