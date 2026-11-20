// Assembles dist/InventaSketch-Webspace and dist/InventaSketch-Webspace-<version>.zip: the folder is uploaded as it is to any
// HTTPS webspace. Contents: the built web add-in (hosts/office-web/dist without source maps, dev page and localhost manifest),
// the setup page index.html with setup.js and the generated manifest-template.js (manifest for the page's own address on a
// button), .htaccess (Apache) and web.config (IIS), robots.txt, version.json, and downloads of the other editions listed in
// downloads.json (only files that exist).
// Ported from Slidemax (scripts/webspace.mjs and paket.mjs; same copyright holder, Apache-2.0 transfer approved 2026-11-20).
//
// Usage: node webspace/package.mjs [--out <folder>] [--downloads <folder with packages>] [--version <x.y.z>]
// Downloads: InventaSketch-AddIn-<version>.zip, InventaSketch-LibreOffice-<version>[-<rid>].oxt (both from dist/).

import { copyFileSync, cpSync, existsSync, mkdirSync, readFileSync, readdirSync, rmSync, statSync, writeFileSync } from "node:fs";
import { basename, dirname, join, relative, resolve, sep } from "node:path";
import { fileURLToPath } from "node:url";
import { deflateRawSync } from "node:zlib";
import { manifestTemplateModule } from "../scripts/manifest.mjs";

const here = dirname(fileURLToPath(import.meta.url));
const web = dirname(here);
const repo = resolve(web, "..", "..");

/** Files of the setup page and server settings copied from this folder. */
export const SITE_FILES = ["index.html", "setup.js", ".htaccess", "web.config", "robots.txt"];

/** Not on the webspace: source maps, the dev page and the manifest for localhost (the setup page creates the right one). */
export function excluded(path) {
  return /\.map$/i.test(path) || ["dev.html", "manifest.xml"].includes(basename(path));
}

/** Version from Directory.Build.props (<Version>), otherwise 0.0.0. */
export function repoVersion(props = join(repo, "Directory.Build.props")) {
  const m = existsSync(props) ? /<Version>([0-9][0-9A-Za-z.\-]*)<\/Version>/.exec(readFileSync(props, "utf8")) : null;
  return m ? m[1] : "0.0.0";
}

/**
 * Packages of the other editions in a folder for a version: the COM add-in ZIP and LibreOffice extensions. Only existing
 * files are returned, never expected ones.
 */
export function findDownloads(folder, version) {
  if (!existsSync(folder)) return [];
  const v = version.replace(/[.\\^$*+?()[\]{}|-]/g, "\\$&");
  const com = new RegExp(`^InventaSketch-AddIn-${v}\\.zip$`);
  const oxt = new RegExp(`^InventaSketch-LibreOffice-${v}(-(win|linux|osx)-(x64|arm64))?\\.oxt$`);
  const found = [];
  for (const name of readdirSync(folder).sort()) {
    const path = join(folder, name);
    if (!statSync(path).isFile()) continue;
    const kind = com.test(name) ? "com" : oxt.test(name) ? "libreoffice" : null;
    if (kind) found.push({ path, kind });
  }
  return found;
}

/** Builds the webspace folder; returns its path and the downloads.json entries. */
export function buildWebspace({ app = join(web, "dist"), out, downloadsFrom, version }) {
  if (!existsSync(join(app, "taskpane.html")) || !existsSync(join(app, "_framework"))) {
    throw new Error(`${app} has no built web add-in (taskpane.html, _framework): run npm run build in hosts/office-web first.`);
  }
  rmSync(out, { recursive: true, force: true });
  cpSync(app, out, { recursive: true, filter: (path) => !excluded(path) });
  for (const name of SITE_FILES) copyFileSync(join(here, name), join(out, name));
  writeFileSync(join(out, "manifest-template.js"), manifestTemplateModule(), "utf8");
  writeFileSync(join(out, "version.json"), JSON.stringify({ version, built: new Date().toISOString().slice(0, 10) }, null, 1) + "\n");

  const downloads = [];
  for (const { path, kind } of findDownloads(downloadsFrom, version)) {
    mkdirSync(join(out, "downloads"), { recursive: true });
    copyFileSync(path, join(out, "downloads", basename(path)));
    const s = statSync(path);
    downloads.push({ file: basename(path), kind, bytes: s.size, date: s.mtime.toISOString().slice(0, 10) });
  }
  writeFileSync(join(out, "downloads.json"), JSON.stringify(downloads, null, 1) + "\n");

  for (const part of [...SITE_FILES, "taskpane.html", "manifest-template.js", "version.json", "downloads.json", "assets/icon-32.png"]) {
    if (!existsSync(join(out, ...part.split("/")))) throw new Error(`Webspace incomplete: ${part} is missing.`);
  }
  return { out, downloads };
}

