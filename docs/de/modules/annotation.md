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
| `ann.mechanics` Technische Mechanik | Kraft, Moment, Koordinatensystem, Schwerpunkt, Boden, Lager, Streckenlast, Balken, Feder, Dämpfer, Masse, **Querkraft- und Momentenverlauf** |
| `ann.flow` Ablaufdiagramm | Ablaufdiagramm-Symbole und Verbinder (siehe [diagrams.md](diagrams.md)) |

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

Ein Kasten mit zwei Bändern: oben die Querkraft (Formelzeichen Q in deutschen, V in englischen Zeichnungen), unten das
Biegemoment, jeweils quer schraffiert, mit dem betragsgrößten Wert beschriftet („max Q = F/2“, „max M = F·L/4“). Die
Kastenbreite steht für die Balkenlänge.

**Einen gezeichneten Balken lesen.** Unter einem `ann.beam` platziert (dem nächsten Balken über dem Verlauf, dessen
Breite die Mitte des Verlaufs überdeckt, höchstens 40 mm oder 1,5 Verlaufshöhen darüber), liest der Verlauf diesen
Balken:

- Fest- und Loslager (`ann.support.pin`, `ann.support.roller`), deren Gelenkpunkt den Balken berührt (bis 3 mm);
- Einspannungen (`ann.support.fixed`), deren Wand das linke oder rechte Balkenende berührt;
- Kräfte (`ann.force`), deren Spitze den Balken berührt; schräge Kräfte zählen mit ihrer Komponente quer zum Balken,
  Kräfte, die von der Balkenunterseite weg zeigen, negativ, Kräfte längs des Balkens gar nicht;
- Streckenlasten (`ann.load.distributed`), deren Unterkante auf dem Balken steht (gleichmäßig über ihre Breite).

Lagen sind Anteile der Balkenlänge; Anteile, die höchstens 0,5 % von einem einfachen Bruch (Halbe bis Zwanzigstel)
abweichen, gelten als dieser Bruch, Lagen bis 3 mm vor einem Ende als das Ende.

**Werte.** Eine Lastbeschriftung wie „10 kN“, „F = 10 kN“ oder „q = 2,5 kN/m“ ist eine Zahl mit Einheit; der optionale
Parameter `value` einer Kraft oder Streckenlast („Betrag (Schnittgrößenverläufe)“) hat Vorrang vor der Beschriftung.
Jede andere Beschriftung („F“, „q“, „F₁“) ist ein Formelzeichen. Der optionale Balkenparameter `length` (m) gibt die
wirkliche Länge.

| Lasten und Länge | Beschriftung |
|---|---|
| alle Lasten Zahlen, Länge angegeben | Zahlen mit Einheiten: „max Q = 5 kN“, „max M = 10 kN·m“ |
| Einzellasten als Zahlen, keine Länge | „max Q = 5 kN“, „max M = 2.5 kN·L“ |
| ein Formelzeichen einer Art (alle Einzellasten „F“ oder alle Streckenlasten „q“) | exakte Vielfache: „F/2“, „F·L/4“, „q·L/2“, „q·L²/8“, „−F·L“; kurze Brüche bis 1/64, sonst drei Nachkommastellen |
| gemischte Formelzeichen, Formelzeichen mit Zahlen, Streckenlasten als Zahlen ohne Länge | „max Q“, „max M“ ohne Wert (die Verläufe mit Einheitswerten gezeichnet) |

**Ohne Balken** (oder über einem Balken ohne Lager und Lasten) zeigt der Verlauf seinen eigenen Lastfall: `support`
(Einfeldträger (Vorgabe) / Kragträger mit Wand links / Wand rechts), `load` (Einzellast F (Vorgabe) / Streckenlast q /
beides), `at` (Lage von F als Anteil, 0,5) und optional `force` (kN), `lineLoad` (kN/m) und `length` (m) für Werte.
Vorlagen: `ann.beam.diagram.cantilever` (Endlast) und `ann.beam.diagram.uniform`.

**Weitere Parameter:** `momentBelow` (true: positive, durchbiegende Momente unter der Achse, auf der Zugseite, wie in
deutschen Lehrbüchern üblich; false: darüber), `textSize` (3,5).

**Statik** (`BeamStatics`, reine Berechnung ohne Host): statisch bestimmte gerade Balken unter Querlasten – zwei
gelenkige Lager (Fest- und Loslager; Kragarme erlaubt; zwei Festlager werden akzeptiert, weil nur Querlasten betrachtet
werden) oder eine Einspannung. Lagerkräfte aus den Gleichgewichtsbedingungen, Querkraft als Summe der aufwärts
gerichteten Kräfte links vom Schnitt, Biegemoment positiv, wenn es den Balken durchbiegt; das Extremum des Moments in
einem belasteten Abschnitt liegt dort, wo die Querkraft null wird. Andere Lagerungen (keine, ein Gelenk, drei Lager,
Einspannung plus Loslager) sind nicht statisch bestimmt: Der Verlauf zeigt dann seine Achsen und den Hinweis „nicht
statisch bestimmt“. Unit-Tests decken die Lehrbuchfälle ab (Einfeldträger mit Mittellast: Lagerkräfte F/2,
M_max = F·L/4; Gleichlast: M_max = q·L²/8; Kragträger mit Endlast: M = −F·L an der Einspannung; außermittige Last
F·a·b/L; Kragarm; Gleichgewichtsproben).

Neuzeichnen: Der Verlauf hängt von seinen Nachbarn ab; Balken, Lager, Kräfte und Streckenlasten nennen die Verläufe auf
ihrer Seite als abhängige Objekte, sodass das Verschieben einer Last den Verlauf neu zeichnet.

## Weitere Ergänzungen

| Typ-Id | Element | Parameter (alle optional) |
|---|---|---|
| `ann.cloud` | Änderungswolke: eine geschlossene Kette nach außen gewölbter Bögen im Kasten, die einen geänderten Bereich markiert | `arc` (Bogenlänge, 5 mm) |
| `ann.dimension.chain` | Kettenmaß: mehrere Punkte nacheinander auf einer Maßlinie bemaßt, ein Wert je Abschnitt; die Punkte werden in ihrer Reihenfolge längs der Messrichtung genommen | `offset` (−8 mm), `axis` (automatisch / waagerecht / senkrecht), `terminator`, `prefix`, `unit`, `decimals`, `scale`, `textSize` (kein gemeinsamer Ersatztext) |
| `ann.beam` | erhält eine optionale Länge `length` (m) für die Werte der Verläufe sowie Begleiter (Lager, Kraft, Streckenlast, Verlauf) | |
| `ann.force` / `ann.load.distributed` | erhalten einen optionalen Betrag `value` (Kraft / Last je Länge) für die Verläufe | |

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
- Schnittgrößenverläufe: nur Einzel- und Gleichlasten quer zum Balken; keine Einzelmomente (`ann.moment`), keine
  Trapezlasten, keine Gelenke im Balken, keine Normalkräfte; der Verlauf passt sich nicht automatisch der Balkenbreite an.
