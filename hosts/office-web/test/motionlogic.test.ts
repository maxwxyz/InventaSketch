import { describe, expect, it } from "vitest";
import { TURN_STEP_DEG, TurnTracker, canMove, insideBounds, nearestJoint, pointerAngle, stepTurn, turnLabel, wrapDelta } from "../src/ui/motionlogic";
import { hasIcon } from "../src/ui/icons";
import strings from "../src/strings.json";

const at = (deg: number, r = 10, c = { x: 50, y: 40 }) => ({ x: c.x + r * Math.cos((deg * Math.PI) / 180), y: c.y + r * Math.sin((deg * Math.PI) / 180) });

describe("motion toggle in the bar", () => {
  it("is offered for a link of a chain that can move", () => {
    expect(canMove({ kind: "linkage", dof: 1 })).toBe(true);
    expect(canMove({ kind: "linkage", dof: 2 })).toBe(true);
  });

  it("is offered for a gear only when it is coupled with something (same rule as COM)", () => {
    expect(canMove({ kind: "gear", x: 0, y: 0, coupled: true })).toBe(true);
    expect(canMove({ kind: "gear", x: 0, y: 0, coupled: false })).toBe(false);
    expect(canMove({ kind: "mechanism", x: 0, y: 0, coupled: true })).toBe(true);
  });

  it("is not offered for other parts or without engine answer", () => {
    expect(canMove({ kind: "none" })).toBe(false);
    expect(canMove(undefined)).toBe(false);
  });

  it("has a pictogram and a bilingual tooltip", () => {
    expect(hasIcon("cmd-motion")).toBe(true);
    const table = strings as Record<string, { en: string; de: string }>;
    for (const key of ["ctx.motion", "motion.on"]) {
      expect(table[key].en.length).toBeGreaterThan(3);
      expect(table[key].de.length).toBeGreaterThan(3);
      expect(table[key].de).not.toBe(table[key].en);
    }
  });
});

describe("angle from the pointer", () => {
  it("is measured clockwise on screen (y down) around the centre", () => {
    const c = { x: 50, y: 40 };
    expect(pointerAngle(c, { x: 60, y: 40 })).toBeCloseTo(0);
    expect(pointerAngle(c, { x: 50, y: 50 })).toBeCloseTo(90); // below the centre = a quarter turn clockwise
    expect(pointerAngle(c, { x: 50, y: 30 })).toBeCloseTo(-90);
    expect(pointerAngle(c, { x: 40, y: 40 })).toBeCloseTo(180);
  });

  it("is undefined right at the centre", () => {
    expect(pointerAngle({ x: 0, y: 0 }, { x: 0.1, y: 0.1 })).toBeUndefined();
    expect(pointerAngle({ x: 0, y: 0 }, { x: 0.1, y: 0.1 }, 0.05)).toBeCloseTo(45);
  });
});

describe("wrap-around of angles", () => {
  it("folds a difference into (-180, 180]", () => {
    expect(wrapDelta(10)).toBe(10);
    expect(wrapDelta(190)).toBe(-170);
    expect(wrapDelta(-190)).toBe(170);
    expect(wrapDelta(350)).toBe(-10);
    expect(wrapDelta(-350)).toBe(10);
    expect(wrapDelta(180)).toBe(180);
    expect(wrapDelta(-180)).toBe(180);
    expect(wrapDelta(720 + 5)).toBe(5);
  });

  it("keeps turning when the pointer crosses ±180°", () => {
    const c = { x: 50, y: 40 };
    const tr = new TurnTracker(c, at(170, 10, c));
    expect(tr.update(at(179, 10, c))).toBeCloseTo(9);
    expect(tr.update(at(-170, 10, c))).toBeCloseTo(20); // = 190°, not -340°
    expect(tr.update(at(-150, 10, c))).toBeCloseTo(40);
  });

  it("adds up full circles and turning back", () => {
    const c = { x: 0, y: 0 };
    const tr = new TurnTracker(c, at(0, 5, c));
    for (let a = 30; a <= 720; a += 30) tr.update(at(a, 5, c));
    expect(tr.total).toBeCloseTo(720);
    for (let a = 690; a >= 600; a -= 30) tr.update(at(a, 5, c));
    expect(tr.total).toBeCloseTo(600);
  });

  it("ignores pointer positions at the centre", () => {
    const c = { x: 0, y: 0 };
    const tr = new TurnTracker(c, at(0, 5, c));
    tr.update(at(45, 5, c));
    expect(tr.update({ x: 0.1, y: 0 })).toBeCloseTo(45);
    expect(tr.update(at(60, 5, c))).toBeCloseTo(60);
  });

  it("starts at the first usable position when pressed at the centre", () => {
    const c = { x: 0, y: 0 };
    const tr = new TurnTracker(c, c);
    expect(tr.update(at(80, 5, c))).toBe(0);
    expect(tr.update(at(100, 5, c))).toBeCloseTo(20);
  });
});

describe("Shift steps and the angle label", () => {
  it("rounds to 15° with Shift only", () => {
    expect(TURN_STEP_DEG).toBe(15);
    expect(stepTurn(22, true)).toBe(15);
    expect(stepTurn(23, true)).toBe(30);
    expect(stepTurn(-7, true)).toBe(0);
    expect(Object.is(stepTurn(-7, true), -0)).toBe(false);
    expect(stepTurn(22.4, false)).toBe(22.4);
  });

  it("shows a signed angle with one decimal", () => {
    expect(turnLabel(30)).toBe("+30°");
    expect(turnLabel(-12.46)).toBe("-12.5°");
    expect(turnLabel(-0.01)).toBe("0°");
  });
});

describe("nearest joint of a link", () => {
  const pts = [{ x: 0, y: 0 }, { x: 30, y: 0 }, { x: 30, y: 20 }];

  it("picks the point closest to the pointer", () => {
    expect(nearestJoint(pts, { x: 2, y: 1 })).toBe(0);
    expect(nearestJoint(pts, { x: 20, y: 0 })).toBe(1);
    expect(nearestJoint(pts, { x: 29, y: 15 })).toBe(2);
  });

  it("takes the first of equally near points and -1 without points", () => {
    expect(nearestJoint(pts, { x: 15, y: 0 })).toBe(0);
    expect(nearestJoint([], { x: 1, y: 1 })).toBe(-1);
  });
});

describe("where a gear turn starts", () => {
  it("is on the gear's frame plus a margin", () => {
    const b = { x: 10, y: 10, w: 20, h: 20 };
    expect(insideBounds(b, { x: 20, y: 20 }, 0)).toBe(true);
    expect(insideBounds(b, { x: 32, y: 20 }, 0)).toBe(false);
    expect(insideBounds(b, { x: 32, y: 20 }, 3)).toBe(true);
    expect(insideBounds(b, { x: 20, y: 6 }, 3)).toBe(false);
  });
});
