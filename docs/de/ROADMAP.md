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

## Plan 24.11.2026: UI/UX-Konzept und Plan-Review

Rückmeldung des Maintainers am 24.11.2026: Das Einfügen-„+“, das Vorschaubilder an die Leiste hängt, ist kein gutes UI
und klappt nicht bei allen Elementen; Spiegeln/Drehen erscheinen auch, wo sie nichts ändern; eine Mehrfachauswahl
bekommt nur das native PowerPoint-Format; ganze Baugruppen (Getriebe, Hausinstallation) sollen zwischen Drauf-, Seiten-
und Vorderansicht wechseln, ohne dass sich der Mechanismus ändert. Reihenfolge: Konzept und Findings zuerst in COM
umsetzen, dann Web, dann LibreOffice.

### UX-Regeln (verbindlich für alle Hosts)

1. **Nur was im Kontext Sinn ergibt.** Ein Knopf erscheint nur, wenn er für dieses Element etwas bewirkt: Ansichten nur
   bei mehr als einer, Spiegeln/Drehen nur, wenn die Zeichnung in dieser Richtung nicht symmetrisch ist (`Symmetry`, aus
   der gezeichneten Geometrie samt Pfeilspitzen berechnet), Zerlegen nur bei zerlegbaren Teilen, Bewegen nur bei
   beweglichen Teilen, F nur bei Koppelgetrieben.
2. **Klein, feste Reihenfolge, Piktogramme zuerst.** Ansichten | Auswahl (Linienart, Spitzen, Stil) | bis zu fünf
   Elementaktionen | Anbauen, Einfügen, Baugruppe, Bewegen, Zerlegen | Transformationen | Duplizieren, Löschen.
   Piktogramme mit Tooltip (Tastenkürzel im Tooltip), Text nur für Werte, die ihn brauchen (1:50, F = 1, Schalter ohne
   sprechendes Piktogramm). Die Leiste wächst nie mit Inhalt (keine Vorschaubildreihen).
3. **Ein Einfügeweg.** Einfügen-Knopf, Kreismenü und Strg+Leertaste öffnen dasselbe Einfügefeld am Zeiger: zuerst, was
   zur Auswahl passt (an seinen Platz gesetzt), dann zuletzt Verwendetes; Tippen durchsucht die ganze Bibliothek (beide
   Sprachen, Umlaute gleichwertig, Ansicht der Auswahl bevorzugt). Ein Anbau-„+“ auf der Folie öffnet dasselbe Feld am
   „+“ mit den dort andockenden Teilen zuerst.
4. **Mehrfachauswahl hat eine eigene Leiste.** Eine Ansicht für alle, Mitten auf eine Zeile/Spalte, gleichmäßig
   verteilen, Bezugszeichen neu nummerieren – jeweils nur, wenn es etwas ändert (`Arrange`); das native
   PowerPoint-Format bleibt verfügbar.
5. **Baugruppen sind Bürger erster Klasse.** „Baugruppe auswählen“ wählt alle gekoppelten Teile; der Ansichtswechsel
   einer ausgewählten Baugruppe hält Eingriffe, Wellen und Achsabstände (`AssemblyViews`); ein Teil behält seinen
   Teilkreis in jeder Ansicht.
6. **Das Ergebnis schon beim Ziehen zeigen.** Gekoppelte Nachbarn (kämmende Räder, Riemen, Wellen am Motor) erscheinen
   beim Ziehen und Skalieren in einer zweiten Farbe, bevor man loslässt.
7. **Überall dieselben Gesten.** Griffe: Ecken/Kanten skalieren, runder Griff dreht, „+“ an Enden und Ports baut an,
   Doppelklick bearbeitet Text, Alt setzt das Einrasten aus, Shift rastet in 15°, Esc bricht ab; Tasten R, X, Y,
   Strg+D, Shift+F10.
