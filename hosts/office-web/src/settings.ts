// Per-user settings in localStorage (task pane conveniences only; the model lives in the document).

import type { LangSetting } from "./i18n";
import type { ThemeSetting } from "./theme";

export interface Settings {
  theme: ThemeSetting;
  lang: LangSetting;
  snap: boolean;
  /** Snapped line ends attach to their target and follow it (like "Anchor" in the COM add-in). */
  anchor: boolean;
  grid: number;
  liveSync: boolean;
  favourites: string[];
  /** Library items inserted last (insert box), most recent first. */
  recent: string[];
  libraryOpen: boolean;
  inspectorOpen: boolean;
}

const KEY = "inventasketch.settings.v1";

export const defaults: Settings = {
  theme: "auto",
  lang: "auto",
  snap: true,
  anchor: true,
  grid: 5,
  liveSync: true,
  favourites: [],
  recent: [],
  libraryOpen: true,
  inspectorOpen: false,
};

export function loadSettings(storage: Pick<Storage, "getItem"> | undefined = safeStorage()): Settings {
  try {
    const raw = storage?.getItem(KEY);
    if (!raw) return { ...defaults };
    return { ...defaults, ...(JSON.parse(raw) as Partial<Settings>) };
  } catch {
    return { ...defaults };
  }
}

export function saveSettings(s: Settings, storage: Pick<Storage, "setItem"> | undefined = safeStorage()): void {
  try {
    storage?.setItem(KEY, JSON.stringify(s));
  } catch {
    /* private mode or blocked storage: settings stay in memory */
  }
}

function safeStorage(): Storage | undefined {
  try {
    return typeof localStorage === "undefined" ? undefined : localStorage;
  } catch {
    return undefined;
  }
}
