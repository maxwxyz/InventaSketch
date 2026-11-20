# Modul Grundrisse & Gebäude (`architecture`)

Schematische Hausplanung für Folien und schnelle technische Skizzen: Wände mit sauberen Anschlüssen, Räume mit
Fläche, Türen, Fenster, Treppen, Stützen, Unterzüge, Möbel, Sanitär, Heizungs-/Leitungssymbole und
Lageplanzeichen. Es ist **kein BIM**: Hinter dem Plan steht kein Gebäudemodell, nur gut gezeichnete, editierbare
Symbole.

Elektro-Grundrisssymbole liegen im Elektro-Modul, Bemaßungen im Beschriftungsmodul; dieses Modul dupliziert sie
nicht.

## Planmaßstab

Jedes Objekt ist ohne Parameter einfügbar. Die Bibliotheksgrößen sind für **1:100** ausgelegt (1 mm auf dem Blatt
entspricht 100 mm in der Realität). Objekte mit realen Maßen haben den optionalen erweiterten Parameter `scale`
(Maßstabszahl, Standard 100):

| Größe | Umrechnung |
|---|---|
| Wanddicke, Unterzugbreite | `thickness / scale` mm auf dem Blatt (240 mm bei 1:100 = 2,4 mm) |
| Raumfläche | Polygonfläche in mm² auf dem Blatt x `scale²`, Anzeige in m² |
| Tür-/Fensterbreite | Boxbreite x `scale` (in den Breiten-Aktionen angezeigt) |

Freies Skalieren von Box-Objekten ändert nur `Size`, nie Parameter.

## Objekttypen

| Id | Name (EN / DE) | Geometrie | Ansichten | Hinweise |
|---|---|---|---|---|
| `arch.wall` | Wall / Wand | Punkte (Achse) | Grundriss, Ansicht | `thickness` (240), `kind` Außen-/Innen-/Trennwand, `closed`, `height`, `scale` |
| `arch.room` | Room / Raum | Punkte (Polygon) | Grundriss | Name (Text oder `function`), Fläche in m², getönter Boden |
| `arch.beam` | Beam / Unterzug | Punkte | Grundriss | gestrichelter Umriss + Achse, `width` |
| `arch.column` | Column / Stütze | Box | Grundriss | rechteckig/rund; Stahlbeton (schwarz), Mauerwerk (schraffiert), Stahl (I-Profil) |
| `arch.door.single` / `.double` / `.sliding` / `.opening` | Türen | Box | Grundriss, Ansicht | `hingeRight`, `swingOut`, Aufschlagbogen |
| `arch.window.single` / `.double` / `.french` | Fenster | Box | Grundriss, Ansicht | `hingeRight`, `flip` (innen/außen), Fensterbank |
| `arch.stair.straight` / `.l` / `.u` / `.spiral` | Treppen | Box | Grundriss | `steps`, `down` (Lauflinie umgekehrt) |
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

## Türen und Fenster

Box-Objekte, deren lokale x-Achse entlang der Wand läuft. Der `Hub`-Anker sitzt in der Boxmitte und fängt auf
eine Wand-`Axis` (bei senkrechten Wänden die Öffnung drehen). In der Öffnung verdeckt ein deckendes weißes Band
die Wand; seine Dicke stammt von der Wand unter der Mitte oder, ohne Wand, aus dem optionalen Parameter
`wallThickness`. Die Öffnung muss deshalb in der Z-Reihenfolge über der Wand liegen (später eingefügt). Auf
farbigen Folienhintergründen ist das weiße Band außerhalb der Wand sichtbar.

- Einflügelige Tür: Türblatt + Viertelkreis-Aufschlag (elliptisch bei gestreckter Box); Anschlag- und
  Aufschlagseite sind Schalter.
- Zweiflügelige Tür, Schiebetür (zwei Paneele und Richtungspfeil), Öffnung ohne Tür (Sturz gestrichelt).
- Fenster: Laibungslinien, doppelte Glaslinie, Fensterbank außen; zweiflügelig mit Pfosten; Fenstertür mit zwei
  aufschlagenden Flügeln. Ansichten zeigen Rahmen, Glas und das gestrichelte Öffnungsdreieck, dessen Spitze zur
  Bandseite zeigt.

## Kontextaktionen

| Typ | Aktionen |
|---|---|
| Wand | dicker/dünner (Standarddicken 75 ... 490 mm), Art durchschalten, geschlossen |
| Türen | Anschlagseite wechseln, Aufschlagrichtung wechseln, breiter/schmaler (±125 mm; Flügeltiefe wächst mit) |
| Fenster | Anschlagseite wechseln, innen/außen tauschen, breiter/schmaler |
| Treppen | Laufrichtung umkehren, mehr/weniger Stufen |
| Raum | Fläche anzeigen, getönter Boden |
| Stütze, Baum, Leitung | Form/Material, Art, Medium durchschalten; Fließrichtungspfeil |
| Schnittlinie, Grundstücksgrenze | Blickrichtung umkehren; geschlossen, Grenzpunkte |

Breiten-/Dickenaktionen verwenden die Icon-Ids `cmd-plus` / `cmd-minus`, die noch nicht Teil von `IconIds` sind
(Hosts zeigen das neutrale Icon).

## Host-Hinweise

Die Ausgabe besteht nur aus nativen Primitiven (Linien, Linienzüge, Polygone, Rechtecke, Ellipsen,
Bézierpfade, Text) und expliziten Schraffurlinien und ist damit in jedem Host nativ darstellbar. Wände und
Öffnungen rendern kontextabhängig (Nachbarwände, Dicke der Trägerwand): Ändert sich eine Wand oder Öffnung,
sollten Hosts die Wände und Öffnungen derselben Seite neu zeichnen, nicht nur das geänderte Objekt.

## Tests und Galerie

`tests/InventaSketch.Modules.Architecture.Tests` prüft Registrierung, zweisprachige Namen, jedes Box-Element in
seiner Box (auch gestreckt) und Punktobjekte nahe ihren Punkten, Wandanschlüsse (L, T, geschlossen), Endpunktfang
von Wänden, Öffnungs-Hubs auf Wandachsen, Raumfläche, Höhenkotentexte, Kontextaktionen und den JSON-Roundtrip.
Der Galerietest schreibt `gallery/*.html` (alle Typen, Vorlagen, Anschlussstudie und ein Beispielhaus auf Englisch
und Deutsch).
