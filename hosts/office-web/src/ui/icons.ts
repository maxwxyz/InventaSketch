// Small stroke icons (24×24, currentColor), drawn for InventaSketch.

const paths: Record<string, string> = {
  undo: "M9 14L4 9l5-5M4 9h10a6 6 0 0 1 0 12h-3",
  redo: "M15 14l5-5-5-5M20 9H10a6 6 0 0 0 0 12h3",
  insert: "M3 5h18v12H3zM12 8v6M9 11h6M8 21h8",
  live: "M4 12a8 8 0 0 1 14-5.3M20 12a8 8 0 0 1-14 5.3M18 3v4h-4M6 21v-4h4",
  load: "M3 5h18v12H3zM12 14V8M9 11l3 3 3-3M8 21h8",
  svg: "M12 3v12M7 10l5 5 5-5M4 19h16",
  png: "M4 5h16v14H4zM4 15l5-5 4 4 3-3 4 4M15 9h.01",
  settings: "M12 8.5a3.5 3.5 0 1 0 0 7 3.5 3.5 0 0 0 0-7zM19 12l2-1.2-2-3.5-2.3.6-1.6-1V4.5h-4.2v2.4l-1.6 1-2.3-.6-2 3.5L5 12l-2 1.2 2 3.5 2.3-.6 1.6 1v2.4h4.2v-2.4l1.6-1 2.3.6 2-3.5z",
  fit: "M4 9V4h5M20 9V4h-5M4 15v5h5M20 15v5h-5M8 8h8v8H8z",
  zoomIn: "M11 4a7 7 0 1 0 0 14 7 7 0 0 0 0-14zM20 20l-4-4M8 11h6M11 8v6",
  zoomOut: "M11 4a7 7 0 1 0 0 14 7 7 0 0 0 0-14zM20 20l-4-4M8 11h6",
  search: "M10.5 4a6.5 6.5 0 1 0 0 13 6.5 6.5 0 0 0 0-13zM20 20l-4.8-4.8",
  star: "M12 3.5l2.6 5.3 5.9.9-4.3 4.1 1 5.8-5.2-2.7-5.2 2.7 1-5.8-4.3-4.1 5.9-.9z",
  delete: "M4 7h16M10 11v6M14 11v6M6 7l1 13h10l1-13M9 7V4h6v3",
  duplicate: "M8 8h12v12H8zM4 16V4h12",
  rotate: "M20 11a8 8 0 1 1-2.3-5.7M20 4v5h-5",
  flipH: "M12 3v18M9 6L3 18h6zM15 6l6 12h-6z",
  flipV: "M3 12h18M6 9L18 3v6zM6 15l12 6v-6z",
  front: "M9 9h11v11H9zM4 4h11v3M4 4v11h3",
  back: "M4 4h11v11H4zM9 20h11V9h-3",
  group: "M3 3h6v6H3zM15 15h6v6h-6zM9 6h6v3M18 9v6M3 13v8h8",
  ungroup: "M3 3h7v7H3zM14 14h7v7h-7z",
  numeral: "M9 4L7 20M17 4l-2 16M4 9h16M3 15h16",
  view: "M2 12s3.6-7 10-7 10 7 10 7-3.6 7-10 7S2 12 2 12zM12 9a3 3 0 1 0 0 6 3 3 0 0 0 0-6z",
  hatch: "M4 4h16v16H4zM4 12l8-8M4 20L20 4M12 20l8-8",
  arrow: "M4 12h15M14 7l5 5-5 5",
  dash: "M3 12h4M10 12h4M17 12h4",
  plus: "M12 5v14M5 12h14",
  style: "M12 3a9 9 0 0 0 0 18c1.5 0 2-1 2-2s-1-1.5-1-2.5 1-1.5 2-1.5h2a4 4 0 0 0 4-4c0-4.4-4-8-9-8zM7.5 11h.01M10 7h.01M15 7h.01",
  close: "M6 6l12 12M18 6L6 18",
  library: "M4 4h6v6H4zM14 4h6v6h-6zM4 14h6v6H4zM14 14h6v6h-6z",
  inspector: "M4 6h10M18 6h2M4 12h4M12 12h8M4 18h12M20 18h0M16 4v4M10 10v4M18 16v4",
  more: "M5 12h.01M12 12h.01M19 12h.01",
  type: "M12 3l2.5 6.5L21 12l-6.5 2.5L12 21l-2.5-6.5L3 12l6.5-2.5z",
};

export function icon(name: string, size = 18): string {
  const d = paths[name] ?? paths.type;
  return `<svg class="isk-icon" width="${size}" height="${size}" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="${d}"/></svg>`;
}

export function hasIcon(name: string): boolean {
  return name in paths;
}
