# Diagramm-Modul (`diagrams`, Typ-IDs `diag.*`)

> English: [../../en/modules/diagrams.md](../../en/modules/diagrams.md)

UML-, Entity-Relationship- (ER), System-/Architektur- und BPMN-light-Diagramme für schnelle technische Skizzen. Jedes
Symbol lässt sich ohne Parameter einfügen; freies Skalieren ändert nur die Boxgröße. Einfache Ablaufdiagramm-Starter
(Verarbeitungsschritt, Verzweigung, Start/Ende, Winkelverbinder) bleiben im Beschriftungsmodul (`ann.flow.*`,
`ann.arrow.elbow`) und werden hier nicht dupliziert.

## Konzepte

- **Box-Symbole** werden in lokalen Koordinaten gezeichnet und bieten **Port-Anker** an den Kantenmitten und
  Viertelpunkten (12 je Rechteck). Ellipsen bieten 8 Ports auf dem Umriss, Rauten ihre Ecken und Kantenmitten, Blöcke
  ihre Port-Quadrate, Aktivierungsbalken alle 2,5 mm je Seite. Die Linie einer Lebenslinie ist eine `Rail` (Nachrichtenenden docken an beliebiger Stelle an und bleiben angebunden) und eine `Axis`, die Aktivierungsbalken mittig ausrichtet (über deren `Hub`).
- **Verbinder** sind Punktobjekte (Beziehungen, Krähenfuß-Verbinder, Sequenznachrichten). Ihre Enden rasten an Ports
  ein und werden als Anbindung gespeichert, folgen also verschobenen, gedrehten und gestreckten Boxen. Mit zwei Punkten
  ist ein Verbinder gerade oder mit `route = orthogonal` rechtwinklig geführt (an eine Boxkante gebundene Enden
  verlassen sie senkrecht). Bei mehr Punkten sind die Punkte Stützpunkte.
- **Mehrzeilige Inhalte** (Attribute, Operationen, Literale, Bahnen, Aktivitäten) stehen in Textparametern, eine Zeile
  pro Eintrag. Jede Zeile wird als eigenes Textprimitiv gezeichnet.
- **Textanpassung:** Text nutzt den Parameter `textSize` (Standard 3,5 mm ≈ 10 pt). Passt der Inhalt nicht in die Box,
  wird der Text automatisch verkleinert (nicht unter 1,8 mm); der Parameter selbst ändert sich nie.
- **Endnotationen** sind Core-Pfeilspitzen im Stil der Verbinderlinie (`ArrowStart`/`ArrowEnd`, Größe `large`):
  offener Pfeil (`open`), gefülltes Dreieck (`triangle`), hohles Dreieck (`triangleOpen`), gefüllte und hohle Raute
  (`diamond`, `diamondOpen`) sowie die Krähenfuß-Enden `crowOne`, `crowOneOnly`, `crowZeroOne`, `crowMany`,
  `crowOneMany`, `crowZeroMany`. Pfeilspitzen-Überschreibungen des Benutzers (Anfang, Ende, Größe) ersetzen sie daher,
  und jeder Host zeichnet sie: nativ, wo er ein passendes Linienende hat, sonst als explizite Formen (siehe
  [Fähigkeitsmatrix](../hosts/capability-matrix.md)). Hohle Spitzen sind ungefüllte Umrisse, die Linie endet an ihrer
  Basis; ein Krähenfuß-Kreis unterbricht die Linie. Beides sieht auf jedem Hintergrund und in Schwarzweiß-Ausgabe
  richtig aus. Eine Linienfarbe des Benutzers färbt Linien, Spitzen und Texte; Breiten- und Strichart-Überschreibungen
  gelten nur für Umriss bzw. Verbinderlinie (die Spitze wächst mit der Linienbreite).
- **Unterstreichungen** (statische Mitglieder, Schlüsselattribute) nutzen die native Schrift-Unterstreichung des
  Textes statt gezeichneter Linien und passen so in jedem Host zur tatsächlichen Textbreite.

## Objekttypen

