# InventaSketch – Produktspezifikation

> Deutsche Übersetzung. Maßgeblich ist die englische Fassung: [../../en/product/specification.md](../../en/product/specification.md).

> Dieses Dokument fasst die drei früheren Planungsentwürfe zusammen und ersetzt sie (einen Gemini-Anforderungskatalog,
> eine ChatGPT-Post-MVP-Spezifikation und eine „Master-Spezifikation“ mit Implementierungs-Prompt); dazu kommen die
> Anforderungen des Maintainers vom 2026-11-20 und die Recherchenotizen in [`docs/en/research/`](../../en/research/)
> (deutsche Kurzfassung: [`docs/de/research/`](../research/README.md)).
> Statuswerte: **ausgeliefert**, **in Arbeit**, **geplant**, **vorgeschlagen**. Regel: Nichts gilt ohne Tests als „ausgeliefert“.

## 1. Vision

InventaSketch macht aus Ideen klare technische Skizzen – **direkt in PowerPoint und LibreOffice**. Es ist ein
Werkzeug für schnelles, pragmatisches Prototyping für Ingenieure, Erfinder und Patentfachleute: Ziehen Sie ein Zahnrad,
eine Welle, ein Wälzlager, einen Widerstand, eine Wand, ein Fahrzeug oder einen Flussdiagramm-Schritt auf die Folie,
skalieren Sie es frei, lassen Sie es an seinen Nachbarn einrasten, ergänzen Sie Pfeile, Beschriftungen und
Bezugszeichen – und das Ergebnis ist eine saubere, bearbeitbare Figur. Ohne Maße, ohne Modul, ohne Zähnezahl.
Genauigkeit, Parameter und Prüfungen nach den Vorschriften der Patentämter stehen bei Bedarf zur Verfügung, werden aber
nie verlangt.

Es ist **kein** CAD-, EDA-, BIM-, GIS-, FEM- oder Mehrkörpersimulationssystem und kein Werkzeug zur rechtlichen
Konformitätsprüfung.

### 1.1 Produktprinzipien

1. **Quick Sketch ist der Standard.** Jedes Objekt lässt sich ohne Dialoge oder Parameter einfügen; freies
   proportionales und nicht proportionales Skalieren erzwingt nie eine Neuberechnung.
2. **Schematische Darstellungen sind vollwertig** (Getriebeschemata in Strichdarstellung, vereinfachte Darstellungen,
   Symbole) – keine minderwertigen Ersatzlösungen für ein 3D-Modell.
3. **Nativ zuerst.** Die Ausgabe besteht aus nativen PowerPoint-/LibreOffice-Formen, soweit der Host es zulässt, damit
   Farben, Linienstärken, Schriften, Gruppierung und Position mit den Werkzeugen des Hosts bearbeitbar bleiben.
   Vektorgrafiken nur dort, wo ein Host die Geometrie nicht abbilden kann – je Host dokumentiert.
4. **Interaktion auf der Folie**, wo der Host es erlaubt (COM, LibreOffice), mit kontextbezogener schwebender Leiste und
   Kreismenü per Rechtsklick; ein **interaktiver Vorschau-Editor in der Seitenleiste**, wo nicht (Office-Web-Add-in).
5. **Semantik hinter der Grafik.** Ein Zahnrad weiß, dass es ein Zahnrad ist, eine Bezugslinie kennt ihr Bauteil, eine
   Leitung kennt ihre Pins. Das kanonische Dokumentmodell bleibt im Host-Dokument erhalten und kann zur Bearbeitung
   wieder geöffnet werden.
6. **Eine Engine, drei Hosts, modulare Fachgebiete.** Gemeinsame C#-Engine; unabhängige Fachmodule, die sich
   hinzufügen, kombinieren oder entfernen lassen, ohne den Kern anzufassen.
7. **Ehrliche Angaben zum Funktionsumfang.** Keine Platzhalter-Schaltflächen, keine ungeprüften Behauptungen der Art
   „normgerecht“; jede Einschränkung eines Hosts steht in der Fähigkeitsmatrix.
