# Diagramme v2 – matplotlib, aber interaktiv

Stand: Plan vom 25.11.2026 (Wunsch des Maintainers); Stufe 1 (Teile) am 25.11.2026 für Engine und PowerPoint (COM) umgesetzt, Web und LibreOffice folgen. Englische Fassung: [docs/en/design/charts-v2.md](../../en/design/charts-v2.md).

## Ziel

Alles, was eine technische Abbildung braucht – was matplotlib im Code bietet – interaktiv: Teile des Diagramms
auswählen, Hover-Hinweise, Kontextleiste und Kreismenü.

- Jedes Teil auswählen (jede Kurve, Achse, Gitter, Achsenbeschriftung, Legende, Wertlinie, Bereich, Anmerkung), ein- und
  ausschalten, formatieren – auch mit PowerPoints eigenen Formatwerkzeugen, die je Teil zurückgeführt werden.
- Mehrere Diagramme übereinander mit gemeinsamer x- oder y-Achse, mit Phasenlinien durch alle (z. B. oben Leistung,
  unten Rotortemperatur auf einer Zeitachse).
- Achsenbrüche, einzelne Wertlinien (waagerecht und senkrecht, mit Beschriftung), schattierte oder schraffierte Bereiche
  (zwischen zwei Kurven, unter einer Kurve, zwischen zwei x-Werten), Maßpfeile („Zeitgewinn“), Text und Bezugszeichen
  für technische Abbildungen.
- Kurven aus Daten, aus Funktionen (konstant, Rampe, Sprung, Rechteckpuls, Sägezahn, Dreieck, Sinus, Exponential, PT1,
  Kreis/Ellipse) und aus einer Handskizze, aus der eine geglättete oder idealisierte Kurve wird.
- Rückwärts: Ein Bild (Zwischenablage, Foto, Bildschirmfoto) wird zu einem bearbeitbaren Diagramm.
- Export als matplotlib-Code, als LaTeX (pgfplots für Diagramme, TikZ für Flussdiagramme und Skizzen; Texte bleiben
  LaTeX-Text, bearbeitbar und in der Dokumentschrift gesetzt) und als SVG mit Texten als Text und Teilen als benannten
  Gruppen (dazu PNG).

Prüfbeispiel: die Beispielfigur des Maintainers (oben Leistung P mit den Phasen „Vorkonditionierung“ und
„Volllastphase“; unten Rotortemperatur mit gestrichelter und durchgezogener Kurve, schraffiertem Bereich dazwischen,
Grenzlinie „Grenztemperatur“, Maßpfeil „Zeitgewinn“ und Bezugszeichen 40–46; gemeinsame Zeitachse, senkrechte
Phasenlinien durch beide Diagramme).

Weitere Prüffiguren (Maintainer, 25.11.2026):

- **Zeitdiagramm einer Entscheidungslogik** (drei Panels übereinander auf einer Zeitachse, Prinzipdarstellung ohne
  Skalenbeschriftung): eine steigende Kurve gegen eine gestrichelte Grenzlinie mit Beschriftung und Bezugszeichen an der
  Achse; eine senkrechte gestrichelte Markierung („t_krit“) durch alle Panels und ein waagerechter Zeitpfeil
  „t_krit“ zwischen zwei Panels; im mittleren Panel eine durchgezogene und eine gestrichelte Kurve, schraffiert nur dort,
  wo die eine über der anderen liegt; Rechteckpulse (durchgezogen und gestrichelt) im unteren Panel; Bezugszeichen 63–69
  mit Bezugslinien; zentrierter Figurentitel.
- **Temperatur über der Zeit** (Datenachsen mit Teilung, nur waagerechtes gepunktetes Gitter): mehrere waagerechte
  gestrichelte Referenzlinien mit Beschriftung („Heizstempel (15)“, „Betriebsmaximum 60 Grad C“); Kurven, die sich einer
  Asymptote nähern (PT1/Exponential), enden und später weiterlaufen (Lücke); offene Kreismarker auf einer Kurve mit
  Wertbeschriftung („31 s“, „77 s“); senkrechter Maßpfeil zwischen zwei Niveaus („35 K“); Beschriftungen an den Kurven;
  Bildunterschrift unter dem Diagramm.
