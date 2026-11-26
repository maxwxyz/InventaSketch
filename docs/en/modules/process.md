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
| `process.cyclone` | Cyclone separator / Zyklonabscheider | 14x30 | N1 tangential inlet (left), N2 gas outlet (vortex finder, top), N3 solids outlet (bottom) | – |
| `process.silo` | Silo (bulk solids) / Silo (Schüttgut) | 16x34 | N1 filling (roof apex), N2 discharge (hopper outlet) | `legs` (true), `level` (fill level) |
| `process.hx.shelltube` | Shell-and-tube heat exchanger / Rohrbündel-Wärmeübertrager | 32x12 | T1, T2 tube side, S1, S2 shell side | `bundle` utube/fixed |
| `process.hx.plate` | Plate heat exchanger / Plattenwärmeübertrager | 14x16 | A1, A2, B1, B2 | `plates` shown (7, 3–15) |
| `process.hx.general` | Heat exchanger (general symbol) / Wärmeübertrager (allgemein) | 12x12 | P1, P2 coil, S1, S2 shell | – |
| `process.hx.aircooler` | Air cooler (fin-fan) / Luftkühler (Rippenrohr mit Ventilator) | 24x14 | P1, P2 | – |
| `process.hx.coolingtower` | Cooling tower / Kühlturm | 20x24 | W1 hot water in, W2 basin outlet | `draft` natural/mechanical |
| `process.pump.centrifugal` | Centrifugal pump / Kreiselpumpe | 12x12 | S suction, D discharge (tangential); shaft hub right (opposite the suction) | `base` (true) |
| `process.pump.displacement` | Positive displacement pump / Verdrängerpumpe | 12x12 | S, D; shaft hub on top | `kind` gear/screw/piston/diaphragm |
| `process.compressor` | Compressor / Verdichter | 14x12 | S, D; shaft hub on top | `kind` dynamic/reciprocating |
| `process.turbine` | Turbine / expander / Turbine / Entspannungsmaschine | 14x12 | I, O; output shaft stub below (axis + hub) | – |
| `process.fan` | Fan / Ventilator | 12x12 | I, O; shaft hub right (radial) or on top (axial) | `kind` radial/axial |
| `process.motor` | Electric motor (drive) / Elektromotor (Antrieb) | 10x10 | shaft (port, kept for old attachments); shaft stub right (axis + hub) | – |
| `process.generator` | Generator (P&ID, driven) / Generator (R&I, angetrieben) | 10x10 | el (power terminal, right); shaft hub on top | – |
| `process.valve.manual` | Valve (manual) / Armatur (handbetätigt) | 10x6 | 1, 2 | `kind` gate/globe/ball/butterfly, `closed` |
| `process.valve.control` | Control valve / Stellventil (Regelventil) | 12x13 | 1, 2, S (signal on the actuator) | `actuator` diaphragm/motor/solenoid/piston, `fail` none/open/close/last (FO/FC/FL) |
| `process.valve.check` | Check valve / Rückschlagarmatur | 10x6 | 1, 2 (flow 1 → 2) | – |
| `process.valve.safety` | Safety / relief valve / Sicherheitsventil | 10x12 | in (bottom), out (side) | – |
| `process.valve.threeway` | Three-way valve / Dreiwegeventil | 9x7 (9x12 motorised) | A, AB, B, S when motorised | `actuated` |
| `process.fitting.reducer` | Reducer / Reduzierstück | 8x6 | 1, 2 | `eccentric` |
| `process.fitting.strainer` | Strainer / filter / Schmutzfänger / Filter | 10x8 | 1, 2 | `kind` filter/y |
| `process.valve.steamtrap` | Steam trap / Kondensatableiter | 10x6 | 1, 2 | – |
| `process.rupturedisc` | Rupture disc / Berstscheibe | 8x10 | in (bottom), out (top) | – |
| `process.sightglass` | Sight glass / Schauglas | 10x6 | 1, 2 | – |
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

