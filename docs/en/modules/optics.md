# Optics module (`optics`)

Library: `src/InventaSketch.Modules.Optics` · Tests: `tests/InventaSketch.Modules.Optics.Tests` ·
German version: [../../de/modules/optics.md](../../de/modules/optics.md)

The module provides optical elements for technical figures and teaching slides — lenses, mirrors, prisms, beam
splitters, stops, gratings, filters, an optical fibre, light sources, detectors, a screen, an object arrow and the
optical axis — and **light rays that are traced live through the elements**. All symbols are drawn independently; no
standard symbol artwork or tables are copied and no compliance with a standard is claimed.

It is a **sketching** module: the ray tracer is two-dimensional and uses ideal (paraxial) lenses and curved mirrors.
It does not design or optimise optical systems (no aberrations, thick-lens data or radiometry).

## Conventions

- **Quick sketch first:** every library item inserts without parameters. Free (uniform or non-uniform) scaling changes
  `Size` only. The box height of a lens, mirror, stop, grating or filter is its aperture.
- **Optical axis = local x axis** of every element (light from the left travels towards +x). Rotate or mirror the element
  to point it elsewhere; mirrors reflect on their local −x side (hatched back on +x).
- **Focal lengths are in the drawing scale** (mm on the page, parameter `f`, magnitude; the sign comes from the type:
  converging/concave positive, diverging/convex negative). Scaling an element does not change `f`.
- **Line roles (ISO 128 ideas):** element outlines visible lines (0.5 mm), details thin (0.18–0.35 mm), the optical axis a
  thin dash-dot centre line, rays thin continuous lines (0.35 mm, red by default), virtual rays dashed (hidden line role).
  Glass is filled with a light tint; monochrome output turns everything black and white.
- **Labels:** optional `label` parameter, drawn below the element (outside the box, like designators elsewhere).

## Object types

| Type id | Element | Parameters (all optional) |
|---|---|---|
| `optics.lens.converging` | Converging lens; views `shape` (section) and `symbol` (double arrow, heads outward) | `shape` biconvex/planoconvex/meniscus, `f` (40), `focal` (F, F′ markers), `rays`, `bundle`, `beam`, `label` |
| `optics.lens.diverging` | Diverging lens; views `shape` and `symbol` (heads inward) | `shape` biconcave/planoconcave/meniscus, as above |
| `optics.mirror.plane` | Plane mirror with hatched back | `label` |
| `optics.mirror.concave` / `optics.mirror.convex` | Curved mirrors | `f` (40), `focal` (F and centre of curvature C/M), `label` |
| `optics.prism.triangle` | Prism, equilateral (deviating) or right-angle (reflecting) | `shape`, `n` (1.5, d line), `dispersion`, `dn` = n(F) − n(C) (0.03), `label` |
| `optics.splitter.cube` / `optics.splitter.plate` | Beam splitter: the diagonal (bottom left to top right) transmits and reflects | `label` |
| `optics.stop.aperture` | Aperture stop / slit (two blades) | `opening` (fraction of the height, 0.35), `label` |
| `optics.grating.transmission` | Transmission grating (plate with grooves) | `angle` (first-order angle at 550 nm, 20°), `orders` (1–3), `label` |
| `optics.filter.plate` | Filter plate, neutral or coloured | `tint` neutral/red/green/blue/yellow, `label` |
| `optics.fibre.cable` | Optical fibre with connectors at both ends | `spread` (output half cone, 12°), `rays`, `label` |
| `optics.source.lamp` | Lamp (bulb, filament, socket) | `emission` cone/parallel/all directions, `rays`, `spread` (0 = fill the next element), `beam`, `label` |
| `optics.source.led` | LED (dome, flange, leads) | `emission`, `rays`, `spread`, `beam`, `color` white/red/green/blue, `label` |
| `optics.source.laser` | Laser (housing, output aperture) | `rays` (1), `beam`, `color` 650/532/445 nm, `label` |
| `optics.detector.photodiode` | Photodiode as optical detector | `label` |
| `optics.detector.camera` | Camera: lens barrel focused on the image sensor | `label` |
| `optics.detector.eye` | Eye (section): cornea, iris, lens, retina, optic nerve | `label` |
| `optics.screen.plain` | Screen | `label` |
| `optics.object.arrow` | Object arrow standing on the axis (imaging figures) | `bundle` principal rays/fan, `rays`, `image` off/image/construction (image), `names` (false), `label` |
| `optics.ray` | Light ray (point object, page coordinates) | `trace` (true), `arrows` middle/end/none, `virtual` (true), `tail` (0 = automatic) |
| `optics.axis.line` | Optical axis (dash-dot centre line, point object) | `label` |

