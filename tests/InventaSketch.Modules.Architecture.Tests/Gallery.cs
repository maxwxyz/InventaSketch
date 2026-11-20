using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Architecture.Tests
{
    /// <summary>
    /// Test-only helper that writes an SVG gallery (every type in every view, stretched variants, library presets and
    /// a sample house) to the test output folder ("gallery/"). Used for visual review (headless browser screenshots).
    /// </summary>
    public class GalleryTests
    {
        private const double PxPerMm = 6;

        private static string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);

        private static string Cell(Registry r, SketchObject o, string label, Lang lang = Lang.En)
        {
            var type = r.TypeOf(o);
            var prims = type.Render(o, new RenderContext(SketchDocument.Create(), lang)).ToList();
            Rect vb;
            if (o.IsPointGeometry)
                vb = (Prim.Bounds(prims) ?? new Rect(0, 0, 10, 10)).Inflate(2);
            else
            {
                double w = o.Size.W, h = o.Size.H;
                // faint box outline to check that the symbol stays inside its box
                prims.Insert(0, Prim.Rect(-w / 2, -h / 2, w, h, new Style { Stroke = "#c8d6e5", StrokeWidth = 0.1, Dash = DashStyle.Dash }));
                vb = new Rect(-w / 2 - 1, -h / 2 - 1, w + 2, h + 2);
            }
            var scale = Math.Min(PxPerMm, 420 / Math.Max(vb.W, vb.H));
            var svg = SvgRenderer.RenderPrimitives(prims, new SvgOptions { Background = null, ViewBox = vb });
            svg = svg.Replace("<svg ", $"<svg width=\"{F(vb.W * scale)}\" height=\"{F(vb.H * scale)}\" ");
            return $"<figure>{svg}<figcaption>{SvgRenderer.Esc(label)}</figcaption></figure>\n";
        }

        private static string Page(string title, string body) =>
            "<!DOCTYPE html><html><head><meta charset=\"utf-8\"><title>" + SvgRenderer.Esc(title) + "</title><style>" +
            "body{font-family:Arial,sans-serif;margin:12px;background:#fff}h1{font-size:18px}h2{font-size:14px;margin:12px 0 4px}" +
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
                    foreach (var v in t.Views) body.Append(Cell(r, baseObj with { View = v.Id }, v.Id));
                    if (t.Geometry == GeometryKind.Box)
                    {
                        body.Append(Cell(r, baseObj with { Size = new Size2(baseObj.Size.W * 1.6, baseObj.Size.H * 0.7) }, "stretched 1.6 x 0.7"));
                        body.Append(Cell(r, baseObj, "German", Lang.De));
                    }
                    body.Append("</div>\n");
                }
                body.Append("<h2>Library presets</h2><div class=\"grid\">\n");
                foreach (var item in r.Library.Where(i => i.Category == cat.Id))
                    body.Append(Cell(r, r.Create(item, Vec2.Zero), item.Name.En));
                body.Append("</div>\n");
                var file = cat.Id.Replace("arch.", "") + ".html";
                File.WriteAllText(Path.Combine(dir, file), Page(cat.Name.En, body.ToString()), Encoding.UTF8);
                index.Append("<p><a href=\"").Append(file).Append("\">").Append(SvgRenderer.Esc(cat.Name.En)).Append("</a></p>\n");
            }

            // Joint study: L, T, cross and closed loops with mixed kinds.
            var jr = Fx.Reg();
            var joints = SketchDocument.Create(new Size2(200, 80), "Wall joints");
            SketchObject W(string item, params Vec2[] pts) => jr.Create(jr.Item(item)!, Vec2.Zero) with { Points = pts };
            joints = Edits.Add(joints, joints.Pages[0].Id,
                W("arch.wall.exterior", new Vec2(10, 10), new Vec2(50, 10)), W("arch.wall.interior", new Vec2(50, 10), new Vec2(50, 60)),
                W("arch.wall.exterior", new Vec2(70, 10), new Vec2(120, 10)), W("arch.wall.interior", new Vec2(95, 10), new Vec2(95, 60)),
                W("arch.wall.partition", new Vec2(95, 35), new Vec2(120, 35)), W("arch.wall.partition", new Vec2(70, 35), new Vec2(95, 35)),
                W("arch.wall.exterior", new Vec2(140, 10), new Vec2(190, 10), new Vec2(165, 60)) with { Params = new Dictionary<string, ParamValue> { ["kind"] = "exterior", ["thickness"] = 365.0, ["closed"] = true } });
            var jsvg = SvgRenderer.RenderPage(jr, joints, joints.Pages[0]).Replace("width=\"200mm\" height=\"80mm\"", "width=\"1200\" height=\"480\"");
            index.Append("<h2>Wall joints (L, T, cross, closed)</h2>").Append(jsvg);

            var (hr, house) = FloorPlan.Build();
            foreach (var lang in new[] { Lang.En, Lang.De })
            {
                var page = SvgRenderer.RenderPage(hr, house, house.Pages[0], lang: lang);
                page = page.Replace("width=\"338.667mm\" height=\"190.5mm\"", "width=\"1600\" height=\"900\"");
                index.Append("<h2>Sample house (" + lang + ")</h2>").Append(page);
            }
            File.WriteAllText(Path.Combine(dir, "index.html"), Page("InventaSketch architecture gallery", index.ToString()), Encoding.UTF8);
            Assert.True(File.Exists(Path.Combine(dir, "index.html")));
        }
    }
}
