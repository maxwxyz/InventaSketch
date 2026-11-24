using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Charts
{
    /// <summary>Rendered chart: primitives in local coordinates and the anchors computed from the same layout.</summary>
    internal sealed class Chart
    {
        public Chart(Frame frame, Lang lang) { F = frame; Lang = lang; }

        public Frame F { get; }
        public Lang Lang { get; }
        /// <summary>Area fills (bars, bands) drawn above the grid and below the lines.</summary>
        public List<Primitive> Areas { get; } = new();
        /// <summary>Series lines and markers.</summary>
        public List<Primitive> Lines { get; } = new();
        /// <summary>Labels and notes drawn above the axes.</summary>
        public List<Primitive> Over { get; } = new();
        public List<Anchor> Anchors { get; } = new();
        /// <summary>Plot positions the inside legend should not cover.</summary>
        public List<IReadOnlyList<Vec2>> Avoid { get; } = new();
        private int _ports;

        /// <summary>Adds a data point port (at most 160 per chart, only inside the plot).</summary>
        public void Port(Vec2 p, string id)
        {
            if (_ports >= 160 || !Frame.Ok(p) || !F.Plot.Inflate(0.01).Contains(p)) return;
            _ports++;
            Anchors.Add(Anchor.Point(AnchorRole.Port, p, id));
        }

        public IReadOnlyList<Primitive> Render()
        {
            var list = new List<Primitive>();
            F.DrawGrid(list);
            F.DrawZero(list);
            list.AddRange(Areas);
            list.AddRange(Lines);
            F.DrawAxes(list, Lang);
            list.AddRange(Over);
            F.DrawLegend(list, Draw.Densify(Avoid, Math.Max(0.5, F.Fs * 0.5)));
            return list;
        }

        public IEnumerable<Anchor> AllAnchors() => F.FrameAnchors().Concat(Anchors);
    }

    /// <summary>
    /// Base class of all chart types: box geometry, drawn in local coordinates inside the object box. Free scaling changes
    /// only <see cref="SketchObject.Size"/>; the plot area, fonts and markers follow the box, the data (parameters) stay.
    /// </summary>
    internal abstract class ChartType : ObjectType
    {
        private readonly string _id;
        private readonly I18nText _name;
        private readonly string _category;
        private readonly string[] _keywords;
        private IReadOnlyList<ParamDef>? _params;

        protected ChartType(string id, I18nText name, string category, params string[] keywords)
        {
            _id = id; _name = name; _category = category; _keywords = keywords;
        }

        public override string Id => _id;
        public override I18nText Name => _name;
        public override string Category => _category;
        public override IReadOnlyList<string> Keywords => _keywords;
        /// <summary>Charts are figures, not parts: never in the bill of materials.</summary>
        public override bool IsPart => false;
        public virtual Size2 Design => new(100, 65);
        public override Size2 DefaultSize => Design;
        public override IReadOnlyList<ParamDef> Parameters => _params ??= BuildParams();

        protected abstract IReadOnlyList<ParamDef> BuildParams();

        /// <summary>Lays out and draws the chart (shared by rendering and anchors).</summary>
        internal abstract Chart Compose(SketchObject o, RenderContext ctx);

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx) => Compose(o, ctx).Render();

        public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx) => Compose(o, ctx).AllAnchors().ToList();

        public static bool Mono(RenderContext ctx) => ctx.Monochrome || ctx.Doc.Settings.Monochrome;
    }

    /// <summary>Parameters and helpers shared by the chart types.</summary>
    internal static class Common
    {
        public static readonly ParamDef Title = Prm.Text("title", "Chart title", "Diagrammtitel");
        public static readonly ParamDef XLabel = Prm.Text("xlabel", "x-axis label", "Beschriftung x-Achse");
        public static readonly ParamDef XUnit = Prm.Text("xunit", "x-axis unit", "Einheit x-Achse");
        public static readonly ParamDef YLabel = Prm.Text("ylabel", "y-axis label", "Beschriftung y-Achse");
        public static readonly ParamDef YUnit = Prm.Text("yunit", "y-axis unit", "Einheit y-Achse");
        public static readonly ParamDef XScale = Prm.Choice("xscale", "x-axis scale", "Skala x-Achse", "linear", ("linear", "Linear", "Linear"), ("log", "Logarithmic", "Logarithmisch"));
        public static readonly ParamDef YScale = Prm.Choice("yscale", "y-axis scale", "Skala y-Achse", "linear", ("linear", "Linear", "Linear"), ("log", "Logarithmic", "Logarithmisch"));
        public static readonly ParamDef XRange = Prm.Text("xrange", "x-axis range (min;max, empty = auto)", "Bereich x-Achse (min;max, leer = automatisch)", advanced: true);
        public static readonly ParamDef YRange = Prm.Text("yrange", "y-axis range (min;max, empty = auto)", "Bereich y-Achse (min;max, leer = automatisch)", advanced: true);
        public static readonly ParamDef Grid = Prm.Bool("grid", "Grid", "Gitternetz", true);
        public static readonly ParamDef Box = Prm.Bool("box", "Closed frame", "Geschlossener Rahmen", false);
        public static readonly ParamDef Legend = Prm.Choice("legend", "Legend", "Legende", "auto",
            ("auto", "Automatic", "Automatisch"), ("inside", "Inside", "Innen"), ("right", "Right", "Rechts"), ("none", "None", "Keine"));
        public static readonly ParamDef Limits = Prm.Text("limits", "Limit lines (y values, e.g. 4.5;5.5)", "Grenzlinien (y-Werte, z. B. 4,5;5,5)");
        public static readonly ParamDef Patterns = Prm.Bool("patterns", "Fill patterns (also in colour)", "Füllmuster (auch in Farbe)", false, advanced: true);

        public static readonly I18nText SeriesWord = new("Series", "Reihe");

        /// <summary>Axis title "label (unit)" in English and "label in unit" in German (DIN 461 style).</summary>
        public static string? AxisTitle(SketchObject o, string labelKey, string unitKey, I18nText defLabel, I18nText? defUnit, Lang lang)
        {
            var label = o.Params.TryGetValue(labelKey, out var lv) && lv.Text is not null ? lv.Text.Trim() : defLabel.Get(lang);
            var unit = o.Params.TryGetValue(unitKey, out var uv) && uv.Text is not null ? uv.Text.Trim() : defUnit?.Get(lang) ?? "";
            if (label.Length == 0 && unit.Length == 0) return null;
            if (unit.Length == 0) return label;
            if (label.Length == 0) return lang == Lang.De ? "in " + unit : "(" + unit + ")";
            return lang == Lang.De ? label + " in " + unit : label + " (" + unit + ")";
        }

        public static string? TitleOf(SketchObject o) => o.Str("title", "") is { Length: > 0 } t ? t : null;

        public static void Apply(Frame f, SketchObject o, RenderContext ctx, bool boxDefault = false, string legendDefault = "auto")
        {
            f.Title = TitleOf(o);
            f.Grid = o.Flag("grid", true);
            f.Box = o.Flag("box", boxDefault);
            f.LegendMode = o.Str("legend", legendDefault);
            f.Mono = ChartType.Mono(ctx);
        }

        public static string SeriesName(string? name, int k, Lang lang) => string.IsNullOrWhiteSpace(name) ? SeriesWord.Get(lang) + " " + Fmt.I(k + 1) : name!.Trim();

        /// <summary>Horizontal limit lines (dash-dot) across every panel at the y values of the <c>limits</c> parameter.</summary>
        public static void DrawLimits(Chart c, SketchObject o)
        {
            var values = ChartData.Numbers(o.Str("limits", ""));
            var lines = new List<IReadOnlyList<Vec2>>();
            foreach (var p in c.F.Panels)
                foreach (var v in values)
                {
                    if (!p.Y.Contains(v)) continue;
                    var y = p.Area.Bottom - p.Y.T(v) * p.Area.H;
                    lines.Add(new[] { new Vec2(p.Area.X, y), new Vec2(p.Area.Right, y) });
                }
            var style = new Style { Stroke = Palette.Helper, StrokeWidth = 0.35, Role = LineRole.Center, Dash = DashStyle.DashDot };
            if (Draw.MultiPath(lines, style, ChartTags.Limit) is { } path) c.Lines.Add(path);
        }

        /// <summary>Data entry in a single-line prompt: rows separated by "|", series by "||".</summary>
        public static ContextAction DataInput(SketchObject o, string def)
        {
            var current = o.Str("data", def).Replace("\r\n", "\n").Replace('\r', '\n');
            return ContextAction.WithInput("chart.data", new I18nText("Data…", "Daten…"), IconIds.Text,
                new I18nText("Data rows “x;y” separated by | (new series: ||)", "Datenzeilen „x;y“ getrennt durch | (neue Reihe: ||)"),
                current.Replace("\n", " | "),
                (x, text) => x.WithParam("data", string.Join("\n", (text ?? "").Split('|').Select(l => l.Trim())))) with { Group = "data" };
        }

        public static ContextAction GridToggle(SketchObject o) => Prm.Toggle(o, "chart.grid", "grid", true, new I18nText("Grid", "Gitternetz"));

        public static ContextAction LegendCycle(SketchObject o) => Prm.Cycle(o, "chart.legend", Legend, new I18nText("Legend", "Legende"));
    }
}
