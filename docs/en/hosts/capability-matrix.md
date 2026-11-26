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
| On-slide overlay, floating bar, pie menu | partial: insert at pointer, drag and resize with snapping, two-point tool, point editing, pie menu and fading floating bar verified 2026-11-20 | unsupported → sidebar stage | partial: no overlay (UNO cannot draw into the view), so no floating bar or handles; the pie's eight places are the top of the context menu, verified 2026-11-26 (`ui_smoke.py --concept-only`, real right-click; LibreOffice hides inactive places), see the LibreOffice table below |
| Sidebar / task pane editor | verified 2026-11-20 (WebView2 task pane, live sync both ways) | in progress | partial: deck with Selection (views, every choice as a dropdown, the element's commands, select connected, insert here, build, turn), Values, Library and Calculator-and-code panels, verified 2026-11-26 (`ui_smoke.py --concept-only`, screenshots); free number/text parameters and line/fill style not editable yet |
| SVG insert | n/a | not tested (XmlSvg coercion; not on iPad) | not tested |
| COM precedence over web add-in | not tested (`EquivalentAddins`) | not tested | n/a |
| Light/dark auto detection | planned | in progress | planned |
| German/English auto detection | verified (Office UI LCID, German detected) | in progress | verified 2026-11-20 (`ooLocale` of the UI; German UI detected, German catalog names) |
| Insert box (search, fitting parts first, Ctrl+Space) | verified 2026-11-24 (scenario `ux-round`) | partial: implemented and checked in the browser dev page, not tested in PowerPoint on the web | partial: Library panel search (engine `search`, fitting parts first), verified 2026-11-25 (`integration_test.py`, `ui_smoke.py`); Ctrl+Space puts the cursor into that search field (verified 2026-11-25, real input); no insert box at the pointer (no overlay) |
| Context-only transforms (mirror/rotate only when they change the drawing) | verified 2026-11-24 (`ux-round`, bar ids) | partial: implemented, browser dev page | verified 2026-11-25 (selection panel, `integration_test.py`) |
| Multi-selection bar (one view for all, align, distribute, like the first, renumber, dimension) | verified 2026-11-24 (`ux-round`, `parts-list`, `building-services`) | partial: implemented, browser dev page | verified 2026-11-25 (`integration_test.py`, `ui_smoke.py`) (selection panel, one undo step) |
| Assembly view switch (gear trains axial ↔ stick, building services plan ↔ section ↔ schematic) | verified 2026-11-24 (`ux-round`, `building-services`) | partial: via the multi-selection bar, browser dev page | planned |
| Slide style: ISO 128 line group, accent colour (and black and white) for all elements, also new ones | verified 2026-11-25 (scenario `line-group`: 1.42 → 2.84 pt at 1 mm, a new element takes it; ribbon "Line weights", slide tag `ISK_STYLE`) | partial: `setLineGroup` choice in the top bar, browser dev page | not exposed (the engine setting is adopted from the slide model) |
| Colour by medium (slide-wide display mode: L1/L2/L3/N/PE per IEC 60445, DC, bus/signal, heating flow/return, water, hydraulic and process lines; legend) | verified 2026-11-26 (scenario `medium-names`: L1 brown, N blue, PE green-yellow, heating flow red / return blue, cabinet core unchanged, new wire takes it, legend, halo, Off restores; More ▸ Colour by medium, slide tag `ISK_STYLE` `colors=medium`) | not yet (engine `setColorBy`, `mediumColours`, `lineMedia` available; no UI) | verified 2026-11-26 (`integration_test.py --only drawing`: L1 brown in one undo step, a new wire takes the mode, legend as one unit inside the page, Off restores; Tools panel dropdown and menu *Colour by Medium*; the mode lives in the units' stored models, engine `insertLegend`) |
| Designations of lines and elements, end markers, "Same name", continuation markers across slides ("→ W3, slide 4") | verified 2026-11-26 (scenario `medium-names`: "→ W3" at the slide edge, "→ W3, slide 1" on slide 2 and "→ W3, slide 2" back on slide 1, end markers, Same name selects both W3 on the slide and reports slide 2; shape tag `ISK_REFS`) | not yet (generic actions arrive through the engine's action list; `designations`, `connected` mode `samename`, `setContinuations` available; no page map yet) | verified 2026-11-26 in Impress (`integration_test.py --only drawing`: "→ W3, Folie 2" on slide 1, "Same name on slide 2: W3" shows slide 2 with the wire selected; page map from the other pages' stored models, engine `designationsOf`); Draw: the same code with "page", not run; not in Calc |
| P2 modules: process & thermal, energy systems, robotics (insert, native shapes) | verified 2026-11-25 (scenario `p2-modules`) | through the engine catalog, not tested | through the engine catalog, not tested |
| Line crossings across modules hop (fluid, electrical, building services, process, energy) | partial: engine tests; `crossings` scenario (wires) green | through the engine | through the engine |
| Tool on a robot flange with "+", turned to the tool axis | verified 2026-11-25 (scenario `robot-tool`) | build "+" (port kind `Mount`), not tested | port list (`portMount`), not tested |
| Diagram layout of connected elements (layered, connectors redock) | verified 2026-11-25 (scenario `diagram-layout`, also align keeps connectors) | through the engine (`arrangeOptions`), not tested | through the engine (selection panel), not tested |
| Snap after dragging, Ctrl+Space insert, context menu | verified | partial (browser dev page) | verified 2026-11-25 (`ui_smoke.py`, real input; snapping after release, no guides while dragging); context menu with the eight places verified 2026-11-26 |
| Compatibility check of all slides with navigation to the finding | verified 2026-11-25 (scenario `compatibility-check`) | panel in the top bar for the page (checked in the browser dev page) and, in PowerPoint, across all slides (unit-tested against an Office.js stand-in, not yet run in PowerPoint) | dialog over every page with navigation (menu; `integration_test.py`, `ui_smoke.py`) |
| Variants: compare a slide with the previous one (added / changed / removed marked) | verified 2026-11-25 (scenario `variants`, Review group) | top bar button in PowerPoint (unit-tested against an Office.js stand-in, not yet run in PowerPoint) | menu command, checked 2026-11-25 (`integration_test.py`) |
| Import CSV (chart data), GeoJSON/GPX (map) | verified 2026-11-25 (scenario `import`) | import button (engine `importData`/`importGeo`), not tested in PowerPoint | not exposed |
| Chart parts: select, hide and style each curve, axis, grid … (also with the host's own formatting) | verified 2026-11-25 (scenario `chart-parts`: native recolour of one curve kept after a redraw, click selects the part, bar shows part actions, hide) | implemented 2026-11-25: a click on the selected chart selects the part (outline from engine `partOutline`), bar and pie show its actions, "Parts …" list; unit-tested (`partlogic.test.ts`), not yet run in the browser or PowerPoint | implemented 2026-11-25: menu "Chart Parts..." (dialog: parts, their actions, value field; command `parts?part=&action=` for automation), unit-tested rows, not yet run in LibreOffice |
| Connection "+" (cables, pipes, ducts at ports and line ends; PV, building services, electrical, fluid, process, machines) | verified 2026-11-25 (scenario `connect-plus`: PV string → DC cable → inverter) | engine `buildPorts` with `medium`, tooltips for connection ports and line ends; not run in the browser | port names in the build list; not run in LibreOffice |
| Sketch a curve into a chart (recognised or smoothed; key points, detail, drawing speed from time-stamped trail) | verified 2026-11-25 (scenario `chart-sketch`, real mouse; key points and detail not yet run in PowerPoint) | bar command and stage trail with pointer times (`[x, y, t]`, drawing speed hint) through engine `sketchCurve`; not run in the browser | not yet (needs a drawing interaction) |
| Chart from picture (clipboard or file) | verified 2026-11-25 (scenario `chart-picture`) | import button and Ctrl+V through engine `chartFromPicture`; not run in the browser | not yet (engine method available) |
| Chart data table editor | verified 2026-11-25 (scenario `chart-data-editor`) | inspector / one-line data input | one-line data input |
| Axis breaks in XY charts and figures; coordinate systems and function graphs as chart types | verified 2026-11-25 (scenario `chart-breaks-math`: break marks as parts, coordinate system and function graph native, Next function) | engine-drawn, not tested in PowerPoint on the web | engine-drawn, not tested in LibreOffice |
| Clean slides for sharing (charts normalised or randomised, texts, metadata) | verified 2026-11-25 (Review ▸ Clean slides …, scenario `clean-slides`) | engine `sanitize`, no UI yet | engine `sanitize`, no UI yet |
| Exports: LaTeX (TikZ), matplotlib, pgfplots, editable SVG | TikZ/matplotlib/pgfplots in Review ▸ Export data (engine exporters); not yet run with LaTeX/matplotlib | engine exporters; SVG export with named groups | engine exporters |
| Import SVG/DXF drawings as editable shapes | verified 2026-11-25 (scenario `vector-import`, Review ▸ Import) | import button (engine `importVector`), checked in the browser dev page | menu command, checked 2026-11-25 (`integration_test.py`) |
| Live coupling preview while dragging | partial: implemented 2026-11-24, checked by eye (blue ghost), no scenario assertion | partial: implemented 2026-11-25, browser dev page | unsupported (needs the overlay; no live drag events) |
| Motion mode (turn gear trains and mechanisms, drag self-built linkages) | verified (`gear-motion`, `linkage-build`) | partial: implemented 2026-11-25, browser dev page | partial: gear trains turn by a typed angle (`motionTurn`), verified 2026-11-25 (`integration_test.py`, `ui_smoke.py`); no dragging |
| Coupler curve and ghost positions of self-built linkages | verified 2026-11-24 (`linkage-extras`) | partial: both in the browser dev page | verified 2026-11-25 (selection panel, `integration_test.py`) |
| Parts list table following the parts | verified 2026-11-24 (`parts-list`) | not tested (engine redraws dependents) | not tested |

### Office web add-in: handles on the drawing (2026-11-26)

Parity with the COM add-in's overlay; details in [office-web.md](office-web.md) ("Handles on the drawing"). Tests:
`test/handlelogic.test.ts`, engine `WebHandleApiTests`; type check and production build.

| Capability | PowerPoint web add-in |
|---|---|
| Progressive disclosure (one primary "+", the rest fades in near the pointer, Shift shows all) | partial: implemented 2026-11-26 on the stage, unit-tested rules; not run in the browser |
| Lock badges (release / couple a link with one click) | partial: implemented 2026-11-26 (engine `links`, `toggleLink`, unit-tested); not run in the browser |
| Corner dots (click adds, drag bends, double-click removes), branch handles, join on drop | partial: implemented 2026-11-26 (engine `polylineHandles`, `insertCorner`, `removeCorner`, `branch`, `joinLines`, unit-tested); no junction icons, no "+" at free line ends, no split in the bar; not run in the browser |
| Insert between table rows/columns (`IInsertHandles`) | partial: "+" dots on the stage (engine `insertHandles`/`applyInsertHandle`); not run in the browser |
| Drag a selected chart curve (`byPart` grip) | partial: implemented 2026-11-26 through `moveHandle`; not run in the browser |
| Value card ↔ grip with live preview, value ranges, ⚠ badge | partial: implemented 2026-11-26 (`dragValue` preview, `values` `range`); not run in the browser |
| Calculation sheet (CSV, Markdown, LaTeX; copy, download, insert as a table) | partial: implemented 2026-11-26 (engine `calcSheet`, `insertCalcTable`, unit-tested); not run in the browser |

### LibreOffice: interaction concept (2026-11-26)

The concept of [interaction-ux.md](../design/interaction-ux.md) and the value UI of
[engineering-calculations.md](../design/engineering-calculations.md) in the LibreOffice extension; details in
[libreoffice.md](libreoffice.md) ("Interaction concept round"). Tests: `test_concept.py`, `integration_test.py --only
concept`, `ui_smoke.py --concept-only` (LibreOffice 26.8.2.2, Windows 11).

| Capability | LibreOffice |
|---|---|
| Choices as dropdowns (parameter choices, engine action choices, menus as "Label ▸"), no cycle buttons | verified 2026-11-26 (Selection panel and context menu submenus; dropdown driven in `ui_smoke.py`); options are named – no option pictograms or pictures (the extension has no pictogram set) |
| Eight fixed places (Text, Edit, Insert, Duplicate, Style, Delete, View, Arrange) | verified 2026-11-26 as the top of the context menu (real right-click, keyboard into View); current options checked natively; a place with nothing is hidden (LibreOffice hides inactive context-menu entries unless *Show inactive menu items* is on), not greyed |
| Values (rows, formulas, conflicts, checks, value level, values label) | verified 2026-11-26 (Values panel; level as document property `ISK_VALUES`); no ↔ grip and no ⚠ badge on the drawing (no overlay) |
| Calculator with units, code import (matplotlib, pgfplots, TikZ) | verified 2026-11-26 (panel "Drawing, calculator and code", menu with file dialog) |
| Select connected (net, moves along, following, loop, room walls), Ctrl+double-click | verified 2026-11-26 (Arrange ▸ Select connected, Selection panel list, real Ctrl+double-click); no fainter hover preview (the context menu reports no hover) |
| Selection look (soft glow along the outline) | partial, verified 2026-11-26: native glow effect on the unit's shapes, blurred shadow for ellipses (they ignore the glow), removed before save/copy/export/print; lies behind each shape (an element on top hides it) |
| Insert between table rows/columns (`IInsertHandles`) | verified 2026-11-26 as a Selection panel list "Insert here" (engine `insertHandles`/`applyInsertHandle`); no "+" dots on the drawing |
| Drag grips on values (`dragHandles`/`moveHandle`) | unsupported: no mouse motion events and no handles for extensions; typed values instead (Values panel, parts dialog) |
| Plausibility warnings with rule and source, "Mute" (Calc 6) | verified 2026-11-26 (`integration_test.py --only drawing`: warning shown, muted, unmuted; Values panel rows with *Mute*; document property `ISK_MUTED`, kept in .odp and .pptx) |
| "Show values in" preferred units (pressure, speed of rotation, energy, power) | verified 2026-11-26 (power in kW: values, values label redrawn in one undo step, calculation sheet; document property `ISK_UNITS`, kept in .odp and .pptx); speed of rotation offers the engine's "rev/s" instead of COM's "s⁻¹" |
| Calculation sheet (CSV, Markdown, LaTeX; copy, save, insert as a table) | verified 2026-11-26 (`integration_test.py --only drawing`: CSV file with byte order mark, Markdown copied and read back from the clipboard, LaTeX, table in one undo step; dialog from the Tools panel and the menu) |

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
