using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Charts
{
    /// <summary>
    /// Lays out and draws a <see cref="FigureSpec"/> with the shared <see cref="Frame"/>: stacked panels on one x axis,
    /// data or principle axes, curves with gaps, value lines, vertical markers through all panels, point markers, regions
    /// (between curves, only where one lies above the other, below a curve, between two x values), notes, dimension arrows
    /// and reference numerals with leader lines. Every primitive carries its part key (see <see cref="ChartTags"/>);
    /// everything stays inside the object box.
    /// </summary>
    internal sealed class FigureComposer
    {
        private readonly SketchObject _o;
        private readonly FigureSpec _s;
        private readonly Lang _lang;
        private readonly bool _mono;
        private readonly Size2 _design;
        private Frame _f = null!;
        private Chart _c = null!;
        private Panel[] _ps = Array.Empty<Panel>();
        private readonly List<FigPanel> _fps;
        private SeriesGeo?[] _geo = Array.Empty<SeriesGeo?>();
        private IReadOnlyList<AreaSeries> _areaSeries = Array.Empty<AreaSeries>();
        private int[] _seriesNum = Array.Empty<int>(), _refNum = Array.Empty<int>(), _regionNum = Array.Empty<int>(), _noteNum = Array.Empty<int>();

        public FigureComposer(SketchObject o, RenderContext ctx, FigureSpec spec, Size2 design)
        {
            _o = o; _s = spec; _lang = ctx.Lang; _design = design;
            _mono = ChartType.Mono(ctx) || spec.Mono;
            _fps = PanelsOf(spec);
        }

        private double Fs => _f.Fs;
        /// <summary>Font size of labels and notes.</summary>
        private double NoteFs => _f.Fs * 0.85;
        private double HalfW => _f.W / 2;
        private double HalfH => _f.H / 2;

        /// <summary>The panels of a figure (one data panel when the list is empty).</summary>
        public static List<FigPanel> PanelsOf(FigureSpec s) => s.Panels.Count > 0 ? s.Panels.Take(FigureSpec.MaxPanels).ToList() : new List<FigPanel> { new() };

        private static int Clamp(int i, int n) => Math.Max(0, Math.Min(n - 1, i));

        private static double Safe(double v, double lo, double hi) => hi < lo ? (lo + hi) / 2 : Math.Max(lo, Math.Min(hi, v));

        // ------------------------------------------------------------------ axes

        /// <summary>Axis over data: principle axes from 0 (or below) to the data maximum plus <paramref name="pad"/>, data axes with nice steps.</summary>
        public static Axis BuildAxis(FigAxis a, IEnumerable<double> data, double pad)
        {
            var vals = data.Where(Fmt.Finite).ToList();
            if (a.Log)
                return Axis.Logarithmic(vals, a.Min is { } lmin && a.Max is { } lmax ? (lmin, lmax) : null);
            if (a.Principle)
            {
                var lo = a.Min ?? Math.Min(0, vals.DefaultIfEmpty(0).Min());
                var hi = a.Max ?? (vals.Count == 0 ? 1 : vals.Max());
                if (a.Max is null) hi += (hi - lo) * pad;
                if (!(hi > lo)) hi = lo + 1;
                return Axis.Linear(Array.Empty<double>(), (lo, hi));
            }
            if (a.Min is { } min && a.Max is { } max && max > min) return Axis.Linear(vals, (min, max));
            var auto = Axis.Linear(vals);
            if (a.Min is null && a.Max is null) return auto;
            double l = a.Min ?? auto.Min, h = a.Max ?? auto.Max;
            return h > l ? Axis.Linear(vals, (l, h)) : auto;
        }

        /// <summary>The shared x axis of a figure.</summary>
        public static Axis XAxis(FigureSpec s)
        {
            var data = s.Series.SelectMany(x => x.X)
                .Concat(s.Refs.Where(r => r.Kind is "v" or "point").Select(r => r.X))
                .Concat(XyChart.AreaX(s.Regions));
            return WithBreaks(BuildAxis(s.X, data, 0.05), s.X, s);
        }

        /// <summary>The axis with the breaks of its specification (linear axes only).</summary>
        private static Axis WithBreaks(Axis a, FigAxis fa, FigureSpec s)
            => fa.Breaks.Count == 0 ? a : a.WithBreaks(fa.Breaks, ChartBreaks.StyleOf(s.BreakStyle));

        /// <summary>The y axis of panel <paramref name="i"/>.</summary>
        public static Axis YAxis(FigureSpec s, int i)
        {
            var panels = PanelsOf(s);
            var n = panels.Count;
            var data = s.Series.Where(x => Clamp(x.Panel, n) == i).SelectMany(x => x.Y)
                .Concat(s.Refs.Where(r => r.Kind == "h" && (r.Panel is null && i == 0 || r.Panel is { } p && (p < 0 || Clamp(p, n) == i))).Select(r => r.Y))
                .Concat(XyChart.AreaY(s.Regions.Where(r => RegionPanel(s, r, n) == i)));
            return WithBreaks(BuildAxis(panels[Clamp(i, n)], data, 0.12), panels[Clamp(i, n)], s);
        }

        /// <summary>The panel whose y axis an area's values belong to (its curve's panel, or its own panel for bands); -1 for none.</summary>
        private static int RegionPanel(FigureSpec s, FigRegion r, int n)
        {
            if (r.Kind == "hspan") return r.Panel is { } p && p < 0 ? 0 : Clamp(r.Panel ?? 0, n);
            if (r.Kind is "below" or "between" && r.A >= 0 && r.A < s.Series.Count) return Clamp(s.Series[r.A].Panel, n);
            return -1;
        }

        private string? AxisTitle(FigAxis a)
        {
            var label = a.Label?.Get(_lang)?.Trim() ?? "";
            var unit = a.Unit?.Get(_lang)?.Trim() ?? "";
            if (label.Length == 0 && unit.Length == 0) return null;
            if (unit.Length == 0) return label;
            if (label.Length == 0) return _lang == Lang.De ? "in " + unit : "(" + unit + ")";
            return _lang == Lang.De ? label + " in " + unit : label + " (" + unit + ")";
        }

        private IReadOnlyList<Tick>? Marks(FigAxis a)
        {
            if (!a.Principle && a.Marks.Count == 0) return null;
            var mark = a.Principle ? TickMark.Minor : TickMark.Major;
            return a.Marks.Select(m => new Tick(m.At, mark, m.Text.Get(_lang))).ToList();
        }

        /// <summary>Running index of every reference numeral (series, refs, regions, numeral notes – in this order).</summary>
        public static (int[] Series, int[] Refs, int[] Regions, int[] Notes, List<string> Texts) NumeralIndex(FigureSpec s)
        {
            var texts = new List<string>();
            int Take(FigNumeral? n) { if (n is null) return -1; texts.Add(n.Text); return texts.Count - 1; }
            var se = s.Series.Select(x => Take(x.Numeral)).ToArray();
            var re = s.Refs.Select(x => Take(x.Numeral)).ToArray();
            var rg = s.Regions.Select(x => Take(x.Numeral)).ToArray();
            var no = s.Notes.Select(x => x.Kind == "numeral" && x.Text is not null ? Take(new FigNumeral { Text = x.Text.Get(Lang.En) }) : -1).ToArray();
            return (se, re, rg, no, texts);
        }

        public static string SeriesName(FigureSpec s, int k, Lang lang)
            => s.Series[k].Label?.Get(lang) is { Length: > 0 } l ? l : new I18nText("Curve", "Kurve").Get(lang) + " " + Fmt.I(k + 1);

        public static DashStyle DashOf(string? line, DashStyle def) => line switch
        {
            "solid" => DashStyle.Solid,
            "dash" => DashStyle.Dash,
            "dot" => DashStyle.Dot,
            "dashdot" => DashStyle.DashDot,
            "longdash" => DashStyle.LongDash,
            "dashdotdot" => DashStyle.DashDotDot,
            _ => def,
        };

        internal static MarkerKind? MarkerOf(string? m) => m switch
        {
            "circle" => MarkerKind.Circle,
            "square" => MarkerKind.Square,
            "triangle" => MarkerKind.Triangle,
            "diamond" => MarkerKind.Diamond,
            "cross" => MarkerKind.Cross,
            "plus" => MarkerKind.Plus,
            _ => null,
        };

        private string SeriesColor(int k) => _mono ? Palette.Black : _s.Series[k].Color ?? SeriesLook.Of(k).Color;

        private Style SeriesStyle(int k)
        {
            var s = _s.Series[k];
            return new Style
            {
                Stroke = SeriesColor(k),
                StrokeWidth = s.Width ?? 0.5,
                Dash = DashOf(s.Line, _mono ? SeriesLook.Of(k).Dash : DashStyle.Solid),
                Role = LineRole.Visible,
            };
        }

        // ------------------------------------------------------------------ composition

        public Chart Compose()
        {
            var n = _fps.Count;
            (_seriesNum, _refNum, _regionNum, _noteNum, _) = NumeralIndex(_s);
            var x = XAxis(_s);
            _ps = _fps.Select((fp, i) => new Panel(YAxis(_s, i), AxisTitle(fp))
            {
                Share = fp.Share,
                Arrow = fp.Principle,
                Grid = fp.Grid ?? (fp.Principle ? false : null),
                Ticks = Marks(fp),
            }).ToArray();
            _f = new Frame(_o.Size, _design, x, AxisTitle(_s.X), _ps)
            {
                Title = _s.Title?.Get(_lang) is { Length: > 0 } t ? t : null,
                Caption = _s.Caption?.Get(_lang) is { Length: > 0 } cap ? cap : null,
                Mono = _mono,
                Grid = true,
                LegendMode = _s.Legend,
                XArrow = _s.X.Principle,
                XTicks = Marks(_s.X),
                XGrid = _s.X.Grid ?? (_s.X.Principle ? false : null),
                GridDash = _s.GridStyle switch { "dot" => DashStyle.Dot, "dash" => DashStyle.Dash, _ => null },
                PanelGap = _s.PanelGap ?? (n > 1 && _ps.Any(p => p.Arrow) ? 2.0 : null),
            };
            for (var k = 0; k < _s.Series.Count; k++)
            {
                var se = _s.Series[k];
                var mk = MarkerOf(se.Marker);
                _f.Legend.Add(new LegendEntry(SeriesName(_s, k, _lang), SeriesStyle(k), mk, !se.MarkerOpen));
            }
            _areaSeries = FigureEditor.Series(_s, _lang);
            foreach (var r in _s.Regions.Where(r => r.Label is not null))
                _f.Legend.Add(new LegendEntry(r.Label!.Get(_lang), ChartAreas.LegendStyle(r, ChartAreas.ColorOf(r, _areaSeries, _mono), _mono, _f.K), Area: true));
            _f.ExtraRight = ExtraRight(x);
            AxisTexts(n);
            _f.Layout(_lang);
            _c = new Chart(_f, _lang);

            _geo = _s.Series.Select((se, k) => SeriesGeo.Build(se, k, _f, _ps[Clamp(se.Panel, n)])).ToArray();
            for (var k = 0; k < _s.Regions.Count; k++) DrawRegion(k);
            for (var k = 0; k < _s.Refs.Count; k++) if (_s.Refs[k].Kind != "point") DrawLineRef(k);
            for (var k = 0; k < _s.Series.Count; k++) DrawSeries(k);
            for (var k = 0; k < _s.Refs.Count; k++) if (_s.Refs[k].Kind == "point") DrawPointRef(k);
            for (var k = 0; k < _s.Notes.Count; k++) DrawNote(k);
            for (var i = 0; i < n; i++)
                if (_fps[i].Title?.Get(_lang) is { Length: > 0 } pt)
                    Text(_c.Over, new Vec2(_ps[i].Area.X + 0.5 * NoteFs, _ps[i].Area.Y + 0.75 * NoteFs), pt, NoteFs, TextAnchor.Start, ChartTags.Nth(ChartTags.PanelTitle, i), true, Palette.Ink, bold: true);
            return _c;
        }

        /// <summary>Space right of the plot for labels at curve ends and value line labels outside the plot (in font heights).</summary>
        private double ExtraRight(Axis x)
        {
            var em = 0.0;
            if (_s.Legend == "none")
                foreach (var se in _s.Series)
                {
                    if (se.Label is null || se.LabelAt is not null && se.LabelPos != "end") continue;
                    var last = Enumerable.Range(0, Math.Min(se.X.Count, se.Y.Count)).Where(i => Fmt.Finite(se.X[i]) && Fmt.Finite(se.Y[i])).Select(i => se.X[i]).DefaultIfEmpty(double.NaN).Last();
                    if (Fmt.Finite(last) && x.T(last) > 0.75) em = Math.Max(em, se.Label.Get(_lang).Length * Fmt.CharWidth * 0.85 + 0.6);
                }
            foreach (var r in _s.Refs)
                if (r.Kind == "h" && r.LabelPos == "end" && r.Label is not null) em = Math.Max(em, r.Label.Get(_lang).Length * Fmt.CharWidth * 0.85 + 0.6);
            return Math.Min(em, 14);
        }

        /// <summary>Texts at the axes (numerals of value lines at the y axis, marker labels and numerals below the x axis); before the layout.</summary>
        private void AxisTexts(int n)
        {
            for (var k = 0; k < _s.Refs.Count; k++)
            {
                var r = _s.Refs[k];
                if (r.Kind == "h" && r.Numeral is { Axis: true } num && Fmt.Finite(r.Y))
                    _ps[HPanels(r, n)[0]].Texts.Add(new AxisText(r.Y, num.Text, ChartTags.Nth(ChartTags.Numeral, _refNum[k])));
                if (r.Kind == "v" && Fmt.Finite(r.X))
                {
                    if (r.LabelPos == "axis" && r.Label is not null) _f.XTexts.Add(new AxisText(r.X, r.Label.Get(_lang), ChartTags.Nth(ChartTags.Ref, k)));
                    if (r.Numeral is { Axis: true } vn) _f.XTexts.Add(new AxisText(r.X, vn.Text, ChartTags.Nth(ChartTags.Numeral, _refNum[k])));
                }
            }
        }

        private static List<int> HPanels(FigRef r, int n)
            => r.Panel is { } p && p < 0 ? Enumerable.Range(0, n).ToList() : new List<int> { Clamp(r.Panel ?? 0, n) };

        // ------------------------------------------------------------------ coordinates

        private double PX(double x) => _ps[0].Area.X + _f.X.T(x) * _ps[0].Area.W;

        private double PY(int panel, double y)
        {
            var p = _ps[panel];
            return p.Area.Bottom - p.Y.T(y) * p.Area.H;
        }

        private Vec2 InBox(Vec2 v) => new(Safe(v.X, -HalfW + 0.05, HalfW - 0.05), Safe(v.Y, -HalfH + 0.05, HalfH - 0.05));

        private Vec2 NotePoint(FigPoint p)
        {
            var n = _ps.Length;
            var pi = Clamp(p.Panel, n);
            var a = _ps[pi].Area;
            var x = Fmt.Finite(p.X) ? PX(p.X) : a.X;
            if (!Fmt.Finite(x)) x = a.X;
            double y;
            if (p.Gap is { } g)
            {
                var below = pi < n - 1 ? _ps[pi + 1].Area.Y - a.Bottom : Fs * 1.5;
                y = a.Bottom + g * below;
            }
            else y = p.Y is { } yv ? PY(pi, yv) : a.Y + a.H / 2;
            if (!Fmt.Finite(y)) y = a.Y + a.H / 2;
            return InBox(new Vec2(x, y));
        }

        /// <summary>Point on curve <paramref name="k"/> at data x (on the drawn curve where it exists, else interpolated across gaps).</summary>
        private Vec2? OnSeries(int k, double x)
        {
            if (k < 0 || k >= _geo.Length || _geo[k] is not { } g || !Fmt.Finite(x)) return null;
            var px = PX(x);
            if (!Fmt.Finite(px)) return null;
            var py = g.At(px);
            if (Fmt.Finite(py)) return new Vec2(px, py);
            var se = _s.Series[k];
            var pts = Enumerable.Range(0, Math.Min(se.X.Count, se.Y.Count)).Where(i => Fmt.Finite(se.X[i]) && Fmt.Finite(se.Y[i])).Select(i => new Vec2(se.X[i], se.Y[i])).ToList();
            for (var i = 0; i + 1 < pts.Count; i++)
            {
                var a = pts[i]; var b = pts[i + 1];
                if (x < Math.Min(a.X, b.X) || x > Math.Max(a.X, b.X)) continue;
                var t = Math.Abs(b.X - a.X) < 1e-12 ? 0 : (x - a.X) / (b.X - a.X);
                var y = PY(g.PanelIndex, a.Y + (b.Y - a.Y) * t);
                if (Fmt.Finite(y)) return new Vec2(px, y);
            }
            return null;
        }

        // ------------------------------------------------------------------ drawing helpers

        /// <summary>
        /// A text kept inside the box (shrunk when wider than the box or <paramref name="maxWidth"/>), optionally on a white
        /// backing. Returns its centre and half extents.
        /// </summary>
        private (Vec2 Center, double Hx, double Hy) Text(List<Primitive> into, Vec2 at, string text, double size, TextAnchor anchor, string tag, bool backing, string color,
            bool italic = false, bool bold = false, double maxWidth = double.PositiveInfinity)
        {
            var pad = 0.3 * Fs;
            double hw = HalfW - pad, hh = HalfH - pad;
            var len = Math.Max(1, text.Length);
            var fs = Math.Min(size, Math.Min(2 * hw - 0.4 * size, maxWidth) / (len * Fmt.CharWidth));
            fs = Math.Max(0.05, Math.Min(fs, 2 * hh / 1.3));
            var w = len * fs * Fmt.CharWidth;
            var m = backing ? 0.15 * fs : 0;
            var x0 = anchor switch { TextAnchor.Start => at.X, TextAnchor.End => at.X - w, _ => at.X - w / 2 };
            x0 = Safe(x0, -hw + m, hw - m - w);
            var y = Safe(Fmt.Finite(at.Y) ? at.Y : 0, -hh + 0.6 * fs, hh - 0.6 * fs);
            var tx = anchor switch { TextAnchor.Start => x0, TextAnchor.End => x0 + w, _ => x0 + w / 2 };
            if (backing) into.Add(Prim.Rect(x0 - m, y - 0.6 * fs, w + 2 * m, 1.2 * fs, S.FillOnly(Palette.White)) with { Tag = tag });
            into.Add(new TextPrim(new Vec2(tx, y), text, new TextStyle { Size = fs, Anchor = anchor, Color = color, Italic = italic, Bold = bold }) { Tag = tag });
            return (new Vec2(x0 + w / 2, y), w / 2 + m, 0.6 * fs);
        }

        private static Style Thin(string color, DashStyle dash, double width = 0.25) => new() { Stroke = color, StrokeWidth = width, Dash = dash, Role = LineRole.Thin };

        /// <summary>A reference numeral: text beside the target (offset in font heights) and a thin leader line to the target.</summary>
        private void Numeral(Vec2 target, string text, double dx, double dy, string tag)
        {
            if (string.IsNullOrEmpty(text) || !Frame.Ok(target)) return;
            target = InBox(target);
            var size = Fs * 0.95;
            var placed = Text(_c.Over, target + new Vec2(dx, dy) * Fs, text, size, TextAnchor.Middle, tag, false, Palette.Ink);
            var c = placed.Center;
            double hx = placed.Hx + 0.2 * size, hy = placed.Hy + 0.1 * size;
            var d = target - c;
            if (Math.Abs(d.X) <= hx && Math.Abs(d.Y) <= hy) return;
            var s = Math.Min(Math.Abs(d.X) > 1e-12 ? hx / Math.Abs(d.X) : double.MaxValue, Math.Abs(d.Y) > 1e-12 ? hy / Math.Abs(d.Y) : double.MaxValue);
            var start = c + d * Math.Min(1, s);
            if (start.DistanceTo(target) < 0.3 * size) return;
            _c.Over.Add(Prim.Line(InBox(start), target, Thin(Palette.Ink, DashStyle.Solid, 0.2)) with { Tag = tag });
        }

        private string NumTag(int j) => ChartTags.Nth(ChartTags.Numeral, Math.Max(0, j));

        // ------------------------------------------------------------------ series

        private void DrawSeries(int k)
        {
            if (_geo[k] is not { } g) return;
            var se = _s.Series[k];
            var style = SeriesStyle(k);
            var tag = ChartTags.Nth(ChartTags.Series, k);
            var area = g.Panel.Area;
            var segs = new List<PathSeg>();
            foreach (var piece in g.Page)
                if (piece.Count >= 2 && Draw.Series(piece, area, style, se.Smooth && !se.Step && piece.Count >= 3, tag) is { } p) segs.AddRange(p.Segments);
            if (segs.Count > 0) _c.Lines.Add(new PathPrim(segs, style) { Tag = tag });
            foreach (var piece in g.Sampled) _c.Avoid.Add(piece);

            var points = new List<Vec2>();
            for (var i = 0; i < Math.Min(se.X.Count, se.Y.Count); i++)
            {
                if (!Fmt.Finite(se.X[i]) || !Fmt.Finite(se.Y[i]) || !_f.IsVisible(g.Panel, se.X[i], se.Y[i])) continue;
                var p = _f.P(g.Panel, se.X[i], se.Y[i]);
                if (!Frame.Ok(p)) continue;
                points.Add(p);
                _c.Port(p, "s" + Fmt.I(k + 1) + "." + Fmt.I(i + 1));
            }
            if (MarkerOf(se.Marker) is { } mk)
            {
                var visible = points.Where(p => area.Inflate(0.01).Contains(p)).ToList();
                if (visible.Count > 0) _c.Lines.Add(Draw.Markers(visible, mk, !se.MarkerOpen, style.EffectiveStroke, Fs * 0.3) with { Tag = ChartTags.Nth(ChartTags.Marker, k) });
            }

            // Label on the curve or at its end (not with a legend).
            if (se.Label?.Get(_lang) is { Length: > 0 } label && _s.Legend == "none")
            {
                var color = _mono ? Palette.Ink : style.EffectiveStroke;
                var ltag = ChartTags.Nth(ChartTags.CurveLabel, k);
                if (se.LabelAt is { } lx && se.LabelPos != "end")
                {
                    if (OnSeries(k, lx) is { } pt && area.Inflate(Fs).Contains(pt))
                    {
                        var fs = NoteFs;
                        switch (se.LabelPos ?? "above")
                        {
                            case "below": Text(_c.Over, pt + new Vec2(0, 0.95 * fs), label, fs, TextAnchor.Middle, ltag, true, color); break;
                            case "on": Text(_c.Over, pt, label, fs, TextAnchor.Middle, ltag, true, color); break;
                            case "right": Text(_c.Over, pt + new Vec2(0.5 * fs, 0), label, fs, TextAnchor.Start, ltag, true, color); break;
                            case "left": Text(_c.Over, pt + new Vec2(-0.5 * fs, 0), label, fs, TextAnchor.End, ltag, true, color); break;
                            default: Text(_c.Over, pt + new Vec2(0, -0.95 * fs), label, fs, TextAnchor.Middle, ltag, true, color); break;
                        }
                    }
                }
                else if (g.LastVisible(area) is { } end)
                    Text(_c.Over, end + new Vec2(0.4 * NoteFs, 0), label, NoteFs, TextAnchor.Start, ltag, false, color);
            }

            if (se.Numeral is { } num && _seriesNum[k] >= 0)
            {
                var xs = se.X.Where(Fmt.Finite).ToList();
                var at = num.At ?? (xs.Count == 0 ? double.NaN : xs.Min() + 0.6 * (xs.Max() - xs.Min()));
                if (OnSeries(k, at) is { } target) Numeral(target, num.Text, num.Dx, num.Dy, NumTag(_seriesNum[k]));
            }
        }

        // ------------------------------------------------------------------ references

        private void DrawLineRef(int k)
        {
            var r = _s.Refs[k];
            var n = _ps.Length;
            var tag = ChartTags.Nth(ChartTags.Ref, k);
            var color = _mono ? Palette.Ink : r.Color ?? Palette.Helper;
            var style = Thin(color, DashOf(r.Line, DashStyle.Dash), r.Width ?? 0.3);
            var label = r.Label?.Get(_lang);
            if (r.Kind == "h")
            {
                if (!Fmt.Finite(r.Y)) return;
                var lines = new List<IReadOnlyList<Vec2>>();
                int? first = null;
                foreach (var pi in HPanels(r, n))
                {
                    var p = _ps[pi];
                    if (!p.Y.Visible(r.Y)) continue;
                    var y = PY(pi, r.Y);
                    lines.Add(new[] { new Vec2(p.Area.X, y), new Vec2(p.Area.Right, y) });
                    first ??= pi;
                }
                if (first is not { } fi) return;
                if (Draw.MultiPath(lines, style, tag) is { } path) _c.Lines.Add(path);
                var a = _ps[fi].Area;
                var py = PY(fi, r.Y);
                if (!string.IsNullOrEmpty(label))
                {
                    var fs = NoteFs;
                    switch (r.LabelPos)
                    {
                        case "left": Text(_c.Over, new Vec2(a.X + 0.4 * fs, py - 0.7 * fs), label!, fs, TextAnchor.Start, tag, true, Palette.Ink); break;
                        case "end": Text(_c.Over, new Vec2(a.Right + 0.4 * fs, py), label!, fs, TextAnchor.Start, tag, false, Palette.Ink); break;
                        case "below": Text(_c.Over, new Vec2(a.Right - 0.3 * fs, py + 0.7 * fs), label!, fs, TextAnchor.End, tag, true, Palette.Ink); break;
                        default: Text(_c.Over, new Vec2(a.Right - 0.3 * fs, py - 0.7 * fs), label!, fs, TextAnchor.End, tag, true, Palette.Ink); break;
                    }
                }
                if (r.Numeral is { Axis: false } num && _refNum[k] >= 0)
                {
                    var at = num.At ?? _f.X.Min + 0.85 * (_f.X.Max - _f.X.Min);
                    var px = PX(at);
                    if (Fmt.Finite(px)) Numeral(new Vec2(Safe(px, a.X, a.Right), py), num.Text, num.Dx, num.Dy, NumTag(_refNum[k]));
                }
                return;
            }

            // Vertical marker: through all panels (default) or one panel.
            if (!Fmt.Finite(r.X) || !_f.X.Visible(r.X)) return;
            var x = PX(r.X);
            int top, bottom;
            if (r.Panel is { } vp && vp >= 0) top = bottom = Clamp(vp, n);
            else { top = 0; bottom = n - 1; }
            var y0 = _ps[top].Area.Y;
            var y1 = _ps[bottom].Area.Bottom;
            _c.Lines.Add(Prim.Line(new Vec2(x, y0), new Vec2(x, y1), style) with { Tag = tag });
            if (!string.IsNullOrEmpty(label) && r.LabelPos != "axis")
            {
                var fs = NoteFs;
                var w = Fmt.Width(label!, fs);
                var right = x + 0.3 * fs + w <= _ps[top].Area.Right;
                Text(_c.Over, new Vec2(right ? x + 0.3 * fs : x - 0.3 * fs, y0 + 0.75 * fs), label!, fs, right ? TextAnchor.Start : TextAnchor.End, tag, true, Palette.Ink);
            }
            if (r.Numeral is { Axis: false } vnum && _refNum[k] >= 0)
            {
                var pi = Clamp(vnum.Panel ?? top, n);
                var a = _ps[pi].Area;
                var y = vnum.At is { } at ? PY(pi, at) : a.Y + a.H / 2;
                if (Fmt.Finite(y)) Numeral(new Vec2(x, Safe(y, a.Y, a.Bottom)), vnum.Text, vnum.Dx, vnum.Dy, NumTag(_refNum[k]));
            }
        }

        private void DrawPointRef(int k)
        {
            var r = _s.Refs[k];
            if (r.Series < 0 || r.Series >= _s.Series.Count || _geo[r.Series] is not { } g || _f.X.InBreak(r.X)) return;
            if (OnSeries(r.Series, r.X) is not { } pt || !g.Panel.Area.Inflate(0.01).Contains(pt)) return;
            var tag = ChartTags.Nth(ChartTags.Ref, k);
            var color = _mono ? Palette.Ink : r.Color ?? SeriesColor(r.Series);
            var rad = Fs * 0.38;
            _c.Lines.Add(Draw.Markers(new[] { pt }, MarkerKind.Circle, r.Filled, color, rad) with { Tag = tag });
            _c.Port(pt, "ref" + Fmt.I(k + 1));
            if (r.Label?.Get(_lang) is { Length: > 0 } label)
            {
                var fs = NoteFs;
                var d = rad + 0.35 * fs;
                switch (r.LabelPos)
                {
                    case "left": Text(_c.Over, pt + new Vec2(-d, -0.6 * fs), label, fs, TextAnchor.End, tag, true, Palette.Ink); break;
                    case "above": Text(_c.Over, pt + new Vec2(0, -d - 0.5 * fs), label, fs, TextAnchor.Middle, tag, true, Palette.Ink); break;
                    case "below": Text(_c.Over, pt + new Vec2(0, d + 0.5 * fs), label, fs, TextAnchor.Middle, tag, true, Palette.Ink); break;
                    default: Text(_c.Over, pt + new Vec2(d, -0.6 * fs), label, fs, TextAnchor.Start, tag, true, Palette.Ink); break;
                }
            }
            if (r.Numeral is { } num && _refNum[k] >= 0) Numeral(pt, num.Text, num.Dx, num.Dy, NumTag(_refNum[k]));
        }

        // ------------------------------------------------------------------ regions

        private void DrawRegion(int k)
        {
            var r = _s.Regions[k];
            var n = _ps.Length;
            // Geometry and drawing shared with the XY chart's areas (every kind, pattern, edge; ChartAreas).
            var (polys, panel) = ChartAreas.Polygons(r, _f, _areaSeries);
            if (polys.Count == 0) return;
            _c.Areas.AddRange(ChartAreas.Render(r, polys, ChartTags.Nth(ChartTags.Region, k), ChartAreas.ColorOf(r, _areaSeries, _mono), _mono, _f.K));

            if (r.Numeral is { } num && _regionNum[k] >= 0)
            {
                Vec2 target;
                if (num.X is { } nx && num.Y is { } ny)
                {
                    var pi = Clamp(num.Panel ?? panel, n);
                    target = new Vec2(PX(nx), PY(pi, ny));
                }
                else
                {
                    var biggest = polys.OrderByDescending(p => Math.Abs(Area(p))).First();
                    target = new Vec2(biggest.Average(p => p.X), biggest.Average(p => p.Y));
                }
                Numeral(target, num.Text, num.Dx, num.Dy, NumTag(_regionNum[k]));
            }
        }

        private static double Area(IReadOnlyList<Vec2> poly)
        {
            var a = 0.0;
            for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++) a += (poly[j].X + poly[i].X) * (poly[j].Y - poly[i].Y);
            return a / 2;
        }

        /// <summary>
        /// Polygons between two curves y = a(x) and y = b(x) in page coordinates over the x range of the region (and the
        /// panel): crossing points are inserted, gaps of either curve split the region, and with <paramref name="above"/>
        /// only the parts where a lies above b (smaller page y) are kept.
        /// </summary>
        internal static List<List<Vec2>> Between(Func<double, double> fa, IReadOnlyList<double> xa, Func<double, double> fb, IReadOnlyList<double> xb, FigRegion r, Rect area, bool above,
            Func<double, double>? px = null)
        {
            var lo = area.X; var hi = area.Right;
            if (px is not null)
            {
                if (r.From is { } from && Fmt.Finite(px(from))) lo = Math.Max(lo, px(from));
                if (r.To is { } to && Fmt.Finite(px(to))) hi = Math.Min(hi, px(to));
            }
            var result = new List<List<Vec2>>();
            if (!(hi > lo)) return result;
            var xs = xa.Concat(xb).Where(x => x > lo && x < hi).Concat(new[] { lo, hi }).OrderBy(x => x).ToList();
            var grid = new List<double>();
            foreach (var x in xs) if (grid.Count == 0 || x - grid[grid.Count - 1] > 1e-7) grid.Add(x);
            var all = new List<(double X, bool Crossing)>();
            for (var i = 0; i < grid.Count; i++)
            {
                all.Add((grid[i], false));
                if (i + 1 >= grid.Count) continue;
                double d0 = fb(grid[i]) - fa(grid[i]), d1 = fb(grid[i + 1]) - fa(grid[i + 1]);
                if (Fmt.Finite(d0) && Fmt.Finite(d1) && d0 * d1 < 0) all.Add((grid[i] + (grid[i + 1] - grid[i]) * d0 / (d0 - d1), true));
            }
            double Y(double y) => Math.Max(area.Y, Math.Min(area.Bottom, y));
            var run = new List<(double X, double A, double B)>();
            void Flush()
            {
                if (run.Count >= 2)
                {
                    var poly = run.Select(p => new Vec2(p.X, Y(p.A))).Concat(Enumerable.Reverse(run).Select(p => new Vec2(p.X, Y(p.B)))).ToList();
                    if (Math.Abs(Area(poly)) > 1e-6) result.Add(poly);
                }
                run.Clear();
            }
            bool Ok(double a, double b) => Fmt.Finite(a) && Fmt.Finite(b) && (!above || b - a >= -1e-7);
            foreach (var (x, crossing) in all)
            {
                double a = fa(x), b = fb(x);
                if (!Ok(a, b)) { Flush(); continue; }
                if (run.Count > 0)
                {
                    var m = (run[run.Count - 1].X + x) / 2;
                    if (!Ok(fa(m), fb(m))) Flush();
                }
                run.Add((x, a, b));
                // A crossing closes the part before it and starts the next one (no self-intersecting polygons).
                if (crossing) { Flush(); run.Add((x, a, b)); }
            }
            Flush();
            return result;
        }

        // ------------------------------------------------------------------ notes

        private void DrawNote(int k)
        {
            var note = _s.Notes[k];
            var tag = ChartTags.Nth(ChartTags.Note, k);
            var text = note.Text?.Get(_lang) ?? "";
            var fs = NoteFs * (note.Size ?? 1);
            var p1 = NotePoint(note.P1);
            var p2 = note.P2 is { } q ? NotePoint(q) : (Vec2?)null;
            var maxW = double.PositiveInfinity;
            if (note.MaxWidth is { } mw && Fmt.Finite(note.P1.X))
            {
                var w = Math.Abs(PX(note.P1.X + mw / 2) - PX(note.P1.X - mw / 2));
                if (Fmt.Finite(w) && w > 0) maxW = w;
            }
            var anchor = note.Anchor switch { "start" => TextAnchor.Start, "end" => TextAnchor.End, _ => TextAnchor.Middle };
            switch (note.Kind)
            {
                case "numeral":
                    if (_noteNum[k] >= 0) Numeral(p1, text, note.Dx, note.Dy, NumTag(_noteNum[k]));
                    return;
                case "arrow":
                case "line":
                case "dim":
                    {
                        if (p2 is not { } b || b.DistanceTo(p1) < 1e-6) break;
                        var style = Thin(Palette.Ink, DashOf(note.Line, DashStyle.Solid), note.Kind == "line" ? 0.2 : 0.25);
                        if (note.Kind == "arrow") style = style with { ArrowEnd = ArrowHead.Triangle, ArrowSize = ArrowSize.Small };
                        if (note.Kind == "dim") style = style with { ArrowStart = ArrowHead.Triangle, ArrowEnd = ArrowHead.Triangle, ArrowSize = ArrowSize.Small };
                        _c.Over.Add(Prim.Line(p1, b, style) with { Tag = tag });
                        if (text.Length == 0) return;
                        if (note.Kind == "dim")
                        {
                            var mid = (p1 + b) * 0.5;
                            var horizontal = Math.Abs(b.X - p1.X) >= Math.Abs(b.Y - p1.Y);
                            switch (note.Side ?? (horizontal ? "above" : "right"))
                            {
                                case "below": Text(_c.Over, mid + new Vec2(0, 0.75 * fs), text, fs, TextAnchor.Middle, tag, false, Palette.Ink, note.Italic, maxWidth: maxW); break;
                                case "left": Text(_c.Over, mid + new Vec2(-0.4 * fs, 0), text, fs, TextAnchor.End, tag, false, Palette.Ink, note.Italic, maxWidth: maxW); break;
                                case "right": Text(_c.Over, mid + new Vec2(0.4 * fs, 0), text, fs, TextAnchor.Start, tag, false, Palette.Ink, note.Italic, maxWidth: maxW); break;
                                default: Text(_c.Over, mid + new Vec2(0, -0.75 * fs), text, fs, TextAnchor.Middle, tag, false, Palette.Ink, note.Italic, maxWidth: maxW); break;
                            }
                            return;
                        }
                        // Arrow and line: the text at the tail, away from the line.
                        var d = p1 - b;
                        var len = d.Length;
                        var u = len > 1e-9 ? d * (1 / len) : new Vec2(-1, 0);
                        var w = Fmt.Width(text, fs);
                        var reach = Math.Abs(u.X) * w / 2 + Math.Abs(u.Y) * fs * 0.6 + 0.3 * fs;
                        Text(_c.Over, p1 + u * reach, text, fs, TextAnchor.Middle, tag, note.Box, Palette.Ink, note.Italic, maxWidth: maxW);
                        return;
                    }
            }
            if (text.Length > 0) Text(_c.Over, p1, text, fs, anchor, tag, note.Box, Palette.Ink, note.Italic, maxWidth: maxW);
        }

        // ------------------------------------------------------------------ series geometry

        /// <summary>A curve in page coordinates: drawn pieces (gaps split them) and the sampled polyline used for regions and markers.</summary>
        private sealed class SeriesGeo
        {
            public Panel Panel { get; private set; } = null!;
            public int PanelIndex { get; private set; }
            public List<List<Vec2>> Page { get; } = new();
            public List<List<Vec2>> Sampled { get; } = new();
            public double[] Xs { get; private set; } = Array.Empty<double>();

            public static SeriesGeo? Build(FigSeries s, int k, Frame f, Panel panel)
            {
                var g = new SeriesGeo { Panel = panel, PanelIndex = Math.Max(0, f.Panels.ToList().IndexOf(panel)) };
                var n = Math.Min(s.X.Count, s.Y.Count);
                var data = new List<List<Vec2>>();
                List<Vec2>? cur = null;
                for (var i = 0; i < n; i++)
                {
                    if (!Fmt.Finite(s.X[i]) || !Fmt.Finite(s.Y[i])) { cur = null; continue; }
                    if (cur is null) { cur = new List<Vec2>(); data.Add(cur); }
                    cur.Add(new Vec2(s.X[i], s.Y[i]));
                }
                foreach (var piece in data)
                {
                    var pts = s.Step ? XyChart.Steps(piece) : piece;
                    // Axis breaks interrupt the curve (visible runs, cut at the break limits).
                    foreach (var run in f.VisibleRuns(panel, pts))
                    {
                        List<Vec2>? page = null;
                        foreach (var p in run)
                        {
                            var q = f.P(panel, p.X, p.Y);
                            if (!Frame.Ok(q)) { page = null; continue; }
                            if (page is null) { page = new List<Vec2>(); g.Page.Add(page); }
                            page.Add(q);
                        }
                    }
                }
                foreach (var piece in g.Page)
                    g.Sampled.Add(s.Smooth && !s.Step && piece.Count >= 3 ? Draw.Sample(Draw.Spline(piece)) : piece);
                g.Xs = g.Sampled.SelectMany(p => p.Select(v => v.X)).ToArray();
                return g;
            }

            /// <summary>Page y of the curve at page x (first piece that covers x); NaN in gaps and outside.</summary>
            public double At(double x)
            {
                foreach (var piece in Sampled)
                    for (var i = 0; i + 1 < piece.Count; i++)
                    {
                        var a = piece[i]; var b = piece[i + 1];
                        if (x < Math.Min(a.X, b.X) - 1e-9 || x > Math.Max(a.X, b.X) + 1e-9) continue;
                        if (Math.Abs(b.X - a.X) < 1e-12) return a.Y;
                        return a.Y + (b.Y - a.Y) * (x - a.X) / (b.X - a.X);
                    }
                return double.NaN;
            }

            /// <summary>The last drawn point inside the area (end of the curve).</summary>
            public Vec2? LastVisible(Rect area)
            {
                var inside = area.Inflate(1e-6);
                for (var p = Sampled.Count - 1; p >= 0; p--)
                    for (var i = Sampled[p].Count - 1; i >= 0; i--)
                        if (inside.Contains(Sampled[p][i])) return Sampled[p][i];
                return null;
            }
        }
    }
}
