# PowerPoint-COM-Add-in (Windows)

> English (maßgeblich): [../../en/hosts/powerpoint-com.md](../../en/hosts/powerpoint-com.md)

## Architektur

- `hosts/powerpoint-com/InventaSketch.AddIn` – .NET-Framework-4.8-Klassenbibliothek, AnyCPU, COM-sichtbare
  Einstiegsklasse `InventaSketch.AddIn.Connect` (`IDTExtensibility2`, `IRibbonExtensibility`). Die Office-Interop-Typen
  kommen aus NuGet und werden eingebettet; das Projekt baut mit `dotnet build`, es werden keine Interop-Assemblies
  ausgeliefert.
- Die Engine (`InventaSketch.Engine` + Module, netstandard2.0) läuft im Prozess. Bibliothekselemente werden von der
  Engine erzeugt, von `DrawPlanBuilder` mit `DrawCapabilities.Full` heruntergebrochen und von `ComRenderer` als native
  Formen geschrieben: `AddShape` (Rechteck, abgerundetes Rechteck, Ellipse, Dreieck, Raute), `AddLine` mit
  Strichmustern und nativen Pfeilspitzen, `BuildFreeform` für Polylinien und Bézierpfade, `AddTextbox`, Gruppierung über
  `Shapes.Range(...).Group()`.
- Jede eingefügte Einheit ist eine Gruppe (oder Einzelform) namens `ISK:<typ>:<id>` mit den Tags `ISK_ID`, `ISK_TYPE`
  und `ISK_MODEL` (komprimiertes Modell, ADR-0006). Ein Rückgängig-Schritt pro Einfügen (`StartNewUndoEntry`).
- Menüband-Register **InventaSketch**, aus der Modulbibliothek erzeugt: eine Werkzeuggruppe (Editor, Fangen und
  Kontextleiste – gespeichert in `%LOCALAPPDATA%\InventaSketch\settings.json` –, Schwarzweiß, Info), danach eine Gruppe
  je Modul mit einer Vorschau-Galerie je Bibliothekskategorie (6 Spalten, Name im Tooltip; Gruppen mit vielen
  Kategorien zeigen nur Symbole, damit das Register auf Laptop-Bildschirme passt). Neue Module erscheinen ohne
  Änderung am Menüband. Vorschaubilder werden mit GDI+ aus den Engine-Primitiven gerendert und folgen dem Office-Design.
- Schwebende Leiste und Kreismenü zeigen abstrakte Linien-Piktogramme (`IconIds`, gezeichnet von
  `OnSlide/IconPainter`) für Ansichten, Spiegeln, Drehen, Duplizieren, Löschen, Typ- und Beschriftungsmenü – bewusst
  keine Abbildung des konkreten Elements; die Leiste zeigt einen Tooltip für die Schaltfläche unter dem Mauszeiger.
  Rendertests (`tests/InventaSketch.AddIn.Tests`, Windows) schreiben helle und dunkle PNGs von Leiste und Kreismenü zur
  Sichtprüfung. Die Sprache folgt der Office-Oberflächensprache (Deutsch bei deutscher LCID, sonst Englisch).
- Automatisierungsobjekt für Skripte und Tests: `Application.COMAddIns("InventaSketch.AddIn.Connect").Object` mit
  `Insert(itemId)`, `Place(itemId)`, `Editor(visible)`, `LibraryItems()`, `Version()`.
- Protokoll: `%LOCALAPPDATA%\InventaSketch\addin.log`.
- Logo: `assets/branding/logo.svg` ist die einzige Quelle; `hosts/office-web/scripts/icons.mjs` rastert daraus die
  Manifest-PNGs und `assets/branding/inventasketch.ico` (16–256 px). Die .ico ist im Add-in eingebettet (`Branding`) und
  erscheint in den Titelleisten von Eingabe- und Info-Dialog, auf der Info-Schaltfläche und der eingeklappten
  Werkzeuggruppe im Menüband sowie als Programmsymbol des Web-Hosts.

