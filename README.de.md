# InventaSketch

> Deutsche Übersetzung. Maßgeblich ist die englische Fassung: [README.md](README.md).

**Schnelle technische Skizzen, schematische Maschinenelemente und Patentzeichnungen – direkt in PowerPoint und LibreOffice.**

[English](README.md) · [Spezifikation](docs/de/product/specification.md) · [Architektur](docs/de/architecture/overview.md) · [Roadmap](docs/de/ROADMAP.md) · [Benutzerhandbuch](docs/de/user-guide/README.md)

> **Status: frühe Entwicklung (v0.1).** Engine-Kern, Fachmodule und das Web-Add-in befinden sich im Aufbau.
> Noch nichts davon ist produktionsreif; was heute tatsächlich funktioniert, zeigen die [Roadmap](docs/de/ROADMAP.md) und die
> [Host-Fähigkeitsmatrix](docs/de/hosts/capability-matrix.md).

InventaSketch ist ein Werkzeug für schnelles, pragmatisches Prototyping („quick and dirty“) für Ingenieure, Erfinder und
Patentfachleute. Ziehen Sie ein Zahnrad, eine Welle, ein Wälzlager, einen Pfeil oder einen Flussdiagramm-Schritt auf die
Folie, skalieren Sie es frei, lassen Sie es an seinen Nachbarn einrasten und ergänzen Sie Bezugslinien und Bezugszeichen –
ohne Maße, ohne Modul, ohne Zähnezahl. Wenn Sie mehr brauchen, tragen dieselben Objekte optionale technische Parameter,
lassen sich gegen die Zeichnungsvorschriften der Patentämter prüfen und monochrom exportieren.

## Highlights (Zielumfang)

- **Getriebe in Sekunden:** Stirnräder, Hohlräder, Kegelräder, Schnecke/Schneckenrad, Planetensätze und Differenziale;
  Wellen und Wälzlager im Getriebeschema (Strichdarstellung nach den Konventionen von VDI 2127 / ISO 3952), vereinfachte
  Darstellung (im Stil von ISO 2203) und Schnitte – das Einrasten lässt Zahnräder auf ihren Teilkreisen kämmen und setzt
  Naben auf Wellenachsen.
- **Patentfiguren:** Bezugszeichen mit geraden oder gekrümmten Bezugslinien, konsistente Nummerierung über alle Figuren,
  Schraffur, FIG-Beschriftungen, Blattnummern, Amtsprofile (DPMA, EPA, PCT, USPTO) mit quellenbelegter Vorabprüfung
  (Preflight) und Schwarz-Weiß-Export.
- **Beschriftungswerkzeuge:** Pfeile mit vielen Pfeilspitzen, gekrümmte und gewinkelte Verbinder, Bemaßungen, Klammern,
  Schnitt- und Bruchlinien, Textfelder, Legenden, Symbole der Technischen Mechanik, Flussdiagramm-Formen.
- **Native Objekte zuerst:** Eingefügte Figuren sind native, bearbeitbare PowerPoint-/LibreOffice-Formen, wo immer der
  Host dies zulässt; Export als SVG/PDF/PNG.
- **Modulare Fachgebiete:** Mechanik, Kinematik, Elektrotechnik und Elektronik, elektrische Maschinen,
  Automotive/ADAS/HMI, Architektur und Grundrisse, UML/ER/Ablauf, Karten und Routen, Diagramme – Modul für Modul ergänzt.
- **Aufgeräumte Oberfläche:** helles/dunkles/automatisches Design, Deutsch/Englisch (automatisch), schwebende
  Kontextleiste, Kreismenü per Rechtsklick, interaktiver Seitenleisten-Editor dort, wo der Host keine Interaktion auf
  der Folie erlaubt.

## Hosts

| Host | Plattformen | Status |
|---|---|---|
| PowerPoint-COM-Add-in (hat Vorrang, sofern installiert) | Windows | geplant |
| PowerPoint-Web-Add-in (Office.js, interaktiver Seitenleisten-Editor) | Windows, macOS, Web | in Arbeit |
| LibreOffice-Erweiterung (Impress, Draw, Calc) | Windows, macOS, Linux | geplant |

## Build

Voraussetzungen: .NET 10 SDK (das Build-Skript installiert es bei Bedarf benutzerlokal); Node.js 20+ für das Web-Add-in.

```powershell
.\scripts\build.ps1
```

```bash
./scripts/build.sh
```

## Aufbau des Repositorys

```text
src/InventaSketch.Core            Engine-Kern (Modell, Geometrie, Einrasten, Rendering, Persistenz)
src/InventaSketch.Modules.*       Fachmodule (Annotation, Patent, Mechanical, …)
src/InventaSketch.Engine          JSON-Engine-API, gemeinsam für alle Hosts
hosts/office-web                  PowerPoint-Web-Add-in (WebAssembly-Engine + TypeScript-UI)
hosts/powerpoint-com              PowerPoint-COM-Add-in (geplant)
hosts/libreoffice                 LibreOffice-Erweiterung (geplant)
docs/en, docs/de                  Dokumentation (Englisch maßgeblich, deutsche Übersetzung)
docs/adr                          Architekturentscheidungen (ADRs)
```

## Mitwirken

Siehe [CONTRIBUTING.md](CONTRIBUTING.md) und [CLAUDE.md](CLAUDE.md) (Arbeitsregeln für Menschen und KI-Agenten, beide
auf Englisch).

## Lizenz

[Apache License 2.0](LICENSE). Die Angaben zu Patentvorschriften dienen nur als Hilfestellung und sind keine
Rechtsberatung.
