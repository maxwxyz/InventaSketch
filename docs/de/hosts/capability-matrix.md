# Host-Fähigkeitsmatrix

> Deutsche Übersetzung. Maßgeblich ist die englische Fassung: [../../en/hosts/capability-matrix.md](../../en/hosts/capability-matrix.md).

> Werte: **verifiziert** (getestet, mit Nachweis), **teilweise**, **nicht unterstützt**, **nicht getestet**. Eine Zelle
> wird nie ohne reproduzierbaren Test als verifiziert markiert.
> Die API-Fakten stammen aus [research/tools-and-host-apis.md](../../en/research/tools-and-host-apis.md) (englisch); sie
> gelten erst nach einem InventaSketch-Test auf dem echten Host als „verifiziert“.

| Fähigkeit | PowerPoint COM (Win) | PowerPoint-Web-Add-in (Win/Mac/Web) | LibreOffice (Win/Mac/Linux) |
|---|---|---|---|
| Native Rechtecke, Ellipsen, Linien, Text | verifiziert (2026-11-20, PowerPoint 16.0.20527, Smoke-Test) | nicht getestet (PowerPointApi 1.4) | nicht getestet (UNO-Formen) |
| Native Freiform / Bézierkurve | verifiziert (BuildFreeform, Smoke-Test) | in Office.js nicht unterstützt → SVG-Bild oder Folieninjektion | nicht getestet (PolyPolygonBezierShape) |
| Native Pfeilspitzen | verifiziert (gebogener Pfeil, Smoke-Test); nativ für Dreieck, offen, Stealth, Raute, Oval (Schrägstrich → offen, Halbpfeil → Dreieck); native Spitzen skalieren nur mit der Linienstärke; die Engine nutzt sie daher nur, wenn sie mindestens 65 % ihrer eigenen Spitzengröße erreichen (`DrawPlanBuilder.NativeHeadFits`), sonst zeichnet sie die Spitze als Formen – dünne Diagrammlinien und Rauten erhalten so gezeichnete Spitzen passend zu den hohlen | in Office.js nicht unterstützt → explizite Geometrie | nicht getestet (LineStart/LineEnd) |
| Hohle UML-Spitzen (`triangleOpen`, `diamondOpen`) | teilweise: kein msoArrowhead-Gegenstück → geschlossene Freiform ohne Füllung, Linie bis zur Basis gekürzt (Zeichenplan per Unit-Test geprüft, auf dem Host nicht getestet) | teilweise: gedrehte geometrische Form Dreieck/Raute ohne Füllung (Drehung PowerPointApi 1.10; Plan per Unit-Test geprüft, auf dem Host nicht getestet) | nicht getestet |
| Krähenfuß-Enden (`crowOne`, `crowOneOnly`, `crowZeroOne`, `crowMany`, `crowOneMany`, `crowZeroMany`) | teilweise: Linien, offene Freiform als Fuß und ungefüllte Ovale; ein Kreis unterbricht die Linie (Plan per Unit-Test geprüft, auf dem Host nicht getestet) | teilweise: Linien und ungefüllte Ellipsen (Plan per Unit-Test geprüft, auf dem Host nicht getestet) | nicht getestet |
| Text unterstrichen | nicht getestet (`Font.Underline`) | nicht getestet (`ShapeFont.underline`, PowerPointApi 1.4; keine gezeichnete Ersatzlinie nötig) | nicht getestet (CharUnderline) |
| Native Muster-/Schraffurfüllung | teilweise: explizite Schraffurlinien (verifiziert); Musterfüllung nicht verwendet | nicht unterstützt → explizite Schraffurlinien | nicht getestet (FillHatch) |
| Gruppieren | verifiziert | nicht getestet (PowerPointApi 1.8) | nicht getestet (XShapeGrouper) |
| Drehung | nicht getestet | nicht getestet (PowerPointApi 1.10) | nicht getestet |
| Form-Tags / Metadaten | verifiziert (ISK_ID, ISK_TYPE, ISK_MODEL geschrieben) | nicht getestet (tags, 1.3) | nicht getestet (Formname + Dokumenteigenschaften) |
| Auswahl lesen/schreiben | nicht getestet | nicht getestet (1.5/1.6) | nicht getestet |
| Overlay auf der Folie, schwebende Leiste, Kreismenü | teilweise: Einfügen am Mauszeiger, Ziehen und Skalieren mit Einrasten, Zwei-Punkt-Werkzeug, Punktbearbeitung, Kreismenü und ausblendende schwebende Leiste verifiziert 2026-11-20 | nicht unterstützt → Arbeitsfläche in der Seitenleiste | geplant (UNO-Handler + Overlay-Ebene) |
| Editor in Seitenleiste / Aufgabenbereich | verifiziert 2026-11-20 (WebView2-Aufgabenbereich, Live-Abgleich in beide Richtungen) | in Arbeit | geplant (Seitenleisten-Panel) |
| SVG einfügen | entfällt | nicht getestet (XmlSvg-Coercion; nicht auf dem iPad) | nicht getestet |
| COM-Vorrang vor dem Web-Add-in | nicht getestet (`EquivalentAddins`) | nicht getestet | entfällt |
| Automatische Erkennung hell/dunkel | geplant | in Arbeit | geplant |
| Automatische Erkennung Deutsch/Englisch | verifiziert (LCID der Office-Oberfläche, Deutsch erkannt) | in Arbeit | geplant |

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
