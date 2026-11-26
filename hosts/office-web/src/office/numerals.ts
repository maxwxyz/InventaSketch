// Reference numerals across slides (docs/en/modules/reference-numerals.md, "Across pages and slides"): the add-in keeps every
// slide in a model of its own, so before "Number all parts" and the compatibility check it reads the units of the other
// slides, lets the engine summarise their numerals (`numeralsOf`: numeral, part name, part kind) and passes them as
// `elsewhere`. The engine then continues numbering after them, gives identical parts their numeral and compares numerals
// between slides. This module only collects the summaries per slide; the rules are the engine's.

import type { Engine } from "../engine/client";
import type { NumeralInfo, OtherPageNumerals } from "../engine/types";
import type { SlideUnits } from "./slides";

/** The id of an action that numbers all parts (the page command, or the same menu of a numeral: "t:patent.numerals.auto=tens"). */
export function isNumberingAction(id: string): boolean {
  return /^(t:)?patent\.numerals\.auto=/.test(id);
}

/**
 * The numerals of every slide except the one with zero-based index `current` (labels: 1-based slide numbers), one engine
 * call for all payloads. Slides without units or numerals are left out; an engine without `numeralsOf` gives none.
 */
export function slideNumerals(engine: Engine, slides: readonly SlideUnits[], current?: number): OtherPageNumerals[] {
  const picked = slides.filter((s) => s.index !== current && s.units.length > 0);
  const payloads = picked.flatMap((s) => s.units.map((u) => u.payload));
  if (!payloads.length) return [];
  let found: NumeralInfo[][];
  try {
    found = engine.call<NumeralInfo[][]>("numeralsOf", { json: JSON.stringify(payloads) });
  } catch {
    return [];
  }
  const out: OtherPageNumerals[] = [];
  let i = 0;
  for (const s of picked) {
    const numerals = s.units.flatMap(() => found[i++] ?? []);
    if (numerals.length) out.push({ page: String(s.index + 1), numerals });
  }
  return out;
}

/** The numerals of the slides other than the one with zero-based index `index` (from `slideNumerals` of all slides). */
export function otherThan(all: readonly OtherPageNumerals[], index: number): OtherPageNumerals[] {
  const page = String(index + 1);
  return all.filter((p) => p.page !== page);
}
