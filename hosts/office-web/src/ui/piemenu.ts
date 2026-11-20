// Radial (pie) context menu. Delete always sits at the top; unavailable actions are shown greyed out.
// Keyboard: arrows/Tab move between items, Enter/Space applies, Esc closes.

import type { ActionInfo } from "../engine/types";
import { t } from "../i18n";
import { clamp, h } from "./dom";
import { hasIcon, icon } from "./icons";

export interface PieLayoutItem { action: ActionInfo; angle: number; x: number; y: number }

/** Places items on a circle, first item (delete) at 12 o'clock, clockwise. Radius grows with the item count. */
export function pieLayout(actions: ActionInfo[], itemSize = 40): { radius: number; items: PieLayoutItem[] } {
  const n = Math.max(1, actions.length);
  const radius = Math.max(62, (n * (itemSize + 8)) / (2 * Math.PI));
  const items = actions.map((action, i) => {
    const angle = -90 + (360 * i) / n;
    const a = (angle * Math.PI) / 180;
    return { action, angle, x: Math.cos(a) * radius, y: Math.sin(a) * radius };
  });
  return { radius, items };
}

export class PieMenu {
  private el?: HTMLDivElement;
  private restoreFocus?: HTMLElement | null;

  constructor(private readonly root: HTMLElement, private readonly onPick: (a: ActionInfo) => void) {}

  get open(): boolean {
    return !!this.el;
  }

  show(actions: ActionInfo[], clientX: number, clientY: number): void {
    this.close();
    this.restoreFocus = document.activeElement as HTMLElement | null;
    const { radius, items } = pieLayout(actions);
    const size = 2 * radius + 56;
    const rr = this.root.getBoundingClientRect();
    const cx = clamp(clientX - rr.left, size / 2 - 40, Math.max(size / 2 - 40, rr.width - size / 2 + 40));
    const cy = clamp(clientY - rr.top, size / 2 - 20, Math.max(size / 2 - 20, rr.height - size / 2 + 20));
    const label = h("div", { class: "isk-pie-label", "aria-live": "polite" }, t("pie.title"));
    const menu = h("div", { class: "isk-pie", role: "menu", "aria-label": t("pie.title"), style: `left:${cx}px;top:${cy}px;width:${size}px;height:${size}px` });
    const ring = h("div", { class: "isk-pie-ring", style: `width:${2 * radius + 44}px;height:${2 * radius + 44}px` });
    const centre = h("button", { class: "isk-pie-centre", type: "button", "aria-label": t("close"), html: icon("close", 16) });
    centre.addEventListener("click", () => this.close());
    menu.append(ring, label, centre);
    const buttons: HTMLButtonElement[] = [];
    for (const it of items) {
      const a = it.action;
      const b = h("button", {
        class: `isk-pie-item ${a.active ? "is-active" : ""} ${a.id === "delete" ? "is-danger" : ""}`,
        type: "button",
        role: "menuitem",
        title: a.label,
        "aria-label": a.label,
        "aria-disabled": a.enabled ? undefined : "true",
        "data-action": a.id,
        style: `left:calc(50% + ${it.x}px);top:calc(50% + ${it.y}px)`,
        html: hasIcon(a.icon) ? icon(a.icon, 18) : `<span class="isk-pie-text">${escapeHtml(a.label.slice(0, 2))}</span>`,
      });
      if (!a.enabled) b.classList.add("is-disabled");
      const showLabel = () => (label.textContent = a.enabled ? a.label : `${a.label} (${t("pie.unavailable")})`);
      b.addEventListener("pointerenter", showLabel);
      b.addEventListener("focus", showLabel);
      b.addEventListener("click", () => {
        if (!a.enabled) return;
        this.close();
        this.onPick(a);
      });
      buttons.push(b);
      menu.append(b);
    }
    menu.addEventListener("keydown", (e) => {
      const i = buttons.indexOf(document.activeElement as HTMLButtonElement);
      if (e.key === "Escape") { e.preventDefault(); e.stopPropagation(); this.close(); }
      else if (e.key === "ArrowRight" || e.key === "ArrowDown") { e.preventDefault(); buttons[(i + 1 + buttons.length) % buttons.length]?.focus(); }
      else if (e.key === "ArrowLeft" || e.key === "ArrowUp") { e.preventDefault(); buttons[(i - 1 + buttons.length) % buttons.length]?.focus(); }
    });
    const backdrop = h("div", { class: "isk-pie-backdrop" });
    backdrop.addEventListener("pointerdown", (e) => { e.preventDefault(); this.close(); });
    backdrop.addEventListener("contextmenu", (e) => e.preventDefault());
    const wrap = h("div", { class: "isk-pie-layer" }, backdrop, menu) as HTMLDivElement;
    this.root.append(wrap);
    this.el = wrap;
    (buttons.find((b) => !b.classList.contains("is-disabled")) ?? centre).focus();
  }

  close(): void {
    if (!this.el) return;
    this.el.remove();
    this.el = undefined;
    this.restoreFocus?.focus?.();
  }
}

function escapeHtml(s: string): string {
  return s.replace(/[&<>"]/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;" })[c]!);
}
