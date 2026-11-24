// Stage: SVG view of the current page with selection frame, handles, live snapping guides, marquee, zoom/pan and
// insert-at-pointer. All geometry decisions (snapping, hit-testing, edits) come from the engine; the stage only
// previews drags with SVG transforms and commits the result through the session.

import type { Session } from "../engine/client";
import type { BuildPortInfo, CatalogItem, EditorState, MotionInfo, ObjectInfo, SnapGuide, Vec2 } from "../engine/types";
import { t } from "../i18n";
import { COUPLING_PREVIEW_MS, PORT_RADIUS_PX, Throttle, portPlusPosition, portTipKey, stretchTarget } from "./buildlogic";
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
  report(error: unknown): void;
}

interface Frame { cx: number; cy: number; w: number; h: number; rot: number }

export type ChainSide = "left" | "top" | "right" | "bottom";

type HandleSpec = { kind: "scale"; sx: number; sy: number } | { kind: "rotate" } | { kind: "point"; index: number } | { kind: "chain"; side: ChainSide } | { kind: "port"; index: number };

/** Change shown by the motion preview (engine `motionPreview`): a gear turn or a joint dragged to a point. */
type MotionChange = { degrees: number } | { index: number; x: number; y: number };

/** Change shown by the live coupling preview (engine `couplingPreview`): a move or a scale about a pivot. */
type CouplingChange = { dx: number; dy: number } | { sx: number; sy: number; pivotX: number; pivotY: number };

