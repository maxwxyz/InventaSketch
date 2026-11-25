# Modul Energiesysteme (`energy`)

Photovoltaik, Wind, Stromaggregate, Wechselrichter und Stromrichter, Batteriespeicher, E-Mobilität, Sektorkopplung
(Wärmepumpe, BHKW, Wärmespeicher, Wasserstoff) und der Netzanschluss mit Elementen für Einlinienschaltbilder über alle
Spannungsebenen – für Folien und schnelle technische Skizzen von Energiesystemen. Jedes Element hat **zwei Ansichten
desselben Elements**:

| View-Id | Bezeichnung (EN / DE) | Icon | Inhalt |
|---|---|---|---|
| `pictorial` | Pictorial (icon) / Bildhaft (Piktogramm) | `IconIds.Front` | bildhafte Vorderansicht für Folien (Module, Anlage, Schränke, Gebäude) |
| `single-line` | Single-line diagram / Einlinienschaltbild | `IconIds.Symbol` | Symbol im Einlinienschaltbild (eine Linie steht für alle Leiter) |

Geräte starten in `pictorial`, Schaltanlagenelemente (Sammelschiene, Leistungsschalter, Trennschalter,
Erdungsschalter) in `single-line`. Zu jedem Box-Typ gibt es eine Vorlage in der jeweils anderen Ansicht
(`<type>.sld` bzw. `<type>.pictorial`); so lässt sich ein ganzes Schaltbild in beiden Darstellungen zeichnen, und eine
Auswahl kann die Ansicht wechseln: Anschlüsse behalten ihre Ids, angedockte Kabel folgen (`Attachments.Refresh`).

Die Symbole sind eigenständig nach üblicher Praxis für Einlinienschaltbilder gezeichnet (Generatorkreis mit „G ~“,
überlappende Wicklungskreise, Stromrichterquadrat mit Diagonale und AC/DC-Zeichen, Schaltmesser, Lastpfeil). Es werden
keine Normsymboltabellen oder -texte wiedergegeben und keine Konformität mit IEC 60617 oder einer anderen Norm
behauptet.

## Verhältnis zum Elektromodul

| Thema | Modul | Hinweis |
|---|---|---|
| Schaltzeichen (Widerstand, Kondensator, Diode, Transistor, Sicherung, Schütz, Spulen), Leiterplatte | electrical | nicht dupliziert |
| Leistungselektronik **als Schaltungsblock** mit U/V/W-Pins (`elec.pe.inverter`, `elec.pe.rectifier`, `elec.pe.dcdc`), Batteriezellen (`elec.battery`), Transformator als Spulen (`elec.transformer`), `elec.generator`, `elec.motor` | electrical | Schaltungsebene: Pins je Leiter |
| Hausinstallation (`elec.house.*`) und die einfachen NS-Verteilungselemente `elec.sld.busbar`, `elec.sld.feeder`, `elec.sld.meter` | electrical | für einen Hausverteiler mit Elektro-Leitungen |
| Geräte **als Systemelemente** mit Bemessungsdaten (kWp, kW, kWh, kVA, kV), bildhafte Ansicht für Folien, spannungsebenenbewusste Einlinienelemente von 0,4 kV bis 380 kV, Sektorkopplung und Wasserstoff | **energy** | dieses Modul |

Faustregel: Schaltungen mit dem Elektromodul zeichnen, Energiesysteme und Netz-Einlinienschaltbilder mit diesem
Modul. Die Module hängen nicht voneinander ab; Kabel des einen docken an Anschlüssen des anderen an, weil Anschlüsse
generische `AnchorRole.Port`-Anker sind. Bewusst verbleibende Überschneidung: `energy.grid.busbar` und
`energy.grid.meter` ergänzen gegenüber `elec.sld.busbar` / `elec.sld.meter` eine bildhafte Ansicht,
Spannungsbeschriftung, Ebenenfarben, eine Andockschiene und den Zweirichtungszähler.

## Konventionen

