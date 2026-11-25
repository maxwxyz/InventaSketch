using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Charts.Scanning
{
    /// <summary>Options of the <see cref="CurveDigitizer"/>; all optional.</summary>
    public sealed record DigitizeOptions
    {
        /// <summary>Calibration of the horizontal axis (overrides labels).</summary>
        public AxisCalibration? XAxis { get; init; }

        /// <summary>Calibration of the vertical axis (overrides labels).</summary>
        public AxisCalibration? YAxis { get; init; }

        /// <summary>Text recognition for axis labels; used for axes without a given calibration.</summary>
        public ITextReader? TextReader { get; init; }

        /// <summary>Try the German number style first when reading labels ("1.000" = thousand).</summary>
        public bool GermanNumbersFirst { get; init; } = true;

        /// <summary>Plot area given by the user (skips the axis search for the area, axes are still reported).</summary>
        public PixelRect? PlotArea { get; init; }

        /// <summary>Smallest horizontal extent of a curve as a fraction of the plot width (shorter traces are noise or legend).</summary>
        public double MinSpan { get; init; } = 0.1;

        /// <summary>Largest number of curves returned (the longest ones).</summary>
        public int MaxCurves { get; init; } = 12;

        /// <summary>Report hatched regions (groups of short parallel straight lines) instead of tracing them as curves.</summary>
        public bool DetectHatching { get; init; } = true;
    }

    /// <summary>
    /// Curve digitiser for line and XY chart pictures. Steps:
    /// <list type="number">
    /// <item>background = most frequent colour; long straight runs of grey ink in rows and columns are lines – the lowest
    /// horizontal one is the x axis, the leftmost vertical one the y axis, the others (gridlines, frame) are masked out; the
    /// plot area lies between them;</item>
    /// <item>curve colours are clusters of strong ink colours inside the plot area; every pixel is explained as a mix of the
    /// background and one cluster colour (coverage α), so anti-aliasing and blur stay with their curve, while light gridlines
    /// and mixed colours drop out; black-and-white pictures simply give one black cluster;</item>
    /// <item>per cluster the columns are scanned from left to right: vertical runs of ink are matched to the curves being
    /// traced by their predicted position (slope from the last points); a run wider than one line shared by two curves is a
    /// crossing, where both keep their slope; curves continue over gaps (dashes, crossings) and are joined afterwards when
    /// one continues the other;</item>
    /// <item>short traces are dropped (text, legend); groups of short straight parallel traces are reported as hatching;
    /// many regular gaps make a curve dashed (dotted for very short dashes);</item>
    /// <item>pixels become data coordinates through two known values per axis (<see cref="DigitizeOptions"/>) or the axis
    /// labels read by an <see cref="ITextReader"/>.</item>
    /// </list>
    /// Pure computation on the pixel buffer; never throws, problems are reported as <see cref="ScanResult.Warnings"/>.
    /// </summary>
    public static class CurveDigitizer
    {
        /// <summary>Difference per channel from the background below which a pixel is background.</summary>
        private const int InkThreshold = 30;

        /// <summary>Difference per channel from the background from which a pixel is strong enough to define a curve colour.</summary>
        private const int StrongThreshold = 80;

        /// <summary>Coverage from which a pixel belongs to a curve.</summary>
        private const double MaskAlpha = 0.45;

        /// <summary>Largest thickness of an axis or gridline in pixels.</summary>
        private const int MaxLineThickness = 6;

        public static ScanResult Digitize(PixelImage image, DigitizeOptions? options = null)
        {
            options ??= new DigitizeOptions();
            if (image is null || !image.IsValid)
                return Fail(new I18nText("The picture is empty or its pixel data is incomplete.", "Das Bild ist leer oder seine Pixeldaten sind unvollständig."));
            int w = image.Width, h = image.Height;
            if (w < 16 || h < 16)
                return Fail(new I18nText("The picture is too small to trace curves (at least 16 × 16 pixels).", "Das Bild ist zu klein, um Kurven zu verfolgen (mindestens 16 × 16 Pixel)."));
            var px = image.ToArgb();
            int bg = BarScanner.Background(px);
            int ink = 0;
            for (int i = 0; i < px.Length; i++)
                if (PixelColor.Distance(px[i], bg) > InkThreshold) ink++;
            if (ink < 10)
                return Fail(new I18nText("The picture is blank: nothing differs from the background.", "Das Bild ist leer: nichts hebt sich vom Hintergrund ab."), bg);

            var warnings = new List<I18nText>();
            var horizontal = FindLines(px, w, h, bg, horizontal: true);
            var vertical = FindLines(px, w, h, bg, horizontal: false);
            ScanLine? xAxis = PickAxis(horizontal, lowest: true);
            ScanLine? yAxis = PickAxis(vertical, lowest: false);
            var plot = options.PlotArea ?? PlotArea(xAxis, yAxis, horizontal, vertical, px, w, h, bg);
            if (plot is null || plot.Width < 8 || plot.Height < 8)
                return Fail(new I18nText("No plot area found.", "Keine Zeichenfläche gefunden."), bg);
            if (xAxis is null || yAxis is null)
                warnings.Add(new I18nText("Axes not found completely; the plot area is estimated from the lines and the ink.", "Achsen nicht vollständig gefunden; die Zeichenfläche ist aus den Linien und der Tinte geschätzt."));

            // Lines (axes, gridlines, frame) are masked out where their colour shows.
            var lines = horizontal.Concat(vertical).ToList();
            var blocked = new bool[w * h];
            foreach (var line in lines) Block(blocked, px, w, h, bg, line);
            var gridlines = lines.Where(l => !ReferenceEquals(l, xAxis) && !ReferenceEquals(l, yAxis)).ToList();

            // Interior of the plot area.
            int x0 = Math.Max(0, plot.X), x1 = Math.Min(w - 1, plot.Right - 1), y0 = Math.Max(0, plot.Y), y1 = Math.Min(h - 1, plot.Bottom - 1);
            var area = new Area(x0, y0, x1, y1);
            var clusters = Clusters(px, w, bg, blocked, area);
            var series = new List<ScanSeries>();
            var hatches = new List<HatchedRegion>();
            var traced = new List<(Track Track, int Color, double Width)>();
            bool filled = false;
            foreach (var color in clusters)
            {
                var alpha = AlphaMap(px, w, bg, blocked, area, color, clusters);
                var runs = ColumnRuns(alpha, area);
                var lengths = runs.SelectMany(r => r).Select(r => (double)r.Length).OrderBy(l => l).ToList();
                if (lengths.Count < 5) continue;
                double width = Math.Max(1, lengths[(int)(0.3 * (lengths.Count - 1))]);
                if (width > Math.Max(8, 0.08 * area.Height))
                {
                    filled = true;
                    continue;
                }
                var tracks = Trace(runs, area, width);
                if (options.DetectHatching) hatches.AddRange(Hatching(tracks, color, width));
                double minSpan = Math.Max(5, options.MinSpan * area.Width);
                foreach (var t in tracks.Where(t => !t.Hatch && t.Span >= minSpan && t.Points.Count >= Math.Max(5, 0.25 * t.Span)))
                    traced.Add((t, color, width));
            }
            if (filled)
                warnings.Add(new I18nText("Filled areas found (bars or areas); use the bar scanner for bar charts.", "Gefüllte Flächen gefunden (Balken oder Flächen); für Balkendiagramme den Balkenscanner verwenden."));
            traced = RemoveDuplicates(traced);
            if (traced.Count > Math.Max(1, options.MaxCurves))
            {
                warnings.Add(new I18nText("More curves found than requested; only the longest ones are returned.", "Mehr Kurven gefunden als verlangt; nur die längsten werden geliefert."));
                traced = traced.OrderByDescending(t => t.Track.Points.Count).Take(Math.Max(1, options.MaxCurves)).ToList();
            }

            // Calibration: given axes first, else from the labels.
            var xCal = options.XAxis is { IsValid: true } ? options.XAxis : null;
            var yCal = options.YAxis is { IsValid: true } ? options.YAxis : null;
            var source = CalibrationSource.None;
            IReadOnlyList<RecognizedText> labels = Array.Empty<RecognizedText>();
            if ((xCal is null || yCal is null) && options.TextReader is not null)
            {
                IReadOnlyList<RecognizedText> words;
                try
                {
                    words = options.TextReader.Read(image, new TextReadHints(bg, Array.Empty<ColorRegion>())) ?? Array.Empty<RecognizedText>();
                }
                catch (Exception)
                {
                    // A host reader must not throw; if it does, the scan continues without labels.
                    words = Array.Empty<RecognizedText>();
                    warnings.Add(new I18nText("The text recognition failed; no axis labels were read.", "Die Texterkennung ist fehlgeschlagen; keine Achsenbeschriftungen gelesen."));
                }
                var (lx, ly, used) = CurveLabelCalibration.FromLabels(plot, words, options.GermanNumbersFirst);
                if ((xCal is null && lx is not null) || (yCal is null && ly is not null)) source = CalibrationSource.Labels;
                xCal ??= lx;
                yCal ??= ly;
                labels = used;
            }
            ChartCalibration? calibration = null;
            if (xCal is not null && yCal is not null)
            {
                calibration = new ChartCalibration(xCal, yCal);
                if (source == CalibrationSource.None) source = CalibrationSource.User;
            }
            else
            {
                source = CalibrationSource.None;
                warnings.Add(new I18nText("No axis calibration: give two known values per axis (or provide a text reader); points are in pixels.",
                    "Keine Achsenkalibrierung: je Achse zwei bekannte Werte angeben (oder eine Texterkennung bereitstellen); Punkte sind in Pixeln."));
            }

            foreach (var (track, color, width) in traced.OrderBy(t => t.Track.Points.Average(p => p.Y)))
            {
                var pixels = track.Points.Select(p => new Vec2(p.X, p.Y)).ToList();
                var data = calibration is null ? null : pixels.Select(calibration.ToData).ToList();
                var (style, coverage) = Style(track, width);
                series.Add(new ScanSeries(color, style, pixels, data, width, coverage));
            }
            if (series.Count == 0)
                warnings.Add(new I18nText("No curves found in the plot area.", "Keine Kurven in der Zeichenfläche gefunden."));
            return new ScanResult
            {
                PlotArea = plot,
                XAxis = xAxis,
                YAxis = yAxis,
                Gridlines = gridlines,
                Series = series,
                Hatches = hatches,
                Background = bg,
                Calibration = calibration,
                CalibrationSource = source,
                AxisLabels = labels,
                Warnings = warnings,
            };
        }

        private static ScanResult Fail(I18nText warning, int background = -1) =>
            new() { Background = background == -1 ? PixelColor.White : background, Warnings = new[] { warning } };

        // ------------------------------------------------------------------ Lines and plot area

        private static bool LineInk(int p, int bg) => PixelColor.Distance(p, bg) > InkThreshold && PixelColor.Chroma(p) <= 40;

        /// <summary>
        /// Long straight lines: per row (or column) the longest run of grey ink; gaps of up to 2 background pixels and crossings
        /// of coloured ink (curves) up to 16 pixels are bridged; rows with a run of at least 30 % of the picture (and 20
        /// pixels) form bands of up to <see cref="MaxLineThickness"/> rows. Only lines at least 60 % as long as the longest
        /// one of the same direction are kept (a flat stretch of a black curve is no gridline).
        /// </summary>
        private static List<ScanLine> FindLines(int[] px, int w, int h, int bg, bool horizontal)
        {
            int n = horizontal ? h : w, len = horizontal ? w : h;
            int min = Math.Max(20, (int)(0.3 * len));
            var best = new (int From, int To)?[n];
            for (int k = 0; k < n; k++)
            {
                int start = -1, last = -1, bestFrom = 0, bestTo = -1, background = 0, other = 0;
                for (int i = 0; i < len; i++)
                {
                    int p = horizontal ? px[k * w + i] : px[i * w + k];
                    if (!LineInk(p, bg))
                    {
                        if (start < 0) continue;
                        if (PixelColor.Distance(p, bg) > InkThreshold) other++;
                        else background++;
                        if (background > 2 || other > 16) start = -1;
                        continue;
                    }
                    if (start < 0) start = i;
                    background = 0;
                    other = 0;
                    last = i;
                    if (last - start > bestTo - bestFrom)
                    {
                        bestFrom = start;
                        bestTo = last;
                    }
                }
                if (bestTo - bestFrom + 1 >= min) best[k] = (bestFrom, bestTo);
            }
            var result = new List<ScanLine>();
            for (int k = 0; k < n; k++)
            {
                if (best[k] is null) continue;
                int e = k;
                while (e + 1 < n && best[e + 1] is not null) e++;
                int thickness = e - k + 1;
                if (thickness <= MaxLineThickness)
                {
                    int from = Enumerable.Range(k, thickness).Min(i => best[i]!.Value.From);
                    int to = Enumerable.Range(k, thickness).Max(i => best[i]!.Value.To);
                    // Colour: the darkest row of the band (anti-aliased neighbours are lighter), sampled along its run.
                    int core = Enumerable.Range(k, thickness).OrderByDescending(i => best[i]!.Value.To - best[i]!.Value.From).ThenBy(i => Darkness(px, w, horizontal, i, best[i]!.Value)).First();
                    result.Add(new ScanLine(horizontal, (k + e) / 2.0, from, to, thickness, MedianColor(px, w, horizontal, core, best[core]!.Value)));
                }
                k = e;
            }
            if (result.Count == 0) return result;
            double longest = result.Max(l => l.Length);
            return result.Where(l => l.Length >= 0.6 * longest).ToList();
        }

        private static double Darkness(int[] px, int w, bool horizontal, int k, (int From, int To) run)
        {
            long sum = 0;
            int count = 0;
            for (int i = run.From; i <= run.To; i += 3)
            {
                int p = horizontal ? px[k * w + i] : px[i * w + k];
                sum += PixelColor.R(p) + PixelColor.G(p) + PixelColor.B(p);
                count++;
            }
            return count == 0 ? 0 : sum / (double)count;
        }

        private static int MedianColor(int[] px, int w, bool horizontal, int k, (int From, int To) run)
        {
            var samples = new List<int>();
            for (int i = run.From; i <= run.To; i++) samples.Add(horizontal ? px[k * w + i] : px[i * w + k]);
            return samples.OrderBy(p => PixelColor.R(p) + PixelColor.G(p) + PixelColor.B(p)).ElementAt(samples.Count / 2);
        }

        /// <summary>The lowest (horizontal) or leftmost (vertical) of the lines that are at least 60 % as long as the longest.</summary>
        private static ScanLine? PickAxis(List<ScanLine> lines, bool lowest)
        {
            if (lines.Count == 0) return null;
            double longest = lines.Max(l => l.Length);
            var candidates = lines.Where(l => l.Length >= 0.6 * longest).ToList();
            return lowest ? candidates.OrderByDescending(l => l.Position).First() : candidates.OrderBy(l => l.Position).First();
        }

        /// <summary>
        /// Plot area: from the y axis to the end of the x axis, from the top of the y axis down to the x axis; a missing axis
        /// is replaced by the outermost long line on that side or by the bounding box of the ink.
        /// </summary>
        private static PixelRect? PlotArea(ScanLine? xAxis, ScanLine? yAxis, List<ScanLine> horizontal, List<ScanLine> vertical, int[] px, int w, int h, int bg)
        {
            int ix0 = w, iy0 = h, ix1 = -1, iy1 = -1;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    if (PixelColor.Distance(px[y * w + x], bg) > InkThreshold)
                    {
                        if (x < ix0) ix0 = x;
                        if (x > ix1) ix1 = x;
                        if (y < iy0) iy0 = y;
                        if (y > iy1) iy1 = y;
                    }
            if (ix1 < 0) return null;
            double left, right, top, bottom;
            var longH = horizontal.Where(l => xAxis is null || l.Length >= 0.6 * xAxis.Length).ToList();
            var longV = vertical.Where(l => yAxis is null || l.Length >= 0.6 * yAxis.Length).ToList();
            if (xAxis is not null && yAxis is not null)
            {
                // Axes meeting at the bottom left corner bound the area; an axis crossing in the middle (negative values)
                // does not, then the extent of the other axis does. Tick marks beyond the corner are ignored.
                left = Math.Abs(yAxis.Position - xAxis.From) <= 0.15 * xAxis.Length ? yAxis.Position : xAxis.From;
                bottom = Math.Abs(xAxis.Position - yAxis.To) <= 0.15 * yAxis.Length ? xAxis.Position : yAxis.To;
                right = Math.Max(xAxis.To, longV.Count > 0 ? longV.Max(l => l.Position) : xAxis.To);
                top = Math.Min(yAxis.From, longH.Count > 0 ? longH.Min(l => l.Position) : yAxis.From);
            }
            else if (xAxis is not null)
            {
                left = xAxis.From;
                right = xAxis.To;
                bottom = xAxis.Position;
                top = longH.Count > 1 ? longH.Min(l => l.Position) : iy0;
            }
            else if (yAxis is not null)
            {
                left = yAxis.Position;
                top = yAxis.From;
                bottom = yAxis.To;
                right = longV.Count > 1 ? longV.Max(l => l.Position) : ix1;
            }
            else
            {
                left = ix0;
                right = ix1;
                top = iy0;
                bottom = iy1;
            }
            int l0 = (int)Math.Floor(left), t0 = (int)Math.Floor(top);
            return new PixelRect(l0, t0, (int)Math.Ceiling(right) - l0 + 1, (int)Math.Ceiling(bottom) - t0 + 1);
        }

        /// <summary>
        /// Marks the grey pixels of a line and up to two pixels beside it whose colour is the line colour or a mix of it with
        /// the background (anti-aliasing, blur).
        /// </summary>
        private static void Block(bool[] blocked, int[] px, int w, int h, int bg, ScanLine line)
        {
            double reach = line.Thickness / 2.0 + 2;
            for (int k = (int)Math.Floor(line.Position - reach); k <= (int)Math.Ceiling(line.Position + reach); k++)
                for (int i = (int)line.From - 1; i <= (int)line.To + 1; i++)
                {
                    int x = line.Horizontal ? i : k, y = line.Horizontal ? k : i;
                    if (x < 0 || y < 0 || x >= w || y >= h) continue;
                    int p = px[y * w + x];
                    if (PixelColor.Chroma(p) > 40) continue;
                    if (PixelColor.Distance(p, line.Color) <= 60 || Explain(p, bg, line.Color) is >= 0.1) blocked[y * w + x] = true;
                }
        }

        // ------------------------------------------------------------------ Colours

        private readonly struct Area
        {
            public Area(int x0, int y0, int x1, int y1)
            {
                X0 = x0;
                Y0 = y0;
                X1 = x1;
                Y1 = y1;
            }

            public int X0 { get; }
            public int Y0 { get; }
            public int X1 { get; }
            public int Y1 { get; }
            public int Width => X1 - X0 + 1;
            public int Height => Y1 - Y0 + 1;
        }

        /// <summary>
        /// Coverage of the pixel as a mix of background and colour: α with p − bg ≈ α (c − bg); NaN if the remainder is too
        /// large (a different colour).
        /// </summary>
        private static double? Explain(int p, int bg, int c)
        {
            double vr = PixelColor.R(p) - PixelColor.R(bg), vg = PixelColor.G(p) - PixelColor.G(bg), vb = PixelColor.B(p) - PixelColor.B(bg);
            double ur = PixelColor.R(c) - PixelColor.R(bg), ug = PixelColor.G(c) - PixelColor.G(bg), ub = PixelColor.B(c) - PixelColor.B(bg);
            double uu = ur * ur + ug * ug + ub * ub;
            if (uu < 1) return null;
            double a = (vr * ur + vg * ug + vb * ub) / uu;
            double er = vr - a * ur, eg = vg - a * ug, eb = vb - a * ub;
            double residual = Math.Sqrt(er * er + eg * eg + eb * eb);
            double v = Math.Sqrt(vr * vr + vg * vg + vb * vb);
            if (a > 1.35 || residual > 30 + 0.12 * v) return null;
            return a;
        }

        /// <summary>
        /// Curve colours: histogram of the strong ink (5 bits per channel) inside the plot area; frequent bins from the
        /// strongest (farthest from the background) down become new colours unless an existing colour explains them as a mix
        /// with the background (so the light edges of a blurred line join its core colour).
        /// </summary>
        private static List<int> Clusters(int[] px, int w, int bg, bool[] blocked, Area area)
        {
            var bins = new Dictionary<int, (int Count, long R, long G, long B)>();
            for (int y = area.Y0; y <= area.Y1; y++)
                for (int x = area.X0; x <= area.X1; x++)
                {
                    int i = y * w + x;
                    if (blocked[i]) continue;
                    int p = px[i];
                    if (PixelColor.Distance(p, bg) < StrongThreshold) continue;
                    int key = ((p >> 19) & 0x1F) << 10 | ((p >> 11) & 0x1F) << 5 | ((p >> 3) & 0x1F);
                    bins.TryGetValue(key, out var b);
                    bins[key] = (b.Count + 1, b.R + PixelColor.R(p), b.G + PixelColor.G(p), b.B + PixelColor.B(p));
                }
            int minBin = Math.Max(6, area.Width / 40);
            var colors = new List<(int Color, int Count)>();
            var frequent = bins.Values.Where(b => b.Count >= minBin)
                .Select(b => (Color: PixelColor.Rgb((int)(b.R / b.Count), (int)(b.G / b.Count), (int)(b.B / b.Count)), b.Count))
                .OrderByDescending(b => PixelColor.Distance(b.Color, bg)).ThenByDescending(b => b.Count).ToList();
            foreach (var bin in frequent)
            {
                int c = bin.Color;
                int k = colors.FindIndex(o => Explain(c, bg, o.Color) is double a && a >= 0.35);
                if (k >= 0) colors[k] = (colors[k].Color, colors[k].Count + bin.Count);
                else if (colors.Count < 16) colors.Add((c, bin.Count));
            }
            int minCount = Math.Max(15, area.Width / 8);
            return colors.Where(c => c.Count >= minCount).Select(c => c.Color).ToList();
        }

        /// <summary>Coverage map of one colour over the area (0 where another colour explains the pixel better).</summary>
        private static float[] AlphaMap(int[] px, int w, int bg, bool[] blocked, Area area, int color, List<int> all)
        {
            var alpha = new float[area.Width * area.Height];
            for (int y = area.Y0; y <= area.Y1; y++)
                for (int x = area.X0; x <= area.X1; x++)
                {
                    int i = y * w + x;
                    if (blocked[i]) continue;
                    int p = px[i];
                    if (PixelColor.Distance(p, bg) <= InkThreshold / 2) continue;
                    if (Explain(p, bg, color) is not double a || a < 0.25) continue;
                    // Belongs to the colour that explains it with the smallest remainder.
                    double own = Residual(p, bg, color, a);
                    bool other = false;
                    foreach (var c in all)
                    {
                        if (c == color || Explain(p, bg, c) is not double b || b < 0.25) continue;
                        if (Residual(p, bg, c, b) < own) { other = true; break; }
                    }
                    if (!other) alpha[(y - area.Y0) * area.Width + (x - area.X0)] = (float)Math.Min(1, a);
                }
            return alpha;
        }

        private static double Residual(int p, int bg, int c, double a)
        {
            double er = PixelColor.R(p) - PixelColor.R(bg) - a * (PixelColor.R(c) - PixelColor.R(bg));
            double eg = PixelColor.G(p) - PixelColor.G(bg) - a * (PixelColor.G(c) - PixelColor.G(bg));
            double eb = PixelColor.B(p) - PixelColor.B(bg) - a * (PixelColor.B(c) - PixelColor.B(bg));
            return er * er + eg * eg + eb * eb;
        }

        // ------------------------------------------------------------------ Tracing

        private sealed class Run
        {
            public int X, Y0, Y1;
            public double Center, Weight;
            public readonly List<Track> Owners = new();
            public int Length => Y1 - Y0 + 1;
        }

        private readonly struct TrackPoint
        {
            public TrackPoint(int x, double y, bool shared)
            {
                X = x;
                Y = y;
                Shared = shared;
            }

            public int X { get; }
            public double Y { get; }
            public bool Shared { get; }
        }

        private sealed class Track
        {
            public readonly List<TrackPoint> Points = new();
            public double Y, Slope;
            public int LastX;
            public bool Closed, Hatch;

            public int FirstX => Points[0].X;
            public double Span => Points.Count == 0 ? 0 : LastX - FirstX + 1;

            public double Predict(int x) => Y + Slope * (x - LastX);

            public void Add(int x, double y, bool shared)
            {
                Points.Add(new TrackPoint(x, y, shared));
                Y = y;
                LastX = x;
                Slope = RecentSlope(Points, Points.Count - 1, 6, 12, Slope);
            }
        }

        /// <summary>Least-squares slope of the points before (and including) index <paramref name="end"/>, at most <paramref name="count"/> within <paramref name="reach"/> columns.</summary>
        private static double RecentSlope(List<TrackPoint> points, int end, int count, int reach, double fallback)
        {
            int start = end;
            while (start > 0 && end - start + 1 < count && points[end].X - points[start - 1].X <= reach) start--;
            int n = end - start + 1;
            if (n < 2) return fallback;
            double mx = 0, my = 0;
            for (int i = start; i <= end; i++)
            {
                mx += points[i].X;
                my += points[i].Y;
            }
            mx /= n;
            my /= n;
            double sxx = 0, sxy = 0;
            for (int i = start; i <= end; i++)
            {
                sxx += (points[i].X - mx) * (points[i].X - mx);
                sxy += (points[i].X - mx) * (points[i].Y - my);
            }
            return sxx < 1e-9 ? fallback : sxy / sxx;
        }

        /// <summary>Vertical runs of covered pixels per column (gaps of one pixel bridged), with the coverage-weighted centre.</summary>
        private static List<Run>[] ColumnRuns(float[] alpha, Area area)
        {
            int aw = area.Width, ah = area.Height;
            var columns = new List<Run>[aw];
            for (int x = 0; x < aw; x++)
            {
                var list = new List<Run>();
                int start = -1, last = -1;
                for (int y = 0; y <= ah; y++)
                {
                    bool on = y < ah && alpha[y * aw + x] >= MaskAlpha;
                    if (on)
                    {
                        if (start < 0) start = y;
                        else if (y - last > 2)
                        {
                            Close(start, last);
                            start = y;
                        }
                        last = y;
                    }
                }
                if (start >= 0) Close(start, last);
                columns[x] = list;

                void Close(int s, int e)
                {
                    double sum = 0, wy = 0;
                    for (int y = Math.Max(0, s - 1); y <= Math.Min(ah - 1, e + 1); y++)
                    {
                        double a = alpha[y * aw + x];
                        sum += a;
                        wy += a * y;
                    }
                    if (sum < 0.8) return;
                    list.Add(new Run { X = x + area.X0, Y0 = s + area.Y0, Y1 = e + area.Y0, Center = wy / sum + area.Y0, Weight = sum });
                }
            }
            return columns;
        }

        /// <summary>
        /// Traces the curves of one colour column by column: runs are matched to the curves by their predicted position,
        /// shared runs (crossings) keep both slopes, unmatched runs start new traces; afterwards traces that continue each
        /// other are joined.
        /// </summary>
        private static List<Track> Trace(List<Run>[] columns, Area area, double width)
        {
            int maxGap = (int)Math.Max(10, Math.Max(4 * width + 6, 0.04 * area.Width));
            var tracks = new List<Track>();
            var active = new List<Track>();
            for (int c = 0; c < columns.Length; c++)
            {
                int x = c + area.X0;
                var runs = columns[c];
                var pairs = new List<(Track T, Run R, double Cost, double Distance)>();
                foreach (var t in active)
                {
                    int dx = x - t.LastX;
                    double pred = t.Predict(x);
                    double gate = 2 + 0.75 * width + 0.5 * Math.Abs(t.Slope) + 0.2 * (dx - 1);
                    foreach (var r in runs)
                    {
                        double d = pred < r.Y0 ? r.Y0 - pred : pred > r.Y1 ? pred - r.Y1 : 0;
                        if (d <= gate) pairs.Add((t, r, d + 0.1 * Math.Abs(pred - r.Center), d));
                    }
                }
                var assigned = new HashSet<Track>();
                foreach (var (t, r, _, d) in pairs.OrderBy(p => p.Cost))
                {
                    if (assigned.Contains(t)) continue;
                    // A run may serve several curves (crossing) only for those predicted inside it.
                    if (r.Owners.Count > 0 && d > 1) continue;
                    r.Owners.Add(t);
                    assigned.Add(t);
                }
                foreach (var r in runs)
                {
                    // Two traces with the same position and slope follow the same line (a split run started a second one):
                    // the younger one ends.
                    for (int i = r.Owners.Count - 1; i > 0; i--)
                        for (int j = 0; j < i; j++)
                        {
                            Track a = r.Owners[i], b = r.Owners[j];
                            if (Math.Abs(a.Predict(x) - b.Predict(x)) >= width || Math.Abs(a.Slope - b.Slope) >= 0.15 + 0.1 * Math.Max(Math.Abs(a.Slope), Math.Abs(b.Slope))) continue;
                            var younger = a.Points.Count <= b.Points.Count ? a : b;
                            younger.Closed = true;
                            r.Owners.Remove(younger);
                            assigned.Remove(younger);
                            break;
                        }
                    if (r.Owners.Count == 0)
                    {
                        var t = new Track();
                        t.Add(x, r.Center, false);
                        tracks.Add(t);
                        active.Add(t);
                        continue;
                    }
                    double lo = r.Y0 + width / 2, hi = r.Y1 - width / 2;
                    foreach (var t in r.Owners)
                    {
                        double pred = t.Predict(x);
                        // A line of slope s and width w covers about |s| + w·√(1 + s²) rows per column.
                        double expected = Math.Abs(t.Slope) + width * Math.Sqrt(1 + t.Slope * t.Slope) + 1;
                        bool shared = r.Owners.Count > 1 || (t.Points.Count >= 3 && r.Length > expected + width + 2);
                        double y = shared && lo <= hi ? Math.Max(lo, Math.Min(hi, pred)) : r.Center;
                        t.Add(x, y, shared);
                    }
                    r.Owners.Clear();
                }
                foreach (var t in active)
                    if (!t.Closed && !assigned.Contains(t) && x - t.LastX > maxGap) t.Closed = true;
                active.RemoveAll(t => t.Closed);
            }
            foreach (var t in active) t.Closed = true;
            return Join(tracks, width, maxGap);
        }

        /// <summary>Joins traces where one continues another after a longer gap (same predicted position and slope).</summary>
        private static List<Track> Join(List<Track> tracks, double width, int maxGap)
        {
            var list = tracks.Where(t => t.Points.Count > 0).OrderBy(t => t.FirstX).ToList();
            bool changed = true;
            while (changed)
            {
                changed = false;
                foreach (var a in list)
                {
                    double endSlope = RecentSlope(a.Points, a.Points.Count - 1, 8, 20, a.Slope);
                    Track? best = null;
                    double bestCost = double.MaxValue;
                    foreach (var b in list)
                    {
                        // The continuation starts after the end of a, or overlaps it by a few columns (a fragment started
                        // beside a crossing while a still had its last points).
                        if (b == a || b.FirstX <= a.LastX - 3 || b.LastX <= a.LastX || b.FirstX - a.LastX > 4 * maxGap) continue;
                        int k = a.Points.Count - 1;
                        while (k >= 0 && a.Points[k].X >= b.FirstX) k--;
                        if (k < 0) continue;
                        int dx = b.FirstX - a.Points[k].X;
                        double pred = a.Points[k].Y + endSlope * dx;
                        double gate = 2 + width + 0.15 * dx + 0.3 * Math.Abs(endSlope);
                        double d = Math.Abs(b.Points[0].Y - pred);
                        if (d > gate) continue;
                        double startSlope = RecentSlope(b.Points, Math.Min(b.Points.Count - 1, 7), 8, 20, endSlope);
                        if (b.Points.Count >= 4 && Math.Abs(startSlope - endSlope) > 0.5 + 0.5 * Math.Abs(endSlope)) continue;
                        double cost = d + 0.05 * dx;
                        if (cost < bestCost)
                        {
                            bestCost = cost;
                            best = b;
                        }
                    }
                    if (best is null) continue;
                    a.Points.RemoveAll(p => p.X >= best.FirstX);
                    foreach (var p in best.Points) a.Points.Add(p);
                    a.Y = best.Y;
                    a.LastX = best.LastX;
                    a.Slope = best.Slope;
                    list.Remove(best);
                    changed = true;
                    break;
                }
            }
            return list;
        }

        /// <summary>Drops traces that run along another trace of the same colour (split thick lines).</summary>
        private static List<(Track Track, int Color, double Width)> RemoveDuplicates(List<(Track Track, int Color, double Width)> traced)
        {
            var keep = traced.OrderByDescending(t => t.Track.Points.Count).ToList();
            for (int i = keep.Count - 1; i > 0; i--)
            {
                var small = keep[i];
                var ys = small.Track.Points.ToDictionary(p => p.X, p => p.Y);
                for (int j = 0; j < i; j++)
                {
                    var big = keep[j];
                    if (big.Color != small.Color) continue;
                    var diffs = big.Track.Points.Where(p => ys.ContainsKey(p.X)).Select(p => Math.Abs(p.Y - ys[p.X])).OrderBy(d => d).ToList();
                    if (diffs.Count < 0.5 * small.Track.Points.Count) continue;
                    if (diffs[diffs.Count / 2] <= Math.Max(1.5, small.Width))
                    {
                        keep.RemoveAt(i);
                        break;
                    }
                }
            }
            return keep;
        }

        // ------------------------------------------------------------------ Hatching and style

        /// <summary>
        /// Groups of at least four straight, short, parallel traces of one colour that overlap each other form a hatched
        /// region; the traces are marked and not reported as curves.
        /// </summary>
        private static IEnumerable<HatchedRegion> Hatching(List<Track> tracks, int color, double width)
        {
            var straight = new List<(Track T, double Slope, double Intercept)>();
            foreach (var t in tracks)
            {
                if (t.Points.Count < 4) continue;
                var (slope, intercept, rms) = Line(t.Points);
                if (rms <= 0.8 && Math.Abs(slope) >= 0.25 && Math.Abs(slope) <= 8) straight.Add((t, slope, intercept));
            }
            var used = new HashSet<Track>();
            foreach (var seed in straight.OrderBy(s => s.T.FirstX))
            {
                if (used.Contains(seed.T)) continue;
                var group = new List<(Track T, double Slope, double Intercept)> { seed };
                bool grown = true;
                while (grown)
                {
                    grown = false;
                    foreach (var s in straight)
                    {
                        if (used.Contains(s.T) || group.Any(g => g.T == s.T)) continue;
                        if (Math.Abs(s.Slope - seed.Slope) > 0.15 * Math.Max(1, Math.Abs(seed.Slope))) continue;
                        // Neighbouring: horizontal extents overlap or touch some member's.
                        if (!group.Any(g => s.T.FirstX <= g.T.LastX + 2 * width + 4 && g.T.FirstX <= s.T.LastX + 2 * width + 4)) continue;
                        group.Add(s);
                        grown = true;
                    }
                }
                if (group.Count < 4) continue;
                foreach (var g in group)
                {
                    used.Add(g.T);
                    g.T.Hatch = true;
                }
                int bx0 = group.Min(g => g.T.FirstX), bx1 = group.Max(g => g.T.LastX);
                double by0 = group.Min(g => g.T.Points.Min(p => p.Y)), by1 = group.Max(g => g.T.Points.Max(p => p.Y));
                double slope = group.Average(g => g.Slope);
                // Spacing perpendicular to the hatch lines: median distance of neighbouring intercepts.
                var intercepts = group.Select(g => g.Intercept).OrderBy(v => v).ToList();
                var gaps = intercepts.Zip(intercepts.Skip(1), (a, b) => b - a).Where(d => d > 0.5).OrderBy(d => d).ToList();
                double spacing = gaps.Count == 0 ? 0 : gaps[gaps.Count / 2] / Math.Sqrt(1 + slope * slope);
                double angle = Math.Atan2(-slope, 1) * 180 / Math.PI;
                if (angle < 0) angle += 180;
                yield return new HatchedRegion(new PixelRect(bx0, (int)Math.Floor(by0), bx1 - bx0 + 1, (int)Math.Ceiling(by1) - (int)Math.Floor(by0) + 1), color, Math.Round(angle, 1), Math.Round(spacing, 1));
            }
        }

        private static (double Slope, double Intercept, double Rms) Line(List<TrackPoint> points)
        {
            double mx = points.Average(p => (double)p.X), my = points.Average(p => p.Y);
            double sxx = points.Sum(p => (p.X - mx) * (p.X - mx)), sxy = points.Sum(p => (p.X - mx) * (p.Y - my));
            double slope = sxx < 1e-9 ? 0 : sxy / sxx;
            double intercept = my - slope * mx;
            double rms = Math.Sqrt(points.Average(p => Math.Pow(p.Y - (intercept + slope * p.X), 2)));
            return (slope, intercept, rms);
        }

        /// <summary>
        /// Line style from the gaps of the trace: many regular gaps (at least four, at least one per 60 pixels, coverage
        /// below 90 %) make it dashed, dotted if the dashes are hardly longer than the line is wide.
        /// </summary>
        private static (CurveStyle Style, double Coverage) Style(Track t, double width)
        {
            var xs = t.Points.Select(p => p.X).Distinct().OrderBy(x => x).ToList();
            double span = xs[xs.Count - 1] - xs[0] + 1;
            double coverage = xs.Count / span;
            var dashes = new List<int>();
            int gaps = 0, run = 1;
            for (int i = 1; i < xs.Count; i++)
            {
                int d = xs[i] - xs[i - 1];
                if (d == 1)
                {
                    run++;
                    continue;
                }
                dashes.Add(run);
                run = 1;
                if (d >= 3) gaps++;
            }
            dashes.Add(run);
            bool dashed = gaps >= 4 && gaps >= span / 60 && coverage < 0.9;
            if (!dashed) return (CurveStyle.Solid, coverage);
            var sorted = dashes.OrderBy(d => d).ToList();
            double median = sorted[sorted.Count / 2];
            return (median <= Math.Max(3, 2.2 * width) ? CurveStyle.Dotted : CurveStyle.Dashed, coverage);
        }
    }
}
