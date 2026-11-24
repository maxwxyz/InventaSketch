# Mechanical module (`mechanical`): screw drives, transmission elements and housings

This page describes the screw drives, transmission elements, gearbox housing and oil sump of the mechanical module.
Gears, shafts, bearings, couplings, belts, fasteners and springs follow the same conventions and are not repeated here.

> **Limits.** All drawings are *illustrative* and drawn independently (no standard figures copied). Thread forms, ball
> grooves, converter blading, cycloid disc and strain wave geometry follow plausible proportions; they are not the result
> of a design calculation. Do not read dimensions from them.

Every element can be inserted without entering parameters. Free uniform and non-uniform scaling changes only the box size;
parameters stay untouched. Schematic views (`stick`) follow the style of the gear-train schematics of the module: shaft
horizontal, element centred on the shaft axis, hub symbols as for gears. Elements with a hub sit on a shaft: their `hub`
anchor snaps onto a shaft axis, the shaft carries them when it moves or turns, and they slide along it when moved alone.

## Screw drives (category "Screw drives")

| Type id | Name (EN / DE) | Views | Anchors |
|---|---|---|---|
| `mech.screwdrive.screw` | Ball screw / lead screw — Gewindespindel (Kugel- / Trapezgewinde) | `stick` schematic, `side` conventional thread, `detailed` thread profile | `axis` (Axis over the full length), `left`/`right` (End) |
| `mech.screwdrive.nut` | Screw-drive nut — Gewindetrieb-Mutter | `stick`, `side` (flange, ball return tube), `section` (balls or trapezoidal thread) | `hub` (Hub, centre), `mount` (Mount, where the carriage attaches) |

- The spindle is drawn in actual millimetres like a shaft: journals (bearing seats) at both ends, threaded length between.
  The side view shows the major diameter as visible line and the minor diameter as thin line; the profile view shows
  trapezoidal teeth (lead screw) or circular ball grooves (ball screw).
- **The nut follows the spindle axis:** its hub docks on the spindle's axis (snap kind "axis"); moving or turning the
  spindle takes the nut along, moving the nut alone slides it along the spindle. Bearings, couplings and gears dock on the
  same axis.
- Parameters (all optional): `kind` = `ball` | `lead` (both types), `pitch` lead in mm per turn (spindle; draws the thread
  pitch true to scale), `lefthand` (spindle), `flange` (nut).
- Library items: `mech.ballscrew.stick|side|profile`, `mech.ballnut.stick|side|section`, `mech.leadscrew.stick|side|profile`,
  `mech.leadnut.stick|side|section`.

## Transmission elements

| Type id | Name (EN / DE) | Category | Views | Parameters | Extra anchors |
|---|---|---|---|---|---|
| `mech.clutch.freewheel` | Freewheel (one-way clutch) / Freilauf | Couplings | `stick`, `section`, `axial` (inner star with ramps, clamping rollers) | `reverse` (locking direction) | — |
| `mech.converter.torque` | Torque converter / Drehmomentwandler | Couplings | `stick` (cover, pump, turbine, stator on freewheel, grounded stator shaft), `section` | `lockup` (lock-up clutch) | `ground` (Mount) |
| `mech.parklock` | Park lock (parking pawl) / Parksperre | Couplings | `stick`, `axial` (park gear, pawl, actuator cone) | `teeth` (6–30, default 12), `engaged` (default on) | `pivot` (Mount) |
| `mech.cvt` | CVT variator / CVT-Variator | Belt & chain drives | `stick` (two cone-pulley pairs on parallel shafts), `axial` (pulleys with belt loop) | `ratio` (0.4–2.5, default 1), `chain` | `hub` and `hub2` (Hub, one per shaft) |
| `mech.gearset.harmonic` | Strain wave gear / Wellgetriebe | Gears | `stick` (wave generator, flexspline cup, grounded circular spline), `axial` | — | `ground` (Mount) |
| `mech.gearset.cycloidal` | Cycloidal drive / Zykloidgetriebe | Gears | `stick` (eccentric, disc, pin ring, output pins), `axial` (cycloid disc in the pin ring) | `lobes` (5–29, default 11; pins = lobes + 1) | `ground` (Mount) |

All of them except the CVT have their `hub` anchor at the box centre on the shaft axis. The CVT has one hub per pulley,
so it sits on two shafts at once; the running radii of the belt follow the ratio (their sum stays constant).
Context actions: freewheel "reverse locking direction", converter "lock-up clutch on/off", park lock "teeth ±1" and
"engaged/released", CVT "ratio ±0.2" and "chain/push belt", cycloidal drive "lobes ±1".

Library items: `mech.freewheel`, `mech.freewheel.section`, `mech.freewheel.axial`, `mech.converter`,
`mech.converter.lockup`, `mech.converter.section`, `mech.parklock`, `mech.parklock.axial`, `mech.cvt.stick`,
`mech.cvt.axial`, `mech.cvt.chain`, `mech.harmonic.stick`, `mech.harmonic.axial`, `mech.cycloidal.stick`,
`mech.cycloidal.axial`.

## Gearbox housings and lubrication (category "Gearbox housings & lubrication")

| Type id | Name (EN / DE) | Views | Parameters | Anchors |
|---|---|---|---|---|
| `mech.housing.gearbox` | Gearbox housing / Getriebegehäuse | `section` (hatched walls), `stick` (wall centre line) | `seats` (1–4 per wall, default 2), `seatpos` (advanced: seat heights 0…1 from the top, `;`-separated) | `seat1l`, `seat1r`, … (bearing seats: Mount circles), `base` (Mount point at the bottom), bottom edge (Mount segment) |
| `mech.housing.sump` | Oil sump with oil level / Ölwanne mit Ölstand | `section` (hatched tray, oil, level line and mark, drain plug), `stick` | `level` (0.05–0.95 of the depth, default 0.6), `drain` (default on) | `mount`, `flange-l`, `flange-r` (Mount), `level` (Rail along the oil level) |

- Both are drawn in actual millimetres: the wall thickness does not stretch, so the housing can be sized freely.
- **Fit around contents** (housing context action): sizes the housing around the parts its box overlaps (gears, bearings,
  clutches …, plus 2 mm clearance and the wall) and puts one seat row at the height of each horizontal shaft crossing it
  (up to four). Shafts set the seat heights but not the size, as they pass through the walls. Rotated housings are set
  back to 0°.
- Bearing seats are circles: a bearing hub dropped near a seat snaps concentrically into it. A seat is no shaft axis, so
  the housing neither carries the bearing nor is carried by a shaft.
- The sump's `mount` docks onto the housing's `base`; annotation leaders dock anywhere along the oil level (`level` rail).
- Context actions: housing "bearing seats ±1" (resets `seatpos`), "fit around contents"; sump "oil level up/down",
  "drain plug on/off".

## Host notes

All new elements render to native host primitives (lines, rectangles, ellipses, polylines and polygons with hatch fill),
as the other mechanical symbols; no vector picture is needed. Hatching of sections and the oil fill (horizontal hatch)
use the same hatch fill as other sections; where a host has no native pattern fill it draws explicit hatch lines (see the
host capability matrix).
