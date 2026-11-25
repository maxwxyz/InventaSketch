// Radial (pie) context menu like the COM add-in's (docs/en/design/interaction-ux.md section 6): a ring of sectors with
// a pictogram over a label per entry, one label size, labels wrapped inside their sector (never inside a word), the
// element's name in the centre, a one-line hint below the ring for the hovered entry. Greyed entries keep their place;
// a click on them keeps the menu open. Submenus replace the ring in place (they start with "← Back").
// Keyboard: arrows/Tab move between entries, Enter/Space applies, Esc closes.

import { t } from "../i18n";
import { clamp, h, s } from "./dom";
import { hasIcon, icon } from "./icons";
import { PIE_FONT, PIE_LINES, pieGeometry, sectorPath, wrapLabel, type Measure, type PieEntry } from "./pielogic";

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
  /** Entries of the open ring as shown ("id\tlabel" per line), for checks and UI tests. */
  lastItems = "";

  constructor(
    private readonly root: HTMLElement,
    private readonly onPick: (entry: PieEntry) => void,
    /** The hovered or focused entry (undefined when none): previews such as the fainter glow of "Select connected". */
    private readonly onHover: (entry: PieEntry | undefined) => void = () => {},
    /** The menu closed (Esc, a click outside or on the centre, or a pick). */
    private readonly onClose: () => void = () => {},
  ) {}

  get open(): boolean {
    return !!this.el;
  }

  /** Centre of the open menu (client px), so submenus open in the same place. */
  get at(): { x: number; y: number } | undefined {
    return this.centre;
  }

  /** Shows the ring at the client point; `title` names what the menu acts on (the element, its part or the submenu). */
  show(entries: PieEntry[], clientX: number, clientY: number, title?: string): void {
    const restore = this.el ? this.restoreFocus : (document.activeElement as HTMLElement | null);
    this.remove();
    this.onHover(undefined);
    this.restoreFocus = restore;
    this.centre = { x: clientX, y: clientY };
    this.lastItems = entries.map((e) => `${e.id}\t${e.label}${e.enabled ? "" : "\t(off)"}`).join("\n");
    const n = entries.length;
    const { inner, outer, slots } = pieGeometry(n);
    const size = 2 * outer + 8;
    const hintRoom = 34;
    const rr = this.root.getBoundingClientRect();
    // The whole ring and its hint line stay inside the pane: smaller panes show the ring scaled down.
    const zoom = Math.min(1, (rr.width - 8) / size, (rr.height - 8) / (size + hintRoom));
    const half = (size * zoom) / 2;
    const cx = clamp(clientX - rr.left, half + 4, Math.max(half + 4, rr.width - half - 4));
    const cy = clamp(clientY - rr.top, half + 4, Math.max(half + 4, rr.height - half - hintRoom * zoom - 4));
    const family = getComputedStyle(this.root).fontFamily || "Segoe UI, sans-serif";
    const measure = measureAt(PIE_FONT, family);

    const menu = h("div", {
      class: "isk-pie", role: "menu", "aria-label": title ?? t("pie.title"),
      style: `left:${cx}px;top:${cy}px;width:${size}px;height:${size}px;transform:translate(-50%,-50%) scale(${zoom})`,
    });
    const ring = s("svg", { class: "isk-pie-ring", width: size, height: size, viewBox: `${-size / 2} ${-size / 2} ${size} ${size}`, "aria-hidden": "true" });
    ring.append(s("circle", { class: "isk-pie-disc", cx: 0, cy: 0, r: outer }));
    const hint = h("div", { class: "isk-pie-hint", role: "status", hidden: true });
    const centre = h("button", { class: "isk-pie-centre", type: "button", "aria-label": `${title ? `${title} – ` : ""}${t("close")}`, title: t("pie.closeHint") },
      h("span", { class: "isk-pie-title" }, title ?? ""));
    centre.style.width = centre.style.height = `${2 * inner - 6}px`;
    centre.addEventListener("click", () => this.close());

    const buttons: HTMLButtonElement[] = [];
    const sectors: SVGPathElement[] = [];
    const hover = (i: number) => {
      sectors.forEach((p, k) => p.classList.toggle("is-hover", k === i));
      const e = entries[i];
      hint.hidden = !e;
      if (e) {
        const full = e.label.replace(/\n/g, " ");
        const text = e.enabled ? (e.hint ? `${full} – ${e.hint}` : full) : `${full} (${t("pie.unavailable")})`;
        hint.textContent = text;
      }
      this.onHover(e?.enabled ? e : undefined);
    };
    entries.forEach((e, i) => {
      const slot = slots[i];
      const cls = `${e.active ? " is-active" : ""}${e.danger ? " is-danger" : ""}${e.enabled ? "" : " is-disabled"}`;
      const sector = s("path", { class: `isk-pie-sector${cls}`, d: sectorPath(i, n, inner + 2, outer - 2) });
      sectors.push(sector);
      ring.append(sector);
      if (n > 1) {
        const a = ((slot.angle - 180 / n) * Math.PI) / 180;
        ring.append(s("line", { class: "isk-pie-sep", x1: Math.cos(a) * (inner + 6), y1: Math.sin(a) * (inner + 6), x2: Math.cos(a) * (outer - 6), y2: Math.sin(a) * (outer - 6) }));
      }
      if (e.active) {
        const sweep = (2 * Math.PI) / Math.max(1, n);
        const a0 = (slot.angle * Math.PI) / 180 - sweep / 2 + 0.07, a1 = a0 + sweep - 0.14, r = outer - 3;
        ring.append(s("path", { class: "isk-pie-mark", d: `M${Math.cos(a0) * r} ${Math.sin(a0) * r}A${r} ${r} 0 0 1 ${Math.cos(a1) * r} ${Math.sin(a1) * r}` }));
      }
      const label = wrapLabel(e.label, slot.width, measure, PIE_LINES);
      const pic = e.image
        ? `<img class="isk-pie-img" src="${escapeAttr(e.image)}" alt="" draggable="false">`
        : e.icon && hasIcon(e.icon) ? `<span class="isk-ico${e.mirror ? " isk-mirror" : ""}">${icon(e.icon, 18)}</span>` : "";
      const b = h("button", {
        class: `isk-pie-item${cls}`,
        type: "button",
        role: "menuitem",
        "aria-label": e.hint ? `${e.label}. ${e.hint}` : e.label,
        "aria-disabled": e.enabled ? undefined : "true",
        "aria-checked": e.active ? "true" : undefined,
        "data-action": e.id,
        "data-full": label.complete ? "1" : "0",
        style: `left:${size / 2 + slot.x}px;top:${size / 2 + slot.y}px;width:${slot.width}px`,
        html: `${pic}<span class="isk-pie-lbl" style="font-size:${PIE_FONT}px">${label.lines.map(escapeHtml).join("<br>")}</span>`,
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
    menu.append(ring, ...buttons, centre, hint);
    hint.style.top = `${size + 2}px`;

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
    this.onHover(undefined);
    this.onClose();
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
