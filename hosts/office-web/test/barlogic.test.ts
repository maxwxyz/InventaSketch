import { describe, expect, it } from "vitest";
import type { ActionInfo, VarInfo } from "../src/engine/types";
import {
  ELEMENT_CHOICE, PART_CHOICE, actionEntries, barActions, choiceFace, choicePick, choicePieLabel, choiceTip, currentOption, groupActionChoices, isPictorial,
  pickBar, shortLabel, type ChoiceEntry,
} from "../src/ui/barlogic";
import { cardDigits, fieldText, provenanceMarker, visibleVars } from "../src/ui/valuecard";
import { codeReportLines, isCodeName } from "../src/ui/tools";
import { fadeOpacity } from "../src/ui/contextbar";
import strings from "../src/strings.json";

const act = (id: string, extra: Partial<ActionInfo> = {}): ActionInfo =>
  ({ id, label: id, icon: "cmd-option", enabled: true, active: false, group: "", kind: "type", ...extra });
const image = (svg: string) => `img:${svg.length}`;

/** A sketched curve's actions as the engine reports them: a state choice with previews, a menu, plain actions. */
const curve: ActionInfo[] = [
  act("chart.smooth", { icon: "cmd-smooth" }),
  act("shape=smooth", { label: "Smoothed", choice: "chart.curve.shape", choiceLabel: "Curve shape", previewSvg: "<svg>a</svg>", active: true }),
  act("shape=linear", { label: "Straight segments", choice: "chart.curve.shape", choiceLabel: "Curve shape", previewSvg: "<svg>bb</svg>" }),
  act("fill=below", { label: "Below", choice: "chart.fill", choiceLabel: "Fill to", choiceMenu: true, icon: "cmd-hatch-below" }),
  act("fill=curve", { label: "Curve 2", choice: "chart.fill", choiceLabel: "Fill to", choiceMenu: true, input: { label: "Curve", value: "2" } }),
  act("part.hide", { icon: "cmd-visibility" }),
];

describe("choices among actions (dropdowns, never cycles)", () => {
  it("groups the options of one choice into one dropdown at its first option", () => {
    const { choices, plain } = groupActionChoices(curve, PART_CHOICE, image);
    expect(choices.map((c) => c.key)).toEqual(["@p.chart.curve.shape", "@p.chart.fill"]);
    expect(plain.map((a) => a.id)).toEqual(["chart.smooth", "part.hide"]);
    const shape = choices[0];
    expect(shape.label).toBe("Curve shape");
    expect(shape.current).toBe("shape=smooth");
    // Options without a telling pictogram carry their preview picture.
    expect(shape.options.map((o) => o.image)).toEqual(["img:12", "img:13"]);
    expect(shape.options.every((o) => !o.icon)).toBe(true);
    const fill = choices[1];
    expect(fill.menu).toBe(true);
    expect(fill.current).toBe("");
    expect(fill.options[0].icon).toBe("cmd-hatch-below");
    expect(fill.options[1].label).toBe("Curve 2 …");
    expect(actionEntries(curve, PART_CHOICE, image).map((e) => ("choice" in e ? e.choice.key : e.action.id)))
      .toEqual(["chart.smooth", "@p.chart.curve.shape", "@p.chart.fill", "part.hide"]);
  });

  it("shows the choice pictogram, else the current name (labelInBar), else the current picture; menus as Label ▸", () => {
    const { choices } = groupActionChoices(curve, ELEMENT_CHOICE, image);
    expect(choiceFace(choices[0])).toEqual({ kind: "image", image: "img:12" });
    expect(choiceFace(choices[1])).toEqual({ kind: "text", text: "Fill to ▸" });
    expect(choiceFace({ ...choices[0], labelInBar: true })).toEqual({ kind: "text", text: "Smoothed" });
    expect(choiceFace({ ...choices[0], barIcon: "cmd-smooth" })).toEqual({ kind: "icon", icon: "cmd-smooth" });
    const text: ChoiceEntry = { key: "k", label: "Kind", current: "b", options: [{ id: "a", label: "A" }, { id: "b", label: "B" }] };
    expect(choiceFace(text)).toEqual({ kind: "text", text: "B" });
    expect(isPictorial(text)).toBe(false);
    expect(isPictorial(choices[0])).toBe(true);
    expect(choiceTip(choices[0], "click to choose")).toBe("Curve shape: Smoothed – click to choose");
    expect(choicePieLabel(choices[0])).toBe("Curve shape: Smoothed ▸");
    expect(choicePieLabel(choices[1])).toBe("Fill to ▸");
    expect(currentOption(choices[1])).toBeUndefined();
  });

  it("takes the choice's own bar pictogram only when it is a known, telling one", () => {
    const withIcon = [act("a=1", { choice: "c", choiceBarIcon: "cmd-smooth", active: true }), act("a=2", { choice: "c" })];
    expect(groupActionChoices(withIcon, ELEMENT_CHOICE, image).choices[0].barIcon).toBe("cmd-smooth");
    const unknown = [act("a=1", { choice: "c", choiceBarIcon: "no-such-icon" }), act("a=2", { choice: "c" })];
    expect(groupActionChoices(unknown, ELEMENT_CHOICE, image).choices[0].barIcon).toBeUndefined();
  });

  it("maps a pick to what it does", () => {
    expect(choicePick("@a.chart.area.add", "area=hatch")).toEqual({ kind: "action", id: "area=hatch" });
    expect(choicePick("@p.chart.curve.shape", "shape=linear")).toEqual({ kind: "part", id: "shape=linear" });
    expect(choicePick("@view", "view:side")).toEqual({ kind: "view", id: "view:side" });
    expect(choicePick("@type", "ann.arrow")).toEqual({ kind: "convert", typeId: "ann.arrow" });
    expect(choicePick("@dash", "dash")).toEqual({ kind: "dash", dash: "dash" });
    expect(choicePick("hub", "keyed")).toEqual({ kind: "param", key: "hub", value: "keyed" });
  });
});

