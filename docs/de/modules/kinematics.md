# Kinematik-Modul (`kinematics`)

Englische Originalfassung (maßgeblich): [../../en/modules/kinematics.md](../../en/modules/kinematics.md)

Das Kinematik-Modul stellt Symbole für kinematische Schemata (Getriebeskizzen) und vordefinierte ebene
Mechanismen für schnelle technische Skizzen und technische Abbildungen bereit. Die Symbolformen folgen dem *Stil*
der ISO 3952 (Kinematische Skizzen – grafische Symbole); sie sind eigenständig gezeichnet und keine Kopien der
Normbilder. Eine Normkonformität wird nicht behauptet.

**Umfang und Grenzen.** Es handelt sich um eine *kinematische Skizze*, keine Dynamiksimulation: Stellungen werden
per geschlossener Schleifengleichung für einen Antriebswinkel berechnet. Es gibt keine Massen, Kräfte,
Zeitintegration, Kollisionsprüfung oder Toleranzanalyse; Geschwindigkeiten gibt es nur als Elementwerte erster
Ordnung in der aktuellen Stellung (siehe „Elementwerte“). Der Kraftpfeil am Kniehebel und der
Antriebspfeil der Kurbel sind reine Symbole.

## Konventionen

- Einheiten in Millimetern, y nach unten, Winkel in Grad **im Uhrzeigersinn auf dem Bildschirm**
  (Projektkonvention). Beim Viergelenkgetriebe bedeutet `angle = -60` daher: Kurbel 60° nach rechts oben.
- Jedes Objekt ist ohne Parameter einfügbar. Freies gleichmäßiges und ungleichmäßiges Skalieren ändert nur `Size`.
- Kastensymbole (Gelenke, Körper) werden auf ihren Kasten gestreckt. **Mechanismen werden mit einem einheitlichen
  Maßstab** zentriert eingepasst, damit die Längenverhältnisse der Glieder auch bei ungleichmäßigem Skalieren exakt
  bleiben. Der Bildausschnitt umfasst den gesamten Bewegungsbereich, sodass die Zeichnung beim Ändern des Winkels
  nicht springt.
- Bewegte Glieder: breite Volllinie (ISO 128); Zwischenlagen: Strich-Zweipunkt-Linie (Lagen beweglicher Teile);
  Bahnkurven: schmale Strichlinie.
- Gelenkkreise sind weiß gefüllt, damit Gliedenden darunter verschwinden.

## Bibliothekskategorien und Objekttypen

### Gelenke & Lagerungen (`kin.joints`)

| Typ-ID | Name (EN / DE) | Ansichten | Fangpunkte |
|---|---|---|---|
| `kin.joint.revolute` | Revolute joint / Drehgelenk | Schema (Kreis), detailliert (Auge und Bolzen) | Port (Mitte) |
| `kin.joint.ground` | Fixed pivot / Gestelldrehpunkt (Festlager) | Schema (Dreieck, schraffierte Basis), detailliert (Lagerbock); mit Anschlägen zwei dünne Anschlaglinien und ein gestrichelter Schwenkbogen | Port (Drehpunkt), Mount (Basis); optional `limited`, `limitFrom`/`limitTo` (°, im Uhrzeigersinn von rechts, Vorgabe −135…−45 = nach oben): die Glieder daran schwenken nur in diesem Bereich |
| `kin.joint.prismatic` | Prismatic joint / Schubgelenk | Schema (Gleitstein auf schraffierter Führung), detailliert (zwei Schienen) | Port (Bolzen), Axis (Führungsachse), Mount |
| `kin.joint.cam` | Cam joint / Kurvengelenk (höheres Paar) | Schema, detailliert (schraffierte Körper) | Port (Berührpunkt) |
| `kin.joint.spherical` | Spherical joint / Kugelgelenk | Schema, detailliert (Kugelpfanne) | Port (Mitte) |
| `kin.joint.gear` | Gear contact / Zahneingriff | Schema (sich berührende Kreise), detailliert (Zähne angedeutet) | Port + Hub (Mittelpunkte), Teilkreise |

### Glieder & Körper (`kin.links`)

