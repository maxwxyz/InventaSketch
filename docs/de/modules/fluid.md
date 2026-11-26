# Modul Fluidtechnik (`fluid`)

Bibliothek: `src/InventaSketch.Modules.Fluid` · Tests: `tests/InventaSketch.Modules.Fluid.Tests` ·
Englische Fassung (maßgeblich): [../../en/modules/fluid.md](../../en/modules/fluid.md)

Das Modul liefert Schaltzeichen für Hydraulik- und Pneumatikschaltpläne im Sinne der Konventionen von ISO 1219-1
(grafische Symbole der Fluidtechnik) und des aus ISO-1219-2-Schaltplänen bekannten Kennzeichnungsschemas (z. B. `1V1`,
`0P1`). Alle Symbole sind **eigenständig gezeichnet**; es werden keine Normtexte, Tabellen oder Symbolgrafiken
übernommen, und es wird **keine Normkonformität behauptet**.

Es ist ein **Skizzier**-Modul für Folien, technische Abbildungen und Konzeptschaltungen. Es simuliert keine Schaltungen;
die optionalen technischen Werte (unten) liefern erste Abschätzungen und Hinweise, keine Auslegung nach einer Norm.

## Konventionen

- **Schnellskizze zuerst:** Jedes Bibliothekselement wird ohne Parameter eingefügt. Freies (gleichmäßiges oder
  ungleichmäßiges) Skalieren ändert nur `Size`; Symbole sind für eine Entwurfsbox gezeichnet und werden auf die
  Objektbox gestreckt. Kleine Details (Dreiecke, Pfeilspitzen, Punkte, Text) behalten ihre Proportionen. Ändert ein
  Parameter die Entwurfsbox (Schaltstellungen, Betätigungen) – per Aktion oder im Inspektor (`ObjectType.OnParamChanged`)
  –, folgt `Size`, sodass der Zeichnungsmaßstab bleibt.
- **Hydraulik oder Pneumatik** ist der optionale Parameter `medium` (`hydraulic` / `pneumatic`) bei den Typen, deren
  Zeichnung sich unterscheidet: Pumpen/Verdichter, Motoren, Schwenkantrieb, Druckquellen, Wegeventile und
  Betätigungssymbole (volle Dreiecke = Hydraulik, hohle = Pneumatik). Die Kontextauswahl `fluid.medium` (Hydraulik / Pneumatik) legt es fest.
- **Linienstärken:** Symbole und Arbeitsleitungen 0,35 mm, Kolben und Stopfen 0,7 mm, Federn und Details 0,25 mm,
  Steuerleitungen lang gestrichelt, Leckölleitungen kurz gestrichelt, Umrahmungen strichpunktiert. Es werden nur
  schwarze Linien und schwarze oder keine Füllungen verwendet; die Schwarzweißausgabe bleibt unverändert.
- **Kennzeichen:** optional `ref` (z. B. `1V1`) mit `showLabel`. Es steht außerhalb der Box auf der ersten Seite ohne
  Anschlüsse, geprüft in den Seitenrichtungen links, oben, rechts, unten, und bleibt beim Drehen aufrecht.
- **Anschlussnamen:** `showPorts` zeigt die Anschlussnamen neben den Anschlüssen (außerhalb der Box). Der
  Rücklaufanschluss von 3/2-, 4/2- und 4/3-Ventilen heißt `T` (Hydraulik) bzw. `R` (Pneumatik); seine Id bleibt `T`.

## Ansichten

| Ansicht | Symbol (`IconIds`) | Verwendet von |
|---|---|---|
| `symbol` | `Symbol` | Schaltzeichen (alle Typen) |
| `detailed` | `Detailed` | Wartungseinheit (FRL): Filter, Druckregler mit Manometer, Öler in einer Umrahmung |
| `simplified` | `Simplified` | Wartungseinheit (FRL) als vereinfachtes Symbol |

Beim Umschalten der FRL-Ansicht bleibt der optische Maßstab erhalten, die Proportionen der Zielansicht werden
übernommen (`ViewDef.Size`).

## Anschlüsse und Leitungen

- Jedes anschließbare Symbol stellt seine Anschlüsse als `Anchor.Point(AnchorRole.Port, …)` mit dem Anschlussnamen als
  **stabiler Id** bereit (`P`, `T`, `A`, `B`, `R`, `S`, `X`, `Y`, `L`; Verbindungsteile `1`/`2`, Kreuzung
  `W`/`E`/`N`/`S`).
