# Modul Verfahrens- & Wärmetechnik (`process`)

R&I- und Fließschema-Symbole über die Fluidtechnik hinaus (Behälter, Kolonnen, Reaktoren, Wärmeübertrager, Pumpen,
Verdichter, Armaturen, Messstellen mit Kennzeichen, Prozess- und Signalleitungen), RLT-Komponenten von
Lüftungsgeräten sowie thermodynamische Kreisprozess-Diagramme (T-s, p-v, log p-h) für Folien und schnelle technische
Skizzen.

Die Symbole sind **eigenständig gezeichnet** nach verbreiteter Fließschema-Praxis (die aus ISO 10628-2, ISA-5.1 und
ISO 14617-6 bekannten Konventionen dienten nur zur Orientierung). Es werden keine Normtexte oder Tabellen
wiedergegeben und **keine Normkonformität behauptet**. Das Modul ist ein Skizzenwerkzeug, kein Prozesssimulator und
keine Stoffdatenbank.

## Ansichten

| Ansicht | Bezeichnung (EN / DE) | Symbol | Verwendet von |
|---|---|---|---|
| `symbol` | P&ID symbol / R&I-Symbol | `IconIds.Symbol` | allen Apparaten, Armaturen, Messstellen und Leitungen (Standard) |
| `block` | Block (block flow diagram) / Block (Grundfließschema) | `IconIds.Block` | Apparaten (Behälter, Wärmeübertragung, Maschinen): Rechteck mit Kennzeichen und Apparatename in der Sprache der Oberfläche |
| `chart` | Diagram / Diagramm | `IconIds.Chart` | den drei Kreisprozess-Diagrammen |

Der Wechsel zwischen `symbol` und `block` behält die Box. Die Stutzen-Ids sind in beiden Ansichten gleich; in der
Blockansicht rückt ein Stutzen, der innerhalb der Symbolbox liegt (auf einem gewölbten Boden), an den nächsten Rand
des Blocks.

## Konventionen

- **Quick Sketch first:** jedes Objekt ist ohne Parameter einfügbar (jeder Parameter hat einen Standardwert); freies
  (auch ungleichmäßiges) Skalieren ändert `Size`, nie Parameter. Jede Zeichnung bleibt in ihrer Box, außer dem
  optionalen Anlagenkennzeichen, das außerhalb der Box steht (wie das Kennzeichen im Fluidtechnik-Modul).
- **Entwurfsbox:** Symbole sind in Entwurfsmillimetern gezeichnet und werden auf die Objektgröße gestreckt. Parameter,
  die Teile hinzufügen (Auflager des Behälters, Rührwerk/Doppelmantel des Reaktors, Antrieb des Dreiwegeventils),
  vergrößern die Entwurfsbox; die Objektgröße folgt ihr (im Inspektor wie bei Kontextaktionen), Symbole werden also
  nie gestaucht.
- **Stutzen** sind `AnchorRole.Port`-Anker mit stabilen Ids (`N1`…, `S`/`D` Saug-/Druckseite, `T1`/`T2` Rohrseite,
  `S1`/`S2` Mantelseite, `A1`/`A2` Luftseite, `W1`/`W2` Wasserseite …). Die meisten liegen auf dem Boxrand, angedockte
  Leitungen verlassen sie daher senkrecht.
- **Anlagenkennzeichen** (`tag`, `showTag`): standardmäßig leer. „Kennzeichen anzeigen“ setzt einen Vorschlag
  (`V-101`, `P-101`, `E-101` …); der Text steht außerhalb der Box auf der Seitenrichtung mit den wenigsten Stutzen
  (unten, rechts, oben, links), weicht einer Leitung aus der Seitenmitte aus und bleibt bei gedrehtem Symbol aufrecht.
- **Kennzeichen der Messstelle** (`tag`, Standard `TIC 101`): aufgeteilt in Funktionsbuchstaben (oben, fett) und
  Nummer (unten): `TIC 101` → TIC / 101, `PT-205A` → PT / 205A.
