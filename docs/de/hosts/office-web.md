# PowerPoint-Web-Add-in (Office.js)

> English (maßgeblich): [../../en/hosts/office-web.md](../../en/hosts/office-web.md)

## Architektur

- **Engine:** die C#-Engine (`src/InventaSketch.Engine` + Module), kompiliert zu .NET WebAssembly durch
  `hosts/office-web/engine/InventaSketch.Web.Engine.csproj` (`Microsoft.NET.Sdk.WebAssembly`, kein Workload nötig).
  TypeScript ruft sie über `[JSExport]`-Methoden mit JSON-Strings auf (`src/engine/*`). Größe: ca. 2,1 MB Brotli;
  Start im Browser ca. 0,5 s (warm) bis 5 s (kalt, gemessen am 2026-11-20 im Dev-Build).
- **Oberfläche (TypeScript, Vite):** Bibliothek mit Suche (EN/DE), Kategorien und Favoriten; Bühne mit
  Auswahlgriffen, Fanghilfslinien, Einfügen am Mauszeiger, Kreismenü (Rechtsklick / `Umschalt+F10`), schwebende
  Kontextleiste, Inspektor; Hell/Dunkel/Automatisch; automatische Sprache Deutsch/Englisch (`src/ui/*`,
  `src/theme.ts`, `src/i18n.ts`).
- **Office-Adapter:** `src/office/*` übersetzt Zeichenpläne der Engine in PowerPoint-Formen (abhängig von den
  Host-Fähigkeiten), reiht alle `PowerPoint.run`-Aufrufe in eine Warteschlange und speichert IDs/Modell in Shape-Tags.
- **Manifest:** erzeugt von `scripts/manifest.mjs` (Basis-URL als Argument oder `ISK_BASE_URL`), mit
  `EquivalentAddins` auf das COM-Add-in `InventaSketch.AddIn.Connect`, sodass Windows das Web-Add-in ausblendet,
  solange das COM-Add-in installiert ist.

## Engine-API

Alle Aufrufe laufen über `EngineExports.Call(method, json)` und liefern `{"ok":true,"result":…}` oder
`{"ok":false,"error":"…"}` (`src/InventaSketch.Engine/EngineApi.cs`, Anfrageobjekt `Contracts.cs`, typisierter Client
`src/engine/client.ts`). Anfragen sind camelCase-JSON; Geometrie in mm (Zeichenpläne und Host-Rechtecke in pt). Jede
Bearbeitungsmethode erhält `session`; `ids` ist standardmäßig die aktuelle Auswahl. Änderungen liefern den
Editorzustand und sind je ein Rückgängig-Schritt (kein Schritt, wenn sich nichts ändert).