| Typ-ID | Name (EN / DE) | Geometrie | Parameter (alle optional) | Fangpunkte |
|---|---|---|---|---|
| `kin.link.binary` | Rigid link / Starres Glied | 2 Punkte | `startJoint`, `endJoint`, `joint` (Kreisradius) | Port an beiden Enden |
| `kin.link.ternary` | Ternary link / Dreigelenkglied | 3 Punkte | `joint` | Port an allen drei Gelenken |
| `kin.link.crank` | Crank / Kurbel | Kasten | – | Port (Drehpunkt, Bolzen), Mount |
| `kin.link.rocker` | Rocker / Schwinge | Kasten | – | Port (Drehpunkt, Bolzen), Mount |
| `kin.link.slider` | Slider block / Gleitstein | Kasten | – | Port + Hub (Mitte, rastet auf Führungsachsen) |
| `kin.link.piston` | Piston and cylinder / Kolben und Zylinder | Kasten | `stroke` (0..1) | Port (beide Augen), Mount, Axis |
| `kin.cam.follower` | Cam disc with follower / Kurvenscheibe mit Stößel | Kasten | `angle`, `follower` (Rolle/flach/Spitze), `base`, `lift`, `dof` | Mount + Hub (Kurvendrehpunkt), Port (Stößelende), Axis (Stößelführung); zerfällt in Gestelldrehpunkt, Kurvenscheibe und Stößel-Gleitstein |
| `kin.cam.disc` | Cam disc / Kurvenscheibe (Einzelteil, 25.11.2026) | Schema, detailliert (Nabe, Mittellinien); Kasten um den Drehpunkt, gleichmäßig | `base`, `lift`, `follower` (wie Stößel anliegen: Rolle mit 0,3 × Grundkreis, Flachstößel, Spitze); dreht sich mit seiner Drehung | Hub + Port (Drehpunkt); ein Gleitstein, dessen Führung durch die Scheibe läuft und dessen Gelenk auf der Kontur sitzt, ist ihr Stößel (`Cams`): Drehen der Scheibe im Bewegungsmodus bewegt den Stößel und die daran angedockte Kette |
| `kin.spring` | Spring / Feder | 2 Punkte | `coils`, `width` | Port an beiden Enden |
| `kin.damper` | Damper / Dämpfer | 2 Punkte | `width` | Port an beiden Enden |
| `kin.rope.pulley` | Rope over pulley / Seil über Rolle | Kasten | `load` | Hub + Port (Rolle), Port (Seilenden), Mount (Decke) |
| `kin.gripper` | Gripper jaws / Greiferbacken | Kasten | `open` (0..1) | Mount + Port (Flansch), Port (Greifpunkt) |

Alle Gliedtypen bieten die Ansichten `schematic` und `detailed` (Stabkontur, Platte, Schraubenfeder,
Dämpferzylinder, Schnitt …). Der Stößelkontakt wird aus der tatsächlichen Kurvenkontur berechnet
(Tangentennocken: Grundkreis plus Nockenkreis): Die Rolle liegt tangential an, der Flachstößel auf dem höchsten
Punkt, der Spitzenstößel im Schnittpunkt mit der Stößelachse.

### Getriebe & Mechanismen (`kin.mechanisms`)

Jeder Mechanismus ist ein Kastenobjekt mit den Ansichten `schematic` (Strichschema) und `detailed`
(Stabkonturen, Lagerböcke, schraffierte Gleitsteine).

