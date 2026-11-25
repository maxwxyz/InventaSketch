// Floating context bar near the selection (docs/en/design/interaction-ux.md section 5, same structure as the COM
// add-in's): the element's main action with a short label, up to three pictograms, then "⋯" (the pie menu with
// everything, grouped). Choices are dropdowns: the closed button shows the choice's pictogram, the current option's
// name or picture and a caret; the open list shows every option with pictogram or picture and label, the current one
// marked. A selection of two or more elements keeps its groups (one view for all | align and distribute | …).
// The bar fades with the pointer distance; once faded out it stays hidden until the selection changes.

import type { ActionInfo } from "../engine/types";
import { t } from "../i18n";
import { choiceFace, choiceTip, type ChoiceEntry } from "./barlogic";
import { clamp, h } from "./dom";
import { hasIcon, icon } from "./icons";

export type { ChoiceEntry } from "./barlogic";

export interface BarBox { left: number; top: number; right: number; bottom: number }

/**
 * A host command of the bar (make it …, values, insert, draw the path, select assembly …): a pictogram, or a short
 * `text` (values such as "F = 1") when given. `active` set (true or false) makes it a toggle button (aria-pressed).
 */
export interface BarCommand { id: string; label: string; icon: string; active?: boolean; text?: string }

/**
 * One button of the bar: a host command, an engine action or a choice (a dropdown). `label` shows a short label next
 * to the pictogram (the main action); `text` makes an action a text button (label-only pictograms).
 */
export type BarItem =
  | { kind: "command"; cmd: BarCommand; label?: string }
  | { kind: "action"; action: ActionInfo; label?: string; text?: string }
  | { kind: "choice"; choice: ChoiceEntry; label?: string };

/** Opacity for a pointer at distance d (px) from the bar: fully there within `near`, fading out until `far` (UX review G1). */
export function fadeOpacity(d: number, near = 90, far = 420): number {
  if (d <= near) return 1;
  if (d >= far) return 0;
  return clamp(1 - (d - near) / (far - near), 0, 1);
}

export function distanceToRect(x: number, y: number, r: { left: number; top: number; right: number; bottom: number }): number {
  const dx = Math.max(r.left - x, 0, x - r.right);
  const dy = Math.max(r.top - y, 0, y - r.bottom);
  return Math.hypot(dx, dy);
}

export class ContextBar {
  readonly el: HTMLDivElement;
  private key = "";
  private dismissed = false;
  /** Choice whose dropdown is open (closes with a pick, a click elsewhere or a new selection). */
  private openChoice?: string;
  /** Repeats the last update (an opened or closed dropdown). */
  private rerender?: () => void;
  private dropdown?: HTMLElement;
  /** Bar ids as last shown (command ids, "act:<id>", "choice:<key>", "more"), for checks and UI tests. */
  lastIds: string[] = [];
  /** Rows of the open dropdown as last shown ("id\tlabel[\t(current)]" per line). */
  lastDropItems = "";

  constructor(
    private readonly root: HTMLElement,
    private readonly onAction: (a: ActionInfo) => void,
    private readonly onMore: (clientX: number, clientY: number) => void,
    /** Host commands (BarCommand ids, "arr:<option>" for the multi-selection bar) with the click position. */
    private readonly onCommand: (id: string, clientX: number, clientY: number) => void,
    /** An option of a choice was picked. */
    private readonly onChoice: (key: string, optionId: string) => void,
  ) {
    this.el = h("div", { class: "isk-ctxbar", role: "toolbar", "aria-label": t("ctx.title") });
    this.el.hidden = true;
    this.el.addEventListener("pointerenter", () => (this.el.style.opacity = "1"));
    root.append(this.el);
    // A click elsewhere closes an open dropdown.
    root.addEventListener("pointerdown", (e) => {
      const target = e.target as Node | null;
      if (!this.openChoice || (target && (this.el.contains(target) || this.dropdown?.contains(target)))) return;
      this.toggleChoice(undefined);
    }, true);
    root.addEventListener("keydown", (e) => {
      if (e.key === "Escape" && this.openChoice) { e.preventDefault(); e.stopPropagation(); this.toggleChoice(undefined); }
    }, true);
  }

