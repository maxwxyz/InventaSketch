# Modul Verteiler & Installation (`switchgear`)

Bibliothek: `src/InventaSketch.Modules.Switchgear` · Tests: `tests/InventaSketch.Modules.Switchgear.Tests` ·
Englische Fassung (maßgeblich): [../../en/modules/switchgear.md](../../en/modules/switchgear.md) · Roadmap: Punkt 13
„Elektroinstallation und Schaltanlagen“.

Das Modul zeichnet **Verteiler und Schaltschränke auf Hutschienen TS35** und die **Installationsgeräte in den Räumen**,
die sie versorgen: Reiheneinbaugeräte mit Frontansichten, die wie die echten Geräte aussehen (Schaltknebel, Bemessung
„B16“, „30 mA“, Prüftaste, Schraubklemmen), Industriegeräte (Motorschutzschalter, Überlastrelais,
NH-Sicherungslasttrenner, Frequenzumrichter, SPS-Baugruppen, Sicherheitsrelais), Schaltzeichen-Ansichten, Reihenklemmen
mit Brücken, Schienen, Phasenschienen, Abdeckstreifen, Kanäle, Endhalter und Beschriftung, Adern und Kabel. Kennzeichen
und Bemessungswerte sind optionale Parameter und erscheinen auf den Geräten; fünf **Installationsberichte** (CSV und
Markdown) entstehen aus der Folie. Optionale **Elementwerte** summieren die Verbraucher je Stromkreis und je
Außenleiter und prüfen einige Plausibilitätsregeln (siehe „Werte und Prüfungen“).

Es ist ein Modul zum **Skizzieren und Dokumentieren**: Es summiert eingegebene Lasten, bemisst aber keine Leitungen,
prüft keine Selektivität oder Schutzmaßnahmen und beansprucht keine Normkonformität. Die Frontansichten sind eigenständig nach dem üblichen Aussehen
von Reiheneinbaugeräten gezeichnet; keine Herstellergestaltung und keine Normgrafik wird übernommen. Das Modul zeichnet
alles selbst und hängt von keinem anderen Modul ab (die Schaltzeichen sind eigene, nicht die des Elektromoduls).

## Konventionen

- **Schnellskizze zuerst:** Jedes Bibliothekselement lässt sich ohne Parameter einfügen. Freies (gleich- oder
  ungleichmäßiges) Skalieren ändert nur `Size`, nie Parameter.
- **Teilungseinheiten:** 1 TE = 17,5 mm. Jedes Reiheneinbaugerät ist eine ganze Zahl von TE breit (1, 2, 3, 4, 8, 12)
  und 90 mm hoch; die Breite folgt Polzahl, Kanälen oder Leistung. Ändert sich ein solcher Parameter, wächst der Rahmen
  im gleichen Maßstab und **behält seine linke Kante** – das Gerät bleibt auf seinem Platz auf der Schiene.
- **Schiene und Einrasten:** Die Hutschiene TS35 (35 mm) hat `Mount`-Anker im TE-Raster auf ihrer Mittellinie (das Raster
  ist auf der Schiene zentriert; sein Abstand ist die halbe Schienenhöhe, eine skalierte Verteilung behält ihr Raster).
  Geräte und Klemmen haben `Mount`-Anker an der linken und rechten Kante auf ihrer Mittellinie: Sie rasten im Raster der
  Schiene und nebeneinander ein.
- **Anschlüsse:** Jeder Anschlusspunkt ist ein `Port`-Anker mit Medium: Schraubklemmen `electric` (Verbinder: Ader
  `sw.line.wire`), Busklemmen `data` (Verbinder: Buskabel `sw.line.cable.knx`), Raumgeräte `electric.installation`
  (Verbinder: Kabel `sw.line.cable`). Ader- und Kabelenden docken dort an und folgen dem Gerät; „+“ baut weiter.
  Anschlussnamen folgen den üblichen Klemmenbezeichnungen: Pole `1/2`, `3/4`, `5/6`, Neutralleiter `N1` (oben) / `N2`
  (unten), Spulen `A1/A2`, Wechsler `11/12/14`, Zeitrelais `15/16/18`, Überspannungsschutz `L1…L3`, `N`, `PE`, Bus
  `bus`, Kanäle der Busaktoren `A1/A2`, `B1/B2` …, Klemmen `1/2` (Etagen `3/4`, `5/6`); Motorschutzschalter,
  Überlastrelais und NH-Lasttrenner `1/2`, `3/4`, `5/6` (Hilfskontakte des Überlastrelais `95/96` Öffner, `97/98`
  Schließer), Frequenzumrichter `L1…L3` (einphasig `L`, `N`) → `U`, `V`, `W`, SPS-Baugruppen `L+`, `M` und die Kanäle
  `I0…`, `Q0…`, `AI0…`, `AQ0…` (CPU: Netzwerk `X1`, Medium `data`), Sicherheitsrelais `A1/A2`, `S11/S12`, `S21/S22`,
  `13/14`, `23/24`, Zähne der Phasenschiene `t1…tn` (an ihrer Unterkante in TE-Mitte, wo die oberen Anschlüsse
  einteiliger Geräte liegen).
