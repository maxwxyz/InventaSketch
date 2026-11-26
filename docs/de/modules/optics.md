# Modul Optik (`optics`)

Bibliothek: `src/InventaSketch.Modules.Optics` · Tests: `tests/InventaSketch.Modules.Optics.Tests` ·
Englische Fassung: [../../en/modules/optics.md](../../en/modules/optics.md)

Das Modul liefert optische Elemente für technische Abbildungen und Lehrfolien – Linsen (dünn und dick), Spiegel (auch
einen Galvo-Scannerspiegel), Prismen, Strahlteiler (schematisch, nicht polarisierend und polarisierend), Blenden, Gitter,
Filter, Polarisatoren, Verzögerungsplatten, eine Glasplatte (auch als Brewster-Fenster), einen Lichtleitstab, einen
Lichtwellenleiter, Lichtquellen, Detektoren (auch ein Spektrometer), einen Schirm, einen Gegenstandspfeil und die optische
Achse – sowie **Lichtstrahlen, die live durch die Elemente verfolgt werden** und dabei ihre Intensität und Polarisation
mitführen. Alle Symbole sind eigenständig gezeichnet; es werden keine genormten Symbolgrafiken oder Tabellen übernommen
und keine Normkonformität behauptet.

Es ist ein **Skizzen**-Modul: Die Strahlverfolgung ist zweidimensional und verwendet ideale (paraxiale) dünne Linsen und
gekrümmte Spiegel; nur die dicke Linse, Prismen, die Glasplatte und der Lichtleitstab werden Fläche für Fläche mit dem
Brechungsgesetz verfolgt. Fresnel-Reflexionsverluste und die durch sie entstehende Polarisation sind je Element wählbar
(Abschnitt „Fresnel-Verluste und Vergütung“). Optische Systeme werden nicht ausgelegt oder optimiert (keine
Abbildungsfehleranalyse, keine Radiometrie in absoluten Einheiten).

## Konventionen

- **Quick Sketch zuerst:** Jedes Bibliothekselement wird ohne Parameter eingefügt. Freies (gleichmäßiges oder
  ungleichmäßiges) Skalieren ändert nur `Size`. Die Boxhöhe von Linse, Spiegel, Blende, Gitter oder Filter ist seine
  Apertur.
- **Optische Achse = lokale x-Achse** jedes Elements (Licht von links läuft in +x-Richtung). Zum Ausrichten das Element
  drehen oder spiegeln; Spiegel reflektieren auf ihrer lokalen −x-Seite (schraffierte Rückseite auf +x).
- **Brennweiten im Zeichnungsmaßstab** (mm auf der Seite, Parameter `f`, Betrag; das Vorzeichen folgt aus dem Typ:
  Sammellinse/Hohlspiegel positiv, Zerstreuungslinse/Wölbspiegel negativ). Skalieren ändert `f` nicht.
- **Linienarten (Ideen der ISO 128):** Umrisse breite Volllinie (0,5 mm), Details schmal (0,18–0,35 mm), optische Achse
  schmale Strichpunktlinie, Strahlen schmale Volllinien (0,35 mm, standardmäßig rot), virtuelle Strahlen und die
  Scanfächer-Strahlen eines Scannerspiegels gestrichelt (Linienart verdeckt). Glas ist hell getönt; Schwarzweiß-Ausgabe macht alles schwarz und weiß.
- **Beschriftung:** optionaler Parameter `label`, unter dem Element (außerhalb der Box, wie Kennzeichen in anderen Modulen).

## Objekttypen