- Bei Standardgröße und rasterausgerichtetem Einfügepunkt liegen alle Anschlüsse im **2,5-mm-Raster** und auf dem
  Boxrand (Verbindungspunkt: Mitte). Anschlüsse werden in Entwurfskoordinaten gespeichert und mit der Box skaliert.
- `fluid.line` ist ein Punktobjekt (`kind`: Arbeits-, Steuer-, Lecköl-, Schlauchleitung; `route`: orthogonal oder
  frei). Orthogonale Leitungen verlassen angedockte Anschlüsse senkrecht zur Kante (auch bei Drehung). Der geführte
  Verlauf ist eine **Schiene** (Rail): andere Leitungsenden und Verbindungspunkte docken überall an.
- `FluidModule.ConnectToPort(registry, doc, lineId, index, tolerance)` setzt ein Leitungsende auf den nächsten Anschluss
  und speichert die Anbindung mit der **Anschluss-Id**. Die Leitung folgt beim Verschieben, Drehen und Skalieren des
  Symbols, und `Attachments.Refresh` führt sie nach, wenn sich der Anschluss im Symbol verschiebt (Ventiltyp,
  Federseite, Betätigung, Ansicht).
- Wegeventile zeichnen ihre Anschlüsse an der Box der **Grundstellung**: Mittelbox bei 3 Schaltstellungen, sonst die
  Box neben der einzigen Rückstellfeder (rechte Box, wenn keine oder beide Seiten eine Feder haben).
- Das Ventil bietet `Mount`-Anker (`left`, `right`) an seinen Enden; das separate Betätigungssymbol
  (`fluid.actuation`) dockt dort mit seinem Anker `mount` an.

**Weiterbauen mit „+“ (Anschlüsse).** Jeder freie Anschluss bietet eine Leitung an (Medium `fluid`): Steueranschlüsse
X/Y/Z von Wegeventilen, entsperrbaren Rückschlagventilen (beide Typen), dem Senkbremsventil und Vorsteuerungen eine
Steuerleitung, Leckanschlüsse L eine Leckleitung, alle anderen eine Arbeitsleitung; danach die Bauteile mit einem
passenden Anschluss. Das freie Ende einer Leitung bietet „+“ zum Verlängern oder zum Andocken des nächsten Bauteils mit
dem zugewandten Anschluss.

## Antriebswellen (ein Motor treibt die Pumpe)

Pumpen/Verdichter und Motoren haben eine echte mechanische Welle mit demselben Vertrag wie die Wellen und Antriebe des
Mechanik- und des Maschinenmoduls ([mechanical-drives.md](mechanical-drives.md)):

- `fluid.pump` hat einen `Hub`-Anker (Id `hub`) am Ende ihrer gezeichneten Welle (rechter Rand, auf der Wellenlinie).
  Sie wird angetrieben: die Wellenachse eines Antriebs (E-Maschine des Maschinenmoduls, Wellenstumpf des
  Motorsymbols, Abtriebsstumpf eines Hydromotors) oder eine mechanische Welle (`mech.shaft`) durch diesen Punkt trägt
  die Pumpe.
- `fluid.motor` ist ein Antrieb (`ObjectType.DrivesShaft`): sein Wellenstumpf ist ein `Axis`-Segment (Id `shaft`) mit
  Nabe und `End`-Marke am äußeren Ende. Die Nabe einer Pumpe oder ein Mechanikteil dockt am Stumpf an; eine vom
  Wellenende fortgesetzte mechanische Welle trägt den Motor, und Verschieben des Motors nimmt die Welle mit.
- Die Nabe hat keinen Bohrungsradius (0): Pumpe oder Motor behalten auf einer Welle ihre Größe. Angedockt wird mit dem
  normalen Fangen (Nabe auf Achse) und der Kopplung des Kerns („bewegt sich mit“: Welle oder Antrieb trägt die Pumpe). Die
  Welle der Pumpe zeigt nach rechts: zum Anbau auf der anderen Seite die Pumpe (oder den Antrieb) spiegeln.