New symbols (drawn independently): the steam trap is a round body with the float (filled ball) and a condensate level;
the rupture disc a domed disc between two holder flanges, bulging towards the outlet; the sight glass a round housing
with two glass panes; the cyclone a cylinder with a tangential inlet, a cone, the dashed vortex finder and a swirl arrow;
the silo a cone-roof cylinder with a hopper bottom on legs.

Instrument location bars: field = no line, control room (accessible) = solid line, local panel = double line, behind
the panel = dashed line. Device shapes: discrete instrument = circle, shared display / DCS = circle in a square,
computer function = hexagon, PLC = diamond in a square.

## Drive shafts (a motor drives the pump)

Pumps, fans, compressors, turbines and the drive motor have a real mechanical shaft with the same contract as the shafts
and drives of the mechanical and machines modules ([mechanical-drives.md](mechanical-drives.md)):

- **Driven machines** (`process.pump.centrifugal`, `process.pump.displacement`, `process.compressor`, `process.fan`,
  `process.generator`) expose a `Hub` anchor (id `hub`) where the shaft leaves the body. A shaft axis through this point carries the machine:
  a mechanical shaft (`mech.shaft`), the shaft of an e-machine, the shaft stub of the machines motor symbol, or the stub
  of the process motor or turbine.
- **Drives** (`process.motor`, `process.turbine`; `ObjectType.DrivesShaft`) expose their shaft stub as an `Axis` segment
  (id `shaft`) with the hub and an `End` mark at the outer end. A driven machine docks with its hub on the stub; a
  mechanical shaft continued from the shaft end carries the drive, and moving the drive takes the shaft (and everything
  on it) along. "Build +" continues the shaft only at the outer end.
- The hub has no bore radius (0), so a machine seated on a shaft keeps its size. Docking uses the normal snapping (hub
  onto an axis) and the core coupling ("moves along"). In the block view the hub moves to the nearest block edge.
- The shaft sides follow the symbols: centrifugal pump and radial fan on the right (opposite the axial suction), the other
  driven machines on top, the turbine shaft below (a compressor, pump or generator below the turbine docks without
  turning), the motor shaft to the right. Flip a symbol to dock on the other side.
- **Values:** every machine with a shaft carries speed *n* (`speed`), torque *T* (`torque`) and shaft power *P* (`power`)
  with *P* = 2π·*n*·*T* [S18]. A machine shares *n* with the element it is coupled to (carried or concentric). *T* is passed
  over a direct one-to-one coupling (a motor on the pump hub, neither coupled to a third element with a torque) and from
  the mechanical shaft or coupling the machine sits on when that shaft line has at most two members that take or give
  torque (the rule of the mechanical module; with a branch no torque is passed). The drive's power thus becomes the
  pump's shaft power: a motor at 1450 min⁻¹ and 50 N·m gives the pump *P* = 7.59 kW; with *V̇* = 100 m³/h and *H* = 20 m
  the pump efficiency follows as η = 71.8 %.
- **Power without a speed:** on a shaft line with exactly two members that take or give torque (the drive and the driven
  machine, shafts and couplings between them) the shaft power *P* itself is passed as well (`process.shaft.power`), when
  the other member has a mechanical power *P* (process machines, the machines motors and e-machines). A compressor's power
  demand thus reaches its motor, and a turbine's output its generator, before any speed is entered. With a branch or a
  gear stage on the line nothing is passed (the power splits in an unknown way).

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
rotary wheel, spray humidifier (the HVAC items named "… (AHU)"), Carnot and saturated Rankine T-s, Diesel and
Carnot p-v and a heat pump cycle (log p-h), and a silo with fill level (`process.silo.level`). Search keywords exist in both languages (e.g. "Behälter", "Kolonne",
"Wärmeübertrager", "Pumpe", "Messstelle", "Stellventil", "Kreisprozess", "Wärmepumpe").

### Overlaps with other modules (one element, one item)

- The default fan item `process.fan` also answers to "supply air", "extract air", "Zuluft", "Abluft"; the former
  `process.hvac.fan` (the same fan once more, in the HVAC category) steps back in its favour (`LibraryItem.Alternative`)
  and is still found by id.
