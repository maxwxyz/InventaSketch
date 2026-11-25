# Modul Optik (`optics`)

Bibliothek: `src/InventaSketch.Modules.Optics` · Tests: `tests/InventaSketch.Modules.Optics.Tests` ·
Englische Fassung: [../../en/modules/optics.md](../../en/modules/optics.md)

Das Modul liefert optische Elemente für technische Abbildungen und Lehrfolien – Linsen, Spiegel, Prismen, Strahlteiler,
Blenden, Gitter, Filter, einen Lichtwellenleiter, Lichtquellen, Detektoren, einen Schirm, einen Gegenstandspfeil und die
optische Achse – sowie **Lichtstrahlen, die live durch die Elemente verfolgt werden**. Alle Symbole sind eigenständig
gezeichnet; es werden keine genormten Symbolgrafiken oder Tabellen übernommen und keine Normkonformität behauptet.

Es ist ein **Skizzen**-Modul: Die Strahlverfolgung ist zweidimensional und verwendet ideale (paraxiale) Linsen und
gekrümmte Spiegel. Optische Systeme werden nicht ausgelegt oder optimiert (keine Abbildungsfehler, Dicklinsendaten oder
Radiometrie).

## Konventionen

- **Quick Sketch zuerst:** Jedes Bibliothekselement wird ohne Parameter eingefügt. Freies (gleichmäßiges oder
  ungleichmäßiges) Skalieren ändert nur `Size`. Die Boxhöhe von Linse, Spiegel, Blende, Gitter oder Filter ist seine
  Apertur.
- **Optische Achse = lokale x-Achse** jedes Elements (Licht von links läuft in +x-Richtung). Zum Ausrichten das Element
  drehen oder spiegeln; Spiegel reflektieren auf ihrer lokalen −x-Seite (schraffierte Rückseite auf +x).
- **Brennweiten im Zeichnungsmaßstab** (mm auf der Seite, Parameter `f`, Betrag; das Vorzeichen folgt aus dem Typ:
  Sammellinse/Hohlspiegel positiv, Zerstreuungslinse/Wölbspiegel negativ). Skalieren ändert `f` nicht.
- **Linienarten (Ideen der ISO 128):** Umrisse breite Volllinie (0,5 mm), Details schmal (0,18–0,35 mm), optische Achse
  schmale Strichpunktlinie, Strahlen schmale Volllinien (0,35 mm, standardmäßig rot), virtuelle Strahlen gestrichelt
  (Linienart verdeckt). Glas ist hell getönt; Schwarzweiß-Ausgabe macht alles schwarz und weiß.
- **Beschriftung:** optionaler Parameter `label`, unter dem Element (außerhalb der Box, wie Kennzeichen in anderen Modulen).

## Objekttypen

