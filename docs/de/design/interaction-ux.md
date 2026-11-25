# Interaktionskonzept: Griffe, schwebende Leiste, Kreismenü

Stand: Konzept und erste Umsetzung im PowerPoint-COM-Add-in (2026-11-25). Web und LibreOffice folgen denselben Regeln
(Folgeschritte unten). Englische Fassung (maßgeblich): [../../en/design/interaction-ux.md](../../en/design/interaction-ux.md).

**Warum.** Urteil des Maintainers vom 2026-11-25: „Überprüfe die Overlay-Leiste für alles, auch das Kreismenü – es ist
eine Katastrophe, die UI/UX erklärt nichts.“ Und: „Mächtig, aber verständlich für neue und ältere Nutzer; nicht zu viel
Text im Overlay; es muss konsistent sein: was findet man wo, wie bedient man was, was gehört zusammen.“

## 1. Befund der Durchsicht (COM, vor dieser Änderung)

1. **Griffe übereinander.** Auf einem kurzen Rohr lagen der Drehgriff (von PowerPoint), ein Schloss, ein Ecken-„+“, ein
   Abzweiggriff, das Verlängern-„+“ und ein zweites Leitungsende-„+“ (beide am selben Ende), die Punktrauten und das
   Verschiebekreuz von PowerPoint wenige Pixel auseinander. Jede Griffart platzierte sich für sich (`PortScreen` wich
   nur PowerPoints Griffen aus, Schlösser nur einander, Segmentgriffe gar nichts).
2. **Mehrere „+“ mit verschiedener Bedeutung.** Ecken-„+“ in der Segmentmitte (umrandet), Verlängern-„+“ hinter einem
   freien Ende (gefüllt), Bau-/Anschluss-„+“ an Anschlüssen und Leitungsenden (gefüllt, gleiches Aussehen), das „+“-
   Piktogramm in der Leiste für „Zeile anfügen“ und „Spalte anfügen“ (eines fiel sogar weg, weil beide dasselbe
   Piktogramm hatten) und das „+“ im Kasten für Einfügen. Keines sagte, was es hinzufügt.
3. **Leiste ohne Ordnung.** Die Reihenfolge wechselte je Element, keine Trenner zwischen Gruppen, reine Symbolknöpfe mit
   allgemeinen Piktogrammen („+“, „−“, Kreislauf) für Verschiedenes, in der Mehrfachauswahl dasselbe Piktogramm für
   „verteilen“ und „Diagramm anordnen“, Duplizieren und Löschen immer in der Leiste, obwohl es Strg+D und Entf gibt.
4. **Kreismenü ohne feste Plätze.** Der Ring wurde nach Priorität gefüllt, derselbe Befehl lag je Element woanders;
   „Mehr“ war ein Überlauf des Rests; Zyklus-Aktionen doppelten die Auswahllisten; kein Eintrag erklärte sich; ein
   ausgegrauter Eintrag schloss beim Klick das Menü.
5. **Einfügefeld** blieb bei einem Klick daneben offen (nur Esc schloss es).
6. **Tabellen** hatten keinen direkten Weg, eine Zeile oder Spalte dort einzufügen, wo man sie will.
7. Aus der Bestandsaufnahme aller Module (Abschnitt 3): Piktogramme für verschiedene Bedeutungen (`ArrowEnd` für
   UML-Beziehungen, Nachrichtenarten und Pfeilspitzen; `Text` für Dateneingabe und Schalter; `Rotate` für „nächster
   Winkel“ und „an Wand ausrichten“), Symbol-IDs außerhalb des gemeinsamen Satzes (zeichnen einen Platzhalter),
   Aktionen, die eine Auswahl oder Ansicht doppeln, Familien mit 8–11 Aktionen, die nicht alle in eine Leiste passen.

## 2. Was wohin gehört