- **Quick Sketch zuerst:** Jedes Element ist ohne Parameter einfügbar; freies (auch ungleichmäßiges) Skalieren ändert
  `Size`, nie Parameter, und jede Zeichnung bleibt in ihrer Box. Kreissymbole (Generatoren, Transformatorwicklungen,
  E-Fahrzeug) behalten bei ungleichmäßiger Skalierung ihre Form; nur die Zuleitungen werden gestreckt.
- **Anschlusskonvention:** Der netzseitige Anschluss liegt an der **Oberkante**, die Geräteseite an der **Unterkante**,
  weitere Medien (Wärme, Wasserstoff, Wasser, Brennstoff, Batterie des Hybridwechselrichters) an der **linken/rechten
  Kante**. Ein Schaltbild liest sich damit vom Netz aus von oben nach unten (klassischer Aufbau). Ausnahmen: Die
  Netzeinspeisung hat ihren Verknüpfungspunkt unten, die Windenergieanlage ihr Kabel am Turmfuß (unten), jeweils in
  beiden Ansichten. Die Anschluss-Ids sind in beiden Ansichten gleich.
- **Gerade Kabel bleiben gerade:** Ein Ansichtswechsel behält den Boxmittelpunkt (nur die Größe ändert sich); daher
  behält jeder Anschluss an Ober- oder Unterkante seinen x-Abstand zur Mitte und jeder seitliche Anschluss seinen
  y-Abstand, in beiden Ansichten. Anschlüsse oben und unten liegen bei x = 0 (Einliniensymbole sind zentriert, der
  Beschriftungsbereich liegt rechts) – außer beim Elektrofahrzeug (Ladeanschluss über dem Heck, x = −15 in beiden
  Ansichten) und beim Dreiwicklungstransformator (mv/lv bei ±2,8). Ein senkrechtes Kabel von einem Abgang der
  Sammelschiene zu einem Gerät bleibt damit ein senkrechtes Segment, wenn das ganze Schaltbild zwischen bildhaft und
  Einlinienschaltbild wechselt (getestet).
- **Beschriftung:** Bezeichnung (`name`) und Bemessungszeilen (z. B. `630 kVA`) stehen in einem reservierten
  Beschriftungsbereich innerhalb der Box; `showLabel` blendet sie aus. Zahlen folgen der Oberflächensprache
  (Dezimalkomma im Deutschen: `0,4 kV`) mit gut lesbaren SI-Vorsätzen (`430 Wp`, `2,58 kWp`, `40 MVA`, `2 MWh`).
  Wechselspannungen werden in kV, Gleichspannungen unter 1 kV in Volt angezeigt (`800 V DC`). Beschriftungen bleiben
  beim Drehen aufrecht.
- **Spannungsebenen und Farben** (Zeichenkonvention des Moduls, keine Norm): Niederspannung ≤ 1 kV blau `#1565c0`,
  Mittelspannung ≤ 52 kV grün `#2e7d32`, Hochspannung ≤ 150 kV orange `#ef6c00`, Höchstspannung rot `#c62828`,
  Gleichstrom violett `#6a1b9a`. Kabel, Sammelschienen und Transformatorwicklungen nutzen sie, solange `levelColors`
  an ist (Standard); aus ergibt schwarze Linien für gedruckte Schaltbilder. Die Schwarzweiß-Einstellung des Dokuments
  gilt zusätzlich.
- **Energieträger** (Leitungen, Energieflüsse; Modulkonvention): Strom bernsteingelb, Wasserstoff petrol (`H₂`,
  strichpunktiert im Einlinienschaltbild), Erdgas braun (`CH₄`, lang gestrichelt), Wärme rot, Kälte blau
  (gestrichelt), Wasser hellblau (`H₂O`).

## Objekttypen

Größen sind die Entwurfsgrößen bildhaft / Einlinienschaltbild (B x H, mm auf der Seite). Jeder Box-Typ hat zusätzlich
`name` (Bezeichnung) und `showLabel` (true).

