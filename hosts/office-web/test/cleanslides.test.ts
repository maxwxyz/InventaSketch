import { afterEach, describe, expect, it } from "vitest";
import { Engine, Session, type EngineBackend } from "../src/engine/client";
import type { SanitizeReport } from "../src/engine/types";
import { setLang, t } from "../src/i18n";
import { cleanSlide, type SlideUnits } from "../src/office/slides";
import strings from "../src/strings.json";
import { addReport, cleanRequest, cleanScopes, defaultCleanForm, emptyTotals, formatCleanReport, parseSeed, randomSeed } from "../src/ui/cleanlogic";

afterEach(() => setLang("en"));

const report = (extra: Partial<SanitizeReport> = {}): SanitizeReport => ({
  mode: "normalised", seed: 42, elements: 2, changed: 1, values: 3, texts: 0, metadata: 1, references: 0, unknown: 0, failed: 0,
  byType: [], summary: "", lines: ["Chart: values normalised"], ...extra,
});

/**
 * Engine stub for one slide: importCompact adds the payload's comma-separated ids (the first is the unit), sanitize
 * reports `changedIds` as changed, drawPlan returns one unit per imported payload.
 */
function fakeEngine(changedIds: string[]) {
  const calls: { method: string; args: Record<string, unknown> }[] = [];
  const sessions = new Map<string, string[][]>();
  let n = 0;
  const backend: EngineBackend = {
    call(method, json) {
      const args = JSON.parse(json);
      calls.push({ method, args });
      const ok = (result: unknown) => JSON.stringify({ ok: true, result });
      const objs = (s: string) => (sessions.get(s) ?? []).flat();
      const st = (s: string, selection: string[] = [], changed?: string[]) =>
        ({ session: s, revision: 1, pageId: "p", pageWidth: 280, pageHeight: 157.5, grid: 0, selection, objects: objs(s).map((id) => ({ id })), changed });
      switch (method) {
        case "createSession": { const s = `s${++n}`; sessions.set(s, []); return ok(st(s)); }
        case "importCompact": { const ids = String(args.payload).split(","); sessions.get(args.session)!.push(ids); return ok(st(args.session, ids)); }
        case "hostTransform": return ok(st(args.session, args.ids));
        case "sanitize": return ok({ state: st(args.session, [], changedIds), report: report({ changed: changedIds.length, seed: args.value.seed }) });
        case "drawPlan": return ok({ unit: "pt", pageWidth: 0, pageHeight: 0, revision: 1, units: sessions.get(args.session)!.map((ids) => ({ id: ids[0], objectIds: ids, type: "t", name: `ISK:t:${ids[0]}`, mode: "native", bounds: { x: 0, y: 0, w: 1, h: 1 }, payload: `clean:${ids[0]}`, ops: [], notes: [] })) });
        case "closeSession": sessions.delete(args.session); return ok({});
        default: return JSON.stringify({ ok: false, error: "unknown " + method });
      }
    },
  };
  return { engine: new Engine(backend), calls, sessions };
}

const draw = { freeform: false, arrowheads: false, patternFill: false, rotation: true, svg: true, shapes: true };
const value = { renameCategories: false, replaceTexts: true, stripMeta: true, seed: 7 };

describe("clean slides: form", () => {
  it("defaults like the COM dialog: the selection if there is one, normalised, metadata removed", () => {
    expect(defaultCleanForm(true)).toEqual({ scope: "selection", mode: "normalised", renameCategories: false, replaceTexts: false, stripMeta: true, seed: "" });
    expect(defaultCleanForm(false).scope).toBe("slide");
  });

  it("offers the selection only with one, the slide or the page, and all slides only with access to the slides", () => {
    expect(cleanScopes(false, true)).toEqual([
      { scope: "selection", key: "clean.scope.selection", enabled: false },
      { scope: "slide", key: "clean.scope.slide", enabled: true },
      { scope: "all", key: "clean.scope.all", enabled: true },
    ]);
    expect(cleanScopes(true, false).map((o) => [o.key, o.enabled])).toEqual([["clean.scope.selection", true], ["clean.scope.page", true]]);
  });

  it("parses a typed seed as a 32-bit whole number, anything else as none", () => {
    expect(parseSeed(" 123 ")).toBe(123);
    expect(parseSeed("-5")).toBe(-5);
    expect(parseSeed("")).toBeUndefined();
    expect(parseSeed("1.5")).toBeUndefined();
    expect(parseSeed("abc")).toBeUndefined();
    expect(parseSeed("2147483648")).toBeUndefined();
    expect(randomSeed(() => 0)).toBe(1);
    expect(randomSeed(() => 0.999999999)).toBeLessThanOrEqual(2147483647);
  });

  it("maps the form to the engine request with one seed for the whole run", () => {
    const form = { ...defaultCleanForm(false), scope: "all" as const, mode: "randomised" as const, renameCategories: true, seed: "99" };
    expect(cleanRequest(form, () => 1)).toEqual({ scope: "all", mode: "randomised", value: { renameCategories: true, replaceTexts: false, stripMeta: true, seed: 99 } });
    // Empty seed: a new one is drawn, so the report can name it.
    expect(cleanRequest({ ...form, seed: "" }, () => 31337).value.seed).toBe(31337);
    expect(cleanRequest({ ...form, mode: "none", stripMeta: false }, () => 1).value).toMatchObject({ stripMeta: false });
  });
});

