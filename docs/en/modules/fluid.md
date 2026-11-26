# Fluid power module (`fluid`)

Library: `src/InventaSketch.Modules.Fluid` · Tests: `tests/InventaSketch.Modules.Fluid.Tests` ·
German version: [../../de/modules/fluid.md](../../de/modules/fluid.md)

The module provides hydraulic and pneumatic circuit diagram symbols in the spirit of the conventions of
ISO 1219-1 (graphical symbols for fluid power systems) and the component designation pattern known from ISO 1219-2
circuit diagrams (for example `1V1`, `0P1`). All symbols are **drawn independently**; no standard texts, tables or
symbol artwork are copied, and **no claim of standards compliance** is made.

It is a **sketching** module for slides, technical figures and concept circuits. It does not simulate, size or check
circuits.

## Conventions

- **Quick sketch first:** every library item is inserted without parameters. Free (uniform or non-uniform) scaling
  changes `Size` only; symbols are authored for a design box and stretched to the object box. Small details (triangles,
  arrow heads, dots, text) keep their proportions. When a parameter changes the design box (positions, actuators) —
  through an action or the inspector (`ObjectType.OnParamChanged`) — `Size` follows, so the drawing scale stays.
- **Hydraulic vs pneumatic** is the optional `medium` parameter (`hydraulic` / `pneumatic`) on the types where the
  drawing differs: pumps/compressors, motors, the rotary actuator, pressure sources, directional valves and actuation
  symbols (filled triangles = hydraulic, hollow = pneumatic). The context choice `fluid.medium` (hydraulic / pneumatic) sets it.
- **Line weights:** symbols and working lines 0.35 mm, pistons and plugs 0.7 mm, springs and details 0.25 mm, pilot
  lines long dashes, drain lines short dashes, enclosures dash-dot. Only black strokes and black or no fills are used,
  so monochrome output is unchanged.
- **Designator:** optional `ref` (e.g. `1V1`) with `showLabel`. It is placed outside the box on the first side without
  ports, tried in the page directions left, above, right, below, and is kept upright when the symbol is rotated.
- **Port names:** `showPorts` shows the port names next to the ports (outside the box). The return port of 3/2, 4/2 and
  4/3 valves is shown as `T` (hydraulic) or `R` (pneumatic); its id stays `T`.

## Views

| View id | Icon (`IconIds`) | Used by |
|---|---|---|
| `symbol` | `Symbol` | circuit symbol (all types) |
| `detailed` | `Detailed` | service unit (FRL): filter, regulator with gauge, lubricator in an enclosure |
| `simplified` | `Simplified` | service unit (FRL) as one simplified symbol |

Switching the FRL view keeps the visual scale and takes the proportions of the target view (`ViewDef.Size`).

## Ports and lines

- Every connectable symbol exposes its ports as `Anchor.Point(AnchorRole.Port, …)` with the port name as a **stable id**
  (`P`, `T`, `A`, `B`, `R`, `S`, `X`, `Y`, `L`; inline/connection parts `1`/`2`, crossing `W`/`E`/`N`/`S`).
- At the default size and a grid-aligned insertion point all ports lie on the **2.5 mm grid** and on the box edge
  (junction dot: centre). Ports are stored in design coordinates and scaled with the box.
- `fluid.line` is a point object (`kind`: working, pilot, drain, hose; `route`: orthogonal or free). Orthogonal lines
  leave docked ports perpendicular to the edge the port sits on (honouring rotation). The routed path is a **rail**, so
  other line ends and junction dots dock anywhere on it.
- `FluidModule.ConnectToPort(registry, doc, lineId, index, tolerance)` moves a line end onto the nearest port and stores
  the attachment with the **port id**. The line follows when the symbol moves, rotates or scales, and
  `Attachments.Refresh` moves it with the port when the port moves inside the symbol (valve type, spring side,
  actuation, view).
- Directional valves draw their ports at the box of the **normal position**: the centre box of 3-position valves,
  otherwise the box next to the single return spring (right box if no or both sides have a spring).
- The valve exposes `Mount` anchors (`left`, `right`) at its ends; the separate actuation symbol (`fluid.actuation`)
  docks there with its `mount` anchor.

**Build "+" (connections).** Every free port offers a line (medium `fluid`): pilot ports X/Y/Z of directional valves,
piloted check valves, the pilot-operated check valve, the counterbalance valve and pilot actuators a pilot line, leakage
ports L a drain line, all others a working line; then the components with a port that fits. The free end of a line offers
"+" to continue it or to dock the next component with its facing port.

## Drive shafts (a motor drives the pump)

Pumps/compressors and motors have a real mechanical shaft, with the same contract as the shafts and drives of the
mechanical and machines modules ([mechanical-drives.md](mechanical-drives.md)):