- Einheiten mm, y nach unten, Winkel im Uhrzeigersinn; Linienbreiten 0,35 mm (Umrisse), 0,5 mm (Hauptfließlinien),
  0,25/0,18 mm Details.

## Objekttypen

| Id | Name (EN / DE) | Entwurfsbox | Stutzen | Parameter |
|---|---|---|---|---|
| `process.vessel.vertical` | Vertical vessel / Behälter, stehend | 14x30 (+5 mit Auflager) | N1 oben, N2 unten, N3 links, N4 rechts | `heads` ellipsoidal/flat/conical, `support` none/legs/skirt, `level` |
| `process.vessel.horizontal` | Horizontal vessel / Behälter, liegend | 32x14 (10 ohne Sättel und Wassersack) | N1, N2 Böden, N3 oben, N4 unten (Wassersack) | `saddles` (true), `boot`, `level` |
| `process.vessel.tank` | Storage tank / Lagertank | 24x20 | N1 Dach, N2, N3 nahe dem Boden | `roof` cone/dome/floating, `level` |
| `process.column` | Column / Kolonne | 12x50 | N1 Kopf, N2 Sumpf, N3 Zulauf, N4 Rücklauf, N5 Rückführung Verdampfer | `internals` trays/packed, `trays` (8, 2–30), `beds` (2, 1–4) |
| `process.reactor` | Stirred tank reactor / Rührkesselreaktor | 16x30 | N1, N3 Deckelstutzen, N2 unten, J1/J2 mit Doppelmantel | `agitator` (true), `jacket` |
| `process.cyclone` | Cyclone separator / Zyklonabscheider | 14x30 | N1 tangentialer Einlauf (links), N2 Gasaustritt (Tauchrohr, oben), N3 Feststoffaustrag (unten) | – |
| `process.silo` | Silo (bulk solids) / Silo (Schüttgut) | 16x34 | N1 Befüllung (Dachspitze), N2 Austrag (Trichterauslauf) | `legs` (true), `level` (Füllstand) |
| `process.hx.shelltube` | Shell-and-tube heat exchanger / Rohrbündel-Wärmeübertrager | 32x12 | T1, T2 Rohrseite, S1, S2 Mantelseite | `bundle` utube/fixed |
| `process.hx.plate` | Plate heat exchanger / Plattenwärmeübertrager | 14x16 | A1, A2, B1, B2 | `plates` dargestellt (7, 3–15) |
| `process.hx.general` | Heat exchanger (general symbol) / Wärmeübertrager (allgemein) | 12x12 | P1, P2 Schlange, S1, S2 Mantel | – |
| `process.hx.aircooler` | Air cooler (fin-fan) / Luftkühler (Rippenrohr mit Ventilator) | 24x14 | P1, P2 | – |
| `process.hx.coolingtower` | Cooling tower / Kühlturm | 20x24 | W1 Warmwasser-Zulauf, W2 Ablauf Becken | `draft` natural/mechanical |
| `process.pump.centrifugal` | Centrifugal pump / Kreiselpumpe | 12x12 | S Saugseite, D Druckseite (tangential); Wellennabe rechts (gegenüber dem Saugstutzen) | `base` (true) |
| `process.pump.displacement` | Positive displacement pump / Verdrängerpumpe | 12x12 | S, D; Wellennabe oben | `kind` gear/screw/piston/diaphragm |
| `process.compressor` | Compressor / Verdichter | 14x12 | S, D; Wellennabe oben | `kind` dynamic/reciprocating |
| `process.turbine` | Turbine / expander / Turbine / Entspannungsmaschine | 14x12 | I, O; Abtriebswellenstumpf unten (Achse + Nabe) | – |
| `process.fan` | Fan / Ventilator | 12x12 | I, O; Wellennabe rechts (radial) oder oben (axial) | `kind` radial/axial |
| `process.motor` | Electric motor (drive) / Elektromotor (Antrieb) | 10x10 | shaft (Anschluss, bleibt für alte Anbindungen); Wellenstumpf rechts (Achse + Nabe) | – |
| `process.valve.manual` | Valve (manual) / Armatur (handbetätigt) | 10x6 | 1, 2 | `kind` gate/globe/ball/butterfly, `closed` |
| `process.valve.control` | Control valve / Stellventil (Regelventil) | 12x13 | 1, 2, S (Signal am Antrieb) | `actuator` diaphragm/motor/solenoid/piston, `fail` none/open/close/last (FO/FC/FL) |
| `process.valve.check` | Check valve / Rückschlagarmatur | 10x6 | 1, 2 (Durchfluss 1 → 2) | – |
| `process.valve.safety` | Safety / relief valve / Sicherheitsventil | 10x12 | in (unten), out (seitlich) | – |
| `process.valve.threeway` | Three-way valve / Dreiwegeventil | 9x7 (9x12 mit Antrieb) | A, AB, B, S mit Antrieb | `actuated` |
| `process.fitting.reducer` | Reducer / Reduzierstück | 8x6 | 1, 2 | `eccentric` |
| `process.fitting.strainer` | Strainer / filter / Schmutzfänger / Filter | 10x8 | 1, 2 | `kind` filter/y |
| `process.valve.steamtrap` | Steam trap / Kondensatableiter | 10x6 | 1, 2 | – |
| `process.rupturedisc` | Rupture disc / Berstscheibe | 8x10 | in (unten), out (oben) | – |
| `process.sightglass` | Sight glass / Schauglas | 10x6 | 1, 2 | – |
| `process.instrument.bubble` | Instrument (tag bubble) / Messstelle (PLT-Stelle) | 12x12 | B, T, L, R | `tag` (TIC 101), `location` field/central/local/behind, `shape` discrete/shared/computer/plc |
| `process.instrument.orifice` | Flow element (orifice / venturi) / Durchflussmessglied (Blende / Venturi) | 10x6 | 1, 2 | `kind` orifice/venturi |
| `process.line` | Process line / Prozessleitung (Fließlinie) | Punkte | Schienen entlang des Verlaufs | `kind` main/secondary/traced/future, `arrow`, `lineNo`, `route` orthogonal/free, `hops` |
| `process.line.signal` | Signal line / Signalleitung (Wirkungslinie) | Punkte | Schienen | `kind` connection/electric/pneumatic/capillary/data, `route` |
| `process.hvac.coil` | Air heating / cooling coil / Lufterhitzer / Luftkühler | 10x12 | A1, A2 Luft, W1, W2 Wasser | `kind` heating (+)/cooling (−) |
| `process.hvac.damper` | Air damper / Luftklappe | 10x10 | A1, A2 | `motorised` |
| `process.hvac.filter` | Air filter / Luftfilter | 10x10 | A1, A2 | `kind` panel/bag |
| `process.hvac.recovery` | Heat recovery / Wärmerückgewinnung | 10x20 | S1, S2 Zuluft (unten), E1, E2 Abluft (oben) | `kind` plate/wheel |
| `process.hvac.humidifier` | Humidifier / Luftbefeuchter | 10x12 | A1, A2, W1 | `kind` steam/spray |
| `process.chart.ts` | T-s diagram (cycle) / T-s-Diagramm (Kreisprozess) | 80x60 | – | `cycle` rankine/carnot, `superheat`, `dome`, `labels`, `caption` |
| `process.chart.pv` | p-v diagram (cycle) / p-v-Diagramm (Kreisprozess) | 80x60 | – | `cycle` otto/diesel/carnot, `ratio` (8), `cutoff` (2), `pressureRatio` (2,5, erweitert), `gamma` (1,4, erweitert), `efficiency`, `labels`, `caption` |
| `process.chart.ph` | log p-h diagram (refrigeration cycle) / log p-h-Diagramm (Kältekreisprozess) | 80x60 | – | `ratio` p_hoch/p_niedrig (4, 1,5–7,5), `superheat`, `subcool`, `labels`, `caption` |

