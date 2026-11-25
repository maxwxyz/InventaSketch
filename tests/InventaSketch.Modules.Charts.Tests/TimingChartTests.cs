using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Charts.Tests
{
    /// <summary>Digital timing diagram (chart.timing): notation, rendering, parts, actions, cleaning and persistence.</summary>
    public class TimingChartTests
    {
        private static readonly string[] Presets = { "chart.timing", "chart.timing.clock", "chart.timing.bus", "chart.timing.handshake" };

        private static SketchObject Timing(Registry r, string signals) => Fx.Item(r, "chart.timing").WithParam("signals", signals);

        private static List<Primitive> Drawn(Registry r, SketchObject o, RenderContext? ctx = null) => Prim.Flatten(r.RenderLocal(o, ctx ?? Fx.Ctx())).ToList();

        private static string Kinds(TimingSignal s) => string.Join(" ", s.Segments.Select(g => g.Kind switch
        {
            WaveKind.Low => "0", WaveKind.High => "1", WaveKind.Undefined => "x", WaveKind.HighZ => "z", _ => "=" + g.Value,
        } + "@" + g.Start.ToString(CultureInfo.InvariantCulture) + "-" + g.End.ToString(CultureInfo.InvariantCulture)));

        // ------------------------------------------------------------------ notation

        [Fact]
        public void LevelsHoldAndMerge()
        {
            var set = TimingNotation.Parse("A: 0..1\nB: 0 1 1 0\nC: l h");
            Assert.Empty(set.Errors);
            Assert.Equal(4, set.Steps);
            Assert.Equal("0@0-3 1@3-4", Kinds(set.Signals[0]));
            Assert.Equal("0@0-1 1@1-3 0@3-4", Kinds(set.Signals[1]));
            // Shorter waves hold their last state to the end.
            Assert.Equal("0@0-1 1@1-4", Kinds(set.Signals[2]));
            Assert.Equal(new[] { "A", "B", "C" }, set.Signals.Select(s => s.Name));
        }

        [Fact]
        public void UndefinedHighImpedanceAndBusValues()
        {
            var set = TimingNotation.Parse("D: x.[A5].[B]=z.\nE;xxzz");
            Assert.Empty(set.Errors);
            Assert.Equal("x@0-2 =A5@2-4 =B@4-5 =@5-6 z@6-8", Kinds(set.Signals[0]));
            Assert.Equal("E", set.Signals[1].Name);
            Assert.Equal("x@0-2 z@2-8", Kinds(set.Signals[1]));
            // Two equal values in a row stay two boxes (a new value is a new transfer).
            Assert.Equal(2, TimingNotation.Parse("D: [A][A]").Signals[0].Segments.Count);
        }

        [Fact]
        public void ClockPeriodsAndTheClockKeyword()
        {
            var p = TimingNotation.Parse("C: p...").Signals[0];
            Assert.Equal(8, p.Segments.Count);
            Assert.Equal("1@0-0.5 0@0.5-1 1@1-1.5 0@1.5-2 1@2-2.5 0@2.5-3 1@3-3.5 0@3.5-4", Kinds(p));
            Assert.Equal("0@0-0.5 1@0.5-1", Kinds(TimingNotation.Parse("N: n").Signals[0]));

            var set = TimingNotation.Parse("CLK: clock 2 25%\nCLKN: clock inv\nEN: 00110011");
            Assert.Empty(set.Errors);
            Assert.Equal(8, set.Steps);
            var clk = set.Signals[0];
            Assert.True(clk.IsClock);
            Assert.Equal(2, clk.Period);
            Assert.Equal(0.25, clk.Duty, 9);
            Assert.Equal("1@0-0.5 0@0.5-2 1@2-2.5 0@2.5-4 1@4-4.5 0@4.5-6 1@6-6.5 0@6.5-8", Kinds(clk));
            Assert.StartsWith("0@0-0.5 1@0.5-1", Kinds(set.Signals[1]));
            Assert.Equal(16, set.Signals[1].Segments.Count);
            // Only clocks: eight steps (or four periods).
            Assert.Equal(8, TimingNotation.Parse("CLK: clock").Steps);
            Assert.Equal(12, TimingNotation.Parse("CLK: takt 3").Steps);
            Assert.Equal(0.3, TimingNotation.Parse("CLK: clk 1 0.3").Signals[0].Duty, 9);
        }

        [Fact]
        public void CommentsBlankLinesAndRowsWithoutName()
        {
            var set = TimingNotation.Parse("# bus cycle\n\nCLK: clock\n0110");
            Assert.Empty(set.Errors);
            Assert.Equal(2, set.Signals.Count);
            Assert.Equal("", set.Signals[1].Name);
            Assert.Equal(4, set.Signals[1].Line);
        }

        [Theory]
        [InlineData("A: 01q", 1, 6)]
        [InlineData("A: [AB", 1, 4)]
        [InlineData("A: .1", 1, 4)]
        [InlineData("OK: 01\nA: clock fast", 2, 0)]
        [InlineData("A:", 1, 0)]
        [InlineData("A: clock 0", 1, 0)]
        [InlineData("A: clock 1 150%", 1, 0)]
        public void ErrorsAreReportedNotThrown(string text, int line, int column)
        {
            var set = TimingNotation.Parse(text);
            var e = Assert.Single(set.Errors);
            Assert.Equal(line, e.Line);
            if (column > 0) Assert.Equal(column, e.Column);
            Assert.False(string.IsNullOrEmpty(e.Message.De));
            Assert.StartsWith("Zeile " + line, e.Text(Lang.De));
            // The diagram still renders.
            var r = Fx.Reg();
            var o = Timing(r, text);
            Fx.AssertInsideBox(Fx.Render(r, o), o.Size, text);
        }

        [Fact]
        public void MarkersAndSpans()
        {
            var errors = new List<TimingError>();
            var m = TimingNotation.Markers("t1=2; t2@5,5; 7; bad=x", errors);
            Assert.Equal(new[] { "t1", "t2", "t3" }, m.Select(x => x.Label));
            Assert.Equal(new[] { 2.0, 5.5, 7.0 }, m.Select(x => x.Time));
            Assert.Single(errors);
            var spans = TimingNotation.Spans("t1-t2; 2-3: t_h; t1-t1; t1-t9", m, errors);
            Assert.Equal(2, spans.Count);
            Assert.Equal((0, 1, "Δt"), (spans[0].From, spans[0].To, spans[0].Text));
            Assert.Equal((1, 2, "t_h"), (spans[1].From, spans[1].To, spans[1].Text));
            Assert.Equal(3, errors.Count);
        }

        [Fact]
        public void LongInputIsCut()
        {
            var set = TimingNotation.Parse("A: 0" + new string('.', 2000) + "\nB: clock 0.05");
            Assert.Equal(TimingNotation.MaxSteps, set.Steps);
            Assert.Equal(TimingNotation.MaxSteps, set.Signals[0].Segments[0].End);
            var r = Fx.Reg();
            var o = Timing(r, "A: 0" + new string('.', 2000) + "\nB: clock 0.05");
            Fx.AssertInsideBox(Fx.Render(r, o), o.Size, "long");
        }

        // ------------------------------------------------------------------ rendering and parts

        [Fact]
        public void DefaultItemInsertsWithoutParametersAndDrawsThreeSignals()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "chart.timing");
            Assert.Null(r.Item("chart.timing")!.Params);
            Assert.Equal(TimingChart.DefaultSignals, TimingChart.SignalsText(o));
            Assert.Equal(new Size2(100, 50), o.Size);
            // A bare object without any parameter draws the same.
            var bare = new SketchObject { Id = "b", Type = TimingChart.TypeId, Size = o.Size };
            Assert.Equal(Drawn(r, o).Count, Drawn(r, bare).Count);
            var prims = Drawn(r, o);
            foreach (var key in new[] { "signal.0", "signal.1", "signal.2", "name.0", "name.1", "name.2", "grid.major", "axis.x", "tick.x", "label.x", "axistitle.x" })
                Assert.Contains(prims, p => p.Tag == key);
            Assert.Equal(new[] { "CLK", "EN", "DATA" }, prims.OfType<TextPrim>().Where(t => ChartTags.Is(t.Tag, TimingTags.Name)).Select(t => t.Text));
            // DATA: value boxes with their texts, the undefined stretches hatched.
            var data = prims.Where(p => p.Tag == "signal.2").ToList();
            Assert.Equal(new[] { "A", "B" }, data.OfType<TextPrim>().Select(t => t.Text));
            var areas = data.OfType<PathPrim>().Where(p => p.IsClosed).ToList();
            Assert.Equal(2, areas.Count(a => a.Style.EffectiveFill.Kind == FillKind.Solid));
            Assert.Equal(2, areas.Count(a => a.Style.EffectiveFill.Kind == FillKind.Hatch));
            // One open freeform per wave.
            Assert.Equal(3, prims.OfType<PathPrim>().Count(p => ChartTags.Is(p.Tag, TimingTags.Signal) && !p.IsClosed));
            // Between the value boxes A and B both level lines cross (one runs from the top to the bottom).
            var wave = prims.OfType<PathPrim>().Single(p => p.Tag == "signal.2" && !p.IsClosed);
            var tops = wave.Segments.Select(s => s.P.Y).Min();
            var bots = wave.Segments.Select(s => s.P.Y).Max();
            var crossings = wave.Segments.Zip(wave.Segments.Skip(1), (p, q) => (p, q))
                .Count(s => s.q.Op == SegOp.L && Math.Abs(s.p.P.Y - tops) < 1e-6 && Math.Abs(s.q.P.Y - bots) < 1e-6 && s.q.P.X > s.p.P.X);
            Assert.Equal(3, crossings);
            // Time labels 0 … 8.
            Assert.Equal(Enumerable.Range(0, 9).Select(i => i.ToString(CultureInfo.InvariantCulture)), prims.OfType<TextPrim>().Where(t => t.Tag == "label.x").Select(t => t.Text));
        }

        [Fact]
        public void EveryPresetRendersInsideItsBoxInAnySize()
        {
            var r = Fx.Reg();
            foreach (var id in Presets)
            {
                var o = Fx.Item(r, id);
                Assert.Empty(TimingNotation.Parse(TimingChart.SignalsText(o)).Errors);
                foreach (var size in new[] { o.Size, new Size2(300, o.Size.H), new Size2(o.Size.W, 200), new Size2(12, 6), new Size2(600, 400) })
                    foreach (var mono in new[] { false, true })
                        Fx.AssertInsideBox(Fx.Render(r, o with { Size = size }, Fx.Ctx(Lang.De, mono)), size, id + " " + size.W + "x" + size.H);
            }
        }

        [Fact]
        public void PresetsHaveMarkersAndDimensionArrowsAsParts()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "chart.timing.bus");
            var parts = r.Parts(o, Fx.Ctx());
            var keys = parts.Select(p => p.Key).ToList();
            foreach (var key in new[] { "signal.0", "signal.4", "name.4", "ref.0", "ref.1", "dim.0", "grid.major", "axis.x" }) Assert.Contains(key, keys);
            Assert.Equal("signal.0", keys[0]);
            Assert.Equal("Signal CLK", parts.Single(p => p.Key == "signal.0").Label.En);
            Assert.Equal("Markierung t1", parts.Single(p => p.Key == "ref.0").Label.De);
            Assert.Equal("Maßpfeil Δt", parts.Single(p => p.Key == "dim.0").Label.De);
            Assert.Equal("Zeitachse", parts.Single(p => p.Key == "axis.x").Label.De);
            var prims = Drawn(r, o);
            var dim = prims.OfType<LinePrim>().Single(p => p.Tag == "dim.0");
            Assert.Equal(ArrowHead.Triangle, dim.Style.ArrowStart);
            Assert.Equal(ArrowHead.Triangle, dim.Style.ArrowEnd);
            // The arrow runs between the two marker lines.
            var markers = prims.OfType<LinePrim>().Where(p => ChartTags.Is(p.Tag, ChartTags.Ref)).Select(p => p.A.X).OrderBy(x => x).ToList();
            Assert.Equal(markers[0], Math.Min(dim.A.X, dim.B.X), 6);
            Assert.Equal(markers[1], Math.Max(dim.A.X, dim.B.X), 6);
            Assert.Contains(prims.OfType<TextPrim>(), t => t.Tag == "dim.0" && t.Text == "Δt");
        }

        [Fact]
        public void OneSignalCanBeRecolouredNatively()
        {
            var r = Fx.Reg();
            var o = PartLooks.WithLook(Fx.Item(r, "chart.timing"), "signal.1", p => p with { Style = new Style { Stroke = "#ff0000" } });
            var prims = Drawn(r, o);
            Assert.All(prims.Where(p => p.Tag == "signal.1" && p is PathPrim { IsClosed: false }), p => Assert.Equal("#ff0000", ((PathPrim)p).Style.Stroke));
            Assert.All(prims.OfType<PathPrim>().Where(p => p.Tag is "signal.0" or "signal.2" && !p.IsClosed), p => Assert.NotEqual("#ff0000", p.Style.Stroke));
            var hidden = PartLooks.WithLook(o, "name", p => p with { Hidden = true });
            Assert.DoesNotContain(Drawn(r, hidden), p => ChartTags.Is(p.Tag, TimingTags.Name));
        }

        [Fact]
        public void SharpEdgesAreVerticalAndSlopedEdgesAreNot()
        {
            var r = Fx.Reg();
            var o = Timing(r, "EN: 0011");
            static List<Vec2> Points(IEnumerable<Primitive> prims) => ((PathPrim)prims.Single(p => p.Tag == "signal.0" && p is PathPrim)).Segments.Select(s => s.P).ToList();
            var sharp = Points(Drawn(r, o.WithParam("slope", "sharp")));
            var sloped = Points(Drawn(r, o));
            Assert.Equal(4, sharp.Count);
            Assert.Equal(sharp[1].X, sharp[2].X, 9);
            Assert.True(sloped[2].X - sloped[1].X > 0.3);
            Assert.True(sharp[1].Y > sharp[2].Y, "rising edge goes up");
        }

        [Fact]
        public void EdgesAreDockingPorts()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "chart.timing");
            var anchors = Fx.Type(r, o).Anchors(o, Fx.Ctx()).ToList();
            var en = anchors.Where(a => a.Id is not null && a.Id.StartsWith("s2.", StringComparison.Ordinal)).ToList();
            Assert.Equal(2, en.Count);
            Assert.All(en, a => Assert.Equal(AnchorRole.Port, a.Role));
            Assert.True(en[1].P.X > en[0].P.X);
            // The clock has an edge every half step.
            Assert.Equal(15, anchors.Count(a => a.Id is not null && a.Id.StartsWith("s1.", StringComparison.Ordinal)));
            foreach (var id in new[] { "plot.tl", "plot.br", "plot.c", "origin", "axis.x" }) Assert.Contains(anchors, a => a.Id == id);
        }

        [Fact]
        public void ScalingKeepsTextSizeSane()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "chart.timing");
            double NameSize(SketchObject x) => Drawn(r, x).OfType<TextPrim>().First(t => t.Tag == "name.0").TextStyle.Size;
            var normal = NameSize(o);
            Assert.InRange(normal, 2, 3.5);
            // Stretching only one direction keeps the text; uniform scaling scales it; it never gets huge or vanishes.
            Assert.Equal(normal, NameSize(o with { Size = new Size2(300, 50) }), 6);
            Assert.Equal(normal * 2, NameSize(o with { Size = new Size2(200, 100) }), 6);
            Assert.InRange(NameSize(o with { Size = new Size2(2000, 1000) }), 7, 8);
            Assert.InRange(NameSize(o with { Size = new Size2(20, 10) }), 0.3, 1);
            // Many signals in a small box: rows shrink, text follows the row height.
            var many = Timing(r, string.Join("\n", Enumerable.Range(1, 20).Select(i => "S" + i + ": 0101")));
            Assert.True(NameSize(many) < normal);
            Fx.AssertInsideBox(Fx.Render(r, many), many.Size, "20 signals");
            // Wide names are fitted into at most a third of the width.
            var wide = Timing(r, "A_VERY_LONG_SIGNAL_NAME_INDEED_FOR_TESTING: 0101");
            Fx.AssertInsideBox(Fx.Render(r, wide), wide.Size, "wide name");
        }

        [Fact]
        public void GridLabelsAndAxisCanBeSwitchedOff()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "chart.timing").WithParam("grid", false).WithParam("timelabels", false);
            var prims = Drawn(r, o);
            Assert.DoesNotContain(prims, p => ChartTags.Is(p.Tag, "grid"));
            Assert.DoesNotContain(prims, p => p.Tag is "label.x" or "axis.x" or "tick.x");
            Fx.AssertInsideBox(Fx.Render(r, o), o.Size, "plain");
            // A time unit and step: labels in time units, axis title with the unit.
            var t = Fx.Item(r, "chart.timing").WithParam("dt", 10.0).WithParam("tunit", "ns");
            var labels = Drawn(r, t, Fx.Ctx(Lang.De)).OfType<TextPrim>().ToList();
            Assert.Contains(labels, x => x.Tag == "label.x" && x.Text == "80");
            Assert.Contains(labels, x => x.Tag == "axistitle.x" && x.Text == "t in ns");
        }

        // ------------------------------------------------------------------ actions

        private static ContextAction Act(Registry r, SketchObject o, string id, Lang lang = Lang.En)
            => r.TypeOf(o).Actions(o, new RenderContext(SketchDocument.Create(), lang)).Single(a => a.Id == id);

        [Fact]
        public void SignalsInputUsesRowsAndRejectsErrors()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "chart.timing");
            var a = Act(r, o, "chart.data");
            Assert.Equal("CLK: clock | EN: 00111100 | DATA: xx[A].[B].xx", a.Input!.Value);
            Assert.Equal(TimingChart.DefaultSignals, a.Input.Apply(o, a.Input.Value).Str("signals", ""));
            var next = a.Input.Apply(o, "A: 01 | B: 10 | C;[X].");
            Assert.Equal("A: 01\nB: 10\nC;[X].", next.Str("signals", ""));
            var ex = Assert.Throws<FormatException>(() => Act(r, o, "chart.data", Lang.De).Input!.Apply(o, "A: 01 | B: 1q"));
            Assert.StartsWith("Zeile 2, Zeichen 5", ex.Message);
            Assert.Throws<FormatException>(() => a.Input.Apply(o, "  "));
        }

        [Fact]
        public void AddAndRemoveSignals()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "chart.timing");
            var added = Act(r, o, "chart.timing.add").Apply(o);
            var set = TimingNotation.Parse(TimingChart.SignalsText(added));
            Assert.Equal(4, set.Signals.Count);
            Assert.Equal("S4", set.Signals[3].Name);
            Assert.Equal("0@0-4 1@4-8", Kinds(set.Signals[3]));
            var removed = Act(r, added, "chart.timing.remove").Apply(added);
            Assert.Equal(TimingChart.DefaultSignals, TimingChart.SignalsText(removed));
            var one = Timing(r, "A: 01");
            Assert.False(Act(r, one, "chart.timing.remove").IsEnabled);
            Assert.Equal(one.Str("signals", ""), Act(r, one, "chart.timing.remove").Apply(one).Str("signals", ""));
            // Part actions: rename and remove one signal.
            var parts = r.PartActions(o, "name.1", Fx.Ctx());
            var rename = parts.Single(x => x.Id == "chart.timing.rename");
            Assert.Equal("EN", rename.Input!.Value);
            Assert.Equal("CLK: clock\nOE: 00111100\nDATA: xx[A].[B].xx", rename.Input.Apply(o, "OE").Str("signals", ""));
            var gone = r.PartActions(o, "signal.0", Fx.Ctx()).Single(x => x.Id == "chart.timing.removeAt").Apply(o);
            Assert.Equal("EN: 00111100\nDATA: xx[A].[B].xx", gone.Str("signals", ""));
            Assert.Contains(r.PartActions(o, "grid.major", Fx.Ctx()), x => x.Id == "chart.grid");
            Assert.Contains(r.PartActions(o, "label.x", Fx.Ctx()), x => x.Id == "chart.timing.labels");
        }

        [Fact]
        public void TogglesCycleAndMarkerInputs()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "chart.timing");
            Assert.False(Act(r, o, "chart.grid").Apply(o).Flag("grid", true));
            Assert.False(Act(r, o, "chart.timing.labels").Apply(o).Flag("timelabels", true));
            var slope = Act(r, o, "chart.timing.slope=sharp");
            Assert.Equal("Sharp (vertical)", slope.Label.En);
            Assert.Equal("Edges", slope.Choice!.Label.En);
            Assert.False(slope.IsActive);
            Assert.True(Act(r, o, "chart.timing.slope=sloped").IsActive);
            Assert.Equal("sharp", slope.Apply(o).Str("slope", ""));
            var markers = Act(r, o, "chart.timing.markers");
            var m = markers.Input!.Apply(o, "t1=2; t2=6");
            Assert.Equal("t1=2; t2=6", m.Str("markers", ""));
            Assert.Throws<FormatException>(() => markers.Input.Apply(o, "t1=soon"));
            Assert.False(Act(r, o, "chart.timing.spans").IsEnabled);
            var spans = Act(r, m, "chart.timing.spans");
            Assert.True(spans.IsEnabled);
            var withSpan = spans.Input!.Apply(m, "t1-t2: t_p");
            Assert.Contains(Drawn(r, withSpan).OfType<TextPrim>(), t => t.Tag == "dim.0" && t.Text == "t_p");
            Assert.Throws<FormatException>(() => spans.Input.Apply(m, "t1-t7"));
            // Removing a marker from its part.
            var removed = r.PartActions(withSpan, "ref.0", Fx.Ctx()).Single(x => x.Id == "chart.timing.marker.remove").Apply(withSpan);
            Assert.Equal("t2=6", removed.Str("markers", ""));
            Assert.DoesNotContain(Drawn(r, removed), p => ChartTags.Is(p.Tag, TimingTags.Dimension));
        }

        // ------------------------------------------------------------------ cleaning, persistence, export

        [Fact]
        public void SanitizeReplacesNamesAndValues()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "chart.timing.bus").WithParam("signals", "CLK: clock\nADDR: x[A0].[A1].[A0]x\nDATA: z[CAFE].z").WithParam("title", "Write cycle of the secret bus");
            var type = r.TypeOf(o);
            var texts = type.Sanitize(o, new SanitizeOptions { Mode = SanitizeMode.None, ReplaceTexts = true }, new Random(1));
            Assert.Equal("S1: clock\nS2: x[A0].[A1].[A0]x\nS3: z[CAFE].z", texts.Str("signals", ""));
            Assert.NotEqual(o.Str("title", ""), texts.Str("title", ""));

            var norm = type.Sanitize(o, new SanitizeOptions { Mode = SanitizeMode.Normalised }, new Random(1));
            Assert.Equal("CLK: clock\nADDR: x[D1].[D2].[D1]x\nDATA: z[D3].z", norm.Str("signals", ""));

            var rand = type.Sanitize(o, new SanitizeOptions { Mode = SanitizeMode.Randomised }, new Random(7));
            var before = TimingNotation.Parse(TimingChart.SignalsText(o));
            var after = TimingNotation.Parse(TimingChart.SignalsText(rand));
            Assert.Empty(after.Errors);
            var bv = before.Signals.SelectMany(s => s.Segments).Select(g => g.Value).ToList();
            var av = after.Signals.SelectMany(s => s.Segments).Select(g => g.Value).ToList();
            Assert.Equal(before.Signals.SelectMany(s => s.Segments).Select(g => (g.Kind, g.Start, g.End)), after.Signals.SelectMany(s => s.Segments).Select(g => (g.Kind, g.Start, g.End)));
            Assert.Equal(bv.Count, av.Count);
            for (var i = 0; i < bv.Count; i++)
                if (!string.IsNullOrEmpty(bv[i])) Assert.NotEqual(bv[i], av[i]);
            // Equal values stay equal.
            var addr = after.Signals[1].Segments.Where(g => g.Kind == WaveKind.Bus).Select(g => g.Value).ToList();
            Assert.Equal(addr[0], addr[2]);
            Assert.NotEqual(addr[0], addr[1]);
        }

        [Fact]
        public void SanitizeNormalisesTheTimeScaleAndRenamesMarkers()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "chart.timing").WithParam("dt", 10.0).WithParam("tunit", "ns").WithParam("markers", "start=20; stop=60").WithParam("spans", "start-stop: setup time");
            var c = r.TypeOf(o).Sanitize(o, new SanitizeOptions { Mode = SanitizeMode.Normalised, ReplaceTexts = true }, new Random(3));
            Assert.Equal(1.0, c.Num("dt", 0));
            Assert.Equal("", c.Str("tunit", "?"));
            Assert.Equal("t1=2; t2=6", c.Str("markers", ""));
            Assert.Equal("t1-t2", c.Str("spans", ""));
            // Markers stay at the same time step.
            Assert.Equal(TimingNotation.Markers(o.Str("markers", "")).Select(m => m.Time / 10), TimingNotation.Markers(c.Str("markers", "")).Select(m => m.Time));
            Assert.Equal(2, Drawn(r, c).OfType<LinePrim>().Count(p => ChartTags.Is(p.Tag, ChartTags.Ref)));
        }

        [Fact]
        public void ModelSurvivesJsonRoundTrip()
        {
            var r = Fx.Reg();
            var o = PartLooks.WithLook(Fx.Item(r, "chart.timing.handshake"), "signal.1", p => p with { Style = new Style { Stroke = "#00aa00" } });
            var doc0 = SketchDocument.Create();
            var doc = Edits.Add(doc0, doc0.Pages[0].Id, o);
            var back = DocumentJson.FromCompact(DocumentJson.ToCompact(doc)).Get(o.Id)!;
            Assert.Equal(o.Type, back.Type);
            Assert.Equal(o.Size, back.Size);
            Assert.Equal(TimingChart.SignalsText(o), TimingChart.SignalsText(back));
            Assert.Equal(o.Str("markers", ""), back.Str("markers", "?"));
            Assert.Equal(o.Str("spans", ""), back.Str("spans", "?"));
            Assert.Equal("#00aa00", back.Parts!["signal.1"].Style!.Stroke);
            Assert.Equal(Drawn(r, o).Count, Drawn(r, back).Count);
        }

        [Fact]
        public void CodeExportsReportTimingDiagramsAndTikzDrawsThem()
        {
            var r = Fx.Reg();
            var o = Fx.Item(r, "chart.timing");
            Assert.Contains("could not be exported: Timing diagrams cannot be exported", ExportTests.Py(r, Lang.En, o));
            Assert.Contains("could not be exported: Zeitdiagramme", ExportTests.Tex(r, Lang.De, o));
            var doc0 = SketchDocument.Create();
            var doc = Edits.Add(doc0, doc0.Pages[0].Id, o);
            var tex = new TikzExporter().Export(r, doc, new[] { doc.Get(o.Id)! }, Lang.En);
            Assert.Contains("\\begin{tikzpicture}", tex);
            Assert.Contains("DATA", tex);
        }

        // ------------------------------------------------------------------ gallery

        /// <summary>Writes every preset (and a few variants) as SVG into the test output folder "gallery/timing" for visual review.</summary>
        [Fact]
        public void WriteGallery()
        {
            var r = Fx.Reg();
            var dir = Path.Combine(AppContext.BaseDirectory, "gallery", "timing");
            Directory.CreateDirectory(dir);
            var cases = Presets.Select(id => (id, Fx.Item(r, id), Lang.En)).ToList();
            cases.Add(("chart.timing.sharp", Fx.Item(r, "chart.timing").WithParam("slope", "sharp").WithParam("title", "Sharp edges"), Lang.En));
            cases.Add(("chart.timing.bus.de.mono", Fx.Item(r, "chart.timing.bus").WithParam("dt", 10.0).WithParam("tunit", "ns").WithParam("markers", "t1=20; t2=50"), Lang.De));
            cases.Add(("chart.timing.close", Fx.Item(r, "chart.timing.handshake").WithParam("markers", "t1=2; t2=2.2; t3=2.4").WithParam("spans", "t1-t3; t1-t2"), Lang.En));
            cases.Add(("chart.timing.small", Fx.Item(r, "chart.timing") with { Size = new Size2(40, 22) }, Lang.En));
            var html = new StringBuilder("<!DOCTYPE html><html><head><meta charset=\"utf-8\"><title>Timing</title></head><body style=\"background:#fff\">\n");
            foreach (var (name, o, lang) in cases)
            {
                var ctx = new RenderContext(SketchDocument.Create(), lang, name.EndsWith(".mono", StringComparison.Ordinal));
                var prims = r.RenderLocal(o, ctx).ToList();
                double w = o.Size.W, h = o.Size.H;
                prims.Insert(0, Prim.Rect(-w / 2, -h / 2, w, h, new Style { Stroke = "#c8d6e5", StrokeWidth = 0.08, Dash = DashStyle.Dash }));
                var vb = new Rect(-w / 2, -h / 2, w, h).Inflate(1);
                var svg = SvgRenderer.RenderPrimitives(prims, new SvgOptions { ViewBox = vb });
                var px = 800 / vb.W;
                svg = svg.Replace("<svg ", "<svg width=\"" + (vb.W * px).ToString("0", CultureInfo.InvariantCulture) + "\" height=\"" + (vb.H * px).ToString("0", CultureInfo.InvariantCulture) + "\" ");
                File.WriteAllText(Path.Combine(dir, name + ".svg"), svg);
                html.Append("<figure>").Append(svg).Append("<figcaption>").Append(SvgRenderer.Esc(name)).Append("</figcaption></figure>\n");
            }
            File.WriteAllText(Path.Combine(dir, "index.html"), html.Append("</body></html>\n").ToString());
            Assert.True(File.Exists(Path.Combine(dir, "chart.timing.bus.svg")));
        }
    }
}
