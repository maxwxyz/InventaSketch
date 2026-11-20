// Inspector: optional parameters, view, text and style of the selection. Every change is one engine command.

import type { Session } from "../engine/client";
import type { ArrowHead, DashStyle, EditorState, InspectorInfo, ParamInfo, ParamValue, StyleInfo } from "../engine/types";
import { lang, t } from "../i18n";
import { h } from "./dom";

export interface InspectorHost {
  session(): Session;
  state(): EditorState;
  apply(state: EditorState, live?: boolean): void;
  report(error: unknown): void;
}

const DASHES: DashStyle[] = ["solid", "dash", "dot", "dashDot", "dashDotDot", "longDash"];
const ARROWS: ArrowHead[] = ["none", "triangle", "open", "stealth", "diamond", "oval", "tick"];

export class Inspector {
  readonly el: HTMLElement;
  private readonly body: HTMLDivElement;
  private key = "";

  constructor(private readonly host: InspectorHost) {
    this.body = h("div", { class: "isk-insp-body" });
    this.el = h("section", { class: "isk-inspector", "aria-label": t("insp.title") }, this.body);
  }

  focusStyle(): void {
    this.el.querySelector<HTMLElement>("[data-focus=style]")?.focus();
  }

  focusText(): void {
    this.el.querySelector<HTMLTextAreaElement>("textarea")?.focus();
  }

  /** Rebuilds when the selection or the document revision changed (skips while an input has focus). */
  update(force = false): void {
    const st = this.host.state();
    const key = `${st.selection.join(",")}@${st.revision}@${lang()}`;
    if (!force && key === this.key) return;
    if (!force && this.el.contains(document.activeElement) && document.activeElement?.tagName !== "BUTTON" && key.split("@")[0] === this.key.split("@")[0]) return;
    this.key = key;
    if (!st.selection.length) {
      this.body.replaceChildren(h("p", { class: "isk-empty" }, t("insp.none")));
      return;
    }
    let info: InspectorInfo;
    try {
      info = this.host.session().inspector(st.selection, lang());
    } catch (e) {
      this.host.report(e);
      return;
    }
    this.body.replaceChildren(...this.build(info, st));
  }

  private edit(f: (s: Session, ids: string[]) => EditorState): void {
    try {
      this.host.apply(f(this.host.session(), this.host.state().selection), true);
    } catch (e) {
      this.host.report(e);
    }
  }

  private build(info: InspectorInfo, st: EditorState): HTMLElement[] {
    const out: HTMLElement[] = [];
    const title = info.typeName ?? t("insp.multi", st.selection.length);
    out.push(h("div", { class: "isk-insp-head" }, h("strong", {}, title), info.ref ? h("span", { class: "isk-badge", title: t("insp.ref") }, info.ref) : ""));

    if (info.views.length > 1) {
      const sel = h("select", { class: "isk-input", "aria-label": t("ctx.view") });
      for (const v of info.views) sel.append(h("option", { value: v.id, selected: v.active }, v.label));
      sel.addEventListener("change", () => this.edit((s, ids) => s.setView(ids, sel.value)));
      out.push(field(t("ctx.view"), sel));
    }

    if (info.hasText) {
      const ta = h("textarea", { class: "isk-input", rows: 2, "aria-label": t("insp.text") });
      ta.value = info.text ?? "";
      ta.addEventListener("change", () => this.edit((s, ids) => s.setText(ids, ta.value)));
      out.push(field(t("insp.text"), ta));
    }

    const basic = info.params.filter((p) => !p.advanced);
    const adv = info.params.filter((p) => p.advanced);
    if (basic.length) out.push(h("h3", { class: "isk-insp-section" }, t("insp.params")), ...basic.map((p) => this.param(p)));
    if (adv.length) {
      const d = h("details", { class: "isk-insp-adv" }, h("summary", {}, t("insp.advanced")), ...adv.map((p) => this.param(p)));
      out.push(d);
    }

    out.push(h("h3", { class: "isk-insp-section" }, t("insp.style")), ...this.style(info.style ?? {}, info.points));
    return out;
  }

