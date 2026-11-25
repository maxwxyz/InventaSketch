using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Engine.Tests
{
    /// <summary>LaTeX (TikZ) export: texts stay editable LaTeX text, every element exports to balanced TikZ code.</summary>
    public class TikzExportTests
    {
        private static readonly Registry Reg = ModuleCatalog.CreateDefault();

        /// <summary>Braces balanced (escaped ones not counted), scopes paired, no invalid numbers.</summary>
        private static void AssertWellFormed(string tex, string what)
        {
            var depth = 0;
            for (var i = 0; i < tex.Length; i++)
            {
                if (tex[i] == '\\' && i + 1 < tex.Length && (tex[i + 1] == '{' || tex[i + 1] == '}' || tex[i + 1] == '\\' || tex[i + 1] == '%')) { i++; continue; }
                if (tex[i] == '%') { while (i < tex.Length && tex[i] != '\n') i++; continue; } // comments
                if (tex[i] == '{') depth++;
                if (tex[i] == '}') depth--;
                Assert.True(depth >= 0, what + ": closing brace without opening one");
            }
            Assert.True(depth == 0, what + ": unbalanced braces");
            Assert.Equal(Regex.Matches(tex, @"\\begin\{scope\}").Count, Regex.Matches(tex, @"\\end\{scope\}").Count);
            Assert.DoesNotContain("NaN", tex);
            Assert.DoesNotContain("Infinity", tex);
            Assert.Contains("\\begin{tikzpicture}[x=1mm, y=-1mm", tex);
            Assert.EndsWith("\\end{document}\n", tex);
        }

        [Fact]
        public void AFlowchartBecomesTikzWithEditableTexts()
        {
            var doc = SketchDocument.Create();
            var step = Reg.Create(Reg.Item("ann.flow.process")!, new Vec2(60, 40)) with { Text = "Heizleistung zuweisen & prüfen (100 % / Δt_1)" };
            var start = Reg.Create(Reg.Item("ann.flow.start")!, new Vec2(60, 10));
            doc = Edits.Add(doc, doc.Pages[0].Id, start, step);
            var tex = new TikzExporter().Export(Reg, doc, new[] { start, step }, Lang.De);
            AssertWellFormed(tex, "flowchart");
            Assert.Contains("{Heizleistung zuweisen \\& prüfen (100 \\% / $\\Delta$t\\_1)}", tex.Replace("\\\\ ", " "));
            Assert.Contains("{Start}", tex);
            Assert.Equal(2, Regex.Matches(tex, @"% .*\[o").Count);       // one commented scope per element
            Assert.Contains("\\fontsize{", tex);                         // set in the document font, size kept
        }

        [Fact]
        public void HatchesColoursAndDashesBecomeTikzOptions()
        {
            var doc = SketchDocument.Create();
            var hatch = Reg.Create(Reg.Item("ann.hatch.rect")!, new Vec2(40, 40));
            var red = Reg.Create(Reg.Item("ann.arrow")!, new Vec2(40, 80)) with { Style = new Style { Stroke = "#c00000", Dash = DashStyle.Dash } };
            doc = Edits.Add(doc, doc.Pages[0].Id, hatch, red);
            var tex = new TikzExporter().Export(Reg, doc, new[] { hatch, red }, Lang.En);
            AssertWellFormed(tex, "hatch");
            Assert.Contains("pattern={Lines[angle=", tex);
            Assert.Contains("\\definecolor{iskC00000}{HTML}{C00000}", tex);
            Assert.Contains("draw=iskC00000", tex);
            Assert.Contains("dash pattern=on ", tex);
        }

        [Fact]
        public void EveryLibraryElementExportsWellFormedTikz()
        {
            var failures = new List<string>();
            foreach (var item in Reg.Library)
            {
                var doc = SketchDocument.Create();
                var o = Reg.Create(item, new Vec2(100, 100));
                doc = Edits.Add(doc, doc.Pages[0].Id, o);
                try
                {
                    var tex = new TikzExporter().Export(Reg, doc, new[] { doc.Get(o.Id)! }, Lang.De);
                    AssertWellFormed(tex, item.Id);
                }
                catch (System.Exception ex)
                {
                    failures.Add(item.Id + ": " + ex.Message.Split('\n')[0]);
                }
            }
            Assert.True(failures.Count == 0, string.Join("\n", failures.Take(20)));
        }

        [Fact]
        public void TheEditableSvgExportNamesElementsAndParts()
        {
            var api = new EngineApi(ModuleCatalog.CreateDefault());
            var session = EngineApiTests.Call(api, "createSession", new { lang = "de" }).GetProperty("session").GetString()!;
            var id = EngineApiTests.Insert(api, session, "chart.line", 100, 80);
            var svg = EngineApiTests.Call(api, "renderPageSvg", new { session, mode = "export" }).GetProperty("svg").GetString()!;
            Assert.Contains("xmlns:inkscape=", svg);
            Assert.Contains("id=\"isk-" + id + "\"", svg);
            Assert.Contains("data-part=\"series.0\"", svg);
            Assert.Contains("<text", svg);
            var xml = System.Xml.Linq.XDocument.Parse(svg);   // well-formed
            Assert.NotNull(xml.Root);
            var plain = EngineApiTests.Call(api, "renderPageSvg", new { session }).GetProperty("svg").GetString()!;
            Assert.DoesNotContain("data-part=", plain);
        }

        [Fact]
        public void TheEngineOffersTheLatexExport()
        {
            var api = new EngineApi(ModuleCatalog.CreateDefault());
            var session = EngineApiTests.Call(api, "createSession", new { lang = "en" }).GetProperty("session").GetString()!;
            var id = EngineApiTests.Insert(api, session, "chart.line", 100, 80);
            var list = EngineApiTests.Call(api, "exporters", new { session, ids = new[] { id } }).EnumerateArray().Select(e => e.GetProperty("id").GetString()).ToList();
            Assert.Contains("latex.tikz", list);
            var result = EngineApiTests.Call(api, "export", new { session, ids = new[] { id }, itemId = "latex.tikz" });
            Assert.Equal("tex", result.GetProperty("extension").GetString());
            AssertWellFormed(result.GetProperty("text").GetString()!, "chart");
        }
    }
}
