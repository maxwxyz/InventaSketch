using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Charts
{
    internal static class ParamEdits
    {
        /// <summary>The object without parameter <paramref name="key"/> (unchanged when it has none).</summary>
        public static SketchObject WithoutParam(this SketchObject o, string key)
        {
            if (!o.Params.ContainsKey(key)) return o;
            return o with { Params = o.Params.Where(kv => kv.Key != key).ToDictionary(kv => kv.Key, kv => kv.Value) };
        }
    }

    /// <summary>Reads and changes the curves of a chart type, point by point (data points, whole curves).</summary>
    internal interface ISeriesEditor
    {
        /// <summary>The curves of <paramref name="o"/> with their names in <paramref name="lang"/>.</summary>
        IReadOnlyList<AreaSeries> Series(SketchObject o, Lang lang);

        /// <summary>
        /// The object with the points of curve <paramref name="k"/> changed (NaN points are gaps); a change returning null
        /// deletes the curve (areas, markers and part looks of later curves follow). A sketched curve becomes plain data.
        /// </summary>
        SketchObject EditSeries(SketchObject o, Lang lang, int k, Func<List<Vec2>, List<Vec2>?> change);
    }

    /// <summary>
    /// The XY chart's curves and areas: curves from the data text (<see cref="ChartData"/>), areas from the parameter
    /// <c>areas</c> (JSON array of area objects, <see cref="FigureSpec.ParseAreas"/>), limit lines as the lines an area can
    /// reach. Changing points rewrites the data as one block per curve ("Name;" header, "x;y" rows), keeping the sketch
    /// memos of the curves that were not changed.
    /// </summary>
    internal sealed class XyEditor : IAreaEditor, ISeriesEditor
    {
        public static readonly XyEditor Instance = new();

        public string Tag => ChartAreas.Tag;

        public static List<FigRegion> Areas(SketchObject o) => FigureSpec.ParseAreas(o.Str("areas", ""));

        public IReadOnlyList<AreaSeries> Series(SketchObject o, Lang lang)
        {
            var series = ChartData.Xy(ChartData.Parse(o.Str("data", XyChart.DefaultData)));
            var step = o.Str("style", "line") == "step";
            var smooth = o.Flag("smooth", false) && !step;
            return series.Select((s, k) => new AreaSeries(s.Points.Select(p => p.X).ToList(), s.Points.Select(p => p.Y).ToList(), 0, step, smooth,
                Common.SeriesName(s.Name, k, lang), SeriesLook.Of(k).Color)).ToList();
        }

        public AreaModel? Model(SketchObject o, Lang lang)
        {
            var (x, y, _, limits) = XyChart.PlainAxes(o);
            return new AreaModel
            {
                Areas = Areas(o),
                Series = Series(o, lang),
                Lines = limits.Select(v => new AreaLine(new I18nText("Limit line " + Fmt.Num(v, Lang.En, 6), "Grenzlinie " + Fmt.Num(v, Lang.De, 6)), v, 0)).ToList(),
                X = x,
                Y = new[] { y },
            };
        }

        public SketchObject Edit(SketchObject o, Lang lang, Action<List<FigRegion>> change)
        {
            var areas = Areas(o);
            change(areas);
            var json = FigureSpec.AreasJson(areas);
            return json.Length == 0 ? o.WithoutParam("areas") : o.WithParam("areas", json);
        }

        public SketchObject EditSeries(SketchObject o, Lang lang, int k, Func<List<Vec2>, List<Vec2>?> change)
        {
            var text = o.Str("data", XyChart.DefaultData);
            var series = ChartData.Xy(ChartData.Parse(text));
            if (k < 0 || k >= series.Count) return o;
            // The sketch memo of every curve, so unchanged sketched curves keep their shape choice.
            var memos = Enumerable.Range(0, series.Count).Select(i => ChartSketchInput.XyBlock(text, i)?.Memo).ToList();
            var next = change(series[k].Points.ToList());
            var blocks = new List<string>();
            for (var i = 0; i < series.Count; i++)
            {
                if (i == k && next is null) continue;
                var pts = i == k ? next!.Where(p => Fmt.Finite(p.X) && Fmt.Finite(p.Y)).ToList() : series[i].Points.ToList();
                if (pts.Count == 0) continue;
                var lines = new List<string>();
                if (!string.IsNullOrWhiteSpace(series[i].Name)) lines.Add(Header(series[i].Name!));
                if (i != k && memos[i] is { } memo) lines.Add(memo);
                lines.AddRange(pts.Select(p => N(p.X) + ";" + N(p.Y)));
                blocks.Add(string.Join("\n", lines));
            }
            var result = o.WithParam("data", string.Join("\n\n", blocks));
            if (next is null || next.Count(p => Fmt.Finite(p.X) && Fmt.Finite(p.Y)) == 0)
            {
                // The curve is gone: its areas go, later areas and part looks move down.
                var areas = Areas(result);
                ChartAreas.SeriesRemoved(areas, k);
                var json = FigureSpec.AreasJson(areas);
                result = json.Length == 0 ? result.WithoutParam("areas") : result.WithParam("areas", json);
                result = ChartPoints.RenumberLooks(result, k);
            }
            return result;
        }

        /// <summary>A header line that names the next curve (a trailing ";" keeps names with spaces or digits one text field).</summary>
        private static string Header(string name) => name.Replace(";", ",").Replace("\n", " ").Trim() + ";";

        private static string N(double v) => (Math.Abs(v) < 1e-300 ? 0 : v).ToString("R", CultureInfo.InvariantCulture);
    }

    /// <summary>The figure's curves (<see cref="FigSeries"/>) and areas (<see cref="FigRegion"/>, part kind "region").</summary>
    internal sealed class FigureEditor : IAreaEditor, ISeriesEditor
    {
        public static readonly FigureEditor Instance = new();

        public string Tag => ChartTags.Region;

        private static FigureSpec? Spec(SketchObject o) => FigureSpec.TryParse(FigureChart.SpecText(o), out var s, out _) ? s : null;

        public IReadOnlyList<AreaSeries> Series(SketchObject o, Lang lang)
            => Spec(o) is { } s ? Series(s, lang) : Array.Empty<AreaSeries>();

        public static IReadOnlyList<AreaSeries> Series(FigureSpec s, Lang lang)
        {
            var n = FigureComposer.PanelsOf(s).Count;
            return s.Series.Select((se, k) => new AreaSeries(se.X, se.Y, Math.Max(0, Math.Min(n - 1, se.Panel)), se.Step, se.Smooth,
                FigureComposer.SeriesName(s, k, lang), se.Color ?? SeriesLook.Of(k).Color)).ToList();
        }

        public AreaModel? Model(SketchObject o, Lang lang)
        {
            if (Spec(o) is not { } s) return null;
            var n = FigureComposer.PanelsOf(s).Count;
            var lines = new List<AreaLine>();
            for (var k = 0; k < s.Refs.Count; k++)
            {
                var r = s.Refs[k];
                if (r.Kind != "h" || !Fmt.Finite(r.Y)) continue;
                var label = r.Label is { } l
                    ? new I18nText(l.En + " (" + Fmt.Num(r.Y, Lang.En, 6) + ")", l.Get(Lang.De) + " (" + Fmt.Num(r.Y, Lang.De, 6) + ")")
                    : new I18nText("Value line " + Fmt.Num(r.Y, Lang.En, 6), "Wertlinie " + Fmt.Num(r.Y, Lang.De, 6));
                var panels = r.Panel is { } p && p < 0 ? Enumerable.Range(0, n) : new[] { Math.Max(0, Math.Min(n - 1, r.Panel ?? 0)) };
                foreach (var pi in panels) lines.Add(new AreaLine(label, r.Y, pi));
            }
            return new AreaModel
            {
                Areas = s.Regions,
                Series = Series(s, lang),
                Lines = lines,
                X = FigureComposer.XAxis(s),
                Y = Enumerable.Range(0, n).Select(i => FigureComposer.YAxis(s, i)).ToList(),
            };
        }

        public SketchObject Edit(SketchObject o, Lang lang, Action<List<FigRegion>> change)
        {
            var spec = FigureSpec.Parse(FigureChart.SpecText(o), lang);
            change(spec.Regions);
            return o.WithParam("spec", spec.ToJson());
        }

        public SketchObject EditSeries(SketchObject o, Lang lang, int k, Func<List<Vec2>, List<Vec2>?> change)
        {
            var spec = FigureSpec.Parse(FigureChart.SpecText(o), lang);
            if (k < 0 || k >= spec.Series.Count) return o;
            var se = spec.Series[k];
            var pts = Enumerable.Range(0, Math.Min(se.X.Count, se.Y.Count)).Select(i => new Vec2(se.X[i], se.Y[i])).ToList();
            var next = change(pts);
            if (next is null || next.Count(p => Fmt.Finite(p.X) && Fmt.Finite(p.Y)) == 0)
            {
                spec.Series.RemoveAt(k);
                ChartAreas.SeriesRemoved(spec.Regions, k);
                for (var i = spec.Refs.Count - 1; i >= 0; i--)
                {
                    var r = spec.Refs[i];
                    if (r.Kind != "point") continue;
                    if (r.Series == k) spec.Refs.RemoveAt(i);
                    else if (r.Series > k) r.Series--;
                }
                return ChartPoints.RenumberLooks(o.WithParam("spec", spec.ToJson()), k);
            }
            se.X = next.Select(p => p.X).ToList();
            se.Y = next.Select(p => p.Y).ToList();
            // Moved by hand: no longer the sketched shape (its shape choice would undo the edit).
            se.Sketch = null;
            return o.WithParam("spec", spec.ToJson());
        }
    }
}
