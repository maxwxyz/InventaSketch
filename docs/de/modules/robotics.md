# Robotik-Modul (`robotics`)

Englisches Original: [../../en/modules/robotics.md](../../en/modules/robotics.md)

Roboter, Greifer, Koordinatensysteme, Roboterzellen und Sicherheitseinrichtungen für Folien und schnelle technische
Skizzen. Die vordefinierten Roboter werden per **Vorwärtskinematik** aus ihren Achswerten gestellt und in **Seiten- und
Draufsicht** gezeichnet; die Zeichnung ist daher für beliebige Winkel geometrisch richtig. Arbeitsräume werden aus den
Gliedlängen und Achsgrenzen berechnet.

**Umfang und Grenzen.** Ein Skizzenmodul, keine Robotersimulation, keine Offline-Programmierung und kein Werkzeug für
Sicherheitsauslegung: keine Dynamik, keine Kollisionsprüfung, keine inverse Kinematik der Arme, keine
Sicherheitsabstandsberechnung. Die Standardproportionen sind für die Roboterklasse plausibel, aber keinem Datenblatt
eines Herstellers entnommen. Symbole (Greifer, Lichtvorhang, Scannerfelder, Not-Halt, Zaun) sind eigenständig nach
üblicher Zeichenpraxis gezeichnet; es wird keine Norm wiedergegeben und **keine Normkonformität behauptet**.
Bereichsnamen („Eingeschränkter Raum“, „Kollaborationsraum“) sind reine Beschriftungen.

Das Modul hängt nur von `InventaSketch.Core` ab. Es zeichnet seine Armgeometrie selbst (es nutzt das Kinematik-Modul
nicht), folgt aber demselben Muster wie dessen Mechanismen: eine bewegte Achse für Schritte, Zwischenlagen und
`AnimationFrames`.

## Konventionen

- Einheiten mm, Blatt-y nach unten, Winkel in Grad **im Uhrzeigersinn auf dem Bildschirm** in der Ansicht, in der die
  Gelenkachse senkrecht zur Zeichnung steht (A1 in der Draufsicht, A2/A3/A5 in der Seitenansicht bei A1 = 0).
  Drehachsen (A4, A6 des Knickarms, A6 des Cobots) drehen rechtshändig um die vom Roboter weg zeigende Achse, also im
  Uhrzeigersinn beim Blick vom Roboter zum Werkzeug.
- Weltkoordinaten der Roboter: x nach vorn (in beiden Ansichten nach rechts), y vom Betrachter der Seitenansicht weg,
  z nach oben. Die Seitenansicht blickt entlang +y (Blatt = (x, −z)), die Draufsicht von oben entlang −z
  (Blatt = (x, −y)).
- **Quick Sketch zuerst:** jedes Element ist ohne Parameter einfügbar; freies (auch ungleichmäßiges) Skalieren ändert
  `Size`, nie Parameter. Roboter werden mit **einem einheitlichen Maßstab** in ihre Box eingepasst (Längenverhältnisse
  bleiben exakt); Symbole werden mit ihrer Box gestreckt.
- **Bildausschnitt der Roboter:** die Box zeigt die aktuelle Stellung und jede Stellung der *bewegten Achse*
  (Parameter `motion`) über ihren Bereich, die anderen Achsen wie gesetzt. Zwischenlagen und alle Animationsbilder
  teilen sich daher einen Ausschnitt, und der Sockel bewegt sich nicht, während die bewegte Achse dreht. Ändert sich
  etwas anderes (eine andere Achse, eine Gliedlänge, eine Grenze, die bewegte Achse, der Arbeitsraum), **folgt die Box
  dem Roboter**: Zeichnungsmaßstab und Blattposition des Sockels bleiben, die Boxgröße passt sich an
  (`OnParamChanged` sowie die Aktionen „Grundstellung“, die Achsauswahl, „Arbeitsraum“). Auch ein Ansichtswechsel
  behält den Zeichnungsmaßstab.
