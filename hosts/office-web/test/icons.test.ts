import { describe, expect, it } from "vitest";
import { hasIcon } from "../src/ui/icons";
import core from "../../../src/InventaSketch.Core/Modules.cs?raw";

// The engine sends abstract icon ids (InventaSketch.Core.IconIds); the task pane must draw every one of them.
describe("icons", () => {
  const ids = [...core.matchAll(/public const string \w+ = "((?:view|cmd)-[a-z-]+)"/g)].map((m) => m[1]);

  it("finds the IconIds vocabulary", () => {
    expect(ids.length).toBeGreaterThan(20);
  });

  it.each(ids)("has a pictogram for %s", (id) => {
    expect(hasIcon(id)).toBe(true);
  });
});
