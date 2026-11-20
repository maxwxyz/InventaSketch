# Electric machines & drives module (`machines`)

Quick sketches of electric machines and electric drive trains for presentations and patent drawings: radial-flux and
axial-flux machine cross-sections and longitudinal sections, separate machine components for free assembly, drive
system blocks with ports and flux/rotation/torque annotations.

> **Limits.** All drawings are *illustrative*. Slot shapes, magnet positions, air gap, yoke thickness, winding layout and
> current pattern follow plausible proportions but are **not** the result of an electromagnetic design or a winding
> calculation. Do not read dimensions, flux densities or winding schemes from them.

Every object can be inserted without entering parameters. Free uniform and non-uniform scaling changes only the box size;
parameters stay untouched. Sectioned iron is hatched (stator 45°, rotor 135°, shaft 45°, housing 135°, so adjacent parts
differ), copper windings use a light copper fill with a fine cross hatch (readable in monochrome), north magnets are red
and south magnets blue (black and white in monochrome/patent output).

## Object types

### Complete machines (category "Electric machines")

| Type id | Name (EN / DE) | Views |
|---|---|---|
| `machines.radial` | Radial-flux machine / Radialflussmaschine | `axial` cross-section (icon `view-axial`), `side` longitudinal section (`view-section`), `schematic` motor symbol (`view-schematic`) |
| `machines.axial` | Axial-flux machine / Axialflussmaschine | `side` section (`view-section`), `face` view of a disc (`view-axial`), `schematic` (`view-schematic`) |

**Radial-flux machine.** Cross-section: housing (optional cooling-jacket channels), stator lamination with slots, teeth and
back iron (hatched), winding in every slot (optional current directions as dot/cross), air gap, rotor, shaft.
Longitudinal section: housing with end shields and bearings, stator stack (yoke hatched), coil sides with end windings,
rotor stack with type-specific details, shaft. Schematic: IEC-style circle with **M** or **G**, phase number (`3~`), phase
leads U/V/W with terminals and a shaft stub.

Rotor types (`rotor` parameter):

| Value | Rotor | Cross-section | Longitudinal section |
|---|---|---|---|
| `spm` | PMSM, surface magnets | arc magnets on the rotor surface | magnet layer |
| `ipm` | PMSM, interior V-shaped magnets | two magnets per pole in pockets (flux barriers) | magnet band |
| `spoke` | PMSM, spoke magnets | radial magnets between the poles | magnet band |
| `eesm` | Wound-field synchronous machine | salient poles with pole shoes and field coils | field winding ends, slip rings with brushes |
| `asm` | Induction machine, squirrel cage | closed rotor bars (`bars`) | bars and short-circuit end rings |
| `srm` | Switched reluctance machine | salient rotor teeth; stator with open slots and tooth coils | plain stack |

**Outer rotor** (`layout = outer`): the rotor ring surrounds the stator; the stator sits on a stationary carrier on the
axle (optional coolant channels in the carrier); the longitudinal section shows the rotor bell with end disc and bearing.

**Axial-flux machine** (single stator, double rotor, YASA style): section with segmented stator cores (pole shoes on both
air gaps), coils around the cores, two rotor discs with magnets (opposite polarity across the stator), housing and
bearings. The face view shows either the rotor magnet disc (`poles` segments with N/S labels) or the stator coil ring
(`coils` segments) via `disc`.

### Machine components (category "Machine components")

| Type id | Name (EN / DE) | Views | Notes |
|---|---|---|---|
| `machines.part.stator` | Stator core / Ständerblechpaket | `axial`, `side` | slots, winding, open slots (tooth coils), inner/outer layout |
| `machines.part.rotor` | Rotor / Läufer | `axial`, `side` | all six rotor types, optional shaft |
| `machines.part.magnet` | Magnet segment / Magnetsegment | `axial` arc segment, `side` block | polarity, magnetisation arrow, N/S label |
| `machines.part.coil` | Coil / Spule | `section` (dot/cross), `top` racetrack, `symbol` inductor | iron core, current directions |
| `machines.part.airgap` | Air-gap ring / Luftspaltring | `axial`, `side` | dotted ring |
| `machines.part.endwinding` | End winding / Wickelkopf | `side`, `axial` end view | coil groups |
| `machines.part.housing` | Machine housing / Maschinengehäuse | `axial`, `side` | cooling jacket, fins, bearings |
| `machines.part.cooling` | Cooling channel / Kühlkanal | `axial` jacket, `section` channel with flow arrow | channel count, flow direction |
| `machines.part.encoder` | Resolver / encoder / Resolver / Drehgeber | `axial`, `side` | resolver (toothed stator, two-lobe rotor) or optical encoder (slit disc, read head) |
| `machines.part.slipring` | Slip rings & brushes / Schleifringe & Bürsten | `side`, `axial` | 1–4 rings |

The default sizes fit together: the default rotor dropped into the default stator leaves an air gap of about 1 mm, the
default air-gap ring fills that gap, magnet segments fit onto the default rotor and the default housing fits around the
default stator.

### Drive system blocks (category "Drive system blocks")

