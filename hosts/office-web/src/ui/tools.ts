// Two small tools of the top bar, like the COM add-in's: the calculator with units (engine `calc`, `convertUnit`:
// "230 V * 16 A", "1500 rpm in rad/s") and the code import (engine `importCode`: matplotlib, pgfplots and TikZ code
// become editable charts and drawings; the code is read, never run). The engine does all the work.

import type { Session } from "../engine/client";
import type { CodeImportResult, EditorState } from "../engine/types";
import { lang, t } from "../i18n";
import { clamp, h } from "./dom";
import { icon } from "./icons";

/** The lines of a code import report: the message, what was imported, then at most `max` skipped parts with their line. */
export function codeReportLines(r: Pick<CodeImportResult, "message" | "imported" | "skipped">, max = 12): string[] {
  const lines = [r.message, ...r.imported.map((x) => `✓ ${x}`)];
  for (const s of r.skipped.slice(0, max)) lines.push(`– ${s.line > 0 ? `${s.line}: ` : ""}${s.reason}${s.code ? ` (${s.code.length > 40 ? s.code.slice(0, 39) + "…" : s.code})` : ""}`);
  if (r.skipped.length > max) lines.push(`… +${r.skipped.length - max}`);
  return lines;
}

/** File types the code import reads. */
export const CODE_FILES = ".py,.tex,.tikz,.pgf,.txt";

/** A file name that holds code for the import (Python script or LaTeX). */
export function isCodeName(name: string): boolean {
  return /\.(py|tex|tikz|pgf)$/i.test(name);
}

interface ToolHost {
  session(): Session;
  apply(state: EditorState, live?: boolean): void;
  report(error: unknown): void;
}

/** A popover in the pane with a title, a close button and Esc; placed under `anchor` or at a point. */
abstract class Popover {
  protected el?: HTMLElement;
  private restoreFocus?: HTMLElement | null;

  constructor(protected readonly root: HTMLElement, protected readonly host: ToolHost) {}

  get open(): boolean {
    return !!this.el;
  }

  protected frame(title: string, cls: string, ...body: HTMLElement[]): HTMLElement {
    this.close();
    this.restoreFocus = document.activeElement as HTMLElement | null;
    const close = h("button", { class: "isk-btn isk-icon-btn isk-close", type: "button", "aria-label": t("close"), title: t("close"), html: icon("close", 16) });
    close.addEventListener("click", () => this.close());
    const el = h("div", { class: `isk-popover ${cls}`, role: "dialog", "aria-label": title }, h("div", { class: "isk-pop-head" }, h("strong", {}, title), close), ...body);
    el.addEventListener("keydown", (e) => { if (e.key === "Escape") { e.preventDefault(); e.stopPropagation(); this.close(); } });
    this.root.append(el);
    this.el = el;
    return el;
  }

  protected placeAt(at: { x: number; y: number } | undefined): void {
    if (!this.el || !at) return;
    const rr = this.root.getBoundingClientRect();
    this.el.style.left = `${clamp(at.x - rr.left - this.el.offsetWidth / 2, 4, Math.max(4, rr.width - this.el.offsetWidth - 4))}px`;
    this.el.style.top = `${clamp(at.y - rr.top + 8, 4, Math.max(4, rr.height - this.el.offsetHeight - 4))}px`;
    this.el.style.right = "auto";
  }

  close(): void {
    if (!this.el) return;
    this.el.remove();
    this.el = undefined;
    this.restoreFocus?.focus?.({ preventScroll: true });
  }
}

/** The calculator with units: an expression, an optional target unit, the result with its steps. */
export class Calculator extends Popover {
  show(at?: { x: number; y: number }, text = ""): void {
    const expr = h("input", { class: "isk-input isk-calc-expr", type: "text", autocomplete: "off", spellcheck: "false", "aria-label": t("calc.expr"), placeholder: "230 V * 16 A", "data-cmd": "calcexpr" });
    expr.value = text;
    const unit = h("input", { class: "isk-input isk-calc-unit", type: "text", autocomplete: "off", spellcheck: "false", "aria-label": t("calc.unit"), placeholder: "kW", "data-cmd": "calcunit" });
    const result = h("output", { class: "isk-calc-result", "data-cmd": "calcresult", "aria-live": "polite" });
    const steps = h("div", { class: "isk-calc-steps isk-muted" });
    const error = h("div", { class: "isk-val-error", role: "alert", hidden: true });
    const go = (quiet: boolean) => {
      error.hidden = true;
      const text = expr.value.trim();
      if (!text) { result.textContent = ""; steps.replaceChildren(); return; }
      try {
        const r = this.host.session().calc(text, lang());
        let shown = r.text;
        const target = unit.value.trim();
        if (target) shown = this.host.session().convertUnit(r.text, target, lang()).text;
        result.textContent = `= ${shown}`;
        steps.replaceChildren(...r.steps.slice(0, 6).map((s) => h("div", {}, s)));
      } catch (e) {
        if (quiet) return;
        result.textContent = "";
        steps.replaceChildren();
        error.hidden = false;
        error.textContent = e instanceof Error ? e.message.replace(/^(calc|convertUnit): /, "") : String(e);
      }
    };
    expr.addEventListener("input", () => go(true));
    unit.addEventListener("input", () => go(true));
    for (const f of [expr, unit]) f.addEventListener("keydown", (e) => { if (e.key === "Enter") { e.preventDefault(); go(false); } });
    this.frame(t("calc.title"), "isk-calc",
      h("label", { class: "isk-field" }, h("span", { class: "isk-field-label" }, t("calc.expr")), expr),
      h("label", { class: "isk-field" }, h("span", { class: "isk-field-label" }, t("calc.unit")), unit),
      result, steps, error,
      h("p", { class: "isk-muted" }, t("calc.examples")));
    this.placeAt(at);
    if (text) go(true);
    expr.focus();
    expr.select();
  }
}

/** The code import: paste or open matplotlib, pgfplots or TikZ code; the report says what was imported and what not. */
export class CodeImport extends Popover {
  show(at?: { x: number; y: number }, text = ""): void {
    const area = h("textarea", { class: "isk-input isk-code-text", rows: 9, spellcheck: "false", "aria-label": t("code.text"), placeholder: "import matplotlib.pyplot as plt\nplt.plot([0, 1, 2], [0, 1, 4])", "data-cmd": "codetext" });
    area.value = text;
    const file = h("input", { type: "file", accept: CODE_FILES, hidden: true });
    file.addEventListener("change", async () => {
      const f = file.files?.[0];
      file.value = "";
      if (f) area.value = await f.text();
    });
    const pick = h("button", { class: "isk-btn", type: "button" }, t("code.file"));
    pick.addEventListener("click", () => file.click());
    const run = h("button", { class: "isk-btn isk-primary", type: "button", "data-cmd": "coderun" }, t("code.run"));
    const report = h("div", { class: "isk-code-report", "aria-live": "polite", "data-cmd": "codereport" });
    run.addEventListener("click", () => {
      const code = area.value;
      if (!code.trim()) return;
      try {
        const r = this.host.session().importCode(code, lang());
        if (r.ids.length) this.host.apply(r.state, true);
        report.replaceChildren(...codeReportLines(r).map((l, i) => h(i === 0 ? "strong" : "div", {}, l)));
      } catch (e) {
        this.host.report(e);
      }
    });
    this.frame(t("code.title"), "isk-code",
      h("p", { class: "isk-muted" }, t("code.hint")),
      area, file,
      h("div", { class: "isk-row", style: "justify-content:space-between" }, pick, run),
      report);
    this.placeAt(at);
    area.focus();
  }
}
