// JSON contract of the C# EngineApi (src/InventaSketch.Engine). Geometry in mm unless stated; draw plans in pt.

export interface Vec2 { x: number; y: number }
export interface Rect { x: number; y: number; w: number; h: number }

export interface ObjectInfo {
  id: string;
  type: string;
  name: string;
  kind: "box" | "points";
  known: boolean;
  bounds: Rect;
  cx: number;
  cy: number;
  w: number;
  h: number;
  rotation: number;
  flipX: boolean;
  flipY: boolean;
  points?: Vec2[];
  groupId?: string;
  locked: boolean;
  view?: string;
  uniform: boolean;
  hasText: boolean;
  text?: string;
  ref?: string;
  /** Selected objects only: centred part (pitch circle or hub) that resizes about its centre by default. */
  centred?: boolean;
  /** Selected objects only: main text for quick editing (double-click, bar, pie); set it with setMainText. */
  mainText?: MainTextInfo;
  /** Selected objects only: quick-add "+" arrows apply (flowchart, UML, ER, BPMN, basic shapes). */
  chain?: boolean;
  /** Selected objects only: the type has values (engine `values`): the bar offers "Values" in Explore and Engineer. */
  hasValues?: boolean;
}

/** Main text of an object: object text (no key) or the text parameter `key` (numeral, text, label, name, title). */
export interface MainTextInfo { key?: string; label: string; value: string }

/** Library item offered for the selection (engine `suggestions`, `nextItems`, `search`). */
export interface SuggestionInfo {
  id: string;
  typeId: string;
  name: string;
  view?: string;
  category: string;
  points: boolean;
  /** search: fits the selection (listed first, marked "fits"). */
  fits?: boolean;
}

/** Which quick transforms change the selection's drawing (engine `transformEffect`, from `Symmetry`). */
export interface TransformEffect { mirrorH: boolean; mirrorV: boolean; rotate90: boolean }

/** Selection-wide operation that changes something (engine `arrangeOptions`): "mview:<view>", "align:row" … */
export interface ArrangeOption { id: string; label: string; icon: string }

/** Path of a joint of a self-built linkage while its crank turns (engine `trace`); empty when the chain has F ≠ 1. */
export interface TraceInfo { points: Vec2[]; closed: boolean }

/**
 * Build "+" port of an element (engine `buildPorts`): kind (Mesh, InnerMesh, AxisEnd, Hub, Pin, Bevel, Joint, Mount), position
 * and outward direction in page mm; `stretch` for a shaft end that can be dragged longer or shorter.
 */
/** A build "+": kind Mesh/Hub/AxisEnd/… or Connect (connection port) / LineEnd (free end of a cable, pipe, duct); `medium` of a connection. */
export interface BuildPortInfo { id: string; kind: string; x: number; y: number; dx: number; dy: number; stretch: boolean; medium?: string }

/** Where the selection touches another element and follows it (engine `links`): a lock badge; a click toggles it (`toggleLink`). */
export interface LinkInfo { key: string; kind: "mesh" | "carried" | "bonded" | "attached" | "spanned"; a: string; b: string; x: number; y: number; released: boolean; index: number; label: string }

/** Corner dot of one segment of a line with free corners (engine `polylineHandles`), page mm; `nx`/`ny` the branch side. */
export interface SegmentHandleInfo { index: number; x: number; y: number; nx: number; ny: number; ax: number; ay: number; bx: number; by: number }

/** Corner dots, branch handles, removable and splittable corners of a line (engine `polylineHandles`). */
export interface PolylineHandles { corners: boolean; branch: boolean; closed: boolean; segments: SegmentHandleInfo[]; removable: number[]; splittable: number[] }

/** An insert place on an element (engine `insertHandles`: between table rows and columns), page mm. */
export interface InsertHandleInfo { key: string; x: number; y: number; dx: number; dy: number; label: string }

/** The calculation sheet (engine `calcSheet`): the text in the asked format, the number of rows and the cited sources. */
export interface CalcSheetResult { text: string; rows: number; sources: { id: string; text: string }[] }
export type CalcSheetMode = "csv" | "markdown" | "latex" | "table";

