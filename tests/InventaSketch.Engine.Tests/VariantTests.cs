using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Engine.Tests
{
    /// <summary>Variants and states: comparing two states of a drawing (<see cref="Variants"/>, engine <c>diff</c>).</summary>
    public class VariantTests
    {
        private static readonly Registry Reg = ModuleCatalog.CreateDefault();

        [Fact]
        public void TheDiffFindsAddedRemovedAndChangedElements()
        {
            var item = Reg.Library.First(i => i.TypeId == "mech.gear.spur" && i.View == "axial");
            var a = Reg.Create(item, new Vec2(40, 40)); var b = Reg.Create(item, new Vec2(90, 40)); var c = Reg.Create(item, new Vec2(140, 40));
            var before = SketchDocument.Create();
            before = Edits.Add(before, before.Pages[0].Id, a, b);
            // After: a has more teeth, b is gone, c is new.
            var after = SketchDocument.Create();
            after = Edits.Add(after, after.Pages[0].Id, a.WithParam("teeth", 40.0), c);
            var d = Variants.Diff(before, after);
            Assert.Equal(new[] { c.Id }, d.Added);
            Assert.Equal(new[] { b.Id }, d.Removed);
            Assert.Equal(new[] { a.Id }, d.Changed);
            Assert.True(Variants.Diff(before, before).None);
            // A moved element counts as changed, an untouched copy does not.
            var moved = Edits.Move(before, new[] { b.Id }, new Vec2(5, 0));
            Assert.Equal(new[] { b.Id }, Variants.Diff(before, moved).Changed);
        }

        [Fact]
        public void TheEngineComparesWithAStoredState()
        {
            var api = new EngineApi(ModuleCatalog.CreateDefault());
            var session = EngineApiTests.Call(api, "createSession", new { lang = "en" }).GetProperty("session").GetString()!;
            var item = api.Registry.Library.First(i => i.TypeId == "mech.gear.spur" && i.View == "axial").Id;
            var g = EngineApiTests.Insert(api, session, item, 60, 60);
            var doc = api.Sessions.Get(session).Doc;
            var payload = EngineApi.CompactOf(doc, doc.Pages[0], doc.Objects.Keys);
            var h = EngineApiTests.Insert(api, session, item, 140, 60);
            var d = EngineApiTests.Call(api, "diff", new { session, payload });
            var added = d.GetProperty("added").EnumerateArray().ToList();
            Assert.Equal(h, Assert.Single(added).GetProperty("id").GetString());
            Assert.True(added[0].GetProperty("w").GetDouble() > 0);
            Assert.Empty(d.GetProperty("removed").EnumerateArray());
            Assert.Empty(d.GetProperty("changed").EnumerateArray());
        }
    }
}
