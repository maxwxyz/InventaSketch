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
pie menu; sidebar task pane; metadata in tags + document properties; COM precedence verified. Status: **in progress** - slice 1 shipped 2026-11-20: per-user registration, ribbon library menus, native insertion (shapes, freeforms, arrowheads, groups, tags) verified in PowerPoint 16.0; slice 2: on-slide insert at pointer, drag with semantic snapping, pie menu, fading floating bar, snapped resize (gear mesh), two-point tool, point editing, type conversion, label pie menu and snapped rotation verified; theme-aware bar and pie menu. Sidebar editor (WebView2 task pane with live sync) verified. UI polish shipped: generated ribbon with thumbnail galleries per category, settings toggles, view pictures and tooltips in bar and pie, light/dark rendering tests. Keyboard shortcuts on the slide, unique ids for copies and sidebar library placement verified. German host docs added. Next: translation of the ported web host, LibreOffice phase.

## Phase 3 – LibreOffice extension

Python-UNO `.oxt` + .NET engine process; Impress/Draw/Calc; native shapes and hatches; sidebar panel; on-canvas
interaction; Windows/Linux/macOS packages. Status: **planned**.

## Phase 4 – Kinematics and expanded mechanics

2D constraint solver, linkages, joint limits, drag-to-simulate, ghosting, animation as slide sequence; technical
mechanics module; multi-view links. Status: **planned**.

## Phase 5 – Electrical, electronics, electric machines

Symbols, wires with pins/junctions, nets, buses, ERC basics, house installation, PCB illustration, machine
cross-sections (PMSM, EESM, ASM, SRM, axial/radial flux). Status: **planned**.

## Phase 6 – Systems and domain expansion

Automotive/ADAS/HMI, architecture/floor plans, UML/ER/flow, maps and routes, charts; further modules on demand
(fluid power, thermodynamics, robotics, chemistry, …). Status: **planned / proposed**.

## Phase 7 – Advanced platform

Variants and diff, multi-view synchronisation, image contour recognition and hatching of imported pictures,
PDF/TIFF export profiles, user libraries, automation. Status: **proposed**.

Domain exports by selection/type (proposed): gear and gearbox topology data (REXS where feasible), kinematic chains,
netlists and connection lists for schematics and E/E wiring (KiCad/SPICE), ratsnest/airwires, harness lists, BOM
(CSV/Excel), reference sign lists, diagram structures (GraphML/Mermaid) — as module export extension points.

## Open items

- **Status 2026-11-20:** engine core, Engine API and ten modules on `main`: basic shapes, annotation, mechanical,
  patent, kinematics, electrical, electric machines, automotive/ADAS/HMI, architecture, diagrams (each with tests,
  gallery and EN/DE module docs). COM add-in feature-complete for slice 2; web add-in builds and runs in the browser.

- UI icons (maintainer requirement 2026-11-20): icons in overlays, floating bar and pie menu must be abstract, clean
  pictograms (one consistent line-icon set, e.g. "stick view", "section", "axial view", "mirror"), not scaled-down
  renders of the concrete element, so they stay recognisable at small sizes. Modules name an icon per view/action
  (`ViewDef`/`ContextAction` icon id); hosts draw it from the shared icon set. Library galleries may keep real previews.
- Order agreed with the maintainer (2026-11-20): finish the COM add-in first, then the web add-in, then LibreOffice.
- Web: webspace package like Slidemax-Webspace (`dist/InventaSketch-Webspace`): upload the folder to any HTTPS webspace;
  it contains the web add-in, a setup page (`index.html`) that generates the manifest for its own URL, `.htaccess` /
  `web.config` (engine MIME types, no indexing/crawlers) and downloads of the other editions (COM add-in ZIP, LibreOffice
  `.oxt`) listed in `downloads.json`.
- Web installer (ported from Slidemax, translated to English with German/English messages 2026-11-20): run a real install and load the add-in in PowerPoint (Windows without COM add-in, Mac, web).
- Core/engine requests from the module work: done 2026-11-20 (rails, mount and port priority, concentric fit,
  pin-id attachments; hollow and crow's-foot heads, underline; input and document actions, change sets with dependents,
  plan scale, view sizes; shared AttachPoint/ReversePoints; ADR-0009). Follow-ups:
  - COM: units moved with PowerPoint's own handles or arrow keys redraw their dependents only at the next InventaSketch
    edit; the plan scale has no on-slide control (sidebar only); document actions only change the unit's own objects;
    the floating bar shows no type actions (input actions are in the pie menu).
  - Web: `combridge.ts` does not use `changed`/`dependents` yet; method contract in the web host docs to extend
    (`setPlanScale`, `input`, `changed`, `dependents`).
  - Modules: use `ViewDef.Size` where views differ in aspect (automotive top/side); annotation text and lead-line types
    still draw their own underline lines (switch to `TextStyle.Underline`); electrical designator/value as input actions.
  - Checked in PowerPoint 2026-11-20: drawn heads, underline, input prompt, dependent wall redraw with one undo step.
- UX feedback from the maintainer (2026-11-20), COM first, then web:
  - Done 2026-11-20 (COM, verified in PowerPoint where noted in the host docs): black and white as a reversible document setting (fills, text, highlighters); ribbon order (Annotate =
    annotation + patent + basic shapes right after Tools, small About); transparent ribbon thumbnails for dark Office;
    sidebar preview drops units deleted on the slide; text editing by double-click and a bar button; view switch keeps
    proportions; resize about the centre for centred parts; pie menu "Insert ▸" with elements that fit the selection
    (shaft → gears and bearings, wire → electrical symbols, always labels/arrows/numerals), placed at the pointer with
    snapping; "Anchor" toggle next to Snap: attached line ends follow moved units across units.
  - Schematic scaling: done for the hub symbols (they keep their size); other schematic details of the modules to review.
  - Web host: the same UX (insert suggestions via an engine method, anchor, text editing, black and white).
  - Ribbon groups like Slidemax: Structure, Colour, Text, Layout: done 2026-11-20 (native formatting is folded into the model).
  - Tools menu button like Slidemax: insert symbol, change decimal separator, proofing language, send slides, save
    slides (port from Slidemax).
  - Setting to hide modules that are not needed (ribbon groups, galleries, sidebar library).
  - Box selection of single points: a rubber band over a line selects its points so they can be moved together.
  - More edit entries in the floating bar (text, key parameters), keeping it short.
- Verify which Office.js metadata store survives copy/paste and reopen on Windows/Mac/web (ADR-0006).
- Decide the PDF writer (vector) for engine-side export.
