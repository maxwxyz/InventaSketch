// Task-pane application: top bar, library, stage, inspector, floating context bar and pie menu around one engine
// session. Runs inside PowerPoint (Office.js present) and standalone in a browser (dev.html).

import { Engine, Session } from "../engine/client";
import type {
  ActionInfo, ArrangeOption, BuildPortInfo, Catalog, CatalogItem, ConnectOption, DashStyle, EditorState, ExporterInfo, InspectorInfo, ObjectInfo, PreflightResult,
  SuggestionInfo, TraceInfo, TransformEffect, ValueLevel,
} from "../engine/types";
import { download, editorExportScope, exportFileStem, exportPng, exportScopeLabel, exportSvg, resolveExportSource, type ExportSource } from "../export";
import { lang, resolveLang, setLang, t, type Lang } from "../i18n";
import { drawCapabilities, noOffice, type HostCapabilities } from "../office/capabilities";
import { SlideSync } from "../office/sync";
import { checkSlides, cleanSlide, closeSession, compareWithPrevious, currentSlide, goToFinding, readCurrentSlideUnits, readSlideUnits, slideSession, type SlideFinding, type SlideUnits } from "../office/slides";
import { alsoOn, continuationMap, designationOf, sameMap, slideDesignations } from "../office/designations";
import { ComSync, type ComSyncBridge } from "../office/combridge";
import { loadSettings, saveSettings, type Settings } from "../settings";
import { applyTheme, resolveTheme } from "../theme";
import { ContextBar, InlinePrompt, type BarItem } from "./contextbar";
import {
  ELEMENT_CHOICE, PART_CHOICE, actionEntries, barActions, choicePick, choicePieLabel, currentOption, groupActionChoices, isPictorial, isStep, isTelling,
  pickBar, shortLabel, type ChoiceEntry,
} from "./barlogic";
import { ValueCard, visibleVars } from "./valuecard";
import { CalcSheet, Calculator, CodeImport, isCodeName } from "./tools";
import { h, svgDataUrl } from "./dom";
import { hasIcon, icon } from "./icons";
import { canDrawGhosts } from "./buildlogic";
import { badgeKind, cornerCommands } from "./handlelogic";
import { addReport, cleanRequest, cleanScopes, defaultCleanForm, emptyTotals, formatCleanReport, type CleanForm, type CleanRequest, type CleanTotals } from "./cleanlogic";
import { canMove } from "./motionlogic";
import { InsertBox, insertRows, rememberRecent } from "./insertbox";
import { Inspector } from "./inspector";
import { Library } from "./library";
import { PieMenu } from "./piemenu";
import { addMenu, entryOf, labelEntries, pieRing, withBack, type LabelChange, type PieEntry, type PieGroup } from "./pielogic";
import { Stage, type ChainSide } from "./stage";
import { isPictureName, pictureToRgba } from "./picture";
import { PART_PREFIX, addedPart, isPartCommand, keepPart, partActionId, partAfterDelete, partDeleteAction, partListEntries, type SelectedPart } from "./partlogic";
import { assemblyExtends, canTrace, elementCount, filterTransforms, findingCounts, multiBarGroups, importKind, orderFindings, sourceLinks } from "./uxlogic";

export interface AppOptions {
  engine: Engine;
  root: HTMLElement;
  office: HostCapabilities;
  officeLanguage?: string;
  officeBackground?: string;
  /** Slide size in mm (Office, PowerPointApi 1.10). */
  pageSize?: { w: number; h: number };
  loadMillis?: number;
  /** Running inside the COM add-in's WebView2 task pane: slide work goes through the COM bridge. */
  comBridge?: ComSyncBridge;
}

/** Line styles of the bar with their pictograms (IconIds.ForDash). */
const DASHES: [DashStyle, string][] = [
  ["solid", "line-solid"], ["dash", "line-dash"], ["dot", "line-dot"], ["dashDot", "line-dashdot"], ["longDash", "line-longdash"], ["dashDotDot", "line-dashdotdot"],
];

/** ISO 128 line groups offered (width of the thick line in mm), as the engine's Style.LineGroups. */
export const LINE_GROUPS = [0.25, 0.35, 0.5, 0.7, 1];

/** What the bar and the pie of a selection are built from (engine actions, choices, inspector), fetched once per use. */
interface ElementModel {
  sel: string[];
  /** The single selected element (undefined for several). */
  obj?: ObjectInfo;
  known: boolean;
  /** Context actions with mirror/rotate left out where they change nothing. */
  actions: ActionInfo[];
  /** Actions that are no choice option and set no parameter shown as a choice. */
  plain: ActionInfo[];
  /** View, line type, parameter choices and line style. */
  paramChoices: ChoiceEntry[];
  /** The element's own choices among actions ("@a.<choice>"), menus included. */
  actionChoices: ChoiceEntry[];
  covered: Set<string>;
  info?: InspectorInfo;
  conversions: ActionInfo[];
}

/** A bar candidate: its id (to drop duplicates), whether it is the main action, and the button. */
interface BarCandidate { id: string; main?: boolean; item: BarItem }

export class App {
  readonly engine: Engine;
  private readonly root: HTMLElement;
  private readonly settings: Settings;
  private readonly office: HostCapabilities;
  private sessionObj!: Session;
  private st!: EditorState;
  private catalog: Catalog = { lang: "en", categories: [], items: [] };
  private readonly stage: Stage;
  private readonly library: Library;
  private readonly inspector: Inspector;
  private readonly pie: PieMenu;
  private readonly bar: ContextBar;
  private readonly prompt: InlinePrompt;
  private readonly insertBox: InsertBox;
  private readonly valueCard: ValueCard;
  private readonly calculator: Calculator;
  private readonly calcSheet: CalcSheet;
  private readonly codeImport: CodeImport;
  private readonly sync?: SlideSync | ComSync;
  private pending?: CatalogItem;
  private dragging?: CatalogItem;
  private toastTimer?: number;
  private readonly topbar: HTMLElement;
  private readonly status: HTMLElement;
  private readonly toastEl: HTMLElement;
  private settingsEl?: HTMLElement;
  private readonly prefersDark = typeof matchMedia === "function" ? matchMedia("(prefers-color-scheme: dark)") : undefined;

  constructor(private readonly opts: AppOptions) {
    this.engine = opts.engine;
    this.root = opts.root;
    this.office = opts.office ?? noOffice;
    this.settings = loadSettings();
    this.applyLanguage();
    this.applyTheme();
    this.prefersDark?.addEventListener?.("change", () => this.applyTheme());

    const first = this.engine.createSession(lang(), opts.pageSize?.w, opts.pageSize?.h);
    this.sessionObj = new Session(this.engine, first.session);
    this.st = first;
    this.configureSnap();

    this.stage = new Stage({
      state: () => this.st,
      session: () => this.sessionObj,
      apply: (s, live) => this.apply(s, live),
      openPie: (x, y) => this.openPie(x, y),
      pendingInsert: () => this.pending ?? this.dragging,
      finishInsert: () => this.cancelInsert(),
      snapEnabled: () => this.settings.snap,
      anchorEnabled: () => this.settings.anchor,
      quickAdd: (id, side, at) => this.quickAdd(id, side, at),
      openPortInsert: (id, port, at) => this.openPortInsert(id, port, at),
      pointerMoved: (x, y) => this.bar.pointerMoved(x, y),
      motionEnabled: () => this.motion,
      hideBar: () => this.bar.hide(),
      clickPart: (id, p, tolerance) => this.selectPartAt(id, p, tolerance),
      selectConnected: (id) => this.selectConnected([id], "auto"),
      selectedPart: (id) => (this.part?.id === id ? this.part.key : undefined),
      selectPart: (id, key) => this.guard(() => {
        const info = this.sessionObj.parts(id, lang()).find((p) => p.key === key);
        if (info) this.setPart({ id, key, label: info.label });
      }),
      valueAlert: (id) => this.valueAlert(id),
      cornerChanged: () => this.updateBar(),
      openValues: () => this.openValues(),
      report: (e) => this.report(e),
    });
    this.stage.onEditText = (o) => this.editText(o);
    this.library = new Library({
      rank: (query) => this.sessionObj.search([], query, lang(), this.settings.recent ?? [], 400).map((x) => x.id),
      favourites: () => this.settings.favourites,
      toggleFavourite: (id) => {
        const f = new Set(this.settings.favourites);
        if (f.has(id)) f.delete(id); else f.add(id);
        this.settings.favourites = [...f];
        saveSettings(this.settings);
      },
      startInsert: (item) => { this.remember(item.id); this.startInsert(item); },
      insertNow: (item) => { this.remember(item.id); this.insertAt(item, this.stage.viewCentre()); },
      dragStart: (item) => (this.dragging = item),
      pendingId: () => this.pending?.id,
    });
    // A picture pasted while the stage has the focus becomes a chart (charts v2).
    window.addEventListener("paste", (e) => { if (document.activeElement === this.stage.el) this.pastePicture(e); });
    this.inspector = new Inspector({ session: () => this.sessionObj, state: () => this.st, apply: (s, live) => this.apply(s, live), report: (e) => this.report(e) });
    this.pie = new PieMenu(this.root, (e) => this.pickPie(e), (e) => this.hoverPie(e), () => {
      this.stage.setHaloPreview([]);
      queueMicrotask(() => { if (!this.pie.open) this.updateBar(); });
    });
    this.prompt = new InlinePrompt(this.root);
    const tools = { session: () => this.sessionObj, apply: (s: EditorState, live?: boolean) => this.apply(s, live), report: (e: unknown) => this.report(e) };
    this.calculator = new Calculator(this.root, tools);
    this.calcSheet = new CalcSheet(this.root, tools);
    this.codeImport = new CodeImport(this.root, tools);
    this.valueCard = new ValueCard(this.root, {
      ...tools,
      state: () => this.st,
      setLevel: (level) => this.setValueLevel(level),
      openCalculator: (text) => this.calculator.show(this.pointerClient(), text),
      showsValues: (id) => this.showsValues(id),
      setShowValues: (id, on) => this.edit((s) => s.setParam([id], "showValues", on)),
      openCalcSheet: (id) => this.calcSheet.show([id], this.pointerClient()),
      closed: () => queueMicrotask(() => this.updateBar()),
    });
    this.insertBox = new InsertBox(this.root);
    this.status =h("div", { class: "isk-status", "aria-live": "polite" });
    this.toastEl = h("div", { class: "isk-toast", role: "status", hidden: true });
    this.topbar = h("header", { class: "isk-topbar" });

    if (opts.comBridge) {
      this.sync = new ComSync({
        session: () => this.sessionObj,
        adopt: (s) => { this.apply(s, false); this.toast(t("office.loaded")); },
        report: (e) => this.report(e),
        hideModules: (ids) => this.hideModules(ids),
      }, opts.comBridge);
    } else if (this.office.office && this.office.shapes) {
      this.sync = new SlideSync({
        session: () => this.sessionObj,
        host: this.office,
        draw: drawCapabilities(this.office),
        adopt: (s, reason) => { this.apply(s, false); this.toast(reason === "native-edit" ? t("office.adoptedEdit") : t("office.loaded")); },
        report: (e) => this.report(e),
      });
    }

    this.layout();
    this.bar = new ContextBar(this.root, (a) => this.runAction(a), (x, y) => this.openPie(x, y),
      (id, x, y) => this.barCommand(id, x, y), (key, value) => this.pickChoice(key, value));
    this.loadCatalog();
    this.bindKeys();
    this.bindDrop();
    new ResizeObserver(() => this.updateLayoutMode()).observe(this.root);
    this.updateLayoutMode();
    this.apply(first);
    this.setStatus(opts.loadMillis !== undefined ? t("status.ready", Math.round(opts.loadMillis)) : "");
    if (this.slideAccess) void this.refreshContinuations();
  }

  // ------------------------------------------------------------------ layout

  private stageWrap!: HTMLElement;
  private libPanel!: HTMLElement;
  private inspPanel!: HTMLElement;

  private layout(): void {
    this.root.replaceChildren();
    this.root.classList.add("isk-app");
    const libToggle = this.panelToggle("library", () => this.settings.libraryOpen, (v) => (this.settings.libraryOpen = v));
    const inspToggle = this.panelToggle("inspector", () => this.settings.inspectorOpen, (v) => (this.settings.inspectorOpen = v));
    this.libPanel = h("aside", { class: "isk-panel isk-lib-panel" }, libToggle, this.library.el);
    this.inspPanel = h("aside", { class: "isk-panel isk-insp-panel" }, inspToggle, this.inspector.el);
    const zoom = h("div", { class: "isk-zoom" },
      this.iconButton("zoomOut", t("zoom.out"), () => this.stage.zoomBy(1 / 1.25)),
      this.iconButton("fit", t("zoom.fit"), () => this.stage.fit()),
      this.iconButton("zoomIn", t("zoom.in"), () => this.stage.zoomBy(1.25)));
    this.stageWrap = h("div", { class: "isk-stage-wrap" }, this.stage.el, zoom, this.status);
    this.root.append(this.topbar, h("div", { class: "isk-main" }, this.libPanel, this.stageWrap, this.inspPanel), this.toastEl);
    this.renderTopbar();
    this.syncPanels();
  }

