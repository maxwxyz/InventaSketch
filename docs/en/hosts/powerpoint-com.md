# PowerPoint COM add-in (Windows)

> German: [../../de/hosts/powerpoint-com.md](../../de/hosts/powerpoint-com.md)

## Architecture

- `hosts/powerpoint-com/InventaSketch.AddIn` – .NET Framework 4.8 class library, AnyCPU, COM-visible entry class
  `InventaSketch.AddIn.Connect` (`IDTExtensibility2`, `IRibbonExtensibility`). Office interop types come from NuGet and
  are embedded, so the project builds with `dotnet build` and no interop assemblies ship.
- The engine (`InventaSketch.Engine` + modules, netstandard2.0) runs in-process. Library items are created with the
  engine, lowered by `DrawPlanBuilder` with `DrawCapabilities.Full` and written by `ComRenderer` as native shapes:
  `AddShape` (rectangle, rounded rectangle, oval, triangle, diamond), `AddLine` with dash styles and native arrowheads,
  `BuildFreeform` for polylines and Bézier paths, `AddTextbox`, grouping via `Shapes.Range(...).Group()`.
- Each inserted unit is a group (or single shape) named `ISK:<type>:<id>` with tags `ISK_ID`, `ISK_TYPE` and
  `ISK_MODEL` (compressed model payload, ADR-0006). One undo step per insertion (`StartNewUndoEntry`).
- Exchange with LibreOffice (`OnSlide/DocumentMirror`): before saving, every unit's model is mirrored into the custom
  document properties `ISK_MODEL_<id>` (chunks `_1`, `_2` … of 60 000 characters) and its drawn bounds into
  `ISK_BOUNDS_<id>` (LibreOffice format, points); mirrors of deleted units are removed. After opening, shapes named
  `ISK:<type>:<id>` without tags but with a mirrored model (drawn by LibreOffice) get their tags back and are units
  again; the presentation's saved state is kept. Verified 2026-11-20 in both directions with PowerPoint 16 and
  LibreOffice 26.8 (.pptx).
- Ribbon tab **InventaSketch**, generated from the module library: a tools group (Editor, Snap and Context bar
  toggles stored in `%LOCALAPPDATA%\InventaSketch\settings.json`, black-and-white, info), then one group per module
  (basic shapes, annotation, patent, mechanics, kinematics, electrical, machines, automotive, architecture, diagrams)
  with one thumbnail gallery per library category (6 columns, name in the
  tooltip; groups with many categories show icons only so the tab fits on laptop screens). New modules appear without
  ribbon changes. Thumbnails are rendered with GDI+ from the engine primitives and follow the Office theme.
- Floating bar and pie menu show abstract line pictograms (`IconIds`, drawn by `OnSlide/IconPainter`) for views,
  mirror, rotate, duplicate, delete, type and label menus — deliberately not renders of the concrete element; the bar shows a tooltip for the hovered button. Rendering tests
  (`tests/InventaSketch.AddIn.Tests`, Windows) write light and dark PNGs of bar and pie menu for visual review. UI language follows the Office UI language (German if
  the LCID is German, otherwise English).
- Automation object for scripts and tests: `Application.COMAddIns("InventaSketch.AddIn.Connect").Object` with
  `Insert(itemId)`, `Place(itemId)`, `Editor(visible)`, `Command(commandId)`, `LibraryItems()`, `Version()`.
  `Command` runs a bar or pie command on the selected unit (`dup`, `rot90`, `view:<id>`, `act:<action>`, `anim`).
- **Animate as slides** (pie menu of mechanisms, `ObjectType.AnimationFrames`): the slide is duplicated 11 times, each
  copy shows the unit at the next input angle (same name, same stacking position) and gets a Morph transition
  (`ppEffectMorphByObject` = 3954, read from the installed type library) that advances after 0.4 s. One undo step.
  Verified 2026-11-20 with a four-bar linkage.
- Log: `%LOCALAPPDATA%\InventaSketch\addin.log`.
- Logo: `assets/branding/logo.svg` is the single source; `hosts/office-web/scripts/icons.mjs` rasterises it into the
  manifest PNGs and `assets/branding/inventasketch.ico` (16–256 px), which is embedded in the add-in (`Branding`) for the
  title bars of the prompt and the about dialog, the ribbon's About button and collapsed tools group, and is the web
  host's application icon.

