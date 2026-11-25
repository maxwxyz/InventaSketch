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
piloted check valves and pilot actuators a pilot line, leakage ports L a drain line, all others a working line; then the
components with a port that fits. The free end of a line offers "+" to continue it or to dock the next component with
its facing port.

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
| `fluid.line.kind=…`, `fluid.line.route=orthogonal/free` | `Option`/route pictograms | line type ▾; routing ▾ |
| `fluid.ports` | `Text` | show port names |
| `fluid.label` | `Text` | show/hide the designator (adds a suggestion such as `1V1` if empty) |
| `fluid.ref.set` | `Text` | designator typed directly (input action) |

## Object types

| Category | Types (`fluid.*`) |
|---|---|
| Pumps, motors & cylinders | `pump` (pump/compressor; `variable`, `flow`), `motor` (`variable`, `flow`), `cylinder.double` (`rod` single/through, `cushion` none/fixed/adjustable), `cylinder.single` (`spring` return/extend/none), `rotary` |
| Directional control valves | `valve.directional` (`ways` 2/2, 3/2, 4/2, 4/3, 5/2, 5/3; `normal`; `centre`; `actLeft`/`actRight`: none, manual, push button, lever, pedal, roller, spring, solenoid, pressure pilot; `centring`; `detent`), `actuation` (separate actuation symbol incl. detent) |
| Non-return, flow & pressure valves | `valve.check` (`spring`, `piloted`), `valve.throttle` (`adjustable`, `check` = one-way flow control), `valve.relief`, `valve.reducing`, `valve.sequence` (drain port `L`), `valve.shuttle` (OR), `valve.twopressure` (AND), `valve.quickexhaust` |
| Supply, conditioning & accumulators | `tank` (open/closed), `source`, `exhaust`, `silencer`, `filter`, `separator`, `lubricator`, `cooler`, `heater`, `frl` (detailed/simplified), `accumulator` (gas/spring/weight) |
| Measuring & signalling | `gauge`, `pressureswitch`, `flowmeter`, `thermometer` |
| Lines & connections | `line` (point object), `junction`, `crossing` (plain or with bridge), `plug`, `coupling` (coupled/uncoupled, with non-return valves), `enclosure` (dash-dot assembly boundary) |

Companions ("Insert ▸"): a line offers valves, cylinders, gauges and junctions; a cylinder offers directional valves,
throttles and check valves; a directional valve offers actuations, lines, cylinders and throttles.

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

## Host notes

All symbols are simple native primitives (lines, rectangles, ellipses, polylines, closed triangles, text); no vector
picture fallback is needed. Rounded rectangles (closed reservoir, accumulator) use the rectangle corner radius.

## Limits and next steps

- No numeric port identification (`1`, `2`, `3`, `12`, `14` …), no servo/proportional valves, no logic-element blocks.
- A line end docked on another line (its rail) is attached to it (a branch): it follows the line and the connection
  list joins both lines; a junction dot is optional.
- Labels and port names lie outside the object box, so host selection frames do not include them.
- Visual review: `dotnet test` writes an SVG gallery (ports as red circles, valve variants, sample circuit) to
  `tests/InventaSketch.Modules.Fluid.Tests/bin/Debug/net10.0/gallery/index.html`.
