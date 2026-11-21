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
done. Open: coupling of bevel pairs, planet pairs (Ravigneaux), exploding the schematic set and the differential,
moving gear trains (P2).

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
  (pin in slot) (2026-11-21). Open: a cam disc part and cam contacts in chains (then exploding the cam mechanism),
  limits/ghosts/coupler curves for self-built chains, gear joints in chains, the engine/web side.
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
- **P2 – Process & thermal (`process.*`):** P&ID beyond fluid power (vessels, columns, heat exchangers, pumps,
  instrumentation bubbles with tags), thermodynamic cycles (T-s, p-v, p-h diagrams), HVAC.
- **P2 – Energy systems (`energy.*`):** PV, wind, battery storage, charger/wallbox, grid connection, hydrogen,
  single-line diagrams across voltage levels.
- **P2 – Robotics (`robot.*`):** arms from kinematic joints, frames, grippers, work cells, reach envelopes, safety zones.
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
