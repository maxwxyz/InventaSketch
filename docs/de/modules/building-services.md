# Modul Technische Gebäudeausrüstung (`building-services`)

Heizung, Sanitär, Lüftung und Elektroinstallation im Haus (TGA) für Folien und schnelle technische Skizzen. Jedes
Element hat **dieselben drei Ansichten** einer Installation:

| View-Id | Bezeichnung (EN / DE) | Icon | Inhalt |
|---|---|---|---|
| `plan` | Plan / Grundriss | `IconIds.Plan` | Draufsicht wie im Grundriss; die Wand liegt an der **Oberkante** der Box |
| `section` | Section / elevation / Schnitt / Ansicht | `IconIds.Front` | das Element von vorn gesehen in seiner Montagehöhe |
| `schematic` | Schematic (riser) / Schema (Strang) | `IconIds.Schematic` | Symbol im Strang- bzw. Fließschema |

Das Modul ist **kein BIM**: Hinter der Zeichnung steht kein Gebäudemodell, sondern sauber gezeichnete, editierbare
Symbole, die ihre Anschlüsse und ihren Platz behalten, wenn die Installation die Ansicht wechselt. Die Symbole sind
eigenständig nach üblicher Zeichenpraxis gezeichnet; es werden keine Normtabellen oder -texte wiedergegeben und keine
Normkonformität behauptet.

## Aufteilung mit Architektur- und Elektromodul

| Thema | Modul | Hinweis |
|---|---|---|
| Wände, Räume, Türen, Fenster, Möbel | Architektur | unverändert |
| `arch.hvac.radiator/heatpump/boiler`, `arch.sanitary.*`, `arch.hvac.pipe` | Architektur | einfache **reine Grundriss**-Symbole der Einrichtung; bleiben für Grundrisse ohne Installation |
| Heizkörper, Kessel, Wärmepumpe, WC, Waschbecken, Dusche, Wanne, Rohrleitungen **als Installationselemente** | TGA | drei Ansichten, Montagehöhe, Anschlüsse mit gleicher Id in jeder Ansicht; Grundrisszeichnung und Rohrfarben an die Architektur angeglichen |
| Steckdose, Lichtschalter, Leuchte, Abzweigdose, Verteiler, Leitungsführung (`elec.house.*`), Übersichtsschaltplan | Elektro | hier **nicht doppelt** |
| Kommunikationsanschluss, Melder, Geräteanschlussdose, Potentialausgleichsschiene | TGA | fehlten bisher im Elektromodul |

Folgewunsch (Eigentümer des Elektromoduls): `elec.house.socket/switch/light/junction/board/cable` erhalten die
Ansichten `section` und `schematic` sowie einen Parameter `height` nach den Konventionen unten, damit die
Elektroinstallation mit dem übrigen Haus umschaltet.

## Konventionen

- **Quick Sketch zuerst:** Jedes Element lässt sich ohne Parameter einfügen; freies (auch ungleichmäßiges) Skalieren
  ändert `Size`, nie Parameter. Jede Zeichnung bleibt in ihrer Box.
- **Planmaßstab:** Größen sind wie im Architekturmodul für **1:100** angelegt. Alle Typen nutzen den Planmaßstab
  (`UsesPlanScale`): optionaler erweiterter Parameter `scale`, sonst Dokument-Planmaßstab, sonst 1:100.
- **Montagehöhe** `height` (mm, jeder Typ): **Unterkante über Oberkante Fertigfußboden** (negativ = im oder unter dem
  Boden). Vorgaben: Heizkörper 150, Waschbecken 500 (Oberkante 850), Kessel 1100, Lüftungsgerät 1000, Dosen 300,
  Melder 2440, Luftdurchlass 2450, stehende Elemente 0, Fußbodenheizung −70, Bodenablauf −150, Rohre 0, Luftleitungen
  2400. Die Kontextaktion „Montagehöhe…“ nimmt `850`, `85 cm`, `0,85 m`, `-50`.
- **Maßstäbliche Elemente und Symbole:** Geräte (Heizkörper, Kessel, Sanitärobjekte, Speicher, Geräte) sind
  maßstäblich; Armaturen in der Leitung, Dosen, Melder, Luftdurchlässe und Bodenablauf sind Symbole in lesbarer Größe.
- **Ansichtswechsel eines Elements:** Die **Breite bleibt** (Grundriss und Ansichten teilen die x-Ausdehnung).
  Maßstäbliche Elemente übernehmen die reale Höhe der Zielansicht im Planmaßstab des Objekts (Heizkörper 600 mm im
  Schnitt = 6 mm bei 1:100, 12 mm bei 1:50); Symbole behalten ihren optischen Maßstab.

## Abbildung Grundriss ↔ Schnitt ↔ Schema (für Core)