| Id | Name (EN / DE) | Größen | Anschlüsse | Parameter |
|---|---|---|---|---|
| `energy.pv.module` | PV module / PV-Modul | 20x34 / 40x22 | dc | `power` (430 Wp) |
| `energy.pv.string` | PV string / PV-String | 50x26 / 40x22 | dc | `modules` (6, 2–24; Aktionen mehr/weniger), `power` je Modul (430 Wp) |
| `energy.pv.array` | PV array / PV-Generator (Feld) | 50x34 / 40x22 | dc | `power` (10 kWp), `rows` (2, 1–6), `columns` (5, 1–12) |
| `energy.wind.turbine` | Wind turbine / Windenergieanlage | 30x44 / 38x24 | ac (unten) | `power` (3 MW), `rotor` (0°; Aktion „Rotor drehen“, Animationsbilder) |
| `energy.gen.genset` | Generator set (genset) / Stromaggregat | 36x24 / 38x24 | ac | `power` (100 kVA), `fuel` diesel/gas/biogas/h2 |
| `energy.conv.inverter` | Inverter / Wechselrichter | 24x30 / 38x24 | ac, dc (+ bat bei Hybrid) | `power` (10 kW), `phases` 3/1, `kind` pv/hybrid/battery |
| `energy.conv.converter` | Power converter / Stromrichter | 24x30 / 38x24 | a, b | `power` (10 kW), `kind` acdc (Gleichrichter)/dcdc/acac (Frequenzumrichter) |
| `energy.storage.battery` | Battery storage / Batteriespeicher | 24x30 / 36x24 | dc | `capacity` (10 kWh), `soc` (75 %, Füllstand), `form` wall/cabinet/container |
| `energy.heat.heatpump` | Heat pump / Wärmepumpe | 34x24 / 38x24 | el, heat | `power` (8 kW), `source` air/ground/water; HP/WP nach Oberflächensprache |
| `energy.heat.chp` | CHP unit / Blockheizkraftwerk (BHKW) | 40x24 / 38x28 | el, fuel, heat | `power` (50 kWel), `thermal` (80 kWth), `fuel` gas/biogas/h2 |
| `energy.heat.storage` | Thermal storage tank / Wärmespeicher | 24x32 / 28x26 | charge, discharge | `volume` (1000 l; ab 10 000 l in m³) |
| `energy.h2.electrolyser` | Electrolyser / Elektrolyseur | 36x26 / 38x26 | el, water, h2 | `power` (1000 kW), `tech` pem/alkaline/soec |
| `energy.h2.fuelcell` | Fuel cell / Brennstoffzelle | 36x26 / 38x26 | el, h2, heat | `power` (100 kW) |
| `energy.h2.tank` | Hydrogen tank / Wasserstofftank | 36x22 / 34x24 | h2 | `mass` (500 kg), `pressure` (350 bar) |
| `energy.mobility.charger` | EV charger / Ladestation | 22x30 / 38x24 | ac, ev | `power` (11 kW), `form` wallbox/pillar, `dc` (false) |
| `energy.mobility.ev` | Electric vehicle / Elektrofahrzeug | 40x20 / 40x24 | charge | `battery` (60 kWh) |
| `energy.load.building` | Consumer (load) / Verbraucher (Last) | 28x30 / 30x24 | ac | `kind` house/apartment/commercial/industry, `power` (10 kW) |
| `energy.control.ems` | Energy management system / Energiemanagementsystem | 26x22 / 38x16 | data (unten, gestrichelt) | – |
| `energy.grid.infeed` | Utility grid (infeed) / Netzeinspeisung (öffentliches Netz) | 30x34 / 38x18 | poc (unten) | `voltage` (20 kV); Titel Grid/Netz, solange keine Bezeichnung gesetzt ist |
| `energy.grid.transformer` | Transformer / Transformator | 30x34 / 38x26 | hv, lv | `hv` (20 kV), `lv` (0,4 kV), `power` (630 kVA), `vector` (Schaltgruppe als Text, leer), `levelColors` |
| `energy.grid.transformer3` | Three-winding transformer / Dreiwicklungstransformator | 30x34 / 38x28 | hv, mv, lv | `hv` (110 kV), `mv` (20 kV), `lv` (10 kV), `power` (40 MVA), `vector`, `levelColors` |
| `energy.grid.substation` | Compact substation / Ortsnetzstation (Kompaktstation) | 34x30 / 40x30 | mv, lv | `hv` (MS-Seite, 20 kV), `lv` (0,4 kV), `power` (630 kVA), `levelColors` |
| `energy.grid.meter` | Energy meter / Energiezähler | 20x28 / 30x24 | a, b | `bidirectional` (true; Pfeile Bezug/Einspeisung), `unit` (kWh) |
| `energy.grid.busbar` | Busbar / Sammelschiene | 60x10 / 60x8 | t1…tn auf der Schiene + Schiene `bar0` | `voltage` (0,4 kV), `dc`, `taps` (5, 2–12), `levelColors`; die Breite bleibt beim Ansichtswechsel erhalten |
| `energy.grid.breaker` | Circuit breaker / Leistungsschalter | 16x24 / 26x24 | a, b | `closed` (true), `symbol` contact (Schalter mit Leistungsschalterkreuz) / square (gefüllt = geschlossen) |
| `energy.grid.disconnector` | Disconnector / Trennschalter | 16x24 / 26x24 | a, b | `closed` (true), `kind` disconnector / loadbreak (Lasttrennschalter) / fuse (Sicherungslasttrennschalter) |
| `energy.grid.earthing` | Earthing switch / Erdungsschalter | 16x24 / 26x24 | a | `closed` (false) |
| `energy.line.cable` | Cable / line / Kabel / Leitung | Punkte | Schienen `rail0…` | `voltage` (0,4 kV), `dc`, `levelColors`, `kind` power/data (gestrichelt), `conductors` (0–5 Striche), `label`, `showVoltage`, `route` free/orthogonal |
| `energy.line.pipe` | Energy carrier pipe / Rohrleitung (Energieträger) | Punkte | Schienen | `carrier` hydrogen/gas/heat/cooling/water, `label`, `showCode` |
| `energy.line.flow` | Energy flow arrow / Energiefluss-Pfeil | Punkte | Schienen | `carrier` electricity/hydrogen/gas/heat/cooling/water, `weight` (Bandbreite 2,5 mm), `label` (z. B. „12 MWh/a“) |

