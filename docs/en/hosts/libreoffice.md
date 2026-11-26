# LibreOffice extension (Impress, Draw, Calc)

> German: [../../de/hosts/libreoffice.md](../../de/hosts/libreoffice.md)

Phase 1 of the LibreOffice host (roadmap "Phase 3 – LibreOffice extension", ADR-0005): an installable `.oxt` that
inserts InventaSketch library elements as native, editable LibreOffice shapes in Impress, Draw and Calc and stores the
model on the shapes so they can be edited again. Ported from the maintainer's Slidemax LibreOffice host (engine process,
UNO shape creation, sidebar plumbing, main-thread marshalling, packaging), translated and adapted.

## Architecture

```
LibreOffice (Impress/Draw/Calc)
 └─ inventasketch_component.py      Python-UNO: protocol handler (menu, scripting API), sidebar panel factory,
                                    job CanvasJob (Jobs.xcu, OnViewCreated)
     └─ pythonpath/inventasketch_lo
         host.py      target page, insertion point, selection, insert/edit flows, one undo step per action
         plan.py      draw plan (pt) -> shape specifications (1/100 mm), no UNO, unit-tested
         render.py    specifications -> native UNO shapes, groups, line-end markers, metadata
         metadata.py  unit name, attributes, chunked document properties, native-edit detection, no UNO
         sidebar.py   deck "InventaSketch", panels "Selection", "Values", "Library", "Drawing, calculator and
                      code";
                      library.py: catalog filter, thumbnails
         ux.py        selection panel rules, recent items, page session bookkeeping, on-canvas rules (no UNO, tested)
         choices.py   interaction concept: choices as dropdowns, the eight places of the context menu, select
                      connected, insert places (no UNO, tested)
         values.py    rows of the Values panel, value level, calculator and code import report (no UNO, tested)
         drawing.py   drawing-wide settings: colour by medium, designations across pages, muted checks and
                      preferred units (document properties), calculation sheet (no UNO, tested);
                      calcsheetdialog.py: the calculation sheet dialog (copy, save, insert as table)
         canvas.py    on-canvas interaction: snap after a mouse drag, Ctrl+Space, context menu with the eight places,
                      Ctrl+double-click, selection glow (UNO input interception); glow.py: the glow itself
         inputdialog.py  one-line input dialog (main text, actions that ask for a value)
         clean.py, exporting.py, picture.py   rules of clean slides, export and chart from picture (no UNO, tested);
                      cleandialog.py, exportdialog.py: their dialogs
         engine.py    engine process (JSON lines), .NET discovery, timeouts, restart
         mainthread.py  commands from the remote bridge run on the main thread
 └─ engine/portable/InventaSketch.LibreOffice.Engine.dll   (or engine/<rid>/… self-contained)
     = hosts/libreoffice/engine (net10.0) + src/InventaSketch.Engine + modules
```

- **One engine:** all domain logic is the C# engine (`EngineApi`). Python only translates draw plans into UNO shapes
  and UI events into engine calls (ADR-0002). The engine process reads one JSON request per line on stdin
  (`{"id":1,"method":"insert","params":{…}}`, `params` = the `EngineRequest`) and answers one line on stdout
  (`{"id":1,"ok":true,"result":…}` / `{"id":1,"ok":false,"error":"…"}`); extra methods `ping`, `methods`, `exit`.
  `drawPlan` without `capabilities` uses `DrawCapabilities.LibreOffice` (freeforms, native line ends, native hatch,
  rotation).
- **Stateless sessions:** every action creates a short engine session with the size of the LibreOffice page
  (`createSession` → `insert` or `importCompact` → edit → `drawPlan` → `closeSession`). Plan coordinates (pt) are page
  coordinates; the engine process keeps no document state and is restarted transparently after a crash or a timeout
  (60 s).
- **Page session (editing):** an edit imports *every* InventaSketch unit of the page into one engine session
  (`importCompact` with `couple: false`, the selected units last), so the engine knows the parts coupled with the
  selection (gear trains, shafts, the links of a self-built chain). A payload also carries copies of the objects its
  points are attached to; units whose own objects were overwritten by such a copy are imported again
  (`ux.reimports`). Native moves/resizes/rotations since the last drawing are applied with `hostTransform`
  (`couple: false`). Then the engine operation runs on the selected object ids, and exactly the units whose objects
  changed (`changed` + `dependents` of the editor state, new objects, the selected units when moved natively) are
  redrawn; units whose objects are gone (explode) or now belong to another unit are removed (`ux.redraw_targets`,
  `ux.stale_units`). This generalises the former single-unit edit to several units (multi-selection) and to coupled
  neighbours.
- **One undo step:** LibreOffice records removing shapes through the API but not adding them. Each step therefore
  removes the replaced units (recorded by LibreOffice), draws the new ones and adds one own undo action
  (`XUndoManager.addUndoAction`) that removes the drawn units on undo (and gives replaced units their document mirror
  back) and draws them again from the same plan on redo. Groups are built as a `GroupShape` that is filled, not with
  `XShapeGrouper.group` (whose own undo action left the children loose on the page).
- **Engine start (decision):** the default package is *portable*: the engine is a framework-dependent `.dll`
  (≈0.7 MB package for all platforms) started with the user's `dotnet`. The extension looks for a host with a
  Microsoft.NETCore.App ≥ 10 runtime in `DOTNET_ROOT`, `PATH`, `%LOCALAPPDATA%\Microsoft\dotnet`,
  `%ProgramFiles%\dotnet`, `~/.dotnet`, `/usr/local/share/dotnet`, `/usr/share/dotnet`, `/usr/lib/dotnet`,
  `/opt/homebrew/opt/dotnet/libexec`, `/usr/local/bin`, `/snap/dotnet-sdk/current` (checked with
  `dotnet --list-runtimes`). Without one, a bilingual message names the download page and the searched places.
  Per-platform packages (`--rid win-x64` etc.) contain a self-contained single-file engine (no .NET needed, larger
  download); the extension prefers `engine/<rid>/` when present. `INVENTASKETCH_ENGINE` overrides the engine path.
- **Main thread:** commands from the menu and the sidebar run on LibreOffice's main thread; commands dispatched over
  the remote UNO bridge (scripts, tests) are posted to it (`com.sun.star.awt.AsyncCallback`), because opening the
  sidebar from a bridge thread crashed soffice.

## Install and use

