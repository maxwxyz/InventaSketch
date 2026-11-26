// Work across slides with Office.js: read the InventaSketch units of every slide (shape name + ISK_MODEL tags, or the
// presentation mirror), run the engine's compatibility check (preflight) per slide, compare a slide with the previous one (engine
// `diff`) and mark the differences, and go to a finding. The domain logic stays in the engine; this module only moves
// models between slides and engine sessions and draws marks.

import type { Engine } from "../engine/client";
import { Session } from "../engine/client";
import type { DrawCapabilities, EditorState, PlanUnit, PreflightFinding, SanitizeMode, SanitizeOptions, SanitizeReport } from "../engine/types";
import type { HostCapabilities } from "./capabilities";
import { joinTag, mirrorKey, nativelyEdited, parseName, TAG_BOUNDS, TAG_ID, TAG_MODEL, type BoxPt } from "./powerpoint";
import { otherThan, slideNumerals } from "./numerals";

type Bounds = BoxPt & { rotation?: number };

/**
 * A unit on a slide: its model and shape; `stored` (bounds when it was drawn) and `current` (bounds now) when known, so a
 * unit moved or resized in PowerPoint is placed where it is now before the engine works on it.
 */
export interface SlideUnit { unitId: string; payload: string; shapeId: string; stored?: Bounds; current?: Bounds }
export interface SlideUnits { index: number; slideId: string; units: SlideUnit[] }

/** A preflight finding on a slide (1-based number) with the shape of its unit, if any. */
export type SlideFinding = PreflightFinding & { slide: number; slideId: string; shapeId?: string };

export interface DiffBox { id: string; x: number; y: number; w: number; h: number }
export interface DiffResult { added: DiffBox[]; changed: DiffBox[]; removed: DiffBox[] }

/** Name prefix of the comparison marks (plain PowerPoint shapes, not InventaSketch units). */
export const DIFF_PREFIX = "ISK_DIFF";
const PT_PER_MM = 72 / 25.4;

/** True for the shape that carries a unit: "ISK:<type>:<id>", not a member "…#n". */
export function isUnitShapeName(name: string | undefined): boolean {
  return !!parseName(name) && !/#\d+$/.test(name ?? "");
}

/** Reads the units of every slide (or of the slides with the given zero-based indexes). Needs PowerPointApi 1.3. */
export async function readSlideUnits(caps: HostCapabilities, only?: number[]): Promise<SlideUnits[]> {
  if (!caps.tags) return [];
  return PowerPoint.run(async (context) => {
    const slides = context.presentation.slides;
    slides.load("items/id");
    const mirror = context.presentation.tags;
    mirror.load("items/key,items/value");
    await context.sync();
    const picked = slides.items.map((s, i) => ({ s, i })).filter((x) => !only || only.includes(x.i));
    const collections = picked.map(({ s, i }) => {
      const c = s.shapes;
      c.load(caps.rotation ? "items/name,items/id,items/left,items/top,items/width,items/height,items/rotation" : "items/name,items/id,items/left,items/top,items/width,items/height");
      return { slide: s, index: i, shapes: c };
    });
    await context.sync();
    const mirrorTags = new Map(mirror.items.map((t) => [t.key.toUpperCase(), t.value]));
    const pending: { index: number; shape: PowerPoint.Shape; tags: PowerPoint.TagCollection }[] = [];
    for (const c of collections)
      for (const shape of c.shapes.items)
        if (isUnitShapeName(shape.name)) {
          const tags = shape.tags;
          tags.load("items/key,items/value");
          pending.push({ index: c.index, shape, tags });
        }
    await context.sync();
    const result = collections.map((c) => ({ index: c.index, slideId: c.slide.id, units: [] as SlideUnit[] }));
    for (const p of pending) {
      const tags = new Map(p.tags.items.map((t) => [t.key.toUpperCase(), t.value]));
      const unitId = tags.get(TAG_ID) ?? parseName(p.shape.name)!.unitId;
      const payload = joinTag(TAG_MODEL, tags) ?? joinTag(mirrorKey(unitId), mirrorTags);
      const slide = result.find((r) => r.index === p.index)!;
      // A copy pasted inside PowerPoint keeps the unit id of its original: only the first one takes part.
      if (!payload || slide.units.some((u) => u.unitId === unitId)) continue;
      const unit: SlideUnit = { unitId, payload, shapeId: p.shape.id };
      const sh = p.shape;
      if ([sh.left, sh.top, sh.width, sh.height].every((v) => typeof v === "number")) {
        unit.current = { left: sh.left, top: sh.top, width: sh.width, height: sh.height };
        if (caps.rotation && typeof sh.rotation === "number") unit.current.rotation = sh.rotation;
        try {
          if (tags.has(TAG_BOUNDS)) unit.stored = JSON.parse(tags.get(TAG_BOUNDS)!);
        } catch {
          /* no usable reference bounds */
        }
      }
      slide.units.push(unit);
    }
    return result;
  });
}

