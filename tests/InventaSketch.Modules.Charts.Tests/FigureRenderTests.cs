using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Charts.Tests
{
    /// <summary>Charts v2 stage 2: rendering, parts, anchors and actions of the figure chart.</summary>
    public class FigureRenderTests
    {
        public static readonly string[] Ids = { "chart.figure.phases", "chart.figure.timing", "chart.figure.temperature" };

        public static IEnumerable<object[]> PresetIds() => Ids.Select(i => new object[] { i });

        private static SketchObject Figure(FigureSpec spec, Size2? size = null)
            => new() { Id = "f", Type = FigureChart.TypeId, Size = size ?? new Size2(100, 80), Params = new Dictionary<string, ParamValue> { ["spec"] = spec.ToJson() } };

        private static List<Primitive> Flat(Registry r, SketchObject o, RenderContext? ctx = null) => Prim.Flatten(Fx.Render(r, o, ctx)).ToList();

        private static Chart Compose(Registry r, SketchObject o) => Fx.Type(r, o).Compose(o, Fx.Ctx());

        [Theory]
        [MemberData(nameof(PresetIds))]
        public void PresetsRenderInsideTheirBox(string id)
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, id);
            Assert.Equal(FigureChart.TypeId, o.Type);
            foreach (var lang in new[] { Lang.En, Lang.De })
                foreach (var mono in new[] { false, true })
                    Fx.AssertInsideBox(Fx.Render(r, o, Fx.Ctx(lang, mono)), o.Size, $"{id} {lang} mono={mono}");
            foreach (var size in new[] { new Size2(o.Size.W * 1.6, o.Size.H * 0.7), new Size2(o.Size.W * 0.5, o.Size.H * 1.5), new Size2(30, 25), new Size2(300, 240) })
                Fx.AssertInsideBox(Fx.Render(r, o with { Size = size }), size, $"{id} {size.W}x{size.H}");
            var tags = Flat(r, o).Select(p => p.Tag).ToList();
            foreach (var kind in new[] { ChartTags.Series, ChartTags.Ref, ChartTags.Axis })
                Assert.Contains(tags, t => ChartTags.Is(t, kind));
        }

        [Fact]
        public void ExampleFigureHasEveryElementOfTheCheckFigure()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "chart.figure.phases");
            var prims = Flat(r, o, Fx.Ctx(Lang.De));
            var texts = prims.OfType<TextPrim>().ToList();
            foreach (var n in new[] { "40", "41", "42", "43", "44", "45", "46" })
                Assert.Contains(texts, t => t.Text == n && ChartTags.Is(t.Tag, ChartTags.Numeral));
            foreach (var t in new[] { "Vorkonditionierung", "Volllastphase", "Grenztemperatur", "Zeitgewinn", "t0", "t1" })
                Assert.Contains(texts, x => x.Text == t);
            // Hatched region between the curves, dashed and solid temperature, a dimension arrow with two heads.
            var region = prims.OfType<PathPrim>().Single(p => p.Tag == "region.0");
            Assert.Equal(FillKind.Hatch, region.Style.EffectiveFill.Kind);
            var lines = prims.OfType<PathPrim>().Where(p => ChartTags.Is(p.Tag, ChartTags.Series)).ToDictionary(p => p.Tag!);
            Assert.Equal(DashStyle.Dash, lines["series.1"].Style.EffectiveDash);
            Assert.Equal(DashStyle.Solid, lines["series.2"].Style.EffectiveDash);
            var dim = prims.OfType<LinePrim>().Single(p => p.Tag == "note.2");
            Assert.Equal(ArrowHead.Triangle, dim.Style.ArrowStart);
            Assert.Equal(ArrowHead.Triangle, dim.Style.ArrowEnd);
            // Principle axes: arrows, no value tick labels on the y axes; monochrome scheme.
            Assert.All(prims.OfType<LinePrim>().Where(p => ChartTags.Is(p.Tag, ChartTags.Axis)), a => Assert.Equal(ArrowHead.Triangle, a.Style.ArrowEnd));
            Assert.DoesNotContain(prims, p => p.Tag is { } t && t.StartsWith("label.y", StringComparison.Ordinal));
            Assert.All(lines.Values, l => Assert.Equal(Palette.Black, l.Style.Stroke));
            // Rotated axis titles.
            Assert.Equal(2, texts.Count(t => t.Tag is { } tag && tag.StartsWith("axistitle.y", StringComparison.Ordinal) && Math.Abs(t.Rotation + 90) < 1e-9));
        }

        [Fact]
        public void VerticalMarkersSpanAllPanels()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "chart.figure.timing");
            var c = Compose(r, o);
            Assert.Equal(3, c.F.Panels.Count);
            var marker = Prim.Flatten(c.Render()).OfType<LinePrim>().Single(p => p.Tag == "ref.1");
            Assert.Equal(c.F.Panels[0].Area.Y, Math.Min(marker.A.Y, marker.B.Y), 6);
            Assert.Equal(c.F.Panels[2].Area.Bottom, Math.Max(marker.A.Y, marker.B.Y), 6);
            // The time arrow t_krit lies in the gap between panel 1 and 2 and ends at the marker.
            var arrow = Prim.Flatten(c.Render()).OfType<LinePrim>().Single(p => p.Tag == "note.0");
            Assert.InRange(arrow.A.Y, c.F.Panels[0].Area.Bottom, c.F.Panels[1].Area.Y);
            Assert.Equal(marker.A.X, arrow.B.X, 6);
            // A marker in one panel stays in that panel.
            var spec = FigureSpec.Parse(FigurePresets.Timing, Lang.En);
            spec.Refs[1].Panel = 1;
            var one = Fx.Type(r, o).Compose(Figure(spec, o.Size), Fx.Ctx());
            var line = Prim.Flatten(one.Render()).OfType<LinePrim>().Single(p => p.Tag == "ref.1");
            Assert.Equal(one.F.Panels[1].Area.Y, Math.Min(line.A.Y, line.B.Y), 6);
            Assert.Equal(one.F.Panels[1].Area.Bottom, Math.Max(line.A.Y, line.B.Y), 6);
            // Numeral 64 at the y axis of the first panel.
            Assert.Contains(Prim.Flatten(c.Render()).OfType<TextPrim>(), t => t.Text == "64" && t.Position.X < c.F.Panels[0].Area.X);
        }

        private static int SubPaths(PathPrim p) => p.Segments.Count(s => s.Op == SegOp.M);

        [Fact]
        public void RegionOnlyWhereAboveKeepsTheCrossingParts()
        {
            var r = Fx.Reg();
            var spec = new FigureSpec
            {
                X = new FigAxis { Min = 0, Max = 4 },
                Panels = { new FigPanel { Min = 0, Max = 1 } },
                Series = { new FigSeries { X = { 0, 1, 2, 3, 4 }, Y = { 0, 1, 0, 1, 0 } }, new FigSeries { X = { 0, 4 }, Y = { 0.5, 0.5 } } },
                Regions = { new FigRegion { A = 0, B = 1, Above = true } },
            };
            var o = Figure(spec);
            var c = Compose(r, o);
            var region = Prim.Flatten(c.Render()).OfType<PathPrim>().Single(p => p.Tag == "region.0");
            Assert.Equal(2, SubPaths(region));
            // The parts run from crossing to crossing (x = 0.5 … 1.5 and 2.5 … 3.5) and lie above the straight line.
            var panel = c.F.Panels[0];
            var xs = region.Segments.Where(s => s.Op != SegOp.Z).Select(s => s.P.X).ToList();
            Assert.Equal(c.F.P(panel, 0.5, 0).X, xs.Min(), 6);
            Assert.Equal(c.F.P(panel, 3.5, 0).X, xs.Max(), 6);
            var lineY = c.F.P(panel, 0, 0.5).Y;
            Assert.All(region.Segments.Where(s => s.Op != SegOp.Z), s => Assert.True(s.P.Y <= lineY + 1e-6));
            // Without "above": every part between the curves, split at the four crossings (five).
            spec.Regions[0].Above = false;
            Assert.Equal(5,SubPaths(Flat(r, Figure(spec)).OfType<PathPrim>().Single(p => p.Tag == "region.0")));
            // Gaps split a region; from/to limit it; below a curve and spans work too.
            spec.Series[0] = new FigSeries { X = { 0, 1, double.NaN, 3, 4 }, Y = { 1, 1, double.NaN, 1, 1 } };
            Assert.Equal(2, SubPaths(Flat(r, Figure(spec)).OfType<PathPrim>().Single(p => p.Tag == "region.0")));
            spec.Regions[0].From = 3.2;
            Assert.Equal(1, SubPaths(Flat(r, Figure(spec)).OfType<PathPrim>().Single(p => p.Tag == "region.0")));
            spec.Regions.Add(new FigRegion { Kind = "below", A = 1, Fill = "solid" });
            spec.Regions.Add(new FigRegion { Kind = "span", From = 1, To = 2, Fill = "dots" });
            var prims = Flat(r, Figure(spec));
            Assert.Equal(FillKind.Solid, prims.OfType<PathPrim>().Single(p => p.Tag == "region.1").Style.EffectiveFill.Kind);
            Assert.Equal(FillKind.Dots, prims.OfType<PathPrim>().Single(p => p.Tag == "region.2").Style.EffectiveFill.Kind);
        }

        [Fact]
        public void TemperatureFigureHasGapsMarkersAndDataAxes()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "chart.figure.temperature");
            var prims = Flat(r, o);
            var fast = prims.OfType<PathPrim>().Single(p => p.Tag == "series.0");
            Assert.Equal(2, SubPaths(fast));                                       // the curve stops and continues later
            Assert.Contains(prims.OfType<TextPrim>(), t => t.Tag == "label.y" && t.Text == "200");
            Assert.Contains(prims.OfType<TextPrim>(), t => t.Tag == "label.x" && t.Text == "150");
            var grid = prims.OfType<PathPrim>().Single(p => p.Tag == ChartTags.Grid);
            for (var i = 1; i < grid.Segments.Count; i++)
                if (grid.Segments[i].Op == SegOp.L) Assert.Equal(grid.Segments[i - 1].P.Y, grid.Segments[i].P.Y, 9); // horizontal only
            Assert.Equal(DashStyle.Dot, grid.Style.Dash);
            Assert.Equal(4, prims.OfType<PathPrim>().Count(p => p.Tag is "ref.0" or "ref.1" or "ref.2" or "ref.3"));
            var marker = prims.OfType<PathPrim>().Single(p => p.Tag == "ref.4");
            Assert.Equal(Palette.White, marker.Style.EffectiveFill.Color);        // open circle
            Assert.Contains(prims.OfType<TextPrim>(), t => t.Tag == "ref.4" && t.Text == "31 s");
            Assert.Contains(prims.OfType<TextPrim>(), t => t.Tag == "ref.5" && t.Text == "77 s");
            Assert.Contains(prims.OfType<TextPrim>(), t => t.Tag == ChartTags.Caption);
            Assert.Contains(prims.OfType<TextPrim>(), t => t.Tag == "curvelabel.0" && t.Text == "fast");
            var dim = prims.OfType<LinePrim>().Single(p => p.Tag == "note.0");
            var c = Compose(r, o);
            Assert.Equal(c.F.P(c.F.Panels[0], 110, 60).Y, dim.A.Y, 6);
            Assert.Equal(c.F.P(c.F.Panels[0], 110, 95).Y, dim.B.Y, 6);
            // The point markers sit on the curve where it reaches 95 °C.
            var ports = Fx.Type(r, o).Anchors(o, Fx.Ctx()).ToDictionary(a => a.Id!);
            Assert.Equal(c.F.P(c.F.Panels[0], 31, 95).Y, ports["ref5"].P.Y, 1);
            Assert.Equal(c.F.P(c.F.Panels[0], 77, 95).Y, ports["ref6"].P.Y, 1);
        }

        [Fact]
        public void PartsCoverEveryElementWithNames()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "chart.figure.phases");
            var parts = r.Parts(o, Fx.Ctx());
            var keys = parts.Select(p => p.Key).ToList();
            foreach (var k in new[] { "series.0", "series.2", "ref.0", "ref.2", "region.0", "note.0", "note.2", "numeral.0", "numeral.6", "axis.x.1", "axis.y.2", "label.x", "axistitle.y.2" })
                Assert.Contains(k, keys);
            Assert.Equal("Reference numeral 40", parts.Single(p => p.Key == "numeral.0").Label.En);
            Assert.Equal("Bezugslinie 3 – Grenztemperatur", parts.Single(p => p.Key == "ref.2").Label.De);
            Assert.Equal("Region 1 – 45", parts.Single(p => p.Key == "region.0").Label.En);
            Assert.StartsWith("Anmerkung 3", parts.Single(p => p.Key == "note.2").Label.De);
            var timing = r.Parts(Fx.Item(r, "chart.figure.timing"), Fx.Ctx());
            Assert.Contains(timing, p => p.Key == ChartTags.Title);
            Assert.Equal("Label – heat demand", timing.Single(p => p.Key == "curvelabel.1").Label.En);
            Assert.Contains(r.Parts(Fx.Item(r, "chart.figure.temperature"), Fx.Ctx()), p => p.Key == ChartTags.Caption && p.Label.De == "Bildunterschrift");

            // Hide the hatched region and restyle numeral 45; everything else stays.
            var hidden = PartLooks.WithLook(o, "region.0", p => p with { Hidden = true });
            Assert.DoesNotContain(Prim.Flatten(r.RenderLocal(hidden, Fx.Ctx())), p => p.Tag == "region.0");
            var red = PartLooks.WithLook(o, "numeral", p => p with { Style = new Style { Stroke = "#ff0000" } });
            Assert.All(Prim.Flatten(r.RenderLocal(red, Fx.Ctx())).OfType<LinePrim>().Where(p => ChartTags.Is(p.Tag, ChartTags.Numeral)), l => Assert.Equal("#ff0000", l.Style.Stroke));
        }

        [Fact]
        public void AnchorsArePlotCornersAxisRailsPerPanelAndSeriesPorts()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "chart.figure.timing");
            var ids = Fx.Type(r, o).Anchors(o, Fx.Ctx()).Select(a => a.Id).ToList();
            foreach (var id in new[] { "plot.tl", "plot.br", "axis.x", "axis.y", "axis.x2", "axis.y2", "axis.x3", "axis.y3", "origin3", "s1.1", "s2.1", "s4.3" })
                Assert.Contains(id, ids);
        }

        [Fact]
        public void UnreadableSpecificationRendersAnEmptyFrame()
        {
            var r = Fx.Reg();
            foreach (var text in new[] { "Test 12", "{\"series\": [", "[]", "{\"series\": 5, \"panels\": \"x\", \"x\": [1]}" })
            {
                var o = new SketchObject { Id = "f", Type = FigureChart.TypeId, Size = new Size2(100, 80), Params = new Dictionary<string, ParamValue> { ["spec"] = text } };
                foreach (var lang in new[] { Lang.En, Lang.De })
                {
                    var prims = Fx.Render(r, o, Fx.Ctx(lang));
                    Fx.AssertInsideBox(prims, o.Size, text);
                    Assert.Contains(Prim.Flatten(prims), p => ChartTags.Is(p.Tag, ChartTags.Axis));
                }
                Assert.NotEmpty(Fx.Type(r, o).Anchors(o, Fx.Ctx()));
                Assert.NotEmpty(r.Parts(o, Fx.Ctx()));
            }
            var bad = new SketchObject { Id = "f", Type = FigureChart.TypeId, Size = new Size2(100, 80), Params = new Dictionary<string, ParamValue> { ["spec"] = "Test 12" } };
            Assert.Contains(Prim.Flatten(Fx.Render(r, bad, Fx.Ctx(Lang.De))).OfType<TextPrim>(), t => t.Tag == ChartTags.Error && t.Text.StartsWith("Bildspezifikation", StringComparison.Ordinal));
        }

        [Fact]
        public void ExtremeValuesStayInsideTheBox()
        {
            var r = Fx.Reg();
            var spec = new FigureSpec
            {
                X = new FigAxis { Scale = "log" },
                Panels = { new FigPanel { Mode = "principle" }, new FigPanel { Scale = "log", Min = 1, Max = 1000 } },
                Series =
                {
                    new FigSeries { X = { -5, 1, 10, 1e6 }, Y = { 1e300, -1e300, 0, 5 }, Smooth = true, Label = new I18nText(new string('x', 120)) },
                    new FigSeries { X = { 1, 10, 100 }, Y = { -1, 0, 2000 }, Panel = 9, Marker = "plus", LabelAt = 50, Label = new I18nText("label") },
                },
                Refs = { new FigRef { Kind = "h", Y = 1e9, Label = new I18nText("far") }, new FigRef { Kind = "point", Series = 5, X = 2 }, new FigRef { Kind = "v", X = -3 } },
                Regions = { new FigRegion { A = 0, B = 1 }, new FigRegion { Kind = "below", A = 1, Base = -4 }, new FigRegion { Kind = "span", From = 1e9, To = -1e9, Panel = 7 } },
                Notes =
                {
                    new FigNote { Kind = "dim", P1 = new FigPoint { X = 1e12, Y = 1e12, Panel = 4 }, P2 = new FigPoint { X = -1e12, Gap = 9 }, Text = new I18nText("dim") },
                    new FigNote { Kind = "numeral", P1 = new FigPoint { X = 10 }, Text = new I18nText("99"), Dx = 500, Dy = -500 },
                    new FigNote { Kind = "text", P1 = new FigPoint(), Text = new I18nText(new string('y', 300)) },
                },
            };
            foreach (var size in new[] { new Size2(100, 80), new Size2(20, 15), new Size2(500, 40) })
                Fx.AssertInsideBox(Fx.Render(r, Figure(spec, size)), size, "extreme " + size.W);
        }

        // ------------------------------------------------------------------ actions

        private static ContextAction Act(Registry r, SketchObject o, string id, Lang lang = Lang.En)
            => r.TypeOf(o).Actions(o, new RenderContext(SketchDocument.Create(), lang)).Single(a => a.Id == id);

        private static FigureSpec SpecOf(SketchObject o) => FigureSpec.Parse(FigureChart.SpecText(o), Lang.En);

        [Fact]
        public void ElementActionsChangeTheSpecification()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "chart.figure.temperature");
            var before = SpecOf(o);
            var h = SpecOf(Act(r, o, "chart.figure.hline").Apply(o));
            Assert.Equal(before.Refs.Count + 1, h.Refs.Count);
            Assert.Equal("h", h.Refs.Last().Kind);
            Assert.InRange(h.Refs.Last().Y, 100, 220);
            var v = SpecOf(Act(r, o, "chart.figure.vline").Apply(o));
            Assert.Equal("v", v.Refs.Last().Kind);
            Assert.Equal(100, v.Refs.Last().X);
            var reg = Act(r, o, "chart.figure.region");
            Assert.True(reg.IsEnabled);
            Assert.Single(SpecOf(reg.Apply(o)).Regions);
            // Principle and data axes.
            var principle = Act(r, o, "chart.figure.principle");
            Assert.False(principle.IsActive);
            var p = principle.Apply(o);
            Assert.True(SpecOf(p).AllPrinciple);
            Assert.True(Act(r, p, "chart.figure.principle").IsActive);
            Assert.DoesNotContain(Flat(r, p), x => x.Tag == "label.y");
            Assert.False(SpecOf(Act(r, p, "chart.figure.principle").Apply(p)).AllPrinciple);
            // Monochrome scheme.
            var mono = Act(r, o, "chart.figure.mono").Apply(o);
            Assert.Equal(Palette.Black, Flat(r, mono).OfType<PathPrim>().Single(x => x.Tag == "series.0").Style.Stroke);
            Assert.True(Act(r, mono, "chart.figure.mono").IsActive);
            // Editing the specification: valid JSON is taken over, invalid JSON is refused with a bilingual message.
            var edit = Act(r, o, "chart.figure.spec", Lang.De);
            Assert.Equal(FigurePresets.Temperature, edit.Input!.Value);
            Assert.Equal("{\"version\":1}", edit.Input.Apply(o, " {\"version\":1} ").Str("spec", ""));
            Assert.StartsWith("Ungültige Bildspezifikation", Assert.Throws<FormatException>(() => edit.Input.Apply(o, "{oops")).Message);
            // Actions on an unreadable specification throw a FormatException instead of losing the text.
            var bad = o.WithParam("spec", "{oops");
            Assert.Throws<FormatException>(() => Act(r, bad, "chart.figure.hline").Apply(bad));
        }

        [Fact]
        public void RegionActionNeedsTwoCurvesInOnePanel()
        {
            var r = Fx.Reg();
            var spec = new FigureSpec { Series = { new FigSeries { X = { 0, 1 }, Y = { 0, 1 } } } };
            var o = Figure(spec);
            var a = Act(r, o, "chart.figure.region");
            Assert.False(a.IsEnabled);
            Assert.Empty(SpecOf(a.Apply(o)).Regions);
        }

        [Fact]
        public void PartActionsWorkOnTheirElement()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "chart.figure.phases");
            var ctx = Fx.Ctx();
            var series = r.PartActions(o, "series.1", ctx);
            Assert.Equal("Line: dotted", series.Single(a => a.Id == "chart.figure.line").Label.En);   // dash → dot
            Assert.Equal("dot", SpecOf(series.Single(a => a.Id == "chart.figure.line").Apply(o)).Series[1].Line);
            Assert.Equal("circle", SpecOf(series.Single(a => a.Id == "chart.figure.markers").Apply(o)).Series[1].Marker);
            Assert.True(SpecOf(series.Single(a => a.Id == "chart.figure.smooth").Apply(o)).Series[1].Smooth);
            var below = SpecOf(series.Single(a => a.Id == "chart.figure.below").Apply(o)).Regions.Last();
            Assert.Equal(("below", 1), (below.Kind, below.A));
            Assert.Contains(series, a => a.Id == "part.hide");

            var refs = r.PartActions(o, "ref.2", ctx);
            Assert.Equal(2, SpecOf(refs.Single(a => a.Id == "chart.figure.remove").Apply(o)).Refs.Count);
            Assert.Equal("Limit", SpecOf(refs.Single(a => a.Id == "chart.figure.label").Input!.Apply(o, " Limit ")).Refs[2].Label!.En);
            Assert.Equal("solid", SpecOf(refs.Single(a => a.Id == "chart.figure.line").Apply(o)).Refs[2].Line); // dashdot → solid

            var region = r.PartActions(o, "region.0", ctx);
            Assert.Equal("cross", SpecOf(region.Single(a => a.Id == "chart.figure.fill").Apply(o)).Regions[0].Fill);
            Assert.Empty(SpecOf(region.Single(a => a.Id == "chart.figure.remove").Apply(o)).Regions);

            var note = r.PartActions(o, "note.0", ctx);
            Assert.Equal("Phase 1", SpecOf(note.Single(a => a.Id == "chart.figure.label").Input!.Apply(o, "Phase 1")).Notes[0].Text!.En);

            // The y axis of the second panel adds a value line in that panel.
            var axis = r.PartActions(o, "axis.y.2", ctx);
            Assert.Equal(1, SpecOf(axis.Single(a => a.Id == "chart.figure.hline").Apply(o)).Refs.Last().Panel);
            Assert.Contains(r.PartActions(o, "axis.x.1", ctx), a => a.Id == "chart.figure.vline");
            Assert.Contains(axis, a => a.Id == "chart.figure.principle");

            // Every part action applies and the result renders inside the box.
            foreach (var part in r.Parts(o, ctx))
                foreach (var a in r.PartActions(o, part.Key, ctx))
                {
                    var next = a.Input is not null ? a.Input.Apply(o, a.Input.Value) : a.Apply(o);
                    Fx.AssertInsideBox(Fx.Render(r, next), next.Size, part.Key + " " + a.Id);
                }
        }
    }
}