```powershell
python hosts/libreoffice/scripts/package.py                 # dist/InventaSketch-LibreOffice-<version>.oxt (portable)
python hosts/libreoffice/scripts/package.py --rid win-x64   # dist/InventaSketch-LibreOffice-<version>-win-x64.oxt
"C:\Program Files\LibreOffice\program\unopkg.com" add --suppress-license dist\InventaSketch-LibreOffice-0.1.0.oxt
```

The version comes from `Directory.Build.props`. The package script needs a .NET 10 SDK; installing works with
`unopkg add` (LibreOffice closed) or *Tools ▸ Extension Manager*. Remove with
`unopkg remove org.inventasketch.libreoffice`.

- **Menu "InventaSketch"** (Impress, Draw, Calc): *Insert from Library…* (opens the sidebar deck), *Edit Model of
  Selected Unit*, *Redraw Selected Unit from Model*, *Values of Selected Element…*, *Calculator with Units…*, *Calculation Sheet…*,
  *Colour by Medium ▸ Off (Own Colours) / By Medium / Insert Legend*, *Add
  Reference Numeral*, *Compatibility (Drawing Rules)…*, *Compare with Previous Slide (Variants)*, *Clean Slides…*,
  *Import Drawing (SVG, DXF)…*, *Import Code (matplotlib, pgfplots, TikZ)…*, *Chart from Picture…*, *Export…*,
  *Remove Data of Deleted Units*, *About InventaSketch*.
- **Clean slides** (2026-11-25): before slides leave the company, the InventaSketch elements of the selection, the
  current slide/page/sheet or every slide/page are cleaned by the engine (`sanitize`), with the choices of the
  PowerPoint COM add-in: charts normalised (same proportions, values 0…1), randomised (plausible values, same look) or
  kept; optionally categories renamed (A, B, C …), texts replaced, metadata removed (default on) and a random seed
  (empty = a new one, the same for every page of the run). Each page is loaded like a page session, the changed units
  are redrawn in place (names and z-order kept), all pages in **one** undo step. A message box shows the counts, the
  seed and the engine's report lines, and says that native LibreOffice charts and tables, notes, comments and document
  properties are not changed. Scope and chart mode are drop-down lists (UNO dialogs group option buttons only by tab
  order). Calc offers the selection and the current sheet.
- **Export** (2026-11-25): exports the selected InventaSketch elements, else every element of the current slide/page
  (Calc: of the sheet, cropped to the elements), like the Office web add-in. The dialog lists *SVG (editable)* (engine
  `renderPageSvg` mode "export": one named group per element and part, texts stay text, for Inkscape/Illustrator) and
  the engine's domain exports that apply (`exporters`/`export`: LaTeX TikZ, chart data for matplotlib and pgfplots,
  bill of materials, netlists …) with what each keeps; the file is chosen in LibreOffice's save dialog with the
  format's extension (CSV with a byte order mark). Native LibreOffice shapes are not part of the export.
- **Chart from picture** (2026-11-25): a PNG, JPEG, BMP or GIF file becomes an editable chart in the middle of the
  slide/page (Calc: of the visible area), one undo step, selected; the engine's note (what was read, whether the values
  come from the axis labels) is shown. LibreOffice decodes the picture (`GraphicProvider`, `XBitmap.getDIB` and, for
  transparent pictures, `getMaskDIB`); `picture.py` reads the DIBs in pure Python, scales pictures down to 1600 px and
  puts transparent areas on white before calling `chartFromPicture`. The mask semantics (255 = transparent) were
  verified with LibreOffice 26.8. Pasting a picture from the clipboard (COM add-in) is not offered.
- **Sketching a curve into a chart** is not available in LibreOffice: the UNO input interception gives mouse presses
  and releases but no mouse motion, the view's pixel-to-page mapping is not exposed (the UI smoke test needs a
  calibration click for it), LibreOffice would run its own rubber-band selection during the stroke, and there is no
  overlay to show the trail.
- **Variants** (2026-11-25, Impress and Draw): a duplicated slide keeps the units' ids. *Compare with Previous Slide*
  marks what differs from the previous slide (engine `diff`, both slides loaded like page sessions): added units green,
  changed ones orange, removed ones as a dashed grey frame where they were, plus a legend at the top right; the marks
  are plain shapes named `ISK_DIFF …`, and running the command again removes them (one undo step each). LibreOffice
  renames a duplicated shape whose name ends in digits ("…eb12" → "…eb 1"); reading a unit restores the name from its
  attributes, because in .pptx the name carries the unit id. Checked: `integration_test.py` (1 added, 1 changed,
  1 removed, an unchanged gear unmarked, names restored; slide exported as `compare.png`).
- **Compatibility check** (2026-11-25, Impress and Draw): a dialog with the office (DPMA, EPO, PCT, USPTO or the common
  safe profile) and the findings of every page with InventaSketch units (engine `preflight`, each page loaded like a
  page session, so native moves count) – errors first, as "Slide 2 · Error · message"; choosing a finding (arrow keys or
  click) shows its suggestion and cited source and goes to its slide and unit. The disclaimer is always shown. Without
  a visible window (headless, tests) the result is logged. Checked: `integration_test.py` (a gear outside the usable
  area on slide 2 found and resolved to its unit) and `ui_smoke.py` (the dialog opens in a real window, the Down key
  goes to slide 2 and selects the gear, Escape closes it).
- **Sidebar deck "InventaSketch"**, four panels (see the UI/UX round and the interaction concept round below):
  - *Selection* follows the document selection (selection listener) and shows what can be done with the selected
    InventaSketch unit(s), context only: every choice as a dropdown, the element's own commands, select connected,
    insert here, build "+", turning.
  - *Values* (2026-11-26): the values of the selected element with their formulas, the value level.
  - *Library*: one search field for the whole library (engine `search`), the category list (first entry
    *Suggestions*: the parts that fit the selection, then the recent items), the item list with preview (the engine's
    SVG thumbnail); double-click or *Insert* inserts the item – a part that fits the selected unit where it fits,
    anything else in the middle of the slide/page (Calc: of the visible cell area).
  - *Drawing, calculator and code* (2026-11-26): the page's colour by medium with *Insert legend* and *Calculation
    sheet …* (see the drawing-wide settings round below), the calculator with units and the code import (matplotlib,
    pgfplots, TikZ).
- **On the slide/page** (Impress, Draw; see the on-canvas round below): a unit dragged with the mouse snaps on
  release (gear mesh, axes, ports, mounts …) and its coupled parts follow; Ctrl+Space opens the Library panel with the
  cursor in the search field (type, Enter inserts); the context menu of a unit starts with the eight places of the
  pie menu (2026-11-26); Ctrl+double-click selects what is connected; selected units glow softly.
