using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Automotive
{
    /// <summary>One representation (view) of an automotive box type.</summary>
    internal sealed record AutoView
    {
        public AutoView(string id, I18nText label, string icon, double w, double h, Action<Pen, SketchObject> draw)
        {
            Id = id; Label = label; Icon = icon; Design = new Size2(w, h); Draw = draw;
        }

        public string Id { get; }
        public I18nText Label { get; }
        public string Icon { get; }
        /// <summary>Design box (centred at 0,0): the drawing is authored for this size and stretched to the object size.</summary>
        public Size2 Design { get; }
        public Action<Pen, SketchObject> Draw { get; }
        /// <summary>Fluid views are drawn directly in actual millimetres (roads: lane widths, dash lengths).</summary>
        public bool Fluid { get; init; }
        /// <summary>Optional parameter-dependent design size (e.g. traffic light with or without pole).</summary>
        public Func<SketchObject, Size2>? DesignOf { get; init; }
        /// <summary>View-specific anchors in local coordinates (drawn with the same pen mapping).</summary>
        public Func<Pen, SketchObject, IEnumerable<Anchor>>? Anchors { get; init; }

        public Size2 DesignFor(SketchObject o) => DesignOf?.Invoke(o) ?? Design;
    }

    /// <summary>Standard view definitions with their abstract pictograms.</summary>
    internal static class Views
    {
        public static readonly I18nText Top = new("Top view", "Draufsicht");
        public static readonly I18nText Side = new("Side view", "Seitenansicht");
        public static readonly I18nText Front = new("Front view", "Vorderansicht");
        public static readonly I18nText Rear = new("Rear view", "Rückansicht");
        public static readonly I18nText Symbol = new("Symbol", "Symbol");
        public static readonly I18nText Iso = new("Isometric", "Isometrisch");

        public static AutoView TopV(double w, double h, Action<Pen, SketchObject> d) => new("top", Top, IconIds.Top, w, h, d);
        public static AutoView SideV(double w, double h, Action<Pen, SketchObject> d) => new("side", Side, IconIds.Side, w, h, d);
        public static AutoView FrontV(double w, double h, Action<Pen, SketchObject> d) => new("front", Front, IconIds.Front, w, h, d);
        public static AutoView RearV(double w, double h, Action<Pen, SketchObject> d) => new("rear", Rear, IconIds.Rear, w, h, d);
        public static AutoView SymbolV(double w, double h, Action<Pen, SketchObject> d) => new("symbol", Symbol, IconIds.Symbol, w, h, d);
    }

    /// <summary>
    /// Data-driven box type used for all automotive symbols: views (each with its own drawing and anchors), optional
    /// parameters and context actions. Every view is drawn inside the object box.
    /// </summary>
    internal sealed class AutoType : ObjectType
    {
        private readonly string _id;
        private readonly I18nText _name;
        private readonly string _category;
        private readonly IReadOnlyList<string> _keywords;
        private readonly IReadOnlyList<AutoView> _views;
        private readonly IReadOnlyList<ViewDef> _viewDefs;

        public AutoType(string id, I18nText name, string category, string[] keywords, params AutoView[] views)
        {
            if (views.Length == 0) throw new ArgumentException("At least one view is required.", nameof(views));
            _id = id; _name = name; _category = category; _keywords = keywords; _views = views;
            _viewDefs = views.Select(v => new ViewDef(v.Id, v.Label) { Icon = v.Icon }).ToList();
        }

        public override string Id => _id;
        public override I18nText Name => _name;
        public override string Category => _category;
        public override IReadOnlyList<string> Keywords => _keywords;
        public override IReadOnlyList<ViewDef> Views => _viewDefs;
        public override Size2 DefaultSize => _views[0].Design;
        /// <summary>The design size of the object's view for its parameters (lanes widen roads, curves and junctions).</summary>
        public override Size2? NaturalSize(SketchObject o) => Spec(o).DesignFor(o);
        public override IReadOnlyList<ParamDef> Parameters => Params;
        public override bool PreferUniformScale => Uniform;
        public override bool HasText => TextEditable;

        public IReadOnlyList<ParamDef> Params { get; init; } = Array.Empty<ParamDef>();
        public bool Uniform { get; init; } = true;
        public bool TextEditable { get; init; }
        /// <summary>Exposes a Hub anchor at the box centre (vehicles: snaps onto lane centrelines).</summary>
        public bool CenterHub { get; init; }
        /// <summary>Label used by the "next view" action (e.g. "Next vehicle view").</summary>
        public I18nText? NextViewLabel { get; init; }
        /// <summary>Type-specific context actions in addition to the view switches.</summary>
        public Func<SketchObject, IEnumerable<ContextAction>>? Extra { get; init; }

        public IReadOnlyList<AutoView> ViewSpecs => _views;

        public AutoView Spec(string? view) => _views.FirstOrDefault(v => v.Id == view) ?? _views[0];
        public AutoView Spec(SketchObject o) => Spec(ViewOf(o));

        public static Pen PenFor(SketchObject o, AutoView v) => new(o.Size, v.Fluid ? o.Size : v.DesignFor(o));

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            var v = Spec(o);
            var pen = PenFor(o, v);
            v.Draw(pen, o);
            return pen.Items;
        }

        public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
        {
            var v = Spec(o);
            if (CenterHub) yield return Anchor.Point(AnchorRole.Hub, Vec2.Zero, "center");
            if (v.Anchors is null) yield break;
            foreach (var a in v.Anchors(PenFor(o, v), o)) yield return a;
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            var current = ViewOf(o);
            if (_views.Count > 1)
            {
                var idx = _views.ToList().FindIndex(v => v.Id == current);
                var next = _views[(Math.Max(0, idx) + 1) % _views.Count];
                yield return new ContextAction("view.next", NextViewLabel ?? new I18nText("Next view", "Nächste Ansicht"), IconIds.Cycle, x => SwitchView(x, NextView(x)))
                {
                    Group = "view",
                };
                foreach (var v in _views)
                {
                    var id = v.Id;
                    yield return new ContextAction("view." + id, v.Label, v.Icon, x => SwitchView(x, id)) { IsActive = current == id, Group = "view" };
                }
            }
            if (Extra is not null)
                foreach (var a in Extra(o)) yield return a;
        }

        private string NextView(SketchObject o)
        {
            var idx = _views.ToList().FindIndex(v => v.Id == ViewOf(o));
            return _views[(Math.Max(0, idx) + 1) % _views.Count].Id;
        }

        /// <summary>
        /// Switches the view and keeps the drawing scale: the new size is the new view's design size times the current
        /// mean scale factor (a vehicle keeps its scale when going from top view to side or front view).
        /// </summary>
        public SketchObject SwitchView(SketchObject o, string viewId)
        {
            var from = Spec(o);
            var to = Spec(viewId);
            if (from.Id == to.Id) return o with { View = to.Id };
            var fd = from.DesignFor(o);
            var k = Math.Sqrt(Math.Max(o.Size.W, 0.01) * Math.Max(o.Size.H, 0.01) / (fd.W * fd.H));
            var td = to.DesignFor(o);
            return o with { View = to.Id, Size = new Size2(td.W * k, td.H * k) };
        }
    }

    /// <summary>Base class of point-geometry automotive types; renders in page coordinates from resolved points.</summary>
    internal abstract class AutoPointType : ObjectType
    {
        private readonly string _id;
        private readonly I18nText _name;
        private readonly string _category;
        private readonly string[] _keywords;

        protected AutoPointType(string id, I18nText name, string category, params string[] keywords)
        {
            _id = id; _name = name; _category = category; _keywords = keywords;
        }

        public override string Id => _id;
        public override I18nText Name => _name;
        public override string Category => _category;
        public override IReadOnlyList<string> Keywords => _keywords;
        public override GeometryKind Geometry => GeometryKind.Points;

        protected virtual int MinPoints => 2;

        public sealed override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            var pts = ctx.Doc.ResolvePoints(o);
            if (pts.Count < MinPoints) return Array.Empty<Primitive>();
            return RenderPoints(o, ctx, pts);
        }

        protected abstract IReadOnlyList<Primitive> RenderPoints(SketchObject o, RenderContext ctx, IReadOnlyList<Vec2> p);
    }
}