- **Ansichten:** Reiheneinbaugeräte und Klemmen haben eine *Frontansicht* (Standard) und eine *Schaltzeichen*-Ansicht
  (Kontakte, Spulen, Klemmen nach Art von IEC 60617, unbetätigt gezeichnet, mit gestrichelter Wirkverbindung,
  Summenstromwandler und IΔ-Auslöser, Kennzeichen und Bemessung rechts). Beide Ansichten haben dieselben Anschlussnamen;
  der Wechsel behält den optischen Maßstab.
- **Kennzeichen** folgen den Ideen von IEC 81346 (Kennbuchstaben nach IEC 81346-2): `-F` Schützen (LS, FI, FI/LS, SPD,
  Überlastrelais, NH-Lasttrenner), `-Q` Energie schalten (Hauptschalter, Schütz, Motorschutzschalter), `-K` Signale
  verarbeiten (Relais, Zeitrelais, Busgeräte, SPS-Baugruppen, Sicherheitsrelais), `-P` Darstellen (Zähler,
  Leuchtmelder), `-T` Umformen (Netzteile, Frequenzumrichter), `-S` Handbetätigung (Taster, Schalter), `-X` Verbinden
  (Klemmen, Steckdosen), `-W` Kabel. Sie sind freier Text; „Kennzeichen …“ schlägt den Kennbuchstaben vor.
  **Gleiche Kennzeichen gehören zusammen** (ein Knoten im Stromkreisplan, ein Kabel in der Kabelliste).
- **Auswahlen sind Dropdowns** (`ActionChoices.FromParam`): Pole, Charakteristik, Bemessungsstrom, Phase, Fehlerstrom,
  FI-Typ, Spulenspannung, Kontakte, Funktion, Farbe, Querschnitt … Die Knebelstellung ist ein Umschalter („Eingeschaltet“).

## Objekttypen

| Typ-Id | Element (DE / EN) | Parameter (alle optional) |
|---|---|---|
| `sw.protection.mcb` | Leitungsschutzschalter / Miniature circuit breaker | `char` B/C/D, `rating` 6–63 A, `poles` 1, 1+N, 2, 3, 3+N, `phase` L1/L2/L3 (nicht bei 3 Polen), `on` |
| `sw.protection.rcd` | Fehlerstromschutzschalter / Residual current device | `poles` 2/4, `rating` 25–100 A, `idn` 10–500 mA, `rcdType` AC/A/F/B, `on` |
| `sw.protection.rcbo` | FI/LS-Schalter / RCBO | `poles` 1+N, 3+N, `char`, `rating`, `idn`, `rcdType`, `phase` (1+N), `on` |
| `sw.protection.spd` | Überspannungsschutz / Surge protective device | `spdType` 1, 1+2, 2, 3, `poles` 1, 1+N, 3, 3+N |
| `sw.switch.main` | Hauptschalter (Lasttrennschalter) / Main switch | `poles` 1–4, `rating` 32–125 A, `on` |
| `sw.protection.mpcb` | Motorschutzschalter / Motor protection circuit breaker | `rating` Einstellstrom 0,4–32 A, `on` (Drehgriff I/O); 3 TE |
| `sw.protection.overload` | Überlastrelais (thermisch) / Thermal overload relay | `rating` Einstellstrom 0,4–32 A; 2 TE |
| `sw.fuse.nh` | NH-Sicherungslasttrenner / NH fuse switch-disconnector | `size` NH000/NH00, `rating` 16–160 A (gG), `on` (Deckel zu/offen); 6 TE |
| `sw.drive.vfd` | Frequenzumrichter / Frequency inverter | `power` 0,37–7,5 kW (4–6 TE), `supply` dreiphasig 400 V / einphasig 230 V |
| `sw.plc.module` | SPS-Baugruppe / PLC module | `kind` CPU, Digitaleingänge, Digitalausgänge, Analogeingänge, Analogausgänge, `channels` 8/16 (2 oder 4 TE; CPU 4) |
| `sw.safety.relay` | Sicherheitsrelais / Safety relay | `function` Not-Halt, Schutztür, Lichtvorhang (Piktogramm), `coil` 24 V DC / 230 V AC; 2 TE |
| `sw.busbar.comb` | Phasenschiene (Kammschiene) / Comb busbar | `phases` 1, 1+N, 3, 3+N (Zahnbeschriftung L1 L2 L3 …), `positions` 2–24 TE, `section` 10/16 mm² |
| `sw.blank` | Abdeckstreifen / Blanking plate | `modules` 1–24 (45 mm hoch, der Ausschnitt der Abdeckung) |
| `sw.meter.energy` | Energiezähler / Energy meter | `phases` 1/3 |
| `sw.supply.psu` | Hutschienennetzteil / DIN-rail power supply | `voltage` 5–48 V, `power` 15–120 W (1–4 TE) |
| `sw.switching.contactor` | Installationsschütz / Installation contactor | `contacts` 2 S, 4 S, 2 S + 2 Ö, `coil` |
| `sw.switching.relay` | Koppelrelais / Coupling relay | `contacts` 1/2 Wechsler, `coil` |
| `sw.switching.impulse` | Stromstoßschalter / Impulse relay | `contacts` 1/2 Schließer, `coil` |
| `sw.switching.timer` | Zeitrelais / Timer relay | `function` ansprechverzögert, rückfallverzögert, Treppenlicht, Blinker, `time`, `coil` |
| `sw.control.pushbutton` | Taster für Hutschiene / Push button for DIN rail | `contact` Schließer/Öffner, `colour` |
| `sw.control.switch` | Schalter für Hutschiene / Switch for DIN rail | `kind` 0-I / I-0-II, `on` |
| `sw.control.lamp` | Leuchtmelder / Indicator lamp | `colour` |
| `sw.socket.dinrail` | Steckdose für Hutschiene / Socket outlet for DIN rail | – |
| `sw.bus.actuator` | Busaktor / Bus actuator | `bus` KNX/allgemein, `function` Schalten, Dimmen, Jalousie, Binäreingang, `channels` 1–12 |
| `sw.bus.coupler` | Buskoppler / Bus coupler | `bus` |
| `sw.bus.power` | Busspannungsversorgung / Bus power supply | `bus`, `current` 160/320/640 mA |
| `sw.terminal.feedthrough` | Durchgangsreihenklemme / Feed-through terminal block | `section` 1,5–16 mm² (Breite 4,2–12,2 mm), `number`, `target`, `bridge` |
| `sw.terminal.n` | N-Reihenklemme (blau) / Neutral terminal | wie Durchgangsklemme |
| `sw.terminal.pe` | Schutzleiterklemme / Protective earth terminal | wie Durchgangsklemme |
| `sw.terminal.fused` | Sicherungsreihenklemme / Fused terminal block | `fuse` 1–10 A, `number`, `target` |
| `sw.terminal.multilevel` | Etagenreihenklemme / Multi-level terminal | `levels` 2/3, `section`, `number`, `target`, `bridge` |
| `sw.terminal.bridge` | Steckbrücke / Plug-in bridge | `positions` 2–10, `section` (Teilung) |
| `sw.rail.ts35` | Hutschiene TS35 / DIN rail TS35 | `ref` (Schienenname) |
| `sw.rail.endclamp` | Endhalter / End clamp | – |
| `sw.duct.slotted` | Verdrahtungskanal / Slotted cable duct | – (senkrecht, wenn höher als breit) |
| `sw.label.strip` | Beschriftungsstreifen / Labelling strip | `content` Kennzeichen und Stromkreis / Kennzeichen / Stromkreis |
| `sw.label.plate` | Bezeichnungsschild / Label plate | freier Text |
| `sw.line.wire` | Ader / Wire (core) | `colour` (Kurzzeichen nach IEC 60757: BK, BN, GY, BU, GNYE, RD, DBU, WH, VT, OG), `section`, `ref` (Adernummer) |
| `sw.line.cable` | Kabel / Cable | `cableType` NYM-J, NYY-J, H07V-K, H05VV-F, J-Y(ST)Y, Cat, `cores`, `section`, `ref`, `length` (m), `from`, `to`, `iz` (Strombelastbarkeit, A) |
| `sw.house.socket` | Steckdose (Raum/Stromkreis) / Socket outlet | `gangs` 1–3, `room`, `feed`, `ref`, `phase` |
| `sw.house.switch` | Lichtschalter (Raum/Stromkreis) / Light switch | `kind` Aus-, Wechsel-, Kreuzschalter, Taster, Dimmer, `room`, `feed`, `ref`, `phase` |
| `sw.house.light` | Leuchtenauslass (Raum/Stromkreis) / Light outlet | `kind` Decke/Wand, `room`, `feed`, `ref`, `phase` |
| `sw.house.appliance` | Geräteanschluss (Raum/Stromkreis) / Appliance connection | `kind` Herd, Waschmaschine, Trockner, Geschirrspüler, Durchlauferhitzer, Gerät, `room`, `feed`, `ref`, `phase` |

