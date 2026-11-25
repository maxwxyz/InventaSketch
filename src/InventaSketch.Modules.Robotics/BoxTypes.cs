using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Robotics
{
    /// <summary>One view of a box symbol: its definition, design size, drawing and anchors.</summary>
    internal sealed class SymView
    {
        public SymView(ViewDef def, Size2 design, Action<Pad, SketchObject> draw, Func<Pad, SketchObject, IEnumerable<Anchor>>? anchors = null)
        {
            Def = def with { Size = design }; Design = design; Draw = draw; Anchors = anchors;
        }

        public ViewDef Def { get; }
        public Size2 Design { get; }
        public Action<Pad, SketchObject> Draw { get; }
        public Func<Pad, SketchObject, IEnumerable<Anchor>>? Anchors { get; }
    }

    /// <summary>
    /// Data-driven box symbol of the robotics module: views with their own design size (the drawing is stretched to the
    /// object box), optional parameters, anchors and context actions. Symbols without views have a single drawing.
    /// </summary>
    internal sealed class RoboBoxType : ObjectType
    {
        private readonly string _id;
        private readonly I18nText _name;
        private readonly string _category;
        private readonly string[] _keywords;
        private readonly IReadOnlyList<SymView> _views;

        public RoboBoxType(string id, I18nText name, string category, string[] keywords, params SymView[] views)
        {
            if (views.Length == 0) throw new ArgumentException("At least one drawing is required.", nameof(views));
            _id = id; _name = name; _category = category; _keywords = keywords; _views = views;
        }

        public override string Id => _id;
        public override I18nText Name => _name;
        public override string Category => _category;
        public override IReadOnlyList<string> Keywords => _keywords;
        public override IReadOnlyList<ViewDef> Views => NoViews ? Array.Empty<ViewDef>() : _views.Select(v => v.Def).ToList();
        public override Size2 DefaultSize => _views[0].Design;
        public override IReadOnlyList<ParamDef> Parameters => Params;
        public override bool PreferUniformScale => Uniform;
        public override IReadOnlyList<string> Companions => CompanionIds;
        public override bool IsPart => Part;

        /// <summary>True for symbols with a single drawing (coordinate frame, TCP marker): no view switch.</summary>
        public bool NoViews { get; init; }
        public IReadOnlyList<ParamDef> Params { get; init; } = Array.Empty<ParamDef>();
        public bool Uniform { get; init; }
        public bool Part { get; init; } = true;
        public IReadOnlyList<string> CompanionIds { get; init; } = Array.Empty<string>();
        public Func<SketchObject, IEnumerable<ContextAction>>? Extra { get; init; }

        public SymView Spec(SketchObject o) => _views.FirstOrDefault(v => v.Def.Id == ViewOf(o)) ?? _views[0];

        public Pad PadFor(SketchObject o, Lang lang) => new(o.Size, Spec(o).Design, lang);

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            var pad = PadFor(o, ctx.Lang);
            Spec(o).Draw(pad, o);
            return pad.Items;
        }

        public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
        {
            var spec = Spec(o);
            return spec.Anchors is null ? Array.Empty<Anchor>() : spec.Anchors(PadFor(o, ctx.Lang), o).ToList();
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang) => Extra?.Invoke(o) ?? Array.Empty<ContextAction>();

        public override Primitive ApplyOverride(Primitive p, Style over) => Rb.Override(p, over);

        public static Anchor Mount(Pad p, double x, double y, string id) => Anchor.Point(AnchorRole.Mount, p.V(x, y), id);
        public static Anchor Port(Pad p, double x, double y, string id) => Anchor.Point(AnchorRole.Port, p.V(x, y), id);
        public static Anchor Rail(Pad p, double x0, double y0, double x1, double y1, string id) => Anchor.Rail(p.V(x0, y0), p.V(x1, y1), id);
    }
}
