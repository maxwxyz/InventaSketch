# Interaction concept: handles, floating bar, pie menu

Status: concept and first implementation in the PowerPoint COM add-in (2026-11-25). Web and LibreOffice follow the same
rules (follow-ups below). German version: [../../de/design/interaction-ux.md](../../de/design/interaction-ux.md).

**Why.** The maintainer's verdict of 2026-11-25: "Review the overlay bar for everything, also the pie menu – it is a
catastrophe, the UI/UX explains nothing." Then: "Powerful, but understandable for new users and older people; not too
much text in the overlay; it must be consistent: what is found where, how is what operated, what belongs together."

## 1. What the review found (COM, before this change)

1. **Handles on top of each other.** On a short pipe the rotate handle (PowerPoint's), a lock badge, a corner "+", a
   branch handle, the extend "+" and a second line-end "+" (both at the same end), the point diamonds and PowerPoint's
   move cross all lay within a few pixels. Every handle family placed itself on its own (`PortScreen` avoided only
   PowerPoint's handles, lock badges only each other, segment handles nothing).
2. **Several "+" with different meanings.** Corner "+" in the middle of a segment (outlined), extend "+" beyond a free
   end (filled), build/connect "+" at ports and line ends (filled, the same look), the "+" pictogram in the bar for
   "add row" and "add column" (one of them was even dropped because both used the same pictogram), and the boxed "+"
   of Insert. None said what it adds.
3. **Bar without order.** The order changed with the element (views first for some, choices first for others), no
   separators between groups, icon-only buttons with generic pictograms ("+", "−", cycle) for different things, a
   multi-selection bar with the same pictogram for "distribute" and "diagram layout", duplicate and delete always taking
   room although Ctrl+D and Del exist.
4. **Pie without places.** The ring was filled in priority order, so the same command sat in a different place for every
   element; "More" was an overflow of whatever did not fit; cycle actions duplicated the dropdown choices; no entry said
   what it does; greyed entries were a click that closed the menu.
5. **Insert box** stayed open on a click outside (only Esc closed it).
6. **Tables** had no direct way to add a row or column where it is wanted (only "add at the end" in the bar or pie).
7. From the inventory of all modules (section 3): pictograms reused for different meanings (`ArrowEnd` for UML
   relations, message kinds and arrowheads; `Text` for data entry and toggles; `Rotate` for "next angle" and "align to
   wall"), icon ids that are not in the shared set and draw a placeholder, actions that duplicate a choice or a view,
   families with 8–11 actions that cannot all be in a bar.

## 2. Where things live

| Place | What goes there | Rule |
|---|---|---|
| **On the element (handles)** | Everything that is done by dragging or one click *at a place*: move, resize, rotate (PowerPoint's handles, taken over), points and ends (diamonds), "+" at a place (build port, free end, table side), corner dot, branch, junction, lock badge, quick-add triangles, chart part outline. | Direct manipulation first. One look per meaning (section 4). Never two handles on top of each other. |
| **Floating bar** | The main action of *this* element (with a short label), its next two or three commands as pictograms, then "⋯". | Context only, small, pictograms with tooltips; a short label only where no pictogram is telling. Never grows with content. |
| **Pie menu** (right-click, Shift+F10, "⋯") | Everything for the element, grouped in eight fixed places. | Complete, labelled, one-line hint, the same place means the same thing for every element. |
| **Ribbon** | Insert (library, search & insert), document- and slide-wide settings (snap, anchor, line group, accent, plan scale of the slide, black and white), tools (clean slides, compatibility, export). | Nothing element-specific. |
| **Sidebar editor** | Every parameter (inspector), data tables, the library with search. | The "long form"; never needed for the quick sketch. |
| **Keyboard** | R / Shift+R, X, Y, Ctrl+D, Del, Ctrl+Space, Shift+F10, Esc, Tab (next part of the assembly), Ctrl+double-click (select connected), Alt (no snapping), Shift (15° steps). | Shown in tooltips and pie hints. |

## 3. Inventory: interactions per element family (all modules)

Derived from the engine (`ObjectType` actions, views, anchors, `Building.Ports`, `Flow.CanChain`, `Polylines`, parts).

| Family (module) | Geometry | Views | On the element | Bar (key actions) | Notes |
|---|---|---|---|---|---|
| Gears, ring gears, bevel, worm, rack (Mechanical) | box | 2–5 | build "+" per free mesh side / inside / apex / hub, lock per mesh | hub dropdown, helix, true size | coupling, motion mode, select assembly |
| Planetary sets, differential, carrier | box | 2–3 | "+" on free pins | planets ±, fixed member | explode into parts |
| Shafts, bearings, couplings, seals | box | 3–4 | "+" at free axis ends (drag: stretch), lock per part on the shaft | hollow | gears ride on the shaft |
| E-machines, drive blocks, kit (Machines) | box | 2–4 | "+" at axis ends and connection ports | arrangement, type dropdowns | drives the shaft |
| Linkages: links, joints, mechanisms (Kinematics) | points / box | 2 | diamonds on joints, "+" on free joints | angle ±, crossed, F = n, coupler curve, ghosts | motion mode, explode mechanism |
| Electrical symbols | box | 1–4 | "+" at free terminals | state, polarity, designator … | installation views switch with walls |
| Wires, cables, buses, PCB tracks | points (corners) | 1 | diamonds, one "+" per free end, corner dots, branch, junction at crossings, lock per docked end | routing dropdown, hops | docked ends follow |
| Fluid symbols, valves | box | 1–2 | "+" at free ports | actuation, spring side, medium | |
| Pipes, ducts, process and energy lines | points (corners) | 1 | as wires | route, line kind dropdowns | |
| Process, energy units, building services | box | 1–3 | "+" at free ports | kind, medium, steppers | plan scale, assembly view switch |
| Walls, rooms (Architecture) | points (corners) | – | diamonds, corner dots, "+" at free wall ends | thicker/thinner, kind dropdown, 1:50 | openings follow the wall |
| Openings, stairs, plan symbols | box | 1–2 | – | width ±, align to wall | |
| Charts (incl. figure charts) | box | – | grips on limit lines, value lines and markers; part outline after a click on a part | data …, style, sketch a curve | parts: series, axis, grid, legend, limit … |
| Diagram nodes: flowchart, UML, BPMN, ER, control | box | 1–2 | quick-add triangles, ports on hover | rows ±, toggles | "Make it …" after a quick add |
| Arrows, lines, leaders, dimensions, connectors | points | – | diamonds, "+" at free ends (polylines), corner dots | line type, heads, line style dropdowns | type conversion, label menu |
| Table, text, parts list (Annotation) | box | – | table: "+" on each side | header, remove last row/column | |
| Patent numerals | points | – | diamonds | numeral, underline | renumber (multi-selection) |
| Maps: routes, areas, markers | points / box | 1–3 | diamonds, corner dots | point ±, reverse | |
| Vehicles, roads, ADAS (Automotive) | box / points | 3–4 | "+" at road ends, mounts | view, heading | |
| Robots, cells, tools (Robotics) | box / points | 2 + style | "+" on the flange (mount) | joints ±, home, ghosts | motion |
| Geometry (Maths) | points | – | diamonds | sides ±, angle …, divisions | |
| Multi-selection (any) | – | – | PowerPoint's | one view for all · align, distribute · diagram layout · dimension, format painter, renumber | only what changes something |

## 4. One visual language for handles

| Look | Meaning | Operation |
|---|---|---|
| PowerPoint's white squares/circles on the frame, rotate arrow above, move cross below small frames | resize, rotate, move (PowerPoint's own) | taken over by InventaSketch with snapping; InventaSketch never draws on top of them |
| **Orange diamond** on a point | this point (end, corner, joint) | drag moves it (snaps and docks); click on a corner selects it (bar: split, remove); double-click removes a corner |
| **Filled blue circle with white "+"** | *add something here* – the tooltip says what (meshing gear, next shaft section, line or element at a port, continue the line, row above …) | click: the insert box with what fits there first (or, for table sides, adds right away); drag at a shaft end: stretch; drag at a free line end: draw the line on |
| **Small hollow blue dot** in a segment | a new corner | click adds it, drag bends the line |
| **Hollow circle with a stub** beside a segment | a branch | drag to a pin or anywhere; click: a short stub |
| **Hollow circle with a cross and a dot** at a crossing | connect the crossing lines (junction) | click |
| **Blue lock** (closed) / **grey lock** (open) beside a contact point, thin lead line | coupled, moves along / touching but released | click toggles |
| **Blue triangle** outside a side (diagram nodes) | the next element on that side | click: a connected copy; drag: connect to another element or place |
| **Blue-rimmed square with small arrowheads** on a value (chart limit line, value line, marker) | a value that is dragged on the drawing; the arrowheads show the allowed direction | drag: the line follows (constrained), the new value shows beside the pointer and snaps (a tenth of the axis step); release = one undo step; Esc cancels; a click selects that part |
| **Small blue-rimmed dot** on a data point of a curve (charts) | a value of the curve; filled blue with a soft glow when selected | appears for the selected curve (or point), the others near the pointer; click: selects the point; drag: moves it (snapped, one undo step, Esc cancels); Del deletes it |
| **Blue outline** of a part (charts) | the selected part; pressing on a selected curve and dragging moves the whole curve | the bar and pie act on it; Del deletes the part (point, curve, area …) or hides it, never the element |
| **Soft blue glow** around the outline of an element | it is selected (alone, with others, or by "select connected") | – (section 6a) |
| **Fainter blue glow** | what a hovered "Select connected" entry (or "select the assembly") would add | choosing the entry selects it |
| **Red dashed guides, red dot, label** while dragging | snapping and what it docks to | Alt suspends |
| **Second colour (blue) ghost** while dragging | what moves along (coupled neighbours) | |

Sizes: diamonds 10 px, "+" 13 px, dots 7 px, locks 15 px (logical pixels × display scale); every target answers within
12 px (24 px hit area) and the nearest handle wins, so neighbouring targets never steal clicks; PowerPoint's own handle
wins when it is nearer (a diamond wins over the resize handle at the same place).

**Layout.** `HandleLayout` (COM, pure, unit tested) places all handles of the element together: PowerPoint's handles,
diamonds and triangles stay where they are; the others in priority order ("+" 90, table "+" 85, lock 70, junction 60,
corner dot 40, branch 30) take the first free place on a search around their preferred place – further out along their
direction, leaning up to 40°, locks and branches also around their anchor, corner dots only along their segment – with
a gap of at least 3 logical px. Optional handles (corner dot, branch) without a free place are hidden; on elements
smaller than 24 px none are shown (zoom in). A "+" that had to move keeps a thin dashed lead to its port.

### Calm by default: progressive disclosure (round 2, 2026-11-25)

Verdict after the first round: "still the same" – the handles no longer overlapped, but a selected gear still showed
four "+", a lock badge and lead lines, a short cable six handles. Rule: **show what the element is and the one next
step; everything else appears where the pointer goes.**

- **Always shown:** PowerPoint's frame, the point diamonds of a line, and at most **one** "+" (the primary place):
  - the free end of a line or cable (else its start);
  - a gear's free side to the right (else any free side);
  - a connection port, then a free axis end, then any build port;
  - the bottom of a table;
  - the triangle below a diagram node.
- **Near the pointer:** every other handle fades in from about 56 logical px and is fully there within 24 px; it fades
  out again when the pointer leaves. These are further "+", corner dots, branches, junctions, value grips, the other
  triangles and lock badges. Opacity follows the distance, so there is no flicker and no timer.
- **Shift** shows all handles at once (for an overview), releasing it hides them again.
- **Attached:** a tiny grey dot marks every joint. The lock badge appears near the joint in quiet grey, turns blue
  under the pointer, and only then shows its lead line. A "+" pushed away from its port shows its dashed lead only
  under the pointer.
- **Selected chart part:** its grips stay shown.
- Hidden handles take no clicks away: a handle can only be hit with the pointer on it, where it is fully shown.

## 5. Floating bar

The same structure for every element (round 2): **at most four buttons plus "⋯"**.

1. **The main action, with a short label:**
   - "Make it …" right after a quick add;
   - else "Data" (charts);
   - else "Text" (texts, diagram nodes, numerals, tables: their cells);
   - else "View" (a dropdown, for elements with several views);
   - else the first style choice (lines: e.g. the arrowheads);
   - else "Insert".
2. **Up to three pictograms with tooltips**, in this order of preference:
   - the element's own actions whose pictogram is unambiguous;
   - the view dropdown;
   - Insert;
   - sketch a curve, coupler curve, ghost positions, explode, move mode, select assembly;
   - the pictorial style dropdowns;
   - rotate 90° and mirror, where they change the drawing.
3. **⋯** – the pie menu: everything, grouped. Duplicate and delete are there (and Ctrl+D, Del).

A selected corner shows "Split" (labelled) and "Remove". A selected chart part shows its name, its three most used
commands and "⋯". Buttons are 32 logical px (targets of at least 28 px) with a tinted, outlined hover state.
Tooltips say what happens plus the shortcut ("Mirror horizontally (X)", "Line style: Dashed – click to choose"). The
multi-selection bar keeps its groups: one view for all | align and distribute | diagram layout ("Diagram ↓", "Diagram →")
| dimension, format painter, renumber.

### Pictograms: one pictogram, one meaning (F3, 2026-11-25)

Every action pictogram is an id of `IconIds` (Core) and is drawn by both hosts that draw pictograms: COM
`IconPainter` and web `icons.ts` (LibreOffice uses text labels only). The F3 pictograms are SVG path data on the
24-unit grid, defined once and copied into both hosts (`IconPainter.SharedPaths`, `icons.ts`); a web test checks that
both tables are identical. Contact sheets (light and dark, 32 and 16 px) are written by
`tests/InventaSketch.AddIn.Tests/IconPainterTests.WritesIconSheet` and `hosts/office-web/test/icons.test.ts`.

- **Text ("T") only for editing the text.** Names, designations, tags and titles use *Name* (a label tag); numbers use
  *Value* (a field with a cursor) or, where the quantity is known, a quantity pictogram (angle, length, width, height,
  thickness, area, volume, mass, pressure, power, voltage, capacity, state of charge, time, frequency, tolerance, flow,
  x/y range; `IconIds.ForQuantity` maps units). Showing a label uses *Show label*, port names *Port labels*.
- **Structure:** add/remove a row (bottom or top), a column, an operation, a port, a point; *one more/one fewer* for
  repeated parts (teeth, poles, modules, steps …). "+"/"−" (*Increase/Decrease*) only for stepping a value.
- **Direction and place:** *Reverse* (U-turn) reverses a direction, *Side swap* puts a dimension or a lead line on the
  other side, *Polarity* swaps + and −, *Swap* exchanges two things (ends, spring side), *Flow arrow* shows a direction
  arrow, *Joint at start/end*, *Next/previous position* (a joint turned one step), *Home*, *Align to wall*. *Rotate* and
  *Mirror* stay the host's commands for the element.
- **Charts:** *Data*, *Legend*, *Grid*, *Tick numbers*, *Smooth*, *Formula* (fx), *Parameters*, *Code* (JSON),
  *Contour*, *Function generator*, *Marker*, *Value line*, *Hatch between/below*, x/y axis break and its removal.
- **Drawing by hand:** *Pencil* ("sketch a curve"); *Trace* only for the coupler curve; *Ghosts* for ghost positions.
- **Label-only markers:** *Toggle* (switch), *Cycle* (next variant) and *Option* (pick this option of a choice:
  relation kinds have their line-end pictograms, other kinds such as fragment or screen kinds use *Option*) never stand
  alone: the bar shows these actions as short text. Step pictograms ("+"/"−", one more/fewer) are shown with their label.

The engine test `tests/InventaSketch.Engine.Tests/IconHygieneTests.cs` checks every action of every element (default
state, after each action, each part): known pictogram; `Text` only when the action asks for the text; `Option` only for
actions that set a parameter; within one element no pictogram with two meanings (meaning = id without place qualifiers
such as `.top`, `Left`, `.2`; label-only markers and explicit step pairs `.inc/.dec`, `.more/.fewer` excepted); labels
at most 34 characters (pie), the ones longer than 22 (bar limit) are listed. Views sharing a pictogram are listed too
(next round of view pictograms).
### 5a. Choices are dropdowns (rule, 2026-11-25)

Maintainer: "Curve shape is a switch to click through, not a dropdown – we had that before, did you forget everything?!"

**Rule: a choice among several options is always a dropdown with the options' pictograms or pictures and labels, the
current one marked – never a cycle button.** It holds for the element and for each of its parts:

- Engine: a choice is one action per option with the same `ActionChoice` (`src/InventaSketch.Core/ActionChoices.cs`;
  id `<choice>=<value>`, the current option `IsActive`; parameter choices through `ActionChoices.FromParam`). Options
  without a shared pictogram carry a small drawing (`ContextAction.Preview`: the curve in that shape, the fill pattern,
  the colour). Commands that form a menu ("Fill to ▸", "Add area ▸") are an `ActionChoice` with `IsMenu` (nothing
  current). No `IconIds.Cycle` action that steps through options is left in the charts.
- Bar: one dropdown button per choice, showing the current option's pictogram or picture (else a short text) and a
  caret; a click opens the list of every option with pictogram or picture and label, the current one marked
  (`FloatingBar.SetDropdown`, the same look as the element's choices). Part choices use the key "@p.&lt;choice&gt;",
  element action choices "@a.&lt;choice&gt;"; picking runs the option ("part:&lt;id&gt;", "act:&lt;id&gt;").
- Pie: state choices sit under **Style ▸** as "Label: Current ▸" (the selected part's own choices when a part is
  selected), menus under **Edit ▸** as "Label ▸"; both open the options (long lists continue in "More ▸").
- Web and LibreOffice: the engine reports `choice`, `choiceLabel`, `choiceMenu` and `previewSvg` with every action, so
  they group the options the same way.

## 6. Pie menu

Eight fixed places for every element, clockwise from 12 o'clock (top = edit/text, right = insert, bottom = style,
left = view/arrange):

```
                 Text …
     Arrange ▸            Edit ▸
  View ▸                       Insert …
     Delete               Duplicate
                 Style ▸
```

- **Text …** – edit the text (double-click). **Edit ▸** – the element's own commands (type actions, line type, label,
  "Make it …", explode, sketch, move mode, parts); with a selected chart part its commands (titled with the part name).
- **Insert …** – the insert box. **Duplicate** (Ctrl+D). **Delete** (Del).
- **Style ▸** – one entry per choice ("Line style: Dashed ▸"), each opening its options with pictograms, the current one
  marked. Actions that only set such a choice are not listed again under Edit.
- **View ▸** – the views (current marked) and the plan scale. **Arrange ▸** – rotate 90° either way, mirror,
  "Select connected ▸" (section 6a; it replaces "select the assembly"), animate as slides.
- A place with nothing for this element stays, greyed (a click on it keeps the menu open). A place with one entry
  shows it directly, except Edit, Style and Arrange, which keep their name.
- Submenus start with "← Back"; more than eleven entries continue in "More ▸".
- Hovering an entry shows its full label and a one-line hint below the ring. Esc, a click outside or on the centre
  closes the menu.
- The centre names the element (or its selected part, or the open submenu), so it is clear what the menu acts on.
  Greyed places have low contrast, so the available ones stand out.

## 6a. Selection look and "select connected" (2026-11-25)

Maintainer: "Possibility to select / show connected elements: connected conductors and lines (loops), rotationally
coupled elements e.g. on shafts / gears, closed loops, all following chain links, closed walls, networks", and: "the
selection is shown slightly differently, e.g. like Slidemax puts a blue gradient behind it – not only connected ones, also
single selected ones: when they lie on top of each other it is often unclear what is selected."

**Selection look.**

- Every InventaSketch selection – one element, several, the result of "select connected" – gets a soft blue glow along
  the exact outline of the element: its drawn lines and shapes, widened and blurred. It is not a bounding box.
- The element itself stays clear: its lines and the inside of its closed shapes are cut out of the glow, so the glow
  lies around the element as if behind it.
- Where the selected element's outline runs under another element, the glow shows over that element. So it is always
  clear which of overlapping elements is selected; the element on top gets no glow of its own.
- The glow is calm: one blue, at most about half opaque right at the outline, fading out within a few pixels. It is
  deeper blue on light slides and lighter blue on dark slides (from the slide background). It lies under every handle,
  the bar and the pie.
- While dragging, resizing or rotating, the ghost takes over and the glow is hidden. A multi-selection that PowerPoint
  moves gets its glow back where the shapes are once the button is released.
- What a hovered "Select connected" entry (or the bar's "select the assembly") would add glows fainter (about half),
  before anything is selected.

**Select connected.**

- The pie's Arrange place has "Select connected ▸" when anything is connected with the element. Its entries are only the
  kinds that apply, each with a one-line hint and the number of elements:

  | Entry | Selects | Typical elements |
  |---|---|---|
  | **Net** | every connection line and element joined to it: wires, cables, buses, pipes, ducts, process and energy lines, branches, junctions and connected crossings, and the elements on them (the whole circuit or piping) | wire, cable, pipe, PV string, pump |
  | **Moves along** | every part that moves with it: meshing gears, the shaft and its hubs, concentric parts, belts and chains over their pulleys, linkages | gear, shaft, pulley, link |
  | **Following elements** | everything after it along directed connectors, with the connectors; a linkage from the selected link outward (away from the frame) | flowchart step, block, process unit, connector |
  | **Closed loop** | the loop the element is part of (only offered when there is one): a circuit, a flowchart loop, the walls around a room, a gear loop | wire in a circuit, step in a loop, wall |
  | **Room walls** | walls joined with it at corners, ends and T joints | wall |

- Hovering an entry previews it with the fainter glow; choosing it selects those elements in PowerPoint (the
  multi-selection bar then offers one view for all, align, diagram layout …).
- **Ctrl+double-click** on the selected element selects the most meaningful kind for it: a wire or pipe its net, a
  gear or shaft what moves along, a flowchart step the following elements, a wall the room walls. (Ctrl+A stays
  PowerPoint's "select all".)
- "Select the assembly" (bar, Tab to the next part) is the same as "Moves along".
- The rules come from the engine (`Connectivity`), so the web add-in and LibreOffice select the same sets.

## 7. Feedback and accessibility

- Snapping: red guides, a dot at the snap point and a label naming the target ("pitch circle", "axis", "pin 2");
  Alt suspends. Coupled neighbours are previewed in blue while dragging. Lock badges show attached (blue) and released
  (grey) and toggle with one click.
- Undo is always one step per gesture; Esc always backs out (drag, insert, draw on, pie, insert box, dropdown).
- Hit targets of 24 logical px at the display scale; the bar and pie use 9–10 px text at least, high-contrast ink in
  light and dark Office themes; positions never depend on the element (bar order, pie places).
- Hints are one line; tooltips say what happens, not how it works; both languages.

## 8. Implemented now (COM, 2026-11-25)

- `OnSlide/HandleLayout.cs` (pure layout, tests `tests/InventaSketch.AddIn.Tests/HandleLayoutTests.cs`) and
  `OnSlideController.Handles.cs`: one layout for diamonds, "+", locks, junctions, corner dots, branches, table "+";
  nearest-handle hit testing; the handle list for UI tests (`LastHandles`, log line "Handles: …").
- One "+" per free line end (the line-end port "+" draws the line on when dragged); corner dots instead of a second "+".
- Bar regrouped (`OnSlideController.Menus.cs`), Insert with label, "⋯" opens the pie, pictograms only when unambiguous,
  multi-selection bar grouped.
- Pie with eight fixed places, submenus, hints (`PieMenu.Hint`), greyed places keep the menu open; `LastPieItems`, log
  line "Pie: …".
- Insert box closes on a click outside (`InsertSearch.ClickedOutside`) and when another window is activated.
- Tables: actions grouped `side.top/bottom/left/right` (engine, `TableType`), drawn as "+" on each side; a row at the
  top goes under the header row.
- Grips on values (`OnSlideController.Handles.cs`, engine `Registry.DragHandles` / `MoveHandle`): drawn in the layout as fixed handles (priority over moving the body inside the plot), hover tooltip with the value, constrained drag with a live preview of the changed part and its new value, one undo step, Esc cancels, a click selects the part; `DragHandlesOf` for UI tests.
- UI scenarios: `overlay-handles`, `pie-menu`, `insert-box-dismiss`, `table-plus`, `chart-drag-handles` (tests/powerpoint-com/scenarios).

### Round 2 (2026-11-25): calm overlay, four-button bar, fitted insert

- Progressive disclosure of every handle (`OnSlideController.Handles.cs`: `Reveal`, `PrimaryHandleId`, `UpdateReveal`,
  faded drawing); quiet lock badges; leads only on hover; the shown handles for UI tests (`LastShownHandles`, log line
  "Shown: …").
- Bar: main action with label plus up to three pictograms plus "⋯" (`BarCandidates`); 32 px buttons with a clear hover;
  every command stays reachable in the pie (`LastCommandIds`, log line "Commands: …").
- Pie: the element's name in the centre, greyed places with low contrast, linkage and corner commands under Edit.
- Tables: "Text" edits the cells (one line, "|" and "\n"); double-click too.
- Fitting: the selected unit is tried object by object (shafts first, then the shaft a selected part sits on); insert
  at the pointer previews the fitted result near a shaft ("Fits here – click to insert") and inserts exactly that
  (`OnSlideController.Fitting.cs`; `Fitting.PlaceNear` is used once the engine has it, by reflection until then).

Before → after, right after selecting with the pointer away from the element:

| Element | Before (round 1) | After (round 2) |
|---|---|---|
| Gear with a meshing partner | 4 blue "+", 1 "+" towards the partner, lock badge with lead, PowerPoint's frame; bar with insert, views, hub, helix, mirror … (8–10 buttons) | PowerPoint's frame and one "+" (the free side to the right); a grey dot at the mesh; bar: "View ▾" + up to 3 pictograms + ⋯ |
| Short cable docked on one end | 2 diamonds, "+", lock badge with lead, corner dot, branch | 2 diamonds and one "+" at the free end; a grey dot at the docked end; corner dot, branch and lock appear near the pointer |
| Chart with limit lines | grips always, bar with data, sketch, insert, style … | quiet chart; grips appear near the lines (always for a selected part); bar: "Data" + up to 3 pictograms + ⋯ |
| Flowchart box | 4 triangles, bar with text, insert, actions, mirror … | the triangle below; the others near the pointer; bar: "Text" + up to 3 + ⋯ |
| Table | 4 "+" on the sides; bar: insert, header, remove row/column … | one "+" below (a row); the others near their side; bar: "Text" (the cells) + up to 3 + ⋯ |

### Round 3 (2026-11-25): selection look and select connected

- Engine: `Connectivity.Connected / AutoKind / Options` (`src/InventaSketch.Core/Connectivity.cs`) with the kinds net,
  moves along, following elements, closed loop and room walls, built on attachments, line media, coupling, linkage roles
  and the neighbours a type renders from.
- COM: `OnSlide/SelectionHalo.cs` (glow, cached per selection and view, never rendered inside a hook) and
  `OnSlideController.Connected.cs` (pie entries, hover preview, Ctrl+double-click, selecting the units); log lines
  "Halo: selected=…; connected=…" and "Select connected (kind): n units"; UI scenario `select-connected`.

### Round 4 (2026-11-25): choices as dropdowns, data points, curves and areas on charts

- Choices on parts and elements (section 5a): `ActionChoices` in Core; every chart cycle (style, scales, legend,
  orientation, mode, machine, system, show, function, edges, curve shape, fitted function, smoothing, line style, marker,
  fill) became a choice; COM `OnSlideController.Choices.cs` (`ActionBarChoices`, `PartChoices`, `ElementActionChoices`,
  option pictures from previews), `OnSlideController.Parts.cs` (part bar and pie), pure helpers
  `OnSlide/ActionChoiceBar.cs` (tests `tests/InventaSketch.AddIn.Tests/ActionChoiceBarTests.cs`); log line
  "Dropdown [label]: …", automation `DropItems()`, `Command("menu:c:@p.<choice>")` opens a pie submenu.
- Data points and curves (docs/en/modules/charts.md): point parts with part shapes (`ObjectType.PartShapes`,
  `Registry.PartPrimitives`), point grips shown with their curve (`DragHandle.ShowWith`), whole curves dragged by
  pressing on the selected curve (`DragHandle.ByPart`), Del on a part (`PartLooks.DeleteAction`).
- Areas: hatched and patterned areas of every matplotlib kind, created with "Fill below ▸", "Fill to ▸" and "Add area ▸".
- UI scenarios: `chart-points`, `chart-curve-drag`, `chart-areas`, `part-choice-dropdown`.

## 9. Follow-ups (phased, with acceptance criteria)

**Phase 1 – finish the language in COM**
1. *Grips beyond charts*: the same grips for every element whose type offers drag handles (dimension values, angles,
   robot joints, gear centre distance). Accept: each such type lists its handles in `DragHandles` and the COM overlay
   needs no change; the grips keep their place in the layout.
2. *Dependency highlight on hover*: hovering a lock badge or "Select assembly" outlines every unit that moves along in
   the second colour. Accept: a gear on a shaft shows the shaft and its other gears outlined; nothing else. *Done for
   "select the assembly" and the "Select connected" entries (fainter glow, section 6a); lock badges open.*
3. *Engine icon hygiene*: every action icon id is in `IconIds.All` (or a new id with a pictogram in all three hosts);
   one pictogram, one meaning (UML relations and message kinds become choices with option pictograms; `Text` only for
   editing text). Accept: an engine test fails for an unknown icon id or one id used with two meanings in one type.
   the second colour. Accept: a gear on a shaft shows the shaft and its other gears outlined; nothing else.
3. *Engine icon hygiene* – **done (F3, 2026-11-25)**, see "Pictograms" in section 5 and `IconHygieneTests`.
   Open: views of nine mechanical elements still share a view pictogram (listed by the test).
4. *Actions that duplicate a choice or a view* get `ParamKeys` or are removed (shaft hollow toggle vs. hollow view,
   robot style vs. views, `view.*` actions of six modules, `map.point.add/remove` vs. corner dots). Accept: the pie of
   these elements lists each thing once.
5. *Between rows/columns of a table*: "+" dots on the inner row and column lines (insert there). Accept: a click on
   the dot between row 2 and 3 inserts a row there.
6. *Corner dot insert where it is drawn* when the layout slid it along its segment. Accept: the new corner appears
   under the dot.

**Phase 2 – the same concept in the web add-in and LibreOffice**
7. Web (`hosts/office-web/src/ui/stage.ts`, `contextbar.ts`, pie): the same handle looks, layout (port the pure
   `HandleLayout`), bar groups and pie places. Accept: the web scenarios show the same bar ids and pie places as COM for
   a gear, an arrow, a pipe and a table.
8. LibreOffice: the on-canvas overlay (ROADMAP item) built on this concept from the start.

**Phase 3 – help for new users**
9. First-use hints: the first selection of an element with "+" shows one line "Blue + adds what fits here" for three
   seconds (once per user). Accept: shown once, never again after a click.
10. Pie and bar in a larger size setting (125 %) for low vision, from the ribbon's settings. Accept: all hit targets and
    texts scale; the layout keeps handles apart.

## 10. Testing

Unit tests (no PowerPoint): handle layout (no overlap on the short pipe of the report, priorities, hiding, PowerPoint's
handles of thin frames), pie places and submenus, bar grouping and pictogram rule, light dismiss, table side actions
(`tests/InventaSketch.Modules.Annotation.Tests/TableSideTests.cs`). UI scenarios in real PowerPoint (run by the
coordinator, PowerPoint closed): `overlay-handles`, `pie-menu`, `insert-box-dismiss`, `table-plus`, `select-connected`;
connectivity (`tests/InventaSketch.Core.Tests/ConnectivityTests.cs`, `tests/InventaSketch.Engine.Tests/ConnectivityModuleTests.cs`)
and the halo (`tests/InventaSketch.AddIn.Tests/SelectionHaloTests.cs`) without PowerPoint; screenshots in
`local-resources/powerpoint-tests/…` show the result for review.
