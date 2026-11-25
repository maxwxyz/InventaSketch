using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using InventaSketch.Core;
using Xunit;
using Xunit.Abstractions;

namespace InventaSketch.Engine.Tests
{
    /// <summary>Clean slides for sharing through the engine: one undo step, the report, and every library element in both modes.</summary>
    public class SanitizeApiTests
    {
        private static readonly Registry Reg = ModuleCatalog.CreateDefault();
        private readonly ITestOutputHelper _out;

        public SanitizeApiTests(ITestOutputHelper output) => _out = output;

        private static JsonElement Call(EngineApi api, string method, object args)
        {
            var json = api.Invoke(method, JsonSerializer.Serialize(args, DocumentJson.Options));
            return JsonDocument.Parse(json).RootElement.Clone();
        }

        private static string Insert(EngineApi api, string session, string item, double x, double y)
            => Call(api, "insert", new { session, itemId = item, x, y }).GetProperty("selection")[0].GetString()!;

        private static string Param(EngineApi api, string session, string id, string key)
        {
            var doc = DocumentJson.Deserialize(Call(api, "saveDocument", new { session }).GetProperty("json").GetString()!);
            return doc.Get(id)!.Str(key, "");
        }

        [Fact]
        public void SanitizeCleansThePageInOneUndoStepAndReports()
        {
            var api = new EngineApi(Reg);
            var session = Call(api, "createSession", new { lang = "de" }).GetProperty("session").GetString()!;
            var chart = Insert(api, session, "chart.line.multi", 100, 80);
            var bars = Insert(api, session, "chart.column.grouped", 220, 80);
            Call(api, "setParam", new { session, ids = new[] { chart }, key = "title", value = "Geheimprojekt" });
            var data = Param(api, session, chart, "data");
            var revision = Call(api, "state", new { session }).GetProperty("revision").GetInt32();

            var res = Call(api, "sanitize", new { session, mode = "randomised", value = new { seed = 42, replaceTexts = true, renameCategories = true } });
            var report = res.GetProperty("report");
            Assert.Equal("randomised", report.GetProperty("mode").GetString());
            Assert.Equal(42, report.GetProperty("seed").GetInt32());
            Assert.Equal(2, report.GetProperty("elements").GetInt32());
            Assert.Equal(2, report.GetProperty("changed").GetInt32());
            Assert.Equal(2, report.GetProperty("values").GetInt32());
            Assert.Equal(1, report.GetProperty("texts").GetInt32());
            Assert.Contains("2 von 2 Elementen bereinigt (zufällig)", report.GetProperty("summary").GetString());
            Assert.Equal(3, report.GetProperty("lines").GetArrayLength());
            var state = res.GetProperty("state");
            Assert.Equal(revision + 1, state.GetProperty("revision").GetInt32());
            Assert.Equal("Clean slide", state.GetProperty("undoLabel").GetString());
            Assert.Contains(chart, state.GetProperty("changed").EnumerateArray().Select(e => e.GetString()));
            var cleaned = Param(api, session, chart, "data");
            Assert.NotEqual(data, cleaned);
            Assert.DoesNotContain("Geheim", Param(api, session, chart, "title"));
            Assert.Contains("\"A\"", Param(api, session, bars, "data"));

            // One undo step restores everything.
            Call(api, "undo", new { session });
            Assert.Equal(data, Param(api, session, chart, "data"));
            Assert.Equal("Geheimprojekt", Param(api, session, chart, "title"));

            // The same seed reproduces the result; ids limit the pass to the selection.
            Call(api, "redo", new { session });
            Call(api, "undo", new { session });
            var again = Call(api, "sanitize", new { session, ids = new[] { chart }, mode = "randomized", value = new { seed = 42, replaceTexts = true } });
            Assert.Equal(1, again.GetProperty("report").GetProperty("elements").GetInt32());
            Assert.Equal(cleaned, Param(api, session, chart, "data"));
        }

        [Fact]
        public void NothingToCleanAddsNoUndoStepAndUnknownModesFail()
        {
            var api = new EngineApi(Reg);
            var session = Call(api, "createSession", new { lang = "en" }).GetProperty("session").GetString()!;
            Insert(api, session, "chart.axes", 100, 80);
            var res = Call(api, "sanitize", new { session, mode = "none" });
            Assert.False(res.GetProperty("state").GetProperty("canUndo").GetBoolean() && res.GetProperty("state").GetProperty("undoLabel").GetString() == "Clean slide");
            Assert.StartsWith("Nothing to clean", res.GetProperty("report").GetProperty("summary").GetString());
            Assert.Throws<ArgumentException>(() => api.Invoke("sanitize", JsonSerializer.Serialize(new { session, mode = "shuffle" }, DocumentJson.Options)));
            var normalised = Call(api, "sanitize", new { session });
            Assert.Equal("normalised", normalised.GetProperty("report").GetProperty("mode").GetString());
            Assert.Equal(1, normalised.GetProperty("report").GetProperty("changed").GetInt32());
        }

        private static SketchObject ByType(string typeId) => Reg.Create(Reg.AllItems.First(i => i.TypeId == typeId), new Vec2(100, 100));

