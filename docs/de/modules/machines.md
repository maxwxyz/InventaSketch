# Modul E-Maschinen & Antriebe (`machines`)

Schnellskizzen elektrischer Maschinen und elektrischer Antriebsstränge für Präsentationen und technische Abbildungen:
Quer- und Längsschnitte von Radial- und Axialflussmaschinen, einzelne Maschinenkomponenten zum freien Zusammensetzen,
Antriebsstrang-Blöcke mit Ports, Antriebsstrangteile für Getriebeschemata, die auf mechanischen Wellen sitzen (E-Maschine,
Verbrennungsmotor, Rad), sowie Fluss-, Drehrichtungs- und Drehmomentkennzeichnungen.

> **Grenzen.** Alle Darstellungen sind *illustrativ*. Nutformen, Magnetlagen, Luftspalt, Jochdicke, Wicklung und
> Strombild folgen plausiblen Proportionen, sind aber **nicht** das Ergebnis einer elektromagnetischen Auslegung oder
> Wicklungsberechnung. Maße, Flussdichten oder Wicklungsschemata dürfen daraus nicht abgelesen werden.

Jedes Objekt lässt sich ohne Parametereingabe einfügen. Freies gleich- und ungleichmäßiges Skalieren ändert nur die
Boxgröße, nie die Parameter. Geschnittenes Eisen ist schraffiert (Ständer 45°, Läufer 135°, Welle 45°, Gehäuse 135°, damit
sich benachbarte Teile unterscheiden), Kupferwicklungen haben eine helle Kupferfüllung mit feiner Kreuzschraffur (auch
einfarbig lesbar), Nordmagnete sind rot und Südmagnete blau (schwarz bzw. weiß in einfarbiger Ausgabe).

## Objekttypen

### Komplette Maschinen (Kategorie „Elektrische Maschinen“)

| Typ-ID | Name (EN / DE) | Ansichten |
|---|---|---|
| `machines.radial` | Radial-flux machine / Radialflussmaschine | `axial` Querschnitt (Symbol `view-axial`), `side` Längsschnitt (`view-section`), `schematic` Schaltzeichen (`view-schematic`) |
| `machines.axial` | Axial-flux machine / Axialflussmaschine | `side` Schnitt (`view-section`), `face` Stirnansicht einer Scheibe (`view-axial`), `schematic` (`view-schematic`) |

**Radialflussmaschine.** Querschnitt: Gehäuse (optional mit Kühlmantelkanälen), Ständerblechpaket mit Nuten, Zähnen und
Rückschluss (schraffiert), Wicklung in jeder Nut (optional Stromrichtungen als Punkt/Kreuz), Luftspalt, Läufer, Welle.
Längsschnitt: Gehäuse mit Lagerschilden und Lagern, Ständerpaket (Joch schraffiert), Spulenseiten mit Wickelköpfen,
Läuferpaket mit bauartspezifischen Details, Welle. Schaltzeichen: Kreis nach IEC-Art mit **M** oder **G**, Phasenzahl
(`3~`), Phasenanschlüsse U/V/W mit Klemmen und Wellenstummel.

Läuferbauarten (Parameter `rotor`):

| Wert | Läufer | Querschnitt | Längsschnitt |
|---|---|---|---|
| `spm` | PMSM, Oberflächenmagnete | Bogenmagnete auf der Läuferoberfläche | Magnetschicht |
| `ipm` | PMSM, vergrabene Magnete in V-Form | zwei Magnete je Pol in Taschen (Flusssperren) | Magnetband |
| `spoke` | PMSM, Speichenmagnete | radiale Magnete zwischen den Polen | Magnetband |
| `eesm` | Fremderregte Synchronmaschine | Schenkelpole mit Polschuhen und Erregerspulen | Erregerwicklungsenden, Schleifringe mit Bürsten |
| `asm` | Asynchronmaschine, Käfigläufer | geschlossene Läuferstäbe (`bars`) | Stäbe und Kurzschlussringe |
| `srm` | Geschaltete Reluktanzmaschine | ausgeprägte Läuferzähne; Ständer mit offenen Nuten und Zahnspulen | glattes Paket |

**Außenläufer** (`layout = outer`): Der Läuferring umgibt den Ständer; der Ständer sitzt auf einem feststehenden Träger
auf der Achse (optional mit Kühlkanälen im Träger); der Längsschnitt zeigt die Läuferglocke mit Stirnscheibe und Lager.

