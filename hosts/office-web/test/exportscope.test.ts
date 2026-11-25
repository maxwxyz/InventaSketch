import { afterEach, describe, expect, it } from "vitest";
import { Engine, Session, type EngineBackend } from "../src/engine/client";
import type { EditorState, ObjectInfo } from "../src/engine/types";
import { editorExportScope, exportFileStem, exportScopeLabel, resolveExportSource } from "../src/export";
import { setLang, t } from "../src/i18n";
import type { SlideUnits } from "../src/office/slides";

afterEach(() => setLang("en"));

const obj = (id: string, groupId?: string) => ({ id, groupId }) as ObjectInfo;

function state(selection: string[], objects: ObjectInfo[] = [obj("a"), obj("b"), obj("g1", "G"), obj("g2", "G")]): EditorState {
  return { session: "ed", revision: 1, pageId: "p", pageWidth: 254, pageHeight: 190.5, grid: 0, selection, canUndo: false, canRedo: false, objects, monochrome: false };
}

/** Engine stub: temporary sessions with the imported payload ids; records calls. */
function fakeEngine() {
  const calls: { method: string; args: Record<string, unknown> }[] = [];
  const sessions = new Map<string, string[]>();
  let n = 0;
  const backend: EngineBackend = {
    call(method, json) {
      const args = JSON.parse(json);
      calls.push({ method, args });
      const ok = (result: unknown) => JSON.stringify({ ok: true, result });
      const st = (s: string, selection: string[] = []) => ({ ...state(selection, (sessions.get(s) ?? []).map((id) => obj(id))), session: s, monochrome: true });
      switch (method) {
        case "createSession": { const s = `t${++n}`; sessions.set(s, []); return ok(st(s)); }
        case "importCompact": { const ids = String(args.payload).split(","); sessions.get(args.session)!.push(...ids); return ok(st(args.session, ids)); }
        case "state": return ok(st(args.session));
        case "closeSession": sessions.delete(args.session); return ok({});
        default: return JSON.stringify({ ok: false, error: "unknown " + method });
      }
    },
  };
  return { engine: new Engine(backend), calls, sessions };
}

describe("export scope: label", () => {
  it("is the selection (a group counts once) if there is one, else the slide, or the page outside PowerPoint", () => {
    expect(editorExportScope(state(["a"]), true)).toEqual({ kind: "selection", count: 1 });
    expect(editorExportScope(state(["a", "b", "g1", "g2"]), true)).toEqual({ kind: "selection", count: 3 });
    expect(editorExportScope(state([]), true)).toEqual({ kind: "slide" });
    expect(editorExportScope(state([]), false)).toEqual({ kind: "page" });
  });

  it("names the scope like the COM add-in, in both languages", () => {
    expect(exportScopeLabel({ kind: "selection", count: 3 }, t)).toBe("Export – selection (3)");
    expect(exportScopeLabel({ kind: "slide" }, t)).toBe("Export – slide");
    expect(exportScopeLabel({ kind: "page" }, t)).toBe("Export – page");
    setLang("de");
    expect(exportScopeLabel({ kind: "selection", count: 1 }, t)).toBe("Export – Auswahl (1)");
    expect(exportScopeLabel({ kind: "slide", slide: 2 }, t)).toBe("Export – Folie");
  });

  it("names the files after the scope", () => {
    expect(exportFileStem({ kind: "selection", count: 2 })).toBe("inventasketch-selection");
    expect(exportFileStem({ kind: "slide", slide: 3 })).toBe("inventasketch-slide-3");
    expect(exportFileStem({ kind: "page" })).toBe("inventasketch");
  });
});

describe("export scope: resolution", () => {
  const slide: SlideUnits = { index: 2, slideId: "258", units: [{ unitId: "u1", payload: "u1", shapeId: "1" }, { unitId: "u2", payload: "u2", shapeId: "2" }] };

  it("exports the editor selection (single or several elements) without reading the slide", async () => {
    const { engine } = fakeEngine();
    const editor = new Session(engine, "ed");
    let read = 0;
    const src = await resolveExportSource({ engine, editor, state: state(["a", "g1", "g2"]), slideAccess: true, lang: "en", readSlide: async () => { read++; return slide; } });
    expect(src.scope).toEqual({ kind: "selection", count: 2 });
    expect(src.session).toBe(editor);
    expect(src.ids).toEqual(["a", "g1", "g2"]);
    expect(src.empty).toBe(false);
    expect(read).toBe(0);
  });

  it("without a selection exports the current slide's elements from a temporary session, closed afterwards", async () => {
    const { engine, sessions } = fakeEngine();
    const editor = new Session(engine, "ed");
    const src = await resolveExportSource({ engine, editor, state: state([]), slideAccess: true, lang: "en", readSlide: async () => slide });
    expect(src.scope).toEqual({ kind: "slide", count: 2, slide: 3 });
    expect(src.session).not.toBe(editor);
    expect(src.ids).toBeUndefined();
    expect(sessions.get(src.session.id)).toEqual(["u1", "u2"]);
    expect(src.monochrome).toBe(true);                          // the slide model's black-and-white setting
    src.dispose();
    expect(sessions.size).toBe(0);
  });

  it("says so for a slide without InventaSketch elements", async () => {
    const { engine, sessions } = fakeEngine();
    const src = await resolveExportSource({ engine, editor: new Session(engine, "ed"), state: state([]), slideAccess: true, lang: "en", readSlide: async () => ({ index: 0, slideId: "256", units: [] }) });
    expect(src.empty).toBe(true);
    expect(src.scope).toMatchObject({ kind: "slide", slide: 1 });
    expect(sessions.size).toBe(0);
  });

  it("exports the editor page without access to the slides", async () => {
    const { engine } = fakeEngine();
    const editor = new Session(engine, "ed");
    const noAccess = await resolveExportSource({ engine, editor, state: state([]), slideAccess: false, lang: "en", readSlide: async () => slide });
    expect(noAccess).toMatchObject({ scope: { kind: "page" }, ids: undefined, empty: false });
    expect(noAccess.session).toBe(editor);
    const unreadable = await resolveExportSource({ engine, editor, state: state([]), slideAccess: true, lang: "en", readSlide: async () => undefined });
    expect(unreadable.scope).toEqual({ kind: "page" });
  });
});
