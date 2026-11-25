# Process & thermal module (`process`)

P&ID and flow sheet symbols beyond fluid power (vessels, columns, reactors, heat exchangers, pumps, compressors,
valves, instrument bubbles with tags, process and signal lines), HVAC process items of air handling units, and
thermodynamic cycle diagrams (T-s, p-v, log p-h) for slides and quick technical sketches.

Symbols are **drawn independently** following common flow sheet practice (the conventions known from ISO 10628-2,
ISA-5.1 and ISO 14617-6 served as orientation only). No standard texts or tables are reproduced and **no standard
compliance is claimed**. This is a sketching module, not a process simulator and not a property database.

## Views

| View id | Label (EN / DE) | Icon | Used by |
|---|---|---|---|
| `symbol` | P&ID symbol / R&I-Symbol | `IconIds.Symbol` | every equipment, fitting, instrument and line (default) |
| `block` | Block (block flow diagram) / Block (Grundfließschema) | `IconIds.Block` | equipment (vessels, heat transfer, machines): a rectangle with the tag and the equipment name in the UI language |
| `chart` | Diagram / Diagramm | `IconIds.Chart` | the three cycle diagrams |

Switching between `symbol` and `block` keeps the box. Nozzle ids are the same in both views; in the block view a
nozzle that lies inside the symbol box (on a dished head) moves to the nearest edge of the block.

## Conventions

