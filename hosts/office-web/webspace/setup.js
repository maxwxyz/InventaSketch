// Setup page of the webspace package (index.html): the add-in manifest for the address the page is served from, on a button,
// sideload help for PowerPoint on Windows, Mac and the web, and downloads of the other editions (downloads.json).
// The manifest comes from manifest-template.js, which the packaging step generates from hosts/office-web/scripts/manifest.mjs,
// so this page and the script produce the same XML for the same address (test/webspace.test.ts).
// Address rules as local/create-manifest.ps1 and create-manifest.sh. Ported from Slidemax (same copyright holder,
// Apache-2.0 transfer approved 2026-11-20).
import { MANIFEST_TEMPLATE, fillManifest } from "./manifest-template.js";

/** Page language: English unless the browser language starts with "de" (or ?lang=de); messages follow it. */
let english = true;

/** Sets the language of messages (start, language button, tests). */
export function setEnglish(en) {
  english = !!en;
}

function t(en, de) {
  return english ? en : de;
}

/** English for the browser languages (navigator.languages) or ?lang= in the address; German only for "de...". */
export function englishFor(languages, href = "") {
  const m = /[?&]lang=([a-z]{2})/i.exec(href);
  if (m) return m[1].toLowerCase() !== "de";
  const first = (languages ?? []).find((x) => !!x);
  return !first || !first.toLowerCase().startsWith("de");
}

/** Applies the language: sections with data-lang shown or hidden, short texts from data-en/data-de, lang and title. */
export function applyLanguage(doc, en) {
  setEnglish(en);
  doc.documentElement.lang = en ? "en" : "de";
  for (const el of Array.from(doc.querySelectorAll("[data-lang]"))) el.hidden = el.dataset.lang !== (en ? "en" : "de");
  for (const el of Array.from(doc.querySelectorAll("[data-en][data-de]"))) el.textContent = (en ? el.dataset.en : el.dataset.de) ?? "";
  doc.title = en ? "Set up InventaSketch" : "InventaSketch einrichten";
}

const FORM = /^https:\/\/[A-Za-z0-9.\-]+(:[0-9]{1,5})?(\/[A-Za-z0-9._~\-/%]*)?$/;

/** Checked address without trailing slash, or the reason why it is not usable (same rules as create-manifest.ps1). */
export function validateAddress(input) {
  let a = (input ?? "").trim();
  if (a.toLowerCase().startsWith("https://")) a = "https://" + a.slice(8);
  a = a.replace(/\/+$/, "");
  if (!a.startsWith("https://")) {
    return { reason: t("Office loads add-ins only over HTTPS: open this page with https:// (valid certificate).",
      "Office lädt Add-ins nur über HTTPS: diese Seite mit https:// öffnen (gültiges Zertifikat).") };
  }
  if (!FORM.test(a)) {
    return { reason: t("Invalid address (allowed: https://host[:port][/path], without ? # spaces or special characters).",
      "Ungültige Adresse (erlaubt: https://host[:port][/pfad], ohne ? # Leer- oder Sonderzeichen).") };
  }
  const rest = a.slice(8);
  const cut = rest.indexOf("/");
  const authority = cut < 0 ? rest : rest.slice(0, cut);
  const path = cut < 0 ? null : rest.slice(cut + 1);
  const colon = authority.indexOf(":");
  const host = colon < 0 ? authority : authority.slice(0, colon);
  const port = colon < 0 ? "" : authority.slice(colon + 1);
  if (!host || host.startsWith(".") || host.endsWith(".") || host.includes("..")) return { reason: t("Invalid host name.", "Ungültiger Hostname.") };
  if (colon >= 0 && (!/^[0-9]+$/.test(port) || Number(port) < 1 || Number(port) > 65535)) return { reason: t("Invalid port.", "Ungültiger Port.") };
  return { address: "https://" + authority.toLowerCase() + (path !== null ? "/" + path : "") };
}