| Methode | Anfrage | Ergebnis |
|---|---|---|
| `version` | – | Engine-Version, Module, Methodenliste |
| `createSession`, `newDocument`, `openDocument` | `lang`, `pageWidth`, `pageHeight`, `title`, `json` | Zustand |
| `saveDocument` | `indented` | `{ json }` |
| `setPage` | `pageWidth`, `pageHeight`, `lang` | Zustand |
| `state` | `includeSvg` (Standard true) | Zustand |
| `catalog` | `lang` | Kategorien und Bibliothekselemente mit Vorschaubildern |
| `insert` | `itemId`, `x`, `y`, `snap`, `tolerance` | Zustand (neues Objekt ausgewählt) |
| `move`, `scale`, `resize`, `rotate`, `flip` | `dx`/`dy`; `pivotX`/`pivotY`, `sx`/`sy`; `id`, `w`, `h`, `x`, `y`; `degrees`; `horizontal` | Zustand |
| `duplicate`, `delete`, `group`, `ungroup`, `reorder` | `ids`, `dx`/`dy`, `to` (front, back, forward, backward) | Zustand |
| `setView` | `view` | Zustand; Größe verzerrungsfrei angepasst (`Registry.WithView`) |
| `setParam`, `setStyle`, `setText`, `setPoint` | `key`/`value`; `style`/`replace`; `text`; `id`/`index`/`x`/`y`/`snap`/`anchor` (false: einrasten ohne anzuhängen) | Zustand |
| `setMainText` | `id` (Standard: erstes ausgewähltes Objekt mit Haupttext), `text` | Zustand; setzt den Haupttext des Objekts (unten) |
| `setPlanScale` | `value` (> 0 setzt, null löscht den Planmaßstab des Dokuments, 100 = 1:100) | Zustand |
| `setMonochrome` | `value` (true/false) | Zustand; Schwarz-Weiß für das Dokument, jedes Objekt gilt als geändert |
| `applyAction` | `action` (ID aus `contextActions`), `input` bei Eingabeaktionen | Zustand |
| `undo`, `redo`, `select`, `selectRect` | `ids`; `x`, `y`, `w`, `h`, `mode` (enclosed/touching), `add` | Zustand |
| `hitTest` | `x`, `y`, `tolerance` | `{ id, ids, stack }` |
| `snapMove`, `snapPoint`, `snapInsert`, `setSnap` | `dx`/`dy`; `x`/`y`/`exclude`; `itemId`; `snapOptions` | Fangergebnis `{ dx, dy, x, y, kind, guides, attach }` |
| `contextActions`, `inspector` | `ids`, `lang` | Aktionen (allgemein, Ansichten, Typaktionen); Inspektordaten |
| `suggestions` | `ids`, `lang`, `max` (Standard 9) | Bibliothekselemente `{ id, typeId, name, view, category, points }`, die zum ersten ausgewählten Objekt passen („Einfügen ▸“: Begleiter, gleiche Kategorie, Beschriftungen, in seiner Art von Ansicht); leer ohne Auswahl |
| `conversions`, `convert` | `ids`, `lang`; `ids`, `itemId` (Zieltyp) | Linientypen, in die die erste ausgewählte Linie, der Pfeil, das Maß oder die Hinweislinie umgewandelt werden kann („Typ ▸“, `{ id, label, active }`); Zustand (Punkte, Enden und Anhänge bleiben) |
| `quickAdd`, `nextItems`, `replaceItem` | `id`, `side` (left/top/right/bottom), optional `x`/`y`, `itemId`; `id`, `max`; `id`, `itemId` | Zustand mit dem neuen Element ausgewählt (Kopie plus Verbinder, ein Rückgängig-Schritt); Bibliothekselemente für das nächste Element; Zustand (Element ersetzt, Verbinder bleiben) |
| `exporters`, `export` | `ids` (Auswahl mit ihren Gruppen, sonst die Seite), `lang`; `itemId` (Exporter-Id) | zutreffende Fachexporte `{ id, name, extension, mime, preserves }`; `{ id, name, extension, mime, text }` |
| `renderPageSvg` | `ids`, `monochrome` (Standard: Dokumenteinstellung), `background`, `padding` | `{ svg, width, height }` |
| `drawPlan` | `ids`, `capabilities` | Zeichenplan in pt (schwarz-weiß, wenn das Dokument es ist) |
| `exportCompact`, `importCompact`, `hostTransform` | `ids`; `payload`, `add`; `from`, `target`, `rotation` | Payload; Zustand |

