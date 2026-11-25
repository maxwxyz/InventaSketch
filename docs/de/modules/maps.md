# Modul Karten & Routen (`maps`)

Schematische Kartenskizzen für Folien: Kartenrahmen, Gebiete, Ortsbeschriftungen, Straßen, Bahnstrecken und
Gewässer, eine Route mit Wegpunkten und Richtungspfeilen, Start-/Zielmarker, nummerierte Halte, Orte von Interesse
(POI), Entfernungs-/Zeitangaben, Maßstabsleiste, Nordpfeil, Windrose und Legende. Es ist **kein GIS**: Hinter der
Zeichnung stehen keine geografischen Koordinaten, Kartenprojektionen oder importierten Kartendaten, nur gut
gezeichnete, editierbare Symbole. (Der Import von GeoJSON/GPX/KML aus der Spezifikation ist nicht Teil dieses Moduls.)

Jedes Objekt ist ohne Parameter einfügbar (Quick Sketch). Box-Symbole werden innerhalb ihrer Box gezeichnet und
dehnen sich beim freien Skalieren; Skalieren ändert `Size`, nie Parameter (eine Maßstabsleiste stellt weiter „1 km“
dar, egal wie lang sie gezeichnet ist). Linien (Straßen, Bahn, Gewässer, Routen, Gebiete) sind Punktobjekte in
Seitenkoordinaten.

## Objekttypen

| Id | Name (EN / DE) | Geometrie | Ansichten | Hinweise |
|---|---|---|---|---|
| `map.frame` | Map frame / Kartenrahmen | Box | Karte | `style` schlicht / Planquadrate / Geländeandeutung, `cells` (Gitterspalten, Zeilen folgen dem Seitenverhältnis), `labels` (A, B … / 1, 2 …), `tint` (Landfarbe); doppelter Kartenrand |
| `map.region` | Region / Gebiet | Punkte (geschlossenes Polygon) | Karte | `kind` Gebiet, Wald, See/Gewässer, Park, bebaute Fläche, Sperrzone (Geofence); `fill` Farbton / Schraffur / keine; `smooth`; Name als Text im Schwerpunkt |
| `map.place` | Place label / Ortsbeschriftung | Box | Karte | `rank` Hauptstadt, Stadt, Ort, Dorf, Ortsteil (Punkt und Schrift nach Größe), `dot`; Text = Name, ohne Text der Name der Ortsgröße in der UI-Sprache |
| `map.road` | Road / Straße | Punkte (Achse) | Karte | `kind` Autobahn (4,2 mm, Mittellinie), Hauptstraße (3 mm), Nebenstraße (2 mm), Weg (gestrichelte Linie); `smooth`, `bridge`, optional `width`; Text = Straßennummer (Schild bei Autobahn/Hauptstraße) oder Name |
| `map.rail` | Rail line / Bahnstrecke | Punkte | Karte | `kind` Eisenbahn (schwarz-weißes Band) oder Straßenbahn (Linie mit Schwellen); `smooth`; Text = Name |
| `map.river` | River / Fluss | Punkte | Karte | `kind` Fluss (Band mit Wasserlinien), Bach (Linie), Kanal (gerades Band); `smooth` (Standard an); optional `width`; kursiver Name |
| `map.route` | Route / Route | Punkte | Karte | `style` durchgezogen / gestrichelt (Alternative) / gepunktet (Fußweg), `arrows` entlang / am Ende / keine, `waypoints` (Punkte), `smooth`, `width`; weißer Halo; Text = Routenbeschriftung |
| `map.marker` | Route marker / Routenmarker | Box | Stecknadel, Symbol | `kind` Start (Startdreieck), Ziel (Zielflagge), Zwischenziel (helle Nadel); Text ersetzt das Piktogramm (z. B. „A“, „B“) |
| `map.stop` | Numbered stop / Nummerierter Halt | Box | Symbol | `number`, `shape` Kreis / Quadrat; Text ersetzt die Nummer |
| `map.callout` | Distance/time callout / Entfernungs-/Zeitangabe | Box | Karte | `distance` + `unit` (km, mi, m), `time` (Minuten, angezeigt als „1 h 35 min“), `mode` Auto / zu Fuß / Fahrrad / ÖPNV / keins, `tail` links / rechts / keiner; Text ersetzt die beiden Zeilen |
| `map.poi` | Point of interest / Ort von Interesse (POI) | Box | Symbol, Stecknadel | `kind` Ort, Gebäude, Parkplatz, Tankstelle, Ladestation, Krankenhaus, Flughafen, Hafen, Bahnhof |
| `map.scalebar` | Scale bar / Maßstabsleiste | Box | Karte | `length` + `unit` (km, mi, m, ft), `segments` (1–10), `style` Wechselbalken / Linie mit Strichen; Beschriftungen werden automatisch ausgedünnt |
| `map.north` | North arrow / Nordpfeil | Box | Karte | geteilte Pfeilspitze, `letter` „N“ |
| `map.compass` | Compass rose / Windrose | Box | Karte | `points` 8 / 4, `labels` (Deutsch: N, O, S, W) |
| `map.legend` | Legend / Legende | Box | Karte | `title` (Standard „Legend“ / „Legende“), `auto`; Einträge siehe unten |

