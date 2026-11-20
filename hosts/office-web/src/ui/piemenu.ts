// Radial (pie) context menu like the COM add-in's: a ring of sectors, delete at 12 o'clock, at most eight entries (the
// rest behind "More ▸"), each sector with a pictogram over a label wrapped without breaking words. Unavailable entries
// stay greyed in place; the full label of a shortened or unavailable entry appears in a pill below the ring.
// Keyboard: arrows/Tab move between entries, Enter/Space applies, Esc closes.

import { t } from "../i18n";
import { clamp, h, s } from "./dom";
import { hasIcon, icon } from "./icons";
import { fitLabel, pieGeometry, sectorPath, type Measure, type PieEntry } from "./pielogic";

let canvas: HTMLCanvasElement | undefined;

/** Text width in the pane's font (canvas), falling back to an estimate. */
function measureAt(fontSize: number, family: string): Measure {
  canvas ??= document.createElement("canvas");
  const ctx = canvas.getContext("2d");
  if (!ctx) return (text) => text.length * fontSize * 0.56;
  ctx.font = `${fontSize}px ${family}`;
  return (text) => ctx.measureText(text).width;
}

export class PieMenu {
  private el?: HTMLDivElement;
  private restoreFocus?: HTMLElement | null;
  private centre?: { x: number; y: number };

  constructor(private readonly root: HTMLElement, private readonly onPick: (entry: PieEntry) => void) {}

  get open(): boolean {
    return !!this.el;
  }

  /** Centre of the open menu (client px), so submenus open in the same place. */
  get at(): { x: number; y: number } | undefined {
    return this.centre;
  }

