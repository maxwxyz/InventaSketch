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
| UML state machine | `diag.state.state` (entry/do/exit), `diag.state.composite` (regions); initial/final states are presets of the activity nodes |
| UML sequence | `diag.seq.lifeline` (box or actor head, dashed tail, optional destruction X), `diag.seq.activation`, `diag.seq.message` (sync, async, reply, create; self message when both ends lie on one vertical) |
| UML relationships | `diag.uml.relation`: association, directed association, aggregation, composition, generalization, realization, dependency, link; multiplicities and role names at both ends, label in the middle |
| ER | `diag.er.entity` (views `diagram`/`table`/`chen`, weak entity), `diag.er.attribute` (key, multivalued, derived), `diag.er.relationship` (Chen diamond, identifying), `diag.er.connector` (crow's foot) |
| System & architecture | `diag.sys.block` (ports per side), `diag.sys.container` (group frame with title tab or band), `diag.sys.cloud`, `diag.sys.database`, `diag.sys.queue`, `diag.sys.server`, `diag.sys.client` (browser window), `diag.sys.user`, `diag.sys.lollipop` (provided ball / required socket) |
| BPMN-lite | `diag.bpmn.start`, `diag.bpmn.intermediate`, `diag.bpmn.end`, `diag.bpmn.task` (sub-process marker), `diag.bpmn.gateway` (exclusive, parallel, inclusive, event-based), `diag.bpmn.pool` (pool with lanes) |
| Control loops | `diag.ctrl.sum`, `diag.ctrl.tf`, `diag.ctrl.branch`, `diag.ctrl.mult`, `diag.ctrl.signal`, `diag.ctrl.loop` (explodable) |
| Computer systems & communication | system block presets by role, `diag.sys.bus`, `diag.sys.phone`, `.basestation`, `.router`, `.switch`, `.satellite`, `.iot`, `diag.sys.wireless` |
| Data layouts | `diag.data.fields` (row, wrapped rows, memory map) |
| GUI wireframes | `diag.ui.screen`, `.button`, `.field`, `.list`, `.checkbox`, `.toggle`, `.slider`, `.image`, `.icon`, `.tabs`, `.touch`, `.cursor` |

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

## Patent flowcharts (annotation module, 2026-11-25)

The flowchart symbols of the annotation module (`ann.flow.*`) fit their text: words wrap onto lines inside the symbol
(the inscribed area of diamond and circle), the size goes down until it fits, and only below 60 % of the size are long
words split with a hyphen. "Start (circle)" (`ann.flow.start`) and "End (double circle)" (`ann.flow.end`) complete the
set. The elbow connector routes by the sides its ends are docked on: exits on the same side run around with a clearance
("C" shape), facing sides meet in the middle, perpendicular sides take one corner when it lies ahead of both. It carries
a label at its start; the library has "Connector “yes”" and "Connector “no”" (`ann.arrow.elbow.yes` / `.no`). The frame
of labels and notes takes a dashed or recoloured style (dashed note boxes). Checked against the maintainer's flowchart
(engine test `TheMaintainersFlowchartIsLaidOutWithoutLinesThroughBoxes`: after the layout no connector runs through a
box). Open: obstacle-aware routing for hand-placed boxes (only the layout computes clearances).

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

Category "Computer systems & communication" (software patent FIG. 1, network sketches):

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

`diag.state.state` has the views `rounded` (UML) and `circle` (patent-style state diagrams: circle with the name, 8
ports on the outline; the size adapts when switching). Library: "State (circle)" and "Transition (event [guard] /
action)" (`diag.state.transition`, a directed UML relation with that label template).

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
## Context actions

| Type | Actions |
|---|---|
| Class / interface / enum | show/hide attributes and operations, add/remove attribute (literal), add/remove operation, abstract |
| State | show activities, add entry/exit activity, remove last exit activity |
| Composite state | add/remove region |
| Swimlanes, pool | horizontal/vertical (swaps width and height), add lane at either end, remove first/last lane, rename lane n |
| Summing junction, transfer block, signal arrow | feedback sign, input from above, cross style / content (name, fraction, characteristic), next characteristic / arrowhead, swap, orthogonal |
| Data fields | layout, equal widths, ruler, add field |
| Fragment, message, lifeline | operator, add/remove operand / number messages / time gap |
| GUI widgets | device, status bar, filled, shape, kind, rows, checked, on, value ±10, next glyph, next active tab, gesture |
| Entity | add/remove attribute, weak entity |
| ER attribute / relationship | key, multivalued, derived / identifying |
| UML relationship | kind (8 choices), swap direction (multiplicities and roles stay at their class ends), orthogonal routing |
| Crow's-foot connector | cycle start/end cardinality (one → one and only one → zero or one → many → one or many → zero or many), swap ends, identifying, orthogonal routing |
| Sequence message | sync/async/reply/create, swap direction |
| Block | add/remove port left/right |
| Lifeline, container, lollipop, task, gateway | destroyed / actor head, dashed frame / title band, required socket, sub-process, next gateway marker |

Adding a row grows the box downwards by one row (top edge stays in place); removing a row shrinks it.

## Host notes

All symbols are built from native primitives (rectangles, ellipses, lines, paths, text), so they stay editable
with the host's formatting tools. Rounded corners are emitted as paths. Lane headers of horizontal swimlanes use
text rotated by −90°. BPMN event and gateway labels are written below the symbol, outside its box (BPMN
convention). Notation sources: UML 2.5.1 (OMG) for UML shapes, BPMN 2.0.2 (OMG) for BPMN shapes, Chen (1976) and
the crow's-foot notation (Information Engineering) for ER; all symbols are drawn independently.
