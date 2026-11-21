// Typed, synchronous client for the C# EngineApi. The backend is the [JSExport] wrapper of the WebAssembly engine
// (wasm.ts); tests can pass any object with the same Call signature.

import type {
  ActionInfo, Catalog, DrawCapabilities, DrawPlan, EditorState, HitResult, InspectorInfo, ParamValue, SnapResult, StyleInfo,
  SuggestionInfo, ExporterInfo, ExportResult,
} from "./types";

export interface EngineBackend {
  /** Returns {"ok":true,"result":…} or {"ok":false,"error":"…"}. */
  call(method: string, requestJson: string): string;
}

export class EngineError extends Error {}

export class Engine {
  constructor(private readonly backend: EngineBackend) {}

  call<T>(method: string, args: object = {}): T {
    const raw = this.backend.call(method, JSON.stringify(args));
    const env = JSON.parse(raw) as { ok: boolean; result?: T; error?: string };
    if (!env.ok) throw new EngineError(`${method}: ${env.error ?? "unknown error"}`);
    return env.result as T;
  }

  version(): { engine: string; modules: { id: string; name: string; version: string; types: number }[]; methods: string[] } {
    return this.call("version");
  }

  createSession(lang: string, pageWidth?: number, pageHeight?: number): EditorState {
    return this.call("createSession", { lang, pageWidth, pageHeight });
  }

  catalog(lang: string): Catalog {
    return this.call("catalog", { lang });
  }
}

/** Editing commands bound to one session; every mutation returns the new editor state (with page SVG). */
export class Session {
  constructor(readonly engine: Engine, readonly id: string) {}

  /** Mechanical coupling for edits (gears in mesh, gears on shafts); the stage clears it while Alt is held. */
  couple = true;

  private c<T>(method: string, args: object = {}): T {
    return this.engine.call<T>(method, { session: this.id, couple: this.couple, ...args });
  }

