// Searchable library with categories, favourites and SVG thumbnails. Click = insert at pointer (with snapping
// ghost, Esc cancels), double click/Enter = insert in the middle of the view, drag = drop onto the stage.

import type { Catalog, CatalogItem } from "../engine/types";
import { t } from "../i18n";
import { h, svgDataUrl } from "./dom";
import { icon } from "./icons";

export interface LibraryHost {
  favourites(): string[];
  toggleFavourite(id: string): void;
  startInsert(item: CatalogItem): void;
  insertNow(item: CatalogItem): void;
  dragStart(item: CatalogItem | undefined): void;
  pendingId(): string | undefined;
}

/** Filters items by free text (all terms must match name/keywords/category) and category ("*" all, "fav"). */
export function filterItems(catalog: Catalog, query: string, category: string, favourites: string[]): CatalogItem[] {
  const terms = query.toLowerCase().split(/\s+/).filter(Boolean);
  const fav = new Set(favourites);
  return catalog.items.filter((i) => {
    if (category === "fav" && !fav.has(i.id)) return false;
    if (category !== "*" && category !== "fav" && i.category !== category) return false;
    if (!terms.length) return true;
    const hay = [i.name, i.category, i.typeId, ...i.keywords].join(" ").toLowerCase();
    return terms.every((x) => hay.includes(x));
  });
}

export class Library {
  readonly el: HTMLElement;
  private readonly grid: HTMLDivElement;
  private readonly chips: HTMLDivElement;
  private readonly search: HTMLInputElement;
  private category = "*";
  private catalog: Catalog = { lang: "en", categories: [], items: [] };

  constructor(private readonly host: LibraryHost) {
    this.search = h("input", { class: "isk-input isk-search", type: "search", placeholder: t("lib.search"), "aria-label": t("lib.search") });
    this.search.addEventListener("input", () => this.renderItems());
    this.search.addEventListener("keydown", (e) => {
      if (e.key === "Enter") {
        const first = this.grid.querySelector<HTMLButtonElement>(".isk-lib-item");
        first?.focus();
      }
    });
    this.chips = h("div", { class: "isk-chips", role: "tablist", "aria-label": t("lib.categories") });
    this.grid = h("div", { class: "isk-lib-grid", role: "listbox", "aria-label": t("lib.title") });
    this.el = h("section", { class: "isk-library", "aria-label": t("lib.title") },
      h("div", { class: "isk-search-row", html: icon("search", 16) }, this.search), this.chips, this.grid);
  }

  setCatalog(c: Catalog): void {
    this.catalog = c;
    this.search.placeholder = t("lib.search");
    this.renderChips();
    this.renderItems();
  }

  private renderChips(): void {
    const chip = (id: string, label: string) => {
      const b = h("button", { class: `isk-chip ${this.category === id ? "is-active" : ""}`, type: "button", role: "tab", "aria-selected": String(this.category === id) }, label);
      b.addEventListener("click", () => { this.category = id; this.renderChips(); this.renderItems(); });
      return b;
    };
    const used = new Set(this.catalog.items.map((i) => i.category));
    this.chips.replaceChildren(
      chip("*", t("lib.all")),
      chip("fav", t("lib.favourites")),
      ...this.catalog.categories.filter((c) => used.has(c.id)).map((c) => chip(c.id, c.name)),
    );
  }

  renderItems(): void {
    const favs = this.host.favourites();
    const items = filterItems(this.catalog, this.search.value, this.category, favs);
    const pending = this.host.pendingId();
    this.grid.replaceChildren(
      ...items.map((item) => {
        const fav = favs.includes(item.id);
        const star = h("span", { class: `isk-star ${fav ? "is-on" : ""}`, role: "button", tabindex: "-1", title: fav ? t("lib.unfavourite") : t("lib.favourite"), html: icon("star", 14) });
        star.addEventListener("click", (e) => { e.stopPropagation(); this.host.toggleFavourite(item.id); this.renderItems(); });
        const img = h("img", { src: svgDataUrl(item.thumbnail), alt: "", draggable: "false" });
        const b = h("button", {
          class: `isk-lib-item ${pending === item.id ? "is-pending" : ""}`, type: "button", role: "option", draggable: "true",
          title: `${item.name} – ${t("lib.hint")}`, "aria-label": item.name, "data-item": item.id,
        }, h("span", { class: "isk-thumb" }, img), h("span", { class: "isk-lib-name" }, item.name), star);
        b.addEventListener("click", () => this.host.startInsert(item));
        b.addEventListener("dblclick", () => this.host.insertNow(item));
        b.addEventListener("keydown", (e) => { if (e.key === "Enter") { e.preventDefault(); this.host.insertNow(item); } });
        b.addEventListener("dragstart", (e) => {
          e.dataTransfer?.setData("text/plain", item.id);
          if (e.dataTransfer) e.dataTransfer.effectAllowed = "copy";
          this.host.dragStart(item);
        });
        b.addEventListener("dragend", () => this.host.dragStart(undefined));
        return b;
      }),
    );
    if (!items.length) this.grid.append(h("p", { class: "isk-empty" }, this.catalog.items.length ? t("lib.noMatch") : t("lib.empty")));
  }
}
