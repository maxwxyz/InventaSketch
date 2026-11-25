using System;
using System.Collections.Generic;
using InventaSketch.Core;

namespace InventaSketch.Modules.Charts.Scanning
{
    /// <summary>Line style guessed for a traced curve.</summary>
    public enum CurveStyle
    {
        Solid,
        Dashed,
        Dotted,
    }

    /// <summary>Where the axis calibration of a curve scan comes from.</summary>
    public enum CalibrationSource
    {
        /// <summary>No calibration: points are only available in pixels.</summary>
        None,

        /// <summary>Both axes given by the caller (<see cref="DigitizeOptions"/>).</summary>
        User,

        /// <summary>At least one axis read from the axis labels (an <see cref="ITextReader"/>), the other given or read.</summary>
        Labels,
    }

    /// <summary>
    /// A long straight line found in the picture (axis, frame or gridline): <see cref="Position"/> is y for a horizontal line
    /// and x for a vertical one, <see cref="From"/>/<see cref="To"/> its extent along the line, all in pixels (pixel centres
    /// at integer coordinates).
    /// </summary>
    public sealed record ScanLine(bool Horizontal, double Position, double From, double To, int Thickness, int Color)
    {
        public double Length => To - From + 1;
    }

    /// <summary>A roughly detected hatched region: outline, hatch colour, hatch angle (degrees counter-clockwise as drawn) and spacing (pixels).</summary>
    public sealed record HatchedRegion(PixelRect Bounds, int Color, double AngleDegrees, double Spacing);

    /// <summary>
    /// One traced curve: colour (ARGB), guessed line style, points in pixels (x ascending, one per traced column; pixel centres
    /// at integer coordinates) and, with a calibration, the same points in data coordinates.
    /// </summary>
    public sealed record ScanSeries(int Color, CurveStyle Style, IReadOnlyList<Vec2> PixelPoints, IReadOnlyList<Vec2>? DataPoints, double LineWidth, double Coverage)
    {
        /// <summary>Points for a chart: data coordinates when calibrated, else pixels.</summary>
        public IReadOnlyList<Vec2> Points => DataPoints ?? PixelPoints;
    }

    /// <summary>
    /// Result of the <see cref="CurveDigitizer"/>: plot area, axes, gridlines, traced series, hatched regions, calibration and
    /// warnings. A failed scan has no series and at least one warning; nothing throws.
    /// </summary>
    public sealed record ScanResult
    {
        /// <summary>Plot area in pixels (inside the axes); null if nothing was found.</summary>
        public PixelRect? PlotArea { get; init; }

        /// <summary>Horizontal axis (lowest long horizontal line); null if none was found.</summary>
        public ScanLine? XAxis { get; init; }

        /// <summary>Vertical axis (leftmost long vertical line); null if none was found.</summary>
        public ScanLine? YAxis { get; init; }

        /// <summary>Further long lines (gridlines, frame) that were masked out before tracing.</summary>
        public IReadOnlyList<ScanLine> Gridlines { get; init; } = Array.Empty<ScanLine>();

        public IReadOnlyList<ScanSeries> Series { get; init; } = Array.Empty<ScanSeries>();

        public IReadOnlyList<HatchedRegion> Hatches { get; init; } = Array.Empty<HatchedRegion>();

        /// <summary>Background colour (ARGB).</summary>
        public int Background { get; init; } = PixelColor.White;

        /// <summary>Pixel to data mapping used for <see cref="ScanSeries.DataPoints"/>; null without calibration.</summary>
        public ChartCalibration? Calibration { get; init; }

        public CalibrationSource CalibrationSource { get; init; }

        /// <summary>Axis labels used for the calibration (empty if none were read).</summary>
        public IReadOnlyList<RecognizedText> AxisLabels { get; init; } = Array.Empty<RecognizedText>();

        /// <summary>Hints and problems (bilingual); empty when everything went well.</summary>
        public IReadOnlyList<I18nText> Warnings { get; init; } = Array.Empty<I18nText>();

        public bool Found => Series.Count > 0;
    }
}
