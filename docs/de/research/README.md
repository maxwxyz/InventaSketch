# Recherche – deutsche Kurzfassung

> Kurzfassung auf Deutsch. Maßgeblich sind die englischen Recherchenotizen [drawing-rules.md](../../en/research/drawing-rules.md) und [tools-and-host-apis.md](../../en/research/tools-and-host-apis.md).

Beide Notizen haben den Stand 2026-11-20 und existieren nur auf Englisch. Diese Seite fasst die wichtigsten Ergebnisse
zusammen; Regel-IDs (z. B. `EP-07`, `US-08`) und Quellen stehen in den Volltexten.

## 1. Zeichnungsvorschriften der Ämter (DPMA, EPA, PCT, USPTO)

Volltext: [drawing-rules.md](../../en/research/drawing-rules.md) – nur Primärquellen; nicht bestätigte
Werte sind dort als **UNVERIFIED** markiert.

**Gemeinsame Eckwerte aller vier Ämter (Zeichnungsblatt A4):**

| Parameter | Wert |
|---|---|
| Mindestränder | oben 2,5 cm, links 2,5 cm, rechts 1,5 cm, unten 1,0 cm |
| Nutzfläche | höchstens 26,2 × 17,0 cm, **ohne Rahmen** (EPA, PCT, USPTO verbieten Rahmen ausdrücklich) |
| Zeichenhöhe | Ziffern und Buchstaben mindestens 0,32 cm (bei Helvetica-artigen Schriften ca. 13 pt; Vorgabe 14 pt) |
| Verkleinerung | alle Einzelheiten müssen bei Verkleinerung auf 2/3 erkennbar bleiben; Maßstab nur grafisch, kein „1:2“ |
| Bezugszeichen | schlicht, **ohne Klammern, Kreise oder Anführungszeichen** (EPA, PCT, USPTO; beim DPMA nicht geregelt) |
| Schnitte | Schraffur mit gleichmäßigem Abstand, bevorzugt 45°, an Bezugszeichen unterbrochen |

**Farbregeln je Anmeldeamt:**

- **DPMA:** nur schwarz-weiß; elektronische PDF-Zeichnungen unkomprimiert und nur mit den Schriften Times, Helvetica,
  Courier und Symbol. Fotos gelten nicht als Zeichnungen im Sinne von § 12 PatV.
- **EPA:** Regel 46 EPÜ ist seit 1. 2. 2023 gestrichen; maßgeblich ist der Beschluss des Präsidenten
  nach Regel 49(2) EPÜ, seit 1. 10. 2025 **ABl. EPA 2025, A49**. Seitdem sind **Farbe und Graustufen zulässig, aber
  nur bei elektronischer Einreichung** (A49 Art. 1(2)(a); A57). Papiereinreichungen bleiben schwarz-weiß.
- **PCT:** Regel 11.13(a) PCT verlangt auch in der ab 1. 1. 2026 geltenden Fassung Linien „without colorings“; Farbe
  wird für die Veröffentlichung in Schwarz-Weiß umgewandelt. Zusätzliche Farb-PDFs (C. PCT 1699) sind nur vorgeschlagen.
- **USPTO:** schwarz-weiß als Regelfall; Farbe bei Utility-Anmeldungen nur auf Antrag (Petition, Gebühr nach 37 CFR
  1.17(h)), bei Design-Anmeldungen ohne Antrag. 37 CFR 1.84(h) **verbietet Mittellinien und Projektionslinien**.

**Weitere wichtige Abweichungen:**

- Blattnummerierung „1/3“ ist nur beim PCT Pflicht (USPTO: empfohlen; EPA: fortlaufend, Format offen; DPMA: nicht
  vorgeschrieben).
- Eine einzelne Figur erhält bei EPA, PCT und USPTO keine Nummer und keine „FIG.“-Beschriftung.
- USPTO: Bezugslinien dürfen sich nicht kreuzen; Explosionsdarstellungen brauchen eine Klammer; Letter-Format erlaubt
  (Nutzfläche dann 17,6 × 24,4 cm).
- DPMA: alternative Größen für die Zusammenfassungsfigur (8,1 × 9,4 cm oder 17,4 × 4,5 cm); Figuren zum Stand der
  Technik mit „Stand der Technik“ kennzeichnen.

