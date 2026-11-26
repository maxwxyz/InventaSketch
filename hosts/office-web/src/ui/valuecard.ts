// Value card of the selected element (docs/en/design/engineering-calculations.md section 11, like the COM add-in's
// ValueCard): one row per variable with its marker, symbol, name, value field and unit; entered values can be cleared;
// derived values show their formula step by step (Explore and Engineer; Engineer adds the source); the status line says
// ✓, what is missing or that entries conflict, and every conflict names both values. All values come from the engine
// (`values`, `setValue`, `clearValue`, `explain`); the card only shows them and passes what is typed. In Explore a ↔ grip
// beside a positive value drags it left or right with a live preview of everything that depends on it (`dragValue`,
// like the COM add-in); a value with tolerances shows its range; "Calculation sheet …" opens the sheet (`calcSheet`).

import type { Session } from "../engine/client";
import type { EditorState, ObjectValues, ValueLevel, VarInfo } from "../engine/types";
import { lang, t } from "../i18n";
import { clamp, h } from "./dom";
import { canDragValue, dragValueStep, rangeText } from "./handlelogic";
import { icon } from "./icons";

/** Marker of a value's provenance: ✎ entered, = derived, ○ default, ⇄ linked, ⚠ in conflict. */
export function provenanceMarker(v: Pick<VarInfo, "provenance" | "conflict">): string {
  if (v.conflict) return "⚠";
  switch (v.provenance) {
    case "entered": return "✎";
    case "derived": return "=";
    case "default": return "○";
    case "linked": return "⇄";
    default: return "";
  }
}

/** The variables the card lists at a level: the card ones, plus the panel and engineer ones in Engineer. */
export function visibleVars(vars: VarInfo[], level: ValueLevel): VarInfo[] {
  return level === "engineer" ? vars : vars.filter((v) => v.relevance === "card");
}

/** Significant digits of the card: three, four in Engineer (section 11.2). */
export function cardDigits(level: ValueLevel): number {
  return level === "engineer" ? 4 : 3;
}

/** The text a value field starts with: the entered text as typed, else the formatted value. */
export function fieldText(v: VarInfo): string {
  return v.provenance === "entered" && v.text ? v.text : v.display;
}

export const VALUE_LEVELS: ValueLevel[] = ["quick", "explore", "engineer"];

export interface ValueCardHost {
  session(): Session;
  state(): EditorState;
  apply(state: EditorState, live?: boolean): void;
  report(error: unknown): void;
  /** The value level of the document (setValueLevel). */
  setLevel(level: ValueLevel): void;
  /** The calculator, prefilled with `text`. */
  openCalculator(text: string): void;
  /** The element shows its values label (parameter showValues) – undefined when the type has no such switch. */
  showsValues(id: string): boolean | undefined;
  setShowValues(id: string, on: boolean): void;
  /** The calculation sheet of element `id` (copy, download, insert as a table). */
  openCalcSheet(id: string): void;
  /** The card closed (Esc, ×, the element left the selection). */
  closed(): void;
}

export class ValueCard {
  private el?: HTMLElement;
  private id?: string;
  private name = "";
  private at = { x: 0, y: 0 };
  /** Messages of entries that were refused (wrong unit or kind), by variable key, until the next successful entry. */
  private errors = new Map<string, string>();
  private restoreFocus?: HTMLElement | null;

  constructor(private readonly root: HTMLElement, private readonly host: ValueCardHost) {}

  get open(): boolean {
    return !!this.el;
  }

  /** The element the card shows. */
  get target(): string | undefined {
    return this.el ? this.id : undefined;
  }

  /** Opens the card for element `id` right of the client point `at`; `focusKey` puts the cursor into that value. */
  show(id: string, name: string, at: { x: number; y: number }, focusKey?: string): void {
    if (!this.el) this.restoreFocus = document.activeElement as HTMLElement | null;
    this.id = id;
    this.name = name;
    this.at = at;
    this.errors.clear();
    this.render(focusKey);
  }

  /** After an edit: shows the new values (or closes when the element is gone or no longer selected alone). */
  refresh(): void {
    if (!this.el || !this.id) return;
    const st = this.host.state();
    if (!st.objects.some((o) => o.id === this.id) || st.selection.length !== 1 || st.selection[0] !== this.id) { this.close(); return; }
    const active = document.activeElement as HTMLElement | null;
    const focusKey = active && this.el.contains(active) ? active.dataset.key : undefined;
    this.render(focusKey);
  }

  close(): void {
    if (!this.el) return;
    this.el.remove();
    this.el = undefined;
    this.restoreFocus?.focus?.({ preventScroll: true });
    this.host.closed();
  }

