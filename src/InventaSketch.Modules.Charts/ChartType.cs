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

        /// <summary>
        /// Every drawn part (charts v2): each curve, its markers, bars, axes, ticks, tick labels, axis titles, grid, legend …
        /// The key is the primitive tag (<see cref="ChartTags"/>); legend pieces form one part.
        /// </summary>
        public override IEnumerable<PartInfo> Parts(SketchObject o, RenderContext ctx)
        {
            var c = Compose(o, ctx);
            var lines = c.F.Legend.Where(e => !e.Area).Select(e => e.Text).ToList();
            var areas = c.F.Legend.Where(e => e.Area).Select(e => e.Text).ToList();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var list = new List<PartInfo>();
            foreach (var p in Prim.Flatten(c.Render()))
            {
                if (p.Tag is not { Length: > 0 } tag) continue;
                var key = ChartTags.Is(tag, ChartTags.Legend) ? ChartTags.Legend : tag;
                if (seen.Add(key)) list.Add(new PartInfo(key, PartLabel(key, lines, areas, ctx.Lang), Kind(key)));
            }
            return list.OrderBy(p => Order(p.Kind)).ToList();
        }

        private static string Kind(string key) => key.IndexOf('.') is var i and > 0 ? key.Substring(0, i) : key;

        private static int Order(string kind) => kind switch
        {
            ChartTags.Series => 0, ChartTags.Marker => 1, ChartTags.Bar => 2, ChartTags.ValueLabel => 3, ChartTags.Axis => 4,
            ChartTags.TickLabel => 5, ChartTags.Tick => 6, ChartTags.AxisTitle => 7, "grid" => 8, ChartTags.Legend => 9, ChartTags.Title => 10,
            _ => 11,
        };

        private static readonly I18nText CurveWord = new("Curve", "Kurve");

        /// <summary>Display name of a part: series by their legend text, axes by direction and panel.</summary>
        internal static I18nText PartLabel(string key, IReadOnlyList<string> lines, IReadOnlyList<string> areas, Lang lang)
        {
            var seg = key.Split('.');
            int? n = seg.Length > 1 && int.TryParse(seg[seg.Length - 1], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : null;
            string Name(IReadOnlyList<string> names, I18nText word, Lang l) => n is int k && k < names.Count ? names[k] : word.Get(l) + " " + Fmt.I((n ?? 0) + 1);
            I18nText Named(IReadOnlyList<string> names, I18nText word) => new(Name(names, word, Lang.En), Name(names, word, Lang.De));
            I18nText Prefixed(string en, string de, IReadOnlyList<string> names, I18nText word) => new(en + " – " + Name(names, word, Lang.En), de + " – " + Name(names, word, Lang.De));
            // Axis direction and panel: "axis.y.2" → "y axis 2", "axis.y2" → secondary y axis.
            var dir = seg.Length > 1 ? seg[1] : "";
            var panel = seg.Length > 2 ? " " + seg[2] : "";
            I18nText Dir(string en, string de) => dir switch
            {
                "x" => new I18nText("x " + en + panel, de + " x" + panel),
                "y" => new I18nText("y " + en + panel, de + " y" + panel),
                "y2" => new I18nText("Secondary y " + en + panel, de + " y rechts" + panel),
                "box" => new I18nText("Frame" + panel, "Rahmen" + panel),
                _ => new I18nText(en, de),
            };
            return Kind(key) switch
            {
                ChartTags.Series => Named(lines, CurveWord),
                ChartTags.Marker => Prefixed("Markers", "Marker", lines, CurveWord),
                ChartTags.Bar => Named(areas, Common.SeriesWord),
                ChartTags.ValueLabel => Prefixed("Value labels", "Wertbeschriftung", areas, Common.SeriesWord),
                ChartTags.Axis => Dir("axis", "Achse"),
                ChartTags.Tick => Dir("ticks", "Teilstriche"),
                ChartTags.TickLabel => Dir("tick labels", "Skalenbeschriftung"),
                ChartTags.AxisTitle => Dir("axis title", "Achsentitel"),
                "grid" => key == ChartTags.GridMinor ? new I18nText("Minor grid", "Nebengitter") : new I18nText("Major grid", "Hauptgitter"),
                ChartTags.Zero => new I18nText("Zero line", "Nulllinie"),
                ChartTags.Limit => new I18nText("Limit lines", "Grenzlinien"),
                ChartTags.Legend => new I18nText("Legend", "Legende"),
                ChartTags.Title => new I18nText("Title", "Titel"),
                ChartTags.Band => new I18nText("Tolerance band", "Toleranzband"),
                ChartTags.Contour => new I18nText("Contour lines", "Höhenlinien"),
                ChartTags.ContourLabel => new I18nText("Contour labels", "Höhenlinienbeschriftung"),
                ChartTags.Envelope => new I18nText("Envelope", "Hüllkurve"),
                ChartTags.Helper => new I18nText("Helper lines", "Hilfslinien"),
                ChartTags.Point => new I18nText("Operating point", "Betriebspunkt"),
                ChartTags.Note => new I18nText("Notes", "Anmerkungen"),
                _ => new I18nText(key),
            };
        }

        /// <summary>
        /// Chart actions that concern a part, taken from the element actions: data, style and smoothing for curves and bars,
        /// the scale for an axis, the grid toggle for the grid, the legend position for the legend.
        /// </summary>
        protected override IEnumerable<ContextAction> OwnPartActions(SketchObject o, string part, RenderContext ctx)
        {
            var kind = Kind(part);
            var dir = part.Split('.') is { Length: > 1 } s ? s[1] : "";
            string[] ids = kind switch
            {
                ChartTags.Series or ChartTags.Marker or ChartTags.Bar or ChartTags.ValueLabel => new[] { "chart.data", "chart.style", "chart.smooth", "chart.mode", "chart.orient", "chart.values" },
                ChartTags.Axis or ChartTags.Tick or ChartTags.TickLabel or ChartTags.AxisTitle => dir == "x" ? new[] { "chart.xscale" } : dir.StartsWith("y", StringComparison.Ordinal) ? new[] { "chart.yscale" } : new[] { "chart.arrows" },
                "grid" => new[] { "chart.grid" },
                ChartTags.Legend => new[] { "chart.legend" },
                _ => Array.Empty<string>(),
            };
            if (ids.Length == 0) return Array.Empty<ContextAction>();
            return Actions(o, ctx).Where(a => ids.Contains(a.Id)).ToList();
        }
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
