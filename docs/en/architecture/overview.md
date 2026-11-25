# Architecture overview

> German version: [../../de/architecture/overview.md](../../de/architecture/overview.md). Decisions: [ADRs](../../adr/).

## 1. One engine, three hosts

```text
                ┌──────────────────────── InventaSketch engine (C#, netstandard2.0 + net10.0) ───────────────────────┐
                │  Core: model · geometry · primitives · registry · snapping · hatching · edits/undo · JSON · SVG    │
                │  Modules: Annotation · Patent · Mechanical · (Kinematics · Electrical · Automotive · …)             │
                │  Engine API: JSON facade, editor sessions, hit test, snapping, context actions, host draw plans   │
                └───────────────┬───────────────────────────────┬────────────────────────────────┬──────────────────┘
                                │ in-process (.NET Fx 4.8)      │ .NET WebAssembly ([JSExport])  │ .NET child process
                                ▼                               ▼                                │ (JSON lines, stdio)
                  PowerPoint COM add-in (Windows)    Office web add-in (TS UI)                   ▼
                  ribbon, on-slide overlay,          sidebar preview editor,          LibreOffice extension (Python-UNO .oxt)
                  floating bar, pie menu,            Office.js draw-plan writer       Impress / Draw / Calc, on-canvas handlers,
                  native shapes via COM              (native shapes + SVG fallback)   overlay layer, native shapes via UNO
```

Why C# and not TypeScript: on-slide interaction in COM (mouse hooks + layered overlay window) and on-canvas
interaction in LibreOffice (UNO mouse handlers) need synchronous snapping/hit-testing in the host process.
.NET runs in-process in PowerPoint, as WebAssembly in the task pane and as a small self-contained process next to
LibreOffice's bundled Python. This pattern is proven in production by the maintainer's earlier add-in.
See [ADR-0002](../../adr/0002-csharp-engine.md).

## 2. Layers

| Layer | Project | Responsibility |
|---|---|---|
| Geometry & primitives | `InventaSketch.Core` (`Geometry.cs`, `Drawing.cs`, `Hatching.cs`) | vectors, matrices, paths, styles, arrowheads, hatching |
| Document model | `InventaSketch.Core` (`Model.cs`) | `SketchDocument`, pages, objects, attachments, references |
| Module contract & registry | `InventaSketch.Core` (`Modules.cs`) | `IModule`, `ObjectType`, views, params, anchors, actions, library |
| Edits & history | `InventaSketch.Core` (`Editing.cs`) | pure `doc → doc` edits, undo/redo |
| Snapping | `InventaSketch.Core` (`Snapping.cs`) | semantic, point, alignment and grid snapping with guides |
| Rendering | `InventaSketch.Core` (`SvgRenderer.cs`) | SVG export (colour/monochrome), thumbnails |
| Persistence | `InventaSketch.Core` (`DocumentJson.cs`) | versioned JSON, compact payload `isk1:` |
| Domain modules | `InventaSketch.Modules.*` | object types, libraries, rules |
| Engine API | `InventaSketch.Engine` | JSON facade for all hosts, editor sessions, draw plans |
| Hosts | `hosts/office-web`, `hosts/powerpoint-com`, `hosts/libreoffice` | UI and host API translation only |

