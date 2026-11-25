using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Charts
{
    /// <summary>
    /// XY chart: one or more series as lines, lines with markers, markers only (scatter) or steps; optional smooth curves,
    /// linear or log axes, limit lines and a legend. Data: <see cref="ChartData"/> text format.
    /// </summary>
    internal sealed class XyChart : ChartType
    {
        public const string TypeId = "chart.xy";

        public const string DefaultData = "0;1.2\n1;2.4\n2;3.1\n3;3.9\n4;4.3\n5;4.8\n6;5.0";

        public static readonly ParamDef DataDef = Prm.Text("data", "Data (rows x;y, blank line = new series)", "Daten (Zeilen x;y, Leerzeile = neue Reihe)", DefaultData);
        public static readonly ParamDef StyleDef = Prm.Choice("style", "Style", "Darstellung", "line",
            ("line", "Line", "Linie"), ("linepoints", "Line with markers", "Linie mit Markern"), ("points", "Markers (scatter)", "Punkte (Streudiagramm)"), ("step", "Steps", "Stufen"));
        public static readonly ParamDef SmoothDef = Prm.Bool("smooth", "Smooth curve", "Geglättete Kurve", false);

        public XyChart() : base(TypeId, new I18nText("XY chart", "XY-Diagramm"), ChartCategories.Data,
            "chart", "graph", "plot", "xy", "line chart", "scatter", "step", "curve", "series", "data", "excel",
            "diagramm", "liniendiagramm", "punktdiagramm", "streudiagramm", "stufendiagramm", "kurve", "messreihe", "daten")
        { }

        protected override IReadOnlyList<ParamDef> BuildParams() => new[]
        {
            DataDef, StyleDef, SmoothDef, Common.Title, Common.XLabel, Common.XUnit, Common.YLabel, Common.YUnit,
            Common.Grid, Common.Legend, Common.Limits, Common.XScale, Common.YScale, Common.XRange, Common.YRange, Common.Box,
            ChartBreaks.YBreaks, ChartBreaks.XBreaks, ChartBreaks.Style,
        };

        /// <summary>The axes of the chart without breaks (also used to suggest a break).</summary>
        private static (Axis X, Axis Y, IReadOnlyList<XySeries> Series, IReadOnlyList<double> Limits) PlainAxes(SketchObject o)
        {
            var series = ChartData.Xy(ChartData.Parse(o.Str("data", DefaultData)));
            var limits = ChartData.Numbers(o.Str("limits", ""));
            var xlog = o.Str("xscale", "linear") == "log";
            var ylog = o.Str("yscale", "linear") == "log";
            var x = Axis.Of(xlog, series.SelectMany(s => s.Points.Select(p => p.X)), ChartData.Range(o.Str("xrange", "")));
            var y = Axis.Of(ylog, series.SelectMany(s => s.Points.Select(p => p.Y)).Concat(limits), ChartData.Range(o.Str("yrange", "")));
            return (x, y, series, limits);
        }

        internal override Chart Compose(SketchObject o, RenderContext ctx)
        {
            var lang = ctx.Lang;
            var (x, y, series, _) = PlainAxes(o);
            x = ChartBreaks.Apply(x, o, ChartBreaks.XKey);
            y = ChartBreaks.Apply(y, o, ChartBreaks.YKey);
            var panel = new Panel(y, Common.AxisTitle(o, "ylabel", "yunit", "y", null, lang));
            var f = new Frame(o.Size, Design, x, Common.AxisTitle(o, "xlabel", "xunit", "x", null, lang), panel);
            Common.Apply(f, o, ctx);
            var style = o.Str("style", "line");
            var smooth = o.Flag("smooth", false);
            var lines = style != "points";
            var markers = style is "points" or "linepoints";
            for (var k = 0; k < series.Count; k++)
            {
                var look = SeriesLook.Of(k);
                f.Legend.Add(new LegendEntry(Common.SeriesName(series[k].Name, k, lang), lines ? look.Line(0.5) : look.Line(0.5) with { Stroke = "none" },
                    markers ? look.Marker : null, look.FilledMarker));
            }
            f.Layout(lang);
            var c = new Chart(f, lang);
            Common.DrawLimits(c, o);
            for (var k = 0; k < series.Count; k++)
            {
                var look = SeriesLook.Of(k);
                var data = series[k].Points;
                // Points hidden by an axis break are left out (no marker, no port).
                var shown = data.Select(p => f.IsVisible(panel, p.X, p.Y)).ToList();
                var pts = data.Select(p => f.P(panel, p.X, p.Y)).ToList();
                if (lines)
                {
                    var tag = ChartTags.Nth(ChartTags.Series, k);
                    var segs = new List<PathSeg>();
                    foreach (var run in f.VisibleRuns(panel, style == "step" ? Steps(data) : data))
                    {
                        var path = run.Select(p => f.P(panel, p.X, p.Y)).ToList();
                        if (Draw.Series(path, panel.Area, look.Line(0.5), smooth && style != "step", tag) is { } line) segs.AddRange(line.Segments);
                        c.Avoid.Add(path);
                    }
                    if (segs.Count > 0) c.Lines.Add(new PathPrim(segs, look.Line(0.5)) { Tag = tag });
                }
                else c.Avoid.Add(pts.Where((_, i) => shown[i]).ToList());
                if (markers)
                {
                    var visible = pts.Where((p, i) => shown[i] && Frame.Ok(p) && panel.Area.Inflate(0.01).Contains(p)).ToList();
                    if (visible.Count > 0) c.Lines.Add(Draw.Markers(visible, look.Marker, look.FilledMarker, look.Color, f.Fs * 0.3) with { Tag = ChartTags.Nth(ChartTags.Marker, k) });
                }
                for (var i = 0; i < pts.Count; i++) if (shown[i]) c.Port(pts[i], "s" + Fmt.I(k + 1) + "." + Fmt.I(i + 1));
            }
            return c;
        }

        /// <summary>Axis break actions on the parts "axis.y"/"axisbreak.y" and "axis.x"/"axisbreak.x" (see <see cref="ChartType"/>).</summary>
        private static IEnumerable<ContextAction> BreakActions(SketchObject o)
        {
            var (x, y, series, limits) = PlainAxes(o);
            return ChartBreaks.Actions(o, x, series.SelectMany(s => s.Points.Select(p => p.X)), y, series.SelectMany(s => s.Points.Select(p => p.Y)).Concat(limits));
        }

        /// <summary>Step curve: each value is held until the next x.</summary>
        public static List<Vec2> Steps(IReadOnlyList<Vec2> p)
        {
            var list = new List<Vec2>();
            for (var i = 0; i < p.Count; i++)
            {
                if (i > 0) list.Add(new Vec2(p[i].X, p[i - 1].Y));
                list.Add(p[i]);
            }
            return list;
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang) => new[]
        {
            Common.DataInput(o, DefaultData),
            Prm.Cycle(o, "chart.style", StyleDef, new I18nText("Style", "Darstellung")),
            Prm.Toggle(o, "chart.smooth", "smooth", false, new I18nText("Smooth curve", "Geglättete Kurve")),
            Prm.Cycle(o, "chart.xscale", Common.XScale, new I18nText("x scale", "x-Skala")),
            Prm.Cycle(o, "chart.yscale", Common.YScale, new I18nText("y scale", "y-Skala")),
            Common.GridToggle(o),
            Common.LegendCycle(o),
        }.Concat(BreakActions(o));
    }

    /// <summary>
    /// Bar chart: vertical columns or horizontal bars per category, several series grouped or stacked, optional value
    /// labels. Data rows "label;value[;value…]"; areas use solid colours, or fill patterns in monochrome output.
    /// </summary>
    internal sealed class BarChart : ChartType
    {
        public const string TypeId = "chart.bar";

        public const string DefaultData = "A;4\nB;6.5\nC;3\nD;5";

        public static readonly ParamDef DataDef = Prm.Text("data", "Data (rows label;value[;value…])", "Daten (Zeilen Bezeichnung;Wert[;Wert…])", DefaultData);
        public static readonly ParamDef OrientDef = Prm.Choice("orient", "Orientation", "Ausrichtung", "column",
            ("column", "Columns (vertical)", "Säulen (senkrecht)"), ("bar", "Bars (horizontal)", "Balken (waagerecht)"));
        public static readonly ParamDef ModeDef = Prm.Choice("mode", "Several series", "Mehrere Reihen", "grouped",
            ("grouped", "Grouped", "Gruppiert"), ("stacked", "Stacked", "Gestapelt"));
        public static readonly ParamDef ValuesDef = Prm.Bool("values", "Value labels", "Wertbeschriftung", false);

        public BarChart() : base(TypeId, new I18nText("Bar chart", "Balkendiagramm"), ChartCategories.Data,
            "chart", "bar", "column", "histogram", "grouped", "stacked", "categories", "excel",
            "diagramm", "balken", "säulen", "säulendiagramm", "balkendiagramm", "gestapelt", "gruppiert", "kategorien")
        { }

        protected override IReadOnlyList<ParamDef> BuildParams() => new[]
        {
            DataDef, OrientDef, ModeDef, ValuesDef, Common.Title, Common.XLabel, Common.YLabel, Common.YUnit,
            Common.Grid, Common.Legend, Common.YRange, Common.Patterns, Common.Box,
        };

        internal override Chart Compose(SketchObject o, RenderContext ctx)
        {
            var lang = ctx.Lang;
            var set = ChartData.Categories(ChartData.Parse(o.Str("data", DefaultData)), lang);
            var horizontal = o.Str("orient", "column") == "bar";
            var stacked = o.Str("mode", "grouped") == "stacked" && set.Series.Count > 1;
            var n = set.Categories.Count;
            var m = set.Series.Count;
            var values = new List<double>();
            if (stacked)
                for (var i = 0; i < n; i++)
                {
                    values.Add(set.Series.Sum(s => Fmt.Finite(s.Values[i]) && s.Values[i] > 0 ? s.Values[i] : 0));
                    values.Add(set.Series.Sum(s => Fmt.Finite(s.Values[i]) && s.Values[i] < 0 ? s.Values[i] : 0));
                }
            else values.AddRange(set.Series.SelectMany(s => s.Values));
            // Value labels need headroom above the longest bar.
            if (o.Flag("values", false)) values.AddRange(values.Where(Fmt.Finite).Select(v => v * 1.12).ToList());
            var valueAxis = Axis.Linear(values, ChartData.Range(o.Str("yrange", "")), includeZero: true);
            var catAxis = Axis.Category(set.Categories, reversed: horizontal);
            var catTitle = Common.AxisTitle(o, "xlabel", "xunit-none", "", null, lang);
            var valTitle = Common.AxisTitle(o, "ylabel", "yunit", "", null, lang);
            var panel = horizontal ? new Panel(catAxis, catTitle) : new Panel(valueAxis, valTitle);
            var f = new Frame(o.Size, Design, horizontal ? valueAxis : catAxis, horizontal ? valTitle : catTitle, panel);
            Common.Apply(f, o, ctx);
            var mono = f.Mono || o.Flag("patterns", false);
            for (var k = 0; k < m; k++)
                f.Legend.Add(new LegendEntry(Common.SeriesName(set.Series[k].Name, k, lang), SeriesLook.Of(k).Area(mono, 0.25), Area: true));
            if (m < 2 && f.LegendMode == "auto") f.LegendMode = "none";
            f.Layout(lang);
            var c = new Chart(f, lang);
            var showValues = o.Flag("values", false);
            var group = stacked || m == 1 ? 0.62 : Math.Min(0.8, 0.5 + 0.08 * m);
            var dec = Math.Max(0, Math.Min(3, valueAxis.Decimals + 1));
            for (var i = 0; i < n; i++)
            {
                double posBase = 0, negBase = 0;
                for (var k = 0; k < m; k++)
                {
                    var v = set.Series[k].Values[i];
                    if (!Fmt.Finite(v)) continue;
                    double from, to, c0, c1;
                    if (stacked)
                    {
                        if (v >= 0) { from = posBase; to = posBase + v; posBase = to; }
                        else { from = negBase; to = negBase + v; negBase = to; }
                        c0 = i + (1 - group) / 2; c1 = c0 + group;
                    }
                    else
                    {
                        from = 0; to = v;
                        var w = group / m;
                        c0 = i + (1 - group) / 2 + k * w; c1 = c0 + w;
                    }
                    var a = Clamp(valueAxis, from); var b = Clamp(valueAxis, to);
                    Vec2 p0 = horizontal ? f.P(panel, a, c0) : f.P(panel, c0, a);
                    Vec2 p1 = horizontal ? f.P(panel, b, c1) : f.P(panel, c1, b);
                    var rx = Math.Min(p0.X, p1.X); var ry = Math.Min(p0.Y, p1.Y);
                    var rw = Math.Abs(p1.X - p0.X); var rh = Math.Abs(p1.Y - p0.Y);
                    if (rw > 1e-6 && rh > 1e-6) c.Areas.Add(Prim.Rect(rx, ry, rw, rh, SeriesLook.Of(k).Area(mono, 0.25)) with { Tag = ChartTags.Nth(ChartTags.Bar, k) });
                    var mid = (c0 + c1) / 2;
                    var end = horizontal ? f.P(panel, b, mid) : f.P(panel, mid, b);
                    c.Avoid.Add(new[] { horizontal ? f.P(panel, a, mid) : f.P(panel, mid, a), end });
                    c.Port(end, "s" + Fmt.I(k + 1) + "." + Fmt.I(i + 1));
                    if (showValues)
                    {
                        var fs = f.Fs * 0.8;
                        var text = Fmt.Num(v, lang, dec);
                        var up = to >= from;
                        var at = horizontal
                            ? end + new Vec2((up ? 1 : -1) * fs * 0.3, 0)
                            : end + new Vec2(0, (up ? -1 : 1) * fs * 0.75);
                        // Labels that would leave the plot go inside the bar end (stacked bars: always centred in the segment).
                        var tw = Fmt.Width(text, fs);
                        var outside = horizontal
                            ? (up ? at.X + tw > panel.Area.Right : at.X - tw < panel.Area.X)
                            : (up ? at.Y - fs * 0.6 < panel.Area.Y : at.Y + fs * 0.6 > panel.Area.Bottom);
                        var inside = stacked || outside;
                        if (stacked) at = horizontal ? f.P(panel, (a + b) / 2, mid) : f.P(panel, mid, (a + b) / 2);
                        else if (outside)
                            at = horizontal ? end + new Vec2((up ? -1 : 1) * (tw / 2 + fs * 0.3), 0) : end + new Vec2(0, (up ? 1 : -1) * fs * 0.75);
                        var anchor = horizontal && !inside ? (up ? TextAnchor.Start : TextAnchor.End) : TextAnchor.Middle;
                        Draw.Label(c.Over, at, text, fs, ChartTags.Nth(ChartTags.ValueLabel, k), anchor, backing: inside);
                    }
                }
            }
            return c;
        }

        private static double Clamp(Axis a, double v) => Geo.Clamp(v, a.Min, a.Max);

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang) => new[]
        {
            Common.DataInput(o, DefaultData) with { Input = Common.DataInput(o, DefaultData).Input! with
            {
                Label = new I18nText("Data rows “label;value” separated by | (new series: ||)", "Datenzeilen „Bezeichnung;Wert“ getrennt durch | (neue Reihe: ||)"),
            } },
            Prm.Cycle(o, "chart.orient", OrientDef, new I18nText("Orientation", "Ausrichtung")),
            Prm.Cycle(o, "chart.mode", ModeDef, new I18nText("Several series", "Mehrere Reihen")),
            Prm.Toggle(o, "chart.values", "values", false, new I18nText("Value labels", "Wertbeschriftung")),
            Common.GridToggle(o),
            Common.LegendCycle(o),
        };
    }

    /// <summary>Axes with ticks, labels, units and optional grid and arrowheads, without data (to draw on or annotate).</summary>
    internal sealed class AxesChart : ChartType
    {
        public const string TypeId = "chart.axes";

        public static readonly ParamDef XRangeDef = Common.XRange with { Default = "0;10", Advanced = false };
        public static readonly ParamDef YRangeDef = Common.YRange with { Default = "0;10", Advanced = false };
        public static readonly ParamDef ArrowsDef = Prm.Bool("arrows", "Arrowheads", "Pfeilspitzen", true);

        public AxesChart() : base(TypeId, new I18nText("Axes and grid", "Achsen und Gitter"), ChartCategories.Data,
            "axes", "axis", "grid", "coordinate system", "graph paper", "ticks", "log",
            "achsen", "achse", "gitter", "koordinatensystem", "netz", "skala", "logarithmisch")
        { }

        protected override IReadOnlyList<ParamDef> BuildParams() => new[]
        {
            XRangeDef, YRangeDef, Common.XLabel, Common.XUnit, Common.YLabel, Common.YUnit, Common.XScale, Common.YScale,
            Common.Grid, ArrowsDef, Common.Title, Common.Box,
        };

        internal override Chart Compose(SketchObject o, RenderContext ctx)
        {
            var lang = ctx.Lang;
            var xr = ChartData.Range(o.Str("xrange", "0;10")) ?? (0, 10);
            var yr = ChartData.Range(o.Str("yrange", "0;10")) ?? (0, 10);
            var xlog = o.Str("xscale", "linear") == "log";
            var ylog = o.Str("yscale", "linear") == "log";
            var x = Axis.Of(xlog, new[] { xr.Item1, xr.Item2 }, xr);
            var y = Axis.Of(ylog, new[] { yr.Item1, yr.Item2 }, yr);
            var panel = new Panel(y, Common.AxisTitle(o, "ylabel", "yunit", "y", null, lang));
            var f = new Frame(o.Size, Design, x, Common.AxisTitle(o, "xlabel", "xunit", "x", null, lang), panel);
            Common.Apply(f, o, ctx);
            f.Arrows = o.Flag("arrows", true) && !f.Box;
            f.Layout(lang);
            return new Chart(f, lang);
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang) => new[]
        {
            Common.GridToggle(o),
            Prm.Toggle(o, "chart.arrows", "arrows", true, new I18nText("Arrowheads", "Pfeilspitzen")),
            Prm.Cycle(o, "chart.xscale", Common.XScale, new I18nText("x scale", "x-Skala")),
            Prm.Cycle(o, "chart.yscale", Common.YScale, new I18nText("y scale", "y-Skala")),
        };
    }
}
