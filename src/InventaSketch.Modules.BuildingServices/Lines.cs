using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.BuildingServices
{
    /// <summary>A medium of a pipe or riser: colour, dash, line width and the short code shown in schematics.</summary>
    internal sealed record Medium(string Value, I18nText Label, I18nText Code, string Color, DashStyle Dash, double Width);

    /// <summary>A kind of air duct (outdoor, supply, extract, exhaust air).</summary>
    internal sealed record AirKind(string Value, I18nText Label, I18nText Code, string Color);

    /// <summary>Media and air kinds shared by lines and box elements.</summary>
    internal static class Media
    {
        /// <summary>
        /// Pipe media. Colours follow the architecture module for heating flow/return, cold and hot water and waste; the
        /// circulation colour is added. Colours and codes are common drawing practice, not a cited standard.
        /// </summary>
        public static readonly Medium[] Pipes =
        {
            new("flow", new I18nText("Heating flow", "Heizung Vorlauf"), new I18nText("F", "VL"), "#c62828", DashStyle.Solid, 0.5),
            new("return", new I18nText("Heating return", "Heizung Rücklauf"), new I18nText("R", "RL"), "#1565c0", DashStyle.Dash, 0.5),
            new("cold", new I18nText("Cold water", "Kaltwasser"), new I18nText("CW", "KW"), "#2e7d32", DashStyle.Solid, 0.5),
            new("hot", new I18nText("Hot water", "Warmwasser"), new I18nText("HW", "WW"), "#ad1457", DashStyle.DashDot, 0.5),
            new("circulation", new I18nText("Hot water circulation", "Zirkulation"), new I18nText("HWC", "Z"), "#6a1b9a", DashStyle.DashDotDot, 0.5),
            new("waste", new I18nText("Waste water", "Schmutzwasser"), new I18nText("W", "SW"), "#6d4c41", DashStyle.Solid, 0.8),
        };

        /// <summary>Air duct kinds with common codes (ODA/SUP/ETA/EHA, AUL/ZUL/ABL/FOL); colours are common practice.</summary>
        public static readonly AirKind[] Air =
        {
            new("supply", new I18nText("Supply air", "Zuluft"), new I18nText("SUP", "ZUL"), "#1565c0"),
            new("extract", new I18nText("Extract air", "Abluft"), new I18nText("ETA", "ABL"), "#b28704"),
            new("outdoor", new I18nText("Outdoor air", "Außenluft"), new I18nText("ODA", "AUL"), "#2e7d32"),
            new("exhaust", new I18nText("Exhaust air", "Fortluft"), new I18nText("EHA", "FOL"), "#6d4c41"),
        };

        public static Medium Pipe(string value) => Pipes.FirstOrDefault(m => m.Value == value) ?? Pipes[0];

        public static AirKind AirOf(string value) => Air.FirstOrDefault(m => m.Value == value) ?? Air[0];

        public static ParamDef PipeParam(string def) => Pm.Choice("medium", "Medium", "Medium", def,
            Pipes.Select(m => (m.Value, m.Label.En, m.Label.De ?? m.Label.En)).ToArray());

        public static ParamDef AirParam(string def) => Pm.Choice("kind", "Air", "Luftart", def,
            Air.Select(m => (m.Value, m.Label.En, m.Label.De ?? m.Label.En)).ToArray());

        public static Style PipeStyle(Medium m, double? width = null) => new() { Stroke = m.Color, StrokeWidth = width ?? m.Width, Dash = m.Dash, Role = LineRole.Visible };
    }

    /// <summary>
    /// Base of the service lines (pipes, ducts) with the shared line language: corners can be inserted and removed, ends
    /// extended, branches docked onto the line's rails (<see cref="Polylines"/>). Every line offers the three views of the
    /// module; the points are page coordinates in every view (the view switch maps them, see <see cref="ServiceViews"/>).
    /// </summary>
    internal abstract class ServiceLineType : ObjectType
    {
        /// <summary>Installations switch plan ↔ section ↔ schematic together (<see cref="ServiceViews"/>).</summary>
        public override IAssemblyViewSwitch? AssemblyViewSwitch => ServiceViews.Rule;

        private static readonly IReadOnlyList<ViewDef> ViewList = new[] { BsViews.PlanView, BsViews.SectionView, BsViews.SchematicView };

        public override GeometryKind Geometry => GeometryKind.Points;
        public override IReadOnlyList<ViewDef> Views => ViewList;
        public override IReadOnlyList<Vec2> DefaultPoints => new[] { new Vec2(-20, 0), new Vec2(20, 0) };
        public override bool InsertablePoints => true;
        public override bool UsesPlanScale => true;

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            var p = Clean(ctx.Doc.ResolvePoints(o));
            return p.Count < 2 ? Array.Empty<Primitive>() : RenderPoints(o, ctx, p);
        }

        protected abstract IReadOnlyList<Primitive> RenderPoints(SketchObject o, RenderContext ctx, IReadOnlyList<Vec2> p);

        /// <summary>Rails along every segment (ids rail0, rail1 … by point index, so branch attachments survive corner edits).</summary>
        public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
        {
            var p = ctx.Doc.ResolvePoints(o);
            for (var i = 0; i + 1 < p.Count; i++)
                if (p[i].DistanceTo(p[i + 1]) > 1e-6) yield return Anchor.Rail(p[i], p[i + 1], "rail" + i.ToString(CultureInfo.InvariantCulture));
        }

        public override Primitive ApplyOverride(Primitive p, Style over) => Bs.Override(p, over);

        /// <summary>Points without consecutive duplicates (a vertical run seen in section collapses to one point).</summary>
        protected static IReadOnlyList<Vec2> Clean(IReadOnlyList<Vec2> p)
        {
            var list = new List<Vec2>(p.Count);
            foreach (var q in p) if (list.Count == 0 || list[list.Count - 1].DistanceTo(q) > 1e-6) list.Add(q);
            return list;
        }

        /// <summary>Middle and unit direction of the longest segment (label position).</summary>
        protected static (Vec2 Mid, Vec2 Dir) Longest(IReadOnlyList<Vec2> p)
        {
            var best = 0; var len = -1.0;
            for (var i = 0; i + 1 < p.Count; i++)
            {
                var l = p[i].DistanceTo(p[i + 1]);
                if (l > len) { len = l; best = i; }
            }
            var d = p[best + 1] - p[best];
            return (Vec2.Lerp(p[best], p[best + 1], 0.5), len > Geo.Eps ? d / len : new Vec2(1, 0));
        }

        /// <summary>Code label beside the longest segment, upright (reads left to right or bottom to top).</summary>
        protected static TextPrim Label(IReadOnlyList<Vec2> p, string text, double size, string color)
        {
            var (mid, dir) = Longest(p);
            if (dir.X < -1e-9 || (Math.Abs(dir.X) < 1e-9 && dir.Y > 0)) dir = -dir;
            var up = new Vec2(dir.Y, -dir.X);
            var at = mid + up * (size * 0.9);
            var rot = Geo.RadToDeg(Math.Atan2(dir.Y, dir.X));
            return new TextPrim(at, text, new TextStyle { Size = size, Color = color }, Math.Abs(rot) < 1e-9 ? 0 : rot) { Tag = BsTags.Label };
        }

        /// <summary>Junction dots where an end is docked onto another line (a tee, <see cref="Polylines.Branch"/>).</summary>
        protected static IEnumerable<Primitive> Junctions(SketchObject o, IReadOnlyList<Vec2> raw, double r, string color)
        {
            if (o.Attach is null) yield break;
            foreach (var i in new[] { 0, raw.Count - 1 })
                if (i >= 0 && i < o.Attach.Count && o.Attach[i]?.AnchorId is { } id && id.StartsWith("rail", StringComparison.Ordinal))
                    yield return Prim.Circle(raw[i].X, raw[i].Y, r, new Style { Stroke = color, StrokeWidth = 0.1, Fill = Fill.Solid(color) }) with { Tag = BsTags.Junction };
        }
    }

    /// <summary>
    /// Pipe (heating flow/return, cold and hot water, circulation, waste) as a polyline: medium colour and dash in every
    /// view; hops where it crosses an earlier pipe; the schematic adds the medium code and the nominal size.
    /// </summary>
    internal sealed class PipeType : ServiceLineType, IValueType, IValueLinks, IValueChecks
    {
        public const string TypeId = "bs.pipe";

        private static readonly ParamDef MediumParam = Media.PipeParam("cold");
        private static readonly ParamDef Dn = new("dn", new I18nText("Nominal size DN", "Nennweite DN"), ParamKind.Number) { Min = 6, Max = 600, Step = 1, Unit = "mm" };
        private static readonly ParamDef Arrow = Pm.Bool("arrow", "Flow arrow", "Fließrichtungspfeil", false);
        private static readonly ParamDef Hops = Pm.Bool("hops", "Hops at crossings", "Brücken an Kreuzungen", true);

        public override string Id => TypeId;
        public override I18nText Name => new("Pipe", "Rohrleitung");
        public override string Category => BsCategories.Plumbing;
        public override IReadOnlyList<string> Keywords => new[]
        {
            "pipe", "pipework", "plumbing", "heating", "flow", "return", "cold water", "hot water", "circulation", "waste",
            "rohr", "rohrleitung", "leitung", "sanitär", "heizung", "vorlauf", "rücklauf", "kaltwasser", "warmwasser", "zirkulation",
            "abwasser", "schmutzwasser", "tga", "trinkwasser",
        };

        public override IReadOnlyList<ParamDef> Parameters => new[]
        {
            MediumParam, Pm.Height(0), Dn, Arrow, Hops, Pm.Scale,
        }.Concat(BsValues.For(TypeId).Params).ToList();

        // ------------------------------------------------------------------ element values (Calc 4, BsValues)

        public IReadOnlyList<VarDef> Variables => BsValues.For(TypeId).Variables;
        public IReadOnlyList<Relation> Relations => BsValues.For(TypeId).Relations;
        public IEnumerable<ValueCheck> ValueChecks => BsValues.For(TypeId).Checks;
        public IEnumerable<BoundRelation> ValueLinks(SketchObject o, RenderContext ctx) => BsValues.Links(o, ctx);

        /// <summary>A pipe drawn over an earlier pipe hops over it; hosts offer to connect there.</summary>
        public override bool HopsAtCrossings => true;
        public override bool DependsOnNeighbours => true;

        public override IReadOnlyList<string> Companions => new[]
        {
            "bs.heat.pump", "bs.heat.mixer", "bs.heat.trv", "bs.heat.radiator", "bs.san.basin", "bs.san.wc", "bs.san.riser",
        };

        /// <summary>The connection medium of a pipe medium value ("flow" → heating flow, "cold" → cold water …).</summary>
        public static string MediumOf(string medium) => medium switch
        {
            "flow" => PortMedia.HeatingFlow,
            "return" => PortMedia.HeatingReturn,
            "hot" => PortMedia.HotWater,
            "circulation" => PortMedia.Circulation,
            "waste" => PortMedia.Waste,
            _ => PortMedia.ColdWater,
        };

        /// <summary>The pipe's medium (build "+" at its free ends, <see cref="Building"/>).</summary>
        public override string? LineMedium(SketchObject line) => MediumOf(line.Str("medium", "cold"));

        private static IReadOnlyList<Vec2> PathOf(SketchObject x, RenderContext ctx) => Clean(ctx.Doc.ResolvePoints(x));

        public override IEnumerable<string> Dependents(SketchObject o, RenderContext ctx)
        {
            var p = PathOf(o, ctx);
            if (p.Count < 2) return Array.Empty<string>();
            return Crossings.Find(ctx.Doc, o, p, x => PathOf(x, ctx), ctx.Registry ?? Owner).Select(c => c.Other).Distinct();
        }

        protected override IReadOnlyList<Primitive> RenderPoints(SketchObject o, RenderContext ctx, IReadOnlyList<Vec2> p)
        {
            var m = Media.Pipe(o.Str("medium", "cold"));
            var view = ViewOf(o);
            var style = Media.PipeStyle(m, view == BsViews.Schematic ? Math.Max(0.5, m.Width) : (double?)null);
            if (o.Flag("arrow", false)) style = style with { ArrowEnd = ArrowHead.Triangle, ArrowSize = ArrowSize.Small };
            var hops = o.Flag("hops", true) ? Crossings.Find(ctx.Doc, o, p, x => PathOf(x, ctx), ctx.Registry ?? Owner).Where(c => c.Above).ToList() : new List<Crossing>();
            Primitive main = hops.Count == 0 ? Prim.Polyline(p, style) : Prim.Path(Crossings.HopPath(p, hops), style);
            var list = new List<Primitive> { main with { Tag = BsTags.Pipe } };
            list.AddRange(Junctions(o, ctx.Doc.ResolvePoints(o), 0.6, m.Color));
            if (view == BsViews.Schematic)
            {
                var code = m.Code.Get(ctx.Lang);
                var dn = o.Num("dn", 0);
                if (dn > 0) code += " DN " + Bs.Mm(dn);
                list.Add(Label(p, code, 2.2, m.Color));
            }
            return list;
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            foreach (var a in Bs.Choice(o, "bs.pipe.medium", MediumParam, new I18nText("Medium", "Medium"))) yield return a;
            yield return Bs.Toggle(o, "bs.pipe.arrow", new I18nText("Flow arrow", "Fließrichtungspfeil"), IconIds.FlowArrow, "arrow", false);
            yield return Bs.Toggle(o, "bs.pipe.hops", new I18nText("Hops at crossings on/off", "Brücken an Kreuzungen an/aus"), IconIds.Hop, "hops", true);
            yield return Bs.HeightAction(o, this, lang);
        }
    }

    /// <summary>
    /// Air duct (supply, extract, outdoor, exhaust air): in plan and section a double line of the duct width at the plan
    /// scale with a centre line; in the schematic a single line with the air code. Branches dock onto the centre line.
    /// </summary>
    internal sealed class DuctType : ServiceLineType, IValueType, IValueLinks, IValueChecks
    {
        public const string TypeId = "bs.duct";

        private static readonly ParamDef KindParam = Media.AirParam("supply");
        private static readonly ParamDef Width = Pm.Num("width", "Duct size (diameter / width)", "Kanalgröße (Durchmesser / Breite)", 160, 50, 2000, "mm", 5);
        private static readonly ParamDef Arrow = Pm.Bool("arrow", "Air flow arrow", "Luftrichtungspfeil", false);

        public override string Id => TypeId;
        public override I18nText Name => new("Air duct", "Luftleitung");
        public override string Category => BsCategories.Ventilation;
        public override IReadOnlyList<string> Keywords => new[]
        {
            "duct", "air duct", "ventilation", "supply air", "extract air", "outdoor air", "exhaust air", "hvac", "spiral duct",
            "luftleitung", "lüftung", "kanal", "lüftungskanal", "zuluft", "abluft", "außenluft", "fortluft", "wickelfalzrohr", "kwl", "tga",
        };

        public override IReadOnlyList<ParamDef> Parameters => new[] { KindParam, Width, Pm.Height(2400), Arrow, Pm.Scale }.Concat(BsValues.For(TypeId).Params).ToList();

        // ------------------------------------------------------------------ element values (Calc 4, BsValues)

        public IReadOnlyList<VarDef> Variables => BsValues.For(TypeId).Variables;
        public IReadOnlyList<Relation> Relations => BsValues.For(TypeId).Relations;
        public IEnumerable<ValueCheck> ValueChecks => BsValues.For(TypeId).Checks;
        public IEnumerable<BoundRelation> ValueLinks(SketchObject o, RenderContext ctx) => BsValues.Links(o, ctx);

        public override IReadOnlyList<string> Companions => new[] { "bs.vent.terminal", "bs.vent.unit" };

        /// <summary>The air the duct carries ("air.supply" …; build "+" at its free ends, <see cref="Building"/>).</summary>
        public override string? LineMedium(SketchObject line) => PortMedia.AirOf(line.Str("kind", "supply"));

        /// <summary>Duct width on the page (mm) at the object's plan scale.</summary>
        public static double PageWidth(SketchObject o, SketchDocument doc) => Math.Max(0.3, o.Num("width", 160) / Bs.ScaleOf(o, doc));

        protected override IReadOnlyList<Primitive> RenderPoints(SketchObject o, RenderContext ctx, IReadOnlyList<Vec2> p)
        {
            var k = Media.AirOf(o.Str("kind", "supply"));
            var view = ViewOf(o);
            var list = new List<Primitive>();
            var arrow = o.Flag("arrow", false);
            if (view == BsViews.Schematic)
            {
                var s = new Style { Stroke = k.Color, StrokeWidth = 0.6, Role = LineRole.Visible };
                if (arrow) s = s with { ArrowEnd = ArrowHead.Triangle, ArrowSize = ArrowSize.Small };
                list.Add(Prim.Polyline(p, s) with { Tag = BsTags.Duct });
                list.Add(Label(p, k.Code.Get(ctx.Lang), 2.2, k.Color));
                return list;
            }
            var h = PageWidth(o, ctx.Doc) / 2;
            var edge = new Style { Stroke = k.Color, StrokeWidth = 0.25, Role = LineRole.Visible };
            var left = Offset(p, h);
            var right = Offset(p, -h);
            list.Add(Prim.Polyline(left, edge) with { Tag = BsTags.Duct });
            list.Add(Prim.Polyline(right, edge) with { Tag = BsTags.Duct });
            list.Add(Prim.Line(left[0], right[0], edge) with { Tag = "cap" });
            list.Add(Prim.Line(left[left.Count - 1], right[right.Count - 1], edge) with { Tag = "cap" });
            var axis = new Style { Stroke = k.Color, StrokeWidth = 0.13, Dash = DashStyle.DashDot, Role = LineRole.Center };
            if (arrow) axis = axis with { ArrowEnd = ArrowHead.Triangle, ArrowSize = ArrowSize.Small, Dash = DashStyle.Solid };
            list.Add(Prim.Polyline(p, axis) with { Tag = "axis" });
            return list;
        }

        /// <summary>Polyline offset by <paramref name="d"/> to the left of the running direction, mitred at corners.</summary>
        internal static List<Vec2> Offset(IReadOnlyList<Vec2> p, double d)
        {
            var res = new List<Vec2>(p.Count);
            for (var i = 0; i < p.Count; i++)
            {
                var din = i > 0 ? (p[i] - p[i - 1]).Normalized() : (p[1] - p[0]).Normalized();
                var dout = i + 1 < p.Count ? (p[i + 1] - p[i]).Normalized() : din;
                var nIn = new Vec2(din.Y, -din.X);
                var nOut = new Vec2(dout.Y, -dout.X);
                var n = (nIn + nOut).Normalized();
                var cos = n.Dot(nOut);
                if (n.Length < Geo.Eps || Math.Abs(cos) < 0.2) { res.Add(p[i] + nOut * d); continue; }
                res.Add(p[i] + n * (d / cos));
            }
            return res;
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            foreach (var a in Bs.Choice(o, "bs.duct.kind", KindParam)) yield return a;
            yield return Bs.Toggle(o, "bs.duct.arrow", new I18nText("Air flow arrow", "Luftrichtungspfeil"), IconIds.FlowArrow, "arrow", false);
            yield return Bs.HeightAction(o, this, lang);
        }
    }
}
