# Host-Fähigkeitsmatrix

> Deutsche Übersetzung. Maßgeblich ist die englische Fassung: [../../en/hosts/capability-matrix.md](../../en/hosts/capability-matrix.md).

> Werte: **verifiziert** (getestet, mit Nachweis), **teilweise**, **nicht unterstützt**, **nicht getestet**. Eine Zelle
> wird nie ohne reproduzierbaren Test als verifiziert markiert.
> Die API-Fakten stammen aus [research/tools-and-host-apis.md](../../en/research/tools-and-host-apis.md) (englisch); sie
> gelten erst nach einem InventaSketch-Test auf dem echten Host als „verifiziert“.

| Fähigkeit | PowerPoint COM (Win) | PowerPoint-Web-Add-in (Win/Mac/Web) | LibreOffice (Win/Mac/Linux) |
|---|---|---|---|
| Native Rechtecke, Ellipsen, Linien, Text | verifiziert (2026-11-20, PowerPoint 16.0.20527, Smoke-Test) | nicht getestet (PowerPointApi 1.4) | verifiziert (2026-11-20, LibreOffice 26.8.2.2, Windows, `tests/integration_test.py`: Impress, Draw, Calc) |
| Native Freiform / Bézierkurve | verifiziert (BuildFreeform, Smoke-Test) | in Office.js nicht unterstützt → SVG-Bild oder Folieninjektion | verifiziert (`OpenBezierShape`/`ClosedBezierShape`, `PolyLineShape`/`PolyPolygonShape`; gebogener Pfeil, Zahnrad mit Zähnen, Bezugszeichen-Leitlinie) |
| Native Pfeilspitzen | verifiziert (gebogener Pfeil, Smoke-Test); nativ für Dreieck, offen, Stealth, Raute, Oval (Schrägstrich → offen, Halbpfeil → Dreieck); native Spitzen skalieren nur mit der Linienstärke; die Engine nutzt sie daher nur, wenn sie mindestens 65 % ihrer eigenen Spitzengröße erreichen (`DrawPlanBuilder.NativeHeadFits`), sonst zeichnet sie die Spitze als Formen – dünne Diagrammlinien und Rauten erhalten so gezeichnete Spitzen passend zu den hohlen | in Office.js nicht unterstützt → explizite Geometrie | verifiziert (2026-11-20): Linienenden mit der Spitzengeometrie der Engine für Dreieck, offen, Stealth, Raute, Oval (Schrägstrich → offen, Halbpfeil → Dreieck), unter Namen im OOXML-Stil (`msArrowEnd 9 isk` …) registriert, damit der .pptx-Export native PowerPoint-Pfeilspitzen schreibt (Dreieck in der .pptx geprüft); die Engine wendet dieselbe 65-%-Regel an (`NativeHeadFits`) |
| Hohle UML-Spitzen (`triangleOpen`, `diamondOpen`) | teilweise: kein msoArrowhead-Gegenstück → geschlossene Freiform ohne Füllung, Linie bis zur Basis gekürzt (verifiziert 2026-11-20: UML-Generalisierung, -Realisierung, -Aggregation; hohle und gefüllte Raute gleich groß) | teilweise: gedrehte geometrische Form Dreieck/Raute ohne Füllung (Drehung PowerPointApi 1.10; Plan per Unit-Test geprüft, auf dem Host nicht getestet) | verifiziert 2026-11-20: von der Engine als geschlossenes Polygon ohne Füllung gezeichnet (UML-Generalisierung) |
| Krähenfuß-Enden (`crowOne`, `crowOneOnly`, `crowZeroOne`, `crowMany`, `crowOneMany`, `crowZeroMany`) | teilweise: Linien, offene Freiform als Fuß und ungefüllte Ovale; ein Kreis unterbricht die Linie (verifiziert 2026-11-20 an ER-Verbindern) | teilweise: Linien und ungefüllte Ellipsen (Plan per Unit-Test geprüft, auf dem Host nicht getestet) | nicht getestet (von der Engine als Formen gezeichnet) |
| Text unterstrichen | verifiziert 2026-11-20 (`Font.Underline`, ER-Primärschlüssel) | nicht getestet (`ShapeFont.underline`, PowerPointApi 1.4; keine gezeichnete Ersatzlinie nötig) | nicht getestet (`CharUnderline`, Abbildung per Unit-Test geprüft) |
| Native Muster-/Schraffurfüllung | teilweise: explizite Schraffurlinien (verifiziert); Musterfüllung nicht verwendet | nicht unterstützt → explizite Schraffurlinien | verifiziert 2026-11-20 (`FillHatch` einfach/kreuz mit optionaler Hintergrundfarbe; Schraffurlinien sind Haarlinien; .pptx-Export → vordefinierte Musterfüllung) |
| Gruppieren | verifiziert | nicht getestet (PowerPointApi 1.8) | verifiziert (`XShapeGrouper`; Gruppen haben keine `UserDefinedAttributes`, sie stehen an der ersten Einzelform) |
| Drehung | verifiziert 2026-11-20 (Drehen mit Fangen zeichnet aus dem Modell neu) | nicht getestet (PowerPointApi 1.10) | teilweise: eine native Drehung der Einheit wird beim Neuzeichnen ins Modell übernommen (verifiziert 2026-11-20, 90°); gedrehte Plan-Ops über `RotateAngle` auf dem Host nicht getestet |
| Form-Tags / Metadaten | verifiziert (ISK_ID, ISK_TYPE, ISK_MODEL geschrieben) | nicht getestet (tags, 1.3) | verifiziert 2026-11-20: Name `ISK:<typ>:<id>` (.odp und .pptx), `UserDefinedAttributes` (nur .odp) und Dokumenteigenschaften `ISK_MODEL_<id>` (.odp und .pptx); Bearbeiten nach dem Laden aus beiden Formaten |
| Auswahl lesen/schreiben | verifiziert 2026-11-20 (Auswahl wird in die Seitenleiste geladen, eingefügte Kopien erhalten neue IDs) | nicht getestet (1.5/1.6) | verifiziert 2026-11-20 (ausgewählte Einheit wird zum Bearbeiten geladen, neue Einheiten werden nach dem Einfügen ausgewählt) |
| Overlay auf der Folie, schwebende Leiste, Kreismenü | teilweise: Einfügen am Mauszeiger, Ziehen und Skalieren mit Einrasten, Zwei-Punkt-Werkzeug, Punktbearbeitung, Kreismenü und ausblendende schwebende Leiste verifiziert 2026-11-20 | nicht unterstützt → Arbeitsfläche in der Seitenleiste | geplant (UNO-Handler + Overlay-Ebene) |
| Editor in Seitenleiste / Aufgabenbereich | verifiziert 2026-11-20 (WebView2-Aufgabenbereich, Live-Abgleich in beide Richtungen) | in Arbeit | teilweise: Deck der Seitenleiste öffnet mit Bibliotheksliste, Vorschau und den Ansichten der ausgewählten Einheit (verifiziert 2026-11-20, Impress, Bildschirmfotos); Suchfilter per Unit-Test geprüft; Parameter und Stil noch nicht bearbeitbar |
| SVG einfügen | entfällt | nicht getestet (XmlSvg-Coercion; nicht auf dem iPad) | nicht getestet |
| COM-Vorrang vor dem Web-Add-in | nicht getestet (`EquivalentAddins`) | nicht getestet | entfällt |
| Automatische Erkennung hell/dunkel | geplant | in Arbeit | geplant |
| Automatische Erkennung Deutsch/Englisch | verifiziert (LCID der Office-Oberfläche, Deutsch erkannt) | in Arbeit | verifiziert 2026-11-20 (`ooLocale` der Oberfläche; deutsche Oberfläche erkannt, deutsche Katalognamen) |
| Einfügefeld (Suche, passende Teile zuerst, Strg+Leertaste) | geprüft 24.11.2026 (Szenario `ux-round`) | teilweise: umgesetzt und auf der Browser-Entwicklungsseite geprüft, nicht in PowerPoint im Web getestet | teilweise: Suche im Bibliotheks-Panel (Engine `search`, passende Teile zuerst), geprüft 25.11.2026 (`integration_test.py`, `ui_smoke.py`); ohne Strg+Leertaste (braucht das Overlay) |
| Spiegeln/Drehen nur mit Wirkung | geprüft 24.11.2026 (`ux-round`, Knopf-IDs) | teilweise: umgesetzt, Browser-Entwicklungsseite | geprüft 25.11.2026 (Auswahl-Panel, `integration_test.py`) |
| Leiste für Mehrfachauswahl (eine Ansicht für alle, ausrichten, verteilen, wie das erste, neu nummerieren, Maß) | geprüft 24.11.2026 (`ux-round`, `parts-list`, `building-services`) | teilweise: umgesetzt, Browser-Entwicklungsseite | geprüft 25.11.2026 (`integration_test.py`, `ui_smoke.py`) (Auswahl-Panel, ein Rückgängig-Schritt) |
| Ansichtswechsel von Baugruppen (Getriebezüge Stirn ↔ Schema, Haustechnik Grundriss ↔ Schnitt ↔ Schema) | geprüft 24.11.2026 (`ux-round`, `building-services`) | teilweise: über die Mehrfachauswahl, Browser-Entwicklungsseite | geplant |
| Folienstil: ISO-128-Liniengruppe, Akzentfarbe (und Schwarz-Weiß) für alle Elemente, auch neue | geprüft 25.11.2026 (Szenario `line-group`: 1,42 → 2,84 pt bei 1 mm, ein neues Element übernimmt sie; Menüband „Linienstärken“, Folien-Tag `ISK_STYLE`) | teilweise: Auswahl `setLineGroup` in der oberen Leiste, Browser-Entwicklungsseite | nicht angeboten (die Engine-Einstellung wird aus dem Folienmodell übernommen) |
| P2-Module: Verfahrens- und Wärmetechnik, Energiesysteme, Robotik (einfügen, native Formen) | geprüft 25.11.2026 (Szenario `p2-modules`) | über den Engine-Katalog, nicht getestet | über den Engine-Katalog, nicht getestet |
| Leitungskreuzungen über Module springen (Fluid, Elektro, Haustechnik, Verfahrenstechnik, Energie) | teilweise: Engine-Tests; Szenario `crossings` (Drähte) grün | über die Engine | über die Engine |
| Werkzeug mit „+“ an einen Roboterflansch, zur Werkzeugachse gedreht | geprüft 25.11.2026 (Szenario `robot-tool`) | Anbau-„+“ (Port-Art `Mount`), nicht getestet | Port-Liste (`portMount`), nicht getestet |
| Diagramm-Layout verbundener Elemente (Ebenen, Verbinder docken um) | geprüft 25.11.2026 (Szenario `diagram-layout`, auch Ausrichten behält die Verbinder) | über die Engine (`arrangeOptions`), nicht getestet | über die Engine (Auswahl-Panel), nicht getestet |
| Einrasten nach dem Ziehen, Strg+Leertaste, Kontextmenü | geprüft | teilweise (Browser-Entwicklungsseite) | geprüft 25.11.2026 (`ui_smoke.py`, echte Eingabe; Einrasten nach dem Loslassen, keine Hilfslinien beim Ziehen) |
| Patentprüfung aller Folien mit Sprung zum Befund | geprüft 25.11.2026 (Szenario `patent-check`) | Panel in der oberen Leiste für die Seite (auf der Browser-Entwicklungsseite geprüft; noch nicht über alle Folien) | Dialog über alle Seiten mit Sprung zum Befund (Menü; `integration_test.py`, `ui_smoke.py`) |
| Varianten: Folie mit der vorherigen vergleichen (neu / geändert / entfernt markiert) | geprüft 25.11.2026 (Szenario `variants`, Menü Extras) | Engine `diff` (noch ohne Befehl) | Menübefehl, geprüft 25.11.2026 (`integration_test.py`) |
| Import CSV (Diagrammdaten), GeoJSON/GPX (Karte) | geprüft 25.11.2026 (Szenario `import`) | Import-Knopf (Engine `importData`/`importGeo`), nicht in PowerPoint getestet | nicht angeboten |
| Live-Vorschau der Kopplung beim Ziehen | teilweise: umgesetzt 24.11.2026, per Auge geprüft (blauer Geist), ohne Szenario-Prüfung | teilweise: umgesetzt 25.11.2026, Browser-Entwicklungsseite | nicht unterstützt (braucht das Overlay; keine Live-Zieh-Ereignisse) |
| Bewegungsmodus (Getriebezüge und Getriebe drehen, selbst gebaute Koppelgetriebe ziehen) | geprüft (`gear-motion`, `linkage-build`) | teilweise: umgesetzt 25.11.2026, Browser-Entwicklungsseite | teilweise: Getriebezüge um einen getippten Winkel drehen (`motionTurn`), geprüft 25.11.2026 (`integration_test.py`, `ui_smoke.py`); kein Ziehen |
| Koppelkurve und Zwischenlagen selbst gebauter Koppelgetriebe | geprüft 24.11.2026 (`linkage-extras`) | teilweise: beides auf der Browser-Entwicklungsseite | geprüft 25.11.2026 (Auswahl-Panel, `integration_test.py`) |
| Stückliste, die den Teilen folgt | geprüft 24.11.2026 (`parts-list`) | nicht getestet (Engine zeichnet Abhängige neu) | nicht getestet |

