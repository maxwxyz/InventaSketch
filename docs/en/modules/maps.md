# Maps & routes module (`maps`)

Schematic map sketches for slides: a map frame, regions, contour lines, place labels, roads, rail lines and rivers,
stations on lines, bridges and tunnels on roads, a route with waypoints, direction arrows and values (length at the map
scale, speed, travel time), start/finish markers, numbered stops, points of interest, distance/time callouts, scale
bar, north arrow, compass rose and a legend. It is **not GIS**: there are no geographic coordinates, map
projections or imported map data behind the drawing, only well-drawn, editable symbols. (Import of GeoJSON/GPX/KML
from the specification is not part of this module.)

Every object is insertable without parameters (Quick Sketch). Box symbols are drawn inside their box and stretch
with free scaling; scaling changes `Size`, never parameters (a scale bar keeps representing "1 km" whatever its
drawn length). Lines (roads, rail, rivers, routes, regions) are point objects in page coordinates.

## Object types

| Id | Name (EN / DE) | Geometry | Views | Notes |
|---|---|---|---|---|
| `map.frame` | Map frame / Kartenrahmen | box | plan | `style` plain / grid / terrain, `cells` (grid columns, rows follow the aspect), `labels` (A, B … / 1, 2 …), `tint` (land colour); double neat line |
| `map.region` | Region / Gebiet | points (closed polygon) | plan | `kind` area, forest, water, park, urban, restricted (geofence); `fill` tint / hatch / none; `smooth`; name text at the centroid |
| `map.place` | Place label / Ortsbeschriftung | box | plan | `rank` capital, city, town, village, locality (dot and font by rank), `dot`; text = name, default = rank name in the UI language |
| `map.road` | Road / Straße | points (centre line) | plan | `kind` motorway (4.2 mm, divider), main (3 mm), side (2 mm), path (dashed line); `smooth`, `bridge`, optional `width`; text = road number (shield for motorway/main) or name |
| `map.rail` | Rail line / Bahnstrecke | points | plan | `kind` rail (black/white band) or tram (line with sleepers); `smooth`; text = name |
| `map.river` | River / Fluss | points | plan | `kind` river (band with water lines), stream (line), canal (straight band); `smooth` (default on); optional `width`; italic name |
| `map.route` | Route / Route | points | plan | `style` solid / dashed (alternative) / dotted (walk), `arrows` along / end / none, `waypoints` (dots), `smooth`, `width`; white halo; text = route label; values: `mode` walk / bike / car, `speed`, `length`, `time`, `scale` (1:N), `showValues` (see "Route values") |
| `map.station` | Station / Station | box | symbol | `kind` railway station (white disc, black ring), interchange (white capsule), tram stop (small ring), bus stop (dark badge with bus pictogram); `name`; text = station name (default: the kind in the UI language), drawn above the line right of the symbol |
| `map.bridge` | Bridge / Brücke | points (section of a road) | plan | follows the road under its end points (also around bends) with the road's width and colours: deck, edges and parapets with flared wings; optional `width` (without a road: 3 mm); text = name |
| `map.tunnel` | Tunnel / Tunnel | points (section of a road) | plan | like the bridge: the section is covered in the land colour, edges dashed, portal marks at both ends; optional `width`; text = name |
| `map.contour` | Contour line / Höhenlinie | points (open or closed) | plan | `elevation` (m), `index` (index contour, thicker), `smooth` (default on), `closed` (hill or hollow), `label` (elevation written into a gap of the line, upright) |
| `map.marker` | Route marker / Routenmarker | box | pin, symbol | `kind` start (go triangle), finish (chequered flag), via (hollow pin); text replaces the glyph (e.g. "A", "B") |
| `map.stop` | Numbered stop / Nummerierter Halt | box | symbol | `number`, `shape` circle / square; text overrides the number |
| `map.callout` | Distance/time callout / Entfernungs-/Zeitangabe | box | plan | `distance` + `unit` (km, mi, m), `time` (minutes, shown as "1 h 35 min"), `mode` car / walk / bike / public transport / none, `tail` left / right / none; text replaces the two lines |
| `map.poi` | Point of interest / Ort von Interesse (POI) | box | symbol, pin | `kind` pin (location), building, parking, fuel, charging, hospital, airport, harbour, station |
| `map.scalebar` | Scale bar / Maßstabsleiste | box | plan | `length` + `unit` (km, mi, m, ft), `segments` (1–10), `style` alternating bar / ticked line; labels thinned out automatically |
| `map.north` | North arrow / Nordpfeil | box | plan | split arrowhead, `letter` "N" |
| `map.compass` | Compass rose / Windrose | box | plan | `points` 8 / 4, `labels` (German: N, O, S, W) |
| `map.legend` | Legend / Legende | box | plan | `title` (default "Legend" / "Legende"), `auto`; entries see below |

