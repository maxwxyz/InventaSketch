# Energy systems module (`energy`)

PV, wind, hydropower, generator sets, inverters and converters, battery and pumped storage, e-mobility, sector coupling
(heat pump, CHP, thermal storage, hydrogen) and the grid connection with single-line diagram elements across voltage levels — for
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
| `energy.hydro.turbine` | Hydro power plant / Wasserkraftanlage | 40x30 / 38x24 | ac (top), water (headwater, left), tail (tailwater, right) | `power` (10 MW); pictorial: reservoir, dam, penstock, powerhouse with generator and turbine; single-line: generator over a turbine on one shaft |
| `energy.gen.genset` | Generator set (genset) / Stromaggregat | 36x24 / 38x24 | ac | `power` (100 kVA), `fuel` diesel/gas/biogas/h2 |
| `energy.conv.inverter` | Inverter / Wechselrichter | 24x30 / 38x24 | ac, dc (+ bat when hybrid) | `power` (10 kW), `phases` 3/1, `kind` pv/hybrid/battery |
| `energy.conv.converter` | Power converter / Stromrichter | 24x30 / 38x24 | a, b | `power` (10 kW), `kind` acdc (rectifier)/dcdc/acac (frequency converter) |
| `energy.storage.battery` | Battery storage / Batteriespeicher | 24x30 / 36x24 | dc | `capacity` (10 kWh), `soc` (75 %, fill level), `form` wall/cabinet/container |
| `energy.storage.pumped` | Pumped storage plant / Pumpspeicherkraftwerk | 44x30 / 38x26 | ac (top) | `power` (300 MW, generating), `capacity` (2400 MWh); upper and lower reservoir, reversible machine M/G, arrows for generating and pumping |
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
`energy.grid.disconnector.loadbreak`, `energy.grid.disconnector.fuse`, `energy.pv.array.yield` (10 kWp, annual yield),
`energy.storage.battery.home` (10 kWh at 50 %), `energy.grid.connection.point` (0.4 kV, feed-in limit and
self-consumption), cables `energy.line.cable.lv` / `.mv` / `.hv` /
`.ehv` / `.dc` / `.data`, pipes `energy.line.pipe.<carrier>` and flows `energy.line.flow.<carrier>`.

Search keywords cover both languages, e.g. Photovoltaik, Wechselrichter, Speicher, Ladestation, Wallbox,
Netzanschluss, Hausanschluss, Elektrolyseur, Brennstoffzelle, Wasserstoff, BHKW, Wärmepumpe, Wasserkraft,
Pumpspeicher, Trafo, Sammelschiene, Leistungsschalter, Trennschalter, Einlinienschaltbild.

Solar thermal collectors are not duplicated here: building services offers `bs.heat.solarthermal`, and its heating pipes
dock on the energy heat ports (see "Port flows").

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
pipe, `fuel` a gas pipe, `water` and `tail` (hydro power plant) a water pipe, `h2` a hydrogen pipe; switchgear, meters and busbars a cable (DC for DC
busbars); converters DC or AC per side by their conversion; charger and vehicle DC for DC charging. The heat port of the
heat pump takes heat and cooling pipes. Then the elements with a port of the same medium (a PV string after a PV module,
an inverter or battery after a DC cable). Cables carry `electric.dc`, `electric.ac` or `data`, pipes their carrier; the
free end of a cable or pipe offers "+" to continue it or to dock the next element. Flow arrows carry no medium of their own.

## Context actions

Show rating label and Designation… (every box element), rated power / capacity / volume / voltage… inputs (accept
`10`, `10 kW`, `10,5`), Voltages… on transformers and the substation (`20/0.4`, `110/20/10`), kind / form /
fuel / phases / source / technology / symbol / carrier / routing as choices ▾, toggle switch state (breaker, disconnector, earthing switch), DC
charging, bidirectional meter, colour by voltage level, more/fewer modules, rows and busbar connection points,
turn rotor (wind turbine); cables: routing, voltage, show voltage, conductor marks, designation; pipes: carrier, code;
flows: carrier, wider/narrower band, label.

## Engineering values (Calc 4)