8. **Lokal zuerst.** Bearbeiten, Bauen und Exportieren funktionieren offline.

### 1.2 Erfolgskriterien (erste öffentliche Version)

- Ein zweistufiges Getriebe (zwei Wellen, vier Zahnräder, vier Wälzlager, Leistungsflusspfeil, Beschriftungen) wird
  in unter einer Minute ohne Eingabe einer einzigen Zahl als Getriebeschema skizziert und lässt sich auf vereinfachte
  Darstellungen umschalten.
- Eine Patentfigur mit Bezugszeichen, gekrümmten Bezugslinien und Schraffur wird erstellt, gegen ein Amtsprofil
  (DPMA/EPA/PCT/USPTO) geprüft und monochrom als SVG/PDF/PNG exportiert.
- Eingefügte Objekte sind native Formen in PowerPoint (Windows-COM) und bleiben mit PowerPoint-Werkzeugen bearbeitbar.
- Dasselbe Dokument lässt sich im Seitenleisten-Editor des Web-Add-ins (PowerPoint für Mac/Web) und in LibreOffice öffnen.

## 2. Anwender und Anwendungsfälle

| Anwender | Typische Arbeit | Wichtigste Anforderungen |
|---|---|---|
| Entwicklungsingenieur | Konzeptskizzen, Getriebe-/Antriebsstranglayouts, Mechanismen, E/E-Architekturen | Tempo, Einrasten, schematische Symbole, Varianten |
| Erfinder / F&E | Erfindungsmeldungen, Ideensammlungen, Funktionskonzepte | schnelle Figuren, Nummerierung, Vorher-/Nachher-Varianten |
| Patentanwalt / Patentsachbearbeiter | Patentfiguren, Bezugszeichenlisten, einreichungsfertige Exporte | Regelprofile, Monochromie, konsistente Bezugszeichen |
| Lehrende / Studierende | Technische Mechanik, Maschinenelemente, Schaltungen | klare Lehrbuchkonventionen |
| Berater / Manager | Prozess- und Systemdiagramme in Präsentationen | native PowerPoint-Formen, Designs |

## 3. Hosts und Plattformen

| Host | Technologie | Plattformen | Interaktionsmodell | Status |
|---|---|---|---|---|
| PowerPoint-COM-Add-in | .NET Framework 4.8, Engine im Prozess | Windows | auf der Folie (Overlay, schwebende Leiste, Kreismenü) + Seitenleiste | geplant (Phase 2) |
| PowerPoint-Web-Add-in | Office.js + Engine als .NET-WebAssembly | Windows, macOS, PowerPoint im Web (iPad nur lesend) | interaktiver Vorschau-Editor in der Seitenleiste; native Formen werden auf die Folie geschrieben | in Arbeit |
| LibreOffice-Erweiterung | Python-UNO-`.oxt` + Engine als .NET-Prozess | Windows, macOS, Linux; Impress, Draw, Calc | auf der Zeichenfläche (Maus-/Tastatur-Handler, Overlay-Ebene) + Seitenleisten-Panel | geplant (Phase 3) |

**COM-Vorrang:** Das Manifest des Web-Add-ins deklariert `EquivalentAddins` mit der COM-ProgId
`InventaSketch.AddIn.Connect`; unter Windows blendet Office das Web-Add-in aus, solange das COM-Add-in installiert ist.
Beide zeigen nie doppelte Bedienelemente. Siehe [ADR-0004](../../adr/0004-com-precedence.md).

Unterschiede im Funktionsumfang (Freiform, Pfeilspitzen, Musterfüllung, Overlay, Metadaten) werden in der
[Host-Fähigkeitsmatrix](../hosts/capability-matrix.md) gepflegt.

## 4. Interaktionsmodell

### 4.1 Einfügen
- Bibliotheksbereich: Suche (deutsche und englische Namen und Schlagwörter), Kategorien, Favoriten, zuletzt verwendet,
  Miniaturansichten.