- `fluid.pump` has a `Hub` anchor (id `hub`) at the end of its drawn shaft (right edge, on the shaft line). It is
  driven: the shaft axis of a drive (an e-machine of the machines module, the stub of the machines motor symbol, the
  output stub of a hydraulic motor) or a mechanical shaft (`mech.shaft`) that passes through this point carries the pump.
- `fluid.motor` is a drive (`ObjectType.DrivesShaft`): its shaft stub is an `Axis` segment (id `shaft`) with the hub and
  an `End` mark at the outer end. A pump's hub or a mechanical part docks on the stub; a mechanical shaft continued from
  the shaft end carries the motor, and moving the motor takes the shaft along.
- The hub has no bore radius (0), so a pump or motor seated on a shaft keeps its size. Docking uses the normal snapping
  (hub onto an axis) and the core coupling ("moves along": the shaft or drive carries the pump). The pump's shaft points to
  the right: flip it (or the drive) to place it on the other side.
- **Values:** pump and motor share the speed *n* with the element they are coupled to. The torque *T* is passed over a
  direct one-to-one coupling (e-machine on the pump hub, neither coupled to a third element with a torque) and from the
  mechanical shaft or coupling they sit on when that shaft line has at most two members that take or give torque (the
  rule of the mechanical module). Example: an e-machine at 1450 min⁻¹ drives a 10 cm³ pump at 100 bar: *Q* = 14.5 l/min,
  *T* = *V*_g·*p*/(2π) = 15.9 N·m is the e-machine's load torque, *P* = 2.42 kW.

## Context actions

| Action id | Icon | Effect |
|---|---|---|
| `view.<id>` | view icon | FRL detailed / simplified |
| `fluid.medium=hydraulic/pneumatic` | `Option` | medium ▾ (choice) |
| `fluid.valve.ways=…` | `Option` | valve type ▾ 2/2, 3/2, 4/2, 4/3, 5/2, 5/3 (the size follows the design box) |
| `fluid.valve.centre=…` | `Option` | centre position ▾, three positions only (4/3: closed, tandem, float, open, pressurised; 5/3: closed, exhausted, pressurised); `fluid.valve.normal=closed/open`: rest position ▾ (2/2, 3/2) |
| `fluid.valve.normal` | `Toggle` | normally closed / open (2/2, 3/2) |
| `fluid.valve.actLeft=…`, `fluid.valve.actRight=…` | `Option` | actuation left ▾ / right ▾ |
| `fluid.valve.flip` | `Swap` | flip the spring side (swap the actuations; the normal position follows) |
| `fluid.valve.detent`, `fluid.valve.centring` | `Toggle` | detent; spring centring of 3-position valves |
| `fluid.variable`, `fluid.flow=one/two` | `Toggle`/`Option` | pumps/motors: variable displacement, flow directions ▾ |
| `fluid.cyl.rod=…`, `fluid.cyl.cushion=…`, `fluid.cyl.spring=…` | `Option` | piston rod ▾, cushioning ▾, spring ▾ |
| `fluid.spring`, `fluid.piloted`, `fluid.adjustable`, `fluid.check` | `Toggle` | check, throttle and pressure valve options |
| `fluid.pilotcheck.twin` | `Toggle` | pilot-operated check valve: single / double (cross-piloted); the size follows the design box |
| `fluid.suctioncup.bellows` | `Toggle` | suction cup with bellows |
| `fluid.line.kind=…`, `fluid.line.route=orthogonal/free` | `Option`/route pictograms | line type ▾; routing ▾ |
| `fluid.ports` | `Text` | show port names |
| `fluid.label` | `Text` | show/hide the designator (adds a suggestion such as `1V1` if empty) |
| `fluid.ref.set` | `Text` | designator typed directly (input action) |

## Object types

