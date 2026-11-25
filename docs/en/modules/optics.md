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
| `optics.object.arrow` | Object arrow standing on the axis (imaging figures) | `bundle` principal rays/fan, `rays`, `label` |
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
(`ObjectType.Dependents`, `DependsOnNeighbours`), so moving, rotating, scaling or changing an element redraws the rays in
the same undo step; the docked start point follows its emitter through the ordinary attachment. Nothing has to be
re-traced by hand.

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

## Snapping anchors

- Every element exposes its optical centre as a `Port` anchor (`o`; mirrors: vertex; detectors and screen: front face;
  sources: `exit`, lamp/LED also `o`; fibre: `in`, `exit`; object arrow: `foot`, `tip`).
- Lenses: `f1`/`f2` at ±f and `2f1`/`2f2` at ±2f on the axis; curved mirrors: `f` and `cc` (centre of curvature).
  Port-to-port snapping lines up focal points (telescopes: F′ of the objective on F of the eyepiece).
- The optical axis (`optics.axis.line`) exposes its segment as a `Rail` (`axis0` …): element ports dock anywhere on it,
  so elements line up on one axis. No `Hub`/`Axis` roles are used, so no mechanical coupling or "+" build ports appear.

## Parts and cleaning

Part keys (`PartLooks`): `body`, `focal`, `hatch`, `detail`, `label` on elements; `ray` (all rays, each `ray.<i>`) and
`virtual` on rays; `axis` on the optical axis. They can be restyled or hidden individually.

Clean slides (`Sanitize`): optics elements carry no values worth hiding; the generic pass replaces free-text labels
(designators such as `L1` stay). Focal lengths, indices and positions are kept.

## Persistence

All state is in the document model: parameters, `Size`, `Transform`, the ray's points and the attachment of its first
point (`AnchorId` = `exit`/`o`/`tip`). The traced paths are not stored; they are recomputed from the document, so JSON
round trips render identically.

## Host notes

- Everything is drawn with native primitives (lines, polylines, polygons, rectangles, ellipses, text, line-end
  arrowheads); no vector pictures are needed.
- Rays depend on neighbours: hosts that keep objects in separate units (PowerPoint COM) must render rays with the other
  units of the slide as context and redraw the dependents reported after an edit, as they already do for walls and lines.
- **Add ray bundle** inserts a new object (`ContextAction.ApplyInDocument`). The engine (Office web, LibreOffice) applies
  that; the PowerPoint COM add-in currently keeps only the selected unit's own objects from an action result, so there the
  action has no visible effect until the add-in inserts new objects of an action result as new units. The library item
  "Light ray (traced)" docked by dragging its start onto a source works in every host.
- The stored points of a docked ray are not updated by the trace; hosts that show point handles show the docked start
  and the (unused) second point.

## Limits and next steps

- Two-dimensional, sequential, paraxial ideal lenses and mirrors; no thick lenses, aberrations, intensities,
  polarisation or interference.
- The fibre does not guide light from its input to its output; a bundle on the output end starts a new cone.
- Light reaching the inside of an element from an unusual side (e.g. through the side of a lens) is treated by the
  element's single optical surface.
- Possible next steps: image arrow construction (real/virtual image drawn automatically), thick lenses with two
  surfaces, Michelson/Mach-Zehnder templates.
