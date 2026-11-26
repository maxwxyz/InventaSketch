# Robotics module (`robotics`)

German translation: [../../de/modules/robotics.md](../../de/modules/robotics.md)

Robots, grippers, coordinate frames, work cells and safety devices for slides and quick technical sketches. The
predefined robots are posed by **forward kinematics** from their joint values and drawn in **side and top view**, so the
drawing is geometrically correct for any angles. Reach envelopes are computed from the link lengths and joint limits.

**Scope and limits.** This is a sketching module, not a robot simulation, an offline programming tool or a safety
layout tool: there are no dynamics, no collision checks, no inverse kinematics for the arms, no safety distance
calculation. Default proportions are plausible for the robot class, not taken from a manufacturer's datasheet. Symbols
(grippers, light curtain, scanner fields, emergency stop, fence) are drawn independently following common drawing
practice; no standard is reproduced and **no standard compliance is claimed**. Zone names ("restricted space",
"collaborative workspace") are plain descriptive labels.

The module depends only on `InventaSketch.Core`. It draws its own arm geometry (it does not use the kinematics module)
but follows the same pattern as the kinematics mechanisms: one moved joint for steps, ghost positions and
`AnimationFrames`.

## Conventions

- Units mm, page y down, angles in degrees **clockwise on screen** in the view in which the joint axis is perpendicular
  to the drawing (J1 in the top view, J2/J3/J5 in the side view at J1 = 0). Roll joints (J4, J6 of the articulated
  arm, J6 of the cobot) are right-handed about the axis pointing away from the robot, i.e. clockwise when looking from
  the robot towards the tool.
- World frame of the robots: x forward (to the right in both views), y away from the side-view observer, z up. The side
  view looks along +y (page = (x, −z)), the top view looks down along −z (page = (x, −y)).
- **Quick Sketch first:** every element is insertable without parameters; free (also non-uniform) scaling changes
  `Size`, never parameters. Robots are fitted into their box with **one uniform scale** (link ratios stay exact);
  symbols are stretched with their box.
- **Framing of robots:** the box shows the current pose and every position of the *moved joint* (parameter `motion`)
  over its limits, with the other joints as they are. So ghost positions and all animation frames share one framing,
  and the base never moves while the moved joint turns. When anything else changes (another joint, a link length, a
  limit, the moved joint, the envelope), the **box follows the robot**: the drawing scale and the page position of the
  base are kept and the box size adapts (`OnParamChanged`, and the actions "Home position", the joint choice,
  "Reach envelope"). A view switch keeps the drawing scale as well.
- Joint values are **clamped to the joint limits** before drawing.
- Line roles: bodies are medium outlines (0.35 mm) with a light fill (`#eef1f5`, darker grey for bases and motors) so a
  user fill recolours the robot; schematic links are wide lines; ghost positions use the phantom line; axes are
  centre lines.

## Forward kinematics

All chains are built from elementary transforms, `T_i = T_(i−1) · Trans(offset) · Rot(axis, θ)` (rotation right-handed
about the local axis, `Frame3.Move` / `Frame3.Turn`), and projected orthographically. Bodies are convex solids drawn by
their exact silhouette (capsule = sphere-swept segment; cylinders, frustums and boxes = convex hull of the projected
points), painted back to front.

### Articulated robot (6-axis), `robot.arm.articulated`

Parameters (mm): `base` d1 = 500 (shoulder height), `offset` a1 = 150, `upper` a2 = 650, `fore` d4 = 700 (elbow to
wrist centre), `wrist` d6 = 120 (wrist centre to flange); the elbow offset c3 = 0.15·a2 (forearm axis above the elbow
axis) is fixed. Zero pose: upper arm vertical, forearm horizontal forward.

```
F0 = Rot(−z, j1)
S  = F0 · (a1, 0, d1)                         shoulder (J2 axis = F0·y)
F2 = [F0 at S] · Rot(y, j2);  E = F2 · (0, 0, a2)     elbow
F3 = [F2 at E] · Rot(y, j3);  W = F3 · (d4, 0, c3)    wrist centre
F5 = [F3 at W] · Rot(x, j4) · Rot(y, j5)
flange = F5 · (d6, 0, 0),  tool axis = F5·x,  flange index mark turns with Rot(x, j6)
```