## On-slide interaction (slice 2)

`OnSlide/OnSlideController` adds interaction directly on the slide, following patterns proven in Slidemax:

- **Mapping:** `SlideView` samples `DocumentWindow.PointsToScreenPixelsX/Y` over a wide span (the API rounds) and finds
  the slide pane under the slide centre (`paneClassDC`, or `mdiClass` in Microsoft 365 minus thumbnails, notes and
  scroll bar). Everything runs per-monitor DPI aware (`DpiScope`).
- **Input:** thread-local `WH_MOUSE`/`WH_KEYBOARD` hooks on PowerPoint's UI thread, installed only while a mode is active
  or an InventaSketch unit is selected. Hooks only compute and draw; object-model calls run via `BeginInvoke`.
  Press/release pairs are never split between PowerPoint and the add-in.
- **Overlay:** a click-through layered window over the slide pane (`UpdateLayeredWindow`, per-pixel alpha).
- **Insert at pointer:** picking a ribbon item shows an orange outline that follows the pointer and snaps (gear mesh,
  hub on shaft axis, points, alignment guides); click inserts, `Esc` cancels, `Alt` suspends snapping.
- **Drag with snapping:** pressing inside a selected unit (PowerPoint keeps the handles on the border) moves it with
  the same snapping and red guides; one undo step on release.
- **Resize with snapping:** PowerPoint's resize handles of a selected unit are taken over. Corner handles scale
  uniformly (`Shift`: free), edge handles stretch one axis, `Ctrl` scales about the centre, `Alt` disables snapping.
  The dragged handle snaps to points and alignment guides; with uniform scaling a gear's pitch circle snaps into mesh
  with a neighbouring gear or ring gear. The preview shows the scale factor; on release the unit is re-rendered from
  its model (parameters unchanged, only sizes).
- **Rotate with snapping:** PowerPoint's rotation handle is taken over; the unit rotates about its centre and snaps to
  multiples of 15° within 4° (`Shift`: strict 15° steps, `Alt`: free). The preview shows the angle; on release the
  rotation is applied to the model (box objects rotate, point objects rotate their points) and re-rendered.
- **Keyboard (selected unit, slide focused):** `R` rotate 90° (`Shift+R` back), `X` / `Y` mirror horizontally /
  vertically, `Ctrl+D` duplicate with new ids, `Shift+F10` or the context-menu key open the pie menu. Arrow keys, `Del`,
  `Ctrl+C/X/V` stay with PowerPoint; a pasted copy gets new ids automatically when it is selected (no two units ever
  share an id).
- **Theme:** floating bar and pie menu follow the Office theme (dark grey/black, or "use system setting" with a dark
  Windows app theme → dark).
- **Two-point tool:** lines, arrows, dimensions and lead lines are placed like connectors: press at the start point
  and drag to the end point (or click start, click end). Both ends snap to centres, corners, ends and ports of other
  units; curve control points keep their relative position.
- **Point editing:** a selected line/arrow/dimension/lead line shows diamond handles on its points; dragging one
  moves it with the same magnetic snapping (`Alt`: free). These handles take precedence over the resize handles.
- **Convert type and control labels (pie menu):** for lines, arrows, dimensions and lead lines the pie menu has
  "Type ▸" (line, arrow, double arrow, curved arrow, elbow connector, lead line, reference numeral, linear and aligned
  dimension; end points stay, numeral/text, layer, group and colour are kept) and "Label ▸", generated from the type's
  optional parameters: edit texts (numeral, dimension text, prefix/unit), cycle choices (line course straight/curved/
  wavy, object end, terminator, direction), toggles and text size. "← Back" returns to the main menu.
