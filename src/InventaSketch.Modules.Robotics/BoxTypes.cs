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
        /// <summary>Grip of a 0 … 1 parameter in this view (gripper opening, carriage position), or null.</summary>
        public OpenGrip? Open { get; init; }
    }

    /// <summary>
    /// A grip that moves a 0 … 1 parameter (<see cref="Key"/>, default "open": the gripper opening) along a straight
    /// stroke: design point <see cref="Closed"/> at 0, moving by <see cref="PerOpen"/> per unit (a jaw or finger tip that
    /// follows "open", the carriage of a linear axis that follows "pos").
    /// </summary>
    internal sealed record OpenGrip(Vec2 Closed, Vec2 PerOpen)
    {
        public const string OpenKey = "open";
        /// <summary>Snap of the dragged value (5 %).</summary>
        public const double Snap = 0.05;

        public string Key { get; init; } = OpenKey;
        public double Default { get; init; } = 0.6;
        /// <summary>Grip label before the value ("Gripper opening: 60 %").</summary>
        public I18nText Label { get; init; } = new("Gripper opening", "Greiferöffnung");

        public Vec2 At(double open) => Closed + PerOpen * open;
    }

    /// <summary>
    /// Data-driven box symbol of the robotics module: views with their own design size (the drawing is stretched to the
    /// object box), optional parameters, anchors and context actions. Symbols without views have a single drawing.
    /// </summary>
    internal sealed class RoboBoxType : ObjectType, IValueType
    {
        private readonly string _id;
        private readonly I18nText _name;
        private readonly string _category;
        private readonly string[] _keywords;
        private readonly IReadOnlyList<SymView> _views;
        private IReadOnlyList<ParamDef>? _parameters;

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
        public override IReadOnlyList<ParamDef> Parameters => _parameters ??= HasMass ? Params.Concat(RoboValues.MassParams).ToList() : Params;
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

        /// <summary>The element has a mass (value "mass", kg): tools, sensors, tool changers, workpieces.</summary>
        public bool HasMass { get; init; }

        /// <summary>
        /// The element docks at a robot flange with its directed mount "flange" (tools, sensors, tool changers). It also
        /// docks with "+" on the tool-side mount "tool" of a tool changer or sensor: while it is a candidate of a build port
        /// (<see cref="IsCandidate"/>) its flange mount is offered under the id "tool" as well. Placed parts keep unique
        /// mount ids, as <see cref="Coupling"/> finds docked parts by id and build ports are named after them.
        /// </summary>
        public bool ToolSide { get; init; }

        public IReadOnlyList<VarDef> Variables => HasMass ? RoboValues.MassVars : Array.Empty<VarDef>();
        public IReadOnlyList<Relation> Relations => Array.Empty<Relation>();

        public SymView Spec(SketchObject o) => _views.FirstOrDefault(v => v.Def.Id == ViewOf(o)) ?? _views[0];

        public Pad PadFor(SketchObject o, Lang lang) => new(o.Size, Spec(o).Design, lang);

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            var pad = PadFor(o, ctx.Lang);
            Spec(o).Draw(pad, o);
            return pad.Items;
        }

        /// <summary>The view's own anchors (local coordinates), without the tool-side alias.</summary>
        public List<Anchor> OwnAnchors(SketchObject o, Lang lang)
        {
            var spec = Spec(o);
            return spec.Anchors is null ? new List<Anchor>() : spec.Anchors(PadFor(o, lang), o).ToList();
        }

        public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
        {
            var list = OwnAnchors(o, ctx.Lang);
            if (ToolSide && IsCandidate(o, ctx))
            {
                var i = list.FindIndex(a => a.Role == AnchorRole.Mount && a.Id == "flange" && a.Direction is not null);
                if (i >= 0) list.Insert(i + 1, list[i] with { Id = RoboValues.ToolMount });
            }
            return list;
        }

        /// <summary>
        /// True while the part is a candidate for a build port: not in the document, or as <see cref="Building"/> probes it
        /// in a temporary copy of the document – centred on the page origin (the item filter) or with a robot flange or a
        /// tool-side mount of another part well inside its box (centred on the port). A part docked on such a mount has it
        /// on its box edge and a free part has none, so placed parts keep unique mount ids. (Core matches build ports by
        /// mount id only; ToolChainTests fail if its probing changes.)
        /// </summary>
        private static bool IsCandidate(SketchObject o, RenderContext ctx)
        {
            if (ctx.Doc.PageOf(o.Id) is not { } page) return true;
            if (o.Transform.X == 0 && o.Transform.Y == 0) return true;
            if (ctx.Registry is not { } reg) return false;
            var reach = Math.Sqrt(o.Size.W * o.Size.W + o.Size.H * o.Size.H) / 2;
            foreach (var x in ctx.Doc.ObjectsOn(page))
            {
                if (x.Id == o.Id || x.IsPointGeometry) continue;
                var near = reach + Math.Sqrt(x.Size.W * x.Size.W + x.Size.H * x.Size.H) / 2;
                if (new Vec2(x.Transform.X - o.Transform.X, x.Transform.Y - o.Transform.Y).Length > near) continue;
                IEnumerable<Anchor> local = reg.Type(x.Type) switch
                {
                    RoboBoxType rb => rb.OwnAnchors(x, ctx.Lang),
                    RobotArmType arm => arm.Anchors(x, ctx),
                    _ => Array.Empty<Anchor>(),
                };
                var m = x.Matrix();
                foreach (var a in local)
                {
                    if (a.Kind != AnchorKind.Point || a.Role != AnchorRole.Mount || a.Direction is null || a.Id is not ("flange" or RoboValues.ToolMount)) continue;
                    var (u, v) = o.ToBox(m.Apply(a.P));
                    if (Math.Abs(u) < 0.4 && Math.Abs(v) < 0.4) return true;
                }
            }
            return false;
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang) => Extra?.Invoke(o) ?? Array.Empty<ContextAction>();

        private static double Value(SketchObject o, OpenGrip g) => Geo.Clamp(o.Num(g.Key, g.Default), 0, 1);

        /// <summary>The grip of the view's 0 … 1 parameter (gripper opening, carriage position), dragged along its stroke.</summary>
        public override IEnumerable<DragHandle> DragHandles(SketchObject o, RenderContext ctx)
        {
            if (Spec(o).Open is not { } g) yield break;
            var pad = PadFor(o, ctx.Lang);
            var open = Value(o, g);
            var value = ": " + Rb.Fmt(Math.Round(open * 100)) + " %";
            var local = new DragHandle(g.Key, pad.V(g.At(open)), Math.Abs(g.PerOpen.X) >= Math.Abs(g.PerOpen.Y) ? DragConstraint.Horizontal : DragConstraint.Vertical,
                new I18nText(g.Label.En + value, (g.Label.De ?? g.Label.En) + value));
            yield return Dragging.ToPage(local, o);
        }

        /// <summary>The pointer's travel along the stroke as the value (snapped to 5 %, 0 … 1).</summary>
        public override SketchObject MoveHandle(SketchObject o, string key, Vec2 pagePos, RenderContext ctx)
        {
            if (Spec(o).Open is not { } g || key != g.Key) return o;
            var pad = PadFor(o, ctx.Lang);
            var local = o.Matrix().Invert().Apply(pagePos);
            var design = new Vec2(local.X / pad.Sx, local.Y / pad.Sy);
            var d2 = g.PerOpen.Dot(g.PerOpen);
            if (d2 < 1e-12) return o;
            return o.WithParam(g.Key, Geo.Clamp(Dragging.Snap((design - g.Closed).Dot(g.PerOpen) / d2, OpenGrip.Snap), 0, 1));
        }

        public override Primitive ApplyOverride(Primitive p, Style over) => Rb.Override(p, over);

        public static Anchor Mount(Pad p, double x, double y, string id) => Anchor.Point(AnchorRole.Mount, p.V(x, y), id);
        public static Anchor Port(Pad p, double x, double y, string id) => Anchor.Point(AnchorRole.Port, p.V(x, y), id);
        public static Anchor Rail(Pad p, double x0, double y0, double x1, double y1, string id) => Anchor.Rail(p.V(x0, y0), p.V(x1, y1), id);

        /// <summary>
        /// A carrier mount without an id (AGV deck, carriage of a linear axis), directed up in the drawing: a robot base docked
        /// on it (its own directed base mount facing it) follows when the carrier moves or turns (<see cref="Coupling"/>). No
        /// id, so it offers no "+" of its own.
        /// </summary>
        public static Anchor Carrier(Pad p, double x, double y) => Anchor.Directed(AnchorRole.Mount, p.V(x, y), new Vec2(0, -1));
    }
}
