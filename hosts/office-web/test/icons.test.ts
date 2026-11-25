import { describe, expect, it } from "vitest";
import { barLabel, hasIcon, icon, iconPath, needsLabel } from "../src/ui/icons";
import core from "../../../src/InventaSketch.Core/Modules.cs?raw";
import painter from "../../powerpoint-com/InventaSketch.AddIn/OnSlide/IconPainter.cs?raw";

// No Node type declarations in this project (see webspace.test.ts): Node modules are loaded dynamically.
interface NodeFs { mkdirSync(path: string, options?: { recursive: boolean }): void; writeFileSync(path: string, data: string | Uint8Array): void }
type Resvg = new (svg: string, options?: object) => { render(): { asPng(): Uint8Array } };
const load = (name: string) => import(/* @vite-ignore */ name);
const { mkdirSync, writeFileSync } = (await load("node:fs")) as NodeFs;

// The engine sends abstract icon ids (InventaSketch.Core.IconIds); the task pane must draw every one of them.
describe("icons", () => {
  const ids = [...core.matchAll(/public const string \w+ = "((?:view|cmd|head|size|line|course|mach|hub|shape|fill|route|wall|qty|text|curve)-[a-z0-9-]+)"/g)].map((m) => m[1]);

  it("finds the IconIds vocabulary", () => {
    expect(ids.length).toBeGreaterThan(150);
  });

  it.each(ids)("has a pictogram for %s", (id) => {
    expect(hasIcon(id)).toBe(true);
  });

  // The F3 pictograms are defined once and copied into both hosts: the COM add-in's SharedPaths must match exactly.
  it("draws the shared pictograms exactly like the COM add-in", () => {
    const shared = [...painter.matchAll(/\["([a-z0-9-]+)"\] = "([^"]+)",/g)].map((m) => [m[1], m[2]] as const);
    expect(shared.length).toBeGreaterThan(60);
    for (const [id, d] of shared) expect(iconPath(id), id).toBe(d);
  });

  it("shows label-only and step pictograms as text", () => {
    expect(needsLabel("cmd-toggle")).toBe(true);
    expect(needsLabel("cmd-count-more")).toBe(true);
    expect(needsLabel("cmd-name")).toBe(false);
    expect(barLabel("Next arrowhead style")).toBe("Arrowhead style");
    expect(barLabel("Thicker wall (240 mm)")).toBe("Thicker wall");
    expect(barLabel("Zwischen Kurven schraffieren lassen")).toHaveLength(22);
  });

  // Contact sheet for review (light and dark): written next to the test output, not compared.
  it("writes a contact sheet", async () => {
    const cell = 64;
    const cols = 10;
    const sheet = (bg: string, ink: string) => {
      const rows = Math.ceil(ids.length / cols);
      const cells = ids.map((id, i) => {
        const x = (i % cols) * cell, y = Math.floor(i / cols) * cell;
        const svg = icon(id, 24).replace("<svg ", `<svg x="${x + 20}" y="${y + 6}" `);
        return `${svg}<text x="${x + cell / 2}" y="${y + 41}" font-size="6.5" text-anchor="middle" fill="${ink}" font-family="sans-serif">${id}</text>`
          + icon(id, 16).replace("<svg ", `<svg x="${x + 24}" y="${y + 47}" `);
      }).join("");
      return `<svg xmlns="http://www.w3.org/2000/svg" width="${cols * cell}" height="${rows * cell}" style="color:${ink}"><rect width="100%" height="100%" fill="${bg}"/>${cells}</svg>`;
    };
    mkdirSync("test-results/ui-renders", { recursive: true });
    const { Resvg } = (await load("@resvg/resvg-js")) as { Resvg: Resvg };
    for (const [name, bg, ink] of [["light", "#ffffff", "#282828"], ["dark", "#1f1f1f", "#e8e8e8"]]) {
      const svg = sheet(bg, ink).replaceAll("currentColor", ink);
      writeFileSync(`test-results/ui-renders/icons-${name}.svg`, svg);
      writeFileSync(`test-results/ui-renders/icons-${name}.png`, new Resvg(svg, { fitTo: { mode: "zoom", value: 2 } }).render().asPng());
    }
  });
});
