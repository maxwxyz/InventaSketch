using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Charts
{
    /// <summary>
    /// Polar chart: series of (angle in degrees, radius) as lines, lines with markers, filled areas (radar charts) or markers
    /// around a pole, with an angular axis in degrees (labels outside the circle) and a radial grid (rings at the radial
    /// ticks, spokes every 30° or 45°). The data are the XY chart's format (<see cref="ChartData.Xy"/>: rows "angle;r" or
    /// "angle;r1;r2…" with a header). 0° points right with angles counter-clockwise (mathematics, matplotlib), or up with
    /// angles clockwise (compass). Radii beyond the radial range are drawn on the rim.
    /// </summary>
    internal sealed class PolarChart : CanvasChart
    {
        public const string TypeId = "chart.polar";

        public const string DefaultData = "°;A;B\n0;8;5\n45;6;7\n90;4;8\n135;5;6\n180;7;4\n225;5;5\n270;3;7\n315;6;6";

        public static readonly ParamDef DataDef = Prm.Text("data", "Data (rows angle in °;r, blank line = new series)", "Daten (Zeilen Winkel in °;r, Leerzeile = neue Reihe)", DefaultData);
        public static readonly ParamDef StyleDef = Prm.Choice("style", "Style", "Darstellung", "line",
            ("line", "Line", "Linie"), ("linepoints", "Line with markers", "Linie mit Markern"), ("area", "Filled area", "Gefüllte Fläche"), ("points", "Markers", "Punkte"));
        public static readonly ParamDef ClosedDef = Prm.Bool("closed", "Closed curves", "Geschlossene Kurven", true);
        public static readonly ParamDef ZeroDef = Prm.Choice("zero", "0° direction", "Richtung von 0°", "right",
            ("right", "Right, counter-clockwise", "Rechts, gegen den Uhrzeigersinn"), ("top", "Up, clockwise (compass)", "Oben, im Uhrzeigersinn (Kompass)"));
        public static readonly ParamDef RRangeDef = Prm.Text("rrange", "Radial range (min;max, empty = auto)", "Radialbereich (min;max, leer = automatisch)", advanced: true);

        public PolarChart() : base(TypeId, new I18nText("Polar chart", "Polardiagramm"), ChartCategories.Data,
            "polar", "polar chart", "radar", "spider", "antenna pattern", "directional", "angle", "radial", "rose",
            "polardiagramm", "netzdiagramm", "radardiagramm", "spinnennetz", "richtdiagramm", "richtcharakteristik", "winkel", "polarkoordinaten")
        { }

        public override Size2 Design => new(80, 65);

        protected override IReadOnlyList<ParamDef> BuildParams() => new[]
        {
            DataDef, StyleDef, ClosedDef, ZeroDef, RRangeDef, Common.Title, Common.Grid, Common.Legend,
        };

        /// <summary>The radial axis of the data (0 at the pole unless a range or negative radii say otherwise).</summary>
        internal static Axis RadialAxis(SketchObject o)
        {
            var series = ChartData.Xy(ChartData.Parse(o.Str("data", DefaultData)));
            return Axis.Linear(series.SelectMany(s => s.Points.Select(p => p.Y)), ChartData.Range(o.Str("rrange", "")), includeZero: true);
        }

        /// <summary>
        /// Screen direction of the data angle <paramref name="deg"/>: 0° right and counter-clockwise, or 0° up and clockwise
        /// (<paramref name="top"/>); y runs down.
        /// </summary>
        internal static Vec2 Dir(double deg, bool top)
        {
            var a = deg * Math.PI / 180;
            return top ? new Vec2(Math.Sin(a), -Math.Cos(a)) : new Vec2(Math.Cos(a), -Math.Sin(a));
        }

        /// <summary>Angular grid step: 30° on a roomy chart, 45° or 90° when the circle is small.</summary>
        internal static double AngleStep(double radius, double fs) => radius > fs * 9 ? 30 : radius > fs * 4.5 ? 45 : 90;

        protected override void Paint(SketchObject o, RenderContext ctx, Canvas cv)
        {
            var lang = ctx.Lang;
            var series = ChartData.Xy(ChartData.Parse(o.Str("data", DefaultData)));
            var style = o.Str("style", "line");
            var top = o.Str("zero", "right") == "top";
            var closed = o.Flag("closed", true);
            var lines = style is "line" or "linepoints" or "area";
            var markers = style is "points" or "linepoints";
            var area = style == "area";
            for (var k = 0; k < series.Count; k++)
            {
                var look = SeriesLook.Of(k);
                cv.Legend.Add(area
                    ? new LegendEntry(Common.SeriesName(series[k].Name, k, lang), AreaStyle(k, cv.Mono), Area: true)
                    : new LegendEntry(Common.SeriesName(series[k].Name, k, lang), lines ? look.Line(0.5) : look.Line(0.5) with { Stroke = "none" }, markers ? look.Marker : null, look.FilledMarker));
            }
            cv.Layout();
            var box = cv.Area;
            var c = box.Center;
            var fs = cv.Fs * 0.85;
            var labelW = Fmt.Width("315°", fs);
            var r = Math.Max(Math.Min(box.W / 2 - labelW - cv.Gap * 2, box.H / 2 - fs - cv.Gap * 2), Math.Min(box.W, box.H) * 0.2);
            r = Math.Max(r, 0.5);
            var axis = RadialAxis(o);
            var step = AngleStep(r, cv.Fs);
            double Rad(double v) => Geo.Clamp(axis.T(v), 0, 1) * r;
            Vec2 P(double deg, double v) => c + Dir(deg, top) * Rad(v);

            // Grid: rings at the radial ticks, spokes at the angle steps (the 0° spoke is the radial axis).
            var ticks = axis.Ticks(lang).Where(t => t.IsMajor && t.Value > axis.Min + 1e-9 * Math.Max(1, Math.Abs(axis.Max)) && t.Value < axis.Max - 1e-9 * Math.Max(1, Math.Abs(axis.Max))).ToList();
            if (o.Flag("grid", true))
            {
                var grid = new List<IReadOnlyList<Vec2>>();
                foreach (var t in ticks) grid.Add(Ring(c, Rad(t.Value)));
                for (var a = step; a < 360 - 1e-9; a += step) grid.Add(new[] { c, c + Dir(a, top) * r });
                if (Draw.MultiPath(grid, S.Grid(cv.Mono), ChartTags.Grid) is { } g) cv.Back.Add(g);
            }
            cv.Main.Add(Prim.Polyline(Ring(c, r), S.Axis) with { Tag = ChartTags.Axis + ".a" });
            cv.Main.Add(Prim.Line(c, c + Dir(0, top) * r, S.Axis) with { Tag = ChartTags.Axis + ".r" });

            // Angle labels outside the circle.
            for (var a = 0.0; a < 360 - 1e-9; a += step)
            {
                var d = Dir(a, top);
                var text = Fmt.Num(a, lang, 0) + "°";
                var anchor = d.X > 0.3 ? TextAnchor.Start : d.X < -0.3 ? TextAnchor.End : TextAnchor.Middle;
                var at = anchor == TextAnchor.Middle ? c + d * (r + cv.Gap + fs * 0.6) : c + d * (r + cv.Gap * 1.5);
                cv.Over.Add(cv.Text(at, text, fs, anchor, ChartTags.TickLabel + ".a"));
            }
            // Radius labels along the ray half a step after 0°.
            var ray = Dir(step / 2, top);
            foreach (var t in axis.Ticks(lang).Where(t => t.IsMajor && t.Label is not null && axis.Contains(t.Value)))
            {
                var rr = Rad(t.Value);
                if (rr < fs * 0.8) continue;
                cv.Over.Add(cv.Text(c + ray * rr + new Vec2(cv.Gap * 0.5, 0), t.Label!, fs * 0.9, TextAnchor.Start, ChartTags.TickLabel + ".r", Palette.Helper));
            }

            // Series.
            for (var k = 0; k < series.Count; k++)
            {
                var look = SeriesLook.Of(k);
                var pts = series[k].Points.Select(p => P(p.X, p.Y)).ToList();
                var path = closed && pts.Count >= 3 ? pts.Concat(new[] { pts[0] }).ToList() : pts;
                var tag = ChartTags.Nth(ChartTags.Series, k);
                if (area && pts.Count >= 3) cv.Back.Add(Prim.Polygon(pts, AreaStyle(k, cv.Mono) with { Stroke = "none" }) with { Tag = tag });
                if (lines && path.Count >= 2) cv.Main.Add(Prim.Polyline(path, look.Line(0.5)) with { Tag = tag });
                if (markers && pts.Count > 0) cv.Main.Add(Draw.Markers(pts, look.Marker, look.FilledMarker, look.Color, cv.Fs * 0.3) with { Tag = ChartTags.Nth(ChartTags.Marker, k) });
                for (var i = 0; i < pts.Count; i++) cv.Port(pts[i], "s" + Fmt.I(k + 1) + "." + Fmt.I(i + 1));
            }

            cv.PlotAnchors(new Rect(c.X - r, c.Y - r, 2 * r, 2 * r));
            cv.Anchors.Add(Anchor.Point(AnchorRole.Port, c, "origin"));
            cv.Anchors.Add(Anchor.Rail(c, c + Dir(0, top) * r, "axis.r"));
        }

        private static List<Vec2> Ring(Vec2 c, double r)
        {
            var n = Math.Max(24, Math.Min(120, (int)Math.Ceiling(r * 2)));
            return Enumerable.Range(0, n + 1).Select(i => c + new Vec2(Math.Cos(2 * Math.PI * i / n), Math.Sin(2 * Math.PI * i / n)) * r).ToList();
        }

        /// <summary>Fill of a filled series: its colour, translucent; in monochrome a hatch per series.</summary>
        private static Style AreaStyle(int k, bool mono)
        {
            var look = SeriesLook.Of(k);
            if (!mono) return new Style { Stroke = look.Color, StrokeWidth = 0.3, Role = LineRole.Visible, Fill = Fill.Solid(look.Color), Opacity = 0.3 };
            var fill = (k % 4) switch
            {
                0 => Fill.Hatched(new HatchPattern(45, 1.2)),
                1 => Fill.Hatched(new HatchPattern(135, 1.2)),
                2 => Fill.Hatched(new HatchPattern(45, 1.4, Cross: true)),
                _ => Fill.Dotted(1.0),
            };
            return new Style { Stroke = Palette.Black, StrokeWidth = 0.3, Role = LineRole.Visible, Fill = fill };
        }

        protected override I18nText PartName(string key, Canvas cv) => key switch
        {
            "axis.a" => new I18nText("Angular axis", "Winkelachse"),
            "axis.r" => new I18nText("Radial axis", "Radialachse"),
            "label.a" => new I18nText("Angle labels", "Winkelbeschriftung"),
            "label.r" => new I18nText("Radius labels", "Radiusbeschriftung"),
            "grid.major" => new I18nText("Polar grid", "Polarnetz"),
            _ => base.PartName(key, cv),
        };

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            var list = new List<ContextAction>
            {
                Common.DataInput(o, DefaultData) with { Input = Common.DataInput(o, DefaultData).Input! with
                {
                    Label = new I18nText("Data rows “angle;r” separated by | (new series: ||)", "Datenzeilen „Winkel;r“ getrennt durch | (neue Reihe: ||)"),
                } },
            };
            list.AddRange(Prm.Options(o, "chart.style", StyleDef, new I18nText("Style", "Darstellung")));
            list.Add(Prm.Toggle(o, "chart.closed", "closed", true, new I18nText("Closed curves", "Geschlossene Kurven")));
            list.AddRange(Prm.Options(o, "chart.zero", ZeroDef, new I18nText("0° direction", "Richtung von 0°")));
            list.Add(ContextAction.WithInput("chart.rrange", new I18nText("Radial range…", "Radialbereich…"), IconIds.RangeY,
                new I18nText("Radial range “min;max” (empty = automatic)", "Radialbereich „min;max“ (leer = automatisch)"), o.Str("rrange", ""),
                (x, text) =>
                {
                    var t = (text ?? "").Trim();
                    if (t.Length == 0) return x.WithoutParam("rrange");
                    if (ChartData.Range(t) is null) throw new FormatException(lang == Lang.De ? "Bitte zwei verschiedene Zahlen „min;max“ eingeben (keine gültige Eingabe)" : "Please enter two different numbers “min;max” (not a valid input)");
                    return x.WithParam("rrange", t);
                }) with { Group = "param", ParamKeys = new[] { "rrange" } });
            list.Add(Common.GridToggle(o));
            list.AddRange(Common.LegendChoice(o));
            return list;
        }
    }
}
