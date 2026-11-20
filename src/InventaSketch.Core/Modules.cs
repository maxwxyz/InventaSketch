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

        /// <summary>Every icon id, for hosts and tests that must provide a pictogram for each.</summary>
        public static IReadOnlyList<string> All { get; } = new[]
        {
            Schematic, Side, Section, Axial, Teeth, Top, Front, Rear, Iso, Detailed, Simplified, Symbol, Block, Plan, Exploded, Table,
            Delete, Duplicate, MirrorH, MirrorV, Rotate, RotateCcw, Increase, Decrease, Swap, ArrowStart, ArrowEnd, Text, Toggle, Cycle, Connect, Animate,
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
            ["table"] = Table, ["list"] = Table,
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
        /// <summary>Wall/structure connection point.</summary>
        Mount,
    }

    /// <summary>
    /// Snap anchor. Point: P. Circle: centre P with Radius. Segment: P to Q. Coordinates are local
    /// (box objects) or page coordinates (point objects) as returned by <see cref="ObjectType.Anchors"/>;
    /// <see cref="Registry.PageAnchors"/> always returns page coordinates.
    /// </summary>
    public sealed record Anchor(AnchorKind Kind, AnchorRole Role, Vec2 P, Vec2 Q = default, double Radius = 0)
    {
        public string? Id { get; init; }
        public static Anchor Point(AnchorRole role, Vec2 p, string? id = null) => new(AnchorKind.Point, role, p) { Id = id };
        public static Anchor Circle(AnchorRole role, Vec2 c, double r) => new(AnchorKind.Circle, role, c, default, r);
        public static Anchor Segment(AnchorRole role, Vec2 a, Vec2 b) => new(AnchorKind.Segment, role, a, b);

        public Anchor Transform(Matrix2D m) => Kind switch
        {
            AnchorKind.Point => this with { P = m.Apply(P) },
            AnchorKind.Segment => this with { P = m.Apply(P), Q = m.Apply(Q) },
            // Circles: radius scales with the mean linear scale (free scaling may make it an ellipse visually).
            _ => this with { P = m.Apply(P), Radius = Radius * Math.Sqrt(Math.Abs(m.Determinant)) },
        };
    }

    /// <summary>Context (pie menu / floating bar) action contributed by an object type.</summary>
    public sealed record ContextAction(string Id, I18nText Label, string Icon, Func<SketchObject, SketchObject> Apply)
    {
        public bool IsActive { get; init; }
        public string? Group { get; init; }
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
                if (o.Style is not null) q = type.ApplyOverride(q, o.Style);
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
