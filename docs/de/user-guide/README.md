# Benutzerhandbuch

> Deutsche Übersetzung. Maßgeblich ist die englische Fassung: [../../en/user-guide/README.md](../../en/user-guide/README.md).

> Dieses Handbuch wächst mit jeder ausgelieferten Funktion und beschreibt nur, was tatsächlich funktioniert.

## 1. Wofür InventaSketch gedacht ist

Schnelle technische Skizzen direkt in Ihrer Präsentation: Getriebe und Maschinenelemente, Mechanismen, Patentfiguren mit
Bezugszeichen, Funktionsdiagramme, Flussdiagramme. Sie ziehen Elemente auf die Folie, skalieren sie frei und lassen sie
aneinander einrasten. Zahlenwerte (Modul, Zähnezahl, Maße) sind optional.

## 2. Installation

- **PowerPoint-Web-Add-in** (Windows, Mac, Web): Zum Sideloading während der Entwicklung siehe
  [hosts/office-web.md](../../en/hosts/office-web.md) (englisch). Die Verteilung über den Store folgt später.
- **PowerPoint-COM-Add-in** (Windows) und **LibreOffice-Erweiterung**: geplant, siehe [Roadmap](../ROADMAP.md).

## 3. Grundlegender Arbeitsablauf (Zielverhalten)

1. Öffnen Sie den InventaSketch-Bereich und durchsuchen Sie die Bibliothek (deutsche und englische Begriffe
   funktionieren, z. B. „Zahnrad“ oder „gear“).
2. Klicken Sie auf ein Element: Ein Umriss folgt dem Mauszeiger und rastet ein. Klicken Sie zum Platzieren oder ziehen
   Sie, um die Größe festzulegen. `Esc` bricht ab.
3. Verschieben Sie Objekte; sie rasten an Mittelpunkten, Kanten und Wellenachsen ein und – bei Zahnrädern in der
   Axialansicht – kämmen auf ihren Teilkreisen. Halten Sie `Alt` gedrückt, um frei zu verschieben.
4. Eckgriffe skalieren proportional, Seitengriffe dehnen in eine Richtung. Durch Skalieren ändern sich Parameter nie.
5. Ein Rechtsklick öffnet das Kreismenü (Ansichten, Spiegeln, Bezugszeichen, Löschen). Die schwebende Leiste neben der
   Auswahl blendet aus, wenn Sie sich mit dem Mauszeiger entfernen.
6. Wechseln Sie die Darstellung (Getriebeschema, Seitenansicht, Schnitt, axial, Verzahnung), ohne Beschriftungen oder
   Verknüpfungen zu verlieren.
7. Fügen Sie Bezugslinien und Bezugszeichen hinzu, führen Sie die Patent-Vorabprüfung (Preflight) für das gewählte
   Anmeldeamt aus und exportieren Sie in Schwarz-Weiß.

## 4. Tastatur

| Taste | Aktion |
|---|---|
| `Del` | löschen |
| `Ctrl+Z` / `Ctrl+Y` | rückgängig / wiederholen |
| `Ctrl+D` | duplizieren |
| `R` | um 90° drehen |
| `X` / `Y` | horizontal / vertikal spiegeln |
| Pfeiltasten | schrittweise verschieben (`Shift` = 10×) |
| `G` | gruppieren |
| `Shift+F10` | Kreismenü |
| `Strg+Leertaste` | Einfügefeld: suchen und einfügen (passende Teile zuerst) |
| `Tab` / `Shift+Tab` | nächstes / vorheriges Teil der Baugruppe (PowerPoint COM) |
| `Esc` | abbrechen / Auswahl aufheben |

## 5. Patentzeichnungen – wichtiger Hinweis

Die Vorabprüfung (Preflight) prüft die formalen Zeichnungsvorschriften von DPMA, EPA, PCT und USPTO mit zitierten
Quellen. Sie ist nur eine Hilfestellung und ersetzt nicht die Prüfung durch eine Patentfachperson. In PowerPoint:
InventaSketch ▸ Prüfen ▸ „Patentprüfung“ prüft alle Folien für das gewählte Amt und listet Fehler,
Warnungen, Hinweise und selbst zu prüfende Punkte mit Fundstelle; ein Doppelklick auf einen Befund springt zur Folie
und zum Element (Engine `preflight`, `patentProfiles`). Im Web-Add-in macht der Prüfknopf in der oberen Leiste dasselbe
für die Seite; ein Klick auf einen Befund wählt sein Element aus. In LibreOffice Impress und Draw: InventaSketch ▸
„Patentprüfung (Zeichnungen) …“ prüft alle Seiten; die Wahl eines Befunds springt zu Folie und Einheit.