type Drag =
  | { kind: "move"; ids: string[]; start: Vec2; dx: number; dy: number; moved: boolean; startClient: Vec2; followers?: string[] }
  | { kind: "scale"; ids: string[]; frame: Frame; sx: number; sy: number; single?: ObjectInfo; centred: boolean; last?: { p: Vec2; client: Vec2; free: boolean; alt: boolean }; result?: ResizeResult }
  | { kind: "rotate"; ids: string[]; frame: Frame; startAngle: number; delta: number }
  | { kind: "point"; obj: ObjectInfo; index: number; pos: Vec2 }
  | { kind: "marquee"; start: Vec2; cur: Vec2; add: boolean }
  | { kind: "pan"; startClient: Vec2; ox: number; oy: number }
  | { kind: "chain"; id: string; side: ChainSide; from: Vec2; cur: Vec2; moved: boolean; centre: Vec2 }
  | { kind: "port"; id: string; port: BuildPortInfo; plus: Vec2; startClient: Vec2; moved: boolean; to?: { x: number; y: number; delta: number } }
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
  private readonly content: SVGGElement;
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
    this.svg.append(defs, this.pageRect, this.gridRect, this.content, this.coupling, this.guides, this.overlay, this.ghost);
    this.scaleLabelEl = document.createElement("div");
    this.scaleLabelEl.className = "isk-scale-label";
    this.scaleLabelEl.hidden = true;
    this.snapLabelEl = document.createElement("div");
    this.snapLabelEl.className = "isk-scale-label isk-snap-label";
    this.snapLabelEl.hidden = true;
    this.el.append(this.svg, this.scaleLabelEl, this.snapLabelEl);
    window.addEventListener("keydown", (e) => this.onModifier(e));
    window.addEventListener("keyup", (e) => this.onModifier(e));

    new ResizeObserver(() => this.resize()).observe(this.el);
    this.svg.addEventListener("pointerdown", (e) => this.onDown(e));
    this.svg.addEventListener("pointermove", (e) => this.onMove(e));
    this.svg.addEventListener("pointerup", (e) => this.onUp(e));
    this.svg.addEventListener("pointercancel", () => this.cancelDrag());
    this.svg.addEventListener("pointerleave", () => { if (!this.drag) this.clearGhost(); });
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

  drawOverlay(previewFrame?: Frame, previewPoints?: { obj: ObjectInfo; pts: Vec2[] }): void {
    this.overlay.replaceChildren();
    this.portPlus = [];
    const st = this.host.state();
    const sel = st.selection;
    if (sel.length === 0 && !previewFrame) return;
    const px = 1 / this.scale;
    const objs = this.objects(sel);
    // Thin outline per selected object (assembly members, multi selection).
    if (objs.length > 1)
      for (const o of objs) this.overlay.append(s("rect", { class: "isk-sel-member", x: o.bounds.x, y: o.bounds.y, width: o.bounds.w, height: o.bounds.h }));
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
      if (!previewPoints && !single.locked && !motion) this.drawPorts(single, pts, { x: single.bounds.x + single.bounds.w, y: single.bounds.y });
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
    // Quick add: blue triangles outside each side of a chainable element (click: connected copy; drag: drop it there).
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
      }
    }
    const handles: Vec2[] = [rh];
    for (const [u, v] of [[-1, -1], [0, -1], [1, -1], [1, 0], [1, 1], [0, 1], [-1, 1], [-1, 0]]) {
      const p = this.framePoint(f, u, v);
      handles.push(p);
      const corner = u !== 0 && v !== 0;
      const r = s("rect", {
        class: `isk-handle ${corner ? "isk-handle-corner" : "isk-handle-side"}`,
        x: p.x - size / 2, y: p.y - size / 2, width: size, height: size,
        transform: f.rot ? `rotate(${f.rot} ${p.x} ${p.y})` : undefined,
      });
      r.dataset.handle = JSON.stringify({ kind: "scale", sx: u, sy: v } satisfies HandleSpec);
      this.overlay.append(r);
    }
    if (single && !previewFrame) this.drawPorts(single, handles, { x: Math.max(...corners.map((c) => c.x)), y: Math.min(...corners.map((c) => c.y)) });
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

  /**
   * Blue "+" circles at the free build ports of the selected element (like the COM add-in): about 15 px beyond the port
   * along its outward direction with a thin dashed lead line, pushed clear of the frame handles; a free hub's "+" sits
   * beyond the top right corner. Click: insert box with what docks there; drag at a shaft end: stretch the shaft.
   */
  private drawPorts(obj: ObjectInfo, obstacles: Vec2[], topRight: Vec2): void {
    const list = this.portsOf(obj);
    const px = 1 / this.scale;
    this.portPlus = [];
    list.forEach((port, i) => {
      const c = portPlusPosition(port, this.scale, obstacles, topRight);
      this.portPlus.push(c);
      this.overlay.append(s("line", { class: "isk-port-lead", x1: port.x, y1: port.y, x2: c.x, y2: c.y }));
      const r = PORT_RADIUS_PX * px, k = r * 0.55;
      const g = s("g", { class: `isk-port${port.stretch ? " isk-port-stretch" : ""}` });
      g.dataset.handle = JSON.stringify({ kind: "port", index: i } satisfies HandleSpec);
      g.dataset.port = port.id;
      const title = s("title");
      title.textContent = t(portTipKey(port));
      g.append(title, s("circle", { cx: c.x, cy: c.y, r }), s("path", { d: `M${c.x - k} ${c.y}H${c.x + k}M${c.x} ${c.y - k}V${c.y + k}` }));
      this.overlay.append(g);
    });
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
      const hit = session.hitTest(p.x, p.y, 6 / this.scale);
      // Double-click detected here: pointer capture and the re-rendered overlay keep browsers from sending dblclick.
      const now = performance.now();
      const last = this.lastDown;
      this.lastDown = { time: now, x: e.clientX, y: e.clientY, id: hit.id };
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

  private startHandle(h: HandleSpec, p: Vec2): void {
    const st = this.host.state();
    const ids = st.selection;
    const f = this.frameOf(ids);
    const objs = this.objects(ids);
    if (h.kind === "point" && objs.length === 1) {
      this.drag = { kind: "point", obj: objs[0], index: h.index, pos: p };
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
    this.lastPointer = p;
    this.host.pointerMoved(e.clientX, e.clientY);
    const d = this.drag;
    if (!d) {
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
        case "point": {
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
    const d = this.drag;
    this.drag = undefined;
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
          else this.resetPreview([...d.ids, ...(d.followers ?? [])]);
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
        case "point":
          this.host.apply(session.setPoint(d.obj.id, d.index, d.pos.x, d.pos.y, this.host.snapEnabled() && !e.altKey, this.tolerance(), this.host.anchorEnabled()), true);
          break;
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
    if (d?.kind === "port") this.ghost.replaceChildren();
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
