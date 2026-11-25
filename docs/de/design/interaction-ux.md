# Interaktionskonzept: Griffe, schwebende Leiste, Kreismenü

Stand: Konzept und erste Umsetzung im PowerPoint-COM-Add-in (2026-11-25). Web und LibreOffice folgen denselben Regeln
(Folgeschritte unten). Englische Fassung (maßgeblich): [../../en/design/interaction-ux.md](../../en/design/interaction-ux.md).

**Warum.** Urteil des Maintainers vom 2026-11-25: „Überprüfe die Overlay-Leiste für alles, auch das Kreismenü – es ist
eine Katastrophe, die UI/UX erklärt nichts.“ Und: „Mächtig, aber verständlich für neue und ältere Nutzer; nicht zu viel
Text im Overlay; es muss konsistent sein: was findet man wo, wie bedient man was, was gehört zusammen.“

## 1. Befund der Durchsicht (COM, vor dieser Änderung)

1. **Griffe übereinander.** Auf einem kurzen Rohr lagen der Drehgriff (von PowerPoint), ein Schloss, ein Ecken-„+“, ein
   Abzweiggriff, das Verlängern-„+“ und ein zweites Leitungsende-„+“ (beide am selben Ende), die Punktrauten und das
   Verschiebekreuz von PowerPoint wenige Pixel auseinander. Jede Griffart platzierte sich für sich (`PortScreen` wich
   nur PowerPoints Griffen aus, Schlösser nur einander, Segmentgriffe gar nichts).
2. **Mehrere „+“ mit verschiedener Bedeutung.** Ecken-„+“ in der Segmentmitte (umrandet), Verlängern-„+“ hinter einem
   freien Ende (gefüllt), Bau-/Anschluss-„+“ an Anschlüssen und Leitungsenden (gefüllt, gleiches Aussehen), das „+“-
   Piktogramm in der Leiste für „Zeile anfügen“ und „Spalte anfügen“ (eines fiel sogar weg, weil beide dasselbe
   Piktogramm hatten) und das „+“ im Kasten für Einfügen. Keines sagte, was es hinzufügt.
3. **Leiste ohne Ordnung.** Die Reihenfolge wechselte je Element, keine Trenner zwischen Gruppen, reine Symbolknöpfe mit
   allgemeinen Piktogrammen („+“, „−“, Kreislauf) für Verschiedenes, in der Mehrfachauswahl dasselbe Piktogramm für
   „verteilen“ und „Diagramm anordnen“, Duplizieren und Löschen immer in der Leiste, obwohl es Strg+D und Entf gibt.
4. **Kreismenü ohne feste Plätze.** Der Ring wurde nach Priorität gefüllt, derselbe Befehl lag je Element woanders;
   „Mehr“ war ein Überlauf des Rests; Zyklus-Aktionen doppelten die Auswahllisten; kein Eintrag erklärte sich; ein
   ausgegrauter Eintrag schloss beim Klick das Menü.
5. **Einfügefeld** blieb bei einem Klick daneben offen (nur Esc schloss es).
6. **Tabellen** hatten keinen direkten Weg, eine Zeile oder Spalte dort einzufügen, wo man sie will.
7. Aus der Bestandsaufnahme aller Module (Abschnitt 3): Piktogramme für verschiedene Bedeutungen (`ArrowEnd` für
   UML-Beziehungen, Nachrichtenarten und Pfeilspitzen; `Text` für Dateneingabe und Schalter; `Rotate` für „nächster
   Winkel“ und „an Wand ausrichten“), Symbol-IDs außerhalb des gemeinsamen Satzes (zeichnen einen Platzhalter),
   Aktionen, die eine Auswahl oder Ansicht doppeln, Familien mit 8–11 Aktionen, die nicht alle in eine Leiste passen.

## 2. Was wohin gehört

| Ort | Was dorthin gehört | Regel |
|---|---|---|
| **Am Element (Griffe)** | Alles, was man durch Ziehen oder einen Klick *an einer Stelle* tut: verschieben, skalieren, drehen (PowerPoints Griffe, übernommen), Punkte und Enden (Rauten), „+“ an einer Stelle (Bauanschluss, freies Ende, Tabellenseite), Eckpunkt, Abzweig, Knotenpunkt, Schloss, Schnell-Anfügen-Dreiecke, Umriss eines Diagrammteils. | Direkte Bearbeitung zuerst. Ein Aussehen je Bedeutung (Abschnitt 4). Nie zwei Griffe übereinander. |
| **Schwebende Leiste** | Die Hauptaktion *dieses* Elements (mit kurzem Text), seine nächsten zwei oder drei Befehle als Piktogramme, dann „⋯“. | Nur Kontext, klein, Piktogramme mit Tooltips; kurzer Text nur, wo kein Piktogramm eindeutig ist. Wächst nie mit Inhalt. |
| **Kreismenü** (Rechtsklick, Umschalt+F10, „⋯“) | Alles zum Element, in acht festen Plätzen gruppiert. | Vollständig, beschriftet, Hinweis in einer Zeile; derselbe Platz bedeutet für jedes Element dasselbe. |
| **Menüband** | Einfügen (Bibliothek, Suchen & Einfügen), dokument- und folienweite Einstellungen (Fangen, Verankern, Liniengruppe, Akzent, Planmaßstab, Schwarz-Weiß), Werkzeuge (Folien bereinigen, Kompatibilität, Export). | Nichts Elementspezifisches. |
| **Seitenleisten-Editor** | Jeder Parameter (Inspektor), Datentabellen, Bibliothek mit Suche. | Die „Langform“; für die Schnellskizze nie nötig. |
| **Tastatur** | R / Umschalt+R, X, Y, Strg+D, Entf, Strg+Leertaste, Umschalt+F10, Esc, Tab (nächstes Teil der Baugruppe), Strg+Doppelklick (Verbundene auswählen), Alt (kein Fangen), Umschalt (15°-Schritte). | In Tooltips und Kreismenü-Hinweisen genannt. |

## 3. Bestandsaufnahme: Interaktionen je Elementfamilie (alle Module)

Abgeleitet aus der Engine (Aktionen, Ansichten, Anker von `ObjectType`, `Building.Ports`, `Flow.CanChain`,
`Polylines`, Teile).