Checks (unit tests): all zero → flange (a1 + d4 + d6, 0, d1 + a2 + c3); j2 = 90 → (a1 + a2 + c3, 0, d1 − d4 − d6);
j1 = 90 → (0, −(a1 + d4 + d6), …), i.e. clockwise in the top view; j5 = 90 → tool straight down; j4 = j5 = 90 → the wrist
bends towards +y.

### Collaborative robot, `robot.arm.cobot`

`base` d1 = 180, `upper` a2 = 425, `fore` a3 = 390, `wrist` d5 = 110, `flange` d6 = 100; the upper arm runs offset by
0.32·a2 beside the joint line (housings side by side, visible in the top view). Chain: `Rot(−z, j1)`, shoulder at
(0, 0, d1), `Rot(y, j2)`, elbow at +a2 (z), `Rot(y, j3)`, wrist 1 at +a3 (x), `Rot(y, j4)`, wrist 2 at +d5 (x),
`Rot(z, j5)`, flange at +d6 (x), `Rot(x, j6)`. Zero pose: flange at (a3 + d5 + d6, 0, d1 + a2).

### SCARA, `robot.arm.scara`

`height` h = 400, `inner` a1 = 325, `outer` a2 = 275, `stroke` = 200. j1, j2 turn about −z (clockwise in the top view),
j3 is the quill stroke (mm, downwards, 0 … stroke), j4 the tool rotation. Flange =
(a1·cos j1 + a2·cos(j1 + j2), −(a1·sin j1 + a2·sin(j1 + j2)), h − 130·u − j3), u = (a1 + a2)/600.

### Delta robot, `robot.arm.delta`

`height` H = 1000 (base plane), `baseRadius` R = 200, `upper` L = 300, `fore` l = 750, `platform` r = 60. Arm i sits at
φ_i = 0°, 120°, 240° (u_i = (cos φ_i, sin φ_i, 0)); θ_i > 0 swings the arm down (−40° … 90°). Elbows
E_i = R·u_i + (0, 0, H) + L·(cos θ_i·u_i − sin θ_i·z). The platform centre P satisfies |P + r·u_i − E_i| = l for all
three arms: it is the lower intersection of three spheres of radius l around C_i = E_i − r·u_i (trilateration). If the
spheres do not meet, the robot draws its base and upper arms and the note "pose not reachable" / "Pose nicht
erreichbar". The tool flange is 60·u below P.

### Cartesian / gantry robot, `robot.arm.gantry`

Axes `x`, `y`, `z` in mm from the start of each stroke (`strokeX` 1200, `strokeY` 800, `strokeZ` 500, `height` 1500);
z moves the ram down. The flange moves exactly by (Δx, Δy, −Δz).

## Reach envelopes

A planar two-link chain (inner link L1, outer link L2, shoulder angle θ, elbow angle φ from the stretched position)
reaches distance `ρ(φ) = √(L1² + L2² + 2·L1·L2·cos φ)` in the direction `α = θ + β(φ)`,
`β(φ) = atan2(L2·sin φ, L1 + L2·cos φ)`. For each |φ| (sampled), the reachable directions are
`[θmin + β, θmax + β]` for every sign of φ inside the elbow range; the area is drawn ring by ring (lo edge, outer arc, hi
edge, inner arc). Rings reached all round are closed with a hairline seam opposite the reachable middle, so the area is
**one fill contour** in every host (holes cannot be expressed by a single PowerPoint freeform); the outline omits the
seam. If both elbow signs are allowed and their direction intervals do not overlap (very narrow shoulder ranges), the
hull of both intervals is drawn – an over-estimate.

| Robot | Side view | Top view |
|---|---|---|
| Articulated | wrist-centre working range in the arm plane (as datasheet diagrams show it): L1 = a2, L2 = √(d4² + c3²), θ = j2 − 90° (j2 in [`j2min`, `j2max`]), φ = j3 + 90° − atan2(c3, d4) (j3 in [`j3min`, `j3max`]); drawn on the side the arm points to | annulus or sector R_in … R_out over ±`j1lim`, R from the horizontal extent of the side area (R_in = 0 if it crosses the axis) |
| Cobot | wrist (J4 axis) range: L1 = a2, L2 = a3, θ = −90° + j2, φ = 90° + j3 (both elbow directions) | annulus/sector over ±`j1lim` (default 360°: full) |
| SCARA | rectangle R_in … R_out × the quill stroke, on the arm side | classic two-link area: L1 = a1, L2 = a2, θ ∈ ±`j1lim`, φ ∈ ±`j2lim` |
| Delta | **convex hull** of flange positions sampled over the arm limits (11³ samples) – an approximation | same, projected |
| Gantry | stroke box of the flange (exact) | stroke rectangle (exact) |

