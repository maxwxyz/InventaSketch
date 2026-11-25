# Electrical & electronics module (`electrical`)

Library: `src/InventaSketch.Modules.Electrical` · Tests: `tests/InventaSketch.Modules.Electrical.Tests` ·
German version: [../../de/modules/electrical.md](../../de/modules/electrical.md)

The module provides circuit symbols in the style of IEC 60617, wires that connect to symbol pins, house installation
floor-plan symbols, single-line diagram elements, power electronics blocks and illustrative PCB objects. All symbols
are **drawn independently**; no standard texts, tables or symbol database content are copied, and no claim of
standards compliance is made.

It is a **sketching** module for slides and technical figures. It is **not** an ECAD tool: it offers a connection list
(netlist CSV) and a basic rule check, but no simulation and no manufacturing output.

## Data exports

- **Netlist / connection list (CSV)** (`elec.netlist`): wire ends docked to pins join pins into nets; junction dots
  merge nets. One row per net with its pins (`R1.1 C1.2`). Keeps connectivity and designators, not the routing.
- **Electrical rule check (CSV report)** (`elec.erc`): one row per finding — `pin.unconnected` (no wire docked to or
  lying on the pin), `wire.open` (a wire end neither docked nor on a pin, junction or other wire), `part.shorted` (one
  wire joins two pins of the same component). A basic check for illustrative schematics: no pin types, drivers or power
  nets. Both exports apply when at least one wire end is docked to a pin.

## Conventions

- **Quick sketch first:** every library item is inserted without parameters. Free (uniform or non-uniform) scaling
  changes `Size` only; symbols are authored for a design box and stretched to the object box.
- **Orientation:** two-terminal symbols are horizontal with pins on the left and right edge midpoints (signal/current
  flow left to right). Rotation and mirroring are the generic object transforms, not symbol parameters.
- **Grid-friendly pins:** at the default size and a grid-aligned insertion point, schematic pins lie on a 2.5 mm grid
  (most on the 5 mm grid). Default boxes are multiples of 5 mm (for example resistor 20 × 8, gate 20 × 20,
  op-amp 30 × 20).
- **Line weights:** symbol outlines and wires 0.35 mm, emphasised strokes (plates, bars) 0.7 mm, busbars and bus lines
  1.0 mm, mechanical linkages thin dashed. All strokes are `LineRole.Visible`/`Thin`, so monochrome output works.
- **Labels:** optional `ref` (designator, e.g. `R1`) and `value` (e.g. `10k`) parameters. The designator is placed above
  and the value below the box (to the right for vertical symbols such as earth or antenna). Labels are kept upright on
  the page when the symbol is rotated and are deliberately placed **outside** the symbol box, like in ECAD schematics.
  `showLabel = false` hides them.
- **Text inside symbols** (`M`, `&`, `≥1`, `kWh`, pin numbers) is also kept upright.

## Views

| View id | Icon (`IconIds`) | Used by |
|---|---|---|
| `symbol` | `Symbol` | IEC-style circuit symbol (default for schematic items) |
| `ansi` | `Simplified` | ANSI zigzag resistor and potentiometer, ANSI distinctive-shape gates, triangle op-amp, curved-plate electrolytic capacitor |
| `circles` | `Simplified` | transformer as two overlapping circles (single-line) |
| `block` | `Block` | ECU/IC block, converters, gate driver |
| `plan` | `Plan` | house installation floor-plan symbols, cable route |
| `section`, `schematic` | `Front`, `Schematic` | socket, switch, luminaire, junction box and distribution board seen on the wall at their mounting height (`height`, mm above the finished floor) and as installation schematic; a selected installation switches plan ↔ section ↔ schematic together with the building services (`InstallationViews` in Core, 2026-11-25) |
| `top` | `Top` | PCB illustration objects |

## Pins and snapping

