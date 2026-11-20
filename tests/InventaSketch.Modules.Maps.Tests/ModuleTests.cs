using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Maps.Tests
{
    public class ModuleTests
    {
        [Fact]
        public void RegistersWithoutCollisions()
        {
            var r = new Registry().Register(new MapsModule());
            Assert.Contains(r.Modules, m => m.Id == "maps");
        }
    }
}
