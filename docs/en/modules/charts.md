# Charts & characteristic curves module (`charts`)

Technical charts for slides: XY line, scatter and step charts, bar/column charts, plain axes with grid, the
torque–speed curve of a motor, an efficiency map, a pump/fan curve with operating point, a Bode plot and a tolerance
band. Every chart is drawn with native primitives (lines, freeforms, rectangles, text), so it stays editable with the
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
| `chart.math.axes` | Coordinate system (2D) / Koordinatensystem (2D) | math style, see [Coordinate systems and function graphs](#coordinate-systems-and-function-graphs-charts-v2-stage-4) |
| `chart.math.function` | Function graph / Funktionsgraph | basic function, typed formula or function generator in the math-style coordinate system |
| `chart.timing` | Timing diagram (digital) / Zeitdiagramm (digital) | `signals` (one signal per row, see [Digital timing diagram](#digital-timing-diagram-charttiming)), `slope` sloped / sharp, `grid`, `timelabels`, `markers`, `spans`, `title`, `dt`, `tunit` |

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
`chart.timing`, `chart.timing.clock`, `chart.timing.bus`, `chart.timing.handshake`, the eight coordinate systems and function graphs listed below) in four categories: data
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
| XY chart | "Data…" (input), style ▸ (line → line with markers → markers → steps), smooth curve, x scale ▸, y scale ▸, grid, legend ▸ |
| Bar chart | "Data…", orientation ▸, several series ▸ (grouped / stacked), value labels, grid, legend ▸ |
| Axes | grid, arrowheads, x scale ▸, y scale ▸ |
| Torque curve | power curve, region notes, grid, legend ▸ |
| Efficiency map | "Contour levels…" (input), contour labels, grid |
| Pump / fan curve | machine ▸ (pump / fan), "Operating flow…" (input), system curve, grid, legend ▸ |
| Bode plot | system ▸, "Corner frequency…" (input: `1 kHz`, `250 Hz`, `2.5k`, `2,5k`), show ▸, corner frequency mark, grid |
| Tolerance band | "Data…", "Tolerance…" (`10 %` = percent, `0.5` = absolute), smooth curves, grid, legend ▸ |

Invalid inputs throw `FormatException` with a bilingual message and change nothing. Cycles use `IconIds.Cycle`,
toggles `IconIds.Toggle`, inputs `IconIds.Text`. The chart types have no view switch.

## Parts (charts v2, 2026-11-25)

Every drawn piece of a chart is a **part** with a key – the primitive tag: `series.0`, `series.1` (curves), `marker.0`
(markers of curve 1), `bar.0` (bars of series 1), `value.0` (value labels), `axis.x`, `axis.y`, `axis.y2` (secondary
axis), `axis.box` (closed frame), `tick.x`, `tick.y`, `label.x`, `label.y` (tick labels), `axistitle.x`, `axistitle.y`,
`grid.major`, `grid.minor`, `zero`, `limit`, `legend`, `title`, `band`, `contour`, `contourlabel`, `envelope`, `helper`,
`point`, `note`. Stacked panels (Bode plot) add the panel number: `axis.y.2`, `label.y.2`.

- Each part can be switched off and styled on its own (`SketchObject.Parts`: per key a look with style or hidden). A
  look on the kind (`series`, `grid`) applies to all parts of that kind; the indexed key wins.
- Part actions: the chart's own ones that concern the part (curve: data, style, smoothing; axis: scale; grid: grid
  toggle; legend: position), then show/hide, thicker, thinner, line style and reset.
- PowerPoint: a click without dragging on the selected chart selects the part under the pointer (outlined in blue); the
  bar shows its name and actions, "Parts …" lists every part including hidden ones, "Whole element" leaves the part.
  Formatting one curve with PowerPoint's own tools (line colour, weight, fill, font) changes that part only and survives
  every redraw; formatting the whole chart still changes the element style.
- Engine: `parts`, `partAt`, `partActions`, `applyPartAction`, `setPartLook` (see the design note
  [charts v2](../design/charts-v2.md)).

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
one panel), principle axes (toggle all axes), monochrome, "Specification (JSON)…" (input). Part actions: curves – line
style, markers, smooth curve, hatch below the curve; value lines and markers – line style or filled circle, "Label…",
remove; regions – fill, remove; notes – "Text…", remove; axes – principle axes, add a value line in that panel or a
vertical marker. Removing shifts the indices of later elements (their part looks then apply to the next element).

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

## Host notes

All output uses native primitives: one freeform per series line (smooth curves as cubic Bézier paths), one
multi-sub-path freeform each for grid lines, ticks, contour levels, limit and helper lines, one freeform per marker
set, rectangles for bars, legend and label backings, a closed polygon for the band, and text. Axis titles of the
y axes are rotated by −90°. Hatch and dot fills use the core fill kinds. No vector picture is needed. Charts do not
depend on neighbouring objects.

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
