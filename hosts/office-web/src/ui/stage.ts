// Stage: SVG view of the current page with selection frame, handles, live snapping guides, marquee, zoom/pan and
// insert-at-pointer. All geometry decisions (snapping, hit-testing, edits) come from the engine; the stage only
// previews drags with SVG transforms and commits the result through the session.

import type { Session } from "../engine/client";
import type { CatalogItem, EditorState, ObjectInfo, SnapGuide, Vec2 } from "../engine/types";
import { clamp, s, svgChildren, svgDataUrl, viewBoxOf } from "./dom";

export interface StageHost {
  state(): EditorState;
  session(): Session;
  /** Adopts an edit result (live = may be synced to the slide). */
  apply(state: EditorState, live?: boolean): void;
  openPie(clientX: number, clientY: number): void;
  pendingInsert(): CatalogItem | undefined;
  finishInsert(placed: boolean): void;
  snapEnabled(): boolean;
  pointerMoved(clientX: number, clientY: number): void;
  report(error: unknown): void;
}

interface Frame { cx: number; cy: number; w: number; h: number; rot: number }

type HandleSpec = { kind: "scale"; sx: number; sy: number } | { kind: "rotate" } | { kind: "point"; index: number };

type Drag =
  | { kind: "move"; ids: string[]; start: Vec2; dx: number; dy: number; moved: boolean; startClient: Vec2 }
  | { kind: "scale"; ids: string[]; frame: Frame; sx: number; sy: number; single?: ObjectInfo; result?: { w: number; h: number; cx: number; cy: number; fx: number; fy: number; pivot: Vec2 } }
  | { kind: "rotate"; ids: string[]; frame: Frame; startAngle: number; delta: number }
  | { kind: "point"; obj: ObjectInfo; index: number; pos: Vec2 }
  | { kind: "marquee"; start: Vec2; cur: Vec2; add: boolean }
  | { kind: "pan"; startClient: Vec2; ox: number; oy: number };

const HANDLE_PX = 9;
const ROTATE_PX = 26;
const MOVE_THRESHOLD_PX = 3;
const SNAP_PX = 8;

const rad = (d: number) => (d * Math.PI) / 180;