Anordnungsstriche der Messstelle: vor Ort = ohne Strich, Leitwarte (zugänglich) = Vollstrich, örtliche Tafel =
Doppelstrich, hinter der Tafel = Strichlinie. Geräteformen: Einzelgerät = Kreis, Prozessleitsystem = Kreis im Quadrat,
Rechnerfunktion = Sechseck, SPS = Raute im Quadrat.

Neue Symbole (eigenständig gezeichnet): der Kondensatableiter ist ein runder Körper mit Schwimmer (gefüllte Kugel) und
Kondensatspiegel; die Berstscheibe eine gewölbte Scheibe zwischen zwei Halterflanschen, zur Austrittsseite gewölbt; das
Schauglas ein rundes Gehäuse mit zwei Glasscheiben; der Zyklon ein Zylinder mit tangentialem Einlauf, Konus,
gestricheltem Tauchrohr und Drallpfeil; das Silo ein Zylinder mit Kegeldach und Trichterboden auf Füßen.

## Antriebswellen (ein Motor treibt die Pumpe)

Pumpen, Ventilatoren, Verdichter, Turbinen und der Antriebsmotor haben eine echte mechanische Welle mit demselben
Vertrag wie die Wellen und Antriebe des Mechanik- und des Maschinenmoduls ([mechanical-drives.md](mechanical-drives.md)):