| Kategorie | Typen |
|---|---|
| UML-Klassen & Struktur | `diag.uml.class`, `diag.uml.interface`, `diag.uml.enum` (Ansichten `full`/`compact`), `diag.uml.package`, `diag.uml.component`, `diag.uml.node`, `diag.uml.note` |
| UML-Anwendungsfall | `diag.uml.actor`, `diag.uml.usecase` |
| UML-Aktivität | `diag.activity.action`, `.initial`, `.final`, `.flowfinal`, `.decision`, `.bar` (Gabelung/Vereinigung), `.swimlane` |
| UML-Zustandsautomat | `diag.state.state` (entry/do/exit), `diag.state.composite` (Regionen); Start-/Endzustand sind Vorlagen der Aktivitätsknoten |
| UML-Sequenz | `diag.seq.lifeline` (Kasten- oder Akteur-Kopf, gestrichelte Linie, optionales Zerstörungs-X), `diag.seq.activation`, `diag.seq.message` (synchron, asynchron, Antwort, Erzeugen; Selbstaufruf, wenn beide Enden auf einer Senkrechten liegen) |
| UML-Beziehungen | `diag.uml.relation`: Assoziation, gerichtete Assoziation, Aggregation, Komposition, Generalisierung, Realisierung, Abhängigkeit, Verbindung; Multiplizitäten und Rollen an beiden Enden, Beschriftung in der Mitte |
| ER | `diag.er.entity` (Ansichten `diagram`/`table`/`chen`, schwache Entität), `diag.er.attribute` (Schlüssel, mehrwertig, abgeleitet), `diag.er.relationship` (Chen-Raute, identifizierend), `diag.er.connector` (Krähenfuß) |
| System & Architektur | `diag.sys.block` (Ports je Seite), `diag.sys.container` (Gruppenrahmen mit Titelreiter oder -band), `diag.sys.cloud`, `diag.sys.database`, `diag.sys.queue`, `diag.sys.server`, `diag.sys.client` (Browserfenster), `diag.sys.user`, `diag.sys.lollipop` (bereitgestellt Kugel / benötigt Buchse) |
| BPMN-light | `diag.bpmn.start`, `diag.bpmn.intermediate`, `diag.bpmn.end`, `diag.bpmn.task` (Teilprozess-Markierung), `diag.bpmn.gateway` (exklusiv, parallel, inklusiv, ereignisbasiert), `diag.bpmn.pool` (Pool mit Bahnen) |
| Regelkreise | `diag.ctrl.sum`, `diag.ctrl.tf`, `diag.ctrl.branch`, `diag.ctrl.mult`, `diag.ctrl.signal`, `diag.ctrl.loop` (zerlegbar) |
| Rechnersysteme & Kommunikation | Systemblock-Vorlagen nach Rolle, `diag.sys.bus`, `diag.sys.phone`, `.basestation`, `.router`, `.switch`, `.satellite`, `.iot`, `diag.sys.wireless` |
| Datenformate | `diag.data.fields` (Zeile, Zeilen fester Bitbreite, Speicherbelegung) |
| GUI-Wireframes | `diag.ui.screen`, `.button`, `.field`, `.list`, `.checkbox`, `.toggle`, `.slider`, `.image`, `.icon`, `.tabs`, `.touch`, `.cursor` |
| Fehlerbaumanalyse (FTA) | `diag.fta.event.top`, `.intermediate` (Rechtecke), `diag.fta.gate.and`, `.or`, `.vote` (k aus n), `.inhibit`, `diag.fta.event.basic` (Kreis), `.undeveloped` (Raute), `.house`, `.conditioning` (Ellipse), `diag.fta.transfer` (Dreieck), `diag.fta.link` |
| Ablaufsprache (SFC, GRAFCET) | `diag.sfc.step.initial` (Doppelrahmen), `diag.sfc.step`, `diag.sfc.transition`, `diag.sfc.action` (optionales Feld für das Bestimmungszeichen), `diag.sfc.branch` (parallel Doppellinie / alternativ Einzellinie), `diag.sfc.link` |
| Ishikawa & Mindmap | `diag.ishikawa` (Fischgräte), `diag.mindmap.node` (abgerundet, Ellipse, Rechteck, unterstrichen), `diag.mindmap.link` (geschwungener Zweig) |

## Textkonventionen

- **Klassenmitglieder:** ein Mitglied pro Zeile, Sichtbarkeit als Teil der Zeile (`+ - # ~`). Ein führendes `_`
  kennzeichnet ein statisches Mitglied, das ohne Unterstrich-Zeichen unterstrichen dargestellt wird.
- **Entitätsattribute:** `*` Primärschlüssel (unterstrichen: in der Diagrammansicht die ganze Zeile, in der
  Tabellenansicht die Namenszelle, in der Chen-Ansicht der Text der Attributellipse), `#` Fremdschlüssel (`(FK)` in der Diagrammansicht,
  Spalte `FK` in der Tabellenansicht); kombinierbar (`*#order_id`). Ein Typ folgt nach Doppelpunkt: `id: int`.
- **Stereotypen** werden ohne Guillemets eingegeben; sie werden automatisch ergänzt (`interface` → `«interface»`).
- **Beziehungsrichtung:** die Markierung sitzt am Endpunkt, also vom Teil/Kind/Client zum Ganzen/Elternteil/Anbieter
  zeichnen.

## Automatisches Layout (25.11.2026)

Die verbundenen Elemente eines Diagramms auswählen (die Verbinder müssen nicht mit ausgewählt sein): Die Mehrfachauswahl
bietet „Als Diagramm anordnen, von oben nach unten“ und „… von links nach rechts“ (Engine `arrange` mit `layout:down` /
`layout:right`, Core `DiagramLayout`). Ein Ebenen-Layout nach Art von Sugiyama: Jede Linie, deren erstes und letztes
angedocktes Ende auf zwei ausgewählten Elementen sitzt, ist eine Kante; Zyklen werden entlang einer Tiefensuche
aufgebrochen, die Ebenen folgen dem längsten Pfad von den Quellen, Baryzentrum-Durchläufe verringern Kreuzungen, Ebenen
und Abstände richten sich nach der Größe der Elemente. Das Diagramm bleibt an seinem Platz (Oberkante und Mitte); die
Verbinder folgen ihren Elementen und docken an den Seiten an, die ein gezeichnetes Flussdiagramm verwendet (25.11.2026):
unten → oben für einen Schritt direkt darunter, seitlich hinaus und oben hinein für einen Zweig darunter und daneben,
beide Enden rechts um die Spalte herum, wenn andere Elemente dazwischen stehen (ein „ja“-Ausgang zu einem späteren
Schritt), beide Enden links für einen Rücksprung zu einem früheren Schritt; solche seitlichen Läufe halten Abstand zu
allen Elementen daneben (der Parameter `clearance` des Winkelverbinders wird gesetzt). Ein Ende an einem Port wechselt
zum nächsten Port dieser Seite. Eine lange Kette von links nach rechts kann über den Seitenrand
hinauslaufen. Funktioniert für die Kastenelemente aller Module, die über angedockte Linien
verbunden sind (Prozessschritte, Flussdiagramm, UML, BPMN, ER, Blöcke).

## Verfahrens-Flussdiagramme (Modul Beschriftung, 25.11.2026)

