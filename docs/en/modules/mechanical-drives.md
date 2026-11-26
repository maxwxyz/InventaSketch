# Mechanical module (`mechanical`): screw drives, transmission elements, serpentine drives and housings

This page describes the screw drives, transmission elements, belt and chain drives over several pulleys, small parts
(retaining ring, pins, O-ring), gearbox housing and oil sump of the mechanical module. Gears, shafts, bearings,
couplings, two-pulley belts, fasteners and springs follow the same conventions and are not repeated here.

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
| `mech.gearset.harmonic` | Strain wave gear / Wellgetriebe | Gears | `stick` (wave generator, flexspline cup, grounded circular spline), `axial` | — | `ground` (Mount); schematic: `in`, `out` (Hub, member ports) |
| `mech.gearset.cycloidal` | Cycloidal drive / Zykloidgetriebe | Gears | `stick` (eccentric, disc, pin ring, output pins), `axial` (cycloid disc in the pin ring) | `lobes` (5–29, default 11; pins = lobes + 1) | `ground` (Mount); schematic: `in`, `out` (Hub, member ports) |
| `mech.clutch.slip` | Slip clutch (torque limiter) / Rutschkupplung (2026-11-26) | Couplings | `stick` (two friction discs with the lining between them, pressure spring against a collar), `side` (discs, lining, hubs, spring pack, adjusting nut) | — (value parameter `slipTorque`, see below) | — |

All of them except the CVT have their `hub` anchor at the box centre on the shaft axis. The CVT has one hub per pulley,
so it sits on two shafts at once; the running radii of the belt follow the ratio (their sum stays constant).

**Member ports of gear units** (2026-11-26, round 4). Gear sets and the CVT have members that turn at different speeds,
so each member docks on its own shaft through a *member port*: a Hub anchor at the end of the member's shaft stub on
the axis. A shaft whose axis passes through the port (it snaps there like onto any hub), or a part whose hub is
concentric with it, is docked on that member and follows the unit's values (see "Links between elements" below).

| Type | Ports (Hub anchor id → member) |
|---|---|
| `mech.gearset.planetary` | schematic: `sun` (right stub → *n*_s, *T*_s), `carrier` (left stub → *n*_c, *T*_c); axial views: the centre bore `hub` → sun |
| `mech.gearset.ravigneaux` | schematic: `sun1` (left stub → small sun *n*_s1), `sun2` (right stub → large sun *n*_s2); axial view: `hub` → large sun |
| `mech.gearset.differential` | `left` (→ *n*₁, *T*₁), `right` (→ *n*₂, *T*₂) at the outer ends of the half shafts (schematic and section) |
| `mech.gearset.harmonic`, `mech.gearset.cycloidal` | schematic: `in` (left stub → *n*₁, *T*₁), `out` (right stub → *n*₂, *T*₂); axial views: the centre bore `hub` → input |
| `mech.cvt` | `hub` (primary pulley → *n*₁, *T*₁), `hub2` (secondary pulley → *n*₂, *T*₂) |