8. **Wenig Text, zweisprachig.** Jede Beschriftung und jeder Tooltip zweisprachig; Tooltips sagen, was passiert, nicht
   wie es funktioniert.

### Review des Plans (24.11.2026)

- Seit 21.11.2026 in COM erledigt: Linien-Sprache, Anbau-Griffe, Koppelgetriebe und Getriebezüge aus Einzelteilen und
  live bewegt, Zerlegen vordefinierter Getriebe (seit 25.11.2026 auch Kurvengetriebe), Einfügen per Tippen, Live-Vorschau der Kopplung,
  kontextbezogene Leiste, Leiste für Mehrfachauswahl, Ansichtswechsel von Getriebezügen, PowerPoint-UI-Szenarien
  (`tests/powerpoint-com`).
- Erledigt am 24.11.2026 (Engine, alle Hosts über die Bibliothek): Modul Diagramme/Kennlinien (`chart.*`, 25 Elemente,
  docs/de/modules/charts.md) und die fehlenden Mechanik-Elemente (Gewindetriebe mit Mutter auf der Spindel, Freilauf,
  Wandler, Parksperre, CVT, Well- und Zykloidgetriebe, Getriebegehäuse mit Lagersitzen, Ölwanne;
  docs/de/modules/mechanical-drives.md). Dort offen: Wirkungsgradkennfelder aus Messrastern, Polar- und Flächendiagramme,
  Histogramme, eine zweite y-Achse für eigene Daten; „um die Auswahl einfügen“ für Gehäuse.
- Seitdem in COM erledigt: Kegelradpaare in der Kopplung, Zerlegen von Schema-Planetensätzen (über die Stirnansicht),
  Fangbeschriftungen, Tab durch Baugruppen, Format übertragen für Semantik, Koppelkurven selbst gebauter Ketten,
  Strg+Leertaste ohne Auswahl, Anbau-„+“ und „Ändern in“ im Einfügefeld.
- Inzwischen in COM erledigt: Zerlegen von Kurvengetrieben in eine Kurvenscheibe als Einzelteil und einen
  Stößel-Gleitstein (25.11.2026); Differentiale zerfallen seit 25.11.2026 in Korb, Achswellen- und Ausgleichsräder, Ansichtswechsel für Haustechnik (Grundriss ↔
  Schnitt ↔ Strangschema). Zwischenlagen und Koppelkurven selbst gebauter Ketten sind erledigt (24.11.2026); explizite
  Gelenkgrenzen sind erledigt (Anschläge an Gestelldrehpunkten, 25.11.2026).
- Web: am 24.11.2026 auf den COM-Stand der UI/UX-Runde gebracht (Spiegeln/Drehen nur mit Wirkung, Einfügefeld mit
  Strg+Leertaste, passende Teile an ihrem Platz, Leiste für Mehrfachauswahl, Baugruppe auswählen, Fangbeschriftung,
  Koppelkurve, F = n und Zerlegen; Engine-Methoden `search`, `transformEffect`, `arrangeOptions`/`arrange`,
  `assembly`, `trace`/`drawTrace`, `insertFitting`, `explode`, `dof`). Noch nicht in PowerPoint im Web geprüft (braucht
  das Entwicklerzertifikat auf dem Rechner des Maintainers). Seitdem auch im Web: Anbau-„+“ auf der Bühne mit
  Wellenziehen (Vorschau aus `buildStretchPreview`), Live-Vorschau der Kopplung, Zwischenlagen, Maß zwischen zwei Teilen,
  Bewegungsmodus (Getriebezüge und vordefinierte Getriebe drehen, selbst gebaute Koppelgetriebe ziehen; Engine
  `motionInfo`/`motionPreview`/`motionTurn`/`motionDrag`, das Ziehen rechnet von seinem letzten Schritt weiter)
  und die Bibliothekssuche mit der Rangfolge der Engine (24./25.11.2026).
