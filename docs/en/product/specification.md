# InventaSketch – Product Specification

> Canonical product specification (English). German version: [../../de/product/specification.md](../../de/product/specification.md).
> This document consolidates and replaces the three earlier planning drafts (a Gemini requirements catalogue, a
> ChatGPT post-MVP specification and a "master specification" with implementation prompt) plus the maintainer's
> requirements of 2026-11-20 and the research notes in [`docs/en/research/`](../research/).
> Status values: **shipped**, **in progress**, **planned**, **proposed**. Rule: nothing is "shipped" without tests.

## 1. Vision

InventaSketch turns ideas into clear technical sketches **directly inside PowerPoint and LibreOffice**. It is a
quick-and-dirty prototyping tool for engineers, inventors and patent professionals: drag in a gear, a shaft, a
bearing, a resistor, a wall, a vehicle or a flowchart step, scale it freely, snap it to its neighbours, add arrows,
labels and reference numerals — and the result is a clean, editable figure. No dimensions, no module, no tooth
count required. Precision, parameters and patent-office checks are available when wanted, never demanded.

It is **not** a CAD, EDA, BIM, GIS, FEM or multibody-simulation system, and it is not a legal compliance tool.

### 1.1 Product principles

1. **Quick Sketch is the default.** Every object is insertable without dialogs or parameters; free uniform and
   non-uniform scaling never forces recalculation.
2. **Schematic representations are first-class** (stick diagrams, conventional representations, symbols) — not
   low-quality fallbacks of a 3D model.
3. **Native first.** Output is native PowerPoint/LibreOffice shapes where the host allows, so colours, line
   widths, fonts, grouping and position stay editable with the host's own tools. Vector pictures only where a host
   cannot express the geometry, documented per host.
4. **Interaction on the slide** where the host permits (COM, LibreOffice), with a contextual floating bar and a
   right-click pie menu; an **interactive preview editor in the sidebar** where it does not (Office web add-in).
5. **Semantics behind the graphics.** A gear knows it is a gear, a lead line knows its part, a wire knows its pins.
   The canonical document model survives in the host document and can be re-opened for editing.
6. **One engine, three hosts, modular domains.** Shared C# engine; independent domain modules that can be added,
   combined or removed without touching the core.
7. **Truthful capability reporting.** No placeholder buttons, no unverified "standard compliant" claims; every
   host limitation is listed in the capability matrix.
8. **Local-first.** Editing, building and exporting work offline.

### 1.2 Success criteria (first public release)

- A two-stage gearbox (two shafts, four gears, four bearings, power-flow arrow, labels) is sketched in under a
  minute without entering a single number, in stick-diagram style, and can be switched to conventional views.
- A patent-style figure with reference numerals, curved lead lines and hatching is created, checked against an
  office profile (DPMA/EPO/PCT/USPTO) and exported monochrome as SVG/PDF/PNG.
- Inserted objects are native shapes in PowerPoint (Windows COM) and remain editable with PowerPoint tools.
- The same document opens in the sidebar editor of the web add-in (PowerPoint for Mac/Web) and in LibreOffice.

## 2. Users and use cases

| User | Typical work | Key needs |
|---|---|---|
| Development engineer | Concept sketches, gearbox/drivetrain layouts, mechanisms, E/E architectures | speed, snapping, schematic symbols, variants |
| Inventor / R&D | Invention disclosures, idea collections, function concepts | quick figures, numbering, before/after variants |
| Patent attorney / paralegal | Patent figures, reference sign lists, office-ready exports | rule profiles, monochrome, numeral consistency |
| Teacher / student | Technical mechanics, machine elements, circuits | clear textbook conventions |
| Consultant / manager | Process and system diagrams in decks | native PowerPoint shapes, themes |

## 3. Hosts and platforms

| Host | Technology | Platforms | Interaction model | Status |
|---|---|---|---|---|
| PowerPoint COM add-in | .NET Framework 4.8, in-process engine | Windows | on-slide (overlay, floating bar, pie menu) + sidebar | planned (phase 2) |
| PowerPoint web add-in | Office.js + engine as .NET WebAssembly | Windows, macOS, PowerPoint on the web (iPad read-only) | interactive sidebar preview editor; native shapes written to the slide | in progress |
| LibreOffice extension | Python-UNO `.oxt` + engine as .NET process | Windows, macOS, Linux; Impress, Draw, Calc | on-canvas (mouse/key handlers, overlay layer) + sidebar panel | planned (phase 3) |

**COM precedence:** the web add-in manifest declares `EquivalentAddins` with the COM ProgId
`InventaSketch.AddIn.Connect`; on Windows, Office hides the web add-in while the COM add-in is installed. Both never
show duplicate UI. See [ADR-0004](../../adr/0004-com-precedence.md).

