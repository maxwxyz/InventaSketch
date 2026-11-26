# Modul Beschriftung (`annotation`)

Bibliothek: `src/InventaSketch.Modules.Annotation` · Tests: `tests/InventaSketch.Modules.Annotation.Tests` ·
Englische Fassung: [../../en/modules/annotation.md](../../en/modules/annotation.md)

Das Modul liefert die Zeichen, die auf eine Skizze kommen: Linien und Pfeile, Beschriftungen und Text, Bemaßung,
technische Zeichnungsangaben (Form- und Lagetoleranzen, Bezüge, Oberflächenangaben, Schweißnähte), Schraffuren,
Zeichen für Abbildungen, Symbole der technischen Mechanik für Freikörperbilder mit **Querkraft- und
Momentenverläufen** sowie Ablaufdiagramm-Grundformen. Alle Typ-Ids beginnen mit `ann.`. Alle Symbole sind eigenständig
aus Linien, Bögen, Kreisen und Text gezeichnet; es werden keine Norm-Grafiken, -Texte oder -Tabellen übernommen und
keine Normkonformität behauptet. Wo unten eine Symbolfamilie genannt ist, sagt der Name nur, welche Art Zeichen gemeint
ist.

## Konventionen

- **Quick Sketch zuerst:** Jedes Bibliothekselement lässt sich ohne Parameter einfügen; freies Skalieren ändert `Size`,
  nie die Parameter oder die Schrifthöhe.
- **Punktobjekte** (Bezugslinien, Maße, Toleranzrahmen, Bezugs- und Schweißsymbole, Kräfte) werden aus ihren Punkten in
  Seitenkoordinaten gezeichnet; **Kastenobjekte** (Oberflächensymbol, Schnittgrößenverläufe, Wolke, Lager) zentriert um
  (0, 0) in `Size`.
- Schrifthöhe 3,5 mm für Zeichnungsangaben und Maße, 5 mm für Beschriftungen.
- **Auswahlen sind Dropdowns:** Eigenschaft, Nahtart, Verfahren, Lagerung und Belastung sind je eine Auswahl; Optionen,
  zu denen kein gemeinsames Piktogramm passt, tragen eine kleine Zeichnung der Option (`ContextAction.Preview`).

## Kategorien

| Kategorie | Inhalt |
|---|---|
| `ann.lines` Linien & Pfeile | Linien, Polylinien, Pfeile (gerade, gebogen, gewinkelt, Block-, Doppellinienpfeil), Knotenpunkte |
| `ann.labels` Beschriftungen & Text | Text, Beschriftung, Hinweis, Positionsnummer, Stückliste, Tabelle |
| `ann.dimensions` Bemaßung | lineares, paralleles, Winkel-, Radius-/Durchmessermaß, **Kettenmaß**; **Toleranzrahmen, Bezugselement, Oberflächensymbol, Schweißsymbol** |
| `ann.hatching` Schraffur | schraffierte Rechtecke, Ellipsen und Polygone mit Werkstoffmustern |
| `ann.patent` Zeichen für Abbildungen | Bezugslinien mit Bezugszeichen, Abbildungsbezeichnung, Schnittverlauf, Einzelheit-Kreis und -Ansicht, Mittellinie, Bruchlinie, Klammern, Ansichtspfeil, **Änderungswolke** |
| `ann.mechanics` Technische Mechanik | Kraft, Moment, Koordinatensystem, Schwerpunkt, Boden, Lager, Streckenlast (gleichmäßig, Dreieck, Trapez), Balken, Zwischengelenk, Feder, Dämpfer, Masse, **Schnittgrößenverläufe (Normalkraft, Querkraft, Biegemoment, Biegelinie)** |
| `ann.flow` Ablaufdiagramm | Ablaufdiagramm-Symbole und Verbinder; Ausgangsbeschriftungen von Verzweigungen (ja/nein oder 1/2/3, wahlweise auch an gezeichneten Ausgängen) und Schrittnummern entlang des Ablaufs, beide standardmäßig aus (siehe [diagrams.md](diagrams.md), „Ausgangsbeschriftungen und Schrittnummern aus dem Ablauf“) |

