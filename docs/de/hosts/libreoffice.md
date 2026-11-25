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
         sidebar.py   Deck „InventaSketch“, Panels „Auswahl“ und „Bibliothek“; library.py: Katalogfilter, Vorschaubilder
         ux.py        Regeln des Auswahl-Panels, zuletzt benutzte Elemente, Buchführung der Seitensitzung, Regeln der
                      Bedienung auf der Folie (ohne UNO)
         canvas.py    Bedienung auf der Folie: Einrasten nach dem Ziehen, Strg+Leertaste, Kontextmenü (UNO-Eingabe-Abfang)
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
  *Modell der ausgewählten Einheit bearbeiten*, *Ausgewählte Einheit aus dem Modell neu zeichnen*,
  *Bezugszeichen hinzufügen*, *Patentprüfung (Zeichnungen) …*, *Mit vorheriger Folie vergleichen (Varianten)*,
  *Daten gelöschter Einheiten entfernen*, *Über InventaSketch*.
- **Varianten** (25.11.2026, Impress und Draw): Eine duplizierte Folie behält die IDs der Einheiten. *Mit vorheriger
  Folie vergleichen* markiert, was sich von der vorherigen Folie unterscheidet (Engine `diff`, beide Folien wie
  Seitensitzungen geladen): neue Einheiten grün, geänderte orange, entfernte als grau gestrichelter Rahmen an ihrer
  alten Stelle, dazu eine Legende oben rechts; die Markierungen sind einfache Formen mit dem Namen `ISK_DIFF …`, ein
  erneuter Aufruf entfernt sie (jeweils ein Rückgängig-Schritt). LibreOffice benennt eine duplizierte Form, deren Name
  auf Ziffern endet, um („…eb12“ → „…eb 1“); beim Lesen einer Einheit wird der Name aus ihren Attributen
  wiederhergestellt, weil in .pptx der Name die Einheiten-ID trägt. Geprüft: `integration_test.py` (1 neu, 1 geändert,
  1 entfernt, ein unverändertes Zahnrad ohne Markierung, Namen wiederhergestellt; Folie als `compare.png` exportiert).
- **Patentprüfung** (25.11.2026, Impress und Draw): ein Dialog mit dem Amt (DPMA, EPA, PCT, USPTO oder das gemeinsame
  sichere Profil) und den Befunden aller Seiten mit InventaSketch-Einheiten (Engine `preflight`, jede Seite wie eine
  Seitensitzung geladen, native Verschiebungen zählen also mit) – Fehler zuerst, als „Folie 2 · Fehler · Meldung“; die
  Wahl eines Befunds (Pfeiltasten oder Klick) zeigt Vorschlag und Fundstelle und springt zu Folie und Einheit. Der
  Haftungshinweis wird immer angezeigt. Ohne sichtbares Fenster (headless, Tests) wird das Ergebnis protokolliert.
  Geprüft: `integration_test.py` (ein Zahnrad außerhalb der Nutzfläche auf Folie 2 gefunden und seiner Einheit
  zugeordnet) und `ui_smoke.py` (der Dialog öffnet sich in einem echten Fenster, die Pfeiltaste nach unten springt zu
  Folie 2 und wählt das Zahnrad aus, Escape schließt ihn).
- **Seitenleiste „InventaSketch“**, zwei Panels (siehe UI/UX-Runde unten):
  - *Auswahl* folgt der Auswahl im Dokument (Auswahl-Listener) und zeigt, was mit den ausgewählten
    InventaSketch-Einheiten möglich ist – nur im Kontext.
  - *Bibliothek*: ein Suchfeld für die ganze Bibliothek (Engine-`search`), die Kategorieliste (erster Eintrag
    *Vorschläge*: die zur Auswahl passenden Teile, dann die zuletzt benutzten), die Elementliste mit Vorschau
    (SVG-Vorschaubild der Engine); Doppelklick oder *Einfügen* fügt das Element ein – ein zur ausgewählten Einheit
    passendes Teil dort, wo es passt, alles andere in der Mitte der Folie/Seite (Calc: des sichtbaren Zellbereichs).
- **Auf der Folie/Seite** (Impress, Draw; siehe Runde „Bedienung auf der Folie“ unten): Eine mit der Maus gezogene
  Einheit rastet beim Loslassen ein (Zahneingriff, Achsen, Anbaustellen, Flansche …), gekoppelte Teile folgen;
  Strg+Leertaste öffnet das Bibliotheks-Panel mit dem Cursor im Suchfeld (tippen, Eingabe fügt ein); das Kontextmenü
  einer Einheit beginnt mit den Aktionen des Auswahl-Panels.
