# Host capability matrix

> Values: **verified** (tested, with reference), **partial**, **unsupported**, **not tested**. Never mark a cell
> verified without a reproducible test. German: [../../de/hosts/capability-matrix.md](../../de/hosts/capability-matrix.md).
> API facts come from [research/tools-and-host-apis.md](../research/tools-and-host-apis.md); they become
> "verified" only after an InventaSketch test on the real host.

| Capability | PowerPoint COM (Win) | PowerPoint web add-in (Win/Mac/Web) | LibreOffice (Win/Mac/Linux) |
|---|---|---|---|
| Native rectangles, ellipses, lines, text | not tested (API available) | not tested (PowerPointApi 1.4) | not tested (UNO shapes) |
| Native freeform / Bézier | not tested (BuildFreeform) | unsupported in Office.js → SVG picture or slide injection | not tested (PolyPolygonBezierShape) |
| Native arrowheads | not tested | unsupported in Office.js → explicit geometry | not tested (LineStart/LineEnd) |
| Native pattern/hatch fill | not tested (patterned fill) | unsupported → explicit hatch lines | not tested (FillHatch) |
| Grouping | not tested | not tested (PowerPointApi 1.8) | not tested (XShapeGrouper) |
| Rotation | not tested | not tested (PowerPointApi 1.10) | not tested |
| Shape tags / metadata | not tested (Tags) | not tested (tags, 1.3) | not tested (shape name + document properties) |
| Selection read/write | not tested | not tested (1.5/1.6) | not tested |
| On-slide overlay, floating bar, pie menu | planned (hooks + layered window) | unsupported → sidebar stage | planned (UNO handlers + overlay layer) |
| Sidebar / task pane editor | planned | in progress | planned (sidebar panel) |
| SVG insert | n/a | not tested (XmlSvg coercion; not on iPad) | not tested |
| COM precedence over web add-in | not tested (`EquivalentAddins`) | not tested | n/a |
| Light/dark auto detection | planned | in progress | planned |
| German/English auto detection | planned | in progress | planned |

Engine-only capabilities (all hosts): SVG export **verified** by unit tests (`SvgContainsHatchAndArrowheadAndIsMonochrome`),
semantic snapping **verified** by unit tests (`SnappingTests`), JSON persistence **verified** (`PersistenceTests`).
