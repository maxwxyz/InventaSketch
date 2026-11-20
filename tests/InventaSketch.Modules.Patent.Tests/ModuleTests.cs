using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Patent.Tests
{
    public class ModuleRegistrationTests
    {
        [Fact]
        public void ModuleRegisters() => new Registry().Register(new PatentModule());
    }
}
