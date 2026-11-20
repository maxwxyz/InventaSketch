using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Fluid.Tests
{
    /// <summary>
    /// Test-only helper that writes an SVG gallery (all types x all views, stretched variants, library presets, valve
    /// combinations and the sample circuit) to the test output folder ("gallery/"), for visual review (e.g. headless
    /// browser screenshots). Ports are marked with small red circles.
    /// </summary>
    public class GalleryTests
    {
        private const double PxPerMm = 5;

        private static string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);

        private static string Cell(Registry r, SketchObject o, string label)
        {
            var type = r.TypeOf(o);
            var ctx = new RenderContext(SketchDocument.Create());
            var prims = type.Render(o, ctx).ToList();
            Rect vb;
            if (o.IsPointGeometry)
            {
                vb = (Prim.Bounds(prims) ?? new Rect(0, 0, 1, 1)).Inflate(3);
            }
            else
            {
                double w = o.Size.W, h = o.Size.H;
                prims.Insert(0, Prim.Rect(-w / 2, -h / 2, w, h, new Style { Stroke = "#c8d6e5", StrokeWidth = 0.15, Dash = DashStyle.Dash }));
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
                body.Append("<h2>Library presets</h2><div class=\"grid\">\n");
                foreach (var item in r.Library.Where(i => i.Category == cat.Id))
                    body.Append(Cell(r, r.Create(item, Vec2.Zero), item.Name.En + " / " + item.Name.De));
                body.Append("</div>\n");
                foreach (var t in r.Types.Where(t => t.Category == cat.Id && t.Geometry == GeometryKind.Box))
                {
                    body.Append("<h2>").Append(SvgRenderer.Esc($"{t.Name.En} / {t.Name.De}  [{t.Id}]")).Append("</h2><div class=\"grid\">\n");
                    var baseObj = t.Create(Vec2.Zero);
                    foreach (var v in t.Views) body.Append(Cell(r, r.WithView(baseObj, v.Id, new RenderContext(SketchDocument.Create())), v.Id));
                    body.Append(Cell(r, baseObj with { Size = new Size2(baseObj.Size.W * 1.8, baseObj.Size.H * 0.7) }, "stretched 1.8 x 0.7"));
                    var named = baseObj.WithParam("ref", "1Z1").WithParam("showPorts", true);
                    body.Append(Cell(r, named, "designator + port names"));
                    body.Append("</div>\n");
                }
                if (cat.Id == DirectionalValves.Cat)
                {
                    var t = (FluidType)r.Type(DirectionalValves.TypeId)!;
                    body.Append("<h2>Valve types x centre positions</h2><div class=\"grid\">\n");
                    foreach (var ways in DirectionalValves.WaysOptions)
                    {
                        var o = t.Create(Vec2.Zero).WithParam("ways", ways).WithParam("showPorts", true);
                        if (!ways.EndsWith("/3", StringComparison.Ordinal)) o = o.WithParam("actRight", "spring");
                        body.Append(Cell(r, o with { Size = t.Design(o) }, ways));
                        if (ways is "2/2" or "3/2")
                        {
                            var no = o.WithParam("normal", "open");
                            body.Append(Cell(r, no with { Size = t.Design(no) }, ways + " normally open"));
                        }
                    }
                    foreach (var centre in DirectionalValves.Centres4)
                    {
                        var o = t.Create(Vec2.Zero).WithParam("centre", centre);
                        body.Append(Cell(r, o with { Size = t.Design(o) }, "4/3 " + centre));
                    }
                    foreach (var centre in DirectionalValves.Centres5)
                    {
                        var o = t.Create(Vec2.Zero).WithParam("ways", "5/3").WithParam("centre", centre).WithParam("medium", "pneumatic");
                        body.Append(Cell(r, o with { Size = t.Design(o) }, "5/3 " + centre));
                    }
                    body.Append("</div><h2>Actuations (left, spring right)</h2><div class=\"grid\">\n");
                    foreach (var op in DirectionalValves.Operators)
                    {
                        var o = t.Create(Vec2.Zero).WithParam("ways", "3/2").WithParam("actLeft", op).WithParam("actRight", "spring").WithParam("medium", "pneumatic");
                        body.Append(Cell(r, o with { Size = t.Design(o) }, op));
                    }
                    body.Append("</div>\n");
                }
                var file = cat.Id.Replace("fluid.", "") + ".html";
                File.WriteAllText(Path.Combine(dir, file), Page(cat.Name.En, body.ToString()), Encoding.UTF8);
                index.Append("<p><a href=\"").Append(file).Append("\">").Append(SvgRenderer.Esc(cat.Name.En)).Append("</a></p>\n");
            }

            var (cr, doc) = Circuit.Build();
            var vb = new Rect(55, 25, 115, 120);
            var page = SvgRenderer.RenderPage(cr, doc, doc.Pages[0], new SvgOptions { ViewBox = vb });
            page = page.Replace($"width=\"{F(vb.W)}mm\" height=\"{F(vb.H)}mm\"", $"width=\"{F(vb.W * 7)}\" height=\"{F(vb.H * 7)}\"");
            index.Append("<h2>Sample circuit (lines docked to ports)</h2>").Append(page);
            var mono = SvgRenderer.RenderPage(cr, doc, doc.Pages[0], new SvgOptions { ViewBox = vb, Monochrome = true })
                .Replace($"width=\"{F(vb.W)}mm\" height=\"{F(vb.H)}mm\"", $"width=\"{F(vb.W * 3)}\" height=\"{F(vb.H * 3)}\"");
            index.Append("<h2>Monochrome</h2>").Append(mono);
            File.WriteAllText(Path.Combine(dir, "index.html"), Page("InventaSketch fluid power gallery", index.ToString()), Encoding.UTF8);
            File.WriteAllText(Path.Combine(dir, "circuit.html"), Page("Sample circuit", page), Encoding.UTF8);
            Assert.True(File.Exists(Path.Combine(dir, "index.html")));
        }
    }
}