/** Live coupling preview (engine `couplingPreview`): neighbours that would follow, as one SVG in page mm ("" for none). */
export interface CouplingPreview { ids: string[]; svg: string }

/**
 * Motion mode (engine `motionInfo`): "gear" turns about its centre x/y with its train (`coupled`: other parts move with
 * it), "linkage" is a link of a self-built chain with `dof` ≥ 1, "none" cannot move that way.
 */
export type MotionInfo =
  | { kind: "gear"; x: number; y: number; coupled: boolean }
  /** A predefined mechanism (four-bar …): turning about its input pivot x/y changes its crank angle. */
  | { kind: "mechanism"; x: number; y: number; coupled: boolean }
  | { kind: "linkage"; dof: number }
  | { kind: "none" };

/** Motion mode live preview (engine `motionPreview`): the objects a turn or joint drag would move, as one SVG in page mm. */
export interface MotionPreview { ids: string[]; svg: string }

export interface EditorState {
  session: string;
  revision: number;
  pageId: string;
  pageWidth: number;
  pageHeight: number;
  grid: number;
  selection: string[];
  selectionBounds?: Rect;
  canUndo: boolean;
  canRedo: boolean;
  undoLabel?: string;
  objects: ObjectInfo[];
  svg?: string;
  /** Document plan scale denominator (100 = 1:100); absent if unset. */
  planScale?: number;
  /** Document black-and-white setting (setMonochrome): page SVG, exports and draw plans are black and white. */
  monochrome?: boolean;
  /** ISO 128 line group (setLineGroup): width of the thick line in mm; thin lines are half of it. 0.5 by default. */
  lineGroup?: number;
  /** Accent colour (setAccent, #rrggbb): coloured parts of all elements take its hue; absent = each module's colours. */
  accent?: string | null;
  /** Objects added or modified by the change that produced this revision (edit, undo, redo). */
  changed?: string[];
  /** Further objects whose rendering depends on the changed or removed ones (wall joints, openings): redraw them too. */
  dependents?: string[];  /** Value level of the document (setValueLevel): quick sketch (default), explore or engineer. */
  valueLevel?: ValueLevel;
}

export type ValueLevel = "quick" | "explore" | "engineer";

/** A kind of "select connected" that adds something (engine `connectOptions`). */
export interface ConnectOption { kind: "net" | "coupled" | "chain" | "loop" | "walls"; label: string; hint: string; ids: string[] }

/** One variable of an element with its value (engine `values`); numbers in SI and in the display unit. */
export interface VarInfo {
  key: string;
  name: string;
  symbol: string;
  kind: string;
  relevance: "card" | "panel" | "engineer";
  unit: string;
  unitFactor: number;
  unitOffset: number;
  units: string[];
  value?: number | null;
  number?: number | null;
  display: string;
  text?: string | null;
  provenance: "unknown" | "entered" | "default" | "linked" | "derived";
  formula?: string | null;
  relation?: string | null;
  source?: string | null;
  error?: string | null;
  /** Range of a value with tolerances (SI) and as text ("180 V … 220 V"); absent without tolerances. */
  lo?: number | null;
  hi?: number | null;
  range?: string | null;
  conflict: boolean;
  param: string;
}

/** A relation that disagrees (engine `values`): both values and the entered values involved. */
export interface ConflictInfo { relation: string; formula: string; objectId: string; key: string; valueText: string; impliedText: string; involved: string[]; message: string }

/** The values of one element (engine `values`). */
export interface ObjectValues {
  id: string;
  status: "empty" | "under" | "determined" | "checked" | "conflict";
  vars: VarInfo[];
  missing: string[][];
  conflicts: ConflictInfo[];
  hint: string;
}

export interface ValuesResult { objects: ObjectValues[]; elapsedMs: number; truncated: boolean }

/** Step-by-step formula lines of a derived value (engine `explain`). */
export interface ExplainResult { id: string; key: string; steps: { key: string; text: string; source?: string | null }[]; text: string }

/** Result of the unit-aware calculator (engine `calc`). */
export interface CalcResult { value: number; dimension: string; kind?: string | null; unit: string; number: number; text: string; steps: string[] }

