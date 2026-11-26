// Stage: SVG view of the current page with selection frame, handles, live snapping guides, marquee, zoom/pan and
// insert-at-pointer. All geometry decisions (snapping, hit-testing, edits) come from the engine; the stage only
// previews drags with SVG transforms and commits the result through the session.

import type { Session } from "../engine/client";
import type { BuildPortInfo, CatalogItem, DragHandleInfo, EditorState, InsertHandleInfo, LinkInfo, MotionInfo, ObjectInfo, PolylineHandles, SegmentHandleInfo, SnapGuide, Vec2 } from "../engine/types";
import { lang, t } from "../i18n";
import { COUPLING_PREVIEW_MS, HUB_CORNER_PX, PORT_OFFSET_PX, PORT_RADIUS_PX, Throttle, portTipKey, stretchTarget } from "./buildlogic";
import { PRIORITY, resolveHandles, type LayoutSpec } from "./handlelayout";
import {
  BADGE_OFFSET_PX, BADGE_PX, BRANCH_OFFSET_PX, BRANCH_PX, CORNER_DOT_PX, GAP_SCALE, MIN_SEGMENT_PX, SMALL_ELEMENT_PX, VALUE_BADGE_PX,
  bodyGrip, canRemoveCorner, cornerAt, primaryPort, revealOpacity, shownWith,
} from "./handlelogic";
import { clamp, s, svgChildren, svgDataUrl, viewBoxOf } from "./dom";
import { TurnTracker, canMove, insideBounds, nearestJoint, stepTurn, turnLabel } from "./motionlogic";
import { snapLabelAnchor } from "./uxlogic";

export interface StageHost {
  state(): EditorState;
  session(): Session;
  /** Adopts an edit result (live = may be synced to the slide). */
  apply(state: EditorState, live?: boolean): void;
  openPie(clientX: number, clientY: number): void;
  pendingInsert(): CatalogItem | undefined;
  finishInsert(placed: boolean): void;
  snapEnabled(): boolean;
  /** Snapped line ends attach to their target ("Anchor"). */
  anchorEnabled(): boolean;
  /** "+" quick add (click on a triangle, or drag and drop at `at`). */
  quickAdd?(id: string, side: ChainSide, at?: Vec2): void;
  /** Click on a build "+": the insert box at `client` with what docks at `port` of `id`. */
  openPortInsert?(id: string, port: BuildPortInfo, client: Vec2): void;
  pointerMoved(clientX: number, clientY: number): void;
  /** Motion mode (session only): dragging a part that can move turns its gear train or pulls a joint of its linkage. */
  motionEnabled?(): boolean;
  /** A drag that previews its result (motion mode) started moving: the context bar gets out of the way. */
  hideBar?(): void;
  /** A click without dragging on the single selected element at page point `p` (charts v2: selects the part there). */
  clickPart?(id: string, p: Vec2, tolerance: number): void;
  /** Ctrl+double-click on element `id`: select what is connected with it (the most meaningful kind). */
  selectConnected?(id: string): void;
  /** The selected part of element `id` (grips of a curve's points show with it), if any. */
  selectedPart?(id: string): string | undefined;
  /** A click on a grip: select its part of element `id`. */
  selectPart?(id: string, part: string): void;
  /** The entered values of element `id` conflict (Explore and Engineer): the stage shows the ⚠ badge. */
  valueConflict?(id: string): boolean;
  /** A click on the ⚠ badge: the value card of the selected element. */
  openValues?(): void;
  report(error: unknown): void;
}

interface Frame { cx: number; cy: number; w: number; h: number; rot: number }

export type ChainSide = "left" | "top" | "right" | "bottom";

type HandleSpec = { kind: "scale"; sx: number; sy: number } | { kind: "rotate" } | { kind: "point"; index: number } | { kind: "chain"; side: ChainSide } | { kind: "port"; index: number } | { kind: "grip"; index: number }
  | { kind: "lock"; index: number } | { kind: "corner"; index: number } | { kind: "branch"; index: number } | { kind: "gap"; index: number } | { kind: "valuebadge" };

/** A handle that fades in near the pointer (progressive disclosure): its element, where it is and where it belongs (page mm). */
interface Revealable { el: SVGElement; at: Vec2; anchor?: Vec2; primary: boolean }

/** Change shown by the motion preview (engine `motionPreview`): a gear turn or a joint dragged to a point. */
type MotionChange = { degrees: number } | { index: number; x: number; y: number };

/** Change shown by the live coupling preview (engine `couplingPreview`): a move or a scale about a pivot. */
type CouplingChange = { dx: number; dy: number } | { sx: number; sy: number; pivotX: number; pivotY: number };

type Drag =
  | { kind: "move"; ids: string[]; start: Vec2; dx: number; dy: number; moved: boolean; startClient: Vec2; followers?: string[] }
  | { kind: "scale"; ids: string[]; frame: Frame; sx: number; sy: number; single?: ObjectInfo; centred: boolean; last?: { p: Vec2; client: Vec2; free: boolean; alt: boolean }; result?: ResizeResult }
  | { kind: "rotate"; ids: string[]; frame: Frame; startAngle: number; delta: number }
  | { kind: "point"; obj: ObjectInfo; index: number; pos: Vec2; startClient: Vec2; moved: boolean }
  | { kind: "marquee"; start: Vec2; cur: Vec2; add: boolean }
  | { kind: "pan"; startClient: Vec2; ox: number; oy: number }
  | { kind: "chain"; id: string; side: ChainSide; from: Vec2; cur: Vec2; moved: boolean; centre: Vec2 }
  | { kind: "port"; id: string; port: BuildPortInfo; plus: Vec2; startClient: Vec2; moved: boolean; to?: { x: number; y: number; delta: number } }
  /** A grip on a value (chart limit line, value line, marker, data point) dragged to `to` (the element constrains and snaps it). */
  | { kind: "grip"; id: string; grip: DragHandleInfo; startClient: Vec2; moved: boolean; to?: Vec2; offset?: Vec2; start?: Vec2 }
  /** A corner dot dragged (or clicked): a new corner on segment `seg`, starting at `start`, moved to `pos`. */
  | { kind: "corner"; obj: ObjectInfo; seg: SegmentHandleInfo; start: Vec2; pos: Vec2; startClient: Vec2; moved: boolean; snapped: boolean }
  /** A branch dragged out of a line (a click adds a short stub). */
  | { kind: "branch"; id: string; seg: SegmentHandleInfo; from: Vec2; end: Vec2; startClient: Vec2; moved: boolean; snapped: boolean }
  /** A click target (lock badge, insert dot, ⚠ badge): runs on release when the pointer did not move away. */
  | { kind: "tap"; run: () => void; startClient: Vec2 }
  /** Motion mode: a gear turned about its centre (`turn`), or joint `index` of a link pulled to `target`. */
  | { kind: "motion"; id: string; turn?: TurnTracker; degrees: number; index: number; from?: Vec2; target: Vec2; startClient: Vec2; moved: boolean; dimmed: string[] };

type MotionDrag = Extract<Drag, { kind: "motion" }>;
type MovingInfo = Exclude<MotionInfo, { kind: "none" }>;

const HANDLE_PX = 9;
/** Quick-add triangles: distance outside the frame and size (px); the top one sits beyond the rotate handle. */
const CHAIN_PX = 14, CHAIN_SIZE_PX = 11;
const ROTATE_PX = 26;
const MOVE_THRESHOLD_PX = 3;
const SNAP_PX = 8;

const rad = (d: number) => (d * Math.PI) / 180;

function rotateVec(v: Vec2, deg: number): Vec2 {
  const c = Math.cos(rad(deg)), si = Math.sin(rad(deg));
  return { x: v.x * c - v.y * si, y: v.x * si + v.y * c };
}

export interface ResizeFrame { cx: number; cy: number; w: number; h: number; rot: number }
export interface ResizeResult { w: number; h: number; cx: number; cy: number; fx: number; fy: number; pivot: Vec2 }

/**
 * New frame for dragging handle (hx, hy) ∈ {-1, 0, 1}² to page point p. Corner handles scale uniformly unless `free`;
 * edge handles stretch one axis. `aboutCentre` keeps the frame centre fixed, otherwise the opposite handle stays.
 */
export function resizeFrame(f: ResizeFrame, hx: number, hy: number, p: Vec2, free: boolean, aboutCentre: boolean): ResizeResult {
  // Pointer in the frame's local (unrotated) coordinates, origin at the frame centre.
  const local = rotateVec({ x: p.x - f.cx, y: p.y - f.cy }, -f.rot);
  const ax = aboutCentre ? 0 : (-hx * f.w) / 2, ay = aboutCentre ? 0 : (-hy * f.h) / 2; // fixed point
  const span = aboutCentre ? 2 : 1;
  let w = f.w, h = f.h;
  if (hx !== 0 && hy !== 0 && !free) {
    const dir = { x: (hx * f.w) / 2 - ax, y: (hy * f.h) / 2 - ay };
    const k = Math.max(0.02, ((local.x - ax) * dir.x + (local.y - ay) * dir.y) / (dir.x * dir.x + dir.y * dir.y || 1));
    w = f.w * k; h = f.h * k;
  } else {
    if (hx !== 0) w = Math.max(0.5, (local.x - ax) * hx * span);
    if (hy !== 0) h = Math.max(0.5, (local.y - ay) * hy * span);
  }
  const cl = aboutCentre ? { x: 0, y: 0 } : { x: hx !== 0 ? ax + (hx * w) / 2 : 0, y: hy !== 0 ? ay + (hy * h) / 2 : 0 };
  const c = rotateVec(cl, f.rot);
  const pivotLocal = rotateVec({ x: ax, y: ay }, f.rot);
  return {
    w, h, cx: f.cx + c.x, cy: f.cy + c.y,
    fx: w / Math.max(1e-6, f.w), fy: h / Math.max(1e-6, f.h),
    pivot: { x: f.cx + pivotLocal.x, y: f.cy + pivotLocal.y },
  };
}

/** Text of the scale label: size (one object) or factor, and the active mode with its Ctrl alternative. */
export function scaleLabel(r: ResizeResult, single: boolean, aboutCentre: boolean): string {
  const f = (v: number) => (Math.round(v * 10) / 10).toString();
  const size = single ? `${f(r.w)} × ${f(r.h)} mm` : Math.abs(r.fx - r.fy) < 1e-3 ? `${Math.round(r.fx * 100)} %` : `${Math.round(r.fx * 100)} × ${Math.round(r.fy * 100)} %`;
  return `${size} · ${t(aboutCentre ? "scale.centre" : "scale.edge")}`;
}