**Axialflussmaschine** (ein Ständer, zwei Läufer, YASA-Bauart): Schnitt mit segmentierten Ständerkernen (Polschuhe an
beiden Luftspalten), Spulen um die Kerne, zwei Läuferscheiben mit Magneten (über den Ständer hinweg entgegengesetzte
Polarität), Gehäuse und Lager. Die Stirnansicht zeigt über `disc` entweder die Läufer-Magnetscheibe (`poles` Segmente mit
N/S-Beschriftung) oder den Ständer-Spulenkranz (`coils` Segmente).

### Maschinenkomponenten (Kategorie „Maschinenkomponenten“)

| Typ-ID | Name (EN / DE) | Ansichten | Hinweise |
|---|---|---|---|
| `machines.part.stator` | Stator core / Ständerblechpaket | `axial`, `side` | Nuten, Wicklung, offene Nuten (Zahnspulen), Innen-/Außenläufer |
| `machines.part.rotor` | Rotor / Läufer | `axial`, `side` | alle sechs Läuferbauarten, Welle optional |
| `machines.part.magnet` | Magnet segment / Magnetsegment | `axial` Bogensegment, `side` Quader | Polarität, Magnetisierungspfeil, N/S-Beschriftung |
| `machines.part.coil` | Coil / Spule | `section` (Punkt/Kreuz), `top` Rennbahnspule, `symbol` Induktivität | Eisenkern, Stromrichtungen |
| `machines.part.airgap` | Air-gap ring / Luftspaltring | `axial`, `side` | gepunkteter Ring |
| `machines.part.endwinding` | End winding / Wickelkopf | `side`, `axial` Stirnansicht | Spulengruppen |
| `machines.part.housing` | Machine housing / Maschinengehäuse | `axial`, `side` | Kühlmantel, Kühlrippen, Lager |
| `machines.part.cooling` | Cooling channel / Kühlkanal | `axial` Kühlmantel, `section` Kanal mit Strömungspfeil | Kanalzahl, Strömungsrichtung |
| `machines.part.encoder` | Resolver / encoder / Resolver / Drehgeber | `axial`, `side` | Resolver (gezahnter Ständer, zweipoliger Läufer) oder optischer Drehgeber (Strichscheibe, Lesekopf) |
| `machines.part.slipring` | Slip rings & brushes / Schleifringe & Bürsten | `side`, `axial` | 1–4 Ringe |

Die Standardgrößen passen zueinander: Der Standardläufer im Standardständer lässt einen Luftspalt von etwa 1 mm, der
Standard-Luftspaltring füllt diesen Spalt, Magnetsegmente passen auf den Standardläufer und das Standardgehäuse um den
Standardständer.

### Antriebsstrang-Blöcke (Kategorie „Antriebsstrang-Blöcke“)

| Typ-ID | Name (EN / DE) | Ansichten |
|---|---|---|
| `machines.drive.battery` | Battery / Batterie | `block`, `symbol` (Zellen) |
| `machines.drive.inverter` | Inverter / Wechselrichter | `block` (Umrichterquadrat mit = und ~), `symbol` Drehstrombrücke (sechs Schalter mit Freilaufdioden, optional Zwischenkreiskondensator, Klemmen U/V/W) |
| `machines.drive.dclink` | DC link / Zwischenkreis | `block`, `symbol` (Kondensator) |
| `machines.drive.motor` | E-motor / E-Motor | `block`, `symbol` (M/G mit Phasenanschlüssen) |
| `machines.drive.gearbox` | Gearbox / Getriebe | `block`, `symbol` (Zahnradpaar als Schema) |
| `machines.drive.differential` | Differential / Differential | `block`, `symbol` (Schema) |
| `machines.drive.wheel` | Wheel / Rad | `side`, `top`, `schematic` (siehe „Antriebsstrang“) |
| `machines.drive.load` | Driven load / Arbeitsmaschine (Last) | `block` (Schwungrad-Glyphe), `symbol` (Schwungscheibe auf Welle mit „J“; Eingangsport `in` links, Wellenachse) |

Die Blockbeschriftung folgt der Sprache des Hosts; ein Objekttext ersetzt die Beschriftung (z. B. „HV-Batterie“).

### Antriebsstrang (Kategorie „Antriebsstrang (Schema)“)

