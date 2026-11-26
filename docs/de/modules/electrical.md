# Modul Elektrotechnik & Elektronik (`electrical`)

Bibliothek: `src/InventaSketch.Modules.Electrical` · Tests: `tests/InventaSketch.Modules.Electrical.Tests` ·
Englische (maßgebliche) Fassung: [../../en/modules/electrical.md](../../en/modules/electrical.md)

Das Modul liefert Schaltzeichen im Stil von IEC 60617, Leitungen, die an Anschlüssen der Symbole andocken,
Installationsplan-Symbole der Hausinstallation, Elemente für einpolige Übersichtsschaltpläne, Blöcke der
Leistungselektronik und illustrative Leiterplatten-Objekte. Alle Symbole sind **eigenständig gezeichnet**; es werden
keine Normtexte, Tabellen oder Inhalte von Symboldatenbanken übernommen und keine Normkonformität behauptet.

Es ist ein **Skizzier**-Modul für Folien und technische Abbildungen, **kein** ECAD-Werkzeug: Es bietet eine
Verbindungsliste (Netzliste als CSV) und eine Grundprüfung, aber keine Simulation und keine Fertigungsdaten.

## Datenexporte

- **Netzliste / Verbindungsliste (CSV)** (`elec.netlist`): an Pins angedockte Leitungsenden verbinden Pins zu Netzen;
  Verbindungspunkte führen Netze zusammen. Eine Zeile je Netz mit seinen Pins (`R1.1 C1.2`). Erhält Verbindungen und
  Kennzeichen, nicht die Leitungsführung.
- **Elektrische Regelprüfung (CSV-Bericht)** (`elec.erc`): eine Zeile je Befund – `pin.unconnected` (keine Leitung am
  Pin angedockt oder darauf liegend), `wire.open` (ein Leitungsende weder angedockt noch auf Pin, Verbindungspunkt oder
  anderer Leitung), `part.shorted` (eine Leitung verbindet zwei Pins desselben Bauteils). Eine Grundprüfung für
  illustrative Schaltpläne: keine Pin-Typen, Treiber oder Versorgungsnetze. Beide Exporte stehen bereit, sobald
  mindestens ein Leitungsende an einem Pin angedockt ist.

## Konventionen

- **Quick Sketch zuerst:** Jedes Bibliothekselement wird ohne Parameter eingefügt. Freies (gleichmäßiges oder
  ungleichmäßiges) Skalieren ändert nur `Size`; Symbole sind für eine Entwurfsbox gezeichnet und werden gestreckt.
- **Lage:** Zweipole liegen waagerecht mit Anschlüssen an den Mitten der linken und rechten Kante (Signal-/Stromfluss von
  links nach rechts). Drehen und Spiegeln sind die allgemeinen Objekttransformationen, keine Symbolparameter.
- **Rasterfreundliche Anschlüsse:** In Standardgröße und an einem Rasterpunkt eingefügt, liegen Schaltplan-Anschlüsse auf
  einem 2,5-mm-Raster (meist auf dem 5-mm-Raster). Standardboxen sind Vielfache von 5 mm (z. B. Widerstand 20 × 8,
  Gatter 20 × 20, Operationsverstärker 30 × 20).
- **Linienbreiten:** Symbolkonturen und Leitungen 0,35 mm, betonte Striche (Platten, Balken) 0,7 mm, Sammelschienen und
  Busleitungen 1,0 mm, mechanische Wirkverbindungen dünn gestrichelt. Alle Linien haben `LineRole.Visible`/`Thin`,
  die Schwarzweiß-Ausgabe funktioniert also.
- **Beschriftung:** optionale Parameter `ref` (Kennzeichen, z. B. `R1`) und `value` (z. B. `10k`). Das Kennzeichen steht
  über, der Wert unter der Box (bei senkrechten Symbolen wie Erde oder Antenne rechts daneben). Beim Drehen bleiben die
  Texte auf der Seite aufrecht; sie liegen bewusst **außerhalb** der Symbolbox wie in ECAD-Schaltplänen.
  `showLabel = false` blendet sie aus.
- **Text in Symbolen** (`M`, `&`, `≥1`, `kWh`, Pinnummern) bleibt ebenfalls aufrecht.

## Darstellungen (Views)

| View-ID | Icon (`IconIds`) | Verwendung |
|---|---|---|
| `symbol` | `Symbol` | Schaltzeichen im IEC-Stil (Standard für Schaltplan-Elemente) |
| `ansi` | `Simplified` | ANSI-Zickzack-Widerstand und -Potentiometer, ANSI-Gatterformen, Dreieck-OPV, Elko mit gebogener Platte |
| `circles` | `Simplified` | Transformator als zwei überlappende Kreise (einpolig) |
| `block` | `Block` | Steuergerät/IC-Block, Stromrichter, Gate-Treiber |
| `plan` | `Plan` | Installationsplan-Symbole, Leitungsführung |
| `section`, `schematic` | `Front`, `Schematic` | Steckdose, Schalter, Leuchte, Abzweigdose und Verteiler an der Wand in ihrer Montagehöhe (`height`, mm über OKFF) und als Installationsschema; eine ausgewählte Installation wechselt zusammen mit der Gebäudetechnik Grundriss ↔ Schnitt ↔ Schema (`InstallationViews` im Core, 25.11.2026) |
| `top` | `Top` | Leiterplatten-Objekte |

## Anschlüsse und Fangen

- Jedes anschließbare Symbol stellt seine Anschlüsse als `Anchor.Point(AnchorRole.Port, …)` mit stabiler ID bereit
  (`1`/`2`, `A`/`K`, `B`/`C`/`E`, `G`/`D`/`S`, `+`/`-`, `in1…`/`out`, `U`/`V`/`W`, …). Anschlüsse sind in normierten
  Boxkoordinaten (−0,5…0,5) gespeichert und bleiben bei jedem Skalieren, Drehen und Spiegeln am Symbol.
- Leitungsenden sind Punktanker (`AnchorRole.End`); der Snapper zieht ein gezogenes Leitungsende auf einen Anschluss
  (`Snapper.SnapPoint`, Fangart `port`) und die Ports eines bewegten Symbols auf Leitungsenden oder andere Anschlüsse.
  Anschlüsse gewinnen immer gegen den allgemeinen Ecken- und Mittelpunktfang, auch bei einem 2 × 2 mm großen
  Verbindungspunkt, dessen Ecke näher am Leitungsende liegt als sein Anschluss.