Dependency rule: Core ← Modules ← Engine ← Hosts. Modules never reference hosts or each other's internals
(Patent may use Annotation's public types). No domain logic in TypeScript or Python.

## 3. Object model in practice

- **Box objects** (gear, bearing, text box …) render in local coordinates inside `Size` centred at (0,0); the
  object matrix (translation · rotation · flip) places them. Free scaling changes `Size` only.
- **Point objects** (arrow, lead line, dimension, wire) store page-space `Points`; each point may be **attached**
  to another object at normalised box coordinates (u, v), so it follows moves, rotations and free scaling.
- **Views** select a representation; **parameters** are optional; **style overrides** recolour without touching
  the type's line roles.
- **Anchors** (centre, corners, edge midpoints, ends, ports, axis segments, pitch circles, hubs) drive snapping.

## 4. Host draw plans

The engine lowers rendered primitives into a host draw plan in points: rectangles, ellipses, lines (dash,
arrowheads), freeforms (polylines/cubic Béziers), text, groups, with style and metadata. Capability-aware lowering
replaces unsupported features per host (e.g. Office.js has no freeform/arrowheads/pattern fill → explicit
geometry or an SVG picture for that object). Each inserted object/assembly is a group carrying tags with id, type
and a compressed model payload; the document-level store mirrors the model.

## 5. Metadata persistence

| Host | Per shape/group | Document level |
|---|---|---|
| PowerPoint COM | `Shape.Tags` (`ISK_ID`, `ISK_TYPE`, `ISK_MODEL`) | custom document properties (chunked) |
| Office.js | `shape.tags` (PowerPointApi 1.3+) | custom properties / custom XML parts (to be verified, see research) |
| LibreOffice | shape name `ISK:<type>:<id>`, `UserDefinedAttributes` on the shape (groups: on their first single shape; ODF only) | document user-defined properties (chunked) |

## 6. Testing strategy

- xUnit per library (geometry, snapping, hatching, edits, persistence, module rendering and anchors).
- SVG gallery of all library items × views for visual review (headless browser screenshots).
- Engine API contract tests; TypeScript adapter tests with a fake Office.js; Python adapter tests with a UNO stub.
- Host integration tests (PowerPoint via COM automation, LibreOffice headless) recorded in the capability matrix.

## 7. Engine API: actions, change sets and document settings

All hosts call the same JSON methods ([`EngineApi`](../../../src/InventaSketch.Engine/EngineApi.cs)); the contract types
live in `Contracts.cs` and `hosts/office-web/src/engine/types.ts`. Decision record: [ADR-0009](../../adr/0009-actions-change-sets-plan-scale.md).

- **Context actions** (`contextActions`, `applyAction`): type actions are listed as `t:<id>` with `enabled` and, for
  actions that need a text value, `input: {label, value, unit?}`. Hosts prompt for the value (COM: prompt dialog;
  web: inline field next to the selection) and call `applyAction {action, input}`; without `input` the engine
  rejects such an action. Types may also contribute **document-aware** actions (`ContextAction.InDocument`), which
  receive the whole document (e.g. architecture "align to wall"). `ContextActions.Run` in Core executes all three
  forms, so the COM add-in and the engine behave the same. `ObjectType.Actions(o, RenderContext)` lets labels and
  enabled states depend on the document.
- **Change sets** (`EditorState.changed`, `EditorState.dependents`): after every edit, undo or redo the state lists
  the objects added or modified by that change and the further objects whose rendering depends on the touched ones
  (`ObjectType.Dependents`, evaluated before and after the change, e.g. walls joined to an edited wall and the doors
  in it). Hosts redraw exactly these units in one undo step. Types whose rendering reads neighbours declare
  `ObjectType.DependsOnNeighbours`; hosts with one model per shape (PowerPoint COM) render such units with the other
  units of the slide as context.
- **Document settings:** `DocumentSettings.PlanScale` (denominator, 100 = 1:100, optional) is persisted with the
  document and every compact slide payload. `setPlanScale {value}` sets or clears it (one undo step, all objects with
  `ObjectType.UsesPlanScale` are reported as changed); `EditorState.planScale` and `inspector.planScale` /
  `usesPlanScale` expose it. `importCompact` adopts the payload's plan scale when the editor document has none.
- **View sizes:** `ViewDef.Size` gives a view's preferred box size. `setView` (and the COM view buttons) call
  `ObjectType.WithView`, which adapts `Size` with `ObjectType.SizeForView`: by default the object keeps its visual
  scale (geometric mean of its scale factors against the current view's preferred size) and takes the target view's
  aspect; types may override it (architecture openings keep their real width).

## Coupling and quick add

`Coupling` (Core) keeps mechanical assemblies consistent after edits: meshing gears, gears on shafts and concentric
parts are recognised from their anchors (pitch circles, hub points, axis segments) before the edit and follow it. Hosts
call it through `EditorSession.Apply` (engine, `couple: false` for free edits) or across units (COM). `Flow` builds
diagrams: `QuickAdd` adds a connected copy on a side (`ObjectType.ChainConnector`), `Replace` swaps the element and
keeps its connectors. Line branches attach to rails of other lines (`Attachments.For`). `Links` lists where objects
touch and follow each other (mechanical pairs and docked line ends) for lock markers and releases or couples a pair
(`SketchObject.Released`, pruned by `Coupling` once the pair no longer touches). `Fitting.Place` puts an inserted part
where it fits the selection (on the shaft, in mesh, through the hub). `Polylines` inserts and removes corners and adds
branches, extends, splits and joins lines, keeping docked branches in place. `Building` lists the build ports of an
element (mesh sides, ring inside, free axis ends, free hub), the library items that fit a port and places them coupled;
`Attachments.OnSurface` docks annotation ends anywhere on the element below them. `Crossings` finds where wires or pipes
cross (the upper one hops) and connects them at a crossing. `GearTrain` solves the angular speeds of a gear train
(linear system over mesh, Willis, shaft, compound, belt, bevel pair and fixed constraints) and turns it; `Assemblies` explodes
predefined assemblies into coupled parts. `Linkage` recognises self-built linkages (links docked on joints, fixed pivots,
sliders, pins in the slots of other links), computes their degree of freedom and drags them with constant link lengths.