export class Stage {
  readonly el: HTMLDivElement;
  readonly svg: SVGSVGElement;
  private readonly pageRect: SVGRectElement;
  private readonly gridRect: SVGRectElement;
  /** Sketch mode (charts v2): the trail in page mm while the button is held, and what receives it on release. */
  private sketch?: { done: (points: number[][]) => void; points: number[][]; drawing: boolean };
  /** Outline of the selected part of a chart (charts v2), below the selection handles. */
  private readonly partLayer: SVGGElement;
  private readonly content: SVGGElement;
  /** Selection look: a soft blue glow along the exact outline of every selected element (above the drawing, under handles). */
  private readonly haloLayer: SVGGElement;
  private readonly haloDefs: SVGDefsElement;
  /** What the halo was last drawn for (selection, revision, zoom, preview): redrawn only when it changes. */
  private haloKey = "";
  /** Elements a hovered "Select connected" entry would add: glow fainter. */
  private haloPreview: string[] = [];
  /** The selected part's outline (page mm polylines): glows like a selection, its element fainter. */
  private partLines?: number[][][];
  private readonly overlay: SVGGElement;
  private readonly guides: SVGGElement;
  private readonly ghost: SVGGElement;
  /** Live coupling preview: neighbours that follow the dragged or resized selection, in blue (like COM). */
  private readonly coupling: SVGGElement;
  private readonly couplingThrottle = new Throttle((ids: string[], change: CouplingChange) => this.showCoupling(ids, change), COUPLING_PREVIEW_MS);
  /** Motion mode preview (engine `motionPreview`), at the same rate as the coupling preview. */
  private readonly motionThrottle = new Throttle((id: string, change: MotionChange) => this.showMotion(id, change), COUPLING_PREVIEW_MS);
  /** Engine `motionInfo` per object, valid for one document revision. */
  private motionCache = { revision: -1, map: new Map<string, MotionInfo>() };
  /** Build ports of the single selected element, fetched once per selection and revision. */
  private ports?: { key: string; list: BuildPortInfo[] };
  /** Page positions of the drawn "+" (same order as `ports.list`). */
  private portPlus: Vec2[] = [];
  /** Grips on values of the single selected element (engine `dragHandles`), fetched once per selection and revision. */
  private grips?: { key: string; list: DragHandleInfo[] };
  /** The grips drawn now (shown ones, in `data-handle` index order). */
  private shownGrips: DragHandleInfo[] = [];
  /** Lock badges of the single selected element (engine `links`), corner dots and branch handles of a selected line
   * (`polylineHandles`) and insert dots (`insertHandles`), each fetched once per selection and revision. */
  private links?: { key: string; list: LinkInfo[] };
  private poly?: { key: string; info?: PolylineHandles };
  private gaps?: { key: string; list: InsertHandleInfo[] };
  /** What the drawn handles stand for, in `data-handle` index order, and where the corner dots were placed (page mm). */
  private shownLinks: LinkInfo[] = [];
  private shownSegs: SegmentHandleInfo[] = [];
  private segPlaced: Vec2[] = [];
  private shownGaps: InsertHandleInfo[] = [];
  /** Progressive disclosure: the handles that fade in near the pointer, the pointer (page mm, none outside) and Shift. */
  private revealables: Revealable[] = [];
  private revealPointer?: Vec2;
  private shiftHeld = false;
  /** Last press on a point diamond (a double-click on a corner removes it). */
  private lastPointDown?: { time: number; index: number; x: number; y: number };
  /** Start time of the sketch trail (pointer times go with the points, for the drawing speed hint). */
  private sketchStart = 0;
  /** Live preview of a dragged grip (engine `moveHandle` with preview), at the coupling preview rate. */
  private readonly gripThrottle = new Throttle((id: string, grip: DragHandleInfo, to: Vec2, client: Vec2) => this.showGrip(id, grip, to, client), COUPLING_PREVIEW_MS);
  private readonly scaleLabelEl: HTMLDivElement;
  /** What a drag docks onto ("pitch circle", "axis", "pin 2"), next to the snap target. */
  private readonly snapLabelEl: HTMLDivElement;
  private scale = 2; // px per mm
  private ox = -10;
  private oy = -10;
  private cw = 400;
  private ch = 300;
  private drag?: Drag;
  private renderedRevision = -1;
  private fitted = false;
  private spaceDown = false;
  private lastPointer?: Vec2;
  private lastDown?: { time: number; x: number; y: number; id?: string };

  constructor(private readonly host: StageHost) {
    this.el = document.createElement("div");
    this.el.className = "isk-stage";
    this.el.tabIndex = 0;
    this.el.setAttribute("role", "application");
    this.svg = s("svg", { class: "isk-stage-svg" });
    const defs = s("defs");
    const pattern = s("pattern", { id: "isk-grid", width: 5, height: 5, patternUnits: "userSpaceOnUse" });
    pattern.append(s("circle", { cx: 0, cy: 0, r: 0.18, class: "isk-grid-dot" }));
    defs.append(pattern);
    this.pageRect = s("rect", { class: "isk-page", x: 0, y: 0 });
    this.gridRect = s("rect", { class: "isk-grid", x: 0, y: 0, fill: "url(#isk-grid)" });
    this.content = s("g", { class: "isk-content" });
    this.guides = s("g", { class: "isk-guides" });
    this.overlay = s("g", { class: "isk-overlay" });
    this.ghost = s("g", { class: "isk-ghost" });
    this.coupling = s("g", { class: "isk-coupling" });
    this.partLayer = s("g", { class: "isk-part-layer" });
    this.haloLayer = s("g", { class: "isk-halo-layer", "aria-hidden": "true" });
    this.haloDefs = s("defs");
    this.svg.append(defs, this.haloDefs, this.pageRect, this.gridRect, this.content, this.coupling, this.haloLayer, this.guides, this.partLayer, this.overlay, this.ghost);
    this.scaleLabelEl = document.createElement("div");
    this.scaleLabelEl.className = "isk-scale-label";
    this.scaleLabelEl.hidden = true;
    this.snapLabelEl = document.createElement("div");
    this.snapLabelEl.className = "isk-scale-label isk-snap-label";
    this.snapLabelEl.hidden = true;
    this.el.append(this.svg, this.scaleLabelEl, this.snapLabelEl);
    window.addEventListener("keydown", (e) => this.onModifier(e));
    window.addEventListener("keydown", (e) => { if (e.key === "Escape" && this.sketch) { this.cancelSketch(); e.preventDefault(); } });
    window.addEventListener("keyup", (e) => this.onModifier(e));
    // Shift shows every handle of the selection; released, only those near the pointer stay.
    for (const type of ["keydown", "keyup"] as const)
      window.addEventListener(type, (e) => { if (e.key === "Shift" && this.shiftHeld !== e.shiftKey) { this.shiftHeld = e.shiftKey; this.updateReveal(); } });

    new ResizeObserver(() => this.resize()).observe(this.el);
    this.svg.addEventListener("pointerdown", (e) => this.onDown(e));
    this.svg.addEventListener("pointermove", (e) => this.onMove(e));
    this.svg.addEventListener("pointerup", (e) => this.onUp(e));
    this.svg.addEventListener("pointercancel", () => this.cancelDrag());
    this.svg.addEventListener("pointerleave", () => {
      if (this.drag) return;
      this.clearGhost();
      this.revealPointer = undefined;
      this.updateReveal();
    });
    this.svg.addEventListener("contextmenu", (e) => e.preventDefault());
    this.svg.addEventListener("wheel", (e) => this.onWheel(e), { passive: false });
    window.addEventListener("keydown", (e) => { if (e.code === "Space" && document.activeElement === this.el) { this.spaceDown = true; e.preventDefault(); } });
    window.addEventListener("keyup", (e) => { if (e.code === "Space") this.spaceDown = false; });
  }

  /** Text edit request on double click; set by the app. */
  onEditText?: (obj: ObjectInfo) => void;

  // ------------------------------------------------------------------ coordinates

  private resize(): void {
    const r = this.el.getBoundingClientRect();
    this.cw = Math.max(50, r.width);
    this.ch = Math.max(50, r.height);
    if (!this.fitted) this.fit();
    else this.updateViewBox();
  }

  fit(): void {
    const st = this.host.state();
    const margin = 14;
    this.scale = Math.max(0.2, Math.min((this.cw - 2 * margin) / st.pageWidth, (this.ch - 2 * margin) / st.pageHeight));
    this.ox = -(this.cw / this.scale - st.pageWidth) / 2;
    this.oy = -(this.ch / this.scale - st.pageHeight) / 2;
    this.fitted = this.cw > 60;
    this.updateViewBox();
  }

  zoomBy(factor: number, at?: Vec2): void {
    const c = at ?? { x: this.ox + this.cw / this.scale / 2, y: this.oy + this.ch / this.scale / 2 };
    const ns = clamp(this.scale * factor, 0.2, 60);
    this.ox = c.x - (c.x - this.ox) * (this.scale / ns);
    this.oy = c.y - (c.y - this.oy) * (this.scale / ns);
    this.scale = ns;
    this.updateViewBox();
  }

  private updateViewBox(): void {
    this.svg.setAttribute("viewBox", `${this.ox} ${this.oy} ${this.cw / this.scale} ${this.ch / this.scale}`);
    this.drawOverlay();
  }

  /** Client (CSS px) to page mm. */
  toPage(clientX: number, clientY: number): Vec2 {
    const r = this.svg.getBoundingClientRect();
    return { x: this.ox + (clientX - r.left) / this.scale, y: this.oy + (clientY - r.top) / this.scale };
  }

  /** Page mm to client px. */
  toClient(p: Vec2): Vec2 {
    const r = this.svg.getBoundingClientRect();
    return { x: r.left + (p.x - this.ox) * this.scale, y: r.top + (p.y - this.oy) * this.scale };
  }

  get pxPerMm(): number {
    return this.scale;
  }

  /** Snap tolerance in mm for the current zoom. */
  tolerance(): number {
    return SNAP_PX / this.scale;
  }

  /** Centre of the visible area in mm (insert by keyboard/double click in the library). */
  viewCentre(): Vec2 {
    return { x: this.ox + this.cw / this.scale / 2, y: this.oy + this.ch / this.scale / 2 };
  }

  // ------------------------------------------------------------------ rendering

  render(): void {
    const st = this.host.state();
    for (const r of [this.pageRect, this.gridRect]) {
      r.setAttribute("width", String(st.pageWidth));
      r.setAttribute("height", String(st.pageHeight));
    }
    const pattern = this.svg.querySelector("#isk-grid");
    if (pattern && st.grid > 0) { pattern.setAttribute("width", String(st.grid)); pattern.setAttribute("height", String(st.grid)); }
    this.gridRect.style.display = st.grid > 0 && this.host.snapEnabled() ? "" : "none";
    if (st.svg !== undefined && st.revision !== this.renderedRevision) {
      this.content.replaceChildren(...svgChildren(st.svg));
      this.renderedRevision = st.revision;
    }
    if (!this.fitted) this.fit();
    this.drawOverlay();
  }

  private objects(ids: string[]): ObjectInfo[] {
    const set = new Set(ids);
    return this.host.state().objects.filter((o) => set.has(o.id));
  }

  /** Frame of the selection: the object's own rotated box for one box object, else the axis-aligned bounds. */
  private frameOf(ids: string[]): Frame | undefined {
    const objs = this.objects(ids);
    if (objs.length === 0) return undefined;
    if (objs.length === 1 && objs[0].kind === "box") {
      const o = objs[0];
      return { cx: o.cx, cy: o.cy, w: o.w, h: o.h, rot: o.rotation };
    }
    const b = this.host.state().selectionBounds;
    let x0 = Infinity, y0 = Infinity, x1 = -Infinity, y1 = -Infinity;
    for (const o of objs) {
      x0 = Math.min(x0, o.bounds.x); y0 = Math.min(y0, o.bounds.y);
      x1 = Math.max(x1, o.bounds.x + o.bounds.w); y1 = Math.max(y1, o.bounds.y + o.bounds.h);
    }
    const r = b && ids === this.host.state().selection ? b : { x: x0, y: y0, w: x1 - x0, h: y1 - y0 };
    return { cx: r.x + r.w / 2, cy: r.y + r.h / 2, w: r.w, h: r.h, rot: 0 };
  }

  private framePoint(f: Frame, u: number, v: number): Vec2 {
    const p = rotateVec({ x: (u * f.w) / 2, y: (v * f.h) / 2 }, f.rot);
    return { x: f.cx + p.x, y: f.cy + p.y };
  }

  /** Selection frame (page mm) for the floating context bar. */
  selectionScreenBox(withHandles = false): { left: number; top: number; right: number; bottom: number } | undefined {
    const f = this.frameOf(this.host.state().selection);
    if (!f) return undefined;
    const pts = [[-1, -1], [1, -1], [1, 1], [-1, 1]].map(([u, v]) => this.toClient(this.framePoint(f, u, v)));
    if (withHandles) {
      // The bar keeps clear of the rotate handle and the build "+" (they stay clickable).
      const top = this.framePoint(f, 0, -1);
      const up = rotateVec({ x: 0, y: -(ROTATE_PX + HANDLE_PX) / this.scale }, f.rot);
      pts.push(this.toClient({ x: top.x + up.x, y: top.y + up.y }));
      const r = PORT_RADIUS_PX + 2;
      for (const c of this.portPlus.map((p) => this.toClient(p))) pts.push({ x: c.x - r, y: c.y - r }, { x: c.x + r, y: c.y + r });
    }
    return { left: Math.min(...pts.map((p) => p.x)), top: Math.min(...pts.map((p) => p.y)), right: Math.max(...pts.map((p) => p.x)), bottom: Math.max(...pts.map((p) => p.y)) };
  }

