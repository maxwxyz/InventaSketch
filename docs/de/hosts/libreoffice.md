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
 └─ inventasketch_component.py      Python-UNO: Protocol-Handler (Menü, Skript-API), Fabrik des Seitenleisten-Panels,
                                    Job CanvasJob (Jobs.xcu, OnViewCreated)
     └─ pythonpath/inventasketch_lo
         host.py      Zielseite, Einfügepunkt, Auswahl, Einfügen/Bearbeiten, ein Rückgängig-Schritt je Aktion
         plan.py      Zeichenplan (pt) -> Formbeschreibungen (1/100 mm), ohne UNO, mit Unit-Tests
         render.py    Formbeschreibungen -> native UNO-Formen, Gruppen, Linienenden, Metadaten
         metadata.py  Name der Einheit, Attribute, Dokumenteigenschaften in Teilstücken, Erkennen nativer Änderungen
         sidebar.py   Deck „InventaSketch“, Panels „Auswahl“, „Werte“, „Bibliothek“, „Zeichnung, Rechner und
                      Code“;
                      library.py: Katalogfilter, Vorschaubilder
         ux.py        Regeln des Auswahl-Panels, zuletzt benutzte Elemente, Buchführung der Seitensitzung, Regeln der
                      Bedienung auf der Folie (ohne UNO)
         choices.py   Bedienkonzept: Auswahlen als Klapplisten, die acht Plätze des Kontextmenüs, Verbundene
                      auswählen, Einfügestellen (ohne UNO, getestet)
         values.py    Zeilen des Werte-Panels, Wertestufe, Rechner und Bericht des Code-Imports (ohne UNO, getestet)
         drawing.py   zeichnungsweite Einstellungen: Farben nach Medium, Bezeichnungen über Seiten hinweg, stumme
                      Prüfungen und bevorzugte Einheiten (Dokumenteigenschaften), Rechenblatt (ohne UNO, getestet);
                      calcsheetdialog.py: der Dialog des Rechenblatts (Kopieren, Speichern, Als Tabelle einfügen)
         canvas.py    Bedienung auf der Folie: Einrasten nach dem Ziehen, Strg+Leertaste, Kontextmenü mit den acht
                      Plätzen, Strg+Doppelklick, Auswahlschein (UNO-Eingabe-Abfang); glow.py: der Schein selbst
         inputdialog.py  einzeiliger Eingabedialog (Haupttext, Aktionen, die nach einem Wert fragen)
         clean.py, exporting.py, picture.py   Regeln für Folien bereinigen, Export und Diagramm aus Bild (ohne UNO,
                      getestet); cleandialog.py, exportdialog.py: ihre Dialoge
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
- **Seitensitzung (Bearbeiten):** Eine Bearbeitung importiert *alle* InventaSketch-Einheiten der Seite in eine
  Engine-Sitzung (`importCompact` mit `couple: false`, die ausgewählten Einheiten zuletzt), damit die Engine die mit der
  Auswahl gekoppelten Teile kennt (Getriebezüge, Wellen, die Glieder eines selbst gebauten Koppelgetriebes). Ein
  Modell enthält auch Kopien der Objekte, an denen seine Punkte hängen; Einheiten, deren eigene Objekte von einer
  solchen Kopie überschrieben wurden, werden noch einmal importiert (`ux.reimports`). Natives Verschieben, Skalieren
  und Drehen seit dem letzten Zeichnen wird mit `hostTransform` übernommen (`couple: false`). Danach läuft die
  Engine-Operation auf den ausgewählten Objekten, und genau die Einheiten, deren Objekte sich geändert haben
  (`changed` + `dependents` des Editorzustands, neue Objekte, nativ veränderte ausgewählte Einheiten), werden neu
  gezeichnet; Einheiten, deren Objekte verschwunden sind (Zerlegen) oder jetzt zu einer anderen Einheit gehören, werden
  entfernt (`ux.redraw_targets`, `ux.stale_units`). Das verallgemeinert das bisherige Bearbeiten einer Einheit auf
  mehrere Einheiten (Mehrfachauswahl) und auf gekoppelte Nachbarn.
- **Ein Rückgängig-Schritt:** LibreOffice zeichnet das Entfernen von Formen über die API für Rückgängig auf, das
  Hinzufügen aber nicht. Jeder Schritt entfernt daher die ersetzten Einheiten (von LibreOffice aufgezeichnet), zeichnet
  die neuen und fügt eine eigene Rückgängig-Aktion hinzu (`XUndoManager.addUndoAction`), die beim Rückgängigmachen die
  gezeichneten Einheiten entfernt (und den ersetzten Einheiten ihren Spiegel im Dokument zurückgibt) und sie beim
  Wiederherstellen aus demselben Plan neu zeichnet. Gruppen entstehen als `GroupShape`, die danach befüllt wird, nicht
  mit `XShapeGrouper.group` (dessen eigene Rückgängig-Aktion die Kinder lose auf der Seite zurückließ).
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
  *Modell der ausgewählten Einheit bearbeiten*, *Ausgewählte Einheit aus dem Modell neu zeichnen*, *Werte des
  ausgewählten Elements …*, *Rechner mit Einheiten …*, *Rechenblatt …*, *Farben nach Medium ▸ Aus (eigene Farben) /
  Nach Medium / Legende einfügen*, *Bezugszeichen hinzufügen*, *Kompatibilität
  (Zeichnungsregeln) …*, *Mit vorheriger Folie vergleichen (Varianten)*, *Folien bereinigen …*, *Zeichnung importieren
  (SVG, DXF) …*, *Code importieren (matplotlib, pgfplots, TikZ) …*, *Diagramm aus Bild …*, *Exportieren …*,
  *Daten gelöschter Einheiten entfernen*, *Über InventaSketch*.
- **Folien bereinigen** (25.11.2026): Bevor Folien das Unternehmen verlassen, bereinigt die Engine (`sanitize`) die
  InventaSketch-Elemente der Auswahl, der aktuellen Folie/Seite/des Tabellenblatts oder aller Folien/Seiten, mit den
  Wahlmöglichkeiten des PowerPoint-COM-Add-ins: Diagramme normiert (gleiche Verhältnisse, Werte 0…1), zufällig
  verändert (plausible Werte, gleiches Aussehen) oder behalten; wahlweise Kategorien umbenannt (A, B, C …), Texte
  ersetzt, Metadaten entfernt (standardmäßig an) und ein Zufallsstartwert (leer = ein neuer, für alle Seiten des Laufs
  derselbe). Jede Seite wird wie eine Seitensitzung geladen, die geänderten Einheiten werden an Ort und Stelle neu
  gezeichnet (Namen und Z-Reihenfolge bleiben), alle Seiten in **einem** Rückgängig-Schritt. Ein Meldungsfenster zeigt
  die Zahlen, den Startwert und die Berichtszeilen der Engine und sagt, dass native LibreOffice-Diagramme und
  -Tabellen, Notizen, Kommentare und Dokumenteigenschaften nicht verändert werden. Bereich und Diagrammmodus sind
  Klapplisten (UNO-Dialoge gruppieren Optionsfelder nur über die Tab-Reihenfolge). Calc bietet die Auswahl und das
  aktuelle Tabellenblatt.
- **Exportieren** (25.11.2026): exportiert die ausgewählten InventaSketch-Elemente, sonst alle Elemente der aktuellen
  Folie/Seite (Calc: des Tabellenblatts, auf die Elemente zugeschnitten), wie das Office-Web-Add-in. Der Dialog nennt
  *SVG (bearbeitbar)* (Engine `renderPageSvg` im Modus „export“: eine benannte Gruppe je Element und Teil, Texte
  bleiben Text, für Inkscape/Illustrator) und die passenden Fachexporte der Engine (`exporters`/`export`: LaTeX TikZ,
  Diagrammdaten für matplotlib und pgfplots, Stückliste, Netzlisten …) mit dem, was sie jeweils erhalten; die Datei
  wird im Speichern-Dialog von LibreOffice mit der Endung des Formats gewählt (CSV mit Byte-Order-Mark). Native
  LibreOffice-Formen gehören nicht zum Export.
- **Diagramm aus Bild** (25.11.2026): Eine PNG-, JPEG-, BMP- oder GIF-Datei wird zu einem bearbeitbaren Diagramm in
  der Mitte der Folie/Seite (Calc: des sichtbaren Bereichs), ein Rückgängig-Schritt, ausgewählt; der Hinweis der Engine
  (was gelesen wurde, ob die Werte aus der Achsbeschriftung stammen) wird angezeigt. LibreOffice dekodiert das Bild
  (`GraphicProvider`, `XBitmap.getDIB` und bei transparenten Bildern `getMaskDIB`); `picture.py` liest die DIBs in
  reinem Python, verkleinert Bilder auf 1600 px und legt transparente Bereiche auf Weiß, bevor `chartFromPicture`
  aufgerufen wird. Die Bedeutung der Maske (255 = transparent) wurde mit LibreOffice 26.8 geprüft. Ein Bild aus der
  Zwischenablage (COM-Add-in) wird nicht angeboten.
