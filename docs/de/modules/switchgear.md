# Modul Verteiler & Installation (`switchgear`)

Bibliothek: `src/InventaSketch.Modules.Switchgear` · Tests: `tests/InventaSketch.Modules.Switchgear.Tests` ·
Englische Fassung (maßgeblich): [../../en/modules/switchgear.md](../../en/modules/switchgear.md) · Roadmap: Punkt 13
„Elektroinstallation und Schaltanlagen“.

Das Modul zeichnet **Verteiler und Schaltschränke auf Hutschienen TS35** und die **Installationsgeräte in den Räumen**,
die sie versorgen: Reiheneinbaugeräte mit Frontansichten, die wie die echten Geräte aussehen (Schaltknebel, Bemessung
„B16“, „30 mA“, Prüftaste, Schraubklemmen), Schaltzeichen-Ansichten, Reihenklemmen mit Brücken, Schienen, Kanäle,
Endhalter und Beschriftung, Adern und Kabel. Kennzeichen und Bemessungswerte sind optionale Parameter und erscheinen auf
den Geräten; fünf **Installationsberichte** (CSV und Markdown) entstehen aus der Folie.

Es ist ein Modul zum **Skizzieren und Dokumentieren**: Es bemisst keine Stromkreise, prüft keine Selektivität oder
Schutzmaßnahmen und beansprucht keine Normkonformität. Die Frontansichten sind eigenständig nach dem üblichen Aussehen
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
  `bus`, Kanäle der Busaktoren `A1/A2`, `B1/B2` …, Klemmen `1/2` (Etagen `3/4`, `5/6`).
- **Ansichten:** Reiheneinbaugeräte und Klemmen haben eine *Frontansicht* (Standard) und eine *Schaltzeichen*-Ansicht
  (Kontakte, Spulen, Klemmen nach Art von IEC 60617, unbetätigt gezeichnet, mit gestrichelter Wirkverbindung,
  Summenstromwandler und IΔ-Auslöser, Kennzeichen und Bemessung rechts). Beide Ansichten haben dieselben Anschlussnamen;
  der Wechsel behält den optischen Maßstab.
- **Kennzeichen** folgen den Ideen von IEC 81346 (Kennbuchstaben nach IEC 81346-2): `-F` Schützen (LS, FI, FI/LS, SPD),
  `-Q` Energie schalten (Hauptschalter, Schütz), `-K` Signale verarbeiten (Relais, Zeitrelais, Busgeräte), `-P`
  Darstellen (Zähler, Leuchtmelder), `-T` Umformen (Netzteile), `-S` Handbetätigung (Taster, Schalter), `-X` Verbinden
  (Klemmen, Steckdosen), `-W` Kabel. Sie sind freier Text; „Kennzeichen …“ schlägt den Kennbuchstaben vor.
  **Gleiche Kennzeichen gehören zusammen** (ein Knoten im Stromkreisplan, ein Kabel in der Kabelliste).
- **Auswahlen sind Dropdowns** (`ActionChoices.FromParam`): Pole, Charakteristik, Bemessungsstrom, Fehlerstrom, FI-Typ,
  Spulenspannung, Kontakte, Funktion, Farbe, Querschnitt … Die Knebelstellung ist ein Umschalter („Eingeschaltet“).

## Objekttypen

| Typ-Id | Element (DE / EN) | Parameter (alle optional) |
|---|---|---|
| `sw.protection.mcb` | Leitungsschutzschalter / Miniature circuit breaker | `char` B/C/D, `rating` 6–63 A, `poles` 1, 1+N, 2, 3, 3+N, `on` |
| `sw.protection.rcd` | Fehlerstromschutzschalter / Residual current device | `poles` 2/4, `rating` 25–100 A, `idn` 10–500 mA, `rcdType` AC/A/F/B, `on` |
| `sw.protection.rcbo` | FI/LS-Schalter / RCBO | `poles` 1+N, 3+N, `char`, `rating`, `idn`, `rcdType`, `on` |
| `sw.protection.spd` | Überspannungsschutz / Surge protective device | `spdType` 1, 1+2, 2, 3, `poles` 1, 1+N, 3, 3+N |
| `sw.switch.main` | Hauptschalter (Lasttrennschalter) / Main switch | `poles` 1–4, `rating` 32–125 A, `on` |
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
| `sw.line.cable` | Kabel / Cable | `cableType` NYM-J, NYY-J, H07V-K, H05VV-F, J-Y(ST)Y, Cat, `cores`, `section`, `ref`, `length` (m), `from`, `to` |
| `sw.house.socket` | Steckdose (Raum/Stromkreis) / Socket outlet | `gangs` 1–3, `room`, `feed`, `ref` |
| `sw.house.switch` | Lichtschalter (Raum/Stromkreis) / Light switch | `kind` Aus-, Wechsel-, Kreuzschalter, Taster, Dimmer, `room`, `feed`, `ref` |
| `sw.house.light` | Leuchtenauslass (Raum/Stromkreis) / Light outlet | `kind` Decke/Wand, `room`, `feed`, `ref` |
| `sw.house.appliance` | Geräteanschluss (Raum/Stromkreis) / Appliance connection | `kind` Herd, Waschmaschine, Trockner, Geschirrspüler, Durchlauferhitzer, Gerät, `room`, `feed`, `ref` |

Jedes Reiheneinbaugerät hat außerdem `ref` (Kennzeichen), `circuit` (Stromkreisname, der Beschriftungstext) und `feed`
(Kennzeichen des vorgeschalteten Geräts); Leitungsschutzschalter und FI/LS zusätzlich `room`. Klemmen nutzen `ref` für
das Leistenkennzeichen („-X1“) und `number` für die Klemmennummer.