Die Flussdiagrammsymbole des Beschriftungsmoduls (`ann.flow.*`) passen ihren Text ein: Wörter werden innerhalb des
Symbols umbrochen (bei Raute und Kreis in der eingeschriebenen Fläche), die Schrift wird kleiner, bis er passt, und erst
unter 60 % der Größe werden lange Wörter mit Bindestrich getrennt. „Start (Kreis)“ (`ann.flow.start`) und „Ende
(Doppelkreis)“ (`ann.flow.end`) ergänzen den Satz. Der Winkelverbinder führt nach den Seiten, an denen seine Enden
andocken: Ausgänge auf derselben Seite laufen mit Abstand herum (C-Form), gegenüberliegende Seiten treffen sich in der
Mitte, rechtwinklige Seiten nehmen eine Ecke, wenn sie vor beiden Enden liegt. Er trägt eine Beschriftung am Anfang; die
Bibliothek hat „Verbinder „ja““ und „Verbinder „nein““ (`ann.arrow.elbow.yes` / `.no`). Der Rahmen von Beschriftungen und
Notizen übernimmt einen gestrichelten oder umgefärbten Stil (gestrichelte Notizkästen). Geprüft am Flussdiagramm des
Maintainers (Engine-Test `TheMaintainersFlowchartIsLaidOutWithoutLinesThroughBoxes`: nach dem Layout läuft kein
Verbinder durch einen Kasten). Offen: hindernisbewusste Führung bei von Hand gesetzten Kästen (nur das Layout berechnet
Abstände).

Weitere Symbole (Konventionen der ISO 5807, eigenständig gezeichnet): vordefinierter Prozess / Unterprogramm
(`ann.flow.predefined`, zwei innere Seitenlinien; der Text passt dazwischen), Seitenverbinder (`ann.flow.offpage`,
Fünfeck mit Spitze nach unten, gleichmäßig skaliert, Buchstabe „A“), Schleifenbegrenzung (`ann.flow.loop`, oben
abgeschrägte Ecken; `ann.flow.loop.end` oder Parameter `edge = end`: unten abgeschrägt), Vorbereitung
(`ann.flow.preparation`, Sechseck), manuelle Verarbeitung (`ann.flow.manual`, Trapez), Datenspeicher
(`ann.flow.database`, Zylinder) und Verzögerung (`ann.flow.delay`, „D“-Form). Jedes Symbol passt seinen Text in seine
eigene Innenfläche ein (unter dem Zylinderdeckel, über der Fünfeckspitze, links vom Bogen der Verzögerung) und hat die
vier Kantenanschlüsse. Jedes `ann.flow.*`-Symbol hat den Parameter `optional`: ein optionaler Schritt wird gestrichelt
umrandet (Aktion „Optionaler Schritt (gestrichelt)“, Bibliothekseintrag `ann.flow.process.optional`).

Parallelbetrieb (`ann.flow.parallel`, Aufteilung / Zusammenführung): ein gefüllter Balken (`style = bar`) oder die
zwei parallelen Linien der ISO 5807 (`style = doubleline`); 2–6 Anschlüsse (`ports`, Vorgabe 3) gleichmäßig verteilt
auf jeder Längsseite; breiter als hoch liegt er waagerecht (Anschlüsse oben und unten), sonst steht er (Anschlüsse links
und rechts). Er wird mit dem Winkelverbinder verkettet.

Ausgänge einer Verzweigung: Fügt „+“ (Schnellhinzufügen) Verbinder an eine Verzweigung an, bekommt der erste die
Ausgangsbeschriftung „ja“, der zweite „nein“. Die Beschriftung wird sprachneutral gespeichert (`exit = yes|no` am
Verbinder) und in der Sprache der Zeichnung geschrieben („yes“/„no“ auf Englisch); ein bearbeiteter Verbindertext geht
vor. Der Parameter `exitLabels` („a|b|c“) gibt andere Beschriftungen in der Reihenfolge des Hinzufügens vor; die Vorlage
„Verzweigung (Vergleich: < = >)“ (`ann.flow.decision.compare`) beschriftet drei Ausgänge mit „<“, „=“, „>“. Die
Bibliothekseinträge „Verbinder „ja““ / „Verbinder „nein““ tragen denselben sprachneutralen Ausgang. Core-Erweiterung:
`ObjectType.ChainConnectorParams(o, index)` liefert Parameter für den index-ten Verbinder, der ein Element verlässt;
`Flow.QuickAdd` zählt die schon abgehenden Verbinder und setzt sie.

## Verbinder und Zeichen für technische Abbildungen (25.11.2026)

Ergänzungen des Beschriftungsmoduls für technische Abbildungen; alle sind ohne Parameter einfügbar, schwarz-weiß und mit
nativen Linien, Freihandformen und Texten gezeichnet.

- **Brücken und Verbindungspunkte.** Der Winkelverbinder und Verbinder-Polylinien (offene Polylinie mit Pfeilspitze oder
  angedocktem Ende) zeichnen eine Brücke, wo sie einen früher gezeichneten Verbinder kreuzen (Parameter `hops`,
  Vorgabe an; Aktion „Brücken an Kreuzungen an/aus“; Brückengeometrie aus Core `Crossings`). Nur Verbinder des
  Beschriftungsmoduls wirken mit: einfache Polylinien (Zeichnungen, importierte Vektorgrafik) überbrücken weder noch
  werden sie überbrückt, Leitungen anderer Module sind nicht betroffen. Ihre gezeichneten Abschnitte sind Schienen: ein
  Verbinderende, das auf dem Abschnitt eines anderen Verbinders andockt, folgt ihm und bekommt automatisch einen
  Verbindungspunkt. „Knotenpunkt (Linien)“ (`ann.junction`) ist ein einzelner Punkt, dessen Mittelanschluss auf einer
  Schiene andockt.
- **Blockpfeil** (`ann.arrow.wide`): hohler Blockpfeil entlang einer geraden (`route = straight`) oder orthogonalen
  Führung als ein geschlossener Umriss; Schaftbreite `width` 6 mm, Spitze 2 × Breite breit und 1,2 × Breite lang,
  Spitzen am Anfang und/oder Ende, Füllung keine, Schraffur oder Punkte, optionaler Text entlang des Schafts. Vorlagen:
  beidseitig (`.double`), mit Knick (`.elbow`), Kehre (`.uturn`).
