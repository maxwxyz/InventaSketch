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
Seitenleisten-Editor mit Live-Synchronisation, Installationspaket. Dunkles Office: Leiste, Kreismenü und
Piktogramme in beiden Designs gerendert und geprüft (21.11.2026). Offen: COM-Vorrang vor dem Web-Add-in
(`EquivalentAddins`) ungetestet (braucht das mit dem Zertifikat des Maintainers installierte Web-Add-in).

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

## Plan 21.11.2026: Interaktion, Module und Elemente

Mit dem Maintainer nach der COM-Feinschliff-Runde vereinbart. Prioritäten: **P1** als Nächstes, **P2** danach, **P3**
später. Jeder Punkt kommt zuerst in Core/Engine, dann COM, dann Web, dann LibreOffice (Host-Reihenfolge unverändert).

### Interaktionsmechanismen und UX-Konzepte

Stand 21.11.2026 (COM): Die einheitliche Liniensprache (verlängern, weiterzeichnen, Ecken, Abzweige inkl. Wand-T-Stöße,
teilen, verbinden), Bau-Griffe für Zahnräder, Wellen und Maschinen (inkl. Welle strecken) und freies Andocken von
Beschriftungen auf Flächen sind erledigt und in PowerPoint geprüft; Engine/Web folgen. Als Nächstes: Kreuzungen von
Leitungen und Rohren (automatische Brücke, Verbinden-Symbol an der Kreuzung), danach die Prüfung von Ankern, Kontakten,
Randbedingungen und Verbindungen je Element und Ansicht.

- **P1 – Eine Liniensprache für alles Linienartige.** Was Leitungen schon können (Ecken, Abzweige), geht überall, wo es
  sinnvoll ist, mit denselben Griffen und Regeln:
  - *Verlängern / Weiterzeichnen:* ein „+“ an jedem freien Ende; Ziehen verlängert die Linie, Klick zeichnet weiter
    (Klick-Klick setzt Ecken, Esc oder Doppelklick beendet, Fangen und Andocken wie gewohnt). Leitungen, Rohre, Straßen
    (Karten und Fahrzeug), Flüsse, Bahnlinien, Routen, Trajektorien, Polylinien, Wände, Schnittlinien.
  - *Abzweigen:* Leitungen, Rohre, Straßen, Bahnlinien (für Leitungen, Rohre, Kartenstraßen erledigt); Wände bekommen
    T-Stöße (eine abzweigende Wand schließt an die Wand an, an der sie beginnt); Riemen und Ketten eine dritte Scheibe
    (umschlingen).
  - *Ecken:* einfügen, ziehen, entfernen für Wände (eine Ecke = zwei verbundene Wände), Schraffurflächen,
    Kartenflächen, Fahrstreifen.
  - *Wellen:* Ende ziehen streckt (Teile bleiben an ihrem Platz), „+“ am Ende setzt den nächsten Wellenabschnitt
    (abgesetzte Welle, Kupplung oder Lager am Stoß); Welle teilen; ein Zahnrad am Wellenende verlängert sie.
  - *Verbinden und Teilen:* ein Ende auf ein Ende derselben Art ziehen verbindet beide Linien; eine Linie an einer Ecke
    teilen (Schere in der Leiste).
- **P1 – Live-Vorschau der Kopplung beim Ziehen:** Eingriffspartner skalieren und Riemen strecken sich schon im
  Geisterbild (heute erst beim Loslassen); mitlaufende Teile werden hervorgehoben.
- **P1 – Einfügen per Tippen:** ein Suchfeld am Mauszeiger (Strg+Leertaste oder Leistenknopf), das das gewählte Element
  am Zeiger in der Ansicht der Auswahl einfügt; zuletzt benutzte und Favoriten zuerst.
- **P2 – Baugruppen als Bibliothekselemente:** Auswahl (mit Kopplungen, Bezugszeichen, Ansichten) als eigenes
  Bibliothekselement speichern; Start-Baugruppen: zweistufiges Getriebe, Planetenstufe, E-Antrieb (Batterie –
  Wechselrichter – E-Maschine – Getriebe – Räder), Riementrieb, Hydraulikzylinder-Kreis, Raum mit Tür und Fenster,
  Kreuzung mit Fahrstreifen.