  /** Starts sketch mode: the next drag draws a trail (shown live) that `done` receives on release; Esc cancels. */
  startSketch(done: (points: number[][]) => void): void {
    this.sketch = { done, points: [], drawing: false };
    this.el.style.cursor = "crosshair";
  }

  /** Leaves sketch mode without a result. */
  cancelSketch(): void {
    this.sketch = undefined;
    this.el.style.cursor = "";
    this.partLayer.querySelectorAll(".isk-sketch-trail").forEach((n) => n.remove());
  }

  get sketching(): boolean {
    return !!this.sketch;
  }

  private drawSketchTrail(): void {
    this.partLayer.querySelectorAll(".isk-sketch-trail").forEach((n) => n.remove());
    const pts = this.sketch?.points ?? [];
    if (pts.length >= 2) this.partLayer.append(s("polyline", { class: "isk-part-outline isk-sketch-trail", points: pts.map((p) => `${p[0]},${p[1]}`).join(" ") }));
  }

  /** Outline of the selected part (page mm polylines); none clears it. The part glows, its element fainter. */
  setPartOutline(lines?: number[][][]): void {
    this.partLines = lines?.length ? lines : undefined;
    this.partLayer.replaceChildren(...(lines ?? []).map((l) => s("polyline", { class: "isk-part-outline", points: l.map((p) => `${p[0]},${p[1]}`).join(" ") })));
    this.haloKey = "";
    this.drawHalo();
  }

  /** Elements a hovered "Select connected" entry would add (fainter glow); empty clears the preview. */
  setHaloPreview(ids: string[]): void {
    this.haloPreview = ids;
    this.drawHalo();
  }

  /** What the halo shows as last drawn (UI checks): selected ids, previewed ids and whether a part glows. */
  get haloState(): { selected: number; preview: number; part: boolean } {
    return {
      selected: this.haloLayer.querySelectorAll(":scope > g.isk-halo-sel").length,
      preview: this.haloLayer.querySelectorAll(":scope > g.isk-halo-preview").length,
      part: !!this.haloLayer.querySelector(":scope > g.isk-halo-partglow"),
    };
  }

  /**
   * The selection look (docs/en/design/interaction-ux.md section 6a, like the COM add-in's SelectionHalo): every selected
   * element gets a soft blue glow along its drawn lines and shapes – a copy of its drawing, widened and blurred by an SVG
   * filter – with the element's own strokes and filled areas cut out, so the glow lies around it as if behind it. The
   * layer lies above the drawing, so an outline running under another element still glows over it, and under every
   * handle. Hidden while dragging (the preview takes over). A selected part glows in full, its element fainter; what a
   * hovered "Select connected" entry would add glows fainter too.
   */
  drawHalo(): void {
    const st = this.host.state();
    const d = this.drag;
    const hidden = !!d && d.kind !== "marquee" && d.kind !== "pan" && d.kind !== "tap" && !("moved" in d && !d.moved);
    const preview = this.haloPreview.filter((id) => !st.selection.includes(id));
    const partKey = this.partLines ? this.partLines.reduce((n, l) => n + l.length, 0) + ":" + (this.partLines[0]?.[0]?.join(",") ?? "") : "";
    const key = hidden ? "hidden" : `${st.revision}|${st.selection.join(",")}|${preview.join(",")}|${this.scale.toFixed(3)}|${partKey}`;
    if (key === this.haloKey) return;
    this.haloKey = key;
    this.haloLayer.replaceChildren();
    this.haloDefs.replaceChildren();
    if (hidden) return;
    let n = 0;
    const add = (node: Element, bounds: { x: number; y: number; w: number; h: number }, opacity: number, cls: string) => {
      const id = `isk-halo-${n++}`;
      this.haloDefs.append(this.haloFilter(id, bounds, opacity));
      const g = s("g", { class: cls, filter: `url(#${id})` });
      g.append(node);
      this.haloLayer.append(g);
    };
    const layers: [string[], number, string][] = [[preview, 0.24, "isk-halo-preview"], [st.selection, this.partLines ? 0.22 : 0.48, "isk-halo-sel"]];
    for (const [ids, opacity, cls] of layers) {
      for (const id of ids) {
        const o = st.objects.find((x) => x.id === id);
        const src = this.content.querySelector(`[data-isk-id="${CSS.escape(id)}"]`);
        if (!o || !src) continue;
        const copy = src.cloneNode(true) as Element;
        copy.removeAttribute("data-isk-id");
        copy.querySelectorAll("[data-isk-id]").forEach((x) => x.removeAttribute("data-isk-id"));
        add(copy, o.bounds, opacity, cls);
      }
    }
    if (this.partLines) {
      const g = s("g", { class: "isk-halo-part" });
      let x0 = Infinity, y0 = Infinity, x1 = -Infinity, y1 = -Infinity;
      for (const l of this.partLines) {
        g.append(s("polyline", { points: l.map((p) => `${p[0]},${p[1]}`).join(" ") }));
        for (const p of l) { x0 = Math.min(x0, p[0]); y0 = Math.min(y0, p[1]); x1 = Math.max(x1, p[0]); y1 = Math.max(y1, p[1]); }
      }
      if (Number.isFinite(x0)) add(g, { x: x0, y: y0, w: x1 - x0, h: y1 - y0 }, 0.55, "isk-halo-partglow");
    }
  }

  /** The glow filter of one element: widen its alpha, blur, tint, then cut the element itself out. */
  private haloFilter(id: string, b: { x: number; y: number; w: number; h: number }, opacity: number): SVGFilterElement {
    const px = 1 / this.scale;
    const spread = 3.5 * px, blur = 4 * px, pad = spread + 3 * blur + 2 * px;
    const f = s("filter", {
      id, filterUnits: "userSpaceOnUse", "color-interpolation-filters": "sRGB",
      x: b.x - pad, y: b.y - pad, width: b.w + 2 * pad, height: b.h + 2 * pad,
    });
    f.append(
      s("feMorphology", { in: "SourceAlpha", operator: "dilate", radius: spread.toFixed(4), result: "wide" }),
      s("feGaussianBlur", { in: "wide", stdDeviation: blur.toFixed(4), result: "soft" }),
      s("feFlood", { "flood-color": "#006ee6", "flood-opacity": opacity, result: "tint" }),
      s("feComposite", { in: "tint", in2: "soft", operator: "in", result: "glow" }),
      s("feComposite", { in: "glow", in2: "SourceAlpha", operator: "out" }),
    );
    return f;
  }

  drawOverlay(previewFrame?: Frame, previewPoints?: { obj: ObjectInfo; pts: Vec2[] }): void {
    this.revealables = [];
    this.shownLinks = [];
    this.shownSegs = [];
    this.segPlaced = [];
    this.shownGaps = [];
    this.drawOverlayCore(previewFrame, previewPoints);
    if (!previewFrame && !previewPoints && !this.drag) this.drawValueBadge();
    this.updateReveal();
  }

  private drawOverlayCore(previewFrame?: Frame, previewPoints?: { obj: ObjectInfo; pts: Vec2[] }): void {
    this.drawHalo();
    this.overlay.replaceChildren();
    this.portPlus = [];
    const st = this.host.state();
    const sel = st.selection;
    if (sel.length === 0 && !previewFrame) return;
    const px = 1 / this.scale;
    const objs = this.objects(sel);
    // Multi selection: every member glows (drawHalo), so no extra boxes are drawn.
    const single = objs.length === 1 ? objs[0] : undefined;
    // Motion mode on a part that can move: only what moves it (joints of a link, the gear's frame and centre); no resize,
    // rotate, quick-add or build handles, so every drag on it is a motion.
    const motion = single && !previewFrame ? this.motionFor(single) : undefined;
    if (single?.kind === "points" && !previewFrame) {
      const pts = previewPoints?.obj.id === single.id ? previewPoints.pts : single.points ?? [];
      this.overlay.append(s("polyline", { class: "isk-sel-path", points: pts.map((p) => `${p.x},${p.y}`).join(" ") }));
      pts.forEach((p, i) => {
        const c = s("circle", { class: `isk-handle isk-handle-point${motion ? " isk-motion-joint" : ""}`, cx: p.x, cy: p.y, r: (HANDLE_PX / 2 + 1) * px });
        c.dataset.handle = JSON.stringify({ kind: "point", index: i } satisfies HandleSpec);
        this.overlay.append(c);
      });
      if (!previewPoints && !single.locked && !motion) {
        const grips = this.drawGrips(single);
        const small = Math.max(single.bounds.w, single.bounds.h) * this.scale < SMALL_ELEMENT_PX;
        this.drawMovable(single, [...pts.map((p) => ({ p, r: HANDLE_PX / 2 + 1 })), ...grips], { x: single.bounds.x + single.bounds.w, y: single.bounds.y }, small);
      }
      return;
    }
    const f = previewFrame ?? this.frameOf(sel);
    if (!f) return;
    const corners = [[-1, -1], [1, -1], [1, 1], [-1, 1]].map(([u, v]) => this.framePoint(f, u, v));
    this.overlay.append(s("polygon", { class: `isk-sel-frame${motion ? " isk-motion-frame" : ""}`, points: corners.map((p) => `${p.x},${p.y}`).join(" ") }));
    if (motion?.kind === "gear" || motion?.kind === "mechanism") {
      this.overlay.append(this.pivotMark({ x: motion.x, y: motion.y }));
      return;
    }
    if (objs.some((o) => o.locked)) return;
    const top = this.framePoint(f, 0, -1);
    const up = rotateVec({ x: 0, y: -ROTATE_PX * px }, f.rot);
    const rh = { x: top.x + up.x, y: top.y + up.y };
    this.overlay.append(s("line", { class: "isk-sel-stem", x1: top.x, y1: top.y, x2: rh.x, y2: rh.y }));
    const rot = s("circle", { class: "isk-handle isk-handle-rotate", cx: rh.x, cy: rh.y, r: (HANDLE_PX / 2 + 1) * px });
    rot.dataset.handle = JSON.stringify({ kind: "rotate" } satisfies HandleSpec);
    this.overlay.append(rot);
    const size = HANDLE_PX * px;
    const handles: { p: Vec2; r: number }[] = [{ p: rh, r: HANDLE_PX / 2 + 1 }];
    // Quick add: blue triangles outside each side of a chainable element (click: connected copy; drag: drop it there).
    // The one below is the element's primary handle; the others fade in near the pointer (COM `TriangleReveal`).
    if (single?.chain && !previewFrame && !f.rot) {
      const sides: [ChainSide, number, number][] = [["left", -1, 0], ["top", 0, -1], ["right", 1, 0], ["bottom", 0, 1]];
      for (const [side, u, v] of sides) {
        const edge = this.framePoint(f, u, v);
        const off = (side === "top" ? ROTATE_PX + CHAIN_PX : CHAIN_PX) * px;
        const c = { x: edge.x + u * off, y: edge.y + v * off };
        const hs = (CHAIN_SIZE_PX / 2) * px;
        const tip = { x: c.x + u * hs, y: c.y + v * hs };
        const b1 = { x: c.x - u * hs - v * hs, y: c.y - v * hs - u * hs };
        const b2 = { x: c.x - u * hs + v * hs, y: c.y - v * hs + u * hs };
        const tri = s("polygon", { class: "isk-chain", points: `${tip.x},${tip.y} ${b1.x},${b1.y} ${b2.x},${b2.y}` });
        tri.dataset.handle = JSON.stringify({ kind: "chain", side } satisfies HandleSpec);
        tri.append(s("title", {}));
        this.overlay.append(tri);
        // Quick-add triangles are fixed handles too: the "+" keep clear of them.
        handles.push({ p: c, r: CHAIN_SIZE_PX / 2 + 1 });
        this.revealables.push({ el: tri, at: c, primary: side === "bottom" });
      }
    }
    for (const [u, v] of [[-1, -1], [0, -1], [1, -1], [1, 0], [1, 1], [0, 1], [-1, 1], [-1, 0]]) {
      const p = this.framePoint(f, u, v);
      handles.push({ p, r: HANDLE_PX / 2 + 1 });
      const corner = u !== 0 && v !== 0;
      const r = s("rect", {
        class: `isk-handle ${corner ? "isk-handle-corner" : "isk-handle-side"}`,
        x: p.x - size / 2, y: p.y - size / 2, width: size, height: size,
        transform: f.rot ? `rotate(${f.rot} ${p.x} ${p.y})` : undefined,
      });
      r.dataset.handle = JSON.stringify({ kind: "scale", sx: u, sy: v } satisfies HandleSpec);
      this.overlay.append(r);
    }
    if (single && !previewFrame) {
      handles.push(...this.drawGrips(single));
      const small = Math.max(f.w, f.h) * this.scale < SMALL_ELEMENT_PX;
      this.drawMovable(single, handles, { x: Math.max(...corners.map((c) => c.x)), y: Math.min(...corners.map((c) => c.y)) }, small);
    }
  }

