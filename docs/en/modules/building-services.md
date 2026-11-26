# Building services module (`building-services`)

Heating, plumbing, ventilation, cooling (split air conditioner) and electrical house installation (German: *Technische Gebäudeausrüstung, TGA*)
for slides and quick technical sketches. Every element has the **same three views** of one installation:

| View id | Label (EN / DE) | Icon | What it shows |
|---|---|---|---|
| `plan` | Plan / Grundriss | `IconIds.Plan` | top view as in a floor plan; the wall is at the **top edge** of the box |
| `section` | Section / elevation / Schnitt / Ansicht | `IconIds.Front` | the element seen from the front at its mounting height |
| `schematic` | Schematic (riser) / Schema (Strang) | `IconIds.Schematic` | riser / flow diagram symbol |

The module is **not BIM**: there is no building model behind the drawing, only well-drawn, editable symbols that
keep their connections and their place when the installation switches its view. Symbols are drawn independently
following common drawing practice; no standard tables or texts are reproduced and no compliance is claimed.

## Split with the architecture and electrical modules

| Topic | Lives in | Note |
|---|---|---|
| Walls, rooms, doors, windows, furniture | architecture | unchanged |
| `arch.hvac.radiator/heatpump/boiler`, `arch.sanitary.*`, `arch.hvac.pipe` | architecture | simple **plan-only** furnishing symbols of a floor plan; they stay for floor plans without installation |
| Radiator, boiler, heat pump, WC, washbasin, shower, bathtub, pipes **as installation elements** | building services | three views, mounting height, ports with the same ids in every view; plan drawings and pipe colours are aligned with architecture |
| Socket, light switch, luminaire, junction box, distribution board, cable route (`elec.house.*`), single-line diagram | electrical | **not duplicated** here |
| Communication outlet, detector, appliance outlet, equipotential bonding bar | building services | missing in the electrical module so far |

One element, one item: the switchgear module's room devices give way to these canonical items while both are registered
(`LibraryItem.Alternative`): socket, switch and light to `elec.house.*`, and the cooker / washing machine connections
(`sw.house.appliance`, `.washer`) to `bs.elec.appliance`, `bs.elec.appliance.washer`, which therefore also answer to
"cooker connection", "Herdanschluss", "washing machine connection" and "Waschmaschinenanschluss". Like the room devices
the appliance outlet carries `room` (**Room …**), `feed` (**Circuit …**, the feeding protective device, e.g. "-F3") and
`phase`, so the switchgear installation list, circuit plan and the operating current I_B of its breaker include it. The air handling
sections of the process module (`process.hvac.*`, named "… (AHU)") are not merged with the duct parts here: they are
sections of an AHU schematic, not parts of a duct run in a building plan.

Requested follow-up (electrical module owner): give `elec.house.socket/switch/light/junction/board/cable` the views
`section` and `schematic` and a `height` parameter with the conventions below, so electrical installations switch with
the rest of the house.

## Conventions

- **Quick Sketch first:** every element is insertable without parameters; free (also non-uniform) scaling changes
  `Size`, never parameters. Every drawing stays inside its box.
- **Plan scale:** sizes are authored for **1:100** like the architecture module. Every type uses the plan scale
  (`UsesPlanScale`): the optional advanced parameter `scale`, else the document plan scale, else 1:100.
- **Mounting height** `height` (mm, every type): the **bottom edge above the finished floor** (negative = in or below
  the floor). Defaults: radiator 150, washbasin 500 (rim 850), boiler 1100, ventilation unit 1000, outlets 300,
  detector 2440, air terminal 2450, floor-standing elements 0, underfloor heating −70, floor drain −150, pipes 0,
  ducts 2400. The context action "Mounting height…" accepts `850`, `85 cm`, `0,85 m`, `-50`.
- **To-scale elements vs. symbols:** equipment (radiator, boiler, sanitary, cylinder, units) is drawn to scale; inline
  fittings, outlets, detectors, air terminals and the floor drain are symbols of a readable size.
- **View switch of one element:** the box **width is kept** (plan and elevations share the x extent). To-scale
  elements take the real height of the target view at the object's plan scale (radiator 600 mm in section =
  6 mm at 1:100, 12 mm at 1:50); symbols keep their visual scale.

## Plan ↔ section ↔ schematic mapping (for Core)