- Klick auf ein Element → der Umriss folgt dem Mauszeiger („Einfügen am Mauszeiger“) und rastet ein; ein Klick fügt in
  Standardgröße ein, Ziehen legt eine eigene Größe fest, `Esc` bricht ab. Drag-and-drop aus der Bibliothek funktioniert
  ebenfalls.
- Kein Assistent, kein Pflicht-Parameterdialog.

### 4.2 Direkte Manipulation
- Auswählen (Klick, Strg-/Umschalt-Klick, Auswahlrahmen: links→rechts = vollständig umschlossen, rechts→links =
  berührend).
- Verschieben mit Live-Einrasten; `Alt` setzt das Einrasten aus.
- **Eckgriffe skalieren proportional, Seitengriffe nicht proportional**; Drehgriff mit 15°-Schritten (`Shift`).
- Punktobjekte (Pfeile, Bezugslinien, Bemaßungen, Leitungen) zeigen End- und Kontrollpunktgriffe; Endpunkte docken an
  Ankerpunkte anderer Objekte an und folgen ihnen („Ziehen hält Verbindungen“).
- Tastatur: `Del`, `Ctrl+Z/Y`, `Ctrl+D` duplizieren, `R` um 90° drehen, `X`/`Y` spiegeln, Pfeiltasten verschieben
  schrittweise (`Shift` = 10×), `G` gruppieren, `Shift+F10` Kreismenü, `Esc` Auswahl aufheben.

### 4.3 Schwebende Kontextleiste
Erscheint neben der Auswahl mit den wichtigsten Aktionen (Ansicht/Darstellung, Spiegeln, Bezugszeichen, Linienstil,
Füllung/Schraffur, Pfeilspitzen). Nahe am Mauszeiger vollständig deckend, blendet mit zunehmender Entfernung aus und
verschwindet bis zur nächsten Auswahl; stiehlt nie den Fokus; abschaltbar.

### 4.4 Kreismenü
Rechtsklick (oder `Shift+F10`) öffnet ein radiales Menü mit genau den Befehlen für das angeklickte Objekt bzw. Teil.
Löschen liegt immer oben; nicht verfügbare Befehle sind ausgegraut (stabile Positionen); Umschalter zeigen ihren Zustand.
Beispiele: Zahnrad → Ansichten, Nabensymbol, kämmen mit …, Welle hinzufügen, Bezugszeichen; Welle → Wälzlager/Zahnrad
hinzufügen, hohl, verlängern; Verbinder → Linienführung, Pfeilspitzen, Beschriftung; Wand → Tür, Fenster, Dicke.

### 4.5 Vorschau-Editor in der Seitenleiste
Pflicht, wo Interaktion auf der Folie unmöglich ist (Office-Web-Add-in), und überall für komplexe Bearbeitungen
verfügbar: eine Live-Arbeitsfläche mit denselben Griffen, demselben Einrasten, Kreismenü und derselben schwebenden Leiste
wie auf der Folie; Eigenschaftenbereich für optionale Parameter und Stil; Übernahme auf die Folie beim Loslassen
(ein Rückgängig-Schritt pro Aktion).

### 4.6 Darstellungen (Ansichten)
Objekte können mehrere Darstellungen mit gemeinsamer Identität haben (z. B. Zahnrad: Symbol im Getriebeschema,
Seitenansicht, Schnitt, vereinfachte Axialansicht, vereinfachte Verzahnung). Beim Umschalten der Ansicht bleiben ID,
Position, Bezugszeichen und Verknüpfungen erhalten.

### 4.7 Einrasten
Priorität: semantisch (Zahneingriff auf den Teilkreisen inkl. Innenverzahnung im Hohlrad, Nabe auf Wellenachse,
fluchtende Wellen, konzentrische Mittelpunkte, elektrische Anschlüsse) → Punkte (Mittelpunkt, Ecken,
Kantenmittelpunkte, Enden) → Ausrichtungshilfslinien (gleiches x/y wie andere Objekte und die Seite) → Raster
(metrisch/zöllig). Für jedes Einrasten gibt es visuelle Hilfslinien.