describe("clean slides: report", () => {
  it("adds the slides' reports and formats head, distinct lines (at most 12) and the note", () => {
    let total = emptyTotals(7);
    total = addReport(total, report({ seed: 7, elements: 3, changed: 2, lines: ["a", "b"] }));
    total = addReport(total, report({ seed: 7, elements: 1, changed: 0, lines: ["b", " "] }));
    expect(total).toEqual({ seed: 7, elements: 4, changed: 2, lines: ["a", "b", "b", " "], slides: 1 });
    const r = formatCleanReport(total, t);
    expect(r.head).toBe("2 of 4 element(s) cleaned (seed 7).");
    expect(r.lines).toEqual(["a", "b"]);
    expect(r.note).toMatch(/Native PowerPoint charts and tables, notes, comments and document properties are not changed/);
    setLang("de");
    expect(formatCleanReport(total, t).head).toBe("2 von 4 Element(en) bereinigt (Startwert 7).");
    const many = { ...total, lines: Array.from({ length: 20 }, (_, i) => `line ${i}`) };
    expect(formatCleanReport(many, t).lines).toHaveLength(12);
  });
});

describe("clean slides: one slide through the engine", () => {
  const slide: SlideUnits = {
    index: 2, slideId: "258", units: [
      { unitId: "u1", payload: "u1", shapeId: "1" },
      { unitId: "u2", payload: "u2,u2b", shapeId: "2", stored: { left: 0, top: 0, width: 10, height: 10 }, current: { left: 20, top: 0, width: 10, height: 10 } },
    ],
  };

  it("sanitizes every element with the request's options and redraws only the changed units", () => {
    const { engine, calls, sessions } = fakeEngine(["u2b"]);
    const r = cleanSlide(engine, slide, { w: 280, h: 157.5 }, "de", "randomised", value, draw);
    const sanitize = calls.find((c) => c.method === "sanitize")!.args;
    expect(sanitize).toMatchObject({ ids: [], mode: "randomised", value, lang: "de" });
    expect(r.slideId).toBe("258");
    expect(r.report.seed).toBe(7);
    expect(r.units.map((u) => [u.id, u.payload])).toEqual([["u2", "clean:u2"]]);
    // The unit moved in PowerPoint is placed where it is now before cleaning.
    expect(calls.filter((c) => c.method === "hostTransform").map((c) => c.args.ids)).toEqual([["u2", "u2b"]]);
    expect(sessions.size).toBe(0);
  });

  it("writes nothing when nothing changed", () => {
    const { engine, sessions } = fakeEngine([]);
    const r = cleanSlide(engine, slide, { w: 280, h: 157.5 }, "en", "normalised", value, draw);
    expect(r.units).toEqual([]);
    expect(r.report.changed).toBe(0);
    expect(sessions.size).toBe(0);
  });

  it("sends the editor's sanitize with ids, mode, options and language", () => {
    const { engine, calls } = fakeEngine(["a"]);
    const session = new Session(engine, engine.createSession("en").session);
    session.sanitize(["a", "b"], "none", value, "en");
    expect(calls.at(-1)).toMatchObject({ method: "sanitize", args: { session: "s1", ids: ["a", "b"], mode: "none", value, lang: "en" } });
  });
});

describe("web strings", () => {
  const table = strings as Record<string, { en: string; de: string }>;
  const sources = import.meta.glob("../src/**/*.ts", { query: "?raw", import: "default", eager: true }) as Record<string, string>;

  it("has every literal text key of the task pane in English and German", () => {
    const keys = new Set<string>();
    for (const code of Object.values(sources)) for (const m of code.matchAll(/\bt\(\s*"([^"]+)"/g)) keys.add(m[1]);
    for (const o of cleanScopes(true, true)) keys.add(o.key);
    for (const k of ["normalised", "randomised", "none"]) keys.add(`clean.mode.${k}`);
    for (const k of ["selection", "slide", "page"]) keys.add(`export.scope.${k}`);
    expect(keys.size).toBeGreaterThan(100);
    const missing = [...keys].filter((k) => !table[k]?.en || !table[k]?.de);
    expect(missing).toEqual([]);
  });

  it("never shows the word 'patent' (the feature is Compatibility / Kompatibilität)", () => {
    const hits = Object.entries(table).filter(([, v]) => /patent/i.test(v.en + " " + v.de)).map(([k]) => k);
    expect(hits).toEqual([]);
  });
});
