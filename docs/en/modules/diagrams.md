# Diagrams module (`diagrams`, type ids `diag.*`)

> German: [../../de/modules/diagrams.md](../../de/modules/diagrams.md)

UML, entity-relationship (ER), system/architecture and BPMN-lite diagrams for quick technical sketches. Every
symbol is insertable without parameters; free scaling changes only the box size. Simple flowchart starters
(process box, decision, terminator, elbow connector) stay in the annotation module (`ann.flow.*`,
`ann.arrow.elbow`) and are not duplicated here.

## Concepts

- **Box symbols** render in local coordinates and expose **Port anchors** at the edge midpoints and quarter points
  (12 per rectangle). Ellipses expose 8 ports on the outline, diamonds their vertices and edge midpoints, blocks
  their port squares, activation bars a port every 2.5 mm per side. A lifeline tail is a `Rail` (message ends dock anywhere on it and stay attached) and an `Axis` that centres activation bars (their `Hub`).
- **Connectors** are point objects (relationships, crow's-foot connectors, sequence messages). Their ends snap to
  ports and are stored as attachments, so they follow moved, rotated and stretched boxes. With two points a
  connector is straight or, with `route = orthogonal`, routed with right angles (ends attached to a box edge leave it
  perpendicular). With more points, the points are waypoints.
- **Multi-line content** (attributes, operations, literals, lanes, activities) is entered in text parameters, one
  line per row. Each line renders as its own text primitive.
- **Text fitting:** text uses the `textSize` parameter (default 3.5 mm ≈ 10 pt). If the content does not fit the
  box, the text is reduced automatically (not below 1.8 mm); the parameter itself never changes.
- **End notations** are Core arrowheads on the connector line's style (`ArrowStart`/`ArrowEnd`, size `large`):
  open arrow (`open`), filled triangle (`triangle`), hollow triangle (`triangleOpen`), filled and hollow diamond
  (`diamond`, `diamondOpen`) and the crow's-foot ends `crowOne`, `crowOneOnly`, `crowZeroOne`, `crowMany`,
  `crowOneMany`, `crowZeroMany`. User arrowhead overrides (start, end, size) therefore replace them, and every host
  draws them: natively where it has a matching line end, otherwise as explicit shapes (see the
  [capability matrix](../hosts/capability-matrix.md)). Hollow heads are unfilled outlines and the line stops at their
  base; a crow's-foot circle interrupts the line. Both look right on any background and in monochrome output. A user
  stroke colour recolours lines, heads and texts; width and dash overrides apply to the outline or connector line
  only (the head grows with the line width).
- **Underlines** (static members, key attributes) use the native font underline of the text, not drawn lines, so they
  match the real text width in every host.

## Object types

| Category | Types |
|---|---|
| UML class & structure | `diag.uml.class`, `diag.uml.interface`, `diag.uml.enum` (views `full`/`compact`), `diag.uml.package`, `diag.uml.component`, `diag.uml.node`, `diag.uml.note` |
| UML use case | `diag.uml.actor`, `diag.uml.usecase` |
| UML activity | `diag.activity.action`, `.initial`, `.final`, `.flowfinal`, `.decision`, `.bar` (fork/join), `.swimlane` |
| UML state machine | `diag.state.state` (entry/do/exit), `diag.state.composite` (regions), `diag.state.pseudo` (shallow/deep history, junction, choice, entry/exit point, terminate); initial/final states are presets of the activity nodes |
| UML sequence | `diag.seq.lifeline` (box or actor head, dashed tail, optional destruction X), `diag.seq.activation`, `diag.seq.message` (sync, async, reply, create; self message when both ends lie on one vertical) |
| UML relationships | `diag.uml.relation`: association, directed association, aggregation, composition, generalization, realization, dependency, link; multiplicities and role names at both ends, label in the middle |
| ER | `diag.er.entity` (views `diagram`/`table`/`chen`, weak entity), `diag.er.attribute` (key, multivalued, derived), `diag.er.relationship` (Chen diamond, identifying), `diag.er.connector` (crow's foot) |
| System & architecture | `diag.sys.block` (ports per side), `diag.sys.container` (group frame with title tab or band), `diag.sys.cloud`, `diag.sys.database`, `diag.sys.queue`, `diag.sys.server`, `diag.sys.client` (browser window), `diag.sys.user`, `diag.sys.lollipop` (provided ball / required socket) |
| BPMN-lite | `diag.bpmn.start`, `diag.bpmn.intermediate`, `diag.bpmn.end`, `diag.bpmn.task` (sub-process marker), `diag.bpmn.gateway` (exclusive, parallel, inclusive, event-based), `diag.bpmn.pool` (pool with lanes), event triggers, `diag.bpmn.dataobject`, `diag.bpmn.datastore`, `diag.bpmn.flow` (sequence, default, conditional, message, association, data association) |
| Control loops | `diag.ctrl.sum`, `diag.ctrl.tf`, `diag.ctrl.branch`, `diag.ctrl.mult`, `diag.ctrl.signal`, `diag.ctrl.loop` (explodable) |
| Computer systems & communication | system block presets by role, `diag.sys.bus`, `diag.sys.phone`, `.basestation`, `.router`, `.switch`, `.satellite`, `.iot`, `diag.sys.wireless` |
| Data layouts | `diag.data.fields` (row, wrapped rows, memory map) |
| GUI wireframes | `diag.ui.screen`, `.button`, `.field`, `.list`, `.checkbox`, `.toggle`, `.slider`, `.image`, `.icon`, `.tabs`, `.touch`, `.cursor` |
| Fault tree analysis (FTA) | `diag.fta.event.top`, `.intermediate` (rectangles), `diag.fta.gate.and`, `.or`, `.vote` (k-out-of-n), `.inhibit`, `diag.fta.event.basic` (circle), `.undeveloped` (diamond), `.house`, `.conditioning` (ellipse), `diag.fta.transfer` (triangle), `diag.fta.link` |
| Sequential function chart (SFC, GRAFCET) | `diag.sfc.step.initial` (double frame), `diag.sfc.step`, `diag.sfc.transition`, `diag.sfc.action` (optional qualifier cell), `diag.sfc.branch` (parallel double line / alternative single line), `diag.sfc.link` |
| Ishikawa & mind map | `diag.ishikawa` (fishbone), `diag.mindmap.node` (rounded, ellipse, box, underlined), `diag.mindmap.link` (curved branch) |
| Network diagram (critical path) | `diag.net.node` (activity on node with ES, D, EF / number, name / LS, TF, LF), `diag.net.link` (finish-to-start dependency) |

## Text conventions

- **Class members:** one member per line, visibility written as part of the line (`+ - # ~`). A leading `_` marks a
  static member, which is shown underlined without the underscore.
- **Entity attributes:** `*` primary key (underlined: the whole row in the diagram view, the name cell in the table
  view, the attribute ellipse text in the Chen view), `#` foreign key (`(FK)` in the diagram view, `FK` column in
  the table view); both can be combined (`*#order_id`). A type follows a colon: `id: int`.
- **Stereotypes** are written without guillemets; they are added automatically (`interface` → `«interface»`).
- **Relationship direction:** the decoration sits at the end point, i.e. draw from the part/child/client to the
  whole/parent/supplier.

## Automatic layout (2026-11-25)

Select the connected elements of a diagram (the connectors need not be selected): the multi-selection offers "Lay out
as a diagram, top to bottom" and "… left to right" (engine `arrange` with `layout:down` / `layout:right`, Core
`DiagramLayout`). A layered layout in the manner of Sugiyama: every line whose first and last docked ends sit on two
selected elements is an edge; cycles are broken along a depth-first search, layers follow the longest path from the
sources, barycentre sweeps reduce crossings, and layers and gaps follow the elements' own sizes. The diagram keeps its
place (top edge and centre); connectors follow their elements and dock on the sides a drawn flowchart uses
(2026-11-25): bottom → top for a step straight below, out of the side and into the top for a branch below and beside it,
both ends on the right around the column when other elements stand in between (a "yes" exit to a later step), both
ends on the left for a loop back to an earlier step; such side runs keep clear of every element beside them (the
elbow connector's `clearance` is set). An end on a port moves to the nearest port of that side. A long chain laid out left to right can run past the
page edge. Works for any module's box elements joined by docked
lines (process steps, flowchart, UML, BPMN, ER, blocks).

## Method flowcharts (annotation module, 2026-11-25)

The flowchart symbols of the annotation module (`ann.flow.*`) fit their text: words wrap onto lines inside the symbol
(the inscribed area of diamond and circle), the size goes down until it fits, and only below 60 % of the size are long
words split with a hyphen. "Start (circle)" (`ann.flow.start`) and "End (double circle)" (`ann.flow.end`) complete the
set. The elbow connector routes by the sides its ends are docked on: exits on the same side run around with a clearance
("C" shape), facing sides meet in the middle, perpendicular sides take one corner when it lies ahead of both. It carries
a label at its start; the library has "Connector “yes”" and "Connector “no”" (`ann.arrow.elbow.yes` / `.no`). The frame
of labels and notes takes a dashed or recoloured style (dashed note boxes). Checked against the maintainer's flowchart
(engine test `TheMaintainersFlowchartIsLaidOutWithoutLinesThroughBoxes`: after the layout no connector runs through a
box). Open: obstacle-aware routing for hand-placed boxes (only the layout computes clearances).

Further symbols (ISO 5807 conventions, drawn independently): predefined process / sub-process (`ann.flow.predefined`,
two inner side lines; the text fits between them), off-page connector (`ann.flow.offpage`, pentagon pointing down,
uniform, letter "A"), loop limit (`ann.flow.loop`, top corners cut; `ann.flow.loop.end` or parameter `edge = end`:
bottom corners cut), preparation (`ann.flow.preparation`, hexagon), manual operation (`ann.flow.manual`, trapezoid),
stored data (`ann.flow.database`, cylinder) and delay (`ann.flow.delay`, "D" shape). Every symbol fits its text into its
own inner area (below the cylinder cap, above the pentagon tip, left of the delay arc) and has the four edge ports.
Every `ann.flow.*` symbol has the parameter `optional`: an optional step is outlined dashed (action "Optional step
(dashed)", library item `ann.flow.process.optional`).

Parallel mode (`ann.flow.parallel`, fork / join): a filled bar (`style = bar`) or the two parallel lines of ISO 5807
(`style = doubleline`); 2–6 ports (`ports`, default 3) evenly spaced on each long side; wider than high it lies
horizontally (ports top and bottom), else upright (ports left and right). It chains with the elbow connector.

Decision exits: when "+" (quick add) adds connectors to a decision, the first gets the exit label "yes", the second
"no". The label is stored language-neutral (`exit = yes|no` on the connector) and written in the language of the
drawing ("ja"/"nein" in German); an edited connector text wins. The parameter `exitLabels` ("a|b|c") gives other
labels in quick-add order; the preset "Decision (compare: < = >)" (`ann.flow.decision.compare`) labels three exits
"<", "=", ">". The library items "Connector “yes”" / "Connector “no”" carry the same language-neutral exit.
Core hook: `ObjectType.ChainConnectorParams(o, index)` returns parameters for the index-th connector leaving an
element; `Flow.QuickAdd` counts the connectors already leaving it and applies them.

**Exit labels and step numbers from the flow (round 7, 2026-11-26).** Both are off by default, so existing drawings
stay unchanged. The flow is read from the drawing: flowchart symbols (`ann.flow.*`) joined by connectors whose first
and last points are docked on them, from the first to the last.

- *Exit label style* (`exitStyle` on a decision, action "Exit labels ▸ yes / no | 1 / 2 / 3"): `yesno` (default) or
  `numbers`, which labels the exits "1", "2", "3" … in quick-add order (any number of exits). `exitLabels` still wins.
- *Label exits automatically* (`autoExits` on a decision, toggle in the bar): every elbow connector leaving the
  decision without its own text or `exit` shows the label of its position among the decision's exits in drawing order
  ("yes"/"no" translated, or the numbers), so connectors drawn by hand are labelled too. The decision redraws its exits,
  and an exit redraws its siblings (`Dependents`).
- *Number steps along the flow* (`numberSteps`, offered on the first element of a flow, i.e. no connector leads
  into it and one leads out, usually the start): the steps reachable from it get `stepPrefix` + `stepFirst`, then
  + `stepIncrement` ("S100", "S110", "S120" …; advanced parameters, defaults "S", 100, 10), written outside the upper
  right corner like an entered `step`. Order: depth first along the exits in drawing order, loop-backs ignored, and a
  step where branches meet only after all of them (a yes/no branch pair gets consecutive numbers before the joining
  step). Process, decision, data, document, predefined process, manual operation, preparation, delay and loop limit
  are numbered; start/end, connectors, off-page connectors, stored data and the parallel bar are passed through. An
  entered `step` wins (its automatic number stays reserved, so the others do not shift). A step reached from two
  numbered starts keeps the number of the first start in drawing order.
- Flow boxes now read their neighbours (`DependsOnNeighbours`), and the elbow connector names the numbered steps of its
  flow as dependents, so hosts that keep elements apart (PowerPoint) redraw the numbers when the flow changes.

## Connectors and marks for technical figures (2026-11-25)

Annotation module additions for technical figures; all are insertable without parameters, black and white, and drawn
with native lines, freeforms and texts.

- **Hops and junction dots.** The elbow connector and connector polylines (an open polyline with an arrowhead or a
  docked end) draw a hop where they cross a connector drawn before them (parameter `hops`, default on; action "Hops
  at crossings on/off"; the hop geometry of Core `Crossings`). Only annotation connectors take part: plain polylines
  (drawings, imported vector graphics) neither hop nor are hopped over, and wires of other modules are not affected.
  Their drawn segments are rails: a connector end docked on another connector's segment follows it and gets a junction
  dot automatically. "Junction dot (lines)" (`ann.junction`) is a stand-alone dot whose centre port docks on a rail.
- **Block arrow** (`ann.arrow.wide`): a hollow block arrow along a straight (`route = straight`) or orthogonal route as
  one closed outline; shaft `width` 6 mm, head 2 × width wide and 1.2 × width long, heads at start and/or end, fill
  none, hatch or dots, optional text along the shaft. Presets: double-headed (`.double`), with a bend (`.elbow`),
  U-turn (`.uturn`).
- **Double-line arrow ⇒** (`ann.arrow.doubleline`): two parallel strokes (`gap` 2 mm) with an open chevron head; with
  two points it routes and docks like the elbow connector (also "C" shaped around boxes), with more points it runs
  orthogonally through them.
- **Arrow pair** (`ann.arrow.pair`): two parallel arrows in opposite directions (`gap` 3 mm, request / response) with
  a label each (`labelForward`, `labelBack`) on their outer sides; action "Swap directions".
- **Bus marks** on `ann.line`, `ann.arrow`, `ann.arrow.elbow` and `ann.polyline`: `busMark = slash` draws one oblique
  stroke with the number of lines (`busCount`, e.g. "8" or "n") beside it, `busMark = strokes` one oblique stroke per
  line (IEC 60617 S00002 / S00003 ideas, drawn independently), on the middle of the longest segment. Preset "Bus line
  with slash (n lines)" (`ann.line.bus`).
- **Table** (`ann.table`): cells as text (`cells`: rows by line break or a written "\n", columns by "|" or tab), bold
  header row, borders all / outer / horizontal / none, column widths from the content, one text size for the table;
  actions add/remove a row or column. Without cells it shows a small two-column table in the drawing language.
- **View arrow** (`ann.viewarrow`): arrow with a bold capital letter; `mode = touching` puts the letter beside the
  shaft (the head touches the outline), `freestanding` beyond the tail (`ann.viewarrow.free`).
- **Detail view** (`ann.detail.view`): circular or rectangular frame for an enlarged detail, labelled with the letter
  of its detail marker (`ann.detail.circle`; the n-th view without own letter takes the n-th marker's letter) and an
  optional graphic scale bar (`scaleBar`, `scale`, `barLength`: a bar of the drawn length labelled with the real
  length, e.g. "5 mm"), so no written scale such as "2:1" is needed.
- **Material hatching** for `ann.hatch.*` (`pattern`): liquid, glass, rubber / plastic, wood, concrete, earth / soil –
  simple independent patterns clipped to the region; library items `ann.hatch.rect.<pattern>`.
- **Heat flow arrow** (`ann.arrow.heat`): wavy shaft with a straight end and a filled head, label "Q" in italics.

Host notes: everything renders as native lines, freeforms, filled circles and texts; the block arrow is one freeform
(hatch/dots fills follow the host's pattern fill support like other hatched regions). Hops are computed by the engine
for annotation connectors only, so hosts show no "connect" icon at their crossings (that stays with wires and pipes).
Open: hops between annotation connectors and other modules' wires, routing of the block arrow around boxes (it follows
its points), a one-column table cannot get an empty row.

## Control loops (2026-11-25)

Category "Control loops": block diagrams of control engineering, drawn after the usual textbook conventions
(independently drawn, no standard figure copied).

- `diag.ctrl.sum` **summing junction**: circle Ø 6 mm in a 12 mm box, ports `left`, `top`, `right`, `bottom` on the
  circle. Parameter `signs`, one character per input in the order left, bottom, top, right (`+-` = left plus, feedback
  minus; space or `.` = no sign). Style `plain`: the sign stands next to the incoming line outside the circle (above a
  horizontal input, left of a vertical one); `cross`: the circle is divided by an X and each sign sits in the sector of its
  input. Presets with cross and with three inputs.
- `diag.ctrl.tf` **transfer block**: content `text` (object text, else the localised name of its `role`: controller,
  plant, actuator, sensor, else `G(s)`), `fraction` (`numerator` over `denominator` with a fraction bar) or `icon` (a
  small step response: P, PT1, PT2, I, D, dead time, PID; the static characteristic of a saturation; an integral sign for
  the integrator; all drawn as paths). Optional `label` (e.g. `K, T`) below the content. Ports on all sides (top:
  disturbance, bottom: feedback).
- `diag.ctrl.mult` multiplication point (× or ·), `diag.ctrl.branch` branch point (filled dot, ports on its four sides).
- `diag.ctrl.signal` **signal arrow**: thin line with a filled head (switchable, e.g. into a branch point), orthogonal
  by default. Two ends docked on the same side of two elements (both `bottom`: a feedback path) run around with an 8 mm
  clearance instead of through the blocks. Ports named after their side also give the exit direction to the other
  diagram connectors (`D.ExitHorizontal`).
- Quick add ("+") chains every control element with a signal arrow.
- `diag.ctrl.loop` **standard control loop** as one element (summing junction → controller → plant → branch point,
  feedback below; options: sensor block in the feedback, disturbance input at the plant). "Explode" turns it into its
  elements with the arrows docked on their ports, so the loop can be edited and extended element by element. Building
  the loop by hand: summing junction, "+" right → controller, "+" right → plant, a branch point, then a signal arrow from
  the branch point's `bottom` to the summing junction's `bottom` (routed around below).

## Computer systems and communication (2026-11-25)

Category "Computer systems & communication" (FIG. 1 of software inventions, network sketches):

- System blocks by role: processor, memory, storage, network interface, I/O interface, display, sensor(s), input
  device (presets of `diag.sys.block` with the new `role` parameter: the localised role name is shown while the block has
  no own text; no port squares).
- `diag.sys.bus` **system bus**: thick line (width parameter) or hollow double line through its points, labelled "Bus"
  (object text). Every segment is a rail: connection lines (taps) dock anywhere on it and follow the bus; corners can be
  added like on other polylines.
- Outline icons with the name below and ports on all four sides: `diag.sys.phone` (smartphone), `diag.sys.basestation`
  (lattice mast with radio arcs), `diag.sys.router`, `diag.sys.switch`, `diag.sys.satellite`, `diag.sys.iot` (sensor
  node with antenna).
- `diag.sys.wireless` **wireless link**: line with a lightning zigzag, or dashed with radio arcs at both ends; arrowheads
  none / end / both.

## Data layouts (2026-11-25)

`diag.data.fields` draws packets, frames, registers and memory maps from the `fields` parameter, e.g.
`Preamble:8;Header:16;Payload:n;CRC:16` (sizes in bits, `0x…` hexadecimal; `n`, `*` or `var` = variable length; one
field per line works too). Layouts: `row`; `wrapped` into rows of `rowBits` bits (a field crossing a row end continues
in the next row, a variable field fills the rest of its row); `stack` (memory map, top-down, start addresses counted from
`base`, `0x…` formatted like the base). Widths `proportional` (variable fields get an equal share) or `equal`. Variable
fields carry a break mark (two slashes) on their long edges. `ruler` adds bit offsets (row: at each field start;
wrapped: every 8 bits) or the address column (stack); `sizes` shows "16 bit" under the name. Every field has ports
`f1.top`, `f1.bottom` … (stack also `f1.right`) for leader lines and reference numerals. Actions: layout, equal widths,
ruler, add field (input `name:size`). Presets: register / header (32-bit rows), memory map.

## Sequence fragments, step numerals and time gaps (2026-11-25)

- `diag.seq.fragment` **combined fragment**: frame with a pentagon tag (`alt`, `opt`, `loop`, `par`, `break`; the object
  text is appended, e.g. `loop 1..n`), `operands` separated by dashed lines, one guard per operand (`guards`, one line
  each; while unset, localised placeholders `[condition]` / `[else]`). Add/remove operand grows/shrinks the frame by one
  operand (top edge stays). Presets opt, loop, par, break.
- Message parameter `step`: a step numeral (e.g. `302`) drawn at the start of the arrow, above the line. The document
  action **"Number messages"** numbers all messages of the page top-down (by the height of their middle, then left to
  right): the topmost keeps its numeral if it is a number (else 302), each next one gets 2 more (302, 304, 306 …). It is
  idempotent, so running it with several messages selected gives the same result.
- Lifeline parameter `breaks`: time gaps in percent of the tail (`50` or `30; 70`); the dashed tail is interrupted and
  marked with two short slashes ("//"). Action "Time gap" switches a gap at 50 % on and off. The tail stays one rail.

## GUI wireframes (2026-11-25)

Category "GUI wireframes" (black on white; the automotive HMI elements `auto.hmi.*` stay in their module):
`diag.ui.screen` (device phone, tablet, monitor, application window, browser; status bar; the object text is the title
or address; screens chain into a screen flow with directed arrows), widgets `diag.ui.button` (rounded / pill / square,
filled = primary), `.field` (text, search, drop-down, password), `.list` (rows from `items`, icons, chevrons, add/remove
row), `.checkbox` (also radio button), `.toggle`, `.slider` (value 0–100 %), `.image` (frame with both diagonals),
`.icon` (menu, search, home, user, settings, bell, star, heart, plus, close, back, share), `.tabs` (active tab bold and
underlined), and the pointers `diag.ui.touch` (finger with ripples: tap, double tap, long press, swipe; port `tip`) and
`diag.ui.cursor` (arrow or I-beam; port `tip` at the hot spot). Widget placeholder texts are localised.

## State circles and transitions (2026-11-25)

`diag.state.state` has the views `rounded` (UML) and `circle` (state diagrams in black-and-white line style: circle with
the name, 8 ports on the outline; the size adapts when switching). Library: "State (circle)" and "Transition (event
[guard] / action)" (`diag.state.transition`, a directed UML relation with that label template).

## BPMN triggers, data and flows; state pseudo states; network diagrams (2026-11-26)

**BPMN events** have a `trigger` choice (message, timer, conditional, signal, error, escalation, compensation, link,
terminate; start events: no link or terminate; end events: no timer, conditional or link; terminate only at the end).
A catching marker is outlined, a throwing one filled: end events always throw, intermediate events throw when
`throwing` is set. `interrupting` = false draws a dashed circle (non-interrupting start or boundary event). Presets:
`diag.bpmn.start.message`, `.start.timer`, `.start.signal`, `.intermediate.message` (catching), `.intermediate.message.throw`,
`.intermediate.timer`, `.intermediate.boundary.timer` (non-interrupting), `.intermediate.error` (boundary),
`.end.message`, `.end.error`, `.end.terminate`. **Data:** `diag.bpmn.dataobject` (page with a folded corner; role
input/output arrow, collection bars; presets `.input`, `.output`, `.collection`) and `diag.bpmn.datastore` (cylinder).
**Flows:** `diag.bpmn.flow` with kind sequence (solid, filled arrowhead), default (slash at the start), conditional
(hollow diamond at the start), message (dashed, small circle at the start, hollow arrowhead), association (dotted, no
head) and data association (dotted, open arrowhead); presets `diag.bpmn.flow.<kind>`. Quick add ("+") from BPMN events,
tasks and gateways adds a sequence flow (`diag.bpmn.flow`), from data objects and stores a data association
(`diag.bpmn.flow.data`; BPMN sequence flows never touch data); round 7, 2026-11-26. UML shapes keep their UML relations.

**State machine pseudo states:** `diag.state.pseudo` with kind shallow history (H), deep history (H*), junction
(small filled dot), choice (diamond, diamond ports), entry point (open circle), exit point (circle with a cross) and
terminate (cross); presets `diag.state.pseudo.<kind>`. Fork and join use the activity bar, initial and final states the
activity nodes; transitions with event [guard] / action stay the UML relation preset `diag.state.transition`.

**Network diagrams (critical path method).** `diag.net.node` is an activity on node: top row earliest start ES,
duration D, earliest finish EF; middle the optional number (`no`) and the name (object text); bottom row latest start
LS, total float TF, latest finish LF. `diag.net.link` joins an activity (start) to its successor (end), finish-to-start
without lag; quick add chains activities with it. The duration (`duration`, e.g. "5", "2,5", "3 weeks": the leading
number, one unit for the whole network) is optional; when every activity of a connected network has one and the
links form no cycle, the forward pass gives ES = max EF of the predecessors (0 at the start) and EF = ES + D, the
backward pass LF = min LS of the successors (the project end for the last activities) and LS = LF − D, the total float
TF = LS − ES and the free float FF = min ES of the successors − EF (textbook method of project planning, drawn
independently). Critical activities (TF = 0) have a heavy outline, links between two critical activities without free
float are heavy. Values are computed while drawing and never stored; a changed duration redraws the whole network
(`Dependents`). Actions: *Duration…*, *Number…*, *Arrange network* (columns by the longest chain of predecessors).
Example: A (3) → B (2) → D (4), A → C (5) → D: critical path A–C–D with 12 units, B has 3 units of float.

## Interactive pools and lanes (2026-11-25)

`diag.bpmn.pool` (new: horizontal lanes, pool name band on the left, localised lane placeholders) and
`diag.activity.swimlane` share the lane logic (`LaneContainerType`):

- Actions "Add lane below / above" (vertical: right / left), "Remove bottom / top lane" and "Rename lane n" (input). Adding
  or removing grows or shrinks the container by one lane about its centre, so the lanes keep their size; as a document
  action it also moves the elements standing in the remaining lanes with their lane.
- Each lane's middle line is an `Axis` anchor (`lane0`, `lane1` …). Flow nodes (actions, initial/final nodes, decisions,
  fork bars, BPMN tasks, events, gateways) expose a centre `Hub`, so a dropped or moved node snaps centred onto the lane's
  middle line, and Core coupling (hub on an axis) carries it along when the pool is moved, turned or stretched (lanes
  scale with the pool). A frame `End` mark keeps the lane lines from offering mechanical build ports.
- Limits (Core): only nodes centred on a lane line are carried; elements placed elsewhere in a lane or other modules'
  elements (`ann.flow.*`) do not follow a moved pool. A general containment relation would need Core support.
## Fault trees, sequential function charts, Ishikawa diagrams and mind maps (2026-11-26)

All symbols are drawn independently after the usual textbook conventions (fault trees as described e.g. in IEC 61025,
step-transition charts in the style of GRAFCET, IEC 60848); no compliance with these standards is claimed. Every element
is insertable without parameters; texts are the object text (double-click or the host's text field), as for other
diagram nodes.

**Fault trees** (category "Fault tree analysis (FTA)"):

- Events: top event (rectangle, heavier outline, bold text), intermediate event (rectangle), basic event (circle),
  undeveloped event (diamond), house event (house outline), conditioning event (ellipse; the condition of an inhibit
  gate). Gates: AND (flat base, round top), OR (curved base, pointed top), k-out-of-n (OR shape with `k/n`, parameters
  `k` and `n`, action "k out of n" asks for `2/3`), inhibit (hexagon). Gates have an output stem at the top (port `top`)
  and an input stem at the bottom (ports `bottom`, `bottom.q1`, `bottom.q3`); the gate text (e.g. `G1`) stands to the
  right of the gate. Transfer symbol: triangle with a label (`A`).
- `diag.fta.link` joins a gate or event (start) to one of its inputs (end): a plain line, orthogonal by default. Quick add
  ("+") chains every fault tree element with such a link; "+" below a gate gives the right direction. A link drawn from a
  leaf event (basic, undeveloped, house, conditioning) up to a gate or event is read the other way round.
- **Probabilities (optional):** leaf events and the transfer symbol take a probability `p` (parameter or action
  "Probability", e.g. `1e-4`, `0.001`, `0,001`, `0.5 %`). Every gate and event above them gets its probability, computed
  while drawing (never stored): AND and inhibit = product, OR = 1 − Π(1 − p), k-out-of-n = probability that at least k of
  the inputs occur (independent events; exact, not the rare-event approximation); an event or transfer with several
  inputs reads them as OR. Leaves show `p = …` below the symbol, event rectangles inside under the text, a gate only when
  no event stands directly above it (to the right). The value is shown only when every input below is known. German
  output uses a decimal comma. Changing a `p`, a gate or a link redraws everything above it (`Dependents`).
- **Arrange tree** (action on every linked fault tree element): the whole tree holding the element is laid out top-down
  from its top event as a tidy tree (levels 8 mm apart, top-aligned; siblings 6 mm apart; each parent centred over its
  inputs); the top event keeps its top edge and centre unless the tree would run past the page edge. The links are
  docked again on the facing bottom/top ports. Elements reached twice are placed at their first visit. The general
  multi-selection layout (Core `DiagramLayout`) also works on fault trees.

**Sequential function charts** (category "Sequential function chart (SFC, GRAFCET)"):

- `diag.sfc.step` (square, step number as text, placeholder `1`), `diag.sfc.step.initial` (double frame, placeholder
  `0`); option `active` draws a token dot. `diag.sfc.transition`: thick bar across the link line, which runs through the
  element from port `top` to port `bottom`; the receptivity (object text) is written to the right of the bar.
  `diag.sfc.action`: action box (dock it on the step's `right` port), optional `qualifier` (N, S, R, D, L, P …, as in
  IEC 61131-3 SFC) in its own cell; preset "Stored action (S)". `diag.sfc.branch`: parallel branch (double line) or
  alternative branch (single line) with `branches` (2–8) docking points `b1.top`, `b1.bottom` … plus `top`/`bottom` in
  the middle; "Add/Remove branch" grows/shrinks the bar by one branch spacing.
- `diag.sfc.link`: plain line, orthogonal by default. A link whose end lies above its start (loop back from the last
  transition to the initial step) runs around on the left, `clearance` (10 mm) beyond its ends and 4 mm clear of every
  element it would otherwise cross, and carries a direction arrow in the middle of its rising run (switchable).

**Ishikawa diagram** `diag.ishikawa` (one element): effect in a head box on the right (object text, placeholder
"Effect"), a spine with an arrow into the head, one diagonal bone per category alternately above and below the spine,
each category in a frame (switchable). `categories` holds one category per line; causes follow a colon, separated by
semicolons (`Machine: wear; vibration`) and are drawn as ribs on the bone. While the parameter is not set, the six
classic categories are shown in the user's language (Man, Machine, Method, Material, Measurement, Environment /
Mensch, Maschine, Methode, Material, Messung, Mitwelt). Actions: add/remove category (up to 8), add cause (input
`2: wear`), rename category n, category frames.

**Mind map:** `diag.mindmap.node` (shapes rounded box, ellipse, box, underlined text; bold), preset "Central topic"
(ellipse, bold) and "Branch text" (underlined); `diag.mindmap.link` is a curved branch (cubic, leaving and entering
along the main direction; switchable to straight). Quick add chains nodes with branches. **Arrange map ▸** (on a node
with branches): *Both sides* (branches already on the left stay left, otherwise the lower half goes left), *To the
right*, or *Radial* (depth d on a circle d·R, every subtree in a wedge in proportion to its leaves). Links are read
without direction; the node the action runs on is the centre and keeps its place (unless the map would run past the
page edge); the branches are docked again on the facing ports.

Open: values of other kinds than probabilities (failure rates over a mission time, minimal cut sets), common-cause
handling of shared basic events (a shared event is counted as independent in each gate), SFC macro steps and
enclosing steps, mind map branch colours per main branch, a dedicated "+" that adds a child of another type (quick add
copies the element; "next element" replaces it).

## Context actions

| Type | Actions |
|---|---|
| Class / interface / enum | show/hide attributes and operations, add/remove attribute (literal), add/remove operation, abstract |
| State | show activities, add entry/exit activity, remove last exit activity |
| Composite state | add/remove region |
| Swimlanes, pool | horizontal/vertical (swaps width and height), add lane at either end, remove first/last lane, rename lane n |
| Summing junction, transfer block, signal arrow | feedback sign, input from above, cross style / content (name, fraction, characteristic), characteristic ▾ (options show the curve) / arrowhead, swap, routing ▾ |
| Data fields | layout, equal widths, ruler, add field |
| Fragment, message, lifeline | operator, add/remove operand / number messages / time gap |
| GUI widgets | device, status bar, filled, shape, kind, rows, checked, on, value ±10, glyph ▾ (options show the glyph), active tab ▾, gesture |
| Entity | add/remove attribute, weak entity |
| ER attribute / relationship | key, multivalued, derived / identifying |
| UML relationship | kind (8 choices), swap direction (multiplicities and roles stay at their class ends), orthogonal routing |
| Crow's-foot connector | start ▾ / end ▾ cardinality (none, one, one and only one, zero or one, many, one or many, zero or many), swap ends, identifying, routing ▾ (straight / orthogonal) |
| Sequence message | sync/async/reply/create, swap direction |
| Block | add/remove port left/right |
| Lifeline, container, lollipop, task, gateway | destroyed / actor head, dashed frame / title band, required socket, sub-process, gateway marker ▾ |
| Fault tree elements | probability (leaves, transfer), k out of n (voting gate), arrange tree |
| BPMN event, data object, flow | trigger ▾, throwing, interrupting / role ▾, collection / kind ▾, reverse direction, routing ▾ |
| Pseudo state | kind ▾ |
| Network activity, dependency | duration…, number…, arrange network / reverse direction, routing ▾ |
| SFC step, branch, link | active (token) / branch ▾ (parallel, alternative), add/remove branch / upward arrow, reverse direction, routing ▾ |
| Ishikawa diagram | add/remove category, add cause, rename category n, category frames |
| Mind map node, branch | shape ▾, bold, arrange map ▸ (both sides, to the right, radial) / curved, reverse direction |

Adding a row grows the box downwards by one row (top edge stays in place); removing a row shrinks it.

## Host notes

All symbols are built from native primitives (rectangles, ellipses, lines, paths, text), so they stay editable
with the host's formatting tools. Rounded corners are emitted as paths. Lane headers of horizontal swimlanes use
text rotated by −90°. BPMN event and gateway labels are written below the symbol, outside its box (BPMN
convention). Notation sources: UML 2.5.1 (OMG) for UML shapes, BPMN 2.0.2 (OMG) for BPMN shapes, Chen (1976) and
the crow's-foot notation (Information Engineering) for ER; all symbols are drawn independently. Fault tree gates are
closed paths (cubic curves), the SFC upward arrow and the Ishikawa spine head are native shapes/arrowheads, and mind map
branches are single cubic paths, so every host draws them as native freeforms. Fault tree values are text primitives;
hosts that keep shapes apart (PowerPoint COM) redraw the elements above a changed event through `Dependents`.