| Familie (Modul) | Geometrie | Ansichten | Am Element | Leiste (Schlüsselaktionen) | Hinweise |
|---|---|---|---|---|---|
| Zahnräder, Hohlräder, Kegel-, Schnecken-, Zahnstange (Mechanik) | Box | 2–5 | Bau-„+“ je freier Eingriffsseite / innen / Spitze / Nabe, Schloss je Eingriff | Naben-Auswahl, Schrägverzahnung, wahre Größe | Kopplung, Bewegungsmodus, Baugruppe wählen |
| Planetensätze, Differential, Steg | Box | 2–3 | „+“ auf freien Bolzen | Planeten ±, festes Glied | in Einzelteile zerlegen |
| Wellen, Lager, Kupplungen, Dichtungen | Box | 3–4 | „+“ an freien Achsenden (ziehen: dehnen), Schloss je Teil auf der Welle | hohl | Räder fahren mit der Welle |
| E-Maschinen, Antriebsblöcke, Baukasten (Maschinen) | Box | 2–4 | „+“ an Achsenden und Anschlüssen | Bauform-, Typ-Auswahl | treibt die Welle |
| Koppelgetriebe: Glieder, Gelenke, Mechanismen (Kinematik) | Punkte / Box | 2 | Rauten auf Gelenken, „+“ an freien Gelenken | Winkel ±, gekreuzt, F = n, Koppelkurve, Zwischenlagen | Bewegungsmodus, Mechanismus zerlegen |
| Elektro-Symbole | Box | 1–4 | „+“ an freien Klemmen | Zustand, Polarität, Kennzeichen … | Installationsansichten wechseln mit Wänden |
| Drähte, Kabel, Busse, Leiterbahnen | Punkte (Ecken) | 1 | Rauten, ein „+“ je freiem Ende, Eckpunkte, Abzweig, Knotenpunkt an Kreuzungen, Schloss je angedocktem Ende | Führung-Auswahl, Brücken | angedockte Enden folgen |
| Fluid-Symbole, Ventile | Box | 1–2 | „+“ an freien Anschlüssen | Betätigung, Federseite, Medium | |
| Rohre, Kanäle, Prozess- und Energieleitungen | Punkte (Ecken) | 1 | wie Drähte | Führung, Leitungsart | |
| Prozess-, Energieanlagen, Gebäudetechnik | Box | 1–3 | „+“ an freien Anschlüssen | Art, Medium, Stufen | Planmaßstab, Baugruppen-Ansichtswechsel |
| Wände, Räume (Architektur) | Punkte (Ecken) | – | Rauten, Eckpunkte, „+“ an freien Wandenden | dicker/dünner, Art-Auswahl, 1:50 | Öffnungen folgen der Wand |
| Öffnungen, Treppen, Plansymbole | Box | 1–2 | – | Breite ±, an Wand ausrichten | |
| Diagramme (auch Figurendiagramme) | Box | – | Griffe auf Grenz-, Wertlinien und Markern; Teil-Umriss nach Klick auf ein Teil | Daten …, Stil, Kurve skizzieren | Teile: Reihe, Achse, Gitter, Legende, Grenze … |
| Diagrammknoten: Flussdiagramm, UML, BPMN, ER, Regelung | Box | 1–2 | Schnell-Anfügen-Dreiecke, Anschlüsse beim Annähern | Zeilen ±, Schalter | „Ändern in …“ nach Schnell-Anfügen |
| Pfeile, Linien, Hinweislinien, Maße, Verbinder | Punkte | – | Rauten, „+“ an freien Enden (Polylinien), Eckpunkte | Linientyp, Spitzen, Linienart | Typ wandeln, Beschriftungsmenü |
| Tabelle, Text, Stückliste (Annotation) | Box | – | Tabelle: „+“ an jeder Seite | Kopfzeile, letzte Zeile/Spalte entfernen | |
| Patent-Bezugszeichen | Punkte | – | Rauten | Bezugszeichen, Unterstreichung | neu nummerieren (Mehrfachauswahl) |
| Karten: Routen, Flächen, Marker | Punkte / Box | 1–3 | Rauten, Eckpunkte | Punkt ±, umkehren | |
| Fahrzeuge, Straßen, ADAS (Automotive) | Box / Punkte | 3–4 | „+“ an Straßenenden, Halterungen | Ansicht, Richtung | |
| Roboter, Zellen, Werkzeuge (Robotik) | Box / Punkte | 2 + Stil | „+“ am Flansch | Gelenke ±, Grundstellung, Zwischenlagen | Bewegung |
| Geometrie (Mathe) | Punkte | – | Rauten | Seiten ±, Winkel …, Teilungen | |
| Mehrfachauswahl (beliebig) | – | – | PowerPoints | eine Ansicht für alle · ausrichten, verteilen · Diagramm anordnen · Maß, Format übertragen, neu nummerieren | nur, was etwas ändert |

## 4. Eine Bildsprache für Griffe

