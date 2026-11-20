using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json.Serialization;

namespace InventaSketch.Core
{
    // =============================================================================================
    // Module contract. A module contributes object types, library items and (optionally) rules.
    // Modules depend only on InventaSketch.Core, never on host APIs or on each other's internals.
    // =============================================================================================

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum Lang { En, De }

    public static class Language
    {
        /// <summary>German if the culture/language tag starts with "de", otherwise English (fallback).</summary>
        public static Lang Detect(string? cultureOrTag) =>
            cultureOrTag is not null && cultureOrTag.Trim().StartsWith("de", StringComparison.OrdinalIgnoreCase) ? Lang.De : Lang.En;

        public static Lang FromCurrentCulture() => Detect(CultureInfo.CurrentUICulture.Name);
    }

    /// <summary>Bilingual text. English is canonical and the fallback.</summary>
    public sealed record I18nText(string En, string? De = null)
    {
        public string Get(Lang lang) => lang == Lang.De && !string.IsNullOrEmpty(De) ? De! : En;
        public static implicit operator I18nText(string en) => new(en);
        public override string ToString() => En;
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum GeometryKind { Box, Points }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ParamKind { Number, Integer, Boolean, Choice, Text }

    public sealed record ParamOption(string Value, I18nText Label);

    /// <summary>Inspector parameter definition. All parameters are optional by design.</summary>
    public sealed record ParamDef(string Key, I18nText Label, ParamKind Kind)
    {
        public ParamValue? Default { get; init; }
        public double? Min { get; init; }
        public double? Max { get; init; }
        public double? Step { get; init; }
        public string? Unit { get; init; }
        public IReadOnlyList<ParamOption>? Options { get; init; }
        /// <summary>Shown only in the "engineering" section of the inspector.</summary>
        public bool Advanced { get; init; }
    }

    public sealed record ViewDef(string Id, I18nText Label)
    {
        /// <summary>Abstract pictogram for bars and pie menus (one of <see cref="IconIds"/>); null = derived from the id.</summary>
        public string? Icon { get; init; }

        /// <summary>
        /// Preferred box size (mm, at the type's default scale) of an object shown in this view; null = the type's
        /// <see cref="ObjectType.DefaultSize"/>. Used to adapt the size when switching between views with very different
        /// aspects (for example a vehicle seen from the top and from the side), see <see cref="ObjectType.SizeForView"/>.
        /// </summary>
        public Size2? Size { get; init; }
    }

    /// <summary>
    /// Shared vocabulary of abstract UI pictograms. Hosts draw these as one consistent line-icon set in floating bars and
    /// pie menus (never as miniature renders of the element). Modules reference them from <see cref="ViewDef.Icon"/> and
    /// <see cref="ContextAction.Icon"/>; unknown ids fall back to a neutral icon.
    /// </summary>
    public static class IconIds
    {
        // Representations / views
        public const string Schematic = "view-schematic";   // stick/kinematic symbol
        public const string Side = "view-side";
        public const string Section = "view-section";       // hatched cut
        public const string Axial = "view-axial";           // circle with centre cross
        public const string Teeth = "view-teeth";           // simplified toothing
        public const string Top = "view-top";
        public const string Front = "view-front";
        public const string Rear = "view-rear";
        public const string Iso = "view-iso";
        public const string Detailed = "view-detailed";
        public const string Simplified = "view-simplified";
        public const string Symbol = "view-symbol";         // standard graphical symbol (IEC/ISO)
        public const string Block = "view-block";           // block diagram
        public const string Plan = "view-plan";             // floor plan / map
        public const string Exploded = "view-exploded";
        public const string Table = "view-table";           // tabular / list form (ER table, BOM)
        public const string Pin = "view-pin";               // map pin / location marker
        // Commands
        public const string Delete = "cmd-delete";
        public const string Duplicate = "cmd-duplicate";
        public const string MirrorH = "cmd-mirror-h";
        public const string MirrorV = "cmd-mirror-v";
        public const string Rotate = "cmd-rotate";          // clockwise
        public const string RotateCcw = "cmd-rotate-ccw";   // counter-clockwise
        public const string Increase = "cmd-increase";      // step a value up (+)
        public const string Decrease = "cmd-decrease";      // step a value down (−)
        public const string Swap = "cmd-swap";
        public const string ArrowStart = "cmd-arrow-start";
        public const string ArrowEnd = "cmd-arrow-end";
        public const string Text = "cmd-text";
        public const string Toggle = "cmd-toggle";
        public const string Cycle = "cmd-cycle";
        public const string Connect = "cmd-connect";
        public const string Animate = "cmd-animate";
        public const string Medium = "cmd-medium";          // switch the medium (hydraulic/pneumatic: a drop)
        public const string Actuation = "cmd-actuation";    // change how something is operated (push button)

