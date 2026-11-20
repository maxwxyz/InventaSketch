import { describe, expect, it } from "vitest";
import type { ActionInfo } from "../src/engine/types";
import {
  approxMeasure, buildPieEntries, ellipsize, labelEntries, fitLabel, MAX_PIE_ITEMS, pieGeometry, pieSplit, submenu, wrapLabel, type PieEntry,
} from "../src/ui/pielogic";
import { resizeFrame, scaleLabel } from "../src/ui/stage";
import strings from "../src/strings.json";

const a = (id: string, kind: ActionInfo["kind"] = "generic", enabled = true, label = id): ActionInfo =>
  ({ id, label, icon: id, enabled, active: false, group: "", kind });

/** What the engine's contextActions returns for a gear with three views and two type actions. */
const gearActions: ActionInfo[] = [
  a("delete"), a("duplicate"), a("rotate90"), a("flipH"), a("flipV"), a("front"), a("back"),
  a("group", "generic", false), a("ungroup", "generic", false), a("numeral"),
  a("view:stick", "view"), a("view:axial", "view"), a("view:side", "view"),
  a("t:teeth+", "type"), a("t:teeth-", "type"),
];
const entry = (id: string): PieEntry => ({ id, label: id, enabled: true, active: false });
const more = entry("menu:more");

describe("pie entries", () => {
  it("offers Type ▸ right after delete for line types", () => {
    const lineActions = [a("delete"), a("duplicate"), a("rotate90"), a("flipH")];
    const ids = buildPieEntries(lineActions, { text: entry("text"), type: entry("menu:type") }).map((c) => c.entry.id);
    expect(ids.slice(0, 3)).toEqual(["delete", "menu:type", "text"]);
  });

  it("puts delete first and keeps the COM priority order", () => {
    const ids = buildPieEntries(gearActions, { text: entry("text"), insert: entry("menu:insert") }).map((c) => c.entry.id);
    expect(ids.slice(0, 8)).toEqual(["delete", "view:stick", "view:axial", "view:side", "text", "menu:insert", "duplicate", "rotate90"]);
    // Type actions and mirror follow; unavailable entries come last.
    expect(ids.indexOf("t:teeth+")).toBeLessThan(ids.indexOf("flipH"));
    expect(ids.slice(-2)).toEqual(["group", "ungroup"]);
  });

  it("offers views only when there is a choice", () => {
    const ids = buildPieEntries([a("delete"), a("view:only", "view")]).map((c) => c.entry.id);
    expect(ids).toEqual(["delete"]);
  });

  it("marks input actions with an ellipsis and delete as dangerous", () => {
    const withInput = { ...a("t:ratio", "type"), label: "Ratio", input: { label: "Ratio", value: "2" } };
    const list = buildPieEntries([a("delete"), withInput]);
    expect(list[0].entry.danger).toBe(true);
    expect(list[1].entry.label).toBe("Ratio …");
  });

  it("keeps submenu arrows and ellipses with their word", () => {
    const table = strings as Record<string, { en: string; de: string }>;
    const m = approxMeasure(10);
    expect(wrapLabel(table["pie.insert"].de, 60, m)).toEqual({ lines: ["Einfügen ▸"], complete: true });
    expect(wrapLabel("Ratio …", 30, m).lines).toHaveLength(1);
  });
});