- Every connectable symbol exposes its pins as `Anchor.Point(AnchorRole.Port, …)` with a stable id (`1`/`2`, `A`/`K`,
  `B`/`C`/`E`, `G`/`D`/`S`, `+`/`-`, `in1…`/`out`, `U`/`V`/`W`, …). Pins are stored in normalised box coordinates
  (−0.5…0.5), so they stay on the symbol for any scaling, rotation and mirroring.
- Wire ends are point anchors (`AnchorRole.End`); the snapper pulls a dragged wire end onto a pin (`Snapper.SnapPoint`,
  snap kind `port`) and a moving symbol's ports onto wire ends or other pins. Pins always beat the generic corner and
  centre snaps, even on a 2 × 2 mm junction dot whose corner is closer to the wire end than its pin.
- Wires and buses are **rails** (`AnchorRole.Rail` along the routed path, ids `rail0`, `rail1`, …): another wire end,
  a junction dot or a pin docks anywhere on them (snap kind `rail`, T junctions). Rail end points and wire corners attract
  like pins; in between a rail ranks below discrete pins and taps.
- `ElectricalModule.ConnectToPin(registry, doc, wireId, index, tolerance)` moves a wire end onto the nearest pin within the
  tolerance and stores an `Attachment` with the **pin id** (`AnchorId`), so the wire follows when the symbol is moved,
  rotated or scaled, and `Attachments.Refresh` (run by the engine after every edit) moves it with the pin when the pin
  moves inside the symbol (polarity flip, pin count or view change).