Editorzustand: `session`, `revision`, `pageId`, `pageWidth`, `pageHeight`, `grid`, `selection`, `selectionBounds`,
`canUndo`, `canRedo`, `undoLabel`, `objects`, `svg` (Seiten-SVG, schwarz-weiß, wenn das Dokument es ist), `planScale`,
`monochrome`, `changed` (durch die Änderung dieser Revision hinzugefügte oder geänderte Objekte, auch bei
Rückgängig/Wiederholen; nach `setMonochrome` alle Objekte, nach `setPlanScale` alle Objekte mit Planmaßstab) und
`dependents` (weitere Objekte, deren Darstellung von den geänderten oder entfernten abhängt: Wandanschlüsse,
Öffnungen). Hosts zeichnen die Einheiten aus `changed` und `dependents` in einem Schritt neu. Jedes Objekt enthält ID,
Typ, Name, Art (box/points), Grenzen, Mitte, Größe, Drehung, Spiegelung, Punkte, Gruppe, Ansicht, Text und Bezug;
**ausgewählte** Objekte zusätzlich `centred` (Teilkreis- oder Nabenanker: standardmäßig um die Mitte skalieren) und
`mainText` `{ key, label, value }`: das Bezugszeichen bei Bezugszeichen und Bezugslinien, sonst der Objekttext bei
Texttypen, sonst der erste Textparameter aus text, label, name und title (`key` fehlt beim Objekttext). `importCompact`
übernimmt die Schwarz-Weiß-Einstellung des Folienmodells, damit Live-Neuzeichnungen das Aussehen der Einheiten auf der
Folie behalten.

## Bearbeitung (gleichwertig zum COM-Add-in, 2026-11-20)

- **Kreismenü** (Rechtsklick, `Umschalt+F10`, „…“ in der Leiste): ein Ring aus Sektoren mit Piktogramm über jeder
  Beschriftung, höchstens acht Einträge; zuerst Löschen, Ansichten (bis zu vier), „Text …“, „Einfügen ▸“, Duplizieren
  und Drehen, Typaktionen, Bezugszeichen, Spiegeln, Reihenfolge und Gruppierung unter „Mehr ▸“ (seitenweise mit
  Zurück). Beschriftungen brechen nur an Leerzeichen um (zwei Zeilen, kleinere Schrift vor dem Kürzen mit „…“); die
  volle Beschriftung eines gekürzten oder nicht verfügbaren Eintrags erscheint in einer Pille unter dem Ring. Layout
  und Umbruch sind reine Funktionen in `src/ui/pielogic.ts` (mit Unit-Tests).
- **Text**: Doppelklick auf ein Objekt, die Text-Schaltfläche der Kontextleiste oder „Text …“ im Kreismenü bearbeiten
  den Haupttext (`mainText`, `setMainText`) in der Inline-Eingabe; mehrzeilige Texte öffnen das Textfeld des Inspektors.
- **Einfügen ▸** listet die `suggestions` der Engine; die Auswahl startet das Einfügen am Mauszeiger auf der Bühne (mit
  Fangen). In der Seitenleiste des COM-Add-ins sendet sie die vorhandene Bridge-Nachricht `place`, sodass das Element
  auf der Folie platziert wird.
- **Schwarz-Weiß**: Umschalter in der oberen Leiste (und in den Einstellungen) ruft `setMonochrome`; Bühne,
  SVG/PNG-Export und die von `src/office/powerpoint.ts` (oder der COM-Bridge) geschriebenen Zeichenpläne folgen der
  Dokumenteinstellung. Erneutes Klicken stellt die Farben wieder her; Rückgängig funktioniert.
- **Skalieren**: zentrierte Teile (Zahnräder, Lager, Räder: `centred`) skalieren standardmäßig um ihre Mitte, mit Strg
  von der Gegenkante; andere Objekte umgekehrt. Eine Beschriftung neben dem Mauszeiger zeigt Größe (oder Faktor) und den
  aktiven Modus mit der Strg-Alternative.
- **Verankern** (Einstellungen, standardmäßig an, wie der Menüband-Schalter des COM-Add-ins): Ein auf ein Fangziel
  gezogenes Linienende hängt daran und folgt, wenn das Ziel verschoben wird. Aus: Das Ende rastet ein, bleibt aber stehen.
- **Kreismenü „Beschriftung ▸“** für Linien, Pfeile, Maße und Hinweislinien: Einträge aus den Typparametern
  (Texteingabe, Schalter, Auswahl, Textgröße), wie im COM-Add-in.

## Bauen und starten

