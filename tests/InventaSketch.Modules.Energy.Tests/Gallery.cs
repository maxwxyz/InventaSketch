using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Energy.Tests
{
    /// <summary>
    /// Test-only helper that writes an SVG gallery (every type in both views, stretched variants, library presets and two
    /// sample systems – a home energy system and an MV/LV single-line diagram – in both views) to the test output folder
    /// ("gallery/") for visual review. Ports are marked with small red circles, rails with dotted blue lines.
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
                else if (a.Role == AnchorRole.Rail) prims.Add(Prim.Line(a.P, a.Q, new Style { Stroke = "#30a0e0", StrokeWidth = 0.1, Dash = DashStyle.Dot }));
            }
            var px = Math.Max(4, Math.Min(PxPerMm, 320 / Math.Max(vb.W, vb.H)));
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
                    foreach (var v in t.Views)
                    {
                        var o = r.WithView(baseObj, v.Id, new RenderContext(SketchDocument.Create()));
                        if (o.IsPointGeometry)
                        {
                            o = o with { Points = new[] { new Vec2(-15, 0), new Vec2(0, 0), new Vec2(0, -10), new Vec2(15, -10) } };
                            body.Append(Cell(r, o, v.Id));
                            body.Append(Cell(r, o.WithParam("label", "Label").WithParam("conductors", 3).WithParam("showVoltage", true), v.Id + " labelled", Lang.De));
                            continue;
                        }
                        body.Append(Cell(r, o, v.Id));
                        body.Append(Cell(r, o.WithParam("name", "X1") with { Size = new Size2(o.Size.W * 1.6, o.Size.H * 0.7) }, v.Id + " named, stretched 1.6 x 0.7", Lang.De));
                    }
                    body.Append("</div>\n");
                }
                body.Append("<h2>Library presets</h2><div class=\"grid\">\n");
                foreach (var item in r.Library.Where(i => i.Category == cat.Id && !i.Id.EndsWith(".sld", StringComparison.Ordinal) && !i.Id.EndsWith(".pictorial", StringComparison.Ordinal)))
                {
                    var o = r.Create(item, Vec2.Zero);
                    body.Append(Cell(r, o, item.Name.En + " / " + item.Name.De));
                    if (!o.IsPointGeometry && r.TypeOf(o) is EnergyType et)
                    {
                        var other = et.ViewOf(o) == EnViews.Pictorial ? EnViews.SingleLine : EnViews.Pictorial;
                        body.Append(Cell(r, r.WithView(o, other, Fx.Ctx()), item.Name.De + " [" + other + "]", Lang.De));
                    }
                }
                body.Append("</div>\n");
                var file = cat.Id.Replace("energy.", "") + ".html";
                File.WriteAllText(Path.Combine(dir, file), Page(cat.Name.En, body.ToString()), Encoding.UTF8);
                index.Append("<p><a href=\"").Append(file).Append("\">").Append(SvgRenderer.Esc(cat.Name.En)).Append("</a></p>\n");
            }

            foreach (var (title, sample) in new[] { ("Home energy system", HomeSystem(r)), ("MV/LV single-line diagram", MvLvDiagram(r)) })
                foreach (var view in EnViews.All)
                {
                    var doc = SwitchAll(r, sample, view);
                    var vb = PageBounds(r, doc).Inflate(4);
                    var page = SvgRenderer.RenderPage(r, doc, doc.Pages[0], new SvgOptions { ViewBox = vb });
                    page = page.Replace($"width=\"{F(vb.W)}mm\" height=\"{F(vb.H)}mm\"", $"width=\"{F(vb.W * 5)}\" height=\"{F(vb.H * 5)}\"");
                    index.Append("<h2>").Append(SvgRenderer.Esc(title)).Append(": ").Append(view).Append("</h2>").Append(page);
                }
            File.WriteAllText(Path.Combine(dir, "index.html"), Page("InventaSketch energy systems gallery", index.ToString()), Encoding.UTF8);
            Assert.True(File.Exists(Path.Combine(dir, "index.html")));
        }

        // ------------------------------------------------------------------ sample systems

        private static Rect PageBounds(Registry r, SketchDocument doc)
        {
            var ctx = new RenderContext(doc);
            var prims = doc.ObjectsOn(doc.Pages[0]).SelectMany(o => r.Render(o, ctx)).ToList();
            return Prim.Bounds(prims) ?? new Rect(0, 0, 100, 100);
        }

        /// <summary>Every object to <paramref name="view"/>, then the cable ends follow their ports.</summary>
        internal static SketchDocument SwitchAll(Registry r, SketchDocument doc, string view)
        {
            var ctx = new RenderContext(doc);
            foreach (var o in doc.Objects.Values.ToList()) doc = Edits.Replace(doc, r.WithView(o, view, ctx));
            return Attachments.Refresh(r, doc);
        }

        private static Vec2 PortOf(Registry r, SketchDocument doc, string id, string port)
            => r.PageAnchors(doc.Get(id)!, new RenderContext(doc)).Single(a => a.Role == AnchorRole.Port && a.Id == port).P;

        /// <summary>A cable (orthogonal) from a port of one element to a port of another, both ends docked.</summary>
        internal static SketchDocument Connect(Registry r, SketchDocument doc, string itemId, string a, string pa, string b, string pb)
        {
            var p0 = PortOf(r, doc, a, pa);
            var p1 = PortOf(r, doc, b, pb);
            var line = r.Create(r.Item(itemId)!, Vec2.Zero) with { Points = new[] { p0, p1 } };
            line = line.WithParam("route", "orthogonal");
            doc = Edits.Add(doc, doc.Pages[0].Id, line);
            doc = Edits.AttachPoint(doc, line.Id, 0, a, p0, pa);
            return Edits.AttachPoint(doc, line.Id, 1, b, p1, pb);
        }

        private static (SketchDocument Doc, string Id) Place(Registry r, SketchDocument doc, string itemId, double x, double y, Size2? size = null, params (string Key, ParamValue Value)[] ps)
        {
            var o = r.Create(r.Item(itemId)!, new Vec2(x, y));
            if (size is not null) o = o with { Size = size };
            foreach (var (k, v) in ps) o = o.WithParam(k, v);
            return (Edits.Add(doc, doc.Pages[0].Id, o), o.Id);
        }

        /// <summary>Grid → meter → house busbar → PV, battery, house, wallbox with EV, heat pump.</summary>
        internal static SketchDocument HomeSystem(Registry r)
        {
            var doc = SketchDocument.Create();
            string grid, meter, bus, inv, pv, binv, bat, house, wb, ev, hp;
            (doc, grid) = Place(r, doc, "energy.grid.infeed.lv", 100, 20);
            (doc, meter) = Place(r, doc, "energy.grid.meter", 100, 60);
            (doc, bus) = Place(r, doc, "energy.grid.busbar.pictorial", 100, 85, new Size2(170, 10), ("name", "0.4 kV house"));
            (doc, inv) = Place(r, doc, "energy.conv.inverter", 32, 115);
            (doc, pv) = Place(r, doc, "energy.pv.string", 32, 150);
            (doc, binv) = Place(r, doc, "energy.conv.inverter.battery", 66, 115);
            (doc, bat) = Place(r, doc, "energy.storage.battery", 66, 150);
            (doc, house) = Place(r, doc, "energy.load.building", 100, 115);
            (doc, wb) = Place(r, doc, "energy.mobility.charger", 134, 115);
            (doc, ev) = Place(r, doc, "energy.mobility.ev", 149, 150);
            (doc, hp) = Place(r, doc, "energy.heat.heatpump", 159, 112);
            doc = Connect(r, doc, "energy.line.cable.lv", grid, "poc", meter, "a");
            doc = Connect(r, doc, "energy.line.cable.lv", meter, "b", bus, "t3");
            doc = Connect(r, doc, "energy.line.cable.lv", bus, "t1", inv, "ac");
            doc = Connect(r, doc, "energy.line.cable.dc", inv, "dc", pv, "dc");
            doc = Connect(r, doc, "energy.line.cable.lv", bus, "t2", binv, "ac");
            doc = Connect(r, doc, "energy.line.cable.dc", binv, "dc", bat, "dc");
            doc = Connect(r, doc, "energy.line.cable.lv", bus, "t3", house, "ac");
            doc = Connect(r, doc, "energy.line.cable.lv", bus, "t4", wb, "ac");
            doc = Connect(r, doc, "energy.line.cable.dc", wb, "ev", ev, "charge");
            doc = Connect(r, doc, "energy.line.cable.lv", bus, "t5", hp, "el");
            return doc;
        }

        /// <summary>20 kV infeed → breaker → transformer 20/0.4 kV → LV busbar → feeders.</summary>
        internal static SketchDocument MvLvDiagram(Registry r)
        {
            var doc = SketchDocument.Create();
            string grid, cb, tr, bus, q1, q2, q3, load, inv, pv, wb;
            (doc, grid) = Place(r, doc, "energy.grid.infeed", 86, 10);
            (doc, cb) = Place(r, doc, "energy.grid.breaker", 84, 36, null, ("name", "Q0"));
            (doc, tr) = Place(r, doc, "energy.grid.transformer.sld", 87, 64, null, ("name", "T1"));
            (doc, bus) = Place(r, doc, "energy.grid.busbar", 80, 90, new Size2(120, 8), ("name", "LV"));
            (doc, q1) = Place(r, doc, "energy.grid.disconnector.fuse", 36, 112, null, ("name", "F1"));
            (doc, q2) = Place(r, doc, "energy.grid.breaker", 84, 112, null, ("name", "Q2"));
            (doc, q3) = Place(r, doc, "energy.grid.disconnector.loadbreak", 132, 112, null, ("name", "Q3"), ("closed", false));
            (doc, load) = Place(r, doc, "energy.load.building.sld", 38, 140);
            (doc, inv) = Place(r, doc, "energy.conv.inverter.sld", 88, 140);
            (doc, pv) = Place(r, doc, "energy.pv.array.sld", 86, 166);
            (doc, wb) = Place(r, doc, "energy.mobility.charger.sld", 135, 140);
            doc = Connect(r, doc, "energy.line.cable.mv", grid, "poc", cb, "a");
            doc = Connect(r, doc, "energy.line.cable.mv", cb, "b", tr, "hv");
            doc = Connect(r, doc, "energy.line.cable.lv", tr, "lv", bus, "t3");
            doc = Connect(r, doc, "energy.line.cable.lv", bus, "t1", q1, "a");
            doc = Connect(r, doc, "energy.line.cable.lv", bus, "t3", q2, "a");
            doc = Connect(r, doc, "energy.line.cable.lv", bus, "t5", q3, "a");
            doc = Connect(r, doc, "energy.line.cable.lv", q1, "b", load, "ac");
            doc = Connect(r, doc, "energy.line.cable.lv", q2, "b", inv, "ac");
            doc = Connect(r, doc, "energy.line.cable.dc", inv, "dc", pv, "dc");
            doc = Connect(r, doc, "energy.line.cable.lv", q3, "b", wb, "ac");
            return doc;
        }

        [Fact]
        public void SampleSystemsStayConnectedInBothViews()
        {
            var r = Fx.Reg();
            foreach (var sample in new[] { HomeSystem(r), MvLvDiagram(r) })
                foreach (var view in EnViews.All)
                {
                    var doc = SwitchAll(r, sample, view);
                    foreach (var cable in doc.Objects.Values.Where(o => o.IsPointGeometry))
                    {
                        var pts = doc.ResolvePoints(cable);
                        for (var i = 0; i < 2; i++)
                        {
                            var a = cable.Attach![i == 0 ? 0 : pts.Count - 1]!;
                            var port = PortOf(r, doc, a.ObjectId, a.AnchorId!);
                            Assert.True(pts[i == 0 ? 0 : pts.Count - 1].DistanceTo(port) < 1e-6, $"{view}: cable end {i} off port {a.AnchorId}");
                        }
                    }
                }
        }
    }
}