Values are optional: every element still inserts and scales without them. They are entered and shown with the Values
card (*Edit ▸ Values ▸*, the *Values* button in Explore and Engineer) and stored in optional text parameters
(engineering section of the inspector). The existing rating parameters are read as values in their stored units:
module and string `power` (Wp), array `power` (kWp), inverter and charger `power` (kW), battery `capacity` (kWh), heat
pump `power` (kW), CHP `power` and `thermal` (kW), thermal storage `volume` (l), electrolyser and fuel cell `power` (kW),
pumped storage `power` (MW) and `capacity` (MWh), busbar `voltage` (kV), transformer `power` (kVA), `hv` and `lv` (kV), string `modules`. Because
these are stored when an element is inserted, a string shows its STC power and a transformer its rated currents right
away. Card variables come first. Design notes:
[engineering-calculations.md, section 13](../design/engineering-calculations.md#13-implementation-notes-calc-4-fluid-process-building-services-energy-2026-11-25).

| Element | Variables (parameter) | Local relations [source] |
|---|---|---|
| PV module | *U*_oc,max (`vocMax`), *P*_mpp (`power`), *U*_oc (`voc`), *U*_mpp (`vmp`); panel *I*_sc (`isc`), *I*_mpp (`imp`), β (`betaVoc`, % per K), θ_min (`tmin`), θ_max (`tmax`), *U*_mpp,min (`vmpMin`) | *P*_mpp = *U*_mpp·*I*_mpp (2 % tolerance for rounded datasheet values); *U*_oc,max = *U*_oc·(1 + β·(θ_min − 25 °C)); *U*_mpp,min = *U*_mpp·(1 + β·(θ_max − 25 °C)) (β of *U*_oc as an approximation) [S33] |
| PV string | *U*_oc,max, *U*_mpp,S (`vmpString`), *I*_sc, *P* (`powerString`); panel *N*_s (`modules`), *P*_mpp (`power`), *U*_oc, *U*_mpp, *I*_mpp, β, θ_min, θ_max, *U*_mpp,min, yield values as on the array | *P* = *N*_s·*P*_mpp; *U*_mpp,S = *N*_s·*U*_mpp [S34]; *U*_oc,max = *N*_s·*U*_oc·(1 + β·(θ_min − 25 °C)); *U*_mpp,min likewise with θ_max [S33]; yield as on the array |
| PV array | *P* (`power`), *N*_p (`strings`), *I*_sc (`iscArray`), *U*_oc,max; panel *P*_S (`powerString`), *I*_sc,S (`isc`), *U*_mpp,min, *E*_a (`annualYield`, annual yield), *Y*_f (`specificYield`, kWh/kWp per year shown as full-load hours h), *H*_POA (`irradiation`, annual in-plane irradiation as peak-sun hours: kWh/m² ÷ 1 kW/m², entered in h), PR (`performanceRatio`, default 80 %) | *P* = *N*_p·*P*_S; *I*_sc = *N*_p·*I*_sc,S [S34]; *Y*_f = *H*_POA·PR; *E*_a = *P*·*Y*_f [S33] (PR as in IEC 61724-1) |
| Inverter | *P*_DC (`powerDc`), *P*_AC (`powerAc`), *I*_AC (`currentAc`, per phase), *U*_oc,max (from the strings); panel η (`efficiency`, default 97 %), *R* (`dcAcRatio`), *P*_AC,N (`power`), *U*_AC (`voltageAc`, line-to-line, default 400 V), cos φ (`powerFactor`, default 1), *U*_mpp,min, *I*_sc (`iscDc`), *U*_mpp (`vmpDc`), *U*_DC,max (`udcMax`), *U*_MPPT,min/max (`mpptMin`, `mpptMax`), *I*_DC,max (`idcMax`) | *P*_AC = η·*P*_DC [S33]; *R* = *P*_DC/*P*_AC,N [S34]; three-phase *I*_AC = *P*_AC/(√3·*U*_AC·cos φ), single-phase *I*_AC = *P*_AC/((*U*_AC/√3)·cos φ) [S7] |
| Grid busbar, grid connection meter (boards) | *P* (`powerIn`, sum of the feeders), *I* (`current`, per phase), *U* (busbar `voltage` in kV, meter `systemVoltage`); panel cos φ (`powerFactor`, default 1) | *I* = *P*/(√3·*U*·cos φ); DC busbar *I* = *P*/*U* [S7] |
| Battery storage | *E*_nutz (`capacity`), *P* (`powerBat`), *C* (`crate`), *t* (`runtime`); panel *U* (`voltage`), *Q* (`ah`), *E* (`energyNominal`), DoD (`dod`), *I* (`current`), *C*_max (`cmax`), SoC (`soc`, the drawn fill level), *E*_st (`energyStored`, stored now), *t*_rest (`timeLeft`), η_rt (`roundTrip`, round-trip efficiency), *E*_in (`energyCharge`, energy drawn to charge fully from empty) | *E* = *U*·*Q*; *E*_nutz = *E*·DoD; *P* = *U*·*I*; *I* = *C*·*Q*/1 h; *t* = *E*_nutz/*P*; *E*_st = SoC·*E*_nutz; *t*_rest = *E*_st/*P*; *E*_nutz = η_rt·*E*_in [S35] |
| EV charger | *P* (`power`), *U* (`voltage`, default 400 V), *I* (`current`), *t* (`chargeTime`); panel cos φ, *E* (`chargeEnergy`, energy to charge) | AC: *P* = √3·*U*·*I*·cos φ; DC charging: *P* = *U*·*I* [S7]; *t* = *E*/*P* (constant power, no losses) [S35] |
| Heat pump | Φ_H (`power`), *P*_el (`powerEl`), COP (`cop`); panel θ_Q (`sourceTemp`), heating water: θ_F (`flowTemp`), θ_R (`returnTemp`), V̇ (`flowRate`); engineer ρ (`density`, 1000 kg/m³), *c* (`specificHeat`, 4.19 kJ/(kg·K)) | Φ_H = COP·*P*_el [S32]; Φ_H = ρ·*c*·V̇·(θ_F − θ_R) [S29] |
| CHP unit | *P*_el (`power`), Φ_th (`thermal`), *P*_F (`powerFuel`, fuel power, lower heating value), η_el (`efficiencyEl`); panel η_th (`efficiencyTh`), heating water as on the heat pump | *P*_el = η_el·*P*_F; Φ_th = η_th·*P*_F [S38]; Φ_th = ρ·*c*·V̇·(θ_F − θ_R) [S29] |
| Thermal storage | *Q* (`heat`, stored heat), *V* (`volume`), Δθ (`deltaT`, spread), θ_S (`temperature`, top); engineer ρ, *c* | *Q* = ρ·*V*·*c*·Δθ [S29] |
| Electrolyser | *P*_el (`power`), *P*_H2 (`powerH2`, lower heating value), η (`efficiency`), ṁ_H2 (`hydrogenFlow`) | *P*_H2 = η·*P*_el [S38]; *P*_H2 = ṁ_H2·*H*_i with *H*_i = 33.3 kWh/kg (120 MJ/kg) [S29] |
| Fuel cell | *P*_el (`power`), *P*_H2 (`powerH2`), η_el (`efficiency`), Φ_th (`heatOutput`); panel η_th (`efficiencyTh`), ṁ_H2 (`hydrogenFlow`), heating water as on the heat pump | *P*_el = η_el·*P*_H2; Φ_th = η_th·*P*_H2 [S38]; *P*_H2 = ṁ_H2·*H*_i [S29]; Φ_th = ρ·*c*·V̇·(θ_F − θ_R) [S29] |
| Wind turbine | *P* (`powerWind`), *D* (`diameter`), *v* (`windSpeed`), *c*_p (`cp`); engineer ρ (`airDensity`, default 1.225 kg/m³) | *P* = ½·ρ·π·*D*²/4·*v*³·*c*_p [S36] |
| Hydro power plant | *P* (`powerHydro`, power from the water; the rated `power` stays a label), *Q* (`flow`), *H* (`head`, net head), η (`efficiency`, overall); engineer ρ (`waterDensity`, 1000 kg/m³) | *P* = ρ·*g*·*Q*·*H*·η, *g* = 9.80665 m/s² [S27] |
| Pumped storage plant | *E*_el (`capacity`), *P* (`power`, turbine), *V* (`reservoirVolume`, usable volume of the upper reservoir), *H* (`head`); panel *E*_pot (`energyPotential`), η_T (`efficiency`, generating, overall), *Q* (`flow`), *t* (`generatingTime`); engineer ρ | *E*_pot = ρ·*g*·*V*·*H* [S27]; *E*_el = η_T·*E*_pot [S38]; *P* = ρ·*g*·*Q*·*H*·η_T [S27]; *t* = *E*_el/*P* [S1] |
| Power transformer | *S*_r (`power`), *I*_HV (`currentHv`), *I*_LV (`currentLv`); panel *U*_HV (`hv`), *U*_LV (`lv`) | *I* = *S*_r/(√3·*U*) per side [S7] |
| Utility grid (grid connection point) | *P*_gen (`powerFeed`, AC power of the inverters on the net of `poc`), *P*_lim (`feedLimit`, the grid operator's feed-in limit), *r*_SC (`selfConsumption`), *r*_SS (`autarky`); panel per year: *E*_PV (`energyPv`), *E*_L (`energyLoad`), *E*_SC (`energySelf`, self-consumed), *E*_feed (`energyFeed`), *E*_grid (`energyGrid`) | *r*_SC = *E*_SC/*E*_PV; *r*_SS = *E*_SC/*E*_L; *E*_feed = *E*_PV − *E*_SC; *E*_grid = *E*_L − *E*_SC [S34] |

**Port flows.** Cables join ports into nets (a busbar joins all its taps and its bar). DC side: the string voltages
*U*_oc,max, *U*_mpp,min and *U*_mpp are shared by the whole net (parallel strings have to agree, otherwise the card shows a
conflict); the short-circuit current and the STC power of parallel strings add up at the inverter. AC side: the system
voltage is shared; the AC power of the inverters adds up at a busbar or at the device side (port b) of a meter, and the
meter passes it on at its grid side (port a). The busbar's 0.4 kV therefore becomes the inverter's *U*_AC.

**Heat and hydrogen.** Heat pipes (`energy.line.pipe` with carrier heat) and hydrogen pipes are net lines too. A heat
port (`heat` of heat pump, CHP and fuel cell) gives the heating water's volume flow V̇ and its flow temperature θ_F;
the thermal storage gives its top temperature θ_S at `charge` and `discharge` (charged and discharged at the top of a
stratified tank). A hydrogen port (`h2` of electrolyser and fuel cell) gives *P*_H2 and ṁ_H2. These are through
quantities: they are shared by the two elements of an unbranched run. Gas, cooling and water pipes carry no values.

**Joins with other modules** (engineering-calculations.md, section 6.1). The energy nets state their physical keys
explicitly (`NetSpec.Keys`): `Uac`, `Ustr` → `U`; `Pac`, `Pdc` → `P`; `Vdot` → `Vdot`; `theta` → `theta`; `m` → `m`;
limits (*U*_oc,max, *U*_mpp,min, *I*_sc) and *P*_H2 stay inside the module. So an energy heat pump connected by a
building-services heating pipe (`bs.pipe.flow`) or by an energy heat pipe to a radiator passes V̇ and θ_F to it, and a
radiator's entered values flow back into the heat pump (θ_R from its heating power). The energy heat pump and the
building-services heat pump stay two types (system view vs. building installation) but name the same variables
(Φ, *P*_el, COP, θ_F, θ_R, θ_Q, V̇, ρ, *c*), so their values meet at a junction; a building heat pump charging an energy
thermal storage passes its flow temperature. The water ports `water` and `tail` of the hydro power plant give its flow
*Q* to water lines of other modules. Energy heat ports are single (single-line view): a flow and a return pipe docked
on the same port are joined into one building-services net, so draw only the flow pipe to an energy heat port.
The process module's turbine-driven generator (`process.generator`) has a power terminal `el` that takes an energy cable
and names its electrical output *P*_el for the key `P`: a cable from it to a meter (port a), a busbar or another single
AC element passes the turbine's electrical output on (unbranched run, like an inverter's *P*_AC; a branched sum is not
split across modules). The energy module itself has no gas or steam turbine: thermodynamic turbines and compressors with
gas values are process symbols (process.md, "Gas machines"); the CHP unit and the genset keep their overall ratings.

Examples (accept criteria): 10 modules with *U*_oc = 49.5 V, β = −0.27 %/K and θ_min = −10 °C give
*U*_oc,max = 10 · 49.5 V · 1.0945 = 541.8 V, and an inverter with *U*_DC,max = 500 V shows the warning `pv.voc.max`.
10 × 400 Wp into an inverter with η = 96 % on a 0.4 kV busbar: *P*_AC = 3.84 kW and *I* = 5.54 A per phase (16.6 A for a
single-phase inverter).

**Checks** (hints only, overlay, nothing changes):

| Id | Rule | Severity | Source |
|---|---|---|---|
| `pv.voc.max` | *N*_s·*U*_oc(θ_min) of the connected strings > *U*_DC,max of the inverter | warning | [S33] (rule named in IEC 62548-1) |
| `pv.mpp.min` | *N*_s·*U*_mpp(θ_max) < *U*_MPPT,min | hint | [S33, S34] |
| `pv.isc.max` | *N*_p·*I*_sc > *I*_DC,max | warning | [S33] |
| `pv.dcac` | DC/AC ratio outside 0.8–1.5 | info | [S34] |
| `bat.crate` | *C* > *C*_max | hint | [S35] |
| `heat.cop.carnot` | COP > *T*_F/(*T*_F − *T*_Q) in kelvin (impossible) | warning | [S29] |
| `wind.betz` | *c*_p > 16/27 (Betz limit, impossible) | warning | [S36] |
| `energy.feedin.limit` | *P*_gen of the inverters at the grid connection point > *P*_lim (feed-in must be curtailed without own consumption) | hint | – (grid operator's limit) |
| `energy.balance` | self-consumed energy > PV generation or consumption (impossible) | warning | [S1] |

**Storage, yield and energy balance (2026-11-26).** The battery's state of charge is its drawn fill level (`soc`, also
set with *State of charge…*), so a home battery of 10 kWh at 50 % shows 5 kWh stored and, with 2.5 kW, 2 h left; a
round-trip efficiency of 90 % gives 11.1 kWh drawn to charge it from empty. PV strings and arrays estimate the annual
yield: the in-plane irradiation is entered as peak-sun hours (1100 kWh/m² per year → `1100 h`; Core has no kWh/m² unit
yet), PR defaults to 80 %, so 10 kWp give *Y*_f = 880 h (880 kWh/kWp) and *E*_a = 8800 kWh. The utility grid element
doubles as the grid connection point: its port `poc` adds the AC power of the inverters on its net (as a busbar does;
an unbranched run passes one inverter's *P*_AC), the feed-in limit is compared with it, and the annual energies give the
self-consumption share and the self-sufficiency (9000 kWh generated, 5000 kWh consumed, 3000 kWh self-consumed: 33 % and
60 %, 6000 kWh fed in, 2000 kWh drawn). The energies are entered per year; they are not taken from the PV arrays
across the inverter (the inverter separates the DC and AC nets). Library items: `energy.storage.battery.home`,
`energy.pv.array.yield`, `energy.grid.connection.point`. The thermal storage already had its values (*Q* = ρ·*V*·*c*·Δθ).

## Host notes

All output uses native primitives (lines, polylines, polygons, rectangles with rounded corners, ellipses, cubic and
quadratic paths, text), so every host draws it natively; no vector picture is needed. Text uses the Unicode subscripts
`₂` and `₄` (H₂, CH₄, H₂O) and the minus sign `−`; hosts need a font that contains them (the default Office and
LibreOffice fonts do). The wind turbine offers animation frames (rotor steps of 120°/n) through
`ObjectType.AnimationFrames`.

## Tests and gallery

`tests/InventaSketch.Modules.Energy.Tests` (114 tests): registration and id conventions, bilingual names, parameters,
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
that every straight cable stays one vertical segment after switching the whole diagram. `HeatHydroValueTests` check
the values of hydropower, pumped storage, electrolyser, fuel cell, CHP, heat pump, thermal storage and the charging
time, the heat and hydrogen pipes as net lines and the explicit keys; `tests/InventaSketch.Engine.Tests/EnergyHeatJoinTests.cs`
checks the joins with building services in the default catalogue.
