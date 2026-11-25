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
| `elec.polarity`/`elec.channel` | `Swap` | NPN ↔ PNP, N- ↔ P-Kanal |
| `elec.<Auswahl>=<Wert>` | `Option` | Auswahlen (Dropdowns): Gatterfunktion, Kontaktart, Erdungszeichen, Quellenart, Stromart, Stecker/Buchse, Gehäuse, Busprotokoll, … |
| `elec.route=free/orthogonal/45` | Führungs-Piktogramme | Leitungs-/Leiterbahnführung als Auswahl |
| `elec.label` | `Text` | Kennzeichen/Wert anzeigen (setzt `ref` = Präfix + `1`, falls leer) oder ausblenden |
| `elec.envelope` | `Toggle` | Gehäusekreis bei Transistoren |

Werte werden im Inspektor bearbeitet (`value`, `ref`); Kontextaktionen können noch keinen Text abfragen.

## Objekttypen

| Kategorie | Typen (`elec.*`) |
|---|---|
| Passive Bauelemente | `resistor` (IEC-Rechteck / ANSI-Zickzack), `potentiometer`, `capacitor`, `capacitor.polarized`, `inductor` (`core`: ohne/Eisen/Ferrit), `transformer` (Spulen / Kreise, `core`) |
| Halbleiter | `diode`, `diode.led`, `diode.zener`, `diode.photo`, `transistor.bjt` (`polarity` npn/pnp), `transistor.mosfet` (`channel` n/p, Anreicherungstyp), `transistor.igbt`, `thyristor`; optionaler Gehäusekreis `envelope` |
| Schalten & Schützen | `switch` (`contact` Schließer/Öffner/Wechsler; auch für Relaiskontakte), `pushbutton`, `relay.coil`, `contactor` (1–3 Pole), `fuse`, `breaker`, `rcd` |
| Quellen & Verbraucher | `ground` (Erde/PE/Gehäuse), `source` (DC/AC/Strom), `battery` (1–4 Zellen), `lamp`, `motor` und `generator` (`supply` ohne/DC/AC/3~; 3~ mit U/V/W), `sensor`, `actuator` (optional `label`), `speaker`, `microphone`, `antenna` |
| Logik & ICs | `gate` (`gate` and/or/not/nand/nor/xor/xnor/buffer, `inputs` 2–4; IEC-Rechteck und ANSI-Form), `flipflop` (D/JK/RS/T), `opamp` (IEC / Dreieck), `block.ecu` (Steuergerät/IC/MCU-Block mit `pinsLeft`/`pinsRight` und `label`) |
| Leitungen, Steckverbinder & Busse | `wire` (Punktobjekt, `route` frei/orthogonal/45), `junction`, `netlabel` (Text = Netzname), `connector` (Stecker/Buchse/Paar, 1–12 Pole), `terminal`, `testpoint`, `bus` (Punktobjekt; Beschriftung CAN/LIN/Ethernet/FlexRay/allgemein) |
| Hausinstallation | `house.socket` (1–4 Steckdosen, Schutzkontakt), `house.switch` (Aus-/Wechselschalter/Taster), `house.light`, `house.junction`, `house.board`, `house.cable` (Punktobjekt mit Adernstrichen), `sld.busbar`, `sld.feeder` (Leistungsschalter/Sicherung/ohne), `sld.meter` |
| Leistungselektronik | `pe.inverter`, `pe.rectifier` (AC-Seite 1- oder 3-phasig), `pe.dcdc`, `pe.dclink`, `pe.gatedriver`, `pe.module` (Halbbrücke mit Freilaufdioden) |
| Leiterplatte (illustrativ) | `pcb.board`, `pcb.pad` (rechteckig/rund/langrund, THT), `pcb.via`, `pcb.track` (Punktobjekt, `width`, Führung frei/orthogonal/45), `pcb.copper`, `pcb.footprint` (SOIC-8, QFP-32, SOT-23, 0603, DIP-8; Pads sind Ports), `pcb.hole`, `pcb.silk` |

Die Installationsplan-Symbole orientieren sich sinngemäß an üblicher DIN/EN-Praxis; ihr genaues Aussehen unterscheidet
sich je nach Land und ist nicht normativ. Leiterplatten-Objekte sind proportional vereinfacht und enthalten keine
Land-Pattern- oder Designregel-Daten.

## Elementwerte (Calc 1)

Der Widerstand (`elec.resistor`) hat die Werte *R*, *U*, *I* und *P* (technische Berechnungen, siehe
[../design/engineering-calculations.md](../design/engineering-calculations.md), Abschnitt 10). *R* wird aus dem vorhandenen
Text `value` gelesen („4k7", „10 kΩ", „100R"); *U*, *I* und *P* sind optionale Textparameter `voltage`, `current` und
`power`, die das Getippte behalten („230 V"). Beziehungen: *U* = *R*·*I* und *P* = *U*·*I* = *I*²·*R* = *U*²/*R* [S7]. Zwei
beliebige Werte ergeben die anderen beiden; drei stimmige werden als geprüft markiert, drei widersprüchliche ergeben einen
Widerspruch, und nichts wird geändert. Ein Text in `value`, der kein Widerstand ist (z. B. „1/4 W"), wird für *R* nicht
verwendet. Die Engine-Methoden `values`, `setValue`, `clearValue`, `explain` und `dragValue` arbeiten damit; die
Oberfläche folgt mit Calc 2. Andere Schaltzeichen bekommen Werte mit Calc 3.

## Persistenz

Der gesamte Zustand liegt im kanonischen Dokument (`type`, `view`, `params`, `points`, `attach`, `text`); es gibt keinen
versteckten Modulzustand. Ein JSON-Round-Trip reproduziert Dokument und gerendertes SVG exakt (getestet).

## Grenzen und nächste Schritte

- Noch keine automatischen Verbindungspunkte und keine Kreuzungsbögen; die Regelprüfung kennt keine Pin-Typen (Ausgang
  gegen Ausgang, unversorgte Netze).
- Ein an einer anderen Leitung oder einem Bus (deren Schiene) angedocktes Leitungsende hängt an dieser Schiene
  (Abzweig): Es folgt, wenn die andere Leitung verschoben oder geändert wird, und die Netzliste fasst beide Leitungen
  zu einem Netz zusammen.
- Beschriftungen liegen außerhalb der Objektbox; Auswahlrahmen der Hosts schließen sie nicht ein.
- Sichtprüfung: `dotnet test` schreibt eine SVG-Galerie nach
  `tests/InventaSketch.Modules.Electrical.Tests/bin/Debug/net10.0/gallery/index.html` (Ports als rote Kreise, dazu ein
  Beispielschaltkreis mit angedockten Leitungen).