  private render(focusKey?: string): void {
    const id = this.id;
    if (!id) return;
    const st = this.host.state();
    const level = st.valueLevel ?? "quick";
    let values: ObjectValues | undefined;
    try {
      values = this.host.session().values(id, lang(), cardDigits(level)).objects.find((o) => o.id === id);
    } catch (e) {
      this.host.report(e);
    }
    const old = this.el;
    const close = h("button", { class: "isk-btn isk-icon-btn isk-close", type: "button", "aria-label": t("close"), title: t("close"), html: icon("close", 16) });
    close.addEventListener("click", () => this.close());
    const levelSel = h("select", { class: "isk-input", "aria-label": t("val.level"), "data-cmd": "valuelevel" });
    for (const l of VALUE_LEVELS) levelSel.append(h("option", { value: l, selected: l === level }, t(`val.level.${l}`)));
    levelSel.addEventListener("change", () => this.host.setLevel(levelSel.value as ValueLevel));
    const card = h("div", { class: "isk-popover isk-valuecard", role: "dialog", "aria-label": `${t("val.title")} – ${this.name}` },
      h("div", { class: "isk-pop-head" }, h("strong", {}, `${t("val.title")} – ${this.name}`), close),
      h("label", { class: "isk-field isk-row" }, h("span", { class: "isk-field-label" }, t("val.level")), levelSel));
    if (!values || values.vars.length === 0) {
      card.append(h("p", { class: "isk-muted" }, t("val.none")));
    } else {
      const status = values.status === "checked" ? `✓ ${t("val.checked")}` : values.hint;
      card.append(h("p", { class: `isk-val-status${values.status === "conflict" ? " is-conflict" : ""}`, "data-cmd": "valuestatus" }, status));
      const table = h("div", { class: "isk-val-rows", role: "group", "aria-label": t("val.title") });
      for (const v of visibleVars(values.vars, level)) table.append(...this.row(id, v, level));
      card.append(table);
      if (values.conflicts.length) {
        const list = h("ul", { class: "isk-val-conflicts" });
        for (const c of values.conflicts) list.append(h("li", {}, `⚠ ${c.message}`));
        card.append(list);
      }
    }
    // Footer: the calculator with the focused (else the first known) value, the values label on the drawing.
    const calc = h("button", { class: "isk-btn", type: "button", "data-cmd": "valuecalc" }, t("val.calculator"));
    calc.addEventListener("click", () => {
      const key = (document.activeElement as HTMLElement | null)?.dataset?.key;
      const v = values?.vars.find((x) => x.key === key) ?? values?.vars.find((x) => x.display);
      this.host.openCalculator(v?.display ?? "");
    });
    const sheet = h("button", { class: "isk-btn", type: "button", "data-cmd": "valuesheet", title: t("sheet.hint") }, t("val.sheet"));
    sheet.addEventListener("click", () => this.host.openCalcSheet(id));
    const foot = h("div", { class: "isk-row isk-val-foot" }, calc, values?.vars.length ? sheet : false);
    const shows = this.host.showsValues(id);
    if (shows !== undefined) {
      const box = h("input", { type: "checkbox", checked: shows, "data-cmd": "valueshow" });
      box.addEventListener("change", () => this.host.setShowValues(id, box.checked));
      foot.append(h("label", { class: "isk-check" }, box, h("span", {}, t("val.show"))));
    }
    card.append(foot);
    card.addEventListener("keydown", (e) => { if (e.key === "Escape") { e.preventDefault(); e.stopPropagation(); this.close(); } });
    if (old) old.replaceWith(card); else this.root.append(card);
    this.el = card;
    this.place();
    const focus = (focusKey && card.querySelector<HTMLInputElement>(`input[data-key="${CSS.escape(focusKey)}"]`)) || (!old ? card.querySelector<HTMLInputElement>("input[data-key]") : null);
    if (focus) { focus.focus({ preventScroll: true }); if (!old) focus.select(); }
  }

