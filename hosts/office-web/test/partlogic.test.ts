import { describe, expect, it } from "vitest";
import type { ActionInfo, PartView } from "../src/engine/types";
import { isPartCommand, keepPart, partActionId, partActions, partListEntries, partPieEntries } from "../src/ui/partlogic";

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

  it("offers the part actions in the pie, then the parts list and the whole element", () => {
    const entries = partPieEntries([act("part.hide"), act("chart.data", { input: { label: "Data", value: "0;1" } })], "Parts ▸", "Whole element");
    expect(entries.map((e) => e.id)).toEqual(["part:part.hide", "part:chart.data", "menu:parts", "part:none"]);
    expect(entries[0].action?.id).toBe("part:part.hide");
    expect(entries[1].label.endsWith("…")).toBe(true);
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
