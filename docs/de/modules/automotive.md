# Modul Fahrzeuge, ADAS & HMI (`automotive`)

Reduzierte Strichzeichnungen im Schwarzweiß-Strichstil für Erfindungsmeldungen zu Fahrzeugfunktionen: Fahrzeuge in
mehreren Ansichten, Fahrzeugkomponenten, Personen, Straßenumgebung, ADAS-Sensoren mit Sichtbereichen,
V2X-Funkverbindungen, Wahrnehmungs-Overlays, Trajektorien und HMI-Elemente. Alle Darstellungen sind eigenständig, generisch und markenneutral.

> **Grenzen.** Das Modul zeichnet *illustrative Szenen*. Es wird nichts simuliert: Sichtbereiche erkennen nichts,
> Trajektorien werden weder geplant noch auf Fahrbarkeit geprüft, und Abmessungen folgen keiner Fahrzeugnorm. Die
> Proportionen sind plausibel, nicht vermessen. TTC-Angabe und Sicherheitsabstand berechnen Lehrbuchwerte (Zeit bis zur
> Kollision, Anhalteweg) aus eingegebenen Geschwindigkeiten und Abständen (siehe „Werte und Prüfungen“); das sind
> Skizzenabschätzungen, keine validierte Fahrfunktion.

English version: [../../en/modules/automotive.md](../../en/modules/automotive.md)

## Konventionen

- **Szenenmaßstab** – die Elemente sind im Maßstab 1:100 entworfen (10 Entwurfseinheiten = 1 m: Pkw 45 × 18,
  Fahrstreifen 35, Erwachsener 17,5 Einheiten). Draufsichten werden mit 0,6 davon eingefügt
  (`AutomotiveModule.TopScale`, 1:167 auf der Folie, 25.11.2026), damit eine Szene auf eine 16:9-Folie passt: Straße mit
  2 Fahrstreifen 96 × 42 mm, Pkw 27 mm lang, Kreuzung 96 × 96 mm, Kreisverkehr 120 × 120 mm; alle Draufsichten teilen
  den Faktor, Fahrzeuge passen also weiter in ihre Fahrstreifen. Seiten-, Vorder- und Rückansichten behalten ihre
  Entwurfsgröße. Freies gleichmäßiges/ungleichmäßiges Skalieren ändert nur die Boxgröße.
- **Eine Fahrstreifenbreite überall** – gerade Straßen, Kurven, Kreuzungen und die Zufahrten des Kreisverkehrs haben bei
  jeder Fahrstreifenzahl dieselbe Fahrstreifenbreite, so dass eine Straße an jedem Arm Streifen für Streifen andockt (vor
  dem 25.11.2026 waren die Zufahrten des Kreisverkehrs schmaler, und Kurven und Kreuzungen verschmälerten ihre Streifen ab
  vier Fahrstreifen). Kurven und Kreuzungen wachsen mit ihren Fahrstreifen („Fahrstreifen hinzufügen“ vergrößert die Box
  mit); `ObjectType.NaturalSize` sagt dem „+“, dass dieses Wachstum keine Skalierung ist.
- **Fahrtrichtung +x** (Front rechts), y nach unten; in der Draufsicht liegt die linke Fahrzeugseite bei −y.
  Für andere Richtungen das Objekt drehen oder spiegeln.
- Jedes Objekt ist ohne Parameter einfügbar und wird in jeder Ansicht innerhalb seiner Box gezeichnet.
- Fahrzeugkarosserien sind weiß gefüllt, spätere Objekte verdecken also frühere. Für einen Schnitt/Einblick (Insasse im
  Fahrzeug) dem Fahrzeug die Stilüberschreibung *Füllung: keine* und bei Bedarf eine Strich-Zweipunkt-Linie geben.
- Linienarten nach ISO-128-Gedanken: sichtbare Kanten, schmale Linien, verdeckte Kanten (gestrichelt, z. B. Räder
  unter der Karosserie in der Draufsicht), Mittellinien (Fahrzeug- und Fahrstreifenachsen), Strich-Zweipunkt-Linien
  (Fahrstreifenpolynom, virtuelles HUD-Bild).

