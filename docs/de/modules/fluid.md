# Modul Fluidtechnik (`fluid`)

Bibliothek: `src/InventaSketch.Modules.Fluid` · Tests: `tests/InventaSketch.Modules.Fluid.Tests` ·
Englische Fassung (maßgeblich): [../../en/modules/fluid.md](../../en/modules/fluid.md)

Das Modul liefert Schaltzeichen für Hydraulik- und Pneumatikschaltpläne im Sinne der Konventionen von ISO 1219-1
(grafische Symbole der Fluidtechnik) und des aus ISO-1219-2-Schaltplänen bekannten Kennzeichnungsschemas (z. B. `1V1`,
`0P1`). Alle Symbole sind **eigenständig gezeichnet**; es werden keine Normtexte, Tabellen oder Symbolgrafiken
übernommen, und es wird **keine Normkonformität behauptet**.

Es ist ein **Skizzier**-Modul für Folien, technische Abbildungen und Konzeptschaltungen. Es simuliert, dimensioniert und
prüft keine Schaltungen.

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
X/Y/Z von Wegeventilen, entsperrbaren Rückschlagventilen und Vorsteuerungen eine Steuerleitung, Leckanschlüsse L eine
Leckleitung, alle anderen eine Arbeitsleitung; danach die Bauteile mit einem passenden Anschluss. Das freie Ende einer
Leitung bietet „+“ zum Verlängern oder zum Andocken des nächsten Bauteils mit dem zugewandten Anschluss.

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
| `fluid.line.kind=…`, `fluid.line.route=orthogonal/free` | `Option`/Führungs-Piktogramme | Leitungsart ▾; Führung ▾ |
| `fluid.ports` | `Text` | Anschlussnamen anzeigen |
| `fluid.label` | `Text` | Kennzeichen ein-/ausblenden (setzt bei Bedarf einen Vorschlag wie `1V1`) |
| `fluid.ref.set` | `Text` | Kennzeichen direkt eingeben (Eingabeaktion) |

## Objekttypen

| Kategorie | Typen (`fluid.*`) |
|---|---|
| Pumpen, Motoren & Zylinder | `pump` (Pumpe/Verdichter; `variable`, `flow`), `motor` (`variable`, `flow`), `cylinder.double` (`rod` einseitig/durchgehend, `cushion` keine/fest/einstellbar), `cylinder.single` (`spring` Rückstellung/Vorhub/keine), `rotary` |
| Wegeventile | `valve.directional` (`ways` 2/2, 3/2, 4/2, 4/3, 5/2, 5/3; `normal`; `centre`; `actLeft`/`actRight`: keine, Muskelkraft, Druckknopf, Hebel, Pedal, Rolle, Feder, Elektromagnet, Druckansteuerung; `centring`; `detent`), `actuation` (separates Betätigungssymbol inkl. Rastung) |
| Sperr-, Strom- & Druckventile | `valve.check` (`spring`, `piloted`), `valve.throttle` (`adjustable`, `check` = Drosselrückschlagventil), `valve.relief`, `valve.reducing`, `valve.sequence` (Leckanschluss `L`), `valve.shuttle` (ODER), `valve.twopressure` (UND), `valve.quickexhaust` |
| Versorgung, Aufbereitung & Speicher | `tank` (offen/geschlossen), `source`, `exhaust`, `silencer`, `filter`, `separator`, `lubricator`, `cooler`, `heater`, `frl` (ausführlich/vereinfacht), `accumulator` (Gas/Feder/Gewicht) |
| Messen & Melden | `gauge`, `pressureswitch`, `flowmeter`, `thermometer` |
| Leitungen & Verbindungen | `line` (Punktobjekt), `junction`, `crossing` (einfach oder mit Brücke), `plug`, `coupling` (gekuppelt/entkuppelt, mit Rückschlagventilen), `enclosure` (strichpunktierte Baugruppenumrahmung) |

