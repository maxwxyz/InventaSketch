// Pure layout logic of the pie menu (no DOM, unit-tested): which entries go into the ring, which into "More ▸", where
// they sit and how their labels wrap. Same priority rules as the COM add-in's on-slide pie menu (PieLayout).

import type { ActionInfo, ParamInfo, ParamValue } from "../engine/types";

/** More entries than this make the ring unreadable; the rest goes into "More ▸". */
export const MAX_PIE_ITEMS = 8;

/** One pie entry: an engine action, a host command ("text", "ins:<itemId>") or a submenu ("menu:more" …). */
export interface PieEntry {
  id: string;
  label: string;
  /** Pictogram id (icons.ts / engine IconIds). */
  icon?: string;
  /** Picture shown instead of a pictogram (library thumbnail as data URL). */
  image?: string;
  enabled: boolean;
  active: boolean;
  danger?: boolean;
  /** The engine action behind the entry, if any. */
  action?: ActionInfo;
}

export interface PieCandidate { entry: PieEntry; essential: boolean }

/** Pie entry of an engine action (input actions get "…"). */
export function entryOf(a: ActionInfo): PieEntry {
  // A no-break space keeps the ellipsis with its word when the label wraps.
  const label = a.input && !a.label.endsWith("…") ? `${a.label} …` : a.label;
  return { id: a.id, label, icon: a.icon, enabled: a.enabled, active: a.active, danger: a.id === "delete", action: a };
}

/** Engine actions that never go into the main ring first (mirror, order, grouping, numeral). */
const SECONDARY = ["numeral", "flipH", "flipV", "group", "ungroup", "front", "back"];

/**
 * All entries of the main pie in priority order. Essential: delete, up to four views, text, insert, duplicate, rotate.
 * The rest (type actions, further views, numeral, mirror, order, grouping) may overflow into "More ▸"; among them
 * available entries come before unavailable ones.
 */
export function buildPieEntries(actions: ActionInfo[], extra: { text?: PieEntry; insert?: PieEntry; type?: PieEntry; label?: PieEntry } = {}): PieCandidate[] {
  const out: PieCandidate[] = [];
  const by = (id: string) => actions.find((a) => a.id === id);
  const del = by("delete");
  if (del) out.push({ entry: entryOf(del), essential: true });
  // Line types: "Type ▸" right after delete (like the COM add-in).
  if (extra.type) out.push({ entry: extra.type, essential: true });
  if (extra.label) out.push({ entry: extra.label, essential: true });
  const views = actions.filter((a) => a.kind === "view");
  if (views.length > 1) views.slice(0, 4).forEach((v) => out.push({ entry: entryOf(v), essential: true }));
  if (extra.text) out.push({ entry: extra.text, essential: true });
  if (extra.insert) out.push({ entry: extra.insert, essential: true });
  for (const id of ["duplicate", "rotate90"]) {
    const a = by(id);
    if (a) out.push({ entry: entryOf(a), essential: true });
  }
  const rest: ActionInfo[] = [
    ...actions.filter((a) => a.kind === "type"),
    ...(views.length > 1 ? views.slice(4) : []),
    ...SECONDARY.map(by).filter((a): a is ActionInfo => !!a),
  ];
  // Anything else the engine offers (future generic actions) still gets a place at the end.
  const known = new Set(["delete", "duplicate", "rotate90", ...SECONDARY]);
  rest.push(...actions.filter((a) => a.kind !== "type" && a.kind !== "view" && !known.has(a.id)));
  const ordered = [...rest.filter((a) => a.enabled), ...rest.filter((a) => !a.enabled)];
  for (const a of ordered) out.push({ entry: entryOf(a), essential: false });
  return out;
}

/**
 * Splits the entries into the ring (at most `max`: essential entries first, then the others in order) and the
 * overflow behind the `more` entry, so no entry appears twice. Same rule as the COM add-in's PieLayout, except that
 * essential entries without a place in the ring lead the overflow.
 */
export function pieSplit(all: PieCandidate[], more: PieEntry, max = MAX_PIE_ITEMS): { main: PieEntry[]; overflow: PieEntry[] } {
  if (all.length <= max) return { main: all.map((c) => c.entry), overflow: [] };
  const essentialAll = all.filter((c) => c.essential).map((c) => c.entry);
  const essential = essentialAll.slice(0, max - 1);
  const rest = [...essentialAll.slice(max - 1), ...all.filter((c) => !c.essential).map((c) => c.entry)];
  const room = max - 1 - essential.length;
  return { main: [...essential, ...rest.slice(0, room), more], overflow: rest.slice(room) };
}

/** A submenu ("More ▸", "Insert ▸"): Back first, paged with a further "More ▸" when it does not fit the ring. */
export function submenu(back: PieEntry, entries: PieEntry[], more: PieEntry, max = MAX_PIE_ITEMS): { main: PieEntry[]; overflow: PieEntry[] } {
  return pieSplit([{ entry: back, essential: true }, ...entries.map((entry) => ({ entry, essential: false }))], more, max);
}

/** A pie entry of the label menu: what it changes when picked. */
export interface LabelChange { key: string; value?: ParamValue; prompt?: { label: string; value: string } }

/**
 * "Label ▸" submenu of lines, arrows, dimensions and lead lines, generated from the type's optional parameters
 * (module-agnostic, same rules as the COM add-in): text parameters open a prompt, switches toggle, choices cycle to the
 * next option, a text size offers larger/smaller. At most ten entries.
 */
export function labelEntries(params: ParamInfo[], text: { larger: string; smaller: string }): { entry: PieEntry; change: LabelChange }[] {
  const out: { entry: PieEntry; change: LabelChange }[] = [];
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
      const i = p.options.findIndex((o) => o.id === cur);
      const next = p.options[(i + 1 + p.options.length) % p.options.length];
      const curLabel = i >= 0 ? p.options[i].label : cur;
      out.push({ entry: { id: id("cycle"), label: `${p.label}: ${curLabel}`, enabled: true, active: false }, change: { key: p.key, value: next.id } });
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

/**
 * Ring geometry for n entries (px): first entry at 12 o'clock, clockwise. Each slot centres the content block
 * (pictogram over label) at 55 % of the ring depth; `width` is the room for the label (sector chord).
 */
export function pieGeometry(n: number): { inner: number; outer: number; slots: PieSlot[] } {
  const count = Math.max(1, n);
  const inner = 28;
  const outer = count <= 6 ? 108 : count <= 8 ? 124 : 136;
  const sweep = (2 * Math.PI) / count;
  const rc = inner + (outer - inner) * 0.55;
  const chord = 2 * rc * Math.sin(Math.min(sweep, Math.PI) / 2);
  const width = Math.max(44, Math.min(chord - 6, 84));
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
    const s = `${parts.slice(0, n).join(" ")} …`;
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
