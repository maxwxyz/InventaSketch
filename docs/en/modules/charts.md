# Charts & characteristic curves module (`charts`)

Technical charts for slides: XY line, scatter and step charts, bar/column charts, plain axes with grid, the
torque–speed curve of a motor, an efficiency map, a pump/fan curve with operating point, a Bode plot and a tolerance
band, and the presentation charts pie and donut, polar and radar, Sankey, Gantt, box plot and histogram. Every chart is drawn with native primitives (lines, freeforms, rectangles, text), so it stays editable with the
host's own tools, and it remains readable in monochrome output.

Every chart is insertable without parameters (Quick Sketch) with sensible sample data. Charts are box objects drawn
in local coordinates inside their box. Free scaling changes `Size`, never the data: the plot area, fonts and markers
follow the box. Text is sized by the smaller scale factor relative to the design size (100 × 65 mm, Bode plot
100 × 80 mm), so non-uniform stretching never distorts text. Charts are figures, not parts (`IsPart = false`).

## Object types

| Id | Name (EN / DE) | Notes |
|---|---|---|
| `chart.xy` | XY chart / XY-Diagramm | `data`, `style` line / line with markers / markers (scatter) / steps, `smooth` (Catmull-Rom curve as cubic path), `limits` (horizontal limit lines), linear or log axes |
| `chart.bar` | Bar chart / Balkendiagramm | `data` (rows `label;value[;value…]`), `orient` columns (vertical) / bars (horizontal), `mode` grouped / stacked, `values` (value labels), `patterns` |
| `chart.axes` | Axes and grid / Achsen und Gitter | no data; `xrange` / `yrange` (default `0;10`), linear or log scales, `arrows` (arrowheads on the axes) |
| `chart.torque` | Torque–speed curve / Drehmoment-Drehzahl-Kennlinie | `tmax` (Nm), `nbase` (corner speed, rpm), `nmax` (rpm); constant torque up to `nbase`, constant power above; `power` (power curve on a secondary axis in kW), `regions` (corner speed line and region notes) |
| `chart.efficiency` | Efficiency map / Wirkungsgradkennfeld | `tmax`, `nbase`, `nmax` (torque envelope), `etamax` (peak efficiency, %), `levels` (contour levels in %, default `80;85;88;90;92;94;95`), `labels` |
| `chart.pump` | Pump / fan curve / Pumpen-/Ventilatorkennlinie | `kind` pump / fan, `h0` (shut-off head or pressure), `qmax`, `hstat` (static part of the system), `qop` (flow at the operating point), `system` (system curve) |
| `chart.bode` | Bode plot / Bode-Diagramm | `system` low pass / high pass of 1st or 2nd order, `fc` (Hz), `zeta` (damping, 2nd order), `gain` (dB), `show` magnitude and phase / magnitude / phase, `marker` (corner frequency line), `xrange`, `data` (measured `f;dB;°`, replaces the system) |
| `chart.band` | Tolerance band / Toleranzband | `data` (nominal curve, see below), `tol` + `tolmode` percent / absolute, `smooth`, `limits`, `patterns`; further data blocks are drawn as measured series with markers |
| `chart.figure` | Figure (principle, technical) / Prinzip- und technische Abbildung | `spec` (JSON specification, see [Figure](#figure-charts-v2-stage-2)); stacked panels, principle or data axes, value lines, markers, regions, notes, reference numerals |
| `chart.linked` | Characteristic chart (linked) / Kennlinie (verknüpft) | `link` (`<element id>#<set key>`, advanced), `title`, `grid`, `legend`; drawn from the curve set of the linked element, see [Linked characteristic charts](#linked-characteristic-charts-calc-5); not in the library, inserted from an element |
| `chart.math.axes` | Coordinate system (2D) / Koordinatensystem (2D) | math style, see [Coordinate systems and function graphs](#coordinate-systems-and-function-graphs-charts-v2-stage-4) |
| `chart.math.function` | Function graph / Funktionsgraph | basic function, typed formula or function generator in the math-style coordinate system |
| `chart.timing` | Timing diagram (digital) / Zeitdiagramm (digital) | `signals` (one signal per row, see [Digital timing diagram](#digital-timing-diagram-charttiming)), `slope` sloped / sharp, `grid`, `timelabels`, `markers`, `spans`, `title`, `dt`, `tunit` |
| `chart.pie` | Pie chart / Kreisdiagramm | `data` (rows `label;value`), `hole` (donut: inner/outer radius, 0–0.9), `labels` percent / name / name and percent / value / none, `start` (° clockwise from 12 o'clock), `direction` clockwise / counter-clockwise, `center` (text in the hole), `legend`, `patterns`; see [Presentation charts](#presentation-charts-2026-11-26) |
| `chart.polar` | Polar chart / Polardiagramm | `data` (rows `angle in °;r`, XY format), `style` line / line with markers / filled area / markers, `closed`, `zero` right counter-clockwise / up clockwise, `rrange`, `grid`, `legend` |
| `chart.sankey` | Sankey diagram / Sankey-Diagramm | `data` (rows `source;target;value`), `values` (values at the nodes), `patterns` |
| `chart.gantt` | Gantt chart / Gantt-Diagramm | `data` (rows `task;start;duration`, no duration = milestone, blocks = groups), `today` (today line), `xlabel`/`xunit`, `xrange`, `grid`, `legend` |
| `chart.box` | Box plot / Boxplot | `data` (rows `category;sample;sample…`), `whis` (whiskers × IQR, 1.5), `outliers`, `yrange`, `grid`, `legend` |
| `chart.histogram` | Histogram / Histogramm | `data` (raw samples, blocks = series), `bins` (0 = automatic, Sturges), `xrange`, `yrange`, `grid`, `legend` |

`chart.xy` also takes axis breaks (`ybreaks`, `xbreaks`, `breakstyle`, see [Axis breaks](#axis-breaks-charts-v2-stage-3)).

Common parameters (where they apply): `title`, `xlabel`, `xunit`, `ylabel`, `yunit` (empty = the type's
bilingual default, an explicitly empty label hides the title), `xscale` / `yscale` linear / log, `xrange` / `yrange`
(`min;max`, empty = automatic, advanced), `grid`, `legend` automatic / inside / right / none, `box` (closed frame).
Axis titles read "label (unit)" in English and "label in unit" in German (DIN 461 style), e.g. "Speed n (rpm)" /
"Drehzahl n in 1/min". Tick labels use the decimal comma in German.

The efficiency map uses a qualitative loss model (constant losses, copper ∝ T², iron ∝ n, windage and eddy currents
∝ n³), scaled so its maximum equals `etamax`. It shows the typical island shape for sketches; it is not measured
data. The pump curve is H(Q) = H₀ (1 − (Q/Q_max)²); the system curve runs through the static head and the operating
point. The Bode plot is computed from the standard transfer functions of first- and second-order low and high passes.

The library has 41 presets (`chart.line`, `chart.line.markers`, `chart.line.multi`, `chart.line.smooth`,
`chart.line.logx`, `chart.scatter`, `chart.step`, `chart.column`, `chart.column.grouped`, `chart.column.stacked`,
`chart.bar.horizontal`, `chart.axes`, `chart.axes.plain`, `chart.axes.loglog`, `chart.line.break`, `chart.limits`, `chart.torque`,
`chart.torque.plain`, `chart.efficiency`, `chart.pump`, `chart.fan`, `chart.bode`, `chart.bode.lowpass1`,
`chart.bode.highpass1`, `chart.band`, `chart.band.measured`, `chart.figure.phases`, `chart.figure.timing`, `chart.figure.temperature`,
`chart.timing`, `chart.timing.clock`, `chart.timing.bus`, `chart.timing.handshake`, the eight coordinate systems and function graphs listed below,
and the nine presentation charts `chart.pie`, `chart.pie.names`, `chart.donut`, `chart.polar`, `chart.polar.radar`,
`chart.sankey`, `chart.gantt`, `chart.box`, `chart.histogram`; 51 in all) in four categories: data
charts, coordinate systems & function graphs, characteristic curves and principle and technical figures.

## Data format

Data are a text parameter (`data`), typed in the inspector or pasted from a spreadsheet:

- one data row per line; in the single-line *Data…* prompt rows are separated by `|` and series by `||`;
- fields are separated by **tab** (Excel paste), **semicolon**, **whitespace**, or **comma** (only when no other
  separator occurs: `1,5` is x = 1, y = 5; `1, 5` with a space is the same);
- numbers in invariant culture (`2.5`) or with a **German decimal comma** (`2,5`) when the fields are separated by
  tab, semicolon or space; thousands separators (`1.234,5`, `1,234.5`), a unicode minus, a trailing `%` and
  exponents (`1e3`) are accepted;
- a **blank line** starts a new series; a text-only line names the next series (one field) or the columns
  (several fields, e.g. an Excel header row `x;Measured;Model`); a field in double quotes is always text (`"2025"`);
- lines starting with `#` or `//` are comments; empty or invalid cells (`-`, `n/a`) are skipped; parsing never fails.

Meaning of the columns:

| Chart | Rows |
|---|---|
| XY chart | `x;y` (one series), `x;y1;y2;…` (one series per column, names from the header), or one value per row (x = 1, 2, 3 …) |
| Bar chart | `label;v1;v2;…` (one series per value column); numeric first columns are used as labels when more values follow (years); several blocks are several series matched by label |
| Tolerance band | `x;y` (band from `tol` in % of y or absolute), `x;y;±t` (symmetric), `x;y;lower;upper`; further blocks are measured series `x;y` |
| Bode plot (`data`) | `f;dB;°` (phase optional) |
| Pie / donut | `label;value` (first value column; zero, negative and empty values get no slice) |
| Polar chart | as the XY chart, x = angle in degrees, y = radius |
| Sankey diagram | `source;target;value` (one flow per row; rows without a positive value are skipped) |
| Gantt chart | `task;start;duration` (a row without duration is a milestone); a blank line or a header line starts a group |
| Box plot | `category;sample;sample…`, or one column per category under a header row, or one block per category |
| Histogram | any numbers (every value of a block is a sample); several blocks are several series |

Lists (`levels`, `limits`) use the same separators; a single value with a decimal comma (`92,5`) is one number.
Ranges (`xrange`, `yrange`) accept `0;100`, `0..100`, `0,5;2,5` or `10 1000`; anything else means automatic.

### Importing a table (2026-11-25)

"Import …" (COM: Review group; web: import button in the top bar; engine `importData`) reads a CSV/TSV file into the
selected chart, or into a new XY chart in the middle of the slide: the separator (tab, ";" or ",") is detected, quoted
fields keep their separators, a decimal comma becomes a point (German Excel) and empty lines are dropped
(`Imports.CsvToChartData`). Checked in PowerPoint (scenario `import`).

## Axes and ticks

Linear axes choose "nice" steps (1, 2, 2.5, 5 × 10ⁿ) for about five intervals and extend automatic ranges to the next
step (bar charts, torque and pump curves always include 0). Log axes run from decade to decade, put major ticks on
decades and minor ticks on 2 … 9 × 10ⁿ, label 2 and 5 when the axis spans at most two decades and thin out decade
labels beyond eight decades; values from 10 000 use SI prefixes (`10k`, `1M`), values below 0.001 exponent form.
Non-positive values are skipped on log axes. The Bode phase axis uses steps of 5, 10, 15, 30, 45, 90 or 180°.
Overlapping tick labels are thinned out; category labels share a reduced font when the bands are narrow. Data
outside a fixed range are clipped at the plot area (lines, smooth curves, bands).

## Monochrome

Series use a fixed sequence of dark colours (luminance below 0.35, they print black), dash patterns (solid, dash,
dot, dash-dot, long dash, dash-dot-dot) and markers (filled circle, hollow square, filled triangle, hollow diamond,
cross, plus), so they stay distinguishable without colour. Bars and the tolerance band switch to fill patterns
(black, white, hatch 45°, hatch 135°, cross hatch, dots; band: hatch) when the document or the render context is
monochrome, or always with `patterns`. The grid turns dotted in monochrome. Limit lines use the centre line role
(dash-dot), helper lines (corner speed, drop lines) the helper role.

## Legend

`legend = automatic` shows a legend inside the plot when there are two or more entries (torque and power, pump and
system curve, nominal and band, several series). The inside legend goes into the plot corner that covers the fewest
data points and notes; `right` places it beside the plot. Entries show the series line with its dash pattern and
marker, or an area sample for bars and bands. Series without a name are called "Series 1", "Series 2" … ("Reihe 1" …).

## Snapping and docking

All anchors are in local coordinates computed from the same layout as the drawing, so they follow free scaling:

- plot corners `plot.tl`, `plot.tr`, `plot.br`, `plot.bl` (corner role) and the plot centre `plot.c` (centre role),
  plus the generic box anchors;
- `origin` (port): the point (0, 0) where it lies inside the plot, else the axis corner; the axes as rails `axis.x`,
  `axis.y` (`axis.y2` for the power axis), so lead lines and arrows dock anywhere on an axis. Panels below the first
  one (Bode phase) carry the suffix `2` (`origin2`, `axis.x2` …);
- data points as ports: `s<series>.<point>` (XY, bar ends, tolerance band nominal and measured points; at most 160 per
  chart, only inside the plot), plus `upper` / `lower` (band), `tmax`, `base`, `end`, `pmax` (torque curve), `peak`,
  `base`, `end` (efficiency map), `op`, `h0`, `qmax`, `hstat` (pump curve), `fc`, `fc.phase` (Bode plot).

## Context actions

| Type | Actions |
|---|---|
| XY chart | "Data…" (input), style ▸ (line, line with markers, markers, steps), smooth curve, x scale ▸, y scale ▸, grid, legend ▸, add area ▸ (below curve 1, between two curves, vertical span, horizontal band, between the limit lines) |
| Bar chart | "Data…", orientation ▸, several series ▸ (grouped / stacked), value labels, grid, legend ▸ |
| Axes | grid, arrowheads, x scale ▸, y scale ▸ |
| Torque curve | power curve, region notes, grid, legend ▸ |
| Efficiency map | "Contour levels…" (input), contour labels, grid |
| Pump / fan curve | machine ▸ (pump / fan), "Operating flow…" (input), system curve, grid, legend ▸ |
| Bode plot | system ▸, "Corner frequency…" (input: `1 kHz`, `250 Hz`, `2.5k`, `2,5k`), show ▸, corner frequency mark, grid |
| Tolerance band | "Data…", "Tolerance…" (`10 %` = percent, `0.5` = absolute), smooth curves, grid, legend ▸ |
| Pie / donut | "Data…", donut (toggle), "Hole size…" (`30 %`), "Start angle…", "Centre text…", slice labels ▸, direction ▸, legend ▸ |
| Polar chart | "Data…", style ▸, closed curves, 0° direction ▸, "Radial range…", grid, legend ▸ |
| Sankey diagram | "Data…" (flow rows), add flow, remove last flow, values at the nodes |
| Gantt chart | "Data…", add task, remove last task, "Today line…" (empty = none), grid, legend ▸ |
| Box plot | "Data…", "Whisker length…", outliers, grid, legend ▸ |
| Histogram | "Data…", "Bins…" (`auto` or a number), more bins, fewer bins, grid, legend ▸ |

Invalid inputs throw `FormatException` with a bilingual message and change nothing. Toggles use `IconIds.Toggle`,
inputs `IconIds.Text`. The chart types have no view switch.

**Choices are dropdowns (2026-11-25).** Every choice among several options (style, scale, legend position, orientation,
machine, function, edges, curve shape, fitted function, smoothing, detail, line style, marker, fill pattern, colour, opacity,
edge, where, steps) is one action per option (`ActionChoices.Option`, id `<choice>=<value>`, e.g. `chart.style=step`,
`chart.curve.shape=fitted`), all carrying the same `ActionChoice` (key and label), the current option marked active.
Hosts show them as one dropdown ("Curve shape: Smoothed ▾") and in the pie as "Curve shape: Smoothed ▸" opening the
options – never as a cycle button. Options without a shared pictogram carry a small drawing (`ContextAction.Preview`:
the fitted function, the smoothing, the fill pattern, the colour, the marker), which the hosts render as the option
picture. The curve shapes have their own pictograms (`IconIds.CurveSmoothed`, `CurveSpline`, `CurveFitted`,
`CurveSegments`, `CurveRaw`), since a drawing of the current curve looks alike in most shapes.
Choices whose options are commands (`ActionChoice.IsMenu`: "Fill below ▸", "Fill to ▸", "Add area ▸") mark nothing.
The engine reports `choice`, `choiceLabel`, `choiceMenu` and `previewSvg` with every action.

## Parts (charts v2, 2026-11-25)

Every drawn piece of a chart is a **part** with a key – the primitive tag: `series.0`, `series.1` (curves), `marker.0`
(markers of curve 1), `bar.0` (bars of series 1), `value.0` (value labels), `axis.x`, `axis.y`, `axis.y2` (secondary
axis), `axis.box` (closed frame), `tick.x`, `tick.y`, `label.x`, `label.y` (tick labels), `axistitle.x`, `axistitle.y`,
`grid.major`, `grid.minor`, `zero`, `limit`, `legend`, `title`, `band`, `contour`, `contourlabel`, `envelope`, `helper`,
`point`, `note`. Stacked panels (Bode plot) add the panel number: `axis.y.2`, `label.y.2`.

- Each part can be switched off and styled on its own (`SketchObject.Parts`: per key a look with style or hidden). A
  look on the kind (`series`, `grid`) applies to all parts of that kind; the indexed key wins.
- Part actions: the chart's own ones that concern the part (curve: data, style, smoothing, shape choices of sketched
  curves, fill below ▸, fill to ▸, delete curve; data point: delete, add a point after it, value …; area: pattern,
  colour, opacity, edge, where, steps, legend label, delete; axis: scale; grid: grid toggle; legend: position), then
  show/hide, thicker, thinner, line style (a choice) and reset. The Del key with a part selected runs the part's
  `part.delete` (point, curve, area, value line, note), else hides the part – it never deletes the whole chart.
- PowerPoint: a click without dragging on the selected chart selects the part under the pointer (outlined in blue); the
  bar shows its name and actions, "Parts …" lists every part including hidden ones, "Whole element" leaves the part.
  Formatting one curve with PowerPoint's own tools (line colour, weight, fill, font) changes that part only and survives
  every redraw; formatting the whole chart still changes the element style.
- Engine: `parts`, `partAt`, `partActions`, `applyPartAction`, `setPartLook` (see the design note
  [charts v2](../design/charts-v2.md)).

## Data points and curves on the drawing (2026-11-25)

XY charts and figures (maintainer: "select the values/points of every curve, highlighted in blue; click+drag on a curve
moves it, values adjusted in the background; single points moved by click and drag or deleted; single curves deleted"):

- Every data point is a part `point.<curve>.<index>` ("Point 3 of Series 1 (2; 4.1)"), a fine part
  (`PartInfo.Detail`: not in the parts list and the Tab order). Its part shape (`ObjectType.PartShapes`, a small circle,
  never drawn) serves the hit test (`Registry.PartAt`: a point wins when the pointer is on it), the outline and the
  glow (`Registry.PartPrimitives`).
- Drag handles (`DragHandles`): one free handle per point, shown with its curve (`DragHandle.ShowWith = "series.k"`);
  the value snaps to a tenth of the axis step (log axes: two significant digits) and stays inside the axis range; on a
  curve whose x never falls the point stays between its neighbours. Curves with more than 64 points (dense data) get
  no point handles – their points stay selectable on the drawing and editable with "Value …"; sketched curves store only
  their key points (at most 64), so they always get grips. One handle per curve
  (`series.k`, `DragHandle.ByPart`) is dragged by pressing on the selected curve: every point moves by the snapped offset
  (log axes: a factor); of a clearly vertical (horizontal) drag only that direction counts.
- Actions: point – delete, add a point after it (halfway to the next one), "Value …" (`x; y`); curve – delete curve.
  Deleting a curve drops its areas and point markers and moves the indices and part looks of later curves down.
  Editing a point of a sketched curve makes it plain data (its shape memo goes); the other curves keep theirs. The XY
  chart's data are then written as one block per curve ("Name;" header, `x;y` rows); a curve that was drawn as a spline
  keeps that look through a `# spline` line before its block.
- PowerPoint: the point grips are small blue-rimmed dots (the selected point filled blue with a soft glow); they appear
  for the selected curve or point, the others near the pointer. A click on a grip selects the point; dragging moves it
  (live preview, one undo step, Esc cancels). With a curve selected, pressing anywhere on it and dragging moves the
  curve. Del deletes the selected point (the curve stays selected), curve or area.

## Sketched curves: key points, detail and drawing speed (2026-11-25)

Maintainer: "When drawing a curve it sometimes samples too many points – maybe it can be set, or depend on how fast or
which curve you draw?" A sketched curve (`ChartSketchInput`, `CurveShapes`, `CurveKeyPoints`) stores only the points
that carry its shape – every stored point is a data row, a point part and a grip – instead of 96–120 dense samples:

- The pointer trail is cleaned once and kept with the curve (`CurveSketchMemo`: XY chart comment `# sketch …` before the
  block, figure series member `sketch`), so every choice below is recomputed from it without loss.
- Key points per curve shape, all tolerances relative to the plot (1 = plot size), at the default detail:
  - **Smoothed**: the smoothed trail; key points so that the chart spline through them stays within 0.6 % of it.
  - **Interpolated (spline)**: the lightly averaged trail; key points so that the spline stays within 1.5 %.
  - **Fitted function**: piecewise-linear functions (constant, ramp, step, pulse, sawtooth, triangle) store their exact
    corners; smooth ones (sine, ellipse) key points for the spline within 0.5 %; PT1, PT2 and decay whose start corner
    lies inside the drawn range are stored as straight segments within 0.5 % (a spline would round the corner).
  - **Straight segments**: Ramer–Douglas–Peucker within 1.5 % of the lightly averaged trail.
  - **As drawn**: the trail thinned with Ramer–Douglas–Peucker within 0.2 % (the tremor stays visible).
- Spline key points (`CurveKeyPoints.Spline`): seeds at the ends, at turning points of x and y (zigzag with a hysteresis
  of 4 tolerances), at corners (≥ 50° within ±2 % of arc length) and at the steepest point between a peak and a valley
  (inflection); between them as few points as possible, spread evenly over the curve's bending (√|curvature| plus a
  small base), their number raised until the spline through all key points stays within the tolerance band in both
  directions (overshoots count); points that can go without leaving the band are dropped. A straight line keeps 2
  points, a single arc about 5–7, a two-period sine about 15–20, a scribble at most the detail's limit.
- Drawing: smoothed, interpolated and smooth fitted curves are drawn with the chart spline (uniform Catmull–Rom,
  `Draw.Spline`; affine-invariant, so the check in plot units holds on the drawing). XY chart: per block, marked by its
  memo (`CurveShapes.IsSpline`) or a `# spline` line (kept when the points are edited by hand or the slide is cleaned;
  `ChartSketchInput.XySplines`), also in the matplotlib/pgfplots export; figure: the series' `smooth` flag.
- **Detail** (part choice next to "Curve shape" and "Smoothing", a dropdown with a preview of the kept points): Low
  (tolerances × 2, at most 24 points), Medium (default, adaptive; at most 40), High (tolerances × ½, at most 64). A lower
  detail never keeps more points than a higher one. Not offered for exact piecewise-linear fits. Stored as `detail=low`
  / `"detail": "high"` (medium is left out).
- **Drawing speed** as a hint: the host passes a time stamp per trail point (COM: ms since the press; engine
  `sketchCurve` accepts `[x, y, t]`). Rule: the speed at a point is measured over at least 120 ms around it in plot
  sizes per second; its tolerance factor is √(speed / 1 plot per second), clamped to 0.6 … 1.5. Slow, careful drawing
  (≤ 0.36 plots/s) tightens the tolerances to 0.6 ×, a fast flick (≥ 2.25 plots/s) relaxes them to 1.5 ×; a stroke of
  one plot width per second is neutral. The factors are stored per kept trail point in steps of 0.1 (`pace=4456…`, one
  digit per point, 0 = 0.6 … 9 = 1.5; left out without time stamps), so the result is deterministic.
- Old sketched data (dense rows, memo without detail or pace) load unchanged and are drawn as splines when their shape is
  smooth; any choice recomputes them as key points.

## Areas (2026-11-25)

Filled areas as in matplotlib, for XY charts (parameter `areas`: a JSON array, empty = none; old documents unchanged)
and figures (the specification's `regions`, same members; part keys `area.k` in the XY chart, `region.k` in the figure,
kept for existing documents):

| Kind | matplotlib | Members |
|---|---|---|
| `below` | `fill_between(x, y)` down to the axis | `a`, `base` (a y value instead of the panel bottom) |
| `between` | `fill_between(x, y1, y2)` | `a`, `b` (curve) or `y2` (horizontal line, e.g. a limit line) |
| `betweenx` | `fill_betweenx(y, x1, x2)` | `a`, `b` (curve) or `x2` (vertical line) |
| `span` | `axvspan(x0, x1)` | `from`, `to`, `panel` (-1 = all) |
| `hspan` | `axhspan(y0, y1)`; a band between two value lines | `y1`, `y2`, optionally `from`, `to` (x range), `panel` |

- `where`: `above` (curve A above – right of – the other edge) or `below`; crossings are computed exactly (also where
  two curves meet in a data point); the earlier `"above": true` is still read and written.
- `step`: `pre`, `post`, `mid` – the edges follow steps (`fill_between(step=)`).
- Look: `hatch` – a matplotlib hatch pattern (`/`, `\`, `|`, `-`, `+`, `x`, `o`, `O`, `.`, `*`, repeated for density,
  combinable: `//`, `xx`, `/o`); `fill` `solid` (alone: a tinted face, opacity 0.3 by default; with a pattern: a light
  tint under it) or `none`; `color` (default: the colour of curve A, grey for spans), `opacity`, `edge` (line style of
  the outline) and `edgeColor`, `label` (legend entry). Without `hatch` the earlier fills apply (`hatch`, `cross`,
  `solid`, `dots` with `angle` and `spacing`). Values areas reach (a base, a line, a band, a span) are inside the
  automatic axis ranges, as in matplotlib.
- Drawing (native first): the area is one closed freeform; one line family of the pattern (or a crossing pair) is its
  hatch fill, which PowerPoint gets as hatch lines (the COM draw plan draws hatches explicitly), further families are
  explicit hatch lines, circles and stars are small closed shapes inside the area (never crossing the edge) – all lines
  and shapes stay editable in PowerPoint, no picture. One character spans 2.4 mm at the design size (it follows the
  chart scale), "//" 1.2 mm.
- Creating: on a curve "Fill below ▸" (one option per pattern, each with a swatch) and "Fill to ▸" (another curve of
  its panel, a value or limit line, a y value …, a vertical line x = … for fill_betweenx); on the chart "Add area ▸".
  Each area is a part with the choices Pattern ▾ (swatches), Colour ▾, Opacity ▾, Edge ▾, Where ▾ and Edges along the
  curve ▾ (steps), the toggle "Tinted background" (patterns), "Legend label …" and Delete.
- Exports: matplotlib `fill_between(..., where=[a >= b …], interpolate=True, step=…, hatch=…, facecolor=…,
  edgecolor=…, linestyle=…, label=…)`, `fill_betweenx`, `axvspan`, `axhspan` (a band over part of x as
  `fill_between`); pgfplots `fill between` for plain areas (patterns `Lines`, `Hatch`, `Dots`, `Stars` of
  `patterns.meta`, a tint as `postaction`), the filled polygons for steps, fill_betweenx and where-conditions,
  rectangles for bands; TikZ and SVG from the drawing.
- Import (matplotlib): `fill_between` with `hatch`, `where` (curve above or below the other edge or a constant),
  `step`, `label`, `facecolor`/`edgecolor`/`linewidth`/`linestyle`; `fill_betweenx`; `axhspan` (over the whole width);
  `axvspan` with its label.

## Axis breaks (charts v2 stage 3)

The XY chart (`chart.xy`), the figure (`chart.figure`) and the math-style coordinate systems and function graphs can
hide value ranges of a linear axis (an axis break), so data far apart share one chart (e.g. 10–15 and 95–100 on one
y axis).

- Parameters: `ybreaks` / `xbreaks` (advanced): hidden ranges as "lo-hi" pairs separated by ";", e.g. `20-80` or
  `-50--20; 120-180` (also `lo..hi`, `lo–hi`, a decimal comma is accepted); `breakstyle` zigzag (default: a double
  zigzag band across the whole plot and the axis line) / slash (a short double slash on the axis line, and on the
  opposite side of a closed frame). Unreadable pairs are ignored when drawing; log and category axes have no breaks.
- Drawing (`Frame`): the axis maps through `BrokenAxis` (the same scale outside the breaks, each break compressed to
  the marker band of `AxisBreaks.RequiredGap`, about 3.6 mm); curves are split into visible runs, cut exactly at the
  break limits; data points inside a break get no marker and no port; tick labels, grid lines, limit lines, value lines
  and texts inside a break are left out; the tick step is chosen for the visible span. The band is filled white so the
  axis line appears interrupted.
- Figure: per axis `"breaks": [[20, 80], …]` in `x` and in each panel (also `{"from": 20, "to": 80}` or a text
  `"20-80; …"`), and `"breakStyle": "zigzag" | "slash"` for the figure. Optional members of version 1: older readers
  ignore them, wrong entries are skipped.
- Parts: `axisbreak.y` (stacked panels `axisbreak.y.1`, `axisbreak.y.2` …) and `axisbreak.x`, named "y axis break" /
  "Achsenbruch y".
- Actions: "Axis break here…" (`chart.ybreak`, input "lo-hi", several separated by ";", empty = none; prefilled with
  the current breaks or a suggestion for the largest data-free gap when it covers more than 40 % of the axis),
  "x-axis break here…" (`chart.xbreak`), "Remove axis breaks" (`chart.ybreak.remove`, `chart.xbreak.remove`). They are
  offered on the element, on the axis parts (axis, ticks, tick labels, axis title of that direction) and on the break
  marker. In a figure (`chart.figure.ybreak` …) the axis part of a stacked panel edits the breaks of that panel.
- Anchors: data ports and the axis rails follow the broken mapping (a port sits where its point is drawn).
- Export: matplotlib and pgfplots do not reproduce breaks; the axes run continuously and the chart starts with the
  comment "Axis breaks are not exported …" naming the hidden ranges.

## Coordinate systems and function graphs (charts v2 stage 4)

The 2D coordinate system and the function graphs of the math module moved into the charts module, so all charts share
one axis system (`Frame`, including axis breaks, parts and export). Category "Coordinate systems & function graphs".

| Id | Name (EN / DE) | Notes |
|---|---|---|
| `chart.math.axes` | Coordinate system (2D) / Koordinatensystem (2D) | math style: both axes cross at the origin (0 clamped into the ranges) and end in arrowheads beyond the plot, ticks across the axis lines, numbers below the x axis and left of the y axis, one "0" at the origin, axis names (`xname`, `yname`, italic) at the arrow tips; `xmin`/`xmax`/`ymin`/`ymax`, `xstep`/`ystep`, `grid`, `numbers`, `xpi` (x ticks at multiples of π/2, labelled π/2, π, 3π/2 …), `title`, axis breaks |
| `chart.math.function` | Function graph / Funktionsgraph | the coordinate system with a curve: `function` = one of ten basic functions as y = a·f(b(x − c)) + d (linear, quadratic, cubic, sine, cosine, exponential, ln, square root, absolute value, reciprocal; `a`–`d`), a typed formula (`expression`, text `expr`) or a function generator (`generator`, text form of the curve functions, e.g. `pt1(y0=0, yinf=3, tau=1.5)`); `formula` shows "f(x) = …" in the plot corner that covers the least |

- Formulas (`FunctionExpression`, a small safe evaluator): numbers (decimal point, or a decimal comma between digits),
  `x`, `π`/`pi`, `e`, `+ − · × * / : ^ ** ² ³`, parentheses, `|…|`, implicit multiplication (`2x`, `3(x+1)`,
  `x sin(x)`) and sin, cos, tan, asin, acos, atan, sinh, cosh, tanh, exp, ln, log/lg, log2, sqrt/√, cbrt, abs, sign,
  floor, ceil, round; a leading "f(x) =" or "y =" is ignored. Nothing but arithmetic is evaluated; length (400) and
  nesting depth are limited. A formula that cannot be read draws the axes and the message "Formula cannot be read".
- Curves are sampled (800 points over the range), clipped to the y range at the exact crossing points and split at
  poles and domain edges (no line through the pole of 1/x); generators are drawn with their exact vertices (jumps).
- Ports: `origin`, the zeros (`zero0` …), extrema (`ext0` …) and the y-intercept (`yint`); the axes are rails
  (`axis.x`, `axis.y`) and every grid point is a snap point (`g2_3` = x 2, y 3), so the math module's points and vectors
  dock on the chart (their ends also dock anywhere on its surface) and follow it when it moves, turns or is resized.
- Parts: the curve `series.0` (named by its formula), `formula`, axes, ticks, numbers, axis names, grid.
- Actions: Function (cycle), "Formula f(x)…" (switches to a typed formula, prefilled with the current function),
  "Coefficients a; b; c; d…", "Function generator…" (generators), Show formula, Grid, Tick numbers, π ticks,
  "x range…", "y range…", axis breaks.
- Library: `chart.axes.math`, `chart.axes.math.quadrant` (first quadrant 0…6 × 0…5), `chart.function` (parabola),
  `chart.function.linear` (y = 0.5x + 1), `chart.function.sine` (−2π…2π, π ticks), `chart.function.exponential`,
  `chart.function.formula` (f(x) = x³/4 − x), `chart.function.generator` (PT1 step response over t).
- The math module keeps its types `math.axes.cartesian` and `math.graph.function` (existing slides keep rendering);
  its library items name these charts items as `LibraryItem.Alternative` and step back while the charts module is
  registered. Number line, 3D axes and polar grid stay in the math module.
- Export: the curve is exported as a series (the math-style axes as frame axes, noted in a comment).

## Figure (charts v2 stage 2)

The chart type `chart.figure` draws principle and technical figures from a **versioned JSON specification** in the
parameter `spec` (inserted without parameters it shows the example figure below). Category "Principle and technical
figures" with three presets, the check figures of the [charts v2 design](../design/charts-v2.md):

| Id | Name | Shows |
|---|---|---|
| `chart.figure.phases` | Phases with rotor temperature (technical figure) | power P with the phases "pre-conditioning" and "full load" (phase lines t0, t1 through both panels); rotor temperature without (dashed) and with pre-conditioning (solid) up to the limit temperature, the region between them hatched, the time gained as a dimension arrow with extension lines, reference numerals 40–46; principle axes, monochrome |
| `chart.figure.timing` | Timing diagram of a decision logic | "Fig. 2 …" title; three panels: maturity mass towards a dashed limit (numeral 64 at the axis, 63 on the curve), heat demand with a bump and dashed drive loss heat hatched only where the demand is higher (65–67), sampling signal with a solid (68) and a dashed pulse (69); t_krit as a dashed marker through all panels and as a time arrow below the first panel; rotated axis titles |
| `chart.figure.temperature` | Temperature over time with thresholds | data axes 0–200 s / 0–220 °C, dotted horizontal grid only, four dashed reference lines with labels, a fast and a slow first-order curve with a gap each and inline labels, open circle markers "31 s" / "77 s", a vertical dimension "35 K", a caption; colour |

The curves of the presets are qualitative (first-order lags, a Gaussian bump, rectangle pulses), not measured data.

### Specification

```json
{ "version": 1, "title": "Fig. 2 …", "caption": "…", "scheme": "mono", "legend": "none", "grid": "dot", "panelGap": 3.2,
  "x": { "mode": "principle", "min": 0, "max": 10, "label": { "en": "Time t", "de": "Zeit t" }, "marks": [ { "at": 6, "text": "t_krit" } ] },
  "panels": [ { "mode": "principle", "max": 1.25, "label": "Power P", "share": 1.5, "title": "…" } ],
  "series": [ { "panel": 0, "x": [0, 1, null, 3], "y": [0, 1, null, 2], "line": "dash", "width": 0.5, "color": "#1f4e9a",
                "marker": "circle", "markerFill": "open", "step": false, "smooth": true,
                "label": "heat demand", "labelAt": 4, "labelPos": "above", "numeral": { "text": "65", "at": 5.2, "dx": 1.3, "dy": -1 } } ],
  "refs": [ { "kind": "h", "y": 0.9, "panel": 0, "label": "limit", "labelPos": "left", "line": "dash", "numeral": { "text": "64", "axis": true } },
            { "kind": "v", "x": 6 },
            { "kind": "point", "series": 0, "x": 31, "fill": "open", "label": "31 s", "labelPos": "left" } ],
  "regions": [ { "kind": "between", "a": 1, "b": 2, "above": true, "from": 0, "to": 10, "fill": "hatch", "angle": 45, "spacing": 1.2, "opacity": 1 },
               { "kind": "below", "a": 0, "base": 0 }, { "kind": "span", "from": 1, "to": 4, "panel": -1, "fill": "solid" } ],
  "notes": [ { "kind": "text", "x": 2.5, "y": 1.15, "text": "pre-conditioning", "maxWidth": 2.9 },
             { "kind": "dim", "x": 0, "gap": 0.55, "x2": 6, "gap2": 0.55, "panel2": 0, "text": "t_krit", "side": "above" },
             { "kind": "arrow", "x": 1, "y": 1, "x2": 2, "y2": 0.5, "text": "…" }, { "kind": "line", "…": "…" },
             { "kind": "numeral", "x": 3, "y": 0.4, "text": "70", "dx": 1.4, "dy": -1.4 } ] }
```

- **Axes** (`x` and each panel): `mode` `data` (ticks, tick labels, grid) or `principle` (axis arrow, no ticks, only
  the named `marks`, drawn like tick labels), `scale` `linear` / `log`, `min` / `max` (principle axes default to 0 …
  data maximum), `label` and `unit` (title "label (unit)" / "label in unit"), `grid` (default: on for data axes).
  The x axis is shared; the panel y-axis titles are rotated. Panels stack top-down with `share` as relative height.
- **Series**: points as `x` / `y` lists or `points` (`[[x, y], …]`); `null`, `NaN` text or missing values make a gap.
  `line` solid / dash / dot / dashdot / longdash / dashdotdot; without a line style and colour the colour scheme uses
  solid lines in the series colours, monochrome uses black and the series dash patterns. `label` is drawn at the curve
  end, or at `labelAt` (`above`, `below`, `on` – inline on a white backing in a gap –, `left`, `right`); with a legend
  (`legend` auto / inside / right) the labels go into the legend instead.
- **Refs**: `h` value line (panel, `-1` = all panels; label right, left, below or `end` beside the plot), `v` vertical
  marker (through all panels by default, or `panel`; label at the top or `axis` below the x axis), `point` circle marker
  on curve `series` at `x` (`fill` open / filled, label right, left, above, below).
- **Regions**: `between` curves `a` and `b` (with `above` only where `a` lies above `b`: crossing points are computed,
  every part is its own sub-path), `below` a curve (down to the panel bottom or `base`), `span` between `from` and `to`
  (one panel or all). Fill `hatch` (default), `cross`, `solid` (opacity 0.3 in colour), `dots` (core fills).
- **Notes**: `text`, `arrow` (text at the tail), `line` (e.g. extension lines), `dim` (arrowheads at both ends, text
  above/below/left/right of the middle), `numeral` (reference numeral with leader). Points are `x`, `y`, `panel`, or
  `gap` (0 … 1 in the gap below the panel, e.g. a time arrow between two panels) instead of `y`.
- **Reference numerals** (`numeral` on series, refs and regions, numeral notes): the text sits at the target plus
  (`dx`, `dy`) font heights with a thin leader line; `axis: true` puts the numeral of a value line at the y axis.
- **Texts** are a string or `{ "en": …, "de": … }`; numbers may be strings with a decimal comma (`"2,5"`).
- **Robustness**: unknown members are ignored, wrong types fall back to defaults; JSON that cannot be read (or a root
  that is not an object) renders empty principle axes with the message "Figure specification cannot be read (JSON)".
  Rendering never throws. Actions on such a figure throw a bilingual `FormatException` and leave the text unchanged.

### Parts, anchors and actions

Part keys: `series.k`, `marker.k`, `curvelabel.k`, `ref.k`, `region.k`, `note.k`, `numeral.j` (running index over
series, refs, regions and numeral notes), `paneltitle.i`, `caption`, `title`, `error` and the frame parts (`axis.x.1`,
`axis.y.2`, `label.x`, `axistitle.y.2`, `grid.major` … – panels numbered from 1 as in the Bode plot). The parts list
names them with their texts ("Reference line 3 – limit temperature", "Reference numeral 44"). Anchors: plot corners and
centre, origin and axis rails per panel (`axis.x`, `axis.y`, `axis.x2` …), data ports `s<series>.<point>` and
`ref<k>` for point markers.

Element actions: add value line, add vertical marker, hatch between curves 1 and 2 (greyed out without two curves in
one panel), principle axes (toggle all axes), monochrome, "Specification (JSON)…" (input), add area ▸. Part actions:
curves – line style ▾, markers ▾ (none, circle, square, triangle, diamond, cross, plus), smooth curve, fill below ▸,
fill to ▸, delete curve; data points – see [Data points and curves](#data-points-and-curves-on-the-drawing-2026-11-25);
value lines and markers – line style ▾ or filled circle, "Label…", delete; regions – see [Areas](#areas-2026-11-25);
notes – "Text…", delete; axes – principle axes, add a value line in that panel or a vertical marker. Deleting shifts
the indices of later elements (their part looks then apply to the next element).

Host notes: everything is native (lines, freeforms, hatched freeforms, text); no vector picture. Frame extensions
(additive): panel height shares, custom ticks (marks), per-panel axis arrows and grid, panel gap, caption, extra right
margin and axis texts. Not yet: the data table editor (Slidemax `ChartSheet` port), function generators and sketch
curves (stage 3), axis breaks.

## Digital timing diagram (`chart.timing`)

Several signal rows on one shared time axis: the signal name at the left, the wave to the right – logic high and low,
clocks, data values in hexagon-shaped boxes with their text, undefined stretches (hatched) and high impedance (a line in
the middle), with sloped or sharp edges. Optional: grid lines at every time step, a time axis with labels, vertical
markers with labels and dimension arrows between two markers. Category "Principle and technical figures"; drawn
independently (the one-character-per-step idea is the one known from WaveDrom, no code or assets are taken over).

| Id | Name (EN / DE) | Signals |
|---|---|---|
| `chart.timing` | Timing diagram / Zeitdiagramm | `CLK: clock`, `EN: 00111100`, `DATA: xx[A].[B].xx` (inserted without parameters) |
| `chart.timing.clock` | Clock and enable / Takt und Freigabe | `CLK: clock`, `EN: 0011110000`, gated clock `GCLK: 00pppp0000` |
| `chart.timing.bus` | Bus transfer / Busübertragung | CLK, CS, ADDR (`x.[A0]..[A1]..x.`), DATA (`z..[D0]..[D1]..z`), WE; markers `t1=2; t2=5` with a Δt arrow (100 × 70 mm) |
| `chart.timing.handshake` | Handshake (REQ/ACK) / Handshake-Protokoll (REQ/ACK) | REQ, ACK, DATA; markers `t1=2; t2=3` with a Δt arrow (100 × 55 mm) |

### Signal notation (`signals`)

One signal per row, `name: wave`. A semicolon works instead of the colon (`CLK;clock`), so a two-column table (name,
wave) pastes directly. Empty rows and rows starting with `#` are ignored; a row without a separator is a wave without a
name. A wave is either a **clock** or **one character per time step**:

| Written | Meaning |
|---|---|
| `clock` (also `clk`, `takt`) `[period] [duty] [inv]` | clock over the whole diagram; period in time steps (default 1), duty cycle `25%` or `0.25` (default 50 %), `inv` starts low – `clock 2 25%` |
| `0` or `l` / `1` or `h` | low / high |
| `x` | undefined (both levels, hatched between them) |
| `z` | high impedance (line in the middle) |
| `[A5]` | data value with the text "A5" (hexagon-shaped box); `[A][A]` are two transfers with an edge between them |
| `=` | data value without text |
| `p` / `n` | one clock period starting high / low (half a step each) |
| `.` | hold the previous state one more step (after `p` or `n`: one more period) |
| spaces | ignored (`0011 1100`) |

The diagram is as long as the longest written wave (only clocks: 8 steps or four periods, at most 400 steps); shorter
waves hold their last state to the end. Consecutive equal levels merge (`0..1` = `0001`). The parser never throws:
problems (unknown character, missing `]`, `.` at the start, an unknown clock option, an empty wave) are reported with row
and character ("Row 2, character 5: Unknown wave character “q” …"); the drawing shows an unknown character as undefined.

Markers (`markers`): `label=time` (or `label@time`, a bare time gets t1, t2 …) separated by `;`, e.g. `t1=2; t2=5.5`
(decimal comma accepted). Times are in axis units, i.e. time steps × `dt`. Dimension arrows (`spans`): `t1-t2` or
`t1-t2: t_su` (text, default "Δt"), markers by label or by number (`1-2`), several separated by `;`; overlapping arrows
get rows of their own. `dt` (time per step, default 1) and `tunit` (e.g. `ns`) label the time axis ("t (ns)" /
"t in ns").

### Layout, parts, anchors and actions

Text follows the smaller scale factor relative to the design size 100 × 50 mm and the row height (many signals in a
small box get smaller text, a one-direction stretch keeps it); names take at most 35 % of the width and are fitted;
value texts are fitted into their box and left out where a box is too narrow to read.

Part keys: `signal.k` (the wave: one open freeform for both level lines, one closed freeform per value box or undefined
stretch, the value texts), `name.k`, `ref.k` (marker line and label), `dim.k` (dimension arrow and text), `grid.major`,
`axis.x`, `tick.x`, `label.x`, `axistitle.x`, `title`. So one signal can be recoloured or hidden on its own; a look on
the kind (`signal`, `name`) applies to all of them. Anchors: wave area corners and centre (`plot.tl` …), `origin` and
the time axis rail `axis.x`; every edge is a docking port `s<signal>.<edge>` (for cause-and-effect arrows).

Element actions: "Signals…" (input, id `chart.data`: one row per signal, rows separated by `|`; the PowerPoint add-in
opens its table editor; errors are rejected with the row and character), add signal (`S4: 0…1…` as long as the
diagram), remove last signal (greyed out with one signal), grid, time labels, edges sloped / sharp, "Markers…",
"Dimension arrows…" (greyed out with fewer than two markers). Part actions: signal or name – "Signals…", "Name…",
remove this signal, edges; marker – "Markers…", remove this marker, "Dimension arrows…"; dimension arrow – edit,
remove it; grid – grid; time axis parts – time labels.

Clean slides for sharing: signal names become S1, S2 … and marker labels t1, t2 … (texts replaced), data values become
D1, D2 … (normalised) or random hexadecimal values (randomised; equal values stay equal), the time scale becomes plain
steps (normalised: `dt` = 1, unit cleared, markers converted) or a random factor (randomised). The waves stay.

Export: everything is native (freeforms, hatched freeforms, lines with arrowheads, text), so the TikZ and SVG exports
draw it from the primitives. The matplotlib and pgfplots exports have no data axes for it and report "could not be
exported" for a timing diagram.

## Presentation charts (2026-11-26)

The chart kinds presenters need besides XY and bar charts: shares (pie, donut), directions (polar, radar), flows
(Sankey), schedules (Gantt) and distributions (box plot, histogram). All are in the data charts category, insertable
without parameters with neutral demo data (letters as names, like the bar chart), and keep their data in the `data`
text parameter in the [data format](#data-format) above, so the *Data…* prompt and the PowerPoint table editor work
as for the other charts.

| Library item | Name (EN / DE) | Preset |
|---|---|---|
| `chart.pie` | Pie chart / Kreisdiagramm | five slices A–E, percent labels, names in the legend (80 × 65 mm) |
| `chart.pie.names` | Pie chart with names / Kreisdiagramm mit Namen | labels "name percent", no legend |
| `chart.donut` | Donut chart / Ringdiagramm | `hole` 0.55 |
| `chart.polar` | Polar chart / Polardiagramm | two series over eight directions, closed lines |
| `chart.polar.radar` | Radar chart / Netzdiagramm | filled areas, 0° up and clockwise |
| `chart.sankey` | Sankey diagram / Sankey-Diagramm | A, B, C → M → X, Y, Z |
| `chart.gantt` | Gantt chart / Gantt-Diagramm | four tasks, a milestone, today line at 6 |
| `chart.box` | Box plot / Boxplot | three categories with ten samples each and two outliers |
| `chart.histogram` | Histogram / Histogramm | thirty samples, automatic bins |

**Pie and donut.** One slice per category with a positive value, its angle the value's share of 360°; the first slice
starts at `start` (degrees clockwise from 12 o'clock, default 0) and the slices run clockwise or counter-clockwise.
`hole` > 0 makes a donut (inner radius as a fraction of the outer one); `center` writes a text into the hole. A slice
label sits inside its slice – in the ring of a donut – where its text box fits between the radii and the slice edges;
otherwise it moves outside with a leader line, outside labels are kept apart on each side and the pie shrinks so they
fit. Every text stays inside the box (it shrinks before it would leave it). Colour: slices in the series colours with
white seams and white labels; monochrome or `patterns`: the series fill patterns with black edges and labels on a white
backing. With names on the slices the automatic legend is off; otherwise the legend lists the names.

**Polar chart.** Rows `angle;r` (several y columns or blocks are several series, as in the XY chart). The radial axis
starts at 0 in the pole (`rrange` fixes it; radii beyond it are drawn on the rim). The angular axis has spokes every
30° (45° or 90° on a small chart) labelled in degrees outside the circle, the radial grid has rings at the radial
ticks with their labels along the ray half a step after 0°. `zero`: 0° to the right with angles counter-clockwise
(mathematics, matplotlib) or 0° up with angles clockwise (compass). `closed` joins the last point to the first; the
filled style fills each series translucently (monochrome: hatches).

**Sankey diagram.** Every node goes into the column of its longest path from a source; nodes without outgoing flows
go into the last column. A node is as high as the larger of its inflow and outflow, with one scale for the whole
diagram (chosen so the fullest column fills the height), so a node's flows stack exactly over its height on each side.
Columns are centred and ordered by the mean position of their neighbours to reduce crossings; flows leave a node in
the order of their targets and enter in the order of their sources. A flow is a closed freeform with two cubic edges
in the colour of its source (35 % opacity); monochrome: black nodes and white outlined flows. Labels "name value" sit
left of the first column and right of the other nodes. Flows that close a cycle cannot run left to right: they are not
drawn and a note says how many were left out.

**Gantt chart.** Tasks top-down on a category axis (the first row on top), time on a linear axis in any unit (the
x-axis title names it, default "Time"/"Zeit"). A row without duration is a milestone (a diamond); blocks are groups
with their own colour and legend entry. `today` draws a dashed today line with its label "Today"/"Heute" and a rail
anchor `today`.

**Box plot.** Per category and series the quartiles with linear interpolation (Hyndman–Fan type 7, as numpy and
matplotlib), a box from the lower to the upper quartile with the median, whiskers to the furthest samples within
`whis` × IQR beyond the box (Tukey, default 1.5) and the samples beyond them as hollow circles; without `outliers` the
whiskers reach minimum and maximum. Boxes of several series stand side by side.

**Histogram.** Equal-width bins over the samples' range (`xrange` fixes it); a bin holds [left, right), the last
includes its right edge (as numpy). `bins` = 0 chooses ⌈log₂ n⌉ + 1 bins (Sturges' rule for the largest series);
"More bins"/"Fewer bins" step by about a fifth. Several series stand side by side within each bin.

**Parts.** Pie: `slice.k` (k = row of the category), `slicelabel.k` (text, backing, leader line), `centertext`,
`legend`, `title`. Polar: `series.k`, `marker.k`, `axis.a` (circle), `axis.r` (radial axis), `label.a`, `label.r`,
`grid.major`, `legend`. Sankey: `node.k`, `nodelabel.k`, `flow.k` (k = flow row), `note`. Gantt: `bar.g` (bars of
group g), `milestone.g`, `today` and the axis parts. Box plot: `box.k` (boxes, medians, whiskers of series k),
`outlier.k` and the axis parts. Histogram: `bar.k` and the axis parts. Part names use the data ("B", "Flow A → M",
"Node M"); part actions offer the type's data and choice actions, then show/hide and the styles.

**Anchors.** Every chart has `plot.tl` … `plot.c` (pie: the square around the pie, plus `centre`). Ports: pie
`s1.<slice>` on the rim at the slice middle; polar `s<series>.<point>`, `origin` (pole) and the rail `axis.r`; Sankey
`n<node>.in` / `n<node>.out`; Gantt bar ends and milestones `s<group>.<task>`; box plot medians `s<series>.<category>`;
histogram bar tops `s<series>.<bin>`. Gantt, box plot and histogram also have the axis anchors (`origin`, `axis.x`,
`axis.y`).

**Clean slides for sharing.** Pie like the bar chart (values divided by the largest, category names become letters).
Box plot and histogram: all samples through one monotone map (order, quartiles and bins keep their ranks). Gantt: times
shifted to 0 and scaled to 0…1 (normalised) or by a random factor, the today line with them. Polar: radii divided by
the largest (normalised) or scaled and varied, the angles stay. Sankey: node names become A, B, C …, values divided by
the largest flow or scaled by one random factor (in/out balances stay).

**Code export and import.** These charts have no matplotlib or pgfplots export yet: both exports report "could not be
exported" for them; the TikZ and SVG exports draw them from the primitives. The matplotlib import reads `pie` (values,
`labels`, `autopct` → percent labels, `startangle` and `counterclock` converted to the clockwise-from-12-o'clock
convention, `wedgeprops=dict(width=…)` → donut hole; `explode`, `colors` and `normalize=False` are reported) and polar
axes (`subplot_kw={'projection': 'polar'}`, `add_subplot(…, projection='polar')` or `polar=True`, `plt.polar`):
`plot` curves with the angles converted from radians to degrees, `fill` → filled style, `set_theta_zero_location('N')`
with `set_theta_direction(-1)` → compass style, `set_rmax`/`set_rlim`/`set_ylim` → `rrange`; polar tick settings
are reported. Other projections (3d) are still reported, `hist` and `boxplot` are not imported yet.

**Host notes.** Native primitives only: slices, rings and flows are closed freeforms (flows with cubic edges), nodes,
bars and boxes rectangles, milestones closed freeforms, grids one freeform each, texts native text; no vector picture.

## Linked characteristic charts (Calc 5)

A characteristic chart can be linked to an element whose values give a curve set (contract `ICharacteristics` in
`src/InventaSketch.Core/Characteristics.cs`; design notes in section 14 of
[engineering-calculations.md](../design/engineering-calculations.md)). The chart (`chart.linked`) stores only the link
`<element id>#<set key>` in its parameter `link`. At every render it asks the element for the set, computed from the
element's solved values, and draws it like a figure: one panel per y axis, the curves (the element's own curve solid,
load and system curves dashed), bands (the inverter's MPP window), limit lines and the marked points (operating point
filled, design and breakdown points open). Notes on assumed values appear as the caption. The element lists the chart
as a dependent, so hosts redraw the chart with every change of the element.

| Element (module) | Set | Curves and points |
|---|---|---|
| Centrifugal pump (Process), circulator (Building services) | Pump and system curve | H(V̇) = H₀·(1 − (V̇/V̇_max)²), system curve H_stat + k·V̇² through the operating point; operating point at the pump's flow (grip) |
| Machine cross-sections, motor and e-machine blocks (Machines) | Torque–speed curve | Kloss curve through n_s and the rated point for the squirrel-cage rotor, torque envelope otherwise; operating point (grip), breakdown point |
| PV module and string (Energy) | PV I–V and P–V curves | I(U) and P(U) at STC (two panels), MPP, inverter MPP window, U_DC,max and U_oc at θ_min |
| Resistor, capacitor, inductor in an RC/RL/RLC circuit (Electrical) | Bode plot, step response | magnitude and phase with the corner (natural) frequency (grip: enters C or L); step response with τ (63.2 %) or the overshoot |
| Gears and shafts (Mechanical) | Gear train speeds and torques | one bar per shaft for n, T and P |
| Boiler, heat pump (Building services) | Heating curve | flow and return temperature over the outdoor temperature; design point (grip: enters the flow temperature), current operating point (grip: the outdoor temperature) |

Actions:

- On the element: **Characteristic chart ▸** (a menu, one command per set) adds a chart linked to that set right of
  the element, in one undo step.
- On the pump, torque–speed, Bode, figure and linked charts: **Link to element ▸** lists the curve sets of the elements
  on the same slide (nearest first, at most eight elements); choosing one turns the chart into a linked chart (position,
  size and title stay).
- On the linked chart: **Unlink (freeze values)** turns it into a figure (`chart.figure`) whose specification holds the
  current curves, points, bands and limit lines as plain data; later changes of the element no longer reach it. It is
  greyed out while the element is missing.

Grips: every draggable point has a handle `op.<point>` on its marker, moving along x (along the curve) or along y. On
drop the new value (snapped to the axis resolution) is handed to the point, which returns the values to write; they
are stored in the element like a dragged value (three significant digits; entered values that would over-determine
the element give way, `ValueDrag`). Curve data that were assumed are stored with the first drag, so the curve stays
where it is. The chart itself does not change: it follows the element. This is a document-level change: hosts call
`DocumentHandles.Move(registry, doc, chartId, key, position, lang)` (it falls back to `ObjectType.MoveHandle` for
every other type) and apply the result as one undo step; the object-level `MoveHandle` of the linked chart changes
nothing.

A missing element or an unreadable link shows a message in an empty frame ("Linked element not found: unlink or link
again"). The matplotlib and pgfplots exports write a linked chart as the figure it would become when unlinked.

## Host notes

All output uses native primitives: one freeform per series line (smooth curves as cubic Bézier paths), one
multi-sub-path freeform each for grid lines, ticks, contour levels, limit and helper lines, one freeform per marker
set, rectangles for bars, legend and label backings, a closed polygon for the band, and text. Axis titles of the
y axes are rotated by −90°. Hatch and dot fills use the core fill kinds. No vector picture is needed. Charts do not
depend on neighbouring objects, except the linked characteristic chart (`chart.linked`), which renders from the linked
element (`DependsOnNeighbours`; the element names it as a dependent). Its operating point grips change the element:
hosts apply them with `DocumentHandles.Move` (see above).

## Charts from pictures (building blocks)

Stage 5 of [charts v2](../design/charts-v2.md): pure image analysis in `src/InventaSketch.Modules.Charts/Scanning`
on a raw `PixelImage` (width, height, RGBA bytes; transparent pixels count as white). No host API, no OCR and no
external service is needed; hosts supply the picture (clipboard, file, screen capture) and later the chart-type
integration. Nothing throws: empty, tiny, blank or inconsistent pictures give bilingual `Warnings`.

- **Bar scanner** (`BarScanner`, ported from the Slidemax chart scanner): background = most frequent colour,
  connected areas of similar colour, thin long pieces are axis or gridlines, the baseline is the edge where most areas
  end; finds columns or bars, stacked (series by colour) or clustered, parts below the baseline and the gridline step.
  `BarScanner.Values` turns pixel extents into numbers with a `BarScale` (baseline value plus the value of the largest
  bar or of one gridline step).
- **Labels**: `ITextReader` is the interface for a host OCR; the engine ships `DigitTextReader` (ported Slidemax digit
  reader: digits, separators, signs and percent in common fonts by pattern correlation, no letters).
  `BarLabelReader` derives the scale from the value axis labels (robust line fit, German and English number styles) or
  from value labels at the bars, and reads category names. `TextReaderGate` lets a host switch a failing OCR off once.
- **Curve digitiser** (`CurveDigitizer`, new): finds the plot area and the axes (lowest long horizontal and leftmost
  long vertical grey line; other long lines are gridlines and are masked out), clusters the curve colours (every pixel
  is explained as a mix of background and curve colour, so anti-aliasing and blur stay with their curve), traces each
  colour column by column (crossings keep the slope of both curves, gaps of dashes and crossings are bridged, fragments
  are joined), drops short traces (text, legend), reports groups of short parallel straight traces as `Hatches` and
  guesses the style solid / dashed / dotted from the gaps. Black-and-white pictures give one colour whose curves are
  separated by the tracing.
- **Calibration**: `DigitizeOptions.XAxis` / `YAxis` (`AxisCalibration`: two pixel positions with known values, linear
  or logarithmic) or, for axes without one, the labels read by `DigitizeOptions.TextReader`
  (`CurveLabelCalibration`, logarithmic axes recognised from 1, 10, 100 …). Without a calibration the points stay in
  pixels with a warning.
- **Result**: `ScanResult` with plot area, axes, gridlines, series (colour, style, pixel and data points), hatches,
  calibration and warnings. `ScanDataText.FromCurves` writes the chart data text for `chart.xy` (rows `x;y`, a blank
  line between series, `#` comments with style and colour); `ScanDataText.FromBars` writes a category table for
  `chart.bar`.
- **Accuracy (synthetic tests)**: crossing curves in two colours within 0.7 % of the y range (1.1 % after a 3 × 3 box
  blur and noise; 98 % of the points within 0.1 %), a crossing solid / dashed black pair within 0.8 %, a steep sigmoid
  within 0.2 %, bars within 1.4 % of the largest value after blur, calibration from stamped digit labels within 0.8 %.
- **Limits**: no rotated pictures (axes must be horizontal and vertical), curves of the same colour crossing at a very
  shallow angle may swap, filled area charts are reported as "filled areas" (use the bar scanner), the digit reader
  reads only numbers in fonts similar to the stored patterns (a host OCR can be plugged in).

## Curve functions, sketch recognition and axis breaks (building blocks)

Charts v2 stage 3 (2026-11-25): pure, host-free libraries in the charts module; chart types and hosts do not use them
yet (integration follows in the figure model).

- **Curve functions** (`CurveFunctions`, `CurveSpec`): constant, ramp (linear), step, rectangle pulse, pulse train,
  sawtooth, triangle, sine, exponential approach (PT1), exponential decay, second-order step response (PT2), Gaussian
  bump and circle/ellipse (closed, parametric). Each kind has a bilingual name and parameter definitions (name,
  default, valid range, bilingual label; unit-less – they take the units of the axes; `ToParamDef()` for the
  inspector). Text form, invariant culture: `pt1(y0=20, yinf=160, tau=12)`; positional values (`sine(2, 10)`), case
  does not matter, missing parameters take their defaults, optional shifts/offsets (`t0`, `phase`, `offset`) are left
  out while neutral, `circle(cx, cy, r)` gives an ellipse. A German decimal comma is accepted where unambiguous
  (`tau=1,5` between two digits in a named value, or all arguments separated by `;`). Errors throw
  `CurveSpecFormatException` (a `FormatException`) with an English and a German message. `Sample(xMin, xMax)` returns
  exact corners for piecewise-linear kinds (a jump is two points with the same x), a vertex at the start `t0` of
  PT1/PT2/decay, at least 64 segments per sine period and a dense band around a narrow Gaussian bump (at most 20 000
  points).
- **Sketch recognition** (`SketchFit.Fit(trail, options)`): input is the pointer trail in data coordinates and,
  preferably, the plot frame in data coordinates (all tolerances are relative to it, so they act like screen
  distances). The trail is resampled by arc length, simplified (Ramer–Douglas–Peucker), checked for a closed loop and
  fitted with constant, ramp, step, rectangle pulse, sawtooth, triangle, sine, PT1, decay or (closed) an axis-parallel
  ellipse: a grid search with linear least squares gives the start, Nelder–Mead refines it on a robust (Huber),
  symmetric trail ↔ curve distance, so vertical edges, overshoots and pen hooks are handled. Among the kinds whose
  residual is at most 1.25 × the best + 0.003 the one with the fewest parameters wins; it is accepted up to a residual
  of 2 % of the frame. The result has the kind, the spec, residual, confidence, the full ranking, the simplified trail
  and always a smoothed trail (centripetal Catmull–Rom; the uniform variant is the Slidemax `Smoothed` formula), which
  is the curve to use when nothing fits. All thresholds are named constants in `SketchFit`.
- **Axis breaks** (`BrokenAxis`, `BrokenPlot`, `AxisBreaks`, ported from Slidemax and generalised from bar charts to
  any x or y axis with continuous values): value ↔ position with one or more breaks (values `Lo … Hi` compressed into
  a gap in mm; values inside a break map linearly into the gap, the inverse holds), page points on axes in any
  direction, XY mapping, `VisibleRuns` cuts curves exactly at the break limits, automatic placement of breaks in
  data-free gaps (`AxisBreaks.Spans`, the Slidemax rules: own extent, shrink to fit, drop ineffective breaks) and the
  zigzag or double-slash markers (sizes from Slidemax: lines 4.5 pt apart, teeth 6.5 × 1.75 pt, slashes at 15°) across
  the plot or short on the axis line. Linear axes only. Drawn by the chart types since stage 3, see
  [Axis breaks](#axis-breaks-charts-v2-stage-3).

Host capability: the building blocks produce data (specs, polylines, marker points); a host will draw them with the
existing native primitives (freeforms and closed polygons), no vector picture is needed.

## Export as matplotlib and pgfplots (charts v2 stage 4)

The charts module offers two domain exports (`IExportProvider`) for selections that contain charts (`chart.*`), so a
chart can be recreated as code in which every text stays editable:

- **Charts as Python (matplotlib)** (`chart.matplotlib`, `.py`): a self-contained script for matplotlib 3.5 or later,
  plain lists, no numpy code. One function `figure_1()`, `figure_2()` … per chart; `python script.py` shows the
  charts, `python script.py out/chart.pdf` saves `out/chart-1.pdf`, `out/chart-2.pdf` … (the extension picks the
  format). Texts are ordinary matplotlib texts, made mathtext-safe (`$` escaped).
- **Charts as LaTeX (pgfplots)** (`chart.pgfplots`, `.tex`): a `standalone` document (pdfLaTeX or LuaLaTeX,
  pgfplots 1.18 compatibility, libraries `fillbetween`, `groupplots`, `arrows.meta`, `patterns.meta`) with one
  `tikzpicture` per chart (one page each); the `tikzpicture` can be copied into any document. Every text — axis titles,
  named ticks, legend entries, titles, notes, numerals — is LaTeX text in the document font. Special characters are
  escaped, Greek letters and symbols become LaTeX commands (pdfLaTeX-safe), German tick labels use
  `/pgf/number format/use comma`.

Both read the chart models, not the drawing: the data text, the parameters and the figure specification, with axis
ranges, ticks and axis titles taken from the composed frame (the same layout the renderer uses). Numbers are always
written in invariant culture (a German decimal comma never reaches the code; value labels in German stay text with a
comma). A subscript written as `t_krit` or `f_c` becomes a math subscript (`$t_{\mathrm{krit}}$`). Every item carries
its part key as a comment (`# series.0`, `% region.0`), and part looks apply: hidden parts are left out, part styles
override colour, width and dash.

| Feature | matplotlib | pgfplots |
| --- | --- | --- |
| Figure size | `figsize` from the element size (mm → inch) | `scale only axis` with the plot area of each panel (mm) |
| Stacked panels (figures, Bode) | `plt.subplots(n, 1, sharex=True)`, height ratios from the panel shares | `groupplot` (1 × n), x labels at the bottom edge |
| Curves | `plot` with colour, width, dash (solid, dashed, dotted, dash-dot, long dash, dash-dot-dot), markers (filled/open) | `\addplot` with the same options |
| Gaps | `nan` in the lists | `nan` with `unbounded coords=jump` |
| Steps | `drawstyle='steps-post'` | `const plot` |
| Smooth curves | the renderer's spline, sampled (in log space on log axes) | same points |
| Scales, ranges, ticks | log scales, fixed ranges, `MultipleLocator(step)`, named marks | `xmode=log`, `xtick distance`, named `xtick`/`xticklabels` |
| Grid | major/minor per axis, grid colour and dash | `xmajorgrids`, `xminorgrids`, grid styles |
| Principle axes | no ticks, arrowheads at the axis ends | `axis lines` with `-Stealth` |
| Value lines, limit lines | `axhline` with label (`annotate`) | `\draw` with a `\node` |
| Vertical marker through all panels | `ConnectionPatch` from the top of the first to the bottom of the last panel | `\coordinate`s in the panels, line drawn after the group |
| Regions | `fill_between` with hatch `//`, `xx`, `..` or solid; "only where A is above B" as `where=[a >= b …]` mask; spans as `axvspan` | `fill between` (`fillbetween` library) with `patterns.meta` patterns; masked regions as the filled polygons computed from both curves |
| Notes, arrows, dimensions | `annotate` (arrow `-\|>`, dimension `<\|-\|>`) | `\node`, `\draw` with `Stealth` tips |
| Reference numerals | `annotate` with a leader line (offset in font heights) | `\node` shifted in `em` plus a leader `\draw` |
| Bars | `bar`/`barh`, grouped by offsets, stacked with `bottom`/`left`, value labels with `bar_label`, patterns in monochrome | `ybar`/`xbar` (stacked), `nodes near coords` with the labels as symbolic meta |
| Efficiency map | `contour` over the field (nan outside the envelope) with `clabel` | traced contour lines as plots, labels as nodes |
| Torque power curve | `twinx` | an overlaid axis with `axis y line=right` |
| Tolerance band | `fill_between` lower/upper | `fill between` of both limit curves |
| Title, caption, panel titles | `suptitle`, `supxlabel`, texts in the panels | `title`, a node below the picture, nodes |

Approximations: layout details (label positions shrunk to fit, the white backing of labels, maximum widths of notes,
the exact spacing of hatch lines and arrowheads) follow each library's own layout; font sizes are relative (the
chart font in matplotlib, `\footnotesize` and smaller in LaTeX). Stacked bars with negative values are stacked by
pgfplots in data order. The positions of texts that only exist in the layout (region notes of the torque chart, the
operating point label, the corner frequency label) are taken from the composed chart and converted to data
coordinates. A figure whose specification cannot be read exports its message.

Tests (`ExportTests`, `ExportRunTests`): every library item in English and German gives a plausible script (balanced
brackets, every visible curve, bar, region, reference and numeral present) and a plausible document (balanced braces,
paired environments, every colour defined); checks for the figure presets (hatched region with the where-mask,
vertical marker across the panels, numerals), hidden parts, part colour overrides, decimal commas and escaping. When
Python is installed the scripts are parsed by Python; with matplotlib they are run headless (Agg) and must save every
figure; with pdflatex the documents are compiled. Without these tools those tests are skipped.

## Tests and gallery

`tests/InventaSketch.Modules.Charts.Tests` covers registration, bilingual names, parameters, options and actions,
search in both languages, the data parser (dot and comma decimals, tabs and Excel header rows, blank-line and header
series, `|` rows, CSV, quoted text, garbage), ranges and lists, axis ticks (nice steps, linear with fixed ranges and
decimal comma, log decades and minor ticks, SI labels, phase steps, category bands), every library item and every
bare type rendered inside its box (English, German, monochrome, stretched, small and large), data drawn, scaling
changes `Size` not data, anchors and data ports, monochrome distinguishability (dashes, markers, patterns), stacked
and horizontal bars, log axes, clipping, step and smooth styles, empty data, German titles, limit lines, the
engineering formulas (Bode response at the corner frequency and slopes, constant power, efficiency peak, contour
tracing, pump operating point, tolerance band columns and polygon clipping), all context actions and inputs, the
core action dispatcher, JSON round trip and monochrome SVG export. The gallery test writes
`gallery-charts/*.html` (every preset in colour, German monochrome and stretched) for visual review.
`TimingChartTests` covers the timing diagram: every wave kind of the notation, clocks, errors reported with row and
character, markers and dimension arrows, tagged parts, edge ports, sharp and sloped edges, text size under scaling,
actions and inputs, cleaning, the JSON round trip and the export behaviour; it writes `gallery/timing/*.svg`.
`PresentationChartTests` covers the presentation charts: slice angles summing to 360° in both directions, percent
texts, labels inside the slice or the donut ring and outside with leader lines inside the box at several sizes,
monochrome patterns, polar angle conventions and radial scale, Sankey node heights equal to their in/out sums and
ribbons filling each node, cycles reported, box plot quartiles, whiskers and outliers of a known sample, histogram
counts and Sturges' rule, Gantt bars, milestones and today line, every action, rendering inside the box at extreme
sizes, the JSON round trip, the export reports, cleaning and the import of matplotlib pie and polar scripts.
