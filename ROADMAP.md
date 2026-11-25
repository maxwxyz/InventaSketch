# Roadmap

> Status values: **shipped** (tested and documented), **in progress**, **planned**, **proposed**, **blocked**.
> Detailed scope per module: [specification](docs/en/product/specification.md). German: [docs/de/ROADMAP.md](docs/de/ROADMAP.md).

## Phase 0 – Foundation (in progress)

| Item | Outcome | Status |
|---|---|---|
| Research | Drawing rules of the offices (DPMA/EPO/PCT/USPTO), tool landscape, host APIs | shipped (docs/en/research) |
| Repository & governance | README EN/DE, licence, ADRs, contribution rules, CI | in progress |
| Engine core | Model, geometry, primitives, snapping, hatching, edits/undo, JSON, SVG | in progress (26 tests) |
| Module contract | `IModule`, `ObjectType`, views, params, anchors, actions, library | in progress |

## Phase 1 – Quick Sketch MVP

| Item | Outcome | Status |
|---|---|---|
| Annotation module | Arrows, lead lines, dimensions, text, hatching, brackets, mechanics symbols, flowchart starters | in progress |
| Mechanical module | Gears (spur/internal/bevel/worm/rack/planetary/differential), shafts, bearings, couplings, fasteners, springs; stick/side/section/axial/teeth views | in progress |
| Figures module | Numerals, numbering scheme, reference sign list, office profiles, preflight, monochrome export | in progress |
| Engine API | JSON facade, sessions, hit test, snapping, context actions, host draw plans | in progress |
| Web add-in | WebAssembly engine, sidebar preview editor, library, inspector, pie menu, floating bar, native shape writer | in progress |

**Exit criteria:** two-stage gearbox and a technical figure can be created without numeric input in the sidebar editor,
inserted into PowerPoint (web) and exported as SVG; all tests green.

## Phase 2 – PowerPoint COM add-in (Windows)

