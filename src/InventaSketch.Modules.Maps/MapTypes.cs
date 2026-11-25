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

        /// <summary>
        /// The type's own actions. Views are not repeated here: hosts offer them in their view dropdown, and switching keeps
        /// the drawing scale through the views' design sizes (<see cref="ViewDef.Size"/>, <see cref="ObjectType.WithView"/>).
        /// </summary>
        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
            => Extra is null ? Array.Empty<ContextAction>() : Extra(o, lang);

        public override IEnumerable<string> Dependents(SketchObject o, RenderContext ctx) => MapDeps.Legends(o, ctx);

        public override Primitive ApplyOverride(Primitive p, Style over) => MapDeps.Override(p, over, base.ApplyOverride);
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
    }
}
