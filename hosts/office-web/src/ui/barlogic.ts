// Pure rules of the floating bar (no DOM, unit-tested), the same as the COM add-in's (docs/en/design/interaction-ux.md
// sections 5 and 5a): choices among actions become one dropdown each, never a cycle button; the bar shows the element's
// main action with a short label, up to three pictograms and "⋯".

import type { ActionInfo } from "../engine/types";
import { hasIcon } from "./icons";

/** Key prefix of an element's action choices in the bar and pie ("@a.chart.area.add"); picking runs the option action. */
export const ELEMENT_CHOICE = "@a.";
/** Key prefix of a selected part's choices ("@p.chart.curve.shape"); picking runs the part action. */
export const PART_CHOICE = "@p.";

/** One option of a choice: a pictogram (`icon`) or a small picture (`image`, data URL), and its label. */
export interface ChoiceOption { id: string; label: string; icon?: string; image?: string }

/**
 * A choice shown as one dropdown: parameter choices (key = parameter), the line type ("@type"), the line style
 * ("@dash"), the view ("@view"), element action choices ("@a.<choice>") and part choices ("@p.<choice>").
 */
export interface ChoiceEntry {
  key: string;
  /** What is chosen ("Curve shape"). */
  label: string;
  /** Id of the current option ("" for a menu). */
  current: string;
  /** Option pictograms are drawn mirrored (start arrowheads). */
  mirror?: boolean;
  /** The options are commands, nothing is current ("Fill to ▸", "Add area ▸"). */
  menu?: boolean;
  /** The closed button shows the current option's name instead of its picture. */
  labelInBar?: boolean;
  /** Pictogram of the choice itself for the closed button. */
  barIcon?: string;
  options: ChoiceOption[];
}

/** Label-only markers (engine IconIds.IsLabelOnly: switch, next variant, pick option): they never stand alone. */
const LABEL_ONLY = new Set(["cmd-toggle", "cmd-cycle", "cmd-option"]);

/**
 * A pictogram that says something (COM `IsTelling`): known and not a label-only marker. Step pictograms ("+"/"−",
 * one more/fewer) count, but are drawn with their label (`isStep`).
 */
export function isTelling(icon: string | undefined): icon is string {
  return !!icon && hasIcon(icon) && !LABEL_ONLY.has(icon) && icon !== "type";
}


/**
 * Groups the choices among `actions` (engine fields `choice`, `choiceLabel`, `choiceMenu`, `choiceLabelInBar`,
 * `choiceBarIcon`, `previewSvg`): one entry per choice, at the place of its first option, with keys `prefix` + choice
 * key. Options keep a telling pictogram, else their preview picture (`toImage` turns the SVG into a data URL). Every
 * other action stays in `plain`, in order.
 */
export function groupActionChoices(actions: ActionInfo[], prefix: string, toImage: (svg: string) => string): { choices: ChoiceEntry[]; plain: ActionInfo[] } {
  const choices: ChoiceEntry[] = [];
  const byKey = new Map<string, ChoiceEntry>();
  const plain: ActionInfo[] = [];
  for (const a of actions) {
    if (!a.choice) { plain.push(a); continue; }
    let c = byKey.get(a.choice);
    if (!c) {
      c = {
        key: prefix + a.choice, label: a.choiceLabel ?? a.label, current: "", menu: !!a.choiceMenu,
        labelInBar: !!a.choiceLabelInBar, barIcon: isTelling(a.choiceBarIcon) ? a.choiceBarIcon : undefined, options: [],
      };
      byKey.set(a.choice, c);
      choices.push(c);
    }
    const label = a.input && !a.label.endsWith("…") ? `${a.label} …` : a.label;
    const telling = isTelling(a.icon);
    c.options.push({ id: a.id, label, icon: telling ? a.icon : undefined, image: !telling && a.previewSvg ? toImage(a.previewSvg) : undefined });
    if (a.active && !c.menu && !c.current) c.current = a.id;
  }
  return { choices, plain };
}

/** The actions in their order with every choice once, at the place of its first option (COM `ActionChoiceBar.Entries`). */
export function actionEntries(actions: ActionInfo[], prefix: string, toImage: (svg: string) => string): ({ action: ActionInfo } | { choice: ChoiceEntry })[] {
  const { choices } = groupActionChoices(actions, prefix, toImage);
  const out: ({ action: ActionInfo } | { choice: ChoiceEntry })[] = [];
  const seen = new Set<string>();
  for (const a of actions) {
    if (!a.choice) { out.push({ action: a }); continue; }
    if (seen.has(a.choice)) continue;
    seen.add(a.choice);
    const c = choices.find((x) => x.key === prefix + a.choice);
    if (c) out.push({ choice: c });
  }
  return out;
}

/** The current option of a choice (none for a menu; the first option when the engine marks none). */
export function currentOption(c: ChoiceEntry): ChoiceOption | undefined {
  if (c.menu) return undefined;
  return c.options.find((o) => o.id === c.current) ?? c.options[0];
}

/** Every option has a pictogram or picture (and the closed button is not text by rule). */
export function isPictorial(c: ChoiceEntry): boolean {
  if (c.menu || c.labelInBar) return false;
  return !!c.barIcon || (c.options.length > 0 && c.options.every((o) => o.icon || o.image));
}