## Technische Zeichnungsangaben

| Typ-Id | Element | Parameter (alle optional) |
|---|---|---|
| `ann.gdt.frame` | Form- und Lagetoleranz: Toleranzrahmen mit dem Piktogramm der Eigenschaft, dem Toleranzwert und bis zu drei Bezugsbuchstaben; Bezugslinie mit Pfeil zum Element. Punkt 0 = Pfeilspitze am Element, Punkt 1 = das Rahmenende, an dem die Bezugslinie ansetzt; der Rahmen läuft von der Spitze weg. Symbolfamilie der ISO 1101. | `characteristic` (Geradheit, Ebenheit, Rundheit, Zylinderform, Linienform, Flächenform, Parallelität, Rechtwinkligkeit, Neigung, Position (Vorgabe), Konzentrizität/Koaxialität, Symmetrie, Lauf, Gesamtlauf), `tolerance` („0.1“; der Objekttext hat Vorrang), `diameter` (Ø vor dem Wert; bei Position und Konzentrizität standardmäßig an), `datum1` („A“), `datum2`, `datum3`, `textSize` (3,5) |
| `ann.gdt.datum` | Bezugselement: Dreieck am Element, verbunden mit einem quadratischen Rahmen mit dem Buchstaben. Punkt 0 = Mitte der Dreiecksgrundseite, Punkt 1 = Rahmenmitte. | `letter` („A“; der Objekttext hat Vorrang), `filled` (true; sonst offenes Dreieck), `textSize` |
| `ann.surface` | Oberflächensymbol: zwei ungleich lange Schenkel unter 60°, die Spitze auf der Oberfläche (Andockpunkt `tip`); Querstrich für „Materialabtrag gefordert“, Kreis für „Materialabtrag unzulässig“; die Anforderung unter einer Linie, die den langen Schenkel verlängert. Symbolfamilie der ISO 1302. | `process` (beliebig / Abtrag (Vorgabe) / kein Abtrag), `value` („Ra 3.2“; der Objekttext hat Vorrang, leerer Text = nur Symbol), `allAround` (Ringsum-Kreis an der Ecke), `textSize` |
| `ann.weld` | Schweißsymbol: Pfeillinie zum Stoß, Bezugslinie von der Spitze weg, gestrichelte Kennlinie, Grundsymbol auf der Pfeilseite (über der Bezugslinie), der Gegenseite (unter der Strichlinie) oder beidseitig (ohne Strichlinie). Punkt 0 = Pfeilspitze, Punkt 1 = Beginn der Bezugslinie. Symbolfamilie der ISO 2553 (Idee des Systems A). | `kind` (Kehlnaht, I-Naht, V-Naht, HV-Naht, Loch-/Schlitznaht), `side` (Pfeilseite / Gegenseite / beidseitig), `allAround` (Ringsum-Kreis am Knick), `size` (links vom Symbol, z. B. „a5“; der Objekttext hat Vorrang), `length` (rechts vom Symbol), `refLength` (24 mm), `textSize` |

Bibliotheksvorlagen: Ebenheit (`ann.gdt.frame.flatness`, ohne Bezüge), Parallelität (`ann.gdt.frame.parallelism`),
Lauf (`ann.gdt.frame.runout`) und V-Naht (`ann.weld.v`).

Eingebaute Regeln: Formtoleranzen (Geradheit, Ebenheit, Rundheit, Zylinderform) zeigen nie Bezugsbuchstaben, und die
Aktion „Bezüge“ ist für sie ausgegraut. Die Aktion „Bezüge“ liest bis zu drei Buchstaben, getrennt durch Leerzeichen,
Kommas, `|` oder `;` („A B C“).

## Querkraft- und Momentenverlauf (`ann.beam.diagram`)