| Typ-Id | Element | Parameter (alle optional) |
|---|---|---|
| `optics.lens.converging` | Sammellinse; Ansichten `shape` (Schnitt) und `symbol` (Doppelpfeil, Spitzen nach außen) | `shape` bikonvex/plankonvex/Meniskus, `f` (40), `focal` (Marken F, F′), `rays`, `bundle`, `beam`, `coating` (ideal, nur im Inspektor), `label` |
| `optics.lens.diverging` | Zerstreuungslinse; Ansichten `shape` und `symbol` (Spitzen nach innen) | `shape` bikonkav/plankonkav/Meniskus, wie oben |
| `optics.mirror.plane` | Planspiegel mit schraffierter Rückseite | `label` |
| `optics.mirror.concave` / `optics.mirror.convex` | Gekrümmte Spiegel | `f` (40), `focal` (F und Krümmungsmittelpunkt C/M), `label` |
| `optics.prism.triangle` | Prisma, gleichseitig (ablenkend) oder rechtwinklig (umlenkend) | `shape`, `n` (1,5, d-Linie), `dispersion`, `dn` = n(F) − n(C) (0,03), `coating` (ideal), `label` |
| `optics.splitter.cube` / `optics.splitter.plate` | Strahlteiler: die Diagonale (links unten nach rechts oben) lässt durch und reflektiert; der polarisierende zeichnet seine Teilerfläche doppelt (Würfel) bzw. mit Mittellinie (Platte) | `split` schematisch/nicht polarisierend/polarisierend (schematisch), `reflectance` (reflektierter Anteil, nicht polarisierend, 0,5), `label` |
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
| `optics.object.arrow` | Gegenstandspfeil auf der Achse (Abbildungsfiguren) | `bundle` Konstruktionsstrahlen/Fächer, `rays`, `image` aus/Bild/Konstruktion (Bild), `names` (false), `label` |
| `optics.ray` | Lichtstrahl (Punktobjekt, Seitenkoordinaten) | `trace` (true), `arrows` Mitte/Ende/keine, `virtual` (true), `tail` (0 = automatisch), `showValues` (Intensität und Polarisation an den Strahlenden, false) |
| `optics.axis.line` | Optische Achse (Strichpunktlinie, Punktobjekt) | `label` |
| `optics.lens.thick` | Dicke Linse: zwei Kugelflächen, an beiden verfolgt (Boxbreite = Dicke, Boxhöhe = Apertur) | `shape` bikonvex/plankonvex/bikonkav/plankonkav, `n` (1,5), `sag` (Wölbungstiefe als Anteil der Breite, 0,35), `focal` (F, F′, Hauptebenen H, H′), `rays`, `beam`, `coating` (ideal), `label` |
| `optics.mirror.scanner` | Galvo-Scannerspiegel: Planspiegel auf einer Drehachse, in seiner Box gedreht | `angle` (Spiegelwinkel, 45°; 0° = aufrecht wie ein Planspiegel), `scan` (mechanischer Scanbereich ±, 10°), `fan` (Endlagen und Strahlenfächer, false), `label` |
| `optics.polarizer` | Linearpolarisator: Platte mit der Durchlassrichtung als Doppelpfeil | `angle` (Durchlassrichtung, 0°), `label` |
| `optics.waveplate` | Verzögerungsplatte: Platte mit schneller Achse (gestrichelter Doppelpfeil) und „λ/4“/„λ/2“ | `retardance` Viertel/Halb (Viertel), `angle` (schnelle Achse, 45°), `label` |
| `optics.lightguide` | Lichtleitstab (rechteckiger Glasstab, Totalreflexion) | `n` (1,5), `coating` (ideal), `label` |
| `optics.window.plate` | Glasplatte (Fenster): planparallele Platte, in ihrer Box geneigt; die Kanten halten Licht auf | `n` (1,5), `tilt` (Einfallswinkel entlang der Achse, 0°, −80° … 80°), `brewster` (Neigung = arctan n, false), `coating` (standardmäßig **unvergütet**), `label` |
| `optics.detector.spectrometer` | Spektrometer: Gehäuse mit Eintrittsspalt, Reflexionsgitter, Detektorzeile und darin gezeichnetem zerlegtem Licht | `label` |

Die 48 Bibliothekselemente umfassen die Linsenformen und -symbole, dicke bikonvexe und plankonvexe Linse, drei Spiegel,
den Scannerspiegel mit und ohne Scanbereich, ablenkendes, Dispersions- und Umlenkprisma, beide Strahlteiler
(schematisch), den 50:50-Würfel (nicht polarisierend) und den Polarisationsstrahlteilerwürfel (PBS), Blende und Spalt,
Gitter, Neutral- und Rotfilter, Lichtleitstab, Linearpolarisator, Viertel- und Halbwellenplatte, Glasplatte und
Brewster-Fenster, Lichtwellenleiter, Lampe (Kegel und parallel), LED, roten und grünen Laser, Fotodiode, Kamera, Auge, Schirm,
Spektrometer, verfolgten und frei gezeichneten Lichtstrahl, optische Achse und Gegenstandspfeil.

## Lichtstrahlen und Strahlverfolgung

Ein Strahlobjekt (`optics.ray`) arbeitet auf eine von drei Arten:

1. **An einem Emitter angedockt** (erster Punkt am Anker des Emitters): Es zeichnet dessen Bündel.
   - Lampe/LED: ein Kegel aus Kolben oder Kuppel (`spread` 0 = genau Linse, Spiegel oder Öffnung des nächsten Elements
     auf der Achse ausleuchten), paralleles Licht (Höhe `beam`, 0 = 70 % der Apertur des nächsten Elements) oder, bei der
     Lampe, in alle Richtungen.
   - Laser: ein Strahl oder `rays` über `beam` verteilt, nach Wellenlänge gefärbt.
   - Lichtwellenleiter: ein Kegel aus dem Ausgangsende.
   - Linse (`o`): ihr eigenes Demonstrationsbündel, parallel zur Achse von links oder von einem Achsenpunkt bei 2f.
     Die dicke Linse hat dasselbe parallele Bündel (`rays`, `beam`).
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
(`ObjectType.Dependents`, `HasDependents`; nur Strahlen und Gegenstandspfeile setzen `DependsOnNeighbours`); Elemente, die auf Licht wirken (eine optische Fläche haben), melden
zusätzlich die Gegenstandspfeile der Seite, die ein Bild zeichnen. Verschieben, Drehen, Skalieren oder Ändern eines
Elements oder das Ziehen eines seiner Griffe zeichnet Strahlen und Bilder daher im selben Rückgängig-Schritt neu, der
angedockte Startpunkt folgt seinem Emitter über die normale Anbindung. Nichts muss von Hand neu verfolgt werden.

**Verwendete Physik:**

- Dünne Linsen: ideal, in der Mittelebene der Linse, Steigungsänderung −y/f (paraxial). Parallele Strahlen treffen sich
  genau in F′; die Linsengleichung 1/f = 1/s + 1/s′ gilt (`ThinLens.ImageDistance`). Zerstreuungslinsen zeichnen die
  Rückverlängerungen zum virtuellen Brennpunkt als gestrichelte **virtuelle Strahlen**.
- Gekrümmte Spiegel: ideal, als dünner Spiegel in der Scheitelebene mit Brennweite f (C bei 2f); der Strahl knickt auf der
  gezeichneten Fläche und wird so ausgerichtet, dass sich parallele Strahlen genau in F treffen. Wölbspiegel zeichnen
  virtuelle Strahlen.
- Planspiegel, Strahlteiler: Reflexionsgesetz, exakt. Die Spiegelrückseite hält Licht auf. Spiegel sind ideal (alles Licht
  reflektiert, Polarisation erhalten). Strahlteiler (`split`): **schematisch** (Standard, wie bisher: durchgelassener und
  reflektierter Strahl behalten beide die volle Intensität), **nicht polarisierend** (der Anteil `reflectance` wird
  reflektiert, der Rest durchgelassen, beide mit der einfallenden Polarisation) oder **polarisierend** (idealer PBS: der
  p-Anteil wird durchgelassen, p-polarisiert, der s-Anteil reflektiert, s-polarisiert; unpolarisiertes Licht wird
  halbiert). Ein Anteil unter 0,5 % wird nicht gezeichnet.
- Prismen: Brechungsgesetz (Snellius) an jeder Fläche, Totalreflexion, wo sie auftritt. Mit Dispersion zerfällt weißes
  Licht beim Eintritt in fünf Farben (656, 589, 546, 486, 436 nm), n(λ) aus einer zweigliedrigen Cauchy-Anpassung durch
  `n` und `dn`. Das Element „Dispersionsprisma“ verwendet `dn` = 0,05 (starke, flintartige Dispersion, der Sichtbarkeit
  wegen gewählt).
- Gitter: sin θm = sin θ + m · sin θ1 · λ / 550 nm für m = ±1 … ±`orders`; weißes Licht ergibt ein Spektrum je Ordnung.
- Blenden lassen Strahlen innerhalb der Öffnung durch und halten die übrigen auf; Filter lassen ihr Farbband durch
  (weißes Licht nimmt die Filterfarbe an); Schirm, Detektoren, Kameragehäuse, Augapfel, Quellengehäuse und
  Faserenden halten Strahlen auf. Auge und Kamera fokussieren paralleles Licht auf Netzhaut bzw. Sensor.
- Dicke Linse: Brechungsgesetz an beiden Kugelflächen, exakt mit den Kreisen geschnitten (keine Facetten); die Ränder
  halten Licht auf. Die Radien folgen aus der Box und `sag` (eine Fläche der Tiefe s über der halben Apertur h hat
  R = (s² + h²) / 2s), Skalieren der Linse ändert also ihre Brennweite. F, F′, H und H′ kommen aus der
  Linsenschleiferformel für dicke Linsen, 1/f = (n − 1)·[1/R1 − 1/R2 + (n − 1)·d / (n·R1·R2)], bildseitige
  Schnittweite f·(1 − (n − 1)·d / (n·R1)) (`ThickLens.FocalLength`, `BackFocalDistance`, `FrontFocalDistance`).
  Achsnahe Strahlen treffen sich in F′, wie die Formel sagt; Randstrahlen schneiden die Achse früher (die sphärische
  Aberration ist sichtbar, wird aber nicht ausgewertet).