- **P2 – Mehransichten-Verknüpfung:** dasselbe Teil in Schema-, Seiten- und Stirnansicht auf verschiedenen Folien oder
  Einheiten teilt seine Identität; Zähnezahl oder Größe in einer Ansicht ändert die anderen mit.
- **P2 – Ziehen zum Simulieren:** einen Antrieb (Kurbel, Welle, Zahnrad) auf der Folie ziehen, und Getriebe bzw.
  Koppelgetriebe bewegen sich mit der richtigen Übersetzung; Stellungen als Geisterbilder oder Morph-Folien festhalten
  (COM-Animation vorhanden).
- **P2 – Fang-Beschriftungen und Maße:** kleines Etikett am Fangziel („Teilkreis“, „Achse“, „Pin 2“) und Live-Abstand
  und -Winkel zu Nachbarn beim Ziehen (wie in Design-Werkzeugen).
- **P2 – Format übertragen für Semantik:** Ansicht, Parameter und Stil von einem Element auf andere desselben Typs
  übertragen.
- **P2 – Operationen über die Auswahl:** bei mehreren Einheiten: Mitten auf einer Achse ausrichten, gleiche Abstände
  entlang einer Linie, gleiche Größe, Bezugszeichen in Leserichtung neu nummerieren, alle Verbindungen lösen/koppeln.
- **P2 – Tastatur:** Tab springt durch verbundene Elemente, Pfeiltasten verschieben mit Kopplung, Umschalt+Klick auf
  ein Schloss löst alle Verbindungen der Auswahl.
- **P3 – Varianten und Zustände** (Spezifikation §7): vorher/nachher, Betriebszustände; Sichtbarkeit je Variante; Folie
  je Variante.
- **P3 – Automatisches Layout und Routing für Diagramme:** Schichtenlayout, das manuelle Platzierung respektiert;
  Verbinder, die Formen ausweichen; Brücken an Kreuzungen.
- **P3 – Patent-Ablauf in den Hosts:** Nummerierungsassistent über Figuren, Preflight-Bereich mit Sprung zum Befund
  (Preflight-Regeln gibt es im Patentmodul), Blattlayout für Figuren.
- **P3 – Import:** SVG/DXF-Umrisse als schraffierbare Flächen, GeoJSON/GPX für Karten, CSV für Diagramme.

### Neue Module

- **P1 – Diagramme/Kennlinien (`chart.*`, Spezifikation §6.12):** XY/Linie/Punkte/Balken, Kennlinien
  (Drehmoment–Drehzahl, Wirkungsgradkennfeld), Bode-Diagramm, Toleranzbänder; Daten eintippen oder aus Excel einfügen;
  schwarz-weiß-tauglich.
- **P2 – Verfahrens- und Wärmetechnik (`process.*`):** R&I-Fließbild über die Fluidtechnik hinaus (Behälter, Kolonnen,
  Wärmetauscher, Pumpen, Messstellen mit Kennzeichnung), thermodynamische Kreisprozesse (T-s-, p-v-, p-h-Diagramme),
  Klimatechnik.
- **P2 – Energiesysteme (`energy.*`):** PV, Wind, Batteriespeicher, Ladegerät/Wallbox, Netzanschluss, Wasserstoff,
  Übersichtsschaltpläne über Spannungsebenen.
- **P2 – Robotik (`robot.*`):** Arme aus Kinematik-Gelenken, Koordinatensysteme, Greifer, Arbeitszellen,
  Reichweitenhüllen, Schutzbereiche.
- **P3 – Netzwerke & IT (`net.*`):** Server, Cloud, Router, Switches, Topologien, C4-Kontext-/Container-Diagramme.
- **P3 – Optik & Physik (`optics.*`):** Strahlen, Linsen, Spiegel, Prismen, Wellen, Felder.
- **P3 – Zuverlässigkeit & Systems Engineering:** Fehlerbäume, FMEA-Blöcke, SysML-artige Blöcke und
  Anforderungsverknüpfungen.
- **P3 – Projektdiagramme:** Gantt/Zeitstrahl, Mindmap, Organigramm.

### Fehlende Elemente in bestehenden Modulen

- **Mechanik (P1):** Kugel- und Trapezgewindetrieb (Mutter folgt der Spindelachse), Freilauf, Drehmomentwandler,
  CVT-Variator, Wellgetriebe (Harmonic Drive), Zykloidgetriebe, Getriebegehäuse-Umriss, Ölsumpf und Ölstand, Parksperre.
