# Modul Mechanik (`mechanical`): Gewindetriebe, Getriebeelemente und Gehäuse

Diese Seite beschreibt die Gewindetriebe, Getriebeelemente, das Getriebegehäuse und die Ölwanne des Mechanik-Moduls.
Zahnräder, Wellen, Lager, Kupplungen, Riemen, Verbindungselemente und Federn folgen denselben Konventionen und werden hier
nicht wiederholt.

> **Grenzen.** Alle Zeichnungen sind *illustrativ* und eigenständig gezeichnet (keine Normbilder übernommen).
> Gewindeformen, Kugelrillen, Wandlerbeschaufelung, Zykloidscheibe und Wellgetriebe-Geometrie folgen plausiblen
> Proportionen; sie sind kein Ergebnis einer Auslegungsrechnung. Maße bitte nicht aus ihnen ablesen.

Jedes Element lässt sich ohne Parametereingabe einfügen. Freies gleichmäßiges und ungleichmäßiges Skalieren ändert nur die
Boxgröße; Parameter bleiben unverändert. Schemaansichten (`stick`) folgen dem Stil der Getriebeschemata des Moduls: Welle
waagerecht, Element mittig auf der Wellenachse, Nabensymbole wie bei Zahnrädern. Elemente mit Nabe sitzen auf einer Welle:
ihr Anker `hub` rastet auf einer Wellenachse ein, die Welle nimmt sie beim Verschieben und Drehen mit, und allein bewegt
gleiten sie entlang der Welle.

## Gewindetriebe (Kategorie „Gewindetriebe“)

| Typ-ID | Name (EN / DE) | Ansichten | Anker |
|---|---|---|---|
| `mech.screwdrive.screw` | Ball screw / lead screw — Gewindespindel (Kugel- / Trapezgewinde) | `stick` Schema, `side` vereinfachte Gewindedarstellung, `detailed` Gewindeprofil | `axis` (Achse über die ganze Länge), `left`/`right` (Ende) |
| `mech.screwdrive.nut` | Screw-drive nut — Gewindetrieb-Mutter | `stick`, `side` (Flansch, Kugelrückführung), `section` (Kugeln oder Trapezgewinde) | `hub` (Nabe, Mitte), `mount` (Befestigung, hier sitzt der Schlitten) |

- Die Spindel wird wie eine Welle in echten Millimetern gezeichnet: Lagerzapfen an beiden Enden, dazwischen die
  Gewindelänge. Die Seitenansicht zeigt den Außendurchmesser als sichtbare und den Kerndurchmesser als schmale Linie; die
  Profilansicht zeigt Trapezzähne (Trapezgewinde) bzw. Kugelrillen (Kugelgewinde).
- **Die Mutter folgt der Spindelachse:** Ihre Nabe dockt an der Spindelachse an (Einrastart „Achse“); Verschieben oder
  Drehen der Spindel nimmt die Mutter mit, die Mutter allein gleitet entlang der Spindel. Lager, Kupplungen und
  Zahnräder docken an derselben Achse an.
- Parameter (alle optional): `kind` = `ball` | `lead` (beide Typen), `pitch` Steigung in mm je Umdrehung (Spindel;
  zeichnet die Gewindeteilung maßstäblich), `lefthand` (Spindel), `flange` (Mutter).
- Bibliothekselemente: `mech.ballscrew.stick|side|profile`, `mech.ballnut.stick|side|section`,
  `mech.leadscrew.stick|side|profile`, `mech.leadnut.stick|side|section`.

## Getriebeelemente

| Typ-ID | Name (EN / DE) | Kategorie | Ansichten | Parameter | Zusätzliche Anker |
|---|---|---|---|---|---|
| `mech.clutch.freewheel` | Freewheel (one-way clutch) / Freilauf | Kupplungen | `stick`, `section`, `axial` (Innenstern mit Rampen, Klemmrollen) | `reverse` (Sperrrichtung) | — |
| `mech.converter.torque` | Torque converter / Drehmomentwandler | Kupplungen | `stick` (Deckel, Pumpe, Turbine, Leitrad auf Freilauf, gestellfeste Leitradwelle), `section` | `lockup` (Überbrückungskupplung) | `ground` (Befestigung) |
| `mech.parklock` | Park lock (parking pawl) / Parksperre | Kupplungen | `stick`, `axial` (Parksperrenrad, Sperrklinke, Betätigungskegel) | `teeth` (6–30, Standard 12), `engaged` (Standard an) | `pivot` (Befestigung) |
| `mech.cvt` | CVT variator / CVT-Variator | Riemen- & Kettentriebe | `stick` (zwei Kegelscheibenpaare auf parallelen Wellen), `axial` (Scheiben mit Bandschleife) | `ratio` (0,4–2,5, Standard 1), `chain` | `hub` und `hub2` (Nabe, je Welle eine) |
| `mech.gearset.harmonic` | Strain wave gear / Wellgetriebe | Zahnräder | `stick` (Wellgenerator, Flexspline-Topf, gestellfester Circular Spline), `axial` | — | `ground` (Befestigung) |
| `mech.gearset.cycloidal` | Cycloidal drive / Zykloidgetriebe | Zahnräder | `stick` (Exzenter, Scheibe, Bolzenring, Abtriebsbolzen), `axial` (Zykloidscheibe im Bolzenring) | `lobes` (5–29, Standard 11; Bolzen = Höcker + 1) | `ground` (Befestigung) |