### 4.8 Erscheinungsbild
Symbole in Overlays, schwebender Leiste und Kreismenü sind abstrakte, einheitliche Linien-Piktogramme (keine
verkleinerten Abbilder des Elements), damit sie auch klein erkennbar bleiben; die Bibliotheks-Galerien zeigen echte
Vorschaubilder.
Aufgeräumte, ruhige, moderne Oberfläche; helles/dunkles/automatisches Design (automatisch folgt Office/Betriebssystem);
deutsche Oberfläche, wenn die Host-Sprache mit `de` beginnt, sonst Englisch; manuelle Auswahl wird gespeichert;
kompaktes Layout für schmale Aufgabenbereiche; per Tastatur bedienbar; Tooltips mit Vorschau.

## 5. Dokumentmodell

Kanonisches, versioniertes JSON (`inventasketch-document` v1). Seiten mit Z-Reihenfolge; Objekte mit ID, Typ,
Transformation (Mittelpunkt, Drehung, Spiegelung), Boxgröße, optionalen Punkten + Anbindungen, Ansicht, optionalen
Parametern, Stil-Überschreibungen, Text, Bezugszeichen, Gruppen-ID, Ebene; Ebenen (inkl. Hilfsebenen, die vom
Patentexport ausgeschlossen sind); Bezugszeichen-Verzeichnis; Einstellungen (Einheiten, Raster, Profil). Einheiten mm,
y nach unten. Details: [Architekturüberblick](../architecture/overview.md).

In Host-Dokumenten reist das Modell mit den Formen: Tags je Gruppe (ID/Typ) plus komprimierte Modelldaten
(`isk1:` GZip+Base64), gespiegelt in einen Speicher auf Dokumentebene. Extern bearbeitete Formen werden erkannt; das
sichtbare Ergebnis wird nie verworfen.

## 6. Modulkatalog

Jedes Modul unterstützt zuerst Quick Sketch. „Ansichten“ nennt die Darstellungen; Parameter sind optional.

### 6.1 Grundlegendes Zeichnen & Beschriftung (`ann.*`) – in Arbeit
Linien, Polylinien, Pfeile (gerade, gekrümmt, gewinkelt, doppelt; Pfeilspitzen: Dreieck, offen, Stealth, Raute, Oval,
Strich, halb; Größen), Bezugslinien (gerade/gekrümmt/gewellt) mit Bezugszeichen, Textfelder, Beschriftungen, Legenden,
Positionsnummern-Kreise, eckige und geschweifte Klammern, Bemaßungen (linear, ausgerichtet, Winkel, Radius;
Schrägstriche oder Pfeile), Mittellinien, Bruchlinien, Schnittverläufe (A–A), Detailkreise, Koordinatensysteme,
schraffierte Bereiche (Stahl 45°, Kreuzschraffur, horizontal/vertikal, dicht/weit, gepunktet, Dämmung; Inseln werden
berücksichtigt), Flussdiagramm-Grundformen (Prozess, Verzweigung, Start/Ende, Daten, Dokument, Konnektor,
Schrittbezeichnungen „S100“).

### 6.2 Patent Studio (`patent.*`) – in Arbeit
Bezugszeichen mit Bezugslinien, Nummerierungsschema (10/20/30, Unterteile 11/12), eine einzige maßgebliche Quelle über
alle Figuren, Export der Bezugszeichenliste, Hilfe für Zeichnungsblatt und Nutzfläche, Blattnummern („1/3“),
FIG-Beschriftungen, monochromer Export, **Vorabprüfung (Preflight)** mit versionierten Amtsprofilen (DPMA, EPA, PCT,
USPTO, gemeinsames sicheres Profil), deren Regeln ihre Rechtsgrundlage zitieren (siehe
[Zeichnungsvorschriften für Patente](../../en/research/patent-drawing-rules.md)). Befunde: Fehler / Warnung / Hinweis /
manuell, mit Navigation zum Objekt und Vorschlägen. Eine fehlerfreie Vorabprüfung ist eine Hilfestellung, keine
rechtliche Bescheinigung.