`ServiceViews.Switch(registry, doc, ids, view, floorY?)` setzt die Abbildung als getestete Referenz um; sie nutzt nur
allgemeine Daten (Ansichten `plan`/`section`/`schematic`, Parameter `height`, Planmaßstab, Anker) und kann daher
unverändert neben `AssemblyViews` nach Core wandern.

1. **Blickrichtung.** Der Schnitt blickt vom unteren Seitenrand auf den Grundriss (Richtung −y), wie die Wandansicht des
   Architekturmoduls. **x ist gemeinsam**: Jedes Element behält in jeder Ansicht seine Mitte in x und seine Breite.
2. **Fußbodenlinie.** `floorY` = die unterste Grundrisskante der Auswahl (größtes y auf der Seite), sofern nicht
   angegeben. Sie wird an jedem Element gespeichert (`Meta["bs.floorY"]`), damit spätere Wechsel dieselbe Linie nutzen.
3. **Grundriss → Schnitt/Schema (Boxen):** y und Drehung im Grundriss werden gespeichert (`Meta["bs.planY"]`,
   `Meta["bs.planRotation"]`), die Drehung wird 0, die Größe passt sich an (Breite bleibt) und die Unterkante kommt auf
   `bottomY = floorY − height / scale`.
4. **Schnitt ↔ Schema:** x und Unterkante bleiben; nur Symbol und Boxhöhe ändern sich. Das Schema ist ein abgewickeltes
   Strangschema: gleiche Reihenfolge waagerecht, Höhenlage nach Montagehöhe.
5. **Schnitt/Schema → Grundriss:** Das Element erhält die Montagehöhe, die es jetzt hat
   (`height = (floorY − bottomY) × scale`; im Schnitt nach oben gezogen liegt es also höher), y und Drehung im Grundriss
   kommen zurück, x bleibt (seitlich im Schnitt verschoben, verschiebt es sich auch im Grundriss).
6. **Leitungen** (Rohre, Luftleitungen): Grundriss → Ansicht speichert die Grundrisspunkte (`Meta["bs.planPoints"]`) und
   setzt jeden Punkt auf `(x, floorY − height / scale)`; senkrechte Abschnitte fallen zusammen (Abschnitte der Länge 0
   werden nicht gezeichnet). Zurück im Grundriss werden die Punkte wiederhergestellt, `height` folgt dem mittleren y der
   freien Punkte. An Anschlüssen angedockte Enden folgen ihrem Element (`Attachments.Refresh`, Anschluss-Ids sind in
   jeder Ansicht gleich). Ein Ende an einem Anschluss, den das Ziel in der neuen Ansicht nicht hat (der
   Grundrissanschluss `riser` eines Strangs), rutscht in der Höhe der Leitung auf die **Schiene** des Ziels und behält
   seine Anschluss-Id für den Rückweg.
7. Elemente an Seitenwänden erscheinen in Vorderansicht an ihrer x-Position (abgewickelte Ansicht); ein echter Schnitt
   je Wand bräuchte ein Schnittlinienobjekt und gehört nicht zum Umfang.

## Objekttypen

Größen: Grundriss / Schnitt / Schema bei 1:100 (B x H, mm auf der Seite).