- **Eine Kurve ins Diagramm skizzieren** gibt es in LibreOffice nicht: Der UNO-Eingabe-Abfang liefert Drücken und
  Loslassen der Maustaste, aber keine Mausbewegung, die Umrechnung von Pixeln der Ansicht in Seitenkoordinaten ist nicht
  zugänglich (der UI-Smoke-Test braucht dafür einen Kalibrierklick), LibreOffice würde während des Strichs seine eigene
  Rahmenauswahl ausführen, und es gibt keine Überlagerung, die den Strich zeigt.
- **Varianten** (25.11.2026, Impress und Draw): Eine duplizierte Folie behält die IDs der Einheiten. *Mit vorheriger
  Folie vergleichen* markiert, was sich von der vorherigen Folie unterscheidet (Engine `diff`, beide Folien wie
  Seitensitzungen geladen): neue Einheiten grün, geänderte orange, entfernte als grau gestrichelter Rahmen an ihrer
  alten Stelle, dazu eine Legende oben rechts; die Markierungen sind einfache Formen mit dem Namen `ISK_DIFF …`, ein
  erneuter Aufruf entfernt sie (jeweils ein Rückgängig-Schritt). LibreOffice benennt eine duplizierte Form, deren Name
  auf Ziffern endet, um („…eb12“ → „…eb 1“); beim Lesen einer Einheit wird der Name aus ihren Attributen
  wiederhergestellt, weil in .pptx der Name die Einheiten-ID trägt. Geprüft: `integration_test.py` (1 neu, 1 geändert,
  1 entfernt, ein unverändertes Zahnrad ohne Markierung, Namen wiederhergestellt; Folie als `compare.png` exportiert).
- **Kompatibilitätsprüfung** (25.11.2026, Impress und Draw): ein Dialog mit dem Amt (DPMA, EPA, PCT, USPTO oder das
  gemeinsame sichere Profil) und den Befunden aller Seiten mit InventaSketch-Einheiten (Engine `preflight`, jede Seite
  wie eine Seitensitzung geladen, native Verschiebungen zählen also mit) – Fehler zuerst, als „Folie 2 · Fehler ·
  Meldung“; die Wahl eines Befunds (Pfeiltasten oder Klick) zeigt Vorschlag und Fundstelle und springt zu Folie und
  Einheit. Der Haftungshinweis wird immer angezeigt. Ohne sichtbares Fenster (headless, Tests) wird das Ergebnis
  protokolliert. Geprüft: `integration_test.py` (ein Zahnrad außerhalb der Nutzfläche auf Folie 2 gefunden und seiner
  Einheit zugeordnet) und `ui_smoke.py` (der Dialog öffnet sich in einem echten Fenster, die Pfeiltaste nach unten
  springt zu Folie 2 und wählt das Zahnrad aus, Escape schließt ihn).
- **Seitenleiste „InventaSketch“**, vier Panels (siehe UI/UX-Runde und Runde „Bedienkonzept“ unten):
  - *Auswahl* folgt der Auswahl im Dokument (Auswahl-Listener) und zeigt, was mit den ausgewählten
    InventaSketch-Einheiten möglich ist – nur im Kontext: jede Auswahl als Klappliste, die eigenen Befehle des
    Elements, Verbundene auswählen, Hier einfügen, Anbauen „+“, Drehen.
  - *Werte* (26.11.2026): die Werte des ausgewählten Elements mit ihren Formeln, die Wertestufe.
  - *Bibliothek*: ein Suchfeld für die ganze Bibliothek (Engine-`search`), die Kategorieliste (erster Eintrag
    *Vorschläge*: die zur Auswahl passenden Teile, dann die zuletzt benutzten), die Elementliste mit Vorschau
    (SVG-Vorschaubild der Engine); Doppelklick oder *Einfügen* fügt das Element ein – ein zur ausgewählten Einheit
    passendes Teil dort, wo es passt, alles andere in der Mitte der Folie/Seite (Calc: des sichtbaren Zellbereichs).
  - *Zeichnung, Rechner und Code* (26.11.2026): Farben nach Medium der Seite mit *Legende einfügen* und
    *Rechenblatt …* (siehe Runde „Zeichnungsweite Einstellungen“ unten), der Rechner mit Einheiten und der Code-Import
    (matplotlib, pgfplots, TikZ).
- **Auf der Folie/Seite** (Impress, Draw; siehe Runde „Bedienung auf der Folie“ unten): Eine mit der Maus gezogene
  Einheit rastet beim Loslassen ein (Zahneingriff, Achsen, Anbaustellen, Flansche …), gekoppelte Teile folgen;
  Strg+Leertaste öffnet das Bibliotheks-Panel mit dem Cursor im Suchfeld (tippen, Eingabe fügt ein); das Kontextmenü
  einer Einheit beginnt mit den acht Plätzen des Kreismenüs (26.11.2026); Strg+Doppelklick wählt das Verbundene aus;
  ausgewählte Einheiten haben einen sanften Schein.
- **Skript-API** (DispatchHelper / Makros), jeweils `inventasketch.libreoffice:<Befehl>`:
  `insert?item=<id>[&x=<mm>&y=<mm>]`, `insertFitting?item=<id>`, `insertSearch?text=<Suchtext>`, `library`,
  `editModel`, `setView?view=<id>`, `redraw`, `numeral`, `transform?kind=mirrorH|mirrorV|rotate90`, `explode`,
  `drawTrace`, `drawGhosts`, `build?port=<Port-ID>&item=<id>`, `turn?degrees=<Winkel>`,
  `arrange?action=<ID aus arrangeOptions>`, `assembly`, `panel?control=<Schlüssel>&text=<Text>` /
  `panel?control=<Schlüssel>&item=<Eintrag>` / `panel?press=<Schlüssel, Beschriftung oder action:<id>>` (bedient ein
  Bedienelement des offenen Panels wie ein Anwender), `snapMove[?px=<Pixel>&undo=<n>]` (ausgewählte Einheit(en) nach
  einer nativen Verschiebung einrasten, wie nach dem Ziehen mit der Maus), `quickInsert[?text=<Text>]`
  (Bibliotheks-Panel mit dem Cursor im Suchfeld, wie Strg+Leertaste), `patentCheck[?profile=<Amts-ID>]`, `compare`, `importVector[?path=<Datei>]` (ohne Pfad mit Dateidialog),
  `cleanSlides[?scope=selection|slide|all&mode=normalised|randomised|none&rename=1&texts=1&meta=0&seed=<n>]`
  (ohne Argumente mit Dialog), `export[?format=<ID oder Endung>&path=<Datei>]` (Dialog und Speichern-Dialog für das,
  was fehlt; headless ohne Argumente werden die Formate protokolliert), `chartFromPicture[?path=<Datei>]`, `cleanup`,
  `about`; seit 26.11.2026 (Bedienkonzept): `action?id=<Engine-Aktions-ID>[&input=<Text>|&ask=1]` (eine
  Engine-Aktion oder eine Option einer Auswahl), `param?key=<k>&value=<v>` (eine Parameter-Auswahl),
  `text[?text=<Text>]`, `duplicate`, `delete`, `connected[?mode=auto|net|coupled|chain|loop|walls]`,
  `insertPlace?key=<k>`, `menu` (protokolliert das Kontextmenü der Auswahl, auch als JSON), `values`,
  `setValue?key=<k>&text=<t>`, `clearValue?key=<k>`, `valueLevel?level=quick|explore|engineer`, `showValues?on=0|1`,
  `tools`, `calc?text=<t>[&unit=<u>]`, `importCode[?path=<Datei>|&text=<Code>]`; seit 26.11.2026 (zeichnungsweite
  Einstellungen): `colorBy?mode=medium|off`, `legend`, `sameName?key=<Bezeichnung>&page=<n>`,
  `muteCheck?key=<Prüfungs-Id>[&mute=0]` (`mute=0` ohne Schlüssel schaltet alle wieder ein),
  `unitPref?kind=<Größenart>&unit=<Einheit>`, `calcSheet[?mode=csv|markdown|latex|table&path=<Datei>&copy=1]` (Dialog
  ohne Argumente, ohne Oberfläche protokolliert); seit Runde 4 `pageAction?id=<Seitenbefehls-Id>[&label=<l>]` (ein
  Befehl der ganzen Seite, z. B. `patent.numerals.auto=tens`); `panel?…&enter=1` drückt Eingabe in
  einem Feld, `control=choice:<Beschriftung oder Schlüssel>` benennt die Klappliste einer Auswahl. Integrations- und
  UI-Smoke-Test benutzen diese Befehle.