## Objekttypen

| Kategorie | Typ-ID | Ansichten | Parameter (alle optional) |
|---|---|---|---|
| Fahrzeuge | `auto.vehicle.car`, `.suv`, `.van`, `.bus` | Draufsicht, Seite, Front, Heck | – |
| | `auto.vehicle.tram` (Niederflur-Straßenbahn, zwei gelenkig verbundene Teile, 20 m × 2,65 m, Zweirichtungsfahrzeug, Halbscherenstromabnehmer) | Draufsicht, Seite, Front | – |
| | `auto.vehicle.truck` (Lkw), `auto.vehicle.semi` (Sattelzug) | Draufsicht, Seite, Front, Heck | – |
| | `auto.vehicle.motorcycle`, `.bicycle`, `.scooter` (E-Scooter) | Draufsicht, Seite, Front | – |
| | `auto.vehicle.trailer` | Draufsicht, Seite, Heck | – |
| | `auto.vehicle.box` (generisches Fahrzeug mit Richtungspfeil) | Draufsicht, Seite, Front | – |
| Komponenten | `auto.sub.axles` (Achsen und Räder, passend zum Pkw-Umriss) | Draufsicht, Seite | `axles` 1–5, `steer` (°), `centerline` |
| | `auto.sub.steering` | Front, Seite, Draufsicht | – |
| | `auto.sub.battery` | Draufsicht, Seite | `modules` je Reihe |
| | `auto.sub.edrive` (Motor, Getriebe, Seitenwellen) | Draufsicht, Seite | `inverter` |
| | `auto.sub.seat` | Seite, Draufsicht, Front | `headrest` |
| | `auto.sub.door` | Seite, Draufsicht (geöffnet, Schwenkbogen) | `open` (°) |
| Personen | `auto.human.standing` | Front, Seite, Draufsicht | – |
| | `auto.human.walking` | Seite, Draufsicht | – |
| | `auto.human.seated` | Seite, Draufsicht | `role` Fahrer/Beifahrer |
| | `auto.human.child` | Front, Seite | – |
| | `auto.human.wheelchair` (Person im Greifreifen-Rollstuhl, 1,3 m hoch, 1,1 m lang) | Seite, Front, Draufsicht | – |
| | `auto.human.hand` (Zeigefinger berührt) | Seite | `ripple` (Berührungsandeutung) |
| | `auto.human.head` (**Punktobjekt**: Kopfmitte → Blickziel) | Draufsicht, Seite | `size` (mm), `cone` (°) |
| Straße | `auto.road.straight` | Draufsicht | `lanes`, `markings` (gestrichelt/durchgezogen/Mittellinie durchgezogen/keine), `centerlines`, `dash`, `gap` |
| | `auto.road.curve` (90°-Abschnitt) | Draufsicht | wie oben (max. 6 Fahrstreifen) |
| | `auto.road.intersection` | Draufsicht | `lanes`, `markings`, `arms` (4 / 3 = Einmündung), `stopLines`, `crosswalks`, `centerlines` |
| | `auto.road.roundabout` | Draufsicht | `ringLanes`, `centerlines` |
| | `auto.road.crosswalk` | Draufsicht | `stripes` |
| | `auto.marking` (**Punktobjekt**, Fahrbahnmarkierung) | – | `pattern` durchgezogen/gestrichelt/doppelt, `smooth`, `dash`, `gap` |
| Verkehr | `auto.traffic.light` | Front, Draufsicht | `state` rot/gelb/grün/aus, `pole` |
| | `auto.traffic.sign` (Text = Schildinhalt) | Front, Draufsicht | `shape` Kreis/Dreieck/Rechteck/Achteck, `pole` |
| | `auto.traffic.barrier` | Draufsicht, Seite | `kind` Schutzplanke/Wand/Schranke, `posts` |
| | `auto.traffic.parking` (Text = Stellplatzbezeichnung) | Draufsicht | `charging` |
| | `auto.traffic.building` | Draufsicht, Seite | `floors` |
| | `auto.traffic.obstacle` | Draufsicht, Seite | `kind` Kiste/Leitkegel/Trümmer |
| Sensoren | `auto.sensor.camera`, `.radar`, `.lidar`, `.ultrasonic` | Symbol | – |
| | `auto.fov` (**Punktobjekt**, Sichtbereichssektor) | – | `range` (mm), `angle` (°, 360 = Vollkreis), `heading` (°), `fill`, `rings`, `axis`, `label` |
| | `auto.v2x` (**Punktobjekt**, V2X-Funkverbindung zwischen zwei Teilnehmern) | – | `kind` V2V/V2I/V2P/V2N/V2X (Standardbeschriftung), `arrows` beide/Ende/keine, `course` gestrichelt/Zickzack (Options-Piktogramme), `waves`, `label`, `textSize` |
| Wahrnehmung | `auto.det.bbox2d` | Bildebene | `label`, `confidence`, `showConfidence`, `dashed` |
| | `auto.det.bbox3d` | isometrisch, Draufsicht | `label`, `confidence`, `showConfidence` |
| | `auto.det.grid` (Belegungsgitter-Ausschnitt) | Draufsicht | `rows`, `cols`, `occupancy`, `seed` |
| | `auto.det.lanepoly` (**Punktobjekt**, glatte Kurve) | – | `showPoints`, `label` |
| Bewegung | `auto.trajectory` (**Punktobjekt**, Spline mit t0..tn) | – | `ticks`, `labels`, `prefix`, `corridor`, `width`, `arrow`, `dashed`, `textSize` |
| | `auto.motion.conflict` | Symbol | – |
| | `auto.motion.safetydistance` (**Punktobjekt**, Maß) | – | `label`, `offset`, `showValue`, `scale` (mm je m), `textSize`; Werte `speed`, `reaction`, `decel`, `gap`, `stopping`, `reactionDistance`, `brakingDistance`, `showValues` |
| | `auto.motion.ttc` (Text ersetzt die Angabe) | Symbol | `ttc` (s, ohne Vorgabewert: die Angabe zeigt 2,4 s, bis er gesetzt ist), `frame`; Werte `vEgo`, `vTarget`, `gap`, `showValues` |
| HMI | `auto.hmi.cluster` | Front | `layout` Rundinstrumente/digital, `value`, `speed` |
| | `auto.hmi.display` | Front, Seite | `content` Kacheln/Karte/leer |
| | `auto.hmi.hud` | Seite (Strahlengang, Auge, virtuelles Bild), Front (Fahrersicht, Trapez des virtuellen Bildes) | `speed` |
| | `auto.hmi.button` (Text = Beschriftung) | Front | `pressed` |
| | `auto.hmi.knob` | Front, Seite | `position` (°) |
| | `auto.hmi.slider` (waagerecht oder senkrecht nach Seitenverhältnis) | Front | `value`, `ticks` |
| | `auto.hmi.warning` (generischer Platzhalter) | Symbol | `shape` Dreieck/Kreis/Quadrat |
| | `auto.hmi.wireframe` | Front | `kind` Bild/Text/Liste/Karte |