| Typ-ID | Name (EN / DE) | Längenparameter (Vorgaben) | Antriebswinkel `angle` | F |
|---|---|---|---|---|
| `kin.mech.fourbar` | Four-bar linkage / Viergelenkgetriebe | `ground` 40, `crank` 12, `coupler` 36, `rocker` 30, `branch` offen/gekreuzt, `cpu`/`cpv` Koppelpunkt | Kurbelwinkel, Vorgabe −60° | 1 |
| `kin.mech.slidercrank` | Slider-crank / Schubkurbel | `crank` 12, `rod` 36, `offset` 0 | Kurbelwinkel, Vorgabe −60° | 1 |
| `kin.mech.scotchyoke` | Scotch yoke / Kreuzschubkurbel | `crank` 12 | Kurbelwinkel, Vorgabe −60° | 1 |
| `kin.mech.toggle` | Toggle mechanism / Kniehebel | `link1` 26, `link2` 26, `force` | Winkel des oberen Hebels zur Führungsachse (0° = Strecklage), Vorgabe 30° | 1 |
| `kin.mech.quickreturn` | Quick-return mechanism / Schnellrücklaufgetriebe | `ground` 20, `crank` 10, `lever` 40, `link` 16, `ram` 44 | Kurbelwinkel, Vorgabe −30° | 1 |
| `kin.mech.geneva` | Geneva drive / Malteserkreuzgetriebe (26.11.2026) | `slots` 4/6, `centre` 30 (O1-O2) | Winkel des Treibers, Vorgabe −20° | 1 |
| `kin.ratchet` | Ratchet and pawl / Klinkengesperre (26.11.2026) | `teeth` 12, `spring` (Klinkenfeder zeigen) | Winkel des Sperrrads, Vorgabe 0° | 1 |
| `kin.mech.sixbar` | Six-bar linkage / Sechsgliedriges Koppelgetriebe (26.11.2026) | `kind` watt/stephenson, `ground` 40, `crank` 12, `coupler` 36, `rocker` 30; Watt: `arm` 24, `armAngle` 60°, `ground2` 36, `coupler2` 28, `rocker2` 22; Stephenson: `cpu` 0,5, `cpv` −0,3, `link5` 40, `link6` 22, `x6` 62, `y6` −6 | Kurbelwinkel, Vorgabe −60° | 1 |
| `kin.mech.pantograph` | Pantograph / Storchschnabel (Pantograph) (26.11.2026) | `length` 40, `scale` 2, `reach` 20, `trace` 5 | Lage des Fahrstifts auf dem abgefahrenen Kreis, Vorgabe −90° | 2 |

Gemeinsame Parameter: `angle`, `ghosts` (0–12), `path` (Bahnkurve), `dof` (Freiheitsgrad-Anzeige). Malteserkreuz
und Klinkengesperre haben keine Zwischenlagen und keine Bahnkurve (ein zweites Rad würde die Zeichnung verdecken); sie
lassen sich wie die anderen animieren. Das Viergelenk hat zusätzlich `grashof` (Plakette mit der Grashof-Klasse oben
rechts, z. B. „Kurbelschwinge“ / „Crank-rocker“).

**Malteserkreuzgetriebe.** Treiberdrehpunkt O1, Raddrehpunkt O2 im Achsabstand C; Kurbelradius a = C·sin(180°/z),
Schlitzmündung bei R = C·cos(180°/z), sodass der Bolzen radial in die Schlitze ein- und austritt. Solange der Bolzen
in einem Schlitz ist (|φ| ≤ 90° − 180°/z), folgt der Schlitz dem Bolzen, sonst ist das Rad bei 180°/z gesperrt (eine
Schlitzteilung je Treiberumdrehung, dazwischen Rast). Das Rad wird mit parallelen Schlitzen, konkaven Sperrbögen
(Radius der Sperrscheibe) und kurzen Hörnern gezeichnet, der Treiber mit seiner zum Bolzen hin freigeschnittenen
Sperrscheibe. `Linkages.GenevaWheelAngle` liefert die Radstellung.

**Klinkengesperre.** Ein Sperrrad mit Sägezähnen und eine Klinke auf einem zweiten Gestelldrehpunkt, von einer Feder
(dünner Zickzack von einem schraffierten Gestellpunkt) auf die Zähne gedrückt. `angle` dreht das Rad; die Klinke
liegt an ihrer ersten Berührung mit dem tatsächlichen Zahnumriss an, gleitet also in der Bewegung jede Flanke hinauf
und fällt hinter den Zahn (im Uhrzeigersinn läuft das Rad frei; rückwärts läuft die steile Zahnbrust gegen die Klinke).

**Sechsgliedriges Koppelgetriebe.** Watt: zwei Viergelenke hintereinander, die Schwinge des ersten ist ein
Dreigelenkglied O4–B–C (als Platte gezeichnet), das das zweite Viergelenk O4–C–D–O6 treibt. Stephenson: die Koppel
A–B–P ist ein Dreigelenkglied, am Koppelpunkt P hängt die Zweischlag-Gruppe P–C–O6. Beide per Kreisschnitt; die
Bahnkurve zeichnet D (Watt) bzw. P (Stephenson). Sechs Glieder, sieben Drehgelenke: F = 1. „Zerlegen“ liefert
Gestelldrehpunkte, Zweigelenkglieder und ein Dreigelenkglied, aneinander angedockt.

