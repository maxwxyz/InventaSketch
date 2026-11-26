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
| `ann.mechanics` Technical mechanics | force, moment, coordinate system, centre of gravity, ground, supports, distributed load (uniform, triangular, trapezoidal), beam, internal hinge, spring, damper, mass, **internal force diagrams (normal force, shear force, bending moment, deflection line)** |
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

A box with one band per row, top to bottom: the normal force N (only when loads act along the beam), the shear force
(symbol V in English and Q in German drawings), the bending moment and, when chosen, the deflection line w. Each band is
hatched across (the deflection line is drawn as a plain curve), with the largest value by magnitude labelled
("max N = √2·F/2", "max V = F/2", "max M = F·L/4", "max w = F·L³/(3·EI)") and the position of the largest bending
moment and of the largest deflection written on the other side of the axis ("x = L/2", "x = L/√3", "x = 2.31 m").
Applied couples show as jumps of the moment line, uniform loads as parabolas, triangular and trapezoidal loads as
parabolic shear and cubic moment lines; internal hinges are marked by small open circles on the moment axis, where the
moment is zero. Without a beam the box width stands for the beam length; under a beam the diagram stretches to the
beam's ends on its own (see below). The rows share the box height equally.

**Reading a drawn beam.** Placed below an `ann.beam` (the nearest beam above the diagram whose width covers the
diagram's centre, at most 40 mm or 1.5 diagram heights above it), the diagram reads that beam. Its axes then run from
the beam's start to its end as projected onto the diagram, whatever the diagram's own width: the diagram stretches to
the beam on its own (and follows a mirrored beam, its start drawn under the beam's start). The drawing may then reach
beyond the diagram's box; the action **Fit to the beam** (`ann.beamdiagram.fit`, greyed out when the box already fits)
sets the box to the beam's width and centre so that the selection frame matches. The diagram reads:

- pin and roller supports (`ann.support.pin`, `ann.support.roller`) whose pin touches the beam (within 3 mm);
- fixed supports (`ann.support.fixed`) whose wall touches the left or right end of the beam;
- forces (`ann.force`) whose tip touches the beam; slanted forces count with their component across the beam (forces
  pointing away from the beam's lower side count negative) and with their component along the beam (positive towards
  the beam's end), which gives the normal force; a force along the beam counts with the latter only;
- distributed loads (`ann.load.distributed`) whose lower edge stands on the beam, uniform, triangular or trapezoidal as
  drawn (the intensity runs from the box's left to its right edge, so a mirrored load falls the other way; a load
  reaching beyond the beam counts with the part standing on it);
- moments (`ann.moment`) whose centre lies on the beam, as applied couples: the symbol's sense of rotation
  (counter-clockwise by default; mirroring the symbol or the beam reverses it) gives the sign, clockwise as seen being
  positive;
- internal hinges (`ann.beam.hinge`) whose centre lies on the beam (Gerber beams); hinges at the beam ends are ignored.

Positions are shares of the beam length; shares within 0.5 % of a simple fraction (halves to twentieths) are taken as
that fraction, positions within 3 mm of an end as the end.

**Values.** A load label such as "10 kN", "F = 10 kN" or "q = 2.5 kN/m" is a number with a unit; the optional
`value` parameter of a force, distributed load (its peak intensity) or moment ("Magnitude (beam diagrams)") wins over
the label. A moment label such as "6 kN·m" gives the moment unit, from which the force unit of the shear force is taken.
Any other label ("F", "q", "F₁", "M₀") is a symbol. The beam's optional `length` parameter (m) gives the real length.
The normal force is labelled like the shear force: a number with the force unit when all loads along the beam are
numbers ("max N = 7.071 kN"), an exact multiple when they share one symbol, with short multiples of √2 and √3 for the
components of loads inclined at 45°, 30° or 60° ("√2·F/2", "√3·F/2").

| Loads and length | Labels |
|---|---|
| all loads numbers, length given | numbers with units: "max V = 5 kN", "max M = 10 kN·m" |
| point loads as numbers, no length | "max V = 5 kN", "max M = 2.5 kN·L" |
| only couples as numbers, no length | "max M = −6 kN·m"; the shear force only when it is zero ("max V = 0") |
| one symbol of one kind (all point loads "F", all line loads "q" or all couples "M₀") | exact multiples: "F/2", "F·L/4", "q·L/2", "q·L²/8", "−F·L", "M₀/L", "−M₀"; short fractions up to 1/64 (longer ones up to 1/512 only when exact to 10⁻¹⁰, such as "13·F·L/128" in continuous beams), short fractions over √3 ("q·L²/(9√3)", "L/√3"), otherwise three decimals |
| mixed symbols or kinds, symbols together with numbers, line loads as numbers without a length | "max V", "max M" without a value (the curves are drawn with unit values) |

**Without a beam** (or above a beam that has neither supports nor loads), the diagram shows its own load case:
`support` (simply supported (default) / cantilever with the wall left / wall right / Gerber beam with the wall left, a
hinge at the middle and a roller right / propped cantilever with the wall left and a roller right / two-span continuous
beam with equal spans / clamped at both ends), `load` (point load F (default) / uniform load q / both / triangular load q
rising to the right / clockwise moment M₀), `at` (position of F or M₀ as a share, 0.5), and optionally `force` (kN),
`lineLoad` (kN/m, peak), `couple` (kN·m) and `length` (m) for values. Presets: `ann.beam.diagram.cantilever` (end load),
`ann.beam.diagram.uniform`, `ann.beam.diagram.triangle` (max M = q·L²/(9√3) at x = L/√3), `ann.beam.diagram.moment`
(cantilever with an end moment: max M = −M₀), `ann.beam.diagram.gerber` (uniform load: max M = −q·L²/4 at the wall),
`ann.beam.diagram.propped` (propped cantilever, uniform load: max M = −q·L²/8 at the wall, max V = 5·q·L/8),
`ann.beam.diagram.continuous` (two-span beam, uniform load: max M = −q·L²/32 over the middle support, L being the total
length), `ann.beam.diagram.fixed` (clamped at both ends, centre load: max M = −F·L/8) and `ann.beam.diagram.deflection`
(cantilever with an end load and the deflection line: max w = F·L³/(3·EI) at x = L; 60 mm × 66 mm).

**Other parameters:** `momentBelow` (true: positive, sagging moments are drawn below the axis, on the tension side, as
usual in German textbooks; false: above), `textSize` (3.5).

**Deflection line.** `deflection` (false; action **Deflection line** `ann.beamdiagram.deflection`, which also grows the
box by one row, keeping its top edge and the height of the other rows, and shrinks it again when switched off) adds the
row w(x), drawn downwards for a downward deflection. Its shape does not depend on the bending stiffness; the value
does: with one symbol of one kind it is an exact multiple of F·L³/EI, q·L⁴/EI or M·L²/EI ("F·L³/(3·EI)",
"5·q·L⁴/(384·EI)"; denominators up to 2000 when exact, otherwise three significant digits, "0.00542·q·L⁴/EI"). With
numbers for all loads, a beam length and the optional `modulus` E (N/mm²) and `inertia` I (cm⁴) it is given in mm
("max w = 12.698 mm"); forces are taken in kN unless the labels say N or MN, lengths in m. Without E and I the row is
labelled "max w" alone.

**Statics** (`BeamStatics`, pure computation, no host): straight beams under point loads (`BeamPointLoad` with a
transverse component `F` and an optional component `Axial` along the beam; `BeamPointLoad.Inclined(x, F, angle)` with
the angle measured clockwise from the beam axis, 90° pointing down), uniform, triangular and trapezoidal line loads
(`BeamLineLoad` with an intensity at each end) and applied couples (`BeamCouple`, clockwise positive) – on pin, roller
and fixed supports, with internal hinges (`BeamModel.Hinges`). Unknowns are one force per pin or roller and a force and
a clamping couple per fixed support; conditions are the two equilibrium conditions (vertical forces, moments) plus zero
bending moment at each internal hinge. With loads along the beam every pin and fixed support adds a horizontal reaction
and the horizontal equilibrium adds a condition (rollers slide). `BeamStatics.Check` compares them by the rank of the
condition matrix: **determinate** (the reactions follow from the conditions), **indeterminate** of degree n (n more
unknowns than independent conditions, e.g. fixed plus roller: 1, three supports: 1, four supports: 2, clamped at both
ends: 2, two pins with a load along the beam: 1) or **unstable** (a condition cannot be met: no or one hinged support,
two supports at one place, a hinge in a simply supported beam, a load along a beam on rollers only).

Determinate beams are solved from the conditions. **Indeterminate beams** (continuous beams over three or more
supports, propped cantilevers, beams clamped at both ends, also with hinges, and their combinations) are solved by the
displacement method for a prismatic beam: nodes at the ends, supports, loads, ends of line loads and hinges, per node a
deflection and a slope (two slopes at a hinge), Hermite beam elements with the exact equivalent nodal loads of linear
line loads, so the nodal values are exact for the Euler–Bernoulli beam. The reactions of a beam with constant EI do not
depend on the size of EI, so no stiffness has to be entered; the normal force of beams held horizontally at several
supports is solved the same way with constant EA. The diagram then shows the solution and, under the box, the note
"statically indeterminate (degree 1), EI constant" ("EA" for the normal force, "EI, EA" for both). Unstable beams
(mechanisms) are not solved: the diagram shows its axes and "unstable (mechanism)" or "no supports (unstable)" instead
of numbers. The deflection line (`BeamSolution.Deflection`, `Slope`, `MaxDeflection`, `DeflectionCurve`, all for
EI = 1) follows from the nodal values by integrating the bending moment twice (EI·w'' = −M, w downwards) inside each
stretch, for determinate and indeterminate beams alike; its extremes are found where the slope passes zero.

Sign conventions: normal force positive in tension (the forces along the beam left of the section, with opposite
sign), shear force as the sum of the upward forces left of the section, bending moment positive when it sags the beam;
a clockwise couple makes the moment jump up by its value. Inside a stretch the shear force is a polynomial of degree two
at most; the moment's extremes lie where it passes zero (solved exactly; the drawn curve passes through these
vertices). A couple placed exactly on a hinge acts on the part right of it. Unit tests cover the textbook cases (simply
supported beam with centre load: reactions F/2, M_max = F·L/4; uniform load: M_max = q·L²/8; triangular load: reactions
q·L/6 and q·L/3, M_max = q·L²/(9√3) at x = L/√3; trapezoidal load as uniform plus triangle; cantilever with end load:
M = −F·L at the wall; cantilever with an end moment: constant M = −M₀; cantilever with a triangular load: M = −q·L²/6;
couple on a simply supported beam with its jump; Gerber beam with one hinge (wall, hinge, roller) and a two-span Gerber
beam on three supports; off-centre load F·a·b/L; overhang; propped cantilever with uniform load: R_B = 3·q·L/8,
M_A = −q·L²/8, field moment 9·q·L²/128 at 5·L/8; propped cantilever with centre load: R_B = 5·F/16; two-span continuous
beam with uniform load: middle support 5·q·l/4, M_B = −q·l²/8; three-span beam: inner supports 1.1·q·l, M = −0.1·q·l²;
clamped at both ends with centre load: M = ∓F·L/8, w = F·L³/(192·EI), with uniform load: −q·L²/12, q·L²/24,
w = q·L⁴/(384·EI); cantilever deflections F·L³/(3·EI) and q·L⁴/(8·EI); simply supported 5·q·L⁴/(384·EI) and
F·L³/(48·EI); the kink of a Gerber beam's deflection line at its hinge; an inclined load with its normal force; two pins
sharing a load along the beam; the classification; equilibrium checks).

Redrawing: the diagram depends on its neighbours; beams, supports, forces, distributed loads, moments and hinges name
the diagrams on their page as dependents, so moving a load redraws the diagram.

## Other additions

| Type id | Element | Parameters (all optional) |
|---|---|---|
| `ann.cloud` | Revision cloud: a closed chain of outward arcs inside the box, marking a changed area | `arc` (arc length, 5 mm) |
| `ann.dimension.chain` | Chain dimension: several points measured one after another along one dimension line, one value per stretch; points are taken in their order along the measuring direction | `offset` (−8 mm), `axis` (auto / horizontal / vertical), `terminator`, `prefix`, `unit`, `decimals`, `scale`, `textSize` (no common override text) |
| `ann.beam` | gains an optional `length` (m) for beam diagram values and companions (supports, force, distributed load, moment, hinge, diagram) | |
| `ann.force` / `ann.load.distributed` / `ann.moment` | gain an optional `value` (magnitude / peak load per length / moment) for beam diagrams | |
| `ann.load.distributed` | arrows follow the intensity; the "Shape" choice (with pictures) switches between the shapes | `shape` (uniform (default) / triangle rising to the right / triangle falling to the right / trapezoid), `low` (low end of the trapezoid as a share of q, 0.4). Presets `ann.load.distributed.triangle` and `ann.load.distributed.trapezoid` |
| `ann.beam.hinge` | Internal hinge (Gerber hinge): a small open circle centred on the beam axis; beam diagrams read it as a moment-free joint | none (5 mm × 5 mm) |

## Host notes

Everything is drawn with lines, polylines, polygons, rectangles, circles and text, so every host draws native,
editable shapes. The beam diagram's bands use a hatch fill (vertical lines), which hosts draw like other hatched regions.
The tolerance frame and the datum frame have a white fill so they hide what lies behind them. Option pictures of the
choices (`Preview`) are shown by hosts that render previews; others list the option names.

## Open items

- Modifiers in tolerance frames (maximum material, projected zone, free state), composite frames and tolerance
  indications on dimension values are not modelled.
- Weld symbols: no supplementary symbols (flush, convex, concave), no tail with process number, no staggered welds.
- Beam diagrams: line loads at most linear and transverse (no distributed loads along the beam), fixed supports only
  at the beam ends, a constant cross-section for indeterminate beams and the deflection line (no stepped beams, no
  shear deformation, no spring supports or support settlements); the deflection value in mm assumes forces in N, kN or
  MN and lengths in m. Under a beam the diagram's drawing stretches to the beam, but its box changes only with the
  "Fit to the beam" action (the engine has no hook that resizes an object when a neighbour changes).