  /** Grips of a single selected element (engine `dragHandles`), cached per selection and revision. */
  private gripsOf(obj: ObjectInfo): DragHandleInfo[] {
    if (!obj.known) return [];
    const key = `${obj.id}@${this.host.state().revision}`;
    if (this.grips?.key !== key) {
      let list: DragHandleInfo[] = [];
      try {
        list = this.host.session().dragHandles([obj.id], lang()).find((h) => h.id === obj.id)?.handles ?? [];
      } catch {
        list = [];
      }
      this.grips = { key, list };
    }
    return this.grips.list;
  }

  /**
   * Grips on values (interaction-ux.md section 4, like the COM add-in): a blue-rimmed square with small arrowheads in
   * the directions it moves, at chart limit lines, value lines, markers; the point grips of a curve are smaller. The grip
   * of the selected part and the grips that come with it (the points of a selected curve) are shown in full, every other
   * one fades in near the pointer. A whole curve has no grip of its own: it is dragged by pressing on the selected curve.
   * Drag: the line follows (the element constrains and snaps), the new value shows beside the pointer, release is one
   * undo step, Esc cancels; a click selects the grip's part. Returns them as layout obstacles.
   */
  private drawGrips(obj: ObjectInfo): { p: Vec2; r: number }[] {
    const all = this.gripsOf(obj);
    const part = this.host.selectedPart?.(obj.id);
    this.shownGrips = all.filter((g) => !g.byPart);
    const px = 1 / this.scale;
    const a = 3 * px;
    return this.shownGrips.map((g, i) => {
      const point = !!g.showWith;
      const size = (point ? 7 : 9) * px;
      const grp = s("g", { class: `isk-grip${point ? " isk-grip-point" : ""}${g.part && g.part === part ? " is-selected" : ""}` });
      grp.dataset.handle = JSON.stringify({ kind: "grip", index: i } satisfies HandleSpec);
      const title = s("title");
      title.textContent = `${g.label} · ${t("handle.gripDrag")}`;
      grp.append(title, point
        ? s("circle", { cx: g.x, cy: g.y, r: size / 2 })
        : s("rect", { x: g.x - size / 2, y: g.y - size / 2, width: size, height: size, rx: 1.5 * px }));
      // Arrowheads show the allowed direction (both ways along it); a free grip has none.
      if (g.constraint !== "free" && !point) {
        const len = Math.hypot(g.dx, g.dy) || 1;
        const d = { x: g.dx / len, y: g.dy / len }, n = { x: -d.y, y: d.x };
        for (const sgn of [1, -1]) {
          const tip = { x: g.x + sgn * d.x * (size / 2 + 2 * a), y: g.y + sgn * d.y * (size / 2 + 2 * a) };
          const base = { x: g.x + sgn * d.x * (size / 2 + 0.6 * a), y: g.y + sgn * d.y * (size / 2 + 0.6 * a) };
          grp.append(s("path", { class: "isk-grip-arrow", d: `M${tip.x} ${tip.y}L${base.x + n.x * a} ${base.y + n.y * a}L${base.x - n.x * a} ${base.y - n.y * a}Z` }));
        }
      }
      this.overlay.append(grp);
      this.revealables.push({ el: grp, at: { x: g.x, y: g.y }, primary: shownWith(g, part, all) });
      return { p: { x: g.x, y: g.y }, r: point ? 5 : 7 };
    });
  }

  /** Live preview of a dragged grip: where it lands (snapped by the element) and its new value beside the pointer. */
  private showGrip(id: string, grip: DragHandleInfo, to: Vec2, client: Vec2): void {
    if (this.drag?.kind !== "grip") return;
    let h: DragHandleInfo | null = null;
    try {
      h = this.host.session().moveHandlePreview(id, grip.key, to.x, to.y, lang());
    } catch {
      h = null;
    }
    if (!h) return;
    const size = 9 / this.scale;
    if (grip.byPart) {
      // A whole curve: its outline moved by the snapped offset (the values move along on release).
      const dx = h.x - grip.x, dy = h.y - grip.y;
      this.ghost.replaceChildren(...(this.partLines ?? []).map((l) => s("polyline", { class: "isk-part-ghost", points: l.map((p) => `${p[0] + dx},${p[1] + dy}`).join(" ") })));
    } else {
      this.ghost.replaceChildren(
        s("line", { class: "isk-grip-lead", x1: grip.x, y1: grip.y, x2: h.x, y2: h.y }),
        s("rect", { class: "isk-grip-ghost", x: h.x - size / 2, y: h.y - size / 2, width: size, height: size }),
      );
    }
    this.showScaleLabel(h.label, client);
  }

  /** Build ports of a single selected element (engine `buildPorts`), cached per selection and revision. */
  private portsOf(obj: ObjectInfo): BuildPortInfo[] {
    if (!obj.known) return [];
    const key = `${obj.id}@${this.host.state().revision}`;
    if (this.ports?.key !== key) {
      let list: BuildPortInfo[] = [];
      try {
        list = this.host.session().buildPorts(obj.id);
      } catch {
        list = [];
      }
      this.ports = { key, list };
    }
    return this.ports.list;
  }

  /** Lock badges of the selected element (engine `links`), only while "Anchor" is on (as the COM add-in). */
  private linksOf(obj: ObjectInfo): LinkInfo[] {
    if (!obj.known || !this.host.anchorEnabled()) return [];
    const key = `${obj.id}@${this.host.state().revision}@${lang()}`;
    if (this.links?.key !== key) {
      let list: LinkInfo[] = [];
      try {
        list = this.host.session().links(obj.id, lang());
      } catch {
        list = [];
      }
      this.links = { key, list };
    }
    return this.links.list;
  }

  /** Corner dots and branch handles of a selected line with free corners (engine `polylineHandles`). */
  private polyOf(obj: ObjectInfo): PolylineHandles | undefined {
    if (!obj.known || obj.kind !== "points") return undefined;
    const key = `${obj.id}@${this.host.state().revision}`;
    if (this.poly?.key !== key) {
      let info: PolylineHandles | undefined;
      try {
        info = this.host.session().polylineHandles(obj.id);
      } catch {
        info = undefined;
      }
      this.poly = { key, info: info?.corners ? info : undefined };
    }
    return this.poly.info;
  }

  /** Insert places of a selected element (engine `insertHandles`: between table rows and columns). */
  private gapsOf(obj: ObjectInfo): InsertHandleInfo[] {
    if (!obj.known || obj.kind !== "box") return [];
    const key = `${obj.id}@${this.host.state().revision}@${lang()}`;
    if (this.gaps?.key !== key) {
      let list: InsertHandleInfo[] = [];
      try {
        list = this.host.session().insertHandles(obj.id, lang());
      } catch {
        list = [];
      }
      this.gaps = { key, list };
    }
    return this.gaps.list;
  }

  /**
   * Every movable handle of the selected element in one layout (`resolveHandles`, the COM HandleLayout): the frame
   * handles, point diamonds, triangles and grips stay; then, by priority, the build "+" (about 15 px beyond its port; a
   * free hub's beyond the top right corner), the insert dots between table rows and columns, the lock badges beside their
   * contact point, the corner dots on the segments of a line (they may slide along their segment) and the branch handles
   * beside them – each on the first free place around its preferred one with a 3 px gap; optional ones without room are
   * left out, and none of the corner dots and branch handles on a very small element or a short segment. A "+" that had to
   * move keeps a thin dashed lead to its port. Only the primary "+" is shown right away; the rest fades in near the pointer.
   */
  private drawMovable(obj: ObjectInfo, obstacles: { p: Vec2; r: number }[], topRight: Vec2, small: boolean): void {
    const ports = this.portsOf(obj);
    const gaps = this.gapsOf(obj);
    const links = this.linksOf(obj);
    const poly = small ? undefined : this.polyOf(obj);
    const px = 1 / this.scale;
    const toPx = (p: Vec2) => ({ x: p.x * this.scale, y: p.y * this.scale });
    const specs: LayoutSpec[] = obstacles.map((o, i) => ({ id: `fixed:${i}`, kind: "frame", anchor: toPx(o.p), dir: { x: 0, y: 0 }, offset: 0, radius: o.r, priority: 0, fixed: true }));
    const preferred: Vec2[] = [];
    ports.forEach((port, i) => {
      const hub = port.kind === "Hub";
      const anchor = hub ? toPx(topRight) : toPx(port);
      const dir = hub ? { x: 1, y: -1 } : { x: port.dx, y: port.dy };
      const offset = hub ? HUB_CORNER_PX * Math.SQRT2 : PORT_OFFSET_PX;
      const len = Math.hypot(dir.x, dir.y) || 1;
      preferred.push({ x: anchor.x + (dir.x / len) * offset, y: anchor.y + (dir.y / len) * offset });
      specs.push({ id: `port:${i}`, kind: "add", anchor, dir, offset, radius: PORT_RADIUS_PX, priority: PRIORITY.add, maxPush: 48 });
    });
    gaps.forEach((g, i) => specs.push({ id: `gap:${i}`, kind: "add", anchor: toPx(g), dir: { x: g.dx, y: g.dy }, offset: 0, radius: PORT_RADIUS_PX * GAP_SCALE, priority: 50, optional: true, maxPush: 18 }));
    links.forEach((l, i) => specs.push({ id: `lock:${i}`, kind: "lock", anchor: toPx(l), dir: { x: Math.SQRT1_2, y: -Math.SQRT1_2 }, offset: BADGE_OFFSET_PX * Math.SQRT2, radius: BADGE_PX, priority: PRIORITY.lock, orbit: true, maxPush: 48 }));
    const segs: SegmentHandleInfo[] = [];
    for (const seg of poly?.segments ?? []) {
      const a = toPx({ x: seg.ax, y: seg.ay }), b = toPx({ x: seg.bx, y: seg.by });
      const len = Math.hypot(b.x - a.x, b.y - a.y);
      if (len < MIN_SEGMENT_PX) continue;
      const k = segs.length;
      segs.push(seg);
      // The corner dot stays on its segment: it may slide along it (a quarter of the length each way).
      specs.push({ id: `corner:${k}`, kind: "corner", anchor: toPx(seg), dir: { x: (b.x - a.x) / len, y: (b.y - a.y) / len }, offset: 0, radius: CORNER_DOT_PX, priority: PRIORITY.corner, optional: true, along: true, maxPush: len / 4 });
      if (poly?.branch) specs.push({ id: `branch:${k}`, kind: "branch", anchor: toPx(seg), dir: { x: seg.nx, y: seg.ny }, offset: BRANCH_OFFSET_PX, radius: BRANCH_PX, priority: PRIORITY.branch, optional: true, orbit: true, maxPush: 10 });
    }
    const placed = resolveHandles(specs);
    const at = (id: string) => {
      const h = placed.find((x) => x.spec.id === id);
      return h ? { at: { x: h.at.x * px, y: h.at.y * px }, visible: h.visible } : undefined;
    };
    const primary = primaryPort(ports, obj.kind === "points" ? obj.points : undefined, !!obj.chain);
    // Draw order: corner dots and branches lowest, then insert dots, lock badges and the "+" on top.
    segs.forEach((seg, k) => {
      const dot = at(`corner:${k}`);
      if (dot?.visible) {
        const c = s("circle", { class: "isk-corner", cx: dot.at.x, cy: dot.at.y, r: CORNER_DOT_PX * px });
        c.dataset.handle = JSON.stringify({ kind: "corner", index: k } satisfies HandleSpec);
        const title = s("title");
        title.textContent = t("handle.corner");
        c.append(title);
        this.overlay.append(c);
        this.revealables.push({ el: c, at: dot.at, anchor: { x: seg.x, y: seg.y }, primary: false });
      }
      this.segPlaced[k] = dot?.visible ? dot.at : { x: seg.x, y: seg.y };
      const br = at(`branch:${k}`);
      if (br?.visible) {
        const r = BRANCH_PX * px, kk = r * 0.55, d = 1.3 * px;
        const g = s("g", { class: "isk-branch" });
        g.dataset.handle = JSON.stringify({ kind: "branch", index: k } satisfies HandleSpec);
        const title = s("title");
        title.textContent = t("handle.branch");
        g.append(title, s("circle", { cx: br.at.x, cy: br.at.y, r }),
          s("circle", { class: "isk-branch-dot", cx: br.at.x, cy: br.at.y + kk, r: d }),
          s("path", { d: `M${br.at.x} ${br.at.y + kk}V${br.at.y - kk}` }));
        this.overlay.append(g);
        this.revealables.push({ el: g, at: br.at, anchor: { x: seg.x, y: seg.y }, primary: false });
      }
    });
    this.shownSegs = segs;
    gaps.forEach((gap, i) => {
      const h = at(`gap:${i}`);
      if (!h?.visible) return;
      const r = PORT_RADIUS_PX * GAP_SCALE * px, k = r * 0.55;
      const g = s("g", { class: "isk-port isk-gap" });
      g.dataset.handle = JSON.stringify({ kind: "gap", index: i } satisfies HandleSpec);
      const title = s("title");
      title.textContent = gap.label;
      g.append(title, s("circle", { cx: h.at.x, cy: h.at.y, r }), s("path", { d: `M${h.at.x - k} ${h.at.y}H${h.at.x + k}M${h.at.x} ${h.at.y - k}V${h.at.y + k}` }));
      this.overlay.append(g);
      this.revealables.push({ el: g, at: h.at, primary: false });
    });
    this.shownGaps = gaps;
    links.forEach((link, i) => {
      const h = at(`lock:${i}`);
      if (!h) return;
      // Quiet by default: a tiny dot on the contact point; the badge fades in near the pointer (or with Shift).
      this.overlay.append(s("circle", { class: `isk-lock-contact${link.released ? " is-released" : ""}`, cx: link.x, cy: link.y, r: 2 * px }));
      const g = s("g", { class: `isk-lock${link.released ? " is-released" : ""}` });
      g.dataset.handle = JSON.stringify({ kind: "lock", index: i } satisfies HandleSpec);
      const title = s("title");
      title.textContent = link.label;
      g.append(title, s("line", { class: "isk-lock-lead", x1: link.x, y1: link.y, x2: h.at.x, y2: h.at.y }), s("circle", { class: "isk-lock-ring", cx: h.at.x, cy: h.at.y, r: BADGE_PX * px }), this.lockGlyph(h.at, BADGE_PX * 0.58 * px, link.released));
      this.overlay.append(g);
      this.revealables.push({ el: g, at: h.at, anchor: { x: link.x, y: link.y }, primary: false });
    });
    this.shownLinks = links;
    this.portPlus = [];
    ports.forEach((port, i) => {
      const h = at(`port:${i}`);
      if (!h) return;
      const c = h.at;
      this.portPlus.push(c);
      const g = s("g", { class: `isk-port${port.stretch ? " isk-port-stretch" : ""}` });
      if (Math.hypot(c.x * this.scale - preferred[i].x, c.y * this.scale - preferred[i].y) > 1) g.append(s("line", { class: "isk-port-lead", x1: port.x, y1: port.y, x2: c.x, y2: c.y }));
      const r = PORT_RADIUS_PX * px, k = r * 0.55;
      g.dataset.handle = JSON.stringify({ kind: "port", index: i } satisfies HandleSpec);
      g.dataset.port = port.id;
      const title = s("title");
      title.textContent = t(portTipKey(port));
      g.append(title, s("circle", { cx: c.x, cy: c.y, r }), s("path", { d: `M${c.x - k} ${c.y}H${c.x + k}M${c.x} ${c.y - k}V${c.y + k}` }));
      this.overlay.append(g);
      this.revealables.push({ el: g, at: c, anchor: { x: port.x, y: port.y }, primary: i === primary });
    });
  }