**Storchschnabel.** Gestelldrehpunkt O, lange Stäbe O–B und B–P (`length`), kurze Stäbe C–T und T–D (`length`/k),
die das Parallelogramm C–B–D–T bilden. O, der Fahrstift T und der Zeichenstift P bleiben auf einer Geraden mit
OP = k·OT (`Linkages.Pantograph`). Der Fahrstift läuft auf einem Kreis (Radius `trace`, Mittelpunkt im Abstand `reach`
von O); mit `path` werden der abgefahrene Kreis und die vergrößerte Kopie des Zeichenstifts gezeichnet. Fünf Glieder,
fünf Drehgelenke: F = 2 (die Anzeige sagt das); der Antriebswinkel bewegt nur den Fahrstift entlang der Vorlage.

**Schleifenschluss.** Viergelenk: Das Gelenk B ist der Schnittpunkt der Kreise um den Kurbelzapfen A (Radius
Koppel) und den Schwingendrehpunkt O4 (Radius Schwinge); die Montagelage (`open`/`crossed`) wählt die Lösung und
bleibt über die ganze Umdrehung erhalten. Schubkurbel: B = (A.x + √(Pleuel² − (Versatz − A.y)²), Versatz).
Kniehebel: C liegt auf der Führungsachse durch den Gestelldrehpunkt. Schnellrücklauf (Kurbelschleife mit
Stößelkoppel): Der Kurbelzapfen gleitet auf der Schwinge O4–T, die Koppel T–B treibt den Stößel auf einer
waagerechten Führung. Die öffentliche Klasse `Linkages` stellt die Löser bereit (`FourBar`, `SliderCrank`,
`CircleCircle`, `Grubler`, `IsGrashof`) und seit 26.11.2026 die Analyse hinter den Elementwerten (`Classify`,
`GrashofMargin`, `TransmissionAngles`, `RockerSwing`, `CrankAngleDifference`, `TimeRatio`, `VelocityRatio`,
`SliderStroke`, `QuickReturnRam`, `GenevaWheelAngle`, `Pantograph`, …).

**Keine Montage möglich.** Lässt sich die Schleife beim gegebenen Winkel nicht schließen (z. B. Nicht-Grashof-Kette
oder Kurbel länger als das Pleuel), werden Gestell und Antriebsglied gezeichnet und unten im Kasten erscheint der
kursive Hinweis „keine Montage möglich“ / „no assembly possible“. Nicht montierbare Zwischenlagen und Bahnpunkte
werden ausgelassen; die Bahnkurve wird an solchen Lücken getrennt.

**Zwischenlagen.** `ghosts = N` zeichnet N weitere Stellungen in Strich-Zweipunkt-Linie (je eine Gruppe mit dem
Tag `ghost`): bei Kurbelgetrieben gleichmäßig über eine Umdrehung verteilt, beim Kniehebel zwischen aktuellem
Winkel und Strecklage.

**Bahnkurve.** `path = true` zeichnet die Bahn des Koppelpunkts (Viergelenk; mit `cpv` ≠ 0 ein Koppelpunkt
außerhalb des Stabs, gezeichnet als Koppeldreieck), der Pleuelmitte (Schubkurbel), des Kurbelzapfens
(Kreuzschubkurbel, Schnellrücklauf) oder des Kniegelenks (Kniehebel).

**Fangpunkte.** Gestelldrehpunkte sind `Mount`-Anker; alle Gelenkmittelpunkte (aktuelle Stellung) sind
`Port`-Anker, sodass Gliedenden, Drehgelenke und Federn Gelenk an Gelenk einrasten; Führungen von Gleitsteinen
sind `Axis`-Strecken, auf die Gleitsteine einrasten.

### Bewegungsangaben (`kin.motion`)

| Typ-ID | Name (EN / DE) | Geometrie | Parameter |
|---|---|---|---|
| `kin.motion.path` | Motion path / Bahnkurve | Punkte (≥ 2) | `smooth`, `arrow`, `closed` |
| `kin.motion.anglelimit` | Angle limits / Winkelbegrenzung | Kasten | `min` (−30°), `max` (45°), `labels` (Werte/Symbole/keine), `joint` |
| `kin.motion.dof` | Degree-of-freedom badge / Freiheitsgrad-Anzeige | Kasten | `links`, `lower`, `higher`, `formula` |

