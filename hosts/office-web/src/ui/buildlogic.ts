// Pure rules of the remaining COM features on the web stage: where a build "+" sits, the stretch target of a shaft
// end, the throttle of the live coupling preview and when the bar offers ghost positions. No DOM access.

import type { BuildPortInfo, TraceInfo, Vec2 } from "../engine/types";
import { canTrace } from "./uxlogic";

/** Distance of a "+" from its port, its radius, the clearance from the frame handles and the push-out step (px, like COM). */
export const PORT_OFFSET_PX = 15, PORT_RADIUS_PX = 6.5, PORT_CLEAR_PX = 15, PORT_STEP_PX = 6;
/** A free hub's "+" sits beyond the top right corner of the element, so it can still be dragged by its centre. */
export const HUB_CORNER_PX = 16;

/** Unit vector of (dx, dy); a port without direction points up and to the right. */
export function unit(dx: number, dy: number): Vec2 {
  const l = Math.hypot(dx, dy);
  return l < 1e-9 ? { x: Math.SQRT1_2, y: -Math.SQRT1_2 } : { x: dx / l, y: dy / l };
}

/**
 * Page position of a port's "+": `PORT_OFFSET_PX` beyond the port along its outward direction, pushed further out (in
 * `PORT_STEP_PX` steps, at most 12) until it is clear of every obstacle (frame handles, rotate handle). A free hub's
 * "+" goes beyond the top right corner `topRight` of the selection frame (same as the COM add-in).
 */
export function portPlusPosition(port: BuildPortInfo, pxPerMm: number, obstacles: readonly Vec2[], topRight?: Vec2): Vec2 {
  const px = 1 / Math.max(1e-6, pxPerMm);
  if (port.kind === "Hub" && topRight) return { x: topRight.x + HUB_CORNER_PX * px, y: topRight.y - HUB_CORNER_PX * px };
  const d = unit(port.dx, port.dy);
  const clear = PORT_CLEAR_PX * px;
  let off = PORT_OFFSET_PX * px;
  let c = { x: port.x + d.x * off, y: port.y + d.y * off };
  for (let i = 0; i < 12; i++) {
    c = { x: port.x + d.x * off, y: port.y + d.y * off };
    if (obstacles.every((h) => Math.abs(h.x - c.x) > clear || Math.abs(h.y - c.y) > clear)) return c;
    off += PORT_STEP_PX * px;
  }
  return c;
}

/**
 * New end of a stretched shaft: the pointer (snapped or not) projected onto the shaft axis through the port along its
 * direction, so the shaft only gets longer or shorter. `delta` is the change in length (mm, positive = longer).
 */
export function stretchTarget(port: BuildPortInfo, p: Vec2): { x: number; y: number; delta: number } {
  const d = unit(port.dx, port.dy);
  const delta = (p.x - port.x) * d.x + (p.y - port.y) * d.y;
  return { x: port.x + d.x * delta, y: port.y + d.y * delta, delta };
}

/** Tooltip key of a "+" (strings.json "port.*"): what docks there and, for a shaft end, that dragging lengthens it. */
export function portTipKey(port: BuildPortInfo): string {
  if (port.kind === "AxisEnd") return port.stretch ? "port.AxisEnd.stretch" : "port.AxisEnd";
  return ["Mesh", "InnerMesh", "Hub", "Pin", "Bevel", "Joint", "Mount", "Connect", "LineEnd"].includes(port.kind) ? `port.${port.kind}` : "port.other";
}

/**
 * "Ghost positions" in the bar: a link (points object) of a self-built linkage that moves with one input (F = 1), the
 * same condition as "draw the path" (`trace` only returns a path for F = 1).
 */
export function canDrawGhosts(kind: "box" | "points" | undefined, dof: number | null | undefined, trace: TraceInfo | undefined): boolean {
  return kind === "points" && (dof === 1 || canTrace(trace));
}

/** Clock of a throttle (injectable for tests). */
export interface ThrottleClock {
  now(): number;
  set(fn: () => void, ms: number): unknown;
  clear(handle: unknown): void;
}

const realClock: ThrottleClock = {
  now: () => (typeof performance !== "undefined" ? performance.now() : Date.now()),
  set: (fn, ms) => setTimeout(fn, ms),
  clear: (h) => clearTimeout(h as ReturnType<typeof setTimeout>),
};

/**
 * Runs `fn` at most once per `intervalMs`: the first call runs at once, later calls inside the interval replace each
 * other and the last one runs when the interval ends (trailing call), so the final drag position is always previewed.
 * `cancel` drops a pending call (release, Esc).
 */
export class Throttle<A extends unknown[]> {
  private last = -Infinity;
  private pending?: A;
  private timer?: unknown;

  constructor(private readonly fn: (...args: A) => void, private readonly intervalMs: number, private readonly clock: ThrottleClock = realClock) {}

  push(...args: A): void {
    const now = this.clock.now();
    const wait = this.last + this.intervalMs - now;
    if (wait <= 0 && this.timer === undefined) {
      this.last = now;
      this.fn(...args);
      return;
    }
    this.pending = args;
    if (this.timer === undefined) this.timer = this.clock.set(() => this.fire(), Math.max(0, wait));
  }

  private fire(): void {
    this.timer = undefined;
    const args = this.pending;
    this.pending = undefined;
    if (!args) return;
    this.last = this.clock.now();
    this.fn(...args);
  }

  /** A call is waiting for the end of the interval. */
  get waiting(): boolean {
    return this.timer !== undefined;
  }

  cancel(): void {
    if (this.timer !== undefined) this.clock.clear(this.timer);
    this.timer = undefined;
    this.pending = undefined;
  }
}

/** Coupling preview rate: about 15 updates per second. */
export const COUPLING_PREVIEW_MS = 66;
