// Floating context bar near the selection (view switcher, text, flip, numeral, type actions, style, more). It becomes
// semi-transparent and fades out as the pointer moves away; once faded out it stays hidden until the selection changes.

import type { ActionInfo } from "../engine/types";
import { t } from "../i18n";
import { clamp, h } from "./dom";
import { hasIcon, icon } from "./icons";

export interface BarBox { left: number; top: number; right: number; bottom: number }

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

/** Bar buttons in order; "text" is the host's main-text command (objects with an editable main text). */
const BAR_IDS = ["text", "flipH", "flipV", "rotate90", "numeral"];

export class ContextBar {
  readonly el: HTMLDivElement;
  private key = "";
  private dismissed = false;

  constructor(
    private readonly root: HTMLElement,
    private readonly onAction: (a: ActionInfo) => void,
    private readonly onStyle: () => void,
    private readonly onMore: (clientX: number, clientY: number) => void,
  ) {
    this.el = h("div", { class: "isk-ctxbar", role: "toolbar", "aria-label": t("ctx.title") });
    this.el.hidden = true;
    this.el.addEventListener("pointerenter", () => (this.el.style.opacity = "1"));
    root.append(this.el);
  }

  /** Rebuilds and positions the bar; selectionKey identifies the selection (new selection → bar reappears). */
  update(selectionKey: string, actions: ActionInfo[], box: BarBox | undefined, suppressed: boolean): void {
    if (selectionKey !== this.key) {
      this.key = selectionKey;
      this.dismissed = false;
    }
    if (!box || !selectionKey || this.dismissed || suppressed) {
      this.el.hidden = true;
      return;
    }
    this.el.replaceChildren();
    const views = actions.filter((a) => a.kind === "view");
    if (views.length > 1) {
      const seg = h("div", { class: "isk-seg", role: "group", "aria-label": t("ctx.view") });
      for (const v of views) seg.append(this.button(v, v.label, v.active));
      this.el.append(seg);
    }
    for (const id of BAR_IDS) {
      const a = actions.find((x) => x.id === id);
      if (a) this.el.append(this.button(a));
    }
    for (const a of actions.filter((x) => x.kind === "type").slice(0, 3)) this.el.append(this.button(a));
    const style = h("button", { class: "isk-btn isk-icon-btn", type: "button", title: t("ctx.style"), "aria-label": t("ctx.style"), html: icon("style") });
    style.addEventListener("click", () => this.onStyle());
    const more = h("button", { class: "isk-btn isk-icon-btn", type: "button", title: t("ctx.more"), "aria-label": t("ctx.more"), html: icon("more") });
    more.addEventListener("click", (e) => this.onMore(e.clientX, e.clientY));
    this.el.append(style, more);
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