- LibreOffice (25.11.2026): dieselbe Engine-API im Seitenleisten-Deck – Bibliotheks-Panel mit der Engine-Suche
  (passende Teile zuerst, zuletzt Verwendetes), Auswahl-Panel mit kontextbezogenen Knöpfen (Ansichtsliste,
  Spiegeln/Drehen nur mit Wirkung, Anbau-Ports, Getriebezug um einen getippten Winkel drehen, Koppelkurve,
  Zwischenlagen, Baugruppe auswählen, Zerlegen) und den Operationen für Mehrfachauswahl aus `arrangeOptions`;
  Bearbeitungen laufen in einer Seitensitzung über alle Einheiten der Seite, ein Rückgängig-Schritt
  (docs/de/hosts/libreoffice.md). Ohne Overlay auf der Arbeitsfläche nicht möglich: Ziehen mit Einrasten,
  Live-Vorschau der Kopplung, Anbau-„+“ auf der Fläche, Bewegen durch Ziehen, Kreismenü, Strg+Leertaste.

### Review am Ende des 24.11.2026 und nächste Schritte

**Heute erledigt:** Die UX-Regeln oben sind in COM umgesetzt (geprüft mit 16 PowerPoint-UI-Szenarien in
`tests/powerpoint-com`) und im Web-Host (Unit-Tests und Browserprüfung). Neue Module: Diagramme/Kennlinien,
technische Gebäudeausrüstung; neue Mechanik-Elemente; Stückliste; Maß zwischen Teilen; Zwischenlagen und Koppelkurven;
Zerlegen über eine andere Ansicht; Kegelradpaare koppeln; Einfügefeld überall; kompakte Leiste (ein Ansichts-Chip ab
mehr als drei Ansichten, Bewegen nur, wo es etwas zeigt); Suchen & Einfügen im Menüband.

**Plan-Review – was sich am Plan ändert:**
- *Baugruppen sind die Arbeitseinheit.* Ansichtswechsel, Baugruppe auswählen, Zerlegen, Stückliste und die Leiste
  für Mehrfachauswahl arbeiten auf Baugruppen; neue Funktionen sollten das auch (Varianten/Zustände, Animation von
  Getriebezügen als Folien).
- *Ein Einfügeweg überall* hat die Paletten in der Leiste ersetzt; dasselbe Feld sollte auch die Bibliothekssuche der
  Seitenleiste bedienen.
- *Prüfung:* Jede sichtbare COM-Funktion bekommt ein Szenario; vor größeren Merges läuft die ganze Suite (etwa
  35 Minuten, Maus belegt).

**Als Nächstes, in dieser Reihenfolge:**
1. Web: Prüfung in PowerPoint im Web, sobald der Maintainer das Entwicklerzertifikat installiert hat (alle
   COM-Funktionen der UI/UX-Runde einschließlich Bewegungsmodus sind umgesetzt).
2. LibreOffice: in der Seitenleiste erledigt (25.11.2026); als Nächstes ein Overlay auf der Arbeitsfläche (Einrasten
   beim Ziehen, „+“, Strg+Leertaste, Kreismenü).
3. COM, fachliche Lücken: aus der Liste keine mehr offen (erledigt 25.11.2026: Kurvenscheibe als Einzelteil mit
   aufliegenden Stößeln – Drehen der Scheibe bewegt den Stößel-Gleitstein und seine Kette –, Kurvengetriebe zerfallen in
   Drehpunkt, Scheibe und Stößel; Ravigneaux-Sätze (Planetenpaare auf einem Steg mit Bolzenpaaren, Zerlegen, Willis-Übersetzungen);
   Gelenkgrenzen als Anschläge an Gestelldrehpunkten; Kegelradpaare in den
   Drehzahlen des Getriebezugs nach Zähnezahl (sie haben nur Seitenansichten und drehen sich daher nicht sichtbar); das
   zerlegte Differential arbeitet als solches (Räder lose im Korb, Willis mit dem Korb: geradeaus drehen beide Seiten mit
   dem Korb, eine Seite festgehalten → die andere dreht doppelt so schnell); der Ansichts-Alias `elevation`/`section` für
   Wände mit Haustechnik).
