using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Diagrams
{
    /// <summary>UML relationship kinds.</summary>
    public enum RelationKind { Association, Directed, Aggregation, Composition, Generalization, Realization, Dependency, Link }

    /// <summary>
    /// UML relationship connector. The decoration sits at the end point (drawn from the part/child/client to the
    /// whole/parent/supplier): directed association (open arrow), aggregation (hollow diamond), composition (filled
    /// diamond), generalization (hollow triangle), realization (dashed, hollow triangle), dependency (dashed, open arrow),
    /// link (dashed, no head; note anchors). Middle label = object text; multiplicities and role names at both ends.
    /// </summary>
    public sealed class UmlRelationType : DiagConnectorType
    {
        public UmlRelationType() : base("diag.uml.relation", new I18nText("Relationship (UML)", "Beziehung (UML)"), DiagCategories.Relations,
            "uml", "relationship", "beziehung", "relation", "connector", "verbinder", "association", "assoziation", "aggregation", "composition",
            "komposition", "generalization", "generalisierung", "vererbung", "inheritance", "realization", "realisierung", "dependency", "abhängigkeit",
            "multiplicity", "multiplizität") { }

        public override IReadOnlyList<Vec2> DefaultPoints => new[] { new Vec2(-20, 0), new Vec2(20, 0) };

        public static readonly (string, string, string)[] KindOptions =
        {
            ("association", "Association", "Assoziation"), ("directed", "Directed association", "Gerichtete Assoziation"),
            ("aggregation", "Aggregation", "Aggregation"), ("composition", "Composition", "Komposition"),
            ("generalization", "Generalization", "Generalisierung"), ("realization", "Realization", "Realisierung"),
            ("dependency", "Dependency", "Abhängigkeit"), ("link", "Link (note anchor)", "Verbindung (Notizanker)"),
        };

        public override IReadOnlyList<ParamDef> Parameters => new[]
        {
            P.Choice("kind", "Kind", "Art", "association", KindOptions),
            P.Text("startMult", "Start multiplicity", "Multiplizität Anfang", null),
            P.Text("endMult", "End multiplicity", "Multiplizität Ende", null),
            P.Text("startRole", "Start role", "Rolle Anfang", null),
            P.Text("endRole", "End role", "Rolle Ende", null),
            P.Route(),
            P.TextSize(),
        };

        public static RelationKind KindOf(SketchObject o) => Enum.TryParse<RelationKind>(o.Str("kind", "association"), true, out var k) ? k : RelationKind.Association;

        protected override DiagEnd StartEnd(SketchObject o) => DiagEnd.None;

        protected override DiagEnd EndEnd(SketchObject o) => KindOf(o) switch
        {
            RelationKind.Directed or RelationKind.Dependency => DiagEnd.Open,
            RelationKind.Aggregation => DiagEnd.HollowDiamond,
            RelationKind.Composition => DiagEnd.FilledDiamond,
            RelationKind.Generalization or RelationKind.Realization => DiagEnd.Hollow,
            _ => DiagEnd.None,
        };

        protected override bool IsDashed(SketchObject o) => KindOf(o) is RelationKind.Realization or RelationKind.Dependency or RelationKind.Link;

        protected override void Labels(List<Primitive> list, SketchObject o, RenderContext ctx, List<Vec2> path)
        {
            base.Labels(list, o, ctx, path);
            var size = LabelSize(o);
            list.AddRange(EndLabel(o.Str("startMult", ""), path, true, StartEnd(o), true, size));
            list.AddRange(EndLabel(o.Str("startRole", ""), path, true, StartEnd(o), false, size));
            list.AddRange(EndLabel(o.Str("endMult", ""), path, false, EndEnd(o), true, size));
            list.AddRange(EndLabel(o.Str("endRole", ""), path, false, EndEnd(o), false, size));
        }

        /// <summary>Reverses the direction; multiplicities and roles stay at their (geometric) class ends.</summary>
        public static SketchObject Swap(SketchObject o) => D.SwapParams(D.SwapParams(D.Reverse(o), "startMult", "endMult"), "startRole", "endRole");

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            foreach (var (value, en, de) in KindOptions)
                yield return D.SetChoice(o, "diag.rel." + value, new I18nText(en, de), IconIds.ArrowEnd, "kind", value, "association", "kind");
            yield return new ContextAction("diag.swap", new I18nText("Swap direction", "Richtung umkehren"), IconIds.Swap, Swap) { Group = "direction" };
            yield return RouteAction(o);
        }
    }

    /// <summary>
    /// ER connector in crow's-foot notation: each end shows a cardinality (one, one and only one, zero or one, many,
    /// one or many, zero or many). Identifying relationships are solid, non-identifying ones dashed. Middle label =
    /// object text (verb phrase).
    /// </summary>
    public sealed class CrowsFootType : DiagConnectorType
    {
        public CrowsFootType() : base("diag.er.connector", new I18nText("Crow's foot connector (ER)", "Krähenfuß-Verbinder (ER)"), DiagCategories.Er,
            "er", "erd", "entity relationship", "crow's foot", "crows foot", "krähenfuß", "cardinality", "kardinalität", "one to many",
            "eins zu viele", "many to many", "relationship", "beziehung", "connector", "verbinder") { }

        public override IReadOnlyList<Vec2> DefaultPoints => new[] { new Vec2(-20, 0), new Vec2(20, 0) };

        public override IReadOnlyList<ParamDef> Parameters => new[]
        {
            P.Choice("startEnd", "Start cardinality", "Kardinalität Anfang", "onlyOne", P.CrowOptions),
            P.Choice("endEnd", "End cardinality", "Kardinalität Ende", "zeroOrMany", P.CrowOptions),
            P.Bool("identifying", "Identifying (solid line)", "Identifizierend (durchgezogen)", true),
            P.Route(),
            P.TextSize(),
        };

        protected override DiagEnd StartEnd(SketchObject o) => D.ParseEnd(o.Str("startEnd", "onlyOne"), DiagEnd.OnlyOne);
        protected override DiagEnd EndEnd(SketchObject o) => D.ParseEnd(o.Str("endEnd", "zeroOrMany"), DiagEnd.ZeroOrMany);
        protected override bool IsDashed(SketchObject o) => !o.Flag("identifying", true);

        /// <summary>Next crow's-foot notation in the cycle (none and UML heads restart the cycle).</summary>
        public static DiagEnd Next(DiagEnd e)
        {
            var i = Array.IndexOf(D.CrowCycle, e);
            return D.CrowCycle[(i + 1) % D.CrowCycle.Length];
        }

        private ContextAction Cycle(string id, I18nText label, string key, DiagEnd fallback)
            => new(id, label, IconIds.Cycle, x => x.WithParam(key, D.EndName(Next(D.ParseEnd(x.Str(key, ""), fallback))))) { Group = "ends" };

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            yield return Cycle("diag.crow.start", new I18nText("Next start cardinality", "Nächste Kardinalität Anfang"), "startEnd", DiagEnd.OnlyOne);
            yield return Cycle("diag.crow.end", new I18nText("Next end cardinality", "Nächste Kardinalität Ende"), "endEnd", DiagEnd.ZeroOrMany);
            yield return new ContextAction("diag.swap", new I18nText("Swap ends", "Enden tauschen"), IconIds.Swap,
                x => D.SwapParams(x.WithParam("startEnd", D.EndName(StartEnd(x))).WithParam("endEnd", D.EndName(EndEnd(x))), "startEnd", "endEnd")) { Group = "direction" };
            yield return D.Toggle(o, "diag.crow.identifying", new I18nText("Identifying", "Identifizierend"), "identifying", true);
            yield return RouteAction(o);
        }
    }
}
