import { describe, expect, it } from "vitest";
import type { ActionInfo, PartView } from "../src/engine/types";
import { addedPart, isPartCommand, keepPart, partActionId, partActions, partAfterDelete, partDeleteAction, partListEntries } from "../src/ui/partlogic";

const act = (id: string, extra: Partial<ActionInfo> = {}): ActionInfo => ({ id, label: id, icon: "cmd-toggle", enabled: true, active: false, group: "part", kind: "generic", ...extra });
const parts: PartView[] = [
  { key: "series.0", label: "Series 1", kind: "series", hidden: false },
  { key: "grid.major", label: "Major grid", kind: "grid", hidden: true },
];

describe("part mode (charts v2)", () => {
  it("prefixes part actions so they never clash with element actions", () => {
    const list = partActions([act("chart.smooth"), act("part.hide")]);
    expect(list.map((a) => a.id)).toEqual(["part:chart.smooth", "part:part.hide"]);
    expect(partActionId("part:part.hide")).toBe("part.hide");
    expect(partActionId("t:chart.smooth")).toBeUndefined();
    expect(isPartCommand("part:none") && isPartCommand("partsel:axis.x") && isPartCommand("partlist")).toBe(true);
    expect(isPartCommand("insert")).toBe(false);
  });

  it("deletes a part with its own delete, else hides it, never the element", () => {
    expect(partDeleteAction([act("part.hide"), act("part.delete")])).toBe("part.delete");
    expect(partDeleteAction([act("part.hide")])).toBe("part.hide");
    expect(partDeleteAction([act("part.hide", { active: true })])).toBeUndefined();
    expect(partDeleteAction([act("part.delete", { enabled: false })])).toBeUndefined();
    expect(partAfterDelete("point.0.3")).toBe("series.0");
    expect(partAfterDelete("series.1")).toBeUndefined();
  });

  it("selects the one part a menu option added", () => {
    expect(addedPart(["series.0"], ["series.0", "area.0"])).toBe("area.0");
    expect(addedPart(["series.0"], ["series.0"])).toBeUndefined();
    expect(addedPart([], ["a", "b"])).toBeUndefined();
  });

  it("lists every part, hidden ones marked, the current one active", () => {
    const list = partListEntries(parts, "series.0", " (hidden)");
    expect(list.map((e) => e.label)).toEqual(["Series 1", "Major grid (hidden)"]);
    expect(list.map((e) => e.id)).toEqual(["partsel:series.0", "partsel:grid.major"]);
    expect(list[0].active).toBe(true);
  });

  it("keeps the part only while its element stays selected alone and the part exists", () => {
    const part = { id: "o1", key: "series.0", label: "old" };
    expect(keepPart(part, ["o1"], () => parts)).toEqual({ id: "o1", key: "series.0", label: "Series 1" });
    expect(keepPart(part, ["o1", "o2"], () => parts)).toBeUndefined();
    expect(keepPart(part, ["o2"], () => parts)).toBeUndefined();
    expect(keepPart({ ...part, key: "series.5" }, ["o1"], () => parts)).toBeUndefined();
    expect(keepPart(undefined, ["o1"], () => parts)).toBeUndefined();
  });
});
