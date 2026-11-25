// Collision-free layout of the handles around a selected element (docs/en/design/interaction-ux.md section 4, a port of
// the COM add-in's pure HandleLayout): fixed handles (the frame's resize and rotate handles, point diamonds, quick-add
// triangles, value grips) stay where they are; every other handle takes the first free place on a search around its
// preferred place – further out along its direction, leaning up to 40° (orbiting handles also go round their anchor,
// "along" handles slide along their direction) – keeping a gap of at least `GAP` px to every handle placed before.
// Optional handles without a free place are hidden. Screen pixels; no DOM (unit-tested).

export interface Pt { x: number; y: number }

export type HandleKind = "frame" | "point" | "triangle" | "add" | "lock" | "junction" | "corner" | "branch" | "grip";

/** One handle to place: where it belongs, the way it moves out of the way, its preferred distance and radius (px). */
export interface LayoutSpec {
  id: string;
  kind: HandleKind;
  anchor: Pt;
  dir: Pt;
  offset: number;
  radius: number;
  priority: number;
  /** Stays at its anchor (frame handles, point diamonds, triangles, grips). */
  fixed?: boolean;
  /** May be hidden when there is no free place (corner dots, branch handles). */
  optional?: boolean;
  /** How far beyond `offset` the handle may be pushed (px); 36 by default. */
  maxPush?: number;
  /** May turn all around its anchor; otherwise it only leans by up to 40°. */
  orbit?: boolean;
  /** Moves only along its direction, both ways. */
  along?: boolean;
}

export interface PlacedSpec { spec: LayoutSpec; at: Pt; visible: boolean }

/** Minimum free space between two handles (px). */
export const GAP = 3;

/** Priorities of the movable handles (COM: "+" 90, table "+" 85, lock 70, junction 60, corner dot 40, branch 30). */
export const PRIORITY = { add: 90, tableAdd: 85, lock: 70, junction: 60, corner: 40, branch: 30 } as const;

const LEAN = [0, 20, -20, 40, -40];
const ORBIT = [0, 30, -30, 60, -60, 90, -90, 135, -135, 180];

/** Free room around a candidate (negative: it overlaps a handle placed before, gap included). */
export function room(c: Pt, radius: number, taken: { at: Pt; r: number }[]): number {
  let best = Number.MAX_VALUE;
  for (const t of taken) best = Math.min(best, Math.hypot(c.x - t.at.x, c.y - t.at.y) - t.r - radius - GAP);
  return best;
}

/** Candidate places in search order: the preferred place, then further out, leaning, orbiting or sliding. */
export function candidates(s: LayoutSpec): Pt[] {
  const len = Math.hypot(s.dir.x, s.dir.y);
  const dir = len < 1e-6 ? { x: 0, y: -1 } : { x: s.dir.x / len, y: s.dir.y / len };
  const step = Math.max(4, s.radius);
  const maxPush = s.maxPush ?? 36;
  const rings = maxPush <= 0 ? 0 : Math.ceil(maxPush / step);
  const list: { cost: number; at: Pt }[] = [];
  if (s.along) {
    for (let ring = 0; ring <= rings; ring++) {
      const d = s.offset + ring * step;
      list.push({ cost: ring, at: { x: s.anchor.x + dir.x * d, y: s.anchor.y + dir.y * d } });
      if (ring > 0) list.push({ cost: ring + 0.1, at: { x: s.anchor.x - dir.x * d, y: s.anchor.y - dir.y * d } });
    }
  } else {
    const angles = s.orbit ? ORBIT : LEAN;
    for (let ring = 0; ring <= rings; ring++) {
      const d = s.offset + ring * step;
      for (let k = 0; k < angles.length; k++) {
        // Without any distance a turn changes nothing: the anchor itself once.
        if (d < 1e-3 && k > 0) break;
        const a = (angles[k] * Math.PI) / 180;
        const c = Math.cos(a), si = Math.sin(a);
        const v = { x: dir.x * c - dir.y * si, y: dir.x * si + dir.y * c };
        list.push({ cost: ring + k * 0.55, at: { x: s.anchor.x + v.x * d, y: s.anchor.y + v.y * d } });
      }
    }
  }
  return list.sort((p, q) => p.cost - q.cost).map((x) => x.at);
}

/** Places the handles; the result keeps the order of `specs`. */
export function resolveHandles(specs: LayoutSpec[]): PlacedSpec[] {
  const placed: PlacedSpec[] = new Array(specs.length);
  const taken: { at: Pt; r: number }[] = [];
  const order = specs.map((_, i) => i).sort((a, b) => {
    const fa = specs[a].fixed ? 0 : 1, fb = specs[b].fixed ? 0 : 1;
    if (fa !== fb) return fa - fb;
    const pa = specs[a].fixed ? 0 : specs[a].priority, pb = specs[b].fixed ? 0 : specs[b].priority;
    return pb - pa || a - b;
  });
  for (const i of order) {
    const s = specs[i];
    if (s.fixed) {
      placed[i] = { spec: s, at: s.anchor, visible: true };
      taken.push({ at: s.anchor, r: s.radius });
      continue;
    }
    let best = s.anchor, bestRoom = -Number.MAX_VALUE, found = false;
    for (const c of candidates(s)) {
      const r = room(c, s.radius, taken);
      if (r >= 0) { best = c; found = true; break; }
      if (r > bestRoom) { bestRoom = r; best = c; }
    }
    const visible = found || !s.optional;
    placed[i] = { spec: s, at: best, visible };
    if (visible) taken.push({ at: best, r: s.radius });
  }
  return placed;
}

/** True if two visible handles overlap (two fixed ones excepted): the layout's promise, for tests. */
export function anyOverlap(handles: PlacedSpec[]): boolean {
  const shown = handles.filter((h) => h.visible);
  for (let i = 0; i < shown.length; i++)
    for (let j = i + 1; j < shown.length; j++) {
      if (shown[i].spec.fixed && shown[j].spec.fixed) continue;
      if (Math.hypot(shown[i].at.x - shown[j].at.x, shown[i].at.y - shown[j].at.y) < shown[i].spec.radius + shown[j].spec.radius - 0.01) return true;
    }
  return false;
}