`ServiceViews.Switch(registry, doc, ids, view, floorY?)` implements the mapping as a tested reference; it only relies on
generic data (views `plan`/`section`/`schematic`, parameter `height`, plan scale, anchors), so it can move into Core
next to `AssemblyViews` unchanged.

1. **Viewing direction.** The section looks at the plan from its lower page edge (towards −y), like the wall elevation
   of the architecture module. **x is shared**: every element keeps its centre x and its width in every view.
2. **Floor line.** `floorY` = the lowest plan edge of the selection (largest page y), unless given. With walls in the
   selection it is their ground line instead (the lowest point of the wall centre lines, `ObjectType.ElevationGround`),
   so the installation stands on the floor the wall elevation draws. It is stored on each element
   (`Meta["bs.floorY"]`) so later switches use the same line.
3. **plan → section/schematic (boxes):** plan y and rotation are stored (`Meta["bs.planY"]`, `Meta["bs.planRotation"]`),
   rotation becomes 0, the size adapts (width kept), and the bottom edge goes to
   `bottomY = floorY − height / scale`.
4. **section ↔ schematic:** x and the bottom edge stay; only the symbol and its box height change. The schematic is a
   developed riser diagram: the same horizontal order, vertical position by mounting height.
5. **section/schematic → plan:** the element gets the mounting height it now has (`height = (floorY − bottomY) × scale`,
   so dragging it up in the section raises it), its stored plan y and rotation come back, x stays (dragging it sideways
   in the section moves it in plan too).
6. **Lines** (pipes, ducts): plan → elevation stores the plan points (`Meta["bs.planPoints"]`) and puts every point at
   `(x, floorY − height / scale)`; vertical runs collapse (zero-length segments are skipped when drawing). Back in plan
   the points are restored and `height` follows the mean y of the free points. Ends docked on ports follow their
   element (`Attachments.Refresh`, port ids are the same in every view). An end docked on a port that the target lacks
   in the new view (the plan port `riser` of a riser) slides onto the target's **rail** at the line's height and keeps
   its port id for the way back.
7. Elements on side walls are shown in front view at their x position (developed elevation); a true per-wall section
   would need a section line object and is out of scope.
8. **Walls, doors and windows switch along** (2026-11-25): `section` and the architecture's `elevation` are aliases
   (`ViewAliases` in Core). With walls and installations selected, the multi-selection offers "All in view: Section";
   the installations go to `section`, the walls, doors and windows to `elevation`, and back to plan together. Wall
   points do not move; the wall keeps its own `height` (wall height) – the installation rule reads `height` (mounting
   height) only from installation types, so no separate key is needed.

## Object types

Sizes are plan / section / schematic at 1:100 (W x H, mm on the page).

