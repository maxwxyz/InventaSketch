using System.Collections.Generic;
using System.Text.Json;
using InventaSketch.Core;

namespace InventaSketch.Engine
{
    /// <summary>
    /// Request bag of the JSON API. Every method reads the members it needs; see docs/en/hosts/office-web.md
    /// ("Engine API") for the per-method contract. Geometry is in mm unless a member says otherwise.
    /// </summary>
    public sealed class EngineRequest
    {
        public string? Session { get; set; }
        public string? Lang { get; set; }
        public string? Id { get; set; }
        public List<string>? Ids { get; set; }
        public List<string>? Exclude { get; set; }
        public string? ItemId { get; set; }
        public double? X { get; set; }
        public double? Y { get; set; }
        public double? W { get; set; }
        public double? H { get; set; }
        public double? Dx { get; set; }
        public double? Dy { get; set; }
        public double? Sx { get; set; }
        public double? Sy { get; set; }
        public double? PivotX { get; set; }
        public double? PivotY { get; set; }
        public double? Degrees { get; set; }
        public double? Tolerance { get; set; }
        public int? Index { get; set; }
        public bool? Horizontal { get; set; }
        /// <summary>Z-order: "front", "back", "forward", "backward".</summary>
        public string? To { get; set; }
        public string? View { get; set; }
        public string? Key { get; set; }
        public JsonElement? Value { get; set; }
        public Style? Style { get; set; }
        public bool? Replace { get; set; }
        public string? Text { get; set; }
        public string? Action { get; set; }
        /// <summary>Text entered for an input action (applyAction with an action whose info carries <c>input</c>).</summary>
        public string? Input { get; set; }
        public string? Json { get; set; }
        public string? Payload { get; set; }
        public SketchObject? Object { get; set; }
        public DrawCapabilities? Capabilities { get; set; }
        public bool? Snap { get; set; }
        /// <summary>setPoint: false snaps the point without attaching it to the target ("Anchor" off); default true.</summary>
        public bool? Anchor { get; set; }
        /// <summary>Marquee mode: "enclosed" (left to right) or "touching" (right to left).</summary>
        public string? Mode { get; set; }
        public bool? Add { get; set; }
        public bool? Monochrome { get; set; }
        public string? Background { get; set; }
        public double? Padding { get; set; }
        public bool? IncludeSvg { get; set; }
        public SnapOptions? SnapOptions { get; set; }
        public double? PageWidth { get; set; }
        public double? PageHeight { get; set; }
        public string? Title { get; set; }
        public bool? Indented { get; set; }
        /// <summary>Host rectangle before a native edit (pt).</summary>
        public PlanRect? From { get; set; }
        /// <summary>Host rectangle after a native edit (pt).</summary>
        public PlanRect? Target { get; set; }
        /// <summary>Host rotation change in degrees (clockwise).</summary>
        public double? Rotation { get; set; }
        /// <summary>Edits: false moves or scales without mechanical coupling (gears in mesh, gears on shafts); default true.</summary>
        public bool? Couple { get; set; }
        /// <summary>quickAdd: side of the "+" arrow ("left", "top", "right", "bottom").</summary>
        public string? Side { get; set; }
        /// <summary>Maximum number of results (suggestions).</summary>
        public int? Max { get; set; }
        /// <summary>search: library item ids used last (most recent first), ranked higher.</summary>
        public List<string>? Recent { get; set; }
        /// <summary>Part key of an element (charts v2: "series.0", "axis.x" …) for partActions, applyPartAction and setPartLook.</summary>
        public string? Part { get; set; }
        /// <summary>setPartLook: switch the part off (true) or on (false); null leaves it.</summary>
        public bool? Hidden { get; set; }
        /// <summary>sketchCurve: the pointer trail in page mm as [x, y] pairs.</summary>
        public List<double[]>? Points { get; set; }
    }

    /// <summary>A selectable part of an element (<see cref="InventaSketch.Core.PartInfo"/>).</summary>
    public sealed class PartView
    {
        public string Key { get; set; } = "";
        public string Label { get; set; } = "";
        public string Kind { get; set; } = "";
        public bool Hidden { get; set; }
        /// <summary>The part's own look, if it has one.</summary>
        public Style? Style { get; set; }
    }