/** Folder of the page as the add-in address: file name, query and fragment are dropped. */
export function baseOfPage(href) {
  const plain = href.split("#")[0].split("?")[0];
  const folder = plain.endsWith("/") ? plain : plain.slice(0, plain.lastIndexOf("/") + 1);
  return validateAddress(folder);
}

/** Manifest for an address (without trailing slash), identical to `node scripts/manifest.mjs <address>/`. */
export function manifestFor(address) {
  return fillManifest(MANIFEST_TEMPLATE, address + "/");
}

/** File name as create-manifest: manifest-<host>[-<port>].xml. */
export function fileName(address) {
  const authority = address.slice("https://".length).split("/", 1)[0].replace(":", "-");
  return "manifest-" + authority.replace(/[^A-Za-z0-9.\-]/g, "-") + ".xml";
}

/** Add-in id (<Id>) from the manifest, otherwise empty. */
export function addinId(xml) {
  const m = /<Id>\s*([0-9A-Fa-f]{8}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{12})\s*<\/Id>/.exec(xml ?? "");
  return m ? m[1].toLowerCase() : "";
}

/** Folder from which PowerPoint for Mac loads manifests (Microsoft sideloading documentation). */
export const MAC_FOLDER = "~/Library/Containers/com.microsoft.Powerpoint/Data/Documents/wef";

/** Registry key from which PowerPoint for Windows loads manifests (value name = add-in id, value = manifest path). */
export const WIN_KEY = "HKCU:\\Software\\Microsoft\\Office\\16.0\\WEF\\Developer";

function checkId(id) {
  if (!/^[0-9a-f-]{36}$/.test(id)) throw new Error("Invalid add-in id: " + id);
}

/** Suggested location of the downloaded file per system: the user's Downloads folder. */
export function pathSuggestion(system, file) {
  return system === "mac" ? `~/Downloads/${file}` : `%USERPROFILE%\\Downloads\\${file}`;
}

/**
 * Checks the entered location of the downloaded file: outer quotes and backslash-escaped spaces (file dragged into Terminal)
 * are removed, file:// addresses are read as paths; the file must end in .xml; no line breaks or control characters.
 */
export function checkPath(system, input) {
  let p = (input ?? "").trim();
  if (p.length >= 2 && ((p.startsWith('"') && p.endsWith('"')) || (p.startsWith("'") && p.endsWith("'")))) p = p.slice(1, -1).trim();
  if (system === "mac") p = p.replace(/\\ /g, " ");
  p = fileUrlToPath(system, p);
  if (!p) return { reason: t("Please enter the location of the downloaded file.", "Bitte den Ort der heruntergeladenen Datei eintragen.") };
  if (/[\u0000-\u001f\u007f]/.test(p)) return { reason: t("The path contains control characters.", "Der Pfad enthält Steuerzeichen.") };
  if (!/\.xml$/i.test(p)) return { reason: t("The path must point to the manifest file (.xml).", "Der Pfad muss auf die Manifestdatei (.xml) zeigen.") };
  return { path: p };
}