Alle außer dem CVT haben ihren Anker `hub` in der Boxmitte auf der Wellenachse. Der CVT hat je Scheibe eine Nabe und sitzt
damit gleichzeitig auf zwei Wellen; die Laufradien des Bandes folgen der Übersetzung (ihre Summe bleibt konstant).
Kontextaktionen: Freilauf „Sperrrichtung umkehren“, Wandler „Überbrückungskupplung an/aus“, Parksperre „Zähne ±1“ und
„eingelegt/gelöst“, CVT „Übersetzung ±0,2“ und „Kette/Schubgliederband“, Zykloidgetriebe „Höcker ±1“.

Bibliothekselemente: `mech.freewheel`, `mech.freewheel.section`, `mech.freewheel.axial`, `mech.converter`,
`mech.converter.lockup`, `mech.converter.section`, `mech.parklock`, `mech.parklock.axial`, `mech.cvt.stick`,
`mech.cvt.axial`, `mech.cvt.chain`, `mech.harmonic.stick`, `mech.harmonic.axial`, `mech.cycloidal.stick`,
`mech.cycloidal.axial`.

## Getriebegehäuse und Schmierung (Kategorie „Getriebegehäuse & Schmierung“)

| Typ-ID | Name (EN / DE) | Ansichten | Parameter | Anker |
|---|---|---|---|---|
| `mech.housing.gearbox` | Gearbox housing / Getriebegehäuse | `section` (schraffierte Wände), `stick` (Wandmittellinie) | `seats` (1–4 je Wand, Standard 2), `seatpos` (erweitert: Sitzhöhen 0…1 von oben, durch `;` getrennt) | `seat1l`, `seat1r`, … (Lagersitze: Befestigungskreise), `base` (Befestigungspunkt unten), Unterkante (Befestigungsstrecke) |
| `mech.housing.sump` | Oil sump with oil level / Ölwanne mit Ölstand | `section` (schraffierte Wanne, Öl, Ölstandslinie und -zeichen, Ablassschraube), `stick` | `level` (0,05–0,95 der Tiefe, Standard 0,6), `drain` (Standard an) | `mount`, `flange-l`, `flange-r` (Befestigung), `level` (Schiene entlang des Ölstands) |

- Beide werden in echten Millimetern gezeichnet: Die Wanddicke streckt sich nicht mit, das Gehäuse lässt sich frei
  skalieren.
- **Um Inhalt anpassen** (Kontextaktion des Gehäuses): passt das Gehäuse an die Teile an, die seine Box überdeckt
  (Zahnräder, Lager, Kupplungen …, plus 2 mm Abstand und die Wand), und legt für jede waagerechte Welle, die es kreuzt,
  eine Lagersitzreihe auf deren Höhe (höchstens vier). Wellen bestimmen die Sitzhöhen, nicht die Größe, da sie durch die
  Wände laufen. Gedrehte Gehäuse werden auf 0° zurückgesetzt.
- Lagersitze sind Kreise: Eine Lagernabe rastet beim Ablegen in der Nähe konzentrisch im Sitz ein. Ein Sitz ist keine
  Wellenachse; das Gehäuse nimmt das Lager also nicht mit und wird auch nicht von einer Welle mitgenommen.
- `mount` der Ölwanne dockt an `base` des Gehäuses an; Bezugslinien docken überall entlang des Ölstands an (Schiene
  `level`).
- Kontextaktionen: Gehäuse „Lagersitze ±1“ (setzt `seatpos` zurück), „Um Inhalt anpassen“; Ölwanne „Ölstand
  höher/niedriger“, „Ablassschraube an/aus“.

## Hinweise zu den Hosts

Alle neuen Elemente werden wie die übrigen Mechaniksymbole in native Host-Grundformen umgesetzt (Linien, Rechtecke,
Ellipsen, Polylinien und Polygone mit Schraffur); ein Vektorbild ist nicht nötig. Schnittschraffuren und die Ölfüllung
(waagerechte Schraffur) nutzen dieselbe Schraffurfüllung wie andere Schnitte; wo ein Host keine native Musterfüllung hat, zeichnet er die
Schraffur als einzelne Linien (siehe Host-Fähigkeitsmatrix).
