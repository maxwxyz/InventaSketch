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
  `Insert(itemId)`, `Place(itemId)`, `Editor(visible)`, `LibraryItems()`, `Version()`.
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
`dist\InventaSketch-AddIn-<version>.zip`: unzip, close PowerPoint, double-click `Install.cmd`; remove with
`Uninstall.cmd`. The package contains the Release binaries, the sidebar editor (`AddIn\App`) and the
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
- **Insert ▸** in the pie menu offers elements that fit the selection (`ObjectType.Companions`, else the same category,
  always numeral/lead line/arrow), in the selection's kind of view; picking one starts insert-at-pointer with snapping.
- **Anchor** (on by default): line ends snapped to another element store an attachment (with pin id) and follow when
  that element is moved, rotated or resized (one undo step); moving the line itself detaches its ends. Verified: an
  arrow snapped to a gear's edge followed the dragged gear.
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