Ein Kasten mit einem Band je Zeile, von oben nach unten: die Normalkraft N (nur wenn Lasten längs des Balkens wirken),
die Querkraft (Formelzeichen Q in deutschen, V in englischen Zeichnungen), das Biegemoment und auf Wunsch die
Biegelinie w. Jedes Band ist quer schraffiert (die Biegelinie ist eine einfache Kurve) und mit dem betragsgrößten Wert
beschriftet („max N = √2·F/2“, „max Q = F/2“, „max M = F·L/4“, „max w = F·L³/(3·EI)“); die Stelle des größten
Biegemoments und der größten Durchbiegung steht auf der anderen Seite der Achse („x = L/2“, „x = L/√3“, „x = 2.31 m“).
Einzelmomente zeigen sich als Sprünge der Momentenlinie, Gleichlasten als Parabeln, Dreiecks- und Trapezlasten als
parabelförmige Querkraft- und kubische Momentenlinien; Zwischengelenke sind als kleine offene Kreise auf der
Momentenachse markiert, wo das Moment null ist. Ohne Balken steht die Kastenbreite für die Balkenlänge; unter einem
Balken streckt sich der Verlauf von selbst auf die Balkenenden (siehe unten). Die Zeilen teilen sich die Kastenhöhe
gleichmäßig.

**Einen gezeichneten Balken lesen.** Unter einem `ann.beam` platziert (dem nächsten Balken über dem Verlauf, dessen
Breite die Mitte des Verlaufs überdeckt, höchstens 40 mm oder 1,5 Verlaufshöhen darüber), liest der Verlauf diesen
Balken. Seine Achsen laufen dann vom Anfang bis zum Ende des Balkens, auf den Verlauf projiziert, unabhängig von der
eigenen Breite des Verlaufs: Der Verlauf streckt sich von selbst auf den Balken (und folgt einem gespiegelten Balken,
sein Anfang unter dem Balkenanfang). Die Zeichnung kann dann über den Kasten des Verlaufs hinausreichen; die Aktion
**An den Balken anpassen** (`ann.beamdiagram.fit`, ausgegraut, wenn der Kasten schon passt) setzt den Kasten auf Breite
und Mitte des Balkens, sodass der Auswahlrahmen passt. Der Verlauf liest:

- Fest- und Loslager (`ann.support.pin`, `ann.support.roller`), deren Gelenkpunkt den Balken berührt (bis 3 mm);
- Einspannungen (`ann.support.fixed`), deren Wand das linke oder rechte Balkenende berührt;
- Kräfte (`ann.force`), deren Spitze den Balken berührt; schräge Kräfte zählen mit ihrer Komponente quer zum Balken
  (Kräfte, die von der Balkenunterseite weg zeigen, negativ) und mit ihrer Komponente längs des Balkens (positiv zum
  Balkenende hin), die die Normalkraft ergibt; eine Kraft längs des Balkens zählt nur mit dieser;
- Streckenlasten (`ann.load.distributed`), deren Unterkante auf dem Balken steht, gleichmäßig, dreieck- oder
  trapezförmig wie gezeichnet (die Intensität verläuft von der linken zur rechten Kastenkante, eine gespiegelte Last
  fällt also andersherum; eine Last, die über den Balken hinausreicht, zählt mit dem Teil, der auf ihm steht);
- Momente (`ann.moment`), deren Mitte auf dem Balken liegt, als Einzelmomente: der Drehsinn des Symbols (Vorgabe gegen
  den Uhrzeigersinn; Spiegeln des Symbols oder des Balkens kehrt ihn um) gibt das Vorzeichen, im Uhrzeigersinn wie
  gesehen positiv;
- Zwischengelenke (`ann.beam.hinge`), deren Mitte auf dem Balken liegt (Gerberträger); Gelenke an den Balkenenden
  werden nicht beachtet.

Lagen sind Anteile der Balkenlänge; Anteile, die höchstens 0,5 % von einem einfachen Bruch (Halbe bis Zwanzigstel)
abweichen, gelten als dieser Bruch, Lagen bis 3 mm vor einem Ende als das Ende.

