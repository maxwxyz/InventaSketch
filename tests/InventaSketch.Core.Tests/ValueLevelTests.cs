using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Core.Tests
{
    /// <summary>Calc 2: the value level is a document setting; Quick sketch is the default and is not written.</summary>
    public class ValueLevelTests
    {
        [Fact]
        public void QuickSketchIsTheDefaultAndOldDocumentsStayUnchanged()
        {
            var doc = SketchDocument.Create();
            Assert.Null(doc.Settings.ValueLevel);
            Assert.Equal(ValueLevel.QuickSketch, doc.Settings.EffectiveValueLevel);
            var json = DocumentJson.Serialize(doc);
            Assert.DoesNotContain("valueLevel", json);
            // A document written before the setting existed reads as Quick sketch.
            var back = DocumentJson.Deserialize(json);
            Assert.Equal(ValueLevel.QuickSketch, back.Settings.EffectiveValueLevel);
            Assert.Equal(json, DocumentJson.Serialize(back));
        }

        [Theory]
        [InlineData(ValueLevel.Explore, "explore")]
        [InlineData(ValueLevel.Engineer, "engineer")]
        public void LevelRoundTripsThroughJson(ValueLevel level, string written)
        {
            var doc = SketchDocument.Create();
            doc = doc with { Settings = doc.Settings.WithValueLevel(level) };
            var json = DocumentJson.Serialize(doc);
            Assert.Contains("\"valueLevel\":\"" + written + "\"", json.Replace(" ", ""));
            var back = DocumentJson.Deserialize(json);
            Assert.Equal(level, back.Settings.EffectiveValueLevel);
            Assert.Equal(json, DocumentJson.Serialize(back));
        }

        [Fact]
        public void QuickSketchIsStoredAsNotSet()
        {
            var s = new DocumentSettings().WithValueLevel(ValueLevel.Engineer).WithValueLevel(ValueLevel.QuickSketch);
            Assert.Null(s.ValueLevel);
        }
    }
}
