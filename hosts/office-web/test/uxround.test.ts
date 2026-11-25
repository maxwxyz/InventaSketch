import { describe, expect, it } from "vitest";
import type { ActionInfo, ArrangeOption, CatalogItem, ObjectInfo, PreflightFinding, SnapGuide, SuggestionInfo } from "../src/engine/types";
import { boxPosition, insertBoxKey, insertRows, rememberRecent } from "../src/ui/insertbox";
import { hasIcon } from "../src/ui/icons";
import {
  assemblyExtends, canTrace, elementCount, filterTransforms, findingCounts, multiBarGroups, orderFindings, snapLabelAnchor, sourceLinks, tracedPathItem, withTracedPoints,
} from "../src/ui/uxlogic";
import strings from "../src/strings.json";

const a = (id: string, kind: ActionInfo["kind"] = "generic"): ActionInfo => ({ id, label: id, icon: id, enabled: true, active: false, group: "", kind });

describe("context-only transforms", () => {
  const actions = [a("delete"), a("flipH"), a("flipV"), a("rotate90"), a("view:axial", "view"), a("numeral")];
  const ids = (list: ActionInfo[]) => list.map((x) => x.id);

  it("drops mirror and rotate that change nothing (gear in axial view)", () => {
    expect(ids(filterTransforms(actions, { mirrorH: false, mirrorV: false, rotate90: false }))).toEqual(["delete", "view:axial", "numeral"]);
  });

  it("keeps only the transforms that change the drawing", () => {
    expect(ids(filterTransforms(actions, { mirrorH: true, mirrorV: false, rotate90: true }))).toEqual(["delete", "flipH", "rotate90", "view:axial", "numeral"]);
  });

  it("keeps everything when the engine cannot tell", () => {
    expect(filterTransforms(actions, undefined)).toEqual(actions);
  });
});

describe("multi-selection bar", () => {
  const obj = (id: string, groupId?: string) => ({ id, groupId }) as ObjectInfo;

  it("counts a group as one element", () => {
    const objects = [obj("a", "g"), obj("b", "g"), obj("c")];
    expect(elementCount(objects, ["a", "b"])).toBe(1);
    expect(elementCount(objects, ["a", "b", "c"])).toBe(2);
    expect(elementCount(objects, ["x", "y"])).toBe(2);
  });

  it("puts one view for all first and keeps the engine order of the rest", () => {
    const opt = (id: string, icon = "cmd-align-row"): ArrangeOption => ({ id, label: id, icon });
    const g = multiBarGroups([opt("align:row"), opt("mview:stick", "view-schematic"), opt("dist:h"), opt("mview:axial", "view-axial"), opt("renumber")]);
    expect(g.views.map((o) => o.id)).toEqual(["mview:stick", "mview:axial"]);
    expect(g.arrange.map((o) => o.id)).toEqual(["align:row", "dist:h", "renumber"]);
  });

  it("caps the view buttons at six", () => {
    const many = Array.from({ length: 8 }, (_, i): ArrangeOption => ({ id: `mview:v${i}`, label: "", icon: "view-side" }));
    expect(multiBarGroups(many).views).toHaveLength(6);
  });

  it("has pictograms for every arrange icon the engine sends", () => {
    for (const id of ["cmd-align-row", "cmd-align-column", "cmd-distribute-h", "cmd-distribute-v", "cmd-renumber", "cmd-match-format", "cmd-select-assembly", "cmd-trace", "cmd-insert", "cmd-cycle", "view-chart"])
      expect(hasIcon(id)).toBe(true);
  });
});

