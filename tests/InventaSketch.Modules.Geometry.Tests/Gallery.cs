using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Geometry.Tests
{
    /// <summary>
    /// Test-only helper that writes an SVG gallery (every library item in English and German, stretched variants, the
    /// choices of the main parameters and a composed teaching slide) to the test output folder ("gallery/") for visual
    /// review. The dashed light-blue frame is the object box; ports are marked with small red circles.
    /// </summary>
    public class GalleryTests
    {
        private const double PxPerMm = 6;

        private static string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);

        private static string Cell(Registry r, SketchObject o, string label, Lang lang = Lang.En)
        {
            var type = r.TypeOf(o);
            var ctx = new RenderContext(SketchDocument.Create(), lang);
            var prims = r.RenderLocal(o, ctx).ToList();
            Rect vb;
            if (o.IsPointGeometry) vb = (Prim.Bounds(prims) ?? new Rect(0, 0, 1, 1)).Inflate(3);
            else
            {
                double w = o.Size.W, h = o.Size.H;
                prims.Insert(0, Prim.Rect(-w / 2, -h / 2, w, h, new Style { Stroke = "#c8d6e5", StrokeWidth = 0.08, Dash = DashStyle.Dash }));
                vb = (Prim.Bounds(prims) ?? new Rect(-w / 2, -h / 2, w, h)).Union(new Rect(-w / 2, -h / 2, w, h)).Inflate(1);
            }
            foreach (var a in type.Anchors(o, ctx).Where(a => a.Role == AnchorRole.Port && a.Kind == AnchorKind.Point))
                prims.Add(Prim.Circle(a.P.X, a.P.Y, 0.45, new Style { Stroke = "#e03030", StrokeWidth = 0.12 }));
            var px = Math.Max(2.5, Math.Min(PxPerMm, 420 / Math.Max(vb.W, vb.H)));
            var svg = SvgRenderer.RenderPrimitives(prims, new SvgOptions { Background = null, ViewBox = vb });
            svg = svg.Replace("<svg ", $"<svg width=\"{F(vb.W * px)}\" height=\"{F(vb.H * px)}\" ");
            return $"<figure>{svg}<figcaption>{SvgRenderer.Esc(label)}</figcaption></figure>\n";
        }

        private static string Page(string title, string body) =>
            "<!DOCTYPE html><html><head><meta charset=\"utf-8\"><title>" + SvgRenderer.Esc(title) + "</title><style>" +
            "body{font-family:Arial,sans-serif;margin:12px;background:#fff}h1{font-size:18px}h2{font-size:14px;margin:14px 0 4px}" +
            ".grid{display:flex;flex-wrap:wrap;gap:10px;align-items:flex-end}" +
            "figure{margin:0;padding:4px;border:1px solid #eee}figcaption{font-size:10px;color:#444;max-width:300px}" +
            "</style></head><body><h1>" + SvgRenderer.Esc(title) + "</h1>\n" + body + "</body></html>\n";

        /// <summary>Parameter variations shown per type (key, values).</summary>
        private static readonly Dictionary<string, (string Key, ParamValue[] Values)[]> Variations = new()
        {
            ["math.graph.function"] = new[] { ("function", new ParamValue[] { "cubic", "cosine", "logarithm", "sqrt", "abs", "reciprocal" }) },
            ["math.shape.triangle"] = new[] { ("construction", new ParamValue[] { "medians", "altitudes" }), ("kind", new ParamValue[] { "isosceles", "equilateral" }) },
            ["math.shape.polygon"] = new[] { ("sides", new ParamValue[] { 3, 5, 8 }) },
            ["math.circle.basic"] = new[] { ("show", new ParamValue[] { "diameter", "both" }) },
            ["math.circle.unit"] = new[] { ("angle", new ParamValue[] { 130.0, 225.0 }), ("tan", new ParamValue[] { true }), ("values", new ParamValue[] { true }) },
            ["math.lines.parallel"] = new[] { ("pair", new ParamValue[] { "alternate", "cointerior", "all" }) },
            ["math.angle.mark"] = new[] { ("mark", new ParamValue[] { "double", "right" }), ("reflex", new ParamValue[] { true }) },
            ["math.solid.cube"] = new[] { ("projection", new ParamValue[] { "cavalier" }) },
            ["math.solid.cylinder"] = new[] { ("projection", new ParamValue[] { "cavalier" }) },
            ["math.solid.cone"] = new[] { ("projection", new ParamValue[] { "cavalier" }) },
            ["math.solid.sphere"] = new[] { ("projection", new ParamValue[] { "cavalier" }) },
            ["math.solid.pyramid"] = new[] { ("projection", new ParamValue[] { "cavalier" }) },
            ["math.arc.sector"] = new[] { ("kind", new ParamValue[] { "segment" }), ("angleLabel", new ParamValue[] { "alpha" }) },
            ["math.shape.pythagoras"] = new[] { ("grid", new ParamValue[] { true }), ("areas", new ParamValue[] { "values" }) },
            ["math.venn.diagram"] = new[] { ("sets", new ParamValue[] { 3 }) },
            ["math.tree.probability"] = new[] { ("levels", new ParamValue[] { 1, 3 }), ("branches", new ParamValue[] { 3 }) },
            ["math.vector.arrow"] = new[] { ("notation", new ParamValue[] { "bold", "plain" }) },
            ["math.axes.polar"] = new[] { ("unit", new ParamValue[] { "rad" }), ("step", new ParamValue[] { "45" }) },
            ["math.axes.spatial"] = new[] { ("names", new ParamValue[] { "indexed" }) },
            ["math.point.labelled"] = new[] { ("position", new ParamValue[] { "sw", "e" }), ("marker", new ParamValue[] { "cross" }) },
        };

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
                foreach (var item in r.Library.Where(i => i.Category == cat.Id))
                {
                    body.Append("<h2>").Append(SvgRenderer.Esc($"{item.Name.En} / {item.Name.De}  [{item.Id}]")).Append("</h2><div class=\"grid\">\n");
                    var o = r.Create(item, Vec2.Zero);
                    body.Append(Cell(r, o, "EN"));
                    body.Append(Cell(r, o, "DE", Lang.De));
                    if (!o.IsPointGeometry)
                    {
                        body.Append(Cell(r, o with { Size = new Size2(o.Size.W * 1.6, o.Size.H * 0.7) }, "stretched 1.6 x 0.7"));
                        body.Append(Cell(r, o with { Size = new Size2(o.Size.W * 0.6, o.Size.H * 1.2) }, "squashed 0.6 x 1.2"));
                    }
                    if (item.Id == item.TypeId && Variations.TryGetValue(item.TypeId, out var vars))
                        foreach (var (key, values) in vars)
                            foreach (var v in values)
                                body.Append(Cell(r, r.SetParam(o, key, v), $"{key} = {v}", Lang.De));
                    body.Append("</div>\n");
                }
                var file = cat.Id.Replace("math.", "") + ".html";
                File.WriteAllText(Path.Combine(dir, file), Page(cat.Name.En + " / " + cat.Name.De, body.ToString()), Encoding.UTF8);
                index.Append("<p><a href=\"").Append(file).Append("\">").Append(SvgRenderer.Esc(cat.Name.En + " / " + cat.Name.De)).Append("</a></p>\n");
            }

            foreach (var lang in new[] { Lang.En, Lang.De })
            {
                var doc = TeachingSlide(r);
                var ctx = new RenderContext(doc, lang);
                var vb = (Prim.Bounds(doc.ObjectsOn(doc.Pages[0]).SelectMany(o => r.Render(o, ctx)).ToList()) ?? new Rect(0, 0, 100, 100)).Inflate(4);
                var page = SvgRenderer.RenderPage(r, doc, doc.Pages[0], new SvgOptions { ViewBox = vb }, lang);
                page = page.Replace($"width=\"{F(vb.W)}mm\" height=\"{F(vb.H)}mm\"", $"width=\"{F(vb.W * 4)}\" height=\"{F(vb.H * 4)}\"");
                index.Append("<h2>Teaching slide (docked vectors, point and angle) – ").Append(lang).Append("</h2>").Append(page);
            }
            File.WriteAllText(Path.Combine(dir, "index.html"), Page("InventaSketch math & geometry gallery", index.ToString()), Encoding.UTF8);
            Assert.True(File.Exists(Path.Combine(dir, "index.html")));
        }

        /// <summary>
        /// A coordinate system with a vector docked from the origin to the grid point (3|2), a second vector from there to
        /// (5|−1), a labelled point on (5|−1), the resultant drawn as a vector sum (head to tail) and the angle φ, all docked on grid points.
        /// </summary>
        internal static SketchDocument TeachingSlide(Registry r)
        {
            var doc = SketchDocument.Create();
            var page = doc.Pages[0].Id;
            var axes = Fx.Item(r, "math.axes.cartesian", new Vec2(120, 90)) with { Size = new Size2(120, 100) };
            doc = Edits.Add(doc, page, axes);
            Vec2 G(string id) => r.PageAnchors(doc.Get(axes.Id)!, new RenderContext(doc)).Single(a => a.Id == id).P;
            var sum = Fx.Item(r, "math.vector.sum.tail") with { Points = new[] { G("origin"), G("g3_2"), G("g5_-1") } };
            doc = Edits.Add(doc, page, sum);
            doc = Edits.AttachPoint(doc, sum.Id, 0, axes.Id, G("origin"), "origin");
            doc = Edits.AttachPoint(doc, sum.Id, 1, axes.Id, G("g3_2"), "g3_2");
            doc = Edits.AttachPoint(doc, sum.Id, 2, axes.Id, G("g5_-1"), "g5_-1");
            var point = Fx.Item(r, "math.point.labelled", G("g5_-1")).WithParam("label", "P(5|−1)").WithParam("position", "se") with { Size = new Size2(30, 12) };
            doc = Edits.Add(doc, page, point);
            var angle = Fx.Item(r, "math.angle.mark").WithParam("rays", false).WithParam("label", "φ").WithParam("radius", 8.0)
                with { Points = new[] { G("g4_0"), G("origin"), G("g3_2") } };
            doc = Edits.Add(doc, page, angle);
            doc = Edits.AttachPoint(doc, angle.Id, 0, axes.Id, G("g4_0"), "g4_0");
            doc = Edits.AttachPoint(doc, angle.Id, 1, axes.Id, G("origin"), "origin");
            return Edits.AttachPoint(doc, angle.Id, 2, axes.Id, G("g3_2"), "g3_2");
        }

        [Fact]
        public void DockedVectorsFollowTheCoordinateSystem()
        {
            var r = Fx.Reg();
            var doc = TeachingSlide(r);
            var axes = doc.Objects.Values.Single(o => o.Type == "math.axes.cartesian");
            // Move the axes: everything docked follows.
            doc = Attachments.Refresh(r, Edits.Move(doc, new[] { axes.Id }, new Vec2(10, 5)));
            axes = doc.Get(axes.Id)!;
            var sum = doc.Objects.Values.Single(o => o.Type == "math.vector.sum");
            var anchors = r.PageAnchors(axes, new RenderContext(doc)).Where(a => a.Id is not null).ToDictionary(a => a.Id!, a => a.P);
            var pts = doc.ResolvePoints(sum);
            Assert.True(pts[0].DistanceTo(anchors["origin"]) < 1e-9);
            Assert.True(pts[1].DistanceTo(anchors["g3_2"]) < 1e-9);
            Assert.True(pts[2].DistanceTo(anchors["g5_-1"]) < 1e-9);
            var angle = doc.Objects.Values.Single(o => o.Type == "math.angle.mark");
            Assert.True(doc.ResolvePoints(angle)[1].DistanceTo(anchors["origin"]) < 1e-9);
            // Free scaling of the axes keeps the vector ends on their grid points.
            doc = Edits.Scale(doc, new[] { axes.Id }, new Vec2(axes.Transform.X, axes.Transform.Y), 1.5, 0.8);
            doc = Attachments.Refresh(r, doc);
            anchors = r.PageAnchors(doc.Get(axes.Id)!, new RenderContext(doc)).Where(a => a.Id is not null).ToDictionary(a => a.Id!, a => a.P);
            pts = doc.ResolvePoints(doc.Get(sum.Id)!);
            Assert.True(pts[1].DistanceTo(anchors["g3_2"]) < 1e-9);
            Assert.True(pts[2].DistanceTo(anchors["g5_-1"]) < 1e-9);
        }
    }
}
