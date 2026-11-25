using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Robotics.Tests
{
    /// <summary>
    /// Test-only helper that writes an SVG gallery (every type in each view, stretched variants, library presets, pose
    /// sequences and a sample work cell) to the test output folder ("gallery/") for visual review. Ports are marked with
    /// small red circles, mounts with blue squares, rails with dotted cyan lines.
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
            if (o.IsPointGeometry) vb = (Prim.Bounds(prims) ?? new Rect(0, 0, 1, 1)).Inflate(3);
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
                else if (a.Role == AnchorRole.Mount) prims.Add(Prim.CRect(a.P.X, a.P.Y, 0.7, 0.7, new Style { Stroke = "#3050e0", StrokeWidth = 0.1 }));
                else if (a.Role == AnchorRole.Rail) prims.Add(Prim.Line(a.P, a.Q, new Style { Stroke = "#30a0e0", StrokeWidth = 0.1, Dash = DashStyle.Dot }));
            }
            var px = Math.Max(4, Math.Min(PxPerMm, 300 / Math.Max(vb.W, vb.H)));
            var svg = SvgRenderer.RenderPrimitives(prims, new SvgOptions { Background = null, ViewBox = vb });
            svg = svg.Replace("<svg ", $"<svg width=\"{F(vb.W * px)}\" height=\"{F(vb.H * px)}\" ");
            return $"<figure>{svg}<figcaption>{SvgRenderer.Esc(label)}</figcaption></figure>\n";
        }

        private static string Page(string title, string body) =>
            "<!DOCTYPE html><html><head><meta charset=\"utf-8\"><title>" + SvgRenderer.Esc(title) + "</title><style>" +
            "body{font-family:Arial,sans-serif;margin:12px;background:#fff}h1{font-size:18px}h2{font-size:14px;margin:12px 0 4px}" +
            ".grid{display:flex;flex-wrap:wrap;gap:10px;align-items:flex-end}" +
            "figure{margin:0;padding:4px;border:1px solid #eee}figcaption{font-size:10px;color:#444;max-width:300px}" +
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
                    var views = t.Views.Count == 0 ? new string?[] { null } : t.Views.Select(v => (string?)v.Id).ToArray();
                    foreach (var v in views)
                    {
                        var o = r.WithView(baseObj, v, Fx.Ctx());
                        body.Append(Cell(r, o, v ?? "symbol"));
                        if (!o.IsPointGeometry) body.Append(Cell(r, o with { Size = new Size2(o.Size.W * 1.6, o.Size.H * 0.7) }, (v ?? "symbol") + " stretched 1.6 x 0.7", Lang.De));
                        if (t is RobotArmType)
                        {
                            body.Append(Cell(r, o.WithParam("style", "schematic").WithParam("labels", true), v + " schematic + labels"));
                            body.Append(Cell(r, o.WithParam("envelope", true), v + " envelope"));
                            body.Append(Cell(r, o.WithParam("ghosts", 4).WithParam("style", "schematic"), v + " ghosts"));
                        }
                    }
                    body.Append("</div>\n");
                }
                body.Append("<h2>Library presets</h2><div class=\"grid\">\n");
                foreach (var item in r.Library.Where(i => i.Category == cat.Id && i.Id != i.TypeId))
                    body.Append(Cell(r, r.Create(item, Vec2.Zero), item.Name.En + " / " + item.Name.De, Lang.De));
                body.Append("</div>\n");
                var file = cat.Id.Replace("robot.", "") + ".html";
                File.WriteAllText(Path.Combine(dir, file), Page(cat.Name.En, body.ToString()), Encoding.UTF8);
                index.Append("<p><a href=\"").Append(file).Append("\">").Append(SvgRenderer.Esc(cat.Name.En)).Append("</a></p>\n");
            }

            // Pose sequences.
            var seq = new StringBuilder("<h2>Articulated arm: J2 sweep and wrist poses</h2><div class=\"grid\">\n");
            var arm = Fx.Item(r, "robot.arm.articulated");
            foreach (var (j2, j3, j5, j1) in new[] { (-40.0, -60.0, 30.0, 0.0), (15, -25, 70, 0), (60, -10, 40, 0), (100, 30, -30, 0), (15, -25, 70, 45), (15, -25, 70, 180) })
                seq.Append(Cell(r, arm.WithParam("j2", j2).WithParam("j3", j3).WithParam("j5", j5).WithParam("j1", j1), $"j1={j1} j2={j2} j3={j3} j5={j5}"));
            var top = r.WithView(arm, "top", Fx.Ctx());
            foreach (var j1 in new[] { 0.0, 45, 135, -90 }) seq.Append(Cell(r, top.WithParam("j1", j1).WithParam("j4", 30.0), $"top j1={j1} j4=30"));
            seq.Append("</div><h2>Animation frames (articulated, 6 of 12)</h2><div class=\"grid\">\n");
            foreach (var f in r.TypeOf(arm).AnimationFrames(arm, 12).Where((_, i) => i % 2 == 0)) seq.Append(Cell(r, f, "j5=" + F(f.Num("j5", 0))));
            seq.Append("</div>\n");
            index.Append(seq);

            // Sample work cell (top view).
            var doc = SketchDocument.Create();
            var page = doc.Pages[0].Id;
            SketchObject At(string item, double x, double y, string? view = "top") => r.WithView(Fx.Item(r, item, new Vec2(x, y)), view, Fx.Ctx());
            var robot = r.SetParam(r.SetParam(At("robot.arm.articulated", 100, 90), "envelope", true), "j1", -30.0);
            // Dock the robot base on the pedestal centre (mount onto mount, as the snapping does).
            var baseAt = r.PageAnchors(robot, Fx.Ctx()).Single(a => a.Id == "base").P;
            robot = robot with { Transform = robot.Transform with { X = robot.Transform.X + 100 - baseAt.X, Y = robot.Transform.Y + 90 - baseAt.Y } };
            var objs = new[]
            {
                At("robot.safety.fence.closed", 100, 90, null) with { Points = new[] { new Vec2(55, 50), new Vec2(150, 50), new Vec2(150, 130), new Vec2(55, 130) } },
                At("robot.safety.zone.warning", 0, 0, null) with { Points = new[] { new Vec2(60, 55), new Vec2(145, 55), new Vec2(145, 125), new Vec2(60, 125) } },
                At("robot.cell.pedestal", 100, 90),
                robot,
                At("robot.cell.conveyor", 128, 110),
                At("robot.cell.pallet", 78, 112),
                At("robot.safety.door", 75, 139.4),
                At("robot.safety.curtain", 135, 130),
                At("robot.safety.estop", 160, 128),
            };
            doc = Edits.Add(doc, page, objs);
            var vb = new Rect(45, 40, 125, 105);
            var svgPage = SvgRenderer.RenderPage(r, doc, doc.Pages[0], new SvgOptions { ViewBox = vb });
            svgPage = svgPage.Replace($"width=\"{F(vb.W)}mm\" height=\"{F(vb.H)}mm\"", $"width=\"{F(vb.W * 6)}\" height=\"{F(vb.H * 6)}\"");
            index.Append("<h2>Sample work cell (top view)</h2>").Append(svgPage);

            File.WriteAllText(Path.Combine(dir, "index.html"), Page("InventaSketch robotics gallery", index.ToString()), Encoding.UTF8);
            Assert.True(File.Exists(Path.Combine(dir, "index.html")));
        }
    }
}