Teile, um Antriebsstränge und Getriebe zusammen mit Wellen, Zahnrädern, Lagern, Kupplungen und dem Differential des
Mechanik-Moduls (`mech.*`) schnell auf einer Folie aufzubauen. Sie sitzen auf einer `mech.shaft` und **folgen ihr**
(Core-Kopplung, siehe „Fanganker“).

| Typ-ID | Name (EN / DE) | Ansichten |
|---|---|---|
| `machines.drivetrain.emachine` | E-machine (gearbox schematic) / E-Maschine (Getriebeschema) | (Standard: Radialfluss, Innenläufer, PMSM; Varianten unten) `stick` Schema (`view-schematic`): Läuferbalken auf einem Läuferträger mit drehfestem Nabenkreuz, schraffierte Ständerbalken über dem Luftspalt, Ständer am Gehäuse; `side` vereinfachter Längsschnitt (`view-side`): Ständerpaket mit Wickelköpfen, Läuferpaket, Welle, Gehäuse; `axial` Stirnansicht (`view-axial`): Ständerring, Läuferscheibe, Welle |
| `machines.drivetrain.engine` | Internal combustion engine / Verbrennungsmotor | `block` (`view-block`, Beschriftung „Engine (ICE)“ / „Verbrennungsmotor“ oder der Objekttext), `schematic` Kurbelwellenschema (`view-schematic`): Kurbelgehäuse, Zylinder mit Kolben und Pleueln, Kurbelwelle mit Kröpfungen, Abtriebswelle rechts |
| `machines.drive.wheel` | Wheel / Rad | zusätzliche Ansicht `schematic` (`view-schematic`): dunkler Reifenbalken für Antriebsstrang-Draufsichten (Bibliothekseintrag „Rad (Antriebsstrangschema)“ in dieser Kategorie) |

**Varianten der E-Maschine.** Ein Typ mit zwei optionalen Parametern; jede Variante gibt es auch als Bibliothekseintrag
(Schema und Seitenansicht):

| `layout` (Bauform) | Schema / Seitenansicht |
|---|---|
| `inner` (Standard) | Radialfluss, Innenläufer: Läuferbalken auf einem Läuferträger, Ständerbalken darum, Ständer am Gehäuse |
| `outer` | Radialfluss, Außenläufer: Läuferglocke von der Welle um den Ständer, Ständer auf einer feststehenden Hülse am Gehäuse |
| `axial` | Axialfluss, eine Läuferscheibe neben einem Ständer (axialer Luftspalt) |
| `axial-2r` | Axialfluss, Doppelläufer (YASA-Bauart): ein Ständer zwischen zwei Läuferscheiben |
| `axial-2s` | Axialfluss, Doppelständer: eine Läuferscheibe zwischen zwei Ständern |

| `kind` (Maschinenart) | Kennzeichen am Läufer (Luftspaltseite) |
|---|---|
| `pmsm` (Standard) | Magnete, rot (N) / blau (S) |
| `eesm` | Erregerwicklung (Kupfer) und zwei Schleifringe mit Bürsten auf der Welle |
| `asm` | Käfigstäbe (grau) mit Kurzschlussringen |
| `srm` | ausgeprägte Läuferzähne (ohne Magnete, ohne Wicklung) |

Die Stirnansicht zeigt den Läufer im Querschnitt (Radialfluss) bzw. die Stirnfläche der Läuferscheibe (Axialfluss) mit
denselben Kennzeichen (Magnetsektoren, Erregerpole, Käfigstäbe, Nuten zwischen den Zähnen).

Weitere Parameter (optional): E-Maschine `shaft` (Wellenstümpfe; aus, wenn die Maschine in der Seitenansicht auf einem
gezeichneten Wellenkörper `mech.shaft` sitzt, standardmäßig an), `ground` (Gehäuseanbindung: Gestellstriche im Schema,
Gehäusekontur in Seiten- und Stirnansicht, an), `hatch`; Verbrennungsmotor `cylinders` (1-6, Standard 4; das Schema zeigt
einen Reihenmotor, äußere Kröpfungen gegenüber den inneren). Kontextaktionen: Bauform ▾ und Maschinenart ▾
(Auswahlen mit den Maschinen-Piktogrammen), Wellenstümpfe an/aus, Gehäuseanbindung an/aus.

Die Darstellungen sind Schemasymbole im Stil kinematischer Getriebeschemata, kein genormtes Schaltzeichen.

