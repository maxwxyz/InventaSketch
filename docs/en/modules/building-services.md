# Building services module (`building-services`)

Heating, plumbing, ventilation and electrical house installation (German: *Technische Gebäudeausrüstung, TGA*)
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
| `bs.san.wc` | WC / WC | 3.6x5.4 / 3.6x4 / 3.6x3.6 | floor | waste, cold | – |
| `bs.san.basin` | Washbasin / Waschbecken | 6x4.5 / 6x4 / 6x3.5 | wall | waste, hot, cold | – |
| `bs.san.shower` | Shower / Dusche | 9x9 / 9x22 / 9x8 | floor | waste, hot, cold | – |
| `bs.san.bathtub` | Bathtub / Badewanne | 17x7.5 / 17x6 / 17x4 | floor | waste, hot, cold | – |
| `bs.san.heater` | Water heater (storage cylinder) / Warmwasserspeicher | 6x6 / 6x16 / 6x10 | floor | hot, circulation, cold (+ flow, return with coil) | `coil` (true) |
| `bs.san.drain` | Floor drain / Bodenablauf | 2x2 / 2x1.5 / 2x2 (symbol) | in floor | waste | – |
| `bs.san.riser` | Riser (vertical pipe) / Steigleitung / Fallleitung | 2x2 / 2x28 / 2x28 | floor | plan: riser; section/schematic: bottom, top + rail | `medium`, `dir` up/down/through |
| `bs.vent.terminal` | Air terminal (valve / grille) / Luftdurchlass (Ventil / Gitter) | 3x3 / 3x1.6 / 3x3 (symbol) | ceiling | duct | `kind` supply/extract |
| `bs.vent.unit` | Ventilation unit with heat recovery / Lüftungsgerät mit Wärmerückgewinnung | 7x4.5 / 7x8 / 7x5 | wall | outdoor, exhaust, extract, supply | – |
| `bs.elec.data` | Communication outlet / Kommunikationsanschluss | 4x4 (symbol) | wall | cable | `kind` data/phone/tv |
| `bs.elec.detector` | Detector / Melder | 4x4 / 4x1.4 / 4x4 (symbol) | ceiling | cable | `kind` smoke/heat/motion |
| `bs.elec.appliance` | Appliance outlet / Geräteanschlussdose | 4x4 (symbol) | wall | cable | `kind` cooker/washer/dryer/dishwasher/generic |
| `bs.elec.bonding` | Equipotential bonding bar / Potentialausgleichsschiene | 6x2 / 6x2 / 6x3 (symbol) | wall | earth + rail along the bar | – |

Every box type also has `height` and the advanced `scale`. Labels in drawings follow the UI language (HP/WP, DHW/WW,
codes F/VL, R/RL, CW/KW, HW/WW, HWC/Z, W/SW, SUP/ZUL, ETA/ABL, ODA/AUL, EHA/FOL, LAN/TEL/TAE/TV/SAT, S/RM, H/WM,
PIR/BM, CK/HE, …). Pipe colours follow the architecture module (flow red, return blue dashed, cold green, hot
magenta dash-dot, waste brown, wider); circulation is violet dash-dot-dot. Air colours: supply blue, extract ochre,
outdoor green, exhaust brown. Colours and codes are common practice, not a cited standard.

## Library

One item per type (id = type id), a `<type>.section` and `<type>.schematic` preset of every box type (so the insert
box can offer elements in the selection's view), pipes per medium (`bs.pipe.flow` … `bs.pipe.waste`; flow/return in
the heating category), ducts per air kind (`bs.duct.supply` …), and variants: `bs.heat.radiator.long`,
`bs.heat.manifold.8`, `bs.heat.floorloop.small`, `bs.san.riser.waste` (soil stack), `bs.san.riser.hot`,
`bs.san.riser.flow`, `bs.san.riser.return`, `bs.san.heater.electric`, `bs.vent.terminal.extract`,
`bs.elec.data.phone`, `bs.elec.data.tv`, `bs.elec.detector.heat`, `bs.elec.detector.motion`,
`bs.elec.appliance.washer`, `bs.elec.appliance.dishwasher`.

Categories: `bs.heating` Heating / Heizung, `bs.plumbing` Plumbing & sanitary / Sanitär & Trinkwasser,
`bs.ventilation` Ventilation / Lüftung, `bs.electrical` Electrical installation (additions) / Elektroinstallation
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

## Context actions

Mounting height… (input, every element), cycle medium/kind (pipe, duct, riser, air terminal, outlets, detector),
flow arrow and hops (pipe), air flow arrow (duct), more/fewer circuits (manifold), heating coil (water heater),
riser direction.

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
on the riser, round trip back to plan, height from dragging in section, rotation) and the JSON round trip. The gallery
test writes `gallery/*.html` (every type in its three views, stretched, presets, and a sample bathroom in plan,
section and schematic).
