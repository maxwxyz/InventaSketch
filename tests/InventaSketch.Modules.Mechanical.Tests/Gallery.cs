using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Mechanical.Tests
{
    /// <summary>
    /// Test-only helper that writes an SVG gallery (all library items x all views, plus stretched variants and a
    /// gearbox page) to the test output folder ("gallery/"), one HTML page per category plus an index page.
    /// Used for visual review of the symbols (e.g. headless browser screenshots).
    /// </summary>
    public class GalleryTests
    {
        private const double PxPerMm = 4;

        private static string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);

        private static string Cell(Registry r, SketchObject o, string label)
        {
            var type = r.TypeOf(o);
            var prims = type.Render(o, new RenderContext(SketchDocument.Create())).ToList();
            double w = o.Size.W, h = o.Size.H;
            // faint box outline to check that the symbol stays inside its box
            prims.Insert(0, Prim.Rect(-w / 2, -h / 2, w, h, new Style { Stroke = "#c8d6e5", StrokeWidth = 0.15, Dash = DashStyle.Dash }));
            var vb = new Rect(-w / 2 - 2, -h / 2 - 2, w + 4, h + 4);
            var svg = SvgRenderer.RenderPrimitives(prims, new SvgOptions { Background = null, ViewBox = vb });
            svg = svg.Replace("<svg ", $"<svg width=\"{F(vb.W * PxPerMm)}\" height=\"{F(vb.H * PxPerMm)}\" ");
            return $"<figure>{svg}<figcaption>{SvgRenderer.Esc(label)}</figcaption></figure>\n";
        }

        private static string Page(string title, string body) =>
            "<!DOCTYPE html><html><head><meta charset=\"utf-8\"><title>" + SvgRenderer.Esc(title) + "</title><style>" +
            "body{font-family:Arial,sans-serif;margin:12px;background:#fff}h1{font-size:18px}h2{font-size:15px;margin:14px 0 4px}" +
            ".grid{display:flex;flex-wrap:wrap;gap:10px;align-items:flex-end}" +
            "figure{margin:0;padding:4px;border:1px solid #eee}figcaption{font-size:10px;color:#444;max-width:220px}" +
            "</style></head><body><h1>" + SvgRenderer.Esc(title) + "</h1>\n" + body + "</body></html>\n";

        [Fact]
        public void WriteGallery()
        {
            var r = Fx.Reg();
            var dir = Path.Combine(AppContext.BaseDirectory, "gallery");
            Directory.CreateDirectory(dir);
            var index = new StringBuilder();
            foreach (var cat in r.Categories.OrderBy(c => c.Order))
            {
                var body = new StringBuilder();
                // every type of the category: all views plus a stretched variant
                foreach (var t in r.Types.Where(t => t.Category == cat.Id))
                {
                    body.Append("<h2>").Append(SvgRenderer.Esc($"{t.Name.En} / {t.Name.De}  [{t.Id}]")).Append("</h2><div class=\"grid\">\n");
                    var baseObj = t.Create(Vec2.Zero);
                    foreach (var v in t.Views)
                    {
                        var o = baseObj;
                        if (o.View != v.Id)
                        {
                            var act = t.Actions(o, Lang.En).FirstOrDefault(a => a.Id == "view." + v.Id);
                            o = act is null ? o with { View = v.Id } : act.Apply(o);
                        }
                        body.Append(Cell(r, o, v.Id));
                    }
                    var stretched = baseObj with { Size = new Size2(baseObj.Size.W * 1.8, baseObj.Size.H * 0.7) };
                    body.Append(Cell(r, stretched, "stretched 1.8 x 0.7"));
                    body.Append("</div>\n");
                }
                // library presets as inserted
                body.Append("<h2>Library presets</h2><div class=\"grid\">\n");
                foreach (var item in r.Library.Where(i => i.Category == cat.Id))
                    body.Append(Cell(r, r.Create(item, Vec2.Zero), item.Name.En));
                body.Append("</div>\n");
                var file = cat.Id.Replace("mech.", "") + ".html";
                File.WriteAllText(Path.Combine(dir, file), Page(cat.Name.En, body.ToString()), Encoding.UTF8);
                index.Append("<p><a href=\"").Append(file).Append("\">").Append(SvgRenderer.Esc(cat.Name.En)).Append("</a></p>\n");
            }

            // gearbox example page
            var (gr, doc) = GearboxDocumentTests.Gearbox();
            var page = SvgRenderer.RenderPage(gr, doc, doc.Pages[0]);
            page = page.Replace("width=\"338.667mm\" height=\"190.5mm\"", "width=\"1354\" height=\"762\"");
            index.Append("<h2>Gearbox example</h2>").Append(page);
            File.WriteAllText(Path.Combine(dir, "index.html"), Page("InventaSketch mechanical gallery", index.ToString()), Encoding.UTF8);
            Assert.True(File.Exists(Path.Combine(dir, "index.html")));
        }
    }
}