- **Scripting API** (DispatchHelper / macros), all `inventasketch.libreoffice:<command>`:
  `insert?item=<id>[&x=<mm>&y=<mm>]`, `insertFitting?item=<id>`, `insertSearch?text=<query>`, `library`, `editModel`,
  `setView?view=<id>`, `redraw`, `numeral`, `transform?kind=mirrorH|mirrorV|rotate90`, `explode`, `drawTrace`,
  `drawGhosts`, `build?port=<port id>&item=<id>`, `turn?degrees=<angle>`, `arrange?action=<id of arrangeOptions>`,
  `assembly`, `panel?control=<key>&text=<text>` / `panel?control=<key>&item=<entry>` / `panel?press=<key, label or
  action:<id>>` (operates a control of the open panel the way a user does), `snapMove[?px=<pixels>&undo=<n>]` (snap
  the selected unit(s) after a native move, as after a mouse drag), `quickInsert[?text=<text>]` (Library panel with
  the cursor in the search field, as Ctrl+Space), `patentCheck[?profile=<office id>]`, `compare`, `importVector[?path=<file>]` (file dialog without a path),
  `cleanSlides[?scope=selection|slide|all&mode=normalised|randomised|none&rename=1&texts=1&meta=0&seed=<n>]`
  (dialog without arguments), `export[?format=<id or extension>&path=<file>]` (dialog and save dialog for what is
  missing; headless without arguments the formats are logged), `chartFromPicture[?path=<file>]`, `cleanup`, `about`;
  since 2026-11-26 (interaction concept): `action?id=<engine action id>[&input=<text>|&ask=1]` (an engine action or
  an option of a choice), `param?key=<k>&value=<v>` (a parameter choice), `text[?text=<text>]`, `duplicate`,
  `delete`, `connected[?mode=auto|net|coupled|chain|loop|walls]`, `insertPlace?key=<k>`, `menu` (logs the context
  menu of the selection, also as JSON), `values`, `setValue?key=<k>&text=<t>`, `clearValue?key=<k>`,
  `valueLevel?level=quick|explore|engineer`, `showValues?on=0|1`, `tools`, `calc?text=<t>[&unit=<u>]`,
  `importCode[?path=<file>|&text=<code>]`; since 2026-11-26 (drawing-wide settings): `colorBy?mode=medium|off`,
  `legend`, `sameName?key=<designation>&page=<n>`, `muteCheck?key=<check id>[&mute=0]` (`mute=0` without a key
  unmutes all), `unitPref?kind=<quantity kind>&unit=<unit>`, `calcSheet[?mode=csv|markdown|latex|table&path=<file>&
  copy=1]` (dialog without arguments, logged when headless); `panel?…&enter=1` presses Enter in a field, `control=choice:<label or
  key>` names a choice's dropdown. The integration and UI smoke tests use these commands.
- **Recent items** (last 12 inserted library items, per user) are kept in `libreoffice-settings.json` next to the log
  and rank higher in the search.
- UI language: German if the LibreOffice UI locale starts with `de`, otherwise English; library names come from the
  engine catalog in the same language.
- Log: `%LOCALAPPDATA%\InventaSketch\libreoffice.log` and `engine-stderr.log` (macOS `~/Library/Logs/InventaSketch`,
  Linux `~/.local/state/InventaSketch`).

## Rendering (draw plan → UNO)

| Plan op | LibreOffice shape |
|---|---|
| `rect` (radius, rotation) | `RectangleShape` (`CornerRadius`, `RotateAngle`) |
| `ellipse` | `EllipseShape` |
| `line` | `LineShape` |
| `polyline` | `PolyLineShape` (open) / `PolyPolygonShape` (closed) |
| `freeform` (M/L/C/Z, sub-paths) | `OpenBezierShape` / `ClosedBezierShape` (`PolyPolygonBezier`) |
| `shape` (triangle, diamond) | `PolyPolygonShape` with the rotated points (only used without freeform support) |
| `text` | `CustomShape` rectangle without line/fill, no word wrap, no auto-grow, centred vertically |
| `group` | `GroupShape` added first and then filled (single children are not wrapped) |
| `svg` | `GraphicObjectShape` with the SVG graphic (fallback; not produced by the LibreOffice preset for the tested items) |

- Stroke: `LineColor`, `LineWidth`, `LineJoint` round; dashes as absolute `LineDash` (style RECT) with the engine's
  dash pattern (`Style.DashArray`: multiples of the line weight, minimum 0.18 mm); round caps for solid lines.
- Arrowheads: native line-end markers with the engine's head geometry (triangle, stealth, diamond, oval, open;
  half → triangle, tick → open as in PowerPoint COM) and the size the engine assumes (`NativeHeadLength`). The markers
  are registered in the document's marker table under OOXML-style names (`msArrowEnd 9 isk`, `msArrowStealthEnd 5 isk`
  …), so the .pptx export writes the matching PowerPoint arrow type and size. Hollow UML heads and crow's feet are
  drawn as shapes by the engine (no native equivalent).
- Fills: solid `FillColor`, opacity as `FillTransparence`/`LineTransparence`, native `FillHatch` (single/cross,
  distance, angle counter-clockwise in 1/10°, optional background colour via `FillBackground`).

## Model storage (ADR-0006)

- Unit shape (group or single shape) named `ISK:<type>:<id>`.
- `UserDefinedAttributes` (namespace `urn:inventasketch:shape:1`): `isk:id`, `isk:type`, `isk:model` (compact model
  `isk1:`), `isk:bounds` (host rectangle in pt and `RotateAngle` after drawing). LibreOffice groups have no
  `UserDefinedAttributes`, so for groups they sit on the group's first leaf shape.
- Document mirror: user-defined document properties `ISK_MODEL_<id>` (chunked `_1`, `_2` … at 60 000 characters) and
  `ISK_BOUNDS_<id>`. Reading prefers the attributes and falls back to the document properties.
- Editing (view switch, redraw, numeral and every operation of the selection panel) runs in a page session (see
  Architecture): stored models imported, a native move/resize (`hostTransform` from the stored to the current
  rectangle) or a native rotation (about the centre) applied, the engine operation run and the touched units replaced at
  their z-positions in one undo step.

## UI/UX round 2026-11-24 (LibreOffice, 2026-11-25)

Follows the UX rules in the roadmap ("Plan 2026-11-24") as far as a sidebar can: LibreOffice has no on-slide overlay
yet, so everything lives in the deck. All decisions come from the engine; pure panel rules in `ux.py`
(`tests/test_ux.py`).

