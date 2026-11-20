# Diagramm-Modul (`diagrams`, Typ-IDs `diag.*`)

> English: [../../en/modules/diagrams.md](../../en/modules/diagrams.md)

UML-, Entity-Relationship- (ER), System-/Architektur- und BPMN-light-Diagramme für schnelle technische Skizzen. Jedes
Symbol lässt sich ohne Parameter einfügen; freies Skalieren ändert nur die Boxgröße. Einfache Ablaufdiagramm-Starter
(Verarbeitungsschritt, Verzweigung, Start/Ende, Winkelverbinder) bleiben im Beschriftungsmodul (`ann.flow.*`,
`ann.arrow.elbow`) und werden hier nicht dupliziert.

## Konzepte

- **Box-Symbole** werden in lokalen Koordinaten gezeichnet und bieten **Port-Anker** an den Kantenmitten und
  Viertelpunkten (12 je Rechteck). Ellipsen bieten 8 Ports auf dem Umriss, Rauten ihre Ecken und Kantenmitten, Blöcke
  ihre Port-Quadrate, Lebenslinien alle 5 mm einen Port entlang der Linie, Aktivierungsbalken alle 2,5 mm je Seite.
- **Verbinder** sind Punktobjekte (Beziehungen, Krähenfuß-Verbinder, Sequenznachrichten). Ihre Enden rasten an Ports
  ein und werden als Anbindung gespeichert, folgen also verschobenen, gedrehten und gestreckten Boxen. Mit zwei Punkten
  ist ein Verbinder gerade oder mit `route = orthogonal` rechtwinklig geführt (an eine Boxkante gebundene Enden
  verlassen sie senkrecht). Bei mehr Punkten sind die Punkte Stützpunkte.
- **Mehrzeilige Inhalte** (Attribute, Operationen, Literale, Bahnen, Aktivitäten) stehen in Textparametern, eine Zeile
  pro Eintrag. Jede Zeile wird als eigenes Textprimitiv gezeichnet.
- **Textanpassung:** Text nutzt den Parameter `textSize` (Standard 3,5 mm ≈ 10 pt). Passt der Inhalt nicht in die Box,
  wird der Text automatisch verkleinert (nicht unter 1,8 mm); der Parameter selbst ändert sich nie.
- **Endnotationen** (Pfeilspitzen, Rauten, Krähenfüße) werden als explizite Primitive gezeichnet, damit hohle Dreiecke
  und Rauten in jedem Host gleich aussehen. Eine Linienfarbe des Benutzers färbt auch gefüllte Spitzen und Texte;
  Breiten- und Strichart-Überschreibungen gelten nur für Umriss bzw. Verbinderlinie.

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
| BPMN-light | `diag.bpmn.start`, `diag.bpmn.intermediate`, `diag.bpmn.end`, `diag.bpmn.task` (Teilprozess-Markierung), `diag.bpmn.gateway` (exklusiv, parallel, inklusiv, ereignisbasiert) |

## Textkonventionen

- **Klassenmitglieder:** ein Mitglied pro Zeile, Sichtbarkeit als Teil der Zeile (`+ - # ~`). Ein führendes `_`
  kennzeichnet ein statisches Mitglied, das ohne Unterstrich-Zeichen unterstrichen dargestellt wird.
- **Entitätsattribute:** `*` Primärschlüssel (unterstrichen), `#` Fremdschlüssel (`(FK)` in der Diagrammansicht,
  Spalte `FK` in der Tabellenansicht); kombinierbar (`*#order_id`). Ein Typ folgt nach Doppelpunkt: `id: int`.
- **Stereotypen** werden ohne Guillemets eingegeben; sie werden automatisch ergänzt (`interface` → `«interface»`).
- **Beziehungsrichtung:** die Markierung sitzt am Endpunkt, also vom Teil/Kind/Client zum Ganzen/Elternteil/Anbieter
  zeichnen.

## Kontextaktionen

| Typ | Aktionen |
|---|---|
| Klasse / Schnittstelle / Aufzählung | Attribute und Operationen ein-/ausblenden, Attribut (Literal) hinzufügen/entfernen, Operation hinzufügen/entfernen, abstrakt |
| Zustand | Aktivitäten anzeigen, Eintritts-/Austrittsaktivität hinzufügen, letzte Austrittsaktivität entfernen |
| Zusammengesetzter Zustand | Region hinzufügen/entfernen |
| Schwimmbahnen | waagerecht/senkrecht (tauscht Breite und Höhe), Bahn hinzufügen/entfernen |
| Entität | Attribut hinzufügen/entfernen, schwache Entität |
| ER-Attribut / -Beziehung | Schlüssel, mehrwertig, abgeleitet / identifizierend |
| UML-Beziehung | Art (8 Möglichkeiten), Richtung umkehren (Multiplizitäten und Rollen bleiben an ihren Klassenenden), rechtwinklige Führung |
| Krähenfuß-Verbinder | Kardinalität Anfang/Ende weiterschalten (eins → genau eins → null oder eins → viele → eins oder viele → null oder viele), Enden tauschen, identifizierend, rechtwinklige Führung |
| Sequenznachricht | synchron/asynchron/Antwort/Erzeugen, Richtung umkehren |
| Block | Port links/rechts hinzufügen/entfernen |
| Lebenslinie, Container, Lolli, Aufgabe, Gateway | zerstört / Akteur-Kopf, gestrichelter Rahmen / Titelband, benötigte Buchse, Teilprozess, nächste Gateway-Markierung |

Eine hinzugefügte Zeile vergrößert die Box um eine Zeilenhöhe nach unten (Oberkante bleibt stehen); Entfernen
verkleinert sie entsprechend.

## Host-Hinweise

Alle Symbole bestehen aus nativen Primitiven (Rechtecke, Ellipsen, Linien, Pfade, Text) und bleiben mit den
Formatwerkzeugen des Hosts bearbeitbar. Abgerundete Ecken werden als Pfade ausgegeben. Bahnköpfe waagerechter
Schwimmbahnen nutzen um −90° gedrehten Text. Beschriftungen von BPMN-Ereignissen und -Gateways stehen unter dem Symbol,
außerhalb seiner Box (BPMN-Konvention). Notationsquellen: UML 2.5.1 (OMG) für UML-Formen, BPMN 2.0.2 (OMG) für
BPMN-Formen, Chen (1976) und die Krähenfuß-Notation (Information Engineering) für ER; alle Symbole sind eigenständig
gezeichnet.