- **Zuletzt benutzte Elemente** (die letzten 12 eingefügten Bibliothekselemente, je Anwender) stehen in
  `libreoffice-settings.json` neben dem Protokoll und werden in der Suche höher eingestuft.
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
| `group` | `GroupShape`, zuerst eingefügt und dann befüllt (einzelne Kinder werden nicht verpackt) |
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
- Bearbeiten (Ansicht wechseln, neu zeichnen, Bezugszeichen und jede Operation des Auswahl-Panels) läuft in einer
  Seitensitzung (siehe Architektur): gespeicherte Modelle importiert, natives Verschieben/Skalieren (`hostTransform`
  vom gespeicherten zum aktuellen Rechteck) oder native Drehung (um die Mitte) übernommen, Engine-Operation ausgeführt
  und die betroffenen Einheiten an ihrer Stelle der Z-Reihenfolge in einem Rückgängig-Schritt ersetzt.

## UI/UX-Runde 2026-11-24 (LibreOffice, 2026-11-25)

Folgt den UX-Regeln der Roadmap („Plan 2026-11-24“), soweit eine Seitenleiste das kann: LibreOffice hat noch kein
Overlay auf der Folie, daher liegt alles im Deck. Alle Entscheidungen kommen aus der Engine; reine Panel-Regeln in
`ux.py` (`tests/test_ux.py`).

- **Ein Einfügeweg (Panel Bibliothek):** Tippen durchsucht die ganze Bibliothek mit der Engine-`search` (beide
  Sprachen, Umlaute gefaltet – „stoessel“ findet „Stößel“ –, zuletzt benutzte Elemente und die Ansicht der Auswahl
  höher eingestuft); bei ausgewählter Einheit stehen die passenden Teile vorn und sind mit „– passt“/„– fits“
  markiert. Ohne Eingabe listet der Eintrag *Vorschläge* die passenden Teile, dann die zuletzt benutzten. *Einfügen*
  setzt ein passendes Teil dorthin, wo es passt (`insertFitting`: Zahnrad auf die Welle, Zahnrad in Eingriff, Lager
  neben ein Zahnrad), gekoppelt; passt es nirgends, oder bei jedem anderen Element, kommt es in die Seitenmitte. Eine
  gewählte Kategorie filtert die Treffer weiterhin.
- **Panel Auswahl, eine Einheit (nur im Kontext, feste Reihenfolge):** Name und ID, *F = n (Glieder, Gelenke)* für ein
  Glied eines selbst gebauten Koppelgetriebes (`dof`); die Ansichtsliste nur bei mehr als einer Ansicht; dann
  Schaltflächen, die nur erscheinen, wenn sie etwas bewirken: *Bahn zeichnen* (Koppelkurve, `drawTrace`) und
  *Zwischenlagen* (`drawGhosts`) bei F = 1, *Baugruppe wählen*, wenn gekoppelte Teile noch nicht ausgewählt sind
  (`assembly`), *Zerlegen* bei zerlegbaren Baugruppen (`dof.explodable`), *Spiegeln ↔*, *Spiegeln ↕*, *Drehen 90°* nur,
  wenn `transformEffect` sagt, dass sie die Zeichnung ändern (ein Zahnrad in Stirnansicht zeigt keine). *Anbauen (+)*:
  die Anbaustellen der Einheit als Liste (`buildPorts`, „1: Eingriff, rechts“), die dort andockenden Teile
  (`buildItems`) und *Anbauen* (`buildPlace`, angedockt und gekoppelt). Ein mit anderen gekoppeltes Zahnrad erhält
  *Getriebe drehen um (°)* mit eingetipptem Winkel (`motionTurn`, der ganze Zug mit seinen Übersetzungen). *Aus dem
  Modell neu zeichnen* wie bisher.
- **Panel Auswahl, zwei oder mehr Einheiten:** die auswahlweiten Operationen der Engine aus `arrangeOptions` mit ihren
  Beschriftungen – eine Ansicht für alle (Mechanismus bleibt erhalten), Mitten auf eine Zeile oder Spalte, gleichmäßig
  verteilen, wie das erste, Maß zwischen beiden, Bezugszeichen neu nummerieren, zwei sich kreuzende Leitungen oder Rohre
  verbinden („Verbinden (Abzweig)“), Linien an einem gemeinsamen Abzweig wieder zur Brücke machen („Als Brücke (nicht
  verbunden)“) –, jeweils nur, wenn sie etwas ändern,
  dazu *Baugruppe wählen*. Ein Klick führt `arrange` auf allen ausgewählten Einheiten in einer Seitensitzung und einem
  Rückgängig-Schritt aus.
- **Beschriftungen:** Die Erweiterung hat noch keinen Symbolsatz; die Schaltflächen tragen kurze zweisprachige
  Textbeschriftungen mit dem Wortlaut des COM-Add-ins als Tooltip; für die auswahlweiten Operationen werden die
  Beschriftungen der Engine verwendet (eine je Zeile).
- **Nicht in LibreOffice (braucht das geplante Overlay):** Hilfslinien und Einrastbeschriftungen *während* des
  Ziehens, die Live-Vorschau der Kopplung, Anbau-„+“ auf der Folie und das Strecken eines Wellenendes durch Ziehen, der
  Bewegungsmodus durch Ziehen (Getriebezüge drehen stattdessen um einen eingetippten Winkel; Koppelgetriebe lassen
  sich nicht ziehen), „Mach daraus …“ nach dem Schnellanfügen und das Kreismenü. Einrasten beim Loslassen,
  Strg+Leertaste und ein Kontextmenü kamen mit der Runde „Bedienung auf der Folie“ unten hinzu.

## Bedienung auf der Folie (LibreOffice, 2026-11-25)

Die UNO-API von LibreOffice bietet einer Erweiterung keinen Weg, in die Dokumentansicht zu zeichnen (kein Overlay für
Griffe, „+“-Schaltflächen oder Hilfslinien, keine Live-Vorschau, während LibreOffice eine Form zieht). Sie erlaubt
aber, Maus und Tastatur der Ansicht zu beobachten und ihr Kontextmenü zu erweitern (`XUserInputInterception`,
`XContextMenuInterception` am Controller). Genau das nutzt die Erweiterung (`canvas.py`, reine Regeln in `ux.py`);
jede Entscheidung kommt weiterhin aus der Engine.

- **Anmelden:** Der Job `CanvasJob` (`Jobs.xcu`, Dokumentereignis `OnViewCreated`) meldet an jeder Impress-/Draw-Ansicht
  eine Mausklick-Behandlung, eine Tastenbehandlung und einen Kontextmenü-Interceptor an; Befehle und Seitenleisten-Panels
  melden erneut an, falls eine Ansicht vor der Erweiterung geöffnet war (je Controller nur einmal). Calc behält nur Menü
  und Seitenleiste (Strg+Leertaste markiert dort eine Spalte, Formen sind an Zellen verankert).
- **Einrasten nach dem Ziehen (wie in PowerPoint):** LibreOffice verschiebt die Form selbst; ein Loslassen der linken
  Maustaste nach einer Mausbewegung stellt den Befehl `snapMove` in den Hauptthread, der läuft, nachdem LibreOffice die
  Verschiebung ausgeführt hat. Haben sich alle ausgewählten Einheiten um denselben Vektor verschoben (keine
  Größenänderung, keine Drehung: `ux.drag_delta`), importiert die Seitensitzung die Einheiten mit den gezogenen an ihrer
  gespeicherten Lage, ruft `snapMove` der Engine mit dem Zugvektor auf (Toleranz 8 Bildschirmpixel beim Zoom des
  Ziehens, aus den Pixeln der Mausbewegung abgeleitet, wie in den anderen Hosts) und wendet den eingerasteten Vektor mit
  `move` *mit* Kopplung an: Ein Zahnrad landet im Eingriff, auf einer Achse, an einer Anbaustelle oder einem Flansch;
  Teile auf einer gezogenen Welle und angedockte Linien folgen. Die betroffenen Einheiten werden neu gezeichnet. Die
  eigene Verschiebe-Aktion von LibreOffice (genau eine neue Rückgängig-Aktion seit dem Drücken der Maustaste) wird vorher
  zurückgenommen, so dass Ziehen + Einrasten **ein** Rückgängig-Schritt ist („InventaSketch: Verschieben und
  einrasten“), der die Einheit an ihre Lage vor dem Ziehen zurückbringt. Strg (Kopie) und Alt (freies Verschieben, wie
  in den anderen Hosts) rasten nicht ein; Größenänderungen, Drehungen, Formen in einer betretenen Gruppe und die
  Textbearbeitung rasten nie ein. Fehler nach dem Ziehen werden protokolliert, nicht angezeigt.