/** file:// address as a system path (file:///Users/a%20b/x.xml, file:///C:/Users/x.xml); other input unchanged. */
export function fileUrlToPath(system, input) {
  if (!/^file:\/\//i.test(input)) return input;
  let rest = input.slice(7);
  if (rest.toLowerCase().startsWith("localhost/")) rest = rest.slice(9);
  let path;
  try {
    path = decodeURIComponent(rest);
  } catch {
    path = rest;
  }
  if (system === "win") {
    // /C:/Users/... becomes C:\Users\..., server/share/... a UNC path.
    if (/^\/[A-Za-z]:\//.test(path)) path = path.slice(1);
    else if (!path.startsWith("/")) path = "//" + path;
    path = path.replace(/\//g, "\\");
  }
  return path;
}

/** Path for the Mac shell in single quotes (no character is special there, not even "!" of zsh); leading "~/" becomes "$HOME". */
function shellPath(path) {
  const home = path === "~" || path.startsWith("~/");
  const rest = home ? path.slice(1) : path;
  return (home ? '"$HOME"' : "") + (rest ? `'${rest.replace(/'/g, "'\\''")}'` : "");
}

/** String for PowerShell in single quotes. */
function psString(text) {
  return `'${text.replace(/'/g, "''")}'`;
}

/** Terminal command for the Mac: create the wef folder and copy the manifest into it. No administrator rights. Throws on an invalid path. */
export function macCommand(input) {
  const { path, reason } = checkPath("mac", input);
  if (!path) throw new Error(reason);
  const target = shellPath(MAC_FOLDER);
  return `mkdir -p ${target} && cp ${shellPath(path)} ${target}/`;
}

/**
 * Windows PowerShell command: copy the manifest to %LOCALAPPDATA%\InventaSketch\Manifest and register it for PowerPoint (own
 * user only, no administrator rights). An existing key is not recreated (New-Item -Force would delete its other values); if
 * the copy fails, nothing is registered. Throws on an invalid path or id.
 */
export function windowsCommand(input, id) {
  checkId(id);
  const { path, reason } = checkPath("win", input);
  if (!path) throw new Error(reason);
  // Environment variable at the start (%USERPROFILE%, %OneDrive%, %LOCALAPPDATA% ...) through $env.
  const variable = /^%([A-Za-z_][A-Za-z0-9_]*)%[\\/]/.exec(path);
  const source = variable ? `(Join-Path $env:${variable[1]} ${psString(path.slice(variable[0].length))})` : psString(path);
  const name = path.split(/[\\/]/).pop();
  return "try { " + [
    "$d = Join-Path $env:LOCALAPPDATA 'InventaSketch\\Manifest'",
    "New-Item -ItemType Directory -Force $d -ErrorAction Stop | Out-Null",
    `Copy-Item -LiteralPath ${source} -Destination $d -Force -ErrorAction Stop`,
    `$k = '${WIN_KEY}'`,
    "if (-not (Test-Path $k)) { New-Item $k -Force -ErrorAction Stop | Out-Null }",
    `Set-ItemProperty $k -Name '${id}' -Value (Join-Path $d ${psString(name)}) -ErrorAction Stop`,
    "Write-Host 'InventaSketch: OK'",
  ].join("; ") + " } catch { Write-Host ('InventaSketch: ' + $_.Exception.Message) -ForegroundColor Red }";
}

/** Windows PowerShell command that removes the registration again. */
export function windowsRemoveCommand(id) {
  checkId(id);
  return `Remove-ItemProperty '${WIN_KEY}' -Name '${id}'`;
}

const KIND_ORDER = { com: 0, libreoffice: 1 };

/** Downloads (entries of downloads.json: file, kind, bytes, date) in a fixed order, with title and hint per kind. */
export function sortDownloads(list) {
  const texts = english ? {
    com: ["PowerPoint on Windows (COM add-in)", "Unzip, close PowerPoint, double-click Install.cmd (no administrator rights). Full editing on the slide."],
    libreoffice: ["LibreOffice Impress", "In LibreOffice: Tools > Extension Manager > Add, choose the file, restart LibreOffice."],
  } : {
    com: ["PowerPoint unter Windows (COM-Add-in)", "Entpacken, PowerPoint schließen, Install.cmd doppelklicken (keine Administratorrechte). Volle Bedienung auf der Folie."],
    libreoffice: ["LibreOffice Impress", "In LibreOffice: Extras > Extension Manager > Hinzufügen, Datei wählen, LibreOffice neu starten."],
  };
  return (Array.isArray(list) ? list : [])
    .filter((d) => d && typeof d.file === "string" && /^[A-Za-z0-9._-]+$/.test(d.file) && d.kind in KIND_ORDER)
    .sort((a, b) => KIND_ORDER[a.kind] - KIND_ORDER[b.kind] || a.file.localeCompare(b.file))
    .map((d) => ({ ...d, title: texts[d.kind][0] + variant(d), hint: texts[d.kind][1] }));
}

/** Platform of a LibreOffice extension from its file name (InventaSketch-LibreOffice-<version>[-<rid>].oxt). */
export function variant(d) {
  if (d.kind !== "libreoffice") return "";
  const rid = /-(win|linux|osx)-(x64|arm64)\.oxt$/i.exec(d.file);
  if (!rid) return t(" (all systems)", " (alle Systeme)");
  const system = { win: "Windows", linux: "Linux", osx: "macOS" }[rid[1].toLowerCase()];
  return ` (${system}, ${rid[2].toLowerCase() === "arm64" ? "ARM" : "x64"})`;
}

/** Size in MB with one decimal. */
export function size(bytes) {
  return (Math.max(0, Number(bytes) || 0) / 1048576).toLocaleString(english ? "en-US" : "de-DE", { minimumFractionDigits: 1, maximumFractionDigits: 1 }) + " MB";
}

/** Line with the version of the package (version.json written by the packaging step). */
export function versionText(info) {
  if (!info || typeof info.version !== "string" || !info.version) return "";
  return `${t("Version", "Version")} ${info.version}` + (typeof info.built === "string" && info.built ? `, ${t("built", "Stand")} ${info.built}` : "");
}

/** Delay before the object URL of the downloaded manifest is released (Safari aborts the download if it ends at once). */
export const RELEASE_MS = 40000;

/** Offers the manifest for the address as a file download. Errors appear in the hint below the button. */
export function downloadManifest(doc, address, hint, url = URL, later = (f, ms) => void setTimeout(f, ms)) {
  if (hint) hint.textContent = "";
  let objectUrl = "";
  try {
    objectUrl = url.createObjectURL(new Blob([manifestFor(address)], { type: "application/xml" }));
    const link = doc.createElement("a");
    link.href = objectUrl;
    link.download = fileName(address);
    doc.body.appendChild(link);
    link.click();
    link.remove();
    return true;
  } catch (e) {
    if (hint) hint.textContent = `${t("Manifest not created", "Manifest nicht erzeugt")}: ${e instanceof Error ? e.message : String(e)}`;
    return false;
  } finally {
    if (objectUrl) later(() => url.revokeObjectURL(objectUrl), RELEASE_MS);
  }
}

/** Fills the list of other editions from downloads.json. */
async function showDownloads(doc, target) {
  target.replaceChildren();
  try {
    const response = await fetch("downloads.json", { cache: "no-store" });
    const list = response.ok ? sortDownloads(await response.json()) : [];
    if (list.length === 0) {
      target.textContent = t("No other editions on this server.", "Keine weiteren Fassungen auf diesem Server.");
      return;
    }
    for (const d of list) {
      const item = doc.createElement("li");
      const link = doc.createElement("a");
      link.href = "downloads/" + encodeURIComponent(d.file);
      link.textContent = d.title;
      link.setAttribute("download", d.file);
      const info = doc.createElement("div");
      info.className = "small";
      info.textContent = d.file + (d.bytes ? `, ${size(d.bytes)}` : "") + (d.date ? `, ${t("dated", "Stand")} ${d.date}` : "") + `. ${d.hint}`;
      item.append(link, info);
      target.appendChild(item);
    }
  } catch {
    target.textContent = t("List of downloads not readable.", "Liste der Downloads nicht lesbar.");
  }
}

/** Builds the page in the browser. */
export async function start(doc, href) {
  const el = (id) => doc.getElementById(id);
  let en = englishFor(typeof navigator !== "undefined" ? [...(navigator.languages ?? []), navigator.language] : [], href);
  applyLanguage(doc, en);
  const base = baseOfPage(href);
  const button = el("download-manifest");
  const xmlView = el("manifest-xml");
  const commands = {};
  const render = () => {
    el("address").textContent = base.address ?? base.reason ?? "";
    el("address").classList.toggle("error", !base.address);
  };
  render();
  if (!base.address) {
    button.disabled = true;
    for (const b of Array.from(doc.querySelectorAll("button[data-copy]"))) b.disabled = true;
  } else {
    const address = base.address;
    const xml = manifestFor(address);
    const id = addinId(xml);
    const file = fileName(address);
    xmlView.textContent = xml;
    for (const node of Array.from(doc.querySelectorAll(".manifest-file"))) node.textContent = file;
    commands.mac = () => macCommand(el("path-mac").value);
    commands.win = () => windowsCommand(el("path-win").value, id);
    commands["win-remove"] = () => windowsRemoveCommand(id);
    for (const system of ["mac", "win"]) {
      const input = el("path-" + system);
      input.value = pathSuggestion(system, file);
      input.addEventListener("input", () => show(system));
      // A file dropped into the field: browsers reveal at most a file:// address as text, never open the file.
      input.addEventListener("dragover", (e) => e.preventDefault());
      input.addEventListener("drop", (e) => {
        e.preventDefault();
        const text = (e.dataTransfer?.getData("text/uri-list") || e.dataTransfer?.getData("text/plain") || "")
          .split(/\r?\n/).find((x) => x && !x.startsWith("#")) ?? "";
        if (text) {
          input.value = text.trim();
          show(system);
        } else {
          el("hint-" + system).textContent = system === "mac"
            ? t("The browser does not pass on the file location. Select the file in the Finder, Option+Command+C copies the path, paste it here.",
              "Der Browser gibt den Ort der Datei nicht weiter. Im Finder die Datei wählen, Wahl+Befehl+C kopiert den Pfad, hier einfügen.")
            : t("The browser does not pass on the file location. In Explorer Shift+right-click, \"Copy as path\", paste it here.",
              "Der Browser gibt den Ort der Datei nicht weiter. Im Explorer Umschalt+Rechtsklick, \"Als Pfad kopieren\", hier einfügen.");
        }
      });
    }
    doc.addEventListener("dragover", (e) => e.preventDefault());
    doc.addEventListener("drop", (e) => e.preventDefault());
    for (const b of Array.from(doc.querySelectorAll("button[data-copy]"))) {
      b.addEventListener("click", () => {
        const text = show(b.dataset.copy);
        if (!text) return;
        const label = b.textContent;
        const report = (word) => {
          b.textContent = word;
          setTimeout(() => (b.textContent = label), 1500);
        };
        const failed = () => report(t("Not copied, select the command below", "Nicht kopiert, Befehl unten markieren"));
        try {
          void (navigator.clipboard?.writeText(text) ?? Promise.reject(new Error("no clipboard"))).then(() => report(t("Copied", "Kopiert")), failed);
        } catch {
          failed();
        }
      });
    }
    button.addEventListener("click", () => void downloadManifest(doc, address, el("hint-manifest")));
  }
  function show(kind) {
    const target = el("command-" + kind);
    const hint = el("hint-" + kind);
    try {
      const text = commands[kind] ? commands[kind]() : "";
      target.textContent = text;
      hint.textContent = "";
      return text;
    } catch (e) {
      target.textContent = "";
      hint.textContent = e instanceof Error ? e.message : String(e);
      return "";
    }
  }
  const refreshCommands = () => {
    if (base.address) for (const kind of ["mac", "win", "win-remove"]) show(kind);
  };
  refreshCommands();
  let info = null;
  try {
    const response = await fetch("version.json", { cache: "no-store" });
    if (response.ok) info = await response.json();
  } catch {
    // no version line without version.json
  }
  el("version").textContent = versionText(info);
  el("language").addEventListener("click", () => {
    en = !en;
    applyLanguage(doc, en);
    // Messages are created in the page language: recreate them in the new one.
    const again = baseOfPage(href);
    base.reason = again.reason;
    render();
    el("version").textContent = versionText(info);
    el("hint-manifest").textContent = "";
    refreshCommands();
    void showDownloads(doc, el("downloads"));
  });
  await showDownloads(doc, el("downloads"));
}

if (typeof document !== "undefined" && document.getElementById("download-manifest")) {
  void start(document, location.href);
}
