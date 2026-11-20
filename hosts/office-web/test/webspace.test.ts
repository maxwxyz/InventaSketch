import { afterAll, beforeAll, describe, expect, it } from "vitest";
import { fillManifest, manifestTemplate, manifestTemplateModule } from "../scripts/manifest.mjs";

// The project has no Node type declarations (tsconfig types: office-js, vite/client): Node modules are loaded dynamically
// and typed with the few members used here.
interface NodeFs {
  copyFileSync(from: string, to: string): void;
  existsSync(path: string): boolean;
  mkdirSync(path: string, options?: { recursive: boolean }): void;
  mkdtempSync(prefix: string): string;
  readFileSync(path: string, encoding: "utf8"): string;
  rmSync(path: string, options?: { recursive: boolean; force: boolean }): void;
  writeFileSync(path: string, data: string): void;
}
interface NodeProcess { execPath: string; env: Record<string, string | undefined> }
const load = (name: string) => import(/* @vite-ignore */ name);
const { copyFileSync, existsSync, mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } = (await load("node:fs")) as NodeFs;
const { tmpdir } = (await load("node:os")) as { tmpdir(): string };
const { dirname, join, resolve } = (await load("node:path")) as Record<"dirname" | "join" | "resolve", (...parts: string[]) => string>;
const { fileURLToPath, pathToFileURL } = (await load("node:url")) as { fileURLToPath(url: string): string; pathToFileURL(path: string): URL };
const { execFileSync } = (await load("node:child_process")) as {
  execFileSync(file: string, args: string[], options: { env: Record<string, string | undefined>; encoding: "utf8" }): string;
};
const process = (globalThis as unknown as { process: NodeProcess }).process;

const web = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const work = mkdtempSync(join(tmpdir(), "isk-webspace-"));

interface Download { file: string; kind: string; bytes: number; date: string }
interface SetupPage {
  setEnglish(en: boolean): void;
  englishFor(languages: readonly string[], href?: string): boolean;
  validateAddress(input: string): { address?: string; reason?: string };
  baseOfPage(href: string): { address?: string; reason?: string };
  manifestFor(address: string): string;
  fileName(address: string): string;
  addinId(xml: string): string;
  macCommand(input: string): string;
  windowsCommand(input: string, id: string): string;
  windowsRemoveCommand(id: string): string;
  sortDownloads(list: unknown): Array<Download & { title: string; hint: string }>;
  downloadManifest(doc: unknown, address: string, hint: null, url: unknown, later: (f: () => void, ms: number) => void): boolean;
}
interface Packager {
  excluded(path: string): boolean;
  findDownloads(folder: string, version: string): Array<{ path: string; kind: string }>;
  buildWebspace(options: { app: string; out: string; downloadsFrom: string; version: string }): { out: string; downloads: Download[] };
}

let page: SetupPage;
let packager: Packager;

beforeAll(async () => {
  // The setup page as it is deployed: setup.js next to the manifest-template.js generated from scripts/manifest.mjs.
  const site = join(work, "site");
  mkdirSync(site, { recursive: true });
  copyFileSync(join(web, "webspace", "setup.js"), join(site, "setup.js"));
  writeFileSync(join(site, "manifest-template.js"), manifestTemplateModule());
  page = (await import(/* @vite-ignore */ pathToFileURL(join(site, "setup.js")).href)) as SetupPage;
  packager = (await import(/* @vite-ignore */ pathToFileURL(join(web, "webspace", "package.mjs")).href)) as Packager;
});

afterAll(() => rmSync(work, { recursive: true, force: true }));

/** Manifest exactly as scripts/manifest.mjs renders it for ISK_BASE_URL, in a separate Node process. */
function scriptManifest(base: string): string {
  const url = pathToFileURL(join(web, "scripts", "manifest.mjs")).href;
  return execFileSync(process.execPath, ["--input-type=module", "-e", `const m = await import(${JSON.stringify(url)}); process.stdout.write(m.manifest);`], {
    env: { ...process.env, ISK_BASE_URL: base },
    encoding: "utf8",
  });
}