| Ort | Was dorthin gehört | Regel |
|---|---|---|
| **Am Element (Griffe)** | Alles, was man durch Ziehen oder einen Klick *an einer Stelle* tut: verschieben, skalieren, drehen (PowerPoints Griffe, übernommen), Punkte und Enden (Rauten), „+“ an einer Stelle (Bauanschluss, freies Ende, Tabellenseite), Eckpunkt, Abzweig, Knotenpunkt, Schloss, Schnell-Anfügen-Dreiecke, Umriss eines Diagrammteils. | Direkte Bearbeitung zuerst. Ein Aussehen je Bedeutung (Abschnitt 4). Nie zwei Griffe übereinander. |
| **Schwebende Leiste** | Die 3–5 häufigsten Befehle *dieses* Elements, dann Ansichten und Transformationen, dann Stil-Auswahlen, dann „⋯“. | Nur Kontext, klein, Piktogramme mit Tooltips; kurzer Text nur, wo kein Piktogramm eindeutig ist. Wächst nie mit Inhalt. |
| **Kreismenü** (Rechtsklick, Umschalt+F10, „⋯“) | Alles zum Element, in acht festen Plätzen gruppiert. | Vollständig, beschriftet, Hinweis in einer Zeile; derselbe Platz bedeutet für jedes Element dasselbe. |
| **Menüband** | Einfügen (Bibliothek, Suchen & Einfügen), dokument- und folienweite Einstellungen (Fangen, Verankern, Liniengruppe, Akzent, Planmaßstab, Schwarz-Weiß), Werkzeuge (Folien bereinigen, Kompatibilität, Export). | Nichts Elementspezifisches. |
| **Seitenleisten-Editor** | Jeder Parameter (Inspektor), Datentabellen, Bibliothek mit Suche. | Die „Langform“; für die Schnellskizze nie nötig. |
| **Tastatur** | R / Umschalt+R, X, Y, Strg+D, Entf, Strg+Leertaste, Umschalt+F10, Esc, Tab (nächstes Teil der Baugruppe), Alt (kein Fangen), Umschalt (15°-Schritte). | In Tooltips und Kreismenü-Hinweisen genannt. |

## 3. Bestandsaufnahme: Interaktionen je Elementfamilie (alle Module)

Abgeleitet aus der Engine (Aktionen, Ansichten, Anker von `ObjectType`, `Building.Ports`, `Flow.CanChain`,
`Polylines`, Teile).

