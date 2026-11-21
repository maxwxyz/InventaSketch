import { describe, expect, it } from "vitest";
import { hasIcon } from "../src/ui/icons";
import core from "../../../src/InventaSketch.Core/Modules.cs?raw";

// The engine sends abstract icon ids (InventaSketch.Core.IconIds); the task pane must draw every one of them.
describe("icons", () => {
  const ids = [...core.matchAll(/public const string \w+ = "((?:view|cmd|head|size|line|course|mach|hub|shape|fill|route|wall)-[a-z0-9-]+)"/g)].map((m) => m[1]);

  it("finds the IconIds vocabulary", () => {
    expect(ids.length).toBeGreaterThan(20);
  });

  it.each(ids)("has a pictogram for %s", (id) => {
    expect(hasIcon(id)).toBe(true);
  });
});
