# Modul Diagramme & Kennlinien (`charts`)

Technische Diagramme für Folien: XY-Linien-, Streu- und Stufendiagramme, Säulen-/Balkendiagramme, leere Achsen mit
Gitter, die Drehmoment-Drehzahl-Kennlinie eines Motors, ein Wirkungsgradkennfeld, eine Pumpen-/Ventilatorkennlinie mit
Betriebspunkt, ein Bode-Diagramm und ein Toleranzband. Jedes Diagramm besteht aus nativen Primitiven (Linien,
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
| `chart.math.axes` | Coordinate system (2D) / Koordinatensystem (2D) | Mathematikstil, siehe [Koordinatensysteme und Funktionsgraphen](#koordinatensysteme-und-funktionsgraphen-diagramme-v2-stufe-4) |
| `chart.math.function` | Function graph / Funktionsgraph | Grundfunktion, eingegebene Formel oder Funktionsgenerator im Koordinatensystem im Mathematikstil |

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

Die Bibliothek enthält 37 Vorlagen (`chart.line`, `chart.line.markers`, `chart.line.multi`, `chart.line.smooth`,
`chart.line.logx`, `chart.scatter`, `chart.step`, `chart.column`, `chart.column.grouped`, `chart.column.stacked`,
`chart.bar.horizontal`, `chart.axes`, `chart.axes.plain`, `chart.axes.loglog`, `chart.line.break`, `chart.limits`, `chart.torque`,
`chart.torque.plain`, `chart.efficiency`, `chart.pump`, `chart.fan`, `chart.bode`, `chart.bode.lowpass1`,
`chart.bode.highpass1`, `chart.band`, `chart.band.measured`, `chart.figure.phases`, `chart.figure.timing`, `chart.figure.temperature`, die acht unten genannten Koordinatensysteme und Funktionsgraphen) in vier Kategorien:
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
| XY-Diagramm | „Daten…“ (Eingabe), Darstellung ▸ (Linie → Linie mit Markern → Punkte → Stufen), geglättete Kurve, x-Skala ▸, y-Skala ▸, Gitternetz, Legende ▸ |
| Balkendiagramm | „Daten…“, Ausrichtung ▸, mehrere Reihen ▸ (gruppiert / gestapelt), Wertbeschriftung, Gitternetz, Legende ▸ |
| Achsen | Gitternetz, Pfeilspitzen, x-Skala ▸, y-Skala ▸ |
| Drehmomentkennlinie | Leistungskurve, Bereichshinweise, Gitternetz, Legende ▸ |
| Wirkungsgradkennfeld | „Höhenlinien…“ (Eingabe), Beschriftung der Linien, Gitternetz |
| Pumpen-/Ventilatorkennlinie | Maschine ▸ (Pumpe / Ventilator), „Förderstrom im Betriebspunkt…“ (Eingabe), Anlagenkennlinie, Gitternetz, Legende ▸ |
| Bode-Diagramm | System ▸, „Eckfrequenz…“ (Eingabe: `1 kHz`, `250 Hz`, `2.5k`, `2,5k`), Anzeigen ▸, Eckfrequenz markieren, Gitternetz |
| Toleranzband | „Daten…“, „Toleranz…“ (`10 %` = Prozent, `0,5` = absolut), geglättete Kurven, Gitternetz, Legende ▸ |

Ungültige Eingaben werfen eine `FormatException` mit zweisprachiger Meldung und ändern nichts. Zyklen verwenden
`IconIds.Cycle`, Schalter `IconIds.Toggle`, Eingaben `IconIds.Text`. Die Diagrammtypen haben keine Ansichtsumschaltung.

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
- Teilaktionen: die Diagrammaktionen, die das Teil betreffen (Kurve: Daten, Darstellung, Glättung; Achse: Skala;
  Gitter: Gitter ein/aus; Legende: Lage), danach ein-/ausblenden, dicker, dünner, Strichart und zurücksetzen.
- PowerPoint: Ein Klick ohne Ziehen auf das ausgewählte Diagramm wählt das Teil unter dem Mauszeiger (blau umrandet);
  die Leiste zeigt seinen Namen und seine Aktionen, „Teile …“ listet alle Teile, auch ausgeblendete, „Ganzes Element“
  verlässt das Teil. Eine Kurve mit PowerPoints eigenen Werkzeugen formatieren (Linienfarbe, Stärke, Füllung, Schrift)
  ändert nur dieses Teil und bleibt bei jedem Neuzeichnen erhalten; das ganze Diagramm formatieren ändert wie bisher
  den Elementstil.
- Engine: `parts`, `partAt`, `partActions`, `applyPartAction`, `setPartLook` (siehe Entwurf
  [Diagramme v2](../design/charts-v2.md)).

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
(JSON)…“ (Eingabe). Teilaktionen: Kurven – Strichart, Marker, geglättete Kurve, Fläche unter der Kurve schraffieren;
Wertlinien und Markierungen – Strichart bzw. gefüllter Kreis, „Beschriftung…“, entfernen; Flächen – Füllung,
entfernen; Anmerkungen – „Text…“, entfernen; Achsen – Prinzipachsen, Wertlinie in diesem Panel bzw. senkrechte
Markierung hinzufügen. Entfernen verschiebt die Indizes späterer Elemente (deren Teil-Aussehen gilt dann für das
nächste Element).

Hinweise zu den Hosts: alles nativ (Linien, Freihandformen, schraffierte Freihandformen, Text); kein Vektorbild.
Erweiterungen des Rahmens (additiv): Höhenanteile der Panels, eigene Teilstriche (Marken), Achspfeile und Gitter je
Panel, Panelabstand, Bildunterschrift, zusätzlicher rechter Rand und Achstexte. Noch nicht: Datentabellen-Editor
(Portierung von Slidemax `ChartSheet`), Funktionsgeneratoren und Skizzenkurven (Stufe 3), Achsbrüche.

## Hinweise zu den Hosts

Die gesamte Ausgabe besteht aus nativen Primitiven: eine Freihandform je Reihenlinie (geglättete Kurven als
kubische Bézierpfade), je eine Freihandform mit mehreren Teilpfaden für Gitterlinien, Teilstriche, Höhenlinien einer
Stufe, Grenz- und Hilfslinien, eine Freihandform je Markersatz, Rechtecke für Balken, Legende und
Beschriftungshintergründe, ein geschlossenes Polygon für das Band sowie Text. Die Titel der y-Achsen sind um −90°
gedreht. Schraffur- und Punktfüllungen nutzen die Füllarten des Kerns. Ein Vektorbild ist nicht nötig. Diagramme
hängen nicht von Nachbarobjekten ab.

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
Sichtprüfung.
