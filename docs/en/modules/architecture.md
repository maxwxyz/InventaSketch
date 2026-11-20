# Floor plans & building module (`architecture`)

Schematic house planning for slides and quick technical sketches: walls with clean joints, rooms with area,
doors, windows, stairs, columns, beams, furniture, sanitary, heating/plumbing symbols and site marks.
It is **not BIM**: there is no building model behind the plan, only well-drawn, editable symbols.

Electrical floor-plan symbols live in the Electrical module, dimensions in the Annotation module; this module
does not duplicate them.

## Plan scale

Every object can be inserted without parameters. Library sizes are authored for **1:100** (1 mm on the page is
100 mm in reality). Objects that use real dimensions have an optional advanced parameter `scale` (the
denominator, default 100):

| Quantity | Conversion |
|---|---|
| Wall thickness, beam width | `thickness / scale` page mm (240 mm at 1:100 = 2.4 mm) |
| Room area | polygon area in page mm² x `scale²`, shown in m² |
| Door/window width | box width x `scale` (shown in the width actions) |

Free scaling of box objects changes `Size` only, never parameters.

## Object types

| Id | Name (EN / DE) | Geometry | Views | Notes |
|---|---|---|---|---|
| `arch.wall` | Wall / Wand | points (centre line) | plan, elevation | `thickness` (240), `kind` exterior/interior/partition, `closed`, `height`, `scale` |
| `arch.room` | Room / Raum | points (polygon) | plan | name (text or `function`), area in m², tinted floor |
| `arch.beam` | Beam / Unterzug | points | plan | dashed outline + axis, `width` |
| `arch.column` | Column / Stütze | box | plan | square/round; concrete (solid), masonry (hatched), steel (I-section) |
| `arch.door.single` / `.double` / `.sliding` / `.opening` | Doors / Türen | box | plan, elevation | `hingeRight`, `swingOut`, swing arc |
| `arch.window.single` / `.double` / `.french` | Windows / Fenster | box | plan, elevation | `hingeRight`, `flip` (inside/outside), sill |
| `arch.stair.straight` / `.l` / `.u` / `.spiral` | Stairs / Treppen | box | plan | `steps`, `down` (walking line reversed) |
| `arch.furniture.*` | bed, sofa, table, desk, wardrobe, counter, sink, stove, fridge | box | plan | bed `pillows`, sofa `seats`, table `chairs`/`round`, sink `basins` |
| `arch.sanitary.*` | WC, washbasin, shower, bathtub | box | plan | |
| `arch.hvac.radiator` / `.heatpump` / `.boiler` | heating symbols | box | plan | heat pump label HP/WP by UI language |
| `arch.hvac.pipe` | Pipe / Rohrleitung | points | plan | `medium` flow/return/cold/hot/waste/gas (colour + dash), `arrow` |
| `arch.site.north` | North arrow / Nordpfeil | box | plan | |
| `arch.site.level` | Level marker / Höhenkote | box | plan | `level` in m (`±0.00`, `+2.75`; decimal comma in German), text overrides |
| `arch.site.section` | Section marker / Schnittlinie | points | plan | letter, `flip` view direction, `full` line |
| `arch.site.boundary` | Site boundary / Grundstücksgrenze | points | plan | dash-dot-dot polygon, boundary point markers |
| `arch.site.parking` | Parking space / Stellplatz | box | plan | 2.5 x 5 m |
| `arch.site.tree` | Tree / Baum | box | plan | deciduous, conifer, shrub |

Views carry abstract icons: `plan` uses `IconIds.Plan`, `elevation` uses `IconIds.Front`.

## Walls

A wall is a polyline of centre-line points. It renders as two face lines (wide continuous line) with the
kind-specific core between them: exterior = 45° hatch on light grey, interior (load-bearing) = 45° hatch,
partition = solid light grey. Corners inside one wall are mitred; a `closed` wall forms a loop (also when the
last point returns to the first).

Joints between separate walls on the same page are resolved at render time:

- **L joint**: exactly one other wall ends at the same point (within 0.25 mm) - both ends are mitred and
  share the corner points, no end lines.
- **T joint**: a wall end lies on another wall's centre line - the end is trimmed to the face of that wall and
  the face line of the through wall is interrupted, so the cores merge.
- **Several walls** at one point: ends stay open (no end line).
- **Free end**: closed with an end line.

Snapping anchors: every point is an `End` (from the registry) and a `Mount`; every segment is an `Axis`.
Wall ends therefore snap onto other wall ends/vertices, and openings snap onto wall axes.

## Doors and windows

Box objects whose local x axis runs along the wall. The `Hub` anchor sits at the box centre and snaps onto a
wall `Axis` (rotate the opening to match vertical walls). Inside the opening an opaque white band hides the
wall; its thickness is taken from the wall under the centre or, off a wall, from the optional `wallThickness`
parameter. The opening must therefore lie above the wall in z-order (inserted later). On coloured slide
backgrounds the white band is visible outside the wall.

- Single door: leaf + quarter swing arc (elliptical when the box is stretched); hinge side and swing side
  are flags.
- Double door, sliding door (two panels and direction arrow), opening without door (lintel dashed).
- Windows: reveal lines, double glass line, outside sill; double casement with mullion; French window with two
  swinging leaves. Elevations show frames, glass and the dashed opening triangle whose apex points to the
  hinge side.

## Context actions

| Type | Actions |
|---|---|
| Wall | thicker/thinner (standard thicknesses 75 ... 490 mm), cycle kind, closed loop |
| Doors | flip hinge side, flip swing direction, wider/narrower (±125 mm; hinged leaves grow with the width) |
| Windows | flip hinge side, swap inside/outside, wider/narrower |
| Stairs | reverse walking direction, more/fewer steps |
| Room | show area, tinted floor |
| Column, tree, pipe | cycle shape/material, kind, medium; pipe flow arrow |
| Section marker, site boundary | flip view direction; closed, boundary markers |

The width/thickness actions use the icon ids `cmd-plus` / `cmd-minus`, which are not yet part of `IconIds`
(hosts fall back to the neutral icon).

## Host notes

All output uses native primitives (lines, polylines, polygons, rectangles, ellipses, cubic paths, text) and
explicit hatch lines, so every host can draw it natively. Walls and openings render context-dependently
(neighbouring walls, host wall thickness): when a wall or opening changes, hosts should redraw the walls and
openings on the same page, not only the changed object.

## Tests and gallery

`tests/InventaSketch.Modules.Architecture.Tests` covers registration, bilingual names, every box item inside
its box (also stretched) and point items near their points, wall joints (L, T, closed), endpoint snapping of
walls, opening hubs on wall axes, room area, level texts, context actions and the JSON round trip. The gallery
test writes `gallery/*.html` (all types, presets, joint study and a sample house in English and German).