Jedes Reiheneinbaugerät hat außerdem `ref` (Kennzeichen), `circuit` (Stromkreisname, der Beschriftungstext) und `feed`
(Kennzeichen des vorgeschalteten Geräts); Leitungsschutzschalter und FI/LS zusätzlich `room`. Klemmen nutzen `ref` für
das Leistenkennzeichen („-X1“) und `number` für die Klemmennummer. Die Wertparameter (`ib`, `power`, `iz`, `current`,
`voltage` …, siehe „Werte und Prüfungen“) sind optionale Textparameter im erweiterten Teil des Inspektors.

Die Bibliothek bietet 88 Elemente (43 Typen) in acht Kategorien (Schutz- & Trenngeräte; Schalten & Steuern; Zähler,
Netzteile & Busgeräte; Antriebe & Automatisierung; Reihenklemmen; Schienen, Kanäle & Beschriftung; Adern & Kabel;
Installationsgeräte in den Räumen), z. B. „Leitungsschutzschalter B16, 1-polig“, „FI-Schutzschalter 40 A 30 mA,
4-polig“, „Motorschutzschalter 4 A“, „NH00-Sicherungslasttrenner 63 A“, „Frequenzumrichter 0,75 kW, 3~“,
„SPS-Digitaleingabe, 8 Kanäle“, „Phasenschiene 3-phasig (12 TE)“, „Abdeckstreifen (6 TE)“, „KNX-Schaltaktor, 4-fach“,
„Kabel NYM-J 5×2,5“.

## Aktionen

- Auswahlen (Dropdowns) für jeden Auswahlparameter; Pole, Kanäle, Leistung oder Querschnitt ändern die Rahmenbreite
  (linke Kante bleibt).
