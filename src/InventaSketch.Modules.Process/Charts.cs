using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using InventaSketch.Core;
using static InventaSketch.Modules.Process.P;

namespace InventaSketch.Modules.Process
{
    /// <summary>
    /// Box chart of an idealised thermodynamic cycle (<see cref="Cycles"/>): axes with arrowheads and labels (no scale values),
    /// the optional saturation dome, the cycle with direction arrows and the numbered states 1–4, and a caption. Everything
    /// is drawn inside the box; free scaling stretches the chart, the text keeps a size that fits.
    /// </summary>
    internal abstract class CycleChartType : ObjectType
    {
        private static readonly IReadOnlyList<ViewDef> ViewList = new[] { ProcessViews.ChartView };

        protected static readonly ParamDef Labels = Flag("labels", "State numbers", "Zustandsnummern", true);
        protected static readonly ParamDef CaptionParam = Flag("caption", "Caption", "Beschriftung", true);

        public override string Category => ProcessCategories.Thermo;
        public override IReadOnlyList<ViewDef> Views => ViewList;
        public override Size2 DefaultSize => new(80, 60);
        public override bool IsPart => false;

        /// <summary>Axis labels (x, y).</summary>
        protected abstract (I18nText X, I18nText Y) AxisLabels(SketchObject o);
        public abstract CycleDiagram Diagram(SketchObject o);
        protected abstract I18nText Caption(SketchObject o);
        protected virtual bool ShowEfficiency(SketchObject o) => false;

        protected static string Percent(double v, Lang lang)
        {
            var s = (v * 100).ToString("0.0", CultureInfo.InvariantCulture);
            return (lang == Lang.De ? s.Replace('.', ',') : s) + " %";
        }

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            var lang = ctx.Lang;
            double w = Math.Max(o.Size.W, 0.5), h = Math.Max(o.Size.H, 0.5);
            double hw = w / 2, hh = h / 2;
            var ts = Geo.Clamp(Math.Min(w, h) * 0.055, 0.4, 4.0);
            var list = new List<Primitive>();
            var (xl, yl) = AxisLabels(o);
            string xLabel = xl.Get(lang), yLabel = yl.Get(lang);
            var showCaption = o.Flag("caption", true);
            var diagram = Diagram(o);

            // Layout: the y label left of the y-axis tip, the x label below the x-axis tip, the caption in the top row.
            var yLabelW = yLabel.Length * 0.55 * ts;
            var x0 = Math.Min(-hw + yLabelW + 0.8 * ts, -hw + w * 0.3);
            var y1 = hh - 1.45 * ts;
            var xTip = hw - 0.4 * ts;
            var yTip = -hh + (showCaption ? 1.9 : 0.6) * ts;
            var head = 0.9 * ts;
            var axis = S.Sym with { StrokeWidth = Math.Min(0.35, 0.08 * ts + 0.05) };
            list.Add(Prim.Line(new Vec2(x0, y1), new Vec2(x0, yTip + head * 0.8), axis) with { Tag = ProcessTags.Axis });
            list.Add(Prim.Line(new Vec2(x0, y1), new Vec2(xTip - head * 0.8, y1), axis) with { Tag = ProcessTags.Axis });
            list.Add(HeadAt(new Vec2(x0, yTip), new Vec2(0, -1), head, ProcessTags.Axis));
            list.Add(HeadAt(new Vec2(xTip, y1), new Vec2(1, 0), head, ProcessTags.Axis));
            list.Add(Text(new Vec2(x0 - 0.4 * ts, yTip + 0.35 * ts), yLabel, ts, TextAnchor.End, ProcessTags.Axis, italic: true));
            list.Add(Text(new Vec2(xTip, y1 + 0.85 * ts), xLabel, ts, TextAnchor.End, ProcessTags.Axis, italic: true));
            if (showCaption)
            {
                var caption = Caption(o).Get(lang);
                if (ShowEfficiency(o) && diagram.Efficiency is double eta) caption += (lang == Lang.De ? ", η = " : ", η = ") + Percent(eta, lang);
                var size = Math.Min(ts * 0.9, (w - 0.4 * ts) / (Math.Max(1, caption.Length) * 0.55));
                list.Add(Text(new Vec2(hw - 0.2 * ts, -hh + 0.75 * ts), caption, size, TextAnchor.End, ProcessTags.Label));
            }

