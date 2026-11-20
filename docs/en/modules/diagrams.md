# Diagrams module (`diagrams`, type ids `diag.*`)

> German: [../../de/modules/diagrams.md](../../de/modules/diagrams.md)

UML, entity-relationship (ER), system/architecture and BPMN-lite diagrams for quick technical sketches. Every
symbol is insertable without parameters; free scaling changes only the box size. Simple flowchart starters
(process box, decision, terminator, elbow connector) stay in the annotation module (`ann.flow.*`,
`ann.arrow.elbow`) and are not duplicated here.

## Concepts

- **Box symbols** render in local coordinates and expose **Port anchors** at the edge midpoints and quarter points
  (12 per rectangle). Ellipses expose 8 ports on the outline, diamonds their vertices and edge midpoints, blocks
  their port squares, lifelines a port every 5 mm along the tail, activation bars a port every 2.5 mm per side.
- **Connectors** are point objects (relationships, crow's-foot connectors, sequence messages). Their ends snap to
  ports and are stored as attachments, so they follow moved, rotated and stretched boxes. With two points a
  connector is straight or, with `route = orthogonal`, routed with right angles (ends attached to a box edge leave it
  perpendicular). With more points, the points are waypoints.
- **Multi-line content** (attributes, operations, literals, lanes, activities) is entered in text parameters, one
  line per row. Each line renders as its own text primitive.
- **Text fitting:** text uses the `textSize` parameter (default 3.5 mm ≈ 10 pt). If the content does not fit the
  box, the text is reduced automatically (not below 1.8 mm); the parameter itself never changes.
- **End notations** (arrowheads, diamonds, crow's feet) are drawn as explicit primitives, so hollow triangles and
  diamonds look identical in every host. A user stroke colour also recolours filled heads and texts; width and dash
  overrides apply to the outline or connector line only.

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
- **Entity attributes:** `*` primary key (underlined), `#` foreign key (`(FK)` in the diagram view, `FK` column in
  the table view); both can be combined (`*#order_id`). A type follows a colon: `id: int`.
- **Stereotypes** are written without guillemets; they are added automatically (`interface` → `«interface»`).
- **Relationship direction:** the decoration sits at the end point, i.e. draw from the part/child/client to the
  whole/parent/supplier.

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