- Scannerspiegel: Reflexionsgesetz am gedrehten Spiegel, eine Drehung um Δ dreht den reflektierten Strahl also um 2Δ. Mit
  `fan` werden die Endlagen des Spiegels (±`scan`) gestrichelt gezeichnet, und jeder an ihm reflektierte Strahl sendet
  zwei gestrichelte **Fächerstrahlen** (die Reflexionen an beiden Bereichsenden, optisch ±2·`scan`), die durch die
  folgenden Elemente weiterverfolgt werden (etwa ein Scanobjektiv). Fächerstrahlen verzweigen an einem zweiten Scanner
  nicht noch einmal.
- Lichtleitstab: Brechungsgesetz an seinen vier Flächen; Licht, das eine Seitenfläche jenseits des Grenzwinkels
  arcsin(1/n) trifft (41,8° bei n = 1,5), wird totalreflektiert und zum anderen Ende geführt, darunter tritt es seitlich
  aus.
- Polarisation (Jones-Formalismus): Ein Strahl trägt seine relative Intensität (1 an der Quelle) und seine Polarisation,
  unpolarisiert bis zum ersten Polarisator. Winkel werden quer zum Strahl von der Zeichenebene (0°) zur Senkrechten auf
  die Zeichnung (90°) gemessen. Ein Polarisator lässt die Hälfte unpolarisierten Lichts und den Anteil cos²(θ − Achse)
  polarisierten Lichts durch (Gesetz von Malus) und polarisiert entlang seiner Achse; bleiben weniger als 0,5 % (gekreuzte
  Polarisatoren), endet der Strahl dort. Eine Verzögerungsplatte verzögert die Komponente entlang ihrer langsamen Achse um
  π/2 (λ/4) oder π (λ/2): Eine Halbwellenplatte dreht lineare Polarisation (θ → 2·Achse − θ), eine Viertelwellenplatte
  unter 45° macht sie zirkular (jeder Analysator lässt dann die Hälfte durch). Der Zustand ist eine Kohärenzmatrix, Licht
  kann also auch **teilweise polarisiert** sein (hinter einem Brewster-Fenster). Spiegel und Gitter erhalten Polarisation
  und Intensität; Strahlteiler und Glasflächen ändern sie wie oben und unten beschrieben. Schwächeres Licht wird
  transparenter gezeichnet (Deckkraft = relative Intensität, mindestens 0,2); Licht mit voller Intensität wird genau wie
  bisher gezeichnet.
- Das Spektrometer ist für die Strahlverfolgung ein Detektor: Strahlen enden an seinem Gehäuse (an der Spaltwand); Gitter
  und Spektrum im Inneren sind schematisch gezeichnet, nicht verfolgt.
- Nach dem letzten Element läuft ein Strahl `tail` mm weiter; automatisch (0): noch einmal so lang wie der bisherige Weg
  (30–400 mm), und die offenen Enden eines Bündels werden quer dazu auf eine Linie gebracht. Ein freier Strahl, der nichts
  trifft, endet an seinem zweiten Punkt.
- **Werte an den Strahlenden** (`showValues`, Schalter **Werte an Strahlenden**, standardmäßig aus):
  Jeder verfolgte Strahl (nicht die Fächerstrahlen) schreibt an sein Ende seine relative Intensität und Polarisation
  (Teil `values`, Schrift 2,5 mm, über dem Ende; rechts daneben bei Strahlen, die nach oben oder unten laufen):
  „50 % (linear 0°)“, „25 % (zirkular)“, „86 % (16 % polarisiert)“ (Polarisationsgrad), „100 %“ bei unpolarisiertem
  Licht; englisch „polarised“, „circular“, „elliptical“, Dezimalpunkt. Ein Strahl, der auf einem Detektor endet, zeigt
  so, was der Detektor empfängt.

### Fresnel-Verluste und Vergütung

Glasflächen (Prisma, dicke Linse, Lichtleitstab, Glasplatte) und dünne Linsen haben den Parameter `coating`
(**Glasoberflächen**, eine Auswahl in der Kontextleiste, außer bei dünnen Linsen, wo er im Inspektor eingestellt wird):

