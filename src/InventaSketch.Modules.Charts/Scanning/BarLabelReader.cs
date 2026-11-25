using System;
using System.Collections.Generic;
using System.Linq;

namespace InventaSketch.Modules.Charts.Scanning
{
    /// <summary>Where the scale of a bar scan comes from.</summary>
    public enum BarScaleSource
    {
        /// <summary>No suitable labels: the scale comes from the user's inputs.</summary>
        None,

        /// <summary>From the labels of the value axis.</summary>
        Axis,

        /// <summary>From value labels in or at the bars (charts without a value axis).</summary>
        DataLabels,
    }

    /// <summary>Result of reading the labels of a bar scan: scale, numbers used and category names.</summary>
    public sealed class BarLabels
    {
        public static readonly BarLabels Empty = new(BarScaleSource.None, null, Array.Empty<RecognizedText>(), Array.Empty<string?>(), Array.Empty<RecognizedText>());

        public BarLabels(BarScaleSource source, BarScale? scale, IReadOnlyList<RecognizedText> valueLabels, IReadOnlyList<string?> categories, IReadOnlyList<RecognizedText> categoryLabels)
        {
            Source = source;
            Scale = scale;
            ValueLabels = valueLabels;
            Categories = categories;
            CategoryLabels = categoryLabels;
        }

        public BarScaleSource Source { get; }

        /// <summary>Scale for <see cref="BarScanner.Values"/>; null without <see cref="Source"/>.</summary>
        public BarScale? Scale { get; }

        /// <summary>Numbers used for the scale (axis or value labels).</summary>
        public IReadOnlyList<RecognizedText> ValueLabels { get; }

        /// <summary>Name per category (null where none was read); empty without names.</summary>
        public IReadOnlyList<string?> Categories { get; }

        /// <summary>Words of the category names.</summary>
        public IReadOnlyList<RecognizedText> CategoryLabels { get; }

        public bool HasCategories => Categories.Any(c => !string.IsNullOrEmpty(c));
    }

    /// <summary>
    /// Evaluates the labels of a scanned bar chart: scale from the value axis (numbers in a column left or right of the
    /// columns, or in a row below or above the bars, whose position is linear in the value; robust line fit through pairs,
    /// outliers do not count), else from value labels in the parts or at the ends of the bars (ratio of value to extent).
    /// German and English number styles are both tried, the better fitting one wins. Category names are the words beyond the
    /// baseline in the first text line (with following lines when wrapped) per category.
    /// </summary>
    /// <remarks>Ported from the Slidemax chart scanner (<c>Import/Scanning/ScanLabels</c>), Apache License 2.0.</remarks>
    public static class BarLabelReader
    {
        /// <summary>Reads scale and category names; <see cref="BarLabels.Empty"/> without bars or words. Never throws.</summary>
        /// <param name="germanFirst">Try the German number style first (decides ties such as "1.000").</param>
        public static BarLabels Read(BarScanResult result, IReadOnlyList<RecognizedText> words, bool categories = true, bool germanFirst = true)
        {
            if (result is null || !result.Found || words is null || words.Count == 0) return BarLabels.Empty;
            var styles = germanFirst ? new[] { true, false } : new[] { false, true };
            LabelFit? best = null;
            foreach (bool german in styles)
            {
                var fit = AxisFit(result, words, german);
                if (fit is not null && (best is null || fit.Inliers.Count > best.Inliers.Count)) best = fit;
            }
            BarScaleSource source = BarScaleSource.None;
            BarScale? scale = null;
            var used = new List<RecognizedText>();
            if (best is not null)
            {
                source = BarScaleSource.Axis;
                double basePixel = result.Baseline;
                double baselineValue = best.Intercept + best.Slope * basePixel;
                double k = Math.Abs(best.Slope);
                // Round the baseline value to a tenth of the label step if that stays below 2 pixels (the centre of the text
                // often lies one pixel beside its line; the slope is not affected).
                var sorted = best.Values.OrderBy(v => v).ToList();
                var steps = sorted.Zip(sorted.Skip(1), (a, b) => b - a).Where(d => d > 0).ToList();
                if (steps.Count > 0)
                {
                    double grain = steps.Min() / 10;
                    double snapped = Math.Round(baselineValue / grain) * grain;
                    if (Math.Abs(snapped - baselineValue) <= 2 * k) baselineValue = snapped;
                }
                scale = ToScale(result, baselineValue, k, axis: true);
                used.AddRange(best.Inliers);
            }
            else
            {
                (double K, List<RecognizedText> Used)? data = null;
                foreach (bool german in styles)
                {
                    var d = DataFit(result, words, german);
                    if (d is not null && (data is null || d.Value.Used.Count > data.Value.Used.Count)) data = d;
                }
                if (data is not null)
                {
                    source = BarScaleSource.DataLabels;
                    scale = ToScale(result, 0, data.Value.K);
                    used.AddRange(data.Value.Used);
                }
            }
            var names = categories ? CategoryNames(result, words, used) : (Array.Empty<string?>(), new List<RecognizedText>());
            return new BarLabels(source, scale, used, names.Item1, names.Item2);
        }