## Interaktion auf der Folie

`OnSlide/OnSlideController` ergänzt Interaktion direkt auf der Folie nach in Slidemax bewährten Mustern:

- **Abbildung:** `SlideView` tastet `DocumentWindow.PointsToScreenPixelsX/Y` über eine große Spanne ab (die API rundet)
  und findet den Folienbereich unter der Folienmitte. Alles läuft Monitor-DPI-bewusst (`DpiScope`).
- **Eingabe:** threadlokale `WH_MOUSE`/`WH_KEYBOARD`-Hooks auf dem UI-Thread von PowerPoint, nur aktiv, solange ein
  Modus läuft oder eine InventaSketch-Einheit ausgewählt ist. Hooks rechnen und zeichnen nur; Objektmodellaufrufe laufen
  über `BeginInvoke`. Drücken/Loslassen-Paare werden nie zwischen PowerPoint und Add-in aufgeteilt.
- **Overlay:** ein durchklickbares Layered Window über dem Folienbereich (`UpdateLayeredWindow`, Alpha je Pixel).
- **Einfügen am Mauszeiger:** nach Auswahl eines Menüband-Elements folgt ein oranger Umriss dem Mauszeiger und rastet
  ein (Zahneingriff, Nabe auf Wellenachse, Punkte, Ausrichtungslinien); Klick fügt ein, `Esc` bricht ab, `Alt`
  setzt das Fangen aus.
- **Ziehen mit Fangen:** Drücken innerhalb einer ausgewählten Einheit verschiebt sie mit demselben Fangen und roten
  Hilfslinien; ein Rückgängig-Schritt beim Loslassen.
- **Größe ändern mit Fangen:** die Größengriffe von PowerPoint werden übernommen. Eckgriffe skalieren gleichmäßig
  (`Umschalt`: frei), Kantengriffe strecken eine Achse, `Strg` skaliert um die Mitte, `Alt` schaltet das Fangen ab.
  Bei gleichmäßiger Skalierung rastet der Teilkreis eines Zahnrads in den Eingriff mit einem Nachbarrad oder Hohlrad
  ein. Die Vorschau zeigt den Faktor; beim Loslassen wird die Einheit aus ihrem Modell neu gezeichnet (Parameter
  unverändert, nur Größen).
- **Drehen mit Fangen:** der Drehgriff wird übernommen; Fangen auf Vielfache von 15° innerhalb 4° (`Umschalt`: strikt
  15°, `Alt`: frei).
- **Tastatur (Einheit ausgewählt, Folie im Fokus):** `R` 90° drehen (`Umschalt+R` zurück), `X` / `Y` horizontal /
  vertikal spiegeln, `Strg+D` duplizieren mit neuen IDs, `Umschalt+F10` oder Kontextmenütaste öffnen das Kreismenü.
  Pfeiltasten, `Entf`, `Strg+C/X/V` bleiben bei PowerPoint; eine eingefügte Kopie erhält bei Auswahl automatisch neue
  IDs.
- **Design:** Leiste und Kreismenü folgen dem Office-Design (Dunkelgrau/Schwarz oder „Systemeinstellung“ bei dunklem
  Windows-App-Design → dunkel).
- **Zweipunkt-Werkzeug:** Linien, Pfeile, Bemaßungen und Bezugslinien werden wie Verbinder gesetzt: am Startpunkt
  drücken und zum Endpunkt ziehen (oder Start klicken, Ende klicken). Beide Enden rasten an Mittelpunkten, Ecken, Enden
  und Ports anderer Einheiten ein.
- **Punkte bearbeiten:** eine ausgewählte Linie/Pfeil/Bemaßung/Bezugslinie zeigt Rautengriffe auf ihren Punkten;
  Ziehen verschiebt sie mit magnetischem Fangen (`Alt`: frei).