- **Floating bar:** a selected unit shows a bar above it (clear of PowerPoint's rotation handle); when that would cover
  other elements of the slide (a coupled gear, the shaft) it goes below, right or left, wherever it hides the least
  (`FloatingBar.Placement`). Content: the "next element" palette after a quick add, its views, edit text, up to five key
  type actions (telling pictograms such as thicker/thinner, more/fewer, swap direction first, then switches and cycles
  such as hub or helix as short text buttons), up to three fitting parts that snap into place right away (gear on the
  shaft, gear in mesh, bearing beside a gear: `Fitting.Place`), "Insert" (opens the other fitting elements as thumbnails
  in the bar), mirror horizontally/vertically,
  rotate 90°, duplicate, delete. Fully opaque within 12 logical px,
  otherwise 0.92 fading over 180 px from the closest pointer distance; below 0.1 it is dismissed until the next
  selection; typing hides it. Clicks on the bar never reach the slide.
- **Choices as dropdowns:** every choice of the selected element (line type, arrowheads, head size, line style, line
  course, e-machine arrangement and type, shaft-hub connection, …) is a bar button showing its current value as a
  pictogram (`ParamOption.Icon`, `ParamDef.WithOptionIcons`) or a short text, with a caret. A click opens a list under
  it with pictogram and label of every option, the current one marked; a click on a row picks, a click elsewhere
  closes. Cycle actions over the same parameters (`ContextAction.ParamKeys`) stay in the pie menu only; switches with
  a telling pictogram (shaft stubs, housing connection, helix) are icon toggles. No text buttons where a pictogram exists.
  Pictograms exist for shapes (square, round, triangle), fills (tint, hatch, dots, none), routing of wires and pipes
  (free, orthogonal, 45°), wall kinds and fluid line types; units, signal colours and similar choices stay short text.
- **Plan scale on the slide:** plan elements (`ObjectType.UsesPlanScale`: walls, rooms, openings) show the slide's plan
  scale as a bar button ("1:50") and a pie entry; both open the plan-scale prompt and re-render the slide.
- **Web add-in units:** model tags split by the web add-in into chunks (`ISK_MODEL`, `ISK_MODEL_2` …, `ISK_MODEL_N`) are
  joined when reading (`SketchUnits.ModelTag`).
- **Quick add (diagrams):** flowchart, UML, ER, BPMN, system and basic elements (`ObjectType.ChainConnector`) show
  blue triangles 13 logical px outside each side, and their connection ports (blue circles) when the pointer is near.
  Click a triangle: a copy is added on that side (past elements already there) with the kind's connector between the
  facing ports, attached at both ends (`Flow.QuickAdd`); the bar then shows the "next element" palette, one click
  turns the copy into another element and the connector stays (`Flow.Replace`). Drag a triangle or port: drop on a
  port of another element to connect both, or on free space to add an element there. One undo step each.
- **Pie menu:** right-click on a selected unit: delete (top), views, type actions, duplicate, mirror, rotate 90°. The unit
  is re-rendered from its model; native moves and resizes are folded into the model first (`SketchUnits.Read`).

## Sidebar editor (custom task pane)

The ribbon toggle **Editor** opens a docked task pane (`Sidebar/TaskPaneControl`, ProgId `InventaSketch.AddIn.TaskPane`,
created through `ICustomTaskPaneConsumer`) with a WebView2 that loads the same editor as the web add-in
(`sidebar.html`: library, stage, inspector, pie menu, floating bar) from `AddIn\App` via a virtual host. WebView2 uses
its own user data folder (`%LOCALAPPDATA%\InventaSketch\WebView2`); the pane width is scaled by the display DPI.
The editor talks to the add-in through `window.chrome.webview` (`combridge.ts` ↔ `SidebarBridge`):
`units` messages carry compact model payloads that the add-in draws natively with the full C# engine (new units are
drawn, existing ISK_IDs replaced, removed units deleted — one undo step); the add-in pushes the selected unit back as
`selection`. Live sync is on by default, so edits in the sidebar update the slide immediately. A click on a library
item in the sidebar starts insert-at-pointer on the slide (snapping, `Esc` cancels), exactly like the ribbon galleries;
dragging an item into the sidebar stage adds it to the editor instead.

Verified 2026-11-20 (keyboard and placement): `R` rotated a shaft (340 × 0 → 0 × 340 pt), `Ctrl+D` produced a copy with
a new id, a PowerPoint `Ctrl+C/V` copy was given new ids on selection, and a sidebar library click followed by a click
on the slide inserted a gear.