- **One insert flow (Library panel):** typing searches the whole library with the engine's `search` (both languages,
  umlauts folded – "stoessel" finds "Stößel" –, recent items and the selection's view ranked higher); with a unit
  selected the parts that fit it come first and are marked "– fits"/"– passt". Without typing, the *Suggestions*
  entry lists the fitting parts, then the recent items. *Insert* places a fitting part where it fits
  (`insertFitting`: gear on the shaft, gear in mesh, bearing beside a gear), coupled; if it fits nowhere, or for any
  other item, it goes to the middle of the page. A chosen category still filters the hits.
- **Selection panel, one unit (context only, fixed order):** name and id, *F = n (links, joints)* for a link of a
  self-built linkage (`dof`); the view list only with more than one view; then buttons that appear only when they do
  something: *Draw path* (coupler curve, `drawTrace`) and *Ghost positions* (`drawGhosts`) for F = 1, *Select
  assembly* when coupled parts are not selected yet (`assembly`), *Explode* for explodable assemblies
  (`dof.explodable`), *Mirror ↔*, *Mirror ↕*, *Rotate 90°* only when `transformEffect` says they change the drawing
  (a gear in axial view shows none). *Build on (+)*: the build ports of the unit as a list (`buildPorts`, "1: Mesh,
  right"), the parts that dock at the chosen port (`buildItems`) and *Build* (`buildPlace`, docked and coupled). A
  gear coupled with others gets *Turn the gear train by (°)* with a typed angle (`motionTurn`, the whole train with its
  ratios). *Redraw from model* as before.
- **Selection panel, two or more units:** the engine's selection-wide operations from `arrangeOptions` with their
  labels – one view for all (the mechanism kept), centres on a row or column, even spacing, like the first, dimension
  between the two, renumber reference numerals –, each only when it changes something, plus *Select assembly*. A click
  runs `arrange` on all selected units in one page session and one undo step.
- **Labels:** the extension has no icon set yet, so the buttons carry short bilingual text labels with the COM
  add-in's wording as tooltip; the engine's labels are used for the selection-wide operations (one per row).
- **Not in LibreOffice (needs the planned overlay):** snap guides and snap labels *while* dragging, the live coupling
  preview, build "+" on the canvas and stretching a shaft end by dragging, motion mode by dragging (gear trains turn by
  a typed angle instead; linkages cannot be dragged), "make it …" after a quick add and the pie menu. Snapping on
  release, Ctrl+Space and a context menu followed in the on-canvas round below.

## On-canvas round (LibreOffice, 2026-11-25)

LibreOffice's UNO API gives an extension no way to draw into the document view (no overlay for handles, "+" buttons
or snap guides, no live preview while LibreOffice drags a shape). It does let an extension watch the mouse and keyboard
of the view and extend its context menu (`XUserInputInterception`, `XContextMenuInterception` on the controller). The
extension uses exactly that (`canvas.py`, pure rules in `ux.py`); every decision still comes from the engine.

- **Attaching:** the job `CanvasJob` (`Jobs.xcu`, document event `OnViewCreated`) attaches a mouse click handler, a
  key handler and a context menu interceptor to every Impress/Draw view; commands and sidebar panels attach again if a
  view was opened before the extension (idempotent per controller). Calc keeps the menu and the sidebar only (Ctrl+Space
  selects a column there, shapes are anchored to cells).
- **Snapping after a drag (like PowerPoint):** LibreOffice moves the shape itself; a left-button release after the
  mouse moved posts the command `snapMove` to the main thread, which runs after LibreOffice has applied the move. If
  every selected unit moved by the same vector (no resize, no rotation: `ux.drag_delta`), the page session imports the
  units with the dragged ones at their stored position, runs the engine's `snapMove` with the drag vector (tolerance
  8 screen pixels at the zoom of the drag, derived from the pixels the mouse moved, as in the other hosts) and applies
  the snapped vector with `move` *with* coupling: a gear lands in mesh, on an axis, at a port or mount; parts on a
  dragged shaft and docked lines follow. The touched units are redrawn. LibreOffice's own move action (exactly one new
  undo action since the mouse press) is taken back first, so drag + snap is **one** undo step ("InventaSketch: Move and
  snap") that returns the unit to where it was before the drag. Ctrl (copy) and Alt (free move, as in the other hosts)
  skip the snap; resizes, rotations, shapes inside an entered group and text edit never snap. Errors after a drag are
  logged, not shown.
- **Ctrl+Space:** opens the InventaSketch deck (sidebar API: `setVisible`, `showDecks`, `XDeck.activate`, because the
  dispatch `.uno:SidebarDeck.<deck>` toggles and would close an open deck) and puts the cursor into the Library
  panel's search field with its text selected; if the panel is not created yet, it takes the request when it is
  filled. Typing searches as before; **Enter** inserts the marked hit (a fitting part where it fits the selection) and
  returns the focus to the document, **Escape** only returns. In text edit Ctrl+Space stays with the text (text edit is
  recognised by the state of `.uno:InsertSoftHyphen`, which LibreOffice enables only then; the selection does not
  change in text edit).
- **Context menu:** right-clicking InventaSketch unit(s) puts the selection panel's actions at the top of LibreOffice's
  menu, followed by a separator: *Switch view ▸* (the other views), *Draw path*, *Ghost positions*, *Select assembly*,
  *Explode*, *Mirror horizontally/vertically*, *Rotate 90 degrees* – each only when the engine says it applies
  (`host.describe`, `ux.context_entries`) –, *Redraw from model* and *More in the InventaSketch sidebar…* (build "+",
  turning). Several units: the engine's selection-wide operations and *Select assembly*. The entries are ordinary
  dispatch commands of the extension. Building the menu runs one page session (all units of the page are imported).
  Since 2026-11-26 the block is the pie menu's eight places (see the interaction concept round below).
- **Not possible without an overlay (documented, not attempted):** guides and the snap label while dragging, the live
  coupling preview, "+" handles on the canvas, stretching a shaft end and motion mode by dragging, the pie menu.
  Expanding a collapsed Library panel on Ctrl+Space was dropped: `XDeck.getPanels()`/`XPanel.expand` crashed soffice
  26.8 once the deck was shown, so a Library panel the user collapsed stays collapsed.

## Interaction concept round (LibreOffice, 2026-11-26)

The concept of [interaction-ux.md](../design/interaction-ux.md) (sections 5a, 6, 6a) and the value UI of
[engineering-calculations.md](../design/engineering-calculations.md) (sections 10–11), as far as LibreOffice lets an
extension go. The Office web add-in's implementation (`barlogic.ts`, `pielogic.ts`, `valuecard.ts`, `tools.ts`) was the
second reference. Every decision still comes from the engine; the pure rules are in `choices.py` and `values.py`
(`tests/test_concept.py`).

- **Choices are dropdowns.** The Selection panel asks the engine for the element's actions (`contextActions`) and
  parameters (`inspector`) and shows **one dropdown per choice**: first the parameter choices (not the advanced ones),
  then the engine's action choices (`choice`, `choiceLabel`, `choiceMenu`) whose parameter is not already a parameter
  choice (`paramKeys`, one place per thing). The closed dropdown shows the current option, the list every option;
  choosing an option runs it (`setParam`, or `applyAction` with the option's id; an option that asks for a value
  opens the input dialog first), one undo step. Menus (`choiceMenu`: "Fill to", "Add area") are dropdowns whose first
  row is "Label ▸"; choosing a row runs it and the title comes back. No cycle buttons anywhere. The element's other
  commands are buttons with the engine's labels (a switch that is on, such as a table's header row, is a pressed
  toggle button); "Text …" edits the main text (`setMainText`). The option pictograms (`IconIds`) and pictures
  (`previewSvg`) of the other hosts are not shown: the extension has no pictogram set, so options are named; no element
  choice in the catalogue carries a `previewSvg` today (those are part choices of charts).
