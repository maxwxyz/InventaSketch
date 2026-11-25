// Office.js adapter: draws engine draw-plan units as native PowerPoint shapes (or an SVG picture), names and tags
// them, and reads InventaSketch groups back from the selection. Pure translation, no domain logic.
//
// Metadata (decision, see docs/en/hosts/office-web.md):
//  - shape name "ISK:<type>:<unitId>" on the group (members "…#n"): survives copy/paste and file round-trips;
//  - shape tags ISK_ID, ISK_TYPE, ISK_MODEL (compact model, chunked), ISK_BOUNDS (pt, for native-edit detection);
//    tags are the OOXML p:custDataLst store that the COM add-in reads via Shape.Tags;
//  - presentation tag ISK_M_<unitId> mirrors the model (PowerPointApi 1.3), used when shape tags are missing.

import type { PlanOp, PlanUnit } from "../engine/types";
import type { HostCapabilities } from "./capabilities";

export const TAG_ID = "ISK_ID";
export const TAG_TYPE = "ISK_TYPE";
export const TAG_MODEL = "ISK_MODEL";
export const TAG_BOUNDS = "ISK_BOUNDS";
export const TAG_MIRROR = "ISK_M_";
/** Conservative chunk size for tag values (no documented limit; COM and Office.js share the store). */
export const TAG_CHUNK = 30000;
/** Office.js shapes must never be 0 wide/high (PowerPoint for Mac substitutes one inch). */
export const MIN_EXTENT = 0.1;

export interface BoxPt { left: number; top: number; width: number; height: number }

export interface UnitOnSlide {
  unitId: string;
  type: string;
  payload?: string;
  /** Bounds stored when the unit was drawn. */
  stored?: BoxPt & { rotation?: number };
  /** Current bounds on the slide. */
  current: BoxPt & { rotation?: number };
}

/** Splits a long tag value into chunks: KEY, KEY_2 … KEY_n (+ KEY_N = n when chunked). */
export function chunkTag(key: string, value: string, size = TAG_CHUNK): [string, string][] {
  if (value.length <= size) return [[key, value]];
  const parts: [string, string][] = [];
  for (let i = 0, n = 1; i < value.length; i += size, n++) parts.push([n === 1 ? key : `${key}_${n}`, value.slice(i, i + size)]);
  parts.push([`${key}_N`, String(parts.length)]);
  return parts;
}

/** Key of a unit's model mirror in the presentation tags as read back (tag keys come back in upper case). */
export function mirrorKey(unitId: string): string {
  return (TAG_MIRROR + unitId).toUpperCase();
}

export function joinTag(key: string, tags: Map<string, string>): string | undefined {
  const first = tags.get(key);
  if (first === undefined) return undefined;
  const n = Number(tags.get(`${key}_N`) ?? "1");
  let s = first;
  for (let i = 2; i <= n; i++) s += tags.get(`${key}_${i}`) ?? "";
  return s;
}