  private panelToggle(which: "library" | "inspector", get: () => boolean, set: (v: boolean) => void): HTMLButtonElement {
    const b = h("button", { class: "isk-panel-head", type: "button" });
    const paint = () => {
      b.setAttribute("aria-expanded", String(get()));
      b.innerHTML = `${icon(which)}<span>${t(which === "library" ? "lib.title" : "insp.title")}</span><span class="isk-chev" aria-hidden="true">${get() ? "▾" : "▸"}</span>`;
    };
    b.addEventListener("click", () => { set(!get()); saveSettings(this.settings); paint(); this.syncPanels(); });
    paint();
    return b;
  }

  private syncPanels(): void {
    this.libPanel.classList.toggle("is-collapsed", !this.settings.libraryOpen);
    this.inspPanel.classList.toggle("is-collapsed", !this.settings.inspectorOpen);
  }

  private updateLayoutMode(): void {
    const w = this.root.getBoundingClientRect().width;
    this.root.dataset.layout = w >= 720 ? "wide" : "narrow";
  }

  private iconButton(name: string, label: string, onClick: (e: MouseEvent) => void, extra: Record<string, string | boolean | undefined> = {}): HTMLButtonElement {
    const b = h("button", { class: "isk-btn isk-icon-btn", type: "button", title: label, "aria-label": label, html: icon(name), ...extra });
    b.addEventListener("click", onClick);
    return b;
  }

  private renderTopbar(): void {
    const st = this.st;
    const scope = st ? exportScopeLabel(editorExportScope(st, this.slideAccess), t) : "";
    const items: (HTMLElement | false)[] = [
      this.iconButton("undo", t("cmd.undo") + " (Ctrl+Z)", () => this.edit((s) => s.undo(), true), { disabled: !st?.canUndo, "data-cmd": "undo" }),
      this.iconButton("redo", t("cmd.redo") + " (Ctrl+Y)", () => this.edit((s) => s.redo(), true), { disabled: !st?.canRedo, "data-cmd": "redo" }),
      h("span", { class: "isk-sep" }),
    ];
    if (this.sync) {
      const insert = h("button", { class: "isk-btn isk-primary", type: "button", title: t("office.insertHint"), "data-cmd": "insert", html: `${icon("insert")}<span>${t("office.insert")}</span>` });
      insert.disabled = !st?.objects.length;
      insert.addEventListener("click", () => this.insertIntoSlide());
      const live = h("button", { class: `isk-btn isk-icon-btn ${this.settings.liveSync ? "is-active" : ""}`, type: "button", title: t("office.live"), "aria-label": t("office.live"), "aria-pressed": String(this.settings.liveSync), html: icon("live") });
      live.addEventListener("click", () => { this.settings.liveSync = !this.settings.liveSync; saveSettings(this.settings); this.renderTopbar(); });
      items.push(insert, live, this.office.selection && this.iconButton("load", t("office.loadSelection"), () => this.sync?.loadSelection()), h("span", { class: "isk-sep" }));
    }
    items.push(
      this.iconButton("mono", t("cmd.mono"), () => this.setMonochrome(!st?.monochrome), {
        class: `isk-btn isk-icon-btn${st?.monochrome ? " is-active" : ""}`, "aria-pressed": String(!!st?.monochrome), "data-cmd": "mono",
      }),
      this.importButton(),
      this.iconButton("cmd-code", t("cmd.importCode"), (e) => this.codeImport.show(this.below(e.currentTarget as HTMLElement)), { "aria-haspopup": "dialog", "data-cmd": "importcode" }),
      this.textButton(t("cmd.calcShort"), t("cmd.calc"), (e) => this.calculator.show(this.below(e.currentTarget as HTMLElement)), { "aria-haspopup": "dialog", "data-cmd": "calculator" }),
      this.lineGroupSelect(st?.lineGroup ?? 0.5),
      this.accentSelect(st?.accent ?? null),
      this.slideAccess
        && this.iconButton("compare", t("cmd.compare"), () => void this.compareSlides(), { "data-cmd": "compare" }),
      this.iconButton("check", t("cmd.patentCheck"), (e) => this.togglePatentCheck(e.currentTarget as HTMLElement), { "aria-haspopup": "dialog", "data-cmd": "patentcheck" }),
      this.iconButton("clean", t("cmd.clean"), (e) => this.toggleCleanSlides(e.currentTarget as HTMLElement), { "aria-haspopup": "dialog", "data-cmd": "clean" }),
      // What the export covers, stated before the buttons (COM: "Selection: n elements" / "Slide").
      h("span", { class: "isk-export-scope", "data-cmd": "exportscope", title: t("export.scopeHint") }, scope),
      this.iconButton("svg", `${scope}: ${t("cmd.exportSvg")}`, () => void this.exportWith((x) => exportSvg(x)), { "data-cmd": "exportsvg" }),
      this.iconButton("png", `${scope}: ${t("cmd.exportPng")}`, () => void this.exportWith((x) => exportPng(x)), { "data-cmd": "exportpng" }),
      h("span", { class: "isk-grow" }),
      this.iconButton("settings", t("cmd.settings"), (e) => this.toggleSettings(e.currentTarget as HTMLElement), { "aria-haspopup": "dialog" }),
    );
    this.topbar.replaceChildren(...items.filter((x): x is HTMLElement => !!x));
  }

  /** A top bar button with a short text label. */
  private textButton(text: string, label: string, onClick: (e: MouseEvent) => void, extra: Record<string, string | boolean | undefined> = {}): HTMLButtonElement {
    const b = h("button", { class: "isk-btn", type: "button", title: label, "aria-label": label, ...extra }, text);
    b.addEventListener("click", onClick);
    return b;
  }

  /** The client point under the middle of a button (popovers open there). */
  private below(el: HTMLElement): { x: number; y: number } {
    const r = el.getBoundingClientRect();
    return { x: r.left + r.width / 2, y: r.bottom };
  }

  /** Black and white is a document setting (one undo step); live sync redraws every unit on the slide. */
  private setMonochrome(value: boolean): void {
    this.edit((s) => s.setMonochrome(value));
  }

  /** "Import": a CSV/TSV table into the selected chart (or a new one), GeoJSON/GPX as map routes and markers, SVG/DXF as shapes. */
  private importButton(): HTMLElement {
    const input = h("input", { type: "file", accept: ".csv,.tsv,.txt,.geojson,.json,.gpx,.svg,.dxf,.py,.tex,.tikz,.pgf,.png,.jpg,.jpeg,.bmp,.gif,.webp", hidden: true });
    input.addEventListener("change", async () => {
      const f = input.files?.[0];
      input.value = "";
      if (!f) return;
      if (isPictureName(f.name)) return this.chartFromPicture(f);
      const text = await f.text();
      // matplotlib, pgfplots and TikZ code: the code import with its report (the code is read, never run).
      if (isCodeName(f.name)) return this.codeImport.show(this.below(button), text);
      const kind = importKind(f.name, text);
      this.edit((s) => (kind === "vector" ? s.importVector(text) : kind === "geo" ? s.importGeo(text) : s.importData(text)));
    });
    const button = this.iconButton("load", t("cmd.import"), () => input.click(), { "data-cmd": "import" });
    return h("span", {}, button, input);
  }

  /** A picture of a chart (file or pasted) as an editable chart in the middle of the page (charts v2). */
  private async chartFromPicture(blob: Blob): Promise<void> {
    try {
      const { w, h, payload } = await pictureToRgba(blob);
      const r = this.sessionObj.chartFromPicture(w, h, payload, lang());
      this.apply(r.state, true);
      this.setStatus(r.message);
    } catch (e) {
      this.report(e);
    }
  }

  /** Ctrl+V with a picture on the clipboard (stage focused): the picture becomes a chart. */
  private pastePicture(e: ClipboardEvent): void {
    const item = Array.from(e.clipboardData?.items ?? []).find((i) => i.kind === "file" && i.type.startsWith("image/"));
    const file = item?.getAsFile();
    if (!file) return;
    e.preventDefault();
    void this.chartFromPicture(file);
  }

  /** ISO 128 line group of the drawing (a document setting, one undo step; live sync redraws every unit on the slide). */
  private lineGroupSelect(current: number): HTMLElement {
    const sel = h("select", { class: "isk-input isk-linegroup", title: t("cmd.lineGroup"), "aria-label": t("cmd.lineGroup"), "data-cmd": "linegroup" });
    for (const g of LINE_GROUPS) {
      const mm = (lang() === "de" ? String(g).replace(".", ",") : String(g)) + " mm";
      sel.append(h("option", { value: String(g), selected: Math.abs(g - current) < 1e-9 }, mm));
    }
    sel.addEventListener("change", () => this.edit((s) => s.setLineGroup(Number(sel.value))));
    return sel;
  }

  /** Accent colour of the drawing (a document setting, one undo step); the engine offers the presets (`accents`). */
  private accentSelect(current: string | null): HTMLElement {
    const sel = h("select", { class: "isk-input isk-linegroup", title: t("cmd.accent"), "aria-label": t("cmd.accent"), "data-cmd": "accent" });
    sel.append(h("option", { value: "", selected: !current }, t("cmd.accentNone")));
    let presets: { id: string; color: string; name: string }[] = [];
    try { presets = this.engine.call<{ id: string; color: string; name: string }[]>("accents", { lang: lang() }); } catch { presets = []; }
    for (const p of presets) sel.append(h("option", { value: p.id, selected: current === p.color }, p.name));
    sel.addEventListener("change", () => this.edit((s) => s.setAccent(sel.value)));
    return sel;
  }

  /** Office chosen last in the compatibility check (the first profile until then). */
  private patentProfile = "";
  /** The compatibility check covers every slide (PowerPoint only). */
  private patentAllSlides = true;

  /**
   * Office.js can read and write the slides (tags, selection API, shapes). Not in the COM add-in's sidebar, where the COM
   * add-in owns the slide (it has its own compare and clean commands).
   */
  private get slideAccess(): boolean {
    return !!(this.office.tags && this.office.selection && this.office.shapes) && this.sync instanceof SlideSync;
  }

  /** The shown slide's units, or undefined without access to the slides. */
  private readCurrentSlide(): Promise<SlideUnits | undefined> {
    return this.slideAccess ? readCurrentSlideUnits(this.office) : Promise.resolve(undefined);
  }

  /** The export scope as it is now: the selection, else the current slide, else the editor page (export.ts). */
  private exportSource(): Promise<ExportSource> {
    return resolveExportSource({
      engine: this.engine, editor: this.sessionObj, state: this.st, slideAccess: this.slideAccess, lang: lang(),
      readSlide: () => this.readCurrentSlide(),
    });
  }

  /** Runs an export on the resolved scope; a slide without InventaSketch elements says so. */
  private async exportWith(f: (source: ExportSource) => void | Promise<void>): Promise<void> {
    let source: ExportSource | undefined;
    try {
      source = await this.exportSource();
      if (source.empty) { this.setStatus(t("export.nothing")); return; }
      await f(source);
      this.setStatus(exportScopeLabel(source.scope, t));
    } catch (e) {
      this.report(e);
    } finally {
      source?.dispose();
    }
  }

