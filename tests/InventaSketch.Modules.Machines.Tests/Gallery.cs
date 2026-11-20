using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Machines.Tests
{
    /// <summary>
    /// Test-only helper that writes an SVG gallery (all types x all views, rotor variants, library presets and an e-drive page)
    /// to the test output folder ("gallery/"), one HTML page per category plus an index page, for visual review
    /// (e.g. headless browser screenshots).
    /// </summary>
    public class GalleryTests
    {
        private const double PxPerMm = 4;

        private static string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);

        private static string Cell(Registry r, SketchObject o, string label, bool mono = false)
        {
            var type = r.TypeOf(o);
            var prims = type.Render(o, new RenderContext(SketchDocument.Create())).ToList();
            double w = o.Size.W, h = o.Size.H;
            prims.Insert(0, Prim.Rect(-w / 2, -h / 2, w, h, new Style { Stroke = "#c8d6e5", StrokeWidth = 0.15, Dash = DashStyle.Dash }));
            var vb = new Rect(-w / 2 - 2, -h / 2 - 2, w + 4, h + 4);
            var svg = SvgRenderer.RenderPrimitives(prims, new SvgOptions { Background = null, ViewBox = vb, Monochrome = mono });
            svg = svg.Replace("<svg ", $"<svg width=\"{F(vb.W * PxPerMm)}\" height=\"{F(vb.H * PxPerMm)}\" ");
            return $"<figure>{svg}<figcaption>{SvgRenderer.Esc(label)}</figcaption></figure>\n";
        }

        private static string Page(string title, string body) =>
            "<!DOCTYPE html><html><head><meta charset=\"utf-8\"><title>" + SvgRenderer.Esc(title) + "</title><style>" +
            "body{font-family:Arial,sans-serif;margin:12px;background:#fff}h1{font-size:18px}h2{font-size:15px;margin:14px 0 4px}" +
            ".grid{display:flex;flex-wrap:wrap;gap:10px;align-items:flex-end}" +
            "figure{margin:0;padding:4px;border:1px solid #eee}figcaption{font-size:10px;color:#444;max-width:260px}" +
            "</style></head><body><h1>" + SvgRenderer.Esc(title) + "</h1>\n" + body + "</body></html>\n";

        private static SketchObject SwitchTo(ObjectType t, SketchObject o, string view)
            => t.Actions(o, Lang.En).FirstOrDefault(a => a.Id == "view." + view)?.Apply(o) ?? o with { View = view };

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
                foreach (var t in r.Types.Where(t => t.Category == cat.Id))
                {
                    body.Append("<h2>").Append(SvgRenderer.Esc($"{t.Name.En} / {t.Name.De}  [{t.Id}]")).Append("</h2><div class=\"grid\">\n");
                    var baseObj = t.Create(Vec2.Zero);
                    foreach (var v in t.Views) body.Append(Cell(r, SwitchTo(t, baseObj, v.Id), v.Id + " (" + v.Icon + ")"));
                    var stretched = baseObj with { Size = new Size2(baseObj.Size.W * 1.8, baseObj.Size.H * 0.7) };
                    body.Append(Cell(r, stretched, "stretched 1.8 x 0.7"));
                    body.Append(Cell(r, baseObj, "monochrome", mono: true));
                    body.Append("</div>\n");
                }
                body.Append("<h2>Library presets</h2><div class=\"grid\">\n");
                foreach (var item in r.Library.Where(i => i.Category == cat.Id))
                    body.Append(Cell(r, r.Create(item, Vec2.Zero), item.Name.En));
                body.Append("</div>\n");
                var file = cat.Id.Replace("machines.", "") + ".html";
                File.WriteAllText(Path.Combine(dir, file), Page(cat.Name.En, body.ToString()), Encoding.UTF8);
                index.Append("<p><a href=\"").Append(file).Append("\">").Append(SvgRenderer.Esc(cat.Name.En)).Append("</a></p>\n");
            }

            // rotor variants x layouts, cross-section and longitudinal section, with current directions
            var variants = new StringBuilder();
            var radial = r.Type("machines.radial")!;
            foreach (var layout in new[] { "inner", "outer" })
            {
                variants.Append("<h2>").Append(layout).Append(" rotor</h2><div class=\"grid\">\n");
                foreach (var rotor in new[] { "spm", "ipm", "spoke", "eesm", "asm", "srm" })
                {
                    var o = radial.Create(Vec2.Zero).WithParam("rotor", rotor).WithParam("layout", layout).WithParam("currents", true).WithParam("cooling", layout == "outer");
                    if (rotor == "srm") o = o.WithParam("slots", 12);
                    if (layout == "outer") o = o.WithParam("slots", 24).WithParam("poles", rotor == "srm" ? 8 : 20);
                    o = o with { Size = new Size2(90, 90) };
                    variants.Append(Cell(r, o, rotor + " / axial"));
                    variants.Append(Cell(r, SwitchTo(radial, o, "side"), rotor + " / side"));
                }
                variants.Append("</div>\n");
            }
            File.WriteAllText(Path.Combine(dir, "variants.html"), Page("Radial machine variants", variants.ToString()), Encoding.UTF8);
            index.Append("<p><a href=\"variants.html\">Radial machine variants</a></p>\n");

            var (er, doc) = DocumentTests.EDrive();
            var page = SvgRenderer.RenderPage(er, doc, doc.Pages[0]);
            page = page.Replace("width=\"338.667mm\" height=\"190.5mm\"", "width=\"1354\" height=\"762\"");
            index.Append("<h2>E-drive example</h2>").Append(page);
            File.WriteAllText(Path.Combine(dir, "index.html"), Page("InventaSketch machines gallery", index.ToString()), Encoding.UTF8);
            Assert.True(File.Exists(Path.Combine(dir, "index.html")));
        }
    }
}