```powershell
cd hosts/office-web
npm ci
npm test            # vitest (Manifest, Bibliotheksfilter, Kreismenü-Layout, Umbruch, Skalieren)
npm run build       # Engine-Publish + Icons + Typprüfung + Vite-Build + Manifest -> dist/
npm run dev         # Dev-Server auf Port 3000 (https, wenn office-addin-dev-certs installiert sind)
```

`dev.html` startet dieselbe App ohne Office (Engine in WebAssembly, Folienbefehle ausgeblendet) für UI-Arbeit in
jedem Browser.

## Installation für Anwender (ohne Administratorrechte)

`scripts\package-web.ps1` baut `dist\InventaSketch-Web` (und ein ZIP): `App\` (Aufgabenbereich +
WebAssembly-Engine), `Server\InventaSketch.WebHost.exe`, `manifest.xml` für `https://localhost:3217/` (Slidemax-Web
nutzt 3000) und die Installationsskripte mit `README.md` / `README.de.md`. Lokaler Host und Skripte wurden mit
Zustimmung des Rechteinhabers aus Slidemax übernommen und ins Englische übersetzt; Konsolenausgaben, Meldungen und Hilfe
erscheinen auf Deutsch, wenn die Sprache des Benutzers Deutsch ist (Windows-Anzeigesprache bzw. `LC_ALL`/
`LC_MESSAGES`/`LANG` unter macOS/Linux), sonst auf Englisch.

- **Windows:** PowerPoint schließen, `Install web add-in.cmd` doppelklicken, die Zertifikatsabfrage von Windows
  einmal bestätigen. Kopiert nach `%LOCALAPPDATA%\InventaSketch\Web`, erzeugt ein `localhost`-Zertifikat im
  Benutzerspeicher und vertraut ihm, legt einen Autostart-Eintrag an, startet den nur lokal erreichbaren HTTPS-Host und
  registriert das Manifest zum Querladen (`HKCU\Software\Microsoft\Office\16.0\WEF\Developer`). Optionen:
  `--no-autostart`, `--no-manifest`, `--dry-run` (die deutschen Namen `-OhneAutostart`, `-OhneManifest`,
  `-Probelauf` gelten weiterhin). Deinstallation: `Uninstall web add-in.cmd`; Starten/Beenden ohne Neuinstallation:
  `Start web add-in.cmd`, `Stop web add-in.cmd`. `Server\InventaSketch.WebHost.exe --help` listet alle Optionen.
  Solange das COM-Add-in installiert ist, blendet Office das Web-Add-in aus.
- **macOS:** `install.command` (Python 3): kopiert nach `~/Library/Application Support/InventaSketch/Web`,
  vertraut dem Zertifikat im Schlüsselbund, LaunchAgent, Manifest im `wef`-Ordner von PowerPoint. Deinstallation:
  `uninstall.command`.
- **Linux / PowerPoint im Web:** `sh install.sh`, danach `manifest.xml` über *Add-Ins › Meine Add-Ins ›
  Mein Add-In hochladen* einbinden. Deinstallation: `sh uninstall.sh`; `start.sh` / `stop.sh` steuern den Server.
  Optionen von `inventasketch_web.py`: `--no-autostart`, `--dry-run` (deutsche Aliase `--ohne-autostart`,
  `--probelauf`).
- **Eigener HTTPS-Server (IT):** `create-manifest.ps1 -Address https://...` bzw. `sh create-manifest.sh https://...`
  schreibt `manifest-<host>.xml` für den anderswo bereitgestellten Ordner `App`.

Geprüft am 2026-11-20 unter Windows: Paketbau, Probelauf des Installers (Plan inkl. COM-Erkennung), Host mit
temporärem In-Memory-Zertifikat lieferte `taskpane.html` und die Engine per HTTPS aus, wies `../`-Pfade ab, Status und
Stopp funktionierten; Linux-Probelauf des Python-Installers. Noch offen: echte Installation mit Zertifikatsvertrauen
(braucht die Bestätigung des Anwenders), macOS-Lauf, Laden des Add-ins in PowerPoint. Nach der Übersetzung
(2026-11-20): Host-Build ohne Warnungen, Paketbau, Probeläufe des Installers auf Englisch und Deutsch (neue Optionsnamen
und deutsche Aliase), HTTPS-Kurztest (200, `../` 404, fremder Host 400, POST 405), Python-Probeläufe (Linux, simuliertes
macOS) in beiden Sprachen.