- **The eight places as the context menu.** Right-clicking InventaSketch unit(s) puts the pie menu's eight places on
  top of LibreOffice's own menu, in the same order and with the same words as COM and web: *Text …*, *Edit*,
  *Insert …*, *Duplicate*, *Style*, *Delete*, *View*, *Arrange* (LibreOffice draws the submenu arrow, so the labels carry
  no "▸"). Edit: *Values …* (opens the Values panel), the element's own commands, *Reference numeral*, the menu
  choices, *Explode*, *F = n* (shown, inactive), *Draw path*, *Ghost positions*, *Parts …*, *More in the InventaSketch
  sidebar…* (build "+", turning, insert here), *Redraw from model*. Style: one submenu per state choice, "Line style:
  Dashed", the current option **checked**. View: the views, the current one checked (several units: "one view for
  all"). Arrange: rotate 90° either way and mirror (only where `transformEffect` says they change the drawing), the
  host's own *Bring to front* / *Send to back* (`.uno:BringToFront`, `.uno:SendToBack`: the unit's z-order is its
  shape's), *Select connected* (submenu) and, for several units, the engine's align/distribute/dimension operations.
  Insert opens the Library panel with the search field (as Ctrl+Space); Duplicate and Delete go through the engine
  (`duplicate`, `delete`: a copy gets new ids, what depends on a deleted element follows), one undo step each. A place
  with one entry shows it directly, Edit, Style and Arrange keep their name; more than 14 entries continue in *More*.
  The check marks and the inactive state are LibreOffice's own: the protocol handler reports the status (`State`,
  `IsEnabled`) of the entry's command. **LibreOffice hides inactive context-menu entries** by default (*Tools ▸ Options
  ▸ LibreOffice ▸ View ▸ Inactive menu items* shows them greyed), so a place with nothing for the selection (Text … for
  a gear) is left out and the places below move up one row – the order never changes.
- **Values panel.** One row per value of the selected element (`values`): marker (✎ entered, = derived, ○ default,
  ⇄ linked, ⚠ in conflict), symbol and name, the entry field (the engine's parser: "4k7", "230 V", "1,5 kW"; Enter or
  leaving the field stores it with `setValue`, Escape restores), the unit, × for an entered value (`clearValue`); under
  a derived value its formula step by step (`explain`, Explore and Engineer; Engineer adds the source and a fourth
  significant digit; the panel and engineer values are listed in Engineer only). The status line says ✓, what is
  missing or that entries conflict; conflicts and violated checks (`checks`) follow. A refused entry shows the engine's
  message in red under its row, never a message box. *Show values on the drawing* switches the values label
  (`showValues`), *Calculator …* puts the first value into the calculator. The **value level** (Quick sketch, Explore,
  Engineer) is a document setting: the user-defined document property `ISK_VALUES` (like the COM add-in's presentation
  tag; Quick sketch removes it), so it travels with .odp and .pptx. The panel follows the selection; all units of the
  page take part in the solve, so values linked across elements are known.
- **Calculator and code.** The calculator with units (`calc`, the result converted with `convertUnit` when a target
  unit is typed): "230 V * 16 A" = 3,68 kW, "1500 rpm" in rad/s = 157,1 rad/s, with the engine's steps; Enter or
  *Calculate*. The code import pastes or opens matplotlib, pgfplots or TikZ code (`importCode`: read, never run): plots
  become charts, drawings shapes, in the middle of the slide/page, one undo step, selected; the report (what was
  imported, every skipped line with its reason) is shown below. The same import is in the menu (file dialog).
- **Select connected.** Arrange ▸ *Select connected* lists the kinds that apply (`connectOptions`: net, moves along,
  following elements, closed loop, room walls) with the number of elements; choosing one selects those units
  (`connected`). The Selection panel has the same list ("Select connected ▸"), which replaces "Select assembly" when the
  engine offers kinds. **Ctrl+double-click** on a selected unit selects the most meaningful kind (mode `auto`); the
  press and the release are consumed, so LibreOffice does not also enter the group or add a shape inside it.