**Werte.** Eine Lastbeschriftung wie „10 kN“, „F = 10 kN“ oder „q = 2,5 kN/m“ ist eine Zahl mit Einheit; der optionale
Parameter `value` einer Kraft, Streckenlast (ihr Größtwert) oder eines Moments („Betrag (Schnittgrößenverläufe)“) hat
Vorrang vor der Beschriftung. Eine Momentbeschriftung wie „6 kN·m“ gibt die Momenteinheit, aus der die Krafteinheit der
Querkraft genommen wird. Jede andere Beschriftung („F“, „q“, „F₁“, „M₀“) ist ein Formelzeichen. Der optionale
Balkenparameter `length` (m) gibt die wirkliche Länge. Die Normalkraft wird wie die Querkraft beschriftet: als Zahl mit
der Krafteinheit, wenn alle Lasten längs des Balkens Zahlen sind („max N = 7.071 kN“), als exaktes Vielfaches, wenn sie
ein Formelzeichen teilen, mit kurzen Vielfachen von √2 und √3 für die Komponenten unter 45°, 30° oder 60° geneigter
Lasten („√2·F/2“, „√3·F/2“).

| Lasten und Länge | Beschriftung |
|---|---|
| alle Lasten Zahlen, Länge angegeben | Zahlen mit Einheiten: „max Q = 5 kN“, „max M = 10 kN·m“ |
| Einzellasten als Zahlen, keine Länge | „max Q = 5 kN“, „max M = 2.5 kN·L“ |
| nur Einzelmomente als Zahlen, keine Länge | „max M = −6 kN·m“; die Querkraft nur, wenn sie null ist („max Q = 0“) |
| ein Formelzeichen einer Art (alle Einzellasten „F“, alle Streckenlasten „q“ oder alle Einzelmomente „M₀“) | exakte Vielfache: „F/2“, „F·L/4“, „q·L/2“, „q·L²/8“, „−F·L“, „M₀/L“, „−M₀“; kurze Brüche bis 1/64 (längere bis 1/512 nur, wenn sie auf 10⁻¹⁰ genau stimmen, etwa „13·F·L/128“ bei Durchlaufträgern), kurze Brüche über √3 („q·L²/(9√3)“, „L/√3“), sonst drei Nachkommastellen |
| gemischte Formelzeichen oder Arten, Formelzeichen mit Zahlen, Streckenlasten als Zahlen ohne Länge | „max Q“, „max M“ ohne Wert (die Verläufe mit Einheitswerten gezeichnet) |

**Ohne Balken** (oder über einem Balken ohne Lager und Lasten) zeigt der Verlauf seinen eigenen Lastfall: `support`
(Einfeldträger (Vorgabe) / Kragträger mit Wand links / Wand rechts / Gerberträger mit Einspannung links, Gelenk in der
Mitte und Loslager rechts / einseitig eingespannter Träger mit Wand links und Loslager rechts / Zweifeldträger mit
gleichen Feldern / beidseitig eingespannter Träger), `load` (Einzellast F (Vorgabe) / Streckenlast q / beides / Dreieckslast q nach rechts
steigend / Moment M₀ im Uhrzeigersinn), `at` (Lage von F oder M₀ als Anteil, 0,5) und optional `force` (kN), `lineLoad`
(kN/m, Größtwert), `couple` (kN·m) und `length` (m) für Werte. Vorlagen: `ann.beam.diagram.cantilever` (Endlast),
`ann.beam.diagram.uniform`, `ann.beam.diagram.triangle` (max M = q·L²/(9√3) bei x = L/√3), `ann.beam.diagram.moment`
(Kragträger mit Endmoment: max M = −M₀), `ann.beam.diagram.gerber` (Gleichlast: max M = −q·L²/4 an der Einspannung),
`ann.beam.diagram.propped` (einseitig eingespannter Träger, Gleichlast: max M = −q·L²/8 an der Einspannung,
max Q = 5·q·L/8), `ann.beam.diagram.continuous` (Zweifeldträger, Gleichlast: max M = −q·L²/32 über dem Mittellager, L
ist die Gesamtlänge), `ann.beam.diagram.fixed` (beidseitig eingespannt, Mittellast: max M = −F·L/8) und
`ann.beam.diagram.deflection` (Kragträger mit Endlast und Biegelinie: max w = F·L³/(3·EI) bei x = L; 60 mm × 66 mm).

