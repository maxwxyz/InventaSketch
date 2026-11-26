# Modul Karten & Routen (`maps`)

Schematische Kartenskizzen für Folien: Kartenrahmen, Gebiete, Höhenlinien, Ortsbeschriftungen, Straßen, Bahnstrecken
und Gewässer, Stationen auf Linien, Brücken und Tunnel auf Straßen, eine Route mit Wegpunkten, Richtungspfeilen und
Werten (Länge im Kartenmaßstab, Geschwindigkeit, Reisezeit, Anstieg und Abstieg aus Höhenlinien), Start-/Zielmarker,
nummerierte Halte, Orte von Interesse (POI), Entfernungs-/Zeitangaben, Entfernungsmessungen mit Kurswinkel, ein
Höhenprofil, Gebietsflächen, Maßstabsleiste, Nordpfeil, Windrose und Legende. Es ist **kein GIS**: Hinter der
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
| `map.region` | Region / Gebiet | Punkte (geschlossenes Polygon) | Karte | `kind` Gebiet, Wald, See/Gewässer, Park, bebaute Fläche, Sperrzone (Geofence); `fill` Farbton / Schraffur / keine; `smooth`; Name als Text im Schwerpunkt; Werte `area`, `perimeter`, `scale`, `showValues` (siehe „Messungen“) |
| `map.place` | Place label / Ortsbeschriftung | Box | Karte | `rank` Hauptstadt, Stadt, Ort, Dorf, Ortsteil (Punkt und Schrift nach Größe), `dot`; Text = Name, ohne Text der Name der Ortsgröße in der UI-Sprache |
| `map.road` | Road / Straße | Punkte (Achse) | Karte | `kind` Autobahn (4,2 mm, Mittellinie), Hauptstraße (3 mm), Nebenstraße (2 mm), Weg (gestrichelte Linie); `smooth`, `bridge`, optional `width`; Text = Straßennummer (Schild bei Autobahn/Hauptstraße) oder Name |
| `map.rail` | Rail line / Bahnstrecke | Punkte | Karte | `kind` Eisenbahn (schwarz-weißes Band) oder Straßenbahn (Linie mit Schwellen); `smooth`; Text = Name |
| `map.river` | River / Fluss | Punkte | Karte | `kind` Fluss (Band mit Wasserlinien), Bach (Linie), Kanal (gerades Band); `smooth` (Standard an); optional `width`; kursiver Name |
| `map.route` | Route / Route | Punkte | Karte | `style` durchgezogen / gestrichelt (Alternative) / gepunktet (Fußweg), `arrows` entlang / am Ende / keine, `waypoints` (Punkte), `smooth`, `width`; weißer Halo; Text = Routenbeschriftung; Werte: `mode` zu Fuß / Fahrrad / Auto, `speed`, `length`, `time`, `scale` (1:N), `showValues` (siehe „Routenwerte“); Anstieg, Abstieg und größte Steigung aus den gekreuzten Höhenlinien (siehe „Messungen“) |
| `map.measure` | Distance measure / Entfernungsmessung | Punkte (2) | Karte | gestrichelte Linie mit Pfeilspitzen und Endstrichen, beschriftet mit der Luftlinie im Kartenmaßstab („1,25 km“; ohne Maßstab „ohne Maßstab“); `showBearing` ergänzt den Kurswinkel („· 143°“); Text ersetzt die Beschriftung; Werte `distance`, `scale`; Enden docken an Markern, POIs und Ortspunkten an |
| `map.profile` | Elevation profile / Höhenprofil | Box | Karte | Höhe über der Strecke entlang einer Route, aus den gekreuzten Höhenlinien; `route` (Objekt-ID, leer: die erste Route des Blatts, die zwei Höhenlinien kreuzt), `fill`; ohne solche Route leere Achsen mit Hinweis |
| `map.station` | Station / Station | Box | Symbol | `kind` Bahnhof (weiße Scheibe, schwarzer Ring), Umsteigebahnhof (weiße Kapsel), Straßenbahnhaltestelle (kleiner Ring), Bushaltestelle (dunkles Symbol mit Bus-Piktogramm); `name`; Text = Stationsname (Standard: die Art in der UI-Sprache), über der Linie rechts vom Symbol |
| `map.bridge` | Bridge / Brücke | Punkte (Abschnitt einer Straße) | Karte | folgt der Straße unter ihren Endpunkten (auch um Kurven) mit deren Breite und Farben: Fahrbahn, Ränder und Brüstungen mit ausgestellten Flügeln; optional `width` (ohne Straße: 3 mm); Text = Name |
| `map.tunnel` | Tunnel / Tunnel | Punkte (Abschnitt einer Straße) | Karte | wie die Brücke: der Abschnitt wird in Landfarbe abgedeckt, Ränder gestrichelt, Portalzeichen an beiden Enden; optional `width`; Text = Name |
| `map.contour` | Contour line / Höhenlinie | Punkte (offen oder geschlossen) | Karte | `elevation` (m), `index` (Zählkurve, kräftiger), `smooth` (Standard an), `closed` (Kuppe oder Senke), `label` (Höhe aufrecht in eine Lücke der Linie geschrieben) |
| `map.marker` | Route marker / Routenmarker | Box | Stecknadel, Symbol | `kind` Start (Startdreieck), Ziel (Zielflagge), Zwischenziel (helle Nadel); Text ersetzt das Piktogramm (z. B. „A“, „B“) |
| `map.stop` | Numbered stop / Nummerierter Halt | Box | Symbol | `number`, `shape` Kreis / Quadrat; Text ersetzt die Nummer |
| `map.callout` | Distance/time callout / Entfernungs-/Zeitangabe | Box | Karte | `distance` + `unit` (km, mi, m), `time` (Minuten, angezeigt als „1 h 35 min“), `mode` Auto / zu Fuß / Fahrrad / ÖPNV / keins, `tail` links / rechts / keiner; Text ersetzt die beiden Zeilen |
| `map.poi` | Point of interest / Ort von Interesse (POI) | Box | Symbol, Stecknadel | `kind` Ort, Gebäude, Parkplatz, Tankstelle, Ladestation, Krankenhaus, Flughafen, Hafen, Bahnhof |
| `map.scalebar` | Scale bar / Maßstabsleiste | Box | Karte | `length` + `unit` (km, mi, m, ft), `segments` (1–10), `style` Wechselbalken / Linie mit Strichen; Beschriftungen werden automatisch ausgedünnt; Werte: Kartenmaßstab 1:N, Prüfung gegen den Planmaßstab (siehe „Messungen“) |
| `map.north` | North arrow / Nordpfeil | Box | Karte | geteilte Pfeilspitze, `letter` „N“ |
| `map.compass` | Compass rose / Windrose | Box | Karte | `points` 8 / 4, `labels` (Deutsch: N, O, S, W) |
| `map.legend` | Legend / Legende | Box | Karte | `title` (Standard „Legend“ / „Legende“), `auto`; Einträge siehe unten |

