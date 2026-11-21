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
Dokumenteigenschaften; COM-Vorrang verifiziert. Status: **weitgehend fertig** (20.11.2026, alles in PowerPoint 16.0 geprüft): natives Einfügen,
Einfügen am Mauszeiger, semantisches Fangen (Verschieben, Skalieren um die Mitte, Drehen), Zweipunkt-Werkzeug,
Punktbearbeitung und Rahmenauswahl von Punkten, Verankern, Kreismenü (8 Einträge, Einfügen/Typ/Beschriftung/Mehr) und
schwebende Leiste mit abstrakten Symbolen, Text bearbeiten, Menüband wie Slidemax (Struktur, Werkzeuge, Beschriftung,
Farbe, Text, Layout, Elemente, Extras), native Formatierung im Modell, Schwarz-Weiß, Datenexporte,
Seitenleisten-Editor mit Live-Synchronisation, Installationspaket. Offen: Prüfung im dunklen Office, COM-Vorrang vor
dem Web-Add-in (`EquivalentAddins`) ungetestet.

## Phase 3 – LibreOffice-Erweiterung

Python-UNO-`.oxt` + .NET-Engine-Prozess; Impress/Draw/Calc; native Formen und Schraffuren; Seitenleisten-Panel;
Interaktion auf der Zeichenfläche; Pakete für Windows/Linux/macOS. Status: **in Arbeit** (Phase 1: Einfügen nativer
Formen aus dem Engine-Prozess; aus dem LibreOffice-Host von Slidemax übernommen). Modellaustausch mit dem
PowerPoint-COM-Add-in über den Dokumentspiegel `ISK_MODEL_<id>`/`ISK_BOUNDS_<id>` am 2026-11-20 in beide Richtungen
verifiziert (.pptx); Einheiten des Web-Add-ins (Präsentations-Tags) werden noch nicht ausgetauscht.

## Phase 4 – Kinematik und erweiterte Mechanik

2D-Constraint-Solver, Koppelgetriebe, Gelenkgrenzen, Simulation durch Ziehen, Geisterbilder (Ghosting), Animation als
Folienfolge; Modul Technische Mechanik; verknüpfte Mehrfachansichten. Status: **in Arbeit** – Kinematik-Modul am
20.11.2026 ausgeliefert (Gelenke, Glieder, Mechanismen mit geschlossener Schleifenlösung, Zwischenlagen, Bahnen,
Freiheitsgrad); Animation als Folienfolge mit Morph am 20.11.2026 erledigt (COM); offen: interaktives Ziehen zur Simulation, Animation im Web-Add-in.

## Phase 5 – Elektrotechnik, Elektronik, elektrische Maschinen

Symbole, Leitungen mit Pins/Verbindungspunkten, Netze, Busse, ERC-Grundlagen, Hausinstallation, Leiterplatten-Illustration,
Maschinenquerschnitte (PMSM, EESM, ASM, SRM, Axial-/Radialfluss). Status: **weitgehend fertig** – Elektro- und
Maschinen-Modul am 20.11.2026 ausgeliefert, inkl. Netzlisten-Export und einer Grundprüfung (offene Pins, lose Leitungsenden, Kurzschlüsse).

## Phase 6 – Systeme und Erweiterung der Fachgebiete

Automotive/ADAS/HMI, Architektur/Grundrisse, UML/ER/Ablauf, Karten und Routen, Diagramme; weitere Module nach Bedarf
(Fluidtechnik, Thermodynamik, Robotik, Chemie, …). Status: **in Arbeit** – Fahrzeuge/ADAS/HMI, Architektur und
Diagramme, Karten und Routen (15 Typen, Export der Routenhalte) sowie Fluidtechnik (36 Typen, nach ISO 1219,
Export der Verbindungsliste) am 20.11.2026 ausgeliefert; Diagramme/Charts,
Thermodynamik, Robotik, Chemie vorgeschlagen.

## Phase 7 – Erweiterte Plattform

Varianten und Vergleich (Diff), Synchronisation mehrerer Ansichten, Konturerkennung in Bildern und Schraffur
importierter Bilder, PDF/TIFF-Exportprofile, Benutzerbibliotheken, Automatisierung. Status: **vorgeschlagen**.

Fachexporte je nach Auswahl/Typ (erste Exporter am 20.11.2026: Stückliste CSV, Verbindungsgraph Mermaid/GraphML,
Netzliste CSV, Bericht der elektrischen Regelprüfung, Bezugszeichenliste; als Nächstes): Zahnrad- und Getriebetopologiedaten (REXS wo machbar), kinematische
Ketten, Netz- und Verbindungslisten für Schaltpläne und E/E-Verschaltung (KiCad/SPICE), Ratsnest/Airwires,
Kabelbaumlisten, Stücklisten (CSV/Excel), Bezugszeichenlisten, Diagrammstrukturen (GraphML/Mermaid) – als
Export-Erweiterungspunkte der Module.

