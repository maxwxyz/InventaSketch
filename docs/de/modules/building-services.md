# Modul Technische Gebäudeausrüstung (`building-services`)

Heizung, Sanitär, Lüftung, Kälte (Split-Klimagerät) und Elektroinstallation im Haus (TGA) für Folien und schnelle technische Skizzen. Jedes
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

Ein Element, ein Eintrag: die Raumgeräte des Verteilermoduls treten für diese maßgeblichen Einträge zurück, solange beide
registriert sind (`LibraryItem.Alternative`): Steckdose, Schalter und Leuchte für `elec.house.*`, Herd- und
Waschmaschinenanschluss (`sw.house.appliance`, `.washer`) für `bs.elec.appliance`, `bs.elec.appliance.washer`, die
deshalb auch auf „cooker connection“, „Herdanschluss“, „washing machine connection“ und „Waschmaschinenanschluss“
hören. Wie die Raumgeräte trägt die Geräteanschlussdose `room` (**Raum …**), `feed` (**Stromkreis …**, das speisende
Schutzorgan, z. B. „-F3“) und `phase`, sodass Hausinstallationsliste, Stromkreisplan und der Betriebsstrom I_B ihres
Leitungsschutzschalters sie einschließen. Die RLT-Komponenten des Verfahrensmoduls (`process.hvac.*`, benannt „… (RLT)“) werden nicht mit den
Luftleitungsteilen hier zusammengelegt: sie sind Abschnitte eines RLT-Schemas, keine Teile einer Luftleitung im
Gebäudeplan.

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
   angegeben. Sind Wände in der Auswahl, ist es stattdessen deren Bodenlinie (der unterste Punkt der Wandmittellinien,
   `ObjectType.ElevationGround`), damit die Installation auf dem Boden der Wandansicht steht. Sie wird an jedem Element
   gespeichert (`Meta["bs.floorY"]`), damit spätere Wechsel dieselbe Linie nutzen.
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
8. **Wände, Türen und Fenster wechseln mit** (25.11.2026): `section` und `elevation` der Architektur sind Aliase
   (`ViewAliases` im Core). Sind Wände und Installationen ausgewählt, bietet die Mehrfachauswahl „Alle in Ansicht:
   Schnitt“ an; die Installationen gehen in `section`, Wände, Türen und Fenster in `elevation` und gemeinsam zurück in
   den Grundriss. Die Wandpunkte bleiben, die Wand behält ihre eigene `height` (Wandhöhe) – die Installationsregel liest
   `height` (Montagehöhe) nur bei Installationstypen, ein eigener Schlüssel ist daher nicht nötig.

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
| `bs.valve.ball` | Ball valve (heating) / Kugelhahn (Heizung) | 3x3 (Symbol) | in der Leitung | in, out | `service` Heizung/Trinkwasser |
| `bs.valve.check` | Check valve (heating) / Rückschlagventil (Heizung) | 3x3 (Symbol) | in der Leitung | in, out (Durchfluss von in nach out, Austrittsdreieck gefüllt) | `service` Heizung/Trinkwasser |
| `bs.valve.safety` | Safety valve (heating) / Sicherheitsventil (Heizung) | 3x3 (Symbol) | in der Leitung | in (unten), waste (Abblasen, rechts) | `service` Heizung/Trinkwasser |
| `bs.strainer` | Strainer (heating) / Schmutzfänger (Heizung) | 3x3 (Symbol) | in der Leitung | in, out | `service` Heizung/Trinkwasser |
| `bs.meter.heat` | Heat meter / Wärmemengenzähler | 4x3 (Symbol) | in der Leitung | in, out | Beschriftung HM/WMZ |
| `bs.meter.water` | Water meter / Wasserzähler | 3x3 (Symbol) | in der Leitung | in, out (Trinkwasser) | Beschriftung WM/WZ |
| `bs.heat.buffer` | Buffer tank / Pufferspeicher | 8x8 / 8x18 / 8x12 | Boden | f1, r1 (Erzeugerseite, links), f2, r2 (Verbraucherseite, rechts) | Beschriftung BUF/PS |
| `bs.heat.solarthermal` | Solar thermal collector / Solarkollektor (Solarthermie) | 20x8 / 20x9 / 20x7 | Boden | flow, return | – |
| `bs.san.wc` | WC / WC | 3,6x5,4 / 3,6x4 / 3,6x3,6 | Boden | waste, cold | – |
| `bs.san.basin` | Washbasin / Waschbecken | 6x4,5 / 6x4 / 6x3,5 | Wand | waste, hot, cold | – |
| `bs.san.shower` | Shower / Dusche | 9x9 / 9x22 / 9x8 | Boden | waste, hot, cold | – |
| `bs.san.bathtub` | Bathtub / Badewanne | 17x7,5 / 17x6 / 17x4 | Boden | waste, hot, cold | – |
| `bs.san.heater` | Water heater (storage cylinder) / Warmwasserspeicher | 6x6 / 6x16 / 6x10 | Boden | hot, circulation, cold (+ flow, return mit Heizwendel) | `coil` (true) |
| `bs.san.drain` | Floor drain / Bodenablauf | 2x2 / 2x1,5 / 2x2 (Symbol) | im Boden | waste | – |
| `bs.san.riser` | Riser (vertical pipe) / Steigleitung / Fallleitung | 2x2 / 2x28 / 2x28 | Boden | Grundriss: riser; Schnitt/Schema: bottom, top + Schiene | `medium`, `dir` steigend/fallend/durchgehend |
| `bs.vent.terminal` | Air terminal (valve / grille) / Luftdurchlass (Ventil / Gitter) | 3x3 / 3x1,6 / 3x3 (Symbol) | Decke | duct | `kind` Zuluft/Abluft |
| `bs.vent.unit` | Ventilation unit with heat recovery / Lüftungsgerät mit Wärmerückgewinnung | 7x4,5 / 7x8 / 7x5 | Wand | outdoor, exhaust, extract, supply | – |
| `bs.vent.damper` | Duct damper / Lüftungsklappe | 4x2,4 / 4x2,4 / 4x3 (Symbol) | in der Leitung | inlet, outlet (auf der Kanalachse) | `kind` Zuluft/Abluft/Außenluft/Fortluft (Farbe) |
| `bs.vent.firedamper` | Fire damper / Brandschutzklappe | 4x2,4 / 4x2,4 / 4x4 (Symbol) | in der Leitung | inlet, outlet | `kind`; Beschriftung FD/BSK |
| `bs.vent.silencer` | Duct silencer (sound attenuator) / Schalldämpfer (Lüftung) | 6x2,4 / 6x2,4 / 6x3 (Symbol) | in der Leitung | inlet, outlet | `kind` |
| `bs.cool.split` | Split air conditioner / Split-Klimagerät | 18x8 / 18x22 / 18x8 | Boden | waste (Kondensat der Inneneinheit), cable (Außeneinheit) | – |
| `bs.elec.data` | Communication outlet / Kommunikationsanschluss | 4x4 (Symbol) | Wand | cable | `kind` Daten/Telefon/TV |
| `bs.elec.detector` | Detector / Melder | 4x4 / 4x1,4 / 4x4 (Symbol) | Decke | cable | `kind` Rauch/Wärme/Bewegung |
| `bs.elec.appliance` | Appliance outlet / Geräteanschlussdose | 4x4 (Symbol) | Wand | cable | `kind` Herd/Waschmaschine/Trockner/Geschirrspüler/allgemein; `room`, `feed`, `phase` (vom Stromkreis / L1–L3 / dreiphasig); Verbraucherwerte `power`, `current`, `voltage`, `powerFactor` |
| `bs.elec.bonding` | Equipotential bonding bar / Potentialausgleichsschiene | 6x2 / 6x2 / 6x3 (Symbol) | Wand | earth + Schiene entlang der Schiene | – |