| Wert | Bedeutung |
|---|---|
| `ideal` | Ideale Flächen: keine Reflexionsverluste und keine Änderung der Polarisation, auch nicht bei Totalreflexion. **Standard aller bereits vorhandenen Elemente**, damit bestehende Zeichnungen unverändert dargestellt werden. |
| `none` | Unvergütetes Glas: die Fresnel-Gleichungen für s und p an jeder Fläche (Standard der Glasplatte). |
| `ar` | Entspiegelt: eine λ/4-Schicht der Brechzahl √n, ausgelegt für 550 nm, als dünne Schicht verfolgt (keine Reflexion bei 550 nm und senkrechtem Einfall, etwas bei anderen Wellenlängen und Winkeln); Totalreflexion wie unvergütet. |

Die Physik (`Fresnel`, alle Winkel gegen die Flächennormale, verlustfreie Medien n1 → n2):

- Reflexionsgrade R_s = r_s², R_p = r_p² mit r_s = (n1·cos θi − n2·cos θt) / (n1·cos θi + n2·cos θt) und
  r_p = (n1·cos θt − n2·cos θi) / (n1·cos θt + n2·cos θi); Transmissionsgrade T = 1 − R. Unvergütetes Glas n = 1,5
  reflektiert bei senkrechtem Einfall 4 % je Fläche (eine dünne Linse lässt auf der Achse daher 0,96² = 92,2 % durch),
  bei 45° 9,2 % (s) und 0,85 % (p).
- Brewster-Winkel tan θB = n2 / n1 (56,31° bei n = 1,5): p wird gar nicht reflektiert, s etwa 14,8 % je Fläche. Eine auf
  den Brewster-Winkel geneigte Platte (`brewster`, Bibliothekselement **Brewster-Fenster**) lässt p-polarisiertes Licht
  verlustfrei durch; innen trifft das Licht die zweite Fläche unter dem inneren Brewster-Winkel 90° − θB, auch diese
  reflektiert also kein p. Unpolarisiertes Licht verlässt sie teilweise p-polarisiert: Intensität (1 + T_s²)/2 = 86 % mit
  dem Polarisationsgrad (1 − T_s²)/(1 + T_s²) = 16 %; ein Polarisator dahinter lässt entlang p 50 % und entlang s 36,3 %
  durch.
- Totalreflexion jenseits des Grenzwinkels sin θc = n2 / n1 (41,8° bei n = 1,5): Alles Licht wird reflektiert, mit einer
  Phasendifferenz zwischen p und s (`Fresnel.ReflectionPhase`). Winkel werden im eigenen Bezugssystem jedes Strahls
  gemessen, in dem ein idealer Spiegel nichts ändert; darin verschiebt eine Totalreflexion unter 45° in n = 1,5 p gegen s
  um 180° − 36,87° (Lehrbuch: tan(Δ/2) = cos θ·√(sin²θ − (n2/n1)²) / sin²θ, dazu die 180° des Bezugssystems), und zwei
  Totalreflexionen unter 54,6° in n = 1,51 ergeben zusammen eine Viertelwelle (Fresnel-Rhombus). Ein Umlenkprisma mit
  unvergüteten Flächen macht linear unter 45° polarisiertes Licht daher elliptisch.
- Eine dünne Linse gilt als zwei ebene Flächen aus Glas n = 1,5 quer zu ihrer Achse, getroffen unter dem Winkel des
  Strahls zur Achse.
- Teilreflexionen an Glasflächen schwächen den durchgelassenen Strahl; sie werden nicht als eigene Strahlen verfolgt
  (keine Geisterbilder). Licht, das (praktisch) verschwunden ist, unter 0,5 %, beendet den Strahl.

## Bildkonstruktion

Ein Gegenstandspfeil (`optics.object.arrow`), vor dem auf seiner Achse eine Sammel- oder Zerstreuungslinse oder ein
gekrümmter Spiegel steht (das nächste solche Element, dessen Mitte höchstens die halbe größere Kastenseite von der
Achsenlinie des Pfeils entfernt ist; ein Spiegel muss dem Pfeil seine spiegelnde Seite zuwenden), zeichnet sein **Bild** –
Parameter `image`, eine Auswahl in der Kontextleiste:

- `off`: kein Bild.
- `image` (Vorgabe): nur der Bildpfeil. Ein **reelles** Bild (das Licht trifft sich dort wirklich) wird durchgezogen
  gezeichnet, ein **virtuelles** gestrichelt (Linienrolle verdeckt).
- `construction`: das Bild mit den drei **Konstruktionsstrahlen** von der Pfeilspitze, je in einer Farbe: Parallelstrahl
  (rot: parallel zur Achse bis zum Element, dann durch F′ oder vom virtuellen F′ weg), Mittelpunktstrahl (grün: durch den
  optischen Mittelpunkt bzw. Scheitel, ungebrochen bzw. symmetrisch reflektiert) und Brennpunktstrahl (blau: durch F oder
  in Richtung F, nach dem Element parallel zur Achse). Rückverlängerungen zu einem virtuellen Bild und der Teil eines
  Brennpunktstrahls, der nur auf F zielt, sind gestrichelt. Wie in Lehrbuchfiguren knicken die Strahlen an der Hauptebene
  des Elements, die bei Bedarf über die gezeichnete Öffnung hinaus verlängert ist.