Die Freiheitsgrad-Anzeige zeigt den Getriebefreiheitsgrad nach Grübler/Kutzbach F = 3(n − 1) − 2·j1 − j2
(n Glieder einschließlich Gestell, j1 Gelenke mit Freiheitsgrad 1, j2 Gelenke mit Freiheitsgrad 2). Text:
„F = 1“ (Deutsch) bzw. „DOF = 1“ (Englisch). Die Winkelbegrenzung hat in ihrer Mitte einen `Port` und rastet so
auf ein Gelenk ein.

**Gelenkgrenzen (25.11.2026):** Ein Gestelldrehpunkt mit Anschlägen (`limited`) hält jedes Glied daran in seinem
Winkelbereich (`ObjectType.JointLimits`, Seitengrad; der Bereich spiegelt und dreht sich mit dem Symbol). Ziehen im
Bewegungsmodus endet an den Anschlägen, Koppelkurve und Zwischenlagen enden dort ebenfalls (die Kette wird bis zum
Anschlag der Kurbel abgefahren).

## Elementwerte (26.11.2026)

Viergelenk, Schubkurbel, Schnellrücklauf und Kreuzschubkurbel haben Elementwerte (Calc 1, `IValueType`;
engineering-calculations.md Abschnitte 2.6 und 3.4). Gliedlängen und Antriebswinkel sind die eigenen Parameter des
Mechanismus: sie sind eingegebene Werte, und die Eingabe einer Länge im Wertefenster ändert die Zeichnung. Alles
andere folgt aus den Lehrbuchbeziehungen von [S24] (Kerle/Corves/Hüsing; Uicker/Pennock/Shigley). Die
Antriebsdrehzahl `n` ist optional (Textparameter `inputSpeed`, min⁻¹; positiv = im Uhrzeigersinn, wie der Griff); mit
ihr folgen die Geschwindigkeiten in der aktuellen Stellung. Das Werteetikett (`showValues`) zeigt die Kartenwerte.

| Element | Variablen (Kartenwerte **fett**) | Beziehungen |
|---|---|---|
| Viergelenk | a, b, c, d (`crank`, `coupler`, `rocker`, `ground`), φ (`angle`), n, Δ_G, Klasse (Text), **μ_min**, **μ_max**, **ψ**, α, **Q**, i, ω₄ | Δ_G = (p + q) − (s + l); μ aus dem Kosinussatz cos μ = (b² + c² − x²)/(2bc) mit x = \|A O4\| über [max(\|d − a\|, \|b − c\|), min(d + a, b + c)]; nur Kurbelschwinge: ψ = arccos((d² + c² − (b + a)²)/(2dc)) − arccos((d² + c² − (b − a)²)/(2dc)), α = arccos((d² + (b − a)² − c²)/(2d(b − a))) − arccos((d² + (b + a)² − c²)/(2d(b + a))); Q = (180° + α)/(180° − α); i = ω₄/ω₂ = a·sin(φ − θ₃)/(c·sin(θ₄ − θ₃)) in der aktuellen Stellung und Montagelage; ω₄ = i·n |
| Schubkurbel | r, l, e (`crank`, `rod`, `offset`), φ, n, **H**, α, **Q**, **μ_min**, **v** | H = √((l + r)² − e²) − √((l − r)² − e²) (ohne Versatz 2r); α = arcsin(\|e\|/(l − r)) − arcsin(\|e\|/(l + r)); μ_min = 90° − arcsin((r + \|e\|)/l); v = n·dx_B/dφ |
| Schnellrücklauf | d, r, L, l, h (`ground`, `crank`, `lever`, `link`, `ram`), φ, **ψ**, α, **Q**, **H** | ψ = 2·arcsin(r/d); α = 180° − 2·arccos(r/d) (Q = 2 für r/d = 1/2); H aus der Lageanalyse über eine Umdrehung |
| Kreuzschubkurbel | r, φ, n, **H**, **v** | H = 2r; v = −n·r·sin φ |

Die Grashof-Klasse ist seit 26.11.2026 auch ein Elementwert: die textwertige Panel-Variable `cls` („Class“ /
„Klasse“, Art `kin.grashofClass`, Beziehung `kin.fourbar.class` [S24]) enthält die Klasse aus `Linkages.Classify`
(Kurbelschwinge, Doppelkurbel, Schwingkurbel, Doppelschwinge mit umlaufender Koppel, Durchschlaggetriebe,
Doppelschwinge Nicht-Grashof) und erscheint als dieser Text im Wertefenster, im Werteetikett (wenn gewählt) und im
Rechenblatt. Sie wird nur berechnet, nie eingegeben und nie in einer Rechnung verwendet (engineering-calculations.md
Abschnitt 1.2, „Textwertige Größen“); die Plakette (`grashof`) und das Vorzeichen von Δ_G bleiben. Schwingwinkel und
Zeitverhältnis gibt es nur bei der Kurbelschwinge (sonst unbekannt).

