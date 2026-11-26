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

**Core colours and cross-section (2026-11-26).** The wire is the one wire type of every module: the switchgear devices
connect with it as well (their own core items give way, see the switchgear module). Optional parameters `colour`
(`auto` – black, or the medium colour when lines are coloured by medium – and the IEC 60757 codes BK, BN, GY, BU,
GNYE, RD, DBU, WH, VT, OG; green-yellow is drawn green with yellow dashes) and `section` (mm², none by default). A wire
with a colour keeps it when lines are coloured by medium (`MediumRole` = own colour). The wire number is the generic
designation (`ref`, "Designation …"), drawn by `Designations`. Library items "Wire, black (cabinet)", "Wire, brown
(L)", "Wire, blue (N)", "Wire, green-yellow (PE)" (`elec.wire.bk`, `.bn`, `.bu`, `.pe`; 1.5 mm²). Colours are useful in
electronics too (red/black supply wires, …).

**Junction ⇄ bridge (2026-11-26).** A wire drawn over another one hops (`HopsAtCrossings`, Core `Crossings`); hosts
offer to connect at a crossing (split there, both parts docked: a junction with a dot) and, for two selected wires that
cross, "Connect (junction)" (`Crossings.ConnectBetween`). The reverse is "Make bridge (not connected)" (action
`elec.bridge`, pictogram `Bridge`, `Crossings.Disconnect`): offered by a wire whose own end is docked onto another wire
(a T or X junction) and by a junction dot (`elec.junction`) with wires passing it. Two ends meeting from opposite sides
are joined into one wire that crosses the other with a hop (the corner left at the junction is removed when the wire
runs straight on, else moved 3 mm off the other wire); a single branch is undocked and carried 3 mm
(`Crossings.BridgeOvershoot`) beyond the other wire, so it crosses it; the junction dot is removed. A junction dot
names the wires docked on it as dependents (`HasDependents`), so hosts with separate units see them.

## Context actions