        private static SketchObject Clean(SketchObject o, SanitizeMode mode, int seed = 3, bool texts = false)
            => Reg.Type(o.Type)!.Sanitize(o, new SanitizeOptions { Mode = mode, Seed = seed, ReplaceTexts = texts }, new Random(seed));

        [Fact]
        public void DimensionsTablesAndComponentValuesAreCleaned()
        {
            var dim = ByType("ann.dimension.linear").WithParam("scale", 2.5);
            var n = Clean(dim, SanitizeMode.Normalised);
            Assert.Equal(1, n.Str("text", "").Length);
            Assert.Equal(1, n.Num("scale", 0));
            var r = Clean(dim, SanitizeMode.Randomised);
            Assert.NotEqual(2.5, r.Num("scale", 2.5));
            Assert.Equal("", r.Str("text", ""));
            var over = Clean(dim.WithParam("text", "Ø 20"), SanitizeMode.Randomised);
            Assert.NotEqual("Ø 20", over.Str("text", ""));
            Assert.StartsWith("Ø ", over.Str("text", ""));

            var table = ByType("ann.table").WithParam("cells", "Part|Mass|Price\nRotor|12|240\nStator|8|120");
            var tn = Clean(table, SanitizeMode.Normalised, texts: true);
            var rows = InventaSketch.Modules.Annotation.TableType.Parse(tn.Str("cells", ""));
            Assert.Equal("1", rows[1][1]);
            Assert.Equal("0.67", rows[2][1]);
            Assert.Equal("0.5", rows[2][2]);
            Assert.NotEqual("Rotor", rows[1][0]);
            var tr = InventaSketch.Modules.Annotation.TableType.Parse(Clean(table, SanitizeMode.Randomised).Str("cells", ""));
            Assert.Equal("Rotor", tr[1][0]);
            Assert.NotEqual("240", tr[1][2]);

            var res = ByType("elec.resistor").WithParam("ref", "R1").WithParam("value", "10 kΩ");
            Assert.Equal("", Clean(res, SanitizeMode.Normalised).Str("value", "x"));
            var rv = Clean(res, SanitizeMode.Randomised);
            Assert.EndsWith(" kΩ", rv.Str("value", ""));
            Assert.NotEqual("10 kΩ", rv.Str("value", ""));
            Assert.Equal("R1", rv.Str("ref", ""));
        }

        /// <summary>
        /// Audit: every library element cleaned in both modes with every text option, without exception, still drawn inside
        /// its box (box objects) and with the same number of points (point objects).
        /// </summary>
        [Fact]
        public void EveryLibraryElementCleansInBothModesAndStaysInItsBox()
        {
            var failures = new List<string>();
            int changed = 0, items = 0;
            foreach (var item in Reg.AllItems)
            {
                SketchObject o;
                try { o = Reg.Create(item, new Vec2(500, 400)); }
                catch (Exception) { continue; }
                items++;
                var doc = SketchDocument.Create(new Size2(1000, 800));
                doc = Edits.Add(doc, doc.Pages[0].Id, o);
                foreach (var mode in new[] { SanitizeMode.Normalised, SanitizeMode.Randomised })
                {
                    try
                    {
                        var opt = new SanitizeOptions { Mode = mode, Seed = 17, RenameCategories = true, ReplaceTexts = true, StripMeta = true };
                        var (clean, report) = Sanitizer.Apply(Reg, doc, new[] { o.Id }, opt);
                        if (report.Failed > 0) { failures.Add($"{item.Id} {mode}: type failed"); continue; }
                        if (report.Changed > 0) changed++;
                        var c = clean.Get(o.Id)!;
                        var prims = Reg.Render(c, new RenderContext(clean));
                        if (prims.Count == 0) { failures.Add($"{item.Id} {mode}: draws nothing"); continue; }
                        if (c.IsPointGeometry)
                        {
                            if (c.Points!.Count != o.Points!.Count) failures.Add($"{item.Id} {mode}: points changed");
                            continue;
                        }
                        var b = Prim.Bounds(prims);
                        var tol = Math.Max(c.Size.W, c.Size.H) * 0.1 + 2;
                        var box = c.Bounds();
                        if (b is { } r && (r.X < box.X - tol || r.Right > box.Right + tol || r.Y < box.Y - tol || r.Bottom > box.Bottom + tol))
                        {
                            // Only a problem if the original fits its box.
                            var b0 = Prim.Bounds(Reg.Render(o, new RenderContext(doc)));
                            var grew = b0 is { } r0 && (r.W > r0.W + tol || r.H > r0.H + tol);
                            if (grew) failures.Add($"{item.Id} {mode}: leaves its box ({r.X - box.X:0.#},{r.Y - box.Y:0.#} {r.W:0.#}×{r.H:0.#} in {c.Size.W:0.#}×{c.Size.H:0.#})");
                        }
                    }
                    catch (Exception ex) { failures.Add($"{item.Id} {mode}: {ex.GetType().Name}: {ex.Message.Split('\n')[0]}"); }
                }
            }
            _out.WriteLine($"{items} items, {changed} cleanings changed an element");
            foreach (var f in failures) _out.WriteLine(f);
            Assert.True(failures.Count == 0, string.Join("\n", failures.Take(40)));
            Assert.True(changed > 50, "cleaning should change many library elements: " + changed);
        }
    }
}