  /** A padlock centred at `c` (page mm): body and shackle, the shackle lifted and open when released (COM `DrawLock`). */
  private lockGlyph(c: Vec2, size: number, open: boolean): SVGElement {
    const bw = size * 1.5, bh = size * 1.05;
    const top = c.y - bh * 0.15;
    const sw = bw * 0.62, sh = size * 0.95, lift = open ? size * 0.35 : 0;
    const arcTop = top - sh - lift;
    const legTop = arcTop + sh * 0.8;
    const x0 = c.x - sw / 2, x1 = c.x + sw / 2;
    const shackle = `M${x0} ${top}V${legTop}A${sw / 2} ${sh * 0.8} 0 0 1 ${x1} ${legTop}${open ? "" : `V${top}`}`;
    const g = s("g", { class: "isk-lock-glyph" });
    g.append(s("rect", { x: c.x - bw / 2, y: top, width: bw, height: bh }), s("path", { d: shackle }));
    return g;
  }

  /**
   * The ⚠ badge (like the COM add-in): a small amber triangle just outside the top right corner of a selected element
   * whose entered values conflict, in Explore and Engineer; a click opens its value card.
   */
  private drawValueBadge(): void {
    const st = this.host.state();
    if (st.selection.length !== 1 || !this.host.valueConflict) return;
    const o = this.objects(st.selection)[0];
    if (!o?.hasValues || !this.host.valueConflict(o.id)) return;
    const f = o.kind === "box" ? this.frameOf(st.selection) : undefined;
    const pts = f ? [[-1, -1], [1, -1], [1, 1], [-1, 1]].map(([u, v]) => this.framePoint(f, u, v)) : [{ x: o.bounds.x, y: o.bounds.y }, { x: o.bounds.x + o.bounds.w, y: o.bounds.y }];
    const px = 1 / this.scale, off = VALUE_BADGE_PX * px, hgt = 11 * px;
    const c = { x: Math.max(...pts.map((p) => p.x)) + off, y: Math.min(...pts.map((p) => p.y)) - off };
    const g = s("g", { class: "isk-value-badge" });
    g.dataset.handle = JSON.stringify({ kind: "valuebadge" } satisfies HandleSpec);
    const title = s("title");
    title.textContent = t("val.badge");
    g.append(title,
      s("path", { d: `M${c.x} ${c.y - hgt / 2}L${c.x + hgt * 0.58} ${c.y + hgt / 2}L${c.x - hgt * 0.58} ${c.y + hgt / 2}Z` }),
      s("path", { class: "isk-value-badge-ink", d: `M${c.x} ${c.y - hgt * 0.18}V${c.y + hgt * 0.18}M${c.x} ${c.y + hgt * 0.3}V${c.y + hgt * 0.36}` }));
    this.overlay.append(g);
  }

  /** The ⚠ badge as last drawn (UI checks): "conflict" or "". */
  get valueBadge(): string {
    return this.overlay.querySelector(".isk-value-badge") ? "conflict" : "";
  }

  /**
   * Progressive disclosure (COM `Reveal`): right after a selection only the frame, the point diamonds, the one primary
   * "+" and the grips of a selected part are shown; every other handle fades in within about 40 px of the pointer (fully
   * within 24 px, gone beyond 56 px) and Shift shows all. Hidden handles take no clicks.
   */
  private updateReveal(): void {
    const ptr = this.revealPointer ? { x: this.revealPointer.x * this.scale, y: this.revealPointer.y * this.scale } : undefined;
    for (const r of this.revealables) {
      const o = revealOpacity({ x: r.at.x * this.scale, y: r.at.y * this.scale }, r.anchor ? { x: r.anchor.x * this.scale, y: r.anchor.y * this.scale } : undefined, ptr, r.primary, this.shiftHeld);
      r.el.style.opacity = o >= 0.98 ? "" : o.toFixed(2);
      r.el.style.pointerEvents = o <= 0.05 ? "none" : "";
    }
  }

  /** Ids of the handles shown at least half (UI checks: calm right after a selection). */
  get shownHandles(): string[] {
    return this.revealables.filter((r) => r.el.style.opacity === "" || Number(r.el.style.opacity) >= 0.5)
      .map((r) => { try { const h = JSON.parse(r.el.dataset.handle ?? "{}") as { kind?: string; index?: number; side?: string }; return `${h.kind}:${h.index ?? h.side ?? ""}`; } catch { return ""; } });
  }

  private drawGuides(guides: SnapGuide[]): void {
    this.guides.replaceChildren();
    const ext = 4000;
    for (const g of guides) {
      switch (g.kind) {
        case "vertical": this.guides.append(s("line", { x1: g.a.x, y1: -ext, x2: g.a.x, y2: ext })); break;
        case "horizontal": this.guides.append(s("line", { x1: -ext, y1: g.a.y, x2: ext, y2: g.a.y })); break;
        case "segment": this.guides.append(s("line", { x1: g.a.x, y1: g.a.y, x2: g.b.x, y2: g.b.y })); break;
        case "circle": this.guides.append(s("circle", { cx: g.a.x, cy: g.a.y, r: g.radius })); break;
        case "point": {
          const r = 5 / this.scale;
          this.guides.append(s("path", { d: `M${g.a.x - r} ${g.a.y - r}L${g.a.x + r} ${g.a.y + r}M${g.a.x - r} ${g.a.y + r}L${g.a.x + r} ${g.a.y - r}`, class: "isk-guide-point" }));
          break;
        }
      }
    }
  }

  private previewTransform(ids: string[], transform: string | null): void {
    for (const id of ids) {
      const g = this.content.querySelector(`[data-isk-id="${CSS.escape(id)}"]`);
      if (!g) continue;
      if (transform) g.setAttribute("transform", transform);
      else g.removeAttribute("transform");
    }
  }

  // ------------------------------------------------------------------ motion mode

  /** Engine `motionInfo` of `id` (cached per document revision); "none" when the engine cannot tell. */
  motionOf(id: string): MotionInfo {
    const rev = this.host.state().revision;
    if (this.motionCache.revision !== rev) this.motionCache = { revision: rev, map: new Map() };
    let info = this.motionCache.map.get(id);
    if (!info) {
      try {
        info = this.host.session().motionInfo(id);
      } catch {
        info = { kind: "none" };
      }
      this.motionCache.map.set(id, info);
    }
    return info;
  }

  /** How `obj` moves while motion mode is on (gear train or linkage); undefined outside the mode or for other parts. */
  private motionFor(obj: ObjectInfo): MovingInfo | undefined {
    if (!this.host.motionEnabled?.() || !obj.known || obj.locked) return undefined;
    const info = this.motionOf(obj.id);
    return canMove(info) && info.kind !== "none" ? info : undefined;
  }

  /** Small cross at a gear's centre: the pivot it turns about. */
  private pivotMark(c: Vec2): SVGElement {
    const r = 5 / this.scale;
    return s("path", { class: "isk-motion-pivot", d: `M${c.x - r} ${c.y}H${c.x + r}M${c.x} ${c.y - r}V${c.y + r}` });
  }

  /**
   * Starts a motion drag on pointer down (motion mode only): a joint handle of the selected link pulls that joint, the
   * selected gear turns anywhere on its frame, and any other part that can move under the pointer is selected and moved
   * at once. False when the pointer is on nothing that moves (then dragging edits as usual).
   */
  private beginMotion(e: PointerEvent, p: Vec2, handle: HandleSpec | undefined): boolean {
    if (!this.host.motionEnabled?.()) return false;
    const session = this.host.session();
    const sel = this.objects(this.host.state().selection);
    const single = sel.length === 1 ? sel[0] : undefined;
    const client = { x: e.clientX, y: e.clientY };
    if (handle) {
      const info = single ? this.motionFor(single) : undefined;
      if (handle.kind !== "point" || !single || info?.kind !== "linkage") return false;
      this.startMotion(single, info, p, client, handle.index);
      return true;
    }
    if (single?.kind === "box") {
      const info = this.motionFor(single);
      if ((info?.kind === "gear" || info?.kind === "mechanism") && insideBounds(single.bounds, p, 6 / this.scale)) {
        this.startMotion(single, info, p, client);
        return true;
      }
    }
    const hit = session.hitTest(p.x, p.y, 6 / this.scale);
    const obj = hit.id ? this.host.state().objects.find((o) => o.id === hit.id) : undefined;
    const info = obj ? this.motionFor(obj) : undefined;
    if (!obj || !info) return false;
    if (!this.host.state().selection.includes(obj.id)) this.host.apply(session.select([obj.id]));
    this.startMotion(obj, info, p, client);
    return true;
  }