| Category | Types (`fluid.*`) |
|---|---|
| Pumps, motors & cylinders | `pump` (pump/compressor; `variable`, `flow`; shaft hub), `motor` (`variable`, `flow`; output shaft axis), `cylinder.double` (`rod` single/through, `cushion` none/fixed/adjustable), `cylinder.single` (`spring` return/extend/none), `rotary`, `suctioncup` (suction cup / Sauggreifer; `bellows`; port `V`) |
| Directional control valves | `valve.directional` (`ways` 2/2, 3/2, 4/2, 4/3, 5/2, 5/3; `normal`; `centre`; `actLeft`/`actRight`: none, manual, push button, lever, pedal, roller, spring, solenoid, pressure pilot; `centring`; `detent`), `actuation` (separate actuation symbol incl. detent) |
| Non-return, flow & pressure valves | `valve.check` (`spring`, `piloted`), `valve.throttle` (`adjustable`, `check` = one-way flow control), `valve.relief`, `valve.reducing`, `valve.sequence` (drain port `L`), `valve.shuttle` (OR), `valve.twopressure` (AND), `valve.quickexhaust`, `valve.pilotcheck` (pilot-operated check valve / entsperrbares Rückschlagventil with pilot piston; `spring` (on), `twin` = double, cross-piloted: ports `P`, `A`, `X`; double `A1`, `A2`, `B1`, `B2`), `valve.counterbalance` (counterbalance valve / Senkbremsventil: normally closed pressure valve from the load port `C` to the valve port `V`, internal pilot from `C`, external pilot `X`, bypass check valve for free flow `V` → `C`; `adjustable`) |
| Supply, conditioning & accumulators | `tank` (open/closed), `source`, `exhaust`, `silencer`, `filter`, `separator`, `lubricator`, `cooler`, `heater`, `frl` (detailed/simplified), `accumulator` (gas/spring/weight), `vacuum.generator` (vacuum generator / ejector – Vakuumerzeuger: driving nozzle and diffuser, ports `P` supply, `R` exhaust, `V` vacuum) |
| Measuring & signalling | `gauge`, `pressureswitch`, `flowmeter`, `thermometer` |
| Lines & connections | `line` (point object), `junction`, `crossing` (plain or with bridge), `plug`, `coupling` (coupled/uncoupled, with non-return valves), `enclosure` (dash-dot assembly boundary) |

Companions ("Insert ▸"): a line offers valves, cylinders, gauges and junctions; a cylinder offers directional valves,
throttles and check valves; a directional valve offers actuations, lines, cylinders and throttles; a vacuum generator
offers the suction cup and vice versa.

Library items of the new elements: `fluid.pilotcheck`, `fluid.pilotcheck.double`, `fluid.counterbalance`,
`fluid.vacuum`, `fluid.suctioncup`, `fluid.suctioncup.bellows` (the older `fluid.check.piloted`, a check valve with the
`piloted` option, stays).

## Connection list export

`fluid.connections` (`IExportProvider`, CSV): line ends docked to ports (attachments with a port id) join ports into
connections. Junction dots join everything docked to them, a crossing keeps `W`–`E` and `N`–`S` apart, a coupled quick
coupling joins its two sides. One row per connection: number, line types used, ports (`designator.port`, otherwise
`type name.port`, separated by `; `).

**Preserves:** which ports are connected, with which kind of line, component designators and displayed port names.
**Not preserved:** line routing, switching paths inside valves (state dependent), line ends that only touch another line
without a junction dot, positions.

## Persistence

All state is in the canonical document (`type`, `view`, `params`, `points`, `attach`, `text`); there is no hidden
module state. A JSON round trip reproduces the document and the rendered SVG exactly (tested).

## Engineering values (Calc 4)