| Id | Name (EN / DE) | Größen | Befestigung | Anschlüsse (gleiche Ids in jeder Ansicht) | Parameter |
|---|---|---|---|---|---|
| `bs.pipe` | Pipe / Rohrleitung | Punkte | – | Schienen auf jedem Abschnitt | `medium` Vorlauf/Rücklauf/Kalt/Warm/Zirkulation/Schmutzwasser, `height`, `dn`, `arrow`, `hops` |
| `bs.duct` | Air duct / Luftleitung | Punkte | – | Schienen | `kind` Zuluft/Abluft/Außenluft/Fortluft, `width` (160 mm), `height`, `arrow` |
| `bs.heat.boiler` | Boiler (wall-hung) / Heizkessel / Therme | 4,4x3,5 / 4,4x7 / 4,4x4,4 | Wand | flow, return | – |
| `bs.heat.heatpump` | Heat pump (outdoor unit) / Wärmepumpe (Außeneinheit) | 10x4 / 10x8 / 10x5 | Boden | flow, return | Beschriftung HP/WP nach UI-Sprache |
| `bs.heat.radiator` | Radiator / Heizkörper | 10x1,2 / 10x6 / 10x4 | Wand | flow, return | – |
| `bs.heat.floorloop` | Underfloor heating loop / Fußbodenheizung (Heizkreis) | 30x30 / 30x0,6 / 30x5 | im Boden | flow, return | `spacing` (150 mm, im Planmaßstab gezeichnet) |
| `bs.heat.manifold` | Heating circuit manifold / Heizkreisverteiler | 6x1,2 / 6x4 / 6x3 | Wand | flow, return, f1…fn, r1…rn | `circuits` (4, 1–12; Aktionen mehr/weniger) |
| `bs.heat.pump` | Circulator pump / Umwälzpumpe | 3x3 (Symbol) | in der Leitung | in, out | – |
| `bs.heat.mixer` | Mixing valve (3-way) / Mischventil (3-Wege) | 4x4 (Symbol) | in der Leitung | a, ab, b | – |
| `bs.heat.expansion` | Expansion vessel / Ausdehnungsgefäß | 3,6x3,6 / 3,6x5 / 3,6x4,4 | Wand | pipe | – |
| `bs.heat.trv` | Thermostatic valve / Thermostatventil | 3x3 (Symbol) | in der Leitung | in, out | – |
| `bs.san.wc` | WC / WC | 3,6x5,4 / 3,6x4 / 3,6x3,6 | Boden | waste, cold | – |
| `bs.san.basin` | Washbasin / Waschbecken | 6x4,5 / 6x4 / 6x3,5 | Wand | waste, hot, cold | – |
| `bs.san.shower` | Shower / Dusche | 9x9 / 9x22 / 9x8 | Boden | waste, hot, cold | – |
| `bs.san.bathtub` | Bathtub / Badewanne | 17x7,5 / 17x6 / 17x4 | Boden | waste, hot, cold | – |
| `bs.san.heater` | Water heater (storage cylinder) / Warmwasserspeicher | 6x6 / 6x16 / 6x10 | Boden | hot, circulation, cold (+ flow, return mit Heizwendel) | `coil` (true) |
| `bs.san.drain` | Floor drain / Bodenablauf | 2x2 / 2x1,5 / 2x2 (Symbol) | im Boden | waste | – |
| `bs.san.riser` | Riser (vertical pipe) / Steigleitung / Fallleitung | 2x2 / 2x28 / 2x28 | Boden | Grundriss: riser; Schnitt/Schema: bottom, top + Schiene | `medium`, `dir` steigend/fallend/durchgehend |
| `bs.vent.terminal` | Air terminal (valve / grille) / Luftdurchlass (Ventil / Gitter) | 3x3 / 3x1,6 / 3x3 (Symbol) | Decke | duct | `kind` Zuluft/Abluft |
| `bs.vent.unit` | Ventilation unit with heat recovery / Lüftungsgerät mit Wärmerückgewinnung | 7x4,5 / 7x8 / 7x5 | Wand | outdoor, exhaust, extract, supply | – |
| `bs.elec.data` | Communication outlet / Kommunikationsanschluss | 4x4 (Symbol) | Wand | cable | `kind` Daten/Telefon/TV |
| `bs.elec.detector` | Detector / Melder | 4x4 / 4x1,4 / 4x4 (Symbol) | Decke | cable | `kind` Rauch/Wärme/Bewegung |
| `bs.elec.appliance` | Appliance outlet / Geräteanschlussdose | 4x4 (Symbol) | Wand | cable | `kind` Herd/Waschmaschine/Trockner/Geschirrspüler/allgemein |
| `bs.elec.bonding` | Equipotential bonding bar / Potentialausgleichsschiene | 6x2 / 6x2 / 6x3 (Symbol) | Wand | earth + Schiene entlang der Schiene | – |

Jeder Box-Typ hat außerdem `height` und den erweiterten Parameter `scale`. Beschriftungen in Zeichnungen folgen der
UI-Sprache (HP/WP, DHW/WW, Kürzel F/VL, R/RL, CW/KW, HW/WW, HWC/Z, W/SW, SUP/ZUL, ETA/ABL, ODA/AUL, EHA/FOL,
LAN/TEL/TAE/TV/SAT, S/RM, H/WM, PIR/BM, CK/HE, …). Rohrfarben wie im Architekturmodul (Vorlauf rot, Rücklauf blau
gestrichelt, Kaltwasser grün, Warmwasser magenta Strich-Punkt, Schmutzwasser braun, breiter); Zirkulation violett
Strich-Punkt-Punkt. Luftfarben: Zuluft blau, Abluft ocker, Außenluft grün, Fortluft braun. Farben und Kürzel sind
übliche Praxis, keine zitierte Norm.

## Bibliothek

