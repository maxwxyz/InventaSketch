# Host capability matrix

> Values: **verified** (tested, with reference), **partial**, **unsupported**, **not tested**. Never mark a cell
> verified without a reproducible test. German: [../../de/hosts/capability-matrix.md](../../de/hosts/capability-matrix.md).
> API facts come from [research/tools-and-host-apis.md](../research/tools-and-host-apis.md); they become
> "verified" only after an InventaSketch test on the real host.

| Capability | PowerPoint COM (Win) | PowerPoint web add-in (Win/Mac/Web) | LibreOffice (Win/Mac/Linux) |
|---|---|---|---|
| Native rectangles, ellipses, lines, text | verified (2026-11-20, PowerPoint 16.0.20527, smoke test) | not tested (PowerPointApi 1.4) | not tested (UNO shapes) |
| Native freeform / Bézier | verified (BuildFreeform, smoke test) | unsupported in Office.js → SVG picture or slide injection | not tested (PolyPolygonBezierShape) |
| Native arrowheads | verified (curved arrow, smoke test); native for triangle, open, stealth, diamond, oval (tick → open, half → triangle); native heads scale with the line weight, so on thin lines they are smaller than the engine's SVG heads | unsupported in Office.js → explicit geometry | not tested (LineStart/LineEnd) |
| Hollow UML heads (`triangleOpen`, `diamondOpen`) | partial: no msoArrowhead equivalent → lowered to a closed freeform without fill, line shortened to its base (draw plan unit-tested, not tested on the host) | partial: rotated Triangle/Diamond geometric shape without fill (PowerPointApi 1.10 rotation; unit-tested plan, not tested on the host) | not tested |
| Crow's-foot ends (`crowOne`, `crowOneOnly`, `crowZeroOne`, `crowMany`, `crowOneMany`, `crowZeroMany`) | partial: lines, open freeform foot and unfilled ovals; a circle interrupts the line (unit-tested plan, not tested on the host) | partial: lines and unfilled ellipses (unit-tested plan, not tested on the host) | not tested |
| Text underline | not tested (`Font.Underline`) | not tested (`ShapeFont.underline`, PowerPointApi 1.4; no drawn fallback needed) | not tested (CharUnderline) |
| Native pattern/hatch fill | partial: explicit hatch lines (verified); patterned fill not used | unsupported → explicit hatch lines | not tested (FillHatch) |
| Grouping | verified | not tested (PowerPointApi 1.8) | not tested (XShapeGrouper) |
| Rotation | not tested | not tested (PowerPointApi 1.10) | not tested |
| Shape tags / metadata | verified (ISK_ID, ISK_TYPE, ISK_MODEL written) | not tested (tags, 1.3) | not tested (shape name + document properties) |
| Selection read/write | not tested | not tested (1.5/1.6) | not tested |
| On-slide overlay, floating bar, pie menu | partial: insert at pointer, drag and resize with snapping, two-point tool, point editing, pie menu and fading floating bar verified 2026-11-20 | unsupported → sidebar stage | planned (UNO handlers + overlay layer) |
| Sidebar / task pane editor | verified 2026-11-20 (WebView2 task pane, live sync both ways) | in progress | planned (sidebar panel) |
| SVG insert | n/a | not tested (XmlSvg coercion; not on iPad) | not tested |
| COM precedence over web add-in | not tested (`EquivalentAddins`) | not tested | n/a |
| Light/dark auto detection | planned | in progress | planned |
| German/English auto detection | verified (Office UI LCID, German detected) | in progress | planned |

Engine-only capabilities (all hosts): SVG export **verified** by unit tests (`SvgContainsHatchAndArrowheadAndIsMonochrome`,
`DrawingTests`: every arrowhead, hollow heads, crow's-foot ends and `text-decoration` underline, also monochrome),
semantic snapping **verified** by unit tests (`SnappingTests`), JSON persistence **verified** (`PersistenceTests`).

Arrowhead lowering (decision): a head becomes a line-end attribute only where the host has a matching native line
end (`DrawPlanBuilder.HasNativeLineEnd`). PowerPoint has no hollow triangle, hollow diamond or crow's foot, so these
are always drawn as explicit native shapes with the same geometry as the SVG output, also in the COM add-in; one line
can carry a native start head and a lowered end head. With freeform shapes (COM) the lowered head is exact; without
(Office.js) triangles and diamonds become rotated geometric shapes, everything else lines and ellipses. Hollow heads
are unfilled and the line stops at their base, so no background colour is assumed. GDI+ thumbnails and the on-slide
preview draw every head from the same engine geometry.