- **Strg+Leertaste:** öffnet das InventaSketch-Deck (Seitenleisten-API: `setVisible`, `showDecks`, `XDeck.activate`,
  weil der Dispatch `.uno:SidebarDeck.<Deck>` umschaltet und ein offenes Deck schließen würde) und setzt den Cursor mit
  markiertem Text in das Suchfeld des Bibliotheks-Panels; gibt es das Panel noch nicht, übernimmt es die Anforderung,
  sobald es gefüllt ist. Tippen sucht wie bisher; **Eingabe** fügt den markierten Treffer ein (ein passendes Teil dort,
  wo es zur Auswahl passt) und gibt den Fokus an das Dokument zurück, **Esc** gibt ihn nur zurück. In der
  Textbearbeitung bleibt Strg+Leertaste beim Text (Textbearbeitung wird am Zustand von `.uno:InsertSoftHyphen` erkannt,
  den LibreOffice nur dann aktiviert; die Auswahl ändert sich in der Textbearbeitung nicht).
- **Kontextmenü:** Ein Rechtsklick auf InventaSketch-Einheit(en) setzt die Aktionen des Auswahl-Panels an den Anfang
  des LibreOffice-Menüs, gefolgt von einem Trenner: *Ansicht wechseln ▸* (die anderen Ansichten), *Bahn zeichnen*,
  *Zwischenlagen*, *Baugruppe wählen*, *Zerlegen*, *Horizontal/Vertikal spiegeln*, *Um 90 Grad drehen* – jeweils nur,
  wenn die Engine sagt, dass sie zutreffen (`host.describe`, `ux.context_entries`) –, *Aus dem Modell neu zeichnen*
  und *Mehr in der InventaSketch-Seitenleiste …* (Anbau-„+“, Drehen). Mehrere Einheiten: die auswahlweiten Operationen
  der Engine und *Baugruppe wählen*. Die Einträge sind gewöhnliche Dispatch-Befehle der Erweiterung. Der Aufbau des
  Menüs kostet eine Seitensitzung (alle Einheiten der Seite werden importiert). Seit 26.11.2026 besteht der Block aus
  den acht Plätzen des Kreismenüs (siehe Runde „Bedienkonzept“ unten).
- **Ohne Overlay nicht möglich (dokumentiert, nicht versucht):** Hilfslinien und Einrastbeschriftung während des
  Ziehens, die Live-Vorschau der Kopplung, „+“-Griffe auf der Folie, Strecken eines Wellenendes und Bewegungsmodus durch
  Ziehen, das Kreismenü. Das Aufklappen eines zugeklappten Bibliotheks-Panels bei Strg+Leertaste entfiel:
  `XDeck.getPanels()`/`XPanel.expand` brachte soffice 26.8 bei gezeigtem Deck zum Absturz; ein vom Anwender
  zugeklapptes Bibliotheks-Panel bleibt daher zu.

## Runde „Bedienkonzept“ (LibreOffice, 26.11.2026)

Das Konzept aus [interaction-ux.md](../design/interaction-ux.md) (Abschnitte 5a, 6, 6a) und die Werte-Oberfläche aus
[engineering-calculations.md](../design/engineering-calculations.md) (Abschnitte 10–11), so weit LibreOffice eine
Erweiterung lässt. Die Umsetzung im Office-Web-Add-in (`barlogic.ts`, `pielogic.ts`, `valuecard.ts`, `tools.ts`) war die
zweite Vorlage. Jede Entscheidung kommt weiter von der Engine; die reinen Regeln stehen in `choices.py` und `values.py`
(`tests/test_concept.py`).

- **Auswahlen sind Klapplisten.** Das Auswahl-Panel fragt die Engine nach den Aktionen (`contextActions`) und Parametern
  (`inspector`) des Elements und zeigt **eine Klappliste je Auswahl**: zuerst die Parameter-Auswahlen (ohne die
  erweiterten), dann die Aktions-Auswahlen der Engine (`choice`, `choiceLabel`, `choiceMenu`), deren Parameter nicht
  schon eine Parameter-Auswahl ist (`paramKeys`, ein Ort je Sache). Die geschlossene Klappliste zeigt die aktuelle
  Option, die Liste alle Optionen; die Wahl einer Option führt sie aus (`setParam` oder `applyAction` mit der ID der
  Option; eine Option, die nach einem Wert fragt, öffnet zuerst den Eingabedialog), ein Rückgängig-Schritt. Menüs
  (`choiceMenu`: „Füllen bis“, „Fläche hinzufügen“) sind Klapplisten, deren erste Zeile „Beschriftung ▸“ ist; die Wahl
  einer Zeile führt sie aus, danach steht wieder der Titel da. Nirgends gibt es Durchschalt-Schaltflächen. Die übrigen
  Befehle des Elements sind Schaltflächen mit den Beschriftungen der Engine (ein eingeschalteter Schalter wie die
  Kopfzeile einer Tabelle ist eine gedrückte Umschaltfläche); „Text …“ bearbeitet den Haupttext (`setMainText`). Die
  Options-Piktogramme (`IconIds`) und -Bilder (`previewSvg`) der anderen Hosts werden nicht gezeigt: Die Erweiterung hat
  keinen Piktogrammsatz, die Optionen sind also benannt; keine Element-Auswahl des Katalogs bringt heute ein
  `previewSvg` mit (das sind Teil-Auswahlen von Diagrammen).
- **Die acht Plätze als Kontextmenü.** Ein Rechtsklick auf InventaSketch-Einheiten setzt die acht Plätze des
  Kreismenüs oben in das Menü von LibreOffice, in derselben Reihenfolge und mit denselben Worten wie COM und Web:
  *Text …*, *Bearbeiten*, *Einfügen …*, *Duplizieren*, *Stil*, *Löschen*, *Ansicht*, *Anordnen* (LibreOffice zeichnet
  den Untermenü-Pfeil selbst, deshalb tragen die Beschriftungen kein „▸“). Bearbeiten: *Werte …* (öffnet das
  Werte-Panel), die eigenen Befehle des Elements, *Bezugszeichen*, die Menü-Auswahlen, *Zerlegen*, *F = n* (angezeigt,
  inaktiv), *Bahn zeichnen*, *Zwischenlagen*, *Teile …*, *Mehr in der InventaSketch-Seitenleiste …* (Anbau-„+“, Drehen,
  Hier einfügen), *Aus dem Modell neu zeichnen*. Stil: ein Untermenü je Zustands-Auswahl, „Linienart: Gestrichelt“, die
  aktuelle Option **abgehakt**. Ansicht: die Ansichten, die aktuelle abgehakt (mehrere Einheiten: „eine Ansicht für
  alle“). Anordnen: um 90° in beide Richtungen drehen und spiegeln (nur, wo `transformEffect` sagt, dass es die Zeichnung
  ändert), die eigenen Befehle des Hosts *In den Vordergrund* / *In den Hintergrund* (`.uno:BringToFront`,
  `.uno:SendToBack`: die Z-Reihenfolge einer Einheit ist die ihrer Form), *Verbundene auswählen* (Untermenü) und bei
  mehreren Einheiten die Ausrichten-/Verteilen-/Bemaßen-Operationen der Engine. Einfügen öffnet das Bibliotheks-Panel
  mit dem Suchfeld (wie Strg+Leertaste); Duplizieren und Löschen laufen über die Engine (`duplicate`, `delete`: eine
  Kopie bekommt neue IDs, was von einem gelöschten Element abhängt, folgt), je ein Rückgängig-Schritt. Ein Platz mit
  einem Eintrag zeigt ihn direkt, Bearbeiten, Stil und Anordnen behalten ihren Namen; mehr als 14 Einträge gehen in
  *Mehr* weiter. Haken und inaktiver Zustand sind die von LibreOffice selbst: Der Protocol-Handler meldet den Status
  (`State`, `IsEnabled`) des Befehls eines Eintrags. **LibreOffice blendet inaktive Kontextmenü-Einträge** standardmäßig
  **aus** (*Extras ▸ Optionen ▸ LibreOffice ▸ Ansicht ▸ Inaktive Menüeinträge* zeigt sie ausgegraut), ein Platz ohne
  Inhalt für die Auswahl (Text … bei einem Zahnrad) fehlt also, und die Plätze darunter rücken eine Zeile hoch – die
  Reihenfolge ändert sich nie.