## Querladen (Entwicklung)

Windows: einmal `npm run certs` (vertrauenswürdiges localhost-Zertifikat), `npm run dev`, dann den Manifestpfad als
Wert unter `HKCU\Software\Microsoft\Office\16.0\WEF\Developer` eintragen (Name = Add-in-ID) und PowerPoint neu
starten. Für Anwender den oben beschriebenen lokalen Installer verwenden.

## Stand (2026-11-20)

| Funktion | Stand |
|---|---|
| Engine in WebAssembly, Katalog, Einfügen am Mauszeiger, Suche | im Browser geprüft (dev.html) |
| Bühneninteraktion (Verschieben, Skalieren, Fangen, Kreismenü, schwebende Leiste) | umgesetzt, Browserprüfung teilweise |
| Kreismenü mit „Mehr ▸“/„Einfügen ▸“, Textbearbeitung, Schwarz-Weiß, Skalieren um die Mitte | im Browser geprüft (dev.html, 2026-11-20) |
| Schreiben nativer Formen in PowerPoint | umgesetzt, **noch nicht in PowerPoint getestet** |
| Lokaler Installer wie beim COM-Add-in (ohne Admin) | umgesetzt, Probelauf geprüft; echte Installation offen |
| Webspace-Paket (Downloadseite, Manifest für eigene URL) | umgesetzt; über lokales HTTPS geprüft, echter Webspace offen |

Grenzen von Office.js (siehe [Host-Fähigkeiten](capability-matrix.md)): keine Freiformen, Pfeilspitzen oder
Musterfüllungen auf Formen; kein Overlay auf der Folie; iPad nur PowerPointApi 1.1.

## Webspace-Paket

`dist/InventaSketch-Webspace` (und `dist/InventaSketch-Webspace-<Version>.zip`, das den Ordner enthält) stellt das
Web-Add-in von einem beliebigen HTTPS-Webspace bereit, ohne Server auf dem Rechner des Anwenders. Bau mit
`scripts/package-webspace.ps1` (Windows) bzw. `scripts/package-webspace.sh` (Linux/macOS); `scripts/build.ps1` und
`scripts/build.sh` rufen es nach dem Web- und dem COM-Paket auf und warnen nur, wenn der Web-Build fehlt. Die Arbeit
erledigt `hosts/office-web/webspace/package.mjs` (`npm run webspace`).

Inhalt:

- das gebaute Web-Add-in (`hosts/office-web/dist` ohne `dev.html`, Quelltextkarten und localhost-Manifest), samt
  vorkomprimierter Engine-Dateien (`_framework/*.br`, `*.gz`);
- `index.html` mit `setup.js`: die Einrichtungsseite. Sie nimmt den Ordner ihrer eigenen Adresse (`location`, ohne
  Dateiname, Abfrage und Anker) als Adresse des Add-ins, prüft sie nach den Regeln von `create-manifest.ps1` (nur HTTPS,
  ohne Abfrage oder Sonderzeichen), zeigt sie an und bietet das Manifest als `manifest-<host>[-<port>].xml` auf Knopfdruck
  an. Dazu erklärt sie "Mein Add-In hochladen" für PowerPoint unter Windows, auf dem Mac und im Web, den `wef`-Ordner auf
  dem Mac und die Registry unter Windows (`HKCU\Software\Microsoft\Office\16.0\WEF\Developer`) mit kopierbaren Befehlen
  für den Fall, dass das Hochladen fehlt, den Katalog im freigegebenen Ordner und die zentrale Bereitstellung durch die IT.
  Texte sind deutsch, wenn die Browsersprache mit `de` beginnt, sonst englisch; ein Knopf wechselt die Sprache;