  /** A dropdown of the bar is open. */
  get dropdownOpen(): boolean {
    return !!this.openChoice;
  }

  /**
   * Shows the bar for a selection: the groups in order with a separator between non-empty ones, then "⋯".
   * `selectionKey` identifies the selection (a new selection makes a dismissed bar reappear).
   */
  show(selectionKey: string, groups: BarItem[][], box: BarBox | undefined, suppressed: boolean, ariaLabel = t("ctx.title")): void {
    this.rerender = () => this.show(selectionKey, groups, box, suppressed, ariaLabel);
    if (selectionKey !== this.key) {
      this.key = selectionKey;
      this.dismissed = false;
      this.openChoice = undefined;
    }
    if (!box || !selectionKey || this.dismissed || suppressed) {
      this.el.hidden = true;
      this.closeDropdown();
      return;
    }
    this.el.replaceChildren();
    this.el.setAttribute("aria-label", ariaLabel);
    this.lastIds = [];
    let openButton: HTMLElement | undefined;
    let openChoice: ChoiceEntry | undefined;
    for (const g of [...groups.filter((x) => x.length), [undefined]]) {
      if (this.el.childElementCount) this.el.append(h("span", { class: "isk-bar-sep", "aria-hidden": "true" }));
      for (const item of g) {
        if (!item) { this.el.append(this.moreButton()); continue; }
        const b = this.item(item);
        this.el.append(b);
        if (item.kind === "choice" && item.choice.key === this.openChoice) { openButton = b; openChoice = item.choice; }
      }
    }
    this.place(box);
    this.showDropdown(openChoice, openButton);
  }

  private moreButton(): HTMLButtonElement {
    const more = h("button", { class: "isk-btn isk-more-btn", type: "button", title: t("ctx.more"), "aria-label": t("ctx.more"), "data-cmd": "more" }, "⋯");
    more.addEventListener("click", (e) => this.onMore(e.clientX, e.clientY));
    this.lastIds.push("more");
    return more;
  }

  private item(item: BarItem): HTMLButtonElement {
    if (item.kind === "command") return this.command(item.cmd, item.label);
    if (item.kind === "action") return this.action(item.action, item.label, item.text);
    return this.choiceButton(item.choice, item.label);
  }

  /** Pictogram (or text) plus an optional short label. */
  private face(b: HTMLElement, iconId: string | undefined, label: string | undefined, text?: string): void {
    if (text) { b.classList.add("isk-text-btn"); b.append(h("span", { class: "isk-bar-text", text })); return; }
    if (iconId && hasIcon(iconId)) b.append(h("span", { class: "isk-ico", html: icon(iconId) }));
    if (label) { b.classList.add("isk-labelled"); b.append(h("span", { class: "isk-bar-label", text: label })); }
    else b.classList.add("isk-icon-btn");
  }

  private command(c: BarCommand, label?: string): HTMLButtonElement {
    const b = h("button", {
      class: `isk-btn${c.active ? " is-active" : ""}`, type: "button", title: c.label, "aria-label": c.label,
      "aria-pressed": c.active === undefined ? undefined : String(c.active), "data-cmd": c.id,
    });
    this.face(b, c.icon, label, c.text);
    b.addEventListener("click", (e) => this.onCommand(c.id, e.clientX, e.clientY));
    this.lastIds.push(c.id);
    return b;
  }

  private action(a: ActionInfo, label?: string, text?: string): HTMLButtonElement {
    const tip = a.input && !a.label.endsWith("…") ? `${a.label} …` : a.label;
    const b = h("button", {
      class: `isk-btn${a.active ? " is-active" : ""}`, type: "button", title: tip, "aria-label": tip,
      "aria-pressed": a.kind === "type" ? String(!!a.active) : undefined, disabled: !a.enabled, "data-cmd": `act:${a.id}`,
    });
    this.face(b, a.icon, label, text);
    b.addEventListener("click", () => this.onAction(a));
    this.lastIds.push(`act:${a.id}`);
    return b;
  }