- **Flussdiagramm** (Modul Diagramme, nicht Charts): Startkreis, Entscheidungen mit Ja/Nein-Ausgängen, Prozesskästen mit
  zweizeiligem Text, Rücksprung in die Seite eines früheren Kastens, gestrichelter Notizkasten mit gestricheltem Pfeil,
  Ausgänge, die seitlich in einen Kasten münden, Endpunkt als Doppelkreis.

Damit gehört zu Stufe 2 auch: Prinzipachsen (Pfeil, ohne Teilung) je Panel, Markierungen über alle Panels, Pfeile
zwischen Panels, Bereiche „wo Kurve A über Kurve B liegt“, Punktmarker mit Beschriftung, Maßpfeile zwischen zwei
y-Werten, Lücken in Kurven; zu Stufe 3 Pulse und asymptotische Kurven.

## Modulschnitt

- **Diagramme & Funktionen** (Modul `charts`): Datendiagramme, Kennlinien und Kennfelder, Prinzipdiagramme,
  Funktionsgraphen und Koordinatensysteme. Ein Achsensystem für alle. Koordinatensysteme und Funktionsgraphen des
  Mathe-Moduls ziehen hierher um (ihre Einträge treten über `LibraryItem.Alternative` zurück; bestehende Folien werden
  weiter gezeichnet).
- **Geometrie & Vektoren** (Modul `math`): Dreiecke, Kreise, Winkel, Konstruktionen, Punkte und Vektoren. Sie rasten am
  Koordinatensystem eines Diagramms ein (Punkt auf Kurve, Vektor im Achsenkreuz).

## Fundament: Teile eines Elements (Core, alle Module)

- Jedes Primitiv hat bereits einen `Tag`. Ein **Teilschlüssel** ist dieser Tag, wo nötig mit Index: `series.0`,
  `axis.x`, `axis.y.1`, `grid.major`, `ticklabels.x`, `legend`, `ref.2`, `region.0`, `note.3`.
- `SketchObject.Parts`: je Teilschlüssel ein `PartLook(Style? Style, bool Hidden)`. Ein Schlüssel ohne Index (`series`)
  gilt für alle Teile dieser Art; der Schlüssel mit Index gewinnt. `Registry.RenderLocal` wendet es nach dem Objektstil
  an – der einzige Renderpfad, also zeigen SVG, Zeichenpläne, Web und LibreOffice es gleich.
- `ObjectType.Parts(o, ctx)`: die auswählbaren Teile mit Bezeichnung und Art; `ObjectType.PartActions(o, part, ctx)`:
  Aktionen für ein Teil (allgemein: aus-/einblenden, dicker/dünner, Strichart, zurücksetzen; typabhängig: glätten,
  Marker, Fläche darunter, Wertlinie hier, logarithmisch, Bruch hier …).
- `Registry.PartAt(o, point, ctx, tolerance)`: Treffertest auf den getaggten Primitiven (Vorrang: Texte und Griffe, dann
  Linien, dann Flächen) – Grundlage für Hover, Auswahl und Tab-Durchlauf.
- Engine: `parts`, `partAt`, `partActions`, `applyPartAction`, `setPartLook`.
- PowerPoint: jede Form einer Einheit merkt sich ihren Teilschlüssel (Tag `ISK_PART`) und wie sie gezeichnet wurde; eine
  Änderung mit PowerPoints Werkzeugen an einigen Formen geht in das Aussehen genau dieser Teile (Slidemax
  `ChartFormatting`: Füllung, Linienfarbe, Breite und Strichart getrennt vergleichen; Designfarben wie angezeigt
  vergleichen). Eine Form innerhalb einer Einheit auswählen wählt das Teil: Kontextleiste und Kreismenü zeigen seine
  Aktionen.

## Diagrammmodell v2

