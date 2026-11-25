using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Engine.Tests
{
    /// <summary>Accent colour as a slide style (<see cref="Accents"/>, <see cref="DocumentSettings.Accent"/>, <c>setAccent</c>).</summary>
    public class AccentTests
    {
        private static readonly Registry Reg = ModuleCatalog.CreateDefault();

        [Fact]
        public void NeutralColoursStayAndColouredOnesTakeTheHue()
        {
            const string accent = "#c62828"; // red
            Assert.Equal("#000000", Accents.Recolor("#000000", accent));
            Assert.Equal("#ffffff", Accents.Recolor("#ffffff", accent));
            Assert.Equal("#808080", Accents.Recolor("#808080", accent));
            Assert.Equal("#37474f", Accents.Recolor("#37474f", accent)); // muted blue-grey of housings stays
            Assert.Equal("none", Accents.Recolor("none", accent));
            // A pale blue fill becomes a pale red, a strong blue line a strong red.
            var pale = Accents.Recolor("#bbdefb", accent)!;
            var strong = Accents.Recolor("#1565c0", accent)!;
            int R(string c) => System.Convert.ToInt32(c.Substring(1, 2), 16);
            int B(string c) => System.Convert.ToInt32(c.Substring(5, 2), 16);
            Assert.True(R(pale) > B(pale) && R(pale) > 200, pale);
            Assert.True(R(strong) > B(strong) && R(strong) < 200, strong);
            Assert.Equal("#1565c0", Accents.Normalize("#1565C0"));
            Assert.Null(Accents.Normalize("blue-ish"));
        }

        [Fact]
        public void ElementsAndDrawPlansTakeTheAccent()
        {
            // An energy cable is coloured by its voltage level (blue for low voltage).
            var cable = Reg.Create(Reg.Library.First(i => i.TypeId == "energy.line.cable"), new Vec2(50, 50)) with { Points = new List<Vec2> { new(20, 50), new(80, 50) } };
            var doc = SketchDocument.Create();
            doc = Edits.Add(doc, doc.Pages[0].Id, cable);
            string Stroke(SketchDocument d) => Reg.Render(cable, new RenderContext(d)).OfType<PathPrim>().Select(p => p.Style.Stroke).First(s => s is not null && s != "#000000")!;
            var own = Stroke(doc);
            var red = doc with { Settings = doc.Settings with { Accent = "#c62828" } };
            Assert.NotEqual(own, Stroke(red));
            Assert.Equal(Accents.Recolor(own, "#c62828"), Stroke(red));
            // The native draw plan follows too (the one rendering path).
            static IEnumerable<PlanOp> All(IEnumerable<PlanOp> ops) => ops.SelectMany(o => new[] { o }.Concat(All(o.Children ?? new List<PlanOp>())));
            var plan = DrawPlanBuilder.Build(new EditorSession("t", Reg, red, Lang.En), new[] { cable.Id }, DrawCapabilities.Full).Units.Single();
            Assert.Contains(All(plan.Ops), o => o.Stroke?.Color == Accents.Recolor(own, "#c62828"));
        }

        [Fact]
        public void TheEngineSetsTheAccentAndAdoptsItFromTheSlideModel()
        {
            var (api, session) = EngineApiTests.NewSession();
            var item = EngineApiTests.Insert(api, session, api.Registry.Library.First().Id, 60, 60);
            var st = EngineApiTests.Call(api, "setAccent", new { session, text = "teal" });
            Assert.Equal("#00838f", st.GetProperty("accent").GetString());
            Assert.Contains(st.GetProperty("changed").EnumerateArray(), x => x.GetString() == item);
            st = EngineApiTests.Call(api, "setAccent", new { session, text = "#123ABC" });
            Assert.Equal("#123abc", api.Sessions.Get(session).Doc.Settings.Accent);
            EngineApiTests.Call(api, "setAccent", new { session, text = "" });
            Assert.Null(api.Sessions.Get(session).Doc.Settings.Accent);
            Assert.ThrowsAny<System.Exception>(() => EngineApiTests.Call(api, "setAccent", new { session, text = "rainbow" }));
            Assert.Equal(Accents.Presets.Count, EngineApiTests.Call(api, "accents", new { session }).GetArrayLength());

            var src = SketchDocument.Create() with { Settings = new DocumentSettings { Accent = "#2e7d32" } };
            EngineApiTests.Call(api, "importCompact", new { session, payload = EngineApi.CompactOf(src, src.Pages[0], System.Array.Empty<string>()) });
            Assert.Equal("#2e7d32", api.Sessions.Get(session).Doc.Settings.Accent);
        }
    }
}
