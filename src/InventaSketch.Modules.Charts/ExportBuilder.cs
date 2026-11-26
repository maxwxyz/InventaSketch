using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Charts
{
    /// <summary>
    /// Builds the export model (<see cref="XFigure"/>) of a chart element: the data come from the chart model (data text,
    /// parameters, figure specification), axis ranges, ticks and titles from the composed frame (the same layout the
    /// renderer uses), and every item gets its part key so part looks apply (hidden parts are left out, part styles win).
    /// Positions that only exist in the layout (region notes, operating point labels) are taken from the composed chart and
    /// converted to data coordinates.
    /// </summary>
    internal sealed class ExportBuilder
    {
        /// <summary>Points per millimetre.</summary>
        public const double PT = 72 / 25.4;

        private readonly SketchObject _o;
        private readonly Lang _lang;
        private readonly Chart _c;
        private readonly Frame _f;
        private readonly XFigure _x;

        private ExportBuilder(SketchObject o, Lang lang, Chart c, string name, string type)
        {
            _o = o; _lang = lang; _c = c; _f = c.F;
            _x = new XFigure { Name = name, Id = o.Id, Type = type, Lang = lang };
        }

        /// <summary>The export model of a chart element; null for other elements.</summary>
        public static XFigure? Build(Registry registry, SketchDocument doc, SketchObject o, Lang lang)
        {
            if (registry.Type(o.Type) is not ChartType type) return null;
            var ctx = new RenderContext(doc, lang).For(registry);
            // A linked chart exports as the figure it would become when unlinked (its current curves as data).
            if (type is LinkedChart linked && linked.Frozen(o, ctx) is { } frozen && registry.Type(frozen.Type) is ChartType figure) { o = frozen; type = figure; }
            // Pie, polar and Sankey charts have no Cartesian frame: their own models.
            if (type is PieChart or PolarChart or SankeyChart) return CanvasExport.Build(o, (CanvasChart)type, ctx);
            var c = type.Compose(o, ctx);
            var b = new ExportBuilder(o, lang, c, type.Name.Get(lang), type.Id);
            b.Axes();
            switch (type)
            {
                case XyChart: b.Xy(); break;
                case BarChart: b.Bars(); break;
                case AxesChart: break;
                case TorqueChart: b.Torque(); break;
                case EfficiencyChart: b.Efficiency(); break;
                case PumpChart: b.Pump(); break;
                case BodeChart: b.Bode(); break;
                case BandChart: b.Band(); break;
                case FigureChart: b.Figure(); break;
                case MathAxesChart: break;
                case FunctionChart fc: b.Function(fc); break;
                case HistogramChart: b.Histogram(); break;
                case BoxChart: b.BoxPlot(); break;
                case GanttChart: b.Gantt(); break;
            }
            return b._x;
        }

        // ------------------------------------------------------------------ part looks

        private PartLook? Look(string key) => PartLooks.LookOf(_o.Parts, key);

        private bool Hidden(string key) => Look(key)?.Hidden == true;

        private static bool IsWhite(string c) => c.Equals("#ffffff", StringComparison.OrdinalIgnoreCase) || c.Equals("#fff", StringComparison.OrdinalIgnoreCase) || c == "white";

        /// <summary>Colour of a drawn item: black in monochrome output (white stays white).</summary>
        private string Col(string c) => _x.Mono && !IsWhite(c) && c != "none" ? Palette.Black : c;

        /// <summary>A stroke with the look of <paramref name="key"/> applied; null when the part is hidden or has no stroke.</summary>
        private XStroke? Stroke(string key, string color, double width, DashStyle dash, double? opacity = null)
        {
            var look = Look(key);
            if (look?.Hidden == true) return null;
            var s = look?.Style;
            var c = s?.Stroke ?? Col(color);
            if (c == "none") return null;
            return new XStroke(c, s?.StrokeWidth ?? width, s?.Dash ?? dash, s?.Opacity ?? opacity);
        }

        private XStroke? Stroke(string key, Style style) => Stroke(key, style.Stroke ?? Palette.Ink, style.StrokeWidth ?? 0.35, style.Dash ?? DashStyle.Solid, style.Opacity);

        private XMarker? Marker(string key, string kind, bool filled, string color, double size)
        {
            var look = Look(key);
            if (look?.Hidden == true) return null;
            return new XMarker(kind, filled, look?.Style?.Stroke ?? Col(color), size);
        }

        internal static string MarkerName(MarkerKind k) => k switch
        {
            MarkerKind.Square => "square",
            MarkerKind.Triangle => "triangle",
            MarkerKind.Diamond => "diamond",
            MarkerKind.Cross => "cross",
            MarkerKind.Plus => "plus",
            _ => "circle",
        };

        /// <summary>A text item with the text look of <paramref name="key"/> (colour, bold, italic, size); null when hidden.</summary>
        private XLabel? Label(string key, int panel, double x, double y, string text, string ha = "center", string va = "center", double dx = 0, double dy = 0,
            bool box = false, double size = 1, string color = Palette.Ink, bool italic = false, bool bold = false, bool xfrac = false, bool yfrac = false)
        {
            if (string.IsNullOrEmpty(text) || Hidden(key) || !Fmt.Finite(x) || !Fmt.Finite(y)) return null;
            var s = Look(key)?.Style;
            return new XLabel
            {
                Panel = panel, Key = key, X = x, Y = y, XFrac = xfrac, YFrac = yfrac, Dx = dx, Dy = dy, HAlign = ha, VAlign = va, Text = text, Box = box,
                Size = size * (s?.FontScale ?? 1), Color = s?.TextColor ?? color, Italic = s?.Italic ?? italic, Bold = s?.Bold ?? bold,
            };
        }

        private void Add(XItem? item)
        {
            if (item is not null) _x.Items.Add(item);
        }

        // ------------------------------------------------------------------ coordinates

        /// <summary>Data value at the normalised position (inverse of the axis mapping, axis breaks included).</summary>
        private static double FromT(Axis a, double t) => a.V(t);

        /// <summary>Panel whose plot area is nearest to the page y.</summary>
        private int PanelAt(double py)
        {
            var best = 0; var dist = double.MaxValue;
            for (var i = 0; i < _f.Panels.Count; i++)
            {
                var a = _f.Panels[i].Area;
                var d = py < a.Y ? a.Y - py : py > a.Bottom ? py - a.Bottom : 0;
                if (d < dist) { dist = d; best = i; }
            }
            return best;
        }

        private double DataX(double px)
        {
            var a = _f.Panels[0].Area;
            return FromT(_f.X, a.W > 0 ? (px - a.X) / a.W : 0);
        }

        private double DataY(int pi, double py, bool secondary = false)
        {
            var p = _f.Panels[pi];
            var axis = secondary && p.Y2 is not null ? p.Y2 : p.Y;
            return FromT(axis, p.Area.H > 0 ? (p.Area.Bottom - py) / p.Area.H : 0);
        }

        /// <summary>The texts of the composed chart with a tag of <paramref name="kind"/>, in data coordinates.</summary>
        private IEnumerable<XLabel> ComposedTexts(string kind, string? key = null)
        {
            var over = Prim.Flatten(_c.Over).ToList();
            foreach (var t in over.OfType<TextPrim>())
            {
                if (!ChartTags.Is(t.Tag, kind)) continue;
                var pi = PanelAt(t.Position.Y);
                var ha = t.TextStyle.Anchor switch { TextAnchor.Start => "left", TextAnchor.End => "right", _ => "center" };
                var box = over.OfType<RectPrim>().Any(r => r.Tag == t.Tag && r.Style.Fill?.Color == Palette.White
                    && t.Position.Y >= r.Y - 1e-6 && t.Position.Y <= r.Y + r.H + 1e-6);
                var l = Label(key ?? t.Tag!, pi, DataX(t.Position.X), DataY(pi, t.Position.Y), t.Text, ha, "center", box: box,
                    size: t.TextStyle.Size / _f.Fs, color: t.TextStyle.Color ?? Palette.Ink, italic: t.TextStyle.Italic, bold: t.TextStyle.Bold);
                if (l is not null) yield return l;
            }
        }

        // ------------------------------------------------------------------ frame

        private void Axes()
        {
            var f = _f;
            var n = f.Panels.Count;
            _x.WidthMm = f.W; _x.HeightMm = f.H; _x.FontMm = f.Fs; _x.Mono = f.Mono; _x.Box = f.Box;
            _x.Title = Hidden(ChartTags.Title) ? null : f.Title;
            _x.Caption = Hidden(ChartTags.Caption) ? null : f.Caption;
            var show = f.Legend.Count > 0 && (f.LegendMode is "inside" or "right" || f.LegendMode == "auto" && f.Legend.Count >= 2) && !Hidden(ChartTags.Legend);
            _x.Legend = !show ? "none" : f.LegendMode == "right" ? "right" : "inside";
            if (_x.Legend == "inside" && Prim.Flatten(_c.Render()).OfType<RectPrim>().FirstOrDefault(r => r.Tag == ChartTags.Legend) is { } box)
            {
                var a = f.Panels[0].Area;
                var cx = box.X + box.W / 2; var cy = box.Y + box.H / 2;
                _x.LegendCorner = (cy <= a.Y + a.H / 2 ? "upper " : "lower ") + (cx >= a.X + a.W / 2 ? "right" : "left");
            }

            var major = f.GridDash is { } gd && !f.Mono ? S.Grid(f.Mono) with { Dash = gd } : S.Grid(f.Mono);
            var gridOn = !Hidden(ChartTags.Grid);
            var minorOn = !Hidden(ChartTags.GridMinor);
            _x.GridStroke = Stroke(ChartTags.Grid, major) ?? new XStroke(Palette.Grid, 0.13, DashStyle.Solid);
            _x.MinorGridStroke = Stroke(ChartTags.GridMinor, S.GridMinor(f.Mono)) ?? new XStroke(Palette.GridMinor, 0.1, DashStyle.Dot);

            var lastPs = n > 1 ? "." + Fmt.I(n) : "";
            _x.X = Axis(f.X, f.XTitle, "x", lastPs, f.XTicks, f.Arrows || f.XArrow, (f.XGrid ?? f.Grid) && gridOn, minorOn);
            for (var i = 0; i < n; i++)
            {
                var p = f.Panels[i];
                var ps = n > 1 ? "." + Fmt.I(i + 1) : "";
                var xp = new XPanel
                {
                    Y = Axis(p.Y, p.YTitle, "y", ps, p.Ticks, f.Arrows || p.Arrow, (p.Grid ?? f.Grid) && gridOn, minorOn),
                    Share = p.Share,
                    WidthMm = p.Area.W,
                    HeightMm = p.Area.H,
                    GapMm = i + 1 < n ? f.Panels[i + 1].Area.Y - p.Area.Bottom : 0,
                };
                if (p.Y2 is not null) xp.Y2 = Axis(p.Y2, p.Y2Title, "y2", ps, null, false, false, false);
                if (f.Box && Hidden(ChartTags.Axis + ".box" + ps)) { xp.Y.LineHidden = true; _x.X.LineHidden = true; }
                _x.Panels.Add(xp);
            }
            // Axis breaks are not exported: the exported axes run continuously over the whole range.
            var broken = new List<string>();
            if (f.X.HasBreaks) broken.Add("x: " + ChartBreaks.Format(f.X.Breaks.Select(b => (b.Lo, b.Hi))));
            for (var i = 0; i < n; i++)
                if (f.Panels[i].Y.HasBreaks) broken.Add("y" + (n > 1 ? " " + Fmt.I(i + 1) : "") + ": " + ChartBreaks.Format(f.Panels[i].Y.Breaks.Select(b => (b.Lo, b.Hi))));
            if (broken.Count > 0)
                _x.Notes.Add((_lang == Lang.De ? "Achsenbrüche werden nicht exportiert, die Achsen laufen durch (" : "Axis breaks are not exported; the axes run continuously (")
                    + string.Join(", ", broken) + ")");
            if (f.MathAxes)
                _x.Notes.Add(_lang == Lang.De ? "Achsen im Mathematikstil (durch den Ursprung) werden als Rahmenachsen exportiert" : "Math-style axes (through the origin) are exported as frame axes");
        }

        private XAxis Axis(Axis a, string? title, string dir, string ps, IReadOnlyList<Tick>? custom, bool arrow, bool grid, bool minor)
        {
            var x = new XAxis
            {
                Kind = a.Kind, Min = a.Min, Max = a.Max, Step = a.Step, Arrow = arrow,
                Title = Hidden(ChartTags.AxisTitle + "." + dir + ps) ? null : title,
                GridMajor = grid && a.Kind != AxisKind.Category,
                GridMinor = grid && minor && a.Kind == AxisKind.Log && custom is null,
                LineHidden = Hidden(ChartTags.Axis + "." + dir + ps),
                TicksHidden = Hidden(ChartTags.Tick + "." + (dir == "y2" ? "y" : dir)),
                TickLabelsHidden = Hidden(ChartTags.TickLabel + "." + dir + (dir == "x" ? "" : ps)),
            };
            if (a.Kind == AxisKind.Category)
            {
                x.Categories = a.Categories.ToList();
                x.Min = -0.5; x.Max = a.Categories.Count - 0.5;
                x.Reversed = a.Reversed;
            }
            if (custom is not null)
                x.Marks = custom.Where(t => a.Contains(t.Value)).Select(t => new XMark(t.Value, t.Label ?? "")).ToList();
            return x;
        }

        // ------------------------------------------------------------------ series helpers

        /// <summary>A smooth curve through the points (the renderer's spline, in log space on log axes), sampled.</summary>
        internal static List<Vec2> Smooth(IReadOnlyList<Vec2> pts, bool xlog, bool ylog)
        {
            if (pts.Count < 3) return pts.ToList();
            Vec2 To(Vec2 p) => new(xlog ? Math.Log10(p.X) : p.X, ylog ? Math.Log10(p.Y) : p.Y);
            Vec2 From(Vec2 p) => new(xlog ? Math.Pow(10, p.X) : p.X, ylog ? Math.Pow(10, p.Y) : p.Y);
            var t = pts.Select(To).ToList();
            if (t.Any(p => !Frame.Ok(p))) return pts.ToList();
            return Draw.Sample(Draw.Spline(t), 8).Select(From).ToList();
        }

        /// <summary>
        /// Adds a curve: the line (sampled when smooth) and its markers at the data points; markers of a smooth curve are a
        /// separate item. Points may contain NaN gaps. Returns the line points as drawn (for labels and regions).
        /// </summary>
        private (List<double> X, List<double> Y) AddSeries(int panel, string key, string? markerKey, IReadOnlyList<Vec2> pts, XStroke? line, XMarker? marker,
            bool step = false, bool smooth = false, string? legend = null, bool y2 = false)
        {
            var xlog = _x.X.IsLog;
            var ylog = panel < _x.Panels.Count && (y2 ? _x.Panels[panel].Y2?.IsLog == true : _x.Panels[panel].Y.IsLog);
            var drawn = new List<Vec2>();
            var pieces = new List<List<Vec2>>();
            List<Vec2>? cur = null;
            foreach (var p in pts)
            {
                if (!Frame.Ok(p)) { cur = null; continue; }
                if (cur is null) { cur = new List<Vec2>(); pieces.Add(cur); }
                cur.Add(p);
            }
            for (var i = 0; i < pieces.Count; i++)
            {
                if (i > 0) drawn.Add(new Vec2(double.NaN, double.NaN));
                drawn.AddRange(smooth && !step ? Smooth(pieces[i], xlog, ylog) : pieces[i]);
            }
            var sampled = smooth && !step && pieces.Any(p => p.Count >= 3);
            if (line is not null || (marker is not null && !sampled))
            {
                Add(new XSeries
                {
                    Panel = panel, Key = line is not null ? key : markerKey ?? key,
                    X = (line is null ? pts : drawn).Select(p => p.X).ToList(), Y = (line is null ? pts : drawn).Select(p => p.Y).ToList(),
                    Line = line, Marker = sampled ? null : marker, Step = step && line is not null, Legend = legend, Y2 = y2,
                });
                if (line is null) legend = null;
            }
            if (marker is not null && sampled)
                Add(new XSeries { Panel = panel, Key = markerKey ?? key, X = pts.Select(p => p.X).ToList(), Y = pts.Select(p => p.Y).ToList(), Marker = marker, Y2 = y2, Legend = line is null ? legend : null });
            if (!step) return (drawn.Select(p => p.X).ToList(), drawn.Select(p => p.Y).ToList());
            // Steps: the outline as drawn (each value held until the next x).
            var outline = new List<Vec2>();
            foreach (var piece in pieces)
            {
                if (outline.Count > 0) outline.Add(new Vec2(double.NaN, double.NaN));
                outline.AddRange(XyChart.Steps(piece));
            }
            return (outline.Select(p => p.X).ToList(), outline.Select(p => p.Y).ToList());
        }

        /// <summary>y of a polyline at x (first segment covering x; NaN in gaps and outside).</summary>
        internal static double Interp(IReadOnlyList<double> xs, IReadOnlyList<double> ys, double x)
        {
            for (var i = 0; i + 1 < xs.Count; i++)
            {
                double x0 = xs[i], x1 = xs[i + 1], y0 = ys[i], y1 = ys[i + 1];
                if (!Fmt.Finite(x0) || !Fmt.Finite(x1) || !Fmt.Finite(y0) || !Fmt.Finite(y1)) continue;
                if (x < Math.Min(x0, x1) - 1e-12 || x > Math.Max(x0, x1) + 1e-12) continue;
                return Math.Abs(x1 - x0) < 1e-15 ? y0 : y0 + (y1 - y0) * (x - x0) / (x1 - x0);
            }
            return double.NaN;
        }

        private void HLine(int panel, double y, XStroke? stroke, string key)
        {
            if (stroke is null || !Fmt.Finite(y)) return;
            Add(new XHLine { Panel = panel, Key = key, Y = y, Stroke = stroke });
        }

        // ------------------------------------------------------------------ chart types

        private void Limits()
        {
            var stroke = Stroke(ChartTags.Limit, Palette.Helper, 0.35, DashStyle.DashDot);
            foreach (var v in ChartData.Numbers(_o.Str("limits", "")))
                for (var i = 0; i < _f.Panels.Count; i++)
                    if (_f.Panels[i].Y.Contains(v)) HLine(i, v, stroke, ChartTags.Limit);
        }

        private void Xy()
        {
            var text = _o.Str("data", XyChart.DefaultData);
            var series = ChartData.Xy(ChartData.Parse(text));
            var style = _o.Str("style", "line");
            var smooth = _o.Flag("smooth", false) && style != "step";
            var splines = ChartSketchInput.XySplines(text);
            var lines = style != "points";
            var markers = style is "points" or "linepoints";
            Limits();
            var drawn = new (List<double> X, List<double> Y)?[series.Count];
            for (var k = 0; k < series.Count; k++)
            {
                var look = SeriesLook.Of(k);
                var key = ChartTags.Nth(ChartTags.Series, k);
                var mkey = ChartTags.Nth(ChartTags.Marker, k);
                var line = lines ? Stroke(key, look.Color, 0.5, look.Dash) : null;
                var marker = markers ? Marker(mkey, MarkerName(look.Marker), look.FilledMarker, look.Color, _f.Fs * 0.3) : null;
                var legend = Common.SeriesName(series[k].Name, k, _lang);
                drawn[k] = AddSeries(0, key, mkey, series[k].Points, line, marker, style == "step", smooth || style != "step" && k < splines.Length && splines[k], legend);
            }
            // Areas below everything else (as drawn).
            var areas = XyEditor.Areas(_o);
            var areaSeries = XyEditor.Instance.Series(_o, _lang);
            var at = 0;
            for (var k = 0; k < areas.Count; k++)
            {
                var key = ChartTags.Nth(ChartAreas.Tag, k);
                if (Hidden(key) || Area(areas[k], key, areaSeries, drawn) is not { } fill) continue;
                _x.Items.Insert(at++, fill);
            }
        }

        /// <summary>Function graph: the sampled curve (gaps between its pieces) and the formula.</summary>
        private void Function(FunctionChart type)
        {
            var pts = new List<Vec2>();
            foreach (var run in FunctionChart.DataRuns(_o, type, _lang))
            {
                if (pts.Count > 0) pts.Add(new Vec2(double.NaN, double.NaN));
                pts.AddRange(run);
            }
            var key = ChartTags.Nth(ChartTags.Series, 0);
            AddSeries(0, key, null, pts, Stroke(key, "#1565c0", 0.5, DashStyle.Solid), null);
            foreach (var t in ComposedTexts(ChartTags.Formula)) Add(t);
        }

        internal static XFillStyle FillOf(Fill? fill, string stroke)
        {
            if (fill is null || fill.Kind == FillKind.None) return new XFillStyle("none", stroke);
            return fill.Kind switch
            {
                FillKind.Solid => new XFillStyle("solid", fill.Color ?? stroke),
                FillKind.Dots => new XFillStyle("dots", stroke, 45, fill.DotSpacing ?? 1.0),
                _ => new XFillStyle(fill.Hatch?.Cross == true ? "cross" : "hatch", fill.Hatch?.Color ?? stroke, fill.Hatch?.Angle ?? 45, fill.Hatch?.Spacing ?? 1.2),
            };
        }

        private void Bars()
        {
            var set = ChartData.Categories(ChartData.Parse(_o.Str("data", BarChart.DefaultData)), _lang);
            var horizontal = _o.Str("orient", "column") == "bar";
            var stacked = _o.Str("mode", "grouped") == "stacked" && set.Series.Count > 1;
            int n = set.Categories.Count, m = set.Series.Count;
            var mono = _f.Mono || _o.Flag("patterns", false);
            var group = stacked || m == 1 ? 0.62 : Math.Min(0.8, 0.5 + 0.08 * m);
            var valueAxis = horizontal ? _f.X : _f.Panels[0].Y;
            var dec = Math.Max(0, Math.Min(3, valueAxis.Decimals + 1));
            var bars = new XBars { Categories = set.Categories, Stacked = stacked, Horizontal = horizontal, Width = stacked ? group : group / Math.Max(1, m) };
            var pos = new double[n]; var neg = new double[n];
            for (var k = 0; k < m; k++)
            {
                var key = ChartTags.Nth(ChartTags.Bar, k);
                var values = set.Series[k].Values;
                var bottoms = new List<double>();
                for (var i = 0; i < n; i++)
                {
                    var v = values[i];
                    if (!stacked || !Fmt.Finite(v)) { bottoms.Add(0); continue; }
                    if (v >= 0) { bottoms.Add(pos[i]); pos[i] += v; }
                    else { bottoms.Add(neg[i]); neg[i] += v; }
                }
                if (Hidden(key)) continue;
                var area = SeriesLook.Of(k).Area(mono, 0.25);
                var look = Look(key)?.Style;
                var stroke = look?.Stroke ?? area.Stroke ?? Palette.Black;
                var fill = FillOf(look?.Fill ?? area.Fill, stroke);
                var vkey = ChartTags.Nth(ChartTags.ValueLabel, k);
                bars.Series.Add(new XBarSeries
                {
                    Key = key, Name = Common.SeriesName(set.Series[k].Name, k, _lang), Values = values.ToList(), Bottoms = bottoms,
                    Offset = stacked ? 0 : -group / 2 + (k + 0.5) * group / Math.Max(1, m),
                    Fill = fill, Edge = stroke, EdgeWidth = look?.StrokeWidth ?? 0.25, InLegend = _x.Legend != "none",
                    Labels = _o.Flag("values", false) && !Hidden(vkey) ? values.Select(v => Fmt.Finite(v) ? Fmt.Num(v, _lang, dec) : "").ToList() : null,
                });
            }
            _x.Bars = bars;
        }

        /// <summary>The fill and edge of an area part: the part style wins over the drawn style.</summary>
        private (XFillStyle Fill, string Edge, double EdgeWidth) AreaLook(string key, Style drawn)
        {
            var look = Look(key)?.Style;
            var edge = look?.Stroke ?? drawn.Stroke ?? Palette.Black;
            return (FillOf(look?.Fill ?? drawn.Fill, edge), edge, look?.StrokeWidth ?? drawn.StrokeWidth ?? 0.25);
        }

        /// <summary>Histogram: the raw samples with the bin edges as drawn (several series side by side).</summary>
        private void Histogram()
        {
            var samples = HistogramChart.Samples(_o.Str("data", HistogramChart.DefaultData));
            var bins = HistogramChart.BinCount(_o, samples);
            var (edges, _) = Stats.Histogram(samples.Select(s => (IReadOnlyList<double>)s.Values).ToList(), bins, ChartData.Range(_o.Str("xrange", "")));
            var mono = _f.Mono || _o.Flag("patterns", false);
            var h = new XHist { Edges = edges.ToList() };
            for (var k = 0; k < samples.Count; k++)
            {
                var key = ChartTags.Nth(ChartTags.Bar, k);
                if (Hidden(key)) continue;
                var (fill, edge, width) = AreaLook(key, HistogramChart.BarStyle(k, mono));
                h.Series.Add(new XHistSeries
                {
                    Key = key, Name = Common.SeriesName(samples[k].Name, k, _lang), Samples = samples[k].Values.ToList(),
                    Fill = fill, Edge = edge, EdgeWidth = width, InLegend = _x.Legend != "none",
                });
            }
            _x.Hist = h;
        }

        /// <summary>Box plot: per series the raw samples of every category at the box positions as drawn.</summary>
        private void BoxPlot()
        {
            var (cats, series) = BoxChart.Groups(_o.Str("data", BoxChart.DefaultData));
            var whis = _o.Num("whis", 1.5);
            var outliers = _o.Flag("outliers", true);
            var mono = _f.Mono || _o.Flag("patterns", false);
            var m = series.Count;
            var group = m == 1 ? 0.55 : Math.Min(0.84, 0.5 + 0.1 * m);
            var w = group / Math.Max(1, m);
            var boxes = new XBoxes { Whis = whis, Outliers = outliers };
            for (var k = 0; k < m; k++)
            {
                var key = ChartTags.Nth(ChartTags.Box, k);
                if (Hidden(key)) continue;
                var drawn = BoxChart.BoxStyle(k, mono);
                var (fill, edge, width) = AreaLook(key, drawn);
                var okey = ChartTags.Nth(ChartTags.Outlier, k);
                var s = new XBoxSeries
                {
                    Key = key, OutlierKey = Hidden(okey) ? null : okey, Name = Common.SeriesName(series[k].Name, k, _lang), Width = Math.Round(w * 0.84, 6),
                    Fill = fill, Line = new XStroke(edge, width, Look(key)?.Style?.Dash ?? DashStyle.Solid),
                    OutlierColor = Look(okey)?.Style?.Stroke ?? (mono ? Palette.Black : SeriesLook.Of(k).Color), InLegend = _x.Legend != "none",
                };
                for (var i = 0; i < cats.Count; i++)
                {
                    if (!series[k].Samples.TryGetValue(i, out var values) || Stats.Box(values, whis, outliers) is not { } st) continue;
                    s.Positions.Add(Math.Round(i + (1 - group) / 2 + k * w + w * 0.5 - 0.5, 6));
                    s.Samples.Add(values.Where(Fmt.Finite).ToList());
                    s.Stats.Add(st);
                }
                if (s.Positions.Count > 0) boxes.Series.Add(s);
            }
            _x.Boxes = boxes;
        }

        /// <summary>Gantt chart: task bars from their start over their duration, milestones and the today line with its label.</summary>
        private void Gantt()
        {
            var (tasks, groups) = GanttChart.Tasks(_o.Str("data", GanttChart.DefaultData), _lang);
            var mono = _f.Mono || _o.Flag("patterns", false);
            var panel = _f.Panels[0];
            var size = GanttChart.MilestoneSize(_f, panel, tasks.Count);
            var g = new XGantt { Height = 0.6 };
            for (var k = 0; k < groups.Count; k++)
            {
                var key = ChartTags.Nth(ChartTags.Bar, k);
                var mkey = ChartTags.Nth(ChartTags.Milestone, k);
                var (fill, edge, width) = AreaLook(key, SeriesLook.Of(k).Area(mono, 0.25));
                var drawnColor = mono ? Palette.Black : SeriesLook.Of(k).Color;
                var group = new XGanttGroup
                {
                    Key = Hidden(key) ? "" : key, MilestoneKey = Hidden(mkey) ? "" : mkey, Name = Common.SeriesName(groups[k], k, _lang),
                    Fill = fill, Edge = edge, EdgeWidth = width, MilestoneColor = Look(mkey)?.Style?.Fill?.Color ?? Look(mkey)?.Style?.Stroke ?? drawnColor,
                    MilestoneSize = size, InLegend = _x.Legend != "none",
                };
                for (var i = 0; i < tasks.Count; i++)
                {
                    var t = tasks[i];
                    if (t.Group != k) continue;
                    if (t.Milestone) { if (group.MilestoneKey.Length > 0) group.Milestones.Add((i, t.Start, t.Name)); }
                    else if (group.Key.Length > 0) group.Bars.Add((i, t.Start, t.Duration, t.Name));
                }
                if (group.Bars.Count > 0 || group.Milestones.Count > 0) g.Groups.Add(group);
            }
            _x.Gantt = g;
            if (GanttChart.Today(_o) is { } now && _f.X.Contains(now) && !Hidden(ChartTags.Today))
            {
                var stroke = Stroke(ChartTags.Today, mono ? Palette.Black : "#b03a2e", 0.4, DashStyle.Dash);
                if (stroke is not null) Add(new XVLine { Panel = 0, LastPanel = 0, Key = ChartTags.Today, X = now, Stroke = stroke });
                // The label at the top of the plot; the task axis runs top-down with task i centred at i.
                foreach (var t in Prim.Flatten(_c.Over).OfType<TextPrim>().Where(t => t.Tag == ChartTags.Today))
                {
                    var row = -0.5 + (panel.Area.H > 0 ? (t.Position.Y - panel.Area.Y) / panel.Area.H : 0) * Math.Max(1, tasks.Count);
                    Add(Label(ChartTags.Today, 0, DataX(t.Position.X), Math.Round(row, 4), t.Text, t.TextStyle.Anchor == TextAnchor.End ? "right" : "left", "center",
                        box: true, size: t.TextStyle.Size / _f.Fs));
                }
            }
        }

        private void Torque()
        {
            var (tmax, nb, nmax) = Drive.Read(_o);
            var power = _o.Flag("power", true);
            var pmax = Drive.PowerKw(tmax, nb);
            var tLook = SeriesLook.Of(0); var pLook = SeriesLook.Of(1);
            var y = _f.Panels[0].Y;
            if (_o.Flag("regions", true) && nb < nmax * 0.98)
            {
                if (Stroke(ChartTags.Helper, S.Helper) is { } h)
                    Add(new XSegment { Key = ChartTags.Helper, A = new Vec2(nb, 0), B = new Vec2(nb, y.Max), Stroke = h });
                foreach (var t in ComposedTexts(ChartTags.Note)) Add(t);
            }
            AddSeries(0, ChartTags.Nth(ChartTags.Series, 0), null, Drive.Envelope(tmax, nb, nmax), Stroke(ChartTags.Nth(ChartTags.Series, 0), tLook.Color, 0.6, tLook.Dash), null,
                legend: Drive.TorqueLabel.Get(_lang));
            if (power)
                AddSeries(0, ChartTags.Nth(ChartTags.Series, 1), null, new[] { new Vec2(0, 0), new Vec2(nb, pmax), new Vec2(nmax, pmax) },
                    Stroke(ChartTags.Nth(ChartTags.Series, 1), pLook.Color, 0.5, pLook.Dash), null, legend: new I18nText("Power P", "Leistung P").Get(_lang), y2: true);
        }

        private void Efficiency()
        {
            var (tmax, nb, nmax) = Drive.Read(_o);
            var peak = Geo.Clamp(_o.Num("etamax", 96), 1, 99.9);
            var (ns, ts, eta, at) = EfficiencyChart.Field(tmax, nb, nmax, peak);
            bool Inside(Vec2 d) => d.Y <= Drive.Torque(d.X, tmax, nb) * (1 + 1e-6) && d.X <= nmax * (1 + 1e-9);
            var levels = ChartData.Numbers(_o.Str("levels", EfficiencyChart.DefaultLevels)).Where(l => l > 0 && l < peak).Distinct().OrderBy(l => l).Take(20).ToList();
            if (Stroke(ChartTags.Contour, Palette.Ink, 0.3, DashStyle.Solid) is { } cs)
            {
                var z = new double[ns.Length, ts.Length];
                for (var i = 0; i < ns.Length; i++)
                    for (var j = 0; j < ts.Length; j++)
                        z[i, j] = Inside(new Vec2(ns[i], ts[j])) ? Math.Round(eta[i, j], 4) : double.NaN;
                var contours = new XContours
                {
                    Key = ChartTags.Contour, X = ns, Y = ts, Z = z, Levels = levels, Stroke = cs,
                    Labels = _o.Flag("labels", true) && !Hidden(ChartTags.ContourLabel), LabelSuffix = " %",
                };
                foreach (var level in levels)
                    foreach (var piece in Contours.Trace(eta, ns, ts, level).SelectMany(l => Contours.Keep(l, Inside)))
                        if (piece.Count >= 2) contours.Pieces.Add((level, piece));
                if (contours.Labels) contours.LabelTexts.AddRange(ComposedTexts(ChartTags.ContourLabel));
                Add(contours);
            }
            AddSeries(0, ChartTags.Envelope, null, Drive.Envelope(tmax, nb, nmax), Stroke(ChartTags.Envelope, Palette.Ink, 0.6, DashStyle.Solid), null);
            AddSeries(0, ChartTags.Marker, ChartTags.Marker, new[] { at }, null, Marker(ChartTags.Marker, "plus", false, Palette.Ink, _f.Fs * 0.35));
            foreach (var t in ComposedTexts(ChartTags.Note)) Add(t);
        }

        private void Pump()
        {
            var fan = _o.Str("kind", "pump") == "fan";
            var h0 = Math.Max(1e-6, _o.Num("h0", 40));
            var qmax = Math.Max(1e-6, _o.Num("qmax", 100));
            var (q, h, hs, k) = PumpChart.OperatingPoint(h0, qmax, _o.Num("hstat", 10), _o.Num("qop", 70));
            var pLook = SeriesLook.Of(0); var sLook = SeriesLook.Of(1);
            var pump = Enumerable.Range(0, 81).Select(i => qmax * i / 80).Select(v => new Vec2(v, h0 * (1 - v * v / (qmax * qmax)))).ToList();
            AddSeries(0, ChartTags.Nth(ChartTags.Series, 0), null, pump, Stroke(ChartTags.Nth(ChartTags.Series, 0), pLook.Color, 0.6, pLook.Dash), null,
                legend: (fan ? new I18nText("Fan curve", "Ventilatorkennlinie") : new I18nText("Pump curve", "Pumpenkennlinie")).Get(_lang));
            if (_o.Flag("system", true))
            {
                var sys = Enumerable.Range(0, 81).Select(i => qmax * i / 80).Select(v => new Vec2(v, hs + k * v * v)).ToList();
                AddSeries(0, ChartTags.Nth(ChartTags.Series, 1), null, sys, Stroke(ChartTags.Nth(ChartTags.Series, 1), sLook.Color, 0.5, sLook.Dash), null,
                    legend: new I18nText("System curve", "Anlagenkennlinie").Get(_lang));
            }
            if (Stroke(ChartTags.Helper, S.Helper) is { } helper)
            {
                Add(new XSegment { Key = ChartTags.Helper, A = new Vec2(q, _f.Panels[0].Y.Min), B = new Vec2(q, h), Stroke = helper });
                Add(new XSegment { Key = ChartTags.Helper, A = new Vec2(_f.X.Min, h), B = new Vec2(q, h), Stroke = helper });
            }
            AddSeries(0, ChartTags.Point, ChartTags.Point, new[] { new Vec2(q, h) }, null, Marker(ChartTags.Point, "circle", true, Palette.Ink, _f.Fs * 0.38));
            foreach (var t in ComposedTexts(ChartTags.Note)) Add(t);
        }

        private void Bode()
        {
            var system = _o.Str("system", "lowpass2");
            var fc = Math.Max(1e-9, _o.Num("fc", 1000));
            var zeta = Geo.Clamp(_o.Num("zeta", 0.3), 0.01, 5);
            var gain = _o.Num("gain", 0);
            var range = ChartData.Range(_o.Str("xrange", ""));
            var measured = ChartData.Parse(_o.Str("data", "")).SelectMany(b => b.Rows)
                .Where(r => r.Label is null && r.Values.Count >= 2 && r.Values[0] > 0 && Fmt.Finite(r.Values[0]) && Fmt.Finite(r.Values[1]))
                .Select(r => (F: r.Values[0], Db: r.Values[1], Deg: r.Values.Count > 2 ? r.Values[2] : double.NaN)).OrderBy(r => r.F).ToList();
            var useData = measured.Count >= 2;
            List<(double F, double Db, double Deg)> pts;
            if (useData) pts = measured;
            else
            {
                double f0 = range is { } rr && rr.Min > 0 ? rr.Min : fc / 100, f1 = range is { } rr2 && rr2.Min > 0 ? rr2.Max : fc * 100;
                pts = Enumerable.Range(0, 241).Select(i => f0 * Math.Pow(f1 / f0, i / 240.0)).Select(fq => { var (db, deg) = BodeChart.Response(system, fq, fc, zeta, gain); return (fq, db, deg); }).ToList();
            }
            var show = _o.Str("show", "both");
            var hasPhase = pts.Any(p => Fmt.Finite(p.Deg));
            var showMag = show != "phase" || !hasPhase;
            var showPhase = show != "magnitude" && hasPhase;
            var look = SeriesLook.Of(0);
            var panel = 0;
            if (showMag)
            {
                var mag = pts.Select(p => new Vec2(p.F, p.Db)).ToList();
                AddSeries(panel, ChartTags.Nth(ChartTags.Series, 0), ChartTags.Nth(ChartTags.Marker, 0), mag, Stroke(ChartTags.Nth(ChartTags.Series, 0), look.Color, 0.5, look.Dash),
                    useData ? Marker(ChartTags.Nth(ChartTags.Marker, 0), MarkerName(look.Marker), true, look.Color, _f.Fs * 0.25) : null);
                panel++;
            }
            if (showPhase)
            {
                var ph = pts.Where(p => Fmt.Finite(p.Deg)).Select(p => new Vec2(p.F, p.Deg)).ToList();
                AddSeries(Math.Min(panel, _f.Panels.Count - 1), ChartTags.Nth(ChartTags.Series, 1), ChartTags.Nth(ChartTags.Marker, 1), ph, Stroke(ChartTags.Nth(ChartTags.Series, 1), look.Color, 0.5, look.Dash),
                    useData ? Marker(ChartTags.Nth(ChartTags.Marker, 1), MarkerName(look.Marker), true, look.Color, _f.Fs * 0.25) : null);
            }
            if (!useData && _f.X.Contains(fc) && _o.Flag("marker", true))
            {
                if (Stroke(ChartTags.Helper, S.Helper) is { } h) Add(new XVLine { Panel = 0, LastPanel = _f.Panels.Count - 1, Key = ChartTags.Helper, X = fc, Stroke = h });
                foreach (var t in ComposedTexts(ChartTags.Note)) Add(t);
            }
        }

        private void Band()
        {
            var blocks = ChartData.Parse(_o.Str("data", BandChart.DefaultData));
            var (nom, lo, hi) = blocks.Count > 0 ? BandChart.Curves(blocks[0], _o.Num("tol", 10), _o.Str("tolmode", "percent") == "percent") : (new List<Vec2>(), new List<Vec2>(), new List<Vec2>());
            var extra = ChartData.Xy(blocks.Skip(1).ToList());
            var mono = _f.Mono || _o.Flag("patterns", false);
            var smooth = _o.Flag("smooth", true);
            var look = SeriesLook.Of(0);
            var lang = _lang;
            if (nom.Count >= 2)
            {
                List<Vec2> Curve(List<Vec2> d) => smooth && d.Count >= 3 ? Smooth(d, false, false) : d;
                var up = Curve(hi); var dn = Curve(lo);
                if (!Hidden(ChartTags.Band))
                {
                    var bandLook = Look(ChartTags.Band)?.Style;
                    var fill = bandLook?.Fill is { } bf ? FillOf(bf, bandLook.Stroke ?? look.Color)
                        : mono ? new XFillStyle("hatch", Palette.Black, 45, 1.5) : new XFillStyle("solid", Palette.Band);
                    var band = new XFill
                    {
                        Key = ChartTags.Band, Kind = "between", X = up.Select(p => p.X).ToList(), Lo = dn.Select(p => p.Y).ToList(), Hi = up.Select(p => p.Y).ToList(),
                        Fill = fill, Legend = new I18nText("Tolerance band", "Toleranzband").Get(lang),
                    };
                    band.Polygons.Add(up.Concat(Enumerable.Reverse(dn)).ToList());
                    Add(band);
                }
                var edge = Stroke(ChartTags.Limit, look.Color, 0.3, DashStyle.Dash);
                if (edge is not null)
                {
                    AddSeries(0, ChartTags.Limit, null, up, edge, null);
                    AddSeries(0, ChartTags.Limit, null, dn, edge, null);
                }
                AddSeries(0, ChartTags.Nth(ChartTags.Series, 0), null, nom, Stroke(ChartTags.Nth(ChartTags.Series, 0), look.Color, 0.5, look.Dash), null,
                    smooth: smooth, legend: new I18nText("Nominal", "Sollwert").Get(lang));
            }
            Limits();
            for (var k = 0; k < extra.Count; k++)
            {
                var l = SeriesLook.Of(k + 1);
                var key = ChartTags.Nth(ChartTags.Series, k + 1);
                var mkey = ChartTags.Nth(ChartTags.Marker, k + 1);
                AddSeries(0, key, mkey, extra[k].Points, Stroke(key, l.Color, 0.35, l.Dash), Marker(mkey, MarkerName(l.Marker), l.FilledMarker, l.Color, _f.Fs * 0.3),
                    legend: Common.SeriesName(extra[k].Name, k + 1, lang));
            }
        }

        // ------------------------------------------------------------------ figure

        private static int Clamp(int i, int n) => Math.Max(0, Math.Min(n - 1, i));

        private void Figure()
        {
            if (!FigureSpec.TryParse(FigureChart.SpecText(_o), out var spec, out _) || spec is null)
            {
                foreach (var t in ComposedTexts(ChartTags.Error)) Add(t);
                return;
            }
            var n = _f.Panels.Count;
            var (sNum, rNum, gNum, nNum, _) = FigureComposer.NumeralIndex(spec);
            var nfs = 0.85;
            var fsPt = _f.Fs * PT;
            var nfsPt = fsPt * nfs;
            string NumKey(int j) => ChartTags.Nth(ChartTags.Numeral, Math.Max(0, j));
            string SeriesColor(int k) => spec.Series[k].Color ?? SeriesLook.Of(k).Color;
            var showLegend = _x.Legend != "none";
            var lines = new (List<double> X, List<double> Y)?[spec.Series.Count];
            var panelOf = spec.Series.Select(s => Clamp(s.Panel, n)).ToArray();

            void Leader(FigNumeral num, int j, int panel, double x, double y, bool yfrac = false)
            {
                var key = NumKey(j);
                if (j < 0 || Hidden(key) || !Fmt.Finite(x) || !Fmt.Finite(y)) return;
                Add(new XLeader
                {
                    Panel = panel, Key = key, X = x, Y = y, YFrac = yfrac, Dx = num.Dx, Dy = -num.Dy, Text = num.Text,
                    Color = Look(key)?.Style?.TextColor ?? Look(key)?.Style?.Stroke ?? Palette.Ink,
                });
            }

            // Point on a curve at x: on the drawn curve, else interpolated across a gap (as the composer does).
            double OnSeries(int k, double x)
            {
                if (k < 0 || k >= lines.Length || lines[k] is not { } l) return double.NaN;
                var y = Interp(l.X, l.Y, x);
                if (Fmt.Finite(y)) return y;
                var se = spec.Series[k];
                var pts = Enumerable.Range(0, Math.Min(se.X.Count, se.Y.Count)).Where(i => Fmt.Finite(se.X[i]) && Fmt.Finite(se.Y[i])).ToList();
                return Interp(pts.Select(i => se.X[i]).ToList(), pts.Select(i => se.Y[i]).ToList(), x);
            }

            // Series.
            for (var k = 0; k < spec.Series.Count; k++)
            {
                var se = spec.Series[k];
                var key = ChartTags.Nth(ChartTags.Series, k);
                var color = SeriesColor(k);
                var dash = FigureComposer.DashOf(se.Line, _x.Mono ? SeriesLook.Of(k).Dash : DashStyle.Solid);
                var pts = Enumerable.Range(0, Math.Min(se.X.Count, se.Y.Count)).Select(i => new Vec2(se.X[i], se.Y[i])).ToList();
                var stroke = Stroke(key, color, se.Width ?? 0.5, dash);
                var marker = se.Marker is { } mk ? Marker(ChartTags.Nth(ChartTags.Marker, k), mk, !se.MarkerOpen, color, _f.Fs * 0.3) : null;
                lines[k] = AddSeries(panelOf[k], key, ChartTags.Nth(ChartTags.Marker, k), pts, stroke, marker, se.Step, se.Smooth && !se.Step,
                    showLegend ? FigureComposer.SeriesName(spec, k, _lang) : null);
                var lineColor = stroke?.Color ?? Col(color);

                if (se.Label?.Get(_lang) is { Length: > 0 } label && !showLegend)
                {
                    var lkey = ChartTags.Nth(ChartTags.CurveLabel, k);
                    var lcolor = _x.Mono ? Palette.Ink : lineColor;
                    if (se.LabelAt is { } lx && se.LabelPos != "end")
                    {
                        var ly = OnSeries(k, lx);
                        Add(se.LabelPos switch
                        {
                            "below" => Label(lkey, panelOf[k], lx, ly, label, "center", "center", 0, -0.95 * nfsPt, true, nfs, lcolor),
                            "on" => Label(lkey, panelOf[k], lx, ly, label, "center", "center", 0, 0, true, nfs, lcolor),
                            "right" => Label(lkey, panelOf[k], lx, ly, label, "left", "center", 0.5 * nfsPt, 0, true, nfs, lcolor),
                            "left" => Label(lkey, panelOf[k], lx, ly, label, "right", "center", -0.5 * nfsPt, 0, true, nfs, lcolor),
                            _ => Label(lkey, panelOf[k], lx, ly, label, "center", "center", 0, 0.95 * nfsPt, true, nfs, lcolor),
                        });
                    }
                    else if (lines[k] is { } l)
                    {
                        var last = Enumerable.Range(0, l.X.Count).Where(i => Fmt.Finite(l.X[i]) && Fmt.Finite(l.Y[i])).DefaultIfEmpty(-1).Last();
                        if (last >= 0) Add(Label(lkey, panelOf[k], l.X[last], l.Y[last], label, "left", "center", 0.4 * nfsPt, 0, false, nfs, lcolor));
                    }
                }
                if (se.Numeral is { } num && sNum[k] >= 0)
                {
                    var xs = se.X.Where(Fmt.Finite).ToList();
                    var at = num.At ?? (xs.Count == 0 ? double.NaN : xs.Min() + 0.6 * (xs.Max() - xs.Min()));
                    Leader(num, sNum[k], panelOf[k], at, OnSeries(k, at));
                }
            }

            // Regions (inserted before the curves: drawn below them, as in the renderer).
            var below = 0;
            for (var k = 0; k < spec.Regions.Count; k++)
            {
                var r = spec.Regions[k];
                var key = ChartTags.Nth(ChartTags.Region, k);
                var fill = Area(r, key, FigureEditor.Series(spec, _lang), lines);
                if (fill is null) continue;
                if (!Hidden(key)) _x.Items.Insert(below++, fill);
                if (r.Numeral is { } num && gNum[k] >= 0)
                {
                    if (num.X is { } nx && num.Y is { } ny) Leader(num, gNum[k], Clamp(num.Panel ?? fill.Panel, n), nx, ny);
                    else if (fill.Polygons.Count > 0)
                    {
                        var biggest = fill.Polygons.OrderByDescending(p => Math.Abs(PolyArea(p))).First();
                        Leader(num, gNum[k], fill.Panel, biggest.Average(p => p.X), biggest.Average(p => p.Y));
                    }
                }
            }

            // References.
            for (var k = 0; k < spec.Refs.Count; k++)
            {
                var r = spec.Refs[k];
                var key = ChartTags.Nth(ChartTags.Ref, k);
                var label = r.Label?.Get(_lang);
                if (r.Kind == "h")
                {
                    if (!Fmt.Finite(r.Y)) continue;
                    var panels = r.Panel is { } p && p < 0 ? Enumerable.Range(0, n).ToList() : new List<int> { Clamp(r.Panel ?? 0, n) };
                    var stroke = Stroke(key, r.Color ?? Palette.Helper, r.Width ?? 0.3, FigureComposer.DashOf(r.Line, DashStyle.Dash));
                    int? first = null;
                    foreach (var pi in panels)
                    {
                        if (!_f.Panels[pi].Y.Contains(r.Y)) continue;
                        HLine(pi, r.Y, stroke, key);
                        first ??= pi;
                    }
                    if (first is not { } fi) continue;
                    if (!string.IsNullOrEmpty(label))
                        Add(r.LabelPos switch
                        {
                            "left" => Label(key, fi, 0, r.Y, label!, "left", "bottom", 0.4 * nfsPt, 0.15 * nfsPt, true, nfs, xfrac: true),
                            "end" => Label(key, fi, 1, r.Y, label!, "left", "center", 0.4 * nfsPt, 0, false, nfs, xfrac: true),
                            "below" => Label(key, fi, 1, r.Y, label!, "right", "top", -0.3 * nfsPt, -0.15 * nfsPt, true, nfs, xfrac: true),
                            _ => Label(key, fi, 1, r.Y, label!, "right", "bottom", -0.3 * nfsPt, 0.15 * nfsPt, true, nfs, xfrac: true),
                        });
                    if (r.Numeral is { } num && rNum[k] >= 0)
                    {
                        if (num.Axis)
                        {
                            var nkey = NumKey(rNum[k]);
                            Add(Label(nkey, fi, 0, r.Y, num.Text, "right", "center", -(0.45 + 0.3) * fsPt, 0, xfrac: true));
                        }
                        else Leader(num, rNum[k], fi, Geo.Clamp(num.At ?? _f.X.Min + 0.85 * (_f.X.Max - _f.X.Min), Math.Min(_f.X.Min, _f.X.Max), Math.Max(_f.X.Min, _f.X.Max)), r.Y);
                    }
                }
                else if (r.Kind == "v")
                {
                    if (!Fmt.Finite(r.X) || !_f.X.Contains(r.X)) continue;
                    int top, bottom;
                    if (r.Panel is { } vp && vp >= 0) top = bottom = Clamp(vp, n);
                    else { top = 0; bottom = n - 1; }
                    if (Stroke(key, r.Color ?? Palette.Helper, r.Width ?? 0.3, FigureComposer.DashOf(r.Line, DashStyle.Dash)) is { } stroke)
                        Add(new XVLine { Panel = top, LastPanel = bottom, Key = key, X = r.X, Stroke = stroke });
                    if (!string.IsNullOrEmpty(label))
                    {
                        if (r.LabelPos == "axis") Add(Label(key, n - 1, r.X, 0, label!, "center", "top", 0, -(0.45 + 0.3) * fsPt, yfrac: true));
                        else Add(Label(key, top, r.X, 1, label!, "left", "top", 0.3 * nfsPt, -0.25 * nfsPt, true, nfs, yfrac: true));
                    }
                    if (r.Numeral is { } vn && rNum[k] >= 0)
                    {
                        if (vn.Axis) Add(Label(NumKey(rNum[k]), n - 1, r.X, 0, vn.Text, "center", "top", 0, -(0.45 + 0.3) * fsPt, yfrac: true));
                        else
                        {
                            var pi = Clamp(vn.Panel ?? top, n);
                            if (vn.At is { } at) Leader(vn, rNum[k], pi, r.X, at);
                            else Leader(vn, rNum[k], pi, r.X, 0.5, yfrac: true);
                        }
                    }
                }
                else
                {
                    if (r.Series < 0 || r.Series >= spec.Series.Count) continue;
                    var y = OnSeries(r.Series, r.X);
                    if (!Fmt.Finite(y)) continue;
                    var pi = panelOf[r.Series];
                    var color = r.Color ?? SeriesColor(r.Series);
                    AddSeries(pi, key, key, new[] { new Vec2(r.X, y) }, null, Marker(key, "circle", r.Filled, color, _f.Fs * 0.38));
                    if (!string.IsNullOrEmpty(label))
                    {
                        var d = (_f.Fs * 0.38) * PT + 0.35 * nfsPt;
                        Add(r.LabelPos switch
                        {
                            "left" => Label(key, pi, r.X, y, label!, "right", "center", -d, 0.6 * nfsPt, true, nfs),
                            "above" => Label(key, pi, r.X, y, label!, "center", "bottom", 0, d, true, nfs),
                            "below" => Label(key, pi, r.X, y, label!, "center", "top", 0, -d, true, nfs),
                            _ => Label(key, pi, r.X, y, label!, "left", "center", d, 0.6 * nfsPt, true, nfs),
                        });
                    }
                    if (r.Numeral is { } num && rNum[k] >= 0) Leader(num, rNum[k], pi, r.X, y);
                }
            }

            // Notes.
            for (var k = 0; k < spec.Notes.Count; k++) Note(spec.Notes[k], k, nNum[k], nfs, nfsPt);

            // Panel titles.
            for (var i = 0; i < n; i++)
            {
                var fp = FigureComposer.PanelsOf(spec)[i];
                if (fp.Title?.Get(_lang) is { Length: > 0 } pt)
                    if (Label(ChartTags.Nth(ChartTags.PanelTitle, i), i, 0, 1, pt, "left", "center", 0.5 * nfsPt, -0.75 * nfsPt, true, nfs, bold: true, xfrac: true, yfrac: true) is { } l)
                        _x.PanelTitles.Add(l);
            }
        }

        private static double PolyArea(IReadOnlyList<Vec2> poly)
        {
            var a = 0.0;
            for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++) a += (poly[j].X + poly[i].X) * (poly[j].Y - poly[i].Y);
            return a / 2;
        }

        /// <summary>Value of a step curve at x: "post" holds a value until the next x, "pre" from the previous x, "mid" around it.</summary>
        internal static double StepAt(IReadOnlyList<double> xs, IReadOnlyList<double> ys, double x, string mode)
        {
            var pts = Enumerable.Range(0, Math.Min(xs.Count, ys.Count)).Where(i => Fmt.Finite(xs[i]) && Fmt.Finite(ys[i])).Select(i => (X: xs[i], Y: ys[i])).ToList();
            if (pts.Count == 0 || x < pts[0].X - 1e-12 || x > pts[pts.Count - 1].X + 1e-12) return double.NaN;
            for (var i = 0; i < pts.Count; i++)
            {
                if (Math.Abs(pts[i].X - x) < 1e-12) return pts[i].Y;
                if (i + 1 < pts.Count && x > pts[i].X && x < pts[i + 1].X)
                    return mode switch
                    {
                        "pre" => pts[i + 1].Y,
                        "mid" => x < (pts[i].X + pts[i + 1].X) / 2 ? pts[i].Y : pts[i + 1].Y,
                        _ => pts[i].Y,
                    };
            }
            return double.NaN;
        }

        /// <summary>
        /// A filled area in data coordinates: the semantic call (fill_between with x grid, edges, where-mask and steps;
        /// fill_betweenx; axvspan; axhspan) and the filled polygons as drawn (the renderer's geometry converted to data, for
        /// exports without a matching call); null when it cannot be drawn.
        /// </summary>
        private XFill? Area(FigRegion r, string key, IReadOnlyList<AreaSeries> series, (List<double> X, List<double> Y)?[] lines)
        {
            var n = _f.Panels.Count;
            var (drawn, panel) = ChartAreas.Polygons(r, _f, series);
            if (drawn.Count == 0) return null;
            var fill = new XFill { Key = key, Kind = r.Kind is "span" or "hspan" or "betweenx" ? r.Kind : "between", Panel = panel, Step = r.Step, WhereBelow = r.Where == "below" };
            // Polygons as drawn, in data coordinates of their panel.
            fill.Polygons = drawn.Select(poly =>
            {
                var pi = PanelAt(poly.Average(v => v.Y));
                return poly.Select(v => new Vec2(DataX(v.X), DataY(pi, v.Y))).ToList();
            }).ToList();
            var xa = _f.X;
            double lo = Math.Min(xa.Min, xa.Max), hi = Math.Max(xa.Min, xa.Max);
            switch (r.Kind)
            {
                case "span":
                    {
                        double from = r.From ?? xa.Min, to = r.To ?? xa.Max;
                        fill.X0 = Geo.Clamp(Math.Min(from, to), lo, hi);
                        fill.X1 = Geo.Clamp(Math.Max(from, to), lo, hi);
                        if (!(fill.X1 > fill.X0)) return null;
                        fill.Panels = (r.Panel is { } sp && sp >= 0 ? new[] { Clamp(sp, n) } : Enumerable.Range(0, n).ToArray()).ToList();
                        break;
                    }
                case "hspan":
                    {
                        var y = _f.Panels[panel].Y;
                        double y0 = r.Y1 ?? y.Min, y1 = r.Y2 ?? y.Max;
                        fill.Y0 = Math.Min(y0, y1);
                        fill.Y1 = Math.Max(y0, y1);
                        fill.X0 = r.From is { } f0 ? Geo.Clamp(f0, lo, hi) : lo;
                        fill.X1 = r.To is { } t0 ? Geo.Clamp(t0, lo, hi) : hi;
                        fill.FullWidth = r.From is null && r.To is null;
                        fill.Panels = (r.Panel is { } hp && hp < 0 ? Enumerable.Range(0, n).ToArray() : new[] { Clamp(r.Panel ?? 0, n) }).ToList();
                        break;
                    }
                case "betweenx":
                    {
                        if (r.A < 0 || r.A >= series.Count) return null;
                        var a = series[r.A];
                        var ys = new List<double>();
                        var x1 = new List<double>();
                        var x2 = new List<double>();
                        for (var i = 0; i < Math.Min(a.X.Count, a.Y.Count); i++)
                        {
                            if (!Fmt.Finite(a.X[i]) || !Fmt.Finite(a.Y[i])) continue;
                            double other;
                            if (r.X2 is { } c) other = c;
                            else if (r.B >= 0 && r.B < series.Count) other = Interp(series[r.B].Y, series[r.B].X, a.Y[i]);
                            else return null;
                            ys.Add(a.Y[i]); x1.Add(a.X[i]); x2.Add(other);
                        }
                        fill.X = ys; fill.Lo = x1; fill.Hi = x2;
                        if (r.Where is not null)
                        {
                            fill.Where = x1.Zip(x2, (p, q) => r.Where == "below" ? p <= q : p >= q).ToList();
                            fill.WhereText = "curve " + Fmt.I(r.A + 1) + (r.Where == "below" ? " left of " : " right of ") + (r.X2 is { } cx ? "x = " + NumText(cx) : "curve " + Fmt.I(r.B + 1));
                        }
                        break;
                    }
                default:
                    {
                        if (r.A < 0 || r.A >= series.Count) return null;
                        var yAxis = _f.Panels[panel].Y;
                        // Step areas use the data points (the export's step= draws the steps); others the curves as drawn.
                        (List<double> X, List<double> Y) Edge(int k) => r.Step is not null || lines.Length <= k || lines[k] is not { } l
                            ? (series[k].X.ToList(), series[k].Y.ToList()) : l;
                        var la = Edge(r.A);
                        (List<double> X, List<double> Y)? lb = null;
                        double? constant = null;
                        if (r.Kind == "below") constant = r.Base is { } bv ? Geo.Clamp(bv, yAxis.Min, yAxis.Max) : yAxis.Min;
                        else if (r.Y2 is { } y2) constant = y2;
                        else if (r.B >= 0 && r.B < series.Count && r.B != r.A) lb = Edge(r.B);
                        else return null;
                        if (r.From is { } f0) lo = Math.Max(lo, f0);
                        if (r.To is { } t0) hi = Math.Min(hi, t0);
                        if (!(hi > lo)) return null;
                        var grid = new List<double>();
                        foreach (var x in la.X.Concat(lb?.X ?? new List<double>()).Where(v => Fmt.Finite(v) && v > lo && v < hi).Concat(new[] { lo, hi }).OrderBy(v => v))
                            if (grid.Count == 0 || x - grid[grid.Count - 1] > 1e-9 * Math.Max(1, Math.Abs(x))) grid.Add(x);
                        double At((List<double> X, List<double> Y) l, double x) => r.Step is { } st ? StepAt(l.X, l.Y, x, st) : Interp(l.X, l.Y, x);
                        fill.X = grid;
                        fill.Lo = grid.Select(x => At(la, x)).ToList();
                        fill.Hi = grid.Select(x => constant ?? At(lb!.Value, x)).ToList();
                        if (r.Where is not null && r.Kind == "between")
                        {
                            fill.Where = fill.Lo.Zip(fill.Hi, (p, q) => r.Where == "below" ? p <= q : p >= q).ToList();
                            fill.WhereText = "curve " + Fmt.I(r.A + 1) + (r.Where == "below" ? " below " : " above ") + (constant is { } cv ? "y = " + NumText(cv) : "curve " + Fmt.I(r.B + 1));
                        }
                        break;
                    }
            }
            var mono = _x.Mono;
            var color = ChartAreas.ColorOf(r, series, mono);
            var look = Look(key)?.Style;
            var face = ChartAreas.FaceStyle(r, color, mono, _f.K);
            var edge = r.Edge is { } e ? new XStroke(look?.Stroke ?? (mono ? Palette.Black : r.EdgeColor ?? color), 0.25, FigureComposer.DashOf(e, DashStyle.Solid)) : null;
            var opacity = look?.Opacity ?? face.Opacity;
            if (r.Hatch is { } hatch)
            {
                var p = AreaPattern.Parse(hatch);
                var first = p.Lines.Count > 0 ? p.Lines[0] : (Angle: 45.0, Count: 1);
                fill.Fill = new XFillStyle("pattern", look?.Stroke ?? color, first.Angle, AreaPattern.LineSpacing(ChartAreas.Unit(r, _f.K), first.Count), opacity)
                {
                    HatchCode = hatch,
                    Face = r.Fill == "solid" ? (face.Fill?.Color ?? ChartAreas.Lighten(color, 0.72)) : null,
                    Edge = edge,
                };
            }
            else
            {
                var spacing = r.Spacing ?? Math.Max(0.6, 1.2 * _f.K);
                fill.Fill = r.Fill switch
                {
                    "solid" => new XFillStyle("solid", look?.Fill?.Color ?? (mono ? "#d9d9d9" : color), Opacity: opacity),
                    "none" => new XFillStyle("none", color),
                    "dots" => new XFillStyle("dots", look?.Stroke ?? color, 45, r.Spacing ?? Math.Max(0.6, 1.0 * _f.K), opacity),
                    "cross" => new XFillStyle("cross", look?.Stroke ?? color, r.Angle ?? 45, spacing, opacity),
                    _ => new XFillStyle("hatch", look?.Stroke ?? color, r.Angle ?? 45, spacing, opacity),
                } with { Edge = edge };
            }
            fill.Legend = r.Label?.Get(_lang);
            return fill;
        }

        private static string NumText(double v) => Fmt.Num(v, Lang.En, 6);

        private string SeriesColor(FigureSpec spec, int k) => Col(spec.Series[k].Color ?? SeriesLook.Of(k).Color);

        /// <summary>Page position of a note point (as the composer places it, before keeping it inside the box).</summary>
        private (Vec2 Page, int Panel) NotePage(FigPoint p)
        {
            var n = _f.Panels.Count;
            var pi = Clamp(p.Panel, n);
            var a = _f.Panels[pi].Area;
            var x = Fmt.Finite(p.X) ? _f.Panels[0].Area.X + _f.X.T(p.X) * _f.Panels[0].Area.W : a.X;
            if (!Fmt.Finite(x)) x = a.X;
            double y;
            if (p.Gap is { } g)
            {
                var below = pi < n - 1 ? _f.Panels[pi + 1].Area.Y - a.Bottom : _f.Fs * 1.5;
                y = a.Bottom + g * below;
            }
            else y = p.Y is { } yv ? a.Bottom - _f.Panels[pi].Y.T(yv) * a.H : a.Y + a.H / 2;
            if (!Fmt.Finite(y)) y = a.Y + a.H / 2;
            return (new Vec2(x, y), pi);
        }

        private Vec2 NoteData(FigPoint p)
        {
            var (page, pi) = NotePage(p);
            var x = Fmt.Finite(p.X) ? p.X : DataX(page.X);
            var y = p.Gap is null && p.Y is { } yv ? yv : DataY(pi, page.Y);
            return new Vec2(x, y);
        }

        private void Note(FigNote note, int k, int numeral, double nfs, double nfsPt)
        {
            var key = ChartTags.Nth(ChartTags.Note, k);
            var text = note.Text?.Get(_lang) ?? "";
            var size = nfs * (note.Size ?? 1);
            var sizePt = nfsPt * (note.Size ?? 1);
            var (p1Page, p1Panel) = NotePage(note.P1);
            var p1 = NoteData(note.P1);
            switch (note.Kind)
            {
                case "numeral":
                    {
                        var nkey = ChartTags.Nth(ChartTags.Numeral, Math.Max(0, numeral));
                        if (numeral < 0 || Hidden(nkey) || text.Length == 0) return;
                        Add(new XLeader { Panel = p1Panel, Key = nkey, X = p1.X, Y = p1.Y, Dx = note.Dx, Dy = -note.Dy, Text = text, Color = Look(nkey)?.Style?.TextColor ?? Palette.Ink });
                        return;
                    }
                case "arrow":
                case "line":
                case "dim":
                    {
                        if (note.P2 is not { } q) break;
                        var (p2Page, p2Panel) = NotePage(q);
                        if (p2Page.DistanceTo(p1Page) < 1e-6) break;
                        var p2 = NoteData(q);
                        if (Stroke(key, Palette.Ink, note.Kind == "line" ? 0.2 : 0.25, FigureComposer.DashOf(note.Line, DashStyle.Solid)) is { } stroke)
                            Add(new XSegment { Panel = p1Panel, Panel2 = p2Panel, Key = key, A = p1, B = p2, Kind = note.Kind, Stroke = stroke });
                        if (text.Length == 0) return;
                        if (note.Kind == "dim")
                        {
                            var midPage = (p1Page + p2Page) * 0.5;
                            var pi = PanelAt(midPage.Y);
                            if (p1Panel == p2Panel) pi = p1Panel;
                            double mx = DataX(midPage.X), my = DataY(pi, midPage.Y);
                            var horizontal = Math.Abs(p2Page.X - p1Page.X) >= Math.Abs(p2Page.Y - p1Page.Y);
                            Add((note.Side ?? (horizontal ? "above" : "right")) switch
                            {
                                "below" => Label(key, pi, mx, my, text, "center", "center", 0, -0.75 * sizePt, note.Box, size, italic: note.Italic),
                                "left" => Label(key, pi, mx, my, text, "right", "center", -0.4 * sizePt, 0, note.Box, size, italic: note.Italic),
                                "right" => Label(key, pi, mx, my, text, "left", "center", 0.4 * sizePt, 0, note.Box, size, italic: note.Italic),
                                _ => Label(key, pi, mx, my, text, "center", "center", 0, 0.75 * sizePt, note.Box, size, italic: note.Italic),
                            });
                            return;
                        }
                        // Arrow and line: the text at the tail, away from the line.
                        var d = p1Page - p2Page;
                        var len = d.Length;
                        var u = len > 1e-9 ? d * (1 / len) : new Vec2(-1, 0);
                        var fsMm = _f.Fs * nfs * (note.Size ?? 1);
                        var reach = Math.Abs(u.X) * Fmt.Width(text, fsMm) / 2 + Math.Abs(u.Y) * fsMm * 0.6 + 0.3 * fsMm;
                        Add(Label(key, p1Panel, p1.X, p1.Y, text, "center", "center", u.X * reach * PT, -u.Y * reach * PT, note.Box, size, italic: note.Italic));
                        return;
                    }
            }
            if (text.Length > 0)
            {
                var ha = note.Anchor switch { "start" => "left", "end" => "right", _ => "center" };
                Add(Label(key, p1Panel, p1.X, p1.Y, text, ha, "center", 0, 0, note.Box, size, italic: note.Italic));
            }
        }
    }
}
