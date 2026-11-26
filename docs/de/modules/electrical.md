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

## Kontextaktionen

| Aktions-ID | Icon | Wirkung |
|---|---|---|
| `view.<id>` | View-Icon | Darstellung wechseln (IEC/ANSI, Spulen/Kreise, …) |
| `elec.toggle` | `Toggle` | Schaltzustand offen/geschlossen (`closed`; Öffner standardmäßig geschlossen) – Schalter, Taster, Schütz, LS-Schalter, RCD |
| `elec.polarity` | `Swap` | Polarität umkehren (`reverse`: spiegelt Symbol und Anschlüsse) – Dioden, Thyristor, Elko, Quelle, Batterie |
| `elec.polarity`/`elec.channel` | `Swap` | NPN ↔ PNP, N- ↔ P-Kanal (MOSFET) |
| `elec.<Auswahl>=<Wert>` | `Option` | Auswahlen (Dropdowns): Gatterfunktion, Kontaktart, Erdungszeichen, Quellenart, Stromart, Stecker/Buchse, Gehäuse, Busprotokoll, Thermistor NTC/PTC (`elec.thermistor.kind`), TVS-Richtung (`elec.tvs.direction`), JFET-Kanal (`elec.jfet.channel`), … |
| `elec.meter.quantity=<v/a/w/ohm>` | Options-Piktogramme (`Preview`: Messgerätekreis mit Buchstaben) | Messgröße des Messgeräts |
| `elec.wire.hops` | `Hop` | Brücken einer Leitung an Kreuzungen an/aus |
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
| Leitungen, Steckverbinder & Busse | `wire` (Punktobjekt, `route` frei/orthogonal/45), `junction`, `netlabel` (Text = Netzname), `connector` (Stecker/Buchse/Paar, 1–12 Pole), `terminal`, `testpoint`, `bus` (Punktobjekt; Beschriftung CAN/LIN/Ethernet/FlexRay/allgemein) |
| Hausinstallation | `house.socket` (1–4 Steckdosen, Schutzkontakt), `house.switch` (Aus-/Wechselschalter/Taster), `house.light`, `house.junction`, `house.board`, `house.cable` (Punktobjekt mit Adernstrichen), `sld.busbar`, `sld.feeder` (Leistungsschalter/Sicherung/ohne), `sld.meter` |
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
| Kondensator `elec.capacitor`, `elec.capacitor.polarized` | *C* (`value`, „100n"), *U*, *Q* (`charge`), *E* (`energy`) | *Q* = *C*·*U*; *E* = ½*C*·*U*² = ½*Q*·*U* = *Q*²/(2*C*) |
| Spule `elec.inductor` | *L* (`value`, „10 mH"), *I*, *E* (`energy`) | *E* = ½*L*·*I*² |
| Diode, LED, Schottky-Diode `elec.diode`, `elec.diode.led`, `elec.diode.schottky` | *U*_F (`uf`), *I*_F (`if`), *P* (`power`); Kennzeichnung „2,1 V 20 mA"; LED: `ifmax` für die Prüfung | *P* = *U*·*I* |
| Z-Diode `elec.diode.zener` | *U*_Z (`uz`), *I* (`current`), *P* | *P* = *U*·*I* |
| Quelle `elec.source`, Batterie `elec.battery` | *U*, *I*, *P*; Kennzeichnung „5 V", „10 mA" (Stromquelle), „12 V 2 A" | *P* = *U*·*I* |
| Spannungsregler `elec.regulator` | *U*_out (`uout`), *U*_in (`uin`), *I*_out (`current`), *P* (`power`, Verlust); eine Spannung in `value` („LM7805 5 V") ist *U*_out | *P* = (*U*_in − *U*_out)·*I*_out (Linearregler, Eigenstromverbrauch vernachlässigt) |
| Transformator `elec.transformer` | *U*₁ (`u1`), *U*₂ (`u2`), *I*₁ (`i1`), *I*₂ (`i2`), *ü* (`ratio`, Panel); Kennzeichnung „230/12 V" | ideal: *ü* = *U*₁/*U*₂ = *I*₂/*I*₁ [S7, S15] |
| Sicherung, LS-Schalter, Abgang `elec.fuse`, `elec.breaker`, `elec.sld.feeder` | *I*_B (`current`), *I*_n (`rated`); Kennzeichnung „B16", „C 10 A", „gG 35 A"; `iz` (*I*_z der Leitung, Benutzer) für die Prüfung | – |
| RCD `elec.rcd` | *I*_n (`rated`), *I*_Δn (`idn`); Kennzeichnung „40 A 30 mA", „40/0,03 A", „30 mA" | – |
| Steckdose, Leuchte `elec.house.socket`, `elec.house.light` | *U*, *I*, *P* | *P* = *U*·*I* |

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
Elemente ohne Werte im Stromkreis (Motor, Generator, Relaisspule, Hubmagnet, Fotodiode, Diac, Leistungsmesser) halten ihn
reduzierbar, aber kein Wert geht durch sie hindurch; Drehstrommaschinen, Transistoren, Triacs, Optokoppler,
Brückengleichrichter, Spannungsregler, ICs und andere mehrpolige Teile sind Anschlüsse mit unbekanntem
Strom, sodass das Netz um sie herum nicht reduziert wird.

**Prüfungen** (nur Hinweise, Overlay, ändern nie etwas; Katalog im Konzept, Abschnitt 4):

| Id | Element | Regel | Schwere | Quelle |
|---|---|---|---|---|
| `elec.r.power` | Widerstand | *P* ≤ *P*_rated (`prated`) | Warnung | [S7] |
| `elec.led.current` | LED | *I*_F ≤ *I*_F,max (`ifmax`, Datenblatt) | Warnung | [S7] |
| `elec.coord.overload` | Sicherung, LS-Schalter, Abgang | *I*_B ≤ *I*_n ≤ *I*_z (*I*_z vom Benutzer eingegeben, keine Belastbarkeitstabellen) | Warnung | [S9] |
| `elec.rcd.additional` | Steckdose | ihr Stromkreis hat ein Überstromschutzorgan, aber keinen RCD mit *I*_Δn ≤ 30 mA (ein RCD ohne Angabe zählt als vorhanden; nationale Regeln weichen ab) | Hinweis | [S10] |
| `elec.net.reducible` | Elemente in einer nicht reduzierbaren Masche | kein Reihen-/Parallelnetz | Info | Abschnitt 1.6 |

## Überschneidungen mit anderen Modulen (ein Element, ein Eintrag)

Die Installationsplansymbole (`elec.house.socket`, `.socket.double`, `.switch`, `.switch.twoway`, `.switch.push`,
`.light`) sind die maßgeblichen Raumgeräte des Katalogs: die Raumgeräte des Verteilermoduls (`sw.house.*`) treten für
sie zurück, solange dieses Modul registriert ist (`LibraryItem.Alternative`, Zuordnung in der Doku des Verteilermoduls).
Ihre Einträge tragen die Namen des Verteilermoduls als Suchwörter („Zweifachsteckdose“, „Lichtschalter“,
„Deckenauslass“, „light outlet“), die Suche findet sie also auch so. Die Geräteanschlussdose kommt aus der TGA
(`bs.elec.appliance`).

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
- Werte (Calc 3): noch keine Wechselstrom-Impedanz, Blindwiderstände, Zeitkonstanten oder Resonanz (keine
  Frequenzvariable); keine Ersatzinduktivität; keine Polarität (nur Beträge); Werte von Motor und Generator bleiben dem
  Modul Maschinen überlassen; Potentiometer, Basiswiderstand des Transistors und Verstärkung des Operationsverstärkers sind
  offen. Zwei widersprüchliche Kennzeichnungen (z. B. eine 24-V-Lampe an einer 12-V-Sekundärwicklung) werden noch nicht als
  Widerspruch gemeldet, weil ein aus einer Kennzeichnung gelesener Wert bei der Stimmigkeitsprüfung des Lösers nicht als
  eingegeben zählt (Core-Änderung vorgeschlagen). Der Spannungsfall der Leitung (`elec.cable.drop`) wartet auf
  Leitungswerte.
- Sichtprüfung: `dotnet test` schreibt eine SVG-Galerie nach
  `tests/InventaSketch.Modules.Electrical.Tests/bin/Debug/net10.0/gallery/index.html` (Ports als rote Kreise, dazu ein
  Beispielschaltkreis mit angedockten Leitungen).