Ein Diagrammtyp „Abbildung“ mit strukturierter Beschreibung (JSON im Parameter `spec`, versioniert); die bestehenden
Diagrammtypen bleiben und werden zu Vorlagen, die diese Beschreibung füllen:

- `panels`: je eine y-Achse (linear/log/Kategorie, Bereich, Einheit, Beschriftung, Brüche), Höhenanteil, Titel;
- `x`: gemeinsame x-Achse (oder je Panel), Phasenlinien (senkrechte Linien mit Beschriftung durch alle Panels);
- `series`: Daten (Tabelle), Funktionsgenerator (Art + Parameter), Skizze (Rohpunkte + erkannte Art), je mit Linie,
  Marker, Stufe/Glättung, Beschriftung am Kurvenende, Bezugszeichen;
- `refs`: Wertlinien (x oder y, Beschriftung, Stil), `regions` (zwischen Kurven, unter einer Kurve, zwischen x- oder
  y-Werten; Füllung, Schraffur, Deckkraft), `notes` (Text, Pfeil, Maßpfeil zwischen zwei Punkten, Bezugszeichen mit
  Bezugslinie);
- Legende, Titel, Zahlenformat, Farben nach Schema (schwarz-weiß für Anmeldungen).

## Stufen

1. **Teile in Core und Engine** (allgemein, jedes Modul) + Diagramme versehen ihre Teile mit Schlüsseln; COM:
   PowerPoint-Formatierung je Teil und Teilauswahl mit Leiste und Kreismenü. Tests: jedes Diagrammteil lässt sich
   ausblenden und formatieren; PowerPoint-Szenario: eine Kurve nativ umfärben, sie bleibt nach dem Neuzeichnen.
2. **Abbildungsmodell**: Panels mit gemeinsamer x-Achse und Phasenlinien, Wertlinien, Bereiche und Schraffur,
   Anmerkungen, Maßpfeile, Bezugszeichen; Tabellen-Dateneditor (Übernahme der Tabellenlogik aus Slidemax `ChartSheet`,
   WinForms-Dialog in COM). Die Beispielfigur des Maintainers wird als Bibliotheksvorlage gebaut und in PowerPoint
   geprüft.
3. **Kurven aus Funktionen und Skizzen**: Funktionsgeneratoren; Skizzenerkennung (vereinfachen, einordnen als Gerade /
   Sprung / Puls / Sägezahn / Dreieck / Sinus / Exponential / Kreis, anpassen, sonst Glättungsspline – Slidemax
   `Smoothed`); Achsenbrüche (Übernahme aus Slidemax `AxisBreakSpans`, `Along/ValueAlong`, Zickzack; erweitert auf XY
   und die x-Achse).
4. **matplotlib-Export** des Abbildungsmodells; **Zusammenlegung mit Mathe** (Koordinatensysteme und Funktionsgraphen
   ins Diagramm-Modul).
5. **Rückwärts aus Bildern**: Übernahme des Slidemax-Scanners (`Import/Scanning`, Balken/Säulen, Kalibrierung über
   Beschriftungen) plus neuer Kurven-Digitalisierer (Farb-/Linienmaske, Spurverfolgung je Spalte, gestrichelt/
   durchgezogen trennen, Achsenkalibrierung wiederverwendet).

Je Stufe: Modell + Darstellung + Anker + Aktionen + Speichern/Laden + Tests + Doku EN/DE + Host-Hinweise; erst COM, dann
Web, dann LibreOffice.

## Parallele Arbeit

Stufe 1 (Core/Engine-API) macht der Koordinator zuerst; danach Agenten mit Dateizuständigkeit: Abbildungsmodell und
Darstellung (`src/InventaSketch.Modules.Charts/Figure*`), Scanner-Übernahme und Digitalisierer (`…/Charts/Scanning`),
Skizzenerkennung und Funktionsgeneratoren (`…/Charts/Curves*`), matplotlib-Export (`…/Charts/Export*`); der Koordinator
verantwortet Core, Engine, Hosts, die Lösung und das Review.
