using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Charts.Tests
{
    public class ActionTests
    {
        private static ContextAction Act(Registry r, SketchObject o, string id, Lang lang = Lang.En)
            => r.TypeOf(o).Actions(o, new RenderContext(SketchDocument.Create(), lang)).Single(a => a.Id == id);

        [Fact]
        public void EveryActionAppliesAndTheResultRenders()
        {
            var r = Fx.Reg();
            foreach (var item in r.Library)
            {
                var o = Fx.Item(r, item.Id);
                foreach (var a in r.TypeOf(o).Actions(o, Fx.Ctx()))
                {
                    var next = a.Input is not null ? a.Input.Apply(o, a.Input.Value) : a.Apply(o);
                    Assert.Equal(o.Size, next.Size);
                    Fx.AssertInsideBox(Fx.Render(r, next), next.Size, item.Id + " after " + a.Id);
                }
            }
        }

        [Fact]
        public void DataInputUsesPipesForRowsAndKeepsTheData()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "chart.line");
            var a = Act(r, o, "chart.data");
            Assert.Contains(" | ", a.Input!.Value);
            Assert.Equal(o.Str("data", ""), a.Input.Apply(o, a.Input.Value).Str("data", "?"));
            var next = a.Input.Apply(o, "0;1 | 1;2 || 0;2 | 1;3");
            Assert.Equal("0;1\n1;2\n\n0;2\n1;3", next.Str("data", ""));
            Assert.Equal(2, ChartData.Xy(ChartData.Parse(next.Str("data", ""))).Count);
        }

        [Fact]
        public void CyclesAndToggles()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "chart.line");
            var styles = new List<string>();
            for (var i = 0; i < 4; i++) { o = Act(r, o, "chart.style").Apply(o); styles.Add(o.Str("style", "")); }
            Assert.Equal(new[] { "linepoints", "points", "step", "line" }, styles);
            var grid = Act(r, o, "chart.grid");
            Assert.True(grid.IsActive);
            Assert.False(grid.Apply(o).Flag("grid", true));
            Assert.Equal("Style: line with markers".ToLowerInvariant(), Act(r, o, "chart.style").Label.En.ToLowerInvariant());
            Assert.StartsWith("Darstellung: ", Act(r, o, "chart.style").Label.De);
            var bar = Fx.Item(r, "chart.column");
            Assert.Equal("bar", Act(r, bar, "chart.orient").Apply(bar).Str("orient", ""));
        }

        [Theory]
        [InlineData("1 kHz", 1000)]
        [InlineData("250 Hz", 250)]
        [InlineData("2,5k", 2500)]
        [InlineData("50", 50)]
        [InlineData("1.5M", 1.5e6)]
        public void CornerFrequencyInput(string text, double expected)
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "chart.bode");
            Assert.Equal(expected, Act(r, o, "chart.fc").Input!.Apply(o, text).Num("fc", 0), 6);
        }

        [Theory]
        [InlineData("chart.bode", "chart.fc", "abc")]
        [InlineData("chart.bode", "chart.fc", "-5 Hz")]
        [InlineData("chart.efficiency", "chart.levels", "x;y")]
        [InlineData("chart.pump", "chart.qop", "viel")]
        [InlineData("chart.band", "chart.tol", "?")]
        public void InvalidInputsThrowBilingualFormatExceptions(string item, string action, string text)
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, item);
            Assert.Throws<FormatException>(() => Act(r, o, action).Input!.Apply(o, text));
            var de = Assert.Throws<FormatException>(() => Act(r, o, action, Lang.De).Input!.Apply(o, text));
            Assert.Contains("gültig", de.Message);
        }

        [Fact]
        public void ToleranceInputSwitchesBetweenPercentAndAbsolute()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "chart.band");
            var a = Act(r, o, "chart.tol");
            Assert.Equal("10 %", a.Input!.Value);
            var abs = a.Input.Apply(o, "0,5");
            Assert.Equal(0.5, abs.Num("tol", 0));
            Assert.Equal("absolute", abs.Str("tolmode", ""));
            var pct = a.Input.Apply(o, "15 %");
            Assert.Equal(15, pct.Num("tol", 0));
            Assert.Equal("percent", pct.Str("tolmode", ""));
        }

        [Fact]
        public void ActionsRunThroughTheCoreDispatcher()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "chart.pump", new Vec2(100, 80));
            var doc = SketchDocument.Create();
            doc = Edits.Add(doc, doc.Pages[0].Id, o);
            doc = ContextActions.Run(r, doc, new[] { o.Id }, "chart.qop", "55", Lang.En);
            Assert.Equal(55, doc.Get(o.Id)!.Num("qop", 0));
            doc = ContextActions.Run(r, doc, new[] { o.Id }, "chart.kind", null, Lang.En);
            Assert.Equal("fan", doc.Get(o.Id)!.Str("kind", ""));
        }
    }

    public class DocumentTests
    {
        [Fact]
        public void JsonRoundTripKeepsEveryChart()
        {
            var r = Fx.Reg();
            var objects = r.Library.Select((item, i) => Fx.Item(r, item.Id, new Vec2(60 + (i % 3) * 110, 40 + (i / 3) * 70))).ToArray();
            var doc = SketchDocument.Create(title: "Charts");
            doc = Edits.Add(doc, doc.Pages[0].Id, objects);
            var json = DocumentJson.Serialize(doc, indented: true);
            var back = DocumentJson.Deserialize(json);
            Assert.Equal(DocumentJson.Serialize(doc), DocumentJson.Serialize(back));
            var ctx = new RenderContext(back);
            foreach (var o in objects)
            {
                var a = r.Render(o, new RenderContext(doc));
                var b = r.Render(back.Get(o.Id)!, ctx);
                Assert.Equal(Svg(a), Svg(b));
            }
            // The multi-line data parameter survives (line breaks included).
            var multi = back.Objects.Values.First(o => o.Str("data", "").StartsWith("x;A;B;C", StringComparison.Ordinal));
            Assert.Contains("\n", multi.Str("data", ""));
        }

        private static string Svg(IReadOnlyList<Primitive> prims) => SvgRenderer.RenderPrimitives(prims, new SvgOptions { Background = null });

        [Fact]
        public void MonochromePageExportKeepsTheChartsReadable()
        {
            var r = Fx.Reg();
            var doc = SketchDocument.Create();
            doc = Edits.Add(doc, doc.Pages[0].Id, Fx.Item(r, "chart.column.stacked", new Vec2(80, 60)), Fx.Item(r, "chart.line.multi", new Vec2(220, 60)));
            var svg = SvgRenderer.RenderPage(r, doc, doc.Pages[0], new SvgOptions { Monochrome = true });
            Assert.DoesNotContain("#1f4e9a", svg);
            Assert.Contains("stroke-dasharray", svg);
        }
    }

    /// <summary>
    /// Test-only helper that writes an SVG gallery of every chart (library presets, stretched, monochrome and German) to
    /// the test output folder ("gallery-charts/") for visual review.
    /// </summary>
    public class GalleryTests
    {
        private static string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);

        private static string Cell(Registry r, SketchObject o, string label, RenderContext ctx)
        {
            var prims = r.TypeOf(o).Render(o, ctx).ToList();
            double w = o.Size.W, h = o.Size.H;
            prims.Insert(0, Prim.Rect(-w / 2, -h / 2, w, h, new Style { Stroke = "#c8d6e5", StrokeWidth = 0.1, Dash = DashStyle.Dash }));
            var vb = new Rect(-w / 2 - 2, -h / 2 - 2, w + 4, h + 4);
            const double scale = 4;
            var svg = SvgRenderer.RenderPrimitives(prims, new SvgOptions { Background = null, ViewBox = vb, Monochrome = ctx.Monochrome });
            svg = svg.Replace("<svg ", $"<svg width=\"{F(vb.W * scale)}\" height=\"{F(vb.H * scale)}\" ");
            return $"<figure>{svg}<figcaption>{SvgRenderer.Esc(label)}</figcaption></figure>\n";
        }

        [Fact]
        public void WriteGallery()
        {
            var r = Fx.Reg();
            var dir = Path.Combine(AppContext.BaseDirectory, "gallery-charts");
            Directory.CreateDirectory(dir);
            var all = new StringBuilder();
            foreach (var cat in r.Categories.OrderBy(c => c.Order))
            {
                var body = new StringBuilder();
                body.Append("<h2>").Append(SvgRenderer.Esc(cat.Name.En + " / " + cat.Name.De)).Append("</h2><div class=\"grid\">\n");
                foreach (var item in r.Library.Where(i => i.Category == cat.Id))
                {
                    var o = Fx.Item(r, item.Id);
                    body.Append(Cell(r, o, item.Name.En + " [" + item.Id + "]", Fx.Ctx()));
                    body.Append(Cell(r, o, item.Name.De + " (DE, mono)", Fx.Ctx(Lang.De, mono: true)));
                    body.Append(Cell(r, o with { Size = new Size2(o.Size.W * 1.6, o.Size.H * 0.7) }, "stretched 1.6 x 0.7", Fx.Ctx()));
                }
                body.Append("</div>\n");
                File.WriteAllText(Path.Combine(dir, cat.Id.Replace("chart.", "") + ".html"), Page(body.ToString()), Encoding.UTF8);
                all.Append(body);
            }
            File.WriteAllText(Path.Combine(dir, "index.html"), Page(all.ToString()), Encoding.UTF8);
            Assert.True(File.Exists(Path.Combine(dir, "index.html")));
        }

        private static string Page(string body)
        {
            return "<!DOCTYPE html><html><head><meta charset=\"utf-8\"><title>InventaSketch charts gallery</title><style>" +
                "body{font-family:Arial,sans-serif;margin:12px;background:#fff}h2{font-size:14px;margin:12px 0 4px}" +
                ".grid{display:flex;flex-wrap:wrap;gap:10px;align-items:flex-end}figure{margin:0;padding:4px;border:1px solid #eee}" +
                "figcaption{font-size:10px;color:#444;max-width:420px}</style></head><body><h1>InventaSketch charts gallery</h1>\n" + body + "</body></html>\n";
        }
    }
}