  show(entries: PieEntry[], clientX: number, clientY: number): void {
    const restore = this.el ? this.restoreFocus : (document.activeElement as HTMLElement | null);
    this.remove();
    this.restoreFocus = restore;
    this.centre = { x: clientX, y: clientY };
    const n = entries.length;
    const { inner, outer, slots } = pieGeometry(n);
    const size = 2 * outer + 8;
    const pillRoom = 30;
    const rr = this.root.getBoundingClientRect();
    const cx = clamp(clientX - rr.left, size / 2, Math.max(size / 2, rr.width - size / 2));
    const cy = clamp(clientY - rr.top, size / 2, Math.max(size / 2, rr.height - size / 2 - pillRoom));
    const family = getComputedStyle(this.root).fontFamily || "Segoe UI, sans-serif";

    const menu = h("div", { class: "isk-pie", role: "menu", "aria-label": t("pie.title"), style: `left:${cx}px;top:${cy}px;width:${size}px;height:${size}px` });
    const ring = s("svg", { class: "isk-pie-ring", width: size, height: size, viewBox: `${-size / 2} ${-size / 2} ${size} ${size}`, "aria-hidden": "true" });
    ring.append(s("circle", { class: "isk-pie-disc", cx: 0, cy: 0, r: outer }));
    const pill = h("div", { class: "isk-pie-pill", role: "status", hidden: true });
    const centre = h("button", { class: "isk-pie-centre", type: "button", "aria-label": t("close"), title: t("close"), html: icon("close", 14) });
    centre.addEventListener("click", () => this.close());

    const buttons: HTMLButtonElement[] = [];
    const sectors: SVGPathElement[] = [];
    const hover = (i: number) => {
      sectors.forEach((p, k) => p.classList.toggle("is-hover", k === i));
      const e = entries[i];
      const full = e ? buttons[i]?.dataset.full === "0" || !e.enabled : false;
      pill.hidden = !e || !full;
      if (e && full) pill.textContent = e.enabled ? e.label : `${e.label} (${t("pie.unavailable")})`;
    };
    entries.forEach((e, i) => {
      const slot = slots[i];
      const cls = `${e.active ? " is-active" : ""}${e.danger ? " is-danger" : ""}${e.enabled ? "" : " is-disabled"}`;
      const sector = s("path", { class: `isk-pie-sector${cls}`, d: sectorPath(i, n, inner + 2, outer - 2) });
      sectors.push(sector);
      ring.append(sector);
      if (n > 1) {
        const a = ((slot.angle - 180 / n) * Math.PI) / 180;
        ring.append(s("line", { class: "isk-pie-sep", x1: Math.cos(a) * (inner + 8), y1: Math.sin(a) * (inner + 8), x2: Math.cos(a) * (outer - 8), y2: Math.sin(a) * (outer - 8) }));
      }
      if (e.active) {
        const sweep = (2 * Math.PI) / Math.max(1, n);
        const a0 = (slot.angle * Math.PI) / 180 - sweep / 2 + 0.07, a1 = a0 + sweep - 0.14, r = outer - 3;
        ring.append(s("path", { class: "isk-pie-mark", d: `M${Math.cos(a0) * r} ${Math.sin(a0) * r}A${r} ${r} 0 0 1 ${Math.cos(a1) * r} ${Math.sin(a1) * r}` }));
      }
      const label = fitLabel(e.label, slot.width, (sz) => measureAt(sz, family), [11, 10, 9]);
      const pic = e.image
        ? `<img class="isk-pie-img" src="${escapeAttr(e.image)}" alt="" draggable="false">`
        : e.icon && hasIcon(e.icon) ? icon(e.icon, 18) : "";
      const b = h("button", {
        class: `isk-pie-item${cls}`,
        type: "button",
        role: "menuitem",
        title: e.label,
        "aria-label": e.label,
        "aria-disabled": e.enabled ? undefined : "true",
        "aria-checked": e.active ? "true" : undefined,
        "data-action": e.id,
        "data-full": label.complete ? "1" : "0",
        style: `left:${size / 2 + slot.x}px;top:${size / 2 + slot.y}px;width:${slot.width}px`,
        html: `${pic}<span class="isk-pie-lbl" style="font-size:${label.fontSize}px">${label.lines.map(escapeHtml).join("<br>")}</span>`,
      });
      const pick = () => {
        if (!e.enabled) return;
        // Submenus replace the ring in place; everything else closes the menu before it runs.
        if (!e.id.startsWith("menu:")) this.close();
        this.onPick(e);
      };
      b.addEventListener("click", pick);
      sector.addEventListener("click", pick);
      for (const el of [b, sector] as Element[]) {
        el.addEventListener("pointerenter", () => hover(i));
        el.addEventListener("pointerleave", () => hover(-1));
      }
      b.addEventListener("focus", () => hover(i));
      buttons.push(b);
    });
    ring.append(s("circle", { class: "isk-pie-hub", cx: 0, cy: 0, r: inner }));
    menu.append(ring, ...buttons, centre, pill);
    pill.style.top = `${size - 2}px`;

    menu.addEventListener("keydown", (ev) => {
      const i = buttons.indexOf(document.activeElement as HTMLButtonElement);
      if (ev.key === "Escape") { ev.preventDefault(); ev.stopPropagation(); this.close(); }
      else if (ev.key === "ArrowRight" || ev.key === "ArrowDown") { ev.preventDefault(); buttons[(i + 1 + buttons.length) % buttons.length]?.focus(); }
      else if (ev.key === "ArrowLeft" || ev.key === "ArrowUp") { ev.preventDefault(); buttons[(i - 1 + buttons.length) % buttons.length]?.focus(); }
    });
    const backdrop = h("div", { class: "isk-pie-backdrop" });
    backdrop.addEventListener("pointerdown", (ev) => { ev.preventDefault(); this.close(); });
    backdrop.addEventListener("contextmenu", (ev) => ev.preventDefault());
    const wrap = h("div", { class: "isk-pie-layer" }, backdrop, menu) as HTMLDivElement;
    this.root.append(wrap);
    this.el = wrap;
    (buttons.find((b) => !b.classList.contains("is-disabled")) ?? centre).focus({ preventScroll: true });
  }

  close(): void {
    if (!this.el) return;
    this.remove();
    this.restoreFocus?.focus?.({ preventScroll: true });
  }

  private remove(): void {
    this.el?.remove();
    this.el = undefined;
  }
}

function escapeHtml(text: string): string {
  return text.replace(/[&<>"]/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;" })[c]!);
}

function escapeAttr(text: string): string {
  return escapeHtml(text).replace(/'/g, "&#39;");
}
