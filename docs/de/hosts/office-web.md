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

## Bauen und starten

```powershell
cd hosts/office-web
npm ci
npm test            # vitest (Manifest, Bibliotheksfilter)
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
| Schreiben nativer Formen in PowerPoint | umgesetzt, **noch nicht in PowerPoint getestet** |
| Lokaler Installer wie beim COM-Add-in (ohne Admin) | umgesetzt, Probelauf geprüft; echte Installation offen |
| Webspace-Paket (Downloadseite, Manifest für eigene URL) | geplant |

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