Varianten (vorher/nachher, Stand der Technik/Erfindung): Folie duplizieren und die Kopie ändern. InventaSketch ▸ Prüfen
▸ „Mit vorheriger Folie vergleichen“ markiert, was neu (grün), geändert (orange) und entfernt (grau gestrichelt) ist,
mit Legende; ein erneuter Aufruf entfernt die Markierungen (Engine `diff`). In LibreOffice Impress und Draw:
InventaSketch ▸ „Mit vorheriger Folie vergleichen (Varianten)“.

## 6a. Zeichnungen und Daten importieren

InventaSketch ▸ Prüfen ▸ „Importieren …“ (Web: Import-Knopf in der oberen Leiste; LibreOffice: InventaSketch ▸ „Zeichnung
importieren (SVG, DXF) …“) liest SVG- und DXF-Zeichnungen als bearbeitbare Formen – Linien, Polylinien, Rechtecke,
Ellipsen und Texte, gruppiert als ein Element. Eine Zeichnung mit Einheiten behält ihre echte Größe, wenn sie auf die
Folie passt; sonst wird sie eingepasst. Bögen und Kurven werden zu Polylinien mit verschiebbaren Punkten. Farben,
Linienarten, Füllungen, Schraffuren, Blöcke und Bemaßungen werden nicht importiert. Derselbe Befehl liest CSV-Tabellen
in Diagramme und GeoJSON/GPX als Kartenrouten.

## 6b. Mit „+“ weiterbauen

Ein einzeln ausgewähltes Element zeigt an jeder freien Stelle, an der etwas angebaut werden kann, ein blaues „+“: ein
Gegenrad am Zahnrad, das nächste Wellenstück oder ein Teil am Wellenende, ein Planet im Hohlrad, das nächste Glied an
einem freien Gelenk, ein Werkzeug am Roboterflansch – und bei allem, was mit Leitungen verbunden wird, an jedem freien
**Anschluss**:

- **Welche Anschlüsse.** Elektrische Schaltzeichen, Hausinstallation, Leiterplatten-Pads, Energieelemente (PV-Module,
  -Strings und -Felder, Wechselrichter, Batterien, Windenergieanlagen, Transformatoren, Schaltgeräte, Ladepunkte,
  Wärmepumpen, Elektrolyseure …), Gebäudetechnik (Heizkörper, Kessel, Wärmepumpen, Verteiler, Pumpen, Ventile,
  Sanitärobjekte, Steigleitungen, Luftdurchlässe, Lüftungsgeräte, Anschlussdosen und Melder), Fluidtechnik, Anlagen der
  Verfahrenstechnik (Stutzen, Armaturen, Pumpen, Messstellen) sowie die elektrischen, Kühl- und Blockschaltbild-Anschlüsse
  der elektrischen Maschinen. Ein Anschluss, an dem schon eine Leitung oder ein Element sitzt, zeigt kein „+“.
- **Was angeboten wird.** Ein Klick auf das „+“ öffnet das Einfügefeld, zuerst mit der Leitung für das Medium des
  Anschlusses – ein DC-Kabel an der DC-Seite von PV-Modul, Wechselrichter oder Batterie, ein AC-Kabel an der Netzseite
  (mit der Spannung der Wicklung oder des Netzes, wo das Element sie angibt), eine Vorlaufleitung am Vorlauf und eine
  Rücklaufleitung am Rücklauf, Kalt-, Warm-, Zirkulations- und Schmutzwasserleitungen an Sanitärobjekten, eine Zu- oder
  Abluftleitung am Luftdurchlass, eine Installationsleitung an Steckdose, Schalter und Anschlussdosen, eine Leitung an
  Schaltzeichen, eine Leiterbahn am Pad, eine Arbeits-, Steuer- oder Leckleitung in der Fluidtechnik, eine Prozess- oder
  Signalleitung im R&I-Fließschema –, danach die Elemente mit einem Anschluss desselben Mediums.
