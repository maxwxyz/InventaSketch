# Modul Mechanik (`mechanical`): Gewindetriebe, Getriebeelemente, Mehrscheibentriebe und Gehäuse

Diese Seite beschreibt die Gewindetriebe, Getriebeelemente, Riemen- und Kettentriebe über mehrere Scheiben, Kleinteile
(Sicherungsring, Stifte, O-Ring), das Getriebegehäuse und die Ölwanne des Mechanik-Moduls. Zahnräder, Wellen, Lager,
Kupplungen, Riemen zwischen zwei Scheiben, Verbindungselemente und Federn folgen denselben Konventionen und werden hier
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
| `mech.gearset.harmonic` | Strain wave gear / Wellgetriebe | Zahnräder | `stick` (Wellgenerator, Flexspline-Topf, gestellfester Circular Spline), `axial` | — | `ground` (Befestigung); Schema: `in`, `out` (Nabe, Gliedanschlüsse) |
| `mech.gearset.cycloidal` | Cycloidal drive / Zykloidgetriebe | Zahnräder | `stick` (Exzenter, Scheibe, Bolzenring, Abtriebsbolzen), `axial` (Zykloidscheibe im Bolzenring) | `lobes` (5–29, Standard 11; Bolzen = Höcker + 1) | `ground` (Befestigung); Schema: `in`, `out` (Nabe, Gliedanschlüsse) |
| `mech.clutch.slip` | Slip clutch (torque limiter) / Rutschkupplung (26.11.2026) | Kupplungen | `stick` (zwei Reibscheiben mit Belag dazwischen, Druckfeder gegen einen Bund), `side` (Scheiben, Belag, Naben, Federpaket, Stellmutter) | — (Werteparameter `slipTorque`, siehe unten) | — |

Alle außer dem CVT haben ihren Anker `hub` in der Boxmitte auf der Wellenachse. Der CVT hat je Scheibe eine Nabe und sitzt
damit gleichzeitig auf zwei Wellen; die Laufradien des Bandes folgen der Übersetzung (ihre Summe bleibt konstant).

**Gliedanschlüsse von Getriebeeinheiten** (26.11.2026, Runde 4). Radsätze und der CVT haben Glieder, die mit
verschiedenen Drehzahlen laufen; jedes Glied sitzt deshalb über einen *Gliedanschluss* auf seiner eigenen Welle: einem
Nabenanker am Ende des Wellenstummels des Glieds auf der Achse. Eine Welle, deren Achse durch den Anschluss läuft (sie
rastet dort wie auf jeder Nabe ein), oder ein Teil, dessen Nabe konzentrisch dazu liegt, sitzt auf diesem Glied und
folgt den Werten der Einheit (siehe „Verknüpfungen zwischen Elementen“ unten).

| Typ | Anschlüsse (Id des Nabenankers → Glied) |
|---|---|
| `mech.gearset.planetary` | Schema: `sun` (rechter Stummel → *n*_s, *M*_s), `carrier` (linker Stummel → *n*_c, *M*_c), `ring` (äußeres Ende des oberen Hohlradbügels → *n*_r, *M*_r); Stirnansichten: die Mittenbohrung `hub` → Sonne; mit Außenverzahnung (`outer`): Teilkreis `ring-teeth` → Hohlrad |
| `mech.gearset.ravigneaux` | Schema: `sun1` (linker Stummel → kleine Sonne *n*_s1), `sun2` (rechter Stummel → große Sonne *n*_s2), `carrier` (oberes Ende des Stegbalkens → *n*_c), `ring` (äußeres Ende des oberen Hohlradbügels → *n*_r); Stirnansicht: `hub` → große Sonne; mit Außenverzahnung: Teilkreis `ring-teeth` → Hohlrad |
| `mech.gearset.differential` | `left` (→ *n*₁, *M*₁), `right` (→ *n*₂, *M*₂) an den äußeren Enden der Achswellen, `case` (Außenkante des Tellerrads, x = −13 der Entwurfsbox 40 → *n*_c, *M*_c) (Schema und Schnitt) |
| `mech.gearset.harmonic`, `mech.gearset.cycloidal` | Schema: `in` (linker Stummel → *n*₁, *M*₁), `out` (rechter Stummel → *n*₂, *M*₂); Stirnansichten: die Mittenbohrung `hub` → Antrieb |
| `mech.cvt` | `hub` (Primärscheibe → *n*₁, *M*₁), `hub2` (Sekundärscheibe → *n*₂, *M*₂) |

Die Mittenbohrung tritt hinter einen Stummelanschluss zurück: eine Welle, die von der Mitte über den Sonnenstummel nach
außen läuft, ist die Sonnenwelle. Eine Welle durch zwei Gliedanschlüsse würde die Einheit blockieren: sie sitzt auf
keinem Glied.