**Weitere Parameter:** `momentBelow` (true: positive, durchbiegende Momente unter der Achse, auf der Zugseite, wie in
deutschen Lehrbüchern üblich; false: darüber), `textSize` (3,5).

**Biegelinie.** `deflection` (false; Aktion **Biegelinie** `ann.beamdiagram.deflection`, die den Kasten zugleich um eine
Zeile vergrößert, mit gleicher Oberkante und gleicher Höhe der anderen Zeilen, und beim Ausschalten wieder verkleinert)
fügt die Zeile w(x) hinzu, eine Durchbiegung nach unten nach unten gezeichnet. Ihre Form hängt nicht von der
Biegesteifigkeit ab, ihr Wert schon: Bei einem Formelzeichen einer Art ist er ein exaktes Vielfaches von F·L³/EI,
q·L⁴/EI oder M·L²/EI („F·L³/(3·EI)“, „5·q·L⁴/(384·EI)“; Nenner bis 2000, wenn exakt, sonst drei gültige Ziffern,
„0.00542·q·L⁴/EI“). Sind alle Lasten Zahlen und sind die Balkenlänge und die optionalen Parameter `modulus` E (N/mm²) und
`inertia` I (cm⁴) angegeben, steht er in mm da („max w = 12.698 mm“); Kräfte gelten als kN, außer die Beschriftungen
nennen N oder MN, Längen als m. Ohne E und I steht nur „max w“.

**Statik** (`BeamStatics`, reine Berechnung ohne Host): gerade Balken unter Einzellasten (`BeamPointLoad` mit einer
Komponente `F` quer zum Balken und einer optionalen Komponente `Axial` längs des Balkens;
`BeamPointLoad.Inclined(x, F, Winkel)` mit dem Winkel im Uhrzeigersinn von der Balkenachse gemessen, 90° nach unten),
Gleich-, Dreiecks- und Trapezlasten (`BeamLineLoad` mit einer Intensität an jedem Ende) und Einzelmomenten
(`BeamCouple`, im Uhrzeigersinn positiv) – auf Fest-, Los- und Einspannlagern, mit Zwischengelenken
(`BeamModel.Hinges`). Unbekannte sind eine Kraft je Fest- oder Loslager und eine Kraft und ein Einspannmoment je
Einspannung; Bedingungen sind die zwei Gleichgewichtsbedingungen (senkrechte Kräfte, Momente) und ein verschwindendes
Biegemoment an jedem Zwischengelenk. Mit Lasten längs des Balkens fügt jedes Festlager und jede Einspannung eine
waagerechte Lagerkraft hinzu und das waagerechte Gleichgewicht eine Bedingung (Loslager gleiten). `BeamStatics.Check`
vergleicht sie über den Rang der Bedingungsmatrix: **statisch bestimmt** (die Lagerreaktionen folgen aus den
Bedingungen), **n-fach statisch unbestimmt** (n Unbekannte mehr als unabhängige Bedingungen, z. B. Einspannung plus
Loslager: 1, drei Lager: 1, vier Lager: 2, beidseitig eingespannt: 2, zwei Festlager mit einer Last längs des Balkens: 1)
oder **kinematisch verschieblich** (eine Bedingung ist nicht erfüllbar: kein oder ein gelenkiges Lager, zwei Lager an
einer Stelle, ein Gelenk im Einfeldträger, eine Last längs eines Balkens nur auf Loslagern).

