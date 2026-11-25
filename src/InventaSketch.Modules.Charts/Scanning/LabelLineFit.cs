using System;
using System.Collections.Generic;
using System.Linq;

namespace InventaSketch.Modules.Charts.Scanning
{
    /// <summary>A number read from a label and its position along an axis in pixels.</summary>
    internal sealed class LabelPoint
    {
        public LabelPoint(RecognizedText word, double value, double position)
        {
            Word = word;
            Value = value;
            Position = position;
        }

        public RecognizedText Word { get; }
        public double Value { get; }

        /// <summary>Position along the axis in pixels (y for a vertical axis, x for a horizontal one).</summary>
        public double Position { get; }
    }

    /// <summary>Straight line value = <see cref="Intercept"/> + <see cref="Slope"/> · position through the inlier labels.</summary>
    internal sealed class LabelFit
    {
        public double Intercept, Slope;
        public List<RecognizedText> Inliers = new();
        public List<double> Values = new();
        public List<double> Positions = new();

        /// <summary>Sum of the position errors of the inliers in pixels (smaller is better).</summary>
        public double Error;
    }

    /// <summary>
    /// Robust straight line through axis labels: value = a + b · position through every pair of numbers; the line with the
    /// most numbers within a tolerance (a third of the text height, at least 2 pixels) wins. At least two numbers at
    /// different positions, with more candidates at least 60 % of them; the slope sign is given (values grow upwards on a
    /// vertical axis, to the right on a horizontal one).
    /// </summary>
    /// <remarks>Ported from the Slidemax chart scanner (<c>Import/Scanning/ScanLabels</c>, axis fit), Apache License 2.0.</remarks>
    internal static class LabelLineFit
    {
        /// <param name="numbers">Candidates (one axis, one column or row of labels).</param>
        /// <param name="slopeSign">+1: values must grow with the position; -1: they must fall (image y down).</param>
        public static LabelFit? Fit(IReadOnlyList<LabelPoint> numbers, int slopeSign)
        {
            if (numbers.Count < 2) return null;
            LabelFit? best = null;
            double bestError = double.MaxValue;
            int bestCount = 0;
            for (int i = 0; i < numbers.Count; i++)
                for (int j = i + 1; j < numbers.Count; j++)
                {
                    var a = numbers[i];
                    var b = numbers[j];
                    double dp = b.Position - a.Position;
                    if (Math.Abs(dp) < 4 || a.Value == b.Value) continue;
                    double slope = (b.Value - a.Value) / dp;
                    if (slopeSign > 0 ? slope <= 0 : slope >= 0) continue;
                    double intercept = a.Value - slope * a.Position;
                    var inliers = new List<LabelPoint>();
                    double error = 0;
                    foreach (var n in numbers)
                    {
                        double predicted = (n.Value - intercept) / slope;
                        double tolerance = Math.Max(2, n.Word.Bounds.Height / 3.0);
                        double d = Math.Abs(predicted - n.Position);
                        if (d <= tolerance)
                        {
                            inliers.Add(n);
                            error += d;
                        }
                    }
                    if (inliers.Select(n => Math.Round(n.Position)).Distinct().Count() < 2) continue;
                    if (best is null || inliers.Count > bestCount || (inliers.Count == bestCount && error < bestError))
                    {
                        best = LeastSquares(inliers);
                        best.Error = error;
                        bestError = error;
                        bestCount = inliers.Count;
                    }
                }
            if (best is null) return null;
            if (best.Inliers.Count < Math.Max(2, Math.Ceiling(0.6 * numbers.Count))) return null;
            return best;
        }

        private static LabelFit LeastSquares(List<LabelPoint> points)
        {
            double mx = points.Average(p => p.Position), my = points.Average(p => p.Value);
            double sxx = points.Sum(p => (p.Position - mx) * (p.Position - mx)), sxy = points.Sum(p => (p.Position - mx) * (p.Value - my));
            double slope = sxx == 0 ? 0 : sxy / sxx;
            return new LabelFit
            {
                Slope = slope,
                Intercept = my - slope * mx,
                Inliers = points.Select(p => p.Word).ToList(),
                Values = points.Select(p => p.Value).ToList(),
                Positions = points.Select(p => p.Position).ToList(),
            };
        }

        /// <summary>
        /// Numbers in columns (vertical axis) or rows (horizontal axis) that overlap across the axis: labels of one axis
        /// stand in one column or row.
        /// </summary>
        public static IEnumerable<List<LabelPoint>> Lines(IEnumerable<LabelPoint> numbers, bool vertical)
        {
            var groups = new List<(int Lo, int Hi, List<LabelPoint> Items)>();
            foreach (var n in numbers.OrderBy(n => vertical ? n.Word.Bounds.Right : n.Word.Bounds.Bottom))
            {
                int lo = vertical ? n.Word.Bounds.X : n.Word.Bounds.Y, hi = vertical ? n.Word.Bounds.Right : n.Word.Bounds.Bottom;
                int i = groups.FindIndex(g => Math.Min(g.Hi, hi) - Math.Max(g.Lo, lo) > 0);
                if (i < 0) groups.Add((lo, hi, new List<LabelPoint> { n }));
                else
                {
                    groups[i].Items.Add(n);
                    groups[i] = (Math.Min(groups[i].Lo, lo), Math.Max(groups[i].Hi, hi), groups[i].Items);
                }
            }
            return groups.Select(g => g.Items);
        }
    }
}
