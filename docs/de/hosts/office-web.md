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
nutzt 3000) und die Installationsskripte. Lokaler Host und Skripte wurden mit Zustimmung des Rechteinhabers aus
Slidemax übernommen.

- **Windows:** PowerPoint schließen, `Web-Add-in installieren.cmd` doppelklicken, die Zertifikatsabfrage von Windows
  einmal bestätigen. Kopiert nach `%LOCALAPPDATA%\InventaSketch\Web`, erzeugt ein `localhost`-Zertifikat im
  Benutzerspeicher und vertraut ihm, legt einen Autostart-Eintrag an, startet den nur lokal erreichbaren HTTPS-Host und
  registriert das Manifest zum Querladen (`HKCU\Software\Microsoft\Office\16.0\WEF\Developer`). Optionen:
  `-OhneAutostart`, `-OhneManifest`, `-Probelauf`. Deinstallation: `Web-Add-in deinstallieren.cmd`. Solange das
  COM-Add-in installiert ist, blendet Office das Web-Add-in aus.
- **macOS:** `installieren.command` (Python 3): kopiert nach `~/Library/Application Support/InventaSketch/Web`,
  vertraut dem Zertifikat im Schlüsselbund, LaunchAgent, Manifest im `wef`-Ordner von PowerPoint.
- **Linux / PowerPoint im Web:** `sh installieren.sh`, danach `manifest.xml` über *Add-Ins › Meine Add-Ins ›
  Mein Add-In hochladen* einbinden.

Geprüft am 2026-11-20 unter Windows: Paketbau, Probelauf des Installers (Plan inkl. COM-Erkennung), Host mit
temporärem In-Memory-Zertifikat lieferte `taskpane.html` und die Engine per HTTPS aus, wies `../`-Pfade ab, Status und
Stopp funktionierten; Linux-Probelauf des Python-Installers. Noch offen: echte Installation mit Zertifikatsvertrauen
(braucht die Bestätigung des Anwenders), macOS-Lauf, Laden des Add-ins in PowerPoint. Meldungen und Bezeichner des
übernommenen Hosts sind noch deutsch (Übersetzung mit englischem Fallback geplant).

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