  private startMotion(obj: ObjectInfo, info: MovingInfo, p: Vec2, client: Vec2, index?: number): void {
    if (info.kind === "gear" || info.kind === "mechanism") {
      this.drag = { kind: "motion", id: obj.id, turn: new TurnTracker({ x: info.x, y: info.y }, p), degrees: 0, index: -1, target: p, startClient: client, moved: false, dimmed: [] };
      return;
    }
    const pts = obj.points ?? [];
    const i = Math.max(0, index ?? nearestJoint(pts, p));
    this.drag = { kind: "motion", id: obj.id, degrees: 0, index: i, from: pts[i], target: p, startClient: client, moved: false, dimmed: [] };
  }

  /** Pointer move of a motion drag: the turn angle (Shift: 15° steps) or the pulled joint, previewed about 15 times a second. */
  private dragMotion(d: MotionDrag, p: Vec2, shift: boolean, client: Vec2): void {
    if (!d.moved) {
      if (Math.hypot(client.x - d.startClient.x, client.y - d.startClient.y) < MOVE_THRESHOLD_PX) return;
      d.moved = true;
      this.host.hideBar?.();
    }
    if (d.turn) {
      d.degrees = stepTurn(d.turn.update(p), shift);
      const c = d.turn.centre;
      this.overlay.replaceChildren(this.pivotMark(c), s("line", { class: "isk-motion-lever", x1: c.x, y1: c.y, x2: p.x, y2: p.y }));
      this.showSnapLabel(turnLabel(d.degrees), c, client);
      if (Math.abs(d.degrees) > 1e-9) this.motionThrottle.push(d.id, { degrees: d.degrees });
      else this.clearMotionPreview(d);
      return;
    }
    d.target = p;
    const marks: SVGElement[] = [s("circle", { class: "isk-motion-target", cx: p.x, cy: p.y, r: (HANDLE_PX / 2 + 1) / this.scale })];
    if (d.from) marks.unshift(s("line", { class: "isk-motion-lever", x1: d.from.x, y1: d.from.y, x2: p.x, y2: p.y }));
    this.overlay.replaceChildren(...marks);
    this.motionThrottle.push(d.id, { index: d.index, x: p.x, y: p.y });
  }

  /** Draws what the motion would move (engine `motionPreview`) and dims those objects' current drawing. */
  private showMotion(id: string, change: MotionChange): void {
    const d = this.drag;
    if (d?.kind !== "motion" || d.id !== id) return;
    try {
      const r = this.host.session().motionPreview(id, change);
      if (r.svg) {
        const g = s("g", { class: "isk-motion-preview" });
        g.append(...svgChildren(r.svg));
        this.ghost.replaceChildren(g);
      } else this.ghost.replaceChildren();
      this.dim(d, r.svg ? r.ids : []);
    } catch (e) {
      this.clearMotionPreview(d);
      this.host.report(e);
    }
  }

  /** Dims the current drawing of `ids` (the parts shown moved in the preview) and restores the others. */
  private dim(d: MotionDrag, ids: string[]): void {
    const next = new Set(ids);
    for (const id of d.dimmed) if (!next.has(id)) this.contentOf(id)?.classList.remove("isk-motion-source");
    for (const id of ids) this.contentOf(id)?.classList.add("isk-motion-source");
    d.dimmed = ids;
  }

  private contentOf(id: string): Element | null {
    return this.content.querySelector(`[data-isk-id="${CSS.escape(id)}"]`);
  }

  private clearMotionPreview(d: MotionDrag): void {
    this.motionThrottle.cancel();
    this.ghost.replaceChildren();
    this.dim(d, []);
  }

  // ------------------------------------------------------------------ live coupling preview

  /** Draws the neighbours the coupling would move or scale (engine `couplingPreview`) while a drag is running. */
  private showCoupling(ids: string[], change: CouplingChange): void {
    if (!this.drag) return;
    try {
      const r = this.host.session().couplingPreview(ids, change);
      if (r.svg) this.coupling.replaceChildren(...svgChildren(r.svg));
      else this.coupling.replaceChildren();
    } catch (e) {
      this.coupling.replaceChildren();
      this.host.report(e);
    }
  }

  private clearCoupling(): void {
    this.couplingThrottle.cancel();
    this.coupling.replaceChildren();
  }

  // ------------------------------------------------------------------ insert ghost

  private showGhost(item: CatalogItem, p: Vec2): Vec2 {
    let at = p;
    if (this.host.snapEnabled()) {
      try {
        const r = this.host.session().snapInsert(item.id, p.x, p.y, this.tolerance());
        at = { x: r.x, y: r.y };
        this.drawGuides(r.guides);
      } catch (e) {
        this.host.report(e);
      }
    }
    const vb = viewBoxOf(item.thumbnail);
    this.ghost.replaceChildren();
    if (vb) {
      this.ghost.append(
        s("image", { href: svgDataUrl(item.thumbnail), x: at.x + vb.x, y: at.y + vb.y, width: vb.w, height: vb.h, opacity: 0.55 }),
        s("rect", { class: "isk-ghost-outline", x: at.x + vb.x, y: at.y + vb.y, width: vb.w, height: vb.h }),
      );
    }
    return at;
  }

  clearGhost(): void {
    this.ghost.replaceChildren();
    this.guides.replaceChildren();
  }

  /** Drag from the library over the stage. */
  dragOver(item: CatalogItem, clientX: number, clientY: number): void {
    this.showGhost(item, this.toPage(clientX, clientY));
  }

  drop(item: CatalogItem, clientX: number, clientY: number): void {
    const p = this.toPage(clientX, clientY);
    this.clearGhost();
    try {
      this.host.apply(this.host.session().insert(item.id, p.x, p.y, this.host.snapEnabled(), this.tolerance()), true);
    } catch (e) {
      this.host.report(e);
    }
  }

  /** Last pointer position over the stage (page mm), for "insert at pointer" by keyboard. */
  lastPointerPage(): Vec2 | undefined {
    return this.lastPointer;
  }

  // ------------------------------------------------------------------ pointer handling

  private handleAt(e: PointerEvent): HandleSpec | undefined {
    const t = (e.target as Element | null)?.closest?.("[data-handle]") as HTMLElement | SVGElement | null;
    const raw = t?.dataset?.handle;
    return raw ? (JSON.parse(raw) as HandleSpec) : undefined;
  }

  private onDown(e: PointerEvent): void {
    this.el.focus({ preventScroll: true });
    const p = this.toPage(e.clientX, e.clientY);
    if (this.sketch && e.button === 0) {
      this.sketch.drawing = true;
      // Pointer times (ms) go with the trail: the engine reads the drawing speed as a detail hint (as in the COM add-in).
      this.sketchStart = performance.now();
      this.sketch.points = [[p.x, p.y, 0]];
      this.svg.setPointerCapture(e.pointerId);
      e.preventDefault();
      return;
    }
    if (this.sketch && e.button === 2) { this.cancelSketch(); return; }
    const session = this.host.session();
    const st = this.host.state();
    if (e.button === 1 || (e.button === 0 && this.spaceDown)) {
      this.drag = { kind: "pan", startClient: { x: e.clientX, y: e.clientY }, ox: this.ox, oy: this.oy };
      this.svg.setPointerCapture(e.pointerId);
      e.preventDefault();
      return;
    }
    const pending = this.host.pendingInsert();
    if (pending && e.button === 0) {
      const at = this.showGhost(pending, p);
      this.clearGhost();
      try {
        this.host.apply(session.insert(pending.id, at.x, at.y, false), true);
        this.host.finishInsert(true);
      } catch (err) {
        this.host.report(err);
      }
      return;
    }
    try {
      if (e.button === 2) {
        const hit = session.hitTest(p.x, p.y, 6 / this.scale);
        if (hit.id && !st.selection.includes(hit.id)) this.host.apply(session.select(hit.ids));
        this.host.openPie(e.clientX, e.clientY);
        return;
      }
      if (e.button !== 0) return;
      const handle = this.handleAt(e);
      if (this.beginMotion(e, p, handle)) {
        this.svg.setPointerCapture(e.pointerId);
        return;
      }
      if (handle) {
        this.startHandle(handle, p);
        this.svg.setPointerCapture(e.pointerId);
        return;
      }
      if (this.beginCurveDrag(p, e)) {
        this.svg.setPointerCapture(e.pointerId);
        return;
      }
      const hit = session.hitTest(p.x, p.y, 6 / this.scale);
      // Double-click detected here: pointer capture and the re-rendered overlay keep browsers from sending dblclick.
      const now = performance.now();
      const last = this.lastDown;
      this.lastDown = { time: now, x: e.clientX, y: e.clientY, id: hit.id };
      if (hit.id && last && last.id === hit.id && now - last.time < 450 && Math.hypot(e.clientX - last.x, e.clientY - last.y) < 6
          && (e.ctrlKey || e.metaKey) && !e.shiftKey && this.host.selectConnected) {
        // Ctrl+double-click: select connected (the most meaningful kind for the element).
        this.lastDown = undefined;
        this.host.selectConnected(hit.id);
        return;
      }
      if (hit.id && last && last.id === hit.id && now - last.time < 450 && Math.hypot(e.clientX - last.x, e.clientY - last.y) < 6
          && !(e.shiftKey || e.ctrlKey || e.metaKey)) {
        this.lastDown = undefined;
        this.editTextAt(hit.id, hit.ids);
        return;
      }
      if (hit.id) {
        const toggle = e.shiftKey || e.ctrlKey || e.metaKey;
        let selection = st.selection;
        if (toggle) {
          const inSel = hit.ids.every((id) => selection.includes(id));
          selection = inSel ? selection.filter((id) => !hit.ids.includes(id)) : [...selection, ...hit.ids];
          this.host.apply(session.select(selection));
        } else if (!selection.includes(hit.id)) {
          this.host.apply(session.select(hit.ids));
        }
        const ids = this.host.state().selection;
        if (ids.length && !this.objects(ids).some((o) => o.locked))
          this.drag = { kind: "move", ids, start: p, dx: 0, dy: 0, moved: false, startClient: { x: e.clientX, y: e.clientY } };
      } else {
        if (!e.shiftKey && st.selection.length) this.host.apply(session.select([]));
        this.drag = { kind: "marquee", start: p, cur: p, add: e.shiftKey };
      }
      this.svg.setPointerCapture(e.pointerId);
    } catch (err) {
      this.host.report(err);
    }
  }

  /**
   * Pressing on the selected curve of a chart drags the whole curve (as in the COM add-in): its `byPart` grip moves by
   * the pointer's offset and the values move along on release; a click without moving selects the part there.
   */
  private beginCurveDrag(p: Vec2, e: PointerEvent): boolean {
    if (e.shiftKey || e.ctrlKey || e.metaKey) return false;
    const sel = this.objects(this.host.state().selection);
    const obj = sel.length === 1 ? sel[0] : undefined;
    if (!obj || obj.locked) return false;
    const part = this.host.selectedPart?.(obj.id);
    const grip = bodyGrip(this.gripsOf(obj), part);
    if (!grip || this.host.session().partAt(obj.id, p.x, p.y, 6 / this.scale) !== part) return false;
    this.drag = { kind: "grip", id: obj.id, grip, startClient: { x: e.clientX, y: e.clientY }, moved: false, offset: { x: grip.x - p.x, y: grip.y - p.y }, start: p };
    return true;
  }

