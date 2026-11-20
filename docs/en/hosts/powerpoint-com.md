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
- Ribbon tab **InventaSketch** with one menu per library category (thumbnails rendered with GDI+ from the engine
  primitives), a black-and-white command and an info dialog. UI language follows the Office UI language (German if
  the LCID is German, otherwise English).
- Automation object for scripts and tests: `Application.COMAddIns("InventaSketch.AddIn.Connect").Object` with
  `Insert(itemId)`, `LibraryItems()`, `Version()`.
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
- **Floating bar:** a selected unit shows a bar above it (clear of PowerPoint's rotation handle, below if there is no
  room): its views, mirror horizontally/vertically, rotate 90°, duplicate, delete. Fully opaque within 12 logical px,
  otherwise 0.92 fading over 180 px from the closest pointer distance; below 0.1 it is dismissed until the next
  selection; typing hides it. Clicks on the bar never reach the slide.
- **Pie menu:** right-click on a selected unit: delete (top), views, type actions, duplicate, mirror, rotate 90°. The unit
  is re-rendered from its model; native moves and resizes are folded into the model first (`SketchUnits.Read`).

## Install (per user, no administrator rights)

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
- Floating bar: appears above the selected gear (screen capture checked), the view button switched the gear to the stick
  symbol, moving the pointer 900 px away dismissed the bar.

## Known limits / next steps

- PowerPoint started via `New-Object -ComObject PowerPoint.Application` did not list the freshly registered add-in in
  this test; a normally started PowerPoint did. To be investigated for automated tests.
- The ribbon shows 16 category menus; with more categories the remaining ones are not reachable yet (gallery planned).
- Automation calls from other processes arrive on RPC threads; `BeginPlacement` marshals itself to the UI thread.
- Next: resize/rotate with snapping, dark mode for bar and pie menu (Office theme), style controls (line, fill,
  arrowheads) on the bar, sidebar task pane with the shared editor.
