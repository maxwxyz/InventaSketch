using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Engine.Tests
{
    public class EngineApiTests
    {
        internal static EngineApi NewApi() => new(ModuleCatalog.Create(new IModule[] { new BasicShapesModule(), new BasicTextItemModule(), new EngineTestModule() }));

        internal static JsonElement Call(EngineApi api, string method, object args)
        {
            var json = api.Invoke(method, JsonSerializer.Serialize(args, DocumentJson.Options));
            return JsonDocument.Parse(json).RootElement.Clone();
        }

        internal static (EngineApi Api, string Session) NewSession(string lang = "en")
        {
            var api = NewApi();
            var st = Call(api, "createSession", new { lang });
            return (api, st.GetProperty("session").GetString()!);
        }

        internal static string Insert(EngineApi api, string session, string item, double x, double y)
        {
            var st = Call(api, "insert", new { session, itemId = item, x, y });
            return st.GetProperty("selection")[0].GetString()!;
        }

        internal static JsonElement Obj(JsonElement state, string id) => state.GetProperty("objects").EnumerateArray().First(o => o.GetProperty("id").GetString() == id);

        [Fact]
        public void DefaultCatalogRegistersAllModulesThroughTheInterface()
        {
            var registry = ModuleCatalog.CreateDefault();
            var ids = registry.Modules.Select(m => m.Id).ToList();
            Assert.Contains("annotation", ids);
            Assert.Contains("mechanical", ids);
            Assert.Contains("patent", ids);
            Assert.Contains("basic", ids);
        }

        [Fact]
        public void CollidingModuleIsSkippedNotFatal()
        {
            var errors = new List<string>();
            var registry = ModuleCatalog.Create(new IModule[] { new EngineTestModule(), new EngineTestModule() }, errors);
            Assert.Single(registry.Modules);
            Assert.Single(errors);
        }

        [Fact]
        public void VersionListsMethods()
        {
            var api = NewApi();
            var v = Call(api, "version", new { });
            var methods = v.GetProperty("methods").EnumerateArray().Select(m => m.GetString()).ToList();
            foreach (var m in new[] { "insert", "undo", "redo", "hitTest", "snapMove", "snapPoint", "contextActions", "drawPlan", "renderPageSvg", "catalog" })
                Assert.Contains(m, methods);
        }

        [Fact]
        public void CallWrapsResultsAndErrorsInEnvelope()
        {
            var api = NewApi();
            using var ok = JsonDocument.Parse(api.Call("createSession", "{\"lang\":\"de\"}"));
            Assert.True(ok.RootElement.GetProperty("ok").GetBoolean());
            Assert.StartsWith("s", ok.RootElement.GetProperty("result").GetProperty("session").GetString());
            using var bad = JsonDocument.Parse(api.Call("nope", "{}"));
            Assert.False(bad.RootElement.GetProperty("ok").GetBoolean());
            Assert.Contains("nope", bad.RootElement.GetProperty("error").GetString());
            using var missing = JsonDocument.Parse(api.Call("move", "{\"session\":\"unknown\"}"));
            Assert.False(missing.RootElement.GetProperty("ok").GetBoolean());
        }

        [Fact]
        public void CatalogHasLocalisedNamesAndSvgThumbnails()
        {
            var api = NewApi();
            var de = Call(api, "catalog", new { lang = "de-DE" });
            Assert.Equal("de", de.GetProperty("lang").GetString());
            var cats = de.GetProperty("categories").EnumerateArray().Select(c => c.GetProperty("name").GetString()).ToList();
            Assert.Contains("Testteile", cats);
            var wheel = de.GetProperty("items").EnumerateArray().First(i => i.GetProperty("id").GetString() == "etest.wheel");
            Assert.Equal("Rad", wheel.GetProperty("name").GetString());
            Assert.StartsWith("<svg", wheel.GetProperty("thumbnail").GetString());
            Assert.Contains("<ellipse", wheel.GetProperty("thumbnail").GetString());
            var en = Call(api, "catalog", new { lang = "fr" });
            Assert.Equal("en", en.GetProperty("lang").GetString());
        }

        [Fact]
        public void InsertCreatesSelectedObjectAtPosition()
        {
            var (api, s) = NewSession();
            var st = Call(api, "insert", new { session = s, itemId = "etest.block", x = 50, y = 40 });
            var id = st.GetProperty("selection")[0].GetString()!;
            var o = Obj(st, id);
            Assert.Equal(50, o.GetProperty("cx").GetDouble(), 6);
            Assert.Equal(40, o.GetProperty("cy").GetDouble(), 6);
            Assert.Equal("front", o.GetProperty("view").GetString());
            Assert.Contains("data-isk-id=\"" + id + "\"", st.GetProperty("svg").GetString());
            Assert.True(st.GetProperty("canUndo").GetBoolean());
        }

        [Fact]
        public void UndoRedoThroughApi()
        {
            var (api, s) = NewSession();
            var id = Insert(api, s, "etest.block", 50, 40);
            Call(api, "move", new { session = s, ids = new[] { id }, dx = 10, dy = 0 });
            var undone = Call(api, "undo", new { session = s });
            Assert.Equal(50, Obj(undone, id).GetProperty("cx").GetDouble(), 6);
            Assert.True(undone.GetProperty("canRedo").GetBoolean());
            var redone = Call(api, "redo", new { session = s });
            Assert.Equal(60, Obj(redone, id).GetProperty("cx").GetDouble(), 6);
            Call(api, "undo", new { session = s });
            var empty = Call(api, "undo", new { session = s });
            Assert.Empty(empty.GetProperty("objects").EnumerateArray());
            Assert.False(empty.GetProperty("canUndo").GetBoolean());
        }

        [Fact]
        public void TransformCommands()
        {
            var (api, s) = NewSession();
            var id = Insert(api, s, "etest.block", 50, 40);
            var st = Call(api, "rotate", new { session = s, ids = new[] { id }, degrees = 90 });
            Assert.Equal(90, Obj(st, id).GetProperty("rotation").GetDouble(), 6);
            st = Call(api, "flip", new { session = s, ids = new[] { id }, horizontal = true });
            Assert.True(Obj(st, id).GetProperty("flipX").GetBoolean());
            st = Call(api, "scale", new { session = s, ids = new[] { id }, sx = 2, sy = 2 });
            Assert.Equal(40, Obj(st, id).GetProperty("w").GetDouble(), 6);
            st = Call(api, "resize", new { session = s, id, w = 30, h = 12, x = 70, y = 45 });
            var o = Obj(st, id);
            Assert.Equal(30, o.GetProperty("w").GetDouble(), 6);
            Assert.Equal(70, o.GetProperty("cx").GetDouble(), 6);
            st = Call(api, "duplicate", new { session = s, ids = new[] { id } });
            var copy = st.GetProperty("selection")[0].GetString();
            Assert.NotEqual(id, copy);
            Assert.Equal(75, Obj(st, copy!).GetProperty("cx").GetDouble(), 6);
            st = Call(api, "reorder", new { session = s, ids = new[] { id }, to = "front" });
            Assert.Equal(id, st.GetProperty("objects").EnumerateArray().Last().GetProperty("id").GetString());
            st = Call(api, "group", new { session = s, ids = new[] { id, copy } });
            Assert.NotNull(Obj(st, id).GetProperty("groupId").GetString());
            st = Call(api, "select", new { session = s, ids = new[] { id } });
            Assert.Equal(2, st.GetProperty("selection").GetArrayLength());
            st = Call(api, "delete", new { session = s, ids = new[] { id } });
            Assert.Empty(st.GetProperty("objects").EnumerateArray());
        }

        [Fact]
        public void ParamViewStyleAndText()
        {
            var (api, s) = NewSession();
            var id = Insert(api, s, "etest.block", 50, 40);
            Call(api, "setParam", new { session = s, ids = new[] { id }, key = "teeth", value = 30 });
            var st = Call(api, "setView", new { session = s, ids = new[] { id }, view = "side" });
            Assert.Equal("side", Obj(st, id).GetProperty("view").GetString());
            Call(api, "setView", new { session = s, ids = new[] { id }, view = "bogus" });
            st = Call(api, "setStyle", new { session = s, ids = new[] { id }, style = new { stroke = "#ff0000", dash = "dash" } });
            Assert.Contains("#ff0000", st.GetProperty("svg").GetString());
            Call(api, "setText", new { session = s, id, text = "hello" });
            var insp = Call(api, "inspector", new { session = s, ids = new[] { id }, lang = "de" });
            Assert.Equal("Block", insp.GetProperty("typeName").GetString());
            Assert.Equal("side", insp.GetProperty("view").GetString());
            var teeth = insp.GetProperty("params").EnumerateArray().First();
            Assert.Equal("Zähne", teeth.GetProperty("label").GetString());
            Assert.Equal(30, teeth.GetProperty("value").GetDouble());
            Assert.Equal("integer", teeth.GetProperty("kind").GetString());
            Assert.Equal("dash", insp.GetProperty("style").GetProperty("dash").GetString());
            Assert.Equal("hello", insp.GetProperty("text").GetString());
        }

        [Fact]
        public void HitTestBoxAndLine()
        {
            var (api, s) = NewSession();
            var box = Insert(api, s, "etest.block", 50, 40);
            var wire = Insert(api, s, "etest.wire", 100, 40); // points (85,40)-(115,40)
            Assert.Equal(box, Call(api, "hitTest", new { session = s, x = 55, y = 42, tolerance = 1 }).GetProperty("id").GetString());
            Assert.Equal(wire, Call(api, "hitTest", new { session = s, x = 100, y = 40.8, tolerance = 1 }).GetProperty("id").GetString());
            var miss = Call(api, "hitTest", new { session = s, x = 100, y = 45, tolerance = 1 });
            Assert.False(miss.TryGetProperty("id", out _)); // null members are omitted
            Assert.Empty(miss.GetProperty("stack").EnumerateArray());
        }

        [Fact]
        public void MarqueeEnclosedVersusTouching()
        {
            var (api, s) = NewSession();
            var a = Insert(api, s, "etest.block", 50, 40);  // 40..60 x 35..45
            var b = Insert(api, s, "etest.block", 100, 40); // 90..110
            var enclosed = Call(api, "selectRect", new { session = s, x = 30, y = 30, w = 65, h = 20, mode = "enclosed" });
            Assert.Equal(new[] { a }, enclosed.GetProperty("selection").EnumerateArray().Select(e => e.GetString()).ToArray());
            var touching = Call(api, "selectRect", new { session = s, x = 30, y = 30, w = 65, h = 20, mode = "touching" });
            Assert.Equal(2, touching.GetProperty("selection").GetArrayLength());
            Assert.Contains(b, touching.GetProperty("selection").EnumerateArray().Select(e => e.GetString()));
        }

        [Fact]
        public void SnapMoveSnapsToCornerAndMeshesWheels()
        {
            var (api, s) = NewSession();
            Call(api, "setSnap", new { session = s, snapOptions = new { tolerance = 2.0, grid = (double?)null } });
            var a = Insert(api, s, "etest.block", 50, 40);  // corner (60,35)
            var b = Insert(api, s, "etest.block", 80, 40);  // corner (70,35)
            // Move b by -9 (corner at 61): snaps to -10 so corners coincide.
            var snap = Call(api, "snapMove", new { session = s, ids = new[] { b }, dx = -9.3, dy = 0.4 });
            Assert.Equal(-10, snap.GetProperty("dx").GetDouble(), 6);
            Assert.Equal(0, snap.GetProperty("dy").GetDouble(), 6);
            Assert.Equal("point", snap.GetProperty("kind").GetString());
            Assert.NotEmpty(snap.GetProperty("guides").EnumerateArray());

            var w1 = Insert(api, s, "etest.wheel", 150, 100); // r = 10
            var w2 = Insert(api, s, "etest.wheel", 171, 100);
            var mesh = Call(api, "snapMove", new { session = s, ids = new[] { w2 }, dx = 0.5, dy = 0 });
            Assert.Equal("mesh", mesh.GetProperty("kind").GetString());
            Assert.Equal(-1, mesh.GetProperty("dx").GetDouble(), 6); // centre distance 20 = r1 + r2
            _ = a; _ = w1;
        }

        [Fact]
        public void SnapPointAttachesToBoxCorner()
        {
            var (api, s) = NewSession();
            Call(api, "setSnap", new { session = s, snapOptions = new { tolerance = 2.0, grid = 5.0 } });
            var box = Insert(api, s, "etest.block", 50, 40);
            var wire = Insert(api, s, "etest.wire", 100, 80);
            var sp = Call(api, "snapPoint", new { session = s, x = 60.6, y = 35.5, exclude = new[] { wire } });
            Assert.Equal(60, sp.GetProperty("x").GetDouble(), 6);
            Assert.Equal(35, sp.GetProperty("y").GetDouble(), 6);
            Assert.Equal(box, sp.GetProperty("attach").GetProperty("objectId").GetString());
            // setPoint with snapping attaches; moving the box drags the wire end along.
            Call(api, "setPoint", new { session = s, id = wire, index = 0, x = 60.6, y = 35.5, snap = true });
            var st = Call(api, "move", new { session = s, ids = new[] { box }, dx = 10, dy = 0 });
            var p0 = Obj(st, wire).GetProperty("points")[0];
            Assert.Equal(70, p0.GetProperty("x").GetDouble(), 6);
            // "Anchor" off: the end still snaps to the corner, but no longer follows the box.
            Call(api, "setPoint", new { session = s, id = wire, index = 0, x = 70.4, y = 35.3, snap = true, anchor = false });
            st = Call(api, "move", new { session = s, ids = new[] { box }, dx = 10, dy = 0 });
            p0 = Obj(st, wire).GetProperty("points")[0];
            Assert.Equal(70, p0.GetProperty("x").GetDouble(), 6);
            // Grid snap when nothing else is near.
            var g = Call(api, "snapPoint", new { session = s, x = 151.2, y = 148.9, exclude = new[] { wire } });
            Assert.Equal("grid", g.GetProperty("kind").GetString());
            Assert.Equal(150, g.GetProperty("x").GetDouble(), 6);
        }

        [Fact]
        public void SnapPointDocksOnLifelineRailAndAttaches()
        {
            var api = new EngineApi(ModuleCatalog.CreateDefault());
            var s = Call(api, "createSession", new { lang = "en" }).GetProperty("session").GetString()!;
            Call(api, "setSnap", new { session = s, snapOptions = new { tolerance = 2.0, grid = (double?)null } });
            var life = Insert(api, s, "diag.seq.lifeline", 60, 80);
            var msg = Insert(api, s, "diag.seq.message", 120, 140);
            var sp = Call(api, "snapPoint", new { session = s, x = 60.8, y = 93.3, exclude = new[] { msg } });
            Assert.Equal("rail", sp.GetProperty("kind").GetString());
            Assert.Equal(60, sp.GetProperty("x").GetDouble(), 6);
            Assert.Equal(life, sp.GetProperty("attach").GetProperty("objectId").GetString());
            // The docked message end follows the lifeline.
            Call(api, "setPoint", new { session = s, id = msg, index = 0, x = 60.8, y = 93.3, snap = true });
            var st = Call(api, "move", new { session = s, ids = new[] { life }, dx = 15, dy = 0 });
            Assert.Equal(75, Obj(st, msg).GetProperty("points")[0].GetProperty("x").GetDouble(), 6);
        }

        [Fact]
        public void SnapInsertPreviewsWithoutInserting()
        {
            var (api, s) = NewSession();
            Call(api, "setSnap", new { session = s, snapOptions = new { tolerance = 2.0, grid = 5.0 } });
            var r = Call(api, "snapInsert", new { session = s, itemId = "etest.block", x = 51.2, y = 39.1 });
            Assert.Equal(50, r.GetProperty("x").GetDouble(), 6);
            Assert.Equal(40, r.GetProperty("y").GetDouble(), 6);
            Assert.Empty(Call(api, "state", new { session = s, includeSvg = false }).GetProperty("objects").EnumerateArray());
        }

        [Fact]
        public void ContextActionsDeleteFirstAndGreyedItems()
        {
            var (api, s) = NewSession("de");
            var id = Insert(api, s, "etest.block", 50, 40);
            var actions = Call(api, "contextActions", new { session = s, ids = new[] { id } }).EnumerateArray().ToList();
            Assert.Equal("delete", actions[0].GetProperty("id").GetString());
            Assert.Equal("Löschen", actions[0].GetProperty("label").GetString());
            Assert.False(actions.First(a => a.GetProperty("id").GetString() == "group").GetProperty("enabled").GetBoolean());
            Assert.False(actions.First(a => a.GetProperty("id").GetString() == "ungroup").GetProperty("enabled").GetBoolean());
            Assert.Contains(actions, a => a.GetProperty("id").GetString() == "view:side");
            Assert.True(actions.First(a => a.GetProperty("id").GetString() == "view:front").GetProperty("active").GetBoolean());
            Assert.Equal(IconIds.Front, actions.First(a => a.GetProperty("id").GetString() == "view:front").GetProperty("icon").GetString());
            Assert.Equal(IconIds.Side, actions.First(a => a.GetProperty("id").GetString() == "view:side").GetProperty("icon").GetString());
            var more = actions.First(a => a.GetProperty("id").GetString() == "t:etest.more");
            Assert.False(more.GetProperty("active").GetBoolean());

            Call(api, "applyAction", new { session = s, ids = new[] { id }, action = "t:etest.more" });
            var insp = Call(api, "inspector", new { session = s, ids = new[] { id } });
            Assert.Equal(40, insp.GetProperty("params")[0].GetProperty("value").GetDouble());
            var st = Call(api, "applyAction", new { session = s, ids = new[] { id }, action = "rotate90" });
            Assert.Equal(90, Obj(st, id).GetProperty("rotation").GetDouble(), 6);
            st = Call(api, "applyAction", new { session = s, ids = new[] { id }, action = "numeral" });
            Assert.Equal("1", Obj(st, id).GetProperty("ref").GetString());
            st = Call(api, "applyAction", new { session = s, ids = new[] { id }, action = "delete" });
            Assert.Empty(st.GetProperty("objects").EnumerateArray());
            var none = Call(api, "contextActions", new { session = s, ids = Array.Empty<string>() }).EnumerateArray().ToList();
            Assert.False(none[0].GetProperty("enabled").GetBoolean());
        }

        [Fact]
        public void SaveOpenRoundTripAndCamelCaseContract()
        {
            var (api, s) = NewSession();
            var id = Insert(api, s, "etest.block", 50, 40);
            var saved = Call(api, "saveDocument", new { session = s }).GetProperty("json").GetString()!;
            Assert.Contains("\"format\":\"inventasketch-document\"", saved);
            Assert.Contains("\"transform\":", saved);
            var opened = Call(api, "openDocument", new { json = saved });
            var s2 = opened.GetProperty("session").GetString();
            Assert.NotEqual(s, s2);
            Assert.Equal(id, opened.GetProperty("objects")[0].GetProperty("id").GetString());
            Assert.False(opened.GetProperty("canUndo").GetBoolean());
        }

        [Fact]
        public void CompactExportImportAndHostTransform()
        {
            var (api, s) = NewSession();
            var id = Insert(api, s, "etest.block", 50, 40);
            var payload = Call(api, "exportCompact", new { session = s, ids = new[] { id } }).GetProperty("payload").GetString()!;
            Assert.StartsWith("isk1:", payload);
            var (api2, s2) = NewSession();
            var st = Call(api2, "importCompact", new { session = s2, payload });
            Assert.Equal(id, st.GetProperty("selection")[0].GetString());

            // The user moved the group by 28.3465 pt (10 mm) and doubled its width on the slide.
            var pt = Geo.PtPerMm;
            var from = new { x = 40 * pt, y = 35 * pt, w = 20 * pt, h = 10 * pt };
            var target = new { x = 50 * pt, y = 35 * pt, w = 40 * pt, h = 10 * pt };
            st = Call(api2, "hostTransform", new { session = s2, ids = new[] { id }, from, target });
            var o = Obj(st, id);
            Assert.Equal(40, o.GetProperty("w").GetDouble(), 4);
            Assert.Equal(70, o.GetProperty("cx").GetDouble(), 4);
            Assert.Equal(40, o.GetProperty("cy").GetDouble(), 4);
        }

        [Fact]
        public void RenderSelectionSvgUsesViewBoxOfSelection()
        {
            var (api, s) = NewSession();
            var id = Insert(api, s, "etest.block", 50, 40);
            Insert(api, s, "etest.block", 150, 100);
            var r = Call(api, "renderPageSvg", new { session = s, ids = new[] { id }, padding = 0, monochrome = true });
            Assert.Equal(20, r.GetProperty("width").GetDouble(), 6);
            var svg = r.GetProperty("svg").GetString()!;
            Assert.Contains("viewBox=\"40 35 20 10\"", svg);
            Assert.Single(System.Text.RegularExpressions.Regex.Matches(svg, "data-isk-id"));
        }
    }
}