- **Eingeschaltet** (Umschalter): Knebel oben/unten, Schaltstellungsanzeige rot/grün (LS, FI, FI/LS, Hauptschalter,
  Schalter); Drehgriff senkrecht/waagerecht (Motorschutzschalter); Deckel zu/offen (NH-Lasttrenner).
- **Kennzeichen …** (alle Geräte, Klemmen, Adern, Kabel), **Schienenname …**, **Länge …** (Kabel).
- **Leiste nummerieren** (Klemmen): nummeriert die Klemmen mit demselben Leistenkennzeichen auf der Folie (ohne
  Kennzeichen: die Klemmen derselben Reihe) von links nach rechts mit 1, 2, 3 …
- **Ein Pol mehr / weniger** (Steckbrücke); **Eine TE mehr / weniger** (Phasenschiene, Abdeckstreifen), die Breite
  ändert sich, die linke Kante bleibt.
- **Phase** (Dropdown): L1/L2/L3 bei einpoligen Leitungsschutzschaltern und FI/LS 1+N (bei dreiphasigen Geräten nicht
  angeboten); Vom Stromkreis / L1 / L2 / L3 / Dreiphasig bei Raumgeräten. Jede Option hat ein Piktogramm
  (`IconIds.ForPhase`): drei Sammelschienen-Stummel, von denen die gewählte weiter und nach unten zu einem Pfeil läuft
  (`phase-l1`, `phase-l2`, `phase-l3`), bei dreiphasig alle drei (`phase-3`), das Leitungsschutzschalter-Symbol für
  „Vom Stromkreis“ (`phase-circuit`).
- Raumgeräte: **Raum …** und **Stromkreis …** (Kennzeichen des speisenden Geräts, z. B. „-F3“, am Symbol angezeigt);
  eine am Raumgerät selbst gewählte Phase steht oben rechts („L2“, „3~“).

## Werte und Prüfungen

Das Modul nimmt an den Ingenieurberechnungen teil (Elementwerte im Werte-Bereich, Werteschild, Prüfungen;
`docs/de/design/engineering-calculations.md`). Jeder Wert ist optional, jedes Element lässt sich weiter ohne Werte
einfügen; abgeleitete Werte werden nie gespeichert.

| Element | Variablen (Kartenwerte zuerst) | Parameter |
|---|---|---|
| LS, FI/LS, Motorschutzschalter, Überlastrelais, NH-Lasttrenner | I_n Bemessungsstrom (Motorschutzschalter und Überlastrelais: Einstellstrom I_r), I_B Betriebsstrom des Stromkreises, P Leistung des Stromkreises; I_z Strombelastbarkeit der Leitung, I_a Sofortauslösestrom (LS, FI/LS), I_Δn (FI/LS); nicht beim Überlastrelais: I_k1 und I_k3 an der Verteilung (vom vorgeschalteten Hauptschalter), I_cn Bemessungsschaltvermögen; Engineer (nur lesbar, vom vorgeschalteten Gerät) I_n,up, I_a,up | `rating` (die Bemessungsauswahl selbst), `ib`, `power`, `iz`, `ia`, `idn`, `ik1`, `ik3`, `icn`, `inUp`, `iaUp` |
| FI | I_n, I_B, P; I_Δn | `rating`, `ib`, `power`, `idn` |
| Hauptschalter | I_n, I_B, P, I_k1 Kurzschlussstrom Außenleiter–PE an der Verteilung; I_k3 dreipoliger Kurzschlussstrom, Z_s Schleifenimpedanz, S_r und u_k des Einspeisetransformators, l und A der Zuleitung; Engineer U_n (Vorgabe 400 V), ρ (Kupfer bei 20 °C, Vorgabe 0,017241 Ω·mm²/m), Z_T, Z_L | `rating`, `ib`, `power`, `ik1`, `ik3`, `zs`, `trafoPower`, `uk`, `supplyLength`, `supplySection`, `systemVoltage`, `resistivity`, `zTrafo`, `zLine` |
| Raumgeräte | P Leistung, I Strom je Außenleiter; U Spannung Außenleiter–Neutralleiter (Vorgabe 230 V), cos φ (Vorgabe 1) | `power`, `current`, `voltage`, `powerFactor` |
| Frequenzumrichter | P_N Bemessungsleistung (die Auswahl `power` in kW), f Ausgangsfrequenz (Sollwert), P_out Ausgangsleistung, I Eingangsstrom je Außenleiter; P Eingangsleistung, U Netzspannung Außenleiter–Neutralleiter (Vorgabe 230 V), cos φ Leistungsfaktor am Eingang (Vorgabe 1), η Wirkungsgrad (Vorgabe 100 %: Verluste vernachlässigt), U_out Ausgangsspannung (verkettet), I_out Ausgangsstrom, f_min und f_max Einstellbereich, P_N,M Bemessungsleistung des angeschlossenen Motors (nur lesbar); Ingenieur f_b Eckfrequenz (Vorgabe 50 Hz), U_b Ausgangsspannung bei f_b (Vorgabe 400 V, einphasige Einspeisung 230 V) | `power`, `frequency`, `powerOut`, `current`, `powerIn`, `voltage`, `powerFactor`, `efficiency`, `voltageOut`, `currentOut`, `frequencyMin`, `frequencyMax`, `motorPower`, `baseFrequency`, `baseVoltage` |
| Hutschiene | I_L1, I_L2, I_L3 Strom je Außenleiter, P Leistung der Verteilung; ΔI Schieflast, ΔI_max zulässige Schieflast (Vorgabe 20 A) | `currentL1` … `currentL3`, `power`, `imbalance`, `imbalanceMax` |