describe("webspace setup page manifest", () => {
  const pages = [
    ["https://addins.example.com/isk-7k2q/", "https://addins.example.com/isk-7k2q"],
    ["https://Example.ORG:8443/a/b-c_d~e/index.html?lang=de#top", "https://example.org:8443/a/b-c_d~e"],
    ["https://host.example/", "https://host.example"],
  ] as const;

  it.each(pages)("produces the same XML as scripts/manifest.mjs for %s", (href, address) => {
    const base = page.baseOfPage(href);
    expect(base.address).toBe(address);
    const xml = page.manifestFor(base.address!);
    expect(xml).toBe(scriptManifest(address + "/"));
    expect(xml).toBe(fillManifest(manifestTemplate, address));
    expect(xml).toContain(`<SourceLocation DefaultValue="${address}/taskpane.html" />`);
    expect(xml).toContain(`<AppDomain>${new URL(address).origin}</AppDomain>`);
    expect(xml).not.toMatch(/@(BASE|ORIGIN)@/);
  });

  it("refuses addresses Office cannot load", () => {
    page.setEnglish(true);
    expect(page.baseOfPage("http://example.com/isk/").reason).toMatch(/HTTPS/);
    expect(page.validateAddress("https://exa mple.com").reason).toMatch(/Invalid address/);
    expect(page.validateAddress("https://example..com").reason).toBe("Invalid host name.");
    expect(page.validateAddress("https://example.com:70000").reason).toBe("Invalid port.");
    page.setEnglish(false);
    expect(page.validateAddress("https://example.com:0").reason).toBe("Ungültiger Port.");
    page.setEnglish(true);
  });

  it("follows the browser language with English as fallback", () => {
    expect(page.englishFor(["de-DE", "en"])).toBe(false);
    expect(page.englishFor(["fr-FR", "de"])).toBe(true);
    expect(page.englishFor([])).toBe(true);
    expect(page.englishFor(["de-DE"], "https://x.example/?lang=en")).toBe(true);
  });

  it("names the file and builds sideload commands like the local package", () => {
    const xml = page.manifestFor("https://example.com:8443/isk");
    const id = page.addinId(xml);
    expect(id).toBe("5d1f2c7e-8a43-4f0b-9c61-2b7e4a9d3f10");
    expect(page.fileName("https://example.com:8443/isk")).toBe("manifest-example.com-8443.xml");
    expect(page.macCommand("~/Downloads/manifest-example.com.xml"))
      .toBe(`mkdir -p "$HOME"'/Library/Containers/com.microsoft.Powerpoint/Data/Documents/wef' && cp "$HOME"'/Downloads/manifest-example.com.xml' "$HOME"'/Library/Containers/com.microsoft.Powerpoint/Data/Documents/wef'/`);
    const win = page.windowsCommand("%USERPROFILE%\\Downloads\\manifest-example.com.xml", id);
    expect(win).toContain("(Join-Path $env:USERPROFILE 'Downloads\\manifest-example.com.xml')");
    expect(win).toContain(`-Name '${id}'`);
    expect(page.windowsRemoveCommand(id)).toBe(`Remove-ItemProperty 'HKCU:\\Software\\Microsoft\\Office\\16.0\\WEF\\Developer' -Name '${id}'`);
    expect(() => page.macCommand("~/Downloads/manifest.txt")).toThrow();
  });

  it("offers the manifest as a file named after the host and releases the object URL later", async () => {
    const clicked: Array<{ href: string; download: string }> = [];
    const link = { href: "", download: "", click: () => clicked.push({ href: link.href, download: link.download }), remove: () => undefined };
    const doc = { createElement: () => link, body: { appendChild: () => undefined } };
    let blob: Blob | null = null;
    const revoked: string[] = [];
    const url = { createObjectURL: (b: Blob) => ((blob = b), "blob:1"), revokeObjectURL: (u: string) => revoked.push(u) };
    let release: (() => void) | null = null;
    expect(page.downloadManifest(doc, "https://example.com/isk", null, url, (f: () => void) => (release = f))).toBe(true);
    expect(clicked).toEqual([{ href: "blob:1", download: "manifest-example.com.xml" }]);
    expect(await blob!.text()).toBe(scriptManifest("https://example.com/isk/"));
    expect(revoked).toEqual([]);
    release!();
    expect(revoked).toEqual(["blob:1"]);
  });

  it("lists only known kinds with safe file names", () => {
    const list = page.sortDownloads([
      { file: "InventaSketch-LibreOffice-0.1.0-osx-arm64.oxt", kind: "libreoffice", bytes: 1, date: "" },
      { file: "InventaSketch-AddIn-0.1.0.zip", kind: "com", bytes: 2, date: "" },
      { file: "../evil.zip", kind: "com", bytes: 0, date: "" },
      { file: "x.exe", kind: "other", bytes: 0, date: "" },
    ]);
    expect(list.map((d) => d.file)).toEqual(["InventaSketch-AddIn-0.1.0.zip", "InventaSketch-LibreOffice-0.1.0-osx-arm64.oxt"]);
    expect(list[1].title).toContain("macOS, ARM");
  });
});