| Action id | Icon | Effect |
|---|---|---|
| `view.<id>` | view icon | switch representation (IEC/ANSI, coils/circles, …) |
| `elec.toggle` | `Toggle` | switch state open/closed (`closed`; NC contacts default to closed) — switch, push button, contactor, breaker, RCD |
| `elec.polarity` | `Swap` | flip polarity (`reverse`: mirrors symbol and pins) — diodes, thyristor, polarised capacitor, source, battery |
| `elec.polarity`/`elec.channel` | `Swap` | NPN ↔ PNP, N ↔ P channel (MOSFET) |
| `elec.<choice>=<value>` | `Option` | choices (dropdowns): gate function, contact type, earth symbol, source type, supply, plug/socket, package, bus protocol, thermistor NTC/PTC (`elec.thermistor.kind`), TVS direction (`elec.tvs.direction`), JFET channel (`elec.jfet.channel`), … |
| `elec.opamp.circuit=<auto/inverting/noninverting/follower/open>` | `Option` | circuit of the op-amp for its gain (from the wiring by default) |
| `elec.meter.quantity=<v/a/w/ohm>` | option pictograms (`Preview`: the instrument circle with its letter) | measured quantity of the measuring instrument |
| `elec.wire.hops` | `Hop` | hops of a wire at crossings on/off |
| `elec.wire.colour=<auto/BK/BN/GY/BU/GNYE/RD/DBU/WH/VT/OG>` | option pictures (`Preview`: a stroke in the colour) | core colour of a wire |
| `elec.wire.section=<–/0.14 … 16>` | `Option` | cross-section of a wire (mm²) |
| `elec.bridge` | `Bridge` | make a junction a bridge again (not connected): wire with an end at a junction, junction dot |
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
| Wires, connectors & buses | `wire` (point object, `route` free/orthogonal/45, `hops`, `colour`, `section`), `junction`, `netlabel` (text = net name), `connector` (plug/socket/pair, 1–12 poles), `terminal`, `testpoint`, `bus` (point object; CAN/LIN/Ethernet/FlexRay/generic label) |
| House installation | `house.socket` (1–4 outlets, protective contact), `house.switch` (one-way/two-way/intermediate/push/dimmer), `house.light`, `house.junction`, `house.board`, `house.cable` (point object with conductor marks), `sld.busbar`, `sld.feeder` (breaker/fuse/none), `sld.meter` |
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
| Capacitor `elec.capacitor`, `elec.capacitor.polarized` | *C* (`value`, "100n"), *U*, *Q* (`charge`), *E* (`energy`); panel *R* (`rseries`, resistance in series), *τ* (`tau`), *f*_c (`cutoff`), *f* (`frequency`), *X*_C (`reactance`); AC (panel): *I* (`current`), *φ*_U, *φ*_I (`phaseu`, `phasei`, read-only), *f*₀ (`resonance`), *Q* (`quality`), *B* (`bandwidth`) | *Q* = *C*·*U*; *E* = ½*C*·*U*² = ½*Q*·*U* = *Q*²/(2*C*); *τ* = *R*·*C*; *f*_c = 1/(2π·*τ*); *X*_C = 1/(2π·*f*·*C*); *U* = *X*_C·*I*; *B* = *f*₀/*Q* |
| Inductor `elec.inductor` | *L* (`value`, "10 mH"), *I*, *E* (`energy`); panel *R* (`rseries`), *τ*, *f*_c, *f*, *X*_L as for the capacitor; AC (panel): *U* (`voltage`), *φ*_U, *φ*_I, *f*₀, *Q*, *B* as for the capacitor | *E* = ½*L*·*I*²; *τ* = *L*/*R*; *f*_c = 1/(2π·*τ*); *X*_L = 2π·*f*·*L*; *U* = *X*_L·*I*; *B* = *f*₀/*Q* |
| Potentiometer `elec.potentiometer` | *R* (`value`, track), *U* (`voltage`, across pins 1–2), *k* (`wiper`, 0 at pin 1 … 100 % at pin 2), *U*₂ (`uwiper`, wiper to pin 2); panel *I*, *P*, *R*₁ (`r1`), *R*₂ (`r2`) | Ohm's law over the track; *R*₁ = *k*·*R*; *R*₂ = *R* − *R*₁; unloaded divider *U*₂ = *U*·*R*₂/*R* |
| Bipolar transistor `elec.transistor.bjt` (as a switch) | *R*_B (`rb`), *I*_C (`ic`), *B* (`beta`, h_FE from the datasheet), *U*_in (`uin`); panel *I*_B (`ib`), *U*_BE (`ube`, default 0.7 V), *k* (`overdrive`, default 3) | *I*_B = *k*·*I*_C/*B*; *R*_B = (*U*_in − *U*_BE)/*I*_B |
| Op-amp `elec.opamp` (ideal) | *A* (`gain`), *U*_in (`uin`), *U*_out (`uout`), *R*_f (`rf`); panel *R*_in (`rin`, *R*_g for the non-inverting amplifier), *U*_S (`usupply`, ±supply); choice `circuit` | *U*_out = *A*·*U*_in; inverting *A* = −*R*_f/*R*_in; non-inverting *A* = 1 + *R*_f/*R*_g; follower *A* = 1 |
| Diode, LED, Schottky diode `elec.diode`, `elec.diode.led`, `elec.diode.schottky` | *U*_F (`uf`), *I*_F (`if`), *P* (`power`); marking "2.1 V 20 mA"; LED: `ifmax` for the check | *P* = *U*·*I* |
| Zener diode `elec.diode.zener` | *U*_Z (`uz`), *I* (`current`), *P* | *P* = *U*·*I* |
| Source `elec.source` | *U*, *I*, *P*; panel \|*Z*\| (`impedance`), *φ* (`phase`), cos *φ* (`pf`), *S* (`apparent`), *Q* (`reactive`) of the load it feeds; frequency `frequency` (a plain parameter, or "50 Hz" in the value text); marking "5 V", "10 mA" (current source), "12 V 2 A", "230 V 50 Hz" | DC and current source: *P* = *U*·*I* with *S* = *P*, cos *φ* = 1, *φ* = 0, *Q* = 0, \|*Z*\| = *U*/*I*; AC: *S* = *U*·*I*; *P* = *S*·cos *φ*; *Q* = *S*·sin *φ*; *S*² = *P*² + *Q*²; *U* = \|*Z*\|·*I* [S6, S7] |
| Battery `elec.battery` | *U*, *I*, *P*; marking "12 V 2 A" | *P* = *U*·*I* |
| Motor, generator `elec.motor`, `elec.generator` | *U* (`voltage`, line voltage for 3~), *I* (`current`, line current), *P* (`power`, electrical active power), cos *φ* (`pf`); panel *S* (`apparent`), *Q* (`reactive`), *η* (`efficiency`), *P*_mech (`pmech`; motor: output, generator: input); motor also *I*_A/*I*_N (`kstart`), *I*_A (`istart`), *I*_A,YΔ (`istartyd`) and the choice `starting` (direct on line / star-delta, three-phase motors) | by `supply`: DC *P* = *U*·*I* (cos *φ* = 1); AC or unspecified *S* = *U*·*I*; three-phase *S* = √3·*U*·*I*; *P* = *S*·cos *φ*; *S*² = *P*² + *Q*² (*Q* ≥ 0, inductive); motor *P*_mech = *η*·*P*, generator *P* = *η*·*P*_mech; *I*_A = (*I*_A/*I*_N)·*I*; star-delta *I*_A,YΔ = *I*_A/3 [S7, S15] |
| Voltage regulator `elec.regulator` | *U*_out (`uout`), *U*_in (`uin`), *I*_out (`current`), *P* (`power`, loss); a voltage in `value` ("LM7805 5 V") is *U*_out | *P* = (*U*_in − *U*_out)·*I*_out (linear regulator, quiescent current neglected) |
| Transformer `elec.transformer` | *U*₁ (`u1`), *U*₂ (`u2`), *I*₁ (`i1`), *I*₂ (`i2`), *n* (`ratio`, panel); marking "230/12 V" | ideal: *n* = *U*₁/*U*₂ = *I*₂/*I*₁ [S7, S15] |
| Fuse, MCB, feeder `elec.fuse`, `elec.breaker`, `elec.sld.feeder` | *I*_B (`current`), *I*_n (`rated`); marking "B16", "C 10 A", "gG 35 A"; `iz` (*I*_z of the cable, user) for the check | – |
| RCD `elec.rcd` | *I*_n (`rated`), *I*_Δn (`idn`); marking "40 A 30 mA", "40/0,03 A", "30 mA" | – |
| Socket outlet, luminaire `elec.house.socket`, `elec.house.light` | *U*, *I*, *P* | *P* = *U*·*I* |
| Cable route `elec.house.cable` | *l* (`length`, one way), *A* (`section`), *I* (`current`), Δ*u* (`droprel`); panel *U*_n (`voltage`), cos *φ* (`pf`, default 1), *ρ* (`rho`, default copper 0.017241 Ω·mm²/m at 20 °C), *R* (`resistance`, one conductor), Δ*U* (`drop`), *P*_loss (`loss`); choices `system` (default from the conductors: 4–5 three-phase, else single-phase; or single-phase, three-phase, DC) and `material` (copper, aluminium 0.028264 Ω·mm²/m); `dumax` for the check | *R* = *ρ*·*l*/*A* [S12]; single-phase Δ*U* = 2·*R*·*I*·cos *φ*, three-phase Δ*U* = √3·*R*·*I*·cos *φ*, DC Δ*U* = 2·*R*·*I* (line reactance *X*′ neglected) [S8]; Δ*u* = Δ*U*/*U*_n; *P*_loss = 2·*I*²·*R* (single-phase, DC) or 3·*I*²·*R* (three-phase) [S7] |

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
circuit (relay coil, solenoid, photodiode, diac, wattmeter) keep it reducible, but no value passes through them; a
single-phase or DC motor is a load and a single-phase or DC generator a source with *U* and *I* (round 6); three-phase machines, transistors, triacs, optocouplers, bridge rectifiers, voltage regulators, ICs and
other multi-pin parts are terminals of unknown current, so the network around them is
not reduced.

**Basic circuits (round 5, 2026-11-26).** Values that read the parts wired around an element (design: section 2.3):

- **RC and RL elements.** A series group of resistors and exactly one capacitor or inductor (an RC or RL element charged
  from a source, a first-order low-pass) gives the capacitor's or inductor's "resistance in series" *R* as the equivalent
  resistance of those resistors (relation `elec.net.rtau`), so *τ* and the cut-off frequency *f*_c follow: 10 kΩ in
  series with 100 nF give *τ* = 1 ms and *f*_c = 159 Hz. *R* may also be entered (no wiring needed), and the reactance
  at a frequency *f* follows from *C* or *L* alone.
- **Potentiometer as a voltage divider.** The track is a resistive branch between pins 1 and 2; the wiper position *k*
  splits it, and the unloaded divider gives the wiper voltage: 10 V across a 10 kΩ potentiometer at *k* = 30 % gives
  *U*₂ = 7 V. A part wired to the wiper makes it a terminal of unknown current, so a loaded divider is not reduced (the
  unloaded relation stays, stated as its validity).
- **Op-amp basic circuits.** The choice **Circuit** (`circuit`, action `elec.opamp.circuit=…`) is "from the wiring"
  (default), inverting, non-inverting, voltage follower or none (comparator, open loop). From the wiring: a single
  resistor between − and the output is *R*_f, a single other resistor at − is *R*_in; *R*_in going to earth (an earth
  symbol or a net label "GND") makes it non-inverting, + on earth inverting, and an output wired straight to − a follower.
  The wired resistors' values are taken over (relation `elec.link.part`): 10 kΩ and 100 kΩ around an inverting op-amp give
  *A* = −10, and 0.5 V in gives −5 V out. A bare op-amp has no gain relation.
- **Transistor as a switch.** A single resistor wired alone to the base is the base resistor: its resistance is *R*_B and
  its current *I*_B. With *I*_C and *B* the overdrive factor *k* = *I*_B·*B*/*I*_C follows (check `elec.bjt.overdrive`).