Capability differences (freeform, arrowheads, pattern fill, overlay, metadata) are maintained in the
[host capability matrix](../hosts/capability-matrix.md).

## 4. Interaction model

### 4.1 Insert
- Library panel: search (EN+DE names and keywords), categories, favourites, recents, thumbnails.
- Click an item → outline follows the pointer ("insert at pointer") and snaps; click inserts at default size,
  drag sets a custom size, `Esc` cancels. Drag-and-drop from the library works too.
- No wizard, no mandatory parameter dialog.

### 4.2 Direct manipulation
- Select (click, Ctrl/Shift-click, marquee: left→right = fully enclosed, right→left = touching).
- Move with live snapping; `Alt` suspends snapping.
- **Corner handles scale uniformly, side handles scale non-uniformly**; rotation handle with 15° steps (`Shift`).
- Point objects (arrows, lead lines, dimensions, wires) show endpoint/control handles; endpoints attach to anchors
  of other objects and follow them ("drag keeps connections").
- Keyboard: `Del`, `Ctrl+Z/Y`, `Ctrl+D` duplicate, `R` rotate 90°, `X`/`Y` mirror, arrows nudge (`Shift` = 10×),
  `G` group, `Shift+F10` pie menu, `Esc` deselect.

### 4.3 Floating context bar
Appears next to the selection with the most relevant actions (view/representation, flip, reference numeral, line
style, fill/hatch, arrowheads). Fully opaque near the pointer, fades with distance and disappears until the next
selection; never steals focus; can be disabled.

### 4.4 Pie menu
Right-click (or `Shift+F10`) opens a radial menu with exactly the commands for the clicked object/part.
Delete is always on top; unavailable commands are greyed (stable positions); toggles show their state.
Examples: gear → views, hub symbol, mesh with…, add shaft, numeral; shaft → add bearing/gear, hollow, extend;
connector → routing, arrowheads, label; wall → door, window, thickness.

### 4.5 Sidebar preview editor
Mandatory where on-slide interaction is impossible (Office web add-in) and available everywhere for complex edits:
a live stage with the same handles, snapping, pie menu and floating bar as on the slide; inspector for optional
parameters and style; apply to slide on release (one undo step per action).

### 4.6 Representations (views)
Objects can have several representations sharing one identity (e.g. gear: stick symbol, side view, section,
axial conventional, simplified teeth). Switching view keeps id, position, numeral and links.

### 4.7 Snapping
Priority: semantic (gear mesh on pitch circles incl. internal ring meshing, hub on shaft axis, collinear shafts,
concentric centres, electrical ports) → points (centre, corners, edge midpoints, ends) → alignment guides
(equal x/y with other objects and the page) → grid (metric/imperial). Visual guides for every snap.

### 4.8 Look and feel
Clean, quiet, modern UI; light/dark/auto theme (auto follows Office/OS); German UI when the host language starts
with `de`, otherwise English; manual override remembered; compact layout for narrow task panes; keyboard
accessible; tooltips with previews.

## 5. Document model

Canonical, versioned JSON (`inventasketch-document` v1). Pages with z-order; objects with id, type, transform
(centre, rotation, flip), box size, optional points + attachments, view, optional parameters, style overrides,
text, reference numeral, group id, layer; layers (incl. helper layers excluded from patent export); reference-sign
dictionary; settings (units, grid, profile). Units mm, y down. Details: [architecture overview](../architecture/overview.md).

In host documents the model travels with the shapes: per-group tags (id/type) plus a compressed model payload
(`isk1:` GZip+Base64), mirrored into document-level storage. Externally edited shapes are detected; the visible
result is never discarded.

## 6. Module catalogue

Every module supports Quick Sketch first. "Views" lists representations; parameters are optional.

### 6.1 Core drawing & annotation (`ann.*`) – in progress
Lines, polylines, arrows (straight, curved, elbow, double; heads: triangle, open, stealth, diamond, oval, tick,
half; sizes), lead lines (straight/curved/wavy) with numerals, text boxes, labels, callouts, balloons, brackets
and braces, dimensions (linear, aligned, angle, radius; ticks or arrows), centre lines, break lines, section lines
(A–A), detail circles, coordinate systems, hatched regions (steel 45°, cross, horizontal/vertical, dense/wide,
dotted, insulation; islands respected), flowchart starters (process, decision, terminator, data, document,
connector, step labels "S100").

### 6.2 Patent Studio (`patent.*`) – in progress
Reference numerals with lead lines, numbering scheme (10/20/30, sub-parts 11/12), single source of truth across
figures, reference sign list export, sheet/usable-area helper, page numbers ("1/3"), FIG labels, monochrome
export, **preflight** with versioned office profiles (DPMA, EPO, PCT, USPTO, common-safe) whose rules cite their
legal source (see [patent drawing rules](../research/patent-drawing-rules.md)). Findings: error / warning / info /
manual, with object navigation and suggestions. A clean preflight is an aid, not legal certification.

