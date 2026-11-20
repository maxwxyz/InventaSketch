# ADR-0009: Input and document actions, change sets, plan scale and view sizes

- **Date:** 2026-11-20 · **Status:** accepted

## Context
The domain modules asked for five engine features: context actions that need a typed value (a door width, a room
name), actions that need the document (align a door to the wall it sits on), redrawing of objects whose rendering
depends on neighbours (wall joints, openings cutting walls), a document-wide plan scale and a size that follows the
aspect of a view (a vehicle seen from the top and from the side). All three hosts must behave the same, and no
domain logic may move into TypeScript or Python.

## Options
1. Host-specific dialogs and redraw rules per module (rejected: domain logic in hosts, three implementations).
2. A generic "redraw everything on the page" rule (rejected: slow with many units, and impossible in PowerPoint COM,
   where every unit keeps its own model and does not know its neighbours).
3. Declarative extensions of the module contract plus engine-computed change sets (chosen).

## Decision
- `ContextAction` gets `Input` (prompt label, current value, apply with text), `ApplyInDocument` (document in,
  document out) and `IsEnabled`; `ContextActions.Run` in Core executes every form. `ObjectType.Actions(o,
  RenderContext)` is the document-aware action list. The JSON contract adds `ActionInfo.input` and
  `applyAction.input`.
- `ObjectType.Dependents(o, ctx)` names the objects that depend on `o`; `ObjectType.DependsOnNeighbours` marks types
  whose rendering reads neighbours. `EditorSession` computes a change set after every edit, undo and redo
  (`ChangeSet.Compute`: changed objects by reference comparison, removed objects, dependents before and after); the
  editor state carries `changed` and `dependents`. PowerPoint COM renders neighbour-dependent units with the other
  units of the slide as context and redraws dependent units in the same undo entry.
- `DocumentSettings.PlanScale` (optional denominator) with `setPlanScale`; `ObjectType.UsesPlanScale` marks the
  types that are redrawn when it changes.
- `ViewDef.Size` and `ObjectType.SizeForView` / `WithView` adapt the box size on view switches.

## Consequences
The contract grows by optional members only; existing modules and documents are unaffected (new fields are omitted
in JSON when unset). Architecture objects no longer store `scale = 100` by default, so the document plan scale
applies to new objects while older objects keep their explicit scale. Hosts must prompt for input actions before
calling `applyAction`. Native PowerPoint moves (handles, arrow keys) do not yet redraw dependents.

## Validation
Engine tests (`ModelRequestTests`), architecture tests (`DocumentAwareTests`), COM context test
(`SketchUnitContextTests`) and the web live-sync test (`live-redraw.test.ts`).
