// Task-pane application: top bar, library, stage, inspector, floating context bar and pie menu around one engine
// session. Runs inside PowerPoint (Office.js present) and standalone in a browser (dev.html).

import { Engine, Session } from "../engine/client";
import type { ActionInfo, ArrangeOption, BuildPortInfo, Catalog, CatalogItem, DashStyle, EditorState, ExporterInfo, ObjectInfo, PreflightResult, SuggestionInfo, TraceInfo, TransformEffect } from "../engine/types";
import { exportPng, exportSvg, download } from "../export";
import { lang, resolveLang, setLang, t, type Lang } from "../i18n";
import { drawCapabilities, noOffice, type HostCapabilities } from "../office/capabilities";
import { SlideSync } from "../office/sync";
import { ComSync, type ComSyncBridge } from "../office/combridge";
import { loadSettings, saveSettings, type Settings } from "../settings";
import { applyTheme, resolveTheme } from "../theme";
import { ContextBar, InlinePrompt, type BarCommand, type ChoiceEntry } from "./contextbar";
import { h, svgDataUrl } from "./dom";
import { hasIcon, icon } from "./icons";
import { canDrawGhosts } from "./buildlogic";
import { canMove } from "./motionlogic";
import { InsertBox, insertRows, rememberRecent } from "./insertbox";
import { Inspector } from "./inspector";
import { Library } from "./library";
import { PieMenu } from "./piemenu";
import { buildPieEntries, labelEntries, pieSplit, submenu, type LabelChange, type PieEntry } from "./pielogic";
import { Stage, type ChainSide } from "./stage";
import { assemblyExtends, canTrace, elementCount, filterTransforms, findingCounts, multiBarGroups, orderFindings, sourceLinks } from "./uxlogic";

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
    this.inspector = new Inspector({ session: () => this.sessionObj, state: () => this.st, apply: (s, live) => this.apply(s, live), report: (e) => this.report(e) });
    this.pie = new PieMenu(this.root, (e) => this.pickPie(e));
    this.prompt = new InlinePrompt(this.root);
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
    this.bar = new ContextBar(this.root, (a) => this.runAction(a), () => this.openInspectorStyle(), (x, y) => this.openPie(x, y),
      (id, x, y) => this.barCommand(id, x, y), (key, value) => this.pickChoice(key, value));
    this.loadCatalog();
    this.bindKeys();
    this.bindDrop();
    new ResizeObserver(() => this.updateLayoutMode()).observe(this.root);
    this.updateLayoutMode();
    this.apply(first);
    this.setStatus(opts.loadMillis !== undefined ? t("status.ready", Math.round(opts.loadMillis)) : "");
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
    const sel = st?.selection ?? [];
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
      this.lineGroupSelect(st?.lineGroup ?? 0.5),
      this.accentSelect(st?.accent ?? null),
      this.iconButton("check", t("cmd.patentCheck"), (e) => this.togglePatentCheck(e.currentTarget as HTMLElement), { "aria-haspopup": "dialog", "data-cmd": "patentcheck" }),
      this.iconButton("svg", t("cmd.exportSvg"), () => this.guard(() => exportSvg(this.sessionObj, sel.length ? sel : undefined, !!this.st.monochrome))),
      this.iconButton("png", t("cmd.exportPng"), () => exportPng(this.sessionObj, sel.length ? sel : undefined, !!this.st.monochrome).catch((e) => this.report(e))),
      h("span", { class: "isk-grow" }),
      this.iconButton("settings", t("cmd.settings"), (e) => this.toggleSettings(e.currentTarget as HTMLElement), { "aria-haspopup": "dialog" }),
    );
    this.topbar.replaceChildren(...items.filter((x): x is HTMLElement => !!x));
  }

  /** Black and white is a document setting (one undo step); live sync redraws every unit on the slide. */
  private setMonochrome(value: boolean): void {
    this.edit((s) => s.setMonochrome(value));
  }

  /** "Import data": a CSV/TSV table into the selected chart (or a new one), GeoJSON/GPX as map routes and markers. */
  private importButton(): HTMLElement {
    const input = h("input", { type: "file", accept: ".csv,.tsv,.txt,.geojson,.json,.gpx", hidden: true });
    input.addEventListener("change", async () => {
      const f = input.files?.[0];
      input.value = "";
      if (!f) return;
      const text = await f.text();
      const geo = /\.(gpx|geojson)$/i.test(f.name) || (/\.json$/i.test(f.name) && /"type"\s*:\s*"(Feature|FeatureCollection|LineString|Point|Polygon)/.test(text));
      this.edit((s) => (geo ? s.importGeo(text) : s.importData(text)));
    });
    const button = this.iconButton("load", t("cmd.import"), () => input.click(), { "data-cmd": "import" });
    return h("span", {}, button, input);
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

  /** Office chosen last in the patent check (the first profile until then). */
  private patentProfile = "";

  /**
   * "Patent check (drawings)": the engine's preflight of the page for an office, as a popover – office choice, findings
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
    const run = () => {
      let r: PreflightResult;
      try { r = this.sessionObj.preflight(this.patentProfile, lang()); } catch (e) { this.report(e); return; }
      disclaimer.textContent = r.disclaimer;
      const c = findingCounts(r.findings);
      summary.textContent = r.findings.length ? t("pc.summary", r.findings.length, c.error, c.warning) : t("pc.none");
      list.replaceChildren(...orderFindings(r.findings).map((f) => {
        // The message selects the element (if the finding has one); the cited sources are short links.
        const id = f.id;
        let message: HTMLElement = h("span", {}, f.message);
        if (id) {
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
    office.addEventListener("change", () => { this.patentProfile = office.value; run(); });
    const again = h("button", { class: "isk-btn", type: "button", "data-cmd": "patentrecheck" }, t("pc.recheck"));
    again.addEventListener("click", run);
    const close = h("button", { class: "isk-btn isk-icon-btn isk-close", type: "button", "aria-label": t("close"), html: icon("close", 16) });
    const panel = h("div", { class: "isk-popover isk-pc", role: "dialog", "aria-label": t("pc.title") },
      h("div", { class: "isk-pop-head" }, h("strong", {}, t("pc.title")), close),
      h("label", { class: "isk-field" }, h("span", { class: "isk-field-label" }, t("pc.office")), h("div", { class: "isk-row" }, office, again)),
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
    const wasSettings = !!this.settingsEl && this.settingsEl.dataset.kind !== "patent";
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

  /** "Export data": one button per domain export that applies to the selection (or the page); downloads the file. */
  private exportFieldset(): HTMLElement {
    const sel = this.st.selection;
    let list: ExporterInfo[] = [];
    try {
      list = this.sessionObj.exporters(sel, lang());
    } catch (e) {
      this.report(e);
    }
    const buttons = list.map((x) => {
      const b = h("button", { class: "isk-btn", type: "button", title: x.preserves }, x.name);
      b.addEventListener("click", () => this.guard(() => {
        const r = this.sessionObj.exportData(sel, x.id, lang());
        const bom = r.extension === "csv" ? "\ufeff" : "";
        download(`sketch.${r.extension}`, new Blob([bom + r.text], { type: `${r.mime};charset=utf-8` }));
      }));
      return b;
    });
    return h("fieldset", {}, h("legend", {}, t("set.export")),
      ...(buttons.length ? [h("div", { class: "isk-col" }, ...buttons)] : [h("p", { class: "isk-muted" }, t("set.exportNone"))]));
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
    this.renderTopbar();
    if (this.settings.inspectorOpen || this.root.dataset.layout === "wide") this.inspector.update();
    this.updateBar();
    if (live && this.sync && this.settings.liveSync) this.sync.scheduleLive(next).catch((e) => this.report(e));
  }

  /**
   * Context bar for the selection (ROADMAP UX rules 1–5): only what does something for it. Mirror/rotate only when they
   * change the drawing (`transformEffect`), "Insert …" for every element, "Draw the path" for links of a chain with
   * F = 1, "Select assembly" when coupled parts are not selected yet, "Make it …" right after a quick add; two or more
   * elements get the multi-selection bar (`arrangeOptions`).
   */
  private updateBar(): void {
    const sel = this.st.selection;
    const key = sel.join(",");
    const box = this.stage.selectionScreenBox(true);
    const suppressed = this.stage.dragging || this.pie.open || this.prompt.open || this.insertBox.open;
    if (sel.length !== 1 || sel[0] !== this.nextFor) this.nextFor = undefined;
    this.traceFor = undefined;
    this.nextChoices = [];
    if (!sel.length) return this.bar.update("", [], undefined, true);
    const known = sel.some((id) => this.objectById(id)?.known);
    const assembly: BarCommand[] = known && this.assemblyGrows(sel) ? [{ id: "assembly", label: t("ctx.assembly"), icon: "cmd-select-assembly" }] : [];
    if (elementCount(this.st.objects, sel) >= 2) {
      let options: ArrangeOption[] = [];
      try {
        options = this.sessionObj.arrangeOptions(sel, lang());
      } catch (e) {
        this.report(e);
      }
      this.bar.updateMulti(key, multiBarGroups(options), box, suppressed, assembly);
      return;
    }
    let actions: ActionInfo[] = [];
    try {
      actions = this.sessionObj.contextActions(sel, lang());
    } catch (e) {
      this.report(e);
    }
    if (this.mainTextTarget()) actions = [...actions, this.textAction()];
    if (known) actions = filterTransforms(actions, this.transformEffect(sel));
    // Just added with "+": "Make it …" opens the insert box with what the new element can become (no thumbnail row).
    const lead: BarCommand[] = [];
    if (this.nextFor) {
      try {
        this.nextChoices = this.sessionObj.nextItems(this.nextFor, lang(), 12);
      } catch (e) {
        this.report(e);
      }
      if (this.nextChoices.length >= 2) lead.push({ id: "makeIt", label: t("ctx.makeIt"), icon: "cmd-cycle" });
    }
    const build: BarCommand[] = [];
    if (known) build.push({ id: "insert", label: t("ctx.insert"), icon: "cmd-insert" });
    const trace = this.traceOf(sel);
    if (canTrace(trace)) {
      this.traceFor = { id: sel[0], trace };
      build.push({ id: "trace", label: t("ctx.trace"), icon: "cmd-trace" });
    }
    const info = this.dofOf(sel);
    if (canDrawGhosts(this.objectById(sel[0])?.kind, info?.dof, trace)) build.push({ id: "ghosts", label: t("ctx.ghosts"), icon: "cmd-animate" });
    if (info?.dof != null) build.push({ id: "info:dof", label: t("ctx.dof", info.dof), icon: "", text: `F = ${info.dof}` });
    // Motion mode only for parts that can move: a link of a chain with F >= 1, a gear coupled with something.
    if (this.objectById(sel[0])?.known && canMove(this.stage.motionOf(sel[0])))
      build.push({ id: "motion", label: t("ctx.motion"), icon: "cmd-motion", active: this.motion });
    if (info?.explodable) build.push({ id: "explode", label: t("ctx.explode"), icon: "cmd-explode" });
    build.push(...assembly);
    this.bar.update(key, actions, box, suppressed, this.barChoices(sel), { lead, build });
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

  private barCommand(id: string, clientX: number, clientY: number): void {
    const sel = this.st.selection;
    if (id === "insert") return this.openInsert({ x: clientX, y: clientY });
    if (id === "makeIt") return this.openMakeIt({ x: clientX, y: clientY });
    if (id === "trace") return this.drawPath();
    if (id === "info:dof") return;
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
   * Choices shown as icon buttons in the bar (no text, no submenu): line type (thumbnails), every choice parameter whose
   * options have pictograms (arrowheads, head size, line course …) and, for lines, the line style.
   */
  private barChoices(sel: string[]): ChoiceEntry[] {
    if (sel.length !== 1) return [];
    const obj = this.st.objects.find((o) => o.id === sel[0]);
    if (!obj?.known) return [];
    const out: ChoiceEntry[] = [];
    try {
      const conv = this.sessionObj.conversions(sel, lang());
      if (conv.length > 1)
        out.push({
          key: "@type", label: t("ctx.lineType"), current: obj.type,
          options: conv.map((c) => {
            const item = this.catalog.items.find((i) => i.typeId === c.id);
            return { id: c.id, label: c.label, image: item?.thumbnail ? svgDataUrl(item.thumbnail) : undefined };
          }),
        });
      const info = this.sessionObj.inspector(sel, lang());
      // Choices with pictograms first, then the other choices (shown as short text), in parameter order.
      const choices = info.params.filter((p) => p.kind === "choice" && !p.advanced && p.options && p.options.length > 1);
      const pictorial = (p: (typeof choices)[number]) => p.options!.every((o) => o.icon && hasIcon(o.icon));
      for (const p of [...choices.filter(pictorial), ...choices.filter((p) => !pictorial(p))]) {
        const icons = pictorial(p);
        out.push({
          key: p.key, label: p.label, current: String(p.value ?? p.default ?? p.options![0].id), mirror: p.mirrorIcons,
          options: p.options!.map((o) => ({ id: o.id, label: o.label, icon: icons ? o.icon : undefined })),
        });
      }
      if (obj.kind === "points")
        out.push({
          key: "@dash", label: t("ctx.lineStyle"), current: info.style?.dash ?? "solid",
          options: DASHES.map(([id, ico]) => ({ id, label: t(`dash.${id}`), icon: ico })),
        });
    } catch (e) {
      this.report(e);
    }
    return out.slice(0, 6);
  }

  private pickChoice(key: string, value: string): void {
    const sel = this.st.selection;
    if (key === "@type") this.edit((s) => s.convert(sel, value));
    else if (key === "@dash") this.edit((s) => s.setStyle(sel, { dash: value as DashStyle }));
    else this.edit((s) => s.setParam(sel, key, value));
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

  /** Where inline prompts open: below the selection, else in the middle of the stage (client px). */
  private promptAnchor(): { x: number; y: number } {
    const box = this.stage.selectionScreenBox();
    const r = this.stage.el.getBoundingClientRect();
    return box ? { x: (box.left + box.right) / 2, y: box.bottom } : { x: r.left + r.width / 2, y: r.top + r.height / 2 };
  }

  private pieOverflow: PieEntry[] = [];
  private pieConversions: ActionInfo[] = [];
  private pieLabels: { entry: PieEntry; change: LabelChange }[] = [];

  private textAction(): ActionInfo {
    return { id: "text", label: t("ctx.text"), icon: "cmd-text", enabled: true, active: false, group: "edit", kind: "generic" };
  }

  private menuEntry(id: string, label: string, icon?: string): PieEntry {
    return { id, label, icon, enabled: true, active: false };
  }

  /**
   * Pie menu with at most eight entries (COM add-in rules): delete, views, "Text …", "Insert …", duplicate, rotate in the
   * ring; type actions, mirror, order and grouping overflow into "More ▸". Mirror/rotate only when they change the
   * drawing; "Insert …" opens the insert box.
   */
  openPie(clientX?: number, clientY?: number): void {
    const sel = this.st.selection;
    let actions: ActionInfo[];
    try {
      actions = this.sessionObj.contextActions(sel, lang());
    } catch (e) {
      return this.report(e);
    }
    const known = sel.some((id) => this.objectById(id)?.known);
    if (known) actions = filterTransforms(actions, this.transformEffect(sel));
    if (clientX === undefined || clientY === undefined) {
      const box = this.stage.selectionScreenBox();
      const r = this.stage.el.getBoundingClientRect();
      clientX = box ? (box.left + box.right) / 2 : r.left + r.width / 2;
      clientY = box ? (box.top + box.bottom) / 2 : r.top + r.height / 2;
    }
    const text = this.mainTextTarget() ? this.menuEntry("text", t("pie.text"), "cmd-text") : undefined;
    const insert = known ? this.menuEntry("menu:insert", t("pie.insert"), "cmd-insert") : undefined;
    this.pieConversions = [];
    try {
      this.pieConversions = this.sessionObj.conversions(sel, lang());
    } catch (e) {
      this.report(e);
    }
    const type = this.pieConversions.length ? this.menuEntry("menu:type", t("pie.type"), "cmd-cycle") : undefined;
    // "Label ▸" belongs to the convertible point types (lines, arrows, dimensions, lead lines), like in the COM add-in.
    this.pieLabels = [];
    if (type) {
      try {
        const info = this.sessionObj.inspector(sel, lang());
        this.pieLabels = labelEntries(info.params, { larger: t("pie.textLarger"), smaller: t("pie.textSmaller") });
      } catch (e) {
        this.report(e);
      }
    }
    const label = this.pieLabels.length ? this.menuEntry("menu:label", t("pie.label"), "cmd-text") : undefined;
    const { main, overflow } = pieSplit(buildPieEntries(actions, { text, insert, type, label }), this.menuEntry("menu:more", t("pie.more"), "more"));
    this.pieOverflow = overflow;
    this.bar.hide();
    this.pie.show(main, clientX, clientY);
  }

  private pickPie(e: PieEntry): void {
    const at = this.pie.at ?? { x: 0, y: 0 };
    const back = this.menuEntry("menu:back", t("pie.back"), "menuBack");
    const more = this.menuEntry("menu:more", t("pie.more"), "more");
    if (e.id === "menu:back") return this.openPie(at.x, at.y);
    if (e.id === "menu:more") {
      const page = submenu(back, this.pieOverflow, more);
      this.pieOverflow = page.overflow;
      return this.pie.show(page.main, at.x, at.y);
    }
    if (e.id === "menu:insert") return this.openInsert(at);
    if (e.id === "menu:type") {
      const entries = this.pieConversions.map((c): PieEntry => ({ id: `conv:${c.id}`, label: c.label, icon: "cmd-cycle", enabled: true, active: !!c.active }));
      const page = submenu(back, entries, more);
      this.pieOverflow = page.overflow;
      return this.pie.show(page.main, at.x, at.y);
    }
    if (e.id === "menu:label") {
      const page = submenu(back, this.pieLabels.map((l) => l.entry), more);
      this.pieOverflow = page.overflow;
      return this.pie.show(page.main, at.x, at.y);
    }
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
    if (e.id.startsWith("conv:")) {
      const sel = this.st.selection;
      return this.edit((s) => s.convert(sel, e.id.slice(5)));
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

  /** Office selection-changed handler. */
  onOfficeSelectionChanged(): void {
    this.sync?.loadSelection().catch((e) => this.report(e));
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
      else if (!ctrl && (e.key === "Delete" || e.key === "Backspace")) act("delete");
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
