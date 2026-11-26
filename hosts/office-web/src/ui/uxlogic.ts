// Pure rules of the UI/UX round 2026-11-24 (ROADMAP "UX rules"): context-only transforms, the multi-selection bar,
// "Select assembly", where the snap label goes and the motion path object of a coupler curve. No DOM access.

import type { ActionInfo, ArrangeOption, ObjectInfo, PreflightFinding, PreflightSeverity, SnapGuide, TraceInfo, TransformEffect, Vec2 } from "../engine/types";

/** Bar/pie action ids of the quick transforms and the engine flag that says whether they change the drawing. */
const TRANSFORMS: Record<string, keyof TransformEffect> = { flipH: "mirrorH", flipV: "mirrorV", rotate90: "rotate90" };

/**
 * Drops mirror and rotate actions that would not change the drawing (a circle, a gear in axial view, a symmetric
 * bearing); every other action stays. Without an effect (no known object, engine error) nothing is dropped.
 */
export function filterTransforms(actions: ActionInfo[], effect: TransformEffect | undefined): ActionInfo[] {
  if (!effect) return actions;
  return actions.filter((a) => {
    const flag = TRANSFORMS[a.id];
    return flag === undefined || effect[flag];
  });
}

/** Number of selected elements: the objects of one group count as one element. */
export function elementCount(objects: ObjectInfo[], selection: string[]): number {
  const byId = new Map(objects.map((o) => [o.id, o]));
  return new Set(selection.map((id) => byId.get(id)?.groupId ?? id)).size;
}

/** The multi-selection bar: "one view for all" buttons (at most six) first, then align, distribute, match, renumber. */
export interface MultiBarGroups { views: ArrangeOption[]; arrange: ArrangeOption[] }

export function multiBarGroups(options: ArrangeOption[]): MultiBarGroups {
  return {
    views: options.filter((o) => o.id.startsWith("mview:")).slice(0, 6),
    arrange: options.filter((o) => !o.id.startsWith("mview:")),
  };
}

/** "Select assembly" only does something when coupled objects outside the selection exist. */
export function assemblyExtends(assembly: string[], selection: string[]): boolean {
  const sel = new Set(selection);
  return assembly.some((id) => !sel.has(id));
}

/**
 * Page point the snap label is drawn next to: the docking point, else a point on the snapped circle (pitch circle),
 * the middle of a snapped segment or a point on an alignment line; undefined without guides.
 */
export function snapLabelAnchor(guides: SnapGuide[]): Vec2 | undefined {
  const point = guides.find((g) => g.kind === "point");
  if (point) return point.a;
  const circle = guides.find((g) => g.kind === "circle");
  if (circle) {
    const d = circle.radius * Math.SQRT1_2;
    return { x: circle.a.x + d, y: circle.a.y - d };
  }
  const seg = guides.find((g) => g.kind === "segment");
  if (seg) return { x: (seg.a.x + seg.b.x) / 2, y: (seg.a.y + seg.b.y) / 2 };
  return guides.find((g) => g.kind === "vertical" || g.kind === "horizontal")?.a;
}

/** A coupler curve worth drawing: the chain moves with one input (F = 1) and the path has at least three points. */
export function canTrace(trace: TraceInfo | undefined): trace is TraceInfo {
  return !!trace && trace.points.length >= 3;
}

/** Library item of the motion path for a traced curve. */
export function tracedPathItem(trace: TraceInfo): "kin.path" | "kin.path.closed" {
  return trace.closed ? "kin.path.closed" : "kin.path";
}

const round4 = (v: number) => Math.round(v * 10000) / 10000;

/**
 * The inserted motion path (document object as in saveDocument) with the traced points; attachments of the default
 * points are dropped and an open path loses its arrowhead (same as the COM add-in).
 */
export function withTracedPoints(obj: Record<string, unknown>, trace: TraceInfo): Record<string, unknown> {
  const params: Record<string, unknown> = { ...((obj.params as Record<string, unknown> | undefined) ?? {}) };
  if (!trace.closed) params.arrow = false;
  const out: Record<string, unknown> = { ...obj, points: trace.points.map((p) => ({ x: round4(p.x), y: round4(p.y) })), params };
  delete out.attach;
  return out;
}

const SEVERITY_ORDER: Record<PreflightSeverity, number> = { error: 0, warning: 1, manual: 2, info: 3 };

/** Compatibility check list: errors first, then warnings, points to check by hand and notes; the engine order within each. */
export function orderFindings(findings: PreflightFinding[]): PreflightFinding[] {
  return findings.map((f, i) => ({ f, i }))
    .sort((a, b) => (SEVERITY_ORDER[a.f.severity] ?? 9) - (SEVERITY_ORDER[b.f.severity] ?? 9) || a.i - b.i)
    .map((x) => x.f);
}

/** Number of findings per severity (for the summary line). */
export function findingCounts(findings: PreflightFinding[]): Record<PreflightSeverity, number> {
  const c: Record<PreflightSeverity, number> = { error: 0, warning: 0, manual: 0, info: 0 };
  for (const f of findings) if (f.severity in c) c[f.severity]++;
  return c;
}

/** A cited source as label and link: "DE-01: PatV § 6(2) <https://…>; EP-02: …" → [{ label, url }, …]. */
export function sourceLinks(source: string): { label: string; url?: string }[] {
  return source.split(/;\s+(?=[A-Z]{2,4}-\d)/).map((part) => {
    const m = /^(.*?)\s*<(https?:\/\/[^>\s]+)>\s*$/.exec(part.trim());
    return m ? { label: m[1], url: m[2] } : { label: part.trim() };
  }).filter((x) => x.label.length > 0);
}

/** Which import a file takes: SVG/DXF drawings as shapes, GeoJSON/GPX as map data, anything else as a chart table. */
export function importKind(name: string, text: string): "vector" | "geo" | "table" {
  if (/\.(svg|dxf)$/i.test(name)) return "vector";
  if (/\.(gpx|geojson)$/i.test(name)) return "geo";
  if (/\.json$/i.test(name) && /"type"\s*:\s*"(Feature|FeatureCollection|LineString|Point|Polygon)/.test(text)) return "geo";
  const head = text.replace(/^\uFEFF/, "").trimStart();
  if ((head.startsWith("<") && /<svg[\s>]/i.test(head)) || /^0\s*\r?\nSECTION/.test(head)) return "vector";
  return "table";
}

/** One menu of page commands ("Number all parts ▸"): its options and whether any of them can do something now. */
export interface PageMenu { key: string; label: string; icon: string; enabled: boolean; options: ActionInfo[] }

/**
 * The engine's page commands (`pageActions`, no selection needed) as menus, one per choice in engine order (a command
 * without a choice is a menu of its own). The top bar shows a menu button only for an `enabled` menu (context-only: on a
 * page without parts, or once every part has a numeral, "Number all parts" can do nothing, so there is no button).
 */
export function pageMenus(actions: ActionInfo[]): PageMenu[] {
  const menus: PageMenu[] = [];
  for (const a of actions) {
    const key = a.choice ?? a.id;
    let m = menus.find((x) => x.key === key);
    // The menu button's pictogram comes from the engine (choiceBarIcon), else the generic "more".
    if (!m) { m = { key, label: a.choiceLabel ?? a.label, icon: a.choiceBarIcon ?? "more", enabled: false, options: [] }; menus.push(m); }
    m.options.push(a);
    m.enabled ||= a.enabled;
  }
  return menus;
}

