# Floor plans & building module (`architecture`)

Schematic house planning for slides and quick technical sketches: walls with clean joints, rooms with area,
doors, windows, stairs, ramps, lifts, railings, shafts, slab openings, roof plans, columns, beams, furniture,
sanitary, heating/plumbing symbols and site marks.
It is **not BIM**: there is no building model behind the plan, only well-drawn, editable symbols.

Electrical floor-plan symbols live in the Electrical module, dimensions in the Annotation module; this module
does not duplicate them.

## Plan scale

Every object can be inserted without parameters. Library sizes are authored for **1:100** (1 mm on the page is
100 mm in reality). The scale denominator of an object is resolved in this order:

1. its own optional advanced parameter `scale` (unset for new objects; older documents may carry `scale = 100`),
2. the **document plan scale** `DocumentSettings.PlanScale` (engine method `setPlanScale`, shown in the inspector
   as "Plan scale (document) 1:" whenever a selected type uses it),
3. 1:100.

Changing the document plan scale redraws every object that uses it (walls, rooms, beams, doors, windows) in one
undo step.

| Quantity | Conversion |
|---|---|
| Wall thickness, beam width | `thickness / scale` page mm (240 mm at 1:100 = 2.4 mm) |
| Room area | polygon area in page mm² x `scale²`, shown in m² (also the room value `A`) |
| Railing post spacing | `spacing / scale` page mm |
| Door/window width | box width x `scale` (shown in the width actions and the "Width…" prompt) |

Free scaling of box objects changes `Size` only, never parameters.

## Object types