- **Werte-Panel.** Eine Zeile je Wert des ausgewählten Elements (`values`): Kennzeichen (✎ eingegeben, = abgeleitet,
  ○ Vorgabe, ⇄ verbunden, ⚠ im Widerspruch), Symbol und Name, das Eingabefeld (der Parser der Engine: „4k7“, „230 V“,
  „1,5 kW“; Eingabe oder Verlassen des Felds speichert mit `setValue`, Escape stellt wieder her), die Einheit, × für einen
  eingegebenen Wert (`clearValue`); unter einem abgeleiteten Wert seine Formel Schritt für Schritt (`explain`, Erkunden
  und Ingenieur; Ingenieur ergänzt die Quelle und eine vierte geltende Ziffer; Panel- und Ingenieurswerte nur bei
  Ingenieur). Die Statuszeile sagt ✓, was fehlt oder dass Eingaben sich widersprechen; Widersprüche und verletzte
  Prüfungen (`checks`) folgen. Eine abgelehnte Eingabe zeigt die Meldung der Engine rot unter ihrer Zeile, nie in einem
  Meldungsfenster. *Werte in der Zeichnung zeigen* schaltet die Werte-Beschriftung (`showValues`), *Rechner …* setzt
  den ersten Wert in den Rechner. Die **Wertestufe** (Schnellskizze, Erkunden, Ingenieur) ist eine Einstellung des
  Dokuments: die benutzerdefinierte Dokumenteigenschaft `ISK_VALUES` (wie die Präsentationsmarke des COM-Add-ins;
  Schnellskizze entfernt sie), sie reist also mit .odp und .pptx. Das Panel folgt der Auswahl; alle Einheiten der Seite
  nehmen an der Rechnung teil, über Elemente verbundene Werte sind also bekannt.
- **Rechner und Code.** Der Rechner mit Einheiten (`calc`, das Ergebnis mit `convertUnit` umgerechnet, wenn eine
  Zieleinheit getippt ist): „230 V * 16 A“ = 3,68 kW, „1500 rpm“ in rad/s = 157,1 rad/s, mit den Schritten der Engine;
  Eingabe oder *Berechnen*. Der Code-Import nimmt eingefügten oder geöffneten matplotlib-, pgfplots- oder TikZ-Code
  (`importCode`: gelesen, nie ausgeführt): Plots werden Diagramme, Zeichnungen Formen, in der Mitte der Folie/Seite, ein
  Rückgängig-Schritt, ausgewählt; der Bericht (was importiert wurde, jede übersprungene Zeile mit Grund) steht darunter.
  Derselbe Import steht im Menü (Dateidialog).
- **Verbundene auswählen.** Anordnen ▸ *Verbundene auswählen* nennt die zutreffenden Arten (`connectOptions`: Netz,
  bewegt sich mit, folgende Elemente, geschlossene Schleife, Raumwände) mit der Zahl der Elemente; die Wahl einer Art
  wählt diese Einheiten aus (`connected`). Das Auswahl-Panel hat dieselbe Liste („Verbundene auswählen ▸“), die
  „Baugruppe wählen“ ersetzt, sobald die Engine Arten anbietet. **Strg+Doppelklick** auf eine ausgewählte Einheit wählt
  die sinnvollste Art (Modus `auto`); Drücken und Loslassen werden verbraucht, damit LibreOffice nicht zusätzlich die
  Gruppe betritt oder eine Form darin der Auswahl hinzufügt.
- **Auswahl-Aussehen.** Ausgewählte Einheiten haben einen Schein: LibreOffice hat kein Overlay, aber seine Formen haben
  einen nativen Leuchteffekt (`GlowEffectRadius`, `GlowEffectColor`, `GlowEffectTransparency`). Jede Einzelform einer
  ausgewählten Einheit bekommt einen ruhigen blauen Schein (1,4 mm, 55 % transparent); **Ellipsen ignorieren den
  Leuchteffekt** (LibreOffice 26.8 liest ihn als 0 zurück), sie bekommen dasselbe Aussehen als weichgezeichneten Schatten
  ohne Versatz (`ShadowBlur`). Der Schein verschwindet, wenn die Auswahl wechselt, vor jedem InventaSketch-Befehl (damit
  LibreOffices Rückgängig nie eine scheinende Form aufhebt) und bevor das Dokument gespeichert, als Kopie gespeichert,
  exportiert oder gedruckt wird (Dokumentereignisse `OnSave`, `OnSaveAs`, `OnCopyTo`, `OnPrint`; storeToURL sendet
  `OnCopyTo`, geprüft), und kommt danach zurück. Das Setzen legt keine Rückgängig-Aktion an, das Geändert-Kennzeichen
  des Dokuments wird wiederhergestellt. Anders als im Konzept liegt der Schein hinter jeder Form auf ihrer eigenen
  Z-Position (ein Element darüber verdeckt ihn), es gibt keine blassere Vorschau für einen überfahrenen Eintrag
  „Verbundene auswählen“ (das Kontextmenü von LibreOffice meldet kein Überfahren), und eine Einheit, die aus der
  Zwischenablage eingefügt wurde, während sie schien, behält den Schein, bis sie einmal ausgewählt war.
- **Einfügestellen.** Die „+“-Punkte zwischen den Zeilen und Spalten einer Tabelle (Core `IInsertHandles`) lassen sich
  nicht auf die Folie zeichnen; das Auswahl-Panel listet sie stattdessen: *Hier einfügen* mit „Hier eine Zeile einfügen
  (vor Zeile 3)“ … und einer Schaltfläche *Einfügen* (Engine `insertHandles` / `applyInsertHandle`, für diesen Host
  ergänzt), ein Rückgängig-Schritt. Im Kontextmenü stehen sie nicht (eine Tabelle mit zehn Zeilen hätte neun Einträge).
- **Nicht in LibreOffice:** Ziehgriffe an Werten (`dragHandles` / `moveHandle`: Grenzlinien eines Diagramms, Maßwerte,
  Robotergelenke) – LibreOffice meldet einer Erweiterung Drücken und Loslassen der Maus, aber keine Bewegung, und
  zeichnet keine Griffe; wo das Element einen Wert hat, ersetzt ihn ein getippter Wert (Werte-Panel, Teile-Dialog).
  Keine schwebende Leiste (kein Overlay), keine Vorschau beim Überfahren, keine Options-Piktogramme (siehe oben).

## Runde „Zeichnungsweite Einstellungen“ (LibreOffice, 26.11.2026)

Die Bedienung der Engine-Funktionen aus [interaction-ux.md](../design/interaction-ux.md) Abschnitt 6b (Farben nach
Medium, Namen, die zusammengehören) und [engineering-calculations.md](../design/engineering-calculations.md) Abschnitt
10.11 (Calc 6: Prüfeinstellungen, bevorzugte Einheiten, Rechenblatt), mit den Worten des COM-Add-ins. Reine Regeln in
`drawing.py` (`tests/test_drawing.py`); jede Entscheidung kommt von der Engine.

- **Farben nach Medium.** *Zeichnung, Rechner und Code ▸ Farben nach Medium* (Klappliste *Aus (eigene Farben)* / *Nach
  Medium*) und das Menü *Farben nach Medium ▸ Aus / Nach Medium*: Die Seitensitzung führt `setColorBy` aus, und jede
  Einheit der Folie/Seite wird in einem Rückgängig-Schritt („InventaSketch: Farben nach Medium“) neu gezeichnet;
  Einheiten, die in einem anderen Modus gezeichnet sind als dem von der Sitzung übernommenen, ebenfalls. Der Modus steckt
  in den gespeicherten Modellen der Einheiten (die Engine übernimmt ihn aus jeder importierten Einheit, wie die
  Folienvorlage im COM-Add-in), daher zeigt eine Seite den Modus ihrer Einheiten, und die Klappliste liest ihn ohne
  Engine aus dem gespeicherten Modell einer Einheit; eine auf einer gefärbten Seite eingefügte Einheit übernimmt ihn
  (`insert` setzt ihn vor dem Zeichnen). *Legende einfügen* (Panel und Menü) fügt die auf der Seite verwendeten Farben
  als eine Einheit oben rechts ein (neue Engine-Methode `insertLegend`: `LineMedia.Used` und `LineMedia.Legend`,
  gruppiert; ein Rückgängig-Schritt, ausgewählt). LibreOffice-Textfelder brechen nicht um, daher schiebt die Engine die
  Legende nach links, bis ihr längster Name, geschätzt mit 0,6 em je Zeichen, innerhalb der Seite endet. Ohne gefärbte
  Linie auf der Seite erscheint die Meldung der Engine.