Views carry abstract icons: `plan` uses `IconIds.Plan`, `symbol` uses `IconIds.Symbol`. the `pin` view uses `IconIds.Pin`.

Pictograms (POIs, markers, transport modes) are drawn independently from simple primitives; no third-party icon set
is used. The hospital uses an "H" sign, deliberately not the Red Cross emblem (protected under the Geneva
Conventions).

## Route values and map scale (2026-11-26)

A route is a value element (`IValueType`): **route length `s`** (km), **mean speed `v`** (km/h) and **travel time `t`**
(min) with the relation `map.route.time`: t = s / v (definition of the mean speed, source S2), solvable for each of the
three. "Show values" (`showValues`, library item "Route with length and travel time") draws "s = 4.72 km, v = 50 km/h,
t = 5.66 min" below the route.

- **Length:** the drawn length (along the curve when smoothed) times the map scale, unless a length is entered (a sketch
  is rarely true to scale; the entered road distance wins). The map has no scale of its own; it is taken, in this order,
  from the route's `scale` parameter (map scale 1:N), from the **first scale bar on the page** (its box width represents
  its length; km, mi, m and ft are converted) or from the document plan scale. Without any of them the length is unknown
  (and with it the travel time) until it is entered.
- **Speed:** the preset of the travel mode `mode` – walking 5 km/h, bicycle 15 km/h, car 50 km/h (typical planning
  values, no standard) – unless a speed is entered, or an entered time and a known length determine it. "Walking route
  (dotted)" presets walking, "Cycle route" the bicycle.
- **Travel time:** derived; entering it gives the speed (or, with the mode preset, the length).
- The route depends on its neighbours (it reads the scale bar); the scale bar reports the routes on its page that show
  their values as dependents (`HasDependents`), so changing the scale bar redraws their labels.
- The travel mode is a choice in the floating bar. Option pictograms (walker, bicycle, car) need new ids in the shared
  `IconIds` vocabulary (Core) and host drawings; until then the options are shown as text.

## Stations, bridges, tunnels and contour lines (2026-11-26)

- **Station** (`map.station`): its symbol centre is a `Port` (`loc`) that docks anywhere onto a rail line, road or route;
  the name sits above the line so the line does not cross it. Stations on a route count as stops in the route stop
  export (kind "Station: Railway station" …).