  state(includeSvg = true): EditorState { return this.c("state", { includeSvg }); }
  setPage(pageWidth: number, pageHeight: number, lang?: string): EditorState { return this.c("setPage", { pageWidth, pageHeight, lang }); }
  insert(itemId: string, x: number, y: number, snap = true, tolerance?: number): EditorState { return this.c("insert", { itemId, x, y, snap, tolerance }); }
  move(ids: string[], dx: number, dy: number): EditorState { return this.c("move", { ids, dx, dy }); }
  scale(ids: string[], pivotX: number, pivotY: number, sx: number, sy: number): EditorState { return this.c("scale", { ids, pivotX, pivotY, sx, sy }); }
  resize(id: string, w: number, h: number, x: number, y: number): EditorState { return this.c("resize", { id, w, h, x, y }); }
  rotate(ids: string[], degrees: number, pivotX?: number, pivotY?: number): EditorState { return this.c("rotate", { ids, degrees, pivotX, pivotY }); }
  flip(ids: string[], horizontal: boolean): EditorState { return this.c("flip", { ids, horizontal }); }
  duplicate(ids: string[]): EditorState { return this.c("duplicate", { ids }); }
  remove(ids: string[]): EditorState { return this.c("delete", { ids }); }
  setView(ids: string[], view: string): EditorState { return this.c("setView", { ids, view }); }
  setParam(ids: string[], key: string, value: ParamValue): EditorState { return this.c("setParam", { ids, key, value }); }
  setStyle(ids: string[], style: StyleInfo, replace = false): EditorState { return this.c("setStyle", { ids, style, replace }); }
  setText(ids: string[], text: string): EditorState { return this.c("setText", { ids, text }); }
  /** Moves point `index`; with `snap` it snaps and (unless `anchor` is false) attaches to the target. */
  setPoint(id: string, index: number, x: number, y: number, snap: boolean, tolerance?: number, anchor = true): EditorState {
    return this.c("setPoint", { id, index, x, y, snap, tolerance, anchor });
  }
  /** Runs a context action; actions whose info carries `input` need the entered text. */
  applyAction(ids: string[], action: string, input?: string): EditorState { return this.c("applyAction", { ids, action, input }); }
  /** Sets the document plan scale denominator (100 = 1:100); null clears it. One undo step. */
  setPlanScale(value: number | null): EditorState { return this.c("setPlanScale", { value }); }
  /** Switches the document's black-and-white setting; one undo step, every object counts as changed. */
  setMonochrome(value: boolean): EditorState { return this.c("setMonochrome", { value }); }
  /** Sets the main text (ObjectInfo.mainText) of one object; one undo step. */
  setMainText(id: string, text: string): EditorState { return this.c("setMainText", { id, text }); }
  /** Library items that fit the first of `ids` ("Insert ▸"), at most `max`. */
  suggestions(ids: string[], lang: string, max?: number): SuggestionInfo[] { return this.c("suggestions", { ids, lang, max }); }
  /** "+" quick add: a connected copy of `id` on `side`, or at x/y when dropped; the new element is selected. */
  quickAdd(id: string, side: "left" | "top" | "right" | "bottom", x?: number, y?: number): EditorState { return this.c("quickAdd", { id, side, x, y }); }
  /** Library items offered as the next element after a quick add. */
  nextItems(id: string, lang: string, max?: number): SuggestionInfo[] { return this.c("nextItems", { id, lang, max }); }
  /** Turns `id` into library item `itemId`; its connectors stay attached. */
  replaceItem(id: string, itemId: string): EditorState { return this.c("replaceItem", { id, itemId }); }
  /** "Type ▸": line types the selected line/arrow/dimension/lead line can become (empty for other objects). */
  conversions(ids: string[], lang: string): ActionInfo[] { return this.c("conversions", { ids, lang }); }
  /** Domain exports that apply to the selection (all objects of the page without a selection). */
  exporters(ids: string[], lang: string): ExporterInfo[] { return this.c("exporters", { ids, lang }); }
  exportData(ids: string[], exporter: string, lang: string): ExportResult { return this.c("export", { ids, itemId: exporter, lang }); }
  /** Converts the selected line-like objects into another line type (end points, numeral and colour kept). */
  convert(ids: string[], typeId: string): EditorState { return this.c("convert", { ids, itemId: typeId }); }
  undo(): EditorState { return this.c("undo"); }
  redo(): EditorState { return this.c("redo"); }
  select(ids: string[]): EditorState { return this.c("select", { ids, includeSvg: false }); }
  selectRect(x: number, y: number, w: number, h: number, mode: "enclosed" | "touching", add: boolean): EditorState {
    return this.c("selectRect", { x, y, w, h, mode, add, includeSvg: false });
  }
  hitTest(x: number, y: number, tolerance: number): HitResult { return this.c("hitTest", { x, y, tolerance }); }
  snapMove(ids: string[], dx: number, dy: number, tolerance: number): SnapResult { return this.c("snapMove", { ids, dx, dy, tolerance }); }
  snapPoint(x: number, y: number, exclude: string[], tolerance: number): SnapResult { return this.c("snapPoint", { x, y, exclude, tolerance }); }
  snapInsert(itemId: string, x: number, y: number, tolerance: number): SnapResult { return this.c("snapInsert", { itemId, x, y, tolerance }); }
  setSnap(options: { tolerance?: number; grid?: number | null; objects?: boolean; semantic?: boolean; alignment?: boolean; page?: boolean }): unknown {
    return this.c("setSnap", { snapOptions: options });
  }
  contextActions(ids: string[], lang: string): ActionInfo[] { return this.c("contextActions", { ids, lang }); }
  inspector(ids: string[], lang: string): InspectorInfo { return this.c("inspector", { ids, lang }); }
  renderSvg(options: { ids?: string[]; monochrome?: boolean; background?: string; padding?: number } = {}): { svg: string; width: number; height: number } {
    return this.c("renderPageSvg", options);
  }
  drawPlan(ids: string[] | undefined, capabilities: DrawCapabilities): DrawPlan { return this.c("drawPlan", { ids, capabilities }); }
  importCompact(payload: string): EditorState { return this.c("importCompact", { payload }); }
  hostTransform(ids: string[], from: { x: number; y: number; w: number; h: number }, target: { x: number; y: number; w: number; h: number }, rotation: number): EditorState {
    return this.c("hostTransform", { ids, from, target, rotation });
  }
  save(): string { return this.c<{ json: string }>("saveDocument").json; }
}
