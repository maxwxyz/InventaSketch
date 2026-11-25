// Floating context bar near the selection. Fixed order (ROADMAP UX rule 2): views | choices | element actions | insert,
// path, assembly | transforms | style, more. Mirror/rotate appear only when they change the drawing; the bar never
// grows with content. A selection of two or more elements gets its own bar (one view for all, align, distribute …).
// It becomes semi-transparent and fades out as the pointer moves away; once faded out it stays hidden until the
// selection changes.

import type { ActionInfo } from "../engine/types";
import { t } from "../i18n";
import { clamp, h } from "./dom";
import { barLabel, hasIcon, icon, needsLabel } from "./icons";
import type { MultiBarGroups } from "./uxlogic";

export interface BarBox { left: number; top: number; right: number; bottom: number }

/** A host command of the bar (make it …, insert, draw the path, select assembly): icon button with a tooltip. */
/** A host command in the bar: a pictogram, or a short `text` (values such as "F = 1") when given. */
/** `active` set (true or false) makes it a toggle button (aria-pressed), e.g. motion mode. */
export interface BarCommand { id: string; label: string; icon: string; active?: boolean; text?: string }

/** Host commands of the single-selection bar: `lead` before the views, `build` after the element actions. */
export interface BarExtras { lead: BarCommand[]; build: BarCommand[] }

/** A choice shown as an icon button (current option) that opens its options as an icon row in the bar. */
export interface ChoiceEntry {
  key: string;
  label: string;
  current: string;
  mirror?: boolean;
  options: { id: string; label: string; icon?: string; image?: string }[];
}

/** Opacity for a pointer at distance d (px) from the bar: 1 near it, fading to 0 at `far`. */
export function fadeOpacity(d: number, near = 28, far = 240): number {
  if (d <= near) return 1;
  if (d >= far) return 0;
  return clamp(1 - (d - near) / (far - near), 0, 1);
}

export function distanceToRect(x: number, y: number, r: { left: number; top: number; right: number; bottom: number }): number {
  const dx = Math.max(r.left - x, 0, x - r.right);
  const dy = Math.max(r.top - y, 0, y - r.bottom);
  return Math.hypot(dx, dy);
}

/** Element actions in order; "text" is the host's main-text command (objects with an editable main text). */
const ELEMENT_IDS = ["text", "numeral"];
/** Quick transforms (the host leaves out the ones that change nothing, see uxlogic.filterTransforms). */
const TRANSFORM_IDS = ["flipH", "flipV", "rotate90"];

export class ContextBar {
  readonly el: HTMLDivElement;
  private key = "";
  private dismissed = false;
  /** Choice whose dropdown is open (closes with a pick, a click elsewhere or a new selection). */
  private openChoice?: string;
  /** Repeats the last update (an opened or closed dropdown). */
  private rerender?: () => void;

  constructor(
    private readonly root: HTMLElement,
    private readonly onAction: (a: ActionInfo) => void,
    private readonly onStyle: () => void,
    private readonly onMore: (clientX: number, clientY: number) => void,
    /** Host commands (BarCommand ids, "arr:<option>" for the multi-selection bar) with the click position. */
    private readonly onCommand: (id: string, clientX: number, clientY: number) => void = () => {},
    private readonly onChoice: (key: string, value: string) => void = () => {},
  ) {
    this.el = h("div", { class: "isk-ctxbar", role: "toolbar", "aria-label": t("ctx.title") });
    this.el.hidden = true;
    this.el.addEventListener("pointerenter", () => (this.el.style.opacity = "1"));
    root.append(this.el);
    // A click elsewhere closes an open dropdown.
    root.addEventListener("pointerdown", (e) => {
      const t = e.target as Node | null;
      if (!this.openChoice || (t && (this.el.contains(t) || this.dropdown?.contains(t)))) return;
      this.toggleChoice(undefined);
    }, true);
  }

  /** Starts an update: false (and the bar hidden) when there is nothing to show. */
  private begin(selectionKey: string, box: BarBox | undefined, suppressed: boolean): box is BarBox {
    if (selectionKey !== this.key) {
      this.key = selectionKey;
      this.dismissed = false;
      this.openChoice = undefined;
    }
    if (!box || !selectionKey || this.dismissed || suppressed) {
      this.el.hidden = true;
      this.dropdown?.remove();
      this.dropdown = undefined;
      return false;
    }
    this.el.replaceChildren();
    this.el.setAttribute("aria-label", t("ctx.title"));
    return true;
  }