- **Bridge / tunnel** (`map.bridge`, `map.tunnel`): two points (more for curved sections) whose ends dock onto a road
  (rail rule). The section follows the centre line of the road beneath between the projections of its end points and
  takes its width (motorway 4.2 mm, main road 3 mm …). The bridge draws the deck in the road colour with the road's edge
  lines, so the road passes over a river or rail drawn above it, plus parapets 0.5 mm outside the edges with flared
  wings. The tunnel covers the section in the land colour (`MapTags.Mask`, kept under style overrides), draws dashed
  edges and a portal bar with ears at both ends. Both depend on their neighbours; a road reports the bridges and
  tunnels near it as dependents. (The road's own `bridge` switch stays for a road passing over other roads.)
- **Contour line** (`map.contour`): smooth point line in the terrain colour, open or closed; the elevation ("250", no
  unit as on maps) is written upright into a gap at the middle of the line (the line is cut around the label, one
  native freeform). The centre line is a rail. Library: contour line, index contour (500 m), hill (closed contour).

## Roads and junctions

A band road renders as one filled polygon (the band, no outline) plus one path with its edge lines and end lines,
so it stays two native shapes. Where the band of another road on the page overlaps, the edge lines are interrupted
exactly at the other road's edges, and an end lying inside another road gets no end line. Crossing roads and
T-junctions therefore look like real junctions in any z-order; the fill of the upper road covers the lower one.
A road with `bridge` keeps its edges and does not interrupt others (place it above the roads it crosses). Roads
declare `DependsOnNeighbours` and report the roads near them as **dependents**, so hosts redraw both sides of a
junction in the same undo step. Paths (dashed lines) take no part in junctions.

## Snapping and docking

- Roads, rail lines, rivers and routes expose their centre line as `Rail` segments (curves as up to 24 chords).
- Markers (pin tip / badge centre), POIs, stops (centre), place labels (dot) and callouts (tail tip) expose a `Port`
  named `loc` (callout: `tip`).
- Moving a marker, POI or stop docks its port anywhere on a road, rail, river or route (rail rule). Dragging a route
  or road point docks it onto a centre line (rail) or onto a marker/stop/POI port; a point docked onto a port is
  attached (`Attachment` with anchor id `loc`) and follows the symbol when it moves.
- Regions offer their centroid as a `Center` point (for labels and POIs).
- Stations expose their symbol centre as the `Port` `loc`; bridges and tunnels their ends as `End` points; contour
  lines their line as `Rail` segments.

## Context actions

| Type | Actions |
|---|---|
| Road | road type ▾ (choice: motorway, main, side, path; the current one marked), smooth curve, bridge |
| Rail, river | line/water type ▸, smooth curve |
| Route | route style ▸ (solid → dashed → dotted), direction arrows ▸, travel mode ▾ (walk, bike, car), waypoint dots, smooth curve, reverse direction |
| Station | show name, station type ▾ |
| Contour line | "Elevation…" (input: `250`, `1250,5 m`), index contour, elevation label, smooth curve |
| Region | area type ▸, fill ▸ (tint → hatch → none), smooth outline |
| Marker, POI | marker ▸ / place type ▸ |
| Stop | number +1 / −1 (not below 0), shape ▾ |
| Callout | "Distance…" (input, accepts `12.5 km`, `8 mi`, `500 m`), "Travel time…" (input: `90`, `1:30`, `1 h 30`, `2h`, `45 min`), unit ▸, mode ▸, pointer ▸ |
| Place label | place rank ▸, settlement dot |
| Scale bar | unit ▸, "Length…" (input: `2 km`, `1,5 mi`, `500 m`, plain number keeps the unit), more / fewer segments, style ▸ |
| Map frame | background ▸, grid labels, land tint, more / fewer grid cells |
| North arrow, compass rose | show "N"; points ▸, cardinal letters |
| Legend | list the map objects (auto), "Title…" (input) |

Each thing is offered once (interaction concept, section 9 item 4): points of roads, rails, rivers, routes and regions
are added with the hosts' corner dots (click or drag on a segment) and removed with the corner commands (select a
corner: "Remove", or double-click it); the views of markers and POIs (pin / symbol) are the hosts' view dropdown, which
keeps the drawing scale through the views' design sizes (`ViewDef.Size`, `ObjectType.WithView`). The former
"Add/Remove point" and `view.*` actions repeated them and were removed. Invalid inputs throw `FormatException` with a bilingual message and change nothing. Steps use
`IconIds.Increase` / `IconIds.Decrease`, choices one `IconIds.Option` per option (`ActionChoice`), toggles `IconIds.Toggle`, inputs `IconIds.Text`,
reverse `IconIds.Swap`.

## Importing GeoJSON and GPX (2026-11-25)

"Import …" (COM: Review group; web: import button; engine `importGeo`) turns a GeoJSON file (Feature,
FeatureCollection or geometry: line strings, polygon rings, points) or a GPX file (tracks, routes, waypoints) into map
elements fitted into the slide (inset by a tenth, north up, equirectangular projection around the data's mean latitude
– right for city and region maps, not for continents): every line becomes a route (dense tracks thinned to points
0.5 mm apart), every point a waypoint marker with its name; a file with lines only gets start and finish markers. Each
element is its own unit and stays editable (`Imports.ParseGeo`, `Imports.GeoElements`). Checked in PowerPoint (scenario
`import`: a GPX run with start and finish).