| Aussehen | Bedeutung | Bedienung |
|---|---|---|
| PowerPoints weiße Quadrate/Kreise am Rahmen, Drehpfeil darüber, Verschiebekreuz unter kleinen Rahmen | skalieren, drehen, verschieben (PowerPoints eigene) | von InventaSketch mit Fangen übernommen; InventaSketch zeichnet nie darüber |
| **Orange Raute** auf einem Punkt | dieser Punkt (Ende, Ecke, Gelenk) | ziehen verschiebt ihn (fängt und dockt an); Klick auf eine Ecke wählt sie (Leiste: teilen, entfernen); Doppelklick entfernt eine Ecke |
| **Gefüllter blauer Kreis mit weißem „+“** | *hier etwas hinzufügen* – der Tooltip sagt was (Gegenrad, nächster Wellenabschnitt, Leitung oder Element am Anschluss, Leitung fortsetzen, Zeile darüber …) | Klick: Einfügefeld mit dem Passenden zuerst (Tabellenseiten: fügt sofort ein); an einem Wellenende ziehen: dehnen; an einem freien Leitungsende ziehen: weiterzeichnen |
| **Kleiner hohler blauer Punkt** im Segment | eine neue Ecke | Klick fügt sie ein, Ziehen knickt die Linie |
| **Hohler Kreis mit Stich** neben einem Segment | ein Abzweig | zu einem Anschluss oder frei ziehen; Klick: kurzer Stich |
| **Hohler Kreis mit Kreuz und Punkt** an einer Kreuzung | die kreuzenden Leitungen verbinden (Knotenpunkt) | Klick |
| **Blaues Schloss** (zu) / **graues Schloss** (offen) neben einem Berührpunkt, dünne Zuleitung | gekoppelt, bewegt sich mit / berührt, aber gelöst | Klick schaltet um |
| **Blaues Dreieck** vor einer Seite (Diagrammknoten) | das nächste Element auf dieser Seite | Klick: verbundene Kopie; Ziehen: mit anderem Element verbinden oder ablegen |
| **Blau umrandetes Quadrat mit kleinen Pfeilspitzen** auf einem Wert (Grenzlinie, Wertlinie, Marker eines Diagramms) | ein Wert, der auf der Zeichnung gezogen wird; die Pfeilspitzen zeigen die erlaubte Richtung | ziehen: die Linie folgt (geführt), der neue Wert steht neben dem Zeiger und rastet ein (ein Zehntel des Achsschritts); loslassen = ein Rückgängig-Schritt; Esc bricht ab; ein Klick wählt dieses Teil |
| **Kleiner blau umrandeter Punkt** auf einem Datenpunkt einer Kurve (Diagramme) | ein Wert der Kurve; ausgewählt blau gefüllt mit sanftem Leuchten | erscheint für die ausgewählte Kurve (oder den Punkt), die anderen nahe am Zeiger; Klick: wählt den Punkt; ziehen: verschiebt ihn (gerastet, ein Rückgängig-Schritt, Esc bricht ab); Entf löscht ihn |
| **Blauer Umriss** eines Teils (Diagramme) | das gewählte Teil; Drücken auf eine ausgewählte Kurve und Ziehen verschiebt die ganze Kurve | Leiste und Kreismenü wirken darauf; Entf löscht das Teil (Punkt, Kurve, Fläche …) oder blendet es aus, nie das Element |
| **Weicher blauer Schein** um die Kontur eines Elements | es ist ausgewählt (allein, mit anderen oder über „Verbundene auswählen“) | – (Abschnitt 6a) |
| **Schwächerer blauer Schein** | was ein überfahrener Eintrag „Verbundene auswählen“ (oder „Baugruppe wählen“) dazunähme | Wählen des Eintrags wählt es aus |
| **Rote gestrichelte Hilfslinien, roter Punkt, Beschriftung** beim Ziehen | Fangen und woran angedockt wird | Alt setzt aus |
| **Zweitfarbige (blaue) Vorschau** beim Ziehen | was mitbewegt wird (gekoppelte Nachbarn) | |

Größen: Rauten 10 px, „+“ 13 px, Punkte 7 px, Schlösser 15 px (logische Pixel × Bildschirmskalierung); jedes Ziel
reagiert im Umkreis von 12 px (24 px Trefferfläche) und der nächste Griff gewinnt, so nehmen sich Nachbarn keine Klicks
weg; PowerPoints eigener Griff gewinnt, wenn er näher ist (eine Raute gewinnt gegen den Skaliergriff an derselben
Stelle).

**Anordnung.** `HandleLayout` (COM, reine Funktion, getestet) platziert alle Griffe des Elements gemeinsam:
PowerPoints Griffe, Rauten und Dreiecke bleiben, wo sie sind; die anderen nehmen nach Priorität („+“ 90, Tabellen-„+“
85, Schloss 70, Knotenpunkt 60, Eckpunkt 40, Abzweig 30) den ersten freien Platz auf einer Suche um ihren Wunschplatz –
weiter hinaus in ihrer Richtung, bis 40° geneigt, Schlösser und Abzweige auch rund um ihren Anker, Eckpunkte nur entlang
ihres Segments – mit mindestens 3 logischen px Abstand. Optionale Griffe (Eckpunkt, Abzweig) ohne freien Platz werden
ausgeblendet; auf Elementen unter 24 px gibt es keine (hineinzoomen). Ein verschobenes „+“ behält eine dünne gestrichelte
Linie zu seinem Anschluss.

### Ruhig von Anfang an: schrittweise Anzeige (Runde 2, 25.11.2026)

Urteil nach der ersten Runde: „immer noch dasselbe“ – die Griffe überlappten nicht mehr, aber ein gewähltes Zahnrad
zeigte noch vier „+“, ein Schloss und Zuleitungen, eine kurze Leitung sechs Griffe. Regel: **zeigen, was das Element
ist, und den einen nächsten Schritt; alles andere erscheint dort, wo der Zeiger hingeht.**

- **Immer sichtbar:** PowerPoints Rahmen, die Punktrauten einer Linie und höchstens **ein** „+“ (der Hauptplatz):
  - das freie Ende einer Linie oder Leitung (sonst ihr Anfang);
  - die freie Seite eines Zahnrads rechts (sonst irgendeine freie Seite);
  - ein Anschluss, dann ein freies Achsende, dann ein beliebiger Bauanschluss;
  - die Unterseite einer Tabelle;
  - das Dreieck unter einem Diagrammknoten.
- **Nahe am Zeiger:** jeder andere Griff blendet sich ab etwa 56 logischen px ein und ist innerhalb von 24 px ganz da;
  er blendet sich wieder aus, wenn der Zeiger geht. Das sind weitere „+“, Eckpunkte, Abzweige, Knotenpunkte,
  Wertgriffe, die übrigen Dreiecke und Schlösser. Die Deckkraft folgt dem Abstand, ohne Flackern und ohne Zeitgeber.
- **Umschalt** zeigt alle Griffe auf einmal (Überblick), Loslassen blendet sie wieder aus.
- **Angedockt:** ein winziger grauer Punkt markiert jede Verbindungsstelle. Das Schloss erscheint in ihrer Nähe in
  ruhigem Grau, wird unter dem Zeiger blau und zeigt erst dann seine Zuleitung. Ein vom Anschluss weggeschobenes „+“
  zeigt seine gestrichelte Linie nur unter dem Zeiger.
- **Gewähltes Diagrammteil:** seine Griffe bleiben sichtbar.
- Ausgeblendete Griffe nehmen keine Klicks weg: getroffen wird ein Griff nur mit dem Zeiger darauf, und dort ist er
  ganz sichtbar.

## 5. Schwebende Leiste

Für jedes Element gleich aufgebaut (Runde 2): **höchstens vier Knöpfe plus „⋯“**.

