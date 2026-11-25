using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Energy.Tests
{
    public class RenderTests
    {
        [Fact]
        public void EveryLibraryItemRendersInEveryViewInsideItsBox()
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
                        var o = r.WithView(t.Create(Vec2.Zero), v.Id, Fx.Ctx()).WithParam("name", "Q12");
                        o = o with { Size = new Size2(o.Size.W * sx, o.Size.H * sy) };
                        Fx.AssertInsideBox(t.Render(o, Fx.Ctx()), o.Size, $"{t.Id} [{v.Id}] {sx}x{sy}");
                    }
        }

        private static IEnumerable<ParamValue> Variants(ParamDef p) => p.Kind switch
        {
            ParamKind.Choice => p.Options!.Select(x => (ParamValue)x.Value),
            ParamKind.Boolean => new ParamValue[] { true, false },
            ParamKind.Integer => new ParamValue[] { p.Min ?? 0, p.Max ?? 10 },
            ParamKind.Number => new ParamValue[] { p.Min ?? 0, p.Max ?? 1000 },
            _ => new ParamValue[] { "", "A very long designation 123" },
        };

        [Fact]
        public void ParameterVariantsRenderInsideTheBox()
        {
            var r = Fx.Reg();
            foreach (var t in Fx.BoxTypes(r))
                foreach (var p in t.Parameters)
                    foreach (var val in Variants(p))
                        foreach (var v in t.Views)
                        {
                            var o = r.WithView(t.Create(Vec2.Zero), v.Id, Fx.Ctx()).WithParam(p.Key, val);
                            Fx.AssertInsideBox(t.Render(o, Fx.Ctx(Lang.De)), o.Size, $"{t.Id}.{p.Key}={val} [{v.Id}]");
                        }
        }

        /// <summary>A different, valid value for a parameter.</summary>
        private static ParamValue Other(ParamDef p, SketchObject o)
        {
            switch (p.Kind)
            {
                case ParamKind.Boolean: return !o.Flag(p.Key, p.Default?.Bool ?? false);
                case ParamKind.Choice: return p.Options!.First(x => x.Value != o.Str(p.Key, p.Default?.Text ?? "")).Value;
                case ParamKind.Text: return o.Str(p.Key, "") == "" ? "X7" : "";
                case ParamKind.Integer:
                    {
                        var cur = o.Int(p.Key, 0);
                        return cur + 1 <= (p.Max ?? 100) ? cur + 1 : cur - 1;
                    }
                default:
                    {
                        var cur = o.Num(p.Key, 0);
                        var next = cur * 1.7 + 1;
                        return next <= (p.Max ?? double.MaxValue) ? next : (p.Min ?? 0);
                    }
            }
        }

        [Fact]
        public void EveryParameterChangesTheDrawingOrThePorts()
        {
            var r = Fx.Reg();
            foreach (var t in r.Types)
                foreach (var p in t.Parameters)
                {
                    // Hops only show where the line crosses another one (engine CrossModuleHopTests).
                    if (p.Key == "hops") continue;
                    // Engineering values (Calc 4) show only in the values label, which is off and empty by default.
                    if (p.Key == ValueLabels.ShowParam || p.Kind == ParamKind.Text && ValueModel.Variables(t).Any(v => v.ParamKey == p.Key)) continue;
                    var baseObj = t.Create(Vec2.Zero);
                    // Lines: a diagonal run, so routing has something to change.
                    if (baseObj.IsPointGeometry) baseObj = baseObj with { Points = new[] { new Vec2(-15, 0), new Vec2(15, 10) } };
                    // With a designation the label switch has something to show for every type.
                    if (p.Key != "name" && t.Parameters.Any(x => x.Key == "name")) baseObj = baseObj.WithParam("name", "N1");
                    // Line labels: give pipes and cables something to label so their label switches show.
                    if (p.Key == "showVoltage" || p.Key == "showCode") baseObj = baseObj.WithParam("label", "L1");
                    var changed = baseObj.WithParam(p.Key, Other(p, baseObj));
                    var differs = t.Views.Any(v =>
                    {
                        var a = r.WithView(baseObj, v.Id, Fx.Ctx());
                        var b = r.WithView(changed, v.Id, Fx.Ctx());
                        return Fx.Signature(r, a) != Fx.Signature(r, b);
                    });
                    Assert.True(differs, $"{t.Id}.{p.Key} does not change the drawing in any view");
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
        public void TheTwoViewsDrawDifferentSymbols()
        {
            var r = Fx.Reg();
            foreach (var t in Fx.BoxTypes(r))
            {
                var o = t.Create(Vec2.Zero);
                var drawn = t.Views.Select(v => string.Join("|", t.Render(r.WithView(o, v.Id, Fx.Ctx()), Fx.Ctx()).Select(p => p.GetType().Name + ":" + p.Tag))).ToList();
                Assert.True(drawn.Distinct().Count() == 2, t.Id + ": views must differ");
            }
        }

        [Fact]
        public void SwitchingTheViewKeepsTheVisualScale()
        {
            var r = Fx.Reg();
            var tr = Fx.Item(r, "energy.grid.transformer");
            Assert.Equal(new Size2(30, 34), tr.Size);
            var sld = r.WithView(tr, "single-line", Fx.Ctx());
            Assert.Equal(new Size2(38, 26), sld.Size);
            var big = r.WithView(tr with { Size = new Size2(60, 68) }, "single-line", Fx.Ctx());
            Assert.Equal(76, big.Size.W, 6);
            Assert.Equal(52, big.Size.H, 6);
            Assert.Equal(tr.Size, r.WithView(sld, "pictorial", Fx.Ctx()).Size);

            // A busbar's width is its length: it is kept, only the height follows the view.
            var bus = Fx.Item(r, "energy.grid.busbar") with { Size = new Size2(170, 8) };
            Assert.Equal(new Size2(170, 10), r.WithView(bus, "pictorial", Fx.Ctx()).Size);
        }

        [Fact]
        public void LabelsFollowTheUiLanguage()
        {
            var r = Fx.Reg();
            var infeed = Fx.Item(r, "energy.grid.infeed");
            Assert.Contains("Grid", Fx.Texts(r, infeed, Lang.En));
            Assert.Contains("Netz", Fx.Texts(r, infeed, Lang.De));
            var tr = Fx.Item(r, "energy.grid.transformer");
            Assert.Contains("20/0.4 kV", Fx.Texts(r, tr, Lang.En));
            Assert.Contains("20/0,4 kV", Fx.Texts(r, tr, Lang.De));
            Assert.Contains("630 kVA", Fx.Texts(r, tr));
            var hp = Fx.Item(r, "energy.heat.heatpump.sld");
            Assert.Contains("HP", Fx.Texts(r, hp, Lang.En));
            Assert.Contains("WP", Fx.Texts(r, hp, Lang.De));
            var pipe = Fx.Item(r, "energy.line.pipe.heat") with { View = "single-line" };
            Assert.Contains(r.Render(pipe, Fx.Ctx(Lang.De)), p => p is TextPrim { Text: "Wärme" });
        }

        [Fact]
        public void TransformerSingleLineShowsOneVoltagePerWindingInItsLevelColour()
        {
            var r = Fx.Reg();
            var tr = Fx.Item(r, "energy.grid.transformer.sld");
            var texts = Fx.Texts(r, tr).ToList();
            Assert.Equal(new[] { "20 kV", "630 kVA", "0.4 kV" }, texts);
            var prims = r.Render(tr, Fx.Ctx());
            Assert.Equal(Voltage.Color(VoltageLevel.Medium), ((EllipsePrim)prims.Single(p => p.Tag == "winding-hv")).Style.Stroke);
            Assert.Equal(Voltage.Color(VoltageLevel.Low), ((EllipsePrim)prims.Single(p => p.Tag == "winding-lv")).Style.Stroke);
            var black = r.Render(tr.WithParam("levelColors", false), Fx.Ctx());
            Assert.Equal("#000000", ((EllipsePrim)black.Single(p => p.Tag == "winding-hv")).Style.Stroke);

            var named = tr.WithParam("name", "T1").WithParam("vector", "Dyn5");
            Assert.Equal(new[] { "T1", "20 kV", "630 kVA Dyn5", "0.4 kV" }, Fx.Texts(r, named));
            Assert.Empty(Fx.Texts(r, named.WithParam("showLabel", false)));
        }

        [Fact]
        public void RatingsUseReadableUnitPrefixes()
        {
            var r = Fx.Reg();
            Assert.Contains("40 MVA", Fx.Texts(r, Fx.Item(r, "energy.grid.transformer.hvmv")));
            Assert.Contains("2 MWh", Fx.Texts(r, Fx.Item(r, "energy.storage.battery.container")));
            Assert.Contains("10 MWp", Fx.Texts(r, Fx.Item(r, "energy.pv.park")));
            Assert.Contains("2,58 kWp", Fx.Texts(r, Fx.Item(r, "energy.pv.string"), Lang.De));
            Assert.Contains("6 × 430 Wp", Fx.Texts(r, Fx.Item(r, "energy.pv.string")));
            Assert.Contains("150 kW DC", Fx.Texts(r, Fx.Item(r, "energy.mobility.charger.hpc")));
            Assert.Contains("800 V DC", Fx.Texts(r, Fx.Item(r, "energy.grid.busbar.dc")));
            Assert.Contains("110 kV", Fx.Texts(r, Fx.Item(r, "energy.grid.busbar.hv")));
        }

        [Fact]
        public void TheWindTurbineAnimatesItsRotor()
        {
            var r = Fx.Reg();
            var wt = Fx.Item(r, "energy.wind.turbine");
            var frames = r.TypeOf(wt).AnimationFrames(wt, 3);
            Assert.Equal(new[] { 0.0, 40.0, 80.0 }, frames.Select(f => f.Num("rotor", -1)));
            Assert.All(frames, f => Assert.Equal(wt.Size, f.Size));
            Assert.Empty(r.TypeOf(Fx.Item(r, "energy.grid.meter")).AnimationFrames(Fx.Item(r, "energy.grid.meter"), 3));
        }

        [Fact]
        public void SwitchStatesAreDrawn()
        {
            var r = Fx.Reg();
            Vec2 BladeEnd(SketchObject o) => ((LinePrim)r.Render(o, Fx.Ctx()).Single(p => p.Tag == "blade")).B;
            var cb = Fx.Item(r, "energy.grid.breaker");
            Assert.Equal(0, BladeEnd(cb).X, 6); // closed: straight to the contact
            Assert.True(BladeEnd(cb.WithParam("closed", false)).X < -4);
            var es = Fx.Item(r, "energy.grid.earthing");
            Assert.True(BladeEnd(es).X < -4); // earthing switches default to open
            var toggle = r.TypeOf(cb).Actions(cb, Lang.En).Single(a => a.Id == "energy.switch.toggle");
            Assert.False(toggle.Apply(cb).Flag("closed", true));
            var square = Fx.Item(r, "energy.grid.breaker.square");
            Assert.Equal("#000000", ((RectPrim)r.Render(square, Fx.Ctx()).Single(p => p.Tag == "breaker")).Style.Fill!.Color);
        }

        [Fact]
        public void StyleOverrideRecoloursStrokesAndText()
        {
            var r = Fx.Reg();
            var tr = Fx.Item(r, "energy.grid.transformer") with { Style = new Style { Stroke = "#ff0000" } };
            var prims = Prim.Flatten(r.Render(tr, Fx.Ctx())).ToList();
            Assert.All(prims.OfType<TextPrim>(), t => Assert.Equal("#ff0000", t.TextStyle.Color));
            Style? StyleOf(Primitive p) => p switch { RectPrim re => re.Style, PathPrim pa => pa.Style, EllipsePrim e => e.Style, LinePrim l => l.Style, _ => null };
            Assert.Contains(prims, p => StyleOf(p)?.Fill?.Color == "#ffffff"); // the white nameplate keeps its fill
            Assert.Contains(prims, p => StyleOf(p)?.Stroke == "#ff0000");
        }
    }
}
