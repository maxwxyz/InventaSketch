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
| `elec.polarity`/`elec.channel` | `Swap` | NPN ↔ PNP, N ↔ P channel (MOSFET) |
| `elec.<choice>=<value>` | `Option` | choices (dropdowns): gate function, contact type, earth symbol, source type, supply, plug/socket, package, bus protocol, thermistor NTC/PTC (`elec.thermistor.kind`), TVS direction (`elec.tvs.direction`), JFET channel (`elec.jfet.channel`), … |
| `elec.meter.quantity=<v/a/w/ohm>` | option pictograms (`Preview`: the instrument circle with its letter) | measured quantity of the measuring instrument |
| `elec.wire.hops` | `Hop` | hops of a wire at crossings on/off |
| `elec.route=free/orthogonal/45` | route pictograms | wire/track routing as a choice |
| `elec.label` | `Text` | show designator/value (adds `ref` = prefix + `1` if empty) or hide labels |
| `elec.envelope` | `Toggle` | envelope circle of transistors |

Value text is edited in the inspector (`value`, `ref`); the context action cannot prompt for text yet.

## Object types

| Category | Types (`elec.*`) |
|---|---|
| Passive components | `resistor` (IEC rectangle / ANSI zigzag), `resistor.thermistor` (`coefficient` ntc/ptc: NTC/PTC thermistor, oblique stroke with −ϑ/+ϑ), `resistor.ldr` (photoresistor, light arrows), `resistor.varistor` (VDR, oblique stroke with U), `potentiometer`, `capacitor`, `capacitor.polarized`, `inductor` (`core`: none/iron/ferrite), `transformer` (coils / circles, `core`), `crystal` (quartz crystal); the resistor kinds have the IEC and ANSI views of the resistor |
| Semiconductors | `diode`, `diode.led`, `diode.zener`, `diode.schottky`, `diode.tvs` (suppressor, `direction` uni/bi), `diode.bridge` (bridge rectifier: `AC1`/`AC2` left/right, `+` top, `-` bottom), `diode.photo`, `transistor.bjt` (`polarity` npn/pnp), `transistor.mosfet` (`channel` n/p, enhancement), `transistor.jfet` (`channel` n/p), `transistor.igbt`, `thyristor`, `triac` (`A1`/`A2`/`G`), `diac`, `optocoupler` (LED `A`/`K` left, phototransistor `C`/`E` right); optional `envelope` circle |
| Switching & protection | `switch` (`contact` no/nc/changeover; also used for relay contacts), `pushbutton`, `relay.coil`, `contactor` (1–3 poles), `fuse`, `breaker`, `rcd` |
| Sources & loads | `ground` (earth/PE/chassis), `source` (DC/AC/current), `battery` (1–4 cells), `lamp`, `motor` and `generator` (`supply` none/DC/AC/3~; 3~ exposes U/V/W), `sensor`, `actuator` (generic actuator block, optional `label`), `solenoid` (two-terminal actuating coil with plunger), `meter` (measuring instrument, `quantity` V/A/W/Ω), `speaker`, `microphone`, `antenna` |
| Logic & ICs | `gate` (`gate` and/or/not/nand/nor/xor/xnor/buffer, `inputs` 2–4; IEC rectangular and ANSI distinctive shapes), `flipflop` (D/JK/SR/T), `opamp` (IEC / triangle), `regulator` (three-terminal voltage regulator `IN`/`OUT`/`GND`), `block.ecu` (ECU/IC/MCU block with `pinsLeft`/`pinsRight` and `label`) |
| Wires, connectors & buses | `wire` (point object, `route` free/orthogonal/45), `junction`, `netlabel` (text = net name), `connector` (plug/socket/pair, 1–12 poles), `terminal`, `testpoint`, `bus` (point object; CAN/LIN/Ethernet/FlexRay/generic label) |
| House installation | `house.socket` (1–4 outlets, protective contact), `house.switch` (one-way/two-way/push), `house.light`, `house.junction`, `house.board`, `house.cable` (point object with conductor marks), `sld.busbar`, `sld.feeder` (breaker/fuse/none), `sld.meter` |
| Power electronics | `pe.inverter`, `pe.rectifier` (1- or 3-phase AC side), `pe.dcdc`, `pe.dclink`, `pe.gatedriver`, `pe.module` (half bridge with freewheeling diodes) |
| PCB (illustrative) | `pcb.board`, `pcb.pad` (rect/round/oblong, THT), `pcb.via`, `pcb.track` (point object, `width`, routing free/orthogonal/45), `pcb.copper`, `pcb.footprint` (SOIC-8, QFP-32, SOT-23, 0603, DIP-8; pads are ports), `pcb.hole`, `pcb.silk` |

