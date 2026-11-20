// Light/dark theme. "auto" follows the Office theme (task pane background) when available, otherwise
// prefers-color-scheme of the browser/webview.

export type Theme = "light" | "dark";
export type ThemeSetting = "auto" | Theme;

/** Relative luminance (0..1) of #rgb/#rrggbb; NaN for anything else. */
export function luminance(color: string | undefined): number {
  if (!color) return NaN;
  let c = color.trim().replace(/^#/, "");
  if (c.length === 3) c = c.split("").map((ch) => ch + ch).join("");
  if (!/^[0-9a-f]{6}/i.test(c)) return NaN;
  const v = parseInt(c.slice(0, 6), 16);
  const r = (v >> 16) & 255, g = (v >> 8) & 255, b = v & 255;
  return (0.2126 * r + 0.7152 * g + 0.0722 * b) / 255;
}

export interface ThemeInputs {
  setting: ThemeSetting;
  /** Office.context.officeTheme?.bodyBackgroundColor (task pane background of the Office theme). */
  officeBodyBackground?: string;
  /** matchMedia("(prefers-color-scheme: dark)").matches */
  prefersDark?: boolean;
}

export function resolveTheme(i: ThemeInputs): Theme {
  if (i.setting !== "auto") return i.setting;
  const l = luminance(i.officeBodyBackground);
  if (!Number.isNaN(l)) return l < 0.5 ? "dark" : "light";
  return i.prefersDark ? "dark" : "light";
}

export function applyTheme(root: HTMLElement, theme: Theme): void {
  root.dataset.theme = theme;
  document.documentElement.dataset.theme = theme;
  document.documentElement.style.colorScheme = theme;
}
