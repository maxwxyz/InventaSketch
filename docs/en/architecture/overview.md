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
| LibreOffice | shape name `isk:<id>`, `UserDefinedAttributes` (ODF only) | document user-defined properties (chunked) |

## 6. Testing strategy

- xUnit per library (geometry, snapping, hatching, edits, persistence, module rendering and anchors).
- SVG gallery of all library items × views for visual review (headless browser screenshots).
- Engine API contract tests; TypeScript adapter tests with a fake Office.js; Python adapter tests with a UNO stub.
- Host integration tests (PowerPoint via COM automation, LibreOffice headless) recorded in the capability matrix.