Per-user registration without admin; ribbon; native shapes via COM (freeform, arrowheads, pattern fill, connectors);
on-slide interaction (input hooks + layered overlay window): insert at pointer, snapping guides, floating bar,
pie menu; sidebar task pane; metadata in tags + document properties; COM precedence verified. Status: **largely done** (2026-11-20, all checked in PowerPoint 16.0): native insertion, insert at
pointer, semantic snapping (move, resize about the centre, rotate), two-point tool, point editing and box selection of
points, anchoring, pie menu (8 entries, Insert/Type/Label/More) and floating bar with abstract icons, text editing,
Slidemax-like ribbon (Structure, Tools, Annotate, Colour, Text, Layout, Elements, Extras), native formatting kept in the
model, black and white, data exports, sidebar editor with live sync, installer package. Dark Office: bar, pie menu and pictograms
rendered and checked in both themes (2026-11-21). Open: COM precedence over the web add-in (`EquivalentAddins`) not
tested (needs the web add-in installed with the maintainer's certificate).

## Phase 3 – LibreOffice extension

Python-UNO `.oxt` + .NET engine process; Impress/Draw/Calc; native shapes and hatches; sidebar panel; on-canvas
interaction; Windows/Linux/macOS packages. Status: **in progress** (phase 1: insertion of native shapes from the
engine process; ported from Slidemax's LibreOffice host). Model exchange with the PowerPoint COM add-in via the
document mirror `ISK_MODEL_<id>`/`ISK_BOUNDS_<id>` verified in both directions 2026-11-20 (.pptx); units of the web
add-in (presentation tags) are not exchanged yet.

## Phase 4 – Kinematics and expanded mechanics

2D constraint solver, linkages, joint limits, drag-to-simulate, ghosting, animation as slide sequence; technical
mechanics module; multi-view links. Status: **in progress** — kinematics module shipped 2026-11-20 (joints, links,
linkages with closed-loop solution, ghosts, paths, DOF); animation as slide sequence with Morph done 2026-11-20 (COM); open: interactive drag-to-simulate, animation in the web add-in.

## Phase 5 – Electrical, electronics, electric machines

Symbols, wires with pins/junctions, nets, buses, ERC basics, house installation, PCB illustration, machine
cross-sections (PMSM, EESM, ASM, SRM, axial/radial flux). Status: **largely done** — electrical and machines modules
shipped 2026-11-20 incl. netlist export and a basic rule check (open pins, open wire ends, shorts).

## Phase 6 – Systems and domain expansion

Automotive/ADAS/HMI, architecture/floor plans, UML/ER/flow, maps and routes, charts; further modules on demand
(fluid power, thermodynamics, robotics, chemistry, …). Status: **in progress** — automotive/ADAS/HMI, architecture and
diagrams, maps and routes (15 types, route stop export) and fluid power (36 types, ISO 1219-style, connection list
export) shipped 2026-11-20; charts, thermodynamics, robotics, chemistry
proposed.

## Phase 7 – Advanced platform

Variants and diff, multi-view synchronisation, image contour recognition and hatching of imported pictures,
PDF/TIFF export profiles, user libraries, automation. Status: **proposed**.

Domain exports by selection/type (first exporters shipped 2026-11-20: BOM CSV, connection graph Mermaid/GraphML,
netlist CSV, electrical rule check report, reference sign list; next): gear and gearbox topology data (REXS where feasible), kinematic chains,
netlists and connection lists for schematics and E/E wiring (KiCad/SPICE), ratsnest/airwires, harness lists, BOM
(CSV/Excel), reference sign lists, diagram structures (GraphML/Mermaid) — as module export extension points.

## Plan 2026-11-24: UI/UX concept and plan review

Maintainer feedback 2026-11-24: the insert "+" that appends thumbnails to the bar is no good UI and does not work for
every element; mirror/rotate are shown even where they change nothing; a multi-selection only gets PowerPoint's native
format; whole assemblies (gearboxes, building services) should switch between top, side and front view with the
mechanism kept. Order: implement concept and findings in COM first, then web, then LibreOffice.

### UX rules (binding for every host)

1. **Context only.** A bar button appears only when it does something for this element: views only with more than one
   view, mirror/rotate only when the drawing is not symmetric that way (`Symmetry`, computed from the drawn geometry incl.
   arrowheads), explode only for explodable parts, motion only for parts that can move, F only for linkages.
2. **Small, fixed order, icons first.** Views | choices (line type, heads, style) | up to five element actions | build,
   insert, assembly, motion, explode | transforms | duplicate, delete. Pictograms with tooltips (shortcut in the
   tooltip), text only for values that need it (1:50, F = 1, switches without a telling icon). The bar never grows with
   content (no thumbnail rows).
3. **One insert flow.** Insert button, pie menu and Ctrl+Space open the same insert box at the pointer: parts that fit
   the selection first (placed where they fit), then recent items; typing searches the whole library (both languages,
   umlaut folding, the selection's view preferred). A build "+" on the canvas opens the same box at the "+" with the
   parts that dock there first.
4. **Multi-selection has its own bar.** One view for all, centres on a row/column, even spacing, renumber reference
   numerals – each only when it changes something (`Arrange`); PowerPoint's native format stays available.
5. **Assemblies are first-class.** "Select assembly" selects every coupled part; switching the view of a selected
   assembly keeps meshes, shafts and centre distances (`AssemblyViews`); a part keeps its pitch circle in every view.
6. **Show the result while dragging.** Coupled neighbours (meshing gears, belts, shafts following a motor) are previewed
   in a second colour while dragging or resizing, before release.
7. **Same gestures everywhere.** Handles: corners/edges resize, round handle rotates, "+" on ends and ports builds on,
   double-click edits text, Alt suspends snapping, Shift steps 15°, Esc cancels; keys R, X, Y, Ctrl+D, Shift+F10.
8. **Little text, two languages.** Every label and tooltip bilingual; tooltips say what happens, not how it works.

### Review of the plan (2026-11-24)

- Done in COM since 2026-11-21: line language, build handles, linkages and gear trains built from parts and moved live,
  explode of predefined mechanisms (except cam), insert by typing, live coupling preview, context-only bar,
  multi-selection bar, assembly view switch for gear trains, PowerPoint UI scenarios (`tests/powerpoint-com`).
- Done 2026-11-24 (engine, all hosts via the library): charts module (`chart.*`, 25 items, docs/en/modules/charts.md)
  and the missing mechanical elements (screw drives with nut on the spindle, freewheel, torque converter, park lock, CVT,
  strain wave and cycloidal drives, gearbox housing with bearing seats, oil sump; docs/en/modules/mechanical-drives.md).
  Open there: efficiency maps from measured grids, polar/area charts and histograms, a user secondary y-axis; "insert
  around the selection" for housings.
- Done since in COM: bevel pairs in coupling, exploding schematic planetary sets (through the axial view), snap labels,
  Tab through assemblies, format painter for semantics, coupler curves of self-built chains, Ctrl+Space without a
  selection, build "+" and "make it" in the insert box.
- Done since in COM: exploding cam mechanisms into a cam disc part and a follower slider (2026-11-25);
  differentials explode into a case part, side and spider gears since 2026-11-25, assembly view switch for building services (plan ↔ section ↔ schematic). Ghost
  positions and coupler curves of self-built chains are done (2026-11-24); explicit joint limits are done (stops on fixed pivots, 2026-11-25).
- Web: brought to the COM state of the UI/UX round on 2026-11-24 (context-only transforms, insert box with Ctrl+Space,
  fitting parts placed in their spot, multi-selection bar, select assembly, snap labels, coupler curve, F = n and
  explode; engine methods `search`, `transformEffect`, `arrangeOptions`/`arrange`, `assembly`, `trace`/`drawTrace`,
  `insertFitting`, `explode`, `dof`). Not yet verified inside PowerPoint on the web (needs the dev certificate on the
  maintainer's machine). Since then also on the web: build "+" ports on the stage with shaft stretching (preview from
  `buildStretchPreview`), live coupling preview, ghost positions, dimension between two parts, motion mode (turn gear
  trains and predefined mechanisms, drag self-built linkages; engine `motionInfo`/`motionPreview`/`motionTurn`/
  `motionDrag`, the drag solves on from its last step) and the library search ranked by the engine (2026-11-24/25).
- LibreOffice (2026-11-25): the same engine API in the sidebar deck – Library panel with the engine search (fitting
  parts first, recent items), selection panel with context-only buttons (view list, mirror/rotate only with effect,
  build ports, turn a gear train by a typed angle, coupler curve, ghost positions, select assembly, explode) and the
  selection-wide operations of `arrangeOptions`; edits run in a page session over all units of the page, one undo
  step (docs/en/hosts/libreoffice.md). Not possible without an on-canvas overlay: dragging with snapping, live coupling
  preview, build "+" on the canvas, motion by dragging, pie menu, Ctrl+Space.

### Review at the end of 2026-11-24 and next steps

**Done today:** the UX rules above are implemented in COM (verified with 16 PowerPoint UI scenarios in
`tests/powerpoint-com`, all green except where noted in the run logs) and in the web host (unit tests and browser
check). New modules: charts, building services; new mechanical elements; parts list; dimension between parts; ghost
positions and coupler curves; explode through another view; bevel pairs couple; insert box everywhere; compact bar
(one view chip for more than three views, motion only where it shows something); search & insert in the ribbon.

**Plan review – what changes in the plan:**
- *Assemblies are the unit of work.* View switch, select assembly, explode, parts list and the multi-selection bar
  all work on assemblies; the next features should too (variants/states, animation as slides for gear trains).
- *One insert flow everywhere* replaced the bar palettes; the same box should serve the sidebar library search.
- *Verification:* every user-visible COM feature gets a scenario; run the whole suite before merging larger changes
  (about 35 minutes, mouse busy).

**Next, in this order:**
1. Web: check in PowerPoint on the web once the maintainer has installed the dev certificate (all COM features of the
   UI/UX round incl. motion mode are implemented).
2. LibreOffice: done in the sidebar (2026-11-25); next an on-canvas overlay (drag snapping, "+", Ctrl+Space, pie menu).
3. COM domain gaps: none left from the list (done 2026-11-25: cam disc part with followers riding on it – turning the disc
   moves the follower slider and its chain – and cam mechanisms explode into pivot, disc and follower; Ravigneaux
   sets (planet pairs on a carrier with paired pins, explode, Willis ratios); joint limits as stops on fixed pivots; bevel pairs in the gear train speeds by their teeth (they only have
   side views, so they do not visibly turn); the exploded differential works as one (gears loose in the case, Willis
   with the case: straight on both sides turn with the case, one side held → the other turns twice as fast); the
   `elevation`/`section` view alias for walls with building services).
4. P2 modules: process & thermal (35 types), energy systems (30), robotics (25) done 2026-11-25 (engine, all hosts
   through the catalog; module docs docs/en/modules/process.md, energy.md, robotics.md); slide-wide style: ISO 128 line
   groups and black and white as a slide style for all elements, new ones included (done 2026-11-25 in COM and web);
   open: colour accents per module.
5. P3: variants and states, diagram auto-layout, figure workflow panel, import (SVG/DXF, GeoJSON/GPX, CSV).

### Review at the end of 2026-11-25 and next steps

**Done today:** LibreOffice UI/UX round (sidebar: engine search, context-only selection panel, multi-selection
operations, page session with one undo step); the COM domain list of item 3 (cam disc part and cam mechanisms that
explode, Ravigneaux sets, joint limits, bevel pairs and the differential in the gear train speeds, walls switching
along with installations); three P2 modules built in parallel by agents with file ownership – process & thermal (35
types), energy systems (30), robotics (25) –, registered in the engine and checked in PowerPoint (scenario
`p2-modules`); ISO 128 line groups as a slide style in COM and web (scenario `line-group`).

**Found by verification (and fixed):** the library search let a name prefix ("Lagertank") outrank parts fitting the
selection – fitting parts now always lead; the differential case locked its gears in the gear train speeds – it now acts
as a carrier; the native draw plans rendered objects on their own and ignored the line group – `Registry.RenderLocal`
is the one rendering path now; energy single-line symbols sat off-centre so cables jogged after a view switch;
PowerPoint scenarios read the bar from lagging log copies – the add-in now reports its bar ids directly.

**Plan review – what changes in the plan:**
- *Two rendering paths are a trap.* Every feature that changes how elements look gets a draw-plan test (the path COM and
  web use for native shapes), not only an SVG/`Registry.Render` test.
- *Parallel module agents work* when the coordinator owns the solution, the engine catalog and the review: every
  gallery is looked at before merging (that is how the cable jogs were found), and every new module gets one
  PowerPoint insert scenario.
- *Cross-module consistency is the next quality step:* lines of fluid power, building services, process and energy do
  not hop over each other yet; robot tools dock without the flange orientation; colour accents per module are open.

**Next, in this order:**
1. Web: check in PowerPoint on the web – needs the maintainer's dev certificate (`npm run certs` in
   `hosts/office-web`) or an uploaded webspace package.
2. Cross-module consistency: one crossing/hop rule for all line families and mounts with a direction (robot flange →
   tool, placed turned with "+") done 2026-11-25 (also: the render context carries the registry, which ended
   order-dependent tests); tools follow joint changes and take the flange size; one accent colour for all modules as part of the slide style
   (all done 2026-11-25).
3. LibreOffice: on-canvas interaction done 2026-11-25 as far as UNO allows – snapping after a drag (engine
   `snapMove`, one undo step), Ctrl+Space into the library search, a context menu with the selection's actions; checked
   in a real LibreOffice with real mouse and keyboard input (`ui_smoke.py` 19, `integration_test.py` 70 checks). Not
   possible without drawing into the view: guides and labels while dragging, live coupling preview, "+" handles, pie
   menu.
4. P3: diagram auto-layout done 2026-11-25 (layered, multi-selection, all hosts through the engine; COM checked in
   PowerPoint); import of CSV (chart data) and GeoJSON/GPX (map routes and markers) done 2026-11-25 (COM Extras menu, web import
   button; checked in PowerPoint); compatibility check of all slides with navigation to the finding (COM; engine `preflight` for web and
   LibreOffice) done 2026-11-25; variants and states as a slide comparison (added/changed/removed marked; COM Extras menu,
   engine `diff`) done 2026-11-25; compatibility check in web (panel for the page) and LibreOffice (dialog over every
   page with navigation) done 2026-11-25; slide comparison in LibreOffice done 2026-11-25; open: the slide comparison
   and the check across all slides in the web add-in (both need the other slides' units through Office.js; engine `diff`
   is ready), SVG/DXF import (vector drawings as editable shapes – a larger piece of its own).

### Review at the end of the P3 round (2026-11-25) and next steps

**Done in this round:** variants as a slide comparison (Core `Variants`, engine `diff`; COM Extras menu, scenario
`variants`; LibreOffice menu, integration check), the compatibility check in all three hosts (COM window over all
slides, web panel for the page, LibreOffice dialog over all pages with keyboard navigation, checked with real input in
`ui_smoke.py`), diagram auto-layout, CSV/GeoJSON/GPX import. Test totals at the end: .NET 1,698 (18 test projects), web 207, LibreOffice
unit 110, LibreOffice integration 77, LibreOffice UI smoke 23, PowerPoint scenarios green one by one.

**Found by verification (and fixed):** a new LibreOffice command is silently ignored unless it is in the protocol
handler's command list; LibreOffice renames duplicated shapes whose names end in digits, which would have cut the unit
id out of the name in .pptx – names are now restored from the attributes; the photographs rule named only the DPMA
although it applies to every office; the first compatibility finding of a page has no element, so navigation must handle
page-level findings.

**Plan review – what changes in the plan:**
- *Same command, same name, same place in every host.* "Compatibility (drawing rules)" and "Compare with the previous
  slide (variants)" now exist under the same names in COM and LibreOffice. In COM both sit in the Extras menu; with
  parts list and data export they form a "Review" family – a ribbon group *Review* (Compatibility, Compare, Parts list,
  Export data) would make them findable without the menu. Proposed for the maintainer's decision, not built.
- *Every host command gets a headless path and a visible path.* The LibreOffice dialog logs its result when no window
  is visible; that made the integration test possible, and `ui_smoke.py` covers the real dialog.
- *Features that need other slides (web)* wait for the Office.js check in PowerPoint; building them blind would break
  the "no fake functionality" rule.

**Next, in this order:**
1. Web in PowerPoint (blocked on the maintainer: dev certificate via `npm run certs` in `hosts/office-web`, or an
   uploaded webspace package). The web check across all slides, the slide comparison and the corner radius are built
   (2026-11-25, unit-tested against an Office.js stand-in) and wait for that run.
2. SVG/DXF import – done 2026-11-25 (Core `VectorImport`, engine `importVector`; COM Review ▸ Import, web import button,
   LibreOffice menu; scenario `vector-import`, LibreOffice integration check).
3. Ribbon group *Review* in COM – done 2026-11-25 (Compatibility, Compare, Import, Export data).
4. Open details: a section line per wall; LibreOffice on-canvas limits (guides while dragging need drawing into the
   view); real webspace and installer runs; the web host's corner radius (PowerPointApi 1.10 adjustments) is built but not
   yet run in PowerPoint. Curves and intersections keep the road lane
   width for any lane count since 2026-11-25 (they grow with their lanes).

**Maintainer test 2026-11-25 – found and fixed:** the insert box failed the second time (it disposed shared thumbnails);
"+" on scaled elements placed library-sized parts (now the scale is passed on, checked for every element: 861 build
placements and 169 quick adds, `BuildAllElementsTests`); a road continued with lane 1 at the "+" of lane 2, and curves,
the roundabout ring and junction arms offered "+" at chord joints and inner ends; road sizes did not fit a slide (an
intersection was 160 mm) and the roundabout's arm lanes were narrower than the roads – top views now share the factor
0.6 and one lane width; rounded rectangles in COM ignored their radius.

**Library review (2026-11-25, maintainer request):** every element of every module checked by tests
(`LibraryAuditTests`: insert, render inside the box, every parameter value, every view, every context action, scaling,
engine round trip with undo/redo – 1,273 elements, 21,341 checks, 0 failures; `CouplingAuditTests`: a line docked on each
of 2,193 ports follows move, rotation and scaling; `BuildAllElementsTests`: "+" on scaled elements). Overlaps cut:
`LibraryItem.Alternative` keeps a duplicate out of the library while its better alternative is registered (modules stay
usable on their own, existing slides keep rendering); names that meant different things were made distinct; a test keeps
any two modules from offering the same name again. Still open from the review: the order of the elements within each
module (most used first), cross-module coupling beyond the generic port docking, a data table editor for charts in COM.

### Charts v2 – matplotlib, but interactive (maintainer request 2026-11-25)

Plan: [docs/en/design/charts-v2.md](docs/en/design/charts-v2.md) – select, switch and style every part of a chart (also
with PowerPoint's own tools), stacked panels with a shared axis and phase lines, value lines, shaded and hatched regions,
dimension arrows and numerals for technical figures, curves from functions and hand sketches, axis breaks, matplotlib
export, charts from pictures. Math coordinate systems and function graphs move into the chart module; geometry and
vectors stay in `math`.

1. **Parts (done 2026-11-25 for engine and COM):** `SketchObject.Parts` (look per part key: style or hidden, kind keys
   apply to all), `ObjectType.Parts`/`PartActions`, `Registry.Parts`/`PartAt`/`PartActions`, applied in the single render
   path; charts tag every piece (`series.0`, `marker.0`, `bar.0`, `axis.x`, `label.y`, `grid.major` …); engine `parts`,
   `partAt`, `partActions`, `applyPartAction`, `setPartLook`; COM: leaf shapes carry `ISK_PART`, native formatting of
   some parts folds into those parts, a click on the selected chart selects the part (bar, pie menu, "Parts …" list,
   outline). Web (click selects the part, bar and pie) and LibreOffice ("Chart Parts..." dialog) done 2026-11-25, not yet
   run in their hosts. Open: Tab cycling through parts.
2. **Figure model (done 2026-11-25):** `chart.figure` with a versioned JSON spec – stacked panels, principle and data
   axes, markers across panels, value lines, regions (also "only where A is above B") with hatching, notes, dimension
   arrows, numerals, captions; the maintainer's three figures as presets; COM table editor for chart data (grid, empty
   row = new series, paste from Excel).
3. **Curves from functions and sketches (done 2026-11-25):** 13 function generators with text form, sketch recognition
   (10 kinds, smoothing fallback), COM "Sketch a curve" on XY charts and figures (verified with the real mouse). Axis
   breaks drawn by XY charts and figures (zigzag or slash, actions on the axis parts) since 2026-11-25; not exported to
   matplotlib/pgfplots (comment only).
4. **matplotlib and pgfplots export (done 2026-11-25):** `chart.matplotlib` (.py) and `chart.pgfplots` (.tex) for every
   chart type incl. figures, part looks respected, texts editable; Python syntax checked, but not yet run with matplotlib
   or compiled with LaTeX (neither installed here). Math merge done 2026-11-25: `chart.math.axes` and
   `chart.math.function` (formula evaluator, function families, generators); the math items step back.
4a. **Editable exports (maintainer 2026-11-25):** LaTeX – every drawing (flowcharts, block diagrams, sketches) as TikZ
   with texts as TikZ nodes (document font, formatting and maths stay editable in LaTeX, umlauts and special characters
   escaped), charts and figures as pgfplots (axes, ticks, legend, labels, value lines, hatched regions as LaTeX code, data
   inline), standalone or as a `tikzpicture` to `\input`; SVG – texts stay `<text>` (already so), every element and part
   becomes a named group (`id`/`class` with the part key, Inkscape layer labels), hatches as patterns, so text and
   styles can be edited in Inkscape/Illustrator. Both through the engine's exporters, offered in all three hosts.
   Done 2026-11-25: TikZ for every drawing (`latex.tikz`, Core `TikzExporter`), editable SVG (engine `renderPageSvg`
   mode `export`, web SVG export). Open: pgfplots for charts (after the figure model), SVG export mode in COM/LibreOffice.
4b. **matplotlib and LaTeX import (maintainer 2026-11-25, planned):** the reverse of the exports – a matplotlib script
   (.py) or a LaTeX file (pgfplots `axis`/`groupplot`, TikZ `tikzpicture`) becomes editable elements: pgfplots and
   matplotlib plots become charts or figures (series from `plot`/`scatter`/`step`/`bar`/`barh`/`fill_between`/
   `axhline`/`axvline`/`annotate`, `\addplot coordinates/table`, `fill between`, axis labels, ranges, log modes,
   legends, subplots/groupplots as stacked panels), TikZ drawings become basic shapes, arrows and texts (`\draw`,
   `\node`, `\fill`, colours, dash patterns, arrowheads). Code is read, never executed: a safe parser for the literal
   subset (lists, numbers, keyword arguments, simple `numpy.linspace`/`arange` and arithmetic on them); anything computed
   beyond that is reported as not imported. Files written by our own exporters round-trip exactly (tests). Through the
   import command in every host (COM Review ▸ Import, web import button, LibreOffice import), plus pasting code from the
   clipboard.
5. **Charts from pictures (done 2026-11-25 for COM):** Slidemax scanner port (bars/columns, digit labels), new curve
   digitiser, `ChartFromPicture`; COM Review "Chart from picture …" (clipboard or file), verified in PowerPoint. Open:
   engine method and web/LibreOffice commands.

**Flowcharts (maintainer example 2026-11-25, done):** flowchart symbols fit and wrap their text, start circle and end
double circle, elbow connectors route by their docked sides (C runs with clearance for "yes" exits and loops back) and
carry a "yes"/"no" label, the diagram layout docks branches and loops on the sides and computes the clearance, dashed
note frames. Open: obstacle-aware routing for hand-placed boxes.

### Technical figures fit-gap (research 2026-11-25)

Research of EPO/USPTO/PCT/DPMA practice and figures from real applications against the library. In progress (agents with
file ownership): annotation – optional steps, ISO 5807 shapes (predefined process, off-page connector, loop limits,
preparation, manual operation, database, delay), fork/join, yes/no exits on quick add, hop-overs and junction dots on
connectors, wide (hollow) arrows, double-line arrows, arrow pairs, bus slash with count, tables, view arrows, detail
views, material hatches, heat-flow arrows; diagrams – control-loop blocks (summing junction, multiplication point,
transfer function), computer-system presets and bus, communication icons (phone, base station, router, satellite, IoT,
wireless link), packet/frame/memory layouts, sequence fragments and message numbering, GUI wireframes, state circles.
Later: digital timing diagrams (`chart.timing`, with charts v2), isometric parts (`mech.iso.*`), "moved position"
phantom copies, battery cell arrays and cooling plates, optics module, polar/hysteresis/constellation/eye/Sankey charts.

### Review at the end of 2026-11-25 (charts v2, figures, connections) and next steps

**Done today:** parts of elements in all three hosts; the figure model with the maintainer's figures; curves from
functions and sketches; charts from pictures; TikZ and editable SVG export; flowcharts as drawn by hand (side-aware
connectors, layout without lines through boxes); the figure fit-gap (flowchart symbols, wide/double/pair arrows,
junctions and hops, tables, control loops, communication icons, data layouts, sequence fragments, GUI wireframes,
interactive pools and lanes); connection "+" for every module with ports (PV, building services, electrical, fluid,
process, machines; COM verified); COM ribbon rework (Extras with logo, modules/line weights/accent inside, Review with
Compatibility and Chart from picture, Layout bottom row, sharp icons, black-and-white toggle per slide); export of
selection or slide; dialogs over the selected element fixed; the word "patent" removed from all texts.

**PowerPoint scenarios green:** chart-parts, chart-sketch, chart-picture, chart-data-editor, connect-plus,
dialog-over-unit, diagram-layout, compatibility-check, ribbon-look.

**Plan review – what is next, in this order (COM first, then web, then LibreOffice):**
1. Host parity for today's engine features: connection "+" (web buildlogic tooltips and `medium`, LibreOffice port
   labels), chart from picture and sketching (engine methods `chartFromPicture`/`sketchCurve`, web canvas trail,
   LibreOffice dialog), export of selection/slide in web and LibreOffice, figure presets checked in both hosts.
2. Axis breaks drawn by XY charts and figures; math merge (coordinate systems and function graphs as chart presets,
   items of the math module step back via `LibraryItem.Alternative`).
3. Clean slides for sharing (below; engine and PowerPoint COM done 2026-11-25, web and LibreOffice open).
4. Obstacle-aware routing for hand-placed flowchart boxes; Tab cycling through chart parts; isometric parts, moved
   positions, battery cell arrays, optics module (fit-gap "later").
5. Kennfeld report (waiting for the maintainer's retry), web check in PowerPoint (needs the dev certificate).

**UI/UX consistency rules confirmed today:** one place per command family in every host (Review: compatibility,
compare, chart from picture, import, export); toggles show the state of the current slide (black and white, line
weights, accent) while working modes are user settings (snap, bar, anchor, move); every modal dialog lets the mouse and
keyboard through (no hook may swallow input of a foreground dialog); sub-selection follows one pattern (click on the
selected element selects its part; bar and pie act on it; "whole element" leaves it); every "+" says what it adds.

### Clean slides for sharing (maintainer 2026-11-25; engine and COM done 2026-11-25, web and LibreOffice open; like Slidemax)

A "Clean slides" command for the selection, the slide or the whole presentation before slides leave the company:
charts and figures either **normalised** (same proportions and shapes, values and axis numbers made unrecognisable –
e.g. scaled to 0…1 or to index values, tick labels replaced) or **randomised** (plausible random values with the same
look, curves keep their character); optionally texts, reference numerals, names, notes, comments, hidden slides and
document properties removed or replaced. One undo step, a report of what was changed; COM first (Extras or Review
group), then web and LibreOffice through the same engine command.

**Engine done (2026-11-25, docs/en/user-guide/README.md section 6d):** `ObjectType.Sanitize` with `SanitizeOptions`
(mode none/normalised/randomised, rename categories, replace texts, strip metadata, seed, spread), `Sanitizer.Apply` with
a bilingual `SanitizeReport`, engine method `sanitize` (ids or page, one undo step, state + report). Every chart type is
covered (XY, bar, axes, torque, efficiency, pump/fan, Bode, tolerance band, figure); linear and radius dimensions, tables
and electrical component values too. Open: the host commands (COM dialog first: scope selection/slide/presentation, mode,
the four options, seed shown in the report; then web and LibreOffice); presentation-level items that live outside the
engine model (native PowerPoint charts and tables, speaker notes, comments, hidden slides, document properties); values
in further modules (parts lists derive from the parts and follow them; energy and building-services ratings, map and
architecture dimensions, mechanical parameters shown as labels); angle dimensions keep their value (the angle is visible
in the drawing).

### Maintainer feedback 2026-11-25 (evening), COM first

- Ribbon (done 2026-11-25): PowerPoint's table dropdown under the shapes in the basics group; black and white right
  before Extras; dimensions in the lines gallery; gallery icons: hatched square for shapes and hatching, the decision for
  flowcharts, the bearing block for joints and supports; "All caps" toggle right of superscript (native text through
  PowerPoint, InventaSketch elements through the style `Caps`, model text keeps its case); Layout bottom row: selection
  pane, picture insert, crop; the maintainer's three example figures left the library (one neutral principle figure
  instead; the examples stay resolvable by id).
- **Complete UI/UX review of the interaction with every element (maintainer 2026-11-25, top priority):** one
  consistent concept for what is found where and how it is operated – direct manipulation (drag points, ends, limit and
  value lines in charts, resize, rotate), snapping, re-snapping and detaching, connecting and chain building, insert
  fitting elements, view switches, data entry; what goes on the element (handles), into the floating bar (few clear
  pictograms), into the pie menu (complete, labelled, fixed sectors), into the ribbon and the sidebar; one visual language
  for handles and states (attached, dependent, snapped); feedback on what moves along; large hit targets, little text,
  understandable for new and older users. Concept: docs/en/design/interaction-ux.md (in progress), then COM, web,
  LibreOffice.
- Overlay bar, pie menu and handles review ("important point"): handles must never overlap (short pipes and cables),
  one "+" per meaning, every bar/pie entry explains itself, search popups close on a click outside, tables get "+" on
  every side for rows and columns. In progress (design note docs/en/design/overlay-ux.md).
- Sketched curves: switch between smoothed, interpolated, fitted function, straight segments and as drawn; cleaner
  default (de-noised trail). In progress.
- Charts: drag limit lines, value lines and marks interactively (drag handles in Core, engine `dragHandles`/`moveHandle`,
  COM overlay). In progress.
- Later: the sidebar editor's preview is not live while the slide changes, and the sidebar still showed the old
  compatibility wording in the maintainer's build (web strings are clean since 2026-11-25; check the COM sidebar after the
  next web build).

