// Keeps slide shapes and the editor session in step: insert, live update after edits, load the selected
// InventaSketch group back into the editor and adopt native moves/resizes. All slide work goes through one WorkQueue.

import type { Session } from "../engine/client";
import type { DrawCapabilities, EditorState, PlanUnit } from "../engine/types";
import type { HostCapabilities } from "./capabilities";
import { acknowledgeSelectedBounds, deleteUnitShapes, nativelyEdited, readSelectedUnit, writeUnits, type BoxPt } from "./powerpoint";
import { WorkQueue } from "./workqueue";

export interface SyncHost {
  session(): Session;
  readonly host: HostCapabilities;
  readonly draw: DrawCapabilities;
  /** The editor adopts a new state (loaded or transformed from the slide). */
  adopt(state: EditorState, reason: "loaded" | "native-edit"): void;
  report(error: unknown): void;
}

const rect = (b: BoxPt) => ({ x: b.left, y: b.top, w: b.width, h: b.height });

/**
 * Object ids to redraw after edits: with `touched` (changed objects plus their dependents, e.g. walls joined to an edited
 * wall and the doors in it) only the tracked units containing one of them, otherwise every tracked unit. Ids that no
 * longer exist are dropped.
 */
export function liveRedrawIds(units: Map<string, string[]>, existing: Set<string>, touched?: Set<string>): string[] {
  const out = new Set<string>();
  for (const objs of units.values()) {
    if (touched && !objs.some((id) => touched.has(id))) continue;
    for (const id of objs) if (existing.has(id)) out.add(id);
  }
  return [...out];
}

export class SlideSync {
  /** Units on the slide that this editor owns: unit id → object ids. */
  readonly units = new Map<string, string[]>();
  readonly queue = new WorkQueue();
  private writing = 0;
  /** Objects touched by edits since the last live write (coalesced writes must not lose earlier edits). */
  private touched = new Set<string>();
  private touchedAll = false;
  lastNotes: string[] = [];

  constructor(private readonly h: SyncHost) {}

  get writingNow(): boolean {
    return this.writing > 0;
  }

  /** Draws the given objects (default: all) onto the current slide and tracks them for live updates. */
  insert(ids?: string[]): Promise<PlanUnit[]> {
    return this.queue.run(async () => {
      const plan = this.h.session().drawPlan(ids && ids.length ? ids : undefined, this.h.draw);
      const stale = this.staleFor(plan.units);
      for (const u of plan.units) this.units.set(u.id, u.objectIds);
      await this.write(plan.units, stale);
      return plan.units;
    });
  }

  /**
   * After an edit: redraw the tracked units touched by it (changed objects and their dependents, in one write) with the
   * newest state (coalesced; at most one pending write). Without change information every tracked unit is redrawn.
   */
  scheduleLive(state: EditorState): Promise<void> {
    if (this.units.size === 0) return Promise.resolve();
    if (state.changed === undefined) this.touchedAll = true;
    else for (const id of [...state.changed, ...(state.dependents ?? [])]) this.touched.add(id);
    return this.queue.latest("live", async () => {
      const touched = this.touchedAll ? undefined : new Set(this.touched);
      this.touched.clear();
      this.touchedAll = false;
      const existing = new Set(state.objects.map((o) => o.id));
      const ids = liveRedrawIds(this.units, existing, touched);
      const gone = [...this.units.keys()].filter((u) => !(this.units.get(u) ?? []).some((id) => existing.has(id)));
      const plan = ids.length ? this.h.session().drawPlan(ids, this.h.draw) : { units: [] as PlanUnit[] };
      const stale = [...new Set([...gone, ...this.staleFor(plan.units)])];
      for (const g of stale) this.units.delete(g);
      for (const u of plan.units) this.units.set(u.id, u.objectIds);
      await this.write(plan.units, stale);
    });
  }

  /** Reads the selected InventaSketch unit into the editor (selection-changed handler). */
  loadSelection(): Promise<void> {
    if (this.writing) return Promise.resolve();
    return this.queue.latest("read", async () => {
      if (this.writing) return;
      const u = await readSelectedUnit(this.h.host);
      if (!u?.payload) return;
      const session = this.h.session();
      let st = session.importCompact(u.payload);
      const ids = st.selection;
      this.units.set(u.unitId, ids);
      if (nativelyEdited(u) && u.stored) {
        const rot = (u.current.rotation ?? 0) - (u.stored.rotation ?? 0);
        st = session.hostTransform(ids, rect(u.stored), rect(u.current), rot);
        await acknowledgeSelectedBounds(u.unitId, this.h.host);
        this.h.adopt(st, "native-edit");
        return;
      }
      this.h.adopt(st, "loaded");
    });
  }

  /** Units currently tracked whose objects moved into one of the new units (e.g. after grouping). */
  private staleFor(units: PlanUnit[]): string[] {
    const fresh = new Set(units.map((u) => u.id));
    const covered = new Set(units.flatMap((u) => u.objectIds));
    return [...this.units.entries()].filter(([id, objs]) => !fresh.has(id) && objs.some((o) => covered.has(o))).map(([id]) => id);
  }

  private async write(units: PlanUnit[], stale: string[]): Promise<void> {
    this.writing++;
    try {
      if (stale.length) await deleteUnitShapes(stale, this.h.host);
      await writeUnits(units, this.h.host);
      this.lastNotes = units.flatMap((u) => u.notes);
    } catch (e) {
      this.h.report(e);
    } finally {
      this.writing--;
    }
  }
}