- **Werte:** Pumpe und Motor teilen die Drehzahl *n* mit dem gekoppelten Element. Das Drehmoment *M* geht über eine
  direkte Eins-zu-eins-Kupplung (E-Maschine an der Pumpennabe, keines von beiden mit einem dritten Element mit Moment
  gekoppelt) und von der mechanischen Welle oder Kupplung, auf der sie sitzen, wenn dieser Wellenstrang höchstens zwei
  Glieder hat, die Moment aufnehmen oder abgeben (die Regel des Mechanikmoduls). Beispiel: eine E-Maschine mit
  1450 min⁻¹ treibt eine 10-cm³-Pumpe bei 100 bar: *Q* = 14,5 l/min, *M* = *V*_g·*p*/(2π) = 15,9 N·m ist das
  Lastmoment der E-Maschine, *P* = 2,42 kW.

## Kontextaktionen

| Aktion | Symbol | Wirkung |
|---|---|---|
| `view.<id>` | Ansichtssymbol | FRL ausführlich / vereinfacht |
| `fluid.medium=hydraulic/pneumatic` | `Option` | Medium ▾ (Auswahl) |
| `fluid.valve.ways=…` | `Option` | Ventiltyp ▾ 2/2, 3/2, 4/2, 4/3, 5/2, 5/3 (die Größe folgt der Entwurfsbox) |
| `fluid.valve.centre=…` | `Option` | Mittelstellung ▾, nur bei drei Stellungen (4/3: gesperrt, Umlauf, Schwimm, offen, belüftet; 5/3: gesperrt, entlüftet, belüftet); `fluid.valve.normal=closed/open`: Ruhestellung ▾ (2/2, 3/2) |
| `fluid.valve.normal` | `Toggle` | Ruhestellung gesperrt / offen (2/2, 3/2) |
| `fluid.valve.actLeft=…`, `fluid.valve.actRight=…` | `Option` | Betätigung links ▾ / rechts ▾ |
| `fluid.valve.flip` | `Swap` | Federseite tauschen (Betätigungen tauschen; die Grundstellung folgt) |
| `fluid.valve.detent`, `fluid.valve.centring` | `Toggle` | Rastung; Federzentrierung bei 3 Stellungen |
| `fluid.variable`, `fluid.flow=one/two` | `Toggle`/`Option` | Pumpen/Motoren: verstellbar, Stromrichtungen ▾ |
| `fluid.cyl.rod=…`, `fluid.cyl.cushion=…`, `fluid.cyl.spring=…` | `Option` | Kolbenstange ▾, Dämpfung ▾, Feder ▾ |
| `fluid.spring`, `fluid.piloted`, `fluid.adjustable`, `fluid.check` | `Toggle` | Optionen von Rückschlag-, Drossel- und Druckventilen |
| `fluid.pilotcheck.twin` | `Toggle` | entsperrbares Rückschlagventil: einfach / doppelt (gegenseitig entsperrt); die Größe folgt der Entwurfsbox |
| `fluid.suctioncup.bellows` | `Toggle` | Sauggreifer mit Faltenbalg |
| `fluid.line.kind=…`, `fluid.line.route=orthogonal/free` | `Option`/Führungs-Piktogramme | Leitungsart ▾; Führung ▾ |
| `fluid.ports` | `Text` | Anschlussnamen anzeigen |
| `fluid.label` | `Text` | Kennzeichen ein-/ausblenden (setzt bei Bedarf einen Vorschlag wie `1V1`) |
| `fluid.ref.set` | `Text` | Kennzeichen direkt eingeben (Eingabeaktion) |

## Objekttypen

