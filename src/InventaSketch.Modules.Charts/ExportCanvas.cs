using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Charts
{
    /// <summary>
    /// Export models of the charts without Cartesian axes: pie/donut (matplotlib <c>pie</c>, TikZ arcs), polar charts
    /// (matplotlib polar axes, pgfplots <c>polaraxis</c>) and Sankey diagrams (node bars and ribbons as laid out, in matplotlib
    /// patches and TikZ paths). The data come from the chart model, the layout (radius, label positions, legend, ribbons)
    /// from the drawn canvas; part looks apply as in <see cref="ExportBuilder"/>.
    /// </summary>
    internal sealed class CanvasExport
    {
        private readonly SketchObject _o;
        private readonly Canvas _cv;
        private readonly XFigure _x;

        private CanvasExport(SketchObject o, Canvas cv, XFigure x) { _o = o; _cv = cv; _x = x; }

        public static XFigure Build(SketchObject o, CanvasChart type, RenderContext ctx)
        {
            var lang = ctx.Lang;
            var cv = type.DrawCanvas(o, ctx);
            if (cv.Model is null && type is PieChart or SankeyChart || Prim.Flatten(cv.Over).Any(p => p.Tag == ChartTags.Error))
                throw new InvalidOperationException(lang == Lang.De ? "Die Daten des Diagramms lassen sich nicht lesen" : "The chart data cannot be read");
            var x = new XFigure { Name = type.Name.Get(lang), Id = o.Id, Type = type.Id, Lang = lang, WidthMm = cv.W, HeightMm = cv.H, FontMm = cv.Fs, Mono = cv.Mono };
            var b = new CanvasExport(o, cv, x);
            x.Title = string.IsNullOrEmpty(cv.Title) || b.Hidden(ChartTags.Title) ? null : cv.Title;
            x.Legend = cv.ShowLegend && !b.Hidden(ChartTags.Legend) ? "right" : "none";
            if (type is PieChart) b.Pie();
            else if (type is SankeyChart) b.Sankey();
            else b.Polar();
            return x;
        }

        // ------------------------------------------------------------------ part looks

        private PartLook? Look(string key) => PartLooks.LookOf(_o.Parts, key);

        private bool Hidden(string key) => Look(key)?.Hidden == true;

        private string Col(string c) => _x.Mono && c != Palette.White && c != "none" ? Palette.Black : c;

        private XStroke? Stroke(string key, string color, double width, DashStyle dash)
        {
            var look = Look(key);
            if (look?.Hidden == true) return null;
            var s = look?.Style;
            var c = s?.Stroke ?? Col(color);
            return c == "none" ? null : new XStroke(c, s?.StrokeWidth ?? width, s?.Dash ?? dash, s?.Opacity);
        }

        /// <summary>A page point as mm from <paramref name="c"/> with y up.</summary>
        private static Vec2 Rel(Vec2 p, Vec2 c) => new(Math.Round(p.X - c.X, 3), Math.Round(c.Y - p.Y, 3));

        // ------------------------------------------------------------------ pie

        private void Pie()
        {
            var d = (PieChart.Drawing)_cv.Model!;
            if (d.Slices.Count == 0)
                throw new InvalidOperationException(_x.Lang == Lang.De ? "Das Kreisdiagramm hat keine positiven Werte" : "The pie chart has no positive values");
            var mono = _cv.Mono || _o.Flag("patterns", false);
            var hole = Geo.Clamp(_o.Num("hole", 0), 0, 0.9);
            var pie = new XPie
            {
                Hole = Fmt.Finite(hole) ? hole : 0, Start = Fmt.Finite(_o.Num("start", 0)) ? _o.Num("start", 0) : 0, Clockwise = _o.Str("direction", "cw") != "ccw",
                Labels = _o.Str("labels", "percent"), LabelColor = mono ? Palette.Ink : Palette.White, RadiusMm = Math.Round(d.Radius, 3),
            };
            var centre = _o.Str("center", "").Trim();
            if (centre.Length > 0 && !Hidden(ChartTags.CenterText)) pie.Center = centre;
            foreach (var s in d.Slices)
            {
                var key = ChartTags.Nth(ChartTags.Slice, s.Index);
                var drawn = PieChart.SliceStyle(s.Index, mono);
                var look = Look(key);
                var edge = look?.Style?.Stroke ?? drawn.Stroke ?? Palette.White;
                var fill = look?.Hidden == true ? new XFillStyle("none", edge) : ExportBuilder.FillOf(look?.Style?.Fill ?? drawn.Fill, edge);
                pie.Slices.Add(new XSlice
                {
                    Key = key, Name = s.Name, Value = s.Value, Start = s.Start, End = s.End, Fill = fill,
                    Edge = look?.Hidden == true ? "none" : edge, EdgeWidth = look?.Style?.StrokeWidth ?? drawn.StrokeWidth ?? 0.3,
                });
            }
            // The drawn texts (slice labels, centre text) and leader lines, in mm around the centre.
            var prims = Prim.Flatten(_cv.Render()).ToList();
            foreach (var t in prims.OfType<TextPrim>())
            {
                if (t.Tag is not { } tag || Hidden(tag)) continue;
                var legend = ChartTags.Is(tag, ChartTags.Legend);
                if (!(ChartTags.Is(tag, ChartTags.SliceLabel) || tag == ChartTags.CenterText || tag == ChartTags.Title && _x.Title is not null || legend && _x.Legend != "none")) continue;
                var at = Rel(t.Position, d.Centre);
                var box = prims.OfType<RectPrim>().Any(r => r.Tag == tag && r.Style.Fill?.Color == Palette.White && t.Position.Y >= r.Y - 1e-6 && t.Position.Y <= r.Y + r.H + 1e-6);
                var s = Look(tag)?.Style;
                pie.Texts.Add(new XText
                {
                    Key = tag, X = at.X, Y = at.Y, Text = t.Text, HAlign = t.TextStyle.Anchor switch { TextAnchor.Start => "left", TextAnchor.End => "right", _ => "center" },
                    Size = Math.Round(t.TextStyle.Size / _cv.Fs * (s?.FontScale ?? 1), 3), Color = s?.TextColor ?? t.TextStyle.Color ?? Palette.Ink,
                    Bold = s?.Bold ?? t.TextStyle.Bold, Box = box,
                });
            }
            foreach (var p in prims.OfType<PathPrim>())
                if (p.Tag is { } tag && ChartTags.Is(tag, ChartTags.SliceLabel) && !p.IsClosed && !Hidden(tag))
                    pie.Leaders.Add((tag, p.Segments.Select(sg => Rel(sg.P, d.Centre)).ToList()));
            if (_x.Legend != "none")
                foreach (var r in prims.OfType<RectPrim>().Where(r => r.Tag is { } tag && ChartTags.Is(tag, ChartTags.Legend)))
                {
                    var corner = Rel(new Vec2(r.X, r.Y + r.H), d.Centre);
                    var edge = r.Style.Stroke is { } st && st != "none" ? new XStroke(st, r.Style.StrokeWidth ?? 0.25, DashStyle.Solid) : null;
                    pie.Rects.Add((r.Tag!, corner.X, corner.Y, Math.Round(r.W, 3), Math.Round(r.H, 3), ExportBuilder.FillOf(r.Style.Fill, r.Style.Stroke ?? Palette.Black) with { Edge = edge }));
                }
            _x.Pie = pie;
        }

        // ------------------------------------------------------------------ Sankey

        private void Sankey()
        {
            var d = (SankeyChart.Drawing)_cv.Model!;
            var m = d.Model;
            var l = d.Layout;
            if (m.Flows.Count == 0)
                throw new InvalidOperationException(_x.Lang == Lang.De ? "Das Sankey-Diagramm hat keine Flüsse" : "The Sankey diagram has no flows");
            var mono = _cv.Mono || _o.Flag("patterns", false);
            var sk = new XSankey { Values = _o.Flag("values", true) };
            var w = l.NodeWidth;
            for (var i = 0; i < m.Nodes.Count; i++)
            {
                var key = ChartTags.Nth(ChartTags.Node, i);
                var look = Look(key);
                var h = Math.Max(l.H[i], 0.05);
                var color = mono ? Palette.Black : SeriesLook.Of(i).Color;
                var edge = look?.Style?.Stroke ?? color;
                var fill = look?.Hidden == true ? null
                    : ExportBuilder.FillOf(look?.Style?.Fill ?? Fill.Solid(color), edge) with { Edge = edge == "none" ? null : new XStroke(edge, look?.Style?.StrokeWidth ?? 0.1, look?.Style?.Dash ?? DashStyle.Solid) };
                var corner = Rel(new Vec2(l.X[i], l.Y[i] + h), new Vec2(0, 0));
                sk.Nodes.Add(new XSankeyNode { Key = key, Name = m.Nodes[i], X = corner.X, Y = corner.Y, W = Math.Round(w, 3), H = Math.Round(h, 3), Fill = fill });
            }
            var ribbons = l.Ribbons.ToDictionary(r => r.Flow, r => r);
            for (var k = 0; k < m.Flows.Count; k++)
            {
                var f = m.Flows[k];
                var key = ChartTags.Nth(ChartTags.Flow, k);
                var look = Look(key);
                var s = look?.Style;
                // As drawn: translucent in the colour of the source, outlined white ribbons in monochrome output.
                var edgeColor = s?.Stroke ?? (mono ? Palette.Black : "none");
                var fill = ExportBuilder.FillOf(s?.Fill ?? Fill.Solid(mono ? Palette.White : SeriesLook.Of(f.Source).Color), edgeColor == "none" ? Palette.Black : edgeColor) with
                {
                    Edge = edgeColor == "none" ? null : new XStroke(edgeColor, s?.StrokeWidth ?? 0.15, s?.Dash ?? DashStyle.Solid),
                    Opacity = s?.Opacity ?? (mono ? null : 0.35),
                };
                var flow = new XSankeyFlow { Key = key, Source = m.Nodes[f.Source], Target = m.Nodes[f.Target], Value = f.Value, Fill = fill };
                if (look?.Hidden != true && ribbons.TryGetValue(f, out var r))
                {
                    var a = Rel(new Vec2(l.X[f.Source] + w, r.Y0), new Vec2(0, 0));
                    var b = Rel(new Vec2(l.X[f.Target], r.Y1), new Vec2(0, 0));
                    flow.Drawn = true;
                    flow.X0 = a.X; flow.Y0 = a.Y; flow.X1 = b.X; flow.Y1 = b.Y; flow.Width = Math.Round(r.T, 3);
                }
                sk.Flows.Add(flow);
            }
            var prims = Prim.Flatten(_cv.Render()).ToList();
            foreach (var t in prims.OfType<TextPrim>())
            {
                if (t.Tag is not { } tag || Hidden(tag)) continue;
                if (!(ChartTags.Is(tag, ChartTags.NodeLabel) || tag == ChartTags.Note || tag == ChartTags.Title && _x.Title is not null)) continue;
                var at = Rel(t.Position, new Vec2(0, 0));
                var box = prims.OfType<RectPrim>().Any(r => r.Tag == tag && r.Style.Fill?.Color == Palette.White);
                var st = Look(tag)?.Style;
                sk.Texts.Add(new XText
                {
                    Key = tag, X = at.X, Y = at.Y, Text = t.Text, HAlign = t.TextStyle.Anchor switch { TextAnchor.Start => "left", TextAnchor.End => "right", _ => "center" },
                    Size = Math.Round(t.TextStyle.Size / _cv.Fs * (st?.FontScale ?? 1), 3), Color = st?.TextColor ?? t.TextStyle.Color ?? Palette.Ink,
                    Bold = st?.Bold ?? t.TextStyle.Bold, Box = box,
                });
            }
            _x.Sankey = sk;
        }

        // ------------------------------------------------------------------ polar

        private void Polar()
        {
            var series = ChartData.Xy(ChartData.Parse(_o.Str("data", PolarChart.DefaultData)));
            var style = _o.Str("style", "line");
            var closed = _o.Flag("closed", true);
            var lines = style is "line" or "linepoints" or "area";
            var markers = style is "points" or "linepoints";
            var area = style == "area";
            var axis = PolarChart.RadialAxis(_o);
            var r = PolarChart.Radius(_cv);
            var polar = new XPolar
            {
                Top = _o.Str("zero", "right") == "top", RadiusMm = Math.Round(r, 3), RMin = axis.Min, RMax = axis.Max, RStep = axis.Step, AngleStep = PolarChart.AngleStep(r, _cv.Fs),
                Grid = _o.Flag("grid", true) && !Hidden(ChartTags.Grid), AngleLabels = !Hidden(ChartTags.TickLabel + ".a"), RadiusLabels = !Hidden(ChartTags.TickLabel + ".r"),
            };
            for (var k = 0; k < series.Count; k++)
            {
                var look = SeriesLook.Of(k);
                var key = ChartTags.Nth(ChartTags.Series, k);
                var mkey = ChartTags.Nth(ChartTags.Marker, k);
                var pts = series[k].Points.Where(p => Fmt.Finite(p.X) && Fmt.Finite(p.Y)).ToList();
                if (pts.Count == 0) continue;
                // Radii beyond the radial range lie on the rim (as drawn); a closed curve repeats its first point.
                var a = pts.Select(p => p.X).ToList();
                var rr = pts.Select(p => Geo.Clamp(p.Y, axis.Min, axis.Max)).ToList();
                if ((closed || area) && pts.Count >= 3) { a.Add(a[0]); rr.Add(rr[0]); }
                var s = new XPolarSeries { Key = key, MarkerKey = mkey, Angles = a, R = rr, Legend = _x.Legend != "none" ? Common.SeriesName(series[k].Name, k, _x.Lang) : null };
                if (lines) s.Line = Stroke(key, look.Color, 0.5, look.Dash);
                if (markers && !Hidden(mkey)) s.Marker = new XMarker(ExportBuilder.MarkerName(look.Marker), look.FilledMarker, Look(mkey)?.Style?.Stroke ?? Col(look.Color), _cv.Fs * 0.3);
                if (area && pts.Count >= 3 && !Hidden(key))
                {
                    var drawn = PolarChart.AreaStyle(k, _cv.Mono);
                    var fill = ExportBuilder.FillOf(Look(key)?.Style?.Fill ?? drawn.Fill, drawn.Stroke ?? Palette.Black);
                    s.Fill = drawn.Opacity is { } op && fill.Kind == "solid" ? fill with { Opacity = op } : fill;
                }
                if (s.Line is null && s.Marker is null && s.Fill is null) continue;
                polar.Series.Add(s);
            }
            _x.Polar = polar;
        }
    }
}