        /// <summary>Every icon id, for hosts and tests that must provide a pictogram for each.</summary>
        public static IReadOnlyList<string> All { get; } = new[]
        {
            Schematic, Side, Section, Axial, Teeth, Top, Front, Rear, Iso, Detailed, Simplified, Symbol, Block, Plan, Exploded, Table, Pin,
            Delete, Duplicate, MirrorH, MirrorV, Rotate, RotateCcw, Increase, Decrease, Swap, ArrowStart, ArrowEnd, Text, Toggle, Cycle, Connect, Animate, Medium, Actuation,
        };

        private static readonly Dictionary<string, string> ViewAliases = new(StringComparer.OrdinalIgnoreCase)
        {
            ["stick"] = Schematic, ["schematic"] = Schematic, ["line"] = Schematic, ["schema"] = Schematic, ["kinematic"] = Schematic,
            ["side"] = Side, ["body"] = Side, ["stepped"] = Side, ["elevation"] = Front,
            ["section"] = Section, ["hollow"] = Section, ["cut"] = Section,
            ["axial"] = Axial, ["face"] = Axial, ["teeth"] = Teeth,
            ["top"] = Top, ["front"] = Front, ["rear"] = Rear, ["iso"] = Iso,
            ["detailed"] = Detailed, ["simplified"] = Simplified, ["ansi"] = Simplified, ["full"] = Detailed, ["compact"] = Simplified,
            ["symbol"] = Symbol, ["block"] = Block, ["diagram"] = Block, ["plan"] = Plan, ["exploded"] = Exploded,
            ["table"] = Table, ["list"] = Table, ["pin"] = Pin, ["marker"] = Pin,
        };

        /// <summary>The pictogram of a view: <see cref="ViewDef.Icon"/>, else derived from the view id, else <see cref="Detailed"/>.</summary>
        public static string ForView(ViewDef view) =>
            view.Icon ?? (ViewAliases.TryGetValue(view.Id, out var id) ? id : Detailed);
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum AnchorKind { Point, Circle, Segment }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum AnchorRole
    {
        Center, Corner, EdgeMid, End, Mid, Port, Quadrant,
        /// <summary>Shaft/rotation axis (segment). Gear, bearing and hub centres snap onto it.</summary>
        Axis,
        /// <summary>External pitch circle (gears mesh when pitch circles touch).</summary>
        Pitch,
        /// <summary>Internal pitch circle (ring gear: planets mesh inside).</summary>
        PitchInner,
        /// <summary>Point that wants to sit on an axis (gear/bearing/hub centre in side view).</summary>
        Hub,
        /// <summary>
        /// Wall/structure/vehicle connection point. A moving <c>Mount</c> point snaps onto a <c>Mount</c> point of
        /// another object (sensor onto vehicle, wall end onto wall end) with the priority of a port.
        /// </summary>
        Mount,
        /// <summary>
        /// Segment along which moving <see cref="End"/> and <see cref="Port"/> points may dock anywhere (wires, buses,
        /// sequence lifelines, lane centres). Its two end points are docking points with port priority; in between it
        /// ranks below discrete ports. Unlike <see cref="Axis"/> it never attracts hubs.
        /// </summary>
        Rail,
    }

    /// <summary>
    /// Snap anchor. Point: P. Circle: centre P with Radius. Segment: P to Q. Coordinates are local
    /// (box objects) or page coordinates (point objects) as returned by <see cref="ObjectType.Anchors"/>;
    /// <see cref="Registry.PageAnchors"/> always returns page coordinates.
    /// </summary>
    public sealed record Anchor(AnchorKind Kind, AnchorRole Role, Vec2 P, Vec2 Q = default, double Radius = 0)
    {
        /// <summary>
        /// Optional id, unique per object (pins, ports, mounts). Attachments refer to it via
        /// <see cref="Attachment.AnchorId"/>, so ids of connection points must be stable across parameter changes.
        /// </summary>
        public string? Id { get; init; }
        /// <summary>Id of the object that contributed the anchor (set by <see cref="Snapper.Targets"/>; null for page anchors).</summary>
        public string? Owner { get; init; }
        public static Anchor Point(AnchorRole role, Vec2 p, string? id = null) => new(AnchorKind.Point, role, p) { Id = id };
        public static Anchor Circle(AnchorRole role, Vec2 c, double r) => new(AnchorKind.Circle, role, c, default, r);
        public static Anchor Segment(AnchorRole role, Vec2 a, Vec2 b) => new(AnchorKind.Segment, role, a, b);
        /// <summary>A <see cref="AnchorRole.Rail"/> segment from <paramref name="a"/> to <paramref name="b"/>.</summary>
        public static Anchor Rail(Vec2 a, Vec2 b, string? id = null) => new(AnchorKind.Segment, AnchorRole.Rail, a, b) { Id = id };