- **Typ umwandeln und Beschriftung steuern (Kreismenü):** „Typ ▸“ (Linie, Pfeil, Doppelpfeil, gebogener Pfeil,
  Winkelverbinder, Bezugslinie, Bezugszeichen, lineare und schräge Bemaßung; Endpunkte, Zeichen/Text, Ebene, Gruppe und
  Farbe bleiben erhalten) und „Beschriftung ▸“ aus den optionalen Parametern des Typs.
- **Schwebende Leiste:** über der ausgewählten Einheit (abseits des Drehgriffs, darunter wenn oben kein Platz ist):
  Ansichten, Spiegeln, 90° drehen, Duplizieren, Löschen. Innerhalb von 12 logischen Pixeln voll deckend, sonst 0,92 und
  über 180 px ausblendend; unter 0,1 bis zur nächsten Auswahl verborgen; Tippen blendet sie aus.
- **Kreismenü:** Rechtsklick auf eine ausgewählte Einheit: Löschen (oben), Ansichten, Typaktionen, Duplizieren,
  Spiegeln, 90° drehen.

## Seitenleisten-Editor (benutzerdefinierter Aufgabenbereich)

Der Menüband-Schalter **Editor** öffnet einen angedockten Aufgabenbereich (`Sidebar/TaskPaneControl`, ProgId
`InventaSketch.AddIn.TaskPane`) mit einem WebView2, das denselben Editor wie das Web-Add-in (`sidebar.html`) aus
`AddIn\App` lädt. Der Editor spricht über `window.chrome.webview` mit dem Add-in (`combridge.ts` ↔ `SidebarBridge`):
`units`-Nachrichten tragen kompakte Modelle, die das Add-in mit der vollen C#-Engine nativ zeichnet; die ausgewählte
Einheit wird als `selection` zurückgemeldet. Live-Synchronisation ist standardmäßig an. Ein Klick auf ein
Bibliothekselement startet das Einfügen am Mauszeiger auf der Folie.

## Installation (pro Benutzer, ohne Administratorrechte)

**Für Anwender:** `scripts\package-com.ps1` (läuft auch in `scripts\build.ps1`) baut `dist\InventaSketch-AddIn` und
`dist\InventaSketch-AddIn-<version>.zip`: entpacken, PowerPoint schließen, `Install.cmd` doppelklicken; Entfernen
mit `Uninstall.cmd`. Das Paket enthält die Release-Binärdateien, den Seitenleisten-Editor (`AddIn\App`) und die
Registrierungsskripte.

**Für Entwickler:**

```powershell
.\scripts\install-com.ps1      # Release bauen, nach %LOCALAPPDATA%\InventaSketch\AddIn kopieren, registrieren (HKCU)
.\scripts\unregister-com.ps1   # Registrierung entfernen
```

Vor der Installation PowerPoint schließen. Das Web-Add-in wird automatisch ausgeblendet, solange das COM-Add-in
installiert ist (`EquivalentAddins`, ADR-0004).

## Geprüft

Siehe die englische Fassung (Abschnitt „Verified“) für die vollständige Liste der am 2026-11-20 in PowerPoint 16.0
(Build 20527, Windows 11, 200 % DPI) geprüften Abläufe: Einfügen, Fangen beim Ziehen/Skalieren/Drehen,
Zweipunkt-Pfeil, Punktbearbeitung, Typumwandlung, schwebende Leiste, Kreismenü, Seitenleisten-Synchronisation,
Tastatur.

Zehn Module (gleiche Umgebung, aus dem Paket mit `install.ps1` installiert, 2026-11-20 abends):

- Das Add-in lädt mit 681 Bibliothekselementen in 59 Kategorien (ca. 0,9–1,1 s); das Menüband zeigt eine Gruppe je
  Modul, die bei geringerer Breite zu Gruppenmenüs mit kompakten Kategorie-Galerien zusammenklappt; Beschriftungen mit
  „&“ werden richtig angezeigt.
