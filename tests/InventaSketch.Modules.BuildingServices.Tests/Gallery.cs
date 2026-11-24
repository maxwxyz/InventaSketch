using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.BuildingServices.Tests
{
    /// <summary>
    /// Test-only helper that writes an SVG gallery (every type in its three views, stretched variants, library presets and a
    /// sample installation in plan, section and schematic) to the test output folder ("gallery/") for visual review.
    /// Ports are marked with small red circles, mounts with blue squares.
    /// </summary>
    public class GalleryTests
    {
        private const double PxPerMm = 40;

        private static string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);

        private static string Cell(Registry r, SketchObject o, string label, Lang lang = Lang.En)
        {
            var type = r.TypeOf(o);
            var ctx = new RenderContext(SketchDocument.Create(), lang);
            var prims = type.Render(o, ctx).ToList();
            Rect vb;
            if (o.IsPointGeometry)
            {
                vb = (Prim.Bounds(prims) ?? new Rect(0, 0, 1, 1)).Inflate(3);
            }
            else
            {
                double w = o.Size.W, h = o.Size.H;
                prims.Insert(0, Prim.Rect(-w / 2, -h / 2, w, h, new Style { Stroke = "#c8d6e5", StrokeWidth = 0.08, Dash = DashStyle.Dash }));
                var all = Prim.Bounds(prims) ?? new Rect(-w / 2, -h / 2, w, h);
                vb = all.Union(new Rect(-w / 2, -h / 2, w, h)).Inflate(1);
            }
            foreach (var a in type.Anchors(o, ctx))
            {
                if (a.Role == AnchorRole.Port) prims.Add(Prim.Circle(a.P.X, a.P.Y, 0.25, new Style { Stroke = "#e03030", StrokeWidth = 0.08 }));
                else if (a.Role == AnchorRole.Mount) prims.Add(Prim.CRect(a.P.X, a.P.Y, 0.4, 0.4, new Style { Stroke = "#3050e0", StrokeWidth = 0.08 }));
                else if (a.Role == AnchorRole.Rail) prims.Add(Prim.Line(a.P, a.Q, new Style { Stroke = "#30a0e0", StrokeWidth = 0.08, Dash = DashStyle.Dot }));
            }
            var px = Math.Max(4, Math.Min(PxPerMm, 260 / Math.Max(vb.W, vb.H)));
            var svg = SvgRenderer.RenderPrimitives(prims, new SvgOptions { Background = null, ViewBox = vb });
            svg = svg.Replace("<svg ", $"<svg width=\"{F(vb.W * px)}\" height=\"{F(vb.H * px)}\" ");
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
                    foreach (var v in t.Views)
                    {
                        var o = r.WithView(baseObj, v.Id, new RenderContext(SketchDocument.Create()));
                        if (o.IsPointGeometry) o = o with { Points = new[] { new Vec2(-15, 0), new Vec2(0, 0), new Vec2(0, -10), new Vec2(15, -10) } };
                        body.Append(Cell(r, o, v.Id));
                        if (!o.IsPointGeometry) body.Append(Cell(r, o with { Size = new Size2(o.Size.W * 1.6, o.Size.H * 0.7) }, v.Id + " stretched 1.6 x 0.7", Lang.De));
                    }
                    body.Append("</div>\n");
                }
                body.Append("<h2>Library presets (plan)</h2><div class=\"grid\">\n");
                foreach (var item in r.Library.Where(i => i.Category == cat.Id && i.View is null))
                    body.Append(Cell(r, r.Create(item, Vec2.Zero), item.Name.En + " / " + item.Name.De));
                body.Append("</div>\n");
                var file = cat.Id.Replace("bs.", "") + ".html";
                File.WriteAllText(Path.Combine(dir, file), Page(cat.Name.En, body.ToString()), Encoding.UTF8);
                index.Append("<p><a href=\"").Append(file).Append("\">").Append(SvgRenderer.Esc(cat.Name.En)).Append("</a></p>\n");
            }

            // Sample installation in the three views.
            var b = ViewSwitchTests.Bathroom();
            var ids = new[] { b.Radiator, b.Basin, b.Wc, b.Riser, b.Pipe };
            foreach (var view in new[] { "plan", "section", "schematic" })
            {
                var doc = view == "plan" ? b.Doc : ServiceViews.Switch(b.R, b.Doc, ids, view);
                var vb = new Rect(30, view == "plan" ? 20 : 0, 70, view == "plan" ? 25 : 40);
                var page = SvgRenderer.RenderPage(b.R, doc, doc.Pages[0], new SvgOptions { ViewBox = vb });
                page = page.Replace($"width=\"{F(vb.W)}mm\" height=\"{F(vb.H)}mm\"", $"width=\"{F(vb.W * 10)}\" height=\"{F(vb.H * 10)}\"");
                index.Append("<h2>Sample bathroom: ").Append(view).Append("</h2>").Append(page);
            }
            File.WriteAllText(Path.Combine(dir, "index.html"), Page("InventaSketch building services gallery", index.ToString()), Encoding.UTF8);
            Assert.True(File.Exists(Path.Combine(dir, "index.html")));
        }
    }
}
