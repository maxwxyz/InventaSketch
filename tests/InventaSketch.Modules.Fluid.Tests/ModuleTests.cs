using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Fluid.Tests
{
    public class ModuleTests
    {
        [Fact]
        public void RegistersWithoutCollisions()
        {
            var r = new Registry().Register(new FluidModule());
            Assert.Contains(r.Modules, m => m.Id == "fluid");
        }
    }
}
