// Parts of an element (charts v2, docs/en/design/charts-v2.md): pure helpers for the bar and pie menu of a selected part.
import type { ActionInfo, PartView } from "../engine/types";
import { entryOf, type PieEntry } from "./pielogic";

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

/** Pie entries of a selected part: its actions, then "Parts ▸" and "Whole element". */
export function partPieEntries(actions: ActionInfo[], partsLabel: string, wholeLabel: string): PieEntry[] {
  return [
    ...partActions(actions).map(entryOf),
    { id: "menu:parts", label: partsLabel, icon: "view-table", enabled: true, active: false },
    { id: PART_PREFIX + "none", label: wholeLabel, icon: "cmd-select-assembly", enabled: true, active: false },
  ];
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