*Anschlüsse an der Außenkontur* (26.11.2026, Runde 6): Das Hohlrad des Planeten- und des Ravigneaux-Satzes, der Steg des
Ravigneaux-Satzes (seine Hohlwelle endet dort, wo die Welle der kleinen Sonne endet, er hat also keinen eigenen Stummel)
und der Differentialkorb haben ihren Anschluss an der Außenkontur des Glieds, abseits der Achse, sodass keine Welle entlang
der Achse ihn berührt. Im Getriebeschema wird eine Trommel oder Hohlwelle als Linie auf dem Radius des Glieds gezeichnet:
eine dort durch den Anschluss gezeichnete Welle (oder ein dazu konzentrisches Teil, die Nabe einer Bremse oder Kupplung)
dreht mit diesem Glied und übernimmt seine Drehzahl und sein Moment wie eine Welle an einem Stummel. Ein gehäusefestes
Hohlrad wird weiterhin am Satz eingetragen (*n*_r = 0).

*Außenverzahnung am Hohlrad* (Parameter `outer`, „Außenverzahnung am Hohlrad“, Vorgabe aus; Kontextschalter
„Außenverzahnung am Hohlrad an/aus“; Planeten- und Ravigneaux-Satz): Das Schema schließt den Hohlradbügel mit der
Radlinie der Außenverzahnung, die Stirnansicht zeichnet Kopf- und Teilkreis, die Ansicht mit Zähnen ihren Umriss (66 Zähne
des Zeichnungsmoduls). Ihr Teilkreis (Id `ring-teeth`) steht an erster Stelle der Teilkreise des Satzes, sodass ein daran
gesetztes Zahnrad mit dem Hohlradglied kämmt (Fangen, Kopplung, Werte, bewegter Räderzug); im Schema ein Strich-Zahnrad auf
einer parallelen Welle über dem Bügel. Die Außenverzahnung hat kein Einzelteil beim Zerlegen (ein Hohlrad-Teil hat nur
Innenzähne): ein Satz mit Außenverzahnung wird wie einer mit Stufenplaneten nicht zerlegt.

Kontextaktionen: Freilauf „Sperrrichtung umkehren“, Wandler „Überbrückungskupplung an/aus“, Parksperre „Zähne ±1“ und
„eingelegt/gelöst“, CVT „Übersetzung ±0,2“ und „Kette/Schubgliederband“, Zykloidgetriebe „Höcker ±1“, Planeten- und
Ravigneaux-Satz „Außenverzahnung am Hohlrad an/aus“, Zahnstange „Feste Zahnstange an/aus“.

Bibliothekselemente: `mech.freewheel`, `mech.freewheel.section`, `mech.freewheel.axial`, `mech.converter`,
`mech.converter.lockup`, `mech.converter.section`, `mech.parklock`, `mech.parklock.axial`, `mech.cvt.stick`,
`mech.cvt.axial`, `mech.cvt.chain`, `mech.harmonic.stick`, `mech.harmonic.axial`, `mech.cycloidal.stick`,
`mech.cycloidal.axial`, `mech.clutch.slip`, `mech.clutch.slip.side` (Suchbegriffe auch „torque limiter“,
„Überlastkupplung“).

Die Rutschkupplung ist eigenständig im Stil der Scheibenkupplung des Moduls gezeichnet; ein Normsymbol wird nicht
beansprucht. Ihre Bohrungen entsprechen der Scheibenkupplung (2,5 im 20 hohen Schema, 3 in der 24 hohen Seitenansicht),
so passt sie auf eine Welle und behält den Sitz nach einem Ansichtswechsel.

## Riemen- und Kettentriebe über mehrere Scheiben (Kategorie „Riemen- & Kettentriebe“)

| Typ-ID / Ansicht | Name (EN / DE) | Ansichten | Parameter | Anker |
|---|---|---|---|---|
| `mech.belt`, Ansicht `serpentine` | Serpentine belt drive / Riementrieb über mehrere Scheiben | `serpentine` (neben `open`, `crossed`, `loop`) | `route` (erweitert, siehe unten) | `hub1` … `hubN` (Nabe, je Verlaufsnabe eine, in Verlaufsreihenfolge) |
| `mech.chain`, Ansicht `serpentine` | Chain over several sprockets / Kette über mehrere Kettenräder | `serpentine` (Laschen und Rollen entlang des Verlaufs; neben `links`, `line`) | `route` | `hub1` … `hubN` |
| `mech.pulley.idler` | Idler pulley / Umlenkrolle | `axial`, `side`, `section` (Wälzlager vereinfacht), `stick` | `hub` (Standard `loose`), `back` (Standard an: glatte Rolle für den Riemenrücken; aus: Rillenrolle für die Laufseite) | `hub` (Nabe mit Bohrung), Teilkreis (Stirnansicht) |
| `mech.tensioner` | Belt tensioner / Riemenspanner | `axial` (gefederter Arm mit Spiralfeder am Drehpunkt, Spannrolle auf Wälzlager), `stick` (gelagerter Hebel, Rolle, Zugfeder zum Gestell) | — | `idler` (Nabe: der Riemen läuft darüber), `pivot` (Befestigung: Drehpunkt des Arms an Motor oder Gestell) |

