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

    public sealed record ParamOption(string Value, I18nText Label)
    {
        /// <summary>
        /// Pictogram of the option (<see cref="IconIds"/>). When every option of a choice has one, hosts show the choice right
        /// in the floating bar as an icon button with the options as an icon row (no text, no submenu).
        /// </summary>
        public string? Icon { get; init; }
    }

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
        /// <summary>Option pictograms are drawn mirrored (start arrowheads point left).</summary>
        public bool MirrorIcons { get; init; }

        /// <summary>True if every option has a pictogram of the shared set: the choice belongs in the floating bar.</summary>
        public bool HasOptionIcons => Kind == ParamKind.Choice && Options is { Count: > 1 } && Options.All(o => o.Icon is not null && IconIds.All.Contains(o.Icon));

        /// <summary>The same choice with option pictograms from <paramref name="iconOf"/> (value to icon id, null = none).</summary>
        public ParamDef WithOptionIcons(Func<string, string?> iconOf, bool mirrored = false)
            => this with { Options = Options?.Select(o => o with { Icon = iconOf(o.Value) }).ToList(), MirrorIcons = mirrored };
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
        public const string Chart = "view-chart";           // chart: axes with a curve
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
        public const string Insert = "cmd-insert";          // insert a fitting element (frame with plus)
        public const string AlignRow = "cmd-align-row";     // centres on one horizontal line
        public const string AlignColumn = "cmd-align-column"; // centres on one vertical line
        public const string DistributeH = "cmd-distribute-h"; // equal spacing left to right
        public const string DistributeV = "cmd-distribute-v"; // equal spacing top to bottom
        public const string Renumber = "cmd-renumber";      // reference numerals in reading order
        public const string SelectAssembly = "cmd-select-assembly"; // select every part coupled with this one
        public const string FitAround = "cmd-fit-around";   // size a housing/frame around what it contains
        public const string MatchFormat = "cmd-match-format"; // take view, parameters and style of the first element
        public const string Trace = "cmd-trace";            // draw the path a point runs through (coupler curve); only that
        public const string Pencil = "cmd-pencil";          // draw by hand (sketch a curve or line with the mouse)
        public const string Dimension = "cmd-dimension";    // a dimension between two parts

        // Option pictograms (line ends, line styles, line courses): a short line with the feature on the right.
        public const string HeadNone = "head-none";
        public const string HeadTriangle = "head-triangle";
        public const string HeadOpen = "head-open";
        public const string HeadStealth = "head-stealth";
        public const string HeadDiamond = "head-diamond";
        public const string HeadDot = "head-dot";
        public const string HeadTick = "head-tick";
        public const string HeadHalf = "head-half";
        public const string HeadTriangleHollow = "head-triangle-hollow";
        public const string HeadDiamondHollow = "head-diamond-hollow";
        public const string SizeSmall = "size-small";
        public const string SizeMedium = "size-medium";
        public const string SizeLarge = "size-large";
        public const string LineSolid = "line-solid";
        public const string LineDash = "line-dash";
        public const string LineDot = "line-dot";
        public const string LineDashDot = "line-dashdot";
        public const string LineDashDotDot = "line-dashdotdot";
        public const string LineLongDash = "line-longdash";
        public const string CourseStraight = "course-straight";
        public const string CourseCurved = "course-curved";
        public const string CourseWavy = "course-wavy";
        public const string CourseZigzag = "course-zigzag";
        public const string CourseElbow = "course-elbow";

        // Drivetrain option pictograms (schematic, shaft horizontal).
        public const string MachInner = "mach-inner";                // radial flux, inner rotor
        public const string MachOuter = "mach-outer";                // radial flux, outer rotor
        public const string MachAxial = "mach-axial";                // axial flux, one stator, one rotor
        public const string MachAxialDoubleRotor = "mach-axial-2r";  // rotor | stator | rotor
        public const string MachAxialDoubleStator = "mach-axial-2s"; // stator | rotor | stator
        public const string MachMagnet = "mach-pm";                  // permanent magnets
        public const string MachWound = "mach-wound";                // field winding
        public const string MachCage = "mach-cage";                  // squirrel cage
        public const string MachReluctance = "mach-srm";             // salient poles
        public const string HubNone = "hub-none";
        public const string HubFixed = "hub-fixed";
        public const string HubSliding = "hub-sliding";
        public const string HubLoose = "hub-loose";
        public const string Helix = "cmd-helix";
        public const string ShaftEnds = "cmd-shaft-ends";
        public const string Ground = "cmd-ground";
        public const string Split = "cmd-split";
        public const string Hop = "cmd-hop";
        public const string Explode = "cmd-explode";
        public const string Motion = "cmd-motion";
        public const string Junction = "cmd-junction";
        public const string RemovePoint = "cmd-point-remove";
        public const string ShapeSquare = "shape-square";
        public const string ShapeCircle = "shape-circle";
        public const string ShapeTriangle = "shape-triangle";
        public const string FillTint = "fill-tint";
        public const string FillHatch = "fill-hatch";
        public const string FillDots = "fill-dots";
        public const string FillNone = "fill-none";
        public const string RouteFree = "route-free";
        public const string RouteOrthogonal = "route-ortho";
        public const string Route45 = "route-45";
        public const string WallThick = "wall-thick";
        public const string WallMedium = "wall-medium";
        public const string WallThin = "wall-thin";

        // ---- Action pictograms with one meaning each (UX review 2026-11-25, F3). Text ("T") is only "edit the text".
        // Label-only markers: hosts never draw these alone, the label carries the meaning.
        public const string Option = "cmd-option";            // pick this option of a choice (radio button); label-only
        // Values, names, labels
        public const string Value = "cmd-value";              // enter a value (input field with a cursor)
        public const string Name = "cmd-name";                // enter a name, designation, tag or title (label tag)
        public const string LabelShow = "cmd-label-show";     // show the element's designation label (part with a label beside it)
        public const string PortLabels = "cmd-port-labels";   // show the port names (pins with short labels)
        public const string Formula = "cmd-formula";          // a formula f(x) ("fx")
        public const string Values = "cmd-values";            // the element's values and how they follow from each other ("x=")
        public const string Parameters = "cmd-parameters";   // coefficients / a parameter set (three sliders)
        public const string Code = "cmd-code";                // the specification as code (braces)
        public const string Data = "cmd-data";                // the element's data (spreadsheet with a header row)
        public const string Legend = "cmd-legend";            // a legend (swatches with lines)
        public const string Visibility = "cmd-visibility";    // show / hide (an eye)
        public const string Grid = "cmd-grid";                // grid lines on / off
        public const string AxisTicks = "cmd-axis-ticks";     // tick numbers on the axes
        public const string Smooth = "cmd-smooth";            // smooth the curve (corner polygon with a smooth curve)
        public const string PointsShow = "cmd-points";        // mark the points / waypoints (polyline with dots)
        public const string Contour = "cmd-contour";          // contour levels (nested closed curves)
        public const string Function = "cmd-function";        // function generator (a wave in a box)
        public const string Marker = "cmd-marker";            // vertical marker line with a flag
        public const string ValueLine = "cmd-value-line";     // horizontal value / limit line on axes
        public const string HatchBetween = "cmd-hatch-between"; // hatch the region between two curves
        public const string HatchBelow = "cmd-hatch-below";   // hatch the area under a curve
        public const string BreakX = "cmd-break-x";           // break the x axis here
        public const string BreakY = "cmd-break-y";           // break the y axis here
        public const string BreakXRemove = "cmd-break-x-remove"; // remove the x-axis breaks
        public const string BreakYRemove = "cmd-break-y-remove"; // remove the y-axis breaks
        public const string Ghosts = "cmd-ghosts";            // ghost positions (a link in three faded positions)
        public const string RayBundle = "cmd-rays";           // add a ray bundle (three parallel rays)
        // Curve shapes of a sketched curve (options of the "Curve shape" choice; one family: the same hump, drawn five ways)
        public const string CurveSmoothed = "curve-smoothed"; // smoothed (a calm curve with the removed jitter as dots on both sides)
        public const string CurveSpline = "curve-spline";     // interpolated spline (a smooth curve exactly through three ringed points)
        public const string CurveFitted = "curve-fitted";     // fitted function (a function curve with measured points beside it, not on it)
        public const string CurveSegments = "curve-segments"; // straight segments (straight lines between three ringed points)
        public const string CurveRaw = "curve-raw";           // as drawn (a shaky hand-drawn line)
        // Quantities (value entry of one physical quantity)
        public const string QtyAngle = "qty-angle";           // angle (two rays with an arc)
        public const string QtyLength = "qty-length";         // length / distance (ruler)
        public const string QtyWidth = "qty-width";           // width (horizontal dimension between two walls)
        public const string QtyHeight = "qty-height";         // height above a floor (vertical dimension)
        public const string QtyThickness = "qty-thickness";   // thickness (two arrows pointing at a slab)
        public const string QtyArea = "qty-area";             // area (tinted rectangle)
        public const string QtyVolume = "qty-volume";         // volume (beaker with a level)
        public const string QtyMass = "qty-mass";             // mass (weight with a handle)
        public const string QtyPressure = "qty-pressure";     // pressure (manometer)
        public const string QtyPower = "qty-power";           // power (lightning bolt)
        public const string QtyVoltage = "qty-voltage";       // voltage (cell symbol: long and short plate)
        public const string QtyCapacity = "qty-capacity";     // storage capacity (battery with a size arrow)
        public const string QtyCharge = "qty-charge";         // state of charge (half-filled battery)
        public const string QtyTime = "qty-time";             // time (clock)
        public const string QtyFrequency = "qty-frequency";   // frequency (one sine period)
        public const string QtyTolerance = "qty-tolerance";   // tolerance (a line inside a dashed band)
        public const string QtyFlow = "qty-flow";             // flow rate (arrow inside a pipe)
        public const string RangeX = "qty-range-x";           // x range of an axis (brackets on a horizontal axis)
        public const string RangeY = "qty-range-y";           // y range of an axis (brackets on a vertical axis)
        // Structure: add / remove parts of an element; more / fewer of a repeated part
        public const string AddPoint = "cmd-point-add";       // add a point / corner / waypoint
        public const string RowAdd = "cmd-row-add";           // add a row (table, list, lane, signal) at the bottom
        public const string RowAddTop = "cmd-row-add-top";    // add a row at the top / above (entry activity, lane above)
        public const string RowRemove = "cmd-row-remove";
        public const string ColumnAdd = "cmd-column-add";     // add a column (table, field, tab)
        public const string ColumnRemove = "cmd-column-remove";
        public const string OperationAdd = "cmd-operation-add"; // add an operation row "()" (UML class)
        public const string OperationRemove = "cmd-operation-remove";
        public const string PortAdd = "cmd-port-add";         // add a connection point / port
        public const string PortRemove = "cmd-port-remove";
        public const string CountMore = "cmd-count-more";     // one more of a repeated part (teeth, poles, modules …); with its label
        public const string CountFewer = "cmd-count-fewer";   // one fewer of a repeated part; with its label
        // Direction, side, angle
        public const string Reverse = "cmd-reverse";          // reverse the direction (U-turn arrow)
        public const string SideSwap = "cmd-side-swap";       // put it on the other side of its line
        public const string Polarity = "cmd-polarity";        // swap the polarity / channel (+ and − exchanging places)
        public const string FlowArrow = "cmd-flow-arrow";     // direction / flow arrow on the line
        public const string JointStart = "cmd-joint-start";  // a joint at the start of a link
        public const string JointEnd = "cmd-joint-end";       // a joint at the end of a link
        public const string AngleNext = "cmd-angle-next";     // next position: turn a joint by one step clockwise
        public const string AnglePrev = "cmd-angle-prev";     // previous position: one step counter-clockwise
        public const string Home = "cmd-home";                // home position (a house)
        public const string AlignWall = "cmd-align-wall";     // align to the wall it sits on
        public const string TextAlignLeft = "text-align-left";
        public const string TextAlignCenter = "text-align-center";
        public const string TextAlignRight = "text-align-right";
        public const string HeadOpenDashed = "head-open-dashed";             // dashed line with an open arrowhead (dependency, reply)
        public const string HeadTriangleHollowDashed = "head-triangle-hollow-dashed"; // dashed line with a hollow triangle (realization)
        public const string CreateObject = "cmd-create-object"; // a message creating an object (dashed arrow into a box)

        /// <summary>
        /// Pictograms hosts never show without their label: they say "switch", "next variant" or "pick" but not what. Actions
        /// with them appear as text buttons (bar) or text rows (pie); several actions of one element may share them.
        /// </summary>
        public static bool IsLabelOnly(string? icon) => icon is Toggle or Cycle or Option;

        /// <summary>
        /// Step pictograms ("+"/"−", one more/fewer): they come in pairs of the same value and hosts show them together with
        /// the (short) label, so several pairs on one element stay distinguishable.
        /// </summary>
        public static bool IsStep(string? icon) => icon is Increase or Decrease or CountMore or CountFewer;

        /// <summary>
        /// Pictogram for entering a value with the given unit (or parameter key): a quantity pictogram where the unit tells
        /// the quantity, otherwise <see cref="Value"/>.
        /// </summary>
        public static string ForQuantity(string? unit, string? key = null)
        {
            var u = (unit ?? "").Trim();
            var k = (key ?? "").ToLowerInvariant();
            if (u is "°" or "deg" or "rad" || k.Contains("angle")) return QtyAngle;
            if (u is "kW" or "W" or "MW" or "kWp" or "GW" or "kVA" or "MVA") return QtyPower;
            if (u is "V" or "kV" or "mV") return QtyVoltage;
            if (u is "kWh" or "MWh" or "Wh" or "Ah" or "mAh") return QtyCapacity;
            if (u is "bar" or "Pa" or "kPa" or "MPa" or "psi") return QtyPressure;
            if (u is "m³" or "m3" or "l" or "L" or "dm³") return QtyVolume;
            if (u is "kg" or "g" or "t") return QtyMass;
            if (u is "s" or "ms" or "min" or "h") return QtyTime;
            if (u is "Hz" or "kHz" or "rad/s" or "1/s") return QtyFrequency;
            if (u is "m²" or "m2") return QtyArea;
            if (u is "%" && (k.Contains("soc") || k.Contains("charge"))) return QtyCharge;
            if (u is "mm" or "m" or "cm" or "km") return k.Contains("width") ? QtyWidth : k.Contains("height") ? QtyHeight : k.Contains("thick") ? QtyThickness : QtyLength;
            return Value;
        }

        /// <summary>Pictogram of a shape choice ("square", "round"/"circle", "triangle"); null if unknown.</summary>
        public static string? ForShape(string value) => value switch
        {
            "square" or "rect" or "box" => ShapeSquare,
            "round" or "circle" => ShapeCircle,
            "triangle" => ShapeTriangle,
            _ => null,
        };

        /// <summary>Pictogram of a fill choice ("tint", "hatch", "dots", "none"); null if unknown.</summary>
        public static string? ForFill(string value) => value switch
        {
            "tint" or "solid" => FillTint,
            "hatch" => FillHatch,
            "dots" => FillDots,
            "none" => FillNone,
            _ => null,
        };

        /// <summary>Pictogram of a line routing ("free", "orthogonal", "45"); null if unknown.</summary>
        public static string? ForRoute(string value) => value switch
        {
            "free" => RouteFree,
            "orthogonal" => RouteOrthogonal,
            "45" => Route45,
            _ => null,
        };

        /// <summary>Pictogram of an arrowhead or line-end name ("triangle", "open", "oval"/"dot" ...); null if unknown.</summary>
        public static string? ForHead(string name) => name switch
        {
            "none" or "plain" => HeadNone,
            "triangle" or "arrow" => HeadTriangle,
            "open" => HeadOpen,
            "stealth" => HeadStealth,
            "diamond" => HeadDiamond,
            "oval" or "dot" => HeadDot,
            "tick" => HeadTick,
            "half" => HeadHalf,
            "triangleOpen" => HeadTriangleHollow,
            "diamondOpen" => HeadDiamondHollow,
            _ => null,
        };

        /// <summary>Pictogram of a line style.</summary>
        public static string ForDash(DashStyle dash) => dash switch
        {
            DashStyle.Dash => LineDash,
            DashStyle.Dot => LineDot,
            DashStyle.DashDot => LineDashDot,
            DashStyle.DashDotDot => LineDashDotDot,
            DashStyle.LongDash => LineLongDash,
            _ => LineSolid,
        };

        /// <summary>Every icon id, for hosts and tests that must provide a pictogram for each.</summary>
        public static IReadOnlyList<string> All { get; } = new[]
        {
            Schematic, Side, Section, Axial, Teeth, Top, Front, Rear, Iso, Detailed, Simplified, Symbol, Block, Plan, Exploded, Table, Pin, Chart,
            Delete, Duplicate, MirrorH, MirrorV, Rotate, RotateCcw, Increase, Decrease, Swap, ArrowStart, ArrowEnd, Text, Toggle, Cycle, Connect, Animate, Medium, Actuation, Insert,
            AlignRow, AlignColumn, DistributeH, DistributeV, Renumber, SelectAssembly, FitAround, MatchFormat, Trace, Pencil, Dimension,
            HeadNone, HeadTriangle, HeadOpen, HeadStealth, HeadDiamond, HeadDot, HeadTick, HeadHalf, HeadTriangleHollow, HeadDiamondHollow,
            SizeSmall, SizeMedium, SizeLarge, LineSolid, LineDash, LineDot, LineDashDot, LineDashDotDot, LineLongDash,
            CourseStraight, CourseCurved, CourseWavy, CourseZigzag, CourseElbow,
            MachInner, MachOuter, MachAxial, MachAxialDoubleRotor, MachAxialDoubleStator, MachMagnet, MachWound, MachCage, MachReluctance,
            HubNone, HubFixed, HubSliding, HubLoose, Helix, ShaftEnds, Ground,
            Split, RemovePoint, Hop, Junction, Explode, Motion,
            ShapeSquare, ShapeCircle, ShapeTriangle, FillTint, FillHatch, FillDots, FillNone, RouteFree, RouteOrthogonal, Route45,
            WallThick, WallMedium, WallThin,
            Option, Value, Values, Name, LabelShow, PortLabels, Formula, Parameters, Code, Data, Legend, Visibility, Grid, AxisTicks, Smooth, PointsShow,
            Contour, Function, Marker, ValueLine, HatchBetween, HatchBelow, BreakX, BreakY, BreakXRemove, BreakYRemove, Ghosts, RayBundle,
            CurveSmoothed, CurveSpline, CurveFitted, CurveSegments, CurveRaw,
            QtyAngle, QtyLength, QtyWidth, QtyHeight, QtyThickness, QtyArea, QtyVolume, QtyMass, QtyPressure, QtyPower, QtyVoltage, QtyCapacity,
            QtyCharge, QtyTime, QtyFrequency, QtyTolerance, QtyFlow, RangeX, RangeY,
            AddPoint, RowAdd, RowAddTop, RowRemove, ColumnAdd, ColumnRemove, OperationAdd, OperationRemove, PortAdd, PortRemove, CountMore, CountFewer,
            Reverse, SideSwap, Polarity, FlowArrow, JointStart, JointEnd, AngleNext, AnglePrev, Home, AlignWall,
            TextAlignLeft, TextAlignCenter, TextAlignRight, HeadOpenDashed, HeadTriangleHollowDashed, CreateObject,
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
        /// <summary>Cone apex of a bevel gear (on its axis): a mating bevel gear shares it (<see cref="Building"/>).</summary>
        Apex,
        /// <summary>
        /// Pitch line of a rack (segment, <see cref="Anchor.PitchLine"/>): a gear seen along its axis whose pitch circle touches
        /// it on the teeth side (<see cref="Anchor.TeethSide"/>) is its pinion: snapping places the gear tangent to the line,
        /// the coupling keeps it there and the moving gear train moves the rack (<see cref="GearTrain"/>).
        /// </summary>
        PitchLine,
    }

    /// <summary>
    /// Section of a shaft axis with its own radius (a stepped shaft): from <see cref="From"/> to <see cref="To"/> as
    /// fractions of the axis segment (P = 0, Q = 1).
    /// </summary>
    public sealed record AxisStep(double From, double To, double Radius);

    /// <summary>
    /// Snap anchor. Point: P. Circle: centre P with Radius. Segment: P to Q. Coordinates are local
    /// (box objects) or page coordinates (point objects) as returned by <see cref="ObjectType.Anchors"/>;
    /// <see cref="Registry.PageAnchors"/> always returns page coordinates.
    /// <para>
    /// Shaft fit (<see cref="ShaftFit"/>): a <see cref="AnchorRole.Hub"/> point may carry the radius of the part's bore and an
    /// <see cref="AnchorRole.Axis"/> segment the radius of the shaft (<see cref="Steps"/> for a stepped shaft). 0 means
    /// unknown: such anchors snap and couple as before, only nothing is fitted.
    /// </para>
    /// </summary>
    public sealed record Anchor(AnchorKind Kind, AnchorRole Role, Vec2 P, Vec2 Q = default, double Radius = 0)
    {
        /// <summary>A hub point with the radius of the part's bore at its current size (<see cref="ShaftFit"/>).</summary>
        public static Anchor Hub(Vec2 p, double boreRadius, string? id = "hub") => new(AnchorKind.Point, AnchorRole.Hub, p, default, Math.Max(0, boreRadius)) { Id = id };

        /// <summary>
        /// A shaft axis from <paramref name="a"/> to <paramref name="b"/> with the shaft radius; <paramref name="steps"/> give the
        /// radius of the sections of a stepped shaft (positions as fractions of the segment), elsewhere <paramref name="radius"/>.
        /// </summary>
        public static Anchor Shaft(Vec2 a, Vec2 b, double radius, IReadOnlyList<AxisStep>? steps = null)
            => new(AnchorKind.Segment, AnchorRole.Axis, a, b, Math.Max(0, radius)) { Steps = steps };

        /// <summary>Radii of the sections of a stepped shaft axis (see <see cref="Shaft"/>), else null.</summary>
        public IReadOnlyList<AxisStep>? Steps { get; init; }

        /// <summary>
        /// The radius at <paramref name="p"/>: for a stepped axis the radius of the section that the projection of the point
        /// falls into, else <see cref="Radius"/>.
        /// </summary>
        public double RadiusAt(Vec2 p)
        {
            if (Kind != AnchorKind.Segment || Steps is not { Count: > 0 } steps) return Radius;
            var (_, t) = Geo.ClosestOnSegment(p, P, Q);
            foreach (var s in steps)
                if (t >= Math.Min(s.From, s.To) - 1e-9 && t <= Math.Max(s.From, s.To) + 1e-9) return s.Radius;
            return Radius;
        }

        /// <summary>
        /// Optional id, unique per object (pins, ports, mounts). Attachments refer to it via
        /// <see cref="Attachment.AnchorId"/>, so ids of connection points must be stable across parameter changes.
        /// </summary>
        public string? Id { get; init; }
        /// <summary>Id of the object that contributed the anchor (set by <see cref="Snapper.Targets"/>; null for page anchors).</summary>
        public string? Owner { get; init; }
        public static Anchor Point(AnchorRole role, Vec2 p, string? id = null) => new(AnchorKind.Point, role, p) { Id = id };

        /// <summary>
        /// A point anchor with a direction (<see cref="Q"/> = a point one unit along it): the outward direction of a connection
        /// face, e.g. a robot flange along the tool axis, or a tool's mount pointing back to the flange. Parts docked there
        /// with "+" are turned so the two directions face each other (<see cref="Building"/>) and, when both faces give their
        /// radius (<see cref="Radius"/>), scaled so the faces match; docked parts follow the face (<see cref="Coupling"/>).
        /// </summary>
        public static Anchor Directed(AnchorRole role, Vec2 p, Vec2 dir, string? id = null, double faceRadius = 0)
            => new(AnchorKind.Point, role, p, p + dir.Normalized(), faceRadius) { Id = id, HasDirection = true };

        /// <summary>True for point anchors with a direction (<see cref="Directed"/>).</summary>
        public bool HasDirection { get; init; }

        /// <summary>
        /// Further mount ids a <see cref="AnchorRole.Mount"/> anchor docks onto besides its own <see cref="Id"/> (null = only its
        /// own id). Ids stay unique per object (attachments, build ports and <see cref="Coupling"/> refer to them); this names
        /// the compatible faces instead: a gripper's mount "flange" that accepts "tool" docks with "+" on a robot flange
        /// ("flange") and on the tool side of a tool changer or sensor ("tool"); snapping prefers such compatible faces. When
        /// a part has several mounts that dock on a port, the one that accepts the port's id explicitly is used (a tool changer
        /// docks on another changer's tool side with its flange, not with its own tool side).
        /// </summary>
        public IReadOnlyList<string>? Accepts { get; init; }

        /// <summary>True if this mount docks onto a mount with id <paramref name="mountId"/>: its own id or one it <see cref="Accepts"/>.</summary>
        public bool DocksOn(string? mountId) => mountId is not null && (Id == mountId || Accepts is { } acc && acc.Contains(mountId));

        /// <summary>
        /// True for a pitch circle that does not mesh (belt pulley, chain sprocket, idler: it only sizes the strand). Set by
        /// <see cref="Registry.PageAnchors"/> from <see cref="IPitchMesh"/>; <see cref="Snapper"/> and <see cref="Building"/> offer
        /// no mesh with it.
        /// </summary>
        public bool NoMesh { get; init; }

        /// <summary>The unit direction of a directed point anchor, else null.</summary>
        public Vec2? Direction => HasDirection && (Q - P).Length > 1e-9 ? (Q - P).Normalized() : null;
        public static Anchor Circle(AnchorRole role, Vec2 c, double r) => new(AnchorKind.Circle, role, c, default, r);
        public static Anchor Segment(AnchorRole role, Vec2 a, Vec2 b) => new(AnchorKind.Segment, role, a, b);
        /// <summary>A <see cref="AnchorRole.Rail"/> segment from <paramref name="a"/> to <paramref name="b"/>.</summary>
        public static Anchor Rail(Vec2 a, Vec2 b, string? id = null) => new(AnchorKind.Segment, AnchorRole.Rail, a, b) { Id = id };

        /// <summary>
        /// Pitch circle of a gear whose axis lies in the drawing plane (side or schematic view): the radius is the half height,
        /// <see cref="Q"/> a point one unit along the axis. Such gears mesh only with gears of the same kind on a parallel
        /// axis, across the axis (<see cref="Coupling"/>, <see cref="Snapper"/>).
        /// </summary>
        public static Anchor SidePitch(Vec2 centre, double radius, Vec2 axisPoint) => new(AnchorKind.Circle, AnchorRole.Pitch, centre, axisPoint, radius) { Id = "pitch-side" };

        /// <summary>Unit axis direction of a side-view pitch circle (<see cref="SidePitch"/>), else null.</summary>
        public Vec2? AxisOf() => Kind == AnchorKind.Circle && Q != default && Q.DistanceTo(P) > 1e-9 ? (Q - P) * (1 / Q.DistanceTo(P)) : null;

        /// <summary>
        /// The pitch line of a rack from <paramref name="a"/> to <paramref name="b"/> (<see cref="AnchorRole.PitchLine"/>). The
        /// teeth (and the pinion) lie to the left of a → b on screen (y down): (b − a) turned by −90°, see <see cref="TeethSide"/>.
        /// A mirrored placement keeps the side (<see cref="Transform"/> swaps the ends).
        /// </summary>
        public static Anchor PitchLine(Vec2 a, Vec2 b, string? id = "pitch") => new(AnchorKind.Segment, AnchorRole.PitchLine, a, b) { Id = id };

        /// <summary>Unit normal of a rack pitch line towards its teeth (the side its pinion sits on), else null.</summary>
        public Vec2? TeethSide()
        {
            if (Role != AnchorRole.PitchLine || Kind != AnchorKind.Segment || (Q - P).Length < 1e-9) return null;
            var d = (Q - P).Normalized();
            return new Vec2(d.Y, -d.X);
        }

        public Anchor Transform(Matrix2D m) => Kind switch
        {
            // A rack pitch line keeps its teeth side: a mirroring placement swaps its ends.
            AnchorKind.Segment when Role == AnchorRole.PitchLine && m.Determinant < 0 => this with { P = m.Apply(Q), Q = m.Apply(P) },
            // Point and segment radii (bore, shaft, face) scale with the mean linear scale; page placement is rigid.
            AnchorKind.Point => HasDirection ? this with { P = m.Apply(P), Q = m.Apply(Q), Radius = Radius * Math.Sqrt(Math.Abs(m.Determinant)) }
                : Radius > 0 ? this with { P = m.Apply(P), Radius = Radius * Math.Sqrt(Math.Abs(m.Determinant)) } : this with { P = m.Apply(P) },
            AnchorKind.Segment => Radius > 0 || Steps is not null
                ? this with
                {
                    P = m.Apply(P), Q = m.Apply(Q), Radius = Radius * Math.Sqrt(Math.Abs(m.Determinant)),
                    Steps = Steps?.Select(s => s with { Radius = s.Radius * Math.Sqrt(Math.Abs(m.Determinant)) }).ToList(),
                }
                : this with { P = m.Apply(P), Q = m.Apply(Q) },
            // Circles: radius scales with the mean linear scale (free scaling may make it an ellipse visually). A circle with Q
            // set carries an axis direction (pitch circle of a gear seen from the side, see AxisOf).
            _ => this with { P = m.Apply(P), Q = Q == default ? Q : m.Apply(Q), Radius = Radius * Math.Sqrt(Math.Abs(m.Determinant)) },
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
        /// <summary>
        /// Parameters this action sets. Hosts that show those parameters as choices (option pictograms in the bar) leave the
        /// action out of the bar, so nothing appears twice; the pie menu keeps it.
        /// </summary>
        public IReadOnlyList<string>? ParamKeys { get; init; }
        /// <summary>Text value the host asks for before running the action (prompt dialog or inline field).</summary>
        public ActionInput? Input { get; init; }
        /// <summary>
        /// Document-aware apply: receives the current document, the object and a render context of that document and
        /// returns the new document (it may change the object and, where a host supports it, other objects).
        /// </summary>
        public Func<SketchDocument, SketchObject, RenderContext, SketchDocument>? ApplyInDocument { get; init; }
        /// <summary>
        /// The choice this action is an option of (<see cref="ActionChoice"/>): hosts group the options of one choice into a
        /// dropdown (bar) or a "Label: Current ▸" submenu (pie), the current one marked by <see cref="IsActive"/>.
        /// </summary>
        public ActionChoice? Choice { get; init; }
        /// <summary>
        /// A small drawing of what the option gives (mm, centred at 0,0, about 6 mm wide), for options no shared pictogram
        /// fits (fill patterns, colours); hosts render it as the option's picture. Null: the pictogram or the label.
        /// </summary>
        public IReadOnlyList<Primitive>? Preview { get; init; }

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
                var a = registry.Actions(o, ctx).FirstOrDefault(x => x.Id == actionId);
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

        /// <summary>
        /// The registry rendering with this context (set by the registry's render and anchor calls; may be null for a type
        /// called directly). Types that read other objects' types (crossings with other modules' lines, the parts list) use
        /// it rather than <see cref="ObjectType.Owner"/>, which is shared by every registry the type is registered with.
        /// </summary>
        public Registry? Registry { get; internal set; }

        /// <summary>
        /// Where else each designation occurs (comparison form, <see cref="Designations.Normalize"/>) → page labels ("2", "4"),
        /// passed by hosts that keep pages apart (PowerPoint slides): connection lines with such a designation show a
        /// continuation marker "→ W3, slide 4" (<see cref="Designations"/>). Null: no continuations known.
        /// </summary>
        public IReadOnlyDictionary<string, IReadOnlyList<string>>? Continuations { get; set; }

        /// <summary>How continuation markers name the pages ("slide"/"Folie" in PowerPoint); null = "page".</summary>
        public PageNames? PageNames { get; set; }

        /// <summary>A context for <paramref name="registry"/>.</summary>
        public RenderContext For(Registry registry)
        {
            Registry ??= registry;
            return this;
        }
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
        /// The unscaled size of <paramref name="o"/> for its parameters and view (relative: only ratios between objects of this
        /// type count), or null if it does not depend on them. A road with more lanes is naturally wider; "+" uses it to tell
        /// a scaled element from one whose parameters made it bigger (<see cref="Building.SourceScale"/>).
        /// </summary>
        public virtual Size2? NaturalSize(SketchObject o) => null;

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
        /// objects in separate units (PowerPoint COM) render such objects with the neighbouring units as context and redraw
        /// them whenever they move. Types that return <see cref="Dependents"/> but render on their own (the source element
        /// of a linked chart) set <see cref="HasDependents"/> instead.
        /// </summary>
        public virtual bool DependsOnNeighbours => false;

        /// <summary>
        /// True if <see cref="Dependents"/> can return objects. Defaults to <see cref="DependsOnNeighbours"/>; hosts that keep
        /// objects in separate units evaluate the dependents of an edit only for such types.
        /// </summary>
        public virtual bool HasDependents => DependsOnNeighbours;

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
        /// True if the rendering lists or summarises all parts of the document (a parts-list table): every edit of a part
        /// redraws it (<c>ChangeSet.DependentsOf</c> in the engine, the COM host's dependent units). Such objects are never
        /// parts themselves.
        /// </summary>
        public virtual bool DependsOnAllParts => false;

        /// <summary>
        /// How a whole installation of this type switches its view with the arrangement kept (<see cref="AssemblyViews"/>),
        /// e.g. building services between plan, section and schematic; null (default): the mechanical rule of
        /// <see cref="AssemblyViews"/> (axes in the plane ↔ along the axes).
        /// </summary>
        public virtual IAssemblyViewSwitch? AssemblyViewSwitch => null;

        /// <summary>
        /// Page y of the ground (floor) line this object draws in its elevation/section view, the same in every view (a wall:
        /// the lowest point of its centre line); null (default) if it draws none. Installations switched together with such
        /// objects stand on that line (<see cref="InstallationViews"/>).
        /// </summary>
        public virtual double? ElevationGround(SketchObject o, RenderContext ctx) => null;

        /// <summary>
        /// The registry the type was last registered with (set by <see cref="Registry.Register"/>). Types that render from
        /// other objects of the document (a parts list naming each part) use it to read their types and labels.
        /// </summary>
        public Registry? Owner { get; internal set; }

        /// <summary>
        /// Quick add (<see cref="Flow"/>): the library item id (or type id) of the connector that joins an element added next
        /// to <paramref name="o"/> with the "+" arrows; "" chains without a connector (process steps), null (default) offers
        /// no quick add. Flowchart, UML, ER and BPMN elements and basic shapes return their usual connector.
        /// </summary>
        public virtual string? ChainConnector(SketchObject o) => null;

        /// <summary>
        /// Connection ports (<see cref="Building"/>, <see cref="PortKind.Connect"/>): what flows through port
        /// <paramref name="port"/> (a <see cref="AnchorRole.Port"/> anchor of <paramref name="o"/>) and which line connects it
        /// – a DC cable at a PV module, a heating flow pipe at a radiator, a supply-air duct at an air terminal. The build "+"
        /// at the port offers that connector (start docked on the port, free end with its own "+") and the elements with a
        /// port of the same medium. Null (default): the port is a geometric point, not a medium connection.
        /// </summary>
        public virtual PortConnection? Connection(SketchObject o, Anchor port) => null;

        /// <summary>
        /// The medium a connection line carries (<see cref="PortMedia"/>): an electrical wire "electric", a DC cable
        /// "electric.dc", a heating flow pipe "heating.flow". The free end of such a line offers "+" (continue the line, or
        /// dock an element with a port of that medium). Null (default): not a connection line; a line docked on a
        /// connection port then carries that port's medium (<see cref="Building.LineMedium"/>).
        /// </summary>
        public virtual string? LineMedium(SketchObject line) => null;

        /// <summary>
        /// The role of a connection line within its medium when the type knows it from its own parameters (a pilot or drain
        /// line, <see cref="LineMedia"/>), or <see cref="LineMedia.OwnColour"/> for lines that already show their real conductor
        /// colour (the colour mode "by medium" leaves them). Null: the role comes from the generic parameter
        /// <see cref="LineMedia.RoleParam"/> or the medium itself.
        /// </summary>
        public virtual string? MediumRole(SketchObject line) => null;

        /// <summary>
        /// Quick add (<see cref="Flow"/>): parameters for the connector that becomes the <paramref name="index"/>-th one leaving
        /// <paramref name="o"/> (0 = first), set on top of the connector item's own, e.g. the "yes" and "no" exits of a decision.
        /// Null (default) adds nothing.
        /// </summary>
        public virtual IReadOnlyDictionary<string, ParamValue>? ChainConnectorParams(SketchObject o, int index) => null;

        /// <summary>
        /// Point objects whose corners can be inserted and removed freely (wires, pipes, roads, polylines): hosts show "+"
        /// handles on the segments (<see cref="Polylines"/>). Lines with a fixed number of points (dimensions, arrows) say no.
        /// </summary>
        public virtual bool InsertablePoints => false;

        /// <summary>
        /// A closed outline (room, region, hatched polygon, closed wall loop or polyline): its points form a loop, so it has
        /// no free ends to extend and its last segment runs back to the first point (<see cref="Polylines"/>).
        /// </summary>
        public virtual bool IsClosed(SketchObject o) => false;

        /// <summary>
        /// Type a straight two-point line becomes when it is drawn on with corners (line and arrow: polyline, keeping the
        /// arrowheads); null when it cannot get corners (<see cref="Polylines.Extend"/>).
        /// </summary>
        public virtual string? CornerType => null;

        /// <summary>
        /// Annotation lines (lead lines, reference arrows, numerals, dimensions): an end dropped anywhere on another element
        /// (inside its box, also away from any snap point, e.g. on a surface) docks there and follows it
        /// (<see cref="Attachments.OnSurface"/>).
        /// </summary>
        public virtual bool DocksOnSurface => false;

        /// <summary>
        /// Wires and pipes: where this line crosses an earlier line of the same type it draws a hop (bridge), and hosts offer
        /// to connect the two there (<see cref="Crossings"/>).
        /// </summary>
        public virtual bool HopsAtCrossings => false;

        /// <summary>
        /// The parts of a predefined assembly (planetary set, differential …) for "explode" (<see cref="Assemblies"/>): each
        /// becomes an element of its own, coupled as before. Null when the object is no assembly (or not in this view).
        /// </summary>
        public virtual IReadOnlyList<PartSpec>? Parts(SketchObject o) => null;

        /// <summary>Role in self-built linkages (<see cref="Linkage"/>): rigid link, fixed pivot, free joint, slider, or none.</summary>
        public virtual LinkageRole LinkageRole(SketchObject o) => Core.LinkageRole.None;

        /// <summary>
        /// Stops of a fixed pivot (<see cref="Core.LinkageRole.FixedPivot"/>): the links on it may only point between
        /// <c>From</c> and <c>To</c> (page degrees, clockwise on screen from +x, the range runs clockwise from From to To);
        /// null (default) = turns freely.
        /// </summary>
        public virtual (double From, double To)? JointLimits(SketchObject o) => null;

        /// <summary>A cam disc's contour in local coordinates (closed polygon, turns with the object), else null (<see cref="Cams"/>).</summary>
        public virtual IReadOnlyList<Vec2>? CamContour(SketchObject o) => null;

        /// <summary>How followers ride on this cam: "roller" with its radius, "flat" or "knife" (<see cref="Cams"/>).</summary>
        public virtual (string Kind, double Radius) CamFollower(SketchObject o) => ("knife", 0);

        /// <summary>
        /// Directions (local, before rotation) in which a meshing gear can be docked (<see cref="Building"/>): all four sides
        /// in axial views; only across the axis (up, down) where the axis lies in the drawing plane (schematic, side view).
        /// </summary>
        public virtual IReadOnlyList<Vec2> MeshDirections(SketchObject o) => new[] { new Vec2(0, 1), new Vec2(0, -1), new Vec2(1, 0), new Vec2(-1, 0) };

        /// <summary>
        /// Coupling: a drive (e-machine, engine, motor) whose hub sits on a shaft takes the shaft along when it is moved,
        /// instead of sliding along it like a gear (<see cref="Coupling"/>).
        /// </summary>
        public virtual bool DrivesShaft => false;

        /// <summary>
        /// Coupling: a belt or chain whose two end hubs (first and second <see cref="AnchorRole.Hub"/> anchor) lie on the hubs
        /// of two pulleys or sprockets. When a pulley moves, the strand follows: its length and angle change, the other pulley
        /// stays (<see cref="Coupling"/>).
        /// </summary>
        public virtual bool SpansHubs => false;

        /// <summary>
        /// Coupling (<see cref="Coupling"/>): the object scaled so its pitch radius grows by <paramref name="k"/>. Default: the
        /// whole box. Side and schematic views of gears scale only the diameter, not the gear width.
        /// </summary>
        public virtual SketchObject ScalePitch(SketchObject o, double k) => o with { Size = new Size2(o.Size.W * k, o.Size.H * k) };

        /// <summary>
        /// Teeth drawn by the object (number, and local angle of a tooth centre in degrees, clockwise on screen); null when the
        /// current view shows no teeth. Meshing gears with teeth are turned so teeth meet gaps (<see cref="Coupling"/>).
        /// </summary>
        public virtual (int Teeth, double ToothDeg)? ToothPhase(SketchObject o) => null;

        /// <summary>
        /// Called when a parameter was set from outside the type's own actions (inspector, label menu): returns the object
        /// to keep, for example with <see cref="SketchObject.Size"/> following a design box that grew (a valve with more
        /// positions keeps its drawing scale instead of being squeezed). Default: <paramref name="after"/> unchanged.
        /// </summary>
        public virtual SketchObject OnParamChanged(SketchObject before, SketchObject after, string key) => after;

        /// <summary>
        /// Clean slides for sharing (<see cref="Sanitizer"/>): <paramref name="o"/> with its values made unrecognisable
        /// according to <see cref="SanitizeOptions.Mode"/> (normalised: same proportions on a neutral scale; randomised:
        /// plausible random values with the same look), and the text and metadata options applied. Use only
        /// <paramref name="rng"/> for random numbers so a seed reproduces the result. Default: no values, only the generic
        /// texts and metadata (<see cref="Sanitizer.Basic"/>); types with values worth hiding (charts, dimensions, tables …)
        /// override it. Returns <paramref name="o"/> itself when nothing changes; must keep the object renderable in its box.
        /// </summary>
        public virtual SketchObject Sanitize(SketchObject o, SanitizeOptions opt, Random rng) => Sanitizer.Basic(this, o, opt, rng);

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

        /// <summary>
        /// The parts of the element that can be selected and styled on their own (charts v2: each curve, axis, grid …). The
        /// key is the tag of their primitives (<see cref="PartLooks"/>). None by default.
        /// </summary>
        public virtual IEnumerable<PartInfo> Parts(SketchObject o, RenderContext ctx) => Array.Empty<PartInfo>();

        /// <summary>
        /// Actions of one part: the type's own (smooth a curve, add a value line …) followed by the generic ones (show/hide,
        /// thicker, thinner, line style, reset). Override <see cref="OwnPartActions"/> to add actions.
        /// </summary>
        public IEnumerable<ContextAction> PartActions(SketchObject o, string part, RenderContext ctx)
        {
            var own = OwnPartActions(o, part, ctx).ToList();
            foreach (var a in own) yield return a;
            // A choice the type offers itself (a figure curve's line style stored in its model) replaces the generic one.
            var ownChoices = new HashSet<string>(own.Where(a => a.Choice is not null).Select(a => a.Choice!.Key), StringComparer.Ordinal);
            var drawn = PartLooks.DrawnWidth(Render(o, ctx), part);
            foreach (var a in PartLooks.Generic(o, part, drawn))
                if (a.Choice is null || !ownChoices.Contains(a.Choice.Key)) yield return a;
        }

        /// <summary>Type-specific actions of a part (see <see cref="PartActions"/>).</summary>
        protected virtual IEnumerable<ContextAction> OwnPartActions(SketchObject o, string part, RenderContext ctx) => Array.Empty<ContextAction>();

        /// <summary>
        /// Shapes of parts that are not drawn but can be selected (local coordinates like <see cref="Render"/>), each tagged
        /// with its part key: the data points of a curve ("point.0.3", a small circle at the point). Hosts use them for hit
        /// testing (<see cref="Registry.PartAt"/>), the outline and the glow of a selected part; they never reach the slide or an
        /// export. None by default.
        /// </summary>
        public virtual IEnumerable<Primitive> PartShapes(SketchObject o, RenderContext ctx) => Array.Empty<Primitive>();

        /// <summary>
        /// Interactive handles of the object (<see cref="DragHandle"/>, positions in page mm): values the user can drag directly
        /// on the drawing, such as the limit and value lines of a chart. None by default.
        /// </summary>
        public virtual IEnumerable<DragHandle> DragHandles(SketchObject o, RenderContext ctx) => Array.Empty<DragHandle>();

        /// <summary>
        /// The object with handle <paramref name="key"/> dragged to the page point <paramref name="pagePos"/> (the type applies the
        /// handle's constraint and snaps the value sensibly, e.g. to the axis resolution). Unknown keys return
        /// <paramref name="o"/> unchanged.
        /// </summary>
        public virtual SketchObject MoveHandle(SketchObject o, string key, Vec2 pagePos, RenderContext ctx) => o;

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
        /// <summary>
        /// Id of another module's item that offers the same element, usually with more views or ports (a WC of the
        /// building services for the floor-plan WC of the architecture). While that item is registered, this one is left
        /// out of <see cref="Registry.Library"/> (no duplicates in galleries and search); <see cref="Registry.Item"/> still
        /// finds it, and its type keeps rendering existing documents. Modules stay usable on their own.
        /// </summary>
        public string? Alternative { get; init; }
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
        private readonly List<LibraryItem> _all = new();
        private List<LibraryItem> _library = new();
        private List<LibraryCategory> _visibleCategories = new();

        public IReadOnlyList<IModule> Modules => _modules;
        /// <summary>Categories with at least one offered item (a category whose items all have an alternative is left out).</summary>
        public IReadOnlyList<LibraryCategory> Categories => _visibleCategories;
        /// <summary>The offered items: every item except those whose <see cref="LibraryItem.Alternative"/> is registered.</summary>
        public IReadOnlyList<LibraryItem> Library => _library;
        /// <summary>Every registered item, including those left out of <see cref="Library"/> in favour of an alternative.</summary>
        public IReadOnlyList<LibraryItem> AllItems => _all;
        public IEnumerable<ObjectType> Types => _types.Values;

        public Registry Register(IModule module)
        {
            if (_modules.Any(m => m.Id == module.Id)) throw new InvalidOperationException($"Module '{module.Id}' is already registered.");
            foreach (var t in module.Types)
            {
                if (_types.ContainsKey(t.Id)) throw new InvalidOperationException($"Object type '{t.Id}' is already registered.");
                _types[t.Id] = t;
                t.Owner = this;
            }
            _modules.Add(module);
            _categories.AddRange(module.Categories);
            // Within a category the items of one type stand together (an element, its views and presets side by side), in
            // the order in which each type first appears; the module decides that order (most used first).
            var items = module.Library.ToList();
            var catFirst = new Dictionary<string, int>(StringComparer.Ordinal);
            var typeFirst = new Dictionary<(string, string), int>();
            for (var i = 0; i < items.Count; i++)
            {
                if (!catFirst.ContainsKey(items[i].Category)) catFirst[items[i].Category] = i;
                if (!typeFirst.ContainsKey((items[i].Category, items[i].TypeId))) typeFirst[(items[i].Category, items[i].TypeId)] = i;
            }
            foreach (var item in items.Select((it, i) => (it, i)).OrderBy(x => catFirst[x.it.Category]).ThenBy(x => typeFirst[(x.it.Category, x.it.TypeId)]).ThenBy(x => x.i).Select(x => x.it))
            {
                if (!_types.ContainsKey(item.TypeId)) throw new InvalidOperationException($"Library item '{item.Id}' references unknown type '{item.TypeId}'.");
                _all.Add(item);
            }
            // Items with a registered alternative leave the offered library (also items of modules registered earlier).
            var ids = new HashSet<string>(_all.Select(i => i.Id), StringComparer.Ordinal);
            _library = _all.Where(i => i.Alternative is null || !ids.Contains(i.Alternative)).ToList();
            var hidden = new HashSet<string>(_all.Except(_library).Select(i => i.Category), StringComparer.Ordinal);
            _visibleCategories = _categories.Where(c => !hidden.Contains(c.Id) || _library.Any(i => i.Category == c.Id)).ToList();
            return this;
        }

        public ObjectType? Type(string id) => _types.TryGetValue(id, out var t) ? t : null;

        public ObjectType TypeOf(SketchObject o) => Type(o.Type) ?? throw new KeyNotFoundException($"Unknown object type '{o.Type}'.");

        /// <summary>The item with this id, also one left out of <see cref="Library"/> in favour of an alternative.</summary>
        public LibraryItem? Item(string id) => _all.FirstOrDefault(i => i.Id == id);

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
            if (o.IsPointGeometry || type.Views.Any(v => v.Size is not null)) return KeepPitch(type, o, type.WithView(o, view, ctx), ctx);
            var from = PresetSize(type, type.ViewOf(o));
            var to = PresetSize(type, view);
            if (from is not Size2 a || to is not Size2 b || a.W < Geo.Eps || a.H < Geo.Eps || o.Size.W < Geo.Eps || o.Size.H < Geo.Eps)
                return KeepPitch(type, o, o with { View = view }, ctx);
            double k;
            if (Math.Abs(a.H - b.H) < 1e-6 * Math.Max(1, a.H)) k = o.Size.H / a.H;
            else if (Math.Abs(a.W - b.W) < 1e-6 * Math.Max(1, a.W)) k = o.Size.W / a.W;
            else if (Math.Abs(a.H - b.W) < 1e-6 * Math.Max(1, a.H)) k = o.Size.H / a.H;
            else if (Math.Abs(a.W - b.H) < 1e-6 * Math.Max(1, a.W)) k = o.Size.W / a.W;
            else k = Math.Sqrt(o.Size.W / a.W * (o.Size.H / a.H));
            return KeepPitch(type, o, o with { View = view, Size = new Size2(b.W * k, b.H * k) }, ctx);
        }

        /// <summary>
        /// The same part in another view keeps its pitch circle (a gear, pulley or sprocket is equally big in every view), so
        /// meshes and belts stay intact when a whole train switches its view.
        /// </summary>
        private SketchObject KeepPitch(ObjectType type, SketchObject before, SketchObject after, RenderContext ctx)
        {
            if (before.IsPointGeometry) return after;
            double? Pitch(SketchObject x)
            {
                try
                {
                    return PageAnchors(x, ctx).FirstOrDefault(a => a.Kind == AnchorKind.Circle && a.Role is AnchorRole.Pitch or AnchorRole.PitchInner)?.Radius;
                }
                catch (Exception)
                {
                    return null;
                }
            }
            if (Pitch(before) is not double r0 || Pitch(after) is not double r1 || r0 < Geo.Eps || r1 < Geo.Eps || Math.Abs(r1 / r0 - 1) < 1e-6) return after;
            return type.ScalePitch(after, r0 / r1);
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
            // The own type only when it is a companion (a gear next to a gear).
            var types = new HashSet<string>(StringComparer.Ordinal);
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
            foreach (var t in AnnotationSuggestions.Where(t => t != o.Type)) Add(_library.FirstOrDefault(i => i.TypeId == t));
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
            var local = RenderLocal(o, ctx);
            if (o.IsPointGeometry) return local;
            var m = o.Matrix();
            return local.Select(p => Prim.Transform(p, m)).ToList();
        }

        /// <summary>
        /// The object's primitives in its local coordinates (page coordinates for point geometry) as every renderer draws
        /// them: the drawing's line group applied (ISO 128: every stroke scales with it), its accent colour
        /// (<see cref="Accents"/>) and the object's own style overrides on top (they still win). SVG, exports and the native draw plans all go through here.
        /// </summary>
        public IReadOnlyList<Primitive> RenderLocal(SketchObject o, RenderContext ctx)
        {
            var type = TypeOf(o);
            ctx.For(this);
            var drawn = type.Render(o, ctx);
            // Colour by medium (slide-wide display mode): connection lines take the scheme colour of their medium and role.
            var medium = LineMedia.ColourFor(this, ctx.Doc, o, ctx.Lang);
            if (medium is not null) drawn = LineMedia.Apply(drawn, medium);
            // The values label (part "values", Calc 2) of types with element values, when the object asks for it.
            var local = ValueLabels.Append(this, o, ctx, drawn);
            // Designations, end markers and continuation markers of connection lines and elements (generic, never stored).
            local = Designations.Append(this, o, ctx, local);
            var k = ctx.Doc.Settings.LineGroup is double g && g > 0 ? g / Style.DesignLineGroup : 1;
            var accent = ctx.Doc.Settings.Accent;
            if (Math.Abs(k - 1) < 1e-9 && accent is null && o.Style is null) return PartLooks.Apply(local, o.Parts);
            var result = new List<Primitive>(local.Count);
            foreach (var p in local)
            {
                var q = Math.Abs(k - 1) > 1e-9 ? Prim.ScaleStrokes(p, k) : p;
                // Media colours are meaningful: the accent leaves a line coloured by medium as it is.
                if (accent is not null && medium is null) q = Accents.Apply(q, accent);
                if (o.Style is not null) q = Prim.TextOverride(type.ApplyOverride(q, o.Style), o.Style);
                result.Add(q);
            }
            // Part looks last: a curve recoloured on its own wins over the object's colour (charts v2).
            return PartLooks.Apply(result, o.Parts);
        }

        /// <summary>
        /// The context actions of <paramref name="o"/>: its type's actions plus the generic ones every host offers alike – the
        /// designation, value label and end markers of connection lines and elements (<see cref="Designations"/>) and the role
        /// of a connection line (<see cref="LineMedia"/>). Hosts and the shared runner (<see cref="ContextActions.Run"/>) use this.
        /// </summary>
        public IReadOnlyList<ContextAction> Actions(SketchObject o, RenderContext ctx)
        {
            ctx.For(this);
            var type = Type(o.Type);
            if (type is null) return Array.Empty<ContextAction>();
            var own = type.Actions(o, ctx).ToList();
            // "view.<id>" actions of a view the type declares duplicate every host's view control (dropdown, pie View ▸):
            // left out here (interaction concept section 9 item 4; the type keeps them for its own size rules and tests).
            var views = new HashSet<string>(type.Views.Select(v => "view." + v.Id), StringComparer.Ordinal);
            if (views.Count > 1) own = own.Where(a => !(a.Group == "view" && views.Contains(a.Id))).ToList();
            var list = new List<ContextAction>(own);
            list.AddRange(Designations.Actions(this, o, ctx, own));
            if (!own.Any(a => a.Choice?.Key == "line.role")) list.AddRange(LineMedia.Actions(this, o, ctx));
            return list;
        }

        /// <summary>The selectable parts of <paramref name="o"/>, with the ones switched off marked.</summary>
        public IReadOnlyList<PartInfo> Parts(SketchObject o, RenderContext ctx)
        {
            ctx.For(this);
            var parts = TypeOf(o).Parts(o, ctx);
            if (ValueLabels.PartOf(this, o) is { } values) parts = parts.Append(values);
            return parts.Select(p => p with { Hidden = PartLooks.LookOf(o.Parts, p.Key)?.Hidden == true }).ToList();
        }

        /// <summary>
        /// The part of <paramref name="o"/> under the page point <paramref name="at"/> (within <paramref name="tolerance"/> mm), or
        /// null. A part shape (<see cref="ObjectType.PartShapes"/>, e.g. a data point) wins when the point lies on it.
        /// </summary>
        public string? PartAt(SketchObject o, Vec2 at, RenderContext ctx, double tolerance = 1.0)
        {
            var keys = Parts(o, ctx).Select(p => p.Key).ToList();
            if (keys.Count == 0) return null;
            var shapes = PartShapes(o, ctx);
            if (shapes.Count > 0 && PartLooks.At(shapes, at, Math.Min(tolerance, 0.25), keys) is { } fine) return fine;
            return PartLooks.At(Render(o, ctx), at, tolerance, keys);
        }

        /// <summary>The part shapes of <paramref name="o"/> in page coordinates (<see cref="ObjectType.PartShapes"/>); empty for unknown types.</summary>
        public IReadOnlyList<Primitive> PartShapes(SketchObject o, RenderContext ctx)
        {
            ctx.For(this);
            var type = Type(o.Type);
            if (type is null) return Array.Empty<Primitive>();
            var local = type.PartShapes(o, ctx).ToList();
            if (local.Count == 0 || o.IsPointGeometry) return local;
            var m = o.Matrix();
            return local.Select(p => Prim.Transform(p, m)).ToList();
        }

        /// <summary>
        /// The page primitives of one part of <paramref name="o"/>: the drawn primitives tagged with <paramref name="part"/> (or a
        /// longer key below it) plus its part shapes. Hosts outline and glow a selected part with them.
        /// </summary>
        public IReadOnlyList<Primitive> PartPrimitives(SketchObject o, string part, RenderContext ctx)
        {
            bool Of(Primitive p) => p.Tag is { } t && (t == part || t.StartsWith(part + ".", StringComparison.Ordinal));
            return Prim.Flatten(Render(o, ctx)).Where(Of).Concat(Prim.Flatten(PartShapes(o, ctx)).Where(Of)).ToList();
        }

        /// <summary>The actions of one part of <paramref name="o"/> (type actions, then show/hide, thicker, thinner, line style, reset).</summary>
        public IReadOnlyList<ContextAction> PartActions(SketchObject o, string part, RenderContext ctx)
        {
            ctx.For(this);
            return TypeOf(o).PartActions(o, part, ctx).ToList();
        }

        /// <summary>The drag handles of <paramref name="o"/> in page mm (<see cref="ObjectType.DragHandles"/>); empty for unknown types.</summary>
        public IReadOnlyList<DragHandle> DragHandles(SketchObject o, RenderContext ctx)
        {
            ctx.For(this);
            var type = Type(o.Type);
            if (type is null) return Array.Empty<DragHandle>();
            try
            {
                return type.DragHandles(o, ctx).ToList();
            }
            catch (Exception ex) when (ex is FormatException or ArgumentException or InvalidOperationException)
            {
                return Array.Empty<DragHandle>();
            }
        }

        /// <summary>
        /// <paramref name="o"/> with handle <paramref name="key"/> dragged to <paramref name="pagePos"/> (page mm,
        /// <see cref="ObjectType.MoveHandle"/>); unchanged for unknown types or keys.
        /// </summary>
        public SketchObject MoveHandle(SketchObject o, string key, Vec2 pagePos, RenderContext ctx)
        {
            ctx.For(this);
            var type = Type(o.Type);
            return type is null ? o : type.MoveHandle(o, key, pagePos, ctx);
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
            ctx.For(this);
            var type = Type(o.Type);
            if (o.IsPointGeometry)
            {
                var pts = ctx.Doc.ResolvePoints(o);
                for (var i = 0; i < pts.Count; i++) yield return Anchor.Point(AnchorRole.End, pts[i], "p" + i.ToString(CultureInfo.InvariantCulture));
                if (type is null) yield break;
                // Rails of lines get ids in their order ("rail0", "rail1" …), so a line end docked onto them (a branch)
                // can be attached to the rail and follow it (Attachments.For / Refresh).
                var rail = 0;
                foreach (var a in type.Anchors(o, ctx))
                    yield return a.Kind == AnchorKind.Segment && a.Role == AnchorRole.Rail && a.Id is null
                        ? a with { Id = "rail" + (rail++).ToString(CultureInfo.InvariantCulture) }
                        : a;
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
            // Pitch circles of pulleys and sprockets are marked: they size the strand but do not mesh (IPitchMesh).
            var noMesh = type is IPitchMesh pm && !pm.MeshesAtPitch(o);
            foreach (var a in type.Anchors(o, ctx))
            {
                var t = a.Transform(m);
                yield return noMesh && t.Kind == AnchorKind.Circle && t.Role is AnchorRole.Pitch or AnchorRole.PitchInner ? t with { NoMesh = true } : t;
            }
        }
    }
}
