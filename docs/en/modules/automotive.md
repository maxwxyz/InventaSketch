# Vehicles, ADAS & HMI module (`automotive`)

Reduced line drawings in black-and-white line style for invention disclosures of vehicle functions: vehicles in several
views, vehicle subsystems, people, the road environment, ADAS sensors with fields of view, V2X radio links, perception
overlays, trajectories and HMI elements. All artwork is original, generic and brand-neutral.

> **Limits.** The module draws *illustrative scenes*. Nothing is simulated: fields of view do not detect anything,
> trajectories are not planned or checked for feasibility, and dimensions are not to any vehicle standard. Proportions
> are plausible, not measured. The TTC label and the safety distance compute textbook values (time to collision,
> stopping distance) from entered speeds and gaps (see "Values and checks"); they are sketch estimates, not a validated
> driving function.

German version: [../../de/modules/automotive.md](../../de/modules/automotive.md)

## Conventions

- **Scene scale** – elements are designed at 1:100 (10 design units = 1 m: a car 45 × 18, a lane 35, an adult 17.5
  units). Top views are inserted at 0.6 of that (`AutomotiveModule.TopScale`, 1:167 on the slide, 2026-11-25), so a scene
  fits a 16:9 slide: a 2-lane road is 96 × 42 mm, a car 27 mm long, an intersection 96 × 96 mm, a roundabout 120 × 120 mm;
  every top view shares the factor, so vehicles still fit their lanes. Side, front and rear views keep their design size.
  Free uniform/non-uniform scaling changes only the box size.
- **One lane width everywhere** – straight roads, curves, intersections and the roundabout's arms have the same lane
  width for any number of lanes, so a road docks lane for lane on every arm (before 2026-11-25 the roundabout's arm lanes
  were narrower, and curves and intersections narrowed their lanes from four lanes on). Curves and intersections grow
  with their lanes ("Add lane" scales the box with them); `ObjectType.NaturalSize` tells "+" that this growth is not a
  scale.
- **Driving direction +x** (front on the right), y down; in the top view the vehicle's left side is −y.
  Rotate or mirror the object for other directions.
- Every object is insertable without parameters and is drawn inside its box in every view.
- Vehicle bodies are drawn with a white fill, so later objects cover earlier ones. For a cutaway (occupant inside a
  vehicle) give the vehicle the style override *fill: none* and, if desired, a phantom dash.
- Line roles follow ISO 128 ideas: visible outlines, thin lines, hidden edges (dashed, e.g. wheels under the body in
  the top view), centre lines (vehicle and lane axes), phantom lines (lane polynomial, HUD virtual image).

## Object types