| Kategorie | Typen (`fluid.*`) |
|---|---|
| Pumpen, Motoren & Zylinder | `pump` (Pumpe/Verdichter; `variable`, `flow`; Wellennabe), `motor` (`variable`, `flow`; Abtriebswelle als Achse), `cylinder.double` (`rod` einseitig/durchgehend, `cushion` keine/fest/einstellbar), `cylinder.single` (`spring` Rückstellung/Vorhub/keine), `rotary`, `suctioncup` (Sauggreifer; `bellows` Faltenbalg; Anschluss `V`) |
| Wegeventile | `valve.directional` (`ways` 2/2, 3/2, 4/2, 4/3, 5/2, 5/3; `normal`; `centre`; `actLeft`/`actRight`: keine, Muskelkraft, Druckknopf, Hebel, Pedal, Rolle, Feder, Elektromagnet, Druckansteuerung; `centring`; `detent`), `actuation` (separates Betätigungssymbol inkl. Rastung) |
| Sperr-, Strom- & Druckventile | `valve.check` (`spring`, `piloted`), `valve.throttle` (`adjustable`, `check` = Drosselrückschlagventil), `valve.relief`, `valve.reducing`, `valve.sequence` (Leckanschluss `L`), `valve.shuttle` (ODER), `valve.twopressure` (UND), `valve.quickexhaust`, `valve.pilotcheck` (entsperrbares Rückschlagventil mit Steuerkolben; `spring` (ein), `twin` = doppelt, gegenseitig entsperrt: Anschlüsse `P`, `A`, `X`; doppelt `A1`, `A2`, `B1`, `B2`), `valve.counterbalance` (Senkbremsventil: in Ruhe geschlossenes Druckventil vom Lastanschluss `C` zum Ventilanschluss `V`, interne Vorsteuerung von `C`, externe Vorsteuerung `X`, Bypass-Rückschlagventil für freien Durchfluss `V` → `C`; `adjustable`) |
| Versorgung, Aufbereitung & Speicher | `tank` (offen/geschlossen), `source`, `exhaust`, `silencer`, `filter`, `separator`, `lubricator`, `cooler`, `heater`, `frl` (ausführlich/vereinfacht), `accumulator` (Gas/Feder/Gewicht), `powerunit` (Hydraulikaggregat: Elektromotor und Pumpe, die aus dem Tank saugt, Druckbegrenzungsventil und Rücklauffilter in einer strichpunktierten Umrahmung; Anschlüsse `P` Druck, `T` Rücklauf), `vacuum.generator` (Vakuumerzeuger / Ejektor: Treibdüse und Diffusor, Anschlüsse `P` Versorgung, `R` Abluft, `V` Vakuum) |
| Messen & Melden | `gauge`, `pressureswitch`, `flowmeter`, `thermometer` |
| Leitungen & Verbindungen | `line` (Punktobjekt), `junction`, `crossing` (einfach oder mit Brücke), `plug`, `coupling` (gekuppelt/entkuppelt, mit Rückschlagventilen), `enclosure` (strichpunktierte Baugruppenumrahmung) |

Begleiter („Einfügen ▸“): Eine Leitung bietet Ventile, Zylinder, Manometer und Verbindungspunkte an; ein Zylinder
Wegeventile, Drosseln und Rückschlagventile; ein Wegeventil Betätigungen, Leitungen, Zylinder und Drosseln; ein
Vakuumerzeuger den Sauggreifer und umgekehrt.

Bibliothekselemente der neuen Bauteile: `fluid.pilotcheck`, `fluid.pilotcheck.double`, `fluid.counterbalance`,
`fluid.vacuum`, `fluid.suctioncup`, `fluid.suctioncup.bellows`, `fluid.powerunit` (Runde 5; das ältere `fluid.check.piloted`, ein Rückschlagventil
mit der Option `piloted`, bleibt).

## Export Verbindungsliste

`fluid.connections` (`IExportProvider`, CSV): An Anschlüsse angedockte Leitungsenden (Anbindungen mit Anschluss-Id)
verbinden Anschlüsse zu Verbindungen. Verbindungspunkte verbinden alles, was an ihnen andockt; eine Kreuzung hält
`W`–`E` und `N`–`S` getrennt; eine gekuppelte Schnellkupplung verbindet beide Seiten. Eine Zeile je Verbindung:
Nummer, verwendete Leitungsarten, Anschlüsse (`Kennzeichen.Anschluss`, sonst `Typname.Anschluss`, getrennt durch `; `).

**Erhalten:** welche Anschlüsse mit welcher Leitungsart verbunden sind, Kennzeichen der Bauteile und angezeigte
Anschlussnamen. **Nicht erhalten:** Leitungsführung, Schaltwege in Ventilen (zustandsabhängig), Leitungsenden, die eine
andere Leitung nur berühren (ohne Verbindungspunkt), Positionen.

## Persistenz

Der gesamte Zustand liegt im kanonischen Dokument (`type`, `view`, `params`, `points`, `attach`, `text`); es gibt keinen
versteckten Modulzustand. Ein JSON-Roundtrip reproduziert Dokument und gerendertes SVG exakt (getestet).

## Technische Werte (Calc 4)