Jeder Punkt des Pfeils (Fuß und Spitze) wird mit der Linsengleichung **1/f = 1/g + 1/b** und dem Abbildungsmaßstab
**β = −b/g** abgebildet (g Gegenstandsweite, b Bildweite; b > 0 hinter einer Linse bzw. vor einem Spiegel = reell,
b < 0 = virtuell; `ThinLens.ImageDistance`/`Magnification`). Beispiele (f = 40 mm): g = 2f ergibt b = 2f, β = −1;
g = 1,5f ergibt b = 3f, β = −2; g = f/2 ergibt b = −f, β = 2 (virtuell, aufrecht, vergrößert). Ein Spiegel klappt den
Bildraum auf seine Vorderseite zurück; das Bild eines Wölbspiegels liegt hinter ihm. Bei g = f (Bild im Unendlichen), bei
Bildern weiter als 5 m oder mehr als 50-fach vergrößert wird kein Bild gezeichnet (die Konstruktionsstrahlen schon, ohne
Brennpunktstrahl).

`names` (Schalter **Namen Gegenstand und Bild**) schreibt die Namen an die Spitzen: deutsch „G“ und „B“, englisch „O“ und
„I“. Bild und Strahlen sind reine paraxiale Geometrie aus dem Dokument, werden mit dem Pfeil gezeichnet (außerhalb seines
Kastens, wie die Brennpunktmarken einer Linse) und folgen live, wenn Pfeil, Linse oder Spiegel verschoben oder geändert
werden. Ein am Pfeil angedocktes Strahlenbündel (Konstruktionsstrahlen, verfolgt) arbeitet weiterhin unabhängig davon:
Die verfolgten Strahlen treffen sich im gezeichneten Bild.

## Griffe (Ziehgriffe)

Werte werden direkt in der Zeichnung gezogen (`ObjectType.DragHandles` / `MoveHandle`, Seiten-mm; die Hosts zeigen und
bedienen sie allgemein). Ein Zug schreibt nur den Parameter; Kasten (`Size`) und Lage ändern sich nie.

| Element | Griff (Schlüssel) | Wo | Bewegung | Schreibt |
|---|---|---|---|---|
| Linsen | `f.back`, bei gezeichneten Brennpunkten auch `f.front` | F′ bei +f (F bei −f) auf der Achse | entlang der Achse | `f` = Abstand von der Linse, ganze mm, 1–5000 (das Vorzeichen bleibt beim Typ) |
| Linsen mit angedocktem eigenem Bündel | `beam` | Rand des Bündels (Start der parallelen Strahlen bzw. auf der Linsenebene bei Strahlen aus 2f) | quer zur Achse | `beam` = doppelter Abstand von der Achse, ganze mm |
| Hohl-/Wölbspiegel | `f`, bei gezeichnetem F und M auch `cc` | F (vor dem Hohlspiegel, hinter dem Wölbspiegel), M bei 2f | entlang der Achse | `f` = Abstand vom Scheitel (M: die Hälfte davon), ganze mm |
| Blende / Spalt | `opening` | Innenkante der oberen Lamelle | quer zur Achse | `opening` = Anteil der halben Höhe, Schritte 0,01, 0,02–0,95 |
| Lampe, LED (Kegel) mit angedockten Strahlen | `spread` | auf dem oberen Randstrahl des Kegels, jenseits des Kolbens | frei; zählt der Winkel von der Kolbenmitte aus | `spread` = halber Kegelwinkel, ganze Grad, 1°–80° |
| Lampe, LED (parallel) mit angedockten Strahlen | `beam` | Rand des Bündels an Kolben/Kuppel | quer zur Achse | `beam`, ganze mm |
| Laser mit mehreren Strahlen und angedockten Strahlen | `beam` | Rand des Strahls am Austritt | quer zur Achse | `beam`, ganze mm |
| Lichtwellenleiter mit angedockten Strahlen | `spread` | auf dem oberen Randstrahl des Austrittskegels | frei, Winkel vom Ausgangsende | `spread`, ganze Grad |
| Dicke Linse mit angedocktem eigenem Bündel | `beam` | Rand des Bündels an seinem Start | quer zur Achse | `beam`, ganze mm (die Brennweite folgt aus Form, Box und `n` und hat daher keinen Griff) |
| Scannerspiegel | `angle` | oberes Ende des Spiegels | frei, um die Drehachse | `angle` = Richtung der Spiegellinie, ganze Grad, gefaltet auf −90° … 90° |
| Polarisator, Verzögerungsplatte | `axis` | Ende des Achsen-Doppelpfeils | frei, um die Mitte | `angle` = Richtung der Achsenlinie, ganze Grad, −90° … 90° |
| Glasplatte (Fenster) | `tilt` | oberes Ende der Platte | frei, um die Mitte | `tilt` = Richtung der Platte, ganze Grad, −80° … 80°; hebt `brewster` auf |
| Freier verfolgter Strahl | `dir` | auf dem gezeichneten Strahl, halbwegs zum zweiten Punkt bzw. zum ersten getroffenen Element | frei, um den Start (Bogen) | zweiter Punkt um den ersten gedreht, ganze Grad (im Uhrzeigersinn am Bildschirm), Länge bleibt |