- **Verbraucher:** I = P/(U·cos φ) einphasig, I = P/(3·U·cos φ) dreiphasig (symmetrisch, U Außenleiter–Neutralleiter)
  [S7].
- **Frequenzumrichter (2026-11-26, Runde 8):** Netzseite als Verbraucher (I = P/(3·U·cos φ) dreiphasig, P/(U·cos φ)
  einphasig), P_out = η·P [S15], Ausgangsspannung nach einer linearen U/f-Kennlinie U_out = U_b·min(f, f_b)/f_b,
  oberhalb der Eckfrequenz konstant (Feldschwächung; keine Spannungsanhebung bei kleiner Frequenz) [S15]. Die Anzeige
  zeigt den eingegebenen Sollwert („35.0 Hz“; ohne Sollwert 50.0 Hz).
- **Werte-Port an den Motorklemmen:** Das eine Element eines anderen Moduls, das an den Ausgangsklemmen `U`, `V`, `W`
  angeschlossen ist (Adern und Kabel beliebiger Module, von Ende zu Ende und über daran angedockte Linien, und durch
  Reihenklemmen oben ↔ unten; ein anderes Gerät dieses Moduls beendet die Suche), ist der Motor. Er übernimmt f
  (`sw.vfd.port.f`), U_out als Klemmenspannung `U` (`sw.vfd.port.u`) und P_out als elektrische Leistung (`Pel`, sonst
  `P`: die Namensliste von `NetKeys`; `sw.vfd.port.p`) und gibt seinen Leiterstrom (I_out) und seine
  Bemessungsleistung `PN` (P_N,M) zurück. So erhält ein Motorblock oder eine E-Maschine des Maschinenmoduls die
  Synchrondrehzahl n_s = 60·f/p und mit einer Drehzahl den Schlupf aus dem Umrichter (35 Hz, 4 Pole: 1050 min⁻¹), der
  Drehstrommotor `elec.motor` (Stromart 3~) des Elektromoduls Spannung und Leistung. Die Leistung wird nur
  weitergegeben, wenn sie auf einer Seite bekannt ist (Leistung oder Strom am Motor eingegeben, oder Ein-/
  Ausgangsleistung oder Strom am Umrichter); sonst gilt der Umrichter mit seiner Bemessungsleistung, P_out = P_N
  (`sw.vfd.rated`, eine Bemessungsannahme wie bisher), und diese Annahme geht nicht an den Motor. Mehrere Motoren an
  einem Umrichter (Gruppenantrieb) ergeben keinen Port. Das Modul des Motors findet den Umrichter auch selbst an seinen
  Phasenklemmen (über die Typ-ID `sw.drive.vfd`), sodass das Werte-Etikett des Motors allein auch mit Leitungen des
  Elektromoduls die Frequenz (Maschinen) bzw. die Spannung (Elektro) des Umrichters zeigt. Ein Motor, an dem die
  Typenschildspannung eingetragen ist, während der Umrichter unterhalb der Eckfrequenz läuft, zeigt einen Widerspruch:
  die Klemmenspannung ist dort U_out.
- **Summen:** Eingangsleistung und -strom des Umrichters zählen in den Summen von Stromkreis und Schiene, wenn an ihm
  Ein- oder Ausgangsleistung, Strom oder Bemessung eingegeben ist (die Bibliothekseinträge tragen die Bemessung) oder
  am Motor an seinem Ausgang eine Leistung oder ein Strom.
- **Stromkreise:** Der Stromkreis eines Geräts ist sein Teil des Einspeisebaums aus dem Stromkreisplan (dieselben
  Regeln: „Gespeist von“, Verdrahtung, Schienenreihenfolge); ein FI oder Hauptschalter umfasst alles darunter, ein
  Frequenzumrichter ist ein Verbraucher des Geräts, das ihn speist. P = Summe der Verbraucherleistungen; I_B = Summe der
  Verbraucherströme bei einem einphasigen Gerät, die größte Außenleitersumme bei einem dreiphasigen Gerät (ein
  dreiphasiger Verbraucher zählt auf jedem Außenleiter) [S7]. Es zählen nur Verbraucher mit eingegebener Leistung oder
  eingegebenem Strom; ein Stromkreis ohne sie hat kein I_B. Ein Gleichzeitigkeitsfaktor wird nicht angewandt.
- **Sofortauslösung:** I_a = 5·I_n, 10·I_n, 20·I_n für Charakteristik B, C, D (obere Grenze des Bereichs) [S11].
- **Bemessungsstrom:** I_n wird aus der Bemessungsauswahl gelesen („16“ → 16 A); ein im Werte-Bereich eingegebener Wert
  wird als Text in die Auswahl zurückgeschrieben („20“), das Gerät zeigt ihn an.
- **Leitung:** I_z wird am Gerät oder an einem Kabel des Stromkreises eingetragen (Kabelparameter `iz`; ein Kabel an
  einem unteren Anschluss des Geräts oder an einem seiner Verbraucher, der kleinste Wert gilt). Das Modul kennt den
  Querschnitt, leitet I_z aber nicht daraus ab: Die Strombelastbarkeit hängt von Verlegeart, Häufung und
  Umgebungstemperatur ab (Tabellen der IEC 60364-5-52, nicht übernommen).
