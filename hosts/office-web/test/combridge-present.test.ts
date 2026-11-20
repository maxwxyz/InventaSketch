import { describe, expect, it } from "vitest";
import { ComSync, type ComSyncBridge, type ComSyncHost } from "../src/office/combridge";

// Units deleted on the slide (PowerPoint's own Delete) must leave the sidebar editor; drafts never placed stay.
describe("ComSync present", () => {
  function setup() {
    let objects = ["a1", "a2", "b1", "draft"];
    const removed: string[][] = [];
    const adopted: string[] = [];
    let listener: (e: { data: unknown }) => void = () => {};
    const bridge: ComSyncBridge = { postMessage: () => {}, addEventListener: (_t, l) => { listener = l; } };
    const state = () => ({ objects: objects.map((id) => ({ id })), selection: [] as string[] });
    const session = {
      state: () => state(),
      importCompact: (payload: string) => ({ ...state(), selection: payload.split(",") }),
      remove: (ids: string[]) => { removed.push(ids); objects = objects.filter((o) => !ids.includes(o)); return state(); },
    };
    const hidden: string[][] = [];
    const host = { session: () => session, adopt: (_s: unknown, reason: string) => adopted.push(reason), report: (e: unknown) => { throw e; }, hideModules: (ids: string[]) => hidden.push(ids) } as unknown as ComSyncHost;
    new ComSync(host, bridge);
    const send = (data: unknown) => listener({ data });
    return { send, removed, adopted, hidden };
  }

  it("removes objects of units that disappeared from the slide", () => {
    const t = setup();
    t.send({ type: "selection", unitId: "A", payload: "a1,a2" });
    t.send({ type: "selection", unitId: "B", payload: "b1" });
    t.send({ type: "present", unitIds: ["B", "other"] });
    expect(t.removed).toEqual([["a1", "a2"]]);
    expect(t.adopted).toEqual(["loaded", "loaded", "native-edit"]);
  });

  it("keeps drafts and does nothing while every known unit is present", () => {
    const t = setup();
    t.send({ type: "selection", unitId: "B", payload: "b1" });
    t.send({ type: "present", unitIds: ["B"] });
    expect(t.removed).toEqual([]);
  });

  it("passes hidden modules to the library", () => {
    const t = setup();
    t.send({ type: "hiddenModules", ids: ["automotive"] });
    expect(t.hidden).toEqual([["automotive"]]);
  });
});

// Live sync to the slide redraws only the units an edit touched (changed objects and their dependents).
describe("ComSync live redraw", () => {
  function setup() {
    const objects = ["a1", "a2", "b1", "c1"];
    const sent: { units: { id: string }[]; remove: string[] }[] = [];
    let listener: (e: { data: unknown }) => void = () => {};
    const bridge: ComSyncBridge = {
      postMessage: (m: unknown) => {
        const msg = m as { type: string; id: number; units: { id: string }[]; remove: string[] };
        if (msg.type !== "units") return;
        sent.push({ units: msg.units, remove: msg.remove });
        queueMicrotask(() => listener({ data: { type: "ack", id: msg.id, ok: true } }));
      },
      addEventListener: (_t, l) => { listener = l; },
    };
    const unitOf = (id: string) => id[0].toUpperCase();
    const session = {
      drawPlan: (ids?: string[]) => {
        const groups = new Map<string, string[]>();
        for (const id of ids ?? objects) groups.set(unitOf(id), [...(groups.get(unitOf(id)) ?? []), id]);
        return { units: [...groups].map(([id, objectIds]) => ({ id, objectIds, payload: id, notes: [] })) };
      },
    };
    const host = { session: () => session, adopt: () => {}, report: (e: unknown) => { throw e; } } as unknown as ComSyncHost;
    const sync = new ComSync(host, bridge);
    const state = (ids: string[], changed?: string[], dependents?: string[]) =>
      ({ objects: ids.map((id) => ({ id })), selection: [], changed, dependents }) as never;
    return { sync, sent, state };
  }

  it("redraws only units with changed or dependent objects", async () => {
    const t = setup();
    await t.sync.insert();
    t.sent.length = 0;
    await t.sync.scheduleLive(t.state(["a1", "a2", "b1", "c1"], ["a2"], ["c1"]));
    expect(t.sent).toEqual([{ units: [{ id: "A", payload: "A" }, { id: "C", payload: "C" }].map((u) => expect.objectContaining(u)), remove: [] }]);
  });

  it("removes units whose objects are gone and redraws everything without change information", async () => {
    const t = setup();
    await t.sync.insert();
    t.sent.length = 0;
    await t.sync.scheduleLive(t.state(["a1", "a2", "c1"], ["b1"]));
    expect(t.sent[0].remove).toEqual(["B"]);
    expect(t.sent[0].units).toEqual([]);
    await t.sync.scheduleLive(t.state(["a1", "a2", "c1"]));
    expect(t.sent[1].units.map((u) => u.id)).toEqual(["A", "C"]);
  });
});
