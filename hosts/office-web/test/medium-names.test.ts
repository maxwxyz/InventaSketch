import { describe, expect, it } from "vitest";
import { Engine, type EngineBackend } from "../src/engine/client";
import { alsoOn, continuationMap, designationOf, sameMap, slideDesignations } from "../src/office/designations";
import type { SlideUnits } from "../src/office/slides";
import {
  UNIT_CHOICES, angleStep, badgeKind, checkLine, cornerCommands, endsWithoutPort, portEndIndex,
} from "../src/ui/handlelogic";
import strings from "../src/strings.json";

// Colour by medium, designations across slides, Calc 6 checks in the value card and the last line handles of the web
// add-in (docs/en/hosts/office-web.md, "Medium colours, names across slides, checks and line ends").

describe("designations across slides", () => {
  it("maps every designation of the other slides to their numbers, the current slide left out", () => {
    const map = continuationMap([
      { index: 0, keys: ["W3", "H1"] },
      { index: 1, keys: ["W3"] },
      { index: 2, keys: [] },
      { index: 3, keys: ["W3", "X9"] },
    ], 1);
    expect(map).toEqual({ H1: ["1"], W3: ["1", "4"], X9: ["4"] });
    expect(continuationMap([{ index: 0, keys: ["W3"] }], 0)).toEqual({});
  });

  it("tells the other slides of the selected element's designation", () => {
    const list = [{ key: "W3", designation: "-W3", ids: ["a", "b"] }, { key: "H1", designation: "H1", ids: ["c"] }];
    expect(designationOf(list, "b")?.key).toBe("W3");
    expect(designationOf(list, "z")).toBeUndefined();
    expect(alsoOn({ W3: ["2", "4"] }, "W3")).toEqual(["2", "4"]);
    expect(alsoOn({ W3: ["2", "4"] }, "H1")).toEqual([]);
    expect(alsoOn({ W3: ["2"] }, undefined)).toEqual([]);
  });

  it("compares maps so the engine is told only about changes", () => {
    expect(sameMap({ W3: ["2"] }, { W3: ["2"] })).toBe(true);
    expect(sameMap({ W3: ["2"] }, { W3: ["2", "4"] })).toBe(false);
    expect(sameMap({}, { W3: ["2"] })).toBe(false);
    expect(sameMap(undefined, {})).toBe(false);
  });

  it("reads each slide's designations in its own engine session and closes it", () => {
    const calls: string[] = [];
    const names = new Map<string, string[]>();
    let n = 0;
    const backend: EngineBackend = {
      call(method, json) {
        const a = JSON.parse(json);
        calls.push(method);
        const ok = (result: unknown) => JSON.stringify({ ok: true, result });
        const st = (s: string, selection: string[] = []) => ({ session: s, revision: 1, pageId: "p", pageWidth: 280, pageHeight: 157.5, grid: 0, selection, objects: [] });
        switch (method) {
          case "createSession": { const s = `s${++n}`; names.set(s, []); return ok(st(s)); }
          case "importCompact": names.get(a.session)!.push(...String(a.payload).split(",")); return ok(st(a.session, ["o"]));
          case "designations": return ok(names.get(a.session)!.map((k) => ({ key: k.toUpperCase(), designation: k, ids: ["o"] })));
          case "closeSession": names.delete(a.session); return ok({});
          default: return JSON.stringify({ ok: false, error: "unknown " + method });
        }
      },
    };
    const slides: SlideUnits[] = [
      { index: 0, slideId: "256", units: [{ unitId: "u1", payload: "w3", shapeId: "1" }, { unitId: "u2", payload: "h1", shapeId: "2" }] },
      { index: 1, slideId: "257", units: [] },
      { index: 2, slideId: "258", units: [{ unitId: "u3", payload: "w3", shapeId: "3" }] },
    ];
    const found = slideDesignations(new Engine(backend), slides, { w: 280, h: 157.5 }, "en");
    expect(found).toEqual([{ index: 0, keys: ["W3", "H1"] }, { index: 1, keys: [] }, { index: 2, keys: ["W3"] }]);
    expect(calls.filter((c) => c === "createSession")).toHaveLength(2);
    expect(calls.filter((c) => c === "closeSession")).toHaveLength(2);
    expect(names.size).toBe(0);
    expect(continuationMap(found, 2)).toEqual({ H1: ["1"], W3: ["1"] });
  });
});