## Offene Punkte

- **Stand 20.11.2026:** Engine-Kern, Engine-API und zwölf Module auf `main` (zuletzt Karten und Routen sowie
  Fluidtechnik; 826 Bibliothekselemente in 70 Kategorien, in PowerPoint geprüft).
- Core-Wünsche aus den Modulen Karten und Fluidtechnik (20.11.2026): gemeinsame Helfer für Anschluss-Routing/`ConnectToPort` (Elektro und Fluid doppeln sie) und
  Freiformen mit mehreren Teilpfaden; Grenzen mehrzeiliger Texte in `Prim.Bounds`; der gewählte Punkt für „Wegpunkt entfernen“. Erledigt am 20.11.2026:
  `IconIds.Pin`, `Medium`, `Actuation` (Piktogramme in COM und Web); `ObjectType.IsPart` (die Stückliste lässt
  Kartensymbole und Diagrammformen aus); `ObjectType.OnParamChanged` mit `Registry.SetParam` (Engine `setParam`,
  COM-Beschriftungsmenü; Fluid-Ventile behalten ihren Zeichnungsmaßstab). Erledigt am 21.11.2026: Leitungsabzweige – ein auf die Schiene einer
  anderen Leitung gezogenes Leitungsende hängt daran (`Attachments.For`, Schienen-Ids aus `Registry.PageAnchors`), folgt
  ihr (auch über COM-Einheiten hinweg), und Netzliste sowie Fluid-Verbindungsliste zählen es.
- UI-Symbole (Vorgabe des Maintainers, 20.11.2026): Symbole in Overlays, schwebender Leiste und Kreismenü sind abstrakte,
  saubere Piktogramme (ein einheitlicher Linien-Symbolsatz, z. B. „Strich-Schema“, „Schnitt“, „Stirnansicht“, „Spiegeln“),
  keine verkleinerten Abbilder des konkreten Elements, damit sie auch klein erkennbar bleiben. Module benennen je Ansicht/
  Aktion eine Symbol-ID (`ViewDef`/`ContextAction`); die Hosts zeichnen sie aus dem gemeinsamen Symbolsatz. Die
  Bibliotheks-Galerien dürfen echte Vorschaubilder behalten.
- Reihenfolge mit dem Maintainer abgestimmt (20.11.2026): erst das COM-Add-in fertigstellen, dann das Web-Add-in, dann LibreOffice.
- Web: Webspace-Paket (`scripts/package-webspace.ps1` → `dist/InventaSketch-Webspace` + ZIP): am 20.11.2026 erledigt –
  Einrichtungsseite erzeugt das Manifest für die eigene HTTPS-Adresse, `.htaccess`/`web.config`/robots, Downloads der
  vorhandenen anderen Fassungen. Offen: Test auf echtem Apache-/IIS-Webspace und Laden des Add-ins von dort.
- Wünsche an Core/Engine aus der Modularbeit: am 20.11.2026 erledigt (Rails, Vorrang für Befestigungs- und Pin-Fang,
  konzentrische Passung, Anbindung über Pin-ID; hohle Spitzen und Krähenfüße, Unterstreichung; Aktionen mit Eingabe und
  Dokumentzugriff, Änderungsmengen mit abhängigen Objekten, Planmaßstab, Größen je Ansicht; gemeinsamer
  AttachPoint/ReversePoints; ADR-0009). Nacharbeiten:
  - COM: native Verschiebungen (PowerPoint-Ziehpunkte, Pfeiltasten) zeichnen verankerte und abhängige Einheiten jetzt neu
    (20.11.2026); der Planmaßstab hat auf der Folie kein Bedienelement (nur Seitenleiste);
    Dokumentaktionen ändern nur die eigenen Objekte der Einheit; die schwebende Leiste zeigt bis zu drei wichtige Typaktionen mit
    aussagekräftigem Piktogramm (der Rest im Kreismenü).
  - Web: am 20.11.2026 erledigt – `combridge.ts` zeichnet nur die von `changed`/`dependents` betroffenen Einheiten neu;
    der Methodenvertrag in der Web-Host-Doku deckt `setPlanScale`, `input`, `changed`, `dependents`, Umwandlungen und
    Exporte ab.
  - Module: am 20.11.2026 erledigt – Ansichtswechsel nehmen die Proportionen aus den Bibliotheksvorlagen
    (Registry.WithView), Unterstreichungen der Beschriftung nutzen `TextStyle.Underline`, Kennzeichen/Wert der
    Elektro-Symbole sind Eingabeaktionen.
  - In PowerPoint am 20.11.2026 geprüft: gezeichnete Spitzen, Unterstreichung, Eingabedialog, abhängiges Neuzeichnen von
    Wänden mit einem Rückgängig-Schritt.
