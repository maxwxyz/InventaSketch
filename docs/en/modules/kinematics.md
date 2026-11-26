# Kinematics module (`kinematics`)

German translation: [../../de/modules/kinematics.md](../../de/modules/kinematics.md)

The kinematics module provides symbols for kinematic diagrams (mechanism sketches) and predefined planar
mechanisms for quick technical sketches and technical figures. The symbol shapes follow the *style* of
ISO 3952 (kinematic diagrams – graphical symbols); they are drawn independently and are not copies of the
standard's figures. No claim of standards compliance is made.

**Scope and limits.** This is a *kinematic sketch*, not a dynamics simulation: positions are computed by
closed-form loop closure at one input angle. There are no masses, forces, time integration, collision checks
or tolerance analysis; speeds exist only as first-order element values at the current position (see "Element
values"). The input force arrow of the toggle mechanism and the drive arrow of the
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
| `kin.joint.ground` | Fixed pivot (ground) / Gestelldrehpunkt (Festlager) | schematic (triangle, hatched base), detailed (bearing block); with stops two thin stop lines and a dashed swing arc | Port (pivot), Mount (base); optional `limited`, `limitFrom`/`limitTo` (°, clockwise from right, default −135…−45 = upwards): the links on it swing only inside this range |
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
| `kin.cam.follower` | Cam disc with follower / Kurvenscheibe mit Stößel | box | `angle`, `follower` (roller/flat/knife), `base`, `lift`, `dof` | Mount + Hub (cam pivot), Port (rod end), Axis (follower guide); explodes into fixed pivot, cam disc and follower slider |
| `kin.cam.disc` | Cam disc / Kurvenscheibe (part, 2026-11-25) | schematic, detailed (hub, centre lines); box centred on the pivot, uniform | `base`, `lift`, `follower` (how followers touch it: roller with 0.3 × base, flat face, knife edge); turns with its rotation | Hub + Port (pivot); a slider whose guide runs through it and whose joint sits on the contour is its follower (`Cams`): turning the disc in motion mode moves the follower and the chain docked on it |
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
| `kin.mech.geneva` | Geneva drive / Malteserkreuzgetriebe (2026-11-26) | `slots` 4/6, `centre` 30 (O1-O2) | driver angle, default −20° | 1 |
| `kin.ratchet` | Ratchet and pawl / Klinkengesperre (2026-11-26) | `teeth` 12, `spring` (pawl spring shown) | wheel angle, default 0° | 1 |
| `kin.mech.sixbar` | Six-bar linkage / Sechsgliedriges Koppelgetriebe (2026-11-26) | `kind` watt/stephenson, `ground` 40, `crank` 12, `coupler` 36, `rocker` 30; Watt: `arm` 24, `armAngle` 60°, `ground2` 36, `coupler2` 28, `rocker2` 22; Stephenson: `cpu` 0.5, `cpv` −0.3, `link5` 40, `link6` 22, `x6` 62, `y6` −6 | crank angle, default −60° | 1 |
| `kin.mech.pantograph` | Pantograph / Storchschnabel (Pantograph) (2026-11-26) | `length` 40, `scale` 2, `reach` 20, `trace` 5 | position of the tracer on the traced circle, default −90° | 2 |

Common parameters: `angle`, `ghosts` (0–12), `path` (motion path), `dof` (degree-of-freedom badge). The Geneva drive
and the ratchet have no ghosts and no motion path (a second wheel would cover the drawing); they animate like the
others. The four-bar also has `grashof` (Grashof class badge at the top right, e.g. "Crank-rocker" / "Kurbelschwinge").

**Geneva drive.** Driver pivot O1, wheel pivot O2 at the centre distance C; crank radius a = C·sin(180°/z), slot mouth
at R = C·cos(180°/z), so the pin enters and leaves the slots radially. While the pin is in a slot (|φ| ≤ 90° − 180°/z)
the slot follows the pin, otherwise the wheel is locked at 180°/z (one slot pitch per turn of the driver, dwell in
between). The wheel is drawn with parallel slots, concave locking arcs (radius of the locking disc) and short horns;
the driver with its locking disc, relieved towards the pin. `Linkages.GenevaWheelAngle` gives the wheel orientation.

**Ratchet and pawl.** A saw-tooth wheel and a pawl on a second fixed pivot, held on the teeth by a spring (thin zigzag
from a hatched frame point). `angle` turns the wheel; the pawl rests at its first contact with the actual tooth
outline, so in motion it rides up each flank and drops behind the tooth (clockwise is the free direction; turning
back, the steep face runs against the pawl).

**Six-bar.** Watt: two four-bars in series, the rocker of the first is a ternary link O4–B–C (drawn as a plate) that
drives the second four-bar O4–C–D–O6. Stephenson: the coupler A–B–P is a ternary link and the dyad P–C–O6 hangs on its
coupler point P. Both by circle intersections; the motion path traces D (Watt) or P (Stephenson). Six links, seven
revolute pairs: F = 1. "Explode" gives fixed pivots, binary links and one ternary link docked onto each other.

**Pantograph.** Fixed pivot O, long bars O–B and B–P (`length`), short bars C–T and T–D (`length`/k) forming the
parallelogram C–B–D–T. O, the tracer T and the pen P stay on one line with OP = k·OT (`Linkages.Pantograph`). The
tracer runs round a circle (`trace` radius, centre at `reach` from O); with `path` the traced circle and the pen's
enlarged copy are drawn. Five links, five revolute pairs: F = 2 (the badge says so); the input angle only moves the
tracer along the drawn figure.

**Loop closure.** Four-bar: the coupler/rocker joint B is the intersection of the circles around the crank pin A
(radius coupler) and the rocker pivot O4 (radius rocker); the assembly mode (`open`/`crossed`) selects the
solution side and is kept over the whole revolution. Slider-crank: B = (A.x + √(rod² − (offset − A.y)²), offset).
Toggle: C lies on the guide axis through the fixed pivot. Quick-return (crank and slotted lever with ram link):
the crank pin slides on the lever O4–T, the link T–B drives the ram slider on a horizontal guide. The public
`Linkages` class exposes the solvers (`FourBar`, `SliderCrank`, `CircleCircle`, `Grubler`, `IsGrashof`) and, since
2026-11-26, the analysis behind the element values (`Classify`, `GrashofMargin`, `TransmissionAngles`, `RockerSwing`,
`CrankAngleDifference`, `TimeRatio`, `VelocityRatio`, `SliderStroke`, `QuickReturnRam`, `GenevaWheelAngle`,
`Pantograph`, …).

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

**Joint limits (2026-11-25):** a fixed pivot with stops (`limited`) keeps every link on it inside its angle range
(`ObjectType.JointLimits`, page degrees; the range mirrors and turns with the symbol). Dragging in motion mode stops at
the stops, and the coupler curve and the ghost positions end there (the chain is walked until a stop holds the crank).

## Element values (2026-11-26)

The four-bar, slider-crank, quick-return mechanism, scotch yoke, Geneva drive and six-bar have element values (Calc 1, `IValueType`;
engineering-calculations.md sections 2.6 and 3.4). Link lengths and the input angle are the mechanism's own parameters:
they are entered values, and entering one in the values panel changes the drawing. Everything else is derived with
the textbook relations of [S24] (Kerle/Corves/Hüsing; Uicker/Pennock/Shigley). The input speed `n` is optional (text
parameter `inputSpeed`, min⁻¹; positive = clockwise, like the grip); with it the speeds at the current position follow.
The values label (`showValues`) shows the card values.

| Element | Variables (card values in **bold**) | Relations |
|---|---|---|
| Four-bar | a, b, c, d (`crank`, `coupler`, `rocker`, `ground`), φ (`angle`), n, Δ_G, class (text), **μ_min**, **μ_max**, **ψ**, α, **Q**, i, ω₄ | Δ_G = (p + q) − (s + l); μ from the cosine rule cos μ = (b² + c² − x²)/(2bc) with x = \|A O4\| over [max(\|d − a\|, \|b − c\|), min(d + a, b + c)]; crank-rocker only: ψ = arccos((d² + c² − (b + a)²)/(2dc)) − arccos((d² + c² − (b − a)²)/(2dc)), α = arccos((d² + (b − a)² − c²)/(2d(b − a))) − arccos((d² + (b + a)² − c²)/(2d(b + a))); Q = (180° + α)/(180° − α); i = ω₄/ω₂ = a·sin(φ − θ₃)/(c·sin(θ₄ − θ₃)) at the current position and assembly mode; ω₄ = i·n |
| Slider-crank | r, l, e (`crank`, `rod`, `offset`), φ, n, **H**, α, **Q**, **μ_min**, **v** | H = √((l + r)² − e²) − √((l − r)² − e²) (2r without offset); α = arcsin(\|e\|/(l − r)) − arcsin(\|e\|/(l + r)); μ_min = 90° − arcsin((r + \|e\|)/l); v = n·dx_B/dφ |
| Quick-return | d, r, L, l, h (`ground`, `crank`, `lever`, `link`, `ram`), φ, **ψ**, α, **Q**, **H** | ψ = 2·arcsin(r/d); α = 180° − 2·arccos(r/d) (Q = 2 for r/d = 1/2); H from the position analysis over a turn |
| Scotch yoke | r, φ, n, **H**, **v** | H = 2r; v = −n·r·sin φ |
| Geneva drive | z (`slots`), C (`centre`), φ (`angle`, driver), n, **β**, **α_m**, α_d, **τ**, a, i, **ω₂** | β = 360°/z (indexing angle); α_m = 180° − 360°/z (driver angle while the wheel moves), α_d = 180° + 360°/z (at rest); τ = α_m/α_d = (z − 2)/(z + 2) (z = 4: 90°/270° = 1/3, z = 6: 1/2); a = C·sin(180°/z); i = ω₂/ω₁ = −λ·(cos φ − λ)/(1 − 2λ·cos φ + λ²) with λ = sin(180°/z) while \|φ\| ≤ 90° − 180°/z, else 0 (locked; φ = 0 on the line of centres, peak −λ/(1 − λ) = −2.414 for z = 4); ω₂ = i·n (negative: the wheel turns against the driver) |
| Six-bar | a, b, c, d (`crank`, `coupler`, `rocker`, `ground`), φ, n, **θ₆**, **μ₁**, **μ₂**, i, **ω₆** | θ₆ = direction of the output rocker O6 → D (Watt) or O6 → C (Stephenson), measured like φ, from the loop closure; μ₁ at B between coupler and rocker: cos μ₁ = (b² + c² − a² − d² + 2ad·cos φ)/(2bc); μ₂ at the output joint between the output coupler (Watt C-D, Stephenson P-C) and the output rocker; i = dθ₆/dφ (central difference over ±0.01°); ω₆ = i·n. The further links of the chain are read from the element's parameters |

The Grashof class is also an element value (since 2026-11-26): the text-valued panel variable `cls` ("Class" /
"Klasse", kind `kin.grashofClass`, relation `kin.fourbar.class` [S24]) holds the class `Linkages.Classify` gives
(crank-rocker, double-crank, rocker-crank, double-rocker with the coupler turning, change point, double-rocker
non-Grashof) and is shown as that text in the values panel, on the values label (when chosen) and in the calculation
sheet. It is computed only, never entered and never used in arithmetic (engineering-calculations.md section 1.2,
"Text-valued variables"); the badge (`grashof`) and the sign of Δ_G remain. Swing angle and time ratio exist only for a
crank-rocker (unknown otherwise).

The Geneva drive's number of slots is its `slots` choice (4 or 6); a whole number entered as a value (3–12) is drawn as
well, so the drawing follows the values panel. Its values are angles per turn of the driver and the wheel speed at the
current driver angle; they have no checks. The six-bar's values hold at the current position (no check over the whole
motion yet).

