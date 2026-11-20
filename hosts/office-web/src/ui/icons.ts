// Small stroke icons (24×24, currentColor), drawn for InventaSketch.

const paths: Record<string, string> = {
  undo: "M9 14L4 9l5-5M4 9h10a6 6 0 0 1 0 12h-3",
  redo: "M15 14l5-5-5-5M20 9H10a6 6 0 0 0 0 12h3",
  insert: "M3 5h18v12H3zM12 8v6M9 11h6M8 21h8",
  live: "M4.95 9.43A7.5 7.5 0 0 1 17.56 6.96M19.8 10.5L19.95 5.56 15.45 8.16ZM19.05 14.57A7.5 7.5 0 0 1 6.44 17.04M4.2 13.5L4.05 18.44 8.55 15.84Z",
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
  rotate: "M17.3 6.7A7.5 7.5 0 1 1 7.31 6.15M11.37 4.02L5.97 3.33 8.43 8.59Z",
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
  menuBack: "M19 12H5M11 6l-6 6 6 6",
  // Black and white: a circle, right half filled.
  mono: "M12 3a9 9 0 1 0 0 18 9 9 0 0 0 0-18zM12 3A9 9 0 0 1 12 21Z",
  // Abstract pictograms of the engine's IconIds (same shapes as the COM add-in's IconPainter). Centre lines are drawn
  // as short dashes because one path shares one stroke style.
  "view-schematic": "M12 4v16M8 4h8M8 20h8M3 12h4M10 12h4M17 12h4",
  "view-side": "M8 3h8v18H8zM3 12h3M10 12h4M18 12h3",
  "view-section": "M8 3h8v18H8zM9 8l6-4M9 12l6-4M9 16l6-4M9 20l6-4",
  "view-axial": "M12 4a8 8 0 1 0 0 16 8 8 0 0 0 0-16zM2 12h4M10 12h4M18 12h4M12 2v4M12 10v4M12 18v4",
  "view-teeth": "M12 6a6 6 0 1 0 0 12 6 6 0 0 0 0-12zM12 6V2.5M12 18v3.5M6 12H2.5M18 12h3.5M7.8 7.8L5.3 5.3M16.2 16.2l2.5 2.5M16.2 7.8l2.5-2.5M7.8 16.2l-2.5 2.5",
  "view-top": "M5 9h14v12H5zM12 2v5M9.5 4.5L12 7l2.5-2.5",
  "view-front": "M9 5h12v14H9zM2 12h5M4.5 9.5L7 12l-2.5 2.5",
  "view-rear": "M3 5h12v14H3zM22 12h-5M19.5 9.5L17 12l2.5 2.5",
  "view-iso": "M12 3l8 4.5v9L12 21l-8-4.5v-9zM12 12v9M12 12L4 7.5M12 12l8-4.5",
  "view-detailed": "M4 5h16v14H4zM7 9h10M7 12h7M7 15h9",
  "view-simplified": "M5 5h14v14H5zM5 5l14 14M19 5L5 19",
  "view-symbol": "M8 8h8v8H8zM2 12h6M16 12h6",
  "view-block": "M2 8h7v8H2zM15 8h7v8h-7zM9 12h6",
  "view-plan": "M4 20V4h16v9M4 20h9M13 20a7 7 0 0 1 7-7",
  "view-exploded": "M3 9h4v6H3zM10 7h4v10h-4zM17 9h4v6h-4z",
  "view-table": "M4 5h16v14H4zM4 9.5h16M4 14h16M10 9.5V19",
  "view-pin": "M12 21l-5.2-9A6 6 0 1 1 17.2 12zM12 7a2 2 0 1 0 0 4a2 2 0 1 0 0-4Z",
  "cmd-delete": "M6 6l12 12M18 6L6 18",
  "cmd-duplicate": "M4 4h11v11H4zM9 9h11v11H9z",
  "cmd-mirror-h": "M12 3v3M12 9v3M12 15v3M12 21v0M9 6v12H3zM15 6v12h6z",
  "cmd-mirror-v": "M3 12h3M9 12h3M15 12h3M21 12h0M6 9h12V3zM6 15h12v6z",
  "cmd-rotate": "M17.3 6.7A7.5 7.5 0 1 1 7.31 6.15M11.37 4.02L5.97 3.33 8.43 8.59Z",
  "cmd-rotate-ccw": "M6.7 6.7A7.5 7.5 0 1 0 16.69 6.15M12.63 4.02L18.03 3.33 15.57 8.59Z",
  "cmd-increase": "M12 5v14M5 12h14",
  "cmd-decrease": "M5 12h14",
  "cmd-swap": "M4 9h16M16 5l4 4-4 4M20 15H4M8 11l-4 4 4 4",
  "cmd-arrow-start": "M3 12h18M8 8l-5 4 5 4",
  "cmd-arrow-end": "M3 12h18M16 8l5 4-5 4",
  "cmd-text": "M5 5h14M12 5v15",
  "cmd-toggle": "M8 7h8a5 5 0 0 1 0 10H8A5 5 0 0 1 8 7zM16 10a2 2 0 1 0 0 4 2 2 0 0 0 0-4z",
  "cmd-cycle": "M4.95 9.43A7.5 7.5 0 0 1 17.56 6.96M19.8 10.5L19.95 5.56 15.45 8.16ZM19.05 14.57A7.5 7.5 0 0 1 6.44 17.04M4.2 13.5L4.05 18.44 8.55 15.84Z",
  "cmd-connect": "M5 15a3 3 0 1 0 0 6 3 3 0 0 0 0-6zM19 3a3 3 0 1 0 0 6 3 3 0 0 0 0-6zM5 18c7 0 7-12 14-12",
  "cmd-animate": "M7 4l12 8-12 8z",
  "cmd-medium": "M12 3c-3 5-6 8-6 11.5a6 6 0 0 0 12 0C18 11 15 8 12 3z",
  "cmd-actuation": "M8 4h8M12 4v7M5 11h14v9H5z",
};

export function icon(name: string, size = 18): string {
  const d = paths[name] ?? paths.type;
  // Sub-paths closed with an uppercase "Z" (arrowheads) are filled; everything else is a line drawing.
  const filled = d.split(/(?=M)/).filter((part) => /Z\s*$/.test(part)).join("");
  const fill = filled ? `<path d="${filled}" fill="currentColor" stroke-width="1"/>` : "";
  return `<svg class="isk-icon" width="${size}" height="${size}" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="${d}"/>${fill}</svg>`;
}

export function hasIcon(name: string): boolean {
  return name in paths;
}