| Type id | Name (EN / DE) | Views |
|---|---|---|
| `machines.drive.battery` | Battery / Batterie | `block`, `symbol` (cells) |
| `machines.drive.inverter` | Inverter / Wechselrichter | `block` (converter square with = and ~), `symbol` three-phase bridge (six switches with freewheeling diodes, optional DC-link capacitor, terminals U/V/W) |
| `machines.drive.dclink` | DC link / Zwischenkreis | `block`, `symbol` (capacitor) |
| `machines.drive.motor` | E-motor / E-Motor | `block`, `symbol` (M/G with phase leads) |
| `machines.drive.gearbox` | Gearbox / Getriebe | `block`, `symbol` (gear pair schematic) |
| `machines.drive.differential` | Differential / Differential | `block`, `symbol` (schematic) |
| `machines.drive.wheel` | Wheel / Rad | `side`, `top` |

Block labels follow the host language; the object text replaces the label (e.g. "HV battery").

### Annotations (category "Flux, rotation & torque")

| Type id | Name (EN / DE) | Views |
|---|---|---|
| `machines.ann.flux` | Flux path / Flusspfad | `arc` (flux line between two poles), `loop` (closed magnetic circuit) |
| `machines.ann.rotation` | Rotation arrow / Drehrichtungspfeil | `axial` (around the centre), `side` (around the shaft) |
| `machines.ann.torque` | Torque arrow / Drehmomentpfeil | `curved` with label (T in English, M in German, or the object text), `vector` (double-headed moment vector) |

## Parameters (all optional)

| Key | Types | Meaning | Default |
|---|---|---|---|
| `rotor` | radial, rotor | `spm`, `ipm`, `spoke`, `eesm`, `asm`, `srm` | `spm` |
| `layout` | radial, stator, rotor | `inner` or `outer` rotor | `inner` |
| `slots` | radial, stator | stator slot count (6–96) | 36 |
| `poles` | radial, rotor, axial, stator | pole count (SRM: rotor teeth); interior-magnet and salient-pole drawings use at least 4 | 8 (axial: 16) |
| `bars` | radial, rotor | squirrel-cage bars (advanced) | 28 |
| `coils` | axial | stator coils | 18 |
| `winding` | radial, stator | show the winding | on |
| `currents` | radial, stator, rotor, axial, coil | current directions (dot = out of the drawing plane, cross = into it) | off (coil: on) |
| `housing` | radial, axial | housing and bearings | on |
| `cooling` | radial, axial, housing | cooling-jacket channels (outer rotor: carrier channels) | off |
| `mode`, `phases` | radial, axial, motor block | motor/generator letter, phase count in the symbol | motor, 3 |
| `hatch` | most sectioned parts | hatch sectioned iron (advanced) | on |

The current pattern in distributed windings is an instantaneous illustration: conductors under a north pole carry
current out of the drawing plane, under a south pole into it.

## Context actions

View switches (with the `ViewDef.Icon` pictograms), **next rotor type** (`param.rotor`, icon `cmd-cycle`), **poles ±2**
(`param.poles.inc/dec`), **slots ±3** (`param.slots.inc/dec`), **current directions on/off**, **cooling on/off**, **inner/outer
rotor** (`cmd-swap`); motor symbol: motor/generator; magnet: swap polarity, arrow on/off; coil: currents, core; cooling
channel: count, flow direction; encoder: resolver/encoder; annotations: reverse direction. Switching between a round
view and a longitudinal section adapts the box width so the proportions are kept.

## Snapping anchors

- All round parts expose a **Hub** point at the centre (concentric snapping, hub onto shaft axis).
- **Stator** (inner-rotor layout) exposes its air-gap circle as **PitchInner**, the **rotor** as **Pitch**. Like the pitch
  circles of an internal gear pair, both lie on the air-gap centre circle, so the rotor snaps **concentrically** into the
  stator (and the stator around the rotor). The air-gap ring exposes the same circle. Housing and cooling jacket expose
  their bore as PitchInner (concentric with the stator). Magnet segments expose their inner arc as PitchInner, so the arc
  centre snaps onto the rotor centre.
- Longitudinal sections (`side`) expose the shaft **Axis** segment; encoders, slip rings and other parts snap their hub
  onto it, and machine axes align collinearly with mechanical shafts.
- Drive blocks expose **Port** anchors on the four edge midpoints (block view) or on the symbol terminals (battery +/−,
  inverter DC+/DC−/U/V/W, motor phase leads, gearbox input/output, differential input/left/right), so arrows and wires
  snap on.

## Known limits

- Illustrative geometry only (see above); no winding schemes, no slot/pole combination checks.
- With user-scaled rotors and stators whose air-gap circles no longer coincide, the core snapper may prefer its
  internal-gear "mesh" snap over the concentric snap for small offsets.
- Outer-rotor components are not size-matched by default (scale the rotor ring to fit around the stator).
- Hosts: all output consists of lines, polylines/polygons, rectangles, ellipses and text; hatches and copper cross hatches
  are explicit hatch lines where a host has no pattern fill (see the host capability matrix).
