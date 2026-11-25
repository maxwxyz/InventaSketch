using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Charts
{
    /// <summary>A curve as the areas see it: data (NaN = gap), panel, drawn as steps (post) or smoothed, name and colour.</summary>
    internal sealed record AreaSeries(IReadOnlyList<double> X, IReadOnlyList<double> Y, int Panel, bool Step, bool Smooth, string Name, string Color);

    /// <summary>A horizontal value or limit line an area can reach.</summary>
    internal sealed record AreaLine(I18nText Label, double Y, int Panel);

    /// <summary>What area editing needs to know of a chart: its areas, curves, value lines and axes.</summary>
    internal sealed class AreaModel
    {
        public List<FigRegion> Areas { get; init; } = new();
        public IReadOnlyList<AreaSeries> Series { get; init; } = Array.Empty<AreaSeries>();
        public IReadOnlyList<AreaLine> Lines { get; init; } = Array.Empty<AreaLine>();
        public Axis X { get; init; } = null!;
        /// <summary>The y axis of each panel.</summary>
        public IReadOnlyList<Axis> Y { get; init; } = Array.Empty<Axis>();
    }

    /// <summary>Reads and writes the areas of one chart type (XY chart: parameter <c>areas</c>; figure: <c>regions</c> of the specification).</summary>
    internal interface IAreaEditor
    {
        /// <summary>Part kind of the areas ("area" for the XY chart, "region" for the figure, whose part keys stay as they were).</summary>
        string Tag { get; }

        /// <summary>The model of <paramref name="o"/>; null when it cannot be read.</summary>
        AreaModel? Model(SketchObject o, Lang lang);

        /// <summary>The object with its area list changed (throws a bilingual <see cref="FormatException"/> when it cannot be read).</summary>
        SketchObject Edit(SketchObject o, Lang lang, Action<List<FigRegion>> change);
    }

    /// <summary>
    /// Filled areas of XY charts and figures (maintainer 2026-11-25: "everything matplotlib can do with all these styles"):
    /// geometry of every kind (below a curve, between two curves, between a curve and a line, fill_betweenx, vertical and
    /// horizontal spans, steps, where-conditions with exact crossings), their native drawing (closed shapes with hatch fills,
    /// hatch lines and pattern shapes; <see cref="AreaPattern"/>), the part actions of an area (pattern, colour, opacity, edge,
    /// where, steps, label, delete – choices as dropdowns) and the commands that create areas ("Fill below ▸", "Fill to ▸",
    /// "Add area ▸").
    /// </summary>
    internal static class ChartAreas
    {
        /// <summary>Part kind of the XY chart's areas ("area.0").</summary>
        public const string Tag = "area";

        // ------------------------------------------------------------------ geometry

        /// <summary>A curve in drawing coordinates as (u, v): u along the independent axis (x; y for fill_betweenx), v the value.</summary>
        private sealed class Curve
        {
            public List<List<Vec2>> Pieces { get; } = new();

            public IEnumerable<double> Us => Pieces.SelectMany(p => p.Select(v => v.X));

            /// <summary>v at u (first piece that covers u); NaN in gaps and outside.</summary>
            public double At(double u)
            {
                foreach (var piece in Pieces)
                    for (var i = 0; i + 1 < piece.Count; i++)
                    {
                        var a = piece[i]; var b = piece[i + 1];
                        if (u < Math.Min(a.X, b.X) - 1e-9 || u > Math.Max(a.X, b.X) + 1e-9) continue;
                        if (Math.Abs(b.X - a.X) < 1e-12) return a.Y;
                        return a.Y + (b.Y - a.Y) * (u - a.X) / (b.X - a.X);
                    }
                return double.NaN;
            }
        }

        /// <summary>Data points as steps: "pre" (the value holds back to the previous x), "post" (holds until the next x), "mid".</summary>
        internal static List<Vec2> StepPoints(IReadOnlyList<Vec2> p, string mode)
        {
            if (p.Count < 2) return p.ToList();
            var list = new List<Vec2> { p[0] };
            for (var i = 1; i < p.Count; i++)
            {
                switch (mode)
                {
                    case "pre": list.Add(new Vec2(p[i - 1].X, p[i].Y)); break;
                    case "mid":
                        {
                            var m = (p[i - 1].X + p[i].X) / 2;
                            list.Add(new Vec2(m, p[i - 1].Y));
                            list.Add(new Vec2(m, p[i].Y));
                            break;
                        }
                    default: list.Add(new Vec2(p[i].X, p[i - 1].Y)); break;
                }
                list.Add(p[i]);
            }
            return list;
        }

        /// <summary>
        /// The drawn curve of a series in drawing coordinates (gaps and axis breaks split it; smoothed curves sampled), with
        /// vertical jumps of steps moved a hair along u so every u has one value; transposed for fill_betweenx.
        /// </summary>
        private static Curve BuildCurve(AreaSeries s, Frame f, Panel panel, string? step, bool transpose)
        {
            var c = new Curve();
            var n = Math.Min(s.X.Count, s.Y.Count);
            var data = new List<List<Vec2>>();
            List<Vec2>? cur = null;
            for (var i = 0; i < n; i++)
            {
                if (!Fmt.Finite(s.X[i]) || !Fmt.Finite(s.Y[i])) { cur = null; continue; }
                if (cur is null) { cur = new List<Vec2>(); data.Add(cur); }
                cur.Add(new Vec2(s.X[i], s.Y[i]));
            }
            var mode = step ?? (s.Step ? "post" : null);
            foreach (var piece in data)
            {
                var pts = mode is null ? piece : StepPoints(piece, mode);
                foreach (var run in f.VisibleRuns(panel, pts))
                {
                    List<Vec2>? page = null;
                    var pages = new List<List<Vec2>>();
                    foreach (var p in run)
                    {
                        var q = f.P(panel, p.X, p.Y);
                        if (!Frame.Ok(q)) { page = null; continue; }
                        if (page is null) { page = new List<Vec2>(); pages.Add(page); }
                        page.Add(q);
                    }
                    foreach (var pg in pages)
                    {
                        var sampled = s.Smooth && mode is null && pg.Count >= 3 ? Draw.Sample(Draw.Spline(pg)) : pg;
                        var uv = transpose ? sampled.Select(v => new Vec2(v.Y, v.X)).ToList() : sampled.ToList();
                        Nudge(uv);
                        if (uv.Count >= 2) c.Pieces.Add(uv);
                    }
                }
            }
            return c;
        }

        /// <summary>Moves the second point of a jump (two points at the same u) a hair along the direction of travel.</summary>
        private static void Nudge(List<Vec2> uv)
        {
            if (uv.Count < 2) return;
            var dir = uv[uv.Count - 1].X >= uv[0].X ? 1 : -1;
            for (var i = 1; i < uv.Count; i++)
                if (Math.Abs(uv[i].X - uv[i - 1].X) < 1e-9) uv[i] = new Vec2(uv[i - 1].X + dir * 1e-4, uv[i].Y);
        }

        private static double PolyArea(IReadOnlyList<Vec2> poly)
        {
            var a = 0.0;
            for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++) a += (poly[j].X + poly[i].X) * (poly[j].Y - poly[i].Y);
            return a / 2;
        }

        /// <summary>
        /// Polygons (u, v) between v = a(u) and v = b(u) for u in [lo, hi], v clamped to [vmin, vmax]: crossings are inserted
        /// exactly, gaps split the area, and only the parts where <paramref name="keep"/>(a, b) holds are kept.
        /// </summary>
        private static List<List<Vec2>> Between(Func<double, double> fa, IEnumerable<double> ua, Func<double, double> fb, IEnumerable<double> ub,
            double lo, double hi, double vmin, double vmax, Func<double, double, bool> keep)
        {
            var result = new List<List<Vec2>>();
            if (!(hi > lo)) return result;
            var grid = new List<double>();
            foreach (var u in ua.Concat(ub).Where(u => u > lo && u < hi).Concat(new[] { lo, hi }).OrderBy(u => u))
                if (grid.Count == 0 || u - grid[grid.Count - 1] > 1e-7) grid.Add(u);
            var all = new List<(double U, bool Crossing)>();
            for (var i = 0; i < grid.Count; i++)
            {
                // Edges that meet exactly at a vertex cross there too (two lines through the same data point): split, or the
                // polygon would be a bow-tie whose halves cancel out.
                var d = fb(grid[i]) - fa(grid[i]);
                all.Add((grid[i], i > 0 && i + 1 < grid.Count && Fmt.Finite(d) && Math.Abs(d) < 1e-9));
                if (i + 1 >= grid.Count) continue;
                double d0 = fb(grid[i]) - fa(grid[i]), d1 = fb(grid[i + 1]) - fa(grid[i + 1]);
                if (Fmt.Finite(d0) && Fmt.Finite(d1) && d0 * d1 < 0) all.Add((grid[i] + (grid[i + 1] - grid[i]) * d0 / (d0 - d1), true));
            }
            double V(double v) => Math.Max(vmin, Math.Min(vmax, v));
            var run = new List<(double U, double A, double B)>();
            void Flush()
            {
                if (run.Count >= 2)
                {
                    var poly = run.Select(p => new Vec2(p.U, V(p.A))).Concat(Enumerable.Reverse(run).Select(p => new Vec2(p.U, V(p.B)))).ToList();
                    if (Math.Abs(PolyArea(poly)) > 1e-6) result.Add(poly);
                }
                run.Clear();
            }
            bool Ok(double a, double b) => Fmt.Finite(a) && Fmt.Finite(b) && keep(a, b);
            foreach (var (u, crossing) in all)
            {
                double a = fa(u), b = fb(u);
                if (!Ok(a, b)) { Flush(); continue; }
                if (run.Count > 0)
                {
                    var m = (run[run.Count - 1].U + u) / 2;
                    if (!Ok(fa(m), fb(m))) Flush();
                }
                run.Add((u, a, b));
                // A crossing closes the part before it and starts the next one (no self-intersecting polygons).
                if (crossing) { Flush(); run.Add((u, a, b)); }
            }
            Flush();
            return result;
        }

        private static int Clamp(int i, int n) => Math.Max(0, Math.Min(n - 1, i));

        private static double Safe(double v, double lo, double hi) => hi < lo ? (lo + hi) / 2 : Math.Max(lo, Math.Min(hi, v));

        private static double PX(Frame f, double x) => f.Panels[0].Area.X + f.X.T(x) * f.Panels[0].Area.W;

        private static double PY(Panel p, double y) => p.Area.Bottom - p.Y.T(y) * p.Area.H;

        /// <summary>
        /// The filled polygons of area <paramref name="r"/> in drawing coordinates (clipped to the plot) and the panel it
        /// belongs to (the first one for spans over several panels); no polygons when it cannot be drawn (a missing curve,
        /// curves in different panels, nothing where the condition holds).
        /// </summary>
        public static (List<List<Vec2>> Polygons, int Panel) Polygons(FigRegion r, Frame f, IReadOnlyList<AreaSeries> series)
        {
            var n = f.Panels.Count;
            var polys = new List<List<Vec2>>();
            switch (r.Kind)
            {
                case "span":
                    {
                        var from = r.From ?? f.X.Min;
                        var to = r.To ?? f.X.Max;
                        double x0 = PX(f, Math.Min(from, to)), x1 = PX(f, Math.Max(from, to));
                        if (!Fmt.Finite(x0) || !Fmt.Finite(x1)) return (polys, 0);
                        var panels = r.Panel is { } sp && sp >= 0 ? new[] { Clamp(sp, n) } : Enumerable.Range(0, n).ToArray();
                        foreach (var pi in panels)
                        {
                            var a = f.Panels[pi].Area;
                            double l = Safe(x0, a.X, a.Right), rr = Safe(x1, a.X, a.Right);
                            if (rr - l < 1e-6) continue;
                            polys.Add(new List<Vec2> { new(l, a.Y), new(rr, a.Y), new(rr, a.Bottom), new(l, a.Bottom) });
                        }
                        return (polys, panels[0]);
                    }
                case "hspan":
                    {
                        var panels = r.Panel is { } hp && hp < 0 ? Enumerable.Range(0, n).ToArray() : new[] { Clamp(r.Panel ?? 0, n) };
                        foreach (var pi in panels)
                        {
                            var p = f.Panels[pi];
                            var a = p.Area;
                            double y0 = r.Y1 is { } v1 ? PY(p, v1) : a.Bottom, y1 = r.Y2 is { } v2 ? PY(p, v2) : a.Y;
                            double l = r.From is { } fx ? PX(f, fx) : a.X, rr = r.To is { } tx ? PX(f, tx) : a.Right;
                            if (!Fmt.Finite(y0) || !Fmt.Finite(y1) || !Fmt.Finite(l) || !Fmt.Finite(rr)) continue;
                            double top = Safe(Math.Min(y0, y1), a.Y, a.Bottom), bottom = Safe(Math.Max(y0, y1), a.Y, a.Bottom);
                            double left = Safe(Math.Min(l, rr), a.X, a.Right), right = Safe(Math.Max(l, rr), a.X, a.Right);
                            if (bottom - top < 1e-6 || right - left < 1e-6) continue;
                            polys.Add(new List<Vec2> { new(left, top), new(right, top), new(right, bottom), new(left, bottom) });
                        }
                        return (polys, panels[0]);
                    }
                case "betweenx":
                    {
                        if (r.A < 0 || r.A >= series.Count) return (polys, 0);
                        var pa = Clamp(series[r.A].Panel, n);
                        var panel = f.Panels[pa];
                        var area = panel.Area;
                        var ca = BuildCurve(series[r.A], f, panel, r.Step, transpose: true);
                        Func<double, double> fb;
                        IEnumerable<double> ub;
                        if (r.X2 is { } x2)
                        {
                            var px = PX(f, x2);
                            if (!Fmt.Finite(px)) return (polys, pa);
                            fb = _ => px; ub = Array.Empty<double>();
                        }
                        else
                        {
                            if (r.B < 0 || r.B >= series.Count || r.B == r.A || Clamp(series[r.B].Panel, n) != pa) return (polys, pa);
                            var cb = BuildCurve(series[r.B], f, panel, r.Step, transpose: true);
                            fb = cb.At; ub = cb.Us;
                        }
                        // u = drawing y, v = drawing x; "above" = curve A right of the other edge.
                        Func<double, double, bool> keep = r.Where switch
                        {
                            "above" => (a, b) => a - b >= -1e-7,
                            "below" => (a, b) => b - a >= -1e-7,
                            _ => (_, _) => true,
                        };
                        var uv = Between(ca.At, ca.Us, fb, ub, area.Y, area.Bottom, area.X, area.Right, keep);
                        polys.AddRange(uv.Select(p => p.Select(v => new Vec2(v.Y, v.X)).ToList()));
                        return (polys, pa);
                    }
                default:
                    {
                        // "below" and "between".
                        if (r.A < 0 || r.A >= series.Count) return (polys, 0);
                        var pa = Clamp(series[r.A].Panel, n);
                        var panel = f.Panels[pa];
                        var area = panel.Area;
                        var ca = BuildCurve(series[r.A], f, panel, r.Step, transpose: false);
                        Func<double, double> fb;
                        IEnumerable<double> ub = Array.Empty<double>();
                        if (r.Kind == "below")
                        {
                            var baseY = r.Base is { } b && Fmt.Finite(PY(panel, b)) ? Safe(PY(panel, b), area.Y, area.Bottom) : area.Bottom;
                            fb = _ => baseY;
                        }
                        else if (r.Y2 is { } y2)
                        {
                            var py = PY(panel, y2);
                            if (!Fmt.Finite(py)) return (polys, pa);
                            fb = _ => py;
                        }
                        else
                        {
                            if (r.B < 0 || r.B >= series.Count || r.B == r.A || Clamp(series[r.B].Panel, n) != pa) return (polys, pa);
                            var cb = BuildCurve(series[r.B], f, panel, r.Step, transpose: false);
                            fb = cb.At; ub = cb.Us;
                        }
                        var lo = area.X; var hi = area.Right;
                        if (r.From is { } from && Fmt.Finite(PX(f, from))) lo = Math.Max(lo, PX(f, from));
                        if (r.To is { } to && Fmt.Finite(PX(f, to))) hi = Math.Min(hi, PX(f, to));
                        // "above" = curve A above the other edge (smaller drawing y).
                        Func<double, double, bool> keep = r.Kind == "below" ? (_, _) => true : r.Where switch
                        {
                            "above" => (a, b) => b - a >= -1e-7,
                            "below" => (a, b) => a - b >= -1e-7,
                            _ => (_, _) => true,
                        };
                        polys.AddRange(Between(ca.At, ca.Us, fb, ub, lo, hi, area.Y, area.Bottom, keep));
                        return (polys, pa);
                    }
            }
        }

        // ------------------------------------------------------------------ drawing

        /// <summary>The colour an area is drawn in: black in monochrome, else its own colour or the colour of its first curve.</summary>
        public static string ColorOf(FigRegion r, IReadOnlyList<AreaSeries> series, bool mono)
        {
            if (mono) return Palette.Black;
            if (r.Color is { } c) return c;
            return r.Kind is "span" or "hspan" || r.A < 0 || r.A >= series.Count ? Palette.Helper : series[r.A].Color;
        }

        /// <summary>The colour mixed with white (<paramref name="t"/> = 1: white).</summary>
        public static string Lighten(string hex, double t)
        {
            if (hex.Length != 7 || hex[0] != '#' || !int.TryParse(hex.Substring(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb)) return hex;
            int Mix(int c) => (int)Math.Round(c + (255 - c) * t);
            var r = Mix((rgb >> 16) & 0xff); var g = Mix((rgb >> 8) & 0xff); var b = Mix(rgb & 0xff);
            return "#" + r.ToString("x2", CultureInfo.InvariantCulture) + g.ToString("x2", CultureInfo.InvariantCulture) + b.ToString("x2", CultureInfo.InvariantCulture);
        }

        /// <summary>Spacing of one pattern character (mm): the area's own spacing, else 2.4 mm at the design size.</summary>
        public static double Unit(FigRegion r, double k) => r.Spacing ?? Math.Max(1.0, 2.4 * k);

        /// <summary>The face style of an area: its fill (solid, earlier fill or the first line family of its pattern), edge and opacity.</summary>
        public static Style FaceStyle(FigRegion r, string color, bool mono, double k, bool explicitLines = false)
        {
            var edge = r.Edge is { } e ? FigureComposer.DashOf(e, DashStyle.Solid) : (DashStyle?)null;
            var stroke = edge is null ? "none" : mono ? Palette.Black : r.EdgeColor ?? color;
            Fill fill;
            double? opacity = r.Opacity;
            var solid = mono ? "#d9d9d9" : color;
            if (r.Hatch is null)
            {
                // The earlier fills (documents before the hatch patterns keep their look).
                var spacing = r.Spacing ?? Math.Max(0.6, 1.2 * k);
                fill = r.Fill switch
                {
                    "solid" => Fill.Solid(solid),
                    "none" => Fill.None,
                    "dots" => Fill.Dotted(r.Spacing ?? Math.Max(0.6, 1.0 * k)),
                    "cross" => Fill.Hatched(new HatchPattern(r.Angle ?? 45, spacing, Cross: true, Color: color, Width: 0.15)),
                    _ => Fill.Hatched(new HatchPattern(r.Angle ?? 45, spacing, Color: color, Width: 0.15)),
                };
                opacity ??= r.Fill == "solid" && !mono ? 0.3 : null;
            }
            else
            {
                var p = AreaPattern.Parse(r.Hatch);
                var tint = r.Fill == "solid" ? (mono ? "#e6e6e6" : Lighten(color, 0.72)) : null;
                if (!explicitLines && FirstFamily(p) is { } first)
                    fill = Fill.Hatched(new HatchPattern(first.Angle, AreaPattern.LineSpacing(Unit(r, k), first.Count), first.Cross, color, 0.15), tint);
                else fill = tint is null ? Fill.None : Fill.Solid(tint);
            }
            return new Style
            {
                Stroke = stroke, StrokeWidth = edge is null ? null : 0.25, Dash = edge, Fill = fill, Opacity = opacity, Role = LineRole.Thin,
            };
        }

        /// <summary>The line family drawn as the face's hatch fill: the first one, as a cross with its perpendicular twin of equal density.</summary>
        private static (double Angle, int Count, bool Cross)? FirstFamily(AreaPattern p)
        {
            if (p.Lines.Count == 0) return null;
            var (a, c) = p.Lines[0];
            var twin = p.Lines.Any(l => Math.Abs(l.Angle - (a + 90)) < 1e-9 && l.Count == c);
            return (a, c, twin);
        }

        /// <summary>
        /// The primitives of an area: the closed face (fill, edge), further hatch line families as explicit lines and the
        /// pattern shapes, all tagged <paramref name="tag"/>; nothing without polygons.
        /// </summary>
        public static List<Primitive> Render(FigRegion r, IReadOnlyList<List<Vec2>> polys, string tag, string color, bool mono, double k, bool explicitLines = false)
        {
            var list = new List<Primitive>();
            if (polys.Count == 0) return list;
            var segs = new List<PathSeg>();
            foreach (var poly in polys)
            {
                segs.Add(PathSeg.Move(poly[0]));
                for (var i = 1; i < poly.Count; i++) segs.Add(PathSeg.Line(poly[i]));
                segs.Add(PathSeg.Close);
            }
            var face = FaceStyle(r, color, mono, k, explicitLines);
            list.Add(new PathPrim(segs, face) { Tag = tag });
            if (r.Hatch is null) return list;
            var p = AreaPattern.Parse(r.Hatch);
            var unit = Unit(r, k);
            var contours = polys.Select(x => (IReadOnlyList<Vec2>)x).ToList();
            var first = explicitLines ? null : FirstFamily(p);
            var lineStyle = new Style { Stroke = color, StrokeWidth = 0.15, Role = LineRole.Thin, Opacity = r.Opacity };
            var extra = new List<PathSeg>();
            foreach (var (angle, count) in p.Lines)
            {
                if (first is { } fl && (Math.Abs(angle - fl.Angle) < 1e-9 || fl.Cross && Math.Abs(angle - (fl.Angle + 90)) < 1e-9)) continue;
                foreach (var (a, b) in Hatching.Segments(contours, new HatchPattern(angle, AreaPattern.LineSpacing(unit, count))))
                {
                    extra.Add(PathSeg.Move(a));
                    extra.Add(PathSeg.Line(b));
                }
            }
            if (extra.Count > 0) list.Add(new PathPrim(extra, lineStyle) { Tag = tag });
            foreach (var (shape, count) in p.Shapes)
            {
                var shapes = AreaPattern.ShapeSegments(contours, shape, AreaPattern.ShapeSpacing(unit, count));
                if (shapes.Count == 0) continue;
                var style = AreaPattern.Filled(shape) ? lineStyle with { StrokeWidth = 0.1, Fill = Fill.Solid(color) } : lineStyle;
                list.Add(new PathPrim(shapes, style) { Tag = tag });
            }
            return list;
        }

        /// <summary>The legend sample style of an area (its face, or a light tint for shape patterns without a face).</summary>
        public static Style LegendStyle(FigRegion r, string color, bool mono, double k)
        {
            var face = FaceStyle(r, color, mono, k);
            var fill = face.Fill is { Kind: FillKind.None } or null ? Fill.Solid(mono ? "#e6e6e6" : Lighten(color, 0.6)) : face.Fill;
            return face with { Stroke = face.Stroke == "none" ? color : face.Stroke, StrokeWidth = face.StrokeWidth ?? 0.18, Fill = fill };
        }

        // ------------------------------------------------------------------ patterns offered

        /// <summary>The patterns offered in the "Pattern" choice and "Fill below ▸" (value, label).</summary>
        public static readonly (string Value, I18nText Label)[] Patterns =
        {
            ("solid", new I18nText("Solid (tinted)", "Flächig (getönt)")),
            ("/", new I18nText("Hatch /", "Schraffur /")),
            ("//", new I18nText("Dense hatch //", "Dichte Schraffur //")),
            ("\\", new I18nText("Hatch \\", "Schraffur \\")),
            ("\\\\", new I18nText("Dense hatch \\\\", "Dichte Schraffur \\\\")),
            ("|", new I18nText("Vertical |", "Senkrecht |")),
            ("-", new I18nText("Horizontal -", "Waagerecht -")),
            ("+", new I18nText("Grid +", "Gitter +")),
            ("x", new I18nText("Cross-hatch x", "Kreuzschraffur x")),
            ("xx", new I18nText("Dense cross-hatch xx", "Dichte Kreuzschraffur xx")),
            ("o", new I18nText("Small circles o", "Kleine Kreise o")),
            ("O", new I18nText("Large circles O", "Große Kreise O")),
            (".", new I18nText("Dots .", "Punkte .")),
            ("*", new I18nText("Stars *", "Sterne *")),
            ("/o", new I18nText("Hatch with circles /o", "Schraffur mit Kreisen /o")),
            ("none", new I18nText("No fill (edge only)", "Ohne Füllung (nur Rand)")),
        };

        /// <summary>The pattern value of an area as offered ("solid", "none" or the hatch code; earlier fills mapped to their code).</summary>
        public static string PatternOf(FigRegion r, double k)
        {
            if (r.Hatch is { } h) return h;
            return r.Fill switch
            {
                "solid" => "solid",
                "none" => "none",
                _ => AreaPattern.FromLegacy(r.Fill, r.Angle, r.Spacing ?? Math.Max(0.6, 1.2 * k)) ?? "solid",
            };
        }

        /// <summary>Sets the pattern value on an area (a solid face stays under a new pattern; "none" keeps at least an edge).</summary>
        public static void SetPattern(FigRegion r, string value)
        {
            switch (value)
            {
                case "solid": r.Hatch = null; r.Fill = "solid"; break;
                case "none": r.Hatch = null; r.Fill = "none"; r.Edge ??= "solid"; break;
                default:
                    r.Hatch = AreaPattern.Clean(value);
                    if (r.Fill != "solid") r.Fill = "hatch";
                    r.Angle = null;
                    break;
            }
        }

        /// <summary>A 6 × 6 mm swatch of a pattern (for the dropdown options).</summary>
        public static IReadOnlyList<Primitive> Swatch(string value, string color = Palette.Ink)
        {
            var r = new FigRegion { Kind = "span" };
            SetPattern(r, value);
            if (value == "solid") r.Opacity = 0.45;
            r.Spacing = 2.4;
            var square = new List<Vec2> { new(-3, -3), new(3, -3), new(3, 3), new(-3, 3) };
            var list = Render(r, new[] { square }, "swatch", color, false, 1, explicitLines: true);
            list.Add(Prim.Rect(-3, -3, 6, 6, new Style { Stroke = "#9e9e9e", StrokeWidth = 0.15, Role = LineRole.Thin, Fill = Fill.None }));
            return list;
        }

        /// <summary>A 6 × 6 mm colour swatch; an opacity is shown as the colour mixed with white (pictures need no transparency).</summary>
        private static IReadOnlyList<Primitive> ColorSwatch(string color, double? opacity = null)
            => new Primitive[] { Prim.Rect(-3, -3, 6, 6, new Style { Stroke = "#9e9e9e", StrokeWidth = 0.15, Role = LineRole.Thin, Fill = Fill.Solid(opacity is { } a ? Lighten(color, 1 - a) : color) }) };

        /// <summary>A name cut to 20 characters with "…" (option labels must fit the bar and the pie).</summary>
        private static string Short(string s) => s.Length <= 20 ? s : s.Substring(0, 19).TrimEnd() + "…";

        /// <summary>A 6 × 4 mm framed swatch of an edge line style (none: a dotted grey frame only as a hint).</summary>
        private static IReadOnlyList<Primitive> EdgeSwatch(string? edge, string color)
            => new Primitive[]
            {
                Prim.Rect(-3, -2, 6, 4, new Style
                {
                    Stroke = edge is null ? "#c8c8c8" : color, StrokeWidth = edge is null ? 0.15 : 0.35, Role = LineRole.Thin,
                    Dash = edge is null ? DashStyle.Dot : FigureComposer.DashOf(edge, DashStyle.Solid), Fill = Fill.Solid(Lighten(color, 0.8)),
                }),
            };

        /// <summary>Colours offered for an area (value "auto" = the curve's colour).</summary>
        private static readonly (string Value, I18nText Label)[] Colors =
        {
            ("auto", new I18nText("Curve colour", "Kurvenfarbe")),
            ("#000000", new I18nText("Black", "Schwarz")),
            ("#757575", new I18nText("Grey", "Grau")),
            ("#1f5fbf", new I18nText("Blue", "Blau")),
            ("#c62828", new I18nText("Red", "Rot")),
            ("#2e7d32", new I18nText("Green", "Grün")),
            ("#ef6c00", new I18nText("Orange", "Orange")),
            ("#6a1b9a", new I18nText("Purple", "Violett")),
        };

        private static readonly double[] Opacities = { 0.1, 0.2, 0.3, 0.5, 0.7, 1 };

        private static readonly (string? Value, I18nText Label)[] Edges =
        {
            (null, new I18nText("No edge", "Kein Rand")),
            ("solid", new I18nText("Solid", "Durchgezogen")),
            ("dash", new I18nText("Dashed", "Gestrichelt")),
            ("dot", new I18nText("Dotted", "Gepunktet")),
            ("dashdot", new I18nText("Dash-dot", "Strichpunkt")),
        };

        // ------------------------------------------------------------------ part actions of an area

        private static I18nText SeriesLabel(AreaModel m, int k) => new(k >= 0 && k < m.Series.Count ? m.Series[k].Name : "–");

        /// <summary>Display name of an area ("Area 2 – below Curve 1", or its label).</summary>
        public static I18nText Name(AreaModel? m, FigRegion r, int k)
        {
            var en = "Area " + Fmt.I(k + 1);
            var de = "Fläche " + Fmt.I(k + 1);
            if (r.Label is { } l) return new I18nText(en + " – " + l.En, de + " – " + l.Get(Lang.De));
            return new I18nText(en, de);
        }

        /// <summary>
        /// Actions of area <paramref name="k"/>: Pattern ▾, Colour ▾, Opacity ▾, Edge ▾ (choices), background tint (patterns),
        /// Where ▾ and Steps ▾ (areas along curves), Label …, Delete.
        /// </summary>
        public static IEnumerable<ContextAction> PartActions(SketchObject o, int k, IAreaEditor ed, Lang lang, double scale)
        {
            var m = ed.Model(o, lang);
            if (m is null || k < 0 || k >= m.Areas.Count) return Array.Empty<ContextAction>();
            var r = m.Areas[k];
            var list = new List<ContextAction>();
            Func<SketchObject, SketchObject> Change(Action<FigRegion> change) => x => ed.Edit(x, lang, areas => { if (k < areas.Count) change(areas[k]); });
            ContextAction Opt(ActionChoice c, string value, I18nText label, string icon, bool current, Action<FigRegion> change, IReadOnlyList<Primitive>? preview = null)
                => ActionChoices.Option(c, value, label, icon, current, Change(change)) with { Group = "area", Preview = preview };

            var color = ColorOf(r, m.Series, false);
            var pattern = PatternOf(r, scale);
            var pc = new ActionChoice("chart.area.pattern", new I18nText("Pattern", "Muster"));
            foreach (var (value, label) in Patterns)
            {
                var v = value;
                list.Add(Opt(pc, v, label, "", v == pattern, x => SetPattern(x, v), Swatch(v, color)));
            }
            if (r.Hatch is not null)
                list.Add(new ContextAction("chart.area.tint", new I18nText("Tinted background", "Getönter Hintergrund"), IconIds.FillTint,
                    Change(x => x.Fill = x.Fill == "solid" ? "hatch" : "solid")) { IsActive = r.Fill == "solid", Group = "area" });

            var cc = new ActionChoice("chart.area.color", new I18nText("Colour", "Farbe"));
            foreach (var (value, label) in Colors)
            {
                var v = value;
                var swatch = v == "auto" ? (r.Kind is "span" or "hspan" || r.A < 0 || r.A >= m.Series.Count ? Palette.Helper : m.Series[r.A].Color) : v;
                list.Add(Opt(cc, v, label, "", v == "auto" ? r.Color is null : r.Color == v, x => x.Color = v == "auto" ? null : v, ColorSwatch(swatch)));
            }

            var oc = new ActionChoice("chart.area.opacity", new I18nText("Opacity", "Deckkraft"));
            var opacity = r.Opacity ?? (r.Hatch is null && r.Fill == "solid" ? 0.3 : 1);
            foreach (var a in Opacities)
            {
                var v = a;
                var text = Fmt.I((int)Math.Round(v * 100)) + " %";
                list.Add(Opt(oc, Fmt.I((int)Math.Round(v * 100)), new I18nText(text), "", Math.Abs(opacity - v) < 1e-6, x => x.Opacity = v >= 1 ? null : v, ColorSwatch(color, v)));
            }

            // The edge options show a framed swatch (the dash pictograms belong to the part's own line style choice).
            var ec = new ActionChoice("chart.area.edge", new I18nText("Edge", "Rand"));
            foreach (var (value, label) in Edges)
            {
                var v = value;
                list.Add(Opt(ec, v ?? "none", label, IconIds.Option, r.Edge == v, x => x.Edge = v, EdgeSwatch(v, color)));
            }

            if (r.Kind is "between" or "betweenx")
            {
                // Short labels (the bar shows 22 characters): "above" / "below" (right / left of) the area's other edge.
                var x = r.Kind == "betweenx";
                var wc = new ActionChoice("chart.area.where", new I18nText("Where", "Wo"));
                list.Add(Opt(wc, "all", new I18nText("Everywhere", "Überall"), IconIds.Option, r.Where is null, a => a.Where = null));
                list.Add(Opt(wc, "above", x ? new I18nText("Only where right", "Nur wo rechts") : new I18nText("Only where above", "Nur wo darüber"),
                    IconIds.Option, r.Where == "above", a => a.Where = "above"));
                list.Add(Opt(wc, "below", x ? new I18nText("Only where left", "Nur wo links") : new I18nText("Only where below", "Nur wo darunter"),
                    IconIds.Option, r.Where == "below", a => a.Where = "below"));
            }
            if (r.Kind is "below" or "between" or "betweenx")
            {
                var sc = new ActionChoice("chart.area.step", new I18nText("Edges along the curve", "Ränder entlang der Kurve"));
                list.Add(Opt(sc, "none", new I18nText("Straight", "Gerade"), "", r.Step is null, x => x.Step = null));
                list.Add(Opt(sc, "pre", new I18nText("Steps (pre)", "Stufen (vorher)"), "", r.Step == "pre", x => x.Step = "pre"));
                list.Add(Opt(sc, "post", new I18nText("Steps (post)", "Stufen (nachher)"), "", r.Step == "post", x => x.Step = "post"));
                list.Add(Opt(sc, "mid", new I18nText("Steps (mid)", "Stufen (Mitte)"), "", r.Step == "mid", x => x.Step = "mid"));
            }

            list.Add(ContextAction.WithInput("chart.area.label", new I18nText("Legend label…", "Legendentext…"), IconIds.Legend,
                new I18nText("Legend entry of the area (empty: none)", "Legendeneintrag der Fläche (leer: keiner)"), r.Label?.Get(lang) ?? "",
                (x, text) => ed.Edit(x, lang, areas => { if (k < areas.Count) areas[k].Label = string.IsNullOrWhiteSpace(text) ? null : new I18nText(text.Trim()); })) with { Group = "area" });
            list.Add(new ContextAction(PartLooks.DeleteAction, new I18nText("Delete area", "Fläche löschen"), IconIds.Delete,
                x => ed.Edit(x, lang, areas => { if (k < areas.Count) areas.RemoveAt(k); })) { Group = "area" });
            return list;
        }

        // ------------------------------------------------------------------ creating areas

        /// <summary>Default pattern of a new area.</summary>
        public const string DefaultPattern = "//";

        private static FigRegion New(string kind, string pattern)
        {
            var r = new FigRegion { Kind = kind };
            SetPattern(r, pattern);
            return r;
        }

        /// <summary>
        /// Commands on curve <paramref name="k"/>: "Fill below ▸" (one option per pattern) and "Fill to ▸" (another curve of its
        /// panel, a value or limit line, a y value …, a vertical line x = … for fill_betweenx).
        /// </summary>
        public static IEnumerable<ContextAction> CurveActions(SketchObject o, int k, IAreaEditor ed, Lang lang)
        {
            var m = ed.Model(o, lang);
            if (m is null || k < 0 || k >= m.Series.Count) yield break;
            var panel = m.Series[k].Panel;
            var color = m.Series[k].Color;
            Func<SketchObject, SketchObject> Add(Func<FigRegion> make) => x => ed.Edit(x, lang, areas => areas.Add(make()));

            var below = new ActionChoice("chart.area.below", new I18nText("Fill below", "Darunter füllen")) { IsMenu = true };
            foreach (var (value, label) in Patterns.Where(p => p.Value != "none"))
            {
                var v = value;
                yield return ActionChoices.Option(below, v, label, "", false, Add(() => { var r = New("below", v); r.A = k; return r; })) with
                {
                    Group = "area", Preview = Swatch(v, color),
                };
            }

            var to = new ActionChoice("chart.area.to", new I18nText("Fill to", "Füllen bis")) { IsMenu = true };
            for (var j = 0; j < m.Series.Count; j++)
            {
                if (j == k || m.Series[j].Panel != panel) continue;
                var b = j;
                yield return ActionChoices.Option(to, "curve." + Fmt.I(j), new I18nText(Short(m.Series[j].Name)), "", false,
                    Add(() => { var r = New("between", DefaultPattern); r.A = k; r.B = b; return r; })) with { Group = "area" };
            }
            for (var i = 0; i < m.Lines.Count; i++)
            {
                var line = m.Lines[i];
                if (line.Panel != panel) continue;
                var y = line.Y;
                yield return ActionChoices.Option(to, "line." + Fmt.I(i), new I18nText(Short(line.Label.En), Short(line.Label.Get(Lang.De))), "", false,
                    Add(() => { var r = New("between", DefaultPattern); r.A = k; r.Y2 = y; return r; })) with { Group = "area" };
            }
            yield return ActionChoices.InputOption(to, "value", new I18nText("A y value …", "Einen y-Wert …"), "",
                new I18nText("Fill between the curve and the horizontal line y =", "Zwischen Kurve und waagerechter Linie y = füllen"), "0",
                (x, text) => ed.Edit(x, lang, areas => { var r = New("between", DefaultPattern); r.A = k; r.Y2 = Fmt.ParseInput(text, lang, -1e300, 1e300); areas.Add(r); })) with { Group = "area" };
            yield return ActionChoices.InputOption(to, "x", new I18nText("A line x = …", "Eine Linie x = …"), "",
                new I18nText("Fill horizontally between the curve and the vertical line x =", "Waagerecht zwischen Kurve und senkrechter Linie x = füllen"), Fmt.Num(m.X.Min, lang, 4),
                (x, text) => ed.Edit(x, lang, areas => { var r = New("betweenx", DefaultPattern); r.A = k; r.X2 = Fmt.ParseInput(text, lang, -1e300, 1e300); areas.Add(r); })) with { Group = "area" };
        }

        private static double Round(double v) => v == 0 || !Fmt.Finite(v) ? v : Math.Round(v, Math.Max(0, Math.Min(12, 2 - (int)Math.Floor(Math.Log10(Math.Abs(v))))));

        private static double Inner(Axis a, double t) => a.IsLog ? Math.Pow(10, Math.Log10(a.Min) + t * (Math.Log10(a.Max) - Math.Log10(a.Min))) : a.Min + t * (a.Max - a.Min);

        /// <summary>
        /// "Add area ▸" on the chart: below the first curve, between the first two curves of a panel, a vertical span over
        /// the middle third of x, a horizontal band over the middle third of y, between the two first value or limit lines.
        /// </summary>
        public static IEnumerable<ContextAction> AddActions(SketchObject o, IAreaEditor ed, Lang lang)
        {
            var m = ed.Model(o, lang);
            if (m is null) yield break;
            var add = new ActionChoice("chart.area.add", new I18nText("Add area", "Fläche hinzufügen")) { IsMenu = true };
            Func<SketchObject, SketchObject> Add(Func<FigRegion> make) => x => ed.Edit(x, lang, areas => areas.Add(make()));
            ContextAction Opt(string value, I18nText label, Func<FigRegion> make) => ActionChoices.Option(add, value, label, "", false, Add(make)) with { Group = "area" };
            // Short fixed labels (the curve names can be long): the first curve, the first two curves of a panel, the first two lines.
            if (m.Series.Count > 0)
                yield return Opt("below", new I18nText("Below the first curve", "Unter der ersten Kurve"), () => { var r = New("below", DefaultPattern); r.A = 0; return r; });
            var pair = Enumerable.Range(0, m.Series.Count).SelectMany(a => Enumerable.Range(a + 1, Math.Max(0, m.Series.Count - a - 1)).Select(b => (a, b)))
                .FirstOrDefault(p => m.Series[p.a].Panel == m.Series[p.b].Panel && p.a != p.b);
            if (m.Series.Count >= 2 && pair.a != pair.b)
                yield return Opt("between", new I18nText("Between two curves", "Zwischen zwei Kurven"),
                    () => { var r = New("between", DefaultPattern); r.A = pair.a; r.B = pair.b; return r; });
            yield return Opt("span", new I18nText("Vertical span (axvspan)", "Senkrechter Bereich (axvspan)"),
                () => { var r = New("span", DefaultPattern); r.From = Round(Inner(m.X, 1.0 / 3)); r.To = Round(Inner(m.X, 2.0 / 3)); return r; });
            if (m.Y.Count > 0)
                yield return Opt("hspan", new I18nText("Horizontal band (axhspan)", "Waagerechtes Band (axhspan)"),
                    () => { var r = New("hspan", DefaultPattern); r.Y1 = Round(Inner(m.Y[0], 1.0 / 3)); r.Y2 = Round(Inner(m.Y[0], 2.0 / 3)); return r; });
            var lines = m.Lines.Where(l => l.Panel == 0).Take(2).ToList();
            if (lines.Count == 2)
                yield return Opt("lines", new I18nText("Between two lines", "Zwischen zwei Linien"),
                    () => { var r = New("hspan", DefaultPattern); r.Y1 = Math.Min(lines[0].Y, lines[1].Y); r.Y2 = Math.Max(lines[0].Y, lines[1].Y); return r; });
        }

        // ------------------------------------------------------------------ series indices

        /// <summary>
        /// The areas after curve <paramref name="removed"/> was deleted: areas on it are dropped, later curve indices move
        /// down by one.
        /// </summary>
        public static void SeriesRemoved(List<FigRegion> areas, int removed)
        {
            for (var i = areas.Count - 1; i >= 0; i--)
            {
                var r = areas[i];
                var usesA = r.Kind is "below" or "between" or "betweenx";
                var usesB = r.Kind is "between" && r.Y2 is null || r.Kind is "betweenx" && r.X2 is null;
                if (usesA && r.A == removed || usesB && r.B == removed) { areas.RemoveAt(i); continue; }
                if (usesA && r.A > removed) r.A--;
                if (usesB && r.B > removed) r.B--;
            }
        }
    }
}
