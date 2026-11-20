import { describe, expect, it } from "vitest";
import { liveRedrawIds } from "../src/office/sync";
import { promptPosition } from "../src/ui/contextbar";
import strings from "../src/strings.json";

// Live sync redraws only the units an edit touched: the changed objects plus their dependents reported by the engine
// (walls joined to an edited wall, doors in it), never untouched units.
describe("liveRedrawIds", () => {
  const units = new Map<string, string[]>([
    ["wallA", ["wallA"]],
    ["wallB", ["wallB"]],
    ["door", ["door"]],
    ["grp", ["m1", "m2"]],
  ]);
  const existing = new Set(["wallA", "wallB", "door", "m1", "m2"]);

  it("redraws every tracked unit without change information", () => {
    expect(liveRedrawIds(units, existing).sort()).toEqual(["door", "m1", "m2", "wallA", "wallB"]);
  });

  it("redraws the changed object and its dependents only", () => {
    expect(liveRedrawIds(units, existing, new Set(["wallA", "door"])).sort()).toEqual(["door", "wallA"]);
  });

  it("redraws whole assemblies and drops deleted objects", () => {
    expect(liveRedrawIds(units, new Set(["wallA", "m1", "m2"]), new Set(["m2", "wallB"])).sort()).toEqual(["m1", "m2"]);
  });

  it("ignores objects that are not on the slide", () => {
    expect(liveRedrawIds(units, existing, new Set(["elsewhere"]))).toEqual([]);
  });
});

describe("inline prompt", () => {
  it("opens below the point and stays inside the pane", () => {
    expect(promptPosition(200, 100, 300, 120, 800, 600)).toEqual({ left: 50, top: 112 });
    expect(promptPosition(10, 100, 300, 120, 800, 600).left).toBe(4);
    expect(promptPosition(400, 560, 300, 120, 800, 600).top).toBe(560 - 120 - 12);
  });

  it("has bilingual texts", () => {
    const table = strings as Record<string, { en: string; de: string }>;
    for (const key of ["prompt.ok", "prompt.cancel", "insp.planScale", "insp.planScaleHint"]) {
      expect(table[key]?.en).toBeTruthy();
      expect(table[key]?.de).toBeTruthy();
    }
  });
});
