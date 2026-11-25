# Host capability matrix

> Values: **verified** (tested, with reference), **partial**, **unsupported**, **not tested**. Never mark a cell
> verified without a reproducible test. German: [../../de/hosts/capability-matrix.md](../../de/hosts/capability-matrix.md).
> API facts come from [research/tools-and-host-apis.md](../research/tools-and-host-apis.md); they become
> "verified" only after an InventaSketch test on the real host.

| Capability | PowerPoint COM (Win) | PowerPoint web add-in (Win/Mac/Web) | LibreOffice (Win/Mac/Linux) |
|---|---|---|---|
| Native rectangles, ellipses, lines, text | verified (2026-11-20, PowerPoint 16.0.20527, smoke test) | not tested (PowerPointApi 1.4) | verified (2026-11-20, LibreOffice 26.8.2.2, Windows, `tests/integration_test.py`: Impress, Draw, Calc) |
| Native freeform / Bézier | verified (BuildFreeform, smoke test) | unsupported in Office.js → SVG picture or slide injection | verified (`OpenBezierShape`/`ClosedBezierShape`, `PolyLineShape`/`PolyPolygonShape`; curved arrow, gear teeth, numeral leader) |
| Native arrowheads | verified (curved arrow, smoke test); native for triangle, open, stealth, diamond, oval (tick → open, half → triangle); native heads scale with the line weight only, so the engine uses them only where they reach at least 65 % of its own head size (`DrawPlanBuilder.NativeHeadFits`) and otherwise draws the head as shapes — thin diagram lines and diamonds therefore get drawn heads that match the hollow ones | unsupported in Office.js → explicit geometry | verified (2026-11-20): line-end markers with the engine's head geometry for triangle, open, stealth, diamond, oval (tick → open, half → triangle), registered under OOXML-style names (`msArrowEnd 9 isk` …) so the .pptx export writes native PowerPoint arrowheads (triangle checked in the .pptx); the engine applies the same 65 % rule (`NativeHeadFits`) |
| Hollow UML heads (`triangleOpen`, `diamondOpen`) | partial: no msoArrowhead equivalent → lowered to a closed freeform without fill, line shortened to its base (verified 2026-11-20: UML generalisation, realisation, aggregation; hollow and filled diamonds equal in size) | partial: rotated Triangle/Diamond geometric shape without fill (PowerPointApi 1.10 rotation; unit-tested plan, not tested on the host) | verified 2026-11-20: lowered by the engine to a closed polygon without fill (UML generalisation) |
| Crow's-foot ends (`crowOne`, `crowOneOnly`, `crowZeroOne`, `crowMany`, `crowOneMany`, `crowZeroMany`) | partial: lines, open freeform foot and unfilled ovals; a circle interrupts the line (verified 2026-11-20 on ER connectors) | partial: lines and unfilled ellipses (unit-tested plan, not tested on the host) | not tested (drawn as shapes by the engine) |
| Text underline | verified 2026-11-20 (`Font.Underline`, ER primary key) | not tested (`ShapeFont.underline`, PowerPointApi 1.4; no drawn fallback needed) | not tested (`CharUnderline`, unit-tested mapping) |
| Native pattern/hatch fill | partial: explicit hatch lines (verified); patterned fill not used | unsupported → explicit hatch lines | verified 2026-11-20 (`FillHatch` single/cross with optional background colour; hatch lines are hairlines; .pptx export → preset pattern fill) |
| Grouping | verified | not tested (PowerPointApi 1.8) | verified (`XShapeGrouper`; groups carry no `UserDefinedAttributes`, so they sit on the first leaf shape) |
| Rotation | verified 2026-11-20 (snapped rotation re-renders from the model) | not tested (PowerPointApi 1.10) | partial: a native rotation of a unit is taken into the model on redraw (verified 2026-11-20, 90°); rotated plan ops via `RotateAngle` not tested on the host |
| Shape tags / metadata | verified (ISK_ID, ISK_TYPE, ISK_MODEL written) | not tested (tags, 1.3) | verified 2026-11-20: name `ISK:<type>:<id>` (.odp and .pptx), `UserDefinedAttributes` (.odp only) and document properties `ISK_MODEL_<id>` (.odp and .pptx); edit after reload from both formats |
| Selection read/write | verified 2026-11-20 (selection loads into the sidebar, pasted copies get new ids) | not tested (1.5/1.6) | verified 2026-11-20 (selected unit loaded for editing, new units selected after insertion) |
| On-slide overlay, floating bar, pie menu | partial: insert at pointer, drag and resize with snapping, two-point tool, point editing, pie menu and fading floating bar verified 2026-11-20 | unsupported → sidebar stage | planned (UNO handlers + overlay layer) |
| Sidebar / task pane editor | verified 2026-11-20 (WebView2 task pane, live sync both ways) | in progress | partial: sidebar deck opens with the library list, preview and the selected unit's views (verified 2026-11-20, Impress, screenshots); search filter unit-tested; no parameter/style editing yet |
| SVG insert | n/a | not tested (XmlSvg coercion; not on iPad) | not tested |
| COM precedence over web add-in | not tested (`EquivalentAddins`) | not tested | n/a |
| Light/dark auto detection | planned | in progress | planned |
| German/English auto detection | verified (Office UI LCID, German detected) | in progress | verified 2026-11-20 (`ooLocale` of the UI; German UI detected, German catalog names) |
| Insert box (search, fitting parts first, Ctrl+Space) | verified 2026-11-24 (scenario `ux-round`) | partial: implemented and checked in the browser dev page, not tested in PowerPoint on the web | partial: Library panel search (engine `search`, fitting parts first), verified 2026-11-25 (`integration_test.py`, `ui_smoke.py`); no Ctrl+Space (needs the overlay) |
| Context-only transforms (mirror/rotate only when they change the drawing) | verified 2026-11-24 (`ux-round`, bar ids) | partial: implemented, browser dev page | verified 2026-11-25 (selection panel, `integration_test.py`) |
| Multi-selection bar (one view for all, align, distribute, like the first, renumber, dimension) | verified 2026-11-24 (`ux-round`, `parts-list`, `building-services`) | partial: implemented, browser dev page | verified 2026-11-25 (`integration_test.py`, `ui_smoke.py`) (selection panel, one undo step) |
| Assembly view switch (gear trains axial ↔ stick, building services plan ↔ section ↔ schematic) | verified 2026-11-24 (`ux-round`, `building-services`) | partial: via the multi-selection bar, browser dev page | planned |
| Slide style: ISO 128 line group (and black and white) for all elements, also new ones | verified 2026-11-25 (scenario `line-group`: 1.42 → 2.84 pt at 1 mm, a new element takes it; ribbon "Line weights", slide tag `ISK_STYLE`) | partial: `setLineGroup` choice in the top bar, browser dev page | not exposed (the engine setting is adopted from the slide model) |
| P2 modules: process & thermal, energy systems, robotics (insert, native shapes) | verified 2026-11-25 (scenario `p2-modules`) | through the engine catalog, not tested | through the engine catalog, not tested |
| Line crossings across modules hop (fluid, electrical, building services, process, energy) | partial: engine tests; `crossings` scenario (wires) green | through the engine | through the engine |
| Tool on a robot flange with "+", turned to the tool axis | verified 2026-11-25 (scenario `robot-tool`) | build "+" (port kind `Mount`), not tested | port list (`portMount`), not tested |
| Live coupling preview while dragging | partial: implemented 2026-11-24, checked by eye (blue ghost), no scenario assertion | partial: implemented 2026-11-25, browser dev page | unsupported (needs the overlay; no live drag events) |
| Motion mode (turn gear trains and mechanisms, drag self-built linkages) | verified (`gear-motion`, `linkage-build`) | partial: implemented 2026-11-25, browser dev page | partial: gear trains turn by a typed angle (`motionTurn`), verified 2026-11-25 (`integration_test.py`, `ui_smoke.py`); no dragging |
| Coupler curve and ghost positions of self-built linkages | verified 2026-11-24 (`linkage-extras`) | partial: both in the browser dev page | verified 2026-11-25 (selection panel, `integration_test.py`) |
| Parts list table following the parts | verified 2026-11-24 (`parts-list`) | not tested (engine redraws dependents) | not tested |

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