Ansichten tragen abstrakte Icons: `plan` verwendet `IconIds.Plan`, `symbol` `IconIds.Symbol`. die Ansicht `pin`
`IconIds.Pin`.

Piktogramme (POIs, Marker, Verkehrsmittel) sind eigenständig aus einfachen Primitiven gezeichnet; es wird kein
Icon-Satz Dritter verwendet. Das Krankenhaus zeigt ein „H“, bewusst nicht das Rote-Kreuz-Zeichen (nach den Genfer
Abkommen geschützt).

## Straßen und Knotenpunkte

Eine Straße mit Fahrbahnband besteht aus einem gefüllten Polygon (Band ohne Umriss) und einem Pfad mit ihren
Randlinien und Endlinien, bleibt also bei zwei nativen Formen. Wo das Band einer anderen Straße der Seite
überlappt, werden die Randlinien genau an deren Rändern unterbrochen, und ein Ende innerhalb einer anderen Straße
bekommt keine Endlinie. Kreuzungen und Einmündungen sehen deshalb in jeder Z-Reihenfolge wie echte Knotenpunkte aus;
die Füllung der oberen Straße deckt die untere ab. Eine Straße mit `bridge` behält ihre Ränder und unterbricht keine
anderen (über die gekreuzten Straßen legen). Straßen melden `DependsOnNeighbours` und die Straßen in ihrer Nähe als
**abhängige Objekte**, damit Hosts beide Seiten eines Knotenpunkts im selben Rückgängig-Schritt neu zeichnen. Wege
(gestrichelte Linien) nehmen an Knotenpunkten nicht teil.

## Fangen und Andocken

- Straßen, Bahnstrecken, Gewässer und Routen stellen ihre Achse als `Rail`-Segmente bereit (Kurven als bis zu 24
  Sehnen).
- Marker (Nadelspitze / Symbolmitte), POIs, Halte (Mitte), Ortsbeschriftungen (Punkt) und Entfernungsangaben
  (Zeigerspitze) haben einen `Port` namens `loc` (Entfernungsangabe: `tip`).
- Beim Verschieben dockt der Port eines Markers, POIs oder Halts an beliebiger Stelle auf eine Straße, Bahn,
  ein Gewässer oder eine Route an (Rail-Regel). Ein gezogener Routen- oder Straßenpunkt dockt auf eine Achse (Rail)
  oder auf den Port eines Markers/Halts/POIs; ein auf einen Port angedockter Punkt wird angeheftet (`Attachment` mit
  Anker-Id `loc`) und folgt dem Symbol, wenn es verschoben wird.
- Gebiete bieten ihren Schwerpunkt als `Center`-Punkt an (für Beschriftungen und POIs).

## Kontextaktionen

