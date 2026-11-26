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
- **Grenze (Kern-Kopplung).** Die Kopplung im Kern behandelt einen Riemen weiterhin als Trum zwischen seinen ersten beiden
  Naben: Verschieben der Scheibe auf Nabe 1 oder 2 streckt und dreht den ganzen gezeichneten Verlauf, Scheiben auf
  weiteren Naben bewegen den Riemen nicht. Den Verlauf mit den Griffen anpassen; ein Nachführen über alle Naben braucht
  eine Änderung von `Coupling` im Kern (offener Punkt).
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
| Zahnstange | *m*, *v* (`linSpeed`), *F* (`force`), *P* | *P* = *F*·*v* [S1] |
| Planetensatz | *z*_s (`zs`, Vorgabe 20), *z*_r (`zr`, Vorgabe 52), *n*_s (`speeds`), *n*_c (`speedc`); Panel *n*_r (`speedr`), *M*_s, *M*_r, *M*_c (`torques`, `torquer`, `torquec`); Ingenieur *k* = *z*_p1/*z*_p2 für Stufenplaneten (`stepRatio`, Vorgabe 1) | Willis *n*_s − *n*_c = −(*z*_r/*z*_s)·*k*·(*n*_r − *n*_c); *M*_r = *M*_s·*k*·*z*_r/*z*_s; *M*_s + *M*_r + *M*_c = 0 [S39]. Drehzahlen und Momente sind hier vorzeichenbehaftet (gleicher Drehsinn positiv) |
| Welle, Keilwelle, starre und elastische Kupplung, Kardangelenk | *n*, *M*, *P* | *P* = 2π·*n*·*M* |
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
  wird kein Moment weitergegeben.
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
- Antriebsstrangteile des Maschinenmoduls (E-Maschine, Verbrennungsmotor, Rad) auf einer Welle gehören mit denselben
  Schlüsseln zum Wellenstrang.

**Prüfungen** (nur Hinweise, Overlay, nichts wird geändert):

| Id | Regel | Schwere | Quelle |
|---|---|---|---|
| `mech.mesh.module` | kämmende Räder mit verschiedenem Modul | Warnung | [S16] |
| `mech.power.balance` | η > 100 % an Zahnrad, Schnecke oder Spindel (mehr Leistung heraus als hinein) | Warnung | [S1] |
| `mech.planetary.assembly` | (*z*_s + *z*_r)/*N* nicht ganzzahlig oder *z*_r − *z*_s ungerade (einfache Planeten) | Warnung | [S18, S39] |
| `mech.bearing.life` | *L*₁₀h unter der eingetragenen geforderten Lebensdauer | Hinweis | [S19] |
| `mech.screw.selflock` | Trapezgewindetrieb mit eingetragenem oder bestimmtem η > 50 % (Rücklauf möglich) | Info | [S18] |

Noch ohne Wertebeziehungen (die Elemente selbst gibt es und sie lassen sich zeichnen; nur ihre Werte fehlen): der
CVT-Variator, der Ravigneaux-Radsatz, der Ausgleichsradsatz, Well- und Zykloidgetriebe, die Energie des Schwungrads und
die Verknüpfung von Zahnstange und Ritzel (die Zahnstange hat eigene Werte). Gar nicht vorhanden: eine Rutschkupplung,
Willis über getrennte Planetenteile, Momentaufteilung an Verzweigungen (Wellenstränge und Trume mit drei oder mehr
momentführenden Teilen). Kein Festigkeitsnachweis (ISO 6336, DIN 743, EN 13906 über *F* = *c*·*s* und *M* = *c*_φ·φ
hinaus).

## Hinweise zu den Hosts

Alle neuen Elemente werden wie die übrigen Mechaniksymbole in native Host-Grundformen umgesetzt (Linien, Rechtecke,
Ellipsen, Polylinien und Polygone mit Schraffur); ein Vektorbild ist nicht nötig. Der Mehrscheiben-Riemen ist ein
geschlossenes Polygon (Bögen abgetastet), die Mehrscheiben-Kette zwei geschlossene Polygone und je Rolle ein kleiner
Kreis. Die Verlaufsgriffe sind gewöhnliche Ziehgriffe der Engine (`dragHandles` / `moveHandle`, wie die Grenzlinien von
Diagrammen); ein Host ohne Ziehgriffe bietet weiterhin den Parameter `route` an. Schnittschraffuren und die Ölfüllung
(waagerechte Schraffur) nutzen dieselbe Schraffurfüllung wie andere Schnitte; wo ein Host keine native Musterfüllung hat, zeichnet er die
Schraffur als einzelne Linien (siehe Host-Fähigkeitsmatrix).