        public Anchor Transform(Matrix2D m) => Kind switch
        {
            AnchorKind.Point => this with { P = m.Apply(P) },
            AnchorKind.Segment => this with { P = m.Apply(P), Q = m.Apply(Q) },
            // Circles: radius scales with the mean linear scale (free scaling may make it an ellipse visually).
            _ => this with { P = m.Apply(P), Radius = Radius * Math.Sqrt(Math.Abs(m.Determinant)) },
        };
    }

    /// <summary>
    /// Context (pie menu / floating bar) action contributed by an object type. Plain actions map one object to a new
    /// object (<see cref="Apply"/>). An action may instead ask the user for a text value (<see cref="Input"/>) or work on
    /// the whole document (<see cref="ApplyInDocument"/>, for example aligning a door to the wall it sits on). Hosts run
    /// actions through <see cref="ContextActions.Run"/> so all three forms behave the same everywhere.
    /// </summary>
    public sealed record ContextAction(string Id, I18nText Label, string Icon, Func<SketchObject, SketchObject> Apply)
    {
        public bool IsActive { get; init; }
        public string? Group { get; init; }
        /// <summary>False greys the action out (offered but not applicable, e.g. "align to wall" without a wall).</summary>
        public bool IsEnabled { get; init; } = true;
        /// <summary>Text value the host asks for before running the action (prompt dialog or inline field).</summary>
        public ActionInput? Input { get; init; }
        /// <summary>
        /// Document-aware apply: receives the current document, the object and a render context of that document and
        /// returns the new document (it may change the object and, where a host supports it, other objects).
        /// </summary>
        public Func<SketchDocument, SketchObject, RenderContext, SketchDocument>? ApplyInDocument { get; init; }

        /// <summary>Action that asks for a text value; <paramref name="apply"/> receives the object and the entered text.</summary>
        public static ContextAction WithInput(string id, I18nText label, string icon, I18nText prompt, string current, Func<SketchObject, string, SketchObject> apply, string? unit = null)
            => new(id, label, icon, o => o) { Input = new ActionInput(prompt, current, apply) { Unit = unit } };

        /// <summary>Action that needs the document (neighbouring objects) to compute its result.</summary>
        public static ContextAction InDocument(string id, I18nText label, string icon, Func<SketchDocument, SketchObject, RenderContext, SketchDocument> apply)
            => new(id, label, icon, o => o) { ApplyInDocument = apply };

        /// <summary>
        /// Runs the action on <paramref name="o"/> inside <paramref name="doc"/>. Input actions need <paramref name="input"/>
        /// (<see cref="ArgumentException"/> otherwise); their apply function may throw <see cref="FormatException"/> for
        /// values it cannot read.
        /// </summary>
        public SketchDocument Run(SketchDocument doc, SketchObject o, RenderContext ctx, string? input = null)
        {
            if (ApplyInDocument is not null) return ApplyInDocument(doc, o, ctx);
            if (Input is not null)
            {
                if (input is null) throw new ArgumentException($"Action '{Id}' needs an input value.");
                return Edits.Replace(doc, Input.Apply(o, input));
            }
            return Edits.Replace(doc, Apply(o));
        }
    }

    /// <summary>Text input of a <see cref="ContextAction"/>: prompt label, current value and the apply function.</summary>
    public sealed record ActionInput(I18nText Label, string Value, Func<SketchObject, string, SketchObject> Apply)
    {
        /// <summary>Optional unit shown next to the field (e.g. "mm").</summary>
        public string? Unit { get; init; }
    }

    /// <summary>Host-independent execution of context actions (engine, COM add-in and LibreOffice share it).</summary>
    public static class ContextActions
    {
        /// <summary>
        /// Applies the type action <paramref name="actionId"/> to every listed object that offers it (enabled). Each object
        /// gets its own action instance (labels and steps may depend on the object), evaluated against the document as
        /// changed by the objects before it.
        /// </summary>
        public static SketchDocument Run(Registry registry, SketchDocument doc, IEnumerable<string> ids, string actionId, string? input, Lang lang)
        {
            foreach (var id in ids.ToList())
            {
                var o = doc.Get(id);
                var type = o is null ? null : registry.Type(o.Type);
                if (o is null || type is null) continue;
                var ctx = new RenderContext(doc, lang);
                var a = type.Actions(o, ctx).FirstOrDefault(x => x.Id == actionId);
                if (a is null || !a.IsEnabled) continue;
                doc = a.Run(doc, o, ctx, input);
            }
            return doc;
        }
    }

    public sealed class RenderContext
    {
        public RenderContext(SketchDocument doc, Lang lang = Lang.En, bool monochrome = false)
        {
            Doc = doc; Lang = lang; Monochrome = monochrome;
        }
        public SketchDocument Doc { get; }
        public Lang Lang { get; }
        public bool Monochrome { get; }
    }

