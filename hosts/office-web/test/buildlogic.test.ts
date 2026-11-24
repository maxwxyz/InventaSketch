import { describe, expect, it } from "vitest";
import type { BuildPortInfo, TraceInfo } from "../src/engine/types";
import {
  COUPLING_PREVIEW_MS, HUB_CORNER_PX, PORT_OFFSET_PX, PORT_STEP_PX, Throttle, canDrawGhosts, portPlusPosition, portTipKey, stretchTarget, unit,
  type ThrottleClock,
} from "../src/ui/buildlogic";
import { hasIcon } from "../src/ui/icons";
import strings from "../src/strings.json";

const port = (over: Partial<BuildPortInfo> = {}): BuildPortInfo => ({ id: "p", kind: "Mesh", x: 100, y: 50, dx: 1, dy: 0, stretch: false, ...over });

describe("build \"+\" placement", () => {
  it("sits 15 px beyond the port along its outward direction", () => {
    // 2 px per mm: 15 px = 7.5 mm.
    expect(portPlusPosition(port(), 2, [])).toEqual({ x: 107.5, y: 50 });
    const c = portPlusPosition(port({ dx: 0, dy: -3 }), 3, []);
    expect(c.x).toBeCloseTo(100);
    expect(c.y).toBeCloseTo(50 - PORT_OFFSET_PX / 3);
  });

  it("normalises the direction", () => {
    const c = portPlusPosition(port({ dx: 3, dy: 4 }), 1, []);
    expect(c.x).toBeCloseTo(100 + 0.6 * PORT_OFFSET_PX);
    expect(c.y).toBeCloseTo(50 + 0.8 * PORT_OFFSET_PX);
    expect(unit(0, 0).x).toBeCloseTo(Math.SQRT1_2);
    expect(unit(0, 0).y).toBeCloseTo(-Math.SQRT1_2);
  });

  it("is pushed out until it is clear of a frame handle", () => {
    // A handle right where the "+" would be: one step further is still within 15 px, so it takes three steps.
    const c = portPlusPosition(port(), 1, [{ x: 115, y: 50 }]);
    expect(c.x).toBeCloseTo(100 + PORT_OFFSET_PX + 3 * PORT_STEP_PX);
    expect(c.y).toBe(50);
    // A handle far enough away to one side does not move it.
    expect(portPlusPosition(port(), 1, [{ x: 115, y: 70 }])).toEqual({ x: 115, y: 50 });
  });

  it("puts a free hub's \"+\" beyond the top right corner", () => {
    const c = portPlusPosition(port({ kind: "Hub", dx: 0, dy: 0 }), 2, [], { x: 120, y: 30 });
    expect(c).toEqual({ x: 120 + HUB_CORNER_PX / 2, y: 30 - HUB_CORNER_PX / 2 });
  });
});

describe("stretching a shaft end", () => {
  it("projects the pointer onto the shaft axis", () => {
    const p = port({ kind: "AxisEnd", stretch: true });
    expect(stretchTarget(p, { x: 130, y: 70 })).toEqual({ x: 130, y: 50, delta: 30 });
    expect(stretchTarget(p, { x: 90, y: 40 })).toEqual({ x: 90, y: 50, delta: -10 });
    const up = stretchTarget(port({ x: 0, y: 0, dx: 0, dy: -2 }), { x: 5, y: -12 });
    expect(up.x).toBeCloseTo(0);
    expect(up.y).toBeCloseTo(-12);
    expect(up.delta).toBeCloseTo(12);
  });
});