| Typ-Id | Element | Parameter (alle optional) |
|---|---|---|
| `optics.lens.converging` | Sammellinse; Ansichten `shape` (Schnitt) und `symbol` (Doppelpfeil, Spitzen nach außen) | `shape` bikonvex/plankonvex/Meniskus, `f` (40), `focal` (Marken F, F′), `rays`, `bundle`, `beam`, `label` |
| `optics.lens.diverging` | Zerstreuungslinse; Ansichten `shape` und `symbol` (Spitzen nach innen) | `shape` bikonkav/plankonkav/Meniskus, wie oben |
| `optics.mirror.plane` | Planspiegel mit schraffierter Rückseite | `label` |
| `optics.mirror.concave` / `optics.mirror.convex` | Gekrümmte Spiegel | `f` (40), `focal` (F und Krümmungsmittelpunkt C/M), `label` |
| `optics.prism.triangle` | Prisma, gleichseitig (ablenkend) oder rechtwinklig (umlenkend) | `shape`, `n` (1,5, d-Linie), `dispersion`, `dn` = n(F) − n(C) (0,03), `label` |
| `optics.splitter.cube` / `optics.splitter.plate` | Strahlteiler: die Diagonale (links unten nach rechts oben) lässt durch und reflektiert | `label` |
| `optics.stop.aperture` | Blende / Spalt (zwei Backen) | `opening` (Anteil der Höhe, 0,35), `label` |
| `optics.grating.transmission` | Transmissionsgitter (Platte mit Furchen) | `angle` (Winkel 1. Ordnung bei 550 nm, 20°), `orders` (1–3), `label` |
| `optics.filter.plate` | Filterplatte, neutral oder farbig | `tint` neutral/rot/grün/blau/gelb, `label` |
| `optics.fibre.cable` | Lichtwellenleiter mit Steckern an beiden Enden | `spread` (halber Austrittskegel, 12°), `rays`, `label` |
| `optics.source.lamp` | Lampe (Kolben, Wendel, Sockel) | `emission` Kegel/parallel/in alle Richtungen, `rays`, `spread` (0 = nächstes Element ausleuchten), `beam`, `label` |
| `optics.source.led` | LED (Kuppel, Kragen, Anschlüsse) | `emission`, `rays`, `spread`, `beam`, `color` weiß/rot/grün/blau, `label` |
| `optics.source.laser` | Laser (Gehäuse, Austrittsöffnung) | `rays` (1), `beam`, `color` 650/532/445 nm, `label` |
| `optics.detector.photodiode` | Fotodiode als optischer Detektor | `label` |
| `optics.detector.camera` | Kamera: Objektiv auf den Bildsensor fokussiert | `label` |
| `optics.detector.eye` | Auge (Schnitt): Hornhaut, Iris, Linse, Netzhaut, Sehnerv | `label` |
| `optics.screen.plain` | Schirm | `label` |
| `optics.object.arrow` | Gegenstandspfeil auf der Achse (Abbildungsfiguren) | `bundle` Konstruktionsstrahlen/Fächer, `rays`, `label` |
| `optics.ray` | Lichtstrahl (Punktobjekt, Seitenkoordinaten) | `trace` (true), `arrows` Mitte/Ende/keine, `virtual` (true), `tail` (0 = automatisch) |
| `optics.axis.line` | Optische Achse (Strichpunktlinie, Punktobjekt) | `label` |

Die 35 Bibliothekselemente umfassen die Linsenformen und -symbole, drei Spiegel, ablenkendes, Dispersions- und
Umlenkprisma, beide Strahlteiler, Blende und Spalt, Gitter, Neutral- und Rotfilter, Lichtwellenleiter, Lampe (Kegel und
parallel), LED, roten und grünen Laser, Fotodiode, Kamera, Auge, Schirm, verfolgten und frei gezeichneten Lichtstrahl,
optische Achse und Gegenstandspfeil.

## Lichtstrahlen und Strahlverfolgung

Ein Strahlobjekt (`optics.ray`) arbeitet auf eine von drei Arten:

1. **An einem Emitter angedockt** (erster Punkt am Anker des Emitters): Es zeichnet dessen Bündel.
   - Lampe/LED: ein Kegel aus Kolben oder Kuppel (`spread` 0 = genau Linse, Spiegel oder Öffnung des nächsten Elements
     auf der Achse ausleuchten), paralleles Licht (Höhe `beam`, 0 = 70 % der Apertur des nächsten Elements) oder, bei der
     Lampe, in alle Richtungen.
   - Laser: ein Strahl oder `rays` über `beam` verteilt, nach Wellenlänge gefärbt.
   - Lichtwellenleiter: ein Kegel aus dem Ausgangsende.
   - Linse (`o`): ihr eigenes Demonstrationsbündel, parallel zur Achse von links oder von einem Achsenpunkt bei 2f.
   - Gegenstandspfeil (`tip`): die **Konstruktionsstrahlen** zur nächsten Linse oder zum nächsten gekrümmten Spiegel auf
     der Achse – Parallelstrahl, Mittelpunktstrahl (durch den optischen Mittelpunkt bzw. Scheitel) und Brennpunktstrahl
     (durch oder in Richtung des vorderen Brennpunkts) – oder ein Fächer über dessen Apertur.