| Id | Name (EN / DE) | Sizes | Mount | Ports (same ids in every view) | Parameters |
|---|---|---|---|---|---|
| `bs.pipe` | Pipe / Rohrleitung | points | – | rails along every segment | `medium` flow/return/cold/hot/circulation/waste, `height`, `dn`, `arrow`, `hops` |
| `bs.duct` | Air duct / Luftleitung | points | – | rails | `kind` supply/extract/outdoor/exhaust, `width` (160 mm), `height`, `arrow` |
| `bs.heat.boiler` | Boiler (wall-hung) / Heizkessel / Therme | 4.4x3.5 / 4.4x7 / 4.4x4.4 | wall | flow, return | – |
| `bs.heat.heatpump` | Heat pump (outdoor unit) / Wärmepumpe (Außeneinheit) | 10x4 / 10x8 / 10x5 | floor | flow, return | label HP/WP by UI language |
| `bs.heat.radiator` | Radiator / Heizkörper | 10x1.2 / 10x6 / 10x4 | wall | flow, return | – |
| `bs.heat.floorloop` | Underfloor heating loop / Fußbodenheizung (Heizkreis) | 30x30 / 30x0.6 / 30x5 | in floor | flow, return | `spacing` (150 mm, drawn at the plan scale) |
| `bs.heat.manifold` | Heating circuit manifold / Heizkreisverteiler | 6x1.2 / 6x4 / 6x3 | wall | flow, return, f1…fn, r1…rn | `circuits` (4, 1–12; more/fewer actions) |
| `bs.heat.pump` | Circulator pump / Umwälzpumpe | 3x3 (symbol) | inline | in, out | – |
| `bs.heat.mixer` | Mixing valve (3-way) / Mischventil (3-Wege) | 4x4 (symbol) | inline | a, ab, b | – |
| `bs.heat.expansion` | Expansion vessel / Ausdehnungsgefäß | 3.6x3.6 / 3.6x5 / 3.6x4.4 | wall | pipe | – |
| `bs.heat.trv` | Thermostatic valve / Thermostatventil | 3x3 (symbol) | inline | in, out | – |
| `bs.valve.ball` | Ball valve (heating) / Kugelhahn (Heizung) | 3x3 (symbol) | inline | in, out | `service` heating/water |
| `bs.valve.check` | Check valve (heating) / Rückschlagventil (Heizung) | 3x3 (symbol) | inline | in, out (flow from in to out, outlet triangle filled) | `service` heating/water |
| `bs.valve.safety` | Safety valve (heating) / Sicherheitsventil (Heizung) | 3x3 (symbol) | inline | in (below), waste (blow-off, right) | `service` heating/water |
| `bs.strainer` | Strainer (heating) / Schmutzfänger (Heizung) | 3x3 (symbol) | inline | in, out | `service` heating/water |
| `bs.meter.heat` | Heat meter / Wärmemengenzähler | 4x3 (symbol) | inline | in, out | label HM/WMZ |
| `bs.meter.water` | Water meter / Wasserzähler | 3x3 (symbol) | inline | in, out (drinking water) | label WM/WZ |
| `bs.heat.buffer` | Buffer tank / Pufferspeicher | 8x8 / 8x18 / 8x12 | floor | f1, r1 (generator side, left), f2, r2 (consumer side, right) | label BUF/PS |
| `bs.heat.solarthermal` | Solar thermal collector / Solarkollektor (Solarthermie) | 20x8 / 20x9 / 20x7 | floor | flow, return | – |
| `bs.san.wc` | WC / WC | 3.6x5.4 / 3.6x4 / 3.6x3.6 | floor | waste, cold | – |
| `bs.san.basin` | Washbasin / Waschbecken | 6x4.5 / 6x4 / 6x3.5 | wall | waste, hot, cold | – |
| `bs.san.shower` | Shower / Dusche | 9x9 / 9x22 / 9x8 | floor | waste, hot, cold | – |
| `bs.san.bathtub` | Bathtub / Badewanne | 17x7.5 / 17x6 / 17x4 | floor | waste, hot, cold | – |
| `bs.san.heater` | Water heater (storage cylinder) / Warmwasserspeicher | 6x6 / 6x16 / 6x10 | floor | hot, circulation, cold (+ flow, return with coil) | `coil` (true) |
| `bs.san.drain` | Floor drain / Bodenablauf | 2x2 / 2x1.5 / 2x2 (symbol) | in floor | waste | – |
| `bs.san.riser` | Riser (vertical pipe) / Steigleitung / Fallleitung | 2x2 / 2x28 / 2x28 | floor | plan: riser; section/schematic: bottom, top + rail | `medium`, `dir` up/down/through |
| `bs.vent.terminal` | Air terminal (valve / grille) / Luftdurchlass (Ventil / Gitter) | 3x3 / 3x1.6 / 3x3 (symbol) | ceiling | duct | `kind` supply/extract |
| `bs.vent.unit` | Ventilation unit with heat recovery / Lüftungsgerät mit Wärmerückgewinnung | 7x4.5 / 7x8 / 7x5 | wall | outdoor, exhaust, extract, supply | – |
| `bs.vent.damper` | Duct damper / Lüftungsklappe | 4x2.4 / 4x2.4 / 4x3 (symbol) | inline | inlet, outlet (on the duct axis) | `kind` supply/extract/outdoor/exhaust (colour) |
| `bs.vent.firedamper` | Fire damper / Brandschutzklappe | 4x2.4 / 4x2.4 / 4x4 (symbol) | inline | inlet, outlet | `kind`; label FD/BSK |
| `bs.vent.silencer` | Duct silencer (sound attenuator) / Schalldämpfer (Lüftung) | 6x2.4 / 6x2.4 / 6x3 (symbol) | inline | inlet, outlet | `kind` |
| `bs.cool.split` | Split air conditioner / Split-Klimagerät | 18x8 / 18x22 / 18x8 | floor | waste (condensate of the indoor unit), cable (outdoor unit) | – |
| `bs.elec.data` | Communication outlet / Kommunikationsanschluss | 4x4 (symbol) | wall | cable | `kind` data/phone/tv |
| `bs.elec.detector` | Detector / Melder | 4x4 / 4x1.4 / 4x4 (symbol) | ceiling | cable | `kind` smoke/heat/motion |
| `bs.elec.appliance` | Appliance outlet / Geräteanschlussdose | 4x4 (symbol) | wall | cable | `kind` cooker/washer/dryer/dishwasher/generic; `room`, `feed`, `phase` (from the circuit / L1–L3 / three-phase); load values `power`, `current`, `voltage`, `powerFactor` |
| `bs.elec.bonding` | Equipotential bonding bar / Potentialausgleichsschiene | 6x2 / 6x2 / 6x3 (symbol) | wall | earth + rail along the bar | – |

