using System;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Robotics.Tests
{
    public class RenderTests
    {
        [Fact]
        public void EveryLibraryItemRendersInEveryViewInsideItsBoxWithoutParameters()
        {
            var r = Fx.Reg();
            foreach (var item in r.Library)
            {
                var t = r.Type(item.TypeId)!;
                var views = t.Views.Count == 0 ? new string?[] { null } : t.Views.Select(v => (string?)v.Id).ToArray();
                foreach (var v in views)
                {
                    var o = r.WithView(r.Create(item, Vec2.Zero), v, Fx.Ctx());
                    if (v is not null) Assert.Equal(v, t.ViewOf(o));
                    foreach (var lang in new[] { Lang.En, Lang.De })
                    {
                        var prims = t.Render(o, Fx.Ctx(lang));
                        var what = $"{item.Id} [{v}, {lang}]";
                        if (o.IsPointGeometry) { Assert.NotEmpty(prims); Fx.AssertFinite(prims, what); }
                        else Fx.AssertInsideBox(prims, o.Size, what);
                    }
                }
            }
        }

        [Fact]
        public void StretchedAndSquashedBoxesStayInsideTheirBox()
        {
            var r = Fx.Reg();
            foreach (var t in r.Types.Where(t => t.Geometry == GeometryKind.Box))
                foreach (var v in t.Views.Count == 0 ? new string?[] { null } : t.Views.Select(x => (string?)x.Id))
                    foreach (var (sx, sy) in new[] { (2.0, 0.6), (0.5, 1.8), (3.0, 3.0), (0.3, 0.3) })
                    {
                        var o = r.WithView(t.Create(Vec2.Zero), v, Fx.Ctx());
                        o = o with { Size = new Size2(o.Size.W * sx, o.Size.H * sy) };
                        Fx.AssertInsideBox(t.Render(o, Fx.Ctx()), o.Size, $"{t.Id} [{v}] {sx}x{sy}");
                    }
        }

        [Fact]
        public void ParameterVariantsRenderInsideTheBox()
        {
            var r = Fx.Reg();
            foreach (var t in r.Types.Where(t => t.Geometry == GeometryKind.Box))
                foreach (var p in t.Parameters.Where(p => p.Kind is ParamKind.Choice or ParamKind.Boolean or ParamKind.Integer or ParamKind.Number))
                {
                    var values = p.Kind switch
                    {
                        ParamKind.Choice => p.Options!.Select(x => (ParamValue)x.Value).ToArray(),
                        ParamKind.Boolean => new ParamValue[] { true, false },
                        _ => new ParamValue[] { p.Min ?? 0, p.Max ?? 4, p.Default ?? 1 },
                    };
                    foreach (var val in values)
                        foreach (var v in t.Views.Count == 0 ? new string?[] { null } : t.Views.Select(x => (string?)x.Id))
                        {
                            var o = r.WithView(t.Create(Vec2.Zero), v, Fx.Ctx()).WithParam(p.Key, val);
                            Fx.AssertInsideBox(t.Render(o, Fx.Ctx(Lang.De)), o.Size, $"{t.Id}.{p.Key}={val} [{v}]");
                        }
                }
        }

        [Fact]
        public void ArmsStayInsideTheirBoxForAnyJointValues()
        {
            var r = Fx.Reg();
            var rnd = new Random(7);
            foreach (var t in Fx.Arms(r))
                foreach (var v in t.Views)
                    for (var k = 0; k < 40; k++)
                    {
                        var o = r.WithView(t.Create(Vec2.Zero), v.Id, Fx.Ctx());
                        foreach (var j in t.Joints) o = o.WithParam(j.Key, j.Min + (j.Max - j.Min) * rnd.NextDouble() * 1.4 - (j.Max - j.Min) * 0.2);
                        if (k % 3 == 0) o = o.WithParam("style", "schematic");
                        if (k % 4 == 0) o = o.WithParam("envelope", true).WithParam("labels", true).WithParam("ghosts", 3);
                        Fx.AssertInsideBox(t.Render(o, Fx.Ctx()), o.Size, $"{t.Id} [{v.Id}] #{k}");
                    }
        }

        [Fact]
        public void ScalingChangesSizeNotParameters()
        {
            var r = Fx.Reg();
            foreach (var t in r.Types.Where(t => t.Geometry == GeometryKind.Box))
            {
                var o = t.Create(new Vec2(50, 50));
                var (doc, _) = Fx.Doc(o);
                var scaled = Edits.Scale(doc, new[] { o.Id }, new Vec2(50, 50), 1.7, 0.6).Get(o.Id)!;
                Assert.Equal(o.Params.OrderBy(k => k.Key), scaled.Params.OrderBy(k => k.Key));
                Assert.NotEqual(o.Size, scaled.Size);
                Fx.AssertInsideBox(t.Render(scaled, Fx.Ctx()), scaled.Size, t.Id + " scaled");
            }
        }

        [Fact]
        public void ViewsDrawDifferentGeometry()
        {
            var r = Fx.Reg();
            foreach (var t in r.Types.Where(t => t.Views.Count > 1))
            {
                var o = t.Create(Vec2.Zero);
                var drawn = t.Views.Select(v =>
                {
                    var x = r.WithView(o, v.Id, Fx.Ctx());
                    var b = Prim.Bounds(t.Render(x, Fx.Ctx()))!.Value;
                    return string.Join("|", t.Render(x, Fx.Ctx()).Select(p => p.GetType().Name + ":" + p.Tag)) + "@" + Math.Round(b.W / b.H, 2);
                }).ToList();
                Assert.True(drawn.Distinct().Count() == t.Views.Count, t.Id + ": views must differ");
            }
        }

        [Fact]
        public void SchematicAndBodyStylesDiffer()
        {
            var r = Fx.Reg();
            foreach (var t in Fx.Arms(r))
            {
                var o = t.Create(Vec2.Zero);
                var body = t.Render(o, Fx.Ctx());
                var schem = t.Render(o.WithParam("style", "schematic"), Fx.Ctx());
                Assert.Contains(body, p => p is PathPrim pp && pp.IsClosed && pp.Tag == RoboTags.Link);
                Assert.Contains(schem, p => p is PathPrim pp && !pp.IsClosed && pp.Tag == RoboTags.Link);
                Assert.Contains(schem, p => p.Tag == RoboTags.Joint);
            }
        }

        [Fact]
        public void LabelsFollowTheUiLanguage()
        {
            var r = Fx.Reg();
            string Texts(SketchObject o, Lang lang) => string.Join(",", r.TypeOf(o).Render(o, Fx.Ctx(lang)).OfType<TextPrim>().Select(t => t.Text));
            var arm = Fx.Item(r, "robot.arm.articulated").WithParam("labels", true);
            Assert.Contains("J2", Texts(arm, Lang.En));
            Assert.Contains("A2", Texts(arm, Lang.De));
            var frame = Fx.Item(r, "robot.frame.coordinate.tool");
            Assert.Contains("Tool", Texts(frame, Lang.En));
            Assert.Contains("Werkzeug", Texts(frame, Lang.De));
            var zone = Fx.Item(r, "robot.safety.zone.collaborative");
            Assert.Contains("Collaborative workspace", Texts(zone, Lang.En));
            Assert.Contains("Kollaborationsraum", Texts(zone, Lang.De));
            var scanner = Fx.Item(r, "robot.safety.scanner").WithParam("labels", true);
            Assert.Contains("Schutzfeld", Texts(scanner, Lang.De));
        }

        [Fact]
        public void UnreachableDeltaPoseShowsANote()
        {
            var r = Fx.Reg();
            var delta = Fx.Item(r, "robot.arm.delta").WithParam("fore", 80.0);
            var prims = r.TypeOf(delta).Render(delta, Fx.Ctx());
            Assert.Contains(prims, p => p.Tag == RoboTags.NoReach);
            Fx.AssertInsideBox(prims, delta.Size, "delta unreachable");
        }

        [Fact]
        public void PointTypesDrawTheirGeometry()
        {
            var r = Fx.Reg();
            var fence = Fx.Item(r, "robot.safety.fence");
            var prims = r.TypeOf(fence).Render(fence, Fx.Ctx());
            // Default U-shaped fence: 36 + 60 + 36 mm at panels of at most 20 mm: 2 + 3 + 2 panels, 8 posts.
            Assert.Equal(8, prims.Count(p => p.Tag == RoboTags.Post));
            var closed = fence.WithParam("closed", true);
            Assert.Equal(10, r.TypeOf(closed).Render(closed, Fx.Ctx()).Count(p => p.Tag == RoboTags.Post));
            Assert.True(r.TypeOf(closed).IsClosed(closed));

            var zone = Fx.Item(r, "robot.safety.zone");
            var z = r.TypeOf(zone).Render(zone, Fx.Ctx());
            Assert.Contains(z, p => p is PathPrim pp && pp.Style.Fill?.Kind == FillKind.Hatch);
            Assert.Contains(z, p => p is TextPrim);
            var tint = zone.WithParam("fill", "tint");
            Assert.Contains(r.TypeOf(tint).Render(tint, Fx.Ctx()), p => p is PathPrim pp && pp.Style.Fill?.Kind == FillKind.Solid);
        }
    }
}