- **Skript-API** (DispatchHelper / Makros), jeweils `inventasketch.libreoffice:<Befehl>`:
  `insert?item=<id>[&x=<mm>&y=<mm>]`, `insertFitting?item=<id>`, `insertSearch?text=<Suchtext>`, `library`,
  `editModel`, `setView?view=<id>`, `redraw`, `numeral`, `transform?kind=mirrorH|mirrorV|rotate90`, `explode`,
  `drawTrace`, `drawGhosts`, `build?port=<Port-ID>&item=<id>`, `turn?degrees=<Winkel>`,
  `arrange?action=<ID aus arrangeOptions>`, `assembly`, `panel?control=<Schlüssel>&text=<Text>` /
  `panel?control=<Schlüssel>&item=<Eintrag>` / `panel?press=<Schlüssel, Beschriftung oder action:<id>>` (bedient ein
  Bedienelement des offenen Panels wie ein Anwender), `snapMove[?px=<Pixel>&undo=<n>]` (ausgewählte Einheit(en) nach
  einer nativen Verschiebung einrasten, wie nach dem Ziehen mit der Maus), `quickInsert[?text=<Text>]`
  (Bibliotheks-Panel mit dem Cursor im Suchfeld, wie Strg+Leertaste), `patentCheck[?profile=<Amts-ID>]`, `compare`, `cleanup`,
  `about`. Integrations- und
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
  verteilen, wie das erste, Maß zwischen beiden, Bezugszeichen neu nummerieren –, jeweils nur, wenn sie etwas ändern,
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
  Menüs kostet eine Seitensitzung (alle Einheiten der Seite werden importiert).
- **Ohne Overlay nicht möglich (dokumentiert, nicht versucht):** Hilfslinien und Einrastbeschriftung während des
  Ziehens, die Live-Vorschau der Kopplung, „+“-Griffe auf der Folie, Strecken eines Wellenendes und Bewegungsmodus durch
  Ziehen, das Kreismenü. Das Aufklappen eines zugeklappten Bibliotheks-Panels bei Strg+Leertaste entfiel:
  `XDeck.getPanels()`/`XPanel.expand` brachte soffice 26.8 bei gezeigtem Deck zum Absturz; ein vom Anwender
  zugeklapptes Bibliotheks-Panel bleibt daher zu.

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

## Tests

```powershell
python -m unittest discover -s hosts/libreoffice/tests -p "test_*.py"     # Unit-Tests, ohne LibreOffice
"C:\Program Files\LibreOffice\program\python.exe" hosts/libreoffice/tests/integration_test.py --oxt dist/InventaSketch-LibreOffice-0.1.0.oxt --out <Ordner>
"C:\Program Files\LibreOffice\program\python.exe" hosts/libreoffice/tests/ui_smoke.py --oxt dist/InventaSketch-LibreOffice-0.1.0.oxt --out <Ordner>
```

Die Unit-Tests decken die Abbildung Plan → UNO, Metadaten, Bibliotheksfilter, Texte, .NET-Suche, das Protokoll (mit
einer Ersatz-Engine: Fehler, Absturz, Zeitlimit, fremde Zeilen), das Weiterreichen in den Hauptthread, die Regeln des
Auswahl-Panels und die Buchführung der Seitensitzung (`test_ux.py`) und – wenn die Engine gebaut ist – echte
Zeichenpläne von Bibliothekselementen sowie die Engine-Abläufe hinter dem Panel mit in getrennten Sitzungen gezeichneten
Modellen ab (Suche mit passenden Teilen, passend einfügen, Anordnen über Einheiten hinweg, ein Getriebezug über
Einheiten gedreht, Zerlegen). Der UI-Smoke-Test bedient die Bedienelemente des Panels mit dem Befehl `panel` (Tippen ins
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
  Hilfslinien oder Einrastbeschriftung während des Ziehens; keine „+“-Griffe, kein Kreismenü, keine Bewegung durch
  Ziehen. Sonst wird über Seitenleiste, Kontextmenü und Menü bearbeitet. Parameter, Stil und Text sind noch nicht
  bearbeitbar.
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
