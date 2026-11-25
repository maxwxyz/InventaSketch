# Charts v2 – matplotlib, but interactive

Status: plan 2026-11-25 (maintainer request); stage 1 (parts) implemented 2026-11-25 for the engine and PowerPoint (COM), web and LibreOffice follow. German mirror: [docs/de/design/charts-v2.md](../../de/design/charts-v2.md).

## Goal

Everything a technical figure needs – what matplotlib offers in code – done interactively by selecting the parts of a
chart and using hover hints, the context bar and the pie menu:

- select every part (each curve, axis, grid, tick labels, legend, value line, region, annotation), switch it on/off,
  style it – also with PowerPoint's own formatting tools, which are folded back per part;
- several plots stacked with a shared x or y axis, with phase lines across all of them (e.g. power on top, rotor
  temperature below on one time axis);
- axis breaks, single value lines (horizontal and vertical, with label), shaded or hatched regions (between two curves,
  below a curve, between two x values), dimension arrows ("time gained"), text and reference numerals for patent figures;
- curves from data, from functions (constant, ramp, step, rectangle pulse, sawtooth, triangle, sine, exponential, PT1,
  circle/ellipse) and from a hand sketch that becomes a smooth or idealised curve;
- reverse engineering: a picture (clipboard, photo, screenshot) becomes an editable chart;
- export as matplotlib code (and the existing SVG/PNG).

Example the design is checked against: the maintainer's patent figure (power P with phases "pre-conditioning" and
"full load" on top; rotor temperature below with a dashed and a solid curve, a hatched region between them, a limit
line "limit temperature", a dimension arrow "time gained" and reference numerals 40–46; shared time axis, vertical phase
lines through both plots).

More check figures (maintainer, 2026-11-25):

- **Timing diagram of a decision logic** (three stacked panels on one time axis, principle style without tick labels):
  a rising curve towards a dashed limit line with a label and a numeral on the axis; a vertical dashed marker
  ("t_krit") through all panels and a horizontal time arrow labelled "t_krit" between two panels; in the middle panel
  a solid and a dashed curve with the region hatched only where one lies above the other; rectangle pulses (one solid,
  one dashed) in the bottom panel; reference numerals 63–69 with leader lines; a centred figure title.
- **Temperature over time** (data axes with ticks, horizontal dotted grid only): several horizontal dashed reference
  lines with labels ("heating ram (15)", "operating maximum 60 °C"); curves approaching an asymptote (PT1 / exponential)
  that stop and continue later (gap); open circle markers on a curve with value labels ("31 s", "77 s"); a vertical
  dimension arrow between two levels ("35 K"); labels on the curves; a caption below the plot.
- **Flowchart** (diagrams module, not charts): start circle, decisions with yes/no exits, process boxes with two-line
  text, a loop back into the side of an earlier box, a dashed note box with a dashed arrow, exits joining a box from the
  side, an end bullseye.

So stage 2 also covers: principle axes (arrow, no ticks) per panel, markers across panels, arrows between panels,
regions "where curve A is above curve B", point markers with labels, dimension arrows between two y values, gaps in
curves; stage 3 covers pulses and asymptotic curves.

## Module cut

- **Charts & functions** (module `charts`): data charts, characteristic curves and maps, principle diagrams, function
  graphs and coordinate systems. One axis system for all of them. The coordinate systems and function graphs of the math
  module move here (their items step back via `LibraryItem.Alternative`; existing slides keep rendering).
- **Geometry & vectors** (module `math`): triangles, circles, angles, constructions, points and vectors. They dock onto
  a chart's coordinate system (point on curve, vector in the axes).

## Foundation: parts of an element (Core, all modules)

- Every primitive already carries a `Tag`. A **part key** is that tag with an index where needed: `series.0`, `axis.x`,
  `axis.y.1`, `grid.major`, `ticklabels.x`, `legend`, `ref.2`, `region.0`, `note.3`.