- **Leitung.** Die Wahl der Leitung zieht sie gerade aus dem Anschluss heraus, ihr Anfang am Anschluss angedockt, und
  wählt sie aus. Ihr freies Ende zeigt wieder ein „+“: dieselbe Leitung wählen verlängert sie um ein Stück, ein Element
  wählen setzt dessen passenden Anschluss auf das Leitungsende (die Leitung bleibt angedockt, wenn das Element bewegt
  wird). So entsteht eine ganze PV-Anlage, ein Heizkreis oder eine Schaltung durch wiederholtes Klicken auf „+“.
- **Element.** Die direkte Wahl eines Elements setzt es eine Leitungslänge entfernt, mit dem passenden Anschluss zurück
  zum Ausgangselement gewandt, und verbindet beide mit der Leitung.
- **Passende Medien.** Eine allgemeine elektrische Leitung passt an DC- und AC-Anschlüsse, eine Heizungspumpe oder ein
  Ventil an Vor- und Rücklauf, eine Wärmepumpe an Wärme- und Kälteleitungen. Elemente anderer Module erscheinen nur, wenn
  ihr Anschluss genau dieses Medium führt (eine Anschlussdose der Gebäudetechnik nach einer Installationsleitung, kein
  Widerstand nach einem PV-String). Eine von Hand aus einem Anschluss gezogene Leitung führt das Medium des Anschlusses,
  ihr freies Ende zeigt also ebenfalls das „+“.

Hosts: Das PowerPoint-Web-Add-in zeigt diese „+“ an Elementen und an Leitungsenden (Engine `buildPorts`, Arten
`Connect` und `LineEnd` mit `medium`; `buildItems`; `buildPlace` baut Leitung, Element oder verlängerte Leitung in einem
Rückgängig-Schritt); LibreOffice listet dieselben Anschlüsse in der Seitenleiste und baut über dieselben Engine-Aufrufe.
Im PowerPoint-COM-Add-in bietet das „+“ an Elementen Leitungen und Elemente an, ein dort gewähltes Element wird aber
noch ohne verbindende Leitung gesetzt, und Leitungsenden zeigen noch kein „+“; beides folgt mit einer
Host-Aktualisierung.

## 6. Module

Jedes Modul hat eine eigene Seite mit Elementen, Ansichten und Parametern:
[Antriebselemente](../modules/mechanical-drives.md), [Kinematik](../modules/kinematics.md),
[Elektrotechnik](../modules/electrical.md), [elektrische Maschinen](../modules/machines.md),
[Automotive](../modules/automotive.md), [Architektur](../modules/architecture.md),
[technische Gebäudeausrüstung](../modules/building-services.md), [Fluidtechnik](../modules/fluid.md),
[Diagramme](../modules/diagrams.md), [Karten und Routen](../modules/maps.md), [Diagramme und Kennlinien](../modules/charts.md),
[Verfahrens- und Wärmetechnik](../modules/process.md), [Energiesysteme](../modules/energy.md), [Robotik](../modules/robotics.md), [Mathematik & Geometrie](../modules/geometry.md).

Jedes Element gehört zu einem Modul (Review 25.11.2026): Installationen (Sanitär, Heizung, Leitungen) zur
Gebäudetechnik, Drehrichtungs- und Drehmomentpfeile zur Mechanik, Kurvenscheiben zur Kinematik, der Nordpfeil zu den
Karten. Elemente mit gleichem Namen, aber anderer Bedeutung tragen eindeutige Namen (Leitungsschutzschalter /
Leistungsschalter, Kolonne / Stütze, Feder (Sinnbild) / Feder zwischen zwei Gelenken). Welche Module Menüband und
Bibliothek zeigen: InventaSketch ▸ Module.
Wie die Add-ins auf der Folie arbeiten (Kontextleiste, Einfügefeld mit Strg+Leertaste, Mehrfachauswahl, Baugruppen,
Bewegungsmodus), steht je Host: [PowerPoint COM](../hosts/powerpoint-com.md), [PowerPoint im Web](../hosts/office-web.md).
