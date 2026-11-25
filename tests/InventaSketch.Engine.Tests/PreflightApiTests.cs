using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Engine.Tests
{
    /// <summary>Patent workflow in the engine: <c>patentProfiles</c> and <c>preflight</c>.</summary>
    public class PreflightApiTests
    {
        [Fact]
        public void PreflightReportsFindingsWithElementAndSource()
        {
            var api = new EngineApi(ModuleCatalog.CreateDefault());
            var session = EngineApiTests.Call(api, "createSession", new { lang = "de" }).GetProperty("session").GetString()!;
            Assert.Contains(EngineApiTests.Call(api, "patentProfiles", new { session }).EnumerateArray(), p => p.GetProperty("id").GetString() == "epo");
            // A coloured element on a 16:9 slide: colour and sheet size do not suit a patent drawing.
            var item = api.Registry.Library.First(i => i.TypeId == "energy.storage.battery").Id;
            var id = EngineApiTests.Insert(api, session, item, 100, 80);
            var result = EngineApiTests.Call(api, "preflight", new { session, text = "epo" });
            Assert.Equal("epo", result.GetProperty("profile").GetString());
            Assert.Contains("Patentanwalt", result.GetProperty("disclaimer").GetString());
            var findings = result.GetProperty("findings").EnumerateArray().ToList();
            Assert.NotEmpty(findings);
            Assert.All(findings, f => Assert.False(string.IsNullOrEmpty(f.GetProperty("message").GetString())));
            Assert.Contains(findings, f => f.TryGetProperty("id", out var x) && x.GetString() == id); // document-level findings have no id
            Assert.Contains(findings, f => f.GetProperty("severity").GetString() is "error" or "warning");
            Assert.All(findings.Where(f => f.GetProperty("severity").GetString() != "manual"), f => Assert.True(f.TryGetProperty("source", out var src) && src.GetString()!.Length > 0));
            Assert.ThrowsAny<System.Exception>(() => EngineApiTests.Call(api, "preflight", new { session, text = "moon-office" }));
        }
    }
}
