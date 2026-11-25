# Energy systems module (`energy`)

PV, wind, generator sets, inverters and converters, battery storage, e-mobility, sector coupling (heat pump, CHP,
thermal storage, hydrogen) and the grid connection with single-line diagram elements across voltage levels — for
slides and quick technical sketches of energy systems. Every element has **two views of the same element**:

| View id | Label (EN / DE) | Icon | What it shows |
|---|---|---|---|
| `pictorial` | Pictorial (icon) / Bildhaft (Piktogramm) | `IconIds.Front` | an iconic front view for slides (panels, turbine, cabinets, buildings) |
| `single-line` | Single-line diagram / Einlinienschaltbild | `IconIds.Symbol` | the symbol of a single-line diagram (one line stands for all conductors) |

Devices start in `pictorial`; switchgear (busbar, circuit breaker, disconnector, earthing switch) starts in
`single-line`. Every box type has a library preset in its other view (`<type>.sld` or `<type>.pictorial`), so a whole
diagram can be drawn in either language, and a selection can switch its view: ports keep their ids, docked cables
follow (`Attachments.Refresh`).

Symbols are drawn independently following common single-line diagram practice (generator circle with "G ~",
overlapping winding circles, converter square with a diagonal and the AC/DC signs, switch blades, load arrow). No
standard symbol tables or texts are reproduced and no compliance with IEC 60617 or any other standard is claimed.

## Relation to the electrical module

| Topic | Lives in | Note |
|---|---|---|
| Circuit symbols (resistor, capacitor, diode, transistor, fuse, contactor, coils), PCB | electrical | not duplicated |
| Power electronics **as circuit blocks** with U/V/W pins (`elec.pe.inverter`, `elec.pe.rectifier`, `elec.pe.dcdc`), battery cells (`elec.battery`), transformer as coils (`elec.transformer`), `elec.generator`, `elec.motor` | electrical | circuit level: pins per conductor |
| House installation (`elec.house.*`) and the simple LV distribution single-line elements `elec.sld.busbar`, `elec.sld.feeder`, `elec.sld.meter` | electrical | for a house distribution board drawn with electrical wires |
| Devices **as system elements** with ratings (kWp, kW, kWh, kVA, kV), pictorial view for slides, voltage-level aware single-line elements from 0.4 kV to 380 kV, sector coupling and hydrogen | **energy** | this module |

Rule of thumb: draw a circuit with the electrical module, an energy system or a grid single-line diagram with this
module. The two do not depend on each other; cables of either module dock on the ports of the other because ports
are generic `AnchorRole.Port` anchors. Overlap that remains on purpose: `energy.grid.busbar` and `energy.grid.meter`
add a pictorial view, voltage labels, level colours, a docking rail and the bidirectional meter to what
`elec.sld.busbar` / `elec.sld.meter` offer for house distribution.

## Conventions

- **Quick Sketch first:** every element is insertable without parameters; free (also non-uniform) scaling changes
  `Size`, never parameters, and every drawing stays inside its box. Circle symbols (generators, transformer windings,
  EV) keep their shape under non-uniform scaling; only their leads stretch.
- **Port convention:** the grid-side port sits on the **top edge**, the device side on the **bottom edge**, other media
  (heat, hydrogen, water, fuel, battery of a hybrid inverter) on the **left/right edges**. A diagram therefore reads
  top-down from the grid (classic single-line layout). Exceptions: the grid infeed has its point of connection at the
  bottom, the wind turbine its cable at the tower foot (bottom) in both views. Port ids are the same in both views.
- **Straight cables stay straight:** a view switch keeps the box centre (only the size changes), so every port on
  the top or bottom edge keeps its x offset from the centre and every side port its y offset in both views. Top and
  bottom ports sit at x = 0 (single-line symbols are centred, the label area is to the right) except the electric
  vehicle (charge port above the rear, x = −15 in both views) and the three-winding transformer (mv/lv at ±2.8).
  A vertical cable from a busbar tap to a device therefore stays one vertical segment when the whole diagram
  switches between pictorial and single-line (tested).
- **Labels:** a designation (`name`) and rating lines (e.g. `630 kVA`) are drawn inside the box in a reserved label
  area; `showLabel` hides them. Numbers use the UI language (decimal comma in German: `0,4 kV`) and readable SI
  prefixes (`430 Wp`, `2.58 kWp`, `40 MVA`, `2 MWh`). AC voltages are shown in kV, DC voltages below 1 kV in volts
  (`800 V DC`). Labels stay upright when the element is rotated.