Ansichten tragen abstrakte Icons: `plan` verwendet `IconIds.Plan`, `symbol` `IconIds.Symbol`. die Ansicht `pin`
`IconIds.Pin`.

Piktogramme (POIs, Marker, Verkehrsmittel) sind eigenständig aus einfachen Primitiven gezeichnet; es wird kein
Icon-Satz Dritter verwendet. Das Krankenhaus zeigt ein „H“, bewusst nicht das Rote-Kreuz-Zeichen (nach den Genfer
Abkommen geschützt).

## Routenwerte und Kartenmaßstab (26.11.2026)

Eine Route ist ein Werteelement (`IValueType`): **Routenlänge `s`** (km), **mittlere Geschwindigkeit `v`** (km/h) und
**Reisezeit `t`** (min) mit der Beziehung `map.route.time`: t = s / v (Definition der mittleren Geschwindigkeit, Quelle
S2), nach jeder der drei auflösbar. „Werte zeigen“ (`showValues`, Bibliothekseintrag „Route mit Länge und Reisezeit“)
zeichnet „s = 4,72 km, v = 50 km/h, t = 5,66 min“ unter die Route.

- **Länge:** die gezeichnete Länge (bei glatter Route entlang der Kurve) mal Kartenmaßstab, sofern keine Länge
  eingegeben ist (eine Skizze ist selten maßstäblich; die eingegebene Straßenentfernung gewinnt). Die Karte hat keinen
  eigenen Maßstab; er kommt in dieser Reihenfolge aus dem Parameter `scale` der Route (Kartenmaßstab 1:N), aus der
  **ersten Maßstabsleiste der Seite** (ihre Boxbreite stellt ihre Länge dar; km, mi, m und ft werden umgerechnet) oder
  aus dem Planmaßstab des Dokuments. Ohne all das bleibt die Länge (und damit die Reisezeit) unbekannt, bis sie
  eingegeben wird.