- **Bezeichnungen über Seiten hinweg (Impress, Draw).** Jede Seitensitzung fragt die Engine nach den Bezeichnungen der
  Seite (`designations`); gibt es welche, werden die Bezeichnungen der Einheiten der anderen Seiten aus deren
  gespeicherten Modellen gelesen (neue Engine-Methode `designationsOf`: die Bezeichnungen jedes kompakten Modells ohne
  Sitzung; in der Erweiterung je Modell zwischengespeichert) und mit den Seitennummern und „slide“/„page“ an
  `setContinuations` gegeben. So zeichnen Linien, deren Bezeichnung auch auf einer anderen Seite vorkommt, „→ W3, Folie
  2“, wann immer sie gezeichnet werden. Hinweise auf anderen Seiten werden aufgefrischt, wenn deren Einheiten neu
  gezeichnet werden (wie in COM). Die Bezeichnungs-Aktionen selbst (*Bezeichnung …*, *Wertangabe …*, *Endmarken*,
  *Leiter ▸*, *Leitungsrolle ▸*, *Medium (DIN 2403) ▸*) sind die allgemeinen Engine-Aktionen `line.ref`, `line.value`,
  `line.ref.ends`, `elem.ref`, `line.role` und erscheinen bereits als Schaltflächen und Klapplisten des Auswahl-Panels
  und in den Plätzen Bearbeiten und Stil.
- **Gleicher Name.** *Verbundene auswählen ▸ Gleicher Name* (Engine-Modus `samename`) erscheint mit den anderen Arten,
  wo die Engine ihn anbietet. Für jede andere Seite mit der Bezeichnung der Auswahl ergänzen die Liste (Auswahl-Panel)
  und das Untermenü Anordnen ▸ Verbundene auswählen „Gleicher Name auf Folie 2: W3“; die Wahl zeigt diese Folie und
  wählt dort die Einheiten mit dieser Bezeichnung aus (Befehl `sameName`). Das ersetzt die Meldung „Gleicher Name: auch
  auf Folien 2, 4“ des COM-Add-ins durch einen Eintrag, der dorthin führt.
- **Plausibilitätswarnungen im Werte-Panel.** Bis zu vier Zeilen „⚠ Meldung (Regel) [Quelle]“ (die `checks` der Engine
  für das Element, wie in der Werte-Karte von COM), jede mit *Stumm*; weitere Warnungen stehen darunter ohne
  Schaltfläche. *Stumm* legt die Prüfungs-Id in der benutzerdefinierten Dokumenteigenschaft `ISK_MUTED` ab (Ids durch
  „;“ getrennt, das Format des Präsentations-Tags im COM-Add-in); *n stumme Prüfung(en) wieder einschalten* leert sie.
- **Werte anzeigen in.** Klapplisten für Druck (bar, MPa, kPa), Drehzahl (min⁻¹, rev/s, rad/s), Energie (kWh, MJ, J)
  und Leistung (W, kW); die erste Einheit ist die Vorgabe und wird als „nicht gesetzt“ abgelegt. Die Wahl prüft die
  Engine (`setUnitPreference`), sie steht in der Dokumenteigenschaft `ISK_UNITS` („pressure=MPa;power=kW“), und die
  Einheiten der Seite, deren Zeichnung sich damit ändert (eine Werteangabe in der Zeichnung: zwei Zeichenpläne
  verglichen), werden in einem Rückgängig-Schritt („InventaSketch: Werte anzeigen in“) neu gezeichnet. Das COM-Add-in
  bietet für die Drehzahl „s⁻¹“ an, das die Engine nicht kennt; LibreOffice bietet das „rev/s“ der Engine an.
- **Dokumenteinstellungen.** `ISK_MUTED` und `ISK_UNITS` werden in jede Seitensitzung übernommen (`muteCheck`,
  `setUnitPreference` nach dem Import der Einheiten); wie die Wertestufe `ISK_VALUES` sind sie Dokumenteigenschaften und
  reisen mit .odp und .pptx (geprüft: beide überstehen Speichern und Neuladen).
- **Rechenblatt.** *Rechenblatt …* (Werkzeug-Panel und Menü) öffnet einen Dialog mit dem Blatt der ausgewählten
  Elemente, sonst aller Elemente der Folie/Seite (Calc: der Elemente des Tabellenblatts): Format *CSV
  (Tabellenkalkulation)*, *Markdown* oder *LaTeX (booktabs)* (Engine `calcSheet`, Stellen nach der Wertestufe), der Text
  (nur lesbar), die Zahl der Zeilen und Quellen, *Kopieren* (Systemzwischenablage, reiner Text), *Speichern …*
  (Speicherdialog von LibreOffice mit der Endung des Formats; CSV mit Byte-Order-Mark) und *Als Tabelle einfügen*
  (Engine `insertCalcTable`: eine Beschriftungstabelle neben den Elementen, ein Rückgängig-Schritt „InventaSketch:
  Rechenblatt“, ausgewählt).

## Runde der Seitenbefehle (LibreOffice, 26.11.2026, Runde 4)

- **Alle Teile nummerieren ohne Auswahl.** Der Bereich *Zeichnung, Rechner und Code* zeigt unter *Rechenblatt …* die
  Klappliste *Alle Teile nummerieren ▸* (Zeilen *10, 20, 30 …* / *1, 2, 3 …*), solange die Folie/Seite ein Teil ohne
  Bezugszeichen hat; auf einer Seite ohne Teile oder wenn jedes Teil nummeriert ist, gibt es keine Klappliste (nur im
  passenden Kontext). Sie stammt aus den Seitenbefehlen der Engine (`pageActions`, eine Seitensitzung ohne ausgewählte
  Einheit; `choices.page_choices` behält die Befehle, die gerade etwas bewirken). Eine Zeile führt den Befehl
  `pageAction?id=<id>&label=<l>` aus (Engine `applyPageAction`): Die Teile, die ein Bezugszeichen erhalten, werden neu
  gezeichnet, die neuen Bezugszeichen als neue Einheiten, ein Rückgängig-Schritt („InventaSketch: Alle Teile
  nummerieren: 10, 20, 30 …“); danach fragt der Bereich erneut. Das Menü ist dasselbe, das ein ausgewähltes
  Bezugszeichen anbietet (`patent.numerals.auto=tens` / `=ones`).
- **Bezugszeichen über Seiten hinweg** (26.11.2026, Runde 5): `host.page_numerals` liest die Einheitenmodelle der anderen
  Seiten (Impress/Draw, mit ihren Nummern bezeichnet) oder Tabellen (Calc, mit ihren Namen bezeichnet), lässt die Engine
  sie zusammenfassen (`numeralsOf`, je Modell zwischengespeichert wie `designationsOf`; `drawing.numerals_elsewhere`
  fasst sie je Seite zusammen) und übergibt sie als `elsewhere` an `applyPageAction` und an das eigene Menü *Alle Teile
  nummerieren ▸* eines Bezugszeichens (`applyAction`, `drawing.is_numbering_action`). Die Nummerierung schließt an die
  Bezugszeichen der anderen Seiten an, gleiche Teile behalten Bezugszeichen und Namen. Die Kompatibilitätsprüfung aller
  Seiten (Impress/Draw) übergibt jeder Seite die Bezugszeichen der anderen (`pageName` „slide“/„page“), sodass
  Bezugszeichen für verschiedene Teile und dasselbe Teil mit unabhängigen Bezugszeichen über Seiten hinweg gemeldet
  werden. Unit- und Engine-getestet (`test_concept`); in LibreOffice noch nicht ausgeführt.
- **Piktogramme.** LibreOffice hat keinen Piktogrammsatz: Die neuen Options-Piktogramme der Verkehrsmittel und Phasen
  (Runde 4) erscheinen in den Klapplisten als Optionsnamen.

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

- 2026-11-25 (UI/UX-Runde, Windows 11, LibreOffice 26.8.2.2, deutsche Oberfläche): `integration_test.py` 61 Prüfungen
  bestanden, darunter Suche in der anderen Sprache („welle“) und mit Umlauten („Stößel“), ein Zahnrad auf eine
  ausgewählte Welle gesetzt (`insertFitting`), „lager“ bei ausgewählter Welle fügt das passende Lager ein, zuletzt
  benutzte Elemente gespeichert, ein kämmendes Zahnrad an der Anbaustelle `mesh.right` angebaut, der Zug um 30°
  gedreht (beide Räder neu gezeichnet), Baugruppe gewählt (2 Einheiten), zwei getrennte Zahnräder in einem
  Rückgängig-Schritt auf eine Zeile gemittelt (Rückgängig und Wiederherstellen hinterlassen genau die Formen der
  Einheiten), eine Ansicht für alle, eine Viergelenkkette in 5 Teile zerlegt, Koppelkurve und Zwischenlagen eines
  Glieds, ein Pfeil um 90° gedreht, Rückgängig nach dem Einfügen entfernt die ganze Einheit. `ui_smoke.py` 8 Prüfungen
  bestanden bei geöffnetem Deck: Ansichtswechsel aus der Ansichtsliste des Panels, „welle“ ins Suchfeld getippt und
  *Einfügen*, „lager“ bei ausgewählter Welle fügt das Lager dort ein, wo es passt, das Panel folgt einer im Dokument
  getroffenen Mehrfachauswahl und deren *Mitten auf eine waagrechte Linie*, *Anbauen* aus der Liste der Anbaustellen
  und *Drehen* mit eingetipptem Winkel; Bildschirmfotos gesichtet.

