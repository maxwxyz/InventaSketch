using System.Linq;
using System.Text.Json;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Engine.Tests
{
    /// <summary>Build "+" ports through the engine API (web and LibreOffice hosts).</summary>
    public class BuildApiTests
    {
        [Fact]
        public void PortsItemsAndPlace()
        {
            var api = new EngineApi(ModuleCatalog.CreateDefault());
            var session = EngineApiTests.Call(api, "createSession", new { lang = "en" }).GetProperty("session").GetString()!;
            var gearItem = api.Registry.Library.First(i => i.TypeId == "mech.gear.spur" && i.View == "axial").Id;
            var gear = EngineApiTests.Insert(api, session, gearItem, 100, 100);
            var ports = EngineApiTests.Call(api, "buildPorts", new { session, id = gear }).EnumerateArray().ToList();
            Assert.NotEmpty(ports);
            var mesh = ports.First(p => p.GetProperty("kind").GetString() == "Mesh").GetProperty("id").GetString();
            var items = EngineApiTests.Call(api, "buildItems", new { session, id = gear, key = mesh }).EnumerateArray().ToList();
            Assert.NotEmpty(items);
            var st = EngineApiTests.Call(api, "buildPlace", new { session, id = gear, key = mesh, itemId = items[0].GetProperty("id").GetString() });
            Assert.Equal(2, st.GetProperty("objects").GetArrayLength());
            // arrangeOptions offers a dimension between the two gears.
            var ids = st.GetProperty("objects").EnumerateArray().Select(o => o.GetProperty("id").GetString()).ToArray();
            var opts = EngineApiTests.Call(api, "arrangeOptions", new { session, ids }).EnumerateArray().Select(o => o.GetProperty("id").GetString()).ToList();
            Assert.Contains("dim", opts);
            var withDim = EngineApiTests.Call(api, "arrange", new { session, ids, action = "dim" });
            Assert.Equal(3, withDim.GetProperty("objects").GetArrayLength());
            var again = EngineApiTests.Call(api, "arrangeOptions", new { session, ids }).EnumerateArray().Select(o => o.GetProperty("id").GetString()).ToList();
            Assert.DoesNotContain("dim", again);
            // A gear port is no shaft end: nothing to stretch.
            Assert.All(EngineApiTests.Call(api, "buildPorts", new { session, id = gear }).EnumerateArray(), p => Assert.False(p.GetProperty("stretch").GetBoolean()));
        }
    }
}
