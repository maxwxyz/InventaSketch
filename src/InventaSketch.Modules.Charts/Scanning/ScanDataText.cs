using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using InventaSketch.Core;

namespace InventaSketch.Modules.Charts.Scanning
{
    /// <summary>
    /// Converts scan results to the chart data text of the charts module (<see cref="ChartData"/>): curves become XY blocks
    /// ("x;y" per row, a blank line between series) for <c>chart.xy</c>, bars a category table ("label;v1;v2" with a header
    /// row) for <c>chart.bar</c>. Numbers are written in invariant culture with the decimals the pixel resolution supports;
    /// comment lines (<c>#</c>) carry style, colour and calibration hints and are ignored by the parser.
    /// </summary>
    public static class ScanDataText
    {
        /// <summary>
        /// XY data text of the traced curves; at most <paramref name="maxPointsPerSeries"/> points per series (evenly picked
        /// along x, first and last kept). Without calibration the pixel coordinates relative to the plot area are written
        /// (x to the right, y upwards from the bottom of the plot area).
        /// </summary>
        public static string FromCurves(ScanResult result, int maxPointsPerSeries = 200)
        {
            var sb = new StringBuilder();
            if (result is null || result.Series.Count == 0) return string.Empty;
            var cal = result.Calibration;
            var plot = result.PlotArea;
            int dx = Decimals(cal?.X), dy = Decimals(cal?.Y);
            if (cal is null) sb.Append("# pixels (no axis calibration): x from the left, y from the bottom of the plot area\n");
            for (int s = 0; s < result.Series.Count; s++)
            {
                var series = result.Series[s];
                if (s > 0) sb.Append('\n');
                sb.Append("# series ").Append(Fmt.I(s + 1)).Append(", ").Append(series.Style.ToString().ToLowerInvariant()).Append(", ").Append(PixelColor.Hex(series.Color)).Append('\n');
                IReadOnlyList<Vec2> points = series.DataPoints
                    ?? series.PixelPoints.Select(p => new Vec2(p.X - (plot?.X ?? 0), (plot is null ? 0 : plot.Bottom - 1) - p.Y)).ToList();
                foreach (var p in Pick(points, Math.Max(2, maxPointsPerSeries)))
                {
                    if (!Fmt.Finite(p.X) || !Fmt.Finite(p.Y)) continue;
                    sb.Append(Fmt.Num(p.X, Lang.En, dx)).Append(';').Append(Fmt.Num(p.Y, Lang.En, dy)).Append('\n');
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// Category data text of a bar scan: a header row ("Category;Series 1;…" or the given names), then one row per
        /// category with its (quoted) name and the values of the series; empty cells where a bar part is missing.
        /// </summary>
        public static string FromBars(BarScanResult result, BarScale scale, IReadOnlyList<string?>? categories = null, IReadOnlyList<string>? seriesNames = null, Lang lang = Lang.En)
        {
            if (result is null || !result.Found || scale is null) return string.Empty;
            var values = BarScanner.Values(result, scale);
            int decimals = BarScanner.Decimals(BarScanner.UnitsPerPixel(result, scale));
            var sb = new StringBuilder();
            sb.Append(Clean(lang == Lang.De ? "Kategorie" : "Category"));
            for (int s = 0; s < result.Series; s++)
            {
                string name = seriesNames is not null && s < seriesNames.Count && !string.IsNullOrWhiteSpace(seriesNames[s])
                    ? seriesNames[s] : (lang == Lang.De ? "Serie " : "Series ") + Fmt.I(s + 1);
                sb.Append(';').Append(Clean(name));
            }
            sb.Append('\n');
            for (int c = 0; c < result.Categories; c++)
            {
                string label = categories is not null && c < categories.Count && !string.IsNullOrWhiteSpace(categories[c]) ? categories[c]! : Fmt.I(c + 1);
                sb.Append('"').Append(Clean(label).Replace("\"", "\"\"")).Append('"');
                for (int s = 0; s < result.Series; s++)
                {
                    sb.Append(';');
                    if (values[s, c] is double v) sb.Append(Fmt.Num(v, Lang.En, decimals));
                }
                sb.Append('\n');
            }
            return sb.ToString();
        }

        /// <summary>Decimals for one axis: one more than the resolution of a pixel needs (at least 0, at most 6).</summary>
        private static int Decimals(AxisCalibration? axis)
        {
            if (axis is null || !axis.IsValid) return 1;
            if (axis.Logarithmic)
            {
                double smallest = Math.Min(Math.Abs(axis.Value1), Math.Abs(axis.Value2));
                return Math.Min(6, BarScanner.Decimals(smallest * axis.UnitsPerPixel * Math.Log(10)) + 1);
            }
            return Math.Min(6, BarScanner.Decimals(axis.UnitsPerPixel) + 1);
        }

        private static IEnumerable<Vec2> Pick(IReadOnlyList<Vec2> points, int max)
        {
            if (points.Count <= max)
            {
                foreach (var p in points) yield return p;
                yield break;
            }
            int last = -1;
            for (int k = 0; k < max; k++)
            {
                int i = (int)Math.Round(k * (points.Count - 1) / (double)(max - 1));
                if (i == last) continue;
                last = i;
                yield return points[i];
            }
        }

        /// <summary>Field text without separators or line breaks (they would split the field).</summary>
        private static string Clean(string text) =>
            text.Replace(';', ',').Replace('\t', ' ').Replace('|', '/').Replace('\r', ' ').Replace('\n', ' ').Trim();
    }
}