- **Phasen:** In einem einphasigen Stromkreis entscheidet die `phase` des Geräts; in einem dreiphasigen Stromkreis die
  eigene `phase` des Verbrauchers („Vom Stromkreis“ = dreiphasig); ein Verbraucher ohne gezeichnetes speisendes Gerät
  nutzt seine eigene Wahl („Vom Stromkreis“ = L1). Ein Umrichter folgt seiner Einspeisung (dreiphasig oder die Phase
  seines Stromkreises).
- **Summen der Schiene:** I_Lk = Summe der Ströme der Verbraucher auf Außenleiter k, P = Summe der Leistungen; jeder
  Verbraucher zählt einmal, über das nächstgelegene Gerät auf einer Schiene, das ihn speist; ΔI = größter − kleinster
  Außenleiterstrom.

| Prüfung | Stufe | Regel | Quelle |
|---|---|---|---|
| `sw.load.rating` | Warnung | I_B ≤ I_n (Schutzorgane, FI, Hauptschalter): die Verbraucher ziehen mehr als die Bemessung | S9 |
| `sw.rating.cable` | Warnung | I_n ≤ I_z (Überstromschutzorgane; nur wenn ein I_z bekannt ist) | S9 |
| `sw.phase.imbalance` | Warnung | I_max − I_min ≤ ΔI_max auf einer Schiene; die Vorgabe 20 A ist ein Plausibilitätswert, die Grenze des Netzbetreibers eintragen | – |
| `sw.disconnect` | Warnung | I_k1 an der Verteilung ≥ I_a (LS, FI/LS): ein Fehler Außenleiter–PE löst das Gerät unverzögert aus (automatische Abschaltung, Z_s·I_a ≤ U_0) | S10 |
| `sw.breaking` | Warnung | I_k3 an der Verteilung ≤ I_cn (nur mit eingetragenem I_cn) | S11 |
| `sw.selectivity` | Hinweis | I_n,up ≥ 1,6·I_n gegenüber dem vorgeschalteten Überstromschutzorgan (Faustregel für gG-Sicherungen; ein Hinweis, keine Selektivitätsuntersuchung) | S11 |
| `sw.selectivity.instant` | Hinweis | I_k3 an der Verteilung < I_a,up eines vorgeschalteten LS-Schalters (sonst lösen beide gleichzeitig aus: keine Stromselektivität) | S11 |
| `sw.rcd.type` | Hinweis | FI- bzw. FI/LS-Typ ≥ dem Typ, den ein Frequenzumrichter darunter braucht (dreiphasig B, einphasig F; Reihenfolge AC < A < F < B) | S11 |
| `sw.vfd.rating` | Warnung | P_N,M ≤ P_N und P_out ≤ P_N: der Motor (seine Bemessungsleistung oder seine elektrische Leistung im Betriebspunkt) braucht mehr als die Bemessungsleistung des Umrichters | – |
| `sw.vfd.range` | Warnung | f_min ≤ f ≤ f_max (nur die eingegebenen Grenzen) | – |

**Kurzschluss, Selektivität und FI-Typ (2026-11-26).** Der Hauptschalter trägt eine Schätzung der Kurzschlussströme an
der Verteilung: Z_T = u_k·U_n²/S_r [S7], Z_L = ρ·l/A [S12], Z_s = Z_T + 2·Z_L (Außen- und Schutzleiter mit gleichem
Querschnitt) [S10], I_k1 = U_n/(√3·Z_s) und I_k3 = U_n/(√3·(Z_T + Z_L)) [S7, S10]. Die Impedanzen werden als Beträge
addiert; vorgelagertes Netz, Reaktanzen und Spannungsfaktor c sind vernachlässigt, der Leiter hat 20 °C – eine Schätzung,
keine Kurzschlussberechnung (IEC 60909 ist nicht umgesetzt). Eine gemessene Schleifenimpedanz kann direkt als Z_s
eingetragen werden. Beispiel: 630 kVA, u_k = 4 %, 50 m Kupfer 95 mm² ergeben Z_s = 28,3 mΩ, I_k1 ≈ 8,2 kA und
I_k3 ≈ 12,0 kA. Jedes Überstromschutzorgan unter dem Hauptschalter im Einspeisebaum erhält I_k1 und I_k3 (Werte an der
Verteilung: die Leitung des Stromkreises verringert I_k weiter, das ist nicht modelliert) sowie Bemessungs- und
Sofortauslösestrom des nächsten Überstromschutzorgans darüber (kein FI, Hauptschalter oder Überlastrelais). Ein FI oder
FI/LS prüft die Frequenzumrichter darunter. Bibliothekseinträge: `sw.switch.main.supply` (Hauptschalter mit
Kurzschluss-Suchwörtern), `sw.protection.rcd.f` (Typ F).

## Beschriftungsstreifen