The centre bore yields to a stub port: a shaft that runs from the centre out over the sun stub is the sun shaft. A shaft
through two member ports would lock the unit: it is docked on no member. The ring gear of the sets and the case of the
differential have no port (enter their speed on the set, e.g. *n*_r = 0 for a ring held by the housing), nor does the
carrier of the Ravigneaux set (its hollow shaft ends where the small sun's shaft ends).
Context actions: freewheel "reverse locking direction", converter "lock-up clutch on/off", park lock "teeth ±1" and
"engaged/released", CVT "ratio ±0.2" and "chain/push belt", cycloidal drive "lobes ±1".

Library items: `mech.freewheel`, `mech.freewheel.section`, `mech.freewheel.axial`, `mech.converter`,
`mech.converter.lockup`, `mech.converter.section`, `mech.parklock`, `mech.parklock.axial`, `mech.cvt.stick`,
`mech.cvt.axial`, `mech.cvt.chain`, `mech.harmonic.stick`, `mech.harmonic.axial`, `mech.cycloidal.stick`,
`mech.cycloidal.axial`, `mech.clutch.slip`, `mech.clutch.slip.side` (keywords also "torque limiter", "Überlastkupplung").

The slip clutch is drawn independently in the style of the disc clutch of the module; no standard symbol is claimed. Its
bores match the disc clutch (2.5 in the 20 high schematic, 3 in the 24 high side view), so it fits onto a shaft and keeps
the fit after a view switch.

## Belt and chain drives over several pulleys (category "Belt & chain drives")

| Type id / view | Name (EN / DE) | Views | Parameters | Anchors |
|---|---|---|---|---|
| `mech.belt`, view `serpentine` | Serpentine belt drive / Riementrieb über mehrere Scheiben | `serpentine` (besides `open`, `crossed`, `loop`) | `route` (advanced, see below) | `hub1` … `hubN` (Hub, one per route hub, in route order) |
| `mech.chain`, view `serpentine` | Chain over several sprockets / Kette über mehrere Kettenräder | `serpentine` (plates and rollers along the route; besides `links`, `line`) | `route` | `hub1` … `hubN` |
| `mech.pulley.idler` | Idler pulley / Umlenkrolle | `axial`, `side`, `section` (rolling bearing simplified), `stick` | `hub` (default `loose`), `back` (default on: flat roller for the belt back; off: grooved roller for the running face) | `hub` (Hub with bore), pitch circle (axial view) |
| `mech.tensioner` | Belt tensioner / Riemenspanner | `axial` (sprung arm with spiral spring at the pivot, idler on a bearing), `stick` (pivoted lever, idler, tension spring to ground) | — | `idler` (Hub: the belt runs over it), `pivot` (Mount: arm pivot on the engine or frame) |

- **Route.** The belt or chain runs around an ordered list of hubs. Each hub is stored relative to the box as
  `u,v,k` or `u,v,k,b` (entries separated by `;`): centre at (u·W, v·H) with u, v from −0.5 to 0.5, running radius
  k·min(W, H), `b` = the belt runs over this hub with its **back** (an idler outside the loop that bends the strand
  inwards). Values out of range are clamped so the drive stays inside its box; scaling changes the size, never the route.
  Without the parameter the default route is used: crankshaft pulley, water pump, alternator and one back-side idler.
- **Geometry.** Consecutive hubs are joined by their common tangent: the outer tangent between two hubs the belt wraps
  with its running face, the crossing tangent next to a back-side idler. The loop runs around the front hubs in the
  sense of the polygon of their centres; front hubs are wrapped in that sense, back-side idlers in the opposite one.
- **Snapping.** Every route hub is a Hub anchor, so pulleys, sprockets, idlers and the tensioner's `idler` hub snap onto
  it exactly as onto the end hubs of a two-pulley belt.
- **Editing.** Grips: each hub centre (free) and its running radius (horizontal grip on the right of the circle).
  Context actions in the serpentine view: "Add idler" (a back-side idler between the last and the first hub), "Remove last
  hub" (at least two stay).
- **Following moved pulleys.** A serpentine belt or chain implements the core's `ISpanRoute`: the coupling counts every
  hub, and when a pulley, sprocket or idler on any hub moves, the strand is routed through the hubs' new places (its box
  becomes the hull of the hub circles, radii stay). Two-pulley belts keep the stretch-and-turn rule.
- Library items: `mech.belt.serpentine`, `mech.chain.serpentine`, `mech.idler.axial`, `mech.idler.grooved`,
  `mech.idler.section`, `mech.tensioner`, `mech.tensioner.stick`. Context actions: idler "Shaft connection" (hub
  pictograms) and "Runs on belt back" (on/off).

## Small parts

| Type id | Name (EN / DE) | Category | Views | Parameters | Anchors |
|---|---|---|---|---|---|
| `mech.circlip` | Retaining ring (circlip) / Sicherungsring | Fasteners & joints | `axial` (open ring with the two lugs for the pliers), `side` (on the shaft: both ring sections blackened) | — | `hub` (Hub with the inner radius as bore: fits onto a shaft) |
| `mech.pin` | Pin / Stift | Fasteners & joints | `side`, `top` (end view) | `kind` = `cylindrical` (default) \| `taper` \| `split` (choice with pictograms) | axis (side view) |
| `mech.oring` | O-ring / O-Ring | Bearings, seals & guides | `section` (both cord sections blackened), `top` (inner and outer edge, cord centre line) | — | `hub` (Hub with the inner radius as bore) |

