using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Energy
{
    /// <summary>An energy carrier of a pipe or flow arrow: colour, single-line dash and the code shown in diagrams.</summary>
    internal sealed record Carrier(string Value, I18nText Label, I18nText Code, string Color, DashStyle Dash);

    /// <summary>Energy carriers. Colours and codes are a drawing convention of this module, not a cited standard.</summary>
    internal static class Carriers
    {
        public static readonly Carrier Electricity = new("electricity", new I18nText("Electricity", "Strom"), new I18nText("el", "el"), "#f2b705", DashStyle.Solid);

        public static readonly Carrier[] Pipes =
        {
            new("hydrogen", new I18nText("Hydrogen", "Wasserstoff"), new I18nText("H₂", "H₂"), "#00897b", DashStyle.DashDot),
            new("gas", new I18nText("Natural gas", "Erdgas"), new I18nText("CH₄", "CH₄"), "#8d6e63", DashStyle.LongDash),
            new("heat", new I18nText("Heat", "Wärme"), new I18nText("Heat", "Wärme"), "#c62828", DashStyle.Solid),
            new("cooling", new I18nText("Cooling", "Kälte"), new I18nText("Cool", "Kälte"), "#1565c0", DashStyle.Dash),
            new("water", new I18nText("Water", "Wasser"), new I18nText("H₂O", "H₂O"), "#29b6f6", DashStyle.DashDotDot),
        };

        public static readonly Carrier[] Flows = new[] { Electricity }.Concat(Pipes).ToArray();

        public static Carrier Of(IReadOnlyList<Carrier> list, string value) => list.FirstOrDefault(c => c.Value == value) ?? list[0];

        public static ParamDef Param(IReadOnlyList<Carrier> list, string def) => Pm.Choice("carrier", "Energy carrier", "Energieträger", def,
            list.Select(c => (c.Value, c.Label.En, c.Label.De ?? c.Label.En)).ToArray());
    }

    /// <summary>
    /// Base of the energy lines (cable, pipe, flow arrow): a polyline in page coordinates whose ends dock on ports and
    /// busbar rails; corners can be inserted and removed, ends extended and branches docked onto its rails
    /// (<see cref="Polylines"/>). Both views of the module are offered so a diagram switches as a whole.
    /// </summary>
    internal abstract class EnergyLineType : ObjectType
    {
        private static readonly IReadOnlyList<ViewDef> ViewList = new[]
        {
            new ViewDef(EnViews.Pictorial, EnViews.PictorialLabel) { Icon = IconIds.Front },
            new ViewDef(EnViews.SingleLine, EnViews.SingleLineLabel) { Icon = IconIds.Symbol },
        };

        public override GeometryKind Geometry => GeometryKind.Points;
        public override IReadOnlyList<ViewDef> Views => ViewList;
        public override IReadOnlyList<Vec2> DefaultPoints => new[] { new Vec2(-15, 0), new Vec2(15, 0) };
        public override bool InsertablePoints => true;

        /// <summary>Hops over earlier lines (of any module) at crossings, unless switched off (<c>hops</c>).</summary>
        internal static readonly ParamDef Hops = Pm.Bool("hops", "Hops at crossings", "Brücken an Kreuzungen", true);

        /// <summary>Cables and pipes hop at crossings (one rule for all line families, <see cref="Crossings"/>); flow arrows do not.</summary>
        public override bool HopsAtCrossings => true;
        public override bool DependsOnNeighbours => HopsAtCrossings;

        /// <summary>The drawn path (routing applied).</summary>
        protected virtual IReadOnlyList<Vec2> PathOf(SketchObject o, RenderContext ctx) => En.Clean(ctx.Doc.ResolvePoints(o));

        /// <summary>Crossings where this line hops (it is drawn over the other line).</summary>
        protected List<Crossing> HopsOf(SketchObject o, RenderContext ctx, IReadOnlyList<Vec2> p)
            => HopsAtCrossings && o.Flag(Hops.Key, true) ? Crossings.Find(ctx.Doc, o, p, x => PathOf(x, ctx), ctx.Registry ?? Owner).Where(c => c.Above).ToList() : new List<Crossing>();

        /// <summary>The line itself: a polyline, with hops where it crosses earlier lines.</summary>
        protected Primitive MainLine(SketchObject o, RenderContext ctx, IReadOnlyList<Vec2> p, Style style)
        {
            var hops = HopsOf(o, ctx, p);
            return hops.Count == 0 ? Prim.Polyline(p, style) : Prim.Path(Crossings.HopPath(p, hops), style);
        }

        /// <summary>Lines crossing this one (their hops depend on where this one runs).</summary>
        public override IEnumerable<string> Dependents(SketchObject o, RenderContext ctx)
        {
            if (!HopsAtCrossings) return Array.Empty<string>();
            var p = PathOf(o, ctx);
            return p.Count < 2 ? Array.Empty<string>() : Crossings.Find(ctx.Doc, o, p, x => PathOf(x, ctx), ctx.Registry ?? Owner).Select(c => c.Other).Distinct();
        }

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            var p = PathOf(o, ctx);
            return p.Count < 2 ? Array.Empty<Primitive>() : RenderPath(o, ctx, p);
        }

        protected abstract IReadOnlyList<Primitive> RenderPath(SketchObject o, RenderContext ctx, IReadOnlyList<Vec2> p);

        /// <summary>Rails along every drawn segment (ids rail0, rail1 …): branches dock anywhere on the line.</summary>
        public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
        {
            var p = PathOf(o, ctx);
            for (var i = 0; i + 1 < p.Count; i++)
                if (p[i].DistanceTo(p[i + 1]) > 1e-6) yield return Anchor.Rail(p[i], p[i + 1], "rail" + i.ToString(CultureInfo.InvariantCulture));
        }

        public override Primitive ApplyOverride(Primitive p, Style over) => En.Override(p, over);

        protected static string LabelText(params string[] parts) => string.Join(" · ", parts.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()));
    }

    /// <summary>
    /// Power cable or line: colour by voltage level (or black), thicker in the pictorial view; optional conductor count
    /// marks, voltage and designation label; free or orthogonal routing; communication (data) lines dashed.
    /// </summary>
    internal sealed class CableType : EnergyLineType
    {
        public const string TypeId = "energy.line.cable";

        internal static readonly ParamDef VoltageParam = Pm.Num("voltage", "Nominal voltage", "Nennspannung", 0.4, 0.01, 1000, "kV", 0.1);
        internal static readonly ParamDef Dc = Pm.Bool("dc", "Direct current", "Gleichstrom", false);
        internal static readonly ParamDef Kind = Pm.Choice("kind", "Kind", "Art", "power", ("power", "Power cable", "Energiekabel"), ("data", "Communication line", "Kommunikationsleitung"));
        internal static readonly ParamDef Conductors = Pm.Int("conductors", "Conductor marks (0 = none)", "Leiterstriche (0 = keine)", 0, 0, 5);
        internal static readonly ParamDef Label = Pm.Text("label", "Cable designation", "Kabelbezeichnung", "");
        internal static readonly ParamDef ShowVoltage = Pm.Bool("showVoltage", "Show voltage", "Spannung anzeigen", false);
        internal static readonly ParamDef Route = Pm.Choice("route", "Routing", "Leitungsführung", "free",
            ("free", "Free (straight)", "Frei (gerade)"), ("orthogonal", "Orthogonal", "Orthogonal")).WithOptionIcons(IconIds.ForRoute);

        public override string Id => TypeId;
        public override I18nText Name => new("Cable / line", "Kabel / Leitung");
        public override string Category => EnCategories.Lines;
        public override IReadOnlyList<string> Keywords => new[]
        {
            "cable", "line", "power line", "feeder", "connection", "overhead line", "dc cable", "data line", "single-line",
            "kabel", "leitung", "energiekabel", "verbindung", "freileitung", "gleichstromkabel", "datenleitung", "einlinienschaltbild",
        };

        public override IReadOnlyList<ParamDef> Parameters => new[] { VoltageParam, Dc, Pm.LevelColors, Kind, Conductors, Label, ShowVoltage, Route, Hops };

        public override IReadOnlyList<string> Companions => new[] { "energy.grid.busbar", "energy.grid.breaker", "energy.grid.transformer", "energy.grid.meter" };

        public static string ColorOf(SketchObject o)
        {
            if (o.S(Kind) == "data") return "#455a64";
            return o.B(Pm.LevelColors) ? Voltage.Color(Voltage.Of(o.Clamped(VoltageParam), o.B(Dc))) : "#000000";
        }

        protected override IReadOnlyList<Vec2> PathOf(SketchObject o, RenderContext ctx)
        {
            var raw = ctx.Doc.ResolvePoints(o);
            var p = En.Clean(raw);
            if (p.Count < 2 || o.S(Route) != "orthogonal") return p;
            return En.Clean(En.Orthogonal(p, En.ExitHorizontal(ctx.Doc, o, 0), En.ExitHorizontal(ctx.Doc, o, raw.Count - 1)));
        }

        protected override IReadOnlyList<Primitive> RenderPath(SketchObject o, RenderContext ctx, IReadOnlyList<Vec2> p)
        {
            var pictorial = ViewOf(o) == EnViews.Pictorial;
            var data = o.S(Kind) == "data";
            var color = ColorOf(o);
            var width = data ? 0.3 : pictorial ? 0.9 : 0.5;
            var style = new Style { Stroke = color, StrokeWidth = width, Role = LineRole.Visible, Dash = data ? DashStyle.Dash : DashStyle.Solid };
            var list = new List<Primitive> { MainLine(o, ctx, p, style) with { Tag = EnTags.Cable } };
            list.AddRange(En.Junctions(ctx.Doc, o, ctx.Doc.ResolvePoints(o), pictorial ? 0.9 : 0.6, color));
            var n = data ? 0 : o.I(Conductors);
            if (n > 0) list.AddRange(Ticks(p, n, color));
            var text = LabelText(o.S(Label), !data && o.B(ShowVoltage) ? Fmt.Kv(o.Clamped(VoltageParam), ctx.Lang, o.B(Dc)) : "");
            if (text.Length > 0) list.Add(En.LineLabel(p, text, 2.2, color, width / 2 + (n > 0 ? 1.2 : 0.4)));
            return list;
        }

        /// <summary>Conductor count marks: short oblique strokes across the middle of the longest segment.</summary>
        internal static IEnumerable<Primitive> Ticks(IReadOnlyList<Vec2> p, int n, string color)
        {
            var (mid, dir) = En.Longest(p);
            var nrm = dir.Perp;
            var s = new Style { Stroke = color, StrokeWidth = 0.25, Role = LineRole.Visible };
            const double gap = 0.9, half = 1.1;
            for (var i = 0; i < n; i++)
            {
                var c = mid + dir * ((i - (n - 1) / 2.0) * gap);
                var a = c + (nrm - dir * 0.6).Normalized() * half;
                var b = c - (nrm - dir * 0.6).Normalized() * half;
                yield return Prim.Line(a, b, s) with { Tag = EnTags.Tick };
            }
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            yield return new ContextAction("energy.route.next", Pm.OptionLabel(Route, o.S(Route) == "free" ? "orthogonal" : "free"), IconIds.Cycle,
                x => x.WithParam(Route.Key, x.S(Route) == "free" ? "orthogonal" : "free")) { Group = "route", ParamKeys = new[] { Route.Key } };
            yield return Act.Number(o, "energy.voltage.set", new I18nText("Nominal voltage…", "Nennspannung…"), VoltageParam, lang);
            yield return Act.Toggle(o, "energy.voltage.show", new I18nText("Show voltage", "Spannung anzeigen"), ShowVoltage, IconIds.Text, "label");
            yield return Act.Step(o, "energy.conductors.more", new I18nText("One conductor mark more", "Ein Leiterstrich mehr"), Conductors, 1);
            yield return Act.Step(o, "energy.conductors.fewer", new I18nText("One conductor mark fewer", "Ein Leiterstrich weniger"), Conductors, -1);
            yield return Act.Toggle(o, "energy.levelcolors", new I18nText("Colour by voltage level", "Farbe nach Spannungsebene"), Pm.LevelColors);
            yield return ContextAction.WithInput("energy.cable.label", new I18nText("Cable designation…", "Kabelbezeichnung…"), IconIds.Text,
                Label.Label, o.S(Label), (x, t) => x.WithParam(Label.Key, (t ?? "").Trim())) with { Group = "label", ParamKeys = new[] { Label.Key } };
        }
    }

    /// <summary>
    /// Pipeline of a non-electric energy carrier (hydrogen, natural gas, heat, cooling, water): a coloured tube in the
    /// pictorial view, a thin coloured line with the carrier's dash and code in the single-line view.
    /// </summary>
    internal sealed class PipeType : EnergyLineType
    {
        public const string TypeId = "energy.line.pipe";

        internal static readonly ParamDef CarrierParam = Carriers.Param(Carriers.Pipes, "hydrogen");
        internal static readonly ParamDef Label = Pm.Text("label", "Label", "Beschriftung", "");
        internal static readonly ParamDef ShowCode = Pm.Bool("showCode", "Show carrier code", "Kürzel anzeigen", true);

        public override string Id => TypeId;
        public override I18nText Name => new("Energy carrier pipe", "Rohrleitung (Energieträger)");
        public override string Category => EnCategories.Lines;
        public override IReadOnlyList<string> Keywords => new[]
        {
            "pipe", "pipeline", "hydrogen pipe", "gas pipe", "district heating", "heat pipe", "cooling", "water",
            "rohrleitung", "leitung", "wasserstoffleitung", "gasleitung", "fernwärme", "wärmeleitung", "kälteleitung", "wasser",
        };

        public override IReadOnlyList<ParamDef> Parameters => new[] { CarrierParam, Label, ShowCode, Hops };
        public override IReadOnlyList<string> Companions => new[] { "energy.h2.electrolyser", "energy.h2.tank", "energy.h2.fuelcell", "energy.heat.storage" };

        protected override IReadOnlyList<Primitive> RenderPath(SketchObject o, RenderContext ctx, IReadOnlyList<Vec2> p)
        {
            var c = Carriers.Of(Carriers.Pipes, o.S(CarrierParam));
            var pictorial = ViewOf(o) == EnViews.Pictorial;
            var list = new List<Primitive>();
            double width;
            if (pictorial)
            {
                width = 1.3;
                list.Add(MainLine(o, ctx, p, new Style { Stroke = c.Color, StrokeWidth = width, Role = LineRole.Visible }) with { Tag = EnTags.Pipe });
                list.Add(MainLine(o, ctx, p, new Style { Stroke = Pal.White, StrokeWidth = 0.35, Opacity = 0.55, Role = LineRole.Thin }) with { Tag = "highlight" });
            }
            else
            {
                width = 0.5;
                list.Add(MainLine(o, ctx, p, new Style { Stroke = c.Color, StrokeWidth = width, Dash = c.Dash, Role = LineRole.Visible }) with { Tag = EnTags.Pipe });
            }
            list.AddRange(En.Junctions(ctx.Doc, o, ctx.Doc.ResolvePoints(o), pictorial ? 1.0 : 0.6, c.Color));
            var text = LabelText(!pictorial && o.B(ShowCode) ? c.Code.Get(ctx.Lang) : "", o.S(Label));
            if (text.Length > 0) list.Add(En.LineLabel(p, text, 2.2, c.Color, width / 2 + 0.4));
            return list;
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            yield return Act.Cycle(o, "energy.carrier.next", CarrierParam) with { Icon = IconIds.Medium };
            yield return Act.Toggle(o, "energy.code.show", new I18nText("Show carrier code", "Kürzel anzeigen"), ShowCode, IconIds.Text, "label");
        }
    }

    /// <summary>
    /// Energy flow arrow for slides: a band of the given weight with an arrow head in the carrier colour (pictorial) or a
    /// thin line with a filled head (single-line), with an optional label such as "12 MWh/a".
    /// </summary>
    internal sealed class FlowType : EnergyLineType
    {
        /// <summary>Flow arrows are annotations, not lines: they do not hop.</summary>
        public override bool HopsAtCrossings => false;

        public const string TypeId = "energy.line.flow";

        internal static readonly ParamDef CarrierParam = Carriers.Param(Carriers.Flows, "electricity");
        internal static readonly ParamDef Weight = Pm.Num("weight", "Band width", "Bandbreite", 2.5, 0.3, 20, "mm", 0.5);
        internal static readonly ParamDef Label = Pm.Text("label", "Label", "Beschriftung", "");

        public override string Id => TypeId;
        public override I18nText Name => new("Energy flow arrow", "Energiefluss-Pfeil");
        public override string Category => EnCategories.Lines;
        public override IReadOnlyList<string> Keywords => new[]
        {
            "energy flow", "flow arrow", "power flow", "sankey", "arrow", "electricity", "heat", "hydrogen",
            "energiefluss", "energieflusspfeil", "leistungsfluss", "pfeil", "strom", "wärme", "wasserstoff",
        };

        public override IReadOnlyList<ParamDef> Parameters => new[] { CarrierParam, Weight, Label };

        protected override IReadOnlyList<Primitive> RenderPath(SketchObject o, RenderContext ctx, IReadOnlyList<Vec2> p)
        {
            var c = Carriers.Of(Carriers.Flows, o.S(CarrierParam));
            var pictorial = ViewOf(o) == EnViews.Pictorial;
            var w = pictorial ? o.Clamped(Weight) : 0.5;
            var list = new List<Primitive>();
            var end = p[p.Count - 1];
            var last = p[p.Count - 2];
            var segLen = last.DistanceTo(end);
            var d = (end - last) / segLen;
            var nl = new Vec2(d.Y, -d.X);
            var headLen = Math.Min(pictorial ? Math.Max(w * 1.4, 2.0) : 2.2, segLen * 0.8);
            var headHalf = pictorial ? w / 2 + Math.Max(w * 0.5, 1.0) : 0.9;
            var baseP = end - d * headLen;
            var shaft = p.Take(p.Count - 1).Concat(new[] { baseP }).ToList();
            var fill = new Style { Stroke = c.Color, StrokeWidth = 0.1, Fill = Fill.Solid(c.Color), Role = LineRole.Visible };
            if (pictorial)
            {
                var left = En.Offset(shaft, w / 2);
                var right = En.Offset(shaft, -w / 2);
                var poly = new List<Vec2>(left) { baseP + nl * headHalf, end, baseP - nl * headHalf };
                for (var i = right.Count - 1; i >= 0; i--) poly.Add(right[i]);
                list.Add(Prim.Polygon(poly, fill) with { Tag = EnTags.Flow });
            }
            else
            {
                list.Add(Prim.Polyline(shaft, new Style { Stroke = c.Color, StrokeWidth = w, Role = LineRole.Visible }) with { Tag = EnTags.Flow });
                list.Add(Prim.Polygon(new[] { end, baseP + nl * headHalf, baseP - nl * headHalf }, fill) with { Tag = "head" });
            }
            var text = o.S(Label).Trim();
            if (text.Length > 0) list.Add(En.LineLabel(p, text, 2.4, c.Color, w / 2 + 0.5));
            return list;
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            yield return Act.Cycle(o, "energy.carrier.next", CarrierParam) with { Icon = IconIds.Medium };
            yield return new ContextAction("energy.weight.more", new I18nText("Wider band", "Breiteres Band"), IconIds.Increase,
                x => x.WithParam(Weight.Key, Math.Min(20, x.Clamped(Weight) * 1.5))) { Group = "weight", ParamKeys = new[] { Weight.Key } };
            yield return new ContextAction("energy.weight.less", new I18nText("Narrower band", "Schmaleres Band"), IconIds.Decrease,
                x => x.WithParam(Weight.Key, Math.Max(0.3, x.Clamped(Weight) / 1.5))) { Group = "weight", ParamKeys = new[] { Weight.Key } };
            yield return ContextAction.WithInput("energy.flow.label", new I18nText("Label…", "Beschriftung…"), IconIds.Text,
                Label.Label, o.S(Label), (x, t) => x.WithParam(Label.Key, (t ?? "").Trim())) with { Group = "label", ParamKeys = new[] { Label.Key } };
        }
    }
}