- **Doppellinienpfeil ⇒** (`ann.arrow.doubleline`): zwei parallele Striche (`gap` 2 mm) mit offener Winkelspitze; mit
  zwei Punkten führt und dockt er wie der Winkelverbinder (auch in C-Form um Kästen), mit mehr Punkten läuft er
  orthogonal durch sie.
- **Pfeilpaar** (`ann.arrow.pair`): zwei parallele Pfeile in Gegenrichtung (`gap` 3 mm, Anfrage / Antwort) mit je
  einer Beschriftung (`labelForward`, `labelBack`) auf ihrer Außenseite; Aktion „Richtungen tauschen“.
- **Busmarkierung** an `ann.line`, `ann.arrow`, `ann.arrow.elbow` und `ann.polyline`: `busMark = slash` zeichnet einen
  Schrägstrich mit der Anzahl der Leitungen (`busCount`, z. B. „8“ oder „n“) daneben, `busMark = strokes` einen
  Schrägstrich je Leitung (Ideen von IEC 60617 S00002 / S00003, eigenständig gezeichnet), in der Mitte des längsten
  Abschnitts. Vorlage „Sammelleitung mit Schrägstrich (n Leitungen)“ (`ann.line.bus`).
- **Tabelle** (`ann.table`): Zellen als Text (`cells`: Zeilen per Zeilenumbruch oder geschriebenem „\n“, Spalten per „|“
  oder Tabulator), fette Kopfzeile, Linien alle / außen / waagerecht / keine, Spaltenbreiten nach Inhalt, eine
  Schriftgröße für die ganze Tabelle; Aktionen fügen Zeilen oder Spalten an bzw. entfernen sie. Ohne Zellen zeigt sie
  eine kleine zweispaltige Tabelle in der Sprache der Zeichnung.
- **Ansichtspfeil** (`ann.viewarrow`): Pfeil mit fettem Großbuchstaben; `mode = touching` setzt den Buchstaben neben
  den Schaft (die Spitze berührt den Umriss), `freestanding` hinter das Pfeilende (`ann.viewarrow.free`).
- **Einzelheit** (`ann.detail.view`): Kreis- oder Rechteckrahmen für eine vergrößerte Einzelheit, beschriftet mit dem
  Buchstaben ihres Einzelheit-Kreises (`ann.detail.circle`; die n-te Einzelheit ohne eigenen Buchstaben nimmt den des
  n-ten Kreises) und optional mit grafischem Maßstab (`scaleBar`, `scale`, `barLength`: ein Balken der gezeichneten
  Länge, beschriftet mit der wahren Länge, z. B. „5 mm“), sodass keine geschriebene Maßstabsangabe wie „2:1“ nötig ist.
- **Werkstoffschraffuren** für `ann.hatch.*` (`pattern`): Flüssigkeit, Glas, Gummi / Kunststoff, Holz, Beton,
  Erdreich – einfache, eigenständige Muster, auf die Fläche beschnitten; Bibliothekseinträge `ann.hatch.rect.<pattern>`.
- **Wärmestrompfeil** (`ann.arrow.heat`): gewellter Schaft mit geradem Ende und gefüllter Spitze, Beschriftung „Q“
  kursiv.

Host-Hinweise: alles wird als native Linien, Freihandformen, gefüllte Kreise und Texte gezeichnet; der Blockpfeil ist
eine Freihandform (Schraffur-/Punktfüllung folgt der Musterfüllung des Hosts wie bei anderen schraffierten Flächen).
Brücken berechnet die Engine nur für Verbinder des Beschriftungsmoduls; Hosts zeigen an deren Kreuzungen kein
„Verbinden“-Symbol (das bleibt Leitungen und Rohren vorbehalten). Offen: Brücken zwischen Verbindern des
Beschriftungsmoduls und Leitungen anderer Module, Führung des Blockpfeils um Kästen (er folgt seinen Punkten), eine
einspaltige Tabelle kann keine leere Zeile bekommen.

## Regelkreise (25.11.2026)

Kategorie „Regelkreise“: Blockschaltbilder der Regelungstechnik nach den üblichen Lehrbuchkonventionen (eigenständig
gezeichnet, keine Norm-Abbildung übernommen).

- `diag.ctrl.sum` **Summierstelle**: Kreis Ø 6 mm in einer 12-mm-Box, Ports `left`, `top`, `right`, `bottom` auf dem
  Kreis. Parameter `signs`, ein Zeichen je Eingang in der Reihenfolge links, unten, oben, rechts (`+-` = links plus,
  Rückführung minus; Leerzeichen oder `.` = kein Vorzeichen). Form `plain`: das Vorzeichen steht neben der ankommenden
  Linie außerhalb des Kreises (über einem waagerechten, links neben einem senkrechten Eingang); `cross`: ein X teilt den
  Kreis, jedes Vorzeichen sitzt im Sektor seines Eingangs. Vorlagen mit Kreuz und mit drei Eingängen.
- `diag.ctrl.tf` **Übertragungsglied**: Inhalt `text` (Objekttext, sonst der übersetzte Name seiner Rolle `role`:
  Regler, Regelstrecke, Stellglied, Messglied, sonst `G(s)`), `fraction` (Zähler `numerator` über Nenner
  `denominator` mit Bruchstrich) oder `icon` (kleine Sprungantwort: P, PT1, PT2, I, D, Totzeit, PID; die statische
  Kennlinie einer Begrenzung; ein Integralzeichen für den Integrierer; alles als Pfade gezeichnet). Optionale
  Beschriftung `label` (z. B. `K, T`) unter dem Inhalt. Ports auf allen Seiten (oben: Störgröße, unten: Rückführung).
- `diag.ctrl.mult` Multiplikationsstelle (× oder ·), `diag.ctrl.branch` Verzweigungspunkt (gefüllter Punkt, Ports auf
  seinen vier Seiten).