  /** The closed dropdown of a choice: its face (pictogram, picture or short text), an optional label and a caret. */
  private choiceButton(c: ChoiceEntry, label?: string): HTMLButtonElement {
    const open = this.openChoice === c.key;
    const b = h("button", {
      class: `isk-btn isk-choice${open ? " is-active" : ""}`, type: "button", title: choiceTip(c, t("ctx.clickToChoose")),
      "aria-label": c.label, "aria-haspopup": "listbox", "aria-expanded": String(open), "data-cmd": `choice:${c.key}`,
    });
    const f = choiceFace(c);
    if (f.kind === "text") b.append(h("span", { class: "isk-choice-text", text: label ? `${label}: ${f.text}` : f.text }));
    else {
      if (f.kind === "icon") b.append(h("span", { class: `isk-ico${c.mirror ? " isk-mirror" : ""}`, html: icon(f.icon) }));
      else b.append(h("img", { src: f.image, alt: "" }));
      if (label) b.append(h("span", { class: "isk-bar-label", text: label }));
    }
    if (!c.menu) b.append(h("span", { class: "isk-caret", "aria-hidden": "true" }));
    b.addEventListener("click", () => this.toggleChoice(c.key));
    this.lastIds.push(`choice:${c.key}`);
    return b;
  }

  /** The list of an open choice under its button: one row per option with pictogram or picture and label, the current one marked. */
  private showDropdown(choice: ChoiceEntry | undefined, anchor: HTMLElement | undefined): void {
    this.closeDropdown();
    if (!choice || !anchor) return;
    const list = h("div", { class: "isk-dropdown", role: "listbox", "aria-label": choice.label });
    list.append(h("div", { class: "isk-drop-head", text: choice.label }));
    const rows: string[] = [];
    for (const o of choice.options) {
      const current = !choice.menu && o.id === choice.current;
      const row = h("button", { class: `isk-drop-row${current ? " is-active" : ""}`, type: "button", role: "option", "aria-selected": String(current), "data-option": o.id });
      if (o.image) row.append(h("img", { src: o.image, alt: "" }));
      else if (o.icon && hasIcon(o.icon)) row.append(h("span", { class: `isk-ico${choice.mirror ? " isk-mirror" : ""}`, html: icon(o.icon) }));
      else row.append(h("span", { class: "isk-drop-blank", "aria-hidden": "true" }));
      row.append(h("span", { text: o.label }));
      row.addEventListener("click", () => { this.openChoice = undefined; this.closeDropdown(); this.onChoice(choice.key, o.id); });
      list.append(row);
      rows.push(`${o.id}\t${o.label}${current ? "\t(current)" : ""}`);
    }
    this.lastDropItems = rows.join("\n");
    this.root.append(list);
    const rr = this.root.getBoundingClientRect();
    const ar = anchor.getBoundingClientRect();
    list.style.left = `${clamp(ar.left - rr.left, 4, Math.max(4, rr.width - list.offsetWidth - 4))}px`;
    // Below the button, or above it when the list does not fit below.
    let top = ar.bottom - rr.top + 4;
    if (top + list.offsetHeight > rr.height - 4) top = Math.max(4, ar.top - rr.top - list.offsetHeight - 4);
    list.style.top = `${top}px`;
    this.dropdown = list;
    (list.querySelector(".is-active") as HTMLElement | null ?? list.querySelector(".isk-drop-row") as HTMLElement | null)?.focus({ preventScroll: true });
  }

  private closeDropdown(): void {
    this.dropdown?.remove();
    this.dropdown = undefined;
  }

  private toggleChoice(key: string | undefined): void {
    this.openChoice = key === this.openChoice ? undefined : key;
    this.rerender?.();
  }

  private place(box: BarBox): void {
    this.el.hidden = false;
    this.el.style.opacity = "1";
    // Position above the selection, or below if there is no room; clamp into the pane.
    const rr = this.root.getBoundingClientRect();
    const bw = this.el.offsetWidth, bh = this.el.offsetHeight;
    const cx = (box.left + box.right) / 2 - rr.left;
    let top = box.top - rr.top - bh - 14;
    if (top < 4) top = box.bottom - rr.top + 14;
    this.el.style.left = `${clamp(cx - bw / 2, 4, Math.max(4, rr.width - bw - 4))}px`;
    this.el.style.top = `${clamp(top, 4, Math.max(4, rr.height - bh - 4))}px`;
  }