Every box type also has `height` and the advanced `scale`. Labels in drawings follow the UI language (HP/WP, DHW/WW,
codes F/VL, R/RL, CW/KW, HW/WW, HWC/Z, W/SW, SUP/ZUL, ETA/ABL, ODA/AUL, EHA/FOL, LAN/TEL/TAE/TV/SAT, S/RM, H/WM,
PIR/BM, CK/HE, HM/WMZ, WM/WZ, BUF/PS, FD/BSK, …). The 3-way mixing valve and the expansion vessel already exist as
`bs.heat.mixer` and `bs.heat.expansion`. The split air conditioner is one element: the wall-mounted indoor unit (with a
snowflake in the schematic) and the outdoor unit (fan) joined by the refrigerant lines drawn inside the element; plan:
indoor unit on the room side (bottom), outdoor unit outside (top right), so the element is placed across the wall.
Pipe colours follow the architecture module (flow red, return blue dashed, cold green, hot
magenta dash-dot, waste brown, wider); circulation is violet dash-dot-dot. Air colours: supply blue, extract ochre,
outdoor green, exhaust brown. Colours and codes are common practice, not a cited standard.

## Library

One item per type (id = type id), a `<type>.section` and `<type>.schematic` preset of every box type (so the insert
box can offer elements in the selection's view), pipes per medium (`bs.pipe.flow` … `bs.pipe.waste`; flow/return in
the heating category), ducts per air kind (`bs.duct.supply` …), and variants: `bs.heat.radiator.long`,
`bs.heat.manifold.8`, `bs.heat.floorloop.small`, `bs.san.riser.waste` (soil stack), `bs.san.riser.hot`,
`bs.san.riser.flow`, `bs.san.riser.return`, `bs.san.heater.electric`, `bs.vent.terminal.extract`,
`bs.elec.data.phone`, `bs.elec.data.tv`, `bs.elec.detector.heat`, `bs.elec.detector.motion`,
`bs.elec.appliance.washer`, `bs.elec.appliance.dishwasher`, and the drinking water fittings (plumbing category,
`service` = water) `bs.valve.ball.water`, `bs.valve.check.water` (Rückflussverhinderer), `bs.strainer.water` (filter).

Categories: `bs.heating` Heating / Heizung, `bs.plumbing` Plumbing & sanitary / Sanitär & Trinkwasser,
`bs.ventilation` Ventilation / Lüftung, `bs.cooling` Cooling & air conditioning / Kälte & Klima, `bs.electrical` Electrical installation (additions) / Elektroinstallation
(Ergänzungen).

## Anchors and line language

- **Ports** (`AnchorRole.Port`) carry the same id in every view (exception: the riser, see above); pipes, ducts and
  cables dock there and follow the element.
- **Mounts** (`AnchorRole.Mount`): in plan `wall` on the top edge of wall-backed elements (`floor`/`ceiling` at the
  centre otherwise); in section and schematic the height reference — `floor` or `wall` at the bottom edge centre,
  `ceiling` at the top edge centre. Inline fittings have no mount.
- **Rails** (`AnchorRole.Rail`): every segment of pipes and ducts (`rail0`, `rail1` …), the riser axis in section and
  schematic, the bonding bar.