  /** One variable: marker, symbol, name, value field, unit, × for an entered value; then its formula line and error. */
  private row(id: string, v: VarInfo, level: ValueLevel): HTMLElement[] {
    const input = h("input", {
      class: "isk-input isk-val-input", type: "text", value: fieldText(v), "data-key": v.key, autocomplete: "off", spellcheck: "false",
      "aria-label": `${v.name} ${v.symbol}`, placeholder: v.provenance === "unknown" ? "–" : undefined,
    });
    input.value = fieldText(v);
    const start = input.value;
    let done = false;
    const commit = () => {
      const text = input.value.trim();
      if (done || text === start.trim()) return;
      done = true;
      try {
        const next = this.host.session().setValue(id, v.key, text);
        this.errors.delete(v.key);
        this.host.apply(next, true);
      } catch (e) {
        this.errors.set(v.key, e instanceof Error ? e.message.replace(/^setValue: /, "") : String(e));
        this.render(v.key);
      }
    };
    input.addEventListener("keydown", (e) => { if (e.key === "Enter") { e.preventDefault(); commit(); } });
    input.addEventListener("change", commit);
    const cells: (HTMLElement | false)[] = [
      h("span", { class: `isk-val-mark${v.conflict ? " is-conflict" : ""}`, title: t(`val.prov.${v.conflict ? "conflict" : v.provenance}`) }, provenanceMarker(v)),
      h("span", { class: "isk-val-sym" }, v.symbol),
      h("span", { class: "isk-val-name" }, v.name),
      input,
      h("span", { class: "isk-val-unit isk-muted" }, v.unit),
      canDragValue(v, level) ? this.grip(id, v, level) : h("span", {}),
    ];
    if (v.provenance === "entered") {
      const clear = h("button", { class: "isk-btn isk-icon-btn isk-val-clear", type: "button", title: t("val.clear"), "aria-label": `${t("val.clear")}: ${v.name}`, html: icon("close", 14) });
      clear.addEventListener("click", () => {
        try { this.host.apply(this.host.session().clearValue(id, v.key), true); } catch (e) { this.host.report(e); }
      });
      cells.push(clear);
    } else cells.push(h("span", {}));
    const out: HTMLElement[] = [h("div", { class: "isk-val-row", "data-key": v.key }, ...cells.filter((c): c is HTMLElement => !!c))];
    // The formula line under a derived value (Explore, Engineer), step by step; Engineer adds the source.
    if (level !== "quick" && (v.provenance === "derived" || v.provenance === "linked")) {
      let line = v.formula ?? "";
      try {
        const steps = this.host.session().explain(id, v.key, lang(), cardDigits(level)).steps;
        line = steps.filter((s) => s.key === v.key).pop()?.text ?? steps[steps.length - 1]?.text ?? line;
      } catch {
        // The formula from `values` stays.
      }
      if (level === "engineer" && v.source) line += ` [${v.source}]`;
      if (line) out.push(h("div", { class: "isk-val-formula isk-muted" }, line));
    }
    // A value with tolerances: its range ("180 V … 220 V").
    const range = rangeText(v);
    if (range) out.push(h("div", { class: "isk-val-range isk-muted", "data-range": v.key }, `${t("val.range")}: ${range}`));
    const err = this.errors.get(v.key) ?? v.error;
    if (err) out.push(h("div", { class: "isk-val-error", role: "alert" }, err));
    return out;
  }

  /**
   * The ↔ grip of a value (Explore, positive values; COM `ValueCard` grip): drag left or right and the value changes
   * logarithmically (a factor e per 90 px, three significant digits); every value that depends on it follows in the card
   * as a preview (`dragValue` with preview, the document unchanged). Release stores it (one undo step), Esc restores.
   */
  private grip(id: string, v: VarInfo, level: ValueLevel): HTMLElement {
    const el = h("span", { class: "isk-val-grip", role: "slider", "aria-label": `${t("val.drag")}: ${v.name}`, title: t("val.drag"), "data-grip": v.key, "aria-valuenow": v.value ?? undefined }, "↔");
    const from = v.value ?? 0;
    let startX = 0, at = from, active = false;
    const digits = cardDigits(level);
    const preview = (value: number) => {
      try {
        const r = this.host.session().dragValuePreview(id, v.key, value, lang(), digits).objects.find((o) => o.id === id);
        if (!r || !this.el) return;
        for (const x of r.vars) {
          const input = this.el.querySelector<HTMLInputElement>(`input[data-key="${CSS.escape(x.key)}"]`);
          if (input) input.value = x.display;
        }
        const status = this.el.querySelector<HTMLElement>("[data-cmd=valuestatus]");
        if (status) status.textContent = r.status === "checked" ? `✓ ${t("val.checked")}` : r.hint;
      } catch {
        // The engine refused the value (out of range): the card keeps the last preview.
      }
    };
    const end = (commit: boolean) => {
      if (!active) return;
      active = false;
      window.removeEventListener("keydown", onKey, true);
      if (commit && Math.abs(at - from) > Math.abs(from) * 1e-9) {
        try { this.host.apply(this.host.session().dragValue(id, v.key, at, digits), true); } catch (e) { this.host.report(e); this.render(v.key); }
      } else this.render();
    };
    const onKey = (e: KeyboardEvent) => { if (e.key === "Escape") { e.preventDefault(); e.stopPropagation(); end(false); } };
    el.addEventListener("pointerdown", (e) => {
      if (e.button !== 0) return;
      e.preventDefault();
      el.setPointerCapture(e.pointerId);
      startX = e.clientX;
      at = from;
      active = true;
      window.addEventListener("keydown", onKey, true);
    });
    el.addEventListener("pointermove", (e) => {
      if (!active) return;
      const next = dragValueStep(from, e.clientX - startX);
      if (Math.abs(next - at) <= Math.abs(at) * 1e-9) return;
      at = next;
      preview(next);
    });
    el.addEventListener("pointerup", () => end(true));
    el.addEventListener("pointercancel", () => end(false));
    return el;
  }

  /** Right of the element, kept inside the pane. */
  private place(): void {
    if (!this.el) return;
    const rr = this.root.getBoundingClientRect();
    const w = this.el.offsetWidth, hgt = this.el.offsetHeight;
    let left = this.at.x - rr.left + 12;
    if (left + w > rr.width - 4) left = this.at.x - rr.left - w - 12;
    this.el.style.left = `${clamp(left, 4, Math.max(4, rr.width - w - 4))}px`;
    this.el.style.top = `${clamp(this.at.y - rr.top - 20, 4, Math.max(4, rr.height - hgt - 4))}px`;
    this.el.style.right = "auto";
  }
}
