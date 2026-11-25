import { describe, expect, it } from "vitest";
import type { ActionInfo } from "../src/engine/types";
import {
  approxMeasure, ellipsize, entryOf, labelEntries, fitLabel, MAX_SUB_ITEMS, PIE_PLACES, pieGeometry, pieRing, summary, withBack, wrapLabel,
  type PieEntry, type PieGroup, type PiePlace,
} from "../src/ui/pielogic";
import { resizeFrame, scaleLabel } from "../src/ui/stage";
import strings from "../src/strings.json";

const a = (id: string, kind: ActionInfo["kind"] = "generic", enabled = true, label = id): ActionInfo =>
  ({ id, label, icon: id, enabled, active: false, group: "", kind });

const entry = (id: string): PieEntry => ({ id, label: id, enabled: true, active: false });

describe("pie places", () => {
  const groups = (over: Partial<Record<PiePlace, PieEntry[]>> = {}): PieGroup[] => PIE_PLACES.map((key) => ({
    key, label: key === "edit" || key === "style" || key === "arrange" || key === "view" ? `${key} ▸` : key, items: over[key] ?? [],
    alwaysMenu: key === "edit" || key === "style" || key === "arrange",
  }));

  it("keeps eight fixed places, clockwise from 12 o'clock, greyed when empty", () => {
    const { ring } = pieRing(groups({ delete: [entry("delete")], duplicate: [entry("duplicate")] }), "More ▸");
    expect(ring).toHaveLength(8);
    expect(ring.map((e) => e.id)).toEqual(["menu:g:text", "menu:g:edit", "menu:g:insert", "duplicate", "menu:g:style", "delete", "menu:g:view", "menu:g:arrange"]);
    expect(ring.filter((e) => !e.enabled).map((e) => e.id)).toEqual(["menu:g:text", "menu:g:edit", "menu:g:insert", "menu:g:style", "menu:g:view", "menu:g:arrange"]);
    expect(PIE_PLACES).toEqual(["text", "edit", "insert", "duplicate", "style", "delete", "view", "arrange"]);
  });

  it("shows a single entry directly, except for places that keep their name", () => {
    const { ring, menus, titles } = pieRing(groups({ text: [entry("text")], edit: [entry("t:x")], arrange: [entry("rotate90")] }), "More ▸");
    expect(ring[0].id).toBe("text");
    expect(ring[1].id).toBe("menu:g:edit");
    expect(ring[1].hint).toBe("t:x");
    expect(menus.get("edit")?.map((e) => e.id)).toEqual(["t:x"]);
    expect(ring[7].id).toBe("menu:g:arrange");
    expect(titles.get("edit")).toBe("edit");
  });

  it("pages long submenus into More with the rest", () => {
    const items = Array.from({ length: 15 }, (_, i) => entry(`a${i}`));
    const { menus } = pieRing(groups({ edit: items }), "More ▸");
    const first = menus.get("edit")!;
    expect(first).toHaveLength(MAX_SUB_ITEMS);
    expect(first[MAX_SUB_ITEMS - 1].id).toBe("menu:g:edit+");
    expect(menus.get("edit+")!.map((e) => e.id)).toEqual(["a10", "a11", "a12", "a13", "a14"]);
    expect(withBack(entry("menu:back"), first)[0].id).toBe("menu:back");
    // Every entry appears exactly once.
    const shown = [...first.slice(0, -1), ...menus.get("edit+")!].map((e) => e.id);
    expect(shown).toEqual(items.map((e) => e.id));
  });

  it("marks input actions with an ellipsis and delete as dangerous", () => {
    expect(entryOf(a("delete")).danger).toBe(true);
    expect(entryOf({ ...a("t:ratio", "type"), label: "Ratio", input: { label: "Ratio", value: "2" } }).label).toBe("Ratio …");
  });

  it("keeps submenu arrows and ellipses with their word", () => {
    const table = strings as Record<string, { en: string; de: string }>;
    const m = approxMeasure(10);
    expect(wrapLabel(table["pie.insert"].de, 60, m)).toEqual({ lines: [table["pie.insert"].de], complete: true });
    expect(wrapLabel("Ratio …", 30, m).lines).toHaveLength(1);
  });

  it("summarises a group for its hint", () => {
    expect(summary([entry("A ▸"), entry("B …"), entry("C"), entry("D"), entry("E")])).toBe("A, B, C, D, …");
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
    expect(pieGeometry(12).outer).toBeGreaterThan(pieGeometry(8).outer);
    for (const n of [2, 5, 8, 10, 12]) {
      const g = pieGeometry(n);
      const w = g.slots[0]?.width ?? 0;
      expect(w).toBeGreaterThanOrEqual(44);
      expect(w).toBeLessThanOrEqual(96);
      // The label room never leaves the ring.
      expect(Math.hypot(g.slots[0].x, g.slots[0].y) + 20).toBeLessThan(g.outer);
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
    expect(ellipsize("one two three four", 60, m)).toBe("one two …");
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

  it("maps parameters to prompts, toggles, choice menus and size steps", () => {
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
      undefined,
      { key: "textSize", value: 3.6 },
      { key: "textSize", value: 2.5 },
    ]);
    // A choice opens its options (never a cycle).
    expect(list[2].entry.id).toBe("menu:c:pos");
    expect(list[2].entry.label).toBe("Position: Above ▸");
    expect(list[1].entry.active).toBe(true);
  });
});
