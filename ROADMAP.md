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
pie menu; sidebar task pane; metadata in tags + document properties; COM precedence verified. Status: **in progress** - slice 1 shipped 2026-11-20: per-user registration, ribbon library menus, native insertion (shapes, freeforms, arrowheads, groups, tags) verified in PowerPoint 16.0; slice 2: on-slide insert at pointer, drag with semantic snapping, pie menu, fading floating bar, snapped resize (gear mesh), two-point tool, point editing, type conversion, label pie menu and snapped rotation verified; theme-aware bar and pie menu. Sidebar editor (WebView2 task pane with live sync) verified. UI polish shipped: generated ribbon with thumbnail galleries per category, settings toggles, view pictures and tooltips in bar and pie, light/dark rendering tests. Keyboard shortcuts on the slide, unique ids for copies and sidebar library placement verified. Next: German host docs, translation of the ported web host, LibreOffice phase.

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

- **Status 2026-11-20:** engine core, Annotation, Patent, Mechanical modules and Engine API merged (162 tests green).
  The web add-in is work in progress on branch `feature/office-web` (build scripts `icons.mjs`, `manifest.mjs`,
  `screenshot.mjs` still missing; WebAssembly publish and UI not yet verified). Module docs (`docs/*/modules/`) still to write.

- Web installer (ported from Slidemax, 2026-11-20): translate identifiers, comments and console messages of tools/InventaSketch.WebHost and hosts/office-web/local to English with German/English messages; run a real install and load the add-in in PowerPoint (Windows without COM add-in, Mac, web).
- Verify which Office.js metadata store survives copy/paste and reopen on Windows/Mac/web (ADR-0006).
- Decide the PDF writer (vector) for engine-side export.
- Clarify with the maintainer whether specific Slidemax components may be relicensed into this repository.