- The air handling items are sections of an AHU schematic (air path left to right), not the duct parts of a building
  plan (building services `bs.vent.damper`, `bs.vent.unit`): they are kept and named "… (AHU)" / "… (RLT)", e.g. "Air
  damper (AHU)" next to the building services' "Duct damper".
- `process.motor` is the P&ID drive symbol that docks on a pump or fan shaft, not the drive train block of the machines
  module (`machines.block.motor`, "E-motor (block)") nor the circuit symbol (`elec.motor`): kept and named "Electric
  motor (P&ID drive)" / "Elektromotor (R&I-Antrieb)".
- `process.generator` is the P&ID generator that docks below a turbine, not the energy module's generator set
  (`energy.gen.genset`, engine and generator in one) nor an e-machine of the machines module: named "Generator (P&ID,
  driven)" / "Generator (R&I, angetrieben)".

## Lines

`process.line` and `process.line.signal` are point objects: corners can be inserted, the routed path is a rail (branches
and instrument connections dock anywhere on it, with a junction dot), routing is orthogonal by default. An attached end
leaves its nozzle perpendicular to the box edge; a nozzle inside the box (on a dished head) leaves towards the nearest
edge. Process lines hop over earlier process lines (`hops`, Core `Crossings`) and report crossing lines as dependents.
The heat-traced kind adds a dashed tracer beside the line; the line number is written along the longest segment.
Signal marks (slashes, crosses, circles) are spaced 8 mm and kept off the corners.