/** Result of a unit conversion (engine `convertUnit`). */
export interface ConvertUnitResult { value: number; unit: string; text: string; kind: string; units: string[] }

/** Result of the code import (engine `importCode`: matplotlib, pgfplots, TikZ; read, never executed). */
export interface CodeImportResult {
  state: EditorState;
  format?: string | null;
  ids: string[];
  imported: string[];
  skipped: { line: number; code: string; reason: string }[];
  message: string;
}

/** A grip on a value of an element (engine `dragHandles`): chart limit lines, value lines, markers, data points. */
export interface DragHandleInfo {
  key: string;
  x: number;
  y: number;
  constraint: "free" | "horizontal" | "vertical";
  dx: number;
  dy: number;
  label: string;
  part?: string | null;
  showWith?: string | null;
  byPart: boolean;
}

export interface ObjectHandles { id: string; handles: DragHandleInfo[] }

export interface CatalogCategory { id: string; name: string; module: string; order: number }
export interface CatalogItem { id: string; typeId: string; name: string; category: string; keywords: string[]; thumbnail: string; points: boolean }
export interface Catalog { lang: string; categories: CatalogCategory[]; items: CatalogItem[] }

/** A domain export (bill of materials, connection graph, netlist, reference sign list) that applies to the selection. */
export interface ExporterInfo { id: string; name: string; extension: string; mime: string; preserves: string }
export interface ExportResult { id: string; name: string; extension: string; mime: string; text: string }

export interface ActionInfo {
  id: string;
  label: string;
  icon: string;
  enabled: boolean;
  active: boolean;
  group: string;
  kind: "generic" | "view" | "type" | "option";
  /** Present for actions that need a text value: prompt for it and pass it to applyAction. */
  input?: { label: string; value: string; unit?: string };
  /** Parameters the action sets: the bar hides it when those are shown as choice buttons. */
  paramKeys?: string[];
  /** Key of the choice this action is an option of: show the options of one choice as one dropdown (never a cycle). */
  choice?: string;
  /** What the choice chooses ("Curve shape"). */
  choiceLabel?: string;
  /** The choice is a menu of commands ("Fill to ▸"): no current option. */
  choiceMenu?: boolean;
  /** The closed dropdown shows the current option's name, not its picture (its picture would repeat a neighbour's). */
  choiceLabelInBar?: boolean;
  /** Pictogram id of the choice itself for the closed dropdown (instead of the current option's picture). */
  choiceBarIcon?: string;
  /** A small SVG drawing of the option (fill patterns, colours, curve shapes) when no shared pictogram fits. */
  previewSvg?: string;
}

export type ParamValue = number | string | boolean | null;

/** A selectable part of an element (charts v2: a curve, an axis, the grid …); engine `parts`. */
export interface PartView { key: string; label: string; kind: string; hidden: boolean; style?: StyleInfo }

export interface ParamInfo {
  key: string;
  label: string;
  kind: "number" | "integer" | "boolean" | "choice" | "text";
  min?: number;
  max?: number;
  step?: number;
  unit?: string;
  advanced: boolean;
  options?: ActionInfo[];
  /** Option icons are drawn mirrored (start arrowheads). */
  mirrorIcons?: boolean;
  value?: ParamValue;
  default?: ParamValue;
}

export type DashStyle = "solid" | "dash" | "dashDot" | "dashDotDot" | "dot" | "longDash";
export type ArrowHead =
  | "none" | "triangle" | "open" | "stealth" | "diamond" | "oval" | "tick" | "half"
  // Hollow UML heads and crow's-foot (ER) ends: never native line ends, the engine lowers them to shapes.
  | "triangleOpen" | "diamondOpen"
  | "crowOne" | "crowMany" | "crowZeroOne" | "crowOneOnly" | "crowZeroMany" | "crowOneMany";

export interface StyleInfo {
  stroke?: string;
  strokeWidth?: number;
  dash?: DashStyle;
  fill?: { kind: "none" | "solid" | "hatch" | "dots"; color?: string; hatch?: { angle: number; spacing: number; cross?: boolean } };
  arrowStart?: ArrowHead;
  arrowEnd?: ArrowHead;
  opacity?: number;
}