Jeder Box-Typ hat außerdem `height` und den erweiterten Parameter `scale`. Beschriftungen in Zeichnungen folgen der
UI-Sprache (HP/WP, DHW/WW, Kürzel F/VL, R/RL, CW/KW, HW/WW, HWC/Z, W/SW, SUP/ZUL, ETA/ABL, ODA/AUL, EHA/FOL,
LAN/TEL/TAE/TV/SAT, S/RM, H/WM, PIR/BM, CK/HE, HM/WMZ, WM/WZ, BUF/PS, FD/BSK, …). Mischventil (3-Wege) und
Ausdehnungsgefäß gibt es bereits als `bs.heat.mixer` und `bs.heat.expansion`. Das Split-Klimagerät ist ein Element:
Wand-Inneneinheit (im Schema mit Schneeflocke) und Außeneinheit (Ventilator), verbunden durch die im Element gezeichneten
Kältemittelleitungen; im Grundriss liegt die Inneneinheit raumseitig (unten), die Außeneinheit außen (oben rechts), das
Element wird also über die Wand gelegt. Rohrfarben wie im Architekturmodul (Vorlauf rot, Rücklauf blau
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
`bs.elec.appliance.washer`, `bs.elec.appliance.dishwasher` sowie die Trinkwasserarmaturen (Kategorie Sanitär,
`service` = water) `bs.valve.ball.water`, `bs.valve.check.water` (Rückflussverhinderer), `bs.strainer.water` (Filter).

Kategorien: `bs.heating` Heating / Heizung, `bs.plumbing` Plumbing & sanitary / Sanitär & Trinkwasser,
`bs.ventilation` Ventilation / Lüftung, `bs.cooling` Cooling & air conditioning / Kälte & Klima, `bs.electrical` Electrical installation (additions) / Elektroinstallation
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

**Weiterbauen mit „+“ (Anschlüsse).** Die Anschluss-IDs nennen das Medium: `flow`/`f1…` eine Vorlaufleitung,
`return`/`r1…` eine Rücklaufleitung, `cold`, `hot`, `circulation`, `waste` die passende Wasserleitung, Steigleitungen die
Leitung ihres Mediums, Luftdurchlässe die Luftleitung ihrer Luftart, das Lüftungsgerät Außen-, Fort-, Ab- und
Zuluftleitungen; Inline-Teile der Heizung (Pumpe, Mischventil, Thermostatventil, Ausdehnungsgefäß, Wärmemengenzähler)
passen an Vor- und Rücklauf; Kugelhahn, Rückschlagventil, Sicherheitsventil (Eintritt) und Schmutzfänger passen mit
`service` = heating (Vorgabe) an Vor- und Rücklauf und mit `service` = water an jede Trinkwasserleitung (`water`: kalt,
warm, Zirkulation; „+“ zeichnet eine Kaltwasserleitung), der Wasserzähler immer an letztere; das Sicherheitsventil bläst
über seinen Anschluss `waste` ab; die Kreise `f1`/`r1`/`f2`/`r2` des Pufferspeichers nehmen Vor- und Rücklaufleitungen wie
die Heizkreise des Verteilers; die Kanalteile (Lüftungsklappe, Brandschutzklappe, Schalldämpfer) haben `inlet`/`outlet`
auf der Kanalachse, passend an jede Luftleitung (`air`; „+“ zeichnet eine Luftleitung ihrer Luftart `kind`); das
Split-Klimagerät führt sein Kondensat in eine Schmutzwasserleitung und nimmt eine Zuleitung; Anschlussdosen, Melder, Geräte und die Potentialausgleichsschiene eine Installationsleitung
(`electric.installation`, die Leitungsführung des Elektromoduls). Rohr- und Luftleitungen führen ihr Medium; ihre freien
Enden bieten „+“ zum Verlängern oder zum Andocken des nächsten Heizkörpers, Sanitärobjekts oder Luftdurchlasses mit dem
passenden Anschluss.

## Kontextaktionen

Montagehöhe… (Eingabe, jedes Element), Medium/Art als Auswahl ▾ (Rohr, Luftleitung, Strang, Luftdurchlass, Kanalteile,
Dosen, Melder), Leitungssystem Heizung/Trinkwasser ▾ (Kugelhahn, Rückschlagventil, Sicherheitsventil, Schmutzfänger), Fließpfeil und Brücken (Rohr), Luftrichtungspfeil (Luftleitung), mehr/weniger Heizkreise (Verteiler),
Heizwendel (Speicher), Strangrichtung.

## Technische Werte (Calc 4)

Werte sind optional: jedes Element lässt sich weiterhin ohne sie einfügen und skalieren. Eingetragen und angezeigt werden
sie mit der Wertekarte (*Bearbeiten ▸ Werte ▸*, die Schaltfläche *Werte* in Erkunden und Ingenieur), gespeichert in
optionalen Textparametern (Ingenieurabschnitt des Inspektors); die vorhandenen Zahlenparameter `dn` (Rohr), `width`
(Luftleitung) und `spacing` (Fußbodenheizung) werden als Werte gelesen. Wasser: ρ = 1000 kg/m³ und *c* = 4,19 kJ/(kg·K)
als Vorgaben; Luft: ρ·*c*_p = 0,34 W·h/(m³·K). Kartengrößen stehen vorn. Entwurfsnotizen:
[engineering-calculations.md, Abschnitt 13](../design/engineering-calculations.md#13-umsetzungsnotizen-calc-4-fluid-prozess-gebäudetechnik-energie-25112026).

| Element | Größen (Parameter) | Eigene Beziehungen [Quelle] |
|---|---|---|
| Heizkessel | Φ (`heatOutput`), θ_VL (`flowTemp`), θ_RL (`returnTemp`), *V̇* (`flowRate`, l/h); Ingenieur ρ (`density`), *c* (`specificHeat`) | Φ = ρ·*c*·*V̇*·(θ_VL − θ_RL) [S29] |
| Wärmepumpe (Außeneinheit) | Φ, *P*_el (`powerEl`), COP (`cop`), θ_VL; Panel θ_Q (`sourceTemp`), θ_RL, *V̇*; Ingenieur ρ, *c* | Φ = COP·*P*_el [S32]; Φ = ρ·*c*·*V̇*·(θ_VL − θ_RL) [S29] |
| Heizkörper | Φ, Φ₅₀ (`phi50`), θ_VL, θ_RL; Panel *V̇*, θ_i (`roomTemp`, Vorgabe 20 °C), *n* (`exponent`, Vorgabe 1,3), Δθ_ln (`excessTemp`); Ingenieur ρ, *c* | Δθ_ln = (θ_VL − θ_RL)/ln((θ_VL − θ_i)/(θ_RL − θ_i)); Φ = Φ₅₀·(Δθ_ln/50 K)^*n* (Kennlinie nach EN 442-2 [S31]); Φ = ρ·*c*·*V̇*·(θ_VL − θ_RL); Betriebspunkt ρ·*c*·*V̇*·(θ_VL − θ_RL) = Φ₅₀·(Δθ_ln/50 K)^*n*, numerisch nach θ_RL gelöst [S31] |
| Fußbodenheizung (Heizkreis) | Φ, *A* (`area`), *q* (`heatFlux`), *V̇*; Panel θ_VL, θ_RL, *s* (`spacing`), *L* (`loopLength`); Ingenieur ρ, *c* | Φ = *q*·*A*; Φ = ρ·*c*·*V̇*·(θ_VL − θ_RL); *L* ≈ *A*/*s* [S30] |
| Umwälzpumpe | *V̇*, *H* (`head`), *P*_el (`powerEl`), η (`efficiency`); Panel *P*_hyd (`powerHyd`); Ingenieur ρ | *P*_hyd = ρ·*g*·*H*·*V̇*; *P*_el = *P*_hyd/η [S28] |
| Mischventil (3-Wege) | θ_AB (`tempAB`), θ_A (`tempA`), θ_B (`tempB`), *V̇*_AB (`flowAB`); Panel *V̇*_A (`flowA`), *V̇*_B (`flowB`) | *V̇*_AB = *V̇*_A + *V̇*_B [S1]; θ_AB = (*V̇*_A·θ_A + *V̇*_B·θ_B)/*V̇*_AB [S29] |
| Warmwasserspeicher | *V* (`volume`), Δθ (`deltaT`), *Q* (`heat`), *P* (`power`); Panel *t* (`heatUpTime`); Ingenieur ρ, *c* | *Q* = ρ·*V*·*c*·Δθ; *t* = *Q*/*P* (ohne Verluste) [S29] |
| Pufferspeicher | *V* (`volume`, l), Δθ (`deltaT`, Temperaturspreizung), *Q* (`heat`, kWh); Ingenieur ρ, *c* | *Q* = ρ·*V*·*c*·Δθ [S29] |
| Wärmemengenzähler | Φ (`heatOutput`, Wärmeleistung), *V̇* (`flowRate`), θ_VL (`flowTemp`), θ_RL (`returnTemp`); Ingenieur ρ, *c* | Φ = ρ·*c*·*V̇*·(θ_VL − θ_RL) [S29] |
| Wasserzähler | *V̇* (`flowRate`, m³/h) | – (der Volumenstrom seines Strangs) |
| Luftdurchlass | *V̇* (`flowRate`, m³/h), *A* (`roomArea`), *n* (`airChange`, je Stunde), *V* (`roomVolume`); Panel *h* (`roomHeight`, Vorgabe 2,5 m) | *V* = *A*·*h*; *V̇* = *n*·*V*/1 h [S30] |
| Lüftungsgerät mit Wärmerückgewinnung | *V̇*_ZUL (`flowSupply`), η_t (`recovery`), θ_ZUL (`tempSupply`), Φ_WRG (`heatRecovered`); Panel *V̇*_ABL (`flowExtract`), θ_AUL (`tempOutdoor`), θ_ABL (`tempExtract`) | θ_ZUL = θ_AUL + η_t·(θ_ABL − θ_AUL); Φ = 0,34 W·h/(m³·K)·*V̇*_ZUL·(θ_ZUL − θ_AUL) [S30] |
| Rohr | *V̇* (`flowRate`), *d*_i (`innerDiameter`), *v* (`velocity`), θ (`temperature`); Panel DN (`dn`) | *v* = 4·*V̇*/(π·*d*_i²) [S27]; *d*_i ≈ DN, wenn kein Innendurchmesser eingetragen ist (50 % Toleranz, DN ist eine Nennweite) [S30] |
| Luftleitung | *V̇*, *d* (`width`, Durchmesser oder Breite), *v*, *A* (`crossSection`); Panel *b* (`ductHeight`), *d*_h (`hydraulicDiameter`) | *v* = *V̇*/*A*; rund: *A* = π·*d*²/4, *d*_h = *d*; mit einer Höhe *b* ist der Kanal rechteckig: *A* = *d*·*b*, *d*_h = 2·*d*·*b*/(*d* + *b*) [S27, S30] |
| Geräteanschlussdose (elektrischer Verbraucher) | *P* (`power`), *I* (`current`, je Außenleiter); Panel *U* (`voltage`, Außenleiter–Neutralleiter, Vorgabe 230 V), cos φ (`powerFactor`, Vorgabe 1) | einphasig *I* = *P*/(*U*·cos φ); mit `phase` dreiphasig *I* = *P*/(3·*U*·cos φ) [S7] |

Die Luftwechselzahl *n* ist eine reine Zahl je Stunde („1“ für 1 h⁻¹): Core hat noch keine Einheit h⁻¹. Die Raumwerte
sitzen am Luftdurchlass, der den Raum versorgt; eine Verknüpfung mit einem Architekturraum (Fläche, Höhe) ist offen.

**Werte über Anschlüsse.** Rohre und Luftleitungen verbinden Anschlüsse zu Netzen. Der Volumenstrom wird auf einem
unverzweigten Strang weitergegeben; an einer Verzweigung erhält der Summenanschluss die Summe der Teile: Vor- und
Rücklaufanschluss des Wärmeerzeugers, der Austritt der Umwälzpumpe, der AB-Anschluss des Mischers und die Stutzen des
Lüftungsgeräts sind Summen; Heizkörper, Fußbodenheizkreise, der Eintritt der Umwälzpumpe, die Anschlüsse A und B des
Mischers und Luftdurchlässe sind Teile. Die Stofftemperatur wird auf einem unverzweigten Strang weitergegeben und vom
einen speisenden Austritt (Vorlaufanschluss des Wärmeerzeugers, Rücklaufanschluss eines Verbrauchers, AB-Anschluss des
Mischers) an jeden Eintritt einer Verzweigung. Thermostatventile, Kugelhähne, Rückschlagventile und Schmutzfänger geben
beides weiter; Umwälzpumpen, Wärmemengen- und Wasserzähler die Temperatur, ihr Eintritt (Teil) und Austritt (Summe)
tragen einen eigenen Volumenstrom, sodass ein Zähler den Volumenstrom seines Strangs zeigt; Lüftungsklappen,
Brandschutzklappen und Schalldämpfer geben den Luftvolumenstrom weiter. Der Pufferspeicher entkoppelt seine Kreise
(keine Werte an seinen Anschlüssen).
Wärmeverluste der Rohre werden nicht abgebildet; Mischung an einer Zusammenführung rechnet nur das Mischventil.

Beispiele (Abnahmekriterien): ein Heizkessel mit 55 °C speist über ein Rohr mit 50 l/h einen Heizkörper (Φ₅₀ = 1000 W,
*n* = 1,3, Raum 20 °C): die Rücklauftemperatur ergibt sich aus dem Betriebspunkt (θ_RL ≈ 46,0 °C, Φ ≈ 521 W), Rücklaufrohr
und Kessel übernehmen θ_RL. Ein Raum mit 20 m² (2,5 m hoch) und *n* = 1 h⁻¹ ergibt 50 m³/h am Luftdurchlass, in der
Luftleitung und am Zuluftstutzen des Geräts; eine Leitung mit 80 mm hat dann 2,76 m/s.

**Prüfungen** (nur Hinweise, Overlay, nichts wird geändert):

| Id | Regel | Schwere | Quelle |
|---|---|---|---|
| `heat.velocity` | Heizungsrohr (Vor-/Rücklauf) außerhalb 0,3–1,0 m/s; Wasserrohr über 2 m/s (Lehrbuchwerte) | Hinweis | [S30] |
| `air.velocity` | Luftgeschwindigkeit außerhalb 2–4 m/s (Wohnbau, Lehrbuch) | Hinweis | [S30] |
| `heat.cop.carnot` | Leistungszahl der Wärmepumpe > *T*_VL/(*T*_VL − *T*_Q) in Kelvin (unmöglich) | Warnung | [S29] |
| `heat.balance` | eingetragene Erzeugerleistung unter der Summe Φ der Heizkörper und Fußbodenheizkreise, die über Heizungsrohre mit ihm verbunden sind | Hinweis | [S29] |

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
Rückweg in den Grundriss, Höhe aus Verschieben im Schnitt, Drehung) und der JSON-Rundlauf; für Armaturen,
Schmutzfänger, Zähler, Pufferspeicher, Solarkollektor, Kanalteile und Split-Gerät (`FittingTests`) die Anschlussmedien,
das Andocken an ein freies Ende der vorhandenen Rohre und Luftleitungen jedes passenden Mediums, „+“ an jedem Anschluss,
die Auswahl des Leitungssystems und die Werte von Pufferspeicher und Zählern in einem Strang. Der Galerietest schreibt
`gallery/*.html` (jeder Typ in seinen drei Ansichten, gestreckt, Vorlagen und ein Beispielbad in Grundriss, Schnitt
und Schema).
