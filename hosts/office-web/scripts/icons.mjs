// Rasterises the InventaSketch logo (assets/branding/logo.svg, the single source) with @resvg/resvg-js (no browser):
// - public/assets/icon-<size>.png for the Office manifest and the task pane favicon (16, 32, 64, 80, 128);
// - assets/branding/inventasketch.ico (16–256 px, PNG frames) for Windows title bars, dialogs and executables;
// - assets/branding/logo-256.png for README and documentation.
// The branding files are committed so the .NET builds do not need Node; the output is deterministic.
import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { Resvg } from "@resvg/resvg-js";

const root = dirname(dirname(fileURLToPath(import.meta.url)));
const branding = join(root, "..", "..", "assets", "branding");
const svg = readFileSync(join(branding, "logo.svg"), "utf8");
const render = (size) => new Resvg(svg, { fitTo: { mode: "width", value: size } }).render().asPng();

const out = join(root, "public", "assets");
mkdirSync(out, { recursive: true });
for (const size of [16, 32, 64, 80, 128]) writeFileSync(join(out, `icon-${size}.png`), render(size));

/** Windows icon with PNG-compressed frames (supported since Windows Vista). */
function ico(sizes) {
  const frames = sizes.map((size) => ({ size, png: render(size) }));
  const header = Buffer.alloc(6 + 16 * frames.length);
  header.writeUInt16LE(0, 0); // reserved
  header.writeUInt16LE(1, 2); // type: icon
  header.writeUInt16LE(frames.length, 4);
  let offset = header.length;
  frames.forEach(({ size, png }, i) => {
    const e = 6 + 16 * i;
    header.writeUInt8(size >= 256 ? 0 : size, e); // width (0 = 256)
    header.writeUInt8(size >= 256 ? 0 : size, e + 1); // height
    header.writeUInt8(0, e + 2); // palette colours
    header.writeUInt8(0, e + 3); // reserved
    header.writeUInt16LE(1, e + 4); // colour planes
    header.writeUInt16LE(32, e + 6); // bits per pixel
    header.writeUInt32LE(png.length, e + 8);
    header.writeUInt32LE(offset, e + 12);
    offset += png.length;
  });
  return Buffer.concat([header, ...frames.map((f) => f.png)]);
}

writeFileSync(join(branding, "inventasketch.ico"), ico([16, 20, 24, 32, 40, 48, 64, 128, 256]));
writeFileSync(join(branding, "logo-256.png"), render(256));
console.log(`icons: written to ${out} and ${branding}`);