describe("webspace packaging", () => {
  function fakeApp(): string {
    const app = join(work, "app");
    mkdirSync(join(app, "_framework"), { recursive: true });
    mkdirSync(join(app, "assets"), { recursive: true });
    for (const f of ["taskpane.html", "sidebar.html", "dev.html", "manifest.xml", "_framework/dotnet.js", "_framework/dotnet.js.br", "assets/icon-32.png", "assets/main.js.map"]) {
      writeFileSync(join(app, ...f.split("/")), f);
    }
    return app;
  }

  it("copies the add-in and the setup page and lists only existing downloads", () => {
    const packages = join(work, "packages");
    mkdirSync(packages, { recursive: true });
    for (const f of ["InventaSketch-AddIn-0.1.0.zip", "InventaSketch-AddIn-0.0.9.zip", "InventaSketch-Web.zip", "notes.txt"]) writeFileSync(join(packages, f), f);
    const out = join(work, "out", "InventaSketch-Webspace");
    const { downloads } = packager.buildWebspace({ app: fakeApp(), out, downloadsFrom: packages, version: "0.1.0" });

    expect(downloads.map((d) => [d.file, d.kind])).toEqual([["InventaSketch-AddIn-0.1.0.zip", "com"]]);
    expect(JSON.parse(readFileSync(join(out, "downloads.json"), "utf8"))).toEqual(downloads);
    expect(existsSync(join(out, "downloads", "InventaSketch-AddIn-0.1.0.zip"))).toBe(true);
    expect(existsSync(join(out, "downloads", "InventaSketch-AddIn-0.0.9.zip"))).toBe(false);
    for (const f of ["index.html", "setup.js", "manifest-template.js", ".htaccess", "web.config", "robots.txt", "version.json", "taskpane.html", "_framework/dotnet.js.br"]) {
      expect(existsSync(join(out, ...f.split("/"))), f).toBe(true);
    }
    for (const f of ["dev.html", "manifest.xml", "assets/main.js.map"]) expect(existsSync(join(out, ...f.split("/"))), f).toBe(false);
    expect(readFileSync(join(out, "manifest-template.js"), "utf8")).toBe(manifestTemplateModule());

    // A LibreOffice extension appears once it exists, never before.
    writeFileSync(join(packages, "InventaSketch-LibreOffice-0.1.0-linux-x64.oxt"), "oxt");
    expect(packager.findDownloads(packages, "0.1.0").map((d) => d.kind)).toEqual(["com", "libreoffice"]);
    expect(packager.findDownloads(join(work, "missing"), "0.1.0")).toEqual([]);
  });

  it("refuses to package without a web build", () => {
    expect(() => packager.buildWebspace({ app: join(work, "nothing"), out: join(work, "out2"), downloadsFrom: work, version: "0.1.0" })).toThrow(/npm run build/);
  });
});
