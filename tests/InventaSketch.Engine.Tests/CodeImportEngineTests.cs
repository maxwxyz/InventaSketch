using System.Linq;
using System.Text.Json;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Engine.Tests
{
    /// <summary>Engine <c>importCode</c> (roadmap 4b): matplotlib, pgfplots and TikZ code become elements in one undo step, with a report.</summary>
    public class CodeImportEngineTests
    {
        private static (EngineApi Api, string Session) NewSession(string lang = "en")
        {
            var api = new EngineApi(ModuleCatalog.CreateDefault());
            var session = EngineApiTests.Call(api, "createSession", new { lang, pageWidth = 280.0, pageHeight = 157.5 }).GetProperty("session").GetString()!;
            return (api, session);
        }

        [Fact]
        public void AMatplotlibScriptBecomesASelectedChartWithAReport()
        {
            var (api, session) = NewSession("de");
            const string code = "import matplotlib.pyplot as plt\nimport numpy as np\nx = np.linspace(0, 1, 11)\nplt.plot(x, x ** 2, label='quadratisch')\nplt.hist(x)\nplt.legend()\n";
            var res = EngineApiTests.Call(api, "importCode", new { session, text = code });
            Assert.Equal("matplotlib", res.GetProperty("format").GetString());
            var ids = res.GetProperty("ids").EnumerateArray().Select(e => e.GetString()!).ToList();
            var chart = api.Sessions.Get(session).Doc.Get(Assert.Single(ids))!;
            Assert.Equal("chart.figure", chart.Type);
            Assert.Equal(ids, res.GetProperty("state").GetProperty("selection").EnumerateArray().Select(e => e.GetString()!).ToList());
            Assert.StartsWith("Diagramm 1", res.GetProperty("imported")[0].GetString());
            var skipped = res.GetProperty("skipped");
            Assert.Equal(1, skipped.GetArrayLength());
            Assert.Equal(5, skipped[0].GetProperty("line").GetInt32());
            Assert.Contains("hist", skipped[0].GetProperty("code").GetString());
            Assert.Contains("nicht importiert", skipped[0].GetProperty("reason").GetString());
            // One undo step removes the import.
            EngineApiTests.Call(api, "undo", new { session });
            Assert.Empty(api.Sessions.Get(session).Doc.Objects);
        }

        [Fact]
        public void ATikzExportRoundTripsAsBasicShapesLinesAndTexts()
        {
            var reg = ModuleCatalog.CreateDefault();
            var doc = SketchDocument.Create();
            var rect = reg.Create(reg.Item("basic.rect")!, new Vec2(80, 60)) with { Size = new Size2(30, 20) };
            var circle = reg.Create(reg.Item("basic.ellipse")!, new Vec2(130, 60)) with { Size = new Size2(16, 16), Style = new Style { Fill = Fill.Solid("#ffcc00") } };
            var line = reg.Create(reg.Item("ann.line")!, new Vec2(0, 0)) with
            {
                Points = new[] { new Vec2(60, 90), new Vec2(150, 90) },
                Style = new Style { Stroke = "#c00000", Dash = DashStyle.Dash },
            };
            var text = reg.Create(reg.Item("ann.text")!, new Vec2(100, 40)) with { Text = "Pumpe 12" };
            doc = Edits.Add(doc, doc.Pages[0].Id, rect, circle, line, text);
            var tex = new TikzExporter().Export(reg, doc, new[] { rect, circle, line, text }, Lang.En);

            var (api, session) = NewSession();
            var res = EngineApiTests.Call(api, "importCode", new { session, text = tex, mode = "auto" });
            Assert.Equal("tikz", res.GetProperty("format").GetString());
            Assert.Equal(0, res.GetProperty("skipped").GetArrayLength());
            var objs = api.Sessions.Get(session).Doc.Objects.Values.ToList();
            Assert.Single(objs.Select(o => o.GroupId).Distinct());
            var r = Assert.Single(objs, o => o.Type == "basic.rect");
            Assert.Equal(30, r.Size.W, 2); Assert.Equal(20, r.Size.H, 2);
            var c = Assert.Single(objs, o => o.Type == "basic.ellipse");
            Assert.Equal(16, c.Size.W, 2);
            Assert.Equal("#ffcc00", c.Style!.Fill!.Color);
            // Real size: the distances between the shapes are kept.
            Assert.Equal(50, c.Transform.X - r.Transform.X, 2);
            Assert.Equal(0, c.Transform.Y - r.Transform.Y, 2);
            var l = objs.Single(o => o.Type == "ann.line" && o.Style?.Stroke == "#c00000");
            Assert.Equal(DashStyle.Dash, l.Style!.Dash);
            Assert.Equal(90, l.Points![1].X - l.Points[0].X, 2);
            Assert.Equal(30, l.Points[0].Y - r.Transform.Y, 1);
            var t = Assert.Single(objs, o => o.Type == "ann.text");
            Assert.Equal("Pumpe 12", t.Text);
        }

        [Fact]
        public void ALatexFileWithAnAxisAndADrawingGivesAChartAndAGroup()
        {
            var (api, session) = NewSession();
            const string tex = @"\documentclass{standalone}\usepackage{pgfplots}\begin{document}
\begin{tikzpicture}\begin{axis}[xlabel=t]\addplot coordinates {(0,0) (1,2) (2,1)};\end{axis}\end{tikzpicture}
\begin{tikzpicture}\draw[->] (0,0) -- (2,0) node[right] {F};\draw (0,0) circle (0.5);\end{tikzpicture}
\end{document}";
            var res = EngineApiTests.Call(api, "importCode", new { session, text = tex });
            Assert.Equal("pgfplots", res.GetProperty("format").GetString());
            var objs = api.Sessions.Get(session).Doc.Objects.Values.ToList();
            Assert.Single(objs, o => o.Type == "chart.figure");
            var arrow = Assert.Single(objs, o => o.Type == "ann.arrow");
            Assert.Equal("open", arrow.Str("headEnd", ""));
            Assert.Equal("none", arrow.Str("headStart", ""));
            Assert.Single(objs, o => o.Type == "basic.ellipse");
            Assert.Single(objs, o => o.Type == "ann.text" && o.Text == "F");
            Assert.Equal(2, res.GetProperty("imported").GetArrayLength());
        }

        [Fact]
        public void TextThatIsNoCodeImportsNothingAndSaysWhy()
        {
            var (api, session) = NewSession();
            var res = EngineApiTests.Call(api, "importCode", new { session, text = "Hello world" });
            Assert.False(res.TryGetProperty("format", out var format) && format.ValueKind != JsonValueKind.Null);
            Assert.Equal(0, res.GetProperty("ids").GetArrayLength());
            Assert.Contains("neither", res.GetProperty("message").GetString());
            Assert.Empty(api.Sessions.Get(session).Doc.Objects);
            var err = api.Call("importCode", JsonSerializer.Serialize(new { session, text = "x", mode = "excel" }));
            Assert.Contains("\"ok\":false", err);
        }
    }
}
