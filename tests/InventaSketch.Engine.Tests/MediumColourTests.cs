using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Engine.Tests
{
    /// <summary>
    /// Colour by medium (<see cref="LineMedia"/>) and designations across pages (<see cref="Designations"/>) with the real
    /// modules and through the engine API (ROADMAP plan review 2026-11-25 night, items 11 and 12).
    /// </summary>
    public class MediumColourTests
    {
        private static readonly Registry Reg = ModuleCatalog.CreateDefault();

        /// <summary>An engine session with every module (the shared test API only has the basic shapes).</summary>
        private static (EngineApi Api, string Session) NewSession()
        {
            var api = new EngineApi(Reg);
            var st = EngineApiTests.Call(api, "createSession", new { lang = "en" });
            return (api, st.GetProperty("session").GetString()!);
        }

        private static SketchObject Line(string typeId, Vec2 a, Vec2 b, params (string Key, ParamValue Value)[] ps)
        {
            var o = Reg.CreateBetween(Reg.AllItems.First(i => i.TypeId == typeId), a, b);
            foreach (var (k, v) in ps) o = o.WithParam(k, v);
            return o;
        }

        private static SketchDocument Doc(params SketchObject[] objects)
        {
            var doc = SketchDocument.Create(new Size2(300, 200));
            return Edits.Add(doc, doc.Pages[0].Id, objects);
        }

        private static string? Colour(SketchDocument doc, SketchObject o) => LineMedia.Of(Reg, doc, o)?.Colour?.Id;

        private static IEnumerable<string?> Strokes(SketchDocument doc, SketchObject o)
            => Prim.Flatten(Reg.Render(o, new RenderContext(doc))).Select(p => p switch { PathPrim pa => pa.Style.Stroke, LinePrim l => l.Style.Stroke, _ => null }).Where(s => s is not null);

        [Fact]
        public void EveryModuleLineGetsItsMediumAndRole()
        {
            var dc = Line("energy.line.cable", new(10, 10), new(80, 10), ("dc", true));
            var ac = Line("energy.line.cable", new(10, 20), new(80, 20));
            var data = Line("energy.line.cable", new(10, 30), new(80, 30), ("kind", "data"));
            var flow = Line("bs.pipe", new(10, 40), new(80, 40), ("medium", "flow"));
            var ret = Line("bs.pipe", new(10, 50), new(80, 50), ("medium", "return"));
            var hot = Line("bs.pipe", new(10, 60), new(80, 60), ("medium", "hot"));
            var pilot = Line("fluid.line", new(10, 70), new(80, 70), ("kind", "pilot"));
            var pressure = Line("fluid.line", new(10, 80), new(80, 80), ("lineRole", "pressure"));
            var steam = Line("process.line", new(10, 90), new(80, 90), ("lineRole", "steam"));
            var plain = Line("process.line", new(10, 100), new(80, 100));
            var bus = Line("elec.bus", new(10, 110), new(80, 110));
            var pe = Line("elec.wire", new(10, 120), new(80, 120), ("lineRole", "PE"));
            var core = Line("sw.line.wire", new(10, 130), new(80, 130), ("colour", "BU"));
            var signal = Line("process.line.signal", new(10, 140), new(80, 140));
            var gas = Line("energy.line.pipe", new(10, 150), new(80, 150), ("carrier", "gas"));
            var doc = Doc(dc, ac, data, flow, ret, hot, pilot, pressure, steam, plain, bus, pe, core, signal, gas);
            Assert.Equal("dc", Colour(doc, dc));
            Assert.Equal("power", Colour(doc, ac));
            Assert.Equal("bus", Colour(doc, data));
            Assert.Equal("flow", Colour(doc, flow));
            Assert.Equal("return", Colour(doc, ret));
            Assert.Equal("hot", Colour(doc, hot));
            Assert.Equal("pilot", Colour(doc, pilot));
            Assert.Equal("pressure", Colour(doc, pressure));
            Assert.Equal("p.steam", Colour(doc, steam));
            Assert.Null(Colour(doc, plain));
            Assert.Equal("bus", Colour(doc, bus));
            Assert.Equal("PE", Colour(doc, pe));
            Assert.Null(Colour(doc, core));
            Assert.Equal("signal", Colour(doc, signal));
            Assert.Equal("gas", Colour(doc, gas));

            // Switched on, the lines take the colours; a cabinet core keeps its real colour; switched off all is as before.
            var on = doc with { Settings = doc.Settings with { ColorBy = LineMedia.ByMedium } };
            Assert.Contains("#d32f2f", Strokes(on, flow));
            Assert.Contains("#2e9d3a", Strokes(on, pe));
            Assert.Contains("#f2d500", Strokes(on, pe));
            Assert.Equal(Strokes(doc, core), Strokes(on, core));
            Assert.Equal(Strokes(doc, plain), Strokes(on, plain));
            Assert.DoesNotContain("#2e9d3a", Strokes(doc, pe));
        }

        [Fact]
        public void TheEngineSwitchesTheModeAndDrawPlansFollow()
        {
            var (api, session) = NewSession();
            var s = api.Sessions.Get(session);
            var wire = Line("elec.wire", new(20, 50), new(120, 50), ("lineRole", "L1"));
            s.Apply("Add", d => Edits.Add(d, s.PageId, wire));
            var st = EngineApiTests.Call(api, "setColorBy", new { session, text = "medium" });
            Assert.Equal("medium", st.GetProperty("colorBy").GetString());
            Assert.Contains(st.GetProperty("changed").EnumerateArray(), x => x.GetString() == wire.Id);
            static IEnumerable<PlanOp> All(IEnumerable<PlanOp> ops) => ops.SelectMany(o => new[] { o }.Concat(All(o.Children ?? new List<PlanOp>())));
            var plan = DrawPlanBuilder.Build(s, new[] { wire.Id }, DrawCapabilities.Full).Units.Single();
            Assert.Contains(All(plan.Ops), o => o.Stroke?.Color == "#8d5524");
            var media = EngineApiTests.Call(api, "lineMedia", new { session, ids = new[] { wire.Id } });
            Assert.Equal("L1", media[0].GetProperty("role").GetString());
            Assert.Equal("electric", media[0].GetProperty("medium").GetString());
            var used = EngineApiTests.Call(api, "mediumColours", new { session, mode = "used", lang = "de" });
            Assert.Equal("Außenleiter L1", used.EnumerateArray().Single().GetProperty("name").GetString());
            Assert.Equal("IEC 60445", used[0].GetProperty("source").GetString());
            Assert.Equal(LineMedia.Scheme.Count, EngineApiTests.Call(api, "mediumColours", new { session }).GetArrayLength());
            EngineApiTests.Call(api, "setColorBy", new { session, text = "off" });
            Assert.Null(s.Doc.Settings.ColorBy);
            Assert.ThrowsAny<System.Exception>(() => EngineApiTests.Call(api, "setColorBy", new { session, text = "rainbow" }));
            // The slide model decides (a unit drawn with the mode on brings it along).
            var src = SketchDocument.Create() with { Settings = new DocumentSettings { ColorBy = LineMedia.ByMedium } };
            EngineApiTests.Call(api, "importCompact", new { session, payload = EngineApi.CompactOf(src, src.Pages[0], System.Array.Empty<string>()) });
            Assert.Equal("medium", s.Doc.Settings.ColorBy);
        }

        [Fact]
        public void DesignationsSameNameAndContinuationsThroughTheEngine()
        {
            var (api, session) = NewSession();
            var s = api.Sessions.Get(session);
            var page = s.Page.Size;
            var a = Line("bs.pipe", new(40, 50), new(page.W - 0.5, 50), ("ref", "H1"), ("value", "DN 20"));
            var b = Line("elec.wire", new(40, 80), new(140, 80), ("ref", "W3"));
            var c = Line("energy.line.cable", new(40, 110), new(140, 110), ("label", "-w3"));
            s.Apply("Add", d => Edits.Add(d, s.PageId, a, b, c));
            var list = EngineApiTests.Call(api, "designations", new { session });
            Assert.Equal(new[] { "H1", "W3" }, list.EnumerateArray().Select(x => x.GetProperty("key").GetString()));
            Assert.Equal(2, list[1].GetProperty("ids").GetArrayLength());
            var same = EngineApiTests.Call(api, "connected", new { session, ids = new[] { b.Id }, mode = "samename" });
            Assert.Equal(new[] { b.Id, c.Id }, same.EnumerateArray().Select(x => x.GetString()));
            var options = EngineApiTests.Call(api, "connectOptions", new { session, ids = new[] { b.Id } });
            Assert.Contains(options.EnumerateArray(), o => o.GetProperty("kind").GetString() == "samename");

            // The pipe ends at the page edge: "→ H1"; once the host says where else it is, "→ H1, slide 4".
            static IEnumerable<PlanOp> All(IEnumerable<PlanOp> ops) => ops.SelectMany(o => new[] { o }.Concat(All(o.Children ?? new List<PlanOp>())));
            string[] Texts() => All(DrawPlanBuilder.Build(s, new[] { a.Id }, DrawCapabilities.Full).Units.Single().Ops).Select(o => o.Text).Where(t => t is not null).ToArray()!;
            Assert.Contains("→ H1", Texts());
            Assert.Contains("H1 · DN 20", Texts());
            EngineApiTests.Call(api, "setContinuations", new { session, json = "{\"h1\":[\"4\"]}", text = "slide" });
            Assert.Contains("→ H1, slide 4", Texts());
            EngineApiTests.Call(api, "setContinuations", new { session });
            Assert.Null(s.Continuations);
        }

        [Fact]
        public void GenericActionsReachEveryConnectionLineAndKeepPictogramsApart()
        {
            var lines = Reg.AllItems.Where(i => Reg.Type(i.TypeId) is { Geometry: GeometryKind.Points } t && t.LineMedium(Reg.Create(i, new Vec2(100, 100))) is not null)
                .GroupBy(i => i.TypeId).Select(g => g.First()).ToList();
            Assert.True(lines.Count >= 10, "connection line types: " + lines.Count);
            foreach (var item in lines)
            {
                var o = Reg.Create(item, new Vec2(100, 100));
                var doc = Doc(o);
                var type = Reg.Type(o.Type)!;
                var actions = Reg.Actions(o, new RenderContext(doc)).ToList();
                // Either the type's own designation or the generic one, never both.
                var own = Designations.OwnKey(type, true);
                Assert.True(own is not null || actions.Any(a => a.Id == "line.ref"), item.Id + ": no designation");
                Assert.False(own is not null && actions.Any(a => a.Id == "line.ref"), item.Id + ": designation twice");
                Assert.Equal(actions.Count, actions.Select(a => a.Id).Distinct().Count());
                // One pictogram one meaning among the generic and the type's actions (label-only markers excepted).
                foreach (var g in actions.Where(a => !IconIds.IsLabelOnly(a.Icon) && !IconIds.IsStep(a.Icon) && a.Icon != IconIds.Option).GroupBy(a => a.Icon))
                    Assert.True(g.Count() == 1 || g.All(a => a.Choice is not null && a.Choice.Key == g.First().Choice?.Key) || g.Select(a => a.Id.Split('.')[0]).Distinct().Count() == 1 && !g.Any(a => a.Id.StartsWith("line.", System.StringComparison.Ordinal)),
                        item.Id + ": pictogram " + g.Key + " for " + string.Join(", ", g.Select(a => a.Id)));
            }
        }

        [Fact]
        public void BuildingServicesDevicesGetADesignationElectricalSymbolsKeepTheirs()
        {
            var pump = Reg.Create(Reg.AllItems.First(i => i.TypeId == "bs.heat.pump"), new Vec2(100, 100));
            var resistor = Reg.Create(Reg.AllItems.First(i => i.TypeId == "elec.resistor"), new Vec2(150, 100));
            var doc = Doc(pump, resistor);
            Assert.Contains(Reg.Actions(pump, new RenderContext(doc)), a => a.Id == "elem.ref");
            Assert.DoesNotContain(Reg.Actions(resistor, new RenderContext(doc)), a => a.Id == "elem.ref");
            var named = ContextActions.Run(Reg, doc, new[] { pump.Id }, "elem.ref", "P1", Lang.En);
            Assert.Contains(Reg.Render(named.Get(pump.Id)!, new RenderContext(named)).OfType<TextPrim>(), t => t.Text == "P1" && t.Tag == Designations.LabelTag);
            Assert.Equal("P1", Designations.Of(Reg, named.Get(pump.Id)!));
        }
    }
}
