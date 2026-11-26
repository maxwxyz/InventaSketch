# Switchgear & installation module (`switchgear`)

Library: `src/InventaSketch.Modules.Switchgear` · Tests: `tests/InventaSketch.Modules.Switchgear.Tests` ·
German version: [../../de/modules/switchgear.md](../../de/modules/switchgear.md) · Roadmap: item 13 "Electrical
installation and switchgear".

The module draws **distribution boards and control cabinets on TS35 DIN rails** and the **installation devices in the
rooms** they feed: modular devices with front views that look like the real devices (lever, rating "B16", "30 mA", test
button, screw terminals), industrial devices (motor protection circuit breaker, overload relay, NH fuse
switch-disconnector, frequency inverter, PLC modules, safety relay), circuit symbol views, terminal blocks with bridges,
rails, comb busbars, blanking plates, ducts, end clamps and labels, wires and cables. Designations and ratings are
optional parameters shown on the devices; five **installation reports** (CSV and Markdown) are generated from the page.
Optional **element values** add up the loads per circuit and per phase and check a few plausibility rules (see "Values
and checks").

It is a **sketching and documentation** module: it sums entered loads, but it does not size cables, check selectivity
or verify protective measures, and it claims no compliance with a standard. Front views are drawn independently after the common look of
modular devices; no manufacturer design or standard artwork is copied. The module draws everything itself and depends
on no other module (its circuit symbols are its own, not the electrical module's).

## Conventions

- **Quick sketch first:** every library item inserts without parameters. Free (uniform or non-uniform) scaling changes
  `Size` only, never parameters.
- **Modules:** one module ("Teilungseinheit", TE) = 17.5 mm. Every modular device is a whole number of modules wide
  (1, 2, 3, 4, 8, 12) and 90 mm high; the width follows the poles, channels or power. Changing such a parameter resizes
  the box with the same scale and **keeps its left edge**, so the device stays in its place on the rail.
- **Rail and snapping:** the TS35 rail (35 mm) has `Mount` anchors every module along its centre line (the grid is
  centred on the rail; its pitch is half the rail height, so a scaled board keeps its grid). Devices and terminals have
  `Mount` anchors at the left and right edge on their centre line: they snap onto the rail grid and side by side.
- **Pins:** every connection point is a `Port` anchor with a medium: screw terminals `electric` (connector: the
  electrical module's wire, item `elec.wire.bk` – blue `elec.wire.bu` at `N…` pins and neutral terminals,
  green-yellow `elec.wire.pe` at `PE` pins and PE terminals; without the electrical module the switchgear's own cores),
  bus terminals `data` (connector: bus cable item `sw.line.cable.knx`), room devices
  `electric.installation` (connector: cable `sw.line.cable`). Wire and cable ends dock there and follow the device; "+"
  builds on (connector, next element). Pin ids follow the usual terminal markings: poles `1/2`, `3/4`, `5/6`, neutral
  `N1` (top) / `N2` (bottom), coils `A1/A2`, changeover contacts `11/12/14`, timer `15/16/18`, surge protection
  `L1…L3`, `N`, `PE`, bus `bus`, bus actuator channels `A1/A2`, `B1/B2` …, terminals `1/2` (levels `3/4`, `5/6`);
  motor protection, overload relay and NH fuse switch `1/2`, `3/4`, `5/6` (overload relay auxiliary contacts `95/96`
  NC, `97/98` NO), frequency inverter `L1…L3` (single-phase `L`, `N`) → `U`, `V`, `W`, PLC modules `L+`, `M` and the
  channels `I0…`, `Q0…`, `AI0…`, `AQ0…` (CPU: network `X1`, medium `data`), safety relay `A1/A2`, `S11/S12`,
  `S21/S22`, `13/14`, `23/24`, comb busbar teeth `t1…tn` (on its bottom edge at the module centres, where the top pins
  of single-module devices are).
- **Views:** modular devices and terminals have a *front view* (default) and a *circuit symbol* view (IEC 60617-like
  contacts, coils, terminals, drawn open/unoperated, with dashed mechanical link, residual current toroid and IΔ release,
  designation and rating at the right). Both views have the same pin ids; switching keeps the visual scale.
- **Designations** follow the ideas of IEC 81346 (letter codes after IEC 81346-2): `-F` protecting (MCB, RCD, RCBO,
  SPD, overload relay, NH fuse switch), `-Q` switching energy (main switch, contactor, motor protection), `-K` processing
  signals (relays, timer relays, bus devices, PLC modules, safety relay), `-P` presenting (meter, indicator lamp), `-T`
  converting (power supplies, frequency inverter), `-S` manual input (push button, switch),
  `-X` connecting (terminals, sockets), `-W` cables. They are free text: "Designation …" pre-fills the letter code.
  **Equal designations belong together** (one node in the circuit plan, one cable in the cable list).
- **Choices are dropdowns** (`ActionChoices.FromParam`): poles, characteristic, rating, phase, residual current, RCD
  type, coil voltage, contacts, function, colour, cross-section … The lever state is a toggle ("Switched on").

## Object types

| Type id | Element (EN / DE) | Parameters (all optional) |
|---|---|---|
| `sw.protection.mcb` | Miniature circuit breaker / Leitungsschutzschalter | `char` B/C/D, `rating` 6–63 A, `poles` 1, 1+N, 2, 3, 3+N, `phase` L1/L2/L3 (not for 3 poles), `on` |
| `sw.protection.rcd` | Residual current device / Fehlerstromschutzschalter | `poles` 2/4, `rating` 25–100 A, `idn` 10–500 mA, `rcdType` AC/A/F/B, `on` |
| `sw.protection.rcbo` | RCBO / FI/LS-Schalter | `poles` 1+N, 3+N, `char`, `rating`, `idn`, `rcdType`, `phase` (1+N), `on` |
| `sw.protection.spd` | Surge protective device / Überspannungsschutz | `spdType` 1, 1+2, 2, 3, `poles` 1, 1+N, 3, 3+N |
| `sw.switch.main` | Main switch (switch-disconnector) / Hauptschalter | `poles` 1–4, `rating` 32–125 A, `on` |
| `sw.protection.mpcb` | Motor protection circuit breaker / Motorschutzschalter | `rating` set current 0.4–32 A, `on` (rotary handle I/O); 3 modules |
| `sw.protection.overload` | Thermal overload relay / Überlastrelais (thermisch) | `rating` set current 0.4–32 A; 2 modules |
| `sw.fuse.nh` | NH fuse switch-disconnector / NH-Sicherungslasttrenner | `size` NH000/NH00, `rating` 16–160 A (gG), `on` (cover closed/open); 6 modules |
| `sw.drive.vfd` | Frequency inverter / Frequenzumrichter | `power` 0.37–7.5 kW (4–6 modules), `supply` three-phase 400 V / single-phase 230 V |
| `sw.plc.module` | PLC module / SPS-Baugruppe | `kind` CPU, digital inputs, digital outputs, analogue inputs, analogue outputs, `channels` 8/16 (2 or 4 modules; CPU 4) |
| `sw.safety.relay` | Safety relay / Sicherheitsrelais | `function` emergency stop, guard door, light curtain (pictogram), `coil` 24 V DC / 230 V AC; 2 modules |
| `sw.busbar.comb` | Comb busbar / Phasenschiene (Kammschiene) | `phases` 1, 1+N, 3, 3+N (tooth labels L1 L2 L3 …), `positions` 2–24 modules, `section` 10/16 mm² |
| `sw.blank` | Blanking plate / Abdeckstreifen | `modules` 1–24 (45 mm high, the front plate opening) |
| `sw.meter.energy` | Energy meter / Energiezähler | `phases` 1/3 |
| `sw.supply.psu` | DIN-rail power supply / Hutschienennetzteil | `voltage` 5–48 V, `power` 15–120 W (1–4 modules) |
| `sw.switching.contactor` | Installation contactor / Installationsschütz | `contacts` 2 NO, 4 NO, 2 NO + 2 NC, `coil` |
| `sw.switching.relay` | Coupling relay / Koppelrelais | `contacts` 1/2 changeovers, `coil` |
| `sw.switching.impulse` | Impulse relay / Stromstoßschalter | `contacts` 1/2 NO, `coil` |
| `sw.switching.timer` | Timer relay / Zeitrelais | `function` on-delay, off-delay, staircase, flasher, `time`, `coil` |
| `sw.control.pushbutton` | Push button for DIN rail / Taster für Hutschiene | `contact` NO/NC, `colour` |
| `sw.control.switch` | Switch for DIN rail / Schalter für Hutschiene | `kind` 0-I / I-0-II, `on` |
| `sw.control.lamp` | Indicator lamp / Leuchtmelder | `colour` |
| `sw.socket.dinrail` | Socket outlet for DIN rail / Steckdose für Hutschiene | – |
| `sw.bus.actuator` | Bus actuator / Busaktor | `bus` KNX/generic, `function` switching, dimming, blinds, binary input, `channels` 1–12 |
| `sw.bus.coupler` | Bus coupler / Buskoppler | `bus` |
| `sw.bus.power` | Bus power supply / Busspannungsversorgung | `bus`, `current` 160/320/640 mA |
| `sw.terminal.feedthrough` | Feed-through terminal block / Durchgangsreihenklemme | `section` 1.5–16 mm² (width 4.2–12.2 mm), `number`, `target`, `bridge` |
| `sw.terminal.n` | Neutral terminal (blue) / N-Reihenklemme | as feed-through |
| `sw.terminal.pe` | Protective earth terminal / Schutzleiterklemme | as feed-through |
| `sw.terminal.fused` | Fused terminal block / Sicherungsreihenklemme | `fuse` 1–10 A, `number`, `target` |
| `sw.terminal.multilevel` | Multi-level terminal / Etagenreihenklemme | `levels` 2/3, `section`, `number`, `target`, `bridge` |
| `sw.terminal.bridge` | Plug-in bridge / Steckbrücke | `positions` 2–10, `section` (pitch) |
| `sw.rail.ts35` | DIN rail TS35 / Hutschiene TS35 | `ref` (rail name) |
| `sw.rail.endclamp` | End clamp / Endhalter | – |
| `sw.duct.slotted` | Slotted cable duct / Verdrahtungskanal | – (vertical when taller than wide) |
| `sw.label.strip` | Labelling strip / Beschriftungsstreifen | `content` designation and circuit / designation / circuit |
| `sw.label.plate` | Label plate / Bezeichnungsschild | free text |
| `sw.line.wire` | Wire (core) / Ader – kept for existing slides; new cores are the electrical module's `elec.wire` | `colour` (IEC 60757 codes BK, BN, GY, BU, GNYE, RD, DBU, WH, VT, OG), `section`, `ref` (wire number), `hops` |
| `sw.line.cable` | Cable / Kabel | `cableType` NYM-J, NYY-J, H07V-K, H05VV-F, J-Y(ST)Y, Cat, `cores`, `section`, `ref`, `length` (m), `from`, `to`, `iz` (current-carrying capacity, A), `hops` |

**One wire type (2026-11-26).** The cores in the cabinet are the electrical module's wires (`elec.wire` with core
colour, cross-section, the generic designation as wire number, hops at crossings, junction dots and "Make bridge (not
connected)"). The four core items of this module (`sw.line.wire`, `.bn`, `.bu`, `.pe`) give way to the electrical
items "Wire, black (cabinet)", "Wire, brown (L)", "Wire, blue (N)", "Wire, green-yellow (PE)" while the electrical
module is registered (`LibraryItem.Alternative`, as for the room devices); the type `sw.line.wire` stays registered, so
existing slides keep loading and feeding the reports, and it now hops at crossings and draws junction dots like the
electrical wire. Modules do not reference each other: the switchgear knows the electrical wire by its id and its
parameters (`colour`, `section`, `ref`). The reports, the feed tree, the circuit values and the inverter's value port
follow both wire types; the terminal plan lists an electrical wire's colour and cross-section only when they are set.
The cable (`sw.line.cable`, also the KNX bus cable) is a different thing – a sheathed multi-core cable with
designation, type, cores × cross-section and length for the cable list, not a single core and not the floor-plan cable
route `elec.house.cable` – and stays; it hops at crossings too (`hops`, "Hops at crossings on/off").
| `sw.house.socket` | Socket outlet (room/circuit) / Steckdose (Raum/Stromkreis) | `gangs` 1–3, `room`, `feed`, `ref`, `phase` |
| `sw.house.switch` | Light switch (room/circuit) / Lichtschalter | `kind` one-way, two-way, intermediate, push button, dimmer, `room`, `feed`, `ref`, `phase` |
| `sw.house.light` | Light outlet (room/circuit) / Leuchtenauslass | `kind` ceiling/wall, `room`, `feed`, `ref`, `phase` |
| `sw.house.appliance` | Appliance connection (room/circuit) / Geräteanschluss | `kind` cooker, washing machine, dryer, dishwasher, water heater, appliance, `room`, `feed`, `ref`, `phase` |

Every modular device also has `ref` (designation), `circuit` (circuit name, the label text) and `feed` (designation of
the device that feeds it); circuit breakers and RCBOs also `room`. Terminals use `ref` for the strip designation
("-X1") and `number` for the terminal number. The value parameters (`ib`, `power`, `iz`, `current`, `voltage` …, see
"Values and checks") are optional text parameters in the advanced part of the inspector.

The library offers 88 items (43 types) in eight categories (protection & isolation; switching & control; meters,
supplies & bus devices; drives & automation; terminal blocks; rails, ducts & labels; wires & cables; installation devices
in the rooms), e.g. "MCB B16, 1-pole", "RCD 40 A 30 mA, 4-pole", "Motor protection circuit breaker 4 A", "NH00 fuse
switch-disconnector 63 A", "Frequency inverter 0.75 kW, 3~", "PLC digital input module, 8 channels", "Comb busbar
3-phase (12 modules)", "Blanking plate (6 modules)", "KNX switch actuator, 4-fold", "Cable NYM-J 5×2.5". With the
electrical module registered, the four core items and the eight room devices step back from galleries and search in
favour of the canonical items of the electrical module and the building services.

## Actions

- Choices (dropdowns) for every choice parameter of the element; changing poles, channels, power or cross-section
  resizes the box (left edge kept).
- **Switched on** (toggle): lever up/down, contact window red/green (circuit breakers, RCDs, RCBOs, main switch, switch);
  rotary handle upright/across (motor protection); cover closed/open (NH fuse switch).
- **Designation …** (all devices, terminals, wires, cables), **Rail name …**, **Length …** (cables).
- **Number the strip** (terminals): numbers the terminals with the same strip designation on the page (without a
  designation: the terminals of the same row) 1, 2, 3 … from left to right.
- **One position more / fewer** (plug-in bridge); **One module more / fewer** (comb busbar, blanking plate), resizing
  the box with its left edge kept.
- **Phase** (dropdown): L1/L2/L3 on single-pole circuit breakers and RCBOs 1+N (not offered for three-phase devices);
  From the circuit / L1 / L2 / L3 / Three-phase on room devices. Every option has a pictogram (`IconIds.ForPhase`):
  three busbar stubs of which the chosen one runs on and down to an arrow (`phase-l1`, `phase-l2`, `phase-l3`), all
  three for three-phase (`phase-3`), the circuit-breaker symbol for "From the circuit" (`phase-circuit`).
- Room devices: **Room …** and **Circuit …** (the designation of the feeding device, e.g. "-F3", shown at the symbol);
  a phase chosen for the room device itself is shown at its top right ("L2", "3~").

## Values and checks

The module takes part in the engineering calculations (element values in the Values panel, the values label, checks;
`docs/en/design/engineering-calculations.md`). Every value is optional and every item still inserts without one;
derived values are never stored.

| Element | Variables (card values first) | Parameters |
|---|---|---|
| MCB, RCBO, motor protection, overload relay, NH fuse switch | I_n rated current (motor protection and overload relay: set current I_r), I_B operating current of the circuit, P power of the circuit; I_z current-carrying capacity of the cable, I_a instantaneous tripping current (MCB, RCBO), I_Δn (RCBO); not on the overload relay: I_k1 and I_k3 at the board (from the main switch upstream), I_cn rated short-circuit capacity; engineer (read-only, from the device upstream) I_n,up, I_a,up | `rating` (the rating choice itself), `ib`, `power`, `iz`, `ia`, `idn`, `ik1`, `ik3`, `icn`, `inUp`, `iaUp` |
| RCD | I_n, I_B, P; I_Δn | `rating`, `ib`, `power`, `idn` |
| Main switch | I_n, I_B, P, I_k1 short-circuit current phase to PE at the board; I_k3 three-phase short-circuit current, Z_s loop impedance, S_r and u_k of the supply transformer, l and A of the supply cable; engineer U_n (default 400 V), ρ (copper at 20 °C, default 0.017241 Ω·mm²/m), Z_T, Z_L | `rating`, `ib`, `power`, `ik1`, `ik3`, `zs`, `trafoPower`, `uk`, `supplyLength`, `supplySection`, `systemVoltage`, `resistivity`, `zTrafo`, `zLine` |
| Room devices | P power, I current per phase; U voltage phase to neutral (default 230 V), cos φ (default 1) | `power`, `current`, `voltage`, `powerFactor` |
| Frequency inverter | P_N rated power (the `power` choice in kW), f output frequency (set point), P_out output power, I input current per phase; P input power, U supply voltage phase to neutral (default 230 V), cos φ input power factor (default 1), η efficiency (default 100 %: losses neglected), U_out output voltage (line-to-line), I_out output current, f_min and f_max set range, P_N,M rated power of the connected motor (read-only); engineer f_b base frequency (default 50 Hz), U_b output voltage at f_b (default 400 V, single-phase supply 230 V) | `power`, `frequency`, `powerOut`, `current`, `powerIn`, `voltage`, `powerFactor`, `efficiency`, `voltageOut`, `currentOut`, `frequencyMin`, `frequencyMax`, `motorPower`, `baseFrequency`, `baseVoltage` |
| DIN rail | I_L1, I_L2, I_L3 current per phase, P power of the board; ΔI imbalance, ΔI_max permitted imbalance (default 20 A) | `currentL1` … `currentL3`, `power`, `imbalance`, `imbalanceMax` |

- **Loads:** I = P/(U·cos φ) single-phase, I = P/(3·U·cos φ) three-phase (symmetrical, U phase to neutral) [S7].
- **Frequency inverter (2026-11-26, round 8):** supply side as a load (I = P/(3·U·cos φ) three-phase, P/(U·cos φ)
  single-phase), P_out = η·P [S15], output voltage after a linear V/f characteristic U_out = U_b·min(f, f_b)/f_b, constant
  above the base frequency (field weakening; no voltage boost at low frequency) [S15]. The display shows the entered
  set point ("35.0 Hz"; 50.0 Hz without one).
- **Value port at the motor terminals:** the one element of another module wired to the output pins `U`, `V`, `W`
  (wires and cables of any module, end to end and through lines docked on them, and through terminal blocks top ↔
  bottom; another switchgear device ends the search) is the motor. It takes f (`sw.vfd.port.f`), U_out as its terminal
  voltage `U` (`sw.vfd.port.u`) and P_out as its electrical power (`Pel`, else `P`: the `NetKeys` name list;
  `sw.vfd.port.p`), and gives back its line current (I_out) and its rated power `PN` (P_N,M). So a motor block or
  e-machine of the machines module gets its synchronous speed n_s = 60·f/p and, with a speed, its slip from the
  inverter (35 Hz, 4 poles: 1050 min⁻¹), and the three-phase motor `elec.motor` (supply 3~) of the electrical module
  its voltage and power. The power passes only when it is known on one side (a power or current entered on the motor,
  or an input/output power or current on the inverter); otherwise the inverter is taken at its rated power,
  P_out = P_N (`sw.vfd.rated`, a sizing assumption as before), and that assumption is not passed to the motor. Several
  motors on one inverter (group drive) give no port. The motor's own module also finds the inverter at its phase
  terminals (by the type id `sw.drive.vfd`), so the motor's value label alone shows the inverter's frequency (machines)
  or voltage (electrical) with electrical wires as well. A motor entered with its nameplate voltage while the inverter
  runs below the base frequency shows a conflict: the terminal voltage there is U_out.
- **Totals:** the inverter's input power and current count in the circuit and rail totals when its input or output
  power, current or rating is entered (the library items carry the rating) or the motor at its output has a power or
  current entered.
- **Circuits:** the circuit of a device is its part of the feed tree of the circuit plan (same rules: "fed from",
  wiring, rail order); an RCD or main switch covers everything below it, a frequency inverter is a load of the device
  that feeds it. P = sum of the load powers; I_B = sum of the load currents for a single-phase device, the largest
  phase sum for a three-phase device (a three-phase load counts on every phase) [S7]. Only loads with a power or a
  current entered count; a circuit without them has no I_B. No diversity (simultaneity) factor is applied.
- **Instantaneous tripping:** I_a = 5·I_n, 10·I_n, 20·I_n for characteristic B, C, D (upper limit of the range) [S11].
- **Rated current:** I_n is read from the rating choice ("16" → 16 A); a value entered in the Values panel is written
  back into the choice as text ("20"), so the device shows it.
- **Cable:** I_z is typed on the device or on a cable of the circuit (cable parameter `iz`; a cable docked at a bottom
  pin of the device or at one of its loads, the smallest value wins). The module knows the cross-section but does not
  derive I_z from it: the current-carrying capacity depends on the installation method, grouping and ambient
  temperature (IEC 60364-5-52 tables, not copied).
- **Phases:** in a single-phase circuit the device's `phase` decides; in a three-phase circuit the load's own `phase`
  ("from the circuit" = three-phase); a load without a drawn feeding device uses its own choice ("from the circuit" =
  L1). An inverter follows its supply (three-phase, or the phase of its circuit).
- **Rail totals:** I_Lk = sum of the currents of the loads on phase k, P = sum of the powers, each load counted once
  through the nearest device on a rail that feeds it; ΔI = largest − smallest phase current.

| Check | Severity | Rule | Source |
|---|---|---|---|
| `sw.load.rating` | warning | I_B ≤ I_n (protective devices, RCD, main switch): the loads draw more than the rating | S9 |
| `sw.rating.cable` | warning | I_n ≤ I_z (overcurrent protective devices; only when an I_z is known) | S9 |
| `sw.phase.imbalance` | warning | I_max − I_min ≤ ΔI_max on a rail; the default 20 A is a plausibility value, enter your grid operator's limit | – |
| `sw.disconnect` | warning | I_k1 at the board ≥ I_a (MCB, RCBO): a fault phase to PE trips the device instantaneously (automatic disconnection, Z_s·I_a ≤ U_0) | S10 |
| `sw.breaking` | warning | I_k3 at the board ≤ I_cn (only when an I_cn is entered) | S11 |
| `sw.selectivity` | hint | I_n,up ≥ 1.6·I_n against the overcurrent device upstream (rule of thumb for gG fuses; a hint, not a selectivity study) | S11 |
| `sw.selectivity.instant` | hint | I_k3 at the board < I_a,up of a circuit breaker upstream (otherwise both trip at once: no current selectivity) | S11 |
| `sw.rcd.type` | hint | RCD/RCBO type ≥ the type a frequency inverter below needs (three-phase B, single-phase F; order AC < A < F < B) | S11 |
| `sw.vfd.rating` | warning | P_N,M ≤ P_N and P_out ≤ P_N: the motor (its rated power or its electrical power at the operating point) needs more than the inverter's rated power | – |
| `sw.vfd.range` | warning | f_min ≤ f ≤ f_max (only the limits entered) | – |

**Short circuit, selectivity and RCD type (2026-11-26).** The main switch carries an estimate of the short-circuit
currents at the board: Z_T = u_k·U_n²/S_r [S7], Z_L = ρ·l/A [S12], Z_s = Z_T + 2·Z_L (phase and protective conductor of
the same cross-section) [S10], I_k1 = U_n/(√3·Z_s) and I_k3 = U_n/(√3·(Z_T + Z_L)) [S7, S10]. Impedances are added as
magnitudes; the upstream grid, reactances and the voltage factor c are neglected and the conductor is at 20 °C, so this is
an estimate, not a short-circuit calculation (IEC 60909 is not implemented). A measured loop impedance can be entered as
Z_s directly. Example: 630 kVA, u_k = 4 %, 50 m of 95 mm² copper give Z_s = 28.3 mΩ, I_k1 ≈ 8.2 kA and I_k3 ≈ 12.0 kA.
Every overcurrent device below the main switch in the feed tree receives I_k1 and I_k3 (values at the board: the circuit's
own cable lowers I_k further, which is not modelled) and the rated and instantaneous tripping currents of the nearest
overcurrent device above it (not an RCD, main switch or overload relay). An RCD or RCBO looks at the frequency inverters
below it. Library items: `sw.switch.main.supply` (main switch with short-circuit keywords), `sw.protection.rcd.f` (type F).

## Labelling strip

`sw.label.strip` reads the devices of the nearest row (above or below, within 120 mm, whose centre lies within the
strip's length) and draws one cell per device, as wide as the device, with the designation and the circuit name (or the
rating when no name is given). Without devices nearby it shows empty cells per module. Devices report nearby strips as
dependents, so the strip is redrawn when a device changes.

## Exports (`IExportProvider`)

Each report exists as CSV (`text/csv`, comma separated, quoted where needed) and as Markdown (`text/markdown`). The
engine lists them with `exporters` and runs them with `export` (ids below with `.csv` / `.md`). Data come from the
parameters of the elements on the page (or the selection) and from the wires and cables docked on their pins.

| Id | Report | Content |
|---|---|---|
| `sw.installation` | House installation list | Rooms → circuits (designation, name, protective device and rating) → devices with quantities. Markdown: one section per room with a nested list. |
| `sw.circuits` | Circuit plan | Feed tree: main switch, meter, SPD, RCDs, circuit breakers, RCBOs, motor protection, overload relays and NH fuse switches by designation, each with the device feeding it and **where that link comes from**; circuits with rooms and devices (room devices and frequency inverters). Markdown: nested list plus the table. |
| `sw.layout` | Board layout and labelling strip | Per rail (top to bottom, named by `ref` or "Rail n"): devices left to right with position, first module, width in modules, designation, device, rating, label; modules used / available (blanking plates count as free); the labelling strip as a two-row table. Accessories (end clamps, bridges, comb busbars, blanking plates) are not listed. Devices on no rail are listed last. |
| `sw.terminals` | Terminal plan | Per strip, terminals in number order (natural order), per level and side (top/bottom): wire or cable, target (designation and pin at the other end, e.g. "-F2:2"), cross-section, colour, bridge. A terminal's `target` gives the outside target when no line is docked. |
| `sw.cables` | Cable list | Designation, type, cores × cross-section, length (m), from, to, number of segments. Segments with the same designation (e.g. on several slides) are one cable: lengths add up, the outer ends are from and to. |

How the circuit plan links devices (in this order): (1) the `feed` parameter ("Fed from"), source "parameter";
(2) the wiring: a wire or cable from a bottom pin of a feed-tree device to a top pin of this device (room devices: any
pin), source "wiring"; (3) for circuit breakers only: the nearest RCD to the left on the same rail, source "rail order
(assumed)" – the usual layout with a comb busbar; the report marks it as assumed. Room devices hang below the
designation in their `feed`; a designation that is named but not drawn appears as "(not drawn)", nothing is invented.

## Overlaps with other modules (one element, one item)

The room devices draw the same elements as the house installation symbols of the electrical module and the appliance
outlet of the building services. Those are the canonical items (plan, section and schematic views, mounting height,
pins and circuit values), so while their module is registered the room device items step back from galleries and search
(`LibraryItem.Alternative`):

| Item here | gives way to |
|---|---|
| `sw.house.socket`, `sw.house.socket.double` | `elec.house.socket`, `elec.house.socket.double` |
| `sw.house.switch`, `.twoway`, `.push` | `elec.house.switch`, `.twoway`, `.push` |
| `sw.house.light` | `elec.house.light` |
| `sw.house.appliance`, `sw.house.appliance.washer` | `bs.elec.appliance`, `bs.elec.appliance.washer` |

`sw.house.light.wall` has no counterpart and stays. The types stay registered: existing slides with room devices load,
render and feed the installation list and the circuit plan as before, and the module on its own still offers every
item.

**The canonical symbols are room devices of the reports and values, too.** `elec.house.socket`, `elec.house.switch`,
`elec.house.light` and `bs.elec.appliance` carry the same optional parameters `room`, `feed` and (loads) `phase`,
`power`, `current`, `voltage` (the appliance outlet also `powerFactor`). The installation list, the circuit plan, the
feed tree and the circuit values read them by type id (modules do not reference each other): names in the reports
follow the kind ("Double socket outlet", "Dimmer", "Luminaire", "Cooker connection"); a cable or wire from the symbol's
pin (`feed`, `n`/`e`/`s`/`w`, `cable`) to a bottom pin of a protective device decides the circuit like for room devices,
an installation cable `elec.house.cable` included. Socket outlet, luminaire and appliance outlet are loads (the switch
takes no power); their current comes from their own module (*P* = *U*·*I*; appliance outlet with cos φ). Without a
voltage entered on them they are on 230 V phase to neutral in a circuit (relation `sw.load.voltage`, as the room devices'
default). They are single-phase unless their own `phase` is three-phase (only the appliance outlet offers it): on a
three-pole device "from the circuit" means L1 for them, not three-phase.

## Persistence

Everything is stored in parameters (plus the line points and attachments of wires and cables); JSON and compact round
trips render identically (tests). Nothing is derived and stored: the labelling strip, the reports and the derived values
are recomputed from the document. Entered values are text parameters ("2 kW", "13 A"); the rated current stays in the
rating choice.

## Host capabilities

| Capability | PowerPoint COM | Office web / LibreOffice (engine) |
|---|---|---|
| Insert, scale, rotate, view switch, choices, toggles, text inputs | yes (engine) | yes (engine) |
| Native shapes | rectangles, rounded rectangles, ellipses, lines, polylines, polygons, arcs, text; no vector picture needed | same |
| Snapping on the rail and side by side (`Mount` anchors), wire ends on pins (`Port`) | yes | yes |
| Labelling strip follows the devices (`DependsOnNeighbours`, dependents) | yes: devices and strip are rendered with the slide's units as context, as walls and rays are | yes |
| **Number the strip** (changes other terminals, `ApplyInDocument`) | changes only the selected terminal until the add-in applies changes of other objects from an action result (same limit as "Add ray bundle" in optics) | yes |
| Exports (CSV, Markdown) | through the engine's `exporters`/`export` where the host offers domain exports | same |

All drawing uses native primitives. Monochrome output is handled by the hosts as for other modules (colours become
black and white).

## Limits and next steps

- Calculations are limited to load sums per circuit and phase, a short-circuit estimate at the board and the checks
  above: no voltage drop, cable sizing, tripping curves (selectivity is a rule-of-thumb hint), loop impedance of the
  final circuits or RCD coverage checks; I_z is entered, not looked up. Surge protective devices carry no values (their
  coordination with the back-up fuse is open).
- The rail-order rule for MCBs is an assumption; draw wires or set "Fed from" for certainty.
- Cable lengths are typed (m); they are not measured from the drawing.
- Frequency inverter: linear V/f only (no boost, no vector control, no slip compensation); the input power factor is a
  plain cos φ (harmonics of the rectifier are not modelled); one motor per inverter for the power port; output
  current limits and the inverter's rated current are not checked.
- The circuit symbol view is a compact per-device symbol, not a full multi-line circuit diagram; the reports, not the
  symbols, carry the circuit structure.
- Possible next steps: the phase of a single-pole device from the comb busbar tooth it sits under, phase and load
  columns in the circuit plan, link from room devices on floor-plan slides to the board ("→ -F3 slide 4").