- Web-Installer (aus Slidemax übernommen, am 20.11.2026 ins Englische übersetzt, Meldungen deutsch/englisch): echte
  Installation und Laden des Add-ins in PowerPoint (Windows ohne COM-Add-in, Mac, Web) stehen aus.
- UX-Rückmeldungen des Maintainers (20.11.2026), erst COM, dann Web:
  - Erledigt am 20.11.2026 (COM, wo in der Host-Doku vermerkt in PowerPoint geprüft): Schwarz-Weiß als umkehrbare Dokumenteinstellung (Füllungen, Text, Markierungen); Menüband-Reihenfolge
    (Beschriftung = Beschriftung + Patent + Grundformen direkt nach Werkzeuge, kleiner Info-Button); transparente
    Menüband-Vorschaubilder für dunkles Office; Seitenleisten-Vorschau entfernt auf der Folie gelöschte Einheiten;
    Text bearbeiten per Doppelklick und Leistenknopf; Ansichtswechsel behält Proportionen; Größe ändern um die Mitte bei
    zentrierten Teilen; Kreismenü „Einfügen ▸“ mit passenden Elementen zur Auswahl (Welle → Zahnräder und Lager,
    Leitung → Schaltzeichen, immer Beschriftung/Pfeile/Bezugszeichen), am Mauszeiger mit Fangen platziert; Schalter
    „Verankern“ neben „Einrasten“: angebundene Linienenden folgen verschobenen Einheiten, auch einheitenübergreifend.
  - Skalieren von Schemadarstellungen: erledigt (20.11.2026). Nabensymbole der Zahnräder behalten ihre Größe;
    Gelenke in Mechanismen sind bereits auf 1,4 mm begrenzt; Einzelsymbole (Gelenke, Lager, Schaltzeichen) skalieren
    bewusst als Ganzes.
  - Web-Host: dieselbe UX am 20.11.2026 erledigt (Kreismenü-Layout, Text bearbeiten, Einfügen ▸ über `suggestions`,
    Schwarz-Weiß über `setMonochrome`, Skalieren um die Mitte). Typ ▸ für Linientypen (Registry.Convert, Engine
    conversions/convert) und ausgeblendete Module in der Seitenleisten-Bibliothek ebenfalls erledigt. Beschriftung ▸ im Web-Kreismenü und die Einstellung Verankern erledigt 2026-11-20.
  - Menüband-Gruppen wie Slidemax: Struktur, Farbe, Text, Layout: am 20.11.2026 erledigt (native Formatierung wird ins Modell übernommen).
  - Werkzeuge-Menü wie Slidemax (Extras: Symbol, Dezimaltrennzeichen, Korrektursprache, Folien senden/speichern) und
    Ausblenden von Modulen im Menüband und in der Seitenleisten-Bibliothek: am 20.11.2026 erledigt.
  - Rahmenauswahl einzelner Punkte (Alt + Ziehen neben einer ausgewählten Linie): am 20.11.2026 in COM erledigt.
  - Mehr Bearbeitungseinträge in der schwebenden Leiste (Text, wichtige Parameter), dabei kurz halten: am 20.11.2026 im COM-Add-in erledigt.
- UX-Runde 21.11.2026 (Maintainer): Schnell-Anfügen-Pfeile und Ziehen aus Anschlüssen für Diagramme mit Palette
  „nächstes Element“ (`Flow`), mehr vom Kreismenü sichtbar in der schwebenden Leiste (wichtige Aktionen, Einfügen-Palette),
  Menüband-Gruppe Beschriftung in zwei Spalten, mechanische Kopplung (`Coupling`: Zahnräder bleiben im Eingriff, wenn sich
  Achsabstand oder ein Rad ändert, Wellen tragen ihre Räder) – in Core, Engine und COM erledigt. Als Nächstes: Kopplung für
  Seiten-/Schemaansichten von Zahnrädern (Teilkreislinien), Riemen und Ketten, Antriebsstränge und Räder; Schnell-Anfügen
  und Kopplung auf der Web-Bühne; freies Verschieben mit Alt im Web-Host.
- Prüfen, welcher Office.js-Metadatenspeicher Kopieren/Einfügen und erneutes Öffnen unter Windows/Mac/Web übersteht (ADR-0006).
- Den (Vektor-)PDF-Writer für den Export auf Engine-Seite festlegen.
