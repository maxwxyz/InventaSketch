using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Maps.Tests
{
    /// <summary>
    /// Test-only helper that writes an SVG gallery (all types x all views, stretched variants, library presets and the
    /// sample route map in colour, monochrome and German) to the test output folder ("gallery-maps/") for visual review
    /// (e.g. headless browser screenshots).
    /// </summary>
    public class GalleryTests
    {
        private const double PxPerMm = 5;

        private static string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);

        private static string Cell(Registry r, SketchObject o, string label)
        {
            List<Primitive> prims;
            Rect vb;
            if (o.IsPointGeometry)
            {
                var (doc, _) = Fx.Doc(o);
                prims = r.Render(o, new RenderContext(doc)).ToList();
                vb = (Prim.Bounds(prims) ?? new Rect(0, 0, 10, 10)).Inflate(3);
            }
            else
            {
                prims = r.TypeOf(o).Render(o, Fx.Ctx()).ToList();
                double w = o.Size.W, h = o.Size.H;
                prims.Insert(0, Prim.Rect(-w / 2, -h / 2, w, h, new Style { Stroke = "#c8d6e5", StrokeWidth = 0.1, Dash = DashStyle.Dash }));
                vb = new Rect(-w / 2 - 2, -h / 2 - 2, w + 4, h + 4);
            }
            var scale = Math.Min(PxPerMm, 700 / Math.Max(vb.W, 1));
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

        private static string MapSvg(Registry r, SketchDocument doc, Page page, bool mono = false, Lang lang = Lang.En)
        {
            var svg = SvgRenderer.RenderPage(r, doc, page, new SvgOptions { Monochrome = mono }, lang);
            return svg.Replace("width=\"338.667mm\" height=\"190.5mm\"", "width=\"1354\" height=\"762\"");
        }

        /// <summary>A hill (closed contours), a route over it with its values, a distance measure, an area with values and the profile.</summary>
        private static (SketchDocument Doc, Page Page) HillMap(Registry r)
        {
            SketchObject Ring(double rx, double ry, double elevation)
            {
                var pts = Enumerable.Range(0, 8).Select(i => new Vec2(150 + rx * Math.Cos(i * Math.PI / 4), 80 + ry * Math.Sin(i * Math.PI / 4))).ToArray();
                return Fx.Line(r, "map.contour", pts).WithParam("closed", true).WithParam("elevation", elevation);
            }
            var start = Fx.Item(r, "map.marker.start", new Vec2(70, 92));
            var finish = Fx.Item(r, "map.marker.finish", new Vec2(235, 66));
            var route = Fx.Line(r, "map.route", new Vec2(70, 100), new Vec2(120, 90), new Vec2(160, 78), new Vec2(200, 74), new Vec2(235, 74))
                .WithParam(ValueLabels.ShowParam, true).WithParam("mode", "walk");
            var objects = new List<SketchObject>
            {
                Fx.Item(r, "map.frame", new Vec2(169, 95)) with { Size = new Size2(320, 170) },
                Ring(70, 45, 300), Ring(48, 30, 350), Ring(26, 16, 400),
                Fx.Line(r, "map.region.values", new Vec2(30, 130), new Vec2(80, 125), new Vec2(90, 165), new Vec2(35, 170)).WithParam("kind", "forest"),
                route, start, finish,
                Fx.Line(r, "map.measure.bearing", new Vec2(70, 100), new Vec2(235, 74)) with { Points = new[] { new Vec2(70, 112), new Vec2(235, 86) } },
                Fx.Item(r, "map.profile", new Vec2(250, 145)) with { Size = new Size2(100, 45) },
                Fx.Item(r, "map.scalebar", new Vec2(60, 25)) with { Size = new Size2(40, 8) },
                Fx.Item(r, "map.north", new Vec2(300, 25)),
            };
            var doc = SketchDocument.Create(PageSizes.Slide16x9, "Hill");
            doc = Edits.Add(doc, doc.Pages[0].Id, objects.ToArray());
            return (doc, doc.Pages[0]);
        }

        [Fact]
        public void WriteGallery()
        {
            var r = Fx.Reg();
            var dir = Path.Combine(AppContext.BaseDirectory, "gallery-maps");
            Directory.CreateDirectory(dir);
            var index = new StringBuilder();
            foreach (var cat in r.Categories.OrderBy(c => c.Order))
            {
                var body = new StringBuilder();
                foreach (var t in r.Types.Where(t => t.Category == cat.Id))
                {
                    body.Append("<h2>").Append(SvgRenderer.Esc($"{t.Name.En} / {t.Name.De}  [{t.Id}]")).Append("</h2><div class=\"grid\">\n");
                    var baseObj = r.Create(r.Library.First(i => i.TypeId == t.Id), t.Geometry == GeometryKind.Points ? new Vec2(100, 100) : Vec2.Zero);
                    foreach (var v in t.Views) body.Append(Cell(r, Fx.InView(t, baseObj, v.Id), v.Id));
                    if (t.Geometry == GeometryKind.Box)
                        body.Append(Cell(r, baseObj with { Size = new Size2(baseObj.Size.W * 1.8, baseObj.Size.H * 0.7) }, "stretched 1.8 x 0.7"));
                    body.Append("</div>\n");
                }
                body.Append("<h2>Library presets</h2><div class=\"grid\">\n");
                foreach (var item in r.Library.Where(i => i.Category == cat.Id))
                {
                    var o = r.Create(item, new Vec2(100, 100));
                    if (o.IsPointGeometry && o.Type != "map.region") o = o with { Text = o.Type == "map.road" && o.Str("kind", "") is "motorway" or "main" ? "A1" : "Name" };
                    body.Append(Cell(r, o, item.Name.En));
                }
                body.Append("</div>\n");
                var file = cat.Id.Replace("map.", "") + ".html";
                File.WriteAllText(Path.Combine(dir, file), Page(cat.Name.En, body.ToString()), Encoding.UTF8);
                index.Append("<p><a href=\"").Append(file).Append("\">").Append(SvgRenderer.Esc(cat.Name.En)).Append("</a></p>\n");
            }
            var (doc, page) = SampleMap.Build(r);
            File.WriteAllText(Path.Combine(dir, "route-map.html"), Page("Sample route map", MapSvg(r, doc, page)), Encoding.UTF8);
            File.WriteAllText(Path.Combine(dir, "route-map-mono.html"), Page("Sample route map (monochrome)", MapSvg(r, doc, page, mono: true)), Encoding.UTF8);
            File.WriteAllText(Path.Combine(dir, "route-map-de.html"), Page("Beispiel-Routenkarte", MapSvg(r, doc, page, lang: Lang.De)), Encoding.UTF8);
            index.Append("<p><a href=\"route-map.html\">Sample route map</a> · <a href=\"route-map-mono.html\">monochrome</a> · <a href=\"route-map-de.html\">Deutsch</a></p>\n");
            var (hill, hillPage) = HillMap(r);
            File.WriteAllText(Path.Combine(dir, "hill-map.html"), Page("Measures and elevation profile", MapSvg(r, hill, hillPage)), Encoding.UTF8);
            index.Append("<p><a href=\"hill-map.html\">Measures and elevation profile</a></p>\n");
            File.WriteAllText(Path.Combine(dir, "index.html"), Page("InventaSketch maps gallery", index.ToString()), Encoding.UTF8);
            Assert.True(File.Exists(Path.Combine(dir, "index.html")));
        }
    }
}
