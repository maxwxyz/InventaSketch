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
| **Floating bar** | The 3–5 most frequent commands of *this* element, then its views and transforms, then its style choices, then "⋯". | Context only, small, pictograms with tooltips; a short label only where no pictogram is telling. Never grows with content. |
| **Pie menu** (right-click, Shift+F10, "⋯") | Everything for the element, grouped in eight fixed places. | Complete, labelled, one-line hint, the same place means the same thing for every element. |
| **Ribbon** | Insert (library, search & insert), document- and slide-wide settings (snap, anchor, line group, accent, plan scale of the slide, black and white), tools (clean slides, compatibility, export). | Nothing element-specific. |
| **Sidebar editor** | Every parameter (inspector), data tables, the library with search. | The "long form"; never needed for the quick sketch. |
| **Keyboard** | R / Shift+R, X, Y, Ctrl+D, Del, Ctrl+Space, Shift+F10, Esc, Tab (next part of the assembly), Alt (no snapping), Shift (15° steps). | Shown in tooltips and pie hints. |

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
| **Blue outline** of a part (charts) | the selected part | the bar and pie act on it |
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

## 5. Floating bar

Order, the same for every element, groups separated by a thin line:

1. **Element:** "Make it …" (just after a quick add, with label) · **Insert** (pictogram *and* label – the main action) ·
   split / remove a selected corner · edit text · up to five key actions (pictogram only when telling *and* no other
   action of the element uses it, otherwise a short text) · 1:50 · sketch a curve · F = n, coupler curve, ghosts ·
   explode · move mode · select assembly.
2. **View and transforms:** up to three view buttons or one view dropdown · mirror ↔ · mirror ↕ · rotate 90° – only
   those that visibly change the drawing.
3. **Style:** dropdowns with the current value as pictogram (line type, arrowheads, line style, routing, …).
4. **⋯** – the pie menu (everything else, grouped). Duplicate and delete are there (and Ctrl+D, Del).

Tooltips: "what happens" plus the shortcut ("Mirror horizontally (X)", "Line style: Dashed – click to choose"). A
selected chart part has its own bar (part name, its actions, parts list, whole element, ⋯). The multi-selection bar has
the groups one view for all | align and distribute | diagram layout (text "Diagram ↓", "Diagram →") | dimension,
format painter, renumber.

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
- **View ▸** – the views (current marked) and the plan scale. **Arrange ▸** – rotate 90° either way, mirror, select the
  assembly, animate as slides.
- A place with nothing for this element stays, greyed (a click on it keeps the menu open). A place with one entry
  shows it directly, except Edit, Style and Arrange, which keep their name.
- Submenus start with "← Back"; more than eleven entries continue in "More ▸".
- Hovering an entry shows its full label and a one-line hint below the ring. Esc, a click outside or on the centre
  closes the menu.

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

## 9. Follow-ups (phased, with acceptance criteria)

**Phase 1 – finish the language in COM**
1. *Grips beyond charts*: the same grips for every element whose type offers drag handles (dimension values, angles,
   robot joints, gear centre distance). Accept: each such type lists its handles in `DragHandles` and the COM overlay
   needs no change; the grips keep their place in the layout.
2. *Dependency highlight on hover*: hovering a lock badge or "Select assembly" outlines every unit that moves along in
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
coordinator, PowerPoint closed): `overlay-handles`, `pie-menu`, `insert-box-dismiss`, `table-plus`; screenshots in
`local-resources/powerpoint-tests/…` show the result for review.
