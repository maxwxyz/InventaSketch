using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Kinematics
{
    /// <summary>Library category ids of the kinematics module.</summary>
    public static class KinCategories
    {
        public const string Joints = "kin.joints";
        public const string Links = "kin.links";
        public const string Mechanisms = "kin.mechanisms";
        public const string Motion = "kin.motion";
    }

    /// <summary>Parameter definition helpers (all parameters are optional).</summary>
    internal static class KP
    {
        public static ParamDef Num(string key, string en, string de, double def, double? min = null, double? max = null, string? unit = "mm", double? step = null, bool advanced = false)
            => new(key, new I18nText(en, de), ParamKind.Number) { Default = def, Min = min, Max = max, Unit = unit, Step = step, Advanced = advanced };

        public static ParamDef Int(string key, string en, string de, int def, int min, int max, bool advanced = false)
            => new(key, new I18nText(en, de), ParamKind.Integer) { Default = def, Min = min, Max = max, Step = 1, Advanced = advanced };

        public static ParamDef Bool(string key, string en, string de, bool def, bool advanced = false)
            => new(key, new I18nText(en, de), ParamKind.Boolean) { Default = def, Advanced = advanced };

        public static ParamDef Choice(string key, string en, string de, string def, params (string Value, string En, string De)[] options)
            => new(key, new I18nText(en, de), ParamKind.Choice)
            {
                Default = def,
                Options = options.Select(x => new ParamOption(x.Value, new I18nText(x.En, x.De))).ToList(),
            };

        public static ParamDef Angle(double def) => Num("angle", "Input angle", "Antriebswinkel", def, -360, 360, "°", 15);
        public static readonly ParamDef Ghosts = Int("ghosts", "Ghost positions", "Zwischenlagen", 0, 0, 12);
        public static readonly ParamDef Path = Bool("path", "Show motion path", "Bahnkurve zeigen", false);
        public static readonly ParamDef Dof = Bool("dof", "Show degree of freedom", "Freiheitsgrad zeigen", false);
    }

    /// <summary>Shared context actions.</summary>
    internal static class KA
    {
        public const double Step = 15;

        public static double NormAngle(double a)
        {
            var r = Geo.NormDeg(a);
            return r > 180 + 1e-9 ? r - 360 : r;
        }

        /// <summary>+15° / -15° steps of the input angle (clockwise on screen is positive).</summary>
        public static IEnumerable<ContextAction> AngleSteps(SketchObject o, double def)
        {
            yield return new ContextAction("kin.angle.next", new I18nText("Next position (+15°)", "Nächste Stellung (+15°)"), IconIds.Rotate,
                x => x.WithParam("angle", NormAngle(x.Num("angle", def) + Step))) { Group = "angle" };
            yield return new ContextAction("kin.angle.prev", new I18nText("Previous position (−15°)", "Vorherige Stellung (−15°)"), IconIds.Rotate,
                x => x.WithParam("angle", NormAngle(x.Num("angle", def) - Step))) { Group = "angle" };
        }

        /// <summary>Toggles ghost positions between 0 and the last used count (4 by default).</summary>
        public static ContextAction Ghosts(SketchObject o)
        {
            var n = o.Int("ghosts", 0);
            return new ContextAction("kin.ghosts", new I18nText("Ghost positions", "Zwischenlagen"), IconIds.Animate, x =>
            {
                var cur = x.Int("ghosts", 0);
                if (cur > 0) return x.WithParam("ghosts", 0).WithParam("ghostsLast", cur);
                return x.WithParam("ghosts", Math.Max(1, x.Int("ghostsLast", 4)));
            }) { IsActive = n > 0, Group = "motion" };
        }

        public static ContextAction Toggle(SketchObject o, string id, I18nText label, string icon, string key, bool def, string group = "motion")
            => new(id, label, icon, x => x.WithParam(key, !x.Flag(key, def))) { IsActive = o.Flag(key, def), Group = group };

        public static ContextAction PathToggle(SketchObject o)
            => Toggle(o, "kin.path", new I18nText("Motion path", "Bahnkurve"), IconIds.Toggle, "path", false);
    }

    /// <summary>One representation (view) of a box symbol.</summary>
    internal sealed class ViewSpec
    {
        public ViewSpec(string id, I18nText label, string icon, Action<Pen, SketchObject, RenderContext> draw)
        {
            Id = id; Label = label; Icon = icon; Draw = draw;
        }

        public string Id { get; }
        public I18nText Label { get; }
        public string Icon { get; }
        public Action<Pen, SketchObject, RenderContext> Draw { get; }

        public static readonly I18nText SchematicLabel = new("Schematic", "Schema");
        public static readonly I18nText DetailedLabel = new("Detailed", "Detailliert");

        public static ViewSpec Schematic(Action<Pen, SketchObject, RenderContext> draw) => new("schematic", SchematicLabel, IconIds.Schematic, draw);
        public static ViewSpec Detailed(Action<Pen, SketchObject, RenderContext> draw) => new("detailed", DetailedLabel, IconIds.Detailed, draw);
    }

    /// <summary>
    /// Data-driven box symbol: views share one design box (so anchors are view independent), optional parameters,
    /// anchors and context actions. The drawing is stretched to the object size.
    /// </summary>
    internal sealed class KinType : ObjectType
    {
        private readonly string _id;
        private readonly I18nText _name;
        private readonly string _category;
        private readonly IReadOnlyList<string> _keywords;
        private readonly IReadOnlyList<ViewSpec> _views;
        private readonly IReadOnlyList<ViewDef> _viewDefs;

        public KinType(string id, I18nText name, string category, Size2 design, string[] keywords, params ViewSpec[] views)
        {
            if (views.Length == 0) throw new ArgumentException("At least one view is required.", nameof(views));
            _id = id; _name = name; _category = category; _keywords = keywords; _views = views; Design = design;
            _viewDefs = views.Select(v => new ViewDef(v.Id, v.Label) { Icon = v.Icon }).ToList();
        }

        public override string Id => _id;
        public override I18nText Name => _name;
        public override string Category => _category;
        public override IReadOnlyList<string> Keywords => _keywords;
        public override IReadOnlyList<ViewDef> Views => _viewDefs;
        public override Size2 DefaultSize => Design;
        public override IReadOnlyList<ParamDef> Parameters => Params;
        public override bool PreferUniformScale => Uniform;

        public Size2 Design { get; }
        public IReadOnlyList<ParamDef> Params { get; init; } = Array.Empty<ParamDef>();
        public bool Uniform { get; init; }
        /// <summary>Anchors in local coordinates (computed with the object's pen).</summary>
        public Func<Pen, SketchObject, IEnumerable<Anchor>>? AnchorsFn { get; init; }
        /// <summary>Role in self-built linkages (fixed pivot, free joint, slider).</summary>
        public LinkageRole Role { get; init; }
        public override LinkageRole LinkageRole(SketchObject o) => Role;
        public Func<SketchObject, IEnumerable<ContextAction>>? Extra { get; init; }

        public ViewSpec Spec(SketchObject o) => _views.FirstOrDefault(v => v.Id == ViewOf(o)) ?? _views[0];

        public Pen PenFor(SketchObject o) => new(o.Size, Design);

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            var pen = PenFor(o);
            Spec(o).Draw(pen, o, ctx);
            return pen.Items;
        }

        public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
            => AnchorsFn is null ? Array.Empty<Anchor>() : AnchorsFn(PenFor(o), o);

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
            => Extra is null ? Array.Empty<ContextAction>() : Extra(o);
    }

    /// <summary>Point-geometry kinematic element (links, springs, dampers, motion paths); renders in page coordinates.</summary>
    internal abstract class KinPointType : ObjectType
    {
        private readonly string _id;
        private readonly I18nText _name;
        private readonly string _category;
        private readonly string[] _keywords;

        protected KinPointType(string id, I18nText name, string category, params string[] keywords)
        {
            _id = id; _name = name; _category = category; _keywords = keywords;
        }

        public override string Id => _id;
        public override I18nText Name => _name;
        public override string Category => _category;
        public override IReadOnlyList<string> Keywords => _keywords;
        public override GeometryKind Geometry => GeometryKind.Points;

        protected virtual int MinPoints => 2;
        /// <summary>Whether the points are joint centres (exposed as <see cref="AnchorRole.Port"/>).</summary>
        protected virtual bool PortsAtPoints => true;

        public sealed override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            var pts = ctx.Doc.ResolvePoints(o);
            if (pts.Count < MinPoints) return Array.Empty<Primitive>();
            return RenderPoints(o, ctx, pts);
        }

        protected abstract IReadOnlyList<Primitive> RenderPoints(SketchObject o, RenderContext ctx, IReadOnlyList<Vec2> p);

        public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
        {
            var p = ctx.Doc.ResolvePoints(o);
            if (PortsAtPoints)
                for (var i = 0; i < p.Count; i++) yield return Anchor.Point(AnchorRole.Port, p[i], "j" + i.ToString(CultureInfo.InvariantCulture));
            for (var i = 0; i + 1 < p.Count; i++) yield return Anchor.Point(AnchorRole.Mid, Vec2.Lerp(p[i], p[i + 1], 0.5), "m" + i.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>Joint radius of point elements (mm), optional parameter "joint".</summary>
        protected static double JointRadius(SketchObject o) => Geo.Clamp(o.Num("joint", 1.2), 0.3, 10);

        protected static readonly ParamDef JointParam = KP.Num("joint", "Joint circle radius", "Gelenkkreisradius", 1.2, 0.3, 10, "mm", 0.1, advanced: true);
    }
}
