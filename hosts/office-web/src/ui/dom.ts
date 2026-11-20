// Tiny DOM helpers (no framework, keeps the bundle small).

export function h<K extends keyof HTMLElementTagNameMap>(
  tag: K,
  attrs: Record<string, string | number | boolean | undefined> = {},
  ...children: (Node | string | undefined | null | false)[]
): HTMLElementTagNameMap[K] {
  const el = document.createElement(tag);
  for (const [k, v] of Object.entries(attrs)) {
    if (v === undefined || v === false) continue;
    if (k === "class") el.className = String(v);
    else if (k === "html") el.innerHTML = String(v);
    else el.setAttribute(k, v === true ? "" : String(v));
  }
  for (const c of children) if (c !== undefined && c !== null && c !== false) el.append(c);
  return el;
}

export const SVGNS = "http://www.w3.org/2000/svg";

export function s<K extends keyof SVGElementTagNameMap>(tag: K, attrs: Record<string, string | number | undefined> = {}): SVGElementTagNameMap[K] {
  const el = document.createElementNS(SVGNS, tag);
  for (const [k, v] of Object.entries(attrs)) if (v !== undefined) el.setAttribute(k, String(v));
  return el;
}

/** Parses an SVG document string and returns its root's child nodes imported into this document. */
export function svgChildren(svg: string): Node[] {
  const doc = new DOMParser().parseFromString(svg, "image/svg+xml");
  return Array.from(doc.documentElement.childNodes).map((n) => document.importNode(n, true));
}

export function svgDataUrl(svg: string): string {
  return "data:image/svg+xml;charset=utf-8," + encodeURIComponent(svg);
}

/** viewBox "x y w h" of an SVG string (thumbnails are rendered in mm around the insertion point). */
export function viewBoxOf(svg: string): { x: number; y: number; w: number; h: number } | undefined {
  const m = /viewBox="([-\d.e]+) ([-\d.e]+) ([-\d.e]+) ([-\d.e]+)"/.exec(svg);
  return m ? { x: +m[1], y: +m[2], w: +m[3], h: +m[4] } : undefined;
}

export function clamp(v: number, lo: number, hi: number): number {
  return v < lo ? lo : v > hi ? hi : v;
}
