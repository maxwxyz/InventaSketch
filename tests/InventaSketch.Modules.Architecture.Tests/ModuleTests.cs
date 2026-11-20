using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Architecture.Tests
{
    public class ModuleRegistrationTests
    {
        [Fact]
        public void ModuleRegisters() => new Registry().Register(new ArchitectureModule());
    }
}
