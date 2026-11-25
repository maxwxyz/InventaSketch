using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Process.Tests
{
    /// <summary>
    /// Test-only helper that writes an SVG gallery (every type in each view, a stretched variant, every library preset and
    /// two sample flow sheets) to the test output folder ("gallery/") for visual review. Ports are marked with small red
    /// circles, rails with dotted blue lines.
    /// </summary>
    public class GalleryTests
    {
        private const double PxPerMm = 12;

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
                if (a.Role == AnchorRole.Port) prims.Add(Prim.Circle(a.P.X, a.P.Y, 0.35, new Style { Stroke = "#e03030", StrokeWidth = 0.1 }));
                else if (a.Role == AnchorRole.Rail) prims.Add(Prim.Line(a.P, a.Q, new Style { Stroke = "#30a0e0", StrokeWidth = 0.08, Dash = DashStyle.Dot }));
            }
            var px = Math.Max(3, Math.Min(PxPerMm, 300 / Math.Max(vb.W, vb.H)));
            var svg = SvgRenderer.RenderPrimitives(prims, new SvgOptions { Background = null, ViewBox = vb });
            svg = svg.Replace("<svg ", $"<svg width=\"{F(vb.W * px)}\" height=\"{F(vb.H * px)}\" ");
            return $"<figure>{svg}<figcaption>{SvgRenderer.Esc(label)}</figcaption></figure>\n";
        }

        private static string Page(string title, string body) =>
            "<!DOCTYPE html><html><head><meta charset=\"utf-8\"><title>" + SvgRenderer.Esc(title) + "</title><style>" +
            "body{font-family:Arial,sans-serif;margin:12px;background:#fff}h1{font-size:18px}h2{font-size:14px;margin:12px 0 4px}" +
            ".grid{display:flex;flex-wrap:wrap;gap:10px;align-items:flex-end}" +
            "figure{margin:0;padding:4px;border:1px solid #eee}figcaption{font-size:10px;color:#444;max-width:260px}" +
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
                    if (baseObj.IsPointGeometry) baseObj = baseObj with { Points = new[] { new Vec2(-15, 0), new Vec2(10, 0), new Vec2(10, -12), new Vec2(30, -12) } };
                    foreach (var v in t.Views)
                    {
                        var o = r.WithView(baseObj, v.Id, new RenderContext(SketchDocument.Create()));
                        if (o.Params.ContainsKey(ProcessType.TagKey) && o.Type != Instruments.BubbleId && v.Id == ProcessViews.Symbol)
                            body.Append(Cell(r, o.WithParam(ProcessType.TagKey, "X-101"), v.Id + " (tag X-101)"));
                        else body.Append(Cell(r, o, v.Id));
                        if (!o.IsPointGeometry) body.Append(Cell(r, o with { Size = new Size2(o.Size.W * 1.6, o.Size.H * 0.7) }, v.Id + " stretched 1.6 x 0.7 (DE)", Lang.De));
                    }
                    body.Append("</div>\n");
                }
                body.Append("<h2>Library presets</h2><div class=\"grid\">\n");
                foreach (var item in r.Library.Where(i => i.Category == cat.Id))
                {
                    var o = r.Create(item, Vec2.Zero);
                    if (o.IsPointGeometry) o = o with { Points = new[] { new Vec2(-15, 0), new Vec2(10, 0), new Vec2(10, -12), new Vec2(30, -12) } };
                    body.Append(Cell(r, o, item.Name.En + " / " + item.Name.De));
                }
                body.Append("</div>\n");
                var file = cat.Id.Replace("process.", "") + ".html";
                File.WriteAllText(Path.Combine(dir, file), Page(cat.Name.En, body.ToString()), Encoding.UTF8);
                index.Append("<p><a href=\"").Append(file).Append("\">").Append(SvgRenderer.Esc(cat.Name.En)).Append("</a></p>\n");
            }

            foreach (var (name, doc, vb) in new[] { Samples.Distillation(r), Samples.AirHandling(r) })
            {
                var page = SvgRenderer.RenderPage(r, doc, doc.Pages[0], new SvgOptions { ViewBox = vb });
                page = page.Replace($"width=\"{F(vb.W)}mm\" height=\"{F(vb.H)}mm\"", $"width=\"{F(vb.W * 6)}\" height=\"{F(vb.H * 6)}\"");
                index.Append("<h2>").Append(SvgRenderer.Esc(name)).Append("</h2>").Append(page);
            }
            File.WriteAllText(Path.Combine(dir, "index.html"), Page("InventaSketch process & thermal gallery", index.ToString()), Encoding.UTF8);
            Assert.True(File.Exists(Path.Combine(dir, "index.html")));
        }
    }

    /// <summary>Small sample flow sheets assembled like a user would (lines docked on nozzles).</summary>
    internal static class Samples
    {
        private static SketchDocument Connect(Registry r, SketchDocument doc, string item, (SketchObject O, string Port) a, (SketchObject O, string Port) b, params Vec2[] via)
        {
            var pa = Fx.Port(r, doc, doc.Get(a.O.Id)!, a.Port);
            var pb = Fx.Port(r, doc, doc.Get(b.O.Id)!, b.Port);
            var line = r.Create(r.Item(item)!, Vec2.Zero) with { Points = new[] { pa.P }.Concat(via).Concat(new[] { pb.P }).ToList() };
            doc = Edits.Add(doc, doc.Pages[0].Id, line);
            doc = Edits.SetPoint(doc, line.Id, 0, pa.P, Attachments.For(doc.Get(a.O.Id)!, pa.P, pa));
            return Edits.SetPoint(doc, line.Id, via.Length + 1, pb.P, Attachments.For(doc.Get(b.O.Id)!, pb.P, pb));
        }

        public static (string, SketchDocument, Rect) Distillation(Registry r)
        {
            var column = Fx.Item(r, "process.column", new Vec2(60, 60)).WithParam("tag", "C-101");
            var condenser = Fx.Item(r, "process.hx.shelltube", new Vec2(110, 20)).WithParam("tag", "E-101");
            var drum = Fx.Item(r, "process.vessel.horizontal", new Vec2(115, 55)).WithParam("tag", "V-101");
            var pump = Fx.Item(r, "process.pump.centrifugal", new Vec2(145, 80)).WithParam("tag", "P-101");
            var reboiler = Fx.Item(r, "process.hx.shelltube", new Vec2(100, 105)).WithParam("tag", "E-102");
            var feedValve = Fx.Item(r, "process.valve.control", new Vec2(25, 56.5));
            var tic = Fx.Item(r, "process.instrument.bubble", new Vec2(25, 35)).WithParam("location", "central");
            var lic = Fx.Item(r, "process.instrument.lic", new Vec2(145, 30));
            var chart = Fx.Item(r, "process.chart.ts", new Vec2(215, 60));
            var doc = SketchDocument.Create();
            doc = Edits.Add(doc, doc.Pages[0].Id, column, condenser, drum, pump, reboiler, feedValve, tic, lic, chart);
            doc = Connect(r, doc, "process.line", (column, "N1"), (condenser, "S1"), new Vec2(60, 8), new Vec2(117, 8));
            doc = Connect(r, doc, "process.line", (condenser, "S2"), (drum, "N3"));
            doc = Connect(r, doc, "process.line", (drum, "N4"), (pump, "S"));
            doc = Connect(r, doc, "process.line.arrow", (pump, "D"), (column, "N4"), new Vec2(158, 76), new Vec2(158, 43));
            doc = Connect(r, doc, "process.line", (column, "N2"), (reboiler, "T1"));
            doc = Connect(r, doc, "process.line.arrow", (reboiler, "S1"), (column, "N5"));
            doc = Connect(r, doc, "process.line.arrow", (feedValve, "2"), (column, "N3"));
            doc = Connect(r, doc, "process.line.signal", (tic, "B"), (feedValve, "S"));
            doc = Connect(r, doc, "process.line.signal", (lic, "B"), (drum, "N2"));
            return ("Sample: distillation column with condenser, reflux drum, pump and reboiler", doc, new Rect(5, 0, 255, 125));
        }

        public static (string, SketchDocument, Rect) AirHandling(Registry r)
        {
            var y = 30.0;
            var damper = Fx.Item(r, "process.hvac.damper.motorised", new Vec2(20, y));
            var filter = Fx.Item(r, "process.hvac.filter.bag", new Vec2(30, y));
            var recovery = Fx.Item(r, "process.hvac.recovery", new Vec2(40, y - 5));
            var heater = Fx.Item(r, "process.hvac.coil", new Vec2(50, y + 1));
            var cooler = Fx.Item(r, "process.hvac.coil.cooling", new Vec2(60, y + 1));
            var hum = Fx.Item(r, "process.hvac.humidifier", new Vec2(70, y + 1));
            var fan = Fx.Item(r, "process.fan.axial", new Vec2(81, y));
            var mix = Fx.Item(r, "process.valve.threeway.motor", new Vec2(46, y + 20));
            var ph = Fx.Item(r, "process.chart.ph", new Vec2(205, y + 5));
            var doc = SketchDocument.Create();
            doc = Edits.Add(doc, doc.Pages[0].Id, damper, filter, recovery, heater, cooler, hum, fan, mix, ph);
            doc = Connect(r, doc, "process.line.secondary", (heater, "W2"), (mix, "AB"));
            return ("Sample: air handling unit (supply air left to right) and a refrigeration cycle", doc, new Rect(5, 5, 250, 60));
        }
    }
}
