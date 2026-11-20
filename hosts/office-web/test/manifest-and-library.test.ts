import { describe, expect, it } from "vitest";
import { filterItems } from "../src/ui/library";
import { manifest } from "../scripts/manifest.mjs";

describe("manifest", () => {
  it("declares the COM add-in as equivalent so Office hides the web add-in when COM is installed", () => {
    expect(manifest).toContain("<ProgId>InventaSketch.AddIn.Connect</ProgId>");
    expect(manifest).toContain("<Type>COM</Type>");
    expect(manifest.indexOf("<EquivalentAddins>")).toBeGreaterThan(manifest.indexOf("</Resources>"));
  });

  it("targets PowerPoint with a task pane", () => {
    expect(manifest).toContain('<Host Name="Presentation" />');
    expect(manifest).toContain("taskpane.html");
  });
});

describe("library filter", () => {
  const catalog = {
    categories: [],
    items: [
      { id: "mech.gear.spur", typeId: "mech.gear.spur", name: "Stirnrad", category: "gears", keywords: ["gear", "zahnrad"], thumbnail: "" },
      { id: "ann.arrow", typeId: "ann.arrow", name: "Pfeil", category: "lines", keywords: ["arrow"], thumbnail: "" },
    ],
  } as never;

  it("matches German and English keywords", () => {
    expect(filterItems(catalog, "zahnrad", "*", []).map((i) => i.id)).toEqual(["mech.gear.spur"]);
    expect(filterItems(catalog, "arrow", "*", []).map((i) => i.id)).toEqual(["ann.arrow"]);
  });

  it("filters favourites and categories", () => {
    expect(filterItems(catalog, "", "fav", ["ann.arrow"]).map((i) => i.id)).toEqual(["ann.arrow"]);
    expect(filterItems(catalog, "", "gears", []).length).toBe(1);
  });
});