- Achswerte werden vor dem Zeichnen **auf die Achsgrenzen begrenzt**.
- Linienarten: Körper als mittlere Konturen (0,35 mm) mit heller Füllung (`#eef1f5`, dunkleres Grau für Sockel und
  Motoren), sodass eine Benutzerfüllung den Roboter umfärbt; Schema-Glieder als breite Linien; Zwischenlagen als
  Strich-Zweipunkt-Linie; Achsen als Mittellinien.

## Vorwärtskinematik

Alle Ketten bestehen aus elementaren Transformationen, `T_i = T_(i−1) · Trans(Versatz) · Rot(Achse, θ)` (Drehung
rechtshändig um die lokale Achse, `Frame3.Move` / `Frame3.Turn`), und werden orthogonal projiziert. Körper sind
konvexe Volumen, gezeichnet mit ihrer exakten Silhouette (Kapsel = kugelüberstrichene Strecke; Zylinder, Kegelstümpfe
und Quader = konvexe Hülle der projizierten Punkte), von hinten nach vorn gemalt.

### Knickarmroboter (6-achsig), `robot.arm.articulated`

Parameter (mm): `base` d1 = 500 (Schulterhöhe), `offset` a1 = 150, `upper` a2 = 650, `fore` d4 = 700 (Ellbogen bis
Handmittelpunkt), `wrist` d6 = 120 (Handmittelpunkt bis Flansch); der Ellbogenversatz c3 = 0,15·a2 (Unterarmachse über
der Ellbogenachse) ist fest. Nullstellung: Oberarm senkrecht, Unterarm waagerecht nach vorn.

```
F0 = Rot(−z, j1)
S  = F0 · (a1, 0, d1)                         Schulter (Achse A2 = F0·y)
F2 = [F0 in S] · Rot(y, j2);  E = F2 · (0, 0, a2)     Ellbogen
F3 = [F2 in E] · Rot(y, j3);  W = F3 · (d4, 0, c3)    Handmittelpunkt
F5 = [F3 in W] · Rot(x, j4) · Rot(y, j5)
Flansch = F5 · (d6, 0, 0),  Werkzeugachse = F5·x,  Flansch-Indexmarke dreht mit Rot(x, j6)
```

Prüfungen (Unit-Tests): alles null → Flansch (a1 + d4 + d6, 0, d1 + a2 + c3); j2 = 90 → (a1 + a2 + c3, 0, d1 − d4 − d6);
j1 = 90 → (0, −(a1 + d4 + d6), …), also im Uhrzeigersinn in der Draufsicht; j5 = 90 → Werkzeug senkrecht nach unten;
j4 = j5 = 90 → die Hand schwenkt nach +y.

### Kollaborativer Roboter, `robot.arm.cobot`

`base` d1 = 180, `upper` a2 = 425, `fore` a3 = 390, `wrist` d5 = 110, `flange` d6 = 100; der Oberarm läuft um 0,32·a2
seitlich versetzt neben der Gelenklinie (Gehäuse nebeneinander, in der Draufsicht sichtbar). Kette: `Rot(−z, j1)`,
Schulter bei (0, 0, d1), `Rot(y, j2)`, Ellbogen bei +a2 (z), `Rot(y, j3)`, Handgelenk 1 bei +a3 (x), `Rot(y, j4)`,
Handgelenk 2 bei +d5 (x), `Rot(z, j5)`, Flansch bei +d6 (x), `Rot(x, j6)`. Nullstellung: Flansch bei
(a3 + d5 + d6, 0, d1 + a2).

### SCARA, `robot.arm.scara`

`height` h = 400, `inner` a1 = 325, `outer` a2 = 275, `stroke` = 200. j1, j2 drehen um −z (im Uhrzeigersinn in der
Draufsicht), j3 ist der Pinolenhub (mm, nach unten, 0 … Hub), j4 die Werkzeugdrehung. Flansch =
(a1·cos j1 + a2·cos(j1 + j2), −(a1·sin j1 + a2·sin(j1 + j2)), h − 130·u − j3), u = (a1 + a2)/600.

### Delta-Roboter, `robot.arm.delta`