- **Line language:** pipes and ducts take corners (`InsertablePoints`), can be extended at both ends, and branches
  dock onto their rails (`Polylines.Branch`); pipe branches draw a junction dot. Pipes hop over earlier pipes
  (`HopsAtCrossings`, switchable with `hops`); ducts do not hop (double lines).
- Ducts: double line of `width / scale` with mitred corners, end lines and a dash-dot axis in plan and section;
  a single line with the air code in the schematic. Pipes: medium line in plan and section; the schematic adds the
  medium code and `DN …`.

**Build "+" (connections).** The port ids name the medium: `flow`/`f1…` a heating flow pipe, `return`/`r1…` a return
pipe, `cold`, `hot`, `circulation`, `waste` the matching water pipe, risers the pipe of their medium, air terminals the
duct of their air kind, the ventilation unit `outdoor`/`exhaust`/`extract`/`supply` ducts; inline heating parts (pump,
mixing valve, thermostatic valve, expansion vessel, heat meter) fit flow and return; ball valve, check valve, safety
valve (inlet) and strainer fit flow and return with `service` = heating (default) and every drinking water pipe
(`water`: cold, hot, circulation; "+" draws a cold water pipe) with `service` = water, the water meter always the latter;
the safety valve blows off into its `waste` port; buffer tank circuits `f1`/`r1`/`f2`/`r2` take flow and return pipes
like the manifold circuits; the duct parts (damper, fire damper, silencer) have `inlet`/`outlet` on the duct axis that
fit every air duct (`air`; "+" draws a duct of their `kind`); the split air conditioner drains its condensate into a
waste pipe and takes a power cable; outlets, detectors, appliances and the
equipotential bonding bar an installation cable (`electric.installation`, the cable route of the electrical module).
Pipes and ducts carry their medium, so their free ends offer "+" to continue or to dock the next radiator, fixture or
terminal with its fitting connection.

## Context actions

Mounting height… (input, every element), medium/kind as choices ▾ (pipe, duct, riser, air terminal, duct parts, outlets,
detector), pipe system heating/drinking water ▾ (ball valve, check valve, safety valve, strainer),
flow arrow and hops (pipe), air flow arrow (duct), more/fewer circuits (manifold), heating coil (water heater),
riser direction.

## Engineering values (Calc 4)

