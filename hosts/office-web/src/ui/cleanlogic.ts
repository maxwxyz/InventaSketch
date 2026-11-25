// "Clean slides …" (like the COM add-in's Review command): pure helpers for the form, the engine request and the report.
// The cleaning itself is the engine's `sanitize`; the App reads and writes the slides.

import type { SanitizeMode, SanitizeOptions, SanitizeReport } from "../engine/types";

/** What is cleaned: the selected elements, the current slide (outside PowerPoint: the editor page) or every slide. */
export type CleanScope = "selection" | "slide" | "all";

/** The choices of the form. */
export interface CleanForm {
  scope: CleanScope;
  mode: SanitizeMode;
  renameCategories: boolean;
  replaceTexts: boolean;
  stripMeta: boolean;
  /** Typed seed; empty = a new one each time. */
  seed: string;
}

/** A scope radio of the form: its text key and whether it can be chosen. */
export interface CleanScopeOption { scope: CleanScope; key: string; enabled: boolean }

/** Defaults as in the COM dialog: the selection if there is one, else the slide; normalised; metadata removed. */
export function defaultCleanForm(hasSelection: boolean): CleanForm {
  return { scope: hasSelection ? "selection" : "slide", mode: "normalised", renameCategories: false, replaceTexts: false, stripMeta: true, seed: "" };
}

/**
 * The scopes offered: "Selected elements" (only with a selection), "Current slide" with access to the slides (else
 * "Whole page" of the editor) and "All slides" only with access to the slides.
 */
export function cleanScopes(hasSelection: boolean, slideAccess: boolean): CleanScopeOption[] {
  const out: CleanScopeOption[] = [
    { scope: "selection", key: "clean.scope.selection", enabled: hasSelection },
    { scope: "slide", key: slideAccess ? "clean.scope.slide" : "clean.scope.page", enabled: true },
  ];
  if (slideAccess) out.push({ scope: "all", key: "clean.scope.all", enabled: true });
  return out;
}

/** A seed typed by the user: a whole number in the 32-bit range (like the engine's TryGetInt32), else none. */
export function parseSeed(text: string): number | undefined {
  const s = text.trim();
  if (!/^[+-]?\d+$/.test(s)) return undefined;
  const n = Number(s);
  return Number.isSafeInteger(n) && n >= -2147483648 && n <= 2147483647 ? n : undefined;
}

/** A fresh seed (1 … 2^31 − 1), as the engine picks one. */
export function randomSeed(random: () => number = Math.random): number {
  return 1 + Math.floor(random() * 2147483646);
}

/** The engine request of the form; one seed for the whole run (all slides), so the report can reproduce it. */
export interface CleanRequest { scope: CleanScope; mode: SanitizeMode; value: SanitizeOptions }

export function cleanRequest(form: CleanForm, seed: () => number = randomSeed): CleanRequest {
  const scope = form.scope;
  return {
    scope,
    mode: form.mode,
    value: {
      renameCategories: form.renameCategories,
      replaceTexts: form.replaceTexts,
      stripMeta: form.stripMeta,
      seed: parseSeed(form.seed) ?? seed(),
    },
  };
}

/** Sums of several engine reports (one per slide). */
export interface CleanTotals { seed: number; elements: number; changed: number; lines: string[]; slides: number }

export function emptyTotals(seed: number): CleanTotals {
  return { seed, elements: 0, changed: 0, lines: [], slides: 0 };
}

/** Adds one report (a slide with elements counts as a cleaned slide when something on it changed). */
export function addReport(total: CleanTotals, r: SanitizeReport): CleanTotals {
  return {
    seed: r.seed || total.seed,
    elements: total.elements + r.elements,
    changed: total.changed + r.changed,
    lines: [...total.lines, ...r.lines],
    slides: total.slides + (r.changed > 0 ? 1 : 0),
  };
}

/** The report for the user (like COM): head with counts and seed, at most `maxLines` distinct lines, the note. */
export function formatCleanReport(total: CleanTotals, t: (key: string, ...args: (string | number)[]) => string, maxLines = 12): { head: string; lines: string[]; note: string } {
  const lines = [...new Set(total.lines.filter((l) => l.trim()))].slice(0, maxLines);
  return { head: t("clean.head", total.changed, total.elements, total.seed), lines, note: t("clean.note") };
}