`height` H = 1000 (Ebene der Grundplatte), `baseRadius` R = 200, `upper` L = 300, `fore` l = 750, `platform` r = 60.
Arm i sitzt bei φ_i = 0°, 120°, 240° (u_i = (cos φ_i, sin φ_i, 0)); θ_i > 0 schwenkt den Arm nach unten (−40° … 90°).
Ellbogen E_i = R·u_i + (0, 0, H) + L·(cos θ_i·u_i − sin θ_i·z). Der Plattformmittelpunkt P erfüllt
|P + r·u_i − E_i| = l für alle drei Arme: er ist der untere Schnittpunkt dreier Kugeln mit Radius l um
C_i = E_i − r·u_i (Trilateration). Schneiden sich die Kugeln nicht, zeichnet der Roboter Grundplatte und Oberarme und
den Hinweis „Pose nicht erreichbar“ / „pose not reachable“. Der Werkzeugflansch liegt 60·u unter P.

### Linear- / Portalroboter, `robot.arm.gantry`

Achsen `x`, `y`, `z` in mm vom Hubanfang (`strokeX` 1200, `strokeY` 800, `strokeZ` 500, `height` 1500); z fährt den
Hub nach unten. Der Flansch bewegt sich exakt um (Δx, Δy, −Δz).

## Arbeitsräume

Eine ebene Zweigliederkette (inneres Glied L1, äußeres Glied L2, Schulterwinkel θ, Ellbogenwinkel φ ab gestreckter
Lage) erreicht den Abstand `ρ(φ) = √(L1² + L2² + 2·L1·L2·cos φ)` in Richtung `α = θ + β(φ)`,
`β(φ) = atan2(L2·sin φ, L1 + L2·cos φ)`. Für jedes |φ| (abgetastet) sind die erreichbaren Richtungen
`[θmin + β, θmax + β]` für jedes Vorzeichen von φ im Ellbogenbereich; die Fläche wird Ring für Ring aufgebaut
(untere Kante, Außenbogen, obere Kante, Innenbogen). Ringe, die rundum erreicht werden, werden mit einer Haarliniennaht
gegenüber der erreichbaren Mitte geschlossen, sodass die Fläche in jedem Host **eine Füllkontur** ist (Löcher lassen
sich in einer einzelnen PowerPoint-Freihandform nicht ausdrücken); die Kontur lässt die Naht aus. Sind beide
Ellbogenrichtungen erlaubt und überlappen ihre Richtungsintervalle nicht (sehr enge Schulterbereiche), wird die Hülle
beider Intervalle gezeichnet – eine Überschätzung.

| Roboter | Seitenansicht | Draufsicht |
|---|---|---|
| Knickarm | Arbeitsbereich des Handmittelpunkts in der Armebene (wie in Datenblattdiagrammen): L1 = a2, L2 = √(d4² + c3²), θ = j2 − 90° (j2 in [`j2min`, `j2max`]), φ = j3 + 90° − atan2(c3, d4) (j3 in [`j3min`, `j3max`]); auf der Seite gezeichnet, auf die der Arm zeigt | Kreisring oder Sektor R_in … R_out über ±`j1lim`, R aus der waagerechten Ausdehnung der Seitenfläche (R_in = 0, wenn sie die Achse kreuzt) |
| Cobot | Bereich des Handgelenks (Achse A4): L1 = a2, L2 = a3, θ = −90° + j2, φ = 90° + j3 (beide Ellbogenrichtungen) | Kreisring/Sektor über ±`j1lim` (Standard 360°: voll) |
| SCARA | Rechteck R_in … R_out × Pinolenhub, auf der Armseite | klassische Zweigliederfläche: L1 = a1, L2 = a2, θ ∈ ±`j1lim`, φ ∈ ±`j2lim` |
| Delta | **konvexe Hülle** der über die Armgrenzen abgetasteten Flanschpositionen (11³ Stichproben) – eine Näherung | ebenso, projiziert |
| Portal | Hubquader des Flansches (exakt) | Hubrechteck (exakt) |