4. P2-Module: Verfahrens- und Wärmetechnik (35 Typen), Energiesysteme (30), Robotik (25) erledigt 25.11.2026 (Engine,
   alle Hosts über den Katalog; Moduldoku docs/de/modules/process.md, energy.md, robotics.md); folienweiter Stil:
   ISO-128-Liniengruppen und Schwarz-Weiß als Folienstil für alle Elemente, auch neue (erledigt 25.11.2026 in COM und
   Web); offen: Farbakzente je Modul.
5. P3: Varianten und Zustände, automatisches Diagramm-Layout, Patent-Arbeitsablauf, Import (SVG/DXF, GeoJSON/GPX,
   CSV).

### Weitere sinnvolle Module und Funktionen

- **Technische Gebäudeausrüstung (TGA, P1 nach den Diagrammen; erledigt am 24.11.2026, Modul `building-services`,
  docs/de/modules/building-services.md; ganze Installationen wechseln Grundriss ↔ Schnitt ↔ Schema über `AssemblyViews`
  mit der Regel des Moduls; Wände, Türen und Fenster wechseln in ihre Ansicht mit
  (Ansichts-Alias `section`/`elevation`, 25.11.2026); offen: Schnittlinie je Wand; die
  Elektro-Hausinstallationssymbole sind seit 25.11.2026 dabei, die Regel liegt als `InstallationViews` im Core):** Heizung, Sanitär, Lüftung und Elektroinstallation,
  gebunden an die Räume der Architektur, mit Grundriss, Schnitt und Schema (Strang) derselben Installation (der
  Ansichtswechsel für Baugruppen gilt).
- **Stückliste aus der Folie (P2, in COM erledigt am 24.11.2026):** ein Tabellenelement, das die Teile der Folie oder Auswahl auflistet (Typ,
  Kennwerte, Bezugszeichen) und sich mit den Teilen aktualisiert; für Patentfiguren und Getriebefolien.
- **Maß zwischen Teilen (P2, in COM erledigt am 24.11.2026 für zwei Teile):** zwei Teile wählen → Achsabstand, Spalt oder Winkel als mitlaufendes Maß.
- **Folienweiter Stil (P2):** Linienbreiten nach ISO-128-Gruppen, Schwarzweiß-/Patentmodus, Farbakzente je Modul, auf
  alle Elemente der Folie auf einmal.
- **Mathematik & Geometrie (P3):** Koordinatensysteme, Vektoren, Winkel, geometrische Konstruktionen für Lehrfolien.
- **Handskizzen-Look (P3):** wahlweise skizzenhafte Darstellung desselben Modells für Workshop-Folien.

## Plan 21.11.2026: Interaktion, Module und Elemente

Mit dem Maintainer nach der COM-Feinschliff-Runde vereinbart. Prioritäten: **P1** als Nächstes, **P2** danach, **P3**
später. Jeder Punkt kommt zuerst in Core/Engine, dann COM, dann Web, dann LibreOffice (Host-Reihenfolge unverändert).

### Interaktionsmechanismen und UX-Konzepte

Stand 21.11.2026 (COM): Die einheitliche Liniensprache (verlängern, weiterzeichnen, Ecken, Abzweige inkl. Wand-T-Stöße,
teilen, verbinden), Bau-Griffe für Zahnräder, Wellen und Maschinen (inkl. Welle strecken) und freies Andocken von
Beschriftungen auf Flächen sind erledigt und in PowerPoint geprüft, ebenso Umschalt-15°-Schritte für Linien und Kreuzungen
von Leitungen und Rohren (automatische Brücke, Verbinden-Symbol). Engine/Web folgen. Prüfung von Ankern, Kontakten,
Randbedingungen und Verbindungen je Element und Ansicht am 21.11.2026 erledigt (Bericht aus
`AnchorAuditTests`): Maschinenschnitte bekommen Naben in Seiten- und Schemaansicht und führen ihre Welle, Heizungs- und
Sanitärsymbole bekommen Rohranschlüsse, Rohrleitungen im Grundriss die Liniensprache, Zahnräder in Seitenansicht kämmen
nur mit Seitenansichten auf paralleler Achse quer zur Achse (der Teilkreis trägt die Achse, `Anchor.SidePitch`).

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

