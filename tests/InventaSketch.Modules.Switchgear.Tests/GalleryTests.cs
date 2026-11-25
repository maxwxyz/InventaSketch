using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Switchgear.Tests
{
    /// <summary>
    /// Test-only helper that writes an SVG gallery (every library item in every view and a sample board) to the test output
    /// folder ("gallery/switchgear.html", or the folder in SW_GALLERY_OUT) for visual review. Ports are marked red, rail and
    /// device mounts blue.
    /// </summary>
    public class GalleryTests
    {
        private const double PxPerMm = 4;

        private static string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);

        private static string Cell(SketchDocument doc, SketchObject o, string label)
        {
            var ctx = new RenderContext(doc);
            var prims = Fx.Reg.RenderLocal(o, ctx).ToList();
            Rect vb;
            if (o.IsPointGeometry) vb = (Prim.Bounds(prims) ?? new Rect(0, 0, 1, 1)).Inflate(3);
            else
            {
                double w = o.Size.W, h = o.Size.H;
                prims.Insert(0, Prim.Rect(-w / 2, -h / 2, w, h, new Style { Stroke = "#c8d6e5", StrokeWidth = 0.15, Dash = DashStyle.Dash }));
                vb = (Prim.Bounds(prims) ?? new Rect(-w / 2, -h / 2, w, h)).Union(new Rect(-w / 2, -h / 2, w, h)).Inflate(2);
                foreach (var a in Fx.Reg.TypeOf(o).Anchors(o, ctx).Where(a => a.Kind == AnchorKind.Point && a.Role is AnchorRole.Port or AnchorRole.Mount))
                    prims.Add(Prim.Circle(a.P.X, a.P.Y, 0.6, new Style { Stroke = a.Role == AnchorRole.Port ? "#e03030" : "#3060e0", StrokeWidth = 0.15 }));
            }
            var svg = SvgRenderer.RenderPrimitives(prims, new SvgOptions { Background = null, ViewBox = vb });
            svg = svg.Replace("<svg ", $"<svg width=\"{F(vb.W * PxPerMm)}\" height=\"{F(vb.H * PxPerMm)}\" ");
            return $"<figure>{svg}<figcaption>{SvgRenderer.Esc(label)}</figcaption></figure>\n";
        }

        [Fact]
        public void WritesTheGallery()
        {
            var body = new StringBuilder();
            foreach (var cat in Fx.Reg.Categories)
            {
                body.Append("<h2>").Append(SvgRenderer.Esc(cat.Name.En)).Append("</h2><div class=\"grid\">");
                foreach (var item in Fx.Reg.Library.Where(i => i.Category == cat.Id))
                {
                    var o = Fx.Reg.Create(item, new Vec2(100, 100));
                    if (o.Type.StartsWith("sw.protection", StringComparison.Ordinal) || o.Type.StartsWith("sw.terminal.", StringComparison.Ordinal))
                        o = o.WithParam("ref", o.Type.StartsWith("sw.terminal.", StringComparison.Ordinal) ? "-X1" : "-F1").WithParam("number", "12");
                    if (o.IsPointGeometry) o = o.WithParam("ref", "-W1");
                    body.Append(Cell(Fx.Doc(o), o, item.Name.En + " / " + item.Name.Get(Lang.De)));
                    var type = Fx.Reg.Type(o.Type)!;
                    foreach (var v in type.Views.Skip(1))
                    {
                        var x = Fx.Reg.WithView(o, v.Id, new RenderContext(Fx.Doc(o)));
                        body.Append(Cell(Fx.Doc(x), x, item.Id + " (" + v.Id + ")"));
                    }
                }
                body.Append("</div>");
            }
            // The sample board of the export tests, as one picture.
            var doc = ExportTests.Sample();
            var strip = Fx.Item("sw.label.strip", 200, 47);
            doc = Edits.Add(doc, doc.Pages[0].Id, strip);
            var page = doc.Pages[0];
            var svg = SvgRenderer.RenderPage(Fx.Reg, doc, page, new SvgOptions { Background = "#ffffff", ViewBox = new Rect(80, 30, 340, 290) });
            body.Append("<h2>Sample board</h2>").Append(svg);
            var html = "<!DOCTYPE html><html><head><meta charset=\"utf-8\"><title>Switchgear gallery</title><style>" +
                "body{font-family:Arial,sans-serif;margin:12px;background:#fff}h2{font-size:14px;margin:14px 0 4px}" +
                ".grid{display:flex;flex-wrap:wrap;gap:10px;align-items:flex-end}figure{margin:0;padding:4px;border:1px solid #eee}" +
                "figcaption{font-size:10px;color:#444;max-width:220px}</style></head><body>" + body + "</body></html>";
            var dir = Environment.GetEnvironmentVariable("SW_GALLERY_OUT");
            if (string.IsNullOrEmpty(dir)) dir = Path.Combine(AppContext.BaseDirectory, "gallery");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "switchgear.html"), html);
            Assert.Contains("<svg", html);
        }
    }
}
