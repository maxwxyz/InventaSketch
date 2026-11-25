using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using InventaSketch.Core;
using Xunit;
using Xunit.Abstractions;

namespace InventaSketch.Engine.Tests
{
    /// <summary>
    /// Review of every library element (maintainer request 2026-11-25: "are all modules complete, do coupling, insert and
    /// edit work?"): insert, render inside its box, every parameter value, every view, every context action, free
    /// non-uniform scaling, the engine round trip (insert, draw plan, compact model back and forth, undo/redo). Every
    /// problem is listed with the element id.
    /// </summary>
    public class LibraryAuditTests
    {
        private static readonly Registry Reg = ModuleCatalog.CreateDefault();
        private readonly ITestOutputHelper _out;

        public LibraryAuditTests(ITestOutputHelper output) => _out = output;

        private static SketchDocument Doc(SketchObject o)
        {
            var doc = SketchDocument.Create(new Size2(1000, 800));
            return Edits.Add(doc, doc.Pages[0].Id, o);
        }

        private static IReadOnlyList<Primitive> Render(SketchObject o) => Reg.Render(o, new RenderContext(Doc(o)));

        /// <summary>Values worth trying for a parameter (besides its default).</summary>
        private static IEnumerable<ParamValue> Alternatives(ParamDef p)
        {
            switch (p.Kind)
            {
                case ParamKind.Boolean:
                    yield return !(p.Default?.Bool ?? false);
                    break;
                case ParamKind.Choice:
                    foreach (var o in p.Options ?? Array.Empty<ParamOption>()) yield return o.Value;
                    break;
                case ParamKind.Integer:
                case ParamKind.Number:
                    var d = p.Default?.Number ?? 1;
                    if (p.Min is double min) yield return p.Kind == ParamKind.Integer ? Math.Ceiling(min) : min;
                    if (p.Max is double max) yield return p.Kind == ParamKind.Integer ? Math.Floor(max) : max;
                    var mid = d * 1.5 + (p.Kind == ParamKind.Integer ? 1 : 0.5);
                    if ((p.Min is null || mid >= p.Min) && (p.Max is null || mid <= p.Max)) yield return p.Kind == ParamKind.Integer ? Math.Round(mid) : mid;
                    break;
                case ParamKind.Text:
                    yield return "Test 12";
                    yield return "";
                    break;
            }
        }

        [Fact]
        public void EveryElementInsertsRendersAndEdits()
        {
            var failures = new List<string>();
            var outside = new List<string>();
            int items = 0, edits = 0;
            void Try(string what, Action a)
            {
                try { a(); edits++; }
                catch (Exception ex) { failures.Add($"{what}: {ex.GetType().Name}: {ex.Message.Split('\n')[0]}"); }
            }
            foreach (var item in Reg.AllItems)
            {
                items++;
                SketchObject o;
                try { o = Reg.Create(item, new Vec2(500, 400)); }
                catch (Exception ex) { failures.Add($"{item.Id} create: {ex.Message}"); continue; }
                var type = Reg.Type(o.Type)!;
                Try($"{item.Id} render", () =>
                {
                    var prims = Render(o);
                    if (prims.Count == 0) throw new InvalidOperationException("draws nothing");
                    // Box objects draw inside their box (tolerance: a tenth of the box plus 2 mm for heads, labels, strokes).
                    if (!o.IsPointGeometry && Prim.Bounds(Reg.RenderLocal(o, new RenderContext(Doc(o)))) is { } b)
                    {
                        double tw = o.Size.W * 0.1 + 2, th = o.Size.H * 0.1 + 2;
                        if (b.X < -o.Size.W / 2 - tw || b.Y < -o.Size.H / 2 - th || b.X + b.W > o.Size.W / 2 + tw || b.Y + b.H > o.Size.H / 2 + th)
                            outside.Add($"{item.Id}: drawing {b.W:0.#} x {b.H:0.#} at ({b.X:0.#}, {b.Y:0.#}), box {o.Size.W:0.#} x {o.Size.H:0.#}");
                    }
                });
                foreach (var p in type.Parameters)
                    foreach (var v in Alternatives(p))
                        Try($"{item.Id} param {p.Key}={v.Number?.ToString() ?? v.Text ?? v.Bool?.ToString()}", () => Render(Reg.SetParam(o, p.Key, v)));
                foreach (var view in type.Views)
                    Try($"{item.Id} view {view.Id}", () => Render(Reg.WithView(o, view.Id, new RenderContext(Doc(o)))));
                var actions = new List<ContextAction>();
                Try($"{item.Id} actions", () => actions.AddRange(type.Actions(o, new RenderContext(Doc(o)))));
                foreach (var a in actions)
                    Try($"{item.Id} action {a.Id}", () => Render(a.Apply(o)));
                if (!o.IsPointGeometry)
                    Try($"{item.Id} scale", () =>
                    {
                        var s = o with { Size = new Size2(o.Size.W * 1.7, o.Size.H * 0.6) };
                        Render(s);
                        if (!s.Params.OrderBy(kv => kv.Key).SequenceEqual(o.Params.OrderBy(kv => kv.Key))) throw new InvalidOperationException("scaling changed parameters");
                    });
            }
            foreach (var f in failures) _out.WriteLine("FAIL " + f);
            foreach (var f in outside) _out.WriteLine("OUTSIDE " + f);
            _out.WriteLine($"{items} elements, {edits} checks, {failures.Count} failure(s), {outside.Count} drawn outside the box");
            Assert.Empty(failures);
            Assert.Empty(outside);
        }

