import { afterEach, describe, expect, it } from "vitest";
import { Engine, Session, type EngineBackend } from "../src/engine/client";
import type { EditorState } from "../src/engine/types";
import type { HostCapabilities } from "../src/office/capabilities";
import { readSlideUnits } from "../src/office/slides";
import { SlideSync } from "../src/office/sync";

const caps: HostCapabilities = { office: true, tags: true, shapes: true, selection: true, groups: true, rotation: true, svg: true };

type FakeShape = { id: string; name: string; left: number; top: number; width: number; height: number; rotation: number; tags: Record<string, string> };

/** Office.js stand-in: selected shapes and the slides' shapes with bounds and tags; no-op sync. */
function fakePowerPoint(selected: FakeShape[], slides: { id: string; shapes: FakeShape[] }[] = []) {
  const coll = <T>(items: T[]) => ({ items, load() { return this; } });
  const tags = (t: Record<string, string>) => coll(Object.entries(t).map(([key, value]) => ({ key, value })));
  const shape = (s: FakeShape) => ({ ...s, tags: tags(s.tags) });
  const presentation = {
    getSelectedShapes: () => coll(selected.map(shape)),
    slides: coll(slides.map((s) => ({ id: s.id, shapes: coll(s.shapes.map(shape)) }))),
    tags: tags({}),
  };
  (globalThis as unknown as { PowerPoint: unknown }).PowerPoint = {
    run: async <R>(f: (context: unknown) => Promise<R>) => f({ presentation, sync: async () => undefined }),
  };
}

afterEach(() => { delete (globalThis as { PowerPoint?: unknown }).PowerPoint; });

function fakeEngine() {
  const calls: { method: string; args: Record<string, unknown> }[] = [];
  let objects: string[] = [];
  let selection: string[] = [];
  const backend: EngineBackend = {
    call(method, json) {
      const args = JSON.parse(json);
      calls.push({ method, args });
      const st = () => JSON.stringify({ ok: true, result: { session: "ed", revision: 1, pageId: "p", pageWidth: 254, pageHeight: 190.5, grid: 0, selection, objects: objects.map((id) => ({ id })), svg: method === "state" ? "<svg/>" : undefined } });
      if (method === "importCompact") { const ids = String(args.payload).split(","); objects = [...new Set([...objects, ...ids])]; selection = ids; }
      else if (method === "select") selection = args.ids;
      return st();
    },
  };
  return { session: new Session(new Engine(backend), "ed"), calls };
}

const unitShape = (id: string, unit: string, payload: string, extra: Partial<FakeShape> = {}): FakeShape => ({
  id, name: `ISK:basic.rect:${unit}`, left: 10, top: 10, width: 50, height: 20, rotation: 0,
  tags: { ISK_ID: unit, ISK_MODEL: payload, ISK_BOUNDS: JSON.stringify({ left: 10, top: 10, width: 50, height: 20, rotation: 0 }) }, ...extra,
});

describe("several units selected on the slide", () => {
  it("are all loaded into the editor and selected together (export and clean follow the slide's selection)", async () => {
    fakePowerPoint([unitShape("1", "u1", "a"), unitShape("2", "u1", "a"), unitShape("3", "u2", "b,c")]);
    const { session, calls } = fakeEngine();
    const adopted: { state: EditorState; reason: string }[] = [];
    const sync = new SlideSync({
      session: () => session, host: caps, draw: { freeform: false, arrowheads: false, patternFill: false, rotation: true, svg: true, shapes: true },
      adopt: (state, reason) => adopted.push({ state, reason }), report: (e) => { throw e; },
    });
    await sync.loadSelection();
    expect(calls.filter((c) => c.method === "importCompact").map((c) => c.args.payload)).toEqual(["a", "b,c"]);   // u1 once
    expect(calls.find((c) => c.method === "select")?.args.ids).toEqual(["a", "b", "c"]);
    expect(adopted).toHaveLength(1);
    expect(adopted[0].reason).toBe("loaded");
    expect(adopted[0].state.selection).toEqual(["a", "b", "c"]);
    expect(adopted[0].state.svg).toBe("<svg/>");                     // full state, not the select-only one
    expect([...sync.units.keys()]).toEqual(["u1", "u2"]);
  });

  it("loads a single unit as before (no extra select)", async () => {
    fakePowerPoint([unitShape("1", "u1", "a")]);
    const { session, calls } = fakeEngine();
    const adopted: string[] = [];
    const sync = new SlideSync({ session: () => session, host: caps, draw: { freeform: false, arrowheads: false, patternFill: false, rotation: true, svg: true, shapes: true }, adopt: (_s, r) => adopted.push(r), report: (e) => { throw e; } });
    await sync.loadSelection();
    expect(calls.map((c) => c.method)).toEqual(["importCompact"]);
    expect(adopted).toEqual(["loaded"]);
  });
});

describe("slide units with their bounds", () => {
  it("carry the stored and current bounds, so a unit moved in PowerPoint is placed where it is now", async () => {
    fakePowerPoint([], [{ id: "256", shapes: [unitShape("1", "u1", "a", { left: 30 })] }]);
    const [slide] = await readSlideUnits(caps);
    expect(slide.units[0]).toMatchObject({ unitId: "u1", payload: "a", shapeId: "1", current: { left: 30, top: 10, width: 50, height: 20, rotation: 0 }, stored: { left: 10 } });
  });
});
