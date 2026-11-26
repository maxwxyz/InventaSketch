import { describe, expect, it } from "vitest";
import { Engine, type EngineBackend } from "../src/engine/client";
import { isNumberingAction, otherThan, slideNumerals } from "../src/office/numerals";
import { checkSlides, type SlideUnits } from "../src/office/slides";

/** Engine stub: `numeralsOf` gives numeral "<payload>0" of kind "<payload>" per payload; preflight echoes `elsewhere`. */
function fakeEngine(withNumerals = true) {
  const calls: { method: string; args: Record<string, unknown> }[] = [];
  let n = 0;
  const backend: EngineBackend = {
    call(method, json) {
      const args = JSON.parse(json);
      calls.push({ method, args });
      const ok = (result: unknown) => JSON.stringify({ ok: true, result });
      const st = (s: string) => ({ session: s, revision: 1, pageId: "p", pageWidth: 280, pageHeight: 157.5, grid: 0, selection: [], objects: [] });
      switch (method) {
        case "numeralsOf":
          if (!withNumerals) break;
          return ok((JSON.parse(args.json) as string[]).map((p) => (p === "empty" ? [] : [{ numeral: `${p}0`, label: p, kind: p }])));
        case "createSession": return ok(st(`s${++n}`));
        case "importCompact": return ok(st(args.session));
        case "preflight": return ok({ profile: args.text, disclaimer: "aid only", findings: [] });
        case "closeSession": return ok({});
      }
      return JSON.stringify({ ok: false, error: "unknown " + method });
    },
  };
  return { engine: new Engine(backend), calls };
}

const slides: SlideUnits[] = [
  { index: 0, slideId: "256", units: [{ unitId: "u1", payload: "1", shapeId: "1" }, { unitId: "u2", payload: "2", shapeId: "2" }] },
  { index: 1, slideId: "257", units: [] },
  { index: 2, slideId: "258", units: [{ unitId: "u3", payload: "empty", shapeId: "9" }] },
  { index: 3, slideId: "259", units: [{ unitId: "u4", payload: "4", shapeId: "7" }] },
];

describe("numerals across slides", () => {
  it("recognises numbering actions (page command and the numeral's menu)", () => {
    expect(isNumberingAction("patent.numerals.auto=tens")).toBe(true);
    expect(isNumberingAction("t:patent.numerals.auto=ones")).toBe(true);
    expect(isNumberingAction("t:patent.numerals.renumber")).toBe(false);
    expect(isNumberingAction("delete")).toBe(false);
  });

  it("collects the numerals of the other slides in one engine call, labelled with slide numbers", () => {
    const { engine, calls } = fakeEngine();
    const other = slideNumerals(engine, slides, 3);
    expect(calls.filter((c) => c.method === "numeralsOf")).toHaveLength(1);
    expect(JSON.parse(calls[0].args.json as string)).toEqual(["1", "2", "empty"]);
    // Slides without units or numerals are left out; a slide's units are merged.
    expect(other).toEqual([{ page: "1", numerals: [{ numeral: "10", label: "1", kind: "1" }, { numeral: "20", label: "2", kind: "2" }] }]);
    expect(otherThan(slideNumerals(engine, slides), 0).map((p) => p.page)).toEqual(["4"]);
    // An engine without numeralsOf: the slide counts alone.
    expect(slideNumerals(fakeEngine(false).engine, slides, 3)).toEqual([]);
  });

  it("checks every slide against the numerals of the others", () => {
    const { engine, calls } = fakeEngine();
    checkSlides(engine, slides, "epo", "en", { w: 280, h: 157.5 });
    const checks = calls.filter((c) => c.method === "preflight").map((c) => c.args);
    expect(checks).toHaveLength(3);
    expect((checks[0].elsewhere as { page: string }[]).map((p) => p.page)).toEqual(["4"]);
    expect(checks[0].pageName).toBe("slide");
    expect((checks[1].elsewhere as { page: string }[]).map((p) => p.page)).toEqual(["1", "4"]);
    expect((checks[2].elsewhere as { page: string }[]).map((p) => p.page)).toEqual(["1"]);
  });
});