- **Geschwindigkeit:** die Vorgabe des Verkehrsmittels `mode` – zu Fuß 5 km/h, Fahrrad 15 km/h, Auto 50 km/h (übliche
  Planungswerte, keine Norm) –, sofern keine Geschwindigkeit eingegeben ist oder eine eingegebene Zeit mit bekannter
  Länge sie bestimmt. „Fußweg (gepunktet)“ ist auf zu Fuß voreingestellt, „Radroute“ auf Fahrrad.
- **Reisezeit:** abgeleitet; eine eingegebene Zeit ergibt die Geschwindigkeit (oder mit der Vorgabe des Verkehrsmittels
  die Länge).
- Die Route hängt von Nachbarn ab (sie liest die Maßstabsleiste); die Maßstabsleiste meldet die Routen ihrer Seite, die
  ihre Werte zeigen, als abhängige Objekte (`HasDependents`), sodass eine geänderte Leiste deren Beschriftung neu zeichnet.
- Das Verkehrsmittel ist eine Auswahl in der schwebenden Leiste, ein Dropdown mit Options-Piktogrammen aus dem
  gemeinsamen `IconIds`-Vokabular: `travel-walk` (Fußgänger), `travel-bike` (Fahrrad), `travel-car` (Auto); das
  Verkehrsmittel der Entfernungsangabe nutzt zusätzlich `travel-transit` (Front eines Busses oder einer Straßenbahn) und
  `travel-none` (Kreis mit Schrägstrich). Die COM- und Web-Add-ins zeichnen sie; LibreOffice zeigt die Optionsnamen.

## Messungen: Fläche, Entfernung, Höhe und Maßstab (Runde 7, 26.11.2026)

Alle Längen kommen aus der Zeichnung im Kartenmaßstab (`scale` der Route, die erste Maßstabsleiste auf dem Blatt oder
der Planmaßstab des Dokuments, wie bei Routen), sofern sie nicht eingegeben sind; ohne Maßstab bleiben sie unbekannt.

| Element | Variablen | Beziehungen (aus der Zeichnung) | Prüfung |
|---|---|---|---|
| `map.region` | `A` Fläche (km²), `U` Umfang (km) | `map.region.area`: gezeichnete Fläche · (m/mm)²; `map.region.perimeter`: gezeichneter Umfang · m/mm (bei `smooth` der geglättete Umriss) | – |
| `map.measure` | `d` Luftlinie (km), `θ` Kurswinkel (°, nur lesbar) | `map.measure.distance`: gezeichnete Länge · m/mm; `map.measure.bearing`: von Norden im Uhrzeigersinn | – |
| `map.route` | Panel, nur lesbar: `Δh↑` Anstieg, `Δh↓` Abstieg (m), `q_max` größte Steigung (%) | `map.route.ascent`, `map.route.descent`: Σ Anstiege und Abstiege zwischen aufeinanderfolgenden Höhenlinienkreuzungen; `map.route.gradient`: max \|Δh\|/Δs (braucht einen Maßstab) | – |
| `map.scalebar` | `N` Kartenmaßstab 1:N (nur lesbar); Panel: `N_Plan` Planmaßstab des Dokuments | `map.scalebar.scale`: dargestellte Länge / gezeichnete Breite; `map.scalebar.plan` | `map.scale.plan` (Hinweis): \|N/N_Plan − 1\| > 2 % |

