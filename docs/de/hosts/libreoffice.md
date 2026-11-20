# LibreOffice-Erweiterung (Impress, Draw, Calc)

> English (maßgeblich): [../../en/hosts/libreoffice.md](../../en/hosts/libreoffice.md)

Phase 1 des LibreOffice-Hosts (Roadmap „Phase 3 – LibreOffice-Erweiterung“, ADR-0005): eine installierbare `.oxt`, die
Elemente der InventaSketch-Bibliothek als native, bearbeitbare LibreOffice-Formen in Impress, Draw und Calc einfügt und
das Modell an den Formen speichert, damit sie erneut bearbeitet werden können. Portiert aus dem LibreOffice-Host von
Slidemax (Engine-Prozess, Anlegen der UNO-Formen, Seitenleiste, Weiterreichen in den Hauptthread, Packen), übersetzt
und angepasst.

## Architektur

```
LibreOffice (Impress/Draw/Calc)
 └─ inventasketch_component.py      Python-UNO: Protocol-Handler (Menü, Skript-API), Fabrik des Seitenleisten-Panels
     └─ pythonpath/inventasketch_lo
         host.py      Zielseite, Einfügepunkt, Auswahl, Einfügen/Bearbeiten, ein Rückgängig-Schritt je Aktion
         plan.py      Zeichenplan (pt) -> Formbeschreibungen (1/100 mm), ohne UNO, mit Unit-Tests
         render.py    Formbeschreibungen -> native UNO-Formen, Gruppen, Linienenden, Metadaten
         metadata.py  Name der Einheit, Attribute, Dokumenteigenschaften in Teilstücken, Erkennen nativer Änderungen
         sidebar.py   Deck „InventaSketch“, Panel „Bibliothek“; library.py: Filter/Suche, Vorschaubilder
         engine.py    Engine-Prozess (JSON-Zeilen), .NET-Suche, Zeitlimit, Neustart
         mainthread.py  Befehle über die Fernbrücke laufen im Hauptthread
 └─ engine/portable/InventaSketch.LibreOffice.Engine.dll   (oder engine/<rid>/… eigenständig)
     = hosts/libreoffice/engine (net10.0) + src/InventaSketch.Engine + Module
```

- **Eine Engine:** Die gesamte Fachlogik ist die C#-Engine (`EngineApi`). Python übersetzt nur Zeichenpläne in
  UNO-Formen und Oberflächenereignisse in Engine-Aufrufe (ADR-0002). Der Engine-Prozess liest je Zeile eine
  JSON-Anfrage auf stdin (`{"id":1,"method":"insert","params":{…}}`, `params` = der `EngineRequest`) und antwortet je
  Zeile auf stdout (`{"id":1,"ok":true,"result":…}` / `{"id":1,"ok":false,"error":"…"}`); zusätzlich `ping`,
  `methods`, `exit`. `drawPlan` ohne `capabilities` nutzt `DrawCapabilities.LibreOffice` (Freiformen, native
  Linienenden, native Schraffur, Drehung).
- **Zustandslose Sitzungen:** Jede Aktion legt eine kurze Engine-Sitzung in der Größe der LibreOffice-Seite an
  (`createSession` → `insert` oder `importCompact` → Bearbeitung → `drawPlan` → `closeSession`). Plankoordinaten (pt)
  sind Seitenkoordinaten; der Engine-Prozess hält keinen Dokumentzustand und wird nach Absturz oder Zeitüberschreitung
  (60 s) unbemerkt neu gestartet.
- **Start der Engine (Entscheidung):** Das Standardpaket ist *portabel*: Die Engine ist eine framework-abhängige `.dll`
  (Paket ≈0,7 MB für alle Plattformen), gestartet mit dem `dotnet` des Anwenders. Die Erweiterung sucht einen Host mit
  Microsoft.NETCore.App ≥ 10 in `DOTNET_ROOT`, `PATH`, `%LOCALAPPDATA%\Microsoft\dotnet`, `%ProgramFiles%\dotnet`,
  `~/.dotnet`, `/usr/local/share/dotnet`, `/usr/share/dotnet`, `/usr/lib/dotnet`, `/opt/homebrew/opt/dotnet/libexec`,
  `/usr/local/bin`, `/snap/dotnet-sdk/current` (geprüft mit `dotnet --list-runtimes`). Ohne passende Laufzeit nennt
  eine zweisprachige Meldung die Download-Seite und die durchsuchten Orte. Pakete je Plattform (`--rid win-x64` usw.)
  enthalten eine eigenständige Engine als Einzeldatei (kein .NET nötig, größerer Download); die Erweiterung bevorzugt
  `engine/<rid>/`, wenn vorhanden. `INVENTASKETCH_ENGINE` überschreibt den Pfad der Engine.
