# Kinematics module (`kinematics`)

German translation: [../../de/modules/kinematics.md](../../de/modules/kinematics.md)

The kinematics module provides symbols for kinematic diagrams (mechanism sketches) and predefined planar
mechanisms for quick technical sketches and patent figures. The symbol shapes follow the *style* of
ISO 3952 (kinematic diagrams – graphical symbols); they are drawn independently and are not copies of the
standard's figures. No claim of standards compliance is made.

**Scope and limits.** This is a *kinematic sketch*, not a dynamics simulation: positions are computed by
closed-form loop closure at one input angle. There are no masses, forces, velocities, time integration,
collision checks or tolerance analysis. The input force arrow of the toggle mechanism and the drive arrow of the
crank are symbols only.

## Conventions

- Units are millimetres, y points down, angles are degrees **clockwise on screen** (project convention).
  For the four-bar linkage, `angle = -60` therefore means the crank points up and to the right at 60°.
- Every object can be inserted without parameters. Free uniform and non-uniform scaling changes only `Size`.
- Box symbols (joints, bodies) are stretched to their box. **Mechanisms are fitted with one uniform scale** into
  their box (centred), so link-length ratios stay exact under non-uniform scaling. The framing uses the envelope
  of the full motion range, so the drawing does not jump when the angle changes.
- Moving links use visible lines (ISO 128 wide line), ghost positions use the phantom line role
  (alternative positions of movable parts), motion traces are thin dashed lines.
- Joint circles are white filled so link ends disappear underneath.

## Library categories and object types

### Joints & supports (`kin.joints`)

| Type id | Name (EN / DE) | Views | Anchors |
|---|---|---|---|
| `kin.joint.revolute` | Revolute joint / Drehgelenk | schematic (circle), detailed (eye and pin) | Port (centre) |
| `kin.joint.ground` | Fixed pivot (ground) / Gestelldrehpunkt (Festlager) | schematic (triangle, hatched base), detailed (bearing block) | Port (pivot), Mount (base) |
| `kin.joint.prismatic` | Prismatic joint / Schubgelenk | schematic (block on hatched guide), detailed (two rails) | Port (pin), Axis (guide axis), Mount |
| `kin.joint.cam` | Cam joint (higher pair) / Kurvengelenk | schematic, detailed (hatched bodies) | Port (contact point) |
| `kin.joint.spherical` | Spherical joint / Kugelgelenk | schematic, detailed (ball and socket) | Port (centre) |
| `kin.joint.gear` | Gear contact / Zahneingriff | schematic (touching circles), detailed (teeth indicated) | Port + Hub (centres), Pitch circles |

### Links & bodies (`kin.links`)

| Type id | Name (EN / DE) | Geometry | Parameters (all optional) | Anchors |
|---|---|---|---|---|
| `kin.link.binary` | Rigid link / Starres Glied | 2 points | `startJoint`, `endJoint`, `joint` (circle radius) | Port at both ends |
| `kin.link.ternary` | Ternary link / Dreigelenkglied | 3 points | `joint` | Port at all three joints |
| `kin.link.crank` | Crank / Kurbel | box | – | Port (pivot, pin), Mount |
| `kin.link.rocker` | Rocker / Schwinge | box | – | Port (pivot, pin), Mount |
| `kin.link.slider` | Slider block / Gleitstein | box | – | Port + Hub (centre, snaps onto guide axes) |
| `kin.link.piston` | Piston and cylinder / Kolben und Zylinder | box | `stroke` (0..1) | Port (both eyes), Mount, Axis |
| `kin.cam.follower` | Cam disc with follower / Kurvenscheibe mit Stößel | box | `angle`, `follower` (roller/flat/knife), `base`, `lift`, `dof` | Mount + Hub (cam pivot), Port (rod end), Axis (follower guide) |
| `kin.spring` | Spring / Feder | 2 points | `coils`, `width` | Port at both ends |
| `kin.damper` | Damper / Dämpfer | 2 points | `width` | Port at both ends |
| `kin.rope.pulley` | Rope over pulley / Seil über Rolle | box | `load` | Hub + Port (pulley), Port (rope ends), Mount (ceiling) |
| `kin.gripper` | Gripper jaws / Greiferbacken | box | `open` (0..1) | Mount + Port (flange), Port (grip point) |

All link types offer the views `schematic` and `detailed` (bar outline, plate, coil spring, damper cylinder,
section, …). The cam follower contact is computed from the actual cam contour (tangent cam: base circle plus
nose circle): a roller rests tangent on the contour, a flat face on the highest point, a knife edge on the
crossing with the follower axis.

### Mechanisms (`kin.mechanisms`)

Each mechanism is one box object with the views `schematic` (stick diagram) and `detailed` (bar outlines,
bearing blocks, hatched sliders).

