# Technische Werte und Berechnungen – Konzept und Katalog

Stand: Konzept und Plan, 25.11.2026 (Wunsch des Maintainers). Calc 0 und Calc 1 (Größen, Einheiten, Parser,
Einheitenumrechner und Rechner, Beziehungs-Engine, Engine-API, ohmsches Gesetz am Widerstand) sind umgesetzt; die
Entwurfsentscheidungen stehen in Abschnitt 10. Calc 2 (die PowerPoint-Oberfläche: Stufe, Werte-Schaltfläche,
Kreismenü, Wertekarte, Wertebeschriftung, Ziehen, Widerspruchszeichen) ist in COM umgesetzt, siehe Abschnitt 11.
Calc 3 (Elektro: Abschnitt 10.10; Maschinenelemente: Abschnitt 12) und Calc 4 (Fluid, Verfahren, Gebäudetechnik, Energie: Abschnitt 13) sind in der Engine umgesetzt (26.11.2026); Calc 5–6 sind offen. Englische (maßgebliche)
Fassung: [../../en/design/engineering-calculations.md](../../en/design/engineering-calculations.md).

**Warum.** Zwei Wünsche des Maintainers vom 25.11.2026:

> „E/E, Elektrik, Maschinenelemente, Maschinen, Fluidtechnik, Solar, … überall, wo man Werte eingeben kann, soll man
> andere Parameter berechnen können, dynamisch und interaktiv, z. B. Spannung, Strom, Leistung, Widerstand, Kapazität,
> Induktivität … Drehzahl, Drehmoment, Leistung (Achsabstand / Zähnezahl), Übersetzung, Volumenstrom, Wärmemenge …"

> „Man kann schnell skizzieren, aber auch interaktiv und spielerisch lernen und verstehen, konkret, physikalisch
> korrekt und technisch präzise zeichnen und rechnen und Dinge auf Plausibilität prüfen. Ebenso bei Diagrammen. Auch
> modulübergreifend, wo sinnvoll, relevant und möglich."

Dieses Dokument legt fest: (1) die Grundsätze einer kleinen Rechenschicht im Core und wie sie in den Hosts erscheint,
(2) einen Katalog der Formelfamilien je Fachgebiet mit Quellen, (3) je Elementfamilie, was heute vorhanden ist und was
fehlt, und (4) einen Stufenplan mit Abnahmekriterien. Die Quellen stehen in Abschnitt 9 und werden als [S*n*] zitiert.
Wir setzen nur Physik und Lehrbuchbeziehungen um. Normen nennen wir, damit Nutzer wissen, wo die verbindlichen Regeln
stehen; ihre Texte, Tabellen und Grenzwerte übernehmen wir nicht (CLAUDE.md, Abschnitt 2).

---

## 1. Grundsätze

### 1.1 Drei Nutzungsstufen

Dasselbe Element dient drei Arbeitsweisen. Die Stufe ist eine Dokumenteinstellung (Menüband: *Werte: Aus · Erkunden ·
Ingenieur*), die der Nutzer jederzeit wechseln kann. Voreingestellt ist Aus, damit die Schnellskizze an erster Stelle
bleibt.

| Stufe | Für wen / wofür | Was der Nutzer sieht | Was der Nutzer tut |
|---|---|---|---|
| **Schnellskizze** (Aus) | eine Prinzipskizze in Sekunden | keine Werte, keine Karten, keine Hinweise; Beschriftungen nur, wenn getippt | zeichnen und skalieren; Werte eingeben bleibt möglich (Kreismenü *Werte ▸*), nichts ist Pflicht |
| **Erkunden** (Lernen) | verstehen, wie Dinge voneinander abhängen | eine kompakte Wertekarte beim Überfahren; Griffe und Schieber an Werten; Formeln Schritt für Schritt ausgeschrieben; Pfeile, die zeigen, in welche Richtung sich abhängige Werte bewegen | einen Wert ziehen und zusehen, wie alles folgt (Zähne → Übersetzung → Drehzahl → Drehmoment); „Was wäre, wenn" ausprobieren, ohne etwas kaputt zu machen (Esc stellt zurück) |
| **Ingenieur** (präzise) | konkrete, physikalisch korrekte Werte | Einheiten nach Wahl, signifikante Stellen, Toleranzen, die Quelle jeder Formel, Plausibilitätsprüfungen mit Regel und Quelle, ein Rechenblatt | Datenblattwerte eingeben, einen Entwurf prüfen, die Rechnung als Tabelle ausgeben |

Erkunden und Ingenieur nutzen dieselben Beziehungen. Sie unterscheiden sich nur in der Darstellung: Erkunden zeigt
gerundete Werte (3 signifikante Stellen), Bewegung und Formeln; Ingenieur zeigt Einheiten, Genauigkeit, Toleranzen und
Quellen.

### 1.2 Werte, Beziehungen und Herkunft

- Eine **Größe** (`VarDef`) ist eine benannte, typisierte Größe eines Elements. Sie hat einen Schlüssel, einen
  zweisprachigen Namen, ein Formelzeichen je Sprache, eine *Größenart*, eine Anzeigeeinheit, einen typischen Bereich
  und eine Relevanz (Karte, Panel oder nur Ingenieur). Beispiel: Widerstand `R` „Resistance"/„Widerstand", Größenart
  *elektrischer Widerstand*, Einheit Ω, Karte.
- Formelzeichen folgen ISO/IEC 80000 und IEC 60027-1 [S2, S3]. Wo die Sprachen sich in der Konvention unterscheiden,
  ist das Zeichen zweisprachig: Drehmoment ist im Englischen *T* und im Deutschen *M*, Drehzahl in beiden *n*.
- Eine **Beziehung** ist eine Gleichung zwischen Größen, z. B. *U* = *R*·*I* oder *n*₂ = *n*₁·*z*₁/*z*₂. Sie hat eine
  Id, einen zweisprachigen Namen, für jede Größe, nach der sie auflösbar ist, einen Löser (explizite Umkehrung, oder
  eine eingegrenzte numerische Nullstellensuche, wo es keine geschlossene Form gibt), ihre Gültigkeitsbedingungen
  („linearer, unbelasteter Teiler", „idealer Transformator") und eine Quellen-Id.
- Jeder Wert hat eine **Herkunft**:

| Herkunft | Bedeutung | Darstellung |
|---|---|---|
| *eingegeben* | vom Nutzer getippt, gezogen oder aus einem Datenblatt übernommen | normal |
| *Vorgabe* | Vorgabewert des Typs (z. B. Modul 430 Wp), nicht eingegeben | grau, „Vorgabe" im Tooltip |
| *verknüpft* | von einem verbundenen Element oder verknüpften Diagramm übernommen | Kettensymbol |
| *abgeleitet* | durch eine Beziehung berechnet | „=" davor, Formel beim Überfahren |

- **Eingegebene Werte werden nie überschrieben.** Abgeleitete Werte werden nie als Wahrheit gespeichert; die Engine
  berechnet sie bei Bedarf neu. Vorgaben weichen, wenn sie eingegebenen Werten widersprechen, und die Karte sagt das
  („Vorgabe 430 Wp ersetzt durch 4 × 405 Wp aus dem Strang").
- **Auflösen in jede Richtung.** Der Nutzer gibt die Werte ein, die er kennt; die Engine berechnet den Rest, sobald
  das System bestimmt ist. Mit *U* und *I* ergeben sich *R* und *P*; mit *P* und *R* ergeben sich *U* und *I*.
- **Unterbestimmt:** Die Karte zeigt, wie viele Werte fehlen und welche genügen würden: „Noch ein Wert: *I*, *R* oder
  *P*".
- **Überbestimmt:** Stimmen alle Werte innerhalb der Toleranz der Beziehung überein, zeigt die Karte ein Häkchen ✓.
  Sonst zeigt sie einen **Widerspruch**. Angezeigt werden der eingegebene Wert, der Wert, den die Beziehung ergibt, die
  Beziehung und die beteiligten Eingaben. Die Engine ändert nichts. Der Nutzer wählt, welche Eingabe er löscht oder
  welche er behält und neu berechnen lässt („*U* und *I* behalten, *R* ableiten"); diese Wahl ist ein
  Rückgängig-Schritt.
- **Geometrie bleibt unabhängig.** Werte verschieben oder skalieren die Zeichnung nie von selbst (Schnellskizze
  zuerst: Skalieren ändert `Size`, nie Parameter). Wo Werte eine Größe ergeben, wendet eine ausdrückliche Aktion sie an.
  Beispiele: „Wahre Größe aus Modul" (gibt es für Zahnräder), „Achsabstand aus Werten", „Kanalbreite aus Volumenstrom".
- **Textwertige Größen** (26.11.2026). Eine Größe mit textwertiger Größenart (`Quantities.Text(id, name, labels)`,
  `QuantityKind.IsText`) enthält den Index einer der zweisprachigen Bezeichnungen der Art, etwa die Grashof-Klasse des
  Viergelenks (`cls`, Art `kin.grashofClass`: Kurbelschwinge, Doppelkurbel, Schwingkurbel, Doppelschwinge mit
  umlaufender Koppel, Durchschlaggetriebe, Nicht-Grashof-Doppelschwinge). Überall, wo Werte formatiert werden
  (`ValueFormat.Format`: `display` in Werte-Panel und Karte, Wertebeschriftung, Erklärung, Rechenblatt), erscheint die
  Bezeichnung; die Engine-API bleibt daher unverändert: `display` ist „Kurbelschwinge", `kind` die Art-Id,
  `number`/`value` der Index. Die Regeln halten sie aus der Rechnung heraus: Sie wird nur von einer Beziehung ihres
  Elements berechnet (eine Beziehung mit textwertiger Größe wird nur nach dieser aufgelöst und nie geprüft), nie
  eingegeben (der Parser lehnt sie ab; unter ihrem Schlüssel Gespeichertes wird ignoriert; Zieh-Vorschauen überspringen
  sie), bekommt keine Vorgabe, keinen Toleranzbereich und kein Angebot „fehlender Wert" und nimmt an keinem Netz teil
  (kein physikalischer Schlüssel hat eine Textart). Textwertige Arten stehen nicht in `Quantities.All`; der Rechner
  benennt also kein Ergebnis danach. Host-Hinweis: Hosts sollten für eine Größe, deren `kind` keine der `units`-Arten
  ist, weder Ziehgriff noch Eingabefeld anbieten (die COM-Karte bietet Ziehen für positive abgeleitete Werte an; die
  Engine ignoriert ein Ziehen an der Klasse).

### 1.3 Größen, Einheiten, Genauigkeit

- **Intern kohärentes SI** [S1]: m, kg, s, A, K, dazu rad für ebene Winkel. Jede Größenart hat einen
  Dimensionsvektor und eine Liste erlaubter Anzeigeeinheiten. Manche Größenarten teilen sich eine Dimension, etwa
  Drehmoment und Energie (N·m gegenüber J) oder Frequenz und Drehzahl (Hz gegenüber 1/min). Die Größenart hält sie
  auseinander, sodass ein Drehmomentfeld N·m und kN·m anbietet, aber nie J.
- **Temperatur und Temperaturdifferenz** sind zwei Größenarten. *θ* nimmt °C oder K an, *Δθ* nur K. Auf Deutsch wird
  ΔT in „°C" bei der Eingabe angenommen und als K angezeigt.
- **Eingabe** läuft über einen gemeinsamen Parser im Core. Er ersetzt die drei eigenen: Charts `Fmt.ParseInput`,
  Energy `Act.Number` und Gebäudetechnik `Bs.ParseMm`. Er versteht:
  - Dezimalkomma oder -punkt
  - SI-Vorsätze p n µ/u m k M G
  - den Buchstabencode nach IEC 60062, bei dem der Buchstabe das Dezimalzeichen ersetzt, wie in `4k7`, `4R7` und
    `2n2` [S4]
  - Einheiten mit oder ohne Leerzeichen (`10µF`, `1,5 kW`, `16 A`, `1500 rpm`, `1500/min`, `40 l/min`, `2,5 mm²`,
    `20 °C`)
  - Brüche für Übersetzungen (`20/0,4`, `3:1`)

  Eine Einheit der falschen Größenart wird mit einer Meldung abgewiesen („16 A ist ein Strom; dieses Feld erwartet
  eine Spannung"). Eine bloße Zahl erhält die Anzeigeeinheit des Feldes.
- **Anzeige.** Werte erscheinen mit signifikanten Stellen: 3 in Erkunden, 2–6 in Ingenieur als Dokumenteinstellung.
  SI-Vorsätze werden innerhalb der Einheitenfamilie des Feldes automatisch gewählt (4,7 kΩ, 2,2 µF, 1,2 MW). Das
  Dezimalzeichen folgt der Oberflächensprache. Eingegebene Werte behalten ihren Text, „4k7" bleibt also „4k7" auf der
  Beschriftung, solange der Nutzer nicht auf die normierte Schreibweise umschaltet.
- **Keine Rundungsdrift.** Gerechnet wird in voller double-Genauigkeit; gerundet wird nur für die Anzeige.
  Toleranzprüfungen nutzen die Toleranz der Beziehung: 1 × 10⁻⁶ relativ für exakte Beziehungen, eine angegebene
  Toleranz für empirische, z. B. 2 % für einen Riementrieb mit Schlupf.
- **Toleranzen (Ingenieur).** Ein eingegebener Wert darf eine Toleranz tragen (`4k7 ±5 %`, `100 µF −20/+80 %`).
  Abgeleitete Werte zeigen dann ein Intervall aus der Intervallarithmetik [S37], z. B. *I* = 48,9 … 54,1 mA. Das ist
  Worst Case, keine Statistik.
- **Normreihen** (E12/E24-Widerstandswerte, Vorzugsnennströme, Modulreihen) wären für „nächster Normwert" nützlich, sind
  aber Normtabellen (IEC 60063, IEC 60898-1, ISO 54) [S5, S11, S16]. Das ist ein offener Punkt: Ausliefern erst nach
  einer festgehaltenen Entscheidung (Quelle und Lizenz in `THIRD-PARTY-NOTICES.md`). Bis dahin ist freie Eingabe der
  einzige Weg.

### 1.4 Was je Element „relevant" ist

Jedes Element meldet höchstens **vier Kartengrößen**, also die, die man neben das Symbol schreiben würde (Widerstand:
*R*, *U*, *I*, *P*; Zahnrad: *z*, *m*, *n*, *M*). Alle anderen Größen stehen im Werte-Panel, reine Ingenieursgrößen
(Toleranz, Temperaturkoeffizient, Aufteilung des Wirkungsgrads) im Ingenieur-Abschnitt. Die Reihenfolge ist je Typ
fest. Die Karte wächst nie über fünf Zeilen; lange Listen gehören ins Panel.

### 1.5 Wie es in der Oberfläche erscheint

| Ort | Verhalten |
|---|---|
| **Schwebende Leiste** | Eine Schaltfläche *Werte* (neues Piktogramm `cmd-values`, „x=", mit kurzem Text) in Gruppe 1. Sie erscheint nur bei Elementen mit Beziehungen und nur in den Stufen Erkunden oder Ingenieur. In Aus bleibt die Leiste unverändert. |
| **Kreismenü** | *Bearbeiten ▸ Werte ▸*: ein Eintrag je Größe („Spannung *U* … 230 V"), Eingabe per `WithInput`. Abgeleitete Größen zeigen „= 460 W" und öffnen die Erklärung. Die acht festen Plätze bleiben unverändert (interaction-ux.md, Abschnitt 6). |
| **Wertekarte beim Überfahren** (Erkunden, Ingenieur) | Eine kleine Karte neben dem Element, 2–5 Zeilen: eingegebene Werte normal, abgeleitete mit „=", ein ✓ oder Widerspruchszeichen, „Noch ein Wert: …". Ein Klick auf einen Wert bearbeitet ihn an Ort und Stelle. In Erkunden hat jeder Wert einen kleinen Schieber oder Griff. Sie gehört nur zum Overlay und nie zur Folie. |
| **Beschriftung am Element** | „Auf der Zeichnung zeigen" je Größe. Die Beschriftung ist ein echter Teil des Elements (Teilschlüssel `values`), wird also wie jeder andere Text formatiert, exportiert und erhalten. Bei Elektroteilen zeigt die vorhandene `value`-Beschriftung die gewählten Größen („4k7 / 0,25 W"). |
| **Griffe auf der Zeichnung** (Erkunden) | Die vorhandenen Wertgriffe (`DragHandles`) werden auf physikalische Werte erweitert: Achsabstand eines Zahnrads, Hub eines Zylinders, Betriebspunkt eines Diagramms ziehen. Abhängige Werte folgen live. Loslassen ergibt einen Rückgängig-Schritt; Esc bricht ab. |
| **Formel Schritt für Schritt** (Erkunden) | Erscheint bei einem Klick auf einen abgeleiteten Wert: „*P* = *U*·*I* = 230 V · 2,00 A = 460 W". Bei Ketten eine Zeile je Schritt („*n*₂ = *n*₁·*z*₁/*z*₂ = 1500 min⁻¹ · 18/54 = 500 min⁻¹"), mit Pfeilen ↑↓, die beim Ziehen zeigen, wohin sich jede abhängige Größe bewegt. |
| **Inspektor in der Seitenleiste** | Abschnitt *Werte* mit allen Größen, Herkunft, Einheitenwahl, Toleranz (Ingenieur), Quelle jeder Beziehung (Ingenieur) und „Löschen". |
| **Prüfungen** | Ein kleiner bernsteinfarbener Punkt am Element (nur Overlay) und eine Liste *Prüfungen* (Menüband, Werkzeuge). Siehe Abschnitt 4. |

**Barrierefreiheit und Sprache.** Jeder Name, jeder Hinweis und jede Meldung ist zweisprachig (`I18nText`), mit
einzeiligen Hinweisen im Stil von interaction-ux.md. Einheiten und Formelzeichen werden nicht übersetzt (SI), außer wo
die Zeichenkonvention abweicht (*T*/*M*).

### 1.6 Weitergabe über Verbindungen

Werte gehen nur über **ausdrückliche Verbindungen** zwischen Elementen, die es im Modell schon gibt. Dazu gehören
Eingriffe und Kupplungen (`Links`, `Coupling`), Wellenverbände (`GearTrain`), Leitungsnetze (Andockpunkte wie beim
Netzlistenexport) sowie Rohr-, Kanal- und Kabelanschlüsse mit `PortMedia`. Jede Verbindungsart bringt
**Anschlussbeziehungen** mit:

| Verbindung | Was gleich ist oder erhalten bleibt | Vorhandene Basis |
|---|---|---|
| Zahneingriff, Riemen, Kette | Drehzahlverhältnis *n*₁/*n*₂ = *z*₂/*z*₁ (oder *d*₂/*d*₁); Drehmoment *M*₂ = *M*₁·*i*·*η* | `Links.Ratio`, `GearTrain.Speeds` (relative Drehzahlen, heute nur für die Animation) |
| Welle, Kupplung, Zahnräder fest auf einer Welle | gleiches *n*; das Drehmoment geht durch, abzüglich abgezweigter Momente | `GearTrain` (Wellen, Stufenräder, Stege über Willis) |
| Planetensatz | Willis-Gleichung *n*_s − *n*_St = −(*z*_H/*z*_s)(*n*_H − *n*_St); Momentengleichgewicht | `GearTrain` (Stege) |
| Elektrisch in Reihe (ein Zweig) | gleicher Strom *I*; Spannungen addieren sich; *R* und *L* addieren sich, 1/*C* addiert sich | Leitungsnetze (Andockpunkte, Knoten) |
| Elektrisch parallel (dieselben zwei Netze) | gleiche Spannung *U*; Ströme addieren sich; 1/*R* addiert sich, *C* addiert sich | Leitungsnetze |
| Knoten mit allen Strömen bis auf einen bekannt | Knotenregel: Σ*I* = 0 | Leitungsnetze |
| PV-Strang / -Feld | Module in Reihe: *U* addiert sich, *I* gleich; Stränge parallel: *I* addiert sich | `energy.pv.string` hat `modules` |
| Rohr oder Kanal in Reihe | gleicher Volumenstrom *V̇* (inkompressibel); Druckverluste addieren sich | `PortMedia` `fluid`, `heating.*`, `water.*`, `air.*` |
| Rohrabzweig | Σ*V̇* = 0; bei Wärmeträgern auch Mischung Σ(*V̇*·*θ*) | Abzweige in Fluid-/TGA-Leitungen |
| Kabel | gleiches *I* an beiden Enden; *U*_Ende = *U*_Anfang − Δ*U* | `energy.line.cable` (kV, dc), Installationskabel |

**Netzumformung statt Simulation.** Netzwerke aus Widerständen, Induktivitäten und Kapazitäten werden nur in Reihen- und
Parallelschritten zusammengefasst. Eine Brücke oder ein vermaschtes Netz, das sich so nicht zusammenfassen lässt,
erhält die Meldung „hier nicht zusammenfassbar – bitte einen Schaltungssimulator verwenden". Jede Anschlussbeziehung ist
lokal, die Weitergabe ist also eine Kette von Schritten mit je einer Unbekannten (Abschnitt 1.8).

### 1.7 Wo es aufhört (nicht im Umfang)

- **Keine Schaltungssimulation.** Also keine Knoten- oder Maschenanalyse vermaschter Netze, keine nichtlinearen
  Bauteilmodelle, keine Transientensimulation und keine AC-Lastflussrechnung. Geschlossene Antworten erster Ordnung
  (RC/RL-Sprung, RLC-Resonanz) sind als Formeln und verknüpfte Diagramme im Umfang.
- **Keine Netzberechnungen:** Kurzschlussströme (IEC 60909), Selektivität und Schutzkennlinien, Erdung und
  Berührungsspannung.
- **Keine Normtabellen:** Strombelastbarkeitstabellen und Umrechnungsfaktoren (IEC 60364-5-52 Anhang B),
  Norm-Außentemperaturen und U-Werte (nationale Anhänge zu EN 12831-1), Zylinderdurchmesserreihen (ISO 3320), E-Reihen
  (bis zur Entscheidung, Abschnitt 1.3). *I*_z, U-Werte und Auslegungstemperaturen gibt der Nutzer selbst ein.
- **Keine Festigkeitsnachweise:** Zahnradtragfähigkeit (ISO 6336), Wellenermüdung (DIN 743), Schraubenberechnung
  (VDI 2230), Federdauerfestigkeit (EN 13906-1 über *F* = *c*·*s* hinaus). Höchstens Nennspannungen, gekennzeichnet als
  „Nennwert, kein Nachweis".
- **Keine CFD, kein Druckstoß, keine Zweiphasenströmung, keine Kältemittel-Stoffdaten.** Das p-h-Diagramm bleibt
  schematisch.
- **Keine PV-Ertragssimulation.** Die Einstrahlung ist eine Eingabezahl, z. B. aus einem Werkzeug wie PVGIS, und wird
  nicht mitgeliefert.
- **Keine Konformitätsaussagen.** Kein Ergebnis heißt „normgerecht". Prüfungen sagen „Hinweis", nennen die Regel und
  die Quelle und überlassen die Entscheidung dem Nutzer.

### 1.8 Löser (Core, hostunabhängig)

1. Die Größen aller Elemente im Bereich sammeln: der Verband der Auswahl, oder im Hintergrunddurchlauf die ganze Folie.
   Dazu ihre eingegebenen, vorgegebenen und verknüpften Werte.
2. Die Beziehungen sammeln: lokale je Element, Anschlussbeziehungen je Verbindung und Verbandsbeziehungen (absolute
   Drehzahlen = `GearTrain.Speeds` × eine bekannte Drehzahl).
3. **Lokale Weitergabe** [S37]. Wiederholt eine Beziehung mit genau einer Unbekannten nehmen und lösen. Dazu dient die
   explizite Umkehrung oder eine eingegrenzte Nullstellensuche (Brent) im physikalischen Bereich der Größe, etwa für die
   Rohrreibungszahl nach Colebrook oder eine Temperatur aus der LMTD. Jede gelöste Größe merkt sich ihren Rechenweg:
   die Beziehung und ihre Eingaben.
4. Beziehungen, deren Größen alle bekannt sind, werden gegen ihre Toleranz geprüft. Jede Abweichung wird ein
   Widerspruch mit beiden Rechenwegen.
5. Vorgaben werden erst für Größen genutzt, die nach Schritt 3 noch unbekannt sind; danach läuft der Durchgang erneut.
   Eine Vorgabe erzeugt nie einen Widerspruch; sie weicht.
6. Was unbekannt bleibt, wird je Element gemeldet, zusammen mit der kleinsten Menge an Größen, die es bestimmen würde.

Der Löser ist deterministisch, begrenzt (jede Beziehung wird je Größe und Durchgang höchstens einmal gelöst) und
nebenwirkungsfrei. Sein Ergebnis ist eine Wertetabelle mit Rechenweg je (Objekt-Id, Größe). Die Engine-API erhält
diese Aufrufe: `values(objectId)`, `setValue(objectId, key, text)`, `clearValue`, `explain(objectId, key)`,
`checks(scope)` und `dragValue(objectId, key, value)` (Live-Vorschau, ein Rückgängig-Schritt erst beim Loslassen). Die
Zeitbudgets:

- 1 000 Elemente und 5 000 Beziehungen in unter 50 ms unter .NET 10
- unter 200 ms in WebAssembly
- ein einzelnes Element in unter 1 ms, damit das Ziehen flüssig bleibt

### 1.9 Plausibilitätsprüfungen

Eine Prüfung ist eine Regel mit diesen Angaben:

- Id, Elementtypen und Schwere (*Info*, *Hinweis* oder *Warnung*; nichts blockiert)
- eine zweisprachige Meldung, die sagt, was gefunden wurde und was man tun kann
- die Regel als Formel
- eine Quelle

Prüfungen laufen nach dem Lösen und erscheinen als bernsteinfarbener Punkt am Element (nur Overlay) und in der Liste
*Prüfungen*. Sie lassen sich je Element oder Dokument stummschalten; das Stummschalten wird gespeichert. Prüfungen
ändern nie Werte oder Geometrie. Der Katalog steht in Abschnitt 4.

### 1.10 Diagramme, die mit Elementwerten verknüpft sind

Ein Diagramm kann mit einem oder mehreren Elementen **verknüpft** werden. Die Verknüpfung wird am Diagramm als Liste von
Objekt-Ids gespeichert, dazu die Zuordnung von Diagrammparametern zu Elementgrößen. Ein verknüpftes Diagramm:

- zeichnet sich neu, wenn sich ein verknüpfter Wert ändert;
- zeigt den **Betriebspunkt** (einen Schnittpunkt oder markierten Wert) als Marke mit Griff;
- **schreibt zurück**: Das Ziehen der Marke trägt den neuen Wert als eingegebenen Wert in das Element ein, ein
  Rückgängig-Schritt für beide. Beispiele: Förderstrom der Pumpe, Betriebsmoment des Motors;
- lässt sich **entkoppeln**; dann friert es seine aktuellen Werte als eigene Parameter ein.

Der Katalog steht in Abschnitt 5.

### 1.11 Modulübergreifende Ketten

Anschlüsse tragen Paare von **Leistungsgrößen**: eine *Potenzialgröße* (Effort) und eine *Flussgröße* (Flow), deren
Produkt die Leistung ist. Das ist die Bondgraph-Sicht auf Mehrdomänensysteme [S38]. Jedes Element einer Kette hat eine
Übertragungsbeziehung mit einem Wirkungsgrad:

| Domäne | Potenzial × Fluss | Leistung |
|---|---|---|
| Elektrisch DC | *U* × *I* | *P* = *U*·*I* |
| Elektrisch AC (1~/3~) | *U* × *I* mit cos *φ*, *f* | *P* = *U*·*I*·cos *φ* (1~), √3·*U*·*I*·cos *φ* (3~) |
| Drehbewegung | *M* × *ω* (*ω* = 2π*n*) | *P* = *M*·*ω* |
| Schiebebewegung | *F* × *v* | *P* = *F*·*v* |
| Hydraulik / Pneumatik | *p* × *V̇* | *P* = Δ*p*·*V̇* |
| Wärmeträger (Wasser, Luft) | *θ*_VL, *θ*_RL × *V̇* (oder *ṁ*) | *Φ* = *ṁ*·*c*·Δ*θ* |

Die Ketten stehen in Abschnitt 6.

---

## 2. Katalog der Formelfamilien

Spalten: Beziehung(en), Eingaben ↔ Ausgaben (in jede Richtung, die die Beziehung erlaubt), typischer Bereich, Quelle.
Formelzeichen nach ISO/IEC 80000 [S2]. „↔" heißt: die Beziehung wird nach der jeweils fehlenden Größe aufgelöst.

### 2.1 Elektrotechnik DC und AC

| Familie | Beziehungen | Größen (↔) | Typischer Bereich | Quelle |
|---|---|---|---|---|
| Ohmsches Gesetz, Leistung | *U* = *R*·*I*; *P* = *U*·*I* = *I*²*R* = *U*²/*R* | *U, I, R, P* (zwei beliebige ergeben den Rest) | mV … kV; µA … kA; mΩ … MΩ | [S7] |
| Reihe / parallel | *R* = Σ*R*ᵢ; 1/*R* = Σ1/*R*ᵢ; *L* ebenso; *C* umgekehrt (in Reihe 1/*C* = Σ1/*C*ᵢ, parallel *C* = Σ*C*ᵢ) | Netzwerk → Ersatzwert | – | [S7] |
| Spannungsteiler (unbelastet) | *U*₂ = *U*·*R*₂/(*R*₁+*R*₂) | *U, U*₂*, R*₁*, R*₂ | – | [S7] |
| Zeitkonstanten | *τ* = *R*·*C*; *τ* = *L*/*R*; *u*_C(*t*) = *U*(1 − e^(−*t*/*τ*)); 63 % bei *τ*, ≈ 99 % bei 5*τ* | *R, C, L, τ, t* | µs … s | [S7] |
| Blindwiderstand | *X*_L = 2π*f*·*L*; *X*_C = 1/(2π*f*·*C*) | *f, L, C, X* | Hz … MHz | [S7] |
| Impedanz (Reihen-RLC) | \|*Z*\| = √(*R*² + (*X*_L − *X*_C)²); *φ* = arctan((*X*_L − *X*_C)/*R*) | *R, X*_L*, X*_C*, Z, φ* | – | [S7] |
| Resonanz, Güte | *f*₀ = 1/(2π√(*L·C*)); *Q* = (1/*R*)√(*L*/*C*) (Reihe); Bandbreite *B* = *f*₀/*Q* | *L, C, R, f*₀*, Q, B* | – | [S7] |
| RC/RL-Filter | *f*_g = 1/(2π*R·C*), *f*_g = *R*/(2π*L*) | *R, C, L, f*_g | – | [S7] |
| Wechselstromleistung, Leistungsfaktor | *S* = *U*·*I*; *P* = *S*·cos *φ*; *Q* = *S*·sin *φ*; *S*² = *P*² + *Q*² | *U, I, S, P, Q, φ, λ* | cos *φ* 0,7 … 1 | [S6, S7] |
| Drehstrom (symmetrisch) | *S* = √3·*U*_LL·*I*; *P* = √3·*U*_LL·*I*·cos *φ*; *U*_LN = *U*_LL/√3 | *U, I, P, S, cos φ* | 230/400 V [S13] | [S7] |
| Idealer Transformator | *U*₁/*U*₂ = *N*₁/*N*₂ = *I*₂/*I*₁; *S* = √3·*U*·*I* (3~) | *U*₁*, U*₂*, N*₁*/N*₂*, I, S* | – | [S7, S15] |
| Energie | *E* = *P*·*t*; Kosten = *E* × Preis (Preis ist Eingabe) | *P, t, E* | Wh … GWh | [S1] |

### 2.2 Kabel und Schutzeinrichtungen

Wir setzen die Physik um. Verbindliche Grenzwerte, Belastbarkeitstabellen und Umrechnungsfaktoren stehen in
IEC 60364-5-52 (Abschnitt 525 Spannungsfall, Anhang G informativ; Anhang B Strombelastbarkeit) und IEC 60364-4-43 sowie
in den nationalen Übernahmen DIN VDE 0100-520 und -430 [S8, S9]. *I*_z und den zulässigen Spannungsfall gibt der
Nutzer ein.

| Familie | Beziehungen | Größen | Typischer Bereich | Quelle |
|---|---|---|---|---|
| Leiterwiderstand | *R* = *ρ*·*l*/*A*; *ρ*(*θ*) = *ρ*₂₀(1 + *α*(*θ* − 20 °C)); *ρ*₂₀,Cu = 0,017241 Ω·mm²/m, *α*_Cu ≈ 0,00393 K⁻¹; Al nach [S12] | *l, A, θ, R* | 0,5 … 300 mm² | [S12] |
| Spannungsfall (Wechselstrom, zwei Leiter) | Δ*U* = 2·*l*·*I*·(*R*′ cos *φ* + *X*′ sin *φ*); mit *X*′ ≈ 0 bei kleinen Querschnitten: Δ*U* = 2·*l*·*I*·*ρ*/*A* | *l, I, A, ΔU*, Δ*u* % | – | [S7, S8] |
| Spannungsfall (Drehstrom, symmetrisch) | Δ*U* = √3·*l*·*I*·(*R*′ cos *φ* + *X*′ sin *φ*) | wie oben | – | [S7, S8] |
| Querschnitt aus zulässigem Spannungsfall (Idee) | *A* = 2·*l*·*I*·*ρ*·cos *φ*/Δ*U*_zul (1~); den nächstgrößeren Querschnitt wählt der Nutzer | *l, I, ΔU*_zul → *A*_min | – | [S7] |
| Leitungsverluste | *P*_V = *k*·*I*²·*R* (*k* = 2 bei 1~, 3 bei 3~) | *I, R, P*_V | – | [S7] |
| Überlastkoordination (nur Prüfung) | *I*_B ≤ *I*_n ≤ *I*_z; *I*₂ ≤ 1,45·*I*_z | *I*_B (Last), *I*_n (Gerät), *I*_z (Nutzereingabe) | – | [S9] |
| LS-Schalter, Sicherung, RCD (Beschriftungen) | LS Typ B/C/D + *I*_n (z. B. „B16"), Sicherung *I*_n + Klasse („gG 35 A"), RCD *I*_Δn („30 mA") werden in Beschriftungsgrößen geparst; keine Auslösekennlinien | *I*_n, Charakteristik, *I*_Δn | 6 … 125 A; 10/30/300 mA | [S10, S11] |

### 2.3 Elektronik (nur Einfaches)

| Familie | Beziehungen | Größen | Typischer Bereich | Quelle |
|---|---|---|---|---|
| LED-Vorwiderstand | *R* = (*U*_S − *n*·*U*_F)/*I*_F; *P*_R = *I*_F²·*R* | *U*_S*, n, U*_F*, I*_F*, R, P*_R | *U*_F 1,8 … 3,5 V; *I*_F 2 … 20 mA (Datenblatt) | [S7] |
| Transistor als Schalter (BJT) | *I*_B ≥ *k*·*I*_C/*B* (Übersteuerung *k* ≈ 2 … 5); *R*_B = (*U*_E − *U*_BE)/*I*_B | *U*_E*, U*_BE*, I*_C*, B, R*_B | *U*_BE ≈ 0,7 V | [S7] |
| OPV-Verstärkung (ideal) | invertierend *A* = −*R*_f/*R*_E; nichtinvertierend *A* = 1 + *R*_f/*R*_g; dB = 20 lg\|*A*\| | *R*, *A* | – | [S7] |
| Kondensator: Ladung, Energie | *Q* = *C*·*U*; *E* = ½*C*·*U*² | *C, U, Q, E* | – | [S7] |
| Spule: Energie | *E* = ½*L*·*I*² | *L, I, E* | – | [S7] |
| Batterie (Schaltzeichen) | *U* = Zellen × Zellspannung (Zellspannung ist Eingabe) | Zellen, *U* | – | [S35] |

### 2.4 Maschinenelemente

| Familie | Beziehungen | Größen | Typischer Bereich | Quelle |
|---|---|---|---|---|
| Stirnradgeometrie | *d* = *m*·*z*; *d*_a = *m*(*z* + 2); *d*_f = *m*(*z* − 2,5); *p* = π*m*; schrägverzahnt: *m*_t = *m*_n/cos *β* | *m, z, d, d*_a*, β* | *m* 0,5 … 25 mm; *z* ≥ 17 ohne Unterschnitt bei *α* = 20° | [S16, S18] |
| Achsabstand (ohne Profilverschiebung) | *a* = *m*(*z*₁ + *z*₂)/2 außen; *a* = *m*(*z*₂ − *z*₁)/2 innen | *m, z*₁*, z*₂*, a* | – | [S16] |
| Übersetzung, Drehzahl, Drehmoment | *i* = *n*₁/*n*₂ = *z*₂/*z*₁ (= *d*₂/*d*₁); *M*₂ = *M*₁·*i*·*η*; *P* = 2π·*n*·*M*; *P*[kW] = *M*[N·m]·*n*[min⁻¹]/9549 | *z, i, n, M, P, η* | *η* 0,97 … 0,99 je Stirnradstufe; Schnecke 0,4 … 0,9 | [S18] |
| Mehrstufiges Getriebe | *i*_ges = Π*i*_k; *η*_ges = Π*η*_k | Stufen | – | [S18] |
| Schneckengetriebe | *i* = *z*_Rad/*z*_Schnecke (Gangzahl); Wirkungsgrad aus Steigungswinkel und Reibung (in der ersten Fassung Eingabe *η*) | *z*₁ (Gänge), *z*₂*, i, η* | – | [S18] |
| Planetengetriebe (Willis) | *n*_s − *n*_St = −(*z*_H/*z*_s)(*n*_H − *n*_St); Montagebedingung (*z*_s + *z*_H)/*N*_Planeten ganzzahlig; *z*_p = (*z*_H − *z*_s)/2 (ohne Profilverschiebung) | *z*_s*, z*_H*, z*_p*, n*_s*, n*_H*, n*_St | Standübersetzung 3 … 10 | [S18, S39] |
| Zahnstange | *v* = *ω*·*d*/2; *F* = 2*M*/*d* | *m, z, n, v, F* | – | [S18] |
| Riementrieb | *i* = *d*₂/*d*₁ (Schlupf 1 … 2 % als angegebene Toleranz); *v* = π·*d*₁·*n*₁; *F*_t = *P*/*v* | *d*₁*, d*₂*, n, v, P, F*_t | *v* ≤ 30 m/s (Keilriemen, typisch) | [S18, S20] |
| Kettentrieb | *i* = *z*₂/*z*₁; mittleres *v* = *z*·*p*·*n*; *F*_t = *P*/*v* | *z, p, n, v, P* | – | [S18, S20] |
| Gewindetrieb | *v* = *P*_h·*n*; *F* = 2π·*M*·*η*/*P*_h; *M* = *F*·*P*_h/(2π*η*) | *P*_h (Steigung)*, n, v, M, F, η* | Kugelgewindetrieb *η* ≈ 0,9; Trapezgewinde 0,2 … 0,6 | [S18, S23] |
| Welle (Torsion, Nennwert) | *M* = *P*/(2π*n*); *τ*_t = 16*M*/(π*d*³) (Vollwelle); *φ* = *M*·*l*/(*G*·*I*_p), *I*_p = π*d*⁴/32 | *P, n, M, d, τ*_t*, φ, l, G* | *G*_Stahl ≈ 81 GPa | [S18] (Festigkeit: DIN 743 [S22], nicht im Umfang) |
| Lagerlebensdauer | *L*₁₀ = (*C*/*P*)^*p* [10⁶ Umdr.], *p* = 3 Kugel, 10/3 Rolle; *L*₁₀h = 10⁶·*L*₁₀/(60·*n*) | *C, P, n, L*₁₀*, L*₁₀h | 10 000 … 100 000 h (je Anwendung) | [S19] |
| Federn | *F* = *c*·*s*; zylindrische Druckfeder *c* = *G*·*d*⁴/(8·*D*³·*n*_f); Drehfeder *M* = *c*_φ·*φ* | *F, s, c, d, D, n*_f*, G* | – | [S21, S18] |
| Kupplungen, Freiläufe | *n* und *M* gehen durch; Rutschkupplung *M* ≤ *M*_R; Freilauf nur in einer Richtung | *M, n* | – | [S18] |
| Translation | *P* = *F*·*v*; *W* = *F*·*s* | *F, v, s, P, W* | – | [S1] |

### 2.5 Elektrische Maschinen und Antriebe

| Familie | Beziehungen | Größen | Typischer Bereich | Quelle |
|---|---|---|---|---|
| Synchrondrehzahl | *n*_s = 60·*f*/*p* (Polpaarzahl *p* = Polzahl/2; das Modell speichert die Polzahl) | *f*, Polzahl, *n*_s | 50/60 Hz | [S14, S15] |
| Schlupf (Asynchronmaschine) | *s* = (*n*_s − *n*)/*n*_s; Generator bei *s* < 0 | *n*_s*, n, s* | *s* 1 … 6 % im Bemessungspunkt | [S15] |
| Leistung, Drehmoment, Drehzahl | *P*_mech = 2π·*n*·*M*; *η* = *P*_ab/*P*_zu; *P*_el = √3·*U*·*I*·cos *φ* (3~) | *P, M, n, η, U, I, cos φ* | *η* 0,8 … 0,97 (IE-Klassen [S14]) | [S14, S15] |
| Drehmoment-Drehzahl (ASM, Kloss) | *M*/*M*_k = 2/(*s*/*s*_k + *s*_k/*s*) | *M*_k (Kippmoment), *s*_k | – | [S15] |
| Gleichstrom- / PM-Maschine | *U* = *R*·*I* + *k*·*ω*; *M* = *k*·*I* (in SI: Drehmomentkonstante = Spannungskonstante) | *U, I, R, k, ω, M* | – | [S15] |
| Elektrische Frequenz (PMSM) | *f* = *n*·*p*/60 | *n*, Polzahl, *f* | – | [S15] |
| Rad / Fahrzeug | *v* = *ω*·*r*_dyn; *F* = *M*/*r*_dyn; *P* = *F*·*v* | *r*, *n*, *M*, *v*, *F* | – | [S18] |
| Verbrennungsmotor (Block) | *P* = 2π·*n*·*M* (keine Thermodynamik) | *n, M, P* | – | [S1] |

### 2.6 Kinematik

| Familie | Beziehungen | Größen | Quelle |
|---|---|---|---|
| Freiheitsgrad (vorhanden) | *F* = 3(*n* − 1) − 2*j*₁ − *j*₂ (Grübler/Kutzbach) | Glieder, Gelenke | [S24] |
| Grashof-Bedingung (Grashof-Abstand Δ_G, Klassen-Plakette, Prüfung `kin.grashof`) | *s* + *l* ≤ *p* + *q* → ein Glied läuft voll um | Gliedlängen | [S24] |
| Schubkurbel | Hub = 2*r*; *v*_max ≈ *ω*·*r* (Pleuel viel länger als Kurbel) | *r, l, ω* | [S24] |
| Schnellrücklauf | Zeitverhältnis = (180° + *α*)/(180° − *α*) aus den Totlagen | Längen | [S24] |
| Übertragungswinkel | *μ* aus der Lagelösung; die übliche Grenze *μ* ≥ 40° ist nur ein Hinweis | Längen, Antriebswinkel | [S24] |
| Geschwindigkeiten | numerische Ableitung der Lagelösung × Antriebsdrehzahl (keine neue Physik) | Antriebs-*ω* | [S24] |

### 2.7 Fluidtechnik (Symbole nach ISO 1219)

| Familie | Beziehungen | Größen | Typischer Bereich | Quelle |
|---|---|---|---|---|
| Volumenstrom und Geschwindigkeit | *V̇* = *A*·*v*; *A* = π*d*_i²/4 | *V̇, d*_i*, v* | siehe Prüfungen | [S27] |
| Zylinder | *A*₁ = π*D*²/4; *A*₂ = π(*D*² − *d*²)/4; *F* = *p*·*A*·*η*_hm (minus Gegendruck × andere Fläche); *v* = *V̇*/*A*; Flächenverhältnis *φ* = *A*₁/*A*₂ | *D, d, p, F, V̇, v, η*_hm | 10 … 350 bar (Hydraulik), 6 … 10 bar (Pneumatik) | [S26] |
| Pumpe | *V̇* = *V*_g·*n*·*η*_v; *M* = *V*_g·Δ*p*/(2π·*η*_hm); *P*_zu = Δ*p*·*V̇*/*η*_t | *V*_g*, n, V̇, Δp, M, P, η* | *V*_g 1 … 500 cm³ | [S26] |
| Motor | *n* = *V̇*·*η*_v/*V*_g; *M* = *V*_g·Δ*p*·*η*_hm/(2π) | wie Pumpe | – | [S26] |
| Hydraulische Leistung | *P* = Δ*p*·*V̇*; *P*[kW] = *p*[bar]·*V̇*[l/min]/600 | *p, V̇, P* | – | [S26] |
| Druckverlust im Rohr | Δ*p* = *λ*·(*l*/*d*)·*ρ*·*v*²/2; *Re* = *v*·*d*/*ν*; laminar *λ* = 64/*Re* (*Re* < 2300); turbulent Colebrook–White, bei glatten Rohren Blasius | *v, d, l, ν, ρ, Δp* | Öl *ν* ≈ 10 … 100 mm²/s | [S27] |
| Blende / Ventil | *V̇* = *α*·*A*·√(2Δ*p*/*ρ*); Datenblatt-Skalierung *V̇* = *V̇*_nenn·√(Δ*p*/Δ*p*_nenn) | *A, Δp, V̇* | – | [S26] |
| Speicher | *p*₀·*V*₀ = *p*₁·*V*₁ = *p*₂·*V*₂ (isotherm), *p*·*V*^*κ* konst. (adiabat, *κ* ≈ 1,4); Δ*V* = *V*₁ − *V*₂ | *p*₀*, p*₁*, p*₂*, V*₀*, ΔV* | – | [S26] |
| Pneumatischer Luftverbrauch | *V̇*_n = *A*·*s*·*f*·(*p*_e + *p*_amb)/*p*_amb (je Hub und Richtung) | *A, s, f, p*_e | – | [S26] (Durchflusskennwerte: ISO 6358 [S25]) |

### 2.8 Wärme- und Verfahrenstechnik (Symbole nach ISO 10628)

| Familie | Beziehungen | Größen | Typischer Bereich | Quelle |
|---|---|---|---|---|
| Wärmemenge | *Q* = *m*·*c*·Δ*θ*; Wasser *c* ≈ 4,19 kJ/(kg·K) | *m, c, Δθ, Q* | – | [S29] |
| Wärmestrom (Träger) | *Φ* = *ṁ*·*c*·Δ*θ* = *ρ*·*V̇*·*c*·Δ*θ*; Wasser: *Φ*[kW] ≈ 1,163·*V̇*[m³/h]·Δ*θ*[K] | *V̇, Δθ, Φ* | – | [S29, S30] |
| Wärmeleitung / -durchgang | *Φ* = *U*·*A*·Δ*θ*; *U* = 1/(1/*h*_i + Σ*d*/*λ* + 1/*h*_a) | *U, A, Δθ, Φ* | – | [S29] |
| Wärmeübertrager (LMTD) | *Φ* = *U*·*A*·Δ*θ*_m; Δ*θ*_m = (Δ*θ*_a − Δ*θ*_b)/ln(Δ*θ*_a/Δ*θ*_b); Gegen- und Gleichstrom; andere Bauarten über Korrekturfaktor (Eingabe) | vier Temperaturen, *U, A, Φ* | – | [S29] |
| Kreiselpumpe | *P*_hyd = *ρ*·*g*·*V̇*·*H*; *P*_Welle = *P*_hyd/*η*; Ähnlichkeitsgesetze *V̇* ∝ *n*, *H* ∝ *n*², *P* ∝ *n*³ | *V̇, H, n, P, η* | – | [S28] |
| Anlagenkennlinie | *H* = *H*_stat + *k*·*V̇*² (*k* aus den Druckverlusten) | *H*_stat*, k, V̇* | – | [S28] (vorhanden in `chart.pump`) |
| Ventilator | *P* = Δ*p*·*V̇*/*η*; Ähnlichkeitsgesetze wie bei Pumpen | Δ*p, V̇, P, η* | – | [S28, S30] |
| Behälter / Speicher | *V* = π*D*²·*h*/4 (Zylinder); gespeicherte Wärme *Q* = *ρ*·*V*·*c*·Δ*θ* | *V, D, h, Δθ, Q* | – | [S29] |
| Thermodynamische Kreisprozesse (vorhanden) | Otto- / Diesel-Wirkungsgrad (`process.chart.pv`) | *ε*, *φ*, *κ* | – | vorhandener Code |

### 2.9 Lüftung, Klima und Gebäudetechnik

| Familie | Beziehungen | Größen | Typischer Bereich | Quelle |
|---|---|---|---|---|
| Wärmestrom Luft | *Φ* = *ρ*·*c*_p·*V̇*·Δ*θ*; Luft ≈ 0,34 W·h/(m³·K): *Φ*[W] ≈ 0,34·*V̇*[m³/h]·Δ*θ*[K] | *V̇, Δθ, Φ* | – | [S30] |
| Luftwechsel | *V̇* = *n*·*V*_Raum (Luftwechselrate *n* in h⁻¹, Nutzereingabe; Personenluftraten aus EN 16798-1 werden nicht übernommen) | *n, V, V̇* | – | [S30, S31] |
| Wärmerückgewinnung | *θ*_Zuluft = *θ*_Außen + *η*_t(*θ*_Abluft − *θ*_Außen) | Temperaturen, *η*_t | – | [S30] |
| Heizlast (vereinfacht, Lernhilfe) | *Φ*_HL = (Σ*U*ᵢ·*A*ᵢ + 0,34·*n*·*V*)·(*θ*_int − *θ*_e) | Flächen, *U*-Werte, *n*, *V*, Temperaturen (alles Nutzereingaben) | – | Idee der vereinfachten Verfahren der EN 12831-1 [S31] |
| Heizkörperleistung | *Φ* = *Φ*_50·(Δ*θ*/50 K)^*n*, *n* ≈ 1,3 (Datenblatt); Δ*θ* als logarithmische oder arithmetische Übertemperatur über Raum | *Φ*_50*, n, θ*_VL*, θ*_RL*, θ*_Raum | – | Kennlinie nach EN 442-2 [S31] |
| Fußbodenheizkreis | *Φ* = *q*·*A*; *V̇* = *Φ*/(*ρ*·*c*·Δ*θ*); Kreislänge ≈ *A*/Verlegeabstand | *A, q, Verlegeabstand, Δθ* | – | [S30] |
| Heizkurve | *θ*_VL(*θ*_e) aus der Heizkörperkennlinie zwischen Auslegungspunkt und *θ*_e = *θ*_Raum (Steigung und Parallelverschiebung als Parameter) | *θ*_VL,Ausl*, θ*_e,Ausl*, θ*_Raum*, n* | – | [S30] |
| Rohr- / Kanalbemessung | *v* = 4*V̇*/(π*d*_i²); runder Kanal *d*; rechteckig *A* = *a*·*b*, *d*_h = 2*ab*/(*a* + *b*) | *V̇, d*_i*, a, b, v* | siehe Prüfungen | [S27, S30] |
| DN und Innendurchmesser | DN ist eine Nennweite; der Innendurchmesser hängt von der Rohrreihe ab. Der Nutzer gibt *d*_i ein, oder DN dient als mit „≈" gekennzeichnete Näherung | DN, *d*_i | – | [S30] |
| Warmwasserspeicher | *Q* = *V*·*ρ*·*c*·Δ*θ* (1000 l, 40 K → 46,5 kWh) | *V, Δθ, Q* | – | [S29] |

### 2.10 Solar und Energie

| Familie | Beziehungen | Größen | Typischer Bereich | Quelle |
|---|---|---|---|---|
| Modul bei STC | *P*_mpp = *U*_mpp·*I*_mpp; STC = 1000 W/m², 25 °C Zelle, AM 1,5 | *P, U*_oc*, U*_mpp*, I*_sc*, I*_mpp | 300 … 700 Wp | [S33] (Datenblattangaben: EN 50380) |
| Temperaturkorrektur | *U*_oc(*θ*) = *U*_oc,STC·(1 + *β*·(*θ* − 25 °C)); ebenso *U*_mpp, *I*_sc mit *α*, *P* mit *γ* | *β* ≈ −0,25 … −0,35 %/K | – | [S33, S34] (Verfahren: IEC 60891) |
| Zelltemperatur | *θ*_Zelle = *θ*_amb + (NMOT − 20 °C)·*G*/800 W/m² | *θ*_amb*, G*, NMOT | NMOT 40 … 47 °C | [S33, S34] |
| Einstrahlungsskalierung | *I* ≈ *I*_STC·*G*/1000 W/m²; *U* ändert sich logarithmisch (in der ersten Fassung vernachlässigt, wird angegeben) | *G, I* | – | [S34] |
| Strang / Feld | *U*_Strang = *N*_s·*U*_Modul; *I*_Feld = *N*_p·*I*_Strang; *P* = *N*_s·*N*_p·*P*_Modul | *N*_s (vorhanden: `modules`)*, N*_p | – | [S34] |
| Höchste Strangspannung | *U*_oc,max = *N*_s·*U*_oc(*θ*_min); kleinste MPP-Spannung *U*_mpp,min = *N*_s·*U*_mpp(*θ*_max) | *θ*_min (Nutzer, Standort), *θ*_max | – | [S33] (IEC 62548-1, Betriebstemperatur) |
| DC/AC-Verhältnis | *R* = *P*_DC,STC/*P*_AC,Nenn | – | typisch 1,0 … 1,3 | [S34] |
| Ertragsabschätzung | *E* = *P*_STC·(*H*_POA/1 kW/m²)·PR; spezifischer Ertrag *E*/*P*_STC | *H*_POA (Eingabe), PR (Eingabe, typ. 0,75 … 0,9) | – | [S33] (PR: IEC 61724-1) |
| Wechselrichter | *P*_AC = *η*·*P*_DC; *I*_AC = *P*_AC/(√3·*U*·cos *φ*) (3~) | *η, P, U, I* | *η* 0,95 … 0,98 | [S33] (EN 50530) |
| Batterie | *E* = *U*_nenn·*Q*_n; *E*_nutzbar = *E*·DoD; C-Rate = *I*/(*Q*_n/1 h); *t* = *E*_nutzbar/*P*; Zykluswirkungsgrad *η*_rt | *U, Q*_n [Ah]*, E* [kWh]*, DoD, I, P, t* | 0,2 … 2 C | [S35] (Stromangabe: IEC 61434) |
| E-Auto laden | *P* = √3·*U*·*I* (3~, z. B. 400 V · 16 A ≈ 11 kW); *t* ≈ Δ*E*/(*P*·*η*) (Ladekurve vernachlässigt, wird angegeben) | *P, U, I, E, t* | – | [S7] |
| Windenergieanlage | *P* = ½·*ρ*·*A*·*v*³·*c*_p; *c*_p ≤ 16/27 (Betz) | *ρ, D, v, c*_p | *c*_p 0,35 … 0,5 | [S36] |
| Wärmepumpe | *Φ*_H = COP·*P*_el; COP ≤ COP_Carnot = *T*_H/(*T*_H − *T*_C) (Kelvin) | COP, *P*_el*, Φ*_H*, θ*_Quelle*, θ*_VL | COP 2,5 … 5 | [S29, S32] |
| BHKW | *η*_ges = (*P*_el + *Φ*_th)/*P*_Brennstoff; Stromkennzahl *σ* = *P*_el/*Φ*_th | *P*_el*, Φ*_th*, P*_Brennstoff | – | [S29] |
| Elektrolyseur / Brennstoffzelle | *ṁ*_H₂ = *η*·*P*/H_u; *P* = *η*·*ṁ*·H_u (Heizwert H₂ ≈ 33,3 kWh/kg) | *P, η, ṁ* | – | [S29] |
| Wasserstofftank | gespeicherte Energie *E* = *m*·H_u | *m, E* | – | [S29] |
| Netztransformator | *I* = *S*/(√3·*U*) je Seite | *S, U*_OS*, U*_US*, I* | – | [S7] |

---

## 3. Elementfamilien: heute und benötigt

„Heute" ist dem Code entnommen (25.11.2026). Die neuen Parameter sind gewöhnliche optionale `ParamDef`s mit `Unit`. Eine
`VarDef` bindet einen vorhandenen Parameter in seiner gespeicherten Einheit, Dokumente brauchen also keine Migration.
Zum Beispiel ist Energy `voltage` in kV gespeichert, und die Größe liest es als kV.

### 3.1 Elektrotechnik (`InventaSketch.Modules.Electrical`)

| Familie | Heute | Beziehungen | Neue Parameter (Vorschlag) |
|---|---|---|---|
| Widerstand, Potentiometer | `value` (freier Text, nur gezeichnet, nie geparst), `ref`, `showLabel` | Ohm, Leistung, Reihe/parallel, Teiler (Poti: Schleiferstellung) | `value` als *R* parsen (`4k7`); `voltage`, `current`, `power`, `prated` (Belastbarkeit), `tolerance`; Poti: `wiper` 0…1 |
| Kondensator (auch gepolt) | `value` | *τ* = *RC*, *X*_C, *Q* = *CU*, *E*, Reihe/parallel | `value` als *C* parsen; `voltage`, `urated` |
| Spule | `value`, `core` | *τ* = *L/R*, *X*_L, *E*, Resonanz mit C | `value` als *L* parsen; `current` |
| Transformator | `core` | ideales Verhältnis, *S* | `u1`, `u2` (oder Verhältnistext „230/12 V"), `power` |
| Quelle (dc/ac/Strom), Batterie | `kind`, `cells` | *U*, *I*, *P*; AC *f* | `value` als *U* parsen (bei Stromquelle *I*); `frequency` für ac |
| Diode, LED | `value` | LED-Vorwiderstand | `uf`, `if` (oder „2,1 V 20 mA" parsen) |
| Transistor (BJT/MOSFET/IGBT) | Polarität, Kanal, Gehäuse | Schalter-Basiswiderstand (nur BJT) | `beta`, `ic` (Ingenieur) |
| Sicherung, LS-Schalter, RCD | nur `value` | Prüfung Überlastkoordination | `value` als *I*_n + Charakteristik parsen („B16", „gG 35 A"); RCD *I*_Δn („30 mA") |
| Motor / Generator (Symbol) | `supply` | *P*, *U*, *I*, cos *φ* | `power`, `voltage`, `current`, `pf` (oder Verknüpfung mit einem Element aus Maschinen) |
| Schalter, Schütz | `contact`, `poles` | lässt *I* durch (geschlossen) oder trennt das Netz (offen; vorhanden als `closed`) | `irated` (Ingenieur) |
| Leitung / Netz | `route`, `hops` | Reihenstrom, Knotenregel, Netzspannung als Beschriftung | keine (Werte sitzen an Bauteilen und Netzen) |
| Hausinstallation (Steckdose, Leuchte, Verteiler, Kabel) | Höhen, Fachzahl, Aderzahl | Last je Stromkreis, Spannungsfall, Koordination | Steckdose/Leuchte `power`; Kabel `length`, `section`, `material`; Stromkreis-Schutzorgan *I*_n |
| OPV, Logik, Steuergeräteblock | Gatterart, Eingänge, Pins | nur OPV-Verstärkung (äußere Widerstände) | keine (die Verstärkung folgt aus den Widerständen ringsum) |

### 3.2 Mechanik (`InventaSketch.Modules.Mechanical`)

| Familie | Heute | Beziehungen | Neue Parameter (Vorschlag) |
|---|---|---|---|
| Stirn-/Schräg-/Hohlrad, Kegelrad | `teeth`, `module` (erweitert, ohne Vorgabe), `helix`, `hub`; „Wahre Größe aus Modul" vorhanden | Geometrie, Achsabstand, Übersetzung, Drehzahl, Drehmoment, Leistung, Wirkungsgrad | `speed`, `torque`, `power`, `efficiency` (je Eingriff, Ingenieur), `pressureAngle` (Ingenieur, Vorgabe 20°), `helixAngle` (bei Schrägverzahnung) |
| Schnecke / Schneckenrad | `starts`, `teeth`, `module` | Schneckenübersetzung, Wirkungsgrad | `efficiency` |
| Zahnstange | `module` | *v*, *F* vom Ritzel | `force`, `speed` (linear) |
| Planetensatz | `planets`, `stepped`; Zähnezahlen nur in den Explosionsangaben | Willis, Montagebedingung | `zs`, `zr` (*z*_p abgeleitet) |
| Steg | `fixed`, `innerRatio` | Willis (über `GearTrain`) | keine |
| Riemenscheibe + Riemen | `hub`; Riemen `ratio` (Durchmesserverhältnis, nur Zeichnung) | Riementrieb | Scheibe `diameter`; Riemen `slip` (Ingenieur) |
| Kettenrad + Kette | `teeth`; Kette keine | Kettentrieb | Kette `pitch` |
| CVT | `ratio` (Zeichnung) | *i* | – (`ratio` wird eine Größe) |
| Gewindetrieb | `pitch` (Steigung, erweitert), `kind` | Gewindetrieb-Beziehungen | `efficiency`, `force`, `speed` |
| Welle | `hollow` | Torsion (Nennwert), gemeinsames *n* | `diameter`, `innerDiameter` (hohl), `length`, `material` (*G*) |
| Lager | keine | *L*₁₀ | `c` (dynamische Tragzahl), `load` *P*, `kind` Kugel/Rolle (Exponent) |
| Federn (Druck, Zug, Dreh) | `coils` | *F* = *c*·*s*, *c* aus der Geometrie | `rate`, `force`, `travel`, `wire`, `meanDiameter`, `material` |
| Kupplungen, Freilauf, Schwungrad | `reverse`, `lockup`, `hub` | Durchleitung; Schwungrad *E* = ½*J*ω² | Schwungrad `inertia`; Rutschkupplung `tslip` |
| Drehmoment- / Drehpfeile | `direction` | zeigen ein verknüpftes *M* oder *n* | `value` (Text, geparst) oder Verknüpfung mit dem Element, auf das sie zeigen |

### 3.3 Maschinen (`InventaSketch.Modules.Machines`)

| Familie | Heute | Beziehungen | Neue Parameter (Vorschlag) |
|---|---|---|---|
| Radial-/Axialflussmaschine (Schnitte) | `poles`, `slots`, `phases`, `mode`, `rotor`, `layout` | *n*_s, *f*, Schlupf, Nut-/Polzahlen (Lochzahl *q* = *Q*/(2*p*·*m*)) | `frequency`, `speed`, `power`, `torque`, `voltage`, `current`, `pf`, `efficiency` |
| Antriebsstrang E-Maschine, Verbrennungsmotor | `kind`, `layout`, `cylinders` | Leistung/Drehmoment/Drehzahl | `power`, `torque`, `speed` |
| Antriebsblöcke: Motor, Wechselrichter, Batterie, Zwischenkreis, Getriebe, Differenzial, Rad | `mode`, `phases`, `dclink`; das Getriebe hat **keine Übersetzung** | Kette Batterie → Wechselrichter → Motor → Getriebe → Differenzial → Rad | Batterie `voltage`, `capacity`; Wechselrichter `power`, `efficiency`; Getriebe `ratio`, `efficiency`; Differenzial `ratio`; Rad `radius` |
| Annotationen (Fluss, Drehung, Drehmoment) | `direction` | zeigen verknüpfte Werte | Verknüpfung |

### 3.4 Kinematik (`InventaSketch.Modules.Kinematics`)

| Familie | Heute | Beziehungen | Neue Parameter (Vorschlag) |
|---|---|---|---|
| Viergelenk, Schubkurbel, Kreuzschleife, Kniehebel, Schnellrücklauf, Kurve | Längen (mm), `angle`, `dof`-Plakette (Grübler; `Linkages.Grubler`, `Linkage.Dof`) | Grashof-Abstand und Klassen-Plakette, Hub, Zeitverhältnis, Übertragungswinkel (Prüfung `kin.transmission`), Abtriebsgeschwindigkeit; Kennliniensatz `transfer` (erledigt 26.11.2026, siehe Moduldoku Kinematik) | `inputSpeed` (min⁻¹) |
| Plakette `kin.motion.dof` | `links`, `lower`, `higher`, `formula` | Grübler (vorhanden, mit Formel) | keine; sie ist das Vorbild für die Schritt-für-Schritt-Anzeige |

### 3.5 Fluidtechnik (`InventaSketch.Modules.Fluid`)

Heute **keine Zahlenparameter**, nur Auswahlen (Medium, verstellbar, Kolbenstange, Dämpfung, Wege, Mittelstellung, …).

| Familie | Beziehungen | Neue Parameter (Vorschlag) |
|---|---|---|
| Pumpe, Motor | Verdrängung, Volumenstrom, Drehmoment, Leistung | `displacement` (cm³), `speed`, `pressure`, `efficiencyV`, `efficiencyHm` (Ingenieur) |
| Zylinder (einfach, doppelt wirkend) | Kraft, Geschwindigkeit, Flächenverhältnis | `bore`, `rod`, `stroke`, `pressure`, `force`, `flow` |
| Schwenkmotor | *M* = *V*_g·Δ*p*/(2π) | `displacement` |
| Druckbegrenzungs-, Druckminder-, Zuschaltventil | Einstelldruck (begrenzt *p*) | `setPressure` |
| Drossel, Stromventil, Wegeventil | Blendenskalierung | `qnom`, `dpnom` (Ingenieur) |
| Speicher | Boyle / adiabat | `volume`, `p0`, `p1`, `p2` |
| Leitung (Arbeits-, Steuer-, Leck-, Schlauch-) | *V̇* = *A*·*v*, Druckverlust | `innerDiameter`, `length` |
| Manometer, Durchflussmesser | zeigen den Wert an ihrem Anschluss (verknüpft) | keine |

### 3.6 Verfahrenstechnik (`InventaSketch.Modules.Process`)

Heute nur Auswahlen. Das p-v-Diagramm berechnet bereits den Otto-/Diesel-Wirkungsgrad.

| Familie | Beziehungen | Neue Parameter (Vorschlag) |
|---|---|---|
| Kreiselpumpe, Verdrängerpumpe, Verdichter, Ventilator | Pumpenleistung, Ähnlichkeit, Verknüpfung mit `chart.pump` | `flow`, `head` (oder `dp`), `power`, `efficiency`, `speed` |
| Wärmeübertrager (Rohrbündel, Platte, Luftkühler) | *Φ* = *U*·*A*·Δ*θ*_m, Energiebilanz beider Seiten | `duty`, `area`, `u`, vier Temperaturen, Ströme |
| Behälter, Kolonnen | Volumen, gespeicherte Wärme | `volume` (oder `diameter`, `height`) |
| Leitungen | Volumenstrom, Geschwindigkeit | `dn`/`innerDiameter`, `flow` |
| Armaturen | Blendenskalierung (*K*_v-Wert als Nutzereingabe) | `kv` (Ingenieur) |

### 3.7 Gebäudetechnik (`InventaSketch.Modules.BuildingServices`)

| Familie | Heute | Beziehungen | Neue Parameter (Vorschlag) |
|---|---|---|---|
| Rohr (`bs.pipe`) | `medium`, `dn` (mm, ohne Vorgabe), `height` | *v* aus *V̇* und *d*_i, Wärmestrom, Druckverlust | `innerDiameter` (sonst DN ≈), `flow`, `length` |
| Kanal (`bs.duct`) | `kind`, `width` (Zeichnung) | *v* aus *V̇*, *d*_h | `shape` rund/eckig, `height2` (Rechteckseite), `flow` |
| Kessel, Wärmepumpe | nur Höhen | Leistung, COP, Volumenstrom aus Δ*θ* | `power`, `flowTemp`, `returnTemp`, `cop` (Wärmepumpe) |
| Heizkörper, Thermostatventil | Höhen | Heizkörperleistung | `phi50`, `exponent`, `roomTemp` |
| Fußbodenheizkreis, Verteiler | `spacing`, `circuits` | Kreislänge, Volumenstrom | `area`, `heatFlux` |
| Pumpe, Mischer, Ausdehnungsgefäß | Höhen | Volumenstrom, Förderhöhe; Mischtemperatur; Ausdehnungsvolumen (Idee) | `flow`, `head` |
| Lüftungsgerät, Luftauslass | `kind` | Luftvolumenstrom, Wärmerückgewinnung | `flow`, `recovery` |
| Warmwasserbereiter (`bs.san.heater`) | `coil` | gespeicherte Wärme, Aufheizzeit | `volume`, `power` |
| Elektroinstallation (`bs.elec.*`) | Art | wie Hausinstallation (Elektro) | wie 3.1 |

### 3.8 Energie (`InventaSketch.Modules.Energy`)

Das Modul ist heute das reichste. Seine Eingabe `Act.Number` mit Begrenzung und seine Ausgabe `Fmt.Si` sind das Vorbild
für den gemeinsamen Parser und Formatierer.

| Familie | Heute | Beziehungen | Neue Parameter (Vorschlag) |
|---|---|---|---|
| PV-Modul | `power` (Wp, 430) | STC, Temperaturkorrektur | `voc`, `vmp`, `isc`, `imp`, `betaVoc`, `gammaP`, `alphaIsc`, `nmot` (Ingenieur) |
| PV-Strang | `power` (Wp), `modules` (2–24); zeigt „6 × 430 Wp" und die Summe | Strangspannung, höchste/kleinste Spannung | Verknüpfung mit den Modulwerten oder eigene Kopien; `tmin`, `tmax` (Standort) |
| PV-Feld | `power` (kWp), `rows`, `columns` (Zeichnung) | Feld = Stränge parallel | `strings` (Vorgabe = rows) |
| Windenergieanlage | `power` (MW), `rotor` | Windleistung, Betz | `diameter`, `windSpeed`, `cp` |
| Stromaggregat | `power` (kVA), `fuel` | *P* = *S*·cos *φ* | `pf` |
| Wechselrichter | `power` (kW), `phases`, `kind` | DC/AC-Verhältnis, *η*, AC-Strom, Spannungsfenster des Strangs | `udcMax`, `mpptMin`, `mpptMax`, `idcMax`, `efficiency` |
| Umrichter | `power`, `kind` | *η* | `efficiency` |
| Batteriespeicher | `capacity` (kWh), `soc` (%) | *E* = *U*·*Q*, C-Rate, Laufzeit | `voltage`, `ah`, `dod`, `cmax`, `roundTrip` |
| Ladepunkt, E-Auto | `power` (kW), `dc`; E-Auto `battery` (kWh) | Ladeleistung, Ladezeit | `current`, `voltage` (AC 3~ Vorgabe 400 V) |
| Gebäudelast | `power` (kW Spitze) | Lastbilanz | `annualEnergy` (kWh/a) |
| Netzeinspeisung, Transformator, Umspannwerk, Sammelschiene | `voltage` (kV), `hv`/`lv`, `power` (kVA/MVA) | Ströme je Seite, Spannungsabgleich | – (Werte vorhanden) |
| Kabel (`energy.line.cable`) | `voltage` (kV), `dc`, `conductors` | Spannungsfall, Verluste | `length`, `section`, `material`, `current` |
| Wärmepumpe, BHKW, Wärmespeicher | `power` (kW), `thermal`, `volume` (l) | COP, BHKW-Wirkungsgrad, gespeicherte Wärme | `cop`, `fuelPower`, `deltaT` |
| Elektrolyseur, Brennstoffzelle, H₂-Tank | `power`, `mass` (kg), `pressure` (bar) | H₂-Massenstrom, gespeicherte Energie | `efficiency` |
| Energieflusspfeil (`energy.line.flow`) | `weight` (Bandbreite, nur Grafik) | optional: Breite ∝ verknüpfte Leistung (Sankey-Art) | `value` (verknüpfte Leistung) |

### 3.9 Diagramme (`InventaSketch.Modules.Charts`) und Architektur

- `chart.torque` (`tmax`, `nbase`, `nmax`), `chart.efficiency` (`etamax`), `chart.pump` (`h0`, `qmax`, `hstat`, `qop`;
  ohne Einheiten; Betriebspunkt berechnet) und `chart.bode` (`fc`, `zeta`, `gain`) sind die ersten Ziele für
  Verknüpfungen. Ihre Parameter werden Größen mit Einheiten; das einheitenlose Pumpendiagramm erhält beim Verknüpfen
  Einheiten.
- Architekturräume haben ein Polygon. Fläche und Volumen (mit Raumhöhe) sind abgeleitete Werte, die Heizlast und
  Luftwechsel speisen (modulübergreifend).
- Bemaßungen (Annotation) messen Geometrie. Eine Bemaßung zwischen zwei Zahnradachsen zeigt den Achsabstand; in
  Erkunden löst das Ziehen nach Modul oder Zähnezahl auf, je nach Wahl des Nutzers (Abschnitt 1.2: Geometrie nur auf
  Wunsch).

---

## 4. Plausibilitätsprüfungen (Katalog)

Alle Prüfungen sind Hinweise: nur Overlay, abschaltbar, und sie ändern nie etwas. „Nutzergrenze" heißt: ein Wert, den
der Nutzer in den Prüfeinstellungen des Dokuments setzt, weil die verbindliche Grenze in einer Norm oder einem
Datenblatt steht, das wir nicht übernehmen.

| Id | Elemente | Regel | Schwere | Quelle |
|---|---|---|---|---|
| `mech.mesh.module` | kämmende Zahnräder | gleicher (Normal-)Modul und Eingriffswinkel | Warnung | [S16] |
| `mech.mesh.distance` | Radpaar mit Bemaßung oder eingegebenem *a* | *a* = *m*(*z*₁ ± *z*₂)/2 (Profilverschiebung nicht modelliert) | Hinweis | [S16] |
| `mech.gear.undercut` | Zahnrad | *z* < 2/sin²*α* (≈ 17 bei 20°) ohne Profilverschiebung | Info | [S18] |
| `mech.planetary.assembly` | Planetensatz | (*z*_s + *z*_H)/*N*_Planeten ganzzahlig; *z*_H − *z*_s gerade | Warnung | [S18, S39] |
| `mech.power.balance` | jede Kette | *P*_ab ≤ *P*_zu (mehr heraus als hinein ist unmöglich); *P*_ab ≈ *P*_zu·Π*η* | Warnung | [S1] |
| `mech.bearing.life` | Lager | *L*₁₀h unter der geforderten Lebensdauer des Nutzers | Hinweis | [S19] |
| `mech.belt.speed` | Riemen | *v* über der Nutzergrenze (typisch 30 m/s bei Keilriemen) | Hinweis | [S18] |
| `mech.screw.selflock` | Trapezgewindetrieb | Rücktrieb möglich, wenn *η* > 0,5 (Hinweis bei senkrechter Last) | Info | [S18] |
| `kin.grashof` | Viergelenk | kein Glied läuft voll um, aber ein Kurbelantrieb ist gesetzt | Hinweis | [S24] |
| `kin.dof` | Getriebe mit Antrieb | *F* ≠ Zahl der Antriebe | Hinweis | [S24] |
| `elec.coord.overload` | Schutzorgan + Kabel/Stromkreis | *I*_B ≤ *I*_n ≤ *I*_z verletzt (*I*_z vom Nutzer eingegeben) | Warnung | [S9] |
| `elec.rcd.additional` | Steckdosenstromkreis | kein RCD mit *I*_Δn ≤ 30 mA vor einem Steckdosenstromkreis gefunden | Hinweis (nationale Regeln unterscheiden sich) | [S10] |
| `elec.cable.drop` | Kabel | Δ*u* über der Nutzergrenze | Hinweis | [S8] |
| `elec.r.power` | Widerstand | *P* > *P*_Nenn | Warnung | [S7] |
| `elec.led.current` | LED | *I* > *I*_F,max des Nutzers oder Datenblatts | Warnung | [S7] |
| `elec.voltage.mismatch` | Element ↔ Kabel/Sammelschiene | Anschlussspannung ≠ Spannung der angeschlossenen Leitung (Energy-Anschlüsse tragen heute kV) | Warnung | – (Konsistenz) |
| `elec.kcl` | Knoten | Σ*I* ≠ 0 bei lauter eingegebenen Strömen | Warnung | [S7] |
| `mach.slip.sign` | Asynchronmaschine im Motorbetrieb | *n* > *n*_s (das ist Generatorbetrieb) | Info | [S15] |
| `mach.efficiency` | jeder Wandler | *η* > 1 oder *P*_ab > *P*_zu | Warnung | [S1] |
| `fluid.cyl.force` | Zylinder | geforderte *F* > *p*_max·*A*·*η*_hm (*p*_max aus Druckbegrenzungsventil oder Pumpe) | Warnung | [S26] |
| `fluid.velocity` | Leitung | *v* außerhalb des Nutzerbereichs je Leitungsart (Vorgaben aus Lehrbüchern: Saugleitung 0,5 … 1,5 m/s, Druckleitung 3 … 6 m/s, Rücklauf 2 … 4 m/s) | Hinweis | [S26] |
| `fluid.pressure.relief` | Pumpe ohne Druckbegrenzung im Kreis | keine Druckbegrenzung gefunden | Hinweis | [S25] (ISO 4413) |
| `heat.velocity` | Heizungs- / Wasserrohr | *v* außerhalb des Nutzerbereichs (Heizungsverteilung ≈ 0,3 … 1 m/s, Lehrbuch) | Hinweis | [S30] |
| `air.velocity` | Kanal | *v* außerhalb des Nutzerbereichs (Wohnung ≈ 2 … 4 m/s, Lehrbuch) | Hinweis | [S30] |
| `heat.cross` | Wärmeübertrager | Temperaturkreuzung (kalter Austritt über heißem Eintritt; Gleichstrom: kalter Austritt über heißem Austritt) | Warnung (unmöglich) | [S29] |
| `heat.cop.carnot` | Wärmepumpe | COP > COP_Carnot | Warnung (unmöglich) | [S29] |
| `heat.balance` | Wärmeerzeuger ↔ Verbraucher | Σ*Φ*_Verbraucher > *Φ*_Erzeuger | Hinweis | [S29] |
| `heat.roomload` | Heizkörper ↔ Raum | Σ*Φ* der Heizflächen im Raum < *Φ*_HL des Raums (Schätzung *q*·*A*) | Hinweis | [S30] |
| `pv.voc.max` | Strang ↔ Wechselrichter | *N*_s·*U*_oc(*θ*_min) > *U*_DC,max | Warnung | [S33] (IEC 62548-1) |
| `pv.mpp.min` | Strang ↔ Wechselrichter | *N*_s·*U*_mpp(*θ*_max) < MPPT-Minimum | Hinweis | [S33, S34] |
| `pv.isc.max` | Feld ↔ Wechselrichtereingang | *N*_p·*I*_sc > *I*_DC,max | Warnung | [S33] |
| `pv.dcac` | PV ↔ Wechselrichter | DC/AC-Verhältnis außerhalb des Nutzerbereichs (Vorgabe 0,8 … 1,5) | Info | [S34] |
| `bat.crate` | Batterie | C-Rate über dem Maximum des Nutzers oder Datenblatts | Hinweis | [S35] |
| `chart.nopoint` | verknüpftes Pumpen-/Anlagendiagramm | Kennlinien schneiden sich nicht (kein Betriebspunkt) | Hinweis | [S28] |
| `value.range` | jede Größe | Wert weit außerhalb des typischen Bereichs seiner Größe (Faktor 1000), z. B. 2300 V an einer Steckdose | Info („Tippfehler?") | – |

---

## 5. Diagramme mit Elementwerten verknüpfen

| Diagramm | Verknüpfte Elemente und Größen | Betriebspunkt | Zurückschreiben (Ziehen) |
|---|---|---|---|
| Drehmoment-Drehzahl (`chart.torque`) | Maschine: *M*_max, Eckdrehzahl, *n*_max, *P*; ASM: Kloss-Kurve aus *M*_k, *s*_k; Lastmoment aus der angetriebenen Kette | Schnitt von Motor- und Lastmoment | Ziehen des Punkts setzt Lastmoment oder Drehzahl |
| Wirkungsgradkennfeld (`chart.efficiency`) | Maschine *η*_max; Betriebspunkt (*n*, *M*) | Marke | – |
| Pumpe / Ventilator gegen Anlage (`chart.pump`) | Pumpe: *H*₀, *V̇*_max (oder Datenblattpunkte); Anlage: *H*_stat, *k* aus den Druckverlusten der angeschlossenen Rohre; Drehzahl über die Ähnlichkeitsgesetze | Schnittpunkt (vorhanden: `OperatingPoint`) | Ziehen des Punkts setzt den Förderstrom der Pumpe (vorhanden als Griff an `qop`) |
| PV-I-U und P-U (neue Vorlage) | Modul/Strang: *U*_oc, *I*_sc, *U*_mpp, *I*_mpp, *N*_s, *θ*, *G* (Eindiodenmodell, an die Datenblattpunkte angepasst [S34]) | MPP-Marke; MPPT-Fenster des Wechselrichters als Band; *U*_DC,max als Grenzlinie | Ziehen von Temperatur oder Einstrahlung verschiebt die Kurve (Erkunden) |
| Bode-Diagramm (`chart.bode`) | RC: *f*_g = 1/(2π*RC*); RLC: *f*₀, *ζ* = (*R*/2)√(*C*/*L*) | −3-dB-Marke | Ziehen von *f*_g ändert *C* oder *R* (nach Wahl des Nutzers; die andere Größe bleibt) |
| Sprungantwort (Funktionsdiagramm, PT1/PT2) | RC/RL: *τ*; RLC: *f*₀, *ζ* | 63 % bei *τ* | – |
| Drehzahl-/Drehmomentplan eines Getriebes (neue Vorlage) | Räderzug: je Welle *n*, *M*, *P* (Balken oder Stufen entlang des Zugs) | – | – |
| Heizkurve (neue Vorlage) | Wärmeerzeuger / Heizkreis: Auslegungspunkt, Raumtemperatur, Exponent | aktuelle Außentemperatur (Eingabe) | Ziehen des Auslegungspunkts ändert die Vorlauftemperatur |
| Erzeugungs-/Lastbilanz (neue Vorlage) | Energieelemente: Σ*P* Erzeugung gegen Σ*P* Last | – | – |

Verknüpfte Diagramme nutzen das Teilemodell von Diagramme v2 (`docs/de/design/charts-v2.md`). Der Betriebspunkt ist ein
Teil mit Griff, und die Verknüpfung erscheint als Kettensymbol in der Leiste des Diagramms mit „Verknüpft mit *Pumpe P1*
– entkoppeln".

---

## 6. Modulübergreifende Ketten

| Kette | Was fließt | Übertragungsbeziehungen | Module |
|---|---|---|---|
| E-Motor → Kupplung → Getriebe → Differenzial → Rad | *n*, *M* (→ *P*); am Rad *v*, *F* | Motor *P*_mech = *η*·*P*_el; Kupplung durchleitend; Getriebe *n*₂ = *n*₁/*i*, *M*₂ = *M*₁·*i*·*η*; Rad *v* = *ω*·*r*, *F* = *M*/*r* | Maschinen, Mechanik (`GearTrain`), Automotive |
| Batterie → Wechselrichter → Motor (Antriebsblöcke) | *U*, *I* (DC) → *U*, *I*, *f* (AC) → *n*, *M* | *P*_AC = *η*_WR·*P*_DC; *f* = *n*·*p*/60 | Maschinen, Energie |
| PV-Module → Strang → Wechselrichter → Verteiler → Stromkreise | *U*, *I* (DC) → *P*, *U*, *I* (AC) | Strangsummen; Wechselrichter *η*, DC/AC-Verhältnis; Verteiler: Σ*I* je Außenleiter; Stromkreise: Schutzkoordination und Spannungsfall | Energie, Elektro (Haus), Gebäudetechnik |
| Netz → Transformator → Sammelschiene → Abgänge → Lasten | *U*, *S*, *I* | idealer Transformator; Sammelschiene Σ*I*; Last *P*, cos *φ* | Energie, Elektro |
| Pumpe → Rohrnetz → Wärmeübertrager → Heizkörper / Fußbodenheizkreis | *V̇*, Δ*p* (→ *P*_hyd); *θ*_VL, *θ*_RL (→ *Φ*) | Pumpenkennlinie gegen Anlagenkennlinie; Rohr-Δ*p*; Wärmeübertrager LMTD; Heizkörperkennlinie; Abzweige mischen | Verfahrenstechnik, Gebäudetechnik, Energie (Wärmepumpe), Diagramme |
| Wärmepumpe → Speicher → Heizkreis → Räume | *Φ*, *θ*, *V̇* | COP; gespeichertes *Q*; Heizkurve; Raumheizlast aus der Fläche des Architekturraums | Energie, Gebäudetechnik, Architektur |
| Hydraulikaggregat → Ventile → Zylinder / Motor | *p*, *V̇* (→ *F*, *v* oder *M*, *n*) | Pumpe *V̇* = *V*_g·*n*; Druckbegrenzung begrenzt *p*; Zylinder *F*, *v*; Motor *M*, *n* | Fluid, Maschinen (Antriebsmotor) |
| Motorgetriebenes Koppelgetriebe | *n* → Abtriebsgeschwindigkeit, Hubzahl | kinematische Lagelösung, Ableitung | Kinematik, Maschinen |
| Lüftungsgerät → Kanäle → Luftauslässe → Räume | *V̇*, *θ* | Σ*V̇* an Abzweigen; Wärmerückgewinnung; Wärmestrom Luft | Gebäudetechnik, Architektur |

**Regeln.** Die Kette wird nur über vorhandene Verbindungen gefunden. Werte gehen nur zwischen passenden Medien
(`PortMedia.Fits`). Ein Anschluss ohne passende Größe beendet die Kette still; er rät nie. Der Löserbereich einer Kette
ist die zusammenhängende Komponente der Auswahl; der Hintergrunddurchlauf deckt die Folie ab. In den ersten Fassungen
wird nichts über Foliengrenzen weitergegeben.

### 6.1 Verknüpfung über Modulgrenzen (Core, 26.11.2026)

Jedes Modul baut seine eigenen Netze (`ValueNets`, je Modul eine `NetSpec`: Energie, Fluidtechnik, Verfahrenstechnik,
Gebäudetechnik). Wo ein Netz ein Element oder eine Leitung **eines anderen Moduls** erreicht – das Präfix der Typ-ID weicht
von `NetSpec.Prefix` ab und das Modul nennt dort keine Größe dieser Klasse –, hält das Netz eine **Übergangsstelle** fest
(`ValueNets.Junctions`, `NetJunction`). Die eigenen Beziehungen der Netze beachten sie nicht; eine Zeichnung, deren
Leitungen in einem Modul bleiben, wird genau wie bisher gelöst.

Der Löser (`ValueSolver`, Schritt 1b) verknüpft Übergangsstellen über einen gemeinsamen **physikalischen Schlüssel**
(`NetKeys`):

| Schlüssel | Größenart | Netzklassen, die als Schlüssel gelesen werden | Gesuchte Größen an einem Element ohne Netze |
|---|---|---|---|
| `U` | Spannung | `U`, `Uac` (Energie AC), `Ustr` (MPP-Spannung des Strings) | `U` |
| `I` | Strom | `I` | `I` |
| `P` | Leistung | `P`, `Pac`, `Pdc` | `Pel`, dann `P` |
| `f` | Frequenz | `f` | `f` |
| `n` | Drehzahl | `n` | `n` |
| `T` | Drehmoment | `T` | `T` |
| `Vdot` | Volumenstrom | `Vdot`, `Q` (Fluidtechnik) | `Vdot`, `Q` |
| `m` | Massenstrom | `m` | `m` |
| `p` | Druck | `p` | `p` |
| `theta` | Temperatur | `theta` | `theta` |

- **Welche Größe des fremden Elements.** Hat das Modul des fremden Elements im selben Löserdurchlauf Netze, nennen dessen
  eigene `NetSpec.PortVars` die Größe an diesem Anschluss (der Vorlaufanschluss eines Heizkörpers liefert `thf` für
  `theta`); genau eine muss passen, sonst wird nichts verknüpft. Andernfalls (Elektrotechnik, Maschinen, Mechanik,
  Schaltanlagen haben keine `NetSpec`) gilt die erste Größe aus der Liste des Schlüssels, die das Element hat (`Pel` vor
  `P`, ein Motor wird also mit seiner elektrischen Seite verknüpft).
- **Größenprüfung.** Jedes Mitglied des Netzes und die fremde Größe müssen die Größenart des Schlüssels haben; bei einer
  Abweichung bleibt die Übergangsstelle unverknüpft.
- **Regeln, vorsichtig.** Ein gemeinsamer Wert (`NetRule.Equal`: Spannung, Druck) verknüpft das ganze Netz. Ströme, Summen
  und Verteilungen nur einen unverzweigten Strang mit höchstens zwei Elementen insgesamt (wie `ValueNets` selbst); eine
  verzweigte Summe (Sammelschiene mit Einspeisungen) wird nicht über Modulgrenzen aufgeteilt.
- **Grenzwerte bleiben drinnen.** Klassen ohne Schlüssel (`UocMax`, `UmppMin`, `Isc`, `pmax`) werden nie verknüpft.
- **Bereich.** Leitungen eines anderen Moduls, die an einem Element im Bereich angedockt sind, holen die Elemente dieses
  Moduls mit Werten in den Bereich (auch über aneinander angedockte Leitungen dieses Moduls); so findet die Werteanzeige
  einer einzelnen Lampe den PV-String, der sie speist. Die Beziehung heißt `net.cross.<Schlüssel>` („… modulübergreifend
  weitergegeben"), die Werte gelten als *übernommen*.
- **Medien** werden beim Bauen der Leitungen geprüft (`PortMedia.Fits`, mit den Gleichsetzungen unten); die
  Werteverknüpfung selbst stützt sich auf das Andocken, den Schlüssel und die Größenart.

Getestet mit dem Standardkatalog (`tests/InventaSketch.Engine.Tests/CrossModuleValueTests.cs`): ein PV-String speist über
ein Energiekabel eine Lampe der Elektrotechnik (U = U_mpp,S, P = Stringleistung; I und R aus den Beziehungen der Lampe),
eine Prozessleitung gibt V̇ und θ an einen Heizkörper, ein Rohr der Gebäudetechnik an einer Prozesspumpe übernimmt deren
V̇, und ein Sammelschienennetz teilt U, aber nicht die summierte P. Die Antriebswelle einer Maschine und eine mechanische
Welle waren bereits über `Coupling` verknüpft (Maschinen `EMachineOnAShaftDrivesAGearStage`).

**Mehrere Medien an einem fremden Anschluss (Nacharbeit, 26.11.2026).** Ein Anschluss eines Elements eines anderen Moduls
ist kein Netzknoten, der Leitungen verschiedener Medien verbindet. Jedes Modul nennt das Medium seiner Leitungen
(`NetSpec.Medium`; ohne Angabe: die Typ-Id der Leitung); an einem fremden Anschluss treffen sich die Leitungen eines
Mediums (eine Verzweigung), Leitungen verschiedener Medien bleiben getrennte Netze. Erreichen mehrere Medien denselben
fremden Anschluss, wird nur das Netz des einen **führenden** Mediums (`NetSpec.Leads`) mit den Werten dieses Anschlusses
verknüpft; ohne genau ein führendes Medium keines. Anschlüsse, die die Spec selbst beschreibt (eine Größe oder ein
Durchgang der Klasse dort), sind wie bisher Mitglieder. Gebäudetechnik: Rohre nach ihrem Medium (`heating.flow`,
`heating.return`, Wasserarten), Kanäle nach ihrer Luftart; der Heizungsvorlauf führt, denn die Wärmeanschlüsse der
Energietechnik (ein Anschluss für Vor- und Rücklauf) geben die Vorlauftemperatur und den Volumenstrom. Eine
Wärmepumpe der Energietechnik mit Vorlauf- und Rücklaufrohr der Gebäudetechnik am Wärmeanschluss gibt also *V̇* und
*θ*_VL an die Vorlaufseite des Heizkörpers, seine Rücklauftemperatur bleibt seine eigene (vorher bildeten beide Rohre
ein Netz und *θ*_RL wurde aus *θ*_VL verteilt). Tests: `tests/InventaSketch.Core.Tests/ForeignPortMediaTests.cs`,
`tests/InventaSketch.Engine.Tests/ValueNetFollowUpTests.cs`.

**Raumwerte über Enthaltensein (Nacharbeit, 26.11.2026).** Elemente der Gebäudetechnik übernehmen die Werte des Raums
der Architektur (`arch.room`: *A*, *h*, *V* und die optionale Heizlast-Schätzung *Φ*_HL = *q*·*A*), in dem sie stehen.
Kein neues Core-Konzept: die eigenen `IValueLinks` der Gebäudetechnik-Typen finden den Raum, dessen Umriss die Mitte des
Elements auf derselben Seite enthält (bei verschachtelten Räumen den kleinsten; Räume werden über die Typ-Id gefunden,
Module referenzieren einander nicht), und verknüpfen die in der Spec des Elements genannten Größen über die Gleichung
`bs.room.link` („aus dem Raum übernommen"), wenn der Raum eine Größe mit gleichem Schlüssel und gleicher Größenart hat;
der Raum kommt dann in den Löserumfang. Der Luftdurchlass übernimmt *A*, *h* und *V*, seine Luftwechselzahl *n* ergibt
also *V̇* = *n*·*V*, ohne den Raum erneut einzugeben; der Heizkörper übernimmt *Φ*_HL, und der Hinweis `heat.roomload`
meldet einen Raum, dessen Heizflächen (Heizkörper, Fußbodenheizkreise) weniger als *Φ*_HL abgeben, wenn alle ihre
Leistungen bekannt sind. *Φ*_HL = *q*·*A* (*q* in W/m², optional am Raum) ist eine grobe Auslegungsschätzung mit
Handbuchwerten [S30], keine Heizlastberechnung nach EN 12831. Wird der Raum allein gelöst, kommen die Elemente darin
nicht mit (die Verknüpfung zeigt vom Element zum Raum).

**Gleichgesetzte Medien** (`PortMedia.Equivalents`, nur von `PortMedia.Fits` genutzt; `Covers` bleibt ein Präfixtest):
`thermal.heat` ≡ `heating` (Vor- und Rücklauf passen weiterhin nie zueinander, Kälte nie zu Heizung),
`process.water` ≡ `water`, `process.air` ≡ `air`, `process.gas` ≡ `gas`, `process.hydrogen` ≡ `hydrogen` (Wasserstoff ist
kein Erdgas), `process.refrigerant` ≡ `refrigerant`, `process.vacuum` ≡ `vacuum`, `air.compressed` ≡ `fluid.pneumatic`.
Neue Medien-IDs: `air.compressed`, `refrigerant`, `vacuum`, `optical` (Lichtwellenleiter, getrennt von Kupfer-`data`),
`mech.shaft` (Wortschatz für Wellenenden; Wellen docken geometrisch über Naben an). Das Farbschema „nach Medium" hat
Einträge für die ersten vier (Druckluft in der Luftfarbe nach DIN 2403, die übrigen als Konvention gekennzeichnet).

**Was Module übernehmen können**, um davon zu profitieren (hier nicht geändert, Dateien anderer Module):

- Verfahrenstechnik: Anschlussmedien nach Stoff angeben, wo bekannt (`process.water`, `process.air`, `process.gas`,
  `process.steam`, …), statt nur `process`, und `theta` an Pumpenanschlüssen ergänzen (`Through` gibt es schon weiter).
- Fluidtechnik: pneumatische Anschlüsse und Leitungen als `fluid.pneumatic` (dann passen Druckluftleitungen der Anlage);
  `Q` der Fluidtechnik wird bereits als Volumenstrom gelesen.
- Gebäudetechnik: ein Kältemedium (`thermal.cooling` oder `refrigerant`) für Kältemaschinen/Split-Geräte; Anschlüsse der
  Wärmepumpe können `refrigerant` für den Kältekreis angeben.
- Energie: den Wärmeanschlüssen Werte geben (`Vdot`, `theta`) und das Wärmerohr (`energy.line.pipe.heat`) in `IsLine`
  aufnehmen, damit Wärmenetze der Energie mit der Gebäudeheizung verknüpft werden; die Schlüssel ggf. über
  `NetSpec.Keys` ausdrücklich angeben.
- Elektrotechnik / Schaltanlagen: entweder eine `NetSpec` mit `PortVars` (Pins mit `U`, `I`, `P`) oder die schlichten
  Namen `U`, `I`, `P` an Elementen behalten, die über Leitungen anderer Module gespeist werden; die elektrische
  Aufnahmeleistung eines Motors als `Pel`.
- Maschinen / Mechanik: Wellenenden können `PortMedia.MechShaft` für die „+"-Angebote angeben; Werte laufen bereits über
  `Coupling`.

---

## 7. Umsetzungsplan

Jede Stufe folgt der Definition of Done aus CLAUDE.md:

- Modell, Darstellung, Persistenz-Rundlauf und Unit-Tests
- zweisprachige Namen
- Dokumentation EN/DE und Hinweise zu Host-Fähigkeiten
- Galeriefälle für Beschriftungen

Die Hosts kommen in dieser Reihenfolge: zuerst COM, dann Web (WebAssembly-Engine, dieselbe API), dann LibreOffice.

**Calc 0 – Größen und Einheiten (Core).** Neu sind `QuantityKind`, `Unit`, `UnitCatalog`, `ValueParser` (SI-Vorsätze,
Dezimalkomma, Buchstabencode nach IEC 60062, Einheiten, Brüche, Toleranzen) und `ValueFormat` (signifikante Stellen,
automatischer Vorsatz, Sprache). Die Parser von Charts, Energy und Gebäudetechnik delegieren daran, ihr Verhalten bleibt
erhalten.

*Abnahme:*
- Parse-/Formattests für `4k7`, `4R7`, `2n2`, `10 µF`, `10uF`, `1,5 kW`, `1500 rpm`, `1500/min`, `40 l/min`,
  `2,5 mm²`, `20 °C` (Temperatur) gegenüber `20 K` (Differenz), `230/12 V`, `4k7 ±5 %`
- eine Einheit der falschen Größenart wird mit zweisprachiger Meldung abgewiesen
- Rundlauf Formatieren → Parsen innerhalb der angezeigten Stellen
- `netstandard2.0` und `net10.0` bestehen beide
- die vorhandenen Eingabetests von Charts, Energy und Gebäudetechnik bestehen unverändert

**Calc 1 – Beziehungs-Engine (Core + Engine-API).** Neu sind `VarDef`, `Relation` (mit Quellen-Id und Toleranz) und
`ObjectType.Variables/Relations` (Vorgabe leer), dazu Anschlussbeziehungen und der Löser aus Abschnitt 1.8. Herkunft,
Widersprüche und Rechenweg gehören dazu. Engine-Aufrufe: `values`, `setValue`, `clearValue`, `explain`, `dragValue` und
`checks`. Eingegebene Werte liegen in Parametern (vorhandene Schlüssel mit ihren Einheiten); abgeleitete Werte werden nie
gespeichert.

*Abnahme:*
- je zwei beliebige von *U, I, R, P* bestimmen die anderen beiden
- drei stimmige Eingaben ergeben ✓; widersprüchliche ergeben einen Widerspruch, der beide Werte nennt und alle Parameter
  unverändert lässt
- Vorgaben weichen ohne Widerspruch
- „fehlt: eines von {…}" wird gemeldet
- der JSON-Rundlauf erhält eingegebene Werte und ihren Text
- die Zeitbudgets aus Abschnitt 1.8 werden in einem Benchmark-Test eingehalten
- die Ergebnisse sind deterministisch (gleiche Eingabe, gleicher Rechenweg)

**Ergänzung zu Calc 0/1 – Einheitenumrechner und Rechner** (Wunsch des Maintainers vom 25.11.2026).
`Units.Convert(wert, von, nach)` mit Prüfung der Größenart und Nullpunkten für °C/°F/K, die Anzeigeeinheiten je
Größenart, ein sicherer einheitenbewusster Ausdrucksrechner (`UnitCalculator`: „230 V * 16 A" → 3,68 kW, „12 mm + 0.5
in", „1500 rpm in rad/s", „4k7 || 10k", „2 kWh / 230 V" → Ah) und die Engine-Methoden `convertUnit`, `calc` und
`units`. Das Rechner-Popup (Menü *Mehr*, Wertekarte) kommt mit Calc 2.

*Stand:* Calc 0 und Calc 1 einschließlich Umrechner und Rechner sind fertig (Abschnitt 10).

**Calc 2 – COM-Oberfläche (Erkunden und Ingenieur).** Diese Stufe bringt:
- die Stufeneinstellung im Menüband
- die Schaltfläche *Werte* in der Leiste
- das Kreismenü *Bearbeiten ▸ Werte ▸*
- die Wertekarte beim Überfahren mit Bearbeitung an Ort und Stelle
- den Abschnitt *Werte* im Inspektor
- Beschriftungen am Element (Teil `values`)
- Griffe und Schieber an Werten mit Live-Aktualisierung
- die Schritt-für-Schritt-Formel
- Widerspruchszeichen
- einen Rückgängig-Schritt je Eingabe oder Ziehen; Esc bricht ab

*Abnahme (PowerPoint-Szenario `values-ohm`):*
- ein Widerstand erhält 230 V und 2 A; die Karte zeigt *R* = 115 Ω und *P* = 460 W mit Formeln
- Ziehen von *R* in Erkunden aktualisiert *I* und *P* live
- Loslassen ist ein Rückgängig-Schritt, und Esc stellt zurück
- eine exportierte Folie enthält nur die gewählten Beschriftungen, keine Karte und keine Zeichen
- in Aus erscheint nichts Neues

*Stand:* in COM fertig (Abschnitt 11) außer dem Abschnitt *Werte* im Inspektor, der Karte beim Überfahren und
Wertegriffen auf der Zeichnung beim Widerstand (der Griff sitzt in der Karte); Web und LibreOffice folgen.

**Calc 3 – Elektro und Maschinenelemente, erste Ketten und Prüfungen.** Diese Stufe bringt:
- die Elementbeziehungen aus den Abschnitten 2.1–2.6 und 3.1–3.4
- das Parsen des elektrischen `value`
- die Netzumformung (Reihe/parallel)
- Spannungsteiler und LED-Vorwiderstand
- Räderzüge mit absolutem *n*, *M*, *P* entlang `GearTrain` (mit Willis) und Wirkungsgraden
- Riemen-, Ketten- und Gewindetriebe, Federn, Lagerlebensdauer
- Motoren (*n*_s, Schlupf, *P*–*M*–*n*)
- die Kette der Antriebsblöcke
- die Prüfungen für Zahnräder, Leistungsbilanz, Überlast, RCD und Widerstandsleistung

*Abnahme:*
- ein dreistufiger Stirnradzug mit *z* und *n*_an, *M*_an zeigt *n*, *M* und *P* an jeder Welle, passend zu einer
  Handrechnung
- Modul 2 und 2,5 in einem Eingriff ergeben die Modulwarnung
- ein Brückennetz meldet „nicht zusammenfassbar"
- „4k7" an einem Widerstand in einem 5-V-Reihenkreis mit LED (2,1 V) ergibt *I* ≈ 0,62 mA
- ein LS B16 an einem Stromkreis mit *I*_z = 13 A (Nutzer) ergibt die Koordinationswarnung
- die Doku der Module Elektro, Mechanik und Maschinen listet die neuen Größen

*Stand (Maschinenelemente):* die Punkte zu Zahnrädern, Trieben, Federn, Lagern, Motoren und Antriebsblöcken samt ihren
Prüfungen sind erledigt, siehe Abschnitt 12.

**Calc 4 – Fluid, Wärme/Verfahren, Gebäudetechnik, Energie/Solar, modulübergreifende Ketten.** Diese Stufe bringt:
- die Beziehungen aus den Abschnitten 2.7–2.10 und 3.5–3.8
- Anschlussflüsse über `PortMedia`
- die übrigen Prüfungen

*Abnahme:*
- ein Strang aus 10 Modulen mit *U*_oc = 49,5 V, *β* = −0,27 %/K und *θ*_min = −10 °C zeigt *U*_oc,max ≈ 541,8 V und
  warnt gegen einen Wechselrichter mit *U*_DC,max 500 V
- eine Pumpe mit *V*_g = 10 cm³ bei 1450 min⁻¹ an einem Zylinder 50/28 mm bei 100 bar zeigt *F* und *v* in beiden
  Richtungen
- eine Heizkörperkette zeigt *Φ* aus Volumenstrom, Temperaturen und *Φ*₅₀/*n*
- Fläche und Luftwechsel eines Raums ergeben Luftvolumenstrom und Kanalgeschwindigkeit
- die Kette PV → Wechselrichter → Verteiler zeigt den AC-Strom je Außenleiter

*Stand:* alle fünf Abnahmepunkte sind in den Modulen Fluid, Prozess, Gebäudetechnik und Energie umgesetzt, mit Unit-Tests
(Abschnitt 13); der Verteiler ist die Sammelschiene oder der Zähler des Energiemoduls, kein Verteiler des Elektromoduls.

**Calc 5 – Verknüpfte Diagramme.** Diese Stufe bringt:
- die Verknüpfungen aus Abschnitt 5: Drehmoment-Drehzahl, Pumpe/Anlage, PV I-U/P-U, Bode RC/RLC, Sprungantwort,
  Getriebeplan, Heizkurve
- Betriebspunkte mit Griffen
- Zurückschreiben und Entkoppeln

*Abnahme:*
- eine Änderung von *H*₀ der Pumpe zeichnet das verknüpfte Diagramm neu und verschiebt den Betriebspunkt
- Ziehen des Punkts ändert den Förderstrom der Pumpe (ein Rückgängig-Schritt für Diagramm und Element)
- Entkoppeln friert die Werte ein
- das PV-Diagramm zeigt den MPP und das Fenster des Wechselrichters

**Calc 6 – Ingenieur-Extras.** Diese Stufe bringt:
- Toleranzen mit Intervallergebnissen
- das Rechenblatt: Tabelle auf der Folie (Annotation-Tabelle), CSV, Markdown/LaTeX
- einen Quellenbereich: jede Beziehung mit ihrem Eintrag [S*n*], zweisprachig
- Prüfeinstellungen (Nutzergrenzen, Stummschaltliste)
- Einheitenvorlieben (bar/MPa, min⁻¹/s⁻¹, kWh/MJ)
- die Entscheidung über Normreihen (E-Reihen, Vorzugsnennwerte) und deren Lizenz

*Abnahme:*
- ein Widerstand 4k7 ±5 % zeigt *I* als Intervall
- das Rechenblatt des Zahnradbeispiels listet Größe, Zeichen, Wert, Einheit, Formel und Quelle
- eine stummgeschaltete Prüfung bleibt nach Speichern und Neuladen stumm

**Web und LibreOffice.** Sie folgen nach jeder COM-Stufe; die Engine-API ist gemeinsam. Web: die Karte in der
Kontextleiste und im Aufgabenbereich, Griffe auf der Bühne. LibreOffice: zuerst das Werte-Panel in der Seitenleiste,
Griffe auf der Zeichenfläche mit dem geplanten Overlay.

*Abnahme:* dieselben Szenarien wie in COM ergeben dieselben Werte (die Engine-Tests sind hostfrei), und die
Host-Hinweise nennen, was fehlt.

**Parallele Arbeit.** Der Koordinator verantwortet Calc 0/1 (Core, Engine) und die Hosts. Später übernimmt je ein Agent
ein Modul (`src/InventaSketch.Modules.<X>/Values*.cs` mit Tests und Doku) und ergänzt Beziehungen und Prüfungen, ohne
den Core anzufassen.

---

## 8. Verhalten je Host (Übersicht)

| Host | Oberfläche Erkunden / Ingenieur | Griffe | Beschriftungen | Prüfungen |
|---|---|---|---|---|
| PowerPoint COM (zuerst) | Wertekarte als selbst gezeichnetes Popup neben der schwebenden Leiste; Abschnitt im Inspektor; Kreismenü *Bearbeiten ▸ Werte ▸* | vorhandene Overlay-Griffe (`DragHandles`) erweitert | nativer Textteil `values` in der Einheit (Formatierung mit PowerPoint-Werkzeugen wird wie bei anderen Teilen übernommen) | bernsteinfarbener Punkt im Overlay, Bereich *Prüfungen* |
| Office Web (WebAssembly) | Karte in der Kontextleisten-Ebene (`contextbar.ts`), Abschnitt im Aufgabenbereich | Griffe auf der Bühne | als SVG/Office-Formen | wie COM |
| LibreOffice | zuerst Panel in der Seitenleiste (UNO) | mit dem geplanten Overlay auf der Zeichenfläche | nativer Text | Liste in der Seitenleiste |

---

## 9. Quellen

Normen werden mit Nummer und Titel genannt; ihre Texte und Tabellen werden nicht wiedergegeben. Lehrbücher sind in
ihrer jeweils aktuellen Auflage gemeint.

- **[S1]** BIPM: *The International System of Units (SI)*, 9. Auflage, 2019.
- **[S2]** ISO 80000 / IEC 80000 *Größen und Einheiten* (Teile 1 Allgemeines, 3 Raum und Zeit, 4 Mechanik,
  5 Thermodynamik, 6 Elektromagnetismus).
- **[S3]** IEC 60027-1 *Formelzeichen für die Elektrotechnik – Teil 1: Allgemeines*.
- **[S4]** IEC 60062 *Kennzeichnung von Widerständen und Kondensatoren* (Buchstabencode mit dem Buchstaben als
  Dezimalzeichen).
- **[S5]** IEC 60063 *Vorzugsreihen für Widerstände und Kondensatoren* (E-Reihen; nicht übernommen, Abschnitt 1.3).
- **[S6]** IEC 60050 *Internationales Elektrotechnisches Wörterbuch* (Electropedia), Begriffe zu Leistung und
  Leistungsfaktor.
- **[S7]** Lehrbücher der Elektrotechnik und Elektronik: G. Hagmann, *Grundlagen der Elektrotechnik* (AULA);
  P. Horowitz, W. Hill, *The Art of Electronics*, 3. Aufl. (Cambridge University Press, 2015); U. Tietze, Ch. Schenk,
  E. Gamm, *Halbleiter-Schaltungstechnik* (Springer Vieweg).
- **[S8]** IEC 60364-5-52 *Errichten von Niederspannungsanlagen – Teil 5-52: Auswahl und Errichtung elektrischer
  Betriebsmittel – Kabel- und Leitungsanlagen* (konsolidierte Ausg. 3.1, 2024; Abschnitt 525 Spannungsfall, Anhang G
  informativ); DIN VDE 0100-520.
- **[S9]** IEC 60364-4-43:2023 *Schutzmaßnahmen – Schutz bei Überstrom* (Koordination von Leitern und
  Schutzeinrichtungen); DIN VDE 0100-430.
- **[S10]** IEC 60364-4-41 *Schutzmaßnahmen – Schutz gegen elektrischen Schlag* (zusätzlicher Schutz durch
  Fehlerstrom-Schutzeinrichtungen); DIN VDE 0100-410.
- **[S11]** IEC 60898-1 (Leitungsschutzschalter für Hausinstallationen), IEC 60269-1 (Niederspannungssicherungen),
  IEC 61008-1 / IEC 61009-1 (RCCB / RCBO).
- **[S12]** IEC 60228 *Leiter für Kabel und isolierte Leitungen*; IEC 60028 *Internationaler Normwert für den
  Widerstand von Kupfer*; IEC 60889 (hartgezogener Aluminiumdraht).
- **[S13]** IEC 60038 *IEC-Normspannungen*.
- **[S14]** IEC 60034-1 *Drehende elektrische Maschinen – Bemessung und Betriebsverhalten*; IEC 60034-30-1
  (Wirkungsgradklassen).
- **[S15]** R. Fischer, *Elektrische Maschinen* (Hanser); A. Hughes, B. Drury, *Electric Motors and Drives*, 5. Aufl.
  (Newnes, 2019).
- **[S16]** ISO 21771 (Stirnräder und Stirnradpaare mit Evolventenverzahnung – Begriffe und Geometrie); ISO 54
  (Moduln); ISO 53 (Bezugsprofil); DIN 3960.
- **[S17]** ISO 6336 (Tragfähigkeitsberechnung von Stirnrädern) – genannt als nicht im Umfang.
- **[S18]** Roloff/Matek, *Maschinenelemente* (Springer Vieweg); Niemann, Winter, Höhn, *Maschinenelemente* (Springer);
  K.-H. Decker, *Maschinenelemente* (Hanser).
- **[S19]** ISO 281:2007 *Wälzlager – Dynamische Tragzahlen und nominelle Lebensdauer*.
- **[S20]** ISO 5292 (Keil- und Keilrippenriemen – Berechnung der Nennleistung); ISO 606 (Kurzgliedrige
  Präzisions-Rollenketten und Kettenräder); ISO 10823 (Leitfaden zur Auswahl von Rollenkettentrieben).
- **[S21]** EN 13906-1/-2/-3 *Zylindrische Schraubenfedern aus runden Drähten und Stäben – Berechnung und Konstruktion*.
- **[S22]** DIN 743 *Tragfähigkeitsberechnung von Wellen und Achsen* – nicht im Umfang.
- **[S23]** ISO 3408 (Kugelgewindetriebe).
- **[S24]** H. Kerle, B. Corves, M. Hüsing, *Getriebetechnik* (Springer Vieweg); J. J. Uicker, G. R. Pennock,
  J. E. Shigley, *Theory of Machines and Mechanisms* (Oxford University Press).
- **[S25]** ISO 1219-1 (Schaltzeichen); ISO 4413 (Hydraulik – allgemeine Regeln und sicherheitstechnische
  Anforderungen); ISO 4414 (Pneumatik); ISO 3320 (Zylinderbohrungen und Kolbenstangendurchmesser – genannt, nicht
  übernommen); ISO 6358 (pneumatische Durchflusskennwerte).
- **[S26]** D. Will, N. Gebhardt, *Hydraulik – Grundlagen, Komponenten, Systeme* (Springer Vieweg); Bosch Rexroth,
  *Der Hydraulik-Trainer*, Band 1.
- **[S27]** F. M. White, *Fluid Mechanics* (McGraw-Hill); C. F. Colebrook (1939), J. Inst. Civil Eng. 11; L. F. Moody
  (1944), Trans. ASME 66.
- **[S28]** J. F. Gülich, *Kreiselpumpen* (Springer); ISO 9906 (Kreiselpumpen – hydraulische Abnahmeprüfungen).
- **[S29]** VDI e. V. (Hrsg.), *VDI-Wärmeatlas* (Springer); F. P. Incropera u. a., *Fundamentals of Heat and Mass
  Transfer* (Wiley).
- **[S30]** Recknagel, Sprenger, Albers, *Taschenbuch für Heizung + Klimatechnik* (aktuelle Ausgabe) –
  Handbuchwerte für Geschwindigkeiten und Wärmestrom Luft.
- **[S31]** EN 12831-1:2017 (Norm-Heizlast, Raumheizlast; Überarbeitung prEN 12831-1:2025 in Arbeit); EN 442-2
  (Raumheizkörper – Prüfverfahren und Leistungsangabe); EN 16798-1 / EN 16798-3 (Lüftung); DIN 1946-6 (Wohnungslüftung);
  DIN 1988-300 / EN 806-3 (Rohrweitenermittlung Trinkwasser).
- **[S32]** EN 14511 und EN 14825 (Wärmepumpen – Leistungsangabe, saisonale Effizienz).
- **[S33]** IEC 60904-3 (spektrale Bezugsbestrahlungsstärke, STC); IEC 60891:2021 (Temperatur- und
  Einstrahlungskorrektur gemessener I-U-Kennlinien); IEC 61215-2 (NMOT); EN 50380 (Datenblattangaben für PV-Module);
  IEC 62548-1:2023 (PV-Generatoren – Auslegungsanforderungen, Betriebstemperatur); IEC 61724-1 (Leistungsverhalten von
  PV-Anlagen, Performance Ratio); EN 50530 (Wirkungsgrad von Wechselrichtern).
- **[S34]** K. Mertens, *Photovoltaik: Lehrbuch zu Grundlagen, Technologie und Praxis* (Hanser; engl. Wiley);
  H. Häberlin, *Photovoltaics: System Design and Practice* (Wiley, 2012); W. De Soto, S. A. Klein, W. A. Beckman
  (2006), „Improvement and validation of a model for photovoltaic array performance", Solar Energy 80, 78–88;
  M. G. Villalva, J. R. Gazoli, E. R. Filho (2009), „Comprehensive approach to modeling and simulation of photovoltaic
  arrays", IEEE Trans. Power Electronics 24(5).
- **[S35]** IEC 61434 (Angabe von Strömen in Normen für Sekundärzellen, *I*_t = *C*_n/1 h); *Linden's Handbook of
  Batteries* (McGraw-Hill).
- **[S36]** E. Hau, *Windkraftanlagen* (Springer); A. Betz (1920), höchster Leistungsbeiwert 16/27.
- **[S37]** G. L. Steele, G. J. Sussman (1980), „Constraints – a language for expressing almost-hierarchical
  descriptions", Artificial Intelligence 14; H. Abelson, G. J. Sussman, *Structure and Interpretation of Computer
  Programs*, 2. Aufl., Abschnitt 3.3.5 „Propagation of constraints"; R. E. Moore, *Interval Analysis* (1966).
- **[S38]** D. C. Karnopp, D. L. Margolis, R. C. Rosenberg, *System Dynamics: Modeling, Simulation, and Control of
  Mechatronic Systems* (Wiley) – Leistungsgrößen (Potenzial × Fluss) über Domänen hinweg.
- **[S39]** H. W. Müller, *Die Umlaufgetriebe* (Springer) – Willis-Gleichung und Montagebedingungen von
  Planetengetrieben.

Vorhandene Code-Beziehungen, die hier wiederverwendet werden: `Linkages.Grubler` und `Linkage.Dof` (Kinematik, Core),
`GearTrain.Speeds` und `Links.Ratio` (Core), `Cycles.OttoEfficiency`/`DieselEfficiency` (Verfahrenstechnik) und
`PumpChart.OperatingPoint` (Diagramme).

---

## 10. Umsetzungsnotizen: Calc 0 und Calc 1 (25.11.2026)

Dieser Abschnitt hält die Entscheidungen fest, die das Konzept offen ließ. Maßgeblich ist der Code.

### 10.1 Dateien

| Bereich | Dateien |
|---|---|
| Größen, Einheiten, Suche, Umrechnung | `src/InventaSketch.Core/Quantities.cs` (`Dimension`, `QuantityKind`, `Unit`, `Quantities`), `Units.cs` (`UnitCatalog`, `Units`) |
| Parser und Formatierung | `src/InventaSketch.Core/ValueParser.cs` (`ValueParser`, `ParsedValue`, `Tolerance`, `ValueFormat`) |
| Rechner | `src/InventaSketch.Core/UnitCalculator.cs` |
| Variablen, Beziehungen, Haken | `src/InventaSketch.Core/Values.cs` (`VarDef`, `Relation`, `BoundRelation`, `IValueType`, `IValueLinks`, `IValueChecks`, `ValueModel`, `RootFinder`) |
| Löser | `src/InventaSketch.Core/ValueSolver.cs` (`ValueSolver`, `ValueTable`, `ValueConflict`, `ExplainStep`) |
| Engine-API | `src/InventaSketch.Engine/EngineValues.cs` (partielle `EngineApi`), DTOs in `Contracts.cs` |
| Beispiel | `src/InventaSketch.Modules.Electrical/ElecValues.cs` (Widerstand) |

### 10.2 Größen und Einheiten

- **Dimension** hat sechs Exponenten: Länge, Masse, Zeit, Strom, Temperatur und **ebener Winkel**. Im SI ist der
  Radiant 1; ihn mitzuführen trennt aber Drehzahl (min⁻¹, rad/s) von Frequenz (Hz) und lässt „1500 rpm in rad/s" mit 2π
  umrechnen. Intern steht eine Drehzahl in rad/s, damit gilt *P* = *T*·*n* im SI ohne Faktor 2π. Wo ein Produkt die
  SI-Sicht braucht (N·m × rad/s = W), fällt der Winkelexponent beim Benennen des Ergebnisses weg; rpm ↔ Hz bleibt
  abgelehnt.
- **Größenarten** (39): Verhältnis, Wirkungsgrad, Anzahl, Spannung, Strom, Leistung, Schein- und Blindleistung,
  Wärmestrom, Widerstand, Kapazität, Induktivität, Ladung, spezifischer Widerstand, Frequenz, Drehzahl, Winkel, Zeit,
  Länge, Fläche, Volumen, Masse, Kraft, Drehmoment, Energie, Geschwindigkeit, Beschleunigung, Druck, Volumenstrom,
  Massenstrom, Temperatur, Temperaturdifferenz, Dichte, spezifische Wärmekapazität, Wärmedurchgangskoeffizient,
  Bestrahlungsstärke, kinematische Viskosität, Trägheitsmoment, Federrate. Drehmoment und Energie, Leistung und
  Wärmestrom, Temperatur und Temperaturdifferenz teilen sich eine Dimension und sind getrennte Größenarten. Die erste
  Einheit einer Größenart ist ihre Anzeigeeinheit (Ladung: Ah, Drehzahl: min⁻¹, Temperatur: °C).
- **Einheiten** haben Faktor und Nullpunkt zum SI und eine ausdrückliche Liste der SI-Vorsätze, die sie annehmen und
  zeigen (Ω nimmt µ m k M G, F nimmt p n µ m). Quadratische und zusammengesetzte Einheiten (mm², kN·m, l/min) stehen
  einzeln in der Liste und nehmen keine Vorsätze.
- **Suchreihenfolge:** genaue Schreibweise, Vorsatz + genaue Schreibweise, dann ohne Groß-/Kleinschreibung (so gehen
  „10 KW" und „1010 MM" weiter). Schreibweisen werden vereinheitlicht: Leerzeichen und `^` fallen weg, Hochzahlen als
  Ziffern (mm2 = mm^2 = mm²), `*`, `⋅` und `.` zwischen Buchstaben als `·` (auch ohne: Nm = N·m), u als µ bei Vorsätzen,
  Ohm-Zeichen, `º` und `℃`.

### 10.3 Parser und Formatierung

- Eine **bloße Zahl** gilt in der Anzeigeeinheit des Feldes (`VarDef.Unit`). Ein **Buchstabencode** („4k7", „2n2",
  „R47") oder ein bloßer Vorsatz („10k", „2.5k") multipliziert die kohärente Einheit der Größenart (Ω, F, V …), wie der
  Code es meint. R als Dezimalzeichen gilt nur für Widerstände.
- **Brüche:** Ein `/` oder `:` mit folgender Zahl trennt Werte („230/12 V", „20/0,4"); ein `/` mit folgendem Buchstaben
  gehört zur Einheit („1500/min", „40 l/min"). Bei dimensionslosen Größenarten ist das Ergebnis der Quotient, sonst
  enthält `Parts` alle Werte und `Value` den ersten. Teile ohne Einheit übernehmen die zuletzt geschriebene Einheit.
- **Toleranzen:** „±5 %", „+/-5%", „−20/+80 %", „±0,1 V" (absolut) und ein nachgestelltes „1%" nach einem Wert („10k
  1%") bei Größenarten ohne eigene %-Einheit (auf einem Wirkungsgradfeld ist „97 %" der Wert).
- **Temperaturdifferenz** nimmt K; „°C" nur auf Deutsch (Abschnitt 1.3), angezeigt als K.
- **Zahlen:** Dezimalkomma oder -punkt; sind beide da, ist das spätere das Dezimalzeichen; wiederholte Trennzeichen sind
  Tausendertrennzeichen (die Regeln des bisherigen Diagramm-Parsers, jetzt `ValueParser.TryNumber`).
- **Anzeige:** `ValueFormat.Format` rundet auf signifikante Stellen (Vorgabe 3), wählt aus der Vorsatzliste der Einheit
  den Vorsatz, der die Zahl nach dem Runden in [1, 1000) hält (999,7 W → „1 kW"), streicht Endnullen, sofern sie nicht
  erhalten bleiben sollen (Formelzeilen behalten sie: „2,00 A"), und setzt auf Deutsch das Dezimalkomma. Formatieren →
  Einlesen ergibt den Wert innerhalb der gezeigten Stellen (für EN und DE getestet).

### 10.4 Einheitenumrechner und Rechner

- `Units.Convert(wert, von, nach)` prüft die **Größenart streng**: m → kg und auch N·m → J werden mit zweisprachiger
  Meldung abgelehnt (`TryConvert`). `Units.DisplayUnits(art)` listet die Einheiten einer Größenart.
- `UnitCalculator` ist ein rekursiv absteigender Parser (keine Codeausführung; höchstens 500 Zeichen und 64
  Schachtelungsebenen): Zahlen in beiden Sprachen und Buchstabencodes, alle Katalogeinheiten und Produkte/Quotienten
  daraus („kg·m/s²", „V/A", „m3/h"), `+ − · × * / ÷ ^ ² ³`, `||` oder `∥` (parallel), Klammern, sqrt/√, abs, sin, cos,
  tan (Bogenmaß oder Winkel), ln, lg/log, exp, π und eine nachgestellte Umrechnung „in/to/as/into/nach/->/→ Einheit"
  (die letzte, deren Rest eine Einheit ist, so bleibt bei „12 mm + 0.5 in" der Zoll erhalten). Er arbeitet
  **dimensionsbasiert**: Summen brauchen gleiche Dimensionen, Umrechnungen dieselbe Dimension (der Winkel darf
  wegfallen, außer bei rpm ↔ Hz).
- **Ergebniseinheit:** Eine Summe behält die zuerst geschriebene Einheit („12 mm + 0.5 in" → 24,7 mm); sonst benennt
  die erste Größenart mit der Dimension des Ergebnisses es (Leistung vor Wärmestrom, Energie vor Drehmoment) in ihrer
  Anzeigeeinheit mit passendem Vorsatz; ohne Größenart wird die kohärente SI-Einheit ausgeschrieben. Temperaturen in
  °C/°F sind absolut: ihre Differenz ist eine Differenz in K, eine addierte Differenz behält °C, zwei absolute
  Temperaturen lassen sich nicht addieren. Vorgabe sind 4 signifikante Stellen.
- Die Engine-Methode heißt `convertUnit`, weil `convert` schon Elemente in andere Typen umwandelt.

### 10.5 Beziehungs-Engine

- **Typvertrag.** Statt neuer virtueller Member an `ObjectType` (`Modules.cs` wird parallel bearbeitet) implementiert
  ein Typ `IValueType { Variables; Relations }`; `ValueModel.Variables(typ)` liefert für alle anderen Typen eine leere
  Liste. Das entspricht „Vorgabe leer" im Plan. Anschlussbeziehungen kommen aus `IValueLinks.ValueLinks(o, ctx)` (der
  Haken für Calc 3/4: Objekte, auf die eine Verknüpfung zeigt, kommen in den Löserumfang, in Breitensuche), Prüfungen aus
  `IValueChecks.ValueChecks`.
- **`VarDef`:** Schlüssel, zweisprachiger Name und Formelzeichen, Größenart, Anzeigeeinheit, gebundener Parameter
  (Vorgabe: der Schlüssel), gespeicherte Einheit eines Zahlparameters (z. B. kV), Relevanz, Vorgabewert (SI) und
  typischer Bereich (SI; zugleich das Intervall einer numerischen Lösung).
- **Speicherentscheidung (wie im Plan):** Eingegebene Werte liegen in den **Parametern**, ohne neues Modellfeld; alte
  Dokumente bleiben unverändert. Ein Textparameter behält den Text wie getippt („4k7 ±5 %", „230 V"); ein Zahlparameter
  hält die Zahl in seiner gespeicherten Einheit. Neue Wertparameter sind optionale Text-`ParamDef`s mit Einheitenhinweis.
  Gespeicherte Texte werden großzügig gelesen (deutsche Regeln); ein Text, der kein Wert der Größenart ist (z. B.
  „BC547"), gilt als nicht eingegeben, und der Fehler wird gemeldet. Abgeleitete Werte werden nie gespeichert.
- **`Relation`:** Id, zweisprachiger Name, Formel mit Schlüsseln in geschweiften Klammern („{U} = {R}·{I}"), explizite
  Löser je Variable mit ihrem Ausdruck („{U}/{I}", für den Rechenweg), optional ein Residuum für eine eingeschlossene
  Brent-Suche, relative Toleranz (Vorgabe 1e-6), Quellen-Id, Gültigkeit und `IsLink` für durchreichende
  Anschlussbeziehungen (`Relation.Equality`). Redundante Formen werden ausdrücklich angegeben (*P* = *U*·*I* = *I*²·*R* =
  *U*²/*R*), damit für die lokale Ausbreitung zwei beliebige Werte genügen.
- **Löser** (`ValueSolver.Solve(doc, registry, ids, options)`): Umfang = übergebene Ids (Vorgabe: erste Seite) plus
  verknüpfte Objekte; Beziehungen in Umfangsreihenfolge, lokale vor Verknüpfungen; eine Arbeitsliste löst jede Beziehung
  mit genau einer Unbekannten (jede Variable einmal); jeder Wert merkt sich Beziehung, Eingänge und die Menge der
  eingegebenen/vorgegebenen Werte, auf denen er beruht (seine *Wurzeln*). Eine vollständig bekannte Beziehung ist nur dann
  eine **unabhängige Prüfung**, wenn eine ihrer Variablen Information trägt, die die anderen nicht haben (ihre Wurzeln
  sind nicht in deren Wurzeln enthalten); eine bestandene unabhängige Prüfung ergibt den Status *geprüft* (✓), eine
  nicht bestandene einen **Widerspruch** mit dem Wert der Variablen, dem von der Beziehung verlangten Wert und den
  beteiligten Eingaben. Vorgabewerte werden nach der Ausbreitung einzeln versucht und zurückgenommen, wenn sie zu einem
  Widerspruch führen. `Missing(id)` nennt die kleinsten Mengen (bis drei) weiterer Variablen, die das Element bestimmen
  würden. Das Zeitbudget (Vorgabe 50 ms) beendet den Durchlauf und setzt `Truncated`. `Overrides` rechnen mit einem
  Wert, als wäre er eingegeben, ohne das Dokument zu ändern (Vorschau beim Ziehen).
- **Status je Element:** leer, unterbestimmt, bestimmt, geprüft, Widerspruch.
- **Rechenweg:** eine Zeile je abgeleitetem Wert der Kette, Eingänge zuerst: „R = U/I = 230 V/2,00 A = 115 Ω";
  eingegebene Werte wie getippt („R = 4k7"); numerische Lösungen „x = 2 (numerisch gelöst: y = x³ + x)".
- **Gemessen:** 1000 Widerstände mit je zwei Eingaben (4000 Beziehungen, 2000 gelesene Texte) brauchen unter .NET 10 im
  Release-Build auf einer ausgelasteten Entwicklungsmaschine etwa 26–35 ms (Test `ThousandResistorsSolveWithinTheBudget`:
  bester von sechs Läufen, Grenze 50 ms im Release-, 150 ms im Debug-Build). Das WebAssembly-Budget (200 ms) ist noch nicht
  gemessen.

### 10.6 Engine-API (ergänzend)

| Methode | Anfrage | Ergebnis |
|---|---|---|
| `values` | `session`, `id` oder `ids` (Vorgabe: Seite), `lang`, `digits` | `objects[]`: `id`, `status`, `hint` („Noch 1 Wert: R, I oder P", „✓", „Widerspruch: …"), `vars[]` (Schlüssel, Name, Formelzeichen, Größenart, Relevanz, Einheit + Faktor/Nullpunkt, Einheiten, Wert SI, Zahl, Anzeige, Text, Herkunft, Formel, Beziehung, Quelle, Toleranz, Fehler, Widerspruch, Parameter), `missing`, `conflicts[]` (Beziehung, Formel, Schlüssel, Wert/verlangter Wert mit Texten, Beteiligte, Meldung); `elapsedMs`, `truncated` |
| `setValue` | `id`, `key`, `text` | Zustand; ein Rückgängig-Schritt; falsche Größenart/unbekannte Einheit → zweisprachiger Fehler; leerer Text löscht |
| `clearValue` | `id`, `key` | Zustand; ein Rückgängig-Schritt |
| `explain` | `id`, `key`, `digits` | `steps[]` und `text` |
| `dragValue` | `id`, `key`, `value` (SI) oder `text`, `preview` | Vorschau: wie `values`, Dokument unverändert; Loslassen: speichert den formatierten Wert, ein Rückgängig-Schritt |
| `checks` | `ids` (Vorgabe: Seite) | verletzte Prüfungen (leer, bis Calc 3 Regeln bringt) |
| `convertUnit` | `value` + `fromUnit` + `unit` oder `text` (+ `quantityKind`) + `unit` | `value`, `unit`, `text`, `kind`, `units` |
| `calc` | `text`, `lang`, `digits` (Vorgabe 4) | `value` (SI), `dimension`, `kind`, `unit`, `number`, `text`, `steps` |
| `units` | `quantityKind` (optional) | Größenarten mit Name, Dimension und Anzeigeeinheiten |

### 10.7 Beispiel: Widerstand

`elec.resistor` hat *R* (aus dem vorhandenen Text `value`, „4k7" auf der Beschriftung ist also der Widerstand), *U*,
*I* und *P* (neue optionale Textparameter `voltage`, `current`, `power`). Zwei beliebige ergeben die anderen; drei
stimmige ergeben ✓; drei widersprüchliche ergeben einen Widerspruch, der beide Werte nennt und nichts ändert. Ein Text in
`value`, der kein Widerstand ist (z. B. „1/4 W"), wird für *R* nicht verwendet.

### 10.8 Parser, die jetzt weiterreichen

| Bisheriger Parser | Jetzt | Verhalten |
|---|---|---|
| Diagramme `Fmt.ParseInput`, `ChartData.TryNumber` | `ValueParser.ParseNumber`, `ValueParser.TryNumber` | derselbe Code, nach Core verschoben |
| Energie `Act.Number` | Zahl über `ValueParser.TryNumber` (Einheitensuffix und Begrenzung unverändert) | nimmt zusätzlich Tausendertrennzeichen |
| Gebäudetechnik `Bs.ParseMm`, Architektur `Arch.ParseMm` | `ValueParser.ParseMillimetres` (Architektur lehnt ≤ 0 weiter ab) | nimmt zusätzlich jede Längeneinheit (km, in, ft) und Leerzeichen als Tausendertrennzeichen |

Nicht umgestellt (anderer Zweck): Energie `Fmt.ParseRatio` (Spannungslisten in kV; `ValueParser` liest „20/0,4 kV"
genauso und kann sie ersetzen, wenn der Transformator Werte bekommt), `ChartBreaks.ParseInput` (Bereiche),
`FunctionExpression` (Diagrammformeln in x; der Rechner ist getrennt) und die Zahlprüfung beim Import in `Imports.cs`.

### 10.9 Offen für spätere Stufen

- Calc 2 (COM): fertig, siehe Abschnitt 11.
- Calc 6: Intervallergebnisse aus Toleranzen (Toleranzen werden gelesen und behalten, aber noch nicht weitergerechnet),
  Stummschaltliste, Einheitenvorlieben, Entscheidung zu den E-Reihen.
- Die Prüfung `value.range` („Tippfehler?") kann `VarDef.Min/Max` nutzen, sobald der Prüfkatalog beginnt (Calc 3).

### 10.10 Calc 3: Elektrotechnik (25.11.2026)

**Stand der elektrotechnischen Abnahmepunkte von Calc 3 (Abschnitt 7):** erledigt – Elementbeziehungen der Abschnitte
2.1–2.3 für die Elemente, die das Modul hat, Lesen des elektrischen `value`, Netzreduktion (Reihe/parallel),
Spannungsteiler und LED-Vorwiderstand („4k7" in einem 5-V-Kreis mit einer 2,1-V-LED ergibt *I* = 0,617 mA), die Brücke
meldet „nicht reduzierbar", ein LS-Schalter B16 mit *I*_z = 13 A ergibt die Warnung zur Schutzzuordnung, die Prüfungen
für Überlast, RCD und Widerstandsleistung, und die Modulbeschreibung Elektrotechnik listet die neuen Variablen. Variablen
und Prüfungen je Element stehen in [../modules/electrical.md](../modules/electrical.md) („Elementwerte").

| Bereich | Dateien |
|---|---|
| Variablen, Beziehungen, Kennzeichnungen, Prüfungen | `src/InventaSketch.Modules.Electrical/ElecValues.cs` |
| Schaltungsanalyse (Netze, Reihen-/Parallelreduktion, Kreisbeziehungen) | `src/InventaSketch.Modules.Electrical/ElecNetwork.cs` |
| Anknüpfung am Typ (`IValueLinks`, `IValueChecks`, `Marking`, `Net`, `Checks`) | `src/InventaSketch.Modules.Electrical/ElecType.cs` |
| Tests | `tests/InventaSketch.Modules.Electrical.Tests/CircuitValueTests.cs` |

Entscheidungen:

- **Bemessungswerte sind Parameter, keine Variablen.** *P*_rated, *I*_F,max und *I*_z sind einfache optionale
  Textparameter, die die Prüfungen lesen. Als Variablen würde keine Beziehung sie je bestimmen, und jedes Element bliebe
  „unterbestimmt".
- **Kennzeichnungen.** Ist der Werttext nicht die Größe des Elements selbst (Quellen, Lampen, LEDs, Schutzorgane,
  Transformator), wird jede darin gefundene Größe zu einer Beziehung mit einer Variablen dieses Elements
  („*I*_n = B16 = 16 A"), geliefert über `IValueLinks`. Der Text wird nie umgeschrieben; ein widersprechender eingegebener
  Wert ergibt einen Widerspruch. Passive Bauteile behalten die direkte Bindung aus Calc 1 (*R*, *C*, *L* = `value`).
- **Netze** entstehen wie beim Netzlistenexport (Andockungen mit Pin-Id, an Leitungen angedockte Abzweige), dazu
  Verbindungspunkte, geschlossene Kontakte (Schutzorgane gelten als geschlossen, außer sie sind ausdrücklich
  ausgeschaltet), gesteckte Steckverbindungen, Sammelschienen, Abzweigdosen, Verteiler, Zähler, Erdungszeichen einer Art
  und Netznamen gleichen Namens (ein einzelner Netzname führt woandershin und zählt als Anschluss mit unbekanntem Strom).
- **Reduktion** (Abschnitt 1.6): offene Zweige bleiben außen vor; parallele Zweige zwischen denselben Netzen; Reihe an
  einem Netz mit genau zwei Zweigen und sonst nichts; Quellen und passive Teile werden getrennt reduziert, dann schließt
  eine Quellengruppe an einer Lastgruppe den Stromkreis. Jede Zusammenfassung liefert Summenbeziehungen über
  Elementvariablen (Reihe: gleicher Strom; parallel: gleiche Spannung; geschlossener Kreis: beides), ohmsche oder
  kapazitive Gruppen den Ersatzwiderstand oder die Ersatzkapazität (Σ*U* = Σ*I*·*R*_ers, explizit nach *U* und *I*
  gelöst, numerisch nach Brent für ein *R*). Einpolige Verbraucher an einem Netz, das ein Zweig speist, ergeben den
  Knotensatz. Eine übrig bleibende Masche ohne Anschlüsse mit unbekanntem Strom markiert ihre Elemente für
  `elec.net.reducible`.
- **Bereich (Scope).** Die Beziehungen eines Stromkreises liefert ein Element (das erste nach Id mit Variablen); jedes
  andere Element liefert eine Zugehörigkeitsbeziehung ohne Löser („gleicher Stromkreis"), die nie etwas löst oder prüft,
  aber den Besitzer in den Lösungsbereich holt. So sehen die Karte und das Werte-Label eines einzelnen Elements den ganzen
  Stromkreis. Die Analyse wird je Dokumentinstanz zwischengespeichert.
- **Richtung.** Spannungen und Ströme sind Beträge in Verbraucherrichtung, gültig für Reihen-Parallel-Netze mit einer
  speisenden Quelle; Quellen in Reihe gelten als gleichsinnig.

Offen (Elektrotechnik): Wechselstrom-Impedanz, Blindwiderstände, Zeitkonstanten und Resonanz (braucht eine
Frequenzvariable, Calc-5-Diagramme); Ersatzinduktivität; Schleiferstellung des Potentiometers, Basiswiderstand des
Bipolartransistors und Verstärkung des Operationsverstärkers; Werte von Motor/Generator (Maschinen); Leitungswerte und
`elec.cable.drop`; `elec.kcl` (braucht Stromrichtungen); `elec.voltage.mismatch`. **Vorgeschlagene Core-Änderung:** Ein
Wert, den eine Beziehung ohne Eingänge löst (eine Kennzeichnung), sollte wie ein eingegebener Wert in sich selbst
wurzeln, damit zwei widersprüchliche Kennzeichnungen (eine 24-V-Lampe an einer 12-V-Sekundärwicklung) einen Widerspruch
ergeben; heute wird ein solches Paar nicht geprüft. Ebenfalls nützlich in Core: Symbole in elementübergreifenden Formeln
mit dem Kennzeichen des Elements („*U*(R1) = …"), da die Spannung jedes Widerstands *U* heißt.

---

### 10.11 Calc 6, Engine-Teil: Toleranzbereiche, Rechenblatt, Quellen (26.11.2026)

- **Toleranzbereiche.** Ein eingegebener Wert mit Toleranz („4k7 ±5 %“, „230 V ±10 %“) bekommt seine Grenzen; jeder
  daraus abgeleitete Wert den Bereich, den seine Beziehung an den Ecken der Eingangsbereiche ergibt (in der Reihenfolge
  der Ableitung, höchstens sechs Eingänge mit Bereich; exakt für Beziehungen, die in jedem Eingang monoton sind).
  `VarValue.Interval`; die Engine-Methode `values` liefert `lo`, `hi` und `range` („180 V … 220 V“). Abnahme: „4k7 ±5 %“
  im 5-V-LED-Kreis ergibt I = 0,617 mA im Bereich 0,588 … 0,650 mA (Test
  `AToleranceOnTheResistorGivesTheCurrentAsARange`).
- **Rechenblatt.** `CalcSheet` (Core) listet jeden bekannten Wert der gewählten Elemente: Element (Kennzeichen und
  Typ), Größe, Zeichen, Wert, Einheit (mit dem Vorsatz, in dem der Wert gezeigt wird), Bereich, Formel, Herkunft
  (eingegeben, berechnet, übernommen, Vorgabe) und Quelle; als CSV („;“ auf Deutsch, sonst „,“), Markdown und LaTeX
  (booktabs) mit den zitierten Quellen im Volltext oder als Zellen einer Beschriftungstabelle für die Folie.
  Engine-Methode `calcSheet` (`ids`, `mode` csv/markdown/latex/table) liefert `{ text, rows, sources }`. Abnahme: das
  Rechenblatt des dreistufigen Zahnradgetriebes listet Größe, Zeichen, Wert, Einheit, Formel und Quelle (Test
  `TheCalculationSheetOfTheGearExampleListsQuantitySymbolValueUnitFormulaAndSource`).
- **Quellen.** `ValueSources` (Core) enthält die Angaben aus Abschnitt 9 in beiden Sprachen (aus diesem Dokument
  erzeugt); ein Engine-Test prüft, dass jede Quelle, die eine Beziehung oder Prüfung nennt, darin steht.
- **Aufschriften.** Ein Wert aus einer Beziehung ohne Eingänge (eine Aufschrift wie „24 V“ auf einer Lampe) ist seine
  eigene Herkunft: zwei widersprechende Aufschriften ergeben einen Widerspruch, der beide nennt. Verknüpfungen, die
  beide Enden melden, werden einmal aufgenommen.
- **Prüfeinstellungen und Einheitenvorlieben (26.11.2026).** `DocumentSettings.MutedChecks` und `UnitPreferences`; der
  Solver überspringt stummgeschaltete Prüfungen, `ValueTable.UnitOf` zeigt jeden Wert in der bevorzugten Einheit seiner
  Größe (Karte, Wertebeschriftung, Rechenblatt, Erklärungen). Engine-Methoden `muteCheck` (`key`, `text` on/off) und
  `setUnitPreference` (`quantityKind`, `unit`, leer löscht); beides übersteht Speichern und Laden (Tests
  `CheckSettingsApiTests`, Calc-6-Abnahme „Stummschalten übersteht Speichern und Laden“).
- **COM (26.11.2026).** Mehr ▸ „Rechenblatt …“ und „Rechenblatt …“ in der Wertekarte (CSV, Markdown, LaTeX, Kopieren,
  Speichern, als Tabelle einfügen); die Wertekarte listet die Plausibilitätswarnungen des Elements mit Regel und Quelle
  und einem Link „Stummschalten“, das ⚠-Abzeichen erscheint auch bei Warnungen; Werte ▸ „Werte anzeigen in“ bietet
  bevorzugte Einheiten für Druck, Drehzahl, Energie und Leistung (Präsentations-Tags `ISK_MUTED`, `ISK_UNITS`).
  Szenarien `calc-sheet`, `check-settings`.
- **Offen (Calc 6):** eigene Grenzwerte für Prüfungen, ein eigener Quellenbereich (Rechenblatt und Karte nennen die
  Quellen), dieselbe Oberfläche in Web-Add-in und LibreOffice (die Engine-Methoden gibt es).
- **Entscheidung zu Normreihen (E-Reihen), 26.11.2026:** InventaSketch liefert keine E-Reihen oder andere
  Vorzugszahlen-Tabellen mit: sie sind Tabelleninhalt von IEC 60063 [S5], und CLAUDE.md verbietet das Kopieren von
  Normtabellen. Werte erscheinen mit drei gültigen Stellen und rasten beim Ziehen in dieser Auflösung ein; jeder Wert
  lässt sich eintippen. Soll später eine Reihe dazukommen, stammt sie aus einer Datei des Benutzers (ein Wert je Zeile),
  nie aus dem Repository.

## 11. Umsetzungsnotizen: Calc 2 in PowerPoint (COM) (25.11.2026)

Dieser Abschnitt hält die Entscheidungen fest, die der Plan offenließ. Maßgeblich ist der Code.

### 11.1 Dateien

| Bereich | Dateien |
|---|---|
| Stufeneinstellung | Core `Model.cs` (`ValueLevel`, `DocumentSettings.ValueLevel`); COM `OnSlide/ValueCardModel.cs` (`ValueLevelSetting`), `Connect.cs` (*Mehr ▸ Werte*) |
| Wertebeschriftung, Eingabe, Ziehen | Core `ValueLabels.cs` (`ValueLabels`, `ValueEdits`, `ValueDrag`), eingehängt in `Registry.RenderLocal` und `Registry.Parts` |
| Karte, Leiste, Kreismenü, Hinweiszeichen | COM `OnSlide/ValueCardModel.cs` (Inhalt, rein), `OnSlide/ValueCard.cs` (Popup), `OnSlide/OnSlideController.Values.cs` |
| Piktogramm | `IconIds.Values` = `cmd-values` („x="), in COM `IconPainter.SharedPaths` und Web `icons.ts` |
| Tests | Core `ValueLevelTests`, Elektro `ValueLabelTests`, AddIn `ValueCardTests`, PowerPoint-Szenario `tests/powerpoint-com/scenarios/values-ohm.ps1` |

### 11.2 Entscheidungen

- **Stufe.** In PowerPoint ist das Dokument die Präsentation, daher ist die Stufe ein Präsentations-Tag (`ISK_VALUES` =
  `quick`, `explore` oder `engineer`). Einheiten mit `DocumentSettings.ValueLevel` (aus dem Web-Host) sind der Rückfall,
  wenn das Tag fehlt. Core speichert die Schnellskizze als „nicht gesetzt"; alte Dokumente bleiben unverändert, und
  `valueLevel` wird nur für Erkunden und Ingenieur geschrieben. Der Schalter ist *Mehr ▸ Werte* mit drei Umschaltflächen,
  die aktuelle gedrückt, wie *Symbolgröße*.
- **Leiste.** In Erkunden und Ingenieur bekommt ein Element, dessen Typ Werte hat, *Werte* (Piktogramm `cmd-values`, mit
  Beschriftung) als Hauptaktion; das ist Gruppe 1 aus Abschnitt 1.5. Die bisherige Hauptaktion behält ihren
  Piktogrammplatz. In der Schnellskizze bleibt die Leiste unverändert.
- **Kreismenü.** *Bearbeiten ▸ Werte ▸* gibt es auf jeder Stufe, weil Werte auch in der Schnellskizze eingebbar bleiben.
  Es hat einen Eintrag je Variable („Spannung *U* … 230 V", „Leistung *P* = 460 W") und danach *Werte auf der Zeichnung
  zeigen*. Die acht Plätze bleiben, wie sie sind.
- **Karte.** Ein randloses Popup rechts neben dem Element. Esc, ein Klick daneben oder ein anderes Fenster schließt es,
  wie beim Einfügefeld. Eine Zeile je Variable:
  - Marke: ✎ eingegeben, = berechnet, ○ Vorgabe, ⇄ übernommen, ⚠ im Widerspruch
  - Symbol (kursiv), Name, Wertfeld (der gemeinsame Parser: „4k7", „230 V", „1,5 kW", deutsch und englisch), Einheit
    (was eine bloße Zahl bedeutet), Griff ↔ (nur Erkunden), × (löscht einen eingegebenen Wert)
  - unter einem berechneten Wert in Erkunden und Ingenieur seine Formelzeile („R = U/I = 230 V/2,00 A = 115 Ω");
    Ingenieur ergänzt die Quelle („[S7]") und eine vierte signifikante Stelle; die Schnellskizze zeigt nur die Werte.

  Die Statuszeile sagt ✓, was fehlt („Noch 1 Wert: R, I oder P") oder dass Eingaben sich widersprechen; jede
  Widerspruchszeile nennt beide Werte und die beteiligten Eingaben. Eine falsche Eingabe (ein Strom im Spannungsfeld)
  zeigt ihre Meldung unter der Zeile, nie in einem Meldungsfenster. Enter oder das Verlassen des Felds übernimmt den Wert
  (wie `setValue`: ein Rückgängig-Schritt, das Element wird neu gezeichnet); × löscht (`clearValue`). Darunter: *Rechner …*
  öffnet den Rechner mit dem gewählten Wert, und *Auf der Zeichnung zeigen* schaltet die Wertebeschriftung. Die Karte nutzt
  eine Schriftgröße; Nebensächliches ist grau.
- **Auf Abruf, nicht beim Überfahren.** Die Karte öffnet über die Leiste oder das Kreismenü statt beim Überfahren. So
  bleibt die Folie standardmäßig ruhig (interaction-ux.md), und eine Hover-Karte würde mit PowerPoints eigenen Griffen
  konkurrieren.
- **Live-Ziehen.** Keine Geometrie des Widerstands stellt *U*, *I* oder *R* dar; ein Griff auf der Zeichnung hätte keine
  Bedeutung. Der Griff sitzt deshalb in der Karte:
  - ↔ nach links oder rechts ziehen ändert den Wert logarithmisch (3 signifikante Stellen);
  - die anderen Zeilen und Formeln folgen live (Solver-Overrides, das Dokument bleibt unverändert);
  - Loslassen speichert den Wert als einen Rückgängig-Schritt, Esc stellt zurück.

  Ein dritter festgehaltener Wert überbestimmt ein Element mit zwei eingegebenen Werten. `ValueDrag` gibt dann die
  eingegebenen Werte nach dem gezogenen in der Reihenfolge des Typs frei, den letzten zuerst. Beispiel: Sind *U* und *I*
  eingegeben, gibt das Ziehen von *R* den Wert *I* frei; *I* und *P* folgen, und das Loslassen speichert *R* und löscht *I*
  im selben Rückgängig-Schritt. Griffe auf der Zeichnung bleiben geometrischen Werten vorbehalten (`DragHandles`: jetzt
  Diagramme; Zahnräder und Zylinder in Calc 3/4).
- **Wertebeschriftung.** Die Beschriftung ist der Teil `values`. `Registry.RenderLocal` zeichnet sie für jeden
  `IValueType`, solange der Parameter `showValues` an ist:
  - mittig unter allem, was das Element zeichnet (auch seine eigenen Beschriftungen), aufrecht bei jeder Drehung oder
    Spiegelung, Schrift 2,5 mm;
  - listet die bekannten Kartenwerte, lässt aber einen Wert weg, den der Typ eingegeben schon selbst zeigt
    (`VarDef.OnTypeLabel`: *R* des Widerstands ist sein Werttext „4k7");
  - `valueLabelKeys` („R,P") wählt die Variablen ausdrücklich (Modell und Tests; die Karte bietet den Schalter für das
    ganze Element);
  - zeigt eingegebene Texte mit Einheit wie getippt.

  Die Beschriftung ist Inhalt: Sie erscheint auf jeder Stufe, wird gedruckt und exportiert und bleibt beim Verschieben am
  Element. Der Widerstand führt `showValues` unter seinen Parametern, daher zeigt ihn auch der Inspektor.
- **Prüfungen.** Solange sich die eingegebenen Werte des ausgewählten Elements widersprechen, sitzt ein kleines
  bernsteinfarbenes ⚠ außen an seiner oberen rechten Ecke (Erkunden und Ingenieur). Es ist nur Overlay: nie exportiert,
  nie modal. Die Karte zeigt die Widerspruchszeilen.

### 11.3 Host-Hinweise (COM)

- Karte, Kreismenüeinträge und Hinweiszeichen wirken auf das ausgewählte Element; andere Elemente der Folie zeigen noch
  kein Hinweiszeichen.
- Die Wertebeschriftung ist nativer PowerPoint-Text in der Einheit, wie die anderen Beschriftungen.
- Automatisierung (UI-Tests): `SetValueLevel`, `ValueLevel`, `Values`, `SetValue`, `ClearValue`, `ShowValues`,
  `DragValue`, `ValueBadge`, `ValueCard`.

### 11.4 Offen

- Der Abschnitt *Werte* im Inspektor (Seitenleiste), die Liste *Prüfungen* und das Hinweiszeichen an jedem Element der
  Folie (mit den Prüfregeln aus Calc 3).
- Einzelne Variablen für die Beschriftung in der Karte wählen (das Modell kann es).
- Die Vorschau von `dragValue` in der Engine gibt noch keine Werte frei; der Web-Host sollte `ValueDrag` genauso nutzen.
- Einheitenwahl und Einstellung der signifikanten Stellen (Ingenieur, Calc 6); Richtungspfeile ↑↓ beim Ziehen.

---

## 12. Umsetzungsnotizen: Calc 3 Maschinenelemente (25.11.2026)

Dieser Abschnitt hält die Entscheidungen für den Maschinenteil von Calc 3 fest (Module Mechanik und Maschinen). Maßgeblich
ist der Code; die Moduldokus listen jede Größe ([mechanical-drives.md](../modules/mechanical-drives.md#technische-werte-calc-3),
[machines.md](../modules/machines.md#technische-werte-calc-3)).

### 12.1 Dateien

| Bereich | Dateien |
|---|---|
| Mechanik: Größen, Beziehungen, Wertparameter, Prüfungen | `src/InventaSketch.Modules.Mechanical/MechValues.cs`; `MechType` implementiert `IValueType`, `IValueLinks`, `IValueChecks` |
| Mechanik: Topologie und Anschlussbeziehungen | `src/InventaSketch.Modules.Mechanical/MechLinks.cs` (`MechTopology`, `MechLinks`) |
| Maschinen: Größen, Beziehungen, Prüfungen, Verknüpfung der Antriebsblöcke | `src/InventaSketch.Modules.Machines/MachValues.cs` (`MachValues`, `MachLinks`); `MachType` in `Kit.cs` |
| Tests | `tests/InventaSketch.Modules.Mechanical.Tests/ValueTests.cs`, `tests/InventaSketch.Modules.Machines.Tests/ValueTests.cs` |

### 12.2 Entscheidungen

- **Beträge.** Drehzahlen, Drehmomente und Leistungen von Zahnrädern, Wellen, Riemenscheiben, Kettenrädern, Maschinen und
  Antriebsblöcken sind Beträge; der Drehsinn ist kein Wert. Vorzeichenbehaftete Drehzahlen würden jeden Außeneingriff zum
  Vorzeichenkonflikt machen, wenn Nutzer auf beiden Seiten positive Drehzahlen eintragen. Nur der Planetensatz rechnet mit
  vorzeichenbehafteten Drehzahlen (gleicher Drehsinn positiv), weil die Willis-Gleichung sie braucht.
- **Topologie aus den Kopplungen des Kerns** (`Coupling.Graph`, die Beziehungen hinter „Verbundene auswählen – bewegt sich
  mit“), je Dokument und Registry zwischengespeichert:
  - *Wellenstränge*: Wellen, Kupplungen und die darauf festen Teile (getragen oder konzentrisch, Nabe nicht lose) haben
    dasselbe *n*. Lager übernehmen die Drehzahl ihrer Welle, verbinden aber nichts. Planetenträger, Ausgleichskörbe und
    Radsätze bleiben außen vor.
  - *Drehmoment entlang eines Wellenstrangs* wird unverändert weitergegeben, wenn höchstens zwei Teile Moment aufnehmen
    oder abgeben (Zahnräder, Riemenscheiben, Kettenräder, Spindeln, Maschinen); bei drei oder mehr ist die Aufteilung
    unbekannt, dann entsteht keine Momentverknüpfung.
  - *Eingriffe*: sich berührende Zahnräder (Stirnrad, Hohlrad, Kegelrad, Schnecke und Schneckenrad; das *z* der Schnecke
    ist ihre Gangzahl) halten *z*_a·*n*_a = *z*_b·*n*_b. Planeten auf einem Trägerbolzen bleiben außen vor, da Willis über
    getrennte Teile vorzeichenbehaftete Drehzahlen braucht.
  - *Trume*: Riemen halten *v* = π·*d*·*n* und *M* = *F*·*d*/2 an beiden Scheiben; Ketten halten *v* = *z*·*p*·*n* und
    *M* = *F*·*z*·*p*/(2π), dazu *z*₁·*n*₁ = *z*₂·*n*₂ und das Momentverhältnis, wenn keine Teilung eingetragen ist. Eine
    Mutter auf einer Spindel übernimmt deren *v* und *F*.
- **Stufenwirkungsgrad.** Das η eines Zahnrads ist der Wirkungsgrad des Eingriffs, in dem es das getriebene Rad ist
  (Vorgabe 1). Über einen Eingriff gilt *P*_b/η_b = *P*_a/η_a, also *M*_b = *M*_a·(*z*_b/*z*_a)·(η_b/η_a). Sind An- und
  Abtriebsmoment eingetragen, weicht die Vorgabe und der Löser bestimmt η.
- **Kette der Antriebsblöcke.** Ein Verbinder (jede Linie), dessen erstes angedocktes Ende an einem Block mit mechanischem
  Abtrieb und dessen letztes an einem Block mit mechanischem Antrieb sitzt, gibt *n* und *M* weiter: Motor/E-Maschine/
  Verbrennungsmotor → Getriebe (*n*₁ = *i*·*n*₂, *M*₂ = *M*₁·*i*·η) → Differential (ebenso) → Rad (*v* = ω·*r*,
  *F* = *M*/*r*). Antriebsstrangteile auf einer mechanischen Welle übernehmen deren Drehzahl. Beide Module verwenden für
  drehende Elemente die Schlüssel `n`, `T` und `P`, daher treffen sich die Verknüpfungen ohne Code-Abhängigkeit zwischen
  den Modulen.
- **Speicherung.** Eingetragene Werte liegen in optionalen Textparametern (im Ingenieurabschnitt des Inspektors);
  vorhandene Zahlenparameter werden genutzt: `teeth` (*z*), `module` (*m*, mm), `pitch` der Spindel (*P*_h, mm), `starts`
  (*z* der Schnecke), `poles`. Beim Einfügen werden `teeth` und `poles` wie bisher gespeichert und zählen daher als
  eingetragene Werte.
- **Lagerlebensdauer** *L*₁₀ ist eine Anzahl in Millionen Umdrehungen (eine Einheit „Umdrehung“ gibt es nicht); *L*₁₀h ist
  eine Zeit in Stunden.
- **Prüfungen** laufen in `ValueSolver.Checks`: `mech.mesh.module` [S16], `mech.power.balance` [S1],
  `mech.planetary.assembly` [S39], `mech.bearing.life` [S19], `mech.screw.selflock` [S18], `mach.slip.sign` [S15],
  `mach.efficiency` [S1] und **`mach.overload`** [S14] (Leistung oder Moment über der eingetragenen Bemessung *P*_N bzw.
  *M*_N). Die letzte ist neu: Abschnitt 4 listet sie nicht, der Prüfumfang von Calc 3 nennt aber „Überlast“.

### 12.3 Stand der Maschinenpunkte von Calc 3

- Erledigt: Räderzüge mit absolutem *n*, *M*, *P* und Wirkungsgraden (Abnahme: der dreistufige Stirnradzug stimmt mit der
  Handrechnung überein; Modul 2 und 2,5 ergeben die Modulwarnung); Planetensatz mit Willis; Riemen-, Ketten- und
  Gewindetriebe; Federn (*F* = *c*·*s*, *c* aus der Geometrie); Lagerlebensdauer; Motoren (*n*_s, Schlupf, *P*–*M*–*n*, η);
  die Kette der Antriebsblöcke; die genannten Prüfungen; die Moduldokus listen die Größen.
- Offen:
  - Willis über getrennte Teile (Sonne, Planeten auf einem Träger, Hohlrad, Träger als einzelne Elemente): braucht
    vorzeichenbehaftete Drehzahlen; vorerst das Element Planetensatz verwenden.
  - Momentaufteilung auf einem Wellenstrang mit drei oder mehr momentführenden Teilen (Verzweigungen).
  - Drehfeder (braucht im Kern eine Größenart Drehfederrate N·m/rad), Verknüpfung Zahnstange–Ritzel (die Zahnstange hat
    keinen Teilkreisanker), CVT, Ravigneaux, Ausgleichsradsatz, Well- und Zykloidgetriebe, Schwungradenergie,
    Rutschkupplung.
  - Generatorbetrieb der Maschinen (die Wirkungsgradbeziehung gilt für Motorbetrieb); Batterie, Wechselrichter und
    Zwischenkreis (Calc 4 mit Energie).
  - Kern: `ValueSolver.Solve` erzeugt seinen `RenderContext` ohne Registry, daher greifen `IValueLinks` auf
    `ObjectType.Owner` zurück (von allen Registries geteilt, in denen der Typ registriert ist). `ctx.For(registry)` im
    Löser (und bei den Aufrufern von `ValueSolver.Checks`) würde das exakt machen.

---

## 13. Umsetzungsnotizen: Calc 4 Fluid, Prozess, Gebäudetechnik, Energie (25.11.2026)

Dieser Abschnitt hält die Entscheidungen zu Calc 4 in den Modulen Fluid, Prozess, Gebäudetechnik und Energie fest. Maßgeblich
ist der Code; die Moduldokumente nennen jede Größe, Beziehung, Verknüpfung und Prüfung
([fluid.md](../modules/fluid.md#technische-werte-calc-4), [process.md](../modules/process.md#technische-werte-calc-4),
[building-services.md](../modules/building-services.md#technische-werte-calc-4),
[energy.md](../modules/energy.md#technische-werte-calc-4)).

### 13.1 Dateien

| Bereich | Dateien |
|---|---|
| Netztopologie und Anschlussbeziehungen (seit 26.11.2026 von den vier Modulen gemeinsam genutzt) | `src/InventaSketch.Core/ValueNets.cs` (`NetSpec`, `NetRule`, `PortVar`, `ValueNets`; Netze je Dokument und Spezifikation zwischengespeichert) |
| Fluid: Größen, Beziehungen, Anschlussrollen, Prüfungen | `src/InventaSketch.Modules.Fluid/FluidValues.cs`; `FluidType` und `FluidLineType` implementieren `IValueType`, `IValueLinks`, `IValueChecks` |
| Prozess | `src/InventaSketch.Modules.Process/ProcessValues.cs`; `ProcessType`, `ProcessLineType` |
| Gebäudetechnik | `src/InventaSketch.Modules.BuildingServices/BsValues.cs`; `BsBoxType`, `PipeType`, `DuctType` |
| Energie | `src/InventaSketch.Modules.Energy/EnergyValues.cs`; `EnergyType` |
| Tests | `tests/InventaSketch.Modules.{Fluid,Process,BuildingServices,Energy}.Tests/ValueTests.cs` |

### 13.2 Entscheidungen

- **Netze aus Andockungen.** Ein Netz ist, was die Leitungen des Moduls (Fluidleitung, Prozessleitung, Rohr und
  Luftleitung, Energiekabel) verbinden: Leitungsenden an Anschlüssen (Andockung mit Anschluss-Id) und an anderen Leitungen
  (Abzweige). Ein Ende an einer Schiene oder an einer Sammelschiene ohne Anschluss-Id zählt als Anschluss `*` des
  Elements. Elemente geben Wertklassen von Anschluss zu Anschluss weiter, wo sie durchlässig sind (Ventile, Kupplungen,
  die Anschlusspunkte einer Sammelschiene). Die Topologie wird je Dokument einmal aufgebaut (Dokumente sind
  unveränderlich) und zwischengespeichert. Jede Wertklasse hat eine Regel:
  - *Gleich* (Druck, Spannung): das ganze Netz teilt den Wert, Abzweige eingeschlossen.
  - *Strom* (Volumen- und Massenstrom, Stofftemperatur im Prozessmodul): gemeinsam, wenn höchstens zwei Elemente die
    Klasse im Netz führen; Leitungen übernehmen ihn nur auf einem unverzweigten Strang. Ein Abzweig zu einem Manometer oder
    Druckbegrenzungsventil hält die Elemente nicht auf, weil diese keinen Strom führen; eine Aufteilung auf zwei
    Verbraucher hält ihn auf (die Aufteilung ist unbekannt).
  - *Summe* (parallele PV-Strings, Einspeisungen an einem Verteiler, Heizkörper an einem Heizungsabzweig): ein als
    *Summe* markierter Anschluss ist die Summe der *Teil*-Anschlüsse seines Netzes; ein unverzweigter Strang mit zwei
    Elementen verhält sich wie *Strom*.
  - *Verteilen* (Stofftemperatur in der Gebäudetechnik): der eine speisende Austritt einer Verzweigung gibt seine
    Temperatur an jeden Eintritt; Mischung an einer Zusammenführung rechnet nur das Mischventil.

  Die Anschlussrollen folgen den Zeichenkonventionen der Module (Vorlaufanschluss des Wärmeerzeugers und Pumpenaustritt
  sind Summen, Heizkörper Teile; die Geräteseite b eines Zählers ist Summe, die Netzseite a Teil). Über einen Anschluss
  ohne Rolle geht nichts weiter.
- **Auswahlabhängige Beziehungen** (durchgehende Kolbenstange, ziehender Zylinder, Rechteckkanal, ein- oder dreiphasiger
  Wechselrichter, DC-Sammelschiene, DC-Ladepunkt) werden je Objekt in `IValueLinks.ValueLinks` als eigene Beziehungen des
  Elements gebunden, weil `IValueType.Relations` je Typ gilt.
- **Gespeicherte Vorgaben gelten als eingetragen.** Vorhandene Bemessungsparameter (Modul-Wp, Wechselrichter-kW,
  Sammelschienen-kV, Kanalbreite, Verlegeabstand …) werden beim Einfügen gespeichert und sind damit eingetragene Werte: ein
  String zeigt sofort seine STC-Leistung. Damit das mit echten Datenblättern keine Konflikte erzeugt, hat
  *P*_mpp = *U*_mpp·*I*_mpp 2 % Toleranz und *d*_i ≈ DN 50 % Toleranz (DN ist eine Nennweite). Die von Calc 4
  hinzugefügten Wertparameter sind optionale Texte ohne Vorgabe; ein neues Element trägt also keinen eigenen Wert.
- **Vorgaben, die weichen** (auf der Karte als *Vorgabe* gekennzeichnet): Wasser ρ = 1000 kg/m³ und *c* = 4,19 kJ/(kg·K),
  Wirkungsgrade von Pumpe und Motor 100 %, Heizkörperexponent *n* = 1,3 und Raumtemperatur 20 °C, Raumhöhe 2,5 m,
  Wechselrichter η = 97 % (typisch, innerhalb der 0,95–0,98 aus Abschnitt 2.10), AC-Netzspannung 400 V, cos φ = 1,
  Luftdichte 1,225 kg/m³.
- **Betriebspunkt des Heizkörpers.** Sind Volumenstrom, Vorlauf-, Raumtemperatur und Φ₅₀/*n* bekannt, folgt die
  Rücklauftemperatur aus ρ·*c*·*V̇*·(θ_VL − θ_RL) = Φ₅₀·(Δθ_ln/50 K)^*n* (zwei Unbekannte, eine Gleichung nach Elimination von
  Φ). Die Beziehung ist ein Residuum, das Brent im Temperaturbereich löst; die Übertemperatur wird für θ_RL ≤ θ_i als 0
  fortgesetzt, damit das Intervall immer einen Vorzeichenwechsel hat.
- **Vorzeichen am Wärmeübertrager.** Seite 1 und Seite 2 statt warm und kalt: Φ > 0 heißt Wärme von Seite 1 zu Seite 2;
  ist Seite 1 kälter, wechseln alle Differenzen das Vorzeichen und die Gegenstrom-LMTD bleibt gültig. `heat.cross` meldet
  sich, wenn die beiden Enddifferenzen verschiedene Vorzeichen haben.
- **Temperaturkoeffizient und Luftwechsel.** Core hat keine Größenart für %/K oder h⁻¹. β ist ein Verhältnis in %, gelesen
  als „je Kelvin“ („−0,27“ oder „−0,27 %“), die Luftwechselzahl eine reine Zahl je Stunde („1“). Eingaben wie „−0,27 %/K“
  oder „1/h“ lehnt der Parser ab. Siehe 13.4.
- **AC-Strom.** *U*_AC ist in beiden Fällen die Außenleiterspannung des Netzes: dreiphasig *I* = *P*/(√3·*U*·cos φ),
  einphasig (Außenleiter gegen Neutralleiter) *I* = *P*/((*U*/√3)·cos φ). Verteiler (Sammelschiene, Zähler) setzen eine
  symmetrische Drehstromlast voraus.
- **Prüfungen** laufen in `ValueSolver.Checks`: `fluid.cyl.force`, `fluid.velocity`, `fluid.pressure.relief` [S25, S26];
  `heat.cross` [S29]; `heat.velocity`, `air.velocity` [S30], `heat.cop.carnot`, `heat.balance` [S29], `heat.roomload` [S30]; `pv.voc.max`,
  `pv.mpp.min`, `pv.isc.max` [S33], `pv.dcac` [S34], `bat.crate` [S35]. **`wind.betz`** [S36] (*c*_p über 16/27) ist neu:
  Abschnitt 4 nennt sie nicht, aber die Betz-Grenze aus Abschnitt 2.10 macht höhere Werte unmöglich. Die
  Lehrbuch-Geschwindigkeitsbereiche sind die Vorgaben aus Abschnitt 4, bis es Nutzergrenzen gibt (Calc 6). `fluid.velocity`
  leitet die Funktion der Leitung aus ihren Andockungen ab (S einer Pumpe: Saugleitung; Tank, T/R oder Leckölleitung:
  Rücklauf; sonst Druckleitung).

### 13.3 Stand der Punkte von Calc 4

- Umgesetzt: die fünf Abnahmepunkte (Unit-Tests mit Handrechnung):
  - 10 Module, *U*_oc = 49,5 V, β = −0,27 %/K, θ_min = −10 °C: *U*_oc,max = 541,78 V; `pv.voc.max` gegen
    *U*_DC,max = 500 V;
  - Pumpe 10 cm³ bei 1450 min⁻¹ (14,5 l/min) über ein 4/3-Wegeventil an einen Zylinder 50/28 mm bei 100 bar:
    *F*_aus = 19,63 kN, *F*_ein = 13,48 kN, *v*_aus = 0,123 m/s, *v*_ein = 0,179 m/s, mit dem Druck an der Pumpe oder am
    Zylinder eingetragen;
  - Heizkessel (55 °C) → Rohr (50 l/h) → Heizkörper (Φ₅₀ = 1000 W, *n* = 1,3): θ_RL ≈ 46,0 °C, Φ ≈ 521 W, zurück zum
    Kessel;
  - Raum 20 m², *n* = 1 h⁻¹: 50 m³/h an Luftdurchlass, Luftleitung und Gerät, 2,76 m/s in einer Leitung mit 80 mm;
  - 10 × 400 Wp → Wechselrichter (η = 96 %) → 0,4-kV-Sammelschiene: 3,84 kW, 5,54 A je Außenleiter (16,6 A einphasig).
- Umgesetzt: die in den Moduldokumenten genannten Beziehungen aus den Abschnitten 2.7–2.10 und 3.5–3.8, Werte über
  Anschlüsse in jedem Modul, die Katalogprüfungen aus Abschnitt 4 für diese Fachgebiete, und jedes Bibliothekselement lässt
  sich weiterhin ohne Werte einfügen und löst keine Prüfung aus (je Modul getestet).
- Offen:
  - Druckverlust in Rohren (Darcy–Weisbach, Colebrook) und Blenden-/Ventilskalierung (*K*_v, *Q*_nenn);
    Druckluftverbrauch; Verdichter und Turbinen (Gaseigenschaften).
  - Heizkurve, Heizlast (Idee von EN 12831-1), Summen am Heizkreisverteiler, Warmwasserzirkulation.
  - PV-Einstrahlung und Zelltemperatur (NMOT), Ertragsabschätzung, BHKW, Elektrolyseur und Brennstoffzelle,
    Wasserstofftank, Spannungsfall der Kabel im Energiemodul, Ladezeit von E-Fahrzeugen.
  - Raumwerte aus einem Architekturraum (Fläche, Höhe) statt am Luftdurchlass: dafür muss das Architekturmodul seine
    Fläche als Größe mit gemeinsamem Schlüssel bereitstellen.
  - Richtungen: Netze sind ungerichtet; Mischung an einer Zusammenführung und die Aufteilung eines Stroms ohne Summen
    brauchen Fließrichtungen (Pfeile oder Leitungsrichtung), die das Modell noch nicht nutzt.

### 13.4 Hilfreiche Änderungen im Kern (hier nicht vorgenommen)

- **Größenarten:** ein Temperaturkoeffizient (%/K, 1/K, mV/K) und eine Luftwechseleinheit h⁻¹ (1/h) für `Frequency`;
  dann nehmen β und *n* ihre Einheiten bei Ein- und Ausgabe an.
- **Ein gemeinsamer Netzhelfer:** erledigt 26.11.2026 – `ValueNets` liegt jetzt in Core
  (`src/InventaSketch.Core/ValueNets.cs`) und ersetzt die vier Kopien; das Elektromodul könnte ihn ebenfalls nutzen (es
  hat für die Reihen-/Parallelreduktion sein eigenes `ElecNetwork`).
- **Doppelte Verknüpfungen:** der Löser sammelt die Anschlussbeziehungen jedes Objekts im Umfang, eine Verknüpfung zwischen
  zwei Objekten kommt also zweimal hinzu (von jedem einmal). Das schadet nicht (die zweite Kopie ist nie unabhängig), eine
  Entdoppelung nach Referenz in `ValueSolver.Solve` würde die Verknüpfungsbeziehungen aber halbieren.

---

## 14. Umsetzungsnotizen: Calc 5 verknüpfte Diagramme (26.11.2026)

Dieser Abschnitt hält die Entscheidungen für Calc 5 fest (Abschnitt 5 und die Calc-5-Punkte von Abschnitt 7). Maßgeblich
ist der Code; die Diagrammseite beschreibt [charts.md](../modules/charts.md#verknüpfte-kennlinien-calc-5).

### 14.1 Dateien

| Bereich | Dateien |
|---|---|
| Kernvertrag: Kennliniensätze, Punkte, Schreibvorgänge, Verknüpfungen, Griffe auf Dokumentebene | `src/InventaSketch.Core/Characteristics.cs` (`ICharacteristics`, `CharacteristicSet`, `CharacteristicPoint`, `ValueWrite`, `Characteristics`, `IDocumentHandles`, `DocumentHandles`) |
| Pumpen- und Anlagenkennlinie, von zwei Modulen genutzt | `src/InventaSketch.Core/CharacteristicKit.cs` |
| Verknüpftes Diagramm, Aufbau als Bild, „Mit Element verknüpfen ▸“ | `src/InventaSketch.Modules.Charts/LinkedChart.cs` (`LinkedChart`, `LinkedFigure`, `ChartLinks`); `ChartType.Actions(o, ctx)`, Registrierung in `ChartsModule`, Export in `ExportBuilder` |
| Kennlinienlieferanten (partielle Klassen der Modultypen) | `Process/PumpCurves.cs`, `BuildingServices/HeatingCurves.cs`, `Machines/MachCurves.cs`, `Energy/PvCurves.cs`, `Electrical/ElecCurves.cs`, `Mechanical/GearCurves.cs` |
| Einhängepunkte in den Typklassen | `partial` an `ProcessType`, `BsBoxType`, `MachType`, `EnergyType`, `ElecType`, `MechType`; `.Concat(CurveParams)` in `Parameters` von `ProcessType`, `BsBoxType` und `MachType` |
| Tests | `tests/InventaSketch.Modules.Charts.Tests/LinkedChartTests.cs` (Vertrag mit einem Testelement), `CurveTests.cs` in den Testprojekten von Prozess, Gebäudetechnik, Maschinen, Energie, Elektrotechnik und Mechanik, `tests/InventaSketch.Engine.Tests/LinkedChartAcceptTests.cs` (Abnahmepunkte mit dem ganzen Katalog) |

### 14.2 Entscheidungen

- **Vertrag statt Modulverweise.** Module hängen nie voneinander ab, daher liest das Diagrammmodul Kennlinien nur über
  `ICharacteristics` im Kern: Ein Typ listet die Sätze, die ein Element anbietet (`CharacteristicSets`, darf vom Dokument
  abhängen: ein Widerstand bietet das Bode-Diagramm nur in einem Stromkreis mit Kondensator oder Spule), und berechnet
  einen aus einem `CharacteristicContext` (Element, Dokument, Registry und die gelösten Werte seines Löserumfangs). Ein
  Satz hat eine x-Achse, je Panel eine y-Achse (Beschriftung und Anzeigeeinheit; die Zahlen der Kurven sind in diesen
  Einheiten), Kurven (Punktlisten), Punkte, Bänder, Grenzlinien und Hinweise. Alle Namen sind zweisprachig.
- **Die Verknüpfung ist ein Parameter.** `chart.linked` speichert `link` = „&lt;Element-Id&gt;#&lt;Satz-Schlüssel&gt;“ und
  sonst nichts von den Kurven. `Characteristics.Resolve` löst das Element (Budget 200 ms) und berechnet den Satz; das
  Ergebnis wird je Dokumentinstanz und Registry zwischengespeichert (Dokumente sind unveränderlich), daher löst ein
  Zeichendurchgang mit mehreren Aufrufen nur einmal.
- **Neuzeichnen.** Die Elementtypen geben ihre verknüpften Diagramme aus `Dependents` zurück und setzen
  `HasDependents` (nur die Instanzen, die Sätze anbieten; sie zeichnen für sich, daher behält PowerPoint ihre Form beim
  Verschieben); das Diagramm setzt `DependsOnNeighbours`, es zeichnet aus seinem Element. `ChangeSet` der Engine und die abhängigen Einheiten des
  COM-Hosts zeichnen das Diagramm dann im Rückgängig-Schritt der Elementänderung neu. Ein Wert des Elements, der sich
  ändert, weil sich ein *Nachbar* geändert hat (ein über ein Rohr übernommener Volumenstrom), erreicht das Diagramm erst,
  wenn Element oder Diagramm neu gezeichnet werden (offen, 14.4).
- **Zeichnen als Bild.** Das verknüpfte Diagramm baut aus dem Satz eine Bildspezifikation (`LinkedFigure`) und zeichnet
  sie mit dem Bildsetzer, daher sind Achsen, Panels, Legende, Teile und Teilformate die von `chart.figure`. Punkte
  werden Ein-Punkt-Reihen mit Marker (der Griff findet den Marker über sein Teilkennzeichen). Lösen speichert genau
  diese Spezifikation in einem `chart.figure` (Lage, Größe und Teilformate bleiben): das eingefrorene Diagramm ist ein
  gewöhnliches Bild.
- **Griffe schreiben Werte, nicht das Diagramm.** Ein Punkt trägt die Bewegung (entlang x oder y) und eine Funktion von
  der neuen Koordinate zu `ValueWrite`s. `Characteristics.Apply` speichert Variablen wie einen gezogenen Wert
  (`ValueDrag.Plan` + `Apply`, drei signifikante Stellen; eingegebene Werte, die das Element überbestimmen würden,
  weichen) und Parameter als formatierten Text. Wo die Kurve auf angenommenen Daten beruht (V̇_max der Pumpe,
  Bemessungspunkt eines Motors), speichert das erste Ziehen sie mit, damit die Kurve unter dem Zeiger nicht wandert.
  Das Diagramm bleibt beim Ziehen unverändert; die Dokumentänderung betrifft nur das Element, das Diagramm folgt als
  abhängiges Objekt: ein Rückgängig-Schritt für beide.
- **Griffe auf Dokumentebene.** `ObjectType.MoveHandle` gibt nur das Objekt zurück. `IDocumentHandles.MoveHandleInDocument`
  (Kern) gibt das Dokument zurück; `DocumentHandles.Move` ruft es auf und fällt für alle anderen Typen auf `MoveHandle`
  zurück. Hosts und `moveHandle` der Engine müssen es aufrufen (14.4); bis dahin bewirken die Griffe der verknüpften
  Kennlinie in den Hosts nichts (keine Scheinwirkung: die objektbezogene Bewegung gibt das Diagramm unverändert zurück).
- **Einfügen und Verknüpfen.** **Kennlinie ▸** am Element ist eine Menüauswahl (`values.chart`, ein Befehl je Satz,
  `ApplyInDocument`), die das Diagramm rechts neben dem Element einfügt; das Diagramm wird über die Registry erzeugt
  (`ctx.Registry ?? type.Owner`), daher braucht das Modul des Elements keinen Verweis auf das Diagrammmodul, und das
  Menü fehlt, wenn kein Diagrammtyp registriert ist. **Mit Element verknüpfen ▸** (`chart.link`) an den Diagrammen
  listet die Sätze der Elemente auf derselben Folie; **Lösen (Werte einfrieren)** ist ausgegraut, wenn das Element fehlt.
- **Kennlinienparameter.** Kennliniendaten, die keine Elementwerte sind, sind optionale Textparameter des Elements
  (Inspektor, erweitert; standardmäßig leer, eingefügte Elemente tragen also nichts Neues): Pumpe `curveH0`,
  `curveQmax`, `curveHstat`; Maschine `curveKloss` (M_k/M_N), `curveNmax`; Wärmeerzeuger `curveDesignOutdoor`,
  `curveRoomTemp`, `curveExponent`, `curveOutdoor`.

### 14.3 Kennlinienfamilien

| Familie (Satz-Schlüssel) | Elemente | Formeln | Rückschreiben (Griff) | Quelle |
|---|---|---|---|---|
| Pumpen- und Anlagenkennlinie (`pump`) | `process.pump.centrifugal`, `bs.heat.pump` | H(V̇) = H₀·(1 − (V̇/V̇_max)²); Betriebspunkt beim V̇ der Pumpe (sonst 0,7·V̇_max), H aus der Kurve; Anlagenkennlinie H_stat + k·V̇² durch ihn (H_stat ohne Eingabe H₀/4); ohne H₀ verläuft die Kurve durch (V̇, H) der Pumpe; V̇_max ohne Eingabe V̇/0,7 | entlang der Pumpenkennlinie: setzt V̇ und H | [S28] |
| Drehmoment-Drehzahl, Asynchronmaschine (`torque`) | `machines.radial`/`.axial` mit Läufer `asm` | s = 1 − n/n_s; M = 2·M_k/(s/s_k + s_k/s), M_k = λ·M_N, s_k = s_N·(λ + √(λ² − 1)) (Kurve durch (n_N, M_N)); λ ohne Eingabe 2,5; Bemessungspunkt aus M_N und P_N, sonst der Betriebspunkt; konstantes Lastmoment durch den Betriebspunkt (gestrichelt) | entlang der Kurve: setzt n und M (speichert M_N, P_N, wenn angenommen) | [S14, S15] |
| Drehmoment-Drehzahl, Momentengrenze (`torque`) | andere Querschnitte, `machines.drive.motor`, `machines.drivetrain.emachine` | M_max = M_N bis n_b = P_N/M_N, darüber M_max·n_b/n, bis n_max (ohne Eingabe 3·n_b) | waagrecht: setzt n (und M, auf die Grenze begrenzt) | [S14, S15] |
| PV I–U und P–U (`pv`) | `energy.pv.module`, `energy.pv.string` | I(U) = I_sc·(1 − C₁·(e^(U/(C₂·U_oc)) − 1)), C₂ = (U_mpp/U_oc − 1)/ln(1 − I_mpp/I_sc), C₁ = (1 − I_mpp/I_sc)·e^(−U_mpp/(C₂·U_oc)); String: U × N_s; MPP per Goldener-Schnitt-Suche; MPP-Bereich und U_DC,max des Wechselrichters im Löserumfang; U_oc bei θ_min | – (Ziehen von θ und G im Erkunden-Modus offen) | [S33, S34] |
| Bode-Diagramm (`bode`) | Widerstand, Kondensator, Spule in einem Kreis mit R und C oder L | RC: f_c = 1/(2πRC); RL: f_c = R/(2πL); erste Ordnung −10·lg(1 + r²), φ = −arctan r; RLC: f₀ = 1/(2π√(LC)), ζ = (R/2)√(C/L), −10·lg((1 − r²)² + (2ζr)²), φ = −arctan2(2ζr, 1 − r²) | entlang f: setzt C (RC, RLC) oder L (RL); R bleibt | [S7] |
| Sprungantwort (`step`) | wie Bode | 1 − e^(−t/τ), τ = RC oder L/R (Punkt bei 63,2 %); PT2 unterkritisch, aperiodischer Grenzfall und überkritisch (Punkt beim Überschwingen) | – | [S7] |
| Getriebe (`train`) | Zahnräder, Wellen | Wellen = Gruppen gleicher gelöster n im Umfang, nach fallendem n; M = größtes Moment der Glieder; P = 2π·n·M | – | [S16, S18] |
| Heizkurve (`heating`) | `bs.heat.boiler`, `bs.heat.heatpump` | q = (θ_i − θ_e)/(θ_i − θ_e,d); θ_VL,RL = θ_i + Δθ_m,d·q^(1/n) ± ½(θ_VL,d − θ_RL,d)·q; θ_VL,d = θ_VL des Erzeugers (ohne Eingabe 55 °C), θ_RL,d sein θ_RL (ohne Eingabe θ_VL,d − 10 K), θ_e,d ohne Eingabe −12 °C, θ_i 20 °C, n 1,3 | Auslegungspunkt senkrecht: setzt θ_VL; aktueller Punkt entlang der Kurve: speichert θ_e | [S29, S31] |

**Abnahmepunkte (Tests mit Handrechnung):**

- „Eine Änderung von H₀ der Pumpe zeichnet das verknüpfte Diagramm neu und verschiebt den Betriebspunkt“: H₀ 40 m,
  V̇_max 100 m³/h, H_stat 10 m, V̇ 70 m³/h → Betriebspunkt (70 m³/h; 20,4 m); H₀ 50 m → (70 m³/h; 25,5 m); das `ChangeSet`
  der Engine zu dieser Änderung nennt das Diagramm als abhängiges Objekt, und seine Zeichnung ändert sich.
- „Ziehen des Punkts ändert den Förderstrom der Pumpe (ein Rückgängig-Schritt für Diagramm und Element)“: der bei
  50 m³/h losgelassene Griff ergibt eine Dokumentänderung, die nur die Pumpe betrifft (V̇ = 50 m³/h und H = 30 m
  eingegeben); das Diagramm steht unter den abhängigen Objekten dieser Änderung.
- „Lösen friert die Werte ein“: die Zeichnung des eingefrorenen Bilds bleibt gleich, wenn sich H₀ ändert, und die Pumpe
  führt es nicht mehr als abhängiges Objekt.
- „Das PV-Diagramm zeigt den MPP und den Bereich des Wechselrichters“: 10 Module (U_oc 49,5 V, U_mpp 41,6 V, I_sc 13,9 A,
  I_mpp 13,0 A) an einem Wechselrichter mit MPP-Bereich 200–480 V und U_DC,max 600 V: Band 200–480 V, Linie bei 600 V,
  U_oc bei θ_min 541,78 V, MPP ≈ 5,41 kW bei ≈ 416 V (im Bereich).
- Weitere Handrechnungen: RC 1 kΩ/100 nF: f_c = 1591,5 Hz bei −3,01 dB und −45°, τ = 100 µs bei 63,2 %; Ziehen von f_c
  auf 1 kHz setzt C = 159 nF; RLC 100 Ω/10 mH/100 nF: f₀ = 5032,9 Hz, ζ = 0,158, Überschwingen 60,1 %;
  Asynchronmaschine 50 Hz, 4 Pole, 1440 min⁻¹, 100 N·m: s_k = 0,1917, Kippmoment 250 N·m bei 1212,5 min⁻¹; Motorblock
  M_N 200 N·m, Eckdrehzahl 3000 min⁻¹: 100 N·m bei 6000 min⁻¹; dreistufiges Getriebe 1500/750/250/83,3 min⁻¹ und
  10/20/60/180 N·m; Heizkurve 55/45 °C, Auslegung −12 °C, +4 °C → θ_VL = 40,10 °C.

### 14.4 Nötige Host-Änderungen und offene Punkte

- **`moveHandle` der Engine (hier nicht geändert):** In `EngineApi.MoveHandleCore` sollte der Zweig beim Loslassen
  `s.Apply("Move handle", d => DocumentHandles.Move(Registry, d, o.Id, key, at, s.Lang))` statt
  `Edits.Replace(d, Registry.MoveHandle(…))` anwenden (die Prüfung auf ein unverändertes Dokument bleibt:
  `DocumentHandles.Move` gibt dasselbe Dokument zurück, wenn sich nichts ändert). Die `dependents` des Zustands nennen
  dann das Diagramm, und die Hosts zeichnen es neu.
- **COM-Host (hier nicht geändert):** Das Loslassen eines Griffs (`OnSlideController.Handles.cs`, die Stelle mit
  `ReplaceWithDependents(slide, unit, Edits.Update(unit.Doc, new[] { o.Id }, _ => moved))`) muss `DocumentHandles.Move`
  auf dem Foliendokument aufrufen (die Objekte der Einheit plus die der anderen Einheiten, wie `WithContext` es baut) und
  die Objekte übernehmen, die es **in anderen Einheiten geändert** hat (die Pumpe liegt in einer anderen Einheit als ihr
  Diagramm). `ApplyOutsideOwn` übernimmt heute nur hinzugefügte und entfernte Objekte; es braucht einen dritten Zweig:
  für jede andere Einheit mit einem Objekt, dessen Instanz sich zwischen vorher und nachher unterscheidet,
  `SketchUnits.Replace` dieser Einheit mit den geänderten Objekten im selben Rückgängig-Schritt, danach
  `RedrawDependents`. Derselbe Zweig lässt allgemein Dokumentaktionen wirken, die Nachbarn ändern.
- **Live-Vorschau eines Griffs:** Die Vorschau von `moveHandle` der Engine und die Ziehvorschau in COM zeigen das Diagramm
  beim Ziehen unverändert; eine Vorschau würde mit `ValueSolveOptions.Overrides` lösen und den Satz aus dieser Tabelle
  zeichnen.
- **Änderungen von Nachbarn:** Ein Wert, den das Element von einem verbundenen Element übernimmt (ein Volumenstrom aus
  einem Rohr), ändert das Diagramm nur, wenn das Element selbst zur Änderung gehört; eine transitive Regel („Diagramme
  jedes Elements im Löserumfang der berührten Objekte“) gehört in den Abhängigkeitsdurchgang der Hosts oder der Engine.
- **Nicht umgesetzt:** Fluidtechnik (Verdrängerpumpen haben keine H(Q)-Kennlinie; eine p–Q-Kennlinie mit
  Druckbegrenzungsventil kommt später), das Wirkungsgradkennfeld und die Bilanz aus Last und Erzeugung aus Abschnitt 5,
  Drehzahländerungen der Pumpe über die Affinitätsgesetze, die PV-Kennlinie bei anderer Einstrahlung und Zelltemperatur
  (Ziehen von θ und G im Erkunden-Modus), Bode-Griffe, die R statt C ändern, und die Eingabe der Kennlinienparameter in
  der Wertekarte. Abschnitt 7 behält den Plantext zu Calc 5; dieser Abschnitt ist sein Stand.