**Build "+" (connections).** Nozzles and pipe ports offer a process line (`process`), instrument bubbles and the actuator
port S of control valves a signal line (`instrument`), the generator terminal `el` a power cable (`electric.ac`, the
energy module's `energy.line.cable` when that module is registered), then the equipment, valves or instruments that
connect there. The
drive shaft of a motor or turbine is a mechanical coupling without a line (see "Drive shafts"); a driven machine's free
hub offers a shaft through it. The free end of a process or signal line offers "+" to
continue it or to dock the next element.

## Context actions

Tag (show / enter), choices ▾ (heads, support, roof, internals, bundle, draft, pump/compressor/fan kind, valve kind,
actuator, fail-safe position, instrument location and device, filter, recovery, humidifier, chart cycle, line routing), toggles
(level, saddles, boot, agitator, jacket, base, closed, motorised, eccentric, arrow, hops, dome, superheat, subcool,
efficiency, state numbers, caption), steps (trays, packed beds, plates, compression/pressure ratio), line number and
instrument tag inputs, orthogonal/straight routing.

## Engineering values (Calc 4)

Values are optional: every symbol still inserts and scales without them. They are entered and shown with the Values card
(*Edit ▸ Values ▸*, the *Values* button in Explore and Engineer) and stored in optional text parameters (engineering
section of the inspector). Water is the default medium: c = 4.19 kJ/(kg·K), ρ = 1000 kg/m³ (defaults that give way to
entered values). Card variables come first. Design notes:
[engineering-calculations.md, section 13](../design/engineering-calculations.md#13-implementation-notes-calc-4-fluid-process-building-services-energy-2026-11-25).

| Element | Variables (parameter) | Local relations [source] |
|---|---|---|
| Centrifugal pump | *V̇* (`flowRate`, m³/h), *H* (`head`), *P* (`power`, shaft), η (`efficiency`); panel Δ*p* (`pressureRise`), *P*_hyd (`powerHyd`), *n* (`speed`), *T* (`torque`); engineer ρ (`density`) | Δ*p* = ρ·*g*·*H*; *P*_hyd = Δ*p*·*V̇*; *P* = *P*_hyd/η [S28]; *P* = 2π·*n*·*T* [S18] |
| Displacement pump, fan | *V̇*, Δ*p*, *P*, η; panel *P*_hyd, *n*, *T* | *P*_hyd = Δ*p*·*V̇*; *P* = *P*_hyd/η [S28]; *P* = 2π·*n*·*T* [S18] |
| Compressor | *P* (`power`, drive), ṁ (`massFlow`, kg/s), π = *p*₂/*p*₁ (`pressureRatio`), θ₂ (`tempOut`); panel θ₁ (`tempIn`, default 20 °C), *p*₁ (`pressureIn`, absolute, default 1.01325 bar), *p*₂ (`pressureOut`), η_s (`efficiency`, isentropic), Δ*T* (`deltaT`, rise), *V̇*₁ (`flowIn`), θ_max (`tempMax`, permissible outlet temperature), *n*, *T*; engineer θ₂s (`tempOutIs`), *P*_s (`powerIs`), *V̇*₂ (`flowOut`), κ (`kappa`), *R* (`gasConstant`), *c*_p (`specificHeat`); choice gas (`gas`, air) | see "Gas machines" [S29]; *P* = 2π·*n*·*T* [S18] |
| Turbine / expander | *P* (`power`, output), ṁ, π = *p*₁/*p*₂, θ₁; panel θ₂, *p*₁, *p*₂, η_s, Δ*T* (drop), *V̇*₁, θ_max (permissible inlet temperature), *n*, *T*; engineer as the compressor; no defaults at the inlet | see "Gas machines" [S29]; *P* = 2π·*n*·*T* [S18] |
| Drive motor | *P* (`power`, output), *n*, *T*; panel η (`efficiency`), *P*_el (`powerEl`, electrical input) | *P* = 2π·*n*·*T* [S18]; *P* = η·*P*_el [S15] |
| Generator | *P*_el (`powerEl`, electrical output), *P* (`power`, shaft input), η (`efficiency`), *n*; panel *T* | *P* = 2π·*n*·*T* [S18]; *P*_el = η·*P* [S15] |
| Shell-and-tube, plate, general heat exchanger | Φ (`duty`), *U* (`u`), *A* (`area`), Δθ_lm (`lmtd`); panel ṁ₁ (`massFlow1`), θ₁,in (`temp1In`), θ₁,out (`temp1Out`), ṁ₂, θ₂,in, θ₂,out; engineer *c*₁, *c*₂ (`specificHeat1/2`) | Φ = ṁ₁·*c*₁·(θ₁,in − θ₁,out); Φ = ṁ₂·*c*₂·(θ₂,out − θ₂,in); counter-flow Δθ_lm = (Δθ_a − Δθ_b)/ln(Δθ_a/Δθ_b) with Δθ_a = θ₁,in − θ₂,out, Δθ_b = θ₁,out − θ₂,in; Φ = *U*·*A*·Δθ_lm [S29] |
| Air cooler, cooling tower | Φ, ṁ (`massFlow`), θ_in (`tempIn`), θ_out (`tempOut`); engineer *c* | Φ = ṁ·*c*·(θ_in − θ_out) [S29] |
| Vessels, tank, reactor, column | *V* (`volume`), *D* (`diameter`), *h* (`length`, cylindrical part), *Q* (`heat`); panel Δθ (`deltaT`); engineer ρ, *c* | *V* = π·*D*²·*h*/4 (heads not counted); *Q* = ρ·*V*·*c*·Δθ [S29] |
| Process line | *V̇* (`flowRate`), ṁ (`massFlow`), *v* (`velocity`), θ (`temperature`); panel *d*_i (`innerDiameter`); engineer ρ | ṁ = ρ·*V̇*; *v* = 4·*V̇*/(π·*d*_i²) [S27, S29] |

Side 1 of a heat exchanger is the tube side (T1 in, T2 out), the A side of the plate exchanger (A1, A2) and the primary
side of the general symbol (P1, P2); side 2 is the shell side (S1, S2), the B side (B1, B2) and the secondary side
(S1, S2). Φ > 0 means heat flows from side 1 to side 2; with side 1 colder all differences change sign and Φ < 0.
Other layouts than counter-flow need a correction factor (not modelled).

**Port flows.** Process lines join nozzles into nets. The classes volume flow *V̇*, mass flow ṁ and stream temperature θ
are shared when at most two elements carry them in a net; lines take them on an unbranched run. The line converts *V̇*
and ṁ. Manual, control and check valves, strainers, reducers, flow elements and sight glasses pass all three, filters and
dampers (A1–A2) as well; pumps and fans pass the temperature. A pump feeding a heat exchanger through a valve thus sets
ṁ₁, and the outlet temperature of side 1 becomes the inlet temperature of the next element. Heat losses and the
temperature rise in pumps are not modelled. Compressors and turbines give ṁ, θ and the absolute pressure *p* at their
nozzles (suction S / inlet I: θ₁, *p*₁; discharge D / outlet O: θ₂, *p*₂). The pressure class `p` is shared by every
nozzle on connected lines (no pressure drop modelled) and passes through nothing: a valve, strainer or flow element
between two machines stops it. The volume flow of a gas is not passed (it changes with *p* and *T*; valves pass *V̇*
unchanged, which is right for liquids only), so a process line next to a gas machine converts ṁ with its own density
(water by default: enter ρ on the line for a gas). Steam traps, rupture discs, cyclones and silos have no values.

**Gas machines (compressor, turbine).** An ideal gas with constant properties, adiabatic machine, absolute pressures and
temperatures [S29]:

| Relation | Compressor | Turbine / expander |
|---|---|---|
| Pressure ratio | *p*₂ = π·*p*₁ | *p*₁ = π·*p*₂ |
| Gas | *c*_p = κ·*R*/(κ − 1) | same |
| Isentropic outlet | *T*₂s = *T*₁·π^((κ−1)/κ) | *T*₂s = *T*₁·π^(−(κ−1)/κ) |
| Isentropic efficiency | η_s = (*T*₂s − *T*₁)/(*T*₂ − *T*₁) | η_s = (*T*₁ − *T*₂)/(*T*₁ − *T*₂s) |
| Temperature change | Δ*T* = *T*₂ − *T*₁ | Δ*T* = *T*₁ − *T*₂ |
| Power (drive / expansion work per second) | *P* = ṁ·*c*_p·Δ*T* | *P* = ṁ·*c*_p·Δ*T* |
| Isentropic power | *P*_s = ṁ·*c*_p·(*T*₂s − *T*₁) = η_s·*P* | *P*_s = ṁ·*c*_p·(*T*₁ − *T*₂s) = *P*/η_s |
| Volume flows | *V̇*₁ = ṁ·*R*·*T*₁/*p*₁, *V̇*₂ = ṁ·*R*·*T*₂/*p*₂ | same |

Every relation is solved for each of its variables, so a measured outlet temperature gives η_s, a known power gives ṁ.
The specific work (*w* = *c*_p·Δ*T*, in kJ/kg) is not a variable of its own: Core has no quantity kind for J/kg yet; it
follows from Δ*T* and *c*_p. Mechanical losses, cooling of the casing, real-gas behaviour and wet steam are not modelled.
Air at 20 °C and π = 4 with η_s = 80 %: *T*₂s = 435.6 K, θ₂ = 198.1 °C, *P* = 178.9 kW per kg/s. A turbine with 900 °C,
10 bar → 1 bar, η_s = 88 %: θ₂ = 402.3 °C, *P* = 2.50 MW for 5 kg/s.

The gas (`gas`, engineering section) gives κ and *R* as values without inputs; an entered κ or *R* takes precedence over
the table, "Other gas" leaves both to the user. Values for the ideal gas at about 300 K and low pressure (*R* = *R*_m/*M*
with *R*_m = 8.314 J/(mol·K)), as tabulated in the ideal-gas property tables of the thermodynamics textbooks (Y. A.
Çengel, M. A. Boles, *Thermodynamics: An Engineering Approach*, McGraw-Hill, table of ideal-gas specific heats of common
gases at 300 K; H. D. Baehr, S. Kabelac, *Thermodynamik*, Springer; property data also in the VDI Heat Atlas [S29]):

| Gas (`gas`) | κ | *R* in J/(kg·K) | *c*_p in kJ/(kg·K) (= κ·*R*/(κ − 1)) |
|---|---|---|---|
| Air (`air`, default) | 1.400 | 287.0 | 1.005 |
| Nitrogen N₂ (`nitrogen`) | 1.400 | 296.8 | 1.039 |
| Oxygen O₂ (`oxygen`) | 1.395 | 259.8 | 0.918 |
| Carbon dioxide CO₂ (`co2`) | 1.289 | 188.9 | 0.843 |
| Methane CH₄, ≈ natural gas (`methane`) | 1.299 | 518.2 | 2.251 |
| Hydrogen H₂ (`hydrogen`) | 1.405 | 4124.0 | 14.307 |
| Helium He (`helium`) | 1.667 | 2076.9 | 5.191 |
| Argon Ar (`argon`) | 1.667 | 208.1 | 0.520 |
| Steam, superheated, as ideal gas (`steam`) | 1.327 | 461.5 | 1.873 |
| Other gas (`custom`) | entered | entered | entered or from κ, *R* |

κ falls with temperature (air ≈ 1.34 at 1000 K), so hot turbine stages come out somewhat optimistic; steam turbines in
the wet region need steam tables (not modelled).

**Shaft links.** Speed, torque and power pass across drive shafts as described in "Drive shafts" (relations
`process.shaft.speed`, `process.shaft.torque`, `process.shaft.power`); solving one machine pulls its drive and shaft line
into the scope. A motor driving a compressor shows the compressor's drive power (and, with its efficiency, the electrical
input); a turbine driving the generator gives it its shaft power, *P*_el = η·*P*.

