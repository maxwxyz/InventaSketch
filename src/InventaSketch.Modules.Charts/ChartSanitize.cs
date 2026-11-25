using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Charts
{
    /// <summary>
    /// Clean slides for sharing (<see cref="Sanitizer"/>) for every chart type: values made unrecognisable while the chart
    /// keeps its look.
    /// <list type="bullet">
    /// <item><b>Normalised:</b> affine maps to 0…1 (log axes: divided by the maximum, so the decades stay), bars divided by
    /// the largest bar or stack, engineering charts with their rated values set to 1 and the ratios kept; units that no
    /// longer apply are cleared. Shapes are exactly the same.</item>
    /// <item><b>Randomised:</b> per value axis a random monotone map (a factor at least 20 % away from 1 and a gentle
    /// bend), per curve smooth noise on the increments (each increment keeps its sign, so monotone curves stay monotone
    /// and peaks stay peaks), bars varied by ±spread around a random factor, rated values varied with their ratios kept
    /// plausible. Reproducible with the seed.</item>
    /// </list>
    /// Data texts are rewritten in the <see cref="ChartData"/> format (comments are dropped), figure specifications through
    /// <see cref="FigureSpec"/>.
    /// </summary>
    internal static class ChartSanitize
    {
        public static SketchObject Apply(ChartType type, SketchObject o, SanitizeOptions opt, Random rng)
        {
            var r = Sanitizer.Basic(type, o, opt, rng);
            var ctx = new Ctx(type, opt, rng);
            if (opt.ReplaceTexts)
            {
                if (r.Str("xlabel", "").Trim().Length > 0) r = r.WithParam("xlabel", "x");
                if (r.Str("ylabel", "").Trim().Length > 0) r = r.WithParam("ylabel", "y");
            }
            r = o.Type switch
            {
                XyChart.TypeId => Xy(r, ctx, XyChart.DefaultData),
                BarChart.TypeId => Bar(r, ctx),
                AxesChart.TypeId => Axes(r, ctx),
                TorqueChart.TypeId => Torque(r, ctx),
                EfficiencyChart.TypeId => Efficiency(r, ctx),
                PumpChart.TypeId => Pump(r, ctx),
                BodeChart.TypeId => Bode(r, ctx),
                BandChart.TypeId => Band(r, ctx),
                FigureChart.TypeId => Figure(r, ctx),
                _ => r,
            };
            return r;
        }

        private sealed class Ctx
        {
            public Ctx(ChartType type, SanitizeOptions opt, Random rng) { Type = type; Opt = opt; Rng = rng; }
            public ChartType Type { get; }
            public SanitizeOptions Opt { get; }
            public Random Rng { get; }
            public bool Norm => Opt.Mode == SanitizeMode.Normalised;
            public bool Rand => Opt.Mode == SanitizeMode.Randomised;
            public double Spread => Opt.Variation;

            /// <summary>Number parameter with the type's default.</summary>
            public double Num(SketchObject o, string key, double fallback)
                => o.Num(key, Type.Parameters.FirstOrDefault(p => p.Key == key)?.Default?.Number ?? fallback);

            /// <summary>Random factor at least 20 % away from 1 (up to 1.2 + 2·spread, or its inverse).</summary>
            public double Off(double reach = 1) => SanitizeText.Away(Rng, Spread, reach);
        }

        // ------------------------------------------------------------------ value maps

        /// <summary>Affine map of [lo, hi] onto [0, 1]; a single value maps to 1 (0 stays 0).</summary>
        private static Func<double, double> Unit(double lo, double hi)
        {
            var span = hi - lo;
            if (!(Math.Abs(span) > 1e-12 * Math.Max(1, Math.Abs(hi)))) return v => Math.Abs(lo) < 1e-300 ? 0 : v / Math.Abs(lo);
            return v => (v - lo) / span;
        }

        /// <summary>Normalising map of an axis: linear 0…1, log divided by the largest value.</summary>
        private static Func<double, double> NormMap(IEnumerable<double> values, bool log)
        {
            var vs = values.Where(Fmt.Finite).Where(v => !log || v > 0).ToList();
            if (vs.Count == 0) return v => v;
            if (log) { var max = vs.Max(); return v => v / max; }
            return Unit(vs.Min(), vs.Max());
        }

        /// <summary>
        /// Random monotone map of an axis: values bent gently inside their range (t + a·t·(1 − t), linear outside), then
        /// multiplied by a factor at least 20 % away from 1; log axes work on the logarithms. Zero stays zero when it is the
        /// lower end of the data.
        /// </summary>
        private static Func<double, double> RandMap(IEnumerable<double> values, bool log, Ctx c)
        {
            var vs = values.Where(Fmt.Finite).Where(v => !log || v > 0).Select(v => log ? Math.Log(v) : v).ToList();
            var k = c.Off();
            var a = SanitizeText.Uniform(c.Rng, -1, 1) * Math.Min(0.6, 2 * c.Spread);
            if (vs.Count == 0) return v => v * k;
            double lo = vs.Min(), hi = vs.Max(), span = hi - lo;
            double H(double t) => t < 0 ? (1 + a) * t : t > 1 ? 1 + (1 - a) * (t - 1) : t + a * t * (1 - t);
            if (log)
            {
                if (span < 1e-12) return v => v * k;
                return v => v > 0 ? k * Math.Exp(lo + span * H((Math.Log(v) - lo) / span)) : v;
            }
            if (span < 1e-12 * Math.Max(1, Math.Abs(hi))) return v => v * k;
            return v => k * (lo + span * H((v - lo) / span));
        }

        /// <summary>
        /// Smooth noise on a curve: each increment is multiplied by a smoothly varying positive factor (signs kept), the
        /// result is fitted back to the original minimum and maximum. Curves with fewer than three points stay.
        /// </summary>
        private static IReadOnlyList<double> Noisy(IReadOnlyList<double> y, Ctx c, double strength = 1)
        {
            var n = y.Count;
            if (n < 3) return y;
            var s = Math.Min(0.8, c.Spread * 1.6 * strength);
            var waves = Enumerable.Range(0, 3).Select(_ => (F: SanitizeText.Uniform(c.Rng, 0.4, 2.5), P: SanitizeText.Uniform(c.Rng, 0, 2 * Math.PI), A: SanitizeText.Uniform(c.Rng, 0.3, 1))).ToList();
            var amp = waves.Sum(w => w.A);
            var z = new double[n];
            z[0] = y[0];
            for (var i = 1; i < n; i++)
            {
                var t = (double)i / (n - 1);
                var wave = waves.Sum(w => w.A * Math.Sin(2 * Math.PI * w.F * t + w.P)) / amp;
                var jitter = SanitizeText.Uniform(c.Rng, -0.25, 0.25);
                var f = Geo.Clamp(1 + s * (wave + jitter), 0.15, 1.85);
                z[i] = z[i - 1] + f * (y[i] - y[i - 1]);
            }
            double ylo = y.Min(), yhi = y.Max(), zlo = z.Min(), zhi = z.Max();
            if (zhi - zlo < 1e-300 || yhi - ylo < 1e-300) return z;
            return z.Select(v => ylo + (v - zlo) * (yhi - ylo) / (zhi - zlo)).ToList();
        }

        // ------------------------------------------------------------------ data text

        private sealed class Row
        {
            public string? Label;
            public double[] V = Array.Empty<double>();
        }

        private sealed class Block
        {
            public string? Name;
            public List<string>? Columns;
            /// <summary>Drawn with the spline (a smooth sketched curve: its memo is dropped, its look kept with <c># spline</c>).</summary>
            public bool Spline;
            public readonly List<Row> Rows = new();
            public bool HasLabels => Rows.Any(r => r.Label is not null);
            public int Cols => Rows.Count == 0 ? 0 : Rows.Max(r => r.V.Length);
            /// <summary>XY reading (<see cref="ChartData.Xy"/>): the x column (null = row number) and the y columns.</summary>
            public (int? X, List<int> Y) XyColumns()
            {
                var cols = Cols;
                if (cols == 0) return (null, new List<int>());
                if (cols == 1 || HasLabels) return (null, Enumerable.Range(0, cols).ToList());
                return (0, Enumerable.Range(1, cols - 1).ToList());
            }

            public IEnumerable<double> Column(int k) => Rows.Where(r => k < r.V.Length && Fmt.Finite(r.V[k])).Select(r => r.V[k]);

            /// <summary>Maps the finite values of column <paramref name="k"/> as one list (row order).</summary>
            public void MapColumn(int k, Func<IReadOnlyList<double>, IReadOnlyList<double>> f)
            {
                var rows = Rows.Where(r => k < r.V.Length && Fmt.Finite(r.V[k])).ToList();
                if (rows.Count == 0) return;
                var mapped = f(rows.Select(r => r.V[k]).ToList());
                for (var i = 0; i < rows.Count && i < mapped.Count; i++) rows[i].V[k] = mapped[i];
            }

            public void MapEach(int k, Func<double, double> f) => MapColumn(k, l => l.Select(f).ToList());
        }

        private static List<Block> Read(string text) => ChartData.Parse(text).Select(b =>
        {
            var x = new Block { Name = b.Name, Columns = b.Columns?.ToList() };
            foreach (var r in b.Rows) x.Rows.Add(new Row { Label = r.Label, V = r.Values.ToArray() });
            return x;
        }).ToList();

        private static string Num(double v) => Fmt.Finite(v) ? SanitizeText.Round(v, 5).ToString("G6", CultureInfo.InvariantCulture) : "";

        private static string Quote(string s) => "\"" + s.Replace("\"", "\"\"") + "\"";

        /// <summary>Writes blocks in the <see cref="ChartData"/> format (semicolons, or tabs when a text contains one).</summary>
        private static string Write(IReadOnlyList<Block> blocks)
        {
            var lines = new List<List<string>?>();
            foreach (var b in blocks)
            {
                if (b.Rows.Count == 0) continue;
                if (lines.Count > 0) lines.Add(null);
                if (b.Spline) lines.Add(new List<string> { ChartSketchInput.SplineComment });
                if (b.Columns is { Count: > 0 }) lines.Add(b.Columns.Select(Quote).ToList());
                else if (!string.IsNullOrEmpty(b.Name)) lines.Add(new List<string> { Quote(b.Name!), "" });
                foreach (var r in b.Rows)
                {
                    var fields = new List<string>();
                    if (r.Label is not null) fields.Add(Quote(r.Label));
                    fields.AddRange(r.V.Select(Num));
                    lines.Add(fields);
                }
            }
            var sep = lines.Any(l => l is not null && l.Any(f => f.IndexOf(';') >= 0)) ? "\t" : ";";
            return string.Join("\n", lines.Select(l => l is null ? "" : string.Join(sep, l)));
        }

        /// <summary>Series names (block names and value column headers) become "Series 1", "Series 2" …</summary>
        private static void RenameSeries(IReadOnlyList<Block> blocks, Ctx c)
        {
            var k = 0;
            string Next() => Common.SeriesWord.Get(c.Opt.Lang) + " " + Fmt.I(++k);
            foreach (var b in blocks)
            {
                if (b.Columns is { Count: > 0 } cols)
                {
                    // The first header names the label column or the x / row label column (several value columns).
                    var skip = b.HasLabels || b.Cols > 1 ? 1 : 0;
                    for (var i = skip; i < cols.Count; i++) if (cols[i].Length > 0) cols[i] = Next();
                }
                else if (!string.IsNullOrEmpty(b.Name)) b.Name = Next();
            }
        }

        /// <summary>Category labels become "A", "B" … (the same label in every block gets the same letter).</summary>
        private static void RenameCategories(IReadOnlyList<Block> blocks, bool numericFirstColumn)
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            string Of(string key) => map.TryGetValue(key, out var v) ? v : map[key] = SanitizeText.Letter(map.Count);
            foreach (var b in blocks)
            {
                var labels = b.HasLabels;
                var cols = b.Cols;
                if (!labels && numericFirstColumn && cols > 1)
                {
                    // Bars: a numeric first column with further values labels the rows (years) – it becomes a text label.
                    foreach (var r in b.Rows)
                        if (r.V.Length > 0)
                        {
                            r.Label = Of(Fmt.Finite(r.V[0]) ? r.V[0].ToString("R", CultureInfo.InvariantCulture) : "");
                            r.V = r.V.Skip(1).ToArray();
                        }
                    continue;
                }
                if (!labels) continue;
                foreach (var r in b.Rows) if (r.Label is not null) r.Label = Of(r.Label);
            }
        }

        private static (double Min, double Max)? MapRange((double Min, double Max)? r, Func<double, double> f)
        {
            if (r is not { } v) return null;
            double a = f(v.Min), b = f(v.Max);
            if (!Fmt.Finite(a) || !Fmt.Finite(b) || Math.Abs(a - b) < 1e-300) return null;
            return (Math.Min(a, b), Math.Max(a, b));
        }

        private static string RangeText((double Min, double Max) r) => Num(r.Min) + ";" + Num(r.Max);

        /// <summary>Maps a range parameter ("min;max") through <paramref name="f"/>; an empty or invalid one stays.</summary>
        private static SketchObject WithRange(SketchObject o, string key, Func<double, double> f)
        {
            if (!o.Params.TryGetValue(key, out var v) || v.Text is null) return o;
            return MapRange(ChartData.Range(v.Text), f) is { } m ? o.WithParam(key, RangeText(m)) : o;
        }

        /// <summary>Maps the values of the XY chart's areas (x: spans, vertical lines; y: bases, lines, bands); labels go with the texts.</summary>
        private static SketchObject WithAreas(SketchObject o, Func<double, double> fx, Func<double, double> fy, bool replaceTexts)
        {
            var areas = XyEditor.Areas(o);
            if (areas.Count == 0) return o;
            double? M(double? v, Func<double, double> f) => v is { } d && Fmt.Finite(d) ? f(d) : v;
            foreach (var r in areas)
            {
                r.From = M(r.From, fx); r.To = M(r.To, fx); r.X2 = M(r.X2, fx);
                r.Base = M(r.Base, fy); r.Y1 = M(r.Y1, fy); r.Y2 = M(r.Y2, fy);
                if (replaceTexts) r.Label = null;
            }
            return o.WithParam("areas", FigureSpec.AreasJson(areas));
        }

        /// <summary>Maps the numbers of a list parameter ("limits").</summary>
        private static SketchObject WithList(SketchObject o, string key, Func<double, double> f)
        {
            if (!o.Params.TryGetValue(key, out var v) || string.IsNullOrWhiteSpace(v.Text)) return o;
            var values = ChartData.Numbers(v.Text);
            return values.Count == 0 ? o : o.WithParam(key, string.Join(";", values.Select(x => Num(f(x)))));
        }

        /// <summary>Clears the unit parameters (normalised values have none).</summary>
        private static SketchObject NoUnits(SketchObject o, params string[] keys)
        {
            foreach (var k in keys) o = o.WithParam(k, "");
            return o;
        }

        private static bool Log(SketchObject o, string key) => o.Str(key, "linear") == "log";

        // ------------------------------------------------------------------ data charts

        private static SketchObject Xy(SketchObject o, Ctx c, string defaultData)
        {
            var text = o.Str("data", defaultData);
            var blocks = Read(text);
            if (blocks.Count == 0) return o;
            var splines = ChartSketchInput.XySplineBlocks(text);
            for (var i = 0; i < blocks.Count && i < splines.Length; i++) blocks[i].Spline = splines[i];
            if (c.Opt.ReplaceTexts) RenameSeries(blocks, c);
            if (c.Opt.RenameCategories) RenameCategories(blocks, false);
            bool xlog = Log(o, "xscale"), ylog = Log(o, "yscale");
            var xs = blocks.SelectMany(b => b.XyColumns().X is int x ? b.Column(x) : Enumerable.Empty<double>()).ToList();
            var ys = blocks.SelectMany(b => b.XyColumns().Y.SelectMany(b.Column)).Concat(ChartData.Numbers(o.Str("limits", ""))).ToList();
            if (c.Opt.Mode == SanitizeMode.None) return c.Opt.ReplaceTexts || c.Opt.RenameCategories ? o.WithParam("data", Write(blocks)) : o;
            Func<double, double> fx, fy;
            if (c.Norm) { fx = NormMap(xs, xlog); fy = NormMap(ys, ylog); }
            else { var kx = c.Off(); fx = v => v * kx; fy = RandMap(ys, ylog, c); }
            foreach (var b in blocks)
            {
                var (x, yc) = b.XyColumns();
                if (x is int xi) b.MapEach(xi, fx);
                foreach (var k in yc) b.MapColumn(k, l => c.Rand ? Noisy(l.Select(fy).ToList(), c) : l.Select(fy).ToList());
            }
            o = o.WithParam("data", Write(blocks));
            o = WithList(o, "limits", fy);
            o = WithAreas(o, fx, fy, c.Opt.ReplaceTexts);
            if (xs.Count > 0) o = WithRange(o, "xrange", fx);
            o = WithRange(o, "yrange", fy);
            if (c.Norm) o = NoUnits(o, xs.Count > 0 ? new[] { "xunit", "yunit" } : new[] { "yunit" });
            return o;
        }

        private static SketchObject Bar(SketchObject o, Ctx c)
        {
            var blocks = Read(o.Str("data", BarChart.DefaultData));
            if (blocks.Count == 0) return o;
            if (c.Opt.ReplaceTexts) RenameSeries(blocks, c);
            if (c.Opt.RenameCategories) RenameCategories(blocks, true);
            if (c.Opt.Mode == SanitizeMode.None) return c.Opt.ReplaceTexts || c.Opt.RenameCategories ? o.WithParam("data", Write(blocks)) : o;
            // Value columns: a numeric first column with further values labels the rows.
            List<int> ValueCols(Block b) => Enumerable.Range(b.HasLabels || b.Cols == 1 ? 0 : 1, Math.Max(0, b.Cols - (b.HasLabels || b.Cols == 1 ? 0 : 1))).ToList();
            Func<double, double> f;
            if (c.Norm)
            {
                double max;
                if (o.Str("mode", "grouped") == "stacked")
                {
                    var set = ChartData.Categories(ChartData.Parse(Write(blocks)));
                    max = Enumerable.Range(0, set.Categories.Count).Select(i => Math.Max(
                        set.Series.Sum(s => Fmt.Finite(s.Values[i]) && s.Values[i] > 0 ? s.Values[i] : 0),
                        -set.Series.Sum(s => Fmt.Finite(s.Values[i]) && s.Values[i] < 0 ? s.Values[i] : 0))).DefaultIfEmpty(0).Max();
                }
                else max = blocks.SelectMany(b => ValueCols(b).SelectMany(b.Column)).Select(Math.Abs).DefaultIfEmpty(0).Max();
                if (max <= 0) max = 1;
                f = v => v / max;
                foreach (var b in blocks) foreach (var k in ValueCols(b)) b.MapEach(k, f);
                o = WithRange(o, "yrange", f);
                return NoUnits(o.WithParam("data", Write(blocks)), "yunit");
            }
            var factor = c.Off();
            foreach (var b in blocks) foreach (var k in ValueCols(b)) b.MapEach(k, v => v * factor * SanitizeText.Vary(c.Rng, c.Spread));
            o = WithRange(o, "yrange", v => v * factor * (1 + c.Spread));
            return o.WithParam("data", Write(blocks));
        }

        private static SketchObject Axes(SketchObject o, Ctx c)
        {
            if (c.Opt.Mode == SanitizeMode.None) return o;
            foreach (var (key, scale, unit) in new[] { ("xrange", "xscale", "xunit"), ("yrange", "yscale", "yunit") })
            {
                var r = ChartData.Range(o.Str(key, "0;10")) ?? (0, 10);
                var log = Log(o, scale) && r.Min > 0;
                Func<double, double> f;
                if (c.Norm) f = NormMap(new[] { r.Min, r.Max }, log);
                else { var k = c.Off(); f = v => SanitizeText.Round(v * k, 2); }
                if (MapRange(r, f) is { } m) o = o.WithParam(key, RangeText(m));
                if (c.Norm) o = o.WithParam(unit, "");
            }
            return o;
        }

        // ------------------------------------------------------------------ engineering charts

        /// <summary>Rated values of a drive (torque and efficiency charts): normalised to 1 with the corner ratio kept, or varied.</summary>
        private static SketchObject Drive(SketchObject o, Ctx c)
        {
            var tmax = Math.Max(1e-6, c.Num(o, "tmax", 300));
            var nmax = Math.Max(1e-6, c.Num(o, "nmax", 12000));
            var ratio = Geo.Clamp(c.Num(o, "nbase", 4000) / nmax, 0.02, 1);
            if (c.Norm)
                return NoUnits(o.WithParam("tmax", 1.0).WithParam("nmax", 1.0).WithParam("nbase", SanitizeText.Round(ratio, 3)), "xunit", "yunit");
            var t = SanitizeText.Round(tmax * c.Off(), 2);
            var n = SanitizeText.Round(nmax * c.Off(), 2);
            var r = ratio >= 0.999 ? 1 : Geo.Clamp(ratio * SanitizeText.Vary(c.Rng, c.Spread * 0.6), 0.05, 0.95);
            return o.WithParam("tmax", t).WithParam("nmax", n).WithParam("nbase", SanitizeText.Round(n * r, 2));
        }

        private static SketchObject Torque(SketchObject o, Ctx c)
        {
            if (c.Opt.Mode == SanitizeMode.None) return o;
            o = Drive(o, c);
            if (c.Norm && o.Flag("power", true))
            {
                // The power axis has no neutral scale (kW from normalised torque and speed): its numbers are hidden.
                var parts = o.Parts is null ? new Dictionary<string, PartLook>(StringComparer.Ordinal) : o.Parts.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);
                parts[ChartTags.TickLabel + ".y2"] = new PartLook { Hidden = true };
                o = o with { Parts = parts };
            }
            return o;
        }

        /// <summary>
        /// Efficiency map: the contour shape depends only on the odds l/(100 − l) of each level relative to the odds of the
        /// peak, so peak and levels are moved together with those ratios kept (same map, other numbers). Normalised: peak 90 %
        /// and the contour labels off; randomised: the peak moved by a few points.
        /// </summary>
        private static SketchObject Efficiency(SketchObject o, Ctx c)
        {
            if (c.Opt.Mode == SanitizeMode.None) return o;
            o = Drive(o, c);
            var peak = Geo.Clamp(c.Num(o, "etamax", 96), 1, 99.9);
            double target;
            if (c.Norm) target = 90;
            else
            {
                var d = SanitizeText.Uniform(c.Rng, 1, 1 + 8 * c.Spread) * (c.Rng.NextDouble() < 0.5 ? -1 : 1);
                target = Math.Round(Geo.Clamp(peak + d, 60, 99), 1);
                if (Math.Abs(target - peak) < 0.5) target = Math.Round(Geo.Clamp(peak - 2, 60, 99), 1);
            }
            double Odds(double l) => l / (100 - l);
            var q = Odds(target) / Odds(peak);
            var levels = ChartData.Numbers(o.Str("levels", EfficiencyChart.DefaultLevels))
                .Select(l => l > 0 && l < 100 ? Math.Round(100 * Odds(l) * q / (1 + Odds(l) * q), 2) : l).ToList();
            o = o.WithParam("etamax", target).WithParam("levels", string.Join(";", levels.Select(Num)));
            return c.Norm ? o.WithParam("labels", false) : o;
        }

        private static SketchObject Pump(SketchObject o, Ctx c)
        {
            if (c.Opt.Mode == SanitizeMode.None) return o;
            var h0 = Math.Max(1e-6, c.Num(o, "h0", 40));
            var qmax = Math.Max(1e-6, c.Num(o, "qmax", 100));
            var hs = Math.Max(0, c.Num(o, "hstat", 10)) / h0;
            var qo = Geo.Clamp(c.Num(o, "qop", 70) / qmax, 0.02, 0.98);
            if (c.Norm)
                return NoUnits(o.WithParam("h0", 1.0).WithParam("qmax", 1.0).WithParam("hstat", SanitizeText.Round(hs, 3)).WithParam("qop", SanitizeText.Round(qo, 3)), "xunit", "yunit");
            var h = SanitizeText.Round(h0 * c.Off(), 2);
            var q = SanitizeText.Round(qmax * c.Off(), 2);
            var hsNew = SanitizeText.Round(h * Geo.Clamp(hs * SanitizeText.Vary(c.Rng, c.Spread * 0.5), 0, 0.9), 2);
            var qoNew = SanitizeText.Round(q * Geo.Clamp(qo * SanitizeText.Vary(c.Rng, c.Spread * 0.4), 0.05, 0.95), 2);
            return o.WithParam("h0", h).WithParam("qmax", q).WithParam("hstat", hsNew).WithParam("qop", qoNew);
        }

        /// <summary>
        /// Bode plot: normalised to the corner frequency (f/fc, fc = 1, gain 0 dB, measured magnitudes relative to their
        /// maximum), the damping kept; randomised: corner frequency moved, damping varied on the same side of 1/√2 (a
        /// resonance peak stays), measured curves with smooth noise.
        /// </summary>
        private static SketchObject Bode(SketchObject o, Ctx c)
        {
            if (c.Opt.Mode == SanitizeMode.None) return o;
            var fc = Math.Max(1e-9, c.Num(o, "fc", 1000));
            var gain = c.Num(o, "gain", 0);
            var zeta = Geo.Clamp(c.Num(o, "zeta", 0.3), 0.01, 5);
            var blocks = Read(o.Str("data", ""));
            var hasData = blocks.Any(b => b.Rows.Any(r => r.Label is null && r.V.Length >= 2));
            Func<double, double> ff;
            if (c.Norm)
            {
                ff = v => v / fc;
                o = o.WithParam("fc", 1.0).WithParam("gain", 0.0);
                if (hasData)
                {
                    var top = blocks.SelectMany(b => b.Column(1)).DefaultIfEmpty(0).Max();
                    foreach (var b in blocks) { b.MapEach(0, ff); b.MapEach(1, v => v - top); }
                }
            }
            else
            {
                var k = c.Off(4);
                ff = v => v * k;
                var z = zeta * SanitizeText.Vary(c.Rng, c.Spread * 0.6);
                z = zeta < 0.7071 ? Math.Min(z, 0.69) : Math.Max(z, 0.72);
                var g = Math.Abs(gain) < 1e-9 ? 0 : Math.Round(gain * SanitizeText.Vary(c.Rng, c.Spread) + (gain > 0 ? 1 : -1) * SanitizeText.Uniform(c.Rng, 1, 4), 1);
                o = o.WithParam("fc", SanitizeText.Round(fc * k, 2)).WithParam("zeta", SanitizeText.Round(Geo.Clamp(z, 0.02, 4.5), 2)).WithParam("gain", g);
                if (hasData)
                {
                    var shift = SanitizeText.Uniform(c.Rng, -6, 6);
                    foreach (var b in blocks)
                    {
                        b.MapEach(0, ff);
                        b.MapColumn(1, l => Noisy(l.Select(v => v + shift).ToList(), c, 0.5));
                        b.MapColumn(2, l => Noisy(l, c, 0.3));
                    }
                }
            }
            if (hasData) o = o.WithParam("data", Write(blocks));
            return WithRange(o, "xrange", ff);
        }

        /// <summary>
        /// Tolerance band: the first block is rewritten as x;nominal;lower;upper (the band computed as drawn, so percent
        /// tolerances keep their shape), the measurement blocks like XY series.
        /// </summary>
        private static SketchObject Band(SketchObject o, Ctx c)
        {
            var text = o.Str("data", BandChart.DefaultData);
            var blocks = Read(text);
            if (blocks.Count == 0) return o;
            if (c.Opt.ReplaceTexts) RenameSeries(blocks.Skip(1).ToList(), c);
            if (c.Opt.Mode == SanitizeMode.None) return blocks.Count > 1 && c.Opt.ReplaceTexts ? o.WithParam("data", Write(blocks)) : o;
            var tol = c.Num(o, "tol", 10);
            var percent = o.Str("tolmode", "percent") == "percent";
            var first = ChartData.Parse(text)[0];
            var (nom, lo, hi) = BandChart.Curves(first, tol, percent);
            var extra = blocks.Skip(1).ToList();
            var xs = nom.Select(p => p.X).Concat(extra.SelectMany(b => b.XyColumns().X is int x ? b.Column(x) : Enumerable.Empty<double>())).ToList();
            var ys = nom.Concat(lo).Concat(hi).Select(p => p.Y).Concat(extra.SelectMany(b => b.XyColumns().Y.SelectMany(b.Column)))
                .Concat(ChartData.Numbers(o.Str("limits", ""))).ToList();
            Func<double, double> fx, fy;
            if (c.Norm) { fx = NormMap(xs, false); fy = NormMap(ys, false); }
            else { var kx = c.Off(); fx = v => v * kx; fy = RandMap(ys, false, c); }
            var band = new Block { Name = blocks[0].Name };
            var ny = nom.Select(p => fy(p.Y)).ToList();
            var noisy = c.Rand ? Noisy(ny, c) : ny;
            for (var i = 0; i < nom.Count; i++)
            {
                var d = noisy[i] - ny[i];
                band.Rows.Add(new Row { V = new[] { fx(nom[i].X), noisy[i], fy(lo[i].Y) + d, fy(hi[i].Y) + d } });
            }
            foreach (var b in extra)
            {
                var (x, yc) = b.XyColumns();
                if (x is int xi) b.MapEach(xi, fx);
                foreach (var k in yc) b.MapColumn(k, l => c.Rand ? Noisy(l.Select(fy).ToList(), c) : l.Select(fy).ToList());
            }
            o = o.WithParam("data", Write(new[] { band }.Concat(extra).ToList()));
            if (!percent) o = o.WithParam("tol", Num(Math.Abs(fy(tol) - fy(0))));
            o = WithList(o, "limits", fy);
            o = WithRange(o, "xrange", fx);
            o = WithRange(o, "yrange", fy);
            return c.Norm ? NoUnits(o, "xunit", "yunit") : o;
        }

        // ------------------------------------------------------------------ figure

        /// <summary>
        /// Figure: every x coordinate (curves, value lines, spans, notes, marks, numerals) through one x map, every y
        /// coordinate through the map of its panel (one map for all panels when a value line runs through all of them). No
        /// per-curve noise: notes, dimension arrows and numerals keep sitting on their curves. Texts: title, caption, panel
        /// titles, curve and line labels and notes become placeholders; axis labels become x/y; numerals and marks stay.
        /// </summary>
        private static SketchObject Figure(SketchObject o, Ctx c)
        {
            if (!FigureSpec.TryParse(FigureChart.SpecText(o), out var spec, out _) || spec is null) return o;
            var changed = false;
            if (c.Opt.ReplaceTexts) changed |= FigureTexts(spec, c);
            if (c.Opt.Mode != SanitizeMode.None) { FigureValues(spec, c); changed = true; }
            return changed ? o.WithParam("spec", spec.ToJson()) : o;
        }

        private static I18nText? Ph(I18nText? t, Ctx c) => t is null || string.IsNullOrWhiteSpace(t.En) ? t : new I18nText(SanitizeText.Placeholder(t.En, c.Rng));

        private static bool FigureTexts(FigureSpec s, Ctx c)
        {
            s.Title = Ph(s.Title, c);
            s.Caption = Ph(s.Caption, c);
            if (s.X.Label is not null) s.X.Label = new I18nText("x");
            for (var i = 0; i < s.Panels.Count; i++)
            {
                if (s.Panels[i].Label is not null) s.Panels[i].Label = new I18nText(s.Panels.Count > 1 ? "y" + Fmt.I(i + 1) : "y");
                s.Panels[i].Title = Ph(s.Panels[i].Title, c);
            }
            foreach (var se in s.Series) se.Label = Ph(se.Label, c);
            foreach (var r in s.Refs) r.Label = Ph(r.Label, c);
            foreach (var n in s.Notes) n.Text = Ph(n.Text, c);
            return true;
        }

        private static void FigureValues(FigureSpec s, Ctx c)
        {
            var n = Math.Max(1, s.Panels.Count);
            int P(int? p) => Math.Max(0, Math.Min(n - 1, p ?? 0));
            FigAxis AxisOf(int p) => p < s.Panels.Count ? s.Panels[p] : new FigAxis();
            bool Is(double? v) => v is { } d && Fmt.Finite(d);

            // x map from the data and a fixed range.
            var xs = s.Series.SelectMany(se => se.X)
                .Concat(s.Refs.Where(r => r.Kind is "v" or "point").Select(r => r.X))
                .Concat(s.Regions.Where(r => r.Kind == "span").SelectMany(r => new[] { r.From ?? double.NaN, r.To ?? double.NaN }))
                .Concat(new[] { s.X.Min ?? double.NaN, s.X.Max ?? double.NaN }).Where(Fmt.Finite).ToList();
            Func<double, double> fx;
            if (c.Norm) fx = NormMap(xs, s.X.Log);
            else { var k = c.Off(); fx = v => v * k; }

            // y maps per panel (joint when a value line runs through all panels).
            IEnumerable<double> PanelData(int i) => s.Series.Where(se => P(se.Panel) == i).SelectMany(se => se.Y)
                .Concat(s.Refs.Where(r => r.Kind == "h" && P(r.Panel) == i && !(r.Panel < 0)).Select(r => r.Y))
                .Concat(s.Regions.Where(r => r.Kind == "below" && r.Base is not null && r.A >= 0 && r.A < s.Series.Count && P(s.Series[r.A].Panel) == i).Select(r => r.Base!.Value))
                .Concat(new[] { AxisOf(i).Min ?? double.NaN, AxisOf(i).Max ?? double.NaN }).Where(Fmt.Finite);
            var joint = s.Refs.Any(r => r.Kind == "h" && r.Panel < 0);
            var fys = new Func<double, double>[n];
            if (joint)
            {
                var all = Enumerable.Range(0, n).SelectMany(PanelData).Concat(s.Refs.Where(r => r.Kind == "h").Select(r => r.Y)).ToList();
                var log = Enumerable.Range(0, n).All(i => AxisOf(i).Log);
                var f = c.Norm ? NormMap(all, log) : RandMap(all, log, c);
                for (var i = 0; i < n; i++) fys[i] = f;
            }
            else
                for (var i = 0; i < n; i++) fys[i] = c.Norm ? NormMap(PanelData(i), AxisOf(i).Log) : RandMap(PanelData(i), AxisOf(i).Log, c);
            Func<double, double> FY(int? p) => fys[P(p)];
            double MX(double v) => Fmt.Finite(v) ? fx(v) : v;
            double? MXn(double? v) => Is(v) ? fx(v!.Value) : v;
            double? MYn(double? v, int? p) => Is(v) ? FY(p)(v!.Value) : v;
            var xScale = xs.Count >= 2 && xs.Max() - xs.Min() > 1e-300 ? Math.Abs(fx(xs.Max()) - fx(xs.Min())) / (xs.Max() - xs.Min()) : 1;

            void Num(FigNumeral? num, int? panel, bool atIsY)
            {
                if (num is null) return;
                num.At = atIsY ? MYn(num.At, num.Panel ?? panel) : MXn(num.At);
                num.X = MXn(num.X);
                num.Y = MYn(num.Y, num.Panel ?? panel);
            }

            // Axes
            s.X.Min = MXn(s.X.Min); s.X.Max = MXn(s.X.Max);
            if (s.X.Min > s.X.Max) (s.X.Min, s.X.Max) = (s.X.Max, s.X.Min);
            s.X.Marks = s.X.Marks.Select(m => m with { At = MX(m.At) }).ToList();
            for (var i = 0; i < s.Panels.Count; i++)
            {
                var p = s.Panels[i];
                var fy = fys[P(i)];
                p.Min = Is(p.Min) ? fy(p.Min!.Value) : p.Min;
                p.Max = Is(p.Max) ? fy(p.Max!.Value) : p.Max;
                if (p.Min > p.Max) (p.Min, p.Max) = (p.Max, p.Min);
                p.Marks = p.Marks.Select(m => m with { At = Fmt.Finite(m.At) ? fy(m.At) : m.At }).ToList();
                if (c.Norm && !p.Principle) p.Unit = null;
            }
            if (c.Norm && !s.X.Principle) s.X.Unit = null;

            foreach (var se in s.Series)
            {
                var fy = FY(se.Panel);
                se.X = se.X.Select(MX).ToList();
                se.Y = se.Y.Select(v => Fmt.Finite(v) ? fy(v) : v).ToList();
                se.LabelAt = MXn(se.LabelAt);
                Num(se.Numeral, se.Panel, false);
                // The kept trail of a sketched curve carries the original values: dropped (the curve stays as drawn).
                se.Sketch = null;
            }
            foreach (var r in s.Refs)
            {
                var panel = r.Kind == "point" && r.Series >= 0 && r.Series < s.Series.Count ? s.Series[r.Series].Panel : r.Panel;
                r.X = MX(r.X);
                if (Fmt.Finite(r.Y)) r.Y = FY(panel)(r.Y);
                Num(r.Numeral, panel, r.Kind == "v");
            }
            foreach (var g in s.Regions)
            {
                var panel = g.A >= 0 && g.A < s.Series.Count ? s.Series[g.A].Panel : g.Panel;
                if (g.Kind == "hspan") panel = g.Panel;
                g.From = MXn(g.From); g.To = MXn(g.To); g.X2 = MXn(g.X2);
                g.Base = MYn(g.Base, panel); g.Y1 = MYn(g.Y1, panel); g.Y2 = MYn(g.Y2, panel);
                if (c.Opt.ReplaceTexts && g.Label is not null) g.Label = null;
                Num(g.Numeral, g.Panel ?? panel, false);
            }
            foreach (var note in s.Notes)
            {
                foreach (var p in new[] { note.P1, note.P2 })
                {
                    if (p is null) continue;
                    p.X = MX(p.X);
                    p.Y = MYn(p.Y, p.Panel);
                }
                if (Is(note.MaxWidth)) note.MaxWidth = note.MaxWidth!.Value * xScale;
            }
        }
    }
}
