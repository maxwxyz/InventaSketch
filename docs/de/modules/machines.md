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
einen Reihenmotor, äußere Kröpfungen gegenüber den inneren). Kontextaktionen: nächste Bauform, nächste Maschinenart
(`cmd-cycle`), Wellenstümpfe an/aus, Gehäuseanbindung an/aus.

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

Ansichtswechsel (mit den `ViewDef.Icon`-Piktogrammen), **nächste Läuferbauart** (`param.rotor`, Symbol `cmd-cycle`),
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

## Bekannte Grenzen

- Antriebsstrangteile werden mit ihrer Welle nur verschoben (die Core-Kopplung verschiebt Teile, sie dreht sie nicht):
  Nach dem Drehen einer Welle die Teile darauf ebenfalls drehen (oder gemeinsam drehen).
- Nur illustrative Geometrie (siehe oben); keine Wicklungsschemata, keine Prüfung von Nut-/Polzahlkombinationen.
- Außenläufer-Komponenten sind standardmäßig nicht größengleich abgestimmt (Läuferring passend um den Ständer skalieren).
- Hosts: Die gesamte Ausgabe besteht aus Linien, Polylinien/Polygonen, Rechtecken, Ellipsen und Text; Schraffuren und
  Kupfer-Kreuzschraffuren werden als explizite Linien ausgegeben, wo ein Host keine Musterfüllung kennt (siehe
  Host-Fähigkeitsmatrix).