- **Angetriebene Maschinen** (`process.pump.centrifugal`, `process.pump.displacement`, `process.compressor`,
  `process.fan`) haben einen `Hub`-Anker (Id `hub`), wo die Welle den Körper verlässt. Eine Wellenachse durch diesen
  Punkt trägt die Maschine: eine mechanische Welle (`mech.shaft`), die Welle einer E-Maschine, der Wellenstumpf des
  Motorsymbols im Maschinenmodul oder der Stumpf des Prozessmotors oder der Turbine.
- **Antriebe** (`process.motor`, `process.turbine`; `ObjectType.DrivesShaft`) bieten ihren Wellenstumpf als
  `Axis`-Segment (Id `shaft`) mit Nabe und `End`-Marke am äußeren Ende. Eine angetriebene Maschine dockt mit ihrer Nabe
  am Stumpf an; eine vom Wellenende fortgesetzte mechanische Welle trägt den Antrieb, und Verschieben des Antriebs nimmt
  die Welle (und alles darauf) mit. „+“ setzt die Welle nur am äußeren Ende fort.
- Die Nabe hat keinen Bohrungsradius (0): eine Maschine auf einer Welle behält ihre Größe. Angedockt wird mit dem
  normalen Fangen (Nabe auf Achse) und der Kopplung des Kerns („bewegt sich mit“). In der Blockansicht rückt die Nabe an
  den nächsten Blockrand.
- Die Wellenseiten folgen den Symbolen: Kreiselpumpe und Radialventilator rechts (gegenüber dem axialen Saugstutzen), die
  übrigen angetriebenen Maschinen oben, die Turbinenwelle unten (ein Verdichter oder eine Pumpe unter der Turbine dockt
  ohne Drehen an), die Motorwelle rechts. Zum Andocken auf der anderen Seite das Symbol spiegeln.
- **Werte:** jede Maschine mit Welle führt Drehzahl *n* (`speed`), Drehmoment *M* (`torque`) und Wellenleistung *P*
  (`power`) mit *P* = 2π·*n*·*M* [S18]. Eine Maschine teilt *n* mit dem Element, mit dem sie gekoppelt ist (getragen oder
  konzentrisch). *M* geht über eine direkte Eins-zu-eins-Kupplung (Motor an der Pumpennabe, keines von beiden mit einem
  dritten Element mit Moment gekoppelt) und von der mechanischen Welle oder Kupplung, auf der die Maschine sitzt, wenn
  dieser Wellenstrang höchstens zwei Glieder hat, die Moment aufnehmen oder abgeben (die Regel des Mechanikmoduls; bei
  einer Verzweigung wird kein Moment weitergegeben). So wird die Leistung des Antriebs zur Wellenleistung der Pumpe: ein
  Motor mit 1450 min⁻¹ und 50 N·m gibt der Pumpe *P* = 7,59 kW; mit *V̇* = 100 m³/h und *H* = 20 m folgt der
  Pumpenwirkungsgrad η = 71,8 %.

## Thermodynamische Diagramme