Die Bibliothek bietet 71 Elemente (35 Typen) in sieben Kategorien (Schutz- & Trenngeräte; Schalten & Steuern;
Zähler, Netzteile & Busgeräte; Reihenklemmen; Schienen, Kanäle & Beschriftung; Adern & Kabel; Installationsgeräte in den
Räumen), z. B. „Leitungsschutzschalter B16, 1-polig“, „FI-Schutzschalter 40 A 30 mA, 4-polig“, „KNX-Schaltaktor,
4-fach“, „Doppelstockklemme 2,5 mm²“, „Kabel NYM-J 5×2,5“.

## Aktionen

- Auswahlen (Dropdowns) für jeden Auswahlparameter; Pole, Kanäle, Leistung oder Querschnitt ändern die Rahmenbreite
  (linke Kante bleibt).
- **Eingeschaltet** (Umschalter): Knebel oben/unten, Schaltstellungsanzeige rot/grün (LS, FI, FI/LS, Hauptschalter,
  Schalter).
- **Kennzeichen …** (alle Geräte, Klemmen, Adern, Kabel), **Schienenname …**, **Länge …** (Kabel).
- **Leiste nummerieren** (Klemmen): nummeriert die Klemmen mit demselben Leistenkennzeichen auf der Folie (ohne
  Kennzeichen: die Klemmen derselben Reihe) von links nach rechts mit 1, 2, 3 …
- **Ein Pol mehr / weniger** (Steckbrücke).
- Raumgeräte: **Raum …** und **Stromkreis …** (Kennzeichen des speisenden Geräts, z. B. „-F3“, am Symbol angezeigt).

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
| `sw.circuits` | Stromkreisplan | Einspeisebaum: Hauptschalter, Zähler, SPD, FI, LS und FI/LS nach Kennzeichen, jeweils mit dem speisenden Gerät und **woher diese Zuordnung stammt**; Stromkreise mit Räumen und Geräten. Markdown: verschachtelte Liste und Tabelle. |
| `sw.layout` | Verteilerbelegung und Beschriftungsstreifen | Je Schiene (von oben nach unten, benannt nach `ref` oder „Schiene n“): Geräte von links nach rechts mit Platz, erster TE, Breite in TE, Kennzeichen, Gerät, Bemessung, Beschriftung; belegte / verfügbare TE; der Beschriftungsstreifen als zweizeilige Tabelle. Geräte auf keiner Schiene stehen am Ende. |
| `sw.terminals` | Klemmenplan | Je Leiste die Klemmen in Nummernfolge (natürliche Sortierung), je Etage und Seite (oben/unten): Ader oder Kabel, Ziel (Kennzeichen und Anschluss am anderen Ende, z. B. „-F2:2“), Querschnitt, Farbe, Brücke. Das `target` einer Klemme nennt das äußere Ziel, wenn keine Leitung angedockt ist. |
| `sw.cables` | Kabelliste | Kennzeichen, Typ, Adern × Querschnitt, Länge (m), von, nach, Anzahl Abschnitte. Abschnitte mit gleichem Kennzeichen (z. B. auf mehreren Folien) sind ein Kabel: Längen werden addiert, die äußeren Enden sind „von“ und „nach“. |

So verknüpft der Stromkreisplan die Geräte (in dieser Reihenfolge): (1) der Parameter `feed` („Gespeist von“), Herkunft
„Parameter“; (2) die Verdrahtung: eine Ader oder ein Kabel von einem unteren Anschluss eines Geräts im Einspeisebaum zu
einem oberen Anschluss dieses Geräts (Raumgeräte: beliebiger Anschluss), Herkunft „Verdrahtung“; (3) nur für
Leitungsschutzschalter: der nächste FI links auf derselben Schiene, Herkunft „Schienenreihenfolge (angenommen)“ – der
übliche Aufbau mit Phasenschiene; der Bericht kennzeichnet dies als Annahme. Raumgeräte hängen unter dem Kennzeichen in
ihrem `feed`; ein genanntes, aber nicht gezeichnetes Kennzeichen erscheint als „(nicht gezeichnet)“ – nichts wird
erfunden.

## Speicherung

Alles steht in Parametern (dazu Punkte und Andockungen der Adern und Kabel); JSON- und Kompakt-Rundreisen zeichnen
identisch (Tests). Nichts wird abgeleitet gespeichert: Beschriftungsstreifen und Berichte werden aus dem Dokument neu
berechnet.

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

- Keine Berechnungen: kein Spannungsfall, keine Leitungsbemessung, Selektivität, Schleifenimpedanz oder FI-Zuordnungs-
  prüfung (Kandidaten für die Ingenieurberechnungen, Roadmap-Punkt 14, z. B. „Sicherung unter der Leitungsbelastung“).
- Die Schienenregel für LS-Schalter ist eine Annahme; für Gewissheit Adern zeichnen oder „Gespeist von“ setzen.
- Kabellängen werden eingegeben (m), nicht aus der Zeichnung gemessen.
- Die Schaltzeichen-Ansicht ist ein kompaktes Gerätesymbol, kein vollständiger mehrpoliger Stromlaufplan; die
  Stromkreisstruktur steht in den Berichten.
- Mögliche nächste Schritte: Blindabdeckungen und Phasenschienen als Elemente, Phasenzuordnung (L1/L2/L3) im
  Stromkreisplan, Last- und Leistungssummen je Stromkreis, Verweis von Raumgeräten auf Grundrissfolien zur Verteilung
  („→ -F3 Folie 4“).