    public sealed class ObjectInfo
    {
        public string Id { get; set; } = "";
        public string Type { get; set; } = "";
        public string Name { get; set; } = "";
        /// <summary>box or points.</summary>
        public string Kind { get; set; } = "box";
        public bool Known { get; set; }
        /// <summary>Axis-aligned page bounds (mm).</summary>
        public Rect Bounds { get; set; }
        public double Cx { get; set; }
        public double Cy { get; set; }
        public double W { get; set; }
        public double H { get; set; }
        public double Rotation { get; set; }
        public bool FlipX { get; set; }
        public bool FlipY { get; set; }
        public List<Vec2>? Points { get; set; }
        public string? GroupId { get; set; }
        public bool Locked { get; set; }
        public string? View { get; set; }
        public bool Uniform { get; set; }
        public bool HasText { get; set; }
        public string? Text { get; set; }
        public string? Ref { get; set; }
        /// <summary>Selected objects only: a centred part (pitch circle or hub anchor) that resizes about its centre by default.</summary>
        public bool Centred { get; set; }
        /// <summary>Selected objects only: the main text for quick editing (double-click, bar, pie); null if none.</summary>
        public TextEditInfo? MainText { get; set; }
        /// <summary>Selected objects only: quick-add "+" arrows apply (<see cref="Flow.CanChain"/>).</summary>
        public bool Chain { get; set; }
    }

    /// <summary>Main text of an object (<see cref="EngineApi.MainTextOf"/>); set it with <c>setMainText</c>.</summary>
    public sealed class TextEditInfo
    {
        /// <summary>Parameter key (numeral, text, label, name, title); null for the object text of text types.</summary>
        public string? Key { get; set; }
        /// <summary>Prompt label (localised).</summary>
        public string Label { get; set; } = "";
        public string Value { get; set; } = "";
    }

    /// <summary>Library item offered by "Insert ▸" (<c>suggestions</c>).</summary>
    public sealed class SuggestionInfo
    {
        public string Id { get; set; } = "";
        public string TypeId { get; set; } = "";
        public string Name { get; set; } = "";
        public string? View { get; set; }
        public string Category { get; set; } = "";
        public bool Points { get; set; }
        /// <summary>search: fits the selection (placed where it fits when inserted with the selection).</summary>
        public bool Fits { get; set; }
    }

    /// <summary>Which quick transforms change the selection (<c>transformEffect</c>).</summary>
    public sealed class TransformEffectInfo
    {
        public bool MirrorH { get; set; }
        public bool MirrorV { get; set; }
        public bool Rotate90 { get; set; }
    }

    /// <summary>Selection-wide operations that change something (<c>arrangeOptions</c>): ids such as "align:row", "mview:axial".</summary>
    public sealed class ArrangeOption
    {
        public string Id { get; set; } = "";
        public string Label { get; set; } = "";
        public string Icon { get; set; } = "";
    }

    /// <summary>Path of a joint of a self-built linkage (<c>trace</c>).</summary>
    public sealed class TraceInfo
    {
        public List<Vec2> Points { get; set; } = new();
        public bool Closed { get; set; }
    }

    public sealed class EditorState
    {
        public string Session { get; set; } = "";
        public int Revision { get; set; }
        public string PageId { get; set; } = "";
        public double PageWidth { get; set; }
        public double PageHeight { get; set; }
        public double Grid { get; set; }
        public List<string> Selection { get; set; } = new();
        public Rect? SelectionBounds { get; set; }
        public bool CanUndo { get; set; }
        public bool CanRedo { get; set; }
        public string? UndoLabel { get; set; }
        public List<ObjectInfo> Objects { get; set; } = new();
        /// <summary>Page SVG (mm user units), unless the request set includeSvg=false.</summary>
        public string? Svg { get; set; }
        /// <summary>Document plan scale denominator (100 = 1:100), null if unset.</summary>
        public double? PlanScale { get; set; }
        /// <summary>Document black-and-white setting (<c>setMonochrome</c>).</summary>
        public bool Monochrome { get; set; }
        /// <summary>ISO 128 line group of the document (thick line width in mm, <c>setLineGroup</c>); 0.5 by default.</summary>
        public double LineGroup { get; set; }
        /// <summary>Accent colour of the document (#rrggbb, <c>setAccent</c>); null = each module's own colours.</summary>
        public string? Accent { get; set; }
        /// <summary>
        /// Objects added or modified by the document change that produced <see cref="Revision"/> (edit, undo or redo);
        /// empty after open/new. Hosts redraw these units.
        /// </summary>
        public List<string> Changed { get; set; } = new();
        /// <summary>
        /// Further existing objects whose rendering depends on the changed or removed ones (wall joints, openings in walls,
        /// see <see cref="InventaSketch.Core.ObjectType.Dependents"/>); hosts redraw their units in the same undo step.
        /// </summary>
        public List<string> Dependents { get; set; } = new();
    }