Standardwerte: Knickarm `j1lim` 170°, A2 −70 … 110°, A3 −80 … 70°; Cobot ±360°, A2 ±110°, A3 ±160°; SCARA ±132°/±145°.
Der Arbeitsraum wird mit dem Parameter `envelope` gezeigt (getönte Fläche mit blauer Kontur) oder als eigenes Element
`robot.reach.envelope` (Knickarm-Parameter, Füllung getönt/schraffiert/keine, Radienangaben „R …“ in mm).

## Bibliothekskategorien und Objekttypen

Roboter haben die Ansichten `side` (`IconIds.Side`) und `top` (`IconIds.Top`); SCARA öffnet in der Draufsicht.
Werkzeuge haben `side` und `axial` (in Werkzeugachse, `IconIds.Axial`). Zellen- und Sicherheitssymbole haben `top` und
`side`. Koordinatensystem und TCP-Marke haben eine Zeichnung; Zaun und Bereich sind Punkt-(Polylinien-)Elemente.

| Id | Name (EN / DE) | Ansichten | Anker | Parameter |
|---|---|---|---|---|
| `robot.arm.articulated` | Articulated robot (6-axis) / Knickarmroboter (6-achsig) | side, top | Mount `base`, Mount `flange`, Port `tcp` | `j1`…`j6` (°), `base`, `offset`, `upper`, `fore`, `wrist`, `style`, `envelope`, `labels`, `ghosts`, `motion` (A5), Grenzen `j1lim`, `j2min/max`, `j3min/max` |
| `robot.arm.cobot` | Collaborative robot (cobot) / Kollaborativer Roboter (Cobot) | side, top | ebenso | `j1`…`j6`, `base`, `upper`, `fore`, `wrist`, `flange`, … `motion` (A4), Grenzen |
| `robot.arm.scara` | SCARA robot / SCARA-Roboter | top, side | ebenso | `j1`, `j2` (°), `j3` (mm), `j4`, `height`, `inner`, `outer`, `stroke`, … `motion` (A2), `j1lim`, `j2lim` |
| `robot.arm.delta` | Delta robot (parallel) / Delta-Roboter (Parallelkinematik) | side, top | ebenso (Sockel oben) | `j1`…`j3`, `height`, `baseRadius`, `upper`, `fore`, `platform`, … `motion` (A1) |
| `robot.arm.gantry` | Cartesian / gantry robot / Linear- / Portalroboter | side, top | ebenso | `x`, `y`, `z` (mm), `strokeX/Y/Z`, `height`, … `motion` (X) |
| `robot.mobile.agv` | Mobile robot (AGV / AMR) / Mobiler Roboter (FTF / AMR) | top, side | Mounts `deck`, `scanner1`, `scanner2` | `arrow` |
| `robot.gripper.parallel` | Parallel gripper / Parallelgreifer | side, axial | Mount `flange`, Port `tcp` | `open` (0…1), `arrows` |
| `robot.gripper.vacuum` | Vacuum gripper / Vakuumgreifer (Sauggreifer) | side, axial | ebenso | `cups` (1–4) |
| `robot.gripper.magnetic` | Magnetic gripper / Magnetgreifer | side, axial | ebenso | – |
| `robot.gripper.threefinger` | Three-finger gripper / Dreifingergreifer | side, axial | ebenso | `open` |
| `robot.tool.torch` | Welding torch / Schweißbrenner | side, axial | Mount `flange`, Port `tcp` an der Drahtspitze | – |
| `robot.tool.spindle` | Spindle (milling / drilling) / Spindel (Fräsen / Bohren) | side, axial | Mount `flange`, Port `tcp` | `arrow` |
| `robot.frame.coordinate` | Coordinate frame / Koordinatensystem | – | Port `origin` | `kind` world/base/tool/user/object, `axes` xz/xy/yz, `name` |
| `robot.frame.tcp` | Tool centre point (TCP) / Werkzeugarbeitspunkt (TCP) | – | Port `tcp` | `name` |
| `robot.reach.envelope` | Reach envelope (working range) / Arbeitsraum (Reichweite) | side, top | Mount `base` | Knickarm-Geometrie und -Grenzen, `fill`, `labels` |
| `robot.cell.workpiece` | Workpiece / Werkstück | top, side | Port `grip` (+ `bottom` in der Seitenansicht) | `shape` box/cylinder |
| `robot.cell.conveyor` | Belt conveyor / Gurtförderer (Förderband) | top, side | Ports `in`, `out`, Schiene `belt` (+ Mount `floor`) | `dir`, `arrow`, `legs` |
| `robot.cell.pallet` | Pallet / Palette | top, side | Port `load` (+ Schiene `deck`, Mount `floor`) | – |
| `robot.cell.pedestal` | Robot pedestal / Robotersockel | side, top | Mount `top` (+ `floor`) | – |
| `robot.safety.fence` | Safety fence / Schutzzaun | Punkte | Schienen entlang jedes Abschnitts | `closed`, `panel` (Blatt-mm), `mesh` |
| `robot.safety.door` | Safety fence door / Schutztür (Schutzzaun) | top, side | Ports `hinge`, `latch` | `kind` hinged/sliding |
| `robot.safety.curtain` | Safety light curtain / Sicherheits-Lichtvorhang | top, side | Ports `emitter`, `receiver` | `beams`, `field` |
| `robot.safety.scanner` | Safety laser scanner / Sicherheits-Laserscanner | top, side | Mount `body` | `shape` sector/rect, `angle` (270°), `ratio` (0,55), `labels` |
| `robot.safety.zone` | Safety zone / Sicherheitsbereich | Punkte (geschlossen) | Kantenmitten | `kind` hazard/protective/warning/restricted/collaborative, `fill`, `label` |
| `robot.safety.estop` | Emergency stop button / Not-Halt-Taster | top, side | Mount `mount` | – |

