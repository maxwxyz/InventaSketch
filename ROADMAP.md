# Roadmap

> Status values: **shipped** (tested and documented), **in progress**, **planned**, **proposed**, **blocked**.
> Detailed scope per module: [specification](docs/en/product/specification.md). German: [docs/de/ROADMAP.md](docs/de/ROADMAP.md).

## Phase 0 – Foundation (in progress)

| Item | Outcome | Status |
|---|---|---|
| Research | Patent drawing rules (DPMA/EPO/PCT/USPTO), tool landscape, host APIs | shipped (docs/en/research) |
| Repository & governance | README EN/DE, licence, ADRs, contribution rules, CI | in progress |
| Engine core | Model, geometry, primitives, snapping, hatching, edits/undo, JSON, SVG | in progress (26 tests) |
| Module contract | `IModule`, `ObjectType`, views, params, anchors, actions, library | in progress |

## Phase 1 – Quick Sketch MVP

| Item | Outcome | Status |
|---|---|---|
| Annotation module | Arrows, lead lines, dimensions, text, hatching, brackets, mechanics symbols, flowchart starters | in progress |
| Mechanical module | Gears (spur/internal/bevel/worm/rack/planetary/differential), shafts, bearings, couplings, fasteners, springs; stick/side/section/axial/teeth views | in progress |
| Patent module | Numerals, numbering scheme, reference sign list, office profiles, preflight, monochrome export | in progress |
| Engine API | JSON facade, sessions, hit test, snapping, context actions, host draw plans | in progress |
| Web add-in | WebAssembly engine, sidebar preview editor, library, inspector, pie menu, floating bar, native shape writer | in progress |

**Exit criteria:** two-stage gearbox and a patent figure can be created without numeric input in the sidebar editor,
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
5. P3: variants and states, diagram auto-layout, patent workflow panel, import (SVG/DXF, GeoJSON/GPX, CSV).

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
   button; checked in PowerPoint); patent check of all slides with navigation to the finding (COM; engine `preflight` for web and
   LibreOffice) done 2026-11-25; variants and states as a slide comparison (added/changed/removed marked; COM Extras menu,
   engine `diff`) done 2026-11-25; patent check in web (panel for the page) and LibreOffice (dialog over every page
   with navigation) done 2026-11-25; slide comparison in LibreOffice done 2026-11-25; open: the slide comparison and the
   check across all slides in the web add-in (both need the other slides' units through Office.js; engine `diff` is
   ready), SVG/DXF import (vector drawings as editable shapes – a larger piece of its own).

### Review at the end of the P3 round (2026-11-25) and next steps

**Done in this round:** variants as a slide comparison (Core `Variants`, engine `diff`; COM Extras menu, scenario
`variants`; LibreOffice menu, integration check), the patent check in all three hosts (COM window over all slides, web
panel for the page, LibreOffice dialog over all pages with keyboard navigation, checked with real input in
`ui_smoke.py`), diagram auto-layout, CSV/GeoJSON/GPX import. Test totals at the end: .NET 1,698 (18 test projects), web 207, LibreOffice
unit 110, LibreOffice integration 77, LibreOffice UI smoke 23, PowerPoint scenarios green one by one.

**Found by verification (and fixed):** a new LibreOffice command is silently ignored unless it is in the protocol
handler's command list; LibreOffice renames duplicated shapes whose names end in digits, which would have cut the unit
id out of the name in .pptx – names are now restored from the attributes; the photographs rule named only the DPMA
although it applies to every office; the first patent finding of a page has no element, so navigation must handle
page-level findings.

**Plan review – what changes in the plan:**
- *Same command, same name, same place in every host.* "Patent check (drawings)" and "Compare with the previous slide
  (variants)" now exist under the same names in COM and LibreOffice. In COM both sit in the Extras menu; with parts list
  and data export they form a "Review" family – a ribbon group *Review* (Patent check, Compare, Parts list, Export
  data) would make them findable without the menu. Proposed for the maintainer's decision, not built.
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
3. Ribbon group *Review* in COM – done 2026-11-25 (Patent check, Compare, Import, Export data).
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
dimension arrows and numerals for patent figures, curves from functions and hand sketches, axis breaks, matplotlib
export, charts from pictures. Math coordinate systems and function graphs move into the chart module; geometry and
vectors stay in `math`.

