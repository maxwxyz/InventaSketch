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
| `chart.figure` | Figure (principle, patent) / Prinzip- und Patentbild | `spec` (JSON-Spezifikation, siehe [Bild](#bild-diagramme-v2-stufe-2)); gestapelte Panels, Prinzip- oder Datenachsen, Wertlinien, Markierungen, Flächen, Anmerkungen, Bezugszeichen |

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

Die Bibliothek enthält 28 Vorlagen (`chart.line`, `chart.line.markers`, `chart.line.multi`, `chart.line.smooth`,
`chart.line.logx`, `chart.scatter`, `chart.step`, `chart.column`, `chart.column.grouped`, `chart.column.stacked`,
`chart.bar.horizontal`, `chart.axes`, `chart.axes.plain`, `chart.axes.loglog`, `chart.limits`, `chart.torque`,
`chart.torque.plain`, `chart.efficiency`, `chart.pump`, `chart.fan`, `chart.bode`, `chart.bode.lowpass1`,
`chart.bode.highpass1`, `chart.band`, `chart.band.measured`, `chart.figure.phases`, `chart.figure.timing`, `chart.figure.temperature`) in drei Kategorien: Datendiagramme, Kennlinien &
Kennfelder sowie Prinzip- und Patentbilder.

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

## Bild (Diagramme v2, Stufe 2)

Der Diagrammtyp `chart.figure` zeichnet Prinzip- und Patentbilder aus einer **versionierten JSON-Spezifikation** im
Parameter `spec` (ohne Parameter eingefügt zeigt er das Patentbild unten). Kategorie „Prinzip- und Patentbilder“ mit
drei Vorlagen, den Prüfbildern aus dem [Entwurf Diagramme v2](../design/charts-v2.md):

| Id | Name | Zeigt |
|---|---|---|
| `chart.figure.phases` | Phasen mit Rotortemperatur (Patentbild) | Leistung P mit den Phasen „Vorkonditionierung“ und „Volllastphase“ (Phasenlinien t0, t1 durch beide Panels); Rotortemperatur ohne (gestrichelt) und mit Vorkonditionierung (durchgezogen) bis zur Grenztemperatur, Fläche dazwischen schraffiert, Zeitgewinn als Maßpfeil mit Hilfslinien, Bezugszeichen 40–46; Prinzipachsen, schwarz-weiß |
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