  /** Fades with the pointer distance; fully faded → hidden until the next selection. */
  pointerMoved(clientX: number, clientY: number): void {
    if (this.el.hidden || this.openChoice) return;
    const r = this.el.getBoundingClientRect();
    const o = fadeOpacity(distanceToRect(clientX, clientY, r));
    if (o <= 0) {
      this.dismissed = true;
      this.el.hidden = true;
      return;
    }
    this.el.style.opacity = String(Math.max(0.12, o));
  }

  hide(): void {
    this.el.hidden = true;
    this.openChoice = undefined;
    this.closeDropdown();
  }
}

export interface PromptRequest {
  /** Action label (title of the prompt). */
  title: string;
  /** Field label and prefilled value from the action's `input`. */
  label: string;
  value: string;
  unit?: string;
}

/**
 * Inline prompt for context actions that need a text value (bar or pie menu). It opens next to the selection inside the
 * pane; Enter applies, Esc or Cancel closes without a change.
 */
export class InlinePrompt {
  private el?: HTMLFormElement;
  private restoreFocus?: HTMLElement | null;

  constructor(private readonly root: HTMLElement) {}

  get open(): boolean {
    return !!this.el;
  }

  /** Shows the prompt near `at` (client coordinates); `onSubmit` receives the entered text. */
  show(req: PromptRequest, at: { x: number; y: number }, onSubmit: (value: string) => void): void {
    this.close();
    this.restoreFocus = document.activeElement as HTMLElement | null;
    const input = h("input", { class: "isk-input", type: "text", "aria-label": req.label, autocomplete: "off", spellcheck: "false" });
    input.value = req.value;
    const ok = h("button", { class: "isk-btn isk-primary", type: "submit" }, t("prompt.ok"));
    const cancel = h("button", { class: "isk-btn", type: "button" }, t("prompt.cancel"));
    const form = h("form", { class: "isk-popover isk-prompt", role: "dialog", "aria-label": req.title },
      h("div", { class: "isk-pop-head" }, h("strong", {}, req.title)),
      h("label", { class: "isk-field" }, h("span", { class: "isk-field-label" }, req.label),
        h("div", { class: "isk-row" }, input, req.unit ? h("span", { class: "isk-muted" }, req.unit) : false)),
      h("div", { class: "isk-row", style: "justify-content:flex-end" }, cancel, ok)) as HTMLFormElement;
    form.addEventListener("submit", (e) => {
      e.preventDefault();
      const value = input.value;
      this.close();
      onSubmit(value);
    });
    cancel.addEventListener("click", () => this.close());
    form.addEventListener("keydown", (e) => {
      if (e.key === "Escape") { e.preventDefault(); e.stopPropagation(); this.close(); }
    });
    this.root.append(form);
    this.el = form;
    const rr = this.root.getBoundingClientRect();
    const pos = promptPosition(at.x - rr.left, at.y - rr.top, form.offsetWidth, form.offsetHeight, rr.width, rr.height);
    form.style.left = `${pos.left}px`;
    form.style.top = `${pos.top}px`;
    form.style.right = "auto";
    input.focus();
    input.select();
  }

  close(): void {
    if (!this.el) return;
    this.el.remove();
    this.el = undefined;
    this.restoreFocus?.focus?.();
  }
}

/** Top-left of a w×h prompt centred below the point (x, y) of a pane of size pw×ph, kept inside the pane. */
export function promptPosition(x: number, y: number, w: number, h: number, pw: number, ph: number): { left: number; top: number } {
  const left = clamp(x - w / 2, 4, Math.max(4, pw - w - 4));
  let top = y + 12;
  if (top + h > ph - 4) top = y - h - 12;
  return { left, top: clamp(top, 4, Math.max(4, ph - h - 4)) };
}
