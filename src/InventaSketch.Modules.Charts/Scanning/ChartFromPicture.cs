using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Charts.Scanning
{
    /// <summary>Result of turning a picture into a chart: the chart element (or null) and what the user should know.</summary>
    public sealed record PictureChart(SketchObject? Chart, I18nText Message, bool Bars, int Series);

    /// <summary>
    /// Charts v2 stage 5, host entry point: a picture (clipboard, photo, screenshot) becomes an editable chart. Bars and
    /// columns are read with <see cref="BarScanner"/> (values from the axis labels when the digits can be read, else relative
    /// to the largest bar = 100); curves with <see cref="CurveDigitizer"/> (calibrated from the axis labels when readable,
    /// else in picture units). The chart is a "chart.bar" or "chart.xy" element with the data, sized like the picture's
    /// aspect ratio, ready to be inserted by the host.
    /// </summary>
    public static class ChartFromPicture
    {
        /// <summary>Largest side of the inserted chart in mm.</summary>
        public const double TargetSize = 110;

        public static PictureChart Build(Registry registry, PixelImage image, Vec2 at, Lang lang, ITextReader? reader = null)
        {
            if (!image.IsValid || image.Width < 16 || image.Height < 16)
                return new PictureChart(null, new I18nText("The picture is empty or too small to be read as a chart.", "Das Bild ist leer oder zu klein, um es als Diagramm zu lesen."), false, 0);
            reader ??= DigitTextReader.Instance;
            var size = Size(image);
            var german = lang == Lang.De;

            // Bars first: a bar chart also has long axis lines, but curves rarely give filled rectangles.
            BarScanResult bars;
            try { bars = BarScanner.Scan(image); }
            catch (Exception) { bars = BarScanResult.Empty; }
            if (bars.Segments.Count >= 2 && registry.Item("chart.column") is { } columnItem)
            {
                IReadOnlyList<RecognizedText> words;
                try { words = reader.Read(image, TextReadHints.From(bars)); }
                catch (Exception) { words = Array.Empty<RecognizedText>(); }
                var labels = BarLabelReader.Read(bars, words, true, german);
                var scale = labels.Scale ?? new BarScale();
                var data = ScanDataText.FromBars(bars, scale, labels.HasCategories ? labels.Categories : null, null, lang);
                var item = bars.Orientation == BarOrientation.Bars && registry.Item("chart.bar.horizontal") is { } h ? h : columnItem;
                var chart = registry.Create(item, at) with { Size = size };
                chart = chart.WithParam("data", data);
                var relative = labels.Scale is null;
                return new PictureChart(chart, relative
                    ? new I18nText($"Read {bars.Categories} categor{(bars.Categories == 1 ? "y" : "ies")} with {bars.Series} series; the axis numbers could not be read, so the largest bar is 100 – edit the data to the real values.",
                                   $"{bars.Categories} Kategorie(n) mit {bars.Series} Reihe(n) gelesen; die Achszahlen waren nicht lesbar, deshalb ist der größte Balken 100 – bitte die Daten auf die echten Werte ändern.")
                    : new I18nText($"Read {bars.Categories} categor{(bars.Categories == 1 ? "y" : "ies")} with {bars.Series} series, calibrated from the axis labels.",
                                   $"{bars.Categories} Kategorie(n) mit {bars.Series} Reihe(n) gelesen, über die Achsbeschriftung kalibriert."), true, bars.Series);
            }

            ScanResult curves;
            try { curves = CurveDigitizer.Digitize(image, new DigitizeOptions { TextReader = reader, GermanNumbersFirst = german }); }
            catch (Exception) { curves = new ScanResult(); }
            if (curves.Found && registry.Item("chart.line") is { } lineItem)
            {
                var data = ScanDataText.FromCurves(curves, 200);
                var chart = registry.Create(lineItem, at) with { Size = size };
                chart = chart.WithParam("data", data);
                var calibrated = curves.Calibration is not null;
                return new PictureChart(chart, calibrated
                    ? new I18nText($"Traced {curves.Series.Count} curve(s), calibrated from the axis labels.", $"{curves.Series.Count} Kurve(n) nachgezeichnet, über die Achsbeschriftung kalibriert.")
                    : new I18nText($"Traced {curves.Series.Count} curve(s); the axis numbers could not be read, so the values are in picture units – edit the axis ranges or the data.",
                                   $"{curves.Series.Count} Kurve(n) nachgezeichnet; die Achszahlen waren nicht lesbar, deshalb sind die Werte in Bildeinheiten – bitte Achsbereiche oder Daten anpassen."), false, curves.Series.Count);
            }

            var warning = curves.Warnings.Concat(bars.Warnings).FirstOrDefault();
            return new PictureChart(null, warning ?? new I18nText("No bars or curves were found in the picture.", "Im Bild wurden keine Balken oder Kurven gefunden."), false, 0);
        }

        /// <summary>Chart size with the picture's aspect ratio, the larger side <see cref="TargetSize"/> mm.</summary>
        private static Size2 Size(PixelImage image)
        {
            double w = image.Width, h = image.Height;
            var k = TargetSize / Math.Max(w, h);
            return new Size2(Math.Max(40, Math.Round(w * k, 1)), Math.Max(30, Math.Round(h * k, 1)));
        }
    }
}
