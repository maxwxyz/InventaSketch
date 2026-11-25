import { describe, expect, it } from "vitest";
import { PRIORITY, anyOverlap, candidates, resolveHandles, room, type LayoutSpec } from "../src/ui/handlelayout";
import { gripShown } from "../src/ui/partlogic";

const fixed = (id: string, x: number, y: number, radius = 5.5): LayoutSpec => ({ id, kind: "frame", anchor: { x, y }, dir: { x: 0, y: 0 }, offset: 0, radius, priority: 0, fixed: true });
const plus = (id: string, x: number, y: number, dx: number, dy: number, extra: Partial<LayoutSpec> = {}): LayoutSpec =>
  ({ id, kind: "add", anchor: { x, y }, dir: { x: dx, y: dy }, offset: 15, radius: 6.5, priority: PRIORITY.add, ...extra });

describe("handle layout (port of the COM HandleLayout)", () => {
  it("keeps fixed handles where they are and puts a free '+' at its preferred place", () => {
    const r = resolveHandles([fixed("tl", 0, 0), plus("p", 100, 50, 1, 0)]);
    expect(r[0].at).toEqual({ x: 0, y: 0 });
    expect(r[1].at.x).toBeCloseTo(115);
    expect(r[1].at.y).toBeCloseTo(50);
    expect(r.every((h) => h.visible)).toBe(true);
  });

  it("moves a '+' off the frame handles of a short element (the short pipe of the review)", () => {
    // A 20 px wide frame: its right handles sit where the end's "+" would go.
    const frame = [fixed("tr", 120, 40), fixed("r", 120, 50), fixed("br", 120, 60), fixed("rot", 110, 14)];
    const specs = [...frame, plus("end", 105, 50, 1, 0), plus("start", 100, 50, 1, 0)];
    const r = resolveHandles(specs);
    expect(anyOverlap(r)).toBe(false);
    // Both "+" are shown, each clear of every other handle by the gap.
    expect(r.filter((h) => h.spec.kind === "add").every((h) => h.visible)).toBe(true);
    const placed = r.filter((h) => h.visible).map((h) => ({ at: h.at, r: h.spec.radius }));
    const end = r[4];
    expect(room(end.at, end.spec.radius, placed.filter((p) => p.at !== end.at))).toBeGreaterThanOrEqual(0);
  });

  it("places higher priorities first and hides optional handles without room", () => {
    const crowd = [fixed("a", 0, 0, 30)];
    const r = resolveHandles([...crowd, plus("dot", 0, 0, 1, 0, { offset: 0, radius: 4, priority: PRIORITY.corner, optional: true, maxPush: 0 })]);
    expect(r[1].visible).toBe(false);
    const order = resolveHandles([fixed("f", 0, 0), plus("lock", 20, 0, 1, 0, { priority: PRIORITY.lock }), plus("add", 20, 0, 1, 0)]);
    // The "+" (priority 90) takes the preferred place, the lock moves on.
    expect(order[2].at.x).toBeCloseTo(35);
    expect(order[1].at.x).not.toBeCloseTo(35);
  });

  it("searches outward and leans to the sides; orbit and along variants", () => {
    const c = candidates(plus("p", 0, 0, 1, 0, { maxPush: 8 }));
    expect(c[0]).toEqual({ x: 15, y: 0 });
    expect(c.some((p) => p.y !== 0)).toBe(true);
    const along = candidates(plus("p", 0, 0, 0, 1, { along: true, offset: 0, maxPush: 8 }));
    expect(along.every((p) => Math.abs(p.x) < 1e-9)).toBe(true);
    expect(along.some((p) => p.y < 0)).toBe(true);
    const orbit = candidates(plus("p", 0, 0, 1, 0, { orbit: true, maxPush: 0 }));
    expect(orbit.some((p) => p.x < 0)).toBe(true);
  });
});

describe("grips that come with a part", () => {
  const grips = [
    { key: "limit", part: "limit.0", showWith: null },
    { key: "p0", part: "point.0.0", showWith: "series.0" },
    { key: "p1", part: "point.0.1", showWith: "series.0" },
    { key: "q0", part: "point.1.0", showWith: "series.1" },
  ];
  it("shows plain grips always and point grips with their curve or one of its points", () => {
    expect(grips.filter((g) => gripShown(g, undefined, grips)).map((g) => g.key)).toEqual(["limit"]);
    expect(grips.filter((g) => gripShown(g, "series.0", grips)).map((g) => g.key)).toEqual(["limit", "p0", "p1"]);
    expect(grips.filter((g) => gripShown(g, "point.0.1", grips)).map((g) => g.key)).toEqual(["limit", "p0", "p1"]);
    expect(grips.filter((g) => gripShown(g, "series.1", grips)).map((g) => g.key)).toEqual(["limit", "q0"]);
  });
});