const rect = (b: BoxPt) => ({ x: b.left, y: b.top, w: b.width, h: b.height });

/**
 * A temporary engine session with the slide's units (page = slide size in mm); units moved, resized or rotated in
 * PowerPoint since they were drawn are placed as they are now (as when the editor loads them). The caller closes it
 * (closeSession).
 */
export function slideSession(engine: Engine, units: SlideUnit[], page: { w: number; h: number }, lang: string): { session: Session; owner: Map<string, string> } {
  const st: EditorState = engine.createSession(lang, page.w, page.h);
  const session = new Session(engine, st.session);
  session.couple = false;
  const owner = new Map<string, string>();
  for (const u of units) {
    const s = session.importCompact(u.payload);
    for (const id of s.selection) owner.set(id, u.shapeId);
    if (u.stored && u.current && nativelyEdited({ unitId: u.unitId, type: "", stored: u.stored, current: u.current }))
      session.hostTransform(s.selection, rect(u.stored), rect(u.current), (u.current.rotation ?? 0) - (u.stored.rotation ?? 0));
  }
  return { session, owner };
}

export function closeSession(engine: Engine, session: Session): void {
  close(engine, session);
}

function close(engine: Engine, session: Session): void {
  try { engine.call("closeSession", { session: session.id }); } catch { /* already gone */ }
}

/**
 * Compatibility check (preflight) of every slide with units, one engine session per slide (the office's page rules apply to each
 * sheet), with the numerals of the other slides (`numeralsOf`), so the reference checks compare numerals between slides.
 * Findings carry the slide number and the shape of their element.
 */
export function checkSlides(engine: Engine, slides: SlideUnits[], profile: string, lang: string, page: { w: number; h: number }): { disclaimer: string; findings: SlideFinding[]; slides: number } {
  const findings: SlideFinding[] = [];
  let disclaimer = "";
  let checked = 0;
  const numerals = slideNumerals(engine, slides);
  for (const s of slides) {
    if (!s.units.length) continue;
    checked++;
    const { session, owner } = slideSession(engine, s.units, page, lang);
    try {
      const others = otherThan(numerals, s.index);
      const r = session.preflight(profile, lang, others.length ? others : undefined);
      disclaimer = r.disclaimer || disclaimer;
      for (const f of r.findings) findings.push({ ...f, slide: s.index + 1, slideId: s.slideId, shapeId: f.id ? owner.get(f.id) : undefined });
    } finally {
      close(engine, session);
    }
  }
  return { disclaimer, findings, slides: checked };
}

/** What differs from the previous slide's units to the current slide's (engine `diff`), bounds in mm. */
export function diffSlides(engine: Engine, previous: SlideUnits, current: SlideUnits, page: { w: number; h: number }, lang: string): DiffResult {
  const before = slideSession(engine, previous.units, page, lang);
  let payload: string;
  try {
    const all = before.session.state(false).objects.map((o) => o.id);
    payload = engine.call<{ payload: string }>("exportCompact", { session: before.session.id, ids: all }).payload;
  } finally {
    close(engine, before.session);
  }
  const after = slideSession(engine, current.units, page, lang);
  try {
    return engine.call<DiffResult>("diff", { session: after.session.id, payload });
  } finally {
    close(engine, after.session);
  }
}

/** A comparison mark in pt: frame padded by 1.5 mm, at least 4 mm; added green, changed orange, removed dashed grey. */
export interface DiffMark { kind: "added" | "changed" | "removed"; left: number; top: number; width: number; height: number; color: string; dashed: boolean }

export function diffMarks(d: DiffResult, pad = 1.5): DiffMark[] {
  const out: DiffMark[] = [];
  const kinds = [["added", "#2E9A3C", false], ["changed", "#F58000", false], ["removed", "#8C8C8C", true]] as const;
  for (const [kind, color, dashed] of kinds)
    for (const b of d[kind] ?? []) {
      const w = Math.max(4, b.w + 2 * pad), h = Math.max(4, b.h + 2 * pad);
      const cx = b.x + b.w / 2, cy = b.y + b.h / 2;
      out.push({ kind, color, dashed, left: (cx - w / 2) * PT_PER_MM, top: (cy - h / 2) * PT_PER_MM, width: w * PT_PER_MM, height: h * PT_PER_MM });
    }
  return out;
}

/**
 * Compares the selected slide with the previous one and marks the differences on it, or removes earlier marks (a second
 * call). Returns "cleared", "none" (nothing differs), "first" (no previous slide) or the counts.
 */
