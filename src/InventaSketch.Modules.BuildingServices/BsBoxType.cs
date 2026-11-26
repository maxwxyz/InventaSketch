using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.BuildingServices
{
    /// <summary>How an element is mounted; decides its mount anchors and the reading of the mounting height.</summary>
    public enum Mounting
    {
        /// <summary>Stands on the floor (WC, shower tray, heat pump, storage cylinder).</summary>
        Floor,
        /// <summary>Hangs on a wall (radiator, washbasin, boiler, outlets).</summary>
        Wall,
        /// <summary>Under the ceiling (detectors, air terminals).</summary>
        Ceiling,
        /// <summary>Embedded in the floor (underfloor heating, floor drain).</summary>
        InFloor,
        /// <summary>Sits in a pipe run (pumps, valves): no mount, only ports.</summary>
        Inline,
    }

    /// <summary>A connection point in local box millimetres with its stable id (the same id in every view).</summary>
    internal readonly record struct PortAt(Vec2 P, string Id);

    /// <summary>
    /// Box element of the building services module with three views of the same installation element:
    /// <c>plan</c> (top view, the wall is at the top edge of the box), <c>section</c> (seen from the front at its mounting
    /// height) and <c>schematic</c> (riser/flow diagram symbol). Plan and the elevation views share the x extent (the box
    /// width is kept when the view changes); the height of the box follows the preferred size of the target view.
    /// </summary>
    internal sealed partial class BsBoxType : ObjectType, IValueType, IValueLinks, IValueChecks
    {
        /// <summary>Installations switch plan ↔ section ↔ schematic together (<see cref="ServiceViews"/>).</summary>
        public override IAssemblyViewSwitch? AssemblyViewSwitch => ServiceViews.Rule;

        private readonly string _id;
        private readonly I18nText _name;
        private readonly string _category;
        private readonly string[] _keywords;
        private readonly Size2 _plan, _section, _schematic;
        private readonly DrawFn _drawPlan, _drawSection, _drawSchematic;
        private readonly IReadOnlyList<ViewDef> _views;

        public BsBoxType(string id, I18nText name, string category, Size2 plan, Size2 section, Size2 schematic,
            DrawFn drawPlan, DrawFn drawSection, DrawFn drawSchematic, params string[] keywords)
        {
            _id = id; _name = name; _category = category; _keywords = keywords;
            _plan = plan; _section = section; _schematic = schematic;
            _drawPlan = drawPlan; _drawSection = drawSection; _drawSchematic = drawSchematic;
            _views = new[]
            {
                BsViews.PlanView with { Size = plan },
                BsViews.SectionView with { Size = section },
                BsViews.SchematicView with { Size = schematic },
            };
        }

        public override string Id => _id;
        public override I18nText Name => _name;
        public override string Category => _category;
        public override IReadOnlyList<string> Keywords => _keywords;
        public override Size2 DefaultSize => _plan;
        public override IReadOnlyList<ViewDef> Views => _views;
        public override bool PreferUniformScale => Uniform;
        public override bool UsesPlanScale => true;
        public override IReadOnlyList<string> Companions => CompanionIds;

        public override IReadOnlyList<ParamDef> Parameters =>
            Params.Concat(new[] { Pm.Height(DefaultHeight), Pm.Scale }).Concat(ValueSpec.Params).Concat(CurveParams).ToList();

        // ------------------------------------------------------------------ element values (Calc 4, BsValues)

        /// <summary>Variables, relations, value parameters, port roles and checks of this type (empty for elements without values).</summary>
        internal BsValues.Spec ValueSpec => BsValues.For(_id);

        public IReadOnlyList<VarDef> Variables => ValueSpec.Variables;
        public IReadOnlyList<Relation> Relations => ValueSpec.Relations;
        public IEnumerable<ValueCheck> ValueChecks => ValueSpec.Checks;

        public IEnumerable<BoundRelation> ValueLinks(SketchObject o, RenderContext ctx)
            => ValueSpec.Variables.Count == 0 ? Array.Empty<BoundRelation>() : BsValues.Links(o, ctx);

        /// <summary>Type-specific parameters (before height and scale).</summary>
        public IReadOnlyList<ParamDef> Params { get; init; } = Array.Empty<ParamDef>();
        public Mounting Mount { get; init; } = Mounting.Wall;
        /// <summary>Default mounting height in mm (bottom edge above the finished floor).</summary>
        public double DefaultHeight { get; init; }
        /// <summary>
        /// True if the preferred sizes are real dimensions at 1:100 (radiator, bathtub): the box height in another view is the
        /// real height at the object's plan scale. False for symbols (valves, outlets): the visual scale is kept.
        /// </summary>
        public bool ToScale { get; init; } = true;
        /// <summary>Stands or hangs against a wall: a wall mount on the top edge in plan.</summary>
        public bool WallBacked { get; init; } = true;
        public bool Uniform { get; init; }
        /// <summary>Ports per view (local millimetres).</summary>
        public Func<Canvas, SketchObject, string, IEnumerable<PortAt>> Ports { get; init; } = (_, _, _) => Array.Empty<PortAt>();
        /// <summary>Rails per view (risers and bonding bars: lines dock anywhere along them).</summary>
        public Func<Canvas, SketchObject, string, IEnumerable<(Vec2 A, Vec2 B)>>? Rails { get; init; }
        public Func<SketchObject, IEnumerable<ContextAction>>? Extra { get; init; }
        public IReadOnlyList<string> CompanionIds { get; init; } = Array.Empty<string>();

        public Size2 PreferredSize(string view) => view switch
        {
            BsViews.Section => _section,
            BsViews.Schematic => _schematic,
            _ => _plan,
        };

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            var c = new Canvas(o.Size, ctx.Lang);
            switch (ViewOf(o))
            {
                case BsViews.Section: _drawSection(c, o, ctx); break;
                case BsViews.Schematic: _drawSchematic(c, o, ctx); break;
                default: _drawPlan(c, o, ctx); break;
            }
            return c.Items;
        }

        public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
        {
            var c = new Canvas(o.Size, ctx.Lang);
            var view = ViewOf(o);
            foreach (var p in Ports(c, o, view)) yield return Anchor.Point(AnchorRole.Port, p.P, p.Id);
            if (Rails is not null)
            {
                var i = 0;
                foreach (var (a, b) in Rails(c, o, view))
                    yield return Anchor.Rail(a, b, "rail" + (i++).ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
            foreach (var m in Mounts(c, view)) yield return m;
        }

        /// <summary>
        /// Mount anchors: in plan the wall side (top edge) of wall-backed elements; in section and schematic the reference
        /// point of the mounting height (bottom edge centre; top edge centre for ceiling elements).
        /// </summary>
        private IEnumerable<Anchor> Mounts(Canvas c, string view)
        {
            if (Mount == Mounting.Inline) yield break;
            if (view == BsViews.Plan)
            {
                if (WallBacked) yield return Anchor.Point(AnchorRole.Mount, new Vec2(0, -c.Hh), "wall");
                else if (Mount == Mounting.Ceiling) yield return Anchor.Point(AnchorRole.Mount, Vec2.Zero, "ceiling");
                else yield return Anchor.Point(AnchorRole.Mount, Vec2.Zero, "floor");
                yield break;
            }
            switch (Mount)
            {
                case Mounting.Ceiling: yield return Anchor.Point(AnchorRole.Mount, new Vec2(0, -c.Hh), "ceiling"); break;
                case Mounting.Wall: yield return Anchor.Point(AnchorRole.Mount, new Vec2(0, c.Hh), "wall"); break;
                default: yield return Anchor.Point(AnchorRole.Mount, new Vec2(0, c.Hh), "floor"); break;
            }
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
            => (Extra?.Invoke(o) ?? Array.Empty<ContextAction>()).Concat(new[] { Bs.HeightAction(o, this, lang) });

        /// <summary>
        /// What a port carries (build "+", <see cref="Building"/>): the port ids name the medium (heating flow/return incl.
        /// manifold circuits f1/r1 …, cold and hot water, circulation, waste, the air of a ventilation unit), risers and air
        /// terminals carry their own medium parameter, inline heating parts (pump, mixer, valve, expansion vessel) take
        /// either heating pipe, electrical outlets an installation cable.
        /// </summary>
        public override PortConnection? Connection(SketchObject o, Anchor port)
        {
            var id = port.Id;
            if (id is null) return null;
            switch (id)
            {
                case "riser":
                case "bottom":
                case "top":
                    return Pipe(o.Str("medium", "cold"));
                case "duct":
                    return Duct(o.Str("kind", "supply"));
                case "outdoor":
                case "exhaust":
                case "extract":
                case "supply":
                    return Duct(id);
                case "in":
                case "out":
                case "a":
                case "ab":
                case "b":
                case "pipe":
                    return PortConnection.Of(PortMedia.Heating, "bs.pipe.flow");
                case "cable":
                case "earth":
                    return PortConnection.Of(PortMedia.ElectricInstallation, "elec.house.cable");
            }
            if (id.Length > 1 && (id[0] == 'f' || id[0] == 'r') && int.TryParse(id.Substring(1), out _)) return Pipe(id[0] == 'f' ? "flow" : "return");
            return Media.Pipes.Any(m => m.Value == id) ? Pipe(id) : null;
        }

        private static PortConnection Pipe(string medium)
            => PortConnection.Of(PipeType.MediumOf(medium), "bs.pipe." + medium, new Dictionary<string, ParamValue> { ["medium"] = medium });

        private static PortConnection Duct(string kind)
            => PortConnection.Of(PortMedia.AirOf(kind), "bs.duct." + kind, new Dictionary<string, ParamValue> { ["kind"] = kind });

        /// <summary>
        /// The width (shared x extent of plan and elevation) is kept. To-scale elements take the real height of the target
        /// view at the object's plan scale; symbols keep their visual scale.
        /// </summary>
        public override Size2 SizeForView(SketchObject o, string toView, RenderContext ctx)
        {
            var from = PreferredSize(ViewOf(o));
            var to = PreferredSize(toView);
            if (ToScale)
            {
                var k = Bs.DefaultScale / Bs.ScaleOf(o, ctx.Doc);
                return new Size2(o.Size.W, Math.Max(0.2, to.H * k));
            }
            var s = from.W > Geo.Eps ? o.Size.W / from.W : 1;
            return new Size2(o.Size.W, Math.Max(0.2, to.H * s));
        }

        public override Primitive ApplyOverride(Primitive p, Style over) => Bs.Override(p, over);

        // ------------------------------------------------------------------ port helpers

        /// <summary>Ports given in normalised box coordinates (u, v in [-0.5, 0.5]) per view.</summary>
        public static Func<Canvas, SketchObject, string, IEnumerable<PortAt>> Uv(
            (double U, double V, string Id)[] plan, (double U, double V, string Id)[] section, (double U, double V, string Id)[]? schematic = null)
            => (c, _, view) => (view switch
            {
                BsViews.Section => section,
                BsViews.Schematic => schematic ?? section,
                _ => plan,
            }).Select(p => new PortAt(c.N(p.U, p.V), p.Id));

        /// <summary>The same normalised ports in every view.</summary>
        public static Func<Canvas, SketchObject, string, IEnumerable<PortAt>> Same(params (double U, double V, string Id)[] ports) => Uv(ports, ports, ports);
    }
}