            // Data area and mapping (data y up → screen y down).
            double px0 = x0 + 0.8 * ts, px1 = xTip - 1.0 * ts, py0 = yTip + 1.0 * ts, py1 = y1 - 0.9 * ts;
            if (px1 - px0 < 0.5) px1 = px0 + 0.5;
            if (py1 - py0 < 0.5) py0 = py1 - 0.5;
            var range = diagram.Range;
            Vec2 Map(Vec2 d) => new(px0 + (d.X - range.X) / range.W * (px1 - px0), py1 - (d.Y - range.Y) / range.H * (py1 - py0));

            var cycleStyle = S.Bold with { StrokeWidth = Math.Min(0.5, 0.12 * ts + 0.05) };
            if (diagram.Dome is { } dome && ShowDome(o))
                list.Add(Prim.Polyline(dome.Select(Map), S.Thin with { StrokeWidth = Math.Min(0.3, 0.07 * ts + 0.04) }) with { Tag = ProcessTags.Dome });
            var outline = new List<Vec2>();
            foreach (var path in diagram.Paths)
                foreach (var q in path.Select(Map))
                    if (outline.Count == 0 || outline[outline.Count - 1].DistanceTo(q) > 1e-9) outline.Add(q);
            if (outline.Count > 1 && outline[0].DistanceTo(outline[outline.Count - 1]) < 1e-9) outline.RemoveAt(outline.Count - 1);
            list.Add(Prim.Polyline(outline, cycleStyle, closed: true) with { Tag = ProcessTags.Cycle });
            foreach (var path in diagram.Paths)
            {
                var (at, dir) = MidOf(path.Select(Map).ToList());
                if (dir.Length > Geo.Eps) list.Add(HeadAt(at + dir * (0.35 * ts), dir, 0.75 * ts, "direction"));
            }

            var states = diagram.States.Select(Map).ToList();
            foreach (var s in states) list.Add(Prim.Circle(s.X, s.Y, 0.22 * ts, S.Black with { StrokeWidth = 0.05 }) with { Tag = ProcessTags.State });
            if (o.Flag("labels", true))
            {
                var centre = new Vec2(states.Average(s => s.X), states.Average(s => s.Y));
                var at = states.Select(s =>
                {
                    var d = (s - centre).Normalized();
                    if (d.Length < Geo.Eps) d = new Vec2(1, -1).Normalized();
                    return s + d * (0.9 * ts);
                }).ToList();
                // Pull apart labels of states that lie close together (e.g. 1 and 4 of the Otto cycle).
                for (var iter = 0; iter < 4; iter++)
                    for (var i = 0; i < at.Count; i++)
                        for (var j = i + 1; j < at.Count; j++)
                        {
                            var gap = at[i].DistanceTo(at[j]);
                            if (gap >= 1.15 * ts) continue;
                            var push = (1.15 * ts - gap) / 2;
                            // Apart along their separation; labels on top of each other go apart vertically, the higher state up.
                            var sep = (at[j] - at[i]).Normalized();
                            if (sep.Length < Geo.Eps) sep = new Vec2(0, states[i].Y < states[j].Y ? 1 : -1);
                            at[i] = at[i] - sep * push;
                            at[j] = at[j] + sep * push;
                        }
                for (var i = 0; i < at.Count; i++)
                {
                    var p = new Vec2(Geo.Clamp(at[i].X, x0 + 0.5 * ts, hw - 0.4 * ts), Geo.Clamp(at[i].Y, -hh + 0.55 * ts, y1 - 0.55 * ts));
                    list.Add(Text(p, (i + 1).ToString(CultureInfo.InvariantCulture), ts * 0.9, TextAnchor.Middle, ProcessTags.State));
                }
            }
            return list;
        }