Jeder Griff trägt eine zweisprachige Beschriftung mit seinem aktuellen Wert („Brennweite f: 40 mm“, „Halber Kegelwinkel:
12°“, „Startwinkel: 30°“). Die Griffe von Linsen und Spiegeln gehören bei gezeichneten Marken zum Teil `focal` (Auswahl
der Brennpunkte zeigt sie). Emitter-Griffe erscheinen nur, solange ein Strahlenbündel angedockt ist (sonst ist kein Kegel
zu sehen); ein an einem Emitter angedockter Strahl übernimmt dessen Richtung (Emitter drehen) und hat keinen eigenen Griff;
ein nicht verfolgter Strahl wird über seine Punkte bearbeitet. Prismen und Gitter haben keine Griffe (kein Parameter
entspricht natürlich einem Punkt der Zeichnung); der Winkel der 1. Ordnung eines Gitters wird in seinem Zahlenfeld
eingestellt.

## Fanganker

- Jedes Element bietet seinen optischen Mittelpunkt als `Port`-Anker (`o`; Spiegel: Scheitel; Detektoren und Schirm:
  Vorderseite; Quellen: `exit`, Lampe/LED zusätzlich `o`; Lichtwellenleiter: `in`, `exit`; Gegenstandspfeil: `foot`, `tip`).
- Linsen: `f1`/`f2` bei ±f und `2f1`/`2f2` bei ±2f auf der Achse; dicke Linse: `f1`/`f2` bei F und F′
  (Linsenschleiferformel); gekrümmte Spiegel: `f` und `cc` (Krümmungsmittelpunkt); Scannerspiegel: `o` an der Drehachse;
  Lichtleitstab: `in`, `exit`, `o`.
  Port-auf-Port-Fangen bringt Brennpunkte zur Deckung (Fernrohr: F′ des Objektivs auf F des Okulars).
- Die optische Achse (`optics.axis.line`) bietet ihre Strecke als `Rail` (`axis0` …): Element-Ports docken überall darauf
  an, die Elemente stehen auf einer Achse. Rollen `Hub`/`Axis` werden nicht verwendet, daher entstehen weder mechanische
  Kopplung noch „+“-Bauanschlüsse.

## Teile und Bereinigen

Teileschlüssel (`PartLooks`): `body`, `focal`, `hatch`, `detail`, `label` an Elementen; `image`, `construction` und
`names` am Gegenstandspfeil (aufgeführt, solange gezeichnet); `ray` (alle Strahlen, einzeln `ray.<i>`), `virtual` und
`values` (die Werte an den Enden, solange gezeigt) an Strahlen; `axis` an der optischen Achse. Sie lassen sich einzeln umgestalten oder ausblenden.

Folien bereinigen (`Sanitize`): Optik-Elemente enthalten keine schützenswerten Werte; der allgemeine Durchgang ersetzt
Freitext-Beschriftungen (Kennzeichen wie `L1` bleiben). Brennweiten, Brechzahlen und Positionen bleiben erhalten.

## Persistenz

Der gesamte Zustand liegt im Dokumentmodell: Parameter, `Size`, `Transform`, die Punkte des Strahls und die Anbindung
seines ersten Punkts (`AnchorId` = `exit`/`o`/`tip`). Die verfolgten Wege, Bilder, Konstruktionsstrahlen und Griffe
werden nicht gespeichert, sondern aus dem Dokument berechnet; JSON-Rundreisen werden deshalb identisch gezeichnet.

## Hinweise für Hosts

- Alles wird mit nativen Primitiven gezeichnet (Linien, Polylinien, Polygone, Rechtecke, Ellipsen, Text,
  Linienend-Pfeilspitzen); Vektorbilder sind nicht nötig.
