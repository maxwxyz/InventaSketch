// Rasterises the InventaSketch logo (assets/logo.svg) to the PNG sizes Office needs (16, 32, 64, 80, 128)
// into public/assets. Uses @resvg/resvg-js (no browser needed).
import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { Resvg } from "@resvg/resvg-js";

const root = dirname(dirname(fileURLToPath(import.meta.url)));
const svg = readFileSync(join(root, "assets", "logo.svg"), "utf8");
const out = join(root, "public", "assets");
mkdirSync(out, { recursive: true });
for (const size of [16, 32, 64, 80, 128]) {
  const png = new Resvg(svg, { fitTo: { mode: "width", value: size } }).render().asPng();
  writeFileSync(join(out, `icon-${size}.png`), png);
}
console.log(`icons: written to ${out}`);