**Suche.** Alle Antriebsstrangteile tragen die Suchwörter `drivetrain`, `powertrain` und `antriebsstrang`: Die Suche nach
„Antriebsstrang“ findet E-Maschine, Verbrennungsmotor, E-Motor, Getriebe, Differential (Block und Kegelraddifferential),
Rad, Wellen, Stirnräder, Scheiben- und Lamellenkupplung und das Kardangelenk (bei geladenem Mechanik-Modul).

**„Einfügen ▸“-Vorschläge.** Eine Welle bietet Stirnrad, Kugellager, E-Maschine (in der Ansichtsart der Welle:
Schema-Welle → E-Maschine als Schema, Wellenkörper → Seitenansicht), Rad, Scheibenkupplung und elastische Kupplung an
(danach Kegelrad, Zylinderrollenlager, starre Kupplung); die E-Maschine bietet Wechselrichter- und Batterieblock, Welle,
Zahnrad, Lager und Zwischenkreis; der Verbrennungsmotor Kupplung, Schwungrad, Welle, Getriebe, E-Maschine; das Rad Welle
und Differential; das Kegelraddifferential Welle, Rad und Zahnräder; die Scheibenkupplung Welle, Verbrennungsmotor,
Schwungrad und E-Maschine; der E-Motor-Block Wechselrichter, Batterie, Getriebe und die E-Maschine. IDs des jeweils
anderen Moduls werden übersprungen, wenn es nicht geladen ist.

### Kennzeichnungen (Kategorie „Fluss, Drehrichtung & Drehmoment“)

| Typ-ID | Name (EN / DE) | Ansichten |
|---|---|---|
| `machines.ann.flux` | Flux path / Flusspfad | `arc` (Feldlinie zwischen zwei Polen), `loop` (geschlossener magnetischer Kreis) |
| `machines.ann.rotation` | Rotation arrow / Drehrichtungspfeil | `axial` (um den Mittelpunkt), `side` (um die Welle) |
| `machines.ann.torque` | Torque arrow / Drehmomentpfeil | `curved` mit Bezeichnung (T auf Englisch, M auf Deutsch oder Objekttext), `vector` (Momentenvektor als Doppelpfeil) |

## Parameter (alle optional)

| Schlüssel | Typen | Bedeutung | Standard |
|---|---|---|---|
| `rotor` | Radialmaschine, Läufer | `spm`, `ipm`, `spoke`, `eesm`, `asm`, `srm` | `spm` |
| `layout` | Radialmaschine, Ständer, Läufer | `inner` (Innenläufer) oder `outer` (Außenläufer) | `inner` |
| `slots` | Radialmaschine, Ständer | Nutzahl (6–96) | 36 |
| `poles` | Radialmaschine, Läufer, Axialmaschine, Ständer | Polzahl (SRM: Läuferzähne); Darstellungen mit vergrabenen Magneten und Schenkelpolen verwenden mindestens 4 | 8 (Axial: 16) |
| `bars` | Radialmaschine, Läufer | Käfigstäbe (erweitert) | 28 |
| `coils` | Axialmaschine | Ständerspulen | 18 |
| `winding` | Radialmaschine, Ständer | Wicklung zeigen | an |
| `currents` | Radialmaschine, Ständer, Läufer, Axialmaschine, Spule | Stromrichtungen (Punkt = aus der Zeichenebene heraus, Kreuz = hinein) | aus (Spule: an) |
| `housing` | Radial-, Axialmaschine | Gehäuse und Lager | an |
| `cooling` | Radial-, Axialmaschine, Gehäuse | Kühlmantelkanäle (Außenläufer: Kanäle im Träger) | aus |
| `mode`, `phases` | Radial-, Axialmaschine, Motorblock | Motor/Generator-Buchstabe, Phasenzahl im Schaltzeichen | Motor, 3 |
| `hatch` | die meisten geschnittenen Teile | geschnittenes Eisen schraffieren (erweitert) | an |

Das Strombild verteilter Wicklungen ist eine Momentaufnahme zur Veranschaulichung: Leiter unter einem Nordpol führen Strom
aus der Zeichenebene heraus, unter einem Südpol in sie hinein.

## Kontextaktionen

