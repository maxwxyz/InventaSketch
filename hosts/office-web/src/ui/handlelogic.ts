// Pure rules of the handles on the drawing (docs/en/design/interaction-ux.md section 4, like the COM add-in's
// OnSlideController.Handles/Links/Corners/Values): progressive disclosure (only the one primary "+" right after a
// selection, everything else fades in near the pointer, Shift shows all), the primary "+", where a corner dot puts its
// corner, the value card's ↔ drag and its range line. Screen pixels and page mm; no DOM (unit-tested).

import type { BuildPortInfo, CheckInfo, DragHandleInfo, LineEndInfo, PolylineHandles, ValueLevel, VarInfo, Vec2 } from "../engine/types";

/**
 * Pointer distance (px) within which a secondary handle is fully shown, and beyond which it is gone; in between it fades
 * (COM `RevealNear`/`RevealFar`).
 */
export const REVEAL_NEAR = 24, REVEAL_FAR = 56;

/** Sizes of the handles (px, as in the COM add-in): lock badge and its offset, corner dot, branch handle, insert dot. */
export const BADGE_PX = 7.5, BADGE_OFFSET_PX = 13, CORNER_DOT_PX = 3.6, BRANCH_PX = 5, BRANCH_OFFSET_PX = 16, GAP_SCALE = 0.72;
/** Segments shorter than this on screen get no corner dot or branch handle; elements smaller than this none at all (px). */
export const MIN_SEGMENT_PX = 28, SMALL_ELEMENT_PX = 24;
/** The ⚠ badge sits this far outside the top right corner of the element (px). */
export const VALUE_BADGE_PX = 6;

/** Opacity of a secondary handle for a pointer distance (px): 1 within `REVEAL_NEAR`, 0 beyond `REVEAL_FAR`, linear between. */
export function revealFor(distance: number): number {
  if (distance <= REVEAL_NEAR) return 1;
  if (distance >= REVEAL_FAR) return 0;
  return (REVEAL_FAR - distance) / (REVEAL_FAR - REVEAL_NEAR);
}

/**
 * How much of a handle is shown (0..1): the primary one and every handle while Shift is held fully; else by the pointer's
 * distance (px) to the handle or to its anchor (a lock badge also fades in near its contact point), 0 without a pointer.
 */
export function revealOpacity(at: Vec2, anchor: Vec2 | undefined, pointer: Vec2 | undefined, primary: boolean, shift: boolean): number {
  if (primary || shift) return 1;
  if (!pointer) return 0;
  let d = Math.hypot(at.x - pointer.x, at.y - pointer.y);
  if (anchor) d = Math.min(d, Math.hypot(anchor.x - pointer.x, anchor.y - pointer.y));
  return revealFor(d);
}

/**
 * The one "+" shown right away (COM `PrimaryHandleId`), as an index into `ports`, or -1. Lines: the "+" at the free end
 * of the line (the port nearest its last point), else at its start, else the first port; other elements: the free side
 * of a gear to the right, any meshing side, a connection port, a free axis end, then any build port. Diagram nodes with
 * quick-add triangles have the triangle below as their primary handle (`chainPrimary`), so no "+" is primary.
 */
export function primaryPort(ports: readonly BuildPortInfo[], points?: readonly Vec2[], chain = false): number {
  if (chain || ports.length === 0) return -1;
  if (points && points.length >= 2) {
    const ends = ports.map((p, i) => ({ p, i })).filter((x) => x.p.kind === "LineEnd");
    const near = (q: Vec2) => {
      let best = -1, bestD = 0.5;
      for (const e of ends) {
        const d = Math.hypot(e.p.x - q.x, e.p.y - q.y);
        if (d < bestD) { best = e.i; bestD = d; }
      }
      return best;
    };
    const end = near(points[points.length - 1]);
    if (end >= 0) return end;
    const start = near(points[0]);
    return start >= 0 ? start : 0;
  }
  const order: ((p: BuildPortInfo) => boolean)[] = [
    (p) => p.kind === "Mesh" && p.id === "mesh.right",
    (p) => p.kind === "Mesh",
    (p) => p.kind === "Connect",
    (p) => p.kind === "AxisEnd" && p.id.endsWith(".end"),
    (p) => p.kind === "AxisEnd",
    () => true,
  ];
  for (const f of order) {
    const i = ports.findIndex(f);
    if (i >= 0) return i;
  }
  return -1;
}