        /// <summary>
        /// Scale for the scanner: baseline value and reference. From the axis always the largest bar (the measured gridline
        /// distance is less precise than the positions of the labels), from value labels the gridline distance if one was
        /// found.
        /// </summary>
        private static BarScale ToScale(BarScanResult result, double baselineValue, double unitsPerPixel, bool axis = false)
        {
            int decimals = BarScanner.Decimals(unitsPerPixel);
            baselineValue = Math.Round(baselineValue, Math.Min(15, decimals + 1), MidpointRounding.AwayFromZero);
            if (Math.Abs(baselineValue) < 0.5 * unitsPerPixel) baselineValue = 0;
            if (!axis && result.GridStep is double step && step > 0)
                return new BarScale(baselineValue, BarScaleReference.GridStep, Nice(unitsPerPixel * step, unitsPerPixel));
            return new BarScale(baselineValue, BarScaleReference.LargestValue, baselineValue + Nice(unitsPerPixel * result.LargestExtent, unitsPerPixel));
        }

        /// <summary>Rounds to the resolution of one pixel, preferring round values (gridline distance 20 instead of 19.97).</summary>
        internal static double Nice(double value, double unitsPerPixel)
        {
            int decimals = BarScanner.Decimals(unitsPerPixel);
            double rounded = Math.Round(value, decimals, MidpointRounding.AwayFromZero);
            // One digit coarser while that stays within half a pixel.
            for (int d = decimals - 1; d >= -6; d--)
            {
                double coarse = d >= 0 ? Math.Round(value, d, MidpointRounding.AwayFromZero) : Math.Round(value / Math.Pow(10, -d)) * Math.Pow(10, -d);
                if (Math.Abs(coarse - value) <= 0.5 * unitsPerPixel) rounded = coarse;
                else break;
            }
            return rounded;
        }

        // ------------------------------------------------------------------ Axis

        /// <summary>Area of the parts: [C0, C1] across, [V0, V1] along the value axis (picture coordinates).</summary>
        private static (int C0, int C1, int V0, int V1) Plot(BarScanResult r)
        {
            bool columns = r.Orientation == BarOrientation.Columns;
            var s = r.Segments;
            return columns
                ? (s.Min(x => x.Bounds.X), s.Max(x => x.Bounds.Right), s.Min(x => x.Bounds.Y), s.Max(x => x.Bounds.Bottom))
                : (s.Min(x => x.Bounds.Y), s.Max(x => x.Bounds.Bottom), s.Min(x => x.Bounds.X), s.Max(x => x.Bounds.Right));
        }

        private static LabelFit? AxisFit(BarScanResult result, IReadOnlyList<RecognizedText> words, bool german)
        {
            bool columns = result.Orientation == BarOrientation.Columns;
            var (c0, c1, _, _) = Plot(result);
            // Numbers outside the bars across the value axis: left and right (columns) or below and above (bars).
            var before = new List<LabelPoint>();
            var after = new List<LabelPoint>();
            foreach (var w in words)
            {
                if (LabelNumber.Parse(w.Text, german) is not double v) continue;
                int lo = columns ? w.Bounds.X : w.Bounds.Y, hi = columns ? w.Bounds.Right : w.Bounds.Bottom;
                var n = new LabelPoint(w, v, columns ? w.CenterY : w.CenterX);
                if (hi <= c0 + 1) before.Add(n);
                else if (lo >= c1 - 1) after.Add(n);
            }
            LabelFit? best = null;
            foreach (var side in new[] { before, after })
            {
                // Per side the column or row of words that overlap across the axis.
                foreach (var line in LabelLineFit.Lines(side, columns))
                {
                    var fit = LabelLineFit.Fit(line, columns ? -1 : +1);
                    if (fit is not null && (best is null || fit.Inliers.Count > best.Inliers.Count)) best = fit;
                }
            }
            return best;
        }