- `SketchObject.Parts`: per part key a `PartLook(Style? Style, bool Hidden)`. A key without index (`series`) applies to
  all parts of that kind; the indexed key wins. `Registry.RenderLocal` applies it after the object style – the single
  render path, so SVG, draw plans, web and LibreOffice all show it.
- `ObjectType.Parts(o, ctx)`: the selectable parts with label and kind; `ObjectType.PartActions(o, part, ctx)`: actions
  for one part (generic: hide/show, thicker/thinner, dash style, reset; type-specific: smooth, markers, fill below, add
  value line here, log scale, break here …).
- `Registry.PartAt(o, point, ctx, tolerance)`: hit test on the tagged primitives (priority: texts and handles, then lines,
  then areas) – the base for hover, selection and Tab cycling.
- Engine: `parts`, `partAt`, `partActions`, `applyPartAction`, `setPartLook`.
- PowerPoint: every leaf shape of a unit remembers its part key (tag `ISK_PART`) and how it was drawn; a change made with
  PowerPoint's tools on some leaves is folded into the look of exactly those parts (Slidemax `ChartFormatting`: compare
  fill, line colour, width and dash independently; theme colours compared as shown). Selecting a leaf inside a unit
  selects the part: the context bar and pie menu show its actions.

## Chart model v2

One chart type "figure" with a structured specification (JSON in the parameter `spec`, versioned), so existing chart
types stay and become presets that fill the specification:

- `panels`: each with its y axis (linear/log/category, range, unit, label, break list), height share, title;
- `x`: shared x axis (or per panel), phase lines (vertical lines with labels across all panels);
- `series`: data (table), function generator (kind + parameters), sketch (raw points + fitted kind), each with line,
  marker, step/smooth, label at the curve end, numeral;
- `refs`: value lines (x or y, label, style), `regions` (between curves, below a curve, between x or y values; fill,
  hatch, opacity), `notes` (text, arrow, dimension arrow between two points, reference numeral with leader);
- legend, title, number format, colours by a scheme (monochrome for patents).

## Stages

1. **Parts in Core and engine** (generic, every module) + charts tag their parts with keys; COM: per-part native
   formatting and part selection with bar and pie menu. Tests: every chart part can be hidden and styled; PowerPoint
   scenario: recolour one curve natively, it stays after a redraw.
2. **Figure model**: panels with shared x axis and phase lines, value lines, regions and hatching, notes, dimension
   arrows, reference numerals; data table editor (port of Slidemax `ChartSheet` grid logic, WinForms dialog in COM).
   The maintainer's patent figure is built as a library preset and checked in PowerPoint.
3. **Curves from functions and sketches**: function generators; sketch recognition (simplify, classify line / step /
   pulse / sawtooth / triangle / sine / exponential / circle, fit, else smoothing spline – Slidemax `Smoothed`);
   axis breaks (port of Slidemax `AxisBreakSpans`, `Along/ValueAlong`, zigzag; extended to XY and the x axis).
4. **matplotlib export** of the figure model; **math merge** (coordinate systems and function graphs into the chart
   module).
5. **Reverse engineering**: port of the Slidemax scanner (`Import/Scanning`, bars/columns, label calibration) plus a new
   curve digitiser (colour/line mask, per-column trace, dashed/solid separation, axis calibration reused).

Each stage: model + rendering + anchors + actions + persistence round trip + tests + docs EN/DE + host notes; COM first,
then web, then LibreOffice.

## Parallel work

Stage 1 (Core/engine API) is done first by the coordinator; afterwards agents with file ownership: figure model and
rendering (`src/InventaSketch.Modules.Charts/Figure*`), scanner port and digitiser (`…/Charts/Scanning`), sketch
recognition and function generators (`…/Charts/Curves*`), matplotlib export (`…/Charts/Export*`); the coordinator owns
Core, Engine, hosts, the solution and the review.
