// Entry point for taskpane.html (inside PowerPoint) and dev.html (standalone browser).

import "./styles.css";
import { Engine } from "./engine/client";
import { loadWasmEngine } from "./engine/wasm";
import { detectLang, setLang, t } from "./i18n";
import { detectCapabilities, noOffice, type HostCapabilities } from "./office/capabilities";
import { App } from "./ui/app";

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

async function boot(): Promise<void> {
  const root = document.getElementById("app")!;
  setLang(detectLang(navigator.language));
  try {
    const [office, loaded] = await Promise.all([initOffice(), loadWasmEngine()]);
    const app = new App({
      engine: new Engine(loaded.backend),
      root,
      office: office.caps,
      officeLanguage: office.language,
      officeBackground: office.background,
      pageSize: office.pageSize,
      loadMillis: loaded.millis,
    });
    window.inventaSketch = app;
    if (office.caps.selection) {
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