**Checks** (section 4 of engineering-calculations.md):

| Id | Elements | Rule | Severity | Source |
|---|---|---|---|---|
| `kin.transmission` | four-bar, slider-crank | μ leaves 40° … 140° somewhere in the motion (μ_min < 40° or μ_max > 140°) | warning | [S24] |
| `kin.grashof` | four-bar | the crank cannot turn fully (not Grashof with the crank or the frame shortest) | hint | [S24] |

**Linked chart.** The four-bar implements `ICharacteristics` (Calc 5): the set `transfer`, "Output angle over input
angle" / "Abtriebswinkel über Antriebswinkel", output angle ψ₄ over the input angle φ (−180° … 180°, one sample per
degree, gaps where it cannot be assembled) and the transmission angle μ in a second panel with lines at 40° and 140°.
The current position is a point; dragging it along φ enters a new input angle. "Characteristic chart ▸" inserts the
chart; the four-bar names its linked charts as dependents.

## Context actions

| Action id | Label (EN / DE) | Icon | Applies to |
|---|---|---|---|
| `kin.angle.next` / `kin.angle.prev` | Position +15° / −15° / Stellung +15° / −15° | `cmd-rotate` | mechanisms, cam follower |
| `kin.ghosts` | Ghost positions / Zwischenlagen (toggles 0 ↔ last count, default 4) | `cmd-animate` | mechanisms |
| `kin.path` | Motion path / Bahnkurve | `cmd-toggle` | mechanisms |
| `kin.dof` | Degree of freedom badge / Freiheitsgrad-Anzeige | `cmd-text` | mechanisms, cam follower |
| `kin.grashof.badge` | Show Grashof class / Grashof-Klasse zeigen | `cmd-values` | four-bar |
| `kin.slots=4/6` | Number of slots ▾ / Schlitzanzahl ▾ (choice) | `cmd-option` | Geneva drive |
| `kin.sixbar.kind=watt/stephenson` | Chain ▾ / Kette ▾ (choice) | `cmd-option` | six-bar |
| `values.chart=transfer` | Characteristic chart ▸ / Kennlinie ▸ (menu, needs the Charts module) | `cmd-option` | four-bar |
| `kin.branch` | Crossed assembly mode / Gekreuzte Montagelage (names `branch` in `ParamKeys`: hosts that show the assembly mode dropdown leave it out) | `cmd-swap` | four-bar |
| `kin.follower=roller/flat/knife` | Follower ▾ / Stößel ▾ (choice) | `cmd-option` | cam follower, cam disc |
| `kin.joint.start` / `kin.joint.end` | Joint at start / end | `cmd-arrow-start` / `cmd-arrow-end` | rigid link |
| `kin.stroke`, `kin.open`, `kin.load` | Extend/retract, open/close, load | `cmd-toggle` | piston, gripper, pulley |
| `kin.path.*`, `kin.labels`, `kin.limit.joint`, `kin.dof.formula` | path, label and badge options | various | motion annotations |