Statisch bestimmte Balken werden aus den Bedingungen gelöst. **Statisch unbestimmte Balken** (Durchlaufträger über drei
oder mehr Lager, einseitig eingespannte Träger, beidseitig eingespannte Träger, auch mit Gelenken, und ihre
Kombinationen) werden mit dem Weggrößenverfahren für einen prismatischen Balken gelöst: Knoten an den Enden, Lagern,
Lasten, Enden von Streckenlasten und Gelenken, je Knoten eine Durchbiegung und eine Neigung (zwei Neigungen an einem
Gelenk), Hermite-Balkenelemente mit den exakten äquivalenten Knotenlasten linearer Streckenlasten, sodass die
Knotenwerte für den Euler-Bernoulli-Balken exakt sind. Die Lagerreaktionen eines Balkens mit konstantem EI hängen nicht
von der Größe von EI ab, daher muss keine Steifigkeit eingegeben werden; die Normalkraft von Balken, die an mehreren
Lagern waagerecht gehalten sind, wird ebenso mit konstantem EA gelöst. Der Verlauf zeigt dann die Lösung und unter dem
Kasten den Hinweis „1-fach statisch unbestimmt, EI konstant“ („EA“ für die Normalkraft, „EI, EA“ für beides).
Kinematisch verschiebliche Balken werden nicht gelöst: Der Verlauf zeigt seine Achsen und statt Zahlen „kinematisch
verschieblich“ oder „keine Lager (verschieblich)“. Die Biegelinie (`BeamSolution.Deflection`, `Slope`, `MaxDeflection`,
`DeflectionCurve`, alle für EI = 1) folgt aus den Knotenwerten durch zweifache Integration des Biegemoments
(EI·w'' = −M, w nach unten) in jedem Abschnitt, für statisch bestimmte und unbestimmte Balken gleichermaßen; ihre
Extrema liegen, wo die Neigung null wird.

Vorzeichen: Normalkraft positiv als Zug (die Kräfte längs des Balkens links vom Schnitt mit umgekehrtem Vorzeichen),
Querkraft als Summe der aufwärts gerichteten Kräfte links vom Schnitt, Biegemoment positiv, wenn es den Balken
durchbiegt; ein Moment im Uhrzeigersinn lässt das Biegemoment um seinen Wert nach oben springen. In einem Abschnitt ist
die Querkraft ein Polynom höchstens zweiten Grades; die Extrema des Moments liegen, wo sie null wird (exakt gelöst; die
gezeichnete Linie geht durch diese Scheitel). Ein Moment genau auf einem Gelenk wirkt auf den Teil rechts davon.
Unit-Tests decken die Lehrbuchfälle ab (Einfeldträger mit Mittellast: Lagerkräfte F/2, M_max = F·L/4; Gleichlast:
M_max = q·L²/8; Dreieckslast: Lagerkräfte q·L/6 und q·L/3, M_max = q·L²/(9√3) bei x = L/√3; Trapezlast als Gleich- plus
Dreieckslast; Kragträger mit Endlast: M = −F·L an der Einspannung; Kragträger mit Endmoment: konstant M = −M₀;
Kragträger mit Dreieckslast: M = −q·L²/6; Moment am Einfeldträger mit seinem Sprung; Gerberträger mit einem Gelenk
(Einspannung, Gelenk, Loslager) und Zweifeld-Gerberträger auf drei Lagern; außermittige Last F·a·b/L; Kragarm; einseitig
eingespannter Träger mit Gleichlast: R_B = 3·q·L/8, M_A = −q·L²/8, Feldmoment 9·q·L²/128 bei 5·L/8; mit Mittellast:
R_B = 5·F/16; Zweifeldträger mit Gleichlast: Mittellager 5·q·l/4, M_B = −q·l²/8; Dreifeldträger: Innenlager 1,1·q·l,
M = −0,1·q·l²; beidseitig eingespannt mit Mittellast: M = ∓F·L/8, w = F·L³/(192·EI), mit Gleichlast: −q·L²/12,
q·L²/24, w = q·L⁴/(384·EI); Durchbiegung des Kragträgers F·L³/(3·EI) und q·L⁴/(8·EI); Einfeldträger 5·q·L⁴/(384·EI) und
F·L³/(48·EI); der Knick der Biegelinie eines Gerberträgers am Gelenk; eine geneigte Last mit ihrer Normalkraft; zwei
Festlager, die sich eine Last längs des Balkens teilen; die Einstufung; Gleichgewichtsproben).