        protected virtual bool ShowDome(SketchObject o) => true;

        private static TextPrim Text(Vec2 at, string text, double size, TextAnchor anchor, string tag, bool italic = false)
            => new(at, text, new TextStyle { Size = size, Anchor = anchor, Baseline = TextBaseline.Middle, Italic = italic }) { Tag = tag };

        private static Primitive HeadAt(Vec2 tip, Vec2 dir, double len, string tag)
        {
            var d = dir.Normalized();
            var b = tip - d * len;
            var n = d.Perp * (len * 0.36);
            return Prim.Polygon(new[] { tip, b + n, b - n }, S.Black with { StrokeWidth = 0.05 }) with { Tag = tag };
        }

        /// <summary>Point and unit direction halfway along a polyline (by length).</summary>
        internal static (Vec2 At, Vec2 Dir) MidOf(IReadOnlyList<Vec2> p)
        {
            var total = 0.0;
            for (var i = 0; i + 1 < p.Count; i++) total += p[i].DistanceTo(p[i + 1]);
            var half = total / 2;
            for (var i = 0; i + 1 < p.Count; i++)
            {
                var len = p[i].DistanceTo(p[i + 1]);
                if (len < Geo.Eps) continue;
                if (half <= len) return (p[i] + (p[i + 1] - p[i]) * (half / len), (p[i + 1] - p[i]) / len);
                half -= len;
            }
            return (p.Count > 0 ? p[0] : Vec2.Zero, Vec2.Zero);
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
            => ChartActions(o).Concat(new[]
            {
                Toggle(o, "process.chart.labels", T("State numbers", "Zustandsnummern"), "labels", true, IconIds.Renumber),
                Toggle(o, "process.chart.caption", T("Caption", "Beschriftung"), "caption", true, IconIds.LabelShow),
            });

        protected abstract IEnumerable<ContextAction> ChartActions(SketchObject o);
    }

    /// <summary>T-s diagram: Clausius-Rankine cycle with the saturation dome (default) or the Carnot cycle.</summary>
    internal sealed class TsChartType : CycleChartType
    {
        public const string TypeId = "process.chart.ts";

        internal static readonly ParamDef CycleParam = Choice("cycle", "Cycle", "Kreisprozess", "rankine",
            ("rankine", "Clausius-Rankine (steam power)", "Clausius-Rankine (Dampfkraft)"), ("carnot", "Carnot", "Carnot"));
        internal static readonly ParamDef Superheat = Flag("superheat", "Superheating", "Überhitzung", true);
        internal static readonly ParamDef Dome = Flag("dome", "Saturation dome", "Nassdampfgebiet (Grenzkurve)", true);

        public override string Id => TypeId;
        public override I18nText Name => T("T-s diagram (cycle)", "T-s-Diagramm (Kreisprozess)");
        public override IReadOnlyList<string> Keywords => new[]
        {
            "t-s diagram", "ts diagram", "temperature entropy", "rankine", "clausius-rankine", "carnot", "steam power", "cycle", "thermodynamics", "saturation dome",
            "t-s-diagramm", "ts-diagramm", "temperatur entropie", "kreisprozess", "dampfkraftprozess", "thermodynamik", "nassdampfgebiet", "grenzkurve",
        };
        public override IReadOnlyList<ParamDef> Parameters => new[] { CycleParam, Superheat, Dome, Labels, CaptionParam };

        protected override (I18nText X, I18nText Y) AxisLabels(SketchObject o) => (T("s", "s"), T("T", "T"));

        public override CycleDiagram Diagram(SketchObject o) => o.Str("cycle", "rankine") == "carnot"
            ? Cycles.CarnotTs()
            : Cycles.Rankine(o.Flag("superheat", true));

        protected override bool ShowDome(SketchObject o) => o.Flag("dome", true);

