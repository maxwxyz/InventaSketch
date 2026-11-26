# Electric machines & drives module (`machines`)

Quick sketches of electric machines and electric drive trains for presentations and technical figures: radial-flux and
axial-flux machine cross-sections and longitudinal sections, separate machine components for free assembly, drive
system blocks with ports, drivetrain schematic parts that sit on mechanical shafts (e-machine, combustion engine, wheel)
and flux/rotation/torque annotations.

> **Limits.** All drawings are *illustrative*. Slot shapes, magnet positions, air gap, yoke thickness, winding layout and
> current pattern follow plausible proportions but are **not** the result of an electromagnetic design or a winding
> calculation. Do not read dimensions, flux densities or winding schemes from them.

Every object can be inserted without entering parameters. Free uniform and non-uniform scaling changes only the box size;
parameters stay untouched. Sectioned iron is hatched (stator 45°, rotor 135°, shaft 45°, housing 135°, so adjacent parts
differ), copper windings use a light copper fill with a fine cross hatch (readable in monochrome), north magnets are red
and south magnets blue (black and white in monochrome output).

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
| `machines.drive.wheel` | Wheel / Rad | `side`, `top`, `schematic` (see "Drivetrain schematic") |
| `machines.drive.load` | Driven load / Arbeitsmaschine (Last) | `block` (flywheel glyph), `symbol` (flywheel disc on a shaft with "J"; input port `in` at the left, shaft axis) |

Block labels follow the host language; the object text replaces the label (e.g. "HV battery").

### Drivetrain schematic (category "Drivetrain schematic")

Parts for building drivetrains and gearboxes quickly on a slide together with the shafts, gears, bearings, clutches and
the differential of the mechanical module (`mech.*`). They sit on a `mech.shaft` and **follow it** (core coupling, see
"Snapping anchors").

| Type id | Name (EN / DE) | Views |
|---|---|---|
| `machines.drivetrain.emachine` | E-machine (gearbox schematic) / E-Maschine (Getriebeschema) | (default: radial flux, inner rotor, PMSM; variants below) `stick` schematic (`view-schematic`): rotor bars on a rotor carrier with fixed hub cross, hatched stator bars across the air gap, stator tied to the housing; `side` simplified longitudinal section (`view-side`): stator stack with end windings, rotor stack, shaft, housing; `axial` view (`view-axial`): stator ring, rotor disc, shaft |
| `machines.drivetrain.engine` | Internal combustion engine / Verbrennungsmotor | `block` (`view-block`, label "Engine (ICE)" / "Verbrennungsmotor", or the object text), `schematic` crankshaft schematic (`view-schematic`): crankcase, cylinders with pistons and connecting rods, crankshaft with throws, output shaft on the right |
| `machines.drive.wheel` | Wheel / Rad | additional `schematic` view (`view-schematic`): dark tyre bar for top-view drivetrain layouts (library item "Wheel (drivetrain schematic)" in this category) |

**E-machine variants.** One type, two optional parameters, each variant also as a library item (schematic and side
view):

| `layout` (arrangement) | Schematic / side view |
|---|---|
| `inner` (default) | radial flux, inner rotor: rotor bars on a rotor carrier, stator bars around them, stator tied to the housing |
| `outer` | radial flux, outer rotor: rotor bell from the shaft around the stator, stator on a fixed sleeve tied to the housing |
| `axial` | axial flux, one rotor disc beside one stator (axial air gap) |
| `axial-2r` | axial flux, double rotor (YASA type): one stator between two rotor discs |
| `axial-2s` | axial flux, double stator: one rotor disc between two stators |

| `kind` (machine type) | Mark on the rotor (air-gap side) |
|---|---|
| `pmsm` (default) | magnets, red (N) / blue (S) |
| `eesm` | field winding (copper) plus two slip rings with brushes on the shaft |
| `asm` | cage bars (grey) with short-circuit rings |
| `srm` | salient rotor teeth (no magnets, no winding) |

The axial view shows the rotor in cross-section (radial flux) or the face of the rotor disc (axial flux) with the same
marks (magnet sectors, field poles, cage bars, slots between teeth).

Further parameters (optional): e-machine `shaft` (shaft stubs; off when the machine sits on a drawn `mech.shaft` body in
side view, on by default), `ground` (housing connection: ground strokes in the schematic, housing outline in side and
axial view, on), `hatch`; engine `cylinders` (1-6, default 4; the schematic shows an inline engine, outer throws opposite
the inner ones). Context actions: arrangement ▾ and machine type ▾ (choices with the machine pictograms), shaft stubs on/off, housing
connection on/off.

The drawings are schematic symbols in the style of kinematic gearbox schematics, not a standardised graphical symbol.