Reine Engine-Fähigkeiten (alle Hosts): SVG-Export durch Unit-Tests **verifiziert** (`SvgContainsHatchAndArrowheadAndIsMonochrome`,
`DrawingTests`: jede Pfeilspitze, hohle Spitzen, Krähenfuß-Enden und Unterstreichung per `text-decoration`, auch in
Schwarzweiß), semantisches Einrasten durch Unit-Tests **verifiziert** (`SnappingTests`), JSON-Persistenz **verifiziert**
(`PersistenceTests`).

Absenken von Pfeilspitzen (Entscheidung): Eine Spitze wird nur dann zum Linienende-Attribut, wenn der Host ein
passendes natives Linienende hat (`DrawPlanBuilder.HasNativeLineEnd`). PowerPoint kennt weder hohles Dreieck noch hohle
Raute noch Krähenfuß; diese werden daher immer als explizite native Formen mit derselben Geometrie wie in der SVG-Ausgabe
gezeichnet, auch im COM-Add-in; eine Linie kann eine native Anfangsspitze und eine abgesenkte Endspitze tragen. Mit
Freiformen (COM) ist die abgesenkte Spitze exakt; ohne (Office.js) werden Dreiecke und Rauten zu gedrehten
geometrischen Formen, alles andere zu Linien und Ellipsen. Hohle Spitzen sind ungefüllt und die Linie endet an ihrer
Basis, es wird also keine Hintergrundfarbe angenommen. GDI+-Vorschaubilder und die Vorschau auf der Folie zeichnen jede
Spitze aus derselben Engine-Geometrie.