describe("checks in the value card", () => {
  it("shows the badge for a conflict, else for a warning about the element", () => {
    const checks = [{ objectId: "r1", severity: "warning" as const }, { objectId: "r2", severity: "hint" as const }];
    expect(badgeKind("conflict", [], "r1")).toBe("conflict");
    expect(badgeKind("checked", checks, "r1")).toBe("warning");
    expect(badgeKind("checked", checks, "r2")).toBe("");
    expect(badgeKind(undefined, [], "r1")).toBe("");
  });

  it("writes a check with rule and source as the COM card does", () => {
    expect(checkLine({ severity: "warning", message: "Power above the rating", rule: "P ≤ P_rated", source: "S9" })).toBe("⚠ Power above the rating (P ≤ P_rated) [S9]");
    expect(checkLine({ severity: "hint", message: "Check the fuse", rule: "", source: null })).toBe("ⓘ Check the fuse");
  });

  it("offers the preferred units of the COM add-in, with units the engine knows", () => {
    expect(UNIT_CHOICES.map((c) => c.kind)).toEqual(["pressure", "rotationalSpeed", "energy", "power"]);
    expect(UNIT_CHOICES.find((c) => c.kind === "rotationalSpeed")?.units).toEqual(["min⁻¹", "rev/s", "rad/s"]);
    const table = strings as Record<string, { en: string; de: string }>;
    for (const c of UNIT_CHOICES) expect(table[`val.kind.${c.kind}`]?.de, c.kind).toBeTruthy();
  });
});

describe("line handles: draw on, corners", () => {
  it("gives a free end its own + only where no line end port sits", () => {
    const ends = [{ x: 0, y: 0, index: 0 }, { x: 50, y: 0, index: 1 }];
    expect(endsWithoutPort(ends, [{ kind: "LineEnd", x: 50.2, y: 0 }]).map((e) => e.index)).toEqual([0]);
    expect(endsWithoutPort(ends, [{ kind: "Connect", x: 50, y: 0 }]).map((e) => e.index)).toEqual([0, 1]);
  });

  it("draws on from the end a line end port stands for", () => {
    expect(portEndIndex({ id: "line.start", kind: "LineEnd" }, 4)).toBe(0);
    expect(portEndIndex({ id: "line.end", kind: "LineEnd" }, 4)).toBe(3);
    expect(portEndIndex({ id: "mesh.right", kind: "Mesh" }, 4)).toBeUndefined();
  });

  it("snaps the direction to 15° steps with Shift, the length kept", () => {
    const q = angleStep({ x: 0, y: 0 }, { x: 10, y: 1 });
    expect(q.x).toBeCloseTo(Math.hypot(10, 1), 6);
    expect(q.y).toBeCloseTo(0, 6);
    const r = angleStep({ x: 0, y: 0 }, { x: 10, y: 9 });
    expect(Math.atan2(r.y, r.x) * 180 / Math.PI).toBeCloseTo(45, 6);
  });

  it("offers split and remove for a selected corner where the engine allows them", () => {
    const open = { closed: false, splittable: [1, 2], removable: [1, 2] };
    expect(cornerCommands(open, 1)).toEqual(["split", "remove"]);
    expect(cornerCommands(open, 0)).toEqual([]);
    expect(cornerCommands({ closed: true, splittable: [0, 1, 2], removable: [] }, 0)).toEqual(["open"]);
    expect(cornerCommands(undefined, 1)).toEqual([]);
  });
});

describe("strings", () => {
  const table = strings as Record<string, { en: string; de: string }>;
  it.each([
    "set.colorBy", "set.colorBy.off", "set.colorBy.medium", "set.legend", "legend.none", "legend.done", "connect.alsoOn", "connect.alsoOn1",
    "val.checks", "val.mute", "val.units", "val.badgeWarn", "handle.junction", "handle.drawOn", "port.LineEnd.draw", "draw.hint",
    "corner.split", "corner.open", "corner.remove", "corner.selected",
  ])("%s is bilingual", (key) => {
    expect(table[key]?.en).toBeTruthy();
    expect(table[key]?.de).toBeTruthy();
    expect(table[key].en.toLowerCase()).not.toContain("patent");
  });
});
