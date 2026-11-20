// Feature detection of the running Office host (requirement sets) and the matching engine draw capabilities.

import type { DrawCapabilities } from "../engine/types";

export interface HostCapabilities {
  /** Office.js present and initialised inside PowerPoint. */
  office: boolean;
  /** PowerPointApi 1.3: shape/presentation tags. */
  tags: boolean;
  /** PowerPointApi 1.4: addGeometricShape, addLine, addTextBox, fill and line format. */
  shapes: boolean;
  /** PowerPointApi 1.5: getSelectedShapes/Slides, setSelectedShapes. */
  selection: boolean;
  /** PowerPointApi 1.8: addGroup, z-order. */
  groups: boolean;
  /** PowerPointApi 1.10: shape.rotation. */
  rotation: boolean;
  /** ImageCoercion 1.2: setSelectedDataAsync with XmlSvg. */
  svg: boolean;
}

export const noOffice: HostCapabilities = { office: false, tags: false, shapes: false, selection: false, groups: false, rotation: false, svg: false };

type Requirements = { isSetSupported(name: string, version?: string): boolean };

export function detectCapabilities(req: Requirements | undefined): HostCapabilities {
  if (!req) return noOffice;
  const pp = (v: string) => safe(() => req.isSetSupported("PowerPointApi", v));
  return {
    office: true,
    tags: pp("1.3"),
    shapes: pp("1.4"),
    selection: pp("1.5"),
    groups: pp("1.8"),
    rotation: pp("1.10"),
    svg: safe(() => req.isSetSupported("ImageCoercion", "1.2")),
  };
}

function safe(f: () => boolean): boolean {
  try {
    return !!f();
  } catch {
    return false;
  }
}

/**
 * What the engine may emit as native ops for this host. Office.js has no freeform shapes, no arrowheads and no
 * pattern fills on shapes (research note B.1), so those are lowered or drawn as SVG by the engine.
 */
export function drawCapabilities(c: HostCapabilities): DrawCapabilities {
  return { freeform: false, arrowheads: false, patternFill: false, rotation: c.rotation, svg: c.svg, shapes: c.shapes };
}