/** Unit id from a shape name "ISK:<type>:<id>" or "ISK:<type>:<id>#n". */
export function parseName(name: string | undefined): { type: string; unitId: string } | undefined {
  const m = /^ISK:([^:]+):([^#]+)(#\d+)?$/.exec(name ?? "");
  return m ? { type: m[1], unitId: m[2] } : undefined;
}

/** Office box for a line from (x1,y1) to (x2,y2): addLine draws top-left→bottom-right, LineInverse the other diagonal. */
export function lineBox(op: PlanOp): { box: BoxPt; inverse: boolean } {
  const x1 = op.x1 ?? 0, y1 = op.y1 ?? 0, x2 = op.x2 ?? 0, y2 = op.y2 ?? 0;
  const inverse = (x2 - x1) * (y2 - y1) < 0;
  return { box: safeBox({ left: Math.min(x1, x2), top: Math.min(y1, y2), width: Math.abs(x2 - x1), height: Math.abs(y2 - y1) }), inverse };
}

/** Never 0 wide/high: grows around the centre so horizontal/vertical lines stay in place. */
export function safeBox(b: BoxPt): BoxPt {
  const width = Number.isFinite(b.width) && b.width >= MIN_EXTENT ? b.width : MIN_EXTENT;
  const height = Number.isFinite(b.height) && b.height >= MIN_EXTENT ? b.height : MIN_EXTENT;
  return { left: b.left - (width - b.width) / 2, top: b.top - (height - b.height) / 2, width, height };
}

/** Adjustment of a rounded rectangle: corner radius over the short side (at most 0.5); undefined without a radius. */
export function cornerAdjustment(op: PlanOp): number | undefined {
  const short = Math.min(op.w ?? 0, op.h ?? 0);
  if (!op.radius || op.radius <= 0 || short <= 0) return undefined;
  return Math.min(0.5, op.radius / short);
}

const dashMap: Record<string, string> = {
  solid: "Solid", dash: "Dash", dashDot: "DashDot", dashDotDot: "DashDotDot", dot: "RoundDot", longDash: "LongDash",
};

function hex(color: string | undefined): string {
  return color && /^#[0-9a-f]{6}$/i.test(color) ? color.toUpperCase() : "#000000";
}

function applyStyle(s: PowerPoint.Shape, op: PlanOp): void {
  if (op.op !== "line") {
    if (op.fill && op.fill.kind === "solid") s.fill.setSolidColor(hex(op.fill.color));
    else s.fill.clear();
    if (op.opacity !== undefined && op.opacity < 1 && op.fill?.kind === "solid") s.fill.transparency = 1 - op.opacity;
  }
  if (!op.stroke) {
    s.lineFormat.visible = false;
    return;
  }
  s.lineFormat.visible = true;
  s.lineFormat.color = hex(op.stroke.color);
  s.lineFormat.weight = Math.max(0.25, op.stroke.weight);
  s.lineFormat.dashStyle = (dashMap[op.stroke.dash] ?? "Solid") as PowerPoint.ShapeLineDashStyle;
  if (op.opacity !== undefined && op.opacity < 1) s.lineFormat.transparency = 1 - op.opacity;
}

/** Creates the shapes of one native op (groups are flattened; the caller groups the whole unit). */
export function drawOp(shapes: PowerPoint.ShapeCollection, op: PlanOp, caps: HostCapabilities, out: PowerPoint.Shape[]): void {
  const box = () => safeBox({ left: op.x ?? 0, top: op.y ?? 0, width: op.w ?? 0, height: op.h ?? 0 });
  let s: PowerPoint.Shape;
  switch (op.op) {
    case "group":
      for (const c of op.children ?? []) drawOp(shapes, c, caps, out);
      return;
    case "rect": {
      s = shapes.addGeometricShape((op.radius ? "RoundRectangle" : "Rectangle") as PowerPoint.GeometricShapeType, box());
      // The corner radius as PowerPoint's adjustment (PowerPointApi 1.10, the set of shape.rotation); without it
      // PowerPoint's default of a sixth of the short side would be drawn.
      const adj = cornerAdjustment(op);
      if (adj !== undefined && caps.rotation) s.adjustments.set(0, adj);
      break;
    }
    case "ellipse":
      s = shapes.addGeometricShape("Ellipse" as PowerPoint.GeometricShapeType, box());
      break;
    case "shape":
      s = shapes.addGeometricShape((op.shape === "diamond" ? "Diamond" : "Triangle") as PowerPoint.GeometricShapeType, box());
      break;
    case "line": {
      const { box: lb, inverse } = lineBox(op);
      s = inverse ? shapes.addGeometricShape("LineInverse" as PowerPoint.GeometricShapeType, lb) : shapes.addLine("Straight" as PowerPoint.ConnectorType, lb);
      break;
    }
    case "text": {
      s = shapes.addTextBox(op.text ?? "", box());
      const tf = s.textFrame;
      tf.wordWrap = false;
      tf.autoSizeSetting = "AutoSizeNone" as PowerPoint.ShapeAutoSize;
      tf.leftMargin = tf.rightMargin = tf.topMargin = tf.bottomMargin = 0;
      tf.verticalAlignment = "Middle" as PowerPoint.TextVerticalAlignment;
      const font = tf.textRange.font;
      font.size = Math.max(1, op.fontSize ?? 10);
      font.color = hex(op.color);
      if (op.font) font.name = op.font.split(",")[0].trim();
      font.bold = !!op.bold;
      font.italic = !!op.italic;
      // ShapeFont.underline is part of PowerPointApi 1.4 (the same set as addTextBox), so no line fallback is needed.
      font.underline = (op.underline ? "Single" : "None") as PowerPoint.ShapeFontUnderlineStyle;
      tf.textRange.paragraphFormat.horizontalAlignment = (op.align === "left" ? "Left" : op.align === "right" ? "Right" : "Center") as PowerPoint.ParagraphHorizontalAlignment;
      s.fill.clear();
      s.lineFormat.visible = false;
      if (op.rotation && caps.rotation) s.rotation = op.rotation;
      out.push(s);
      return;
    }
    default:
      // polyline/freeform/svg never reach native drawing with Office.js capabilities (the engine lowers them).
      throw new Error(`Op '${op.op}' is not drawable with Office.js shapes.`);
  }
  applyStyle(s, op);
  if (op.rotation && caps.rotation) s.rotation = op.rotation;
  out.push(s);
}

function targetSlide(context: PowerPoint.RequestContext, caps: HostCapabilities): PowerPoint.Slide {
  return caps.selection ? context.presentation.getSelectedSlides().getItemAt(0) : context.presentation.slides.getItemAt(0);
}

/** Deletes all shapes of the given units on the slide (by name). */
async function deleteUnits(context: PowerPoint.RequestContext, slide: PowerPoint.Slide, unitIds: Set<string>): Promise<number> {
  const shapes = slide.shapes;
  shapes.load("items/name,items/id");
  await context.sync();
  let n = 0;
  for (const s of shapes.items) {
    const parsed = parseName(s.name);
    if (parsed && unitIds.has(parsed.unitId)) {
      s.delete();
      n++;
    }
  }
  if (n) await context.sync();
  return n;
}

function tagAll(shape: PowerPoint.Shape, unit: PlanUnit): void {
  shape.tags.add(TAG_ID, unit.id);
  shape.tags.add(TAG_TYPE, unit.type);
  for (const [k, v] of chunkTag(TAG_MODEL, unit.payload)) shape.tags.add(k, v);
}

async function storeBounds(context: PowerPoint.RequestContext, shape: PowerPoint.Shape, caps: HostCapabilities): Promise<void> {
  shape.load(caps.rotation ? "left,top,width,height,rotation" : "left,top,width,height");
  await context.sync();
  const b: BoxPt & { rotation?: number } = { left: shape.left, top: shape.top, width: shape.width, height: shape.height };
  if (caps.rotation) b.rotation = shape.rotation;
  shape.tags.add(TAG_BOUNDS, JSON.stringify(b));
}

/** Draws native units on the current slide (replacing earlier shapes of the same units). */
export async function writeNativeUnits(units: PlanUnit[], caps: HostCapabilities): Promise<void> {
  await PowerPoint.run(async (context) => {
    const slide = targetSlide(context, caps);
    await deleteUnits(context, slide, new Set(units.map((u) => u.id)));
    for (const unit of units) {
      const created: PowerPoint.Shape[] = [];
      for (const op of unit.ops) drawOp(slide.shapes, op, caps, created);
      if (created.length === 0) continue;
      let main: PowerPoint.Shape;
      if (caps.groups && created.length > 1) {
        created.forEach((s, i) => (s.name = `${unit.name}#${i + 1}`));
        main = slide.shapes.addGroup(created);
      } else {
        created.forEach((s, i) => (s.name = i === 0 ? unit.name : `${unit.name}#${i + 1}`));
        main = created[0];
        // Without groups every part carries the id so the unit can still be found and replaced.
        created.slice(1).forEach((s) => s.tags.add(TAG_ID, unit.id));
      }
      main.name = unit.name;
      tagAll(main, unit);
      if (caps.tags) for (const [k, v] of chunkTag(TAG_MIRROR + unit.id, unit.payload)) context.presentation.tags.add(k, v);
      await context.sync();
      await storeBounds(context, main, caps);
    }
    await context.sync();
  });
}

/** Inserts an SVG unit as a picture (ImageCoercion 1.2), then names and tags the inserted shape. */
export async function writeSvgUnit(unit: PlanUnit, caps: HostCapabilities): Promise<void> {
  const op = unit.ops.find((o) => o.op === "svg");
  if (!op?.svg) return;
  if (caps.shapes || caps.selection) {
    await PowerPoint.run(async (context) => {
      const slide = targetSlide(context, caps);
      await deleteUnits(context, slide, new Set([unit.id]));
      // Inserting while a shape is selected could replace or nest into it: clear the shape selection first.
      if (caps.selection) slide.setSelectedShapes([]);
      await context.sync();
    });
  }
  await new Promise<void>((resolve, reject) => {
    Office.context.document.setSelectedDataAsync(
      op.svg!,
      { coercionType: Office.CoercionType.XmlSvg, imageLeft: op.x, imageTop: op.y, imageWidth: op.w, imageHeight: op.h },
      (r: Office.AsyncResult<void>) => (r.status === Office.AsyncResultStatus.Succeeded ? resolve() : reject(new Error(r.error?.message ?? "SVG insert failed"))),
    );
  });
  if (!caps.selection) return; // cannot find the picture again without the selection API
  await PowerPoint.run(async (context) => {
    const sel = context.presentation.getSelectedShapes();
    sel.load("items/name");
    await context.sync();
    const pic = sel.items[0];
    if (!pic) return;
    pic.name = unit.name;
    if (caps.tags) {
      tagAll(pic, unit);
      for (const [k, v] of chunkTag(TAG_MIRROR + unit.id, unit.payload)) context.presentation.tags.add(k, v);
    }
    await context.sync();
    if (caps.tags) await storeBounds(context, pic, caps);
  });
}

/** Writes units (native or SVG) in order. */
export async function writeUnits(units: PlanUnit[], caps: HostCapabilities): Promise<void> {
  const native = units.filter((u) => u.mode === "native");
  if (native.length) await writeNativeUnits(native, caps);
  for (const u of units.filter((x) => x.mode === "svg")) await writeSvgUnit(u, caps);
}

/** Removes units from the current slide. */
export async function deleteUnitShapes(unitIds: string[], caps: HostCapabilities): Promise<number> {
  if (!unitIds.length || !caps.shapes) return 0;
  return PowerPoint.run(async (context) => deleteUnits(context, targetSlide(context, caps), new Set(unitIds)));
}

async function readTags(context: PowerPoint.RequestContext, coll: PowerPoint.TagCollection): Promise<Map<string, string>> {
  coll.load("items/key,items/value");
  await context.sync();
  return new Map(coll.items.map((t) => [t.key.toUpperCase(), t.value]));
}

/** Reads the selected InventaSketch unit (group, picture or member shape) with model and bounds. */
export async function readSelectedUnit(caps: HostCapabilities): Promise<UnitOnSlide | undefined> {
  if (!caps.selection) return undefined;
  return PowerPoint.run(async (context) => {
    const sel = context.presentation.getSelectedShapes();
    sel.load(caps.rotation ? "items/name,items/left,items/top,items/width,items/height,items/rotation" : "items/name,items/left,items/top,items/width,items/height");
    await context.sync();
    for (const shape of sel.items) {
      const tags = caps.tags ? await readTags(context, shape.tags) : new Map<string, string>();
      const parsed = parseName(shape.name);
      const unitId = tags.get(TAG_ID) ?? parsed?.unitId;
      if (!unitId) continue;
      let payload = joinTag(TAG_MODEL, tags);
      if (!payload && caps.tags) payload = joinTag(mirrorKey(unitId), await readTags(context, context.presentation.tags));
      let stored: UnitOnSlide["stored"];
      try {
        stored = tags.has(TAG_BOUNDS) ? JSON.parse(tags.get(TAG_BOUNDS)!) : undefined;
      } catch {
        stored = undefined;
      }
      const current: UnitOnSlide["current"] = { left: shape.left, top: shape.top, width: shape.width, height: shape.height };
      if (caps.rotation) current.rotation = shape.rotation;
      return { unitId, type: tags.get(TAG_TYPE) ?? parsed?.type ?? "", payload, stored, current };
    }
    return undefined;
  });
}

/** Stores the current bounds of the selected unit as its reference (after the model adopted a native edit). */
export async function acknowledgeSelectedBounds(unitId: string, caps: HostCapabilities): Promise<void> {
  if (!caps.selection || !caps.tags) return;
  await PowerPoint.run(async (context) => {
    const sel = context.presentation.getSelectedShapes();
    sel.load("items/name");
    await context.sync();
    const shape = sel.items.find((s) => parseName(s.name)?.unitId === unitId);
    if (shape) await storeBounds(context, shape, caps);
    await context.sync();
  });
}

/** True if the user moved/resized/rotated the unit natively since it was drawn (tolerance 0.5 pt / 0.5°). */
export function nativelyEdited(u: UnitOnSlide): boolean {
  const s = u.stored;
  if (!s) return false;
  const d = (a: number, b: number) => Math.abs(a - b) > 0.5;
  return d(s.left, u.current.left) || d(s.top, u.current.top) || d(s.width, u.current.width) || d(s.height, u.current.height)
    || (s.rotation !== undefined && u.current.rotation !== undefined && d(s.rotation, u.current.rotation));
}