  /**
   * "Clean slides …" (Review, like the COM add-in): a form for the scope (selection, current slide or all slides), the
   * chart handling and the options, then the engine's `sanitize`; changed elements are redrawn on their slides and the
   * report (counts, seed, lines and what is not changed) replaces the form.
   */
  private toggleCleanSlides(anchor: HTMLElement): void {
    const wasClean = this.settingsEl?.dataset.kind === "clean";
    if (this.settingsEl) { this.settingsEl.remove(); this.settingsEl = undefined; if (wasClean) return; }
    const hasSelection = this.st.selection.length > 0;
    const slides = this.slideAccess;
    const form: CleanForm = defaultCleanForm(hasSelection);
    const radio = (name: string, value: string, checked: boolean, label: string, enabled: boolean, on: () => void) => {
      const r = h("input", { type: "radio", name, value, checked, disabled: !enabled, "data-cmd": `${name}-${value}` });
      r.addEventListener("change", () => { if (r.checked) on(); });
      return h("label", { class: "isk-check" }, r, h("span", {}, label));
    };
    const check = (checked: boolean, label: string, cmd: string, on: (v: boolean) => void) => {
      const c = h("input", { type: "checkbox", checked, "data-cmd": cmd });
      c.addEventListener("change", () => on(c.checked));
      return h("label", { class: "isk-check" }, c, h("span", {}, label));
    };
    const seed = h("input", { class: "isk-input", type: "text", inputmode: "numeric", "aria-label": t("clean.seed"), "data-cmd": "clean-seed" });
    seed.addEventListener("input", () => (form.seed = seed.value));
    const body = h("div", {},
      h("fieldset", {}, h("legend", {}, t("clean.what")),
        ...cleanScopes(hasSelection, slides).map((o) => radio("isk-clean-scope", o.scope, form.scope === o.scope, t(o.key), o.enabled, () => (form.scope = o.scope)))),
      h("fieldset", {}, h("legend", {}, t("clean.charts")),
        ...(["normalised", "randomised", "none"] as const).map((m) => radio("isk-clean-mode", m, form.mode === m, t(`clean.mode.${m}`), true, () => (form.mode = m)))),
      h("fieldset", {}, h("legend", {}, t("clean.also")),
        check(form.renameCategories, t("clean.rename"), "clean-rename", (v) => (form.renameCategories = v)),
        check(form.replaceTexts, t("clean.texts"), "clean-texts", (v) => (form.replaceTexts = v)),
        check(form.stripMeta, t("clean.meta"), "clean-meta", (v) => (form.stripMeta = v)),
        h("label", { class: "isk-field" }, h("span", { class: "isk-field-label" }, t("clean.seed")), seed)),
      ...(slides ? [h("p", { class: "isk-muted" }, t("clean.undo"))] : []));
    const ok = h("button", { class: "isk-btn isk-primary", type: "button", "data-cmd": "clean-run" }, t("clean.run"));
    const cancel = h("button", { class: "isk-btn", type: "button", "data-cmd": "clean-cancel" }, t("clean.cancel"));
    const buttons = h("div", { class: "isk-row isk-clean-buttons" }, ok, cancel);
    const close = h("button", { class: "isk-btn isk-icon-btn isk-close", type: "button", "aria-label": t("close"), html: icon("close", 16) });
    const panel = h("div", { class: "isk-popover isk-clean", role: "dialog", "aria-label": t("clean.title") },
      h("div", { class: "isk-pop-head" }, h("strong", {}, t("clean.title")), close), body, buttons);
    panel.dataset.kind = "clean";
    const dismiss = () => { panel.remove(); if (this.settingsEl === panel) this.settingsEl = undefined; anchor.focus(); };
    close.addEventListener("click", dismiss);
    cancel.addEventListener("click", dismiss);
    panel.addEventListener("keydown", (e) => { if (e.key === "Escape") { e.stopPropagation(); dismiss(); } });
    ok.addEventListener("click", async () => {
      ok.disabled = true;
      body.replaceChildren(h("p", { class: "isk-pc-summary", role: "status" }, t("clean.working")));
      buttons.replaceChildren();
      let totals: CleanTotals | undefined;
      try {
        totals = await this.cleanSlides(cleanRequest(form));
      } catch (e) {
        this.report(e);
      }
      if (!totals) { dismiss(); return; }
      const r = formatCleanReport(totals, t);
      body.replaceChildren(
        h("p", { class: "isk-pc-summary", role: "status" }, r.head),
        ...(r.lines.length ? [h("ul", { class: "isk-findings isk-clean-lines" }, ...r.lines.map((l) => h("li", {}, l)))] : []),
        h("p", { class: "isk-muted" }, r.note));
      const done = h("button", { class: "isk-btn", type: "button", "data-cmd": "clean-close" }, t("close"));
      done.addEventListener("click", dismiss);
      buttons.replaceChildren(done);
      this.setStatus(r.head);
      done.focus();
    });
    this.root.append(panel);
    this.settingsEl = panel;
    (panel.querySelector<HTMLInputElement>("input:checked") ?? ok).focus();
  }

  /**
   * Cleans the chosen scope. Selection (and the editor page without access to the slides): the editor session, one undo
   * step there; the tracked units are redrawn on the slide (also with live sync off – cleaning is about the slide).
   * Current slide / all slides: per slide a temporary session with its units as they are now; the changed units are
   * redrawn on their slide, and units the editor holds take the cleaned model (so a later edit does not bring the old
   * values back). One seed for the whole run.
   */
  private async cleanSlides(req: CleanRequest): Promise<CleanTotals> {
    let totals = emptyTotals(req.value.seed ?? 0);
    if (req.scope === "selection" || !this.slideAccess) {
      const ids = req.scope === "selection" ? [...this.st.selection] : [];
      const r = this.sessionObj.sanitize(ids, req.mode, req.value, lang());
      totals = addReport(totals, r.report);
      if (r.report.changed) {
        this.apply(r.state, false);
        await this.sync?.scheduleLive(r.state);
      }
      return totals;
    }
    const page = { w: this.st.pageWidth, h: this.st.pageHeight };
    const list: SlideUnits[] = req.scope === "all"
      ? await readSlideUnits(this.office)
      : [await this.readCurrentSlide()].filter((x): x is SlideUnits => !!x);
    const draw = drawCapabilities(this.office);
    const sync = this.sync as SlideSync;
    const inEditor = new Set(this.st.objects.map((o) => o.id));
    const reload: string[] = [];
    for (const slide of list) {
      if (!slide.units.length) continue;
      const cleaned = cleanSlide(this.engine, slide, page, lang(), req.mode, req.value, draw);
      totals = addReport(totals, cleaned.report);
      if (!cleaned.units.length) continue;
      await sync.replaceOnSlide(cleaned.units, slide.slideId);
      for (const u of cleaned.units) if (u.objectIds.some((id) => inEditor.has(id))) reload.push(u.payload);
    }
    if (reload.length) {
      // The editor holds some of the cleaned units: they take the cleaned models; the selection stays.
      const selection = [...this.st.selection];
      for (const payload of reload) this.sessionObj.importCompact(payload);
      this.sessionObj.select(selection);
      this.apply(this.sessionObj.state(), false);
    }
    return totals;
  }

  /** "Compare with the previous slide": marks what differs on the selected slide, or removes the marks again. */
  private async compareSlides(): Promise<void> {
    try {
      const r = await compareWithPrevious(this.engine, this.office, { w: this.st.pageWidth, h: this.st.pageHeight }, lang(),
        (d, previous) => t("cmp.legend", previous, d.added.length, d.changed.length, d.removed.length));
      if (r === "cleared") this.setStatus(t("cmp.cleared"));
      else if (r === "none") this.setStatus(t("cmp.none"));
      else if (r === "first") this.setStatus(t("cmp.first"));
      else if (r === "unsupported") this.setStatus(t("cmp.unsupported"));
      else this.setStatus(t("cmp.done", r.added.length, r.changed.length, r.removed.length));
    } catch (e) {
      this.report(e);
    }
  }

  /**
   * "Compatibility (drawing rules)": the engine's preflight of the page for an office, as a popover – office choice, findings
   * (errors first) with suggestion and cited source; a finding with an element selects it. The disclaimer is always shown.
   */
  private togglePatentCheck(anchor: HTMLElement): void {
    const wasPatent = this.settingsEl?.dataset.kind === "patent";
    if (this.settingsEl) { this.settingsEl.remove(); this.settingsEl = undefined; if (wasPatent) return; }
    let profiles: { id: string; name: string }[] = [];
    try { profiles = this.engine.patentProfiles(lang()); } catch (e) { this.report(e); return; }
    if (!profiles.some((p) => p.id === this.patentProfile)) this.patentProfile = profiles[0]?.id ?? "";
    const office = h("select", { class: "isk-input", "aria-label": t("pc.office"), "data-cmd": "patentprofile" });
    for (const p of profiles) office.append(h("option", { value: p.id, selected: p.id === this.patentProfile }, p.name));
    const summary = h("p", { class: "isk-pc-summary", role: "status" });
    const list = h("ul", { class: "isk-findings" });
    const disclaimer = h("p", { class: "isk-muted" });
    // In PowerPoint the check can cover every slide (units read from the slides, one engine session per slide).
    const slidesBox = h("input", { type: "checkbox", checked: this.patentAllSlides, "data-cmd": "patentallslides" });
    const scope = this.office.tags ? h("label", { class: "isk-check" }, slidesBox, h("span", {}, t("pc.allSlides"))) : undefined;
    const show = (r: { disclaimer: string; findings: (PreflightResult["findings"][number] & Partial<SlideFinding>)[] }) => {
      disclaimer.textContent = r.disclaimer;
      const c = findingCounts(r.findings);
      summary.textContent = r.findings.length ? t("pc.summary", r.findings.length, c.error, c.warning) : t("pc.none");
      list.replaceChildren(...orderFindings(r.findings).map((g) => {
        const f = g as PreflightResult["findings"][number] & Partial<SlideFinding>;
        // The message selects the element (if the finding has one): on the page, or on its slide in PowerPoint; the
        // cited sources are short links.
        const id = f.id;
        const where = f.slide ? t("pc.slide", f.slide) + " · " : "";
        let message: HTMLElement = h("span", {}, where + f.message);
        if (f.slideId) {
          message = h("button", { class: "isk-finding-go", type: "button", title: t("pc.goTo") }, where + f.message);
          message.addEventListener("click", () => goToFinding(this.office, f.slideId!, f.shapeId).catch((e) => this.report(e)));
        } else if (id) {
          message = h("button", { class: "isk-finding-go", type: "button", title: t("pc.goTo") }, f.message);
          message.addEventListener("click", () => this.guard(() => this.apply(this.sessionObj.select([id]))));
        }
        const sources = h("span", { class: "isk-sources" }, ...sourceLinks(f.source).map((s) =>
          s.url ? h("a", { href: s.url, target: "_blank", rel: "noopener noreferrer", title: s.url }, s.label) : h("span", {}, s.label)));
        const body = h("span", { class: "isk-finding-text" },
          message,
          ...(f.suggestion ? [h("span", { class: "isk-muted" }, f.suggestion)] : []),
          sources);
        const badge = h("span", { class: `isk-sev isk-sev-${f.severity}` }, t(`pc.sev.${f.severity}`));
        return h("li", {}, h("div", { class: "isk-finding" }, badge, body));
      }));
    };
    const run = () => {
      if (this.patentAllSlides && this.office.tags) {
        summary.textContent = t("pc.reading");
        readSlideUnits(this.office)
          .then((slides) => show(checkSlides(this.engine, slides, this.patentProfile, lang(), { w: this.st.pageWidth, h: this.st.pageHeight })))
          .catch((e) => this.report(e));
        return;
      }
      try { show(this.sessionObj.preflight(this.patentProfile, lang())); } catch (e) { this.report(e); }
    };
    slidesBox.addEventListener("change", () => { this.patentAllSlides = slidesBox.checked; run(); });
    office.addEventListener("change", () => { this.patentProfile = office.value; run(); });
    const again = h("button", { class: "isk-btn", type: "button", "data-cmd": "patentrecheck" }, t("pc.recheck"));
    again.addEventListener("click", run);
    const close = h("button", { class: "isk-btn isk-icon-btn isk-close", type: "button", "aria-label": t("close"), html: icon("close", 16) });
    const panel = h("div", { class: "isk-popover isk-pc", role: "dialog", "aria-label": t("pc.title") },
      h("div", { class: "isk-pop-head" }, h("strong", {}, t("pc.title")), close),
      h("label", { class: "isk-field" }, h("span", { class: "isk-field-label" }, t("pc.office")), h("div", { class: "isk-row" }, office, again)),
      ...(scope ? [scope] : []),
      summary, list, disclaimer);
    panel.dataset.kind = "patent";
    close.addEventListener("click", () => { panel.remove(); this.settingsEl = undefined; anchor.focus(); });
    panel.addEventListener("keydown", (e) => { if (e.key === "Escape") { e.stopPropagation(); close.click(); } });
    this.root.append(panel);
    this.settingsEl = panel;
    run();
    office.focus();
  }

  private toggleSettings(anchor: HTMLElement): void {
    const wasSettings = !!this.settingsEl && !this.settingsEl.dataset.kind;
    if (this.settingsEl) { this.settingsEl.remove(); this.settingsEl = undefined; if (wasSettings) return; }
    const radio = (name: string, value: string, current: string, label: string, on: () => void) => {
      const r = h("input", { type: "radio", name, value, checked: current === value });
      r.addEventListener("change", on);
      return h("label", { class: "isk-check" }, r, h("span", {}, label));
    };
    const check = (checked: boolean, label: string, on: (v: boolean) => void) => {
      const c = h("input", { type: "checkbox", checked });
      c.addEventListener("change", () => on(c.checked));
      return h("label", { class: "isk-check" }, c, h("span", {}, label));
    };
    const grid = h("select", { class: "isk-input", "aria-label": t("set.grid") });
    for (const g of [0, 1, 2.5, 5, 10]) grid.append(h("option", { value: String(g), selected: this.settings.grid === g }, g ? `${g} mm` : t("set.gridOff")));
    grid.addEventListener("change", () => { this.settings.grid = Number(grid.value); saveSettings(this.settings); this.configureSnap(); this.stage.render(); });
    const open = h("input", { type: "file", accept: ".json,.isk.json,application/json", hidden: true });
    open.addEventListener("change", async () => {
      const f = open.files?.[0];
      if (!f) return;
      try {
        this.apply(this.engine.call<EditorState>("openDocument", { session: this.sessionObj.id, json: await f.text() }));
        this.stage.fit();
      } catch (e) { this.report(e); }
    });
    const saveBtn = h("button", { class: "isk-btn", type: "button" }, t("set.save"));
    saveBtn.addEventListener("click", () => this.guard(() => download("sketch.isk.json", new Blob([this.sessionObj.save()], { type: "application/json" }))));
    const openBtn = h("button", { class: "isk-btn", type: "button" }, t("set.open"));
    openBtn.addEventListener("click", () => open.click());
    const close = h("button", { class: "isk-btn isk-icon-btn isk-close", type: "button", "aria-label": t("close"), html: icon("close", 16) });
    const panel = h("div", { class: "isk-popover", role: "dialog", "aria-label": t("cmd.settings") },
      h("div", { class: "isk-pop-head" }, h("strong", {}, t("cmd.settings")), close),
      h("fieldset", {}, h("legend", {}, t("set.theme")),
        ...(["auto", "light", "dark"] as const).map((v) => radio("isk-theme", v, this.settings.theme, t(`set.theme.${v}`), () => { this.settings.theme = v; saveSettings(this.settings); this.applyTheme(); }))),
      h("fieldset", {}, h("legend", {}, t("set.lang")),
        ...(["auto", "de", "en"] as const).map((v) => radio("isk-lang", v, this.settings.lang, t(`set.lang.${v}`), () => { this.settings.lang = v; saveSettings(this.settings); this.relocalise(); }))),
      h("fieldset", {}, h("legend", {}, t("set.drawing")),
        check(this.settings.snap, t("set.snap"), (v) => { this.settings.snap = v; saveSettings(this.settings); this.stage.render(); }),
        check(this.settings.anchor, t("set.anchor"), (v) => { this.settings.anchor = v; saveSettings(this.settings); }),
        h("label", { class: "isk-field" }, h("span", { class: "isk-field-label" }, t("set.grid")), grid),
        check(!!this.st.monochrome, t("set.mono"), (v) => this.setMonochrome(v))),
      this.colorByFieldset(),
      h("fieldset", {}, h("legend", {}, t("val.level")),
        ...(["quick", "explore", "engineer"] as const).map((v) => radio("isk-level", v, this.st.valueLevel ?? "quick", t(`val.level.${v}`), () => this.setValueLevel(v))),
        h("p", { class: "isk-muted" }, t("val.levelHint"))),
      h("fieldset", {}, h("legend", {}, t("set.file")), h("div", { class: "isk-row" }, openBtn, saveBtn), open),
      this.exportFieldset(),
      h("p", { class: "isk-muted" }, this.versionLine()),
    );
    close.addEventListener("click", () => { panel.remove(); this.settingsEl = undefined; anchor.focus(); });
    panel.addEventListener("keydown", (e) => { if (e.key === "Escape") { e.stopPropagation(); close.click(); } });
    this.root.append(panel);
    this.settingsEl = panel;
    panel.querySelector<HTMLInputElement>("input")?.focus();
  }