1. **Parts (done 2026-11-25 for engine and COM):** `SketchObject.Parts` (look per part key: style or hidden, kind keys
   apply to all), `ObjectType.Parts`/`PartActions`, `Registry.Parts`/`PartAt`/`PartActions`, applied in the single render
   path; charts tag every piece (`series.0`, `marker.0`, `bar.0`, `axis.x`, `label.y`, `grid.major` …); engine `parts`,
   `partAt`, `partActions`, `applyPartAction`, `setPartLook`; COM: leaf shapes carry `ISK_PART`, native formatting of
   some parts folds into those parts, a click on the selected chart selects the part (bar, pie menu, "Parts …" list,
   outline). Open: web and LibreOffice part selection; Tab cycling through parts.
2. Figure model (panels, phase lines, value lines, regions/hatching, notes, dimension arrows, numerals; table editor;
   the maintainer's patent figure as a preset).
3. Curves from functions and sketches; axis breaks.
4. matplotlib export; math merge.
5. Charts from pictures (scanner port, curve digitiser).

### Further modules and functions worth adding

- **Building services (TGA, P1 after charts; done 2026-11-24, module `building-services`, docs/en/modules/building-services.md;
  whole installations switch plan ↔ section ↔ schematic through `AssemblyViews` with the module's rule; walls, doors and
  windows switch along to their elevation (view alias `section`/`elevation`, 2026-11-25); open: a section line per
  wall; the electrical house symbols joined the installation views on
  2026-11-25, the rule moved to Core as `InstallationViews`):** heating, plumbing, ventilation and electrical installation tied to the
  architecture rooms, with plan, section and schematic (riser) views of the same installation (the view switch of
  assemblies applies).
- **Parts list from the slide (P2, done in COM 2026-11-24):** a table element that lists the parts of the slide or selection (type, key
  parameters, reference numeral), updated when parts change; for patent figures and gearbox slides.
- **Dimension between parts (P2, done in COM 2026-11-24 for two parts):** select two parts → centre distance, gap or angle as a live dimension.
- **Slide-wide style (P2):** line weights per ISO 128 group, monochrome/patent mode, colour accents per module, applied
  to every element on the slide at once.
- **Math & geometry (P3, done 2026-11-25, module `math`, docs/en/modules/geometry.md):** coordinate systems, function
  graphs, vectors, angles, triangles and circles, constructions for teaching slides (18 types, 32 items).
- **Hand-drawn look (P3):** an optional sketchy rendering of the same model for workshop slides.
- **Optics (P3, idea 2026-11-25):** lenses, mirrors, prisms, beam splitters, light sources and detectors with beam
  paths that follow the elements – common in patent figures and teaching slides.
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
- **P3 – Patent workflow in the hosts:** numbering assistant across figures, preflight panel with navigation to the
  finding (preflight rules exist in the patent module), figure-sheet layout.
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

- **PowerPoint UI scenarios (2026-11-21):** `tests/powerpoint-com` holds eleven scenarios ported from the session's
  test scripts; `explode-linkages` runs green (10 checks). The other ten (quick-add, bar, gear-mesh, motor-shaft,
  build-ports, line-language, crossings, gear-trains, gear-motion, linkage-build) have not run in this form yet and may
  need screen-position fixes.
- **Status 2026-11-20:** engine core, Engine API and twelve modules on `main`: basic shapes, annotation, mechanical,
  patent, kinematics, electrical, electric machines, automotive/ADAS/HMI, architecture, diagrams, maps and routes,
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
    annotation + patent + basic shapes right after Tools, small About); transparent ribbon thumbnails for dark Office;
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