  private param(p: ParamInfo): HTMLElement {
    const label = p.unit ? `${p.label} (${p.unit})` : p.label;
    const current = p.value ?? p.default ?? null;
    const set = (v: ParamValue) => this.edit((s, ids) => s.setParam(ids, p.key, v));
    switch (p.kind) {
      case "boolean": {
        const cb = h("input", { type: "checkbox", checked: current === true });
        cb.addEventListener("change", () => set(cb.checked));
        return h("label", { class: "isk-field isk-check" }, cb, h("span", {}, label));
      }
      case "choice": {
        const sel = h("select", { class: "isk-input", "aria-label": label });
        sel.append(h("option", { value: "" }, "–"));
        for (const o of p.options ?? []) sel.append(h("option", { value: o.id, selected: String(current) === o.id }, o.label));
        sel.addEventListener("change", () => set(sel.value === "" ? null : sel.value));
        return field(label, sel);
      }
      case "text": {
        const inp = h("input", { class: "isk-input", type: "text", "aria-label": label });
        inp.value = current === null ? "" : String(current);
        inp.addEventListener("change", () => set(inp.value === "" ? null : inp.value));
        return field(label, inp);
      }
      default: {
        const inp = h("input", {
          class: "isk-input", type: "number", "aria-label": label, min: p.min, max: p.max,
          step: p.step ?? (p.kind === "integer" ? 1 : "any"), placeholder: p.default != null ? String(p.default) : t("insp.optional"),
        });
        inp.value = p.value === undefined || p.value === null ? "" : String(p.value);
        inp.addEventListener("change", () => {
          if (inp.value.trim() === "") return set(null);
          const n = Number(inp.value);
          if (Number.isFinite(n)) set(p.kind === "integer" ? Math.round(n) : n);
        });
        return field(label, inp);
      }
    }
  }

  private style(style: StyleInfo, points: boolean): HTMLElement[] {
    const set = (st: StyleInfo) => this.edit((s, ids) => s.setStyle(ids, st));
    const color = h("input", { class: "isk-color", type: "color", "aria-label": t("style.stroke"), "data-focus": "style" });
    color.value = style.stroke && /^#[0-9a-f]{6}$/i.test(style.stroke) ? style.stroke : "#000000";
    color.addEventListener("change", () => set({ stroke: color.value }));
    const width = h("input", { class: "isk-input", type: "number", min: 0.05, max: 5, step: 0.05, placeholder: t("style.auto"), "aria-label": t("style.width") });
    width.value = style.strokeWidth !== undefined ? String(style.strokeWidth) : "";
    width.addEventListener("change", () => { const n = Number(width.value); if (n > 0) set({ strokeWidth: n }); });
    const dash = h("select", { class: "isk-input", "aria-label": t("style.dash") });
    dash.append(h("option", { value: "" }, t("style.auto")));
    for (const d of DASHES) dash.append(h("option", { value: d, selected: style.dash === d }, t(`dash.${d}`)));
    dash.addEventListener("change", () => dash.value && set({ dash: dash.value as DashStyle }));
    const out = [field(t("style.stroke"), color), field(t("style.width"), width), field(t("style.dash"), dash)];

    if (!points) {
      const fill = h("select", { class: "isk-input", "aria-label": t("style.fill") });
      const kind = style.fill?.kind ?? "";
      fill.append(h("option", { value: "" }, t("style.auto")));
      for (const k of ["none", "solid", "hatch"]) fill.append(h("option", { value: k, selected: kind === k }, t(`fill.${k}`)));
      const fillColor = h("input", { class: "isk-color", type: "color", "aria-label": t("style.fillColor") });
      fillColor.value = style.fill?.color && /^#[0-9a-f]{6}$/i.test(style.fill.color) ? style.fill.color : "#d9e2ec";
      const apply = () => {
        const k = fill.value;
        if (!k) return;
        set({ fill: k === "solid" ? { kind: "solid", color: fillColor.value } : k === "hatch" ? { kind: "hatch", hatch: { angle: 45, spacing: 2 } } : { kind: "none" } });
      };
      fill.addEventListener("change", apply);
      fillColor.addEventListener("change", () => { fill.value = "solid"; apply(); });
      out.push(field(t("style.fill"), h("div", { class: "isk-row" }, fill, fillColor)));
    } else {
      for (const end of ["arrowStart", "arrowEnd"] as const) {
        const sel = h("select", { class: "isk-input", "aria-label": t(`style.${end}`) });
        for (const a of ARROWS) sel.append(h("option", { value: a, selected: (style[end] ?? "none") === a }, t(`arrow.${a}`)));
        sel.addEventListener("change", () => set({ [end]: sel.value as ArrowHead }));
        out.push(field(t(`style.${end}`), sel));
      }
    }
    const reset = h("button", { class: "isk-btn isk-link", type: "button" }, t("style.reset"));
    reset.addEventListener("click", () => this.edit((s, ids) => s.setStyle(ids, {}, true)));
    out.push(h("div", { class: "isk-field" }, reset));
    return out;
  }
}

function field(label: string, control: HTMLElement): HTMLElement {
  return h("label", { class: "isk-field" }, h("span", { class: "isk-field-label" }, label), control);
}