- Leitungen und Busse sind **Schienen** (`AnchorRole.Rail` entlang des geführten Verlaufs, IDs `rail0`, `rail1`, …): Ein
  anderes Leitungsende, ein Verbindungspunkt oder ein Anschluss dockt an beliebiger Stelle an (Fangart `rail`,
  T-Abzweig). Schienenenden und Leitungsknicke ziehen wie Anschlüsse an; dazwischen rangiert eine Schiene hinter
  einzelnen Anschlüssen und Abgriffen.
- `ElectricalModule.ConnectToPin(registry, doc, wireId, index, tolerance)` setzt ein Leitungsende auf den nächsten
  Anschluss innerhalb der Toleranz und speichert eine `Attachment` mit der **Anschluss-ID** (`AnchorId`); die Leitung
  folgt dann beim Verschieben, Drehen und Skalieren des Symbols, und `Attachments.Refresh` (läuft in der Engine nach
  jeder Bearbeitung) führt sie mit, wenn der Anschluss im Symbol wandert (Polaritätsumkehr, Anschlusszahl, Ansicht).
- Orthogonale Leitungen verlassen bzw. erreichen angedockte Anschlüsse senkrecht zur Boxkante, auf der der Anschluss
  liegt (unter Berücksichtigung der Symboldrehung).
- Busleitungen bieten alle 10 mm `Port`-Abgriffe und dazwischen eine Schiene, die Sammelschiene fünf Abgriffe.
- Objekte ohne Anschlüsse: Leiterplattenkontur, Kupferfläche, Befestigungsbohrung, Bestückungsdruck-Text.

**Weiterbauen mit „+“ (Anschlüsse).** Jeder freie Anschluss bietet eine Leitung (orthogonal) und die dort passenden
Schaltzeichen an; Elemente der Hausinstallation die Leitungsführung (`electric.installation`, auch von den Anschlussdosen
der Gebäudetechnik genutzt), Pads und Bauformen der Leiterplatte eine Leiterbahn (`pcb`). Leitungen führen `electric`
(sie passen an DC- und AC-Anschlüsse anderer Module), Busleitungen `data`; das freie Ende einer Leitung, Busleitung,
Leitungsführung oder Leiterbahn bietet „+“ zum Verlängern oder zum Andocken des nächsten Schaltzeichens mit dem
zugewandten Anschluss.

**Aderfarben und Querschnitt (26.11.2026).** Die Leitung ist der eine Leitungstyp aller Module: Auch die Geräte des
Verteilermoduls schließen sie an (dessen eigene Ader-Einträge treten zurück, siehe Verteilermodul). Optionale Parameter
`colour` (`auto` – schwarz bzw. die Medienfarbe bei Färbung nach Medium – und die Kennbuchstaben nach IEC 60757 BK, BN,
GY, BU, GNYE, RD, DBU, WH, VT, OG; Grün-gelb wird grün mit gelben Strichen gezeichnet) und `section` (mm², standardmäßig
ohne). Eine Leitung mit Farbe behält sie bei Färbung nach Medium (`MediumRole` = eigene Farbe). Die Adernummer ist die
allgemeine Bezeichnung (`ref`, „Bezeichnung …“), gezeichnet von `Designations`. Bibliothekseinträge „Ader schwarz
(Schaltschrank)“, „Ader braun (L)“, „Ader blau (N)“, „Ader grün-gelb (PE)“ (`elec.wire.bk`, `.bn`, `.bu`, `.pe`;
1,5 mm²). Farben helfen auch in der Elektronik (rote/schwarze Versorgungsleitungen …).

**Abzweig ⇄ Brücke (26.11.2026).** Eine über eine andere gezogene Leitung springt mit einer Brücke (`HopsAtCrossings`,
Core `Crossings`); die Hosts bieten an einer Kreuzung das Verbinden an (dort geteilt, beide Teile angedockt: ein Abzweig
mit Punkt) und für zwei ausgewählte, sich kreuzende Leitungen „Verbinden (Abzweig)“ (`Crossings.ConnectBetween`). Die
Umkehrung ist „Als Brücke (nicht verbunden)“ (Aktion `elec.bridge`, Piktogramm `Bridge`, `Crossings.Disconnect`):
angeboten von einer Leitung, deren eigenes Ende an einer anderen Leitung angedockt ist (T- oder X-Abzweig), und von einem
Verbindungspunkt (`elec.junction`), durch den Leitungen gehen. Zwei Enden, die von gegenüberliegenden Seiten kommen,
werden zu einer Leitung verbunden, die die andere mit einer Brücke kreuzt (der am Abzweig verbleibende Eckpunkt entfällt,
wenn die Leitung gerade weiterläuft, sonst rückt er 3 mm von der anderen Leitung ab); ein einzelner Abzweig wird gelöst
und 3 mm (`Crossings.BridgeOvershoot`) über die andere Leitung hinaus verlängert, sodass er sie kreuzt; der
Verbindungspunkt entfällt. Ein Verbindungspunkt nennt die an ihm angedockten Leitungen als abhängig (`HasDependents`),
damit Hosts mit getrennten Einheiten sie sehen.

## Kontextaktionen

