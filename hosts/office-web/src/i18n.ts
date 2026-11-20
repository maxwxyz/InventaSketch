// Bilingual UI texts (src/strings.json). German if the host UI language starts with "de", otherwise English.

import strings from "./strings.json";

export type Lang = "en" | "de";
export type LangSetting = "auto" | Lang;
type Table = Record<string, { en: string; de: string }>;

const table = strings as Table;
let current: Lang = "en";

/** German if the tag starts with "de" (case-insensitive), English otherwise. Mirrors Core's Language.Detect. */
export function detectLang(tag: string | undefined | null): Lang {
  return tag && tag.trim().toLowerCase().startsWith("de") ? "de" : "en";
}

/** Resolves the setting against the Office display language (or the browser language without Office). */
export function resolveLang(setting: LangSetting, officeDisplayLanguage?: string, browserLanguage?: string): Lang {
  if (setting !== "auto") return setting;
  return detectLang(officeDisplayLanguage ?? browserLanguage);
}

export function setLang(lang: Lang): void {
  current = lang;
}

export function lang(): Lang {
  return current;
}

/** Text for a key; "{0}"… are replaced by the arguments. Unknown keys return the key (visible in tests). */
export function t(key: string, ...args: (string | number)[]): string {
  const entry = table[key];
  let s = entry ? entry[current] || entry.en : key;
  args.forEach((a, i) => (s = s.replace(`{${i}}`, String(a))));
  return s;
}

export function hasText(key: string): boolean {
  return key in table;
}

export function allKeys(): string[] {
  return Object.keys(table);
}
