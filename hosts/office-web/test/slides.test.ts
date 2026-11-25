import { afterEach, describe, expect, it } from "vitest";
import { Engine, type EngineBackend } from "../src/engine/client";
import type { HostCapabilities } from "../src/office/capabilities";
import { cornerAdjustment } from "../src/office/powerpoint";
import { checkSlides, diffMarks, diffSlides, isUnitShapeName, readSlideUnits, type SlideUnits } from "../src/office/slides";

const caps: HostCapabilities = { office: true, tags: true, shapes: true, selection: true, groups: true, rotation: true, svg: true };

/** Minimal Office.js object model: slides with shapes and tags, presentation tags, a no-op sync. */
function fakePowerPoint(slides: { id: string; shapes: { id: string; name: string; tags: Record<string, string> }[] }[], mirror: Record<string, string> = {}) {
  const coll = <T>(items: T[]) => ({ items, load() { return this; } });
  const tags = (t: Record<string, string>) => coll(Object.entries(t).map(([key, value]) => ({ key, value })));
  const presentation = {
    slides: coll(slides.map((s) => ({ id: s.id, shapes: coll(s.shapes.map((sh) => ({ id: sh.id, name: sh.name, tags: tags(sh.tags) }))) }))),
    tags: tags(mirror),
  };
  (globalThis as unknown as { PowerPoint: unknown }).PowerPoint = {
    run: async <R>(f: (context: unknown) => Promise<R>) => f({ presentation, sync: async () => undefined }),
  };
}

afterEach(() => { delete (globalThis as { PowerPoint?: unknown }).PowerPoint; });

/** Engine stub: records the calls, one session per createSession; objects are the imported payload's ids. */
function fakeEngine(findingsFor: (payloads: string[]) => { id?: string; severity: string; message: string }[]) {
  const calls: { method: string; args: Record<string, unknown> }[] = [];
  const sessions = new Map<string, string[]>();
  let n = 0;
  const backend: EngineBackend = {
    call(method, json) {
      const args = JSON.parse(json);
      calls.push({ method, args });
      const ok = (result: unknown) => JSON.stringify({ ok: true, result });
      const st = (s: string, selection: string[] = []) => ({ session: s, revision: 1, pageId: "p", pageWidth: 280, pageHeight: 157.5, grid: 0, selection, objects: (sessions.get(s) ?? []).map((id) => ({ id })) });
      switch (method) {
        case "createSession": { const s = `s${++n}`; sessions.set(s, []); return ok(st(s)); }
        case "importCompact": { const ids = String(args.payload).split(","); sessions.get(args.session)!.push(...ids); return ok(st(args.session, ids)); }
        case "state": return ok(st(args.session));
        case "preflight": return ok({ profile: args.text, disclaimer: "aid only", findings: findingsFor(sessions.get(args.session)!) });
        case "exportCompact": return ok({ payload: (args.ids as string[]).join(",") });
        case "diff": return ok({ added: [{ id: "c", x: 10, y: 10, w: 20, h: 10 }], changed: [], removed: [{ id: "b", x: 50, y: 10, w: 1, h: 1 }], before: args.payload });
        case "closeSession": sessions.delete(args.session); return ok({});
        default: return JSON.stringify({ ok: false, error: "unknown " + method });
      }
    },
  };
  return { engine: new Engine(backend), calls, sessions };
}

describe("slides: reading units", () => {
  it("takes unit shapes, not their members or other shapes", () => {
    expect(isUnitShapeName("ISK:mech.gear.spur:o1")).toBe(true);
    expect(isUnitShapeName("ISK:mech.gear.spur:o1#2")).toBe(false);
    expect(isUnitShapeName("Rechteck 3")).toBe(false);
    expect(isUnitShapeName("ISK_DIFF added 1")).toBe(false);
  });

  it("reads the model from shape tags or the presentation mirror, once per unit", async () => {
    fakePowerPoint([
      { id: "256", shapes: [
        { id: "1", name: "ISK:mech.gear.spur:u1", tags: { ISK_ID: "u1", ISK_MODEL: "a" } },
        { id: "2", name: "ISK:mech.gear.spur:u1", tags: { ISK_ID: "u1", ISK_MODEL: "a" } },          // pasted copy
        { id: "3", name: "ISK:basic.rect:u2", tags: {} },                                               // model in the mirror
        { id: "4", name: "Titel 1", tags: {} },
      ] },
      { id: "257", shapes: [{ id: "9", name: "ISK:basic.rect:u3", tags: { ISK_MODEL: "x", ISK_MODEL_2: "y", ISK_MODEL_N: "2" } }] },
    ], { ISK_M_u2: "m" });
    const slides = await readSlideUnits(caps);
    expect(slides.map((s) => s.slideId)).toEqual(["256", "257"]);
    expect(slides[0].units).toEqual([{ unitId: "u1", payload: "a", shapeId: "1" }, { unitId: "u2", payload: "m", shapeId: "3" }]);
    expect(slides[1].units).toEqual([{ unitId: "u3", payload: "xy", shapeId: "9" }]);
    expect((await readSlideUnits(caps, [1])).map((s) => s.index)).toEqual([1]);
    expect(await readSlideUnits({ ...caps, tags: false })).toEqual([]);
  });
});