- `manifest-template.js`, erzeugt aus `scripts/manifest.mjs` (`manifestTemplateModule`): Vorlage des Manifests und
  `fillManifest`, damit Seite und `node scripts/manifest.mjs <url>` für dieselbe Adresse dasselbe XML erzeugen
  (`test/webspace.test.ts`);
- `.htaccess` (Apache) und `web.config` (IIS): MIME-Typen für `.wasm`, `.dll`, `.dat`, `.blat`, `.json`, `.js`, `.br`,
  `.gz`; kein Verzeichnislisting; `X-Robots-Tag: noindex`, `Referrer-Policy: no-referrer`, `Cache-Control: no-cache` (ein
  neuer Upload kommt sofort an); Crawler, Vorschau-Dienste und Werkzeuge werden anhand des User-Agents abgewiesen. Apache
  liefert die `.br`-/`.gz`-Kopien mit `Content-Encoding` aus, wenn der Browser sie annimmt (braucht `mod_rewrite` und
  `mod_headers`, sonst gehen die Originale raus). IIS liefert die Originale und komprimiert selbst; vorkomprimierte
  Dateien bräuchten das optionale URL-Rewrite-Modul, dessen Fehlen den ganzen Ordner lahmlegt;
- `robots.txt` (`Disallow: /`; Crawler lesen sie nur im Wurzelverzeichnis eines Hosts, im Unterordner wirken der Header
  und das robots-Meta-Tag);
- `version.json` (Versionszeile auf der Seite) und `downloads.json` mit `downloads/`: die übrigen Fassungen, die neben dem
  Paket in `dist` liegen: `InventaSketch-AddIn-<Version>.zip` (COM-Add-in) und
  `InventaSketch-LibreOffice-<Version>[-<rid>].oxt`. Aufgeführt werden nur vorhandene Dateien, nie erwartete; ohne sie
  meldet die Seite, dass es keine weiteren Fassungen gibt.

Verwendung: den Ordner mit allen Dateien hochladen (FTP-Programme zeigen `.htaccess` erst mit eingeschalteten versteckten
Dateien), in einen Ordner mit nicht erratbarem Namen, z. B. `https://example.com/isk-7k2q9x/`. Voraussetzungen: HTTPS mit
gültigem Zertifikat und kein Passwortschutz (Office-Add-ins können sich nicht anmelden). Die Adresse im Browser öffnen,
das Manifest herunterladen und in PowerPoint hochladen (Start > Add-Ins > Weitere Add-Ins > Meine Add-Ins > Mein Add-In
hochladen). Präsentationsdaten gehen nicht an den Webspace. Aktualisieren: den neuen Ordner hochladen und den alten Inhalt
ersetzen; ein neues Manifest ist nur nötig, wenn sich das Manifest geändert hat.

Herkunft: Einrichtungsseite, Servereinstellungen und Paketbau wurden aus Slidemax-Webspace (gleicher Rechteinhaber)
übernommen und ins Englische übersetzt.

Geprüft am 2026-11-20 unter Windows: Paketbau (111 Dateien, COM-Add-in-ZIP aufgeführt); der Ordner über einen lokalen
statischen Server in einem Unterordner per HTTPS (temporäres selbstsigniertes Zertifikat) und HTTP ausgeliefert; die Seite
in Edge (headless) auf Deutsch und Englisch geladen: Adresse `https://localhost:<Port>/isk-test`, erzeugtes Manifest
wohlgeformtes XML mit dieser Adresse und identisch mit `scripts/manifest.mjs`; über HTTP lehnt die Seite die Adresse ab
und sperrt den Knopf. Noch nicht geprüft: `.htaccess` auf einem echten Apache und `web.config` unter IIS, Laden des
Add-ins in PowerPoint von einem Webspace.