describe("pie split", () => {
  it("keeps at most eight entries in the ring with the rest under More", () => {
    const all = buildPieEntries(gearActions, { text: entry("text"), insert: entry("menu:insert") });
    const { main, overflow } = pieSplit(all, more);
    expect(main).toHaveLength(MAX_PIE_ITEMS);
    expect(main[0].id).toBe("delete");
    expect(main[main.length - 1].id).toBe("menu:more");
    // Every entry appears exactly once (ring or overflow).
    const shown = [...main.filter((e) => e.id !== "menu:more"), ...overflow].map((e) => e.id).sort();
    expect(shown).toEqual(all.map((c) => c.entry.id).sort());
    // Essential entries that do not fit move into the overflow first.
    expect(overflow[0].id).toBe("rotate90");
  });

  it("shows everything without More when it fits", () => {
    const all = buildPieEntries([a("delete"), a("duplicate"), a("rotate90"), a("flipH")]);
    const { main, overflow } = pieSplit(all, more);
    expect(main.map((e) => e.id)).toEqual(["delete", "duplicate", "rotate90", "flipH"]);
    expect(overflow).toEqual([]);
  });

  it("fills free ring places with secondary entries in order", () => {
    const all = buildPieEntries([a("delete"), a("duplicate"), a("rotate90"), a("t:x", "type"), a("numeral"), a("flipH"), a("flipV"), a("front"), a("back")]);
    const { main, overflow } = pieSplit(all, more);
    expect(main.map((e) => e.id)).toEqual(["delete", "duplicate", "rotate90", "t:x", "numeral", "flipH", "flipV", "menu:more"]);
    expect(overflow.map((e) => e.id)).toEqual(["front", "back"]);
  });

  it("pages long submenus with Back first", () => {
    const back = entry("menu:back");
    const items = Array.from({ length: 10 }, (_, i) => entry(`ins:${i}`));
    const page = submenu(back, items, more);
    expect(page.main).toHaveLength(MAX_PIE_ITEMS);
    expect(page.main[0].id).toBe("menu:back");
    expect(page.main[7].id).toBe("menu:more");
    expect(page.overflow.map((e) => e.id)).toEqual(["ins:6", "ins:7", "ins:8", "ins:9"]);
    expect(submenu(back, items.slice(0, 5), more).main.map((e) => e.id)).toEqual(["menu:back", "ins:0", "ins:1", "ins:2", "ins:3", "ins:4"]);
  });
});

describe("pie geometry", () => {
  it("starts at 12 o'clock and runs clockwise", () => {
    const g = pieGeometry(4);
    expect(g.slots[0].x).toBeCloseTo(0);
    expect(g.slots[0].y).toBeLessThan(0);
    expect(g.slots[1].x).toBeGreaterThan(0);
    expect(g.slots[2].y).toBeGreaterThan(0);
  });

  it("grows the ring with the entry count and gives labels room", () => {
    expect(pieGeometry(8).outer).toBeGreaterThan(pieGeometry(5).outer);
    for (const n of [2, 5, 8, 10]) {
      const w = pieGeometry(n).slots[0]?.width ?? 0;
      expect(w).toBeGreaterThanOrEqual(44);
      expect(w).toBeLessThanOrEqual(84);
    }
  });
});

describe("label wrapping", () => {
  const m = approxMeasure(10); // 5.6 px per character

  it("wraps at spaces and never inside a word", () => {
    const r = wrapLabel("Bring to front", 50, m);
    expect(r).toEqual({ lines: ["Bring to", "front"], complete: true });
    for (const line of r.lines) expect(m(line)).toBeLessThanOrEqual(50);
  });

  it("keeps short labels on one line", () => {
    expect(wrapLabel("Delete", 60, m)).toEqual({ lines: ["Delete"], complete: true });
  });

  it("shortens a word that is wider than the line with an ellipsis instead of breaking it", () => {
    const r = wrapLabel("Zahnradgetriebe links", 50, m);
    expect(r.complete).toBe(false);
    expect(r.lines).toHaveLength(1);
    expect(r.lines[0].endsWith("…")).toBe(true);
    expect(m(r.lines[0])).toBeLessThanOrEqual(50);
  });

  it("shortens the second line when the label needs more than two", () => {
    const r = wrapLabel("Send to the very back of the page", 50, m);
    expect(r.lines).toHaveLength(2);
    expect(r.complete).toBe(false);
    expect(r.lines[1].endsWith("…")).toBe(true);
    expect(r.lines.every((l) => m(l) <= 50)).toBe(true);
  });

  it("prefers word boundaries when shortening", () => {
    expect(ellipsize("one two three four", 60, m)).toBe("one two …");
    expect(ellipsize("short", 60, m)).toBe("short");
  });

  it("falls back to a smaller font before shortening", () => {
    const r = fitLabel("Duplizieren", 56, approxMeasure, [11, 10, 9]);
    expect(r.complete).toBe(true);
    expect(r.fontSize).toBeLessThan(11);
    expect(r.lines).toEqual(["Duplizieren"]);
  });
});