Wichtige Fakten aus der Recherche (2026-11): Ränder oben 2,5 / links 2,5 / rechts 1,5 / unten 1,0 cm auf A4 bei allen
vier Ämtern; Nutzfläche max. 26,2 × 17,0 cm; Zeichenhöhe ≥ 0,32 cm; das EPA akzeptiert seit 2025-10-01 Farbe/Graustufen
bei elektronischer Einreichung (ABl. EPA 2025, A49); PCT-Regel 11.13 verlangt weiterhin Zeichnungen ohne Farbgebung;
USPTO 37 CFR 1.84(h) verbietet Mittellinien; die Blattnummerierung „1/3“ ist nur beim PCT Pflicht; eine einzelne Figur
erhält bei EPA/PCT/USPTO keine „FIG.“-Beschriftung; elektronische PDFs beim DPMA: schwarz-weiß, Schriften
Times/Helvetica/Courier/Symbol.

### 6.3 Mechanical Studio (`mech.*`) – in Arbeit
Freier Aufbau von Getrieben und Antrieben per Drag-and-drop, freiem Skalieren und semantischem Einrasten.
- **Zahnräder:** Stirnräder (außen-/innenverzahnt, d. h. Hohlrad), Andeutung der Schrägverzahnung, Kegelrad,
  Schnecke/Schneckenrad, Zahnstange, Planetensatz (Sonnenrad, Planeten, Hohlrad, Steg), Kegelraddifferenzial,
  mehrstufige Räderketten. Ansichten: Getriebeschema (Symbol „I“ im Stil von VDI 2127/ISO 3952 mit Nabensymbolen
  fest/verschiebbar/lose), Seitenansicht, Schnitt (schraffiert), vereinfachte Axialansicht (Kopfkreis voll, Teilkreis
  strichpunktiert, im Stil von ISO 2203), vereinfachte Verzahnung.
- **Wellen:** Vollwelle, Hohlwelle, abgesetzte Welle; Linien- oder Körperdarstellung; Achsanker zum Einrasten.
- **Lager:** Rillenkugellager, Schrägkugellager, Kegelrollenlager, Zylinderrollenlager, Nadellager, Axiallager,
  Gleitlager; Darstellung im Getriebeschema, vereinfacht (im Stil von ISO 8826) und detailliert; Symbole für
  Festlager/Loslager.
- **Kupplungen, Schaltkupplungen, Bremsen, Synchronisierungen, Kardangelenke, Gehäuse/Gestell.**
- **Verbindungselemente und Verbindungen:** Schrauben, Muttern, Unterlegscheiben, Niete, Passfedern, Zahnwellen
  (Keilwellen), Schweiß-, Klebe- und Lötverbindungen, Pressverband.
- **Federn, Nocken, Schwungräder, Riemen/Riemenscheiben, Ketten/Kettenräder, Linearführungen, Dichtungen.**
- **Antriebspfeile:** Drehung, Drehmoment, Leistungsfluss.
- Optionale technische Daten: Modul, Zähnezahl, Anzeige der Übersetzung, Vorschlag für den Achsabstand (d = m·z,
  a = m(z₁+z₂)/2), Drehrichtung. Deutlich gekennzeichnet als schematisch vs. näherungsweise vs. validiert.

### 6.4 Technische Mechanik (Startsatz `ann.mech*`, später Modul `mechanics.*`) – in Arbeit / geplant
Balken, Stäbe, Fachwerke, Fest-, Los- und Einspannlager, Einzel- und Streckenlasten, Momente, Schwerpunkt, Federn,
Dämpfer, Massen, Freischnittgrenzen, Koordinatensysteme, Grundelemente für Querkraft- und Momentenverläufe.