Key facts from research (2026-11): margins top 2.5 / left 2.5 / right 1.5 / bottom 1.0 cm on A4 at all four
offices; usable area max. 26.2 × 17.0 cm; character height ≥ 0.32 cm; EPO accepts colour/greyscale for electronic
filings since 2025-10-01 (OJ EPO 2025, A49); PCT Rule 11.13 still requires drawings without colouring; USPTO
37 CFR 1.84(h) forbids centre lines; "1/3" sheet numbering mandatory only for PCT; a single figure gets no "FIG."
label at EPO/PCT/USPTO; DPMA electronic PDFs: black and white, fonts Times/Helvetica/Courier/Symbol.

### 6.3 Mechanical Studio (`mech.*`) – in progress
Free assembly of gearboxes and drives by drag & drop, free scaling and semantic snapping.
- **Gears:** spur (external/internal), helical indication, bevel, worm/worm wheel, rack, planetary set (sun,
  planets, ring, carrier), bevel differential, multi-stage trains. Views: stick (VDI 2127/ISO 3952 style "I"
  symbol with hub symbols fixed/sliding/loose), side, section (hatched), axial conventional (tip solid, pitch
  dash-dot, ISO 2203 style), simplified teeth.
- **Shafts:** solid, hollow, stepped; line or body view; axis anchor for snapping.
- **Bearings:** deep-groove, angular-contact, tapered, cylindrical, needle, thrust, plain; stick, simplified
  (ISO 8826 style) and detailed views; fixed/floating support symbols.
- **Couplings, clutches, brakes, synchronisers, universal joints, housing/ground.**
- **Fasteners and joints:** screws, nuts, washers, rivets, keys, splines, welds, adhesive, solder, press fit.
- **Springs, cams, flywheels, belts/pulleys, chains/sprockets, linear guides, seals.**
- **Drive arrows:** rotation, torque, power flow.
- Optional engineering data: module, tooth count, ratio display, centre-distance suggestion (d = m·z,
  a = m(z₁+z₂)/2), rotation direction. Clearly marked as schematic vs. approximate vs. validated.

### 6.4 Technical mechanics (`ann.mech*` starter set, later module `mechanics.*`) – in progress / planned
Beams, rods, trusses, pin/roller/fixed supports, point and distributed loads, moments, centre of gravity,
springs, dampers, masses, free-body cut boundaries, coordinate systems, shear/moment diagram primitives.

### 6.5 Kinematics & mechanisms (`kin.*`) – planned
Links, revolute/prismatic/spherical/cam joints, four-bar, slider-crank, scotch yoke, toggle, cam-follower;
drag-to-simulate with a 2D constraint solver; joint/angle limits written into the drawing; motion traces and
ghosting (start/intermediate/end positions as dashed overlays for patent figures); export as slide sequence for
animation; degree-of-freedom indicator. No dynamics simulation implied.

### 6.6 Electrical & electronics (`elec.*`) – planned
IEC-style symbols drawn independently (resistor, capacitor, inductor, diode, LED, transistor, MOSFET, IGBT,
op-amp, gates, switches, relays, fuses, breakers, RCD, sources, batteries, ground/PE, motors, generators,
transformers, sensors, actuators, connectors, ECUs); wires snapping to pins, junction dots vs. crossings, net
labels, buses, orthogonal/45°/free routing; simple ERC (open pins, unconnected nets). House installation:
outlets, switches, lights, distribution board, circuits on a floor-plan layer; single-line diagrams.
PCB illustration: board outline, pads, tracks, vias, layers, silkscreen (visual planning only).

### 6.7 Electric machines & drives (`emach.*`) – planned
PMSM, EESM, induction (ASM), switched reluctance, axial-flux and radial-flux arrangements, inner/outer rotor;
stator/rotor cross-sections with slots, windings, magnets, air gap, shaft, housing, cooling; concentric snapping;
inverter, DC link, battery, gearbox and differential as e-drive assemblies; flux/rotation/torque arrows.

### 6.8 Automotive, ADAS & HMI (`auto.*`) – planned
Vehicle silhouettes (car, SUV, van, truck, bus, motorcycle, bicycle, trailer; top/side/front/rear), subsystems
(axles, steering, battery, drive units), humans (standing, walking, seated, hands, gaze), interior/exterior
interaction zones; roads and lanes (straight, curved, intersections, roundabouts, markings, signs, signals,
barriers), trajectories with time stamps, corridors, safety distances, conflict points, TTC markers; sensors
(camera, radar, lidar, ultrasonic) with editable fields of view anchored to the vehicle, detections (2D/3D boxes,
labels, confidence, occupancy grid); HMI: cluster, centre display, HUD, buttons, knobs, screen wireframes and flows.

