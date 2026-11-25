using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Charts.Scanning
{
    /// <summary>
    /// Calibration of one axis from two known points: pixel position <see cref="Pixel1"/> has value <see cref="Value1"/>,
    /// <see cref="Pixel2"/> has <see cref="Value2"/> (x pixels for the horizontal axis, y pixels for the vertical one).
    /// Linear, or logarithmic (decades evenly spaced; both values positive).
    /// </summary>
    public sealed record AxisCalibration(double Pixel1, double Value1, double Pixel2, double Value2, bool Logarithmic = false)
    {
        /// <summary>True if the two points are finite and distinct (and positive for a logarithmic axis).</summary>
        public bool IsValid =>
            Finite(Pixel1) && Finite(Pixel2) && Finite(Value1) && Finite(Value2) && Math.Abs(Pixel2 - Pixel1) > 1e-9 && Value1 != Value2
            && (!Logarithmic || (Value1 > 0 && Value2 > 0));

        /// <summary>Value at a pixel position (NaN if not <see cref="IsValid"/>).</summary>
        public double ToValue(double pixel)
        {
            if (!IsValid) return double.NaN;
            var t = (pixel - Pixel1) / (Pixel2 - Pixel1);
            if (!Logarithmic) return Value1 + t * (Value2 - Value1);
            var l1 = Math.Log10(Value1);
            return Math.Pow(10, l1 + t * (Math.Log10(Value2) - l1));
        }

        /// <summary>Pixel position of a value (NaN if not <see cref="IsValid"/> or not positive on a logarithmic axis).</summary>
        public double ToPixel(double value)
        {
            if (!IsValid || (Logarithmic && value <= 0)) return double.NaN;
            double t = Logarithmic
                ? (Math.Log10(value) - Math.Log10(Value1)) / (Math.Log10(Value2) - Math.Log10(Value1))
                : (value - Value1) / (Value2 - Value1);
            return Pixel1 + t * (Pixel2 - Pixel1);
        }

        /// <summary>Value change per pixel (linear axes; for logarithmic axes the decades per pixel).</summary>
        public double UnitsPerPixel => !IsValid ? double.NaN
            : Logarithmic ? Math.Abs(Math.Log10(Value2) - Math.Log10(Value1)) / Math.Abs(Pixel2 - Pixel1)
            : Math.Abs(Value2 - Value1) / Math.Abs(Pixel2 - Pixel1);

        private static bool Finite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);
    }

    /// <summary>Calibration of both axes: maps pixel points to data points and back.</summary>
    public sealed record ChartCalibration(AxisCalibration X, AxisCalibration Y)
    {
        public bool IsValid => X is not null && Y is not null && X.IsValid && Y.IsValid;

        public Vec2 ToData(Vec2 pixel) => new(X.ToValue(pixel.X), Y.ToValue(pixel.Y));

        public Vec2 ToPixel(Vec2 data) => new(X.ToPixel(data.X), Y.ToPixel(data.Y));
    }

    /// <summary>
    /// Axis calibration of an XY chart from its labels: numbers below the plot area (x axis) and left of it (y axis) are fitted
    /// with the robust line fit of the bar scanner (<see cref="BarLabelReader"/>); a logarithmic axis is recognised when the
    /// logarithms of the labels fit better than the labels themselves. German and English number styles are both tried.
    /// </summary>
    public static class CurveLabelCalibration
    {
        /// <summary>Calibrations read from the words (null per axis if no consistent labels were found) and the labels used.</summary>
        public static (AxisCalibration? X, AxisCalibration? Y, IReadOnlyList<RecognizedText> Used) FromLabels(PixelRect plot, IReadOnlyList<RecognizedText> words, bool germanFirst = true)
        {
            var used = new List<RecognizedText>();
            if (plot is null || words is null || words.Count == 0) return (null, null, used);
            var styles = germanFirst ? new[] { true, false } : new[] { false, true };
            (AxisCalibration Axis, LabelFit Fit)? bestX = null, bestY = null;
            foreach (var german in styles)
            {
                var x = Axis(plot, words, german, horizontal: true);
                if (x is not null && (bestX is null || x.Value.Fit.Inliers.Count > bestX.Value.Fit.Inliers.Count)) bestX = x;
                var y = Axis(plot, words, german, horizontal: false);
                if (y is not null && (bestY is null || y.Value.Fit.Inliers.Count > bestY.Value.Fit.Inliers.Count)) bestY = y;
            }
            if (bestX is not null) used.AddRange(bestX.Value.Fit.Inliers);
            if (bestY is not null) used.AddRange(bestY.Value.Fit.Inliers);
            return (bestX?.Axis, bestY?.Axis, used);
        }

        private static (AxisCalibration Axis, LabelFit Fit)? Axis(PixelRect plot, IReadOnlyList<RecognizedText> words, bool german, bool horizontal)
        {
            var numbers = new List<LabelPoint>();
            foreach (var w in words)
            {
                if (LabelNumber.Parse(w.Text, german) is not double v) continue;
                var b = w.Bounds;
                if (horizontal)
                {
                    // Below the plot area, at most a few text heights away, horizontally near the plot.
                    if (b.Y < plot.Bottom - 2 || b.Y > plot.Bottom + 4 * Math.Max(6, b.Height) + 8) continue;
                    if (w.CenterX < plot.X - 0.1 * plot.Width || w.CenterX > plot.Right + 0.1 * plot.Width) continue;
                    numbers.Add(new LabelPoint(w, v, w.CenterX));
                }
                else
                {
                    // Left of the plot area, vertically near it.
                    if (b.Right > plot.X + 2 || b.Right < plot.X - 12 * Math.Max(6, b.Height)) continue;
                    if (w.CenterY < plot.Y - 0.1 * plot.Height || w.CenterY > plot.Bottom + 0.1 * plot.Height) continue;
                    numbers.Add(new LabelPoint(w, v, w.CenterY));
                }
            }
            (AxisCalibration, LabelFit)? best = null;
            // Labels of one axis stand in one row (x) or column (y); the one nearest to the plot wins on equal counts.
            var lines = LabelLineFit.Lines(numbers, vertical: !horizontal)
                .OrderBy(l => horizontal ? l.Min(n => n.Word.Bounds.Y) : -l.Max(n => n.Word.Bounds.Right));
            foreach (var line in lines)
            {
                var sign = horizontal ? +1 : -1;
                var linear = LabelLineFit.Fit(line, sign);
                LabelFit? log = null;
                if (line.Count >= 3 && line.All(n => n.Value > 0))
                    log = LabelLineFit.Fit(line.Select(n => new LabelPoint(n.Word, Math.Log10(n.Value), n.Position)).ToList(), sign);
                bool useLog = log is not null && (linear is null || log.Inliers.Count > linear.Inliers.Count);
                var fit = useLog ? log : linear;
                if (fit is null || fit.Positions.Count < 2) continue;
                if (best is not null && fit.Inliers.Count <= best.Value.Item2.Inliers.Count) continue;
                double p1 = fit.Positions.Min(), p2 = fit.Positions.Max();
                double v1 = fit.Intercept + fit.Slope * p1, v2 = fit.Intercept + fit.Slope * p2;
                if (useLog)
                {
                    v1 = Math.Pow(10, v1);
                    v2 = Math.Pow(10, v2);
                }
                else
                {
                    // Snap to the label values when the fit is within a tenth of a pixel of them (clean round numbers).
                    v1 = Snap(v1, fit, p1);
                    v2 = Snap(v2, fit, p2);
                }
                var axis = new AxisCalibration(p1, v1, p2, v2, useLog);
                if (axis.IsValid) best = (axis, fit);
            }
            return best;
        }

        private static double Snap(double value, LabelFit fit, double position)
        {
            var k = Math.Abs(fit.Slope);
            for (int i = 0; i < fit.Positions.Count; i++)
                if (Math.Abs(fit.Positions[i] - position) < 1e-9 && Math.Abs(fit.Values[i] - value) <= 0.1 * k) return fit.Values[i];
            return value;
        }
    }
}