describe("slides: patent check and comparison", () => {
  const slides: SlideUnits[] = [
    { index: 0, slideId: "256", units: [{ unitId: "u1", payload: "o1,o2", shapeId: "1" }] },
    { index: 1, slideId: "257", units: [] },
    { index: 2, slideId: "258", units: [{ unitId: "u3", payload: "o3", shapeId: "9" }] },
  ];

  it("checks every slide with units in its own session and points findings at slide and shape", () => {
    const { engine, calls, sessions } = fakeEngine((objs) => [
      { severity: "error", message: "sheet size" },
      { id: objs[objs.length - 1], severity: "warning", message: "outside" },
    ]);
    const r = checkSlides(engine, slides, "uspto", "de", { w: 280, h: 157.5 });
    expect(r.slides).toBe(2);
    expect(r.disclaimer).toBe("aid only");
    expect(r.findings.map((f) => [f.slide, f.slideId, f.shapeId ?? null, f.message])).toEqual([
      [1, "256", null, "sheet size"], [1, "256", "1", "outside"], [3, "258", null, "sheet size"], [3, "258", "9", "outside"],
    ]);
    expect(calls.filter((c) => c.method === "createSession").map((c) => [c.args.pageWidth, c.args.pageHeight])).toEqual([[280, 157.5], [280, 157.5]]);
    expect(calls.filter((c) => c.method === "preflight").every((c) => c.args.text === "uspto")).toBe(true);
    expect(sessions.size).toBe(0);           // every session closed
  });

  it("compares the previous slide's model with the current slide", () => {
    const { engine, calls, sessions } = fakeEngine(() => []);
    const d = diffSlides(engine, slides[0], slides[2], { w: 280, h: 157.5 }, "en");
    expect(d.added[0].id).toBe("c");
    expect((d as unknown as { before: string }).before).toBe("o1,o2");      // the previous slide's objects went to diff
    expect(calls.find((c) => c.method === "diff")!.args.session).not.toBe(calls.find((c) => c.method === "exportCompact")!.args.session);
    expect(sessions.size).toBe(0);
  });

  it("marks differences in pt, padded, coloured and dashed for removed ones", () => {
    const marks = diffMarks({ added: [{ id: "a", x: 10, y: 20, w: 30, h: 10 }], changed: [], removed: [{ id: "r", x: 0, y: 0, w: 1, h: 1 }] });
    const k = 72 / 25.4;
    expect(marks[0]).toMatchObject({ kind: "added", color: "#2E9A3C", dashed: false });
    expect(marks[0].left).toBeCloseTo(8.5 * k, 6);
    expect(marks[0].width).toBeCloseTo(33 * k, 6);
    expect(marks[1]).toMatchObject({ kind: "removed", dashed: true });
    expect(marks[1].width).toBeCloseTo(4 * k, 6);         // at least 4 mm
  });
});

describe("rounded rectangles", () => {
  it("passes the corner radius as PowerPoint's adjustment", () => {
    expect(cornerAdjustment({ op: "rect", x: 0, y: 0, w: 100, h: 50, radius: 3 } as never)).toBeCloseTo(0.06, 9);
    expect(cornerAdjustment({ op: "rect", w: 10, h: 10, radius: 8 } as never)).toBe(0.5);
    expect(cornerAdjustment({ op: "rect", w: 10, h: 10 } as never)).toBeUndefined();
  });
});