    /// <summary>Base class for all object types.</summary>
    public abstract class ObjectType
    {
        public abstract string Id { get; }
        public abstract I18nText Name { get; }
        public virtual string Category => "general";
        public virtual IReadOnlyList<string> Keywords => Array.Empty<string>();
        public virtual GeometryKind Geometry => GeometryKind.Box;
        public virtual Size2 DefaultSize => new(30, 30);

        /// <summary>
        /// Types that are typically added next to this one (a shaft: gears, bearings, couplings; a wire: electrical
        /// symbols). Offered by the hosts' "Insert ▸" menu in the selected object's view; see <see cref="Registry.Suggestions"/>.
        /// </summary>
        public virtual IReadOnlyList<string> Companions => Array.Empty<string>();
        /// <summary>Default points relative to the insertion point (point-geometry types).</summary>
        public virtual IReadOnlyList<Vec2> DefaultPoints => new[] { new Vec2(-15, 0), new Vec2(15, 0) };
        public virtual IReadOnlyList<ViewDef> Views => Array.Empty<ViewDef>();
        public virtual string? DefaultView => Views.Count > 0 ? Views[0].Id : null;
        public virtual IReadOnlyList<ParamDef> Parameters => Array.Empty<ParamDef>();
        /// <summary>True if the object should preferably scale uniformly (side handles still allowed).</summary>
        public virtual bool PreferUniformScale => false;
        /// <summary>True for objects whose text is edited directly (labels, text boxes).</summary>
        public virtual bool HasText => false;
        /// <summary>Preferred host output: native shapes when possible, or a single vector picture.</summary>
        public virtual bool PreferNative => true;

        /// <summary>Renders the object. Box geometry: local coordinates centred at (0,0). Points: page coordinates.</summary>
        public abstract IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx);

        /// <summary>Type-specific snap anchors (local coordinates for box geometry, page coordinates for points).</summary>
        public virtual IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx) => Array.Empty<Anchor>();

        public virtual IEnumerable<ContextAction> Actions(SketchObject o, Lang lang) => Array.Empty<ContextAction>();

        /// <summary>
        /// Document-aware action list (hosts call this one). Defaults to <see cref="Actions(SketchObject, Lang)"/>; types
        /// override it when labels, enabled states or document-aware actions depend on other objects or document settings.
        /// </summary>
        public virtual IEnumerable<ContextAction> Actions(SketchObject o, RenderContext ctx) => Actions(o, ctx.Lang);

        /// <summary>
        /// True if the rendering reads other objects of the document (wall joints, openings cutting walls). Hosts that keep
        /// objects in separate units (PowerPoint COM) render such objects with the neighbouring units as context. Types that
        /// return <see cref="Dependents"/> must set this flag as well.
        /// </summary>
        public virtual bool DependsOnNeighbours => false;

        /// <summary>
        /// Ids of other objects whose rendering depends on <paramref name="o"/> and must be redrawn when it is added, changed
        /// or removed (for example the walls joined to a wall and the doors sitting in it). Evaluated by the engine before and
        /// after every edit; the default is none.
        /// </summary>
        public virtual IEnumerable<string> Dependents(SketchObject o, RenderContext ctx) => Array.Empty<string>();

        /// <summary>True if the rendering uses the document plan scale (<see cref="DocumentSettings.PlanScale"/>).</summary>
        public virtual bool UsesPlanScale => false;

        /// <summary>
        /// True if objects of this type are parts of a product and count in the bill of materials (box objects only).
        /// Map symbols, diagram shapes and other non-physical symbols return false.
        /// </summary>
        public virtual bool IsPart => true;

        /// <summary>
        /// Called when a parameter was set from outside the type's own actions (inspector, label menu): returns the object
        /// to keep, for example with <see cref="SketchObject.Size"/> following a design box that grew (a valve with more
        /// positions keeps its drawing scale instead of being squeezed). Default: <paramref name="after"/> unchanged.
        /// </summary>
        public virtual SketchObject OnParamChanged(SketchObject before, SketchObject after, string key) => after;

        /// <summary>
        /// Motion frames for an animation as a slide sequence (mechanisms: the input angle stepped through its range):
        /// <paramref name="count"/> copies of <paramref name="o"/> in playing order, the first one the current state, all
        /// with the same box so hosts can morph between them. Empty for types without motion.
        /// </summary>
        public virtual IReadOnlyList<SketchObject> AnimationFrames(SketchObject o, int count) => Array.Empty<SketchObject>();

