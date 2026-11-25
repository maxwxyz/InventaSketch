# InventaSketch research: tool landscape and host APIs

Status: research note, 2026-11-20. Scope: what to copy from existing tools, which open-source parts can be reused, and what the host APIs (PowerPoint COM, Office.js, LibreOffice UNO) really allow.

Verification legend:

- **[V]** means the claim was checked against a source opened for this note. The URL is in the text or in the source list at the end.
- **[K]** means the claim comes from general project knowledge or package metadata and was **not re-opened** this session. Check it before relying on it, especially for licences.
- **[U]** means unverified or contradicted. Do not rely on it.

---

## Part A: Tool landscape

### A.1 Diagramming and drawing tools

| Tool | Category / interaction model | Data model | UX ideas worth copying | Licence / reuse | Adoptable in InventaSketch? |
|---|---|---|---|---|---|
| **draw.io / diagrams.net** | General diagrams. Drag from a stencil palette; hover arrows to clone and connect; floating connection points; waypoint editing; edge styles (orthogonal, curved, entity-relation) | mxGraph XML (cells, geometry, style strings) | Blue hover arrows to "add a connected shape here"; style-string inheritance; very large stencil libraries; "Edit style" raw text | **[V]** Source is Apache-2.0. Icons, stencils and templates carry an extra restriction: they must not be incorporated into Atlassian products, and this includes conversions and traced reproductions. Diagram output is unrestricted. External pull requests are not accepted, and "draw.io" is a trademark. ([GitHub](https://github.com/jgraph/drawio)) | **Partly.** The stencil XML is Apache-2.0 and can be converted for a non-Atlassian product. Keep attribution, check third-party icon sets one by one, and never use the name. Copy the hover-to-connect gesture into the task-pane editor. |
| **draw.io Office add-in** | Inserts a page of a draw.io file into Word, Excel or PowerPoint | Picture plus a reference to the source file | Re-insert from source to update | Same as above | **No** as an architecture. **[V]** Diagrams are embedded as images, and you update them by re-inserting ([drawio docs](https://www.drawio.com/docs/integrations/microsoft/powerpoint-diagrams/)). This is the "image island" anti-pattern InventaSketch wants to avoid. |
| **Microsoft Visio** | Professional diagrams: shapes with ShapeSheet formulas, glue to connection points, dynamic connectors, containers, data linking | VSDX (OPC + ShapeSheet cells) | Formula-driven smart shapes (the ShapeSheet); glue (dynamic vs static); container behaviour; layer properties | Proprietary | **Partly (concepts).** Use a "parameter cells, then geometry" model per symbol, i.e. a small ShapeSheet-like expression layer. |
| **Inkscape** | SVG vector editor: node editing, boolean path ops, clones, LPE (live path effects), Trace Bitmap (potrace) | SVG plus `inkscape:` namespace | Live path effects (non-destructive); clones; snapping toolbar with fine-grained snap targets | **[K]** GPL-2.0-or-later (some parts GPL-3) | **No code reuse** (copyleft vs. a permissive or mixed add-in). Copy the snap-target taxonomy and the idea of non-destructive "effects" such as hatching and dimensioning applied to a base path. |
| **Excalidraw** | Hand-drawn whiteboard; arrow binding to shapes; keyboard-first tools | JSON elements | Arrow binding with "focus/gap"; rough.js sketch style; library sharing | **[K]** MIT | **Yes.** The binding algorithm idea and rough.js (MIT) can serve as a "sketchy" render style. The model is too generic to adopt wholesale. |
| **tldraw** | Infinite canvas SDK with bindings, shape utils, state charts for tools | JSON store (records) | Tool state machines; bindings as first-class records | **[V]** Source-available, **not open source**. Production needs a licence key (commercial, 100-day trial, or a hobby licence with watermark) ([tldraw licence](https://tldraw.dev/community/license)) | **No** as the engine (licence would pass to every user). Copy the "binding records" and "tool state machine" patterns. |
| **Lucidchart** | Web diagrams, conditional formatting, data linking; Office add-ins insert images | Proprietary | Smart containers, swimlanes | Proprietary | Concepts only. |
| **Mermaid / PlantUML** | Text to diagram (UML, ER, flow, sequence, Gantt) | DSL text | Round-trip "code view" next to the canvas | **[K]** Mermaid is MIT. PlantUML is GPL-3, with MIT, LGPL, Apache and EPL licence variants published separately | **Yes, for import.** Parse Mermaid (MIT) or run it in the task pane, take its layout or SVG, then convert to native shapes. For PlantUML, use one of the permissive builds, or call it out of process. |

### A.2 Electronics and schematic tools

| Tool | Interaction model | Data model | UX ideas | Licence / reuse | Adoptable? |
|---|---|---|---|---|---|
| **KiCad (Eeschema)** | **Move (M)** detaches wires, **Drag (G)** keeps wires connected and rubber-bands them. Hotkey-first. ERC (electrical rules check) with a pin-type matrix (input, output, power, passive…) | S-expression files; symbols have pins with electrical types | M vs G distinction; ERC matrix; net labels and global labels; "junction dot" automation | **[V]** Symbol and footprint libraries are **CC-BY-SA 4.0 with an exception**: designs made with the libraries are released from Article 3 ([KiCad licence](https://www.kicad.org/libraries/license/)). KiCad code is GPL-3 **[K]** | **Partly.** Using KiCad symbols in a user's drawing is fine because of the exception. *Redistributing* converted symbol libraries inside InventaSketch makes that library CC-BY-SA 4.0: ship it as a separate attributed data package, not inside proprietary code. Copy M vs G and a light ERC (unconnected pins, output-to-output shorts). |
| **Fritzing** | Three synchronised views (breadboard, schematic, PCB); edits propagate between them | FZP (XML) plus SVG per view | Multi-view consistency: one model, several renderings | **[V]** Graphics and documentation are CC-BY-SA 3.0 Unported ([LICENSE.txt](https://raw.githubusercontent.com/fritzing/fritzing-parts/develop/LICENSE.txt)). App is GPL-3 **[K]** | **Concept: yes.** One model rendered as "functional", "schematic" and "line-figure" views is exactly what InventaSketch needs (for example a gearbox as a 3D-ish view, a stick diagram and black-and-white line art). Parts graphics are share-alike. |
| **CircuitJS (Falstad)** | Live-simulated schematic in the browser; current animation | Text netlist | Instant feedback; drag a value and see the effect | **[K]** GPL-2.0 | **No code.** Optional future "simulate" link via export to the CircuitJS text format (users open it externally). |
| **Qucs / Qucs-S** | Schematic capture plus SPICE | XML-ish schematic | Component property tables on canvas | **[K]** GPL-2.0 | Concepts only. |

### A.3 CAD and constraint tools

| Tool | Interaction model | Notes | Licence | Adoptable? |
|---|---|---|---|---|
| **LibreCAD / QCAD** | Command line plus snap modes (endpoint, midpoint, intersection, perpendicular), relative coordinates `@dx,dy` | DXF native | **[K]** LibreCAD GPL-2.0; QCAD Community GPL-3 | Copy the snap-mode bar and typed coordinates in the task-pane editor. No code. |
| **FreeCAD Sketcher** | Draw roughly, then add constraints; DOF counter; colours for fully constrained vs under-constrained | Solver is **PlaneGCS** (DogLeg, LM, BFGS, SQP) | **[K]** FreeCAD is LGPL-2.0-or-later | **Yes, via the WASM build** (below). Copy the DOF counter and the green "fully constrained" feedback. |
| **SolveSpace** | Parametric 2D/3D, constraint solver *libslvs* | Has an experimental web build that the README itself calls very buggy | **[V]** GPL-3.0-or-later; libslvs lives in the same GPL tree ([GitHub](https://github.com/solvespace/solvespace)) | **No** (GPL-3 would infect the engine). Use PlaneGCS instead. |
| **PlaneGCS WASM** (`@salusoft89/planegcs`) | FreeCAD's 2D solver compiled to WebAssembly, with TS types | Primitives: point, line, circle, arc, ellipse, conic arcs, B-spline. All planegcs constraints | **[V]** LGPL-2.1 ([GitHub](https://github.com/Salusoft89/planegcs)) | **Yes.** Load it as a separate, replaceable WASM module (dynamic linking satisfies LGPL). Use it for linkages, sketch constraints and gear centre-distance constraints. |
| **kiwi.js (Cassowary)** | Linear-constraint solver for layout (UI-style equalities and inequalities) | **[V]** Repo archived 2021-12, points to `lume/kiwi` ([GitHub](https://github.com/IjzerenHein/kiwi.js)). Licence **[K]** BSD-3 | **Yes** for alignment and distribution constraints (floor plans, block diagrams). Use the maintained fork. |

### A.4 Mechanism, gear and drivetrain tools

| Tool | Model | Ideas to copy | Licence | Adoptable? |
|---|---|---|---|---|
| **Linkage (David Rector)** | Windows mechanism editor: connectors, links, sliders, gears/chains; animate the input crank | Connectors are "anchor" or free; a drive input with RPM; motion-path traces; draw tracers | **[U]** Freeware for Windows. Licence and source not verified | Concepts: anchor/link/slider primitives, path tracing, a "run" button. |
| **MotionGen** (Stony Brook) | Mobile/web app for planar four-bar/six-bar synthesis from task positions | Synthesis from precision positions; coupler-curve atlas | **[U]** Proprietary freeware | Concepts: "draw desired path, then get a four-bar". Later feature. |
| **GIM** (University of the Basque Country) | Kinematic/dynamic analysis of planar mechanisms | Velocity and acceleration polygon visualisation | **[U]** Free but closed | Concepts only. |
| **KISSsoft / MASTA / Romax** | Commercial drivetrain design. All have a **2D schematic or "stick" diagram** of shafts, gears, bearings and clutches, with **power-flow** colouring per gear stage | Stick symbols (shaft = line, gear = short bar with tooth count, bearing = small box/cross), power-flow arrows, speed/torque labels per element | Proprietary | **Concepts: yes.** Use KISSsoft/Romax-style stick notation as the InventaSketch "schematic gearbox" style, and the power-flow overlay as a toggle. **[K]** for the exact notation per vendor. |
| **Gear generators** (e.g. geargenerator.com) | Place gears, auto-mesh by clicking the neighbour, animate; planetary presets | Snapping a gear onto a neighbour; centre distance `a = m(z1+z2)/2`; planetary assembly condition `(zs+zr)/n ∈ ℕ` | **[U]** Site licence not checked | **Yes (algorithm).** Snapping is closed-form, so no solver is needed. Generate involute outlines ourselves or draw schematic circles. |

### A.5 Floor plans, roads, trajectories

| Tool | Model | Ideas | Licence | Adoptable? |
|---|---|---|---|---|
| **Sweet Home 3D** | Walls as thick polylines with auto-joined corners; rooms from wall loops; furniture catalogue; dimension lines | Double-click wall drawing; magnetism (15° angles, wall snapping); live dimensions | **[K]** GPL-2.0-or-later (furniture models have mixed licences) | Concepts. Implement wall offsetting with Clipper2. |
| **Floorplanner** | Web 2D/3D planner | Room-first drawing | Proprietary | Concepts. |
| **RoadRunner (MathWorks) / Driving Scenario Designer** | Road networks from clicked control points; lanes; junction auto-generation; actor trajectories with waypoints and speed | OpenDRIVE / OpenSCENARIO export | Proprietary | Concepts: lane-template plus spline-centreline model, trajectory waypoints with time stamps. |
| **esmini** | OpenSCENARIO player | Reference player for OpenSCENARIO and OpenDRIVE | **[K]** MPL-2.0 | Optional: export `.xodr`/`.xosc` light and let users play them in esmini externally. |

### A.6 Line-figure drawing tools

| Tool | What it does | Notes |
|---|---|---|
| **PatentFig AI, PatDraw, CAD-to-line-art services** | Generate or convert drawings into black-and-white line art with reference numerals and lead lines. Some use AI vectorisation; others are services that redraw from CAD | **[U]** Feature claims and licences were not verified this session. |
| **Generic pipeline** (any tool) | Office rules: black lines on white, no colour, hatching for cut sections, numerals ≥ 0.32 cm high, lead lines, "FIG. n" labels, margins (USPTO 37 CFR 1.84, EPO Rule 46) | **[K]** Rules are stable but should be cited from USPTO/EPO directly in a separate note. |

The InventaSketch "monochrome mode" should be a **render style plus a checker** rather than a separate editor:

- monochrome conversion
- line-weight normalisation
- hatch styles (ISO 128 / ANSI 31-style)
- auto reference numerals with leader lines, avoiding crossings via libavoid
- a margin and font-size checker
- export to PDF and TIFF G4

### A.7 PowerPoint add-ins with on-slide interaction (think-cell and similar)

- **think-cell** is a native C++ COM add-in. Its public API is COM, reached through `Application.COMAddIns` with late binding **[V via search snippet of think-cell manual]**. How it implements on-slide handles is not publicly documented **[U]**. The observable behaviour is native-looking handles, drag feedback over the slide pane, and its own context menus and floating toolbars. That is consistent with top-level layered windows tracking the slide-pane window, plus hooks on PowerPoint's window messages.
- **General technique for COM/VSTO add-ins** **[K]**:
  - Create a borderless top-level window with `WS_EX_LAYERED | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE`, owned by PowerPoint's main window.
  - Add `WS_EX_TRANSPARENT` when the overlay should not capture input.
  - Position it over the slide pane, converting slide points to screen pixels with `DocumentWindow.PointsToScreenPixelsX/Y` ([doc](https://learn.microsoft.com/en-us/office/vba/api/powerpoint.documentwindow.pointstoscreenpixelsx)) **[V]**.
  - Re-align on `WindowSelectionChange` ([doc](https://learn.microsoft.com/en-us/office/vba/api/powerpoint.application.windowselectionchange)) **[V]**, and on zoom, scroll or resize, which have no COM events: use `SetWinEventHook(EVENT_OBJECT_LOCATIONCHANGE)` on the pane window plus a low-rate timer to compare `ActiveWindow.View.Zoom` and the result of PointsToScreenPixels.
  - Make the overlay thread per-monitor-DPI-aware v2 (`SetThreadDpiAwarenessContext`). PointsToScreenPixels already returns physical pixels for the monitor PowerPoint is on, but you must test mixed-DPI multi-monitor setups.
  - Keep the overlay **transient**: it shows only during a tool gesture, and the final result is committed as native shapes.

### A.8 Candidate JS/WASM engine libraries

| Need | Library | Licence | Notes |
|---|---|---|---|
| 2D geometric constraints | `@salusoft89/planegcs` | **[V]** LGPL-2.1 | Keep it as a separate WASM file (replaceable). |
| Linear layout constraints | kiwi.js / `lume/kiwi` | **[K]** BSD-3 | Original archived **[V]**. |
| Boolean ops / offsets | Clipper2 (C++/C#/Delphi) with ports `clipper2-ts`, `Clipper2-WASM` | **[V]** Clipper2 is BSL-1.0; port licences not stated on the Clipper2 page | Best for wall offsets, hatch clipping, outlines. **There is also an official C# Clipper2, which is useful for a C# host.** |
| Boolean ops (alt.) | `polygon-clipping`, `martinez-polygon-clipping` | **[K]** MIT | Simpler, slower. |
| Vector scene / paths | paper.js | **[K]** MIT | Good path maths (offsets via plugin, hit-testing). Optional. |
| Hatching / pattern fill | Own code: parallel lines clipped by Clipper2. Or native host hatch (LibreOffice `FillHatch`, OOXML `a:pattFill`) | n/a | Generate **real line geometry** for monochrome output. Host hatches don't scale like ISO hatches. |
| Image tracing | imagetracerjs | **[V]** Unlicense (public domain) | Colour and BW tracing to SVG. |
|  | potrace (C) and JS/WASM ports | **[K]** GPL-2.0 | Best quality, but copyleft. Run it out of process or as a user-installed plugin only. |
|  | OpenCV.js | **[K]** Apache-2.0 (OpenCV ≥ 4.5) | Edge and contour extraction for "photo to sketch". Large (~8 MB WASM). |
| SVG to PDF | svg2pdf.js + jsPDF; pdf-lib for assembly | **[K]** MIT (all three) | Vector PDF export from the canvas model. |
| TIFF G4 | UTIF.js (decoder with G3/G4; **encoder writes uncompressed** **[U]**); `ts-ccitt-g4-encoder` (v1.0, small, Japanese docs) **[U]**; libtiff compiled to WASM (**[K]** BSD-like libtiff licence) | mixed | Recommendation: libtiff→WASM, or a ~300-line own CCITT T.6 encoder (the algorithm is public and simple for bilevel). |
| Graph layout | elkjs (layered, orthogonal) | **[K]** EPL-2.0 | Best for flowcharts and block diagrams. EPL is weak copyleft at file level, so use it unmodified. |
|  | dagre / @dagrejs | **[K]** MIT | Simpler alternative. |
| Orthogonal connector routing | libavoid-js | **[V]** LGPL-2.1, WASM, TS typings, orthogonal plus polyline routing, incremental `processTransaction()` ([GitHub](https://github.com/Aksem/libavoid-js)) | Ship as a separate WASM module. Used by JointJS and Sprotty. |
| Sketchy style | rough.js | **[K]** MIT | Optional "napkin" look. |

---

## Part B: Host APIs

### B.1 Office.js: PowerPoint

**Requirement sets and platforms** [V] ([source](https://learn.microsoft.com/en-us/javascript/api/requirement-sets/powerpoint/powerpoint-api-requirement-sets)):

| Set | Web | Windows M365 (min.) | Windows LTSC/perpetual | Mac (min.) | iPad | Key content (verified per set page) |
|---|---|---|---|---|---|---|
| 1.1 | ✓ | 1810 | Office 2021 | 16.19 | 2.17 | `createPresentation` |
| 1.2 | ✓ | 2011 | Office 2021 | 16.43 | ✗ | `insertSlidesFromBase64`, delete slides |
| 1.3 | ✓ | 2111 | Office 2024 | 16.55 | ✗ | add slides, **tags** on presentation, slide and shape |
| 1.4 | ✓ | 2207 | Office 2024 | 16.62 | ✗ | `addGeometricShape`, `addLine(connectorType)`, `addTextBox`, fill (`setSolidColor`, transparency), `lineFormat` (color, **dashStyle**, style, weight, transparency), text frame |
| 1.5 | ✓ | 2208 | Office 2024 | 16.64 | ✗ | **Selection:** `getSelectedShapes`, `getSelectedSlides`, `setSelectedSlides`, `slide.setSelectedShapes`, `getSelectedTextRange` (see note below) |
| 1.6 | ✓ | 2410 | ✗ | 16.90 | ✗ | Hyperlinks (read) |
| 1.7 | ✓ | 2412 | ✗ | 16.92 | ✗ | **customXmlParts** on presentation, slide, layout, master **and shape**; document and custom properties |
| 1.8 | ✓ | 2504 | ✗ | 16.96 | ✗ | **`addGroup`, `ShapeScopedCollection.group()`, `ShapeGroup.ungroup()`**, `setZOrder`, `addTable`, **bindings**, `fill.setImage`, `slide.exportAsBase64()`, `slide.getImageAsBase64()`, placeholders |
| 1.9 | ✓ | 2508 | ✗ | 16.100 | ✗ | Table formatting (borders, merge, rows/cols) |
| 1.10 | ✓ | 2601 | ✗ | 16.105 | ✗ | **`shape.rotation`**, `adjustments` (read), `visible`, `altText*`, `creationId`, `shape.getImageAsBase64`, `setHyperlink`, slide background (incl. pattern fill), theme colours, `pageSetup.slideWidth/Height`, `exportAsBase64Presentation` |
| Preview | Insider | | | | | `addPicture`, **`Graphic.convertToShape()`** (returns a ShapeScopedCollection; looks like UI "Convert to Shape" for SVG graphics), `onSlideSelectionChanged`, `getActiveSlideOrNullObject` |

Notes:

- The overview page says 1.5 = hyperlinks and 1.6 = selection. The individual set pages say the reverse: the **1.5 page lists the selection APIs** and the **1.6 page lists hyperlinks**. Trust the set pages and runtime checks (`isSetSupported`).
- **iPad is effectively out** (only 1.1). **Office 2024 LTSC stops at 1.5**, so no groups and no customXmlParts there. Design for 1.4 as the floor (shapes plus tags) and progressively enhance.

**What Office.js PowerPoint still cannot do (as of preview, 2026-10)** [V by absence on class pages]:

- **No arrowheads**: `ShapeLineFormat` has only color, dashStyle, style, transparency, visible and weight ([class page](https://learn.microsoft.com/en-us/javascript/api/powerpoint/powerpoint.shapelineformat?view=powerpoint-js-preview)).
- **No connector glue** (no begin/end connect). Compare Excel, which has it.
- **No freeform/custom geometry**, no Bézier paths.
- **No pattern or hatch fill on shapes**: pattern fill exists only for slide backgrounds in 1.10.
- No flip, and adjustments are read-only in 1.10.

**Workarounds for full fidelity in the web add-in:**

1. **SVG insert:** `Office.context.document.setSelectedDataAsync(svg, {coercionType: XmlSvg})`. ImageCoercion 1.2 covers PowerPoint on web, Windows and Mac, but **not iPad** ([source](https://learn.microsoft.com/en-us/javascript/api/requirement-sets/common/image-coercion-requirement-sets)) [V]. The result is an SVG *graphic*, not native shapes, unless the user runs "Convert to Shape". The preview `Graphic.convertToShape()` [V, preview] would automate this. Watch it closely, because it could become the cheapest path to native freeform shapes.
2. **OOXML slide injection:** generate a one-slide PPTX in JS containing DrawingML: `a:custGeom` freeforms, `a:headEnd/a:tailEnd` arrows, `a:pattFill`, `p:cxnSp` with `stCxn/endCxn` glue, groups, and `p:custDataLst` tags. Insert it with `insertSlidesFromBase64` (1.2). For "draw onto the current slide", one pattern is:
   - `slide.exportAsBase64()` (1.8)
   - merge the shapes into that slide XML in JS
   - insert the result after the slide
   - delete the original and reselect.

   This gives full native fidelity, but the slide ID and creation ID change, undo becomes coarse, and comments or animations need care. It is **[K]** a common pattern; prototype it early.
3. **Shape-by-shape API** for simple primitives, for example rectangles, circles, straight lines and text, styled live during editing.

**Selection and events:**

- `getSelectedShapes()` (1.5) [V].
- `Office.EventType.DocumentSelectionChanged` via `Office.context.document.addHandlerAsync` (Common API) **[K]**: it does not say *what* changed, so you re-query.
- Preview `onSlideSelectionChanged` [V].
- There is no shape-moved event. Detect user edits by re-reading positions on selection change.

**Overlays on the slide:** **No.** A web add-in runs in a task pane, dialog or content-add-in iframe. There is no API to draw over the slide canvas **[K; consistent with the absence of any such API in the PowerPoint reference]**. Alternatives:

- **Task-pane editor** (the primary approach): an interactive preview with commit to the slide.
- **Dialog API** `displayDialogAsync` [V] ([source](https://learn.microsoft.com/en-us/office/dev/add-ins/develop/dialog-api-in-office-add-ins)): a large editor window. It always opens centred and is movable, resizable and non-modal, up to about 99.5% of the screen. Messaging uses `messageParent`/`messageChild`, and `displayInIframe` on the web. Only one dialog per host page.
- **Content add-ins** in PowerPoint **[K]**: a live web view embedded *as an object on the slide*. You can interact on the slide, but the content is not native shapes, and it renders as a snapshot where the add-in is not available. Useful at most for a "live mechanism animation" object, not as the main path.

**Shared runtime and keyboard shortcuts** [V] ([source](https://learn.microsoft.com/en-us/office/dev/add-ins/design/keyboard-shortcuts)):

- **PowerPoint now supports custom keyboard shortcuts**: Windows 2601 (Build 19628.20150), MEC 2604, Mac 16.105.2, and web.
- They require SharedRuntime 1.1, and only `executeFunction` actions are supported.
- On the web, shortcuts don't fire while the task pane has focus.
- Use the pattern Ctrl+Alt+Shift+x to avoid conflicts.

**Bindings** (1.8) [V]: `presentation.bindings.add(shape, type, id)` gives a stable handle to a shape. This is useful as a second anchor besides tags.

### B.2 COM and Web add-in coexistence

Source: [Make your Office Add-in compatible with an existing COM add-in](https://learn.microsoft.com/en-us/office/dev/add-ins/develop/make-office-add-in-compatible-with-existing-com-add-in) [V].

| Mode | XML manifest | Unified JSON manifest | Behaviour | Min. version |
|---|---|---|---|---|
| **Hide the web add-in when the COM add-in is installed** | `<EquivalentAddins><EquivalentAddin><ProgId>X</ProgId><Type>COM</Type></EquivalentAddin></EquivalentAddins>`, placed immediately before `</VersionOverrides>` | `extensions[].alternates[].prefer.comAddin.progId` | On Windows, the web add-in's **ribbon UI is hidden**, but it is still installed and listed in My Add-ins and the ribbon manager. A call-out points to the COM button. If the COM add-in is uninstalled, the web UI returns on the next start. **Office stops processing updates for the web add-in while the COM add-in is present.** | Excel, Word, PowerPoint on Windows 1904+ |
| **Hide the COM add-in** (web add-in wins) | same, plus `<Effect>DisableWithNotification</Effect>` as last child | `alternates[].hide.windowsExtensions{effect:"disableWithNotification", comAddin:{progIds:[…]}}` | The COM add-in is disabled with a popup notice | Windows 2506 (19029.20004) |
| **User chooses** | `<Effect>UserOptionToDisable</Effect>` | `effect:"userOptionToDisable"` | Install-time dialog asks whether to disable the older add-in | Windows 2506 |

Requirements:

- `ProgId` is the **registry key name** under `HKCU|HKLM\Software\Microsoft\Office\PowerPoint\Addins`. It is case-sensitive and also used for VSTO add-ins.
- `<Type>` is **"COM"** for COM *and* VSTO add-ins. XLL exists for Excel custom functions.
- The markup has no effect on Mac or web.
- Microsoft's guidance expects the same developer, the same workload and similar names and icons.

### B.3 PowerPoint COM (Windows)

Shape creation **[K]** unless marked **[V]**:

- `Shapes.AddShape(msoAutoShapeType, l, t, w, h)`, `AddLine`, `AddCurve(SafeArrayOfPoints)` (Bézier), `BuildFreeform(editingType, x, y)` → `FreeformBuilder.AddNodes(segmentType, editingType, x1, y1, …)` → `ConvertToShape()`, `AddPolyline`, and `AddTextbox`.
- `AddConnector(msoConnectorStraight|Elbow|Curve, …)` followed by **`ConnectorFormat.BeginConnect(shape, site)` / `EndConnect`** **[V]**. The site is in `1..ConnectionSiteCount`, and `RerouteConnections` picks the shortest sites ([doc](https://learn.microsoft.com/en-us/office/vba/api/powerpoint.connectorformat.beginconnect)).
- Formatting:
  - `LineFormat.BeginArrowheadStyle/EndArrowheadStyle/…Length/…Width`, `DashStyle`
  - `FillFormat.Patterned(msoPatternXxx)` (fixed bitmap patterns, not scalable hatch)
  - `Shape.Rotation`, `Flip`, `Adjustments.Item(i)` read/write
- Grouping: `Shapes.Range(Array(...)).Group()` and `Ungroup()`.
- Metadata: **`Shape.Tags.Add(name, value)`**, plus Slide and Presentation Tags. Tags are stored as OOXML `p:custDataLst` tags, which are the same store Office.js `shape.tags` reads, so **COM and Web can share metadata** **[K, test]**. Presentation-level `CustomXMLParts`.
- Events:
  - `Application.WindowSelectionChange(Sel)` **[V]**, which fires for UI and code changes
  - `PresentationBeforeSave`, `AfterShapeSizeChange` (2013+) **[K]**
  - No general "shape moved" event, so poll `Selection.ShapeRange` positions after mouse-up (a low-level mouse hook or a timer).
- Overlay coordinates: `ActiveWindow.PointsToScreenPixelsX/Y` **[V]** (see A.7). Recompute after `View.Zoom` changes. In Normal view the slide pane is a child window (class `paneClassDC` **[K]**) whose rectangle you can get via UI Automation or `FindWindowEx`.
- `CommandBars.ExecuteMso("ObjectsGroup")` etc. **[K]** runs built-in commands such as "Convert to Shape", edit points or align, with the same effect as the UI.
- WebView2 in the COM add-in: a task pane (`CustomTaskPane` with a WinForms UserControl hosting WebView2) or an overlay window can host the same TS UI. **[V]** You must set a custom **user data folder** (for example `%LOCALAPPDATA%\InventaSketch\WebView2`). The default next to the host exe (POWERPNT.EXE in Program Files) is not writable and WebView2 will fail to start ([doc](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/user-data-folder)).

### B.4 LibreOffice

**Extension languages (2026):**

- **Python UNO `.oxt`** is the most practical choice. The Windows and macOS installers bundle their own Python, and Linux uses the distribution's Python with `python3-uno` **[K]**. Only pure-Python dependencies can be vendored reliably; binary wheels must match the bundled interpreter per OS and architecture.
- **Java**: needs a JRE configured by the user. **Basic**: fine for glue, weak for an engine.
- **.NET bindings:**
  - The GSoC 2024 project replaced the 20-year-old Windows-only CLI bindings with cross-platform .NET (Standard 2.0, .NET 8) bindings, a NuGet package and an out-of-process bridge.
  - **The in-process bridge for authoring UNO components (that is, extensions) in .NET was listed as future work** [V] ([GSoC report gist](https://gist.github.com/RMZeroFour/80e5115ddd59030a0ec808abc396e839)).
  - Search results also report that official SDKs do not ship the prebuilt .NET assemblies or NuGet package (tdf#165585) [V via search snippet; the Ask page was bot-blocked].
  - **Verdict: not viable for a cross-platform extension in 2026**, only for external automation.

**UI options:**

- **Sidebar panel:** `Sidebar.xcu` (deck plus panel) and a Python `XUIElementFactory`/`XToolPanel` implementation, with controls created via the toolkit or an XDL dialog loaded as a panel **[K]**.
- **Dialogs:** XDL from the Basic IDE, loaded with `DialogProvider2` **[K]**.
- **HTML/WebView inside the LibreOffice UI: none found.** No embedded browser control exists for UNO dialogs or sidebars. A historic request for a browser widget was declined [V via search summary; Ask/Bugzilla pages bot-blocked]. TDF posts from October 2026 do not mention one.
- **The "Document Foundation developer blog, October 2026, pywebview" claim is UNVERIFIED.** The TDF blog archive for October 2026 (pages 1–2, 12 posts) has no post about pywebview, WebView or HTML extension UIs ([p.1](https://blog.documentfoundation.org/blog/2026/10/), [p.2](https://blog.documentfoundation.org/blog/2026/10/page/2/)) [V]. Page 3 and the Planet aggregator were not checked. A targeted web search for "pywebview LibreOffice" found nothing.
- **Workable alternatives:**
  - (a) A local HTTP server in the extension's Python (`127.0.0.1`, random port, one-time token) plus `XSystemShellExecute` to open the system browser running the TS editor. It communicates by fetch/long-poll, and UNO calls are marshalled to the main thread via `com.sun.star.awt.AsyncCallback`.
  - (b) pywebview as a **separate process**. It is only easy where the user can pip-install (Linux system Python), since it is not bundled.
  - (c) The native sidebar for parameters only.

**Drawing API** **[V]** for Shape and Hatch, otherwise **[K]**:

- Pages: `XDrawPagesSupplier.getDrawPages()` (Draw/Impress), `sheet.DrawPage` (Calc), and `XShapes.add()`. Creation goes through `doc.createInstance("com.sun.star.drawing.RectangleShape" | "EllipseShape" | "LineShape" | "PolyLineShape" | "PolyPolygonShape" | "PolyPolygonBezierShape" | "ConnectorShape" | "TextShape" | "GraphicObjectShape")`.
- Bézier shapes use `PolyPolygonBezierCoords` (coordinates plus `PolygonFlags` NORMAL/CONTROL/SMOOTH/SYMMETRIC). Units are 1/100 mm.
- `ConnectorShape`: `StartShape`/`EndShape` plus `StartGluePointIndex`/`EndGluePointIndex`. Custom glue points via `XGluePointsSupplier`. `EdgeKind` STANDARD/CURVE/LINE/LINES.
- Arrows: `LineStartName`/`LineEndName` (e.g. "Arrow", "Square 45"), `LineStartWidth`.
- **`FillStyle=HATCH` + `FillHatch` = `Hatch{Style (SINGLE/DOUBLE/TRIPLE), Color, Distance (1/100 mm), Angle (1/10°)}`** [V] ([Hatch](https://api.libreoffice.org/docs/idl/ref/structcom_1_1sun_1_1star_1_1drawing_1_1Hatch.html)). This is a real vector hatch, good for sectional views in office drawings. `FillBitmap*` covers bitmaps.
- Grouping: `XShapeGrouper.group(XShapes)` and `ungroup`.
- **Metadata:** the property is named **`ShapeUserDefinedAttributes`** (XNameContainer of `AttributeData`). It is stored as XML attributes in ODF automatic styles [V] ([Shape service](https://api.libreoffice.org/docs/idl/ref/servicecom_1_1sun_1_1star_1_1drawing_1_1Shape.html)), not "UserDefinedAttributes" as in the brief. It **is not expected to survive PPTX export** [U]. Also available: `Name`, `Title`, `Description`, and `InteropGrabBag` (ignored by the ODF filter [V]). Document level: `XDocumentProperties.UserDefinedProperties` or an extra stream in the document storage.
- **Selection:** `controller.addSelectionChangeListener(XSelectionChangeListener)`. Use `XModifyListener` on the document to catch moves and resizes, then re-solve.
- **SVG import as editable shapes:** insert as a graphic, then dispatch `.uno:Break`; or `.uno:ConvertIntoMetaFile` then `.uno:Break`. Alternatively load the SVG with the Draw import filter, which yields shapes, and copy them over. Results vary with complex SVG (clip paths, patterns). Generating UNO shapes directly from the IR is more reliable.
- **Impress, Draw and Calc differences:**
  - Draw: arbitrary page size and layers. This makes it the best fit for drawing sheets for applications (A4 with
    margins).
  - Impress: masters, notes and presentation objects.
  - Calc: one draw page per sheet. Shapes have an `Anchor` (cell or page), there are no slides, coordinates are relative to the sheet origin, and row-height changes move cell-anchored shapes.
- **Version context:** LibreOffice now uses year-based versions. 26.8.1 was released 2026-10-08 and 26.8.2 on 2026-10-29 [V via the TDF blog archive].

### B.5 Verification of the Gemini-plan claims

| Claim | Verdict | Evidence |
|---|---|---|
| (a) Microsoft removed the Visio Data Visualizer add-in for Excel from Dec 2025/Jan 2026 and shut the service down on 2 March 2026 | **TRUE (with nuance)** | Store and ribbon removal was planned for **8 Dec 2025** and then "extended until at least January 2026". The **service shutdown date of 2 March 2026 stayed unchanged** ([Tech Community](https://techcommunity.microsoft.com/blog/microsoft_365blog/retirement-of-the-visio-data-visualizer-add-in-for-excel-and-next-steps/4465747); MC1182535). |
| (b) The draw.io Office add-in only embeds rendered images | **TRUE** | "Embedded diagrams are displayed in your PowerPoint slides as images". To update, you re-insert ([drawio docs](https://www.drawio.com/docs/integrations/microsoft/powerpoint-diagrams/)). |
| (c) ExcelApi 1.9 has `addSvg` and `connectBeginShape`/`connectEndShape` | **PARTLY FALSE** | ExcelApi 1.9 has `Line.connectBeginShape(shape, connectionSite)` / `connectEndShape`, plus `beginArrowheadStyle`, `addLine`, `addGroup` and `addImage` (JPEG/PNG). **There is no `addSvg` in Excel's `ShapeCollection` at all, including preview** ([1.9 set](https://learn.microsoft.com/en-us/javascript/api/requirement-sets/excel/excel-api-1-9-requirement-set), [ShapeCollection preview](https://learn.microsoft.com/en-us/javascript/api/excel/excel.shapecollection?view=excel-js-preview)). |
| (d) PowerPointApi 1.7 adds customXmlParts | **TRUE** | 1.7 adds `customXmlParts` on Presentation, Slide, SlideLayout, SlideMaster **and Shape**, plus document and custom properties ([1.7 set](https://learn.microsoft.com/en-us/javascript/api/requirement-sets/powerpoint/powerpoint-api-1-7-requirement-set)). Not on LTSC/perpetual Office or iPad. |

---

## Part C: Recommendations

### C.1 Shared engine language: TypeScript core, thin native adapters

> **Project decision (2026-11-20):** this recommendation was weighed and **not adopted**. InventaSketch uses one
> C# engine (in-process in COM, WebAssembly in the task pane, .NET process for LibreOffice) because on-slide and
> on-canvas interaction needs synchronous snapping in the host process and the pattern is proven in production.
> See [ADR-0002](../../adr/0002-csharp-engine.md). The analysis below is kept for reference.

Structure the TypeScript core as a monorepo:

- `core-model`: document model with symbols and parameters, plus a constraint graph.
- `core-geom`: Clipper2 port and hatching.
- `core-solve`: planegcs WASM plus closed-form gear and linkage rules.
- `core-render`: produces an **intermediate representation (IR)** of host-neutral drawing operations, for example "freeform path with arrowheads, line style, hatch; group; connector from A.site to B.site; tag k=v".
- Back-ends that turn the IR into:
  - Office.js calls (simple shapes)
  - OOXML DrawingML (full fidelity, for slide injection and for the COM path)
  - SVG (preview and export)
  - a JSON op list for UNO (LibreOffice).

Options weighed:

| Option | Pros | Cons | Verdict |
|---|---|---|---|
| **A. TS core in WebView2 inside the COM add-in.** The UI is the same task-pane app; it sends IR ops to C#, which applies them via COM | One UI and one engine. Feature parity with the web add-in. WebView2 is evergreen on Win10/11 | Asynchronous messaging (`postMessage`). Each drag round-trip costs about 1–5 ms **[K]**, which is fine for commits but marginal for live on-slide drag feedback. You must handle the UDF location [V] and WebView2 Runtime presence | **Recommended baseline.** |
| **B. TS core in ClearScript (V8) in-process in C#** | Synchronous calls from C# overlay code at 60 Hz. Same JS bundle | ClearScript ships native V8 binaries (~20–30 MB per arch). WASM support inside ClearScript V8 is **[U]** (test planegcs). Debugging is harder | **Recommended add-on** for the overlay hot path if A is too slow. Keep it optional. |
| **C. Jint** (pure .NET JS interpreter) | No native deps | Slow for solvers; **no WebAssembly** **[K]**, so planegcs cannot run | Reject. |
| **D. C# core (port)** | Fastest in COM; native debugging | Two engines that diverge. LibreOffice still needs another port | Reject, except for small C# helpers (Clipper2 C# for overlay hit-testing). |

For LibreOffice:

- Do **not** port the full engine to Python. The editor (TS, in the system browser) does the solving and sends IR ops to the Python adapter.
- For "user dragged a native gear, re-snap its neighbours" without the editor open, implement a **small Python rule kernel**: gear centre distance, planetary positions and four-bar closure. These are closed-form and do not need planegcs.
- Embedding a JS engine in the bundled Python (QuickJS bindings, wasmtime-py) needs per-platform binary wheels. Avoid it for v1.

### C.2 Native-shape strategy per host

| Host | Primitive shapes | Freeform / arrows / hatch / connectors | Strategy |
|---|---|---|---|
| **PowerPoint COM (Windows, priority)** | `AddShape` / `AddLine` / `AddTextbox` | `BuildFreeform`/`AddCurve`; arrowheads via `LineFormat`; **connectors glued via `BeginConnect`/`EndConnect`**; hatch as **generated line geometry** grouped with the outline (`FillFormat.Patterned` is a bitmap pattern and unsuitable for office drawings) | Apply the IR directly via COM. Group per symbol. Tags on every shape. The overlay is transient. Commit as one undo unit where possible (PowerPoint has no explicit undo transaction API **[K]**, so minimise operation count). |
| **Office.js (Mac/web/Windows fallback)** | 1.4 API | Not available shape-by-shape. Use **(1) OOXML slide injection** for full fidelity, **(2) SVG via XmlSvg** for "picture plus convert to shape" (watch preview `Graphic.convertToShape()`), and **(3) 1.4 shapes** for simple cases | Feature-detect: 1.8+ for grouping, 1.7+ for customXml. Keep the task-pane editor as the interaction surface. Commit on "Insert/Update". |
| **LibreOffice Impress/Draw/Calc** | UNO shapes | `PolyPolygonBezierShape`, `LineStart/EndName`, **native `FillHatch` vector hatch**, `ConnectorShape` with glue points | Apply the IR via the Python adapter. Use Draw as the target for drawing sheets. In Calc, anchor to the page. |

Cross-host interaction model: **edit natively, re-solve on commit.** The user moves a native shape. A selection or modify event fires, the adapter reads the shape's tag (symbol ID), and the engine re-solves and repositions dependent shapes. This works in all three hosts without overlays. Overlays (COM only) add live previews during drags.

### C.3 Metadata persistence per host

Use a layered approach. Every layer must survive the "foreign host round-trip" (PPTX opened in LibreOffice and back) on a best-effort basis.

1. **Per-shape ID (mandatory, lossless across hosts):** encode a short ID in the **shape name** (`IS:<kind>:<uuid8>`). Names survive PPTX↔ODP round-trips best **[K, test]**. Mirror the ID in:
   - PowerPoint **Tags** (COM `Tags` = Office.js `shape.tags`, from 1.3)
   - LibreOffice `ShapeUserDefinedAttributes` (ODF only).
2. **Per-diagram model (parameters, constraints):** store compact JSON with a version field in:
   - PowerPoint: a **presentation-level CustomXmlPart** with namespace `urn:inventasketch:model:v1`. Use COM `CustomXMLParts`, or Office.js 1.7+; on hosts without 1.7, chunked tags on the group shape. Prefer presentation-level parts with shape IDs over shape-level customXmlParts until both COM and Web behaviour is tested.
   - LibreOffice: document `UserDefinedProperties` (string, chunked), or a stream in the document storage. ODF only. On PPTX save, write tags through the export only if LibreOffice preserves `custDataLst`, which is **[U]**. Otherwise the model is lost and the shapes degrade to plain shapes. That is acceptable, but show a warning.
3. **Self-healing:** if the model is missing but shapes with `IS:` names exist, rebuild a "dumb" model (positions only) so the user can at least re-snap.

### C.4 Most reliable cross-platform LibreOffice UI in 2026

1. **Python `.oxt` with a native sidebar deck** (`Sidebar.xcu` plus `XUIElementFactory`) for symbol palettes, parameters and quick actions. It works the same on Windows, macOS and Linux and needs no extra runtime.
2. **The rich editor is the same TS web app served from a localhost HTTP server inside the extension**, opened in the **system browser** (`XSystemShellExecute`). Bind to 127.0.0.1, use a random port and a per-session token, and marshal UNO calls back to the main thread with `AsyncCallback`. No binary dependencies.
3. pywebview (separate process) is optional where available. It is not the default because it is not bundled with LibreOffice's Python on Windows or macOS and needs per-OS GUI back-ends.
4. Do not wait for an in-LibreOffice WebView. None exists or has been announced [V by absence: TDF blog Oct 2026 p.1–2; search].
5. Do not use the .NET bindings for the extension: component authoring is not available [V].

---

## Sources opened for this note

- PowerPoint requirement sets overview, plus pages for 1.4, 1.5, 1.6, 1.7, 1.8, 1.9, 1.10 and preview: `https://learn.microsoft.com/en-us/javascript/api/requirement-sets/powerpoint/` (`powerpoint-api-requirement-sets`, `powerpoint-api-1-4-requirement-set` … `powerpoint-api-1-10-requirement-set`, `powerpoint-preview-apis`)
- https://learn.microsoft.com/en-us/javascript/api/powerpoint/powerpoint.graphic
- https://learn.microsoft.com/en-us/javascript/api/powerpoint/powerpoint.shapelineformat?view=powerpoint-js-preview
- https://learn.microsoft.com/en-us/office/dev/add-ins/powerpoint/shapes
- https://learn.microsoft.com/en-us/javascript/api/requirement-sets/common/image-coercion-requirement-sets
- https://learn.microsoft.com/en-us/office/dev/add-ins/design/keyboard-shortcuts
- https://learn.microsoft.com/en-us/office/dev/add-ins/develop/dialog-api-in-office-add-ins
- https://learn.microsoft.com/en-us/office/dev/add-ins/develop/make-office-add-in-compatible-with-existing-com-add-in
- https://learn.microsoft.com/en-us/javascript/api/requirement-sets/excel/excel-api-1-9-requirement-set
- https://learn.microsoft.com/en-us/javascript/api/excel/excel.shapecollection?view=excel-js-preview
- https://learn.microsoft.com/en-us/office/vba/api/powerpoint.documentwindow.pointstoscreenpixelsx
- https://learn.microsoft.com/en-us/office/vba/api/powerpoint.connectorformat.beginconnect
- https://learn.microsoft.com/en-us/office/vba/api/powerpoint.application.windowselectionchange
- https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/user-data-folder
- https://techcommunity.microsoft.com/blog/microsoft_365blog/retirement-of-the-visio-data-visualizer-add-in-for-excel-and-next-steps/4465747
- https://www.drawio.com/docs/integrations/microsoft/powerpoint-diagrams/
- https://github.com/jgraph/drawio
- https://www.kicad.org/libraries/license/
- https://raw.githubusercontent.com/fritzing/fritzing-parts/develop/LICENSE.txt
- https://tldraw.dev/community/license
- https://github.com/Salusoft89/planegcs
- https://github.com/Aksem/libavoid-js
- https://github.com/solvespace/solvespace
- https://github.com/AngusJohnson/Clipper2
- https://github.com/IjzerenHein/kiwi.js
- https://github.com/jankovicsandras/imagetracerjs
- https://api.libreoffice.org/docs/idl/ref/servicecom_1_1sun_1_1star_1_1drawing_1_1Shape.html
- https://api.libreoffice.org/docs/idl/ref/structcom_1_1sun_1_1star_1_1drawing_1_1Hatch.html
- https://blog.documentfoundation.org/blog/2026/10/ and https://blog.documentfoundation.org/blog/2026/10/page/2/
- https://gist.github.com/RMZeroFour/80e5115ddd59030a0ec808abc396e839 (GSoC 2024 .NET UNO bindings report)

Blocked when fetching (bot protection): wiki.documentfoundation.org, ask.libreoffice.org, lists.freedesktop.org. The LibreOffice statements that depend on them are marked [K] or [U].

## Open items to verify next

1. Office.js `shape.tags` and COM `Shape.Tags` are the same OOXML store (round-trip test).
2. Behaviour of the "export slide, merge, re-insert" pattern on web, Mac and Windows (IDs, undo, comments).
3. When preview `Graphic.convertToShape()` ships, and whether it works on XmlSvg-inserted graphics.
4. Whether LibreOffice preserves `p:custDataLst` tags and shape names on a PPTX round-trip.
5. ClearScript V8 running planegcs WASM.
6. Licences marked [K]: elkjs, esmini, potrace ports, clipper2-ts, kiwi fork, OpenCV.js build, Sweet Home 3D, PlantUML variant.
