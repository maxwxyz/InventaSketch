# Modul Grundrisse & Gebäude (`architecture`)

Schematische Hausplanung für Folien und schnelle technische Skizzen: Wände mit sauberen Anschlüssen, Räume mit
Fläche, Türen, Fenster, Treppen, Rampen, Aufzüge, Geländer, Schächte, Deckenöffnungen, Dachaufsichten, Stützen,
Unterzüge, Möbel, Sanitär, Heizungs-/Leitungssymbole und Lageplanzeichen. Es ist **kein BIM**:
Hinter dem Plan steht kein Gebäudemodell, nur gut gezeichnete, editierbare Symbole.

Elektro-Grundrisssymbole liegen im Elektro-Modul, Bemaßungen im Beschriftungsmodul; dieses Modul dupliziert sie
nicht.

## Planmaßstab

Jedes Objekt ist ohne Parameter einfügbar. Die Bibliotheksgrößen sind für **1:100** ausgelegt (1 mm auf dem Blatt
entspricht 100 mm in der Realität). Die Maßstabszahl eines Objekts ergibt sich in dieser Reihenfolge:

1. sein eigener optionaler erweiterter Parameter `scale` (bei neuen Objekten nicht gesetzt; ältere Dokumente können
   `scale = 100` enthalten),
2. der **Planmaßstab des Dokuments** `DocumentSettings.PlanScale` (Engine-Methode `setPlanScale`, im Inspektor als
   „Planmaßstab (Dokument) 1:“, sobald ein ausgewählter Typ ihn verwendet),
3. 1:100.

Eine Änderung des Dokument-Planmaßstabs zeichnet alle Objekte neu, die ihn verwenden (Wände, Räume, Unterzüge,
Türen, Fenster), in einem Rückgängig-Schritt.

| Größe | Umrechnung |
|---|---|
| Wanddicke, Unterzugbreite | `thickness / scale` mm auf dem Blatt (240 mm bei 1:100 = 2,4 mm) |
| Raumfläche | Polygonfläche in mm² auf dem Blatt x `scale²`, Anzeige in m² (auch der Raumwert `A`) |
| Pfostenabstand des Geländers | `spacing / scale` mm auf dem Blatt |
| Tür-/Fensterbreite | Boxbreite x `scale` (in den Breiten-Aktionen und der Eingabe „Breite…“ angezeigt) |

Freies Skalieren von Box-Objekten ändert nur `Size`, nie Parameter.

## Objekttypen