Die Bibliothek enthält rund 140 Vorlagen (jede Ansicht jedes Fahrzeugs, sensortypische Sichtbereiche, Straße mit
Mittellinie, Einmündung, Ladeparkplatz, prädizierte Trajektorie, …). Die Suche funktioniert auf Deutsch und Englisch.

## Werte und Prüfungen (26.11.2026)

Zwei Overlays tragen Elementwerte (`IValueType`, Engineering-Berechnungen Abschnitt 1): Sie werden als Text mit Einheit
(„100 km/h“, „25 m“, „1,5 s“) in der Wertekarte oder im Inspektor eingegeben, abgeleitete Werte berechnet der
Werte-Solver, und „Werte zeigen“ (`showValues`) zeichnet die Wertebeschriftung unter das Element.

| Element | Variablen (Karte: die ersten) | Beziehungen | Prüfung |
|---|---|---|---|
| `auto.motion.ttc` | `v_ego` (km/h), `v_Ziel` (km/h), `d` Abstand (m), `TTC` (s) | `auto.ttc`: TTC = d / (v_ego − v_Ziel), nach jeder Variablen auflösbar | `auto.ttc.low` (Warnung): TTC < 2 s |
| `auto.motion.safetydistance` | `v` (km/h), `s` Anhalteweg (m), `d` Abstand (m); Panel: `t_R` Reaktionszeit (Vorgabe 1 s), `a` Verzögerung (Vorgabe 8 m/s²), `s_R` Reaktionsweg, `s_B` Bremsweg | `auto.braking.reaction`: s_R = v·t_R; `auto.braking.distance`: s_B = v²/(2a); `auto.braking.stopping`: s = s_R + s_B | `auto.braking.gap` (Warnung): s > d |

