import { describe, expect, it } from "vitest";
import type { BuildPortInfo, DragHandleInfo } from "../src/engine/types";
import {
  REVEAL_FAR, REVEAL_NEAR, bodyGrip, canDragValue, canRemoveCorner, cornerAt, dragValueStep, primaryPort, rangeText, revealFor, revealOpacity, shownWith,
} from "../src/ui/handlelogic";
import { SHEET_FORMATS, sheetFileName } from "../src/ui/tools";
import strings from "../src/strings.json";

const port = (id: string, kind: string, x = 0, y = 0): BuildPortInfo => ({ id, kind, x, y, dx: 1, dy: 0, stretch: false });
const grip = (key: string, extra: Partial<DragHandleInfo> = {}): DragHandleInfo =>
  ({ key, x: 0, y: 0, constraint: "free", dx: 0, dy: 0, label: key, byPart: false, ...extra });

describe("progressive disclosure (COM Reveal)", () => {
  it("shows a handle fully within 24 px, fades it out until 56 px", () => {
    expect(revealFor(0)).toBe(1);
    expect(revealFor(REVEAL_NEAR)).toBe(1);
    expect(revealFor(40)).toBeCloseTo(0.5);
    expect(revealFor(REVEAL_FAR)).toBe(0);
    expect(revealFor(500)).toBe(0);
  });

  it("keeps the primary handle and everything under Shift; others need the pointer near them or their anchor", () => {
    const at = { x: 100, y: 100 };
    expect(revealOpacity(at, undefined, undefined, true, false)).toBe(1);
    expect(revealOpacity(at, undefined, undefined, false, true)).toBe(1);
    expect(revealOpacity(at, undefined, undefined, false, false)).toBe(0);
    expect(revealOpacity(at, undefined, { x: 300, y: 300 }, false, false)).toBe(0);
    expect(revealOpacity(at, undefined, { x: 110, y: 100 }, false, false)).toBe(1);
    // A lock badge also appears near its contact point.
    expect(revealOpacity(at, { x: 90, y: 110 }, { x: 80, y: 120 }, false, false)).toBe(1);
  });

  it("picks the one primary '+': the line's free end, a gear's right side, a connection port", () => {
    const pts = [{ x: 0, y: 0 }, { x: 50, y: 0 }];
    expect(primaryPort([port("a", "LineEnd", 0, 0), port("b", "LineEnd", 50, 0)], pts)).toBe(1);
    expect(primaryPort([port("a", "LineEnd", 0, 0)], pts)).toBe(0);
    expect(primaryPort([port("mesh.top", "Mesh"), port("mesh.right", "Mesh"), port("hub", "Hub")])).toBe(1);
    expect(primaryPort([port("hub", "Hub"), port("p1", "Connect")])).toBe(1);
    expect(primaryPort([port("x.start", "AxisEnd"), port("x.end", "AxisEnd")])).toBe(1);
    expect(primaryPort([port("hub", "Hub")])).toBe(0);
    expect(primaryPort([])).toBe(-1);
    // Diagram nodes: the triangle below is primary, no "+".
    expect(primaryPort([port("p1", "Connect")], undefined, true)).toBe(-1);
  });
});

describe("corners, grips and values", () => {
  it("puts a corner where its dot is drawn, projected onto the segment and clear of its ends", () => {
    const a = { x: 0, y: 0 }, b = { x: 100, y: 0 }, mid = { x: 50, y: 0 };
    expect(cornerAt(a, b, { x: 50.2, y: 3 }, mid, 0.5)).toEqual(mid);
    expect(cornerAt(a, b, { x: 70, y: 3 }, mid, 0.5)).toEqual({ x: 70, y: 0 });
    expect(cornerAt(a, b, { x: 1, y: 0 }, mid, 0.5).x).toBeCloseTo(8);
    expect(canRemoveCorner([1, 2], 2)).toBe(true);
    expect(canRemoveCorner([1, 2], 0)).toBe(false);
    expect(canRemoveCorner(undefined, 1)).toBe(false);
  });

  it("drags a whole curve by its byPart grip only while that curve is selected", () => {
    const grips = [grip("series.0.body", { part: "series.0", byPart: true }), grip("limit", { part: "limit.0" })];
    expect(bodyGrip(grips, "series.0")?.key).toBe("series.0.body");
    expect(bodyGrip(grips, "limit.0")).toBeUndefined();
    expect(bodyGrip(grips, undefined)).toBeUndefined();
  });

  it("shows the grips of the selected part and its points in full (COM ShownWith)", () => {
    const all = [grip("p0", { part: "point.0.0", showWith: "series.0" }), grip("p1", { part: "point.0.1", showWith: "series.0" }), grip("lim", { part: "limit.0" })];
    expect(shownWith(all[0], "series.0", all)).toBe(true);
    expect(shownWith(all[1], "point.0.0", all)).toBe(true);
    expect(shownWith(all[2], "series.0", all)).toBe(false);
    expect(shownWith(all[2], "limit.0", all)).toBe(true);
    expect(shownWith(all[0], undefined, all)).toBe(false);
  });

  it("changes a dragged value by a factor e per 90 px, rounded to three digits (COM DragValue)", () => {
    expect(dragValueStep(100, 0)).toBe(100);
    expect(dragValueStep(100, 90)).toBeCloseTo(272);
    expect(dragValueStep(100, -90)).toBeCloseTo(36.8);
    expect(dragValueStep(0, 50)).toBe(0);
    expect(canDragValue({ value: 4.7 }, "explore")).toBe(true);
    expect(canDragValue({ value: 4.7 }, "engineer")).toBe(false);
    expect(canDragValue({ value: -1 }, "explore")).toBe(false);
    expect(canDragValue({ value: null }, "explore")).toBe(false);
    expect(rangeText({ range: "180 V … 220 V" })).toBe("180 V … 220 V");
    expect(rangeText({ range: null })).toBe("");
  });

  it("names the downloaded calculation sheet by its format", () => {
    expect(SHEET_FORMATS.map((f) => f.mode)).toEqual(["csv", "markdown", "latex"]);
    expect(sheetFileName("markdown")).toBe("inventasketch-calculation.md");
    expect(sheetFileName("latex")).toBe("inventasketch-calculation.tex");
  });

  it("has every new text in both languages and never the word 'patent'", () => {
    const table = strings as Record<string, { en: string; de: string }>;
    const keys = ["val.range", "val.drag", "val.badge", "val.sheet", "sheet.title", "sheet.hint", "sheet.format", "sheet.rows", "sheet.copy",
      "sheet.copied", "sheet.copyHint", "sheet.download", "sheet.table", "sheet.tableHint", "handle.corner", "handle.branch", "handle.gripDrag",
      ...SHEET_FORMATS.map((f) => `sheet.format.${f.mode}`)];
    for (const k of keys) {
      expect(table[k]?.en, k).toBeTruthy();
      expect(table[k]?.de, k).toBeTruthy();
      expect(`${table[k].en} ${table[k].de}`.toLowerCase()).not.toContain("patent");
    }
  });
});
