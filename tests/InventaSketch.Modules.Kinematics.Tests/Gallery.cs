using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Kinematics.Tests
{
    /// <summary>
    /// Test-only helper that writes an SVG gallery (all library items x all views, stretched variants, mechanism angle
    /// sweeps and a sample page) to the test output folder ("gallery/") for visual review, e.g. headless browser screenshots.
    /// </summary>
    public class GalleryTests
    {
        private const double PxPerMm = 4;

        private static string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);

        private static string Cell(Registry r, SketchObject o, string label)
        {
            var doc = SketchDocument.Create();
            doc = Edits.Add(doc, doc.Pages[0].Id, o);
            var prims = r.TypeOf(o).Render(o, new RenderContext(doc)).ToList();
            Rect vb;
            if (o.IsPointGeometry)
            {
                vb = Rect.FromPoints(o.Points!).Inflate(5);
            }
            else
            {
                double w = o.Size.W, h = o.Size.H;
                prims.Insert(0, Prim.Rect(-w / 2, -h / 2, w, h, new Style { Stroke = "#c8d6e5", StrokeWidth = 0.15, Dash = DashStyle.Dash }));
                vb = new Rect(-w / 2 - 2, -h / 2 - 2, w + 4, h + 4);
            }
            var svg = SvgRenderer.RenderPrimitives(prims, new SvgOptions { Background = null, ViewBox = vb });
            svg = svg.Replace("<svg ", $"<svg width=\"{F(vb.W * PxPerMm)}\" height=\"{F(vb.H * PxPerMm)}\" ");
            return $"<figure>{svg}<figcaption>{SvgRenderer.Esc(label)}</figcaption></figure>\n";
        }

        private static string Page(string title, string body) =>
            "<!DOCTYPE html><html><head><meta charset=\"utf-8\"><title>" + SvgRenderer.Esc(title) + "</title><style>" +
            "body{font-family:Arial,sans-serif;margin:12px;background:#fff}h1{font-size:18px}h2{font-size:15px;margin:14px 0 4px}" +
            ".grid{display:flex;flex-wrap:wrap;gap:10px;align-items:flex-end}" +
            "figure{margin:0;padding:4px;border:1px solid #eee}figcaption{font-size:10px;color:#444;max-width:240px}" +
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
                foreach (var t in r.Types.Where(t => t.Category == cat.Id))
                {
                    body.Append("<h2>").Append(SvgRenderer.Esc($"{t.Name.En} / {t.Name.De}  [{t.Id}]")).Append("</h2><div class=\"grid\">\n");
                    var baseObj = t.Create(Vec2.Zero);
                    var views = t.Views.Count == 0 ? new[] { (string?)null } : t.Views.Select(v => (string?)v.Id).ToArray();
                    foreach (var v in views) body.Append(Cell(r, v is null ? baseObj : baseObj with { View = v }, v ?? "default"));
                    if (t.Geometry == GeometryKind.Box)
                        body.Append(Cell(r, baseObj with { Size = new Size2(baseObj.Size.W * 1.8, baseObj.Size.H * 0.7) }, "stretched 1.8 x 0.7"));
                    if (t.Parameters.Any(p => p.Key == "angle"))
                        foreach (var a in new[] { -150, -90, 0, 90 })
                            body.Append(Cell(r, baseObj.WithParam("angle", a), "angle " + a));
                    body.Append("</div>\n");
                }
                body.Append("<h2>Library presets</h2><div class=\"grid\">\n");
                foreach (var item in r.Library.Where(i => i.Category == cat.Id))
                    body.Append(Cell(r, r.Create(item, Vec2.Zero), item.Name.En));
                body.Append("</div>\n");
                var file = cat.Id.Replace("kin.", "") + ".html";
                File.WriteAllText(Path.Combine(dir, file), Page(cat.Name.En, body.ToString()), Encoding.UTF8);
                index.Append("<p><a href=\"").Append(file).Append("\">").Append(SvgRenderer.Esc(cat.Name.En)).Append("</a></p>\n");
            }

            var (sr, doc) = PersistenceTests.Sample();
            var page = SvgRenderer.RenderPage(sr, doc, doc.Pages[0]);
            page = page.Replace("width=\"338.667mm\" height=\"190.5mm\"", "width=\"1354\" height=\"762\"");
            index.Append("<h2>Sample page</h2>").Append(page);
            File.WriteAllText(Path.Combine(dir, "index.html"), Page("InventaSketch kinematics gallery", index.ToString()), Encoding.UTF8);
            Assert.True(File.Exists(Path.Combine(dir, "index.html")));
        }
    }
}