- **Voltage levels and colours** (module drawing convention, not a standard): low voltage ≤ 1 kV blue `#1565c0`,
  medium voltage ≤ 52 kV green `#2e7d32`, high voltage ≤ 150 kV orange `#ef6c00`, extra-high voltage red `#c62828`,
  DC violet `#6a1b9a`. Cables, busbars and transformer windings use them while `levelColors` is on (default);
  off gives black lines for printed diagrams. The document's black-and-white setting also applies.
- **Energy carriers** (pipes, flow arrows; module convention): electricity amber, hydrogen teal (`H₂`, dash-dot in
  single-line), natural gas brown (`CH₄`, long dash), heat red, cooling blue (dash), water light blue (`H₂O`).

## Object types

Sizes are pictorial / single-line design sizes (W x H, mm on the page). Every box type also has `name` (designation)
and `showLabel` (true).

| Id | Name (EN / DE) | Sizes | Ports | Parameters |
|---|---|---|---|---|
| `energy.pv.module` | PV module / PV-Modul | 20x34 / 40x22 | dc | `power` (430 Wp) |
| `energy.pv.string` | PV string / PV-String | 50x26 / 40x22 | dc | `modules` (6, 2–24; more/fewer actions), `power` per module (430 Wp) |
| `energy.pv.array` | PV array / PV-Generator (Feld) | 50x34 / 40x22 | dc | `power` (10 kWp), `rows` (2, 1–6), `columns` (5, 1–12) |
| `energy.wind.turbine` | Wind turbine / Windenergieanlage | 30x44 / 38x24 | ac (bottom) | `power` (3 MW), `rotor` (0°; "turn rotor" action, animation frames) |
| `energy.gen.genset` | Generator set (genset) / Stromaggregat | 36x24 / 38x24 | ac | `power` (100 kVA), `fuel` diesel/gas/biogas/h2 |
| `energy.conv.inverter` | Inverter / Wechselrichter | 24x30 / 38x24 | ac, dc (+ bat when hybrid) | `power` (10 kW), `phases` 3/1, `kind` pv/hybrid/battery |
| `energy.conv.converter` | Power converter / Stromrichter | 24x30 / 38x24 | a, b | `power` (10 kW), `kind` acdc (rectifier)/dcdc/acac (frequency converter) |
| `energy.storage.battery` | Battery storage / Batteriespeicher | 24x30 / 36x24 | dc | `capacity` (10 kWh), `soc` (75 %, fill level), `form` wall/cabinet/container |
| `energy.heat.heatpump` | Heat pump / Wärmepumpe | 34x24 / 38x24 | el, heat | `power` (8 kW), `source` air/ground/water; HP/WP by UI language |
| `energy.heat.chp` | CHP unit / Blockheizkraftwerk (BHKW) | 40x24 / 38x28 | el, fuel, heat | `power` (50 kWel), `thermal` (80 kWth), `fuel` gas/biogas/h2 |
| `energy.heat.storage` | Thermal storage tank / Wärmespeicher | 24x32 / 28x26 | charge, discharge | `volume` (1000 l; m³ from 10 000 l) |
| `energy.h2.electrolyser` | Electrolyser / Elektrolyseur | 36x26 / 38x26 | el, water, h2 | `power` (1000 kW), `tech` pem/alkaline/soec |
| `energy.h2.fuelcell` | Fuel cell / Brennstoffzelle | 36x26 / 38x26 | el, h2, heat | `power` (100 kW) |
| `energy.h2.tank` | Hydrogen tank / Wasserstofftank | 36x22 / 34x24 | h2 | `mass` (500 kg), `pressure` (350 bar) |
| `energy.mobility.charger` | EV charger / Ladestation | 22x30 / 38x24 | ac, ev | `power` (11 kW), `form` wallbox/pillar, `dc` (false) |
| `energy.mobility.ev` | Electric vehicle / Elektrofahrzeug | 40x20 / 40x24 | charge | `battery` (60 kWh) |
| `energy.load.building` | Consumer (load) / Verbraucher (Last) | 28x30 / 30x24 | ac | `kind` house/apartment/commercial/industry, `power` (10 kW) |
| `energy.control.ems` | Energy management system / Energiemanagementsystem | 26x22 / 38x16 | data (bottom, dashed) | – |
| `energy.grid.infeed` | Utility grid (infeed) / Netzeinspeisung (öffentliches Netz) | 30x34 / 38x18 | poc (bottom) | `voltage` (20 kV); title Grid/Netz unless named |
| `energy.grid.transformer` | Transformer / Transformator | 30x34 / 38x26 | hv, lv | `hv` (20 kV), `lv` (0.4 kV), `power` (630 kVA), `vector` (vector group text, empty), `levelColors` |
| `energy.grid.transformer3` | Three-winding transformer / Dreiwicklungstransformator | 30x34 / 38x28 | hv, mv, lv | `hv` (110 kV), `mv` (20 kV), `lv` (10 kV), `power` (40 MVA), `vector`, `levelColors` |
| `energy.grid.substation` | Compact substation / Ortsnetzstation (Kompaktstation) | 34x30 / 40x30 | mv, lv | `hv` (MV side, 20 kV), `lv` (0.4 kV), `power` (630 kVA), `levelColors` |
| `energy.grid.meter` | Energy meter / Energiezähler | 20x28 / 30x24 | a, b | `bidirectional` (true; import/export arrows), `unit` (kWh) |
| `energy.grid.busbar` | Busbar / Sammelschiene | 60x10 / 60x8 | t1…tn on the bar + rail `bar0` | `voltage` (0.4 kV), `dc`, `taps` (5, 2–12), `levelColors`; the width is kept on a view switch |
| `energy.grid.breaker` | Circuit breaker / Leistungsschalter | 16x24 / 26x24 | a, b | `closed` (true), `symbol` contact (switch with breaker cross) / square (filled = closed) |
| `energy.grid.disconnector` | Disconnector / Trennschalter | 16x24 / 26x24 | a, b | `closed` (true), `kind` disconnector / loadbreak (switch-disconnector) / fuse (fuse switch-disconnector) |
| `energy.grid.earthing` | Earthing switch / Erdungsschalter | 16x24 / 26x24 | a | `closed` (false) |
| `energy.line.cable` | Cable / line / Kabel / Leitung | points | rails `rail0…` | `voltage` (0.4 kV), `dc`, `levelColors`, `kind` power/data (dashed), `conductors` (0–5 marks), `label`, `showVoltage`, `route` free/orthogonal |
| `energy.line.pipe` | Energy carrier pipe / Rohrleitung (Energieträger) | points | rails | `carrier` hydrogen/gas/heat/cooling/water, `label`, `showCode` |
| `energy.line.flow` | Energy flow arrow / Energiefluss-Pfeil | points | rails | `carrier` electricity/hydrogen/gas/heat/cooling/water, `weight` (band width 2.5 mm), `label` (e.g. "12 MWh/a") |