Ansichtswechsel (mit den `ViewDef.Icon`-Piktogrammen), **Läuferbauart ▾** (Auswahl `param.rotor=…`),
**Polzahl ±2** (`param.poles.inc/dec`), **Nutzahl ±3** (`param.slots.inc/dec`), **Stromrichtungen an/aus**, **Kühlung an/aus**,
**Innen-/Außenläufer** (`cmd-swap`); Schaltzeichen: Motor/Generator; Magnet: Polarität tauschen, Pfeil an/aus; Spule:
Ströme, Kern; Kühlkanal: Anzahl, Strömungsrichtung; Drehgeber: Resolver/Drehgeber; Kennzeichnungen: Richtung umkehren.
Beim Wechsel zwischen runder Ansicht und Längsschnitt wird die Boxbreite angepasst, sodass die Proportionen erhalten bleiben.

## Fanganker

- Alle runden Teile bieten einen **Hub**-Punkt in der Mitte (konzentrisches Fangen, Nabe auf Wellenachse).
- Der **Ständer** (Innenläufer) bietet seine tatsächliche Bohrung als **PitchInner**, der **Läufer** seinen tatsächlichen
  Außenradius als **Pitch** an (Außenläufer: Ständer-Außenradius als Pitch, Läufer-Innenradius als PitchInner; die
  vollständige Radialflussmaschine bietet beide). Die Radien sind nahezu gleich, daher behandelt der Core-Snapper das Paar
  als Passung und fängt den Läufer **konzentrisch** im Ständer (und den Ständer um den Läufer) statt ihn wie ein Planetenrad
  im Hohlrad eingreifen zu lassen – auch nach freiem Skalieren (Regel: Hohlrad-Eingriff nur unter 90 % des Hohlradradius,
  `Snapper.InternalMeshMaxRatio`). Der Luftspaltring bietet seinen Mittenkreis als PitchInner. Gehäuse und Kühlmantel bieten ihre
  Bohrung als PitchInner an (konzentrisch zum Ständer). Magnetsegmente bieten ihren Innenbogen als PitchInner an, sodass
  der Bogenmittelpunkt auf den Läufermittelpunkt fängt.
- Längsschnitte (`side`) bieten das **Axis**-Segment der Welle an; Drehgeber, Schleifringe und andere Teile fangen mit
  ihrer Nabe darauf, Maschinenachsen fluchten mit mechanischen Wellen.
- **Antriebsstrangteile und Kopplung.** E-Maschine (jede Ansicht) und Rad (jede Ansicht) bieten einen **Hub** in der
  Mitte; der Verbrennungsmotor bietet seinen **Hub** und einen `out`-**Port** am Ende der Abtriebswelle (Schema: rechtes
  Ende der Kurbelwellenachse, die zugleich ein **Axis**-Segment für Schwungrad oder Riemenscheibe ist; Block: Mitte der
  rechten Kante). Die E-Maschine hat `w`/`e`-**Ports** an den Wellenenden und einen **Mount** unter dem Ständer, das
  Motorschema einen **Mount** unter dem Kurbelgehäuse; Draufsicht und Schema des Rads bieten die Radachse als **Axis**.
  Liegt eine Nabe auf der Achse einer `mech.shaft`, folgt das Teil der Welle über die Core-Kopplung (`Coupling`,
  „mitgenommen“): Verschieben, Drehen oder Strecken der Welle nimmt E-Maschine, Räder und einen Verbrennungsmotor, dessen
  Abtrieb auf dem Wellenende sitzt, mit (beim Strecken bleibt die relative Lage auf der Achse erhalten). Ein Rad allein
  verschoben gleitet auf der Welle; E-Maschine, Verbrennungsmotor oder E-Motor verschoben nehmen die Welle samt allem,
  was darauf sitzt, mit (`ObjectType.DrivesShaft`: ein Antrieb führt seine Welle). Die Seitenansicht des Rads bewegt sich mit einem konzentrischen Teil in
  Stirnansicht (z. B. einem Zahnrad auf derselben Achse).
- Antriebsblöcke bieten **Port**-Anker an den vier Kantenmitten (Blockansicht) bzw. an den Symbolklemmen (Batterie +/−,
  Wechselrichter DC+/DC−/U/V/W, Motor-Phasenanschlüsse, Getriebe-Ein-/Ausgang, Differential Eingang/links/rechts), sodass
  Pfeile und Leitungen andocken.

### Weiterbauen mit „+“ (Anschlüsse)

