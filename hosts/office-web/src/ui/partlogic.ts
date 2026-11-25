// Parts of an element (charts v2, docs/en/design/charts-v2.md): pure helpers for the bar and pie menu of a selected part.
import type { ActionInfo, PartView } from "../engine/types";
import type { PieEntry } from "./pielogic";

/** Prefix of part action ids in the bar and pie ("part:part.hide"); the engine id follows it. */
export const PART_PREFIX = "part:";

/** The selected part of the single selected element. */
export interface SelectedPart {
  id: string;
  key: string;
  label: string;
}

/** True for a bar/pie id that belongs to the part mode. */
export function isPartCommand(id: string): boolean {
  return id.startsWith(PART_PREFIX) || id.startsWith("partsel:") || id === "partlist";
}

/** Engine part actions as bar/pie actions (ids prefixed, so they never clash with element actions). */
export function partActions(actions: ActionInfo[]): ActionInfo[] {
  return actions.map((a) => ({ ...a, id: PART_PREFIX + a.id }));
}

/** The engine action id of a prefixed part action id, or undefined. */
export function partActionId(id: string): string | undefined {
  return id.startsWith(PART_PREFIX) ? id.slice(PART_PREFIX.length) : undefined;
}

/** "Parts ▸": every part of the element (hidden ones marked), choosing one selects it. */
export function partListEntries(parts: PartView[], current: string | undefined, hiddenSuffix: string): PieEntry[] {
  return parts.map((p) => ({ id: "partsel:" + p.key, label: p.label + (p.hidden ? hiddenSuffix : ""), icon: "", enabled: true, active: p.key === current }));
}

/** The part to keep after an edit: the same element still alone in the selection and the part still listed. */
export function keepPart(part: SelectedPart | undefined, selection: string[], parts: () => PartView[]): SelectedPart | undefined {
  if (!part || selection.length !== 1 || selection[0] !== part.id) return undefined;
  const now = parts().find((p) => p.key === part.key);
  return now ? { ...part, label: now.label } : undefined;
}

/** What the Del key runs on a selected part: its own delete action, else hiding it; undefined when neither applies. */
export function partDeleteAction(actions: ActionInfo[]): string | undefined {
  if (actions.some((a) => a.id === "part.delete" && a.enabled)) return "part.delete";
  if (actions.some((a) => a.id === "part.hide" && a.enabled && !a.active)) return "part.hide";
  return undefined;
}

/** The one part key in `after` that was not in `before` (an area just added by a menu choice), else undefined. */
export function addedPart(before: string[], after: string[]): string | undefined {
  const old = new Set(before);
  const added = [...new Set(after.filter((k) => !old.has(k)))];
  return added.length === 1 ? added[0] : undefined;
}

/** The part selected after `part` was deleted: a point leaves its curve selected ("point.0.3" to "series.0"), else none. */
export function partAfterDelete(part: string): string | undefined {
  const seg = part.split(".");
  return seg.length === 3 && seg[0] === "point" ? `series.${seg[1]}` : undefined;
}

/**
 * Whether a grip is shown (COM `ActionChoiceBar.ShownWith`): grips without a group always; grips that come with a part
 * (the point grips of a curve) only while that part, or a part of the same group (a point of that curve), is selected.
 */
export function gripShown(grip: { part?: string | null; showWith?: string | null }, selectedPart: string | undefined, all: { part?: string | null; showWith?: string | null }[]): boolean {
  if (!grip.showWith) return true;
  if (!selectedPart) return false;
  if (grip.part === selectedPart) return true;
  const group = all.find((g) => g.part === selectedPart && g.showWith)?.showWith ?? selectedPart;
  return grip.showWith === group;
}
