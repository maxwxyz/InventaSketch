# Kinematik-Modul (`kinematics`)

Englische Originalfassung (maßgeblich): [../../en/modules/kinematics.md](../../en/modules/kinematics.md)

Das Kinematik-Modul stellt Symbole für kinematische Schemata (Getriebeskizzen) und vordefinierte ebene
Mechanismen für schnelle technische Skizzen und Patentzeichnungen bereit. Die Symbolformen folgen dem *Stil*
der ISO 3952 (Kinematische Skizzen – grafische Symbole); sie sind eigenständig gezeichnet und keine Kopien der
Normbilder. Eine Normkonformität wird nicht behauptet.

**Umfang und Grenzen.** Es handelt sich um eine *kinematische Skizze*, keine Dynamiksimulation: Stellungen werden
per geschlossener Schleifengleichung für einen Antriebswinkel berechnet. Es gibt keine Massen, Kräfte,
Geschwindigkeiten, Zeitintegration, Kollisionsprüfung oder Toleranzanalyse. Der Kraftpfeil am Kniehebel und der
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
| `kin.cam.follower` | Cam disc with follower / Kurvenscheibe mit Stößel | Kasten | `angle`, `follower` (Rolle/flach/Spitze), `base`, `lift`, `dof` | Mount + Hub (Kurvendrehpunkt), Port (Stößelende), Axis (Stößelführung) |
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

Gemeinsame Parameter: `angle`, `ghosts` (0–12), `path` (Bahnkurve), `dof` (Freiheitsgrad-Anzeige).

**Schleifenschluss.** Viergelenk: Das Gelenk B ist der Schnittpunkt der Kreise um den Kurbelzapfen A (Radius
Koppel) und den Schwingendrehpunkt O4 (Radius Schwinge); die Montagelage (`open`/`crossed`) wählt die Lösung und
bleibt über die ganze Umdrehung erhalten. Schubkurbel: B = (A.x + √(Pleuel² − (Versatz − A.y)²), Versatz).
Kniehebel: C liegt auf der Führungsachse durch den Gestelldrehpunkt. Schnellrücklauf (Kurbelschleife mit
Stößelkoppel): Der Kurbelzapfen gleitet auf der Schwinge O4–T, die Koppel T–B treibt den Stößel auf einer
waagerechten Führung. Die öffentliche Klasse `Linkages` stellt die Löser bereit (`FourBar`, `SliderCrank`,
`CircleCircle`, `Grubler`, `IsGrashof`).

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

## Kontextaktionen

| Aktions-ID | Bezeichnung (EN / DE) | Icon | Gilt für |
|---|---|---|---|
| `kin.angle.next` / `kin.angle.prev` | Next / previous position (±15°) / Nächste / vorherige Stellung | `cmd-rotate` | Mechanismen, Kurvenscheibe |
| `kin.ghosts` | Ghost positions / Zwischenlagen (schaltet 0 ↔ letzte Anzahl, Vorgabe 4) | `cmd-animate` | Mechanismen |
| `kin.path` | Motion path / Bahnkurve | `cmd-toggle` | Mechanismen |
| `kin.dof` | Degree of freedom badge / Freiheitsgrad-Anzeige | `cmd-text` | Mechanismen, Kurvenscheibe |
| `kin.branch` | Crossed assembly mode / Gekreuzte Montagelage | `cmd-swap` | Viergelenk |
| `kin.follower` | Next follower type / Nächste Stößelform | `cmd-cycle` | Kurvenscheibe |
| `kin.joint.start` / `kin.joint.end` | Gelenk am Anfang / Ende | `cmd-arrow-start` / `cmd-arrow-end` | Starres Glied |
| `kin.stroke`, `kin.open`, `kin.load` | Aus-/Einfahren, Öffnen/Schließen, Last | `cmd-toggle` | Kolben, Greifer, Seilrolle |
| `kin.path.*`, `kin.labels`, `kin.limit.joint`, `kin.dof.formula` | Bahn-, Beschriftungs- und Anzeigeoptionen | verschiedene | Bewegungsangaben |

Den Ansichtswechsel (`schematic`, `detailed`) bietet der Host über `ViewDef` mit den Icons `view-schematic`
und `view-detailed` an.

## Hinweise für Hosts

Alle Ausgaben verwenden native Grundelemente (Linien, Polylinien, Kreise, Rechtecke, Text, Gruppen); Schraffuren
werden als explizite Linien ausgegeben, wo Hosts keine Musterfüllung haben. Zwischenlagen sind `GroupPrim`s.
Ein Vektorbild-Rückfall ist nicht nötig.

Mechanismen liefern `AnimationFrames(o, count)`: den aktuellen Zustand, dann den Antriebswinkel in Schritten wie die
Zwischenlagen (eine volle Umdrehung oder den Bereich des Kniehebels). Hosts machen daraus eine Folienfolge (PowerPoint
COM: „Als Folien animieren“ mit Morph-Übergängen); jedes Bild behält die Box.