- Orthogonal wires leave and enter attached pins perpendicular to the box edge the pin sits on (honouring the
  symbol's rotation).
- Bus lines expose `Port` taps every 10 mm along their length and are rails in between; the busbar exposes five taps.
- Objects without connection points: board outline, copper area, mounting hole, silkscreen label.

**Build "+" (connections).** Every free pin offers a wire (orthogonal) and the symbols that connect there; house
installation elements offer the installation cable route (`electric.installation`, also used by the outlets of the
building services), PCB pads and footprints a track (`pcb`). Wires carry `electric` (they fit DC and AC ports of other
modules), buses `data`; the free end of a wire, bus, cable route or track offers "+" to continue it or to dock the next
symbol with its facing pin.

## Context actions

| Action id | Icon | Effect |
|---|---|---|
| `view.<id>` | view icon | switch representation (IEC/ANSI, coils/circles, …) |
| `elec.toggle` | `Toggle` | switch state open/closed (`closed`; NC contacts default to closed) — switch, push button, contactor, breaker, RCD |
| `elec.polarity` | `Swap` | flip polarity (`reverse`: mirrors symbol and pins) — diodes, thyristor, polarised capacitor, source, battery |
| `elec.polarity`/`elec.channel` | `Swap` | NPN ↔ PNP, N ↔ P channel |
| `elec.<choice>=<value>` | `Option` | choices (dropdowns): gate function, contact type, earth symbol, source type, supply, plug/socket, package, bus protocol, … |
| `elec.route=free/orthogonal/45` | route pictograms | wire/track routing as a choice |
| `elec.label` | `Text` | show designator/value (adds `ref` = prefix + `1` if empty) or hide labels |
| `elec.envelope` | `Toggle` | envelope circle of transistors |

Value text is edited in the inspector (`value`, `ref`); the context action cannot prompt for text yet.

## Object types

| Category | Types (`elec.*`) |
|---|---|
| Passive components | `resistor` (IEC rectangle / ANSI zigzag), `potentiometer`, `capacitor`, `capacitor.polarized`, `inductor` (`core`: none/iron/ferrite), `transformer` (coils / circles, `core`) |
| Semiconductors | `diode`, `diode.led`, `diode.zener`, `diode.photo`, `transistor.bjt` (`polarity` npn/pnp), `transistor.mosfet` (`channel` n/p, enhancement), `transistor.igbt`, `thyristor`; optional `envelope` circle |
| Switching & protection | `switch` (`contact` no/nc/changeover; also used for relay contacts), `pushbutton`, `relay.coil`, `contactor` (1–3 poles), `fuse`, `breaker`, `rcd` |
| Sources & loads | `ground` (earth/PE/chassis), `source` (DC/AC/current), `battery` (1–4 cells), `lamp`, `motor` and `generator` (`supply` none/DC/AC/3~; 3~ exposes U/V/W), `sensor`, `actuator` (optional `label`), `speaker`, `microphone`, `antenna` |
| Logic & ICs | `gate` (`gate` and/or/not/nand/nor/xor/xnor/buffer, `inputs` 2–4; IEC rectangular and ANSI distinctive shapes), `flipflop` (D/JK/SR/T), `opamp` (IEC / triangle), `block.ecu` (ECU/IC/MCU block with `pinsLeft`/`pinsRight` and `label`) |
| Wires, connectors & buses | `wire` (point object, `route` free/orthogonal/45), `junction`, `netlabel` (text = net name), `connector` (plug/socket/pair, 1–12 poles), `terminal`, `testpoint`, `bus` (point object; CAN/LIN/Ethernet/FlexRay/generic label) |
| House installation | `house.socket` (1–4 outlets, protective contact), `house.switch` (one-way/two-way/push), `house.light`, `house.junction`, `house.board`, `house.cable` (point object with conductor marks), `sld.busbar`, `sld.feeder` (breaker/fuse/none), `sld.meter` |
| Power electronics | `pe.inverter`, `pe.rectifier` (1- or 3-phase AC side), `pe.dcdc`, `pe.dclink`, `pe.gatedriver`, `pe.module` (half bridge with freewheeling diodes) |
| PCB (illustrative) | `pcb.board`, `pcb.pad` (rect/round/oblong, THT), `pcb.via`, `pcb.track` (point object, `width`, routing free/orthogonal/45), `pcb.copper`, `pcb.footprint` (SOIC-8, QFP-32, SOT-23, 0603, DIP-8; pads are ports), `pcb.hole`, `pcb.silk` |

House installation symbols follow common DIN/EN floor-plan practice in spirit; their exact appearance varies between
countries and is not normative. PCB objects are proportionally simplified and imply no land-pattern or design-rule data.

## Element values (Calc 1)

The resistor (`elec.resistor`) has the values *R*, *U*, *I* and *P* (engineering calculations, see
[../design/engineering-calculations.md](../design/engineering-calculations.md), section 10). *R* is read from the existing
`value` text ("4k7", "10 kΩ", "100R"); *U*, *I* and *P* are optional text parameters `voltage`, `current` and `power`
that keep what was typed ("230 V"). Relations: *U* = *R*·*I* and *P* = *U*·*I* = *I*²·*R* = *U*²/*R* [S7]. Any two values
give the other two; three that agree are marked as checked, three that disagree give a conflict and nothing is changed.
A `value` text that is no resistance (e.g. "1/4 W") is ignored for *R*. The engine methods `values`, `setValue`,
`clearValue`, `explain` and `dragValue` work on it; the user interface follows with Calc 2. Other symbols get values with
Calc 3.

## Persistence

All state is in the canonical document (`type`, `view`, `params`, `points`, `attach`, `text`); there is no hidden
module state. A JSON round trip reproduces the document and the rendered SVG exactly (tested).

## Limits and next steps

- No auto-junctions or crossing jumps yet; the rule check knows no pin types (output against output, unpowered nets).
- A wire end docked on another wire or a bus (its rail) is attached to that rail (a branch): it follows when the
  other wire moves or changes, and the netlist joins both wires into one net.
- Labels lie outside the object box, so host selection frames do not include them.
- Visual review: `dotnet test` writes an SVG gallery to
  `tests/InventaSketch.Modules.Electrical.Tests/bin/Debug/net10.0/gallery/index.html` (ports shown as red circles,
  plus an example circuit with attached wires).