Werte sind optional: jedes Symbol lässt sich weiterhin ohne sie einfügen und skalieren. Eingetragen und angezeigt werden
sie mit der Wertekarte (*Bearbeiten ▸ Werte ▸*, die Schaltfläche *Werte* in Erkunden und Ingenieur), gespeichert in
optionalen Textparametern (Ingenieurabschnitt des Inspektors). Drehzahlen sind Beträge, *P* = 2π·*n*·*M*. Kartengrößen
stehen vorn. Entwurfsnotizen:
[engineering-calculations.md, Abschnitt 13](../design/engineering-calculations.md#13-umsetzungsnotizen-calc-4-fluid-prozess-gebäudetechnik-energie-25112026).

| Element | Größen (Parameter) | Eigene Beziehungen [Quelle] |
|---|---|---|
| Pumpe / Verdichter (`fluid.pump`) | *V*_g (`displacement`, cm³), *n* (`speed`), *Q* (`flowRate`, l/min), *p* (`pressure`, bar); Panel *M* (`torque`, Antrieb), *P* (`power`, Antrieb), *P*_hyd (`powerHyd`); Ingenieur η_v (`efficiencyV`), η_hm (`efficiencyHm`), beide Vorgabe 100 % | *Q* = *V*_g·*n*·η_v; *M* = *V*_g·*p*/(2π·η_hm); *P* = 2π·*n*·*M*; *P*_hyd = *p*·*Q*; *P* = *P*_hyd/(η_v·η_hm) [S26] |
| Motor (`fluid.motor`) | wie die Pumpe; *M* und *P* sind Abtriebswerte | *n* = *Q*·η_v/*V*_g; *M* = *V*_g·*p*·η_hm/(2π); *P* = 2π·*n*·*M*; *P*_hyd = *p*·*Q*; *P* = *P*_hyd·η_v·η_hm [S26] |
| Doppeltwirkender Zylinder | *D* (`bore`), *d* (`rodDiameter`), *F*_aus (`forceExt`), *v*_aus (`speedExt`); Panel *p*, *Q*, *F*_ein (`forceRet`), *v*_ein (`speedRet`), *A*₁ (`areaExt`), *A*₂ (`areaRet`), φ (`areaRatio`), *s* (`stroke`), *t*_aus (`timeExt`), *t*_ein (`timeRet`), ΣΔ*p* (`supplyPressureDrop`) und *p*_verf (`pressureAvailable`), beide aus dem Zulaufweg berechnet (siehe unten); Ingenieur η_hm, *F*_erf (`forceReq`), *p*_max (`pmax`, verknüpft) | *A*₁ = π·*D*²/4 (durchgehende Stange: *A*₁ = *A*₂); *A*₂ = π·(*D*² − *d*²)/4; φ = *A*₁/*A*₂; *F* = *p*·*A*·η_hm und *v* = *Q*/*A* in beide Richtungen; *t* = *s*/*v* [S26] |
| Einfachwirkender Zylinder | *D*, *F* (`force`, ohne Federkraft), *v* (`speed`), *p*; Panel *Q*, *d*, *A* (`area`), *s*, *t* (`time`), ΣΔ*p*, *p*_verf; Ingenieur η_hm, *F*_erf, *p*_max | *A* = π·*D*²/4 (Federvorhub: Ringfläche); *F* = *p*·*A*·η_hm; *v* = *Q*/*A*; *t* = *s*/*v* [S26] |
| Beide Zylinder: Luftverbrauch Pneumatik (Ingenieur) | *V*_H (`sweptVolume`), *V*_L (`airPerCycle`, freie Luft je Arbeitsspiel), *Q*_n (`airFlow`, Luftverbrauch während eines Hubs) | *V*_H = (*A*₁ + *A*₂)·*s* (einfachwirkend: *A*·*s*); *V*_L = *V*_H·(*p* + *p*_amb)/*p*_amb; *Q*_n = *Q*·(*p* + *p*_amb)/*p*_amb (*p* Überdruck, *p*_amb = 1,013 bar, isotherm) [S26] |
| Schwenkantrieb | *V*_g, *p*, *M*; Ingenieur η_hm | *M* = *V*_g·*p*·η_hm/(2π) [S26] |
| Druckbegrenzungs-, Druckminder-, Zuschaltventil | *p*_E (`setPressure`) | – |
| Drossel (`fluid.valve.throttle`) | *Q* (`flowRate`), Δ*p* (`pressureDrop`), *d*₀ (`orificeDiameter`); Panel *p*₁ (`pressureIn`, an P), *p*₂ (`pressureOut`, an A), *A*₀ (`orificeArea`); Ingenieur α (`flowCoefficient`, Vorgabe 0,6), ρ (`density`, Vorgabe 870 kg/m³) | *A*₀ = π·*d*₀²/4; *Q* = α·*A*₀·√(2Δ*p*/ρ) (scharfkantige Blende, inkompressibel); Δ*p* = *p*₁ − *p*₂ [S26] |
| Wegeventil, Rückschlagventil, entsperrbares Rückschlagventil, Filter, Kühler, Heizer (Runde 6) | Δ*p* (`pressureDrop`); Panel ζ (`lossCoefficient`), Δ*p*_N (`pressureDropNominal`) bei *Q*_N (`flowNominal`, der Datenblattpunkt), *Q* (`flowRate`, von der Leitung an P, sonst A/A1); Ingenieur *v* (`velocity`, von der Leitung), ρ (`density`, von der Leitung, Vorgabe 870 kg/m³) | Δ*p* = ζ·ρ·*v*²/2 [S27]; Δ*p* = Δ*p*_N·(*Q*/*Q*_N)² (turbulente Durchströmung, Fluid des Datenblatts) [S26] |
| Hydraulikaggregat (`fluid.powerunit`) | *Q* (`flowRate`), *p* (`pressure`), *P* (`power`, Antrieb), *V*_T (`tankVolume`); Panel *P*_hyd (`powerHyd`), η_t (`efficiency`, Vorgabe 100 %), *p*_E (`setPressure`, das eingebaute Druckbegrenzungsventil) | *P*_hyd = *p*·*Q*; *P* = *P*_hyd/η_t [S26] |
| Manometer, Druckschalter, Druckquelle | *p* (aus dem Netz angezeigt) | – |
| Durchflussmesser | *Q* (aus dem Netz angezeigt) | – |
| Hydrospeicher | *V*₀ (`volume`), *p*₀ (`p0`), Δ*V* (`volumeUsable`); Panel *p*₁, *p*₂, *V*₁, *V*₂ (absolute Drücke) | *p*₀·*V*₀ = *p*₁·*V*₁ = *p*₂·*V*₂ (isotherm); Δ*V* = *V*₁ − *V*₂ [S26] |
| Leitung (`fluid.line`) | *Q* (`flowRate`), *d*_i (`innerDiameter`), *v* (`velocity`); Panel *p*, *L* (`length`), Δ*p* (`pressureDrop`); Ingenieur *Re* (`reynolds`), λ (`friction`), ν (`viscosity`, Vorgabe 46 mm²/s: Öl der Viskositätsklasse VG 46 bei 40 °C), ρ (`density`, Vorgabe 870 kg/m³), *k* (`roughness`, Vorgabe 0,0015 mm, gezogenes Rohr); Panel Σζ (`lossCoefficient`, Bögen, T-Stücke und Verschraubungen des Strangs) und Δ*p*_ζ (`pressureDropSingle`) | *v* = 4·*Q*/(π·*d*_i²); *Re* = *v*·*d*_i/ν; λ = 64/*Re* unter *Re* = 2320, darüber die explizite Näherung nach Haaland für Colebrook–White; Δ*p* = λ·(*L*/*d*_i)·ρ·*v*²/2 (gerade Leitung) [S27]; Einzelwiderstände Δ*p*_ζ = Σζ·ρ·*v*²/2 [S27] |

