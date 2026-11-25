using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Electrical.Tests
{
    /// <summary>
    /// Test-only helper that writes an SVG gallery (all types x all views, stretched variants, library presets and an
    /// example circuit) to the test output folder ("gallery/"), for visual review (e.g. headless browser screenshots).
    /// Port anchors are marked with small red circles.
    /// </summary>
    public class GalleryTests
    {
        private const double PxPerMm = 4;

        private static string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);

        private static string Cell(Registry r, SketchObject o, string label)
        {
            var type = r.TypeOf(o);
            var ctx = new RenderContext(SketchDocument.Create());
            var prims = type.Render(o, ctx).ToList();
            Rect vb;
            if (o.IsPointGeometry)
            {
                var b = Prim.Bounds(prims) ?? new Rect(0, 0, 1, 1);
                vb = b.Inflate(3);
            }
            else
            {
                double w = o.Size.W, h = o.Size.H;
                prims.Insert(0, Prim.Rect(-w / 2, -h / 2, w, h, new Style { Stroke = "#c8d6e5", StrokeWidth = 0.15, Dash = DashStyle.Dash }));
                // Designator/value labels sit outside the box by design: widen the view to show them.
                var all = Prim.Bounds(prims) ?? new Rect(-w / 2, -h / 2, w, h);
                vb = all.Union(new Rect(-w / 2, -h / 2, w, h)).Inflate(2);
            }
            foreach (var a in type.Anchors(o, ctx).Where(a => a.Role == AnchorRole.Port))
                prims.Add(Prim.Circle(a.P.X, a.P.Y, 0.5, new Style { Stroke = "#e03030", StrokeWidth = 0.12 }));
            var svg = SvgRenderer.RenderPrimitives(prims, new SvgOptions { Background = null, ViewBox = vb });
            svg = svg.Replace("<svg ", $"<svg width=\"{F(vb.W * PxPerMm)}\" height=\"{F(vb.H * PxPerMm)}\" ");
            return $"<figure>{svg}<figcaption>{SvgRenderer.Esc(label)}</figcaption></figure>\n";
        }

        private static string Page(string title, string body) =>
            "<!DOCTYPE html><html><head><meta charset=\"utf-8\"><title>" + SvgRenderer.Esc(title) + "</title><style>" +
            "body{font-family:Arial,sans-serif;margin:12px;background:#fff}h1{font-size:18px}h2{font-size:14px;margin:12px 0 4px}" +
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
                    if (!baseObj.IsPointGeometry)
                    {
                        body.Append(Cell(r, baseObj with { Size = new Size2(baseObj.Size.W * 1.8, baseObj.Size.H * 0.7) }, "stretched 1.8 x 0.7"));
                        var labelled = baseObj.WithParam("ref", "X1").WithParam("value", "val");
                        body.Append(Cell(r, labelled, "with ref/value"));
                    }
                    body.Append("</div>\n");
                }
                body.Append("<h2>Library presets</h2><div class=\"grid\">\n");
                foreach (var item in r.Library.Where(i => i.Category == cat.Id))
                {
                    var o = r.Create(item, Vec2.Zero);
                    body.Append(Cell(r, o, item.Name.En));
                    var toggle = r.TypeOf(o).Actions(o, Lang.En).FirstOrDefault(a => a.Id == "elec.toggle");
                    if (toggle is not null) body.Append(Cell(r, toggle.Apply(o), item.Name.En + " (toggled)"));
                }
                body.Append("</div>\n");
                var file = cat.Id.Replace("elec.", "") + ".html";
                File.WriteAllText(Path.Combine(dir, file), Page(cat.Name.En, body.ToString()), Encoding.UTF8);
                index.Append("<p><a href=\"").Append(file).Append("\">").Append(SvgRenderer.Esc(cat.Name.En)).Append("</a></p>\n");
            }

            var (cr, doc) = PersistenceTests.Circuit();
            var page = SvgRenderer.RenderPage(cr, doc, doc.Pages[0]);
            page = page.Replace("width=\"338.667mm\" height=\"190.5mm\"", "width=\"1354\" height=\"762\"");
            index.Append("<h2>Example circuit (wires attached to pins)</h2>").Append(page);
            // Calc 3: the same circuit closed, with values derived through the wires (LED series resistor).
            var (vr, vdoc) = CircuitValueTests.ValuedCircuit();
            var valued = SvgRenderer.RenderPage(vr, vdoc, vdoc.Pages[0]).Replace("width=\"338.667mm\" height=\"190.5mm\"", "width=\"1354\" height=\"762\"");
            index.Append("<h2>Example circuit with values (switch closed, LED 2.1 V)</h2>").Append(valued);
            File.WriteAllText(Path.Combine(dir, "index.html"), Page("InventaSketch electrical gallery", index.ToString()), Encoding.UTF8);
            Assert.True(File.Exists(Path.Combine(dir, "index.html")));
        }
    }
}