Phasenklemmen u/v/w bieten eine AC-Leitung an, Batterie- und Zwischenkreisklemmen (plus/minus, dc+/dc-) eine
DC-Leitung, Spulenenden, Bürsten und Gebersignale eine Leitung (Leitungen des Elektromoduls), der Kühlkanal ein/aus eine
Wasserleitung (Energiemodul), die vier Seiten der Antriebsstrang-Blöcke einen Pfeil (Blockschaltbild), danach die
passenden Elemente. Wellenenden (Getriebe, Räder, E-Maschinen, Motorabtrieb) sind mechanisch und werden mit Wellen an
ihren Achsenden weitergebaut.

## Technische Werte (Calc 3)

Werte sind optional: jedes Element lässt sich weiterhin ohne sie einfügen und skalieren. Eingetragen und angezeigt werden
sie mit der Wertekarte (*Bearbeiten ▸ Werte ▸*, die Schaltfläche *Werte* in Erkunden und Ingenieur), gespeichert in
optionalen Textparametern (Ingenieurabschnitt des Inspektors); `poles` ist die vorhandene Polzahl. Drehzahlen *n*,
Drehmomente *M* und Leistungen sind Beträge; *P* = 2π·*n*·*M*. Kartengrößen stehen vorn. Entwurfsnotizen:
[engineering-calculations.md, Abschnitt 12](../design/engineering-calculations.md#12-umsetzungsnotizen-calc-3-maschinenelemente-25112026).

| Element | Größen (Parameter) | Eigene Beziehungen [Quelle] |
|---|---|---|
| Radial- und Axialflussmaschine (`machines.radial`, `machines.axial`) | *n* (`speed`), *M* (`torque`), *P* (`power`), *f* (`frequency`); Panel *n*_s (`syncSpeed`), *s* (`slip`), Polzahl 2*p* (`poles`); Ingenieur η (`efficiency`), *P*_el (`powerEl`), *P*_N (`ratedPower`), *M*_N (`ratedTorque`) | *n*_s = 60·*f*/*p* (*p* = Polzahl/2) [S15]; *s* = (*n*_s − *n*)/*n*_s [S15]; *P* = 2π·*n*·*M*; Motorbetrieb *P* = η·*P*_el, Generatorbetrieb (`mode` = Generator, das Generatorsymbol) *P*_el = η·*P* [S14] |
| E-Motor-Block, E-Maschine (Antriebsstrang) | *n*, *M*, *P*, η; Ingenieur *P*_el, *P*_N, *M*_N; Betriebsart `mode` (beim E-Motor-Block dessen eigene Auswahl; bei der E-Maschine des Antriebsstrangs ein Ingenieurparameter, Standard Motor) | *P* = 2π·*n*·*M*; Motor *P* = η·*P*_el, Generator *P*_el = η·*P* [S14] |
| Verbrennungsmotor | *n*, *M*, *P*; Ingenieur *P*_N, *M*_N | *P* = 2π·*n*·*M* (keine Thermodynamik) |
| Getriebe, Differential (Blöcke) | *i* (`ratio`), η (`efficiency`, Vorgabe 100 %), *n*₂ (`speedOut`), *M*₂ (`torqueOut`); Panel *n*₁ (`speedIn`), *M*₁ (`torqueIn`), *P*₁ (`powerIn`), *P*₂ (`powerOut`) | *n*₁ = *i*·*n*₂; *M*₂ = *M*₁·*i*·η [S18]; *P*₁ = 2π·*n*₁·*M*₁; *P*₂ = 2π·*n*₂·*M*₂ |
| Rad | *r* (`radius`, dynamischer Rollradius), *n*, *M*, *v* (`linSpeed`, km/h); Panel *F* (`force`), *P* | *v* = 2π·*n*·*r*; *F* = *M*/*r* [S18]; *P* = 2π·*n*·*M* |

**Verknüpfungen.**

- *Antriebskette*: ein Verbinder (ein Pfeil oder jede Linie), dessen erstes angedocktes Ende an einem Block mit
  mechanischem Abtrieb (E-Motor, E-Maschine, Verbrennungsmotor, Getriebe, Differential) und dessen letztes an einem Block
  mit mechanischem Antrieb (Getriebe, Differential, Rad) sitzt, gibt *n* und *M* weiter: Motor → Getriebe → Differential
  → Rad. Die Richtung des Verbinders bestimmt, welcher Block vorn liegt.
- *Wellen*: E-Maschine, Verbrennungsmotor und Rad auf einer mechanischen Welle übernehmen deren Drehzahl; das
  Mechanik-Modul gibt das Moment entlang des Wellenstrangs und über Zahneingriffe weiter (Schlüssel `n`, `T`, `P` in
  beiden Modulen).

**Prüfungen** (nur Hinweise, Overlay, nichts wird geändert):

| Id | Regel | Schwere | Quelle |
|---|---|---|---|
| `mach.slip.sign` | Asynchronmaschine (`rotor` = `asm`) im Motorbetrieb mit *n* > *n*_s (Generatorbetrieb) | Info | [S15] |
| `mach.efficiency` | η > 100 %, *P*₂ > *P*₁ (Getriebe, Differential), *P* > *P*_el (Motor) oder *P*_el > *P* (Generator) | Warnung | [S1] |
| `mach.overload` | *P* > *P*_N oder *M* > *M*_N (Bemessung vom Nutzer eingetragen) | Warnung | [S14] |

**Generatorbetrieb** (26.11.2026). Die Betriebsart (`mode`: Standard Motor, Generator) bestimmt die
Wirkungsgradbeziehung: ein Motor wandelt elektrische in mechanische Leistung (*P* = η·*P*_el, `mach.efficiency.motor`),
ein Generator, angetrieben von einer Turbine, einem Verbrennungsmotor oder einer Getriebestufe, die Wellenleistung in
elektrische Leistung (*P*_el = η·*P*, `mach.efficiency.generator`). Eine Maschine an einer Turbinenwelle der
Verfahrenstechnik übernimmt die *P* der Turbine über die Welle und gibt *P*_el ab (Verfahrenstechnikmodul). Drehzahlen,
Momente und Leistungen bleiben Beträge; das Vorzeichen des Schlupfs schaltet die Betriebsart nicht um.

**Frequenz, Klemmen, Last und Hochlauf** (Runde 7, 26.11.2026).

| Element | Neue Variablen (Parameter) | Beziehungen [Quelle] |
|---|---|---|
| E-Motor-Block, Antriebsstrang-E-Maschine | Panel *f* (`frequency`), Polzahl 2*p* (`poles`, Vorgabe 4), *n*_s (`syncSpeed`); Ingenieur *s* (`slip`) | E-Motor-Block: *n*_s = 60·*f*/*p*, *s* = (*n*_s − *n*)/*n*_s [S15]; Antriebsstrang-E-Maschine nach `kind`: PMSM und EESM *n* = *n*_s (`mach.speed.synchronous`), ASM mit Schlupf, SRM ohne Netzfrequenzbeziehung |
| Maschinen, E-Motor-Block, Antriebsstrang-E-Maschine | Panel *U* (`voltage`, verkettet), *I* (`current`); Ingenieur cos φ (`powerFactor`) | drei Phasen (Standard): *P*_el = √3·*U*·*I*·cos φ (`mach.power.el3`) [S15]; eine Phase (`phases` = 1): *P*_el = *U*·*I*·cos φ (`mach.power.el1`) [S7]; andere Phasenzahlen: keine |
| Radial- und Axialflussmaschine | wie oben, dazu die Synchronbindung | Magnet- und fremderregte Läufer (`spm`, `ipm`, `spoke`, `eesm`; die Axialflussmaschine): *n* = *n*_s [S15] |
| Maschinen, E-Motor-Block, Antriebsstrang-E-Maschine | Panel *J*_M (`rotorInertia`), *J*_L (`loadInertia`, aus dem Antriebsstrang oder eingegeben), *M*_M mittleres Motormoment während des Hochlaufs (`runUpTorque`), *t*_a Hochlaufzeit (`runUpTime`); Ingenieur *J* (`inertia`) | *J* = *J*_M + *J*_L (`mach.inertia.total`); *t*_a = *J*·2π·*n*/(*M*_M − *M*) (`mach.runup`, konstante Momente, aus dem Stillstand bis *n*; das Lastmoment ist das Wellenmoment *M*) [S15] |
| Getriebe, Differential | Panel *J*₂ (`inertiaOut`), *J*₁ (`inertiaIn`) | *J*₁ = *J*₂/*i*² (`mach.gearbox.inertia`, Verluste und Eigenträgheit vernachlässigt) [S18] |
| Rad | Panel *m* (`mass`, über das Rad oder die Achse angetriebene Fahrzeugmasse); Ingenieur *J* (`inertia`) | *J* = *m*·*r*² (`mach.wheel.inertia`) [S18] |
| Arbeitsmaschine `machines.drive.load` (neu; Block und Schwungrad-Symbol) | *n*, *M* Lastmoment, *P*, *J* (`inertia`) | *P* = 2π·*n*·*M*; mechanischer Eingang links |

- *Trägheit entlang des Strangs*: Die Verbindung, die *n* und *M* nach hinten weitergibt, gibt das Lastträgheitsmoment
  nach vorn weiter: Das *J* der Last (oder die Fahrzeugmasse am Rad) wird zum *J*₂ des Getriebes, sein *J*₁ = *J*₂/*i*² zum
  *J*_L des Motors. Beispiel: Last 2,5 kg·m² und 100 N·m hinter einem Getriebe *i* = 5 → *J*_L = 0,1 kg·m², *M* = 20 N·m;
  mit *J*_M = 0,02 kg·m², 1500 min⁻¹ und *M*_M = 50 N·m dauert der Hochlauf 0,63 s (Unit-Test).
- *Prüfung* `mach.runup.torque` (Warnung, [S15]): *M*_M ≤ *M* – der Antrieb kann die Last nicht beschleunigen.
- *Modulübergreifende Verknüpfung*: Die Klemmenvariablen verwenden die gemeinsamen Schlüssel *U*, *I*, *f* und *P*_el
  (`NetKeys`), so dass ein Abgang eines anderen Moduls, der diese Werte an den Motorklemmen meldet, sie ohne
  Code-Abhängigkeit trifft. Der Frequenzumrichter der Schaltanlage meldet noch keine Ausgangsfrequenz; „Umrichterfrequenz →
  Motordrehzahl“ braucht daher einen Werteport auf der Schaltanlagenseite (offen).
- Bibliothek: „Arbeitsmaschine (Block)“, „Schwungmasse auf Welle (Symbol)“, „E-Motor mit Hochlaufzeit“ (1450 min⁻¹,
  20 N·m Last, 60 N·m im Hochlauf, *J* = 0,01 + 0,09 kg·m² → *t*_a = 0,38 s; die Wertebeschriftung zeigt *t*_a).

Noch nicht: Nut-/Polzahlen, Konstanten der Gleichstrommaschine, Effizienzklassen (IE-Codes brauchen die Grenzwerttabelle
der IEC 60034-30-1, die nicht wiedergegeben wird), Betriebsarten S1–S10, Werte für Batterie, Wechselrichter und
Zwischenkreis (Calc 4 mit dem Energiemodul).

## Bekannte Grenzen

- Der Eintrag Motorsymbol (`machines.motor.symbol`, die Ansicht `schematic` von `machines.radial`) und der Drehstrommotor
  des Elektromoduls (`elec.motor.3ph`) zeichnen dasselbe Schaltzeichen, bleiben aber zwei Einträge: das Maschinensymbol
  ist die Maschine selbst (Wellennabe, die an Antriebsstränge und Pumpen koppelt, Maschinenwerte *n*, *T*, *P*, *f*,
  Schlupf, η, 1–6 Phasen, Motor/Generator, wechselt in die Querschnittsansichten) und hat keine elektrischen Anschlüsse;
  das elektrische ist ein Schaltungsbauteil mit Anschlüssen U/V/W in einem verdrahteten Netz und überlässt die
  Maschinenwerte diesem Modul.

- Antriebsstrangteile werden mit ihrer Welle nur verschoben (die Core-Kopplung verschiebt Teile, sie dreht sie nicht):
  Nach dem Drehen einer Welle die Teile darauf ebenfalls drehen (oder gemeinsam drehen).
- Nur illustrative Geometrie (siehe oben); keine Wicklungsschemata, keine Prüfung von Nut-/Polzahlkombinationen.
- Außenläufer-Komponenten sind standardmäßig nicht größengleich abgestimmt (Läuferring passend um den Ständer skalieren).
- Hosts: Die gesamte Ausgabe besteht aus Linien, Polylinien/Polygonen, Rechtecken, Ellipsen und Text; Schraffuren und
  Kupfer-Kreuzschraffuren werden als explizite Linien ausgegeben, wo ein Host keine Musterfüllung kennt (siehe
  Host-Fähigkeitsmatrix).