### 6.5 Kinematik & Mechanismen (`kin.*`) – geplant
Glieder, Dreh-/Schub-/Kugel-/Kurvengelenke, Viergelenk, Schubkurbel, Kreuzschleife, Kniehebel, Kurvengetriebe mit
Abtriebsglied; Simulation durch Ziehen mit einem 2D-Constraint-Solver; Gelenk- und Winkelgrenzen, die in die Zeichnung
eingetragen werden; Bewegungsbahnen und Geisterbilder (Start-/Zwischen-/Endlagen als gestrichelte Überlagerungen für
Patentfiguren); Export als Folienfolge für Animationen; Anzeige des Freiheitsgrads. Keine Dynamiksimulation impliziert.

### 6.6 Elektrotechnik & Elektronik (`elec.*`) – geplant
Unabhängig gezeichnete Symbole im IEC-Stil (Widerstand, Kondensator, Spule, Diode, LED, Transistor, MOSFET, IGBT,
Operationsverstärker, Gatter, Schalter, Relais, Sicherungen, Leitungsschutzschalter, FI-Schutzschalter, Quellen,
Batterien, Masse/PE, Motoren, Generatoren, Transformatoren, Sensoren, Aktoren, Steckverbinder, Steuergeräte);
Leitungen, die an Pins einrasten, Verbindungspunkte vs. Kreuzungen, Netzbezeichnungen, Busse, orthogonale/45°-/freie
Leitungsführung; einfacher ERC (offene Pins, nicht verbundene Netze). Hausinstallation: Steckdosen, Schalter, Leuchten,
Verteilung, Stromkreise auf einer Grundriss-Ebene; einpolige Übersichtsschaltpläne. Leiterplatten-Illustration:
Platinenkontur, Pads, Leiterbahnen, Durchkontaktierungen, Lagen, Bestückungsdruck (nur visuelle Planung).

### 6.7 Elektrische Maschinen & Antriebe (`emach.*`) – geplant
PMSM, EESM, Asynchronmaschine (ASM), geschaltete Reluktanzmaschine, Axialfluss- und Radialfluss-Anordnungen,
Innen-/Außenläufer; Stator-/Rotorquerschnitte mit Nuten, Wicklungen, Magneten, Luftspalt, Welle, Gehäuse, Kühlung;
konzentrisches Einrasten; Wechselrichter, Zwischenkreis, Batterie, Getriebe und Differenzial als E-Antriebsbaugruppen;
Pfeile für Fluss, Drehung und Drehmoment.

### 6.8 Automotive, ADAS & HMI (`auto.*`) – geplant
Fahrzeugsilhouetten (Pkw, SUV, Transporter, Lkw, Bus, Motorrad, Fahrrad, Anhänger; Drauf-/Seiten-/Front-/Heckansicht),
Teilsysteme (Achsen, Lenkung, Batterie, Antriebseinheiten), Menschen (stehend, gehend, sitzend, Hände, Blickrichtung),
Interaktionszonen innen/außen; Straßen und Fahrstreifen (gerade, gekrümmt, Kreuzungen, Kreisverkehre, Markierungen,
Verkehrszeichen, Lichtsignale, Schutzplanken), Trajektorien mit Zeitstempeln, Korridore, Sicherheitsabstände,
Konfliktpunkte, TTC-Marker; Sensoren (Kamera, Radar, Lidar, Ultraschall) mit bearbeitbaren, am Fahrzeug verankerten
Sichtfeldern, Detektionen (2D-/3D-Boxen, Labels, Konfidenz, Belegungsgitter); HMI: Kombiinstrument, Zentraldisplay,
HUD, Tasten, Drehsteller, Bildschirm-Wireframes und Bedienabläufe.

### 6.9 Architektur & Bauwesen (`arch.*`) – geplant
Wände mit ziehbaren Endpunkten und Dicke, Eckverbindungen, Räume aus geschlossenen Wandzügen mit Name/Fläche, in Wände
eingesetzte Türen und Fenster, Treppen, Stützen, Möbel, Sanitär-, Küchen-, Elektro- und HLK-Symbole, Bemaßungen,
Geschosse; optionaler Maßstab. Kein BIM.

