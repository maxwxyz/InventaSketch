// Bridge to the PowerPoint COM add-in when the editor runs inside its WebView2 task pane (sidebar.html).
// The COM add-in owns the slide: it draws units natively from their model payloads (freeforms, arrowheads, hatch
// lines) and reports the selected InventaSketch unit back. Same public surface as SlideSync, so the App does not care.

import type { Session } from "../engine/client";
import type { DrawCapabilities, EditorState, PlanUnit } from "../engine/types";
import type { HostCapabilities } from "./capabilities";

export interface ComSyncBridge {
  postMessage(message: unknown): void;
  addEventListener(type: "message", listener: (e: { data: unknown }) => void): void;
}

declare global {
  interface Window {
    chrome?: { webview?: ComSyncBridge };
  }
}

export function comBridge(): ComSyncBridge | undefined {
  return typeof window !== "undefined" ? window.chrome?.webview : undefined;
}

/** Everything the COM add-in can do natively (it re-renders from payloads with the full C# engine). */
export const comCapabilities: HostCapabilities = { office: true, tags: true, shapes: true, selection: true, groups: true, rotation: true, svg: true };
export const comDraw: DrawCapabilities = { freeform: true, arrowheads: true, patternFill: false, rotation: true, svg: true, shapes: true };

export interface ComSyncHost {
  session(): Session;
  adopt(state: EditorState, reason: "loaded" | "native-edit"): void;
  report(error: unknown): void;
  /** Modules hidden in the COM add-in ("Show and hide modules"): the sidebar library hides them too. */
  hideModules?(moduleIds: string[]): void;
}

/** Messages from the COM add-in. */
type FromHost =
  | { type: "selection"; unitId: string; payload: string }
  | { type: "present"; unitIds: string[] }
  | { type: "hiddenModules"; ids: string[] }
  | { type: "ack"; id: number; ok: boolean; error?: string };

export class ComSync {
  readonly units = new Map<string, string[]>();
  /** Every unit that came from or went to the slide, with its objects in the editor (for removals on the slide). */
  private readonly known = new Map<string, string[]>();
  lastNotes: string[] = [];
  private next = 1;
  private readonly waiting = new Map<number, { resolve: () => void; reject: (e: Error) => void }>();
  private writing = 0;

  constructor(private readonly h: ComSyncHost, private readonly bridge: ComSyncBridge) {
    bridge.addEventListener("message", (e) => this.receive(e.data as FromHost));
    bridge.postMessage({ type: "ready" });
  }

  get writingNow(): boolean {
    return this.writing > 0;
  }

  /** Draws the given objects (default: all) on the current slide as new units. */
  async insert(ids?: string[]): Promise<PlanUnit[]> {
    const plan = this.h.session().drawPlan(ids && ids.length ? ids : undefined, comDraw);
    for (const u of plan.units) { this.units.set(u.id, u.objectIds); this.known.set(u.id, u.objectIds); }
    await this.send({ type: "units", units: plan.units.map((u) => ({ id: u.id, payload: u.payload })), remove: [] });
    this.lastNotes = plan.units.flatMap((u) => u.notes);
    return plan.units;
  }

  /** After an edit: replace the tracked units on the slide with the newest state. */
  async scheduleLive(state: EditorState): Promise<void> {
    if (this.units.size === 0) return;
    const existing = new Set(state.objects.map((o) => o.id));
    const ids = [...new Set([...this.units.values()].flat())].filter((id) => existing.has(id));
    const plan = ids.length ? this.h.session().drawPlan(ids, comDraw) : { units: [] as PlanUnit[] };
    const fresh = new Set(plan.units.map((u) => u.id));
    const remove = [...this.units.keys()].filter((u) => !fresh.has(u));
    for (const r of remove) this.units.delete(r);
    for (const u of plan.units) { this.units.set(u.id, u.objectIds); this.known.set(u.id, u.objectIds); }
    await this.send({ type: "units", units: plan.units.map((u) => ({ id: u.id, payload: u.payload })), remove });
  }

  /** Starts insert-at-pointer on the slide for a library item (handled by the COM add-in). */
  place(itemId: string): void {
    this.bridge.postMessage({ type: "place", itemId });
  }

  /** The COM add-in pushes the selection; nothing to poll. */
  loadSelection(): Promise<void> {
    return Promise.resolve();
  }

  private receive(msg: FromHost): void {
    try {
      if (msg.type === "ack") {
        const w = this.waiting.get(msg.id);
        this.waiting.delete(msg.id);
        if (w) msg.ok ? w.resolve() : w.reject(new Error(msg.error ?? "COM add-in error"));
        return;
      }
      if (msg.type === "selection" && msg.payload && !this.writing) {
        const st = this.h.session().importCompact(msg.payload);
        this.units.clear();
        this.units.set(msg.unitId, st.selection);
        this.known.set(msg.unitId, st.selection);
        this.h.adopt(st, "loaded");
        return;
      }
      if (msg.type === "present" && !this.writing) this.dropMissing(new Set(msg.unitIds));
      if (msg.type === "hiddenModules") this.h.hideModules?.(msg.ids);
    } catch (e) {
      this.h.report(e);
    }
  }

  /**
   * Units deleted on the slide (PowerPoint's own Delete, cut, undo) leave the editor too. Only objects that came from
   * or went to the slide are affected; drafts that were never placed stay.
   */
  private dropMissing(present: Set<string>): void {
    const missing = [...this.known.keys()].filter((id) => !present.has(id));
    if (missing.length === 0) return;
    const existing = new Set(this.h.session().state(false).objects.map((o) => o.id));
    const objects = missing.flatMap((id) => this.known.get(id) ?? []).filter((id) => existing.has(id));
    for (const id of missing) { this.known.delete(id); this.units.delete(id); }
    if (objects.length > 0) this.h.adopt(this.h.session().remove(objects), "native-edit");
  }

  private send(message: Record<string, unknown>): Promise<void> {
    const id = this.next++;
    this.writing++;
    return new Promise<void>((resolve, reject) => {
      this.waiting.set(id, { resolve, reject });
      this.bridge.postMessage({ ...message, id });
    }).finally(() => this.writing--);
  }
}
