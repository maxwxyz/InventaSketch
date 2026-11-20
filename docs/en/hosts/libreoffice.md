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
         sidebar.py   deck "InventaSketch", panel "Library"; library.py: filter/search, thumbnails
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
- **Sidebar deck "InventaSketch" ▸ Library:** search field (words match names, ids and keywords, accents ignored),
  category list, item list with preview (the engine's SVG thumbnail); double-click or *Insert* inserts the item in the
  middle of the slide/page (Calc: of the visible cell area). *Selected unit* shows the type of the selected unit and
  its views; choosing a view re-renders the unit from its stored model.
- **Scripting API** (DispatchHelper / macros), all `inventasketch.libreoffice:<command>`:
  `insert?item=<id>[&x=<mm>&y=<mm>]`, `library`, `editModel`, `setView?view=<id>`, `redraw`, `numeral`, `cleanup`,
  `about`. The integration test uses exactly these commands.
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
| `group` | `XShapeGrouper.group` (single children are not wrapped) |
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
- Editing (view switch, redraw, numeral) imports the stored model, applies a native move/resize (`hostTransform`
  from the stored to the current rectangle) or a native rotation (about the centre), runs the engine edit and replaces
  the unit at the same z-position in one undo step.

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

## Tests

```powershell
python -m unittest discover -s hosts/libreoffice/tests -p "test_*.py"     # unit tests, no LibreOffice
"C:\Program Files\LibreOffice\program\python.exe" hosts/libreoffice/tests/integration_test.py --oxt dist/InventaSketch-LibreOffice-0.1.0.oxt --out <folder>
"C:\Program Files\LibreOffice\program\python.exe" hosts/libreoffice/tests/ui_smoke.py --oxt dist/InventaSketch-LibreOffice-0.1.0.oxt --out <folder>
```

Unit tests cover the plan-to-UNO mapping, metadata, library filter, texts, .NET discovery, the protocol (with a
stand-in engine: errors, crash, timeout, foreign lines), main-thread marshalling and, if the engine is built, real
draw plans of library items. The integration test installs the package into a temporary profile, runs soffice
headless and never touches the user's profile or files; Linux/macOS run it with a Python that can import `uno`.

## Limits (phase 1)

- No on-canvas interaction yet (overlay, snapping while dragging, pie menu, selection listener): editing goes through
  the sidebar/menu. The sidebar shows views only; parameters, style and text editing are not exposed yet.
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