Bibliothek (`robot.robots`, `robot.tools`, `robot.frames`, `robot.cell`, `robot.safety`): ein Eintrag je Typ (Id = Typ-Id)
und jeder Roboter in seiner zweiten Ansicht (`<id>.top` / `<id>.side`), Knickarm mit Arbeitsraum (Seite und Draufsicht),
Knickarm und Cobot als kinematisches Schema mit Beschriftung, SCARA mit Arbeitsraum, geschlossener Parallelgreifer,
Vakuumgreifer mit 4 Saugnäpfen, zylindrisches Werkstück, geschlossener Zaun, Schiebetür, Scanner mit rechteckigen
Feldern, Bereiche (Warnfeld, Schutzfeld, eingeschränkter Raum, Kollaborationsraum), Werkzeug- und Weltkoordinatensystem,
Arbeitsraum in der Draufsicht. Suchbegriffe in beiden Sprachen (roboter, knickarm, greifer, sauggreifer, schutzzaun,
lichtvorhang, arbeitsraum, reichweite, ftf, not-halt, …).

## Anker (Andocken)

- Robotersockel: **Mount** `base` (Fuß auf dem Boden; beim Delta oben auf der Grundplatte). Er rastet auf dem Mount
  `top` des Robotersockels oder dem Mount `deck` des mobilen Roboters ein (mobiler Manipulator).
- Roboterflansch: **Mount** `flange` – der eigene Mount `flange` eines Greifers oder Werkzeugs rastet dort ein (Mount auf
  Mount); dazu **Port** `tcp` am selben Punkt für TCP-Marken und Koordinatensysteme (Port auf Port). Seit 25.11.2026 ist
  der Flansch-Mount entlang der Werkzeugachse **gerichtet** (`Anchor.Directed`, wo die Achse in der Ansicht sichtbar ist),
  und die Mounts der Werkzeuge zeigen zum Roboter zurück: Der Flansch bietet ein „+“ (Anbau-Port `mount.flange`) mit den
  Greifern und Werkzeugen, das gewählte wird **zur Werkzeugachse gedreht** auf den Flansch gesetzt (in PowerPoint
  geprüft, Szenario `robot-tool`). Ein auf den Flansch gezogenes Werkzeug rastet nur in der Lage ein. Das Werkzeug wird
  so skaliert, dass seine Flanschplatte zum Roboterflansch passt (beide Flächen geben ihren Radius an), und ein am
  Flansch angedocktes Werkzeug **folgt**, wenn sich ein Gelenk dreht oder der Roboter bewegt wird (`Coupling.Propagate`
  dreht und verschiebt an einem gerichteten Mount angedockte Teile; in PowerPoint geprüft: A5 + 15° dreht den Greifer am
  Flansch von −30° auf −15°).