- 2026-11-25 (Bedienung auf der Folie, Windows 11, LibreOffice 26.8.2.2, deutsche Oberfläche, 200 %
  Anzeigeskalierung): `integration_test.py` 70 Prüfungen bestanden, neu: Der Job meldet die Behandlungen vor jedem
  Befehl an einer neuen Ansicht an, ein 1,3 mm neben der Eingriffslage einer anderen Einheit abgelegtes Zahnrad rastet
  in einem Rückgängig-Schritt in den Eingriff ein (Achsabstand 25,20 mm), die Auswahl bleibt; eine um 20 mm verschobene
  Welle nimmt das Zahnrad darauf mit (Kopplung); eine native Größenänderung rastet nicht ein. `ui_smoke.py`
  19 Prüfungen bestanden (zweimal) mit **echter Maus- und Tastatureingabe** (Windows `SendInput`): Ein mit der Maus
  bis 2,5 mm neben die Eingriffslage gezogenes Zahnrad rastet beim Loslassen in den Eingriff ein (25,20 mm, Toleranz
  4,99 mm bei 21 % Zoom), ein Rückgängig bringt es an den Start zurück, Wiederherstellen rastet es wieder ein; ein
  Rechtsklick auf das Zahnrad zeigt *Ansicht wechseln*, *Baugruppe wählen*, *Aus dem Modell neu zeichnen*, *Mehr in der
  Seitenleiste* über dem LibreOffice-Menü, und Ab, Rechts, Eingabe wechselt seine Ansicht; Strg+Leertaste, „welle“
  tippen und Eingabe fügt eine Welle ein, erneut Strg+Leertaste bei ausgewählter Welle, „lager“, Eingabe fügt das
  passende Lager ein; in der Textbearbeitung bleiben Strg+Leertaste und Tippen im Text; die 8 Seitenleisten-Prüfungen
  der UI/UX-Runde bestehen weiterhin. Bildschirmfotos gesichtet.

- 2026-11-25 (Runde „Weitergeben“, Windows 11, LibreOffice 26.8.2.2, headless, deutsche Oberfläche):
  `integration_test.py --only share` 16 Prüfungen bestanden: *Folien bereinigen* über zwei Folien (zufällig, Startwert
  42, Texte ersetzt) änderte beide Diagramm-Einheiten und ließ den Pfeil unverändert, behielt die Namen der Einheiten,
  war ein Rückgängig-Schritt („InventaSketch: Folien bereinigen“), und Rückgängig stellte alle gespeicherten Modelle
  wieder her; mit nur dem Diagramm ausgewählt änderte sich nur das Diagramm. *Exportieren*: TikZ und matplotlib des
  ausgewählten Diagramms (ohne den Pfeil), das bearbeitbare SVG der Folie, die für die Folie angebotenen Formate
  protokolliert. *Diagramm aus Bild*: ein PNG mit fünf Säulen auf transparentem Hintergrund (darunter schwarz) wurde zu
  einer `chart.bar`-Einheit (Maske benutzt), ein Rückgängig-Schritt; Folie als `share.png` exportiert und gesichtet.
  Die beiden Dialoge wurden über die UNO-Brücke in beiden Sprachen aufgebaut (alle Eigenschaften der Bedienelemente
  angenommen); in einem sichtbaren Fenster wurden sie nicht gezeigt.

- 26.11.2026 (Runde „Bedienkonzept“, Windows 11, LibreOffice 26.8.2.2, deutsche Oberfläche, 200 % Anzeigeskalierung):
  `integration_test.py` 118 Prüfungen bestanden (alle Phasen; `--only concept` 24 Prüfungen): Das Kontextmenü eines
  Widerstands hat die acht Plätze in Reihenfolge mit *Werte …* unter Bearbeiten und abgehakter aktueller Ansicht; der
  Platz Stil einer Tabelle hat die Auswahl Linien mit abgehakter aktueller Option, eine andere Option zeichnet die
  Tabelle in einem Rückgängig-Schritt neu; Wertestufe Erkunden als `ISK_VALUES` gespeichert (Schnellskizze entfernt
  sie), U = 230 V und I = 2 A ergeben R = 115 Ω mit „R = U/I = 230 V/2,00 A = 115 Ω“, je ein Rückgängig-Schritt, das
  Löschen von I lässt R offen; Duplizieren ergibt eine neue ID, Löschen entfernt sie; „Hier eine Zeile einfügen“ ändert
  die Tabelle in einem Rückgängig-Schritt; zwei kämmende Zahnräder bieten „Bewegt sich mit (2 Elemente)“, `connected`
  wählt beide; der Rechner ergibt 3,68 kW und 157,1 rad/s; eine matplotlib-Datei wird eine `chart.figure`-Einheit
  (Rückgängig-Schritt „Code importieren“); das ausgewählte Zahnrad scheint auf allen 6 Formen (2 Linien per Leuchteffekt,
  4 Ellipsen per weichem Schatten), ohne das Dokument als geändert zu markieren, die mit storeToURL gespeicherte Kopie
  hat keinen Schein, der Schein kehrt nach dem Speichern zurück und verschwindet beim Abwählen (Seite als
  `concept-glow.png` exportiert und angesehen). `ui_smoke.py` 33 Prüfungen bestanden (alle Phasen; die frühere
  Kontextmenü-Prüfung geht jetzt zum Platz Ansicht; `--concept-only` allein 12 Prüfungen) mit **echter Maus- und
  Tastatureingabe**: der Schein nach dem Auswählen, Rechtsklick auf ein Zahnrad zeigt die acht Plätze (Text …
  als inaktiv ausgeblendet) oben im Menü von LibreOffice (Bildschirmfoto angesehen: native Untermenü-Pfeile, Symbole und
  Tastenkürzel bei *In den Vordergrund*), Pfeil nach unten bis Ansicht, Pfeil nach rechts zeigt die Ansichten mit der
  aktuellen abgehakt, Pfeil nach unten + Eingabe wechselt die Ansicht; Strg+Doppelklick wählt genau die beiden
  kämmenden Zahnräder; in der Seitenleiste schaltet die Klappliste Linien auf „Nur Außenrahmen“, *Hier einfügen* fügt
  eine Zeile ein, das Werte-Panel nimmt „230 V“ und „2 A“ in seinen Feldern an und zeigt R und P mit ihren Formeln,
  „2 kg“ für U wird mit der Meldung der Engine rot unter der Zeile abgelehnt, der Rechner ergibt 3,68 kW.
  Bildschirmfotos angesehen (das Deck wird mit dem Mausrad zum Werte-Panel gescrollt; das Panel „Rechner und Code“
  unter dem Bibliotheks-Panel wurde bedient, aber nicht ins Bildschirmfoto gebracht).

