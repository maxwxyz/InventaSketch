using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Charts
{
    /// <summary>A plot panel: its vertical axis (and optional secondary axis on the right) and its area after layout.</summary>
    internal sealed class Panel
    {
        public Panel(Axis y, string? title) { Y = y; YTitle = title; }
        public Axis Y { get; }
        public string? YTitle { get; }
        public Axis? Y2 { get; set; }
        public string? Y2Title { get; set; }
        public Rect Area { get; set; }
        /// <summary>Relative height of the panel among stacked panels (figure charts; default 1 = equal heights).</summary>
        public double Share { get; set; } = 1;
        /// <summary>Ticks drawn instead of the axis ticks (figure charts: named marks on principle axes; empty = no ticks).</summary>
        public IReadOnlyList<Tick>? Ticks { get; set; }
        /// <summary>Arrowhead on this panel's y axis (principle axes) even when the frame has no arrows.</summary>
        public bool Arrow { get; set; }
        /// <summary>Horizontal grid lines of this panel; null = the frame setting.</summary>
        public bool? Grid { get; set; }
        /// <summary>Texts left of the y axis at a value, like tick labels but with their own part tag (numerals at the axis).</summary>
        public List<AxisText> Texts { get; } = new();

        /// <summary>The ticks to draw: the custom ticks if set, else the axis ticks.</summary>
        public IReadOnlyList<Tick> TickList(Lang lang) => Ticks is null ? Y.Ticks(lang) : Ticks.Where(t => Y.Visible(t.Value)).ToList();
    }

    /// <summary>A text at an axis value (drawn like a tick label, tagged with its own part key).</summary>
    internal sealed record AxisText(double Value, string Text, string Tag);

    /// <summary>Legend entry: a line sample (with dash and marker) or an area sample (bars, bands).</summary>
    internal sealed record LegendEntry(string Text, Style Style, MarkerKind? Marker = null, bool FilledMarker = false, bool Area = false);

    /// <summary>
    /// Layout and axis drawing of a chart inside its box (local coordinates centred at 0,0, mm). One shared horizontal axis
    /// and one or more panels stacked vertically (Bode plot: magnitude above phase). Text sizes follow the box scale
    /// (<see cref="Fs"/>); margins are computed from the estimated label widths so everything stays inside the box.
    /// </summary>
    internal sealed class Frame
    {
        public Frame(Size2 size, Size2 design, Axis x, string? xTitle, params Panel[] panels)
        {
            W = Math.Max(size.W, 1); H = Math.Max(size.H, 1);
            K = Math.Min(W / design.W, H / design.H);
            Fs = Geo.Clamp(2.8 * K, 0.8, 8);
            X = x; XTitle = xTitle; Panels = panels;
        }

        public double W { get; }
        public double H { get; }
        /// <summary>Detail scale (box size relative to the design size).</summary>
        public double K { get; }
        /// <summary>Font size of tick labels and titles (mm).</summary>
        public double Fs { get; private set; }
        public Axis X { get; }
        public string? XTitle { get; }
        public IReadOnlyList<Panel> Panels { get; }
        public string? Title { get; set; }
        public bool Grid { get; set; } = true;
        public bool Box { get; set; }
        public bool Arrows { get; set; }
        public bool Mono { get; set; }
        /// <summary>Legend mode: auto (inside when there are two or more entries), inside, right, none.</summary>
        public string LegendMode { get; set; } = "auto";
        public List<LegendEntry> Legend { get; } = new();
        /// <summary>Area of all panels (after <see cref="Layout"/>).</summary>
        public Rect Plot { get; private set; }
        /// <summary>Ticks of the x axis drawn instead of the axis ticks (figure charts: named marks; empty = none).</summary>
        public IReadOnlyList<Tick>? XTicks { get; set; }
        /// <summary>Arrowhead on the x axes (principle axes) even when <see cref="Arrows"/> is off.</summary>
        public bool XArrow { get; set; }
        /// <summary>Vertical grid lines; null = <see cref="Grid"/>.</summary>
        public bool? XGrid { get; set; }
        /// <summary>Dash pattern of the major grid; null = solid in colour, dotted in monochrome.</summary>
        public DashStyle? GridDash { get; set; }
        /// <summary>Gap between stacked panels in multiples of <see cref="Fs"/>; null = 1.2.</summary>
        public double? PanelGap { get; set; }
        /// <summary>Caption below the plot (figure charts).</summary>
        public string? Caption { get; set; }
        /// <summary>Extra space right of the plot in multiples of <see cref="Fs"/> (labels at curve ends).</summary>
        public double ExtraRight { get; set; }
        /// <summary>Texts below the x axis of the bottom panel at an x value (like tick labels, own part tag).</summary>
        public List<AxisText> XTexts { get; } = new();

        /// <summary>The x ticks to draw: the custom ticks if set, else the axis ticks.</summary>
        public IReadOnlyList<Tick> XTickList(Lang lang) => XTicks is null ? X.Ticks(lang) : XTicks.Where(t => X.Visible(t.Value)).ToList();

        /// <summary>
        /// Math style (coordinate systems and function graphs): one panel, the axes cross at the origin (clamped into the
        /// ranges) and end in arrowheads beyond the plot, ticks cross the axis lines, tick numbers sit beside the axes and the
        /// axis names (<see cref="XName"/>, <see cref="YName"/>) at the arrow tips.
        /// </summary>
        public bool MathAxes { get; set; }
        /// <summary>Axis name at the tip of the x axis (math style).</summary>
        public string? XName { get; set; }
        /// <summary>Axis name at the tip of the y axis (math style).</summary>
        public string? YName { get; set; }
        /// <summary>Tick numbers (math style; data charts always label their ticks).</summary>
        public bool Numbers { get; set; } = true;
        /// <summary>Page points a tick number should not cover (math style: the number moves to the other side of its axis).</summary>
        public List<Vec2> NumberAvoid { get; } = new();

        /// <summary>Data value where the other axis crosses (math style): 0 clamped into the range.</summary>
        private static double Cross(Axis a) => a.Kind == AxisKind.Linear ? Geo.Clamp(0, a.Min, a.Max) : a.Min;
        /// <summary>Data x of the y axis.</summary>
        public double OriginX => MathAxes ? Cross(X) : X.Min;
        /// <summary>Data y of the x axis (first panel).</summary>
        public double OriginY => MathAxes && Panels.Count > 0 ? Cross(Panels[0].Y) : Panels.Count > 0 ? Panels[0].Y.Min : 0;
        /// <summary>Page x of the y axis line of a panel.</summary>
        public double AxisX(Panel p) => MathAxes ? p.Area.X + X.T(OriginX) * p.Area.W : p.Area.X;
        /// <summary>Page y of the x axis line of a panel.</summary>
        public double AxisY(Panel p) => MathAxes ? p.Area.Bottom - p.Y.T(Cross(p.Y)) * p.Area.H : p.Area.Bottom;
        /// <summary>Overhang of the math-style axes beyond the plot (arrowheads).</summary>
        private double Over => Math.Max(Fs * 0.9, 1.0);
        /// <summary>Size of the axis names (math style).</summary>
        private double NameFs => Fs * 1.3;
        private double MathTick => 0.25 * Fs;
        private double MathGap => 0.33 * Fs;

        /// <summary>Height of the caption line (0 without caption).</summary>
        private double CaptionH => string.IsNullOrEmpty(Caption) ? 0 : Fs * 1.1 + Gap;

        private double Pad => 0.35 * Fs;
        private double TickLen => 0.45 * Fs;
        private double Gap => 0.3 * Fs;
        private double LegendRow => Fs * 1.3;
        private bool ShowLegend => Legend.Count > 0 && (LegendMode is "inside" or "right" || LegendMode == "auto" && Legend.Count >= 2);

        private static double MaxWidth(Axis a, Lang lang, double fs)
            => a.Ticks(lang).Where(t => t.Label is not null).Select(t => Fmt.Width(t.Label!, fs)).DefaultIfEmpty(0).Max();

        /// <summary>Widest tick label or axis text of a panel (left of its y axis).</summary>
        private static double LabelWidth(Panel p, Lang lang, double fs)
            => p.TickList(lang).Where(t => t.Label is not null).Select(t => Fmt.Width(t.Label!, fs))
                .Concat(p.Texts.Select(t => Fmt.Width(t.Text, fs))).DefaultIfEmpty(0).Max();

        private double LegendWidth => Legend.Count == 0 ? 0 : Fs * 0.8 + Fs * 3.2 + Gap + Legend.Max(e => Fmt.Width(e.Text, Fs * 0.9)) + Fs * 0.6;

        /// <summary>Computes the plot area and the panel areas; reduces the font when the box is too small.</summary>
        public void Layout(Lang lang)
        {
            if (MathAxes) { MathLayout(lang); return; }
            for (var attempt = 0; attempt < 6; attempt++)
            {
                double hw = W / 2, hh = H / 2;
                var yw = Panels.Max(p => LabelWidth(p, lang, Fs));
                var left = Pad + (Panels.Any(p => !string.IsNullOrEmpty(p.YTitle)) ? Fs * 1.05 + Gap : 0) + yw + Gap + TickLen;
                var right = Pad;
                var y2 = Panels.Where(p => p.Y2 is not null).ToList();
                if (y2.Count > 0)
                    right += y2.Max(p => MaxWidth(p.Y2!, lang, Fs)) + Gap + TickLen + (y2.Any(p => !string.IsNullOrEmpty(p.Y2Title)) ? Fs * 1.05 + Gap : 0);
                var xt = XTickList(lang).Where(t => t.Label is not null && X.Kind != AxisKind.Category).ToList();
                if (xt.Count > 0)
                {
                    // The first and last x labels are centred on their ticks: keep them inside the box.
                    var firstT = X.T(xt[0].Value); var lastT = X.T(xt[xt.Count - 1].Value);
                    if (firstT < 0.02) left = Math.Max(left, Pad + Fmt.Width(xt[0].Label!, Fs) / 2);
                    if (lastT > 0.98 && y2.Count == 0) right = Math.Max(right, Pad + Fmt.Width(xt[xt.Count - 1].Label!, Fs) / 2);
                }
                if (Arrows || XArrow) right = Math.Max(right, Pad + Fs);
                if (ShowLegend && LegendMode == "right") right += LegendWidth + Gap;
                right += Math.Max(0, ExtraRight) * Fs;
                var top = Pad + (string.IsNullOrEmpty(Title) ? Fs * 0.5 : Fs * 1.3 + Gap) + (Arrows || Panels.Count > 0 && Panels[0].Arrow ? Fs * 0.5 : 0);
                var bottom = Pad + TickLen + Gap + Fs + (string.IsNullOrEmpty(XTitle) ? 0 : Gap + Fs * 1.05) + CaptionH;
                var pw = W - left - right;
                var ph = H - top - bottom;
                if ((pw < W * 0.3 || ph < H * 0.3) && attempt < 5 && Fs > 0.8) { Fs *= 0.8; continue; }
                pw = Math.Max(pw, W * 0.1); ph = Math.Max(ph, H * 0.1);
                Plot = new Rect(-hw + left, -hh + top, pw, ph);
                break;
            }
            var n = Panels.Count;
            var between = n > 1 ? Fs * (PanelGap ?? 1.2) : 0;
            // Panel heights by share (equal shares: equal heights, as before).
            var shares = Panels.Select(p => p.Share > 0 && Fmt.Finite(p.Share) ? p.Share : 1).ToList();
            var total = shares.Sum();
            var free = Math.Max(Plot.H - between * (n - 1), n);
            var y0 = Plot.Y;
            for (var i = 0; i < n; i++)
            {
                var h = free * shares[i] / total;
                Panels[i].Area = new Rect(Plot.X, y0, Plot.W, Math.Max(h, 1));
                y0 += h + between;
            }
            SetLengths();
        }

        /// <summary>The broken axes get their length in mm (the break bands have a fixed size in mm).</summary>
        private void SetLengths()
        {
            X.SetLength(Plot.W);
            foreach (var p in Panels)
            {
                p.Y.SetLength(p.Area.H);
                p.Y2?.SetLength(p.Area.H);
            }
        }

        /// <summary>
        /// Layout of the math style: room for the arrowheads and axis names right and above, tick numbers beside the axes
        /// (which cross at the origin, so the margins depend on where the origin lies).
        /// </summary>
        private void MathLayout(Lang lang)
        {
            for (var attempt = 0; attempt < 6; attempt++)
            {
                double hw = W / 2, hh = H / 2;
                var p = Panels[0];
                var xw = Numbers ? XTickList(lang).Where(t => t.Label is not null).Select(t => Fmt.Width(t.Label!, Fs)).DefaultIfEmpty(0).Max() : 0;
                var yw = Numbers ? p.TickList(lang).Where(t => t.Label is not null).Select(t => Fmt.Width(t.Label!, Fs)).DefaultIfEmpty(0).Max() : 0;
                var xn = string.IsNullOrEmpty(XName) ? 0 : Fmt.Width(XName!, NameFs);
                var yn = string.IsNullOrEmpty(YName) ? 0 : Fmt.Width(YName!, NameFs);
                var right = Pad + Over + (xn > 0 ? 0.25 * NameFs + xn : 0);
                var top = Pad + (string.IsNullOrEmpty(Title) ? 0 : Fs * 1.3 + Gap) + Over + (yn > 0 ? 0.15 * NameFs + NameFs : 0);
                double left = Pad + xw / 2, bottom = Pad + CaptionH + (Numbers ? Fs / 2 : 0);
                for (var iter = 0; iter < 5; iter++)
                {
                    Plot = new Rect(-hw + left, -hh + top, Math.Max(W - left - right, 1), Math.Max(H - top - bottom, 1));
                    p.Area = Plot;
                    SetLengths();
                    double ax = AxisX(p), ay = AxisY(p);
                    var needLeft = Numbers ? yw + MathTick + MathGap + Pad - (ax + hw) : 0;
                    if (yn > 0) needLeft = Math.Max(needLeft, yn / 2 + Pad - (ax + hw));
                    var needBottom = Numbers ? MathTick + MathGap + Fs + Pad + CaptionH - (hh - ay) : 0;
                    if (needLeft <= 1e-6 && needBottom <= 1e-6) break;
                    left += Math.Max(0, needLeft);
                    bottom += Math.Max(0, needBottom);
                }
                if ((Plot.W < W * 0.3 || Plot.H < H * 0.3) && attempt < 5 && Fs > 0.8) { Fs *= 0.8; continue; }
                break;
            }
            SetLengths();
        }

        /// <summary>Page (local) position of a data point in a panel; <paramref name="secondary"/> uses the panel's right axis.</summary>
        public Vec2 P(Panel p, double x, double y, bool secondary = false)
        {
            var a = p.Area;
            var ya = secondary && p.Y2 is not null ? p.Y2 : p.Y;
            return new Vec2(a.X + X.T(x) * a.W, a.Bottom - ya.T(y) * a.H);
        }

        /// <summary>True if the panel position is a finite point.</summary>
        public static bool Ok(Vec2 v) => Fmt.Finite(v.X) && Fmt.Finite(v.Y);

        /// <summary>True if the x axis or the panel's y axis has breaks.</summary>
        public bool HasBreaks(Panel p) => X.HasBreaks || p.Y.HasBreaks;

        /// <summary>True if the data point is not hidden by a break of the x axis or the panel's y axis.</summary>
        public bool IsVisible(Panel p, double x, double y) => !X.InBreak(x) && !p.Y.InBreak(y);

        /// <summary>
        /// The parts of a data polyline outside the axis breaks (cut exactly at the break limits), in data coordinates; non-finite
        /// points are gaps. Without breaks the polyline is returned as one run.
        /// </summary>
        public IReadOnlyList<IReadOnlyList<Vec2>> VisibleRuns(Panel p, IReadOnlyList<Vec2> data)
        {
            if (!HasBreaks(p)) return new[] { data };
            var plot = new BrokenPlot(p.Area, X.Min, X.Max, X.Breaks, p.Y.Min, p.Y.Max, p.Y.Breaks);
            var runs = new List<IReadOnlyList<Vec2>>();
            var piece = new List<Vec2>();
            void Flush()
            {
                if (piece.Count > 0) runs.AddRange(plot.VisibleRuns(piece));
                piece = new List<Vec2>();
            }
            foreach (var v in data)
            {
                if (!Ok(v)) { Flush(); continue; }
                piece.Add(v);
            }
            Flush();
            return runs;
        }

        /// <summary>Grid lines behind the data (major and minor).</summary>
        public void DrawGrid(List<Primitive> into)
        {
            var xGrid = XGrid ?? Grid;
            if (!xGrid && !Panels.Any(p => p.Grid ?? Grid)) return;
            var major = new List<IReadOnlyList<Vec2>>();
            var minor = new List<IReadOnlyList<Vec2>>();
            foreach (var p in Panels)
            {
                var a = p.Area;
                if (X.Kind != AxisKind.Category && xGrid)
                    foreach (var t in XTickList(Lang.En))
                    {
                        if (t.Mark == TickMark.None) continue;
                        var x = a.X + X.T(t.Value) * a.W;
                        if (!MathAxes && (Math.Abs(x - a.X) < 0.01 || Math.Abs(x - a.Right) < 0.01 && Box)) continue;
                        (t.IsMajor ? major : minor).Add(new[] { new Vec2(x, a.Y), new Vec2(x, a.Bottom) });
                    }
                if (p.Y.Kind != AxisKind.Category && (p.Grid ?? Grid))
                    foreach (var t in p.TickList(Lang.En))
                    {
                        if (t.Mark == TickMark.None) continue;
                        var y = a.Bottom - p.Y.T(t.Value) * a.H;
                        if (!MathAxes && (Math.Abs(y - a.Bottom) < 0.01 || Math.Abs(y - a.Y) < 0.01 && Box)) continue;
                        (t.IsMajor ? major : minor).Add(new[] { new Vec2(a.X, y), new Vec2(a.Right, y) });
                    }
            }
            if (Draw.MultiPath(minor, S.GridMinor(Mono), ChartTags.GridMinor) is { } mi) into.Add(mi);
            var majorStyle = GridDash is { } gd && !Mono ? S.Grid(Mono) with { Dash = gd } : S.Grid(Mono);
            if (Draw.MultiPath(major, majorStyle, ChartTags.Grid) is { } ma) into.Add(ma);
        }

        /// <summary>Thin zero line where 0 lies strictly inside a linear value axis (bars, Bode magnitude, negative data).</summary>
        public void DrawZero(List<Primitive> into)
        {
            if (MathAxes) return; // the axes themselves run through zero
            foreach (var p in Panels)
            {
                var a = p.Area;
                if (p.Y.Kind == AxisKind.Linear && p.Y.Min < 0 && p.Y.Max > 0 && !p.Y.InBreak(0))
                {
                    var y = a.Bottom - p.Y.T(0) * a.H;
                    into.Add(Prim.Line(new Vec2(a.X, y), new Vec2(a.Right, y), S.Zero) with { Tag = ChartTags.Zero });
                }
                if (X.Kind == AxisKind.Linear && X.Min < 0 && X.Max > 0 && !X.InBreak(0))
                {
                    var x = a.X + X.T(0) * a.W;
                    into.Add(Prim.Line(new Vec2(x, a.Y), new Vec2(x, a.Bottom), S.Zero) with { Tag = ChartTags.Zero });
                }
            }
        }

        /// <summary>Axis lines, ticks, tick labels, axis titles and chart title (drawn above the data).</summary>
        public void DrawAxes(List<Primitive> into, Lang lang)
        {
            if (MathAxes) DrawMathAxes(into, lang);
            else DrawFrameAxes(into, lang);
            DrawBreaks(into);
        }

        private void DrawFrameAxes(List<Primitive> into, Lang lang)
        {
            var ticks = new List<IReadOnlyList<Vec2>>();
            var yticks = new List<IReadOnlyList<Vec2>>();
            var labelColor = Palette.Ink;
            double hw = W / 2, hh = H / 2;
            for (var pi = 0; pi < Panels.Count; pi++)
            {
                var p = Panels[pi];
                var a = p.Area;
                var last = pi == Panels.Count - 1;
                // Part keys per panel: "axis.y" in a single-panel chart, "axis.y.2" for the second of stacked panels.
                var ps = Panels.Count > 1 ? "." + Fmt.I(pi + 1) : "";
                if (Box) into.Add(Prim.Rect(a.X, a.Y, a.W, a.H, S.Axis) with { Tag = ChartTags.Axis + ".box" + ps });
                else
                {
                    var axis = S.Axis;
                    var withHead = axis with { ArrowEnd = ArrowHead.Triangle, ArrowSize = ArrowSize.Small };
                    var xArrow = Arrows || XArrow;
                    var yArrow = Arrows || p.Arrow;
                    var xExt = xArrow ? Fs * 0.8 : 0;
                    var yExt = yArrow ? Fs * 0.8 : 0;
                    into.Add(Prim.Line(new Vec2(a.X, a.Bottom), new Vec2(a.Right + xExt, a.Bottom), xArrow ? withHead : axis) with { Tag = ChartTags.Axis + ".x" + ps });
                    into.Add(Prim.Line(new Vec2(a.X, a.Bottom), new Vec2(a.X, a.Y - yExt), yArrow ? withHead : axis) with { Tag = ChartTags.Axis + ".y" + ps });
                    if (p.Y2 is not null) into.Add(Prim.Line(new Vec2(a.Right, a.Bottom), new Vec2(a.Right, a.Y), axis) with { Tag = ChartTags.Axis + ".y2" + ps });
                }

                // X ticks (every panel) and labels (bottom panel).
                var xl = new List<(double Pos, string Text)>();
                foreach (var t in XTickList(lang))
                {
                    var x = a.X + X.T(t.Value) * a.W;
                    if (t.Mark != TickMark.None)
                    {
                        var len = t.IsMajor ? TickLen : TickLen * 0.55;
                        ticks.Add(new[] { new Vec2(x, a.Bottom), new Vec2(x, a.Bottom + len) });
                    }
                    if (last && t.Label is not null) xl.Add((x, t.Label));
                }
                if (last) XLabels(into, xl, a, labelColor);
                if (last) AxisTexts(into, XTexts, a, labelColor);

                YAxis(into, yticks, p.Y, a, a.X, -1, lang, labelColor, ChartTags.TickLabel + ".y" + ps, p.Ticks);
                foreach (var t in p.Texts)
                {
                    if (!p.Y.Visible(t.Value)) continue;
                    var py = Geo.Clamp(a.Bottom - p.Y.T(t.Value) * a.H, -hh + Pad + Fs / 2, hh - Pad - Fs / 2);
                    into.Add(new TextPrim(new Vec2(a.X - TickLen - Gap, py), t.Text, new TextStyle { Size = Fs, Color = labelColor, Anchor = TextAnchor.End }) { Tag = t.Tag });
                }
                if (p.Y2 is not null) YAxis(into, yticks, p.Y2, a, a.Right, 1, lang, labelColor, ChartTags.TickLabel + ".y2" + ps);

                // Axis titles.
                if (!string.IsNullOrEmpty(p.YTitle))
                {
                    var fs = Fit(p.YTitle!, Fs, a.H);
                    into.Add(new TextPrim(new Vec2(-hw + Pad + Fs * 0.55, a.Y + a.H / 2), p.YTitle!, new TextStyle { Size = fs, Color = labelColor }, -90) { Tag = ChartTags.AxisTitle + ".y" + ps });
                }
                if (p.Y2 is not null && !string.IsNullOrEmpty(p.Y2Title))
                {
                    var fs = Fit(p.Y2Title!, Fs, a.H);
                    into.Add(new TextPrim(new Vec2(hw - Pad - Fs * 0.55, a.Y + a.H / 2), p.Y2Title!, new TextStyle { Size = fs, Color = labelColor }, -90) { Tag = ChartTags.AxisTitle + ".y2" + ps });
                }
            }
            if (Draw.MultiPath(ticks, S.Tick, ChartTags.Tick + ".x") is { } tp) into.Add(tp);
            if (Draw.MultiPath(yticks, S.Tick, ChartTags.Tick + ".y") is { } typ) into.Add(typ);
            if (!string.IsNullOrEmpty(XTitle))
            {
                var fs = Fit(XTitle!, Fs, Plot.W);
                into.Add(new TextPrim(new Vec2(Plot.X + Plot.W / 2, hh - Pad - CaptionH - Fs * 0.5), XTitle!, new TextStyle { Size = fs, Color = labelColor }) { Tag = ChartTags.AxisTitle + ".x" });
            }
            if (!string.IsNullOrEmpty(Caption))
            {
                var fs = Math.Max(0.05, Math.Min(Fs, (W - 2 * Pad) / (Math.Max(1, Caption!.Length) * Fmt.CharWidth)));
                into.Add(new TextPrim(new Vec2(0, hh - Pad - Fs * 0.55), Caption!, new TextStyle { Size = fs, Color = labelColor }) { Tag = ChartTags.Caption });
            }
            if (!string.IsNullOrEmpty(Title))
            {
                var fs = Fit(Title!, Fs * 1.1, W - 2 * Pad);
                into.Add(new TextPrim(new Vec2(0, -hh + Pad + Fs * 0.6), Title!, new TextStyle { Size = fs, Bold = true, Color = labelColor }) { Tag = ChartTags.Title });
            }
        }

        /// <summary>
        /// Math-style axes: both axes through the origin with arrowheads beyond the plot, ticks across the axis lines, numbers
        /// below the x axis and left of the y axis (on the other side where a curve passes, <see cref="NumberAvoid"/>), a
        /// single "0" at the origin and the axis names at the arrow tips.
        /// </summary>
        private void DrawMathAxes(List<Primitive> into, Lang lang)
        {
            double hw = W / 2, hh = H / 2;
            var p = Panels[0];
            var a = p.Area;
            double ax = AxisX(p), ay = AxisY(p);
            var head = S.Axis with { ArrowEnd = ArrowHead.Triangle, ArrowSize = ArrowSize.Small };
            into.Add(Prim.Line(new Vec2(a.X, ay), new Vec2(a.Right + Over, ay), head) with { Tag = ChartTags.Axis + ".x" });
            into.Add(Prim.Line(new Vec2(ax, a.Bottom), new Vec2(ax, a.Y - Over), head) with { Tag = ChartTags.Axis + ".y" });
            var color = Palette.Ink;
            bool Hit(Rect r) => NumberAvoid.Any(r.Contains);
            var xt = new List<IReadOnlyList<Vec2>>();
            var yt = new List<IReadOnlyList<Vec2>>();

            var lastRight = double.NegativeInfinity;
            foreach (var t in XTickList(lang).OrderBy(t => t.Value))
            {
                var x = a.X + X.T(t.Value) * a.W;
                if (!Fmt.Finite(x) || Math.Abs(x - ax) < 0.01) continue; // the origin: the "0" below
                if (t.Mark != TickMark.None)
                {
                    var len = t.IsMajor ? MathTick : MathTick * 0.6;
                    xt.Add(new[] { new Vec2(x, ay - len), new Vec2(x, ay + len) });
                }
                if (!Numbers || t.Label is null) continue;
                var w = Fmt.Width(t.Label, Fs);
                var cx = Geo.Clamp(x, -hw + Pad + w / 2, hw - Pad - w / 2);
                if (cx - w / 2 < lastRight + Fs * 0.4) continue; // thinned out
                var y = ay + MathTick + MathGap + Fs / 2;
                var above = new Rect(cx - w / 2, ay - MathTick - MathGap - Fs, w, Fs);
                if (Hit(new Rect(cx - w / 2, ay + MathTick + MathGap, w, Fs)) && above.Y >= a.Y && !Hit(above)) y = ay - MathTick - MathGap - Fs / 2;
                y = Geo.Clamp(y, -hh + Pad + Fs / 2, hh - Pad - Fs / 2);
                into.Add(new TextPrim(new Vec2(cx, y), t.Label, new TextStyle { Size = Fs, Color = color }) { Tag = ChartTags.TickLabel + ".x" });
                lastRight = cx + w / 2;
            }

            var lastY = double.PositiveInfinity;
            foreach (var t in p.TickList(lang).Select(t => (t, Pos: a.Bottom - p.Y.T(t.Value) * a.H)).Where(x => Fmt.Finite(x.Pos)).OrderByDescending(x => x.Pos).Select(x => x.t))
            {
                var y = a.Bottom - p.Y.T(t.Value) * a.H;
                if (Math.Abs(y - ay) < 0.01) continue;
                if (t.Mark != TickMark.None)
                {
                    var len = t.IsMajor ? MathTick : MathTick * 0.6;
                    yt.Add(new[] { new Vec2(ax - len, y), new Vec2(ax + len, y) });
                }
                if (!Numbers || t.Label is null) continue;
                var cy = Geo.Clamp(y, -hh + Pad + Fs / 2, hh - Pad - Fs / 2);
                if (lastY - cy < Fs * 1.1) continue; // thinned out
                var w = Fmt.Width(t.Label, Fs);
                var right = new Rect(ax + MathTick + MathGap, cy - Fs / 2, w, Fs);
                var onRight = Hit(new Rect(ax - MathTick - MathGap - w, cy - Fs / 2, w, Fs)) && right.Right <= a.Right && !Hit(right);
                var x = onRight ? ax + MathTick + MathGap : Math.Max(ax - MathTick - MathGap, -hw + Pad + w);
                into.Add(new TextPrim(new Vec2(x, cy), t.Label, new TextStyle { Size = Fs, Color = color, Anchor = onRight ? TextAnchor.Start : TextAnchor.End }) { Tag = ChartTags.TickLabel + ".y" });
                lastY = cy;
            }
            if (Numbers && X.Kind == AxisKind.Linear && p.Y.Kind == AxisKind.Linear && OriginX == 0 && OriginY == 0 && X.Visible(0) && p.Y.Visible(0))
            {
                var w = Fmt.Width("0", Fs);
                var at = new Vec2(Math.Max(ax - MathGap, -hw + Pad + w), Geo.Clamp(ay + MathTick + MathGap + Fs / 2, -hh + Pad + Fs / 2, hh - Pad - Fs / 2));
                into.Add(new TextPrim(at, "0", new TextStyle { Size = Fs, Color = color, Anchor = TextAnchor.End }) { Tag = ChartTags.TickLabel + ".x" });
            }
            if (Draw.MultiPath(xt, S.Tick, ChartTags.Tick + ".x") is { } tx) into.Add(tx);
            if (Draw.MultiPath(yt, S.Tick, ChartTags.Tick + ".y") is { } ty) into.Add(ty);

            if (!string.IsNullOrEmpty(XName))
            {
                var fs = Math.Min(NameFs, Math.Max(0.3, (hw - Pad - (a.Right + Over + 0.25 * NameFs)) / (Math.Max(1, XName!.Length) * Fmt.CharWidth)));
                into.Add(new TextPrim(new Vec2(a.Right + Over + 0.25 * NameFs, Geo.Clamp(ay, -hh + Pad + fs / 2, hh - Pad - fs / 2)), XName!,
                    new TextStyle { Size = fs, Color = color, Italic = true, Anchor = TextAnchor.Start }) { Tag = ChartTags.AxisTitle + ".x" });
            }
            if (!string.IsNullOrEmpty(YName))
            {
                var w = Fmt.Width(YName!, NameFs);
                var at = new Vec2(Geo.Clamp(ax, -hw + Pad + w / 2, hw - Pad - w / 2), a.Y - Over - 0.15 * NameFs - NameFs / 2);
                into.Add(new TextPrim(at, YName!, new TextStyle { Size = NameFs, Color = color, Italic = true }) { Tag = ChartTags.AxisTitle + ".y" });
            }
            if (!string.IsNullOrEmpty(Caption))
            {
                var fs = Math.Max(0.05, Math.Min(Fs, (W - 2 * Pad) / (Math.Max(1, Caption!.Length) * Fmt.CharWidth)));
                into.Add(new TextPrim(new Vec2(0, hh - Pad - Fs * 0.55), Caption!, new TextStyle { Size = fs, Color = color }) { Tag = ChartTags.Caption });
            }
            if (!string.IsNullOrEmpty(Title))
            {
                var fs = Fit(Title!, Fs * 1.1, W - 2 * Pad);
                into.Add(new TextPrim(new Vec2(0, -hh + Pad + Fs * 0.6), Title!, new TextStyle { Size = fs, Bold = true, Color = color }) { Tag = ChartTags.Title });
            }
        }

        /// <summary>
        /// Break markers (charts v2 stage 3) above the axes: a zigzag band across the plot and the axis line, or a double slash
        /// on the axis line (and on the opposite frame side of a closed frame). The band is filled with white so the axis line
        /// appears interrupted; parts "axisbreak.y" (per panel like "axis.y.2") and "axisbreak.x".
        /// </summary>
        private void DrawBreaks(List<Primitive> into)
        {
            double hw = W / 2 - 0.05, hh = H / 2 - 0.05;
            var w = AxisBreaks.AxisMarkerWidth;
            Vec2 In(Vec2 v) => new(Geo.Clamp(v.X, -hw, hw), Geo.Clamp(v.Y, -hh, hh));
            var xMarkers = new List<BreakMarker>();
            for (var pi = 0; pi < Panels.Count; pi++)
            {
                var p = Panels[pi];
                var a = p.Area;
                var ps = Panels.Count > 1 ? "." + Fmt.I(pi + 1) : "";
                if (p.Y.Broken is { } yb)
                {
                    var list = new List<BreakMarker>();
                    var axisX = AxisX(p);
                    var far = Box || p.Y2 is not null;
                    for (var i = 0; i < yb.Breaks.Count; i++)
                    {
                        var (g0, g1) = yb.GapPositions(i);
                        var y = a.Bottom - (g0 + g1) / 2;
                        if (p.Y.BreakStyle == BreakMarkerStyle.Zigzag)
                            list.Add(AxisBreaks.Marker(BreakMarkerStyle.Zigzag, In(new Vec2(Math.Min(a.X, axisX - w / 2), y)), In(new Vec2(Math.Max(far ? a.Right + w / 2 : a.Right, axisX + w / 2), y))));
                        else
                        {
                            list.Add(AxisBreaks.Marker(BreakMarkerStyle.Slash, In(new Vec2(axisX - w / 2, y)), In(new Vec2(axisX + w / 2, y))));
                            if (far) list.Add(AxisBreaks.Marker(BreakMarkerStyle.Slash, In(new Vec2(a.Right - w / 2, y)), In(new Vec2(a.Right + w / 2, y))));
                        }
                    }
                    EmitBreaks(into, list, ChartTags.AxisBreak + ".y" + ps);
                }
                if (X.Broken is { } xb)
                {
                    var axisY = AxisY(p);
                    for (var i = 0; i < xb.Breaks.Count; i++)
                    {
                        var (g0, g1) = xb.GapPositions(i);
                        var x = a.X + (g0 + g1) / 2;
                        if (X.BreakStyle == BreakMarkerStyle.Zigzag)
                            xMarkers.Add(AxisBreaks.Marker(BreakMarkerStyle.Zigzag, In(new Vec2(x, Math.Max(a.Bottom, axisY + w / 2))), In(new Vec2(x, Math.Min(Box ? a.Y - w / 2 : a.Y, axisY - w / 2)))));
                        else
                        {
                            xMarkers.Add(AxisBreaks.Marker(BreakMarkerStyle.Slash, In(new Vec2(x, axisY + w / 2)), In(new Vec2(x, axisY - w / 2))));
                            if (Box) xMarkers.Add(AxisBreaks.Marker(BreakMarkerStyle.Slash, In(new Vec2(x, a.Y + w / 2)), In(new Vec2(x, a.Y - w / 2))));
                        }
                    }
                }
            }
            EmitBreaks(into, xMarkers, ChartTags.AxisBreak + ".x");
        }

        private static void EmitBreaks(List<Primitive> into, List<BreakMarker> markers, string tag)
        {
            if (markers.Count == 0) return;
            var band = new List<PathSeg>();
            foreach (var m in markers)
            {
                band.Add(PathSeg.Move(m.Band[0]));
                for (var i = 1; i < m.Band.Count; i++) band.Add(PathSeg.Line(m.Band[i]));
                band.Add(PathSeg.Close);
            }
            into.Add(new PathPrim(band, S.FillOnly(Palette.White)) { Tag = tag });
            var line = new Style { Stroke = Palette.Axis, StrokeWidth = 0.25, Role = LineRole.Visible };
            if (Draw.MultiPath(markers.SelectMany(m => new[] { m.Line1, m.Line2 }), line, tag) is { } lines) into.Add(lines);
        }

        private static double Fit(string text, double fs, double width) => Math.Max(0.5, Math.Min(fs, width / (Math.Max(1, text.Length) * Fmt.CharWidth)));

        private void XLabels(List<Primitive> into, List<(double Pos, string Text)> labels, Rect a, string color)
        {
            if (labels.Count == 0) return;
            double hw = W / 2;
            var fs = Fs;
            if (X.Kind == AxisKind.Category)
            {
                // Category labels share the band width: one common (reduced) font size.
                var band = a.W / Math.Max(1, X.Categories.Count);
                fs = Math.Max(0.6, Math.Min(Fs, labels.Min(l => band * 0.95 / (Math.Max(1, l.Text.Length) * Fmt.CharWidth))));
            }
            var y = a.Bottom + TickLen + Gap + Fs / 2;
            var lastRight = double.NegativeInfinity;
            foreach (var (pos, text) in labels)
            {
                var w = Fmt.Width(text, fs);
                var x = Geo.Clamp(pos, -hw + Pad + w / 2, hw - Pad - w / 2);
                if (x - w / 2 < lastRight + fs * 0.4) continue; // thinned out: would overlap the previous label
                into.Add(new TextPrim(new Vec2(x, y), text, new TextStyle { Size = fs, Color = color }) { Tag = ChartTags.TickLabel + ".x" });
                lastRight = x + w / 2;
            }
        }

        /// <summary>Texts below the x axis at their x values (no thinning; kept inside the box).</summary>
        private void AxisTexts(List<Primitive> into, IReadOnlyList<AxisText> texts, Rect a, string color)
        {
            double hw = W / 2;
            var y = a.Bottom + TickLen + Gap + Fs / 2;
            foreach (var t in texts)
            {
                if (!X.Visible(t.Value)) continue;
                var w = Fmt.Width(t.Text, Fs);
                var x = Geo.Clamp(a.X + X.T(t.Value) * a.W, -hw + Pad + w / 2, hw - Pad - w / 2);
                into.Add(new TextPrim(new Vec2(x, y), t.Text, new TextStyle { Size = Fs, Color = color }) { Tag = t.Tag });
            }
        }

        private void YAxis(List<Primitive> into, List<IReadOnlyList<Vec2>> ticks, Axis y, Rect a, double x0, int side, Lang lang, string color, string labelTag, IReadOnlyList<Tick>? custom = null)
        {
            double hh = H / 2;
            var fs = Fs;
            if (y.Kind == AxisKind.Category)
            {
                var band = a.H / Math.Max(1, y.Categories.Count);
                fs = Math.Max(0.6, Math.Min(Fs, band * 0.8));
            }
            var lastY = double.PositiveInfinity;
            var list = (custom?.Where(t => y.Visible(t.Value)) ?? y.Ticks(lang)).Select(t => (t, Pos: a.Bottom - y.T(t.Value) * a.H)).OrderByDescending(x => x.Pos).ToList();
            foreach (var (t, py) in list)
            {
                if (t.Mark != TickMark.None)
                {
                    var len = t.IsMajor ? TickLen : TickLen * 0.55;
                    ticks.Add(new[] { new Vec2(x0, py), new Vec2(x0 + side * len, py) });
                }
                if (t.Label is null) continue;
                var cy = Geo.Clamp(py, -hh + Pad + fs / 2, hh - Pad - fs / 2);
                if (lastY - cy < fs * 1.1) continue; // thinned out
                var x = x0 + side * (TickLen + Gap);
                into.Add(new TextPrim(new Vec2(x, cy), t.Label, new TextStyle { Size = fs, Color = color, Anchor = side < 0 ? TextAnchor.End : TextAnchor.Start }) { Tag = labelTag });
                lastY = cy;
            }
        }

        /// <summary>
        /// Legend: inside the plot in the corner that covers the fewest data points (<paramref name="avoid"/>), or right of the
        /// plot. Each entry shows a line sample with dash pattern and marker, or an area sample, and its text.
        /// </summary>
        public void DrawLegend(List<Primitive> into, IReadOnlyList<Vec2> avoid)
        {
            if (!ShowLegend) return;
            var fs = Fs * 0.9;
            var rows = Legend.Count;
            var lw = LegendWidth;
            var lh = rows * LegendRow + Fs * 0.5;
            Rect box;
            if (LegendMode == "right")
            {
                box = new Rect(Plot.Right + Gap + (Panels.Any(p => p.Y2 is not null) ? 0 : 0), Plot.Y, lw, lh);
                box = new Rect(Math.Min(box.X, W / 2 - Pad - lw), box.Y, lw, lh);
            }
            else
            {
                var inset = Fs * 0.4;
                var a = Panels[0].Area;
                lw = Math.Min(lw, a.W - 2 * inset);
                lh = Math.Min(lh, a.H - 2 * inset);
                var candidates = new[]
                {
                    new Rect(a.Right - inset - lw, a.Y + inset, lw, lh),
                    new Rect(a.X + inset, a.Y + inset, lw, lh),
                    new Rect(a.Right - inset - lw, a.Bottom - inset - lh, lw, lh),
                    new Rect(a.X + inset, a.Bottom - inset - lh, lw, lh),
                };
                box = candidates.OrderBy(c => avoid.Count(c.Inflate(Fs * 0.3).Contains)).First();
            }
            into.Add(Prim.Rect(box.X, box.Y, box.W, box.H, S.LegendBox) with { Tag = ChartTags.Legend });
            var sample = Math.Min(Fs * 3.2, box.W * 0.35);
            for (var i = 0; i < rows; i++)
            {
                var e = Legend[i];
                var cy = box.Y + Fs * 0.25 + LegendRow * (i + 0.5);
                if (cy > box.Bottom) break;
                var x0 = box.X + Fs * 0.4;
                if (e.Area)
                {
                    var h = Math.Min(LegendRow * 0.65, fs);
                    into.Add(Prim.Rect(x0, cy - h / 2, sample, h, e.Style) with { Tag = ChartTags.LegendSample });
                }
                else
                {
                    into.Add(Prim.Line(new Vec2(x0, cy), new Vec2(x0 + sample, cy), e.Style) with { Tag = ChartTags.LegendSample });
                    if (e.Marker is MarkerKind mk)
                        into.Add(Draw.Markers(new[] { new Vec2(x0 + sample / 2, cy) }, mk, e.FilledMarker, e.Style.EffectiveStroke, Fs * 0.42) with { Tag = ChartTags.LegendSample });
                }
                var tw = box.Right - (x0 + sample + Gap) - Fs * 0.2;
                into.Add(new TextPrim(new Vec2(x0 + sample + Gap, cy), e.Text, new TextStyle { Size = Fit(e.Text, fs, Math.Max(tw, 1)), Anchor = TextAnchor.Start, Color = Palette.Ink }) { Tag = ChartTags.LegendText });
            }
        }

        /// <summary>Generic anchors: plot corners, plot centre, axis origin and the axes as rails.</summary>
        public IEnumerable<Anchor> FrameAnchors()
        {
            var a = Plot;
            yield return Anchor.Point(AnchorRole.Corner, new Vec2(a.X, a.Y), "plot.tl");
            yield return Anchor.Point(AnchorRole.Corner, new Vec2(a.Right, a.Y), "plot.tr");
            yield return Anchor.Point(AnchorRole.Corner, new Vec2(a.Right, a.Bottom), "plot.br");
            yield return Anchor.Point(AnchorRole.Corner, new Vec2(a.X, a.Bottom), "plot.bl");
            yield return Anchor.Point(AnchorRole.Center, a.Center, "plot.c");
            if (MathAxes)
            {
                // Math style: the origin and the axes where they are drawn, and the grid points (to dock points and vectors).
                var p = Panels[0];
                double ax = AxisX(p), ay = AxisY(p);
                yield return Anchor.Point(AnchorRole.Port, new Vec2(ax, ay), "origin");
                yield return Anchor.Rail(new Vec2(p.Area.X, ay), new Vec2(p.Area.Right + Over, ay), "axis.x");
                yield return Anchor.Rail(new Vec2(ax, p.Area.Bottom), new Vec2(ax, p.Area.Y - Over), "axis.y");
                var xs = XTickList(Lang.En).Where(t => t.Mark == TickMark.Major).Select(t => t.Value).ToList();
                var ys = p.TickList(Lang.En).Where(t => t.Mark == TickMark.Major).Select(t => t.Value).ToList();
                if (xs.Count * ys.Count <= 900)
                    foreach (var gx in xs)
                        foreach (var gy in ys)
                        {
                            var g = P(p, gx, gy);
                            if (Ok(g)) yield return Anchor.Point(AnchorRole.Mid, g, "g" + Fmt.Num(gx, Lang.En, 6) + "_" + Fmt.Num(gy, Lang.En, 6));
                        }
                yield break;
            }
            for (var i = 0; i < Panels.Count; i++)
            {
                var p = Panels[i];
                var suffix = i > 0 ? Fmt.I(i + 1) : "";
                // Origin: (0, 0) where it lies inside the panel, else the axis corner.
                var ox = X.Kind == AxisKind.Linear && X.Contains(0) ? 0 : (double?)null;
                var oy = p.Y.Kind == AxisKind.Linear && p.Y.Contains(0) ? 0 : (double?)null;
                var o = new Vec2(ox is null ? p.Area.X : P(p, 0, 0).X, oy is null ? p.Area.Bottom : P(p, X.Min, 0).Y);
                yield return Anchor.Point(AnchorRole.Port, o, "origin" + suffix);
                yield return Anchor.Rail(new Vec2(p.Area.X, p.Area.Bottom), new Vec2(p.Area.Right, p.Area.Bottom), "axis.x" + suffix);
                yield return Anchor.Rail(new Vec2(p.Area.X, p.Area.Bottom), new Vec2(p.Area.X, p.Area.Y), "axis.y" + suffix);
                if (p.Y2 is not null) yield return Anchor.Rail(new Vec2(p.Area.Right, p.Area.Bottom), new Vec2(p.Area.Right, p.Area.Y), "axis.y2" + suffix);
            }
        }
    }

    /// <summary>Drawing helpers: series lines with clipping, markers, multi-sub-path freeforms.</summary>
    internal static class Draw
    {
        /// <summary>
        /// One path with an open sub-path per piece, so a host draws a single native freeform instead of many line shapes.
        /// Null if there is nothing to draw.
        /// </summary>
        public static PathPrim? MultiPath(IEnumerable<IReadOnlyList<Vec2>> pieces, Style style, string tag)
        {
            var segs = new List<PathSeg>();
            foreach (var piece in pieces)
            {
                if (piece.Count < 2) continue;
                segs.Add(PathSeg.Move(piece[0]));
                for (var i = 1; i < piece.Count; i++) segs.Add(PathSeg.Line(piece[i]));
            }
            return segs.Count == 0 ? null : new PathPrim(segs, style) { Tag = tag };
        }

        /// <summary>Clips a polyline to a rectangle (Liang–Barsky per segment); returns the visible pieces.</summary>
        public static List<List<Vec2>> Clip(IReadOnlyList<Vec2> pts, Rect r)
        {
            var pieces = new List<List<Vec2>>();
            List<Vec2>? cur = null;
            for (var i = 0; i + 1 < pts.Count; i++)
            {
                var a = pts[i]; var b = pts[i + 1];
                if (!Frame.Ok(a) || !Frame.Ok(b)) { cur = null; continue; }
                if (!ClipSegment(a, b, r, out var p, out var q)) { cur = null; continue; }
                if (cur is null || cur[cur.Count - 1].DistanceTo(p) > 1e-6) { cur = new List<Vec2> { p }; pieces.Add(cur); }
                cur.Add(q);
                if (q.DistanceTo(b) > 1e-6) cur = null;
            }
            return pieces.Where(p => p.Count >= 2).ToList();
        }

        private static bool ClipSegment(Vec2 a, Vec2 b, Rect r, out Vec2 p, out Vec2 q)
        {
            const double tol = 1e-7;
            double t0 = 0, t1 = 1, dx = b.X - a.X, dy = b.Y - a.Y;
            bool Edge(double pp, double qq)
            {
                if (Math.Abs(pp) < 1e-15) return qq >= -tol;
                var t = qq / pp;
                if (pp < 0) { if (t > t1) return false; if (t > t0) t0 = t; }
                else { if (t < t0) return false; if (t < t1) t1 = t; }
                return true;
            }
            var ok = Edge(-dx, a.X - r.X + tol) && Edge(dx, r.Right - a.X + tol) && Edge(-dy, a.Y - r.Y + tol) && Edge(dy, r.Bottom - a.Y + tol);
            p = new Vec2(a.X + t0 * dx, a.Y + t0 * dy);
            q = new Vec2(a.X + t1 * dx, a.Y + t1 * dy);
            return ok && t1 >= t0;
        }

        /// <summary>Catmull-Rom spline through the points as cubic Bézier segments.</summary>
        public static List<PathSeg> Spline(IReadOnlyList<Vec2> p)
        {
            var n = p.Count;
            var segs = new List<PathSeg> { PathSeg.Move(p[0]) };
            if (n == 2) { segs.Add(PathSeg.Line(p[1])); return segs; }
            Vec2 P(int i) => p[Math.Max(0, Math.Min(n - 1, i))];
            for (var i = 0; i < n - 1; i++)
            {
                Vec2 p0 = P(i - 1), p1 = P(i), p2 = P(i + 1), p3 = P(i + 2);
                segs.Add(PathSeg.Cubic(p1 + (p2 - p0) / 6, p2 - (p3 - p1) / 6, p2));
            }
            return segs;
        }

        /// <summary>Samples a path of moves, lines and cubics into a polyline.</summary>
        public static List<Vec2> Sample(IReadOnlyList<PathSeg> segs, int steps = 10)
        {
            var pts = new List<Vec2>();
            var last = Vec2.Zero;
            foreach (var s in segs)
            {
                switch (s.Op)
                {
                    case SegOp.M: case SegOp.L: pts.Add(s.P); last = s.P; break;
                    case SegOp.C:
                        for (var i = 1; i <= steps; i++) pts.Add(Geo.CubicAt(last, s.C1, s.C2, s.P, (double)i / steps));
                        last = s.P; break;
                    case SegOp.Q:
                        for (var i = 1; i <= steps; i++) pts.Add(Geo.QuadAt(last, s.C1, s.P, (double)i / steps));
                        last = s.P; break;
                }
            }
            return pts;
        }

        /// <summary>
        /// A series line in plot coordinates: straight segments (clipped to <paramref name="clip"/>) or a smooth curve
        /// (cubic path when it lies inside the plot, else sampled and clipped). Null when nothing is visible.
        /// </summary>
        public static PathPrim? Series(IReadOnlyList<Vec2> pts, Rect clip, Style style, bool smooth, string tag = ChartTags.Series)
        {
            var clean = pts.Where(Frame.Ok).ToList();
            if (clean.Count < 2) return null;
            var inside = clip.Inflate(1e-6);
            if (smooth && clean.Count >= 3)
            {
                var spline = Spline(clean);
                var sampled = Sample(spline);
                if (sampled.All(inside.Contains)) return new PathPrim(spline, style) { Tag = tag };
                return MultiPath(Clip(sampled, clip), style, tag);
            }
            if (clean.All(inside.Contains)) return Prim.Polyline(clean, style) with { Tag = tag };
            return MultiPath(Clip(clean, clip), style, tag);
        }

        /// <summary>
        /// All markers of a series as one path (closed sub-paths for circle, square, triangle and diamond; open strokes for
        /// cross and plus). Filled markers use the series colour, hollow ones white.
        /// </summary>
        public static PathPrim Markers(IEnumerable<Vec2> centres, MarkerKind kind, bool filled, string color, double r)
        {
            var segs = new List<PathSeg>();
            foreach (var c in centres)
            {
                switch (kind)
                {
                    case MarkerKind.Circle:
                        segs.AddRange(Prim.EllipseSegments(c, r, r));
                        break;
                    case MarkerKind.Square:
                        Poly(segs, new[] { c + new Vec2(-r, -r) * 0.85, c + new Vec2(r, -r) * 0.85, c + new Vec2(r, r) * 0.85, c + new Vec2(-r, r) * 0.85 });
                        break;
                    case MarkerKind.Triangle:
                        Poly(segs, new[] { c + new Vec2(0, -r * 1.15), c + new Vec2(r, r * 0.75), c + new Vec2(-r, r * 0.75) });
                        break;
                    case MarkerKind.Diamond:
                        Poly(segs, new[] { c + new Vec2(0, -r * 1.2), c + new Vec2(r, 0), c + new Vec2(0, r * 1.2), c + new Vec2(-r, 0) });
                        break;
                    case MarkerKind.Cross:
                        segs.Add(PathSeg.Move(c + new Vec2(-r, -r) * 0.8)); segs.Add(PathSeg.Line(c + new Vec2(r, r) * 0.8));
                        segs.Add(PathSeg.Move(c + new Vec2(-r, r) * 0.8)); segs.Add(PathSeg.Line(c + new Vec2(r, -r) * 0.8));
                        break;
                    default:
                        segs.Add(PathSeg.Move(c + new Vec2(-r, 0))); segs.Add(PathSeg.Line(c + new Vec2(r, 0)));
                        segs.Add(PathSeg.Move(c + new Vec2(0, -r))); segs.Add(PathSeg.Line(c + new Vec2(0, r)));
                        break;
                }
            }
            var open = kind is MarkerKind.Cross or MarkerKind.Plus;
            var style = new Style
            {
                Stroke = color,
                StrokeWidth = open ? 0.35 : 0.25,
                Role = LineRole.Visible,
                Dash = DashStyle.Solid,
                Fill = open ? Fill.None : Fill.Solid(filled ? color : Palette.White),
            };
            return new PathPrim(segs, style) { Tag = ChartTags.Marker };
        }

        private static void Poly(List<PathSeg> segs, Vec2[] pts)
        {
            segs.Add(PathSeg.Move(pts[0]));
            for (var i = 1; i < pts.Length; i++) segs.Add(PathSeg.Line(pts[i]));
            segs.Add(PathSeg.Close);
        }

        /// <summary>Points along polylines every <paramref name="step"/> mm (used to place the legend away from the data).</summary>
        public static List<Vec2> Densify(IEnumerable<IReadOnlyList<Vec2>> lines, double step)
        {
            var result = new List<Vec2>();
            foreach (var l in lines)
            {
                for (var i = 0; i + 1 < l.Count; i++)
                {
                    var a = l[i]; var b = l[i + 1];
                    if (!Frame.Ok(a) || !Frame.Ok(b)) continue;
                    var n = Math.Max(1, Math.Min(200, (int)Math.Ceiling(a.DistanceTo(b) / Math.Max(step, 0.1))));
                    for (var k = 0; k < n; k++) result.Add(Vec2.Lerp(a, b, (double)k / n));
                }
                if (l.Count > 0 && Frame.Ok(l[l.Count - 1])) result.Add(l[l.Count - 1]);
            }
            return result;
        }

        /// <summary>Text with a white backing (labels on curves, operating point labels).</summary>
        public static void Label(List<Primitive> into, Vec2 at, string text, double fs, string tag, TextAnchor anchor = TextAnchor.Middle, bool backing = true)
        {
            var w = Fmt.Width(text, fs);
            var x0 = anchor switch { TextAnchor.Start => at.X, TextAnchor.End => at.X - w, _ => at.X - w / 2 };
            if (backing) into.Add(Prim.Rect(x0 - fs * 0.15, at.Y - fs * 0.6, w + fs * 0.3, fs * 1.2, S.FillOnly(Palette.White)) with { Tag = tag });
            into.Add(new TextPrim(at, text, new TextStyle { Size = fs, Anchor = anchor, Color = Palette.Ink }) { Tag = tag });
        }
    }
}
