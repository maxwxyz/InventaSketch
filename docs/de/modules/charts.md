# Modul Diagramme & Kennlinien (`charts`)

Technische Diagramme für Folien: XY-Linien-, Streu- und Stufendiagramme, Säulen-/Balkendiagramme, leere Achsen mit
Gitter, die Drehmoment-Drehzahl-Kennlinie eines Motors, ein Wirkungsgradkennfeld, eine Pumpen-/Ventilatorkennlinie mit
Betriebspunkt, ein Bode-Diagramm und ein Toleranzband sowie die Präsentationsdiagramme Kreis- und Ringdiagramm, Polar-
und Netzdiagramm, Sankey-Diagramm, Gantt-Diagramm, Boxplot und Histogramm. Jedes Diagramm besteht aus nativen Primitiven (Linien,
Freihandformen, Rechtecke, Text), bleibt also mit den Werkzeugen des Hosts bearbeitbar, und ist auch in
Schwarz-Weiß-Ausgabe lesbar.

Jedes Diagramm ist ohne Parameter einfügbar (Quick Sketch) und bringt sinnvolle Beispieldaten mit. Diagramme sind
Box-Objekte, gezeichnet in lokalen Koordinaten innerhalb ihrer Box. Freies Skalieren ändert `Size`, nie die Daten:
Plotfläche, Schrift und Marker folgen der Box. Die Schriftgröße richtet sich nach dem kleineren Skalierungsfaktor
gegenüber der Entwurfsgröße (100 × 65 mm, Bode-Diagramm 100 × 80 mm), nichtproportionales Dehnen verzerrt also keine
Schrift. Diagramme sind Abbildungen, keine Bauteile (`IsPart = false`).

## Objekttypen

