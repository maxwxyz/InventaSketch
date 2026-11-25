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
    }

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

        private double Pad => 0.35 * Fs;
        private double TickLen => 0.45 * Fs;
        private double Gap => 0.3 * Fs;
        private double LegendRow => Fs * 1.3;
        private bool ShowLegend => Legend.Count > 0 && (LegendMode is "inside" or "right" || LegendMode == "auto" && Legend.Count >= 2);

        private static double MaxWidth(Axis a, Lang lang, double fs)
            => a.Ticks(lang).Where(t => t.Label is not null).Select(t => Fmt.Width(t.Label!, fs)).DefaultIfEmpty(0).Max();

        private double LegendWidth => Legend.Count == 0 ? 0 : Fs * 0.8 + Fs * 3.2 + Gap + Legend.Max(e => Fmt.Width(e.Text, Fs * 0.9)) + Fs * 0.6;

        /// <summary>Computes the plot area and the panel areas; reduces the font when the box is too small.</summary>
        public void Layout(Lang lang)
        {
            for (var attempt = 0; attempt < 6; attempt++)
            {
                double hw = W / 2, hh = H / 2;
                var yw = Panels.Max(p => MaxWidth(p.Y, lang, Fs));
                var left = Pad + (Panels.Any(p => !string.IsNullOrEmpty(p.YTitle)) ? Fs * 1.05 + Gap : 0) + yw + Gap + TickLen;
                var right = Pad;
                var y2 = Panels.Where(p => p.Y2 is not null).ToList();
                if (y2.Count > 0)
                    right += y2.Max(p => MaxWidth(p.Y2!, lang, Fs)) + Gap + TickLen + (y2.Any(p => !string.IsNullOrEmpty(p.Y2Title)) ? Fs * 1.05 + Gap : 0);
                var xt = X.Ticks(lang).Where(t => t.Label is not null && X.Kind != AxisKind.Category).ToList();
                if (xt.Count > 0)
                {
                    // The first and last x labels are centred on their ticks: keep them inside the box.
                    var firstT = X.T(xt[0].Value); var lastT = X.T(xt[xt.Count - 1].Value);
                    if (firstT < 0.02) left = Math.Max(left, Pad + Fmt.Width(xt[0].Label!, Fs) / 2);
                    if (lastT > 0.98 && y2.Count == 0) right = Math.Max(right, Pad + Fmt.Width(xt[xt.Count - 1].Label!, Fs) / 2);
                }
                if (Arrows) right = Math.Max(right, Pad + Fs);
                if (ShowLegend && LegendMode == "right") right += LegendWidth + Gap;
                var top = Pad + (string.IsNullOrEmpty(Title) ? Fs * 0.5 : Fs * 1.3 + Gap) + (Arrows ? Fs * 0.5 : 0);
                var bottom = Pad + TickLen + Gap + Fs + (string.IsNullOrEmpty(XTitle) ? 0 : Gap + Fs * 1.05);
                var pw = W - left - right;
                var ph = H - top - bottom;
                if ((pw < W * 0.3 || ph < H * 0.3) && attempt < 5 && Fs > 0.8) { Fs *= 0.8; continue; }
                pw = Math.Max(pw, W * 0.1); ph = Math.Max(ph, H * 0.1);
                Plot = new Rect(-hw + left, -hh + top, pw, ph);
                break;
            }
            var n = Panels.Count;
            var between = n > 1 ? Fs * 1.2 : 0;
            var each = (Plot.H - between * (n - 1)) / n;
            for (var i = 0; i < n; i++) Panels[i].Area = new Rect(Plot.X, Plot.Y + i * (each + between), Plot.W, Math.Max(each, 1));
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

        /// <summary>Grid lines behind the data (major and minor).</summary>
        public void DrawGrid(List<Primitive> into)
        {
            if (!Grid) return;
            var major = new List<IReadOnlyList<Vec2>>();
            var minor = new List<IReadOnlyList<Vec2>>();
            foreach (var p in Panels)
            {
                var a = p.Area;
                if (X.Kind != AxisKind.Category)
                    foreach (var t in X.Ticks(Lang.En))
                    {
                        if (t.Mark == TickMark.None) continue;
                        var x = a.X + X.T(t.Value) * a.W;
                        if (Math.Abs(x - a.X) < 0.01 || Math.Abs(x - a.Right) < 0.01 && Box) continue;
                        (t.IsMajor ? major : minor).Add(new[] { new Vec2(x, a.Y), new Vec2(x, a.Bottom) });
                    }
                if (p.Y.Kind != AxisKind.Category)
                    foreach (var t in p.Y.Ticks(Lang.En))
                    {
                        if (t.Mark == TickMark.None) continue;
                        var y = a.Bottom - p.Y.T(t.Value) * a.H;
                        if (Math.Abs(y - a.Bottom) < 0.01 || Math.Abs(y - a.Y) < 0.01 && Box) continue;
                        (t.IsMajor ? major : minor).Add(new[] { new Vec2(a.X, y), new Vec2(a.Right, y) });
                    }
            }
            if (Draw.MultiPath(minor, S.GridMinor(Mono), ChartTags.GridMinor) is { } mi) into.Add(mi);
            if (Draw.MultiPath(major, S.Grid(Mono), ChartTags.Grid) is { } ma) into.Add(ma);
        }

        /// <summary>Thin zero line where 0 lies strictly inside a linear value axis (bars, Bode magnitude, negative data).</summary>
        public void DrawZero(List<Primitive> into)
        {
            foreach (var p in Panels)
            {
                var a = p.Area;
                if (p.Y.Kind == AxisKind.Linear && p.Y.Min < 0 && p.Y.Max > 0)
                {
                    var y = a.Bottom - p.Y.T(0) * a.H;
                    into.Add(Prim.Line(new Vec2(a.X, y), new Vec2(a.Right, y), S.Zero) with { Tag = ChartTags.Zero });
                }
                if (X.Kind == AxisKind.Linear && X.Min < 0 && X.Max > 0)
                {
                    var x = a.X + X.T(0) * a.W;
                    into.Add(Prim.Line(new Vec2(x, a.Y), new Vec2(x, a.Bottom), S.Zero) with { Tag = ChartTags.Zero });
                }
            }
        }

        /// <summary>Axis lines, ticks, tick labels, axis titles and chart title (drawn above the data).</summary>
        public void DrawAxes(List<Primitive> into, Lang lang)
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
                    var arrow = Arrows ? axis with { ArrowEnd = ArrowHead.Triangle, ArrowSize = ArrowSize.Small } : axis;
                    var ext = Arrows ? Fs * 0.8 : 0;
                    into.Add(Prim.Line(new Vec2(a.X, a.Bottom), new Vec2(a.Right + ext, a.Bottom), arrow) with { Tag = ChartTags.Axis + ".x" + ps });
                    into.Add(Prim.Line(new Vec2(a.X, a.Bottom), new Vec2(a.X, a.Y - ext), arrow) with { Tag = ChartTags.Axis + ".y" + ps });
                    if (p.Y2 is not null) into.Add(Prim.Line(new Vec2(a.Right, a.Bottom), new Vec2(a.Right, a.Y), axis) with { Tag = ChartTags.Axis + ".y2" + ps });
                }

                // X ticks (every panel) and labels (bottom panel).
                var xl = new List<(double Pos, string Text)>();
                foreach (var t in X.Ticks(lang))
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

                YAxis(into, yticks, p.Y, a, a.X, -1, lang, labelColor, ChartTags.TickLabel + ".y" + ps);
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
                into.Add(new TextPrim(new Vec2(Plot.X + Plot.W / 2, hh - Pad - Fs * 0.5), XTitle!, new TextStyle { Size = fs, Color = labelColor }) { Tag = ChartTags.AxisTitle + ".x" });
            }
            if (!string.IsNullOrEmpty(Title))
            {
                var fs = Fit(Title!, Fs * 1.1, W - 2 * Pad);
                into.Add(new TextPrim(new Vec2(0, -hh + Pad + Fs * 0.6), Title!, new TextStyle { Size = fs, Bold = true, Color = labelColor }) { Tag = ChartTags.Title });
            }
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

        private void YAxis(List<Primitive> into, List<IReadOnlyList<Vec2>> ticks, Axis y, Rect a, double x0, int side, Lang lang, string color, string labelTag)
        {
            double hh = H / 2;
            var fs = Fs;
            if (y.Kind == AxisKind.Category)
            {
                var band = a.H / Math.Max(1, y.Categories.Count);
                fs = Math.Max(0.6, Math.Min(Fs, band * 0.8));
            }
            var lastY = double.PositiveInfinity;
            var list = y.Ticks(lang).Select(t => (t, Pos: a.Bottom - y.T(t.Value) * a.H)).OrderByDescending(x => x.Pos).ToList();
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