Die Diagramme sind Lehrskizzen mit **normierten Achsen ohne Skalenwerte**. Die Zustände sind mit 1–4 nummeriert,
kleine Pfeilspitzen zeigen die Richtung jeder Zustandsänderung, eine optionale Beschriftung nennt den Prozess.

- **p-v (Vergleichsprozesse, ideales Gas, konstante Wärmekapazitäten):** Isentropen `p·v^γ = const` (im Deutschen
  meist κ), Isothermen `p·v = const`, `v2 = 1`, `p1 = 1`.
  - Otto: 1→2 isentrope Verdichtung um `r` (ε), 2→3 isochor (`p3 = α·p2`), 3→4 isentrop, 4→1 isochor;
    `η = 1 − r^(1−γ)` (r = 8, γ = 1,4: 56,5 %).
  - Diesel: 2→3 isobar bis zum Einspritzverhältnis `r_c` (φ); `η = 1 − r^(1−γ)·(r_c^γ − 1)/(γ·(r_c − 1))`.
  - Carnot: Gesamtvolumenverhältnis `r = v4/v2`, gleichmäßig auf das adiabate und das isotherme Verhältnis aufgeteilt
    (je `√r`); `η = 1 − T_niedrig/T_hoch`.
  Der Wirkungsgrad wird nur auf Wunsch angezeigt (`efficiency`) und nur für diese p-v-Prozesse, bei denen er aus dem
  Modell folgt.
- **T-s:** die Grenzkurve ist eine qualitative Form (`s = s_c ∓ a·√τ ∓ b·τ`, `τ = (T_c − T)/(T_c − T_0)`), keine
  Wasserdampftafel. Clausius-Rankine: 1→2 Pumpe (leicht überhöht gezeichnet), 2→3 Vorwärmung, Verdampfung und
  optionale Überhitzung (Isobare des idealen Gases `T = T_g·exp((s − s_g)/c_p)`), 3→4 isentrope Entspannung ins
  Nassdampfgebiet, 4→1 Kondensation. Carnot: ein Rechteck innerhalb der Grenzkurve. Kein Wirkungsgrad (keine Skala).
- **log p-h (Kaltdampf-Kompression):** qualitative Grenzkurve; 1→2 Verdichtung nach `h2 − h1 = c·ln(p2/p1)` (aus
  `dh = v·dp` mit `p·v ≈ const`), 2→3 Verflüssigung (optional unterkühlt), 3→4 isenthalpe Drosselung, 4→1 Verdampfung
  (optional überhitzt). Das Druckverhältnis ist so begrenzt, dass die Verflüssigung unter dem kritischen Punkt bleibt.

Die Formeln stehen in `Cycles` (öffentlich, mit Unit-Tests).

## Bibliothek

Ein Standardeintrag je Typ (Id = Typ-Id) und Vorlagen: stehender Behälter auf Füßen, stehender Abscheider mit
Füllstand, Trichter (Kegelboden), Dreiphasenabscheider mit Wassersack, Tank mit Kuppel- und Schwimmdach,
Packungskolonne, Rührkessel mit Doppelmantel, Wärmeübertrager mit festen Rohrböden, zwangsbelüfteter Kühlturm,
Schraubenspindel-/Kolben-/Membranpumpe, Kolbenverdichter, Axialventilator, Absperrventil/Kugelhahn/Absperrklappe,
Absperrschieber normal geschlossen, Stellventil mit Elektroantrieb und Magnetventil, Stellventil „schließt bei
Ausfall“, Dreiwegeventil mit Stellantrieb (Kategorie RLT), exzentrisches Reduzierstück, Y-Schmutzfänger,
Messstellen-Vorlagen (TIC Leitwarte, PI, FT, LIC im Prozessleitsystem, PT, SPS-Funktion), Venturirohr,
Neben-/beheizte/geplante Leitung und Leitung mit Fließpfeil, pneumatische/Wirkdruck-/Kapillar-/Datenleitung,
Luftkühler, Luftklappe mit Antrieb, Taschenfilter, Rotationswärmeübertrager, Sprühbefeuchter, Zu-/Abluftventilator
(Kategorie RLT), Carnot und Clausius-Rankine ohne Überhitzung (T-s), Diesel und Carnot (p-v), ein
Wärmepumpenprozess (log p-h) sowie ein Silo mit Füllstand (`process.silo.level`). Suchbegriffe gibt es in beiden Sprachen (z. B. „Behälter“, „Kolonne“,
„Wärmeübertrager“, „Pumpe“, „Messstelle“, „Stellventil“, „Kreisprozess“, „Wärmepumpe“).

