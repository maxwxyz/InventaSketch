using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Charts
{
    /// <summary>A curve sketched into a chart: the changed chart and what was recognised.</summary>
    public sealed record SketchedCurve(SketchObject Chart, I18nText Message, CurveKind? Kind);

    /// <summary>
    /// Charts v2 stage 3, host entry point: a curve drawn by hand into an XY chart or a figure (pointer trail in page mm)
    /// becomes a new curve of that chart – the trail is mapped to the chart's data coordinates (the panel it was drawn in,
    /// log axes in log space), recognised as a step, pulse, ramp, sine, PT1 … (<see cref="SketchFit"/>) or else smoothed,
    /// and added as a series (XY chart: a new data block; figure: a new series in the specification). The cleaned trail is
    /// kept with the series (<see cref="CurveSketchMemo"/>), so the curve shape can be switched later on the curve's part
    /// ("Curve shape": smoothed, interpolated, fitted function, straight segments, as drawn).
    /// </summary>
    public static class ChartSketchInput
    {
        /// <summary>Most points a sketched curve keeps.</summary>
        public const int MaxPoints = CurveShapes.MaxPoints;

        /// <summary>True for the chart types a curve can be sketched into.</summary>
        public static bool Supports(SketchObject o) => o.Type is XyChart.TypeId or FigureChart.TypeId;

        public static SketchedCurve? Add(Registry registry, SketchObject chart, IReadOnlyList<Vec2> pageTrail, RenderContext ctx)
        {
            if (!Supports(chart) || pageTrail.Count < 3 || registry.TypeOf(chart) is not ChartType type) return null;
            ctx.For(registry);
            var c = type.Compose(chart, ctx);
            var inverse = chart.Matrix().Invert();
            var local = pageTrail.Select(p => inverse.Apply(p)).ToList();
            var panelIndex = Enumerable.Range(0, c.F.Panels.Count)
                .OrderByDescending(i => local.Count(p => c.F.Panels[i].Area.Inflate(2).Contains(p))).First();
            var panel = c.F.Panels[panelIndex];
            var a = panel.Area;
            Axis xa = c.F.X, ya = panel.Y;
            // Fit in the axes' own scale (log axes in log10), so a decade looks like a decade.
            double Fx(double v) => xa.IsLog ? Math.Log10(v) : v;
            double Fy(double v) => ya.IsLog ? Math.Log10(v) : v;
            var data = local
                .Select(p => new Vec2(Fx(xa.V((p.X - a.X) / Math.Max(a.W, 1e-9))), Fy(ya.V((a.Bottom - p.Y) / Math.Max(a.H, 1e-9)))))
                .Where(p => Fmt.Finite(p.X) && Fmt.Finite(p.Y)).ToList();
            if (data.Count < 3) return null;
            double x0 = Fx(xa.Min), x1 = Fx(xa.Max), y0 = Fy(ya.Min), y1 = Fy(ya.Max);
            var frame = new Rect(Math.Min(x0, x1), Math.Min(y0, y1), Math.Abs(x1 - x0), Math.Abs(y1 - y0));
            var fit = SketchFit.Fit(data, new SketchFitOptions { Frame = frame });
            var memo = CurveShapes.Create(data, frame, xa.IsLog, ya.IsLog, fit);
            var curve = CurveShapes.Points(memo);
            if (curve.Count < 2) return null;
            var lang = ctx.Lang;
            var name = fit.Kind is CurveKind k ? CurveFunctions.Info(k).Name : new I18nText("Sketched curve", "Skizzierte Kurve");
            var message = fit.Kind is CurveKind kind
                ? new I18nText("Recognised: " + CurveFunctions.Info(kind).Name.En + " (" + fit.Spec!.Format() + ")", "Erkannt: " + CurveFunctions.Info(kind).Name.De + " (" + fit.Spec!.Format() + ")")
                : new I18nText("No standard shape recognised: added as a smoothed curve.", "Keine Standardform erkannt: als geglättete Kurve hinzugefügt.");

            if (chart.Type == XyChart.TypeId)
            {
                var text = chart.Str("data", XyChart.DefaultData).TrimEnd();
                var block = CurveShapes.ToComment(memo) + "\n" + Rows(curve, memo);
                return new SketchedCurve(chart.WithParam("data", text + "\n\n" + block), message, fit.Kind);
            }
            var spec = FigureSpec.Parse(chart.Str("spec", ""), lang);
            spec.Series.Add(new FigSeries
            {
                X = curve.Select(p => p.X).ToList(),
                Y = curve.Select(p => p.Y).ToList(),
                Panel = panelIndex,
                Label = name,
                Sketch = memo,
            });
            return new SketchedCurve(chart.WithParam("spec", spec.ToJson()), message, fit.Kind);
        }

        /// <summary>Data rows "x;y" of a sketched curve (precision relative to the plot frame).</summary>
        private static string Rows(IReadOnlyList<Vec2> curve, CurveSketchMemo m)
            => string.Join("\n", curve.Select(p => CurveShapes.Text(p.X, m.XLog, m.Frame.W) + ";" + CurveShapes.Text(p.Y, m.YLog, m.Frame.H)));

        // ------------------------------------------------------------------ curve shape of a sketched series

        /// <summary>
        /// The part actions that switch the shape of sketched curve <paramref name="series"/> (XY chart: index of the series
        /// in the data; figure: index in the specification). Empty for curves that were not sketched.
        /// </summary>
        internal static IEnumerable<ContextAction> ShapeActions(SketchObject o, int series)
        {
            if (Memo(o, series) is not { } memo) return Array.Empty<ContextAction>();
            return CurveShapes.Actions(memo, change => x => WithShape(x, series, change)).ToList();
        }

        /// <summary>The memo of sketched curve <paramref name="series"/>, or null.</summary>
        internal static CurveSketchMemo? Memo(SketchObject o, int series)
        {
            if (series < 0) return null;
            if (o.Type == XyChart.TypeId)
                return XyBlock(o.Str("data", XyChart.DefaultData), series) is { Memo: { } line } && CurveShapes.TryParseComment(line, out var m) ? m : null;
            if (o.Type == FigureChart.TypeId && FigureSpec.TryParse(FigureChart.SpecText(o), out var spec, out _) && spec is not null && series < spec.Series.Count)
                return spec.Series[series].Sketch;
            return null;
        }

        /// <summary>The chart with the memo of sketched curve <paramref name="series"/> changed and its points recomputed.</summary>
        internal static SketchObject WithShape(SketchObject o, int series, Func<CurveSketchMemo, CurveSketchMemo> change)
        {
            if (o.Type == XyChart.TypeId)
            {
                var text = o.Str("data", XyChart.DefaultData);
                if (XyBlock(text, series) is not { Memo: { } line } block || !CurveShapes.TryParseComment(line, out var memo) || memo is null) return o;
                var next = change(memo);
                var pts = CurveShapes.Points(next);
                if (pts.Count < 2) return o;
                var lines = block.Lines;
                var rows = new HashSet<int>(block.Rows);
                var first = block.Rows.Min();
                var output = new List<string>();
                for (var i = 0; i < lines.Length; i++)
                {
                    if (i == block.MemoLine) output.Add(CurveShapes.ToComment(next));
                    else if (i == first) output.Add(Rows(pts, next));
                    else if (!rows.Contains(i)) output.Add(lines[i]);
                }
                return o.WithParam("data", string.Join("\n", output));
            }
            if (o.Type == FigureChart.TypeId && FigureSpec.TryParse(FigureChart.SpecText(o), out var spec, out _) && spec is not null
                && series >= 0 && series < spec.Series.Count && spec.Series[series].Sketch is { } fm)
            {
                var next = change(fm);
                var pts = CurveShapes.Points(next);
                if (pts.Count < 2) return o;
                var se = spec.Series[series];
                se.Sketch = next;
                se.X = pts.Select(p => p.X).ToList();
                se.Y = pts.Select(p => p.Y).ToList();
                se.Smooth = false;
                se.Step = false;
                return o.WithParam("spec", spec.ToJson());
            }
            return o;
        }

        /// <summary>Lines of an XY data block: all lines of the text, the row lines of the block and its memo line.</summary>
        internal sealed record DataBlockLines(string[] Lines, IReadOnlyList<int> Rows, int MemoLine, string? Memo);

        /// <summary>
        /// The data block that holds series <paramref name="series"/> of XY chart data, if that block holds exactly this one
        /// series; with its memo comment (the last <c># sketch</c> line before its first row). Mirrors <see cref="ChartData.Parse"/>.
        /// </summary>
        internal static DataBlockLines? XyBlock(string text, int series)
        {
            var blocks = ChartData.Parse(text);
            var lines = (text ?? "").Replace("\r\n", "\n").Replace('\r', '\n').Replace('|', '\n').Split('\n');
            var scan = Scan(lines);
            if (scan.Count != blocks.Count) return null;
            var k = 0;
            for (var b = 0; b < blocks.Count; b++)
            {
                var count = ChartData.Xy(new[] { blocks[b] }).Count;
                if (series < k + count)
                {
                    if (count != 1 || scan[b].Memo is not int ml) return null;
                    return new DataBlockLines(lines, scan[b].Rows, ml, lines[ml]);
                }
                k += count;
            }
            return null;
        }

        private sealed class ScanBlock
        {
            public List<int> Rows { get; } = new();
            public int? Memo { get; set; }
        }

        /// <summary>Row lines per block, exactly as <see cref="ChartData.Parse"/> splits them, plus each block's memo line.</summary>
        private static List<ScanBlock> Scan(string[] lines)
        {
            var blocks = new List<ScanBlock>();
            ScanBlock? cur = null;
            int? pending = null;
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (line.Length == 0)
                {
                    if (cur is not null && cur.Rows.Count > 0) cur = null;
                    continue;
                }
                if (line.StartsWith("#", StringComparison.Ordinal) || line.StartsWith("//", StringComparison.Ordinal))
                {
                    // A memo belongs to the rows that follow it (a memo between rows of a block is stale and ignored).
                    if (CurveShapes.IsComment(line)) pending = i;
                    continue;
                }
                var fields = ChartData.SplitFields(line);
                if (fields.Count == 0) continue;
                var header = fields.All(f => !ChartData.TryNumber(f, out _));
                if (header)
                {
                    if (cur is null || cur.Rows.Count > 0) { cur = new ScanBlock(); blocks.Add(cur); }
                    continue;
                }
                if (cur is null) { cur = new ScanBlock(); blocks.Add(cur); }
                if (cur.Rows.Count == 0) { cur.Memo = pending; pending = null; }
                else pending = null;
                cur.Rows.Add(i);
            }
            return blocks.Where(b => b.Rows.Count > 0).ToList();
        }
    }
}