House installation symbols follow common DIN/EN floor-plan practice in spirit; their exact appearance varies between
countries and is not normative. PCB objects are proportionally simplified and imply no land-pattern or design-rule data.

## Element values (Calc 1 and Calc 3)

Engineering values are optional (Quick Sketch first): every element still inserts without parameters, and nothing is
derived until values are entered. Design and sources: [../design/engineering-calculations.md](../design/engineering-calculations.md)
(sections 2.1–2.3, 10.7 and 10.10). Entered values live in optional text parameters that keep what was typed ("230 V",
"4k7"); derived values are never stored. Any values that determine an element give the others; values that agree are
marked as checked, values that disagree give a conflict and nothing is changed.

| Element | Variables (parameter) | Relations [S7] |
|---|---|---|
| Resistor `elec.resistor`, thermistor `elec.resistor.thermistor`, photoresistor `elec.resistor.ldr` | *R* (`value`, "4k7"; for thermistor and photoresistor the resistance at the operating point), *U* (`voltage`), *I* (`current`), *P* (`power`); rating `prated` for the check | *U* = *R*·*I*; *P* = *U*·*I* = *I*²·*R* = *U*²/*R* |
| Lamp `elec.lamp` | *U*, *I*, *P*, *R* (`resistance`, hot); marking in `value` ("12 V 5 W") | as the resistor |
| Capacitor `elec.capacitor`, `elec.capacitor.polarized` | *C* (`value`, "100n"), *U*, *Q* (`charge`), *E* (`energy`) | *Q* = *C*·*U*; *E* = ½*C*·*U*² = ½*Q*·*U* = *Q*²/(2*C*) |
| Inductor `elec.inductor` | *L* (`value`, "10 mH"), *I*, *E* (`energy`) | *E* = ½*L*·*I*² |
| Diode, LED, Schottky diode `elec.diode`, `elec.diode.led`, `elec.diode.schottky` | *U*_F (`uf`), *I*_F (`if`), *P* (`power`); marking "2.1 V 20 mA"; LED: `ifmax` for the check | *P* = *U*·*I* |
| Zener diode `elec.diode.zener` | *U*_Z (`uz`), *I* (`current`), *P* | *P* = *U*·*I* |
| Source `elec.source`, battery `elec.battery` | *U*, *I*, *P*; marking "5 V", "10 mA" (current source), "12 V 2 A" | *P* = *U*·*I* |
| Voltage regulator `elec.regulator` | *U*_out (`uout`), *U*_in (`uin`), *I*_out (`current`), *P* (`power`, loss); a voltage in `value` ("LM7805 5 V") is *U*_out | *P* = (*U*_in − *U*_out)·*I*_out (linear regulator, quiescent current neglected) |
| Transformer `elec.transformer` | *U*₁ (`u1`), *U*₂ (`u2`), *I*₁ (`i1`), *I*₂ (`i2`), *n* (`ratio`, panel); marking "230/12 V" | ideal: *n* = *U*₁/*U*₂ = *I*₂/*I*₁ [S7, S15] |
| Fuse, MCB, feeder `elec.fuse`, `elec.breaker`, `elec.sld.feeder` | *I*_B (`current`), *I*_n (`rated`); marking "B16", "C 10 A", "gG 35 A"; `iz` (*I*_z of the cable, user) for the check | – |
| RCD `elec.rcd` | *I*_n (`rated`), *I*_Δn (`idn`); marking "40 A 30 mA", "40/0,03 A", "30 mA" | – |
| Socket outlet, luminaire `elec.house.socket`, `elec.house.light` | *U*, *I*, *P* | *P* = *U*·*I* |

**Markings.** Where the value text is a marking rather than the element's own quantity, the quantities in it are read
(each with its unit) and shown as derived with the marking as formula ("*I*_n = B16 = 16 A"). An entered value that
disagrees with the marking gives a conflict. A value text that is no marking ("1N4148", "red") gives nothing and no
error.

**Circuits.** Values pass through wires (and cable routes and buses) docked to pins, as in the netlist export; junction
dots, closed contacts, a mated plug and socket, busbars, junction boxes, distribution boards and energy meters join
nets; earth symbols of the same kind and net labels of the same name join nets on a page (a net label used once leads
elsewhere). Open contacts and test points carry no current. Protective devices are drawn in their rest position by
convention, so they conduct unless the parameter `closed` is set to false explicitly. Varistors, TVS diodes and quartz
crystals do not conduct (direct current, normal operation); measuring instruments are ideal: a voltmeter or ohmmeter
carries no current, an ammeter joins its nets (no voltage drop), a wattmeter keeps the circuit reducible. The network is reduced only by
series and parallel steps (no circuit simulation):

- series elements share the current, parallel elements share the voltage;
- resistive groups give the equivalent resistance (*R* = Σ*R*ᵢ, 1/*R* = Σ1/*R*ᵢ), capacitive groups the equivalent
  capacitance (series capacitors share the charge);
- one source (or sources in series or parallel) across the reduced load closes the circuit: the source's voltage and
  current equal the load's (Kirchhoff's voltage law), e.g. the LED series resistor: "4k7" in a 5 V circuit with a 2.1 V
  LED gives *I* = 0.617 mA, and the voltage divider: 10 V over 1 kΩ + 4 kΩ gives 8 V across the 4 kΩ;
- single-line loads (socket outlets, luminaires) on a net fed by one branch add up (Kirchhoff's current law) and share
  its voltage;
- a transformer is a load of its primary circuit and the source of its secondary circuit.

Voltages and currents are magnitudes in consumer orientation; this holds for series-parallel networks driven by one
source. A loop that these steps cannot reduce (a bridge, sources in several places) gets the hint `elec.net.reducible`
("not reducible here – use a circuit simulator"); the element relations still work. Elements without values in a
circuit (motor, generator, relay coil, solenoid, photodiode, diac, wattmeter) keep it reducible, but no value passes
through them; three-phase machines, transistors, triacs, optocouplers, bridge rectifiers, voltage regulators, ICs and
other multi-pin parts are terminals of unknown current, so the network around them is
not reduced.

**Checks** (hints only, overlay, never change anything; catalogue in the design document, section 4):

| Id | Element | Rule | Severity | Source |
|---|---|---|---|---|
| `elec.r.power` | resistor | *P* ≤ *P*_rated (`prated`) | warning | [S7] |
| `elec.led.current` | LED | *I*_F ≤ *I*_F,max (`ifmax`, datasheet) | warning | [S7] |
| `elec.coord.overload` | fuse, MCB, feeder | *I*_B ≤ *I*_n ≤ *I*_z (*I*_z entered by the user, no ampacity tables) | warning | [S9] |
| `elec.rcd.additional` | socket outlet | its circuit has an overcurrent device drawn but no RCD with *I*_Δn ≤ 30 mA (an RCD without rating counts as present; national rules differ) | hint | [S10] |
| `elec.net.reducible` | elements in an irreducible loop | not a series/parallel network | info | section 1.6 |

## Persistence

All state is in the canonical document (`type`, `view`, `params`, `points`, `attach`, `text`); there is no hidden
module state. A JSON round trip reproduces the document and the rendered SVG exactly (tested).

## Limits and next steps

- A wire end docked onto another wire (T junction) is drawn with a junction dot, and a wire drawn over an earlier wire
  hops over it at the crossing (`hops`, on by default, action `elec.wire.hops`); wires that merely cross are never
  connected. The rule check knows no pin types (output against output, unpowered nets).
- A wire end docked on another wire or a bus (its rail) is attached to that rail (a branch): it follows when the
  other wire moves or changes, and the netlist joins both wires into one net.
- Labels lie outside the object box, so host selection frames do not include them.
- Values (Calc 3): no AC impedance, reactance, time constants or resonance yet (no frequency variable); no equivalent
  inductance; no polarity (magnitudes only); motor and generator values are left to the Machines module; potentiometer,
  transistor base resistor and op-amp gain are open. Two markings that disagree (e.g. a 24 V lamp on a 12 V transformer
  secondary) are not reported as a conflict yet, because a value read from a marking does not count as entered in the
  solver's consistency check (Core change proposed). Cable voltage drop (`elec.cable.drop`) waits for cable values.
- Visual review: `dotnet test` writes an SVG gallery to
  `tests/InventaSketch.Modules.Electrical.Tests/bin/Debug/net10.0/gallery/index.html` (ports shown as red circles,
  plus an example circuit with attached wires).