Defaults: articulated `j1lim` 170°, J2 −70 … 110°, J3 −80 … 70°; cobot ±360°, J2 ±110°, J3 ±160°; SCARA ±132°/±145°.
The envelope is shown with the parameter `envelope` (tinted area with a blue outline) or as the stand-alone element
`robot.reach.envelope` (articulated-arm parameters, fill tint/hatch/none, radius labels "R …" in mm).

**Along a linear axis** (`travel` > 0 on `robot.reach.envelope`): the range swept when the base moves by 0 … travel along
x, drawn by its outer limit. Top view: convex hull of the area at both ends of the travel (exact for the convex sector of
the default limits; a notch behind the robot is closed). Side view: row by row from the leftmost to the rightmost
reachable point plus the travel (inner unreachable pockets are filled). The base's travel is drawn as a centre line.

## Values and checks

Engineering-calculation values (optional; entered as text with units, German or English number format):

| Element | Variable | Meaning |
|---|---|---|
| tools, tool changer, force-torque sensor, vision sensor, workpiece | `mass` m (kg) | mass of the part |
| robots | `payload` m_max (kg) | rated payload from the datasheet (no default) |
| robots | `load` m_L (kg) | sum of the masses entered on the tool chain docked at the flange (flange → changer → sensor → gripper …) and on the workpiece whose port `grip` sits on the chain's TCP; parts without a mass are left out |
| robots | `reach` R (mm) | from the geometry: the largest distance of the top-view reach envelope from the base axis (wrist centre for the articulated arm, J4 axis for the cobot, flange for SCARA and delta); none for the gantry; an entered value wins |

Checks (warnings, bilingual messages):

- `robot.payload` – m_L > m_max (only when both are known). The datasheet's load diagram also limits the
  centre-of-gravity offset; that is not checked.