Neuzeichnen: Der Verlauf hängt von seinen Nachbarn ab; Balken, Lager, Kräfte, Streckenlasten, Momente und Gelenke nennen
die Verläufe auf ihrer Seite als abhängige Objekte, sodass das Verschieben einer Last den Verlauf neu zeichnet.

## Weitere Ergänzungen

| Typ-Id | Element | Parameter (alle optional) |
|---|---|---|
| `ann.cloud` | Änderungswolke: eine geschlossene Kette nach außen gewölbter Bögen im Kasten, die einen geänderten Bereich markiert | `arc` (Bogenlänge, 5 mm) |
| `ann.dimension.chain` | Kettenmaß: mehrere Punkte nacheinander auf einer Maßlinie bemaßt, ein Wert je Abschnitt; die Punkte werden in ihrer Reihenfolge längs der Messrichtung genommen | `offset` (−8 mm), `axis` (automatisch / waagerecht / senkrecht), `terminator`, `prefix`, `unit`, `decimals`, `scale`, `textSize` (kein gemeinsamer Ersatztext) |
| `ann.beam` | erhält eine optionale Länge `length` (m) für die Werte der Verläufe sowie Begleiter (Lager, Kraft, Streckenlast, Moment, Gelenk, Verlauf) | |
| `ann.force` / `ann.load.distributed` / `ann.moment` | erhalten einen optionalen Betrag `value` (Kraft / größte Last je Länge / Moment) für die Verläufe | |
| `ann.load.distributed` | die Pfeile folgen der Intensität; die Auswahl „Form“ (mit Bildern) wechselt die Form | `shape` (gleichmäßig (Vorgabe) / Dreieck nach rechts steigend / Dreieck nach rechts fallend / Trapez), `low` (niedriges Ende des Trapezes als Anteil von q, 0,4). Vorlagen `ann.load.distributed.triangle` und `ann.load.distributed.trapezoid` |
| `ann.beam.hinge` | Zwischengelenk (Gerbergelenk): ein kleiner offener Kreis, mittig auf der Balkenachse; die Verläufe lesen es als momentenfreies Gelenk | keine (5 mm × 5 mm) |

## Hinweise zu den Hosts

Alles wird mit Linien, Polylinien, Polygonen, Rechtecken, Kreisen und Text gezeichnet, sodass jeder Host native,
bearbeitbare Formen zeichnet. Die Bänder des Schnittgrößenverlaufs nutzen eine Schraffurfüllung (senkrechte Linien), die
Hosts wie andere schraffierte Flächen zeichnen. Toleranz- und Bezugsrahmen sind weiß gefüllt und verdecken, was dahinter
liegt. Die Optionsbilder der Auswahlen (`Preview`) zeigen Hosts, die Vorschauen darstellen; andere listen die
Optionsnamen.

## Offene Punkte

- Modifikatoren im Toleranzrahmen (Maximum-Material, projizierte Toleranzzone, freier Zustand), kombinierte Rahmen und
  Toleranzangaben an Maßzahlen sind nicht abgebildet.
- Schweißsymbole: keine Zusatzsymbole (flach, gewölbt, hohl), kein Gabelende mit Prozessnummer, keine versetzten Nähte.
- Schnittgrößenverläufe: Streckenlasten höchstens linear und quer zum Balken (keine verteilten Lasten längs des
  Balkens), Einspannungen nur an den Balkenenden, konstanter Querschnitt für statisch unbestimmte Balken und die
  Biegelinie (keine abgestuften Balken, keine Schubverformung, keine Federlager oder Lagersenkungen); der Wert der
  Durchbiegung in mm setzt Kräfte in N, kN oder MN und Längen in m voraus. Unter einem Balken streckt sich die Zeichnung
  des Verlaufs auf den Balken, sein Kasten ändert sich aber nur mit der Aktion „An den Balken anpassen“ (die Engine hat
  keinen Haken, der ein Objekt bei Änderung eines Nachbarn in der Größe ändert).
