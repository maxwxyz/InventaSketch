using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.BuildingServices.Tests
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
                foreach (var v in t.Views)
                {
                    var o = Fx.InView(r, item, v.Id);
                    Assert.Equal(v.Id, t.ViewOf(o));
                    foreach (var lang in new[] { Lang.En, Lang.De })
                    {
                        var prims = t.Render(o, Fx.Ctx(lang));
                        var what = $"{item.Id} [{v.Id}, {lang}]";
                        if (o.IsPointGeometry)
                        {
                            Assert.NotEmpty(prims);
                            Fx.AssertFinite(prims, what);
                        }
                        else Fx.AssertInsideBox(prims, o.Size, what);
                    }
                }
            }
        }

        [Fact]
        public void StretchedAndSquashedBoxesStayInsideTheirBox()
        {
            var r = Fx.Reg();
            foreach (var t in Fx.BoxTypes(r))
                foreach (var v in t.Views)
                    foreach (var (sx, sy) in new[] { (2.0, 0.6), (0.5, 1.8), (3.0, 3.0), (0.3, 0.3) })
                    {
                        var o = r.WithView(t.Create(Vec2.Zero), v.Id, Fx.Ctx());
                        o = o with { Size = new Size2(o.Size.W * sx, o.Size.H * sy) };
                        Fx.AssertInsideBox(t.Render(o, Fx.Ctx()), o.Size, $"{t.Id} [{v.Id}] {sx}x{sy}");
                    }
        }

        [Fact]
        public void ParameterVariantsRenderInsideTheBox()
        {
            var r = Fx.Reg();
            foreach (var t in Fx.BoxTypes(r))
                foreach (var p in t.Parameters.Where(p => p.Kind is ParamKind.Choice or ParamKind.Boolean or ParamKind.Integer))
                {
                    var values = p.Kind switch
                    {
                        ParamKind.Choice => p.Options!.Select(x => (ParamValue)x.Value).ToArray(),
                        ParamKind.Boolean => new ParamValue[] { true, false },
                        _ => new ParamValue[] { p.Min ?? 1, p.Max ?? 4 },
                    };
                    foreach (var val in values)
                        foreach (var v in t.Views)
                        {
                            var o = r.WithView(t.Create(Vec2.Zero), v.Id, Fx.Ctx()).WithParam(p.Key, val);
                            Fx.AssertInsideBox(t.Render(o, Fx.Ctx(Lang.De)), o.Size, $"{t.Id}.{p.Key}={val} [{v.Id}]");
                        }
                }
        }

        [Fact]
        public void ScalingChangesSizeNotParameters()
        {
            var r = Fx.Reg();
            foreach (var t in Fx.BoxTypes(r))
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
        public void ViewsDrawDifferentSymbols()
        {
            var r = Fx.Reg();
            foreach (var t in Fx.BoxTypes(r))
            {
                var o = t.Create(Vec2.Zero);
                var drawn = t.Views.Select(v =>
                {
                    var x = r.WithView(o, v.Id, Fx.Ctx());
                    // Normalised geometry signature (independent of the box size).
                    return string.Join("|", t.Render(x, Fx.Ctx()).Select(p => p.GetType().Name + ":" + p.Tag));
                }).ToList();
                Assert.True(drawn.Distinct().Count() == 3, t.Id + ": views must differ");
            }
        }

        [Fact]
        public void SwitchingTheViewKeepsTheWidthAndTakesTheRealHeight()
        {
            var r = Fx.Reg();
            var radiator = Fx.Item(r, "bs.heat.radiator.long");
            var section = r.WithView(radiator, "section", Fx.Ctx());
            Assert.Equal(16, section.Size.W, 6);
            Assert.Equal(6, section.Size.H, 6); // 600 mm at 1:100
            var at50 = r.WithView(radiator.WithParam("scale", 50.0) with { Size = new Size2(32, 2.4) }, "section", Fx.Ctx());
            Assert.Equal(32, at50.Size.W, 6);
            Assert.Equal(12, at50.Size.H, 6); // 600 mm at 1:50
            var back = r.WithView(section, "plan", Fx.Ctx());
            Assert.Equal(radiator.Size, back.Size);

            // Symbols keep their visual scale.
            var pump = Fx.Item(r, "bs.heat.pump") with { Size = new Size2(6, 6) };
            Assert.Equal(new Size2(6, 6), r.WithView(pump, "schematic", Fx.Ctx()).Size);
        }

        [Fact]
        public void LabelsFollowTheUiLanguage()
        {
            var r = Fx.Reg();
            string Texts(SketchObject o, Lang lang) => string.Join(",", r.TypeOf(o).Render(o, Fx.Ctx(lang)).OfType<TextPrim>().Select(t => t.Text));
            var hp = Fx.Item(r, "bs.heat.heatpump");
            Assert.Contains("HP", Texts(hp, Lang.En));
            Assert.Contains("WP", Texts(hp, Lang.De));
            var pipe = Fx.Item(r, "bs.pipe.hot") with { View = "schematic" };
            Assert.Contains("HW", Texts(pipe, Lang.En));
            Assert.Contains("WW", Texts(pipe, Lang.De));
            var duct = Fx.Item(r, "bs.duct.extract") with { View = "schematic" };
            Assert.Contains("ETA", Texts(duct, Lang.En));
            Assert.Contains("ABL", Texts(duct, Lang.De));
        }
    }
}
