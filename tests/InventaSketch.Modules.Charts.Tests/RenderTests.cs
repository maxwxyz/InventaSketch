using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Charts.Tests
{
    public class RenderTests
    {
        public static IEnumerable<object[]> ItemIds() => Fx.Reg().Library.Select(i => new object[] { i.Id });

        [Theory]
        [MemberData(nameof(ItemIds))]
        public void EveryItemRendersInsideItsBox(string id)
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, id);
            foreach (var lang in new[] { Lang.En, Lang.De })
                foreach (var mono in new[] { false, true })
                {
                    var prims = Fx.Render(r, o, Fx.Ctx(lang, mono));
                    Fx.AssertInsideBox(prims, o.Size, $"{id} {lang} mono={mono}");
                    if (!Fx.NoAxes.Contains(o.Type)) Assert.Contains(Prim.Flatten(prims), p => ChartTags.Is(p.Tag, ChartTags.TickLabel));
                }
            foreach (var size in new[] { new Size2(o.Size.W * 2, o.Size.H), new Size2(o.Size.W * 0.6, o.Size.H * 1.4), new Size2(o.Size.W * 0.35, o.Size.H * 0.35), new Size2(400, 260) })
                Fx.AssertInsideBox(Fx.Render(r, o with { Size = size }), size, $"{id} {size.W}x{size.H}");
        }

        [Theory]
        [MemberData(nameof(ItemIds))]
        public void EveryItemDrawsItsData(string id)
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, id);
            var tags = Prim.Flatten(Fx.Render(r, o)).Select(p => p.Tag).ToList();
            if (o.Type is "chart.axes" or "chart.math.axes") { Assert.Contains(tags, t => ChartTags.Is(t, ChartTags.Axis)); return; }
            var data = new[] { ChartTags.Series, ChartTags.Marker, ChartTags.Bar, ChartTags.Contour, ChartTags.Band, TimingTags.Signal, ChartTags.Slice, ChartTags.Flow, ChartTags.Box };
            Assert.True(tags.Any(t => data.Any(d => ChartTags.Is(t, d))), id + " draws no data");
        }

        [Fact]
        public void ScalingChangesSizeNotData()
        {
            var r = Fx.Reg();
            foreach (var item in r.Library)
            {
                var o = Fx.Item(r, item.Id);
                var big = o with { Size = new Size2(o.Size.W * 2, o.Size.H * 2) };
                Assert.Equal(o.Params.Count, big.Params.Count);
                foreach (var kv in o.Params) Assert.Equal(kv.Value, big.Params[kv.Key]);
                var a = Prim.Bounds(Fx.Render(r, o))!.Value;
                var b = Prim.Bounds(Fx.Render(r, big))!.Value;
                Assert.InRange(b.W / a.W, 1.8, 2.2);
                Assert.InRange(b.H / a.H, 1.8, 2.2);
                // Data ports keep their ids and scale with the box.
                var pa = Fx.Type(r, o).Anchors(o, Fx.Ctx()).Where(x => x.Id is not null).ToDictionary(x => x.Id!);
                var pb = Fx.Type(r, big).Anchors(big, Fx.Ctx()).Where(x => x.Id is not null).ToDictionary(x => x.Id!);
                Assert.Equal(pa.Keys.OrderBy(x => x), pb.Keys.OrderBy(x => x));
                Assert.InRange(pb["plot.tr"].P.X / pa["plot.tr"].P.X, 1.8, 2.2);
            }
        }

        [Fact]
        public void NonUniformStretchKeepsTextUpright()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "chart.line");
            var wide = o with { Size = new Size2(o.Size.W * 3, o.Size.H) };
            var a = Prim.Flatten(Fx.Render(r, o)).OfType<TextPrim>().First(t => ChartTags.Is(t.Tag, ChartTags.TickLabel));
            var b = Prim.Flatten(Fx.Render(r, wide)).OfType<TextPrim>().First(t => ChartTags.Is(t.Tag, ChartTags.TickLabel));
            Assert.Equal(a.TextStyle.Size, b.TextStyle.Size, 6);
            Assert.Equal(0, b.Rotation);
        }

        [Theory]
        [MemberData(nameof(ItemIds))]
        public void AnchorsArePresent(string id)
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, id);
            var anchors = Fx.Type(r, o).Anchors(o, Fx.Ctx()).ToList();
            var ids = anchors.Select(a => a.Id).ToList();
            foreach (var x in new[] { "plot.tl", "plot.tr", "plot.br", "plot.bl", "plot.c" }) Assert.Contains(x, ids);
            if (!Fx.NotCartesian.Contains(o.Type))
            {
                Assert.Contains("axis.x", ids);
                Assert.Contains(ids, x => x is not null && x.StartsWith("origin", StringComparison.Ordinal));
            }
            Assert.Equal(ids.Count, ids.Distinct().Count());
            foreach (var a in anchors) Assert.True(Math.Abs(a.P.X) <= o.Size.W / 2 + 1e-6 && Math.Abs(a.P.Y) <= o.Size.H / 2 + 1e-6, id + "/" + a.Id);
            if (o.Type is not ("chart.axes" or "chart.math.axes")) Assert.Contains(anchors, a => a.Role == AnchorRole.Port && a.Id is not null && !a.Id.StartsWith("origin", StringComparison.Ordinal));
            // The page anchors include the generic box anchors (centre "c") and transform with the object.
            var placed = o with { Transform = new Transform(100, 50) };
            var page = r.PageAnchors(placed, Fx.Ctx()).ToList();
            Assert.Contains(page, a => a.Id == "c" && a.P == new Vec2(100, 50));
            Assert.Contains(page, a => a.Id == "plot.c");
        }

        [Fact]
        public void DataPointsAreDockingPorts()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "chart.line");
            var anchors = Fx.Type(r, o).Anchors(o, Fx.Ctx()).ToList();
            var ports = anchors.Where(a => a.Id is not null && a.Id.StartsWith("s1.", StringComparison.Ordinal)).ToList();
            Assert.Equal(7, ports.Count);
            Assert.All(ports, p => Assert.Equal(AnchorRole.Port, p.Role));
            // Ports lie on the drawn series line.
            var line = (PathPrim)Prim.Flatten(Fx.Render(r, o)).First(p => ChartTags.Is(p.Tag, ChartTags.Series));
            var vertices = line.Segments.Where(s => s.Op != SegOp.Z).Select(s => s.P).ToList();
            foreach (var p in ports) Assert.Contains(vertices, v => v.DistanceTo(p.P) < 1e-6);
            // Increasing x runs to the right, increasing y runs up.
            Assert.True(ports[6].P.X > ports[0].P.X && ports[6].P.Y < ports[0].P.Y);
        }

        [Fact]
        public void SeriesAreDistinguishableInMonochrome()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "chart.line.multi");
            var prims = Prim.Flatten(Fx.Render(r, o, Fx.Ctx(mono: true))).ToList();
            var lines = prims.OfType<PathPrim>().Where(p => ChartTags.Is(p.Tag, ChartTags.Series)).ToList();
            Assert.Equal(3, lines.Count);
            Assert.Equal(3, lines.Select(l => l.Style.EffectiveDash).Distinct().Count());
            var markers = prims.OfType<PathPrim>().Where(p => ChartTags.Is(p.Tag, ChartTags.Marker)).ToList();
            Assert.Equal(3, markers.Count);
            Assert.All(SeriesLook.Colors, c => Assert.True(SvgRenderer.Luminance(c) < 0.35, c + " is too light for monochrome output"));
            // Legend with three entries.
            Assert.Equal(3, prims.Count(p => ChartTags.Is(p.Tag, ChartTags.LegendText)));
            Assert.Equal(new[] { "A", "B", "C" }, prims.OfType<TextPrim>().Where(p => ChartTags.Is(p.Tag, ChartTags.LegendText)).Select(t => t.Text));
        }

        [Fact]
        public void BarsUsePatternsInMonochrome()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "chart.column.grouped");
            var colour = Prim.Flatten(Fx.Render(r, o)).OfType<RectPrim>().Where(p => ChartTags.Is(p.Tag, ChartTags.Bar)).ToList();
            Assert.Equal(8, colour.Count);
            Assert.All(colour, b => Assert.Equal(FillKind.Solid, b.Style.EffectiveFill.Kind));
            var mono = Prim.Flatten(Fx.Render(r, o, Fx.Ctx(mono: true))).OfType<RectPrim>().Where(p => ChartTags.Is(p.Tag, ChartTags.Bar)).ToList();
            Assert.Equal(2, mono.Select(b => (b.Style.EffectiveFill.Kind, b.Style.EffectiveFill.Color)).Distinct().Count());
            var doc = SketchDocument.Create() with { Settings = new DocumentSettings { Monochrome = true } };
            var viaDoc = Prim.Flatten(Fx.Render(r, o.WithParam("data", "A;1;2;3\nB;2;3;4"), new RenderContext(doc))).OfType<RectPrim>().Where(p => ChartTags.Is(p.Tag, ChartTags.Bar)).ToList();
            Assert.Contains(viaDoc, b => b.Style.EffectiveFill.Kind == FillKind.Hatch);
        }

        [Fact]
        public void StackedBarsStackAndHorizontalBarsRunRight()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "chart.column.stacked").WithParam("data", "A;1;2\nB;2;2");
            var bars = Prim.Flatten(Fx.Render(r, o)).OfType<RectPrim>().Where(p => ChartTags.Is(p.Tag, ChartTags.Bar)).ToList();
            Assert.Equal(4, bars.Count);
            // Second segment of category A sits on top of the first.
            Assert.Equal(bars[0].Y, bars[1].Y + bars[1].H, 6);
            Assert.Equal(bars[0].X, bars[1].X, 6);
            var h = Fx.Item(r, "chart.bar.horizontal");
            var hb = Prim.Flatten(Fx.Render(r, h)).OfType<RectPrim>().Where(p => ChartTags.Is(p.Tag, ChartTags.Bar)).ToList();
            Assert.Equal(4, hb.Count);
            Assert.True(hb.All(b => b.W > b.H * 0.5));
            Assert.True(hb[0].Y < hb[1].Y, "first category on top");
            Assert.Equal(4, Prim.Flatten(Fx.Render(r, h)).Count(p => ChartTags.Is(p.Tag, ChartTags.ValueLabel) && p is TextPrim));
        }

        [Fact]
        public void LogAxesRenderAndSkipNonPositiveValues()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "chart.line").WithParam("yscale", "log").WithParam("data", "0;0\n1;10\n2;100\n3;-5\n4;1000");
            var prims = Fx.Render(r, o);
            Fx.AssertInsideBox(prims, o.Size, "log y");
            Assert.Contains(Prim.Flatten(prims).OfType<TextPrim>(), t => t.Text == "1000");
            Assert.Contains(Prim.Flatten(prims), p => ChartTags.Is(p.Tag, ChartTags.GridMinor));
        }

        [Fact]
        public void UserRangesClipTheData()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "chart.line").WithParam("xrange", "1;4").WithParam("yrange", "2;4");
            var prims = Fx.Render(r, o);
            var plot = Fx.Type(r, o).Anchors(o, Fx.Ctx()).ToDictionary(a => a.Id ?? Guid.NewGuid().ToString());
            var tl = plot["plot.tl"].P; var br = plot["plot.br"].P;
            foreach (var p in Prim.Flatten(prims).Where(p => ChartTags.Is(p.Tag, ChartTags.Series)).SelectMany(Prim.Points))
                Assert.True(p.X >= tl.X - 1e-6 && p.X <= br.X + 1e-6 && p.Y >= tl.Y - 1e-6 && p.Y <= br.Y + 1e-6, "series point outside the plot " + p);
        }

        [Fact]
        public void StepAndSmoothStyles()
        {
            var r = Fx.Reg();
            var step = Fx.Item(r, "chart.step");
            var line = (PathPrim)Prim.Flatten(Fx.Render(r, step)).First(p => ChartTags.Is(p.Tag, ChartTags.Series));
            Assert.Equal(13, line.Segments.Count); // 7 points, 6 risers
            Assert.All(line.Segments.Zip(line.Segments.Skip(1), (a, b) => (a, b)), s => Assert.True(Math.Abs(s.a.P.X - s.b.P.X) < 1e-9 || Math.Abs(s.a.P.Y - s.b.P.Y) < 1e-9));
            var smooth = (PathPrim)Prim.Flatten(Fx.Render(r, Fx.Item(r, "chart.line.smooth"))).First(p => ChartTags.Is(p.Tag, ChartTags.Series));
            Assert.Contains(smooth.Segments, s => s.Op == SegOp.C);
        }

        [Fact]
        public void EmptyDataStillShowsAxes()
        {
            var r = Fx.Reg();
            foreach (var id in new[] { "chart.line", "chart.column", "chart.band" })
            {
                var o = Fx.Item(r, id).WithParam("data", "");
                var prims = Fx.Render(r, o);
                Fx.AssertInsideBox(prims, o.Size, id + " empty");
                Assert.Contains(Prim.Flatten(prims), p => ChartTags.Is(p.Tag, ChartTags.Axis));
            }
        }

        [Fact]
        public void GermanLabelsUseDecimalCommaAndGermanTitles()
        {
            var r = Fx.Reg();
            var torque = Fx.Item(r, "chart.torque");
            var de = Prim.Flatten(Fx.Render(r, torque, Fx.Ctx(Lang.De))).OfType<TextPrim>().Select(t => t.Text).ToList();
            Assert.Contains("Drehzahl n in 1/min", de);
            Assert.Contains("Drehmoment M in Nm", de);
            Assert.Contains("Leistung P in kW", de);
            var en = Prim.Flatten(Fx.Render(r, torque)).OfType<TextPrim>().Select(t => t.Text).ToList();
            Assert.Contains("Speed n (rpm)", en);
            var line = Fx.Item(r, "chart.line").WithParam("data", "0;0\n1;0.5");
            Assert.Contains(Prim.Flatten(Fx.Render(r, line, Fx.Ctx(Lang.De))).OfType<TextPrim>(), t => t.Text.Contains(","));
            // Explicit labels win, empty labels hide the title.
            var named = line.WithParam("xlabel", "Time t").WithParam("xunit", "s").WithParam("ylabel", "");
            var texts = Prim.Flatten(Fx.Render(r, named)).OfType<TextPrim>().ToList();
            Assert.Contains(texts, t => t.Text == "Time t (s)");
            Assert.DoesNotContain(texts, t => ChartTags.Is(t.Tag, ChartTags.AxisTitle) && t.Text == "y");
        }

        [Fact]
        public void TitleAndLimitLines()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "chart.limits").WithParam("title", "Response");
            var prims = Prim.Flatten(Fx.Render(r, o)).ToList();
            Assert.Contains(prims, p => p is TextPrim t && ChartTags.Is(t.Tag, ChartTags.Title) && t.Text == "Response");
            var limit = (PathPrim)prims.First(p => ChartTags.Is(p.Tag, ChartTags.Limit));
            Assert.Equal(2, limit.Segments.Count(s => s.Op == SegOp.M));
            Assert.Equal(LineRole.Center, limit.Style.Role);
        }
    }
}