Values are optional: every symbol still inserts and scales without them. They are entered and shown with the Values card
(*Edit ▸ Values ▸*, the *Values* button in Explore and Engineer) and stored in optional text parameters (engineering
section of the inspector). Speeds are magnitudes, *P* = 2π·*n*·*T* (German *M*). Card variables come first. Design notes:
[engineering-calculations.md, section 13](../design/engineering-calculations.md#13-implementation-notes-calc-4-fluid-process-building-services-energy-2026-11-25).

| Element | Variables (parameter) | Local relations [source] |
|---|---|---|
| Pump / compressor (`fluid.pump`) | *V*_g (`displacement`, cm³), *n* (`speed`), *Q* (`flowRate`, l/min), *p* (`pressure`, bar); panel *T* (`torque`, drive), *P* (`power`, drive), *P*_hyd (`powerHyd`); engineer η_v (`efficiencyV`), η_hm (`efficiencyHm`), both default 100 % | *Q* = *V*_g·*n*·η_v; *T* = *V*_g·*p*/(2π·η_hm); *P* = 2π·*n*·*T*; *P*_hyd = *p*·*Q*; *P* = *P*_hyd/(η_v·η_hm) [S26] |
| Motor (`fluid.motor`) | as the pump; *T* and *P* are output values | *n* = *Q*·η_v/*V*_g; *T* = *V*_g·*p*·η_hm/(2π); *P* = 2π·*n*·*T*; *P*_hyd = *p*·*Q*; *P* = *P*_hyd·η_v·η_hm [S26] |
| Double-acting cylinder | *D* (`bore`), *d* (`rodDiameter`), *F*_ext (`forceExt`), *v*_ext (`speedExt`); panel *p*, *Q*, *F*_ret (`forceRet`), *v*_ret (`speedRet`), *A*₁ (`areaExt`), *A*₂ (`areaRet`), φ (`areaRatio`), *s* (`stroke`), *t*_ext (`timeExt`), *t*_ret (`timeRet`); engineer η_hm, *F*_req (`forceReq`), *p*_max (`pmax`, linked) | *A*₁ = π·*D*²/4 (through rod: *A*₁ = *A*₂); *A*₂ = π·(*D*² − *d*²)/4; φ = *A*₁/*A*₂; *F* = *p*·*A*·η_hm and *v* = *Q*/*A* both ways; *t* = *s*/*v* [S26] |
| Single-acting cylinder | *D*, *F* (`force`, spring force not included), *v* (`speed`), *p*; panel *Q*, *d*, *A* (`area`), *s*, *t* (`time`); engineer η_hm, *F*_req, *p*_max | *A* = π·*D*²/4 (spring extend: annulus); *F* = *p*·*A*·η_hm; *v* = *Q*/*A*; *t* = *s*/*v* [S26] |
| Rotary actuator | *V*_g, *p*, *T*; engineer η_hm | *T* = *V*_g·*p*·η_hm/(2π) [S26] |
| Relief, reducing, sequence valve | *p*_set (`setPressure`) | – |
| Gauge, pressure switch, pressure source | *p* (shown from the net) | – |
| Flow meter | *Q* (shown from the net) | – |
| Accumulator | *V*₀ (`volume`), *p*₀ (`p0`), Δ*V* (`volumeUsable`); panel *p*₁, *p*₂, *V*₁, *V*₂ (absolute pressures) | *p*₀·*V*₀ = *p*₁·*V*₁ = *p*₂·*V*₂ (isothermal); Δ*V* = *V*₁ − *V*₂ [S26] |
| Line (`fluid.line`) | *Q* (`flowRate`), *d*_i (`innerDiameter`), *v* (`velocity`); panel *p* | *v* = 4·*Q*/(π·*d*_i²) [S27] |

**Port flows.** Lines join ports into nets (line ends docked on ports and on other lines). Per net:

- *Q* (volume flow) is shared when at most two elements carry it (a pump and a cylinder, say); lines take it only on an
  unbranched run. A tee to a relief valve or a gauge does not count as a branch for the elements, because they carry no
  flow. Two cylinders on one net share nothing (the split is unknown).
- *p* (pressure) is shared by the whole net; *p*_max is the relief valve's set pressure, passed to the cylinders.
- Directional valves pass *Q*, *p* and *p*_max between P, A and B (supply view; T/R/S stay apart); non-return valves,
  flow meters, filters, coolers, heaters, FRL units, crossings and coupled quick couplings pass all three; throttles pass
  *Q* and *p*_max but not *p*; reducing valves pass *Q* and set *p* at A to *p*_set. The pilot-operated check valve
  passes all three (P–A; double: A1–A2 and B1–B2), the counterbalance valve only *Q* (C–V: the load pressure is held).
- Pump and motor share *n* (and *T* as described under "Drive shafts") with the drive or shaft they are coupled to.
- Line and valve losses are not modelled.

Example (accept criterion): a pump with *V*_g = 10 cm³ at 1450 min⁻¹ gives *Q* = 14.5 l/min; through a 4/3 valve to a
50/28 mm cylinder at 100 bar: *F*_ext = 19.6 kN, *F*_ret = 13.5 kN, *v*_ext = 0.123 m/s, *v*_ret = 0.179 m/s, whether
the 100 bar is entered at the pump or at the cylinder.

**Checks** (hints only, overlay, nothing changes):

| Id | Rule | Severity | Source |
|---|---|---|---|
| `fluid.cyl.force` | *F*_req > *p*_max·*A*·η_hm (*p*_max from the relief valve, else the supply pressure *p*) | warning | [S26] |
| `fluid.velocity` | line velocity outside the textbook range of its function: suction 0.5–1.5 m/s (docked on a pump's S), return 2–4 m/s (drain line, tank, T/R port), pressure 3–6 m/s | hint | [S26] |
| `fluid.pressure.relief` | a pump with values whose pressure net (through valves) contains no relief valve | hint | [S25] (ISO 4413) |

## Host notes

All symbols are simple native primitives (lines, rectangles, ellipses, polylines, closed triangles, text); no vector
picture fallback is needed. Rounded rectangles (closed reservoir, accumulator) use the rectangle corner radius.

## Limits and next steps

- No numeric port identification (`1`, `2`, `3`, `12`, `14` …), no servo/proportional valves, no logic-element blocks.
- A line end docked on another line (its rail) is attached to it (a branch): it follows the line and the connection
  list joins both lines; a junction dot is optional.
- Labels and port names lie outside the object box, so host selection frames do not include them.
- Shaft tests (`ShaftTests`) use the mechanical and machines modules as test references only; the module itself
  depends on Core alone (the shaft links read the core coupling and the value keys `n`/`T`).
- Visual review: `dotnet test` writes an SVG gallery (ports as red circles, valve variants, sample circuit) to
  `tests/InventaSketch.Modules.Fluid.Tests/bin/Debug/net10.0/gallery/index.html`.
