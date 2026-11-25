using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace InventaSketch.Modules.Charts.Scanning
{
    /// <summary>
    /// A recognised word in a chart picture: text and outline in pixels. Comes from a host text recognition (an
    /// <see cref="ITextReader"/> such as the operating system's OCR) or from the built-in <see cref="DigitTextReader"/>
    /// (numbers only).
    /// </summary>
    public sealed record RecognizedText(string Text, PixelRect Bounds)
    {
        public double CenterX => Bounds.X + Bounds.Width / 2.0;
        public double CenterY => Bounds.Y + Bounds.Height / 2.0;
    }

    /// <summary>An area of one colour in the picture (e.g. a bar), the reference colour for ink inside it.</summary>
    public sealed record ColorRegion(PixelRect Bounds, int Color);

    /// <summary>
    /// What a text reader may know about the picture: the background colour (ARGB) and filled areas whose colour is the
    /// reference for text inside them (white numbers on a bar). Readers may ignore the hints.
    /// </summary>
    public sealed record TextReadHints(int Background, IReadOnlyList<ColorRegion> Fills)
    {
        /// <summary>Hints from a bar scan: its background and its bar parts.</summary>
        public static TextReadHints From(BarScanResult result) =>
            new(result.Background, result.Segments.Select(s => new ColorRegion(s.Bounds, s.Color)).ToList());

        /// <summary>Hints from a curve scan: its background only (curves are thin).</summary>
        public static TextReadHints From(ScanResult result) => new(result.Background, Array.Empty<ColorRegion>());
    }

    /// <summary>
    /// Text recognition used by the scanners for axis labels, category names and value labels. The engine ships
    /// <see cref="DigitTextReader"/> (numbers in common fonts, no external service); hosts may provide a full OCR (e.g. the
    /// Windows OCR in the COM add-in) through this interface. Implementations must not throw; they return an empty list
    /// when nothing is readable.
    /// </summary>
    public interface ITextReader
    {
        IReadOnlyList<RecognizedText> Read(PixelImage image, TextReadHints? hints);
    }

    /// <summary>
    /// Reads numbers in labels: sign (also parentheses and the typographic minus), thousands separators only in groups of
    /// three, currency before (symbol or code like "EUR"), units after ("20 %", "€ 1,5", "12 Mio."). German style: comma as
    /// decimal separator, dot as thousands separator; English the other way round. Spaces and apostrophes separate thousands
    /// in both.
    /// </summary>
    /// <remarks>Ported from the Slidemax chart scanner (<c>Import/Scanning/ScanText</c>), Apache License 2.0.</remarks>
    public static class LabelNumber
    {
        private static readonly Regex Pattern = new(
            @"^(?<pre>[€$£¥]|[A-Z]{3})?\s*(?<sign>[-+(])?\s*(?<pre2>[€$£¥])?\s*(?<num>\d(?:[\d.,'    ]*\d)?)\s*(?<close>\))?\s*(?<suf>[^\d]{0,7})$",
            RegexOptions.CultureInvariant);

        /// <summary>Number of a label; null if the text is no number in this style.</summary>
        public static double? Parse(string? text, bool german)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            string t = Normalize(text!);
            var m = Pattern.Match(t);
            if (!m.Success) return null;
            string sign = m.Groups["sign"].Value;
            bool close = m.Groups["close"].Success;
            if ((sign == "(") != close) return null;
            // Units do not consist of separators alone ("1.5." or "2,").
            string suffix = m.Groups["suf"].Value.Trim();
            if (suffix.Length > 0 && suffix.All(c => c == '.' || c == ',')) return null;
            double? value = Digits(m.Groups["num"].Value, german);
            if (value is null) return null;
            return sign == "-" || sign == "(" ? -value.Value : value.Value;
        }

        /// <summary>Unifies typographic characters; a single "O" (frequent misreading) is zero.</summary>
        private static string Normalize(string text)
        {
            var sb = new StringBuilder(text.Trim());
            for (int i = 0; i < sb.Length; i++)
            {
                switch (sb[i])
                {
                    case '−':
                    case '–':
                    case '—':
                        sb[i] = '-';
                        break;
                    case '’':
                        sb[i] = '\'';
                        break;
                }
            }
            string s = sb.ToString();
            return s == "O" || s == "o" ? "0" : s;
        }

        /// <summary>Checks and reads a digit sequence with separators.</summary>
        private static double? Digits(string num, bool german)
        {
            char dec = german ? ',' : '.', group = german ? '.' : ',';
            int decimalAt = num.IndexOf(dec);
            if (decimalAt >= 0 && num.IndexOf(dec, decimalAt + 1) >= 0) return null;
            string whole = decimalAt >= 0 ? num.Substring(0, decimalAt) : num;
            string fraction = decimalAt >= 0 ? num.Substring(decimalAt + 1) : string.Empty;
            if (fraction.Any(c => !char.IsDigit(c))) return null;
            // Thousands groups: first group 1 to 3 digits, every further one exactly 3, one separator in between.
            var groups = whole.Split(new[] { group, ' ', '\'', ' ', ' ', ' ' });
            if (groups.Length > 1)
            {
                if (groups[0].Length < 1 || groups[0].Length > 3) return null;
                for (int i = 1; i < groups.Length; i++)
                    if (groups[i].Length != 3) return null;
            }
            if (groups.Any(g => g.Length == 0 || g.Any(c => !char.IsDigit(c)))) return null;
            string plain = string.Concat(groups) + (fraction.Length > 0 ? "." + fraction : string.Empty);
            return double.TryParse(plain, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out double v) ? v : null;
        }
    }

    /// <summary>
    /// Whether an optional host text recognition (an <see cref="ITextReader"/> backed by an operating system service) is still
    /// used. If it fails for a reason that does not go away by itself (types missing, asynchronous call cannot be awaited),
    /// it stays off for the rest of the session and the scanners read with <see cref="DigitTextReader"/>. The log gets exactly
    /// one line for it, not a pile on every scan. A timeout does not switch it off (large pictures, busy system).
    /// </summary>
    /// <remarks>Ported from the Slidemax chart scanner (<c>Import/Scanning/TextRecognitionGate</c>), Apache License 2.0.</remarks>
    public sealed class TextReaderGate
    {
        private bool _timeoutReported;

        /// <summary>May the host text recognition be tried?</summary>
        public bool Usable { get; private set; } = true;

        /// <summary>Reports a failure. Returns the log line when switching off for the first time, else null.</summary>
        public string? Failed(string error)
        {
            if (!Usable) return null;
            Usable = false;
            return "Chart scanner: host text recognition not usable (" + error + "), continuing with the built-in digit reader";
        }

        /// <summary>Reports a timeout: the log line the first time, else null; stays usable.</summary>
        public string? TimedOut(int milliseconds)
        {
            if (_timeoutReported) return null;
            _timeoutReported = true;
            return "Chart scanner: host text recognition gave no result after " + milliseconds.ToString(CultureInfo.InvariantCulture) + " ms, using the built-in digit reader this time";
        }
    }
}