// ZIP writer (deflate, UTF-8 names, forward slashes): Compress-Archive of Windows PowerShell 5.1 writes backslashes.
const crcTable = Array.from({ length: 256 }, (_, n) => {
  let c = n;
  for (let k = 0; k < 8; k++) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1;
  return c >>> 0;
});
const crc32 = (b) => {
  let c = 0xffffffff;
  for (const x of b) c = crcTable[(c ^ x) & 0xff] ^ (c >>> 8);
  return (c ^ 0xffffffff) >>> 0;
};

function files(folder) {
  const list = [];
  for (const name of readdirSync(folder).sort()) {
    const path = join(folder, name);
    if (statSync(path).isDirectory()) list.push(...files(path));
    else list.push(path);
  }
  return list;
}

/** Writes all files below folder into a ZIP (entries under prefix/); returns the number of files. */
export function zipFolder(folder, target, prefix = "") {
  const parts = [];
  const directory = [];
  let offset = 0;
  for (const path of files(folder)) {
    const name = Buffer.from(prefix + relative(folder, path).split(sep).join("/"), "utf8");
    const raw = readFileSync(path);
    const packed = deflateRawSync(raw, { level: 9 });
    const time = statSync(path).mtime;
    const dosTime = (time.getHours() << 11) | (time.getMinutes() << 5) | (time.getSeconds() >> 1);
    const dosDate = ((Math.max(time.getFullYear(), 1980) - 1980) << 9) | ((time.getMonth() + 1) << 5) | time.getDate();
    const crc = crc32(raw);
    const local = Buffer.alloc(30);
    local.writeUInt32LE(0x04034b50, 0);
    local.writeUInt16LE(20, 4);
    local.writeUInt16LE(0x0800, 6);
    local.writeUInt16LE(8, 8);
    local.writeUInt16LE(dosTime, 10);
    local.writeUInt16LE(dosDate, 12);
    local.writeUInt32LE(crc, 14);
    local.writeUInt32LE(packed.length, 18);
    local.writeUInt32LE(raw.length, 22);
    local.writeUInt16LE(name.length, 26);
    local.writeUInt16LE(0, 28);
    const central = Buffer.alloc(46);
    central.writeUInt32LE(0x02014b50, 0);
    central.writeUInt16LE((3 << 8) | 20, 4);
    central.writeUInt16LE(20, 6);
    central.writeUInt16LE(0x0800, 8);
    central.writeUInt16LE(8, 10);
    central.writeUInt16LE(dosTime, 12);
    central.writeUInt16LE(dosDate, 14);
    central.writeUInt32LE(crc, 16);
    central.writeUInt32LE(packed.length, 20);
    central.writeUInt32LE(raw.length, 24);
    central.writeUInt16LE(name.length, 28);
    central.writeUInt32LE((0o100644 << 16) >>> 0, 38);
    central.writeUInt32LE(offset, 42);
    parts.push(local, name, packed);
    directory.push(central, name);
    offset += local.length + name.length + packed.length;
  }
  const directorySize = directory.reduce((s, b) => s + b.length, 0);
  const end = Buffer.alloc(22);
  end.writeUInt32LE(0x06054b50, 0);
  end.writeUInt16LE(directory.length / 2, 8);
  end.writeUInt16LE(directory.length / 2, 10);
  end.writeUInt32LE(directorySize, 12);
  end.writeUInt32LE(offset, 16);
  writeFileSync(target, Buffer.concat([...parts, ...directory, end]));
  return directory.length / 2;
}

function main() {
  const args = process.argv.slice(2);
  const arg = (name, fallback) => {
    const i = args.indexOf(name);
    return i >= 0 && i + 1 < args.length ? args[i + 1] : fallback;
  };
  const outDir = resolve(arg("--out", join(repo, "dist")));
  const downloadsFrom = resolve(arg("--downloads", outDir));
  const version = arg("--version", repoVersion());
  mkdirSync(outDir, { recursive: true });
  const { out, downloads } = buildWebspace({ out: join(outDir, "InventaSketch-Webspace"), downloadsFrom, version });
  if (!downloads.some((d) => d.kind === "com")) {
    console.warn(`webspace: InventaSketch-AddIn-${version}.zip not found in ${downloadsFrom}; the COM add-in is not offered (run scripts/package-com.ps1 first).`);
  }
  const zip = join(outDir, `InventaSketch-Webspace-${version}.zip`);
  rmSync(zip, { force: true });
  const count = zipFolder(out, zip, "InventaSketch-Webspace/");
  console.log(`Webspace: ${out} (downloads: ${downloads.map((d) => d.file).join(", ") || "none"})`);
  console.log(`ZIP:      ${zip} (${count} files)`);
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) main();