- The taper of the taper pin is exaggerated so it shows; the split pin has its eye on the left and two half-round legs
  of unequal length. Pin type pictograms reuse the shared shape set (rectangle, triangle, split); dedicated pin
  pictograms would need new entries in the shared icon set (open item).
- Library items: `mech.circlip`, `mech.circlip.side`, `mech.pin.cylindrical`, `mech.pin.taper`, `mech.pin.split`,
  `mech.oring`, `mech.oring.top`.

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

## Engineering values (Calc 3)

This section covers all elements of the module (gears, shafts, belts, chains, screw drives, springs, bearings), not only
the ones described above. Values are optional: every element still inserts and scales without them. They are entered and
shown with the Values card (*Edit ▸ Values ▸*, the *Values* button in Explore and Engineer) and stored in optional text
parameters (engineering section of the inspector); existing parameters are reused where they exist. Design notes:
[engineering-calculations.md, section 12](../design/engineering-calculations.md#12-implementation-notes-calc-3-machine-elements-2026-11-25).

Speeds *n*, torques *T* (German symbol *M*) and powers *P* are magnitudes; *P* = 2π·*n*·*T*. Card variables come first.

| Element | Variables (parameter) | Local relations [source] |
|---|---|---|
| Spur/helical, ring, bevel gear, worm wheel | *z* (`teeth`), *m* (`module`, mm), *n* (`speed`), *T* (`torque`); panel *P* (`power`), *d* (`diameter`); engineer η (`efficiency`, default 100 %) | *d* = *m*·*z* [S16]; *P* = 2π·*n*·*T* [S18] |
| Worm | *z* = number of starts (`starts`), *m*, *n*, *T*; *P*, η | *P* = 2π·*n*·*T* |
| Rack | *m*, *v* (`linSpeed`), *F* (`force`); panel *P*, pinion *z*_p (`pinionTeeth`), *d*_p (`pinionDiameter`, mm), *n*_p (`pinionSpeed`), *T*_p (`pinionTorque`) | *P* = *F*·*v* [S1]; *d*_p = *m*·*z*_p [S16]; *v* = ω·*r* = π·*d*_p·*n*_p; *F* = *T*/*r* = 2·*T*_p/*d*_p [S18] |
| Planetary set | *z*_s (`zs`, default 20), *z*_r (`zr`, default 52), *n*_s (`speeds`), *n*_c (`speedc`); panel *n*_r (`speedr`), *T*_s, *T*_r, *T*_c (`torques`, `torquer`, `torquec`); engineer *k* = *z*_p1/*z*_p2 for stepped planets (`stepRatio`, default 1) | Willis *n*_s − *n*_c = −(*z*_r/*z*_s)·*k*·(*n*_r − *n*_c); *T*_r = *T*_s·*k*·*z*_r/*z*_s; *T*_s + *T*_r + *T*_c = 0; so *T*_c = −*T*_s·(1 + *k*·*z*_r/*z*_s) [S39]. Speeds and torques are signed here (same sense positive) |
| Ravigneaux set | *n*_s1 (`speeds1`, small sun), *n*_s2 (`speeds2`, large sun), *n*_r (`speedr`), *n*_c (`speedc`); panel *z*_s1 (`zs1`, default 14), *z*_s2 (`zs2`, default 20), *z*_r (`zr`, default 52) | *n*_s2 − *n*_c = −(*z*_r/*z*_s2)·(*n*_r − *n*_c) (long planets); *n*_s1 − *n*_c = +(*z*_r/*z*_s1)·(*n*_r − *n*_c) (planet pairs: two external meshes, the planet teeth cancel) [S39]. Signed speeds. No torque relations (two degrees of freedom: the split depends on which members are held) |
| Bevel differential (`mech.gearset.differential`) | *n*_c (`speedc`, case/crown wheel), *n*₁, *n*₂ (`speed1`, `speed2`, half shafts), *T*_c (`torquec`); panel *T*₁, *T*₂ (`torque1`, `torque2`) | *n*_c = (*n*₁ + *n*₂)/2 [S39] (signed); *T*₁ = *T*₂ = *T*_c/2 (open differential, friction neglected) [S18] |
| Strain wave gear | *z*_f (`flexTeeth`, default 200), *z*_c (`circularTeeth`, default 202), *i* (`ratio`), *n*₂ (`speedOut`); panel *n*₁ (`speedIn`), *T*₁ (`torqueIn`), *T*₂ (`torqueOut`); engineer η (`efficiency`, default 100 %) | *i* = *n*₁/*n*₂ = −*z*_f/(*z*_c − *z*_f) (circular spline fixed, wave generator in, flexspline out; Willis with the wave generator as carrier) [S39]; *n*₁ = *i*·*n*₂; *T*₂ = \|*i*\|·η·*T*₁ [S18]. Signed speeds: *i* < 0, the output turns against the input |
| Cycloidal drive | *z*_d (lobes, the type's `lobes`, default 11), *i* (`ratio`), *n*₂ (`speedOut`); panel *z*_p (`pins`), *n*₁, *T*₁, *T*₂; engineer η | *z*_p = *z*_d + 1 (as drawn); *i* = −*z*_d/(*z*_p − *z*_d) (pin ring fixed, eccentric in, disc rotation out through the output pins) [S39]; *n*₁ = *i*·*n*₂; *T*₂ = \|*i*\|·η·*T*₁ [S18] |
| CVT variator | *i* (the type's `ratio`, default 1), *n*₁ (`speedIn`), *n*₂ (`speedOut`); panel *T*₁, *T*₂; engineer η | *n*₁ = *i*·*n*₂; *T*₂ = *i*·η·*T*₁ [S18]. The drawing follows an entered `ratio`; a ratio derived from the speeds is checked against the spread 0.4 … 2.5 |
| Flywheel | *n* (`speed`), *m* (`mass`, kg), *D* (`diameter`, outer, mm), *E* (`energy`, kJ); panel *J* (`inertia`, kg·m²), *d*_i (`innerDiameter`, mm, default 0), Δ*E* (`energyFluctuation`, kJ), Δ*n* (`speedFluctuation`); engineer δ (`fluctuationCoefficient`) | *J* = *m*·(*D*² + *d*_i²)/8: solid disc *d*_i = 0 (*J* = ½·*m*·*r*²), rim between *d*_i and *D* (thin rim *J* → *m*·*r*²; hub and spokes neglected); *E* = ½·*J*·ω²; Δ*E* = *J*·ω·Δω = 4π²·*J*·*n*·Δ*n* (*n* the mean speed); δ = Δ*n*/*n* [S18] |
| Shaft, spline shaft, rigid and flexible coupling, universal joint | *n*, *T*, *P* | *P* = 2π·*n*·*T* |
| Slip clutch | *n*, *T*, *P*, *T*_slip (`slipTorque`, German symbol *M*_R) | *P* = 2π·*n*·*T*; check `mech.clutch.slip` |
| Pulley | *d* (`diameter`, effective diameter), *n*, *T*, *P* | *P* = 2π·*n*·*T* |
| Idler pulley, tensioner (its idler) | *d* (`diameter`), *n* | – (an idler passes no torque) |
| Sprocket | *z* (`teeth`), *n*, *T*, *P* | *P* = 2π·*n*·*T* |
| Belt | *v*, *F* (effective pull), *P* | *P* = *F*·*v* |
| Roller chain | *p* (`pitch`, mm), *v*, *F*, *P* | *P* = *F*·*v* |
| Screw (spindle) | *P*_h (lead, `pitch`, mm), *n*, *v*, *F*; panel *T*, *P*; engineer η (default 100 %) | *v* = *P*_h·*n*; *F* = 2π·*T*·η/*P*_h [S18, S23]; *P* = 2π·*n*·*T* |
| Screw-drive nut | *v*, *F* | – |
| Compression and tension spring | *F* (`force`), *s* (`travel`), *c* (`rate`); panel *d* (`wire`), *D* (`meanDiameter`), *n*_f (`activeCoils`); engineer *G* (`shearModulus`, default 81 000 N/mm²) | *F* = *c*·*s*; *c* = *G*·*d*⁴/(8·*D*³·*n*_f) [S21] |
| Torsion spring | *T* (`torque`), φ (`angle`, °), *c*_φ (`rate`, N·m/rad or N·m/°) | *T* = *c*_φ·φ (φ in rad) [S21] |
| Rolling bearings (ball, angular contact, thrust: *p* = 3; tapered, cylindrical, needle roller: *p* = 10/3) | *C* (`loadRating`, kN), *P* (`load`, kN), *L*₁₀ (`life`, 10⁶ revolutions), *L*₁₀h (`lifeHours`, h); panel *n*; engineer *L*_req (`requiredLife`, h) | *L*₁₀ = (*C*/*P*)^*p*; *L*₁₀h = 10⁶·*L*₁₀/(60·*n*) [S19] |
| Plain bearing | *n* | – |