- `diag.ctrl.signal` **Signalpfeil**: dünne Linie mit gefüllter Spitze (abschaltbar, z. B. in einen Verzweigungspunkt),
  standardmäßig rechtwinklig geführt. Zwei Enden an derselben Seite zweier Elemente (beide `bottom`: eine Rückführung)
  laufen mit 8 mm Abstand außen herum statt durch die Blöcke. Nach ihrer Seite benannte Ports geben auch den anderen
  Diagrammverbindern die Austrittsrichtung vor (`D.ExitHorizontal`).
- Schnelles Anfügen („+“) verkettet jedes Regelkreiselement mit einem Signalpfeil.
- `diag.ctrl.loop` **Standardregelkreis** als ein Element (Summierstelle → Regler → Regelstrecke → Verzweigungspunkt,
  Rückführung darunter; Optionen: Messglied in der Rückführung, Störgröße an der Strecke). „Zerlegen“ macht daraus die
  Einzelelemente mit an ihren Ports angedockten Pfeilen, sodass der Kreis Element für Element bearbeitet und erweitert
  werden kann. Von Hand aufbauen: Summierstelle, „+“ rechts → Regler, „+“ rechts → Strecke, ein Verzweigungspunkt, dann
  ein Signalpfeil von `bottom` des Verzweigungspunkts zu `bottom` der Summierstelle (läuft unten herum).

## Rechnersysteme und Kommunikation (25.11.2026)

Kategorie „Rechnersysteme & Kommunikation“ (FIG. 1 von Software-Erfindungen, Netzwerkskizzen):

- Systemblöcke nach Rolle: Prozessor, Speicher, Massenspeicher, Netzwerkschnittstelle, E/A-Schnittstelle, Anzeige,
  Sensor(en), Eingabegerät (Vorlagen von `diag.sys.block` mit dem neuen Parameter `role`: der übersetzte Rollenname
  erscheint, solange der Block keinen eigenen Text hat; ohne Port-Quadrate).
- `diag.sys.bus` **Systembus**: dicke Linie (Parameter Breite) oder hohle Doppellinie durch seine Punkte, beschriftet mit
  „Bus“ (Objekttext). Jedes Segment ist eine Rail: Verbindungslinien (Abgriffe) docken an beliebiger Stelle an und folgen
  dem Bus; Ecken lassen sich wie bei anderen Polylinien einfügen.
- Umriss-Symbole mit dem Namen darunter und Ports auf allen vier Seiten: `diag.sys.phone` (Smartphone),
  `diag.sys.basestation` (Gittermast mit Funkbögen), `diag.sys.router`, `diag.sys.switch`, `diag.sys.satellite`,
  `diag.sys.iot` (Sensorknoten mit Antenne).
- `diag.sys.wireless` **Funkverbindung**: Linie mit Blitz-Zickzack oder gestrichelt mit Funkbögen an beiden Enden;
  Pfeilspitzen keine / am Ende / beidseitig.

## Datenformate (25.11.2026)

`diag.data.fields` zeichnet Pakete, Rahmen, Register und Speicherbelegungen aus dem Parameter `fields`, z. B.
`Präambel:8;Header:16;Nutzdaten:n;CRC:16` (Größen in Bit, `0x…` hexadezimal; `n`, `*` oder `var` = variable Länge; ein
Feld pro Zeile geht ebenfalls). Anordnungen: `row` (eine Zeile); `wrapped`, Zeilen zu `rowBits` Bit (ein Feld über das
Zeilenende läuft in der nächsten Zeile weiter, ein variables Feld füllt den Rest seiner Zeile); `stack` (Speicherbelegung
von oben nach unten, Startadressen ab `base`, `0x…` im Format der Basis). Breiten `proportional` (variable Felder
erhalten einen gleichen Anteil) oder `equal`. Variable Felder tragen eine Bruchmarke (zwei Schrägstriche) auf ihren
Längskanten. `ruler` ergänzt Bit-Offsets (Zeile: an jedem Feldanfang; Zeilen: alle 8 Bit) bzw. die Adressspalte
(Stapel); `sizes` zeigt „16 Bit“ unter dem Namen. Jedes Feld hat Ports `f1.top`, `f1.bottom` … (Stapel zusätzlich
`f1.right`) für Bezugslinien und Bezugszeichen. Aktionen: Anordnung, gleiche Breiten, Lineal, Feld hinzufügen (Eingabe
`Name:Größe`). Vorlagen: Register / Header (32-Bit-Zeilen), Speicherbelegung.

## Sequenzfragmente, Schrittnummern und Zeitlücken (25.11.2026)

- `diag.seq.fragment` **kombiniertes Fragment**: Rahmen mit Fünfeck-Reiter (`alt`, `opt`, `loop`, `par`, `break`; der
  Objekttext wird angehängt, z. B. `loop 1..n`), Operanden (`operands`) durch gestrichelte Linien getrennt, eine
  Bedingung je Operand (`guards`, je eine Zeile; solange nicht gesetzt, übersetzte Platzhalter `[Bedingung]` / `[else]`).
  Operand hinzufügen/entfernen vergrößert/verkleinert den Rahmen um einen Operanden (Oberkante bleibt). Vorlagen opt,
  loop, par, break.
- Nachrichtenparameter `step`: eine Schrittnummer (z. B. `302`) am Anfang des Pfeils über der Linie. Die Dokumentaktion
  **„Nachrichten nummerieren“** nummeriert alle Nachrichten der Seite von oben nach unten (nach der Höhe ihrer Mitte,
  dann von links nach rechts): die oberste behält ihre Nummer, wenn sie eine Zahl ist (sonst 302), jede weitere erhält 2
  mehr (302, 304, 306 …). Sie ist idempotent, mit mehreren ausgewählten Nachrichten ergibt sich dasselbe Ergebnis.
