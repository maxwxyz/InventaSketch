// Pure logic of the pie menu (no DOM, unit-tested): the eight fixed places, submenus with "← Back" and "More ▸", ring
// geometry and label wrapping. Same rules as the COM add-in's on-slide pie menu (docs/en/design/interaction-ux.md
// section 6: `PieSlots`, `PieRing`, `MaxSubItems`).

import type { ActionInfo, ParamInfo, ParamValue } from "../engine/types";

/** One pie entry: an engine action, a host command ("text", "insert" …) or a submenu ("menu:g:edit", "menu:c:<key>" …). */
export interface PieEntry {
  id: string;
  label: string;
  /** Pictogram id (icons.ts / engine IconIds). */
  icon?: string;
  /** Picture shown instead of a pictogram (option preview or library thumbnail as data URL). */
  image?: string;
  enabled: boolean;
  active: boolean;
  danger?: boolean;
  /** One line on what the entry does (shown below the ring while the entry is hovered or focused). */
  hint?: string;
  /** Option pictograms are drawn mirrored (start arrowheads). */
  mirror?: boolean;
  /** The engine action behind the entry, if any. */
  action?: ActionInfo;
}

/** Pie entry of an engine action (input actions get "…"). */
export function entryOf(a: ActionInfo): PieEntry {
  const label = a.input && !a.label.endsWith("…") ? `${a.label} …` : a.label;
  return { id: a.id, label, icon: a.icon, enabled: a.enabled, active: a.active, danger: a.id === "delete", action: a };
}

/** The eight places of the main ring, clockwise from 12 o'clock (top = text, right = insert, bottom = style, left = view). */
export const PIE_PLACES = ["text", "edit", "insert", "duplicate", "style", "delete", "view", "arrange"] as const;
export type PiePlace = (typeof PIE_PLACES)[number];

/** Entries per submenu ring besides "← Back"; longer groups continue in "More ▸". */
export const MAX_SUB_ITEMS = 11;

/** The content of one place: its entries and how the place is labelled when it opens a submenu. */
export interface PieGroup {
  key: PiePlace;
  label: string;
  icon?: string;
  items: PieEntry[];
  hint?: string;
  /** Opens a submenu even with a single entry (edit, style, arrange: the place keeps its name). */
  alwaysMenu?: boolean;
}

/** The first few entry labels of a group, as the hint of its submenu entry. */
export function summary(items: PieEntry[]): string {
  const labels = [...new Set(items.filter((i) => i.enabled).map((i) => i.label.replace(/ ▸$/, "").replace(/ …$/, "")))];
  return labels.slice(0, 4).join(", ") + (labels.length > 4 ? ", …" : "");
}

/** Adds a submenu, paged: more than `MAX_SUB_ITEMS` entries continue in "More ▸" (key + "+"). */
export function addMenu(menus: Map<string, PieEntry[]>, key: string, items: PieEntry[], moreLabel: string): void {
  if (items.length <= MAX_SUB_ITEMS) { menus.set(key, items); return; }
  const rest = items.slice(MAX_SUB_ITEMS - 1);
  menus.set(key, [...items.slice(0, MAX_SUB_ITEMS - 1), { id: `menu:g:${key}+`, label: moreLabel, icon: "more", enabled: true, active: false, hint: summary(rest) }]);
  addMenu(menus, `${key}+`, rest, moreLabel);
}

/**
 * The main ring: one entry per place (eight, fixed, in the order given). A group with exactly one entry shows it
 * directly (unless `alwaysMenu`), several entries become "Label ▸" opening a submenu (`menu:g:<key>`), none a greyed
 * entry that keeps the place (a click on it keeps the menu open).
 */
export function pieRing(groups: PieGroup[], moreLabel: string): { ring: PieEntry[]; menus: Map<string, PieEntry[]>; titles: Map<string, string> } {
  const menus = new Map<string, PieEntry[]>();
  const titles = new Map<string, string>();
  const ring: PieEntry[] = [];
  for (const g of groups) {
    titles.set(g.key, g.label.replace(/ ▸$/, "").replace(/ …$/, ""));
    if (g.items.length === 0) {
      ring.push({ id: `menu:g:${g.key}`, label: g.label, icon: g.icon, enabled: false, active: false, hint: g.hint });
      continue;
    }
    if (g.items.length === 1 && !g.alwaysMenu) {
      ring.push(g.items[0]);
      continue;
    }
    addMenu(menus, g.key, g.items, moreLabel);
    ring.push({ id: `menu:g:${g.key}`, label: g.label, icon: g.icon, enabled: true, active: false, hint: g.hint || summary(g.items) });
  }
  return { ring, menus, titles };
}