        /// <summary>
        /// Size of <paramref name="o"/> after switching to <paramref name="toView"/>. Default: when either view declares a
        /// preferred <see cref="ViewDef.Size"/>, the object keeps its visual scale (geometric mean of its scale factors
        /// relative to the preferred size of the current view) and takes the aspect of the target view; otherwise the size
        /// is kept.
        /// </summary>
        public virtual Size2 SizeForView(SketchObject o, string toView, RenderContext ctx)
        {
            var fromDef = Views.FirstOrDefault(v => v.Id == ViewOf(o));
            var toDef = Views.FirstOrDefault(v => v.Id == toView);
            if (fromDef?.Size is null && toDef?.Size is null) return o.Size;
            var a = fromDef?.Size ?? DefaultSize;
            var b = toDef?.Size ?? DefaultSize;
            if (a.W < Geo.Eps || a.H < Geo.Eps || o.Size.W < Geo.Eps || o.Size.H < Geo.Eps) return o.Size;
            var k = Math.Sqrt(o.Size.W / a.W * (o.Size.H / a.H));
            return new Size2(b.W * k, b.H * k);
        }

        /// <summary>The object switched to <paramref name="view"/> (unknown or current view: unchanged), size adapted for box objects.</summary>
        public SketchObject WithView(SketchObject o, string? view, RenderContext ctx)
        {
            if (view is null || Views.All(v => v.Id != view) || ViewOf(o) == view) return o;
            return o.IsPointGeometry ? o with { View = view } : o with { View = view, Size = SizeForView(o, view, ctx) };
        }

        /// <summary>
        /// Other objects on the page of <paramref name="o"/> whose rough page bounds (box corners or raw points) come closer
        /// than <paramref name="margin"/> to the rough bounds of <paramref name="o"/>. Cheap neighbourhood query for
        /// <see cref="Dependents"/>.
        /// </summary>
        public static IEnumerable<SketchObject> ObjectsNear(SketchDocument doc, SketchObject o, double margin)
        {
            var page = doc.PageOf(o.Id);
            if (page is null) yield break;
            var area = RoughBounds(doc, o).Inflate(margin);
            foreach (var other in doc.ObjectsOn(page))
                if (other.Id != o.Id && area.Intersects(RoughBounds(doc, other))) yield return other;
        }

        private static Rect RoughBounds(SketchDocument doc, SketchObject o)
            => o.IsPointGeometry ? Rect.FromPoints(doc.ResolvePoints(o)) : o.Bounds();

        public string ViewOf(SketchObject o) => o.View ?? DefaultView ?? "";

        /// <summary>Creates a new instance at the given page position.</summary>
        public virtual SketchObject Create(Vec2 at, LibraryItem? item = null)
        {
            var parameters = new Dictionary<string, ParamValue>();
            foreach (var p in Parameters) if (p.Default.HasValue) parameters[p.Key] = p.Default.Value;
            if (item?.Params is not null) foreach (var kv in item.Params) parameters[kv.Key] = kv.Value;
            var size = item?.Size ?? DefaultSize;
            var obj = new SketchObject
            {
                Id = Ids.New(),
                Type = Id,
                Transform = new Transform(at.X, at.Y),
                Size = size,
                View = item?.View ?? DefaultView,
                Params = parameters,
                Text = item?.Text,
                Style = item?.Style,
            };
            if (Geometry == GeometryKind.Points)
                obj = obj with { Points = (item?.Points ?? DefaultPoints).Select(p => p + at).ToList() };
            return obj;
        }

        /// <summary>Applies the user's style override to rendered primitives.</summary>
        public virtual Primitive ApplyOverride(Primitive p, Style over) => Prim.Restyle(p, s => s with
        {
            Stroke = over.Stroke ?? s.Stroke,
            Opacity = over.Opacity ?? s.Opacity,
            StrokeWidth = over.StrokeWidth is double w && (s.Role ?? LineRole.Visible) is LineRole.Visible or LineRole.Cut ? w : s.StrokeWidth,
            Fill = over.Fill is not null && s.Fill is not null && s.Fill.Kind != FillKind.None ? over.Fill : s.Fill,
            ArrowStart = over.ArrowStart ?? s.ArrowStart,
            ArrowEnd = over.ArrowEnd ?? s.ArrowEnd,
            ArrowSize = over.ArrowSize ?? s.ArrowSize,
            Dash = over.Dash ?? s.Dash,
        });
    }

    public sealed record LibraryCategory(string Id, I18nText Name, string ModuleId, int Order = 100);

    /// <summary>A preset in the library: a type plus optional view/params/size.</summary>
    public sealed record LibraryItem(string Id, string TypeId, I18nText Name, string Category)
    {
        public string? View { get; init; }
        public IReadOnlyDictionary<string, ParamValue>? Params { get; init; }
        public Size2? Size { get; init; }
        public IReadOnlyList<Vec2>? Points { get; init; }
        public string? Text { get; init; }
        public Style? Style { get; init; }
        public IReadOnlyList<string> Keywords { get; init; } = Array.Empty<string>();
    }

    public interface IModule
    {
        string Id { get; }
        I18nText Name { get; }
        string Version { get; }
        IEnumerable<ObjectType> Types { get; }
        IEnumerable<LibraryCategory> Categories { get; }
        IEnumerable<LibraryItem> Library { get; }
    }