- Je ein Element aus Kinematik, Elektro, Maschinen, Fahrzeuge, Architektur und Diagramme nativ eingefügt
  (Folienexport geprüft). Ein Auto aus der Galerie wurde am Mauszeiger platziert; die Leiste zeigt eigene abstrakte
  Symbole für Draufsicht, Seiten-, Vorder- und Rückansicht.
- Dabei behoben: Mittig ausgerichteter Text in nativen Textfeldern lag um eine halbe Feldbreite zu weit links (neue
  Textfelder passten sich an ihren leeren Text an, bevor die automatische Größe abgeschaltet war) – Titel von UML/ER,
  Maßzahlen und Unterstreichungen sitzen jetzt richtig.

Kreismenü, Eingabedialog und abhängige Einheiten (gleiche Umgebung, 2026-11-20 abends):

- Kreismenü: höchstens acht Einträge im Ring (wichtige zuerst: Löschen, Typ/Beschriftung nur für Linientypen,
  Ansichten, Duplizieren, Drehen), alles Weitere ohne Dopplungen unter „Mehr ▸“; Beschriftungen brechen auf die
  Sehnenbreite des Sektors um (zwei Zeilen, kleinere Schrift oder „…“), eine Pille unter dem Ring zeigt den vollen
  Text eines gekürzten Eintrags; die aktive Ansicht ist am Rand markiert.
- Ein Rechtsklick bis 6 logische Pixel neben einer ausgewählten dünnen Einheit öffnet das Kreismenü; der Maus-Hook
  schluckt die Freigabe jedes von ihm geschluckten Drückens (und bleibt bis dahin installiert), daher erscheint das
  native PowerPoint-Kontextmenü nicht mehr.
- Eingabeaktionen: „Mehr ▸ → Dicke…“ an einer Wand öffnete den Dialog (Layout aus Schriftmaßen, korrekt bei 200 % DPI);
  365 mm machten die Wand 10,3 pt dick und zeichneten die angeschlossene Wand neu („Redrew 1 dependent unit(s)“); ein
  Strg+Z nahm beides zurück.

UX-Runde (2026-11-20, nach Rückmeldung des Maintainers):

- Menüband: Werkzeuge (Editor, Einrasten, Kontextleiste, Schwarz-Weiß, Verankern, kleines Info mit Logo), dann
  **Beschriftung** (Beschriftung, Patentzeichnung und Grundformen), dann je Fachmodul eine Gruppe. Menüband-Bilder
  werden als 32-Bit-DIB mit Alphakanal übergeben (aus Slidemax übernommen), daher ohne weißen Kasten im dunklen Office.
- **Schwarz-Weiß** ist ein umkehrbarer Schalter: Er setzt `DocumentSettings.Monochrome` an jeder Einheit der Folie und
  zeichnet neu; der Zeichenplan bildet alle Farben (Linien, Füllungen, Text, Markierungen) auf Schwarz/Weiß ab und
  entfernt durchscheinende Füllungen. Erneutes Klicken stellt die Farben wieder her.
- **Text**: Doppelklick auf eine ausgewählte Einheit, der Leistenknopf „Text“ oder „Text …“ im Kreismenü bearbeiten
  ihren Haupttext (Objekttext, Bezugszeichen, Name oder Beschriftung).
- **Einfügen ▸** im Kreismenü bietet passende Elemente zur Auswahl an (`ObjectType.Companions`, sonst dieselbe
  Kategorie, immer Bezugszeichen/Bezugslinie/Pfeil), in derselben Art von Ansicht; die Wahl startet das Einfügen am
  Mauszeiger mit Fangen.