**Prüfungen** (Abschnitt 4 von engineering-calculations.md):

| Id | Elemente | Regel | Schwere | Quelle |
|---|---|---|---|---|
| `kin.transmission` | Viergelenk, Schubkurbel | μ verlässt irgendwo in der Bewegung 40° … 140° (μ_min < 40° oder μ_max > 140°) | Warnung | [S24] |
| `kin.grashof` | Viergelenk | die Kurbel kann nicht umlaufen (nicht Grashof mit Kurbel oder Gestell als kürzestem Glied) | Hinweis | [S24] |

**Verknüpftes Diagramm.** Das Viergelenk implementiert `ICharacteristics` (Calc 5): den Satz `transfer`,
„Abtriebswinkel über Antriebswinkel“ / „Output angle over input angle“, Abtriebswinkel ψ₄ über dem Antriebswinkel φ
(−180° … 180°, ein Punkt je Grad, Lücken wo nicht montierbar) und den Übertragungswinkel μ in einem zweiten Feld mit
Linien bei 40° und 140°. Die aktuelle Stellung ist ein Punkt; Ziehen längs φ trägt einen neuen Antriebswinkel ein.
„Kennlinie ▸“ fügt das Diagramm ein; das Viergelenk nennt seine verknüpften Diagramme als abhängige Objekte.

## Kontextaktionen

| Aktions-ID | Bezeichnung (EN / DE) | Icon | Gilt für |
|---|---|---|---|
| `kin.angle.next` / `kin.angle.prev` | Position +15° / −15° / Stellung +15° / −15° | `cmd-rotate` | Mechanismen, Kurvenscheibe |
| `kin.ghosts` | Ghost positions / Zwischenlagen (schaltet 0 ↔ letzte Anzahl, Vorgabe 4) | `cmd-animate` | Mechanismen |
| `kin.path` | Motion path / Bahnkurve | `cmd-toggle` | Mechanismen |
| `kin.dof` | Degree of freedom badge / Freiheitsgrad-Anzeige | `cmd-text` | Mechanismen, Kurvenscheibe |
| `kin.grashof.badge` | Show Grashof class / Grashof-Klasse zeigen | `cmd-values` | Viergelenk |
| `kin.slots=4/6` | Number of slots ▾ / Schlitzanzahl ▾ (Auswahl) | `cmd-option` | Malteserkreuz |
| `kin.sixbar.kind=watt/stephenson` | Chain ▾ / Kette ▾ (Auswahl) | `cmd-option` | Sechsgliedriges Koppelgetriebe |
| `values.chart=transfer` | Characteristic chart ▸ / Kennlinie ▸ (Menü, braucht das Charts-Modul) | `cmd-option` | Viergelenk |
| `kin.branch` | Crossed assembly mode / Gekreuzte Montagelage (nennt `branch` in `ParamKeys`: Hosts, die die Auswahl der Montagelage zeigen, lassen sie weg) | `cmd-swap` | Viergelenk |
| `kin.follower=roller/flat/knife` | Follower ▾ / Stößel ▾ (Auswahl) | `cmd-option` | Stößel, Kurvenscheibe |
| `kin.joint.start` / `kin.joint.end` | Gelenk am Anfang / Ende | `cmd-arrow-start` / `cmd-arrow-end` | Starres Glied |
| `kin.stroke`, `kin.open`, `kin.load` | Aus-/Einfahren, Öffnen/Schließen, Last | `cmd-toggle` | Kolben, Greifer, Seilrolle |
| `kin.path.*`, `kin.labels`, `kin.limit.joint`, `kin.dof.formula` | Bahn-, Beschriftungs- und Anzeigeoptionen | verschiedene | Bewegungsangaben |

Den Ansichtswechsel (`schematic`, `detailed`) bietet der Host über `ViewDef` mit den Icons `view-schematic`
und `view-detailed` an.

## Griffe (Ziehgriffe)