**Werte über Anschlüsse.** Leitungen verbinden Anschlüsse zu Netzen (Leitungsenden an Anschlüssen und an anderen
Leitungen). Je Netz gilt:

- *Q* (Volumenstrom) gilt gemeinsam, wenn höchstens zwei Elemente ihn führen (etwa Pumpe und Zylinder); Leitungen
  übernehmen ihn nur auf einem unverzweigten Strang. Ein Abzweig zu einem Druckbegrenzungsventil oder Manometer zählt
  für die Elemente nicht als Verzweigung, weil diese keinen Strom führen. Zwei Zylinder an einem Netz teilen nichts (die
  Aufteilung ist unbekannt).
- *p* (Druck) gilt im ganzen Netz; *p*_max ist der Einstelldruck des Druckbegrenzungsventils und geht an die Zylinder.
- Wegeventile geben *Q*, *p* und *p*_max zwischen P, A und B weiter (Versorgungssicht; T/R/S bleiben getrennt);
  Rückschlagventile, Durchflussmesser, Filter, Kühler, Heizer, Wartungseinheiten, Kreuzungen und gekuppelte
  Schnellkupplungen geben alle drei weiter; Drosseln geben *Q* und *p*_max weiter, aber nicht *p*; Druckminderventile
  geben *Q* weiter und setzen *p* an A auf *p*_E. Das entsperrbare Rückschlagventil gibt alle drei weiter (P–A; doppelt
  A1–A2 und B1–B2), das Senkbremsventil nur *Q* (C–V: der Lastdruck wird gehalten).