describe("resize on the stage", () => {
  const f = { cx: 50, cy: 50, w: 20, h: 10, rot: 0 };

  it("keeps the opposite corner by default", () => {
    const r = resizeFrame(f, 1, 1, { x: 80, y: 65 }, false, false);
    expect(r.fx).toBeCloseTo(r.fy);
    expect(r.pivot).toEqual({ x: 40, y: 45 });
    expect(r.cx - r.w / 2).toBeCloseTo(40);
    expect(r.cy - r.h / 2).toBeCloseTo(45);
  });

  it("scales about the centre for centred parts", () => {
    const r = resizeFrame(f, 1, 1, { x: 70, y: 60 }, false, true);
    expect(r.cx).toBeCloseTo(50);
    expect(r.cy).toBeCloseTo(50);
    expect(r.pivot).toEqual({ x: 50, y: 50 });
    expect(r.fx).toBeCloseTo(2);
    const edge = resizeFrame(f, 1, 0, { x: 65, y: 50 }, false, true);
    expect(edge.w).toBeCloseTo(30);
    expect(edge.h).toBeCloseTo(10);
    expect(edge.cx).toBeCloseTo(50);
  });

  it("names the mode and its Ctrl alternative in the label", () => {
    const r = resizeFrame(f, 1, 1, { x: 70, y: 60 }, false, true);
    expect(scaleLabel(r, true, true)).toContain("40 × 20 mm");
    expect(scaleLabel(r, false, true)).toContain("200 %");
    const table = strings as Record<string, { en: string; de: string }>;
    expect(scaleLabel(r, true, true)).toContain(table["scale.centre"].en);
    expect(scaleLabel(r, true, false)).toContain(table["scale.edge"].en);
  });

  it("has bilingual texts for the new commands", () => {
    const table = strings as Record<string, { en: string; de: string }>;
    for (const key of ["pie.more", "pie.back", "pie.insert", "pie.text", "text.title", "ctx.text", "cmd.mono", "set.mono", "scale.centre", "scale.edge"]) {
      expect(table[key]?.en).toBeTruthy();
      expect(table[key]?.de).toBeTruthy();
    }
  });
});

describe("label submenu", () => {
  const param = (p: Partial<import("../src/engine/types").ParamInfo>) => ({ key: "k", label: "K", kind: "text", advanced: false, ...p }) as import("../src/engine/types").ParamInfo;
  const text = { larger: "Larger", smaller: "Smaller" };

  it("offers Label ▸ right after Type ▸", () => {
    const ids = buildPieEntries([a("delete"), a("duplicate")], { type: entry("menu:type"), label: entry("menu:label") }).map((c) => c.entry.id);
    expect(ids.slice(0, 3)).toEqual(["delete", "menu:type", "menu:label"]);
  });

  it("maps parameters to prompts, toggles, cycles and size steps", () => {
    const list = labelEntries([
      param({ key: "text", label: "Text", kind: "text", value: "A" }),
      param({ key: "box", label: "Box", kind: "boolean", value: true }),
      param({ key: "pos", label: "Position", kind: "choice", value: "above", options: [a("above", "generic", true, "Above"), a("below", "generic", true, "Below")] }),
      param({ key: "textSize", label: "Size", kind: "number", value: 3, min: 1, max: 20 }),
      param({ key: "width", label: "Width", kind: "number", value: 3 }),
    ], text);
    expect(list.map((l) => l.change)).toEqual([
      { key: "text", prompt: { label: "Text", value: "A" } },
      { key: "box", value: false },
      { key: "pos", value: "below" },
      { key: "textSize", value: 3.6 },
      { key: "textSize", value: 2.5 },
    ]);
    expect(list[2].entry.label).toBe("Position: Above");
    expect(list[1].entry.active).toBe(true);
  });
});