| Familie (Modul) | Geometrie | Ansichten | Am Element | Leiste (Schlüsselaktionen) | Hinweise |
|---|---|---|---|---|---|
| Zahnräder, Hohlräder, Kegel-, Schnecken-, Zahnstange (Mechanik) | Box | 2–5 | Bau-„+“ je freier Eingriffsseite / innen / Spitze / Nabe, Schloss je Eingriff | Naben-Auswahl, Schrägverzahnung, wahre Größe | Kopplung, Bewegungsmodus, Baugruppe wählen |
| Planetensätze, Differential, Steg | Box | 2–3 | „+“ auf freien Bolzen | Planeten ±, festes Glied | in Einzelteile zerlegen |
| Wellen, Lager, Kupplungen, Dichtungen | Box | 3–4 | „+“ an freien Achsenden (ziehen: dehnen), Schloss je Teil auf der Welle | hohl | Räder fahren mit der Welle |
| E-Maschinen, Antriebsblöcke, Baukasten (Maschinen) | Box | 2–4 | „+“ an Achsenden und Anschlüssen | Bauform-, Typ-Auswahl | treibt die Welle |
| Koppelgetriebe: Glieder, Gelenke, Mechanismen (Kinematik) | Punkte / Box | 2 | Rauten auf Gelenken, „+“ an freien Gelenken | Winkel ±, gekreuzt, F = n, Koppelkurve, Zwischenlagen | Bewegungsmodus, Mechanismus zerlegen |
| Elektro-Symbole | Box | 1–4 | „+“ an freien Klemmen | Zustand, Polarität, Kennzeichen … | Installationsansichten wechseln mit Wänden |
| Drähte, Kabel, Busse, Leiterbahnen | Punkte (Ecken) | 1 | Rauten, ein „+“ je freiem Ende, Eckpunkte, Abzweig, Knotenpunkt an Kreuzungen, Schloss je angedocktem Ende | Führung-Auswahl, Brücken | angedockte Enden folgen |
| Fluid-Symbole, Ventile | Box | 1–2 | „+“ an freien Anschlüssen | Betätigung, Federseite, Medium | |
| Rohre, Kanäle, Prozess- und Energieleitungen | Punkte (Ecken) | 1 | wie Drähte | Führung, Leitungsart | |
| Prozess-, Energieanlagen, Gebäudetechnik | Box | 1–3 | „+“ an freien Anschlüssen | Art, Medium, Stufen | Planmaßstab, Baugruppen-Ansichtswechsel |
| Wände, Räume (Architektur) | Punkte (Ecken) | – | Rauten, Eckpunkte, „+“ an freien Wandenden | dicker/dünner, Art-Auswahl, 1:50 | Öffnungen folgen der Wand |
| Öffnungen, Treppen, Plansymbole | Box | 1–2 | – | Breite ±, an Wand ausrichten | |
| Diagramme (auch Figurendiagramme) | Box | – | Griffe auf Grenz-, Wertlinien und Markern; Teil-Umriss nach Klick auf ein Teil | Daten …, Stil, Kurve skizzieren | Teile: Reihe, Achse, Gitter, Legende, Grenze … |
| Diagrammknoten: Flussdiagramm, UML, BPMN, ER, Regelung | Box | 1–2 | Schnell-Anfügen-Dreiecke, Anschlüsse beim Annähern | Zeilen ±, Schalter | „Ändern in …“ nach Schnell-Anfügen |
| Pfeile, Linien, Hinweislinien, Maße, Verbinder | Punkte | – | Rauten, „+“ an freien Enden (Polylinien), Eckpunkte | Linientyp, Spitzen, Linienart | Typ wandeln, Beschriftungsmenü |
| Tabelle, Text, Stückliste (Annotation) | Box | – | Tabelle: „+“ an jeder Seite | Kopfzeile, letzte Zeile/Spalte entfernen | |
| Patent-Bezugszeichen | Punkte | – | Rauten | Bezugszeichen, Unterstreichung | neu nummerieren (Mehrfachauswahl) |
| Karten: Routen, Flächen, Marker | Punkte / Box | 1–3 | Rauten, Eckpunkte | Punkt ±, umkehren | |
| Fahrzeuge, Straßen, ADAS (Automotive) | Box / Punkte | 3–4 | „+“ an Straßenenden, Halterungen | Ansicht, Richtung | |
| Roboter, Zellen, Werkzeuge (Robotik) | Box / Punkte | 2 + Stil | „+“ am Flansch | Gelenke ±, Grundstellung, Zwischenlagen | Bewegung |
| Geometrie (Mathe) | Punkte | – | Rauten | Seiten ±, Winkel …, Teilungen | |
| Mehrfachauswahl (beliebig) | – | – | PowerPoints | eine Ansicht für alle · ausrichten, verteilen · Diagramm anordnen · Maß, Format übertragen, neu nummerieren | nur, was etwas ändert |

## 4. Eine Bildsprache für Griffe