### 6.10 Diagramme: UML, ER, Ablauf, Systeme (`diag.*`) – geplant
Flussdiagramme, Swimlanes, UML (Klassen-, Sequenz-, Aktivitäts-, Zustands-, Komponenten-, Verteilungs-,
Anwendungsfalldiagramm), ER (Entitäten, Attribute, Schlüssel, Kardinalitäten), Block-/Architekturdiagramme,
Zustandsautomaten, Datenfluss; Anschlüsse, orthogonale Linienführung mit Kreuzungsbögen, optionales automatisches
Layout, das manuelle Platzierungen nie stillschweigend zerstört.

### 6.11 Karten, Straßen & Routen (`map.*`) – geplant
Schematische Karten, Straßen, Routen und Wegpunkte, Bus-/Straßenbahn-/Bahnlinien und Haltestellen, Zonen und
Geofences; Import von GeoJSON/GPX/KML (lizenzbewusst); Maßstabsleiste.

### 6.12 Daten- & Ingenieurdiagramme (`chart.*`) – geplant
XY/Linie/Streuung/Balken/Fläche, Polar, Histogramme, Kennlinien (Drehmoment–Drehzahl, Wirkungsgradkennfelder),
Bode-Diagramme, Toleranzbänder und Grenzwerte; monochromtaugliche Reihenstile; Daten aus CSV/Excel; Vektorexport.

### 6.13 Weitere Fachmodule (vorgeschlagen)
Fluidtechnik & Verfahrenstechnik (Hydraulik/Pneumatik im Stil von ISO 1219, R&I-Fließschemata), Thermodynamik
(Kreisprozesse, Wärmeübertrager, T-s-/p-v-Diagramme), Aerodynamik (Profile, Stromlinien), Akustik/NVH, Tribologie &
Schmierung (Ölwege, Ölsümpfe, Dichtungen), Robotik (Arme, Koordinatensysteme, Arbeitsräume, Roboterzellen), Fertigung &
Produktion (Linien, Stationen, Wertstromdesign), Chemie & Biochemie (Apparaturen, Reaktionen, Stoffwechselwege),
Physik & Optik (Strahlen, Linsen, Felder, Wellen), Mathematik (Achsen, Funktionen, geometrische Konstruktionen),
Informatik & Netzwerke (Server, Busse, Topologien), Bauingenieurwesen & Infrastruktur (Brücken, Versorgungsleitungen,
Profile), Energiesysteme (Netz, PV, Wind, Wasserstoff), Medizinprodukte, Sicherheit & Zuverlässigkeit (FMEA,
Fehlerbäume), Systems Engineering (SysML-artige Blöcke, Anforderungsverfolgung), Logistik, Landwirtschaft, Schiffbau
sowie Luft- und Raumfahrt. Jedes davon wird zum Modul, sobald ein konkreter Umfang und Bedarf der Anwender bestehen.

## 7. Querschnittsfunktionen

- **Varianten & Zustände:** Stand der Technik vs. Erfindung, Betriebsmodi, vorher/nachher, A/B/C; Sichtbarkeit je
  Variante; visueller Vergleich (hinzugefügt/entfernt/geändert). Geplant.
- **Mehrfachansichten:** gemeinsame Identität über Ansichten hinweg (z. B. Zahnrad im Getriebeschema, in Seiten- und
  Axialansicht). Geplant.
- **Ebenen:** Mechanik, Elektrik, Sensorik, Bemaßungen, Bezugszeichen, Hilfselemente. In Arbeit (Modell).
- **Import & Erkennung:** SVG, PDF, PNG/JPEG, TIFF, DXF, CSV; Konturerkennung in Bildern, um geschlossene Bereiche zu
  schraffieren, unterstützte Vektorisierung und OCR von Bezugszeichen – stets bestätigbar, das Original bleibt erhalten.
  Geplant.
