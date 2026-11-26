# Math & geometry module (`math`)

Coordinate systems, function graphs, points and vectors, angles, triangles, circles and compass constructions — for
teaching and explanation slides. Every element is drawn with native primitives (lines, polylines, polygons, ellipses,
text), stays editable with the host's own tools and is insertable without parameters. Parameters are optional
enrichment: ranges, coefficients, angles, labels and the construction to show.

The drawings follow common school and textbook practice (axes with arrowheads, italic variables, upright point names,
compass marks, right-angle square with a dot). No standard texts or tables are reproduced and no compliance with a
standard is claimed.

## Relation to other modules

| Topic | Lives in | Note |
|---|---|---|
| Small coordinate-system symbol for free-body diagrams (`ann.coordsys`), angle and radius dimensions (`ann.dimension.angle`, `ann.dimension.radius`) | annotation | technical drawing marks, no ranges or grid |
| Data charts (`chart.xy`, `chart.axes`, bar charts, characteristic curves) | charts | measured or engineering data with units and legends |
| 2D coordinate system and function graphs in math style (`chart.math.axes`, `chart.math.function`) | charts | moved there in charts v2 stage 4, see [below](#moved-to-the-charts-module-charts-v2-stage-4-2026-11-25) |
| Coordinate systems with grid and grid points to dock on, function graphs from a formula family, vectors, angles measured from the drawing, school constructions | **math** | this module (the 2D coordinate system and function graph types remain for existing slides) |

The modules do not depend on each other. Lines of any module can dock on the ports of this one (`AnchorRole.Port`).

## Moved to the charts module (charts v2 stage 4, 2026-11-25)

The 2D coordinate system and the function graphs are offered by the charts module, so all charts share one axis
system (axis breaks, parts, export): `chart.axes.math` (Coordinate system (2D)), `chart.axes.math.quadrant`,
`chart.function` (parabola), `chart.function.linear`, `chart.function.sine`, `chart.function.exponential`, plus a typed
formula (`chart.function.formula`) and a function generator (`chart.function.generator`); see
[charts](charts.md#coordinate-systems-and-function-graphs-charts-v2-stage-4).

- The items `math.axes.cartesian`, `math.axes.cartesian.quadrant`, `math.graph.function`, `math.graph.function.linear`,
  `math.graph.function.sine` and `math.graph.function.exponential` name those items as `LibraryItem.Alternative`:
  while the charts module is registered they are left out of galleries and search (the category "Function graphs" then
  disappears); they are still found by id, and the types `math.axes.cartesian` and `math.graph.function` stay
  registered, so existing slides keep rendering and editing. On its own, the math module still offers everything.
- Points and vectors dock on the chart version as before: the origin is a port, the axes are rails (`axis.x`,
  `axis.y`), every grid point is a snap point (`g<x>_<y>` with the data values, e.g. `g2_3`), function graphs have the
  ports `zero0…`, `ext0…` and `yint`; vector ends also dock anywhere on the chart's surface and follow it (tested in
  `ChartsMergeTests`). The vector suggests the chart coordinate system as companion (the math one without charts).
- Number line, 3D axes and polar grid stay in this module.

## Conventions

- **Quick Sketch first:** every element is insertable without parameters; free (also non-uniform) scaling changes
  `Size`, never parameters, and every drawing stays inside its box (tested at stretched, squashed and tiny sizes with
  every parameter variant).
- **What stretches and what stays true:** coordinate systems, number lines, function graphs and the parallel lines
  stretch with the box (non-uniform scaling changes the units of x and y independently, the ranges stay). Triangles
  stretch too, but a right triangle keeps its right angle (legs along the box edges) and an isosceles triangle stays
  isosceles; the constructions (incircle, circumcircle, medians, altitudes) are computed from the stretched vertices,
  so they are always correct. Circles, regular polygons, the 3D axes, the polar grid, the unit circle, Thales, the
  tangent and the compass constructions keep their shape: they are fitted uniformly into the box and centred.
- **Angles:** parameters that place something on a circle (point of contact, point C, unit-circle angle, polar grid,
  angle of a transversal or of the bisected angle) use the mathematical sense: degrees **counter-clockwise** from the
  positive x axis, as in school mathematics (like the hatch angles of Core). The object's own rotation remains
  clockwise on screen like every other object.
- **Labels:** variables are italic (x, y, a, b, c, α, r, d, t), point names upright (A, B, C, M, P). Text scales with
  the box relative to its design size (smaller factor, 0.5 … 2.5) so non-uniform stretching never distorts it; point
  types (vector, vector addition, angle) have a `textSize` parameter (4 mm ≈ 11 pt). Numbers use the decimal comma in
  German and the minus sign "−". Names that differ by language: circle centre O (EN) / M (DE); incentre I / W,
  circumcentre O / U, centroid G / S, orthocentre H / H; point of contact of a tangent T / B.
- **Line roles (ISO 128 ideas):** figure outlines and vectors use the visible line (0.5 mm), axes 0.35 mm with a
  filled arrowhead, grid lines thin grey (0.15 mm), construction lines thin (0.25 mm), helper lines (vector
  components, projections, parallelogram copies) dashed (hidden line).
- **Colours (module convention):** constructions and function curves blue `#1565c0`, results red `#c62828` (resultant
  of a vector addition, perpendicular and angle bisector, tangent, sine), second vector and tangent segment green
  `#2e7d32`, cosine blue. The document's black-and-white setting and the accent colour apply as usual.
- **Style overrides:** a stroke colour recolours every line, text and point marker of the element; the width applies
  to the main stroke and outlines; dash and arrowheads to the main stroke; a fill applies to the closed outline of a
  triangle, polygon or circle, which is drawn first so the fill never hides constructions.
- **Figures, not parts:** `IsPart` is false for every type (no bill-of-materials entries).

## Object types

Sizes are design sizes (W x H, mm). "points" = point geometry (page coordinates, ends dock like line ends).

| Id | Name (EN / DE) | Size | Parameters |
|---|---|---|---|
| `math.axes.cartesian` | Coordinate system (2D) / Koordinatensystem (2D) | 70x60 | `xMin`/`xMax` (−5…5), `yMin`/`yMax` (−4…4), `xStep`/`yStep` (1), `grid` (true), `numbers` (true), `xPi` (x ticks at multiples of π/2), `xLabel` ("x"), `yLabel` ("y") |
| `math.axes.number` | Number line / Zahlenstrahl | 90x14 | `min`/`max` (−5…5), `step` (1), `minor` (minor ticks between numbers, 0–9), `numbers` (true), `label` (name at the arrow) |
| `math.axes.spatial` | Coordinate system (3D) / Koordinatensystem (3D) | 50x50 | `projection` oblique (cabinet: x at 45°, shortened by ½√2) / isometric, `names` x, y, z / x₁, x₂, x₃, `ticks` (unit ticks, 4), `negative` (negative half-axes, true), `numbers` ("1" at the first tick) |
| `math.axes.polar` | Polar grid / Polarkoordinatennetz | 64x64 | `rings` (4, 1–10), `step` 15/30/45/90°, `unit` degrees / radians (π/6, π/3 …), `angleLabels`, `radiusLabels` |
| `math.graph.function` | Function graph / Funktionsgraph | 70x60 | `function` linear / quadratic / cubic / sine / cosine / exponential (eˣ) / logarithm (ln x) / sqrt / abs / reciprocal; coefficients of y = a·f(b·(x − c)) + d: `a`, `b`, `c`, `d`; `formula` (true); all axis parameters (default x −4…4, y −2…8) |
| `math.point.labelled` | Point with label / Punkt mit Beschriftung | 14x10 | `label` ("A"), `marker` dot / cross / circle, `position` of the label (8 directions, top right) |
| `math.vector.arrow` | Vector / Vektor | points (2) | `label` ("v"), `notation` arrow above / bold / plain, `components` (dashed x and y components with v<sub>x</sub>, v<sub>y</sub>), `tailLabel`, `tipLabel`, `points` (dots at both ends), `textSize` |
| `math.vector.sum` | Vector addition / Vektoraddition | points (3) | `construction` parallelogram (points O, tip of a, tip of b) / head to tail (O, tip of a, tip of b drawn from a), `labelA` ("a"), `labelB` ("b"), `notation`, `result` (resultant a + b, true), `textSize` |
| `math.angle.mark` | Angle / Winkel | points (3) | point on the first ray, vertex, point on the second ray; `label` ("α"), `mark` automatic (square at 90°) / arc / double arc / right-angle square, `value` (measured value, e.g. "α = 45°"), `rays` (true), `reflex` (other side), `radius` (7 mm), `textSize` |
| `math.shape.triangle` | Triangle / Dreieck | 50x40 | `kind` general / right-angled (γ = 90° at C) / isosceles / equilateral / true to the entered values, values `a`, `b`, `c`, `alpha`, `beta`, `gamma`, `area` (see [round 7](#triangle-values-live-constructions-and-conic-sections-2026-11-26-round-7)), `showValues`, `construction` none / incircle (angle bisectors) / circumcircle (perpendicular bisectors) / medians and centroid / altitudes and orthocentre, `vertexLabels`, `sideLabels`, `angleMarks` (α, β, γ) |
| `math.shape.polygon` | Regular polygon / Regelmäßiges Vieleck | 40x40 | `sides` (6, 3–12; flat bottom edge), `circumcircle`, `incircle`, `centre`, `labels` (A, B, C …) |
| `math.circle.basic` | Circle with radius / Kreis mit Radius | 40x40 | `show` radius / diameter / both / circle only, `angle` (direction of the radius, 45°; the diameter of "both" is horizontal), `centre`, `labels` |
| `math.circle.tangent` | Tangent to a circle / Tangente an einen Kreis | 56x44 | `angle` (position of the point of contact, 60°), `radius` (radius and right angle), `labels` |
| `math.circle.thales` | Thales circle / Thaleskreis | 60x40 | `angle` (position of C, 115°, 1–179), `full` (full circle instead of the semicircle), `centre`, `labels` |
| `math.circle.unit` | Unit circle / Einheitskreis | 64x64 | `angle` (40°), `label` ("α"), `tan` (tangent segment on x = 1), `values` (cos α = 0.77 …) |
| `math.construct.perpendicular` | Perpendicular bisector / Mittelsenkrechte | 50x40 | `arcs` (compass marks), `labels` (A, B, M, m) |
| `math.construct.anglebisector` | Angle bisector / Winkelhalbierende | 56x40 | `angle` (70°, 10–170), `arcs` (compass marks), `labels` (S, w) |
| `math.lines.parallel` | Parallel lines and transversal / Parallelen mit Querlinie | 64x44 | `angle` of the transversal (60°, 20–160), `pair` corresponding / alternate / co-interior / all / none, `values` (degrees instead of α, β), `labels` (g, h) |

**Function graph:** there is deliberately no free expression parser. The ten basic functions with the four
coefficients cover the usual school examples (vertex form of a parabola, stretched and shifted sine, growth). The
curve is sampled (800 points over the range, simplified), clipped exactly at the plot range (bisection), cut at the
pole of 1/x and the domain edge of ln and √ (no jump line across a pole). The formula is written in school notation
and simplified ("f(x) = 2(x − 1)² − 3", "f(x) = 0,5x + 1", "f(x) = e⁻ˣ", "f(x) = 2·sin(3x) + 1"; exponents that cannot
be written as superscripts fall back to "exp(…)") and placed in the corner of the plot where it covers neither the
curve nor an axis; tick numbers move to the other side of their axis where the curve passes through them.

**Ticks:** numbers are labelled at every n-th tick so they never overlap; the crossing of the axes is labelled once
with "0" when it is the origin. Ranges with more than 100 ticks double the step. `xPi` labels the x axis in
multiples of π/2 (π for wide ranges): "π/2", "π", "3π/2", "−π".

## Solids, sectors, Pythagoras, sets and probability (2026-11-26)

| Id | Name (EN / DE) | Size | Parameters |
|---|---|---|---|
| `math.solid.cube` | Cube (cuboid) / Würfel (Quader) | 44x44 | `projection` cabinet (depth ½, 45°) / cavalier (depth 1, 45°), `hidden` (hidden edges dashed, true), `labels` (a, h) |
| `math.solid.cylinder` | Cylinder / Zylinder | 36x44 | as the cube; labels r, h |
| `math.solid.cone` | Cone / Kegel | 36x42 | as the cube; labels r, h (radius and height dashed inside) |
| `math.solid.sphere` | Sphere / Kugel | 40x40 | as the cube; label r |
| `math.solid.pyramid` | Square pyramid / Quadratische Pyramide | 40x40 | as the cube; labels a, h |
| `math.arc.sector` | Circle sector / Kreissektor (Kreisausschnitt) | 40x40 | `kind` sector / segment, `angle` (central angle, 70°, 1–359), `start` (direction of the first radius, 20°), `angleLabel` value (70°) / letter (α) / none, `circle` (rest of the circle), `fill`, `labels` (r) |
| `math.shape.pythagoras` | Pythagoras (squares on the sides) / Satz des Pythagoras (Quadrate) | 50x54 | legs `a` (3) and `b` (4), `areas` a², b², c² / values (9, 16, 25) / none, `labels` (a, b, c), `grid` (unit squares for whole-number sides), `fill` |
| `math.venn.diagram` | Venn diagram / Venn-Diagramm (Mengendiagramm) | 60x44 | `sets` (2–3), `labelA`/`labelB`/`labelC`, `universe` (frame), `universeLabel` (empty: U in English, G in German), `fill` (translucent tints) |
| `math.tree.probability` | Probability tree / Baumdiagramm (Wahrscheinlichkeit) | 84x54 | `levels` (stages, 1–3), `branches` (2–3 per node), `e1`…`e3` (outcomes per stage, "A; Ā"), `p1`…`p3` (probabilities per stage), `products` (path probabilities) |

**Solids (oblique view, German "Schrägbild"):** the front plane is drawn in true shape, depth recedes at 45° to the
upper right, shortened by q = ½ (cabinet projection, default) or q = 1 (cavalier projection). Polyhedra (cube,
pyramid) decide visibility per face: a face is visible when its outward normal points towards the viewer of the
projection, an edge when one of its faces is; hidden edges are drawn with `LineRole.Hidden` (dashed). Cylinder and
cone use the exact projection of the base circle (a slightly tilted ellipse), the cylinder's silhouettes touch it at
its widest points and the cone's sides are the tangents from the apex; the back part of the base is hidden. The sphere
is drawn the conventional way (outline circle, equator as an ellipse whose depth follows q, back half dashed): its exact
oblique outline would be a skewed ellipse. The solid fills its box: a square box gives a cube; free (also non-uniform)
scaling makes a cuboid with a square base, a taller or flatter cylinder, cone or pyramid (the sphere stays round).
Anchors: polyhedron vertices as ports `v0…`, apex (`AnchorRole.Apex`), centres of top and bottom faces.
Context actions: projection (choice), hidden edges, labels.

**Sector and segment:** the sector is bounded by two radii and the arc (closed outline from the centre), the segment by
the chord and the arc (radii as thin construction lines). The central angle is marked by an arc and labelled with its
value (decimal comma in German) or α. Grips: the first radius turns the figure (`start`), the second sets the angle.
Context actions: sector or segment, "Central angle…", angle label, rest of the circle, tint, radius label.

**Pythagoras:** right angle at C, hypotenuse c horizontal at the bottom with its square below, the squares on the legs
outward; side letters inside the squares along the triangle's sides. The figure keeps its shape (uniform fit).
Context actions: square labels, side labels, unit squares, tint.

**Venn diagram:** circles of equal radius with centre distance = radius (two sets side by side, three sets in a
triangle), names in the exclusive parts, tints drawn as separate translucent circles below the outlines, the
universal set as the frame of the box. Context actions: two/three sets, "Set names…" ("A; B; C"), universal set, tint.

**Probability tree:** leaves are spaced evenly, every node is centred over its children; outcomes stand at the
nodes, probabilities on the branches (above rising branches, below falling ones; the middle one of three branches in
the wedge above it), path probabilities ("= 0.18", product rule) after the leaves. Probabilities are typed per stage
separated by ";" ("0.3; 0.7", "0,3", "30 %", "1/6"); several nodes of one stage are separated by "|" ("0.6 | 0.2"), the
last group applies to the remaining nodes. Missing values share what is left of 1 (one missing value is the
complement); fractions stay exact ("1/3", product "1/12"), percentages stay percentages, an impossible remainder
shows "?". Text that is no probability is ignored. Context actions: more/fewer stages, two/three branches,
"Probabilities of stage n…", "Outcomes of stage n…", path probabilities.

## Triangle values, live constructions and conic sections (2026-11-26, round 7)

| Id | Name (EN / DE) | Size | Parameters |
|---|---|---|---|
| `math.free.triangle` | Triangle through three points / Dreieck durch drei Punkte | points (3) | `construction` (as the triangle), `vertexLabels`, `sideLabels`, `angleMarks`, `values` (measured angles "α = 63.4°"), `textSize` |
| `math.free.polygon` | Polygon with area and centroid / Vieleck mit Fläche und Schwerpunkt | points (3 or more, corners can be inserted) | `area` (label "A = 12 cm²"), `centroid` (S / G), `vertexLabels` (A, B, C …), `scale` (1 : n, default 1:1), `textSize`, `showValues` |
| `math.free.bisector` | Perpendicular bisector of two points / Mittelsenkrechte zu zwei Punkten | points (2) | `segment` (AB), `arcs` (compass marks), `labels` (A, B, M, m), `textSize` |
| `math.free.anglebisector` | Angle bisector of three points / Winkelhalbierende zu drei Punkten | points (3: on the first ray, vertex, on the second ray) | `arcs`, `values` (measured half angles), `labels` (S, w), `textSize` |
| `math.free.tangents` | Tangents from a point to a circle / Tangenten von einem Punkt an einen Kreis | points (3: centre M, a point on the circle, P) | `thales` (Thales circle over MP), `radii` (radii and right angles), `labels` (M/O, P, T₁, T₂ / B₁, B₂), `textSize` |
| `math.conic.section` | Conic section (foci) / Kegelschnitt (Brennpunkte) | 60x40 | `kind` ellipse / hyperbola / parabola, `point` (P with its distances), `position` of P (50°), `formula` (defining property), `axes` (and asymptotes), `labels` |

**Triangle values** (`math.shape.triangle`, Calc values, `IValueType`): sides `a`, `b`, `c` (cm), angles `alpha`, `beta`,
`gamma` (°), area `A` (cm², parameter `area`) and perimeter `u`, all optional text parameters as typed ("5 cm", "60°").
Relations: the angle sum, the law of cosines three times (`math.triangle.cosine.a/b/c`: a side from two sides and the
included angle, an angle from three sides), the law of sines three times (`math.triangle.sine.*`: a side from a side and
two angles, an angle from two sides and an opposite angle, the acute solution of the ambiguous case), the area
A = ½·a·b·sin γ and the perimeter. Any three independent values with at least one side (SSS, SAS, SSA, ASA, AAS) give the
rest; contradicting values are reported as a conflict. Entered sides appear in the side labels ("a = 5 cm"), entered
angles are marked with their value ("γ = 60°", also without the angle marks), an entered area stands inside the triangle
(without a construction). The new kind **"True to the entered values"** (`values`) draws the shape the entered values
give (two or three angles alone give the shape; values that give no triangle fall back to the general shape), fitted
uniformly into the box, so free scaling keeps it. Item `math.shape.triangle.values`: a = 5 cm, b = 4 cm, γ = 60° (SAS).

**Live constructions on points:** the free figures are point objects like the vector and the angle. Their points dock
on ports, grid points and surfaces of other figures and follow them (`Attachments.Refresh`): a perpendicular bisector
whose ends dock on the vertices A and B of a triangle stays the bisector of AB when the triangle is moved, turned or
stretched (tested). The triangle through three points draws the same constructions as the box triangle (incircle with
the angle bisectors, circumcircle with the perpendicular bisectors, medians and centroid, altitudes and orthocentre;
shared code `Tri.Construct`) from its vertices, so dragging a vertex updates them; items
`math.free.triangle.circles` (circumcircle) and `math.free.triangle.incircle`. The tangents from P are constructed
with the Thales circle over MP: T₁, T₂ (German B₁, B₂) are its intersections with the circle; for P inside the circle
only the circle and P are drawn. The polygon measures its area with the shoelace formula (a self-intersecting outline
counts its loops with sign) and its area centroid; values `A` and `u` are read-only (from the drawing at the scale
`scale`).

**Conic sections:** the curve fills the box, so free (also non-uniform) scaling changes the semi-axes and the foci
follow. Ellipse: a and b are the half box sides, c² = a² − b² (foci on the longer axis), native ellipse. Hyperbola:
a = 0.4 of the half width, b chosen so that both branches stay in the box, c² = a² + b², asymptotes dashed. Parabola:
directrix at the bottom, vertex S (EN V) and focus F at the distance p, the curve reaching the top corners. The point P
(`position`: the parameter angle, for hyperbola and parabola its sine places P along the branch) shows the distances
of the defining property: |PF₁| + |PF₂| = 2a, ||PF₁| − |PF₂|| = 2a, |PF| = |PL| (L foot on the directrix). Ports:
`centre`, `F1`, `F2` (`F`, `vertex` for the parabola), `P`; vertices `V1`, `V2` (quadrant points); rails along the
axis and the directrix.

## Library

56 items: `math.axes.cartesian`, `math.axes.cartesian.quadrant` (first quadrant 0…6 x 0…5), `math.axes.number`,
`math.axes.spatial`, `math.axes.spatial.isometric`, `math.axes.polar`; graphs `math.graph.function` (parabola y = x²),
`math.graph.function.linear` (y = 0.5x + 1), `math.graph.function.sine` (−2π…2π, π ticks, 90x45),
`math.graph.function.exponential`; `math.point.labelled`, `math.vector.arrow`, `math.vector.arrow.position` (position
vector from O to P with dots), `math.vector.arrow.components`, `math.vector.sum` (parallelogram),
`math.vector.sum.tail` (head to tail); angles `math.angle.mark`, `math.angle.mark.right` (90°, square),
`math.angle.mark.value` ("α = 45°"), `math.angle.mark.arc` (arc only, for marking an existing figure); figures
`math.shape.triangle`, `math.shape.triangle.right` (with angle marks), `math.shape.triangle.incircle`,
`math.shape.triangle.circumcircle`, `math.shape.polygon` (hexagon), `math.circle.basic`, `math.circle.tangent`,
`math.circle.thales`, `math.circle.unit`, `math.arc.sector`, `math.arc.sector.segment` (110°), `math.shape.pythagoras`;
constructions `math.construct.perpendicular`, `math.construct.anglebisector`, `math.lines.parallel`; solids
`math.solid.cube`, `math.solid.cuboid` (56x36), `math.solid.cylinder`, `math.solid.cone`, `math.solid.sphere`,
`math.solid.pyramid`; sets and probability `math.venn.diagram`, `math.venn.diagram.three`, `math.tree.probability`,
`math.tree.probability.dice` (one stage, outcomes 1, 2, 3 with 1/6, 1/3, 1/2); round 7: `math.shape.triangle.values`,
`math.free.triangle`, `math.free.triangle.circles`, `math.free.triangle.incircle`, `math.free.polygon`,
`math.free.bisector`, `math.free.anglebisector`, `math.free.tangents`, `math.conic.section` (ellipse),
`math.conic.section.hyperbola`, `math.conic.section.parabola` (50x44).

Categories: `math.axes` Coordinate systems / Koordinatensysteme, `math.graphs` Function graphs / Funktionsgraphen,
`math.vectors` Points & vectors / Punkte & Vektoren, `math.angles` Angles / Winkel, `math.figures` Figures & circles /
Figuren & Kreise, `math.constructions` Constructions / Konstruktionen, `math.solids` Solids / Körper,
`math.stochastics` Sets & probability / Mengen & Wahrscheinlichkeit.

Search keywords cover both languages, e.g. Koordinatensystem, Zahlenstrahl, Schrägbild, Parabel, Sinus, Ortsvektor,
Kräfteparallelogramm, rechter Winkel, Dreieck, Inkreis, Umkreis, Höhe, Schwerpunkt, Thaleskreis, Einheitskreis,
Tangente, Sechseck, Mittelsenkrechte, Winkelhalbierende, Stufenwinkel, Wechselwinkel, Würfel, Quader, Zylinder,
Kegel, Kugel, Pyramide, Kreisausschnitt, Kreisabschnitt, Satz des Pythagoras, Mengendiagramm, Baumdiagramm,
Sinussatz, Kosinussatz, Dreiecksberechnung, Tangenten, Kegelschnitt, Ellipse, Hyperbel, Brennpunkt, Leitlinie.

## Anchors

- **Ports** (`AnchorRole.Port`, stable ids, a docked line end follows them through moves, free scaling and parameter
  changes via `Attachments.Refresh`): `origin` of the 2D and 3D axes, `pole` of the polar grid, `zero` of the number
  line, `pt` of a point, `A`/`B`/`C` and `centre` (the construction's centre) of a triangle, `v0…` and `centre` of a
  polygon, `centre` of a circle, `centre`/`contact` of the tangent, `A`/`B`/`C`/`centre` of Thales, `origin`/`P` of the
  unit circle, `A`/`B`/`M`/`S1`/`S2` of the perpendicular bisector, `vertex`/`P1`/`P2`/`Q` of the angle bisector,
  `s1`/`s2` (intersections) of the parallel lines, `sum` (tip of the resultant) of the vector addition and `vertex` of
  the angle. Function graphs add their key points: `zero0…` (zeros), `ext0…` (extrema) and `yint` (y-intercept).
- **Grid points** (`AnchorRole.Mid`): every tick crossing of a 2D coordinate system (`g<i>_<j>` = the point
  (i·xStep | j·yStep), e.g. `g3_-2`; up to 900 points), every tick of the number line (`t<i>`), the unit ticks of the 3D
  axes (`x_1`, `y_2` … or `x1_1` …), ring/ray crossings of the polar grid (`r<ring>a<degrees>`), side midpoints and
  polygon edge midpoints, circle quadrant points (`AnchorRole.Quadrant`). A vector drawn on a coordinate system docks
  its ends on grid points and stays on them when the coordinate system is moved or stretched (tested).
- **Rails** (`AnchorRole.Rail`): the axes (`xaxis`, `yaxis`, `axis-x` …), the number line, the parallel lines `g`, `h`
  and the transversal — line ends dock anywhere along them.
- Point types (vector, vector addition, angle) dock on surfaces (`DocksOnSurface`): an end dropped on a figure
  follows it.

## Context actions

Coordinate systems: grid, tick numbers, x range… and y range… (accept `-5;5`, `−2,5..7,5`, `0 10`, `0 bis 10`; a comma
is a decimal comma). Number line: numbers, range…, finer/coarser minor ticks. 3D axes: projection ▾, axis names ▾,
negative half-axes. Polar grid: more/fewer rings, angle step ▾, angle unit ▾. Function graph: function ▾,
coefficients a; b; c; d… (e.g. `2; 1; 1; -3`), show formula, plus the axis actions. Point: label…, label position ▾,
marker ▾. Vector: name…, notation ▾, components, mark the points, reverse direction. Vector addition:
construction ▾ parallelogram / head to tail (keeps a, b and the resultant: the third point moves), resultant, notation ▾. Angle:
set angle… (turns the second ray about the vertex; values above 180° give the reflex angle), show value, mark ▾,
draw the rays, other side, name…. Triangle: kind ▾, construction ▾, vertex/side labels, angle marks. Polygon:
more/fewer sides, circumcircle, incircle, vertex labels. Circle: line shown ▾ (radius/diameter), centre, labels. Tangent: point of
contact at angle…, move the point of contact (+30°), radius and right angle. Thales: position of C…, move C along the
circle, full circle. Unit circle: angle…, turn by 15°, tangent segment, show values. Constructions: compass marks,
labels, angle…; parallel lines: angle pair ▾, angle…, show values. Round 7: triangle through points: construction ▾,
vertex/side labels, angle marks, measured angles; polygon: area, centroid, vertex labels; bisectors and tangents:
compass marks / measured half angles / Thales circle / radii, labels; conic section: curve ▾, position of P…, point P,
defining property, axes, labels. The triangle's kind ▾ offers "true to the entered values".

## Grips (drag handles)

`DragHandles` / `MoveHandle` (hosts draw them as blue grips; interaction-ux.md section 4). Angles are mathematical
degrees (counter-clockwise from +x); every angle grip moves freely and turns its point about the centre towards the
pointer, snapped to 1° and kept within half a turn of the old value:

| Figure | Key | Grip on | Parameter | Range |
|---|---|---|---|---|
| Circle with radius | `angle` | end of the radius (of the diameter when only that is shown; none for "circle only") | `angle` | −360° … 360° |
| Tangent to a circle | `angle` | point of contact | `angle` | −360° … 360° |
| Thales circle | `angle` | C | `angle` | 1° … 179° |
| Unit circle | `angle` | P | `angle` | −360° … 360° |
| Angle bisector | `angle` | end of the second ray | `angle` | 10° … 170° |
| Parallel lines and transversal | `angle` | upper end of the transversal | `angle` | 20° … 160° |
| Angle (point object) | `radius` | the arc at 0.3 of the sweep, or the outer corner of the right-angle square; moves along that ray | `radius` | 0.5 mm steps, 1 … 100 mm |

Grips of box figures are placed where the figure draws the point, including the shrink of extreme box sizes
(`Fig.Fit`). Labels name the value ("Point of contact: 60°" / "Berührpunkt: 60°", "Arc radius: 7 mm" /
"Bogenradius: 7 mm"). Sizes of the regular polygon, circles and axes are the box size (free scaling), not parameters,
so they have no grip.

## Host notes and limits

- All output uses native primitives (lines with arrowheads, polylines, closed polygons, ellipses, text); arcs are
  polylines (`Prim.Arc`), so every host draws everything natively — no vector picture is needed.
- Text uses Unicode characters the default Office and LibreOffice fonts contain (α β γ φ π √ ² ³ − ·, subscripts
  ₁ ₂ ₃). Exponents of eˣ use superscript characters including the modifier letter ˣ (U+02E3) and ⁽ ⁾; fonts without
  them fall back through the host's font linking. Vector arrows over letters are separate short arrow lines, and
  indices (v<sub>x</sub>) are separate smaller text boxes, both positioned from an estimated glyph width; with very
  wide or narrow fonts they may sit slightly off.
- Labels of rotated box elements rotate with the element (no upright correction).
- The function graph has no free expression parser (by design); `chart.xy` of the charts module plots data.
- The right-angle square drawn with `mark = right` follows the actual rays: on an angle that is not 90° it becomes a
  rhombus. `auto` (default) draws the square only at 90° (±0.05°).
- Very small boxes: if labels and drawing cannot fit, the whole figure (including text sizes and anchors) shrinks
  uniformly into the box.

## Tests and gallery

`tests/InventaSketch.Modules.Geometry.Tests` (207 tests): registration, id conventions, bilingual names, parameters,
options and actions; insertion without parameters; library items resolve to valid parameters; every library item
inside its box in both languages, stretched, squashed and with every parameter variant; every parameter changes the
drawing; free scaling changes `Size` and keeps parameters; anchors inside the box with unique ids; ports at the key
points; style and fill overrides; every context action runs; number formatting, range parsing, π ticks and formulas;
grid-point anchors, first quadrant, dense ticks without overlapping numbers, clipping of the parabola at the range,
zeros/extrema/y-intercept anchors, the two branches of the hyperbola, sine with π ticks, ln starting at the bottom
edge; coefficient and range inputs; 3D axes keep their angles when stretched; polar labels in degrees and radians;
vector components and notation, position vector, vector addition keeping the resultant when the construction
changes; measured angle values, automatic right-angle square, set angle; right and isosceles triangles under
stretching, equilateral triangle, incircle tangent to all sides, circumcircle through the vertices, orthocentre on all
altitudes (also obtuse), regular polygons stay regular, unit circle sine/cosine lengths, Thales right angle, tangent
perpendicular to the radius, round circles, perpendicular and angle bisector geometry, parallel-line angle marks; a
composed slide where a vector addition and an angle docked on grid points follow the coordinate system when it is
moved and stretched. `NewFigureTests`: visible and hidden edges of cube and pyramid, the depth factor at 45° of both
projections, cylinder silhouettes, cone sides tangent to the base, round sphere, sector/segment outline, angle label
and grip, Pythagoras areas (a² + b² = c²) and unit squares, Venn layout and set-name input, probability tree
complements, products, fractions, percentages and layout, probability parsing and the JSON round trip.
`ValueAndLiveFigureTests` (round 7): triangle values from SSS, SAS, ASA and SSA (law of sines and cosines, area,
perimeter), conflicts, the triangle true to its values (shape, labels, angles alone, invalid values), the triangle
through points with its incircle and measured angles, a perpendicular bisector docked on a triangle following it, the
angle bisector, tangents at right angles (and none from inside), polygon area, perimeter, scale and centroid, the foci
and defining properties of ellipse, hyperbola and parabola.

The gallery test writes `gallery/*.html` into the test output folder (one page per category: every library item in
English and German, stretched and squashed, plus the choices of the main parameters) and `gallery/index.html` with a
composed teaching slide (coordinate system, docked vector addition, labelled point, angle φ) in both languages.
