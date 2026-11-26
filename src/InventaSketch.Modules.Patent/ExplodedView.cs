using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using InventaSketch.Modules.Annotation;

namespace InventaSketch.Modules.Patent
{
    /// <summary>
    /// Assembly axis of an exploded view: a thin dashed line in the gap between two parts pulled apart along their common
    /// axis, showing where they go together. Both ends dock on the parts and follow them. Point 0 and the last point are
    /// the ends. The "chain" form draws a dash-dot centre line (role <see cref="LineRole.Center"/>, so the USPTO
    /// preflight reports it like any centre line).
    /// </summary>
    public sealed class AssemblyAxisType : AnnPointType
    {
        public AssemblyAxisType() : base(PatentIds.AssemblyAxis, new I18nText("Assembly axis (exploded view)", "Montageachse (Explosionsdarstellung)"), PatentIds.Category,
            "exploded", "explosion", "assembly", "axis", "explosionsdarstellung", "montage", "achse", "dashed", "gestrichelt") { }

        public override IReadOnlyList<Vec2> DefaultPoints => new[] { new Vec2(0, -15), new Vec2(0, 15) };

        public override IReadOnlyList<ParamDef> Parameters => new[]
        {
            new ParamDef("form", new I18nText("Line", "Linie"), ParamKind.Choice)
            {
                Default = "dashed",
                Options = new[]
                {
                    new ParamOption("dashed", new I18nText("Dashed", "Gestrichelt")) { Icon = IconIds.LineDash },
                    new ParamOption("chain", new I18nText("Dash-dot", "Strichpunkt")) { Icon = IconIds.LineDashDot },
                },
            },
        };

        public static Style StyleOf(SketchObject o)
            => o.Str("form", "dashed") == "chain" ? Style.CenterLine : Style.ThinLine with { Dash = DashStyle.Dash };

        protected override IReadOnlyList<Primitive> RenderPoints(SketchObject o, RenderContext ctx, IReadOnlyList<Vec2> p)
            => new Primitive[] { new LinePrim(p[0], p[p.Count - 1], StyleOf(o)) { Tag = AnnTags.Main } };

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
            => ActionChoices.FromParam(o, "patent.axis.form", Parameters[0], null, "style", null);
    }

    /// <summary>Exploded views: assembly axes between parts that were pulled apart.</summary>
    public static class ExplodedViews
    {
        public const string AxisActionId = "patent.numeral.axis";

        /// <summary>
        /// Adds an assembly axis between two parts: on the line through their centres, from the edge of one part's
        /// bounding box to the facing edge of the other (centre to centre when the boxes overlap). Both ends are attached.
        /// </summary>
        public static (SketchDocument Doc, SketchObject Axis) AddAxis(Registry registry, SketchDocument doc, string pageId, string partA, string partB, string form = "dashed")
        {
            var a = doc.Get(partA) ?? throw new KeyNotFoundException(partA);
            var b = doc.Get(partB) ?? throw new KeyNotFoundException(partB);
            if (a.IsPointGeometry || b.IsPointGeometry) throw new ArgumentException("Assembly axes join box objects.");
            var ba = a.Bounds(); var bb = b.Bounds();
            Vec2 ca = ba.Center, cb = bb.Center;
            Vec2 pa = ca, pb = cb;
            if (!ba.Intersects(bb) && ca.DistanceTo(cb) > Geo.Eps)
            {
                pa = Exit(ba, ca, cb - ca);
                pb = Exit(bb, cb, ca - cb);
            }
            var type = registry.Type(PatentIds.AssemblyAxis) ?? throw new InvalidOperationException("Figures module is not registered.");
            var axis = type.Create(pa) with { Points = new[] { pa, pb } };
            axis = axis.WithParam("form", form);
            doc = Edits.Add(doc, pageId, axis);
            doc = Edits.AttachPoint(doc, axis.Id, 0, a.Id, pa);
            doc = Edits.AttachPoint(doc, axis.Id, 1, b.Id, pb);
            return (doc, doc.Get(axis.Id)!);
        }

        /// <summary>Where the ray from <paramref name="c"/> (inside <paramref name="r"/>) along <paramref name="d"/> leaves the box.</summary>
        private static Vec2 Exit(Rect r, Vec2 c, Vec2 d)
        {
            var tx = Math.Abs(d.X) < Geo.Eps ? double.MaxValue : (r.W / 2) / Math.Abs(d.X);
            var ty = Math.Abs(d.Y) < Geo.Eps ? double.MaxValue : (r.H / 2) / Math.Abs(d.Y);
            return c + d * Math.Min(tx, ty);
        }

        /// <summary>True if an assembly axis already joins the two objects.</summary>
        public static bool Joined(SketchDocument doc, string a, string b)
            => doc.Objects.Values.Any(o => o.Type == PatentIds.AssemblyAxis && o.Attach is { Count: >= 2 } att
                && att[0] is { } x && att[att.Count - 1] is { } y && ((x.ObjectId == a && y.ObjectId == b) || (x.ObjectId == b && y.ObjectId == a)));

        /// <summary>
        /// The part on the same page closest to <paramref name="part"/> (centre distance) that is not yet joined to it by an
        /// assembly axis; null if there is none.
        /// </summary>
        public static SketchObject? NearestPart(Registry registry, SketchDocument doc, SketchObject part)
        {
            var page = doc.PageOf(part.Id);
            if (page is null) return null;
            var c = part.Bounds().Center;
            return doc.ObjectsOn(page).Where(o => o.Id != part.Id && PartsList.IsPart(registry, o) && !Joined(doc, part.Id, o.Id))
                .OrderBy(o => o.Bounds().Center.DistanceTo(c)).FirstOrDefault();
        }

        /// <summary>
        /// "Assembly axis to the nearest part" on a numeral attached to a part (exploded views): joins that part with the
        /// nearest part not yet joined to it.
        /// </summary>
        public static ContextAction AxisAction(SketchObject numeral, RenderContext ctx, Registry? registry)
        {
            registry = ctx.Registry ?? registry;
            var part = ReferenceNumerals.TargetOf(ctx.Doc, numeral);
            var enabled = registry is not null && part is not null && PartsList.IsPart(registry, part) && NearestPart(registry, ctx.Doc, part) is not null;
            var reg = registry;
            return ContextAction.InDocument(AxisActionId, new I18nText("Assembly axis to the nearest part", "Montageachse zum nächsten Teil"), IconIds.Explode, (d, o, c) =>
            {
                var r = c.Registry ?? reg;
                if (r is null || ReferenceNumerals.TargetOf(d, o) is not { } p || d.PageOf(p.Id) is not { } page || NearestPart(r, d, p) is not { } other) return d;
                return AddAxis(r, d, page.Id, p.Id, other.Id).Doc;
            }) with { IsEnabled = enabled, Group = "exploded" };
        }
    }
}