    /// <summary>A rendered object in page coordinates.</summary>
    public sealed record RenderedObject(SketchObject Object, IReadOnlyList<Primitive> Primitives);

    public sealed class Registry
    {
        private readonly Dictionary<string, ObjectType> _types = new(StringComparer.Ordinal);
        private readonly List<IModule> _modules = new();
        private readonly List<LibraryCategory> _categories = new();
        private readonly List<LibraryItem> _library = new();

        public IReadOnlyList<IModule> Modules => _modules;
        public IReadOnlyList<LibraryCategory> Categories => _categories;
        public IReadOnlyList<LibraryItem> Library => _library;
        public IEnumerable<ObjectType> Types => _types.Values;

        public Registry Register(IModule module)
        {
            if (_modules.Any(m => m.Id == module.Id)) throw new InvalidOperationException($"Module '{module.Id}' is already registered.");
            foreach (var t in module.Types)
            {
                if (_types.ContainsKey(t.Id)) throw new InvalidOperationException($"Object type '{t.Id}' is already registered.");
                _types[t.Id] = t;
            }
            _modules.Add(module);
            _categories.AddRange(module.Categories);
            foreach (var item in module.Library)
            {
                if (!_types.ContainsKey(item.TypeId)) throw new InvalidOperationException($"Library item '{item.Id}' references unknown type '{item.TypeId}'.");
                _library.Add(item);
            }
            return this;
        }

        public ObjectType? Type(string id) => _types.TryGetValue(id, out var t) ? t : null;

        public ObjectType TypeOf(SketchObject o) => Type(o.Type) ?? throw new KeyNotFoundException($"Unknown object type '{o.Type}'.");

        public LibraryItem? Item(string id) => _library.FirstOrDefault(i => i.Id == id);

        /// <summary>
        /// The object with parameter <paramref name="key"/> set to <paramref name="value"/> (null removes it), passed through
        /// <see cref="ObjectType.OnParamChanged"/> so the type can keep its proportions. Hosts set parameters through this.
        /// </summary>
        public SketchObject SetParam(SketchObject o, string key, ParamValue? value)
        {
            SketchObject next;
            if (value is ParamValue pv) next = o.WithParam(key, pv);
            else next = o with { Params = o.Params.Where(kv => kv.Key != key).ToDictionary(kv => kv.Key, kv => kv.Value) };
            return Type(o.Type)?.OnParamChanged(o, next, key) ?? next;
        }

        /// <summary>
        /// The object switched to <paramref name="view"/> without distortion. Types that declare view sizes
        /// (<see cref="ViewDef.Size"/>) adapt themselves (<see cref="ObjectType.WithView"/>); otherwise the library presets
        /// of the type in both views give the aspect: a dimension both presets share (e.g. the diameter of a gear in front
        /// and side view) keeps its current length, else the visual scale (geometric mean) is kept.
        /// </summary>
        public SketchObject WithView(SketchObject o, string? view, RenderContext ctx)
        {
            var type = Type(o.Type);
            if (type is null || view is null || type.Views.All(v => v.Id != view) || type.ViewOf(o) == view) return o;
            if (o.IsPointGeometry || type.Views.Any(v => v.Size is not null)) return type.WithView(o, view, ctx);
            var from = PresetSize(type, type.ViewOf(o));
            var to = PresetSize(type, view);
            if (from is not Size2 a || to is not Size2 b || a.W < Geo.Eps || a.H < Geo.Eps || o.Size.W < Geo.Eps || o.Size.H < Geo.Eps)
                return o with { View = view };
            double k;
            if (Math.Abs(a.H - b.H) < 1e-6 * Math.Max(1, a.H)) k = o.Size.H / a.H;
            else if (Math.Abs(a.W - b.W) < 1e-6 * Math.Max(1, a.W)) k = o.Size.W / a.W;
            else if (Math.Abs(a.H - b.W) < 1e-6 * Math.Max(1, a.H)) k = o.Size.H / a.H;
            else if (Math.Abs(a.W - b.H) < 1e-6 * Math.Max(1, a.W)) k = o.Size.W / a.W;
            else k = Math.Sqrt(o.Size.W / a.W * (o.Size.H / a.H));
            return o with { View = view, Size = new Size2(b.W * k, b.H * k) };
        }

        /// <summary>Point-geometry types a line, arrow, dimension or lead line can be converted into ("Type ▸").</summary>
        public static readonly IReadOnlyList<string> ConversionTargets = new[]
        {
            "ann.line", "ann.arrow", "ann.arrow.double", "ann.arrow.curved", "ann.arrow.elbow", "ann.leader",
            "patent.numeral", "ann.dimension.linear", "ann.dimension.aligned",
        };

        /// <summary>True if the object is one of the convertible line types.</summary>
        public static bool IsConvertible(SketchObject o) => o.IsPointGeometry && ConversionTargets.Contains(o.Type);