- Strahlen hängen von Nachbarn ab: Hosts mit getrennten Einheiten (PowerPoint COM) müssen Strahlen mit den übrigen
  Einheiten der Folie als Kontext zeichnen und die nach einer Bearbeitung gemeldeten abhängigen Objekte neu zeichnen, wie
  bereits bei Wänden und Leitungen.
- **Strahlenbündel einfügen** fügt ein neues Objekt ein (`ContextAction.ApplyInDocument`). Die Engine (Office Web,
  LibreOffice) übernimmt das, und das PowerPoint-COM-Add-in fügt die neuen Objekte eines Aktionsergebnisses als neue
  Einheiten ein. Das Bibliothekselement „Lichtstrahl (verfolgt)“, dessen Start man auf eine Quelle zieht, funktioniert in
  jedem Host.
- Die gespeicherten Punkte eines angedockten Strahls werden durch die Verfolgung nicht aktualisiert; Hosts mit
  Punktgriffen zeigen den angedockten Start und den (unbenutzten) zweiten Punkt.
- **Gegenstandspfeile mit Bild hängen von Nachbarn ab** wie Strahlen: mit den übrigen Einheiten der Folie als Kontext
  zeichnen und neu zeichnen, wenn eine Linse oder ein Spiegel, durch die sie abbilden, bearbeitet wird (sie werden als deren
  abhängige Objekte gemeldet). Das Bild liegt außerhalb des Pfeilkastens, die native Gruppe des Pfeils wächst also mit
  (wie eine Linse mit ihren Brennpunktmarken).
- **Griffe:** Das COM-Add-in zeigt und zieht die Griffe jedes Typs allgemein (Vorschau des gezogenen Elements, ein
  Rückgängig-Schritt beim Loslassen, dann werden die abhängigen Objekte neu gezeichnet). Strahlen und Bilder in *anderen*
  Einheiten folgen beim Loslassen, nicht während des Ziehens: Ein Live-Folgen beim Ziehen eines Griffs oder beim
  Verschieben von Quelle, Linse oder Spiegel braucht einen Host, der die gemeldeten abhängigen Objekte mit dem bewegten
  Element in seiner Zieh-Vorschau neu zeichnet (offener Punkt). `moveHandle` der Engine (Web, LibreOffice) erkennt eine
  Änderung nur an Parametern und Größe; der Richtungsgriff des freien Strahls, der die Punkte des Strahls ändert, braucht
  dort auch den Vergleich der Punkte.

## Grenzen und nächste Schritte

- Zweidimensional, sequenziell; dünne Linsen und gekrümmte Spiegel sind paraxiale Ideale. Keine Interferenz.
  Fresnel-Verluste nur, wo `coating` nicht ideal ist; Teilreflexionen an Glasflächen werden nicht als Strahlen gezeichnet
  (keine Geisterbilder). Spiegel sind ideal (keine Metallreflexion oder -phase), Strahlteiler ideal (kein
  Auslöschungsverhältnis, keine Absorption). Die Entspiegelung ist eine idealisierte Einzelschicht (Brechzahl √n).
- Die Strahlwerte sind eine Beschriftung an den Strahlenden; Optikelemente haben keine Wertekarte (Detektoren melden die
  empfangene Intensität noch nicht als Wert).
- Die Bildkonstruktion des Gegenstandspfeils verwendet nur dünne Linsen und gekrümmte Spiegel (nicht die dicke Linse; ihre
  verfolgten Strahlen durchlaufen sie dagegen). Die dicke Linse hat keinen Brennweitengriff.
- Das Spektrometer verfolgt das Licht in seinem Inneren nicht; sein Spektrum ist eine schematische Zeichnung.
- Der Lichtwellenleiter führt kein Licht vom Eingang zum Ausgang; ein Bündel am Ausgangsende beginnt einen neuen Kegel.
- Licht, das ein Element von einer ungewöhnlichen Seite erreicht (etwa durch den Rand einer Linse), wird von der einzigen
  optischen Fläche des Elements behandelt.
- Die Bildkonstruktion verwendet nur die erste Linse bzw. den ersten gekrümmten Spiegel auf der Achse des Pfeils (kein Bild
  eines Bildes durch ein zweites Element); das verfolgte Strahlenbündel durchläuft dagegen alle Elemente.
- Mögliche nächste Schritte: Bilder durch mehrere Elemente, Abbildung durch die dicke Linse für den Gegenstandspfeil,
  empfangene Intensität als Detektorwert, Geisterreflexe, Vorlagen für Michelson/Mach-Zehnder.
