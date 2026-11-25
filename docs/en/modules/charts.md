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

Common parameters (where they apply): `title`, `xlabel`, `xunit`, `ylabel`, `yunit` (empty = the type's
bilingual default, an explicitly empty label hides the title), `xscale` / `yscale` linear / log, `xrange` / `yrange`
(`min;max`, empty = automatic, advanced), `grid`, `legend` automatic / inside / right / none, `box` (closed frame).
Axis titles read "label (unit)" in English and "label in unit" in German (DIN 461 style), e.g. "Speed n (rpm)" /
"Drehzahl n in 1/min". Tick labels use the decimal comma in German.

The efficiency map uses a qualitative loss model (constant losses, copper ∝ T², iron ∝ n, windage and eddy currents
∝ n³), scaled so its maximum equals `etamax`. It shows the typical island shape for sketches; it is not measured
data. The pump curve is H(Q) = H₀ (1 − (Q/Q_max)²); the system curve runs through the static head and the operating
point. The Bode plot is computed from the standard transfer functions of first- and second-order low and high passes.

The library has 25 presets (`chart.line`, `chart.line.markers`, `chart.line.multi`, `chart.line.smooth`,
`chart.line.logx`, `chart.scatter`, `chart.step`, `chart.column`, `chart.column.grouped`, `chart.column.stacked`,
`chart.bar.horizontal`, `chart.axes`, `chart.axes.plain`, `chart.axes.loglog`, `chart.limits`, `chart.torque`,
`chart.torque.plain`, `chart.efficiency`, `chart.pump`, `chart.fan`, `chart.bode`, `chart.bode.lowpass1`,
`chart.bode.highpass1`, `chart.band`, `chart.band.measured`) in two categories: data charts and characteristic curves.

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
