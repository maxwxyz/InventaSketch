using System;
using System.Linq;
using System.Text.Json;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Engine.Tests
{
    /// <summary>Engine methods added for the LibreOffice host: <c>insertLegend</c> and <c>designationsOf</c>.</summary>
    public class EngineLoApiTests
    {
        private static JsonElement Call(EngineApi api, string method, object args) => EngineApiTests.Call(api, method, args);

        private static readonly Registry Reg = ModuleCatalog.CreateDefault();

        private static SketchObject Line(string typeId, Vec2 a, Vec2 b, params (string Key, ParamValue Value)[] ps)
        {
            var o = Reg.CreateBetween(Reg.AllItems.First(i => i.TypeId == typeId), a, b);
            foreach (var (k, v) in ps) o = o.WithParam(k, v);
            return o;
        }

        [Fact]
        public void TheLegendListsTheUsedColoursAsOneGroupAtTheTopRight()
        {
            var api = new EngineApi(Reg);
            var session = Call(api, "createSession", new { lang = "de", pageWidth = 254.0, pageHeight = 190.5 }).GetProperty("session").GetString()!;
            var s = api.Sessions.Get(session);
            // Nothing coloured yet: a message, no change.
            Assert.ThrowsAny<Exception>(() => Call(api, "insertLegend", new { session }));
            var l1 = Line("elec.wire", new(20, 50), new(120, 50), ("lineRole", "L1"));
            var pe = Line("elec.wire", new(20, 70), new(120, 70), ("lineRole", "PE"));
            s.Apply("Add", d => Edits.Add(d, s.PageId, l1, pe));
            var before = s.Doc.Objects.Count;
            var st = Call(api, "insertLegend", new { session });
            var selection = st.GetProperty("selection").EnumerateArray().Select(x => x.GetString()!).ToList();
            // Title, two lines (green-yellow: two lines) and two names.
            Assert.True(selection.Count >= 5, "legend objects: " + selection.Count);
            Assert.Equal(before + selection.Count, s.Doc.Objects.Count);
            var group = s.Doc.Get(selection[0])!.GroupId;
            Assert.NotNull(group);
            Assert.All(selection, id => Assert.Equal(group, s.Doc.Get(id)!.GroupId));
            var texts = selection.Select(id => s.Doc.Get(id)!.Text).Where(t => t is not null).ToList();
            Assert.Contains("Farben nach Medium", texts);
            Assert.Contains(texts, t => t!.Contains("IEC 60445"));
            // Top right, moved left so the longest name (estimated 0.6 em per character) ends inside the page.
            var texts2 = selection.Select(id => s.Doc.Get(id)!).Where(o => o.Text is not null).ToList();
            Assert.All(texts2, o => Assert.True(o.Transform.X - o.Size.W / 2 + o.Text!.Length * 0.6 * 5.0 <= 254.0 - 5.9, o.Text));
            Assert.True(texts2.Min(o => o.Transform.X - o.Size.W / 2) > 254.0 / 2, "right half");
            Assert.All(selection, id => Assert.True(s.Doc.Get(id)!.Transform.X < 254.0 || s.Doc.Get(id)!.IsPointGeometry, id));
            // One undo step removes the whole legend.
            Call(api, "undo", new { session });
            Assert.Equal(before, s.Doc.Objects.Count);
        }

        [Fact]
        public void DesignationsOfPayloadsWithoutASession()
        {
            var api = new EngineApi(Reg);
            var a = SketchDocument.Create(new Size2(300, 200));
            var w3 = Line("elec.wire", new(40, 80), new(140, 80), ("ref", "-W3"));
            var h1 = Line("bs.pipe", new(40, 50), new(140, 50), ("ref", "H1"));
            a = Edits.Add(a, a.Pages[0].Id, w3, h1);
            var b = SketchDocument.Create(new Size2(300, 200));
            b = Edits.Add(b, b.Pages[0].Id, Line("elec.wire", new(10, 10), new(60, 10)));
            var payloads = new[] { EngineApi.CompactOf(a, a.Pages[0], new[] { w3.Id, h1.Id }), EngineApi.CompactOf(b, b.Pages[0], b.Pages[0].Order), "isk1:broken" };
            var result = Call(api, "designationsOf", new { json = JsonSerializer.Serialize(payloads) });
            Assert.Equal(3, result.GetArrayLength());
            Assert.Equal(new[] { "H1", "W3" }, result[0].EnumerateArray().Select(x => x.GetString()).ToArray());
            Assert.Equal(0, result[1].GetArrayLength());
            Assert.Equal(0, result[2].GetArrayLength());
            Assert.Equal(0, Call(api, "designationsOf", new { }).GetArrayLength());
        }
    }
}