- **TTC-Angabe:** Mit eingegebenen Geschwindigkeiten und Abstand zeichnet die Angabe die berechnete TTC („TTC = 1.8 s“);
  ist das Egofahrzeug nicht schneller als das vorausfahrende, gibt es keinen Kollisionskurs („TTC = ∞“, kein Wert,
  keine Warnung). Ohne sie zeichnet sie weiter ihren Parameter `ttc` (Quick Sketch). Eine eingegebene TTC lässt den
  Solver auch eine fehlende Geschwindigkeit oder den Abstand berechnen. Gültigkeit: konstante Geschwindigkeiten,
  Folgefahrt im selben Fahrstreifen.
- **Sicherheitsabstand:** Der Abstand `d` ist die gezeichnete Länge geteilt durch `scale` (mm je m, Vorgabe 10 = 1:100),
  sofern er nicht eingegeben ist; der eigene Wertetext des Maßes („d_safe = 30.0 m“, `showValue`) zeigt den eingegebenen
  Abstand. Reaktionszeit und Verzögerung sind übliche Vorgaben (1 s, 8 m/s² Vollbremsung auf trockener Fahrbahn), keine
  Normwerte; eigene Werte eingeben. Lehrbuchprobe: 100 km/h, 1 s, 8 m/s² → s = 27,8 m + 48,2 m = 76,0 m (Unit-Test).
- **Quellen:** Die Beziehungen sind elementare Kinematik und die 2-s-Schwelle ist ein Richtwert; kein Eintrag des
  Quellenkatalogs (`ValueSources`) passt, daher wird keiner zitiert.
- Bibliotheksvorlagen: „TTC aus Abstand und Geschwindigkeiten“ (100 km/h, 60 km/h, 25 m → 2,25 s) und
  „Anhalteweg-Prüfung“ (50 km/h, Abstand 30 m, Wertebeschriftung an).

## Fangen und Anhängen

- **Fahrzeuge** haben einen `Hub`-Anker in der Mitte (`center`) und an Vorder-/Hinterachse (`axle.front`,
  `axle.rear`) sowie `Mount`-Punkte (`mount.front`, `mount.rear`, `mount.roof`, `mount.windshield`,
  `mount.mirror.left/right`, Ecken in der Draufsicht).
- **Straßen** stellen jede Fahrstreifenmitte als `Axis`-Segment bereit (Kurven und Kreisverkehrsring als Sehnen), so
  dass der Hub eines Fahrzeugs auf den Fahrstreifen schnappt, und zusätzlich als `Rail` mit derselben Geometrie
  (`lane0.rail`, …), so dass Trajektorienpunkte, Fahrstreifenpolynome und andere Linienenden an beliebiger Stelle der
  Fahrstreifenmitte andocken (Fangart `rail`). Nur Fahrstreifenenden bekommen ein „+“ (nächster Straßenabschnitt), nicht
  die Sehnenstöße von Kurven und Ring und nicht die inneren Enden von Kreuzungsarmen; das „+“ setzt die Straße Streifen
  für Streifen mit der Fahrstreifenbreite des Elements fort, auch wenn es skaliert wurde (25.11.2026). Fahrstreifenenden
  sind `End`-Punkte zum Verketten von Straßen und
  Trajektorien.