View switching (`schematic`, `detailed`) is offered by the host from `ViewDef` with the icons
`view-schematic` and `view-detailed`.

## Grips (drag handles)

`DragHandles` / `MoveHandle` (hosts draw them as blue grips; interaction-ux.md section 4). Only values that are
parameters of the type get a grip:

| Element | Key | Grip on | Motion | Parameter | Snap |
|---|---|---|---|---|---|
| Four-bar, slider-crank, scotch yoke, quick-return | `angle` | crank pin A | free; the crank turns towards the pointer about its frame pivot | `angle` | 1°, normalised to (−180°, 180°] |
| Geneva drive, six-bar | `angle` | crank pin A | about O1 / O2 | `angle` | 1° |
| Ratchet | `angle` | tip of tooth 0 | about the wheel pivot | `angle` | 1° |
| Pantograph | `angle` | tracer T | about the centre of the traced circle | `angle` | 1° |
| Toggle | `angle` | knee B | about the upper pivot A | `angle` | 1° |
| Cam with follower | `angle` | cam nose (outermost point) | about the cam pivot | `angle` | 1° |
| Piston and cylinder | `stroke` | rod end | along the cylinder axis | `stroke` | 5 %, 0 … 1 |
| Gripper jaws | `open` | right jaw | sideways | `open` | 5 %, 0 … 1 |
| Fixed pivot with stops (`limited`) | `limitFrom`, `limitTo` | outer end of each stop line | about the pivot | `limitFrom`, `limitTo` | 5°, within half a turn of the old value |
| Angle limits | `min`, `max` | outer end of each limit ray | about the joint | `min`, `max` | 5°, within half a turn of the old value |

