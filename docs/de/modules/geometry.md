# Modul Mathematik & Geometrie (`math`)

Koordinatensysteme, Funktionsgraphen, Punkte und Vektoren, Winkel, Dreiecke, Kreise und Zirkelkonstruktionen – für
Unterrichts- und Erklärfolien. Jedes Element wird mit nativen Grundformen gezeichnet (Linien, Linienzüge, Polygone,
Ellipsen, Text), bleibt mit den eigenen Werkzeugen des Hosts bearbeitbar und ist ohne Parameter einfügbar. Parameter
sind optionale Anreicherung: Bereiche, Koeffizienten, Winkel, Beschriftungen und die gezeigte Konstruktion.

Die Zeichnungen folgen üblicher Schul- und Lehrbuchpraxis (Achsen mit Pfeilspitzen, kursive Variablen, aufrechte
Punktnamen, Zirkelbögen, Rechter-Winkel-Quadrat mit Punkt). Es werden keine Normtexte oder -tabellen wiedergegeben
und keine Normkonformität behauptet.

## Verhältnis zu anderen Modulen

| Thema | Modul | Hinweis |
|---|---|---|
| Kleines Koordinatensystem-Symbol für Freikörperbilder (`ann.coordsys`), Winkel- und Radiusmaße (`ann.dimension.angle`, `ann.dimension.radius`) | annotation | technische Zeichnungszeichen, ohne Bereiche oder Gitter |
| Datendiagramme (`chart.xy`, `chart.axes`, Balkendiagramme, Kennlinien) | charts | Mess- oder Auslegungsdaten mit Einheiten und Legenden |
| 2D-Koordinatensystem und Funktionsgraphen im Mathematikstil (`chart.math.axes`, `chart.math.function`) | charts | in Diagramme v2, Stufe 4 dorthin umgezogen, siehe [unten](#ins-diagrammmodul-umgezogen-diagramme-v2-stufe-4-25112026) |
| Koordinatensysteme mit Gitter und Gitterpunkten zum Andocken, Funktionsgraphen aus einer Formelfamilie, Vektoren, aus der Zeichnung gemessene Winkel, Schulkonstruktionen | **math** | dieses Modul (die Typen 2D-Koordinatensystem und Funktionsgraph bleiben für bestehende Folien) |

Die Module hängen nicht voneinander ab. Linien jedes Moduls docken an den Anschlüssen dieses Moduls an
(`AnchorRole.Port`).

## Ins Diagrammmodul umgezogen (Diagramme v2, Stufe 4, 25.11.2026)

Das 2D-Koordinatensystem und die Funktionsgraphen bietet das Diagrammmodul an, damit alle Diagramme ein Achsensystem
teilen (Achsenbrüche, Teile, Export): `chart.axes.math` (Koordinatensystem (2D)), `chart.axes.math.quadrant`,
`chart.function` (Parabel), `chart.function.linear`, `chart.function.sine`, `chart.function.exponential`, dazu eine
eingegebene Formel (`chart.function.formula`) und ein Funktionsgenerator (`chart.function.generator`); siehe
[Diagramme](charts.md#koordinatensysteme-und-funktionsgraphen-diagramme-v2-stufe-4).

- Die Einträge `math.axes.cartesian`, `math.axes.cartesian.quadrant`, `math.graph.function`,
  `math.graph.function.linear`, `math.graph.function.sine` und `math.graph.function.exponential` nennen diese Einträge
  als `LibraryItem.Alternative`: solange das Diagrammmodul registriert ist, fehlen sie in Galerien und Suche (die
  Kategorie „Funktionsgraphen“ entfällt dann); per Id werden sie weiter gefunden, und die Typen `math.axes.cartesian`
  und `math.graph.function` bleiben registriert, bestehende Folien werden also weiter gezeichnet und bearbeitet. Für
  sich allein bietet das Mathematikmodul weiterhin alles an.
- Punkte und Vektoren docken an der Diagrammfassung wie bisher an: der Ursprung ist ein Anschluss, die Achsen sind
  Schienen (`axis.x`, `axis.y`), jeder Gitterpunkt ist ein Fangpunkt (`g<x>_<y>` mit den Datenwerten, z. B. `g2_3`),
  Funktionsgraphen haben die Anschlüsse `zero0…`, `ext0…` und `yint`; Vektorenden docken auch frei auf der Fläche des
  Diagramms an und folgen ihm (getestet in `ChartsMergeTests`). Der Vektor schlägt das Diagramm-Koordinatensystem als
  Begleiter vor (ohne Diagrammmodul das des Mathematikmoduls).
- Zahlenstrahl, 3D-Achsen und Polarkoordinatennetz bleiben in diesem Modul.

## Konventionen

- **Quick Sketch zuerst:** Jedes Element ist ohne Parameter einfügbar; freies (auch ungleichmäßiges) Skalieren ändert
  `Size`, nie Parameter, und jede Zeichnung bleibt in ihrer Box (getestet in gestreckten, gestauchten und sehr kleinen
  Größen mit jeder Parametervariante).
- **Was sich streckt und was stimmt:** Koordinatensysteme, Zahlenstrahl, Funktionsgraphen und die Parallelen strecken
  sich mit der Box (ungleichmäßiges Skalieren ändert die Einheiten von x und y unabhängig, die Bereiche bleiben).
  Dreiecke strecken sich ebenfalls, aber ein rechtwinkliges Dreieck behält seinen rechten Winkel (Katheten entlang der
  Boxkanten) und ein gleichschenkliges bleibt gleichschenklig; die Konstruktionen (Inkreis, Umkreis,
  Seitenhalbierende, Höhen) werden aus den gestreckten Ecken berechnet und stimmen daher immer. Kreise, regelmäßige
  Vielecke, die 3D-Achsen, das Polarkoordinatennetz, der Einheitskreis, Thales, die Tangente und die
  Zirkelkonstruktionen behalten ihre Form: Sie werden gleichmäßig in die Box eingepasst und zentriert.
- **Winkel:** Parameter, die etwas auf einem Kreis platzieren (Berührpunkt, Punkt C, Winkel am Einheitskreis,
  Polarnetz, Winkel der Querlinie oder des halbierten Winkels), verwenden den mathematischen Drehsinn: Grad **gegen den
  Uhrzeigersinn** ab der positiven x-Achse, wie in der Schulmathematik (wie die Schraffurwinkel im Core). Die eigene
  Drehung des Objekts bleibt wie bei allen Objekten im Uhrzeigersinn auf dem Bildschirm.
- **Beschriftung:** Variablen kursiv (x, y, a, b, c, α, r, d, t), Punktnamen aufrecht (A, B, C, M, P). Text skaliert
  mit der Box relativ zu ihrer Entwurfsgröße (kleinerer Faktor, 0,5 … 2,5), sodass ungleichmäßiges Strecken ihn nie
  verzerrt; Punkttypen (Vektor, Vektoraddition, Winkel) haben einen Parameter `textSize` (4 mm ≈ 11 pt). Zahlen mit
  Dezimalkomma im Deutschen und Minuszeichen „−“. Sprachabhängige Namen: Kreismittelpunkt O (EN) / M (DE);
  Inkreismittelpunkt I / W, Umkreismittelpunkt O / U, Schwerpunkt G / S, Höhenschnittpunkt H / H; Berührpunkt einer
  Tangente T / B.
- **Linienarten (Ideen aus ISO 128):** Figurumrisse und Vektoren als breite Volllinie (0,5 mm), Achsen 0,35 mm mit
  gefüllter Pfeilspitze, Gitterlinien schmal grau (0,15 mm), Konstruktionslinien schmal (0,25 mm), Hilfslinien
  (Vektorkomponenten, Projektionen, Parallelogrammkopien) gestrichelt (Strichlinie).
- **Farben (Modulkonvention):** Konstruktionen und Funktionskurven blau `#1565c0`, Ergebnisse rot `#c62828`
  (Resultierende einer Vektoraddition, Mittelsenkrechte und Winkelhalbierende, Tangente, Sinus), zweiter Vektor und
  Tangensstrecke grün `#2e7d32`, Kosinus blau. Die Schwarz-Weiß-Einstellung des Dokuments und die Akzentfarbe gelten
  wie üblich.
- **Stilüberschreibungen:** Eine Strichfarbe färbt jede Linie, jeden Text und jede Punktmarkierung des Elements um; die
  Breite gilt für den Hauptstrich und Umrisse, Strichart und Pfeilspitzen für den Hauptstrich, eine Füllung für den
  geschlossenen Umriss von Dreieck, Vieleck oder Kreis – er wird zuerst gezeichnet, sodass die Füllung keine
  Konstruktion verdeckt.
- **Figuren, keine Bauteile:** `IsPart` ist für alle Typen falsch (keine Stücklisteneinträge).

## Objekttypen

Größen sind Entwurfsgrößen (B x H, mm). „Punkte“ = Punktgeometrie (Seitenkoordinaten, Enden docken wie Linienenden an).

| Id | Name (EN / DE) | Größe | Parameter |
|---|---|---|---|
| `math.axes.cartesian` | Coordinate system (2D) / Koordinatensystem (2D) | 70x60 | `xMin`/`xMax` (−5…5), `yMin`/`yMax` (−4…4), `xStep`/`yStep` (1), `grid` (an), `numbers` (an), `xPi` (x-Teilung in Vielfachen von π/2), `xLabel` („x“), `yLabel` („y“) |
| `math.axes.number` | Number line / Zahlenstrahl | 90x14 | `min`/`max` (−5…5), `step` (1), `minor` (Zwischenstriche zwischen den Zahlen, 0–9), `numbers` (an), `label` (Name am Pfeil) |
| `math.axes.spatial` | Coordinate system (3D) / Koordinatensystem (3D) | 50x50 | `projection` Schrägbild (x unter 45°, verkürzt um ½√2) / isometrisch, `names` x, y, z / x₁, x₂, x₃, `ticks` (Einheitsstriche, 4), `negative` (negative Halbachsen, an), `numbers` („1“ am ersten Strich) |
| `math.axes.polar` | Polar grid / Polarkoordinatennetz | 64x64 | `rings` (4, 1–10), `step` 15/30/45/90°, `unit` Grad / Bogenmaß (π/6, π/3 …), `angleLabels`, `radiusLabels` |
| `math.graph.function` | Function graph / Funktionsgraph | 70x60 | `function` linear / quadratisch / kubisch / Sinus / Kosinus / exponentiell (eˣ) / Logarithmus (ln x) / Wurzel / Betrag / Kehrwert; Koeffizienten von y = a·f(b·(x − c)) + d: `a`, `b`, `c`, `d`; `formula` (an); alle Achsenparameter (Standard x −4…4, y −2…8) |
| `math.point.labelled` | Point with label / Punkt mit Beschriftung | 14x10 | `label` („A“), `marker` Punkt / Kreuz / Kreis, `position` der Beschriftung (8 Richtungen, rechts oben) |
| `math.vector.arrow` | Vector / Vektor | Punkte (2) | `label` („v“), `notation` Pfeil darüber / fett / ohne, `components` (gestrichelte x- und y-Komponente mit v<sub>x</sub>, v<sub>y</sub>), `tailLabel`, `tipLabel`, `points` (Punkte an beiden Enden), `textSize` |
| `math.vector.sum` | Vector addition / Vektoraddition | Punkte (3) | `construction` Parallelogramm (Punkte O, Spitze von a, Spitze von b) / Aneinanderhängen (O, Spitze von a, Spitze des an a gehängten b), `labelA` („a“), `labelB` („b“), `notation`, `result` (Resultierende a + b, an), `textSize` |
| `math.angle.mark` | Angle / Winkel | Punkte (3) | Punkt auf dem ersten Schenkel, Scheitel, Punkt auf dem zweiten Schenkel; `label` („α“), `mark` automatisch (Quadrat bei 90°) / Bogen / Doppelbogen / Rechter-Winkel-Quadrat, `value` (gemessener Wert, z. B. „α = 45°“), `rays` (an), `reflex` (andere Seite), `radius` (7 mm), `textSize` |
| `math.shape.triangle` | Triangle / Dreieck | 50x40 | `kind` allgemein / rechtwinklig (γ = 90° bei C) / gleichschenklig / gleichseitig / maßstäblich nach den Werten, Werte `a`, `b`, `c`, `alpha`, `beta`, `gamma`, `area` (siehe [Runde 7](#dreieckswerte-lebendige-konstruktionen-und-kegelschnitte-26112026-runde-7)), `showValues`, `construction` keine / Inkreis (Winkelhalbierende) / Umkreis (Mittelsenkrechte) / Seitenhalbierende und Schwerpunkt / Höhen und Höhenschnittpunkt, `vertexLabels`, `sideLabels`, `angleMarks` (α, β, γ) |
| `math.shape.polygon` | Regular polygon / Regelmäßiges Vieleck | 40x40 | `sides` (6, 3–12; waagerechte Unterkante), `circumcircle`, `incircle`, `centre`, `labels` (A, B, C …) |
| `math.circle.basic` | Circle with radius / Kreis mit Radius | 40x40 | `show` Radius / Durchmesser / beides / nur Kreis, `angle` (Richtung des Radius, 45°; der Durchmesser bei „beides“ liegt waagerecht), `centre`, `labels` |
| `math.circle.tangent` | Tangent to a circle / Tangente an einen Kreis | 56x44 | `angle` (Lage des Berührpunkts, 60°), `radius` (Radius und rechter Winkel), `labels` |
| `math.circle.thales` | Thales circle / Thaleskreis | 60x40 | `angle` (Lage von C, 115°, 1–179), `full` (Vollkreis statt Halbkreis), `centre`, `labels` |
| `math.circle.unit` | Unit circle / Einheitskreis | 64x64 | `angle` (40°), `label` („α“), `tan` (Tangensstrecke auf x = 1), `values` (cos α = 0,77 …) |
| `math.construct.perpendicular` | Perpendicular bisector / Mittelsenkrechte | 50x40 | `arcs` (Zirkelbögen), `labels` (A, B, M, m) |
| `math.construct.anglebisector` | Angle bisector / Winkelhalbierende | 56x40 | `angle` (70°, 10–170), `arcs` (Zirkelbögen), `labels` (S, w) |
| `math.lines.parallel` | Parallel lines and transversal / Parallelen mit Querlinie | 64x44 | `angle` der Querlinie (60°, 20–160), `pair` Stufenwinkel / Wechselwinkel / Nachbarwinkel (Ergänzungswinkel) / alle / keine, `values` (Grad statt α, β), `labels` (g, h) |

**Funktionsgraph:** Es gibt bewusst keinen freien Formelparser. Die zehn Grundfunktionen mit den vier Koeffizienten
decken die üblichen Schulbeispiele ab (Scheitelpunktform einer Parabel, gestreckter und verschobener Sinus,
Wachstum). Die Kurve wird abgetastet (800 Punkte über den Bereich, vereinfacht), exakt am Zeichenbereich
abgeschnitten (Bisektion) und an der Polstelle von 1/x sowie am Definitionsrand von ln und √ getrennt (keine
Sprunglinie über einen Pol). Die Funktionsgleichung steht in Schulschreibweise und vereinfacht da
(„f(x) = 2(x − 1)² − 3“, „f(x) = 0,5x + 1“, „f(x) = e⁻ˣ“, „f(x) = 2·sin(3x) + 1“; Exponenten, die sich nicht hochgestellt
schreiben lassen, werden zu „exp(…)“) und in der Ecke des Zeichenbereichs platziert, in der sie weder Kurve noch Achse
verdeckt; Achsenzahlen wechseln auf die andere Seite ihrer Achse, wo die Kurve durch sie läuft.

**Teilung:** Zahlen stehen an jedem n-ten Teilstrich, sodass sie sich nie überlappen; der Achsenschnittpunkt wird
einmal mit „0“ beschriftet, wenn er der Ursprung ist. Bereiche mit mehr als 100 Teilstrichen verdoppeln die Teilung.
`xPi` beschriftet die x-Achse in Vielfachen von π/2 (π bei breiten Bereichen): „π/2“, „π“, „3π/2“, „−π“.

## Körper, Kreissektoren, Pythagoras, Mengen und Wahrscheinlichkeit (26.11.2026)

| Id | Name (EN / DE) | Größe | Parameter |
|---|---|---|---|
| `math.solid.cube` | Cube (cuboid) / Würfel (Quader) | 44x44 | `projection` Kabinettprojektion (Tiefe ½, 45°) / Kavalierprojektion (Tiefe 1, 45°), `hidden` (verdeckte Kanten gestrichelt, true), `labels` (a, h) |
| `math.solid.cylinder` | Cylinder / Zylinder | 36x44 | wie Würfel; Beschriftung r, h |
| `math.solid.cone` | Cone / Kegel | 36x42 | wie Würfel; Beschriftung r, h (Radius und Höhe innen gestrichelt) |
| `math.solid.sphere` | Sphere / Kugel | 40x40 | wie Würfel; Beschriftung r |
| `math.solid.pyramid` | Square pyramid / Quadratische Pyramide | 40x40 | wie Würfel; Beschriftung a, h |
| `math.arc.sector` | Circle sector / Kreissektor (Kreisausschnitt) | 40x40 | `kind` Sektor / Segment, `angle` (Mittelpunktswinkel, 70°, 1–359), `start` (Richtung des ersten Radius, 20°), `angleLabel` Wert (70°) / Buchstabe (α) / keine, `circle` (restlicher Kreis), `fill`, `labels` (r) |
| `math.shape.pythagoras` | Pythagoras (squares on the sides) / Satz des Pythagoras (Quadrate) | 50x54 | Katheten `a` (3) und `b` (4), `areas` a², b², c² / Werte (9, 16, 25) / keine, `labels` (a, b, c), `grid` (Einheitsquadrate bei ganzzahligen Seiten), `fill` |
| `math.venn.diagram` | Venn diagram / Venn-Diagramm (Mengendiagramm) | 60x44 | `sets` (2–3), `labelA`/`labelB`/`labelC`, `universe` (Rahmen), `universeLabel` (leer: U auf Englisch, G auf Deutsch), `fill` (durchscheinende Tönung) |
| `math.tree.probability` | Probability tree / Baumdiagramm (Wahrscheinlichkeit) | 84x54 | `levels` (Stufen, 1–3), `branches` (2–3 je Knoten), `e1`…`e3` (Ergebnisse je Stufe, „A; Ā“), `p1`…`p3` (Wahrscheinlichkeiten je Stufe), `products` (Pfadwahrscheinlichkeiten) |

**Körper (Schrägbild):** Die Frontebene wird in wahrer Größe gezeichnet, die Tiefe läuft unter 45° nach rechts oben,
verkürzt um q = ½ (Kabinettprojektion, Vorgabe) oder q = 1 (Kavalierprojektion). Bei Polyedern (Würfel, Pyramide)
wird die Sichtbarkeit je Fläche bestimmt: Eine Fläche ist sichtbar, wenn ihre äußere Normale zum Betrachter der
Projektion zeigt, eine Kante, wenn eine ihrer Flächen sichtbar ist; verdeckte Kanten erhalten `LineRole.Hidden`
(gestrichelt). Zylinder und Kegel verwenden die exakte Projektion des Grundkreises (eine leicht gekippte Ellipse); die
Mantellinien des Zylinders berühren sie an den breitesten Stellen, die Mantellinien des Kegels sind die Tangenten von
der Spitze; der hintere Teil des Grundkreises ist verdeckt. Die Kugel wird wie üblich gezeichnet (Umrisskreis, Äquator
als Ellipse mit der Tiefe nach q, hintere Hälfte gestrichelt), denn ihr exakter Schrägbild-Umriss wäre eine schiefe
Ellipse. Der Körper füllt seine Box: Eine quadratische Box ergibt einen Würfel; freies (auch ungleichmäßiges) Skalieren
ergibt einen Quader mit quadratischer Grundfläche bzw. einen höheren oder flacheren Zylinder, Kegel oder eine Pyramide
(die Kugel bleibt rund). Anker: Ecken der Polyeder als Anschlüsse `v0…`, Spitze (`AnchorRole.Apex`), Mittelpunkte von
Deck- und Grundfläche. Kontextaktionen: Projektion (Auswahl), verdeckte Kanten, Beschriftung.

**Sektor und Segment:** Der Sektor wird von zwei Radien und dem Bogen begrenzt (geschlossener Umriss ab dem
Mittelpunkt), das Segment von Sehne und Bogen (Radien als dünne Konstruktionslinien). Der Mittelpunktswinkel ist mit
einem Bogen markiert und mit seinem Wert (Dezimalkomma auf Deutsch) oder α beschriftet. Griffe: Der erste Radius dreht
die Figur (`start`), der zweite legt den Winkel fest. Kontextaktionen: Sektor oder Segment, „Mittelpunktswinkel…“,
Winkelbeschriftung, restlicher Kreis, Tönung, Radiusbeschriftung.

**Pythagoras:** rechter Winkel bei C, Hypotenuse c waagrecht unten mit ihrem Quadrat darunter, die Kathetenquadrate
nach außen; die Seitenbuchstaben stehen in den Quadraten entlang der Dreiecksseiten. Die Figur behält ihre Form
(gleichmäßig eingepasst). Kontextaktionen: Beschriftung der Quadrate, Seitenbeschriftung, Einheitsquadrate, Tönung.

**Venn-Diagramm:** gleich große Kreise mit Mittelpunktsabstand = Radius (zwei Mengen nebeneinander, drei im Dreieck),
Namen in den Teilen, die nur zu einer Menge gehören, Tönungen als eigene durchscheinende Kreise unter den Umrissen, die
Grundmenge als Rahmen der Box. Kontextaktionen: zwei/drei Mengen, „Mengennamen…“ („A; B; C“), Grundmenge, Tönung.

**Baumdiagramm:** Die Blätter sind gleichmäßig verteilt, jeder Knoten steht mittig über seinen Kindern; Ergebnisse an
den Knoten, Wahrscheinlichkeiten an den Ästen (über steigenden, unter fallenden Ästen; der mittlere von drei Ästen im
Keil darüber), Pfadwahrscheinlichkeiten („= 0,18“, Pfadregel) hinter den Blättern. Wahrscheinlichkeiten werden je Stufe
durch „;“ getrennt eingegeben („0.3; 0.7“, „0,3“, „30 %“, „1/6“); mehrere Knoten einer Stufe durch „|“ („0.6 | 0.2“), die
letzte Gruppe gilt für die übrigen Knoten. Fehlende Werte teilen sich den Rest bis 1 (ein fehlender Wert ist das
Gegenereignis); Brüche bleiben exakt („1/3“, Produkt „1/12“), Prozente bleiben Prozente, ein unmöglicher Rest zeigt
„?“. Text, der keine Wahrscheinlichkeit ist, wird ignoriert. Kontextaktionen: mehr/weniger Stufen, zwei/drei Äste,
„Wahrscheinlichkeiten Stufe n…“, „Ergebnisse Stufe n…“, Pfadwahrscheinlichkeiten.

## Dreieckswerte, lebendige Konstruktionen und Kegelschnitte (26.11.2026, Runde 7)

| Id | Name (EN / DE) | Größe | Parameter |
|---|---|---|---|
| `math.free.triangle` | Triangle through three points / Dreieck durch drei Punkte | Punkte (3) | `construction` (wie beim Dreieck), `vertexLabels`, `sideLabels`, `angleMarks`, `values` (gemessene Winkel „α = 63,4°“), `textSize` |
| `math.free.polygon` | Polygon with area and centroid / Vieleck mit Fläche und Schwerpunkt | Punkte (3 oder mehr, Ecken einfügbar) | `area` (Beschriftung „A = 12 cm²“), `centroid` (S / G), `vertexLabels` (A, B, C …), `scale` (1 : n, Vorgabe 1:1), `textSize`, `showValues` |
| `math.free.bisector` | Perpendicular bisector of two points / Mittelsenkrechte zu zwei Punkten | Punkte (2) | `segment` (AB), `arcs` (Zirkelbögen), `labels` (A, B, M, m), `textSize` |
| `math.free.anglebisector` | Angle bisector of three points / Winkelhalbierende zu drei Punkten | Punkte (3: auf dem ersten Schenkel, Scheitel, auf dem zweiten Schenkel) | `arcs`, `values` (gemessene halbe Winkel), `labels` (S, w), `textSize` |
| `math.free.tangents` | Tangents from a point to a circle / Tangenten von einem Punkt an einen Kreis | Punkte (3: Mittelpunkt M, ein Punkt auf dem Kreis, P) | `thales` (Thaleskreis über MP), `radii` (Radien und rechte Winkel), `labels` (M/O, P, B₁, B₂ / T₁, T₂), `textSize` |
| `math.conic.section` | Conic section (foci) / Kegelschnitt (Brennpunkte) | 60x40 | `kind` Ellipse / Hyperbel / Parabel, `point` (P mit seinen Abständen), `position` von P (50°), `formula` (definierende Eigenschaft), `axes` (und Asymptoten), `labels` |

**Dreieckswerte** (`math.shape.triangle`, Werte nach Calc, `IValueType`): Seiten `a`, `b`, `c` (cm), Winkel `alpha`,
`beta`, `gamma` (°), Flächeninhalt `A` (cm², Parameter `area`) und Umfang `u`, alles optionale Textparameter wie
eingegeben („5 cm“, „60°“). Relationen: Winkelsumme, dreimal der Kosinussatz (`math.triangle.cosine.a/b/c`: eine Seite
aus zwei Seiten und dem eingeschlossenen Winkel, ein Winkel aus drei Seiten), dreimal der Sinussatz
(`math.triangle.sine.*`: eine Seite aus einer Seite und zwei Winkeln, ein Winkel aus zwei Seiten und einem
gegenüberliegenden Winkel, im mehrdeutigen Fall die spitze Lösung), der Flächeninhalt A = ½·a·b·sin γ und der Umfang.
Drei unabhängige Werte mit mindestens einer Seite (SSS, SWS, SsW, WSW, SWW) ergeben den Rest; widersprüchliche Werte
werden als Konflikt gemeldet. Eingegebene Seiten erscheinen in der Seitenbeschriftung („a = 5 cm“), eingegebene Winkel
werden mit ihrem Wert markiert („γ = 60°“, auch ohne Winkelmarkierung), ein eingegebener Flächeninhalt steht im Dreieck
(ohne Konstruktion). Die neue Art **„Maßstäblich nach den Werten“** (`values`) zeichnet die Form, die die eingegebenen
Werte ergeben (zwei oder drei Winkel allein ergeben die Form; Werte, die kein Dreieck ergeben, fallen auf die
allgemeine Form zurück), gleichmäßig in die Box eingepasst, sodass freies Skalieren sie behält. Element
`math.shape.triangle.values`: a = 5 cm, b = 4 cm, γ = 60° (SWS).

**Lebendige Konstruktionen auf Punkten:** Die freien Figuren sind Punktobjekte wie Vektor und Winkel. Ihre Punkte docken
an Anschlüssen, Gitterpunkten und Flächen anderer Figuren an und folgen ihnen (`Attachments.Refresh`): Eine
Mittelsenkrechte, deren Enden an den Ecken A und B eines Dreiecks angedockt sind, bleibt die Mittelsenkrechte von AB,
wenn das Dreieck verschoben, gedreht oder gestreckt wird (getestet). Das Dreieck durch drei Punkte zeichnet dieselben
Konstruktionen wie das Box-Dreieck (Inkreis mit Winkelhalbierenden, Umkreis mit Mittelsenkrechten, Seitenhalbierende
und Schwerpunkt, Höhen und Höhenschnittpunkt; gemeinsamer Code `Tri.Construct`) aus seinen Ecken, sodass das Ziehen
einer Ecke sie mitführt; Elemente `math.free.triangle.circles` (Umkreis) und `math.free.triangle.incircle`. Die
Tangenten von P werden mit dem Thaleskreis über MP konstruiert: B₁, B₂ (englisch T₁, T₂) sind seine Schnittpunkte mit dem
Kreis; liegt P im Kreis, werden nur Kreis und P gezeichnet. Das Vieleck misst seinen Flächeninhalt mit der Gaußschen
Trapezformel (ein sich selbst schneidender Umriss zählt seine Schleifen mit Vorzeichen) und seinen
Flächenschwerpunkt; die Werte `A` und `u` sind schreibgeschützt (aus der Zeichnung im Maßstab `scale`).

**Kegelschnitte:** Die Kurve füllt die Box, freies (auch ungleichmäßiges) Skalieren ändert also die Halbachsen, die
Brennpunkte folgen. Ellipse: a und b sind die halben Boxseiten, c² = a² − b² (Brennpunkte auf der längeren Achse),
native Ellipse. Hyperbel: a = 0,4 der halben Breite, b so gewählt, dass beide Äste in der Box bleiben, c² = a² + b²,
Asymptoten gestrichelt. Parabel: Leitlinie unten, Scheitel S (englisch V) und Brennpunkt F im Abstand p, die Kurve
erreicht die oberen Ecken. Der Punkt P (`position`: der Parameterwinkel, bei Hyperbel und Parabel legt sein Sinus P
entlang des Astes fest) zeigt die Abstände der definierenden Eigenschaft: |PF₁| + |PF₂| = 2a, ||PF₁| − |PF₂|| = 2a,
|PF| = |PL| (L Lotfußpunkt auf der Leitlinie). Anschlüsse: `centre`, `F1`, `F2` (`F`, `vertex` bei der Parabel), `P`;
Scheitel `V1`, `V2` (Quadrantenpunkte); Schienen entlang der Achse und der Leitlinie.

## Bibliothek

56 Einträge: `math.axes.cartesian`, `math.axes.cartesian.quadrant` (erster Quadrant 0…6 x 0…5), `math.axes.number`,
`math.axes.spatial`, `math.axes.spatial.isometric`, `math.axes.polar`; Graphen `math.graph.function` (Parabel
y = x²), `math.graph.function.linear` (y = 0,5x + 1), `math.graph.function.sine` (−2π…2π, π-Teilung, 90x45),
`math.graph.function.exponential`; `math.point.labelled`, `math.vector.arrow`, `math.vector.arrow.position` (Ortsvektor
von O nach P mit Punkten), `math.vector.arrow.components`, `math.vector.sum` (Parallelogramm), `math.vector.sum.tail`
(aneinandergehängt); Winkel `math.angle.mark`, `math.angle.mark.right` (90°, Quadrat), `math.angle.mark.value`
(„α = 45°“), `math.angle.mark.arc` (nur Bogen, zum Markieren einer vorhandenen Figur); Figuren `math.shape.triangle`,
`math.shape.triangle.right` (mit Winkelmarkierungen), `math.shape.triangle.incircle`,
`math.shape.triangle.circumcircle`, `math.shape.polygon` (Sechseck), `math.circle.basic`, `math.circle.tangent`,
`math.circle.thales`, `math.circle.unit`, `math.arc.sector`, `math.arc.sector.segment` (110°), `math.shape.pythagoras`;
Konstruktionen `math.construct.perpendicular`, `math.construct.anglebisector`, `math.lines.parallel`; Körper
`math.solid.cube`, `math.solid.cuboid` (56x36), `math.solid.cylinder`, `math.solid.cone`, `math.solid.sphere`,
`math.solid.pyramid`; Mengen und Wahrscheinlichkeit `math.venn.diagram`, `math.venn.diagram.three`,
`math.tree.probability`, `math.tree.probability.dice` (einstufig, Ergebnisse 1, 2, 3 mit 1/6, 1/3, 1/2); Runde 7:
`math.shape.triangle.values`, `math.free.triangle`, `math.free.triangle.circles`, `math.free.triangle.incircle`,
`math.free.polygon`, `math.free.bisector`, `math.free.anglebisector`, `math.free.tangents`, `math.conic.section`
(Ellipse), `math.conic.section.hyperbola`, `math.conic.section.parabola` (50x44).

Kategorien: `math.axes` Coordinate systems / Koordinatensysteme, `math.graphs` Function graphs / Funktionsgraphen,
`math.vectors` Points & vectors / Punkte & Vektoren, `math.angles` Angles / Winkel, `math.figures` Figures & circles /
Figuren & Kreise, `math.constructions` Constructions / Konstruktionen, `math.solids` Solids / Körper,
`math.stochastics` Sets & probability / Mengen & Wahrscheinlichkeit.

Suchbegriffe decken beide Sprachen ab, z. B. Koordinatensystem, Zahlenstrahl, Schrägbild, Parabel, Sinus, Ortsvektor,
Kräfteparallelogramm, rechter Winkel, Dreieck, Inkreis, Umkreis, Höhe, Schwerpunkt, Thaleskreis, Einheitskreis,
Tangente, Sechseck, Mittelsenkrechte, Winkelhalbierende, Stufenwinkel, Wechselwinkel, Würfel, Quader, Zylinder,
Kegel, Kugel, Pyramide, Kreisausschnitt, Kreisabschnitt, Satz des Pythagoras, Mengendiagramm, Baumdiagramm,
Sinussatz, Kosinussatz, Dreiecksberechnung, Tangenten, Kegelschnitt, Ellipse, Hyperbel, Brennpunkt, Leitlinie.

## Anker

- **Anschlüsse** (`AnchorRole.Port`, stabile Ids; ein angedocktes Linienende folgt ihnen bei Verschieben, freiem
  Skalieren und Parameteränderungen über `Attachments.Refresh`): `origin` der 2D- und 3D-Achsen, `pole` des
  Polarnetzes, `zero` des Zahlenstrahls, `pt` eines Punkts, `A`/`B`/`C` und `centre` (Mittelpunkt der Konstruktion)
  eines Dreiecks, `v0…` und `centre` eines Vielecks, `centre` eines Kreises, `centre`/`contact` der Tangente,
  `A`/`B`/`C`/`centre` bei Thales, `origin`/`P` des Einheitskreises, `A`/`B`/`M`/`S1`/`S2` der Mittelsenkrechten,
  `vertex`/`P1`/`P2`/`Q` der Winkelhalbierenden, `s1`/`s2` (Schnittpunkte) der Parallelen, `sum` (Spitze der
  Resultierenden) der Vektoraddition und `vertex` des Winkels. Funktionsgraphen ergänzen ihre markanten Punkte:
  `zero0…` (Nullstellen), `ext0…` (Extrema) und `yint` (y-Achsenabschnitt).
- **Gitterpunkte** (`AnchorRole.Mid`): jeder Teilstrich-Kreuzungspunkt eines 2D-Koordinatensystems (`g<i>_<j>` = der
  Punkt (i·xStep | j·yStep), z. B. `g3_-2`; bis 900 Punkte), jeder Teilstrich des Zahlenstrahls (`t<i>`), die
  Einheitsstriche der 3D-Achsen (`x_1`, `y_2` … bzw. `x1_1` …), Ring/Strahl-Kreuzungen des Polarnetzes
  (`r<Ring>a<Grad>`), Seitenmitten und Kantenmitten von Vielecken, Quadrantenpunkte von Kreisen
  (`AnchorRole.Quadrant`). Ein auf ein Koordinatensystem gezeichneter Vektor dockt mit seinen Enden an Gitterpunkten an
  und bleibt dort, wenn das Koordinatensystem verschoben oder gestreckt wird (getestet).
- **Schienen** (`AnchorRole.Rail`): die Achsen (`xaxis`, `yaxis`, `axis-x` …), der Zahlenstrahl, die Parallelen `g`,
  `h` und die Querlinie – Linienenden docken überall entlang an.
- Punkttypen (Vektor, Vektoraddition, Winkel) docken auf Flächen an (`DocksOnSurface`): Ein auf eine Figur gezogenes
  Ende folgt ihr.

## Kontextaktionen

Koordinatensysteme: Gitternetz, Achsenzahlen, x-Bereich… und y-Bereich… (akzeptiert `-5;5`, `−2,5..7,5`, `0 10`,
`0 bis 10`; ein Komma ist ein Dezimalkomma). Zahlenstrahl: Zahlen, Bereich…, feinere/gröbere Unterteilung. 3D-Achsen:
Projektion ▾, Achsennamen ▾, negative Halbachsen. Polarnetz: mehr/weniger Ringe, Winkelschritt ▾,
Winkeleinheit ▾. Funktionsgraph: Funktion ▾, Koeffizienten a; b; c; d… (z. B. `2; 1; 1; -3`),
Funktionsgleichung anzeigen, dazu die Achsenaktionen. Punkt: Beschriftung…, Beschriftungslage ▾,
Markierung ▾. Vektor: Name…, Schreibweise ▾, Komponenten, Punkte markieren, Richtung umkehren. Vektoraddition:
Parallelogramm / Aneinanderhängen (a, b und die Resultierende bleiben: der dritte Punkt wandert), Resultierende,
Schreibweise ▾. Winkel: Winkel festlegen… (dreht den zweiten Schenkel um den Scheitel; Werte über 180° ergeben
den überstumpfen Winkel), Wert anzeigen, Markierung ▾, Schenkel zeichnen, andere Seite, Name…. Dreieck:
Art ▾, Konstruktion ▾, Ecken/Seiten beschriften, Winkel markieren. Vieleck: mehr/weniger Ecken, Umkreis, Inkreis,
Eckpunkte beschriften. Kreis: gezeigte Strecke ▾ (Radius/Durchmesser), Mittelpunkt, Beschriftung. Tangente: Berührpunkt bei Winkel…,
Berührpunkt verschieben (+30°), Radius und rechter Winkel. Thales: Lage von C…, C auf dem Kreis verschieben,
Vollkreis. Einheitskreis: Winkel…, um 15° drehen, Tangensstrecke, Werte anzeigen. Konstruktionen: Zirkelbögen,
Beschriftung, Winkel…; Parallelen: Winkelpaar ▾, Winkel…, Werte anzeigen.
Runde 7: Dreieck durch drei Punkte: Konstruktion ▾, Ecken-/Seitenbeschriftung, Winkelmarkierungen, gemessene
Winkel; Vieleck: Flächeninhalt, Schwerpunkt, Eckenbeschriftung; Mittelsenkrechte, Winkelhalbierende und Tangenten:
Zirkelbögen / gemessene halbe Winkel / Thaleskreis / Radien, Beschriftung; Kegelschnitt: Kurve ▾, Lage von P…, Punkt P,
definierende Eigenschaft, Achsen, Beschriftung. Die Dreiecksart ▾ bietet „Maßstäblich nach den Werten“.

## Griffe (Ziehgriffe)

`DragHandles` / `MoveHandle` (Hosts zeichnen sie als blaue Griffe; interaction-ux.md Abschnitt 4). Winkel sind
mathematische Grad (gegen den Uhrzeigersinn ab +x); jeder Winkelgriff bewegt sich frei und dreht seinen Punkt um den
Mittelpunkt zum Zeiger hin, gerastet auf 1° und innerhalb einer halben Drehung vom alten Wert:

| Figur | Schlüssel | Griff an | Parameter | Bereich |
|---|---|---|---|---|
| Kreis mit Radius | `angle` | Ende des Radius (des Durchmessers, wenn nur er gezeigt wird; keiner bei „Nur Kreis“) | `angle` | −360° … 360° |
| Tangente an einen Kreis | `angle` | Berührpunkt | `angle` | −360° … 360° |
| Thaleskreis | `angle` | C | `angle` | 1° … 179° |
| Einheitskreis | `angle` | P | `angle` | −360° … 360° |
| Winkelhalbierende | `angle` | Ende des zweiten Schenkels | `angle` | 10° … 170° |
| Parallelen mit Querlinie | `angle` | oberes Ende der Querlinie | `angle` | 20° … 160° |
| Winkel (Punktobjekt) | `radius` | der Bogen bei 0,3 des Bogens oder die äußere Ecke des Rechter-Winkel-Quadrats; bewegt sich längs dieses Strahls | `radius` | 0,5-mm-Schritte, 1 … 100 mm |

Griffe von Box-Figuren sitzen dort, wo die Figur den Punkt zeichnet, auch wenn extreme Boxgrößen die Figur verkleinern
(`Fig.Fit`). Die Beschriftungen nennen den Wert („Berührpunkt: 60°“ / „Point of contact: 60°“, „Bogenradius: 7 mm“ /
„Arc radius: 7 mm“). Größen des regelmäßigen Vielecks, der Kreise und Achsen sind die Boxgröße (freies Skalieren),
keine Parameter, daher ohne Griff.

## Host-Hinweise und Grenzen

- Die gesamte Ausgabe besteht aus nativen Grundformen (Linien mit Pfeilspitzen, Linienzüge, geschlossene Polygone,
  Ellipsen, Text); Bögen sind Linienzüge (`Prim.Arc`), daher zeichnet jeder Host alles nativ – kein Vektorbild nötig.
- Text verwendet Unicode-Zeichen, die die Standardschriften von Office und LibreOffice enthalten (α β γ φ π √ ² ³ − ·,
  tiefgestellte ₁ ₂ ₃). Exponenten von eˣ verwenden hochgestellte Zeichen einschließlich des Modifikatorbuchstabens ˣ
  (U+02E3) und ⁽ ⁾; Schriften ohne diese Zeichen greifen auf die Schriftverknüpfung des Hosts zurück. Vektorpfeile über
  Buchstaben sind eigene kurze Pfeillinien und Indizes (v<sub>x</sub>) eigene kleinere Textfelder, beide anhand einer
  geschätzten Zeichenbreite positioniert; bei sehr breiten oder schmalen Schriften können sie leicht versetzt sitzen.
- Beschriftungen gedrehter Box-Elemente drehen sich mit (keine Aufrecht-Korrektur).
- Der Funktionsgraph hat bewusst keinen freien Formelparser; Messdaten zeichnet `chart.xy` des Diagrammmoduls.
- Das mit `mark = right` gezeichnete Rechter-Winkel-Quadrat folgt den tatsächlichen Schenkeln: Bei einem Winkel, der
  nicht 90° beträgt, wird es zur Raute. `auto` (Standard) zeichnet das Quadrat nur bei 90° (±0,05°).
- Sehr kleine Boxen: Passen Beschriftungen und Zeichnung nicht hinein, schrumpft die ganze Figur (einschließlich
  Schriftgrößen und Anker) gleichmäßig in die Box.

## Tests und Galerie

`tests/InventaSketch.Modules.Geometry.Tests` (207 Tests): Registrierung, Id-Konventionen, zweisprachige Namen,
Parameter, Optionen und Aktionen; Einfügen ohne Parameter; Bibliothekseinträge verweisen auf gültige Parameter; jeder
Bibliothekseintrag in seiner Box in beiden Sprachen, gestreckt, gestaucht und mit jeder Parametervariante; jeder
Parameter ändert die Zeichnung; freies Skalieren ändert `Size` und behält Parameter; Anker in der Box mit eindeutigen
Ids; Anschlüsse an den markanten Punkten; Stil- und Füllungsüberschreibung; jede Kontextaktion läuft;
Zahlenformatierung, Bereichseingabe, π-Teilung und Funktionsgleichungen; Gitterpunkt-Anker, erster Quadrant, dichte
Teilung ohne überlappende Zahlen, Abschneiden der Parabel am Bereich, Anker für Nullstellen/Extrema/y-Achsenabschnitt,
die zwei Äste der Hyperbel, Sinus mit π-Teilung, ln ab der Unterkante; Koeffizienten- und Bereichseingabe; 3D-Achsen
behalten ihre Winkel beim Strecken; Polarbeschriftung in Grad und Bogenmaß; Vektorkomponenten und Schreibweise,
Ortsvektor, Vektoraddition behält die Resultierende beim Konstruktionswechsel; gemessene Winkelwerte, automatisches
Rechter-Winkel-Quadrat, Winkel festlegen; rechtwinkliges und gleichschenkliges Dreieck beim Strecken, gleichseitiges
Dreieck, Inkreis berührt alle Seiten, Umkreis durch die Ecken, Höhenschnittpunkt auf allen Höhen (auch stumpf),
regelmäßige Vielecke bleiben regelmäßig, Sinus-/Kosinuslängen am Einheitskreis, rechter Winkel bei Thales, Tangente
senkrecht zum Radius, runde Kreise, Geometrie von Mittelsenkrechter und Winkelhalbierender, Winkelmarken an
Parallelen; eine zusammengesetzte Folie, auf der eine an Gitterpunkten angedockte Vektoraddition und ein Winkel dem
Koordinatensystem beim Verschieben und Strecken folgen. `NewFigureTests`: sichtbare und verdeckte Kanten von Würfel
und Pyramide, der Tiefenfaktor unter 45° beider Projektionen, Mantellinien des Zylinders, Kegelmantel tangential an
den Grundkreis, runde Kugel, Umriss von Sektor/Segment, Winkelbeschriftung und Griff, Pythagoras-Flächen (a² + b² = c²)
und Einheitsquadrate, Venn-Anordnung und Eingabe der Mengennamen, Gegenwahrscheinlichkeiten, Pfadprodukte, Brüche,
Prozente und Anordnung im Baumdiagramm, Lesen von Wahrscheinlichkeiten und der JSON-Rundlauf.
`ValueAndLiveFigureTests` (Runde 7): Dreieckswerte aus SSS, SWS, WSW und SsW (Sinus- und Kosinussatz, Fläche, Umfang),
Konflikte, das Dreieck maßstäblich nach den Werten (Form, Beschriftung, nur Winkel, ungültige Werte), das Dreieck durch
drei Punkte mit Inkreis und gemessenen Winkeln, eine an einem Dreieck angedockte Mittelsenkrechte folgt ihm, die
Winkelhalbierende, Tangenten mit rechten Winkeln (und keine von innen), Fläche, Umfang, Maßstab und Schwerpunkt des
Vielecks, Brennpunkte und definierende Eigenschaften von Ellipse, Hyperbel und Parabel.

Der Galerietest schreibt `gallery/*.html` in den Testausgabeordner (eine Seite je Kategorie: jeder Bibliothekseintrag
auf Englisch und Deutsch, gestreckt und gestaucht, dazu die Auswahlwerte der wichtigsten Parameter) und
`gallery/index.html` mit einer zusammengesetzten Unterrichtsfolie (Koordinatensystem, angedockte Vektoraddition,
beschrifteter Punkt, Winkel φ) in beiden Sprachen.