/**
 * Where the corner of a dot goes (COM `DrawnCornerAt`): the dot's drawn place projected onto its segment a…b, kept clear
 * of the ends (8 % … 92 %); the engine's own point `fallback` when the dot sits within `tolerance` mm of it.
 */
export function cornerAt(a: Vec2, b: Vec2, drawn: Vec2, fallback: Vec2, tolerance: number): Vec2 {
  const dx = b.x - a.x, dy = b.y - a.y;
  const len2 = dx * dx + dy * dy;
  if (len2 < 1e-12) return fallback;
  const t = Math.max(0.08, Math.min(0.92, ((drawn.x - a.x) * dx + (drawn.y - a.y) * dy) / len2));
  const at = { x: a.x + dx * t, y: a.y + dy * t };
  return Math.hypot(at.x - fallback.x, at.y - fallback.y) < tolerance ? fallback : at;
}

/** The grip dragged by pressing on the selected part (`byPart`, a whole curve), or undefined (COM `BodyGrip`). */
export function bodyGrip(grips: readonly DragHandleInfo[], selectedPart: string | undefined): DragHandleInfo | undefined {
  return selectedPart ? grips.find((g) => g.byPart && g.part === selectedPart) : undefined;
}

/** Pixels for a factor e (≈ 2.7) of a value while dragging its ↔ grip in the value card (COM `PixelsPerE`). */
export const PIXELS_PER_E = 90;

/** The value after dragging `dx` px from `from`: logarithmic, rounded to three significant digits (COM `DragValue`). */
export function dragValueStep(from: number, dx: number, scale = 1): number {
  if (!(from > 0) || !Number.isFinite(from)) return from;
  const v = from * Math.exp(dx / (PIXELS_PER_E * Math.max(0.5, scale)));
  const mag = Math.pow(10, Math.floor(Math.log10(v)) - 2);
  return Math.round(v / mag) * mag;
}

/** A value row offers the ↔ grip: Explore level and a positive number (COM `CanDrag`). */
export function canDragValue(v: Pick<VarInfo, "value" | "readOnly">, level: ValueLevel): boolean {
  return level === "explore" && !v.readOnly && typeof v.value === "number" && v.value > 0;
}

/** The range line under a value with tolerances ("180 V … 220 V"), "" without one. */
export function rangeText(v: Pick<VarInfo, "range">): string {
  return v.range ? v.range : "";
}

/**
 * A grip shown in full while `selectedPart` is selected (COM `ActionChoiceBar.ShownWith`): the grip of that part and the
 * grips that come with it (the point grips of a selected curve, and of the curve of a selected point); every other grip
 * fades in near the pointer.
 */
export function shownWith(grip: Pick<DragHandleInfo, "part" | "showWith">, selectedPart: string | undefined, all: readonly Pick<DragHandleInfo, "part" | "showWith">[]): boolean {
  if (!selectedPart) return false;
  if (grip.part === selectedPart) return true;
  if (!grip.showWith) return false;
  const group = all.find((g) => g.part === selectedPart && g.showWith)?.showWith ?? selectedPart;
  return grip.showWith === group;
}

/** Corner `index` of a line can be removed by a double-click (the engine's `removable` list). */
export function canRemoveCorner(removable: readonly number[] | undefined, index: number): boolean {
  return !!removable && removable.includes(index);
}