        [Fact]
        public void EveryElementSurvivesTheEngineRoundTrip()
        {
            var api = new EngineApi(ModuleCatalog.CreateDefault());
            var failures = new List<string>();
            foreach (var item in api.Registry.AllItems)
            {
                try
                {
                    var session = EngineApiTests.Call(api, "createSession", new { lang = "de", pageWidth = 280.0, pageHeight = 157.5 }).GetProperty("session").GetString()!;
                    var st = EngineApiTests.Call(api, "insert", new { session, itemId = item.Id, x = 140.0, y = 80.0 });
                    var ids = st.GetProperty("selection").EnumerateArray().Select(e => e.GetString()!).ToArray();
                    if (ids.Length == 0) { failures.Add($"{item.Id}: insert selected nothing"); continue; }
                    var plan = EngineApiTests.Call(api, "drawPlan", new { session, ids });
                    if (plan.GetProperty("units").GetArrayLength() == 0) failures.Add($"{item.Id}: empty draw plan");
                    var payload = EngineApiTests.Call(api, "exportCompact", new { session, ids }).GetProperty("payload").GetString()!;
                    var other = EngineApiTests.Call(api, "createSession", new { lang = "de", pageWidth = 280.0, pageHeight = 157.5 }).GetProperty("session").GetString()!;
                    EngineApiTests.Call(api, "importCompact", new { session = other, payload });
                    var a = api.Sessions.Get(session).Doc; var b = api.Sessions.Get(other).Doc;
                    if (Variants.Diff(a, b) is { None: false } d) failures.Add($"{item.Id}: compact round trip differs (+{d.Added.Count} -{d.Removed.Count} ~{d.Changed.Count})");
                    EngineApiTests.Call(api, "undo", new { session });
                    if (api.Sessions.Get(session).Doc.Objects.Count != 0) failures.Add($"{item.Id}: undo left objects");
                    EngineApiTests.Call(api, "redo", new { session });
                    if (api.Sessions.Get(session).Doc.Objects.Count == 0) failures.Add($"{item.Id}: redo restored nothing");
                    EngineApiTests.Call(api, "closeSession", new { session });
                    EngineApiTests.Call(api, "closeSession", new { session = other });
                }
                catch (Exception ex) { failures.Add($"{item.Id}: {ex.GetType().Name}: {ex.Message.Split('\n')[0]}"); }
            }
            foreach (var f in failures) _out.WriteLine("FAIL " + f);
            _out.WriteLine($"{api.Registry.Library.Count()} elements, {failures.Count} failure(s)");
            Assert.Empty(failures);
        }

        [Fact]
        public void NoTwoModulesOfferAnElementOfTheSameName()
        {
            // Overlaps are cut (LibraryItem.Alternative) or named apart (2026-11-25 review): one name, one element.
            string Norm(string s) => new string(s.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
            var moduleOf = Reg.Categories.ToDictionary(c => c.Id, c => c.ModuleId);
            var clashes = new List<string>();
            foreach (var key in new Func<LibraryItem, string>[] { i => Norm(i.Name.En), i => Norm(i.Name.De ?? i.Name.En) })
                foreach (var g in Reg.Library.GroupBy(key).Where(g => g.Select(i => moduleOf[i.Category]).Distinct().Count() > 1))
                    clashes.Add(string.Join(", ", g.Select(i => $"{i.Id} \"{i.Name.En}\" / \"{i.Name.De}\"")));
            foreach (var c in clashes) _out.WriteLine("CLASH " + c);
            Assert.Empty(clashes);
        }

        [Fact]
        public void AnElementWithAnAlternativeStepsBackOnlyWhileTheAlternativeIsThere()
        {
            Assert.Null(Reg.Library.FirstOrDefault(i => i.Id == "arch.sanitary.wc"));
            Assert.NotNull(Reg.Item("arch.sanitary.wc"));                         // still found by id (existing slides)
            Assert.DoesNotContain(Reg.Categories, c => c.Id == Reg.Item("arch.sanitary.wc")!.Category);
            // The architecture module on its own keeps its sanitary and heating symbols.
            var alone = new Registry().Register(new InventaSketch.Modules.Architecture.ArchitectureModule());
            Assert.NotNull(alone.Library.FirstOrDefault(i => i.Id == "arch.sanitary.wc"));
            Assert.Contains(alone.Categories, c => c.Id == alone.Item("arch.sanitary.wc")!.Category);
        }

        [Fact]
        public void Order()
        {
            // Not an assertion: categories in menu order and their items in library order, for the ordering review.
            foreach (var m in Reg.Modules)
                foreach (var c in Reg.Categories.Where(c => c.ModuleId == m.Id).OrderBy(c => c.Order))
                    _out.WriteLine($"ORDER {m.Id} | {c.Order} {c.Name.De} | " + string.Join(" · ", Reg.Library.Where(i => i.Category == c.Id).Select(i => i.Name.De)));
        }

        [Fact]
        public void Inventory()
        {
            // Not an assertion: the module and category overview plus element names that occur in more than one module
            // (candidates for overlaps), for the review.
            foreach (var m in Reg.Modules)
            {
                var cats = Reg.Categories.Where(c => c.ModuleId == m.Id).ToList();
                var n = Reg.Library.Count(i => cats.Any(c => c.Id == i.Category));
                _out.WriteLine($"MODULE {m.Id} ({m.Name.En}): {cats.Count} categories, {n} items, {m.Types.Count()} types");
            }
            string Norm(string s) => new string(s.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
            var moduleOf = Reg.Categories.ToDictionary(c => c.Id, c => c.ModuleId);
            foreach (var g in Reg.Library.GroupBy(i => Norm(i.Name.En)).Where(g => g.Select(i => moduleOf.TryGetValue(i.Category, out var m) ? m : "?").Distinct().Count() > 1))
                _out.WriteLine("SAME NAME " + g.First().Name.En + ": " + string.Join(", ", g.Select(i => i.Id)));
        }
    }
}
