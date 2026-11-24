// Insert box (one insert flow, ROADMAP UX rule 3): the bar's insert button, "Insert …" in the pie menu and Ctrl+Space
// open a search box at the pointer. Without typing it lists the parts that fit the selection (marked "fits") and then
// the recent items; typing searches the whole library (engine `search`: both languages, umlaut folding, the
// selection's view preferred). ↑/↓ move, Enter takes the marked row, Esc or a click elsewhere closes.

import type { CatalogItem, SuggestionInfo } from "../engine/types";
import { t } from "../i18n";
import { clamp, h, svgDataUrl } from "./dom";
import { icon } from "./icons";

/** One row of the box. */
export interface InsertRow { id: string; name: string; fits: boolean; recent: boolean; image?: string }

/** Most recent first, without duplicates, at most `max` (mirrors Core's LibrarySearch.Remember). */
export function rememberRecent(recent: readonly string[], id: string, max = 12): string[] {
  return [id, ...recent.filter((x) => x !== id)].slice(0, max);
}

/**
 * Rows for engine hits in engine order: items missing from the (visible) catalog are left out, `fits` comes from the
 * engine, recent items that do not fit are marked as recent.
 */
export function insertRows(hits: SuggestionInfo[], recent: readonly string[], item: (id: string) => CatalogItem | undefined): InsertRow[] {
  const rec = new Set(recent);
  const seen = new Set<string>();
  const rows: InsertRow[] = [];
  for (const hit of hits) {
    const it = item(hit.id);
    if (!it || seen.has(hit.id)) continue;
    seen.add(hit.id);
    const fits = !!hit.fits;
    rows.push({ id: hit.id, name: hit.name || it.name, fits, recent: !fits && rec.has(hit.id), image: it.thumbnail ? svgDataUrl(it.thumbnail) : undefined });
  }
  return rows;
}

export type BoxAction = "none" | "move" | "choose" | "close";

/** Keyboard of the box: ↑/↓ move the marked row (wrapping), Enter takes it, Esc closes. */
export function insertBoxKey(key: string, index: number, count: number): { index: number; action: BoxAction } {
  switch (key) {
    case "ArrowDown": return count ? { index: (index + 1) % count, action: "move" } : { index: 0, action: "none" };
    case "ArrowUp": return count ? { index: (index - 1 + count) % count, action: "move" } : { index: 0, action: "none" };
    case "Enter": return count ? { index: clamp(index, 0, count - 1), action: "choose" } : { index, action: "none" };
    case "Escape": return { index, action: "close" };
    default: return { index, action: "none" };
  }
}

/** Top-left of a w×h box at the pointer (x, y) in a pane of pw×ph: below right of it, flipped where there is no room. */
export function boxPosition(x: number, y: number, w: number, h: number, pw: number, ph: number): { left: number; top: number } {
  let left = x + 4;
  if (left + w > pw - 4) left = x - w - 4;
  let top = y + 4;
  if (top + h > ph - 4) top = y - h - 4;
  return { left: clamp(left, 4, Math.max(4, pw - w - 4)), top: clamp(top, 4, Math.max(4, ph - h - 4)) };
}

export interface InsertBoxRequest {
  /** Pointer in client coordinates. */
  at: { x: number; y: number };
  /** Accessible name of the box. */
  label: string;
  /** Rows for the typed text ("" = fitting and recent items). */
  rows(text: string): InsertRow[];
  choose(id: string): void;
  /** Called whenever the box closes (before `choose`). */
  closed?(): void;
}

export class InsertBox {
  private el?: HTMLElement;
  private list?: HTMLElement;
  private req?: InsertBoxRequest;
  private rows: InsertRow[] = [];
  private index = 0;
  private text = "";
  private restoreFocus?: HTMLElement | null;

  constructor(private readonly root: HTMLElement) {
    root.addEventListener("pointerdown", (e) => {
      if (this.el && !this.el.contains(e.target as Node | null)) this.close();
    }, true);
  }

  get open(): boolean {
    return !!this.el;
  }

  show(req: InsertBoxRequest): void {
    this.close(false);
    this.req = req;
    this.restoreFocus = document.activeElement as HTMLElement | null;
    const input = h("input", { class: "isk-input isk-ins-input", type: "search", placeholder: t("ins.search"), "aria-label": t("ins.search"), autocomplete: "off", spellcheck: "false" });
    this.list = h("div", { class: "isk-ins-list", role: "listbox", "aria-label": req.label });
    this.el = h("div", { class: "isk-insertbox", role: "dialog", "aria-label": req.label },
      h("div", { class: "isk-search-row", html: icon("search", 16) }, input), this.list);
    input.addEventListener("input", () => this.refresh(input.value));
    input.addEventListener("keydown", (e) => {
      const k = insertBoxKey(e.key, this.index, this.rows.length);
      if (k.action === "none") return;
      e.preventDefault();
      e.stopPropagation();
      if (k.action === "close") this.close();
      else if (k.action === "move") { this.index = k.index; this.paint(); }
      else this.pick(this.rows[k.index]);
    });
    this.root.append(this.el);
    this.refresh("");
    const rr = this.root.getBoundingClientRect();
    const pos = boxPosition(req.at.x - rr.left, req.at.y - rr.top, this.el.offsetWidth, this.el.offsetHeight, rr.width, rr.height);
    this.el.style.left = `${pos.left}px`;
    this.el.style.top = `${pos.top}px`;
    input.focus();
  }

  /** Closes the box; `notify` false skips `closed` (the box is replaced by a new one). */
  close(notify = true): void {
    if (!this.el) return;
    const req = notify ? this.req : undefined;
    this.el.remove();
    this.el = undefined;
    this.list = undefined;
    this.req = undefined;
    this.restoreFocus?.focus?.({ preventScroll: true });
    req?.closed?.();
  }

  private refresh(text: string): void {
    this.text = text;
    try {
      this.rows = this.req?.rows(text) ?? [];
    } catch (e) {
      console.error(e);
      this.rows = [];
    }
    this.index = 0;
    this.paint();
  }

  private paint(): void {
    const list = this.list;
    if (!list) return;
    if (!this.rows.length) {
      list.replaceChildren(h("p", { class: "isk-muted isk-ins-empty" }, this.text.trim() ? t("ins.none") : t("ins.empty")));
      return;
    }
    list.replaceChildren(...this.rows.map((r, i) => {
      const b = h("button", { class: `isk-ins-row${i === this.index ? " is-active" : ""}`, type: "button", role: "option", "aria-selected": String(i === this.index), tabindex: "-1" },
        r.image ? h("img", { src: r.image, alt: "" }) : h("span", { class: "isk-ins-noimg" }),
        h("span", { class: "isk-ins-name" }, r.name),
        r.fits ? h("span", { class: "isk-ins-badge" }, t("ins.fits")) : r.recent ? h("span", { class: "isk-ins-recent" }, t("ins.recent")) : false);
      b.addEventListener("click", () => this.pick(r));
      return b;
    }));
    list.children[this.index]?.scrollIntoView?.({ block: "nearest" });
  }

  private pick(row: InsertRow | undefined): void {
    const req = this.req;
    if (!row || !req) return;
    this.close();
    req.choose(row.id);
  }
}