- **Gebiet:** „Gebiet mit Fläche und Umfang“ zeigt „A = 1,34 km², U = 4,68 km“ unter dem Gebiet. Eine eingegebene
  Fläche oder ein eingegebener Umfang hat Vorrang vor der Zeichnung.
- **Entfernungsmessung:** zwei Punkte, deren Enden an Markern, POIs und Ortspunkten andocken und ihnen folgen. Norden ist
  die Blattoberkante oder, wenn er gedreht ist, die Richtung des ersten Nordpfeils auf dem Blatt. „Entfernung und
  Kurswinkel“ zeigt beides.
- **Höhe aus Höhenlinien:** Jede Kreuzung der Routenmittellinie mit einer Höhenlinie des Blatts liefert dort die Höhe
  der Höhenlinie. Anstieg und Abstieg summieren die Anstiege und Abstiege zwischen aufeinanderfolgenden Kreuzungen; eine
  Kuppe zwischen zwei Kreuzungen derselben Höhenlinie zählt nicht, die Werte sind also Untergrenzen. Die größte Steigung
  teilt die Höhenstufe durch den Abstand der Kreuzungen im Kartenmaßstab. Höhenlinien melden die Routen mit
  Wertebeschriftung und die Profile des Blatts als abhängig; Routen melden die Profile.
- **Höhenprofil:** Höhe (niedrigste und höchste gekreuzte Höhenlinie) über der Strecke entlang der Route (0 bis zur
  Routenlänge im Kartenmaßstab), Punkte an den Kreuzungen, helle Fläche darunter, „↑ 100 m ↓ 100 m“ in der Ecke;
  Achsbeschriftungen in der Sprache der Oberfläche. Es hängt von seinen Nachbarn ab; Maßstabsleiste, Route und
  Höhenlinien zeichnen es neu.
- **Maßstabsleiste:** Für Kartenlängen hat die Maßstabsleiste Vorrang vor dem Planmaßstab; der Hinweis meldet, wenn
  beide nicht übereinstimmen.
- **Quellen:** Geometrie (Fläche, Umfang, Entfernung) und die Definition der Steigung; keine Katalogquelle zitiert.

## Stationen, Brücken, Tunnel und Höhenlinien (26.11.2026)

- **Station** (`map.station`): Die Symbolmitte ist ein `Port` (`loc`), der an beliebiger Stelle auf eine Bahnstrecke,
  Straße oder Route andockt; der Name steht über der Linie, damit sie ihn nicht kreuzt. Stationen auf einer Route zählen
  im Routenhalte-Export als Halte (Art „Station: Bahnhof“ …).
- **Brücke / Tunnel** (`map.bridge`, `map.tunnel`): zwei Punkte (bei gekrümmten Abschnitten mehr), deren Enden auf eine
  Straße andocken (Rail-Regel). Der Abschnitt folgt der Achse der Straße darunter zwischen den Lotfußpunkten seiner
  Enden und übernimmt ihre Breite (Autobahn 4,2 mm, Hauptstraße 3 mm …). Die Brücke zeichnet die Fahrbahn in der
  Straßenfarbe mit den Randlinien der Straße, sodass die Straße über ein darüber gezeichnetes Gewässer oder eine Bahn
  führt, dazu Brüstungen 0,5 mm außerhalb der Ränder mit ausgestellten Flügeln. Der Tunnel deckt den Abschnitt in
  Landfarbe ab (`MapTags.Mask`, bleibt bei Stil-Überschreibungen erhalten), zeichnet gestrichelte Ränder und an beiden
  Enden einen Portalbalken mit Ohren. Beide hängen von Nachbarn ab; eine Straße meldet die Brücken und Tunnel in ihrer
  Nähe als abhängige Objekte. (Der Schalter `bridge` der Straße bleibt für eine Straße über andere Straßen.)