        protected override I18nText Caption(SketchObject o) => o.Str("cycle", "rankine") == "carnot"
            ? T("Carnot cycle", "Carnot-Prozess")
            : T("Rankine cycle", "Clausius-Rankine-Prozess");

        protected override IEnumerable<ContextAction> ChartActions(SketchObject o) => new[]
        {
            Cycle("process.chart.cycle", T("Rankine / Carnot", "Rankine / Carnot"), CycleParam),
            Toggle(o, "process.chart.superheat", T("Superheating", "Überhitzung"), "superheat", true),
            Toggle(o, "process.chart.dome", T("Saturation dome", "Grenzkurve"), "dome", true),
        };
    }

    /// <summary>p-v diagram of the air-standard cycles: Otto (default), Diesel, Carnot (ideal gas).</summary>
    internal sealed class PvChartType : CycleChartType
    {
        public const string TypeId = "process.chart.pv";

        internal static readonly ParamDef CycleParam = Choice("cycle", "Cycle", "Kreisprozess", "otto",
            ("otto", "Otto (constant volume)", "Otto (Gleichraum)"), ("diesel", "Diesel (constant pressure)", "Diesel (Gleichdruck)"),
            ("carnot", "Carnot (ideal gas)", "Carnot (ideales Gas)"));
        internal static readonly ParamDef Ratio = Num("ratio", "Compression ratio r = v_max/v_min", "Verdichtungsverhältnis ε = v_max/v_min", 8, 2, 25, 0.5);
        internal static readonly ParamDef Cutoff = Num("cutoff", "Cut-off ratio (Diesel)", "Einspritzverhältnis φ (Diesel)", 2, 1.1, 5, 0.1);
        internal static readonly ParamDef PressureRatio = Num("pressureRatio", "Pressure ratio of heat addition (Otto)", "Drucksteigerung bei Wärmezufuhr (Otto)", 2.5, 1.2, 6, 0.1, advanced: true);
        internal static readonly ParamDef Gamma = Num("gamma", "Isentropic exponent γ", "Isentropenexponent κ", 1.4, 1.1, 1.67, 0.01, advanced: true);
        internal static readonly ParamDef Efficiency = Flag("efficiency", "Show efficiency", "Wirkungsgrad anzeigen", false);

        public override string Id => TypeId;
        public override I18nText Name => T("p-v diagram (cycle)", "p-v-Diagramm (Kreisprozess)");
        public override IReadOnlyList<string> Keywords => new[]
        {
            "p-v diagram", "pv diagram", "pressure volume", "otto", "diesel", "carnot", "air standard", "engine cycle", "cycle", "thermodynamics",
            "p-v-diagramm", "pv-diagramm", "druck volumen", "kreisprozess", "gleichraumprozess", "gleichdruckprozess", "verbrennungsmotor", "thermodynamik",
        };
        public override IReadOnlyList<ParamDef> Parameters => new[] { CycleParam, Ratio, Cutoff, PressureRatio, Gamma, Efficiency, Labels, CaptionParam };

        protected override (I18nText X, I18nText Y) AxisLabels(SketchObject o) => (T("v", "v"), T("p", "p"));

        public override CycleDiagram Diagram(SketchObject o)
        {
            var r = o.Num("ratio", 8);
            var g = o.Num("gamma", 1.4);
            return o.Str("cycle", "otto") switch
            {
                "diesel" => Cycles.Diesel(r, o.Num("cutoff", 2), g),
                "carnot" => Cycles.CarnotPv(r, g),
                _ => Cycles.Otto(r, o.Num("pressureRatio", 2.5), g),
            };
        }

        protected override bool ShowEfficiency(SketchObject o) => o.Flag("efficiency", false);

        protected override I18nText Caption(SketchObject o) => o.Str("cycle", "otto") switch
        {
            "diesel" => T("Diesel cycle", "Diesel-Prozess"),
            "carnot" => T("Carnot cycle", "Carnot-Prozess"),
            _ => T("Otto cycle", "Otto-Prozess"),
        };