  /** A thin separator before the next group (only between non-empty groups). */
  private group(...buttons: HTMLElement[]): void {
    if (!buttons.length) return;
    if (this.el.childElementCount) this.el.append(h("span", { class: "isk-bar-sep", "aria-hidden": "true" }));
    this.el.append(...buttons);
  }

  private command(c: BarCommand, prefix = ""): HTMLButtonElement {
    const b = c.text
      ? h("button", { class: `isk-btn${c.active ? " is-active" : ""}`, type: "button", title: c.label, "aria-label": c.label, "data-cmd": prefix + c.id }, c.text)
      : h("button", {
        class: `isk-btn isk-icon-btn${c.active ? " is-active" : ""}`, type: "button", title: c.label, "aria-label": c.label,
        "aria-pressed": c.active === undefined ? undefined : String(c.active),
        "data-cmd": prefix + c.id, html: icon(hasIcon(c.icon) ? c.icon : "type"),
      });
    b.addEventListener("click", (e) => this.onCommand(prefix + c.id, e.clientX, e.clientY));
    return b;
  }

  private tail(): HTMLElement[] {
    const style = h("button", { class: "isk-btn isk-icon-btn", type: "button", title: t("ctx.style"), "aria-label": t("ctx.style"), html: icon("style") });
    style.addEventListener("click", () => this.onStyle());
    const more = h("button", { class: "isk-btn isk-icon-btn", type: "button", title: t("ctx.more"), "aria-label": t("ctx.more"), html: icon("more") });
    more.addEventListener("click", (e) => this.onMore(e.clientX, e.clientY));
    return [style, more];
  }

  /**
   * Bar of a multi-selection (two or more elements): one view for all, centres on a row/column, even spacing, like the
   * first, renumber (engine `arrangeOptions`, each only when it changes something), then host commands, style and more.
   */
  updateMulti(selectionKey: string, groups: MultiBarGroups, box: BarBox | undefined, suppressed: boolean, extras: BarCommand[] = []): void {
    this.rerender = () => this.updateMulti(selectionKey, groups, box, suppressed, extras);
    if (!this.begin(selectionKey, box, suppressed)) return;
    this.el.setAttribute("aria-label", t("ctx.multi"));
    const asCommand = (o: { id: string; label: string; icon: string }) => this.command({ id: o.id, label: o.label, icon: o.icon }, "arr:");
    this.group(...groups.views.map(asCommand));
    this.group(...groups.arrange.map(asCommand));
    this.group(...extras.map((c) => this.command(c)));
    this.group(...this.tail());
    this.place(box);
  }