1. **Die Hauptaktion mit kurzem Text:**
   - „Ändern in …“ direkt nach Schnell-Anfügen;
   - sonst „Werte“ (Piktogramm „x="), für Elemente mit Werten, wenn die Wertestufe Erkunden oder Ingenieur ist
     (engineering-calculations.md, Abschnitt 11);
   - sonst „Daten“ (Diagramme);
   - sonst „Text“ (Texte, Diagrammknoten, Bezugszeichen, Tabellen: ihre Zellen);
   - sonst „Ansicht“ (eine Auswahlliste, für Elemente mit mehreren Ansichten);
   - sonst die erste Stil-Auswahl (Linien: z. B. die Pfeilspitzen);
   - sonst „Einfügen“.
2. **Bis zu drei Piktogramme mit Tooltips**, in dieser Reihenfolge:
   - eigene Aktionen des Elements mit eindeutigem Piktogramm;
   - die Ansichtsauswahl;
   - Einfügen;
   - Kurve skizzieren, Koppelkurve, Zwischenlagen, zerlegen, Bewegungsmodus, Baugruppe wählen;
   - die Stil-Auswahlen mit Piktogramm;
   - 90° drehen und spiegeln, wo es die Zeichnung ändert.
3. **⋯** – das Kreismenü: alles, gruppiert. Duplizieren und Löschen sind dort (und Strg+D, Entf).

Eine gewählte Ecke zeigt „Teilen“ (mit Text) und „Entfernen“. Ein gewähltes Diagrammteil zeigt seinen Namen, seine
drei häufigsten Befehle und „⋯“. Knöpfe sind 32 logische px groß (Trefferflächen von mindestens 28 px) mit getöntem,
umrandetem Hover. Tooltips sagen, was passiert, plus Tastenkürzel („Horizontal spiegeln (X)“, „Linienart: Gestrichelt –
klicken zum Wählen“). Die Mehrfachauswahl behält ihre Gruppen: eine Ansicht für alle | ausrichten und verteilen |
Diagramm anordnen („Diagramm ↓“, „Diagramm →“) | Maß, Format übertragen, neu nummerieren.

### Piktogramme: ein Piktogramm, eine Bedeutung (F3, 2026-11-25)

Jedes Aktions-Piktogramm ist eine Id aus `IconIds` (Core) und wird von beiden Hosts mit Piktogrammen gezeichnet: COM
`IconPainter` und Web `icons.ts` (LibreOffice zeigt nur Textbeschriftungen). Die F3-Piktogramme sind SVG-Pfaddaten im
24er-Raster, einmal definiert und in beide Hosts übernommen (`IconPainter.SharedPaths`, `icons.ts`); ein Web-Test prüft,
dass beide Tabellen gleich sind. Kontaktbögen (hell und dunkel, 32 und 16 px) schreiben
`tests/InventaSketch.AddIn.Tests/IconPainterTests.WritesIconSheet` und `hosts/office-web/test/icons.test.ts`.

- **Text („T“) nur für „Text bearbeiten“.** Namen, Kennzeichen, Bezeichnungen und Titel nutzen *Name* (ein Etikett);
  Zahlen *Wert* (Feld mit Schreibmarke) oder, wo die Größe bekannt ist, ein Größen-Piktogramm (Winkel, Länge, Breite,
  Höhe, Dicke, Fläche, Volumen, Masse, Druck, Leistung, Spannung, Kapazität, Ladezustand, Zeit, Frequenz, Toleranz,
  Förderstrom, x-/y-Bereich; `IconIds.ForQuantity` ordnet Einheiten zu). Beschriftung zeigen nutzt *Beschriftung
  zeigen*, Anschlussnamen *Anschlüsse beschriften*.
- **Aufbau:** Zeile (unten oder oben), Spalte, Operation, Anschluss, Punkt hinzufügen/entfernen; *eins mehr/eins
  weniger* für wiederholte Teile (Zähne, Pole, Module, Stufen …). „+“/„−“ (*Erhöhen/Verringern*) nur zum Verstellen
  eines Werts.
- **Richtung und Lage:** *Umkehren* (Wendepfeil) kehrt eine Richtung um, *Seite wechseln* legt Maß oder Bezugslinie auf
  die andere Seite, *Polarität* tauscht + und −, *Tauschen* tauscht zwei Dinge (Enden, Federseite), *Fließpfeil* zeigt
  einen Richtungspfeil, *Gelenk am Anfang/Ende*, *Nächste/vorige Stellung* (Gelenk um einen Schritt gedreht),
  *Grundstellung*, *An Wand ausrichten*. *Drehen* und *Spiegeln* bleiben die Host-Befehle für das Element.
- **Diagramme:** *Daten*, *Legende*, *Gitternetz*, *Achsenzahlen*, *Glätten*, *Formel* (fx), *Parameter*, *Code*
  (JSON), *Höhenlinien*, *Funktionsgenerator*, *Marke*, *Wertlinie*, *Zwischen/unter Kurven schraffieren*,
  Achsenbruch x/y und sein Entfernen.
- **Von Hand zeichnen:** *Stift* („Kurve skizzieren“); *Bahnkurve* nur für die Koppelkurve; *Zwischenlagen*.
- **Nur mit Beschriftung:** *Schalter*, *Nächste Variante* und *Option* (diese Option einer Auswahl wählen:
  Beziehungsarten haben ihre Linienende-Piktogramme, andere Arten wie Fragment- oder Bildschirmarten nutzen *Option*)
  stehen nie allein: die Leiste zeigt diese Aktionen als kurzen Text. Schritt-Piktogramme („+“/„−“, eins mehr/weniger)
  erscheinen mit ihrer Beschriftung.

Der Engine-Test `tests/InventaSketch.Engine.Tests/IconHygieneTests.cs` prüft jede Aktion jedes Elements
(Ausgangszustand, nach jeder Aktion, jedes Teil): bekanntes Piktogramm; `Text` nur, wenn die Aktion nach dem Text fragt;
`Option` nur für Aktionen, die einen Parameter setzen; innerhalb eines Elements kein Piktogramm mit zwei Bedeutungen
(Bedeutung = Id ohne Ortsangaben wie `.top`, `Left`, `.2`; ausgenommen die Marker nur mit Beschriftung und ausdrückliche
Schrittpaare `.inc/.dec`, `.more/.fewer`); Beschriftungen höchstens 34 Zeichen (Kreismenü), die über 22 (Leiste) werden
aufgelistet. Ansichten mit gleichem Piktogramm werden ebenfalls aufgelistet (nächste Runde der Ansichts-Piktogramme).
### 5a. Auswahlen sind Klapplisten (Regel, 25.11.2026)

Maintainer: „Kurvenform ist ein Schalter zum Durchklicken, keine Klappliste – das hatten wir doch schon, habt ihr alles
vergessen?!“

**Regel: Eine Auswahl unter mehreren Möglichkeiten ist immer eine Klappliste mit den Piktogrammen oder Bildern und
Bezeichnungen der Möglichkeiten, die aktuelle markiert – nie ein Durchschalt-Knopf.** Das gilt für das Element und für
jedes seiner Teile:

- Engine: eine Auswahl ist eine Aktion je Möglichkeit mit derselben `ActionChoice`
  (`src/InventaSketch.Core/ActionChoices.cs`; Id `<Auswahl>=<Wert>`, die aktuelle Möglichkeit `IsActive`;
  Parameterauswahlen über `ActionChoices.FromParam`). Möglichkeiten ohne passendes gemeinsames Piktogramm tragen eine
  kleine Zeichnung (`ContextAction.Preview`: die Kurve in dieser Form, das Füllmuster, die Farbe). Befehle, die ein Menü
  bilden („Füllen bis ▸“, „Fläche hinzufügen ▸“), sind eine `ActionChoice` mit `IsMenu` (nichts aktuell). In den
  Diagrammen bleibt keine `IconIds.Cycle`-Aktion, die durch Möglichkeiten schaltet.
- Leiste: ein Klapplisten-Knopf je Auswahl mit dem Piktogramm oder Bild der aktuellen Möglichkeit (sonst einem kurzen
  Text) und einem Pfeil; ein Klick öffnet die Liste aller Möglichkeiten mit Piktogramm oder Bild und Bezeichnung, die
  aktuelle markiert (`FloatingBar.SetDropdown`, dasselbe Aussehen wie die Auswahlen des Elements). Teilauswahlen haben
  den Schlüssel „@p.&lt;Auswahl&gt;“, Aktionsauswahlen des Elements „@a.&lt;Auswahl&gt;“; die Wahl führt die
  Möglichkeit aus („part:&lt;Id&gt;“, „act:&lt;Id&gt;“).
- Kreismenü: Zustandsauswahlen stehen unter **Stil ▸** als „Bezeichnung: Aktuell ▸“ (bei ausgewähltem Teil dessen
  eigene Auswahlen), Menüs unter **Bearbeiten ▸** als „Bezeichnung ▸“; beide öffnen die Möglichkeiten (lange Listen
  gehen unter „Mehr ▸“ weiter).
- Web und LibreOffice: die Engine liefert zu jeder Aktion `choice`, `choiceLabel`, `choiceMenu` und `previewSvg`, damit
  sie die Möglichkeiten genauso zusammenfassen.

## 6. Kreismenü

Acht feste Plätze für jedes Element, im Uhrzeigersinn ab 12 Uhr (oben = bearbeiten/Text, rechts = einfügen,
unten = Stil, links = Ansicht/anordnen):

```
                 Text …
     Anordnen ▸           Bearbeiten ▸
  Ansicht ▸                    Einfügen …
     Löschen              Duplizieren
                 Stil ▸
```

- **Text …** – Text bearbeiten (Doppelklick). **Bearbeiten ▸** – die eigenen Befehle des Elements (Typ-Aktionen,
  Linientyp, Beschriftung, „Ändern in …“, zerlegen, skizzieren, Bewegungsmodus, Teile); bei gewähltem Diagrammteil
  dessen Befehle (mit dem Teilnamen als Titel).
- **Einfügen …** – das Einfügefeld. **Duplizieren** (Strg+D). **Löschen** (Entf).
- **Stil ▸** – ein Eintrag je Auswahl („Linienart: Gestrichelt ▸“), jeder öffnet seine Optionen mit Piktogrammen, die
  aktuelle markiert. Aktionen, die nur eine solche Auswahl setzen, stehen nicht noch einmal unter Bearbeiten.
- **Ansicht ▸** – die Ansichten (aktuelle markiert) und der Planmaßstab. **Anordnen ▸** – um 90° in beide Richtungen
  drehen, spiegeln, „Verbundene auswählen ▸“ (Abschnitt 6a; ersetzt „Baugruppe wählen“), als Folien animieren.
- Ein Platz ohne Inhalt für dieses Element bleibt, ausgegraut (ein Klick darauf lässt das Menü offen). Ein Platz mit
  einem Eintrag zeigt ihn direkt, außer Bearbeiten, Stil und Anordnen, die ihren Namen behalten.
- Untermenüs beginnen mit „← Zurück“; mehr als elf Einträge gehen in „Mehr ▸“ weiter.
- Beim Überfahren zeigt ein Eintrag seinen vollen Text und einen Hinweis in einer Zeile unter dem Ring. Esc, ein Klick
  daneben oder in die Mitte schließt das Menü.
- Die Mitte nennt das Element (oder sein gewähltes Teil, oder das offene Untermenü), damit klar ist, worauf das Menü
  wirkt. Ausgegraute Plätze haben wenig Kontrast, so heben sich die verfügbaren ab.

## 6a. Auswahldarstellung und „Verbundene auswählen“ (25.11.2026)

Maintainer: „Möglichkeit, verbundene Elemente auszuwählen bzw. anzuzeigen: verbundene Leiter und Leitungen (Schleifen),
drehgekoppelte Elemente z. B. auf Wellen/Zahnrädern, geschlossene Schleifen, alle folgenden Kettenglieder, geschlossene
Wände, Netze“, und: „die Auswahl wird etwas anders dargestellt, z. B. wie Slidemax einen blauen Verlauf dahinterlegt –
nicht nur bei verbundenen, auch bei einzeln ausgewählten: wenn sie übereinanderliegen, ist oft unklar, was ausgewählt
ist.“

**Auswahldarstellung.**

- Jede InventaSketch-Auswahl – ein Element, mehrere, das Ergebnis von „Verbundene auswählen“ – bekommt einen weichen
  blauen Schein entlang der genauen Kontur des Elements: seine gezeichneten Linien und Formen, verbreitert und
  weichgezeichnet. Es ist kein umgebendes Rechteck.
- Das Element selbst bleibt frei: seine Linien und das Innere seiner geschlossenen Formen sind aus dem Schein
  ausgespart, so liegt der Schein um das Element, als läge er dahinter.
- Wo die Kontur des gewählten Elements unter einem anderen Element verläuft, zeigt sich der Schein über diesem Element.
  So ist immer klar, welches von übereinanderliegenden Elementen gewählt ist; das obere Element bekommt keinen eigenen
  Schein.
- Der Schein ist ruhig: ein Blau, direkt an der Kontur höchstens etwa halb deckend, nach wenigen Pixeln ausgeblendet. Auf
  hellen Folien ist er kräftiger blau, auf dunklen heller blau (nach dem Folienhintergrund). Er liegt unter allen
  Griffen, der Leiste und dem Kreismenü.
- Beim Ziehen, Skalieren und Drehen übernimmt die Vorschau, der Schein ist ausgeblendet. Eine Mehrfachauswahl, die
  PowerPoint verschiebt, bekommt ihren Schein nach dem Loslassen an der neuen Stelle zurück.
- Was ein überfahrener Eintrag „Verbundene auswählen“ (oder „Baugruppe wählen“ in der Leiste) dazunähme, leuchtet
  schwächer (etwa halb), bevor etwas ausgewählt ist.

**Verbundene auswählen.**

- Der Platz Anordnen im Kreismenü hat „Verbundene auswählen ▸“, wenn etwas mit dem Element verbunden ist. Die Einträge
  sind nur die Arten, die zutreffen, je mit einem Hinweis in einer Zeile und der Zahl der Elemente:

  | Eintrag | Wählt | Typische Elemente |
  |---|---|---|
  | **Netz** | jede damit verbundene Verbindungslinie und jedes Element: Leitungen, Kabel, Busse, Rohre, Kanäle, Verfahrens- und Energieleitungen, Abzweige, Knoten und verbundene Kreuzungen, und die Elemente daran (der ganze Stromkreis bzw. das Rohrnetz) | Leitung, Kabel, Rohr, PV-String, Pumpe |
  | **Bewegt sich mit** | jedes Teil, das sich mitbewegt: kämmende Zahnräder, die Welle und ihre Naben, konzentrische Teile, Riemen und Ketten über ihren Scheiben, Koppelgetriebe | Zahnrad, Welle, Riemenscheibe, Glied |
  | **Folgende Elemente** | alles danach entlang gerichteter Verbinder, mit den Verbindern; bei einem Koppelgetriebe vom gewählten Glied nach außen (weg vom Gestell) | Ablaufschritt, Block, Verfahrenseinheit, Verbinder |
  | **Geschlossene Schleife** | die Schleife, zu der das Element gehört (nur angeboten, wenn es eine gibt): ein Stromkreis, eine Schleife im Ablaufdiagramm, die Wände um einen Raum, eine Zahnradschleife | Leitung im Stromkreis, Schritt in einer Schleife, Wand |
  | **Raumwände** | an Ecken, Enden und T-Stößen damit verbundene Wände | Wand |

- Überfahren eines Eintrags zeigt ihn vorab mit dem schwächeren Schein; Wählen wählt diese Elemente in PowerPoint aus
  (die Leiste der Mehrfachauswahl bietet dann eine Ansicht für alle, Ausrichten, Diagrammlayout …).
- **Strg+Doppelklick** auf das gewählte Element wählt die sinnvollste Art: bei einer Leitung oder einem Rohr das Netz,
  bei Zahnrad oder Welle, was sich mitbewegt, bei einem Ablaufschritt die folgenden Elemente, bei einer Wand die
  Raumwände. (Strg+A bleibt PowerPoints „Alles auswählen“.)
- „Baugruppe wählen“ (Leiste, Tab zum nächsten Teil) ist dasselbe wie „Bewegt sich mit“.
- Die Regeln kommen aus der Engine (`Connectivity`), so wählen Web-Add-in und LibreOffice dieselben Mengen.

## 7. Rückmeldung und Barrierearmut

- Fangen: rote Hilfslinien, ein Punkt am Fangpunkt und eine Beschriftung mit dem Ziel („Teilkreis“, „Achse“,
  „Bolzen 2“); Alt setzt aus. Gekoppelte Nachbarn erscheinen beim Ziehen blau. Schlösser zeigen angedockt (blau) und
  gelöst (grau) und schalten mit einem Klick.
- Rückgängig ist immer ein Schritt je Geste; Esc bricht immer ab (Ziehen, Einfügen, Weiterzeichnen, Kreismenü,
  Einfügefeld, Auswahlliste).
- Trefferflächen von 24 logischen px bei der Bildschirmskalierung; Leiste und Kreismenü mit mindestens 9–10 px Schrift,
  kontrastreich im hellen und dunklen Office-Design; Positionen hängen nie vom Element ab (Leistenreihenfolge,
  Kreismenüplätze).
- Hinweise sind einzeilig; Tooltips sagen, was passiert, nicht wie es funktioniert; zweisprachig.

## 8. Jetzt umgesetzt (COM, 2026-11-25)

- `OnSlide/HandleLayout.cs` (reine Anordnung, Tests `tests/InventaSketch.AddIn.Tests/HandleLayoutTests.cs`) und
  `OnSlideController.Handles.cs`: eine Anordnung für Rauten, „+“, Schlösser, Knotenpunkte, Eckpunkte, Abzweige,
  Tabellen-„+“; Trefferprüfung „nächster Griff“; Griffliste für UI-Tests (`LastHandles`, Protokollzeile „Handles: …“).
- Ein „+“ je freiem Leitungsende (das „+“ des Leitungsendes zeichnet beim Ziehen weiter); Eckpunkte statt eines
  zweiten „+“.
- Leiste neu gruppiert (`OnSlideController.Menus.cs`), Einfügen mit Text, „⋯“ öffnet das Kreismenü, Piktogramme nur
  wenn eindeutig, Mehrfachauswahl gruppiert.
- Kreismenü mit acht festen Plätzen, Untermenüs, Hinweisen (`PieMenu.Hint`), ausgegraute Plätze lassen das Menü offen;
  `LastPieItems`, Protokollzeile „Pie: …“.
- Einfügefeld schließt bei Klick daneben (`InsertSearch.ClickedOutside`) und wenn ein anderes Fenster aktiv wird.
- Tabellen: Aktionen gruppiert `side.top/bottom/left/right` (Engine, `TableType`), als „+“ an jeder Seite gezeichnet;
  eine Zeile oben kommt unter die Kopfzeile.
- Griffe auf Werten (`OnSlideController.Handles.cs`, Engine `Registry.DragHandles` / `MoveHandle`): in der Anordnung als feste Griffe (Vorrang vor dem Verschieben des Körpers im Diagrammfeld), Tooltip mit dem Wert, geführtes Ziehen mit Live-Vorschau des geänderten Teils und seines neuen Werts, ein Rückgängig-Schritt, Esc bricht ab, ein Klick wählt das Teil; `DragHandlesOf` für UI-Tests.
- UI-Szenarien: `overlay-handles`, `pie-menu`, `insert-box-dismiss`, `table-plus`, `chart-drag-handles` (tests/powerpoint-com/scenarios).

### Runde 2 (25.11.2026): ruhiges Overlay, Leiste mit vier Knöpfen, eingepasstes Einfügen

- Schrittweise Anzeige aller Griffe (`OnSlideController.Handles.cs`: `Reveal`, `PrimaryHandleId`, `UpdateReveal`,
  ausgeblendetes Zeichnen); ruhige Schlösser; Zuleitungen nur beim Überfahren; die sichtbaren Griffe für UI-Tests
  (`LastShownHandles`, Protokollzeile „Shown: …“).
- Leiste: Hauptaktion mit Text plus bis zu drei Piktogramme plus „⋯“ (`BarCandidates`); 32-px-Knöpfe mit deutlichem
  Hover; jeder Befehl bleibt im Kreismenü erreichbar (`LastCommandIds`, Protokollzeile „Commands: …“).
- Kreismenü: der Name des Elements in der Mitte, ausgegraute Plätze mit wenig Kontrast, Koppelgetriebe- und
  Eckenbefehle unter Bearbeiten.
- Tabellen: „Text“ bearbeitet die Zellen (eine Zeile, „|“ und „\n“); Doppelklick ebenso.
- Einpassen: die gewählte Einheit wird Objekt für Objekt geprüft (Wellen zuerst, dann die Welle, auf der ein gewähltes
  Teil sitzt); Einfügen am Zeiger zeigt nahe einer Welle schon das eingepasste Ergebnis („Passt hier – Klick fügt ein“)
  und fügt genau das ein (`OnSlideController.Fitting.cs`; `Fitting.PlaceNear` wird genutzt, sobald die Engine es hat,
  bis dahin per Reflexion).

Vorher → nachher, direkt nach der Auswahl mit dem Zeiger abseits des Elements:

| Element | Vorher (Runde 1) | Nachher (Runde 2) |
|---|---|---|
| Zahnrad mit Gegenrad | 4 blaue „+“, 1 „+“ zum Gegenrad, Schloss mit Zuleitung, PowerPoints Rahmen; Leiste mit Einfügen, Ansichten, Nabe, Schrägverzahnung, Spiegeln … (8–10 Knöpfe) | PowerPoints Rahmen und ein „+“ (die freie Seite rechts); ein grauer Punkt am Eingriff; Leiste: „Ansicht ▾“ + bis zu 3 Piktogramme + ⋯ |
| Kurze, einseitig angedockte Leitung | 2 Rauten, „+“, Schloss mit Zuleitung, Eckpunkt, Abzweig | 2 Rauten und ein „+“ am freien Ende; ein grauer Punkt am angedockten Ende; Eckpunkt, Abzweig und Schloss erscheinen nahe am Zeiger |
| Diagramm mit Grenzlinien | Griffe immer, Leiste mit Daten, Skizzieren, Einfügen, Stil … | ruhiges Diagramm; Griffe erscheinen nahe den Linien (immer bei gewähltem Teil); Leiste: „Daten“ + bis zu 3 Piktogramme + ⋯ |
| Ablaufdiagramm-Kasten | 4 Dreiecke, Leiste mit Text, Einfügen, Aktionen, Spiegeln … | das Dreieck unten; die anderen nahe am Zeiger; Leiste: „Text“ + bis zu 3 + ⋯ |
| Tabelle | 4 „+“ an den Seiten; Leiste: Einfügen, Kopfzeile, Zeile/Spalte entfernen … | ein „+“ unten (eine Zeile); die anderen nahe ihrer Seite; Leiste: „Text“ (die Zellen) + bis zu 3 + ⋯ |

### Runde 3 (25.11.2026): Auswahldarstellung und Verbundene auswählen

- Engine: `Connectivity.Connected / AutoKind / Options` (`src/InventaSketch.Core/Connectivity.cs`) mit den Arten Netz,
  bewegt sich mit, folgende Elemente, geschlossene Schleife und Raumwände, aufgebaut auf Anbindungen, Leitungsmedien,
  Kopplung, Koppelgetriebe-Rollen und den Nachbarn, aus denen ein Typ zeichnet.
- COM: `OnSlide/SelectionHalo.cs` (Schein, je Auswahl und Ansicht zwischengespeichert, nie innerhalb eines Hooks
  gezeichnet) und `OnSlideController.Connected.cs` (Kreismenü-Einträge, Vorschau beim Überfahren, Strg+Doppelklick,
  Auswahl der Einheiten); Protokollzeilen „Halo: selected=…; connected=…“ und „Select connected (Art): n units“;
  UI-Szenario `select-connected`.

### Runde 4 (25.11.2026): Auswahlen als Klapplisten, Datenpunkte, Kurven und Flächen in Diagrammen

- Auswahlen an Teilen und Elementen (Abschnitt 5a): `ActionChoices` in Core; jeder Durchschalter der Diagramme
  (Darstellung, Skalen, Legende, Ausrichtung, Modus, Maschine, System, Anzeige, Funktion, Flanken, Kurvenform,
  Ausgleichsfunktion, Glättung, Strichart, Marker, Füllung) wurde eine Auswahl; COM `OnSlideController.Choices.cs`
  (`ActionBarChoices`, `PartChoices`, `ElementActionChoices`, Bilder der Möglichkeiten aus den Vorschauen),
  `OnSlideController.Parts.cs` (Teil-Leiste und Kreismenü), reine Helfer `OnSlide/ActionChoiceBar.cs` (Tests
  `tests/InventaSketch.AddIn.Tests/ActionChoiceBarTests.cs`); Protokollzeile „Dropdown [Bezeichnung]: …“, Automation
  `DropItems()`, `Command("menu:c:@p.<Auswahl>")` öffnet ein Kreismenü-Untermenü.
- Datenpunkte und Kurven (docs/de/modules/charts.md): Punkt-Teile mit Teilformen (`ObjectType.PartShapes`,
  `Registry.PartPrimitives`), Punktgriffe mit ihrer Kurve gezeigt (`DragHandle.ShowWith`), ganze Kurven durch Drücken
  auf die ausgewählte Kurve gezogen (`DragHandle.ByPart`), Entf auf einem Teil (`PartLooks.DeleteAction`).
- Flächen: schraffierte und gemusterte Flächen aller matplotlib-Arten, angelegt mit „Darunter füllen ▸“, „Füllen bis ▸“
  und „Fläche hinzufügen ▸“.
- UI-Szenarien: `chart-points`, `chart-curve-drag`, `chart-areas`, `part-choice-dropdown`.

## 9. Folgeschritte (in Phasen, mit Abnahmekriterien)

**Phase 1 – die Sprache in COM vollenden**
1. *Griffe über Diagramme hinaus*: dieselben Griffe für jedes Element, dessen Typ Ziehgriffe anbietet (Maßwerte,
   Winkel, Robotergelenke, Achsabstand von Zahnrädern). Abnahme: jeder solche Typ nennt seine Griffe in `DragHandles`,
   das COM-Overlay braucht keine Änderung; die Griffe behalten ihren Platz in der Anordnung.
2. *Abhängigkeiten beim Überfahren hervorheben*: Überfahren eines Schlosses oder von „Baugruppe wählen“ umrandet jede
   mitbewegte Einheit in der Zweitfarbe. Abnahme: ein Rad auf einer Welle zeigt Welle und deren andere Räder, sonst
   nichts. *Umgesetzt für „Baugruppe wählen“ und die Einträge „Verbundene auswählen“ (schwächerer Schein, Abschnitt 6a);
   Schlösser offen.*
3. *Symbol-Hygiene in der Engine*: jede Aktions-Symbol-ID steht in `IconIds.All` (oder eine neue ID mit Piktogramm in
   allen drei Hosts); ein Piktogramm, eine Bedeutung (UML-Beziehungen und Nachrichtenarten werden Auswahlen mit
   Options-Piktogrammen; `Text` nur für Textbearbeitung). Abnahme: ein Engine-Test schlägt bei unbekannter ID oder einer
   ID mit zwei Bedeutungen in einem Typ fehl.
   nichts.
3. *Symbol-Hygiene in der Engine* – **erledigt (F3, 2026-11-25)**, siehe „Piktogramme“ in Abschnitt 5 und
   `IconHygieneTests`. Offen: Ansichten von neun Mechanik-Elementen teilen noch ein Ansichts-Piktogramm (vom Test
   aufgelistet).
4. *Aktionen, die eine Auswahl oder Ansicht doppeln*, bekommen `ParamKeys` oder entfallen (Welle hohl als Schalter und
   Ansicht, Roboter-Stil neben Ansichten, `view.*`-Aktionen in sechs Modulen, `map.point.add/remove` neben Eckpunkten).
   Abnahme: das Kreismenü dieser Elemente nennt jedes Ding einmal.
5. *Zwischen Zeilen/Spalten einer Tabelle*: „+“-Punkte auf den inneren Zeilen- und Spaltenlinien (dort einfügen).
   Abnahme: ein Klick auf den Punkt zwischen Zeile 2 und 3 fügt dort eine Zeile ein.
6. *Ecke dort einfügen, wo der Punkt gezeichnet ist*, wenn die Anordnung ihn entlang des Segments verschoben hat.
   Abnahme: die neue Ecke erscheint unter dem Punkt.

**Phase 2 – dasselbe Konzept im Web-Add-in und in LibreOffice**
7. Web (`hosts/office-web/src/ui/stage.ts`, `contextbar.ts`, Kreismenü): gleiche Griffbilder, Anordnung (die reine
   `HandleLayout` portieren), Leistengruppen und Kreismenüplätze. Abnahme: die Web-Szenarien zeigen für Zahnrad, Pfeil,
   Rohr und Tabelle dieselben Leisten-IDs und Kreismenüplätze wie COM.
8. LibreOffice: das Overlay auf der Zeichenfläche (ROADMAP-Punkt) von Anfang an nach diesem Konzept.

**Phase 3 – Hilfe für neue Nutzer**
9. Hinweise beim ersten Mal: die erste Auswahl eines Elements mit „+“ zeigt drei Sekunden lang eine Zeile „Blaues +
   fügt hier Passendes hinzu“ (einmal je Nutzer). Abnahme: einmal gezeigt, nach einem Klick nie wieder.
10. Kreismenü und Leiste in größerer Darstellung (125 %) für schlechtes Sehen, über die Einstellungen im Menüband.
    Abnahme: alle Trefferflächen und Texte skalieren; die Anordnung hält Griffe auseinander.

## 10. Tests

Unit-Tests (ohne PowerPoint): Griffanordnung (keine Überlappung auf dem kurzen Rohr der Meldung, Prioritäten,
Ausblenden, PowerPoints Griffe dünner Rahmen), Kreismenüplätze und Untermenüs, Leistengruppen und Piktogrammregel,
Schließen bei Klick daneben, Tabellenseiten-Aktionen (`tests/InventaSketch.Modules.Annotation.Tests/TableSideTests.cs`).
UI-Szenarien in echtem PowerPoint (vom Koordinator auszuführen, PowerPoint geschlossen): `overlay-handles`,
`pie-menu`, `insert-box-dismiss`, `table-plus`, `select-connected`; Verbindungen
(`tests/InventaSketch.Core.Tests/ConnectivityTests.cs`, `tests/InventaSketch.Engine.Tests/ConnectivityModuleTests.cs`) und
der Schein (`tests/InventaSketch.AddIn.Tests/SelectionHaloTests.cs`) ohne PowerPoint; Bildschirmfotos unter
`local-resources/powerpoint-tests/…` zeigen das Ergebnis zur Durchsicht.