        /// <summary>
        /// Creates a point object of the item between two points: the item's points are mapped so that its first point is
        /// <paramref name="start"/> and its last <paramref name="end"/> (curve control points keep their relative position).
        /// </summary>
        public SketchObject CreateBetween(LibraryItem item, Vec2 start, Vec2 end)
        {
            var obj = Create(item, start);
            var pts = obj.Points ?? new[] { start, end };
            if (pts.Count < 2) return obj with { Points = new[] { start, end } };
            Vec2 a = pts[0], b = pts[pts.Count - 1];
            var axis = b - a;
            var l2 = axis.Dot(axis);
            var target = end - start;
            var mapped = pts.Select((q, i) =>
            {
                if (i == 0) return start;
                if (i == pts.Count - 1) return end;
                if (l2 < Geo.Eps) return Vec2.Lerp(start, end, (double)i / (pts.Count - 1));
                var u = (q - a).Dot(axis) / l2;
                var v = (q - a).Cross(axis) / l2;
                return start + target * u - target.Perp * v;
            }).ToList();
            return obj with { Points = mapped };
        }

        /// <summary>
        /// Converts a line-like object into another point type between the same end points; id, group, layer, numeral
        /// and stroke colour/width/opacity are kept. Unchanged if the target is unknown or the same.
        /// </summary>
        public SketchObject Convert(SketchObject o, string targetType)
        {
            var target = Type(targetType);
            if (target is null || o.Points is not { Count: >= 2 } || o.Type == targetType) return o;
            var item = _library.FirstOrDefault(i => i.Id == targetType && i.TypeId == targetType)
                ?? new LibraryItem(targetType, targetType, target.Name, "convert");
            var created = CreateBetween(item, o.Points[0], o.Points[o.Points.Count - 1]);
            var numeral = o.Type is "ann.leader" or "patent.numeral" ? (o.Text ?? o.Str("numeral", o.Ref ?? "")) : o.Ref;
            var result = created with
            {
                Id = o.Id,
                GroupId = o.GroupId,
                Layer = o.Layer,
                Ref = numeral,
                Attach = o.Attach,
                Style = o.Style is null ? null : new Style { Stroke = o.Style.Stroke, StrokeWidth = o.Style.StrokeWidth, Opacity = o.Style.Opacity },
            };
            if (result.Attach is not null && result.Points is { } rp && result.Attach.Count != rp.Count)
            {
                // Keep the end attachments on the end points when the point count changes.
                var att = new Attachment?[rp.Count];
                att[0] = o.Attach![0];
                att[rp.Count - 1] = o.Attach[o.Attach.Count - 1];
                result = result with { Attach = att };
            }
            if (!string.IsNullOrEmpty(numeral) && target.Parameters.Any(pd => pd.Key == "numeral")) result = result.WithParam("numeral", numeral!) with { Text = numeral };
            return result;
        }

        /// <summary>Annotation items every drawing needs; offered after the companions of a selection.</summary>
        public static readonly IReadOnlyList<string> AnnotationSuggestions = new[] { "patent.numeral", "ann.leader", "ann.arrow", "ann.text", "ann.dimension.linear" };

        /// <summary>
        /// Library items to insert next to <paramref name="o"/> ("Insert ▸"): the type's <see cref="ObjectType.Companions"/>
        /// (each in the object's view when the type has it), then other types of the same category, then annotation items.
        /// One item per type, at most <paramref name="max"/>.
        /// </summary>
        public IReadOnlyList<LibraryItem> Suggestions(SketchObject o, int max = 9)
        {
            var type = Type(o.Type);
            var list = new List<LibraryItem>();
            var types = new HashSet<string>(StringComparer.Ordinal) { o.Type };
            // Same view id, else the same kind of view (shaft "line" and gear "stick" are both schematic), else the first item.
            var viewIcon = type is null || o.View is null ? null : type.Views.FirstOrDefault(v => v.Id == o.View) is { } vd ? IconIds.ForView(vd) : null;
            LibraryItem? ItemOf(string typeId)
            {
                var t = Type(typeId);
                return _library.FirstOrDefault(i => i.TypeId == typeId && o.View is not null && i.View == o.View)
                    ?? (viewIcon is null || t is null ? null : _library.FirstOrDefault(i => i.TypeId == typeId && i.View is not null
                        && t.Views.FirstOrDefault(v => v.Id == i.View) is { } iv && IconIds.ForView(iv) == viewIcon))
                    ?? _library.FirstOrDefault(i => i.TypeId == typeId);
            }
            void Add(LibraryItem? item)
            {
                if (item is null || list.Count >= max || !types.Add(item.TypeId)) return;
                list.Add(item);
            }
            if (type is not null)
            {
                // Keep room for at least three annotation items (numeral, lead line, arrow).
                foreach (var t in type.Companions) { if (list.Count >= max - 3) break; Add(ItemOf(t)); }
                var annotations = Math.Max(3, AnnotationSuggestions.Count(id => _library.Any(i => i.TypeId == id)));
                foreach (var item in _library.Where(i => i.Category == type.Category && i.TypeId != o.Type).GroupBy(i => i.TypeId).Select(g => ItemOf(g.Key)))
                {
                    if (list.Count >= max - annotations) break;
                    Add(item);
                }
            }
            foreach (var t in AnnotationSuggestions) Add(_library.FirstOrDefault(i => i.TypeId == t));
            return list;
        }

