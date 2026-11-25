using System;
using System.Linq;
using System.Text.Json;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Engine.Tests
{
    /// <summary>
    /// Engine methods for the web add-in's handles on the drawing (EngineWeb.cs): lock badges (<c>links</c>,
    /// <c>toggleLink</c>), corners and branches (<c>polylineHandles</c>, <c>insertCorner</c>, <c>removeCorner</c>,
    /// <c>splitLine</c>, <c>branch</c>, <c>joinLines</c>), insert places (<c>insertHandles</c>, <c>applyInsertHandle</c>)
    /// and the calculation sheet as a table (<c>insertCalcTable</c>).
    /// </summary>
    public class WebHandleApiTests
    {
        private static readonly Lazy<EngineApi> Api = new(() => new EngineApi(ModuleCatalog.CreateDefault()));
        private static Registry Reg => Api.Value.Registry;

        private static JsonElement Call(string method, object args) => EngineApiTests.Call(Api.Value, method, args);

        private static string Open(SketchDocument doc, string lang = "en")
            => Call("createSession", new { lang, json = DocumentJson.Serialize(doc) }).GetProperty("session").GetString()!;

        private static SketchDocument Doc(params SketchObject[] objects)
        {
            var doc = SketchDocument.Create();
            return Edits.Add(doc, doc.Pages[0].Id, objects);
        }

        private static SketchObject Wire(Vec2 a, Vec2 b)
            => Reg.Create(Reg.Library.First(i => i.TypeId == "elec.wire"), Vec2.Zero).WithParam("route", "free") with { Points = new[] { a, b } };

        private static JsonElement Points(JsonElement state, string id) => EngineApiTests.Obj(state, id).GetProperty("points");

        [Fact]
        public void LinksListTheMeshAndAClickReleasesAndCouplesIt()
        {
            var ga = Reg.Create(Reg.Item("mech.spur.axial")!, new Vec2(60, 60)) with { Size = new Size2(30, 30) };
            var gb = Reg.Create(Reg.Item("mech.spur.axial")!, Vec2.Zero) with { Size = new Size2(20, 20) };
            var doc = Doc(ga, gb);
            double R(string id) => Reg.PageAnchors(doc.Get(id)!, new RenderContext(doc)).First(x => x.Kind == AnchorKind.Circle && x.Role == AnchorRole.Pitch).Radius;
            var d = R(ga.Id) + R(gb.Id);
            doc = Edits.Update(doc, new[] { gb.Id }, o => o with { Transform = o.Transform with { X = 60 + d, Y = 60 } });
            var session = Open(doc);

            var link = Assert.Single(Call("links", new { session, id = ga.Id }).EnumerateArray());
            Assert.Equal("mesh", link.GetProperty("kind").GetString());
            Assert.False(link.GetProperty("released").GetBoolean());
            Assert.Equal(60 + R(ga.Id), link.GetProperty("x").GetDouble(), 6);
            Assert.Contains("click releases", link.GetProperty("label").GetString());

            var key = link.GetProperty("key").GetString()!;
            var st = Call("toggleLink", new { session, id = ga.Id, key });
            Assert.True(st.GetProperty("canUndo").GetBoolean());
            Assert.True(Call("links", new { session, id = ga.Id })[0].GetProperty("released").GetBoolean());
            // The key stays the same, so the next click couples again.
            Call("toggleLink", new { session, id = ga.Id, key });
            Assert.False(Call("links", new { session, id = ga.Id })[0].GetProperty("released").GetBoolean());
            Assert.Contains("Unknown link", Api.Value.Call("toggleLink", JsonSerializer.Serialize(new { session, id = ga.Id, key = "mesh:x:y:-1" })));
        }

        [Fact]
        public void CornersAreAddedBentRemovedAndSplit()
        {
            var w = Wire(new Vec2(20, 40), new Vec2(120, 40));
            var session = Open(Doc(w));

            var h = Call("polylineHandles", new { session, id = w.Id });
            Assert.True(h.GetProperty("corners").GetBoolean());
            Assert.True(h.GetProperty("branch").GetBoolean());
            var seg = Assert.Single(h.GetProperty("segments").EnumerateArray());
            Assert.Equal(70, seg.GetProperty("x").GetDouble(), 6);
            Assert.Equal(-1, seg.GetProperty("ny").GetDouble(), 6);
            Assert.Empty(h.GetProperty("removable").EnumerateArray());

            // Click on the dot: a corner where the dot is; drag: bent to the pointer (one undo step).
            var st = Call("insertCorner", new { session, id = w.Id, index = 0, x = 70, y = 20, points = new[] { new[] { 70.0, 40.0 } } });
            var pts = Points(st, w.Id);
            Assert.Equal(3, pts.GetArrayLength());
            Assert.Equal(20, pts[1].GetProperty("y").GetDouble(), 6);
            Assert.Equal(new[] { 1 }, Call("polylineHandles", new { session, id = w.Id }).GetProperty("removable").EnumerateArray().Select(x => x.GetInt32()));

            st = Call("removeCorner", new { session, id = w.Id, index = 1 });
            Assert.Equal(2, Points(st, w.Id).GetArrayLength());
            Call("undo", new { session });

            st = Call("splitLine", new { session, id = w.Id, index = 1 });
            Assert.Equal(2, st.GetProperty("objects").GetArrayLength());
            Assert.Equal(2, Points(st, w.Id).GetArrayLength());
        }

        [Fact]
        public void ABranchStartsOnTheLineAndIsSelected()
        {
            var w = Wire(new Vec2(20, 40), new Vec2(120, 40));
            var session = Open(Doc(w));
            var st = Call("branch", new { session, id = w.Id, points = new[] { new[] { 90.0, 40.0 }, new[] { 90.0, 80.0 } } });
            var id = st.GetProperty("selection")[0].GetString()!;
            Assert.NotEqual(w.Id, id);
            Assert.Equal("elec.wire", EngineApiTests.Obj(st, id).GetProperty("type").GetString());
            var pts = Points(st, id);
            Assert.Equal(90, pts[0].GetProperty("x").GetDouble(), 6);
            Assert.Equal(40, pts[0].GetProperty("y").GetDouble(), 6);
            Assert.Equal(80, pts[pts.GetArrayLength() - 1].GetProperty("y").GetDouble(), 6);
        }

        [Fact]
        public void AnEndDroppedOnAFreeEndOfTheSameKindJoinsBothLines()
        {
            var a = Wire(new Vec2(0, 0), new Vec2(10, 0));
            var b = Wire(new Vec2(20, 0), new Vec2(30, 0));
            var session = Open(Doc(a, b));
            var miss = Call("joinLines", new { session, id = a.Id, index = 1, x = 15, y = 0 });
            Assert.False(miss.GetProperty("joined").GetBoolean());
            Assert.False(Call("state", new { session, includeSvg = false }).GetProperty("canUndo").GetBoolean());

            var hit = Call("joinLines", new { session, id = a.Id, index = 1, x = 20, y = 0 });
            Assert.True(hit.GetProperty("joined").GetBoolean());
            var st = hit.GetProperty("state");
            Assert.Single(st.GetProperty("objects").EnumerateArray());
            Assert.Equal(30, Points(st, a.Id).EnumerateArray().Max(p => p.GetProperty("x").GetDouble()), 6);
        }

        [Fact]
        public void InsertPlacesOfATableAddARowWhereTheDotIs()
        {
            var session = Call("createSession", new { lang = "en" }).GetProperty("session").GetString()!;
            var id = EngineApiTests.Insert(Api.Value, session, "ann.table", 80, 60);
            var list = Call("insertHandles", new { session, id }).EnumerateArray().ToList();
            Assert.Contains(list, h => h.GetProperty("key").GetString() == "row.1");
            Assert.Contains(list, h => h.GetProperty("key").GetString() == "col.1");
            var row = list.First(h => h.GetProperty("key").GetString() == "row.1");
            Assert.Equal(-1, row.GetProperty("dx").GetDouble(), 6);
            Assert.Contains("row", row.GetProperty("label").GetString());

            var before = list.Count(h => h.GetProperty("key").GetString()!.StartsWith("row.", StringComparison.Ordinal));
            Call("applyInsertHandle", new { session, id, key = "row.1" });
            var after = Call("insertHandles", new { session, id }).EnumerateArray().Count(h => h.GetProperty("key").GetString()!.StartsWith("row.", StringComparison.Ordinal));
            Assert.Equal(before + 1, after);
            Assert.Contains("Unknown insert place", Api.Value.Call("applyInsertHandle", JsonSerializer.Serialize(new { session, id, key = "row.99" })));
        }

        [Fact]
        public void TheCalculationSheetGoesIntoATableRightOfTheElement()
        {
            var session = Call("createSession", new { lang = "de" }).GetProperty("session").GetString()!;
            var id = EngineApiTests.Insert(Api.Value, session, "elec.resistor", 50, 50);
            Call("setValue", new { session, id, key = "R", text = "100 Ω" });
            Call("setValue", new { session, id, key = "I", text = "2 A" });
            var st = Call("insertCalcTable", new { session, ids = new[] { id } });
            var tableId = st.GetProperty("selection")[0].GetString()!;
            var table = EngineApiTests.Obj(st, tableId);
            Assert.Equal("ann.table", table.GetProperty("type").GetString());
            Assert.True(table.GetProperty("bounds").GetProperty("x").GetDouble() > EngineApiTests.Obj(st, id).GetProperty("bounds").GetProperty("x").GetDouble());
            var saved = DocumentJson.Deserialize(Call("saveDocument", new { session }).GetProperty("json").GetString()!);
            Assert.StartsWith("Element|Zeichen|Wert", saved.Get(tableId)!.Str("cells", ""));
            Call("undo", new { session });
            Assert.Single(Call("state", new { session, includeSvg = false }).GetProperty("objects").EnumerateArray());
        }
    }
}