Values are optional: every element still inserts and scales without them. They are entered and shown with the Values
card (*Edit ▸ Values ▸*, the *Values* button in Explore and Engineer) and stored in optional text parameters
(engineering section of the inspector); the existing number parameters `dn` (pipe), `width` (duct) and `spacing` (floor
loop) are read as values. Water: ρ = 1000 kg/m³ and *c* = 4.19 kJ/(kg·K) as defaults; air: ρ·*c*_p = 0.34 W·h/(m³·K).
Card variables come first. Design notes:
[engineering-calculations.md, section 13](../design/engineering-calculations.md#13-implementation-notes-calc-4-fluid-process-building-services-energy-2026-11-25).

| Element | Variables (parameter) | Local relations [source] |
|---|---|---|
| Boiler | Φ (`heatOutput`), θ_F (`flowTemp`), θ_R (`returnTemp`), *V̇* (`flowRate`, l/h); engineer ρ (`density`), *c* (`specificHeat`) | Φ = ρ·*c*·*V̇*·(θ_F − θ_R) [S29] |
| Heat pump (outdoor unit) | Φ, *P*_el (`powerEl`), COP (`cop`), θ_F; panel θ_Q (`sourceTemp`), θ_R, *V̇*; engineer ρ, *c* | Φ = COP·*P*_el [S32]; Φ = ρ·*c*·*V̇*·(θ_F − θ_R) [S29] |
| Radiator | Φ, Φ₅₀ (`phi50`), θ_F, θ_R; panel *V̇*, θ_i (`roomTemp`, default 20 °C), *n* (`exponent`, default 1.3), Δθ_ln (`excessTemp`), Φ_HL (`roomHeatLoad`, from the room); engineer ρ, *c* | Δθ_ln = (θ_F − θ_R)/ln((θ_F − θ_i)/(θ_R − θ_i)); Φ = Φ₅₀·(Δθ_ln/50 K)^*n* (EN 442-2 characteristic [S31]); Φ = ρ·*c*·*V̇*·(θ_F − θ_R); operating point ρ·*c*·*V̇*·(θ_F − θ_R) = Φ₅₀·(Δθ_ln/50 K)^*n*, solved numerically for θ_R [S31] |
| Underfloor heating loop | Φ, *A* (`area`), *q* (`heatFlux`), *V̇*; panel θ_F, θ_R, *s* (`spacing`), *L* (`loopLength`); engineer ρ, *c* | Φ = *q*·*A*; Φ = ρ·*c*·*V̇*·(θ_F − θ_R); *L* ≈ *A*/*s* [S30] |
| Circulator pump | *V̇*, *H* (`head`), *P*_el (`powerEl`), η (`efficiency`); panel *P*_hyd (`powerHyd`); engineer ρ | *P*_hyd = ρ·*g*·*H*·*V̇*; *P*_el = *P*_hyd/η [S28] |
| Mixing valve (3-way) | θ_AB (`tempAB`), θ_A (`tempA`), θ_B (`tempB`), *V̇*_AB (`flowAB`); panel *V̇*_A (`flowA`), *V̇*_B (`flowB`) | *V̇*_AB = *V̇*_A + *V̇*_B [S1]; θ_AB = (*V̇*_A·θ_A + *V̇*_B·θ_B)/*V̇*_AB [S29] |
| Water heater (storage cylinder) | *V* (`volume`), Δθ (`deltaT`), *Q* (`heat`), *P* (`power`); panel *t* (`heatUpTime`); engineer ρ, *c* | *Q* = ρ·*V*·*c*·Δθ; *t* = *Q*/*P* (no losses) [S29] |
| Buffer tank | *V* (`volume`, l), Δθ (`deltaT`, temperature spread), *Q* (`heat`, kWh); engineer ρ, *c* | *Q* = ρ·*V*·*c*·Δθ [S29] |
| Heat meter | Φ (`heatOutput`, thermal power), *V̇* (`flowRate`), θ_F (`flowTemp`), θ_R (`returnTemp`); engineer ρ, *c* | Φ = ρ·*c*·*V̇*·(θ_F − θ_R) [S29] |
| Water meter | *V̇* (`flowRate`, m³/h) | – (the flow of the run it sits in) |
| Air terminal | *V̇* (`flowRate`, m³/h), *A* (`roomArea`), *n* (`airChange`, per hour), *V* (`roomVolume`); panel *h* (`roomHeight`, default 2.5 m) | *V* = *A*·*h*; *V̇* = *n*·*V*/1 h [S30] |
| Ventilation unit with heat recovery | *V̇*_SUP (`flowSupply`), η_t (`recovery`), θ_SUP (`tempSupply`), Φ_WRG (`heatRecovered`); panel *V̇*_ETA (`flowExtract`), θ_ODA (`tempOutdoor`), θ_ETA (`tempExtract`) | θ_SUP = θ_ODA + η_t·(θ_ETA − θ_ODA); Φ = 0.34 W·h/(m³·K)·*V̇*_SUP·(θ_SUP − θ_ODA) [S30] |
| Pipe | *V̇* (`flowRate`), *d*_i (`innerDiameter`), *v* (`velocity`), θ (`temperature`); panel DN (`dn`) | *v* = 4·*V̇*/(π·*d*_i²) [S27]; *d*_i ≈ DN when no inner diameter is entered (50 % tolerance, DN is a nominal size) [S30] |
| Air duct | *V̇*, *d* (`width`, diameter or width), *v*, *A* (`crossSection`); panel *b* (`ductHeight`), *d*_h (`hydraulicDiameter`) | *v* = *V̇*/*A*; round: *A* = π·*d*²/4, *d*_h = *d*; with a height *b* the duct is rectangular: *A* = *d*·*b*, *d*_h = 2·*d*·*b*/(*d* + *b*) [S27, S30] |
| Appliance outlet (electrical load) | *P* (`power`), *I* (`current`, per phase); panel *U* (`voltage`, phase to neutral, default 230 V), cos φ (`powerFactor`, default 1) | single-phase *I* = *P*/(*U*·cos φ); with `phase` three-phase *I* = *P*/(3·*U*·cos φ) [S7] |

The air change rate *n* is a plain number per hour ("1" for 1 h⁻¹): Core has no h⁻¹ unit yet. The room values sit on
the air terminal that serves the room.

**Room values by containment** (since 2026-11-26). An air terminal or radiator whose centre lies inside an architecture
room (`arch.room`) on the same page takes the room's values through the link `bs.room.link` ("Taken from the room";
the smallest room when rooms are nested): the air terminal its *A*, *h* and *V*, so *n* alone gives *V̇* = *n*·*V*; the
radiator the room's heating load estimate Φ_HL (panel, `roomHeatLoad`; the room's Φ_HL = *q*·*A*). No line is needed;
the room is found by its type id (the module does not reference Architecture). An entered value that disagrees with the
room is a conflict, as everywhere.