Verified 2026-11-20: pane opens (WebView2 157, engine ready in about 0.5 s, German UI); selecting a shaft on the slide
loads it into the sidebar with its views; rotating it in the sidebar (`rotate90`) rotated it on the slide
(340 × 0 → 0 × 340 pt).

## Install (per user, no administrator rights)

**For users:** `scripts\package-com.ps1` (also run by `scripts\build.ps1`) builds `dist\InventaSketch-AddIn` and
`dist\InventaSketch-AddIn-<version>.zip`: unzip, close PowerPoint, double-click `Install.cmd`; remove with
`Uninstall.cmd`. The package contains the Release binaries, the sidebar editor (`AddIn\App`) and the
registration scripts.

**Start time and protection against disabling.** Office measures the start time of add-ins and disables slow ones,
resetting their registration on the way. The module library (about 0.7 s of cold JIT) is therefore built on a worker
thread; PowerPoint gets the ribbon from `%LOCALAPPDATA%\InventaSketch\ribbon-<de|en>.xml`, a cache bound to the
installed build (module version id plus name, size and time of every InventaSketch assembly) that `install.ps1` warms
right after registering and the add-in refreshes once the library is ready. Like Slidemax (B80), `Resilience` writes the
`DoNotDisableAddinList` entry and `LoadBehavior = 3` after startup and at shutdown and clears a hard disable after a
crash (current user only; group policies are never touched).

**Installing from automation:** registry writes from a process inside an app container (for example a shell of the
Claude desktop app) are virtualised and invisible to a PowerPoint started from the Start menu. Run `Install.cmd` (or
`install.ps1`) through `explorer.exe` so it runs in the normal user context, and verify with a PowerPoint started the
same way.

**For developers:**

```powershell
.\scripts\install-com.ps1      # build Release, copy to %LOCALAPPDATA%\InventaSketch\AddIn, register (HKCU)
.\scripts\unregister-com.ps1   # remove the registration
```

Close PowerPoint before installing. The web add-in is hidden automatically while the COM add-in is installed
(`EquivalentAddins`, ADR-0004).

## Verified (2026-11-20, Windows 11, Microsoft 365 PowerPoint 16.0 build 20527, 64-bit)

- Add-in loads when PowerPoint starts normally (482 ms, 159 library items in 17 categories, German UI detected).
- Insertion through the automation object of spur gear (stick), planetary set, bevel gear, shaft, ball bearing, spring,
  hatched region (explicit hatch lines), curved arrow (native arrowhead) and lead line with numeral: all native
  groups/shapes with tags and payload; slide export checked visually.

Slice 2 (same environment, simulated mouse input with PowerPoint in the foreground):

- Drag: gear released 3 pt off the shaft axis snapped exactly onto it (centre y = 270.0 pt).
- Insert at pointer: gear placed 4 pt off the axis snapped onto it.
- Pie menu: right-click + top entry deleted the unit.
- Resize: dragging a gear's corner handle near the mesh position snapped to a uniform scale of 1.429 at which its
  pitch circle touches the neighbouring gear (`gear mesh snap=True` in the log, preview captured).
- Two-point arrow: start pressed 3 pt off a gear centre snapped exactly onto it (162.52 / 102.52 pt).
- Point editing: the arrow tip dragged 2–3 pt off the shaft end snapped exactly onto it (309.92 / 270.0 pt).
- Rotate: a shaft dragged by about 32° at the rotation handle snapped to exactly 30° (new bounds 294.6 × 170.1 pt =
  340 · cos 30° × 340 · sin 30°). Dark theme: implemented, not yet checked visually (Office runs in a light theme here).
- Lead-line numerals never overlap the line: 72 automated cases (straight/curved/wavy × bend × 12 directions) keep at
  least 0.8 mm between line and a realistic glyph box.
- Type and label menus: an arrow converted to a lead line with numeral "10"; "Line course" switched it to curved
  (slide export checked).
- Floating bar: appears above the selected gear (screen capture checked), the view button switched the gear to the stick
  symbol, moving the pointer 900 px away dismissed the bar.

Ten modules (same environment, installed from the package with `install.ps1`, 2026-11-20 evening):

