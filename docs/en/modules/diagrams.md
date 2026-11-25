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
| BPMN-lite | `diag.bpmn.start`, `diag.bpmn.intermediate`, `diag.bpmn.end`, `diag.bpmn.task` (sub-process marker), `diag.bpmn.gateway` (exclusive, parallel, inclusive, event-based) |

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
place (top edge and centre); connectors follow their elements and dock on the facing sides (bottom → top, right →
left; an end on a port moves to the nearest port of that side). A long chain laid out left to right can run past the
page edge. Works for any module's box elements joined by docked
lines (process steps, flowchart, UML, BPMN, ER, blocks).

## Context actions

| Type | Actions |
|---|---|
| Class / interface / enum | show/hide attributes and operations, add/remove attribute (literal), add/remove operation, abstract |
| State | show activities, add entry/exit activity, remove last exit activity |
| Composite state | add/remove region |
| Swimlanes | horizontal/vertical (swaps width and height), add/remove lane |
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