**Search.** All drivetrain parts carry the keywords `drivetrain`, `powertrain` and `antriebsstrang`: searching for
"Antriebsstrang" finds e-machine, engine, e-motor, gearbox, differential (block and bevel gear set), wheel, shafts,
spur gears, disc and multi-disc clutch and the universal joint (with the mechanical module loaded).

**"Insert ▸" companions.** A shaft offers spur gear, ball bearing, e-machine (in the shaft's kind of view: schematic shaft
→ schematic e-machine, shaft body → side view), wheel, disc clutch and flexible coupling (then bevel gear, cylindrical
roller bearing, rigid coupling); the e-machine offers inverter and battery blocks, shaft, gear, bearing and DC link; the
engine offers clutch, flywheel, shaft, gearbox, e-machine; the wheel offers shaft and differential; the bevel
differential offers shaft, wheel and gears; the disc clutch offers shaft, engine, flywheel and e-machine; the e-motor block
offers inverter, battery, gearbox and the e-machine. Ids of the other module are skipped when it is not loaded.

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

View switches (with the `ViewDef.Icon` pictograms), **rotor type ▾** (choice `param.rotor=…`), **poles ±2**
(`param.poles.inc/dec`), **slots ±3** (`param.slots.inc/dec`), **current directions on/off**, **cooling on/off**, **inner/outer
rotor** (`cmd-swap`); motor symbol: motor/generator; magnet: swap polarity, arrow on/off; coil: currents, core; cooling
channel: count, flow direction; encoder: resolver/encoder; annotations: reverse direction. Switching between a round
view and a longitudinal section adapts the box width so the proportions are kept.

## Snapping anchors

- All round parts expose a **Hub** point at the centre (concentric snapping, hub onto shaft axis).
- **Stator** (inner-rotor layout) exposes its real bore as **PitchInner**, the **rotor** its real outer radius as **Pitch**
  (outer-rotor layout: stator outer radius as Pitch, rotor inner radius as PitchInner; the full radial machine exposes both).
  Their radii are almost equal, so the core snapper treats the pair as a fit and snaps the rotor **concentrically** into
  the stator (and the stator around the rotor) instead of meshing it like a planet in a ring gear — also after free
  scaling (rule: internal mesh only below 90 % of the ring radius, `Snapper.InternalMeshMaxRatio`). The air-gap ring
  exposes its centre circle as PitchInner. Housing and cooling jacket expose
  their bore as PitchInner (concentric with the stator). Magnet segments expose their inner arc as PitchInner, so the arc
  centre snaps onto the rotor centre.
- Longitudinal sections (`side`) expose the shaft **Axis** segment; encoders, slip rings and other parts snap their hub
  onto it, and machine axes align collinearly with mechanical shafts.
- **Drivetrain parts and coupling.** The e-machine (every view) and the wheel (every view) expose a **Hub** at the
  centre; the engine exposes its **Hub** and an `out` **Port** at the output shaft end (schematic: right end of the
  crankshaft axis, which is also an **Axis** segment for a flywheel or pulley; block: right edge midpoint). The e-machine
  has `w`/`e` **Ports** at the shaft ends and a **Mount** below the stator, the engine schematic a **Mount** below the
  crankcase; wheel top and schematic views expose the wheel **Axis**. A hub lying on a `mech.shaft` axis makes the part
  follow the shaft through the core coupling (`Coupling`, "carried"): moving, rotating or stretching the shaft takes the
  e-machine, the wheels and an engine whose output sits on the shaft end along (stretching keeps the relative position on
  the axis). Moving a wheel alone slides it along the shaft; moving the e-machine, the engine or the e-motor takes the
  shaft and everything on it along (`ObjectType.DrivesShaft`: a drive leads its shaft). The wheel side view moves together with a concentric part in
  axial view (e.g. a gear on the same axle).
- Drive blocks expose **Port** anchors on the four edge midpoints (block view) or on the symbol terminals (battery +/−,
  inverter DC+/DC−/U/V/W, motor phase leads, gearbox input/output, differential input/left/right), so arrows and wires
  snap on.

### Build "+" (connections)

Phase terminals u/v/w offer an AC wire, battery and DC-link terminals (plus/minus, dc+/dc-) a DC wire, coil ends, brushes
and encoder signals a wire (wires of the electrical module), the cooling channel in/out a water pipe (energy module), the
four sides of the drive system blocks an arrow (block diagram), then the elements that fit. Shaft ends (gearbox, wheels,
e-machines, engine output) are mechanical and are built on with shafts at their axis ends.

## Engineering values (Calc 3)

