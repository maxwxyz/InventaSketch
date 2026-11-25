using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Process.Tests
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
                        else Fx.AssertInsideBox(Fx.WithoutTag(prims, o), o.Size, what);
                    }
                }
            }
        }

        [Fact]
        public void TypesWithoutParametersRenderFromABareObject()
        {
            // Quick Sketch first: an object without any parameter (e.g. from an older document) still draws.
            var r = Fx.Reg();
            foreach (var t in r.Types)
            {
                var o = t.Create(new Vec2(5, 5)) with { Params = new Dictionary<string, ParamValue>() };
                var (doc, _) = Fx.Doc(o);
                var prims = t.Render(o, new RenderContext(doc));
                Assert.NotEmpty(prims);
                if (!o.IsPointGeometry) Fx.AssertInsideBox(Fx.WithoutTag(prims, o), o.Size, t.Id + " bare");
            }
        }

        [Fact]
        public void StretchedAndSquashedBoxesStayInsideTheirBox()
        {
            var r = Fx.Reg();
            foreach (var t in r.Types.Where(t => t.Geometry == GeometryKind.Box))
                foreach (var v in t.Views)
                    foreach (var (sx, sy) in new[] { (2.0, 0.6), (0.5, 1.8), (3.0, 3.0), (0.3, 0.3) })
                    {
                        var o = r.WithView(t.Create(Vec2.Zero), v.Id, Fx.Ctx());
                        o = o with { Size = new Size2(o.Size.W * sx, o.Size.H * sy) };
                        Fx.AssertInsideBox(Fx.WithoutTag(t.Render(o, Fx.Ctx()), o), o.Size, $"{t.Id} [{v.Id}] {sx}x{sy}");
                    }
        }

        [Fact]
        public void ParameterVariantsRenderInsideTheBoxAndChangeTheDrawing()
        {
            var r = Fx.Reg();
            foreach (var t in r.Types.Where(t => t.Geometry == GeometryKind.Box))
                foreach (var p in t.Parameters.Where(p => p.Kind is ParamKind.Choice or ParamKind.Boolean or ParamKind.Integer or ParamKind.Number))
                {
                    var values = p.Kind switch
                    {
                        ParamKind.Choice => p.Options!.Select(x => (ParamValue)x.Value).ToArray(),
                        ParamKind.Boolean => new ParamValue[] { true, false },
                        ParamKind.Integer => new ParamValue[] { (int)p.Min!.Value, (int)p.Max!.Value },
                        _ => new ParamValue[] { p.Min!.Value, p.Max!.Value },
                    };
                    var drawn = new HashSet<string>();
                    foreach (var val in values)
                    {
                        // Set like the inspector does (the box follows a design box that grew).
                        var o = r.SetParam(t.Create(Vec2.Zero), p.Key, val);
                        if (t.Id == "process.chart.pv" && p.Key == "cutoff") o = o.WithParam("cycle", "diesel");
                        if (t.Id == "process.chart.ts" && p.Key == "superheat") o = o.WithParam("cycle", "rankine");
                        if (t.Id == "process.column" && p.Key == "beds") o = o.WithParam("internals", "packed");
                        if (t.Id == "process.chart.pv" && p.Key == "efficiency") o = o.WithParam("caption", true);
                        if (p.Key == ProcessType.ShowTagKey) o = o.WithParam(ProcessType.TagKey, "X-1");
                        var prims = t.Render(o, Fx.Ctx(Lang.De));
                        Fx.AssertInsideBox(Fx.WithoutTag(prims, o), o.Size, $"{t.Id}.{p.Key}={val}");
                        drawn.Add(Fx.Signature(prims));
                    }
                    Assert.True(drawn.Count == values.Length, $"{t.Id}.{p.Key}: every value must change the drawing");
                }
        }

        [Fact]
        public void LineParametersChangeTheDrawing()
        {
            var r = Fx.Reg();
            foreach (var id in new[] { "process.line", "process.line.signal" })
            {
                var t = r.Type(id)!;
                var o = t.Create(Vec2.Zero) with { Points = new[] { new Vec2(0, 0), new Vec2(40, 0), new Vec2(40, 30) } };
                foreach (var p in t.Parameters.Where(p => p.Kind == ParamKind.Choice && p.Key != "route"))
                {
                    var drawn = p.Options!.Select(opt => Fx.Signature(t.Render(o.WithParam(p.Key, opt.Value), Fx.Ctx()))).ToList();
                    Assert.Equal(drawn.Count, drawn.Distinct().Count());
                }
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
                Fx.AssertInsideBox(Fx.WithoutTag(t.Render(scaled, Fx.Ctx()), scaled), scaled.Size, t.Id + " scaled");
            }
        }

        [Fact]
        public void ViewsDrawDifferently()
        {
            var r = Fx.Reg();
            foreach (var t in r.Types.Where(t => t.Views.Count > 1))
            {
                var o = t.Create(Vec2.Zero);
                var drawn = t.Views.Select(v => Fx.Signature(t.Render(r.WithView(o, v.Id, Fx.Ctx()), Fx.Ctx()))).ToList();
                Assert.Equal(t.Views.Count, drawn.Distinct().Count());
            }
        }

        [Fact]
        public void BlockViewNamesTheEquipmentInTheUiLanguageWithItsTag()
        {
            var r = Fx.Reg();
            var pump = r.WithView(Fx.Item(r, "process.pump.centrifugal").WithParam("tag", "P-201"), ProcessViews.Block, Fx.Ctx());
            string Texts(Lang lang) => string.Join(" ", r.TypeOf(pump).Render(pump, Fx.Ctx(lang)).OfType<TextPrim>().Select(x => x.Text));
            Assert.Contains("P-201", Texts(Lang.En));
            Assert.Contains("Centrifugal", Texts(Lang.En));
            Assert.Contains("Kreiselpumpe", Texts(Lang.De));
            Assert.Contains(r.TypeOf(pump).Render(pump, Fx.Ctx()), p => p is RectPrim && p.Tag == ProcessTags.Body);
        }

        [Fact]
        public void EquipmentTagIsShownOutsideTheBoxOnlyWhenSet()
        {
            var r = Fx.Reg();
            var vessel = Fx.Item(r, "process.vessel.vertical");
            var t = r.TypeOf(vessel);
            Assert.DoesNotContain(t.Render(vessel, Fx.Ctx()), p => p.Tag == ProcessTags.Tag);
            var shown = Fx.Action(r, vessel, "process.tag.show").Apply(vessel);
            Assert.Equal("V-101", shown.Str("tag", ""));
            var tag = t.Render(shown, Fx.Ctx()).OfType<TextPrim>().Single(p => p.Tag == ProcessTags.Tag);
            Assert.Equal("V-101", tag.Text);
            var box = new Rect(-shown.Size.W / 2, -shown.Size.H / 2, shown.Size.W, shown.Size.H);
            Assert.False(box.Contains(tag.Position), "tag sits outside the box");
            var hidden = Fx.Action(r, shown, "process.tag.show").Apply(shown);
            Assert.DoesNotContain(t.Render(hidden, Fx.Ctx()), p => p.Tag == ProcessTags.Tag);
            var input = Fx.Action(r, vessel, "process.tag.set");
            var set = input.Input!.Apply(vessel, " V-300 ");
            Assert.Equal("V-300", t.Render(set, Fx.Ctx()).OfType<TextPrim>().Single(p => p.Tag == ProcessTags.Tag).Text);
        }

        [Fact]
        public void TagStaysUprightOnTheRotatedSymbol()
        {
            var r = Fx.Reg();
            var pump = Fx.Item(r, "process.pump.centrifugal").WithParam("tag", "P-1");
            pump = pump with { Transform = pump.Transform with { Rotation = 90 } };
            var (doc, _) = Fx.Doc(pump);
            var page = r.Render(pump, new RenderContext(doc)).OfType<TextPrim>().Single(p => p.Tag == ProcessTags.Tag);
            Assert.Equal(0, page.Rotation % 360, 6);
        }

        [Fact]
        public void InstrumentBubbleShowsFunctionLettersAndLoopNumber()
        {
            var r = Fx.Reg();
            var bubble = Fx.Item(r, "process.instrument.bubble");
            var texts = r.TypeOf(bubble).Render(bubble, Fx.Ctx()).OfType<TextPrim>().Select(p => p.Text).ToList();
            Assert.Equal(new[] { "TIC", "101" }, texts);
            var pt = Fx.Action(r, bubble, "process.instrument.tag").Input!.Apply(bubble, "PT-205A");
            Assert.Equal(new[] { "PT", "205A" }, r.TypeOf(pt).Render(pt, Fx.Ctx()).OfType<TextPrim>().Select(p => p.Text));
            Assert.Equal(("FI", ""), Instruments.SplitTag("FI"));
            Assert.Equal(("TIC", "101"), Instruments.SplitTag(" TIC 101 "));
            Assert.Equal(("123", ""), Instruments.SplitTag("123"));
        }

        [Fact]
        public void InstrumentLocationDrawsTheMatchingBar()
        {
            var r = Fx.Reg();
            var bubble = Fx.Item(r, "process.instrument.bubble");
            int Bars(string location) => r.TypeOf(bubble).Render(bubble.WithParam("location", location), Fx.Ctx()).Count(p => p.Tag == "location");
            Assert.Equal(0, Bars("field"));
            Assert.Equal(1, Bars("central"));
            Assert.Equal(2, Bars("local"));
            var behind = r.TypeOf(bubble).Render(bubble.WithParam("location", "behind"), Fx.Ctx()).OfType<LinePrim>().Single(p => p.Tag == "location");
            Assert.Equal(DashStyle.Dash, behind.Style.Dash);
        }

        [Fact]
        public void ControlValveShowsTheFailSafePosition()
        {
            var r = Fx.Reg();
            var v = Fx.Item(r, "process.valve.control.fc");
            Assert.Contains(r.TypeOf(v).Render(v, Fx.Ctx()).OfType<TextPrim>(), p => p.Text == "FC");
        }

        [Fact]
        public void ColumnDrawsTheRequestedNumberOfTrays()
        {
            var r = Fx.Reg();
            var c = Fx.Item(r, "process.column");
            Assert.Equal(8, r.TypeOf(c).Render(c, Fx.Ctx()).Count(p => p.Tag == "tray"));
            var more = Fx.Action(r, c, "process.column.more").Apply(c);
            Assert.Equal(10, r.TypeOf(more).Render(more, Fx.Ctx()).Count(p => p.Tag == "tray"));
            Assert.Equal(c.Size, more.Size);
        }

        [Fact]
        public void AddingASupportGrowsTheBoxInsteadOfSquashingTheVessel()
        {
            var r = Fx.Reg();
            var v = Fx.Item(r, "process.vessel.vertical");
            var withLegs = r.SetParam(v, "support", "legs");
            Assert.Equal(v.Size.W, withLegs.Size.W, 6);
            Assert.True(withLegs.Size.H > v.Size.H);
            var viaAction = Fx.Action(r, v, "process.vessel.support=legs").Apply(v);
            Assert.Equal(withLegs.Size, viaAction.Size);
        }
    }
}