The input angle follows the mechanism's own sense of rotation (found from the solved pose), so the grip moves with the
pointer for every mechanism. Labels name the value: "Input angle: −60°" / "Antriebswinkel: −60°", "Piston position:
45 %" / "Kolbenstellung: 45 %", "Jaw opening: 60 %" / "Backenöffnung: 60 %", "Stop from: −135°" / "Anschlag von:
−135°", "Minimum angle: −30°" / "Minimaler Winkel: −30°". Self-built linkages (links and joints as point objects) are
moved through their point handles and motion mode, not through grips.

## Host notes

All output uses native primitives (lines, polylines, circles, rectangles, text, groups); hatching is explicit
line segments where hosts lack pattern fills. Ghost positions are `GroupPrim`s. No vector-picture fallback is
needed.

Mechanisms provide `AnimationFrames(o, count)`: the current state, then the input angle stepped like the ghost
positions (a full turn, or the toggle range). Hosts turn them into a slide sequence (PowerPoint COM: "Animate as
slides" with Morph transitions); every frame keeps the box.

"Explode" (`Parts`) exists for the four-bar, slider-crank, scotch yoke, toggle, quick-return, cam and the six-bar (both
chains). The Geneva drive, the ratchet and the pantograph do not explode: the self-built linkage has no intermittent
pin-in-slot pair, no pawl contact, and no bar with three joints in a row; they move by their grip and animation frames.
Wheels, discs and pawls are white-filled polygons (`wheel`, `disc`, `pawl` tags), native freeforms in PowerPoint.
