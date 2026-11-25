// SVG/PNG and data export of the selection or the slide (engine renders the SVG; the browser rasterises PNG).
//
// Scope (maintainer requirement, as in the COM add-in): always the selection – one or several elements – if there is
// one, otherwise the current slide. The editor selection follows the units selected on the slide (SlideSync loads all
// of them), so "selection" is the editor selection; without one, the shown slide's InventaSketch elements are read into a
// temporary engine session (as they are on the slide now). Without access to the slides (outside PowerPoint, or in the
// COM add-in's sidebar where the COM add-in owns the slide) the editor page is exported.

import type { Engine, Session } from "./engine/client";
import type { EditorState } from "./engine/types";
import { closeSession, slideSession, type SlideUnits } from "./office/slides";
import { elementCount } from "./ui/uxlogic";

export function download(name: string, blob: Blob): void {
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url;
  a.download = name;
  document.body.append(a);
  a.click();
  a.remove();
  setTimeout(() => URL.revokeObjectURL(url), 5000);
}

/** Pixel size for a raster export: `pxPerMm` (default ≈ 300 dpi), capped so the longer side stays ≤ maxPx. */
export function rasterSize(widthMm: number, heightMm: number, pxPerMm = 300 / 25.4, maxPx = 6000): { w: number; h: number } {
  let k = pxPerMm;
  const longest = Math.max(widthMm, heightMm) * k;
  if (longest > maxPx) k *= maxPx / longest;
  return { w: Math.max(1, Math.round(widthMm * k)), h: Math.max(1, Math.round(heightMm * k)) };
}

/** What an export covers: the selection (`count` elements), the current slide (number `slide`, 1-based) or the editor page. */
export type ExportScope = { kind: "selection"; count: number } | { kind: "slide"; count?: number; slide?: number } | { kind: "page" };

/** The scope known without reading the slide (for labels): the editor selection (a group counts as one element), else the slide (or the page). */
export function editorExportScope(state: Pick<EditorState, "selection" | "objects">, slideAccess: boolean): ExportScope {
  if (state.selection.length) return { kind: "selection", count: elementCount(state.objects, state.selection) };
  return slideAccess ? { kind: "slide" } : { kind: "page" };
}

/** UI label of the scope, as in the COM add-in: "Export – selection (3)", "Export – slide", "Export – page". */
export function exportScopeLabel(scope: ExportScope, t: (key: string, ...args: (string | number)[]) => string): string {
  return scope.kind === "selection" ? t("export.scope.selection", scope.count) : t(`export.scope.${scope.kind}`);
}

/** File name stem: "inventasketch-selection", "inventasketch-slide-3" or "inventasketch". */
export function exportFileStem(scope: ExportScope): string {
  if (scope.kind === "selection") return "inventasketch-selection";
  if (scope.kind === "slide") return scope.slide ? `inventasketch-slide-${scope.slide}` : "inventasketch-slide";
  return "inventasketch";
}

/** A resolved export: the session and ids to render or export (ids undefined = the whole page); dispose when done. */
export interface ExportSource {
  scope: ExportScope;
  session: Session;
  ids?: string[];
  monochrome: boolean;
  /** True when the scope holds nothing to export (a slide without InventaSketch elements). */
  empty: boolean;
  dispose(): void;
}

export interface ExportContext {
  engine: Engine;
  editor: Session;
  state: EditorState;
  /** The slides can be read (Office.js with tags and the selection API, not the COM sidebar). */
  slideAccess: boolean;
  /** Reads the shown slide's units (undefined when that is not possible). */
  readSlide: () => Promise<SlideUnits | undefined>;
  lang: string;
}

/** Resolves the export scope: the editor selection, else the shown slide's elements, else the editor page. */
export async function resolveExportSource(c: ExportContext): Promise<ExportSource> {
  const editor = (scope: ExportScope, ids?: string[]): ExportSource => ({
    scope, session: c.editor, ids, monochrome: !!c.state.monochrome, empty: !c.state.objects.length, dispose: () => undefined,
  });
  const scope = editorExportScope(c.state, c.slideAccess);
  if (scope.kind === "selection") return editor(scope, [...c.state.selection]);
  if (scope.kind === "page") return editor(scope);
  const slide = await c.readSlide();
  if (!slide) return editor({ kind: "page" });
  const slideScope: ExportScope = { kind: "slide", count: slide.units.length, slide: slide.index + 1 };
  if (!slide.units.length)
    return { scope: slideScope, session: c.editor, ids: [], monochrome: false, empty: true, dispose: () => undefined };
  const { session } = slideSession(c.engine, slide.units, { w: c.state.pageWidth, h: c.state.pageHeight }, c.lang);
  let monochrome = false;
  try {
    monochrome = !!session.state(false).monochrome;
  } catch {
    monochrome = false;
  }
  return { scope: slideScope, session, ids: undefined, monochrome, empty: false, dispose: () => closeSession(c.engine, session) };
}

export function exportSvg(source: ExportSource): void {
  const r = source.session.renderSvg({ ids: source.ids, monochrome: source.monochrome, background: "#ffffff", mode: "export" });
  download(`${exportFileStem(source.scope)}.svg`, new Blob([r.svg], { type: "image/svg+xml" }));
}

export async function exportPng(source: ExportSource): Promise<void> {
  const r = source.session.renderSvg({ ids: source.ids, monochrome: source.monochrome, background: "#ffffff" });
  const { w, h } = rasterSize(r.width, r.height);
  const img = new Image();
  img.decoding = "async";
  const url = URL.createObjectURL(new Blob([r.svg], { type: "image/svg+xml" }));
  try {
    img.src = url;
    await img.decode();
    const canvas = document.createElement("canvas");
    canvas.width = w;
    canvas.height = h;
    const ctx = canvas.getContext("2d")!;
    ctx.fillStyle = "#ffffff";
    ctx.fillRect(0, 0, w, h);
    ctx.drawImage(img, 0, 0, w, h);
    const blob = await new Promise<Blob | null>((res) => canvas.toBlob(res, "image/png"));
    if (!blob) throw new Error("PNG encoding failed");
    download(`${exportFileStem(source.scope)}.png`, blob);
  } finally {
    URL.revokeObjectURL(url);
  }
}
