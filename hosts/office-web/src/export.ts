// SVG/PNG export of the page or the selection (engine renders the SVG; the browser rasterises PNG).

import type { Session } from "./engine/client";

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

export function exportSvg(session: Session, ids: string[] | undefined, monochrome: boolean): void {
  const r = session.renderSvg({ ids, monochrome, background: "#ffffff", mode: "export" });
  download(ids?.length ? "inventasketch-selection.svg" : "inventasketch.svg", new Blob([r.svg], { type: "image/svg+xml" }));
}

export async function exportPng(session: Session, ids: string[] | undefined, monochrome: boolean): Promise<void> {
  const r = session.renderSvg({ ids, monochrome, background: "#ffffff" });
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
    download(ids?.length ? "inventasketch-selection.png" : "inventasketch.png", blob);
  } finally {
    URL.revokeObjectURL(url);
  }
}
