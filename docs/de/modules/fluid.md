# Modul Fluidtechnik (`fluid`)

Bibliothek: `src/InventaSketch.Modules.Fluid` · Tests: `tests/InventaSketch.Modules.Fluid.Tests` ·
Englische Fassung (maßgeblich): [../../en/modules/fluid.md](../../en/modules/fluid.md)

Das Modul liefert Schaltzeichen für Hydraulik- und Pneumatikschaltpläne im Sinne der Konventionen von ISO 1219-1
(grafische Symbole der Fluidtechnik) und des aus ISO-1219-2-Schaltplänen bekannten Kennzeichnungsschemas (z. B. `1V1`,
`0P1`). Alle Symbole sind **eigenständig gezeichnet**; es werden keine Normtexte, Tabellen oder Symbolgrafiken
übernommen, und es wird **keine Normkonformität behauptet**.

Es ist ein **Skizzier**-Modul für Folien, Patentzeichnungen und Konzeptschaltungen. Es simuliert, dimensioniert und
prüft keine Schaltungen.

## Konventionen

- **Schnellskizze zuerst:** Jedes Bibliothekselement wird ohne Parameter eingefügt. Freies (gleichmäßiges oder
  ungleichmäßiges) Skalieren ändert nur `Size`; Symbole sind für eine Entwurfsbox gezeichnet und werden auf die
  Objektbox gestreckt. Kleine Details (Dreiecke, Pfeilspitzen, Punkte, Text) behalten ihre Proportionen. Ändert ein
  Parameter die Entwurfsbox (Schaltstellungen, Betätigungen) – per Aktion oder im Inspektor (`ObjectType.OnParamChanged`)
  –, folgt `Size`, sodass der Zeichnungsmaßstab bleibt.
- **Hydraulik oder Pneumatik** ist der optionale Parameter `medium` (`hydraulic` / `pneumatic`) bei den Typen, deren
  Zeichnung sich unterscheidet: Pumpen/Verdichter, Motoren, Schwenkantrieb, Druckquellen, Wegeventile und
  Betätigungssymbole (volle Dreiecke = Hydraulik, hohle = Pneumatik). Die Kontextaktion `fluid.medium` schaltet um.
- **Linienstärken:** Symbole und Arbeitsleitungen 0,35 mm, Kolben und Stopfen 0,7 mm, Federn und Details 0,25 mm,
  Steuerleitungen lang gestrichelt, Leckölleitungen kurz gestrichelt, Umrahmungen strichpunktiert. Es werden nur
  schwarze Linien und schwarze oder keine Füllungen verwendet; die Schwarzweißausgabe (Patent) bleibt unverändert.
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
| `fluid.medium` | `Medium` | Hydraulik ↔ Pneumatik |
| `fluid.valve.ways.next` | `Cycle` | 2/2 → 3/2 → 4/2 → 4/3 → 5/2 → 5/3 (die Größe folgt der Entwurfsbox) |
| `fluid.valve.centre.next` | `Cycle` | Mittelstellung (4/3: gesperrt, Umlauf, Schwimm, offen, belüftet; 5/3: gesperrt, entlüftet, belüftet) |
| `fluid.valve.normal` | `Toggle` | Ruhestellung gesperrt / offen (2/2, 3/2) |
| `fluid.valve.actLeft.next`, `fluid.valve.actRight.next` | `Actuation` | Betätigung je Seite |
| `fluid.valve.flip` | `Swap` | Federseite tauschen (Betätigungen tauschen; die Grundstellung folgt) |
| `fluid.valve.detent`, `fluid.valve.centring` | `Toggle` | Rastung; Federzentrierung bei 3 Stellungen |
| `fluid.variable`, `fluid.flow.next` | `Toggle`/`Cycle` | Pumpen/Motoren: verstellbar, eine/zwei Stromrichtungen |
| `fluid.cyl.rod`, `fluid.cyl.cushion.next`, `fluid.cyl.spring.next` | `Toggle`/`Cycle` | Kolbenstange, Dämpfung, Feder |
| `fluid.spring`, `fluid.piloted`, `fluid.adjustable`, `fluid.check` | `Toggle` | Optionen von Rückschlag-, Drossel- und Druckventilen |
| `fluid.line.kind.next`, `fluid.line.route` | `Cycle`/`Toggle` | Leitungsart; orthogonal / gerade |
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