## Bibliothek

Ein Eintrag je Typ (Id = Typ-Id), eine Vorlage jedes Box-Typs in seiner anderen Ansicht (`<type>.sld` /
`<type>.pictorial`) und Varianten: `energy.pv.string.10`, `energy.pv.array.large` (100 kWp), `energy.pv.park`
(10 MWp), `energy.wind.offshore` (15 MW), `energy.gen.genset.gas`, `energy.conv.inverter.hybrid`,
`energy.conv.inverter.battery`, `energy.conv.inverter.central` (2,5 MW), `energy.conv.converter.dcdc`,
`energy.conv.converter.acac`, `energy.storage.battery.cabinet` (100 kWh), `energy.storage.battery.container` (2 MWh
BESS), `energy.heat.heatpump.ground`, `energy.heat.chp.biogas`, `energy.h2.electrolyser.alkaline` (10 MW),
`energy.mobility.charger.hpc` (150 kW DC-Säule), `energy.load.apartment`, `energy.load.commercial`,
`energy.load.industry`, `energy.grid.infeed.lv` (Hausanschluss 0,4 kV), `energy.grid.infeed.hv` (110 kV),
`energy.grid.transformer.hvmv` (110/20 kV, 40 MVA), `energy.grid.transformer.ehv` (380/110 kV, 300 MVA),
`energy.grid.busbar.mv` / `.hv` / `.dc` (20 kV, 110 kV, 800 V DC), `energy.grid.breaker.square`,
`energy.grid.disconnector.loadbreak`, `energy.grid.disconnector.fuse`, Kabel `energy.line.cable.lv` / `.mv` / `.hv` /
`.ehv` / `.dc` / `.data`, Leitungen `energy.line.pipe.<carrier>` und Energieflüsse `energy.line.flow.<carrier>`.