| Id | Name (EN / DE) | Hinweise |
|---|---|---|
| `chart.xy` | XY chart / XY-Diagramm | `data`, `style` Linie / Linie mit Markern / Punkte (Streudiagramm) / Stufen, `smooth` (Catmull-Rom-Kurve als kubischer Pfad), `limits` (waagerechte Grenzlinien), lineare oder logarithmische Achsen |
| `chart.bar` | Bar chart / Balkendiagramm | `data` (Zeilen `Bezeichnung;Wert[;Wert…]`), `orient` Säulen (senkrecht) / Balken (waagerecht), `mode` gruppiert / gestapelt, `values` (Wertbeschriftung), `patterns` |
| `chart.axes` | Axes and grid / Achsen und Gitter | ohne Daten; `xrange` / `yrange` (Vorgabe `0;10`), lineare oder logarithmische Skalen, `arrows` (Pfeilspitzen an den Achsen) |
| `chart.torque` | Torque–speed curve / Drehmoment-Drehzahl-Kennlinie | `tmax` (Nm), `nbase` (Eckdrehzahl, 1/min), `nmax` (1/min); konstantes Moment bis `nbase`, darüber konstante Leistung; `power` (Leistungskurve auf zweiter Achse in kW), `regions` (Eckdrehzahllinie und Bereichshinweise) |
| `chart.efficiency` | Efficiency map / Wirkungsgradkennfeld | `tmax`, `nbase`, `nmax` (Volllastkurve), `etamax` (Bestwirkungsgrad, %), `levels` (Höhenlinien in %, Vorgabe `80;85;88;90;92;94;95`), `labels` |
| `chart.pump` | Pump / fan curve / Pumpen-/Ventilatorkennlinie | `kind` Pumpe / Ventilator, `h0` (Nullförderhöhe bzw. Nulldruck), `qmax`, `hstat` (statischer Anteil der Anlage), `qop` (Förderstrom im Betriebspunkt), `system` (Anlagenkennlinie) |
| `chart.bode` | Bode plot / Bode-Diagramm | `system` Tief- / Hochpass 1. oder 2. Ordnung, `fc` (Hz), `zeta` (Dämpfungsgrad, 2. Ordnung), `gain` (dB), `show` Betrag und Phase / Betrag / Phase, `marker` (Linie bei der Eckfrequenz), `xrange`, `data` (Messdaten `f;dB;°`, ersetzen das System) |
| `chart.band` | Tolerance band / Toleranzband | `data` (Sollkurve, siehe unten), `tol` + `tolmode` Prozent / absolut, `smooth`, `limits`, `patterns`; weitere Datenblöcke werden als Messreihen mit Markern gezeichnet |
| `chart.figure` | Figure (principle, technical) / Prinzip- und technische Abbildung | `spec` (JSON-Spezifikation, siehe [Bild](#bild-diagramme-v2-stufe-2)); gestapelte Panels, Prinzip- oder Datenachsen, Wertlinien, Markierungen, Flächen, Anmerkungen, Bezugszeichen |
| `chart.linked` | Characteristic chart (linked) / Kennlinie (verknüpft) | `link` (`<Element-Id>#<Satz-Schlüssel>`, erweitert), `title`, `grid`, `legend`; gezeichnet aus dem Kennliniensatz des verknüpften Elements, siehe [Verknüpfte Kennlinien](#verknüpfte-kennlinien-calc-5); nicht in der Bibliothek, wird von einem Element aus eingefügt |
| `chart.math.axes` | Coordinate system (2D) / Koordinatensystem (2D) | Mathematikstil, siehe [Koordinatensysteme und Funktionsgraphen](#koordinatensysteme-und-funktionsgraphen-diagramme-v2-stufe-4) |
| `chart.math.function` | Function graph / Funktionsgraph | Grundfunktion, eingegebene Formel oder Funktionsgenerator im Koordinatensystem im Mathematikstil |
| `chart.timing` | Timing diagram (digital) / Zeitdiagramm (digital) | `signals` (ein Signal je Zeile, siehe [Digitales Zeitdiagramm](#digitales-zeitdiagramm-charttiming)), `slope` schräg / senkrecht, `grid`, `timelabels`, `markers`, `spans`, `title`, `dt`, `tunit` |
| `chart.pie` | Pie chart / Kreisdiagramm | `data` (Zeilen `Bezeichnung;Wert`), `hole` (Ring: Innen-/Außenradius, 0–0,9), `labels` Prozent / Name / Name und Prozent / Wert / keine, `start` (° im Uhrzeigersinn ab 12 Uhr), `direction` im / gegen den Uhrzeigersinn, `center` (Text im Loch), `legend`, `patterns`; siehe [Präsentationsdiagramme](#präsentationsdiagramme-2026-11-26) |
| `chart.polar` | Polar chart / Polardiagramm | `data` (Zeilen `Winkel in °;r`, XY-Format), `style` Linie / Linie mit Markern / gefüllte Fläche / Punkte, `closed`, `zero` rechts gegen den Uhrzeigersinn / oben im Uhrzeigersinn, `rrange`, `grid`, `legend` |
| `chart.sankey` | Sankey diagram / Sankey-Diagramm | `data` (Zeilen `Quelle;Ziel;Wert`), `values` (Werte an den Knoten), `patterns` |
| `chart.gantt` | Gantt chart / Gantt-Diagramm | `data` (Zeilen `Vorgang;Start;Dauer`, ohne Dauer = Meilenstein, Blöcke = Gruppen), `today` (Heute-Linie), `xlabel`/`xunit`, `xrange`, `grid`, `legend` |
| `chart.box` | Box plot / Boxplot | `data` (Zeilen `Kategorie;Wert;Wert…`), `whis` (Whisker × IQR, 1,5), `outliers`, `yrange`, `grid`, `legend` |
| `chart.histogram` | Histogram / Histogramm | `data` (Rohwerte, Blöcke = Reihen), `bins` (0 = automatisch, Sturges), `xrange`, `yrange`, `grid`, `legend` |

`chart.xy` nimmt auch Achsenbrüche (`ybreaks`, `xbreaks`, `breakstyle`, siehe [Achsenbrüche](#achsenbrüche-diagramme-v2-stufe-3)).

Gemeinsame Parameter (wo sinnvoll): `title`, `xlabel`, `xunit`, `ylabel`, `yunit` (leer = zweisprachige Vorgabe
des Typs, ausdrücklich leere Beschriftung blendet den Achsentitel aus), `xscale` / `yscale` linear / logarithmisch,
`xrange` / `yrange` (`min;max`, leer = automatisch, erweitert), `grid`, `legend` automatisch / innen / rechts / keine,
`box` (geschlossener Rahmen). Achsentitel lauten im Deutschen „Größe in Einheit“ (nach DIN 461), im Englischen
„label (unit)“, z. B. „Drehzahl n in 1/min“ / „Speed n (rpm)“. Teilstrichbeschriftungen verwenden im Deutschen das
Dezimalkomma.

Das Wirkungsgradkennfeld beruht auf einem qualitativen Verlustmodell (konstante Verluste, Kupfer ∝ M², Eisen ∝ n,
Luftreibung und Wirbelströme ∝ n³), so skaliert, dass sein Maximum `etamax` beträgt. Es zeigt die typische Inselform
für Skizzen und ist keine Messung. Die Pumpenkennlinie ist H(Q) = H₀ (1 − (Q/Q_max)²); die Anlagenkennlinie verläuft
durch den statischen Anteil und den Betriebspunkt. Das Bode-Diagramm wird aus den Standard-Übertragungsfunktionen von
Tief- und Hochpässen 1. und 2. Ordnung berechnet.

Die Bibliothek enthält 41 Vorlagen (`chart.line`, `chart.line.markers`, `chart.line.multi`, `chart.line.smooth`,
`chart.line.logx`, `chart.scatter`, `chart.step`, `chart.column`, `chart.column.grouped`, `chart.column.stacked`,
`chart.bar.horizontal`, `chart.axes`, `chart.axes.plain`, `chart.axes.loglog`, `chart.line.break`, `chart.limits`, `chart.torque`,
`chart.torque.plain`, `chart.efficiency`, `chart.pump`, `chart.fan`, `chart.bode`, `chart.bode.lowpass1`,
`chart.bode.highpass1`, `chart.band`, `chart.band.measured`, `chart.figure.phases`, `chart.figure.timing`, `chart.figure.temperature`,
`chart.timing`, `chart.timing.clock`, `chart.timing.bus`, `chart.timing.handshake`, die acht unten genannten Koordinatensysteme und Funktionsgraphen
und die neun Präsentationsdiagramme `chart.pie`, `chart.pie.names`, `chart.donut`, `chart.polar`, `chart.polar.radar`,
`chart.sankey`, `chart.gantt`, `chart.box`, `chart.histogram`; zusammen 51) in vier Kategorien:
Datendiagramme, Koordinatensysteme & Funktionsgraphen, Kennlinien & Kennfelder sowie Prinzip- und technische
Abbildungen.

## Datenformat

Daten sind ein Textparameter (`data`), im Inspektor eingetippt oder aus einer Tabellenkalkulation eingefügt:

- eine Datenzeile je Zeile; in der einzeiligen Eingabe *Daten…* trennt `|` die Zeilen und `||` die Reihen;
- Felder werden durch **Tabulator** (Einfügen aus Excel), **Semikolon**, **Leerzeichen** oder **Komma** getrennt
  (Komma nur, wenn kein anderes Trennzeichen vorkommt: `1,5` ist x = 1, y = 5; `1, 5` mit Leerzeichen ebenso);
- Zahlen in invarianter Schreibweise (`2.5`) oder mit **deutschem Dezimalkomma** (`2,5`), wenn die Felder durch
  Tabulator, Semikolon oder Leerzeichen getrennt sind; Tausendertrennzeichen (`1.234,5`, `1,234.5`), Unicode-Minus,
  ein nachgestelltes `%` und Exponenten (`1e3`) werden akzeptiert;
- eine **Leerzeile** beginnt eine neue Reihe; eine reine Textzeile benennt die nächste Reihe (ein Feld) oder die
  Spalten (mehrere Felder, z. B. eine Excel-Kopfzeile `x;Messung;Modell`); ein Feld in Anführungszeichen ist immer
  Text (`"2025"`);
- Zeilen, die mit `#` oder `//` beginnen, sind Kommentare; leere oder ungültige Zellen (`-`, `n/a`) werden
  übersprungen; das Einlesen schlägt nie fehl.

Bedeutung der Spalten:

| Diagramm | Zeilen |
|---|---|
| XY-Diagramm | `x;y` (eine Reihe), `x;y1;y2;…` (eine Reihe je Spalte, Namen aus der Kopfzeile) oder ein Wert je Zeile (x = 1, 2, 3 …) |
| Balkendiagramm | `Bezeichnung;w1;w2;…` (eine Reihe je Wertspalte); eine numerische erste Spalte dient als Bezeichnung, wenn weitere Werte folgen (Jahreszahlen); mehrere Blöcke sind mehrere Reihen, zugeordnet über die Bezeichnung |
| Toleranzband | `x;y` (Band aus `tol` in % von y oder absolut), `x;y;±t` (symmetrisch), `x;y;unten;oben`; weitere Blöcke sind Messreihen `x;y` |
| Bode-Diagramm (`data`) | `f;dB;°` (Phase optional) |
| Kreis- / Ringdiagramm | `Bezeichnung;Wert` (erste Wertespalte; Null, negative und leere Werte ergeben kein Segment) |
| Polardiagramm | wie das XY-Diagramm, x = Winkel in Grad, y = Radius |
| Sankey-Diagramm | `Quelle;Ziel;Wert` (ein Fluss je Zeile; Zeilen ohne positiven Wert werden übersprungen) |
| Gantt-Diagramm | `Vorgang;Start;Dauer` (eine Zeile ohne Dauer ist ein Meilenstein); eine Leerzeile oder Kopfzeile beginnt eine Gruppe |
| Boxplot | `Kategorie;Wert;Wert…`, oder eine Spalte je Kategorie unter einer Kopfzeile, oder ein Block je Kategorie |
| Histogramm | beliebige Zahlen (jeder Wert eines Blocks ist ein Messwert); mehrere Blöcke sind mehrere Reihen |

Listen (`levels`, `limits`) verwenden dieselben Trennzeichen; ein einzelner Wert mit Dezimalkomma (`92,5`) ist eine
Zahl. Bereiche (`xrange`, `yrange`) akzeptieren `0;100`, `0..100`, `0,5;2,5` oder `10 1000`; alles andere bedeutet
automatisch.

### Tabelle importieren (25.11.2026)

„Importieren …“ (COM: Gruppe Prüfen; Web: Import-Knopf in der oberen Leiste; Engine `importData`) liest eine
CSV/TSV-Datei in das ausgewählte Diagramm oder in ein neues XY-Diagramm in der Folienmitte: Das Trennzeichen (Tab, „;“
oder „,“) wird erkannt, Felder in Anführungszeichen behalten ihre Trennzeichen, ein Dezimalkomma wird zum Punkt
(deutsches Excel), leere Zeilen entfallen (`Imports.CsvToChartData`). In PowerPoint geprüft (Szenario `import`).

## Achsen und Teilung

Lineare Achsen wählen „runde“ Schrittweiten (1, 2, 2,5, 5 × 10ⁿ) für etwa fünf Intervalle und erweitern automatische
Bereiche bis zum nächsten Schritt (Balkendiagramme, Drehmoment- und Pumpenkennlinien schließen immer 0 ein).
Logarithmische Achsen laufen von Dekade zu Dekade, haben Hauptteilstriche auf den Dekaden und Nebenteilstriche bei
2 … 9 × 10ⁿ, beschriften 2 und 5, wenn die Achse höchstens zwei Dekaden umfasst, und dünnen Dekadenbeschriftungen ab
acht Dekaden aus; Werte ab 10 000 erhalten SI-Präfixe (`10k`, `1M`), Werte unter 0,001 Exponentenschreibweise. Werte
≤ 0 werden auf logarithmischen Achsen übersprungen. Die Phasenachse des Bode-Diagramms nutzt Schritte von 5, 10, 15,
30, 45, 90 oder 180°. Überlappende Beschriftungen werden ausgedünnt; Kategoriebeschriftungen teilen sich bei schmalen
Bändern eine verkleinerte Schrift. Daten außerhalb eines festen Bereichs werden an der Plotfläche abgeschnitten
(Linien, geglättete Kurven, Bänder).

## Schwarz-Weiß

Reihen nutzen eine feste Folge dunkler Farben (Leuchtdichte unter 0,35, sie werden schwarz gedruckt),
Strichmuster (durchgezogen, gestrichelt, gepunktet, Strich-Punkt, lang gestrichelt, Strich-Punkt-Punkt) und Marker
(gefüllter Kreis, hohles Quadrat, gefülltes Dreieck, hohle Raute, Kreuz, Plus) und bleiben so ohne Farbe
unterscheidbar. Balken und das Toleranzband wechseln zu Füllmustern (schwarz, weiß, Schraffur 45°, Schraffur 135°,
Kreuzschraffur, Punkte; Band: Schraffur), wenn Dokument oder Renderkontext schwarz-weiß sind, oder immer mit
`patterns`. Das Gitter wird in Schwarz-Weiß gepunktet. Grenzlinien verwenden die Linienart Mittellinie
(Strich-Punkt), Hilfslinien (Eckdrehzahl, Lote auf die Achsen) die Hilfslinienart.

## Legende

`legend = automatisch` zeigt eine Legende in der Plotfläche, sobald es zwei oder mehr Einträge gibt (Moment und
Leistung, Pumpen- und Anlagenkennlinie, Sollwert und Band, mehrere Reihen). Die innere Legende kommt in die Ecke der
Plotfläche, die die wenigsten Datenpunkte und Hinweise verdeckt; `rechts` setzt sie neben die Plotfläche. Einträge
zeigen die Reihenlinie mit Strichmuster und Marker bzw. ein Flächenmuster für Balken und Bänder. Reihen ohne Namen
heißen „Reihe 1“, „Reihe 2“ … („Series 1“ … im Englischen).

## Fangen und Andocken

Alle Anker liegen in lokalen Koordinaten und stammen aus demselben Layout wie die Zeichnung; sie folgen also dem
freien Skalieren:

- Ecken der Plotfläche `plot.tl`, `plot.tr`, `plot.br`, `plot.bl` (Rolle Ecke) und ihre Mitte `plot.c` (Rolle
  Mitte), dazu die allgemeinen Box-Anker;
- `origin` (Port): der Punkt (0, 0), wenn er in der Plotfläche liegt, sonst die Achsenecke; die Achsen als Schienen
  `axis.x`, `axis.y` (`axis.y2` für die Leistungsachse), sodass Bezugslinien und Pfeile überall an einer Achse
  andocken. Felder unterhalb des ersten (Bode-Phase) tragen die Endung `2` (`origin2`, `axis.x2` …);
- Datenpunkte als Ports: `s<Reihe>.<Punkt>` (XY, Balkenenden, Soll- und Messpunkte des Toleranzbands; höchstens 160 je
  Diagramm, nur innerhalb der Plotfläche), dazu `upper` / `lower` (Band), `tmax`, `base`, `end`, `pmax`
  (Drehmomentkennlinie), `peak`, `base`, `end` (Wirkungsgradkennfeld), `op`, `h0`, `qmax`, `hstat`
  (Pumpenkennlinie), `fc`, `fc.phase` (Bode-Diagramm).

## Kontextaktionen

| Typ | Aktionen |
|---|---|
| XY-Diagramm | „Daten…“ (Eingabe), Darstellung ▸ (Linie, Linie mit Markern, Punkte, Stufen), geglättete Kurve, x-Skala ▸, y-Skala ▸, Gitternetz, Legende ▸, Fläche hinzufügen ▸ (unter Kurve 1, zwischen zwei Kurven, senkrechter Bereich, waagerechtes Band, zwischen den Grenzlinien) |
| Balkendiagramm | „Daten…“, Ausrichtung ▸, mehrere Reihen ▸ (gruppiert / gestapelt), Wertbeschriftung, Gitternetz, Legende ▸ |
| Achsen | Gitternetz, Pfeilspitzen, x-Skala ▸, y-Skala ▸ |
| Drehmomentkennlinie | Leistungskurve, Bereichshinweise, Gitternetz, Legende ▸ |
| Wirkungsgradkennfeld | „Höhenlinien…“ (Eingabe), Beschriftung der Linien, Gitternetz |
| Pumpen-/Ventilatorkennlinie | Maschine ▸ (Pumpe / Ventilator), „Förderstrom im Betriebspunkt…“ (Eingabe), Anlagenkennlinie, Gitternetz, Legende ▸ |
| Bode-Diagramm | System ▸, „Eckfrequenz…“ (Eingabe: `1 kHz`, `250 Hz`, `2.5k`, `2,5k`), Anzeigen ▸, Eckfrequenz markieren, Gitternetz |
| Toleranzband | „Daten…“, „Toleranz…“ (`10 %` = Prozent, `0,5` = absolut), geglättete Kurven, Gitternetz, Legende ▸ |
| Kreis- / Ringdiagramm | „Daten…“, Ring (Schalter), „Lochgröße…“ (`30 %`), „Startwinkel…“, „Text in der Mitte…“, Segmentbeschriftung ▸, Richtung ▸, Legende ▸ |
| Polardiagramm | „Daten…“, Darstellung ▸, geschlossene Kurven, Richtung von 0° ▸, „Radialbereich…“, Gitternetz, Legende ▸ |
| Sankey-Diagramm | „Daten…“ (Flusszeilen), Fluss hinzufügen, letzten Fluss entfernen, Werte an den Knoten |
| Gantt-Diagramm | „Daten…“, Vorgang hinzufügen, letzten Vorgang entfernen, „Heute-Linie…“ (leer = keine), Gitternetz, Legende ▸ |
| Boxplot | „Daten…“, „Whiskerlänge…“, Ausreißer, Gitternetz, Legende ▸ |
| Histogramm | „Daten…“, „Klassen…“ (`auto` oder eine Zahl), mehr Klassen, weniger Klassen, Gitternetz, Legende ▸ |

Ungültige Eingaben werfen eine `FormatException` mit zweisprachiger Meldung und ändern nichts. Schalter verwenden
`IconIds.Toggle`, Eingaben `IconIds.Text`. Die Diagrammtypen haben keine Ansichtsumschaltung.

**Auswahlen sind Klapplisten (25.11.2026).** Jede Auswahl unter mehreren Möglichkeiten (Darstellung, Skala, Lage der
Legende, Ausrichtung, Maschine, Funktion, Flanken, Kurvenform, Ausgleichsfunktion, Glättung, Detailgrad, Strichart, Marker,
Füllmuster, Farbe, Deckkraft, Rand, Wo, Stufen) ist eine Aktion je Möglichkeit (`ActionChoices.Option`, Id
`<Auswahl>=<Wert>`, z. B. `chart.style=step`, `chart.curve.shape=fitted`), alle mit derselben `ActionChoice` (Schlüssel
und Bezeichnung), die aktuelle aktiv markiert. Die Hosts zeigen sie als eine Klappliste („Kurvenform: Geglättet ▾“) und
im Kreismenü als „Kurvenform: Geglättet ▸“, das die Möglichkeiten öffnet – nie als Durchschalt-Knopf. Möglichkeiten
ohne passendes gemeinsames Piktogramm tragen eine kleine Zeichnung (`ContextAction.Preview`: die Ausgleichsfunktion,
die Glättung, das Füllmuster, die Farbe, der Marker), die die Hosts als Bild der Möglichkeit zeigen. Die Kurvenformen
haben eigene Piktogramme (`IconIds.CurveSmoothed`, `CurveSpline`, `CurveFitted`, `CurveSegments`, `CurveRaw`), weil
eine Zeichnung der aktuellen Kurve in den meisten Formen gleich aussieht. Auswahlen aus Befehlen
(`ActionChoice.IsMenu`: „Darunter füllen ▸“, „Füllen bis ▸“, „Fläche hinzufügen ▸“) markieren nichts. Die Engine
liefert zu jeder Aktion `choice`, `choiceLabel`, `choiceMenu` und `previewSvg`.

## Teile (Diagramme v2, 25.11.2026)

Jedes gezeichnete Stück eines Diagramms ist ein **Teil** mit Schlüssel – dem Tag des Primitivs: `series.0`, `series.1`
(Kurven), `marker.0` (Marker der Kurve 1), `bar.0` (Balken der Reihe 1), `value.0` (Wertbeschriftungen), `axis.x`,
`axis.y`, `axis.y2` (rechte y-Achse), `axis.box` (geschlossener Rahmen), `tick.x`, `tick.y`, `label.x`, `label.y`
(Skalenbeschriftung), `axistitle.x`, `axistitle.y`, `grid.major`, `grid.minor`, `zero`, `limit`, `legend`, `title`,
`band`, `contour`, `contourlabel`, `envelope`, `helper`, `point`, `note`. Übereinanderliegende Panels (Bode-Diagramm)
hängen die Panelnummer an: `axis.y.2`, `label.y.2`.

- Jedes Teil lässt sich einzeln ausblenden und formatieren (`SketchObject.Parts`: je Schlüssel ein Aussehen mit Stil
  oder ausgeblendet). Ein Aussehen für die Art (`series`, `grid`) gilt für alle Teile dieser Art; der Schlüssel mit
  Index gewinnt.
- Teilaktionen: die Diagrammaktionen, die das Teil betreffen (Kurve: Daten, Darstellung, Glättung, Formauswahlen
  skizzierter Kurven, darunter füllen ▸, füllen bis ▸, Kurve löschen; Datenpunkt: löschen, Punkt danach einfügen,
  Wert …; Fläche: Muster, Farbe, Deckkraft, Rand, Wo, Stufen, Legendentext, löschen; Achse: Skala; Gitter: Gitter
  ein/aus; Legende: Lage), danach ein-/ausblenden, dicker, dünner, Strichart (eine Auswahl) und zurücksetzen. Die
  Entf-Taste mit ausgewähltem Teil führt dessen `part.delete` aus (Punkt, Kurve, Fläche, Wertlinie, Anmerkung), sonst
  blendet sie das Teil aus – nie löscht sie das ganze Diagramm.
- PowerPoint: Ein Klick ohne Ziehen auf das ausgewählte Diagramm wählt das Teil unter dem Mauszeiger (blau umrandet);
  die Leiste zeigt seinen Namen und seine Aktionen, „Teile …“ listet alle Teile, auch ausgeblendete, „Ganzes Element“
  verlässt das Teil. Eine Kurve mit PowerPoints eigenen Werkzeugen formatieren (Linienfarbe, Stärke, Füllung, Schrift)
  ändert nur dieses Teil und bleibt bei jedem Neuzeichnen erhalten; das ganze Diagramm formatieren ändert wie bisher
  den Elementstil.
- Engine: `parts`, `partAt`, `partActions`, `applyPartAction`, `setPartLook` (siehe Entwurf
  [Diagramme v2](../design/charts-v2.md)).

## Datenpunkte und Kurven auf der Zeichnung (25.11.2026)

XY-Diagramme und Bilder (Maintainer: „die Werte/Punkte jeder Kurve auswählen, blau hervorgehoben; Klicken und Ziehen
auf einer Kurve verschiebt sie, die Werte werden im Hintergrund angepasst; einzelne Punkte per Klicken und Ziehen
verschieben oder löschen; einzelne Kurven löschen“):

- Jeder Datenpunkt ist ein Teil `point.<Kurve>.<Index>` („Punkt 3 von Reihe 1 (2; 4,1)“), ein feines Teil
  (`PartInfo.Detail`: nicht in der Teileliste und der Tab-Reihenfolge). Seine Teilform (`ObjectType.PartShapes`, ein
  kleiner Kreis, nie gezeichnet) dient dem Treffertest (`Registry.PartAt`: ein Punkt gewinnt, wenn der Zeiger auf ihm
  liegt), der Umrandung und dem Leuchten (`Registry.PartPrimitives`).
- Ziehgriffe (`DragHandles`): je Punkt ein freier Griff, gezeigt mit seiner Kurve (`DragHandle.ShowWith = "series.k"`);
  der Wert rastet auf ein Zehntel des Achsschritts (log. Achsen: zwei gültige Stellen) und bleibt im Achsbereich; auf
  einer Kurve, deren x nie fällt, bleibt der Punkt zwischen seinen Nachbarn. Kurven mit mehr als 64 Punkten (dichte
  Daten) erhalten keine Punktgriffe – ihre Punkte bleiben auf der Zeichnung auswählbar und mit „Wert …“ änderbar;
  skizzierte Kurven speichern nur ihre Stützpunkte (höchstens 64) und erhalten daher immer Griffe. Je Kurve ein
  Griff (`series.k`, `DragHandle.ByPart`) wird gezogen, indem man auf die ausgewählte Kurve drückt: jeder Punkt wandert
  um den gerasteten Versatz (log. Achsen: um einen Faktor); bei deutlich senkrechtem (waagerechtem) Ziehen zählt nur
  diese Richtung.
- Aktionen: Punkt – löschen, Punkt danach einfügen (halbwegs zum nächsten), „Wert …“ (`x; y`); Kurve – Kurve löschen.
  Eine gelöschte Kurve nimmt ihre Flächen und Punktmarker mit, Indizes und Teil-Aussehen späterer Kurven rücken nach.
  Ein geänderter Punkt einer skizzierten Kurve macht sie zu reinen Daten (ihr Form-Memo entfällt); die anderen Kurven
  behalten ihres. Die Daten des XY-Diagramms werden dann als ein Block je Kurve geschrieben (Kopfzeile „Name;“, Zeilen
  `x;y`); eine als Spline gezeichnete Kurve behält dieses Aussehen über eine Zeile `# spline` vor ihrem Block.
- PowerPoint: Die Punktgriffe sind kleine blau umrandete Punkte (der ausgewählte Punkt blau gefüllt mit sanftem
  Leuchten); sie erscheinen für die ausgewählte Kurve oder den ausgewählten Punkt, die anderen nahe am Zeiger. Ein Klick
  auf einen Griff wählt den Punkt; Ziehen verschiebt ihn (Live-Vorschau, ein Rückgängig-Schritt, Esc bricht ab). Mit
  ausgewählter Kurve verschiebt Drücken irgendwo auf ihr und Ziehen die Kurve. Entf löscht den ausgewählten Punkt (die
  Kurve bleibt ausgewählt), die Kurve oder die Fläche.

## Skizzierte Kurven: Stützpunkte, Detailgrad und Zeichengeschwindigkeit (25.11.2026)

Maintainer: „Beim Zeichnen einer Kurve werden manchmal zu viele Punkte gesetzt – vielleicht einstellbar, oder abhängig
davon, wie schnell oder welche Kurve man zeichnet?“ Eine skizzierte Kurve (`ChartSketchInput`, `CurveShapes`,
`CurveKeyPoints`) speichert nur die Punkte, die ihre Form tragen – jeder gespeicherte Punkt ist eine Datenzeile, ein
Punkt-Teil und ein Griff – statt 96–120 dichter Abtastpunkte:

- Die Zeigerspur wird einmal bereinigt und mit der Kurve aufbewahrt (`CurveSketchMemo`: im XY-Diagramm der Kommentar
  `# sketch …` vor dem Block, in Abbildungen das Reihen-Feld `sketch`), sodass jede der folgenden Auswahlen verlustfrei
  daraus neu berechnet wird.
- Stützpunkte je Kurvenform, alle Toleranzen relativ zum Plot (1 = Plotgröße), beim Standard-Detailgrad:
  - **Geglättet**: die geglättete Spur; Stützpunkte so, dass der Diagramm-Spline durch sie höchstens 0,6 % abweicht.
  - **Interpoliert (Spline)**: die leicht gemittelte Spur; Stützpunkte so, dass der Spline höchstens 1,5 % abweicht.
  - **Ausgleichsfunktion**: stückweise lineare Funktionen (konstant, Rampe, Sprung, Puls, Sägezahn, Dreieck) speichern
    ihre exakten Ecken; glatte (Sinus, Ellipse) Stützpunkte für den Spline innerhalb 0,5 %; PT1, PT2 und Abfall mit
    Startecke im gezeichneten Bereich werden als gerade Abschnitte innerhalb 0,5 % gespeichert (ein Spline würde die
    Ecke abrunden).
  - **Gerade Abschnitte**: Ramer–Douglas–Peucker innerhalb 1,5 % der leicht gemittelten Spur.
  - **Wie gezeichnet**: die Spur, mit Ramer–Douglas–Peucker innerhalb 0,2 % ausgedünnt (das Zittern bleibt sichtbar).
- Spline-Stützpunkte (`CurveKeyPoints.Spline`): feste Punkte an den Enden, an Umkehrpunkten von x und y (Zickzack mit
  einer Hysterese von 4 Toleranzen), an Ecken (≥ 50° innerhalb ±2 % Bogenlänge) und an der steilsten Stelle zwischen
  Gipfel und Tal (Wendepunkt); dazwischen so wenige Punkte wie möglich, gleichmäßig über die Krümmung verteilt
  (√|Krümmung| plus kleine Grundlast), ihre Zahl wird erhöht, bis der Spline durch alle Stützpunkte in beiden Richtungen
  im Toleranzband bleibt (Überschwinger zählen); Punkte, ohne die das Band hält, entfallen. Eine Gerade behält 2 Punkte,
  ein einzelner Bogen etwa 5–7, ein Sinus über zwei Perioden etwa 15–20, ein Gekritzel höchstens die Grenze des
  Detailgrads.
- Zeichnen: geglättete, interpolierte und glatte angenäherte Kurven werden mit dem Diagramm-Spline gezeichnet
  (gleichmäßiges Catmull–Rom, `Draw.Spline`; affin invariant, daher gilt die Prüfung in Ploteinheiten auch auf der
  Zeichnung). XY-Diagramm: je Block, markiert durch sein Memo (`CurveShapes.IsSpline`) oder eine Zeile `# spline`
  (bleibt beim Ändern der Punkte von Hand und beim Bereinigen der Folie erhalten; `ChartSketchInput.XySplines`), auch im
  matplotlib-/pgfplots-Export; Abbildung: das Feld `smooth` der Reihe.
- **Detailgrad** (Teil-Auswahl neben „Kurvenform“ und „Glättung“, eine Klappliste mit Vorschau der behaltenen Punkte):
  Gering (Toleranzen × 2, höchstens 24 Punkte), Mittel (Standard, adaptiv; höchstens 40), Hoch (Toleranzen × ½,
  höchstens 64). Ein geringerer Detailgrad behält nie mehr Punkte als ein höherer. Nicht angeboten für exakte stückweise
  lineare Ausgleichsfunktionen. Gespeichert als `detail=low` / `"detail": "high"` (Mittel entfällt).
- **Zeichengeschwindigkeit** als Hinweis: der Host gibt je Spurpunkt einen Zeitstempel (COM: ms seit dem Drücken; Engine
  `sketchCurve` nimmt `[x, y, t]`). Regel: die Geschwindigkeit an einem Punkt wird über mindestens 120 ms um ihn in
  Plotgrößen je Sekunde gemessen; ihr Toleranzfaktor ist √(Geschwindigkeit / 1 Plot je Sekunde), begrenzt auf
  0,6 … 1,5. Langsames, sorgfältiges Zeichnen (≤ 0,36 Plots/s) verschärft die Toleranzen auf 0,6 ×, ein schneller Strich
  (≥ 2,25 Plots/s) lockert sie auf 1,5 ×; ein Strich von einer Plotbreite je Sekunde ist neutral. Die Faktoren werden je
  behaltenem Spurpunkt in Stufen von 0,1 gespeichert (`pace=4456…`, eine Ziffer je Punkt, 0 = 0,6 … 9 = 1,5; ohne
  Zeitstempel entfällt das Feld), das Ergebnis ist also deterministisch.
- Alte skizzierte Daten (dichte Zeilen, Memo ohne Detailgrad und Tempo) laden unverändert und werden bei glatter Form als
  Spline gezeichnet; jede Auswahl berechnet sie als Stützpunkte neu.

## Flächen (25.11.2026)

Gefüllte Flächen wie in matplotlib, für XY-Diagramme (Parameter `areas`: ein JSON-Array, leer = keine; alte Dokumente
unverändert) und Bilder (die `regions` der Spezifikation, dieselben Felder; Teilschlüssel `area.k` im XY-Diagramm,
`region.k` im Bild, für bestehende Dokumente beibehalten):

| Art | matplotlib | Felder |
|---|---|---|
| `below` | `fill_between(x, y)` bis zur Achse | `a`, `base` (ein y-Wert statt des Panelbodens) |
| `between` | `fill_between(x, y1, y2)` | `a`, `b` (Kurve) oder `y2` (waagerechte Linie, z. B. eine Grenzlinie) |
| `betweenx` | `fill_betweenx(y, x1, x2)` | `a`, `b` (Kurve) oder `x2` (senkrechte Linie) |
| `span` | `axvspan(x0, x1)` | `from`, `to`, `panel` (-1 = alle) |
| `hspan` | `axhspan(y0, y1)`; ein Band zwischen zwei Wertlinien | `y1`, `y2`, wahlweise `from`, `to` (x-Bereich), `panel` |

- `where`: `above` (Kurve A über – rechts von – dem anderen Rand) oder `below`; Schnittpunkte werden exakt berechnet
  (auch wo sich zwei Kurven in einem Datenpunkt treffen); das frühere `"above": true` wird weiter gelesen und geschrieben.
- `step`: `pre`, `post`, `mid` – die Ränder folgen Stufen (`fill_between(step=)`).
- Aussehen: `hatch` – ein matplotlib-Schraffurmuster (`/`, `\`, `|`, `-`, `+`, `x`, `o`, `O`, `.`, `*`, wiederholt für
  größere Dichte, kombinierbar: `//`, `xx`, `/o`); `fill` `solid` (allein: eine getönte Fläche, Deckkraft 0,3 als
  Vorgabe; mit Muster: eine helle Tönung darunter) oder `none`; `color` (Vorgabe: Farbe der Kurve A, grau für Bereiche),
  `opacity`, `edge` (Strichart des Umrisses) und `edgeColor`, `label` (Legendeneintrag). Ohne `hatch` gelten die
  früheren Füllungen (`hatch`, `cross`, `solid`, `dots` mit `angle` und `spacing`). Werte, die Flächen erreichen (eine
  Basis, eine Linie, ein Band, ein Bereich), liegen wie in matplotlib in den automatischen Achsbereichen.
- Zeichnung (nativ zuerst): die Fläche ist eine geschlossene Freihandform; eine Linienschar des Musters (oder ein
  kreuzendes Paar) ist ihre Schraffurfüllung, die PowerPoint als Schraffurlinien erhält (der COM-Zeichenplan zeichnet
  Schraffuren explizit), weitere Scharen sind explizite Schraffurlinien, Kreise und Sterne sind kleine geschlossene
  Formen innerhalb der Fläche (nie über den Rand) – alle Linien und Formen bleiben in PowerPoint bearbeitbar, kein Bild.
  Ein Zeichen hat 2,4 mm Abstand bei der Entwurfsgröße (es folgt dem Maßstab des Diagramms), „//“ 1,2 mm.
- Anlegen: auf einer Kurve „Darunter füllen ▸“ (eine Möglichkeit je Muster, jede mit Musterfeld) und „Füllen bis ▸“
  (eine andere Kurve ihres Panels, eine Wert- oder Grenzlinie, ein y-Wert …, eine senkrechte Linie x = … für
  fill_betweenx); am Diagramm „Fläche hinzufügen ▸“. Jede Fläche ist ein Teil mit den Auswahlen Muster ▾ (Musterfelder),
  Farbe ▾, Deckkraft ▾, Rand ▾, Wo ▾ und Ränder entlang der Kurve ▾ (Stufen), dem Schalter „Getönter Hintergrund“
  (Muster), „Legendentext …“ und Löschen.
- Exporte: matplotlib `fill_between(..., where=[a >= b …], interpolate=True, step=…, hatch=…, facecolor=…,
  edgecolor=…, linestyle=…, label=…)`, `fill_betweenx`, `axvspan`, `axhspan` (ein Band über einen Teil von x als
  `fill_between`); pgfplots `fill between` für einfache Flächen (Muster `Lines`, `Hatch`, `Dots`, `Stars` aus
  `patterns.meta`, eine Tönung als `postaction`), die gefüllten Polygone für Stufen, fill_betweenx und where-Bedingungen,
  Rechtecke für Bänder; TikZ und SVG aus der Zeichnung.
- Import (matplotlib): `fill_between` mit `hatch`, `where` (Kurve über oder unter dem anderen Rand oder einer
  Konstanten), `step`, `label`, `facecolor`/`edgecolor`/`linewidth`/`linestyle`; `fill_betweenx`; `axhspan` (über die
  ganze Breite); `axvspan` mit seinem Legendeneintrag.

## Achsenbrüche (Diagramme v2, Stufe 3)

Das XY-Diagramm (`chart.xy`), das Bild (`chart.figure`) sowie Koordinatensysteme und Funktionsgraphen im
Mathematikstil können Wertebereiche einer linearen Achse ausblenden (Achsenbruch), so passen weit auseinanderliegende
Daten in ein Diagramm (z. B. 10–15 und 95–100 auf einer y-Achse).

- Parameter: `ybreaks` / `xbreaks` (erweitert): ausgeblendete Bereiche als Paare „von-bis“, getrennt durch „;“, z. B.
  `20-80` oder `-50--20; 120-180` (auch `von..bis`, `von–bis`, Dezimalkomma erlaubt); `breakstyle` Zickzack (Vorgabe:
  ein doppeltes Zickzackband über das ganze Diagramm und die Achslinie) / Schrägstrich (kurzer doppelter Schrägstrich
  an der Achslinie und an der Gegenseite eines geschlossenen Rahmens). Unlesbare Paare werden beim Zeichnen
  übergangen; logarithmische und Kategorieachsen haben keine Brüche.
- Zeichnen (`Frame`): die Achse bildet über `BrokenAxis` ab (gleicher Maßstab außerhalb der Brüche, jeder Bruch auf
  das Zeichenband von `AxisBreaks.RequiredGap` gestaucht, etwa 3,6 mm); Kurven werden in sichtbare Abschnitte geteilt,
  genau an den Bruchgrenzen geschnitten; Datenpunkte im Bruch erhalten keinen Marker und keinen Anschluss;
  Skalenbeschriftungen, Gitterlinien, Grenzlinien, Wertlinien und Texte im Bruch entfallen; die Teilung richtet sich
  nach dem sichtbaren Bereich. Das Band ist weiß gefüllt, die Achslinie erscheint unterbrochen.
- Bild: je Achse `"breaks": [[20, 80], …]` in `x` und in jedem Panel (auch `{"from": 20, "to": 80}` oder ein Text
  `"20-80; …"`) sowie `"breakStyle": "zigzag" | "slash"` für das Bild. Optionale Angaben der Version 1: ältere Leser
  übergehen sie, fehlerhafte Einträge werden ausgelassen.
- Teile: `axisbreak.y` (gestapelte Panels `axisbreak.y.1`, `axisbreak.y.2` …) und `axisbreak.x`, benannt
  „Achsenbruch y“ / „y axis break“.
- Aktionen: „Achsenbruch hier…“ (`chart.ybreak`, Eingabe „von-bis“, mehrere mit „;“, leer = keiner; vorbelegt mit den
  aktuellen Brüchen oder einem Vorschlag für die größte datenfreie Lücke, wenn sie mehr als 40 % der Achse einnimmt),
  „Achsenbruch x hier…“ (`chart.xbreak`), „Achsenbrüche entfernen“ (`chart.ybreak.remove`, `chart.xbreak.remove`).
  Angeboten am Element, an den Achsenteilen (Achse, Teilstriche, Skalenbeschriftung, Achsentitel dieser Richtung)
  und am Bruchzeichen. Im Bild (`chart.figure.ybreak` …) bearbeitet der Achsenteil eines gestapelten Panels die Brüche
  dieses Panels.
- Anker: Datenanschlüsse und Achsschienen folgen der gebrochenen Abbildung (ein Anschluss liegt dort, wo sein Punkt
  gezeichnet ist).
- Export: matplotlib und pgfplots geben Brüche nicht wieder; die Achsen laufen durch, das Diagramm beginnt mit dem
  Kommentar „Achsenbrüche werden nicht exportiert …“ mit den ausgeblendeten Bereichen.

## Koordinatensysteme und Funktionsgraphen (Diagramme v2, Stufe 4)

Das 2D-Koordinatensystem und die Funktionsgraphen des Mathematikmoduls sind ins Diagrammmodul umgezogen, damit alle
Diagramme ein Achsensystem teilen (`Frame`, mit Achsenbrüchen, Teilen und Export). Kategorie „Koordinatensysteme &
Funktionsgraphen“.

| Id | Name (EN / DE) | Hinweise |
|---|---|---|
| `chart.math.axes` | Coordinate system (2D) / Koordinatensystem (2D) | Mathematikstil: beide Achsen schneiden sich im Ursprung (0 in die Bereiche geklemmt) und enden mit Pfeilspitzen jenseits des Diagramms, Teilstriche quer zu den Achslinien, Zahlen unter der x-Achse und links der y-Achse, eine „0“ im Ursprung, Achsennamen (`xname`, `yname`, kursiv) an den Pfeilspitzen; `xmin`/`xmax`/`ymin`/`ymax`, `xstep`/`ystep`, `grid`, `numbers`, `xpi` (x-Teilung in Vielfachen von π/2, beschriftet π/2, π, 3π/2 …), `title`, Achsenbrüche |
| `chart.math.function` | Function graph / Funktionsgraph | das Koordinatensystem mit einer Kurve: `function` = eine von zehn Grundfunktionen als y = a·f(b(x − c)) + d (linear, quadratisch, kubisch, Sinus, Kosinus, Exponential, ln, Quadratwurzel, Betrag, Kehrwert; `a`–`d`), eine eingegebene Formel (`expression`, Text `expr`) oder ein Funktionsgenerator (`generator`, Textform der Kurvenfunktionen, z. B. `pt1(y0=0, yinf=3, tau=1.5)`); `formula` zeigt „f(x) = …“ in der am wenigsten verdeckenden Ecke |

- Formeln (`FunctionExpression`, ein kleiner sicherer Auswerter): Zahlen (Dezimalpunkt oder Dezimalkomma zwischen
  Ziffern), `x`, `π`/`pi`, `e`, `+ − · × * / : ^ ** ² ³`, Klammern, `|…|`, Multiplikation ohne Zeichen (`2x`,
  `3(x+1)`, `x sin(x)`) sowie sin, cos, tan, asin, acos, atan, sinh, cosh, tanh, exp, ln, log/lg, log2, sqrt/√, cbrt,
  abs, sign, floor, ceil, round; ein vorangestelltes „f(x) =“ oder „y =“ wird übergangen. Ausgewertet wird nur
  Arithmetik; Länge (400) und Verschachtelungstiefe sind begrenzt. Eine unlesbare Formel zeichnet die Achsen und den
  Hinweis „Formel nicht lesbar“.
- Kurven werden abgetastet (800 Punkte über den Bereich), am y-Bereich an den genauen Schnittpunkten abgeschnitten und
  an Polstellen und Definitionsrändern getrennt (keine Linie durch den Pol von 1/x); Generatoren werden mit ihren
  exakten Eckpunkten (Sprünge) gezeichnet.
- Anschlüsse: `origin`, die Nullstellen (`zero0` …), Extrema (`ext0` …) und der y-Achsenabschnitt (`yint`); die Achsen
  sind Schienen (`axis.x`, `axis.y`), jeder Gitterpunkt ist ein Fangpunkt (`g2_3` = x 2, y 3). So docken Punkte und
  Vektoren des Mathematikmoduls am Diagramm an (ihre Enden auch frei auf seiner Fläche) und folgen ihm beim
  Verschieben, Drehen und Skalieren.
- Teile: die Kurve `series.0` (nach ihrer Formel benannt), `formula`, Achsen, Teilstriche, Zahlen, Achsennamen, Gitter.
- Aktionen: Funktion (weiterschalten), „Formel f(x)…“ (wechselt zu einer eingegebenen Formel, vorbelegt mit der
  aktuellen Funktion), „Koeffizienten a; b; c; d…“, „Funktionsgenerator…“ (bei Generatoren), Funktionsgleichung
  anzeigen, Gitternetz, Achsenzahlen, π-Teilung, „x-Bereich…“, „y-Bereich…“, Achsenbrüche.
- Bibliothek: `chart.axes.math`, `chart.axes.math.quadrant` (erster Quadrant 0…6 × 0…5), `chart.function` (Parabel),
  `chart.function.linear` (y = 0,5x + 1), `chart.function.sine` (−2π…2π, π-Teilung), `chart.function.exponential`,
  `chart.function.formula` (f(x) = x³/4 − x), `chart.function.generator` (PT1-Sprungantwort über t).
- Das Mathematikmodul behält seine Typen `math.axes.cartesian` und `math.graph.function` (bestehende Folien werden
  weiter gezeichnet); seine Bibliothekseinträge nennen diese Diagrammeinträge als `LibraryItem.Alternative` und
  treten zurück, solange das Diagrammmodul registriert ist. Zahlenstrahl, 3D-Achsen und Polarkoordinatennetz bleiben
  im Mathematikmodul.
- Export: die Kurve wird als Reihe exportiert (die Achsen im Mathematikstil als Rahmenachsen, mit Kommentar).

## Bild (Diagramme v2, Stufe 2)

Der Diagrammtyp `chart.figure` zeichnet Prinzip- und technische Abbildungen aus einer **versionierten
JSON-Spezifikation** im Parameter `spec` (ohne Parameter eingefügt zeigt er das Beispielbild unten). Kategorie „Prinzip-
und technische Abbildungen“ mit drei Vorlagen, den Prüfbildern aus dem [Entwurf Diagramme v2](../design/charts-v2.md):

| Id | Name | Zeigt |
|---|---|---|
| `chart.figure.phases` | Phasen mit Rotortemperatur (technische Abbildung) | Leistung P mit den Phasen „Vorkonditionierung“ und „Volllastphase“ (Phasenlinien t0, t1 durch beide Panels); Rotortemperatur ohne (gestrichelt) und mit Vorkonditionierung (durchgezogen) bis zur Grenztemperatur, Fläche dazwischen schraffiert, Zeitgewinn als Maßpfeil mit Hilfslinien, Bezugszeichen 40–46; Prinzipachsen, schwarz-weiß |
| `chart.figure.timing` | Zeitablauf einer Entscheidungslogik | Titel „Fig. 2 …“; drei Panels: Reifemasse gegen eine gestrichelte Grenze (Bezugszeichen 64 an der Achse, 63 an der Kurve), Wärmebedarf mit Buckel und gestrichelte Antriebsverlustwärme, nur dort schraffiert, wo der Bedarf höher ist (65–67), Abtastsignal mit durchgezogenem (68) und gestricheltem Impuls (69); t_krit als gestrichelte Markierung durch alle Panels und als Zeitpfeil unter dem ersten Panel; gedrehte Achsentitel |
| `chart.figure.temperature` | Temperaturverlauf mit Schwellen | Datenachsen 0–200 s / 0–220 °C, nur waagerechtes gepunktetes Gitter, vier gestrichelte Bezugslinien mit Beschriftung, eine schnelle und eine langsame Kurve 1. Ordnung mit je einer Lücke und Beschriftung in der Lücke, offene Kreismarker „31 s“ / „77 s“, ein senkrechter Maßpfeil „35 K“, eine Bildunterschrift; farbig |

Die Kurven der Vorlagen sind qualitativ (Verzögerungsglieder 1. Ordnung, ein Gauß-Buckel, Rechteckimpulse), keine
Messdaten.

### Spezifikation

Aufbau und Beispiel wie im englischen Dokument ([Figure](../../en/modules/charts.md#figure-charts-v2-stage-2));
die Schlüssel sind englisch:

- **Achsen** (`x` und jedes Panel): `mode` `data` (Teilstriche, Beschriftung, Gitter) oder `principle` (Achspfeil,
  keine Teilstriche, nur die benannten `marks`, gezeichnet wie Skalenbeschriftungen), `scale` `linear` / `log`, `min` /
  `max` (Prinzipachsen standardmäßig 0 … Datenmaximum), `label` und `unit` (Titel „Größe in Einheit“ / „label (unit)“),
  `grid` (Standard: an bei Datenachsen). Die x-Achse ist gemeinsam; die y-Achsentitel der Panels sind gedreht. Panels
  liegen von oben nach unten, `share` ist die relative Höhe.
- **Kurven** (`series`): Punkte als Listen `x` / `y` oder `points` (`[[x, y], …]`); `null`, Text oder fehlende Werte
  ergeben eine Lücke. `line` solid / dash / dot / dashdot / longdash / dashdotdot; ohne Strichart und Farbe nutzt das
  Farbschema durchgezogene Linien in den Reihenfarben, Schwarz-Weiß (`scheme: mono`) schwarze Linien mit den
  Strichmustern der Reihen. `label` steht am Kurvenende oder bei `labelAt` (`above`, `below`, `on` – in der Lücke auf
  weißem Grund –, `left`, `right`); mit Legende (`legend` auto / inside / right) gehen die Beschriftungen in die Legende.
- **Bezüge** (`refs`): `h` Wertlinie (Panel, `-1` = alle Panels; Beschriftung rechts, links, unten oder `end` neben dem
  Plot), `v` senkrechte Markierung (standardmäßig durch alle Panels, sonst `panel`; Beschriftung oben oder `axis` unter
  der x-Achse), `point` Kreismarker auf Kurve `series` bei `x` (`fill` open / filled, Beschriftung rechts, links, oben,
  unten).
- **Flächen** (`regions`): `between` zwischen Kurve `a` und `b` (mit `above` nur dort, wo `a` über `b` liegt: Schnitt-
  punkte werden berechnet, jedes Stück ist ein eigener Teilpfad), `below` unter einer Kurve (bis zum Panelboden oder
  `base`), `span` zwischen `from` und `to` (ein Panel oder alle). Füllung `hatch` (Standard), `cross`, `solid`
  (Deckkraft 0,3 in Farbe), `dots` (Füllarten des Kerns).
- **Anmerkungen** (`notes`): `text`, `arrow` (Text am Pfeilende), `line` (z. B. Hilfslinien), `dim` (Pfeilspitzen an
  beiden Enden, Text über/unter/links/rechts der Mitte), `numeral` (Bezugszeichen mit Hinweislinie). Punkte sind `x`,
  `y`, `panel` oder statt `y` `gap` (0 … 1 im Zwischenraum unter dem Panel, z. B. ein Zeitpfeil zwischen zwei Panels).
- **Bezugszeichen** (`numeral` an Kurven, Bezügen und Flächen, Anmerkungen der Art `numeral`): der Text steht am Ziel
  plus (`dx`, `dy`) Schrifthöhen mit dünner Hinweislinie; `axis: true` setzt das Bezugszeichen einer Wertlinie an die
  y-Achse.
- **Texte** sind eine Zeichenkette oder `{ "en": …, "de": … }`; Zahlen dürfen Zeichenketten mit Dezimalkomma sein
  (`"2,5"`).
- **Robustheit**: unbekannte Einträge werden ignoriert, falsche Typen fallen auf Standardwerte zurück; nicht lesbares
  JSON (oder eine Wurzel, die kein Objekt ist) zeichnet leere Prinzipachsen mit dem Hinweis „Bildspezifikation nicht
  lesbar (JSON)“. Das Zeichnen wirft nie. Aktionen auf einem solchen Bild werfen eine zweisprachige
  `FormatException` und lassen den Text unverändert.

### Teile, Anker und Aktionen

Teilschlüssel: `series.k`, `marker.k`, `curvelabel.k`, `ref.k`, `region.k`, `note.k`, `numeral.j` (fortlaufend über
Kurven, Bezüge, Flächen und Bezugszeichen-Anmerkungen), `paneltitle.i`, `caption`, `title`, `error` und die Teile des
Rahmens (`axis.x.1`, `axis.y.2`, `label.x`, `axistitle.y.2`, `grid.major` … – Panels ab 1 gezählt wie beim
Bode-Diagramm). Die Teileliste nennt sie mit ihren Texten („Bezugslinie 3 – Grenztemperatur“, „Bezugszeichen 44“).
Anker: Plotecken und -mitte, Ursprung und Achsschienen je Panel (`axis.x`, `axis.y`, `axis.x2` …), Datenports
`s<Reihe>.<Punkt>` und `ref<k>` für Punktmarker.

Elementaktionen: Wertlinie hinzufügen, senkrechte Markierung hinzufügen, zwischen Kurve 1 und 2 schraffieren
(ausgegraut ohne zwei Kurven in einem Panel), Prinzipachsen (alle Achsen umschalten), Schwarz-Weiß, „Spezifikation
(JSON)…“ (Eingabe), Fläche hinzufügen ▸. Teilaktionen: Kurven – Strichart ▾, Marker ▾ (keine, Kreis, Quadrat, Dreieck,
Raute, Kreuz, Plus), geglättete Kurve, darunter füllen ▸, füllen bis ▸, Kurve löschen; Datenpunkte – siehe
[Datenpunkte und Kurven](#datenpunkte-und-kurven-auf-der-zeichnung-25112026); Wertlinien und Markierungen – Strichart ▾
bzw. gefüllter Kreis, „Beschriftung…“, löschen; Flächen – siehe [Flächen](#flächen-25112026); Anmerkungen – „Text…“,
löschen; Achsen – Prinzipachsen, Wertlinie in diesem Panel bzw. senkrechte Markierung hinzufügen. Löschen verschiebt die
Indizes späterer Elemente (deren Teil-Aussehen gilt dann für das nächste Element).

Hinweise zu den Hosts: alles nativ (Linien, Freihandformen, schraffierte Freihandformen, Text); kein Vektorbild.
Erweiterungen des Rahmens (additiv): Höhenanteile der Panels, eigene Teilstriche (Marken), Achspfeile und Gitter je
Panel, Panelabstand, Bildunterschrift, zusätzlicher rechter Rand und Achstexte. Noch nicht: Datentabellen-Editor
(Portierung von Slidemax `ChartSheet`), Funktionsgeneratoren und Skizzenkurven (Stufe 3), Achsbrüche.

## Digitales Zeitdiagramm (`chart.timing`)

Mehrere Signalzeilen auf einer gemeinsamen Zeitachse: links der Signalname, rechts der Verlauf – logisch High und Low,
Takte, Datenwerte in sechseckigen Kästchen mit ihrem Text, undefinierte Abschnitte (schraffiert) und hochohmige
Abschnitte (Linie in der Mitte), mit schrägen oder senkrechten Flanken. Wahlweise: Gitterlinien an jedem Zeitschritt,
eine Zeitachse mit Beschriftung, senkrechte Markierungen mit Namen und Maßpfeile zwischen zwei Markierungen. Kategorie
„Prinzip- und technische Abbildungen“; eigenständig gezeichnet (die Idee „ein Zeichen je Zeitschritt“ ist die von
WaveDrom bekannte, Code oder Grafiken werden nicht übernommen).

| Id | Name (EN / DE) | Signale |
|---|---|---|
| `chart.timing` | Timing diagram / Zeitdiagramm | `CLK: clock`, `EN: 00111100`, `DATA: xx[A].[B].xx` (ohne Parameter eingefügt) |
| `chart.timing.clock` | Clock and enable / Takt und Freigabe | `CLK: clock`, `EN: 0011110000`, gegateter Takt `GCLK: 00pppp0000` |
| `chart.timing.bus` | Bus transfer / Busübertragung | CLK, CS, ADDR (`x.[A0]..[A1]..x.`), DATA (`z..[D0]..[D1]..z`), WE; Markierungen `t1=2; t2=5` mit Δt-Pfeil (100 × 70 mm) |
| `chart.timing.handshake` | Handshake (REQ/ACK) / Handshake-Protokoll (REQ/ACK) | REQ, ACK, DATA; Markierungen `t1=2; t2=3` mit Δt-Pfeil (100 × 55 mm) |

### Signalschreibweise (`signals`)

Ein Signal je Zeile, `Name: Verlauf`. Statt des Doppelpunkts geht auch ein Semikolon (`CLK;clock`), sodass eine
zweispaltige Tabelle (Name, Verlauf) direkt eingefügt werden kann. Leere Zeilen und Zeilen, die mit `#` beginnen,
werden übergangen; eine Zeile ohne Trennzeichen ist ein Verlauf ohne Namen. Ein Verlauf ist entweder ein **Takt** oder
**ein Zeichen je Zeitschritt**:

| Geschrieben | Bedeutung |
|---|---|
| `clock` (auch `clk`, `takt`) `[Periode] [Tastgrad] [inv]` | Takt über das ganze Diagramm; Periode in Zeitschritten (Standard 1), Tastgrad `25%` oder `0.25` (Standard 50 %), `inv` beginnt mit Low – `clock 2 25%` |
| `0` oder `l` / `1` oder `h` | Low / High |
| `x` | undefiniert (beide Pegel, dazwischen schraffiert) |
| `z` | hochohmig (Linie in der Mitte) |
| `[A5]` | Datenwert mit dem Text „A5“ (sechseckiges Kästchen); `[A][A]` sind zwei Übertragungen mit einer Flanke dazwischen |
| `=` | Datenwert ohne Text |
| `p` / `n` | eine Taktperiode, beginnend mit High / Low (je ein halber Schritt) |
| `.` | den vorigen Zustand einen Schritt länger halten (nach `p` oder `n`: eine Periode mehr) |
| Leerzeichen | werden übergangen (`0011 1100`) |

Das Diagramm ist so lang wie der längste geschriebene Verlauf (nur Takte: 8 Schritte bzw. vier Perioden, höchstens
400 Schritte); kürzere Verläufe halten ihren letzten Zustand bis zum Ende. Gleiche Pegel hintereinander verschmelzen
(`0..1` = `0001`). Der Parser wirft nie: Probleme (unbekanntes Zeichen, fehlendes `]`, `.` am Anfang, unbekannte
Taktangabe, leerer Verlauf) werden mit Zeile und Zeichen gemeldet („Zeile 2, Zeichen 5: Unbekanntes Zeichen „q“ …“);
die Zeichnung zeigt ein unbekanntes Zeichen als undefiniert.

Markierungen (`markers`): `Name=Zeit` (oder `Name@Zeit`, eine bloße Zeit heißt t1, t2 …), getrennt durch `;`, z. B.
`t1=2; t2=5,5` (Dezimalkomma erlaubt). Zeiten in Achseinheiten, also Zeitschritte × `dt`. Maßpfeile (`spans`): `t1-t2`
oder `t1-t2: t_su` (Text, Standard „Δt“), Markierungen über ihren Namen oder ihre Nummer (`1-2`), mehrere getrennt durch
`;`; überlappende Pfeile erhalten eigene Zeilen. `dt` (Zeit je Schritt, Standard 1) und `tunit` (z. B. `ns`)
beschriften die Zeitachse („t in ns“ / „t (ns)“).

### Layout, Teile, Anker und Aktionen

Texte folgen dem kleineren Maßstabsfaktor bezogen auf die Entwurfsgröße 100 × 50 mm und der Zeilenhöhe (viele Signale
in einem kleinen Rahmen ergeben kleineren Text, ein Strecken in nur einer Richtung lässt ihn gleich); Namen nehmen
höchstens 35 % der Breite ein und werden eingepasst; Werttexte werden in ihr Kästchen eingepasst und entfallen, wo ein
Kästchen zu schmal zum Lesen ist.

Teilschlüssel: `signal.k` (der Verlauf: eine offene Freihandform für beide Pegellinien, eine geschlossene Freihandform je
Wertkästchen bzw. undefiniertem Abschnitt, die Werttexte), `name.k`, `ref.k` (Markierungslinie und Name), `dim.k`
(Maßpfeil und Text), `grid.major`, `axis.x`, `tick.x`, `label.x`, `axistitle.x`, `title`. So lässt sich ein einzelnes
Signal umfärben oder ausblenden; ein Aussehen auf der Art (`signal`, `name`) gilt für alle. Anker: Ecken und Mitte des
Verlaufsbereichs (`plot.tl` …), `origin` und die Zeitachse als Schiene `axis.x`; jede Flanke ist ein Andockpunkt
`s<Signal>.<Flanke>` (für Ursache-Wirkungs-Pfeile).

Elementaktionen: „Signale…“ (Eingabe, Id `chart.data`: eine Zeile je Signal, Zeilen getrennt durch `|`; das
PowerPoint-Add-in öffnet seinen Tabelleneditor; Fehler werden mit Zeile und Zeichen abgewiesen), Signal hinzufügen
(`S4: 0…1…` so lang wie das Diagramm), letztes Signal entfernen (bei einem Signal ausgegraut), Gitternetz,
Zeitbeschriftung, Flanken schräg / senkrecht, „Markierungen…“, „Maßpfeile…“ (bei weniger als zwei Markierungen
ausgegraut). Teilaktionen: Signal oder Name – „Signale…“, „Name…“, dieses Signal entfernen, Flanken; Markierung –
„Markierungen…“, diese Markierung entfernen, „Maßpfeile…“; Maßpfeil – bearbeiten, entfernen; Gitter – Gitternetz;
Teile der Zeitachse – Zeitbeschriftung.

Folien für die Weitergabe bereinigen: Signalnamen werden zu S1, S2 … und Markierungsnamen zu t1, t2 … (Texte ersetzen),
Datenwerte zu D1, D2 … (normiert) oder zufälligen Hexadezimalwerten (zufällig; gleiche Werte bleiben gleich), der
Zeitmaßstab zu reinen Schritten (normiert: `dt` = 1, Einheit gelöscht, Markierungen umgerechnet) oder einem
Zufallsfaktor (zufällig). Die Verläufe bleiben.

Export: alles ist nativ (Freihandformen, schraffierte Freihandformen, Linien mit Pfeilspitzen, Text), daher zeichnen
der TikZ- und der SVG-Export es aus den Grundelementen. Die Exporte als matplotlib und pgfplots haben dafür keine
Datenachsen und melden ein Zeitdiagramm als „could not be exported“ (nicht exportierbar).

## Präsentationsdiagramme (2026-11-26)

Die Diagrammarten, die Vortragende neben XY- und Balkendiagrammen brauchen: Anteile (Kreis, Ring), Richtungen (Polar,
Netz), Flüsse (Sankey), Zeitpläne (Gantt) und Verteilungen (Boxplot, Histogramm). Alle stehen in der Kategorie
Datendiagramme, sind ohne Parameter mit neutralen Beispieldaten einfügbar (Buchstaben als Namen, wie beim
Balkendiagramm) und halten ihre Daten im Textparameter `data` im obigen [Datenformat](#datenformat), sodass die
Eingabe *Daten…* und der Tabelleneditor in PowerPoint wie bei den anderen Diagrammen arbeiten.

| Bibliothekseintrag | Name (EN / DE) | Vorlage |
|---|---|---|
| `chart.pie` | Pie chart / Kreisdiagramm | fünf Segmente A–E, Prozentbeschriftung, Namen in der Legende (80 × 65 mm) |
| `chart.pie.names` | Pie chart with names / Kreisdiagramm mit Namen | Beschriftung „Name Prozent“, keine Legende |
| `chart.donut` | Donut chart / Ringdiagramm | `hole` 0,55 |
| `chart.polar` | Polar chart / Polardiagramm | zwei Reihen über acht Richtungen, geschlossene Linien |
| `chart.polar.radar` | Radar chart / Netzdiagramm | gefüllte Flächen, 0° oben und im Uhrzeigersinn |
| `chart.sankey` | Sankey diagram / Sankey-Diagramm | A, B, C → M → X, Y, Z |
| `chart.gantt` | Gantt chart / Gantt-Diagramm | vier Vorgänge, ein Meilenstein, Heute-Linie bei 6 |
| `chart.box` | Box plot / Boxplot | drei Kategorien mit je zehn Werten und zwei Ausreißern |
| `chart.histogram` | Histogram / Histogramm | dreißig Messwerte, automatische Klassen |

**Kreis und Ring.** Ein Segment je Kategorie mit positivem Wert, sein Winkel ist der Anteil des Werts an 360°; das
erste Segment beginnt bei `start` (Grad im Uhrzeigersinn ab 12 Uhr, Vorgabe 0), die Segmente laufen im oder gegen den
Uhrzeigersinn. `hole` > 0 ergibt einen Ring (Innenradius als Anteil des Außenradius); `center` schreibt einen Text in
das Loch. Eine Segmentbeschriftung steht im Segment – beim Ring im Ring –, wo ihr Textrahmen zwischen die Radien und
die Segmentkanten passt; sonst rückt sie mit einer Führungslinie nach außen, äußere Beschriftungen werden je Seite
auseinandergeschoben und der Kreis wird kleiner, damit sie passen. Jeder Text bleibt in der Box (er wird kleiner, bevor
er sie verlassen würde). Farbig: Segmente in den Reihenfarben mit weißen Fugen und weißer Beschriftung; Schwarz-Weiß
oder `patterns`: die Füllmuster der Reihen mit schwarzen Kanten und Beschriftungen auf weißem Grund. Stehen die Namen an
den Segmenten, ist die automatische Legende aus; sonst nennt die Legende die Namen.

**Polardiagramm.** Zeilen `Winkel;r` (mehrere y-Spalten oder Blöcke sind mehrere Reihen, wie im XY-Diagramm). Die
Radialachse beginnt mit 0 im Pol (`rrange` legt sie fest; Radien darüber hinaus liegen auf dem Rand). Die Winkelachse
hat Speichen alle 30° (45° oder 90° bei kleinen Diagrammen), außen in Grad beschriftet; das Radialnetz hat Ringe an den
Radialteilungen, beschriftet entlang des Strahls einen halben Schritt nach 0°. `zero`: 0° rechts mit Winkeln gegen den
Uhrzeigersinn (Mathematik, matplotlib) oder 0° oben mit Winkeln im Uhrzeigersinn (Kompass). `closed` verbindet den
letzten Punkt mit dem ersten; die Darstellung „gefüllte Fläche“ füllt jede Reihe durchscheinend (Schwarz-Weiß:
Schraffuren).

**Sankey-Diagramm.** Jeder Knoten kommt in die Spalte seines längsten Wegs von einer Quelle; Knoten ohne abgehende
Flüsse kommen in die letzte Spalte. Ein Knoten ist so hoch wie der größere Wert aus Zu- und Abfluss, mit einem Maßstab
für das ganze Diagramm (so gewählt, dass die vollste Spalte die Höhe füllt); die Flüsse eines Knotens liegen also auf
jeder Seite genau über seiner Höhe. Spalten werden mittig gesetzt und nach der mittleren Lage ihrer Nachbarn
geordnet, damit sich wenig kreuzt; Flüsse verlassen einen Knoten in der Reihenfolge ihrer Ziele und erreichen ihn in
der Reihenfolge ihrer Quellen. Ein Fluss ist eine geschlossene Freihandform mit zwei kubischen Kanten in der Farbe
seiner Quelle (35 % Deckkraft); Schwarz-Weiß: schwarze Knoten und weiße, umrandete Flüsse. Beschriftungen „Name Wert“
stehen links der ersten Spalte und rechts der übrigen Knoten. Flüsse, die einen Kreislauf schließen, können nicht von
links nach rechts laufen: sie werden nicht gezeichnet, ein Hinweis nennt ihre Zahl.

**Gantt-Diagramm.** Vorgänge von oben nach unten auf einer Kategorieachse (die erste Zeile oben), die Zeit auf einer
linearen Achse in beliebiger Einheit (der x-Achsentitel nennt sie, Vorgabe „Zeit“/„Time“). Eine Zeile ohne Dauer ist
ein Meilenstein (eine Raute); Blöcke sind Gruppen mit eigener Farbe und eigenem Legendeneintrag. `today` zeichnet eine
gestrichelte Heute-Linie mit der Beschriftung „Heute“/„Today“ und dem Schienenanker `today`.

**Boxplot.** Je Kategorie und Reihe die Quartile mit linearer Interpolation (Hyndman–Fan Typ 7, wie numpy und
matplotlib), eine Box vom unteren zum oberen Quartil mit dem Median, Whisker bis zu den äußersten Werten innerhalb von
`whis` × IQR jenseits der Box (Tukey, Vorgabe 1,5) und die Werte darüber hinaus als hohle Kreise; ohne `outliers`
reichen die Whisker bis Minimum und Maximum. Boxen mehrerer Reihen stehen nebeneinander.

**Histogramm.** Klassen gleicher Breite über den Bereich der Messwerte (`xrange` legt ihn fest); eine Klasse umfasst
[links, rechts), die letzte schließt ihren rechten Rand ein (wie numpy). `bins` = 0 wählt ⌈log₂ n⌉ + 1 Klassen
(Sturges-Regel für die größte Reihe); „Mehr Klassen“/„Weniger Klassen“ ändern um etwa ein Fünftel. Mehrere Reihen
stehen in jeder Klasse nebeneinander.

**Teile.** Kreis: `slice.k` (k = Zeile der Kategorie), `slicelabel.k` (Text, Hinterlegung, Führungslinie),
`centertext`, `legend`, `title`. Polar: `series.k`, `marker.k`, `axis.a` (Kreis), `axis.r` (Radialachse), `label.a`,
`label.r`, `grid.major`, `legend`. Sankey: `node.k`, `nodelabel.k`, `flow.k` (k = Zeile des Flusses), `note`. Gantt:
`bar.g` (Balken der Gruppe g), `milestone.g`, `today` und die Achsenteile. Boxplot: `box.k` (Boxen, Mediane, Whisker
der Reihe k), `outlier.k` und die Achsenteile. Histogramm: `bar.k` und die Achsenteile. Teilnamen kommen aus den Daten
(„B“, „Fluss A → M“, „Knoten M“); Teilaktionen bieten die Daten- und Auswahlaktionen des Typs, dann Ein-/Ausblenden und
die Stile.

**Anker.** Jedes Diagramm hat `plot.tl` … `plot.c` (Kreis: das Quadrat um den Kreis, dazu `centre`). Andockpunkte:
Kreis `s1.<Segment>` auf dem Rand in der Segmentmitte; Polar `s<Reihe>.<Punkt>`, `origin` (Pol) und die Schiene
`axis.r`; Sankey `n<Knoten>.in` / `n<Knoten>.out`; Gantt Balkenenden und Meilensteine `s<Gruppe>.<Vorgang>`; Boxplot
Mediane `s<Reihe>.<Kategorie>`; Histogramm Balkenoberkanten `s<Reihe>.<Klasse>`. Gantt, Boxplot und Histogramm haben
außerdem die Achsenanker (`origin`, `axis.x`, `axis.y`).

**Folien bereinigen.** Kreis wie das Balkendiagramm (Werte durch den größten geteilt, Kategorienamen werden
Buchstaben). Boxplot und Histogramm: alle Werte durch eine monotone Abbildung (Reihenfolge, Quartile und Klassen
behalten ihre Ränge). Gantt: Zeiten auf 0 verschoben und auf 0…1 skaliert (normiert) oder mit einem Zufallsfaktor,
die Heute-Linie mit ihnen. Polar: Radien durch den größten geteilt (normiert) oder skaliert und variiert, die Winkel
bleiben. Sankey: Knotennamen werden A, B, C …, Werte durch den größten Fluss geteilt oder mit einem Zufallsfaktor
skaliert (Zu-/Abflussbilanzen bleiben).

**Codeexport und -import.** Für diese Diagramme gibt es noch keinen matplotlib- oder pgfplots-Export: beide Exporte
melden sie als „could not be exported“ (nicht exportierbar, mit deutscher Begründung); TikZ- und SVG-Export zeichnen sie aus den Primitiven. Der
matplotlib-Import liest `pie` (Werte, `labels`, `autopct` → Prozentbeschriftung, `startangle` und `counterclock`
umgerechnet auf „im Uhrzeigersinn ab 12 Uhr“, `wedgeprops=dict(width=…)` → Ringloch; `explode`, `colors` und
`normalize=False` werden gemeldet) und Polarachsen (`subplot_kw={'projection': 'polar'}`,
`add_subplot(…, projection='polar')` oder `polar=True`, `plt.polar`): `plot`-Kurven mit den Winkeln von Bogenmaß in
Grad umgerechnet, `fill` → gefüllte Darstellung, `set_theta_zero_location('N')` mit `set_theta_direction(-1)` →
Kompassdarstellung, `set_rmax`/`set_rlim`/`set_ylim` → `rrange`; Teilungseinstellungen der Polarachsen werden
gemeldet. Andere Projektionen (3d) werden weiter gemeldet, `hist` und `boxplot` werden noch nicht importiert.

**Hinweise für die Hosts.** Nur native Primitive: Segmente, Ringe und Flüsse sind geschlossene Freihandformen (Flüsse
mit kubischen Kanten), Knoten, Balken und Boxen Rechtecke, Meilensteine geschlossene Freihandformen, Netze je eine
Freihandform, Texte native Texte; kein Vektorbild.

## Verknüpfte Kennlinien (Calc 5)

Eine Kennlinie kann mit einem Element verknüpft werden, dessen Werte einen Kennliniensatz ergeben (Vertrag
`ICharacteristics` in `src/InventaSketch.Core/Characteristics.cs`; Entwurfsnotizen in Abschnitt 14 von
[engineering-calculations.md](../design/engineering-calculations.md)). Das Diagramm (`chart.linked`) speichert nur die
Verknüpfung `<Element-Id>#<Satz-Schlüssel>` im Parameter `link`. Bei jedem Zeichnen fragt es das Element nach dem Satz,
berechnet aus den gelösten Werten des Elements, und zeichnet ihn wie ein Bild: ein Panel je y-Achse, die Kurven (die
eigene Kennlinie durchgezogen, Last- und Anlagenkennlinien gestrichelt), Bänder (der MPP-Bereich des
Wechselrichters), Grenzlinien und die markierten Punkte (Betriebspunkt gefüllt, Auslegungs- und Kipppunkt offen).
Hinweise auf angenommene Werte stehen als Bildunterschrift darunter. Das Element führt das Diagramm als abhängiges
Objekt, daher zeichnen die Hosts es bei jeder Änderung des Elements neu.

| Element (Modul) | Satz | Kurven und Punkte |
|---|---|---|
| Kreiselpumpe (Prozess), Umwälzpumpe (Gebäudetechnik) | Pumpen- und Anlagenkennlinie | H(V̇) = H₀·(1 − (V̇/V̇_max)²), Anlagenkennlinie H_stat + k·V̇² durch den Betriebspunkt; Betriebspunkt beim Förderstrom der Pumpe (Griff) |
| Maschinenquerschnitte, Motor- und E-Maschinenblock (Maschinen) | Drehmoment-Drehzahl-Kennlinie | Kloss-Kennlinie durch n_s und den Bemessungspunkt beim Käfigläufer, sonst Momentengrenze; Betriebspunkt (Griff), Kipppunkt |
| PV-Modul und -String (Energie) | PV-Kennlinien I–U und P–U | I(U) und P(U) bei STC (zwei Panels), MPP, MPP-Bereich des Wechselrichters, U_DC,max und U_oc bei θ_min |
| Widerstand, Kondensator, Spule in einem RC-/RL-/RLC-Kreis (Elektrotechnik) | Bode-Diagramm, Sprungantwort | Betrag und Phase mit Eck- bzw. Kennfrequenz (Griff: setzt C oder L); Sprungantwort mit τ (63,2 %) oder dem Überschwingen |
| Zahnräder und Wellen (Mechanik) | Drehzahlen und Momente des Getriebes | ein Balken je Welle für n, M und P |
| Heizkessel, Wärmepumpe (Gebäudetechnik) | Heizkurve | Vor- und Rücklauftemperatur über der Außentemperatur; Auslegungspunkt (Griff: setzt die Vorlauftemperatur), aktueller Betriebspunkt (Griff: die Außentemperatur) |

Aktionen:

- Am Element: **Kennlinie ▸** (ein Menü, ein Befehl je Satz) fügt rechts neben dem Element ein damit verknüpftes
  Diagramm ein, in einem Rückgängig-Schritt.
- An Pumpen-, Drehmoment-, Bode-, Bild- und verknüpften Diagrammen: **Mit Element verknüpfen ▸** listet die
  Kennliniensätze der Elemente derselben Folie (nächste zuerst, höchstens acht Elemente); die Wahl macht das Diagramm
  zu einer verknüpften Kennlinie (Lage, Größe und Titel bleiben).
- An der verknüpften Kennlinie: **Lösen (Werte einfrieren)** macht daraus ein Bild (`chart.figure`), dessen
  Spezifikation die aktuellen Kurven, Punkte, Bänder und Grenzlinien als einfache Daten enthält; spätere Änderungen
  des Elements erreichen es nicht mehr. Solange das Element fehlt, ist die Aktion ausgegraut.

Griffe: Jeder ziehbare Punkt hat einen Griff `op.<Punkt>` auf seinem Marker, der sich entlang x (auf der Kurve) oder
entlang y bewegt. Beim Loslassen erhält der Punkt den neuen Wert (auf die Achsenauflösung gerundet) und gibt die zu
schreibenden Werte zurück; sie werden im Element wie ein gezogener Wert gespeichert (drei signifikante Stellen;
eingegebene Werte, die das Element überbestimmen würden, weichen, `ValueDrag`). Angenommene Kennliniendaten werden
beim ersten Ziehen mitgespeichert, damit die Kurve bleibt, wo sie ist. Das Diagramm selbst ändert sich nicht, es folgt
dem Element. Das ist eine Änderung auf Dokumentebene: Hosts rufen `DocumentHandles.Move(registry, doc, chartId, key,
position, lang)` auf (für alle anderen Typen fällt es auf `ObjectType.MoveHandle` zurück) und wenden das Ergebnis als
einen Rückgängig-Schritt an; das objektbezogene `MoveHandle` der verknüpften Kennlinie ändert nichts.

Ein fehlendes Element oder eine unlesbare Verknüpfung zeigt eine Meldung in einem leeren Rahmen („Verknüpftes Element
fehlt: lösen oder neu verknüpfen“). Die Exporte nach matplotlib und pgfplots schreiben eine verknüpfte Kennlinie als
das Bild, das beim Lösen entstünde.

## Hinweise zu den Hosts

Die gesamte Ausgabe besteht aus nativen Primitiven: eine Freihandform je Reihenlinie (geglättete Kurven als
kubische Bézierpfade), je eine Freihandform mit mehreren Teilpfaden für Gitterlinien, Teilstriche, Höhenlinien einer
Stufe, Grenz- und Hilfslinien, eine Freihandform je Markersatz, Rechtecke für Balken, Legende und
Beschriftungshintergründe, ein geschlossenes Polygon für das Band sowie Text. Die Titel der y-Achsen sind um −90°
gedreht. Schraffur- und Punktfüllungen nutzen die Füllarten des Kerns. Ein Vektorbild ist nicht nötig. Diagramme
hängen nicht von Nachbarobjekten ab, außer der verknüpften Kennlinie (`chart.linked`), die aus dem verknüpften Element
gezeichnet wird (`DependsOnNeighbours`; das Element nennt sie als abhängiges Objekt). Ihre Griffe am Betriebspunkt
ändern das Element: Hosts wenden sie mit `DocumentHandles.Move` an (siehe oben).

## Diagramme aus Bildern (Bausteine)

Stufe 5 von [Diagramme v2](../design/charts-v2.md): reine Bildanalyse in `src/InventaSketch.Modules.Charts/Scanning`
auf einem rohen `PixelImage` (Breite, Höhe, RGBA-Bytes; durchsichtige Pixel gelten als weiß). Keine Host-API, keine
Texterkennung und kein fremder Dienst sind nötig; die Hosts liefern das Bild (Zwischenablage, Datei, Bildschirmfoto)
und später die Anbindung an die Diagrammtypen. Nichts wirft eine Ausnahme: leere, winzige, weiße oder widersprüchliche
Bilder ergeben zweisprachige `Warnings`.

- **Balkenscanner** (`BarScanner`, portiert aus dem Diagramm-Scanner von Slidemax): Hintergrund = häufigste Farbe,
  zusammenhängende Flächen ähnlicher Farbe, dünne lange Stücke sind Achse oder Gitternetz, die Grundlinie ist die
  Kante, an der die meisten Flächen enden; erkennt Säulen oder Balken, gestapelt (Serien nach Farbe) oder gruppiert,
  Teile unter der Grundlinie und den Gitterabstand. `BarScanner.Values` macht aus Pixelausdehnungen Zahlen mit einem
  `BarScale` (Wert der Grundlinie und Wert der größten Säule oder eines Gitterschritts).
- **Beschriftungen**: `ITextReader` ist die Schnittstelle für eine Texterkennung des Hosts; die Engine bringt
  `DigitTextReader` mit (portierte Ziffernerkennung aus Slidemax: Ziffern, Trennzeichen, Vorzeichen und Prozent
  gängiger Schriften per Musterkorrelation, keine Buchstaben). `BarLabelReader` bestimmt den Maßstab aus der
  Beschriftung der Wertachse (robuste Geradenanpassung, deutsche und englische Schreibweise) oder aus
  Wertbeschriftungen an den Säulen und liest Kategorienamen. Mit `TextReaderGate` schaltet ein Host eine scheiternde
  Texterkennung einmalig ab.
- **Kurvendigitalisierer** (`CurveDigitizer`, neu): findet Zeichenfläche und Achsen (unterste lange waagerechte und
  linke lange senkrechte graue Linie; weitere lange Linien sind Gitternetz und werden ausgeblendet), gruppiert die
  Kurvenfarben (jedes Pixel wird als Mischung aus Hintergrund und Kurvenfarbe erklärt, so bleiben Kantenglättung und
  Unschärfe bei ihrer Kurve), verfolgt jede Farbe Spalte für Spalte (an Kreuzungen behalten beide Kurven ihre
  Steigung, Lücken von Strichen und Kreuzungen werden überbrückt, Bruchstücke verbunden), verwirft kurze Spuren (Text,
  Legende), meldet Gruppen kurzer paralleler gerader Spuren als `Hatches` (Schraffur) und schätzt die Strichart
  durchgezogen / gestrichelt / gepunktet aus den Lücken. Schwarz-Weiß-Bilder ergeben eine Farbe, deren Kurven die
  Verfolgung trennt.
- **Kalibrierung**: `DigitizeOptions.XAxis` / `YAxis` (`AxisCalibration`: zwei Pixelpositionen mit bekannten Werten,
  linear oder logarithmisch) oder, für Achsen ohne Angabe, die von `DigitizeOptions.TextReader` gelesenen
  Beschriftungen (`CurveLabelCalibration`, logarithmische Achsen werden an 1, 10, 100 … erkannt). Ohne Kalibrierung
  bleiben die Punkte in Pixeln, mit Warnung.
- **Ergebnis**: `ScanResult` mit Zeichenfläche, Achsen, Gitterlinien, Reihen (Farbe, Strichart, Pixel- und
  Datenpunkte), Schraffuren, Kalibrierung und Warnungen. `ScanDataText.FromCurves` schreibt den Datentext für
  `chart.xy` (Zeilen `x;y`, Leerzeile zwischen Reihen, `#`-Kommentare mit Strichart und Farbe);
  `ScanDataText.FromBars` schreibt eine Kategorientabelle für `chart.bar`.
- **Genauigkeit (synthetische Tests)**: sich kreuzende Kurven in zwei Farben innerhalb 0,7 % des y-Bereichs (1,1 % nach
  3 × 3-Kastenfilter und Rauschen; 98 % der Punkte innerhalb 0,1 %), ein sich kreuzendes schwarzes Paar durchgezogen /
  gestrichelt innerhalb 0,8 %, eine steile Sigmoidkurve innerhalb 0,2 %, Balken nach Unschärfe innerhalb 1,4 % des
  größten Werts, Kalibrierung aus gestempelten Ziffern innerhalb 0,8 %.
- **Grenzen**: keine gedrehten Bilder (Achsen müssen waagerecht und senkrecht sein), gleichfarbige Kurven, die sich
  sehr flach kreuzen, können vertauscht werden, gefüllte Flächendiagramme werden als „gefüllte Flächen“ gemeldet (dafür
  den Balkenscanner verwenden), die Ziffernerkennung liest nur Zahlen in Schriften ähnlich den gespeicherten Mustern
  (eine Texterkennung des Hosts lässt sich einstecken).

## Kurvenfunktionen, Skizzenerkennung und Achsenbrüche (Bausteine)

Diagramme v2, Stufe 3 (25.11.2026): reine, hostunabhängige Bibliotheken im Diagrammmodul; Diagrammtypen und Hosts
nutzen sie noch nicht (die Einbindung folgt im Figurenmodell).

- **Kurvenfunktionen** (`CurveFunctions`, `CurveSpec`): Konstante, Rampe (linear), Sprung, Rechteckimpuls, Pulsfolge,
  Sägezahn, Dreieck, Sinus, exponentielle Annäherung (PT1), exponentieller Abfall, Sprungantwort zweiter Ordnung (PT2),
  Gauß-Glocke und Kreis/Ellipse (geschlossen, parametrisch). Jede Art hat einen zweisprachigen Namen und
  Parameterdefinitionen (Name, Vorgabe, gültiger Bereich, zweisprachige Bezeichnung; ohne Einheit – sie übernehmen die
  Einheiten der Achsen; `ToParamDef()` für den Inspektor). Textform, kulturunabhängig: `pt1(y0=20, yinf=160, tau=12)`;
  Werte auch nach Position (`sine(2, 10)`), Groß-/Kleinschreibung egal, fehlende Parameter erhalten ihre Vorgabe,
  optionale Verschiebungen (`t0`, `phase`, `offset`) entfallen, solange sie neutral sind, `circle(cx, cy, r)` ergibt
  eine Ellipse. Ein deutsches Dezimalkomma wird angenommen, wo es eindeutig ist (`tau=1,5` zwischen zwei Ziffern in
  einem benannten Wert, oder alle Argumente mit `;` getrennt). Fehler werfen `CurveSpecFormatException` (eine
  `FormatException`) mit englischer und deutscher Meldung. `Sample(xMin, xMax)` liefert exakte Ecken für stückweise
  lineare Arten (ein Sprung sind zwei Punkte mit gleichem x), einen Stützpunkt am Beginn `t0` von PT1/PT2/Abfall,
  mindestens 64 Abschnitte je Sinusperiode und ein dichtes Band um eine schmale Gauß-Glocke (höchstens 20 000 Punkte).
- **Skizzenerkennung** (`SketchFit.Fit(trail, options)`): Eingabe ist die Mausspur in Datenkoordinaten und möglichst
  der Plotrahmen in Datenkoordinaten (alle Toleranzen beziehen sich darauf und wirken so wie Bildschirmabstände). Die
  Spur wird nach Bogenlänge neu abgetastet, vereinfacht (Ramer–Douglas–Peucker), auf eine geschlossene Schleife
  geprüft und mit Konstante, Rampe, Sprung, Rechteckimpuls, Sägezahn, Dreieck, Sinus, PT1, Abfall oder (geschlossen)
  einer achsparallelen Ellipse angepasst: Eine Rastersuche mit linearen kleinsten Quadraten liefert den Start,
  Nelder–Mead verfeinert ihn auf einem robusten (Huber), symmetrischen Abstand Spur ↔ Kurve, so dass senkrechte
  Flanken, Überschwinger und Absetzhaken nicht stören. Unter den Arten, deren Restfehler höchstens 1,25 × den besten
  + 0,003 beträgt, gewinnt die mit den wenigsten Parametern; angenommen wird sie bis zu einem Restfehler von 2 % des
  Rahmens. Das Ergebnis enthält Art, Spezifikation, Restfehler, Zuversicht, die vollständige Rangfolge, die
  vereinfachte Spur und immer eine geglättete Spur (zentripetales Catmull–Rom; die gleichförmige Variante ist die
  Formel von Slidemax `Smoothed`), die verwendet wird, wenn nichts passt. Alle Schwellen sind benannte Konstanten in
  `SketchFit`.
- **Achsenbrüche** (`BrokenAxis`, `BrokenPlot`, `AxisBreaks`, aus Slidemax übernommen und von Säulendiagrammen auf
  jede x- oder y-Achse mit stetigen Werten verallgemeinert): Wert ↔ Lage mit einem oder mehreren Brüchen (Werte
  `Lo … Hi` auf eine Lücke in mm gestaucht; Werte im Bruch liegen linear in der Lücke, die Umkehrung gilt),
  Seitenpunkte auf Achsen beliebiger Richtung, XY-Abbildung, `VisibleRuns` schneidet Kurven genau an den Bruchgrenzen,
  automatische Lage von Brüchen in datenfreien Lücken (`AxisBreaks.Spans`, die Slidemax-Regeln: eigene Höhe, ganz
  stauchen, wirkungslose Brüche entfallen) sowie die Zickzack- oder Doppelschrägstrich-Markierungen (Maße aus
  Slidemax: Linien 4,5 pt auseinander, Zacken 6,5 × 1,75 pt, Schrägstriche unter 15°) quer über den Plot oder kurz auf
  der Achslinie. Nur lineare Achsen. Seit Stufe 3 von den Diagrammtypen gezeichnet, siehe
  [Achsenbrüche](#achsenbrüche-diagramme-v2-stufe-3).

Hosts: Die Bausteine erzeugen Daten (Spezifikationen, Polylinien, Markierungspunkte); ein Host zeichnet sie mit den
vorhandenen nativen Primitiven (Freihandformen und geschlossene Polygone), ein Vektorbild ist nicht nötig.

## Export als matplotlib und pgfplots (Diagramme v2, Stufe 4)

Das Diagramm-Modul bietet zwei Fachexporte (`IExportProvider`) für Auswahlen mit Diagrammen (`chart.*`), mit denen sich
ein Diagramm als Code nachbauen lässt, in dem jeder Text bearbeitbar bleibt:

- **Diagramme als Python (matplotlib)** (`chart.matplotlib`, `.py`): ein eigenständiges Skript für matplotlib 3.5 oder
  neuer, einfache Listen, kein numpy-Code. Eine Funktion `figure_1()`, `figure_2()` … je Diagramm; `python skript.py`
  zeigt die Diagramme, `python skript.py out/bild.pdf` speichert `out/bild-1.pdf`, `out/bild-2.pdf` … (die Endung
  wählt das Format). Texte sind gewöhnliche matplotlib-Texte, mathtext-sicher gemacht (`$` maskiert).
- **Diagramme als LaTeX (pgfplots)** (`chart.pgfplots`, `.tex`): ein `standalone`-Dokument (pdfLaTeX oder LuaLaTeX,
  pgfplots-Kompatibilität 1.18, Bibliotheken `fillbetween`, `groupplots`, `arrows.meta`, `patterns.meta`) mit einem
  `tikzpicture` je Diagramm (je eine Seite); das `tikzpicture` lässt sich in jedes Dokument kopieren. Jeder Text –
  Achsentitel, benannte Teilstriche, Legendeneinträge, Titel, Anmerkungen, Bezugszeichen – ist LaTeX-Text in der
  Dokumentschrift. Sonderzeichen werden maskiert, griechische Buchstaben und Symbole werden LaTeX-Befehle
  (pdfLaTeX-tauglich), deutsche Skalenbeschriftungen nutzen `/pgf/number format/use comma`.

Beide lesen die Diagrammmodelle, nicht die Zeichnung: den Datentext, die Parameter und die Bildspezifikation;
Achsbereiche, Teilung und Achsentitel kommen aus dem berechneten Rahmen (dasselbe Layout wie beim Zeichnen). Zahlen
werden immer kulturunabhängig geschrieben (ein deutsches Dezimalkomma gelangt nie in den Code; deutsche
Wertbeschriftungen bleiben Text mit Komma). Ein Index wie `t_krit` oder `f_c` wird ein mathematischer Index
(`$t_{\mathrm{krit}}$`). Jedes Element trägt seinen Teileschlüssel als Kommentar (`# series.0`, `% region.0`), und das
Aussehen der Teile gilt: ausgeblendete Teile entfallen, Teilestile ersetzen Farbe, Breite und Strichart.

| Merkmal | matplotlib | pgfplots |
| --- | --- | --- |
| Bildgröße | `figsize` aus der Elementgröße (mm → Zoll) | `scale only axis` mit der Plotfläche jedes Teilbilds (mm) |
| Gestapelte Teilbilder (Bilder, Bode) | `plt.subplots(n, 1, sharex=True)`, Höhenverhältnisse aus den Anteilen | `groupplot` (1 × n), x-Beschriftung am unteren Rand |
| Kurven | `plot` mit Farbe, Breite, Strichart (durchgezogen, gestrichelt, gepunktet, Strichpunkt, lang gestrichelt, Strich-Punkt-Punkt), Markern (gefüllt/offen) | `\addplot` mit denselben Optionen |
| Lücken | `nan` in den Listen | `nan` mit `unbounded coords=jump` |
| Stufen | `drawstyle='steps-post'` | `const plot` |
| Geglättete Kurven | der Spline des Renderers, abgetastet (bei log. Achsen im log. Raum) | dieselben Punkte |
| Skalen, Bereiche, Teilung | log. Skalen, feste Bereiche, `MultipleLocator(Schritt)`, benannte Marken | `xmode=log`, `xtick distance`, benannte `xtick`/`xticklabels` |
| Gitter | Haupt-/Nebengitter je Achse, Gitterfarbe und Strichart | `xmajorgrids`, `xminorgrids`, Gitterstile |
| Prinzipachsen | keine Teilstriche, Pfeilspitzen an den Achsenden | `axis lines` mit `-Stealth` |
| Wertlinien, Grenzlinien | `axhline` mit Beschriftung (`annotate`) | `\draw` mit einem `\node` |
| Senkrechte Markierung durch alle Teilbilder | `ConnectionPatch` vom oberen Rand des ersten zum unteren Rand des letzten Teilbilds | `\coordinate`s in den Teilbildern, Linie nach der Gruppe gezeichnet |
| Flächen | `fill_between` mit Schraffur `//`, `xx`, `..` oder flächig; „nur wo A über B liegt“ als Maske `where=[a >= b …]`; x-Bereiche als `axvspan` | `fill between` (Bibliothek `fillbetween`) mit `patterns.meta`-Mustern; maskierte Flächen als die aus beiden Kurven berechneten Polygone |
| Anmerkungen, Pfeile, Maße | `annotate` (Pfeil `-\|>`, Maßpfeil `<\|-\|>`) | `\node`, `\draw` mit `Stealth`-Spitzen |
| Bezugszeichen | `annotate` mit Hinweislinie (Versatz in Schrifthöhen) | `\node` um `em` verschoben plus Hinweislinie `\draw` |
| Balken | `bar`/`barh`, gruppiert über Versatz, gestapelt mit `bottom`/`left`, Wertbeschriftung mit `bar_label`, Muster in Schwarz-Weiß | `ybar`/`xbar` (gestapelt), `nodes near coords` mit den Beschriftungen als symbolische Metadaten |
| Wirkungsgradkennfeld | `contour` über dem Feld (nan außerhalb der Hüllkurve) mit `clabel` | verfolgte Höhenlinien als Plots, Beschriftungen als Knoten |
| Leistungskurve (Drehmoment) | `twinx` | überlagerte Achse mit `axis y line=right` |
| Toleranzband | `fill_between` unten/oben | `fill between` der beiden Grenzkurven |
| Titel, Bildunterschrift, Teilbildtitel | `suptitle`, `supxlabel`, Texte in den Teilbildern | `title`, Knoten unter dem Bild, Knoten |

Näherungen: Layoutdetails (verkleinerte Beschriftungen, weiße Hinterlegung, Höchstbreiten von Anmerkungen, genauer
Abstand der Schraffurlinien und Pfeilspitzen) folgen dem Layout der jeweiligen Bibliothek; Schriftgrößen sind relativ
(Diagrammschrift in matplotlib, `\footnotesize` und kleiner in LaTeX). Gestapelte Balken mit negativen Werten stapelt
pgfplots in Datenreihenfolge. Die Lage von Texten, die nur im Layout existieren (Bereichshinweise der
Drehmomentkennlinie, Betriebspunkt- und Eckfrequenzbeschriftung), wird aus dem berechneten Diagramm übernommen und in
Datenkoordinaten umgerechnet. Ein Bild mit unlesbarer Spezifikation exportiert seine Meldung.

Tests (`ExportTests`, `ExportRunTests`): Jede Bibliotheksvorlage ergibt auf Englisch und Deutsch ein plausibles
Skript (ausgeglichene Klammern, jede sichtbare Kurve, jeder Balken, jede Fläche, Bezugslinie und jedes Bezugszeichen
vorhanden) und ein plausibles Dokument (ausgeglichene geschweifte Klammern, gepaarte Umgebungen, jede Farbe
definiert); dazu Prüfungen der Bildvorlagen (schraffierte Fläche mit Maske, senkrechte Markierung durch die
Teilbilder, Bezugszeichen), ausgeblendeter Teile, überschriebener Teilefarben, Dezimalkommas und Maskierung. Ist Python
installiert, werden die Skripte von Python geparst; mit matplotlib laufen sie ohne Bildschirm (Agg) und müssen jedes
Bild speichern; mit pdflatex werden die Dokumente übersetzt. Ohne diese Werkzeuge werden diese Tests übersprungen.

## Tests und Galerie

`tests/InventaSketch.Modules.Charts.Tests` prüft Registrierung, zweisprachige Namen, Parameter, Optionen und
Aktionen, Suche in beiden Sprachen, den Datenparser (Punkt- und Kommadezimalen, Tabulatoren und Excel-Kopfzeilen,
Reihen über Leerzeilen und Kopfzeilen, `|`-Zeilen, CSV, Text in Anführungszeichen, unsinnige Eingaben), Bereiche und
Listen, die Achsteilung (runde Schritte, linear mit festem Bereich und Dezimalkomma, logarithmische Dekaden und
Nebenteilstriche, SI-Beschriftung, Phasenschritte, Kategoriebänder), jede Bibliotheksvorlage und jeden nackten Typ
innerhalb seiner Box (Englisch, Deutsch, Schwarz-Weiß, gedehnt, klein und groß), gezeichnete Daten, Skalieren ändert
`Size` und nicht die Daten, Anker und Datenports, Unterscheidbarkeit in Schwarz-Weiß (Striche, Marker, Muster),
gestapelte und waagerechte Balken, logarithmische Achsen, Abschneiden, Stufen und Glättung, leere Daten, deutsche
Titel, Grenzlinien, die Formeln der Kennlinien (Bode-Antwort bei der Eckfrequenz und Steigungen, konstante Leistung,
Bestwirkungsgrad, Höhenlinienverfolgung, Betriebspunkt der Pumpe, Spalten des Toleranzbands und Polygonbeschnitt),
alle Kontextaktionen und Eingaben, den Aktionsverteiler des Kerns, den JSON-Rundlauf und den Schwarz-Weiß-SVG-Export.
Der Galerietest schreibt `gallery-charts/*.html` (jede Vorlage farbig, deutsch in Schwarz-Weiß und gedehnt) zur
Sichtprüfung. `TimingChartTests` prüft das Zeitdiagramm: jede Verlaufsart der Schreibweise, Takte, Fehlermeldungen mit
Zeile und Zeichen, Markierungen und Maßpfeile, markierte Teile, Flanken als Andockpunkte, senkrechte und schräge
Flanken, Textgröße beim Skalieren, Aktionen und Eingaben, Bereinigen, den JSON-Rundlauf und das Exportverhalten; er
schreibt `gallery/timing/*.svg`. `PresentationChartTests` prüft die Präsentationsdiagramme: Segmentwinkel mit der Summe
360° in beiden Richtungen, Prozenttexte, Beschriftungen im Segment bzw. im Ring und außen mit Führungslinie innerhalb
der Box bei mehreren Größen, Schwarz-Weiß-Muster, Winkelkonventionen und Radialmaßstab des Polardiagramms,
Knotenhöhen des Sankey-Diagramms gleich den Zu- und Abflusssummen und Bänder, die jeden Knoten genau füllen, gemeldete
Kreisläufe, Quartile, Whisker und Ausreißer einer bekannten Stichprobe, Histogrammzählungen und Sturges-Regel,
Gantt-Balken, Meilensteine und Heute-Linie, jede Aktion, das Zeichnen innerhalb der Box bei extremen Größen, den
JSON-Rundlauf, die Exportmeldungen, das Bereinigen und den Import von matplotlib-Kreis- und Polarskripten.