**Port flows.** Pipes and ducts join ports into nets. The volume flow is passed on an unbranched run; at a branch the
total port gets the sum of the parts: the generator's flow and return ports, the circulator's outlet, the mixer's AB port
and the ventilation unit's spigots are totals; radiators, floor loops, the circulator's inlet, the mixer's A and B ports
and air terminals are parts. The stream temperature is passed on an unbranched run and from the one supplying outlet (the
generator's flow port, a consumer's return port, the mixer's AB port) to every inlet of a split. Thermostatic valves, ball
valves, check valves and strainers pass both; circulators, heat meters and water meters pass the temperature, and their
inlet (part) and outlet (total) carry their own volume flow, so a meter shows the flow of its run; dampers, fire dampers
and silencers pass the air flow. The buffer tank decouples its circuits (no values at its ports). Heat losses of pipes are not modelled; mixing at a merge is only computed in the
mixing valve. At a port of another module's element (an energy heat port takes flow and return), pipes of different
media stay separate nets; only the heating flow takes that port's values (engineering-calculations.md section 6.1).

Examples (accept criteria): a boiler at 55 °C feeding a radiator (Φ₅₀ = 1000 W, *n* = 1.3, room 20 °C) over a pipe
with 50 l/h: the radiator's return temperature solves the operating point (θ_R ≈ 46.0 °C, Φ ≈ 521 W), and the return
pipe and the boiler take θ_R. A room of 20 m² (2.5 m high) with *n* = 1 h⁻¹ gives 50 m³/h at the terminal, in the duct
and at the unit's supply spigot; an 80 mm duct then runs at 2.76 m/s.

**Checks** (hints only, overlay, nothing changes):

| Id | Rule | Severity | Source |
|---|---|---|---|
| `heat.velocity` | heating pipe (flow/return) outside 0.3–1.0 m/s; water pipe above 2 m/s (textbook values) | hint | [S30] |
| `air.velocity` | duct velocity outside 2–4 m/s (residential, textbook) | hint | [S30] |
| `heat.cop.carnot` | heat pump COP > *T*_F/(*T*_F − *T*_Q) in kelvin (impossible) | warning | [S29] |
| `heat.balance` | entered generator output below the sum of Φ of the radiators and floor loops joined to it by heating pipes | hint | [S29] |
| `heat.roomload` | the radiators and floor loops standing in a room give less than the room's heating load estimate Φ_HL (judged only when all their outputs are known) | hint | [S30] |

## Host notes

All output uses native primitives (lines, polylines, polygons, rectangles, ellipses, arcs, cubic paths, text), so every
host draws it natively; no vector picture is needed. Pipes depend on crossing pipes (hops) and report them as
dependents. The installation view switch (`ServiceViews.Switch`) is engine-side; hosts only need a "view for the
selection" command once Core/engine expose it.

## Tests and gallery

`tests/InventaSketch.Modules.BuildingServices.Tests`: registration and id conventions, bilingual names, the three views
with their icons, plan scale and `height` on every type, every library item rendered in every view in both languages
inside its box (also stretched, squashed and with every choice), scaling changes `Size` not parameters, views draw
different symbols, ports with the same ids in every view, mounts, rails, line language (corners, extend, branches,
junction dots, hops), duct width at the plan scale, the view switch (x kept, height mapping, docked pipes, rail docking
on the riser, round trip back to plan, height from dragging in section, rotation) and the JSON round trip; for the
valves, strainer, meters, buffer tank, solar collector, duct parts and split unit (`FittingTests`) the port media, docking
on a free end of the existing pipes and ducts of every fitting medium, "+" at every port, the service choice and the
values of buffer tank and meters in a pipe run. The gallery
test writes `gallery/*.html` (every type in its three views, stretched, presets, and a sample bathroom in plan,
section and schematic).