| Aktions-ID | Icon | Wirkung |
|---|---|---|
| `view.<id>` | View-Icon | Darstellung wechseln (IEC/ANSI, Spulen/Kreise, …) |
| `elec.toggle` | `Toggle` | Schaltzustand offen/geschlossen (`closed`; Öffner standardmäßig geschlossen) – Schalter, Taster, Schütz, LS-Schalter, RCD |
| `elec.polarity` | `Swap` | Polarität umkehren (`reverse`: spiegelt Symbol und Anschlüsse) – Dioden, Thyristor, Elko, Quelle, Batterie |
| `elec.polarity`/`elec.channel` | `Swap` | NPN ↔ PNP, N- ↔ P-Kanal (MOSFET) |
| `elec.<Auswahl>=<Wert>` | `Option` | Auswahlen (Dropdowns): Gatterfunktion, Kontaktart, Erdungszeichen, Quellenart, Stromart, Stecker/Buchse, Gehäuse, Busprotokoll, Thermistor NTC/PTC (`elec.thermistor.kind`), TVS-Richtung (`elec.tvs.direction`), JFET-Kanal (`elec.jfet.channel`), … |
| `elec.opamp.circuit=<auto/inverting/noninverting/follower/open>` | `Option` | Schaltung des Operationsverstärkers für die Verstärkung (Vorgabe: aus der Verdrahtung) |
| `elec.meter.quantity=<v/a/w/ohm>` | Options-Piktogramme (`Preview`: Messgerätekreis mit Buchstaben) | Messgröße des Messgeräts |
| `elec.wire.hops` | `Hop` | Brücken einer Leitung an Kreuzungen an/aus |
| `elec.wire.colour=<auto/BK/BN/GY/BU/GNYE/RD/DBU/WH/VT/OG>` | Optionsbilder (`Preview`: ein Strich in der Farbe) | Aderfarbe einer Leitung |
| `elec.wire.section=<–/0,14 … 16>` | `Option` | Querschnitt einer Leitung (mm²) |
| `elec.bridge` | `Bridge` | Abzweig wieder zur Brücke machen (nicht verbunden): Leitung mit einem Ende am Abzweig, Verbindungspunkt |
| `elec.route=free/orthogonal/45` | Führungs-Piktogramme | Leitungs-/Leiterbahnführung als Auswahl |
| `elec.label` | `Text` | Kennzeichen/Wert anzeigen (setzt `ref` = Präfix + `1`, falls leer) oder ausblenden |
| `elec.envelope` | `Toggle` | Gehäusekreis bei Transistoren |

Werte werden im Inspektor bearbeitet (`value`, `ref`); Kontextaktionen können noch keinen Text abfragen.

## Objekttypen

| Kategorie | Typen (`elec.*`) |
|---|---|
| Passive Bauelemente | `resistor` (IEC-Rechteck / ANSI-Zickzack), `resistor.thermistor` (`coefficient` ntc/ptc: Heißleiter/Kaltleiter, schräger Strich mit −ϑ/+ϑ), `resistor.ldr` (Fotowiderstand, Lichtpfeile), `resistor.varistor` (VDR, schräger Strich mit U), `potentiometer`, `capacitor`, `capacitor.polarized`, `inductor` (`core`: ohne/Eisen/Ferrit), `transformer` (Spulen / Kreise, `core`), `crystal` (Schwingquarz); die Widerstandsarten haben die IEC- und ANSI-Darstellung des Widerstands |
| Halbleiter | `diode`, `diode.led`, `diode.zener`, `diode.schottky`, `diode.tvs` (Suppressordiode, `direction` uni/bi), `diode.bridge` (Brückengleichrichter: `AC1`/`AC2` links/rechts, `+` oben, `-` unten), `diode.photo`, `transistor.bjt` (`polarity` npn/pnp), `transistor.mosfet` (`channel` n/p, Anreicherungstyp), `transistor.jfet` (`channel` n/p), `transistor.igbt`, `thyristor`, `triac` (`A1`/`A2`/`G`), `diac`, `optocoupler` (Optokoppler: LED `A`/`K` links, Fototransistor `C`/`E` rechts); optionaler Gehäusekreis `envelope` |
| Schalten & Schützen | `switch` (`contact` Schließer/Öffner/Wechsler; auch für Relaiskontakte), `pushbutton`, `relay.coil`, `contactor` (1–3 Pole), `fuse`, `breaker`, `rcd` |
| Quellen & Verbraucher | `ground` (Erde/PE/Gehäuse), `source` (DC/AC/Strom), `battery` (1–4 Zellen), `lamp`, `motor` und `generator` (`supply` ohne/DC/AC/3~; 3~ mit U/V/W), `sensor`, `actuator` (allgemeiner Aktorblock, optional `label`), `solenoid` (Hubmagnet: zweipolige Betätigungsspule mit Anker), `meter` (Messgerät, `quantity` V/A/W/Ω), `speaker`, `microphone`, `antenna` |
| Logik & ICs | `gate` (`gate` and/or/not/nand/nor/xor/xnor/buffer, `inputs` 2–4; IEC-Rechteck und ANSI-Form), `flipflop` (D/JK/RS/T), `opamp` (IEC / Dreieck), `regulator` (Spannungsregler mit drei Anschlüssen `IN`/`OUT`/`GND`), `block.ecu` (Steuergerät/IC/MCU-Block mit `pinsLeft`/`pinsRight` und `label`) |
| Leitungen, Steckverbinder & Busse | `wire` (Punktobjekt, `route` frei/orthogonal/45, `hops`, `colour`, `section`), `junction`, `netlabel` (Text = Netzname), `connector` (Stecker/Buchse/Paar, 1–12 Pole), `terminal`, `testpoint`, `bus` (Punktobjekt; Beschriftung CAN/LIN/Ethernet/FlexRay/allgemein) |
| Hausinstallation | `house.socket` (1–4 Steckdosen, Schutzkontakt), `house.switch` (Aus-/Wechsel-/Kreuzschalter/Taster/Dimmer), `house.light`, `house.junction`, `house.board`, `house.cable` (Punktobjekt mit Adernstrichen), `sld.busbar`, `sld.feeder` (Leistungsschalter/Sicherung/ohne), `sld.meter` |
| Leistungselektronik | `pe.inverter`, `pe.rectifier` (AC-Seite 1- oder 3-phasig), `pe.dcdc`, `pe.dclink`, `pe.gatedriver`, `pe.module` (Halbbrücke mit Freilaufdioden) |
| Leiterplatte (illustrativ) | `pcb.board`, `pcb.pad` (rechteckig/rund/langrund, THT), `pcb.via`, `pcb.track` (Punktobjekt, `width`, Führung frei/orthogonal/45), `pcb.copper`, `pcb.footprint` (SOIC-8, QFP-32, SOT-23, 0603, DIP-8; Pads sind Ports), `pcb.hole`, `pcb.silk` |

Die Installationsplan-Symbole orientieren sich sinngemäß an üblicher DIN/EN-Praxis; ihr genaues Aussehen unterscheidet
sich je nach Land und ist nicht normativ. Leiterplatten-Objekte sind proportional vereinfacht und enthalten keine
Land-Pattern- oder Designregel-Daten.