export async function compareWithPrevious(engine: Engine, caps: HostCapabilities, page: { w: number; h: number }, lang: string, legend: (d: DiffResult, previous: number) => string)
  : Promise<"cleared" | "none" | "first" | "unsupported" | DiffResult> {
  if (!caps.tags || !caps.selection || !caps.shapes) return "unsupported";
  const current = await currentSlide(caps);
  if (!current) return "unsupported";
  // A second call removes the marks.
  const cleared = await PowerPoint.run(async (context) => {
    const shapes = context.presentation.slides.getItem(current.id!).shapes;
    shapes.load("items/name");
    await context.sync();
    const marks = shapes.items.filter((s) => (s.name ?? "").startsWith(DIFF_PREFIX));
    marks.forEach((s) => s.delete());
    await context.sync();
    return marks.length;
  });
  if (cleared) return "cleared";
  if (current.index === 0) return "first";
  const [previous, mine] = await readSlideUnits(caps, [current.index - 1, current.index]);
  const d = diffSlides(engine, previous, mine, page, lang);
  const marks = diffMarks(d);
  if (!marks.length) return "none";
  await PowerPoint.run(async (context) => {
    const shapes = context.presentation.slides.getItem(current.id!).shapes;
    marks.forEach((m, i) => {
      const s = shapes.addGeometricShape("RoundRectangle" as PowerPoint.GeometricShapeType, { left: m.left, top: m.top, width: m.width, height: m.height });
      s.fill.clear();
      s.lineFormat.color = m.color;
      s.lineFormat.weight = 2;
      if (m.dashed) s.lineFormat.dashStyle = "Dash" as PowerPoint.ShapeLineDashStyle;
      s.name = `${DIFF_PREFIX} ${m.kind} ${i + 1}`;
    });
    const w = page.w * PT_PER_MM;
    const text = shapes.addTextBox(legend(d, current.index), { left: w - 250, top: 6, width: 244, height: 20 });
    text.textFrame.textRange.font.size = 9;
    text.textFrame.textRange.paragraphFormat.horizontalAlignment = "Right" as PowerPoint.ParagraphHorizontalAlignment;
    text.name = `${DIFF_PREFIX} legend`;
    await context.sync();
  });
  return d;
}

/** The shown (first selected) slide: zero-based index and id; undefined without the selection API (PowerPointApi 1.5). */
export async function currentSlide(caps: HostCapabilities): Promise<{ index: number; id: string } | undefined> {
  if (!caps.selection) return undefined;
  return PowerPoint.run(async (context) => {
    const sel = context.presentation.getSelectedSlides();
    sel.load("items/id");
    const all = context.presentation.slides;
    all.load("items/id");
    await context.sync();
    const id = sel.items[0]?.id;
    const index = all.items.findIndex((s) => s.id === id);
    return id && index >= 0 ? { index, id } : undefined;
  });
}

/** The units of the shown slide (an empty list without units); undefined without access to the slides. */
export async function readCurrentSlideUnits(caps: HostCapabilities): Promise<SlideUnits | undefined> {
  if (!caps.tags) return undefined;
  const current = await currentSlide(caps);
  if (!current) return undefined;
  return (await readSlideUnits(caps, [current.index]))[0] ?? { index: current.index, slideId: current.id, units: [] };
}

/** Result of cleaning one slide: the engine's report and the units to redraw on it (changed ones only). */
export interface CleanedSlide { slideId: string; index: number; report: SanitizeReport; units: PlanUnit[] }

/**
 * "Clean slides" for one slide: its units in a temporary session (as they are now on the slide), the engine's `sanitize`
 * over every element with the given options and seed, and the draw plan of the units that changed (same unit ids, so
 * writing them replaces the old shapes). Nothing is written here.
 */
export function cleanSlide(engine: Engine, slide: SlideUnits, page: { w: number; h: number }, lang: string,
  mode: SanitizeMode, value: SanitizeOptions, draw: DrawCapabilities): CleanedSlide {
  const { session } = slideSession(engine, slide.units, page, lang);
  try {
    const r = session.sanitize([], mode, value, lang);
    if (!r.report.changed) return { slideId: slide.slideId, index: slide.index, report: r.report, units: [] };
    const touched = r.state.changed === undefined ? undefined : new Set([...r.state.changed, ...(r.state.dependents ?? [])]);
    const onSlide = new Set(slide.units.map((u) => u.unitId));
    // Only units that are on this slide (a unit's stored copy of a docking target is not drawn again).
    const units = session.drawPlan(undefined, draw).units
      .filter((u) => onSlide.has(u.id) && (!touched || u.objectIds.some((id) => touched.has(id))));
    return { slideId: slide.slideId, index: slide.index, report: r.report, units };
  } finally {
    close(engine, session);
  }
}

/** Shows the finding's slide and selects its shape (PowerPointApi 1.5). */
export async function goToFinding(caps: HostCapabilities, slideId: string, shapeId?: string): Promise<void> {
  if (!caps.selection) return;
  await PowerPoint.run(async (context) => {
    context.presentation.setSelectedSlides([slideId]);
    await context.sync();
    if (shapeId) {
      context.presentation.slides.getItem(slideId).setSelectedShapes([shapeId]);
      await context.sync();
    }
  });
}