| Type id | Name (EN / DE) | Link-length parameters (defaults) | Input `angle` | DOF |
|---|---|---|---|---|
| `kin.mech.fourbar` | Four-bar linkage / Viergelenkgetriebe | `ground` 40, `crank` 12, `coupler` 36, `rocker` 30, `branch` open/crossed, `cpu`/`cpv` coupler point | crank angle, default −60° | 1 |
| `kin.mech.slidercrank` | Slider-crank / Schubkurbel | `crank` 12, `rod` 36, `offset` 0 | crank angle, default −60° | 1 |
| `kin.mech.scotchyoke` | Scotch yoke / Kreuzschubkurbel | `crank` 12 | crank angle, default −60° | 1 |
| `kin.mech.toggle` | Toggle mechanism / Kniehebel | `link1` 26, `link2` 26, `force` | angle of the upper link from the guide axis (0° = stretched), default 30° | 1 |
| `kin.mech.quickreturn` | Quick-return mechanism / Schnellrücklaufgetriebe | `ground` 20, `crank` 10, `lever` 40, `link` 16, `ram` 44 | crank angle, default −30° | 1 |

Common parameters: `angle`, `ghosts` (0–12), `path` (motion path), `dof` (degree-of-freedom badge).

**Loop closure.** Four-bar: the coupler/rocker joint B is the intersection of the circles around the crank pin A
(radius coupler) and the rocker pivot O4 (radius rocker); the assembly mode (`open`/`crossed`) selects the
solution side and is kept over the whole revolution. Slider-crank: B = (A.x + √(rod² − (offset − A.y)²), offset).
Toggle: C lies on the guide axis through the fixed pivot. Quick-return (crank and slotted lever with ram link):
the crank pin slides on the lever O4–T, the link T–B drives the ram slider on a horizontal guide. The public
`Linkages` class exposes the solvers (`FourBar`, `SliderCrank`, `CircleCircle`, `Grubler`, `IsGrashof`).

**No assembly possible.** If the loop cannot be closed at the given angle (e.g. a non-Grashof chain or a crank
longer than the rod), the frame and the input link are drawn, and an italic note "no assembly possible" /
"keine Montage möglich" is shown at the bottom of the box. Ghost positions and path samples that cannot be
assembled are skipped; the path is split at such gaps.

**Ghost positions.** `ghosts = N` draws N further positions with phantom lines (each one a group tagged `ghost`):
evenly spread over a full turn for crank-driven mechanisms, and between the current angle and the stretched
position for the toggle mechanism.

**Motion path.** `path = true` traces the coupler point (four-bar; set `cpv` ≠ 0 for an off-bar coupler point,
drawn as a coupler triangle), the connecting-rod midpoint (slider-crank), the crank pin (scotch yoke,
quick-return) or the knee (toggle).

**Snapping anchors.** Ground pivots are `Mount` anchors; all joint centres (current position) are `Port`
anchors, so link ends, revolute joints and springs snap joint to joint; slider guides are `Axis` segments, so
slider blocks snap onto them.

### Motion annotations (`kin.motion`)

| Type id | Name (EN / DE) | Geometry | Parameters |
|---|---|---|---|
| `kin.motion.path` | Motion path / Bahnkurve | points (≥ 2) | `smooth`, `arrow`, `closed` |
| `kin.motion.anglelimit` | Angle limits / Winkelbegrenzung | box | `min` (−30°), `max` (45°), `labels` (values/symbols/none), `joint` |
| `kin.motion.dof` | Degree-of-freedom badge / Freiheitsgrad-Anzeige | box | `links`, `lower`, `higher`, `formula` |

The DOF badge shows the Grübler/Kutzbach mobility F = 3(n − 1) − 2·j1 − j2 (n links including the frame,
j1 lower pairs, j2 higher pairs). Text: "DOF = 1" (English) or "F = 1" (German). The angle-limit annotation
has a `Port` at its centre so it snaps onto a joint.

## Context actions

| Action id | Label (EN / DE) | Icon | Applies to |
|---|---|---|---|
| `kin.angle.next` / `kin.angle.prev` | Next / previous position (±15°) / Nächste / vorherige Stellung | `cmd-rotate` | mechanisms, cam follower |
| `kin.ghosts` | Ghost positions / Zwischenlagen (toggles 0 ↔ last count, default 4) | `cmd-animate` | mechanisms |
| `kin.path` | Motion path / Bahnkurve | `cmd-toggle` | mechanisms |
| `kin.dof` | Degree of freedom badge / Freiheitsgrad-Anzeige | `cmd-text` | mechanisms, cam follower |
| `kin.branch` | Crossed assembly mode / Gekreuzte Montagelage | `cmd-swap` | four-bar |
| `kin.follower` | Next follower type / Nächste Stößelform | `cmd-cycle` | cam follower |
| `kin.joint.start` / `kin.joint.end` | Joint at start / end | `cmd-arrow-start` / `cmd-arrow-end` | rigid link |
| `kin.stroke`, `kin.open`, `kin.load` | Extend/retract, open/close, load | `cmd-toggle` | piston, gripper, pulley |
| `kin.path.*`, `kin.labels`, `kin.limit.joint`, `kin.dof.formula` | path, label and badge options | various | motion annotations |

View switching (`schematic`, `detailed`) is offered by the host from `ViewDef` with the icons
`view-schematic` and `view-detailed`.

## Host notes

All output uses native primitives (lines, polylines, circles, rectangles, text, groups); hatching is explicit
line segments where hosts lack pattern fills. Ghost positions are `GroupPrim`s. No vector-picture fallback is
needed.

Mechanisms provide `AnimationFrames(o, count)`: the current state, then the input angle stepped like the ghost
positions (a full turn, or the toggle range). Hosts turn them into a slide sequence (PowerPoint COM: "Animate as
slides" with Morph transitions); every frame keeps the box.
