# Annotation module (`annotation`)

Library: `src/InventaSketch.Modules.Annotation` · Tests: `tests/InventaSketch.Modules.Annotation.Tests` ·
German version: [../../de/modules/annotation.md](../../de/modules/annotation.md)

The module provides the marks that go on top of a sketch: lines and arrows, labels and text, dimensions, technical
drawing indications (geometric tolerances, datums, surface texture, welds), hatching, figure marks, technical-mechanics
symbols for free-body diagrams with **shear force and bending moment diagrams**, and flowchart starters. All type ids
start with `ann.`. All symbols are drawn independently from lines, arcs, circles and text; no standard artwork, texts
or tables are copied and no compliance with a standard is claimed. Where a symbol family is named below, the name only
says which kind of mark is meant.

## Conventions

- **Quick sketch first:** every library item inserts without parameters; free scaling changes `Size`, never the
  parameters or the text height.
- **Point objects** (leaders, dimensions, tolerance frames, datum and weld symbols, forces) render in page coordinates
  from their points; **box objects** (surface symbol, beam diagram, cloud, supports) render centred at (0, 0) in `Size`.
- Text heights default to 3.5 mm for drawing indications and dimensions, 5 mm for labels.
- **Choices are dropdowns:** characteristic, weld kind, process, supports and load are one choice each; options that no
  shared pictogram fits carry a small drawing of the option (`ContextAction.Preview`).

## Categories

| Category | Contents |
|---|---|
| `ann.lines` Lines & arrows | lines, polylines, arrows (straight, curved, elbow, block, double-line), junctions |
| `ann.labels` Labels & text | text, label, callout, balloon, parts list, table |
| `ann.dimensions` Dimensions | linear, aligned, angle, radius/diameter, **chain**; **geometric tolerance frame, datum feature, surface texture symbol, weld symbol** |
| `ann.hatching` Hatching | hatched rectangles, ellipses and polygons with material patterns |
| `ann.patent` Figure marks | leaders with numerals, figure label, section line, detail circle and view, centre line, break line, brackets, view arrow, **revision cloud** |
| `ann.mechanics` Technical mechanics | force, moment, coordinate system, centre of gravity, ground, supports, distributed load, beam, spring, damper, mass, **shear and moment diagrams** |
| `ann.flow` Flowchart | flowchart symbols and connectors (see [diagrams.md](diagrams.md)) |

## Technical drawing indications

| Type id | Element | Parameters (all optional) |
|---|---|---|
| `ann.gdt.frame` | Geometric tolerance indication: feature control frame with the characteristic pictogram, the tolerance value and up to three datum letters; a leader with an arrow to the feature. Point 0 = arrow tip on the feature, point 1 = the frame end the leader starts from; the frame extends away from the tip. Symbol family of ISO 1101. | `characteristic` (straightness, flatness, circularity, cylindricity, profile of a line, profile of a surface, parallelism, perpendicularity, angularity, position (default), concentricity/coaxiality, symmetry, circular run-out, total run-out), `tolerance` ("0.1"; the object text overrides it), `diameter` (Ø before the value; default on for position and concentricity), `datum1` ("A"), `datum2`, `datum3`, `textSize` (3.5) |
| `ann.gdt.datum` | Datum feature indicator: triangle on the feature joined to a square frame with the letter. Point 0 = middle of the triangle's base, point 1 = frame centre. | `letter` ("A"; the object text overrides it), `filled` (true; open triangle otherwise), `textSize` |
| `ann.surface` | Surface texture symbol: two legs of unequal length at 60°, the tip on the surface (mount anchor `tip`); bar for "material removal required", circle for "material removal not permitted"; the requirement under a line extending the long leg. Symbol family of ISO 1302. | `process` (any / removal (default) / noRemoval), `value` ("Ra 3.2"; the object text overrides it, empty text = symbol only), `allAround` (circle at the corner), `textSize` |
| `ann.weld` | Weld symbol: arrow line to the joint, reference line running away from the tip, dashed identification line, elementary symbol on the arrow side (above the reference line), on the other side (below the dashed line) or on both sides (no dashed line). Point 0 = arrow tip, point 1 = start of the reference line. Symbol family of ISO 2553 (system A idea). | `kind` (fillet, square butt, V, bevel, plug/slot), `side` (arrow / other / both), `allAround` (circle at the knee), `size` (left of the symbol, e.g. "a5"; the object text overrides it), `length` (right of the symbol), `refLength` (24 mm), `textSize` |

Library presets: flatness (`ann.gdt.frame.flatness`, no datums), parallelism (`ann.gdt.frame.parallelism`), circular
run-out (`ann.gdt.frame.runout`) and V butt weld (`ann.weld.v`).

Rules built in: form tolerances (straightness, flatness, circularity, cylindricity) never show datum letters, and the
"Datums" action is greyed out for them. The "Datums" action reads up to three letters separated by spaces, commas, `|`
or `;` ("A B C").

## Shear force and bending moment diagrams (`ann.beam.diagram`)