Values are optional: every element still inserts and scales without them. They are entered and shown with the Values
card (*Edit ▸ Values ▸*, the *Values* button in Explore and Engineer) and stored in optional text parameters (engineering
section of the inspector); `poles` is the existing pole count. Speeds *n*, torques *T* (German *M*) and powers are
magnitudes; *P* = 2π·*n*·*T*. Card variables come first. Design notes:
[engineering-calculations.md, section 12](../design/engineering-calculations.md#12-implementation-notes-calc-3-machine-elements-2026-11-25).

| Element | Variables (parameter) | Local relations [source] |
|---|---|---|
| Radial- and axial-flux machine (`machines.radial`, `machines.axial`) | *n* (`speed`), *T* (`torque`), *P* (`power`), *f* (`frequency`); panel *n*_s (`syncSpeed`), *s* (`slip`), pole count 2*p* (`poles`); engineer η (`efficiency`), *P*_el (`powerEl`), *P*_N (`ratedPower`), *T*_N (`ratedTorque`) | *n*_s = 60·*f*/*p* (*p* = pole count/2) [S15]; *s* = (*n*_s − *n*)/*n*_s [S15]; *P* = 2π·*n*·*T*; motor operation *P* = η·*P*_el, generator operation (`mode` = generator, the generator symbol) *P*_el = η·*P* [S14] |
| E-motor block, drivetrain e-machine | *n*, *T*, *P*, η; engineer *P*_el, *P*_N, *T*_N; operating mode `mode` (the e-motor block's own choice; for the drivetrain e-machine an engineering parameter, motor by default) | *P* = 2π·*n*·*T*; motor *P* = η·*P*_el, generator *P*_el = η·*P* [S14] |
| Combustion engine | *n*, *T*, *P*; engineer *P*_N, *T*_N | *P* = 2π·*n*·*T* (no thermodynamics) |
| Gearbox, differential (blocks) | *i* (`ratio`), η (`efficiency`, default 100 %), *n*₂ (`speedOut`), *T*₂ (`torqueOut`); panel *n*₁ (`speedIn`), *T*₁ (`torqueIn`), *P*₁ (`powerIn`), *P*₂ (`powerOut`) | *n*₁ = *i*·*n*₂; *T*₂ = *T*₁·*i*·η [S18]; *P*₁ = 2π·*n*₁·*T*₁; *P*₂ = 2π·*n*₂·*T*₂ |
| Wheel | *r* (`radius`, dynamic rolling radius), *n*, *T*, *v* (`linSpeed`, km/h); panel *F* (`force`), *P* | *v* = 2π·*n*·*r*; *F* = *T*/*r* [S18]; *P* = 2π·*n*·*T* |

**Links.**

- *Drive chain*: a connector (an arrow or any line) whose first docked end sits on a block with a mechanical output
  (e-motor, e-machine, engine, gearbox, differential) and whose last docked end sits on a block with a mechanical input
  (gearbox, differential, wheel) passes *n* and *T* downstream: motor → gearbox → differential → wheel. The direction of
  the connector decides which block is upstream.
- *Shafts*: the e-machine, the engine and the wheel on a mechanical shaft share its speed; the mechanical module passes
  the torque along the shaft line and through gear meshes (keys `n`, `T`, `P` in both modules).

**Checks** (hints only, overlay, nothing changes):

| Id | Rule | Severity | Source |
|---|---|---|---|
| `mach.slip.sign` | induction machine (`rotor` = `asm`) in motor mode with *n* > *n*_s (generator operation) | info | [S15] |
| `mach.efficiency` | η > 100 %, *P*₂ > *P*₁ (gearbox, differential), *P* > *P*_el (motor) or *P*_el > *P* (generator) | warning | [S1] |
| `mach.overload` | *P* > *P*_N or *T* > *T*_N (rating entered by the user) | warning | [S14] |

**Generator mode** (2026-11-26). The operating mode (`mode`: motor by default, generator) decides the efficiency rule:
a motor turns electrical into mechanical power (*P* = η·*P*_el, `mach.efficiency.motor`), a generator driven by a turbine,
an engine or a gear stage turns the shaft power into electrical power (*P*_el = η·*P*, `mach.efficiency.generator`). A
machine on a process turbine shaft takes the turbine's *P* over the shaft and gives *P*_el (process module). Speeds,
torques and powers stay magnitudes; the sign of the slip is not used to switch the mode.

**Frequency, terminals, load and run-up** (round 7, 2026-11-26).

| Element | New variables (parameter) | Relations [source] |
|---|---|---|
| E-motor block, drivetrain e-machine | panel *f* (`frequency`), pole count 2*p* (`poles`, default 4), *n*_s (`syncSpeed`); engineer *s* (`slip`) | e-motor block: *n*_s = 60·*f*/*p*, *s* = (*n*_s − *n*)/*n*_s [S15]; drivetrain e-machine by `kind`: PMSM and EESM *n* = *n*_s (`mach.speed.synchronous`), ASM with slip, SRM without a mains-frequency relation |
| Machines, e-motor block, drivetrain e-machine | panel *U* (`voltage`, line-to-line), *I* (`current`); engineer cos φ (`powerFactor`) | three phases (default): *P*_el = √3·*U*·*I*·cos φ (`mach.power.el3`) [S15]; one phase (`phases` = 1): *P*_el = *U*·*I*·cos φ (`mach.power.el1`) [S7]; other phase counts: none |
| Radial- and axial-flux machine | as above, plus the synchronous lock | magnet and wound-field rotors (`spm`, `ipm`, `spoke`, `eesm`; the axial-flux machine): *n* = *n*_s [S15] |
| Machines, e-motor block, drivetrain e-machine | panel *J*_M (`rotorInertia`), *J*_L (`loadInertia`, from the drive chain or entered), *T*_M mean motor torque during the run-up (`runUpTorque`), *t*_a run-up time (`runUpTime`); engineer *J* (`inertia`) | *J* = *J*_M + *J*_L (`mach.inertia.total`); *t*_a = *J*·2π·*n*/(*T*_M − *T*) (`mach.runup`, constant torques, from standstill to *n*; the load torque is the shaft torque *T*) [S15] |
| Gearbox, differential | panel *J*₂ (`inertiaOut`), *J*₁ (`inertiaIn`) | *J*₁ = *J*₂/*i*² (`mach.gearbox.inertia`, losses and own inertia neglected) [S18] |
| Wheel | panel *m* (`mass`, vehicle mass driven through the wheel or axle); engineer *J* (`inertia`) | *J* = *m*·*r*² (`mach.wheel.inertia`) [S18] |
| Driven load `machines.drive.load` (new; block and flywheel symbol) | *n*, *T* load torque, *P*, *J* (`inertia`) | *P* = 2π·*n*·*T*; mechanical input at the left |

- *Inertia along the chain*: the connector that passes *n* and *T* downstream also passes the load inertia upstream: the
  load's *J* (or the vehicle mass at the wheel) becomes the gearbox's *J*₂, its *J*₁ = *J*₂/*i*² the motor's *J*_L. Example:
  load 2.5 kg·m² and 100 N·m behind a gearbox *i* = 5 → *J*_L = 0.1 kg·m², *T* = 20 N·m; with *J*_M = 0.02 kg·m²,
  1500 min⁻¹ and *T*_M = 50 N·m the run-up takes 0.63 s (unit test).
- *Check* `mach.runup.torque` (warning, [S15]): *T*_M ≤ *T* – the drive cannot accelerate the load.
- *Joins across modules*: the terminal variables use the shared keys *U*, *I*, *f* and *P*_el (`NetKeys`), so a feeder
  of another module that reports these values at the motor's terminals meets them without a code dependency. The
  switchgear frequency inverter does not report an output frequency yet, so "inverter frequency → motor speed" needs a
  value port on the switchgear side (open).
- Library: "Driven load (block)", "Flywheel load on a shaft (symbol)", "E-motor with run-up time" (1450 min⁻¹, 20 N·m
  load, 60 N·m during the run-up, *J* = 0.01 + 0.09 kg·m² → *t*_a = 0.38 s; the values label shows *t*_a).

Not yet: slot/pole numbers, DC machine constants, efficiency classes (IE codes need the limit table of IEC 60034-30-1,
which is not reproduced), duty types S1–S10, battery, inverter and DC link values (Calc 4 with the energy module).

## Known limits

- The motor symbol item (`machines.motor.symbol`, the `schematic` view of `machines.radial`) and the electrical
  three-phase motor (`elec.motor.3ph`) draw the same circuit symbol but are kept as two items: the machines symbol is
  the machine itself (shaft hub that couples to drive trains and pumps, machine values *n*, *T*, *P*, *f*, slip, η, 1–6
  phases, motor/generator, switches to the cross-section views) and has no electrical pins; the electrical one is a
  circuit component with U/V/W pins in a wired network and leaves the machine values to this module.

- Drivetrain parts only translate with their shaft (the core coupling moves parts, it does not turn them): after turning
  a shaft, turn the parts on it as well (or turn them together).
- Illustrative geometry only (see above); no winding schemes, no slot/pole combination checks.
- Outer-rotor components are not size-matched by default (scale the rotor ring to fit around the stator).
- Hosts: all output consists of lines, polylines/polygons, rectangles, ellipses and text; hatches and copper cross hatches
  are explicit hatch lines where a host has no pattern fill (see the host capability matrix).