- **Verlauf.** Riemen oder Kette laufen um eine geordnete Liste von Naben. Jede Nabe wird relativ zur Box gespeichert als
  `u,v,k` oder `u,v,k,b` (Einträge durch `;` getrennt): Mitte bei (u·B, v·H) mit u, v von −0,5 bis 0,5, Laufradius
  k·min(B, H), `b` = der Riemen läuft mit dem **Rücken** über diese Nabe (eine Umlenkrolle außerhalb der Schleife, die das
  Trum nach innen drückt). Werte außerhalb des Bereichs werden begrenzt, sodass der Trieb in seiner Box bleibt; Skalieren
  ändert die Größe, nie den Verlauf. Ohne den Parameter gilt der Standardverlauf: Kurbelwellenscheibe, Wasserpumpe,
  Generator und eine Umlenkrolle auf dem Rücken.
- **Geometrie.** Aufeinanderfolgende Naben werden durch ihre gemeinsame Tangente verbunden: die äußere Tangente zwischen
  zwei Naben, die der Riemen mit der Laufseite umschlingt, die gekreuzte Tangente neben einer Rückenrolle. Die Schleife
  läuft im Umlaufsinn des Polygons der Laufseiten-Naben um; diese werden in diesem Sinn umschlungen, Rückenrollen im
  entgegengesetzten.
- **Einrasten.** Jede Verlaufsnabe ist ein Nabenanker; Riemenscheiben, Kettenräder, Umlenkrollen und die Nabe `idler`
  des Riemenspanners rasten darauf ein wie auf die Endnaben eines Riemens zwischen zwei Scheiben.
- **Bearbeiten.** Griffe: jede Nabenmitte (frei) und ihr Laufradius (waagerechter Griff rechts am Kreis).
  Kontextaktionen in der Ansicht `serpentine`: „Umlenkrolle hinzufügen“ (eine Rückenrolle zwischen der letzten und der
  ersten Nabe), „Letzte Nabe entfernen“ (mindestens zwei bleiben).
- **Verschobene Scheiben.** Ein Riemen oder eine Kette über mehrere Scheiben implementiert `ISpanRoute` des Kerns: Die
  Kopplung zählt jede Nabe, und wird eine Scheibe, ein Kettenrad oder eine Umlenkrolle auf irgendeiner Nabe verschoben,
  läuft das Trum durch die neuen Nabenorte (sein Rahmen wird die Hülle der Nabenkreise, die Radien bleiben). Riemen über
  zwei Scheiben behalten die Regel „strecken und drehen“.
- Bibliothekselemente: `mech.belt.serpentine`, `mech.chain.serpentine`, `mech.idler.axial`, `mech.idler.grooved`,
  `mech.idler.section`, `mech.tensioner`, `mech.tensioner.stick`. Kontextaktionen: Umlenkrolle
  „Welle-Nabe-Verbindung“ (Nabensymbole) und „Läuft auf Riemenrücken“ (an/aus).

## Kleinteile

| Typ-ID | Name (EN / DE) | Kategorie | Ansichten | Parameter | Anker |
|---|---|---|---|---|---|
| `mech.circlip` | Retaining ring (circlip) / Sicherungsring | Verbindungselemente & Fügestellen | `axial` (offener Ring mit den beiden Augen für die Zange), `side` (auf der Welle: beide Ringquerschnitte geschwärzt) | — | `hub` (Nabe mit dem Innenradius als Bohrung: passt auf eine Welle) |
| `mech.pin` | Pin / Stift | Verbindungselemente & Fügestellen | `side`, `top` (Stirnansicht) | `kind` = `cylindrical` (Standard) \| `taper` \| `split` (Auswahl mit Symbolen) | Achse (Seitenansicht) |
| `mech.oring` | O-ring / O-Ring | Lager, Dichtungen & Führungen | `section` (beide Schnurquerschnitte geschwärzt), `top` (Innen- und Außenkante, Schnurmittellinie) | — | `hub` (Nabe mit dem Innenradius als Bohrung) |

- Die Kegelform des Kegelstifts ist überzeichnet, damit sie sichtbar wird; der Splint hat sein Auge links und zwei
  halbrunde Schenkel ungleicher Länge. Die Symbole der Stiftart nutzen den gemeinsamen Formensatz (Rechteck, Dreieck,
  Teilen); eigene Stiftsymbole bräuchten neue Einträge im gemeinsamen Symbolsatz (offener Punkt).
- Bibliothekselemente: `mech.circlip`, `mech.circlip.side`, `mech.pin.cylindrical`, `mech.pin.taper`, `mech.pin.split`,
  `mech.oring`, `mech.oring.top`.

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

## Technische Werte (Calc 3)

