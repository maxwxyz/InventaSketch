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

## Known limits / next steps

- PowerPoint started via `New-Object -ComObject PowerPoint.Application` did not list the freshly registered add-in in
  this test; a normally started PowerPoint did. To be investigated for automated tests.
- The ribbon shows 16 category menus; with more categories the remaining ones are not reachable yet (gallery planned).
- Next: on-slide interaction (input hooks + layered overlay window: insert at pointer, snapping guides, floating bar,
  pie menu), editing existing units from their payload, sidebar task pane.