  /**
   * "Colour by medium" (interaction-ux.md section 6b, the COM add-in's More ▸ Colour by medium): Off / By medium for the
   * drawing (`setColorBy`, one undo step; every unit is redrawn) and "Insert legend" with the colours used.
   */
  private colorByFieldset(): HTMLElement {
    const sel = h("select", { class: "isk-input", "aria-label": t("set.colorBy"), title: t("set.colorByHint"), "data-cmd": "colorby" });
    sel.append(h("option", { value: "", selected: !this.st.colorBy }, t("set.colorBy.off")), h("option", { value: "medium", selected: this.st.colorBy === "medium" }, t("set.colorBy.medium")));
    sel.addEventListener("change", () => this.edit((s) => s.setColorBy(sel.value === "medium" ? "medium" : "")));
    const legend = h("button", { class: "isk-btn", type: "button", "data-cmd": "colorbylegend", html: `${icon("cmd-legend")}<span>${t("set.legend")}</span>` });
    legend.addEventListener("click", () => void this.insertLegend());
    return h("fieldset", {}, h("legend", {}, t("set.colorBy")),
      h("div", { class: "isk-row" }, sel, legend),
      h("p", { class: "isk-muted" }, t("set.colorByHint")));
  }

  /**
   * "Insert legend": the medium colours used on the current slide (PowerPoint: read from every unit on it), else on the
   * editor page, as one group top right (engine `insertMediumLegend`); in PowerPoint it is written onto the slide.
   */
  private async insertLegend(): Promise<void> {
    try {
      let colours: string[] | undefined;
      const slide = this.slideAccess ? await this.readCurrentSlide() : undefined;
      if (slide?.units.length) {
        const { session } = slideSession(this.engine, slide.units, { w: this.st.pageWidth, h: this.st.pageHeight }, lang());
        try { colours = session.mediumColours(lang(), true).map((c) => c.id); } finally { closeSession(this.engine, session); }
      } else colours = this.sessionObj.mediumColours(lang(), true).map((c) => c.id);
      if (!colours.length) { this.toast(t("legend.none")); return; }
      const r = this.sessionObj.insertMediumLegend(lang(), colours);
      if (!r.inserted) { this.toast(t("legend.none")); return; }
      this.apply(r.state, true);
      if (this.sync) await this.sync.insert(r.state.selection);
      this.toast(t("legend.done", colours.length));
    } catch (e) {
      this.report(e);
    }
  }

  /**
   * "Export data": the scope first ("Export \u2013 selection (3)" / "Export \u2013 slide", like the COM add-in), then one button per
   * domain export that applies to it; a click resolves the scope again and downloads the file.
   */
  private exportFieldset(): HTMLElement {
    const scope = h("p", { class: "isk-muted", "data-cmd": "exportdatascope" }, exportScopeLabel(editorExportScope(this.st, this.slideAccess), t));
    const content = h("div", { class: "isk-col" });
    const fill = (source: ExportSource) => {
      scope.textContent = exportScopeLabel(source.scope, t);
      let list: ExporterInfo[] = [];
      try {
        list = source.empty ? [] : source.session.exporters(source.ids ?? [], lang());
      } catch (e) {
        this.report(e);
      }
      const buttons = list.map((x) => {
        const b = h("button", { class: "isk-btn", type: "button", title: x.preserves }, x.name);
        b.addEventListener("click", () => void this.exportWith((s) => {
          const r = s.session.exportData(s.ids ?? [], x.id, lang());
          const bom = r.extension === "csv" ? "\ufeff" : "";
          download(`${exportFileStem(s.scope)}.${r.extension}`, new Blob([bom + r.text], { type: `${r.mime};charset=utf-8` }));
        }));
        return b;
      });
      content.replaceChildren(...(buttons.length ? buttons : [h("p", { class: "isk-muted" }, t("set.exportNone"))]));
    };
    this.exportSource()
      .then((source) => { try { fill(source); } finally { source.dispose(); } })
      .catch((e) => this.report(e));
    return h("fieldset", {}, h("legend", {}, t("set.export")), scope, content);
  }

  private versionLine(): string {
    try {
      const v = this.engine.version();
      return `Engine ${v.engine} · ${v.modules.map((m) => `${m.id} ${m.version} (${m.types})`).join(", ")}`;
    } catch {
      return "";
    }
  }

  // ------------------------------------------------------------------ language/theme

  private applyLanguage(): void {
    const browser = typeof navigator !== "undefined" ? navigator.language : undefined;
    setLang(resolveLang(this.settings.lang, this.opts.officeLanguage, browser));
    document.documentElement.lang = lang();
  }

  private relocalise(): void {
    this.insertBox.close();
    this.applyLanguage();
    this.engine.call("setPage", { session: this.sessionObj.id, lang: lang(), includeSvg: false });
    this.layout();
    this.root.append(this.bar.el);
    this.loadCatalog();
    this.apply(this.sessionObj.state());
    this.inspector.update(true);
  }

  private applyTheme(): void {
    applyTheme(this.root, resolveTheme({ setting: this.settings.theme, officeBodyBackground: this.opts.officeBackground, prefersDark: this.prefersDark?.matches }));
  }

  private loadCatalog(): void {
    try {
      this.catalog = this.engine.catalog(lang() as Lang);
      this.library.setCatalog(this.visibleCatalog(this.catalog));
    } catch (e) {
      this.report(e);
    }
  }

  private configureSnap(): void {
    this.sessionObj.setSnap({ grid: this.settings.grid > 0 ? this.settings.grid : null });
  }

  // ------------------------------------------------------------------ state

  get session(): Session {
    return this.sessionObj;
  }

  get state(): EditorState {
    return this.st;
  }

  apply(next: EditorState, live = false): void {
    if (next.svg === undefined && this.st) next = { ...next, svg: this.st.svg };
    this.st = next;
    this.stage.render();
    this.refreshPart();
    this.renderTopbar();
    if (this.settings.inspectorOpen || this.root.dataset.layout === "wide") this.inspector.update();
    this.valueCard.refresh();
    this.updateBar();
    if (live && this.sync && this.settings.liveSync) this.sync.scheduleLive(next).catch((e) => this.report(e));
  }

  /**
   * The floating bar (docs/en/design/interaction-ux.md section 5, the COM add-in's structure): the element's main action
   * with a short label ("Make it …", "Values", "Data", "Text", "View", the first style choice, else "Insert"), up to three
   * pictograms in the order of use (its telling actions, the view, Insert, its tools, its pictorial choices, rotate and
   * mirror where they change the drawing), then "⋯" – the pie with everything. A selected part shows its name, its first
   * three commands and "⋯"; two or more elements keep the multi-selection groups (`arrangeOptions`).
   */
  private updateBar(): void {
    const sel = this.st.selection;
    const key = sel.join(",");
    const box = this.stage.selectionScreenBox(true);
    const suppressed = this.stage.dragging || this.pie.open || this.prompt.open || this.insertBox.open || this.valueCard.open;
    if (sel.length !== 1 || sel[0] !== this.nextFor) this.nextFor = undefined;
    this.traceFor = undefined;
    this.nextChoices = [];
    if (!sel.length) return this.bar.show("", [], undefined, true);
    const part = this.partFor(sel);
    if (part) return this.bar.show(`${key}#${part.key}`, this.partBar(part), box, suppressed);
    // A selected corner of a line: its number, split here (or open the outline) and remove (COM `CornerButtons`).
    const corner = this.stage.corner;
    if (corner && sel.length === 1 && sel[0] === corner.id) {
      const cmds = cornerCommands(this.stage.polylineInfo(corner.id), corner.index);
      if (cmds.length) {
        const items: BarItem[] = cmds.map((c) => ({ kind: "command", cmd: { id: `corner:${c}`, label: t(`corner.${c}`), icon: c === "remove" ? "cmd-point-remove" : "cmd-split" } }));
        return this.bar.show(`${key}@corner${corner.index}`, [[{ kind: "command", cmd: { id: "info:corner", label: t("corner.selected", corner.index + 1), icon: "", text: t("corner.selected", corner.index + 1) } }], items], box, suppressed);
      }
    }
    const known = sel.some((id) => this.objectById(id)?.known);
    if (elementCount(this.st.objects, sel) >= 2) {
      let options: ArrangeOption[] = [];
      try {
        options = this.sessionObj.arrangeOptions(sel, lang());
      } catch (e) {
        this.report(e);
      }
      const groups = multiBarGroups(options);
      const asItem = (o: { id: string; label: string; icon: string }): BarItem => ({ kind: "command", cmd: { id: `arr:${o.id}`, label: o.label, icon: o.icon } });
      const assembly: BarItem[] = known && this.assemblyGrows(sel) ? [{ kind: "command", cmd: { id: "assembly", label: t("ctx.assembly"), icon: "cmd-select-assembly" } }] : [];
      this.bar.show(key, [groups.views.map(asItem), groups.arrange.map(asItem), assembly], box, suppressed, t("ctx.multi"));
      return;
    }
    if (this.nextFor) {
      try {
        this.nextChoices = this.sessionObj.nextItems(this.nextFor, lang(), 12);
      } catch (e) {
        this.report(e);
      }
    }
    const m = this.elementModel(sel);
    const { main, icons } = pickBar(m.obj?.known ? this.barCandidates(m) : []);
    this.bar.show(key, [main ? [main.item] : [], icons.map((c) => c.item)], box, suppressed);
  }

  /** The selected part, while its element is selected alone. */
  private partFor(sel: string[]): SelectedPart | undefined {
    const part = this.part;
    return part && sel.length === 1 && sel[0] === part.id ? part : undefined;
  }

  /** The bar of a selected part: its name, its first three commands (choices as dropdowns), then "⋯". */
  private partBar(part: SelectedPart): BarItem[][] {
    let acts: ActionInfo[] = [];
    try {
      acts = this.sessionObj.partActions(part.id, part.key, lang());
    } catch (e) {
      this.report(e);
    }
    const items: BarItem[] = [];
    for (const e of actionEntries(acts, PART_CHOICE, svgDataUrl)) {
      if (items.length >= 3) break;
      if ("choice" in e) { items.push({ kind: "choice", choice: e.choice }); continue; }
      if (!e.action.enabled) continue;
      const a = { ...e.action, id: PART_PREFIX + e.action.id };
      items.push(isTelling(a.icon)
        ? { kind: "action", action: a, label: isStep(a.icon) ? shortLabel(a.label, 14) : undefined }
        : { kind: "action", action: a, text: shortLabel(a.label, 18) });
    }
    return [[{ kind: "command", cmd: { id: "info:part", label: t("part.selected"), icon: "", text: part.label } }], items];
  }