- Add-in loads with 681 library items in 59 categories (about 0.9–1.1 s); the ribbon shows one group per module,
  collapsing into group menus with compact category galleries at narrower widths; labels with "&" display correctly.
- One item each of kinematics, electrical, machines, automotive, architecture and diagrams inserted natively
  (slide export checked). A car picked from the gallery was placed at the pointer; the bar shows distinct abstract
  icons for top, side, front and rear view.
- Fixed during this check: centred text in native text boxes was shifted half a box to the left (new text boxes
  auto-fit to their empty text before AutoSize was switched off) — UML/ER titles, dimension values and underlines
  are now placed correctly.

Pie menu, input prompt and dependents (same environment, 2026-11-20 evening):

- Pie menu: at most eight entries in the ring (essential entries first: delete, type/label for line types only, views,
  duplicate, rotate), everything else under "More ▸" without duplicates; labels wrap to the sector chord (two lines,
  smaller font or "…"), a pill below the ring shows the full label of a shortened entry; the active view is marked
  at the rim.
- Right-click within 6 logical px of a selected thin unit opens the pie menu; the mouse hook swallows the release of
  every press it swallowed (and stays installed until then), so PowerPoint's own context menu no longer appears.
- Input actions: "More ▸ → Thickness…" on a wall opened the prompt (layout from font metrics, correct at 200 % DPI);
  365 mm made the wall 10.3 pt thick and redrew the joined wall ("Redrew 1 dependent unit(s)"); one Ctrl+Z undid both.

