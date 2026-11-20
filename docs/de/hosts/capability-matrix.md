# Host-Fähigkeitsmatrix

> Deutsche Übersetzung. Maßgeblich ist die englische Fassung: [../../en/hosts/capability-matrix.md](../../en/hosts/capability-matrix.md).

> Werte: **verifiziert** (getestet, mit Nachweis), **teilweise**, **nicht unterstützt**, **nicht getestet**. Eine Zelle
> wird nie ohne reproduzierbaren Test als verifiziert markiert.
> Die API-Fakten stammen aus [research/tools-and-host-apis.md](../../en/research/tools-and-host-apis.md) (englisch); sie
> gelten erst nach einem InventaSketch-Test auf dem echten Host als „verifiziert“.

| Fähigkeit | PowerPoint COM (Win) | PowerPoint-Web-Add-in (Win/Mac/Web) | LibreOffice (Win/Mac/Linux) |
|---|---|---|---|
| Native Rechtecke, Ellipsen, Linien, Text | nicht getestet (API vorhanden) | nicht getestet (PowerPointApi 1.4) | nicht getestet (UNO-Formen) |
| Native Freiform / Bézierkurve | nicht getestet (BuildFreeform) | in Office.js nicht unterstützt → SVG-Bild oder Folieninjektion | nicht getestet (PolyPolygonBezierShape) |
| Native Pfeilspitzen | nicht getestet | in Office.js nicht unterstützt → explizite Geometrie | nicht getestet (LineStart/LineEnd) |
| Native Muster-/Schraffurfüllung | nicht getestet (Musterfüllung) | nicht unterstützt → explizite Schraffurlinien | nicht getestet (FillHatch) |
| Gruppieren | nicht getestet | nicht getestet (PowerPointApi 1.8) | nicht getestet (XShapeGrouper) |
| Drehung | nicht getestet | nicht getestet (PowerPointApi 1.10) | nicht getestet |
| Form-Tags / Metadaten | nicht getestet (Tags) | nicht getestet (tags, 1.3) | nicht getestet (Formname + Dokumenteigenschaften) |
| Auswahl lesen/schreiben | nicht getestet | nicht getestet (1.5/1.6) | nicht getestet |
| Overlay auf der Folie, schwebende Leiste, Kreismenü | geplant (Hooks + geschichtetes Fenster) | nicht unterstützt → Arbeitsfläche in der Seitenleiste | geplant (UNO-Handler + Overlay-Ebene) |
| Editor in Seitenleiste / Aufgabenbereich | geplant | in Arbeit | geplant (Seitenleisten-Panel) |
| SVG einfügen | entfällt | nicht getestet (XmlSvg-Coercion; nicht auf dem iPad) | nicht getestet |
| COM-Vorrang vor dem Web-Add-in | nicht getestet (`EquivalentAddins`) | nicht getestet | entfällt |
| Automatische Erkennung hell/dunkel | geplant | in Arbeit | geplant |
| Automatische Erkennung Deutsch/Englisch | geplant | in Arbeit | geplant |

Reine Engine-Fähigkeiten (alle Hosts): SVG-Export durch Unit-Tests **verifiziert** (`SvgContainsHatchAndArrowheadAndIsMonochrome`),
semantisches Einrasten durch Unit-Tests **verifiziert** (`SnappingTests`), JSON-Persistenz **verifiziert** (`PersistenceTests`).