**Joins across modules.** The generator terminal `el` names *P*_el for the physical key *P*
([engineering-calculations.md, section 6.1](../design/engineering-calculations.md#61-joins-across-modules-core-2026-11-26)):
an energy cable from the terminal to a meter, busbar or load passes the electrical output on (unbranched run), and the
machines motor on a compressor shaft takes the compressor's power as its mechanical *P* (its *P*_el = *P*/η).

**Checks** (hints only, overlay, nothing changes):

| Id | Rule | Severity | Source |
|---|---|---|---|
| `heat.cross` | heat exchanger with all four temperatures: (θ₁,in − θ₂,out)·(θ₁,out − θ₂,in) ≤ 0, a temperature cross that counter-flow cannot reach | warning | [S29] |
| `process.efficiency` | pumps, fans, motor, generator, compressor and turbine (isentropic): η ≤ 0 or η > 1 (also when η follows from the temperatures, e.g. an outlet colder than the isentropic one) | warning | [S1] |
| `gas.ratio` | compressor *p*₂ < *p*₁, turbine *p*₂ > *p*₁ (π < 1) | warning | [S29] |
| `gas.temperature` | compressor θ₂ > θ_max, turbine θ₁ > θ_max (the permissible temperature is entered from the datasheet; no limit is assumed) | warning | [S29] |
| `gas.kappa` | κ ≤ 1 or κ > 5/3 (no gas) | warning | [S29] |

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
flow sheet, and the cycle formulas (Otto, Diesel, Carnot p-v and T-s, Rankine, refrigeration). `ShaftTests` dock pumps,
compressors and fans on a mechanical shaft, the machines motor symbol, an e-machine shaft line, the process motor and a
turbine (coupling and shared *n*, *T*, *P*). `GasMachineTests` check the gas formulas against hand calculations (air
compressor, gas turbine, efficiency from a measured outlet temperature, every gas of the choice, entered κ, "other gas"),
the checks, ṁ/θ/*p* from a compressor outlet to a turbine inlet and the pressure stopped by a valve, a machines motor
that sees a compressor's power without a speed, and a turbine driving the generator whose terminal feeds an energy
meter through an energy cable. The tests reference the mechanical, machines and energy modules as test dependencies only
(the module itself depends on Core alone). The gallery test writes
`gallery/*.html` into the test output folder (every type in each view, stretched, presets, a distillation sample and
an air handling unit sample).
