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
}

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
  /** Objects added or modified by the change that produced this revision (edit, undo, redo). */
  changed?: string[];
  /** Further objects whose rendering depends on the changed or removed ones (wall joints, openings): redraw them too. */
  dependents?: string[];
}

export interface CatalogCategory { id: string; name: string; module: string; order: number }
export interface CatalogItem { id: string; typeId: string; name: string; category: string; keywords: string[]; thumbnail: string; points: boolean }
export interface Catalog { lang: string; categories: CatalogCategory[]; items: CatalogItem[] }

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
}

export type ParamValue = number | string | boolean | null;

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
  value?: ParamValue;
  default?: ParamValue;
}

export type DashStyle = "solid" | "dash" | "dashDot" | "dashDotDot" | "dot" | "longDash";
export type ArrowHead = "none" | "triangle" | "open" | "stealth" | "diamond" | "oval" | "tick" | "half";

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