**Links between elements** (read from the coupling of the drawing, the same relations as "select connected – moves
along"):

- *Shaft line*: a shaft, couplings on it and the parts fixed on it (hub fixed, sliding or none) share *n*; a loose hub
  turns freely. Bearings take the speed of their shaft. The torque passes along the line unchanged when at most two
  parts on it take or give torque; with a branch (three or more) no torque is passed. A slip clutch passes *n* and *T*
  like a coupling (while it holds; `mech.clutch.slip` warns above *T*_slip). A flywheel takes the speed only: it has no
  torque variable, so it never counts as a branch.
- *Mesh*: *z*_a·*n*_a = *z*_b·*n*_b and *T*_b = *T*_a·(*z*_b/*z*_a)·(η_b/η_a) [S18]. η of a gear is the efficiency of
  the mesh in which it is the driven gear (enter it on the driven gear of each stage); with an input and an output
  torque entered, η is derived. Planets on a carrier pin are not linked (use the planetary set).
- *Belt*: *v* = π·*d*·*n* and *T* = *F*·*d*/2 at both pulleys. *Chain*: *v* = *z*·*p*·*n* and *T* = *F*·*z*·*p*/(2π);
  without a pitch the sprockets still keep *z*₁·*n*₁ = *z*₂·*n*₂ and *T*₂·*z*₁ = *T*₁·*z*₂ [S18, S20].
- *Serpentine drives*: every part on a route hub runs at the strand speed (*v* = π·*d*·*n*, chain *v* = *z*·*p*·*n*), so
  the speed ratio of any two pulleys follows their diameters (sprockets: their teeth). Idlers (idler pulley, tensioner
  and any part on a hub marked `b`) take the speed only: they never change the ratio and pass no torque. The torque
  relation (*T* = *F*·*d*/2, chain *T* = *F*·*z*·*p*/(2π)) is set only while at most two parts take or give torque
  through the strand; with three or more the effective pull differs per strand section and no torque is derived.
- *Screw and nut*: the nut on the spindle takes its *v* and *F*.
- *Gear units* (planetary, Ravigneaux, differential, strain wave, cycloidal, CVT): a member docked on a shaft or part
  (see "Member ports of gear units") shares the speed of that part's shaft line and, when the line carries torque, its
  torque. The docked member counts as one part taking or giving torque, so a motor, its shaft and a sun pass the torque,
  while a second gear on the same shaft makes a branch and no torque is passed. Shaft speeds and torques are magnitudes,
  the members of the sets are signed: the shaft takes the magnitude (*n* = \|*n*_m\|, *T* = \|*T*_m\|) and a member driven
  from its shaft takes the positive sense. Example: motor shaft at 1000 min⁻¹ with 10 N·m on the sun (*z*_s = 20), ring
  held (*z*_r = 52): the carrier shaft runs at 1000·20/72 = 277.8 min⁻¹ with 36 N·m. The CVT's speeds and torques are
  magnitudes and are linked unchanged: at ratio 2, 3000 min⁻¹ and 100 N·m in with η = 90 % give 1500 min⁻¹ and 180 N·m on
  the output shaft. Two units docked on one shaft (a CVT driving a planetary carrier) pass the torque through it.
- *Rack and pinion*: a spur gear seen along its axis whose pitch circle touches the rack's pitch line on the teeth side
  (within the rack's length; 45 % of the tooth depth below the tips, schematic: midway between body line and tips) is the
  rack's pinion: the rack takes its *n*_p, *T*_p, *z*_p and *d*_p from the gear, so *v* = ω·*r* follows the pinion (and
  the pinion's shaft line takes its speed back from an entered rack speed). With two or more such gears the rack takes
  none (enter the pinion values on the rack).
- Drivetrain parts of the machines module (e-machine, engine, wheel) on a shaft join the shaft line with the same keys.

**Shaft lines across modules.** The shaft line rule is shared by all modules (core `ShaftLines`, built on the coupling
relations): shafts, spline shafts, couplings, slip clutches and universal joints pass the torque on unchanged, bearings take the speed
only, planet carriers, differential cases, gear sets and the screw-drive nut stay out (gear sets and the CVT dock their
members on shafts instead; the core rule of other modules' elements does not yet count a docked member as a torque
part). Elements of any module with a
speed *n* (and a torque *T*) whose hub sits on a shaft axis or on another hub join the line. Every module links its
elements as a star around the line's shaft or coupling, so an e-machine of the machines module, a mechanical shaft and a
pump of the process or fluid module share *n* and, with at most two torque members, *T*: the drive's power 2π·*n*·*T*
becomes the pump's shaft power. Pulleys, sprockets, idler pulleys and tensioners have a pitch circle that sizes the belt
or chain only: two of them whose pitch circles touch do not mesh like gears (no gear ratio, no scaling when one moves).

**Checks** (hints only, overlay, nothing changes):

| Id | Rule | Severity | Source |
|---|---|---|---|
| `mech.mesh.module` | meshing gears with different modules | warning | [S16] |
| `mech.power.balance` | η > 100 % on a gear, worm or screw (more power out than in) | warning | [S1] |
| `mech.planetary.assembly` | (*z*_s + *z*_r)/*N* not an integer or *z*_r − *z*_s odd (simple planets) | warning | [S18, S39] |
| `mech.bearing.life` | *L*₁₀h below the entered required life | hint | [S19] |
| `mech.screw.selflock` | lead screw with an entered or derived η > 50 % (back-driving possible) | info | [S18] |
| `mech.harmonic.teeth` | strain wave gear with *z*_c − *z*_f not a positive even number (two-lobe wave generator) | warning | [S39] |
| `mech.cvt.range` | CVT ratio (entered or derived from *n*₁/*n*₂) outside the spread of its `ratio` parameter (0.4 … 2.5) | warning | [S18] |
| `mech.clutch.slip` | torque through a slip clutch (its own *T*, linked from the shaft line) above *T*_slip | warning | [S18] |

Not yet available: torque relations of the Ravigneaux set (speeds only); ports for the ring gears, the differential case
(crown wheel driven by a bevel pinion) and the Ravigneaux carrier; meshes with a gear set (a gear on the ring's outside);
the sense of rotation of a shaft (a shaft takes the magnitude of a signed member, so entering both the input and the
output shaft speed of a set whose output turns backwards gives a conflict: enter the output on the set); the rack and
gear set links in the moving gear train of the core (turning a part does not move a rack, and the core treats a
schematic gear set carried by two shafts as one rigid body); Willis across separate planetary parts; torque split at
branches (shaft lines and strands with three or more parts taking
torque). No strength verification (ISO 6336, DIN 743, EN 13906 beyond *F* = *c*·*s* and *T* = *c*_φ·φ), no slip
torque design (friction radius, number of faces, spring force).

## Host notes

All new elements render to native host primitives (lines, rectangles, ellipses, polylines and polygons with hatch fill),
as the other mechanical symbols; no vector picture is needed. The serpentine belt is one closed polygon (arcs sampled),
the serpentine chain two closed polygons plus one small circle per roller. The route grips are ordinary drag handles of the
engine (`dragHandles` / `moveHandle`, as the limit lines of charts); a host without drag-handle support still offers the
`route` parameter. Hatching of sections and the oil fill (horizontal hatch)
use the same hatch fill as other sections; where a host has no native pattern fill it draws explicit hatch lines (see the
host capability matrix).
