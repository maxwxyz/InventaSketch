using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Optics.Tests
{
    /// <summary>
    /// Test-only helper that writes an SVG gallery (every library item, every view, stretched variants and traced sample
    /// setups) to the test output folder ("gallery/") for visual review. Ports are marked with small red circles.
    /// </summary>
    public class GalleryTests
    {
        private const double PxPerMm = 5;

        private static string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);

        private static string Cell(Registry r, SketchObject o, string label)
        {
            var doc = Fx.Doc(o);
            var ctx = new RenderContext(doc);
            var prims = r.RenderLocal(o, ctx).ToList();
            Rect vb;
            if (o.IsPointGeometry) vb = (Prim.Bounds(prims) ?? new Rect(0, 0, 1, 1)).Inflate(3);
            else
            {
                double w = o.Size.W, h = o.Size.H;
                prims.Insert(0, Prim.Rect(-w / 2, -h / 2, w, h, new Style { Stroke = "#c8d6e5", StrokeWidth = 0.15, Dash = DashStyle.Dash }));
                vb = (Prim.Bounds(prims) ?? new Rect(-w / 2, -h / 2, w, h)).Union(new Rect(-w / 2, -h / 2, w, h)).Inflate(2);
                foreach (var a in r.TypeOf(o).Anchors(o, ctx).Where(a => a.Role == AnchorRole.Port && a.P.X >= vb.X && a.P.X <= vb.Right))
                    prims.Add(Prim.Circle(a.P.X, a.P.Y, 0.5, new Style { Stroke = "#e03030", StrokeWidth = 0.12 }));
            }
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

        /// <summary>Sample setups: name, document and the view box to show.</summary>
        internal static IEnumerable<(string Name, SketchDocument Doc, Rect View)> Scenes(Registry r)
        {
            SketchObject I(string id, double x, double y) => Fx.Item(r, id, new Vec2(x, y));
            SketchObject Axis(double x0, double x1, double y) => Fx.Item(r, "optics.axis", Vec2.Zero) with { Points = new[] { new Vec2(x0, y), new Vec2(x1, y) } };
            SketchDocument Bundle(SketchDocument d, string sourceId, string anchor) => Fx.Bundle(d, d.Get(sourceId)!, anchor).Doc;

            {
                var lens = I("optics.lens.biconvex", 180, 150).WithParam("focal", true).WithParam("label", "L");
                var obj = I("optics.object.arrow", 100, 142);
                var d = Bundle(Fx.Doc(Axis(80, 290, 150), lens, obj), obj.Id, "tip");
                yield return ("Imaging: object at 2f, principal rays meet at 2f′", d, new Rect(85, 115, 200, 70));
            }
            {
                var lens = I("optics.lens.biconvex", 180, 150).WithParam("focal", true);
                var obj = I("optics.object.arrow", 100, 142).WithParam("bundle", "fan");
                var d = Bundle(Fx.Doc(Axis(80, 290, 150), lens, obj, I("optics.screen", 262, 150)), obj.Id, "tip");
                yield return ("Imaging with a fan of rays onto a screen", d, new Rect(85, 115, 200, 70));
            }
            {
                var lens = I("optics.lens.biconcave", 180, 150).WithParam("f", 30.0).WithParam("focal", true);
                var d = Bundle(Fx.Doc(Axis(120, 260, 150), lens), lens.Id, "o");
                yield return ("Diverging lens: virtual rays back to F′", d, new Rect(125, 125, 130, 50));
            }
            {
                var l1 = I("optics.lens.biconvex", 140, 150).WithParam("f", 60.0).WithParam("label", "Objective");
                var l2 = I("optics.lens.biconvex", 220, 150).WithParam("f", 20.0) with { Size = new Size2(6, 24) };
                var d = Bundle(Fx.Doc(Axis(60, 290, 150), l1, l2), l1.Id, "o");
                yield return ("Kepler telescope (f1 = 60, f2 = 20)", d, new Rect(70, 125, 215, 55));
            }
            {
                var laser = I("optics.source.laser", 80, 150);
                var mirror = I("optics.mirror.plane", 160, 150) with { Transform = new Transform(160, 150, 45) };
                var cube = I("optics.splitter.cube", 160, 100);
                var screen = I("optics.screen", 160, 50) with { Transform = new Transform(160, 50, 90) };
                var pd = I("optics.detector.photodiode", 230, 100);
                var d = Bundle(Fx.Doc(laser, mirror, cube, screen, pd), laser.Id, "exit");
                yield return ("Laser, mirror, beam splitter, screen and photodiode", d, new Rect(60, 40, 185, 125));
            }
            {
                // White light at the angle of minimum deviation onto the middle of the left face.
                var mid = new Vec2(152.5, 160);
                var tilt = -18.6;
                var dir = new Vec2(Math.Cos(tilt * Math.PI / 180), Math.Sin(tilt * Math.PI / 180));
                var lp = mid - dir * 75;
                var sp = mid - dir * 35;
                var lamp = I("optics.source.lamp", lp.X, lp.Y).WithParam("emission", "parallel").WithParam("rays", 1) with { Transform = new Transform(lp.X, lp.Y, tilt) };
                var slit = I("optics.stop.slit", sp.X, sp.Y) with { Transform = new Transform(sp.X, sp.Y, tilt) };
                var prism = I("optics.prism.dispersing", 160, 160);
                var screen = I("optics.screen", 222, 200) with { Transform = new Transform(222, 200, 32) };
                var d = Bundle(Fx.Doc(lamp, slit, prism, screen), lamp.Id, "exit");
                yield return ("Dispersion of white light in a prism", d, new Rect(70, 130, 180, 90));
            }
            {
                var lamp = I("optics.source.lamp.collimated", 70, 150).WithParam("rays", 5).WithParam("beam", 24.0);
                var mirror = I("optics.mirror.concave", 190, 150).WithParam("focal", true);
                var d = Bundle(Fx.Doc(Axis(60, 200, 150), lamp, mirror), lamp.Id, "exit");
                yield return ("Concave mirror focusing parallel light", d, new Rect(55, 125, 150, 50));
            }
            {
                var mirror = I("optics.mirror.convex", 190, 150).WithParam("focal", true);
                var d = Fx.Doc(Axis(100, 240, 150), mirror);
                foreach (var y in new[] { -10.0, -5, 5, 10 }) d = Fx.Add(d, Fx.Ray(new Vec2(110, 150 + y), new Vec2(130, 150 + y)));
                yield return ("Convex mirror with virtual rays", d, new Rect(100, 125, 145, 50));
            }
            {
                var laser = I("optics.source.laser.green", 80, 150);
                var grating = I("optics.grating", 150, 150).WithParam("orders", 2);
                var screen = I("optics.screen", 230, 150) with { Size = new Size2(3, 110) };
                var lamp = I("optics.source.lamp", 80, 260).WithParam("emission", "parallel").WithParam("rays", 1);
                var g2 = I("optics.grating", 150, 260);
                var s2 = I("optics.screen", 230, 260) with { Size = new Size2(3, 80) };
                var d = Bundle(Bundle(Fx.Doc(laser, grating, screen, lamp, g2, s2), laser.Id, "exit"), lamp.Id, "exit");
                yield return ("Grating: laser (orders ±2) and white light (spectra)", d, new Rect(60, 90, 180, 220));
            }
            {
                var lamp = I("optics.source.lamp.collimated", 80, 150).WithParam("rays", 3).WithParam("beam", 6.0);
                var eye = I("optics.detector.eye", 170, 150);
                var d = Bundle(Fx.Doc(lamp, eye), lamp.Id, "exit");
                yield return ("Eye focusing parallel light on the retina", d, new Rect(65, 135, 120, 30));
            }
            {
                var obj = I("optics.object.arrow", 80, 142).WithParam("bundle", "fan").WithParam("rays", 3);
                var cam = I("optics.detector.camera", 200, 150);
                var d = Bundle(Fx.Doc(Axis(70, 215, 150), obj, cam), obj.Id, "tip");
                yield return ("Camera imaging an object", d, new Rect(70, 125, 150, 45));
            }
            {
                var led = I("optics.source.led", 100, 150);
                var lens = I("optics.lens.planoconvex", 140, 150).WithParam("f", 30.0) with { Size = new Size2(8, 30) };
                var pd = I("optics.detector.photodiode", 230, 150);
                var fibre = I("optics.fibre", 120, 210);
                var l2 = I("optics.lens.converging.symbol", 190, 210).WithParam("f", 30.0) with { Size = new Size2(4, 24) };
                var d = Bundle(Bundle(Fx.Doc(Axis(85, 240, 150), led, lens, pd, fibre, l2), led.Id, "exit"), fibre.Id, "exit");
                yield return ("LED with collimator, fibre output with symbol lens", d, new Rect(85, 130, 160, 100));
            }
        }

        [Fact]
        public void WriteGallery()
        {
            var r = Fx.Reg();
            var dir = Path.Combine(AppContext.BaseDirectory, "gallery");
            Directory.CreateDirectory(dir);
            var body = new StringBuilder();
            foreach (var cat in r.Categories.OrderBy(c => c.Order))
            {
                body.Append("<h2>").Append(SvgRenderer.Esc(cat.Name.En + " / " + cat.Name.De)).Append("</h2><div class=\"grid\">\n");
                foreach (var item in r.Library.Where(i => i.Category == cat.Id))
                {
                    var o = r.Create(item, Vec2.Zero);
                    body.Append(Cell(r, o, item.Name.En + " / " + item.Name.De));
                    if (o.IsPointGeometry) continue;
                    foreach (var v in r.TypeOf(o).Views.Where(v => v.Id != (o.View ?? r.TypeOf(o).DefaultView)))
                        body.Append(Cell(r, o with { View = v.Id }, "view " + v.Id));
                    body.Append(Cell(r, o with { Size = new Size2(o.Size.W * 1.8, o.Size.H * 0.7) }, "stretched 1.8 x 0.7"));
                }
                body.Append("</div>\n");
            }
            var lens = Fx.Item(r, "optics.lens.biconvex").WithParam("focal", true).WithParam("label", "L1");
            body.Append("<h2>Options</h2><div class=\"grid\">\n").Append(Cell(r, lens, "focal points + label"));
            body.Append(Cell(r, Fx.Item(r, "optics.mirror.concave").WithParam("focal", true), "concave mirror, F and C"));
            body.Append("</div>\n");
            File.WriteAllText(Path.Combine(dir, "items.html"), Page("InventaSketch optics: library", body.ToString()), Encoding.UTF8);

            var scenes = new StringBuilder();
            var n = 0;
            foreach (var (name, doc, view) in Scenes(r))
            {
                var svg = SvgRenderer.RenderPage(r, doc, doc.Pages[0], new SvgOptions { ViewBox = view });
                svg = svg.Replace($"width=\"{F(view.W)}mm\" height=\"{F(view.H)}mm\"", $"width=\"{F(view.W * 5)}\" height=\"{F(view.H * 5)}\"");
                File.WriteAllText(Path.Combine(dir, $"scene{++n:00}.svg"), svg, Encoding.UTF8);
                scenes.Append("<h2>").Append(SvgRenderer.Esc(name)).Append("</h2>").Append(svg);
            }
            File.WriteAllText(Path.Combine(dir, "scenes.html"), Page("InventaSketch optics: traced setups", scenes.ToString()), Encoding.UTF8);
            Assert.True(n >= 10);
        }

        [Fact]
        public void SampleScenesRenderWithoutFailures()
        {
            var r = Fx.Reg();
            foreach (var (name, doc, _) in Scenes(r))
                foreach (var ray in doc.Objects.Values.Where(o => o.Type == RayType.TypeId))
                {
                    var result = Fx.Trace(doc, ray.Id);
                    Assert.True(result.Paths.Count > 0, name);
                    Assert.All(result.Paths, p => Assert.True(p.Points.Count >= 2, name));
                }
        }
    }
}