- **Verankern** (standardmäßig an): an einem anderen Element eingerastete Linienenden speichern eine Anbindung (mit
  Pin-ID) und folgen, wenn das Element verschoben, gedreht oder skaliert wird (ein Rückgängig-Schritt); wird die Linie
  selbst verschoben, lösen sich ihre Enden. Geprüft: ein am Zahnradrand eingerasteter Pfeil folgte dem gezogenen Zahnrad.
- Ansichtswechsel behalten die Proportionen (Bibliotheksvorlagen beider Ansichten; eine gemeinsame Abmessung wie der
  Zahnraddurchmesser behält ihre Länge). Zentrierte Teile (Teilkreis oder Nabe) ändern ihre Größe standardmäßig um die
  Mitte (Strg: gegenüberliegende Kante). Nabensymbole im Schema behalten beim Vergrößern ihre Größe.
- Der Seitenleisten-Editor entfernt auf der Folie gelöschte Einheiten (Nachricht `present` bei jedem Auswahlwechsel).

Formatierungsgruppen (2026-11-20): Das Register lautet Struktur | Werkzeuge | Beschriftung | Farbe | Text | Layout |
Elemente, wie in Slidemax. Struktur, Farbe und Text nutzen Office-eigene Steuerelemente (neue Folie, Einfügen, Format
übertragen, Schrift-/Füll-/Konturfarbe, Schrift, Größe, fett … Aufzählungen); Layout ergänzt Anordnen, Gruppieren,
eigene Knöpfe für gleiche Höhe/Breite/Größe, Formgröße, Ausrichten und Verteilen (zur Auswahl, eine einzelne Form zur
Folie) und Tauschen (aus Slidemax übernommen). Die Fachmodule sind je ein großes Menü in „Elemente“, damit das Register
auf Laptop-Bildschirme passt (schmale Fenster klappen Layout und Elemente zu Menüs zusammen). Formatierungen mit diesen
Steuerelementen an InventaSketch-Einheiten bleiben erhalten: jede gezeichnete Form merkt sich ihr gerendertes Aussehen
(Tag `ISK_FMT`), beim Einlesen werden Abweichungen (Linienfarbe und -stärke, Füllung, Textfarbe, Schrift,
Größenfaktor, fett/kursiv/unterstrichen) in den Objektstil übernommen (`NativeFormat`). Geprüft: ein rot gefärbtes
Zahnrad mit 2,5-pt-Linien blieb so, nachdem eine Drehung es neu gezeichnet hatte.

Menü „Extras“ (Werkzeuge-Gruppe, wie das Werkzeuge-Menü von Slidemax, schlank übernommen): Symbol einfügen,
Dezimaltrennzeichen ändern (Komma/Punkt in Zahlen der aktuellen Folie oder der ganzen Präsentation;
InventaSketch-Einheiten bleiben unberührt), Korrektursprache (Deutsch, Englisch UK/USA, weitere über den Office-Dialog;
Auswahl oder alle Folien), Folien senden (ausgewählte Folien als .pptx/.pdf/beides, Outlook-Mail mit Anlage, sonst
Dateien in der Zwischenablage), Folien speichern (.pptx/.pdf, danach geöffnet), Module ein- und ausblenden (in
settings.json als `HiddenModules`; ausgeblendete Module verschwinden aus dem Menüband). Geprüft: das Menü erscheint,
ausgeblendete Module fehlen in „Elemente“.

## Bekannte Grenzen / nächste Schritte

- Ein per `New-Object -ComObject PowerPoint.Application` gestartetes PowerPoint listete das frisch registrierte Add-in
  im Test nicht; ein normal gestartetes schon.
- Dunkles Design von Leiste und Kreismenü nur über Rendertests geprüft, noch nicht in einem dunklen Office.
- Auf der Folie noch nicht geprüft: Texteingabedialog, `X`/`Y`/`Umschalt+R`, Kreismenü per Tastatur.
- Als Nächstes: Stilsteuerung (Linie, Füllung, Pfeilspitzen) in der Leiste, Fachexporte (Stückliste, Netzlisten,
  Topologien).