2. **Freier Strahl** (nicht angedockt): ein einzelner Strahl vom ersten Punkt in Richtung des zweiten.
3. **Frei gezeichnet** (`trace` = false): Polylinie durch seine Punkte, ohne Verfolgung.

Die Kontextaktion **Strahlenbündel einfügen** (`optics.trace`) an Lampen, LEDs, Lasern, Lichtwellenleitern, Linsen und
Gegenstandspfeilen fügt ein am Element angedocktes Strahlobjekt ein (Anker `exit`, `o` oder `tip`); ist bereits eines
angedockt, heißt die Aktion **Strahlenbündel entfernen** und entfernt es.

**Live-Verfolgung.** Strahlen werden bei jedem Zeichnen aus dem aktuellen Dokument verfolgt: Alle Optik-Elemente der
Seite (ohne ausgeblendete Ebenen) wirken mit. Jedes Optik-Element meldet die Strahlen seiner Seite als abhängige Objekte
(`ObjectType.Dependents`, `DependsOnNeighbours`); Verschieben, Drehen, Skalieren oder Ändern eines Elements zeichnet die
Strahlen im selben Rückgängig-Schritt neu, der angedockte Startpunkt folgt seinem Emitter über die normale Anbindung.
Nichts muss von Hand neu verfolgt werden.

**Verwendete Physik:**

- Dünne Linsen: ideal, in der Mittelebene der Linse, Steigungsänderung −y/f (paraxial). Parallele Strahlen treffen sich
  genau in F′; die Linsengleichung 1/f = 1/s + 1/s′ gilt (`ThinLens.ImageDistance`). Zerstreuungslinsen zeichnen die
  Rückverlängerungen zum virtuellen Brennpunkt als gestrichelte **virtuelle Strahlen**.
- Gekrümmte Spiegel: ideal, als dünner Spiegel in der Scheitelebene mit Brennweite f (C bei 2f); der Strahl knickt auf der
  gezeichneten Fläche und wird so ausgerichtet, dass sich parallele Strahlen genau in F treffen. Wölbspiegel zeichnen
  virtuelle Strahlen.
- Planspiegel, Strahlteiler: Reflexionsgesetz, exakt. Die Spiegelrückseite hält Licht auf.
- Prismen: Brechungsgesetz (Snellius) an jeder Fläche, Totalreflexion, wo sie auftritt. Mit Dispersion zerfällt weißes
  Licht beim Eintritt in fünf Farben (656, 589, 546, 486, 436 nm), n(λ) aus einer zweigliedrigen Cauchy-Anpassung durch
  `n` und `dn`. Das Element „Dispersionsprisma“ verwendet `dn` = 0,05 (starke, flintartige Dispersion, der Sichtbarkeit
  wegen gewählt).
- Gitter: sin θm = sin θ + m · sin θ1 · λ / 550 nm für m = ±1 … ±`orders`; weißes Licht ergibt ein Spektrum je Ordnung.
- Blenden lassen Strahlen innerhalb der Öffnung durch und halten die übrigen auf; Filter lassen ihr Farbband durch
  (weißes Licht nimmt die Filterfarbe an); Schirm, Detektoren, Kameragehäuse, Augapfel, Quellengehäuse und
  Faserenden halten Strahlen auf. Auge und Kamera fokussieren paralleles Licht auf Netzhaut bzw. Sensor.
- Nach dem letzten Element läuft ein Strahl `tail` mm weiter; automatisch (0): noch einmal so lang wie der bisherige Weg
  (30–400 mm), und die offenen Enden eines Bündels werden quer dazu auf eine Linie gebracht. Ein freier Strahl, der nichts
  trifft, endet an seinem zweiten Punkt.

## Fanganker

- Jedes Element bietet seinen optischen Mittelpunkt als `Port`-Anker (`o`; Spiegel: Scheitel; Detektoren und Schirm:
  Vorderseite; Quellen: `exit`, Lampe/LED zusätzlich `o`; Lichtwellenleiter: `in`, `exit`; Gegenstandspfeil: `foot`, `tip`).
