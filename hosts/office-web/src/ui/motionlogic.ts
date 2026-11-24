// Pure rules of motion mode on the web stage (like the COM add-in): when the bar offers the toggle, the turning angle of
// a gear from the pointer around its centre (unwrapped across ±180°, Shift: 15° steps) and the joint of a link that a
// drag pulls. No DOM access.

import type { MotionInfo, Vec2 } from "../engine/types";

/** Shift turns in steps of this many degrees (UX rule 7). */
export const TURN_STEP_DEG = 15;
/** Closer than this to the centre (mm) the pointer angle is undefined and the turn keeps its value. */
export const TURN_MIN_RADIUS_MM = 0.5;

/**
 * The bar offers motion mode for a link of a self-built chain that can move (engine `motionInfo` "linkage") and for a gear
 * that is coupled with something; a lone gear coupled with nothing only spins in place, which shows nothing (same rule
 * as the COM add-in).
 */
export function canMove(info: MotionInfo | undefined): boolean {
  if (!info) return false;
  if (info.kind === "linkage") return true;
  return (info.kind === "gear" || info.kind === "mechanism") && info.coupled === true;
}

/** Angle of `p` around `centre` in degrees, clockwise on screen (y down), in (-180, 180]; undefined at the centre. */
export function pointerAngle(centre: Vec2, p: Vec2, minRadius = TURN_MIN_RADIUS_MM): number | undefined {
  const dx = p.x - centre.x, dy = p.y - centre.y;
  if (Math.hypot(dx, dy) < minRadius) return undefined;
  return (Math.atan2(dy, dx) * 180) / Math.PI;
}

/** A difference of angles folded into (-180, 180] (the short way round). */
export function wrapDelta(deg: number): number {
  let d = deg % 360;
  if (d > 180) d -= 360;
  if (d <= -180) d += 360;
  return d;
}

/**
 * Accumulates the turn of a gear while the pointer circles its centre: each step adds the short-way difference to the
 * last pointer angle, so a pointer that crosses ±180° or circles several times keeps turning instead of jumping back.
 */
export class TurnTracker {
  private last?: number;
  private sum = 0;

  constructor(readonly centre: Vec2, start: Vec2) {
    this.last = pointerAngle(centre, start);
  }

  /** Feeds a pointer position; returns the total turn so far (degrees, clockwise positive). */
  update(p: Vec2): number {
    const a = pointerAngle(this.centre, p);
    if (a === undefined) return this.sum;
    if (this.last !== undefined) this.sum += wrapDelta(a - this.last);
    this.last = a;
    return this.sum;
  }

  get total(): number {
    return this.sum;
  }
}

/** The applied turn: Shift rounds to 15° steps. */
export function stepTurn(deg: number, shift: boolean): number {
  if (!shift) return deg;
  const r = Math.round(deg / TURN_STEP_DEG) * TURN_STEP_DEG;
  return Object.is(r, -0) ? 0 : r;
}

/** Text of the angle label at the pivot ("+30°", "-12.5°"). */
export function turnLabel(deg: number): string {
  const v = Math.round(deg * 10) / 10;
  return `${v > 0 ? "+" : ""}${Object.is(v, -0) ? 0 : v}°`;
}

/** Index of the joint (point) of a link nearest to `p`; -1 for a link without points. */
export function nearestJoint(points: readonly Vec2[], p: Vec2): number {
  let best = -1, bestD = Infinity;
  points.forEach((q, i) => {
    const d = Math.hypot(q.x - p.x, q.y - p.y);
    if (d < bestD) { bestD = d; best = i; }
  });
  return best;
}

/** A gear turn starts on the selected gear's frame, enlarged by `marginMm` (the pitch circle's inside may be empty). */
export function insideBounds(b: { x: number; y: number; w: number; h: number }, p: Vec2, marginMm: number): boolean {
  return p.x >= b.x - marginMm && p.x <= b.x + b.w + marginMm && p.y >= b.y - marginMm && p.y <= b.y + b.h + marginMm;
}