- 26.11.2026 (Runde „Zeichnungsweite Einstellungen“, Windows 11, LibreOffice 26.8.2.2, deutsche Oberfläche):
  `integration_test.py --only drawing` 22 Prüfungen bestanden: Eine Leitung mit *Leiter ▸ L1* und der Bezeichnung W3
  wird mit Farben nach Medium in einem Rückgängig-Schritt („Farben nach Medium“) braun (#8d5524), eine danach eingefügte
  Leitung übernimmt den Modus, die Legende ist eine Einheit mit „Außenleiter L1 (IEC 60445)“, *Aus* stellt die eigenen
  Farben wieder her; dieselbe Bezeichnung auf Folie 2 zeichnet auf Folie 1 „→ W3, Folie 2“, und „Gleicher Name auf Folie
  2: W3“ im Kontextmenü zeigt Folie 2 mit dieser Leitung ausgewählt; ein Widerstand mit 529 W bei 0,25 W zeigt
  `elec.r.power`, Stummschalten legt `ISK_MUTED` ab und die Warnung verschwindet, Wiedereinschalten bringt sie zurück;
  Leistung in kW legt `ISK_UNITS` ab, zeichnet die Werteangabe des Widerstands („P = 0,529 kW“) in einem
  Rückgängig-Schritt neu und lässt alles andere unberührt (Drehzahl: 0 Einheiten neu gezeichnet); das Rechenblatt als
  CSV-Datei (Byte-Order-Mark, kW), Markdown (in die Zwischenablage kopiert und zurückgelesen), LaTeX (`\toprule`) und als
  Beschriftungstabelle (ein Rückgängig-Schritt); `ISK_MUTED` und `ISK_UNITS` überstehen Speichern und Neuladen als .odp
  und .pptx (Warnung weiter stumm, Leistung weiter in kW). Seiten als `drawing-medium.png` und `drawing.png` exportiert
  und angesehen (Legende innerhalb der Seite, nachdem die Engine sie nach links geschoben hat). `ui_smoke.py
  --drawing-only` 9 Prüfungen in einem sichtbaren Fenster bestanden (deutsche Oberfläche, 200 % Anzeigeskalierung):
  „Gleicher Name auf Folie 2: W3“ in der Liste des Auswahl-Panels zeigt Folie 2 mit ausgewählter Leitung, *Nach Medium*
  in der Klappliste des Werkzeug-Panels färbt die Seite, *Legende einfügen* fügt die Legende ein, das Werte-Panel zeigt
  die Warnung des Widerstands mit Regel und Quelle neben *Stumm*, und *Stumm* schaltet sie stumm, *Leistung: kW* legt die
  Vorgabe ab, *Rechenblatt …* öffnet den Dialog, und **echte Tasten** (Tab ×4, Leertaste) drücken *Als Tabelle
  einfügen*, das die Tabelle einfügt. Bildschirmfotos von Werkzeug-Panel, Werte-Panel und Dialog angesehen (der
  vollständige Lauf aller Phasen: `integration_test.py` 138 Prüfungen bestanden).

## Tests

```powershell
python -m unittest discover -s hosts/libreoffice/tests -p "test_*.py"     # Unit-Tests, ohne LibreOffice
"C:\Program Files\LibreOffice\program\python.exe" hosts/libreoffice/tests/integration_test.py --oxt <oxt> --out <Ordner> --only concept
"C:\Program Files\LibreOffice\program\python.exe" hosts/libreoffice/tests/ui_smoke.py --oxt <oxt> --out <Ordner> --concept-only
"C:\Program Files\LibreOffice\program\python.exe" hosts/libreoffice/tests/integration_test.py --oxt <oxt> --out <Ordner> --only drawing
"C:\Program Files\LibreOffice\program\python.exe" hosts/libreoffice/tests/ui_smoke.py --oxt <oxt> --out <Ordner> --drawing-only
"C:\Program Files\LibreOffice\program\python.exe" hosts/libreoffice/tests/integration_test.py --oxt dist/InventaSketch-LibreOffice-0.1.0.oxt --out <Ordner>
"C:\Program Files\LibreOffice\program\python.exe" hosts/libreoffice/tests/ui_smoke.py --oxt dist/InventaSketch-LibreOffice-0.1.0.oxt --out <Ordner>
```

Die Unit-Tests decken die Abbildung Plan → UNO, Metadaten, Bibliotheksfilter, Texte, .NET-Suche, das Protokoll (mit
einer Ersatz-Engine: Fehler, Absturz, Zeitlimit, fremde Zeilen), das Weiterreichen in den Hauptthread, die Regeln des
Auswahl-Panels und die Buchführung der Seitensitzung (`test_ux.py`) und – wenn die Engine gebaut ist – echte
Zeichenpläne von Bibliothekselementen sowie die Engine-Abläufe hinter dem Panel mit in getrennten Sitzungen gezeichneten
Modellen ab (Suche mit passenden Teilen, passend einfügen, Anordnen über Einheiten hinweg, ein Getriebezug über
Einheiten gedreht, Zerlegen). `test_share.py` deckt Folien bereinigen (Wahl, Startwert, Engine-Anfrage, Seiten eines
Bereichs, Bericht), Export (Formate, Namen, Endungen, Bytes), Diagramm aus Bild (Lesen von DIBs mit 1/8/16/24/32 Bit,
Verkleinern, Transparenz auf Weiß) und die Regel ab, dass kein sichtbarer Text „patent“ enthält, dazu die
Engine-Anfragen dieser Module gegen die gebaute Engine. `integration_test.py --only share` führt nur die Phase dieser
drei Befehle aus. `test_concept.py` deckt das Bedienkonzept ab: das Gruppieren der Auswahlen (Parameter-Auswahlen
zuerst, abgedeckte Aktions-Auswahlen weggelassen, Menüs), Zeilen und Wahl der Klapplisten, die acht Plätze und ihre
Untermenüs (abgehakte aktuelle Optionen, ausgegraute Plätze, „Mehr“), die Zeilen des Werte-Panels je Stufe, Status und
Widersprüche, die Zeilen von Rechner und Code-Bericht, den Schein (Signatur, Schatten für Ellipsen, Geändert-Kennzeichen)
und gegen die gebaute Engine, dass jede Auswahl einer Stichprobe des Katalogs eine richtige Klappliste ist, eine Option
über `applyAction` läuft, die Werte eines Widerstands, die Einfügestellen einer Tabelle, Verbundene auswählen bei
kämmenden Zahnrädern, den Rechner und einen matplotlib-Import. `integration_test.py --only concept` und
`ui_smoke.py --concept-only` führen die LibreOffice-Prüfungen dieser Runde aus. `test_drawing.py` deckt die
zeichnungsweiten Einstellungen ab: Farbmodi und den Modus in einem gespeicherten Modell, geänderte Planeinheiten, die
Karte der Fortsetzungen und die Zeilen „Gleicher Name“, stumme Prüfungen und bevorzugte Einheiten (Lesen, Ändern, die
Vorgabe löscht, die Engine-Aufrufe einer Seitensitzung), die Prüfungszeilen, Formate und Namen des Rechenblatts und gegen
die gebaute Engine den Farbmodus, der mit einem gespeicherten Modell reist, die Legende, Bezeichnungen gespeicherter
Modelle als Fortsetzungshinweise, die Dokumenteinstellungen in einer frischen Sitzung (jede angebotene Einheit von der
Engine angenommen) und das Rechenblatt in jedem Format und als Tabelle. `integration_test.py --only drawing` und
`ui_smoke.py --drawing-only` führen die LibreOffice-Prüfungen dieser Runde aus. Der UI-Smoke-Test bedient die Bedienelemente des Panels mit dem Befehl `panel` (Tippen ins
Suchfeld und die Wahl eines Listeneintrags laufen über die Bedienelemente und rufen dieselben Listener auf wie ein
Anwender; Schaltflächen führen ihre Klick-Behandlung aus); echte Mausklicks werden nicht nachgestellt, weil die Peers
der Bedienelemente über die Fernbrücke nur generische Fenster ohne Listenfeld- oder Barrierefreiheits-Schnittstellen
sind. Sein erster Teil (`canvas_phase`, Windows) schickt dagegen echte Maus- und Tastatureingaben an die
Dokumentansicht: Eine Test-Mausbehandlung über die Brücke meldet Fensterpixel, so liefert ein Klick den Ursprung der
Ansicht auf dem Bildschirm und ein waagrechtes Ziehen eines Hilfsrechtecks den Maßstab; Seitenkoordinaten werden dann
über die `VisibleArea` des Controllers umgerechnet. Vor jedem Klick, Ziehen oder Tastendruck prüft der Test, dass sein
eigenes LibreOffice-Fenster vorn und unter dem Mauszeiger ist, und bricht sonst ohne Eingabe ab (`--skip-canvas` lässt
diesen Teil weg, `--canvas-only` führt nur ihn aus). Der Integrationstest installiert das Paket in ein
temporäres Profil, startet soffice ohne Oberfläche und berührt nie das Profil oder Dateien des Anwenders; unter
Linux/macOS läuft er mit einem Python, das `uno` importieren kann.

## Grenzen (Phase 1)

- Kein Overlay auf der Folie (UNO kann nicht in die Ansicht zeichnen): Eingerastet wird beim Loslassen, ohne
  Hilfslinien oder Einrastbeschriftung während des Ziehens; keine „+“-Griffe, keine schwebende Leiste, keine
  Ziehgriffe an Werten, keine Bewegung durch Ziehen; die acht Plätze des Kreismenüs stehen oben im Kontextmenü. Sonst
  wird über Seitenleiste, Kontextmenü und Menü bearbeitet. Auswahl-Parameter, die Befehle des Elements, der Haupttext
  und die Werte sind bearbeitbar (26.11.2026); freie Zahl- und Textparameter sowie Linie/Füllung noch nicht (noch kein
  Inspektor).
- Jede Bearbeitung importiert alle InventaSketch-Einheiten der Seite (ein Engine-Aufruf je Einheit plus der Zustand);
  Seiten mit sehr vielen Einheiten werden langsamer. Bei Einheiten, die gegenseitig aneinander hängen, kann die Engine
  von der Kopie des Modells der anderen Einheit ausgehen, die bei der ersten gespeichert ist (die Importreihenfolge
  kann nicht beide eigenen Modelle gewinnen lassen).
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
