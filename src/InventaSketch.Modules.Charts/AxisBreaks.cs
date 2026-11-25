using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Charts
{
    /// <summary>
    /// A break of an axis: the values <see cref="Lo"/> … <see cref="Hi"/> are compressed into <see cref="Gap"/> (length along the
    /// axis in mm, the band that carries the break marker). Values inside the break map linearly into the gap.
    /// </summary>
    public readonly record struct AxisBreak(double Lo, double Hi, double Gap);

    /// <summary>Marker drawn at an axis break.</summary>
    public enum BreakMarkerStyle
    {
        /// <summary>Two parallel zigzag lines (Slidemax "wave").</summary>
        Zigzag,
        /// <summary>Two parallel oblique lines, rising 15° (double slash, Slidemax straight band).</summary>
        Slash,
    }

    /// <summary>
    /// Request for an automatic break (see <see cref="AxisBreaks.Spans"/>): a break near the value <see cref="At"/>, optionally
    /// with the total length <see cref="Extent"/> (mm) of the compressed gap region; without it the region is just the marker band.
    /// </summary>
    public readonly record struct AxisBreakRequest(double At, double? Extent = null, BreakMarkerStyle Style = BreakMarkerStyle.Zigzag);

    /// <summary>
    /// Result of <see cref="AxisBreaks.Spans"/>: request <see cref="Index"/> compresses the values Lo … Hi into the band
    /// (<see cref="Gap"/> mm); the free region of the data around it is <see cref="Height"/> mm long in total.
    /// </summary>
    public sealed record AxisBreakSpan(int Index, double Lo, double Hi, double Gap, double Height)
    {
        public AxisBreak ToBreak() => new(Lo, Hi, Gap);
    }

    /// <summary>
    /// Geometry of a break marker in page coordinates: the two marker lines and the closed band between them (to be filled with
    /// the background colour so the axis and curves appear interrupted).
    /// </summary>
    public sealed record BreakMarker(BreakMarkerStyle Style, IReadOnlyList<Vec2> Line1, IReadOnlyList<Vec2> Line2, IReadOnlyList<Vec2> Band)
    {
        public IEnumerable<Vec2> AllPoints => Line1.Concat(Line2).Concat(Band);
    }

    /// <summary>
    /// One axis with breaks: value ↔ distance from the minimum along the axis. Outside the breaks all values share one scale
    /// (<see cref="UnitLength"/>); a break takes its <see cref="AxisBreak.Gap"/>. Works for any axis (x or y, also reversed –
    /// see <see cref="Point"/>) with continuous values; linear axes only.
    /// </summary>
    /// <remarks>Ported from Slidemax <c>ChartGeometry.Layout.Along/ValueAlong/ScaleAlong</c> (bar charts), generalised.</remarks>
    public sealed class BrokenAxis
    {
        /// <param name="min">Value at the start of the axis.</param>
        /// <param name="max">Value at the end of the axis (&gt; min).</param>
        /// <param name="length">Length of the axis in mm.</param>
        /// <param name="breaks">Breaks; clipped to [min, max], sorted, overlapping ones merged (largest gap wins), empty ones dropped.</param>
        public BrokenAxis(double min, double max, double length, IEnumerable<AxisBreak>? breaks = null)
        {
            if (!(max > min)) throw new ArgumentException("The axis needs max > min.", nameof(max));
            if (!(length > 0)) throw new ArgumentException("The axis needs a positive length.", nameof(length));
            Min = min; Max = max; Length = length;
            var list = new List<AxisBreak>();
            foreach (var b in (breaks ?? Enumerable.Empty<AxisBreak>()).Select(b => b.Lo <= b.Hi ? b : new AxisBreak(b.Hi, b.Lo, b.Gap)).OrderBy(b => b.Lo))
            {
                double lo = Math.Max(min, b.Lo), hi = Math.Min(max, b.Hi), gap = Math.Max(0, double.IsNaN(b.Gap) ? 0 : b.Gap);
                if (!(hi - lo > 1e-12 * (max - min))) continue;
                if (list.Count > 0 && lo <= list[list.Count - 1].Hi)
                {
                    var prev = list[list.Count - 1];
                    list[list.Count - 1] = new AxisBreak(prev.Lo, Math.Max(prev.Hi, hi), Math.Max(prev.Gap, gap));
                }
                else list.Add(new AxisBreak(lo, hi, gap));
            }
            Breaks = list;
            double span = max - min - list.Sum(b => b.Hi - b.Lo), gaps = list.Sum(b => b.Gap);
            UnitLength = Math.Max(1e-9, length - gaps) / Math.Max(1e-9 * (max - min), span);
        }

        public double Min { get; }
        public double Max { get; }
        public double Length { get; }
        /// <summary>Normalised breaks, ascending.</summary>
        public IReadOnlyList<AxisBreak> Breaks { get; }
        /// <summary>Length (mm) per value unit outside the breaks.</summary>
        public double UnitLength { get; }

        /// <summary>Distance (mm) of <paramref name="value"/> from the axis start; linear extrapolation outside [Min, Max].</summary>
        public double Position(double value)
        {
            double k = UnitLength, pos = 0, prev = Min;
            foreach (var b in Breaks)
            {
                if (value <= b.Lo) break;
                pos += (b.Lo - prev) * k;
                if (value < b.Hi) return pos + b.Gap * (value - b.Lo) / (b.Hi - b.Lo);
                pos += b.Gap;
                prev = b.Hi;
            }
            return pos + (value - prev) * k;
        }

        /// <summary>Value at the distance <paramref name="position"/> from the axis start (inverse of <see cref="Position"/>).</summary>
        public double Value(double position)
        {
            double k = UnitLength, prev = Min, at = 0;
            foreach (var b in Breaks)
            {
                var below = (b.Lo - prev) * k;
                if (position <= at + below) return prev + (position - at) / k;
                at += below;
                if (position <= at + b.Gap) return b.Gap <= 0 ? b.Lo : b.Lo + (position - at) / b.Gap * (b.Hi - b.Lo);
                at += b.Gap;
                prev = b.Hi;
            }
            return prev + (position - at) / k;
        }

        /// <summary>Normalised position 0 … 1 of a value.</summary>
        public double T(double value) => Position(value) / Length;

        /// <summary>Index of the break that hides <paramref name="value"/> (strictly inside Lo … Hi); −1 if the value is visible.</summary>
        public int BreakIndexOf(double value)
        {
            for (var i = 0; i < Breaks.Count; i++)
                if (value > Breaks[i].Lo && value < Breaks[i].Hi) return i;
            return -1;
        }

        public bool IsVisible(double value) => BreakIndexOf(value) < 0;

        /// <summary>Visible value ranges between the breaks, ascending.</summary>
        public IReadOnlyList<(double Lo, double Hi)> VisibleRanges
        {
            get
            {
                var list = new List<(double, double)>();
                var prev = Min;
                foreach (var b in Breaks)
                {
                    if (b.Lo > prev) list.Add((prev, b.Lo));
                    prev = b.Hi;
                }
                if (Max > prev) list.Add((prev, Max));
                return list;
            }
        }

        /// <summary>Start and end distance (mm) of the band of break <paramref name="index"/>.</summary>
        public (double From, double To) GapPositions(int index)
        {
            var b = Breaks[index];
            return (Position(b.Lo), Position(b.Hi));
        }

        /// <summary>Page point of a value on the axis line from <paramref name="start"/> (Min) to <paramref name="end"/> (Max).</summary>
        public Vec2 Point(double value, Vec2 start, Vec2 end) => Vec2.Lerp(start, end, T(value));

        /// <summary>Value at the page point <paramref name="p"/> projected onto the axis line from <paramref name="start"/> to <paramref name="end"/>.</summary>
        public double ValueAt(Vec2 p, Vec2 start, Vec2 end)
        {
            var d = end - start;
            var l2 = d.Dot(d);
            var t = l2 < Geo.Eps ? 0 : (p - start).Dot(d) / l2;
            return Value(t * Length);
        }
    }

    /// <summary>
    /// XY plot area with optionally broken x and y axes: data ↔ page mapping (y up in data, down on the page), visible parts of
    /// polylines and the break markers.
    /// </summary>
    public sealed class BrokenPlot
    {
        public BrokenPlot(Rect plot, double xMin, double xMax, IEnumerable<AxisBreak>? xBreaks, double yMin, double yMax, IEnumerable<AxisBreak>? yBreaks)
        {
            Plot = plot;
            X = new BrokenAxis(xMin, xMax, plot.W, xBreaks);
            Y = new BrokenAxis(yMin, yMax, plot.H, yBreaks);
        }

        public Rect Plot { get; }
        public BrokenAxis X { get; }
        public BrokenAxis Y { get; }

        public Vec2 ToPage(Vec2 data) => new(Plot.X + X.Position(data.X), Plot.Bottom - Y.Position(data.Y));

        public Vec2 ToData(Vec2 page) => new(X.Value(page.X - Plot.X), Y.Value(Plot.Bottom - page.Y));

        /// <summary>True if neither coordinate lies inside a break.</summary>
        public bool IsVisible(Vec2 data) => X.IsVisible(data.X) && Y.IsVisible(data.Y);

        /// <summary>
        /// The parts of a data polyline outside all breaks (cut exactly at the break limits), in data coordinates. A curve that runs
        /// through a break is interrupted there; map the runs with <see cref="ToPage"/> to draw them.
        /// </summary>
        public IReadOnlyList<IReadOnlyList<Vec2>> VisibleRuns(IReadOnlyList<Vec2> polyline)
        {
            var runs = new List<IReadOnlyList<Vec2>>();
            var run = new List<Vec2>();
            void Close()
            {
                if (run.Count > 0) runs.Add(run);
                run = new List<Vec2>();
            }
            void Add(Vec2 p)
            {
                if (run.Count == 0 || run[run.Count - 1] != p) run.Add(p);
            }
            if (polyline.Count == 1)
            {
                if (IsVisible(polyline[0])) runs.Add(new[] { polyline[0] });
                return runs;
            }
            for (var i = 0; i + 1 < polyline.Count; i++)
            {
                Vec2 p = polyline[i], q = polyline[i + 1];
                var ts = new List<double> { 0, 1 };
                void Cuts(double a, double b, BrokenAxis axis)
                {
                    if (a == b) return;
                    foreach (var br in axis.Breaks)
                        foreach (var limit in new[] { br.Lo, br.Hi })
                        {
                            var t = (limit - a) / (b - a);
                            if (t > 0 && t < 1) ts.Add(t);
                        }
                }
                Cuts(p.X, q.X, X);
                Cuts(p.Y, q.Y, Y);
                ts.Sort();
                for (var k = 0; k + 1 < ts.Count; k++)
                {
                    if (ts[k + 1] - ts[k] <= 1e-12) continue;
                    var mid = Vec2.Lerp(p, q, (ts[k] + ts[k + 1]) / 2);
                    if (IsVisible(mid))
                    {
                        Add(Vec2.Lerp(p, q, ts[k]));
                        Add(ts[k + 1] == 1 ? q : Vec2.Lerp(p, q, ts[k + 1]));
                    }
                    else Close();
                }
            }
            Close();
            return runs;
        }

        /// <summary>
        /// Markers of all breaks. <paramref name="acrossPlot"/>: the marker runs across the whole plot (Slidemax line and area
        /// charts); otherwise a short marker of <see cref="AxisBreaks.AxisMarkerWidth"/> centred on the axis line (y axis at the
        /// left, x axis at the bottom of the plot).
        /// </summary>
        public IReadOnlyList<BreakMarker> Markers(BreakMarkerStyle style, bool acrossPlot)
        {
            var list = new List<BreakMarker>();
            for (var i = 0; i < Y.Breaks.Count; i++)
            {
                var (a, b) = Y.GapPositions(i);
                var y = Plot.Bottom - (a + b) / 2;
                double x0 = acrossPlot ? Plot.X : Plot.X - AxisBreaks.AxisMarkerWidth / 2, x1 = acrossPlot ? Plot.Right : Plot.X + AxisBreaks.AxisMarkerWidth / 2;
                list.Add(AxisBreaks.Marker(style, new Vec2(x0, y), new Vec2(x1, y), AxisBreaks.BreakThickness));
            }
            for (var i = 0; i < X.Breaks.Count; i++)
            {
                var (a, b) = X.GapPositions(i);
                var x = Plot.X + (a + b) / 2;
                double y0 = acrossPlot ? Plot.Bottom : Plot.Bottom + AxisBreaks.AxisMarkerWidth / 2, y1 = acrossPlot ? Plot.Y : Plot.Bottom - AxisBreaks.AxisMarkerWidth / 2;
                list.Add(AxisBreaks.Marker(style, new Vec2(x, y0), new Vec2(x, y1), AxisBreaks.BreakThickness));
            }
            return list;
        }
    }

    /// <summary>
    /// Axis break helpers (charts v2, stage 3): automatic placement of breaks in data-free gaps, marker geometry and the sizes
    /// measured in Slidemax (converted from pt to mm).
    /// </summary>
    /// <remarks>Ported from Slidemax <c>ChartGeometry.AxisBreakSpans</c> and <c>ZigzagPoints</c>, generalised to any axis direction.</remarks>
    public static class AxisBreaks
    {
        private const double MmPerPt = 25.4 / 72;

        /// <summary>Distance of the two marker lines across the band: 4.5 pt (Slidemax B247).</summary>
        public const double BreakThickness = 4.5 * MmPerPt;
        /// <summary>Zigzag tooth length 6.5 pt and height 1.75 pt (Slidemax B247).</summary>
        public const double WiggleStep = 6.5 * MmPerPt, WiggleAmplitude = 1.75 * MmPerPt;
        /// <summary>Margin before and after the band inside the smallest gap: 2 pt.</summary>
        public const double BreakMargin = 2 * MmPerPt;
        /// <summary>Tilt of the slash lines against the cross direction: 15°.</summary>
        public const double SlashAngleDegrees = 15;
        /// <summary>Length of a short marker on the axis line: 11.5 pt.</summary>
        public const double AxisMarkerWidth = 11.5 * MmPerPt;

        /// <summary>Smallest gap (mm along the axis) a marker of the given cross length fits in, margins included.</summary>
        public static double RequiredGap(BreakMarkerStyle style, double crossLength) => style == BreakMarkerStyle.Zigzag
            ? BreakThickness + WiggleAmplitude + 2 * BreakMargin
            : BreakThickness + Math.Tan(SlashAngleDegrees * Math.PI / 180) * Math.Max(0, crossLength) + 2 * BreakMargin;

        public static I18nText Name(BreakMarkerStyle style) => style == BreakMarkerStyle.Zigzag
            ? new I18nText("Zigzag", "Zickzack")
            : new I18nText("Double slash", "Doppelter Schrägstrich");

        /// <summary>
        /// Zigzag from <paramref name="from"/> towards <paramref name="to"/>: whole teeth of <paramref name="step"/>, centred, starting
        /// on the +½ amplitude side (perpendicular <c>(to − from).Perp</c>). Unlike Slidemax (rounded tooth count) the count is
        /// rounded down, and the step shrinks for short lines, so every point stays between the ends.
        /// </summary>
        public static IReadOnlyList<Vec2> ZigzagPoints(Vec2 from, Vec2 to, double step = WiggleStep, double amplitude = WiggleAmplitude)
        {
            var d = to - from;
            var len = d.Length;
            if (len < Geo.Eps) return new[] { from, to };
            var c = d / len;
            var n = c.Perp;
            var count = Math.Max(2, (int)Math.Floor(len / step));
            if (count * step > len) step = len / count;
            var start = (len - count * step) / 2;
            var pts = new List<Vec2>(count + 1);
            for (var k = 0; k <= count; k++)
                pts.Add(from + c * (start + k * step) + n * (k % 2 == 0 ? amplitude / 2 : -amplitude / 2));
            return pts;
        }

        /// <summary>
        /// Marker across an axis along the line <paramref name="from"/> → <paramref name="to"/> (its centre line). The two lines lie
        /// ±<paramref name="thickness"/>/2 apart along the axis; slash lines are tilted by <see cref="SlashAngleDegrees"/> so that the
        /// band rises to the right for a left-to-right cross line on the page (y down).
        /// </summary>
        public static BreakMarker Marker(BreakMarkerStyle style, Vec2 from, Vec2 to, double thickness = BreakThickness)
        {
            var d = to - from;
            var n = d.Length < Geo.Eps ? new Vec2(0, 1) : d.Normalized().Perp;
            if (style == BreakMarkerStyle.Zigzag)
            {
                var upper = ZigzagPoints(from - n * (thickness / 2), to - n * (thickness / 2));
                var lower = ZigzagPoints(from + n * (thickness / 2), to + n * (thickness / 2));
                return new BreakMarker(style, upper, lower, upper.Concat(lower.Reverse()).ToList());
            }
            var rise = Math.Tan(SlashAngleDegrees * Math.PI / 180) * d.Length;
            Vec2 a0 = from + n * (rise / 2 - thickness / 2), a1 = to + n * (-rise / 2 - thickness / 2);
            Vec2 b0 = from + n * (rise / 2 + thickness / 2), b1 = to + n * (-rise / 2 + thickness / 2);
            return new BreakMarker(style, new[] { a0, a1 }, new[] { b0, b1 }, new[] { b0, b1, a1, a0 });
        }

        /// <summary>
        /// Places breaks in data-free gaps: each request at value At lies in the gap between the nearest <paramref name="keep"/>
        /// values (data edges, value lines; the axis ends and 0 are always kept) below and above it. The gap is compressed to its
        /// height (request Extent, at least the marker band <see cref="RequiredGap"/> for <paramref name="crossLength"/>); the band
        /// takes the compressed middle part, the rest of the gap keeps the scale of the chart. If the own height does not fit any
        /// more (small chart) the gap is compressed fully (<paramref name="shrinkToFit"/>); a break is dropped only when the
        /// uncompressed gap would hardly be longer than the band (≤ 1.2 ×). Requests in the same gap count once.
        /// </summary>
        /// <param name="min">Axis start value.</param>
        /// <param name="max">Axis end value.</param>
        /// <param name="axisLength">Axis length in mm.</param>
        /// <param name="keep">Values that must stay visible (bar edges, data minima/maxima, value lines …).</param>
        /// <param name="requests">The requested breaks; <see cref="AxisBreakSpan.Index"/> refers to this list.</param>
        /// <param name="crossLength">Length of a slash marker across the axis (mm), for its band.</param>
        /// <param name="shrinkToFit">Compress fully instead of dropping a break whose own height does not fit.</param>
        public static IReadOnlyList<AxisBreakSpan> Spans(double min, double max, double axisLength, IEnumerable<double> keep,
            IReadOnlyList<AxisBreakRequest> requests, double crossLength = 0, bool shrinkToFit = true)
        {
            if (requests.Count == 0 || max - min < 1e-12) return Array.Empty<AxisBreakSpan>();
            var edges = new List<double> { 0, min, max };
            edges.AddRange(keep);
            var sorted = edges.Where(v => v >= min - 1e-9 && v <= max + 1e-9).Select(v => Math.Round(v, 9)).Distinct().OrderBy(v => v).ToList();
            var gaps = new List<(int Index, double Lo, double Hi, double Height, double Band)>();
            for (var i = 0; i < requests.Count; i++)
            {
                var at = requests[i].At;
                var upper = sorted.FindIndex(v => v > at + 1e-9);
                if (upper <= 0) continue;
                double lo = sorted[upper - 1], hi = sorted[upper];
                if (gaps.Any(g => Math.Abs(g.Lo - lo) < 1e-9)) continue;
                var band = RequiredGap(requests[i].Style, crossLength);
                gaps.Add((i, lo, hi, Math.Max(band, requests[i].Extent ?? band), band));
            }
            // The scale outside the gaps depends on all breaks: remove breaks without effect until all remaining ones hold.
            while (gaps.Count > 0)
            {
                var k = (axisLength - gaps.Sum(g => g.Height)) / Math.Max(1e-9, max - min - gaps.Sum(g => g.Hi - g.Lo));
                var spans = new List<AxisBreakSpan>();
                var weak = new List<int>();
                foreach (var g in gaps)
                {
                    var width = k <= 1e-9 ? 0 : g.Hi - g.Lo - (g.Height - g.Band) / k;
                    if (width * k <= g.Band * 1.2)
                    {
                        weak.Add(g.Index);
                        continue;
                    }
                    var lo = g.Lo + (g.Hi - g.Lo - width) / 2;
                    spans.Add(new AxisBreakSpan(g.Index, lo, lo + width, g.Band, g.Height));
                }
                if (weak.Count == 0) return spans.OrderBy(b => b.Lo).ToList();
                var relaxed = false;
                for (var j = 0; j < gaps.Count; j++)
                    if (shrinkToFit && weak.Contains(gaps[j].Index) && gaps[j].Height > gaps[j].Band + 1e-9)
                    {
                        gaps[j] = (gaps[j].Index, gaps[j].Lo, gaps[j].Hi, gaps[j].Band, gaps[j].Band);
                        relaxed = true;
                    }
                if (!relaxed) gaps.RemoveAll(g => weak.Contains(g.Index));
            }
            return Array.Empty<AxisBreakSpan>();
        }
    }
}