- **Hauptthread:** Befehle aus Menü und Seitenleiste laufen im Hauptthread von LibreOffice; Befehle über die
  UNO-Fernbrücke (Skripte, Tests) werden dorthin weitergereicht (`com.sun.star.awt.AsyncCallback`), weil das Öffnen der
  Seitenleiste aus einem Thread der Brücke soffice abstürzen ließ.

## Installieren und benutzen

```powershell
python hosts/libreoffice/scripts/package.py                 # dist/InventaSketch-LibreOffice-<Version>.oxt (portabel)
python hosts/libreoffice/scripts/package.py --rid win-x64   # dist/InventaSketch-LibreOffice-<Version>-win-x64.oxt
"C:\Program Files\LibreOffice\program\unopkg.com" add --suppress-license dist\InventaSketch-LibreOffice-0.1.0.oxt
```

Die Version stammt aus `Directory.Build.props`. Das Packskript braucht ein .NET-10-SDK; installiert wird mit
`unopkg add` (LibreOffice geschlossen) oder über *Extras ▸ Extension-Manager*. Entfernen mit
`unopkg remove org.inventasketch.libreoffice`.

- **Menü „InventaSketch“** (Impress, Draw, Calc): *Aus Bibliothek einfügen …* (öffnet das Deck der Seitenleiste),
  *Modell der ausgewählten Einheit bearbeiten*, *Ausgewählte Einheit aus dem Modell neu zeichnen*,
  *Bezugszeichen hinzufügen*, *Daten gelöschter Einheiten entfernen*, *Über InventaSketch*.
- **Seitenleiste „InventaSketch“ ▸ Bibliothek:** Suchfeld (Wörter treffen Namen, IDs und Schlagwörter, Akzente
  werden ignoriert), Kategorieliste, Elementliste mit Vorschau (SVG-Vorschaubild der Engine); Doppelklick oder
  *Einfügen* fügt das Element in der Mitte der Folie/Seite ein (Calc: in der Mitte des sichtbaren Zellbereichs).
  *Ausgewählte Einheit* zeigt den Typ der ausgewählten Einheit und ihre Ansichten; die Wahl einer Ansicht zeichnet die
  Einheit aus ihrem gespeicherten Modell neu.
- **Skript-API** (DispatchHelper / Makros), jeweils `inventasketch.libreoffice:<Befehl>`:
  `insert?item=<id>[&x=<mm>&y=<mm>]`, `library`, `editModel`, `setView?view=<id>`, `redraw`, `numeral`, `cleanup`,
  `about`. Der Integrationstest benutzt genau diese Befehle.
- Sprache der Oberfläche: Deutsch, wenn die Oberflächensprache von LibreOffice mit `de` beginnt, sonst Englisch; die
  Namen der Bibliothek liefert der Engine-Katalog in derselben Sprache.
- Protokoll: `%LOCALAPPDATA%\InventaSketch\libreoffice.log` und `engine-stderr.log` (macOS
  `~/Library/Logs/InventaSketch`, Linux `~/.local/state/InventaSketch`).

## Zeichnen (Zeichenplan → UNO)

| Plan-Op | LibreOffice-Form |
|---|---|
| `rect` (Radius, Drehung) | `RectangleShape` (`CornerRadius`, `RotateAngle`) |
| `ellipse` | `EllipseShape` |
| `line` | `LineShape` |
| `polyline` | `PolyLineShape` (offen) / `PolyPolygonShape` (geschlossen) |
| `freeform` (M/L/C/Z, Teilpfade) | `OpenBezierShape` / `ClosedBezierShape` (`PolyPolygonBezier`) |
| `shape` (Dreieck, Raute) | `PolyPolygonShape` mit gedrehten Punkten (nur ohne Freiform-Unterstützung) |
| `text` | `CustomShape`-Rechteck ohne Linie/Füllung, ohne Umbruch und Autogröße, vertikal zentriert |
| `group` | `XShapeGrouper.group` (einzelne Kinder werden nicht verpackt) |
| `svg` | `GraphicObjectShape` mit der SVG-Grafik (Ersatz; mit dem LibreOffice-Profil bei den getesteten Elementen nicht erzeugt) |

