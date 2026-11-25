# Switchgear & installation module (`switchgear`)

Library: `src/InventaSketch.Modules.Switchgear` · Tests: `tests/InventaSketch.Modules.Switchgear.Tests` ·
German version: [../../de/modules/switchgear.md](../../de/modules/switchgear.md) · Roadmap: item 13 "Electrical
installation and switchgear".

The module draws **distribution boards and control cabinets on TS35 DIN rails** and the **installation devices in the
rooms** they feed: modular devices with front views that look like the real devices (lever, rating "B16", "30 mA", test
button, screw terminals), circuit symbol views, terminal blocks with bridges, rails, ducts, end clamps and labels, wires
and cables. Designations and ratings are optional parameters shown on the devices; five **installation reports** (CSV
and Markdown) are generated from the page.

It is a **sketching and documentation** module: it does not dimension circuits, check selectivity or verify protective
measures, and it claims no compliance with a standard. Front views are drawn independently after the common look of
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
- **Pins:** every connection point is a `Port` anchor with a medium: screw terminals `electric` (connector: wire
  `sw.line.wire`), bus terminals `data` (connector: bus cable item `sw.line.cable.knx`), room devices
  `electric.installation` (connector: cable `sw.line.cable`). Wire and cable ends dock there and follow the device; "+"
  builds on (connector, next element). Pin ids follow the usual terminal markings: poles `1/2`, `3/4`, `5/6`, neutral
  `N1` (top) / `N2` (bottom), coils `A1/A2`, changeover contacts `11/12/14`, timer `15/16/18`, surge protection
  `L1…L3`, `N`, `PE`, bus `bus`, bus actuator channels `A1/A2`, `B1/B2` …, terminals `1/2` (levels `3/4`, `5/6`).
- **Views:** modular devices and terminals have a *front view* (default) and a *circuit symbol* view (IEC 60617-like
  contacts, coils, terminals, drawn open/unoperated, with dashed mechanical link, residual current toroid and IΔ release,
  designation and rating at the right). Both views have the same pin ids; switching keeps the visual scale.
- **Designations** follow the ideas of IEC 81346 (letter codes after IEC 81346-2): `-F` protecting (MCB, RCD, RCBO,
  SPD), `-Q` switching energy (main switch, contactor), `-K` processing signals (relays, timer relays, bus devices),
  `-P` presenting (meter, indicator lamp), `-T` converting (power supplies), `-S` manual input (push button, switch),
  `-X` connecting (terminals, sockets), `-W` cables. They are free text: "Designation …" pre-fills the letter code.
  **Equal designations belong together** (one node in the circuit plan, one cable in the cable list).
- **Choices are dropdowns** (`ActionChoices.FromParam`): poles, characteristic, rating, residual current, RCD type,
  coil voltage, contacts, function, colour, cross-section … The lever state is a toggle ("Switched on").

## Object types

| Type id | Element (EN / DE) | Parameters (all optional) |
|---|---|---|
| `sw.protection.mcb` | Miniature circuit breaker / Leitungsschutzschalter | `char` B/C/D, `rating` 6–63 A, `poles` 1, 1+N, 2, 3, 3+N, `on` |
| `sw.protection.rcd` | Residual current device / Fehlerstromschutzschalter | `poles` 2/4, `rating` 25–100 A, `idn` 10–500 mA, `rcdType` AC/A/F/B, `on` |
| `sw.protection.rcbo` | RCBO / FI/LS-Schalter | `poles` 1+N, 3+N, `char`, `rating`, `idn`, `rcdType`, `on` |
| `sw.protection.spd` | Surge protective device / Überspannungsschutz | `spdType` 1, 1+2, 2, 3, `poles` 1, 1+N, 3, 3+N |
| `sw.switch.main` | Main switch (switch-disconnector) / Hauptschalter | `poles` 1–4, `rating` 32–125 A, `on` |
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
| `sw.line.wire` | Wire (core) / Ader | `colour` (IEC 60757 codes BK, BN, GY, BU, GNYE, RD, DBU, WH, VT, OG), `section`, `ref` (wire number) |
| `sw.line.cable` | Cable / Kabel | `cableType` NYM-J, NYY-J, H07V-K, H05VV-F, J-Y(ST)Y, Cat, `cores`, `section`, `ref`, `length` (m), `from`, `to` |
| `sw.house.socket` | Socket outlet (room/circuit) / Steckdose (Raum/Stromkreis) | `gangs` 1–3, `room`, `feed`, `ref` |
| `sw.house.switch` | Light switch (room/circuit) / Lichtschalter | `kind` one-way, two-way, intermediate, push button, dimmer, `room`, `feed`, `ref` |
| `sw.house.light` | Light outlet (room/circuit) / Leuchtenauslass | `kind` ceiling/wall, `room`, `feed`, `ref` |
| `sw.house.appliance` | Appliance connection (room/circuit) / Geräteanschluss | `kind` cooker, washing machine, dryer, dishwasher, water heater, appliance, `room`, `feed`, `ref` |