Library items (35) include the lens shapes and symbols, three mirrors, deviating/dispersing/right-angle prisms, both
splitters, aperture and slit, grating, neutral and red filters, fibre, lamp (cone and parallel), LED, red and green
laser, photodiode, camera, eye, screen, traced and freely drawn light ray, optical axis and object arrow.

## Light rays and ray tracing

A ray object (`optics.ray`) works in one of three ways:

1. **Docked on an emitter** (its first point attached to the emitter's anchor): it draws that element's bundle.
   - Lamp/LED: a cone from the bulb or dome (`spread` 0 = just fill the lens, mirror or aperture of the next element on
     the axis), parallel light (`beam` height, 0 = 70 % of the next element's aperture) or, for the lamp, all directions.
   - Laser: one ray or `rays` across `beam`, coloured by wavelength.
   - Fibre: a cone from the output end.
   - Lens (`o`): its own demonstration bundle, parallel to the axis from the left or from a point on the axis at 2f.
   - Object arrow (`tip`): the **principal rays** to the next lens or curved mirror on the axis — parallel ray, central
     ray (through the optical centre or vertex) and focal ray (through or towards the front focal point) — or a fan
     across its aperture.
2. **Free ray** (not docked): a single ray from its first point towards its second point.
3. **Drawn freely** (`trace` = false): a polyline through its points, no tracing.

The context action **Add ray bundle** (`optics.trace`) on lamps, LEDs, lasers, fibres, lenses and object arrows inserts a
ray object docked on the element (anchor `exit`, `o` or `tip`); when one is docked already the action reads
**Remove ray bundle** and removes it.

**Live tracing.** Rays are traced every time they are drawn, from the current document: every optics element on the
page (hidden layers excluded) takes part. Every optics element reports the rays of its page as dependents
(`ObjectType.Dependents`, `HasDependents`; only rays and object arrows set `DependsOnNeighbours`); elements that act on light (they have an optical surface) also report
the object arrows of the page that draw an image. So moving, rotating, scaling or changing an element, or dragging one of
its grips, redraws the rays and images in the same undo step; the docked start point follows its emitter through the
ordinary attachment. Nothing has to be re-traced by hand.

**Physics used:**

- Thin lenses: ideal, in the centre plane of the lens, slope change −y/f (paraxial). Parallel rays meet exactly in F′;
  the thin-lens equation 1/f = 1/s + 1/s′ holds (`ThinLens.ImageDistance`). Diverging lenses draw the backward
  extensions to the virtual focal point as dashed **virtual rays**.
- Curved mirrors: ideal, as a thin mirror in the vertex plane with focal length f (C at 2f); the ray turns on the drawn
  surface and is aimed so that parallel rays meet exactly in F. Convex mirrors draw virtual rays.
- Plane mirrors, beam splitters: law of reflection, exact. The back of a mirror stops light.
- Prisms: Snell's law at every face, total internal reflection when it applies. With dispersion, white light entering
  the prism splits into five colours (656, 589, 546, 486, 436 nm) with n(λ) from a two-term Cauchy fit through `n`
  and `dn`. The dispersing prism item uses `dn` = 0.05 (strong flint-like dispersion, chosen for visibility).
- Gratings: sin θm = sin θ + m · sin θ1 · λ / 550 nm for m = ±1 … ±`orders`; white light gives a spectrum per order.
- Stops pass rays within the opening and stop the others; filters pass their colour band (white light takes the filter
  colour); screens, detectors, the camera body, the eye ball, source housings and the fibre ends stop rays. The eye and
  camera focus parallel light on the retina / sensor.
- After the last element a ray runs on for `tail` mm; automatic (0): as long again as the way so far (30–400 mm), and the
  open ends of a bundle are lined up across it. A free ray that meets nothing ends at its second point.

## Image construction

An object arrow (`optics.object.arrow`) with a converging or diverging lens or a curved mirror in front of it on its axis
(the nearest such element whose centre lies within half its larger box side of the arrow's axis line; a mirror must turn
its reflecting side to the arrow) draws its **image** – parameter `image`, a choice in the context bar:

- `off`: no image.
- `image` (default): the image arrow only. A **real** image (light really meets there) is drawn solid, a **virtual** one
  dashed (hidden line role).
- `construction`: the image with the three **construction rays** from the arrow tip, one colour each: the parallel ray
  (red: parallel to the axis up to the element, then through F′ or away from the virtual F′), the central ray (green:
  through the optical centre or vertex, undeviated or reflected symmetrically) and the focal ray (blue: through or towards
  F, parallel to the axis after the element). Backward extensions to a virtual image and the part of a focal ray that only
  aims at F are dashed. As in textbook figures, the rays turn on the element's principal plane, which is extended beyond the
  drawn aperture where needed.

Each point of the arrow (foot and tip) is imaged with the thin lens equation **1/f = 1/g + 1/b** and the magnification
**β = −b/g** (g object distance, b image distance; b > 0 behind a lens or in front of a mirror = real, b < 0 = virtual;
`ThinLens.ImageDistance`/`Magnification`). Examples (f = 40 mm): g = 2f gives b = 2f, β = −1; g = 1.5f gives b = 3f,
β = −2; g = f/2 gives b = −f, β = 2 (virtual, upright, magnified). A mirror folds the image space back onto its front;
a convex mirror's image lies behind it. At g = f (image at infinity), for images farther than 5 m or magnified more than
50 times, no image is drawn (the construction rays still are, without the focal ray).

`names` (toggle **Names of object and image**) writes the names at the tips: German "G" and "B", English "O" and "I".
The image and its rays are pure paraxial geometry from the document, drawn with the arrow (outside its box, like the focal
point marks of a lens), and follow live when the arrow, the lens or the mirror moves or changes. A ray bundle docked on the
arrow (principal rays, traced) still works independently: the traced rays meet in the drawn image.

## Grips (drag handles)

Values are dragged directly on the drawing (`ObjectType.DragHandles` / `MoveHandle`, page mm; the hosts show and drive
them generically). A drag only writes the parameter; the box (`Size`) and the position never change.

| Element | Grip (key) | Where | Moves | Writes |
|---|---|---|---|---|
| Lenses | `f.back`, and `f.front` when the focal points are drawn | F′ at +f (F at −f) on the axis | along the axis | `f` = distance from the lens, whole mm, 1–5000 (the sign stays with the type) |
| Lenses with their own bundle docked | `beam` | edge of the bundle (start of the parallel rays, or on the lens plane for rays from 2f) | across the axis | `beam` = twice the distance from the axis, whole mm |
| Concave/convex mirror | `f`, and `cc` when F and C are drawn | F (in front of a concave mirror, behind a convex one), C at 2f | along the axis | `f` = distance from the vertex (C: half of it), whole mm |
| Aperture stop / slit | `opening` | inner edge of the upper blade | across the axis | `opening` = share of the half height, steps of 0.01, 0.02–0.95 |
| Lamp, LED (cone) with docked rays | `spread` | on the upper edge ray of the cone, beyond the bulb | freely; the angle seen from the bulb centre counts | `spread` = half cone angle, whole degrees, 1°–80° |
| Lamp, LED (parallel) with docked rays | `beam` | edge of the bundle at the bulb/dome | across the axis | `beam`, whole mm |
| Laser with several rays and docked rays | `beam` | edge of the beam at the output | across the axis | `beam`, whole mm |
| Fibre with docked rays | `spread` | on the upper edge ray of the output cone | freely, angle from the output end | `spread`, whole degrees |
| Free traced ray | `dir` | on the drawn ray, halfway to its second point or to the first element it meets | freely, around the start (arc) | the second point turned around the first, whole degrees (clockwise on screen), length kept |

Every grip carries a bilingual label with its current value ("Focal length f: 40 mm" / "Brennweite f: 40 mm", "Half cone
angle: 12°", "Start angle: 30°"). The grips of lenses and mirrors belong to the part `focal` when the marks are drawn
(selecting the focal points shows them). Emitter grips only appear while a ray bundle is docked (otherwise there is no cone
to see); a ray docked on an emitter takes its direction from the emitter (turn the emitter) and has no grip of its own;
an untraced ray is edited by its points. Prisms and gratings have no grips (no parameter maps onto a point of the drawing
naturally); the angle of a grating's first order is set in its number field.

## Snapping anchors

- Every element exposes its optical centre as a `Port` anchor (`o`; mirrors: vertex; detectors and screen: front face;
  sources: `exit`, lamp/LED also `o`; fibre: `in`, `exit`; object arrow: `foot`, `tip`).
- Lenses: `f1`/`f2` at ±f and `2f1`/`2f2` at ±2f on the axis; curved mirrors: `f` and `cc` (centre of curvature).
  Port-to-port snapping lines up focal points (telescopes: F′ of the objective on F of the eyepiece).
- The optical axis (`optics.axis.line`) exposes its segment as a `Rail` (`axis0` …): element ports dock anywhere on it,
  so elements line up on one axis. No `Hub`/`Axis` roles are used, so no mechanical coupling or "+" build ports appear.

## Parts and cleaning

Part keys (`PartLooks`): `body`, `focal`, `hatch`, `detail`, `label` on elements; `image`, `construction` and `names`
on the object arrow (listed while drawn); `ray` (all rays, each `ray.<i>`) and `virtual` on rays; `axis` on the optical
axis. They can be restyled or hidden individually.

Clean slides (`Sanitize`): optics elements carry no values worth hiding; the generic pass replaces free-text labels
(designators such as `L1` stay). Focal lengths, indices and positions are kept.

## Persistence

All state is in the document model: parameters, `Size`, `Transform`, the ray's points and the attachment of its first
point (`AnchorId` = `exit`/`o`/`tip`). The traced paths, images, construction rays and grips are not stored; they are
recomputed from the document, so JSON round trips render identically.

## Host notes

- Everything is drawn with native primitives (lines, polylines, polygons, rectangles, ellipses, text, line-end
  arrowheads); no vector pictures are needed.
- Rays depend on neighbours: hosts that keep objects in separate units (PowerPoint COM) must render rays with the other
  units of the slide as context and redraw the dependents reported after an edit, as they already do for walls and lines.
- **Add ray bundle** inserts a new object (`ContextAction.ApplyInDocument`). The engine (Office web, LibreOffice) applies
  that, and the PowerPoint COM add-in inserts the new objects of an action result as new units. The library item
  "Light ray (traced)" docked by dragging its start onto a source works in every host.
- The stored points of a docked ray are not updated by the trace; hosts that show point handles show the docked start
  and the (unused) second point.
- **Object arrows with an image depend on neighbours** like rays: render them with the slide's other units as context and
  redraw them when a lens or mirror they image through is edited (they are reported as its dependents). The image lies
  outside the arrow's box, so the arrow's native group grows with it (like a lens with its focal point marks).
- **Grips:** the COM add-in shows and drags the grips of any type generically (preview of the dragged element, one undo
  step on release, dependents redrawn then). Rays and images in *other* units follow on release, not during the drag: a
  live follow while dragging a grip or moving a source, lens or mirror needs the host to re-render the reported
  dependents with the moved element in its drag preview (open item). The engine's
  `moveHandle` (web, LibreOffice) recognises a change by parameters and size only, so the free ray's direction grip, which
  changes the ray's points, needs `moveHandle` to compare the points too.

## Limits and next steps

- Two-dimensional, sequential, paraxial ideal lenses and mirrors; no thick lenses, aberrations, intensities,
  polarisation or interference.
- The fibre does not guide light from its input to its output; a bundle on the output end starts a new cone.
- Light reaching the inside of an element from an unusual side (e.g. through the side of a lens) is treated by the
  element's single optical surface.
- The image construction uses the first lens or curved mirror on the arrow's axis only (no image of an image through a
  second element); the traced ray bundle does pass every element.
- Possible next steps: images through several elements, thick lenses with two surfaces, Michelson/Mach-Zehnder templates.
