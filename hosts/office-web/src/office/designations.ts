// Designations across slides (docs/en/design/interaction-ux.md section 6b, like the COM add-in's OnSlide/Continuations):
// the add-in reads the designations of every other slide's units (engine `designations` in a temporary session per slide)
// and passes the map "designation → slides" to the editor session (`setContinuations`), so a line that leaves the slide
// says where it continues ("→ W3, slide 4") and "Select connected" can report "Same name: also on slides 2, 4".
// The comparison form of a designation (case, leading IEC 81346 sign) is the engine's; this module only collects keys.

import type { Engine } from "../engine/client";
import type { DesignationInfo } from "../engine/types";
import { closeSession, slideSession, type SlideUnits } from "./slides";

/** The designations found on one slide (zero-based index). */
export interface SlideNames { index: number; keys: string[] }

/**
 * The continuation map for the slide with zero-based index `current`: every designation key found on another slide, with
 * the 1-based numbers of those slides in ascending order (as text, the engine's page labels). Slides without units and the
 * current slide add nothing.
 */
export function continuationMap(slides: readonly SlideNames[], current: number): Record<string, string[]> {
  const map = new Map<string, Set<number>>();
  for (const s of slides) {
    if (s.index === current) continue;
    for (const key of s.keys) {
      if (!key) continue;
      if (!map.has(key)) map.set(key, new Set());
      map.get(key)!.add(s.index + 1);
    }
  }
  const out: Record<string, string[]> = {};
  for (const key of [...map.keys()].sort()) out[key] = [...map.get(key)!].sort((a, b) => a - b).map(String);
  return out;
}

/** The comparison key of the designation element `id` carries in `list` (engine `designations`), if any. */
export function designationOf(list: readonly DesignationInfo[], id: string): DesignationInfo | undefined {
  return list.find((d) => d.ids.includes(id));
}

/** The other slides (1-based numbers as text) where designation `key` occurs; empty when nowhere else. */
export function alsoOn(map: Readonly<Record<string, string[]>>, key: string | undefined): string[] {
  return key ? map[key] ?? [] : [];
}

/** Two maps say the same (no need to tell the engine again). */
export function sameMap(a: Readonly<Record<string, string[]>> | undefined, b: Readonly<Record<string, string[]>>): boolean {
  if (!a) return false;
  const ka = Object.keys(a), kb = Object.keys(b);
  return ka.length === kb.length && ka.every((k) => (b[k] ?? []).join(",") === a[k].join(","));
}

/**
 * The designations of every slide's units: one temporary engine session per slide with units (as `checkSlides`), the
 * engine's `designations`, then closed. A slide whose units cannot be read adds nothing.
 */
export function slideDesignations(engine: Engine, slides: readonly SlideUnits[], page: { w: number; h: number }, lang: string): SlideNames[] {
  const out: SlideNames[] = [];
  for (const s of slides) {
    if (!s.units.length) { out.push({ index: s.index, keys: [] }); continue; }
    let session: ReturnType<typeof slideSession>["session"] | undefined;
    try {
      session = slideSession(engine, s.units, page, lang).session;
      out.push({ index: s.index, keys: session.designations().map((d) => d.key) });
    } catch {
      out.push({ index: s.index, keys: [] });
    } finally {
      if (session) closeSession(engine, session);
    }
  }
  return out;
}
