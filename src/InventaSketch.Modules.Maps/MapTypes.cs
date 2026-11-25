using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Maps
{
    /// <summary>One representation (view) of a map box type.</summary>
    internal sealed record MapView
    {
        public MapView(string id, I18nText label, string icon, double w, double h, Action<Pen, SketchObject, RenderContext> draw)
        {
            Id = id; Label = label; Icon = icon; Design = new Size2(w, h); Draw = draw;
        }

        public string Id { get; }
        public I18nText Label { get; }
        public string Icon { get; }
        /// <summary>Design box (centred at 0,0): the drawing is authored for this size and stretched to the object size.</summary>
        public Size2 Design { get; }
        public Action<Pen, SketchObject, RenderContext> Draw { get; }
        /// <summary>View-specific anchors in local coordinates (same pen mapping as the drawing).</summary>
        public Func<Pen, SketchObject, IEnumerable<Anchor>>? Anchors { get; init; }
    }

    /// <summary>Standard view definitions of the module with their abstract pictograms.</summary>
    internal static class MapViews
    {
        public static readonly I18nText PlanLabel = new("Map", "Karte");
        public static readonly I18nText SymbolLabel = new("Symbol", "Symbol");
        public static readonly I18nText PinLabel = new("Pin", "Stecknadel");

        public static MapView Plan(double w, double h, Action<Pen, SketchObject, RenderContext> d) => new("plan", PlanLabel, IconIds.Plan, w, h, d);
        public static MapView Symbol(double w, double h, Action<Pen, SketchObject, RenderContext> d) => new("symbol", SymbolLabel, IconIds.Symbol, w, h, d);
        /// <summary>Map pin: <see cref="IconIds"/> has no pin pictogram yet, the "detailed" view icon stands in for it.</summary>
        public static MapView Pin(double w, double h, Action<Pen, SketchObject, RenderContext> d) => new("pin", PinLabel, IconIds.Pin, w, h, d);
    }

    /// <summary>Shared behaviour of all map types.</summary>
    internal static class MapDeps
    {
        public const string LegendId = "map.legend";

        /// <summary>Legends on the page of <paramref name="o"/> that list the page content automatically (they redraw when map objects change).</summary>
        public static IEnumerable<string> Legends(SketchObject o, RenderContext ctx)
        {
            var page = ctx.Doc.PageOf(o.Id);
            if (page is null) yield break;
            foreach (var x in ctx.Doc.ObjectsOn(page))
                if (x.Id != o.Id && x.Type == LegendId && Legend.IsAuto(x)) yield return x.Id;
        }

        /// <summary>True if the document or the render context asks for black-and-white output.</summary>
        public static bool Mono(RenderContext ctx) => ctx.Monochrome || ctx.Doc.Settings.Monochrome;

        /// <summary>
        /// Style override that leaves primitives with fixed colours (white halos, glyphs on badges, shields) alone except for
        /// the opacity, so a recoloured route keeps its white casing and a recoloured badge keeps a readable glyph.
        /// </summary>
        public static Primitive Override(Primitive p, Style over, Func<Primitive, Style, Primitive> standard)
        {
            if (p.Tag is MapTags.Halo or MapTags.Inlay or MapTags.Glyph or MapTags.Shield or MapTags.Sample)
                return over.Opacity is double op ? Prim.Restyle(p, s => s with { Opacity = op }) : p;
            return standard(p, over);
        }
    }

    /// <summary>
    /// Data-driven box type used for all map symbols: views (each with its own drawing and anchors), optional parameters
    /// and context actions. Every view is drawn inside the object box, stretched with free scaling.
    /// </summary>
    internal class MapBoxType : ObjectType
    {
        /// <summary>Symbols, not physical parts: never in the bill of materials.</summary>
        public override bool IsPart => false;

        private readonly string _id;
        private readonly I18nText _name;
        private readonly string _category;
        private readonly IReadOnlyList<string> _keywords;
        private readonly IReadOnlyList<MapView> _views;
        private readonly IReadOnlyList<ViewDef> _viewDefs;

        public MapBoxType(string id, I18nText name, string category, string[] keywords, params MapView[] views)
        {
            if (views.Length == 0) throw new ArgumentException("At least one view is required.", nameof(views));
            _id = id; _name = name; _category = category; _keywords = keywords; _views = views;
            _viewDefs = views.Select(v => new ViewDef(v.Id, v.Label) { Icon = v.Icon, Size = views.Length > 1 ? v.Design : null }).ToList();
        }

        public override string Id => _id;
        public override I18nText Name => _name;
        public override string Category => _category;
        public override IReadOnlyList<string> Keywords => _keywords;
        public override IReadOnlyList<ViewDef> Views => _viewDefs;
        public override Size2 DefaultSize => _views[0].Design;
        public override IReadOnlyList<ParamDef> Parameters => Params;
        public override bool PreferUniformScale => Uniform;
        public override bool HasText => TextEditable;
        public override IReadOnlyList<string> Companions => CompanionIds;
        public override bool DependsOnNeighbours => NeedsNeighbours;

        /// <summary>The rendering reads other objects of the page (legend).</summary>
        public bool NeedsNeighbours { get; init; }
        public IReadOnlyList<ParamDef> Params { get; init; } = Array.Empty<ParamDef>();
        public bool Uniform { get; init; } = true;
        public bool TextEditable { get; init; }
        public IReadOnlyList<string> CompanionIds { get; init; } = Array.Empty<string>();
        /// <summary>Type-specific context actions in addition to the view switches.</summary>
        public Func<SketchObject, Lang, IEnumerable<ContextAction>>? Extra { get; init; }

        public IReadOnlyList<MapView> ViewSpecs => _views;

        public MapView Spec(string? view) => _views.FirstOrDefault(v => v.Id == view) ?? _views[0];
        public MapView Spec(SketchObject o) => Spec(ViewOf(o));

        public static Pen PenFor(SketchObject o, MapView v) => new(o.Size, v.Design);

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            var v = Spec(o);
            var pen = PenFor(o, v);
            v.Draw(pen, o, ctx);
            return pen.Items;
        }

        public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
        {
            var v = Spec(o);
            if (v.Anchors is null) return Array.Empty<Anchor>();
            return v.Anchors(PenFor(o, v), o).ToList();
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            var current = ViewOf(o);
            if (_views.Count > 1)
                foreach (var v in _views)
                {
                    var id = v.Id;
                    yield return new ContextAction("view." + id, v.Label, v.Icon, x => SwitchView(x, id)) { IsActive = current == id, Group = "view" };
                }
            if (Extra is not null)
                foreach (var a in Extra(o, lang)) yield return a;
        }

        public override IEnumerable<string> Dependents(SketchObject o, RenderContext ctx) => MapDeps.Legends(o, ctx);

        public override Primitive ApplyOverride(Primitive p, Style over) => MapDeps.Override(p, over, base.ApplyOverride);

        /// <summary>Switches the view and keeps the drawing scale (the new view's design size times the current mean scale factor).</summary>
        public SketchObject SwitchView(SketchObject o, string viewId)
        {
            var from = Spec(o);
            var to = Spec(viewId);
            if (from.Id == to.Id) return o with { View = to.Id };
            var k = Math.Sqrt(Math.Max(o.Size.W, 0.01) * Math.Max(o.Size.H, 0.01) / (from.Design.W * from.Design.H));
            return o with { View = to.Id, Size = new Size2(to.Design.W * k, to.Design.H * k) };
        }
    }

    /// <summary>Base class of point-geometry map types; renders in page coordinates from the resolved points.</summary>
    internal abstract class MapPointType : ObjectType
    {
        private readonly string _id;
        private readonly I18nText _name;
        private readonly string _category;
        private readonly string[] _keywords;

        protected MapPointType(string id, I18nText name, string category, params string[] keywords)
        {
            _id = id; _name = name; _category = category; _keywords = keywords;
        }

        public override string Id => _id;
        public override I18nText Name => _name;
        public override string Category => _category;
        public override IReadOnlyList<string> Keywords => _keywords;
        public override GeometryKind Geometry => GeometryKind.Points;
        public override IReadOnlyList<ViewDef> Views { get; } = new[] { new ViewDef("plan", MapViews.PlanLabel) { Icon = IconIds.Plan } };

        protected virtual int MinPoints => 2;
        /// <summary>True for closed outlines (regions): point editing considers the closing segment.</summary>
        protected virtual bool ClosedOutline => false;
        public override bool IsClosed(SketchObject o) => ClosedOutline;

        public sealed override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            var pts = Geom.Dedupe(ctx.Doc.ResolvePoints(o));
            if (pts.Count < MinPoints) return Array.Empty<Primitive>();
            return RenderPoints(o, ctx, pts);
        }

        protected abstract IReadOnlyList<Primitive> RenderPoints(SketchObject o, RenderContext ctx, IReadOnlyList<Vec2> p);

        public override IEnumerable<string> Dependents(SketchObject o, RenderContext ctx) => MapDeps.Legends(o, ctx);

        public override Primitive ApplyOverride(Primitive p, Style over) => MapDeps.Override(p, over, base.ApplyOverride);

        /// <summary>"Add waypoint" / "Remove waypoint" actions (point editing without a selected vertex).</summary>
        protected IEnumerable<ContextAction> PointActions(SketchObject o, I18nText add, I18nText remove)
        {
            var count = o.Points?.Count ?? 0;
            yield return new ContextAction("map.point.add", add, IconIds.AddPoint, x => PointEdit.Insert(x, ClosedOutline)) { Group = "points" };
            yield return new ContextAction("map.point.remove", remove, IconIds.RemovePoint, x => PointEdit.Remove(x, ClosedOutline, ClosedOutline ? 3 : 2))
            {
                Group = "points",
                IsEnabled = count > (ClosedOutline ? 3 : 2),
            };
        }
    }

    /// <summary>Point editing of point objects: insert a point in the longest segment, remove the least significant one.</summary>
    internal static class PointEdit
    {
        /// <summary>Inserts the midpoint of the longest segment (attachments keep their points).</summary>
        public static SketchObject Insert(SketchObject o, bool closed)
        {
            if (o.Points is not { Count: >= 2 } pts) return o;
            var n = pts.Count;
            var segs = closed && n >= 3 ? n : n - 1;
            int best = 0; double bestLen = -1;
            for (var i = 0; i < segs; i++)
            {
                var l = pts[i].DistanceTo(pts[(i + 1) % n]);
                if (l > bestLen + 1e-9) { bestLen = l; best = i; }
            }
            var mid = Vec2.Lerp(pts[best], pts[(best + 1) % n], 0.5);
            var list = pts.ToList();
            list.Insert(best + 1, mid);
            IReadOnlyList<Attachment?>? att = null;
            if (o.Attach is not null)
            {
                var a = o.Attach.ToList();
                while (a.Count < n) a.Add(null);
                a.Insert(best + 1, null);
                att = a;
            }
            return o with { Points = list, Attach = att };
        }

        /// <summary>
        /// Removes the point whose removal changes the outline least (smallest triangle with its neighbours; end points of
        /// open lines stay). A just inserted midpoint is therefore removed again. Unchanged at <paramref name="min"/> points.
        /// </summary>
        public static SketchObject Remove(SketchObject o, bool closed, int min)
        {
            if (o.Points is not { } pts || pts.Count <= min) return o;
            var n = pts.Count;
            int best = -1; double bestArea = double.MaxValue;
            for (var i = closed ? 0 : 1; i < (closed ? n : n - 1); i++)
            {
                var a = pts[(i - 1 + n) % n]; var b = pts[i]; var c = pts[(i + 1) % n];
                var area = Math.Abs((b - a).Cross(c - a)) / 2;
                if (area < bestArea - 1e-9) { bestArea = area; best = i; }
            }
            if (best < 0) return o;
            var list = pts.ToList();
            list.RemoveAt(best);
            IReadOnlyList<Attachment?>? att = null;
            if (o.Attach is not null)
            {
                var a = o.Attach.ToList();
                while (a.Count < n) a.Add(null);
                a.RemoveAt(best);
                att = a.Any(x => x is not null) ? a : null;
            }
            return o with { Points = list, Attach = att };
        }
    }
}