### Plan review, end of 2026-11-25 (night)

**Done since the evening review (merged to main):**
- Interaction concept for every element (docs/en/design/interaction-ux.md) and its first COM implementation: one
  handle layout without overlaps (PowerPoint's own handles count as obstacles, 24 px hit areas), one "+" per meaning
  (extend and connect merged on cables and pipes, corners as dots), bar groups with separators and a "⋯" that opens the
  pie, a pie with eight fixed labelled places (Text, Edit, Insert, Duplicate, Style, Delete, View, Arrange) and one-line
  hints, insert box closes on a click outside, tables add rows and columns on every side, chart grips.
- Charts: sketched curves switch between smoothed, interpolated, fitted function, straight segments and as drawn
  without losing the drawing; limit lines, value lines, markers, axis marks and span edges are dragged with grips (Core
  `DragHandles`, engine `dragHandles`/`moveHandle`); digital timing diagrams `chart.timing` (4 presets).
- Import of matplotlib, pgfplots and TikZ code (read, never executed; engine `importCode`; COM Review ▸ Import with
  files and clipboard; a report of what was not imported).
- Optics module (22 types, 35 items, rays traced live through lenses, mirrors, prisms, splitters, gratings).
- Web and LibreOffice: clean slides, export of selection or slide; LibreOffice chart from picture.

**Next, in this order (COM first):**
1. Interaction concept phase 1 (concept section 9): grips for every type with values (dimensions, angles, joints,
   centre distances), dependency highlight on hover, one pictogram one meaning (engine test), remove actions that
   duplicate a choice or view, "+" between table rows/columns, corner dot inserts where drawn; COM ribbon icons in the
   same line style everywhere (menus of the Elements group).
2. COM: "Add ray bundle" (optics) must insert the new ray (action results with new objects become units).
3. Host docs (powerpoint-com.md EN/DE) rewritten to the new bar, pie and handles.
4. Phase 2: the same concept in the web add-in (port `HandleLayout`, bar groups, pie places), code import buttons in
   web and LibreOffice, chart grips in web.
5. Phase 3: first-use hints, larger UI size for low vision.
6. Optics (maintainer idea 2026-11-25, optional): interactive light rays – drag a source, a lens or a mirror and watch
   the rays follow live while dragging; drag the ray's start angle or the focal point (grips); show the image point.
7. Ribbon (done 2026-11-25): the Review group moved into the menu "More" (was "Extras", section "Check and
   exchange"); painted 16 px icons for the basics galleries and the collapsed Elements and Text groups; the table
   dropdown as an own menu of the same width; fluid power shares one menu with process and thermal engineering (two
   titled sections; the modules stay separate because ISO 1219 and ISO 10628 draw pumps, valves and lines differently);
   gallery thumbnails fill ellipses and rounded rectangles as such (no white squares), tiny elements stay small.
8. Selection (maintainer 2026-11-25, in progress): a clear selection look for single, multiple and connected
   selections (soft blue halo around the exact outline, like Slidemax's gradient), so overlapping elements show what is
   selected; "Select connected": electrical and fluid nets, loops, what moves along (shafts, gears, belts), all
   following elements (flowcharts, chains), closed room walls. Attached elements appear on the side that was clicked
   (fitting with a preferred direction).
9. UI/UX review of all elements, combinations and workflows (2026-11-25, docs/en/design/ux-review-2026-11-25.md):
   readable bar and pie, element name on the pie's edit place, progressive disclosure, four-button bar; follow-ups F2
   slide-wide symbol size, F3 pictogram and label hygiene, F4 multi-selection review.
10. **Selection look everywhere (maintainer 2026-11-25):** the blue halo also for every part that can be selected:
    curves, areas, axes, limit lines, arrows and labels of charts and figures (COM: the selected part glows, the rest of
    the element faintly – implemented, to be verified in PowerPoint), and the same in the web add-in and LibreOffice.
11. **Connected elements, extended (maintainer 2026-11-25):** besides nets, loops, "moves along", following elements and
    room walls: the force flow (load path) through mechanical assemblies; lines up to and including the device and on
    beyond it; display options per medium where relevant – bus vs. power, DC/AC (L1/L2/L3/N/PE colours), warm/cold
    (supply/return), pressure/return/drain, signal – as a slide-wide colour mode and as a highlight of the connected set.
12. **Names that belong together, across slides (maintainer 2026-11-25):** every line, cable and pipe can carry a
    designation (and a value where relevant: cross-section, type, rating, DN, flow); line and cable ends get end
    markers; the same name means the same connection – also across slides (four walls of a room on four slides: where a
    line leaves the slide it says where it continues, "→ W3 slide 4", and the selection/halo shows its continuation).
    Every element gets its designation and value label where relevant (resistor, capacitor, inductor, fuse, motor,
    pump, valve, building-services devices), placed automatically and kept on edits.
13. **Electrical installation and switchgear (maintainer 2026-11-25):** a distribution board / control cabinet module:
    DIN rails, terminal blocks (feed-through, PE, N, fused, multi-level), RCD (FI), MCBs, RCBOs, SPDs, main switch,
    meters, contactors, relays, bus actuators and couplers (KNX and others), power supplies, switches and push buttons,
    cable ducts – with designations (BMK/reference designations per IEC 81346 ideas) and ratings. Exports: house
    installation plan list, network/circuit plan (which circuit feeds which room/device), distribution board layout
    and labelling, terminal plan (terminal strip with wires, cross-sections, colours, targets), cable list.
14. **Engineering calculations, interactive (maintainer 2026-11-25):** wherever values can be entered (electrical and
    electronics, machine elements, machines, fluid power, solar/energy, building services, process), derived values
    are computed live and shown on request: U/I/P/R, C, L, impedance and time constants; speed, torque, power, ratio,
    centre distance from module and teeth; flow, pressure, force; heat quantity and heat flow; PV string voltages and
    yield. Concept and catalogue: docs/en/design/engineering-calculations.md (done 2026-11-25: principles, formula
    catalogue with sources per domain, ~35 plausibility checks, linked charts, cross-module chains). Phases, each COM
    first, then web and LibreOffice on the shared engine API:
    - **Calc 0** quantities, units, one parser/formatter (4k7, µF, rpm, °C vs K, tolerances); a unit converter and a
      calculator with units ("230 V * 16 A", "1500 rpm in rad/s"; maintainer 2026-11-25), engine `convert`/`calc`, COM
      popup in the More menu and the value card with Calc 2.
    - **Calc 1** relation engine and engine API (values, setValue, clearValue, explain, dragValue, checks); entered
      values are never overwritten, conflicts show both values.
    - **Calc 2** COM UI: level switch (quick sketch / explore / engineer), Values button, pie Edit ▸ Values ▸, value
      card, labels, grips/sliders, formulas step by step.
    - **Calc 3** electrical and machine elements (series/parallel, LED, gear trains n/T/P, belts, chains, screws,
      springs, bearings, motors) with checks.
    - **Calc 4** fluid, thermal/process, building services, energy/solar and cross-module chains.
    - **Calc 5** linked charts (torque–speed, pump/system, PV I–V, Bode, step response, gear train, heating curve).
    - **Calc 6** engineer extras (tolerance intervals, calculation sheet, sources pane, check settings).
    **Guiding idea (maintainer 2026-11-25):** sketch fast, but also learn and understand interactively and playfully,
    and draw and compute physically correct and technically precise, with plausibility checks (gentle warnings with
    their rule and source: gear modules that do not match, a fuse below the cable load, a PV string above the
    inverter's maximum voltage at low temperature …); the same for charts (curves generated from or linked to element
    values with operating points, e.g. motor torque–speed, pump vs. system curve, PV IV curve with MPP, Bode plot of an
    RC network); and across modules where sensible (e-motor → coupling → gearbox → wheel, PV → inverter → distribution
    board → circuits, pump → pipes → heat exchanger → radiator).
15. Later: sidebar preview live, sequence/GRAFCET charts, isometric parts, moved positions, battery arrays,
   obstacle-aware routing for hand-placed flowchart boxes.

### Further modules and functions worth adding

- **Building services (TGA, P1 after charts; done 2026-11-24, module `building-services`, docs/en/modules/building-services.md;
  whole installations switch plan ↔ section ↔ schematic through `AssemblyViews` with the module's rule; walls, doors and
  windows switch along to their elevation (view alias `section`/`elevation`, 2026-11-25); open: a section line per
  wall; the electrical house symbols joined the installation views on
  2026-11-25, the rule moved to Core as `InstallationViews`):** heating, plumbing, ventilation and electrical installation tied to the
  architecture rooms, with plan, section and schematic (riser) views of the same installation (the view switch of
  assemblies applies).
- **Parts list from the slide (P2, done in COM 2026-11-24):** a table element that lists the parts of the slide or selection (type, key
  parameters, reference numeral), updated when parts change; for technical figures and gearbox slides.
- **Dimension between parts (P2, done in COM 2026-11-24 for two parts):** select two parts → centre distance, gap or angle as a live dimension.
- **Slide-wide style (P2):** line weights per ISO 128 group, monochrome mode, colour accents per module, applied
  to every element on the slide at once.
- **Math & geometry (P3, done 2026-11-25, module `math`, docs/en/modules/geometry.md):** coordinate systems, function
  graphs, vectors, angles, triangles and circles, constructions for teaching slides (18 types, 32 items).
- **Hand-drawn look (P3):** an optional sketchy rendering of the same model for workshop slides.
- **Optics (P3, idea 2026-11-25):** lenses, mirrors, prisms, beam splitters, light sources and detectors with beam
  paths that follow the elements – common in technical figures and teaching slides.
- **Timing and sequence charts (P3, idea 2026-11-25):** signal timing diagrams and step/transition charts (GRAFCET
  ideas) tied to the electrical and fluid modules' designators.

## Plan 2026-11-21: interaction, modules and elements

Agreed with the maintainer after the COM polish round. Priorities: **P1** next, **P2** after that, **P3** later. Each
item lands in Core/engine first, then COM, then web, then LibreOffice (host order unchanged).

### Interaction mechanisms and UX concepts

Status 2026-11-21 (COM): the one line language (extend, draw on, corners, branches incl. wall T joints, split, join),
build handles for gears, shafts and machines (incl. shaft stretch) and free docking of annotations on surfaces are done
and verified in PowerPoint; so are Shift 15° steps for lines and crossings of wires and pipes (automatic hop,
connect icon). Engine/web follow. Anchor audit done 2026-11-21 (report from `AnchorAuditTests`): machine cross-sections
get hubs in side and schematic views and lead their shaft, heating and sanitary symbols get pipe connections, plan pipes
take the line language, side-view gears mesh only with side views on a parallel axis across it (pitch circle carries the
axis, `Anchor.SidePitch`).

- **P1 – One line language for everything line-like.** What wires already do (corners, branches) works wherever it makes
  sense, with the same handles and the same rules:
  - *Extend / continue drawing:* a "+" at every free end; drag it to lengthen the line, click it to keep drawing
    (click-click adds corners, Esc or double-click ends, snapping and docking as usual). Wires, pipes, roads (maps and
    automotive), rivers, rails, routes, trajectories, polylines, walls, section lines.
  - *Branch:* wires, pipes, roads, rails (done for wires, pipes, map roads); walls get T-joints (a branch wall joins
    the wall it starts on); belts and chains get a third pulley (wrap around).
  - *Corners:* insert, drag, remove for walls (a corner = two joined walls), hatch regions, map regions, lanes.
  - *Shafts:* drag an end to stretch (parts keep their place), "+" at the end adds the next shaft section (stepped
    shaft, coupling or bearing at the joint); split a shaft; a gear on the end of a shaft extends it.
  - *Join and split:* drop an end onto another end of the same kind to merge the two lines; split a line at a corner
    (scissors in the bar).
- **P1 – Live coupling preview while dragging:** meshing partners scale and belts stretch in the ghost (today the
  result appears on release); followers are highlighted.
- **P1 – Insert by typing:** a search box at the pointer (Ctrl+Space or a bar button) that inserts the chosen item
  where the pointer is, in the selection's view; recent and favourite items first.
- **P2 – Assemblies as library items:** save the selection (with couplings, numerals, views) as a user library item;
  starter assemblies: two-stage gearbox, planetary stage, e-drive (battery – inverter – e-machine – gearbox – wheels),
  belt drive, hydraulic cylinder circuit, room with door and window, junction with lanes.
- **P2 – Multi-view links:** the same part in stick, side and axial view on different slides or units shares its
  identity; changing teeth or size in one view updates the others.
- **P2 – Drag to simulate:** drag an input (crank, shaft, gear) on the slide and the linkage or gear train moves
  with the right ratio; capture positions as ghosts or as Morph slides (COM animation exists).
- **P2 – Snap labels and measurements:** a small label at the snap target ("pitch circle", "axis", "pin 2") and live
  distance/angle to neighbours while dragging (like design tools).
- **P2 – Format painter for semantics:** copy view, parameters and style from one element to others of the same type.
- **P2 – Selection-wide operations:** with several units selected: align centres on an axis, equal spacing along a
  line, same size, renumber reference numerals in reading order, release/couple all links.
- **P2 – Keyboard:** Tab cycles through connected elements, arrow keys nudge with coupling, Shift+click on a lock
  badge releases all links of the selection.
- **P3 – Variants and states** (spec §7): before/after, operating modes; visibility per variant; slide per variant.
- **P3 – Diagram auto-layout and routing:** layered layout that keeps manual placement, connectors that avoid shapes,
  jump-overs at crossings.
- **P3 – Figure workflow in the hosts:** numbering assistant across figures, preflight panel with navigation to the
  finding (preflight rules exist in the figures module), figure-sheet layout.
- **P3 – Import:** SVG/DXF outlines as hatchable regions, GeoJSON/GPX for maps, CSV for charts.

### Building gear trains yourself (maintainer 2026-11-21)

Status 2026-11-21 (Core + COM): carrier part with pins and build "+", planets sized to sun/ring, 2–8 planets, stepped
planets, explode of planetary sets (axial views), bevel gears with apex ports (mitre pairs, differential from parts)
done. Since done: coupling of bevel pairs, exploding the schematic set and the differential, moving gear trains, the
differential in the gear train speeds, planet pairs (Ravigneaux set, 2026-11-24/25).

- **P1 – Planet carrier (Steg) as its own part:** axial view with 2–8 arms or a disc and planet pins at a radius, side
  view with the pins as axes; planets dock on the pins and are carried; build "+" on free pins (planet) and the centre
  (shaft); option "fixed carrier" (grounded, housing symbol), carriers that are no shafts.
- **P1 – Planetary sets you can edit:** number of planets 2–8 (today 3–6), several planet rows, stepped planets (two
  gear stages on one planet pin, sun side and ring side), planet pairs (Ravigneaux-like), compound sets; "explode" a set
  into sun, planets, ring and carrier so each part can be edited and coupled.
- **P1 – Bevel gears and differentials built from parts:** bevel pairs mesh across perpendicular axes in side view;
  build "+" on a bevel gear offers the mating bevel at 90°; a differential from case/carrier, two side bevels and two or
  more spider bevels, also as a starter assembly.
- **Done 2026-11-21 (Core + COM): building linkages yourself** — links dock on joints, pivots, joint symbols and sliders,
  build "+" at free joints, degree of freedom in the bar, drag in motion mode with constant lengths (see the host docs).
  Four-bar, slider-crank, toggle, quick-return and scotch yoke explode into a self-built linkage; links offer slots
  (pin in slot) (2026-11-21). Since done: the cam disc part with cam contacts and exploding the cam mechanism, limits,
  ghosts and coupler curves for self-built chains, the engine/web side (2026-11-24/25). Open: gear joints in chains.
- **P1 – Building linkages yourself (maintainer 2026-11-21: "only predefined ones exist"):** links, joints and fixed
  pivots exist as parts, but link ends do not dock onto each other and only the predefined mechanisms (four-bar,
  slider-crank, …) move. Plan: link ends dock onto the joints of other links (attachment to a point of a point object,
  follows it); links, joints, sliders and fixed pivots form a chain the engine recognises (graph), with the degree of
  freedom shown live (Grübler); build "+" at a link's free joint (next link, fixed pivot, slider, cam follower); drag a
  joint or crank on the slide and the chain moves with constant link lengths (general 2D constraint solver instead of
  the per-mechanism closed forms); limits, ghost positions, coupler curves and animation as slides for any chain;
  predefined mechanisms become starter assemblies that can be exploded into their links.
- **Done 2026-11-21 (Core + COM): moving gear trains live by dragging in motion mode** (see the host docs); linkages
  turn their input crank. Open: slide animation of gear trains as an extra for everything that moves (maintainer:
  live dragging first), drag-to-simulate for self-built linkages.
- **P2 – Moving gear trains:** turn the input (drag a gear or shaft on the slide) and every gear turns with its ratio
  (teeth stay in mesh, carriers revolve, planets roll); ratios shown on request; animation as Morph slides like the
  linkages (`ObjectType.AnimationFrames` for gear trains). Today only linkages of the kinematics module animate as slides
  (pie menu), and drag-to-simulate is open for both.

### New modules

- **P1 – Charts (`chart.*`, spec §6.12):** XY/line/scatter/bar, characteristic curves (torque–speed, efficiency map),
  Bode plot, tolerance bands; data typed in or pasted from Excel; monochrome-safe.
- **P2 – Process & thermal (`process.*`, done 2026-11-25):** P&ID beyond fluid power (vessels, columns, heat exchangers, pumps,
  instrumentation bubbles with tags), thermodynamic cycles (T-s, p-v, p-h diagrams), HVAC.
- **P2 – Energy systems (`energy.*`, done 2026-11-25):** PV, wind, battery storage, charger/wallbox, grid connection, hydrogen,
  single-line diagrams across voltage levels.
- **P2 – Robotics (`robot.*`, done 2026-11-25):** arms from kinematic joints, frames, grippers, work cells, reach envelopes, safety zones.
- **P3 – Networks & IT (`net.*`):** servers, cloud, routers, switches, topologies, C4 context/container diagrams.
- **P3 – Optics & physics (`optics.*`):** rays, lenses, mirrors, prisms, waves, fields.
- **P3 – Reliability & systems engineering:** fault trees, FMEA blocks, SysML-style blocks and requirement links.
- **P3 – Project diagrams:** Gantt/timeline, mind map, org chart.

### Missing elements in existing modules

- **Mechanical (P1):** ball and lead screw (with nut that follows the screw axis), freewheel, torque converter, CVT
  variator, harmonic drive, cycloidal drive, gearbox housing outline, oil sump and level, park lock.
- **Machines (P2):** battery pack from modules/cells, on-board charger, busbars, cable/phase leads, inverter topology
  detail (half-bridges), cooling circuit around the e-drive.
- **Electrical (P2):** PLC with I/O cards, motor starter, sensor family (temperature, pressure, Hall, position),
  connector pinout, harness segments with lengths.
- **Fluid (P2):** cartridge valves, proportional and servo valves, valve manifold/block, quick couplings, filter with
  bypass and clogging indicator.
- **Architecture (P2):** roofs (plan and section), dimension chains for walls, electrical/HVAC layer tied to rooms,
  garden and landscape.
- **Automotive (P3):** manoeuvre templates (lane change, overtaking, turning, parking), V2X, more signs, interior
  layout (seats, displays, controls).
- **Kinematics (P3):** gear and belt joints, cam profile generator, parallel robots (delta), Geneva drive.
- **Diagrams (P3):** sequence fragments (alt/loop/opt), deployment nodes, C4 (or in `net.*`).
- **Annotation (P3):** tables, magnified detail view linked to its source, revision clouds.

## Open items

- **Sketched curves: key points (2026-11-25, `CurveKeyPoints`):** curves store only their key points, with a "Detail"
  choice and the drawing speed as a hint (COM trail time stamps). Open: the `chart-sketch` scenario (row count, Detail
  dropdown) has not run in PowerPoint yet; the web add-in does not send pointer times to `sketchCurve` (`[x, y, t]`)
  yet; selecting a freshly sketched curve computes all choice previews once (about 50–130 ms, then cached).
- **Shaft fit (2026-11-25, `ShaftFit`):** parts fit their shaft on insertion, at an axis end and when dragged on; a shaft
  whose diameter changes later does not refit the parts already on it (gears would leave their mesh), and an assembly
  view switch keeps sizes. Worm wheel and freewheel axial bores differ from their side/section bores by about 7 % after a
  view switch; views without a library preset (pulley side, coupling side, washer top) keep the box when switched to
  (`Registry.WithView`). Planetary/Ravigneaux schematics, worms, ring gears, supports and the reducers (strain wave,
  cycloidal, CVT, torque converter) have no bore. The COM host still has to call `Fitting.PlaceNear` for the live
  insertion preview and pass the click point (`toward`) to `Fitting.Place`.
- **PowerPoint UI scenarios (2026-11-21):** `tests/powerpoint-com` holds eleven scenarios ported from the session's
  test scripts; `explode-linkages` runs green (10 checks). The other ten (quick-add, bar, gear-mesh, motor-shaft,
  build-ports, line-language, crossings, gear-trains, gear-motion, linkage-build) have not run in this form yet and may
  need screen-position fixes.
- **Status 2026-11-20:** engine core, Engine API and twelve modules on `main`: basic shapes, annotation, mechanical,
  figures, kinematics, electrical, electric machines, automotive/ADAS/HMI, architecture, diagrams, maps and routes,
  fluid power (each with tests, gallery and EN/DE module docs; 826 library items in 70 categories, verified in
  PowerPoint).
- Core wishes from the maps and fluid modules (2026-11-20): shared helpers for port routing/`ConnectToPort` (electrical and fluid duplicate them) and multi-subpath
  freeforms; multi-line text bounds in `Prim.Bounds`; the selected point index for "remove waypoint". Done 2026-11-20:
  `IconIds.Pin`, `Medium`, `Actuation` (COM and web pictograms); `ObjectType.IsPart` (the BOM skips map symbols and
  diagram shapes); `ObjectType.OnParamChanged` with `Registry.SetParam` (engine `setParam`, COM label menu; fluid
  valves keep their drawing scale). Done 2026-11-21: line branches — a line end docked onto another line's rail is
  attached to it (`Attachments.For`, rail ids from `Registry.PageAnchors`), follows it (also across COM units) and the
  netlist and fluid connection list count it. COM add-in feature-complete for slice 2; web add-in builds and runs in the browser.

- UI icons (maintainer requirement 2026-11-20): icons in overlays, floating bar and pie menu must be abstract, clean
  pictograms (one consistent line-icon set, e.g. "stick view", "section", "axial view", "mirror"), not scaled-down
  renders of the concrete element, so they stay recognisable at small sizes. Modules name an icon per view/action
  (`ViewDef`/`ContextAction` icon id); hosts draw it from the shared icon set. Library galleries may keep real previews.
- Order agreed with the maintainer (2026-11-20): finish the COM add-in first, then the web add-in, then LibreOffice.
- Web: webspace package (`scripts/package-webspace.ps1` → `dist/InventaSketch-Webspace` + ZIP): done 2026-11-20 —
  setup page generating the manifest for its own HTTPS address, `.htaccess`/`web.config`/robots, downloads of the
  other editions that exist. Open: test on a real Apache/IIS webspace and load the add-in from there.
- Web installer (ported from Slidemax, translated to English with German/English messages 2026-11-20): run a real install and load the add-in in PowerPoint (Windows without COM add-in, Mac, web).
- Core/engine requests from the module work: done 2026-11-20 (rails, mount and port priority, concentric fit,
  pin-id attachments; hollow and crow's-foot heads, underline; input and document actions, change sets with dependents,
  plan scale, view sizes; shared AttachPoint/ReversePoints; ADR-0009). Follow-ups:
  - COM: native moves (PowerPoint handles, arrow keys) now redraw anchored and dependent units (2026-11-20); the plan scale is in the floating bar and pie menu of plan elements (2026-11-21); units of the web add-in with chunked
    model tags (`ISK_MODEL_2` … `ISK_MODEL_N`) are read (2026-11-21); document actions only change the unit's own objects;
    the floating bar shows up to three key type actions with telling pictograms (the rest is in the pie menu).
  - Web: done 2026-11-20 — `combridge.ts` redraws only units touched by `changed`/`dependents`; the method contract in
    the web host docs covers `setPlanScale`, `input`, `changed`, `dependents`, conversions and exports.
  - Modules: done 2026-11-20 — view switches take proportions from library presets (Registry.WithView), annotation
    underlines use `TextStyle.Underline`, electrical designator/value are input actions.
  - Checked in PowerPoint 2026-11-20: drawn heads, underline, input prompt, dependent wall redraw with one undo step.
- UX feedback from the maintainer (2026-11-20), COM first, then web:
  - Done 2026-11-20 (COM, verified in PowerPoint where noted in the host docs): black and white as a reversible document setting (fills, text, highlighters); ribbon order (Annotate =
    annotation + figures + basic shapes right after Tools, small About); transparent ribbon thumbnails for dark Office;
    sidebar preview drops units deleted on the slide; text editing by double-click and a bar button; view switch keeps
    proportions; resize about the centre for centred parts; pie menu "Insert ▸" with elements that fit the selection
    (shaft → gears and bearings, wire → electrical symbols, always labels/arrows/numerals), placed at the pointer with
    snapping; "Anchor" toggle next to Snap: attached line ends follow moved units across units.
  - Schematic scaling: done (2026-11-20). Gear hub symbols keep their size; mechanism joints are already capped at
    1.4 mm; stand-alone symbols (joints, bearings, electrical symbols) scale as a whole on purpose.
  - Web host: same UX done 2026-11-20 (pie layout, text editing, Insert ▸ via `suggestions`, black and white via
    `setMonochrome`, centred resize). Type ▸ for line types (Registry.Convert, engine conversions/convert) and hidden
    modules in the sidebar library done too. Label ▸ in the web pie menu and the Anchor setting done 2026-11-20.
  - Ribbon groups like Slidemax: Structure, Colour, Text, Layout: done 2026-11-20 (native formatting is folded into the model).
  - Tools menu like Slidemax (Extras: symbol, decimal separator, proofing language, send/save slides) and hiding
    modules in the ribbon and the sidebar library: done 2026-11-20.
  - Box selection of single points (Alt + drag beside a selected line): done 2026-11-20 in COM.
  - More edit entries in the floating bar (text, key parameters), keeping it short: done 2026-11-20 in COM.
- UX round 2026-11-21 (maintainer): quick-add arrows and port drags for diagrams with a next-element palette (`Flow`),
  more of the pie menu visible in the floating bar (key actions, insert palette), ribbon Annotate group in two columns,
  mechanical coupling (`Coupling`: gears stay in mesh when the centre distance or a gear changes, shafts carry their
  gears) — done in Core, engine and COM. Next: coupling for side/stick views of gears (pitch lines), belts and chains,
  drivetrains and wheels; quick add, coupling and Alt-free edits in the web stage done 2026-11-21 (port drags and connect-to-port still COM only).
- UX round 2026-11-21, part 2 (maintainer), COM: lock badges at every place where the selection touches and follows
  another element, a click releases or couples (`Links`, `SketchObject.Released`); insert palette places fitting parts
  right away (`Fitting.Place`: gear/bearing on the shaft, gear in mesh, shaft through the hub, bearing beside a gear);
  "+" corner and branch handles on wires, pipes, roads and polylines with junction dots at branch ends (`Polylines`),
  double-click removes a corner. Open: the same in the web stage (engine methods for links, fitting and polylines);
  verify all three in PowerPoint with the maintainer. Map roads already form junctions where they cross.
- COM polish 2026-11-21 (maintainer: "bring the COM add-in up to date"): the bar avoids covering other elements, up to
  three fitting parts right in the bar, inner corners do not dock, drives take their shaft along, belts and chains follow
  their pulleys, pictograms for shapes, fills, routing, wall kinds and fluid line types. Open for COM: further exporters;
  `EquivalentAddins` precedence test (needs the web add-in with the maintainer's certificate).
- Verify which Office.js metadata store survives copy/paste and reopen on Windows/Mac/web (ADR-0006).
- Decide the PDF writer (vector) for engine-side export.