- **Quick Sketch first:** every object is insertable without parameters (every parameter has a default); free (also
  non-uniform) scaling changes `Size`, never parameters. Every drawing stays inside its box, except the optional
  equipment tag, which is written outside the box (like the fluid module's designator).
- **Design box:** symbols are authored in design millimetres and stretched to the object size. Parameters that add
  parts (vessel support, reactor agitator/jacket, three-way valve actuator) grow the design box; the object size
  follows it (inspector and context actions alike), so symbols are never squeezed.
- **Nozzles** are `AnchorRole.Port` anchors with stable ids (`N1`…, `S`/`D` suction/discharge, `T1`/`T2` tube side,
  `S1`/`S2` shell side, `A1`/`A2` air side, `W1`/`W2` water side …). Most lie on the box edge, so docked lines leave
  them perpendicular.
- **Equipment tag** (`tag`, `showTag`): empty by default. "Show tag" fills in a suggestion (`V-101`, `P-101`, `E-101` …);
  the text is written outside the box on the page side with the fewest nozzles (below, right, above, left), moved off a
  line leaving the middle of that side, and kept upright when the symbol is rotated.
- **Instrument bubble tag** (`tag`, default `TIC 101`): split into the function letters (top, bold) and the loop number
  (bottom): `TIC 101` → TIC / 101, `PT-205A` → PT / 205A.
- Units mm, y down, angles clockwise; line widths 0.35 mm (outlines), 0.5 mm (main process lines), 0.25/0.18 mm details.

## Object types

| Id | Name (EN / DE) | Design box | Nozzles | Parameters |
|---|---|---|---|---|
| `process.vessel.vertical` | Vertical vessel / Behälter, stehend | 14x30 (+5 with support) | N1 top, N2 bottom, N3 left, N4 right | `heads` ellipsoidal/flat/conical, `support` none/legs/skirt, `level` |
| `process.vessel.horizontal` | Horizontal vessel / Behälter, liegend | 32x14 (10 without saddles and boot) | N1, N2 heads, N3 top, N4 bottom (boot) | `saddles` (true), `boot`, `level` |
| `process.vessel.tank` | Storage tank / Lagertank | 24x20 | N1 roof, N2, N3 near the bottom | `roof` cone/dome/floating, `level` |
| `process.column` | Column / Kolonne | 12x50 | N1 top, N2 bottom, N3 feed, N4 reflux, N5 reboiler return | `internals` trays/packed, `trays` (8, 2–30), `beds` (2, 1–4) |
| `process.reactor` | Stirred tank reactor / Rührkesselreaktor | 16x30 | N1, N3 top nozzles, N2 bottom, J1/J2 with jacket | `agitator` (true), `jacket` |
| `process.hx.shelltube` | Shell-and-tube heat exchanger / Rohrbündel-Wärmeübertrager | 32x12 | T1, T2 tube side, S1, S2 shell side | `bundle` utube/fixed |
| `process.hx.plate` | Plate heat exchanger / Plattenwärmeübertrager | 14x16 | A1, A2, B1, B2 | `plates` shown (7, 3–15) |
| `process.hx.general` | Heat exchanger (general symbol) / Wärmeübertrager (allgemein) | 12x12 | P1, P2 coil, S1, S2 shell | – |
| `process.hx.aircooler` | Air cooler (fin-fan) / Luftkühler (Rippenrohr mit Ventilator) | 24x14 | P1, P2 | – |
| `process.hx.coolingtower` | Cooling tower / Kühlturm | 20x24 | W1 hot water in, W2 basin outlet | `draft` natural/mechanical |
| `process.pump.centrifugal` | Centrifugal pump / Kreiselpumpe | 12x12 | S suction, D discharge (tangential) | `base` (true) |
| `process.pump.displacement` | Positive displacement pump / Verdrängerpumpe | 12x12 | S, D | `kind` gear/screw/piston/diaphragm |
| `process.compressor` | Compressor / Verdichter | 14x12 | S, D | `kind` dynamic/reciprocating |
| `process.turbine` | Turbine / expander / Turbine / Entspannungsmaschine | 14x12 | I, O | – |
| `process.fan` | Fan / Ventilator | 12x12 | I, O | `kind` radial/axial |
| `process.motor` | Electric motor (drive) / Elektromotor (Antrieb) | 10x10 | shaft | – |
| `process.valve.manual` | Valve (manual) / Armatur (handbetätigt) | 10x6 | 1, 2 | `kind` gate/globe/ball/butterfly, `closed` |
| `process.valve.control` | Control valve / Stellventil (Regelventil) | 12x13 | 1, 2, S (signal on the actuator) | `actuator` diaphragm/motor/solenoid/piston, `fail` none/open/close/last (FO/FC/FL) |
| `process.valve.check` | Check valve / Rückschlagarmatur | 10x6 | 1, 2 (flow 1 → 2) | – |
| `process.valve.safety` | Safety / relief valve / Sicherheitsventil | 10x12 | in (bottom), out (side) | – |
| `process.valve.threeway` | Three-way valve / Dreiwegeventil | 9x7 (9x12 motorised) | A, AB, B, S when motorised | `actuated` |
| `process.fitting.reducer` | Reducer / Reduzierstück | 8x6 | 1, 2 | `eccentric` |
| `process.fitting.strainer` | Strainer / filter / Schmutzfänger / Filter | 10x8 | 1, 2 | `kind` filter/y |
| `process.instrument.bubble` | Instrument (tag bubble) / Messstelle (PLT-Stelle) | 12x12 | B, T, L, R | `tag` (TIC 101), `location` field/central/local/behind, `shape` discrete/shared/computer/plc |
| `process.instrument.orifice` | Flow element (orifice / venturi) / Durchflussmessglied (Blende / Venturi) | 10x6 | 1, 2 | `kind` orifice/venturi |
| `process.line` | Process line / Prozessleitung (Fließlinie) | points | rails along the routed path | `kind` main/secondary/traced/future, `arrow`, `lineNo`, `route` orthogonal/free, `hops` |
| `process.line.signal` | Signal line / Signalleitung (Wirkungslinie) | points | rails | `kind` connection/electric/pneumatic/capillary/data, `route` |
| `process.hvac.coil` | Air heating / cooling coil / Lufterhitzer / Luftkühler | 10x12 | A1, A2 air, W1, W2 water | `kind` heating (+)/cooling (−) |
| `process.hvac.damper` | Air damper / Luftklappe | 10x10 | A1, A2 | `motorised` |
| `process.hvac.filter` | Air filter / Luftfilter | 10x10 | A1, A2 | `kind` panel/bag |
| `process.hvac.recovery` | Heat recovery / Wärmerückgewinnung | 10x20 | S1, S2 supply (lower), E1, E2 extract (upper) | `kind` plate/wheel |
| `process.hvac.humidifier` | Humidifier / Luftbefeuchter | 10x12 | A1, A2, W1 | `kind` steam/spray |
| `process.chart.ts` | T-s diagram (cycle) / T-s-Diagramm (Kreisprozess) | 80x60 | – | `cycle` rankine/carnot, `superheat`, `dome`, `labels`, `caption` |
| `process.chart.pv` | p-v diagram (cycle) / p-v-Diagramm (Kreisprozess) | 80x60 | – | `cycle` otto/diesel/carnot, `ratio` (8), `cutoff` (2), `pressureRatio` (2.5, advanced), `gamma` (1.4, advanced), `efficiency`, `labels`, `caption` |
| `process.chart.ph` | log p-h diagram (refrigeration cycle) / log p-h-Diagramm (Kältekreisprozess) | 80x60 | – | `ratio` p_high/p_low (4, 1.5–7.5), `superheat`, `subcool`, `labels`, `caption` |

Instrument location bars: field = no line, control room (accessible) = solid line, local panel = double line, behind
the panel = dashed line. Device shapes: discrete instrument = circle, shared display / DCS = circle in a square,
computer function = hexagon, PLC = diamond in a square.

## Thermodynamic diagrams

The diagrams are teaching sketches with **normalised axes without scale values**. The cycle states are numbered 1–4,
small arrowheads show the direction of each change of state, and an optional caption names the cycle.

- **p-v (air-standard cycles, ideal gas, constant heat capacities):** isentropes `p·v^γ = const`, isotherms
  `p·v = const`, `v2 = 1`, `p1 = 1`.
  - Otto: 1→2 isentropic compression by `r`, 2→3 constant volume (`p3 = α·p2`), 3→4 isentropic, 4→1 constant volume;
    `η = 1 − r^(1−γ)` (r = 8, γ = 1.4: 56.5 %).
  - Diesel: 2→3 constant pressure up to the cut-off ratio `r_c`; `η = 1 − r^(1−γ)·(r_c^γ − 1)/(γ·(r_c − 1))`.
  - Carnot: overall volume ratio `r = v4/v2` split evenly into the adiabatic and isothermal ratios (`√r` each);
    `η = 1 − T_low/T_high`.
  The efficiency is shown only on request (`efficiency`) and only for these p-v cycles, where it follows from the model.
- **T-s:** the saturation dome is a qualitative shape (`s = s_c ∓ a·√τ ∓ b·τ`, `τ = (T_c − T)/(T_c − T_0)`), not
  steam-table data. Clausius-Rankine: 1→2 pump (drawn slightly exaggerated), 2→3 heating, evaporation and optional
  superheating (ideal-gas isobar `T = T_g·exp((s − s_g)/c_p)`), 3→4 isentropic expansion into the wet region,
  4→1 condensation. Carnot: a rectangle inside the dome. No efficiency is stated (no scale).
- **log p-h (vapour compression):** qualitative dome; 1→2 compression along `h2 − h1 = c·ln(p2/p1)` (from `dh = v·dp`
  with `p·v ≈ const`), 2→3 condensation (optionally subcooled), 3→4 isenthalpic throttling, 4→1 evaporation (optionally
  superheated). The pressure ratio is limited so that condensation stays below the critical point.

The formulas are in `Cycles` (public, unit tested).

## Library

One default item per type (id = type id) plus presets: vertical vessel on legs, vertical separator with level,
hopper (conical bottom), three-phase separator with boot, dome and floating roof tanks, packed column, jacketed
reactor, fixed tube sheet exchanger, mechanical draft cooling tower, screw/piston/diaphragm pumps, reciprocating
compressor, axial fan, globe/ball/butterfly valves, gate valve normally closed, motor-operated and solenoid valves,
fail-closed control valve, motorised three-way valve (HVAC category), eccentric reducer, Y-strainer, instrument
presets (TIC control room, PI, FT, LIC on the DCS, PT, PLC function), venturi, secondary/traced/future lines and a
line with flow arrow, pneumatic/impulse/capillary/data signal lines, cooling coil, motorised damper, bag filter,
rotary wheel, spray humidifier, supply/extract air fan (HVAC category), Carnot and saturated Rankine T-s, Diesel and
Carnot p-v and a heat pump cycle (log p-h). Search keywords exist in both languages (e.g. "Behälter", "Kolonne",
"Wärmeübertrager", "Pumpe", "Messstelle", "Stellventil", "Kreisprozess", "Wärmepumpe").

## Lines

`process.line` and `process.line.signal` are point objects: corners can be inserted, the routed path is a rail (branches
and instrument connections dock anywhere on it, with a junction dot), routing is orthogonal by default. An attached end
leaves its nozzle perpendicular to the box edge; a nozzle inside the box (on a dished head) leaves towards the nearest
edge. Process lines hop over earlier process lines (`hops`, Core `Crossings`) and report crossing lines as dependents.
The heat-traced kind adds a dashed tracer beside the line; the line number is written along the longest segment.
Signal marks (slashes, crosses, circles) are spaced 8 mm and kept off the corners.

## Context actions

Tag (show / enter), cycle choices (heads, support, roof, internals, bundle, draft, pump/compressor/fan kind, valve kind,
actuator, fail-safe position, instrument location and device, filter, recovery, humidifier, chart cycle), toggles
(level, saddles, boot, agitator, jacket, base, closed, motorised, eccentric, arrow, hops, dome, superheat, subcool,
efficiency, state numbers, caption), steps (trays, packed beds, plates, compression/pressure ratio), line number and
instrument tag inputs, orthogonal/straight routing.

## Host notes

All output uses native primitives (lines, polylines, polygons, rectangles, ellipses, cubic paths, text), so every
host can draw it natively; no vector picture is needed. Process lines depend on crossing lines (hops). Charts are not
parts (`IsPart = false`), nor are lines.

## Tests and gallery

`tests/InventaSketch.Modules.Process.Tests`: registration, id conventions, default library item per type, library
items resolve with their parameters, bilingual names/labels/options/actions, shared pictograms, defaults for every
parameter, search in both languages, companions; every library item rendered in every view in both languages inside
its box (also bare, stretched, squashed, scaled), every parameter value changes the drawing, block view text and tag,
external tag placement and upright text, instrument tag split and location bars, ports (named, unique, inside the box,
mostly on the edge, same ids in every view, scaled with the box), snapping a line onto a nozzle, docked lines following
equipment and leaving nozzles perpendicular, rails, hops, line kinds, signal marks, the JSON round trip of a sample
flow sheet, and the cycle formulas (Otto, Diesel, Carnot p-v and T-s, Rankine, refrigeration). The gallery test writes
`gallery/*.html` into the test output folder (every type in each view, stretched, presets, a distillation sample and
an air handling unit sample).