`sw.label.strip` liest die Geräte der nächstgelegenen Reihe (darüber oder darunter, bis 120 mm entfernt, Mitte innerhalb
der Streifenlänge) und zeichnet je Gerät ein Feld in Gerätebreite mit Kennzeichen und Stromkreisname (oder der Bemessung,
wenn kein Name angegeben ist). Ohne Geräte in der Nähe zeigt er leere Felder je TE. Geräte melden nahe Streifen als
abhängig; der Streifen wird bei jeder Geräteänderung neu gezeichnet.

## Exporte (`IExportProvider`)

Jeder Bericht gibt es als CSV (`text/csv`, kommagetrennt, bei Bedarf in Anführungszeichen) und als Markdown
(`text/markdown`). Die Engine listet sie mit `exporters` und führt sie mit `export` aus (Ids unten mit `.csv` / `.md`).
Die Daten stammen aus den Parametern der Elemente auf der Folie (oder der Auswahl) und aus den Adern und Kabeln, die an
ihren Anschlüssen angedockt sind.

| Id | Bericht | Inhalt |
|---|---|---|
| `sw.installation` | Hausinstallationsliste | Räume → Stromkreise (Kennzeichen, Name, Schutzorgan und Bemessung) → Geräte mit Mengen. Markdown: je Raum ein Abschnitt mit verschachtelter Liste. |
| `sw.circuits` | Stromkreisplan | Einspeisebaum: Hauptschalter, Zähler, SPD, FI, LS, FI/LS, Motorschutzschalter, Überlastrelais und NH-Lasttrenner nach Kennzeichen, jeweils mit dem speisenden Gerät und **woher diese Zuordnung stammt**; Stromkreise mit Räumen und Geräten (Raumgeräte und Frequenzumrichter). Markdown: verschachtelte Liste und Tabelle. |
| `sw.layout` | Verteilerbelegung und Beschriftungsstreifen | Je Schiene (von oben nach unten, benannt nach `ref` oder „Schiene n“): Geräte von links nach rechts mit Platz, erster TE, Breite in TE, Kennzeichen, Gerät, Bemessung, Beschriftung; belegte / verfügbare TE (Abdeckstreifen zählen als frei); der Beschriftungsstreifen als zweizeilige Tabelle. Zubehör (Endhalter, Brücken, Phasenschienen, Abdeckstreifen) wird nicht aufgeführt. Geräte auf keiner Schiene stehen am Ende. |
| `sw.terminals` | Klemmenplan | Je Leiste die Klemmen in Nummernfolge (natürliche Sortierung), je Etage und Seite (oben/unten): Ader oder Kabel, Ziel (Kennzeichen und Anschluss am anderen Ende, z. B. „-F2:2“), Querschnitt, Farbe, Brücke. Das `target` einer Klemme nennt das äußere Ziel, wenn keine Leitung angedockt ist. |
| `sw.cables` | Kabelliste | Kennzeichen, Typ, Adern × Querschnitt, Länge (m), von, nach, Anzahl Abschnitte. Abschnitte mit gleichem Kennzeichen (z. B. auf mehreren Folien) sind ein Kabel: Längen werden addiert, die äußeren Enden sind „von“ und „nach“. |

So verknüpft der Stromkreisplan die Geräte (in dieser Reihenfolge): (1) der Parameter `feed` („Gespeist von“), Herkunft
„Parameter“; (2) die Verdrahtung: eine Ader oder ein Kabel von einem unteren Anschluss eines Geräts im Einspeisebaum zu
einem oberen Anschluss dieses Geräts (Raumgeräte: beliebiger Anschluss), Herkunft „Verdrahtung“; (3) nur für
Leitungsschutzschalter: der nächste FI links auf derselben Schiene, Herkunft „Schienenreihenfolge (angenommen)“ – der
übliche Aufbau mit Phasenschiene; der Bericht kennzeichnet dies als Annahme. Raumgeräte hängen unter dem Kennzeichen in
ihrem `feed`; ein genanntes, aber nicht gezeichnetes Kennzeichen erscheint als „(nicht gezeichnet)“ – nichts wird
erfunden.

## Überschneidungen mit anderen Modulen (ein Element, ein Eintrag)

Die Installationsgeräte in den Räumen zeichnen dieselben Elemente wie die Installationsplansymbole des Elektromoduls und
die Geräteanschlussdose der TGA. Diese sind die maßgeblichen Einträge (Grundriss-, Schnitt- und Schemaansicht,
Montagehöhe, Anschlüsse und Stromkreiswerte); solange ihr Modul registriert ist, treten die Raumgeräte hier aus Galerien
und Suche zurück (`LibraryItem.Alternative`):

| Eintrag hier | weicht für |
|---|---|
| `sw.house.socket`, `sw.house.socket.double` | `elec.house.socket`, `elec.house.socket.double` |
| `sw.house.switch`, `.twoway`, `.push` | `elec.house.switch`, `.twoway`, `.push` |
| `sw.house.light` | `elec.house.light` |
| `sw.house.appliance`, `sw.house.appliance.washer` | `bs.elec.appliance`, `bs.elec.appliance.washer` |

`sw.house.light.wall` hat kein Gegenstück und bleibt. Die Typen bleiben registriert: bestehende Folien mit Raumgeräten
laden, zeichnen und speisen Hausinstallationsliste und Stromkreisplan wie bisher, und das Modul allein bietet weiterhin
alle Einträge an.