describe("assembly and coupler curve", () => {
  it("offers Select assembly only when coupled parts are not selected yet", () => {
    expect(assemblyExtends(["g1"], ["g1"])).toBe(false);
    expect(assemblyExtends(["g1", "g2", "s"], ["g1"])).toBe(true);
    expect(assemblyExtends(["g1", "g2"], ["g2", "g1"])).toBe(false);
  });

  it("draws a path only for chains that move with one input", () => {
    expect(canTrace(undefined)).toBe(false);
    expect(canTrace({ points: [], closed: false })).toBe(false);
    expect(canTrace({ points: [{ x: 0, y: 0 }, { x: 1, y: 0 }, { x: 1, y: 1 }], closed: true })).toBe(true);
    expect(tracedPathItem({ points: [], closed: true })).toBe("kin.path.closed");
    expect(tracedPathItem({ points: [], closed: false })).toBe("kin.path");
  });

  it("puts the traced points into the inserted path, open paths without arrowhead", () => {
    const inserted = { id: "p1", type: "kin.path", points: [{ x: 0, y: 0 }, { x: 5, y: 5 }], attach: [null, null], params: { smooth: true } };
    const open = withTracedPoints(inserted, { points: [{ x: 1.234567, y: 2 }, { x: 3, y: 4 }, { x: 5, y: 6 }], closed: false });
    expect(open.points).toEqual([{ x: 1.2346, y: 2 }, { x: 3, y: 4 }, { x: 5, y: 6 }]);
    expect(open.params).toEqual({ smooth: true, arrow: false });
    expect("attach" in open).toBe(false);
    expect(open.id).toBe("p1");
    const closed = withTracedPoints(inserted, { points: [{ x: 1, y: 2 }, { x: 3, y: 4 }, { x: 5, y: 6 }], closed: true });
    expect(closed.params).toEqual({ smooth: true });
    expect(inserted.params).toEqual({ smooth: true });
  });
});

describe("snap label", () => {
  const g = (kind: SnapGuide["kind"], a: { x: number; y: number }, b = a, radius = 0): SnapGuide => ({ kind, a, b, radius });

  it("sits at the docking point first", () => {
    expect(snapLabelAnchor([g("vertical", { x: 1, y: 0 }), g("point", { x: 10, y: 20 })])).toEqual({ x: 10, y: 20 });
  });

  it("sits on the snapped circle, segment or alignment line otherwise", () => {
    const c = snapLabelAnchor([g("circle", { x: 0, y: 0 }, { x: 0, y: 0 }, 10)])!;
    expect(Math.hypot(c.x, c.y)).toBeCloseTo(10, 6);
    expect(c.x).toBeGreaterThan(0);
    expect(c.y).toBeLessThan(0);
    expect(snapLabelAnchor([g("segment", { x: 0, y: 0 }, { x: 10, y: 4 })])).toEqual({ x: 5, y: 2 });
    expect(snapLabelAnchor([g("horizontal", { x: 3, y: 7 })])).toEqual({ x: 3, y: 7 });
    expect(snapLabelAnchor([])).toBeUndefined();
  });
});