- **Maschinen (P2):** Batteriepack aus Modulen/Zellen, Bordlader, Stromschienen, Kabel/Phasenleitungen,
  Wechselrichter-Topologie im Detail (Halbbrücken), Kühlkreislauf um den E-Antrieb.
- **Elektrik (P2):** SPS mit E/A-Karten, Motorstarter, Sensorfamilie (Temperatur, Druck, Hall, Position),
  Steckerbelegung, Kabelbaumsegmente mit Längen.
- **Fluid (P2):** Einbauventile, Proportional- und Servoventile, Ventilblock, Schnellkupplungen, Filter mit Bypass und
  Verschmutzungsanzeige.
- **Architektur (P2):** Dächer (Grundriss und Schnitt), Maßketten für Wände, Elektro-/HLK-Ebene an Räume gebunden,
  Garten und Landschaft.
- **Fahrzeug (P3):** Manöver-Vorlagen (Spurwechsel, Überholen, Abbiegen, Einparken), V2X, weitere Verkehrszeichen,
  Innenraum (Sitze, Anzeigen, Bedienelemente).
- **Kinematik (P3):** Zahnrad- und Riemengelenke, Kurvenscheiben-Generator, Parallelroboter (Delta), Malteserkreuz.
- **Diagramme (P3):** Sequenz-Fragmente (alt/loop/opt), Verteilungsknoten, C4 (oder in `net.*`).
- **Beschriftung (P3):** Tabellen, vergrößerte Einzelheit mit Verknüpfung zur Quelle, Revisionswolken.

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
    (20.11.2026); der Planmaßstab steht in schwebender Leiste und Kreismenü von Planelementen (21.11.2026); Einheiten des Web-Add-ins
    mit aufgeteilten Modell-Tags (`ISK_MODEL_2` … `ISK_MODEL_N`) werden gelesen (21.11.2026);
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
  Seiten-/Schemaansichten von Zahnrädern (Teilkreislinien), Riemen und Ketten, Antriebsstränge und Räder; Schnell-Anfügen,
  Kopplung und freies Bearbeiten mit Alt auf der Web-Bühne am 21.11.2026 erledigt (Ziehen aus Anschlüssen und Verbinden
  mit Anschlüssen noch nur im COM-Add-in).
- UX-Runde 21.11.2026, Teil 2 (Maintainer), COM: Schloss-Marker an jeder Stelle, an der die Auswahl ein anderes Element
  berührt und ihm folgt, ein Klick löst oder koppelt (`Links`, `SketchObject.Released`); die Einfüge-Palette setzt passende
  Teile sofort richtig (`Fitting.Place`: Rad/Lager auf die Welle, Rad in Eingriff, Welle durch die Nabe, Lager neben ein
  Rad); „+“-Griffe für Ecken und Abzweige an Leitungen, Rohren, Straßen und Polylinien mit Verbindungspunkt am Abzweig
  (`Polylines`), Doppelklick entfernt eine Ecke. Offen: dasselbe auf der Web-Bühne (Engine-Methoden für Links, Fitting und
  Polylines); alle drei mit dem Maintainer in PowerPoint prüfen. Kartenstraßen bilden dort, wo sie sich kreuzen, bereits
  Kreuzungen.
- COM-Feinschliff 21.11.2026 (Maintainer: „bringe COM-Add-in auf Stand“): Die Leiste verdeckt keine anderen Elemente mehr,
  bis zu drei passende Teile direkt in der Leiste, innere Ecken docken nicht an, Antriebe nehmen ihre Welle mit, Riemen
  und Ketten folgen ihren Scheiben, Piktogramme für Formen, Füllungen, Leitungsführung, Wandarten und Fluid-Leitungsarten.
  Offen für COM: weitere Exporte; Vorrang-Test `EquivalentAddins` (braucht das Web-Add-in mit dem Zertifikat des
  Maintainers).
- Prüfen, welcher Office.js-Metadatenspeicher Kopieren/Einfügen und erneutes Öffnen unter Windows/Mac/Web übersteht (ADR-0006).
- Den (Vektor-)PDF-Writer für den Export auf Engine-Seite festlegen.