Dieser Abschnitt gilt für alle Elemente des Moduls (Zahnräder, Wellen, Riemen, Ketten, Gewindetriebe, Federn, Lager),
nicht nur für die oben beschriebenen. Werte sind optional: jedes Element lässt sich weiterhin ohne sie einfügen und
skalieren. Eingetragen und angezeigt werden sie mit der Wertekarte (*Bearbeiten ▸ Werte ▸*, die Schaltfläche *Werte* in
Erkunden und Ingenieur), gespeichert in optionalen Textparametern (Ingenieurabschnitt des Inspektors); vorhandene
Parameter werden genutzt, wo es sie gibt. Entwurfsnotizen:
[engineering-calculations.md, Abschnitt 12](../design/engineering-calculations.md#12-umsetzungsnotizen-calc-3-maschinenelemente-25112026).

Drehzahlen *n*, Drehmomente *M* und Leistungen *P* sind Beträge; *P* = 2π·*n*·*M*. Kartengrößen stehen vorn.

| Element | Größen (Parameter) | Eigene Beziehungen [Quelle] |
|---|---|---|
| Stirnrad/Schrägstirnrad, Hohlrad, Kegelrad, Schneckenrad | *z* (`teeth`), *m* (`module`, mm), *n* (`speed`), *M* (`torque`); Panel *P* (`power`), *d* (`diameter`); Ingenieur η (`efficiency`, Vorgabe 100 %) | *d* = *m*·*z* [S16]; *P* = 2π·*n*·*M* [S18] |
| Schnecke | *z* = Gangzahl (`starts`), *m*, *n*, *M*; *P*, η | *P* = 2π·*n*·*M* |
| Zahnstange | *m*, *v* (`linSpeed`), *F* (`force`); Panel *P*, Ritzel *z*_p (`pinionTeeth`), *d*_p (`pinionDiameter`, mm), *n*_p (`pinionSpeed`), *M*_p (`pinionTorque`) | *P* = *F*·*v* [S1]; *d*_p = *m*·*z*_p [S16]; *v* = ω·*r* = π·*d*_p·*n*_p; *F* = *M*/*r* = 2·*M*_p/*d*_p [S18] |
| Planetensatz | *z*_s (`zs`, Vorgabe 20), *z*_r (`zr`, Vorgabe 52), *n*_s (`speeds`), *n*_c (`speedc`); Panel *n*_r (`speedr`), *M*_s, *M*_r, *M*_c (`torques`, `torquer`, `torquec`); Ingenieur *k* = *z*_p1/*z*_p2 für Stufenplaneten (`stepRatio`, Vorgabe 1); Panel *z*_o (`zo`, Außenverzahnung am Hohlrad, Vorgabe 66) | Willis *n*_s − *n*_c = −(*z*_r/*z*_s)·*k*·(*n*_r − *n*_c); *M*_r = *M*_s·*k*·*z*_r/*z*_s; *M*_s + *M*_r + *M*_c = 0; also *M*_c = −*M*_s·(1 + *k*·*z*_r/*z*_s) [S39]. Drehzahlen und Momente sind hier vorzeichenbehaftet (gleicher Drehsinn positiv) |
| Ravigneaux-Radsatz | *n*_s1 (`speeds1`, kleine Sonne), *n*_s2 (`speeds2`, große Sonne), *n*_r (`speedr`), *n*_c (`speedc`); Panel *z*_s1 (`zs1`, Vorgabe 14), *z*_s2 (`zs2`, Vorgabe 20), *z*_r (`zr`, Vorgabe 52), *z*_o (`zo`, Außenverzahnung am Hohlrad, Vorgabe 66) | *n*_s2 − *n*_c = −(*z*_r/*z*_s2)·(*n*_r − *n*_c) (lange Planeten); *n*_s1 − *n*_c = +(*z*_r/*z*_s1)·(*n*_r − *n*_c) (Planetenpaare: zwei Außeneingriffe, die Planetenzähnezahlen kürzen sich) [S39]. Drehzahlen vorzeichenbehaftet. Keine Momentbeziehungen (zwei Freiheitsgrade: die Aufteilung hängt davon ab, welche Glieder festgehalten werden) |
| Kegelraddifferential (`mech.gearset.differential`) | *n*_c (`speedc`, Korb/Tellerrad), *n*₁, *n*₂ (`speed1`, `speed2`, Achswellen), *M*_c (`torquec`); Panel *M*₁, *M*₂ (`torque1`, `torque2`) | *n*_c = (*n*₁ + *n*₂)/2 [S39] (vorzeichenbehaftet); *M*₁ = *M*₂ = *M*_c/2 (offenes Differential, Reibung vernachlässigt) [S18] |
| Wellgetriebe | *z*_f (`flexTeeth`, Vorgabe 200), *z*_c (`circularTeeth`, Vorgabe 202), *i* (`ratio`), *n*₂ (`speedOut`); Panel *n*₁ (`speedIn`), *M*₁ (`torqueIn`), *M*₂ (`torqueOut`); Ingenieur η (`efficiency`, Vorgabe 100 %) | *i* = *n*₁/*n*₂ = −*z*_f/(*z*_c − *z*_f) (Hohlrad fest, Wellgenerator treibt, Abtrieb am Flexrad; Willis mit dem Wellgenerator als Steg) [S39]; *n*₁ = *i*·*n*₂; *M*₂ = \|*i*\|·η·*M*₁ [S18]. Drehzahlen vorzeichenbehaftet: *i* < 0, der Abtrieb dreht gegen den Antrieb |
| Zykloidgetriebe | *z*_d (Höcker, der Typparameter `lobes`, Vorgabe 11), *i* (`ratio`), *n*₂ (`speedOut`); Panel *z*_p (`pins`), *n*₁, *M*₁, *M*₂; Ingenieur η | *z*_p = *z*_d + 1 (wie gezeichnet); *i* = −*z*_d/(*z*_p − *z*_d) (Bolzenring fest, Exzenter treibt, Abtrieb aus der Scheibendrehung über die Abtriebsbolzen) [S39]; *n*₁ = *i*·*n*₂; *M*₂ = \|*i*\|·η·*M*₁ [S18] |
| CVT-Variator | *i* (der Typparameter `ratio`, Vorgabe 1), *n*₁ (`speedIn`), *n*₂ (`speedOut`); Panel *M*₁, *M*₂; Ingenieur η | *n*₁ = *i*·*n*₂; *M*₂ = *i*·η·*M*₁ [S18]. Die Zeichnung folgt einer eingetragenen `ratio`; eine aus den Drehzahlen bestimmte Übersetzung wird gegen die Spreizung 0,4 … 2,5 geprüft |
| Schwungrad | *n* (`speed`), *m* (`mass`, kg), *D* (`diameter`, außen, mm), *E* (`energy`, kJ); Panel *J* (`inertia`, kg·m²), *d*_i (`innerDiameter`, mm, Vorgabe 0), Δ*E* (`energyFluctuation`, kJ), Δ*n* (`speedFluctuation`); Ingenieur δ (`fluctuationCoefficient`) | *J* = *m*·(*D*² + *d*_i²)/8: Vollscheibe *d*_i = 0 (*J* = ½·*m*·*r*²), Kranz zwischen *d*_i und *D* (dünner Kranz *J* → *m*·*r*²; Nabe und Speichen vernachlässigt); *E* = ½·*J*·ω²; Δ*E* = *J*·ω·Δω = 4π²·*J*·*n*·Δ*n* (*n* die mittlere Drehzahl); δ = Δ*n*/*n* (Ungleichförmigkeitsgrad) [S18] |
| Welle, Keilwelle, starre und elastische Kupplung, Kardangelenk | *n*, *M*, *P* | *P* = 2π·*n*·*M* |
| Rutschkupplung | *n*, *M*, *P*, *M*_R (`slipTorque`, Rutschmoment; englisches Formelzeichen *T*_slip) | *P* = 2π·*n*·*M*; Prüfung `mech.clutch.slip` |
| Riemenscheibe | *d* (`diameter`, Wirkdurchmesser), *n*, *M*, *P* | *P* = 2π·*n*·*M* |
| Umlenkrolle, Riemenspanner (seine Rolle) | *d* (`diameter`), *n* | – (eine Umlenkrolle leitet kein Moment weiter) |
| Kettenrad | *z* (`teeth`), *n*, *M*, *P* | *P* = 2π·*n*·*M* |
| Riemen | *v*, *F* (Umfangskraft), *P* | *P* = *F*·*v* |
| Rollenkette | *p* (`pitch`, mm), *v*, *F*, *P* | *P* = *F*·*v* |
| Gewindespindel | *P*_h (Steigung, `pitch`, mm), *n*, *v*, *F*; Panel *M*, *P*; Ingenieur η (Vorgabe 100 %) | *v* = *P*_h·*n*; *F* = 2π·*M*·η/*P*_h [S18, S23]; *P* = 2π·*n*·*M* |
| Gewindetrieb-Mutter | *v*, *F* | – |
| Druck- und Zugfeder | *F* (`force`), *s* (`travel`), *c* (`rate`); Panel *d* (`wire`), *D* (`meanDiameter`), *n*_f (`activeCoils`); Ingenieur *G* (`shearModulus`, Vorgabe 81 000 N/mm²) | *F* = *c*·*s*; *c* = *G*·*d*⁴/(8·*D*³·*n*_f) [S21] |
| Drehfeder | *M* (`torque`), φ (`angle`, °), *c*_φ (`rate`, N·m/rad oder N·m/°) | *M* = *c*_φ·φ (φ in rad) [S21] |
| Wälzlager (Rillen-, Schräg-, Axialkugellager: *p* = 3; Kegel-, Zylinderrollen-, Nadellager: *p* = 10/3) | *C* (`loadRating`, kN), *P* (`load`, kN), *L*₁₀ (`life`, 10⁶ Umdrehungen), *L*₁₀h (`lifeHours`, h); Panel *n*; Ingenieur *L*_erf (`requiredLife`, h) | *L*₁₀ = (*C*/*P*)^*p*; *L*₁₀h = 10⁶·*L*₁₀/(60·*n*) [S19] |
| Gleitlager | *n* | – |