### Getriebe selbst bauen (Maintainer 21.11.2026)

Stand 21.11.2026 (Core + COM): Steg als Teil mit Bolzen und Bau-„+“, Planeten passend zu Sonne/Hohlrad, 2–8 Planeten,
Stufenplaneten, Zerlegen von Planetensätzen (Stirnansichten), Kegelräder mit Spitzen-Griffen (Winkelpaare, Differential aus
Einzelteilen) erledigt. Inzwischen erledigt: Kopplung von Kegelradpaaren, Zerlegen des Schema-Satzes und des
Differentials, Getriebe bewegen, das Differential in den Drehzahlen des Getriebezugs, Planetenpaare (Ravigneaux-Satz,
24./25.11.2026).

- **P1 – Planetenträger (Steg) als eigenes Teil:** Stirnansicht mit 2–8 Armen oder Scheibe und Planetenbolzen auf einem
  Radius, Seitenansicht mit den Bolzen als Achsen; Planeten docken an den Bolzen an und werden mitgenommen; Bau-„+“ an
  freien Bolzen (Planet) und in der Mitte (Welle); Option „fester Steg“ (gestellfest, Gehäusesymbol), Stege, die keine
  Wellen sind.
- **P1 – Planetensätze frei gestalten:** Planetenanzahl 2–8 (heute 3–6), mehrere Planetenreihen, Stufenplaneten (zwei
  Verzahnungen auf einem Planetenbolzen, sonnen- und hohlradseitig), Planetenpaare (Ravigneaux-artig), zusammengesetzte
  Sätze; einen Satz in Sonne, Planeten, Hohlrad und Steg „zerlegen“, damit jedes Teil einzeln bearbeitet und gekoppelt
  werden kann.
- **P1 – Kegelräder und Differentiale aus Einzelteilen:** Kegelradpaare kämmen in der Seitenansicht über rechtwinklige
  Achsen; das Bau-„+“ an einem Kegelrad bietet das Gegenrad unter 90°; ein Differential aus Gehäuse/Steg, zwei
  Achswellenrädern und zwei oder mehr Ausgleichsrädern, auch als Start-Baugruppe.
- **Erledigt 21.11.2026 (Core + COM): Koppelgetriebe selbst bauen** — Glieder docken an Gelenken, Gestellpunkten,
  Gelenksymbolen und Schiebern an, Bau-„+“ an freien Gelenken, Freiheitsgrad in der Leiste, Ziehen im Bewegungsmodus bei
  konstanten Längen (siehe Host-Doku). Viergelenk, Schubkurbel, Kniehebel, Schnellrücklaufgetriebe und
  Kreuzschubkurbel lassen sich in ein selbst gebautes Koppelgetriebe zerlegen; Glieder bieten Kulissen (Gleitstein im
  Schlitz) (21.11.2026). Inzwischen erledigt: Einzelteil Kurvenscheibe mit Kurvenkontakt und Zerlegen des
  Kurvengetriebes, Grenzen/Zwischenlagen/Koppelkurven für selbst gebaute Ketten, Engine/Web (24./25.11.2026). Offen:
  Zahnradgelenke in Ketten.