`DragHandles` / `MoveHandle` (Hosts zeichnen sie als blaue Griffe; interaction-ux.md Abschnitt 4). Nur Werte, die
Parameter des Typs sind, bekommen einen Griff:

| Element | Schlüssel | Griff an | Bewegung | Parameter | Raster |
|---|---|---|---|---|---|
| Viergelenk, Schubkurbel, Kreuzschubkurbel, Schnellrücklauf | `angle` | Kurbelzapfen A | frei; die Kurbel dreht sich um ihren Gestelldrehpunkt zum Zeiger hin | `angle` | 1°, auf (−180°, 180°] normiert |
| Malteserkreuz, Sechsgliedriges Koppelgetriebe | `angle` | Kurbelzapfen A | um O1 / O2 | `angle` | 1° |
| Klinkengesperre | `angle` | Spitze von Zahn 0 | um den Raddrehpunkt | `angle` | 1° |
| Storchschnabel | `angle` | Fahrstift T | um den Mittelpunkt des abgefahrenen Kreises | `angle` | 1° |
| Kniehebel | `angle` | Kniegelenk B | um den oberen Drehpunkt A | `angle` | 1° |
| Kurvenscheibe mit Stößel | `angle` | Nocken (äußerster Punkt) | um den Drehpunkt der Scheibe | `angle` | 1° |
| Kolben und Zylinder | `stroke` | Stangenende | längs der Zylinderachse | `stroke` | 5 %, 0 … 1 |
| Greiferbacken | `open` | rechte Backe | seitlich | `open` | 5 %, 0 … 1 |
| Gestelldrehpunkt mit Anschlägen (`limited`) | `limitFrom`, `limitTo` | äußeres Ende jeder Anschlaglinie | um den Drehpunkt | `limitFrom`, `limitTo` | 5°, innerhalb einer halben Drehung vom alten Wert |
| Winkelbegrenzung | `min`, `max` | äußeres Ende jedes Grenzstrahls | um das Gelenk | `min`, `max` | 5°, innerhalb einer halben Drehung vom alten Wert |

Der Antriebswinkel folgt dem eigenen Drehsinn des Mechanismus (aus der gelösten Stellung ermittelt), so bewegt sich der
Griff bei jedem Mechanismus mit dem Zeiger. Die Beschriftungen nennen den Wert: „Antriebswinkel: −60°“ / „Input angle:
−60°“, „Kolbenstellung: 45 %“ / „Piston position: 45 %“, „Backenöffnung: 60 %“ / „Jaw opening: 60 %“, „Anschlag von:
−135°“ / „Stop from: −135°“, „Minimaler Winkel: −30°“ / „Minimum angle: −30°“. Selbst gebaute Koppelgetriebe (Glieder
und Gelenke als Punktobjekte) bewegt man über ihre Punktgriffe und den Bewegungsmodus, nicht über Griffe.

## Hinweise für Hosts

Alle Ausgaben verwenden native Grundelemente (Linien, Polylinien, Kreise, Rechtecke, Text, Gruppen); Schraffuren
werden als explizite Linien ausgegeben, wo Hosts keine Musterfüllung haben. Zwischenlagen sind `GroupPrim`s.
Ein Vektorbild-Rückfall ist nicht nötig.

Mechanismen liefern `AnimationFrames(o, count)`: den aktuellen Zustand, dann den Antriebswinkel in Schritten wie die
Zwischenlagen (eine volle Umdrehung oder den Bereich des Kniehebels). Hosts machen daraus eine Folienfolge (PowerPoint
COM: „Als Folien animieren“ mit Morph-Übergängen); jedes Bild behält die Box.

„Zerlegen“ (`Parts`) gibt es für Viergelenk, Schubkurbel, Kreuzschubkurbel, Kniehebel, Schnellrücklauf, Kurvenscheibe
und das sechsgliedrige Koppelgetriebe (beide Ketten). Malteserkreuz, Klinkengesperre und Storchschnabel werden nicht
zerlegt: das selbst gebaute Koppelgetriebe kennt kein aussetzendes Bolzen-Schlitz-Paar, keinen Klinkenkontakt und
keinen Stab mit drei Gelenken in einer Reihe; sie bewegen sich über ihren Griff und die Animationsbilder. Räder,
Scheiben und Klinken sind weiß gefüllte Polygone (Tags `wheel`, `disc`, `pawl`), in PowerPoint native Freihandformen.
