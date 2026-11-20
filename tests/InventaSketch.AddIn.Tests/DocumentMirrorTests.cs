using System.Collections.Generic;
using System.Linq;
using InventaSketch.AddIn.OnSlide;
using Xunit;

namespace InventaSketch.AddIn.Tests
{
    /// <summary>Document mirror for LibreOffice exchange: same names, chunking and bounds format as the LibreOffice extension.</summary>
    public class DocumentMirrorTests
    {
        [Fact]
        public void ParsesUnitNamesLikeLibreOffice()
        {
            Assert.Equal(("mech.gear.spur", "o3a50fce3934f"), DocumentMirror.ParseName("ISK:mech.gear.spur:o3a50fce3934f"));
            foreach (var name in new[] { null, "", "Rectangle 1", "ISK:", "ISK:typeonly", "ISK:t:bad id", "isk:t:id" })
                Assert.Null(DocumentMirror.ParseName(name));
        }

        [Fact]
        public void ChunksJoinBack()
        {
            var value = new string('a', 25) + new string('b', 25) + "c";
            var chunks = DocumentMirror.Chunks("ISK_MODEL_x", value, 25);
            Assert.Equal(new[] { "ISK_MODEL_x", "ISK_MODEL_x_1", "ISK_MODEL_x_2" }, chunks.Select(c => c.Name));
            var store = chunks.ToDictionary(c => c.Name, c => c.Value);
            Assert.Equal(value, DocumentMirror.Join(n => store.TryGetValue(n, out var v) ? v : null, "ISK_MODEL_x"));
            Assert.Null(DocumentMirror.Join(n => store.TryGetValue(n, out var v) ? v : null, "ISK_MODEL_y"));
            Assert.Single(DocumentMirror.Chunks("k", ""));
            Assert.Single(DocumentMirror.Chunks("k", new string('z', 25), 25));
        }

        [Fact]
        public void BoundsUseTheLibreOfficeFormat()
        {
            var json = DocumentMirror.BoundsJson(10.5f, 20f, 100.25f, 50f, 90);
            Assert.Equal("{\"x\":10.5,\"y\":20,\"w\":100.25,\"h\":50,\"r\":27000}", json);
            Assert.Equal(new[] { 10.5f, 20f, 100.25f, 50f }, DocumentMirror.ParseBounds(json));
            Assert.Null(DocumentMirror.ParseBounds("{\"x\":1}"));
            Assert.Null(DocumentMirror.ParseBounds(null));
        }

        [Fact]
        public void OrphansAreMirrorsOfVanishedUnits()
        {
            var names = new[] { "ISK_MODEL_a", "ISK_MODEL_a_1", "ISK_BOUNDS_a", "ISK_MODEL_b", "ISK_MODEL_b_1", "ISK_BOUNDS_b", "Other" };
            Assert.Equal(new[] { "ISK_MODEL_b", "ISK_MODEL_b_1", "ISK_BOUNDS_b" }, DocumentMirror.Orphans(names, new HashSet<string> { "a" }));
        }
    }
}
