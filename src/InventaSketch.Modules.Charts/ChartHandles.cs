using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Charts
{
    /// <summary>
    /// A drag handle of a chart in local coordinates with the edit it makes: <see cref="Move"/> receives the object and the
    /// pointer in local coordinates and returns the changed object.
    /// </summary>
    internal sealed record ChartHandle(DragHandle Handle, Func<SketchObject, Vec2, SketchObject> Move);

    /// <summary>
    /// Interactive handles of charts (<see cref="ObjectType.DragHandles"/>): limit lines of XY and tolerance band charts; value
    /// lines, vertical markers, point markers, axis marks and the edges of x spans of figures. Values snap to a tenth of the
    /// axis step on linear axes and to two significant digits on log axes, and stay inside the axis range.
    /// </summary>
    internal static class ChartHandles
    {
        /// <summary>Share of the plot width where handles of horizontal lines sit (the middle, away from labels at the ends).</summary>
        public const double LineHandleAt = 0.5;

        /// <summary>The value at the normalised axis position <paramref name="t"/> (clamped to the axis), snapped (<see cref="Snap"/>).</summary>
        public static double ValueAt(Axis axis, double t)
        {
            if (!Fmt.Finite(t)) return double.NaN;
            return Snap(axis, axis.V(Geo.Clamp(t, 0, 1)));
        }

        /// <summary>Snaps a value to the axis resolution: a tenth of the major step (linear), two significant digits (log).</summary>
        public static double Snap(Axis axis, double v)
        {
            if (!Fmt.Finite(v)) return v;
            if (axis.IsLog) return Geo.Clamp(Dragging.SnapSignificant(v, 2), axis.Min, axis.Max);
            var step = axis.Step > 0 ? axis.Step : Axis.NiceStep(Math.Abs(axis.Max - axis.Min) / 5);
            return Geo.Clamp(Dragging.Snap(v, step / 10), Math.Min(axis.Min, axis.Max), Math.Max(axis.Min, axis.Max));
        }

        /// <summary>Value of the y axis of <paramref name="p"/> at the local point (vertical motion).</summary>
        public static double YValue(Panel p, Vec2 local) => ValueAt(p.Y, (p.Area.Bottom - local.Y) / Math.Max(p.Area.H, 1e-9));

        /// <summary>Value of the x axis at the local point (horizontal motion; all panels share the x mapping).</summary>
        public static double XValue(Frame f, Vec2 local)
        {
            var a = f.Panels[0].Area;
            return ValueAt(f.X, (local.X - a.X) / Math.Max(a.W, 1e-9));
        }

        public static double PX(Frame f, double x) => f.Panels[0].Area.X + f.X.T(x) * f.Panels[0].Area.W;

        public static double PY(Panel p, double y) => p.Area.Bottom - p.Y.T(y) * p.Area.H;

        private static string Num(double v) => Math.Round(v, 10).ToString("R", CultureInfo.InvariantCulture);

        /// <summary>"Limit line: 5.5" / "Grenzlinie: 5,5".</summary>
        public static I18nText Labelled(string en, string de, double v)
            => new(en + ": " + Fmt.Num(v, Lang.En, 6), de + ": " + Fmt.Num(v, Lang.De, 6));

        // ------------------------------------------------------------------ limit lines (XY chart, tolerance band)

        /// <summary>
        /// One vertical handle per limit line (parameter <c>limits</c>) on the first panel where the value is visible; dragging
        /// rewrites that value of the list.
        /// </summary>
        public static IEnumerable<ChartHandle> Limits(Chart c, SketchObject o)
        {
            var values = ChartData.Numbers(o.Str("limits", ""));
            for (var i = 0; i < values.Count; i++)
            {
                var v = values[i];
                var panel = c.F.Panels.FirstOrDefault(p => p.Y.Visible(v));
                if (panel is null) continue;
                var at = new Vec2(panel.Area.X + panel.Area.W * LineHandleAt, PY(panel, v));
                if (!Frame.Ok(at)) continue;
                var index = i;
                var handle = new DragHandle(ChartTags.Nth(ChartTags.Limit, i), at, DragConstraint.Vertical, Labelled("Limit line", "Grenzlinie", v)) { Part = ChartTags.Limit };
                yield return new ChartHandle(handle, (x, local) =>
                {
                    var nv = YValue(panel, local);
                    var list = ChartData.Numbers(x.Str("limits", "")).ToList();
                    if (!Fmt.Finite(nv) || index >= list.Count) return x;
                    list[index] = nv;
                    return x.WithParam("limits", string.Join(";", list.Select(Num)));
                });
            }
        }

        // ------------------------------------------------------------------ figures

        /// <summary>
        /// Handles of a figure: value lines ("ref.k", vertical motion), vertical markers ("ref.k", horizontal), point markers
        /// on a curve ("ref.k", horizontal: the point slides along its curve), named marks on the x axis ("xmark.i") and on
        /// the y axes ("ymark.p.i"), and the edges of x spans ("region.k.from" / "region.k.to").
        /// </summary>
        public static IEnumerable<ChartHandle> Figure(Chart c, FigureSpec s, Lang lang)
        {
            var f = c.F;
            var n = f.Panels.Count;
            int Clamp(int i) => Math.Max(0, Math.Min(n - 1, i));
            var prims = Prim.Flatten(c.Lines).ToList();

            for (var k = 0; k < s.Refs.Count; k++)
            {
                var r = s.Refs[k];
                var key = ChartTags.Nth(ChartTags.Ref, k);
                var index = k;
                var name = r.Label?.Get(lang);
                switch (r.Kind)
                {
                    case "h":
                        {
                            if (!Fmt.Finite(r.Y)) break;
                            var panels = r.Panel is { } rp && rp < 0 ? Enumerable.Range(0, n) : new[] { Clamp(r.Panel ?? 0) };
                            var pi = panels.Where(i => f.Panels[i].Y.Visible(r.Y)).Cast<int?>().FirstOrDefault();
                            if (pi is not int p) break;
                            var panel = f.Panels[p];
                            var at = new Vec2(panel.Area.X + panel.Area.W * LineHandleAt, PY(panel, r.Y));
                            if (!Frame.Ok(at)) break;
                            yield return new ChartHandle(new DragHandle(key, at, DragConstraint.Vertical, Named("Value line", "Wertlinie", name, r.Y)) { Part = key },
                                (x, local) => Edit(x, lang, sp => { if (index < sp.Refs.Count && YValue(panel, local) is var v && Fmt.Finite(v)) sp.Refs[index].Y = v; }));
                            break;
                        }
                    case "v":
                        {
                            if (!Fmt.Finite(r.X) || !f.X.Visible(r.X)) break;
                            int top, bottom;
                            if (r.Panel is { } vp && vp >= 0) top = bottom = Clamp(vp);
                            else { top = 0; bottom = n - 1; }
                            var at = new Vec2(PX(f, r.X), (f.Panels[top].Area.Y + f.Panels[bottom].Area.Bottom) / 2);
                            if (!Frame.Ok(at)) break;
                            yield return new ChartHandle(new DragHandle(key, at, DragConstraint.Horizontal, Named("Vertical marker", "Senkrechte Markierung", name, r.X)) { Part = key },
                                (x, local) => Edit(x, lang, sp => { if (index < sp.Refs.Count && XValue(f, local) is var v && Fmt.Finite(v)) sp.Refs[index].X = v; }));
                            break;
                        }
                    case "point":
                        {
                            // The marker as drawn (it sits on the curve); hidden or clipped markers have no handle.
                            var drawn = prims.Where(p => p.Tag == key).ToList();
                            if (drawn.Count == 0 || Prim.Bounds(drawn) is not { } b) break;
                            var at = b.Center;
                            yield return new ChartHandle(new DragHandle(key, at, DragConstraint.Horizontal, Named("Point marker", "Punktmarkierung", name, r.X)) { Part = key },
                                (x, local) => Edit(x, lang, sp => { if (index < sp.Refs.Count && XValue(f, local) is var v && Fmt.Finite(v)) sp.Refs[index].X = v; }));
                            break;
                        }
                }
            }

            // Named marks on the x axis (below the bottom panel) and on the y axes.
            var bottomPanel = f.Panels[n - 1];
            for (var i = 0; i < s.X.Marks.Count; i++)
            {
                var m = s.X.Marks[i];
                if (!f.X.Visible(m.At)) continue;
                var at = new Vec2(PX(f, m.At), f.AxisY(bottomPanel));
                if (!Frame.Ok(at)) continue;
                var index = i;
                yield return new ChartHandle(new DragHandle("xmark." + Fmt.I(i), at, DragConstraint.Horizontal, Named("Mark", "Marke", m.Text.Get(lang), m.At)) { Part = "axis.x" },
                    (x, local) => Edit(x, lang, sp => { if (index < sp.X.Marks.Count && XValue(f, local) is var v && Fmt.Finite(v)) sp.X.Marks[index] = sp.X.Marks[index] with { At = v }; }));
            }
            for (var p = 0; p < Math.Min(n, s.Panels.Count); p++)
            {
                var panel = f.Panels[p];
                var marks = s.Panels[p].Marks;
                for (var i = 0; i < marks.Count; i++)
                {
                    var m = marks[i];
                    if (!panel.Y.Visible(m.At)) continue;
                    var at = new Vec2(f.AxisX(panel), PY(panel, m.At));
                    if (!Frame.Ok(at)) continue;
                    int pIndex = p, index = i;
                    yield return new ChartHandle(new DragHandle("ymark." + Fmt.I(p) + "." + Fmt.I(i), at, DragConstraint.Vertical, Named("Mark", "Marke", m.Text.Get(lang), m.At))
                    { Part = n > 1 ? "axis.y." + Fmt.I(p + 1) : "axis.y" },
                        (x, local) => Edit(x, lang, sp =>
                        {
                            var v = YValue(panel, local);
                            if (pIndex >= sp.Panels.Count || index >= sp.Panels[pIndex].Marks.Count || !Fmt.Finite(v)) return;
                            sp.Panels[pIndex].Marks[index] = sp.Panels[pIndex].Marks[index] with { At = v };
                        }));
                }
            }

            // Edges of x spans (hatched ranges between two x values).
            for (var k = 0; k < s.Regions.Count; k++)
            {
                var g = s.Regions[k];
                if (g.Kind != "span") continue;
                int top, bottom;
                if (g.Panel is { } gp && gp >= 0) top = bottom = Clamp(gp);
                else { top = 0; bottom = n - 1; }
                var y = (f.Panels[top].Area.Y + f.Panels[bottom].Area.Bottom) / 2;
                var key = ChartTags.Nth(ChartTags.Region, k);
                var index = k;
                foreach (var (edge, value) in new[] { ("from", g.From), ("to", g.To) })
                {
                    if (value is not double ev || !f.X.Visible(ev)) continue;
                    var at = new Vec2(PX(f, ev), y);
                    if (!Frame.Ok(at)) continue;
                    var from = edge == "from";
                    yield return new ChartHandle(new DragHandle(key + "." + edge, at, DragConstraint.Horizontal,
                        Labelled(from ? "Region start" : "Region end", from ? "Bereichsanfang" : "Bereichsende", ev)) { Part = key },
                        (x, local) => Edit(x, lang, sp =>
                        {
                            var v = XValue(f, local);
                            if (index >= sp.Regions.Count || !Fmt.Finite(v)) return;
                            if (from) sp.Regions[index].From = v; else sp.Regions[index].To = v;
                        }));
                }
            }
        }

        /// <summary>"Value line – limit temperature: 80" or "Value line: 80".</summary>
        private static I18nText Named(string en, string de, string? name, double v)
            => string.IsNullOrWhiteSpace(name) ? Labelled(en, de, v) : Labelled(en + " – " + name, de + " – " + name, v);

        private static SketchObject Edit(SketchObject o, Lang lang, Action<FigureSpec> change)
        {
            if (!FigureSpec.TryParse(FigureChart.SpecText(o), out var spec, out _) || spec is null) return o;
            change(spec);
            return o.WithParam("spec", spec.ToJson());
        }
    }
}
