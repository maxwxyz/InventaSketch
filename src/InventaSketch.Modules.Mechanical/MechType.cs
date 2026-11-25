using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Mechanical
{
    /// <summary>One representation (view) of a mechanical object type.</summary>
    internal sealed class ViewSpec
    {
        public ViewSpec(string id, I18nText label, double w, double h, Action<Pen, SketchObject> draw)
        {
            Id = id; Label = label; Design = new Size2(w, h); Draw = draw;
        }

        public string Id { get; }
        public I18nText Label { get; }
        /// <summary>Design box: the drawing is authored for this size and stretched to the object size.
        /// For <see cref="Fluid"/> views it is only the default size.</summary>
        public Size2 Design { get; }
        public Action<Pen, SketchObject> Draw { get; }
        /// <summary>Fluid views are drawn directly in actual millimetres (shafts, belts, springs, racks).</summary>
        public bool Fluid { get; init; }
        /// <summary>View-specific anchors in local coordinates.</summary>
        public Func<Pen, SketchObject, IEnumerable<Anchor>>? Anchors { get; init; }
        /// <summary>
        /// Bore radius (actual mm at the current size) carried by the hub anchor, so the part fits onto a shaft
        /// (<see cref="ShaftFit"/>). Views with the axis in the drawing plane measure it across the axis (design radius
        /// times <see cref="Pen.Sy"/>; schematic views, which draw no bore, use the value that gives the bore of the type's
        /// side or section view after a view switch with <see cref="Registry.WithView"/>, so the fit carries over), axial views
        /// give the round bore (times <see cref="Pen.K"/>). Null: no meaningful bore, the part keeps its size on a shaft.
        /// </summary>
        public Func<Pen, SketchObject, double>? Bore { get; init; }
    }

    /// <summary>
    /// Data-driven object type used for all mechanical symbols: a list of views (each with its own drawing),
    /// optional parameters, anchors and context actions.
    /// </summary>
    internal sealed class MechType : ObjectType
    {
        private readonly string _id;
        private readonly I18nText _name;
        private readonly string _category;
        private readonly IReadOnlyList<string> _keywords;
        private readonly IReadOnlyList<ViewSpec> _views;
        private readonly IReadOnlyList<ViewDef> _viewDefs;

        public MechType(string id, I18nText name, string category, string[] keywords, params ViewSpec[] views)
        {
            if (views.Length == 0) throw new ArgumentException("At least one view is required.", nameof(views));
            _id = id; _name = name; _category = category; _keywords = keywords; _views = views;
            _viewDefs = views.Select(v => new ViewDef(v.Id, v.Label)).ToList();
        }

        public override string Id => _id;
        public override I18nText Name => _name;
        public override string Category => _category;
        public override IReadOnlyList<string> Keywords => _keywords;
        public override IReadOnlyList<ViewDef> Views => _viewDefs;
        public override Size2 DefaultSize => _views[0].Design;
        public override IReadOnlyList<ParamDef> Parameters => Params;
        public override bool PreferUniformScale => Uniform;

        public IReadOnlyList<ParamDef> Params { get; init; } = Array.Empty<ParamDef>();
        /// <summary>Types offered by "Insert ▸" next to this one (see <see cref="ObjectType.Companions"/>).</summary>
        public IReadOnlyList<string> CompanionTypes { get; init; } = Array.Empty<string>();
        public override IReadOnlyList<string> Companions => CompanionTypes;
        public bool Uniform { get; init; }
        /// <summary>Rotating elements expose a Hub anchor at the box centre (snaps onto shaft axes).</summary>
        public bool Rotating { get; init; } = true;
        /// <summary>Belt or chain between two hubs (<see cref="ObjectType.SpansHubs"/>).</summary>
        public bool Spans { get; init; }
        public override bool SpansHubs => Spans;
        /// <summary>Type-specific context actions in addition to the view switches.</summary>
        public Func<SketchObject, IEnumerable<ContextAction>>? Extra { get; init; }

        /// <summary>Teeth shown in the current view (number, local angle of a tooth centre); null when none are drawn.</summary>
        public Func<SketchObject, (int Teeth, double ToothDeg)?>? Teeth { get; init; }

        public override (int Teeth, double ToothDeg)? ToothPhase(SketchObject o) => Teeth?.Invoke(o);

        /// <summary>Views with the rotation axis horizontal in the box (schematic, side, section): coupling scales only the diameter.</summary>
        private static readonly HashSet<string> AxisViews = new(StringComparer.Ordinal) { "stick", "side", "section", "line", "body" };

        /// <summary>Parts for "explode" (<see cref="ObjectType.Parts"/>).</summary>
        public Func<SketchObject, IReadOnlyList<PartSpec>?>? PartsOf { get; init; }

        public override IReadOnlyList<PartSpec>? Parts(SketchObject o) => PartsOf?.Invoke(o);

        public override IReadOnlyList<Vec2> MeshDirections(SketchObject o)
            => AxisViews.Contains(ViewOf(o)) ? new[] { new Vec2(0, 1), new Vec2(0, -1) } : base.MeshDirections(o);

        public override SketchObject ScalePitch(SketchObject o, double k)
            => AxisViews.Contains(ViewOf(o)) ? o with { Size = new Size2(o.Size.W, o.Size.H * k) } : base.ScalePitch(o, k);

        public IReadOnlyList<ViewSpec> ViewSpecs => _views;

        public ViewSpec Spec(string? view) => _views.FirstOrDefault(v => v.Id == view) ?? _views[0];

        public ViewSpec Spec(SketchObject o) => Spec(ViewOf(o));

        public Pen PenFor(SketchObject o, ViewSpec v) => new(o.Size, v.Fluid ? o.Size : v.Design);

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
            var pen = PenFor(o, v);
            if (Rotating) yield return v.Bore is null ? Anchor.Point(AnchorRole.Hub, Vec2.Zero, "hub") : Anchor.Hub(Vec2.Zero, v.Bore(pen, o));
            if (v.Anchors is null) yield break;
            foreach (var a in v.Anchors(pen, o)) yield return a;
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            var current = ViewOf(o);
            if (_views.Count > 1)
                foreach (var v in _views)
                {
                    var id = v.Id;
                    yield return new ContextAction("view." + id, v.Label, IconIds.ForView(_viewDefs.First(d => d.Id == id)), x => SwitchView(x, id))
                    {
                        IsActive = current == id,
                        Group = "view",
                    };
                }
            if (Extra is not null)
                foreach (var a in Extra(o)) yield return a;
        }

        /// <summary>
        /// Switches the view. If the new view has a clearly different aspect (e.g. side view to axial view),
        /// the width is adapted so the symbol keeps its proportions; a free stretch the user applied is kept.
        /// </summary>
        public SketchObject SwitchView(SketchObject o, string viewId)
        {
            var from = Spec(o);
            var to = Spec(viewId);
            var size = o.Size;
            if (!from.Fluid && !to.Fluid && o.Size.H > Geo.Eps)
            {
                var a0 = from.Design.W / from.Design.H;
                var a1 = to.Design.W / to.Design.H;
                if (Math.Abs(Math.Log(a1 / a0)) > Math.Log(1.5))
                {
                    var stretch = o.Size.W / o.Size.H / a0;
                    size = new Size2(o.Size.H * a1 * stretch, o.Size.H);
                }
            }
            return o with { View = to.Id, Size = size };
        }
    }

    /// <summary>
    /// Drivetrain ("Antriebsstrang") search keywords and the type ids of drivetrain parts of the machines module that
    /// shafts, differentials and clutches offer in "Insert ▸". Only ids are referenced (no code dependency); they are
    /// skipped when the machines module is not loaded.
    /// </summary>
    internal static class Drivetrain
    {
        public const string Kw1 = "drivetrain", Kw2 = "powertrain", Kw3 = "antriebsstrang";
        public const string EMachine = "machines.drivetrain.emachine";
        public const string Engine = "machines.drivetrain.engine";
        public const string Wheel = "machines.drive.wheel";
    }

    /// <summary>Common parameter definitions and context actions.</summary>
    internal static class Common
    {
        public static readonly string[] HubKinds = { "none", "fixed", "sliding", "loose" };

        public static readonly ParamDef Hub = new ParamDef("hub", new I18nText("Shaft connection", "Welle-Nabe-Verbindung"), ParamKind.Choice)
        {
            Default = "fixed",
            Options = new[]
            {
                new ParamOption("none", new I18nText("None", "Ohne")),
                new ParamOption("fixed", new I18nText("Fixed (keyed)", "Drehfest")),
                new ParamOption("sliding", new I18nText("Sliding (keyed, axially movable)", "Drehfest verschiebbar")),
                new ParamOption("loose", new I18nText("Loose (idler on bearing)", "Drehbar (Losrad)")),
            },
        }.WithOptionIcons(v => v switch { "none" => IconIds.HubNone, "sliding" => IconIds.HubSliding, "loose" => IconIds.HubLoose, _ => IconIds.HubFixed });

        public static ParamDef Teeth(int def) => new("teeth", new I18nText("Number of teeth", "Zähnezahl"), ParamKind.Integer)
        {
            Default = def, Min = 6, Max = 400, Step = 1,
        };

        public static readonly ParamDef Module = new("module", new I18nText("Module", "Modul"), ParamKind.Number)
        {
            Unit = "mm", Min = 0.05, Max = 100, Step = 0.25, Advanced = true,
        };

        public static readonly ParamDef Helix = new("helix", new I18nText("Helical teeth", "Schrägverzahnung"), ParamKind.Boolean) { Default = false };
        public static readonly ParamDef Bore = new("bore", new I18nText("Show bore", "Bohrung zeigen"), ParamKind.Boolean) { Default = true };
        public static readonly ParamDef Keyway = new("keyway", new I18nText("Keyway", "Passfedernut"), ParamKind.Boolean) { Default = false, Advanced = true };

        public static ParamDef Count(string key, I18nText label, int def, int min, int max) => new(key, label, ParamKind.Integer)
        {
            Default = def, Min = min, Max = max, Step = 1,
        };

        public static ParamDef Flag(string key, I18nText label, bool def) => new(key, label, ParamKind.Boolean) { Default = def };

        /// <summary>
        /// The shaft-hub connection (none, fixed, sliding, loose) as a choice with the hub pictograms – a dropdown, never a
        /// cycle button (maintainer 2026-11-25).
        /// </summary>
        public static IEnumerable<ContextAction> HubChoice(SketchObject o)
            => ActionChoices.FromParam(o, "param.hub", Hub, null, "param", HubKinds);

        /// <summary>More / fewer of a counted parameter (planets, pins) as two pictogram actions.</summary>
        public static IEnumerable<ContextAction> Stepper(SketchObject o, string key, I18nText name, int def, int step, int min, int max)
        {
            var cur = o.Int(key, def);
            if (cur + step <= max)
                yield return new ContextAction("param." + key + ".inc", new I18nText(name.En + " +" + step, (name.De ?? name.En) + " +" + step), IconIds.CountMore,
                    x => x.WithParam(key, Math.Min(max, x.Int(key, def) + step))) { Group = "param" };
            if (cur - step >= min)
                yield return new ContextAction("param." + key + ".dec", new I18nText(name.En + " −" + step, (name.De ?? name.En) + " −" + step), IconIds.CountFewer,
                    x => x.WithParam(key, Math.Max(min, x.Int(key, def) - step))) { Group = "param" };
        }

        public static ContextAction Toggle(SketchObject o, string key, bool def, I18nText label, string icon)
        {
            var on = o.Flag(key, def);
            return new ContextAction("param." + key, label, icon, x => x.WithParam(key, !x.Flag(key, def))) { IsActive = on, Group = "param" };
        }
    }
}