/** A submenu ring: "← Back" first, then the entries (paged like `addMenu`). */
export function withBack(back: PieEntry, items: PieEntry[]): PieEntry[] {
  return [back, ...items];
}

/** A pie entry of the label menu: what it changes when picked. */
export interface LabelChange { key: string; value?: ParamValue; prompt?: { label: string; value: string } }

/**
 * "Label ▸" submenu of lines, arrows, dimensions and lead lines, generated from the type's optional parameters
 * (module-agnostic, same rules as the COM add-in): text parameters open a prompt, switches toggle, choices open their
 * options ("menu:c:<key>", never a cycle), a text size offers larger/smaller. At most ten entries.
 */
export function labelEntries(params: ParamInfo[], text: { larger: string; smaller: string }): { entry: PieEntry; change?: LabelChange }[] {
  const out: { entry: PieEntry; change?: LabelChange }[] = [];
  for (const p of params) {
    if (out.length >= 10) break;
    const id = (s: string) => `lbl:${p.key}:${s}`;
    if (p.kind === "text") {
      const cur = typeof p.value === "string" ? p.value : typeof p.default === "string" ? p.default : "";
      out.push({ entry: { id: id("edit"), label: `${p.label} …`, icon: "cmd-text", enabled: true, active: false }, change: { key: p.key, prompt: { label: p.label, value: cur } } });
    } else if (p.kind === "boolean") {
      const cur = typeof p.value === "boolean" ? p.value : p.default === true;
      out.push({ entry: { id: id("toggle"), label: p.label, enabled: true, active: cur }, change: { key: p.key, value: !cur } });
    } else if (p.kind === "choice" && p.options && p.options.length > 1) {
      const cur = String(p.value ?? p.default ?? p.options[0].id);
      const curLabel = p.options.find((o) => o.id === cur)?.label ?? cur;
      out.push({ entry: { id: `menu:c:${p.key}`, label: `${p.label}: ${curLabel} ▸`, enabled: true, active: false } });
    } else if (p.kind === "number" && p.key === "textSize") {
      const cur = typeof p.value === "number" ? p.value : typeof p.default === "number" ? p.default : 3.5;
      const clamp = (v: number) => Math.round(Math.min(p.max ?? 1e9, Math.max(p.min ?? 0, v)) * 100) / 100;
      out.push({ entry: { id: id("larger"), label: text.larger, icon: "cmd-increase", enabled: true, active: false }, change: { key: p.key, value: clamp(cur * 1.2) } });
      out.push({ entry: { id: id("smaller"), label: text.smaller, icon: "cmd-decrease", enabled: true, active: false }, change: { key: p.key, value: clamp(cur / 1.2) } });
    }
  }
  return out;
}

// ------------------------------------------------------------------ geometry

export interface PieSlot { angle: number; x: number; y: number; width: number }

/** Label font size of every entry (one size, UX review G4). */
export const PIE_FONT = 11.5;
/** Label lines per entry before shortening (the full label is in the hint line). */
export const PIE_LINES = 3;

/**
 * Ring geometry for n entries (px): first entry at 12 o'clock, clockwise. Each slot centres its content block
 * (pictogram over label) in the middle of the ring depth; `width` is the room for the label: the sector chord where the
 * label lines sit, so labels stay inside their sector and the ring.
 */
export function pieGeometry(n: number): { inner: number; outer: number; slots: PieSlot[] } {
  const count = Math.max(1, n);
  const inner = 40;
  const outer = count <= 8 ? 150 : 168;
  const sweep = (2 * Math.PI) / count;
  const rc = (inner + outer) / 2 + 4;
  // The label lines sit a little inside the centre of the block (below the pictogram at the top slot).
  const rInner = rc - 8;
  const chord = count === 1 ? 2 * rInner : 2 * rInner * Math.sin(Math.min(sweep, Math.PI) / 2);
  const width = Math.max(44, Math.min(chord - 4, 96));
  const slots = Array.from({ length: n }, (_, i) => {
    const a = -Math.PI / 2 + i * sweep;
    return { angle: (a * 180) / Math.PI, x: Math.cos(a) * rc, y: Math.sin(a) * rc, width };
  });
  return { inner, outer, slots };
}

