using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Charts
{
    /// <summary>
    /// Data points and whole curves on the drawing (maintainer 2026-11-25: "select the values/points of every curve,
    /// highlighted in blue; click+drag on a curve moves it, values adjusted in the background; single points moved by click
    /// and drag or deleted; single curves deleted"), for XY charts and figures:
    /// <list type="bullet">
    /// <item>every data point is a part "point.&lt;curve&gt;.&lt;index&gt;" ("Point 3 of Curve 1 (2; 4.1)"), a fine part
    /// (<see cref="PartInfo.Detail"/>) with a small circle as its part shape (hit test, outline, glow; never drawn);</item>
    /// <item>a free drag handle per point (snapped to the axis resolution; on a curve with rising x the point stays between its
    /// neighbours), shown with its curve (<see cref="DragHandle.ShowWith"/>);</item>
    /// <item>a handle per curve dragged by grabbing the selected curve (<see cref="DragHandle.ByPart"/>): every point moves by
    /// the snapped offset (log axes: by a factor); the smaller direction of a mostly vertical or horizontal drag is ignored;</item>
    /// <item>part actions: delete the point, add a point after it, its value …; delete the curve.</item>
    /// </list>
    /// </summary>
    internal static class ChartPoints
    {
        public const string Kind = "point";

        /// <summary>
        /// Curves with more visible points than this (sketched and sampled curves) get no point handles: a press on them moves
        /// the whole curve; their points stay selectable on the drawing (part shapes) and editable through "Value …".
        /// </summary>
        public const int MaxPointHandles = 40;

        /// <summary>Part key of point <paramref name="i"/> of curve <paramref name="k"/> ("point.0.3").</summary>
        public static string Key(int k, int i) => Kind + "." + Fmt.I(k) + "." + Fmt.I(i);

        /// <summary>Curve and point index of a point key; null for other keys ("point" alone is the operating point of a pump chart).</summary>
        public static (int Series, int Index)? Parse(string part)
        {
            var seg = part.Split('.');
            if (seg.Length != 3 || seg[0] != Kind) return null;
            if (!int.TryParse(seg[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var k) || !int.TryParse(seg[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) || k < 0 || i < 0) return null;
            return (k, i);
        }

        /// <summary>The curve index of a curve part ("series.2", "marker.2", "curvelabel.2"), or -1.</summary>
        public static int SeriesOf(string part)
        {
            var seg = part.Split('.');
            return seg.Length == 2 && seg[0] is ChartTags.Series or ChartTags.Marker or ChartTags.CurveLabel
                && int.TryParse(seg[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var k) && k >= 0 ? k : -1;
        }

        private static string Num(double v, Lang lang) => Fmt.Num(v, lang, 4);

        /// <summary>"Point 3 of Curve 1 (2; 4.1)" / "Punkt 3 von Kurve 1 (2; 4,1)".</summary>
        public static I18nText Label(string nameEn, string nameDe, int i, Vec2 p)
            => new("Point " + Fmt.I(i + 1) + " of " + nameEn + " (" + Num(p.X, Lang.En) + "; " + Num(p.Y, Lang.En) + ")",
                "Punkt " + Fmt.I(i + 1) + " von " + nameDe + " (" + Num(p.X, Lang.De) + "; " + Num(p.Y, Lang.De) + ")");

        /// <summary>Visible data points of every curve: (curve, index, data point, drawing position).</summary>
        private static IEnumerable<(int K, int I, Vec2 Data, Vec2 At)> Visible(Chart c, IReadOnlyList<AreaSeries> series)
        {
            var f = c.F;
            var n = f.Panels.Count;
            for (var k = 0; k < series.Count; k++)
            {
                var s = series[k];
                var panel = f.Panels[Math.Max(0, Math.Min(n - 1, s.Panel))];
                for (var i = 0; i < Math.Min(s.X.Count, s.Y.Count); i++)
                {
                    double x = s.X[i], y = s.Y[i];
                    if (!Fmt.Finite(x) || !Fmt.Finite(y) || !f.IsVisible(panel, x, y)) continue;
                    var at = f.P(panel, x, y);
                    if (!Frame.Ok(at) || !panel.Area.Inflate(0.01).Contains(at)) continue;
                    yield return (k, i, new Vec2(x, y), at);
                }
            }
        }

        /// <summary>The point parts (fine parts, after the drawn ones).</summary>
        public static IEnumerable<PartInfo> Parts(Chart c, ISeriesEditor ed, SketchObject o)
        {
            var en = ed.Series(o, Lang.En);
            var de = ed.Series(o, Lang.De);
            foreach (var (k, i, data, _) in Visible(c, en))
                yield return new PartInfo(Key(k, i), Label(en[k].Name, k < de.Count ? de[k].Name : en[k].Name, i, data), Kind) { Detail = true };
        }

        /// <summary>Radius of a point's part shape and grip area (mm).</summary>
        public static double Radius(Frame f) => Math.Max(0.6, f.Fs * 0.3);

        /// <summary>A small filled circle per visible data point, tagged with its point key (part shapes, never drawn).</summary>
        public static IEnumerable<Primitive> Shapes(Chart c, IReadOnlyList<AreaSeries> series)
        {
            var r = Radius(c.F);
            var style = new Style { Stroke = "#1f5fbf", StrokeWidth = 0.2, Fill = Fill.Solid("#1f5fbf"), Role = LineRole.Thin };
            foreach (var (k, i, _, at) in Visible(c, series))
                yield return Prim.Circle(at.X, at.Y, r, style) with { Tag = Key(k, i) };
        }

        /// <summary>True if the finite x values of the points never fall (a curve over x, not a loop or a scatter cloud).</summary>
        private static bool Rising(IReadOnlyList<Vec2> pts)
        {
            var xs = pts.Where(p => Fmt.Finite(p.X) && Fmt.Finite(p.Y)).Select(p => p.X).ToList();
            for (var i = 1; i < xs.Count; i++) if (xs[i] < xs[i - 1]) return false;
            return true;
        }

        /// <summary>
        /// The point handles (free, snapped; between the neighbours' x on a rising curve) and the curve handles (dragged by
        /// grabbing the selected curve) of the chart.
        /// </summary>
        public static IEnumerable<ChartHandle> Handles(Chart c, ISeriesEditor ed, SketchObject o, Lang lang)
        {
            var f = c.F;
            var n = f.Panels.Count;
            var series = ed.Series(o, lang);
            var other = ed.Series(o, lang == Lang.De ? Lang.En : Lang.De);
            var visible = Visible(c, series).ToList();
            var dense = new HashSet<int>(visible.GroupBy(v => v.K).Where(g => g.Count() > MaxPointHandles).Select(g => g.Key));
            foreach (var (k, i, data, at) in visible)
            {
                if (dense.Contains(k)) continue;
                var panel = f.Panels[Math.Max(0, Math.Min(n - 1, series[k].Panel))];
                var sk = k; var si = i;
                var nameEn = lang == Lang.En ? series[k].Name : k < other.Count ? other[k].Name : series[k].Name;
                var nameDe = lang == Lang.De ? series[k].Name : k < other.Count ? other[k].Name : series[k].Name;
                var key = Key(k, i);
                yield return new ChartHandle(new DragHandle(key, at, DragConstraint.Free, Label(nameEn, nameDe, i, data))
                {
                    Part = key, ShowWith = ChartTags.Nth(ChartTags.Series, k),
                }, (x, local) => ed.EditSeries(x, lang, sk, pts => MovePoint(pts, si, ChartHandles.XValue(f, local), ChartHandles.YValue(panel, local))));
            }
            for (var k = 0; k < series.Count; k++)
            {
                var pts = visible.Where(v => v.K == k).ToList();
                if (pts.Count == 0) continue;
                var mid = pts[pts.Count / 2];
                var panel = f.Panels[Math.Max(0, Math.Min(n - 1, series[k].Panel))];
                var sk = k;
                var key = ChartTags.Nth(ChartTags.Series, k);
                var label = new I18nText((lang == Lang.En ? series[k].Name : k < other.Count ? other[k].Name : series[k].Name) + " – drag to move the curve",
                    (lang == Lang.De ? series[k].Name : k < other.Count ? other[k].Name : series[k].Name) + " – ziehen verschiebt die Kurve");
                yield return new ChartHandle(new DragHandle(key, mid.At, DragConstraint.Free, label) { Part = key, ByPart = true },
                    (x, local) => ed.EditSeries(x, lang, sk, p => Offset(p, f, panel, mid.Data, mid.At, local)));
            }
        }

        /// <summary>Point <paramref name="i"/> moved to (x, y); on a rising curve x stays between its neighbours.</summary>
        internal static List<Vec2> MovePoint(List<Vec2> pts, int i, double x, double y)
        {
            if (i < 0 || i >= pts.Count || !Fmt.Finite(x) || !Fmt.Finite(y)) return pts;
            if (Rising(pts))
            {
                var prev = Enumerable.Range(0, i).Reverse().Select(j => pts[j]).FirstOrDefault(p => Fmt.Finite(p.X) && Fmt.Finite(p.Y));
                var next = Enumerable.Range(i + 1, pts.Count - i - 1).Select(j => pts[j]).FirstOrDefault(p => Fmt.Finite(p.X) && Fmt.Finite(p.Y));
                var lo = i > 0 && Enumerable.Range(0, i).Any(j => Fmt.Finite(pts[j].X) && Fmt.Finite(pts[j].Y)) ? prev.X : double.NegativeInfinity;
                var hi = i + 1 < pts.Count && Enumerable.Range(i + 1, pts.Count - i - 1).Any(j => Fmt.Finite(pts[j].X) && Fmt.Finite(pts[j].Y)) ? next.X : double.PositiveInfinity;
                x = Math.Max(lo, Math.Min(hi, x));
            }
            var list = pts.ToList();
            list[i] = new Vec2(x, y);
            return list;
        }

        /// <summary>
        /// Every point moved by the offset the anchor point (<paramref name="anchor"/>, drawn at <paramref name="anchorAt"/>)
        /// makes when dragged to <paramref name="local"/>: the offset is snapped to the axis resolution (log axes: a factor);
        /// of a clearly vertical (horizontal) drag only the vertical (horizontal) part counts.
        /// </summary>
        internal static List<Vec2> Offset(List<Vec2> pts, Frame f, Panel panel, Vec2 anchor, Vec2 anchorAt, Vec2 local)
        {
            var area = panel.Area;
            var dtx = (local.X - anchorAt.X) / Math.Max(area.W, 1e-9);
            var dty = (anchorAt.Y - local.Y) / Math.Max(area.H, 1e-9);
            if (Math.Abs(dtx) < 0.5 * Math.Abs(dty)) dtx = 0;
            if (Math.Abs(dty) < 0.5 * Math.Abs(dtx)) dty = 0;
            var (fx, gx) = Shift(f.X, anchor.X, dtx);
            var (fy, gy) = Shift(panel.Y, anchor.Y, dty);
            return pts.Select(p => Fmt.Finite(p.X) && Fmt.Finite(p.Y) ? new Vec2(Apply(p.X, fx, gx), Apply(p.Y, fy, gy)) : p).ToList();
        }

        /// <summary>The snapped shift of an axis value moved by <paramref name="dt"/> (axis fraction): (added, factor).</summary>
        private static (double Add, double Factor) Shift(Axis axis, double v, double dt)
        {
            if (Math.Abs(dt) < 1e-12 || !Fmt.Finite(v)) return (0, 1);
            var to = axis.V(axis.T(v) + dt);
            if (!Fmt.Finite(to)) return (0, 1);
            if (axis.IsLog)
            {
                if (v <= 0 || to <= 0) return (0, 1);
                var factor = Dragging.SnapSignificant(to / v, 2);
                return (0, factor > 0 ? factor : 1);
            }
            var step = axis.Step > 0 ? axis.Step : Axis.NiceStep(Math.Abs(axis.Max - axis.Min) / 5);
            return (Dragging.Snap(to - v, step / 10), 1);
        }

        private static double Apply(double v, double add, double factor)
        {
            var r = factor != 1 ? v * factor : v + add;
            return r == 0 ? 0 : Math.Round(r, Math.Max(0, Math.Min(15, 12 - (int)Math.Floor(Math.Log10(Math.Abs(r))))));
        }

        /// <summary>A point after <paramref name="i"/>: halfway to the next point (after the last: one step on).</summary>
        internal static List<Vec2> AddAfter(List<Vec2> pts, int i)
        {
            if (i < 0 || i >= pts.Count || !Fmt.Finite(pts[i].X) || !Fmt.Finite(pts[i].Y)) return pts;
            var list = pts.ToList();
            var j = Enumerable.Range(i + 1, pts.Count - i - 1).FirstOrDefault(x => Fmt.Finite(pts[x].X) && Fmt.Finite(pts[x].Y));
            Vec2 p;
            if (j > i) p = (pts[i] + pts[j]) / 2;
            else
            {
                var prev = Enumerable.Range(0, i).Reverse().Where(x => Fmt.Finite(pts[x].X) && Fmt.Finite(pts[x].Y)).Select(x => pts[x]).FirstOrDefault();
                p = i > 0 && Enumerable.Range(0, i).Any(x => Fmt.Finite(pts[x].X) && Fmt.Finite(pts[x].Y)) ? pts[i] + (pts[i] - prev) : pts[i] + new Vec2(1, 0);
            }
            list.Insert(i + 1, new Vec2(Apply(p.X, 0, 1), Apply(p.Y, 0, 1)));
            return list;
        }

        /// <summary>Part actions of a data point: delete it, add a point after it, its value ….</summary>
        public static IEnumerable<ContextAction> PointActions(SketchObject o, int k, int i, ISeriesEditor ed, Lang lang)
        {
            var series = ed.Series(o, lang);
            if (k >= series.Count || i >= Math.Min(series[k].X.Count, series[k].Y.Count)) yield break;
            var p = new Vec2(series[k].X[i], series[k].Y[i]);
            yield return new ContextAction(PartLooks.DeleteAction, new I18nText("Delete point", "Punkt löschen"), IconIds.Delete,
                x => ed.EditSeries(x, lang, k, pts => { var l = pts.ToList(); if (i < l.Count) l.RemoveAt(i); return l; })) { Group = "point" };
            yield return new ContextAction("chart.point.add", new I18nText("Add a point after it", "Punkt danach einfügen"), IconIds.AddPoint,
                x => ed.EditSeries(x, lang, k, pts => AddAfter(pts, i))) { Group = "point" };
            yield return ContextAction.WithInput("chart.point.value", new I18nText("Value…", "Wert…"), IconIds.Value,
                new I18nText("x; y of the point", "x; y des Punkts"), Num(p.X, lang) + "; " + Num(p.Y, lang),
                (x, text) =>
                {
                    var parts = (text ?? "").Split(';');
                    if (parts.Length != 2 || !ChartData.TryNumber(parts[0], out var nx) || !ChartData.TryNumber(parts[1], out var ny))
                        throw new FormatException(lang == Lang.De ? "Bitte „x; y“ eingeben, z. B. 2; 4,5" : "Please enter “x; y”, e.g. 2; 4.5");
                    return ed.EditSeries(x, lang, k, pts => { var l = pts.ToList(); if (i < l.Count) l[i] = new Vec2(nx, ny); return l; });
                }) with { Group = "point" };
        }

        /// <summary>"Delete curve" on a curve part.</summary>
        public static ContextAction DeleteSeries(int k, ISeriesEditor ed, Lang lang)
            => new(PartLooks.DeleteAction, new I18nText("Delete curve", "Kurve löschen"), IconIds.Delete, x => ed.EditSeries(x, lang, k, _ => null)) { Group = "series" };

        /// <summary>
        /// The part looks after curve <paramref name="removed"/> was deleted: looks of its parts go, those of later curves
        /// (series, markers, curve labels, points) move down by one.
        /// </summary>
        public static SketchObject RenumberLooks(SketchObject o, int removed)
        {
            if (o.Parts is null || o.Parts.Count == 0) return o;
            var d = new Dictionary<string, PartLook>(StringComparer.Ordinal);
            foreach (var kv in o.Parts)
            {
                var seg = kv.Key.Split('.');
                var indexed = seg.Length >= 2 && seg[0] is ChartTags.Series or ChartTags.Marker or ChartTags.CurveLabel or Kind
                    && int.TryParse(seg[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
                if (!indexed) { d[kv.Key] = kv.Value; continue; }
                var k = int.Parse(seg[1], CultureInfo.InvariantCulture);
                if (k == removed) continue;
                if (k > removed) seg[1] = Fmt.I(k - 1);
                d[string.Join(".", seg)] = kv.Value;
            }
            return o with { Parts = d.Count == 0 ? null : d };
        }
    }
}
