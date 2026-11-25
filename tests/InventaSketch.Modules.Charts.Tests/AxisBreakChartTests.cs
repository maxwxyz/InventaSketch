using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Charts.Tests
{
    /// <summary>Charts v2 stage 3: axis breaks drawn by the XY chart and the figure (parameters, mapping, markers, actions, export).</summary>
    public class AxisBreakChartTests
    {
        private static List<Primitive> Drawn(Registry r, SketchObject o) => Prim.Flatten(Fx.Render(r, o)).ToList();

        private static SketchObject Xy(Registry r, string data, string? ybreaks = null, string? xbreaks = null)
        {
            var o = Fx.Item(r, "chart.line").WithParam("data", data).WithParam("style", "linepoints");
            if (ybreaks is not null) o = o.WithParam("ybreaks", ybreaks);
            if (xbreaks is not null) o = o.WithParam("xbreaks", xbreaks);
            return o;
        }

        private static ContextAction Act(Registry r, SketchObject o, string id, Lang lang = Lang.En)
            => r.TypeOf(o).Actions(o, new RenderContext(SketchDocument.Create(), lang)).Single(a => a.Id == id);

        // ------------------------------------------------------------------ text form

        [Fact]
        public void BreakTextIsParsedTolerantlyAndFormattedInvariant()
        {
            Assert.Equal(new[] { (20.0, 80.0), (-50.0, -20.0), (1.5, 2.5), (10.0, 20.0), (3.0, 4.0) },
                ChartBreaks.Parse("20-80; -50--20; 1,5-2,5 | 10..20; 4 – 3; nonsense; 5-5"));
            Assert.Equal("20-80; -50--20; 1.5-2.5", ChartBreaks.Format(new[] { (20.0, 80.0), (-50.0, -20.0), (1.5, 2.5) }));
            Assert.True(ChartBreaks.TryPair("1e-3-2e-3", out var e));
            Assert.Equal((0.001, 0.002), e);
            Assert.Empty(ChartBreaks.ParseInput("  ", Lang.En));
            Assert.Throws<FormatException>(() => ChartBreaks.ParseInput("20-80; abc", Lang.En));
            var de = Assert.Throws<FormatException>(() => ChartBreaks.ParseInput("abc", Lang.De));
            Assert.Contains("Bereich", de.Message);
        }

        // ------------------------------------------------------------------ axis

        [Fact]
        public void AxisWithBreaksMapsLikeTheBrokenAxisAndSkipsHiddenTicks()
        {
            var plain = Axis.Linear(Array.Empty<double>(), (0, 100));
            var a = plain.WithBreaks(new[] { (20.0, 80.0) });
            a.SetLength(100);
            Assert.True(a.HasBreaks);
            var reference = new BrokenAxis(0, 100, 100, new[] { new AxisBreak(20, 80, AxisBreaks.RequiredGap(BreakMarkerStyle.Zigzag, AxisBreaks.AxisMarkerWidth)) });
            foreach (var v in new[] { 0.0, 10, 20, 50, 80, 90, 100 })
            {
                Assert.Equal(reference.T(v), a.T(v), 9);
                Assert.Equal(v, a.V(a.T(v)), 6);
            }
            Assert.True(a.InBreak(50));
            Assert.False(a.Visible(50));
            Assert.True(a.Visible(20));
            var ticks = a.Ticks(Lang.En).Select(t => t.Value).ToList();
            Assert.DoesNotContain(ticks, v => v > 20 && v < 80);
            Assert.Contains(0.0, ticks);
            Assert.Contains(100.0, ticks);
            // Log and category axes and breaks outside the range stay unchanged.
            var log = Axis.Logarithmic(new[] { 1.0, 1000 });
            Assert.Same(log, log.WithBreaks(new[] { (10.0, 100.0) }));
            Assert.Same(plain, plain.WithBreaks(new[] { (200.0, 300.0) }));
            Assert.Same(plain, plain.WithBreaks(new[] { (-10.0, 110.0) }));
        }

        // ------------------------------------------------------------------ XY chart

        [Fact]
        public void XyChartDrawsMarkersHidesDataInTheBreakAndKeepsTheBox()
        {
            var r = Fx.Reg();
            var o = Xy(r, "0;10\n1;50\n2;90\n3;95", ybreaks: "30-70");
            foreach (var lang in new[] { Lang.En, Lang.De })
                Fx.AssertInsideBox(Fx.Render(r, o, Fx.Ctx(lang)), o.Size, "break " + lang);
            foreach (var size in new[] { new Size2(40, 30), new Size2(200, 60), new Size2(30, 120) })
                Fx.AssertInsideBox(Fx.Render(r, o with { Size = size }), size, "break " + size.W);

            var c = Fx.Type(r, o).Compose(o, Fx.Ctx());
            var p = c.F.Panels[0];
            Assert.True(p.Y.HasBreaks);
            double gapTop = c.F.P(p, 0, 70).Y, gapBottom = c.F.P(p, 0, 30).Y;
            Assert.True(gapBottom - gapTop > 1, "the break has a band");

            var drawn = Drawn(r, o);
            var marker = drawn.Where(x => x.Tag == "axisbreak.y").ToList();
            Assert.Equal(2, marker.Count); // band (white) + marker lines
            Assert.Contains(marker, x => x is PathPrim pp && pp.Style.Fill?.Color == "#ffffff");
            // The marker lies in the gap band and runs across the plot and the axis line.
            var mp = marker.SelectMany(Prim.Points).ToList();
            Assert.All(mp, q => Assert.InRange(q.Y, gapTop - 0.01, gapBottom + 0.01));
            // Whole zigzag teeth: the lines end at most one tooth before the ends of the band.
            Assert.True(mp.Min(q => q.X) < p.Area.X && mp.Max(q => q.X) >= p.Area.Right - AxisBreaks.WiggleStep);

            // The curve is interrupted: no vertex strictly inside the gap.
            var series = drawn.Single(x => x.Tag == "series.0");
            Assert.DoesNotContain(Prim.Points(series), q => q.Y > gapTop + 0.01 && q.Y < gapBottom - 0.01);
            Assert.True(((PathPrim)series).Segments.Count(s => s.Op == SegOp.M) >= 2, "two visible runs");
            // The hidden data point has no marker and no port; the others keep theirs.
            var ports = Fx.Type(r, o).Anchors(o, Fx.Ctx()).Where(a => a.Role == AnchorRole.Port).Select(a => a.Id).ToList();
            Assert.Contains("s1.1", ports);
            Assert.DoesNotContain("s1.2", ports);
            Assert.Contains("s1.3", ports);
            Assert.Contains("s1.4", ports);
            var port3 = Fx.Type(r, o).Anchors(o, Fx.Ctx()).Single(a => a.Id == "s1.3");
            Assert.Equal(c.F.P(p, 2, 90), port3.P);
            Assert.True(port3.P.Y < gapTop);
            // Tick labels and grid lines skip the hidden range.
            var labels = drawn.OfType<TextPrim>().Where(t => t.Tag == "label.y").Select(t => t.Text).ToList();
            Assert.DoesNotContain("40", labels);
            Assert.DoesNotContain("60", labels);
            var grid = drawn.Where(x => x.Tag == ChartTags.Grid).SelectMany(Prim.Points).ToList();
            Assert.DoesNotContain(grid, q => q.Y > gapTop + 0.01 && q.Y < gapBottom - 0.01);
        }

        [Fact]
        public void XBreakAndSlashStyle()
        {
            var r = Fx.Reg();
            var o = Xy(r, "0;1\n1;2\n2;3\n8;4\n9;5\n10;6", xbreaks: "3-7").WithParam("breakstyle", "slash");
            Fx.AssertInsideBox(Fx.Render(r, o), o.Size, "x break");
            var drawn = Drawn(r, o);
            var lines = drawn.Single(x => x.Tag == "axisbreak.x" && x is PathPrim pp && pp.Style.Fill?.Kind != FillKind.Solid);
            // Double slash: two straight strokes on the x axis.
            Assert.Equal(2, ((PathPrim)lines).Segments.Count(s => s.Op == SegOp.M));
            Assert.Equal(4, Prim.Points(lines).Count());
            var labels = drawn.OfType<TextPrim>().Where(t => t.Tag == "label.x").Select(t => t.Text).ToList();
            Assert.DoesNotContain("4", labels);
            Assert.DoesNotContain("5", labels);
            Assert.Contains(r.Parts(o, Fx.Ctx()), p => p.Key == "axisbreak.x" && p.Label.En == "x axis break" && p.Label.De == "Achsenbruch x");
        }

        [Fact]
        public void LibraryItemWithBreakListsThePartAndItsActions()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "chart.line.break");
            var parts = r.Parts(o, Fx.Ctx());
            Assert.Contains(parts, p => p.Key == "axisbreak.y" && p.Label.En == "y axis break");
            var onBreak = r.TypeOf(o).PartActions(o, "axisbreak.y", Fx.Ctx()).Select(a => a.Id).ToList();
            Assert.Contains("chart.ybreak", onBreak);
            Assert.Contains("chart.ybreak.remove", onBreak);
            var onAxis = r.TypeOf(o).PartActions(o, "axis.y", Fx.Ctx()).Select(a => a.Id).ToList();
            Assert.Contains("chart.ybreak", onAxis);
            Assert.Contains("chart.yscale", onAxis);
            Assert.Contains("chart.xbreak", r.TypeOf(o).PartActions(o, "axis.x", Fx.Ctx()).Select(a => a.Id));
            // Remove takes the breaks away.
            var removed = Act(r, o, "chart.ybreak.remove").Apply(o);
            Assert.DoesNotContain(r.Parts(removed, Fx.Ctx()), p => p.Key == "axisbreak.y");
            Assert.False(Act(r, removed, "chart.ybreak.remove").IsEnabled);
        }

        [Fact]
        public void AxisBreakInputSuggestsAGapSetsAndValidates()
        {
            var r = Fx.Reg();
            var o = Xy(r, "0;10\n1;12\n2;95\n3;98");
            var a = Act(r, o, "chart.ybreak");
            Assert.Equal("Break the y axis…", a.Label.En);
            Assert.Equal("y-Achse unterbrechen…", Act(r, o, "chart.ybreak", Lang.De).Label.De);
            Assert.False(string.IsNullOrEmpty(a.Input!.Value));            // suggestion in the data-free gap 12 … 95
            var sug = ChartBreaks.Parse(a.Input.Value).Single();
            Assert.True(sug.Lo >= 12 && sug.Hi <= 95 && sug.Hi > sug.Lo);
            var set = a.Input.Apply(o, "20-80");
            Assert.Equal("20-80", set.Str("ybreaks", ""));
            Assert.Equal("20-80", Act(r, set, "chart.ybreak").Input!.Value); // the current breaks
            Assert.Throws<FormatException>(() => a.Input.Apply(o, "twenty"));
            Assert.Equal("", a.Input.Apply(set, "").Str("ybreaks", "?"));
            // Even data: no suggestion.
            Assert.Equal("", Act(r, Fx.Item(r, "chart.line"), "chart.ybreak").Input!.Value);
        }

        [Fact]
        public void BreaksSurviveTheDocumentRoundTrip()
        {
            var r = Fx.Reg();
            var o = Xy(r, "0;10\n1;50\n2;90", ybreaks: "30-70", xbreaks: "0.5-0.8").WithParam("breakstyle", "slash");
            var d0 = SketchDocument.Create();
            var doc = Edits.Add(d0, d0.Pages[0].Id, o);
            var back = DocumentJson.Deserialize(DocumentJson.Serialize(doc));
            var o2 = back.Objects.Values.Single();
            Assert.Equal("30-70", o2.Str("ybreaks", ""));
            Assert.Equal("slash", o2.Str("breakstyle", ""));
            Assert.Equal(Drawn(r, o).Count, Drawn(r, o2).Count);
            var compact = DocumentJson.FromCompact(DocumentJson.ToCompact(doc)).Objects.Values.Single();
            Assert.Equal("0.5-0.8", compact.Str("xbreaks", ""));
        }

        // ------------------------------------------------------------------ figure

        private const string BrokenFigure = @"{ ""version"": 1, ""breakStyle"": ""zigzag"",
            ""x"": { ""label"": ""t"", ""breaks"": [[4, 6]] },
            ""panels"": [ { ""label"": ""T"", ""breaks"": ""20-70"" }, { ""label"": ""P"", ""breaks"": [ { ""from"": 2, ""to"": 3 } ] } ],
            ""series"": [ { ""x"": [0, 2, 3, 7, 10], ""y"": [10, 15, 90, 95, 100] }, { ""panel"": 1, ""x"": [0, 10], ""y"": [1, 4] } ],
            ""refs"": [ { ""kind"": ""h"", ""y"": 50 }, { ""kind"": ""v"", ""x"": 5 } ] }";

        [Fact]
        public void FigureSpecReadsAndWritesBreaks()
        {
            Assert.True(FigureSpec.TryParse(BrokenFigure, out var s, out _));
            Assert.Equal(new[] { (4.0, 6.0) }, s!.X.Breaks);
            Assert.Equal(new[] { (20.0, 70.0) }, s.Panels[0].Breaks);
            Assert.Equal(new[] { (2.0, 3.0) }, s.Panels[1].Breaks);
            var json = s.ToJson();
            Assert.Contains("\"breaks\":[[4,6]]", json);
            Assert.DoesNotContain("breakStyle", json); // default
            Assert.True(FigureSpec.TryParse(json, out var again, out _));
            Assert.Equal(json, again!.ToJson());
            // Tolerant: wrong entries are skipped, a slash style is kept.
            Assert.True(FigureSpec.TryParse(@"{ ""breakStyle"": ""slash"", ""x"": { ""breaks"": [ ""a"", [1], [5, 5], [3, ""1""] ] } }", out var t, out _));
            Assert.Equal(new[] { (1.0, 3.0) }, t!.X.Breaks);
            Assert.Contains("\"breakStyle\":\"slash\"", t.ToJson());
        }

        [Fact]
        public void FigureDrawsBreaksPerPanelAndHidesReferencesInThem()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "chart.figure.phases").WithParam("spec", BrokenFigure);
            Fx.AssertInsideBox(Fx.Render(r, o), o.Size, "figure with breaks");
            var keys = r.Parts(o, Fx.Ctx()).Select(p => p.Key).ToList();
            Assert.Contains("axisbreak.x", keys);
            Assert.Contains("axisbreak.y.1", keys);
            Assert.Contains("axisbreak.y.2", keys);
            // The value line at 50 lies in the y break of panel 1, the marker at x = 5 in the x break: neither is drawn.
            Assert.DoesNotContain(keys, k => k == "ref.0" || k == "ref.1");
            // Axis actions per panel: "axis.y.2" edits the breaks of the second panel.
            var act = r.TypeOf(o).PartActions(o, "axis.y.2", Fx.Ctx()).Single(a => a.Id == "chart.figure.ybreak");
            Assert.Equal("2-3", act.Input!.Value);
            var next = act.Input.Apply(o, "1.5-3.5");
            Assert.True(FigureSpec.TryParse(next.Str("spec", ""), out var ns, out _));
            Assert.Equal(new[] { (1.5, 3.5) }, ns!.Panels[1].Breaks);
            Assert.Equal(new[] { (20.0, 70.0) }, ns.Panels[0].Breaks);
            var remove = r.TypeOf(o).PartActions(o, "axisbreak.x", Fx.Ctx()).Single(a => a.Id == "chart.figure.xbreak.remove");
            Assert.True(FigureSpec.TryParse(remove.Apply(o).Str("spec", ""), out var rs, out _));
            Assert.Empty(rs!.X.Breaks);
            // Element actions: the y break of the first panel and the x break.
            var ids = r.TypeOf(o).Actions(o, Fx.Ctx()).Select(a => a.Id).ToList();
            Assert.Contains("chart.figure.ybreak", ids);
            Assert.Contains("chart.figure.xbreak.remove", ids);
        }

        // ------------------------------------------------------------------ export

        [Fact]
        public void ExportsSayThatBreaksAreNotExported()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "chart.line.break");
            var py = ExportTests.Py(r, Lang.En, o);
            Assert.Contains("# Note: Axis breaks are not exported", py);
            Assert.Contains("y: 25-85", py);
            ExportTests.AssertPythonBalanced(py, "break");
            var tex = ExportTests.Tex(r, Lang.De, o);
            Assert.Contains("% Note: Achsenbrüche werden nicht exportiert", tex);
            ExportTests.AssertLatexBalanced(tex, "break");
            var fig = Fx.Item(r, "chart.figure.phases").WithParam("spec", BrokenFigure);
            Assert.Contains("x: 4-6", ExportTests.Py(r, Lang.En, fig));
        }
    }
}
