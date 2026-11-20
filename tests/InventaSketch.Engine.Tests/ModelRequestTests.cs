using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Engine.Tests
{
    /// <summary>Test module for input actions, document actions, dependents, plan scale and per-view sizes.</summary>
    internal sealed class ModelRequestModule : IModule
    {
        public string Id => "mrtest";
        public I18nText Name => new("Model request test", "Modellanforderungstest");
        public string Version => "1.0";

        public IEnumerable<ObjectType> Types => new ObjectType[] { new Note(), new Post(), new Plan(), new Vehicle() };

        public IEnumerable<LibraryCategory> Categories => new[] { new LibraryCategory("mrtest", new I18nText("Test", "Test"), Id, 5) };

        public IEnumerable<LibraryItem> Library => new[]
        {
            new LibraryItem("mrtest.note", "mrtest.note", new I18nText("Note", "Notiz"), "mrtest"),
            new LibraryItem("mrtest.post", "mrtest.post", new I18nText("Post", "Pfosten"), "mrtest"),
            new LibraryItem("mrtest.plan", "mrtest.plan", new I18nText("Plan box", "Planbox"), "mrtest"),
            new LibraryItem("mrtest.vehicle", "mrtest.vehicle", new I18nText("Vehicle", "Fahrzeug"), "mrtest"),
        };

        private static IReadOnlyList<Primitive> Box(SketchObject o) => new Primitive[] { Prim.CRect(0, 0, o.Size.W, o.Size.H, Style.Visible) };

        /// <summary>Box with an input action (label text), a document action (rotate like the nearest post) and a disabled action.</summary>
        internal sealed class Note : ObjectType
        {
            public override string Id => "mrtest.note";
            public override I18nText Name => new("Note", "Notiz");
            public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx) => Box(o);

            public override IEnumerable<ContextAction> Actions(SketchObject o, RenderContext ctx)
            {
                yield return ContextAction.WithInput("label", new I18nText("Label…", "Beschriftung…"), IconIds.Text,
                    new I18nText("Label text", "Beschriftungstext"), o.Text ?? "", (x, text) => x with { Text = text });
                var post = ctx.Doc.Objects.Values.FirstOrDefault(x => x.Type == "mrtest.post");
                yield return ContextAction.InDocument("match", new I18nText("Rotate like the post", "Wie der Pfosten drehen"), IconIds.Rotate,
                    (doc, x, _) => Edits.Replace(doc, x with { Transform = x.Transform with { Rotation = post!.Transform.Rotation } })) with { IsEnabled = post is not null };
            }
        }

        /// <summary>Neighbour-dependent box: every other post within 5 mm depends on it.</summary>
        internal sealed class Post : ObjectType
        {
            public override string Id => "mrtest.post";
            public override I18nText Name => new("Post", "Pfosten");
            public override Size2 DefaultSize => new(4, 4);
            public override bool DependsOnNeighbours => true;
            public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx) => Box(o);

            public override IEnumerable<string> Dependents(SketchObject o, RenderContext ctx)
                => ObjectsNear(ctx.Doc, o, 5).Where(x => x.Type == Id).Select(x => x.Id);
        }

        /// <summary>Box whose rendered width is 1000 mm at the document plan scale.</summary>
        internal sealed class Plan : ObjectType
        {
            public override string Id => "mrtest.plan";
            public override I18nText Name => new("Plan box", "Planbox");
            public override bool UsesPlanScale => true;

            public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
            {
                var w = 1000 / (ctx.Doc.Settings.PlanScale ?? 100);
                return new Primitive[] { Prim.CRect(0, 0, w, 1, Style.Visible) };
            }
        }

        /// <summary>Views with very different preferred aspects (top 10 x 20, side 30 x 10).</summary>
        internal sealed class Vehicle : ObjectType
        {
            public override string Id => "mrtest.vehicle";
            public override I18nText Name => new("Vehicle", "Fahrzeug");
            public override Size2 DefaultSize => new(10, 20);
            public override IReadOnlyList<ViewDef> Views => new[]
            {
                new ViewDef("top", new I18nText("Top", "Oben")) { Size = new Size2(10, 20) },
                new ViewDef("side", new I18nText("Side", "Seite")) { Size = new Size2(30, 10) },
                new ViewDef("schematic", new I18nText("Schematic", "Schema")),
            };
            public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx) => Box(o);
        }
    }

    public class ModelRequestTests
    {
        private static (EngineApi Api, string Session) NewSession()
        {
            var api = new EngineApi(ModuleCatalog.Create(new IModule[] { new BasicShapesModule(), new ModelRequestModule() }));
            var st = EngineApiTests.Call(api, "createSession", new { lang = "de" });
            return (api, st.GetProperty("session").GetString()!);
        }

        private static JsonElement Obj(JsonElement state, string id) => state.GetProperty("objects").EnumerateArray().First(o => o.GetProperty("id").GetString() == id);

        private static List<string> Ids(JsonElement state, string prop) => state.GetProperty(prop).EnumerateArray().Select(x => x.GetString()!).ToList();

        [Fact]
        public void InputActionIsListedWithItsCurrentValueAndTakesTheEnteredText()
        {
            var (api, s) = NewSession();
            var id = EngineApiTests.Insert(api, s, "mrtest.note", 50, 50);
            var actions = EngineApiTests.Call(api, "contextActions", new { session = s, ids = new[] { id }, lang = "de" }).EnumerateArray().ToList();
            var label = actions.Single(a => a.GetProperty("id").GetString() == "t:label");
            Assert.Equal("Beschriftung…", label.GetProperty("label").GetString());
            Assert.Equal("Beschriftungstext", label.GetProperty("input").GetProperty("label").GetString());
            Assert.Equal("", label.GetProperty("input").GetProperty("value").GetString());
            Assert.False(actions.Single(a => a.GetProperty("id").GetString() == "t:match").TryGetProperty("input", out _));

            var st = EngineApiTests.Call(api, "applyAction", new { session = s, ids = new[] { id }, action = "t:label", input = "Pumpe" });
            Assert.Equal("Pumpe", Obj(st, id).GetProperty("text").GetString());
            Assert.Equal(new[] { id }, Ids(st, "changed"));
            var again = EngineApiTests.Call(api, "contextActions", new { session = s, ids = new[] { id } }).EnumerateArray()
                .Single(a => a.GetProperty("id").GetString() == "t:label");
            Assert.Equal("Pumpe", again.GetProperty("input").GetProperty("value").GetString());

            // Without a value the engine refuses instead of guessing.
            var err = api.Call("applyAction", JsonSerializer.Serialize(new { session = s, ids = new[] { id }, action = "t:label" }));
            Assert.Contains("\"ok\":false", err);
        }

        [Fact]
        public void DocumentActionSeesOtherObjectsAndIsDisabledWithoutThem()
        {
            var (api, s) = NewSession();
            var note = EngineApiTests.Insert(api, s, "mrtest.note", 50, 50);
            var match = EngineApiTests.Call(api, "contextActions", new { session = s, ids = new[] { note } }).EnumerateArray()
                .Single(a => a.GetProperty("id").GetString() == "t:match");
            Assert.False(match.GetProperty("enabled").GetBoolean());
            var unchanged = EngineApiTests.Call(api, "applyAction", new { session = s, ids = new[] { note }, action = "t:match" });
            Assert.Equal(0, Obj(unchanged, note).GetProperty("rotation").GetDouble());

            var post = EngineApiTests.Insert(api, s, "mrtest.post", 100, 50);
            EngineApiTests.Call(api, "rotate", new { session = s, ids = new[] { post }, degrees = 30 });
            match = EngineApiTests.Call(api, "contextActions", new { session = s, ids = new[] { note } }).EnumerateArray()
                .Single(a => a.GetProperty("id").GetString() == "t:match");
            Assert.True(match.GetProperty("enabled").GetBoolean());
            var st = EngineApiTests.Call(api, "applyAction", new { session = s, ids = new[] { note }, action = "t:match" });
            Assert.Equal(30, Obj(st, note).GetProperty("rotation").GetDouble(), 6);
            Assert.Equal("Action", st.GetProperty("undoLabel").GetString());
            var undone = EngineApiTests.Call(api, "undo", new { session = s });
            Assert.Equal(0, Obj(undone, note).GetProperty("rotation").GetDouble(), 6);
        }

        [Fact]
        public void EditsReportChangedObjectsAndTheirDependents()
        {
            var (api, s) = NewSession();
            var a = EngineApiTests.Insert(api, s, "mrtest.post", 50, 50);
            var b = EngineApiTests.Insert(api, s, "mrtest.post", 56, 50);   // 2 mm gap: depends on a
            var far = EngineApiTests.Insert(api, s, "mrtest.post", 150, 50);
            var inserted = EngineApiTests.Call(api, "state", new { session = s });
            Assert.Equal(new[] { far }, Ids(inserted, "changed"));
            Assert.Empty(Ids(inserted, "dependents"));

            var moved = EngineApiTests.Call(api, "move", new { session = s, ids = new[] { a }, dx = 1, dy = 0 });
            Assert.Equal(new[] { a }, Ids(moved, "changed"));
            Assert.Equal(new[] { b }, Ids(moved, "dependents"));

            // Moving away: b depended on the old position, so it is still redrawn once.
            var away = EngineApiTests.Call(api, "move", new { session = s, ids = new[] { a }, dx = 0, dy = 60 });
            Assert.Equal(new[] { b }, Ids(away, "dependents"));
            var later = EngineApiTests.Call(api, "move", new { session = s, ids = new[] { a }, dx = 1, dy = 0 });
            Assert.Empty(Ids(later, "dependents"));

            // Undo and delete report too (the deleted object is not listed, its former neighbour is).
            var undo = EngineApiTests.Call(api, "undo", new { session = s });
            Assert.Equal(new[] { a }, Ids(undo, "changed"));
            EngineApiTests.Call(api, "undo", new { session = s });
            var deleted = EngineApiTests.Call(api, "delete", new { session = s, ids = new[] { a } });
            Assert.Empty(Ids(deleted, "changed"));
            Assert.Equal(new[] { b }, Ids(deleted, "dependents"));
        }

        [Fact]
        public void PlanScaleIsSetUndoneAndPersisted()
        {
            var (api, s) = NewSession();
            var id = EngineApiTests.Insert(api, s, "mrtest.plan", 50, 50);
            var other = EngineApiTests.Insert(api, s, "mrtest.note", 80, 50);
            var st = EngineApiTests.Call(api, "state", new { session = s });
            Assert.False(st.TryGetProperty("planScale", out _));

            st = EngineApiTests.Call(api, "setPlanScale", new { session = s, value = 50 });
            Assert.Equal(50, st.GetProperty("planScale").GetDouble());
            Assert.Equal(new[] { id }, Ids(st, "changed"));      // only objects that use the plan scale are redrawn
            Assert.DoesNotContain(other, Ids(st, "changed"));
            Assert.Equal("Plan scale", st.GetProperty("undoLabel").GetString());

            var insp = EngineApiTests.Call(api, "inspector", new { session = s, ids = new[] { id } });
            Assert.True(insp.GetProperty("usesPlanScale").GetBoolean());
            Assert.Equal(50, insp.GetProperty("planScale").GetDouble());
            Assert.False(EngineApiTests.Call(api, "inspector", new { session = s, ids = new[] { other } }).GetProperty("usesPlanScale").GetBoolean());

            var json = EngineApiTests.Call(api, "saveDocument", new { session = s }).GetProperty("json").GetString()!;
            Assert.Contains("\"planScale\":50", json);
            var back = DocumentJson.Deserialize(json);
            Assert.Equal(50, back.Settings.PlanScale);
            Assert.Equal(50, DocumentJson.FromCompact(DocumentJson.ToCompact(back)).Settings.PlanScale);

            // The rendering follows the document scale: 1000 mm at 1:50 = 20 mm wide.
            var reg = ModuleCatalog.Create(new IModule[] { new ModelRequestModule() });
            var prims = reg.Render(back.Get(id)!, new RenderContext(back));
            Assert.Equal(20, Prim.Bounds(prims)!.Value.W, 6);

            st = EngineApiTests.Call(api, "undo", new { session = s });
            Assert.False(st.TryGetProperty("planScale", out _));
            st = EngineApiTests.Call(api, "setPlanScale", new { session = s, value = 200 });
            Assert.Equal(200, st.GetProperty("planScale").GetDouble());
            st = EngineApiTests.Call(api, "setPlanScale", new { session = s, value = (double?)null });
            Assert.False(st.TryGetProperty("planScale", out _));
        }

        [Fact]
        public void ImportedSlideModelBringsItsPlanScaleIntoAnEmptySetting()
        {
            var (api, s) = NewSession();
            var doc = SketchDocument.Create() with { Settings = new DocumentSettings { PlanScale = 50 } };
            doc = Edits.Add(doc, doc.Pages[0].Id, new ModelRequestModule.Plan().Create(new Vec2(10, 10)));
            var st = EngineApiTests.Call(api, "importCompact", new { session = s, payload = DocumentJson.ToCompact(doc) });
            Assert.Equal(50, st.GetProperty("planScale").GetDouble());
        }

        [Fact]
        public void ViewSwitchAdaptsTheSizeToTheViewAspectKeepingTheScale()
        {
            var (api, s) = NewSession();
            var id = EngineApiTests.Insert(api, s, "mrtest.vehicle", 100, 80);
            // The user doubled the vehicle: the side view comes out doubled as well.
            EngineApiTests.Call(api, "resize", new { session = s, id, w = 20, h = 40 });
            var side = Obj(EngineApiTests.Call(api, "setView", new { session = s, ids = new[] { id }, view = "side" }), id);
            Assert.Equal(60, side.GetProperty("w").GetDouble(), 6);
            Assert.Equal(20, side.GetProperty("h").GetDouble(), 6);
            Assert.Equal(100, side.GetProperty("cx").GetDouble(), 6);   // centre kept

            var top = Obj(EngineApiTests.Call(api, "setView", new { session = s, ids = new[] { id }, view = "top" }), id);
            Assert.Equal(20, top.GetProperty("w").GetDouble(), 6);
            Assert.Equal(40, top.GetProperty("h").GetDouble(), 6);

            // A view without a preferred size uses the type's default size (10 x 20) as its reference.
            var schematic = Obj(EngineApiTests.Call(api, "setView", new { session = s, ids = new[] { id }, view = "schematic" }), id);
            Assert.Equal(20, schematic.GetProperty("w").GetDouble(), 6);
            Assert.Equal(40, schematic.GetProperty("h").GetDouble(), 6);

            // Same view again and unknown views change nothing.
            var same = Obj(EngineApiTests.Call(api, "setView", new { session = s, ids = new[] { id }, view = "schematic" }), id);
            Assert.Equal(20, same.GetProperty("w").GetDouble(), 6);
            var unknown = Obj(EngineApiTests.Call(api, "setView", new { session = s, ids = new[] { id }, view = "nope" }), id);
            Assert.Equal("schematic", unknown.GetProperty("view").GetString());
        }

        [Fact]
        public void ViewSwitchWithoutPreferredSizesKeepsTheSize()
        {
            var (api, s) = EngineApiTests.NewSession();
            var id = EngineApiTests.Insert(api, s, "etest.block", 50, 50);
            var before = Obj(EngineApiTests.Call(api, "state", new { session = s }), id);
            var after = Obj(EngineApiTests.Call(api, "setView", new { session = s, ids = new[] { id }, view = "side" }), id);
            Assert.Equal("side", after.GetProperty("view").GetString());
            Assert.Equal(before.GetProperty("w").GetDouble(), after.GetProperty("w").GetDouble(), 9);
            Assert.Equal(before.GetProperty("h").GetDouble(), after.GetProperty("h").GetDouble(), 9);
        }

        [Fact]
        public void ContextActionsRunnerIsSharedByAllHosts()
        {
            var reg = ModuleCatalog.Create(new IModule[] { new ModelRequestModule() });
            var doc = SketchDocument.Create();
            var note = new ModelRequestModule.Note().Create(new Vec2(10, 10));
            var post = new ModelRequestModule.Post().Create(new Vec2(40, 10)) with { Transform = new Transform(40, 10, 45) };
            doc = Edits.Add(doc, doc.Pages[0].Id, note, post);
            var labelled = ContextActions.Run(reg, doc, new[] { note.Id }, "label", "A", Lang.En);
            Assert.Equal("A", labelled.Get(note.Id)!.Text);
            var matched = ContextActions.Run(reg, labelled, new[] { note.Id, post.Id }, "match", null, Lang.En);
            Assert.Equal(45, matched.Get(note.Id)!.Transform.Rotation, 9);
            Assert.Same(labelled.Get(post.Id), matched.Get(post.Id));      // the post has no such action
            Assert.Throws<ArgumentException>(() => ContextActions.Run(reg, doc, new[] { note.Id }, "label", null, Lang.En));
        }

        [Fact]
        public void ArchitectureWallEditRedrawsJoinedWallsAndItsDoors()
        {
            var api = new EngineApi(ModuleCatalog.CreateDefault());
            var s = EngineApiTests.Call(api, "createSession", new { lang = "en" }).GetProperty("session").GetString()!;
            var w1 = EngineApiTests.Call(api, "insert", new { session = s, itemId = "arch.wall", x = 100, y = 80 }).GetProperty("selection")[0].GetString()!;
            var wall = EngineApiTests.Call(api, "state", new { session = s }).GetProperty("objects").EnumerateArray().Single().GetProperty("points");
            var end = new Vec2(wall[1].GetProperty("x").GetDouble(), wall[1].GetProperty("y").GetDouble());
            var w2 = EngineApiTests.Call(api, "insert", new { session = s, itemId = "arch.wall", x = end.X + 30, y = end.Y + 30 }).GetProperty("selection")[0].GetString()!;
            EngineApiTests.Call(api, "setPoint", new { session = s, id = w2, index = 0, x = end.X, y = end.Y });
            var door = EngineApiTests.Insert(api, s, "arch.door.single", 100, 80);

            var st = EngineApiTests.Call(api, "setParam", new { session = s, ids = new[] { w1 }, key = "thickness", value = 300 });
            Assert.Equal(new[] { w1 }, Ids(st, "changed"));
            Assert.Contains(w2, Ids(st, "dependents"));
            Assert.Contains(door, Ids(st, "dependents"));
            Assert.Equal(string.Join(",", Ids(st, "dependents").OrderBy(x => x)), string.Join(",", new[] { w2, door }.OrderBy(x => x)));
        }
    }
}
