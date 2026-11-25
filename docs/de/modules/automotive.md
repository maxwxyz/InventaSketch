# Modul Fahrzeuge, ADAS & HMI (`automotive`)

Reduzierte Strichzeichnungen im Patentstil für Erfindungsmeldungen zu Fahrzeugfunktionen: Fahrzeuge in mehreren
Ansichten, Fahrzeugkomponenten, Personen, Straßenumgebung, ADAS-Sensoren mit Sichtbereichen, Wahrnehmungs-Overlays,
Trajektorien und HMI-Elemente. Alle Darstellungen sind eigenständig, generisch und markenneutral.

> **Grenzen.** Das Modul zeichnet *illustrative Szenen*. Es wird nichts simuliert: Sichtbereiche erkennen nichts,
> Trajektorien werden weder geplant noch auf Fahrbarkeit geprüft, TTC- und Sicherheitsabstandswerte sind
> Beschriftungen, und Abmessungen folgen keiner Fahrzeugnorm. Die Proportionen sind plausibel, nicht vermessen.

English version: [../../en/modules/automotive.md](../../en/modules/automotive.md)

## Konventionen

- **Szenenmaßstab** – die Elemente sind im Maßstab 1:100 entworfen (10 Entwurfseinheiten = 1 m: Pkw 45 × 18,
  Fahrstreifen 35, Erwachsener 17,5 Einheiten). Draufsichten werden mit 0,6 davon eingefügt
  (`AutomotiveModule.TopScale`, 1:167 auf der Folie, 25.11.2026), damit eine Szene auf eine 16:9-Folie passt: Straße mit
  2 Fahrstreifen 96 × 42 mm, Pkw 27 mm lang, Kreuzung 96 × 96 mm, Kreisverkehr 120 × 120 mm; alle Draufsichten teilen
  den Faktor, Fahrzeuge passen also weiter in ihre Fahrstreifen. Seiten-, Vorder- und Rückansichten behalten ihre
  Entwurfsgröße. Freies gleichmäßiges/ungleichmäßiges Skalieren ändert nur die Boxgröße.
- **Eine Fahrstreifenbreite überall** – gerade Straßen, Kurven, Kreuzungen (bis drei Fahrstreifen je Richtung) und die
  Zufahrten des Kreisverkehrs haben dieselbe Fahrstreifenbreite, so dass eine Straße an jedem Arm Streifen für Streifen
  andockt (die Zufahrten des Kreisverkehrs waren vor dem 25.11.2026 schmaler).
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
| Wahrnehmung | `auto.det.bbox2d` | Bildebene | `label`, `confidence`, `showConfidence`, `dashed` |
| | `auto.det.bbox3d` | isometrisch, Draufsicht | `label`, `confidence`, `showConfidence` |
| | `auto.det.grid` (Belegungsgitter-Ausschnitt) | Draufsicht | `rows`, `cols`, `occupancy`, `seed` |
| | `auto.det.lanepoly` (**Punktobjekt**, glatte Kurve) | – | `showPoints`, `label` |
| Bewegung | `auto.trajectory` (**Punktobjekt**, Spline mit t0..tn) | – | `ticks`, `labels`, `prefix`, `corridor`, `width`, `arrow`, `dashed`, `textSize` |
| | `auto.motion.conflict` | Symbol | – |
| | `auto.motion.safetydistance` (**Punktobjekt**, Maß) | – | `label`, `offset`, `showValue`, `scale` (mm je m), `textSize` |
| | `auto.motion.ttc` (Text ersetzt die Angabe) | Symbol | `ttc` (s), `frame` |
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

## Kontextaktionen

| Objekt | Aktionen |
|---|---|
| Fahrzeuge | nächste Fahrzeugansicht (Draufsicht → Seite → Front → Heck), direkte Ansichtswahl; der Maßstab bleibt erhalten |
| Straßen | Fahrstreifen + / − (der gerade Abschnitt behält die Fahrstreifenbreite und wächst/schrumpft), nächstes Markierungsmuster, Fahrstreifenmitten; Kreuzung: Kreuzung/Einmündung, Fußgängerüberwege |
| Sichtbereich | Reichweite + / − (×1,25), Öffnungswinkel + / − (10°), um 15° drehen, nächste Füllung |
| Trajektorie | Fahrschlauch ein/aus, Fahrschlauchbreite + / −, Zeitmarken, gestrichelt (prädiziert) |
| Sonstige | Parameter-Schalter und -Wechsel (Signalzustand, Schildform, Absperrungsart, Türöffnung, Lenkwinkel, …) |

Plus-/Minus-Aktionen verwenden die Piktogramm-IDs `cmd-increase`/`cmd-decrease`, die noch nicht zum gemeinsamen
`IconIds`-Vokabular gehören (Hosts zeigen bis zur Aufnahme ein neutrales Symbol).

## Hinweise für Hosts

Alles wird aus Linien, Pfaden, Rechtecken, Ellipsen und Text aufgebaut; alle Hosts zeichnen also native, bearbeitbare
Formen. Punkt- und Schraffurfüllungen (Sichtbereich, belegte Zellen, schraffierte Flächen) folgen dem Füllmodell des
Cores (explizite Schraffurlinien im SVG-Export). Schriftgrößen werden automatisch verkleinert, damit Beschriftungen in
ihren Boxen bleiben.