| Aussehen | Bedeutung | Bedienung |
|---|---|---|
| PowerPoints weiße Quadrate/Kreise am Rahmen, Drehpfeil darüber, Verschiebekreuz unter kleinen Rahmen | skalieren, drehen, verschieben (PowerPoints eigene) | von InventaSketch mit Fangen übernommen; InventaSketch zeichnet nie darüber |
| **Orange Raute** auf einem Punkt | dieser Punkt (Ende, Ecke, Gelenk) | ziehen verschiebt ihn (fängt und dockt an); Klick auf eine Ecke wählt sie (Leiste: teilen, entfernen); Doppelklick entfernt eine Ecke |
| **Gefüllter blauer Kreis mit weißem „+“** | *hier etwas hinzufügen* – der Tooltip sagt was (Gegenrad, nächster Wellenabschnitt, Leitung oder Element am Anschluss, Leitung fortsetzen, Zeile darüber …) | Klick: Einfügefeld mit dem Passenden zuerst (Tabellenseiten: fügt sofort ein); an einem Wellenende ziehen: dehnen; an einem freien Leitungsende ziehen: weiterzeichnen |
| **Kleiner hohler blauer Punkt** im Segment | eine neue Ecke | Klick fügt sie ein, Ziehen knickt die Linie |
| **Hohler Kreis mit Stich** neben einem Segment | ein Abzweig | zu einem Anschluss oder frei ziehen; Klick: kurzer Stich |
| **Hohler Kreis mit Kreuz und Punkt** an einer Kreuzung | die kreuzenden Leitungen verbinden (Knotenpunkt) | Klick |
| **Blaues Schloss** (zu) / **graues Schloss** (offen) neben einem Berührpunkt, dünne Zuleitung | gekoppelt, bewegt sich mit / berührt, aber gelöst | Klick schaltet um |
| **Blaues Dreieck** vor einer Seite (Diagrammknoten) | das nächste Element auf dieser Seite | Klick: verbundene Kopie; Ziehen: mit anderem Element verbinden oder ablegen |
| **Blau umrandetes Quadrat mit kleinen Pfeilspitzen** auf einem Wert (Grenzlinie, Wertlinie, Marker eines Diagramms) | ein Wert, der auf der Zeichnung gezogen wird; die Pfeilspitzen zeigen die erlaubte Richtung | ziehen: die Linie folgt (geführt), der neue Wert steht neben dem Zeiger und rastet ein (ein Zehntel des Achsschritts); loslassen = ein Rückgängig-Schritt; Esc bricht ab; ein Klick wählt dieses Teil |
| **Blauer Umriss** eines Teils (Diagramme) | das gewählte Teil | Leiste und Kreismenü wirken darauf |
| **Rote gestrichelte Hilfslinien, roter Punkt, Beschriftung** beim Ziehen | Fangen und woran angedockt wird | Alt setzt aus |
| **Zweitfarbige (blaue) Vorschau** beim Ziehen | was mitbewegt wird (gekoppelte Nachbarn) | |

Größen: Rauten 10 px, „+“ 13 px, Punkte 7 px, Schlösser 15 px (logische Pixel × Bildschirmskalierung); jedes Ziel
reagiert im Umkreis von 12 px (24 px Trefferfläche) und der nächste Griff gewinnt, so nehmen sich Nachbarn keine Klicks
weg; PowerPoints eigener Griff gewinnt, wenn er näher ist (eine Raute gewinnt gegen den Skaliergriff an derselben
Stelle).

**Anordnung.** `HandleLayout` (COM, reine Funktion, getestet) platziert alle Griffe des Elements gemeinsam:
PowerPoints Griffe, Rauten und Dreiecke bleiben, wo sie sind; die anderen nehmen nach Priorität („+“ 90, Tabellen-„+“
85, Schloss 70, Knotenpunkt 60, Eckpunkt 40, Abzweig 30) den ersten freien Platz auf einer Suche um ihren Wunschplatz –
weiter hinaus in ihrer Richtung, bis 40° geneigt, Schlösser und Abzweige auch rund um ihren Anker, Eckpunkte nur entlang
ihres Segments – mit mindestens 3 logischen px Abstand. Optionale Griffe (Eckpunkt, Abzweig) ohne freien Platz werden
ausgeblendet; auf Elementen unter 24 px gibt es keine (hineinzoomen). Ein verschobenes „+“ behält eine dünne gestrichelte
Linie zu seinem Anschluss.