- `robot.reach` – a workpiece (port `grip`) or TCP marker on the same page and in the same view, assigned to the nearest
  robot by base distance, lies outside that robot's reach envelope as drawn by more than the wrist length (articulated:
  d6; cobot: d5 + d6), the docked tool length (flange to the chain's TCP) and 1 % of the envelope size. A sketch-level
  plausibility check, not a reachability analysis (orientation, collisions and singularities are ignored).

## Library categories and object types

Robots have the views `side` (`IconIds.Side`) and `top` (`IconIds.Top`); SCARA opens in the top view. Tools have `side`
and `axial` (along the tool axis, `IconIds.Axial`). Cell and safety symbols have `top` and `side`. Coordinate frame and
TCP marker have one drawing; fence and zone are point (polyline) elements.

| Id | Name (EN / DE) | Views | Anchors | Parameters |
|---|---|---|---|---|
| `robot.arm.articulated` | Articulated robot (6-axis) / Knickarmroboter (6-achsig) | side, top | mount `base`, mount `flange`, port `tcp` | `j1`…`j6` (°), `base`, `offset`, `upper`, `fore`, `wrist`, `style`, `envelope`, `labels`, `ghosts`, `motion` (J5), limits `j1lim`, `j2min/max`, `j3min/max` |
| `robot.arm.cobot` | Collaborative robot (cobot) / Kollaborativer Roboter (Cobot) | side, top | same | `j1`…`j6`, `base`, `upper`, `fore`, `wrist`, `flange`, … `motion` (J4), limits |
| `robot.arm.scara` | SCARA robot / SCARA-Roboter | top, side | same | `j1`, `j2` (°), `j3` (mm), `j4`, `height`, `inner`, `outer`, `stroke`, … `motion` (J2), `j1lim`, `j2lim` |
| `robot.arm.delta` | Delta robot (parallel) / Delta-Roboter (Parallelkinematik) | side, top | same (base on top) | `j1`…`j3`, `height`, `baseRadius`, `upper`, `fore`, `platform`, … `motion` (J1) |
| `robot.arm.gantry` | Cartesian / gantry robot / Linear- / Portalroboter | side, top | same | `x`, `y`, `z` (mm), `strokeX/Y/Z`, `height`, … `motion` (X) |
| `robot.mobile.agv` | Mobile robot (AGV / AMR) / Mobiler Roboter (FTF / AMR) | top, side | mounts `deck` (+ carrier mount), `scanner1`, `scanner2` | `arrow` |
| `robot.track.linear` | Linear axis (7th axis) / Linearachse (7. Achse) | side, top | mount `carriage` (+ carrier mount), port `chain` on the carriage, rail `rail` and mount `floor` (side view) | `pos` (0 … 1, 0.25), `ends` |
| `robot.gripper.parallel` | Parallel gripper / Parallelgreifer | side, axial | mount `flange`, port `tcp` | `open` (0…1), `arrows` |
| `robot.gripper.vacuum` | Vacuum gripper / Vakuumgreifer (Sauggreifer) | side, axial | same | `cups` (1–4) |
| `robot.gripper.magnetic` | Magnetic gripper / Magnetgreifer | side, axial | same | – |
| `robot.gripper.threefinger` | Three-finger gripper / Dreifingergreifer | side, axial | same | `open` |
| `robot.tool.torch` | Welding torch / Schweißbrenner | side, axial | mount `flange`, port `tcp` at the wire tip | – |
| `robot.tool.spindle` | Spindle (milling / drilling) / Spindel (Fräsen / Bohren) | side, axial | mount `flange`, port `tcp` | `arrow` |
| `robot.tool.weldgun` | Welding gun / torch / Schweißzange / Schweißbrenner | side, axial | mount `flange`, port `tcp` between the electrode tips (spot) or at the wire tip (arc) | `kind` spot/arc |
| `robot.tool.dispenser` | Adhesive dispenser / Klebedüse (Dosierventil) | side, axial | mount `flange`, port `tcp` at the nozzle tip | `bead` |
| `robot.tool.changer` | Tool changer / Werkzeugwechsler | side, axial | mount `flange` (robot side, top), mount `tool` (tool side, bottom) | `utility` (media module) |
| `robot.sensor.ft` | Force-torque sensor / Kraft-Momenten-Sensor | side, axial | mount `flange` (top), mount `tool` (bottom) | `label` (F/T) |
| `robot.sensor.vision` | Vision sensor (camera) / Bildverarbeitungssensor (Kamera) | side, axial | mount `flange`, port `view` at the end of the field of view | `angle` (50°), `cone` |
| `robot.frame.coordinate` | Coordinate frame / Koordinatensystem | – | port `origin` | `kind` world/base/tool/user/object, `axes` xz/xy/yz, `name` |
| `robot.frame.tcp` | Tool centre point (TCP) / Werkzeugarbeitspunkt (TCP) | – | port `tcp` | `name` |
| `robot.reach.envelope` | Reach envelope (working range) / Arbeitsraum (Reichweite) | side, top | mount `base` | articulated geometry and limits, `fill`, `labels`, `travel` (mm, 0) |
| `robot.cell.workpiece` | Workpiece / Werkstück | top, side | port `grip` (+ `bottom` in side view) | `shape` box/cylinder |
| `robot.cell.conveyor` | Belt conveyor / Gurtförderer (Förderband) | top, side | ports `in`, `out`, rail `belt` (+ mount `floor`) | `dir`, `arrow`, `legs` |
| `robot.cell.pallet` | Pallet / Palette | top, side | port `load` (+ rail `deck`, mount `floor`) | – |
| `robot.cell.pedestal` | Robot pedestal / Robotersockel | side, top | mount `top` (+ `floor`) | – |
| `robot.cable.dragchain` | Energy chain (drag chain) / Energiekette (Schleppkette) | points | its ends (dock like line ends) | `radius` (4), `width` (2.4), `pitch` (2.4) (page mm) |
| `robot.safety.fence` | Safety fence / Schutzzaun | points | rails along every segment | `closed`, `panel` (page mm), `mesh` |
| `robot.safety.door` | Safety fence door / Schutztür (Schutzzaun) | top, side | ports `hinge`, `latch` | `kind` hinged/sliding |
| `robot.safety.curtain` | Safety light curtain / Sicherheits-Lichtvorhang | top, side | ports `emitter`, `receiver` | `beams`, `field` |
| `robot.safety.scanner` | Safety laser scanner / Sicherheits-Laserscanner | top, side | mount `body` | `shape` sector/rect, `angle` (270°), `ratio` (0.55), `labels` |
| `robot.safety.zone` | Safety zone / Sicherheitsbereich | points (closed) | edge midpoints | `kind` hazard/protective/warning/restricted/collaborative, `fill`, `label` |
| `robot.safety.estop` | Emergency stop button / Not-Halt-Taster | top, side | mount `mount` | – |

Library (`robot.robots`, `robot.tools`, `robot.frames`, `robot.cell`, `robot.safety`): one item per type (id = type id)
plus every robot in its second view (`<id>.top` / `<id>.side`), articulated with envelope (side and top), articulated and
cobot as kinematic schematic with labels, SCARA with working range, closed parallel gripper, vacuum gripper with 4 cups,
cylindrical workpiece, closed fence, sliding door, scanner with rectangular fields, zones (warning, protective,
restricted, collaborative), tool and world frames, envelope in top view, dispenser with bead, linear axis in top view, envelope
swept along a linear axis (top view, `travel` 2000 mm). Keywords in both languages (roboter, knickarm,
greifer, sauggreifer, schutzzaun, lichtvorhang, arbeitsraum, reichweite, ftf, not-halt, linearachse, werkzeugwechsler,
kraft-momenten-sensor, kamera, schweißzange, klebedüse, energiekette, …).

## Anchors (docking)

- Robot base: **mount** `base` (foot on the floor; on top of the delta's base plate). It snaps onto the mount `top` of
  the pedestal, the mount `deck` of the mobile robot (mobile manipulator, e.g. a cobot on an AGV) or the mount
  `carriage` of the linear axis (see "Robots on carriers" below).
- Robot flange: **mount** `flange` – a gripper's or tool's own mount `flange` snaps there (mount onto mount); and
  **port** `tcp` at the same point for TCP markers and frames (port onto port). Since 2026-11-25 the flange mount is
  **directed** along the tool axis (`Anchor.Directed`, where the axis shows in the view) and the tools' mounts face
  back to the robot: the flange offers a "+" (build port `mount.flange`) with the grippers and tools, and the chosen one
  is placed on the flange **turned to the tool axis** (checked in PowerPoint, scenario `robot-tool`). Dragging a tool
  onto the flange snaps the position only. The tool is scaled so its flange plate matches the robot's flange (both faces
  give their radius), and a tool docked on the flange **follows** when a joint turns or the robot moves
  (`Coupling.Propagate` turns and moves parts docked on a directed mount; checked in PowerPoint: J5 + 15° turns the
  gripper from −30° to −15° on the flange).
- **Tool chain** (since 2026-11-26): tool changers and force-torque sensors sit between flange and tool. Their mount
  `flange` (top, directed to the robot) docks on the robot flange or on another part's tool side; their mount `tool`
  (bottom, directed to the tool) takes the next part. Each offers a "+" (build port `mount.tool`) with grippers, tools,
  sensors and changers, so arm → tool changer → force-torque sensor → gripper is built with three clicks, every part
  turned to the tool axis and scaled to the robot's flange (all flange plates span the same design width). The whole
  chain follows when a joint turns (each part's directed mount sits on the previous one, facing it). Core matches build
  ports by mount id only, so a part offers its flange also as `tool` while it is a build-port candidate (not in the
  document, or probed in a temporary copy: centred on the page origin or with a flange/tool mount of another part well
  inside its box); placed parts keep unique mount ids, as `Coupling` finds docked parts by id.
- Tools: port `tcp` at the gripping point / wire tip / tool tip; the TCP marker's port `tcp` snaps there, and so does a
  workpiece's port `grip` (the workpiece counts as held: its mass is part of the robot's load).
- **Robots on carriers:** floor-standing robots (articulated, cobot, SCARA) have, besides the mount `base`, a directed
  mount without an id at the base (facing down); the mobile robot's deck and the linear axis's carriage have a matching
  one (facing up). A robot whose base snaps onto the deck or the carriage (mount onto mount) therefore rides along when
  the mobile robot drives or turns or the carriage moves (`pos`, also by its grip), and – as with tools on the flange –
  the carrier follows when the docked robot is moved. Without an id these mounts offer no "+".
- Linear axis: port `chain` on the carriage and rail `rail` along the rail top for the ends of an energy chain; a docked
  chain end follows the carriage (attachment with the anchor id).
- Fence: rails along every segment; door ports `hinge`/`latch` and light-curtain ports dock anywhere on them.
- Conveyor: rail `belt` along the belt surface (workpiece ports dock anywhere on it).

## Context actions

Robots: `J… +15°` / `J… −15°` (50 mm for linear axes) on the moved joint, joint ▾ (choice of the moved joint),
"Home position", "Ghost positions" (0 ↔ 4, over the moved joint's range), "Reach envelope", drawing style body ↔
kinematic schematic (option pictograms detailed/schematic), "Joint labels" (J1… / A1… by UI language, coinciding joints
share a label "J5/J6"). Grippers: open/close, stroke arrows, more/fewer cups. Tool changer: media module; force-torque
sensor: label F/T; vision sensor: field of view; welding gun: process ▾ (spot welding gun / arc welding torch);
dispenser: adhesive bead; linear axis: end positions (phantom lines). Others: kind ▾ / shape ▾ / axes ▾ (choices), toggles for
names, arrows, mesh band, field tint, closed fence, reverse conveyor.

## Grips (drag handles)

`DragHandles` / `MoveHandle` (hosts draw them as blue grips; interaction-ux.md section 4):

| Element | Key | Motion | Parameter | Snap |
|---|---|---|---|---|
| Robots: revolute joint whose axis faces the viewer (side view: shoulder, elbow, wrist bend; top view: base axis, SCARA arms) | `joint.j1` … | free; the joint turns towards the pointer about its axis (on the projected pose, so tilted axes work too) | `j1` … | 1°, wrapped into and clamped to the joint limits |
| Robots: linear axis moving across the view (gantry X/Y/Z, SCARA quill in the side view) | `joint.x`, `joint.j3` … | along the projected axis | `x`, `y`, `z`, `j3` | 5 mm, clamped to the stroke |
| Parallel and three-finger gripper (side and axial view) | `open` | along the jaw stroke | `open` | 5 %, 0 … 1 |
| Linear axis (side and top view) | `pos` | along the rail ("Carriage position: 25 %" / "Schlittenposition: 25 %") | `pos` | 5 %, 0 … 1 |

The grip sits on the first point further down the chain that moves with the joint (next joint, else the flange), so
dragging it feels like taking the arm by that point. Joints whose motion does not show in the view have no grip (base
axis in the side view, forearm and flange roll); coinciding grips (a wrist seen end-on) keep the first joint's.
Labels: "Joint angle J2: 15°" / "Gelenkwinkel A2: 15°", "Axis position X: 600 mm" / "Achsstellung X: 600 mm",
"Gripper opening: 60 %" / "Greiferöffnung: 60 %". A joint drag keeps the drawing scale and the base on the page
(`KeepScale`), like the joint steps.

## Host notes

All output uses native primitives (polygons, polylines, rectangles, ellipses, cubic paths, text). Envelopes and zones are
single closed contours (holes bridged), so PowerPoint draws them as one freeform; hatching uses the core hatch pattern.
No vector picture is needed. The robots' "box follows the robot" behaviour works through `OnParamChanged` (hosts that
set parameters via `Registry.SetParam`) and the module's own actions. `AnimationFrames` sweeps the moved joint from its
current value to one limit, to the other and back (first frame = the object itself, same box).

## Tests and gallery

`tests/InventaSketch.Modules.Robotics.Tests`: registration and id convention, bilingual names/parameters/actions,
view pictograms, parameters optional, library items resolve, search in both languages; every library item rendered in
every view in both languages inside its box (also stretched, squashed, every choice/boolean/number extreme, random joint
values beyond the limits); scaling changes `Size` only; forward kinematics of every robot at known angles, delta loop
closure for random angles, joint clamping; reach radii from the link lengths (two-link, articulated, SCARA, gantry,
annulus); framing contains every position of the moved joint; scale and base kept on parameter changes and view
switches; animation frames share the box; ghosts are phantom groups; actions; fence posts and zone fills; the tool chain
arm → tool changer → force-torque sensor → gripper built with "+", unique mount ids, the chain following a joint, the
chain and held workpiece found in order; a cobot snapping onto and riding on the mobile robot, a robot riding on the
linear axis's carriage, an energy chain end following the carriage, the energy chain's bend radius, the swept envelope;
load sum, payload and reach checks, reach from the geometry or entered, nearest-robot assignment, JSON round trip of
masses and carriage position (`ToolChainTests`, `ValueTests`). The gallery
test writes `gallery/*.html` (every type in each view, stretched, schematic/envelope/ghost variants, presets, pose
sequences, animation frames and a sample work cell).
