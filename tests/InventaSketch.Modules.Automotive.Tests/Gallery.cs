using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Automotive.Tests
{
    /// <summary>Example scenes used by the persistence tests and the gallery.</summary>
    internal static class Scene
    {
        private static SketchObject At(Registry r, string itemId, double x, double y, double rotation = 0)
        {
            var o = r.Create(r.Item(itemId) ?? throw new KeyNotFoundException(itemId), new Vec2(x, y));
            return rotation == 0 ? o : o with { Transform = o.Transform with { Rotation = rotation } };
        }

        /// <summary>Top-view traffic scene: ego car with camera and corner radar, lane change, pedestrian at a crosswalk.</summary>
        public static (SketchDocument Doc, Page Page) Build(Registry r)
        {
            var road = At(r, "auto.road.2", 169, 95) with { Size = new Size2(330, 70) };
            var ego = At(r, "auto.car.top", 70, 112.5);
            var lead = At(r, "auto.suv.top", 190, 112.5);
            var other = At(r, "auto.van.top", 140, 77.5);
            var cam = At(r, "auto.camera", 96.5, 112.5);
            var radar = At(r, "auto.radar", 91, 104.6, -45);
            var camFov = At(r, "auto.fov.camera", 0, 0);
            var radarFov = At(r, "auto.fov.radar.corner", 0, 0);
            var traj = r.Create(r.Item("auto.trajectory.corridor")!, Vec2.Zero) with
            {
                Points = new[] { new Vec2(92.5, 112.5), new Vec2(118, 111), new Vec2(140, 96), new Vec2(162, 80), new Vec2(185, 77.5) },
            };
            var objects = new[]
            {
                At(r, "auto.building.top", 40, 25), At(r, "auto.building.top", 300, 25), At(r, "auto.building.top", 300, 165),
                road, At(r, "auto.crosswalk", 250, 95), At(r, "auto.light.top", 236, 55),
                camFov, radarFov, traj, ego, lead, other, cam, radar,
                At(r, "auto.walking.top", 252, 140, -90), At(r, "auto.conflict", 252, 112.5),
                r.Create(r.Item("auto.safetydistance")!, Vec2.Zero) with { Points = new[] { new Vec2(92.5, 112.5), new Vec2(166.5, 112.5) }, Params = new Dictionary<string, ParamValue> { ["offset"] = -14.0, ["label"] = "d_safe" } },
                At(r, "auto.ttc", 215, 150),
            };
            var doc = SketchDocument.Create(title: "Automotive scene");
            doc = Edits.Add(doc, doc.Pages[0].Id, objects);
            doc = Fov.AttachTo(doc, camFov.Id, cam.Id);
            doc = Fov.AttachTo(doc, radarFov.Id, radar.Id);
            return (doc, doc.Pages[0]);
        }

        /// <summary>Side-view scene: car with driver, steering wheel, seat, windshield camera and a pedestrian and bus.</summary>
        public static (SketchDocument Doc, Page Page) BuildSide(Registry r)
        {
            // vehicle drawn in phantom lines without fill (cutaway), occupant and interior visible
            var car = At(r, "auto.car.side", 80, 120) with
            {
                Size = new Size2(135, 43.5),
                Style = new Style { Fill = Fill.None, Dash = DashStyle.DashDotDot, StrokeWidth = 0.35 },
            };
            var anchors = r.PageAnchors(car, new RenderContext(SketchDocument.Create())).ToList();
            var ws = anchors.Single(a => a.Id == "mount.windshield").P;
            var cam = At(r, "auto.camera", ws.X - 4.5, ws.Y + 2.5);
            var fov = At(r, "auto.fov.camera", 0, 0) with { Params = new Dictionary<string, ParamValue> { ["range"] = 120.0, ["angle"] = 24.0, ["heading"] = 8.0, ["fill"] = "none" } };
            var objects = new List<SketchObject>
            {
                car,
                At(r, "auto.seat.side", 78.5, 123.9) with { Size = new Size2(30, 39) },
                At(r, "auto.driver.side", 80, 121.5) with { Size = new Size2(33.6, 43.8) },
                At(r, "auto.steering.side", 86.5, 116.4) with { Size = new Size2(13.5, 18) },
                cam, fov,
                At(r, "auto.standing.side", 250, 115.5) with { Transform = new Transform(250, 115.5, 0, FlipX: true), Size = new Size2(15, 52.5) },
                At(r, "auto.bus.side", 250, 40) with { Size = new Size2(120, 32) },
                At(r, "auto.light", 315, 117.75) with { Size = new Size2(16, 48) },
            };
            var doc = SketchDocument.Create(title: "Automotive side scene");
            doc = Edits.Add(doc, doc.Pages[0].Id, objects.ToArray());
            doc = Fov.AttachTo(doc, fov.Id, cam.Id);
            return (doc, doc.Pages[0]);
        }
    }

    /// <summary>
    /// Test-only helper that writes an SVG gallery (all types x all views, stretched variants, library presets and example
    /// scenes) to the test output folder ("gallery-automotive/") for visual review (e.g. headless browser screenshots).
    /// </summary>
    public class GalleryTests
    {
        private const double PxPerMm = 4;

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
                prims = r.TypeOf(o).Render(o, new RenderContext(SketchDocument.Create())).ToList();
                double w = o.Size.W, h = o.Size.H;
                prims.Insert(0, Prim.Rect(-w / 2, -h / 2, w, h, new Style { Stroke = "#c8d6e5", StrokeWidth = 0.15, Dash = DashStyle.Dash }));
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

        private static string ScenePage(Registry r, SketchDocument doc, Page page)
        {
            var svg = SvgRenderer.RenderPage(r, doc, page);
            return svg.Replace("width=\"338.667mm\" height=\"190.5mm\"", "width=\"1354\" height=\"762\"");
        }

        [Fact]
        public void WriteGallery()
        {
            var r = Fx.Reg();
            var dir = Path.Combine(AppContext.BaseDirectory, "gallery-automotive");
            Directory.CreateDirectory(dir);
            var index = new StringBuilder();
            foreach (var cat in r.Categories.OrderBy(c => c.Order))
            {
                var body = new StringBuilder();
                foreach (var t in r.Types.Where(t => t.Category == cat.Id))
                {
                    body.Append("<h2>").Append(SvgRenderer.Esc($"{t.Name.En} / {t.Name.De}  [{t.Id}]")).Append("</h2><div class=\"grid\">\n");
                    var baseObj = t.Create(t.Geometry == GeometryKind.Points ? new Vec2(100, 100) : Vec2.Zero);
                    var views = t.Views.Count > 0 ? t.Views.Select(v => v.Id).ToList() : new List<string> { baseObj.View ?? "" };
                    foreach (var v in views)
                    {
                        var o = baseObj;
                        if (o.View != v)
                        {
                            var act = t.Actions(o, Lang.En).FirstOrDefault(a => a.Id == "view." + v);
                            o = act is null ? o with { View = v } : act.Apply(o);
                        }
                        body.Append(Cell(r, o, v));
                    }
                    if (t.Geometry == GeometryKind.Box)
                        body.Append(Cell(r, baseObj with { Size = new Size2(baseObj.Size.W * 1.8, baseObj.Size.H * 0.7) }, "stretched 1.8 x 0.7"));
                    body.Append("</div>\n");
                }
                body.Append("<h2>Library presets</h2><div class=\"grid\">\n");
                foreach (var item in r.Library.Where(i => i.Category == cat.Id))
                    body.Append(Cell(r, r.Create(item, new Vec2(100, 100)), item.Name.En));
                body.Append("</div>\n");
                var file = cat.Id.Replace("auto.", "") + ".html";
                File.WriteAllText(Path.Combine(dir, file), Page(cat.Name.En, body.ToString()), Encoding.UTF8);
                index.Append("<p><a href=\"").Append(file).Append("\">").Append(SvgRenderer.Esc(cat.Name.En)).Append("</a></p>\n");
            }
            var (doc, page) = Scene.Build(r);
            index.Append("<h2>Top-view scene</h2>").Append(ScenePage(r, doc, page));
            var (doc2, page2) = Scene.BuildSide(r);
            index.Append("<h2>Side-view scene</h2>").Append(ScenePage(r, doc2, page2));
            File.WriteAllText(Path.Combine(dir, "index.html"), Page("InventaSketch automotive gallery", index.ToString()), Encoding.UTF8);
            Assert.True(File.Exists(Path.Combine(dir, "index.html")));
        }
    }
}
