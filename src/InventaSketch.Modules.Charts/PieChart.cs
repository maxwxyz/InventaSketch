using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Charts
{
    /// <summary>A slice of a pie: category index, name, value and its angles (degrees clockwise from 12 o'clock; End &lt; Start when counter-clockwise).</summary>
    internal readonly record struct PieSlice(int Index, string Name, double Value, double Start, double End)
    {
        public double Sweep => Math.Abs(End - Start);
        public double Mid => (Start + End) / 2;
    }

    /// <summary>
    /// Pie and donut chart: one slice per category of the data (rows "label;value", the same <see cref="ChartData"/> format
    /// as the bar chart; the first value column counts), a hole for a donut (<c>hole</c> = inner radius / outer radius),
    /// labels with percentages, names or values. A label stays inside its slice (in the ring of a donut) where it fits and
    /// moves outside with a leader line otherwise; everything stays inside the box. Every slice and every label is a part.
    /// </summary>
    internal sealed class PieChart : CanvasChart
    {
        public const string TypeId = "chart.pie";

        public const string DefaultData = "A;35\nB;25\nC;20\nD;12\nE;8";

        public static readonly ParamDef DataDef = Prm.Text("data", "Data (rows label;value)", "Daten (Zeilen Bezeichnung;Wert)", DefaultData);
        public static readonly ParamDef HoleDef = Prm.Num("hole", "Hole (donut, inner/outer radius)", "Loch (Ring, Innen-/Außenradius)", 0, 0, 0.9, step: 0.05);
        public static readonly ParamDef LabelsDef = Prm.Choice("labels", "Slice labels", "Segmentbeschriftung", "percent",
            ("percent", "Percent", "Prozent"), ("name", "Name", "Name"), ("namepercent", "Name and percent", "Name und Prozent"),
            ("value", "Value", "Wert"), ("none", "None", "Keine"));
        public static readonly ParamDef StartDef = Prm.Num("start", "Start angle (° clockwise from 12 o'clock)", "Startwinkel (° im Uhrzeigersinn ab 12 Uhr)", 0, -360, 360, "°", 5);
        public static readonly ParamDef DirectionDef = Prm.Choice("direction", "Direction", "Richtung", "cw",
            ("cw", "Clockwise", "Im Uhrzeigersinn"), ("ccw", "Counter-clockwise", "Gegen den Uhrzeigersinn"));
        public static readonly ParamDef CenterDef = Prm.Text("center", "Centre text (donut)", "Text in der Mitte (Ring)");

        public PieChart() : base(TypeId, new I18nText("Pie chart", "Kreisdiagramm"), ChartCategories.Data,
            "pie", "pie chart", "donut", "doughnut", "ring", "share", "percent", "proportion", "slice", "excel",
            "kreisdiagramm", "tortendiagramm", "torte", "ringdiagramm", "anteil", "prozent", "segment", "kuchendiagramm")
        { }

        public override Size2 Design => new(80, 65);

        protected override IReadOnlyList<ParamDef> BuildParams() => new[]
        {
            DataDef, HoleDef, LabelsDef, StartDef, DirectionDef, CenterDef, Common.Title, Common.Legend, Common.Patterns,
        };

        /// <summary>
        /// The slices of the data: categories with a positive finite value, in order, each with its share of 360°, starting at
        /// <paramref name="start"/> (degrees clockwise from 12 o'clock) and running clockwise or counter-clockwise.
        /// </summary>
        public static List<PieSlice> Slices(string? data, double start, bool clockwise, Lang lang = Lang.En)
        {
            var set = ChartData.Categories(ChartData.Parse(data), lang);
            var list = new List<PieSlice>();
            if (set.Series.Count == 0) return list;
            var values = set.Series[0].Values;
            var total = values.Where(v => Fmt.Finite(v) && v > 0).Sum();
            if (!(total > 0)) return list;
            var a = Fmt.Finite(start) ? start : 0;
            var dir = clockwise ? 1 : -1;
            for (var i = 0; i < set.Categories.Count; i++)
            {
                var v = i < values.Count ? values[i] : double.NaN;
                if (!Fmt.Finite(v) || v <= 0) continue;
                var sweep = 360 * v / total;
                list.Add(new PieSlice(i, set.Categories[i], v, a, a + dir * sweep));
                a += dir * sweep;
            }
            return list;
        }

        /// <summary>Share of a slice as text: "25%" in English, "25 %" in German (one decimal below 10 %).</summary>
        internal static string Percent(double share, Lang lang)
        {
            var p = 100 * share;
            var text = Fmt.Num(p, lang, p < 9.95 ? 1 : 0);
            return lang == Lang.De ? text + " %" : text + "%";
        }

        /// <summary>Point at clock angle <paramref name="deg"/> (clockwise from 12 o'clock) and radius <paramref name="r"/> around <paramref name="c"/>.</summary>
        internal static Vec2 At(Vec2 c, double r, double deg)
        {
            var a = deg * Math.PI / 180;
            return new Vec2(c.X + r * Math.Sin(a), c.Y - r * Math.Cos(a));
        }

        /// <summary>Points of an arc from clock angle <paramref name="a0"/> to <paramref name="a1"/> (at most 3° apart).</summary>
        internal static IEnumerable<Vec2> Arc(Vec2 c, double r, double a0, double a1)
        {
            var n = Math.Max(2, (int)Math.Ceiling(Math.Abs(a1 - a0) / 3));
            for (var i = 0; i <= n; i++) yield return At(c, r, a0 + (a1 - a0) * i / n);
        }

        /// <summary>The model of a drawn pie (for part names and tests).</summary>
        internal sealed class Drawing
        {
            public List<PieSlice> Slices { get; } = new();
            public Vec2 Centre { get; set; }
            public double Radius { get; set; }
            public double Inner { get; set; }
            /// <summary>Label positions per slice index (centre of the text) and whether the label sits inside its slice.</summary>
            public Dictionary<int, (Vec2 At, bool Inside)> Labels { get; } = new();
        }

        private string LabelText(PieSlice s, double total, string mode, Lang lang) => mode switch
        {
            "name" => s.Name,
            "namepercent" => s.Name + " " + Percent(s.Value / total, lang),
            "value" => Fmt.Num(s.Value, lang, 3),
            "none" => "",
            _ => Percent(s.Value / total, lang),
        };

        /// <summary>True if the text box (centre <paramref name="at"/>, w × h) lies inside the slice between the radii.</summary>
        private static bool Fits(Vec2 c, Vec2 at, double w, double h, PieSlice s, double inner, double outer)
        {
            foreach (var p in new[] { at + new Vec2(-w / 2, -h / 2), at + new Vec2(w / 2, -h / 2), at + new Vec2(w / 2, h / 2), at + new Vec2(-w / 2, h / 2) })
            {
                var d = p.DistanceTo(c);
                if (d > outer * 0.96 || d < inner * 1.04) return false;
                if (s.Sweep >= 359.9) continue;
                var ang = Math.Atan2(p.X - c.X, -(p.Y - c.Y)) * 180 / Math.PI;
                var diff = ((ang - s.Mid) % 360 + 540) % 360 - 180;
                if (Math.Abs(diff) > s.Sweep / 2 - 0.5) return false;
            }
            return true;
        }

        protected override void Paint(SketchObject o, RenderContext ctx, Canvas cv)
        {
            var lang = ctx.Lang;
            var clockwise = o.Str("direction", "cw") != "ccw";
            var slices = Slices(o.Str("data", DefaultData), o.Num("start", 0), clockwise, lang);
            var mode = o.Str("labels", "percent");
            var mono = cv.Mono || o.Flag("patterns", false);
            var d = new Drawing();
            d.Slices.AddRange(slices);
            cv.Model = d;
            foreach (var s in slices) cv.Legend.Add(new LegendEntry(s.Name, SliceStyle(s.Index, mono), Area: true));
            // Names on the slices: no legend unless asked for.
            if (mode is "name" or "namepercent" && cv.LegendMode == "auto") cv.LegendMode = "none";
            cv.Layout();
            var area = cv.Area;
            var c = area.Center;
            var hole = Geo.Clamp(o.Num("hole", 0), 0, 0.9);
            if (!Fmt.Finite(hole)) hole = 0;
            var total = slices.Sum(s => s.Value);
            var fs = cv.Fs * 0.85;
            var h = fs * 1.2;
            var texts = slices.ToDictionary(s => s.Index, s => LabelText(s, total, mode, lang));
            // Radius: labels that do not fit inside need room beside the pie (a few rounds, since the fit depends on the radius).
            var r = Math.Min(area.W, area.H) / 2;
            var outside = new HashSet<int>();
            for (var round = 0; round < 4; round++)
            {
                var inner = hole * r;
                outside.Clear();
                foreach (var s in slices)
                {
                    var t = texts[s.Index];
                    if (t.Length == 0) continue;
                    var at = At(c, hole > 0 ? (inner + r) / 2 : r * 0.62, s.Mid);
                    if (!Fits(c, at, Fmt.Width(t, fs), h, s, inner, r)) outside.Add(s.Index);
                }
                if (outside.Count == 0) break;
                var side = slices.Where(s => outside.Contains(s.Index)).Max(s => Fmt.Width(texts[s.Index], fs)) + cv.Gap * 3;
                var next = Math.Max(Math.Min(area.W / 2 - side, area.H / 2 - h), Math.Min(area.W, area.H) * 0.22);
                if (Math.Abs(next - r) < 1e-6) break;
                r = next;
            }
            r = Math.Max(r, 0.5);
            var rIn = hole * r;
            d.Centre = c; d.Radius = r; d.Inner = rIn;

            if (slices.Count == 0)
            {
                // No positive values: an empty ring as a placeholder, the data stay as they are.
                cv.Back.Add(Prim.Polygon(Arc(c, r, 0, 360), S.Helper) with { Tag = ChartTags.Helper });
            }
            foreach (var s in slices)
            {
                var pts = Arc(c, r, s.Start, s.End).ToList();
                if (rIn > 1e-6) pts.AddRange(Arc(c, rIn, s.End, s.Start));
                else pts.Add(c);
                cv.Back.Add(Prim.Polygon(pts, SliceStyle(s.Index, mono)) with { Tag = ChartTags.Nth(ChartTags.Slice, s.Index) });
                cv.Port(At(c, r, s.Mid), "s1." + Fmt.I(s.Index + 1));
            }

            // Labels: inside where they fit, else outside with a leader line, kept apart on each side.
            var outer = new List<(PieSlice S, string Text, Vec2 Rim, double Y, bool Right)>();
            foreach (var s in slices)
            {
                var t = texts[s.Index];
                if (t.Length == 0) continue;
                var tag = ChartTags.Nth(ChartTags.SliceLabel, s.Index);
                if (!outside.Contains(s.Index))
                {
                    var at = At(c, rIn > 0 ? (rIn + r) / 2 : r * 0.62, s.Mid);
                    if (Fits(c, at, Fmt.Width(t, fs), h, s, rIn, r))
                    {
                        d.Labels[s.Index] = (at, true);
                        if (mono)
                        {
                            var w = Fmt.Width(t, fs);
                            cv.Over.Add(Prim.Rect(at.X - w / 2 - fs * 0.15, at.Y - fs * 0.6, w + fs * 0.3, fs * 1.2, S.FillOnly(Palette.White)) with { Tag = tag });
                            cv.Over.Add(new TextPrim(at, t, new TextStyle { Size = fs, Color = Palette.Ink }) { Tag = tag });
                        }
                        else cv.Over.Add(new TextPrim(at, t, new TextStyle { Size = fs, Color = Palette.White }) { Tag = tag });
                        continue;
                    }
                }
                var rim = At(c, r, s.Mid);
                var p = At(c, r + cv.Gap * 2, s.Mid);
                outer.Add((s, t, rim, p.Y, Math.Sin(s.Mid * Math.PI / 180) >= -1e-9));
            }
            foreach (var right in new[] { true, false })
            {
                var group = outer.Where(x => x.Right == right).OrderBy(x => x.Y).ToList();
                var ys = group.Select(x => x.Y).ToArray();
                double top = area.Y + h / 2, bottom = area.Bottom - h / 2;
                for (var i = 1; i < ys.Length; i++) ys[i] = Math.Max(ys[i], ys[i - 1] + h);
                if (ys.Length > 0 && ys[ys.Length - 1] > bottom)
                {
                    ys[ys.Length - 1] = bottom;
                    for (var i = ys.Length - 2; i >= 0; i--) ys[i] = Math.Min(ys[i], ys[i + 1] - h);
                }
                for (var i = 0; i < ys.Length; i++) ys[i] = Geo.Clamp(ys[i], top, Math.Max(top, bottom));
                for (var i = 0; i < group.Count; i++)
                {
                    var g = group[i];
                    var tag = ChartTags.Nth(ChartTags.SliceLabel, g.S.Index);
                    var dir = right ? 1 : -1;
                    var elbow = At(c, r + cv.Gap * 1.2, g.S.Mid);
                    var end = new Vec2(c.X + dir * (r + cv.Gap * 2.2), ys[i]);
                    if (Math.Abs(end.X - c.X) < Math.Abs(elbow.X - c.X)) end = new Vec2(elbow.X + dir * cv.Gap, ys[i]);
                    cv.Over.Add(Prim.Polyline(new[] { g.Rim, elbow, end }, S.Tick) with { Tag = tag });
                    var at = new Vec2(end.X + dir * cv.Gap * 0.5, ys[i]);
                    cv.Over.Add(cv.Text(at, g.Text, fs, right ? TextAnchor.Start : TextAnchor.End, tag));
                    d.Labels[g.S.Index] = (at, false);
                }
            }

            // Centre text of a donut (on a pie it sits on a white backing).
            var centre = o.Str("center", "").Trim();
            if (centre.Length > 0)
            {
                if (rIn > fs)
                {
                    var size = Canvas.Fit(centre, cv.Fs * 1.1, rIn * 1.7);
                    cv.Over.Add(new TextPrim(c, centre, new TextStyle { Size = Math.Min(size, rIn * 0.8), Bold = true, Color = Palette.Ink }) { Tag = ChartTags.CenterText });
                }
                else
                {
                    var size = Canvas.Fit(centre, fs, r * 1.4);
                    Draw.Label(cv.Over, c, centre, size, ChartTags.CenterText);
                }
            }

            cv.PlotAnchors(new Rect(c.X - r, c.Y - r, 2 * r, 2 * r));
            cv.Anchors.Add(Anchor.Point(AnchorRole.Center, c, "centre"));
        }

        /// <summary>Fill of slice <paramref name="k"/>: the series colour with a white seam, or the monochrome pattern with a black edge.</summary>
        internal static Style SliceStyle(int k, bool mono)
        {
            var look = SeriesLook.Of(k);
            return mono
                ? new Style { Stroke = Palette.Black, StrokeWidth = 0.25, Role = LineRole.Visible, Fill = look.MonoFill }
                : new Style { Stroke = Palette.White, StrokeWidth = 0.3, Role = LineRole.Visible, Fill = Fill.Solid(look.Color) };
        }

        private static readonly I18nText SliceWord = new("Slice", "Segment");

        protected override I18nText PartName(string key, Canvas cv)
        {
            var i = IndexOf(key);
            string Name(Lang l) => cv.Model is Drawing d && d.Slices.FirstOrDefault(s => s.Index == i) is { Name: { Length: > 0 } n } ? n : SliceWord.Get(l) + " " + Fmt.I(i + 1);
            return KindOf(key) switch
            {
                ChartTags.Slice when i >= 0 => new I18nText(Name(Lang.En), Name(Lang.De)),
                ChartTags.SliceLabel when i >= 0 => new I18nText("Label – " + Name(Lang.En), "Beschriftung – " + Name(Lang.De)),
                ChartTags.CenterText => new I18nText("Centre text", "Text in der Mitte"),
                ChartTags.Helper => new I18nText("Empty pie", "Leeres Kreisdiagramm"),
                _ => base.PartName(key, cv),
            };
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            var hole = o.Num("hole", 0);
            var list = new List<ContextAction>
            {
                Common.DataInput(o, DefaultData) with { Input = Common.DataInput(o, DefaultData).Input! with
                {
                    Label = new I18nText("Data rows “label;value” separated by |", "Datenzeilen „Bezeichnung;Wert“ getrennt durch |"),
                } },
                new ContextAction("chart.donut", new I18nText("Donut", "Ring"), IconIds.Toggle, x => x.WithParam("hole", x.Num("hole", 0) > 0 ? 0.0 : 0.5))
                    { IsActive = hole > 0, Group = "param", ParamKeys = new[] { "hole" } },
                ContextAction.WithInput("chart.hole", new I18nText("Hole size…", "Lochgröße…"), IconIds.QtyWidth,
                    new I18nText("Inner radius in % of the outer radius (0 = pie)", "Innenradius in % des Außenradius (0 = Kreis)"),
                    Fmt.Num(hole * 100, lang, 0) + " %",
                    (x, text) => x.WithParam("hole", Math.Round(Fmt.ParseInput(text ?? "", lang, 0, 90, "%"), 1) / 100)) with { Group = "param", ParamKeys = new[] { "hole" } },
                ContextAction.WithInput("chart.start", new I18nText("Start angle…", "Startwinkel…"), IconIds.Rotate,
                    new I18nText("Start of the first slice in degrees clockwise from 12 o'clock", "Beginn des ersten Segments in Grad im Uhrzeigersinn ab 12 Uhr"),
                    Fmt.Num(o.Num("start", 0), lang, 1) + "°",
                    (x, text) => x.WithParam("start", Fmt.ParseInput(text ?? "", lang, -360, 360, "°"))) with { Group = "param", ParamKeys = new[] { "start" } },
                ContextAction.WithInput("chart.center.text", new I18nText("Centre text…", "Text in der Mitte…"), IconIds.Text,
                    new I18nText("Text in the middle of the ring (empty = none)", "Text in der Mitte des Rings (leer = keiner)"), o.Str("center", ""),
                    (x, text) => x.WithParam("center", (text ?? "").Trim())) with { Group = "param", ParamKeys = new[] { "center" } },
            };
            list.AddRange(Prm.Options(o, "chart.labels", LabelsDef, new I18nText("Slice labels", "Segmentbeschriftung")));
            list.AddRange(Prm.Options(o, "chart.direction", DirectionDef, new I18nText("Direction", "Richtung")));
            list.AddRange(Common.LegendChoice(o));
            return list;
        }
    }
}