- **P1 – Koppelgetriebe selbst bauen (Maintainer 21.11.2026: „es gibt nur vordefinierte“):** Glieder, Gelenke und
  Gestelldrehpunkte gibt es als Einzelteile, aber Gliedenden docken nicht aneinander an, und nur die vordefinierten
  Getriebe (Viergelenk, Schubkurbel …) bewegen sich. Plan: Gliedenden docken an Gelenken anderer Glieder an (Anbindung an
  einen Punkt eines Punktobjekts, folgt ihm); Glieder, Gelenke, Gleitsteine und Gestelldrehpunkte bilden eine Kette, die
  die Engine erkennt (Graph), mit live angezeigtem Freiheitsgrad (Grübler); Bau-„+“ am freien Gelenk eines Glieds
  (nächstes Glied, Gestelldrehpunkt, Gleitstein, Kurvenstößel); ein Gelenk oder eine Kurbel auf der Folie ziehen, und die
  Kette bewegt sich bei konstanten Gliedlängen (allgemeiner 2D-Zwangsbedingungslöser statt der geschlossenen Lösungen je
  Getriebe); Grenzen, Zwischenlagen, Koppelkurven und Animation als Folien für jede Kette; vordefinierte Getriebe werden
  Start-Baugruppen, die sich in ihre Glieder zerlegen lassen.
- **Erledigt 21.11.2026 (Core + COM): Getriebe live per Ziehen im Bewegungsmodus bewegen** (siehe Host-Doku);
  Koppelgetriebe drehen ihre Antriebskurbel. Offen: Folien-Animation von Getrieben als Extra für alles Bewegliche
  (Maintainer: Live-Ziehen zuerst), Ziehen zum Simulieren für selbst gebaute Koppelgetriebe.
- **P2 – Getriebe bewegen:** den Antrieb drehen (Zahnrad oder Welle auf der Folie ziehen), und jedes Rad dreht sich mit
  seiner Übersetzung (Zähne bleiben im Eingriff, Stege laufen um, Planeten wälzen ab); Übersetzungen auf Wunsch
  angezeigt; Animation als Morph-Folien wie bei den Koppelgetrieben (`ObjectType.AnimationFrames` für Getriebe). Heute
  animieren nur die Koppelgetriebe des Kinematikmoduls als Folien (Kreismenü); Ziehen zum Simulieren ist für beide offen.

### Neue Module

- **P1 – Diagramme/Kennlinien (`chart.*`, Spezifikation §6.12):** XY/Linie/Punkte/Balken, Kennlinien
  (Drehmoment–Drehzahl, Wirkungsgradkennfeld), Bode-Diagramm, Toleranzbänder; Daten eintippen oder aus Excel einfügen;
  schwarz-weiß-tauglich.
- **P2 – Verfahrens- und Wärmetechnik (`process.*`, erledigt 25.11.2026):** R&I-Fließbild über die Fluidtechnik hinaus (Behälter, Kolonnen,
  Wärmetauscher, Pumpen, Messstellen mit Kennzeichnung), thermodynamische Kreisprozesse (T-s-, p-v-, p-h-Diagramme),
  Klimatechnik.
- **P2 – Energiesysteme (`energy.*`, erledigt 25.11.2026):** PV, Wind, Batteriespeicher, Ladegerät/Wallbox, Netzanschluss, Wasserstoff,
  Übersichtsschaltpläne über Spannungsebenen.
- **P2 – Robotik (`robot.*`, erledigt 25.11.2026):** Arme aus Kinematik-Gelenken, Koordinatensysteme, Greifer, Arbeitszellen,
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

- **PowerPoint-UI-Szenarien (21.11.2026):** `tests/powerpoint-com` enthält elf Szenarien, übernommen aus den
  Testskripten der Sitzung; `explode-linkages` läuft grün (10 Prüfungen). Die übrigen zehn (quick-add, bar, gear-mesh,
  motor-shaft, build-ports, line-language, crossings, gear-trains, gear-motion, linkage-build) sind in dieser Form noch
  nicht gelaufen und brauchen evtl. Korrekturen an Bildschirmpositionen.
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
