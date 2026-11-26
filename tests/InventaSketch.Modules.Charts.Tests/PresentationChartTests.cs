using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Charts.Tests
{
    /// <summary>Pie and donut, polar, Sankey, Gantt, box plot and histogram: data → geometry, rendering, actions, persistence, import.</summary>
    public class PresentationChartTests
    {
        private static IReadOnlyList<Primitive> Drawn(Registry r, SketchObject o, RenderContext? ctx = null) => Prim.Flatten(r.RenderLocal(o, ctx ?? Fx.Ctx())).ToList();

        private static ContextAction Act(Registry r, SketchObject o, string id, Lang lang = Lang.En)
            => r.TypeOf(o).Actions(o, new RenderContext(SketchDocument.Create(), lang)).Single(a => a.Id == id);

        private static Canvas Canvas(Registry r, SketchObject o, RenderContext? ctx = null) => ((CanvasChart)r.TypeOf(o)).DrawCanvas(o, ctx ?? Fx.Ctx());

        public static IEnumerable<object[]> NewItems() => new[]
        {
            "chart.pie", "chart.pie.names", "chart.donut", "chart.polar", "chart.polar.radar", "chart.sankey", "chart.gantt", "chart.box", "chart.histogram",
        }.Select(id => new object[] { id });

        // ------------------------------------------------------------------ pie and donut

        [Theory]
        [InlineData(0, true)]
        [InlineData(90, false)]
        [InlineData(-45, true)]
        public void SliceAnglesSumTo360AndFollowTheData(double start, bool clockwise)
        {
            var slices = PieChart.Slices("A;1\nB;2\nC;3\nD;-4\nE;0\nF;2", start, clockwise);
            Assert.Equal(new[] { "A", "B", "C", "F" }, slices.Select(s => s.Name));
            Assert.Equal(360, slices.Sum(s => s.Sweep), 9);
            Assert.Equal(start, slices[0].Start, 9);
            for (var i = 1; i < slices.Count; i++) Assert.Equal(slices[i - 1].End, slices[i].Start, 9);
            Assert.Equal(360 * 3 / 8.0, slices[2].Sweep, 9);
            Assert.True(clockwise ? slices[0].End > slices[0].Start : slices[0].End < slices[0].Start);
            Assert.Empty(PieChart.Slices("A;0\nB;-1", 0, true));
            Assert.Equal("25%", PieChart.Percent(0.25, Lang.En));
            Assert.Equal("7,5 %", PieChart.Percent(0.075, Lang.De));
        }

        [Fact]
        public void PieDrawsOneSlicePerPositiveValueWithItsShareOfTheCircle()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "chart.pie").WithParam("data", "A;1\nB;1\nC;2");
            var prims = Drawn(r, o);
            var slices = prims.OfType<PathPrim>().Where(p => ChartTags.Is(p.Tag, ChartTags.Slice)).ToList();
            Assert.Equal(new[] { "slice.0", "slice.1", "slice.2" }, slices.Select(s => s.Tag));
            var d = (PieChart.Drawing)Canvas(r, o).Model!;
            // Slice C covers half of the circle: starting at 12 o'clock clockwise, its first rim point is at 6 o'clock.
            var c = d.Centre;
            var rim = slices[2].Segments.First().P;
            Assert.Equal(c.X, rim.X, 6);
            Assert.Equal(c.Y + d.Radius, rim.Y, 6);
            // Percent labels inside the slices, white on the colour.
            var labels = prims.OfType<TextPrim>().Where(t => ChartTags.Is(t.Tag, ChartTags.SliceLabel)).ToList();
            Assert.Equal(new[] { "25%", "25%", "50%" }, labels.Select(t => t.Text));
            Assert.All(d.Labels.Values, l => Assert.True(l.Inside));
            Assert.All(labels, t => Assert.True(t.Position.DistanceTo(c) < d.Radius));
            // Names in the legend.
            Assert.Equal(new[] { "A", "B", "C" }, prims.OfType<TextPrim>().Where(t => ChartTags.Is(t.Tag, ChartTags.LegendText)).Select(t => t.Text));
            Assert.Contains(Fx.Type(r, o).Anchors(o, Fx.Ctx()), a => a.Id == "s1.3" && a.Role == AnchorRole.Port);
        }

        [Fact]
        public void DonutLabelsStayInTheRingAndTheCentreTextInTheHole()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "chart.donut").WithParam("center", "100");
            Assert.Equal(0.55, o.Num("hole", 0));
            var d = (PieChart.Drawing)Canvas(r, o).Model!;
            Assert.Equal(d.Radius * 0.55, d.Inner, 6);
            foreach (var (at, inside) in d.Labels.Values)
            {
                Assert.True(inside);
                var dist = at.DistanceTo(d.Centre);
                Assert.InRange(dist, d.Inner, d.Radius);
            }
            var slice = Drawn(r, o).OfType<PathPrim>().First(p => p.Tag == "slice.0");
            // The ring: no slice point lies in the hole.
            Assert.All(slice.Segments.Where(s => s.Op != SegOp.Z), s => Assert.True(s.P.DistanceTo(d.Centre) >= d.Inner - 1e-6));
            var centre = Drawn(r, o).OfType<TextPrim>().Single(t => t.Tag == ChartTags.CenterText);
            Assert.Equal("100", centre.Text);
            Assert.Contains(Fx.Type(r, o).Parts(o, Fx.Ctx()), p => p.Key == ChartTags.CenterText);
        }

        [Fact]
        public void LabelsThatDoNotFitMoveOutsideWithALeaderAndStayInTheBox()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "chart.pie.names").WithParam("data", "Very long first category;60\nSecond long category;30\nTiny;3\nSmall one;2\nAlmost nothing;1");
            foreach (var size in new[] { o.Size, new Size2(40, 30), new Size2(160, 60), new Size2(30, 80) })
            {
                var big = o with { Size = size };
                var prims = Drawn(r, big);
                Fx.AssertInsideBox(prims, size, "pie names " + size.W + "x" + size.H);
                var d = (PieChart.Drawing)Canvas(r, big).Model!;
                Assert.Contains(d.Labels.Values, l => !l.Inside);
                // An outside label has its leader line.
                var outside = d.Labels.First(kv => !kv.Value.Inside).Key;
                Assert.Contains(prims, p => p is PathPrim && p.Tag == "slicelabel." + outside);
            }
            // Names on the slices: no legend.
            Assert.DoesNotContain(Drawn(r, o), p => ChartTags.Is(p.Tag, ChartTags.Legend));
        }

        [Fact]
        public void PieActionsSwitchToADonutAndBack()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "chart.pie");
            var donut = Act(r, o, "chart.donut").Apply(o);
            Assert.Equal(0.5, donut.Num("hole", 0));
            Assert.True(Act(r, donut, "chart.donut").IsActive);
            Assert.Equal(0, Act(r, donut, "chart.donut").Apply(donut).Num("hole", 1));
            Assert.Equal(0.3, Act(r, o, "chart.hole").Input!.Apply(o, "30 %").Num("hole", 0), 6);
            Assert.Throws<FormatException>(() => Act(r, o, "chart.hole").Input!.Apply(o, "95"));
            Assert.Equal(90, Act(r, o, "chart.start").Input!.Apply(o, "90°").Num("start", 0));
            Assert.Equal("ccw", Act(r, o, "chart.direction=ccw").Apply(o).Str("direction", ""));
            Assert.Equal("value", Act(r, o, "chart.labels=value").Apply(o).Str("labels", ""));
            Assert.Equal("Sum", Act(r, o, "chart.center.text").Input!.Apply(o, " Sum ").Str("center", ""));
            // Part actions of a slice: data and the pie choices, then the generic part actions.
            var ids = r.PartActions(o, "slice.1", Fx.Ctx()).Select(a => a.Id).ToList();
            Assert.Contains("chart.data", ids);
            Assert.Contains("chart.donut", ids);
            Assert.Contains("part.hide", ids);
            Assert.Equal("B", r.Parts(o, Fx.Ctx()).Single(p => p.Key == "slice.1").Label.En);
            Assert.Equal("Beschriftung – B", r.Parts(o, Fx.Ctx()).Single(p => p.Key == "slicelabel.1").Label.De);
        }

        [Fact]
        public void PieInMonochromeUsesPatternsAndBackedLabels()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "chart.pie");
            var prims = Drawn(r, o, Fx.Ctx(mono: true));
            var fills = prims.OfType<PathPrim>().Where(p => ChartTags.Is(p.Tag, ChartTags.Slice)).Select(p => p.Style.EffectiveFill.Kind).ToList();
            Assert.Contains(FillKind.Hatch, fills);
            Assert.All(prims.OfType<TextPrim>().Where(t => ChartTags.Is(t.Tag, ChartTags.SliceLabel)), t => Assert.Equal(Palette.Ink, t.TextStyle.Color));
            Assert.Contains(prims, p => p is RectPrim && ChartTags.Is(p.Tag, ChartTags.SliceLabel));
        }

        // ------------------------------------------------------------------ polar

        [Fact]
        public void PolarPointsFollowTheAngleConventionAndTheRadialAxis()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "chart.polar").WithParam("data", "0;10\n90;10\n180;5\n270;10").WithParam("rrange", "0;10");
            var anchors = Fx.Type(r, o).Anchors(o, Fx.Ctx()).Where(a => a.Id is not null).ToDictionary(a => a.Id!);
            var c = anchors["origin"].P;
            var p0 = anchors["s1.1"].P; var p90 = anchors["s1.2"].P; var p180 = anchors["s1.3"].P;
            // 0° right, 90° up (counter-clockwise); r = 5 lies halfway to the rim.
            Assert.True(p0.X > c.X && Math.Abs(p0.Y - c.Y) < 1e-6);
            Assert.True(p90.Y < c.Y && Math.Abs(p90.X - c.X) < 1e-6);
            Assert.Equal(p0.DistanceTo(c) / 2, p180.DistanceTo(c), 6);
            var top = o.WithParam("zero", "top");
            var at = Fx.Type(r, top).Anchors(top, Fx.Ctx()).Where(a => a.Id is not null).ToDictionary(a => a.Id!);
            // Compass: 0° up, 90° right (clockwise).
            Assert.True(at["s1.1"].P.Y < at["origin"].P.Y);
            Assert.True(at["s1.2"].P.X > at["origin"].P.X);
            var prims = Drawn(r, o);
            var angles = prims.OfType<TextPrim>().Where(t => t.Tag == "label.a").Select(t => t.Text).ToList();
            Assert.Equal(12, angles.Count);
            Assert.Contains("90°", angles);
            Assert.Contains(prims, p => p.Tag == "axis.a");
            Assert.Contains(prims, p => p.Tag == ChartTags.Grid);
            // A closed curve returns to its first point.
            var line = prims.OfType<PathPrim>().Single(p => p.Tag == "series.0");
            Assert.Equal(line.Segments.First().P, line.Segments.Last().P);
            var open = Drawn(r, o.WithParam("closed", false)).OfType<PathPrim>().Single(p => p.Tag == "series.0");
            Assert.Equal(4, open.Segments.Count);
        }

        [Fact]
        public void RadarChartIsAFilledPolarChart()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "chart.polar.radar");
            var prims = Drawn(r, o);
            var fills = prims.OfType<PathPrim>().Where(p => p.Tag == "series.0" && p.IsClosed).ToList();
            Assert.Single(fills);
            Assert.Equal(FillKind.Solid, fills[0].Style.EffectiveFill.Kind);
            Assert.Equal(2, prims.Count(p => ChartTags.Is(p.Tag, ChartTags.LegendText)));
            Assert.Equal("Winkelachse", r.Parts(o, Fx.Ctx(Lang.De)).Single(p => p.Key == "axis.a").Label.De);
        }

        // ------------------------------------------------------------------ Sankey

        [Fact]
        public void SankeyNodeHeightsEqualTheirInAndOutSums()
        {
            var m = SankeyModel.Parse("From;To;Value\nA;M;30\nB;M;45\nC;M;25\nM;X;50\nM;Y;35\nM;Z;15\n# comment\nA;X;0");
            Assert.Equal(new[] { "A", "M", "B", "C", "X", "Y", "Z" }, m.Nodes);
            Assert.Equal(6, m.Flows.Count);
            var area = new Rect(-40, -25, 80, 50);
            var l = SankeyLayout.Compute(m, area, 2, 2);
            var mi = m.Nodes.IndexOf("M");
            Assert.Equal(100, l.In[mi], 9);
            Assert.Equal(100, l.Out[mi], 9);
            for (var i = 0; i < m.Nodes.Count; i++) Assert.Equal(Math.Max(l.In[i], l.Out[i]) * l.Scale, l.H[i], 9);
            // Columns: sources left, M in the middle, sinks right; the fullest column fills the height.
            Assert.Equal(new[] { 0, 1, 0, 0, 2, 2, 2 }, l.Layer);
            Assert.Equal(area.X, l.X[0], 9);
            Assert.Equal(area.Right - 2, l.X[m.Nodes.IndexOf("Z")], 9);
            Assert.Equal(area.H, l.H[0] + l.H[2] + l.H[3] + 2 * 2, 9);
            // The ribbons fill each node exactly: they stack from its top to its bottom on both sides.
            var outs = l.Ribbons.Where(x => x.Flow.Source == mi).OrderBy(x => x.Y0).ToList();
            var ins = l.Ribbons.Where(x => x.Flow.Target == mi).OrderBy(x => x.Y1).ToList();
            Assert.Equal(l.Y[mi], outs[0].Y0, 9);
            Assert.Equal(l.Y[mi] + l.H[mi], outs[outs.Count - 1].Y0 + outs[outs.Count - 1].T, 9);
            Assert.Equal(l.H[mi], ins.Sum(x => x.T), 9);
            Assert.All(l.Ribbons, x => Assert.Equal(x.Flow.Value * l.Scale, x.T, 9));
            Assert.Empty(l.Backward);
        }

        [Fact]
        public void SankeyCyclesAreReportedAndTheDiagramStaysInTheBox()
        {
            var m = SankeyModel.Parse("A;B;10\nB;C;10\nC;A;4");
            var l = SankeyLayout.Compute(m, new Rect(0, 0, 60, 40), 1.5, 2);
            Assert.Single(l.Backward);
            Assert.Equal(2, l.Ribbons.Count);
            var r = Fx.Reg();
            var o = Fx.Item(r, "chart.sankey").WithParam("data", "A;B;10\nB;C;10\nC;A;4");
            var prims = Drawn(r, o);
            Fx.AssertInsideBox(prims, o.Size, "sankey cycle");
            Assert.Contains(prims.OfType<TextPrim>(), t => t.Tag == ChartTags.Note && t.Text.Contains("cycle"));
        }

        [Fact]
        public void SankeyDrawsNodesFlowsAndLabelsAsParts()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "chart.sankey");
            var prims = Drawn(r, o);
            Assert.Equal(7, prims.Count(p => p is RectPrim && ChartTags.Is(p.Tag, ChartTags.Node)));
            Assert.Equal(6, prims.Count(p => p is PathPrim && ChartTags.Is(p.Tag, ChartTags.Flow)));
            Assert.Contains(prims.OfType<TextPrim>(), t => t.Tag == "nodelabel.1" && t.Text == "M 100");
            var parts = r.Parts(o, Fx.Ctx());
            Assert.Equal("Flow A → M", parts.Single(p => p.Key == "flow.0").Label.En);
            Assert.Equal("Knoten M", parts.Single(p => p.Key == "node.1").Label.De);
            var anchors = Fx.Type(r, o).Anchors(o, Fx.Ctx()).Select(a => a.Id).ToList();
            Assert.Contains("n2.in", anchors);
            Assert.Contains("n2.out", anchors);
            // Mono: black nodes, outlined white flows.
            var mono = Drawn(r, o, Fx.Ctx(mono: true));
            Assert.All(mono.OfType<PathPrim>().Where(p => ChartTags.Is(p.Tag, ChartTags.Flow)), p => Assert.Equal(Palette.Black, p.Style.Stroke));
            // Actions: add and remove a flow, values off.
            var more = Act(r, o, "chart.sankey.add").Apply(o);
            Assert.Equal(7, SankeyModel.Parse(more.Str("data", "")).Flows.Count);
            Assert.Equal(6, SankeyModel.Parse(Act(r, more, "chart.sankey.remove").Apply(more).Str("data", "")).Flows.Count);
            Assert.DoesNotContain(Drawn(r, Act(r, o, "chart.values").Apply(o)).OfType<TextPrim>(), t => t.Text == "M 100");
        }

        // ------------------------------------------------------------------ box plot and histogram

        [Fact]
        public void BoxStatisticsOfAKnownSample()
        {
            var b = Stats.Box(new double[] { 9, 2, 100, 4, 5, 6, 7, 8, 1, 3 })!;
            Assert.Equal(3.25, b.Q1, 9);
            Assert.Equal(5.5, b.Median, 9);
            Assert.Equal(7.75, b.Q3, 9);
            Assert.Equal(4.5, b.Iqr, 9);
            Assert.Equal(1, b.Low);
            Assert.Equal(9, b.High);
            Assert.Equal(new double[] { 100 }, b.Outliers);
            var all = Stats.Box(new double[] { 9, 2, 100, 4, 5, 6, 7, 8, 1, 3 }, outliers: false)!;
            Assert.Equal(100, all.High);
            Assert.Empty(all.Outliers);
            Assert.Equal(2, Stats.Quantile(new double[] { 1, 2, 3 }, 0.5));
            Assert.Null(Stats.Box(Array.Empty<double>()));
        }

        [Fact]
        public void BoxPlotDrawsBoxesAtTheQuartilesAndTheOutliers()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "chart.box").WithParam("data", "A;1;2;3;4;5;6;7;8;9;100\nB;2;3;4").WithParam("yrange", "0;110");
            var prims = Drawn(r, o);
            var boxes = prims.OfType<RectPrim>().Where(p => ChartTags.Is(p.Tag, ChartTags.Box)).ToList();
            Assert.Equal(2, boxes.Count);
            var anchors = Fx.Type(r, o).Anchors(o, Fx.Ctx()).Where(a => a.Id is not null).ToDictionary(a => a.Id!);
            var bl = anchors["plot.bl"].P; var tl = anchors["plot.tl"].P;
            double Y(double v) => bl.Y + (tl.Y - bl.Y) * v / 110;
            Assert.Equal(Y(7.75), boxes[0].Y, 6);
            Assert.Equal(Y(3.25), boxes[0].Y + boxes[0].H, 6);
            Assert.Equal(Y(5.5), anchors["s1.1"].P.Y, 6);
            Assert.Single(prims, p => p.Tag == "outlier.0");
            Assert.DoesNotContain(Drawn(r, Act(r, o, "chart.outliers").Apply(o)), p => ChartTags.Is(p.Tag, ChartTags.Outlier));
            Assert.Equal(3, Act(r, o, "chart.whis").Input!.Apply(o, "3").Num("whis", 0));
            // One block per category (single columns) and column-wise samples under a header.
            Assert.Equal(new[] { "x", "y" }, BoxChart.Groups("x\n1\n2\n3\n\ny\n4\n5").Categories);
            var cols = BoxChart.Groups("P;Q\n1;10\n2;20\n3;30");
            Assert.Equal(new[] { "P", "Q" }, cols.Categories);
            Assert.Equal(new double[] { 10, 20, 30 }, cols.Series[0].Samples[1]);
        }

        [Fact]
        public void HistogramCountsTheSamplesInEqualBins()
        {
            var (edges, counts) = Stats.Histogram(new[] { (IReadOnlyList<double>)new double[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 } }, 5);
            Assert.Equal(new[] { 0, 1.8, 3.6, 5.4, 7.2, 9 }, edges.Select(e => Math.Round(e, 9)));
            Assert.Equal(new[] { 2, 2, 2, 2, 2 }, counts[0]);
            var (_, c2) = Stats.Histogram(new[] { (IReadOnlyList<double>)new double[] { 1, 1, 1, 4 }, new double[] { 2, 3, 99 } }, 3, (1, 4));
            Assert.Equal(new[] { 3, 0, 1 }, c2[0]);
            Assert.Equal(new[] { 0, 1, 1 }, c2[1]);
            Assert.Equal(6, Stats.SturgesBins(30));
            Assert.Equal(1, Stats.SturgesBins(1));
            Assert.Equal(5, Stats.SturgesBins(16));

            var r = Fx.Reg();
            var o = Fx.Item(r, "chart.histogram");
            var bars = Drawn(r, o).OfType<RectPrim>().Where(p => ChartTags.Is(p.Tag, ChartTags.Bar)).ToList();
            Assert.Equal(6, bars.Count);
            var five = o.WithParam("bins", 5.0).WithParam("data", "0;1;2;3;4;5;6;7;8;9");
            var fb = Drawn(r, five).OfType<RectPrim>().Where(p => ChartTags.Is(p.Tag, ChartTags.Bar)).ToList();
            Assert.Equal(5, fb.Count);
            Assert.All(fb, b => Assert.Equal(fb[0].H, b.H, 6));
            // Bin actions.
            Assert.Equal(7, Act(r, o, "chart.bins.more").Apply(o).Num("bins", 0));
            Assert.Equal(5, Act(r, o, "chart.bins.fewer").Apply(o).Num("bins", 0));
            Assert.Equal("auto", Act(r, o, "chart.bins").Input!.Value);
            Assert.Equal(12, Act(r, o, "chart.bins").Input!.Apply(o, "12").Num("bins", 0));
            Assert.False(Act(r, o, "chart.bins").Input!.Apply(five, "auto").Params.ContainsKey("bins"));
        }

        // ------------------------------------------------------------------ Gantt

        [Fact]
        public void GanttDrawsTasksMilestonesAndTheTodayLine()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "chart.gantt").WithParam("data", "Build\nA;0;3\nB;2;4\n\nTest\nC;6;2\nGo live;8").WithParam("today", "5").WithParam("xrange", "0;10");
            var (tasks, groups) = GanttChart.Tasks(o.Str("data", ""), Lang.En);
            Assert.Equal(4, tasks.Count);
            Assert.Equal(new string?[] { "Build", "Test" }, groups);
            Assert.True(tasks[3].Milestone);
            var prims = Drawn(r, o);
            var bars = prims.OfType<RectPrim>().Where(p => ChartTags.Is(p.Tag, ChartTags.Bar)).ToList();
            Assert.Equal(3, bars.Count);
            Assert.Equal(new[] { "bar.0", "bar.0", "bar.1" }, bars.Select(b => b.Tag));
            var anchors = Fx.Type(r, o).Anchors(o, Fx.Ctx()).Where(a => a.Id is not null).ToDictionary(a => a.Id!);
            double X(double t) => anchors["plot.bl"].P.X + (anchors["plot.br"].P.X - anchors["plot.bl"].P.X) * t / 10;
            Assert.Equal(X(2), bars[1].X, 6);
            Assert.Equal(X(6), bars[1].X + bars[1].W, 6);
            // The first task is on top.
            Assert.True(bars[0].Y < bars[1].Y);
            Assert.Single(prims, p => p is PathPrim && p.Tag == "milestone.1");
            var today = prims.OfType<LinePrim>().Single(p => p.Tag == ChartTags.Today);
            Assert.Equal(X(5), today.A.X, 6);
            Assert.Contains(prims.OfType<TextPrim>(), t => t.Tag == ChartTags.Today && t.Text == "Today");
            Assert.Contains(prims.OfType<TextPrim>(), t => ChartTags.Is(t.Tag, ChartTags.TickLabel) && t.Text == "Go live");
            Assert.Equal(2, prims.Count(p => ChartTags.Is(p.Tag, ChartTags.LegendText)));
            // Actions.
            var added = Act(r, o, "chart.gantt.add").Apply(o);
            var last = GanttChart.Tasks(added.Str("data", ""), Lang.En).Tasks.Last();
            Assert.Equal(("Task 5", 8.0, 2.0), (last.Name, last.Start, last.Duration));
            Assert.Equal(3, GanttChart.Tasks(Act(r, o, "chart.gantt.remove").Apply(o).Str("data", ""), Lang.En).Tasks.Count);
            Assert.False(Act(r, o, "chart.today").Input!.Apply(o, "").Params.ContainsKey("today"));
            Assert.Equal("7.5", Act(r, o, "chart.today").Input!.Apply(o, "7,5").Str("today", ""));
        }

        // ------------------------------------------------------------------ every new chart

        [Theory]
        [MemberData(nameof(NewItems))]
        public void NewChartsRenderInsideTheBoxAtEverySize(string id)
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, id);
            foreach (var size in new[] { o.Size, new Size2(25, 18), new Size2(300, 60), new Size2(50, 160) })
                foreach (var lang in new[] { Lang.En, Lang.De })
                    Fx.AssertInsideBox(Fx.Render(r, o with { Size = size }, Fx.Ctx(lang, mono: size.W > 200)), size, id + " " + size.W + "x" + size.H + " " + lang);
            // Empty data never throws and still draws inside the box.
            var empty = o.WithParam("data", "");
            Fx.AssertFinite(Fx.Render(r, empty), id + " empty");
            Assert.NotEmpty(r.Parts(o, Fx.Ctx()));
        }

        [Fact]
        public void NewChartsSurviveSaveAndLoad()
        {
            var r = Fx.Reg();
            var objects = new[]
            {
                Fx.Item(r, "chart.donut", new Vec2(60, 40)).WithParam("center", "Σ 100").WithParam("labels", "namepercent").WithParam("direction", "ccw").WithParam("start", -30.0),
                Fx.Item(r, "chart.polar.radar", new Vec2(170, 40)).WithParam("rrange", "0;12").WithParam("closed", false),
                Fx.Item(r, "chart.sankey", new Vec2(60, 120)).WithParam("data", "\"Coal, lignite\";Power;12,5\nPower;Homes;12,5"),
                Fx.Item(r, "chart.gantt", new Vec2(170, 120)).WithParam("today", "3.5"),
                Fx.Item(r, "chart.box", new Vec2(60, 200)).WithParam("whis", 2.0).WithParam("outliers", false),
                Fx.Item(r, "chart.histogram", new Vec2(170, 200)).WithParam("bins", 9.0),
            };
            objects[0] = PartLooks.WithLook(objects[0], "slice.2", p => p with { Style = new Style { Fill = Fill.Solid("#00aa00") } });
            var doc0 = SketchDocument.Create();
            var doc = Edits.Add(doc0, doc0.Pages[0].Id, objects);
            var back = DocumentJson.Deserialize(DocumentJson.Serialize(doc, indented: true));
            Assert.Equal(DocumentJson.Serialize(doc), DocumentJson.Serialize(back));
            var compact = DocumentJson.FromCompact(DocumentJson.ToCompact(doc));
            foreach (var o in objects)
            {
                var b = back.Get(o.Id)!;
                var c = compact.Get(o.Id)!;
                foreach (var kv in o.Params) { Assert.Equal(kv.Value, b.Params[kv.Key]); Assert.Equal(kv.Value, c.Params[kv.Key]); }
                Assert.Equal(Drawn(r, o).Count, Drawn(r, b).Count);
            }
            Assert.Equal("#00aa00", back.Get(objects[0].Id)!.Parts!["slice.2"].Style!.Fill!.Color);
            Assert.Equal(new[] { "Coal, lignite", "Power", "Homes" },SankeyModel.Parse(back.Get(objects[2].Id)!.Str("data", "")).Nodes);
        }

        [Fact]
        public void CodeExportsReportThePresentationCharts()
        {
            var r = Fx.Reg();
            foreach (var id in new[] { "chart.pie", "chart.polar", "chart.sankey", "chart.gantt", "chart.box", "chart.histogram" })
            {
                var o = Fx.Item(r, id);
                Assert.Contains("could not be exported", ExportTests.Py(r, Lang.En, o));
                Assert.Contains("could not be exported", ExportTests.Tex(r, Lang.De, o));
            }
            var doc0 = SketchDocument.Create();
            var pie = Fx.Item(r, "chart.pie");
            var doc = Edits.Add(doc0, doc0.Pages[0].Id, pie);
            Assert.Contains("\\begin{tikzpicture}", new TikzExporter().Export(r, doc, new[] { doc.Get(pie.Id)! }, Lang.En));
        }

        [Fact]
        public void CleanSlidesCleanThePresentationCharts()
        {
            var r = Fx.Reg();
            var norm = new SanitizeOptions { Mode = SanitizeMode.Normalised, ReplaceTexts = true, RenameCategories = true };
            var sankey = Fx.Item(r, "chart.sankey").WithParam("data", "Coal;Power;40\nPower;Homes;30\nPower;Losses;10");
            var s = r.TypeOf(sankey).Sanitize(sankey, norm, new Random(3));
            var m = SankeyModel.Parse(s.Str("data", ""));
            Assert.Equal(new[] { "A", "B", "C", "D" }, m.Nodes);
            Assert.Equal(new[] { 1, 0.75, 0.25 }, m.Flows.Select(f => f.Value));
            var pie = Fx.Item(r, "chart.pie").WithParam("data", "North;20\nSouth;60");
            var p = r.TypeOf(pie).Sanitize(pie, norm, new Random(3));
            var slices = PieChart.Slices(p.Str("data", ""), 0, true);
            Assert.DoesNotContain(slices, x => x.Name is "North" or "South");
            Assert.Equal(new[] { 90.0, 270.0 }, slices.Select(x => Math.Round(x.Sweep, 2)));
            var box = Fx.Item(r, "chart.box");
            var rb = r.TypeOf(box).Sanitize(box, new SanitizeOptions { Mode = SanitizeMode.Randomised }, new Random(5));
            var before = BoxChart.Groups(box.Str("data", BoxChart.DefaultData));
            var after = BoxChart.Groups(rb.Str("data", ""));
            Assert.NotEqual(box.Str("data", BoxChart.DefaultData), rb.Str("data", ""));
            // The order of the samples (and so the quartiles' ranks) stays.
            var a0 = before.Series[0].Samples[0]; var a1 = after.Series[0].Samples[0];
            Assert.Equal(Enumerable.Range(0, a0.Count).OrderBy(i => a0[i]), Enumerable.Range(0, a1.Count).OrderBy(i => a1[i]));
            var gantt = Fx.Item(r, "chart.gantt");
            var g = r.TypeOf(gantt).Sanitize(gantt, norm, new Random(1));
            var tasks = GanttChart.Tasks(g.Str("data", ""), Lang.En).Tasks;
            Assert.Equal(0, tasks.Min(t => t.Start), 9);
            Assert.Equal(1, tasks.Max(t => t.End), 9);
            Assert.InRange(GanttChart.Today(g)!.Value, 0, 1);
        }

        // ------------------------------------------------------------------ matplotlib import

        private static readonly Size2 Page = new(280, 157.5);

        [Fact]
        public void MatplotlibPieScriptBecomesAPieChart()
        {
            const string code = @"
import matplotlib.pyplot as plt
sizes = [15, 30, 45, 10]
labels = ['Frogs', 'Hogs', 'Dogs', 'Logs']
fig, ax = plt.subplots()
ax.pie(sizes, labels=labels, autopct='%1.1f%%', startangle=90, counterclock=False, wedgeprops=dict(width=0.4))
ax.axis('equal')
ax.set_title('Pets')
plt.show()
";
            var res = ChartCodeImport.Matplotlib(Fx.Reg(), code, Page);
            Assert.True(res.Report.Skipped.Count == 0, string.Join("\n", res.Report.Skipped.Select(s => s.Line + ": " + s.Reason.En)));
            var o = Assert.Single(res.Charts);
            Assert.Equal(PieChart.TypeId, o.Type);
            var slices = PieChart.Slices(o.Str("data", ""), o.Num("start", 99), o.Str("direction", "") == "cw");
            Assert.Equal(new[] { "Frogs", "Hogs", "Dogs", "Logs" }, slices.Select(s => s.Name));
            Assert.Equal(new double[] { 15, 30, 45, 10 }, slices.Select(s => s.Value));
            Assert.Equal(0, o.Num("start", 99), 9);
            Assert.Equal("cw", o.Str("direction", ""));
            Assert.Equal(0.6, o.Num("hole", 0), 9);
            Assert.Equal("namepercent", o.Str("labels", ""));
            Assert.Equal("Pets", o.Str("title", ""));
            Assert.Contains("donut", res.Report.Imported[0].En);
            Assert.Contains("4 slices", res.Report.Imported[0].En);
            Fx.AssertInsideBox(Fx.Render(Fx.Reg(), o), o.Size, "imported pie");
        }

        [Fact]
        public void MatplotlibPieDefaultsAndLimitationsAreReported()
        {
            const string code = "import matplotlib.pyplot as plt\nplt.pie([1, 2, 3], explode=[0, 0.1, 0], colors=['r', 'g', 'b'])\nplt.legend(['a', 'b', 'c'])\n";
            var res = ChartCodeImport.Matplotlib(Fx.Reg(), code, Page);
            var o = Assert.Single(res.Charts);
            // matplotlib starts at 3 o'clock and runs counter-clockwise.
            Assert.Equal(90, o.Num("start", 0), 9);
            Assert.Equal("ccw", o.Str("direction", ""));
            Assert.Equal("none", o.Str("labels", ""));
            Assert.Equal("right", o.Str("legend", ""));
            Assert.Equal(2, res.Report.Skipped.Count(s => s.Line == 2));
        }

        [Fact]
        public void MatplotlibPolarScriptBecomesAPolarChart()
        {
            const string code = @"
import numpy as np
import matplotlib.pyplot as plt
theta = np.linspace(0, 2 * np.pi, 9)
r = [1, 2, 3, 2, 1, 2, 3, 2, 1]
fig, ax = plt.subplots(subplot_kw={'projection': 'polar'})
ax.plot(theta, r, label='gain')
ax.fill(theta, r, alpha=0.25)
ax.set_theta_zero_location('N')
ax.set_theta_direction(-1)
ax.set_rmax(4)
ax.legend()
";
            var res = ChartCodeImport.Matplotlib(Fx.Reg(), code, Page);
            Assert.True(res.Report.Skipped.Count == 0, string.Join("\n", res.Report.Skipped.Select(s => s.Line + ": " + s.Reason.En)));
            var o = Assert.Single(res.Charts);
            Assert.Equal(PolarChart.TypeId, o.Type);
            var series = Assert.Single(ChartData.Xy(ChartData.Parse(o.Str("data", ""))));
            Assert.Equal("gain", series.Name);
            Assert.Equal(new double[] { 0, 45, 90, 135, 180, 225, 270, 315, 360 }, series.Points.Select(p => Math.Round(p.X, 6)));
            Assert.Equal("area", o.Str("style", ""));
            Assert.Equal("top", o.Str("zero", ""));
            Assert.Equal("0;4", o.Str("rrange", ""));
            Assert.Equal("right", o.Str("legend", ""));
            Assert.Contains("polar chart", res.Report.Imported[0].En);
            // 3d projections are still reported.
            var three = ChartCodeImport.Matplotlib(Fx.Reg(), "import matplotlib.pyplot as plt\nfig = plt.figure()\nax = fig.add_subplot(111, projection='3d')\nax.plot([1, 2], [3, 4])\n", Page);
            Assert.Contains(three.Report.Skipped, s => s.Line == 3);
        }
    }
}