function rotateVec(v: Vec2, deg: number): Vec2 {
  const c = Math.cos(rad(deg)), si = Math.sin(rad(deg));
  return { x: v.x * c - v.y * si, y: v.x * si + v.y * c };
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
    this.svg.append(defs, this.pageRect, this.gridRect, this.content, this.guides, this.overlay, this.ghost);
    this.el.append(this.svg);

    new ResizeObserver(() => this.resize()).observe(this.el);
    this.svg.addEventListener("pointerdown", (e) => this.onDown(e));
    this.svg.addEventListener("pointermove", (e) => this.onMove(e));
    this.svg.addEventListener("pointerup", (e) => this.onUp(e));
    this.svg.addEventListener("pointercancel", () => this.cancelDrag());
    this.svg.addEventListener("pointerleave", () => { if (!this.drag) this.clearGhost(); });
    this.svg.addEventListener("contextmenu", (e) => e.preventDefault());
    this.svg.addEventListener("wheel", (e) => this.onWheel(e), { passive: false });
    this.svg.addEventListener("dblclick", (e) => this.onDoubleClick(e));
    window.addEventListener("keydown", (e) => { if (e.code === "Space" && document.activeElement === this.el) { this.spaceDown = true; e.preventDefault(); } });
    window.addEventListener("keyup", (e) => { if (e.code === "Space") this.spaceDown = false; });
  }

  /** Text edit request on double click (objects with text); set by the app. */
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
  selectionScreenBox(): { left: number; top: number; right: number; bottom: number } | undefined {
    const f = this.frameOf(this.host.state().selection);
    if (!f) return undefined;
    const pts = [[-1, -1], [1, -1], [1, 1], [-1, 1]].map(([u, v]) => this.toClient(this.framePoint(f, u, v)));
    return { left: Math.min(...pts.map((p) => p.x)), top: Math.min(...pts.map((p) => p.y)), right: Math.max(...pts.map((p) => p.x)), bottom: Math.max(...pts.map((p) => p.y)) };
  }

  drawOverlay(previewFrame?: Frame, previewPoints?: { obj: ObjectInfo; pts: Vec2[] }): void {
    this.overlay.replaceChildren();
    const st = this.host.state();
    const sel = st.selection;
    if (sel.length === 0 && !previewFrame) return;
    const px = 1 / this.scale;
    const objs = this.objects(sel);
    // Thin outline per selected object (assembly members, multi selection).
    if (objs.length > 1)
      for (const o of objs) this.overlay.append(s("rect", { class: "isk-sel-member", x: o.bounds.x, y: o.bounds.y, width: o.bounds.w, height: o.bounds.h }));
    const single = objs.length === 1 ? objs[0] : undefined;
    if (single?.kind === "points" && !previewFrame) {
      const pts = previewPoints?.obj.id === single.id ? previewPoints.pts : single.points ?? [];
      this.overlay.append(s("polyline", { class: "isk-sel-path", points: pts.map((p) => `${p.x},${p.y}`).join(" ") }));
      pts.forEach((p, i) => {
        const c = s("circle", { class: "isk-handle isk-handle-point", cx: p.x, cy: p.y, r: (HANDLE_PX / 2 + 1) * px });
        c.dataset.handle = JSON.stringify({ kind: "point", index: i } satisfies HandleSpec);
        this.overlay.append(c);
      });
      return;
    }
    const f = previewFrame ?? this.frameOf(sel);
    if (!f) return;
    const corners = [[-1, -1], [1, -1], [1, 1], [-1, 1]].map(([u, v]) => this.framePoint(f, u, v));
    this.overlay.append(s("polygon", { class: "isk-sel-frame", points: corners.map((p) => `${p.x},${p.y}`).join(" ") }));
    if (objs.some((o) => o.locked)) return;
    const top = this.framePoint(f, 0, -1);
    const up = rotateVec({ x: 0, y: -ROTATE_PX * px }, f.rot);
    const rh = { x: top.x + up.x, y: top.y + up.y };
    this.overlay.append(s("line", { class: "isk-sel-stem", x1: top.x, y1: top.y, x2: rh.x, y2: rh.y }));
    const rot = s("circle", { class: "isk-handle isk-handle-rotate", cx: rh.x, cy: rh.y, r: (HANDLE_PX / 2 + 1) * px });
    rot.dataset.handle = JSON.stringify({ kind: "rotate" } satisfies HandleSpec);
    this.overlay.append(rot);
    const size = HANDLE_PX * px;
    for (const [u, v] of [[-1, -1], [0, -1], [1, -1], [1, 0], [1, 1], [0, 1], [-1, 1], [-1, 0]]) {
      const p = this.framePoint(f, u, v);
      const corner = u !== 0 && v !== 0;
      const r = s("rect", {
        class: `isk-handle ${corner ? "isk-handle-corner" : "isk-handle-side"}`,
        x: p.x - size / 2, y: p.y - size / 2, width: size, height: size,
        transform: f.rot ? `rotate(${f.rot} ${p.x} ${p.y})` : undefined,
      });
      r.dataset.handle = JSON.stringify({ kind: "scale", sx: u, sy: v } satisfies HandleSpec);
      this.overlay.append(r);
    }
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
      if (handle) {
        this.startHandle(handle, p);
        this.svg.setPointerCapture(e.pointerId);
        return;
      }
      const hit = session.hitTest(p.x, p.y, 6 / this.scale);
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
    if (!f) return;
    if (h.kind === "rotate") {
      this.drag = { kind: "rotate", ids, frame: f, startAngle: Math.atan2(p.y - f.cy, p.x - f.cx), delta: 0 };
      return;
    }
    if (h.kind === "scale") {
      const single = objs.length === 1 && objs[0].kind === "box" ? objs[0] : undefined;
      this.drag = { kind: "scale", ids, frame: f, sx: h.sx, sy: h.sy, single };
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
        case "pan":
          this.ox = d.ox - (e.clientX - d.startClient.x) / this.scale;
          this.oy = d.oy - (e.clientY - d.startClient.y) / this.scale;
          this.updateViewBox();
          break;
        case "move": {
          if (!d.moved && Math.hypot(e.clientX - d.startClient.x, e.clientY - d.startClient.y) < MOVE_THRESHOLD_PX) return;
          d.moved = true;
          let dx = p.x - d.start.x, dy = p.y - d.start.y;
          if (this.host.snapEnabled() && !e.altKey) {
            const r = this.host.session().snapMove(d.ids, dx, dy, this.tolerance());
            dx = r.dx; dy = r.dy;
            this.drawGuides(r.guides);
          } else this.guides.replaceChildren();
          d.dx = dx; d.dy = dy;
          this.previewTransform(d.ids, `translate(${dx} ${dy})`);
          const f = this.frameOf(d.ids);
          if (f) this.drawOverlay({ ...f, cx: f.cx + dx, cy: f.cy + dy });
          break;
        }
        case "scale":
          this.dragScale(d, p, e.shiftKey);
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

  private dragScale(d: Extract<Drag, { kind: "scale" }>, p: Vec2, free: boolean): void {
    const f = d.frame;
    // Pointer in the frame's local (unrotated) coordinates, origin at the frame centre.
    const local = rotateVec({ x: p.x - f.cx, y: p.y - f.cy }, -f.rot);
    const ax = (-d.sx * f.w) / 2, ay = (-d.sy * f.h) / 2; // fixed opposite handle
    let w = f.w, h = f.h;
    const corner = d.sx !== 0 && d.sy !== 0;
    if (corner && !free) {
      const dir = { x: d.sx * f.w, y: d.sy * f.h };
      const k = Math.max(0.02, ((local.x - ax) * dir.x + (local.y - ay) * dir.y) / (dir.x * dir.x + dir.y * dir.y));
      w = f.w * k; h = f.h * k;
    } else {
      if (d.sx !== 0) w = Math.max(0.5, (local.x - ax) * d.sx);
      if (d.sy !== 0) h = Math.max(0.5, (local.y - ay) * d.sy);
    }
    const cl = { x: d.sx !== 0 ? ax + (d.sx * w) / 2 : 0, y: d.sy !== 0 ? ay + (d.sy * h) / 2 : 0 };
    const c = rotateVec(cl, f.rot);
    const cx = f.cx + c.x, cy = f.cy + c.y;
    const fx = w / Math.max(1e-6, f.w), fy = h / Math.max(1e-6, f.h);
    const pivotLocal = rotateVec({ x: ax, y: ay }, f.rot);
    d.result = { w, h, cx, cy, fx, fy, pivot: { x: f.cx + pivotLocal.x, y: f.cy + pivotLocal.y } };
    // Preview: old frame → new frame in the frame's rotated axes.
    const t = `translate(${cx} ${cy}) rotate(${f.rot}) scale(${fx} ${fy}) rotate(${-f.rot}) translate(${-f.cx} ${-f.cy})`;
    this.previewTransform(d.ids, t);
    this.drawOverlay({ cx, cy, w, h, rot: f.rot });
  }

  private onUp(e: PointerEvent): void {
    const d = this.drag;
    this.drag = undefined;
    if (this.svg.hasPointerCapture(e.pointerId)) this.svg.releasePointerCapture(e.pointerId);
    this.guides.replaceChildren();
    if (!d) return;
    const session = this.host.session();
    try {
      switch (d.kind) {
        case "move":
          if (d.moved && (Math.abs(d.dx) > 1e-9 || Math.abs(d.dy) > 1e-9)) this.host.apply(session.move(d.ids, d.dx, d.dy), true);
          else this.resetPreview(d.ids);
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
          this.host.apply(session.setPoint(d.obj.id, d.index, d.pos.x, d.pos.y, this.host.snapEnabled() && !e.altKey, this.tolerance()), true);
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
      }
    } catch (err) {
      this.host.report(err);
      this.render();
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

  private onDoubleClick(e: MouseEvent): void {
    const p = this.toPage(e.clientX, e.clientY);
    try {
      const hit = this.host.session().hitTest(p.x, p.y, 6 / this.scale);
      const o = hit.id ? this.host.state().objects.find((x) => x.id === hit.id) : undefined;
      if (o?.hasText) this.onEditText?.(o);
    } catch (err) {
      this.host.report(err);
    }
  }
}