## Library

One item per type (id = type id), a preset of every box type in its other view (`<type>.sld` / `<type>.pictorial`),
and variants: `energy.pv.string.10`, `energy.pv.array.large` (100 kWp), `energy.pv.park` (10 MWp),
`energy.wind.offshore` (15 MW), `energy.gen.genset.gas`, `energy.conv.inverter.hybrid`, `energy.conv.inverter.battery`,
`energy.conv.inverter.central` (2.5 MW), `energy.conv.converter.dcdc`, `energy.conv.converter.acac`,
`energy.storage.battery.cabinet` (100 kWh), `energy.storage.battery.container` (2 MWh BESS),
`energy.heat.heatpump.ground`, `energy.heat.chp.biogas`, `energy.h2.electrolyser.alkaline` (10 MW),
`energy.mobility.charger.hpc` (150 kW DC pillar), `energy.load.apartment`, `energy.load.commercial`,
`energy.load.industry`, `energy.grid.infeed.lv` (house connection 0.4 kV), `energy.grid.infeed.hv` (110 kV),
`energy.grid.transformer.hvmv` (110/20 kV, 40 MVA), `energy.grid.transformer.ehv` (380/110 kV, 300 MVA),
`energy.grid.busbar.mv` / `.hv` / `.dc` (20 kV, 110 kV, 800 V DC), `energy.grid.breaker.square`,
`energy.grid.disconnector.loadbreak`, `energy.grid.disconnector.fuse`, cables `energy.line.cable.lv` / `.mv` / `.hv` /
`.ehv` / `.dc` / `.data`, pipes `energy.line.pipe.<carrier>` and flows `energy.line.flow.<carrier>`.

Search keywords cover both languages, e.g. Photovoltaik, Wechselrichter, Speicher, Ladestation, Wallbox,
Netzanschluss, Hausanschluss, Elektrolyseur, Brennstoffzelle, Wasserstoff, BHKW, Wärmepumpe, Trafo, Sammelschiene,
Leistungsschalter, Trennschalter, Einlinienschaltbild.

Categories: `energy.generation` Generation / Erzeugung, `energy.storage` Storage & conversion / Speicher & Umrichter,
`energy.sector` Sector coupling & hydrogen / Sektorkopplung & Wasserstoff, `energy.consumers` Consumers & e-mobility /
Verbraucher & E-Mobilität, `energy.grid` Grid connection & single-line / Netzanschluss & Einlinienschaltbild,
`energy.lines` Cables, pipes & flows / Kabel, Leitungen & Energieflüsse.