        protected override IEnumerable<ContextAction> ChartActions(SketchObject o) => new[]
        {
            Cycle("process.chart.cycle", T("Otto / Diesel / Carnot", "Otto / Diesel / Carnot"), CycleParam),
            new ContextAction("process.chart.ratio.up", T("Higher compression ratio", "Höheres Verdichtungsverhältnis"), IconIds.Increase,
                x => x.WithParam("ratio", Math.Min(25, x.Num("ratio", 8) + 1))) { Group = "param", ParamKeys = new[] { "ratio" } },
            new ContextAction("process.chart.ratio.down", T("Lower compression ratio", "Niedrigeres Verdichtungsverhältnis"), IconIds.Decrease,
                x => x.WithParam("ratio", Math.Max(2, x.Num("ratio", 8) - 1))) { Group = "param", ParamKeys = new[] { "ratio" } },
            Toggle(o, "process.chart.efficiency", T("Show efficiency", "Wirkungsgrad anzeigen"), "efficiency", false),
        };
    }

    /// <summary>log p-h diagram: vapour-compression refrigeration (heat pump) cycle with the saturation dome.</summary>
    internal sealed class PhChartType : CycleChartType
    {
        public const string TypeId = "process.chart.ph";

        internal static readonly ParamDef Ratio = Num("ratio", "Pressure ratio p_high/p_low", "Druckverhältnis p_hoch/p_niedrig", 4, 1.5, Cycles.PhMaxRatio, 0.5);
        internal static readonly ParamDef Superheat = Flag("superheat", "Superheating at the evaporator outlet", "Überhitzung am Verdampferaustritt", true);
        internal static readonly ParamDef Subcool = Flag("subcool", "Subcooling at the condenser outlet", "Unterkühlung am Verflüssigeraustritt", false);

        public override string Id => TypeId;
        public override I18nText Name => T("log p-h diagram (refrigeration cycle)", "log p-h-Diagramm (Kältekreisprozess)");
        public override IReadOnlyList<string> Keywords => new[]
        {
            "p-h diagram", "ph diagram", "log p-h", "pressure enthalpy", "refrigeration cycle", "vapour compression", "vapor compression", "heat pump", "refrigerant", "mollier",
            "log p-h-diagramm", "ph-diagramm", "druck enthalpie", "kältekreis", "kältekreisprozess", "kaltdampfprozess", "wärmepumpe", "kältemittel",
        };
        public override IReadOnlyList<ParamDef> Parameters => new[] { Ratio, Superheat, Subcool, Labels, CaptionParam };

        protected override (I18nText X, I18nText Y) AxisLabels(SketchObject o) => (T("h", "h"), T("log p", "lg p"));

        public override CycleDiagram Diagram(SketchObject o) => Cycles.Refrigeration(o.Num("ratio", 4), o.Flag("superheat", true), o.Flag("subcool", false));

        protected override I18nText Caption(SketchObject o) => T("Vapour-compression cycle", "Kaltdampf-Kompressionsprozess");

        protected override IEnumerable<ContextAction> ChartActions(SketchObject o) => new[]
        {
            new ContextAction("process.chart.ratio.up", T("Higher pressure ratio", "Höheres Druckverhältnis"), IconIds.Increase,
                x => x.WithParam("ratio", Math.Min(Cycles.PhMaxRatio, x.Num("ratio", 4) + 0.5))) { Group = "param", ParamKeys = new[] { "ratio" } },
            new ContextAction("process.chart.ratio.down", T("Lower pressure ratio", "Niedrigeres Druckverhältnis"), IconIds.Decrease,
                x => x.WithParam("ratio", Math.Max(1.5, x.Num("ratio", 4) - 0.5))) { Group = "param", ParamKeys = new[] { "ratio" } },
            Toggle(o, "process.chart.superheat", T("Superheating", "Überhitzung"), "superheat", true),
            Toggle(o, "process.chart.subcool", T("Subcooling", "Unterkühlung"), "subcool", false),
        };
    }
}
