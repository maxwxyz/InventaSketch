using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Mechanical.Tests
{
    public class ModuleRegistrationTests
    {
        [Fact]
        public void ModuleRegisters() => new Registry().Register(new MechanicalModule());
    }
}
