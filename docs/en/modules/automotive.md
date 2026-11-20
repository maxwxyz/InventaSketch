# Vehicles, ADAS & HMI module (`automotive`)

Reduced, patent-style line drawings for invention disclosures of vehicle functions: vehicles in several views,
vehicle subsystems, people, the road environment, ADAS sensors with fields of view, perception overlays, trajectories
and HMI elements. All artwork is original, generic and brand-neutral.

> **Limits.** The module draws *illustrative scenes*. Nothing is simulated: fields of view do not detect anything,
> trajectories are not planned or checked for feasibility, TTC and safety-distance values are labels, and
> dimensions are not to any vehicle standard. Proportions are plausible, not measured.

German version: [../../de/modules/automotive.md](../../de/modules/automotive.md)

## Conventions

- **Scene scale 1:100** – 10 mm on the slide correspond to 1 m (a passenger car is 45 × 20 mm in top view,
  a lane is 35 mm wide, an adult is 17.5 mm tall). Free uniform/non-uniform scaling changes only the box size.
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
| Perception | `auto.det.bbox2d` | image | `label`, `confidence`, `showConfidence`, `dashed` |
| | `auto.det.bbox3d` | iso, top | `label`, `confidence`, `showConfidence` |
| | `auto.det.grid` (occupancy grid patch) | top | `rows`, `cols`, `occupancy`, `seed` |
| | `auto.det.lanepoly` (**point object**, smooth curve) | – | `showPoints`, `label` |
| Motion | `auto.trajectory` (**point object**, spline with t0..tn) | – | `ticks`, `labels`, `prefix`, `corridor`, `width`, `arrow`, `dashed`, `textSize` |
| | `auto.motion.conflict` | symbol | – |
| | `auto.motion.safetydistance` (**point object**, dimension) | – | `label`, `offset`, `showValue`, `scale` (mm per m), `textSize` |
| | `auto.motion.ttc` (text overrides the label) | symbol | `ttc` (s), `frame` |
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

## Snapping and attachments

- **Vehicles** expose a `Hub` anchor at the centre (`center`) and at the front/rear axle (`axle.front`, `axle.rear`),
  plus `Mount` points (`mount.front`, `mount.rear`, `mount.roof`, `mount.windshield`, `mount.mirror.left/right`,
  corners in the top view).
- **Roads** expose every lane centreline as an `Axis` segment (curves and the roundabout ring as chords), so a
  vehicle's hub snaps onto the lane, and as a `Rail` with the same geometry (`lane0.rail`, …), so trajectory points,
  lane polynomials and other line ends dock anywhere on a lane centre (snap kind `rail`). Lane ends are `End` points for
  chaining roads and trajectories.
- **Sensors** have a `Mount` point at the back and a `Port` at the aperture. `Mount` onto `Mount` is a semantic snap
  (kind `mount`, priority of a port): the sensor's mount wins over closer generic corner or centre snaps.
- **Field of view:** point 0 is the apex, point 1 a direction reference; the sector axis points from point 0 to
  point 1, rotated by `heading`. `Fov.AttachTo(doc, fovId, sensorId)` attaches both points to the sensor (aperture
  and one box length ahead), so the FoV follows moving, rotating and scaling the sensor. Without attachment the FoV is
  a free object.
- Overlays (axles, battery, e-drive output) have `Hub` anchors that snap concentrically onto vehicle hubs.

## Context actions

| Object | Actions |
|---|---|
| Vehicles | next vehicle view (cycles top → side → front → rear), direct view switches; the scale is kept |
| Roads | lanes + / − (the straight segment keeps its lane width and grows/shrinks), next marking pattern, lane centre lines; intersection: cross/T, crosswalks |
| Field of view | range + / − (×1.25), angle + / − (10°), rotate 15°, next fill |
| Trajectory | toggle driving corridor, corridor width + / −, time ticks, dashed (predicted) |
| Others | parameter toggles and cycles (signal state, sign shape, barrier kind, door opening, steering angle, …) |

Increase/decrease actions use the pictogram ids `cmd-increase`/`cmd-decrease`, which are not yet part of the shared
`IconIds` vocabulary (hosts show a neutral icon until they are added).

## Host notes

Everything renders to lines, paths, rectangles, ellipses and text, so all hosts draw native, editable shapes.
Dotted/hatched fills (field of view, occupancy cells, hatched areas) follow the Core fill model (explicit hatch lines in
SVG export). Text sizes shrink automatically so labels stay inside their boxes.