  /** Actions, choices and inspector of a selection, for the bar and the pie. */
  private elementModel(sel: string[]): ElementModel {
    const obj = sel.length === 1 ? this.objectById(sel[0]) : undefined;
    const known = sel.some((id) => this.objectById(id)?.known);
    let actions: ActionInfo[] = [];
    try {
      actions = this.sessionObj.contextActions(sel, lang());
    } catch (e) {
      this.report(e);
    }
    if (known) actions = filterTransforms(actions, this.transformEffect(sel));
    let info: InspectorInfo | undefined;
    let conversions: ActionInfo[] = [];
    const paramChoices: ChoiceEntry[] = [];
    const views = actions.filter((a) => a.kind === "view");
    if (views.length > 1)
      paramChoices.push({ key: "@view", label: t("ctx.view"), current: views.find((v) => v.active)?.id ?? views[0].id, options: views.map((v) => ({ id: v.id, label: v.label, icon: hasIcon(v.icon) ? v.icon : undefined })) });
    if (obj?.known) {
      try {
        conversions = this.sessionObj.conversions(sel, lang());
        if (conversions.length > 1)
          paramChoices.push({
            key: "@type", label: t("ctx.lineType"), current: obj.type,
            options: conversions.map((c) => {
              const item = this.catalog.items.find((i) => i.typeId === c.id);
              return { id: c.id, label: c.label, image: item?.thumbnail ? svgDataUrl(item.thumbnail) : undefined };
            }),
          });
        info = this.sessionObj.inspector(sel, lang());
        // Choices with pictograms first, then the other choices (shown as short text), in parameter order.
        const choices = info.params.filter((p) => p.kind === "choice" && !p.advanced && p.options && p.options.length > 1);
        const pictorial = (p: (typeof choices)[number]) => p.options!.every((o) => o.icon && hasIcon(o.icon));
        for (const p of [...choices.filter(pictorial), ...choices.filter((p) => !pictorial(p))]) paramChoices.push(this.paramChoice(p, pictorial(p)));
        if (obj.kind === "points")
          paramChoices.push({
            key: "@dash", label: t("ctx.lineStyle"), current: info.style?.dash ?? "solid",
            options: DASHES.map(([id, ico]) => ({ id, label: t(`dash.${id}`), icon: ico })),
          });
      } catch (e) {
        this.report(e);
      }
    }
    const covered = new Set(paramChoices.map((c) => c.key));
    const isCovered = (a: ActionInfo) => (a.paramKeys ?? []).some((k) => covered.has(k));
    const { choices: actionChoices } = groupActionChoices(actions.filter((a) => a.choice && !isCovered(a)), ELEMENT_CHOICE, svgDataUrl);
    const plain = actions.filter((a) => !a.choice && !(a.kind === "type" && isCovered(a)));
    return { sel, obj, known, actions, plain, paramChoices, actionChoices, covered, info, conversions };
  }

  /** A parameter choice as a dropdown (pictograms when every option has one). */
  private paramChoice(p: InspectorInfo["params"][number], pictorial: boolean): ChoiceEntry {
    return {
      key: p.key, label: p.label, current: String(p.value ?? p.default ?? p.options![0].id), mirror: p.mirrorIcons,
      options: p.options!.map((o) => ({ id: o.id, label: o.label, icon: pictorial ? o.icon : undefined })),
    };
  }

  /** Bar candidates of one element in the order of use; the first marked main gets a short label (COM `BarCandidates`). */
  private barCandidates(m: ElementModel): BarCandidate[] {
    const obj = m.obj!;
    const sel = m.sel;
    const out: BarCandidate[] = [];
    const cmd = (id: string, label: string, iconId: string, active?: boolean): BarItem => ({ kind: "command", cmd: { id, label, icon: iconId, active } });
    const viewChoice = m.paramChoices.find((c) => c.key === "@view");
    const styleChoices = [...m.paramChoices.filter((c) => c.key !== "@view"), ...m.actionChoices];
    const text = this.mainTextTarget();
    const insert = m.known ? cmd("insert", t("ctx.insert"), "cmd-insert") : undefined;
    const data = m.actions.find((a) => a.id === "chart.data" && a.enabled);
    const labelled = (item: BarItem, label: string): BarItem => ({ ...item, label });
    // The main action.
    let main: BarCandidate | undefined;
    if (this.nextFor && this.nextChoices.length >= 2) main = { id: "makeIt", item: labelled(cmd("makeIt", t("ctx.makeIt"), "cmd-cycle"), t("bar.makeIt")) };
    else if (obj.hasValues && (this.st.valueLevel ?? "quick") !== "quick") main = { id: "values", item: labelled(cmd("values", t("ctx.values"), "cmd-values"), t("bar.values")) };
    else if (data) main = { id: `act:${data.id}`, item: { kind: "action", action: data, label: t("bar.data") } };
    else if (text) main = { id: "text", item: labelled(cmd("text", t("ctx.text"), "cmd-text"), t("bar.text")) };
    else if (viewChoice) main = { id: `choice:${viewChoice.key}`, item: { kind: "choice", choice: viewChoice, label: t("bar.view") } };
    else {
      const first = styleChoices.find(isPictorial);
      if (first) main = { id: `choice:${first.key}`, item: { kind: "choice", choice: first, label: shortLabel(first.label, 20) } };
    }
    if (!main && insert) main = { id: "insert", item: labelled(insert, t("bar.insert")) };
    if (main) out.push({ ...main, main: true });
    // Then the pictograms, in order of preference.
    const chart = obj.type === "chart.xy" || obj.type === "chart.figure";
    if (chart) out.push({ id: "sketch", item: cmd("sketch", t("ctx.sketch"), "cmd-pencil") });
    for (const { action, pictorial } of barActions(m.plain, m.covered, !!text))
      if (pictorial && action.id !== data?.id) out.push({ id: `act:${action.id}`, item: { kind: "action", action, label: isStep(action.icon) ? shortLabel(action.label, 14) : undefined } });
    if (viewChoice) out.push({ id: `choice:${viewChoice.key}`, item: { kind: "choice", choice: viewChoice } });
    if (insert) out.push({ id: "insert", item: insert });
    const trace = this.traceOf(sel);
    if (canTrace(trace)) {
      this.traceFor = { id: sel[0], trace };
      out.push({ id: "trace", item: cmd("trace", t("ctx.trace"), "cmd-trace") });
    }
    const info = this.dofOf(sel);
    if (canDrawGhosts(obj.kind, info?.dof, trace)) out.push({ id: "ghosts", item: cmd("ghosts", t("ctx.ghosts"), "cmd-ghosts") });
    if (info?.explodable) out.push({ id: "explode", item: cmd("explode", t("ctx.explode"), "cmd-explode") });
    // Motion mode only for parts that can move: a link of a chain with F >= 1, a gear coupled with something.
    if (canMove(this.stage.motionOf(sel[0]))) out.push({ id: "motion", item: cmd("motion", t("ctx.motion"), "cmd-motion", this.motion) });
    if (this.assemblyGrows(sel)) out.push({ id: "assembly", item: cmd("assembly", t("ctx.assembly"), "cmd-select-assembly") });
    for (const c of styleChoices.filter(isPictorial)) out.push({ id: `choice:${c.key}`, item: { kind: "choice", choice: c } });
    for (const c of styleChoices.filter((x) => x.labelInBar && !x.menu)) out.push({ id: `choice:${c.key}`, item: { kind: "choice", choice: c } });
    for (const c of styleChoices.filter((x) => x.menu)) out.push({ id: `choice:${c.key}`, item: { kind: "choice", choice: c } });
    for (const id of ["rotate90", "flipH"]) {
      const a = m.actions.find((x) => x.id === id);
      if (a) out.push({ id: `act:${a.id}`, item: { kind: "action", action: a } });
    }
    return out;
  }

  private objectById(id: string): ObjectInfo | undefined {
    return this.st.objects.find((o) => o.id === id);
  }

  /** Mirror/rotate effect of the selection; undefined (keep all) if the engine cannot tell. */
  private transformEffect(sel: string[]): TransformEffect | undefined {
    try {
      return this.sessionObj.transformEffect(sel);
    } catch (e) {
      this.report(e);
      return undefined;
    }
  }

  /** Coupled objects outside the selection exist (gear train, drive). */
  private assemblyGrows(sel: string[]): boolean {
    try {
      return assemblyExtends(this.sessionObj.assembly(sel), sel);
    } catch (e) {
      this.report(e);
      return false;
    }
  }

  /** Coupler curve of a single selected link (points object); the engine returns no points unless the chain has F = 1. */
  private traceOf(sel: string[]): TraceInfo | undefined {
    const o = sel.length === 1 ? this.objectById(sel[0]) : undefined;
    if (!o?.known || o.kind !== "points") return undefined;
    try {
      return this.sessionObj.trace(o.id);
    } catch {
      return undefined;
    }
  }

  /** Degree of freedom (self-built linkage) and explodability of a single selected object. */
  private dofOf(sel: string[]): { dof: number | null; explodable: boolean } | undefined {
    const o = sel.length === 1 ? this.objectById(sel[0]) : undefined;
    if (!o?.known) return undefined;
    try {
      return this.sessionObj.dof(o.id);
    } catch {
      return undefined;
    }
  }

  /**
   * Motion mode (bar toggle): dragging a part that can move turns its gear train or pulls its linkage instead of moving
   * it. Session only, like the COM add-in: not saved, the task pane always starts in edit mode.
   */
  private motion = false;

  /** Link whose coupler curve the bar offers, with the traced path. */
  private traceFor?: { id: string; trace: TraceInfo };
  /** "Make it …" choices for the element just added with "+". */
  private nextChoices: SuggestionInfo[] = [];

  /** Selected part of the single selected element (charts v2), shown outlined; the bar and pie act on it. */
  private part?: SelectedPart;

  /** A click without dragging on the selected element: the part under the pointer, or the whole element. */
  private selectPartAt(id: string, p: { x: number; y: number }, tolerance: number): void {
    try {
      const parts = this.sessionObj.parts(id, lang());
      if (!parts.length) return;
      const key = this.sessionObj.partAt(id, p.x, p.y, tolerance);
      const info = key ? parts.find((x) => x.key === key) : undefined;
      this.setPart(info ? { id, key: info.key, label: info.label } : undefined);
    } catch (e) {
      this.report(e);
    }
  }

  private setPart(part: SelectedPart | undefined): void {
    this.part = part;
    try {
      this.stage.setPartOutline(part ? this.sessionObj.partOutline(part.id, part.key) : undefined);
    } catch (e) {
      this.report(e);
    }
    // The grips of a selected curve's points come with it.
    this.stage.drawOverlay();
    this.updateBar();
  }

  /** After an edit: keep the part while its element stays selected alone, refresh its outline. */
  private refreshPart(): void {
    if (!this.part) return;
    const next = keepPart(this.part, this.st.selection, () => this.sessionObj.parts(this.part!.id, lang()));
    this.part = next;
    try {
      this.stage.setPartOutline(next ? this.sessionObj.partOutline(next.id, next.key) : undefined);
    } catch (e) {
      this.report(e);
    }
  }

  /** Part mode commands: the parts list, choosing a part from it, leaving the part. */
  private partCommand(id: string, clientX: number, clientY: number): void {
    const part = this.part;
    const sel = this.st.selection;
    if (id === "part:none") return this.setPart(undefined);
    if (id === "partlist" || id === "menu:parts") {
      if (sel.length !== 1) return;
      try {
        const entries = partListEntries(this.sessionObj.parts(sel[0], lang()), part?.key, t("part.hidden"));
        this.bar.hide();
        this.showSubmenu("parts", entries, t("part.title"), this.pie.open ? undefined : { x: clientX, y: clientY });
      } catch (e) {
        this.report(e);
      }
      return;
    }
    if (id.startsWith("partsel:") && sel.length === 1) {
      const key = id.slice(8);
      try {
        const info = this.sessionObj.parts(sel[0], lang()).find((x) => x.key === key);
        if (info) this.setPart({ id: sel[0], key, label: info.label });
      } catch (e) {
        this.report(e);
      }
    }
  }

  private barCommand(id: string, clientX: number, clientY: number): void {
    const sel = this.st.selection;
    if (id === "insert") return this.openInsert({ x: clientX, y: clientY });
    if (id === "makeIt") return this.openMakeIt({ x: clientX, y: clientY });
    if (id === "trace") return this.drawPath();
    if (id === "text") return this.editText();
    if (id === "values") return this.openValues();
    if (id === "format") return this.openInspectorStyle();
    if (id === "rotm90") { this.edit((s) => s.rotate(sel, -90)); return; }
    if (id.startsWith("info:")) return;
    if (id.startsWith("corner:")) {
      const corner = this.stage.corner;
      this.stage.clearCorner();
      if (!corner) return this.updateBar();
      const what = id.slice(7);
      this.edit((s) => (what === "remove" ? s.removeCorner(corner.id, corner.index) : s.splitLine(corner.id, corner.index)));
      this.stage.el.focus({ preventScroll: true });
      return;
    }
    if (id === "sketch" && sel.length === 1) {
      const chart = sel[0];
      this.bar.hide();
      this.setStatus(t("sketch.hint"));
      this.stage.startSketch((points) => {
        try {
          const r = this.sessionObj.sketchCurve(chart, points, lang());
          this.apply(r.state, true);
          this.setStatus(r.message);
        } catch (e) {
          this.report(e);
        }
      });
      return;
    }
    if (isPartCommand(id)) return this.partCommand(id, clientX, clientY);
    if (id === "motion") {
      this.motion = !this.motion;
      this.stage.drawOverlay();
      this.updateBar();
      this.setStatus(this.motion ? t("motion.on") : "");
      this.stage.el.focus({ preventScroll: true });
      return;
    }
    if (id === "ghosts" && sel.length === 1) {
      this.edit((s) => s.drawGhosts(sel[0]));
      return;
    }
    if (id === "explode" && sel.length === 1) {
      this.edit((s) => s.explode(sel[0]));
      return;
    }
    if (id === "assembly") {
      try {
        this.apply(this.sessionObj.select(this.sessionObj.assembly(sel)));
      } catch (e) {
        this.report(e);
      }
      return;
    }
    if (id.startsWith("arr:")) {
      this.edit((s) => s.arrange(sel, id.slice(4)));
      this.stage.el.focus({ preventScroll: true });
    }
  }