- Linie: `LineColor`, `LineWidth`, runde `LineJoint`; Strichmuster als absolutes `LineDash` (Stil RECT) mit dem
  Muster der Engine (`Style.DashArray`: Vielfache der Linienstärke, mindestens 0,18 mm); runde Enden bei
  durchgezogenen Linien.
- Pfeilspitzen: native Linienenden mit der Spitzengeometrie der Engine (Dreieck, Stealth, Raute, Oval, offen;
  Halbpfeil → Dreieck, Schrägstrich → offen wie in PowerPoint COM) und der Größe, die die Engine annimmt
  (`NativeHeadLength`). Die Linienenden stehen in der Linienenden-Tabelle des Dokuments unter Namen im Stil des
  OOXML-Imports (`msArrowEnd 9 isk`, `msArrowStealthEnd 5 isk` …), damit der .pptx-Export den passenden
  PowerPoint-Pfeiltyp und die Größe schreibt. Hohle UML-Spitzen und Krähenfüße zeichnet die Engine als Formen (kein
  natives Gegenstück).
- Füllungen: einfarbig `FillColor`, Deckkraft als `FillTransparence`/`LineTransparence`, native `FillHatch`
  (einfach/kreuz, Abstand, Winkel gegen den Uhrzeigersinn in 1/10°, optional Hintergrundfarbe über
  `FillBackground`).

## Ablage des Modells (ADR-0006)

- Form der Einheit (Gruppe oder Einzelform) mit dem Namen `ISK:<typ>:<id>`.
- `UserDefinedAttributes` (Namensraum `urn:inventasketch:shape:1`): `isk:id`, `isk:type`, `isk:model` (kompaktes Modell
  `isk1:`), `isk:bounds` (Rechteck in pt und `RotateAngle` nach dem Zeichnen). Gruppen haben in LibreOffice keine
  `UserDefinedAttributes`; bei Gruppen stehen sie daher an der ersten Einzelform der Gruppe.
- Spiegel im Dokument: benutzerdefinierte Dokumenteigenschaften `ISK_MODEL_<id>` (in Teilstücken `_1`, `_2` … ab
  60 000 Zeichen) und `ISK_BOUNDS_<id>`. Gelesen werden zuerst die Attribute, sonst die Dokumenteigenschaften.
- Bearbeiten (Ansicht wechseln, neu zeichnen, Bezugszeichen) importiert das gespeicherte Modell, übernimmt ein natives
  Verschieben/Skalieren (`hostTransform` vom gespeicherten zum aktuellen Rechteck) oder eine native Drehung (um die
  Mitte), führt die Bearbeitung in der Engine aus und ersetzt die Einheit an derselben Stelle der Z-Reihenfolge in einem
  Rückgängig-Schritt.

## Verifizierte Funktionen

Verifiziert unter Windows 11 mit LibreOffice 26.8.2.2 am 2026-11-20 (`tests/integration_test.py`, 43 Prüfungen
bestanden; `tests/ui_smoke.py`, Bildschirmfotos gesichtet):

- `unopkg add` des portablen Pakets in ein frisches Profil; Engine mit der installierten .NET-10-Laufzeit gestartet.
  Das eigenständige `win-x64`-Paket (32,9 MB) hat denselben Test mit seiner mitgelieferten Engine bestanden.
- Impress: Stirnrad (Zähne), Zahnrad im Schnitt (native `FillHatch`), Pfeil mit nativem Linienende, Bezugszeichen mit
  Pfeil (offene Bézierkurve mit nativer Anfangsspitze), Wand (Schraffur auf Hintergrundfarbe), UML-Klasse (Text),
  UML-Generalisierung (hohle Spitze als Formen), kreuzschraffiertes Rechteck, gebogener Pfeil (`OpenBezierShape`) –
  alles nativ, gruppiert, eine Einheit je Einfügen; Bezugszeichen am Pfeil ergänzt (neue Beschriftungseinheit);
  Ansichtswechsel des Zahnrads (Zähne → Seitenansicht) behält Namen und Z-Position; natives Verschieben und native
  90°-Drehung werden beim Neuzeichnen ins Modell übernommen; Folie als PNG exportiert und gesichtet.