Suchbegriffe decken beide Sprachen ab, z. B. Photovoltaik, Wechselrichter, Speicher, Ladestation, Wallbox,
Netzanschluss, Hausanschluss, Elektrolyseur, Brennstoffzelle, Wasserstoff, BHKW, Wärmepumpe, Trafo, Sammelschiene,
Leistungsschalter, Trennschalter, Einlinienschaltbild.

Kategorien: `energy.generation` Generation / Erzeugung, `energy.storage` Storage & conversion / Speicher & Umrichter,
`energy.sector` Sector coupling & hydrogen / Sektorkopplung & Wasserstoff, `energy.consumers` Consumers & e-mobility /
Verbraucher & E-Mobilität, `energy.grid` Grid connection & single-line / Netzanschluss & Einlinienschaltbild,
`energy.lines` Cables, pipes & flows / Kabel, Leitungen & Energieflüsse.

## Anker und Linienlogik

- **Anschlüsse** (`AnchorRole.Port`) liegen auf der Boxkante und tragen in beiden Ansichten dieselbe Id (sie hängen nur
  dort von Parametern ab, wo sich das Gerät ändert: der Hybridwechselrichter ergänzt `bat`, die Sammelschiene hat
  `t1…tn`). Zu jedem Anschluss führt eine gezeichnete Zuleitung, ein angedocktes Kabel trifft also die Zeichnung.
- **Schienen** (`AnchorRole.Rail`): die Sammelschiene (`bar0`, über die ganze Länge – Kabel docken überall an und
  zeichnen einen Verbindungspunkt) sowie jedes gezeichnete Segment von Kabeln, Leitungen und Energieflüssen (`rail0`,
  `rail1` …) für Abzweige.
- **Linienlogik:** Kabel, Leitungen und Energieflüsse nehmen Ecken auf (`InsertablePoints`), lassen sich an beiden
  Enden verlängern, und Abzweige docken an ihren Schienen an (`Polylines.Branch`), mit Verbindungspunkt. Orthogonale
  Kabelführung verlässt ein angedocktes Ende senkrecht zur Kante seines Anschlusses.
- **Kabel:** Ebenenfarbe (oder schwarz), 0,9 mm in der bildhaften, 0,5 mm in der Einlinienansicht;
  Kommunikationsleitungen dünn und gestrichelt; optionale Leiterstriche und eine Beschriftung „Bezeichnung · Spannung“.
- **Rohrleitung:** farbiges Rohr mit Glanzlinie in der bildhaften Ansicht; dünne Linie mit Strichart und Kürzel des
  Energieträgers im Einlinienschaltbild.
- **Energiefluss-Pfeil:** Band der Breite `weight` mit Pfeilspitze (Gehrungsecken) in der bildhaften Ansicht; dünne
  Linie mit gefüllter Spitze im Einlinienschaltbild.

**Weiterbauen mit „+“ (Anschlüsse).** Jeder freie Anschluss bietet zuerst seine Leitung an (`ObjectType.Connection`,
siehe Benutzerhandbuch): `dc` und `bat` ein DC-Kabel, `ac`, `el`, `poc`, `hv`, `mv`, `lv` ein AC-Kabel mit der Spannung,
die das Element für diesen Anschluss angibt (Transformatorwicklungen, Netzeinspeisung), `data` eine
Kommunikationsleitung, `heat`, `charge`/`discharge` (Wärmespeicher) eine Wärmeleitung, `fuel` eine Gasleitung, `water`
eine Wasserleitung, `h2` eine Wasserstoffleitung; Schaltgeräte, Zähler und Sammelschienen ein Kabel (DC bei
DC-Sammelschienen); Umrichter je Seite DC oder AC nach ihrer Umformung; Ladepunkt und Fahrzeug DC beim DC-Laden. Der
Wärmeanschluss der Wärmepumpe nimmt Wärme- und Kälteleitungen. Danach folgen die Elemente mit einem Anschluss desselben
Mediums (ein PV-String nach einem PV-Modul, ein Wechselrichter oder eine Batterie nach einem DC-Kabel). Kabel führen
`electric.dc`, `electric.ac` oder `data`, Rohrleitungen ihren Energieträger; das freie Ende eines Kabels oder einer
Leitung bietet „+“ zum Verlängern oder zum Andocken des nächsten Elements. Energieflusspfeile führen kein eigenes Medium.