## Leitungen

`process.line` und `process.line.signal` sind Punktobjekte: Ecken lassen sich einfügen, der geführte Verlauf ist eine
Schiene (Abzweige und Messstellen-Anschlüsse docken überall an, mit Verbindungspunkt), die Führung ist standardmäßig
orthogonal. Ein angedocktes Ende verlässt seinen Stutzen senkrecht zum Boxrand; ein Stutzen innerhalb der Box (auf einem
gewölbten Boden) verlässt ihn in Richtung des nächsten Randes. Prozessleitungen springen über ältere Prozessleitungen
(`hops`, Core `Crossings`) und melden kreuzende Leitungen als abhängig. Die beheizte Leitung erhält eine gestrichelte
Begleitheizung neben der Linie; die Leitungsnummer steht entlang des längsten Abschnitts. Signalzeichen (Schrägstriche,
Kreuze, Kreise) haben 8 mm Abstand und meiden die Ecken.

**Weiterbauen mit „+“ (Anschlüsse).** Stutzen und Rohranschlüsse bieten eine Prozessleitung an (`process`),
Messstellen und der Stellantriebsanschluss S von Stellventilen eine Signalleitung (`instrument`), danach die Apparate,
Armaturen oder Messstellen, die dort anschließen. Die Antriebswelle eines Motors oder einer Turbine ist eine
mechanische Kupplung ohne Leitung (siehe „Antriebswellen“); die freie Nabe einer angetriebenen Maschine bietet eine Welle
durch sie an. Das freie Ende einer Prozess- oder Signalleitung bietet „+“ zum Verlängern oder zum Andocken des nächsten
Elements.

## Kontextaktionen

Kennzeichen (anzeigen / eingeben), Auswahlen ▾ (Böden, Auflager, Dach, Einbauten, Rohrbündel, Zug,
Pumpen-/Verdichter-/Ventilatorbauart, Armaturenart, Antrieb, Sicherheitsstellung, Anordnung und Gerät der Messstelle,
Filter, Wärmerückgewinnung, Befeuchter, Kreisprozess, Leitungsführung), Umschalter (Füllstand, Sättel, Wassersack, Rührwerk,
Doppelmantel, Grundrahmen, geschlossen, Stellantrieb, exzentrisch, Pfeil, Brücken, Grenzkurve, Überhitzung,
Unterkühlung, Wirkungsgrad, Zustandsnummern, Beschriftung), Schritte (Böden, Packungsbetten, Platten, Verdichtungs-/
Druckverhältnis), Eingaben für Leitungsnummer und Messstellen-Kennzeichen, orthogonale/gerade Führung.

## Technische Werte (Calc 4)

