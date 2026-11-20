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