  private startHandle(h: HandleSpec, p: Vec2): void {
    const st = this.host.state();
    const ids = st.selection;
    const f = this.frameOf(ids);
    const objs = this.objects(ids);
    const client = this.toClient(p);
    if (h.kind === "point" && objs.length === 1) {
      // A double-click on a corner of a line with free corners removes it (as in the COM add-in).
      const now = performance.now();
      const last = this.lastPointDown;
      this.lastPointDown = { time: now, index: h.index, x: client.x, y: client.y };
      if (last && last.index === h.index && now - last.time < 450 && Math.hypot(client.x - last.x, client.y - last.y) < 6
          && canRemoveCorner(this.polyOf(objs[0])?.removable, h.index)) {
        this.lastPointDown = undefined;
        this.drag = { kind: "tap", startClient: client, run: () => this.host.apply(this.host.session().removeCorner(objs[0].id, h.index), true) };
        return;
      }
      this.drag = { kind: "point", obj: objs[0], index: h.index, pos: p, startClient: client, moved: false };
      return;
    }
    if (h.kind === "lock" && objs.length === 1) {
      const link = this.shownLinks[h.index];
      if (link) this.drag = { kind: "tap", startClient: client, run: () => this.host.apply(this.host.session().toggleLink(objs[0].id, link.key), true) };
      return;
    }
    if (h.kind === "gap" && objs.length === 1) {
      const gap = this.shownGaps[h.index];
      if (gap) this.drag = { kind: "tap", startClient: client, run: () => this.host.apply(this.host.session().applyInsertHandle(objs[0].id, gap.key), true) };
      return;
    }
    if (h.kind === "valuebadge") {
      this.drag = { kind: "tap", startClient: client, run: () => this.host.openValues?.() };
      return;
    }
    if (h.kind === "corner" && objs.length === 1) {
      const seg = this.shownSegs[h.index];
      if (!seg) return;
      // The corner starts where its dot is drawn (the layout may have slid it along its segment).
      const start = cornerAt({ x: seg.ax, y: seg.ay }, { x: seg.bx, y: seg.by }, this.segPlaced[h.index] ?? seg, { x: seg.x, y: seg.y }, 1.5 / this.scale);
      this.drag = { kind: "corner", obj: objs[0], seg, start, pos: start, startClient: client, moved: false, snapped: false };
      return;
    }
    if (h.kind === "branch" && objs.length === 1) {
      const seg = this.shownSegs[h.index];
      if (!seg) return;
      const from = { x: seg.x, y: seg.y };
      // A click adds a short stub (15 mm) on the handle's side.
      this.drag = { kind: "branch", id: objs[0].id, seg, from, end: { x: from.x + seg.nx * 15, y: from.y + seg.ny * 15 }, startClient: client, moved: false, snapped: false };
      return;
    }
    if (h.kind === "grip" && objs.length === 1) {
      const grip = this.shownGrips[h.index];
      if (grip) this.drag = { kind: "grip", id: objs[0].id, grip, startClient: this.toClient(p), moved: false };
      return;
    }
    if (h.kind === "port" && objs.length === 1) {
      const port = this.ports?.list[h.index];
      const plus = this.portPlus[h.index];
      if (port && plus) this.drag = { kind: "port", id: objs[0].id, port, plus, startClient: this.toClient(p), moved: false };
      return;
    }
    if (!f) return;
    if (h.kind === "chain" && objs.length === 1) {
      const u = h.side === "left" ? -1 : h.side === "right" ? 1 : 0;
      const v = h.side === "top" ? -1 : h.side === "bottom" ? 1 : 0;
      this.drag = { kind: "chain", id: objs[0].id, side: h.side, from: this.framePoint(f, u, v), cur: p, moved: false, centre: { x: f.cx, y: f.cy } };
      return;
    }
    if (h.kind === "rotate") {
      this.drag = { kind: "rotate", ids, frame: f, startAngle: Math.atan2(p.y - f.cy, p.x - f.cx), delta: 0 };
      return;
    }
    if (h.kind === "scale") {
      const single = objs.length === 1 && objs[0].kind === "box" ? objs[0] : undefined;
      // Centred parts scale about their centre by default (the engine marks them, see ObjectInfo.centred).
      const centred = objs.some((o) => o.centred);
      this.drag = { kind: "scale", ids, frame: f, sx: h.sx, sy: h.sy, single, centred };
    }
  }

  private onMove(e: PointerEvent): void {
    const p = this.toPage(e.clientX, e.clientY);
    if (this.sketch?.drawing) {
      const last = this.sketch.points[this.sketch.points.length - 1];
      if (Math.hypot(p.x - last[0], p.y - last[1]) * this.scale >= 2) { this.sketch.points.push([p.x, p.y, Math.round(performance.now() - this.sketchStart)]); this.drawSketchTrail(); }
      return;
    }
    this.lastPointer = p;
    this.host.pointerMoved(e.clientX, e.clientY);
    const d = this.drag;
    if (!d) {
      this.revealPointer = p;
      this.shiftHeld = e.shiftKey;
      this.updateReveal();
      const pending = this.host.pendingInsert();
      if (pending) this.showGhost(pending, p);
      return;
    }
    try {
      switch (d.kind) {
        case "motion":
          this.dragMotion(d, p, e.shiftKey, { x: e.clientX, y: e.clientY });
          return;
        case "chain": {
          d.cur = p;
          if (!d.moved && Math.hypot(p.x - d.from.x, p.y - d.from.y) * this.scale < 6) return;
          d.moved = true;
          // Preview: dashed line from the side to the pointer, where the new element will be added.
          this.guides.replaceChildren(s("line", { class: "isk-chain-preview", x1: d.from.x, y1: d.from.y, x2: p.x, y2: p.y }));
          return;
        }
        case "grip": {
          if (!d.moved && Math.hypot(e.clientX - d.startClient.x, e.clientY - d.startClient.y) < 4) return;
          d.moved = true;
          d.to = d.offset ? { x: p.x + d.offset.x, y: p.y + d.offset.y } : p;
          this.gripThrottle.push(d.id, d.grip, d.to, { x: e.clientX, y: e.clientY });
          return;
        }
        case "port": {
          if (!d.moved && Math.hypot(e.clientX - d.startClient.x, e.clientY - d.startClient.y) < 5) return;
          // Only a shaft end stretches; any other "+" is click-only.
          if (!d.port.stretch) return;
          d.moved = true;
          let q = p;
          if (this.host.snapEnabled() && !e.altKey) {
            const r = this.host.session().snapPoint(p.x, p.y, [d.id], this.tolerance());
            q = { x: r.x, y: r.y };
            this.drawGuides(r.guides);
          } else {
            this.guides.replaceChildren();
          }
          const to = stretchTarget(d.port, q);
          d.to = to;
          // Preview: the stretched shaft as the engine draws it (buildStretchPreview), plus the new end; the axis line only
          // when the engine has no drawing for it.
          let shaft: Node[] = [];
          try {
            const svg = this.host.session().buildStretchPreview(d.id, d.port.id, to.x, to.y);
            if (svg) shaft = svgChildren(svg);
          } catch {
            shaft = [];
          }
          this.ghost.replaceChildren(
            ...(shaft.length > 0
              ? shaft
              : [s("line", { class: `isk-stretch-preview${to.delta < 0 ? " is-shorter" : ""}`, x1: d.port.x, y1: d.port.y, x2: to.x, y2: to.y })]),
            s("circle", { class: "isk-stretch-end", cx: to.x, cy: to.y, r: 3 / this.scale }),
          );
          const mm = Math.round(to.delta * 10) / 10;
          this.showScaleLabel(t("build.stretch", `${mm > 0 ? "+" : ""}${mm}`), { x: e.clientX, y: e.clientY });
          return;
        }
        case "pan":
          this.ox = d.ox - (e.clientX - d.startClient.x) / this.scale;
          this.oy = d.oy - (e.clientY - d.startClient.y) / this.scale;
          this.updateViewBox();
          break;
        case "move": {
          if (!d.moved && Math.hypot(e.clientX - d.startClient.x, e.clientY - d.startClient.y) < MOVE_THRESHOLD_PX) return;
          d.moved = true;
          let dx = p.x - d.start.x, dy = p.y - d.start.y;
          // Coupled parts (gears on a dragged shaft) move along in the preview and snap too; Alt moves freely.
          const session = this.host.session();
          session.couple = !e.altKey;
          if (d.followers === undefined) d.followers = session.couple ? session.followers(d.ids) : [];
          const shown = session.couple ? [...d.ids, ...d.followers] : d.ids;
          if (!session.couple && d.followers.length) this.previewTransform(d.followers, null);
          if (this.host.snapEnabled() && !e.altKey) {
            const r = session.snapMove(d.ids, dx, dy, this.tolerance());
            dx = r.dx; dy = r.dy;
            this.drawGuides(r.guides);
            this.showSnapLabel(r.label, snapLabelAnchor(r.guides), { x: e.clientX, y: e.clientY });
          } else {
            this.guides.replaceChildren();
            this.hideSnapLabel();
          }
          session.couple = true;
          d.dx = dx; d.dy = dy;
          // Live coupling preview (meshing gears, belts, parts on a shaft) in blue, about 15 times a second; Alt: none.
          if (!e.altKey && (dx !== 0 || dy !== 0)) this.couplingThrottle.push(d.ids, { dx, dy });
          else this.clearCoupling();
          this.previewTransform(shown, `translate(${dx} ${dy})`);
          const f = this.frameOf(d.ids);
          if (f) this.drawOverlay({ ...f, cx: f.cx + dx, cy: f.cy + dy });
          break;
        }
        case "scale":
          this.dragScale(d, p, e.shiftKey, e.ctrlKey || e.metaKey, { x: e.clientX, y: e.clientY }, e.altKey);
          break;
        case "rotate": {
          const f = d.frame;
          let delta = ((Math.atan2(p.y - f.cy, p.x - f.cx) - d.startAngle) * 180) / Math.PI;
          if (!e.altKey) delta = Math.round(delta / 15) * 15;
          d.delta = delta;
          this.previewTransform(d.ids, `rotate(${delta} ${f.cx} ${f.cy})`);
          this.drawOverlay({ ...f, rot: f.rot + delta });
          break;
        }
        case "tap":
          return;
        case "corner": {
          if (!d.moved && Math.hypot(e.clientX - d.startClient.x, e.clientY - d.startClient.y) < MOVE_THRESHOLD_PX) return;
          d.moved = true;
          let q = p;
          d.snapped = this.host.snapEnabled() && !e.altKey;
          if (d.snapped) {
            const r = this.host.session().snapPoint(p.x, p.y, [d.obj.id], this.tolerance());
            q = { x: r.x, y: r.y };
            this.drawGuides(r.guides);
          } else this.guides.replaceChildren();
          d.pos = q;
          const pts = [...(d.obj.points ?? [])];
          pts.splice(d.seg.index + 1, 0, q);
          this.drawOverlay(undefined, { obj: d.obj, pts });
          return;
        }
        case "branch": {
          if (!d.moved && Math.hypot(e.clientX - d.startClient.x, e.clientY - d.startClient.y) < 5) return;
          d.moved = true;
          let q = p;
          d.snapped = this.host.snapEnabled() && !e.altKey && !e.shiftKey;
          if (e.shiftKey) {
            // Shift: 15 degree steps from where the branch leaves the line.
            const a = Math.round(Math.atan2(p.y - d.from.y, p.x - d.from.x) / (Math.PI / 12)) * (Math.PI / 12);
            const len = Math.hypot(p.x - d.from.x, p.y - d.from.y);
            q = { x: d.from.x + Math.cos(a) * len, y: d.from.y + Math.sin(a) * len };
            this.guides.replaceChildren();
          } else if (d.snapped) {
            const r = this.host.session().snapPoint(p.x, p.y, [d.id], this.tolerance());
            q = { x: r.x, y: r.y };
            this.drawGuides(r.guides);
          } else this.guides.replaceChildren();
          d.end = q;
          this.ghost.replaceChildren(
            s("line", { class: "isk-branch-preview", x1: d.from.x, y1: d.from.y, x2: q.x, y2: q.y }),
            s("circle", { class: "isk-stretch-end", cx: d.from.x, cy: d.from.y, r: 2 / this.scale }),
          );
          return;
        }
        case "point": {
          if (!d.moved && Math.hypot(e.clientX - d.startClient.x, e.clientY - d.startClient.y) < MOVE_THRESHOLD_PX) return;
          d.moved = true;
          let q = p;
          if (this.host.snapEnabled() && !e.altKey) {
            const r = this.host.session().snapPoint(p.x, p.y, [d.obj.id], this.tolerance());
            q = { x: r.x, y: r.y };
            this.drawGuides(r.guides);
          }
          d.pos = q;
          const pts = (d.obj.points ?? []).map((pt, i) => (i === d.index ? q : pt));
          this.drawOverlay(undefined, { obj: d.obj, pts });
          break;
        }
        case "marquee": {
          d.cur = p;
          this.overlay.replaceChildren();
          const x = Math.min(d.start.x, p.x), y = Math.min(d.start.y, p.y);
          const touching = p.x < d.start.x;
          this.overlay.append(s("rect", { class: `isk-marquee ${touching ? "isk-marquee-touch" : ""}`, x, y, width: Math.abs(p.x - d.start.x), height: Math.abs(p.y - d.start.y) }));
          break;
        }
      }
    } catch (err) {
      this.host.report(err);
    }
  }