  /** Library items the box may offer (hidden modules left out). */
  private itemLookup(): (id: string) => CatalogItem | undefined {
    const map = new Map(this.visibleCatalog(this.catalog).items.map((i) => [i.id, i]));
    return (id) => map.get(id);
  }

  /** Pointer over the stage in client coordinates (insert box by keyboard), else the middle of the stage. */
  private pointerClient(): { x: number; y: number } {
    const p = this.stage.lastPointerPage();
    if (p) return this.stage.toClient(p);
    const r = this.stage.el.getBoundingClientRect();
    return { x: r.left + r.width / 2, y: r.top + r.height / 3 };
  }

  private remember(itemId: string): void {
    this.settings.recent = rememberRecent(this.settings.recent ?? [], itemId);
    saveSettings(this.settings);
  }

  /**
   * The insert box at the pointer: parts that fit the selection first, then recent items; typing searches the whole
   * library. The chosen item follows the pointer with snapping (click places it, Esc cancels).
   */
  private openInsert(at?: { x: number; y: number }): void {
    const sel = this.st.selection.filter((id) => this.objectById(id)?.known);
    const item = this.itemLookup();
    if (this.pie.open) this.pie.close();
    this.bar.hide();
    this.insertBox.show({
      at: at ?? this.pointerClient(),
      label: t("ins.title"),
      rows: (text) => insertRows(this.sessionObj.search(sel, text, lang(), this.settings.recent ?? [], 30), this.settings.recent ?? [], item),
      choose: (id) => {
        const chosen = item(id);
        if (!chosen) return;
        this.remember(id);
        if (sel.length === 1 && !(this.sync instanceof ComSync)) {
          try {
            const res = this.sessionObj.insertFitting(sel[0], id);
            if (res.placed) {
              this.apply(res.state, true);
              return;
            }
          } catch (e) {
            this.report(e);
          }
        }
        this.startInsert(chosen, false);
      },
      closed: () => this.updateBar(),
    });
  }

  /**
   * The insert box at a build "+" (like the COM add-in): the parts that dock at that port first (`buildItems`); a pick
   * among them docks it there, coupled and selected (`buildPlace`). Typing searches the whole library; anything else
   * starts the normal placement at the pointer.
   */
  private openPortInsert(id: string, port: BuildPortInfo, at: { x: number; y: number }): void {
    let fitting: SuggestionInfo[] = [];
    try {
      fitting = this.sessionObj.buildItems(id, port.id, lang(), 12);
    } catch (e) {
      this.report(e);
    }
    const docks = new Set(fitting.map((f) => f.id));
    const item = this.itemLookup();
    const recent = () => this.settings.recent ?? [];
    if (this.pie.open) this.pie.close();
    this.bar.hide();
    this.insertBox.show({
      at,
      label: t("ins.port"),
      rows: (text) => text.trim()
        ? insertRows(this.sessionObj.search([id], text, lang(), recent(), 30).map((r) => (docks.has(r.id) ? { ...r, fits: true } : r)), recent(), item)
        : insertRows(fitting, recent(), item),
      choose: (itemId) => {
        const chosen = item(itemId);
        if (!chosen) return;
        this.remember(itemId);
        if (docks.has(itemId)) this.edit((s) => s.buildPlace(id, port.id, itemId));
        else this.startInsert(chosen, false);
      },
      closed: () => this.updateBar(),
    });
  }

  /** "Make it …" after a quick add: what the new element can become first, typing searches the library; picks replace it. */
  private openMakeIt(at: { x: number; y: number }): void {
    const target = this.nextFor;
    if (!target) return;
    const choices = this.nextChoices;
    const item = this.itemLookup();
    this.bar.hide();
    this.insertBox.show({
      at,
      label: t("ins.makeIt"),
      rows: (text) => text.trim()
        ? insertRows(this.sessionObj.search([target], text, lang(), this.settings.recent ?? [], 30), this.settings.recent ?? [], item)
        : insertRows(choices, this.settings.recent ?? [], item),
      choose: (id) => {
        this.remember(id);
        this.edit((s) => s.replaceItem(target, id));
        this.nextFor = this.st.selection.length === 1 ? this.st.selection[0] : undefined;
        this.updateBar();
      },
      closed: () => this.updateBar(),
    });
  }

  /** Inserts the coupler curve of the selected link as a motion path (kin.path / kin.path.closed with the traced points). */
  private drawPath(): void {
    const traced = this.traceFor;
    if (!traced) return;
    try {
      this.apply(this.sessionObj.drawTrace(traced.id), true);
      this.toast(t("trace.done", traced.trace.points.length));
    } catch (e) {
      this.report(e);
      this.guard(() => this.apply(this.sessionObj.state(), true));
    }
  }

  /**
   * An option of a choice was picked (bar dropdown or pie): line type, line style, parameter, view, or the option action
   * of an element choice ("@a.") or of the selected part's choice ("@p."); input options ask for their value first.
   */
  private pickChoice(key: string, value: string): void {
    const sel = this.st.selection;
    const pick = choicePick(key, value);
    switch (pick.kind) {
      case "convert": return this.edit((s) => s.convert(sel, pick.typeId));
      case "dash": return this.edit((s) => s.setStyle(sel, { dash: pick.dash as DashStyle }));
      case "param": return this.edit((s) => s.setParam(sel, pick.key, pick.value));
      case "view":
      case "action": {
        let a: ActionInfo | undefined;
        try { a = this.sessionObj.contextActions(sel, lang()).find((x) => x.id === pick.id); } catch (e) { this.report(e); }
        if (a) this.runAction(a);
        return;
      }
      case "part": {
        const part = this.partFor(sel);
        if (!part) return;
        let a: ActionInfo | undefined;
        try { a = this.sessionObj.partActions(part.id, part.key, lang()).find((x) => x.id === pick.id); } catch (e) { this.report(e); }
        if (a) this.runAction({ ...a, id: PART_PREFIX + a.id });
      }
    }
  }

  /** Select connected: what is connected with `ids` by `mode` ("auto": the most meaningful kind for the element). */
  private selectConnected(ids: string[], mode: string): void {
    try {
      const found = this.sessionObj.connected(ids, mode);
      if (found.length > ids.length || found.some((id) => !ids.includes(id))) {
        this.apply(this.sessionObj.select(found));
        this.setStatus(t("connect.done", found.length));
      } else this.setStatus(t("connect.none"));
    } catch (e) {
      this.report(e);
    }
  }

  /** The value level of the drawing (Quick sketch, Explore, Engineer): a document setting, one undo step. */
  private setValueLevel(level: ValueLevel): void {
    this.edit((s) => s.setValueLevel(level), false);
  }

  /**
   * The ⚠ badge of element `id` (Explore and Engineer), cached per revision: "conflict" when its entered values conflict,
   * "warning" when a plausibility check that is not muted warns about it (as the COM add-in), else "".
   */
  private alertCache: { key: string; value: "conflict" | "warning" | "" } = { key: "", value: "" };

  private valueAlert(id: string): "conflict" | "warning" | "" {
    const level = this.st.valueLevel ?? "quick";
    if (level === "quick") return "";
    const key = `${id}@${this.st.revision}@${level}`;
    if (this.alertCache.key !== key) {
      let value: "conflict" | "warning" | "" = "";
      try {
        const status = this.sessionObj.values(id, lang()).objects.find((o) => o.id === id)?.status;
        value = badgeKind(status, status === "conflict" ? [] : this.sessionObj.checks([id], lang()), id);
      } catch {
        value = "";
      }
      this.alertCache = { key, value };
    }
    return this.alertCache.value;
  }

  private showsValues(id: string): boolean | undefined {
    try {
      const p = this.sessionObj.inspector([id], lang()).params.find((x) => x.key === "showValues");
      return p ? (p.value ?? p.default) === true : undefined;
    } catch {
      return undefined;
    }
  }

  /** The value card of the selected element, right of it; `focusKey` puts the cursor into that value. */
  private openValues(focusKey?: string): void {
    const sel = this.st.selection;
    const obj = sel.length === 1 ? this.objectById(sel[0]) : undefined;
    if (!obj?.hasValues) return;
    const box = this.stage.selectionScreenBox();
    this.bar.hide();
    this.valueCard.show(obj.id, obj.name, box ? { x: box.right, y: box.top } : this.pointerClient(), focusKey);
  }

  /** Element just added by quick add: the bar offers the next-element palette for it. */
  private nextFor?: string;

  private quickAdd(id: string, side: ChainSide, at?: { x: number; y: number }): void {
    this.edit((s) => s.quickAdd(id, side, at?.x, at?.y));
    this.nextFor = this.st.selection.length === 1 ? this.st.selection[0] : undefined;
    this.updateBar();
  }

  private edit(f: (s: Session) => EditorState, live = true): void {
    try {
      this.apply(f(this.sessionObj), live);
    } catch (e) {
      this.report(e);
    }
  }

  private guard(f: () => void): void {
    try { f(); } catch (e) { this.report(e); }
  }

  runAction(a: ActionInfo): void {
    if (!a.enabled) return;
    const partAction = partActionId(a.id);
    const part = this.part;
    if (partAction !== undefined && part) {
      if (partAction === "none") return this.partCommand(a.id, 0, 0);
      const run = (input?: string) => this.runPartAction(part, partAction, !!a.choiceMenu, input);
      if (a.input) {
        const input = a.input;
        this.bar.hide();
        this.prompt.show({ title: a.label, label: input.label, value: input.value, unit: input.unit }, this.promptAnchor(), (value) => {
          run(value);
          this.stage.el.focus({ preventScroll: true });
        });
        return;
      }
      run();
      this.stage.el.focus({ preventScroll: true });
      return;
    }
    if (a.id === "text") return this.editText();
    const sel = this.st.selection;
    if (a.input) {
      // Input actions ask for their value inline next to the selection (Esc cancels without a change).
      const input = a.input;
      this.bar.hide();
      this.prompt.show({ title: a.label, label: input.label, value: input.value, unit: input.unit }, this.promptAnchor(), (value) => {
        this.edit((s) => s.applyAction(sel, a.id, value));
        this.stage.el.focus({ preventScroll: true });
      });
      return;
    }
    this.edit((s) => s.applyAction(sel, a.id));
    this.stage.el.focus({ preventScroll: true });
  }

  /**
   * Runs a part action (one undo step). A deleted point leaves its curve selected, any other deleted part the whole
   * element; a menu option that adds a part ("Fill below ▸ //") selects the new part, so its choices are right there.
   */
  private runPartAction(part: SelectedPart, action: string, menu: boolean, input?: string): void {
    const keys = () => { try { return this.sessionObj.parts(part.id, lang()).map((p) => p.key); } catch { return []; } };
    const before = menu ? keys() : [];
    try {
      const next = this.sessionObj.applyPartAction(part.id, part.key, action, input);
      if (action === "part.delete") {
        const after = partAfterDelete(part.key);
        this.part = after ? { id: part.id, key: after, label: after } : undefined;
      } else if (menu) {
        const added = addedPart(before, keys());
        if (added) this.part = { id: part.id, key: added, label: added };
      }
      this.apply(next, true);
      if (this.part) this.setPart(keepPart(this.part, this.st.selection, () => this.sessionObj.parts(part.id, lang())));
    } catch (e) {
      this.report(e);
    }
  }

  /** Where inline prompts open: below the selection, else in the middle of the stage (client px). */
  private promptAnchor(): { x: number; y: number } {
    const box = this.stage.selectionScreenBox();
    const r = this.stage.el.getBoundingClientRect();
    return box ? { x: (box.left + box.right) / 2, y: box.bottom } : { x: r.left + r.width / 2, y: r.top + r.height / 2 };
  }

  /** Submenus of the open pie (place groups, choices, paged continuations) and their titles. */
  private pieMenus = new Map<string, PieEntry[]>();
  private pieTitles = new Map<string, string>();
  /** Every choice the open pie can open ("menu:c:<key>"). */
  private pieChoices: ChoiceEntry[] = [];
  private pieLabels: { entry: PieEntry; change?: LabelChange }[] = [];
  /** "Select connected" kinds of the open pie. */
  private pieConnect: ConnectOption[] = [];
  /** Other slides with the selected element's designation (1-based numbers). */
  private pieAlso: string[] = [];

  /** "Same name: also on slides 2, 4". */
  private alsoOnText(): string {
    return this.pieAlso.length === 1 ? t("connect.alsoOn1", this.pieAlso[0]) : t("connect.alsoOn", this.pieAlso.join(", "));
  }

  private menuEntry(id: string, label: string, icon?: string, hint?: string, extra: Partial<PieEntry> = {}): PieEntry {
    return { id, label, icon, enabled: true, active: false, hint, ...extra };
  }