  /** Rebuilds and positions the bar; selectionKey identifies the selection (new selection → bar reappears). */
  update(selectionKey: string, actions: ActionInfo[], box: BarBox | undefined, suppressed: boolean, choices: ChoiceEntry[] = [], extras: BarExtras = { lead: [], build: [] }): void {
    this.rerender = () => this.update(selectionKey, actions, box, suppressed, choices, extras);
    if (!this.begin(selectionKey, box, suppressed)) return;
    this.group(...extras.lead.map((c) => this.command(c)));
    const views = actions.filter((a) => a.kind === "view");
    if (views.length > 1) {
      const seg = h("div", { class: "isk-seg", role: "group", "aria-label": t("ctx.view") });
      for (const v of views) seg.append(this.button(v, v.label, v.active));
      this.group(seg);
    }
    // Choices as dropdowns: current option as pictogram (or short text) plus a caret; the list shows icon and label.
    let openButton: HTMLElement | undefined;
    const chips: HTMLElement[] = [];
    for (const c of choices) {
      const cur = c.options.find((o) => o.id === c.current) ?? c.options[0];
      const pictorial = c.options.every((o) => o.icon || o.image);
      const b = h("button", {
        class: `isk-btn isk-choice${pictorial ? " isk-icon-btn" : ""}${this.openChoice === c.key ? " is-active" : ""}`,
        type: "button", title: `${c.label}: ${cur?.label ?? ""}`, "aria-label": c.label, "aria-haspopup": "listbox", "aria-expanded": String(this.openChoice === c.key),
      });
      if (pictorial) this.fillIcon(b, cur?.icon, cur?.image, c.mirror);
      else b.append(h("span", { class: "isk-choice-text", text: cur?.label ?? c.label }));
      b.append(h("span", { class: "isk-caret", "aria-hidden": "true" }));
      b.addEventListener("click", () => this.toggleChoice(c.key));
      if (this.openChoice === c.key) openButton = b;
      chips.push(b);
    }
    this.group(...chips);
    const covered = new Set(choices.map((c) => c.key));
    const byId = (ids: string[]) => ids.map((id) => actions.find((x) => x.id === id)).filter((a): a is ActionInfo => !!a).map((a) => this.button(a));
    // Label-only and step pictograms (switch, next variant, "+"/"−") are shown as short text: one pictogram, one meaning.
    const typeActions = actions.filter((x) => x.kind === "type" && !(x.paramKeys ?? []).some((k) => covered.has(k))).slice(0, 3)
      .map((a) => this.button(a, needsLabel(a.icon) ? barLabel(a.label) : undefined));
    this.group(...byId(ELEMENT_IDS), ...typeActions);
    this.group(...extras.build.map((c) => this.command(c)));
    this.group(...byId(TRANSFORM_IDS));
    this.group(...this.tail());
    this.place(box);
    this.showDropdown(choices.find((c) => c.key === this.openChoice), openButton);
  }

  /** The list of an open choice under its button: one row per option with icon and label, the current one marked. */
  private showDropdown(choice: ChoiceEntry | undefined, anchor: HTMLElement | undefined): void {
    this.dropdown?.remove();
    this.dropdown = undefined;
    if (!choice || !anchor) return;
    const list = h("div", { class: "isk-dropdown", role: "listbox", "aria-label": choice.label });
    for (const o of choice.options) {
      const row = h("button", { class: `isk-drop-row${o.id === choice.current ? " is-active" : ""}`, type: "button", role: "option", "aria-selected": String(o.id === choice.current) });
      this.fillIcon(row, o.icon, o.image, choice.mirror);
      row.append(h("span", { text: o.label }));
      row.addEventListener("click", () => { this.openChoice = undefined; this.dropdown?.remove(); this.dropdown = undefined; this.onChoice(choice.key, o.id); });
      list.append(row);
    }
    this.root.append(list);
    const rr = this.root.getBoundingClientRect();
    const ar = anchor.getBoundingClientRect();
    list.style.left = `${clamp(ar.left - rr.left, 4, Math.max(4, rr.width - list.offsetWidth - 4))}px`;
    list.style.top = `${ar.bottom - rr.top + 4}px`;
    this.dropdown = list;
  }

  private dropdown?: HTMLElement;

  private toggleChoice(key: string | undefined): void {
    this.openChoice = key === this.openChoice ? undefined : key;
    this.rerender?.();
  }

  private fillIcon(b: HTMLElement, iconId?: string, image?: string, mirror?: boolean): void {
    if (image) b.append(h("img", { src: image, alt: "" }));
    else if (iconId && hasIcon(iconId)) {
      const span = h("span", { class: `isk-ico${mirror ? " isk-mirror" : ""}`, html: icon(iconId) });
      b.append(span);
    }
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

  private button(a: ActionInfo, text?: string, pressed?: boolean): HTMLButtonElement {
    const b = h("button", {
      class: `isk-btn ${text ? "isk-seg-btn" : "isk-icon-btn"} ${a.active ? "is-active" : ""}`,
      type: "button",
      title: a.label,
      "aria-label": a.label,
      "aria-pressed": pressed !== undefined || a.kind === "type" ? String(!!(pressed ?? a.active)) : undefined,
      disabled: !a.enabled,
      html: text ? undefined : hasIcon(a.icon) ? icon(a.icon) : icon("type"),
    }, text ?? "");
    b.addEventListener("click", () => this.onAction(a));
    return b;
  }

  /** Fades with the pointer distance; fully faded → hidden until the next selection. */
  pointerMoved(clientX: number, clientY: number): void {
    if (this.el.hidden) return;
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