## Elementwerte (Calc 1 und Calc 3)

Technische Werte sind optional (Quick Sketch zuerst): Jedes Element lässt sich weiter ohne Parameter einfügen, und
abgeleitet wird erst, wenn Werte eingegeben sind. Konzept und Quellen:
[../design/engineering-calculations.md](../design/engineering-calculations.md) (Abschnitte 2.1–2.3, 10.7 und 10.10).
Eingegebene Werte liegen in optionalen Textparametern, die das Getippte behalten („230 V", „4k7"); abgeleitete Werte
werden nie gespeichert. Werte, die ein Element bestimmen, ergeben die übrigen; stimmige Werte werden als geprüft
markiert, widersprüchliche ergeben einen Widerspruch, und nichts wird geändert.

| Element | Variablen (Parameter) | Beziehungen [S7] |
|---|---|---|
| Widerstand `elec.resistor`, Thermistor `elec.resistor.thermistor`, Fotowiderstand `elec.resistor.ldr` | *R* (`value`, „4k7"; bei Thermistor und Fotowiderstand der Widerstand im Arbeitspunkt), *U* (`voltage`), *I* (`current`), *P* (`power`); Nennleistung `prated` für die Prüfung | *U* = *R*·*I*; *P* = *U*·*I* = *I*²·*R* = *U*²/*R* |
| Lampe `elec.lamp` | *U*, *I*, *P*, *R* (`resistance`, warm); Kennzeichnung in `value` („12 V 5 W") | wie beim Widerstand |
| Kondensator `elec.capacitor`, `elec.capacitor.polarized` | *C* (`value`, „100n"), *U*, *Q* (`charge`), *E* (`energy`); Panel *R* (`rseries`, Widerstand in Reihe), *τ* (`tau`), *f*_g (`cutoff`), *f* (`frequency`), *X*_C (`reactance`); Wechselstrom (Panel): *I* (`current`), *φ*_U, *φ*_I (`phaseu`, `phasei`, nur lesbar), *f*₀ (`resonance`), *Q* (`quality`), *B* (`bandwidth`) | *Q* = *C*·*U*; *E* = ½*C*·*U*² = ½*Q*·*U* = *Q*²/(2*C*); *τ* = *R*·*C*; *f*_g = 1/(2π·*τ*); *X*_C = 1/(2π·*f*·*C*); *U* = *X*_C·*I*; *B* = *f*₀/*Q* |
| Spule `elec.inductor` | *L* (`value`, „10 mH"), *I*, *E* (`energy`); Panel *R* (`rseries`), *τ*, *f*_g, *f*, *X*_L wie beim Kondensator; Wechselstrom (Panel): *U* (`voltage`), *φ*_U, *φ*_I, *f*₀, *Q*, *B* wie beim Kondensator | *E* = ½*L*·*I*²; *τ* = *L*/*R*; *f*_g = 1/(2π·*τ*); *X*_L = 2π·*f*·*L*; *U* = *X*_L·*I*; *B* = *f*₀/*Q* |
| Potentiometer `elec.potentiometer` | *R* (`value`, Bahn), *U* (`voltage`, zwischen Pin 1 und 2), *k* (`wiper`, 0 an Pin 1 … 100 % an Pin 2), *U*₂ (`uwiper`, Schleifer gegen Pin 2); Panel *I*, *P*, *R*₁ (`r1`), *R*₂ (`r2`) | Ohmsches Gesetz über die Bahn; *R*₁ = *k*·*R*; *R*₂ = *R* − *R*₁; unbelasteter Teiler *U*₂ = *U*·*R*₂/*R* |
| Bipolartransistor `elec.transistor.bjt` (als Schalter) | *R*_B (`rb`), *I*_C (`ic`), *B* (`beta`, h_FE aus dem Datenblatt), *U*_in (`uin`); Panel *I*_B (`ib`), *U*_BE (`ube`, Vorgabe 0,7 V), *k* (`overdrive`, Vorgabe 3) | *I*_B = *k*·*I*_C/*B*; *R*_B = (*U*_in − *U*_BE)/*I*_B |
| Operationsverstärker `elec.opamp` (ideal) | *V*_u (`gain`), *U*_e (`uin`), *U*_a (`uout`), *R*_f (`rf`); Panel *R*_e (`rin`, beim nichtinvertierenden Verstärker *R*_g), *U*_B (`usupply`, ±Versorgung); Auswahl `circuit` | *U*_a = *V*_u·*U*_e; invertierend *V*_u = −*R*_f/*R*_e; nichtinvertierend *V*_u = 1 + *R*_f/*R*_g; Spannungsfolger *V*_u = 1 |
| Diode, LED, Schottky-Diode `elec.diode`, `elec.diode.led`, `elec.diode.schottky` | *U*_F (`uf`), *I*_F (`if`), *P* (`power`); Kennzeichnung „2,1 V 20 mA"; LED: `ifmax` für die Prüfung | *P* = *U*·*I* |
| Z-Diode `elec.diode.zener` | *U*_Z (`uz`), *I* (`current`), *P* | *P* = *U*·*I* |
| Quelle `elec.source` | *U*, *I*, *P*; Panel \|*Z*\| (`impedance`), *φ* (`phase`), cos *φ* (`pf`), *S* (`apparent`), *Q* (`reactive`) der gespeisten Last; Frequenz `frequency` (einfacher Parameter oder „50 Hz" im Werttext); Kennzeichnung „5 V", „10 mA" (Stromquelle), „12 V 2 A", „230 V 50 Hz" | Gleichspannung und Stromquelle: *P* = *U*·*I* mit *S* = *P*, cos *φ* = 1, *φ* = 0, *Q* = 0, \|*Z*\| = *U*/*I*; Wechselspannung: *S* = *U*·*I*; *P* = *S*·cos *φ*; *Q* = *S*·sin *φ*; *S*² = *P*² + *Q*²; *U* = \|*Z*\|·*I* [S6, S7] |
| Batterie `elec.battery` | *U*, *I*, *P*; Kennzeichnung „12 V 2 A" | *P* = *U*·*I* |
| Motor, Generator `elec.motor`, `elec.generator` | *U* (`voltage`, bei 3~ Leiterspannung), *I* (`current`, Leiterstrom), *P* (`power`, elektrische Wirkleistung), cos *φ* (`pf`); Panel *S* (`apparent`), *Q* (`reactive`), *η* (`efficiency`), *P*_mech (`pmech`; Motor: abgegeben, Generator: aufgenommen); beim Motor zusätzlich *I*_A/*I*_N (`kstart`), *I*_A (`istart`), *I*_A,YΔ (`istartyd`) und die Auswahl `starting` (Direktanlauf / Stern-Dreieck, Drehstrommotoren) | nach `supply`: Gleichstrom *P* = *U*·*I* (cos *φ* = 1); Wechselstrom oder ohne Angabe *S* = *U*·*I*; Drehstrom *S* = √3·*U*·*I*; *P* = *S*·cos *φ*; *S*² = *P*² + *Q*² (*Q* ≥ 0, induktiv); Motor *P*_mech = *η*·*P*, Generator *P* = *η*·*P*_mech; *I*_A = (*I*_A/*I*_N)·*I*; Stern-Dreieck *I*_A,YΔ = *I*_A/3 [S7, S15] |
| Spannungsregler `elec.regulator` | *U*_out (`uout`), *U*_in (`uin`), *I*_out (`current`), *P* (`power`, Verlust); eine Spannung in `value` („LM7805 5 V") ist *U*_out | *P* = (*U*_in − *U*_out)·*I*_out (Linearregler, Eigenstromverbrauch vernachlässigt) |
| Transformator `elec.transformer` | *U*₁ (`u1`), *U*₂ (`u2`), *I*₁ (`i1`), *I*₂ (`i2`), *ü* (`ratio`, Panel); Kennzeichnung „230/12 V" | ideal: *ü* = *U*₁/*U*₂ = *I*₂/*I*₁ [S7, S15] |
| Sicherung, LS-Schalter, Abgang `elec.fuse`, `elec.breaker`, `elec.sld.feeder` | *I*_B (`current`), *I*_n (`rated`); Kennzeichnung „B16", „C 10 A", „gG 35 A"; `iz` (*I*_z der Leitung, Benutzer) für die Prüfung | – |
| RCD `elec.rcd` | *I*_n (`rated`), *I*_Δn (`idn`); Kennzeichnung „40 A 30 mA", „40/0,03 A", „30 mA" | – |
| Steckdose, Leuchte `elec.house.socket`, `elec.house.light` | *U*, *I*, *P* | *P* = *U*·*I* |
| Leitungsführung `elec.house.cable` | *l* (`length`, einfache Länge), *A* (`section`), *I* (`current`), Δ*u* (`droprel`); Panel *U*_n (`voltage`), cos *φ* (`pf`, Vorgabe 1), *ρ* (`rho`, Vorgabe Kupfer 0,017241 Ω·mm²/m bei 20 °C), *R* (`resistance`, ein Leiter), Δ*U* (`drop`), *P*_V (`loss`); Auswahl `system` (Vorgabe aus den Adern: 4–5 Drehstrom, sonst Wechselstrom; oder Wechselstrom, Drehstrom, Gleichstrom) und `material` (Kupfer, Aluminium 0,028264 Ω·mm²/m); `dumax` für die Prüfung | *R* = *ρ*·*l*/*A* [S12]; Wechselstrom Δ*U* = 2·*R*·*I*·cos *φ*, Drehstrom Δ*U* = √3·*R*·*I*·cos *φ*, Gleichstrom Δ*U* = 2·*R*·*I* (Leitungsreaktanz *X*′ vernachlässigt) [S8]; Δ*u* = Δ*U*/*U*_n; *P*_V = 2·*I*²·*R* (Wechselstrom, Gleichstrom) oder 3·*I*²·*R* (Drehstrom) [S7] |

**Kennzeichnungen.** Ist der Werttext eine Kennzeichnung und nicht die Größe des Elements selbst, werden die Größen darin
gelesen (jeweils mit Einheit) und als abgeleitet gezeigt, mit der Kennzeichnung als Formel („*I*_n = B16 = 16 A"). Ein
eingegebener Wert, der der Kennzeichnung widerspricht, ergibt einen Widerspruch. Ein Werttext ohne Kennzeichnung
(„1N4148", „rot") ergibt nichts und keinen Fehler.

**Stromkreise.** Werte gehen über Leitungen (auch Leitungsführungen und Busse), die an Pins angedockt sind, wie beim
Netzlistenexport; Verbindungspunkte, geschlossene Kontakte, eine gesteckte Steckverbindung, Sammelschienen, Abzweigdosen,
Verteiler und Energiezähler verbinden Netze; Erdungszeichen derselben Art und Netznamen mit gleichem Namen verbinden
Netze auf einer Folie (ein nur einmal verwendeter Netzname führt woandershin). Offene Kontakte und Messpunkte führen
keinen Strom. Schutzorgane werden üblicherweise in Ruhestellung gezeichnet; sie leiten daher, außer der Parameter
`closed` ist ausdrücklich auf falsch gesetzt. Varistoren, Suppressordioden und Schwingquarze leiten nicht (Gleichstrom,
Normalbetrieb); Messgeräte sind ideal: Spannungs- und Widerstandsmesser führen keinen Strom, ein Strommesser verbindet
seine Netze (kein Spannungsfall), ein Leistungsmesser hält den Stromkreis reduzierbar. Das Netz wird nur schrittweise über Reihen- und Parallelschaltungen
reduziert (keine Schaltungssimulation):

- Elemente in Reihe haben denselben Strom, parallele Elemente dieselbe Spannung;
- ohmsche Gruppen ergeben den Ersatzwiderstand (*R* = Σ*R*ᵢ, 1/*R* = Σ1/*R*ᵢ), kapazitive Gruppen die Ersatzkapazität
  (Kondensatoren in Reihe haben dieselbe Ladung);
- eine Quelle (oder Quellen in Reihe oder parallel) an der reduzierten Last schließt den Stromkreis: Spannung und Strom
  der Quelle sind die der Last (Maschensatz), z. B. der LED-Vorwiderstand: „4k7" in einem 5-V-Kreis mit einer 2,1-V-LED
  ergibt *I* = 0,617 mA, und der Spannungsteiler: 10 V an 1 kΩ + 4 kΩ ergibt 8 V am 4-kΩ-Widerstand;
- einpolige Verbraucher (Steckdosen, Leuchten) an einem Netz, das ein Zweig speist, addieren sich (Knotensatz) und haben
  dessen Spannung;
- ein Transformator ist Last seines Primär- und Quelle seines Sekundärkreises.

Spannungen und Ströme sind Beträge in Verbraucherrichtung; das gilt für Reihen-Parallel-Netze, die eine Quelle speist.
Eine Masche, die diese Schritte nicht reduzieren (eine Brücke, Quellen an mehreren Stellen), bekommt den Hinweis
`elec.net.reducible` („hier nicht reduzierbar – Schaltungssimulator verwenden"); die Elementbeziehungen wirken weiter.
Elemente ohne Werte im Stromkreis (Relaisspule, Hubmagnet, Fotodiode, Diac, Leistungsmesser) halten ihn reduzierbar,
aber kein Wert geht durch sie hindurch; ein Wechsel- oder Gleichstrommotor ist eine Last, ein Wechsel- oder
Gleichstromgenerator eine Quelle mit *U* und *I* (Runde 6); Drehstrommaschinen, Transistoren, Triacs, Optokoppler,
Brückengleichrichter, Spannungsregler, ICs und andere mehrpolige Teile sind Anschlüsse mit unbekanntem
Strom, sodass das Netz um sie herum nicht reduziert wird.

**Grundschaltungen (Runde 5, 2026-11-26).** Werte, die die um ein Element verdrahteten Bauteile lesen (Konzept:
Abschnitt 2.3):

- **RC- und RL-Glieder.** Eine Reihengruppe aus Widerständen und genau einem Kondensator oder einer Spule (ein RC- oder
  RL-Glied, das eine Quelle lädt, ein Tiefpass 1. Ordnung) ergibt den „Widerstand in Reihe" *R* des Kondensators oder der
  Spule als Ersatzwiderstand dieser Widerstände (Beziehung `elec.net.rtau`); *τ* und die Grenzfrequenz *f*_g folgen:
  10 kΩ in Reihe mit 100 nF ergeben *τ* = 1 ms und *f*_g = 159 Hz. *R* lässt sich auch eingeben (ohne Verdrahtung), und
  der Blindwiderstand bei einer Frequenz *f* folgt allein aus *C* oder *L*.
- **Potentiometer als Spannungsteiler.** Die Bahn ist ein ohmscher Zweig zwischen Pin 1 und 2; die Schleiferstellung *k*
  teilt sie, und der unbelastete Teiler ergibt die Schleiferspannung: 10 V an einem 10-kΩ-Potentiometer bei *k* = 30 %
  ergeben *U*₂ = 7 V. Ein am Schleifer angeschlossenes Bauteil macht ihn zum Anschluss mit unbekanntem Strom, ein
  belasteter Teiler wird daher nicht reduziert (die unbelastete Beziehung bleibt, mit ihrer Gültigkeit angegeben).
- **Grundschaltungen des Operationsverstärkers.** Die Auswahl **Schaltung** (`circuit`, Aktion `elec.opamp.circuit=…`)
  ist „aus der Verdrahtung" (Vorgabe), invertierend, nichtinvertierend, Spannungsfolger oder keine (Komparator, offen).
  Aus der Verdrahtung: ein einzelner Widerstand zwischen − und Ausgang ist *R*_f, ein einzelner weiterer Widerstand an −
  ist *R*_e; führt *R*_e zur Masse (Erdungszeichen oder Netzname „GND"), ist die Schaltung nichtinvertierend, liegt + an
  Masse, invertierend, und ein direkt mit − verbundener Ausgang ergibt einen Spannungsfolger. Die Werte der verdrahteten
  Widerstände werden übernommen (Beziehung `elec.link.part`): 10 kΩ und 100 kΩ an einem invertierenden
  Operationsverstärker ergeben *V*_u = −10, 0,5 V am Eingang ergeben −5 V am Ausgang. Ein Operationsverstärker ohne
  Beschaltung hat keine Verstärkungsbeziehung.
- **Transistor als Schalter.** Ein einzelner, allein an die Basis angeschlossener Widerstand ist der Basiswiderstand:
  sein Widerstand ist *R*_B, sein Strom *I*_B. Mit *I*_C und *B* folgt der Übersteuerungsfaktor *k* = *I*_B·*B*/*I*_C
  (Prüfung `elec.bjt.overdrive`).

**Wechselstromkreise (Runde 6, 2026-11-26).** Konzept: Abschnitte 2.1 und 2.2. Alle Werte sind Effektivwerte (Beträge).

- **Wechselstrombetrieb.** Ein Stromkreis mit einer Wechselspannungsquelle (`kind` ac), einem Generator für Wechsel- oder
  Drehstrom oder einem Transformator ist ein Wechselstromkreis. Spannungen in Reihe und Ströme parallel addieren sich darin
  nur als Zahlen, solange die Elemente in Phase sind (alle ohmsch, alle induktiv oder alle kapazitiv); eine gemischte
  Gruppe bekommt keine Summenbeziehung, an ihre Stelle treten die Zeigerbeziehungen. Gleichstromkreise bleiben unverändert.
- **Impedanz und Phase.** Eine Wechselspannungsquelle an einer Last aus Widerständen, Spulen und Kondensatoren (Sicherungen
  und LS-Schalter zählen als ideale Leiter) erhält die Impedanz |*Z*| und den Phasenwinkel *φ* = arg *Z* der Last, mit den
  komplexen Widerständen *R*, j*ωL* und 1/(j*ωC*) (in Reihe addiert, parallel 1/Σ(1/*Z*ᵢ)). Jede Spule und jeder
  Kondensator erhält seinen Strom *I*, seine Spannung *U* = *X*·*I* und die Phasen *φ*_U, *φ*_I von Spannung und Strom
  gegen die Quellenspannung (nur lesbar); ein Widerstand erhält seinen Strom (seine Spannung folgt aus *U* = *R*·*I*, in
  Phase mit dem Strom). Die Frequenz ist der Parameter `frequency` der Quelle oder eine Frequenz in ihrem Werttext
  („230 V 50 Hz"); die Blindelemente übernehmen sie als ihr *f*. Beispiel: 230 V, 50 Hz an *R* = 100 Ω und *L* = 0,318 H
  in Reihe ergeben |*Z*| ≈ 141 Ω, *φ* ≈ 45°, *I* = 1,63 A und cos *φ* ≈ 0,71; 230 Ω parallel zu einem Kondensator mit
  *X*_C = 230 Ω führen je 1 A, die Quelle liefert aber √2 A. Eine rein ohmsche Wechselstromlast braucht keine Frequenz.
- **Wechselstromleistung.** An einer Wechselspannungsquelle gilt *S* = *U*·*I*, *P* = *S*·cos *φ*, *Q* = *S*·sin *φ*
  (positiv induktiv, negativ kapazitiv) und *S*² = *P*² + *Q*². cos *φ* folgt aus *φ*, nicht umgekehrt (das Vorzeichen
  ginge verloren). An einer Gleichspannungsquelle sind diese Werte trivial (*S* = *P*, cos *φ* = 1, *φ* = 0, *Q* = 0,
  |*Z*| = *U*/*I*), eine Gleichspannungsquelle mit *U* und *I* bleibt also vollständig bestimmt.
- **Resonanz.** Eine Spule und ein Kondensator in einer Reihen- oder Parallelgruppe, allein oder mit Widerständen, bilden
  einen Schwingkreis: beide erhalten *f*₀ = 1/(2π√(*LC*)) (ein eingegebenes *f*₀ ergibt das fehlende *L* oder *C*); mit
  Widerständen auch die Güte *Q* = √(*L*/*C*)/*R* (Reihe, *R* in Reihe) oder *Q* = *R*·√(*C*/*L*) (parallel, *R*
  parallel) und die Bandbreite *B* = *f*₀/*Q*. In Resonanz ist ein RLC-Reihenkreis rein ohmsch (|*Z*| = *R*), und die
  Spannungen an *L* und *C* sind *Q*-mal so groß wie die Quellenspannung (Hinweis `elec.ac.resonance`). Ein in Reihe zu
  *L* gezeichneter Spulenwiderstand in einem Parallelkreis ((*R* + *L*) ∥ *C*) wird nicht erkannt.
- **Motor und Generator.** Die Symbole tragen elektrische Werte nach ihrer Stromart: Drehstrom *P* = √3·*U*·*I*·cos *φ*
  mit Leiterspannung und Leiterstrom (400 V, 10 A und cos *φ* = 0,85 ergeben 5,89 kW), Wechselstrom
  *P* = *U*·*I*·cos *φ*, Gleichstrom *P* = *U*·*I*; Wirkungsgrad und mechanische Leistung (Motor: abgegeben, Generator:
  aufgenommen); beim Motor den Anlaufstrom aus *I*_A/*I*_N und mit **Anlauf** Stern-Dreieck (`starting`, Aktion
  `elec.motor.starting=…`, Drehstrommotoren) ein Drittel davon [S15]. Im Stromkreis ist ein Wechsel- oder
  Gleichstrommotor eine Last mit *U* und *I* (eine Wechselstromlast mit eigenem Leistungsfaktor, also mit nichts anderem
  in Phase), ein Wechsel- oder Gleichstromgenerator eine Quelle; Drehstrommaschinen bleiben Anschlüsse mit unbekanntem
  Strom. Drehzahl und Drehmoment bleiben im Modul Maschinen.
- **Motor am Frequenzumrichter** (2026-11-26). Ein Drehstrommotor (`supply` 3~), dessen Klemmen U, V, W an den
  Ausgangsklemmen des Frequenzumrichters `sw.drive.vfd` der Schaltanlage angeschlossen sind (Leitungen beliebiger
  Module), übernimmt die U/f-Ausgangsspannung des Umrichters als *U* und gibt, wenn auf einer Seite eine Leistung
  bekannt ist, seine elektrische Leistung *P* als Ausgangsleistung des Umrichters (und seinen Leiterstrom zurück); der
  Eingangsstrom des Umrichters zählt dann in den Stromkreissummen der Schaltanlage (Schaltanlagenmodul, „Werte-Port an
  den Motorklemmen“). Der Motor findet den Umrichter auch selbst (über seine Typ-ID, Relation `elec.motor.drive.u`),
  sodass schon sein Werte-Etikett die Umrichterspannung zeigt. Der Motor hat keine Frequenz- oder Drehzahlwerte; für die
  Synchrondrehzahl einen Motor des Moduls Maschinen verwenden.
- **Spannungsfall auf Leitungen.** Die Leitungsführung der Hausinstallation hat optionale Leitungswerte (Tabelle oben):
  aus Länge, Querschnitt und Strom den Leiterwiderstand, den Spannungsfall mit cos *φ* (Wechselstrom 2·*R*·*I*·cos *φ*,
  Drehstrom √3·*R*·*I*·cos *φ*, Gleichstrom 2·*R*·*I*; Leitungsreaktanz vernachlässigt), den relativen Spannungsfall und
  die Verluste. Drei von *l*, *A*, *I* und Δ*u* (mit *U*_n) ergeben den vierten, z. B. den Querschnitt für einen
  zulässigen Spannungsfall. Der Strom wird eingegeben (er wird nicht aus dem Stromkreis übernommen). Den zulässigen
  Spannungsfall Δ*u*_max (`dumax`) gibt der Benutzer vor; die Grenzen der IEC 60364-5-52 und nationaler Regeln werden
  nicht übernommen [S8]. 20 m Kupfer 1,5 mm² bei 16 A und 230 V ergeben Δ*U* = 7,4 V, Δ*u* = 3,2 %.

**Prüfungen** (nur Hinweise, Overlay, ändern nie etwas; Katalog im Konzept, Abschnitt 4):

| Id | Element | Regel | Schwere | Quelle |
|---|---|---|---|---|
| `elec.r.power` | Widerstand | *P* ≤ *P*_rated (`prated`) | Warnung | [S7] |
| `elec.led.current` | LED | *I*_F ≤ *I*_F,max (`ifmax`, Datenblatt) | Warnung | [S7] |
| `elec.coord.overload` | Sicherung, LS-Schalter, Abgang | *I*_B ≤ *I*_n ≤ *I*_z (*I*_z vom Benutzer eingegeben, keine Belastbarkeitstabellen) | Warnung | [S9] |
| `elec.rcd.additional` | Steckdose | ihr Stromkreis hat ein Überstromschutzorgan, aber keinen RCD mit *I*_Δn ≤ 30 mA (ein RCD ohne Angabe zählt als vorhanden; nationale Regeln weichen ab) | Hinweis | [S10] |
| `elec.net.reducible` | Elemente in einer nicht reduzierbaren Masche | kein Reihen-/Parallelnetz | Info | Abschnitt 1.6 |
| `elec.opamp.saturation` | Operationsverstärker | \|*U*_a\| ≤ *U*_B (der Ausgang kann nicht über die Versorgung hinaus) | Warnung | [S7] |
| `elec.bjt.overdrive` | Bipolartransistor | *k* = *I*_B·*B*/*I*_C ≥ 1 (sonst schaltet er nicht voll durch) | Warnung | [S7] |
| `elec.cable.drop` | Leitungsführung | Δ*u* ≤ Δ*u*_max (`dumax`, vom Benutzer eingegeben) | Hinweis | [S8] |
| `elec.ac.resonance` | Kondensator eines Schwingkreises | die Betriebsfrequenz liegt in der Bandbreite um *f*₀ (\|*f* − *f*₀\| ≤ *B*/2): Spannungen (Reihe) oder Ströme (parallel) an *L* und *C* weit über denen der Quelle | Info | [S7] |

## Überschneidungen mit anderen Modulen (ein Element, ein Eintrag)

Die Installationsplansymbole (`elec.house.socket`, `.socket.double`, `.switch`, `.switch.twoway`, `.switch.push`,
`.light`) sind die maßgeblichen Raumgeräte des Katalogs: die Raumgeräte des Verteilermoduls (`sw.house.*`) treten für
sie zurück, solange dieses Modul registriert ist (`LibraryItem.Alternative`, Zuordnung in der Doku des Verteilermoduls).
Ihre Einträge tragen die Namen des Verteilermoduls als Suchwörter („Zweifachsteckdose“, „Lichtschalter“,
„Deckenauslass“, „light outlet“), die Suche findet sie also auch so. Die Geräteanschlussdose kommt aus der TGA
(`bs.elec.appliance`).

Wie die Raumgeräte tragen sie die optionalen Parameter `room` (**Raum …**) und `feed` (**Stromkreis …**, das Kennzeichen
des speisenden Schutzorgans, z. B. „-F3“, klein oben links am Installationsplansymbol); Steckdose und Leuchte zusätzlich
`phase` (vom Stromkreis / L1 / L2 / L3). Die Berichte (Hausinstallationsliste, Stromkreisplan) und Stromkreiswerte des
Verteilermoduls lesen sie: die an einer Steckdose oder Leuchte eingetragene Leistung oder der Strom zählt im
Betriebsstrom I_B des in `feed` genannten (oder angeschlossenen) Leitungsschutzschalters, mit 230 V, wenn keine Spannung
eingetragen ist. Die Schalterarten entsprechen dem Lichtschalter des Verteilermoduls: Ausschalter, Wechselschalter,
Kreuzschalter (alle vier Hebel), Taster, Dimmer (Stellkeil; im Schnitt ein Drehknopf); die Auswahl der Art zeigt das
Installationsplansymbol jeder Art als Piktogramm.

## Persistenz

Der gesamte Zustand liegt im kanonischen Dokument (`type`, `view`, `params`, `points`, `attach`, `text`); es gibt keinen
versteckten Modulzustand. Ein JSON-Round-Trip reproduziert Dokument und gerendertes SVG exakt (getestet).

## Grenzen und nächste Schritte

- Ein an einer anderen Leitung angedocktes Leitungsende (T-Abzweig) wird mit einem Verbindungspunkt gezeichnet, und eine
  über eine frühere Leitung gezogene Leitung überspringt sie an der Kreuzung mit einer Brücke (`hops`, standardmäßig an,
  Aktion `elec.wire.hops`); sich nur kreuzende Leitungen werden nie verbunden. Die Regelprüfung kennt keine Pin-Typen
  (Ausgang gegen Ausgang, unversorgte Netze).
- Ein an einer anderen Leitung oder einem Bus (deren Schiene) angedocktes Leitungsende hängt an dieser Schiene
  (Abzweig): Es folgt, wenn die andere Leitung verschoben oder geändert wird, und die Netzliste fasst beide Leitungen
  zu einem Netz zusammen.
- Beschriftungen liegen außerhalb der Objektbox; Auswahlrahmen der Hosts schließen sie nicht ein.
- Werte (Calc 3, Runden 5–6): Die Wechselstromberechnung umfasst Reihen-Parallel-Netze aus R, L und C an einer
  Wechselspannungsquelle; eine Wechselstromlast mit weiteren Elementen (Dioden, Motoren, Transformator-Primärseiten)
  erhält keine Zeigerwerte, und drei Impedanzen in Stern oder Dreieck werden nicht umgerechnet (Y–Δ; das Modul hat kein
  Stern-/Dreieck-Netzelement, und die Umrechnung würde Brücken reduzierbar machen). Keine Ersatzinduktivität; keine
  Polarität (nur Beträge, die Verstärkung des Operationsverstärkers behält ihr Vorzeichen). Operationsverstärker und
  Transistor lesen nur eindeutig verdrahtete Widerstände (ein Rückkopplungs-, ein Eingangswiderstand, ein Basiswiderstand
  allein am Basisnetz). Zwei widersprüchliche Kennzeichnungen (z. B. eine 24-V-Lampe an einer 12-V-Sekundärwicklung) werden noch nicht als
  Widerspruch gemeldet, weil ein aus einer Kennzeichnung gelesener Wert bei der Stimmigkeitsprüfung des Lösers nicht als
  eingegeben zählt (Core-Änderung vorgeschlagen). Die Leitungsführung übernimmt ihren Strom nicht aus dem
  Stromkreis, Leitungsreaktanz und Leitertemperatur werden nicht abgebildet (für einen warmen Leiter *ρ* eingeben).
- Sichtprüfung: `dotnet test` schreibt eine SVG-Galerie nach
  `tests/InventaSketch.Modules.Electrical.Tests/bin/Debug/net10.0/gallery/index.html` (Ports als rote Kreise, dazu ein
  Beispielschaltkreis mit angedockten Leitungen).