- Linsen: `f1`/`f2` bei ±f und `2f1`/`2f2` bei ±2f auf der Achse; gekrümmte Spiegel: `f` und `cc` (Krümmungsmittelpunkt).
  Port-auf-Port-Fangen bringt Brennpunkte zur Deckung (Fernrohr: F′ des Objektivs auf F des Okulars).
- Die optische Achse (`optics.axis.line`) bietet ihre Strecke als `Rail` (`axis0` …): Element-Ports docken überall darauf
  an, die Elemente stehen auf einer Achse. Rollen `Hub`/`Axis` werden nicht verwendet, daher entstehen weder mechanische
  Kopplung noch „+“-Bauanschlüsse.

## Teile und Bereinigen

Teileschlüssel (`PartLooks`): `body`, `focal`, `hatch`, `detail`, `label` an Elementen; `ray` (alle Strahlen, einzeln
`ray.<i>`) und `virtual` an Strahlen; `axis` an der optischen Achse. Sie lassen sich einzeln umgestalten oder ausblenden.

Folien bereinigen (`Sanitize`): Optik-Elemente enthalten keine schützenswerten Werte; der allgemeine Durchgang ersetzt
Freitext-Beschriftungen (Kennzeichen wie `L1` bleiben). Brennweiten, Brechzahlen und Positionen bleiben erhalten.

## Persistenz

Der gesamte Zustand liegt im Dokumentmodell: Parameter, `Size`, `Transform`, die Punkte des Strahls und die Anbindung
seines ersten Punkts (`AnchorId` = `exit`/`o`/`tip`). Die verfolgten Wege werden nicht gespeichert, sondern aus dem
Dokument berechnet; JSON-Rundreisen werden deshalb identisch gezeichnet.

## Hinweise für Hosts

- Alles wird mit nativen Primitiven gezeichnet (Linien, Polylinien, Polygone, Rechtecke, Ellipsen, Text,
  Linienend-Pfeilspitzen); Vektorbilder sind nicht nötig.
- Strahlen hängen von Nachbarn ab: Hosts mit getrennten Einheiten (PowerPoint COM) müssen Strahlen mit den übrigen
  Einheiten der Folie als Kontext zeichnen und die nach einer Bearbeitung gemeldeten abhängigen Objekte neu zeichnen, wie
  bereits bei Wänden und Leitungen.
- **Strahlenbündel einfügen** fügt ein neues Objekt ein (`ContextAction.ApplyInDocument`). Die Engine (Office Web,
  LibreOffice) übernimmt das; das PowerPoint-COM-Add-in übernimmt derzeit aus einem Aktionsergebnis nur die eigenen
  Objekte der gewählten Einheit, dort hat die Aktion also keine sichtbare Wirkung, bis das Add-in neue Objekte eines
  Aktionsergebnisses als neue Einheiten einfügt. Das Bibliothekselement „Lichtstrahl (verfolgt)“, dessen Start man auf
  eine Quelle zieht, funktioniert in jedem Host.
- Die gespeicherten Punkte eines angedockten Strahls werden durch die Verfolgung nicht aktualisiert; Hosts mit
  Punktgriffen zeigen den angedockten Start und den (unbenutzten) zweiten Punkt.

## Grenzen und nächste Schritte

- Zweidimensional, sequenziell, paraxial-ideale Linsen und Spiegel; keine dicken Linsen, Abbildungsfehler, Intensitäten,
  Polarisation oder Interferenz.
- Der Lichtwellenleiter führt kein Licht vom Eingang zum Ausgang; ein Bündel am Ausgangsende beginnt einen neuen Kegel.
- Licht, das ein Element von einer ungewöhnlichen Seite erreicht (etwa durch den Rand einer Linse), wird von der einzigen
  optischen Fläche des Elements behandelt.
- Mögliche nächste Schritte: automatische Bildkonstruktion (reelles/virtuelles Bild), dicke Linsen mit zwei Flächen,
  Vorlagen für Michelson/Mach-Zehnder.