  /** The pie entry of a choice: "Label: Current ▸" (or "Label ▸" for a menu) with the current option's picture. */
  private choicePieEntry(c: ChoiceEntry): PieEntry {
    const cur = currentOption(c);
    const names = c.options.map((o) => o.label);
    return {
      id: `menu:c:${c.key}`, label: choicePieLabel(c), icon: cur?.icon, image: cur?.icon ? undefined : cur?.image, mirror: c.mirror, enabled: true, active: false,
      hint: t("pie.choose", names.slice(0, 4).join(", ") + (names.length > 4 ? ", …" : "")),
    };
  }

  /** An engine action as a pie entry: its pictogram only when it is telling (label-only markers show the text alone). */
  private actionPieEntry(a: ActionInfo, hint?: string): PieEntry {
    const e = entryOf(a);
    return { ...e, icon: isTelling(a.icon) || a.icon === "delete" || a.icon === "cmd-delete" ? a.icon : undefined, hint: hint ?? (a.input ? t("pie.hint.asks") : undefined) };
  }

  /**
   * The eight places of the pie for the selection (docs/en/design/interaction-ux.md section 6, COM `PieGroups`): Text,
   * Edit ▸ (the element's own commands; a selected part's commands), Insert, Duplicate, Style ▸ (one entry per choice),
   * Delete, View ▸ (the views), Arrange ▸ (rotate, mirror, order, grouping, select connected).
   */
  private pieGroups(sel: string[]): PieGroup[] {
    const m = this.elementModel(sel);
    const obj = m.obj;
    const part = this.partFor(sel);
    const entry = (id: string, label: string, iconId?: string, hint?: string, extra: Partial<PieEntry> = {}) => this.menuEntry(id, label, iconId, hint, extra);
    this.pieChoices = [...m.paramChoices, ...m.actionChoices];
    this.pieLabels = [];

    const text = this.mainTextTarget() ? [entry("text", t("pie.text"), "cmd-text", t("pie.hint.text"))] : [];

    const edit: PieEntry[] = [];
    let partStyle: PieEntry[] | undefined;
    if (part) {
      let acts: ActionInfo[] = [];
      try { acts = this.sessionObj.partActions(part.id, part.key, lang()); } catch (e) { this.report(e); }
      partStyle = [];
      for (const e of actionEntries(acts, PART_CHOICE, svgDataUrl)) {
        if ("choice" in e) {
          this.pieChoices.push(e.choice);
          (e.choice.menu ? edit : partStyle).push(this.choicePieEntry(e.choice));
        } else edit.push(this.actionPieEntry({ ...e.action, id: PART_PREFIX + e.action.id }));
      }
      edit.push(entry("part:none", t("part.whole"), "cmd-select-assembly", t("pie.hint.whole")));
    } else {
      if (this.nextFor && this.nextChoices.length >= 2) edit.push(entry("makeIt", t("ctx.makeIt"), "cmd-cycle", t("pie.hint.makeIt")));
      if (obj?.hasValues) edit.push(entry("menu:values", t("pie.values"), "cmd-values", t("pie.hint.values")));
      if (m.conversions.length && m.info) {
        this.pieLabels = labelEntries(m.info.params, { larger: t("pie.textLarger"), smaller: t("pie.textSmaller") });
        for (const p of m.info.params)
          if (p.kind === "choice" && p.options && p.options.length > 1 && !this.pieChoices.some((c) => c.key === p.key))
            this.pieChoices.push(this.paramChoice(p, p.options.every((o) => o.icon && hasIcon(o.icon))));
        if (this.pieLabels.length) edit.push(entry("menu:label", t("pie.label"), "cmd-label-show", t("pie.hint.label")));
      }
      // Actions that only set a choice shown under Style stay there (one entry per thing).
      edit.push(...m.plain.filter((a) => a.kind === "type").map((a) => this.actionPieEntry(a)));
      const numeral = m.actions.find((a) => a.id === "numeral");
      if (numeral) edit.push(this.actionPieEntry(numeral));
      edit.push(...m.actionChoices.filter((c) => c.menu).map((c) => this.choicePieEntry(c)));
      if (obj?.known) {
        const info = this.dofOf(sel);
        const trace = this.traceOf(sel);
        if (info?.explodable) edit.push(entry("explode", t("ctx.explode"), "cmd-explode", t("pie.hint.explode")));
        if (obj.type === "chart.xy" || obj.type === "chart.figure") edit.push(entry("sketch", t("pie.sketch"), "cmd-pencil", t("pie.hint.sketch")));
        if (canMove(this.stage.motionOf(obj.id))) edit.push(entry("motion", t("pie.motion"), "cmd-motion", t("ctx.motion"), { active: this.motion }));
        if (info?.dof != null) edit.push(entry("info:dof", `F = ${info.dof}`, undefined, t("ctx.dof", info.dof)));
        if (canTrace(trace)) { this.traceFor = { id: obj.id, trace }; edit.push(entry("trace", t("pie.trace"), "cmd-trace", t("ctx.trace"))); }
        if (canDrawGhosts(obj.kind, info?.dof, trace)) edit.push(entry("ghosts", t("pie.ghosts"), "cmd-ghosts", t("ctx.ghosts")));
      }
    }
    if (obj?.known) {
      let parts = 0;
      try { parts = this.sessionObj.parts(obj.id, lang()).length; } catch { parts = 0; }
      if (parts) edit.push(entry("menu:parts", t("part.parts"), "view-table", t("pie.hint.parts")));
    }

    const insert = m.known ? [entry("insert", t("pie.insert"), "cmd-insert", t("pie.hint.insert"))] : [];
    const by = (id: string) => m.actions.find((a) => a.id === id);
    const dupAction = by("duplicate");
    const dup = dupAction ? [this.actionPieEntry(dupAction, t("pie.hint.duplicate"))] : [];
    const delAction = by("delete");
    const del = delAction ? [{ ...this.actionPieEntry(delAction, t("pie.hint.delete")), danger: true }] : [];

    const style = partStyle ?? [...m.paramChoices.filter((c) => c.key !== "@view"), ...m.actionChoices.filter((c) => !c.menu)].map((c) => this.choicePieEntry(c));
    if (obj?.known && !part) style.push(entry("format", t("pie.format"), "style", t("pie.hint.format")));

    const views = m.actions.filter((a) => a.kind === "view");
    const view = views.length > 1 ? views.map((v) => this.actionPieEntry(v, t("pie.hint.view"))) : [];

    const arrange: PieEntry[] = [];
    const rot = by("rotate90");
    if (rot) {
      arrange.push(this.actionPieEntry(rot, t("pie.hint.rotate")));
      arrange.push(entry("rotm90", t("pie.rotateBack"), "cmd-rotate-ccw", t("pie.hint.rotateBack")));
    }
    for (const id of ["flipH", "flipV"]) { const a = by(id); if (a) arrange.push(this.actionPieEntry(a, t(`pie.hint.${id}`))); }
    for (const id of ["front", "back", "group", "ungroup"]) { const a = by(id); if (a?.enabled) arrange.push(this.actionPieEntry(a)); }
    this.pieConnect = [];
    this.pieAlso = [];
    if (m.known) {
      try { this.pieConnect = this.sessionObj.connectOptions(sel, lang()); } catch { this.pieConnect = []; }
      // The same designation on other slides (COM "Same name: also on slides 2, 4").
      if (sel.length === 1) try { this.pieAlso = alsoOn(this.elsewhere, designationOf(this.sessionObj.designations(), sel[0])?.key); } catch { this.pieAlso = []; }
      if (this.pieConnect.length || this.pieAlso.length)
        arrange.push(entry("menu:connected", t("pie.connected"), "cmd-select-assembly", [...this.pieConnect.map((c) => c.label), ...(this.pieAlso.length ? [this.alsoOnText()] : [])].join(", ")));
    }

    return [
      { key: "text", label: t("pie.text"), icon: "cmd-text", items: text, hint: t("pie.hint.text") },
      { key: "edit", label: t("pie.edit"), icon: "inspector", items: edit, alwaysMenu: true },
      { key: "insert", label: t("pie.insert"), icon: "cmd-insert", items: insert, hint: t("pie.hint.insert") },
      { key: "duplicate", label: t("pie.duplicate"), icon: "duplicate", items: dup, hint: t("pie.hint.duplicate") },
      { key: "style", label: t("pie.style"), icon: "style", items: style, alwaysMenu: true },
      { key: "delete", label: t("pie.delete"), icon: "delete", items: del, hint: t("pie.hint.delete") },
      { key: "view", label: t("pie.view"), icon: "view", items: view, alwaysMenu: views.length > 1 },
      { key: "arrange", label: t("pie.arrange"), icon: "front", items: arrange, alwaysMenu: true },
    ];
  }

  /** What the pie names in its centre: the selected part, the element, or the number of elements. */
  private pieTitle(sel: string[]): string {
    const part = this.partFor(sel);
    if (part) return part.label;
    const n = elementCount(this.st.objects, sel);
    if (n >= 2) return t("pie.elements", n);
    return this.objectById(sel[0])?.name ?? "";
  }

  /** A hovered pie entry: "Select connected" kinds preview what they would add with a fainter glow. */
  private hoverPie(e: PieEntry | undefined): void {
    const kind = e?.id.startsWith("connect:") ? e.id.slice(8) : undefined;
    this.stage.setHaloPreview(kind ? this.pieConnect.find((c) => c.kind === kind)?.ids ?? [] : []);
  }

  /** Shows a submenu ring (Back first), paged into "More ▸" when long. */
  private showSubmenu(key: string, items: PieEntry[], title: string, where?: { x: number; y: number }): void {
    const at = where ?? this.pie.at ?? this.pointerClient();
    addMenu(this.pieMenus, key, items, t("pie.more"));
    this.pieTitles.set(key, title);
    this.pie.show(withBack(this.backEntry(), this.pieMenus.get(key) ?? []), at.x, at.y, title);
  }

  private backEntry(): PieEntry {
    return this.menuEntry("menu:back", t("pie.back"), undefined, t("pie.hint.back"));
  }

  /**
   * The pie menu (right-click, Shift+F10, "⋯"): eight fixed labelled places for every element, the element's name in the
   * centre; submenus open by click and start with "← Back".
   */
  openPie(clientX?: number, clientY?: number): void {
    const sel = this.st.selection;
    if (!sel.length) return;
    if (clientX === undefined || clientY === undefined) {
      const box = this.stage.selectionScreenBox();
      const r = this.stage.el.getBoundingClientRect();
      clientX = box ? (box.left + box.right) / 2 : r.left + r.width / 2;
      clientY = box ? (box.top + box.bottom) / 2 : r.top + r.height / 2;
    }
    const { ring, menus, titles } = pieRing(this.pieGroups(sel), t("pie.more"));
    this.pieMenus = menus;
    this.pieTitles = titles;
    this.bar.hide();
    this.valueCard.close();
    this.pie.show(ring, clientX, clientY, this.pieTitle(sel));
  }

  private pickPie(e: PieEntry): void {
    const at = this.pie.at ?? { x: 0, y: 0 };
    const sel = this.st.selection;
    if (e.id === "menu:back") return this.openPie(at.x, at.y);
    if (e.id.startsWith("menu:g:")) {
      const key = e.id.slice(7);
      const items = this.pieMenus.get(key);
      if (items) this.pie.show(withBack(this.backEntry(), items), at.x, at.y, this.pieTitles.get(key.replace(/\+$/, "")) ?? this.pieTitles.get(key));
      return;
    }
    if (e.id.startsWith("menu:c:")) {
      const key = e.id.slice(7);
      const c = this.pieChoices.find((x) => x.key === key);
      if (!c) return;
      const options = c.options.map((o): PieEntry => ({
        id: `pick:${key}:${o.id}`, label: o.label, icon: o.icon, image: o.icon ? undefined : o.image, mirror: c.mirror,
        enabled: true, active: !c.menu && o.id === c.current, hint: c.label,
      }));
      return this.showSubmenu(`c-${key}`, options, c.label);
    }
    if (e.id.startsWith("pick:")) {
      const rest = e.id.slice(5);
      const cut = rest.indexOf(":");
      if (cut > 0) this.pickChoice(rest.slice(0, cut), rest.slice(cut + 1));
      return;
    }
    if (e.id === "menu:connected") {
      const items = this.pieConnect.map((c) => this.menuEntry(`connect:${c.kind}`, `${c.label} (${c.ids.length})`, "cmd-select-assembly", c.hint));
      if (this.pieAlso.length) items.push(this.menuEntry("info:samename", this.alsoOnText(), undefined, t("connect.alsoOnHint")));
      return this.showSubmenu("connected", items, t("pie.connectedTitle"));
    }
    if (e.id.startsWith("connect:")) {
      const option = this.pieConnect.find((c) => c.kind === e.id.slice(8));
      if (option) {
        this.apply(this.sessionObj.select(option.ids));
        this.setStatus(t("connect.done", option.ids.length));
      }
      return;
    }
    if (e.id === "menu:values") {
      const obj = sel.length === 1 ? this.objectById(sel[0]) : undefined;
      if (!obj) return;
      const items: PieEntry[] = [];
      try {
        const level = this.st.valueLevel ?? "quick";
        const vals = this.sessionObj.values(obj.id, lang()).objects.find((o) => o.id === obj.id);
        for (const v of visibleVars(vals?.vars ?? [], level)) {
          const derived = v.provenance === "derived" || v.provenance === "linked";
          items.push(this.menuEntry(`val:${v.key}`, `${v.name} ${v.symbol}${v.display ? (derived ? ` = ${v.display}` : ` … ${v.display}`) : " …"}`, undefined, derived ? v.formula ?? undefined : t("pie.hint.enterValue")));
        }
      } catch (err) {
        this.report(err);
      }
      const shows = this.showsValues(obj.id);
      if (shows !== undefined) items.push(this.menuEntry("valshow", t("val.show"), "cmd-label-show", t("pie.hint.valshow"), { active: shows }));
      if (items.length) items.push(this.menuEntry("valsheet", t("val.sheet"), undefined, t("sheet.hint")));
      return this.showSubmenu("values", items, t("val.title"));
    }
    if (e.id.startsWith("val:")) return this.openValues(e.id.slice(4));
    if (e.id === "valsheet" && sel.length === 1) return this.calcSheet.show([sel[0]], at);
    if (e.id === "valshow") {
      const id = sel[0];
      const shows = this.showsValues(id);
      if (shows !== undefined) this.edit((s) => s.setParam([id], "showValues", !shows));
      return;
    }
    if (e.id === "insert") return this.openInsert(at);
    if (e.id === "menu:parts") {
      if (sel.length !== 1) return;
      try {
        const entries = partListEntries(this.sessionObj.parts(sel[0], lang()), this.part?.key, t("part.hidden"));
        this.showSubmenu("parts", entries, t("part.title"));
      } catch (err) {
        this.report(err);
      }
      return;
    }
    if (e.id.startsWith("partsel:") || e.id === "part:none") return this.partCommand(e.id, at.x, at.y);
    if (e.id === "menu:label") return this.showSubmenu("label", this.pieLabels.map((l) => l.entry), t("pie.labelTitle"));
    if (["makeIt", "explode", "sketch", "motion", "trace", "ghosts", "rotm90", "format", "values"].includes(e.id) || e.id.startsWith("info:"))
      return this.barCommand(e.id, at.x, at.y);
    if (e.id.startsWith("lbl:")) {
      const change = this.pieLabels.find((l) => l.entry.id === e.id)?.change;
      if (!change) return;
      const sel = this.st.selection;
      if (change.prompt) {
        const prompt = change.prompt;
        this.prompt.show({ title: t("pie.label"), label: prompt.label, value: prompt.value }, this.promptAnchor(), (value) => {
          this.edit((s) => s.setParam(sel, change.key, value));
        });
        return;
      }
      return this.edit((s) => s.setParam(sel, change.key, change.value ?? null));
    }
    if (e.id === "text") return this.editText();
    if (e.action) this.runAction(e.action);
  }