/** SVG path of the annular sector of slot i of n (centre 0,0). */
export function sectorPath(i: number, n: number, inner: number, outer: number): string {
  const count = Math.max(1, n);
  if (count === 1) {
    return `M${-outer} 0A${outer} ${outer} 0 1 0 ${outer} 0A${outer} ${outer} 0 1 0 ${-outer} 0Z`
      + `M${-inner} 0A${inner} ${inner} 0 1 1 ${inner} 0A${inner} ${inner} 0 1 1 ${-inner} 0Z`;
  }
  const sweep = (2 * Math.PI) / count;
  const a0 = -Math.PI / 2 - sweep / 2 + i * sweep, a1 = a0 + sweep;
  const p = (r: number, a: number) => `${(Math.cos(a) * r).toFixed(2)} ${(Math.sin(a) * r).toFixed(2)}`;
  const large = sweep > Math.PI ? 1 : 0;
  return `M${p(outer, a0)}A${outer} ${outer} 0 ${large} 1 ${p(outer, a1)}L${p(inner, a1)}A${inner} ${inner} 0 ${large} 0 ${p(inner, a0)}Z`;
}

// ------------------------------------------------------------------ labels

export type Measure = (text: string) => number;

/** Words of a label: split at ordinary spaces only, so a no-break space keeps "Insert ▸" or "Text …" together. */
const words = (text: string) => text.split(/[ \t\r\n]+/).filter(Boolean);

/** Rough text width for tests and environments without canvas: average glyph width × length. */
export function approxMeasure(fontSize: number): Measure {
  return (text) => text.length * fontSize * 0.56;
}

/** Shortens text with "…" to `width`, preferably at a word boundary. */
export function ellipsize(text: string, width: number, measure: Measure): string {
  if (measure(text) <= width) return text;
  const parts = words(text);
  for (let n = parts.length - 1; n >= 1; n--) {
    const s = `${parts.slice(0, n).join(" ")} …`;
    if (measure(s) <= width) return s;
  }
  let s = text;
  while (s.length > 1 && measure(`${s}…`) > width) s = s.slice(0, -1);
  return `${s.trimEnd()}…`;
}

/**
 * Wraps a label to `width` in at most `maxLines` lines without breaking inside a word. `complete` is false when the
 * label had to be shortened (a word wider than the line gives one shortened line; too many lines shorten the last).
 */
export function wrapLabel(text: string, width: number, measure: Measure, maxLines = 2): { lines: string[]; complete: boolean } {
  const parts = words(text);
  if (parts.length === 0) return { lines: [""], complete: true };
  if (parts.some((w) => measure(w) > width)) return { lines: [ellipsize(text, width, measure)], complete: false };
  const lines: string[] = [];
  let cur = "";
  for (const w of parts) {
    const next = cur ? `${cur} ${w}` : w;
    if (!cur || measure(next) <= width) cur = next;
    else { lines.push(cur); cur = w; }
  }
  lines.push(cur);
  if (lines.length <= maxLines) return { lines, complete: true };
  const kept = lines.slice(0, maxLines);
  kept[maxLines - 1] = ellipsize(lines.slice(maxLines - 1).join(" "), width, measure);
  return { lines: kept, complete: false };
}

/** Wraps with the first font size that shows the whole label (else the smallest size, shortened). */
export function fitLabel(text: string, width: number, measureAt: (fontSize: number) => Measure, sizes: number[] = [11, 10, 9]): { lines: string[]; complete: boolean; fontSize: number } {
  let last = { lines: [text], complete: false, fontSize: sizes[sizes.length - 1] ?? 10 };
  for (const size of sizes) {
    const r = wrapLabel(text, width, measureAt(size));
    last = { ...r, fontSize: size };
    if (r.complete) return last;
  }
  return last;
}