| Id | Name (EN / DE) | Geometrie | Ansichten | Hinweise |
|---|---|---|---|---|
| `arch.wall` | Wall / Wand | Punkte (Achse) | Grundriss, Ansicht | `thickness` (240), `kind` Außen-/Innen-/Trennwand, `closed`, `height`, `scale` |
| `arch.room` | Room / Raum | Punkte (Polygon) | Grundriss | Name (Text oder `function`), Fläche in m², getönter Boden; Werte A, h, V (siehe [Raumwerte](#raumwerte)) |
| `arch.beam` | Beam / Unterzug | Punkte | Grundriss | gestrichelter Umriss + Achse, `width` |
| `arch.column` | Column / Stütze | Box | Grundriss | rechteckig/rund; Stahlbeton (schwarz), Mauerwerk (schraffiert), Stahl (I-Profil) |
| `arch.door.single` / `.double` / `.sliding` / `.opening` | Türen | Box | Grundriss, Ansicht | `hingeRight`, `swingOut`, Aufschlagbogen |
| `arch.window.single` / `.double` / `.french` | Fenster | Box | Grundriss, Ansicht | `hingeRight`, `flip` (innen/außen), Fensterbank |
| `arch.stair.straight` / `.l` / `.u` / `.spiral` | Treppen | Box | Grundriss | `steps`, `down` (Lauflinie umgekehrt) |
| `arch.lift` | Lift (shaft with car) / Aufzug (Schacht mit Kabine) | Box | Grundriss | Schachtumriss, Kabine mit Diagonalkreuz, `counterweight` (Gegengewicht), `door` (zweiteilige Schachttür); 2,0 x 2,2 m |
| `arch.ramp` | Ramp / Rampe | Box | Grundriss | Umriss, Pfeil bergauf (Startkreis wie bei Treppen), `slope` in % (6, angezeigt als „6 %“, Dezimalkomma auf Deutsch), `down`, `showSlope`; verläuft entlang der längeren Boxseite |
| `arch.railing` | Railing / Geländer | Punkte | Grundriss | mittlere Linie mit Pfosten an den Eckpunkten und alle `spacing` (1000 mm im Planmaßstab), `posts`, `scale`; Schienen je Segment |
| `arch.shaft` | Shaft (building) / Schacht (Gebäude) | Box | Grundriss | Rechteck mit durchgezogenem Diagonalkreuz (`cross`) |
| `arch.void` | Slab opening (void) / Deckenöffnung (Luftraum) | Box | Grundriss | dünner Umriss mit gestrichelten Diagonalen (verdeckte Linie) |
| `arch.roof` | Roof plan / Dachaufsicht | Box | Grundriss | `kind` Satteldach / Walmdach, `ridge` parallel zur langen oder kurzen Seite (Satteldach), `arrows` (Gefällepfeile), `walls` (Außenwände darunter, gestrichelt) |
| `arch.furniture.*` | Bett, Sofa, Tisch, Schreibtisch, Schrank, Küchenzeile, Spüle, Herd, Kühlschrank | Box | Grundriss | Bett `pillows`, Sofa `seats`, Tisch `chairs`/`round`, Spüle `basins` |
| `arch.sanitary.*` | WC, Waschbecken, Dusche, Badewanne | Box | Grundriss | |
| `arch.hvac.radiator` / `.heatpump` / `.boiler` | Heizungssymbole | Box | Grundriss | Wärmepumpe beschriftet WP/HP je nach UI-Sprache |
| `arch.hvac.pipe` | Pipe / Rohrleitung | Punkte | Grundriss | `medium` Vorlauf/Rücklauf/Kalt-/Warmwasser/Abwasser/Gas (Farbe + Strichart), `arrow` |
| `arch.site.north` | Nordpfeil | Box | Grundriss | |
| `arch.site.level` | Höhenkote | Box | Grundriss | `level` in m (`±0,00`, `+2,75`; Dezimalkomma auf Deutsch), Text überschreibt |
| `arch.site.section` | Schnittlinie | Punkte | Grundriss | Buchstabe, `flip` Blickrichtung, `full` Linie |
| `arch.site.boundary` | Grundstücksgrenze | Punkte | Grundriss | Strich-Punkt-Punkt-Polygon, Grenzpunkte |
| `arch.site.parking` | Stellplatz | Box | Grundriss | 2,5 x 5 m |
| `arch.site.tree` | Baum | Box | Grundriss | Laubbaum, Nadelbaum, Strauch |

Ansichten tragen abstrakte Icons: `plan` nutzt `IconIds.Plan`, `elevation` nutzt `IconIds.Front`.

## Wände

Eine Wand ist ein Linienzug aus Achspunkten. Sie wird als zwei Ansichtskanten (breite Volllinie) mit dem
artabhängigen Kern dazwischen gezeichnet: Außenwand = 45°-Schraffur auf Hellgrau, Innenwand (tragend) =
45°-Schraffur, Trennwand = hellgraue Fläche. Ecken innerhalb einer Wand werden auf Gehrung geschnitten; eine
`closed`-Wand bildet einen Ring (auch wenn der letzte Punkt auf den ersten zurückführt).

Anschlüsse zwischen getrennten Wänden auf derselben Seite werden beim Rendern aufgelöst:

- **L-Anschluss**: Genau eine andere Wand endet am selben Punkt (Toleranz 0,25 mm) - beide Enden auf Gehrung,
  gemeinsame Eckpunkte, keine Abschlusslinien.
- **T-Anschluss**: Ein Wandende liegt auf der Achse einer anderen Wand - das Ende wird bis zur Wandfläche gekürzt
  und die Kante der durchgehenden Wand unterbrochen, sodass die Kerne ineinander übergehen.
- **Mehrere Wände** an einem Punkt: Enden bleiben offen (ohne Abschlusslinie).
- **Freies Ende**: mit Abschlusslinie geschlossen.

Fanganker: Jeder Punkt ist `End` (aus der Registry) und `Mount`; jedes Segment ist eine `Axis`. Wandenden fangen
daher auf andere Wandenden/Eckpunkte, Öffnungen fangen auf Wandachsen.

Da Anschlüsse aus den Nachbarn berechnet werden, setzen Wände `DependsOnNeighbours` und melden ihre **abhängigen
Objekte** (`ObjectType.Dependents`): die angeschlossenen Wände (L-/T-Anschlüsse in beide Richtungen) sowie Türen und
Fenster, deren Mitte auf der Wand liegt. Die Engine liefert diese Ids bei jeder Bearbeitung (`dependents` im
Editorzustand), sodass Hosts sie im selben Rückgängig-Schritt neu zeichnen (siehe
[Überblick](../architecture/overview.md#7-engine-api-aktionen-änderungsmengen-und-dokumenteinstellungen)).

## Türen und Fenster

Box-Objekte, deren lokale x-Achse entlang der Wand läuft. Der `Hub`-Anker sitzt in der Boxmitte und fängt auf
eine Wand-`Axis` (bei senkrechten Wänden die Öffnung drehen). In der Öffnung verdeckt ein deckendes weißes Band
die Wand; seine Dicke stammt von der Wand unter der Mitte oder, ohne Wand, aus dem optionalen Parameter
`wallThickness`. Die Öffnung muss deshalb in der Z-Reihenfolge über der Wand liegen (später eingefügt). Auf
farbigen Folienhintergründen ist das weiße Band außerhalb der Wand sichtbar.

**An Wand ausrichten** (dokumentbezogene Kontextaktion): dreht die Öffnung in die Richtung des nächsten
Wandabschnitts in Reichweite (halbe Wanddicke plus halbe kleinere Boxseite) und zentriert sie auf der Wandachse. Von
den beiden Wandrichtungen gewinnt die, die näher an der aktuellen Drehung liegt, damit die Aufschlagseite erhalten
bleibt. Ohne Wand in Reichweite wird die Aktion ausgegraut angeboten.

**Ansichten und Größe:** Grundriss und Ansicht haben eigene bevorzugte Größen (Türen 2,10 m hoch, Fenster 1,25 m,
Fenstertüren 2,20 m in der Ansicht). Beim Wechsel der Ansicht bleibt die reale Breite erhalten; die andere Seite
übernimmt die bevorzugte Größe der Zielansicht im Planmaßstab des Objekts (im Grundriss wächst die Tiefe wie bei den
Breitenschritten mit den Flügeln).

- Einflügelige Tür: Türblatt + Viertelkreis-Aufschlag (elliptisch bei gestreckter Box); Anschlag- und
  Aufschlagseite sind Schalter.
- Zweiflügelige Tür, Schiebetür (zwei Paneele und Richtungspfeil), Öffnung ohne Tür (Sturz gestrichelt).
- Fenster: Laibungslinien, doppelte Glaslinie, Fensterbank außen; zweiflügelig mit Pfosten; Fenstertür mit zwei
  aufschlagenden Flügeln. Ansichten zeigen Rahmen, Glas und das gestrichelte Öffnungsdreieck, dessen Spitze zur
  Bandseite zeigt.

## Aufzüge, Rampen, Geländer, Schächte, Öffnungen und Dächer

- **Aufzug:** Schachtumriss (mittlere Linie), die Kabine als dünnes Rechteck mit Diagonalkreuz, das Gegengewicht als
  grauer Block hinter der Kabine und eine zweiteilige Schiebe-Schachttür davor. Ohne Gegengewicht oder Tür nimmt die
  Kabine den Platz ein.
- **Rampe:** Umriss, eine Lauflinie vom Startkreis zu einem Pfeil bergauf (`down` kehrt sie um) und die Steigung in
  Prozent darüber. Hochkant gestellte Boxen verlaufen entlang y.
- **Geländer:** ein Punktobjekt wie Wände und Leitungen (Ecken, Verlängern, Schienen je Segment zum Fangen); Pfosten
  sind kleine gefüllte Punkte an jedem Eckpunkt und dazwischen gleichmäßig im Abstand von etwa `spacing` realen
  Millimetern.
- **Schacht / Deckenöffnung:** Rechteck mit durchgezogenem Diagonalkreuz (Schacht) oder gestrichelten Diagonalen
  (Öffnung in der Decke, Luftraum).
- **Dachaufsicht:** Traufumriss und First; ein Walmdach ergänzt die vier Grate unter 45° im Grundriss, also gleiche
  Neigung aller Dachflächen, daher verläuft sein First entlang der längeren Seite und ist (Länge − Breite) lang, ein
  Quadrat ergibt ein Zeltdach. Gefällepfeile zeigen jede Dachfläche hinab. Der First eines Satteldachs kann entlang
  beider Seiten laufen.

Keines dieser Elemente überschneidet sich mit den Modulen Haustechnik oder Karten, daher tragen sie kein `Alternative`.

## Raumwerte

Räume sind Elemente mit Werten (`IValueType`, siehe Entwurf der technischen Berechnungen): Grundfläche `A` (m²),
Raumhöhe `h` (m) und Volumen `V` (m³) mit der Beziehung V = A·h. Die Fläche kommt aus dem Umriss: Der Raum meldet eine
eigene Beziehung (`IValueLinks`, „Grundfläche aus dem Umriss im Planmaßstab“), die `A` auf die Polygonfläche mal dem
Quadrat des Planmaßstabs setzt (eigener `scale`, sonst `DocumentSettings.PlanScale`, sonst 1:100). Die Höhe ist der
optionale Zahlenparameter `height` (m) ohne Vorgabewert, damit der Wertvorgabe h = 2,5 m gilt, bis eine Höhe oder ein
Volumen eingegeben wird; ein eingegebenes Volumen (Textparameter `volume`, z. B. „33 m³“) ergibt dann die Höhe. Eine
eingegebene Fläche, die dem Umriss widerspricht, wird als Widerspruch gemeldet. `showValues` zeichnet das Werte-Label
unter den Raum. Beispiel: Ein Umriss von 40 x 30 mm bei 1:100 ist 4 m x 3 m, A = 12 m², mit h = 2,5 m ist V = 30 m³.

Schlüssel und Größenarten (`A` Fläche, `h` Länge, `V` Volumen) entsprechen den Raumwerten der Haustechnik
(Luftdurchlass), sodass eine spätere modulübergreifende Verknüpfung (`NetKeys`) sie weitergeben kann; Core wurde dafür
nicht geändert.

## Kontextaktionen

| Typ | Aktionen |
|---|---|
| Wand | dicker/dünner (Standarddicken 75 ... 490 mm), „Dicke…“ (Eingabe, mm), Wandart ▾ (Auswahl), geschlossen |
| Türen | Anschlagseite wechseln, Aufschlagrichtung wechseln, breiter/schmaler (±125 mm; Flügeltiefe wächst mit), „Breite…“ (Eingabe, mm), an Wand ausrichten |
| Fenster | Anschlagseite wechseln, innen/außen tauschen, breiter/schmaler, „Breite…“ (Eingabe, mm), an Wand ausrichten |
| Treppen | Laufrichtung umkehren, mehr/weniger Stufen |
| Aufzug, Rampe, Geländer, Schacht | Gegengewicht, Schachttür; Richtung umkehren, Steigung anzeigen; Pfosten; Diagonalkreuz |
| Dachaufsicht | Dachform ▾ (Auswahl), Gefällepfeile, Außenwände darunter |
| Raum | „Name…“ (Eingabe), Fläche anzeigen, getönter Boden |
| Stütze, Baum, Leitung | Form ▾ / Material ▾, Art ▾, Medium ▾ (Auswahlen; die Medien zeigen ihre Linie); Fließrichtungspfeil |
| Schnittlinie, Grundstücksgrenze | Blickrichtung umkehren; geschlossen, Grenzpunkte |

Eingabeaktionen („…“) fragen einen Wert ab: das COM-Add-in öffnet seinen Eingabedialog, der Web-Aufgabenbereich ein
Inline-Feld. Längen akzeptieren `1010`, `1010 mm`, `101 cm`, `1,01 m` oder `1.01 m`; alles andere wird mit einer
Meldung abgelehnt und ändert nichts. Die Breiten-/Dickenschritte verwenden `IconIds.Increase` / `IconIds.Decrease`,
die Eingaben `IconIds.Text` und das Ausrichten `IconIds.Rotate`.

## Host-Hinweise

Die Ausgabe besteht nur aus nativen Primitiven (Linien, Linienzüge, Polygone, Rechtecke, Ellipsen,
Bézierpfade, Text) und expliziten Schraffurlinien und ist damit in jedem Host nativ darstellbar. Wände und
Öffnungen rendern kontextabhängig (Nachbarwände, Dicke der Trägerwand):

- **Web-Aufgabenbereich:** Die Live-Synchronisation zeichnet die geänderten Einheiten plus die `dependents` der Engine
  neu.
- **PowerPoint-COM:** Jede Einheit behält ihr eigenes Modell; Einheiten mit Wänden oder Öffnungen werden mit den
  übrigen Einheiten der Folie als Kontext gerendert (gespeichert werden nur die eigenen Objekte), und nach
  Bearbeiten, Einfügen, Verschieben oder Löschen werden die abhängigen Einheiten im selben Rückgängig-Eintrag neu
  gezeichnet. Native PowerPoint-Verschiebungen (Ziehpunkte, Pfeiltasten) zeichnen abhängige Einheiten erst bei der
  nächsten InventaSketch-Bearbeitung neu.

## Tests und Galerie

`tests/InventaSketch.Modules.Architecture.Tests` prüft Registrierung, zweisprachige Namen, jedes Box-Element in
seiner Box (auch gestreckt) und Punktobjekte nahe ihren Punkten, Wandanschlüsse (L, T, geschlossen), Endpunktfang
von Wänden, Öffnungs-Hubs auf Wandachsen, Raumfläche, Höhenkotentexte, Kontextaktionen und den JSON-Roundtrip;
`BuildingTests` prüft Aufzug, Rampe (Steigungstext in beiden Sprachen, Richtung, hochkant), Geländerpfosten nach
Abstand und Planmaßstab, Diagonalen von Schacht und Deckenöffnung, Sattel- und Walmdach sowie die Raumwerte (12 m² und
30 m³ für ein bekanntes Rechteck bei 1:100, eingegebene Höhe, Dokument- und eigener Planmaßstab, eingegebenes Volumen
ergibt die Höhe, widersprüchliche Fläche, Werte-Label, JSON);
`DocumentAwareTests` prüft das Ausrichten an Wänden, abhängige Objekte, den Dokument-Planmaßstab, Eingabeaktionen
und die Größen je Ansicht.
Der Galerietest schreibt `gallery/*.html` (alle Typen, Vorlagen, Anschlussstudie und ein Beispielhaus auf Englisch
und Deutsch).
