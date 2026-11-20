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
- Ribbon tab **InventaSketch**, generated from the module library: a tools group (Editor, Snap and Context bar
  toggles stored in `%LOCALAPPDATA%\InventaSketch\settings.json`, black-and-white, info), then one group per module
  (mechanics, annotation, patent, basic shapes) with one thumbnail gallery per library category (6 columns, name in the
  tooltip; groups with many categories show icons only so the tab fits on laptop screens). New modules appear without
  ribbon changes. Thumbnails are rendered with GDI+ from the engine primitives and follow the Office theme.
- Floating bar and pie menu show abstract line pictograms (`IconIds`, drawn by `OnSlide/IconPainter`) for views,
  mirror, rotate, duplicate, delete, type and label menus — deliberately not renders of the concrete element; the bar shows a tooltip for the hovered button. Rendering tests
  (`tests/InventaSketch.AddIn.Tests`, Windows) write light and dark PNGs of bar and pie menu for visual review. UI language follows the Office UI language (German if
  the LCID is German, otherwise English).
- Automation object for scripts and tests: `Application.COMAddIns("InventaSketch.AddIn.Connect").Object` with
  `Insert(itemId)`, `Place(itemId)`, `Editor(visible)`, `LibraryItems()`, `Version()`.
- Log: `%LOCALAPPDATA%\InventaSketch\addin.log`.

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
- **Floating bar:** a selected unit shows a bar above it (clear of PowerPoint's rotation handle, below if there is no
  room): its views, mirror horizontally/vertically, rotate 90°, duplicate, delete. Fully opaque within 12 logical px,
  otherwise 0.92 fading over 180 px from the closest pointer distance; below 0.1 it is dismissed until the next
  selection; typing hides it. Clicks on the bar never reach the slide.
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
`dist\InventaSketch-AddIn-<version>.zip`: unzip, close PowerPoint, double-click `Installieren.cmd`; remove with
`Deinstallieren.cmd`. The package contains the Release binaries, the sidebar editor (`AddIn\App`) and the
registration scripts.

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

## Known limits / next steps

- PowerPoint started via `New-Object -ComObject PowerPoint.Application` did not list the freshly registered add-in in
  this test; a normally started PowerPoint did. To be investigated for automated tests.
- Automation calls from other processes arrive on RPC threads; `BeginPlacement` marshals itself to the UI thread.
- Dark theme of bar and pie menu is checked only through rendering tests, not yet in a dark Office.
- Not yet verified on the slide: text prompt dialog, `X`/`Y`/`Shift+R`, pie menu via keyboard.
- Next: style controls (line, fill, arrowheads) on the bar, domain exports (BOM, netlists, topologies).