Die Notiz leitet daraus ein **gemeinsames sicheres Profil** (strengster gemeinsamer Nenner) und 30 Regelideen für die
automatische **Vorabprüfung (Preflight, P1–P30)** mit den Schweregraden Fehler / Warnung / Hinweis / manuell ab.
Außerdem listet sie relevante Zeichnungsnormen (u. a. ISO 128-2/-3, ISO 3952, ISO 2203, ISO 8826, ISO 1219,
IEC 60617, ISO 5807, VDI 2127) und empfiehlt für die Symbolbibliothek: alle Symbole eigenständig nach
Funktionsbeschreibung zeichnen, nichts aus Norm-PDFs oder der IEC-Datenbank übernehmen und nur „im Stil von …“ statt
„konform zu ISO …“ angeben. Die Angaben sind eine Hilfestellung, keine Rechtsberatung.

## 2. Werkzeuglandschaft und Host-APIs

Volltext: [tools-and-host-apis.md](../../en/research/tools-and-host-apis.md) – Aussagen sind als **[V]** (geprüft),
**[K]** (nicht erneut geprüft) oder **[U]** (ungeprüft/widerlegt) gekennzeichnet.

- **Werkzeuge:** Übernommen werden vor allem Bedienideen (Hover-zum-Verbinden aus draw.io, „Move vs. Drag“ aus KiCad,
  Mehrfachansichten aus Fritzing, Getriebeschema mit Leistungsfluss nach KISSsoft/Romax, Einrasten kämmender
  Zahnräder wie in Zahnradgeneratoren über den Achsabstand a = m(z₁+z₂)/2). Lizenzhinweise: PlaneGCS und libavoid LGPL-2.1, Clipper2 BSL-1.0 (auch als
  C#), tldraw nicht Open Source, SolveSpace GPL-3, KiCad-Bibliotheken CC-BY-SA 4.0 mit Ausnahme.
- **Office.js (PowerPoint):** Ab Anforderungssatz 1.4 Formen und Linienstile, 1.3 Tags, 1.7 Custom XML Parts,
  1.8 Gruppen, 1.10 Drehung. Es gibt **keine Pfeilspitzen, keine Freiformen/Bézierkurven, keine Musterfüllung und kein
  Verbinder-Andocken**; kein Overlay über der Folie. Das iPad unterstützt praktisch nur 1.1, Office 2024 LTSC endet bei
  1.5. Auswege: SVG-Einfügen (XmlSvg), OOXML-Folieninjektion, einfache Formen über die API.
- **COM-Vorrang:** `EquivalentAddins` mit der ProgId des COM-Add-ins blendet das Web-Add-in unter Windows aus.
- **PowerPoint COM:** volle Formen-API (`BuildFreeform`, Pfeilspitzen, `BeginConnect`/`EndConnect`, `Shape.Tags`);
  Overlay über ein geschichtetes Fenster mit `PointsToScreenPixelsX/Y`. `FillFormat.Patterned` ist eine Bitmap und für
  Schraffuren in Amtszeichnungen ungeeignet – Schraffur als echte Liniengeometrie erzeugen.
- **LibreOffice:** Python-UNO-`.oxt` ist die praktikabelste Erweiterungsform; die .NET-UNO-Bindings eignen sich 2026
  nicht für plattformübergreifende Erweiterungen; eine eingebettete WebView gibt es nicht. Native Vektorschraffur über
  `FillHatch`; Metadaten über `ShapeUserDefinedAttributes` (nur ODF).

**Hinweis zur Empfehlung C.1:** Die Notiz empfiehlt einen gemeinsamen **TypeScript-Kern** mit dünnen nativen Adaptern.
Diese Empfehlung wurde geprüft und **nicht übernommen**: InventaSketch verwendet eine C#-Engine (im Prozess in COM, als
WebAssembly im Aufgabenbereich, als .NET-Prozess für LibreOffice), weil die Interaktion auf Folie und Zeichenfläche
synchrones Einrasten im Host-Prozess braucht. Siehe [ADR-0002](../../adr/0002-csharp-engine.md).