UX round (2026-11-20, from the maintainer's feedback):

- Ribbon: Tools (Editor, Snap, Context bar, Black & white, Anchor, small About with logo), then **Annotate** (annotation,
  patent figures and basic shapes), then one group per domain module. Ribbon images are passed as 32-bit DIBs with
  alpha (ported from Slidemax), so thumbnails have no white box in a dark Office.
- **Black & white** is a reversible switch: it sets `DocumentSettings.Monochrome` on every unit of the slide and
  re-renders; the draw plan maps all colours (lines, fills, text, highlighters) to black/white and drops translucent
  fills. Clicking again restores the colours.
- **Text**: double-click on a selected unit, the "Text" bar button or "Text …" in the pie menu edit its main text
  (object text, reference numeral, name or label).
- **Insert ▸** in the pie menu (and the insert palette of the bar) offers elements that fit the selection
  (`ObjectType.Companions`, else the same category, always numeral/lead line/arrow), in the selection's kind of view.
  Picking one places it where it belongs (`Fitting.Place`), coupled and selected: a gear, bearing or wheel onto the
  selected shaft (hub on the axis, at a free place along it), a gear next to a gear in mesh (below, above, right or left,
  wherever it does not overlap), a shaft through the hub of a part, a bearing beside a gear onto the gear's shaft. Its
  choices (shaft connection fixed / sliding / loose …) are then in the bar. Items without such a rule start
  insert-at-pointer with snapping.
- **Anchor** (on by default): line ends snapped to another element store an attachment (with pin id) and follow when
  that element is moved, rotated or resized (one undo step); moving the line itself detaches its ends. Verified: an
  arrow snapped to a gear's edge followed the dragged gear.
- **Coupling** (with Anchor on; Alt edits freely; `Coupling.Propagate` in Core, applied across units by
  `SketchUnits.RedrawDependents`): gears whose pitch circles touch stay in mesh. Pulling a gear away from its partner
  (or pushing it closer) scales both by the same factor, so the ratio stays and the partner keeps its centre; a gear
  that grows (resize, teeth) pushes its partners with their shafts and the rest of the train along the centre line.
  Gears, bearings and hubs whose hub lies on a shaft axis follow the shaft (move, rotate, stretch); moving the gear
  alone slides it. A gear and a shaft with the same hub (axial view) move together. Resizing a meshed gear no longer
  snaps against its partner. Verified 2026-11-21: a gear pulled 9.35 mm away scaled both gears by 1.37 in mesh.
- **What is connected (lock badges):** a selected unit shows a small lock badge beside every place where it touches
  another element and follows it (`Links.For`): meshing gears (on the centre line), parts on a shaft (at the hub),
  concentric parts, docked line ends. Closed blue lock: coupled, moves or scales along. Open grey lock: touching but
  released, or a line end lying on a pin without being docked. Hovering shows what it is and what a click does
  ("Meshes with Spur gear: coupled – click releases"); a click toggles it (`Links.Toggle`, one undo step). A released
  pair is stored on both objects (`SketchObject.Released`) and forgotten once they no longer touch, so parts snapped
  together again are coupled again. The badges sit beside the contact point, so dragging a gear by its centre or a line by
  its end still works. Shown only while Anchor is on.
- **Drives, belts and chains:** a motor, e-machine or engine on a shaft takes the shaft (and the parts on it) along when
  it is moved (`ObjectType.DrivesShaft`); a gear still slides along its shaft. Belts and chains whose end hubs lie on two
  pulleys or sprockets (`ObjectType.SpansHubs`) follow a moved pulley: the strand stretches and turns, the other pulley
  stays; their lock badges read "Runs over Belt pulley".
- **One line language (extend, draw on, corners, branches, split, join):** every line-like element works the same way:
  wires, buses, cable routes, PCB tracks, pipes, roads, rails, rivers, routes, lane markings, trajectories, lane models,
  walls, polylines, and straight lines and arrows (drawn on, they become a polyline with the same heads). A free end
  shows a blue "+": drag it to lengthen the line, click it to keep drawing (each click adds a corner; double-click,
  Enter, Esc or right-click ends; the last point docks like a line end). A click on a corner selects it: the bar offers
  split here and remove (pictograms with tooltips); a closed outline (room, region, hatched polygon, closed wall) is
  opened at that corner instead. Dropping an end on a free end of a line of the same kind joins both lines. Walls get
  T joints as branches (the branch wall docks onto the wall's centre line and follows it). `Polylines.Extend`, `Split`,
  `Join`, `ObjectType.IsClosed`, `ObjectType.CornerType`. Verified in PowerPoint 2026-11-21: extend by dragging,
  drawing on with two clicks and Esc, split at a corner.
- **Shift: 15° steps.** While placing the second point of a line, extending, drawing on, branching or dragging a line
  point, Shift snaps the direction from the previous point to 15° steps (other snapping is off meanwhile).
- **Crossings of wires and pipes:** where a wire or pipe crosses another one of the same kind, the one on top (drawn
  later, higher in the slide's stacking order) draws a small hop (`ObjectType.HopsAtCrossings`, `Crossings`; switch per
  line: bar pictogram "hops"). A selected wire or pipe shows a junction icon beside each crossing (tooltip "Connect here");
  a click splits the line there and docks both parts onto the other line (`Crossings.Connect`): junction dots, and
  connection exports count it. Units render with the other units in slide z-order (`SketchUnits.WithContext(own, others,
  z)`). Verified in PowerPoint 2026-11-21: the upper wire hops, connect makes three wires with docked ends, undo restores.
- **Build handles for assemblies (gearboxes, drivetrains, e-drives):** a selected gear, ring gear, shaft, e-machine or
  engine shows a blue "+" at every free build port (`Building.Ports`): the sides of a gear (all four in axial view,
  above and below where the axis lies in the drawing, `ObjectType.MeshDirections`), inside a ring gear (planet), the
  free ends of an axis (next shaft section, coupling, bearing, e-machine, wheel …), a free hub (shaft; its "+" sits
  beyond the top right corner). A click on a "+" puts what fits there into the bar as thumbnails; a click on one docks
  it, coupled and selected (`Building.Place`). Dragging the "+" at a shaft end stretches the shaft while the parts on it
  keep their place (`Building.StretchEnd`, no coupling for that edit). The "+" keep clear of PowerPoint's resize and
  rotation handles. Verified in PowerPoint 2026-11-21: meshing gear docked at a port, bearing at a shaft end, shaft
  lengthened by dragging with the bearing in place.
- **Motion mode ("Move" in the ribbon and in the bar of parts that can move):** dragging moves things only the way
  they can move. A gear, carrier or pulley seen along its axis turns about its centre and the whole gear train turns with
  it, live while dragging (`GearTrain`: external and internal mesh with Willis' equation for planets, parts fixed on a
  shaft, compound gears, belts and chains, fixed carriers; open speeds are taken as 0, ring gears and carriers first, so a
  ring fixed to the housing comes out). Planets orbit with their carrier; centre distances and sizes never change. A
  predefined linkage turns its input crank about its fixed pivot. Shift: 15° steps; the angle is shown at the pivot;
  release applies it (one undo step, docked lead lines follow). Not saved: PowerPoint starts in edit mode. Turning a
  part in axial view with the rotation handle (edit mode) also drives its train (`Coupling.Propagate`). The lock tooltip
  of a mesh shows the ratio ("Meshes with Spur gear (i = 1.8)"). Verified in PowerPoint 2026-11-21: a sun turned by 90°
  moved carrier and planets by 24.9° (ring fixed), as computed.
- **Building linkages yourself:** a link end dropped on a joint of another link docks there and follows it
  (`Attachments.For` with the point id, `SketchDocument.ResolvePoints`); links also dock on fixed pivots, joint symbols
  and sliders. A selected link shows a blue "+" at every free joint: the next link (same length, bent by 60° so a chain
  between two pivots can move), a fixed pivot (the link docks onto its pivot point), a joint or a slider. The bar shows
  the chain's degree of freedom ("F = 1", Grübler, tooltip with links and joints). In motion mode, dragging a link
  pulls its nearest joint: the chain follows live with constant link lengths, fixed pivots stay, sliders stay on their
  guide (`Linkage.Drag`, position-based constraint solving from the current position, so the assembly mode is kept);
  release applies it in one undo step. A box anchor of a type now wins over the generic box centre with the same id
  when attachments are refreshed (a link on a fixed pivot stayed on the box centre before). Verified in PowerPoint
  2026-11-21: a four-bar built with the "+" (pivot, crank, coupler, rocker, pivot), F = 1, crank dragged: all link
  lengths kept (30.10 / 30.10 / 32.38 mm), pivots in place.
- **Exploding predefined linkages:** the four-bar and the slider-crank show "Explode into parts" in the bar too: they
  become fixed pivots, links and a slider at the current position, docked onto each other (part recipes with points,
  docks and an align anchor, `PartSpec.Points`, `PartSpec.Docks`, `PartSpec.AlignAnchor`), i.e. a self-built linkage
  with F = 1 that can be edited part by part and dragged in motion mode. Verified in PowerPoint 2026-11-21. The toggle
  (pivot, two links, slider on the vertical guide), the quick-return mechanism (two pivots, slotted lever, crank with its
  pin in the lever's slot, link, ram slider) and the scotch yoke (pivot, crank with its pin in the slot of the yoke plate,
  sliders at the slot end and the rod end) explode too. **Slots:** binary links and plates offer slots along their edges
  (rails `slot0` …); a link end docked there slides along the link in motion mode (pin in slot, counted as a higher pair
  in F). The cam mechanism is not exploded: no single part keeps the cam profile and the cam contact is not part of
  self-built chains yet.
- **Building gear trains yourself:** a planet carrier (`mech.carrier`, axial and schematic, 2–8 pins, disc or arms,
  "fixed carrier" with a ground symbol) is a part of its own. Its build "+" offer a planet on every free pin (sized to
  mesh with the sun or ring already there) and, in the centre, a sun gear, a ring gear (sized to the planets) or a shaft;
  a sun gear's "+" in the centre offers a carrier with room for planets (pin radius 1.8 × the sun). Planets on the pins
  ride on the carrier (they move with it) and mesh with sun and ring. Planetary sets have 2–8 planets and stepped
  planets (schematic and axial view); in the axial views a set explodes into sun, planets, ring gear and carrier with
  the bar pictogram "Explode into parts" (`Assemblies.Explode`, `ObjectType.Parts`), coupled as before. Bevel gears have
  their cone apex as an anchor: their "+" offer a mating bevel gear above or below (axes at 90°) and one facing it on
  the same axis, so a differential is built from a side gear, the opposite side gear and spider gears. Gear ports only
  offer parts in the same kind of view (no side-view shaft for an axial gear). Bevel pairs are not coupled yet (moving
  one does not move the other).
- **Free docking of annotations on surfaces:** lead lines, reference arrows, numerals and dimensions
  (`ObjectType.DocksOnSurface`) dock an end that is dropped anywhere on another element, not only on a snap point
  (`Attachments.OnSurface`: the smallest box below the point); the end then follows that element. Verified in
  PowerPoint 2026-11-21: a lead line tip on a rectangle followed it.
- **Corners and branches of lines:** a selected wire, pipe, road, river, route, region or polyline
  (`ObjectType.InsertablePoints`) shows a "+" in the middle of each segment (on the drawn path of routed wires). Click
  it for a new corner, or drag it to bend the line in one go. Lines that take branches (named rails) also show a branch
  handle beside it: drag it to a pin or anywhere and a new line of the same kind starts on the line (`Polylines.Branch`);
  a click adds a short stub. Wires and pipes draw a junction dot at a branch end, roads join. Corners between the ends
  snap but never dock to other elements (only line ends do). A double-click on a corner
  removes it. Branches already docked on the line stay where they are (`Polylines.InsertCorner` docks them again).
- View switches keep proportions (library presets of both views; a shared dimension such as the gear diameter keeps its
  length). Centred parts (pitch circle or hub) resize about their centre by default (Ctrl: opposite edge). Schematic
  hub symbols keep their size when a stick symbol is scaled up.
- The sidebar editor drops units deleted on the slide (`present` message on every selection change).

Formatting groups (2026-11-20): the tab reads Structure | Tools | Annotate | Colour | Text | Layout | Elements, like
Slidemax. Structure, Colour and Text use Office's own controls (new slide, paste, format painter, font/fill/outline
colour, font, size, bold … bullets); Layout adds arrange, group, own same height/width/size, shape size, align and
distribute (relative to the selection, a single shape relative to the slide) and swap (ported from Slidemax). The
domain modules are one large menu each in "Elements", so the tab fits on laptop screens (narrow windows collapse Layout
and Elements into menus). Formatting done with these controls on InventaSketch units is kept: every drawn shape
remembers its rendered look (tag `ISK_FMT`), and reading a unit folds the differences (line colour and weight, fill,
text colour, font, size factor, bold/italic/underline) into the objects' style (`NativeFormat`). Verified: a gear
recoloured red with 2.5 pt lines stayed so after a rotation re-rendered it.

Extras menu (tools group, like Slidemax's tools menu, ported in a lean form): insert symbol, change decimal separator
(comma/point in numbers of the current slide or the whole presentation; InventaSketch units are skipped), proofing
language (German, English UK/US, more via Office's dialog; selection or all slides), send slides (selected slides as
.pptx/.pdf/both, Outlook mail with attachment, otherwise files on the clipboard), save slides (.pptx/.pdf, opened
afterwards), show and hide modules (stored in settings.json as `HiddenModules`; hidden modules disappear from the
ribbon). Verified: the menu appears, hidden modules are missing from "Elements".

Box selection of points: with a line, polyline, wire or wall selected, Alt + drag beside it draws a box; the points
inside are selected (filled handles) and move together when one of them is dragged (one undo step). Verified: two of
four polyline points moved up 40 pt together.

Data export (Extras ▸ Export data, 2026-11-20): offers the domain exports that apply to the selected units or the
slide (bill of materials, connection graph as Mermaid/GraphML, netlist, reference sign list), saves the file and copies
the text to the clipboard. Verified: two equal spur gears and a shaft gave a bill of materials with quantity 2.

## Known limits / next steps

- PowerPoint started via `New-Object -ComObject PowerPoint.Application` did not list the freshly registered add-in in
  this test; a normally started PowerPoint did. To be investigated for automated tests.
- Automation calls from other processes arrive on RPC threads; `BeginPlacement` marshals itself to the UI thread.
- Dark theme of bar and pie menu is checked only through rendering tests, not yet in a dark Office.
- Not yet verified on the slide: text prompt dialog, `X`/`Y`/`Shift+R`, pie menu via keyboard.
- Next: style controls (line, fill, arrowheads) on the bar, domain exports (BOM, netlists, topologies).