Werte sind optional: jedes Symbol lässt sich weiterhin ohne sie einfügen und skalieren. Eingetragen und angezeigt werden
sie mit der Wertekarte (*Bearbeiten ▸ Werte ▸*, die Schaltfläche *Werte* in Erkunden und Ingenieur), gespeichert in
optionalen Textparametern (Ingenieurabschnitt des Inspektors). Wasser ist das voreingestellte Medium: c = 4,19 kJ/(kg·K),
ρ = 1000 kg/m³ (Vorgaben, die eingetragenen Werten weichen). Kartengrößen stehen vorn. Entwurfsnotizen:
[engineering-calculations.md, Abschnitt 13](../design/engineering-calculations.md#13-umsetzungsnotizen-calc-4-fluid-prozess-gebäudetechnik-energie-25112026).

| Element | Größen (Parameter) | Eigene Beziehungen [Quelle] |
|---|---|---|
| Kreiselpumpe | *V̇* (`flowRate`, m³/h), *H* (`head`), *P* (`power`, Welle), η (`efficiency`); Panel Δ*p* (`pressureRise`), *P*_hyd (`powerHyd`), *n* (`speed`), *M* (`torque`); Ingenieur ρ (`density`) | Δ*p* = ρ·*g*·*H*; *P*_hyd = Δ*p*·*V̇*; *P* = *P*_hyd/η [S28]; *P* = 2π·*n*·*M* [S18] |
| Verdrängerpumpe, Ventilator | *V̇*, Δ*p*, *P*, η; Panel *P*_hyd, *n*, *M* | *P*_hyd = Δ*p*·*V̇*; *P* = *P*_hyd/η [S28]; *P* = 2π·*n*·*M* [S18] |
| Verdichter, Turbine | *P* (`power`, Wellenleistung: Antrieb des Verdichters, Abgabe der Turbine), *n* (`speed`); Panel *M* (`torque`) | *P* = 2π·*n*·*M* [S18] |
| Antriebsmotor | *P* (`power`, Abgabe), *n*, *M* | *P* = 2π·*n*·*M* [S18] |
| Rohrbündel-, Platten-, allgemeiner Wärmeübertrager | Φ (`duty`), *k* (`u`), *A* (`area`), Δθ_m (`lmtd`); Panel ṁ₁ (`massFlow1`), θ₁,ein (`temp1In`), θ₁,aus (`temp1Out`), ṁ₂, θ₂,ein, θ₂,aus; Ingenieur *c*₁, *c*₂ (`specificHeat1/2`) | Φ = ṁ₁·*c*₁·(θ₁,ein − θ₁,aus); Φ = ṁ₂·*c*₂·(θ₂,aus − θ₂,ein); Gegenstrom Δθ_m = (Δθ_a − Δθ_b)/ln(Δθ_a/Δθ_b) mit Δθ_a = θ₁,ein − θ₂,aus, Δθ_b = θ₁,aus − θ₂,ein; Φ = *k*·*A*·Δθ_m [S29] |
| Luftkühler, Kühlturm | Φ, ṁ (`massFlow`), θ_ein (`tempIn`), θ_aus (`tempOut`); Ingenieur *c* | Φ = ṁ·*c*·(θ_ein − θ_aus) [S29] |
| Behälter, Tank, Reaktor, Kolonne | *V* (`volume`), *D* (`diameter`), *h* (`length`, zylindrischer Teil), *Q* (`heat`); Panel Δθ (`deltaT`); Ingenieur ρ, *c* | *V* = π·*D*²·*h*/4 (Böden nicht mitgerechnet); *Q* = ρ·*V*·*c*·Δθ [S29] |
| Prozessleitung | *V̇* (`flowRate`), ṁ (`massFlow`), *v* (`velocity`), θ (`temperature`); Panel *d*_i (`innerDiameter`); Ingenieur ρ | ṁ = ρ·*V̇*; *v* = 4·*V̇*/(π·*d*_i²) [S27, S29] |

Seite 1 eines Wärmeübertragers ist die Rohrseite (T1 ein, T2 aus), die A-Seite des Plattenwärmeübertragers (A1, A2) und
die Primärseite des allgemeinen Symbols (P1, P2); Seite 2 ist die Mantelseite (S1, S2), die B-Seite (B1, B2) und die
Sekundärseite (S1, S2). Φ > 0 heißt: Wärme fließt von Seite 1 zu Seite 2; ist Seite 1 kälter, wechseln alle Differenzen
das Vorzeichen und Φ < 0. Andere Bauarten als Gegenstrom brauchen einen Korrekturfaktor (nicht abgebildet).

**Werte über Anschlüsse.** Prozessleitungen verbinden Stutzen zu Netzen. Volumenstrom *V̇*, Massenstrom ṁ und
Stofftemperatur θ gelten gemeinsam, wenn höchstens zwei Elemente sie im Netz führen; Leitungen übernehmen sie auf einem
unverzweigten Strang. Die Leitung rechnet *V̇* und ṁ ineinander um. Hand-, Stell- und Rückschlagarmaturen,
Schmutzfänger, Reduzierstücke, Durchflussmessglieder und Schaugläser geben alle drei weiter, Luftfilter und Luftklappen
(A1–A2) ebenso; Pumpen und Ventilatoren geben die Temperatur weiter. Eine Pumpe, die über eine Armatur einen
Wärmeübertrager speist, setzt so ṁ₁, und die Austrittstemperatur der Seite 1 wird zur Eintrittstemperatur des nächsten
Elements. Wärmeverluste und die Erwärmung in Pumpen werden nicht abgebildet. Verdichter und Turbinen haben nur
Wellenwerte (Verdichtungs- und Entspannungsarbeit brauchen Gaseigenschaften); Kondensatableiter, Berstscheiben, Zyklone
und Silos haben keine Werte.

**Wellenverknüpfungen.** Drehzahl, Drehmoment und Leistung gehen über Antriebswellen wie unter „Antriebswellen“
beschrieben (Beziehungen `process.shaft.speed`, `process.shaft.torque`); das Lösen einer Maschine holt ihren Antrieb und
ihren Wellenstrang in den Umfang.

**Prüfungen** (nur Hinweise, Overlay, nichts wird geändert):

| Id | Regel | Schwere | Quelle |
|---|---|---|---|
| `heat.cross` | Wärmeübertrager mit allen vier Temperaturen: (θ₁,ein − θ₂,aus)·(θ₁,aus − θ₂,ein) ≤ 0, eine Temperaturkreuzung, die im Gegenstrom nicht erreichbar ist | Warnung | [S29] |

## Hinweise für Hosts

Alle Ausgaben sind native Primitive (Linien, Polylinien, Polygone, Rechtecke, Ellipsen, kubische Pfade, Text); jeder
Host kann sie nativ zeichnen, ein Vektorbild ist nicht nötig. Prozessleitungen hängen von kreuzenden Leitungen ab
(Brücken). Diagramme und Leitungen sind keine Teile (`IsPart = false`).

## Tests und Galerie

`tests/InventaSketch.Modules.Process.Tests`: Registrierung, Id-Konventionen, Standard-Bibliothekseintrag je Typ,
auflösbare Bibliothekseinträge mit ihren Parametern, zweisprachige Namen/Bezeichnungen/Optionen/Aktionen, gemeinsame
Piktogramme, Standardwerte für jeden Parameter, Suche in beiden Sprachen, Begleiter; jeder Bibliothekseintrag in jeder
Ansicht in beiden Sprachen innerhalb seiner Box (auch ohne Parameter, gestreckt, gestaucht, skaliert), jeder
Parameterwert ändert die Zeichnung, Blockansicht mit Text und Kennzeichen, Lage des äußeren Kennzeichens und aufrechter
Text, Aufteilung des Messstellen-Kennzeichens und Anordnungsstriche, Stutzen (benannt, eindeutig, in der Box, meist auf
dem Rand, gleiche Ids in jeder Ansicht, mit der Box skaliert), Fangen einer Leitung an einem Stutzen, angedockte
Leitungen folgen dem Apparat und verlassen Stutzen senkrecht, Schienen, Brücken, Leitungsarten, Signalzeichen,
JSON-Rundlauf eines Beispiel-Fließschemas und die Kreisprozess-Formeln (Otto, Diesel, Carnot p-v und T-s, Rankine,
Kältekreis). `ShaftTests` docken Pumpen, Verdichter und Ventilatoren an eine mechanische Welle, das Motorsymbol des
Maschinenmoduls, einen Wellenstrang mit E-Maschine, den Prozessmotor und eine Turbine an (Kopplung und gemeinsame *n*,
*M*, *P*); sie nutzen das Mechanik- und das Maschinenmodul nur als Testabhängigkeiten (das Modul selbst hängt allein vom
Kern ab). Der Galerie-Test schreibt `gallery/*.html` in den Testausgabeordner (jeder Typ in jeder Ansicht,
gestreckt, Vorlagen, ein Destillations- und ein Lüftungsgeräte-Beispiel).