  /**
   * Resize preview. Centred parts (gears, bearings, wheels: pitch circle or hub, engine flag `centred`) scale about their
   * centre by default and Ctrl switches to the opposite edge; other objects scale from the opposite edge, Ctrl about the
   * centre (same as the COM add-in). The label next to the pointer shows the size and the active mode.
   */
  private dragScale(d: Extract<Drag, { kind: "scale" }>, p: Vec2, free: boolean, ctrl: boolean, client: Vec2, alt: boolean): void {
    const f = d.frame;
    d.last = { p, client, free, alt };
    const aboutCentre = ctrl !== d.centred;
    const r = resizeFrame(f, d.sx, d.sy, p, free, aboutCentre);
    d.result = r;
    // Live coupling preview: neighbours that follow the resize (a meshing gear moving out), Alt: none.
    if (!alt && (Math.abs(r.fx - 1) > 1e-6 || Math.abs(r.fy - 1) > 1e-6)) this.couplingThrottle.push(d.ids, { sx: r.fx, sy: r.fy, pivotX: r.pivot.x, pivotY: r.pivot.y });
    else this.clearCoupling();
    // Preview: old frame → new frame in the frame's rotated axes.
    const tr = `translate(${r.cx} ${r.cy}) rotate(${f.rot}) scale(${r.fx} ${r.fy}) rotate(${-f.rot}) translate(${-f.cx} ${-f.cy})`;
    this.previewTransform(d.ids, tr);
    this.drawOverlay({ cx: r.cx, cy: r.cy, w: r.w, h: r.h, rot: f.rot });
    this.showScaleLabel(scaleLabel(r, !!d.single, aboutCentre), client);
  }

  private showScaleLabel(text: string, client: Vec2): void {
    const er = this.el.getBoundingClientRect();
    this.scaleLabelEl.textContent = text;
    this.scaleLabelEl.hidden = false;
    const w = this.scaleLabelEl.offsetWidth, h = this.scaleLabelEl.offsetHeight;
    this.scaleLabelEl.style.left = `${clamp(client.x - er.left + 14, 4, Math.max(4, er.width - w - 4))}px`;
    this.scaleLabelEl.style.top = `${clamp(client.y - er.top + 18, 4, Math.max(4, er.height - h - 4))}px`;
  }

  private hideScaleLabel(): void {
    this.scaleLabelEl.hidden = true;
    this.hideSnapLabel();
  }

  /** The snap label next to the target (page point `at`), else next to the pointer; hidden without a label. */
  private showSnapLabel(label: string | undefined, at: Vec2 | undefined, client: Vec2): void {
    if (!label) return this.hideSnapLabel();
    const er = this.el.getBoundingClientRect();
    const p = at ? this.toClient(at) : client;
    this.snapLabelEl.textContent = label;
    this.snapLabelEl.hidden = false;
    const w = this.snapLabelEl.offsetWidth, h = this.snapLabelEl.offsetHeight;
    this.snapLabelEl.style.left = `${clamp(p.x - er.left + 10, 4, Math.max(4, er.width - w - 4))}px`;
    this.snapLabelEl.style.top = `${clamp(p.y - er.top - h - 8, 4, Math.max(4, er.height - h - 4))}px`;
  }

  private hideSnapLabel(): void {
    this.snapLabelEl.hidden = true;
  }

  /** Ctrl pressed or released during a resize switches the mode without moving the pointer. */
  private onModifier(e: KeyboardEvent): void {
    const d = this.drag;
    if (d?.kind !== "scale" || !d.last || (e.key !== "Control" && e.key !== "Meta")) return;
    this.dragScale(d, d.last.p, d.last.free, e.ctrlKey || e.metaKey, d.last.client, d.last.alt);
  }

  private onUp(e: PointerEvent): void {
    const sk = this.sketch;
    if (sk?.drawing) {
      if (this.svg.hasPointerCapture(e.pointerId)) this.svg.releasePointerCapture(e.pointerId);
      const p = this.toPage(e.clientX, e.clientY);
      sk.points.push([p.x, p.y, Math.round(performance.now() - this.sketchStart)]);
      const pts = sk.points;
      this.cancelSketch();
      if (pts.length >= 3) sk.done(pts);
      return;
    }
    const d = this.drag;
    this.drag = undefined;
    // The handles near the pointer stay shown after the edit.
    this.revealPointer = this.toPage(e.clientX, e.clientY);
    // Alt edits freely: no mechanical coupling (gears in mesh, gears on shafts) for this edit.
    this.host.session().couple = !e.altKey;
    if (this.svg.hasPointerCapture(e.pointerId)) this.svg.releasePointerCapture(e.pointerId);
    this.guides.replaceChildren();
    this.hideScaleLabel();
    this.clearCoupling();
    if (!d) return;
    const session = this.host.session();
    try {
      switch (d.kind) {
        case "grip": {
          // Release: one undo step with the value the element snaps to; a click selects the grip's part.
          this.gripThrottle.cancel();
          this.ghost.replaceChildren();
          if (d.moved && d.to) this.host.apply(session.moveHandle(d.id, d.grip.key, d.to.x, d.to.y), true);
          else if (d.grip.byPart && d.start) this.host.clickPart?.(d.id, d.start, 6 / this.scale);
          else if (d.grip.part) this.host.selectPart?.(d.id, d.grip.part);
          else this.drawOverlay();
          break;
        }
        case "motion": {
          // Release applies the motion once (one undo step); a click without a drag changes nothing.
          this.clearMotionPreview(d);
          if (d.moved && d.turn && Math.abs(d.degrees) > 1e-6) this.host.apply(session.motionTurn(d.id, d.degrees), true);
          else if (d.moved && !d.turn) this.host.apply(session.motionDrag(d.id, d.index, d.target.x, d.target.y), true);
          else this.drawOverlay();
          break;
        }
        case "port": {
          this.ghost.replaceChildren();
          if (!d.moved) { this.host.openPortInsert?.(d.id, d.port, this.toClient(d.plus)); break; }
          // Stretching a shaft: the parts on it keep their place (no coupling for this edit, like the COM add-in).
          if (d.to && Math.abs(d.to.delta) > 1e-6) {
            session.couple = false;
            this.host.apply(session.buildStretch(d.id, d.port.id, d.to.x, d.to.y), true);
          } else this.drawOverlay();
          break;
        }
        case "move":
          if (d.moved && (Math.abs(d.dx) > 1e-9 || Math.abs(d.dy) > 1e-9)) this.host.apply(session.move(d.ids, d.dx, d.dy), true);
          else {
            this.resetPreview([...d.ids, ...(d.followers ?? [])]);
            if (!d.moved && d.ids.length === 1) this.host.clickPart?.(d.ids[0], d.start, 6 / this.scale);
          }
          break;
        case "scale": {
          const r = d.result;
          if (!r) { this.resetPreview(d.ids); break; }
          if (d.single) this.host.apply(session.resize(d.single.id, r.w, r.h, r.cx, r.cy), true);
          else this.host.apply(session.scale(d.ids, r.pivot.x, r.pivot.y, r.fx, r.fy), true);
          break;
        }
        case "rotate":
          if (Math.abs(d.delta) > 1e-6) this.host.apply(session.rotate(d.ids, d.delta, d.frame.cx, d.frame.cy), true);
          else this.resetPreview(d.ids);
          break;
        case "point": {
          if (!d.moved) { this.drawOverlay(); break; }
          const snap = this.host.snapEnabled() && !e.altKey;
          const n = d.obj.points?.length ?? 0;
          // An end dropped on a free end of a line of the same kind joins both lines (as in the COM add-in).
          if (n >= 2 && (d.index === 0 || d.index === n - 1)) {
            const j = session.joinLines(d.obj.id, d.index, d.pos.x, d.pos.y, snap, this.tolerance());
            if (j.joined && j.state) { this.host.apply(j.state, true); break; }
          }
          this.host.apply(session.setPoint(d.obj.id, d.index, d.pos.x, d.pos.y, snap, this.tolerance(), this.host.anchorEnabled()), true);
          break;
        }
        case "tap": {
          if (Math.hypot(e.clientX - d.startClient.x, e.clientY - d.startClient.y) < 6) d.run();
          else this.drawOverlay();
          break;
        }
        case "corner": {
          // Click: a corner where the dot is drawn; drag: bent to the pointer (snapped, docked). One undo step.
          const to = d.moved ? d.pos : d.start;
          this.host.apply(session.insertCorner(d.obj.id, d.seg.index, d.start, to.x, to.y, d.moved && d.snapped, this.tolerance(), this.host.anchorEnabled()), true);
          break;
        }
        case "branch": {
          this.ghost.replaceChildren();
          this.host.apply(session.branch(d.id, d.from, d.end, d.moved && d.snapped, this.tolerance(), this.host.anchorEnabled()), true);
          break;
        }
        case "marquee": {
          const w = d.cur.x - d.start.x, h = d.cur.y - d.start.y;
          if (Math.abs(w) * this.scale > 2 || Math.abs(h) * this.scale > 2)
            this.host.apply(session.selectRect(d.start.x, d.start.y, w, h, w < 0 ? "touching" : "enclosed", d.add));
          else this.drawOverlay();
          break;
        }
        case "pan":
          break;
        case "chain": {
          if (!d.moved) { this.host.quickAdd?.(d.id, d.side); break; }
          const dx = d.cur.x - d.centre.x, dy = d.cur.y - d.centre.y;
          const side: ChainSide = Math.abs(dx) >= Math.abs(dy) ? (dx < 0 ? "left" : "right") : (dy < 0 ? "top" : "bottom");
          this.host.quickAdd?.(d.id, side, d.cur);
          break;
        }
      }
    } catch (err) {
      this.host.report(err);
      this.render();
    } finally {
      this.host.session().couple = true;
    }
  }

  private resetPreview(ids: string[]): void {
    this.previewTransform(ids, null);
    this.drawOverlay();
  }

  cancelDrag(): void {
    const d = this.drag;
    this.drag = undefined;
    this.guides.replaceChildren();
    this.hideScaleLabel();
    this.clearCoupling();
    if (d?.kind === "port" || d?.kind === "grip" || d?.kind === "branch") this.ghost.replaceChildren();
    if (d?.kind === "grip") this.gripThrottle.cancel();
    if (d?.kind === "motion") this.clearMotionPreview(d);
    if (d && "ids" in d) this.resetPreview(d.ids);
    else this.drawOverlay();
  }

  get dragging(): boolean {
    return !!this.drag;
  }

  private onWheel(e: WheelEvent): void {
    e.preventDefault();
    if (e.ctrlKey || e.metaKey) this.zoomBy(Math.pow(1.0015, -e.deltaY), this.toPage(e.clientX, e.clientY));
    else {
      this.ox += (e.shiftKey ? e.deltaY : e.deltaX) / this.scale;
      this.oy += (e.shiftKey ? 0 : e.deltaY) / this.scale;
      this.updateViewBox();
    }
  }

  /** Double-click on an object edits its main text (the first click selected it; the app decides what is editable). */
  private editTextAt(id: string, ids: string[]): void {
    if (!this.host.state().selection.includes(id)) this.host.apply(this.host.session().select(ids));
    const o = this.host.state().objects.find((x) => x.id === id);
    if (o) this.onEditText?.(o);
  }
}
