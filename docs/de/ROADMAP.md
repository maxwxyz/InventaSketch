# Roadmap

> Deutsche Übersetzung. Maßgeblich ist die englische Fassung: [ROADMAP.md](../../ROADMAP.md).

> Statuswerte: **ausgeliefert** (getestet und dokumentiert), **in Arbeit**, **geplant**, **vorgeschlagen**, **blockiert**.
> Detaillierter Umfang je Modul: [Spezifikation](product/specification.md). Englisch: [ROADMAP.md](../../ROADMAP.md).

## Phase 0 – Grundlagen (in Arbeit)

| Punkt | Ergebnis | Status |
|---|---|---|
| Recherche | Zeichnungsvorschriften für Patente (DPMA/EPA/PCT/USPTO), Werkzeuglandschaft, Host-APIs | ausgeliefert ([docs/en/research](../en/research/); deutsche Kurzfassung: [research/README.md](research/README.md)) |
| Repository & Governance | README EN/DE, Lizenz, ADRs, Regeln für Beiträge, CI | in Arbeit |
| Engine-Kern | Modell, Geometrie, Primitive, Einrasten, Schraffur, Bearbeitungen/Rückgängig, JSON, SVG | in Arbeit (26 Tests) |
| Modulvertrag | `IModule`, `ObjectType`, Ansichten, Parameter, Ankerpunkte, Aktionen, Bibliothek | in Arbeit |

## Phase 1 – Quick-Sketch-MVP

| Punkt | Ergebnis | Status |
|---|---|---|
| Annotation-Modul | Pfeile, Bezugslinien, Bemaßungen, Text, Schraffur, Klammern, Mechanik-Symbole, Flussdiagramm-Grundformen | in Arbeit |
| Mechanical-Modul | Zahnräder (Stirnrad/Hohlrad/Kegelrad/Schnecke/Zahnstange/Planetensatz/Differenzial), Wellen, Wälzlager, Kupplungen, Verbindungselemente, Federn; Ansichten Getriebeschema/Seite/Schnitt/axial/Verzahnung | in Arbeit |
| Patent-Modul | Bezugszeichen, Nummerierungsschema, Bezugszeichenliste, Amtsprofile, Vorabprüfung (Preflight), monochromer Export | in Arbeit |
| Engine-API | JSON-Fassade, Sitzungen, Treffertest, Einrasten, Kontextaktionen, Host-Zeichenpläne | in Arbeit |
| Web-Add-in | WebAssembly-Engine, Vorschau-Editor in der Seitenleiste, Bibliothek, Eigenschaftenbereich, Kreismenü, schwebende Leiste, Schreiber für native Formen | in Arbeit |

**Abschlusskriterien:** Ein zweistufiges Getriebe und eine Patentfigur lassen sich im Seitenleisten-Editor ohne
Zahleneingabe erstellen, in PowerPoint (Web) einfügen und als SVG exportieren; alle Tests grün.

## Phase 2 – PowerPoint-COM-Add-in (Windows)

Registrierung pro Benutzer ohne Administratorrechte; Menüband; native Formen über COM (Freiform, Pfeilspitzen,
Musterfüllung, Verbinder); Interaktion auf der Folie (Eingabe-Hooks + geschichtetes Overlay-Fenster): Einfügen am
Mauszeiger, Einrast-Hilfslinien, schwebende Leiste, Kreismenü; Aufgabenbereich in der Seitenleiste; Metadaten in Tags +
Dokumenteigenschaften; COM-Vorrang verifiziert. Status: **in Arbeit** – Stufe 1 am 2026-11-20 ausgeliefert:
Registrierung pro Benutzer, Menüband, natives Einfügen (Formen, Freiformen, Pfeilspitzen, Gruppen, Tags) in
PowerPoint 16.0 geprüft; Stufe 2: Einfügen am Mauszeiger, Ziehen mit semantischem Fangen, Kreismenü, ausblendende
Leiste, Skalieren mit Fangen (Zahneingriff), Zweipunkt-Werkzeug, Punktbearbeitung, Typumwandlung,
Beschriftungsmenü und Drehen mit Fangen geprüft; Leiste und Kreismenü folgen dem Office-Design. Seitenleisten-Editor
(WebView2 mit Live-Synchronisation) geprüft. Menüband mit Vorschau-Galerien je Kategorie, Einstellungsschalter,
abstrakte Piktogramme in Leiste und Kreismenü, Tastaturkürzel, eindeutige IDs für Kopien, Installationspaket
(`scripts\package-com.ps1`). Deutsche Host-Dokumentation ergänzt. Als Nächstes: Übersetzung des übernommenen Web-Hosts,
LibreOffice-Phase.

## Phase 3 – LibreOffice-Erweiterung

Python-UNO-`.oxt` + .NET-Engine-Prozess; Impress/Draw/Calc; native Formen und Schraffuren; Seitenleisten-Panel;
Interaktion auf der Zeichenfläche; Pakete für Windows/Linux/macOS. Status: **geplant**.

## Phase 4 – Kinematik und erweiterte Mechanik

2D-Constraint-Solver, Koppelgetriebe, Gelenkgrenzen, Simulation durch Ziehen, Geisterbilder (Ghosting), Animation als
Folienfolge; Modul Technische Mechanik; verknüpfte Mehrfachansichten. Status: **geplant**.

