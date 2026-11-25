using System;
using System.Linq;
using Xunit;

namespace InventaSketch.Engine.Tests
{
    /// <summary>
    /// The value level and the "has values" flag for hosts without their own document setting (web add-in, Calc 2 parity):
    /// <c>setValueLevel</c>, <c>state.valueLevel</c>, <c>objects[].hasValues</c> of selected objects.
    /// </summary>
    public class ValueLevelApiTests
    {
        [Fact]
        public void TheLevelIsADocumentSettingWithOneUndoStep()
        {
            var (api, session) = EngineApiTests.NewSession();
            var st = EngineApiTests.Call(api, "state", new { session, includeSvg = false });
            Assert.Equal("quick", st.GetProperty("valueLevel").GetString());

            var revision = api.Sessions.Get(session).Revision;
            st = EngineApiTests.Call(api, "setValueLevel", new { session, text = "explore" });
            Assert.Equal("explore", st.GetProperty("valueLevel").GetString());
            Assert.Equal(revision + 1, api.Sessions.Get(session).Revision);
            Assert.Equal(Core.ValueLevel.Explore, api.Sessions.Get(session).Doc.Settings.ValueLevel);

            // The same level again is no undo step.
            EngineApiTests.Call(api, "setValueLevel", new { session, text = "Explore" });
            Assert.Equal(revision + 1, api.Sessions.Get(session).Revision);

            st = EngineApiTests.Call(api, "setValueLevel", new { session, text = "engineer" });
            Assert.Equal("engineer", st.GetProperty("valueLevel").GetString());
            st = EngineApiTests.Call(api, "undo", new { session });
            Assert.Equal("explore", st.GetProperty("valueLevel").GetString());

            // Quick sketch is stored as "not set", so old documents stay unchanged.
            st = EngineApiTests.Call(api, "setValueLevel", new { session, text = "quick" });
            Assert.Equal("quick", st.GetProperty("valueLevel").GetString());
            Assert.Null(api.Sessions.Get(session).Doc.Settings.ValueLevel);

            Assert.Throws<ArgumentException>(() => EngineApiTests.Call(api, "setValueLevel", new { session, text = "expert" }));
        }

        private static readonly Lazy<EngineApi> Full = new(() => new EngineApi(ModuleCatalog.CreateDefault()));

        [Fact]
        public void SelectedElementsSayWhetherTheyHaveValues()
        {
            var api = Full.Value;
            var session = EngineApiTests.Call(api, "createSession", new { lang = "en" }).GetProperty("session").GetString()!;
            var resistor = EngineApiTests.Insert(api, session, "elec.resistor", 40, 40);
            var other = EngineApiTests.Insert(api, session, api.Registry.Library.First(i => !Core.ValueModel.HasValues(api.Registry.Type(i.TypeId))).Id, 120, 40);

            bool HasValues(string id)
            {
                var st = EngineApiTests.Call(api, "select", new { session, ids = new[] { id } });
                return st.GetProperty("objects").EnumerateArray().Single(o => o.GetProperty("id").GetString() == id).GetProperty("hasValues").GetBoolean();
            }

            Assert.True(HasValues(resistor));
            Assert.False(HasValues(other));
            // Objects that are not selected carry no flag (it is evaluated for the selection only).
            var all = EngineApiTests.Call(api, "select", new { session, ids = new[] { other } });
            Assert.False(all.GetProperty("objects").EnumerateArray().Single(o => o.GetProperty("id").GetString() == resistor).GetProperty("hasValues").GetBoolean());
        }
    }
}