        /// <summary>Preset size of a type in a view: the first library item of the type with that view (the default view also
        /// matches items without a view) and a size, else the type's default size for the default view.</summary>
        private Size2? PresetSize(ObjectType type, string view)
        {
            var isDefault = type.Views.Count == 0 || type.Views[0].Id == view;
            var item = _library.FirstOrDefault(i => i.TypeId == type.Id && i.Size is not null && (i.View == view || isDefault && i.View is null));
            return item?.Size ?? (isDefault ? type.DefaultSize : null);
        }

        public SketchObject Create(LibraryItem item, Vec2 at) => TypeOf(new SketchObject { Type = item.TypeId }).Create(at, item);

        /// <summary>Case-insensitive search over names (both languages), keywords and categories.</summary>
        public IEnumerable<LibraryItem> Search(string? query)
        {
            if (string.IsNullOrWhiteSpace(query)) return _library;
            var terms = query!.ToLowerInvariant().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            return _library.Where(i =>
            {
                var t = Type(i.TypeId);
                var hay = string.Join(" ", new[] { i.Name.En, i.Name.De ?? "", i.Category, t?.Name.En ?? "", t?.Name.De ?? "" }
                    .Concat(i.Keywords).Concat(t?.Keywords ?? Array.Empty<string>())).ToLowerInvariant();
                return terms.All(hay.Contains);
            });
        }

        /// <summary>Renders an object into page coordinates (style overrides applied).</summary>
        public IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            var type = TypeOf(o);
            var local = type.Render(o, ctx);
            var m = o.Matrix();
            var result = new List<Primitive>(local.Count);
            foreach (var p in local)
            {
                var q = o.IsPointGeometry ? p : Prim.Transform(p, m);
                if (o.Style is not null) q = Prim.TextOverride(type.ApplyOverride(q, o.Style), o.Style);
                result.Add(q);
            }
            return result;
        }

        public IEnumerable<RenderedObject> RenderPage(SketchDocument doc, Page page, RenderContext ctx, Func<SketchObject, bool>? filter = null)
        {
            var hidden = new HashSet<string>(doc.Layers.Where(l => !l.Visible).Select(l => l.Id));
            foreach (var o in doc.ObjectsOn(page))
            {
                if (o.Layer is not null && hidden.Contains(o.Layer)) continue;
                if (filter is not null && !filter(o)) continue;
                if (Type(o.Type) is null) continue; // unknown types (missing module) are skipped, not lost
                yield return new RenderedObject(o, Render(o, ctx));
            }
        }

        /// <summary>Generic box anchors (centre, corners, edge midpoints) plus type anchors, in page coordinates.</summary>
        public IEnumerable<Anchor> PageAnchors(SketchObject o, RenderContext ctx)
        {
            var type = Type(o.Type);
            if (o.IsPointGeometry)
            {
                var pts = ctx.Doc.ResolvePoints(o);
                for (var i = 0; i < pts.Count; i++) yield return Anchor.Point(AnchorRole.End, pts[i], "p" + i.ToString(CultureInfo.InvariantCulture));
                if (type is not null) foreach (var a in type.Anchors(o, ctx)) yield return a;
                yield break;
            }
            var m = o.Matrix();
            double hw = o.Size.W / 2, hh = o.Size.H / 2;
            yield return Anchor.Point(AnchorRole.Center, m.Apply(Vec2.Zero), "c");
            yield return Anchor.Point(AnchorRole.Corner, m.Apply(new Vec2(-hw, -hh)));
            yield return Anchor.Point(AnchorRole.Corner, m.Apply(new Vec2(hw, -hh)));
            yield return Anchor.Point(AnchorRole.Corner, m.Apply(new Vec2(hw, hh)));
            yield return Anchor.Point(AnchorRole.Corner, m.Apply(new Vec2(-hw, hh)));
            yield return Anchor.Point(AnchorRole.EdgeMid, m.Apply(new Vec2(0, -hh)));
            yield return Anchor.Point(AnchorRole.EdgeMid, m.Apply(new Vec2(hw, 0)));
            yield return Anchor.Point(AnchorRole.EdgeMid, m.Apply(new Vec2(0, hh)));
            yield return Anchor.Point(AnchorRole.EdgeMid, m.Apply(new Vec2(-hw, 0)));
            if (type is null) yield break;
            foreach (var a in type.Anchors(o, ctx)) yield return a.Transform(m);
        }
    }
}