- Pumpe und Motor teilen *n* (und *M* wie unter „Antriebswellen“ beschrieben) mit dem gekoppelten Antrieb oder der Welle.
- Die Drossel liest den Volumenstrom der an P (oder A) angedockten Leitung und die Drücke der an P und A angedockten
  Leitungen (Beziehung `fluid.line.docked`): eine 100-bar-Versorgung, eine 2-mm-Drossel und ein einfachwirkender
  Zylinder bei 60 bar ergeben Δ*p* = 40 bar, den Volumenstrom und die Zylindergeschwindigkeit. Das Hydraulikaggregat gibt
  *Q* und *p* an P wie eine Pumpe und seinen Einstelldruck als *p*_max weiter, die versorgten Zylinder erhalten so ihre
  Kraftgrenze.
- Druckverluste sind Werte der Leitungen, Ventile und der Drossel; sie werden nicht von den Netzdrücken abgezogen, die je
  Netz ein Wert bleiben. Der Zulaufweg unten summiert sie je Zylinder.

**Zulaufweg eines Zylinders** (Runde 6, `FluidCircuits`). Von der ersten Versorgung (nach Id), deren Druckanschluss P den
Zylinder erreicht – Pumpe, Hydraulikaggregat oder Druckquelle –, folgt das Modul den Leitungen (auch auf Leitungen
angedockten Leitungen und mehreren Leitungen an einem Anschluss) und den Durchgängen der Elemente (wie für den
Volumenstrom *Q* oben: Wegeventil P–A–B, Rückschlag- und entsperrbare Rückschlagventile, Filter, Kühler, Heizer,
Drosseln, Kupplungen, Kreuzungen) bis zum Anschluss A des Zylinders (sonst B). Jeder Weg summiert die Druckverluste, die
er passiert, aber nur die der Teile mit eingegebenem Verlust: Leitungen mit eingegebener *L* oder Δ*p* (ihr Δ*p*) oder Σζ
(ihr Δ*p*_ζ), Ventile und Aufbereitungsteile mit eingegebenem Δ*p*, ζ oder Datenblattpunkt, die Drossel mit eingegebener
Blende oder Δ*p*; andere Teile zählen null. ΣΔ*p* des Zylinders ist die größte Wegsumme (Beziehung `fluid.circuit.sum`),
also bekannt, sobald jeder gezählte Verlust auf jedem Weg bekannt ist, und der am Zylinder verfügbare Druck ist
*p*_verf = *p*_Versorgung − ΣΔ*p* (`fluid.circuit.available`, *p*_Versorgung das *p* der Versorgung) [S26]. Ein
Druckbegrenzungsventil oder Manometer an einem Abzweig beendet seinen Weg dort (kein Durchgang). Elemente anderer Module
beenden einen Weg; die Suche endet nach 256 Wegen oder 50 000 Schritten (dann keine Summe). Beispiel
(Abnahmekriterium): die Pumpe mit 14,5 l/min von oben bei 100 bar, eine 2 m lange Leitung mit 10 mm zum 4/3-Wegeventil
(laminar, Δ*p* = 0,79 bar), der Datenblattpunkt des Ventils 6 bar bei 30 l/min (1,40 bar) und 1,5 bar eingetragen an der
Leitung nach A ergeben ΣΔ*p* = 3,69 bar und *p*_verf = 96,3 bar; eine erforderliche Kraft von 19,3 kN ist dann nicht mehr
erreichbar (`fluid.cyl.force.losses`), obwohl 100 bar·*A*₁ = 19,6 kN sie gäbe.

