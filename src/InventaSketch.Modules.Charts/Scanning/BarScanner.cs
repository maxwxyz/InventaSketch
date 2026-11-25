using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Charts.Scanning
{
    /// <summary>Direction of the bars: vertical (columns) or horizontal (bars).</summary>
    public enum BarOrientation
    {
        Columns,
        Bars,
    }

    /// <summary>What the user-given reference value of a <see cref="BarScale"/> refers to.</summary>
    public enum BarScaleReference
    {
        /// <summary>The value belongs to the largest bar (sum of its stack).</summary>
        LargestValue,

        /// <summary>The value is the distance of two gridlines.</summary>
        GridStep,
    }

    /// <summary>
    /// Scale from two user inputs (without text recognition): the value of the baseline and a reference value (largest bar or
    /// gridline distance).
    /// </summary>
    public sealed record BarScale(double BaselineValue = 0, BarScaleReference Reference = BarScaleReference.LargestValue, double ReferenceValue = 100);

    /// <summary>
    /// One recognised part of a bar: outline in the picture, colour, category, series and extent in pixels along the value
    /// axis (positive = from the baseline up or to the right). <see cref="AtBaseline"/>: the part starts at the baseline.
    /// </summary>
    public sealed record BarSegment(PixelRect Bounds, int Color, int Category, int Series, double Extent, bool AtBaseline);

    /// <summary>Result of the <see cref="BarScanner"/>: orientation, baseline, parts, gridlines, warnings.</summary>
    public sealed class BarScanResult
    {
        public static readonly BarScanResult Empty = new(BarOrientation.Columns, 0, Array.Empty<BarSegment>(), 0, 0, Array.Empty<double>(), null, false);

        public BarScanResult(BarOrientation orientation, double baseline, IReadOnlyList<BarSegment> segments, int categories, int series,
            IReadOnlyList<double> gridlines, double? gridStep, bool clustered, int background = 0, IReadOnlyList<I18nText>? warnings = null)
        {
            Background = background;
            Orientation = orientation;
            Baseline = baseline;
            Segments = segments;
            Categories = categories;
            Series = series;
            Gridlines = gridlines;
            GridStep = gridStep;
            Clustered = clustered;
            Warnings = warnings ?? Array.Empty<I18nText>();
        }

        public BarOrientation Orientation { get; }

        /// <summary>Background colour (ARGB), the reference for text recognition.</summary>
        public int Background { get; }

        /// <summary>Baseline in pixels: y for columns, x for bars.</summary>
        public double Baseline { get; }

        public IReadOnlyList<BarSegment> Segments { get; }

        public int Categories { get; }

        public int Series { get; }

        /// <summary>Gridlines (without the baseline) in pixels: y for columns, x for bars.</summary>
        public IReadOnlyList<double> Gridlines { get; }

        /// <summary>Regular gridline distance in pixels; null if none was found.</summary>
        public double? GridStep { get; }

        /// <summary>Clustered bars (series side by side) instead of stacked ones.</summary>
        public bool Clustered { get; }

        /// <summary>Why nothing (or only part) was found; empty when the scan went well.</summary>
        public IReadOnlyList<I18nText> Warnings { get; }

        public bool Found => Segments.Count > 0;

        /// <summary>Largest bar in pixels: sum of the parts of a stack on one side of the baseline.</summary>
        public double LargestExtent
        {
            get
            {
                double best = 0;
                foreach (var g in Segments.GroupBy(s => (s.Category, Clustered ? s.Series : -1)))
                {
                    best = Math.Max(best, g.Where(s => s.Extent > 0).Sum(s => s.Extent));
                    best = Math.Max(best, -g.Where(s => s.Extent < 0).Sum(s => s.Extent));
                }
                return best;
            }
        }

        internal static BarScanResult Failed(I18nText warning, int background = 0) =>
            new(BarOrientation.Columns, 0, Array.Empty<BarSegment>(), 0, 0, Array.Empty<double>(), null, false, background, new[] { warning });
    }

    /// <summary>
    /// Bar and column chart scanner: finds the baseline, the bars and their parts (stacked or clustered, also below the
    /// baseline) and the gridlines in a picture of a bar or column chart; pure image analysis without external services. The
    /// numbers follow from the extents in pixels with a <see cref="BarScale"/> (<see cref="Values"/>), or from the axis labels
    /// (<see cref="BarLabelReader"/>).
    /// Method: the background is the most frequent colour; connected areas of similar colour; thin pieces that together form a
    /// long line are axis or gridlines; the baseline is the edge where most areas end; from there the stacks are followed
    /// outwards.
    /// </summary>
    /// <remarks>Ported from the Slidemax chart scanner (<c>Import/Scanning/ChartScanner</c>), Apache License 2.0.</remarks>
    public static class BarScanner
    {
        /// <summary>Difference per channel up to which a pixel counts as background.</summary>
        public const int BackgroundTolerance = 20;

        /// <summary>Difference per channel within one area (anti-aliasing falls out).</summary>
        public const int SegmentTolerance = 24;

        /// <summary>Largest thickness of a line in pixels.</summary>
        private const int LineThickness = 3;

        /// <summary>Tolerance within which edges count as equal (pixels).</summary>
        private const int EdgeSlack = 2;

        /// <summary>Largest gap between two parts of a stack (separator lines).</summary>
        private const int StackGap = 3;

        private sealed class Blob
        {
            public int MinX = int.MaxValue, MinY = int.MaxValue, MaxX = -1, MaxY = -1, Count;
            public long R, G, B;
            public bool Line;

            public int Width => MaxX - MinX + 1;
            public int Height => MaxY - MinY + 1;
            public double Fill => Count / (double)(Width * Height);
            public int Color => PixelColor.Rgb((int)(R / Count), (int)(G / Count), (int)(B / Count));

            public void Add(int x, int y, int argb)
            {
                if (x < MinX) MinX = x;
                if (x > MaxX) MaxX = x;
                if (y < MinY) MinY = y;
                if (y > MaxY) MaxY = y;
                Count++;
                R += (argb >> 16) & 0xFF;
                G += (argb >> 8) & 0xFF;
                B += argb & 0xFF;
            }
        }

        /// <summary>Area in axis coordinates: category axis [C0, C1), value axis [V0, V1) (growing in positive direction).</summary>
        private sealed class Box
        {
            public Box(Blob blob, BarOrientation o)
            {
                Blob = blob;
                if (o == BarOrientation.Columns)
                {
                    C0 = blob.MinX;
                    C1 = blob.MaxX + 1;
                    V0 = -(blob.MaxY + 1);
                    V1 = -blob.MinY;
                }
                else
                {
                    C0 = blob.MinY;
                    C1 = blob.MaxY + 1;
                    V0 = blob.MinX;
                    V1 = blob.MaxX + 1;
                }
            }

            public Blob Blob { get; }
            public int C0 { get; }
            public int C1 { get; }
            public int V0 { get; }
            public int V1 { get; }
            public int Thickness => C1 - C0;
            public double Center => (C0 + C1) / 2.0;

            public int Overlap(Box o) => Math.Max(0, Math.Min(C1, o.C1) - Math.Max(C0, o.C0));
        }

        /// <summary>A bar: parts from the baseline outwards, up (or right) and down (or left).</summary>
        private sealed class Stack
        {
            public readonly List<Box> Up = new();
            public readonly List<Box> Down = new();
            public int C0 = int.MaxValue, C1 = int.MinValue;

            public double Center => (C0 + C1) / 2.0;
            public int Thickness => C1 - C0;

            public void Take(Box b)
            {
                C0 = Math.Min(C0, b.C0);
                C1 = Math.Max(C1, b.C1);
            }

            public int FirstColor => (Up.Count > 0 ? Up[0] : Down[0]).Blob.Color;
        }

        /// <summary>
        /// Analyses the picture. Never throws: empty, tiny or blank pictures and pictures without bars give a result without
        /// segments and with a warning.
        /// </summary>
        public static BarScanResult Scan(PixelImage image)
        {
            if (image is null || !image.IsValid)
                return BarScanResult.Failed(new I18nText("The picture is empty or its pixel data is incomplete.", "Das Bild ist leer oder seine Pixeldaten sind unvollständig."));
            if (image.Width < 8 || image.Height < 8)
                return BarScanResult.Failed(new I18nText("The picture is too small to find bars (at least 8 × 8 pixels).", "Das Bild ist zu klein, um Balken zu finden (mindestens 8 × 8 Pixel)."));
            var pixels = image.ToArgb();
            int background = Background(pixels);
            var blobs = Blobs(pixels, image.Width, image.Height, background);
            MarkLines(blobs, image.Width, image.Height);
            // Bars are filled rectangles (value labels inside them leave only small holes); letters of labels hardly fill
            // three quarters of their outline and would otherwise stand on their common bottom edge as bars.
            var candidates = blobs.Where(b => !b.Line && Math.Min(b.Width, b.Height) >= 2 && Math.Max(b.Width, b.Height) >= 3 && b.Fill >= 0.75).ToList();
            if (candidates.Count == 0) return NotFound(background);

            var columns = Analyze(candidates, BarOrientation.Columns);
            var bars = Analyze(candidates, BarOrientation.Bars);
            // If the bars of one orientation cover a multiple of the area of the other, that one wins (labels on one line
            // would otherwise give more, but tiny bars); else the orientation with more bars or more even widths.
            Analysis best;
            if (bars.Area > 3 * columns.Area) best = bars;
            else if (columns.Area > 3 * bars.Area) best = columns;
            else best = bars.Score > columns.Score || (bars.Score == columns.Score && bars.Score > 0 && bars.Spread < columns.Spread) ? bars : columns;
            if (best.Stacks.Count == 0) return NotFound(background);
            return Build(best, blobs, background);
        }

        private static BarScanResult NotFound(int background) =>
            BarScanResult.Failed(new I18nText("No column or bar chart recognised.", "Kein Säulen- oder Balkendiagramm erkannt."), background);

        // ------------------------------------------------------------------ Areas

        /// <summary>Most frequent colour (rounded to 5 bits per channel).</summary>
        internal static int Background(int[] pixels)
        {
            if (pixels.Length == 0) return PixelColor.White;
            var counts = new Dictionary<int, int>();
            var sample = new Dictionary<int, int>();
            foreach (int p in pixels)
            {
                int key = ((p >> 19) & 0x1F) << 10 | ((p >> 11) & 0x1F) << 5 | ((p >> 3) & 0x1F);
                counts.TryGetValue(key, out int n);
                counts[key] = n + 1;
                if (n == 0) sample[key] = p;
            }
            int bestKey = counts.OrderByDescending(p => p.Value).ThenBy(p => p.Key).First().Key;
            return sample[bestKey];
        }

        /// <summary>Connected areas of similar colour (horizontal and vertical neighbours), without the background.</summary>
        private static List<Blob> Blobs(int[] px, int w, int h, int background)
        {
            var seen = new bool[w * h];
            var pending = new int[w * h];
            var blobs = new List<Blob>();
            for (int i = 0; i < px.Length; i++)
            {
                if (seen[i]) continue;
                seen[i] = true;
                int seed = px[i];
                if (PixelColor.Distance(seed, background) <= BackgroundTolerance) continue;
                var blob = new Blob();
                int top = 0;
                pending[top++] = i;
                while (top > 0)
                {
                    int p = pending[--top];
                    int x = p % w, y = p / w;
                    blob.Add(x, y, px[p]);
                    if (x > 0) Visit(p - 1);
                    if (x < w - 1) Visit(p + 1);
                    if (y > 0) Visit(p - w);
                    if (y < h - 1) Visit(p + w);
                }
                blobs.Add(blob);

                void Visit(int j)
                {
                    if (seen[j] || PixelColor.Distance(px[j], seed) > SegmentTolerance || PixelColor.Distance(px[j], background) <= BackgroundTolerance) return;
                    seen[j] = true;
                    pending[top++] = j;
                }
            }
            return blobs;
        }

        /// <summary>
        /// Thin pieces that together cover at least a quarter of the picture width at the same height are lines (axis,
        /// gridlines, also dashed or interrupted by bars); vertical ones likewise.
        /// </summary>
        private static void MarkLines(List<Blob> blobs, int width, int height)
        {
            Mark(blobs.Where(b => b.Height <= LineThickness && b.Width >= 2 && b.Width >= b.Height && b.Fill >= 0.5).ToList(), b => (b.MinY + b.MaxY) / 2, b => (b.MinX, b.MaxX + 1), width);
            Mark(blobs.Where(b => b.Width <= LineThickness && b.Height >= 2 && b.Height >= b.Width && b.Fill >= 0.5).ToList(), b => (b.MinX + b.MaxX) / 2, b => (b.MinY, b.MaxY + 1), height);
        }

        private static void Mark(List<Blob> pieces, Func<Blob, int> position, Func<Blob, (int From, int To)> span, int length)
        {
            foreach (var group in Groups(pieces, position))
            {
                if (Coverage(group.Select(span)) >= length / 4.0)
                    foreach (var b in group) b.Line = true;
            }
        }

        /// <summary>Pieces grouped by position (neighbouring positions up to 1 pixel apart belong together).</summary>
        private static IEnumerable<List<Blob>> Groups(List<Blob> pieces, Func<Blob, int> position)
        {
            var sorted = pieces.OrderBy(position).ToList();
            var group = new List<Blob>();
            int last = int.MinValue;
            foreach (var b in sorted)
            {
                int p = position(b);
                if (group.Count > 0 && p - last > 1)
                {
                    yield return group;
                    group = new List<Blob>();
                }
                group.Add(b);
                last = p;
            }
            if (group.Count > 0) yield return group;
        }

        /// <summary>Length of the union of intervals.</summary>
        private static int Coverage(IEnumerable<(int From, int To)> spans)
        {
            int total = 0, end = int.MinValue;
            foreach (var (from, to) in spans.OrderBy(s => s.From))
            {
                int start = Math.Max(from, end);
                if (to > start) total += to - start;
                end = Math.Max(end, to);
            }
            return total;
        }

        // ------------------------------------------------------------------ Baseline and stacks

        private sealed class Analysis
        {
            public BarOrientation Orientation;
            public int Baseline;
            public int Score;
            public double Spread;

            /// <summary>Area of all parts of the bars in pixels.</summary>
            public long Area;
            public List<Stack> Stacks = new();
        }

        private static Analysis Analyze(List<Blob> candidates, BarOrientation orientation)
        {
            var boxes = candidates.Select(b => new Box(b, orientation)).ToList();
            var result = new Analysis { Orientation = orientation };
            // Count edges: per area its start and its end along the value axis.
            var edges = new Dictionary<int, HashSet<Box>>();
            foreach (var b in boxes)
            {
                Add(b.V0, b);
                Add(b.V1, b);
            }
            int bestEdge = 0, bestScore = 0;
            long bestWeight = 0;
            foreach (var e in edges.Keys.OrderBy(k => k))
            {
                var near = new HashSet<Box>();
                for (int d = -EdgeSlack; d <= EdgeSlack; d++)
                    if (edges.TryGetValue(e + d, out var set)) near.UnionWith(set);
                // Bars are counted, not areas: two parts of a stack touching at a separator line count once (otherwise the
                // border between two series wins for stacked bars).
                int score = Columns(near);
                // Equal count: the edge with the larger areas (bars instead of labels below the axis).
                long weight = near.Sum(b => (long)b.Thickness * (b.V1 - b.V0));
                if (score > bestScore || (score == bestScore && weight > bestWeight))
                {
                    bestScore = score;
                    bestWeight = weight;
                    bestEdge = e;
                }
            }
            if (bestScore == 0) return result;
            // Exact position: most frequent edge within the tolerance.
            var exact = boxes.SelectMany(b => new[] { b.V0, b.V1 }).Where(v => Math.Abs(v - bestEdge) <= EdgeSlack).GroupBy(v => v).OrderByDescending(g => g.Count()).ThenBy(g => g.Key).First().Key;
            result.Baseline = exact;

            var used = new HashSet<Box>();
            var starts = boxes.Where(b => Math.Abs(b.V0 - exact) <= EdgeSlack || Math.Abs(b.V1 - exact) <= EdgeSlack).OrderBy(b => b.C0).ToList();
            foreach (var b in starts)
            {
                if (used.Contains(b)) continue;
                bool up = Math.Abs(b.V0 - exact) <= EdgeSlack && (b.V0 + b.V1) / 2.0 >= exact;
                bool down = Math.Abs(b.V1 - exact) <= EdgeSlack && (b.V0 + b.V1) / 2.0 < exact;
                if (!up && !down) continue;
                // Does the area belong to a bar with parts on the other side of the baseline?
                var stack = result.Stacks.FirstOrDefault(s => Math.Max(0, Math.Min(s.C1, b.C1) - Math.Max(s.C0, b.C0)) >= 0.6 * Math.Min(s.Thickness, b.Thickness)
                    && (up ? s.Up.Count == 0 : s.Down.Count == 0));
                if (stack is null)
                {
                    stack = new Stack();
                    result.Stacks.Add(stack);
                }
                stack.Take(b);
                used.Add(b);
                var chain = up ? stack.Up : stack.Down;
                chain.Add(b);
                Grow(chain, boxes, used, up);
            }
            // Very narrow stacks (tick marks, characters at the baseline) are dropped.
            if (result.Stacks.Count > 0)
            {
                var thickness = result.Stacks.Select(s => (double)s.Thickness).OrderBy(t => t).ToList();
                double median = thickness[thickness.Count / 2];
                result.Stacks.RemoveAll(s => s.Thickness < Math.Max(3, 0.4 * median));
            }
            result.Stacks = result.Stacks.OrderBy(s => s.Center).ToList();
            result.Score = result.Stacks.Count;
            result.Area = result.Stacks.Sum(s => s.Up.Concat(s.Down).Sum(b => (long)b.Thickness * (b.V1 - b.V0)));
            if (result.Stacks.Count > 1)
            {
                var t = result.Stacks.Select(s => (double)s.Thickness).ToList();
                double mean = t.Average();
                result.Spread = Math.Sqrt(t.Sum(x => (x - mean) * (x - mean)) / t.Count) / Math.Max(1, mean);
            }
            return result;

            void Add(int edge, Box b)
            {
                if (!edges.TryGetValue(edge, out var set)) edges[edge] = set = new HashSet<Box>();
                set.Add(b);
            }
        }

        /// <summary>Number of bars among the areas: areas overlapping along the category axis belong to one.</summary>
        private static int Columns(IEnumerable<Box> boxes)
        {
            var groups = new List<(int C0, int C1)>();
            foreach (var b in boxes.OrderBy(b => b.C0))
            {
                int i = groups.FindIndex(g => Math.Min(g.C1, b.C1) - Math.Max(g.C0, b.C0) >= 0.6 * Math.Min(g.C1 - g.C0, b.Thickness));
                if (i < 0) groups.Add((b.C0, b.C1));
                else groups[i] = (Math.Min(groups[i].C0, b.C0), Math.Max(groups[i].C1, b.C1));
            }
            return groups.Count;
        }

        /// <summary>Follows a stack outwards: the next part starts at most <see cref="StackGap"/> pixels after the last one.</summary>
        private static void Grow(List<Box> chain, List<Box> boxes, HashSet<Box> used, bool up)
        {
            while (true)
            {
                var last = chain[chain.Count - 1];
                Box? next = null;
                int bestOverlap = 0;
                foreach (var b in boxes)
                {
                    if (used.Contains(b)) continue;
                    int gap = up ? b.V0 - last.V1 : last.V0 - b.V1;
                    if (gap < -1 || gap > StackGap) continue;
                    int overlap = last.Overlap(b);
                    if (overlap < 0.6 * Math.Min(last.Thickness, b.Thickness) || overlap <= bestOverlap) continue;
                    next = b;
                    bestOverlap = overlap;
                }
                if (next is null) return;
                used.Add(next);
                chain.Add(next);
            }
        }

        // ------------------------------------------------------------------ Result

        private static BarScanResult Build(Analysis a, List<Blob> blobs, int background)
        {
            var stacks = a.Stacks;
            // Clustered bars: bars that (almost) touch form one category; series side by side.
            var clusters = Clusters(stacks);
            bool clustered = clusters.Count < stacks.Count && clusters.Any(c => c.Count > 1)
                && clusters.All(c => c.Select(s => s.FirstColor).Distinct().Count() == c.Count || c.Count == 1)
                && clusters.Count > 1;
            var segments = new List<BarSegment>();
            int seriesCount;
            if (clustered)
            {
                seriesCount = clusters.Max(c => c.Count);
                for (int c = 0; c < clusters.Count; c++)
                    for (int s = 0; s < clusters[c].Count; s++)
                    {
                        var series = s;
                        AddChains(segments, a, clusters[c][s], c, _ => series);
                    }
            }
            else
            {
                // Series by colour; if a colour repeats within a bar, by position from the outside.
                var colors = Palette(stacks.SelectMany(s => s.Up.Concat(s.Down)).Select(b => b.Blob.Color));
                bool byColor = stacks.All(s => s.Up.Select(b => ColorIndex(colors, b.Blob.Color)).Distinct().Count() == s.Up.Count
                    && s.Down.Select(b => ColorIndex(colors, b.Blob.Color)).Distinct().Count() == s.Down.Count);
                Func<Stack, Box, int> seriesOf;
                if (byColor)
                {
                    // Order as in a data sheet: the outermost colour first (the first series lies on top of the stack).
                    var rank = new Dictionary<int, List<double>>();
                    foreach (var s in stacks)
                    {
                        for (int i = 0; i < s.Up.Count; i++) Rank(rank, ColorIndex(colors, s.Up[i].Blob.Color), s.Up.Count - 1 - i);
                        for (int i = 0; i < s.Down.Count; i++) Rank(rank, ColorIndex(colors, s.Down[i].Blob.Color), i + 1000);
                    }
                    var order = rank.OrderBy(p => p.Value.Average()).Select(p => p.Key).ToList();
                    seriesOf = (s, b) => order.IndexOf(ColorIndex(colors, b.Blob.Color));
                    seriesCount = order.Count;
                }
                else
                {
                    int maxUp = stacks.Max(s => s.Up.Count);
                    seriesOf = (s, b) => s.Up.Contains(b) ? maxUp - s.Up.Count + (s.Up.Count - 1 - s.Up.IndexOf(b)) : maxUp + s.Down.IndexOf(b);
                    seriesCount = maxUp + stacks.Max(s => s.Down.Count);
                }
                for (int c = 0; c < stacks.Count; c++)
                {
                    var stack = stacks[c];
                    AddChains(segments, a, stack, c, b => seriesOf(stack, b));
                }
            }
            var (gridlines, step) = Grid(a, blobs, stacks);
            double baseline = a.Orientation == BarOrientation.Columns ? -a.Baseline : a.Baseline;
            return new BarScanResult(a.Orientation, baseline, segments, clustered ? clusters.Count : stacks.Count, seriesCount, gridlines, step, clustered, background);
        }

        private static void Rank(Dictionary<int, List<double>> rank, int color, double position)
        {
            if (!rank.TryGetValue(color, out var list)) rank[color] = list = new List<double>();
            list.Add(position);
        }

        /// <summary>Parts of a bar with their extent: borders between two parts lie in the middle of their gap.</summary>
        private static void AddChains(List<BarSegment> segments, Analysis a, Stack stack, int category, Func<Box, int> series)
        {
            foreach (var (chain, sign) in new[] { (stack.Up, 1), (stack.Down, -1) })
            {
                double boundary = a.Baseline;
                for (int i = 0; i < chain.Count; i++)
                {
                    var b = chain[i];
                    double outer;
                    if (i + 1 < chain.Count)
                        outer = sign > 0 ? (b.V1 + chain[i + 1].V0) / 2.0 : (b.V0 + chain[i + 1].V1) / 2.0;
                    else outer = sign > 0 ? b.V1 : b.V0;
                    double extent = outer - boundary;
                    var blob = b.Blob;
                    segments.Add(new BarSegment(new PixelRect(blob.MinX, blob.MinY, blob.Width, blob.Height), blob.Color, category, series(b), extent, i == 0));
                    boundary = outer;
                }
            }
        }

        /// <summary>Bars whose distance is at most a fifth of the median width belong together.</summary>
        private static List<List<Stack>> Clusters(List<Stack> stacks)
        {
            var result = new List<List<Stack>>();
            if (stacks.Count == 0) return result;
            var widths = stacks.Select(s => (double)s.Thickness).OrderBy(t => t).ToList();
            double limit = Math.Max(1, widths[widths.Count / 2] * 0.2);
            var current = new List<Stack> { stacks[0] };
            for (int i = 1; i < stacks.Count; i++)
            {
                if (stacks[i].C0 - stacks[i - 1].C1 > limit)
                {
                    result.Add(current);
                    current = new List<Stack>();
                }
                current.Add(stacks[i]);
            }
            result.Add(current);
            return result;
        }

        /// <summary>Distinct colours (similar ones merged) in the order of their appearance.</summary>
        private static List<int> Palette(IEnumerable<int> colors)
        {
            var palette = new List<int>();
            foreach (int c in colors)
                if (!palette.Any(p => PixelColor.Distance(p, c) <= SegmentTolerance)) palette.Add(c);
            return palette;
        }

        private static int ColorIndex(List<int> palette, int color)
        {
            int best = 0, distance = int.MaxValue;
            for (int i = 0; i < palette.Count; i++)
            {
                int d = PixelColor.Distance(palette[i], color);
                if (d < distance)
                {
                    distance = d;
                    best = i;
                }
            }
            return best;
        }

        /// <summary>
        /// Gridlines across the value axis in the area of the bars (without the baseline) and their regular distance (baseline
        /// included, deviation at most 15 %).
        /// </summary>
        private static (IReadOnlyList<double> Lines, double? Step) Grid(Analysis a, List<Blob> blobs, List<Stack> stacks)
        {
            bool columns = a.Orientation == BarOrientation.Columns;
            var pieces = blobs.Where(b => b.Line && (columns ? b.Height <= LineThickness && b.Width >= b.Height : b.Width <= LineThickness && b.Height >= b.Width)).ToList();
            int c0 = stacks.Min(s => s.C0), c1 = stacks.Max(s => s.C1);
            var lines = new List<double>();
            foreach (var group in Groups(pieces, b => columns ? (b.MinY + b.MaxY) / 2 : (b.MinX + b.MaxX) / 2))
            {
                double position = group.Average(b => columns ? (b.MinY + b.MaxY + 1) / 2.0 : (b.MinX + b.MaxX + 1) / 2.0);
                double value = columns ? -position : position;
                if (Math.Abs(value - a.Baseline) <= EdgeSlack + 1) continue;
                int covered = Coverage(group.Select(b => columns ? (Math.Max(c0, b.MinX), Math.Min(c1, b.MaxX + 1)) : (Math.Max(c0, b.MinY), Math.Min(c1, b.MaxY + 1))));
                // Reference is the free width: bars reaching across the line hide it.
                int hidden = stacks.Where(s => s.Up.Concat(s.Down).Any(b => b.V0 <= value && value <= b.V1)).Sum(s => s.Thickness);
                double free = c1 - c0 - hidden;
                if (free >= 4 && covered < 0.2 * free) continue;
                lines.Add(position);
            }
            lines.Sort();
            double basePixel = columns ? -a.Baseline : a.Baseline;
            var all = lines.Concat(new[] { basePixel }).OrderBy(v => v).ToList();
            double? step = null;
            if (all.Count >= 2)
            {
                var gaps = all.Zip(all.Skip(1), (x, y) => y - x).ToList();
                var sorted = gaps.OrderBy(g => g).ToList();
                double median = sorted[sorted.Count / 2];
                if (median > 2 && gaps.All(g => Math.Abs(g - median) <= 0.15 * median)) step = median;
            }
            return (lines, step);
        }

        // ------------------------------------------------------------------ Numbers

        /// <summary>Value per pixel according to the scale; 1 if the reference is missing.</summary>
        public static double UnitsPerPixel(BarScanResult result, BarScale scale)
        {
            if (scale.Reference == BarScaleReference.GridStep && result.GridStep is double step && step > 0) return scale.ReferenceValue / step;
            double largest = result.LargestExtent;
            if (largest <= 0) return 1;
            double span = scale.ReferenceValue - scale.BaselineValue;
            return span == 0 ? 1 : Math.Abs(span) / largest;
        }

        /// <summary>Decimals that the resolution of one pixel supports.</summary>
        public static int Decimals(double unitsPerPixel)
        {
            if (unitsPerPixel <= 0 || double.IsNaN(unitsPerPixel) || double.IsInfinity(unitsPerPixel)) return 0;
            return Math.Max(0, Math.Min(6, (int)Math.Ceiling(-Math.Log10(unitsPerPixel) - 1e-9)));
        }

        /// <summary>
        /// Values per series (row) and category (column), as in a data sheet; null where no part was found. The part at the
        /// baseline starts at the baseline value (truncated axis), further parts of a stack add their height.
        /// </summary>
        public static double?[,] Values(BarScanResult result, BarScale scale)
        {
            var values = new double?[result.Series, result.Categories];
            double k = UnitsPerPixel(result, scale);
            int decimals = Decimals(k);
            foreach (var s in result.Segments)
            {
                if (s.Series < 0 || s.Series >= result.Series || s.Category < 0 || s.Category >= result.Categories) continue;
                double v = k * s.Extent + (s.AtBaseline ? scale.BaselineValue : 0);
                values[s.Series, s.Category] = Math.Round((values[s.Series, s.Category] ?? 0) + v, decimals, MidpointRounding.AwayFromZero);
            }
            return values;
        }

        /// <summary>Short description for a status line, e.g. "3 categories, 2 series, columns, stacked".</summary>
        public static I18nText Describe(BarScanResult result)
        {
            if (!result.Found) return new I18nText("No column or bar chart recognised.", "Kein Säulen- oder Balkendiagramm erkannt.");
            string kindEn = result.Clustered ? "clustered" : result.Series > 1 ? "stacked" : string.Empty;
            string kindDe = result.Clustered ? "gruppiert" : result.Series > 1 ? "gestapelt" : string.Empty;
            string orientationEn = result.Orientation == BarOrientation.Bars ? "bars" : "columns";
            string orientationDe = result.Orientation == BarOrientation.Bars ? "Balken" : "Säulen";
            string en = result.Categories + " categories, " + result.Series + " series, " + orientationEn;
            string de = result.Categories + " Kategorien, " + result.Series + " Serien, " + orientationDe;
            if (kindEn.Length > 0)
            {
                en += ", " + kindEn;
                de += ", " + kindDe;
            }
            if (result.GridStep is not null)
            {
                en += ", gridlines found";
                de += ", Gitternetz erkannt";
            }
            return new I18nText(en, de);
        }
    }
}