- **Höhenlinie** (`map.contour`): glatte Punktlinie in Geländefarbe, offen oder geschlossen; die Höhe („250“, ohne
  Einheit wie auf Karten) steht aufrecht in einer Lücke in der Mitte der Linie (die Linie wird um die Beschriftung
  geschnitten, eine native Freihandform). Die Linie ist eine Rail. Bibliothek: Höhenlinie, Zählkurve (500 m), Kuppe
  (geschlossene Höhenlinie).

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
- Stationen bieten ihre Symbolmitte als `Port` `loc` an, Brücken und Tunnel ihre Enden als `End`-Punkte, Höhenlinien
  ihre Linie als `Rail`-Segmente.

## Kontextaktionen

| Typ | Aktionen |
|---|---|
| Straße | Straßenart ▾ (Auswahl: Autobahn, Hauptstraße, Nebenstraße, Weg; die aktuelle markiert), glatte Kurve, Brücke |
| Bahn, Gewässer | Linien-/Gewässerart ▸, glatte Kurve |
| Route | Routenstil ▸ (durchgezogen → gestrichelt → gepunktet), Richtungspfeile ▸, Verkehrsmittel ▾ (zu Fuß, Fahrrad, Auto), Wegpunkte zeigen, glatte Kurve, Richtung umkehren |
| Station | Name zeigen, Stationsart ▾ |
| Höhenlinie | „Höhe…“ (Eingabe: `250`, `1250,5 m`), Zählkurve, Höhenangabe, glatte Kurve |
| Gebiet | Flächenart ▸, Füllung ▸ (Farbton → Schraffur → keine), glatter Umriss |
| Marker, POI | Marker ▸ / Ortsart ▸ |
| Halt | Nummer +1 / −1 (nicht unter 0), Form ▾ |
| Entfernungsangabe | „Entfernung…“ (Eingabe, akzeptiert `12,5 km`, `8 mi`, `500 m`), „Fahrzeit…“ (Eingabe: `90`, `1:30`, `1 h 30`, `2h`, `45 min`), Einheit ▸, Verkehrsmittel ▸, Zeiger ▸ |
| Ortsbeschriftung | Ortsgröße ▸, Ortspunkt |
| Maßstabsleiste | Einheit ▸, „Länge…“ (Eingabe: `2 km`, `1,5 mi`, `500 m`, eine reine Zahl behält die Einheit), mehr / weniger Abschnitte, Stil ▸ |
| Kartenrahmen | Hintergrund ▸, Gitterbeschriftung, Landfarbe, mehr / weniger Planquadrate |
| Nordpfeil, Windrose | „N“ zeigen; Strahlen ▸, Himmelsrichtungen beschriften |
| Legende | Kartenobjekte auflisten (automatisch), „Titel…“ (Eingabe) |

Jedes Ding wird einmal angeboten (Bedienkonzept, Abschnitt 9 Punkt 4): Punkte von Straßen, Bahnen, Gewässern, Routen
und Gebieten fügt man mit den Eckpunkt-Punkten des Hosts hinzu (Klick oder Ziehen auf einem Abschnitt) und entfernt
sie mit den Eckbefehlen (Ecke auswählen: „Entfernen“, oder Doppelklick); die Ansichten von Markern und POIs (Stecknadel
/ Symbol) sind die Ansichtsauswahl des Hosts, die den Zeichenmaßstab über die Entwurfsgrößen der Ansichten behält
(`ViewDef.Size`, `ObjectType.WithView`). Die früheren Aktionen „Punkt hinzufügen/entfernen“ und `view.*` wiederholten
sie und wurden entfernt. Ungültige Eingaben werfen eine `FormatException` mit
zweisprachiger Meldung und ändern nichts. Schritte verwenden `IconIds.Increase` / `IconIds.Decrease`, Auswahlen je Option
`IconIds.Option` (`ActionChoice`), Schalter `IconIds.Toggle`, Eingaben `IconIds.Text`, Umkehren `IconIds.Swap`.

## GeoJSON und GPX importieren (25.11.2026)