- Werkzeuge: Port `tcp` am Greifpunkt / an der Drahtspitze / an der Werkzeugspitze; der Port `tcp` der TCP-Marke rastet
  dort ein.
- Zaun: Schienen entlang jedes Abschnitts; die Ports `hinge`/`latch` der Tür und die Ports des Lichtvorhangs docken
  beliebig darauf an.
- Förderband: Schiene `belt` entlang der Gurtoberfläche (Werkstück-Ports docken beliebig darauf an).

## Kontextaktionen

Roboter: `A… +15°` / `A… −15°` (50 mm bei Linearachsen) an der bewegten Achse, Achse ▾ (Auswahl der
bewegten Achse), „Grundstellung“, „Zwischenlagen“ (0 ↔ 4, über den Bereich der bewegten Achse), „Arbeitsraum“,
Darstellung Körperkontur ↔ kinematisches Schema (Options-Piktogramme detailliert/Schema), „Achsbeschriftung“
(J1… / A1… je nach UI-Sprache, zusammenfallende Achsen teilen sich eine Beschriftung „A5/A6“). Greifer:
öffnen/schließen, Hubpfeile, mehr/weniger Saugnäpfe. Übrige: Art ▾ / Form ▾ / Achsen ▾ (Auswahlen), Schalter für Namen, Pfeile,
Gitterband, Feldtönung, geschlossenen Zaun, Förderrichtung umkehren.

## Host-Hinweise

Alle Ausgaben nutzen native Primitive (Polygone, Polylinien, Rechtecke, Ellipsen, kubische Pfade, Text). Arbeitsräume
und Bereiche sind einzelne geschlossene Konturen (Löcher überbrückt), PowerPoint zeichnet sie als eine Freihandform;
Schraffuren nutzen das Schraffurmuster aus Core. Ein Vektorbild ist nicht nötig. Das Verhalten „Box folgt dem Roboter“
wirkt über `OnParamChanged` (Hosts, die Parameter über `Registry.SetParam` setzen) und die eigenen Aktionen des Moduls.
`AnimationFrames` fährt die bewegte Achse vom aktuellen Wert zu einer Grenze, zur anderen und zurück (erstes Bild = das
Objekt selbst, gleiche Box).

## Tests und Galerie

`tests/InventaSketch.Modules.Robotics.Tests`: Registrierung und Id-Konvention, zweisprachige Namen/Parameter/Aktionen,
Ansichts-Piktogramme, alle Parameter optional, Bibliothekseinträge lösen auf, Suche in beiden Sprachen; jeder
Bibliothekseintrag in jeder Ansicht in beiden Sprachen innerhalb seiner Box (auch gestreckt, gestaucht, jede
Auswahl/jeder Schalter/jedes Zahlenextrem, zufällige Achswerte jenseits der Grenzen); Skalieren ändert nur `Size`;
Vorwärtskinematik aller Roboter bei bekannten Winkeln, Schleifenschluss des Delta bei zufälligen Winkeln, Begrenzung
der Achswerte; Arbeitsraumradien aus den Gliedlängen (Zweigliederkette, Knickarm, SCARA, Portal, Kreisring); der
Ausschnitt enthält jede Stellung der bewegten Achse; Maßstab und Sockel bleiben bei Parameteränderungen und
Ansichtswechseln; Animationsbilder teilen die Box; Zwischenlagen sind Strich-Zweipunkt-Gruppen; Aktionen; Zaunpfosten
und Bereichsfüllungen. Der Galerietest schreibt `gallery/*.html` (jeder Typ in jeder Ansicht, gestreckt,
Schema-/Arbeitsraum-/Zwischenlagen-Varianten, Vorlagen, Stellungsfolgen, Animationsbilder und eine Beispielzelle).