        // ------------------------------------------------------------------ Value labels

        /// <summary>
        /// Numbers inside a part (value of the part) or just beyond the end of a bar (sum of the bar on this side of the
        /// baseline); units per pixel as median of the ratios, at least two matching numbers and 60 % of all.
        /// </summary>
        private static (double K, List<RecognizedText> Used)? DataFit(BarScanResult result, IReadOnlyList<RecognizedText> words, bool german)
        {
            bool columns = result.Orientation == BarOrientation.Columns;
            var pairs = new List<(RecognizedText Word, double Ratio)>();
            var stacks = result.Segments.GroupBy(s => (s.Category, result.Clustered ? s.Series : -1, Math.Sign(s.Extent))).ToList();
            foreach (var w in words)
            {
                if (LabelNumber.Parse(w.Text, german) is not double v || v == 0) continue;
                double cx = w.CenterX, cy = w.CenterY;
                var inside = result.Segments.FirstOrDefault(s => cx > s.Bounds.X && cx < s.Bounds.Right && cy > s.Bounds.Y && cy < s.Bounds.Bottom);
                if (inside is not null)
                {
                    if (Math.Abs(inside.Extent) >= 8) pairs.Add((w, Math.Abs(v) / Math.Abs(inside.Extent)));
                    continue;
                }
                foreach (var stack in stacks)
                {
                    int lo = stack.Min(s => columns ? s.Bounds.X : s.Bounds.Y), hi = stack.Max(s => columns ? s.Bounds.Right : s.Bounds.Bottom);
                    double across = columns ? cx : cy;
                    if (across < lo || across > hi) continue;
                    bool positive = stack.Key.Item3 > 0;
                    double size = w.Bounds.Height;
                    double gap;
                    if (columns) gap = positive ? stack.Min(s => s.Bounds.Y) - w.Bounds.Bottom : w.Bounds.Y - stack.Max(s => s.Bounds.Bottom);
                    else gap = positive ? w.Bounds.X - stack.Max(s => s.Bounds.Right) : stack.Min(s => s.Bounds.X) - w.Bounds.Right;
                    if (gap < -1 || gap > 1.5 * size + 2) continue;
                    double extent = Math.Abs(stack.Sum(s => s.Extent));
                    if (extent >= 8) pairs.Add((w, Math.Abs(v) / extent));
                    break;
                }
            }
            if (pairs.Count < 2) return null;
            var ratios = pairs.Select(p => p.Ratio).OrderBy(r => r).ToList();
            double median = ratios[ratios.Count / 2];
            var inliers = pairs.Where(p => Math.Abs(p.Ratio - median) <= 0.06 * median).ToList();
            if (inliers.Count < 2 || inliers.Count < 0.6 * pairs.Count) return null;
            return (inliers.Average(p => p.Ratio), inliers.Select(p => p.Word).ToList());
        }

        // ------------------------------------------------------------------ Categories