| Id | Name (EN / DE) | Geometry | Views | Notes |
|---|---|---|---|---|
| `arch.wall` | Wall / Wand | points (centre line) | plan, elevation | `thickness` (240), `kind` exterior/interior/partition, `closed`, `height`, `scale` |
| `arch.room` | Room / Raum | points (polygon) | plan | name (text or `function`), area in m², tinted floor; values A, h, V (see [Room values](#room-values)) |
| `arch.beam` | Beam / Unterzug | points | plan | dashed outline + axis, `width` |
| `arch.column` | Column / Stütze | box | plan | square/round; concrete (solid), masonry (hatched), steel (I-section) |
| `arch.door.single` / `.double` / `.sliding` / `.opening` | Doors / Türen | box | plan, elevation | `hingeRight`, `swingOut`, swing arc |
| `arch.window.single` / `.double` / `.french` | Windows / Fenster | box | plan, elevation | `hingeRight`, `flip` (inside/outside), sill |
| `arch.stair.straight` / `.l` / `.u` / `.spiral` | Stairs / Treppen | box | plan | `steps`, `down` (walking line reversed) |
| `arch.lift` | Lift (shaft with car) / Aufzug (Schacht mit Kabine) | box | plan | shaft outline, car with diagonal cross, `counterweight`, `door` (two-panel landing door); 2.0 x 2.2 m |
| `arch.ramp` | Ramp / Rampe | box | plan | outline, arrow up the slope (start circle as on stairs), `slope` in % (6, shown as "6 %", decimal comma in German), `down`, `showSlope`; runs along the longer box side |
| `arch.railing` | Railing / Geländer | points | plan | medium line with posts at the vertices and every `spacing` (1000 mm at the plan scale), `posts`, `scale`; rails per segment |
| `arch.shaft` | Shaft (building) / Schacht (Gebäude) | box | plan | rectangle with solid diagonal cross (`cross`) |
| `arch.void` | Slab opening (void) / Deckenöffnung (Luftraum) | box | plan | thin outline with dashed (hidden-line) diagonals |
| `arch.roof` | Roof plan / Dachaufsicht | box | plan | `kind` gable / hip, `ridge` along the long or short side (gable), `arrows` (fall arrows), `walls` (outer walls below, dashed) |
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

Because joints are computed from the neighbours, walls declare `DependsOnNeighbours` and report their
**dependents** (`ObjectType.Dependents`): the walls joined to them (L/T joints in both directions) and the doors and
windows whose centre sits on them. The engine returns these ids with every edit (`dependents` in the editor state),
so hosts redraw them in the same undo step (see [overview](../architecture/overview.md#7-engine-api-actions-change-sets-and-document-settings)).

Snapping anchors: every point is an `End` (from the registry) and a `Mount`; every segment is an `Axis`.
Wall ends therefore snap onto other wall ends/vertices, and openings snap onto wall axes.

## Doors and windows

Box objects whose local x axis runs along the wall. The `Hub` anchor sits at the box centre and snaps onto a
wall `Axis` (rotate the opening to match vertical walls). Inside the opening an opaque white band hides the
wall; its thickness is taken from the wall under the centre or, off a wall, from the optional `wallThickness`
parameter. The opening must therefore lie above the wall in z-order (inserted later). On coloured slide
backgrounds the white band is visible outside the wall.

**Align to wall** (document-aware context action): rotates the opening to the direction of the nearest wall
segment within reach (wall half thickness plus half the smaller box side) and centres it on the wall axis. Of the
two wall directions the one closer to the current rotation wins, so the side the door opens to is kept. Without a
wall in reach the action is offered but greyed out.

**Views and size:** plan and elevation have their own preferred sizes (doors 2.10 m high, windows 1.25 m, French
windows 2.20 m in elevation). Switching the view keeps the real width; the other side takes the preferred size of
the target view at the object's plan scale (in plan, hinged leaves add their extra width as depth, like the width
steps).

- Single door: leaf + quarter swing arc (elliptical when the box is stretched); hinge side and swing side
  are flags.
- Double door, sliding door (two panels and direction arrow), opening without door (lintel dashed).
- Windows: reveal lines, double glass line, outside sill; double casement with mullion; French window with two
  swinging leaves. Elevations show frames, glass and the dashed opening triangle whose apex points to the
  hinge side.

## Lifts, ramps, railings, shafts, openings and roofs

- **Lift:** the shaft outline (medium line), the car as a thin rectangle with a diagonal cross, the counterweight as a
  grey block behind the car and a two-panel sliding landing door in front of it. Without counterweight or door the car
  takes the space.
- **Ramp:** outline, a walking line from a start circle to an arrow pointing up the slope (`down` reverses it) and the
  slope in percent above it. Upright boxes run along y.
- **Railing:** a point object like walls and pipes (corners, extending, rails per segment for snapping); posts are
  small filled dots at every vertex and evenly spaced at about `spacing` real millimetres in between.
- **Shaft / slab opening:** rectangle with a solid diagonal cross (shaft) or dashed diagonals (opening in the slab, void).
- **Roof plan:** the eaves outline and the ridge; a hip roof adds the four hips at 45° in plan, i.e. equal pitches on
  all faces, so its ridge runs along the longer side and is (length − width) long, a square gives a pyramid roof. Fall
  arrows point down each face. The ridge of a gable roof can run along either side.

None of these overlap with the building services or maps modules, so they carry no `Alternative`.

## Room values

Rooms are elements with values (`IValueType`, see the engineering calculations design): floor area `A` (m²), room
height `h` (m) and volume `V` (m³) with the relation V = A·h. The area comes from the outline: the room reports a
relation of its own (`IValueLinks`, "Floor area from the outline at the plan scale") that sets `A` to the polygon
area times the plan scale squared (own `scale`, else `DocumentSettings.PlanScale`, else 1:100). The height is the
optional number parameter `height` (m) without a default value, so that the value default h = 2.5 m applies until a
height or a volume is entered; an entered volume (text parameter `volume`, e.g. "33 m³") then gives the height. An
entered area that contradicts the outline is reported as a conflict. `showValues` draws the values label below the
room. Example: a 40 x 30 mm outline at 1:100 is 4 m x 3 m, A = 12 m², with h = 2.5 m V = 30 m³.

The keys and quantities (`A` area, `h` length, `V` volume) are those of the building services' room values (air
terminal), so a later cross-module join (`NetKeys`) can pass them on; Core was not changed for this.

## Context actions

| Type | Actions |
|---|---|
| Wall | thicker/thinner (standard thicknesses 75 ... 490 mm), "Thickness…" (input, mm), wall kind ▾ (choice), closed loop |
| Doors | flip hinge side, flip swing direction, wider/narrower (±125 mm; hinged leaves grow with the width), "Width…" (input, mm), align to wall |
| Windows | flip hinge side, swap inside/outside, wider/narrower, "Width…" (input, mm), align to wall |
| Stairs | reverse walking direction, more/fewer steps |
| Lift, ramp, railing, shaft | counterweight, landing door; reverse direction, show slope; posts; diagonal cross |
| Roof plan | roof shape ▾ (choice), fall arrows, outer walls below |
| Room | "Name…" (input), show area, tinted floor |
| Column, tree, pipe | shape ▾ / material ▾, kind ▾, medium ▾ (choices; the medium options show their line); pipe flow arrow |
| Section marker, site boundary | flip view direction; closed, boundary markers |

Input actions ("…") ask for a value: the COM add-in opens its prompt dialog, the web task pane an inline field.
Lengths accept `1010`, `1010 mm`, `101 cm`, `1,01 m` or `1.01 m`; anything else is rejected with a message and
changes nothing. The width/thickness steps use `IconIds.Increase` / `IconIds.Decrease`, the inputs `IconIds.Text`
and align to wall `IconIds.Rotate`.

## Host notes

All output uses native primitives (lines, polylines, polygons, rectangles, ellipses, cubic paths, text) and
explicit hatch lines, so every host can draw it natively. Walls and openings render context-dependently
(neighbouring walls, host wall thickness):

- **Web task pane:** the live sync redraws the changed units plus the engine's `dependents`.
- **PowerPoint COM:** every unit keeps its own model; units containing walls or openings are rendered with the
  other units of the slide as context (only the unit's own objects are stored), and after an edit, insert, move or
  delete the dependent units are redrawn in the same undo entry. Native PowerPoint moves (handles, arrow keys) do
  not redraw dependents until the next InventaSketch edit.

## Tests and gallery

`tests/InventaSketch.Modules.Architecture.Tests` covers registration, bilingual names, every box item inside
its box (also stretched) and point items near their points, wall joints (L, T, closed), endpoint snapping of
walls, opening hubs on wall axes, room area, level texts, context actions and the JSON round trip; `BuildingTests`
covers lift, ramp (slope text in both languages, direction, upright), railing posts at the spacing and plan scale,
shaft and opening diagonals, gable and hip roofs, and the room values (12 m² and 30 m³ for a known rectangle at 1:100,
entered height, document and own plan scale, entered volume giving the height, conflicting area, values label, JSON);
`DocumentAwareTests` covers align to wall, dependents, the document plan scale, input actions and view sizes. The gallery
test writes `gallery/*.html` (all types, presets, joint study and a sample house in English and German).