- Lebenslinienparameter `breaks`: Zeitlücken in Prozent der Linie (`50` oder `30; 70`); die gestrichelte Linie wird
  unterbrochen und mit zwei kurzen Schrägstrichen („//“) markiert. Die Aktion „Zeitlücke“ schaltet eine Lücke bei 50 %
  ein und aus. Die Linie bleibt eine Rail.

## GUI-Wireframes (25.11.2026)

Kategorie „GUI-Wireframes“ (schwarz auf weiß; die Automotive-HMI-Elemente `auto.hmi.*` bleiben in ihrem Modul):
`diag.ui.screen` (Gerät Smartphone, Tablet, Monitor, Programmfenster, Browser; Statusleiste; der Objekttext ist Titel
oder Adresse; Bildschirme verketten sich mit gerichteten Pfeilen zu einem Bildschirmablauf), Bedienelemente
`diag.ui.button` (abgerundet / Pille / eckig, gefüllt = primär), `.field` (Text, Suche, Auswahlliste, Passwort),
`.list` (Zeilen aus `items`, Symbole, Pfeile, Zeile hinzufügen/entfernen), `.checkbox` (auch Optionsfeld), `.toggle`,
`.slider` (Wert 0–100 %), `.image` (Rahmen mit beiden Diagonalen), `.icon` (Menü, Suche, Start, Benutzer,
Einstellungen, Glocke, Stern, Herz, Plus, Schließen, Zurück, Teilen), `.tabs` (aktiver Reiter fett und unterstrichen)
sowie die Zeiger `diag.ui.touch` (Finger mit Wellen: Tippen, Doppeltippen, lange drücken, Wischen; Port `tip`) und
`diag.ui.cursor` (Pfeil oder I-Balken; Port `tip` am Hotspot). Platzhaltertexte der Elemente sind übersetzt.

## Zustandskreise und Transitionen (25.11.2026)

`diag.state.state` hat die Ansichten `rounded` (UML) und `circle` (Zustandsdiagramme im Schwarzweiß-Strichstil: Kreis
mit dem Namen, 8 Ports auf dem Umriss; die Größe passt sich beim Umschalten an). Bibliothek: „Zustand (Kreis)“ und
„Transition (Ereignis [Bedingung] / Aktion)“ (`diag.state.transition`, eine gerichtete UML-Beziehung mit dieser
Beschriftungsvorlage).

## Interaktive Pools und Bahnen (25.11.2026)

`diag.bpmn.pool` (neu: waagerechte Bahnen, Poolname im Band links, übersetzte Bahn-Platzhalter) und
`diag.activity.swimlane` teilen sich die Bahnlogik (`LaneContainerType`):

- Aktionen „Bahn unten / oben hinzufügen“ (senkrecht: rechts / links), „Untere / obere Bahn entfernen“ und „Bahn n
  umbenennen“ (Eingabe). Hinzufügen oder Entfernen vergrößert bzw. verkleinert den Container um eine Bahn um seine Mitte,
  die Bahnen behalten also ihre Größe; als Dokumentaktion verschiebt es auch die Elemente der verbleibenden Bahnen mit
  ihrer Bahn.
- Die Mittellinie jeder Bahn ist ein `Axis`-Anker (`lane0`, `lane1` …). Ablaufknoten (Aktionen, Start-/Endknoten,
  Verzweigungen, Gabelungsbalken, BPMN-Aufgaben, -Ereignisse, -Gateways) haben eine `Hub` in der Mitte: ein abgelegter
  oder verschobener Knoten rastet mittig auf der Bahnlinie ein, und die Core-Kopplung (Nabe auf Achse) nimmt ihn mit,
  wenn der Pool verschoben, gedreht oder gestreckt wird (die Bahnen skalieren mit dem Pool). Eine `End`-Marke am Rahmen
  verhindert mechanische Anbau-Ports an den Bahnlinien.
- Grenzen (Core): nur auf einer Bahnlinie zentrierte Knoten werden mitgenommen; anderswo in einer Bahn platzierte
  Elemente oder Elemente anderer Module (`ann.flow.*`) folgen einem verschobenen Pool nicht. Eine allgemeine
  Enthaltensein-Beziehung bräuchte Unterstützung im Core.
## Fehlerbäume, Ablaufsprache, Ishikawa-Diagramme und Mindmaps (26.11.2026)

Alle Symbole sind eigenständig nach den üblichen Lehrbuchkonventionen gezeichnet (Fehlerbäume wie z. B. in IEC 61025
beschrieben, Schritt-Transitions-Pläne im Stil von GRAFCET, IEC 60848); Normkonformität wird nicht behauptet. Jedes
Element lässt sich ohne Parameter einfügen; Texte sind der Objekttext (Doppelklick oder Textfeld des Hosts), wie bei den
anderen Diagrammknoten.

**Fehlerbäume** (Kategorie „Fehlerbaumanalyse (FTA)“):

- Ereignisse: Top-Ereignis (Rechteck, kräftigerer Umriss, fette Schrift), Zwischenereignis (Rechteck), Basisereignis
  (Kreis), nicht entwickeltes Ereignis (Raute), Hausereignis (Hausumriss), Bedingungsereignis (Ellipse; die Bedingung
  eines Inhibit-Gatters). Gatter: UND (gerade Basis, runde Kuppe), ODER (gewölbte Basis, Spitze oben), k aus n
  (ODER-Form mit `k/n`, Parameter `k` und `n`, Aktion „k aus n“ fragt nach `2/3`), Inhibit (Sechseck). Gatter haben oben
  einen Ausgangsstiel (Port `top`) und unten einen Eingangsstiel (Ports `bottom`, `bottom.q1`, `bottom.q3`); der
  Gattertext (z. B. `G1`) steht rechts neben dem Gatter. Übertragsymbol: Dreieck mit Kennung (`A`).
- `diag.fta.link` verbindet ein Gatter oder Ereignis (Anfang) mit einem seiner Eingänge (Ende): eine einfache Linie,
  standardmäßig rechtwinklig. Schnelles Hinzufügen („+“) verkettet alle Fehlerbaumelemente mit dieser Verbindung; „+“
  unter einem Gatter ergibt die richtige Richtung. Eine von einem Blattereignis (Basis-, nicht entwickeltes, Haus-,
  Bedingungsereignis) nach oben zu einem Gatter oder Ereignis gezogene Verbindung wird umgekehrt gelesen.
- **Wahrscheinlichkeiten (optional):** Blattereignisse und das Übertragsymbol nehmen eine Wahrscheinlichkeit `p` auf
  (Parameter oder Aktion „Wahrscheinlichkeit“, z. B. `1e-4`, `0.001`, `0,001`, `0,5 %`). Jedes Gatter und Ereignis
  darüber erhält seine Wahrscheinlichkeit, berechnet beim Zeichnen (nie gespeichert): UND und Inhibit = Produkt, ODER =
  1 − Π(1 − p), k aus n = Wahrscheinlichkeit, dass mindestens k Eingänge eintreten (unabhängige Ereignisse; exakt, nicht
  die Näherung für seltene Ereignisse); ein Ereignis oder Übertrag mit mehreren Eingängen liest sie als ODER. Blätter
  zeigen `p = …` unter dem Symbol, Ereignisrechtecke innen unter dem Text, ein Gatter nur, wenn kein Ereignis direkt
  darüber steht (rechts daneben). Der Wert erscheint nur, wenn alle Eingänge darunter bekannt sind. Deutsche Ausgabe mit
  Dezimalkomma. Ändert sich ein `p`, ein Gatter oder eine Verbindung, wird alles darüber neu gezeichnet (`Dependents`).
- **Baum anordnen** (Aktion an jedem verbundenen Fehlerbaumelement): der ganze Baum, zu dem das Element gehört, wird vom
  Top-Ereignis aus als ordentlicher Baum von oben nach unten angeordnet (Ebenen 8 mm auseinander, oben bündig;
  Geschwister 6 mm auseinander; jedes Elternelement mittig über seinen Eingängen); das Top-Ereignis behält Oberkante und
  Mitte, außer der Baum ragte über den Seitenrand. Die Verbindungen werden wieder an den zugewandten Ports unten/oben
  angedockt. Mehrfach erreichte Elemente werden beim ersten Besuch platziert. Das allgemeine Layout der Mehrfachauswahl
  (Core `DiagramLayout`) funktioniert ebenfalls für Fehlerbäume.

**Ablaufsprache** (Kategorie „Ablaufsprache (SFC, GRAFCET)“):

- `diag.sfc.step` (Quadrat, Schrittnummer als Text, Platzhalter `1`), `diag.sfc.step.initial` (Doppelrahmen, Platzhalter
  `0`); Option `active` zeichnet einen Markenpunkt. `diag.sfc.transition`: dicker Querstrich über der Verbindungslinie,
  die vom Port `top` zum Port `bottom` durch das Element läuft; die Übergangsbedingung (Objekttext) steht rechts neben
  dem Strich. `diag.sfc.action`: Aktionskasten (am Port `right` des Schritts andocken), optionales
  `qualifier`-Bestimmungszeichen (N, S, R, D, L, P …, wie in der SFC nach IEC 61131-3) in eigenem Feld; Vorlage
  „Gespeicherte Aktion (S)“. `diag.sfc.branch`: Parallelverzweigung (Doppellinie) oder Alternativverzweigung
  (Einzellinie) mit `branches` (2–8) Andockpunkten `b1.top`, `b1.bottom` … sowie `top`/`bottom` in der Mitte; „Zweig
  hinzufügen/entfernen“ verlängert/verkürzt den Balken um einen Zweigabstand.
- `diag.sfc.link`: einfache Linie, standardmäßig rechtwinklig. Eine Verbindung, deren Ende über ihrem Anfang liegt
  (Rücksprung von der letzten Transition zum Anfangsschritt), läuft links herum, `clearance` (10 mm) neben ihren Enden und
  4 mm frei von jedem Element, das sie sonst kreuzen würde, und trägt in der Mitte ihres aufsteigenden Stücks einen
  Richtungspfeil (abschaltbar).

**Ishikawa-Diagramm** `diag.ishikawa` (ein Element): Wirkung in einem Kopfkasten rechts (Objekttext, Platzhalter
„Wirkung“), eine Mittelgräte mit Pfeil in den Kopf, je Kategorie eine schräge Gräte abwechselnd über und unter der
Mittelgräte, jede Kategorie in einem Rahmen (abschaltbar). `categories` enthält eine Kategorie pro Zeile; Ursachen folgen
nach einem Doppelpunkt, mit Semikolon getrennt (`Maschine: Verschleiß; Schwingung`), und werden als Nebengräten
gezeichnet. Solange der Parameter nicht gesetzt ist, erscheinen die sechs klassischen Kategorien in der Sprache des
Benutzers (Mensch, Maschine, Methode, Material, Messung, Mitwelt / Man, Machine, Method, Material, Measurement,
Environment). Aktionen: Kategorie hinzufügen/entfernen (bis 8), Ursache hinzufügen (Eingabe `2: Verschleiß`), Kategorie
n umbenennen, Kategorierahmen.

**Mindmap:** `diag.mindmap.node` (Formen abgerundetes Rechteck, Ellipse, Rechteck, unterstrichener Text; fett), Vorlagen
„Zentrales Thema“ (Ellipse, fett) und „Zweigtext“ (unterstrichen); `diag.mindmap.link` ist ein geschwungener Zweig
(kubisch, verlässt und erreicht die Knoten entlang der Hauptrichtung; auf gerade umschaltbar). Schnelles Hinzufügen
verkettet Knoten mit Zweigen. **Mindmap anordnen ▸** (an einem Knoten mit Zweigen): *Beidseitig* (schon links stehende
Zweige bleiben links, sonst geht die untere Hälfte nach links), *Nach rechts* oder *Radial* (Tiefe d auf einem Kreis d·R,
jeder Teilbaum in einem Sektor im Verhältnis seiner Blätter). Verbindungen werden ohne Richtung gelesen; der Knoten, an
dem die Aktion läuft, ist die Mitte und bleibt stehen (außer die Mindmap ragte über den Seitenrand); die Zweige werden
wieder an den zugewandten Ports angedockt.

Offen: andere Werte als Wahrscheinlichkeiten (Ausfallraten über eine Missionszeit, minimale Schnittmengen), Behandlung
gemeinsamer Ursachen bei mehrfach genutzten Basisereignissen (ein geteiltes Ereignis zählt in jedem Gatter als
unabhängig), SFC-Makroschritte und einschließende Schritte, Zweigfarben je Hauptzweig der Mindmap, ein eigenes „+“, das
ein Kind eines anderen Typs anlegt (schnelles Hinzufügen kopiert das Element; „nächstes Element“ ersetzt es).

## Kontextaktionen

| Typ | Aktionen |
|---|---|
| Klasse / Schnittstelle / Aufzählung | Attribute und Operationen ein-/ausblenden, Attribut (Literal) hinzufügen/entfernen, Operation hinzufügen/entfernen, abstrakt |
| Zustand | Aktivitäten anzeigen, Eintritts-/Austrittsaktivität hinzufügen, letzte Austrittsaktivität entfernen |
| Zusammengesetzter Zustand | Region hinzufügen/entfernen |
| Schwimmbahnen, Pool | waagerecht/senkrecht (tauscht Breite und Höhe), Bahn an einem der Enden hinzufügen, erste/letzte Bahn entfernen, Bahn n umbenennen |
| Summierstelle, Übertragungsglied, Signalpfeil | Vorzeichen Rückführung, Eingang von oben, Kreuzform / Inhalt (Name, Bruch, Kennlinie), Kennlinie ▾ (Optionen zeigen die Kurve) / Pfeilspitze, Richtung umkehren, Führung ▾ |
| Datenfelder | Anordnung, gleiche Breiten, Lineal, Feld hinzufügen |
| Fragment, Nachricht, Lebenslinie | Operator, Operand hinzufügen/entfernen / Nachrichten nummerieren / Zeitlücke |
| GUI-Elemente | Gerät, Statusleiste, gefüllt, Form, Art, Zeilen, ausgewählt, ein, Wert ±10, Motiv ▾ (Optionen zeigen das Motiv), aktiver Reiter ▾, Geste |
| Entität | Attribut hinzufügen/entfernen, schwache Entität |
| ER-Attribut / -Beziehung | Schlüssel, mehrwertig, abgeleitet / identifizierend |
| UML-Beziehung | Art (8 Möglichkeiten), Richtung umkehren (Multiplizitäten und Rollen bleiben an ihren Klassenenden), rechtwinklige Führung |
| Krähenfuß-Verbinder | Kardinalität Anfang ▾ / Ende ▾ (keine, eins, genau eins, null oder eins, viele, eins oder viele, null oder viele), Enden tauschen, identifizierend, Führung ▾ (gerade / rechtwinklig) |
| Sequenznachricht | synchron/asynchron/Antwort/Erzeugen, Richtung umkehren |
| Block | Port links/rechts hinzufügen/entfernen |
| Lebenslinie, Container, Lolli, Aufgabe, Gateway | zerstört / Akteur-Kopf, gestrichelter Rahmen / Titelband, benötigte Buchse, Teilprozess, Gateway-Markierung ▾ |
| Fehlerbaumelemente | Wahrscheinlichkeit (Blätter, Übertrag), k aus n (Mehrheitsgatter), Baum anordnen |
| SFC-Schritt, -Verzweigung, -Verbindung | aktiv (Marke) / Verzweigung ▾ (parallel, alternativ), Zweig hinzufügen/entfernen / Aufwärtspfeil, Richtung umkehren, Führung ▾ |
| Ishikawa-Diagramm | Kategorie hinzufügen/entfernen, Ursache hinzufügen, Kategorie n umbenennen, Kategorierahmen |
| Mindmap-Knoten, -Zweig | Form ▾, fett, Mindmap anordnen ▸ (beidseitig, nach rechts, radial) / geschwungen, Richtung umkehren |

Eine hinzugefügte Zeile vergrößert die Box um eine Zeilenhöhe nach unten (Oberkante bleibt stehen); Entfernen
verkleinert sie entsprechend.

## Host-Hinweise

Alle Symbole bestehen aus nativen Primitiven (Rechtecke, Ellipsen, Linien, Pfade, Text) und bleiben mit den
Formatwerkzeugen des Hosts bearbeitbar. Abgerundete Ecken werden als Pfade ausgegeben. Bahnköpfe waagerechter
Schwimmbahnen nutzen um −90° gedrehten Text. Beschriftungen von BPMN-Ereignissen und -Gateways stehen unter dem Symbol,
außerhalb seiner Box (BPMN-Konvention). Notationsquellen: UML 2.5.1 (OMG) für UML-Formen, BPMN 2.0.2 (OMG) für
BPMN-Formen, Chen (1976) und die Krähenfuß-Notation (Information Engineering) für ER; alle Symbole sind eigenständig
gezeichnet. Fehlerbaumgatter sind geschlossene Pfade (kubische Kurven), der SFC-Aufwärtspfeil und die Pfeilspitze der
Ishikawa-Mittelgräte sind native Formen/Pfeilspitzen, Mindmap-Zweige einzelne kubische Pfade, sodass jeder Host sie als
native Freihandformen zeichnet. Fehlerbaumwerte sind Textprimitive; Hosts, die Formen getrennt halten (PowerPoint COM),
zeichnen die Elemente über einem geänderten Ereignis über `Dependents` neu.
