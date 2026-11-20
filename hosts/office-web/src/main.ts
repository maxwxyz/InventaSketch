// Entry point for taskpane.html (inside PowerPoint) and dev.html (standalone browser).

import "./styles.css";
import { Engine } from "./engine/client";
import { loadWasmEngine } from "./engine/wasm";
import { detectLang, setLang, t } from "./i18n";
import { detectCapabilities, noOffice, type HostCapabilities } from "./office/capabilities";
import { App } from "./ui/app";
import { comBridge, comCapabilities } from "./office/combridge";

declare global {
  interface Window {
    /** Test/debug handle (dev.html screenshots, console). */
    inventaSketch?: App;
  }
}

interface OfficeInfo { caps: HostCapabilities; language?: string; background?: string; pageSize?: { w: number; h: number } }

async function initOffice(): Promise<OfficeInfo> {
  if (typeof Office === "undefined" || document.getElementById("app")?.dataset.host !== "office") return { caps: noOffice };
  const info = await Office.onReady();
  if (info.host !== Office.HostType.PowerPoint) return { caps: noOffice, language: Office.context?.displayLanguage };
  const caps = detectCapabilities(Office.context.requirements);
  const out: OfficeInfo = { caps, language: Office.context.displayLanguage, background: Office.context.officeTheme?.bodyBackgroundColor };
  if (caps.rotation) {
    // PowerPointApi 1.10: slide size in pt → page size in mm, so page and slide coordinates match.
    try {
      out.pageSize = await PowerPoint.run(async (ctx) => {
        const ps = ctx.presentation.pageSetup;
        ps.load("slideWidth,slideHeight");
        await ctx.sync();
        return { w: (ps.slideWidth * 25.4) / 72, h: (ps.slideHeight * 25.4) / 72 };
      });
    } catch {
      /* default 16:9 page */
    }
  }
  return out;
}

/** sidebar.html inside the COM add-in's WebView2: language, theme and slide size come as query parameters. */
function initCom(): OfficeInfo | undefined {
  const bridge = comBridge();
  if (!bridge || document.getElementById("app")?.dataset.host !== "com") return undefined;
  const q = new URLSearchParams(location.search);
  const w = Number(q.get("w")), h = Number(q.get("h"));
  return {
    caps: comCapabilities,
    language: q.get("lang") ?? undefined,
    background: q.get("theme") === "dark" ? "#262626" : undefined,
    pageSize: w > 0 && h > 0 ? { w, h } : undefined,
  };
}

async function boot(): Promise<void> {
  const root = document.getElementById("app")!;
  setLang(detectLang(navigator.language));
  try {
    const com = initCom();
    const [office, loaded] = await Promise.all([com ? Promise.resolve(com) : initOffice(), loadWasmEngine()]);
    const app = new App({
      engine: new Engine(loaded.backend),
      root,
      office: office.caps,
      officeLanguage: office.language,
      officeBackground: office.background,
      pageSize: office.pageSize,
      loadMillis: loaded.millis,
      comBridge: com ? comBridge() : undefined,
    });
    window.inventaSketch = app;
    if (office.caps.selection && !com) {
      Office.context.document.addHandlerAsync(Office.EventType.DocumentSelectionChanged, () => app.onOfficeSelectionChanged());
    }
  } catch (e) {
    console.error(e);
    root.innerHTML = "";
    const p = document.createElement("p");
    p.className = "isk-fatal";
    p.textContent = `${t("error.engine")} ${e instanceof Error ? e.message : String(e)}`;
    root.append(p);
  }
}

void boot();