| Typ | Aktionen |
|---|---|
| Straße | Straßenart ▸ (Autobahn → Hauptstraße → Nebenstraße → Weg; die Beschriftung nennt die nächste Art), glatte Kurve, Brücke, Punkt hinzufügen, Punkt entfernen |
| Bahn, Gewässer | Linien-/Gewässerart ▸, glatte Kurve, Punkt hinzufügen/entfernen |
| Route | Routenstil ▸ (durchgezogen → gestrichelt → gepunktet), Richtungspfeile ▸, Wegpunkte zeigen, glatte Kurve, Wegpunkt hinzufügen, Wegpunkt entfernen, Richtung umkehren |
| Gebiet | Flächenart ▸, Füllung ▸ (Farbton → Schraffur → keine), glatter Umriss, Ecke hinzufügen/entfernen |
| Marker, POI | Ansichtswechsel (Stecknadel / Symbol, Maßstab bleibt), Marker ▸ / Ortsart ▸ |
| Halt | nächste / vorige Nummer (nicht unter 0), Form ▸ |
| Entfernungsangabe | „Entfernung…“ (Eingabe, akzeptiert `12,5 km`, `8 mi`, `500 m`), „Fahrzeit…“ (Eingabe: `90`, `1:30`, `1 h 30`, `2h`, `45 min`), Einheit ▸, Verkehrsmittel ▸, Zeiger ▸ |
| Ortsbeschriftung | Ortsgröße ▸, Ortspunkt |
| Maßstabsleiste | Einheit ▸, „Länge…“ (Eingabe: `2 km`, `1,5 mi`, `500 m`, eine reine Zahl behält die Einheit), mehr / weniger Abschnitte, Stil ▸ |
| Kartenrahmen | Hintergrund ▸, Gitterbeschriftung, Landfarbe, mehr / weniger Planquadrate |
| Nordpfeil, Windrose | „N“ zeigen; Strahlen ▸, Himmelsrichtungen beschriften |
| Legende | Kartenobjekte auflisten (automatisch), „Titel…“ (Eingabe) |

*Wegpunkt hinzufügen* fügt den Mittelpunkt des längsten Abschnitts ein; *Wegpunkt entfernen* entfernt den Punkt,
dessen Wegfall die Linie am wenigsten verändert (kleinstes Dreieck mit den Nachbarn; Endpunkte offener Linien
bleiben), ein gerade eingefügter Mittelpunkt wird also wieder entfernt. Anheftungen bleiben bei ihren Punkten.
*Entfernen* ist bei zwei Punkten (Gebiete: drei) ausgegraut. Ungültige Eingaben werfen eine `FormatException` mit
zweisprachiger Meldung und ändern nichts. Schritte verwenden `IconIds.Increase` / `IconIds.Decrease`, Durchschalten
`IconIds.Cycle`, Schalter `IconIds.Toggle`, Eingaben `IconIds.Text`, Umkehren `IconIds.Swap`.

## GeoJSON und GPX importieren (25.11.2026)

„Daten importieren …“ (COM: Menü Extras; Web: Import-Knopf; Engine `importGeo`) macht aus einer GeoJSON-Datei (Feature,
FeatureCollection oder Geometrie: Linien, Polygonringe, Punkte) oder einer GPX-Datei (Tracks, Routen, Wegpunkte)
Kartenelemente, eingepasst in die Folie (um ein Zehntel eingerückt, genordet, abstandstreue Zylinderprojektion um die
mittlere Breite der Daten – passend für Stadt- und Regionalkarten, nicht für Kontinente): Jede Linie wird eine Route
(dichte Tracks auf Punkte im Abstand von 0,5 mm ausgedünnt), jeder Punkt ein Wegpunkt-Marker mit seinem Namen; eine
Datei nur mit Linien bekommt Start- und Zielmarker. Jedes Element ist eine eigene Einheit und bleibt bearbeitbar
(`Imports.ParseGeo`, `Imports.GeoElements`). In PowerPoint geprüft (Szenario `import`: ein GPX-Lauf mit Start und Ziel).

## Legende

Die Einträge stammen in dieser Rangfolge aus

1. dem **Text** des Objekts: ein Eintrag pro Zeile, optional mit Musterschlüssel in eckigen Klammern
   (`[road.motorway] A8`, `[poi.parking] P+R`; Zeilen ohne Schlüssel erhalten einen Punkt). Schlüssel: `route`,
   `route.dashed`, `route.dotted`, `road.<kind>`, `rail.<kind>`, `river.<kind>`, `region.<kind>`, `marker.<kind>`,
   `stop`, `poi.<kind>`, `place.<rank>`;
2. den **Kartenobjekten derselben Seite** (`auto`, Standard): Routen (Routentext als Beschriftung), Marker, Halte,
   POIs, Straßen, Bahn, Gewässer und Gebiete, jede Art einmal, zweisprachig beschriftet, höchstens 16;
