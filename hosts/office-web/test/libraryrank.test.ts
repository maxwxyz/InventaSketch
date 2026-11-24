import { describe, expect, it } from "vitest";
import { rankedItems } from "../src/ui/library";
import type { Catalog } from "../src/engine/types";

const catalog = {
  lang: "de",
  categories: [],
  items: [
    { id: "a", name: "Stirnrad", category: "gears", typeId: "g", keywords: [], thumbnail: "" },
    { id: "b", name: "Stößel", category: "kin", typeId: "k", keywords: [], thumbnail: "" },
    { id: "c", name: "Kegelrad", category: "gears", typeId: "g", keywords: [], thumbnail: "" },
  ],
} as unknown as Catalog;

describe("library ranking", () => {
  it("keeps the engine order and the category", () => {
    expect(rankedItems(catalog, ["c", "a", "b"], "*", []).map((i) => i.id)).toEqual(["c", "a", "b"]);
    expect(rankedItems(catalog, ["c", "a", "b"], "gears", []).map((i) => i.id)).toEqual(["c", "a"]);
    expect(rankedItems(catalog, ["c", "a", "b"], "fav", ["b"]).map((i) => i.id)).toEqual(["b"]);
    expect(rankedItems(catalog, ["x", "a"], "*", []).map((i) => i.id)).toEqual(["a"]);
  });
});