Je Typ ein Eintrag (Id = Typ-Id), je Box-Typ eine Vorlage `<typ>.section` und `<typ>.schematic` (damit die Einfügebox
Elemente in der Ansicht der Auswahl anbietet), Rohre je Medium (`bs.pipe.flow` … `bs.pipe.waste`; Vorlauf/Rücklauf in
der Kategorie Heizung), Luftleitungen je Luftart (`bs.duct.supply` …) und Varianten: `bs.heat.radiator.long`,
`bs.heat.manifold.8`, `bs.heat.floorloop.small`, `bs.san.riser.waste` (Fallleitung), `bs.san.riser.hot`,
`bs.san.riser.flow`, `bs.san.riser.return`, `bs.san.heater.electric`, `bs.vent.terminal.extract`,
`bs.elec.data.phone`, `bs.elec.data.tv`, `bs.elec.detector.heat`, `bs.elec.detector.motion`,
`bs.elec.appliance.washer`, `bs.elec.appliance.dishwasher`.

Kategorien: `bs.heating` Heating / Heizung, `bs.plumbing` Plumbing & sanitary / Sanitär & Trinkwasser,
`bs.ventilation` Ventilation / Lüftung, `bs.electrical` Electrical installation (additions) / Elektroinstallation
(Ergänzungen).

## Anker und Linienlogik

- **Anschlüsse** (`AnchorRole.Port`) tragen in jeder Ansicht dieselbe Id (Ausnahme: Strang, siehe oben); Rohre,
  Luftleitungen und Kabel docken dort an und folgen dem Element.
- **Befestigungen** (`AnchorRole.Mount`): im Grundriss `wall` an der Oberkante wandständiger Elemente (sonst
  `floor`/`ceiling` in der Mitte); in Schnitt und Schema der Bezugspunkt der Höhe — `floor` bzw. `wall` in der Mitte der
  Unterkante, `ceiling` in der Mitte der Oberkante. Armaturen in der Leitung haben keine Befestigung.
- **Schienen** (`AnchorRole.Rail`): jeder Abschnitt von Rohren und Luftleitungen (`rail0`, `rail1` …), die Achse des
  Strangs in Schnitt und Schema, die Potentialausgleichsschiene.
- **Linienlogik:** Rohre und Luftleitungen erhalten Ecken (`InsertablePoints`), lassen sich an beiden Enden verlängern,
  Abzweige docken an ihren Schienen an (`Polylines.Branch`); Rohrabzweige zeichnen einen Verbindungspunkt. Rohre
  springen über frühere Rohre (`HopsAtCrossings`, abschaltbar mit `hops`); Luftleitungen nicht (Doppellinien).
- Luftleitungen: Doppellinie der Breite `width / scale` mit Gehrungsecken, Endlinien und Strich-Punkt-Achse in Grundriss
  und Schnitt; im Schema eine Linie mit Luftkürzel. Rohre: Medienlinie in Grundriss und Schnitt; das Schema ergänzt
  Medienkürzel und `DN …`.

## Kontextaktionen

Montagehöhe… (Eingabe, jedes Element), Medium/Art weiterschalten (Rohr, Luftleitung, Strang, Luftdurchlass, Dosen,
Melder), Fließpfeil und Brücken (Rohr), Luftrichtungspfeil (Luftleitung), mehr/weniger Heizkreise (Verteiler),
Heizwendel (Speicher), Strangrichtung.

## Hinweise für Hosts

Alle Ausgaben bestehen aus nativen Grundelementen (Linien, Polylinien, Polygone, Rechtecke, Ellipsen, Bögen,
kubische Pfade, Text); jeder Host zeichnet sie nativ, ein Vektorbild ist nicht nötig. Rohre hängen von kreuzenden
Rohren ab (Brücken) und melden diese als abhängige Objekte. Der Ansichtswechsel der Installation
(`ServiceViews.Switch`) liegt in der Engine; Hosts brauchen nur einen Befehl „Ansicht für die Auswahl“, sobald
Core/Engine ihn anbieten.

## Tests und Galerie

`tests/InventaSketch.Modules.BuildingServices.Tests`: Registrierung und Id-Konventionen, zweisprachige Namen, die drei
Ansichten mit ihren Icons, Planmaßstab und `height` an jedem Typ, jeder Bibliothekseintrag in jeder Ansicht in beiden
Sprachen innerhalb seiner Box (auch gestreckt, gestaucht und mit jeder Auswahl), Skalieren ändert `Size`, nicht
Parameter, Ansichten zeichnen unterschiedliche Symbole, Anschlüsse mit gleichen Ids in jeder Ansicht, Befestigungen,
Schienen, Linienlogik (Ecken, Verlängern, Abzweige, Verbindungspunkte, Brücken), Breite der Luftleitung im
Planmaßstab, der Ansichtswechsel (x bleibt, Höhenabbildung, angedockte Rohre, Andocken an die Strangschiene,
Rückweg in den Grundriss, Höhe aus Verschieben im Schnitt, Drehung) und der JSON-Rundlauf. Der Galerietest schreibt
`gallery/*.html` (jeder Typ in seinen drei Ansichten, gestreckt, Vorlagen und ein Beispielbad in Grundriss, Schnitt
und Schema).