## 5. Schwebende Leiste

Reihenfolge, für jedes Element gleich, Gruppen durch einen dünnen Strich getrennt:

1. **Element:** „Ändern in …“ (direkt nach Schnell-Anfügen, mit Text) · **Einfügen** (Piktogramm *und* Text – die
   Hauptaktion) · gewählte Ecke teilen / entfernen · Text bearbeiten · bis zu fünf Schlüsselaktionen (Piktogramm nur,
   wenn eindeutig *und* von keiner anderen Aktion des Elements benutzt, sonst kurzer Text) · 1:50 · Kurve skizzieren ·
   F = n, Koppelkurve, Zwischenlagen · zerlegen · Bewegungsmodus · Baugruppe wählen.
2. **Ansicht und Transformationen:** bis zu drei Ansichtsknöpfe oder eine Ansichtsauswahl · spiegeln ↔ · spiegeln ↕ ·
   um 90° drehen – nur, was die Zeichnung sichtbar ändert.
3. **Stil:** Auswahllisten mit dem aktuellen Wert als Piktogramm (Linientyp, Spitzen, Linienart, Führung …).
4. **⋯** – das Kreismenü (alles andere, gruppiert). Duplizieren und Löschen sind dort (und Strg+D, Entf).

Tooltips: „was passiert“ plus Tastenkürzel („Horizontal spiegeln (X)“, „Linienart: Gestrichelt – klicken zum Wählen“).
Ein gewähltes Diagrammteil hat eine eigene Leiste (Teilname, seine Aktionen, Teileliste, ganzes Element, ⋯). Die
Mehrfachauswahl hat die Gruppen eine Ansicht für alle | ausrichten und verteilen | Diagramm anordnen (Text
„Diagramm ↓“, „Diagramm →“) | Maß, Format übertragen, neu nummerieren.

## 6. Kreismenü

Acht feste Plätze für jedes Element, im Uhrzeigersinn ab 12 Uhr (oben = bearbeiten/Text, rechts = einfügen,
unten = Stil, links = Ansicht/anordnen):

```
                 Text …
     Anordnen ▸           Bearbeiten ▸
  Ansicht ▸                    Einfügen …
     Löschen              Duplizieren
                 Stil ▸
```

- **Text …** – Text bearbeiten (Doppelklick). **Bearbeiten ▸** – die eigenen Befehle des Elements (Typ-Aktionen,
  Linientyp, Beschriftung, „Ändern in …“, zerlegen, skizzieren, Bewegungsmodus, Teile); bei gewähltem Diagrammteil
  dessen Befehle (mit dem Teilnamen als Titel).
- **Einfügen …** – das Einfügefeld. **Duplizieren** (Strg+D). **Löschen** (Entf).
- **Stil ▸** – ein Eintrag je Auswahl („Linienart: Gestrichelt ▸“), jeder öffnet seine Optionen mit Piktogrammen, die
  aktuelle markiert. Aktionen, die nur eine solche Auswahl setzen, stehen nicht noch einmal unter Bearbeiten.
- **Ansicht ▸** – die Ansichten (aktuelle markiert) und der Planmaßstab. **Anordnen ▸** – um 90° in beide Richtungen
  drehen, spiegeln, Baugruppe wählen, als Folien animieren.
- Ein Platz ohne Inhalt für dieses Element bleibt, ausgegraut (ein Klick darauf lässt das Menü offen). Ein Platz mit
  einem Eintrag zeigt ihn direkt, außer Bearbeiten, Stil und Anordnen, die ihren Namen behalten.
- Untermenüs beginnen mit „← Zurück“; mehr als elf Einträge gehen in „Mehr ▸“ weiter.
- Beim Überfahren zeigt ein Eintrag seinen vollen Text und einen Hinweis in einer Zeile unter dem Ring. Esc, ein Klick
  daneben oder in die Mitte schließt das Menü.