**AC circuits (round 6, 2026-11-26).** Design: sections 2.1 and 2.2. All values are RMS magnitudes.

- **AC mode.** A circuit that contains an AC source (`kind` ac), a generator on AC or three-phase, or a transformer is an
  AC circuit. Its series voltages and parallel currents add as numbers only while the elements are in phase (all
  resistive, all inductive or all capacitive); a mixed group gets no sum relation, and the phasor relations take its
  place. DC circuits are unchanged.
- **Impedance and phase.** An AC source across a load of resistors, inductors and capacitors (fuses and breakers count as
  ideal conductors) gives the source the impedance |*Z*| and the phase angle *φ* = arg *Z* of the load, with the complex
  impedances *R*, j*ωL* and 1/(j*ωC*) (series: they add; parallel: 1/Σ(1/*Z*ᵢ)). Every inductor and capacitor gets its
  current *I*, its voltage *U* = *X*·*I* and the phases *φ*_U, *φ*_I of its voltage and current against the source voltage
  (read-only); a resistor gets its current (its voltage follows from *U* = *R*·*I*, in phase with the current). The
  frequency is the source's parameter `frequency` or a frequency in its value text ("230 V 50 Hz"); the reactive parts take
  it over as their *f*. Example: 230 V, 50 Hz across *R* = 100 Ω and *L* = 0.318 H in series give |*Z*| ≈ 141 Ω,
  *φ* ≈ 45°, *I* = 1.63 A and cos *φ* ≈ 0.71; 230 Ω in parallel with a capacitor of *X*_C = 230 Ω carry 1 A each, but the
  source supplies √2 A. A purely resistive AC load needs no frequency.
- **AC power.** On an AC source *S* = *U*·*I*, *P* = *S*·cos *φ*, *Q* = *S*·sin *φ* (positive inductive, negative
  capacitive) and *S*² = *P*² + *Q*². cos *φ* follows from *φ*, not the other way round (the sign would be lost). On a DC
  source these values are trivial (*S* = *P*, cos *φ* = 1, *φ* = 0, *Q* = 0, |*Z*| = *U*/*I*), so a DC source with *U* and
  *I* stays fully determined.
- **Resonance.** One inductor and one capacitor in a series or parallel group, alone or with resistors, form a resonant
  circuit: both get *f*₀ = 1/(2π√(*LC*)) (an entered *f*₀ gives the missing *L* or *C*); with resistors also the quality
  factor *Q* = √(*L*/*C*)/*R* (series, *R* in series) or *Q* = *R*·√(*C*/*L*) (parallel, *R* in parallel) and the
  bandwidth *B* = *f*₀/*Q*. At resonance a series RLC circuit is purely resistive (|*Z*| = *R*), and the voltages across
  *L* and *C* are *Q* times the source voltage (hint `elec.ac.resonance`). A coil resistance drawn in series with *L*
  inside a parallel tank ((*R* + *L*) ∥ *C*) is not recognised.
- **Motor and generator.** The symbols carry electrical values by their supply: three-phase *P* = √3·*U*·*I*·cos *φ* with
  line voltage and line current (400 V, 10 A and cos *φ* = 0.85 give 5.89 kW), single-phase *P* = *U*·*I*·cos *φ*, DC
  *P* = *U*·*I*; efficiency and mechanical power (motor: output, generator: input); a motor's starting current from
  *I*_A/*I*_N and, with **Starting** star-delta (`starting`, action `elec.motor.starting=…`, three-phase motors), one third
  of it [S15]. In a circuit a single-phase or DC motor is a load with *U* and *I* (an AC load with a power factor of its
  own, so it is in phase with nothing else), a single-phase or DC generator a source; three-phase machines stay terminals
  of unknown current. Speed and torque stay in the Machines module.
- **Motor on a frequency inverter** (2026-11-26). A three-phase motor (`supply` 3~) wired with its terminals U, V, W to
  the output pins of the switchgear frequency inverter `sw.drive.vfd` (lines of any module) takes the inverter's V/f
  output voltage as *U* and, when a power is known on either side, gives its electrical power *P* as the inverter's
  output power (and its line current back); the inverter's input current then counts in the switchgear circuit totals
  (switchgear module, "Value port at the motor terminals"). The motor also finds the inverter itself (by its type id,
  relation `elec.motor.drive.u`), so its value label alone shows the inverter voltage. The motor has no frequency or
  speed values; for the synchronous speed use a motor of the Machines module.
- **Cable voltage drop.** The installation cable route has optional cable values (table above): from length,
  cross-section and current the conductor resistance, the voltage drop with cos *φ* (single-phase 2·*R*·*I*·cos *φ*,
  three-phase √3·*R*·*I*·cos *φ*, DC 2·*R*·*I*; line reactance neglected), the relative drop and the losses. Any three of
  *l*, *A*, *I* and Δ*u* (with *U*_n) give the fourth, e.g. the cross-section for a permitted drop. The current is entered
  (it is not taken from the circuit). The permitted drop Δ*u*_max (`dumax`) is the user's; the limits of IEC 60364-5-52
  and of national rules are not copied [S8]. 20 m of 1.5 mm² copper at 16 A and 230 V give Δ*U* = 7.4 V, Δ*u* = 3.2 %.

**Checks** (hints only, overlay, never change anything; catalogue in the design document, section 4):

| Id | Element | Rule | Severity | Source |
|---|---|---|---|---|
| `elec.r.power` | resistor | *P* ≤ *P*_rated (`prated`) | warning | [S7] |
| `elec.led.current` | LED | *I*_F ≤ *I*_F,max (`ifmax`, datasheet) | warning | [S7] |
| `elec.coord.overload` | fuse, MCB, feeder | *I*_B ≤ *I*_n ≤ *I*_z (*I*_z entered by the user, no ampacity tables) | warning | [S9] |
| `elec.rcd.additional` | socket outlet | its circuit has an overcurrent device drawn but no RCD with *I*_Δn ≤ 30 mA (an RCD without rating counts as present; national rules differ) | hint | [S10] |
| `elec.net.reducible` | elements in an irreducible loop | not a series/parallel network | info | section 1.6 |
| `elec.opamp.saturation` | op-amp | \|*U*_out\| ≤ *U*_S (the output cannot go beyond the supply) | warning | [S7] |
| `elec.bjt.overdrive` | bipolar transistor | *k* = *I*_B·*B*/*I*_C ≥ 1 (otherwise it does not switch fully on) | warning | [S7] |
| `elec.cable.drop` | cable route | Δ*u* ≤ Δ*u*_max (`dumax`, entered by the user) | hint | [S8] |
| `elec.ac.resonance` | capacitor of a resonant circuit | the working frequency lies within the bandwidth around *f*₀ (\|*f* − *f*₀\| ≤ *B*/2): voltages (series) or currents (parallel) at *L* and *C* far above those of the source | info | [S7] |

## Overlaps with other modules (one element, one item)

The house installation symbols (`elec.house.socket`, `.socket.double`, `.switch`, `.switch.twoway`, `.switch.push`,
`.light`) are the canonical room devices of the catalogue: the switchgear module's room devices (`sw.house.*`) give way
to them while this module is registered (`LibraryItem.Alternative`, mapping in the switchgear documentation). Their
items carry the switchgear names as keywords ("Zweifachsteckdose", "Lichtschalter", "Deckenauslass", "light outlet"), so
search still finds them by those names. The appliance outlet is offered by the building services (`bs.elec.appliance`).

Like the room devices they carry the optional parameters `room` (**Room …**) and `feed` (**Circuit …**, the designation
of the feeding protective device, e.g. "-F3", shown small at the top left of the plan symbol); socket outlet and luminaire
also `phase` (from the circuit / L1 / L2 / L3). The switchgear reports (house installation list, circuit plan) and
circuit values read them: the power or current entered on a socket outlet or luminaire counts in the operating current
I_B of the breaker named in `feed` (or wired to it), at 230 V when no voltage is entered. The switch kinds match the
switchgear light switch: one-way, two-way, intermediate (all four levers), push button, dimmer (adjustment wedge; a
rotary knob in the section view); the kind choice shows each kind's plan symbol as its pictogram.

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
- Values (Calc 3, rounds 5–6): the AC analysis covers series-parallel networks of R, L and C fed by one AC voltage source;
  an AC load that also contains other elements (diodes, motors, transformer primaries) gets no phasor values, and three
  impedances in star or delta are not converted (Y–Δ; the module has no star/delta network element, and the conversion
  would make bridges reducible). No equivalent inductance; no polarity (magnitudes only, the op-amp gain keeps its sign).
  The op-amp and the transistor read only resistors that are clearly in place (one feedback
  resistor, one input resistor, one base resistor alone on the base net). Two markings that disagree (e.g. a 24 V lamp on a 12 V transformer
  secondary) are not reported as a conflict yet, because a value read from a marking does not count as entered in the
  solver's consistency check (Core change proposed). The cable route does not take its current from the circuit, and its
  line reactance and conductor temperature are not modelled (enter *ρ* for a warm conductor).
- Visual review: `dotnet test` writes an SVG gallery to
  `tests/InventaSketch.Modules.Electrical.Tests/bin/Debug/net10.0/gallery/index.html` (ports shown as red circles,
  plus an example circuit with attached wires).