- **Sensoren** haben hinten einen `Mount`-Punkt und an der Öffnung einen `Port`. `Mount` auf `Mount` ist ein semantischer
  Fang (Fangart `mount`, Priorität wie ein Port): Der Anbaupunkt des Sensors gewinnt gegen nähere allgemeine Ecken- oder
  Mittelpunktfänge.
- **Sichtbereich:** Punkt 0 ist der Scheitel, Punkt 1 eine Richtungsreferenz; die Sektorachse zeigt von Punkt 0 zu
  Punkt 1, gedreht um `heading`. `Fov.AttachTo(doc, fovId, sensorId)` hängt beide Punkte an den Sensor (Öffnung und
  eine Boxlänge davor), so dass der Sichtbereich beim Verschieben, Drehen und Skalieren des Sensors mitgeht. Ohne
  Anhängen ist er ein freies Objekt.
- Overlays (Achsen, Batterie, E-Antriebs-Abtrieb) haben `Hub`-Anker, die konzentrisch auf Fahrzeug-Hubs schnappen.
- **V2X-Verbindung:** Beide Enden sind Punktobjekt-Enden (`p0`, `p1`), die an Fahrzeug-Hubs und Dach-Anbaupunkten,
  Sensoren und Ampeln andocken; die Funkwellenbögen sitzen am sendenden Ende (bei Verbindungen in beide Richtungen an
  beiden Enden).
- **Straßenbahn:** wie die anderen Fahrzeuge ein `Hub` in der Mitte (schnappt auf Fahrstreifenmitten), Drehgestell-Hubs
  (`axle.front`, `axle.rear`) und Anbaupunkte (`mount.front`, `mount.rear`, `mount.roof` am Stromabnehmer).

## Kontextaktionen

| Objekt | Aktionen |
|---|---|
| Fahrzeuge | Ansichtswahl (Draufsicht, Seite, Front, Heck; Ansichtsauswahl der Hosts, kein „nächste Ansicht“-Knopf); der Maßstab bleibt erhalten |
| Straßen | Fahrstreifen + / − (der gerade Abschnitt behält die Fahrstreifenbreite und wächst/schrumpft), Markierung ▾, Fahrstreifenmitten; Kreuzung: Kreuzung/Einmündung, Fußgängerüberwege |
| Sichtbereich | Reichweite + / − (×1,25), Öffnungswinkel + / − (10°), um 15° drehen, Füllung ▾ |
| Trajektorie | Fahrschlauch ein/aus, Fahrschlauchbreite + / −, Zeitmarken, gestrichelt (prädiziert) |
| V2X-Verbindung | Verbindungsart ▾, Richtung ▾, Linie ▾ (Piktogramme gestrichelt / Zickzack), Funkwellen, Richtung umkehren |
| Sonstige | Parameter-Schalter und Auswahlen ▾ (Signalzustand, Schildform, Absperrungsart, Türöffnung, Lenkwinkel, …) |

Plus-/Minus-Aktionen verwenden die Piktogramm-IDs `cmd-increase`/`cmd-decrease`, die noch nicht zum gemeinsamen
`IconIds`-Vokabular gehören (Hosts zeigen bis zur Aufnahme ein neutrales Symbol).

## Hinweise für Hosts

Alles wird aus Linien, Pfaden, Rechtecken, Ellipsen und Text aufgebaut; alle Hosts zeichnen also native, bearbeitbare
Formen. Punkt- und Schraffurfüllungen (Sichtbereich, belegte Zellen, schraffierte Flächen) folgen dem Füllmodell des
Cores (explizite Schraffurlinien im SVG-Export). Schriftgrößen werden automatisch verkleinert, damit Beschriftungen in
ihren Boxen bleiben.