## Anchors and line language

- **Ports** (`AnchorRole.Port`) on the box edge carry the same id in both views (port ids depend only on parameters
  where the device changes: the hybrid inverter adds `bat`, the busbar has `t1…tn`). Every port is reached by a drawn
  lead, so a docked cable meets the drawing.
- **Rails** (`AnchorRole.Rail`): the busbar (`bar0`, along the whole bar — cables dock anywhere and draw a junction
  dot), and every drawn segment of cables, pipes and flow arrows (`rail0`, `rail1` …) for branches.
- **Line language:** cables, pipes and flows take corners (`InsertablePoints`), can be extended at both ends, and
  branches dock onto their rails (`Polylines.Branch`) with a junction dot. Orthogonal cable routing leaves a docked end
  perpendicular to the edge its port sits on.
- **Cable:** level colour (or black), 0.9 mm in the pictorial view, 0.5 mm in the single-line view; data lines thin and
  dashed; optional conductor marks (oblique strokes) and a label "designation · voltage".
- **Pipe:** a coloured tube with a highlight in the pictorial view; a thin line with the carrier's dash and code in the
  single-line view.
- **Flow arrow:** a band of `weight` with an arrow head (mitred corners) in the pictorial view; a thin line with a filled
  head in the single-line view.

**Build "+" (connections).** Every free port offers its line first (`ObjectType.Connection`, see the user guide): `dc`
and `bat` a DC cable, `ac`, `el`, `poc`, `hv`, `mv`, `lv` an AC cable at the voltage the element states for that port
(transformer windings, grid infeed), `data` a communication line, `heat`, `charge`/`discharge` (heat storage) a heat
pipe, `fuel` a gas pipe, `water` a water pipe, `h2` a hydrogen pipe; switchgear, meters and busbars a cable (DC for DC
busbars); converters DC or AC per side by their conversion; charger and vehicle DC for DC charging. The heat port of the
heat pump takes heat and cooling pipes. Then the elements with a port of the same medium (a PV string after a PV module,
an inverter or battery after a DC cable). Cables carry `electric.dc`, `electric.ac` or `data`, pipes their carrier; the
free end of a cable or pipe offers "+" to continue it or to dock the next element. Flow arrows carry no medium of their own.

## Context actions

Show rating label and Designation… (every box element), rated power / capacity / volume / voltage… inputs (accept
`10`, `10 kW`, `10,5`), Voltages… on transformers and the substation (`20/0.4`, `110/20/10`), cycle kind / form /
fuel / phases / source / technology / symbol, toggle switch state (breaker, disconnector, earthing switch), DC
charging, bidirectional meter, colour by voltage level, more/fewer modules, rows and busbar connection points,
turn rotor (wind turbine); cables: routing, voltage, show voltage, conductor marks, designation; pipes: carrier, code;
flows: carrier, wider/narrower band, label.

## Host notes

All output uses native primitives (lines, polylines, polygons, rectangles with rounded corners, ellipses, cubic and
quadratic paths, text), so every host draws it natively; no vector picture is needed. Text uses the Unicode subscripts
`₂` and `₄` (H₂, CH₄, H₂O) and the minus sign `−`; hosts need a font that contains them (the default Office and
LibreOffice fonts do). The wind turbine offers animation frames (rotor steps of 120°/n) through
`ObjectType.AnimationFrames`.

## Tests and gallery

`tests/InventaSketch.Modules.Energy.Tests` (77 tests): registration and id conventions, bilingual names, parameters,
options and actions, both views with their icons, insertion without parameters, library items resolve to valid
parameters and views, every library item rendered in every view in both languages inside its box (also stretched,
squashed, with every choice and the limits of every number), every parameter changes the drawing or the ports,
scaling changes `Size` not parameters, the views draw different symbols, view switch sizes (visual scale kept,
busbar width kept), labels in both languages and unit prefixes, level colours, switch states, style override, ports
with the same ids in both views on the box edge, reached by a lead and keeping their offset from the centre across
the view switch, busbar rail and taps, a cable docked on a port
following moves and view switches, a cable docked on the busbar with a junction dot, line language (corners, extend,
branches), orthogonal routing, conductor marks and labels, pipe codes, the flow band, number formatting and typed
voltages. The gallery test writes `gallery/*.html` (every type in both views, stretched and named, all presets in both
views, and two sample systems — a home energy system and an MV/LV single-line diagram — each in the pictorial and the
single-line view); further tests check that every cable end of the samples stays on its port in both views and
that every straight cable stays one vertical segment after switching the whole diagram.