Begleiter („Einfügen ▸“): Eine Leitung bietet Ventile, Zylinder, Manometer und Verbindungspunkte an; ein Zylinder
Wegeventile, Drosseln und Rückschlagventile; ein Wegeventil Betätigungen, Leitungen, Zylinder und Drosseln.

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
| Doppeltwirkender Zylinder | *D* (`bore`), *d* (`rodDiameter`), *F*_aus (`forceExt`), *v*_aus (`speedExt`); Panel *p*, *Q*, *F*_ein (`forceRet`), *v*_ein (`speedRet`), *A*₁ (`areaExt`), *A*₂ (`areaRet`), φ (`areaRatio`), *s* (`stroke`), *t*_aus (`timeExt`), *t*_ein (`timeRet`); Ingenieur η_hm, *F*_erf (`forceReq`), *p*_max (`pmax`, verknüpft) | *A*₁ = π·*D*²/4 (durchgehende Stange: *A*₁ = *A*₂); *A*₂ = π·(*D*² − *d*²)/4; φ = *A*₁/*A*₂; *F* = *p*·*A*·η_hm und *v* = *Q*/*A* in beide Richtungen; *t* = *s*/*v* [S26] |
| Einfachwirkender Zylinder | *D*, *F* (`force`, ohne Federkraft), *v* (`speed`), *p*; Panel *Q*, *d*, *A* (`area`), *s*, *t* (`time`); Ingenieur η_hm, *F*_erf, *p*_max | *A* = π·*D*²/4 (Federvorhub: Ringfläche); *F* = *p*·*A*·η_hm; *v* = *Q*/*A*; *t* = *s*/*v* [S26] |
| Schwenkantrieb | *V*_g, *p*, *M*; Ingenieur η_hm | *M* = *V*_g·*p*·η_hm/(2π) [S26] |
| Druckbegrenzungs-, Druckminder-, Zuschaltventil | *p*_E (`setPressure`) | – |
| Manometer, Druckschalter, Druckquelle | *p* (aus dem Netz angezeigt) | – |
| Durchflussmesser | *Q* (aus dem Netz angezeigt) | – |
| Hydrospeicher | *V*₀ (`volume`), *p*₀ (`p0`), Δ*V* (`volumeUsable`); Panel *p*₁, *p*₂, *V*₁, *V*₂ (absolute Drücke) | *p*₀·*V*₀ = *p*₁·*V*₁ = *p*₂·*V*₂ (isotherm); Δ*V* = *V*₁ − *V*₂ [S26] |
| Leitung (`fluid.line`) | *Q* (`flowRate`), *d*_i (`innerDiameter`), *v* (`velocity`); Panel *p* | *v* = 4·*Q*/(π·*d*_i²) [S27] |

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
  geben *Q* weiter und setzen *p* an A auf *p*_E.
- Verluste in Leitungen und Ventilen werden nicht abgebildet.

Beispiel (Abnahmekriterium): eine Pumpe mit *V*_g = 10 cm³ bei 1450 min⁻¹ liefert *Q* = 14,5 l/min; über ein
4/3-Wegeventil an einen Zylinder 50/28 mm bei 100 bar: *F*_aus = 19,6 kN, *F*_ein = 13,5 kN, *v*_aus = 0,123 m/s,
*v*_ein = 0,179 m/s, gleich ob die 100 bar an der Pumpe oder am Zylinder eingetragen sind.

**Prüfungen** (nur Hinweise, Overlay, nichts wird geändert):

| Id | Regel | Schwere | Quelle |
|---|---|---|---|
| `fluid.cyl.force` | *F*_erf > *p*_max·*A*·η_hm (*p*_max vom Druckbegrenzungsventil, sonst der Versorgungsdruck *p*) | Warnung | [S26] |
| `fluid.velocity` | Leitungsgeschwindigkeit außerhalb des Lehrbuchbereichs ihrer Funktion: Saugleitung 0,5–1,5 m/s (an S einer Pumpe), Rücklauf 2–4 m/s (Leckölleitung, Tank, Anschluss T/R), Druckleitung 3–6 m/s | Hinweis | [S26] |
| `fluid.pressure.relief` | eine Pumpe mit Werten, in deren Drucknetz (über Ventile) kein Druckbegrenzungsventil liegt | Hinweis | [S25] (ISO 4413) |

## Hinweise für Hosts

Alle Symbole bestehen aus einfachen nativen Grundelementen (Linien, Rechtecke, Ellipsen, Polylinien, geschlossene
Dreiecke, Text); ein Vektorbild als Ausweichlösung ist nicht nötig. Abgerundete Rechtecke (geschlossener Behälter,
Speicher) nutzen den Eckenradius des Rechtecks.

## Grenzen und nächste Schritte

- Keine numerische Anschlusskennzeichnung (`1`, `2`, `3`, `12`, `14` …), keine Servo-/Proportionalventile, keine
  Logikbausteine.
- Ein auf einer anderen Leitung (deren Schiene) angedocktes Leitungsende hängt an ihr (Abzweig): Es folgt der Leitung,
  und die Verbindungsliste fasst beide Leitungen zusammen; ein Verbindungspunkt ist optional.
- Kennzeichen und Anschlussnamen liegen außerhalb der Objektbox; Auswahlrahmen der Hosts schließen sie nicht ein.
- Sichtprüfung: `dotnet test` schreibt eine SVG-Galerie (Anschlüsse als rote Kreise, Ventilvarianten, Beispielschaltung)
  nach `tests/InventaSketch.Modules.Fluid.Tests/bin/Debug/net10.0/gallery/index.html`.
