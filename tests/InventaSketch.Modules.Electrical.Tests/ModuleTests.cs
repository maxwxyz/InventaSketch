using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Electrical.Tests
{
    public class ModuleRegistrationTests
    {
        [Fact]
        public void ModuleRegisters() => new Registry().Register(new ElectricalModule());
    }
}