Every modular device also has `ref` (designation), `circuit` (circuit name, the label text) and `feed` (designation of
the device that feeds it); circuit breakers and RCBOs also `room`. Terminals use `ref` for the strip designation
("-X1") and `number` for the terminal number.

The library offers 71 items (35 types) in seven categories (protection & isolation; switching & control; meters, supplies & bus
devices; terminal blocks; rails, ducts & labels; wires & cables; installation devices in the rooms), e.g. "MCB B16,
1-pole", "RCD 40 A 30 mA, 4-pole", "KNX switch actuator, 4-fold", "Double-level terminal 2.5 mm²", "Cable NYM-J 5×2.5".

## Actions

- Choices (dropdowns) for every choice parameter of the element; changing poles, channels, power or cross-section
  resizes the box (left edge kept).
- **Switched on** (toggle): lever up/down, contact window red/green (circuit breakers, RCDs, RCBOs, main switch, switch).
- **Designation …** (all devices, terminals, wires, cables), **Rail name …**, **Length …** (cables).
- **Number the strip** (terminals): numbers the terminals with the same strip designation on the page (without a
  designation: the terminals of the same row) 1, 2, 3 … from left to right.
- **One position more / fewer** (plug-in bridge).
- Room devices: **Room …** and **Circuit …** (the designation of the feeding device, e.g. "-F3", shown at the symbol).

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
| `sw.circuits` | Circuit plan | Feed tree: main switch, meter, SPD, RCDs, circuit breakers and RCBOs by designation, each with the device feeding it and **where that link comes from**; circuits with rooms and devices. Markdown: nested list plus the table. |
| `sw.layout` | Board layout and labelling strip | Per rail (top to bottom, named by `ref` or "Rail n"): devices left to right with position, first module, width in modules, designation, device, rating, label; modules used / available; the labelling strip as a two-row table. Devices on no rail are listed last. |
| `sw.terminals` | Terminal plan | Per strip, terminals in number order (natural order), per level and side (top/bottom): wire or cable, target (designation and pin at the other end, e.g. "-F2:2"), cross-section, colour, bridge. A terminal's `target` gives the outside target when no line is docked. |
| `sw.cables` | Cable list | Designation, type, cores × cross-section, length (m), from, to, number of segments. Segments with the same designation (e.g. on several slides) are one cable: lengths add up, the outer ends are from and to. |

How the circuit plan links devices (in this order): (1) the `feed` parameter ("Fed from"), source "parameter";
(2) the wiring: a wire or cable from a bottom pin of a feed-tree device to a top pin of this device (room devices: any
pin), source "wiring"; (3) for circuit breakers only: the nearest RCD to the left on the same rail, source "rail order
(assumed)" – the usual layout with a comb busbar; the report marks it as assumed. Room devices hang below the
designation in their `feed`; a designation that is named but not drawn appears as "(not drawn)", nothing is invented.

## Persistence

Everything is stored in parameters (plus the line points and attachments of wires and cables); JSON and compact round
trips render identically (tests). Nothing is derived and stored: the labelling strip and the reports are recomputed
from the document.

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

- No calculations: no voltage drop, cable sizing, selectivity, loop impedance or RCD coverage checks (candidates for
  the engineering calculations, roadmap item 14, e.g. "a fuse below the cable load").
- The rail-order rule for MCBs is an assumption; draw wires or set "Fed from" for certainty.
- Cable lengths are typed (m); they are not measured from the drawing.
- The circuit symbol view is a compact per-device symbol, not a full multi-line circuit diagram; the reports, not the
  symbols, carry the circuit structure.
- Possible next steps: blanking plates and comb busbars as elements, phase assignment (L1/L2/L3) in the circuit plan,
  load and power totals per circuit, link from room devices on floor-plan slides to the board ("→ -F3 slide 4").