describe("insert box", () => {
  const item = (id: string): CatalogItem => ({ id, typeId: id, name: id.toUpperCase(), category: "c", keywords: [], thumbnail: '<svg viewBox="0 0 1 1"></svg>', points: false });
  const catalog = new Map(["gear", "bearing", "shaft", "motor"].map((id) => [id, item(id)]));
  const lookup = (id: string) => catalog.get(id);
  const hit = (id: string, fits = false): SuggestionInfo => ({ id, typeId: id, name: `${id} name`, category: "c", points: false, fits });

  it("keeps the engine order: fitting parts first and marked, then recent items", () => {
    const rows = insertRows([hit("bearing", true), hit("gear", true), hit("motor"), hit("shaft")], ["motor", "gear"], lookup);
    expect(rows.map((r) => [r.id, r.fits, r.recent])).toEqual([["bearing", true, false], ["gear", true, false], ["motor", false, true], ["shaft", false, false]]);
    expect(rows[0].name).toBe("bearing name");
    expect(rows[0].image).toMatch(/^data:image\/svg\+xml/);
  });

  it("leaves out items of hidden modules and duplicates", () => {
    expect(insertRows([hit("gear"), hit("hidden"), hit("gear")], [], lookup).map((r) => r.id)).toEqual(["gear"]);
  });

  it("marks nothing as fitting in lists without a fit flag (make it …)", () => {
    const rows = insertRows([{ ...hit("gear"), fits: undefined }, hit("motor")], ["motor"], lookup);
    expect(rows.map((r) => [r.fits, r.recent])).toEqual([[false, false], [false, true]]);
  });

  it("moves with the arrow keys (wrapping), takes Enter and closes on Esc", () => {
    expect(insertBoxKey("ArrowDown", 0, 3)).toEqual({ index: 1, action: "move" });
    expect(insertBoxKey("ArrowDown", 2, 3)).toEqual({ index: 0, action: "move" });
    expect(insertBoxKey("ArrowUp", 0, 3)).toEqual({ index: 2, action: "move" });
    expect(insertBoxKey("Enter", 1, 3)).toEqual({ index: 1, action: "choose" });
    expect(insertBoxKey("Enter", 0, 0).action).toBe("none");
    expect(insertBoxKey("ArrowDown", 0, 0).action).toBe("none");
    expect(insertBoxKey("Escape", 1, 3).action).toBe("close");
    expect(insertBoxKey("a", 1, 3)).toEqual({ index: 1, action: "none" });
  });

  it("opens below right of the pointer and flips where there is no room", () => {
    expect(boxPosition(100, 100, 280, 200, 800, 600)).toEqual({ left: 104, top: 104 });
    expect(boxPosition(700, 500, 280, 200, 800, 600)).toEqual({ left: 416, top: 296 });
    expect(boxPosition(10, 10, 280, 200, 200, 150).left).toBe(4);
  });

  it("remembers recent items, most recent first, without duplicates", () => {
    expect(rememberRecent([], "gear")).toEqual(["gear"]);
    expect(rememberRecent(["a", "gear", "b"], "gear")).toEqual(["gear", "a", "b"]);
    const many = Array.from({ length: 12 }, (_, i) => `i${i}`);
    expect(rememberRecent(many, "new")).toHaveLength(12);
    expect(rememberRecent(many, "new")[0]).toBe("new");
    expect(rememberRecent(many, "new", 3)).toEqual(["new", "i0", "i1"]);
  });
});

describe("texts of the UI/UX round", () => {
  it("are bilingual", () => {
    const table = strings as Record<string, { en: string; de: string }>;
    for (const key of ["ctx.insert", "ctx.makeIt", "ctx.assembly", "ctx.trace", "ctx.multi", "trace.done", "ins.title", "ins.makeIt", "ins.search", "ins.fits", "ins.recent", "ins.none", "ins.empty"]) {
      expect(table[key]?.en).toBeTruthy();
      expect(table[key]?.de).toBeTruthy();
      expect(table[key].de).not.toBe(table[key].en);
    }
  });
});

describe("patent check list", () => {
  const f = (severity: PreflightFinding["severity"], rule: string): PreflightFinding => ({ rule, severity, message: rule, source: "" });

  it("puts errors first and keeps the engine order within a severity", () => {
    const list = [f("info", "i1"), f("warning", "w1"), f("error", "e1"), f("manual", "m1"), f("error", "e2")];
    expect(orderFindings(list).map((x) => x.rule)).toEqual(["e1", "e2", "w1", "m1", "i1"]);
  });

  it("counts the findings per severity", () => {
    expect(findingCounts([f("error", "a"), f("error", "b"), f("info", "c")])).toEqual({ error: 2, warning: 0, manual: 0, info: 1 });
  });

  it("has a label and a pictogram for the check", () => {
    for (const s of ["error", "warning", "manual", "info"]) expect((strings as Record<string, unknown>)[`pc.sev.${s}`]).toBeTruthy();
    expect(hasIcon("check")).toBe(true);
  });
});

describe("patent check sources", () => {
  it("splits cited sources into labels and links", () => {
    const s = "DE-01: PatV § 6(2) <https://www.gesetze-im-internet.de/patv/__6.html>; EP-02: OJ EPO 2025, A49 Art. 2(1) <https://www.epo.org/a49>; US-06: 37 CFR 1.84(f)";
    expect(sourceLinks(s)).toEqual([
      { label: "DE-01: PatV § 6(2)", url: "https://www.gesetze-im-internet.de/patv/__6.html" },
      { label: "EP-02: OJ EPO 2025, A49 Art. 2(1)", url: "https://www.epo.org/a49" },
      { label: "US-06: 37 CFR 1.84(f)" },
    ]);
  });
});