    public sealed class CatalogCategory
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Module { get; set; } = "";
        public int Order { get; set; }
    }

    public sealed class CatalogItem
    {
        public string Id { get; set; } = "";
        public string TypeId { get; set; } = "";
        public string Name { get; set; } = "";
        public string Category { get; set; } = "";
        public List<string> Keywords { get; set; } = new();
        public string Thumbnail { get; set; } = "";
        public bool Points { get; set; }
    }

    public sealed class Catalog
    {
        public string Lang { get; set; } = "en";
        public List<CatalogCategory> Categories { get; set; } = new();
        public List<CatalogItem> Items { get; set; } = new();
    }

    public sealed class ActionInfo
    {
        public string Id { get; set; } = "";
        public string Label { get; set; } = "";
        public string Icon { get; set; } = "";
        public bool Enabled { get; set; } = true;
        public bool Active { get; set; }
        public string Group { get; set; } = "";
        /// <summary>generic, view or type.</summary>
        public string Kind { get; set; } = "generic";
        /// <summary>Set for actions that need a text value: hosts prompt for it and pass it as <c>input</c> to applyAction.</summary>
        public ActionInputInfo? Input { get; set; }
        /// <summary>Parameters the action sets (<see cref="ContextAction.ParamKeys"/>): hide it where those are choice buttons.</summary>
        public List<string>? ParamKeys { get; set; }
    }

    public sealed class ActionInputInfo
    {
        /// <summary>Prompt label (localised).</summary>
        public string Label { get; set; } = "";
        /// <summary>Current value, prefilled in the prompt.</summary>
        public string Value { get; set; } = "";
        public string? Unit { get; set; }
    }

    public sealed class ParamInfo
    {
        public string Key { get; set; } = "";
        public string Label { get; set; } = "";
        public string Kind { get; set; } = "number";
        public double? Min { get; set; }
        public double? Max { get; set; }
        public double? Step { get; set; }
        public string? Unit { get; set; }
        public bool Advanced { get; set; }
        public List<ActionInfo>? Options { get; set; }
        /// <summary>Option icons are drawn mirrored (start arrowheads).</summary>
        public bool MirrorIcons { get; set; }
        public ParamValue? Value { get; set; }
        public ParamValue? Default { get; set; }
    }

    public sealed class InspectorInfo
    {
        public List<string> Ids { get; set; } = new();
        public string? TypeId { get; set; }
        public string? TypeName { get; set; }
        public bool Points { get; set; }
        public List<ActionInfo> Views { get; set; } = new();
        public string? View { get; set; }
        public List<ParamInfo> Params { get; set; } = new();
        public Style? Style { get; set; }
        public bool HasText { get; set; }
        public string? Text { get; set; }
        public string? Ref { get; set; }
        /// <summary>True if the selected type uses the document plan scale (the inspector then offers it).</summary>
        public bool UsesPlanScale { get; set; }
        /// <summary>Document plan scale denominator, null if unset.</summary>
        public double? PlanScale { get; set; }
    }

    public sealed class HitResult
    {
        public string? Id { get; set; }
        /// <summary>Hit object plus its assembly members.</summary>
        public List<string> Ids { get; set; } = new();
        /// <summary>All objects under the point, top first.</summary>
        public List<string> Stack { get; set; } = new();
    }

    public sealed class SnapResultDto
    {
        public double Dx { get; set; }
        public double Dy { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public string Kind { get; set; } = "none";
        public IReadOnlyList<SnapGuide> Guides { get; set; } = new List<SnapGuide>();
        public Attachment? Attach { get; set; }
        /// <summary>What the snap docks onto, for a label at the target ("pitch circle", "axis", "pin 2"), else null.</summary>
        public string? Label { get; set; }
    }
}