- Als .odp und .pptx gespeichert und neu geladen: Die Namen der Einheiten bleiben in beiden erhalten;
  `UserDefinedAttributes` bleiben in .odp erhalten und gehen in .pptx verloren (erwartet); die Dokumenteigenschaften
  bleiben in beiden erhalten; eine aus jedem Format geladene Einheit wird aus ihrem Modell neu gezeichnet (UML-Klasse
  vollständig → kompakt). Die .pptx enthält native `a:headEnd`/`a:tailEnd` vom Typ `triangle`, die Namen der Einheiten
  und die benutzerdefinierten Eigenschaften `ISK_MODEL_*`.
- Draw und Calc: Einfügen (Calc: in der Mitte des sichtbaren Zellbereichs auf der Zeichenebene des Tabellenblatts) und
  Ansichtswechsel.
- Seitenleiste (Impress, deutsche Oberfläche, 200 % Anzeigeskalierung): Deck öffnet über den Befehl *Modell
  bearbeiten*, Bibliotheksliste mit 681 Elementen, Vorschau, ausgewählte Einheit mit ihren Ansichten; die Höhen der
  Bedienelemente folgen der Oberflächenschrift. Eingaben ins Suchfeld und ein Klick auf *Einfügen* wurden im Smoke-Test
  nicht ausgeführt (Filter per Unit-Test geprüft; *Einfügen* ruft denselben Befehl `insert` auf).

## Tests

```powershell
python -m unittest discover -s hosts/libreoffice/tests -p "test_*.py"     # Unit-Tests, ohne LibreOffice
"C:\Program Files\LibreOffice\program\python.exe" hosts/libreoffice/tests/integration_test.py --oxt dist/InventaSketch-LibreOffice-0.1.0.oxt --out <Ordner>
"C:\Program Files\LibreOffice\program\python.exe" hosts/libreoffice/tests/ui_smoke.py --oxt dist/InventaSketch-LibreOffice-0.1.0.oxt --out <Ordner>
```

Die Unit-Tests decken die Abbildung Plan → UNO, Metadaten, Bibliotheksfilter, Texte, .NET-Suche, das Protokoll (mit
einer Ersatz-Engine: Fehler, Absturz, Zeitlimit, fremde Zeilen), das Weiterreichen in den Hauptthread und – wenn die
Engine gebaut ist – echte Zeichenpläne von Bibliothekselementen ab. Der Integrationstest installiert das Paket in ein
temporäres Profil, startet soffice ohne Oberfläche und berührt nie das Profil oder Dateien des Anwenders; unter
Linux/macOS läuft er mit einem Python, das `uno` importieren kann.

## Grenzen (Phase 1)

- Noch keine Bedienung direkt auf der Folie (Overlay, Einrasten beim Ziehen, Kreismenü, Auswahl-Listener):
  Bearbeitet wird über Seitenleiste und Menü. Die Seitenleiste zeigt nur Ansichten; Parameter, Stil und Text sind
  noch nicht bearbeitbar.
- Schraffurlinien von LibreOffice sind Haarlinien: Die Linienstärke der Schraffur aus dem Plan wird nicht übernommen.
  Der .pptx-Export macht aus `FillHatch` eine vordefinierte Musterfüllung (Näherung von LibreOffice).
- Textfelder nutzen die von der Engine geschätzte Größe (0,6 em je Zeichen); Schriften ohne kompatible Metrik können
  über das Feld hinausragen (kein Umbruch).
- Ein natives Skalieren zusammen mit einer Drehung wird nicht erkannt (nur die Drehung und das Verschieben der Mitte
  werden übernommen). Eine in LibreOffice eingefügte Kopie behält die ID ihres Originals (noch kein Abgleich von
  Duplikaten).
- Der Austausch mit dem PowerPoint-COM-Add-in läuft über den Spiegel im Dokument (das COM-Add-in schreibt
  `ISK_MODEL_<id>` und `ISK_BOUNDS_<id>` vor dem Speichern und übernimmt LibreOffice-Einheiten nach dem Öffnen).
  Am 2026-11-20 in beide Richtungen verifiziert (`integration_test.py --from-powerpoint`). Einheiten des Office-Web-Add-ins
  halten ihr Modell in Präsentations-Tags, die LibreOffice verwirft; sie sind hier noch nicht bearbeitbar.
- Nur unter Windows verifiziert; Pakete für Linux und macOS (eigenständig, `--rid linux-x64`, `osx-arm64` …) baut
  dasselbe Skript, sie sind aber nicht getestet (macOS-Pakete müssen auf macOS gebaut werden, ADR-0005).