  /** Hidden modules (set in the COM add-in): their categories and items leave the library. */
  private hiddenModules = new Set<string>();

  private hideModules(ids: string[]): void {
    this.hiddenModules = new Set(ids);
    if (this.catalog) this.library.setCatalog(this.visibleCatalog(this.catalog));
  }

  private visibleCatalog(c: Catalog): Catalog {
    if (this.hiddenModules.size === 0) return c;
    const categories = c.categories.filter((cat) => !this.hiddenModules.has(cat.module));
    const keep = new Set(categories.map((cat) => cat.id));
    return { ...c, categories, items: c.items.filter((i) => keep.has(i.category)) };
  }
  private openInspectorStyle(): void {
    this.settings.inspectorOpen = true;
    saveSettings(this.settings);
    this.layoutPanelsAfterChange();
    this.inspector.update(true);
    this.inspector.focusStyle();
  }

  private layoutPanelsAfterChange(): void {
    this.syncPanels();
    const head = this.inspPanel.querySelector(".isk-panel-head");
    if (head) {
      head.setAttribute("aria-expanded", "true");
      const chev = head.querySelector(".isk-chev");
      if (chev) chev.textContent = "▾";
    }
  }

  /** First selected object with an editable main text (engine `mainText`), preferring `prefer`. */
  private mainTextTarget(prefer?: string): ObjectInfo | undefined {
    const byId = new Map(this.st.objects.map((o) => [o.id, o]));
    if (prefer && this.st.selection.includes(prefer) && byId.get(prefer)?.mainText) return byId.get(prefer);
    return this.st.selection.map((id) => byId.get(id)).find((o) => o?.mainText);
  }

  /**
   * Edits the main text (object text, numeral, text/label/name/title parameter) in the inline prompt: double-click on
   * the stage, the bar's text button or "Text …" in the pie menu. Multi-line texts open the inspector's text box.
   */
  private editText(prefer?: ObjectInfo): void {
    const target = this.mainTextTarget(prefer?.id);
    const main = target?.mainText;
    if (!target || !main) return;
    if (main.value.includes("\n")) {
      this.settings.inspectorOpen = true;
      this.layoutPanelsAfterChange();
      this.inspector.update(true);
      this.inspector.focusText();
      return;
    }
    this.bar.hide();
    this.prompt.show({ title: t("text.title"), label: main.label, value: main.value }, this.promptAnchor(), (value) => {
      if (value !== main.value) this.edit((s) => s.setMainText(target.id, value));
      this.stage.el.focus({ preventScroll: true });
    });
  }

  // ------------------------------------------------------------------ insert

  /** Starts insert at the pointer; `toggle` (library click) cancels when the same item is already pending. */
  private startInsert(item: CatalogItem, toggle = true): void {
    // Inside the COM add-in: a library click places the element directly on the slide (insert at pointer with
    // snapping, like the ribbon galleries); dragging into the stage still adds it to the editor.
    if (this.sync instanceof ComSync) {
      this.sync.place(item.id);
      this.setStatus(t("com.placing", item.name));
      return;
    }
    this.pending = toggle && this.pending?.id === item.id ? undefined : item;
    this.library.renderItems();
    this.setStatus(this.pending ? t("lib.placing", item.name) : "");
    this.stage.el.focus({ preventScroll: true });
    const p = this.stage.lastPointerPage();
    if (!this.pending) this.stage.clearGhost();
    else if (p) this.stage.dragOver(item, this.stage.toClient(p).x, this.stage.toClient(p).y);
  }

  private cancelInsert(): void {
    if (!this.pending) return;
    this.pending = undefined;
    this.stage.clearGhost();
    this.library.renderItems();
    this.setStatus("");
  }

  private insertAt(item: CatalogItem, p: { x: number; y: number }): void {
    this.cancelInsert();
    this.edit((s) => s.insert(item.id, p.x, p.y, this.settings.snap, this.stage.tolerance()));
  }

  private bindDrop(): void {
    const el = this.stage.el;
    el.addEventListener("dragover", (e) => {
      if (!this.dragging) return;
      e.preventDefault();
      if (e.dataTransfer) e.dataTransfer.dropEffect = "copy";
      this.stage.dragOver(this.dragging, e.clientX, e.clientY);
    });
    el.addEventListener("dragleave", () => this.stage.clearGhost());
    el.addEventListener("drop", (e) => {
      e.preventDefault();
      const id = e.dataTransfer?.getData("text/plain");
      const item = this.dragging ?? this.catalog.items.find((i) => i.id === id);
      this.dragging = undefined;
      if (item) this.stage.drop(item, e.clientX, e.clientY);
    });
  }

  // ------------------------------------------------------------------ office

  private async insertIntoSlide(): Promise<void> {
    if (!this.sync) return;
    const sel = this.st.selection;
    try {
      this.setStatus(t("office.inserting"));
      const units = await this.sync.insert(sel.length ? sel : undefined);
      const svg = units.filter((u) => u.mode === "svg").length;
      this.toast(t("office.inserted", units.length, svg));
      if (svg) console.info("InventaSketch picture fallback:", [...new Set(units.flatMap((u) => u.notes))]);
      this.setStatus(svg ? t("office.svgNote") : "");
    } catch (e) {
      this.report(e);
    }
  }

  /** Office selection-changed handler (also tells a slide change: the continuation map is read again then). */
  onOfficeSelectionChanged(): void {
    this.sync?.loadSelection().catch((e) => this.report(e));
    if (this.slideAccess) void this.refreshContinuations();
  }

  /** Designations on the other slides (comparison key → 1-based slide numbers), for the slide shown. */
  private elsewhere: Record<string, string[]> = {};
  private elsewhereSlide?: string;
  private continuationsBusy = false;

  /**
   * Designations across slides (interaction-ux.md section 6b): when the task pane opens and when another slide is shown,
   * the designations of every other slide's units are read (one temporary engine session per slide) and passed to the
   * editor session (`setContinuations`), so lines drawn from it carry continuation markers ("→ W3, slide 4") and "Select
   * connected" reports the same name on other slides. Office.js reads every slide's shapes and tags for it, so it runs
   * only then, not after every edit.
   */
  private async refreshContinuations(): Promise<void> {
    if (this.continuationsBusy) return;
    this.continuationsBusy = true;
    try {
      const cur = await currentSlide(this.office);
      if (!cur || cur.id === this.elsewhereSlide) return;
      const slides = await readSlideUnits(this.office);
      const map = continuationMap(slideDesignations(this.engine, slides, { w: this.st.pageWidth, h: this.st.pageHeight }, lang()), cur.index);
      this.elsewhereSlide = cur.id;
      if (sameMap(this.elsewhere, map)) return;
      this.elsewhere = map;
      this.sessionObj.setContinuations(map, "slide");
    } catch (e) {
      console.warn("InventaSketch: designations of the other slides could not be read", e);
    } finally {
      this.continuationsBusy = false;
    }
  }

  // ------------------------------------------------------------------ keyboard

  private bindKeys(): void {
    this.root.addEventListener("keydown", (e) => {
      const target = e.target as HTMLElement;
      const typing = /^(INPUT|TEXTAREA|SELECT)$/.test(target.tagName) || target.isContentEditable;
      if (e.key === "Escape") {
        if (this.pie.open || this.prompt.open || this.insertBox.open) return;
        if (this.pending) { this.cancelInsert(); e.preventDefault(); return; }
        if (this.stage.dragging) { this.stage.cancelDrag(); this.updateBar(); e.preventDefault(); return; }
        if (!typing && this.st.selection.length) { this.apply(this.sessionObj.select([])); e.preventDefault(); }
        return;
      }
      if (typing || this.pie.open || this.prompt.open || this.insertBox.open) return;
      const sel = this.st.selection;
      const ctrl = e.ctrlKey || e.metaKey;
      const k = e.key.toLowerCase();
      // Ctrl+Space: the insert box at the pointer (with or without a selection).
      if (ctrl && (e.code === "Space" || e.key === " ")) { e.preventDefault(); this.openInsert(); return; }
      const act = (id: string) => { e.preventDefault(); if (sel.length) this.edit((s) => s.applyAction(sel, id)); };
      if (ctrl && k === "z" && !e.shiftKey) { e.preventDefault(); this.edit((s) => s.undo()); }
      else if (ctrl && (k === "y" || (k === "z" && e.shiftKey))) { e.preventDefault(); this.edit((s) => s.redo()); }
      else if (ctrl && k === "d") act("duplicate");
      else if (ctrl && k === "g") act(e.shiftKey ? "ungroup" : "group");
      else if (ctrl && k === "a") { e.preventDefault(); this.apply(this.sessionObj.select(this.st.objects.map((o) => o.id))); }
      else if (!ctrl && (e.key === "Delete" || e.key === "Backspace")) {
        // A selected part: its own delete (a point, a curve, an area …), else it is hidden – never the whole element.
        const part = this.partFor(sel);
        if (part) {
          e.preventDefault();
          let acts: ActionInfo[] = [];
          try { acts = this.sessionObj.partActions(part.id, part.key, lang()); } catch (err) { this.report(err); }
          const id = partDeleteAction(acts);
          if (id) this.runPartAction(part, id, false);
          return;
        }
        act("delete");
      }
      else if (!ctrl && !e.altKey && k === "r") act("rotate90");
      else if (!ctrl && !e.altKey && k === "x") act("flipH");
      else if (!ctrl && !e.altKey && k === "y") act("flipV");
      else if ((e.shiftKey && e.key === "F10") || e.key === "ContextMenu") { e.preventDefault(); if (sel.length) this.openPie(); }
      else if (e.key.startsWith("Arrow") && sel.length) {
        e.preventDefault();
        const step = e.shiftKey ? Math.max(1, this.settings.grid || 5) : 0.5;
        const dx = e.key === "ArrowLeft" ? -step : e.key === "ArrowRight" ? step : 0;
        const dy = e.key === "ArrowUp" ? -step : e.key === "ArrowDown" ? step : 0;
        this.edit((s) => s.move(sel, dx, dy));
      } else if (!ctrl && (e.key === "+" || e.key === "=")) this.stage.zoomBy(1.25);
      else if (!ctrl && e.key === "-") this.stage.zoomBy(1 / 1.25);
      else if (!ctrl && e.key === "0") this.stage.fit();
    });
  }

  // ------------------------------------------------------------------ feedback

  private setStatus(text: string): void {
    this.status.textContent = text;
  }

  toast(text: string): void {
    this.toastEl.textContent = text;
    this.toastEl.hidden = false;
    clearTimeout(this.toastTimer);
    this.toastTimer = window.setTimeout(() => (this.toastEl.hidden = true), 3500);
  }

  report(e: unknown): void {
    const msg = e instanceof Error ? e.message : String(e);
    console.error(e);
    this.toast(`${t("error")}: ${msg}`);
  }
}
