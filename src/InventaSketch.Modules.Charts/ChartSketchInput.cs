using System;
using System.Collections.Generic;
using System.Globalization;
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
    /// and added as a series (XY chart: a new data block; figure: a new series in the specification).
    /// </summary>
    public static class ChartSketchInput
    {
        /// <summary>Most points a sketched curve keeps.</summary>
        public const int MaxPoints = 120;

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
            double Bx(double v) => xa.IsLog ? Math.Pow(10, v) : v;
            double By(double v) => ya.IsLog ? Math.Pow(10, v) : v;
            var data = local
                .Select(p => new Vec2(Fx(xa.V((p.X - a.X) / Math.Max(a.W, 1e-9))), Fy(ya.V((a.Bottom - p.Y) / Math.Max(a.H, 1e-9)))))
                .Where(p => Fmt.Finite(p.X) && Fmt.Finite(p.Y)).ToList();
            if (data.Count < 3) return null;
            double x0 = Fx(xa.Min), x1 = Fx(xa.Max), y0 = Fy(ya.Min), y1 = Fy(ya.Max);
            var frame = new Rect(Math.Min(x0, x1), Math.Min(y0, y1), Math.Abs(x1 - x0), Math.Abs(y1 - y0));
            var fit = SketchFit.Fit(data, new SketchFitOptions { Frame = frame });
            var curve = Thin(fit.Curve, MaxPoints).Select(p => new Vec2(Bx(p.X), By(p.Y))).ToList();
            if (curve.Count < 2) return null;
            var lang = ctx.Lang;
            var name = fit.Kind is CurveKind k ? CurveFunctions.Info(k).Name : new I18nText("Sketched curve", "Skizzierte Kurve");
            var message = fit.Kind is CurveKind kind
                ? new I18nText("Recognised: " + CurveFunctions.Info(kind).Name.En + " (" + fit.Spec!.Format() + ")", "Erkannt: " + CurveFunctions.Info(kind).Name.De + " (" + fit.Spec!.Format() + ")")
                : new I18nText("No standard shape recognised: added as a smoothed curve.", "Keine Standardform erkannt: als geglättete Kurve hinzugefügt.");

            if (chart.Type == XyChart.TypeId)
            {
                var text = chart.Str("data", XyChart.DefaultData).TrimEnd();
                var rows = string.Join("\n", curve.Select(p => Num(p.X) + ";" + Num(p.Y)));
                return new SketchedCurve(chart.WithParam("data", text + "\n\n" + rows), message, fit.Kind);
            }
            var spec = FigureSpec.Parse(chart.Str("spec", ""), lang);
            spec.Series.Add(new FigSeries
            {
                X = curve.Select(p => p.X).ToList(),
                Y = curve.Select(p => p.Y).ToList(),
                Panel = panelIndex,
                Smooth = fit.Kind is null,
                Label = name,
            });
            return new SketchedCurve(chart.WithParam("spec", spec.ToJson()), message, fit.Kind);
        }

        private static string Num(double v) => Math.Round(v, 4).ToString("0.####", CultureInfo.InvariantCulture);

        /// <summary>At most <paramref name="max"/> points, evenly picked, first and last kept.</summary>
        private static IReadOnlyList<Vec2> Thin(IReadOnlyList<Vec2> pts, int max)
        {
            if (pts.Count <= max) return pts;
            var list = new List<Vec2>(max);
            for (var i = 0; i < max; i++) list.Add(pts[(int)Math.Round(i * (pts.Count - 1) / (double)(max - 1))]);
            return list;
        }
    }
}