        /// <summary>
        /// Category names: words beyond the baseline outside the parts (below the columns or left of the bars), without the
        /// numbers of the scale; the first line at most three text heights from the chart, following lines at a distance
        /// below one text height. Per category the words in its band (up to the middle to the neighbouring category).
        /// </summary>
        private static (IReadOnlyList<string?>, List<RecognizedText>) CategoryNames(BarScanResult result, IReadOnlyList<RecognizedText> words, List<RecognizedText> used)
        {
            bool columns = result.Orientation == BarOrientation.Columns;
            int n = result.Categories;
            if (n == 0) return (Array.Empty<string?>(), new List<RecognizedText>());
            var spans = Enumerable.Range(0, n).Select(c =>
            {
                var parts = result.Segments.Where(s => s.Category == c).ToList();
                return parts.Count == 0 ? ((double, double)?)null
                    : (columns ? parts.Min(s => s.Bounds.X) : parts.Min(s => s.Bounds.Y), columns ? parts.Max(s => s.Bounds.Right) : parts.Max(s => s.Bounds.Bottom));
            }).ToList();
            if (spans.Any(s => s is null)) return (Array.Empty<string?>(), new List<RecognizedText>());
            var bands = new (double Lo, double Hi)[n];
            for (int c = 0; c < n; c++)
            {
                var (lo, hi) = spans[c]!.Value;
                double left = c > 0 ? (spans[c - 1]!.Value.Item2 + lo) / 2 : lo - (n > 1 ? (spans[1]!.Value.Item1 - hi) / 2 : (hi - lo) / 2);
                double right = c + 1 < n ? (hi + spans[c + 1]!.Value.Item1) / 2 : hi + (n > 1 ? (lo - spans[c - 1]!.Value.Item2) / 2 : (hi - lo) / 2);
                bands[c] = (Math.Min(left, lo), Math.Max(right, hi));
            }
            var (_, _, v0, v1) = Plot(result);
            // Candidates: beyond the baseline, not overlapping parts, not used for the scale.
            var candidates = words.Where(w => !used.Contains(w)
                && !result.Segments.Any(s => Overlaps(s.Bounds, w.Bounds))
                && (columns ? w.CenterY > result.Baseline : w.CenterX < result.Baseline)).ToList();
            if (columns)
            {
                // Lines from the top; the first line below the lowest edge of the parts or the baseline.
                double edge = Math.Max(result.Baseline, v1);
                var lines = TextLines(candidates.Where(w => w.Bounds.Y >= edge - 2).ToList());
                var chosen = new List<RecognizedText>();
                double last = edge;
                foreach (var line in lines)
                {
                    double height = line.Average(w => (double)w.Bounds.Height);
                    double top = line.Min(w => w.Bounds.Y);
                    double limit = chosen.Count == 0 ? 3 * height + 4 : 0.9 * height + 2;
                    if (top - last > limit) break;
                    chosen.AddRange(line);
                    last = line.Max(w => w.Bounds.Bottom);
                }
                return Assign(chosen, bands, w => w.CenterX, n);
            }
            else
            {
                double edge = Math.Min(result.Baseline, v0);
                var near = candidates.Where(w => w.Bounds.Right <= edge + 2).ToList();
                if (near.Count == 0) return (new string?[n], new List<RecognizedText>());
                // Only words near the bars: right edge at most a few text heights from the word nearest to the chart.
                double nearest = near.Max(w => w.Bounds.Right);
                double height = near.Average(w => (double)w.Bounds.Height);
                var chosen = near.Where(w => w.Bounds.Right >= nearest - 12 * height).ToList();
                return Assign(chosen, bands, w => w.CenterY, n);
            }
        }

        private static (IReadOnlyList<string?>, List<RecognizedText>) Assign(List<RecognizedText> chosen, (double Lo, double Hi)[] bands, Func<RecognizedText, double> along, int n)
        {
            var names = new string?[n];
            var labels = new List<RecognizedText>();
            for (int c = 0; c < n; c++)
            {
                var inBand = chosen.Where(w => along(w) >= bands[c].Lo && along(w) < bands[c].Hi).ToList();
                if (inBand.Count == 0) continue;
                labels.AddRange(inBand);
                names[c] = string.Join(" ", TextLines(inBand).Select(l => string.Join(" ", l.OrderBy(w => w.Bounds.X).Select(w => w.Text.Trim()))));
            }
            return (names, labels);
        }

        /// <summary>Words to lines (centre within half the height), from top to bottom.</summary>
        internal static List<List<RecognizedText>> TextLines(List<RecognizedText> words)
        {
            var lines = new List<List<RecognizedText>>();
            foreach (var w in words.OrderBy(w => w.CenterY))
            {
                var line = lines.FirstOrDefault(l => Math.Abs(l.Average(x => x.CenterY) - w.CenterY) <= 0.5 * Math.Max(w.Bounds.Height, l.Max(x => x.Bounds.Height)));
                if (line is null) lines.Add(new List<RecognizedText> { w });
                else line.Add(w);
            }
            return lines.OrderBy(l => l.Min(w => w.Bounds.Y)).ToList();
        }

        private static bool Overlaps(PixelRect a, PixelRect b)
            => Math.Min(a.Right, b.Right) - Math.Max(a.X, b.X) > 0 && Math.Min(a.Bottom, b.Bottom) - Math.Max(a.Y, b.Y) > 0;
    }
}
