using System;
using System.Linq;
using System.Text.Json;
using InventaSketch.Core;
using InventaSketch.Engine;
using Xunit;

namespace InventaSketch.Engine.Tests
{
    /// <summary>Calc 6 check settings and unit preferences: muteCheck, setUnitPreference, and both survive save and reload.</summary>
    public class CheckSettingsApiTests
    {
        private static JsonElement Call(EngineApi api, string method, object args) => EngineApiTests.Call(api, method, args);

        private static readonly Lazy<EngineApi> Api = new(() => new EngineApi(ModuleCatalog.CreateDefault()));

        [Fact]
        public void AMutedCheckStaysMutedAfterSaveAndReload()
        {
            var api = Api.Value;
            var session = Call(api, "createSession", new { lang = "en" }).GetProperty("session").GetString()!;
            var id = EngineApiTests.Insert(api, session, "elec.resistor", 50, 50);
            // 230 V across 100 Ω: 529 W, far above a 0.25 W rating.
            Call(api, "setParam", new { session, ids = new[] { id }, key = "prated", value = "0.25 W" });
            Call(api, "setValue", new { session, id, key = "U", text = "230 V" });
            Call(api, "setValue", new { session, id, key = "R", text = "100" });
            static string[] Ids(JsonElement e) => e.EnumerateArray().Select(c => c.GetProperty("id").GetString()!).ToArray();
            Assert.Contains("elec.r.power", Ids(Call(api, "checks", new { session })));

            var muted = Call(api, "muteCheck", new { session, key = "elec.r.power" });
            Assert.Equal(new[] { "elec.r.power" }, muted.GetProperty("muted").EnumerateArray().Select(x => x.GetString()).ToArray());
            Assert.DoesNotContain("elec.r.power", Ids(Call(api, "checks", new { session })));

            // Save and reload: the check stays muted.
            var json = Call(api, "saveDocument", new { session }).GetProperty("json").GetString()!;
            var session2 = Call(api, "openDocument", new { json, lang = "en" }).GetProperty("session").GetString()!;
            Assert.DoesNotContain("elec.r.power", Ids(Call(api, "checks", new { session = session2 })));
            // Unmuting brings it back.
            Call(api, "muteCheck", new { session = session2, key = "elec.r.power", text = "off" });
            Assert.Contains("elec.r.power", Ids(Call(api, "checks", new { session = session2 })));
        }

        [Fact]
        public void AUnitPreferenceChangesHowValuesAreShown()
        {
            var api = Api.Value;
            var session = Call(api, "createSession", new { lang = "en" }).GetProperty("session").GetString()!;
            var id = EngineApiTests.Insert(api, session, "elec.resistor", 50, 50);
            Call(api, "setValue", new { session, id, key = "U", text = "230 V" });
            Call(api, "setValue", new { session, id, key = "I", text = "2 A" });
            JsonElement P() => Call(api, "values", new { session, id }).GetProperty("objects").EnumerateArray().Single()
                .GetProperty("vars").EnumerateArray().Single(v => v.GetProperty("key").GetString() == "P");
            Assert.Equal("460 W", P().GetProperty("display").GetString());
            var pref = Call(api, "setUnitPreference", new { session, quantityKind = "power", unit = "kW" });
            Assert.Equal("kW", pref.GetProperty("unitPreferences").GetProperty("power").GetString());
            Assert.Equal("0.46 kW", P().GetProperty("display").GetString());
            var json = Call(api, "saveDocument", new { session }).GetProperty("json").GetString()!;
            var session2 = Call(api, "openDocument", new { json, lang = "en" }).GetProperty("session").GetString()!;
            var p2 = Call(api, "values", new { session = session2, id }).GetProperty("objects").EnumerateArray().Single()
                .GetProperty("vars").EnumerateArray().Single(v => v.GetProperty("key").GetString() == "P");
            Assert.Equal("0.46 kW", p2.GetProperty("display").GetString());
            // A unit of another kind is refused; an empty unit clears the preference.
            Assert.Throws<ArgumentException>(() => ThrowIfError(api.Invoke("setUnitPreference", JsonSerializer.Serialize(new { session, quantityKind = "power", unit = "bar" }))));
            Call(api, "setUnitPreference", new { session, quantityKind = "power", unit = "" });
            Assert.Equal("460 W", P().GetProperty("display").GetString());
        }

        /// <summary>
        /// Sources of linked charts name the charts as dependents but render on their own: hosts that keep objects in
        /// separate units (PowerPoint) must not redraw them on every move (2026-11-26 regression: moved gears and PV strings
        /// were replaced, so references to their shapes broke).
        /// </summary>
        [Theory]
        [InlineData("mech.spur.axial")]
        [InlineData("energy.pv.string.10")]
        [InlineData("elec.resistor")]
        [InlineData("optics.lens.biconvex")]
        public void ChartSourcesHaveDependentsButRenderOnTheirOwn(string itemId)
        {
            var registry = ModuleCatalog.CreateDefault();
            var item = registry.Item(itemId)!;
            var type = registry.Type(item.TypeId)!;
            Assert.True(type.HasDependents, itemId);
            Assert.False(type.DependsOnNeighbours, itemId);
        }

        private static void ThrowIfError(string json)
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("error", out var e)) throw new ArgumentException(e.ToString());
        }
    }
}