## Phase 5 – Elektrotechnik, Elektronik, elektrische Maschinen

Symbole, Leitungen mit Pins/Verbindungspunkten, Netze, Busse, ERC-Grundlagen, Hausinstallation, Leiterplatten-Illustration,
Maschinenquerschnitte (PMSM, EESM, ASM, SRM, Axial-/Radialfluss). Status: **geplant**.

## Phase 6 – Systeme und Erweiterung der Fachgebiete

Automotive/ADAS/HMI, Architektur/Grundrisse, UML/ER/Ablauf, Karten und Routen, Diagramme; weitere Module nach Bedarf
(Fluidtechnik, Thermodynamik, Robotik, Chemie, …). Status: **geplant / vorgeschlagen**.

## Phase 7 – Erweiterte Plattform

Varianten und Vergleich (Diff), Synchronisation mehrerer Ansichten, Konturerkennung in Bildern und Schraffur
importierter Bilder, PDF/TIFF-Exportprofile, Benutzerbibliotheken, Automatisierung. Status: **vorgeschlagen**.

Fachexporte je nach Auswahl/Typ (vorgeschlagen): Zahnrad- und Getriebetopologiedaten (REXS wo machbar), kinematische
Ketten, Netz- und Verbindungslisten für Schaltpläne und E/E-Verschaltung (KiCad/SPICE), Ratsnest/Airwires,
Kabelbaumlisten, Stücklisten (CSV/Excel), Bezugszeichenlisten, Diagrammstrukturen (GraphML/Mermaid) – als
Export-Erweiterungspunkte der Module.

## Offene Punkte

- UI-Symbole (Vorgabe des Maintainers, 20.11.2026): Symbole in Overlays, schwebender Leiste und Kreismenü sind abstrakte,
  saubere Piktogramme (ein einheitlicher Linien-Symbolsatz, z. B. „Strich-Schema“, „Schnitt“, „Stirnansicht“, „Spiegeln“),
  keine verkleinerten Abbilder des konkreten Elements, damit sie auch klein erkennbar bleiben. Module benennen je Ansicht/
  Aktion eine Symbol-ID (`ViewDef`/`ContextAction`); die Hosts zeichnen sie aus dem gemeinsamen Symbolsatz. Die
  Bibliotheks-Galerien dürfen echte Vorschaubilder behalten.
- Reihenfolge mit dem Maintainer abgestimmt (20.11.2026): erst das COM-Add-in fertigstellen, dann das Web-Add-in, dann LibreOffice.
- Web: Webspace-Paket wie Slidemax-Webspace (`dist/InventaSketch-Webspace`): den Ordner auf einen beliebigen HTTPS-Webspace
  legen; enthält das Web-Add-in, eine Einrichtungsseite (`index.html`), die das Manifest für die eigene Adresse erzeugt,
  `.htaccess`/`web.config` (Dateitypen der Engine, keine Indizierung) und Downloads der anderen Fassungen (COM-Add-in-ZIP,
  LibreOffice-`.oxt`) laut `downloads.json`.
- Wünsche an Core/Engine aus der Modularbeit: am 20.11.2026 erledigt (Rails, Vorrang für Befestigungs- und Pin-Fang,
  konzentrische Passung, Anbindung über Pin-ID; hohle Spitzen und Krähenfüße, Unterstreichung; Aktionen mit Eingabe und
  Dokumentzugriff, Änderungsmengen mit abhängigen Objekten, Planmaßstab, Größen je Ansicht; gemeinsamer
  AttachPoint/ReversePoints; ADR-0009). Nacharbeiten:
  - COM: mit PowerPoint-eigenen Ziehpunkten oder Pfeiltasten verschobene Einheiten zeichnen abhängige Einheiten erst bei
    der nächsten InventaSketch-Bearbeitung neu; der Planmaßstab hat auf der Folie kein Bedienelement (nur Seitenleiste);
    Dokumentaktionen ändern nur die eigenen Objekte der Einheit; die schwebende Leiste zeigt keine Typaktionen
    (Eingabeaktionen im Kreismenü).
  - Web: `combridge.ts` nutzt `changed`/`dependents` noch nicht; Methodenvertrag in der Web-Host-Doku ergänzen
    (`setPlanScale`, `input`, `changed`, `dependents`).
  - Module: `ViewDef.Size` nutzen, wo Ansichten im Seitenverhältnis abweichen (Fahrzeug Drauf-/Seitenansicht);
    Beschriftungs-Text- und Bezugslinientypen zeichnen noch eigene Unterstreichungslinien (auf `TextStyle.Underline`
    umstellen); Kennzeichen/Wert der Elektro-Symbole als Eingabeaktionen.
  - Noch nicht in echtem PowerPoint geprüft: Eingabedialog und abhängiges Neuzeichnen von Wänden (gezeichnete Spitzen und
    Unterstreichung am 20.11.2026 geprüft).
- Web-Installer (aus Slidemax übernommen, am 20.11.2026 ins Englische übersetzt, Meldungen deutsch/englisch): echte
  Installation und Laden des Add-ins in PowerPoint (Windows ohne COM-Add-in, Mac, Web) stehen aus.
- Prüfen, welcher Office.js-Metadatenspeicher Kopieren/Einfügen und erneutes Öffnen unter Windows/Mac/Web übersteht (ADR-0006).
- Den (Vektor-)PDF-Writer für den Export auf Engine-Seite festlegen.