**Verknüpfungen zwischen Elementen** (aus der Kopplung der Zeichnung, dieselben Beziehungen wie „Verbundene auswählen –
bewegt sich mit“):

- *Wellenstrang*: eine Welle, Kupplungen darauf und die darauf festen Teile (Nabe fest, verschiebbar oder ohne) haben
  dasselbe *n*; eine lose Nabe dreht frei. Lager übernehmen die Drehzahl ihrer Welle. Das Moment wird unverändert
  weitergegeben, wenn höchstens zwei Teile darauf Moment aufnehmen oder abgeben; bei einer Verzweigung (drei oder mehr)
  wird kein Moment weitergegeben. Eine Rutschkupplung gibt *n* und *M* wie eine Kupplung weiter (solange sie hält;
  `mech.clutch.slip` warnt oberhalb von *M*_R). Ein Schwungrad übernimmt nur die Drehzahl: es hat keine Momentgröße und
  zählt deshalb nie als Verzweigung.
- *Eingriff*: *z*_a·*n*_a = *z*_b·*n*_b und *M*_b = *M*_a·(*z*_b/*z*_a)·(η_b/η_a) [S18]. Das η eines Zahnrads ist der
  Wirkungsgrad des Eingriffs, in dem es getrieben wird (am getriebenen Rad jeder Stufe eintragen); sind An- und
  Abtriebsmoment eingetragen, wird η bestimmt. Planeten auf einem Trägerbolzen werden nicht verknüpft (Planetensatz
  verwenden).
- *Riemen*: *v* = π·*d*·*n* und *M* = *F*·*d*/2 an beiden Scheiben. *Kette*: *v* = *z*·*p*·*n* und
  *M* = *F*·*z*·*p*/(2π); ohne Teilung halten die Kettenräder trotzdem *z*₁·*n*₁ = *z*₂·*n*₂ und *M*₂·*z*₁ = *M*₁·*z*₂
  [S18, S20].
- *Mehrscheibentriebe*: jedes Teil auf einer Verlaufsnabe läuft mit der Trumgeschwindigkeit (*v* = π·*d*·*n*, Kette
  *v* = *z*·*p*·*n*); das Drehzahlverhältnis zweier Scheiben folgt also ihren Durchmessern (Kettenräder: ihren
  Zähnezahlen). Umlenkrollen (Umlenkrolle, Riemenspanner und jedes Teil auf einer mit `b` markierten Nabe) übernehmen nur
  die Drehzahl: Sie ändern die Übersetzung nie und leiten kein Moment weiter. Die Momentbeziehung (*M* = *F*·*d*/2, Kette
  *M* = *F*·*z*·*p*/(2π)) wird nur gesetzt, solange höchstens zwei Teile über das Trum Moment aufnehmen oder abgeben; bei
  drei oder mehr unterscheidet sich die Umfangskraft je Trumabschnitt, und es wird kein Moment bestimmt.
- *Spindel und Mutter*: die Mutter auf der Spindel übernimmt deren *v* und *F*.
- *Getriebeeinheiten* (Planetensatz, Ravigneaux, Differential, Well- und Zykloidgetriebe, CVT): ein Glied, das auf einer
  Welle oder einem Teil sitzt (siehe „Gliedanschlüsse von Getriebeeinheiten“), teilt die Drehzahl des Wellenstrangs dieses
  Teils und, wenn der Strang Moment führt, dessen Moment. Das angeschlossene Glied zählt als ein Teil, das Moment aufnimmt
  oder abgibt: Motor, Welle und Sonne geben das Moment weiter, ein zweites Zahnrad auf derselben Welle bildet eine
  Verzweigung, und es wird kein Moment weitergegeben. Drehzahlen und Momente von Wellen sind Beträge, die Glieder der
  Sätze vorzeichenbehaftet: die Welle übernimmt den Betrag (*n* = \|*n*_m\|, *M* = \|*M*_m\|), ein von seiner Welle
  angetriebenes Glied nimmt den positiven Drehsinn an. Beispiel: Motorwelle mit 1000 min⁻¹ und 10 N·m an der Sonne
  (*z*_s = 20), Hohlrad fest (*z*_r = 52): die Stegwelle läuft mit 1000·20/72 = 277,8 min⁻¹ und 36 N·m. Drehzahlen und
  Momente des CVT sind Beträge und werden unverändert verknüpft: bei Übersetzung 2 ergeben 3000 min⁻¹ und 100 N·m am
  Antrieb mit η = 90 % an der Abtriebswelle 1500 min⁻¹ und 180 N·m. Zwei Einheiten auf einer Welle (ein CVT treibt den
  Steg eines Planetensatzes) geben das Moment über sie weiter. Drehzahl und Moment einer Welle, die sie von einem Glied
  übernimmt (n = \|n_m\|, M = \|M_m\|), reichen den Wert nur über den Anschluss weiter: sie erscheinen als *übernommen*, wie
  die einfachen Gleichheiten von CVT, Wellensträngen und Zahnstangen; Werte, die ein Satz aus seinen eigenen Beziehungen
  berechnet (Willis, Momentverhältnisse), erscheinen als *abgeleitet*.
- *Außenverzahnung am Hohlrad eines Satzes* (`outer`): ein damit kämmendes Rad und das Hohlradglied halten
  *z*_g·*n*_g = *z*_o·\|*n*_r\| und verlustfrei *M*_g·*z*_o = \|*M*_r\|·*z*_g ein [S18]; die Werte des Rads sind Beträge, ein vom
  Rad angetriebenes Hohlrad nimmt den positiven Drehsinn an. Beispiel: Steg fest, ein Rad mit *z* = 30 bei 600 min⁻¹ und
  5 N·m an *z*_o = 66: *n*_r = 272,7 min⁻¹, *n*_s = −(52/20)·272,7 = −709,1 min⁻¹, *M*_r = 11 N·m. Der Ravigneaux-Satz hat
  keine Momentvariablen: nur die Drehzahl.
- *Zahnstange und Ritzel*: ein Stirnrad in Stirnansicht, dessen Teilkreis die Teillinie der Zahnstange auf der
  Zahnseite berührt (innerhalb der Länge der Zahnstange; 45 % der Zahnhöhe unter den Zahnköpfen, im Schema mittig zwischen
  Körperlinie und Zahnköpfen), ist das Ritzel der Zahnstange: sie übernimmt *n*_p, *M*_p, *z*_p und *d*_p von diesem
  Rad, so folgt *v* = ω·*r* dem Ritzel (und der Wellenstrang des Ritzels erhält seine Drehzahl aus einer eingetragenen
  Zahnstangengeschwindigkeit). Bei zwei oder mehr solchen Rädern übernimmt die Zahnstange keines (Ritzelwerte an der
  Zahnstange eintragen). Die Zahnstange bietet diese Linie als Anker ihrer Wälzgeraden an (Kern `AnchorRole.PitchLine`,
  Id `pitch`, Zahnseite gekennzeichnet): ein Ritzel, das in ihre Nähe gezogen wird, fängt tangential an ihr (Fangart
  „mesh“, Beschriftung „Wälzgerade“), die Kopplung hält das Paar (ein wachsendes Ritzel schiebt die Zahnstange entlang
  ihrer Normalen weg), und die Werte finden das Ritzel über eben diesen Kopplungseingriff. Eine *feste Zahnstange*
  (Parameter `fixed`, „Feste Zahnstange (das Ritzel rollt daran ab)“, Vorgabe aus; Gestellstriche auf ihrem Rücken) bleibt
  stehen: ein wachsendes Ritzel bewegt sich stattdessen von ihr weg, und *v* ist die Geschwindigkeit der Ritzelmitte
  entlang der Zahnstange.
- Antriebsstrangteile des Maschinenmoduls (E-Maschine, Verbrennungsmotor, Rad) auf einer Welle gehören mit denselben
  Schlüsseln zum Wellenstrang.

**Wellenstränge über Modulgrenzen.** Die Regel des Wellenstrangs teilen alle Module (Kern `ShaftLines`, auf den
Kopplungsbeziehungen aufgebaut): Wellen, Keilwellen, Kupplungen, Rutschkupplungen und Kardangelenke geben das Moment unverändert weiter,
Lager übernehmen nur die Drehzahl, Planetenträger, Differentialkorb, Radsätze und die Gewindetrieb-Mutter bleiben
draußen (Radsätze und der CVT setzen ihre Glieder stattdessen auf Wellen). Ein an einen Strang angedocktes Glied zählt
auch in der Kernregel als ein Teil, das Moment aufnimmt oder abgibt (Kern `IGearUnit`, `GearUnitDocking`, 26.11.2026),
so zählt eine Pumpe des Prozessmoduls auf einer Welle an einem Sonnenanschluss genauso wie hier. Elemente beliebiger Module mit einer Drehzahl *n* (und einem Moment *T*), deren Nabe auf einer Wellenachse
oder einer anderen Nabe sitzt, gehören zum Strang. Jedes Modul verknüpft seine Elemente sternförmig mit der Welle oder
Kupplung des Strangs, so teilen eine E-Maschine des Maschinenmoduls, eine mechanische Welle und eine Pumpe des Prozess-
oder Fluidmoduls *n* und, bei höchstens zwei Momentgliedern, *T*: die Leistung 2π·*n*·*T* des Antriebs wird zur
Wellenleistung der Pumpe. Riemenscheiben, Kettenräder, Umlenkrollen und Riemenspanner haben einen Teilkreis, der nur den
Riemen oder die Kette bemisst: zwei davon, deren Teilkreise sich berühren, kämmen nicht wie Zahnräder (keine
Übersetzung, kein Mitskalieren beim Verschieben).

**Bewegter Räderzug** (Drehen eines Rads in Stirnansicht, Animationsbilder; Kern `GearTrain`, 26.11.2026). Ein Radsatz
oder CVT dreht nicht mehr alle angedockten Teile starr mit: jedes angedockte Teil dreht mit seinem Glied, und die Glieder
folgen der Drehzahlbeziehung der Einheit (`IGearUnit.SpeedRelations`, aus den eingetragenen Zähnezahlen und
Übersetzungen oder ihren Vorgaben): Planetensatz Willis *z*_s·ω_s + *k*·*z*_r·ω_r − (*z*_s + *k*·*z*_r)·ω_c = 0,
Ravigneaux-Satz beide Willis-Gleichungen, Differential ω₁ + ω₂ = 2·ω_c, Wellgetriebe, Zykloidgetriebe und CVT
ω_an = *i*·ω_ab (eingetragenes *i*, sonst aus den Zähnezahlen bzw. dem Parameter `ratio` des CVT). Ein Glied, dessen
Drehzahl mit 0 eingetragen ist, steht fest (`speedr` = 0 hält das Hohlrad). Ein Satz mit zwei Freiheitsgraden ohne
festgehaltenes Glied lässt die Teile dahinter unbestimmt: sie bewegen sich nicht. Teile an den Anschlüssen von Hohlrad,
Steg und Korb drehen mit diesen Gliedern; ein Rad an der Außenverzahnung eines Hohlrads kämmt mit dem Hohlradglied
(`GearUnitDocking.MeshPort`: ein Teilkreis, dessen Id einen Gliedanschluss benennt). Eine Zahnstange verschiebt sich
entlang ihrer Wälzgeraden, wenn ihr Ritzel dreht, *s* = φ·*r* (eine Vierteldrehung bei 20 mm Teilkreisradius verschiebt
sie um 31,4 mm). Eine feste Zahnstange bleibt stehen, und das Ritzel rollt in Gegenrichtung an ihr ab (`GearTrain.Travel`):
seine Mitte wandert *s* = φ·*r*, und jedes nicht feste Teil seines Räderzugs wandert mit (der Schlitten: die Welle des
Ritzels, die Räder und der Motor, die es antreiben).

**Prüfungen** (nur Hinweise, Overlay, nichts wird geändert):

| Id | Regel | Schwere | Quelle |
|---|---|---|---|
| `mech.mesh.module` | kämmende Räder mit verschiedenem Modul | Warnung | [S16] |
| `mech.power.balance` | η > 100 % an Zahnrad, Schnecke oder Spindel (mehr Leistung heraus als hinein) | Warnung | [S1] |
| `mech.planetary.assembly` | (*z*_s + *z*_r)/*N* nicht ganzzahlig oder *z*_r − *z*_s ungerade (einfache Planeten) | Warnung | [S18, S39] |
| `mech.bearing.life` | *L*₁₀h unter der eingetragenen geforderten Lebensdauer | Hinweis | [S19] |
| `mech.screw.selflock` | Trapezgewindetrieb mit eingetragenem oder bestimmtem η > 50 % (Rücklauf möglich) | Info | [S18] |
| `mech.harmonic.teeth` | Wellgetriebe mit *z*_c − *z*_f nicht positiv und gerade (Wellgenerator mit zwei Nocken) | Warnung | [S39] |
| `mech.cvt.range` | CVT-Übersetzung (eingetragen oder aus *n*₁/*n*₂ bestimmt) außerhalb der Spreizung ihres Parameters `ratio` (0,4 … 2,5) | Warnung | [S18] |
| `mech.clutch.slip` | Moment durch eine Rutschkupplung (ihr eigenes *M*, aus dem Wellenstrang verknüpft) über *M*_R | Warnung | [S18] |

Noch nicht vorhanden: Momentbeziehungen des Ravigneaux-Radsatzes (nur Drehzahlen); das Kegelritzel, das das Tellerrad
treibt, als Eingriff (ein Antrieb am Korbanschluss dreht mit der Korbdrehzahl: die Achsantriebsstufe davor setzen);
Anschlüsse für Hohlrad und Steg in den Stirnansichten (die Sätze in Stirnansicht drehen als Ganzes mit dem Glied an ihrer
Bohrung; ihr Hohlrad kämmt über die Außenverzahnung); das Zerlegen eines Satzes mit Außenverzahnung; eine feste Zahnstange
mit zwei Ritzeln eines Räderzugs, deren Wege sich widersprechen (nichts rollt); der Drehsinn einer Welle (eine Welle übernimmt den Betrag eines
vorzeichenbehafteten Glieds; werden An- und Abtriebsdrehzahl eines Satzes eingetragen, dessen Abtrieb rückwärts dreht,
entsteht ein Widerspruch: den Abtrieb am Satz eintragen); Willis über getrennte Planetenteile; Momentaufteilung an
Verzweigungen (Wellenstränge und Trume mit drei oder mehr momentführenden Teilen). Kein Festigkeitsnachweis (ISO 6336,
DIN 743, EN 13906 über *F* = *c*·*s* und *M* = *c*_φ·φ hinaus), keine Auslegung des Rutschmoments (Reibradius,
Reibflächenzahl, Federkraft).

## Hinweise zu den Hosts

Alle neuen Elemente werden wie die übrigen Mechaniksymbole in native Host-Grundformen umgesetzt (Linien, Rechtecke,
Ellipsen, Polylinien und Polygone mit Schraffur); ein Vektorbild ist nicht nötig. Der Mehrscheiben-Riemen ist ein
geschlossenes Polygon (Bögen abgetastet), die Mehrscheiben-Kette zwei geschlossene Polygone und je Rolle ein kleiner
Kreis. Die Verlaufsgriffe sind gewöhnliche Ziehgriffe der Engine (`dragHandles` / `moveHandle`, wie die Grenzlinien von
Diagrammen); ein Host ohne Ziehgriffe bietet weiterhin den Parameter `route` an. Schnittschraffuren und die Ölfüllung
(waagerechte Schraffur) nutzen dieselbe Schraffurfüllung wie andere Schnitte; wo ein Host keine native Musterfüllung hat, zeichnet er die
Schraffur als einzelne Linien (siehe Host-Fähigkeitsmatrix).