- **Selection look.** Selected units glow: LibreOffice has no overlay, but its shapes have a native glow effect
  (`GlowEffectRadius`, `GlowEffectColor`, `GlowEffectTransparency`). Every leaf shape of a selected unit gets a calm blue
  glow (1.4 mm, 55 % transparent); **ellipses ignore the glow effect** (LibreOffice 26.8 reads it back as 0), so they
  get the same look as a blurred shadow without offset (`ShadowBlur`). The glow is removed when the selection changes,
  before every InventaSketch command (so LibreOffice's undo never keeps a glowing shape) and before the document is
  saved, saved as a copy, exported or printed (document events `OnSave`, `OnSaveAs`, `OnCopyTo`, `OnPrint`; storeToURL
  sends `OnCopyTo`, verified), and comes back afterwards. Setting it records no undo action and the document's modified
  flag is restored. Unlike the concept, the glow lies behind each shape at its own z-position (an element on top hides
  it), there is no fainter preview for a hovered "Select connected" entry (LibreOffice's context menu reports no
  hover), and a unit pasted from the clipboard while it glowed keeps the glow until it has been selected once.
- **Insert places.** The "+" dots between a table's rows and columns (Core `IInsertHandles`) cannot be drawn on the
  slide; the Selection panel lists them instead: *Insert here* with "Insert a row here (before row 3)" … and an
  *Insert* button (engine `insertHandles` / `applyInsertHandle`, added for this host), one undo step. They are not in
  the context menu (a table with ten rows would list nine entries).
- **Not in LibreOffice:** drag grips on values (`dragHandles` / `moveHandle`: a chart's limit lines, dimension values,
  robot joints) – LibreOffice reports mouse presses and releases to an extension but no motion and draws no handles; a
  typed value replaces them where the element has one (Values panel, parts dialog). No floating bar (no overlay), no
  hover preview, no option pictograms (see above).

## Drawing-wide settings round (LibreOffice, 2026-11-26)

The UI for the engine features of [interaction-ux.md](../design/interaction-ux.md) section 6b (colour by medium, names
that belong together) and [engineering-calculations.md](../design/engineering-calculations.md) section 10.11 (Calc 6:
check settings, preferred units, calculation sheet), with the words of the COM add-in. Pure rules in `drawing.py`
(`tests/test_drawing.py`); every decision comes from the engine.

- **Colour by medium.** *Drawing, calculator and code ▸ Colour by medium* (dropdown *Off (own colours)* / *By medium*)
  and the menu *Colour by Medium ▸ Off / By Medium*: the page session runs `setColorBy` and every unit of the slide/page
  is redrawn in one undo step ("InventaSketch: Colour by medium"); units drawn in another mode than the session adopted
  are redrawn too. The mode lives in the units' stored models (the engine adopts it from every imported unit, as the
  COM add-in's slide style does), so a page shows the mode of its units and the dropdown reads it from a unit's stored
  model without the engine; a unit inserted on a coloured page takes the mode (`insert` sets it before drawing).
  *Insert legend* (panel and menu) inserts the colours used on the page as one unit at the top right (new engine
  method `insertLegend`: `LineMedia.Used` and `LineMedia.Legend`, grouped; one undo step, selected). LibreOffice text
  boxes do not wrap, so the engine moves the legend left until its longest name, estimated at 0.6 em per character,
  ends inside the page. Without a coloured line on the page the engine's message is shown.
- **Designations across pages (Impress, Draw).** Every page session asks the engine for the page's designations
  (`designations`); if there are any, the designations of the other pages' units are read from their stored models
  (new engine method `designationsOf`: the designations of each compact model without a session; cached per model in
  the extension) and passed to `setContinuations` with the page numbers and "slide"/"page", so lines whose designation
  also occurs on another page draw "→ W3, slide 2" ("→ W3, Folie 2") whenever they are drawn. Markers of other pages
  are refreshed when their units are drawn again (as in COM). The designation actions themselves (*Designation …*,
  *Value label …*, *End markers*, *Conductor ▸*, *Line role ▸*, *Fluid (DIN 2403) ▸*) are the engine's generic actions
  `line.ref`, `line.value`, `line.ref.ends`, `elem.ref`, `line.role` and already appear as buttons and dropdowns of the
  Selection panel and in the Edit and Style places.
- **Same name.** *Select connected ▸ Same name* (engine mode `samename`) is offered with the other kinds where the
  engine offers it. For each other page that has the selection's designation the list (Selection panel) and the
  Arrange ▸ Select connected submenu add "Same name on slide 2: W3"; choosing it shows that slide and selects the units
  with that designation there (command `sameName`). This replaces the COM add-in's report "Same name: also on slides
  2, 4" with an entry that goes there.
- **Plausibility warnings in the Values panel.** Up to four rows "⚠ message (rule) [source]" (the engine's `checks` of
  the element, as in the COM value card), each with *Mute*; more warnings are listed below without a button. *Mute*
  stores the check id in the user-defined document property `ISK_MUTED` (ids separated by ";", the format of the COM
  add-in's presentation tag); *Unmute n muted check(s)* clears it.
- **Show values in.** Dropdowns for pressure (bar, MPa, kPa), speed of rotation (min⁻¹, rev/s, rad/s), energy (kWh,
  MJ, J) and power (W, kW); the first unit is the default and is stored as "not set". The choice is checked by the
  engine (`setUnitPreference`), stored in the document property `ISK_UNITS` ("pressure=MPa;power=kW"), and the units
  of the page whose drawing changes with it (a values label on the drawing: two draw plans compared) are redrawn in one
  undo step ("InventaSketch: Show values in"). The COM add-in offers "s⁻¹" for the speed of rotation, which the engine
  does not know; LibreOffice offers the engine's "rev/s".
- **Document settings.** `ISK_MUTED` and `ISK_UNITS` are applied to every page session (`muteCheck`,
  `setUnitPreference` after the units are imported), like the value level `ISK_VALUES` they are document properties
  and travel with .odp and .pptx (checked: both survive saving and reloading).
- **Calculation sheet.** *Calculation sheet …* (Tools panel and menu) opens a dialog with the sheet of the selected
  elements, else of every element of the slide/page (Calc: of the sheet's elements): format *CSV (spreadsheet)*,
  *Markdown* or *LaTeX (booktabs)* (engine `calcSheet`, digits by the value level), the text (read-only), the number
  of rows and sources, *Copy* (system clipboard, plain text), *Save …* (LibreOffice's save dialog with the format's
  extension; CSV with a byte order mark) and *Insert as table* (engine `insertCalcTable`: an annotation table beside
  the elements, one undo step "InventaSketch: Calculation sheet", selected).

## Verified features

Verified on Windows 11 with LibreOffice 26.8.2.2 on 2026-11-20 (`tests/integration_test.py`, 43 checks passed;
`tests/ui_smoke.py`, screenshots reviewed):

- `unopkg add` of the portable package into a fresh profile; engine started with the installed .NET 10 runtime. The
  self-contained `win-x64` package (32.9 MB) passed the same test with its bundled engine.
- Impress: spur gear (teeth), gear in section (native `FillHatch`), arrow with native line end, reference numeral with
  leader arrow (open Bézier with native start head), wall (hatch on background colour), UML class (text), UML
  generalisation (hollow head as shapes), cross-hatched rectangle, curved arrow (`OpenBezierShape`) — all native,
  grouped, one unit per insertion; reference numeral added to the arrow (new label unit); view switch of the gear
  (teeth → side view) keeps the unit name and z-position; native move and native 90° rotation are taken into the
  model on redraw; slide exported as PNG and reviewed.
- Saved as .odp and .pptx and reloaded: unit names survive both; `UserDefinedAttributes` survive .odp and are lost
  in .pptx (expected); the document properties survive both; a unit reloaded from each format re-renders from its
  stored model (UML class full → compact). The .pptx contains native `a:headEnd`/`a:tailEnd` of type `triangle`, the
  unit names and `ISK_MODEL_*` custom properties.
- Draw and Calc: insert (Calc: middle of the visible cell area on the sheet's draw page) and view switch.
- Sidebar (Impress, German UI, 200 % display scaling): deck opens from the *Edit model* command, library list with 681
  elements, preview, selected unit with its views; control heights follow the UI font. Typing into the search field
  and clicking *Insert* were not driven by the smoke test (filter unit-tested; *Insert* runs the same `insert` command).

- 2026-11-25 (UI/UX round, Windows 11, LibreOffice 26.8.2.2, German UI): `integration_test.py` 61 checks passed,
  among them search in the other language ("welle") and with umlauts ("Stößel"), a gear placed on a selected shaft
  (`insertFitting`), typing "lager" with the shaft selected inserts the fitting bearing, recent items stored, a meshing
  gear built at the port `mesh.right`, the train turned by 30° (both gears redrawn), select assembly (2 units), two
  separate gears centred on a row in one undo step (undo and redo leave exactly the unit shapes), one view for all,
  a four-bar exploded into 5 parts, coupler curve and ghost positions of a link, an arrow rotated by 90°, undo of an
  insert removes the whole unit. `ui_smoke.py` 8 checks passed with the deck open: view switch from the panel's view
  list, typing "welle" into the search field and *Insert*, typing "lager" with the shaft selected inserts the bearing
  where it fits, the panel following a multi-selection made in the document and its *centres on one horizontal line*,
  *Build* from the port list and *Turn* with a typed angle; screenshots reviewed.

- 2026-11-25 (on-canvas round, Windows 11, LibreOffice 26.8.2.2, German UI, 200 % display scaling):
  `integration_test.py` 70 checks passed, new: the job attaches the handlers to a new view before any command, a gear
  dropped 1.3 mm beside the meshing position of another unit snaps into mesh (centre distance 25.20 mm) in one undo
  step with the selection kept, a shaft moved by 20 mm takes the gear on it along (coupling), a native resize does not
  snap. `ui_smoke.py` 19 checks passed (twice) with **real mouse and keyboard input** (Windows `SendInput`): a gear
  dragged with the mouse to 2.5 mm beside the meshing position snaps into mesh on release (25.20 mm, tolerance 4.99 mm
  at 21 % zoom), one undo returns it to its start and redo snaps it again; right-click on the gear shows *Switch view*,
  *Select assembly*, *Redraw from model*, *More in the sidebar* on top of LibreOffice's menu and Down, Right, Enter
  switches its view; Ctrl+Space, typing "welle" and Enter inserts a shaft, Ctrl+Space again with the shaft selected,
  "lager", Enter inserts the fitting bearing; in text edit Ctrl+Space and typing stay in the text; the 8 sidebar checks
  of the UI/UX round still pass. Screenshots reviewed.

- 2026-11-25 (sharing round, Windows 11, LibreOffice 26.8.2.2, headless, German UI): `integration_test.py --only
  share` 16 checks passed: *Clean slides* over two slides (randomised, seed 42, texts replaced) changed both chart units
  and left the arrow, kept the unit names, was one undo step ("InventaSketch: Folien bereinigen") and undo restored
  every stored model; with only the chart selected only the chart changed. *Export*: TikZ and matplotlib of the
  selected chart (without the arrow), the editable SVG of the slide, the formats offered for the page logged. *Chart
  from picture*: a PNG with five columns on a transparent background (black underneath) became a `chart.bar` unit
  (mask used), one undo step; slide exported as `share.png` and reviewed. The two dialogs were built over the UNO bridge
  in both languages (every control property accepted); they were not shown in a visible window.

- 2026-11-26 (interaction concept round, Windows 11, LibreOffice 26.8.2.2, German UI, 200 % display scaling):
  `integration_test.py` 118 checks passed (all phases; `--only concept` 24 checks): the context menu of a resistor has
  the eight places in order with *Values …* under Edit and the current view checked; a table's Style place has the
  borders choice with the current option checked and another option redraws the table in one undo step; value level
  Explore stored as `ISK_VALUES` (Quick sketch removes it), U = 230 V and I = 2 A give R = 115 Ω with "R = U/I = 230 V/2,00
  A = 115 Ω" in one undo step each, clearing I leaves R open; duplicate gives a new id, delete removes it; "insert a row
  here" changes the table in one undo step; two meshing gears offer "Moves along (2 elements)" and `connected` selects
  both; the calculator gives 3,68 kW and 157,1 rad/s; a matplotlib file becomes a `chart.figure` unit ("Import code"
  undo step); the selected gear glows on all 6 shapes (2 lines by glow, 4 ellipses by the blurred shadow) without
  making the document modified, the copy saved with storeToURL has no glow, the glow comes back after saving and goes
  when deselected (page exported as `concept-glow.png` and reviewed). `ui_smoke.py` 33 checks passed (all phases; the
  earlier context-menu check now walks to the View place; `--concept-only` alone 12 checks) with **real mouse and
  keyboard input**: the glow after selecting, right-click on a gear shows the eight places (Text …
  hidden as inactive) on top of LibreOffice's menu (screenshot reviewed: native submenu arrows, the order/icons/shortcuts
  of *Bring to front*), Down to View, Right shows the views with the current one checked, Down + Enter switches the view;
  Ctrl+double-click selects exactly the two meshing gears; in the sidebar the borders dropdown switches to "outer frame
  only", *Insert here* adds a row, the Values panel takes "230 V" and "2 A" typed into its fields and shows R and P with
  their formulas, "2 kg" for U is refused with the engine's message in red under the row, the calculator gives 3,68 kW.
  Screenshots reviewed (the deck is scrolled with the mouse wheel to the Values panel; the Calculator panel below the
  Library panel was operated but not brought into a screenshot).

- 2026-11-26 (drawing-wide settings round, Windows 11, LibreOffice 26.8.2.2, German UI): `integration_test.py --only
  drawing` 22 checks passed: a wire with *Conductor ▸ L1* and the designation W3 turns brown (#8d5524) with colour by
  medium in one undo step ("Farben nach Medium"), a wire inserted afterwards takes the mode, the legend is one unit with
  "Außenleiter L1 (IEC 60445)", *Off* restores the own colours; the same designation on slide 2 draws "→ W3, Folie 2"
  on slide 1 and the context menu's "Gleicher Name auf Folie 2: W3" shows slide 2 with that wire selected; a resistor
  with 529 W at 0.25 W shows `elec.r.power`, muting stores `ISK_MUTED` and the warning goes, unmuting brings it back;
  power in kW stores `ISK_UNITS`, redraws the resistor's values label ("P = 0,529 kW") in one undo step and leaves
  everything else alone (speed of rotation: 0 units redrawn); the calculation sheet as CSV file (byte order mark, kW),
  Markdown (copied to the clipboard and read back), LaTeX (`\toprule`) and as an annotation table (one undo step);
  `ISK_MUTED` and `ISK_UNITS` survive saving and reloading as .odp and .pptx (warning still muted, power still in kW).
  Pages exported as `drawing-medium.png` and `drawing.png` and reviewed (legend inside the page after the engine moved
  it left).

## Tests

```powershell
python -m unittest discover -s hosts/libreoffice/tests -p "test_*.py"     # unit tests, no LibreOffice
"C:\Program Files\LibreOffice\program\python.exe" hosts/libreoffice/tests/integration_test.py --oxt <oxt> --out <folder> --only concept
"C:\Program Files\LibreOffice\program\python.exe" hosts/libreoffice/tests/ui_smoke.py --oxt <oxt> --out <folder> --concept-only
"C:\Program Files\LibreOffice\program\python.exe" hosts/libreoffice/tests/integration_test.py --oxt <oxt> --out <folder> --only drawing
"C:\Program Files\LibreOffice\program\python.exe" hosts/libreoffice/tests/ui_smoke.py --oxt <oxt> --out <folder> --drawing-only
"C:\Program Files\LibreOffice\program\python.exe" hosts/libreoffice/tests/integration_test.py --oxt dist/InventaSketch-LibreOffice-0.1.0.oxt --out <folder>
"C:\Program Files\LibreOffice\program\python.exe" hosts/libreoffice/tests/ui_smoke.py --oxt dist/InventaSketch-LibreOffice-0.1.0.oxt --out <folder>
```

Unit tests cover the plan-to-UNO mapping, metadata, library filter, texts, .NET discovery, the protocol (with a
stand-in engine: errors, crash, timeout, foreign lines), main-thread marshalling, the selection panel rules and the page
session bookkeeping (`test_ux.py`) and, if the engine is built, real draw plans of library items and the engine flows
behind the panel with payloads drawn in separate sessions (search with fitting parts, insert fitting, arrange across
units, a gear train turned across units, explode). `test_share.py` covers clean slides (choices, seed, engine request,
pages of a scope, report), export (formats, names, extensions, bytes), chart from picture (DIB decoding of 1/8/16/24/32
bits, scaling, transparency on white) and the rule that no user-visible text says "patent", plus the engine requests of
these modules against the built engine. `integration_test.py --only share` runs just the phase for these three
commands. `test_concept.py` covers the interaction concept: grouping of choices (parameter choices first, covered
action choices left out, menus), dropdown rows and picks, the eight places and their submenus (checked current options,
greyed places, "More"), the Values rows per level, status and conflicts, the calculator and code report lines, the
glow (signature, shadow fallback for ellipses, modified flag) and, against the built engine, that every choice of a
sample of the catalogue is a proper dropdown, an option runs through `applyAction`, a resistor's values, a table's
insert places, select connected of meshing gears, the calculator and a matplotlib import. `integration_test.py --only
concept` and `ui_smoke.py --concept-only` run the LibreOffice checks of this round. `test_drawing.py` covers the
drawing-wide settings: colour modes and the mode in a stored model, changed plan units, the continuation map and the
"Same name" rows, muted checks and preferred units (parse, update, the default clears, the engine calls of a page
session), the check rows, sheet formats and names and, against the built engine, the colour mode travelling with a
stored model and the legend, designations of stored models as continuation markers, the document settings applied to
a fresh session (every offered unit accepted by the engine) and the calculation sheet in every format and as a table.
`integration_test.py --only drawing` and `ui_smoke.py --drawing-only` run the LibreOffice checks of that round. The UI smoke test operates the panel's controls with the `panel`
command (typing into the search field and choosing a list entry go through the controls and call the same listeners
as a user; buttons run their click handler); real mouse clicks are not simulated, because over the remote bridge the
panel's control peers are generic windows without list box or accessibility interfaces. Its first part
(`canvas_phase`, Windows) does send real mouse and keyboard input to the document view: a test mouse handler over the
bridge reports window pixels, so a click gives the view's origin on the screen and a horizontal drag of a helper
rectangle the scale; page coordinates then map through the controller's `VisibleArea`. Before every click, drag or key
the test checks that its own LibreOffice window is in front and under the pointer, and stops without sending input
otherwise (`--skip-canvas` leaves this part out, `--canvas-only` runs only it). The integration test installs the
package into a temporary profile, runs soffice
headless and never touches the user's profile or files; Linux/macOS run it with a Python that can import `uno`.

## Limits (phase 1)

- No overlay on the canvas (UNO cannot draw into the view): snapping happens on release, without guides or a snap
  label while dragging; no "+" handles, floating bar, drag grips on values or motion by dragging; the pie menu's eight
  places are the top of the context menu. Editing otherwise goes through the sidebar, the context menu and the menu.
  Choice parameters, the element's commands, the main text and values are exposed (2026-11-26); free number and text
  parameters and the line/fill style are not (no inspector yet).
- Every edit imports all InventaSketch units of the page (one engine call per unit, plus the state); pages with very
  many units get slower. For units attached to each other in both directions the engine may start from the copy of
  the other unit's model that is stored with the first one (the import order cannot make both own models win).
- LibreOffice hatch lines are hairlines: the hatch line weight of the plan is not applied. The .pptx export turns
  `FillHatch` into a preset pattern fill (approximation by LibreOffice).
- Text boxes use the engine's estimated text box size (0.6 em per character); fonts without metric compatibility can
  overflow the box (no word wrap).
- A native resize combined with a rotation is not detected (only the rotation and the move of the centre are taken
  over). A copy pasted inside LibreOffice keeps the unit id of its original (no reconciliation of duplicates yet).
- Exchange with the PowerPoint COM add-in goes through the document mirror (the COM add-in writes `ISK_MODEL_<id>` and
  `ISK_BOUNDS_<id>` before saving and adopts LibreOffice units after opening). Verified 2026-11-20 in both directions
  (`integration_test.py --from-powerpoint`). Units of the Office web add-in keep their model in presentation tags, which
  LibreOffice drops, so they are not editable here yet.
- Verified on Windows only; Linux and macOS packages (self-contained, `--rid linux-x64`, `osx-arm64` …) are built by the
  same script but not tested (macOS packages must be built on macOS, ADR-0005).