3. einem Standardsatz (Route, Autobahn, Hauptstraße, Start, Ziel, Ort), wenn beides nicht greift.

Die Muster werden von den echten Kartentypen gezeichnet (in die Musterzelle eingepasst), die Legende passt also
immer zur Karte. Eine automatische Legende meldet `DependsOnNeighbours`; jedes Kartenobjekt meldet die automatischen
Legenden seiner Seite als abhängige Objekte, sodass die Legende bei jeder Kartenänderung neu gezeichnet wird.

## Schwarz-Weiß

Jede Vollfüllung ist eindeutig dunkel (Leuchtdichte < 0,3, wird schwarz) oder eindeutig hell (> 0,45, wird weiß):
POI-/Marker-/Halt-Symbole werden schwarz mit weißem Piktogramm, der Zwischenziel-Marker weiß mit schwarzem
Piktogramm. Linienobjekte bleiben über Breite, Strichelung und Aufbau unterscheidbar (Autobahn-Mittellinie,
Bahnband, Wasserlinien). Gebiete ersetzen ihren Farbton durch die Schraffur ihrer Art (Wald: Punkte), wenn das
Dokument oder der Render-Kontext schwarz-weiß ist.

## Fachexport: Routenhalte (CSV)

`map.route.stops` (das Modul implementiert `IExportProvider`, gilt bei ausgewählter Route). Je ausgewählter Route:
Start, jeder Routenpunkt mit Marker, Halt, POI oder Ortsbeschriftung (angedockt oder Standort innerhalb 3 mm) und das
Ende, in Routenreihenfolge, mit Name, Art, Abschnitts- und Gesamtentfernung entlang der gezeichneten Route (bei
glatter Route entlang der Kurve) und Blattkoordinaten. Entfernungen werden über die Maßstabsleiste der Seite
umgerechnet (Boxbreite = dargestellte Länge, Einheit der Leiste), sonst in Millimetern auf dem Blatt. Spalten:
`Route,Nr,Name,Art,Abschnitt (Einheit),Gesamt (Einheit),X (mm),Y (mm)` (englische Kopfzeile in englischer UI).
**Erhalten:** Reihenfolge der Halte, Namen und Arten, Entfernungen; **nicht:** geografische Koordinaten. Ein
GPX-Export wurde bewusst weggelassen: Skizzenkarten haben keine Breiten-/Längengrade, und erfundene Koordinaten wären
Scheindaten.

## Host-Hinweise

Alle Ausgaben verwenden native Primitive: Polygone und Freihandformen (Bänder, Gebiete, Nadeln, Piktogramme),
Freihandformen mit mehreren Teilpfaden für unterbrochene Ränder, Striche, Schwellen und Pfeile (Hosts ohne
Freihandformen weichen auf Linienformen aus), Ellipsen, Rechtecke, kubische Pfade (glatte Linien) und Text. Ein
Vektorbild ist nicht nötig. Gebietsschraffuren und Punktfüllungen verwenden die Füllarten des Kerns. Straßen und
automatische Legenden hängen von Nachbarn ab; im PowerPoint-COM-Host werden sie mit den anderen Einheiten der Folie
als Kontext gezeichnet (siehe Architekturüberblick).

## Tests und Galerie

`tests/InventaSketch.Modules.Maps.Tests` prüft Registrierung, zweisprachige Namen/Schlagwörter/Aktionen,
Ansichts-Icons, jedes Box-Element in jeder Ansicht innerhalb seiner Box (auch gedehnt und mit langen Texten),
Punktelemente nahe ihren Punkten, Straßenbreiten und Knotenpunkte (Kreuzung, Einmündung, Brücke), Routendarstellung
und Stil-Überschreibungen, Rails und Ports (Marker auf Straße, POI auf Route, Routenpunkt auf Straße und auf Halt mit
Anheftung), alle Kontextaktionen samt Eingaben, Legendeneinträge und Abhängigkeiten, den Routenhalte-Export, den
JSON-Rundlauf und die Schwarz-Weiß-Tauglichkeit. Der Galerietest schreibt `gallery-maps/*.html` (alle Typen,
Ansichten, gedehnte Varianten, Vorlagen und eine Beispiel-Routenkarte in Farbe, Schwarz-Weiß und Deutsch) zur
Sichtprüfung.