Beispiel (Abnahmekriterium): eine Pumpe mit *V*_g = 10 cm³ bei 1450 min⁻¹ liefert *Q* = 14,5 l/min; über ein
4/3-Wegeventil an einen Zylinder 50/28 mm bei 100 bar: *F*_aus = 19,6 kN, *F*_ein = 13,5 kN, *v*_aus = 0,123 m/s,
*v*_ein = 0,179 m/s, gleich ob die 100 bar an der Pumpe oder am Zylinder eingetragen sind.

**Prüfungen** (nur Hinweise, Overlay, nichts wird geändert):

| Id | Regel | Schwere | Quelle |
|---|---|---|---|
| `fluid.cyl.force` | *F*_erf > *p*_max·*A*·η_hm (*p*_max vom Druckbegrenzungsventil, sonst der Versorgungsdruck *p*) | Warnung | [S26] |
| `fluid.velocity` | Leitungsgeschwindigkeit außerhalb des Lehrbuchbereichs ihrer Funktion: Saugleitung 0,5–1,5 m/s (an S einer Pumpe), Rücklauf 2–4 m/s (Leckölleitung, Tank, Anschluss T/R), Druckleitung 3–6 m/s | Hinweis | [S26] |
| `fluid.pressure.relief` | eine Pumpe mit Werten, in deren Drucknetz (über Ventile) kein Druckbegrenzungsventil liegt | Hinweis | [S25] (ISO 4413) |
| `fluid.tank.volume` | Hydraulikaggregat: *V*_T < 3·*Q*·1 min (Faustregel aus dem Lehrbuch für stationäre Aggregate: 3–5-facher Förderstrom je Minute) | Hinweis | [S26] |
| `fluid.circuit.losses` | Zylinder: die summierten Druckverluste des Zulaufwegs erreichen den Versorgungsdruck (*p*_verf ≤ 0) | Warnung | [S26] |
| `fluid.cyl.force.losses` | Zylinder: *F*_erf > *p*_verf·*A*·η_hm (*A*₁ oder *A*₂ je nach erreichtem Anschluss; nicht gemeldet, wenn `fluid.cyl.force` gemeldet wird) | Warnung | [S26] |

## Hinweise für Hosts

Alle Symbole bestehen aus einfachen nativen Grundelementen (Linien, Rechtecke, Ellipsen, Polylinien, geschlossene
Dreiecke, Text); ein Vektorbild als Ausweichlösung ist nicht nötig. Abgerundete Rechtecke (geschlossener Behälter,
Speicher) nutzen den Eckenradius des Rechtecks.

## Grenzen und nächste Schritte

- Keine numerische Anschlusskennzeichnung (`1`, `2`, `3`, `12`, `14` …), keine Servo-/Proportionalventile, keine
  Logikbausteine.
- Das Hydraulikaggregat ist ein Symbol: Motor, Pumpe, Druckbegrenzungsventil und Filter sind gezeichnet, keine eigenen
  Elemente (keine Welle, keine eigenen Anschlüsse); der Förderstrom wird am Aggregat eingetragen. Ventilverluste nutzen
  einen Datenblattpunkt (Δ*p* ∝ *Q*²), keine gemessene Kennlinie; die Kompressibilität der Luft in der Blendenbeziehung
  und der rücklaufseitige Gegendruck eines Zylinders werden nicht abgebildet.
- Ein auf einer anderen Leitung (deren Schiene) angedocktes Leitungsende hängt an ihr (Abzweig): Es folgt der Leitung,
  und die Verbindungsliste fasst beide Leitungen zusammen; ein Verbindungspunkt ist optional.
- Kennzeichen und Anschlussnamen liegen außerhalb der Objektbox; Auswahlrahmen der Hosts schließen sie nicht ein.
- Die Wellentests (`ShaftTests`) nutzen das Mechanik- und das Maschinenmodul nur als Testreferenzen; das Modul selbst
  hängt allein vom Kern ab (die Wellenverknüpfungen lesen die Kopplung des Kerns und die Wertschlüssel `n`/`T`).
- Sichtprüfung: `dotnet test` schreibt eine SVG-Galerie (Anschlüsse als rote Kreise, Ventilvarianten, Beispielschaltung)
  nach `tests/InventaSketch.Modules.Fluid.Tests/bin/Debug/net10.0/gallery/index.html`.
