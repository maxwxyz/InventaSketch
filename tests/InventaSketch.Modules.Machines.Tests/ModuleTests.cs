using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Machines.Tests
{
    public class ModuleRegistrationTests
    {
        [Fact]
        public void ModuleRegisters() => new Registry().Register(new MachinesModule());
    }
}