**Die maßgeblichen Symbole sind auch Raumgeräte der Berichte und Werte.** `elec.house.socket`, `elec.house.switch`,
`elec.house.light` und `bs.elec.appliance` tragen dieselben optionalen Parameter `room`, `feed` und (Verbraucher)
`phase`, `power`, `current`, `voltage` (die Geräteanschlussdose auch `powerFactor`). Hausinstallationsliste,
Stromkreisplan, Einspeisebaum und Stromkreiswerte lesen sie über die Typ-Id (Module verweisen nicht aufeinander): die
Namen in den Berichten folgen der Art („Zweifachsteckdose“, „Dimmer“, „Leuchte“, „Herdanschluss“); ein Kabel oder eine
Ader vom Anschluss des Symbols (`feed`, `n`/`e`/`s`/`w`, `cable`) zu einem unteren Anschluss eines Schutzorgans legt den
Stromkreis fest wie bei den Raumgeräten, eine Installationsleitung `elec.house.cable` eingeschlossen. Steckdose, Leuchte
und Geräteanschlussdose sind Verbraucher (der Schalter nimmt keine Leistung auf); ihren Strom rechnet ihr eigenes Modul
(*P* = *U*·*I*; Geräteanschlussdose mit cos φ). Ohne eingetragene Spannung liegen sie im Stromkreis an 230 V
Außenleiter–Neutralleiter (Beziehung `sw.load.voltage`, wie die Vorgabe der Raumgeräte). Sie sind einphasig, außer ihre
eigene `phase` ist dreiphasig (nur die Geräteanschlussdose bietet das an): an einem dreipoligen Gerät bedeutet „vom
Stromkreis“ für sie L1, nicht dreiphasig.

## Speicherung

Alles steht in Parametern (dazu Punkte und Andockungen der Adern und Kabel); JSON- und Kompakt-Rundreisen zeichnen
identisch (Tests). Nichts wird abgeleitet gespeichert: Beschriftungsstreifen, Berichte und abgeleitete Werte werden aus
dem Dokument neu berechnet. Eingegebene Werte sind Textparameter („2 kW“, „13 A“); der Bemessungsstrom bleibt in der
Bemessungsauswahl.

## Host-Fähigkeiten

| Fähigkeit | PowerPoint COM | Office Web / LibreOffice (Engine) |
|---|---|---|
| Einfügen, Skalieren, Drehen, Ansichtswechsel, Auswahlen, Umschalter, Texteingaben | ja (Engine) | ja (Engine) |
| Native Formen | Rechtecke, abgerundete Rechtecke, Ellipsen, Linien, Polylinien, Polygone, Bögen, Text; keine Vektorgrafik nötig | ebenso |
| Einrasten auf der Schiene und nebeneinander (`Mount`), Leitungsenden an Anschlüssen (`Port`) | ja | ja |
| Beschriftungsstreifen folgt den Geräten (`DependsOnNeighbours`, abhängige Objekte) | ja: Geräte und Streifen werden mit den Einheiten der Folie als Kontext gezeichnet, wie Wände und Strahlen | ja |
| **Leiste nummerieren** (ändert andere Klemmen, `ApplyInDocument`) | ändert nur die gewählte Klemme, bis das Add-in Änderungen anderer Objekte aus einem Aktionsergebnis übernimmt (dieselbe Grenze wie „Strahlenbündel hinzufügen“ in der Optik) | ja |
| Exporte (CSV, Markdown) | über `exporters`/`export` der Engine, wo der Host Fachexporte anbietet | ebenso |

Alles wird mit nativen Grundformen gezeichnet. Schwarz-Weiß-Ausgabe übernehmen die Hosts wie bei anderen Modulen.

## Grenzen und nächste Schritte

- Die Berechnungen beschränken sich auf Lastsummen je Stromkreis und Außenleiter, eine Kurzschlussschätzung an der
  Verteilung und die Prüfungen oben: kein Spannungsfall, keine Leitungsbemessung, keine Auslösekennlinien (Selektivität
  ist ein Faustregel-Hinweis), keine Schleifenimpedanz der Endstromkreise und keine FI-Zuordnungsprüfung; I_z wird
  eingegeben, nicht nachgeschlagen. Überspannungsschutzgeräte tragen keine Werte (die Abstimmung mit der Vorsicherung
  ist offen).
- Die Schienenregel für LS-Schalter ist eine Annahme; für Gewissheit Adern zeichnen oder „Gespeist von“ setzen.
- Kabellängen werden eingegeben (m), nicht aus der Zeichnung gemessen.
- Frequenzumrichter: nur lineare U/f-Kennlinie (kein Boost, keine Vektorregelung, kein Schlupfausgleich); der
  Leistungsfaktor am Eingang ist ein einfaches cos φ (Oberschwingungen des Gleichrichters nicht modelliert); ein Motor je
  Umrichter für den Leistungs-Port; Ausgangsstromgrenzen und der Bemessungsstrom des Umrichters werden nicht geprüft.
- Die Schaltzeichen-Ansicht ist ein kompaktes Gerätesymbol, kein vollständiger mehrpoliger Stromlaufplan; die
  Stromkreisstruktur steht in den Berichten.
- Mögliche nächste Schritte: die Phase eines einpoligen Geräts aus dem Zahn der Phasenschiene darüber, Phasen- und
  Lastspalten im Stromkreisplan, Verweis von Raumgeräten auf Grundrissfolien zur Verteilung („→ -F3 Folie 4“).