| Category | Type id | Views | Parameters (all optional) |
|---|---|---|---|
| Vehicles | `auto.vehicle.car`, `.suv`, `.van`, `.bus` | top, side, front, rear | – |
| | `auto.vehicle.tram` (low-floor tram, two articulated sections, 20 m × 2.65 m, bidirectional, half pantograph) | top, side, front | – |
| | `auto.vehicle.truck` (rigid), `auto.vehicle.semi` (tractor-trailer) | top, side, front, rear | – |
| | `auto.vehicle.motorcycle`, `.bicycle`, `.scooter` (e-scooter) | top, side, front | – |
| | `auto.vehicle.trailer` | top, side, rear | – |
| | `auto.vehicle.box` (generic vehicle with heading arrow) | top, side, front | – |
| Subsystems | `auto.sub.axles` (axles and wheels, matches the car footprint) | top, side | `axles` 1–5, `steer` (°), `centerline` |
| | `auto.sub.steering` | front, side, top | – |
| | `auto.sub.battery` | top, side | `modules` per row |
| | `auto.sub.edrive` (motor, gearbox, half shafts) | top, side | `inverter` |
| | `auto.sub.seat` | side, top, front | `headrest` |
| | `auto.sub.door` | side, top (opened, swing arc) | `open` (°) |
| People | `auto.human.standing` | front, side, top | – |
| | `auto.human.walking` | side, top | – |
| | `auto.human.seated` | side, top | `role` driver/passenger |
| | `auto.human.child` | front, side | – |
| | `auto.human.wheelchair` (person in a manual wheelchair, 1.3 m high, 1.1 m long) | side, front, top | – |
| | `auto.human.hand` (index finger touching) | side | `ripple` (touch indication) |
| | `auto.human.head` (**point object**: head centre → gaze target) | top, side | `size` (mm), `cone` (°) |
| Road | `auto.road.straight` | top | `lanes`, `markings` (dashed/solid/center-solid/none), `centerlines`, `dash`, `gap` |
| | `auto.road.curve` (90° segment) | top | as above (max. 6 lanes) |
| | `auto.road.intersection` | top | `lanes`, `markings`, `arms` (4 / 3 = T-junction), `stopLines`, `crosswalks`, `centerlines` |
| | `auto.road.roundabout` | top | `ringLanes`, `centerlines` |
| | `auto.road.crosswalk` | top | `stripes` |
| | `auto.marking` (**point object**, lane marking line) | – | `pattern` solid/dashed/double, `smooth`, `dash`, `gap` |
| Traffic | `auto.traffic.light` | front, top | `state` red/amber/green/off, `pole` |
| | `auto.traffic.sign` (text = sign content) | front, top | `shape` circle/triangle/rectangle/octagon, `pole` |
| | `auto.traffic.barrier` | top, side | `kind` guardrail/wall/boom, `posts` |
| | `auto.traffic.parking` (text = slot label) | top | `charging` |
| | `auto.traffic.building` | top, side | `floors` |
| | `auto.traffic.obstacle` | top, side | `kind` box/cone/debris |
| Sensors | `auto.sensor.camera`, `.radar`, `.lidar`, `.ultrasonic` | symbol | – |
| | `auto.fov` (**point object**, field of view sector) | – | `range` (mm), `angle` (°, 360 = full circle), `heading` (°), `fill`, `rings`, `axis`, `label` |
| | `auto.v2x` (**point object**, V2X radio link between two participants) | – | `kind` V2V/V2I/V2P/V2N/V2X (default label), `arrows` both/end/none, `course` dashed/zigzag (option pictograms), `waves`, `label`, `textSize` |
| Perception | `auto.det.bbox2d` | image | `label`, `confidence`, `showConfidence`, `dashed` |
| | `auto.det.bbox3d` | iso, top | `label`, `confidence`, `showConfidence` |
| | `auto.det.grid` (occupancy grid patch) | top | `rows`, `cols`, `occupancy`, `seed` |
| | `auto.det.lanepoly` (**point object**, smooth curve) | – | `showPoints`, `label` |
| Motion | `auto.trajectory` (**point object**, spline with t0..tn) | – | `ticks`, `labels`, `prefix`, `corridor`, `width`, `arrow`, `dashed`, `textSize` |
| | `auto.motion.conflict` | symbol | – |
| | `auto.motion.safetydistance` (**point object**, dimension) | – | `label`, `offset`, `showValue`, `scale` (mm per m), `textSize`; values `speed`, `reaction`, `decel`, `gap`, `stopping`, `reactionDistance`, `brakingDistance`, `showValues` |
| | `auto.motion.ttc` (text overrides the label) | symbol | `ttc` (s, no default: the label shows 2.4 s until set), `frame`; values `vEgo`, `vTarget`, `gap`, `showValues` |
| HMI | `auto.hmi.cluster` | front | `layout` analog/digital, `value`, `speed` |
| | `auto.hmi.display` | front, side | `content` tiles/map/none |
| | `auto.hmi.hud` | side (optical path, eye, virtual image), front (driver's view, virtual image trapezoid) | `speed` |
| | `auto.hmi.button` (text = caption) | front | `pressed` |
| | `auto.hmi.knob` | front, side | `position` (°) |
| | `auto.hmi.slider` (horizontal or vertical by box aspect) | front | `value`, `ticks` |
| | `auto.hmi.warning` (generic placeholder) | symbol | `shape` triangle/circle/square |
| | `auto.hmi.wireframe` | front | `kind` image/text/list/map |

The library contains about 140 presets (every view of every vehicle, sensor-specific fields of view, divided road,
T-junction, charging slot, predicted trajectory, …). Search works in English and German.

## Values and checks (2026-11-26)

Two overlays carry element values (`IValueType`, engineering calculations section 1): they are entered as text with a
unit ("100 km/h", "25 m", "1,5 s") in the value card or the inspector, derived values are computed by the value solver,
and "Show values" (`showValues`) draws the values label below the element.

| Element | Variables (card: first ones) | Relations | Check |
|---|---|---|---|
| `auto.motion.ttc` | `v_ego` (km/h), `v_target` (km/h), `d` gap (m), `TTC` (s) | `auto.ttc`: TTC = d / (v_ego − v_target), solvable for every variable | `auto.ttc.low` (warning): TTC < 2 s |
| `auto.motion.safetydistance` | `v` (km/h), `s` stopping distance (m), `d` gap (m); panel: `t_r` reaction time (default 1 s), `a` deceleration (default 8 m/s²), `s_r` reaction distance, `s_b` braking distance | `auto.braking.reaction`: s_r = v·t_r; `auto.braking.distance`: s_b = v²/(2a); `auto.braking.stopping`: s = s_r + s_b | `auto.braking.gap` (warning): s > d |

- **TTC label:** with speeds and gap entered the label draws the computed TTC ("TTC = 1.8 s"); when the ego vehicle is
  not faster than the vehicle ahead there is no collision course ("TTC = ∞", no value, no warning). Without them it
  keeps drawing its `ttc` parameter (Quick Sketch). An entered TTC also lets the solver compute a missing speed or gap.
  Validity: constant speeds, following in the same lane.
- **Safety distance:** the gap `d` is the drawn length divided by `scale` (mm per m, default 10 = 1:100) unless it is
  entered; the dimension's own value text ("d_safe = 30.0 m", `showValue`) shows the entered gap. Reaction time and
  deceleration are typical defaults (1 s, 8 m/s² full braking on a dry road), not values from a standard; enter your
  own. Textbook check: 100 km/h, 1 s, 8 m/s² → s = 27.8 m + 48.2 m = 76.0 m (unit test).
- **Sources:** the relations are elementary kinematics and the 2 s threshold is a rule of thumb; no entry of the source
  catalogue (`ValueSources`) fits, so none is cited.
- Library presets: "TTC from gap and speeds" (100 km/h, 60 km/h, 25 m → 2.25 s) and "Stopping distance check" (50 km/h,
  gap 30 m, values label on).

## Snapping and attachments

- **Vehicles** expose a `Hub` anchor at the centre (`center`) and at the front/rear axle (`axle.front`, `axle.rear`),
  plus `Mount` points (`mount.front`, `mount.rear`, `mount.roof`, `mount.windshield`, `mount.mirror.left/right`,
  corners in the top view).
- **Roads** expose every lane centreline as an `Axis` segment (curves and the roundabout ring as chords), so a
  vehicle's hub snaps onto the lane, and as a `Rail` with the same geometry (`lane0.rail`, …), so trajectory points,
  lane polynomials and other line ends dock anywhere on a lane centre (snap kind `rail`). Lane ends are `End` points for
  chaining roads and trajectories; only they get a "+" (the next road segment), not the chord joints of curves and the
  ring or the inner ends of junction arms. The "+" continues the road lane for lane with the lane width of the element,
  also after it was scaled (2026-11-25).
- **Sensors** have a `Mount` point at the back and a `Port` at the aperture. `Mount` onto `Mount` is a semantic snap
  (kind `mount`, priority of a port): the sensor's mount wins over closer generic corner or centre snaps.
- **Field of view:** point 0 is the apex, point 1 a direction reference; the sector axis points from point 0 to
  point 1, rotated by `heading`. `Fov.AttachTo(doc, fovId, sensorId)` attaches both points to the sensor (aperture
  and one box length ahead), so the FoV follows moving, rotating and scaling the sensor. Without attachment the FoV is
  a free object.
- Overlays (axles, battery, e-drive output) have `Hub` anchors that snap concentrically onto vehicle hubs.
- **V2X link:** both ends are point-object ends (`p0`, `p1`) that dock onto vehicle hubs and roof mounts, sensors and
  traffic lights; the radio wave arcs sit at the transmitting end (both ends for two-way links).
- **Tram:** like the other vehicles a centre `Hub` (snaps onto lane centrelines), bogie hubs (`axle.front`,
  `axle.rear`) and mounts (`mount.front`, `mount.rear`, `mount.roof` at the pantograph).

## Context actions

| Object | Actions |
|---|---|
| Vehicles | view switches (top, side, front, rear; the hosts' view control, no "next view" button); the scale is kept |
| Roads | lanes + / − (the straight segment keeps its lane width and grows/shrinks), markings ▾, lane centre lines; intersection: cross/T, crosswalks |
| Field of view | range + / − (×1.25), angle + / − (10°), rotate 15°, fill ▾ |
| Trajectory | toggle driving corridor, corridor width + / −, time ticks, dashed (predicted) |
| V2X link | link type ▾, direction ▾, line ▾ (dashed / zigzag pictograms), radio waves, reverse direction |
| Others | parameter toggles and choices ▾ (signal state, sign shape, barrier kind, door opening, steering angle, …) |

Increase/decrease actions use the pictogram ids `cmd-increase`/`cmd-decrease`, which are not yet part of the shared
`IconIds` vocabulary (hosts show a neutral icon until they are added).

## Host notes

Everything renders to lines, paths, rectangles, ellipses and text, so all hosts draw native, editable shapes.
Dotted/hatched fills (field of view, occupancy cells, hatched areas) follow the Core fill model (explicit hatch lines in
SVG export). Text sizes shrink automatically so labels stay inside their boxes.
