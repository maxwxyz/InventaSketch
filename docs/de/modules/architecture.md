# Modul Grundrisse & Gebäude (`architecture`)

Schematische Hausplanung für Folien und schnelle technische Skizzen: Wände mit sauberen Anschlüssen, Räume mit
Fläche, Türen, Fenster, Treppen, Rampen, Aufzüge, Geländer, Schächte, Deckenöffnungen, Dachaufsichten, Stützen,
Unterzüge, Möbel, Sanitär, Heizungs-/Leitungssymbole, Lageplanzeichen und Listen (Raumflächen, Türen und Fenster).
Räume, Wände, Treppen, Rampen und Listen tragen Werte (siehe [Werte der Bauteile](#werte-der-bauteile-26112026-runde-7)).
Es ist **kein BIM**:
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
| Wandlänge, Auftritt, Rampenlänge | mm auf dem Blatt x `scale` (Werte `l`, `g`, `l`) |
| Raumfläche | Polygonfläche in mm² auf dem Blatt x `scale²`, Anzeige in m² (auch der Raumwert `A`) |
| Pfostenabstand des Geländers | `spacing / scale` mm auf dem Blatt |
| Tür-/Fensterbreite | Boxbreite x `scale` (in den Breiten-Aktionen und der Eingabe „Breite…“ angezeigt) |

Freies Skalieren von Box-Objekten ändert nur `Size`, nie Parameter.

## Objekttypen

| Id | Name (EN / DE) | Geometrie | Ansichten | Hinweise |
|---|---|---|---|---|
| `arch.wall` | Wall / Wand | Punkte (Achse) | Grundriss, Ansicht | `thickness` (240), `kind` Außen-/Innen-/Trennwand, `closed`, `height`, `scale`; Werte l, h, A, A_Öff, A_netto, U, Δθ, Φ_T (`uValue`, `deltaT`, `heatLoss`) |
| `arch.room` | Room / Raum | Punkte (Polygon) | Grundriss | Name (Text oder `function`), Fläche in m², getönter Boden; Werte A, h, V (siehe [Raumwerte](#raumwerte)) |
| `arch.beam` | Beam / Unterzug | Punkte | Grundriss | gestrichelter Umriss + Achse, `width` |
| `arch.column` | Column / Stütze | Box | Grundriss | rechteckig/rund; Stahlbeton (schwarz), Mauerwerk (schraffiert), Stahl (I-Profil) |
| `arch.door.single` / `.double` / `.sliding` / `.opening` | Türen | Box | Grundriss, Ansicht | `hingeRight`, `swingOut`, Aufschlagbogen |
| `arch.window.single` / `.double` / `.french` | Fenster | Box | Grundriss, Ansicht | `hingeRight`, `flip` (innen/außen), Fensterbank |
| `arch.stair.straight` / `.l` / `.u` / `.spiral` | Treppen | Box | Grundriss | `steps` (Steigungen), `down` (Lauflinie umgekehrt), `storeyHeight` (m), `riser`, `scale`; Werte H, s, a, 2s + a, n, α |
| `arch.lift` | Lift (shaft with car) / Aufzug (Schacht mit Kabine) | Box | Grundriss | Schachtumriss, Kabine mit Diagonalkreuz, `counterweight` (Gegengewicht), `door` (zweiteilige Schachttür); 2,0 x 2,2 m |
| `arch.ramp` | Ramp / Rampe | Box | Grundriss | Umriss, Pfeil bergauf (Startkreis wie bei Treppen), `slope` in % (6, angezeigt als „6 %“, Dezimalkomma auf Deutsch), `down`, `showSlope`, `scale`; verläuft entlang der längeren Boxseite; Werte l, s, Δh, α |
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
| `arch.schedule` | Schedule (rooms, doors and windows) / Liste (Räume, Türen und Fenster) | Box | Grundriss | `list` Räume / Öffnungen, `title`, `textSize`; Element `arch.schedule.openings` (Tür- und Fensterliste); siehe [Listen](#listen) |

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

Optionale Heizlast-Schätzung (seit 26.11.2026): `Φ_HL` = q·A (W, Kartenwert) aus der spezifischen Heizlast `q`
(W/m², Panel; Textparameter `heatLoadDensity` und `heatLoad`, beide erweitert), Beziehung `arch.room.heatload` [S30].
Sie ist eine grobe Auslegungsschätzung mit Handbuchwerten, keine Heizlastberechnung nach EN 12831; eine eingegebene
Φ_HL ergibt q.

Schlüssel und Größenarten (`A` Fläche, `h` Länge, `V` Volumen, `PhiHL` Wärmestrom) entsprechen den Raumwerten der
Haustechnik: Elemente der Haustechnik im Raum (Mitte innerhalb des Umrisses auf derselben Seite, bei verschachtelten
Räumen der kleinste) übernehmen sie über das Enthaltensein, der Luftdurchlass A, h und V, der Heizkörper Φ_HL
(BsValues, Verknüpfung `bs.room.link`; engineering-calculations.md Abschnitt 6.1). Der Raum braucht dafür keine Leitung
und kennt das Haustechnik-Modul nicht.

## Werte der Bauteile (26.11.2026, Runde 7)

Alle Werte sind optional: Jedes Element wird weiterhin ohne Parameter eingefügt, und Werte aus der Zeichnung sind
schreibgeschützt (sie ändern sich mit der Zeichnung, nie umgekehrt). Jedes Element mit Werten bietet das Werte-Label
(`showValues`).

### Treppen: Steigung, Auftritt und Schrittmaßregel

| Schlüssel | Größe (Symbol EN / DE) | Herkunft |
|---|---|---|
| `H` | Geschosshöhe (H) | Parameter `storeyHeight` (m), Vorgabe 2,75 m (die Vorgabe-Wandhöhe) |
| `n` | Anzahl Steigungen (n) | Parameter `steps` (die gezeichneten Stufen) |
| `h` | Steigungshöhe (h / s) | `H/n` oder eingegeben (`riser`, z. B. „17,5 cm“; dann H = n·s) |
| `g` | Auftritt (g / a) | schreibgeschützt: der gezeichnete Auftritt entlang der Lauflinie im Planmaßstab (gerade: Breite/n; L und U: Auftritt des ersten Laufs; Wendeltreppe: Bogen je Stufe bei 60 % des Radius) |
| `step` | Schrittmaß (2h + g / 2s + a) | schreibgeschützt, Relation `arch.stair.steplength` |
| `alpha` | Steigungswinkel α | schreibgeschützt, tan α = s/a |

Beispiel: Die gerade Treppe (36 x 10 mm bei 1:100, 14 Steigungen) hat a = 3,60 m/14 = 25,7 cm; mit H = 2,75 m ist
s = 19,6 cm und 2s + a = 65 cm.

Der Hinweis **`arch.stair.steprule`** meldet ein Schrittmaß außerhalb von 59 … 65 cm (Schrittmaßregel 2s + a ≈ 63 cm,
eine alte Faustregel für bequeme Treppen; nationale Treppenregeln haben eigene Grenzen und werden nicht wiedergegeben).
Er meldet sich erst, wenn eine Höhe gegeben ist (Geschosshöhe oder Steigung eingegeben oder verknüpft), weil die
Vorgabehöhe nur geschätzt ist. Die Aktion **„Stufen nach Schrittmaßregel (n)“** setzt die Stufenzahl, deren Schrittmaß
2·H/n + a(n) für die gezeichnete Treppe 63 cm am nächsten kommt (ausgegraut, wenn die aktuelle Zahl schon die beste ist).

### Rampen: Höhenunterschied

`l` Rampenlänge (schreibgeschützt, längere Boxseite im Planmaßstab), `s` Steigung (Parameter `slope` in %), `dh`
Höhenunterschied Δh = s·l (schreibgeschützt), `alpha` Neigungswinkel α = arctan s. Beispiel: 60 mm bei 1:100 = 6 m bei
6 % ergibt Δh = 0,36 m. Der Hinweis **`arch.ramp.slope`** meldet eine Steigung über 6 %, eine übliche Grenze für Rampen,
die mit dem Rollstuhl befahren werden; nationale Regeln unterscheiden sich (lange Rampen brauchen zudem Zwischenpodeste),
daher nennt die Meldung sie als üblichen Wert, nicht als Anforderung.

### Wände: Flächen und Transmissionswärmeverlust

| Schlüssel | Größe | Herkunft |
|---|---|---|
| `l` | Wandlänge | schreibgeschützt: Achslänge (geschlossene Wände: die Schleife) im Planmaßstab |
| `h` | Wandhöhe | Parameter `height` (mm, 2750) |
| `A` | Wandfläche (brutto) | `A = l·h` |
| `Aopen` | Fläche der Türen und Fenster | schreibgeschützt: Σ Breite x Höhe der Türen und Fenster, deren Mitte auf der Wand liegt (Breite im Planmaßstab; Höhe der Ansichtsbox in der Ansicht, sonst die bevorzugte Ansichtshöhe: Türen 2,10 m, Fenster 1,25 m, Fenstertüren 2,20 m) |
| `Anet` | Wandfläche (netto) | `A − A_Öff` |
| `U` | U-Wert | Textparameter `uValue` (W/(m²·K)) |
| `dT` | Temperaturdifferenz Δθ | Textparameter `deltaT` (K) |
| `PhiT` | Transmissionswärmeverlust Φ_T | `U·A_netto·Δθ` [S29] oder eingegeben (`heatLoss`, dann folgt U oder Δθ) |

Gültigkeit von `arch.wall.transmission`: stationär, ohne Wärmebrücken, Nettofläche ohne Türen und Fenster; keine
Heizlastberechnung nach EN 12831. Beispiel: eine 6 m lange, 2,75 m hohe Wand mit einer Tür 0,90 x 2,10 m: A = 16,5 m²,
A_Öff = 1,89 m², A_netto = 14,61 m²; mit U = 0,28 W/(m²·K) und Δθ = 32 K ist Φ_T = 131 W. Eine Tür oder ein Fenster nennt
seine Wand als abhängiges Objekt, solange die Wand ihr Werte-Label zeigt, damit das Label folgt, wenn die Öffnung
verschoben oder verbreitert wird.

### Listen

`arch.schedule` ist eine Tabelle, die aus der Seite gezeichnet wird, auf der sie liegt (eine Seite = ein Geschoss):

- **Räume** (`list = rooms`, Vorgabe): Nummer, Name und Grundfläche jedes Raums der Seite (Umriss in seinem
  Planmaßstab), die Summe „Grundfläche“ und, wenn die Seite geschlossene Außenwände hat, die **Brutto-Grundfläche**:
  die Fläche innerhalb der Außenflächen der geschlossenen Außenwandschleifen. Werte: `Asum` (ΣA), `Agross`, `count`
  (schreibgeschützt).
- **Türen und Fenster** (`list = openings`, Element `arch.schedule.openings`): gleiche Öffnungen (gleicher Typ, Breite
  auf 5 mm und Höhe) werden mit Anzahl, Typname und Breite x Höhe in Metern zusammengefasst; die Fußzeile nennt Anzahl
  und Fläche insgesamt. Werte: `Aopen`, `count`.

Räume, Türen, Fenster und Wände nennen die Listen ihrer Seite als abhängige Objekte, damit die Tabelle im selben
Rückgängig-Schritt mit ihnen neu gezeichnet wird (`DependsOnNeighbours` der Liste). Die Zeilen teilen sich die
Boxhöhe; bei vielen Zeilen wird die Schrift kleiner (Box vergrößern). Namen und Zahlen folgen der UI-Sprache
(Dezimalkomma auf Deutsch).

## Kontextaktionen

| Typ | Aktionen |
|---|---|
| Wand | dicker/dünner (Standarddicken 75 ... 490 mm), „Dicke…“ (Eingabe, mm), Wandart ▾ (Auswahl), geschlossen |
| Türen | Anschlagseite wechseln, Aufschlagrichtung wechseln, breiter/schmaler (±125 mm; Flügeltiefe wächst mit), „Breite…“ (Eingabe, mm), an Wand ausrichten |
| Fenster | Anschlagseite wechseln, innen/außen tauschen, breiter/schmaler, „Breite…“ (Eingabe, mm), an Wand ausrichten |
| Treppen | Laufrichtung umkehren, mehr/weniger Stufen, Stufen nach Schrittmaßregel |
| Liste | Listet ▾ (Räume / Türen und Fenster), Titelzeile |
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
und die Größen je Ansicht. `ValueTests` (Runde 7) prüft Steigung, Auftritt, Schrittmaß und Steigungswinkel der Treppen
(Vorgabe, eingegebene Geschosshöhe und Steigung, Planmaßstab), den Schrittmaß-Hinweis und die Aktion dazu, den
Höhenunterschied und den Steigungshinweis der Rampe, Wandlänge, Brutto-, Öffnungs- und Nettofläche mit dem
Transmissionswärmeverlust (und U zurück aus einem eingegebenen Verlust), die Wand als abhängiges Objekt einer Tür,
beide Listen (Zeilen, Summen, Brutto-Grundfläche, Zusammenfassung, abhängige Objekte, viele Zeilen in der Box) und den
JSON-Roundtrip der Wertparameter.
Der Galerietest schreibt `gallery/*.html` (alle Typen, Vorlagen, Anschlussstudie und ein Beispielhaus mit beiden
Listen auf Englisch und Deutsch).