## 7. Rückmeldung und Barrierearmut

- Fangen: rote Hilfslinien, ein Punkt am Fangpunkt und eine Beschriftung mit dem Ziel („Teilkreis“, „Achse“,
  „Bolzen 2“); Alt setzt aus. Gekoppelte Nachbarn erscheinen beim Ziehen blau. Schlösser zeigen angedockt (blau) und
  gelöst (grau) und schalten mit einem Klick.
- Rückgängig ist immer ein Schritt je Geste; Esc bricht immer ab (Ziehen, Einfügen, Weiterzeichnen, Kreismenü,
  Einfügefeld, Auswahlliste).
- Trefferflächen von 24 logischen px bei der Bildschirmskalierung; Leiste und Kreismenü mit mindestens 9–10 px Schrift,
  kontrastreich im hellen und dunklen Office-Design; Positionen hängen nie vom Element ab (Leistenreihenfolge,
  Kreismenüplätze).
- Hinweise sind einzeilig; Tooltips sagen, was passiert, nicht wie es funktioniert; zweisprachig.

## 8. Jetzt umgesetzt (COM, 2026-11-25)

- `OnSlide/HandleLayout.cs` (reine Anordnung, Tests `tests/InventaSketch.AddIn.Tests/HandleLayoutTests.cs`) und
  `OnSlideController.Handles.cs`: eine Anordnung für Rauten, „+“, Schlösser, Knotenpunkte, Eckpunkte, Abzweige,
  Tabellen-„+“; Trefferprüfung „nächster Griff“; Griffliste für UI-Tests (`LastHandles`, Protokollzeile „Handles: …“).
- Ein „+“ je freiem Leitungsende (das „+“ des Leitungsendes zeichnet beim Ziehen weiter); Eckpunkte statt eines
  zweiten „+“.
- Leiste neu gruppiert (`OnSlideController.Menus.cs`), Einfügen mit Text, „⋯“ öffnet das Kreismenü, Piktogramme nur
  wenn eindeutig, Mehrfachauswahl gruppiert.
- Kreismenü mit acht festen Plätzen, Untermenüs, Hinweisen (`PieMenu.Hint`), ausgegraute Plätze lassen das Menü offen;
  `LastPieItems`, Protokollzeile „Pie: …“.
- Einfügefeld schließt bei Klick daneben (`InsertSearch.ClickedOutside`) und wenn ein anderes Fenster aktiv wird.
- Tabellen: Aktionen gruppiert `side.top/bottom/left/right` (Engine, `TableType`), als „+“ an jeder Seite gezeichnet;
  eine Zeile oben kommt unter die Kopfzeile.
- Griffe auf Werten (`OnSlideController.Handles.cs`, Engine `Registry.DragHandles` / `MoveHandle`): in der Anordnung als feste Griffe (Vorrang vor dem Verschieben des Körpers im Diagrammfeld), Tooltip mit dem Wert, geführtes Ziehen mit Live-Vorschau des geänderten Teils und seines neuen Werts, ein Rückgängig-Schritt, Esc bricht ab, ein Klick wählt das Teil; `DragHandlesOf` für UI-Tests.
- UI-Szenarien: `overlay-handles`, `pie-menu`, `insert-box-dismiss`, `table-plus`, `chart-drag-handles` (tests/powerpoint-com/scenarios).

## 9. Folgeschritte (in Phasen, mit Abnahmekriterien)

**Phase 1 – die Sprache in COM vollenden**
1. *Griffe über Diagramme hinaus*: dieselben Griffe für jedes Element, dessen Typ Ziehgriffe anbietet (Maßwerte,
   Winkel, Robotergelenke, Achsabstand von Zahnrädern). Abnahme: jeder solche Typ nennt seine Griffe in `DragHandles`,
   das COM-Overlay braucht keine Änderung; die Griffe behalten ihren Platz in der Anordnung.
