// Typed, synchronous client for the C# EngineApi. The backend is the [JSExport] wrapper of the WebAssembly engine
// (wasm.ts); tests can pass any object with the same Call signature.

import type {
  ActionInfo, Catalog, DrawCapabilities, DrawPlan, EditorState, HitResult, InspectorInfo, ParamValue, SnapResult, StyleInfo,
  SuggestionInfo, ExporterInfo, ExportResult, TransformEffect, ArrangeOption, TraceInfo, BuildPortInfo, CouplingPreview,
  MotionInfo, MotionPreview, PreflightResult, PartView, SanitizeMode, SanitizeOptions, SanitizeResult,
  CalcResult, CodeImportResult, ConnectOption, ConvertUnitResult, DragHandleInfo, ExplainResult, ObjectHandles, ValueLevel, ValuesResult,
  CalcSheetMode, CalcSheetResult, InsertHandleInfo, LinkInfo, PolylineHandles, Vec2,
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

  /** The offices the compatibility check (preflight) knows (`{ id, name }`, the first is the default). */
  patentProfiles(lang: string): { id: string; name: string }[] {
    return this.call("patentProfiles", { lang });
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
  /** ISO 128 line group of the drawing (thick line width in mm: 0.25, 0.35, 0.5, 0.7 or 1). */
  setLineGroup(value: number): EditorState { return this.c("setLineGroup", { value }); }
  /** Accent colour of the drawing: preset id, #rrggbb, or "" for each module's own colours. */
  setAccent(text: string): EditorState { return this.c("setAccent", { text }); }
  /** A CSV/TSV table as the data of the selected chart (or a new chart). */
  importData(text: string): EditorState { return this.c("importData", { text }); }
  /** GeoJSON or GPX as map routes and markers fitted into the page. */
  importGeo(text: string): EditorState { return this.c("importGeo", { text }); }
  /** SVG or DXF drawing as editable, grouped basic shapes (real size if it has units and fits, else fitted). */
  importVector(text: string): EditorState { return this.c("importVector", { text }); }
  /** Compatibility check (drawing-rule preflight) of the page for office `profile` – a drafting aid, not a legal certification. */
  preflight(profile: string, lang: string): PreflightResult { return this.c("preflight", { text: profile, lang }); }
  /**
   * "Clean slides": makes the values of `ids` unrecognisable (none = every element of the page; groups expanded) –
   * charts normalised, randomised or kept (`mode`), optionally categories renamed, texts replaced and metadata stripped.
   * One undo step; the report names the seed used (pass it again to reproduce a randomised result).
   */
  sanitize(ids: string[], mode: SanitizeMode, value: SanitizeOptions, lang: string): SanitizeResult {
    return this.c("sanitize", { ids, mode, value, lang });
  }
  /** Sets the main text (ObjectInfo.mainText) of one object; one undo step. */
  setMainText(id: string, text: string): EditorState { return this.c("setMainText", { id, text }); }
  /** Library items that fit the first of `ids` ("Insert ▸"), at most `max`. */
  suggestions(ids: string[], lang: string, max?: number): SuggestionInfo[] { return this.c("suggestions", { ids, lang, max }); }
  /** "+" quick add: a connected copy of `id` on `side`, or at x/y when dropped; the new element is selected. */
  quickAdd(id: string, side: "left" | "top" | "right" | "bottom", x?: number, y?: number): EditorState { return this.c("quickAdd", { id, side, x, y }); }
  /** Objects that move rigidly with `ids` (gears on a dragged shaft); previewed with the drag. */
  followers(ids: string[]): string[] { return this.c("followers", { ids }); }
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
  /** Parts of element `id` (charts v2): curves, axes, grid … with label and hidden flag. */
  parts(id: string, lang: string): PartView[] { return this.c("parts", { id, lang }); }
  /** Part of element `id` at the page point (mm) within `tolerance`, or null. */
  partAt(id: string, x: number, y: number, tolerance: number): string | null { return this.c<{ part: string | null }>("partAt", { id, x, y, tolerance }).part; }
  /** Actions of one part: the element's own ones that concern it, then show/hide, thicker, thinner, line style, reset. */
  partActions(id: string, part: string, lang: string): ActionInfo[] { return this.c("partActions", { id, part, lang }); }
  /** Runs a part action (one undo step). */
  applyPartAction(id: string, part: string, action: string, input?: string): EditorState { return this.c("applyPartAction", { id, part, action, input }); }
  /** A hand-drawn trail (page mm) into chart `id`: recognised or smoothed, added as a series; one undo step. */
  sketchCurve(id: string, points: number[][], lang: string): { state: EditorState; message: string; kind?: string } { return this.c("sketchCurve", { id, points, lang }); }
  /** A picture (RGBA bytes as base64, w × h) as a chart at (x, y) mm, selected; one undo step. */
  chartFromPicture(w: number, h: number, payload: string, lang: string, x?: number, y?: number): { state: EditorState; message: string; id?: string } {
    return this.c("chartFromPicture", { w, h, payload, lang, x, y });
  }
  /** Outline of a part in page mm (polylines of [x, y]), for highlighting it. */
  partOutline(id: string, part: string): number[][][] { return this.c("partOutline", { id, part }); }
  inspector(ids: string[], lang: string): InspectorInfo { return this.c("inspector", { ids, lang }); }
  /** Page or selection as SVG; mode "export" adds named groups per element and part (editable in Inkscape/Illustrator). */
  renderSvg(options: { ids?: string[]; monochrome?: boolean; background?: string; padding?: number; mode?: "export" } = {}): { svg: string; width: number; height: number } {
    return this.c("renderPageSvg", options);
  }
  drawPlan(ids: string[] | undefined, capabilities: DrawCapabilities): DrawPlan { return this.c("drawPlan", { ids, capabilities }); }
  importCompact(payload: string): EditorState { return this.c("importCompact", { payload }); }
  hostTransform(ids: string[], from: { x: number; y: number; w: number; h: number }, target: { x: number; y: number; w: number; h: number }, rotation: number): EditorState {
    return this.c("hostTransform", { ids, from, target, rotation });
  }
  save(): string { return this.c<{ json: string }>("saveDocument").json; }
  /** Insert by typing: library hits for `text`; parts that fit `ids` first (`fits`), `recent` ranked higher. */
  search(ids: string[], text: string, lang: string, recent: string[], max?: number): SuggestionInfo[] {
    return this.c("search", { ids, text, lang, recent, max });
  }
  /** Which of mirror horizontally/vertically and rotate by 90° change the drawing of `ids`. */
  transformEffect(ids: string[]): TransformEffect { return this.c("transformEffect", { ids }); }
  /** Selection-wide operations that change something for `ids` (one view for all, align, distribute, match, renumber). */
  arrangeOptions(ids: string[], lang: string): ArrangeOption[] { return this.c("arrangeOptions", { ids, lang }); }
  /** Runs a selection-wide operation from arrangeOptions; one undo step. */
  arrange(ids: string[], action: string): EditorState { return this.c("arrange", { ids, action }); }
  /** `ids` plus every object coupled with them (gear train, drive), selection first. */
  assembly(ids: string[]): string[] { return this.c("assembly", { ids }); }
  /** Path of point `index` (default: last) of link `id` while its chain's crank turns (coupler curve). */
  trace(id: string, index?: number): TraceInfo { return this.c("trace", { id, index }); }
  /** Inserts `itemId` where it fits object `id` (gear on the shaft …); `placed` false when it fits nowhere. */
  insertFitting(id: string, itemId: string): { placed: boolean; state: EditorState } { return this.c("insertFitting", { id, itemId }); }
  /** Inserts the coupler curve of link `id` as a motion path; one undo step. */
  drawTrace(id: string, index?: number): EditorState { return this.c("drawTrace", { id, index }); }
  /** Explodes assembly `id` into its parts; one undo step. */
  explode(id: string): EditorState { return this.c("explode", { id }); }
  /** Degree of freedom of the self-built linkage of `id` and whether `id` can be exploded. */
  dof(id: string): { links: number | null; joints: number | null; dof: number | null; explodable: boolean } { return this.c("dof", { id }); }
  /** Ghost positions of the self-built linkage of link `id`: dashed grey copies over the crank's range, grouped; one undo step. */
  drawGhosts(id: string, max?: number): EditorState { return this.c("drawGhosts", { id, max }); }
  /** Live coupling preview of a move (dx/dy) or scale (sx/sy about pivot): neighbours that would follow, as SVG in page mm. */
  couplingPreview(ids: string[], change: { dx: number; dy: number } | { sx: number; sy: number; pivotX: number; pivotY: number }): CouplingPreview {
    return this.c("couplingPreview", { ids, ...change });
  }
  /** Build "+" ports of object `id` (page mm, outward direction; `stretch` for a shaft end that can be dragged). */
  buildPorts(id: string): BuildPortInfo[] { return this.c("buildPorts", { id }); }
  /** Library items that dock at port `key` of object `id`, best first. */
  buildItems(id: string, key: string, lang: string, max?: number): SuggestionInfo[] { return this.c("buildItems", { id, key, lang, max }); }
  /** Docks library item `itemId` at port `key` of object `id`, coupled and selected; one undo step. */
  buildPlace(id: string, key: string, itemId: string): EditorState { return this.c("buildPlace", { id, key, itemId }); }
  /** Stretches the shaft end at port `key` of object `id` to x/y; one undo step. */
  buildStretch(id: string, key: string, x: number, y: number): EditorState { return this.c("buildStretch", { id, key, x, y }); }
  /** SVG (page mm) of `id` with the shaft end at `key` stretched to x/y; nothing changes. */
  buildStretchPreview(id: string, key: string, x: number, y: number): string { return this.c<{ svg: string }>("buildStretchPreview", { id, key, x, y }).svg; }
  /** Motion mode: whether `id` turns as part of a gear train (centre, coupled), drags as a link of a chain, or neither. */
  motionInfo(id: string): MotionInfo { return this.c("motionInfo", { id }); }
  /** Motion mode preview: gear `id` turned by `degrees`, or joint `index` of link `id` dragged to x/y; nothing changes. */
  motionPreview(id: string, change: { degrees: number } | { index: number; x: number; y: number }): MotionPreview {
    return this.c("motionPreview", { id, ...change });
  }
  /** Motion mode: turns gear `id` by `degrees` (clockwise) with its whole train; one undo step. */
  motionTurn(id: string, degrees: number): EditorState { return this.c("motionTurn", { id, degrees }); }
  /** Motion mode: drags joint `index` of link `id` to x/y, the chain follows with constant lengths; one undo step. */
  motionDrag(id: string, index: number, x: number, y: number): EditorState { return this.c("motionDrag", { id, index, x, y }); }
  /** Select connected: `ids` plus what is connected by `mode` (auto, net, coupled, chain, loop, walls), selection first. */
  connected(ids: string[], mode: string): string[] { return this.c("connected", { ids, mode }); }
  /** The kinds of "select connected" that add something for the first of `ids`, each with its ids. */
  connectOptions(ids: string[], lang: string): ConnectOption[] { return this.c("connectOptions", { ids, lang }); }
  /** Value level of the document (quick sketch, explore, engineer); one undo step when it changes. */
  setValueLevel(level: ValueLevel): EditorState { return this.c("setValueLevel", { text: level }); }
  /** The values of element `id` (status, variables with provenance and formula, missing sets, conflicts). */
  values(id: string, lang: string, digits?: number): ValuesResult { return this.c("values", { id, lang, digits }); }
  /** Stores an entered value as typed ("4k7", "230 V"); empty text clears; one undo step. A wrong unit throws. */
  setValue(id: string, key: string, text: string): EditorState { return this.c("setValue", { id, key, text }); }
  /** Removes an entered value; one undo step. */
  clearValue(id: string, key: string): EditorState { return this.c("clearValue", { id, key }); }
  /** Step-by-step formula lines of value `key` of element `id`. */
  explain(id: string, key: string, lang: string, digits?: number): ExplainResult { return this.c("explain", { id, key, lang, digits }); }
  /** The unit-aware calculator ("230 V * 16 A", "1500 rpm in rad/s"). */
  calc(text: string, lang: string, digits?: number): CalcResult { return this.c("calc", { text, lang, digits }); }
  /** Converts a value with a unit ("12 mm") into `unit`. */
  convertUnit(text: string, unit: string, lang: string): ConvertUnitResult { return this.c("convertUnit", { text, unit, lang }); }
  /** matplotlib, pgfplots or TikZ code as editable elements (read, never executed); one undo step, the new ones selected. */
  importCode(text: string, lang: string, mode = "auto"): CodeImportResult { return this.c("importCode", { text, lang, mode }); }
  /** Grips on values of `ids` (chart limit lines, value lines, markers, points), page mm. */
  dragHandles(ids: string[], lang: string): ObjectHandles[] { return this.c("dragHandles", { ids, lang }); }
  /** Preview of grip `key` of element `id` dragged to x/y: the snapped grip with its label, document unchanged. */
  moveHandlePreview(id: string, key: string, x: number, y: number, lang: string): DragHandleInfo | null {
    return this.c<{ handle: DragHandleInfo | null }>("moveHandle", { id, key, x, y, lang, preview: true }).handle;
  }
  /** Drags grip `key` of element `id` to x/y (constrained and snapped by the element); one undo step. */
  moveHandle(id: string, key: string, x: number, y: number): EditorState { return this.c("moveHandle", { id, key, x, y }); }
  /** Values of element `id` as if value `key` were `value` (SI), without changing the document (the value card's ↔ grip). */
  dragValuePreview(id: string, key: string, value: number, lang: string, digits?: number): ValuesResult {
    return this.c("dragValue", { id, key, value, lang, digits, preview: true });
  }
  /** Stores value `key` of element `id` as `value` (SI, formatted by the engine); one undo step. */
  dragValue(id: string, key: string, value: number, digits?: number): EditorState { return this.c("dragValue", { id, key, value, digits }); }
  /** The calculation sheet of `ids` (none: the page) as CSV, Markdown, LaTeX or table cells. */
  calcSheet(ids: string[] | undefined, mode: CalcSheetMode, lang: string, digits?: number): CalcSheetResult { return this.c("calcSheet", { ids, mode, lang, digits }); }
  /** The calculation sheet of `ids` as an annotation table right of the elements; one undo step, the table selected. */
  insertCalcTable(ids: string[] | undefined, lang: string, digits?: number): EditorState { return this.c("insertCalcTable", { ids, lang, digits }); }
  /** Where element `id` touches others and follows them (lock badges). */
  links(id: string, lang: string): LinkInfo[] { return this.c("links", { id, lang }); }
  /** Releases or couples the link `key` of element `id`; one undo step. */
  toggleLink(id: string, key: string): EditorState { return this.c("toggleLink", { id, key }); }
  /** Corner dots and branch handles of line `id`. */
  polylineHandles(id: string): PolylineHandles { return this.c("polylineHandles", { id }); }
  /** A new corner on segment `index` of line `id`, started at the dot `from` and moved to x/y (snapped, docked); one undo step. */
  insertCorner(id: string, index: number, from: Vec2, x: number, y: number, snap: boolean, tolerance?: number, anchor = true): EditorState {
    return this.c("insertCorner", { id, index, points: [[from.x, from.y]], x, y, snap, tolerance, anchor });
  }
  /** Removes corner `index` of line `id`; one undo step. */
  removeCorner(id: string, index: number): EditorState { return this.c("removeCorner", { id, index }); }
  /** Splits line `id` at corner `index` (opens an outline there); one undo step. */
  splitLine(id: string, index: number): EditorState { return this.c("splitLine", { id, index }); }
  /** A branch of line `id` from `from` (on the line) to `to` (snapped, docked); one undo step, the branch selected. */
  branch(id: string, from: Vec2, to: Vec2, snap: boolean, tolerance?: number, anchor = true): EditorState {
    return this.c("branch", { id, points: [[from.x, from.y], [to.x, to.y]], snap, tolerance, anchor });
  }
  /** End `index` of line `id` dropped at x/y: joins a free end of a line of the same kind there; nothing changes without one. */
  joinLines(id: string, index: number, x: number, y: number, snap: boolean, tolerance?: number): { joined: boolean; state: EditorState | null } {
    return this.c("joinLines", { id, index, x, y, snap, tolerance });
  }
  /** Insert places of element `id` (between table rows and columns). */
  insertHandles(id: string, lang: string): InsertHandleInfo[] {
    return this.c<{ id: string; handles: InsertHandleInfo[] }[]>("insertHandles", { id, lang }).find((x) => x.id === id)?.handles ?? [];
  }
  /** Inserts at place `key` of element `id` (a row, a column); one undo step. */
  applyInsertHandle(id: string, key: string): EditorState { return this.c("applyInsertHandle", { id, key }); }
  /** Replaces one object with a full document object (as in saveDocument); one undo step. */
  update(object: unknown): EditorState { return this.c("update", { object }); }
}
