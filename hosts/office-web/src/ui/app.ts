// Task-pane application: top bar, library, stage, inspector, floating context bar and pie menu around one engine
// session. Runs inside PowerPoint (Office.js present) and standalone in a browser (dev.html).

import { Engine, Session } from "../engine/client";
import type { ActionInfo, Catalog, CatalogItem, EditorState, ObjectInfo } from "../engine/types";
import { exportPng, exportSvg, download } from "../export";
import { lang, resolveLang, setLang, t, type Lang } from "../i18n";
import { drawCapabilities, noOffice, type HostCapabilities } from "../office/capabilities";
import { SlideSync } from "../office/sync";
import { ComSync, type ComSyncBridge } from "../office/combridge";
import { loadSettings, saveSettings, type Settings } from "../settings";
import { applyTheme, resolveTheme } from "../theme";
import { ContextBar } from "./contextbar";
import { h } from "./dom";
import { icon } from "./icons";
import { Inspector } from "./inspector";
import { Library } from "./library";
import { PieMenu } from "./piemenu";
import { Stage } from "./stage";

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
      pointerMoved: (x, y) => this.bar.pointerMoved(x, y),
      report: (e) => this.report(e),
    });
    this.stage.onEditText = (o) => this.editText(o);
    this.library = new Library({
      favourites: () => this.settings.favourites,
      toggleFavourite: (id) => {
        const f = new Set(this.settings.favourites);
        if (f.has(id)) f.delete(id); else f.add(id);
        this.settings.favourites = [...f];
        saveSettings(this.settings);
      },
      startInsert: (item) => this.startInsert(item),
      insertNow: (item) => this.insertAt(item, this.stage.viewCentre()),
      dragStart: (item) => (this.dragging = item),
      pendingId: () => this.pending?.id,
    });
    this.inspector = new Inspector({ session: () => this.sessionObj, state: () => this.st, apply: (s, live) => this.apply(s, live), report: (e) => this.report(e) });
    this.pie = new PieMenu(this.root, (a) => this.runAction(a));
    this.status = h("div", { class: "isk-status", "aria-live": "polite" });
    this.toastEl = h("div", { class: "isk-toast", role: "status", hidden: true });
    this.topbar = h("header", { class: "isk-topbar" });

    if (opts.comBridge) {
      this.sync = new ComSync({
        session: () => this.sessionObj,
        adopt: (s) => { this.apply(s, false); this.toast(t("office.loaded")); },
        report: (e) => this.report(e),
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
    this.bar = new ContextBar(this.root, (a) => this.runAction(a), () => this.openInspectorStyle(), (x, y) => this.openPie(x, y));
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
      this.iconButton("svg", t("cmd.exportSvg"), () => this.guard(() => exportSvg(this.sessionObj, sel.length ? sel : undefined, this.monochrome))),
      this.iconButton("png", t("cmd.exportPng"), () => exportPng(this.sessionObj, sel.length ? sel : undefined, this.monochrome).catch((e) => this.report(e))),
      h("span", { class: "isk-grow" }),
      this.iconButton("settings", t("cmd.settings"), (e) => this.toggleSettings(e.currentTarget as HTMLElement), { "aria-haspopup": "dialog" }),
    );
    this.topbar.replaceChildren(...items.filter((x): x is HTMLElement => !!x));
  }

  private monochrome = false;

  private toggleSettings(anchor: HTMLElement): void {
    if (this.settingsEl) { this.settingsEl.remove(); this.settingsEl = undefined; return; }
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
        h("label", { class: "isk-field" }, h("span", { class: "isk-field-label" }, t("set.grid")), grid),
        check(this.monochrome, t("set.mono"), (v) => (this.monochrome = v))),
      h("fieldset", {}, h("legend", {}, t("set.file")), h("div", { class: "isk-row" }, openBtn, saveBtn), open),
      h("p", { class: "isk-muted" }, this.versionLine()),
    );
    close.addEventListener("click", () => { panel.remove(); this.settingsEl = undefined; anchor.focus(); });
    panel.addEventListener("keydown", (e) => { if (e.key === "Escape") { e.stopPropagation(); close.click(); } });
    this.root.append(panel);
    this.settingsEl = panel;
    panel.querySelector<HTMLInputElement>("input")?.focus();
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
      this.library.setCatalog(this.catalog);
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

  private updateBar(): void {
    const sel = this.st.selection;
    let actions: ActionInfo[] = [];
    if (sel.length) {
      try {
        actions = this.sessionObj.contextActions(sel, lang());
      } catch (e) {
        this.report(e);
      }
    }
    this.bar.update(sel.join(","), actions, this.stage.selectionScreenBox(), this.stage.dragging || this.pie.open);
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
    const sel = this.st.selection;
    this.edit((s) => s.applyAction(sel, a.id));
    this.stage.el.focus({ preventScroll: true });
  }

  openPie(clientX?: number, clientY?: number): void {
    const sel = this.st.selection;
    let actions: ActionInfo[];
    try {
      actions = this.sessionObj.contextActions(sel, lang());
    } catch (e) {
      return this.report(e);
    }
    if (clientX === undefined || clientY === undefined) {
      const box = this.stage.selectionScreenBox();
      const r = this.stage.el.getBoundingClientRect();
      clientX = box ? (box.left + box.right) / 2 : r.left + r.width / 2;
      clientY = box ? (box.top + box.bottom) / 2 : r.top + r.height / 2;
    }
    this.bar.hide();
    this.pie.show(actions, clientX, clientY);
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

  private editText(o: ObjectInfo): void {
    this.apply(this.sessionObj.select([o.id]));
    this.settings.inspectorOpen = true;
    this.layoutPanelsAfterChange();
    this.inspector.update(true);
    this.inspector.focusText();
  }

  // ------------------------------------------------------------------ insert

  private startInsert(item: CatalogItem): void {
    // Inside the COM add-in: a library click places the element directly on the slide (insert at pointer with
    // snapping, like the ribbon galleries); dragging into the stage still adds it to the editor.
    if (this.sync instanceof ComSync) {
      this.sync.place(item.id);
      this.setStatus(t("com.placing", item.name));
      return;
    }
    this.pending = this.pending?.id === item.id ? undefined : item;
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
        if (this.pie.open) return;
        if (this.pending) { this.cancelInsert(); e.preventDefault(); return; }
        if (this.stage.dragging) { this.stage.cancelDrag(); e.preventDefault(); return; }
        if (!typing && this.st.selection.length) { this.apply(this.sessionObj.select([])); e.preventDefault(); }
        return;
      }
      if (typing || this.pie.open) return;
      const sel = this.st.selection;
      const ctrl = e.ctrlKey || e.metaKey;
      const k = e.key.toLowerCase();
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