## Kontextaktionen

Bemessungsdaten anzeigen und Bezeichnung… (jedes Box-Element), Eingaben für Leistung / Kapazität / Volumen /
Spannung… (akzeptieren `10`, `10 kW`, `10,5`), Spannungen… an Transformatoren und Ortsnetzstation (`20/0,4`,
`110/20/10`), Art / Bauform / Brennstoff / Phasen / Wärmequelle / Technologie / Darstellung weiterschalten,
Schaltzustand umschalten (Leistungs-, Trenn-, Erdungsschalter), DC-Laden, Zweirichtungszähler, Farbe nach
Spannungsebene, Module, Reihen und Anschlusspunkte der Sammelschiene mehr/weniger, Rotor drehen (Windenergieanlage);
Kabel: Führung, Spannung, Spannung anzeigen, Leiterstriche, Kabelbezeichnung; Leitungen: Energieträger, Kürzel;
Energieflüsse: Energieträger, breiteres/schmaleres Band, Beschriftung.

## Hinweise für Hosts

Alle Ausgaben sind native Primitive (Linien, Polylinien, Polygone, Rechtecke mit abgerundeten Ecken, Ellipsen,
kubische und quadratische Pfade, Text); jeder Host zeichnet sie nativ, ein Vektorbild ist nicht nötig. Texte nutzen
die Unicode-Tiefstellungen `₂` und `₄` (H₂, CH₄, H₂O) und das Minuszeichen `−`; der Host braucht eine Schrift, die sie
enthält (die Standardschriften von Office und LibreOffice tun das). Die Windenergieanlage liefert Animationsbilder
(Rotorschritte von 120°/n) über `ObjectType.AnimationFrames`.

## Tests und Galerie

`tests/InventaSketch.Modules.Energy.Tests` (77 Tests): Registrierung und Id-Konventionen, zweisprachige Namen,
Parameter, Optionen und Aktionen, beide Ansichten mit ihren Icons, Einfügen ohne Parameter, Bibliothekseinträge mit
gültigen Parametern und Ansichten, jeder Bibliothekseintrag in jeder Ansicht in beiden Sprachen innerhalb seiner Box
(auch gestreckt, gestaucht, mit jeder Auswahl und den Grenzen jeder Zahl), jeder Parameter ändert Zeichnung oder
Anschlüsse, Skalieren ändert `Size` statt Parameter, die Ansichten zeichnen verschiedene Symbole, Größen beim
Ansichtswechsel (visueller Maßstab bleibt, Breite der Sammelschiene bleibt), Beschriftungen in beiden Sprachen und
SI-Vorsätze, Ebenenfarben, Schaltzustände, Stilüberschreibung, Anschlüsse mit gleichen Ids in beiden Ansichten auf der
Boxkante, mit Zuleitung und gleichem Abstand zur Mitte über den Ansichtswechsel, Schiene und Abgänge der Sammelschiene, ein an einem Anschluss angedocktes Kabel folgt
Verschieben und Ansichtswechsel, ein an der Sammelschiene angedocktes Kabel mit Verbindungspunkt, Linienlogik (Ecken,
Verlängern, Abzweige), orthogonale Führung, Leiterstriche und Beschriftung, Kürzel der Rohrleitungen, das Band des
Energieflusses, Zahlenformate und eingegebene Spannungen. Der Galerietest schreibt `gallery/*.html` (jeder Typ in
beiden Ansichten, gestreckt und beschriftet, alle Vorlagen in beiden Ansichten sowie zwei Beispielsysteme – ein
Heim-Energiesystem und ein MS/NS-Einlinienschaltbild – jeweils bildhaft und als Einlinienschaltbild); weitere
Tests prüfen, dass jedes Kabelende der Beispiele in beiden Ansichten auf seinem Anschluss bleibt und jedes gerade
Kabel nach dem Ansichtswechsel des ganzen Schaltbilds ein senkrechtes Segment bleibt.
