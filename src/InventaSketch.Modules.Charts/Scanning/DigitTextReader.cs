using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace InventaSketch.Modules.Charts.Scanning
{
    /// <summary>
    /// Reads numbers in a picture without external services: axis and value labels, on every platform and when no host text
    /// recognition is available. Ink is what clearly differs from the background or from the colour of the filled area below
    /// (<see cref="TextReadHints"/>); connected ink gives pieces, neighbouring pieces on the same line a word. Within a word
    /// the column profile separates the characters even when they touch: cuts at weak columns, the split with the best sum of
    /// scores wins. Digits and the percent sign are compared with patterns of common fonts (<see cref="DigitGlyphs"/>,
    /// correlation on a 10 × 14 grid after cropping), comma, dot, minus and plus by position and size. A word only counts if
    /// every character is recognised with confidence; letters therefore give no number.
    /// </summary>
    /// <remarks>Ported from the Slidemax chart scanner (<c>Import/Scanning/DigitReader</c>), Apache License 2.0.</remarks>
    public sealed class DigitTextReader : ITextReader
    {
        /// <summary>Shared instance (the reader has no state).</summary>
        public static readonly DigitTextReader Instance = new();

        private const int GridW = 10, GridH = 14;

        /// <summary>Difference per channel from the reference from which a pixel counts as ink.</summary>
        private const int InkThreshold = 60;

        /// <summary>Smallest height of a digit in pixels.</summary>
        private const int MinDigitHeight = 5;

        /// <summary>Minimum score (correlation minus aspect ratio difference) of a character.</summary>
        public const double MinScore = 0.6;

        /// <summary>Minimum distance to the best other character, else the character counts as uncertain.</summary>
        public const double MinMargin = 0.02;

        /// <summary>Threshold per character in the split: a cut only pays off if both parts score better than this.</summary>
        private const double PieceBias = 0.9;

        private sealed class Pattern
        {
            public Pattern(char c, float[] desc, double aspect)
            {
                Char = c;
                Desc = desc;
                Aspect = aspect;
            }

            public char Char { get; }
            public float[] Desc { get; }
            public double Aspect { get; }
        }

        private static readonly Lazy<List<Pattern>> Patterns = new(LoadPatterns);

        /// <summary>Connected ink.</summary>
        private sealed class Part
        {
            public int X0 = int.MaxValue, Y0 = int.MaxValue, X1 = -1, Y1 = -1;
            public readonly List<int> Pixels = new();

            public int Width => X1 - X0 + 1;
            public int Height => Y1 - Y0 + 1;
            public double CenterX => (X0 + X1 + 1) / 2.0;
            public double CenterY => (Y0 + Y1 + 1) / 2.0;

            public void Add(int x, int y, int index)
            {
                if (x < X0) X0 = x;
                if (x > X1) X1 = x;
                if (y < Y0) Y0 = y;
                if (y > Y1) Y1 = y;
                Pixels.Add(index);
            }
        }

        /// <summary>Word: pieces of one line, band of the digits (top, bottom) and digit height.</summary>
        private sealed class Word
        {
            public readonly List<Part> Parts = new();
            public int Top = int.MaxValue, Bottom = -1;

            public int Height => Bottom - Top + 1;
            public int X0 => Parts.Min(p => p.X0);
            public int X1 => Parts.Max(p => p.X1);

            public void AddTall(Part p)
            {
                Parts.Add(p);
                Top = Math.Min(Top, p.Y0);
                Bottom = Math.Max(Bottom, p.Y1);
            }
        }

        /// <summary>
        /// Recognised numbers (words of digits, separators, signs, percent) with their outline. Without hints the background
        /// is the most frequent colour. Never throws; an invalid picture gives an empty list.
        /// </summary>
        public IReadOnlyList<RecognizedText> Read(PixelImage image, TextReadHints? hints)
        {
            if (image is null || !image.IsValid) return Array.Empty<RecognizedText>();
            int w = image.Width, h = image.Height;
            if (w < 4 || h < 4) return Array.Empty<RecognizedText>();
            var pixels = image.ToArgb();
            var strength = Strength(pixels, w, h, hints ?? new TextReadHints(BarScanner.Background(pixels), Array.Empty<ColorRegion>()));
            var parts = Components(strength, w, h).Where(p => Plausible(p, h)).ToList();
            var texts = new List<RecognizedText>();
            foreach (var word in Words(parts))
            {
                string? text = Recognize(word, strength, w);
                if (text is null) continue;
                int x0 = word.X0, y0 = word.Parts.Min(p => p.Y0), x1 = word.X1, y1 = word.Parts.Max(p => p.Y1);
                texts.Add(new RecognizedText(text, new PixelRect(x0, y0, x1 - x0 + 1, y1 - y0 + 1)));
            }
            return texts;
        }

        // ------------------------------------------------------------------ Ink

        /// <summary>
        /// Difference of every pixel from its reference: colour of the fill whose outline contains it, else the background. A
        /// border of one pixel around every fill stays empty (anti-aliasing of the bars is no text).
        /// </summary>
        private static int[] Strength(int[] pixels, int w, int h, TextReadHints hints)
        {
            var reference = new int[w * h];
            for (int i = 0; i < reference.Length; i++) reference[i] = hints.Background;
            var skip = new bool[w * h];
            foreach (var s in hints.Fills ?? Array.Empty<ColorRegion>())
            {
                var b = s.Bounds;
                for (int y = Math.Max(0, b.Y - 1); y < Math.Min(h, b.Bottom + 1); y++)
                    for (int x = Math.Max(0, b.X - 1); x < Math.Min(w, b.Right + 1); x++)
                    {
                        bool inside = x >= b.X && x < b.Right && y >= b.Y && y < b.Bottom;
                        bool edge = x <= b.X || x >= b.Right - 1 || y <= b.Y || y >= b.Bottom - 1;
                        if (inside) reference[y * w + x] = s.Color;
                        if (edge) skip[y * w + x] = true;
                    }
            }
            var strength = new int[w * h];
            for (int i = 0; i < strength.Length; i++)
                strength[i] = skip[i] ? 0 : PixelColor.Distance(pixels[i], reference[i]);
            return strength;
        }

        /// <summary>Connected ink (neighbours also diagonal).</summary>
        private static List<Part> Components(int[] strength, int w, int h)
        {
            var seen = new bool[strength.Length];
            var stack = new int[strength.Length];
            var list = new List<Part>();
            for (int i = 0; i < strength.Length; i++)
            {
                if (seen[i] || strength[i] < InkThreshold) continue;
                var g = new Part();
                int top = 0;
                stack[top++] = i;
                seen[i] = true;
                while (top > 0)
                {
                    int p = stack[--top];
                    int x = p % w, y = p / w;
                    g.Add(x, y, p);
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int nx = x + dx, ny = y + dy;
                            if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                            int q = ny * w + nx;
                            if (seen[q] || strength[q] < InkThreshold) continue;
                            seen[q] = true;
                            stack[top++] = q;
                        }
                }
                list.Add(g);
            }
            return list;
        }

        /// <summary>Lines (axis, gridlines), large areas and filled boxes (legend) are no text.</summary>
        private static bool Plausible(Part g, int h)
        {
            int thin = Math.Min(g.Width, g.Height), length = Math.Max(g.Width, g.Height);
            if (thin <= 3 && length >= Math.Max(25, 6 * thin)) return false;
            if (g.Height > 60 || g.Width > 200 || g.Height > h / 3 + 1) return false;
            if (g.Width >= 5 && g.Height >= 5 && g.Pixels.Count >= 0.9 * g.Width * g.Height) return false;
            return true;
        }

        // ------------------------------------------------------------------ Words

        /// <summary>
        /// Words: tall pieces (5 pixels and more) side by side with overlapping height and a gap up to 0.6 digit heights (also
        /// parts of a percent sign); then small pieces in between and before (comma, dot, minus), which may also join two
        /// words ("1.000").
        /// </summary>
        private static List<Word> Words(List<Part> parts)
        {
            var words = new List<Word>();
            foreach (var p in parts.Where(p => p.Height >= MinDigitHeight).OrderBy(p => p.X0))
            {
                Word? best = null;
                double bestOverlap = 0;
                foreach (var word in words)
                {
                    int hw = word.Height;
                    if (p.Height < 0.4 * hw || p.Height > 1.6 * hw + 1) continue;
                    int gap = p.X0 - word.X1 - 1;
                    if (gap < -p.Width || gap > 0.6 * Math.Max(hw, p.Height) + 1) continue;
                    double overlap = Math.Min(word.Bottom, p.Y1) - Math.Max(word.Top, p.Y0) + 1;
                    if (overlap < 0.6 * Math.Min(hw, p.Height) || overlap <= bestOverlap) continue;
                    best = word;
                    bestOverlap = overlap;
                }
                if (best is null)
                {
                    best = new Word();
                    words.Add(best);
                }
                best.AddTall(p);
            }
            // Single pieces that are low next to a word (large comma) are small characters.
            var small = parts.Where(p => p.Height < MinDigitHeight).ToList();
            foreach (var single in words.Where(x => x.Parts.Count == 1).ToList())
            {
                var p = single.Parts[0];
                bool low = words.Any(o => o != single && p.Height <= 0.45 * o.Height && p.Y0 >= o.Top && p.Y0 <= o.Bottom
                    && Math.Max(o.X0 - p.X1, p.X0 - o.X1) - 1 <= 0.8 * o.Height);
                if (!low) continue;
                words.Remove(single);
                small.Add(p);
            }
            words = words.OrderBy(x => x.X0).ToList();
            foreach (var s in small)
            {
                foreach (var word in words)
                {
                    int hw = word.Height;
                    if (s.Height > 0.45 * hw || s.Width > 0.8 * hw) continue;
                    if (s.Y1 < word.Top || s.Y0 > word.Bottom + 0.4 * hw) continue;
                    bool inside = s.X0 > word.X0 && s.X1 < word.X1;
                    bool before = s.X1 < word.X0 && word.X0 - s.X1 - 1 <= 0.8 * hw;
                    bool after = s.X0 > word.X1 && s.X0 - word.X1 - 1 <= 0.6 * hw;
                    if (!inside && !before && !after) continue;
                    word.Parts.Add(s);
                    break;
                }
            }
            // Join words that only touch through a separator.
            bool changed = true;
            while (changed)
            {
                changed = false;
                foreach (var a in words)
                {
                    int lastTall = a.Parts.Where(t => !small.Contains(t)).Max(t => t.X1);
                    if (!a.Parts.Any(t => small.Contains(t) && t.X0 > lastTall)) continue;
                    // Next word of the same line to the right.
                    var b = words.Where(o => o != a && o.X0 > a.X1
                            && Math.Min(a.Bottom, o.Bottom) - Math.Max(a.Top, o.Top) + 1 >= 0.6 * Math.Min(a.Height, o.Height))
                        .OrderBy(o => o.X0).FirstOrDefault();
                    if (b is null || b.X0 - a.X1 - 1 > 0.6 * Math.Max(a.Height, b.Height) + 1) continue;
                    foreach (var p in b.Parts)
                    {
                        if (!small.Contains(p)) a.AddTall(p);
                        else a.Parts.Add(p);
                    }
                    words.Remove(b);
                    changed = true;
                    break;
                }
            }
            return words;
        }

        /// <summary>Text of a word; null if a character is uncertain, the sequence is no number or contains no digit.</summary>
        private static string? Recognize(Word word, int[] strength, int width)
        {
            int height = word.Height;
            if (height < MinDigitHeight) return null;
            int top = word.Top, bottom = word.Bottom;
            var marks = word.Parts.Where(p => p.Height < MinDigitHeight || p.Height <= 0.45 * height && p.Width <= 0.8 * height).ToList();
            // Remove small characters including a border of one pixel (weak anti-aliasing) from the band.
            var markPixels = new HashSet<int>();
            foreach (int q in marks.SelectMany(p => p.Pixels))
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                        markPixels.Add(q + dy * width + dx);
            var tall = word.Parts.Where(p => !marks.Contains(p)).ToList();
            if (tall.Count == 0) return null;
            int x0 = tall.Min(p => p.X0), x1 = tall.Max(p => p.X1);
            int cw = x1 - x0 + 1;
            int max = word.Parts.SelectMany(p => p.Pixels).Max(p => strength[p]);
            // Coverage in the band of the digits, without the small characters.
            var cov = new float[cw * height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < cw; x++)
                {
                    int p = (top + y) * width + x0 + x;
                    if (p < 0 || p >= strength.Length || markPixels.Contains(p)) continue;
                    cov[y * cw + x] = Math.Min(1f, strength[p] / (float)Math.Max(1, max));
                }
            var profile = new double[cw];
            for (int x = 0; x < cw; x++)
                for (int y = 0; y < height; y++)
                    profile[x] += cov[y * cw + x];

            var symbols = new List<(double X, char C)>();
            // Runs without an empty column; every run is one character or several touching ones.
            int start = -1;
            for (int x = 0; x <= cw; x++)
            {
                bool ink = x < cw && profile[x] > 0.2;
                if (ink && start < 0) start = x;
                if (!ink && start >= 0)
                {
                    var pieces = Segment(cov, cw, height, profile, start, x);
                    // A run with little ink (rest of the anti-aliasing) does not count.
                    double peak = 0;
                    for (int k = start; k < x; k++) peak = Math.Max(peak, profile[k]);
                    if (pieces is null && peak < 0.3 * height)
                    {
                        start = -1;
                        continue;
                    }
                    if (pieces is null) return null;
                    symbols.AddRange(pieces.Select(p => (x0 + p.X, p.C)));
                    start = -1;
                }
            }
            if (symbols.Count == 0) return null;
            double firstDigit = symbols.Min(s => s.X), lastDigit = symbols.Max(s => s.X);
            foreach (var m in marks)
            {
                double center = m.CenterY - top;
                char c;
                if (m.CenterX > firstDigit && m.CenterX < lastDigit)
                {
                    // Separators at the bottom: the comma reaches below the baseline of the digits, the dot does not.
                    if (m.Y0 < top + 0.45 * height) return null;
                    c = m.Y1 > bottom ? ',' : '.';
                }
                else if (m.CenterX < firstDigit)
                {
                    // Before the number only signs at medium height; anything else (specks) does not count.
                    if (center < 0.25 * height || center > 0.8 * height) continue;
                    if (m.Width >= 1.5 * m.Height && m.Width >= 0.2 * height) c = '-';
                    else if (m.Height >= 0.3 * height && m.Width >= 0.6 * m.Height && m.Width <= 1.6 * m.Height) c = '+';
                    else continue;
                }
                else continue;
                symbols.Add((m.CenterX, c));
            }
            var text = new string(symbols.OrderBy(s => s.X).Select(s => s.C).ToArray());
            // Percent only at the end, signs only at the start, separators only between digits.
            if (text.IndexOf('%') >= 0 && text.IndexOf('%') != text.Length - 1) return null;
            if (!text.Any(char.IsDigit)) return null;
            return text;
        }

        /// <summary>
        /// Splits the run [a, b) into characters: cuts at weak columns (local minima, below 35 % of the strongest), every piece
        /// 0.2 to 1.4 digit heights wide; the split with the largest sum (score minus <see cref="PieceBias"/>) wins. Null if
        /// no split gives only confident characters.
        /// </summary>
        private static List<(double X, char C)>? Segment(float[] cov, int cw, int height, double[] profile, int a, int b)
        {
            double peak = 0;
            for (int x = a; x < b; x++) peak = Math.Max(peak, profile[x]);
            var cuts = new SortedSet<int> { a, b };
            for (int x = a + 1; x < b - 1; x++)
            {
                bool minimum = profile[x] <= profile[x - 1] && profile[x] <= profile[x + 1];
                if (minimum || profile[x] <= 0.35 * peak)
                {
                    cuts.Add(x);
                    cuts.Add(x + 1);
                }
            }
            var points = cuts.ToList();
            int n = points.Count;
            var best = new double[n];
            var from = new int[n];
            var piece = new (char C, double Score, double Margin)[n];
            for (int i = 1; i < n; i++) best[i] = double.NegativeInfinity;
            double minW = Math.Max(1, 0.2 * height), maxW = 1.4 * height + 1;
            var cache = new Dictionary<(int, int), (char C, double Score, double Margin)>();
            for (int j = 1; j < n; j++)
                for (int i = 0; i < j; i++)
                {
                    if (double.IsNegativeInfinity(best[i])) continue;
                    int w = points[j] - points[i];
                    if (w < minW || w > maxW) continue;
                    if (!cache.TryGetValue((points[i], points[j]), out var c))
                        cache[(points[i], points[j])] = c = Classify(cov, cw, height, points[i], points[j]);
                    if (c.Score < MinScore) continue;
                    double total = best[i] + c.Score - PieceBias;
                    if (total > best[j])
                    {
                        best[j] = total;
                        from[j] = i;
                        piece[j] = c;
                    }
                }
            if (double.IsNegativeInfinity(best[n - 1])) return null;
            var result = new List<(double X, char C)>();
            for (int j = n - 1; j > 0; j = from[j])
            {
                if (piece[j].Margin < MinMargin) return null;
                result.Add(((points[from[j]] + points[j]) / 2.0, piece[j].C));
            }
            result.Reverse();
            return result;
        }

        /// <summary>Best pattern for the columns [a, b) of the band: character, score and distance to the best other character.</summary>
        private static (char C, double Score, double Margin) Classify(float[] cov, int cw, int height, int a, int b)
        {
            int w = b - a;
            var sub = new float[w * height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < w; x++)
                    sub[y * w + x] = cov[y * cw + a + x];
            var (desc, aspect) = Describe(sub, w, height);
            if (desc is null) return ('?', 0, 0);
            var best = new Dictionary<char, double>();
            foreach (var pattern in Patterns.Value)
            {
                double score = Score(desc, aspect, pattern);
                if (!best.TryGetValue(pattern.Char, out double old) || score > old) best[pattern.Char] = score;
            }
            var ranked = best.OrderByDescending(p => p.Value).ToList();
            return (ranked[0].Key, ranked[0].Value, ranked.Count > 1 ? ranked[0].Value - ranked[1].Value : 1);
        }

        private static double Score(float[] desc, double aspect, Pattern p)
        {
            double dot = 0;
            for (int i = 0; i < desc.Length; i++) dot += desc[i] * p.Desc[i];
            return dot - 0.4 * Math.Abs(aspect - p.Aspect);
        }

        /// <summary>
        /// Feature of a character: cropped to the ink (coverage from 0.25), centred in a box at least 0.75 of the height wide
        /// (narrow characters like the one stay narrow), averaged onto 10 × 14 cells, mean removed and normalised to length 1;
        /// plus the aspect ratio.
        /// </summary>
        private static (float[]? Desc, double Aspect) Describe(float[] cov, int w, int h)
        {
            int x0 = w, y0 = h, x1 = -1, y1 = -1;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    if (cov[y * w + x] >= 0.25f)
                    {
                        if (x < x0) x0 = x;
                        if (x > x1) x1 = x;
                        if (y < y0) y0 = y;
                        if (y > y1) y1 = y;
                    }
            if (x1 < 0) return (null, 0);
            int cw = x1 - x0 + 1, ch = y1 - y0 + 1;
            // Pieces that are too low (rest of a neighbouring character) are no digit.
            if (ch < 0.6 * h) return (null, 0);
            double boxW = Math.Max(cw, 0.75 * ch);
            double left = x0 + cw / 2.0 - boxW / 2.0;
            var desc = new float[GridW * GridH];
            for (int v = 0; v < GridH; v++)
            {
                double ya = y0 + v * (double)ch / GridH, yb = y0 + (v + 1) * (double)ch / GridH;
                for (int u = 0; u < GridW; u++)
                {
                    double xa = left + u * boxW / GridW, xb = left + (u + 1) * boxW / GridW;
                    double sum = 0;
                    for (int y = Math.Max(y0, (int)Math.Floor(ya)); y < Math.Min(y1 + 1, (int)Math.Ceiling(yb)); y++)
                    {
                        double wy = Math.Min(yb, y + 1) - Math.Max(ya, y);
                        if (wy <= 0) continue;
                        for (int x = Math.Max(x0, (int)Math.Floor(xa)); x < Math.Min(x1 + 1, (int)Math.Ceiling(xb)); x++)
                        {
                            double wx = Math.Min(xb, x + 1) - Math.Max(xa, x);
                            if (wx > 0) sum += wx * wy * cov[y * w + x];
                        }
                    }
                    desc[v * GridW + u] = (float)sum;
                }
            }
            double mean = desc.Average(d => (double)d);
            double norm = 0;
            for (int i = 0; i < desc.Length; i++)
            {
                desc[i] = (float)(desc[i] - mean);
                norm += desc[i] * desc[i];
            }
            norm = Math.Sqrt(norm);
            if (norm <= 1e-9) return (null, 0);
            for (int i = 0; i < desc.Length; i++) desc[i] = (float)(desc[i] / norm);
            return (desc, cw / (double)ch);
        }

        private static List<Pattern> LoadPatterns()
        {
            var list = new List<Pattern>();
            foreach (string line in DigitGlyphs.Data.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = line.Split('|');
                if (parts.Length != 4) continue;
                int w = int.Parse(parts[1], CultureInfo.InvariantCulture), h = int.Parse(parts[2], CultureInfo.InvariantCulture);
                var cov = new float[w * h];
                for (int i = 0; i < cov.Length && i < parts[3].Length; i++)
                    cov[i] = int.Parse(parts[3][i].ToString(), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 15f;
                var (desc, aspect) = Describe(cov, w, h);
                if (desc is not null) list.Add(new Pattern(parts[0][0], desc, aspect));
            }
            return list;
        }
    }
}
