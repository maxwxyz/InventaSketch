# LibreOffice extension (Impress, Draw, Calc)

> German: [../../de/hosts/libreoffice.md](../../de/hosts/libreoffice.md)

Phase 1 of the LibreOffice host (roadmap "Phase 3 – LibreOffice extension", ADR-0005): an installable `.oxt` that
inserts InventaSketch library elements as native, editable LibreOffice shapes in Impress, Draw and Calc and stores the
model on the shapes so they can be edited again. Ported from the maintainer's Slidemax LibreOffice host (engine process,
UNO shape creation, sidebar plumbing, main-thread marshalling, packaging), translated and adapted.

## Architecture

```
LibreOffice (Impress/Draw/Calc)
 └─ inventasketch_component.py      Python-UNO: protocol handler (menu, scripting API), sidebar panel factory
     └─ pythonpath/inventasketch_lo
         host.py      target page, insertion point, selection, insert/edit flows, one undo step per action
         plan.py      draw plan (pt) -> shape specifications (1/100 mm), no UNO, unit-tested
         render.py    specifications -> native UNO shapes, groups, line-end markers, metadata
         metadata.py  unit name, attributes, chunked document properties, native-edit detection, no UNO
         sidebar.py   deck "InventaSketch", panels "Selection" and "Library"; library.py: catalog filter, thumbnails
         ux.py        selection panel rules, recent items, page session bookkeeping (no UNO, unit-tested)
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
  Selected Unit*, *Redraw Selected Unit from Model*, *Add Reference Numeral*, *Remove Data of Deleted Units*,
  *About InventaSketch*.
- **Sidebar deck "InventaSketch"**, two panels (see the UI/UX round below):
  - *Selection* follows the document selection (selection listener) and shows what can be done with the selected
    InventaSketch unit(s), context only.
  - *Library*: one search field for the whole library (engine `search`), the category list (first entry
    *Suggestions*: the parts that fit the selection, then the recent items), the item list with preview (the engine's
    SVG thumbnail); double-click or *Insert* inserts the item – a part that fits the selected unit where it fits,
    anything else in the middle of the slide/page (Calc: of the visible cell area).
- **Scripting API** (DispatchHelper / macros), all `inventasketch.libreoffice:<command>`:
  `insert?item=<id>[&x=<mm>&y=<mm>]`, `insertFitting?item=<id>`, `insertSearch?text=<query>`, `library`, `editModel`,
  `setView?view=<id>`, `redraw`, `numeral`, `transform?kind=mirrorH|mirrorV|rotate90`, `explode`, `drawTrace`,
  `drawGhosts`, `build?port=<port id>&item=<id>`, `turn?degrees=<angle>`, `arrange?action=<id of arrangeOptions>`,
  `assembly`, `panel?control=<key>&text=<text>` / `panel?control=<key>&item=<entry>` / `panel?press=<key, label or
  action:<id>>` (operates a control of the open panel the way a user does), `cleanup`, `about`. The integration and UI
  smoke tests use these commands.
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
- **Not in LibreOffice (needs the planned overlay):** dragging with snapping and snap labels, the live coupling
  preview, build "+" on the canvas and stretching a shaft end by dragging, motion mode by dragging (gear trains turn by
  a typed angle instead; linkages cannot be dragged), "make it …" after a quick add, the pie menu and Ctrl+Space.

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

## Tests

```powershell
python -m unittest discover -s hosts/libreoffice/tests -p "test_*.py"     # unit tests, no LibreOffice
"C:\Program Files\LibreOffice\program\python.exe" hosts/libreoffice/tests/integration_test.py --oxt dist/InventaSketch-LibreOffice-0.1.0.oxt --out <folder>
"C:\Program Files\LibreOffice\program\python.exe" hosts/libreoffice/tests/ui_smoke.py --oxt dist/InventaSketch-LibreOffice-0.1.0.oxt --out <folder>
```

Unit tests cover the plan-to-UNO mapping, metadata, library filter, texts, .NET discovery, the protocol (with a
stand-in engine: errors, crash, timeout, foreign lines), main-thread marshalling, the selection panel rules and the page
session bookkeeping (`test_ux.py`) and, if the engine is built, real draw plans of library items and the engine flows
behind the panel with payloads drawn in separate sessions (search with fitting parts, insert fitting, arrange across
units, a gear train turned across units, explode). The UI smoke test operates the panel's controls with the `panel`
command (typing into the search field and choosing a list entry go through the controls and call the same listeners
as a user; buttons run their click handler); real mouse clicks are not simulated, because over the remote bridge the
panel's control peers are generic windows without list box or accessibility interfaces. The integration test installs the package into a temporary profile, runs soffice
headless and never touches the user's profile or files; Linux/macOS run it with a Python that can import `uno`.

## Limits (phase 1)

- No on-canvas interaction yet (overlay, snapping while dragging, pie menu): editing goes through the sidebar and the
  menu; the selection panel follows the selection. Parameters, style and text editing are not exposed yet.
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