„Importieren …“ (COM: Gruppe Prüfen; Web: Import-Knopf; Engine `importGeo`) macht aus einer GeoJSON-Datei (Feature,
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
   `stop`, `station.<kind>`, `bridge`, `tunnel`, `contour`, `poi.<kind>`, `place.<rank>`;
2. den **Kartenobjekten derselben Seite** (`auto`, Standard): Routen (Routentext als Beschriftung), Marker, Halte,
   Stationen, POIs, Straßen, Brücken, Tunnel, Bahn, Gewässer, Gebiete und Höhenlinien, jede Art einmal, zweisprachig
   beschriftet, höchstens 16;
3. einem Standardsatz (Route, Autobahn, Hauptstraße, Start, Ziel, Ort), wenn beides nicht greift.

Die Muster werden von den echten Kartentypen gezeichnet (in die Musterzelle eingepasst), die Legende passt also
immer zur Karte. Eine automatische Legende meldet `DependsOnNeighbours`; jedes Kartenobjekt meldet die automatischen
Legenden seiner Seite als abhängige Objekte, sodass die Legende bei jeder Kartenänderung neu gezeichnet wird.

## Schwarz-Weiß

Jede Vollfüllung ist eindeutig dunkel (Leuchtdichte < 0,3, wird schwarz) oder eindeutig hell (> 0,45, wird weiß):
POI-/Marker-/Halt-Symbole werden schwarz mit weißem Piktogramm, der Zwischenziel-Marker weiß mit schwarzem
Piktogramm. Linienobjekte bleiben über Breite, Strichelung und Aufbau unterscheidbar (Autobahn-Mittellinie,
Bahnband, Wasserlinien). Gebiete ersetzen ihren Farbton durch die Schraffur ihrer Art (Wald: Punkte), wenn das
Dokument oder der Render-Kontext schwarz-weiß ist. Die Tunnelabdeckung ist hell (wird weiß); Brückenbrüstungen und
Tunnelportale sind dunkle Linien.

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
als Kontext gezeichnet (siehe Architekturüberblick). Auch Routen (sie lesen die Maßstabsleiste), Brücken und Tunnel
(sie lesen die Straße darunter) hängen von Nachbarn ab. Die Tunnelabdeckung verwendet die Standard-Landfarbe; auf
einem Kartenrahmen mit anderem `tint` oder über einem Gebiet erscheint sie als helles Band.

## Tests und Galerie

`tests/InventaSketch.Modules.Maps.Tests` prüft Registrierung, zweisprachige Namen/Schlagwörter/Aktionen,
Ansichts-Icons, jedes Box-Element in jeder Ansicht innerhalb seiner Box (auch gedehnt und mit langen Texten),
Punktelemente nahe ihren Punkten, Straßenbreiten und Knotenpunkte (Kreuzung, Einmündung, Brücke), Routendarstellung
und Stil-Überschreibungen, Rails und Ports (Marker auf Straße, POI auf Route, Routenpunkt auf Straße und auf Halt mit
Anheftung), alle Kontextaktionen samt Eingaben, Legendeneinträge und Abhängigkeiten, den Routenhalte-Export, den
JSON-Rundlauf und die Schwarz-Weiß-Tauglichkeit; Routenwerte (Länge aus der Maßstabsleiste, eigener Maßstab und
Planmaßstab, Einheitenumrechnung, glatte Routen, Vorrang eingegebener Werte, Vorgaben der Verkehrsmittel,
Wertebeschriftung, Abhängigkeiten der Maßstabsleiste, Rundlauf), Stationen (Andocken, Arten, Namen), Brücken und
Tunnel (folgen einer abknickenden Straße, Breite, Fahrbahnfarbe, Abdeckung, Portale, Abhängigkeiten), Höhenlinien
(Lücke und Beschriftung, Zählkurve, geschlossen) und die neuen Legendenschlüssel. `structures.html`/`.svg` in
`gallery-maps/` zeigt sie in einer Beispielkarte. Der Galerietest schreibt `gallery-maps/*.html` (alle Typen,
Ansichten, gedehnte Varianten, Vorlagen und eine Beispiel-Routenkarte in Farbe, Schwarz-Weiß und Deutsch) zur
Sichtprüfung.