### 6.9 Architecture & building (`arch.*`) – planned
Walls with draggable endpoints and thickness, joins at corners, rooms from closed wall loops with name/area,
doors and windows hosted in walls, stairs, columns, furniture, sanitary, kitchen, electrical and HVAC symbols,
dimensions, levels; optional scale. Not BIM.

### 6.10 Diagrams: UML, ER, flow, systems (`diag.*`) – planned
Flowcharts, swimlanes, UML (class, sequence, activity, state, component, deployment, use case), ER (entities,
attributes, keys, cardinalities), block/architecture diagrams, state machines, data flow; ports, orthogonal routing
with jump-overs, optional auto-layout that never silently destroys manual placement.

### 6.11 Maps, roads & routes (`map.*`) – planned
Schematic maps, roads, routes and waypoints, bus/tram/rail lines and stops, zones and geofences; import of
GeoJSON/GPX/KML (licence-aware); scale bar.

### 6.12 Data & engineering charts (`chart.*`) – planned
XY/line/scatter/bar/area, polar, histograms, characteristic curves (torque–speed, efficiency maps), Bode plots,
tolerance bands and limits; monochrome-safe series styles; data from CSV/Excel; vector export.

### 6.13 Further domain modules (proposed)
Fluid power & process (ISO 1219 style hydraulics/pneumatics, P&ID), thermodynamics (cycles, heat exchangers,
T-s/p-v diagrams), aerodynamics (airfoils, streamlines), acoustics/NVH, tribology & lubrication (oil paths, sumps,
seals), robotics (arms, frames, reach envelopes, work cells), manufacturing & production (lines, stations, VSM),
chemistry & biochemistry (apparatus, reactions, pathways), physics & optics (rays, lenses, fields, waves),
mathematics (axes, functions, geometry constructions), computing & networks (servers, buses, topologies),
civil & infrastructure (bridges, utilities, profiles), energy systems (grid, PV, wind, hydrogen), medical
devices, safety & reliability (FMEA, fault trees), systems engineering (SysML-style blocks, requirements tracing),
logistics, agriculture, marine & aerospace. Each becomes a module when a concrete scope and user demand exist.

## 7. Cross-cutting features

- **Variants & states:** baseline vs. invention, operating modes, before/after, A/B/C; visibility per variant;
  visual diff (added/removed/changed). Planned.
- **Multi-view:** shared identity across views (e.g. gear in stick, side and axial view). Planned.
- **Layers:** mechanics, electrics, sensors, dimensions, numerals, helpers. In progress (model).
- **Import & recognition:** SVG, PDF, PNG/JPEG, TIFF, DXF, CSV; contour detection on images to hatch closed regions,
  assistive vectorisation and OCR of numerals — always confirmable, original kept. Planned.
- **Export:** native shapes, SVG, PDF (vector), PNG/TIFF (300–600 dpi, 1-bit option), reference sign list.
  SVG shipped in core; others planned.
- **Libraries:** user libraries, reusable assemblies, import/export of library packages. Planned.

## 8. Non-functional requirements

- Smooth interaction with 1,000 simple objects; deterministic export; no data loss on save/reopen.
- Safe SVG import (sanitising), no network dependency for editing.
- ≥ 80 % coverage target for critical domain logic; host adapters tested with fakes plus documented manual checks.
- Accessibility: full keyboard operation of primary workflows, visible focus, sufficient contrast.

## 9. Acceptance workflows (reference)

1. Insert, move, rotate, resize generic shapes; format natively.
2. Gearbox: shafts, two gear pairs, bearings, torque arrows — no parameter entry; switch to axial/section views.
3. Free-body diagram: beam, supports, loads, centre of gravity.
4. Four-bar linkage with angular limits, animated; ghosted positions as static figure.
5. Circuit with wires attached to pins; junctions vs. crossings.
6. Electric motor cross-section with rotor, stator, air gap.
7. Vehicle, road, sensor FoV, trajectory.
8. House floor plan with walls, rooms, doors, windows.
9. Process diagram with swimlanes.
10. Patent figure: numerals, hatching, preflight, monochrome export.
11. Export native objects + SVG/PDF; reopen and verify.

## 10. Out of scope

3D CAD, FEM/CFD, multibody dynamics, manufacturing-grade PCB output, BIM, GIS-grade topology, full navigation,
legal certification of patent drawings.

## 11. Sources

See [research/patent-drawing-rules.md](../research/patent-drawing-rules.md) (official office sources) and
[research/tools-and-host-apis.md](../research/tools-and-host-apis.md) (tool landscape, host APIs, licences).