/** Size of a junction icon (px) and its distance from the crossing (COM `SegHandlePx`, the layout's junction priority 60). */
export const JUNCTION_PX = 5, JUNCTION_OFFSET_PX = 12;
/** Distance of the "+" at a free line end without a port beyond the end (px, COM "end:" handle). */
export const END_OFFSET_PX = 18;

/**
 * The ⚠ badge of an element (COM `ValueBadge`): "conflict" when its entered values conflict, "warning" when a plausibility
 * check that is not muted warns about it (the engine leaves muted ones out), else "".
 */
export function badgeKind(status: string | undefined, checks: readonly Pick<CheckInfo, "objectId" | "severity">[], id: string): "conflict" | "warning" | "" {
  if (status === "conflict") return "conflict";
  return checks.some((c) => c.objectId === id && c.severity === "warning") ? "warning" : "";
}

/** The card line of a check (COM `CheckLines`): "⚠ message (rule) [source]" for warnings, "ⓘ …" for hints. */
export function checkLine(c: Pick<CheckInfo, "severity" | "message" | "rule" | "source">): string {
  return (c.severity === "warning" ? "⚠ " : "ⓘ ") + c.message + (c.rule?.trim() ? ` (${c.rule})` : "") + (c.source?.trim() ? ` [${c.source}]` : "");
}

/**
 * The preferred display units offered under "Show values in" (COM `ValueDocSettings.Offered`): quantity kind and unit
 * symbols as the engine knows them (speed of rotation: min⁻¹, rev/s, rad/s).
 */
export const UNIT_CHOICES: readonly { kind: string; units: readonly string[] }[] = [
  { kind: "pressure", units: ["bar", "MPa", "kPa"] },
  { kind: "rotationalSpeed", units: ["min⁻¹", "rev/s", "rad/s"] },
  { kind: "energy", units: ["kWh", "MJ", "J"] },
  { kind: "power", units: ["W", "kW"] },
];

/** Shift while drawing a line on: the direction from `from` to `p` in 15° steps, the length kept (COM `AngleStep`). */
export function angleStep(from: Vec2, p: Vec2): Vec2 {
  const step = Math.PI / 12;
  const a = Math.round(Math.atan2(p.y - from.y, p.x - from.x) / step) * step;
  const len = Math.hypot(p.x - from.x, p.y - from.y);
  return { x: from.x + Math.cos(a) * len, y: from.y + Math.sin(a) * len };
}

/**
 * The free line ends that get their own "+" (COM `_endHandles` without a port "+" there): those of `ends` with no
 * `LineEnd` build port within half a millimetre (a connection line's port "+" draws on itself).
 */
export function endsWithoutPort<T extends Pick<LineEndInfo, "x" | "y">>(ends: readonly T[], ports: readonly Pick<BuildPortInfo, "kind" | "x" | "y">[]): T[] {
  return ends.filter((e) => !ports.some((p) => p.kind === "LineEnd" && Math.hypot(p.x - e.x, p.y - e.y) < 0.5));
}

/** The point index a `LineEnd` port "+" draws the line on from: its start ("line.start") or its last point. */
export function portEndIndex(port: Pick<BuildPortInfo, "id" | "kind">, pointCount: number): number | undefined {
  if (port.kind !== "LineEnd" || pointCount < 2) return undefined;
  return port.id === "line.start" ? 0 : pointCount - 1;
}

/**
 * The bar of a selected corner (COM `CornerButtons`): split (or open a closed outline) where the engine says it can,
 * remove where it can; empty when neither applies.
 */
export function cornerCommands(info: Pick<PolylineHandles, "closed" | "splittable" | "removable"> | undefined, index: number): ("split" | "open" | "remove")[] {
  if (!info) return [];
  const out: ("split" | "open" | "remove")[] = [];
  if (info.splittable.includes(index)) out.push(info.closed ? "open" : "split");
  if (info.removable.includes(index)) out.push("remove");
  return out;
}