export interface InspectorInfo {
  ids: string[];
  typeId?: string;
  typeName?: string;
  points: boolean;
  views: ActionInfo[];
  view?: string;
  params: ParamInfo[];
  style?: StyleInfo;
  hasText: boolean;
  text?: string;
  ref?: string;
  /** The selected type uses the document plan scale (offer it in the inspector). */
  usesPlanScale?: boolean;
  planScale?: number;
}

export interface HitResult { id?: string; ids: string[]; stack: string[] }

export interface SnapGuide { kind: "vertical" | "horizontal" | "point" | "circle" | "segment"; a: Vec2; b: Vec2; radius: number }

export interface SnapResult {
  dx: number;
  dy: number;
  x: number;
  y: number;
  kind: string;
  guides: SnapGuide[];
  attach?: { objectId: string; u: number; v: number };
  /** snapMove: what the drag docks onto ("pitch circle", "axis", "pin 2"), for a label at the target. */
  label?: string;
}

export interface DrawCapabilities {
  freeform: boolean;
  arrowheads: boolean;
  patternFill: boolean;
  rotation: boolean;
  svg: boolean;
  shapes: boolean;
}

export interface PlanStroke { color: string; weight: number; dash: DashStyle; arrowStart?: string; arrowEnd?: string; arrowSize?: string }
export interface PlanFill { kind: "none" | "solid" | "hatch"; color?: string }

export interface PlanOp {
  op: "rect" | "ellipse" | "shape" | "line" | "polyline" | "freeform" | "text" | "group" | "svg";
  x?: number; y?: number; w?: number; h?: number;
  x1?: number; y1?: number; x2?: number; y2?: number;
  rotation?: number;
  radius?: number;
  shape?: "triangle" | "diamond";
  points?: [number, number][];
  closed?: boolean;
  stroke?: PlanStroke;
  fill?: PlanFill;
  opacity?: number;
  text?: string;
  fontSize?: number;
  font?: string;
  bold?: boolean;
  italic?: boolean;
  underline?: boolean;
  color?: string;
  align?: "left" | "center" | "right";
  children?: PlanOp[];
  svg?: string;
  tag?: string;
}

export interface PlanUnit {
  id: string;
  objectIds: string[];
  type: string;
  name: string;
  mode: "native" | "svg";
  bounds: Rect;
  payload: string;
  ops: PlanOp[];
  notes: string[];
}

export interface DrawPlan { unit: "pt"; pageWidth: number; pageHeight: number; revision: number; units: PlanUnit[] }

/**
 * Chart handling of "Clean slides" (engine `sanitize`): "normalised" (same proportions on a neutral scale), "randomised"
 * (plausible random values, same look) or "none" (chart values kept; only the text and metadata options apply).
 */
export type SanitizeMode = "normalised" | "randomised" | "none";

/** Options of engine `sanitize` (its `value`); a fixed seed reproduces a randomised result. */
export interface SanitizeOptions { renameCategories: boolean; replaceTexts: boolean; stripMeta: boolean; seed?: number; spread?: number }

/** What engine `sanitize` changed: counts, the seed used and the report lines (request language). */
export interface SanitizeReport {
  mode: SanitizeMode;
  seed: number;
  elements: number;
  changed: number;
  values: number;
  texts: number;
  metadata: number;
  references: number;
  unknown: number;
  failed: number;
  byType: { type: string; name: string; count: number }[];
  summary: string;
  lines: string[];
}

export interface SanitizeResult { state: EditorState; report: SanitizeReport }

/** Severity of a compatibility check (preflight) finding ("manual": a point to check by hand). */
export type PreflightSeverity = "error" | "warning" | "info" | "manual";

/** One finding of the compatibility check (preflight) (engine `preflight`). */
export interface PreflightFinding {
  rule: string;
  severity: PreflightSeverity;
  /** The element concerned, if any. */
  id?: string;
  page?: string;
  message: string;
  suggestion?: string;
  /** The cited rule (office, regulation, paragraph). */
  source: string;
}

/** Result of `preflight`: profile id, disclaimer (always shown) and findings. */
export interface PreflightResult {
  profile: string;
  disclaimer: string;
  findings: PreflightFinding[];
}
