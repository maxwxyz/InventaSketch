# Architekturüberblick

> Deutsche Übersetzung. Maßgeblich ist die englische Fassung: [../../en/architecture/overview.md](../../en/architecture/overview.md).

> Entscheidungen: [ADRs](../../adr/).

## 1. Eine Engine, drei Hosts

```text
                ┌──────────────────────── InventaSketch-Engine (C#, netstandard2.0 + net10.0) ───────────────────────┐
                │  Kern: Modell · Geometrie · Primitive · Registry · Fang · Schraffur · Bearbeiten/Undo · JSON · SVG │
                │  Module: Annotation · Patent · Mechanical · (Kinematics · Electrical · Automotive · …)             │
                │  Engine-API: JSON-Fassade, Editor-Sitzungen, Treffertest, Einrasten, Kontextaktionen, Zeichenpläne │
                └───────────────┬───────────────────────────────┬────────────────────────────────┬──────────────────┘
                                │ im Prozess (.NET Fx 4.8)      │ .NET-WebAssembly ([JSExport])  │ .NET-Kindprozess
                                ▼                               ▼                                │ (JSON-Zeilen, stdio)
                  PowerPoint-COM-Add-in (Windows)    Office-Web-Add-in (TS-UI)                   ▼
                  Menüband, Overlay auf der Folie,   Vorschau-Editor in der Seiten-   LibreOffice-Erweiterung (Python-UNO .oxt)
                  schwebende Leiste, Kreismenü,      leiste, Office.js-Zeichenplan-   Impress / Draw / Calc, Handler auf der
                  native Formen über COM             Schreiber (native Formen +       Zeichenfläche, Overlay-Ebene,
                                                     SVG-Ersatz)                      native Formen über UNO
```

Warum C# und nicht TypeScript: Die Interaktion auf der Folie in COM (Maus-Hooks + geschichtetes Overlay-Fenster) und auf
der Zeichenfläche in LibreOffice (UNO-Maus-Handler) braucht synchrones Einrasten und synchrone Treffertests im
Host-Prozess. .NET läuft in PowerPoint im Prozess, im Aufgabenbereich als WebAssembly und neben dem mitgelieferten
Python von LibreOffice als kleiner, eigenständiger Prozess. Dieses Muster hat sich im früheren Add-in des Maintainers in
der Produktion bewährt. Siehe [ADR-0002](../../adr/0002-csharp-engine.md).

## 2. Schichten

| Schicht | Projekt | Verantwortung |
|---|---|---|
| Geometrie & Primitive | `InventaSketch.Core` (`Geometry.cs`, `Drawing.cs`, `Hatching.cs`) | Vektoren, Matrizen, Pfade, Stile, Pfeilspitzen, Schraffur |
| Dokumentmodell | `InventaSketch.Core` (`Model.cs`) | `SketchDocument`, Seiten, Objekte, Anbindungen, Referenzen |
| Modulvertrag & Registry | `InventaSketch.Core` (`Modules.cs`) | `IModule`, `ObjectType`, Ansichten, Parameter, Ankerpunkte, Aktionen, Bibliothek |
| Bearbeitungen & Verlauf | `InventaSketch.Core` (`Editing.cs`) | reine `doc → doc`-Bearbeitungen, Rückgängig/Wiederholen |
| Einrasten | `InventaSketch.Core` (`Snapping.cs`) | semantisches Einrasten, Punkt-, Ausrichtungs- und Rasterfang mit Hilfslinien |
| Rendering | `InventaSketch.Core` (`SvgRenderer.cs`) | SVG-Export (farbig/monochrom), Miniaturansichten |
| Persistenz | `InventaSketch.Core` (`DocumentJson.cs`) | versioniertes JSON, kompakte Nutzdaten `isk1:` |
| Fachmodule | `InventaSketch.Modules.*` | Objekttypen, Bibliotheken, Regeln |
| Engine-API | `InventaSketch.Engine` | JSON-Fassade für alle Hosts, Editor-Sitzungen, Zeichenpläne |
| Hosts | `hosts/office-web`, `hosts/powerpoint-com`, `hosts/libreoffice` | nur Bedienoberfläche und Übersetzung auf die Host-API |

Abhängigkeitsregel: Core ← Modules ← Engine ← Hosts. Module referenzieren nie Hosts oder die Interna anderer Module
(Patent darf die öffentlichen Typen von Annotation verwenden). Keine Fachlogik in TypeScript oder Python.

## 3. Das Objektmodell in der Praxis

- **Box-Objekte** (Zahnrad, Wälzlager, Textfeld …) werden in lokalen Koordinaten innerhalb von `Size`, zentriert um
  (0,0), gerendert; die Objektmatrix (Verschiebung · Drehung · Spiegelung) platziert sie. Freies Skalieren ändert nur
  `Size`.
- **Punktobjekte** (Pfeil, Bezugslinie, Bemaßung, Leitung) speichern `Points` in Seitenkoordinaten; jeder Punkt kann
  an normierten Boxkoordinaten (u, v) an ein anderes Objekt **angebunden** sein, sodass er Verschiebungen, Drehungen und
  freiem Skalieren folgt.
- **Ansichten** wählen eine Darstellung; **Parameter** sind optional; **Stil-Überschreibungen** färben um, ohne die
  Linienrollen des Typs anzutasten.
- **Ankerpunkte** (Mittelpunkt, Ecken, Kantenmittelpunkte, Enden, Anschlüsse, Achsabschnitte, Teilkreise, Naben) steuern
  das Einrasten.

## 4. Host-Zeichenpläne

Die Engine setzt gerenderte Primitive in einen Host-Zeichenplan in Punkt (pt) um: Rechtecke, Ellipsen, Linien
(Strichart, Pfeilspitzen), Freiformen (Polylinien/kubische Bézierkurven), Text, Gruppen, jeweils mit Stil und Metadaten.
Die fähigkeitsabhängige Umsetzung ersetzt je Host nicht unterstützte Merkmale (z. B. hat Office.js keine
Freiformen/Pfeilspitzen/Musterfüllung → explizite Geometrie oder ein SVG-Bild für dieses Objekt). Jedes eingefügte
Objekt bzw. jede Baugruppe ist eine Gruppe mit Tags für ID, Typ und komprimierte Modelldaten; der Speicher auf
Dokumentebene spiegelt das Modell.

## 5. Persistenz der Metadaten

| Host | Je Form/Gruppe | Dokumentebene |
|---|---|---|
| PowerPoint COM | `Shape.Tags` (`ISK_ID`, `ISK_TYPE`, `ISK_MODEL`) | benutzerdefinierte Dokumenteigenschaften (in Teilstücken) |
| Office.js | `shape.tags` (PowerPointApi 1.3+) | benutzerdefinierte Eigenschaften / Custom XML Parts (noch zu prüfen, siehe Recherche) |
| LibreOffice | Formname `isk:<id>`, `UserDefinedAttributes` (nur ODF) | benutzerdefinierte Dokumenteigenschaften (in Teilstücken) |

## 6. Teststrategie

- xUnit je Bibliothek (Geometrie, Einrasten, Schraffur, Bearbeitungen, Persistenz, Rendering und Ankerpunkte der Module).
- SVG-Galerie aller Bibliothekselemente × Ansichten zur visuellen Prüfung (Screenshots mit Headless-Browser).
- Vertragstests für die Engine-API; Tests des TypeScript-Adapters mit einem Fake von Office.js; Tests des
  Python-Adapters mit einem UNO-Stub.
- Host-Integrationstests (PowerPoint per COM-Automatisierung, LibreOffice headless), dokumentiert in der
  Fähigkeitsmatrix.