/** What the closed dropdown button shows. */
export type ChoiceFace = { kind: "icon"; icon: string } | { kind: "image"; image: string } | { kind: "text"; text: string };

/**
 * The closed button of a choice: the choice's own pictogram if it has one, else the current option's name when
 * `labelInBar`, else the current option's pictogram or picture; menus show "Label ▸"; options without pictures show the
 * current option's name.
 */
export function choiceFace(c: ChoiceEntry): ChoiceFace {
  if (c.menu) return { kind: "text", text: `${c.label} ▸` };
  if (c.barIcon) return { kind: "icon", icon: c.barIcon };
  const cur = currentOption(c);
  if (c.labelInBar || !isPictorial(c)) return { kind: "text", text: cur?.label ?? c.label };
  if (cur?.icon) return { kind: "icon", icon: cur.icon };
  if (cur?.image) return { kind: "image", image: cur.image };
  return { kind: "text", text: cur?.label ?? c.label };
}

/** Tooltip of a closed choice: "Label: Current – click to choose" ("Label …" for a menu). */
export function choiceTip(c: ChoiceEntry, clickToChoose: string): string {
  const cur = currentOption(c);
  return `${c.label}${cur ? `: ${cur.label}` : " …"} – ${clickToChoose}`;
}

/** "Label: Current ▸" (a state choice) or "Label ▸" (a menu) for the pie. */
export function choicePieLabel(c: ChoiceEntry): string {
  const cur = currentOption(c);
  return cur ? `${c.label}: ${cur.label} ▸` : `${c.label} ▸`;
}

/** What picking option `id` of choice `key` does. */
export type ChoicePick =
  | { kind: "action"; id: string }
  | { kind: "part"; id: string }
  | { kind: "view"; id: string }
  | { kind: "convert"; typeId: string }
  | { kind: "dash"; dash: string }
  | { kind: "param"; key: string; value: string };

export function choicePick(key: string, id: string): ChoicePick {
  if (key.startsWith(ELEMENT_CHOICE)) return { kind: "action", id };
  if (key.startsWith(PART_CHOICE)) return { kind: "part", id };
  if (key === "@view") return { kind: "view", id };
  if (key === "@type") return { kind: "convert", typeId: id };
  if (key === "@dash") return { kind: "dash", dash: id };
  return { kind: "param", key, value: id };
}

/** Pictograms the bar and pie already have as generic commands of their own. */
const NOT_IN_BAR = new Set(["delete", "cmd-delete", "duplicate", "cmd-duplicate", "flipH", "flipV", "cmd-mirror-h", "cmd-mirror-v", "rotate", "cmd-rotate", "cmd-rotate-ccw"]);

/** Step pictograms ("+"/"−", one more/fewer) are drawn with their label, so several pairs stay apart. */
export function isStep(icon: string): boolean {
  return ["cmd-increase", "cmd-decrease", "cmd-count-more", "cmd-count-fewer"].includes(icon);
}

/**
 * Type actions that may go into the bar (COM `BarActions`): enabled, not a choice option, not covered by a parameter
 * choice the bar shows, not one of the generic commands; a pictogram only when it is telling and no other usable action
 * shares it (step pictograms excepted, they keep their label). Pictorial ones first, at most `max`.
 */
export function barActions(actions: ActionInfo[], coveredParams: Set<string>, hasTextButton: boolean, max = 5): { action: ActionInfo; pictorial: boolean }[] {
  const usable = actions.filter((a) => a.kind === "type" && a.enabled && !a.choice && !(a.paramKeys ?? []).some((k) => coveredParams.has(k))
    && !NOT_IN_BAR.has(a.icon) && !(hasTextButton && a.icon === "cmd-text"));
  const count = new Map<string, number>();
  for (const a of usable) count.set(a.icon, (count.get(a.icon) ?? 0) + 1);
  const pictorial = (a: ActionInfo) => isTelling(a.icon) && ((count.get(a.icon) ?? 0) < 2 || isStep(a.icon));
  return [
    ...usable.filter(pictorial).map((action) => ({ action, pictorial: true })),
    ...usable.filter((a) => !pictorial(a)).map((action) => ({ action, pictorial: false })),
  ].slice(0, max);
}

/** At most this many buttons in the bar before "⋯" (the main action plus three pictograms). */
export const MAX_BAR_BUTTONS = 4;

/**
 * The bar of one element from its candidates in order of use: the first one marked main, then up to three others
 * (duplicates by id dropped). Returns the ids in bar order.
 */
export function pickBar<T extends { id: string; main?: boolean }>(candidates: T[], max = MAX_BAR_BUTTONS): { main?: T; icons: T[] } {
  const main = candidates.find((c) => c.main);
  const seen = new Set(main ? [main.id] : []);
  const icons: T[] = [];
  for (const c of candidates) {
    if (c.main || seen.has(c.id)) continue;
    seen.add(c.id);
    icons.push(c);
    if (icons.length >= max - 1) break;
  }
  return { main, icons };
}

/** A short label (no cut words where possible): at most `max` characters with "…". */
export function shortLabel(label: string, max = 20): string {
  const s = label.replace(/\s*\(.*\)$/, "").trim();
  if (s.length <= max) return s;
  const cut = s.lastIndexOf(" ", max - 1);
  return (cut > max / 2 ? s.slice(0, cut) : s.slice(0, max - 1)).trimEnd() + "…";
}