- **Fachexporte (erste Exporter am 20.11.2026 ausgeliefert: Stückliste CSV, Verbindungsgraph als Mermaid und GraphML,
  Elektro-Netzliste CSV, Bezugszeichenliste; Erweiterungspunkt `IExportProvider`; COM: Extras ▸ Daten exportieren;
  Web: Einstellungen ▸ Daten exportieren):** je nach Auswahl und Objekttypen den semantischen Inhalt in gängige
  Ingenieursformate exportieren, nicht nur als Grafik. Beispiele: Zahnradsätze und Getriebetopologien (Verzahnungs-
  datentabellen, Leistungsfluss-Topologie, REXS wo machbar), kinematische Ketten und Koppelgetriebe (Gelenk-/Glied-
  tabellen, URDF-ähnliche Beschreibungen), Schaltpläne und E/E-Verschaltung (Netzlisten z. B. KiCad/SPICE,
  Verbindungslisten, Leitungs-/Kabelbaumlisten, Ratsnest/Airwires für ungeroutete Verbindungen), Platinenskizzen
  (Bestückungslisten), Stücklisten (BOM als CSV/Excel mit Bezugszeichen, Benennung, Menge und optionalen Parametern),
  Bezugszeichenlisten sowie Ablauf-/Diagrammstrukturen (z. B. GraphML, Mermaid). Jeder Exporter ist ein
  Erweiterungspunkt der Module und gibt genau an, welche Semantik er erhält.
- **Export:** native Formen, SVG, PDF (Vektor), PNG/TIFF (300–600 dpi, 1-Bit-Option), Bezugszeichenliste.
  SVG im Kern ausgeliefert; übrige geplant.
- **Bibliotheken:** Benutzerbibliotheken, wiederverwendbare Baugruppen, Import/Export von Bibliothekspaketen. Geplant.

## 8. Nichtfunktionale Anforderungen

- Flüssige Interaktion mit 1.000 einfachen Objekten; deterministischer Export; kein Datenverlust beim Speichern und
  erneuten Öffnen.
- Sicherer SVG-Import (Bereinigung), keine Netzwerkabhängigkeit beim Bearbeiten.
- Zielwert ≥ 80 % Testabdeckung für kritische Fachlogik; Host-Adapter mit Fakes getestet plus dokumentierte manuelle
  Prüfungen.
- Barrierefreiheit: vollständige Tastaturbedienung der wichtigsten Arbeitsabläufe, sichtbarer Fokus, ausreichender
  Kontrast.

## 9. Abnahme-Arbeitsabläufe (Referenz)

1. Generische Formen einfügen, verschieben, drehen, in der Größe ändern; nativ formatieren.
2. Getriebe: Wellen, zwei Zahnradpaare, Wälzlager, Drehmomentpfeile – ohne Parametereingabe; Umschalten auf Axial-/
   Schnittansichten.
3. Freikörperbild: Balken, Lager, Lasten, Schwerpunkt.
4. Viergelenkgetriebe mit Winkelgrenzen, animiert; Geisterbild-Lagen als statische Figur.
5. Schaltung mit an Pins angebundenen Leitungen; Verbindungspunkte vs. Kreuzungen.
6. Querschnitt eines Elektromotors mit Rotor, Stator, Luftspalt.
7. Fahrzeug, Straße, Sensorsichtfeld, Trajektorie.
8. Grundriss eines Hauses mit Wänden, Räumen, Türen, Fenstern.
9. Prozessdiagramm mit Swimlanes.
10. Patentfigur: Bezugszeichen, Schraffur, Vorabprüfung, monochromer Export.
11. Native Objekte + SVG/PDF exportieren; erneut öffnen und prüfen.

## 10. Nicht im Umfang

3D-CAD, FEM/CFD, Mehrkörperdynamik, fertigungstaugliche Leiterplattenausgabe, BIM, Topologie in GIS-Qualität,
vollständige Navigation, rechtliche Zertifizierung von Patentzeichnungen.

## 11. Quellen

Siehe [research/patent-drawing-rules.md](../../en/research/patent-drawing-rules.md) (amtliche Quellen der Patentämter)
und [research/tools-and-host-apis.md](../../en/research/tools-and-host-apis.md) (Werkzeuglandschaft, Host-APIs,
Lizenzen); beide nur auf Englisch, deutsche Kurzfassung: [research/README.md](../research/README.md).