2. *Abhängigkeiten beim Überfahren hervorheben*: Überfahren eines Schlosses oder von „Baugruppe wählen“ umrandet jede
   mitbewegte Einheit in der Zweitfarbe. Abnahme: ein Rad auf einer Welle zeigt Welle und deren andere Räder, sonst
   nichts.
3. *Symbol-Hygiene in der Engine*: jede Aktions-Symbol-ID steht in `IconIds.All` (oder eine neue ID mit Piktogramm in
   allen drei Hosts); ein Piktogramm, eine Bedeutung (UML-Beziehungen und Nachrichtenarten werden Auswahlen mit
   Options-Piktogrammen; `Text` nur für Textbearbeitung). Abnahme: ein Engine-Test schlägt bei unbekannter ID oder einer
   ID mit zwei Bedeutungen in einem Typ fehl.
4. *Aktionen, die eine Auswahl oder Ansicht doppeln*, bekommen `ParamKeys` oder entfallen (Welle hohl als Schalter und
   Ansicht, Roboter-Stil neben Ansichten, `view.*`-Aktionen in sechs Modulen, `map.point.add/remove` neben Eckpunkten).
   Abnahme: das Kreismenü dieser Elemente nennt jedes Ding einmal.
5. *Zwischen Zeilen/Spalten einer Tabelle*: „+“-Punkte auf den inneren Zeilen- und Spaltenlinien (dort einfügen).
   Abnahme: ein Klick auf den Punkt zwischen Zeile 2 und 3 fügt dort eine Zeile ein.
6. *Ecke dort einfügen, wo der Punkt gezeichnet ist*, wenn die Anordnung ihn entlang des Segments verschoben hat.
   Abnahme: die neue Ecke erscheint unter dem Punkt.

**Phase 2 – dasselbe Konzept im Web-Add-in und in LibreOffice**
7. Web (`hosts/office-web/src/ui/stage.ts`, `contextbar.ts`, Kreismenü): gleiche Griffbilder, Anordnung (die reine
   `HandleLayout` portieren), Leistengruppen und Kreismenüplätze. Abnahme: die Web-Szenarien zeigen für Zahnrad, Pfeil,
   Rohr und Tabelle dieselben Leisten-IDs und Kreismenüplätze wie COM.
8. LibreOffice: das Overlay auf der Zeichenfläche (ROADMAP-Punkt) von Anfang an nach diesem Konzept.

**Phase 3 – Hilfe für neue Nutzer**
9. Hinweise beim ersten Mal: die erste Auswahl eines Elements mit „+“ zeigt drei Sekunden lang eine Zeile „Blaues +
   fügt hier Passendes hinzu“ (einmal je Nutzer). Abnahme: einmal gezeigt, nach einem Klick nie wieder.
10. Kreismenü und Leiste in größerer Darstellung (125 %) für schlechtes Sehen, über die Einstellungen im Menüband.
    Abnahme: alle Trefferflächen und Texte skalieren; die Anordnung hält Griffe auseinander.

## 10. Tests

Unit-Tests (ohne PowerPoint): Griffanordnung (keine Überlappung auf dem kurzen Rohr der Meldung, Prioritäten,
Ausblenden, PowerPoints Griffe dünner Rahmen), Kreismenüplätze und Untermenüs, Leistengruppen und Piktogrammregel,
Schließen bei Klick daneben, Tabellenseiten-Aktionen (`tests/InventaSketch.Modules.Annotation.Tests/TableSideTests.cs`).
UI-Szenarien in echtem PowerPoint (vom Koordinator auszuführen, PowerPoint geschlossen): `overlay-handles`,
`pie-menu`, `insert-box-dismiss`, `table-plus`; Bildschirmfotos unter `local-resources/powerpoint-tests/…` zeigen das
Ergebnis zur Durchsicht.