## Legend

Entries come, in this order of precedence, from

1. the object **text**: one entry per line, optionally with a sample key in brackets
   (`[road.motorway] A8`, `[poi.parking] P+R`, lines without a key get a bullet). Keys: `route`, `route.dashed`,
   `route.dotted`, `road.<kind>`, `rail.<kind>`, `river.<kind>`, `region.<kind>`, `marker.<kind>`, `stop`,
   `station.<kind>`, `bridge`, `tunnel`, `contour`, `poi.<kind>`, `place.<rank>`;
2. the **map objects on the same page** (`auto`, default): routes (route text as label), markers, stops, stations,
   POIs, roads, bridges, tunnels, rail, rivers, regions and contour lines, each kind once, with bilingual labels, at
   most 16;
3. a default set (route, motorway, main road, start, finish, location) when neither applies.

Samples are drawn by the real map types (fitted into the sample cell), so the legend always matches the map. An
automatic legend declares `DependsOnNeighbours`; every map object reports the automatic legends on its page as
dependents, so the legend is redrawn with every change of the map.

## Monochrome

Every solid fill is clearly dark (luminance < 0.3, becomes black) or clearly light (> 0.45, becomes white):
POI/marker/stop badges turn black with white glyphs, the via marker white with a black glyph. Line features stay
distinguishable by width, dashes and structure (motorway divider, rail band, water lines). Regions switch their
tint to the hatch of their kind (forest: dots) when the document or the render context is monochrome. The tunnel cover
is light (becomes white); bridge parapets and tunnel portals are dark lines.

## Domain export: route stops (CSV)

`map.route.stops` (module implements `IExportProvider`, applies when a route is selected). For each selected route:
start, every route point that carries a marker, stop, POI or place label (docked, or its location within 3 mm) and
the end, in route order, with name, kind, leg and cumulative distance along the drawn route (along the curve when
smoothed) and page coordinates. Distances use the scale bar on the page (box width = represented length, unit
of the scale bar), otherwise millimetres on the page. Columns: `Route,No,Name,Kind,Leg (unit),Total (unit),X (mm),Y (mm)`
(German headers in German UI). **Preserves:** order of stops, their names and kinds, distances; **not:** geographic
coordinates. A GPX export was deliberately left out: sketch maps have no latitude/longitude, and inventing them
would be fake data.

## Host notes

All output uses native primitives: polygons and freeforms (bands, regions, pins, glyphs), multi-sub-path freeforms
for interrupted edges, dashes, sleepers and chevrons (hosts without freeforms fall back to line shapes), ellipses,
rectangles, cubic paths (smooth lines) and text. No vector picture is needed. Region hatches and dot fills use the
core fill kinds. Roads and automatic legends depend on neighbours; in the PowerPoint COM host they are rendered with
the other units of the slide as context (see the architecture overview). Routes (they read the scale bar), bridges
and tunnels (they read the road beneath) also depend on neighbours. The tunnel cover uses the default land colour; on
a map frame with another `tint` or over a region it shows as a light band.

## Tests and gallery

`tests/InventaSketch.Modules.Maps.Tests` covers registration, bilingual names/keywords/actions, view icons, every
box item in every view inside its box (also stretched and with long texts), point items near their points, road
widths and junctions (cross, T, bridge), route rendering and style overrides, rails and ports (marker onto a road,
POI onto a route, route point onto a road and onto a stop with attachment), all context actions including inputs,
legend entries and dependents, the route stop export, JSON round trip and monochrome safety; route values (length from
the scale bar, own scale and plan scale, unit conversion, smooth routes, entered values winning, travel mode presets,
values label, scale-bar dependents, round trip), stations (docking, kinds, names), bridges and tunnels (following a bent
road, width, deck colour, mask, portals, dependents), contour lines (gap and label, index, closed) and the new legend
keys. `structures.html`/`.svg` in `gallery-maps/` shows them in a sample map. The gallery test writes
`gallery-maps/*.html` (all types, views, stretched variants, presets and a sample route map in colour, monochrome and
German) for visual review.