describe("port tooltips", () => {
  it("name what docks there, bilingual", () => {
    expect(portTipKey(port())).toBe("port.Mesh");
    expect(portTipKey(port({ kind: "AxisEnd", stretch: true }))).toBe("port.AxisEnd.stretch");
    expect(portTipKey(port({ kind: "AxisEnd" }))).toBe("port.AxisEnd");
    expect(portTipKey(port({ kind: "Future" }))).toBe("port.other");
    const table = strings as Record<string, { en: string; de: string }>;
    for (const kind of ["Mesh", "InnerMesh", "AxisEnd", "Hub", "Pin", "Bevel", "Joint", "Future"])
      for (const stretch of [false, true]) {
        const entry = table[portTipKey(port({ kind, stretch }))];
        expect(entry?.en).toBeTruthy();
        expect(entry?.de).toBeTruthy();
      }
    for (const key of ["ctx.ghosts", "ins.port", "build.stretch"]) {
      expect(table[key]?.en).toBeTruthy();
      expect(table[key]?.de).toBeTruthy();
    }
  });
});

describe("ghost positions in the bar", () => {
  const path: TraceInfo = { points: [{ x: 0, y: 0 }, { x: 1, y: 0 }, { x: 1, y: 1 }], closed: true };

  it("shows for a link of a chain with F = 1", () => {
    expect(canDrawGhosts("points", 1, undefined)).toBe(true);
    expect(canDrawGhosts("points", null, path)).toBe(true);
  });

  it("does not show for other chains or elements", () => {
    expect(canDrawGhosts("points", 2, { points: [], closed: false })).toBe(false);
    expect(canDrawGhosts("points", 0, undefined)).toBe(false);
    expect(canDrawGhosts("box", 1, path)).toBe(false);
    expect(canDrawGhosts(undefined, 1, path)).toBe(false);
  });

  it("uses existing icons (ghosts, dimension)", () => {
    expect(hasIcon("cmd-animate")).toBe(true);
    expect(hasIcon("cmd-dimension")).toBe(true);
  });
});

describe("coupling preview throttle", () => {
  function fakeClock() {
    let now = 0;
    const timers: { at: number; fn: () => void; id: number }[] = [];
    let next = 1;
    const clock: ThrottleClock = {
      now: () => now,
      set: (fn, ms) => { const id = next++; timers.push({ at: now + ms, fn, id }); return id; },
      clear: (h) => { const i = timers.findIndex((x) => x.id === h); if (i >= 0) timers.splice(i, 1); },
    };
    const advance = (ms: number) => {
      now += ms;
      for (;;) {
        const due = timers.filter((x) => x.at <= now).sort((a, b) => a.at - b.at)[0];
        if (!due) break;
        timers.splice(timers.indexOf(due), 1);
        due.fn();
      }
    };
    return { clock, advance, pending: () => timers.length };
  }

  it("runs about 15 times a second", () => {
    expect(Math.round(1000 / COUPLING_PREVIEW_MS)).toBe(15);
  });

  it("runs the first call at once and the last of a burst at the end of the interval", () => {
    const { clock, advance } = fakeClock();
    const calls: number[] = [];
    const th = new Throttle((v: number) => calls.push(v), 66, clock);
    th.push(1);
    expect(calls).toEqual([1]);
    advance(10); th.push(2);
    advance(10); th.push(3);
    expect(calls).toEqual([1]);
    expect(th.waiting).toBe(true);
    advance(46);
    expect(calls).toEqual([1, 3]);
    // A drag of one second at 120 pointer events per second gives about 15 previews.
    calls.length = 0;
    advance(100);
    for (let i = 0; i < 120; i++) { th.push(i); advance(1000 / 120); }
    advance(100);
    expect(calls.length).toBeGreaterThanOrEqual(14);
    expect(calls.length).toBeLessThanOrEqual(17);
    expect(calls[calls.length - 1]).toBe(119);
  });

  it("drops the waiting call on cancel (release, Esc)", () => {
    const { clock, advance, pending } = fakeClock();
    const calls: number[] = [];
    const th = new Throttle((v: number) => calls.push(v), 66, clock);
    th.push(1);
    th.push(2);
    th.cancel();
    expect(pending()).toBe(0);
    advance(200);
    expect(calls).toEqual([1]);
    th.push(3);
    expect(calls).toEqual([1, 3]);
  });
});
