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
- Verify which Office.js metadata store survives copy/paste and reopen on Windows/Mac/web (ADR-0006).
- Decide the PDF writer (vector) for engine-side export.