describe("bar of one element (main action, up to three pictograms, ⋯)", () => {
  it("keeps the main action and three others, without duplicates", () => {
    const c = [{ id: "insert" }, { id: "text", main: true }, { id: "sketch" }, { id: "insert" }, { id: "choice:@view" }, { id: "rotate90" }];
    const { main, icons } = pickBar(c);
    expect(main?.id).toBe("text");
    expect(icons.map((x) => x.id)).toEqual(["insert", "sketch", "choice:@view"]);
  });

  it("offers only telling, unshared pictograms of enabled plain actions; steps keep their label", () => {
    const list = barActions([
      act("t:teeth+", { icon: "cmd-count-more" }), act("t:teeth-", { icon: "cmd-count-more" }),
      act("t:a", { icon: "cmd-swap" }), act("t:b", { icon: "cmd-swap" }),
      act("t:c", { icon: "cmd-helix" }), act("t:off", { icon: "cmd-home", enabled: false }),
      act("t:toggle", { icon: "cmd-toggle" }), act("t:del", { icon: "cmd-delete" }),
      act("t:hub", { icon: "cmd-helix", paramKeys: ["hub"] }), act("t:opt", { icon: "cmd-option", choice: "x" }),
      act("t:text", { icon: "cmd-text" }),
    ], new Set(["hub"]), true);
    expect(list.filter((x) => x.pictorial).map((x) => x.action.id)).toEqual(["t:teeth+", "t:teeth-", "t:c"]);
    expect(list.filter((x) => !x.pictorial).map((x) => x.action.id)).toEqual(["t:a", "t:b"]);
  });

  it("shortens labels at a word boundary", () => {
    expect(shortLabel("Line style (dashes)")).toBe("Line style");
    expect(shortLabel("Arrowheads at both ends of the line", 20)).toBe("Arrowheads at both…");
  });

  it("stays fully visible near the pointer and fades out far away (UX review G1)", () => {
    expect(fadeOpacity(60)).toBe(1);
    expect(fadeOpacity(90)).toBe(1);
    expect(fadeOpacity(255)).toBeCloseTo(0.5, 1);
    expect(fadeOpacity(500)).toBe(0);
  });
});

describe("value card and tools", () => {
  const v = (key: string, extra: Partial<VarInfo> = {}): VarInfo => ({
    key, name: key, symbol: key, kind: "voltage", relevance: "card", unit: "V", unitFactor: 1, unitOffset: 0, units: ["V"],
    display: "", provenance: "unknown", conflict: false, param: key, ...extra,
  });

  it("marks where a value comes from", () => {
    expect(provenanceMarker(v("U", { provenance: "entered" }))).toBe("✎");
    expect(provenanceMarker(v("U", { provenance: "derived" }))).toBe("=");
    expect(provenanceMarker(v("U", { provenance: "default" }))).toBe("○");
    expect(provenanceMarker(v("U", { provenance: "linked" }))).toBe("⇄");
    expect(provenanceMarker(v("U", { provenance: "entered", conflict: true }))).toBe("⚠");
    expect(provenanceMarker(v("U"))).toBe("");
  });

  it("lists the card values, all of them in Engineer, with the text as typed", () => {
    const vars = [v("R"), v("T", { relevance: "engineer" }), v("P", { relevance: "panel" })];
    expect(visibleVars(vars, "explore").map((x) => x.key)).toEqual(["R"]);
    expect(visibleVars(vars, "engineer").map((x) => x.key)).toEqual(["R", "T", "P"]);
    expect(cardDigits("engineer")).toBe(4);
    expect(cardDigits("quick")).toBe(3);
    expect(fieldText(v("R", { provenance: "entered", text: "4k7", display: "4.7 kΩ" }))).toBe("4k7");
    expect(fieldText(v("I", { provenance: "derived", display: "2 A" }))).toBe("2 A");
  });

  it("reports a code import in short lines", () => {
    const lines = codeReportLines({ message: "Imported; 2 part(s) were not imported.", imported: ["Chart: 2 curves"], skipped: [{ line: 4, code: "plt.annotate('x')", reason: "Annotations are not imported" }, { line: 0, code: "", reason: "Unknown" }] });
    expect(lines).toEqual(["Imported; 2 part(s) were not imported.", "✓ Chart: 2 curves", "– 4: Annotations are not imported (plt.annotate('x'))", "– Unknown"]);
    expect(isCodeName("plot.py") && isCodeName("fig.TEX") && !isCodeName("data.csv")).toBe(true);
  });

  it("has both languages for every new text", () => {
    const table = strings as Record<string, { en: string; de: string }>;
    const keys = Object.keys(table).filter((k) => /^(bar\.|val\.|calc\.|code\.|connect\.|pie\.(edit|style|view|arrange|delete|duplicate|values|hint\.))/.test(k));
    expect(keys.length).toBeGreaterThan(40);
    for (const k of keys) {
      expect(table[k].en, k).toBeTruthy();
      expect(table[k].de, k).toBeTruthy();
      // Never the word "patent" in what users read.
      expect(`${table[k].en} ${table[k].de}`.toLowerCase(), k).not.toContain("patent");
    }
  });
});