A box with two bands: the shear force (top, symbol V in English and Q in German drawings) and the bending moment
(bottom), each hatched across, with the largest value by magnitude labelled ("max V = F/2", "max M = F·L/4"). The box
width stands for the beam length.

**Reading a drawn beam.** Placed below an `ann.beam` (the nearest beam above the diagram whose width covers the
diagram's centre, at most 40 mm or 1.5 diagram heights above it), the diagram reads that beam:

- pin and roller supports (`ann.support.pin`, `ann.support.roller`) whose pin touches the beam (within 3 mm);
- fixed supports (`ann.support.fixed`) whose wall touches the left or right end of the beam;
- forces (`ann.force`) whose tip touches the beam; slanted forces count with their component across the beam, forces
  pointing away from the beam's lower side count negative, forces along the beam are ignored;
- distributed loads (`ann.load.distributed`) whose lower edge stands on the beam (uniform over their width).

Positions are shares of the beam length; shares within 0.5 % of a simple fraction (halves to twentieths) are taken as
that fraction, positions within 3 mm of an end as the end.

**Values.** A load label such as "10 kN", "F = 10 kN" or "q = 2.5 kN/m" is a number with a unit; the optional
`value` parameter of a force or distributed load ("Magnitude (beam diagrams)") wins over the label. Any other label
("F", "q", "F₁") is a symbol. The beam's optional `length` parameter (m) gives the real length.

| Loads and length | Labels |
|---|---|
| all loads numbers, length given | numbers with units: "max V = 5 kN", "max M = 10 kN·m" |
| point loads as numbers, no length | "max V = 5 kN", "max M = 2.5 kN·L" |
| one symbol of one kind (all point loads "F", or all line loads "q") | exact multiples: "F/2", "F·L/4", "q·L/2", "q·L²/8", "−F·L"; short fractions up to 1/64, otherwise three decimals |
| mixed symbols, symbols together with numbers, line loads as numbers without a length | "max V", "max M" without a value (the curves are drawn with unit values) |

**Without a beam** (or above a beam that has neither supports nor loads), the diagram shows its own load case:
`support` (simply supported (default) / cantilever with the wall left / wall right), `load` (point load F (default) /
uniform load q / both), `at` (position of F as a share, 0.5), and optionally `force` (kN), `lineLoad` (kN/m) and
`length` (m) for values. Presets: `ann.beam.diagram.cantilever` (end load) and `ann.beam.diagram.uniform`.

**Other parameters:** `momentBelow` (true: positive, sagging moments are drawn below the axis, on the tension side, as
usual in German textbooks; false: above), `textSize` (3.5).

**Statics** (`BeamStatics`, pure computation, no host): statically determinate straight beams under transverse loads –
two hinged supports (pin and roller; overhangs allowed; two pins are accepted because only transverse loads are
considered) or one fixed support. Reactions from the equilibrium conditions, shear force as the sum of the upward forces
left of the section, bending moment positive when it sags the beam; the moment's extreme inside a loaded stretch is
found where the shear force passes zero. Other support arrangements (none, one hinge, three supports, fixed plus roller)
are not statically determinate: the diagram then shows its axes and the note "not statically determinate". Unit tests
cover the textbook cases (simply supported beam with centre load: reactions F/2, M_max = F·L/4; uniform load:
M_max = q·L²/8; cantilever with end load: M = −F·L at the wall; off-centre load F·a·b/L; overhang; equilibrium checks).

Redrawing: the diagram depends on its neighbours; beams, supports, forces and distributed loads name the diagrams on
their page as dependents, so moving a load redraws the diagram.

## Other additions

| Type id | Element | Parameters (all optional) |
|---|---|---|
| `ann.cloud` | Revision cloud: a closed chain of outward arcs inside the box, marking a changed area | `arc` (arc length, 5 mm) |
| `ann.dimension.chain` | Chain dimension: several points measured one after another along one dimension line, one value per stretch; points are taken in their order along the measuring direction | `offset` (−8 mm), `axis` (auto / horizontal / vertical), `terminator`, `prefix`, `unit`, `decimals`, `scale`, `textSize` (no common override text) |
| `ann.beam` | gains an optional `length` (m) for beam diagram values and companions (supports, force, distributed load, diagram) | |
| `ann.force` / `ann.load.distributed` | gain an optional `value` (magnitude / load per length) for beam diagrams | |

## Host notes

Everything is drawn with lines, polylines, polygons, rectangles, circles and text, so every host draws native,
editable shapes. The beam diagram's bands use a hatch fill (vertical lines), which hosts draw like other hatched regions.
The tolerance frame and the datum frame have a white fill so they hide what lies behind them. Option pictures of the
choices (`Preview`) are shown by hosts that render previews; others list the option names.

## Open items

- Modifiers in tolerance frames (maximum material, projected zone, free state), composite frames and tolerance
  indications on dimension values are not modelled.
- Weld symbols: no supplementary symbols (flush, convex, concave), no tail with process number, no staggered welds.
- Beam diagrams: only transverse point and uniform loads; no concentrated moments (`ann.moment`), no trapezoidal loads,
  no hinges inside the beam, no axial forces; the diagram does not stretch to the beam's page width automatically.
