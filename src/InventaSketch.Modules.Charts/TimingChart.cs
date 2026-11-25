using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Charts
{
    /// <summary>Part keys of the timing diagram that are not shared with the other charts.</summary>
    public static class TimingTags
    {
        /// <summary>Wave of a signal with its value boxes and value texts ("signal.0").</summary>
        public const string Signal = "signal";
        /// <summary>Name of a signal at the left ("name.0").</summary>
        public const string Name = "name";
        /// <summary>Dimension arrow between two markers with its text ("dim.0").</summary>
        public const string Dimension = "dim";
    }

    /// <summary>
    /// Digital timing diagram: several signal rows on one shared time axis, each with its name at the left and a wave
    /// (logic high/low, clock, bus values in hexagon-shaped boxes, undefined hatched, high impedance), sharp or sloped
    /// edges, optional grid lines at each time step, a time axis with labels, vertical markers and dimension arrows
    /// between markers. The signals are a compact text parameter (<see cref="TimingNotation"/>). Every signal, name,
    /// marker and dimension arrow is a part of its own (charts v2), so one signal can be recoloured natively. Drawn with
    /// native primitives only: one freeform per wave, one closed freeform per value box, lines and texts.
    /// </summary>
    internal sealed class TimingChart : ChartType
    {
        public const string TypeId = "chart.timing";

        public const string DefaultSignals = "CLK: clock\nEN: 00111100\nDATA: xx[A].[B].xx";

        public static readonly ParamDef SignalsDef = Prm.Text("signals", "Signals (one per row, name: wave)", "Signale (eine Zeile je Signal, Name: Verlauf)", DefaultSignals);
        public static readonly ParamDef SlopeDef = Prm.Choice("slope", "Edges", "Flanken", "sloped",
            ("sloped", "Sloped", "Schräg"), ("sharp", "Sharp (vertical)", "Senkrecht"));
        public static readonly ParamDef TimeLabelsDef = Prm.Bool("timelabels", "Time axis with labels", "Zeitachse mit Beschriftung", true);
        public static readonly ParamDef DtDef = Prm.Num("dt", "Time per step", "Zeit je Schritt", 1, 1e-9, 1e9, advanced: true);
        public static readonly ParamDef UnitDef = Prm.Text("tunit", "Time unit (e.g. ns)", "Zeiteinheit (z. B. ns)", advanced: true);
        public static readonly ParamDef MarkersDef = Prm.Text("markers", "Markers (label=time; …, e.g. t1=2; t2=5)", "Markierungen (Name=Zeit; …, z. B. t1=2; t2=5)");
        public static readonly ParamDef SpansDef = Prm.Text("spans", "Dimension arrows between markers (e.g. t1-t2 or t1-t2: t_su)", "Maßpfeile zwischen Markierungen (z. B. t1-t2 oder t1-t2: t_su)");

        private const string WaveColor = "#1f4e9a";
        private const string BusFill = "#e6edf7";
        private const string MarkerColor = "#b03a2e";
        private const double WaveWidth = 0.4;

        public TimingChart() : base(TypeId, new I18nText("Timing diagram (digital)", "Zeitdiagramm (digital)"), FigureChart.CategoryId,
            "timing diagram", "timing", "digital", "signal", "waveform", "clock", "bus", "logic", "handshake", "protocol", "wave",
            "zeitdiagramm", "impulsdiagramm", "signalverlauf", "takt", "taktsignal", "digital", "logik", "bus", "protokoll", "zeitablauf")
        { }

        public override Size2 Design => new(100, 50);

        protected override IReadOnlyList<ParamDef> BuildParams() => new[]
        {
            SignalsDef, SlopeDef, Common.Grid, TimeLabelsDef, MarkersDef, SpansDef, Common.Title, DtDef, UnitDef,
        };

        internal static string SignalsText(SketchObject o) => o.Str("signals", DefaultSignals);

        /// <summary>
        /// Timing diagrams have no data axes: the matplotlib and pgfplots exports report them as not exportable (the TikZ
        /// and SVG exports draw them from the primitives).
        /// </summary>
        internal override Chart Compose(SketchObject o, RenderContext ctx)
            => throw new InvalidOperationException(ctx.Lang == Lang.De
                ? "Zeitdiagramme lassen sich nicht als matplotlib- oder pgfplots-Code ausgeben; bitte als TikZ oder SVG exportieren"
                : "Timing diagrams cannot be exported as matplotlib or pgfplots code; export them as TikZ or SVG instead");

        // ------------------------------------------------------------------ layout and drawing

        /// <summary>The drawn diagram: primitives, anchors and what they were made from.</summary>
        private sealed class Drawing
        {
            public List<Primitive> Prims { get; } = new();
            public List<Anchor> Anchors { get; } = new();
            public TimingSet Set { get; set; } = new();
            public List<TimingMarker> Markers { get; set; } = new();
            public List<TimingSpan> Spans { get; set; } = new();
        }

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx) => Paint(o, ctx).Prims;

        public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx) => Paint(o, ctx).Anchors;

        private static double TextW(string text, double size) => Fmt.Width(text, size);

        /// <summary>Greedy rows for the dimension arrows so overlapping arrows never share a row.</summary>
        private static int[] SpanRows(IReadOnlyList<(double A, double B)> spans, out int rows)
        {
            var result = new int[spans.Count];
            var ends = new List<double>();
            for (var i = 0; i < spans.Count; i++)
            {
                var (a, b) = spans[i];
                var r = ends.FindIndex(e => e < a - 1e-9);
                if (r < 0) { ends.Add(b); r = ends.Count - 1; }
                else ends[r] = b;
                result[i] = r;
            }
            rows = ends.Count;
            return result;
        }

        private Drawing Paint(SketchObject o, RenderContext ctx)
        {
            try { return Build(o, ctx); }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IndexOutOfRangeException or NullReferenceException or FormatException or OverflowException)
            {
                // Rendering must never throw (hosts render on every redraw).
                var d = new Drawing();
                var fs = Geo.Clamp(2.8 * Math.Min(o.Size.W / Design.W, o.Size.H / Design.H), 0.5, 8);
                d.Prims.Add(new TextPrim(new Vec2(0, 0), "?", new TextStyle { Size = fs, Color = Palette.Helper }) { Tag = ChartTags.Error });
                return d;
            }
        }

        private Drawing Build(SketchObject o, RenderContext ctx)
        {
            var lang = ctx.Lang;
            var mono = Mono(ctx);
            var d = new Drawing { Set = TimingNotation.Parse(SignalsText(o)) };
            var set = d.Set;
            var steps = set.Steps;
            var dt = o.Num("dt", 1);
            if (!(dt > 0) || !Fmt.Finite(dt)) dt = 1;
            var unit = o.Str("tunit", "").Trim();
            d.Markers = TimingNotation.Markers(o.Str("markers", ""));
            d.Spans = TimingNotation.Spans(o.Str("spans", ""), d.Markers);
            var axis = o.Flag("timelabels", true);
            var grid = o.Flag("grid", true);
            var sloped = o.Str("slope", "sloped") != "sharp";
            var title = Common.TitleOf(o);

            // Markers inside the time range; arrows between two of them.
            bool Inside(TimingMarker m) => m.Time / dt >= -1e-9 && m.Time / dt <= steps + 1e-9;
            var shownMarkers = Enumerable.Range(0, d.Markers.Count).Where(i => Inside(d.Markers[i])).ToList();
            var shownSpans = Enumerable.Range(0, d.Spans.Count).Where(i => Inside(d.Markers[d.Spans[i].From]) && Inside(d.Markers[d.Spans[i].To])).ToList();
            var spanRanges = shownSpans.Select(i =>
            {
                var a = d.Markers[d.Spans[i].From].Time / dt; var b = d.Markers[d.Spans[i].To].Time / dt;
                return (Math.Min(a, b), Math.Max(a, b));
            }).ToList();
            var spanRow = SpanRows(spanRanges, out var dimRows);

            // Vertical budget in multiples of the font size; the rows take the rest.
            double W = Math.Max(o.Size.W, 1), H = Math.Max(o.Size.H, 1);
            var k = Math.Min(W / Design.W, H / Design.H);
            var fs = Geo.Clamp(2.8 * k, 0.8, 8);
            var n = Math.Max(1, set.Signals.Count);
            const double TitleRow = 1.5, MarkerRow = 1.4, DimRow = 1.8, AxisRow = 2.0, Pads = 0.8;
            var fixedRows = Pads + (title is null ? 0 : TitleRow) + (shownMarkers.Count > 0 ? MarkerRow : 0) + dimRows * DimRow + (axis ? AxisRow : 0);
            fs = Math.Min(fs, H / (fixedRows + 1.7 * n));
            fs = Math.Min(fs, W / 12);
            var pad = 0.4 * fs;
            var rowH = (H - fixedRows * fs) / n;
            var amp = Math.Min(rowH * 0.3, fs * 1.6);

            // Horizontal: names at the left, the time axis arrow and title at the right.
            var nameFs = Math.Min(fs, rowH * 0.6);
            var maxName = set.Signals.Select(s => s.Name.Length).DefaultIfEmpty(0).Max();
            var gap = Math.Min(0.8 * fs, 0.05 * W);
            var nameW = maxName == 0 ? 0 : TextW(new string('M', maxName), nameFs) + gap;
            if (nameW > 0.35 * W)
            {
                nameFs = Math.Max(0.05, (0.35 * W - gap) / (maxName * Fmt.CharWidth));
                nameW = 0.35 * W;
            }
            var labelFs = fs * 0.85;
            var axisTitle = AxisTitle(unit, lang);
            var axisFs = fs * 0.9;
            var arrowLen = 1.2 * fs;
            var lastLabel = Fmt.Num(steps * dt, lang);
            double rightMargin = 0;
            if (axis)
            {
                var avail = 0.3 * W - arrowLen - 0.3 * fs;
                if (TextW(axisTitle, axisFs) > avail) axisFs = Math.Max(0.05, avail / (Math.Max(1, axisTitle.Length) * Fmt.CharWidth));
                rightMargin = Math.Max(arrowLen + 0.3 * fs + TextW(axisTitle, axisFs), TextW(lastLabel, labelFs) / 2);
            }
            var x0 = -W / 2 + pad + nameW;
            var x1 = W / 2 - pad - rightMargin;
            if (x1 - x0 < W * 0.2) x1 = x0 + W * 0.2;
            var dx = (x1 - x0) / steps;
            double X(double t) => x0 + t * dx;

            // Rows from the top.
            var y = -H / 2 + pad;
            var list = d.Prims;
            var titleY = y + 0.7 * fs;
            if (title is not null) y += TitleRow * fs;
            var markerY = y + 0.7 * fs;
            if (shownMarkers.Count > 0) y += MarkerRow * fs;
            var rowsTop = y;
            var rowsBottom = y + n * rowH;
            y = rowsBottom;
            var dimTop = y;
            y += dimRows * DimRow * fs;
            var axisY = y + 0.3 * fs;

            // Grid lines at the time steps (thinned when the steps are narrow).
            var every = Every(steps, dx, fs, Enumerable.Range(0, steps + 1).Select(i => Fmt.Num(i * dt, lang)).Max(s => TextW(s, labelFs)));
            if (grid && set.Signals.Count > 0)
            {
                var lines = new List<IReadOnlyList<Vec2>>();
                for (var i = 0; i <= steps; i += Math.Max(1, (int)Math.Ceiling(0.6 / Math.Max(dx, 1e-9))))
                    lines.Add(new[] { new Vec2(X(i), rowsTop), new Vec2(X(i), rowsBottom) });
                var gs = new Style { Stroke = Palette.Grid, StrokeWidth = 0.13, Role = LineRole.Thin, Dash = DashStyle.Dot };
                if (Draw.MultiPath(lines, gs, ChartTags.Grid) is { } gp) list.Add(gp);
            }

            // Waves, names and their ports.
            var ports = 0;
            var color = mono ? Palette.Black : WaveColor;
            for (var s = 0; s < set.Signals.Count; s++)
            {
                var sig = set.Signals[s];
                var ci = rowsTop + (s + 0.5) * rowH;
                DrawWave(d, sig, s, X, ci, amp, dx, sloped, fs, color, mono, ref ports);
                if (sig.Name.Length > 0)
                    list.Add(new TextPrim(new Vec2(x0 - gap, ci), sig.Name, new TextStyle { Size = nameFs, Anchor = TextAnchor.End, Color = Palette.Ink }) { Tag = ChartTags.Nth(TimingTags.Name, s) });
            }

            // Markers: dashed vertical lines through all rows (down to the dimension arrows), label on top.
            var mcolor = mono ? Palette.Black : MarkerColor;
            var lowest = dimRows > 0 ? dimTop + (dimRows - 1) * DimRow * fs + 1.4 * fs + 0.25 * fs : rowsBottom;
            var labelRight = double.NegativeInfinity;
            foreach (var i in shownMarkers.OrderBy(i => d.Markers[i].Time))
            {
                var m = d.Markers[i];
                var xm = X(m.Time / dt);
                var tag = ChartTags.Nth(ChartTags.Ref, i);
                var top = rowsTop - 0.15 * fs;
                list.Add(Prim.Line(new Vec2(xm, top), new Vec2(xm, lowest), new Style { Stroke = mcolor, StrokeWidth = 0.25, Role = LineRole.Thin, Dash = DashStyle.Dash }) with { Tag = tag });
                if (m.Label.Length > 0)
                {
                    var mfs = Math.Min(fs * 0.9, (W - 2 * pad) / (m.Label.Length * Fmt.CharWidth));
                    var tw = TextW(m.Label, mfs);
                    // Labels of close markers move right instead of running into each other.
                    var cx = ClampText(Math.Max(xm, labelRight + 0.4 * fs + tw / 2), tw, W, pad);
                    labelRight = cx + tw / 2;
                    list.Add(new TextPrim(new Vec2(cx, markerY), m.Label, new TextStyle { Size = mfs, Color = mcolor }) { Tag = tag });
                }
            }

            // Dimension arrows between two markers.
            for (var j = 0; j < shownSpans.Count; j++)
            {
                var sp = d.Spans[shownSpans[j]];
                var tag = ChartTags.Nth(TimingTags.Dimension, shownSpans[j]);
                var (a, b) = spanRanges[j];
                var rt = dimTop + spanRow[j] * DimRow * fs;
                var ly = rt + 1.4 * fs;
                list.Add(Prim.Line(new Vec2(X(a), ly), new Vec2(X(b), ly), new Style
                {
                    Stroke = Palette.Ink, StrokeWidth = 0.25, Role = LineRole.Thin,
                    ArrowStart = ArrowHead.Triangle, ArrowEnd = ArrowHead.Triangle, ArrowSize = ArrowSize.Small,
                }) with { Tag = tag });
                var dfs = Math.Min(fs * 0.9, (W - 2 * pad) / (Math.Max(1, sp.Text.Length) * Fmt.CharWidth));
                var cx = ClampText((X(a) + X(b)) / 2, TextW(sp.Text, dfs), W, pad);
                list.Add(new TextPrim(new Vec2(cx, rt + 0.65 * fs), sp.Text, new TextStyle { Size = dfs, Color = Palette.Ink }) { Tag = tag });
            }

            // Time axis with ticks, labels and title.
            var railY = rowsBottom;
            if (axis)
            {
                railY = axisY;
                list.Add(Prim.Line(new Vec2(x0, axisY), new Vec2(x1 + arrowLen, axisY), new Style
                {
                    Stroke = Palette.Axis, StrokeWidth = 0.35, Role = LineRole.Visible, ArrowEnd = ArrowHead.Triangle, ArrowSize = ArrowSize.Small,
                }) with { Tag = "axis.x" });
                var ticks = new List<IReadOnlyList<Vec2>>();
                for (var i = 0; i <= steps; i += every)
                {
                    ticks.Add(new[] { new Vec2(X(i), axisY), new Vec2(X(i), axisY + 0.35 * fs) });
                    list.Add(new TextPrim(new Vec2(X(i), axisY + 1.1 * fs), Fmt.Num(i * dt, lang), new TextStyle { Size = labelFs, Color = Palette.Ink }) { Tag = "label.x" });
                }
                if (Draw.MultiPath(ticks, S.Tick, "tick.x") is { } tp) list.Add(tp);
                list.Add(new TextPrim(new Vec2(x1 + arrowLen + 0.3 * fs, axisY), axisTitle, new TextStyle { Size = axisFs, Anchor = TextAnchor.Start, Italic = unit.Length == 0, Color = Palette.Ink }) { Tag = "axistitle.x" });
            }

            if (title is not null)
            {
                var tfs = Math.Min(fs * 1.1, (W - 2 * pad) / (title.Length * Fmt.CharWidth));
                list.Add(new TextPrim(new Vec2(0, titleY), title, new TextStyle { Size = tfs, Bold = true, Color = Palette.Ink }) { Tag = ChartTags.Title });
            }

            // Anchors: the wave area, the origin of the time axis, the axis as a rail; the edges are ports (added by DrawWave).
            d.Anchors.InsertRange(0, new[]
            {
                Anchor.Point(AnchorRole.Corner, new Vec2(x0, rowsTop), "plot.tl"),
                Anchor.Point(AnchorRole.Corner, new Vec2(x1, rowsTop), "plot.tr"),
                Anchor.Point(AnchorRole.Corner, new Vec2(x1, rowsBottom), "plot.br"),
                Anchor.Point(AnchorRole.Corner, new Vec2(x0, rowsBottom), "plot.bl"),
                Anchor.Point(AnchorRole.Center, new Vec2((x0 + x1) / 2, (rowsTop + rowsBottom) / 2), "plot.c"),
                Anchor.Point(AnchorRole.Port, new Vec2(x0, railY), "origin"),
                Anchor.Rail(new Vec2(x0, railY), new Vec2(x1, railY), "axis.x"),
            });
            return d;
        }

        /// <summary>"t", "t in ns" (German) or "t (ns)".</summary>
        private static string AxisTitle(string unit, Lang lang) => unit.Length == 0 ? "t" : lang == Lang.De ? "t in " + unit : "t (" + unit + ")";

        /// <summary>Label step: 1, 2, 5, 10 … time steps so the labels do not overlap.</summary>
        private static int Every(int steps, double dx, double fs, double labelW)
        {
            var need = (labelW + 0.5 * fs) / Math.Max(dx, 1e-9);
            foreach (var e in new[] { 1, 2, 5, 10, 20, 50, 100, 200, 500 })
                if (e >= need) return e;
            return Math.Max(1, steps);
        }

        /// <summary>Centre x of a text kept inside the box.</summary>
        private static double ClampText(double x, double w, double boxW, double pad)
        {
            var lo = -boxW / 2 + pad + w / 2;
            var hi = boxW / 2 - pad - w / 2;
            return lo > hi ? 0 : Geo.Clamp(x, lo, hi);
        }

        /// <summary>
        /// One wave: an open freeform with the upper level line and the lower level line where it differs (bus and
        /// undefined states, crossing edges), below it one closed freeform per value box (filled) or undefined stretch
        /// (hatched), and the value texts. All tagged "signal.k".
        /// </summary>
        private static void DrawWave(Drawing d, TimingSignal sig, int k, Func<double, double> X, double ci, double a, double dx, bool sloped, double fs,
            string color, bool mono, ref int ports)
        {
            var segs = sig.Segments;
            if (segs.Count == 0) return;
            var tag = ChartTags.Nth(TimingTags.Signal, k);
            double top = ci - a, bot = ci + a;
            (double U, double L) Rails(WaveSegment g) => g.Kind switch
            {
                WaveKind.Low => (bot, bot),
                WaveKind.High => (top, top),
                WaveKind.HighZ => (ci, ci),
                _ => (top, bot),
            };
            var slope = sloped ? Math.Min(dx * 0.16, a * 0.7) : 0;
            var busSlope = Math.Min(dx * 0.14, a * 0.7);
            var h = new double[segs.Count + 1];
            for (var j = 1; j < segs.Count; j++)
            {
                var shorter = dx * Math.Min(segs[j - 1].Length, segs[j].Length);
                var w = segs[j - 1].TwoRail || segs[j].TwoRail ? Math.Max(slope, busSlope) : Math.Min(slope, 0.25 * shorter);
                h[j] = Math.Min(w / 2, 0.45 * shorter);
            }

            // Both level lines on shared x positions. Between two value boxes the lines cross: the line that ran along the
            // top continues along the bottom, so its rails swap ("flip").
            var xs = new List<double>(); var up = new List<double>(); var lo = new List<double>();
            void P(double x, (double U, double L) r, bool flip) { xs.Add(x); up.Add(flip ? r.L : r.U); lo.Add(flip ? r.U : r.L); }
            var flipped = false;
            P(X(segs[0].Start), Rails(segs[0]), flipped);
            for (var j = 1; j < segs.Count; j++)
            {
                var t = X(segs[j].Start);
                P(t - h[j], Rails(segs[j - 1]), flipped);
                if (segs[j - 1].TwoRail && segs[j].TwoRail) flipped = !flipped;
                P(t + h[j], Rails(segs[j]), flipped);
            }
            P(X(segs[segs.Count - 1].End), Rails(segs[segs.Count - 1]), flipped);

            static bool Same(double p, double q) => Math.Abs(p - q) < 1e-9;
            var path = new List<PathSeg>();
            void Piece(List<Vec2> pts)
            {
                var clean = new List<Vec2>();
                foreach (var p in pts) if (clean.Count == 0 || clean[clean.Count - 1].DistanceTo(p) > 1e-9) clean.Add(p);
                if (clean.Count < 2) return;
                path.Add(PathSeg.Move(clean[0]));
                for (var i = 1; i < clean.Count; i++) path.Add(PathSeg.Line(clean[i]));
            }
            Piece(xs.Select((x, i) => new Vec2(x, up[i])).ToList());
            List<Vec2>? piece = null;
            for (var i = 0; i < xs.Count - 1; i++)
            {
                if (Same(lo[i], up[i]) && Same(lo[i + 1], up[i + 1]))
                {
                    if (piece is not null) { Piece(piece); piece = null; }
                    continue;
                }
                piece ??= new List<Vec2> { new(xs[i], lo[i]) };
                piece.Add(new Vec2(xs[i + 1], lo[i + 1]));
            }
            if (piece is not null) Piece(piece);

            // Value boxes and undefined stretches (below the level lines).
            var hatch = Math.Max(0.4, Math.Min(2.0, a * 0.5));
            for (var j = 0; j < segs.Count; j++)
            {
                var g = segs[j];
                if (!g.TwoRail) continue;
                double xs0 = X(g.Start), xe0 = X(g.End);
                double hl = j > 0 ? h[j] : 0, hr = j < segs.Count - 1 ? h[j + 1] : 0;
                var poly = new List<Vec2>();
                Vec2? tipL = j == 0 ? null : segs[j - 1].TwoRail ? new Vec2(xs0, ci) : new Vec2(xs0 - hl, Rails(segs[j - 1]).U);
                Vec2? tipR = j == segs.Count - 1 ? null : segs[j + 1].TwoRail ? new Vec2(xe0, ci) : new Vec2(xe0 + hr, Rails(segs[j + 1]).U);
                poly.Add(tipL ?? new Vec2(xs0, top));
                poly.Add(new Vec2(xs0 + hl, top));
                poly.Add(new Vec2(xe0 - hr, top));
                poly.Add(tipR ?? new Vec2(xe0, top));
                if (tipR is null) poly.Add(new Vec2(xe0, bot));
                poly.Add(new Vec2(xe0 - hr, bot));
                poly.Add(new Vec2(xs0 + hl, bot));
                if (tipL is null) poly.Add(new Vec2(xs0, bot));
                var clean = new List<Vec2>();
                foreach (var p in poly) if (clean.Count == 0 || clean[clean.Count - 1].DistanceTo(p) > 1e-9) clean.Add(p);
                if (clean.Count > 2 && clean[0].DistanceTo(clean[clean.Count - 1]) < 1e-9) clean.RemoveAt(clean.Count - 1);
                if (clean.Count < 3) continue;
                var fill = g.Kind == WaveKind.Bus
                    ? Fill.Solid(mono ? Palette.White : BusFill)
                    : Fill.Hatched(new HatchPattern(45, hatch, Color: mono ? Palette.Black : Palette.Helper, Width: 0.1), Palette.White);
                d.Prims.Add(Prim.Polygon(clean, new Style { Stroke = "none", Role = LineRole.Visible, Fill = fill }) with { Tag = tag });
            }
            if (path.Count > 0) d.Prims.Add(new PathPrim(path, new Style { Stroke = color, StrokeWidth = WaveWidth, Role = LineRole.Visible }) with { Tag = tag });

            // Value texts inside their boxes (left out where a box is too narrow to read).
            for (var j = 0; j < segs.Count; j++)
            {
                var g = segs[j];
                if (g.Kind != WaveKind.Bus || string.IsNullOrEmpty(g.Value)) continue;
                double hl = j > 0 ? h[j] : 0, hr = j < segs.Count - 1 ? h[j + 1] : 0;
                var room = X(g.End) - X(g.Start) - hl - hr - 0.3 * fs;
                var vfs = Math.Min(Math.Min(fs * 0.85, 2 * a * 0.7), room / (g.Value!.Length * Fmt.CharWidth));
                if (vfs < Math.Max(0.3, fs * 0.3)) continue;
                d.Prims.Add(new TextPrim(new Vec2((X(g.Start) + X(g.End)) / 2, ci), g.Value, new TextStyle { Size = vfs, Color = Palette.Ink }) { Tag = tag });
            }

            // Every edge is a docking port (cause-and-effect arrows between signals).
            for (var j = 1; j < segs.Count && ports < 160; j++, ports++)
            {
                var (u0, l0) = Rails(segs[j - 1]);
                var (u1, l1) = Rails(segs[j]);
                d.Anchors.Add(Anchor.Point(AnchorRole.Port, new Vec2(X(segs[j].Start), (u0 + l0 + u1 + l1) / 4), "s" + Fmt.I(k + 1) + "." + Fmt.I(j)));
            }
        }

        // ------------------------------------------------------------------ parts

        private static string Kind(string key) => key.IndexOf('.') is var i and > 0 ? key.Substring(0, i) : key;

        private static int Order(string kind) => kind switch
        {
            TimingTags.Signal => 0, TimingTags.Name => 1, ChartTags.Ref => 2, TimingTags.Dimension => 3, "grid" => 4,
            ChartTags.Axis => 5, ChartTags.Tick => 6, ChartTags.TickLabel => 7, ChartTags.AxisTitle => 8, ChartTags.Title => 9, _ => 10,
        };

        private static int Index(string key)
            => key.Split('.') is { Length: 2 } seg && int.TryParse(seg[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : -1;

        /// <summary>Signals, names, markers, dimension arrows, grid, time axis and title as parts.</summary>
        public override IEnumerable<PartInfo> Parts(SketchObject o, RenderContext ctx)
        {
            var d = Paint(o, ctx);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var list = new List<PartInfo>();
            foreach (var p in Prim.Flatten(d.Prims))
                if (p.Tag is { Length: > 0 } tag && tag != ChartTags.Error && seen.Add(tag))
                    list.Add(new PartInfo(tag, Label(tag, d), Kind(tag)));
            return list.OrderBy(p => Order(p.Kind)).ToList();
        }

        private static I18nText Label(string key, Drawing d)
        {
            var i = Index(key);
            string SignalName(int s) => s >= 0 && s < d.Set.Signals.Count && d.Set.Signals[s].Name.Length > 0 ? d.Set.Signals[s].Name : Fmt.I(s + 1);
            switch (Kind(key))
            {
                case TimingTags.Signal: return new I18nText("Signal " + SignalName(i), "Signal " + SignalName(i));
                case TimingTags.Name: return new I18nText("Name – " + SignalName(i), "Name – " + SignalName(i));
                case ChartTags.Ref:
                    {
                        var label = i >= 0 && i < d.Markers.Count ? d.Markers[i].Label : Fmt.I(i + 1);
                        return new I18nText("Marker " + label, "Markierung " + label);
                    }
                case TimingTags.Dimension:
                    {
                        var text = i >= 0 && i < d.Spans.Count ? d.Spans[i].Text : Fmt.I(i + 1);
                        return new I18nText("Dimension arrow " + text, "Maßpfeil " + text);
                    }
                case ChartTags.Axis: return new I18nText("Time axis", "Zeitachse");
                case ChartTags.Tick: return new I18nText("Time axis ticks", "Teilstriche Zeitachse");
                case ChartTags.TickLabel: return new I18nText("Time labels", "Zeitbeschriftung");
                case ChartTags.AxisTitle: return new I18nText("Time axis title", "Titel Zeitachse");
                case "grid": return new I18nText("Grid lines at the time steps", "Gitterlinien an den Zeitschritten");
                default: return PartLabel(key, Array.Empty<string>(), Array.Empty<string>(), Lang.En);
            }
        }

        // ------------------------------------------------------------------ actions

        private static readonly I18nText SignalsPrompt = new(
            "One signal per row “name: wave” (0 1 x z = [value] p n . or “clock 2 25%”); rows separated by |",
            "Ein Signal je Zeile „Name: Verlauf“ (0 1 x z = [Wert] p n . oder „clock 2 25%“); Zeilen getrennt durch |");

        /// <summary>The signals as rows: "Signals…" (id "chart.data", so hosts with a table editor use it, one row per signal).</summary>
        private static ContextAction SignalsInput(SketchObject o, Lang lang)
        {
            var current = SignalsText(o).Replace("\r\n", "\n").Replace('\r', '\n');
            var rows = current.Split('\n').Select(l => l.Trim()).ToList();
            while (rows.Count > 0 && rows[rows.Count - 1].Length == 0) rows.RemoveAt(rows.Count - 1);
            return ContextAction.WithInput("chart.data", new I18nText("Signals…", "Signale…"), IconIds.Text, SignalsPrompt, string.Join(" | ", rows),
                (x, text) => x.WithParam("signals", CheckSignals(text, lang))) with { Group = "data" };
        }

        /// <summary>Rows separated by '|' or line breaks as the stored notation; throws a bilingual <see cref="FormatException"/> for errors.</summary>
        internal static string CheckSignals(string? text, Lang lang)
        {
            var notation = string.Join("\n", (text ?? "").Replace("\r", "").Split('|', '\n').Select(l => l.Trim()));
            notation = notation.Trim('\n');
            var set = TimingNotation.Parse(notation);
            if (set.Signals.Count == 0) throw new FormatException(lang == Lang.De ? "Keine Signale angegeben" : "No signals given");
            if (set.Errors.Count > 0) throw new FormatException(set.Errors[0].Text(lang));
            return notation;
        }

        private static ContextAction MarkersInput(SketchObject o, Lang lang)
            => ContextAction.WithInput("chart.timing.markers", new I18nText("Markers…", "Markierungen…"), IconIds.Text,
                new I18nText("Vertical markers “label=time”, separated by ; (e.g. t1=2; t2=5)", "Senkrechte Markierungen „Name=Zeit“, getrennt durch ; (z. B. t1=2; t2=5)"),
                o.Str("markers", ""), (x, text) =>
                {
                    var errors = new List<TimingError>();
                    TimingNotation.Markers(text, errors);
                    if (errors.Count > 0) throw new FormatException(errors[0].Message.Get(lang));
                    return x.WithParam("markers", (text ?? "").Trim());
                }) with { Group = "timing" };

        private static ContextAction SpansInput(SketchObject o, Lang lang)
            => ContextAction.WithInput("chart.timing.spans", new I18nText("Dimension arrows…", "Maßpfeile…"), IconIds.Text,
                new I18nText("Arrows between markers, separated by ; (e.g. t1-t2 or t1-t2: t_su)", "Pfeile zwischen Markierungen, getrennt durch ; (z. B. t1-t2 oder t1-t2: t_su)"),
                o.Str("spans", ""), (x, text) =>
                {
                    var errors = new List<TimingError>();
                    TimingNotation.Spans(text, TimingNotation.Markers(x.Str("markers", "")), errors);
                    if (errors.Count > 0) throw new FormatException(errors[0].Message.Get(lang));
                    return x.WithParam("spans", (text ?? "").Trim());
                }) with { IsEnabled = TimingNotation.Markers(o.Str("markers", "")).Count >= 2, Group = "timing" };

        /// <summary>Appends a signal "S4: 0…1…" as long as the diagram (low for the first half, then high).</summary>
        internal static SketchObject AddSignal(SketchObject x)
        {
            var text = SignalsText(x).TrimEnd();
            var set = TimingNotation.Parse(text);
            var names = new HashSet<string>(set.Signals.Select(s => s.Name), StringComparer.OrdinalIgnoreCase);
            var i = set.Signals.Count + 1;
            while (names.Contains("S" + Fmt.I(i))) i++;
            var steps = set.Steps;
            var half = Math.Max(1, steps / 2);
            var wave = steps < 2 ? "1" : "0" + new string('.', half - 1) + "1" + new string('.', Math.Max(0, steps - half - 1));
            return x.WithParam("signals", (text.Length > 0 ? text + "\n" : "") + "S" + Fmt.I(i) + ": " + wave);
        }

        /// <summary>Removes signal <paramref name="index"/> (-1: the last one); the last remaining signal stays.</summary>
        internal static SketchObject RemoveSignal(SketchObject x, int index)
        {
            var text = SignalsText(x);
            var rows = TimingNotation.Lines(text).ToList();
            if (rows.Count < 2) return x;
            var r = index < 0 || index >= rows.Count ? rows[rows.Count - 1] : rows[index];
            var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n').ToList();
            lines.RemoveAt(r.Line - 1);
            return x.WithParam("signals", string.Join("\n", lines).Trim('\n'));
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            var count = TimingNotation.Parse(SignalsText(o)).Signals.Count;
            return new[]
            {
                SignalsInput(o, lang),
                new ContextAction("chart.timing.add", new I18nText("Add signal", "Signal hinzufügen"), IconIds.Insert, AddSignal) { Group = "timing" },
                new ContextAction("chart.timing.remove", new I18nText("Remove last signal", "Letztes Signal entfernen"), IconIds.Delete, x => RemoveSignal(x, -1))
                    { Group = "timing", IsEnabled = count > 1 },
                Common.GridToggle(o),
                Prm.Toggle(o, "chart.timing.labels", "timelabels", true, new I18nText("Time labels", "Zeitbeschriftung")),
                Prm.Cycle(o, "chart.timing.slope", SlopeDef, new I18nText("Edges", "Flanken")),
                MarkersInput(o, lang),
                SpansInput(o, lang),
            };
        }

        /// <summary>
        /// Part actions: a signal or its name (signals, rename, remove this signal, edges), a marker (markers, remove it,
        /// dimension arrows), a dimension arrow (edit, remove it), the grid (toggle) and the time axis (time labels).
        /// </summary>
        protected override IEnumerable<ContextAction> OwnPartActions(SketchObject o, string part, RenderContext ctx)
        {
            var lang = ctx.Lang;
            var i = Index(part);
            var all = Actions(o, lang).ToList();
            ContextAction? A(string id) => all.FirstOrDefault(a => a.Id == id);
            var list = new List<ContextAction?>();
            switch (Kind(part))
            {
                case TimingTags.Signal or TimingTags.Name when i >= 0:
                    {
                        var set = TimingNotation.Parse(SignalsText(o));
                        var name = i < set.Signals.Count ? set.Signals[i].Name : "";
                        list.Add(A("chart.data"));
                        list.Add(ContextAction.WithInput("chart.timing.rename", new I18nText("Name…", "Name…"), IconIds.Text, new I18nText("Signal name", "Signalname"), name,
                            (x, text) => x.WithParam("signals", TimingNotation.Rewrite(SignalsText(x), (s, old) => s == i ? TimingNotation.CleanName(text) : old, null))) with { Group = "timing" });
                        list.Add(new ContextAction("chart.timing.removeAt", new I18nText("Remove this signal", "Dieses Signal entfernen"), IconIds.Delete, x => RemoveSignal(x, i))
                            { Group = "timing", IsEnabled = set.Signals.Count > 1 });
                        list.Add(A("chart.timing.slope"));
                        break;
                    }
                case ChartTags.Ref when i >= 0:
                    list.Add(A("chart.timing.markers"));
                    list.Add(new ContextAction("chart.timing.marker.remove", new I18nText("Remove this marker", "Diese Markierung entfernen"), IconIds.Delete, x =>
                    {
                        var ms = TimingNotation.Markers(x.Str("markers", ""));
                        if (i >= ms.Count) return x;
                        ms.RemoveAt(i);
                        return x.WithParam("markers", TimingNotation.FormatMarkers(ms));
                    }) { Group = "timing" });
                    list.Add(A("chart.timing.spans"));
                    break;
                case TimingTags.Dimension when i >= 0:
                    list.Add(A("chart.timing.spans"));
                    list.Add(new ContextAction("chart.timing.span.remove", new I18nText("Remove this dimension arrow", "Diesen Maßpfeil entfernen"), IconIds.Delete, x =>
                    {
                        var entries = x.Str("spans", "").Split(new[] { ';', '\n', '\r', '|' }, StringSplitOptions.RemoveEmptyEntries).Select(e => e.Trim()).Where(e => e.Length > 0).ToList();
                        if (i >= entries.Count) return x;
                        entries.RemoveAt(i);
                        return x.WithParam("spans", string.Join("; ", entries));
                    }) { Group = "timing" });
                    break;
                case "grid":
                    list.Add(A("chart.grid"));
                    break;
                case ChartTags.Axis or ChartTags.Tick or ChartTags.TickLabel or ChartTags.AxisTitle:
                    list.Add(A("chart.timing.labels"));
                    break;
            }
            return list.Where(a => a is not null).Select(a => a!).ToList();
        }

        // ------------------------------------------------------------------ clean slides for sharing

        /// <summary>
        /// Clean slides for sharing: the generic pass (title, metadata), then signal names → S1, S2 … and marker labels →
        /// t1, t2 … when texts are replaced; data values → D1, D2 … (normalised) or random hexadecimal values (randomised,
        /// equal values stay equal) and the time scale → steps (normalised) or a random factor (randomised) when values are
        /// cleaned. The waves themselves stay.
        /// </summary>
        public override SketchObject Sanitize(SketchObject o, SanitizeOptions opt, Random rng)
        {
            var r = base.Sanitize(o, opt, rng);
            var signals = SignalsText(r);
            var next = signals;
            if (opt.ReplaceTexts)
                next = TimingNotation.Rewrite(next, (k, old) => old.Length == 0 ? old : "S" + Fmt.I(k + 1), null);
            if (opt.Mode != SanitizeMode.None)
            {
                var map = new Dictionary<string, string>(StringComparer.Ordinal);
                var random = opt.Mode == SanitizeMode.Randomised;
                next = TimingNotation.Rewrite(next, null, v =>
                {
                    if (map.TryGetValue(v, out var nv)) return nv;
                    nv = random ? RandomValue(rng, v, map.Values) : "D" + Fmt.I(map.Count + 1);
                    map[v] = nv;
                    return nv;
                });
            }
            if (next != signals) r = r.WithParam("signals", next);

            // Markers and dimension arrows: neutral labels, time scale with the values.
            var markers = TimingNotation.Markers(r.Str("markers", ""));
            var spans = TimingNotation.Spans(r.Str("spans", ""), markers);
            var dt = r.Num("dt", 1);
            if (!(dt > 0) || !Fmt.Finite(dt)) dt = 1;
            var factor = 1.0;
            if (opt.Mode == SanitizeMode.Normalised && (Math.Abs(dt - 1) > 1e-12 || r.Str("tunit", "").Length > 0))
            {
                factor = 1 / dt;
                r = r.WithParam("dt", 1.0).WithParam("tunit", "");
            }
            else if (opt.Mode == SanitizeMode.Randomised && r.Params.ContainsKey("dt"))
            {
                var ndt = SanitizeText.Round(dt * SanitizeText.Away(rng, opt.Variation), 2);
                factor = ndt / dt;
                r = r.WithParam("dt", ndt);
            }
            var relabel = opt.ReplaceTexts && markers.Any(m => !SanitizeText.IsIdentifier(m.Label));
            if (markers.Count > 0 && (relabel || Math.Abs(factor - 1) > 1e-12))
            {
                var renamed = markers.Select((m, i) => new TimingMarker(relabel ? "t" + Fmt.I(i + 1) : m.Label, SanitizeText.Round(m.Time * factor, 6))).ToList();
                r = r.WithParam("markers", TimingNotation.FormatMarkers(renamed));
                if (spans.Count > 0)
                    r = r.WithParam("spans", string.Join("; ", spans.Select(s =>
                    {
                        var text = opt.ReplaceTexts && !SanitizeText.IsIdentifier(s.Text) ? "Δt" : s.Text;
                        return renamed[s.From].Label + "-" + renamed[s.To].Label + (text == "Δt" ? "" : ": " + text);
                    })));
            }
            else if (opt.ReplaceTexts && spans.Any(s => s.Text != "Δt" && !SanitizeText.IsIdentifier(s.Text)))
                r = r.WithParam("spans", string.Join("; ", spans.Select(s =>
                    markers[s.From].Label + "-" + markers[s.To].Label + (s.Text == "Δt" || !SanitizeText.IsIdentifier(s.Text) ? "" : ": " + s.Text))));
            return r;
        }

        /// <summary>A random hexadecimal value as long as <paramref name="v"/> (1 to 4 digits), different from it and from <paramref name="used"/>.</summary>
        private static string RandomValue(Random rng, string v, IEnumerable<string> used)
        {
            var taken = new HashSet<string>(used, StringComparer.Ordinal);
            var len = Math.Max(1, Math.Min(4, v.Length));
            for (var attempt = 0; attempt < 40; attempt++)
            {
                var s = new string(Enumerable.Range(0, len).Select(_ => "0123456789ABCDEF"[rng.Next(16)]).ToArray());
                if (s != v && !taken.Contains(s)) return s;
            }
            return "D" + Fmt.I(taken.Count + 1);
        }
    }
}
