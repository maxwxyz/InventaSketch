using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Electrical.Tests
{
    /// <summary>Designator and value can be typed directly from the pie menu (input actions) and show the label.</summary>
    public class InputActionTests
    {
        [Fact]
        public void DesignatorAndValueAreInputActions()
        {
            var r = new Registry().Register(new ElectricalModule());
            var o = r.Create(r.Library.First(i => i.TypeId == "elec.resistor"), new Vec2(50, 50));
            var actions = r.Type("elec.resistor")!.Actions(o, Lang.De).ToList();
            var refAction = actions.Single(a => a.Id == "elec.ref.set");
            var valueAction = actions.Single(a => a.Id == "elec.value.set");
            Assert.Equal("Kennzeichen …", refAction.Label.Get(Lang.De));
            Assert.NotNull(refAction.Input);
            var named = refAction.Input!.Apply(o, " R12 ");
            Assert.Equal("R12", named.Str("ref", ""));
            Assert.True(named.Flag("showLabel", false));
            Assert.Equal("4,7k", valueAction.Input!.Apply(named, "4,7k").Str("value", ""));
        }
    }
}
